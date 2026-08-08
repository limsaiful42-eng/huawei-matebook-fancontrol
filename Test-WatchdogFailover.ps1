#Requires -Version 5.1
#Requires -RunAsAdministrator

[CmdletBinding()]
param(
    [ValidateSet(3800, 5100, 6300)]
    [int] $TestRPM = 5100
)

$ErrorActionPreference = 'Stop'
$runtimePath = Join-Path $PSScriptRoot 'runtime'
if (-not (Test-Path -LiteralPath $runtimePath)) {
    New-Item -ItemType Directory -Path $runtimePath -Force | Out-Null
}
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$heartbeatPath = Join-Path $runtimePath "watchdog-test-heartbeat-$stamp.txt"
$stopPath = Join-Path $runtimePath "watchdog-test-stop-$stamp.signal"
$watchdogLogPath = Join-Path $runtimePath "watchdog-test-$stamp.log"
$summaryPath = Join-Path $runtimePath "watchdog-test-$stamp.json"
$manualModeEntered = $false
$fallbackRestoreUsed = $false
$testError = $null
$before = $null
$manual = $null
$recovery = $null

$oemWmi = Get-CimInstance -Namespace 'root\wmi' -ClassName OemWMIMethod |
    Where-Object { $_.InstanceName -eq 'ACPI\PNP0C14\HWMI_0' } |
    Select-Object -First 1
if (-not $oemWmi) {
    throw 'Huawei OemWMIMethod instance was not found.'
}

function Invoke-HuaweiOemMethod([byte[]] $Command) {
    $buffer = [byte[]]::new(64)
    [Array]::Copy($Command, $buffer, $Command.Count)
    $result = Invoke-CimMethod -InputObject $oemWmi -MethodName OemWMIfun -Arguments @{ u8Input = $buffer }
    if (-not $result.u8Output) { throw 'Huawei BIOS WMI method returned no output.' }
    [byte[]] $result.u8Output
}

function Assert-HuaweiSuccess([byte[]] $Command) {
    $output = Invoke-HuaweiOemMethod -Command $Command
    if ($output.Count -lt 1 -or $output[0] -ne 0) {
        $status = if ($output.Count) { '0x{0:X2}' -f $output[0] } else { 'missing' }
        throw "Huawei BIOS command failed with status $status."
    }
    $output
}

function Set-HuaweiFan([int] $Fan, [int] $RPM) {
    $low = [byte] ($RPM -band 0xFF)
    $high = [byte] (($RPM -shr 8) -band 0xFF)
    Assert-HuaweiSuccess -Command ([byte[]] @(0x02, 0x11, $Fan, 0x01, $low, $high)) | Out-Null
}

function Disable-HuaweiFanManualMode {
    Assert-HuaweiSuccess -Command ([byte[]] @(0x02, 0x11, 0x00, 0x00, 0x00, 0x00)) | Out-Null
}

function Get-HuaweiSample {
    $temperatureOutput = Assert-HuaweiSuccess -Command ([byte[]] @(0x02, 0x02, 0x05))
    $rpms = foreach ($fan in 0, 1) {
        $output = Assert-HuaweiSuccess -Command ([byte[]] @(0x02, 0x08, $fan))
        [int] $output[1] -bor ([int] $output[2] -shl 8)
    }
    [pscustomobject]@{
        Timestamp = (Get-Date).ToString('o')
        TemperatureC = [int] $temperatureOutput[2]
        Fan0RPM = $rpms[0]
        Fan1RPM = $rpms[1]
    }
}

try {
    $before = Get-HuaweiSample
    Set-HuaweiFan -Fan 0 -RPM $TestRPM
    $manualModeEntered = $true
    Set-HuaweiFan -Fan 1 -RPM $TestRPM
    Start-Sleep -Seconds 8
    $manual = Get-HuaweiSample

    'stale' | Out-File -LiteralPath $heartbeatPath -Encoding ascii -Force
    (Get-Item -LiteralPath $heartbeatPath).LastWriteTime = (Get-Date).AddMinutes(-2)
    $watchdogPath = Join-Path $PSScriptRoot 'HuaweiFan-Watchdog.ps1'
    $arguments = @(
        '-NoProfile', '-ExecutionPolicy', 'Bypass',
        '-File', ('"{0}"' -f $watchdogPath),
        '-HeartbeatPath', ('"{0}"' -f $heartbeatPath),
        '-StopPath', ('"{0}"' -f $stopPath),
        '-LogPath', ('"{0}"' -f $watchdogLogPath),
        '-ControllerProcessId', 2147483000,
        '-TimeoutSeconds', 6
    )
    $watchdog = Start-Process -FilePath 'powershell.exe' -ArgumentList $arguments -WindowStyle Hidden -PassThru
    if (-not $watchdog.WaitForExit(20000)) {
        throw 'Watchdog did not finish within 20 seconds.'
    }
    if ($watchdog.ExitCode -ne 0) {
        throw "Watchdog returned exit code $($watchdog.ExitCode)."
    }
    # The EC ramps large downward changes gradually; allow the physical fans to coast down.
    Start-Sleep -Seconds 25
    $recovery = Get-HuaweiSample
}
catch {
    $testError = $_.Exception.Message
}
finally {
    if ($manualModeEntered -and ($testError -or -not (Test-Path -LiteralPath $watchdogLogPath) -or
        -not (Select-String -LiteralPath $watchdogLogPath -SimpleMatch 'Restore succeeded' -Quiet))) {
        try {
            Disable-HuaweiFanManualMode
            $fallbackRestoreUsed = $true
        }
        catch {
            if (-not $testError) { $testError = $_.Exception.Message }
        }
    }
}

$watchdogRestored = (Test-Path -LiteralPath $watchdogLogPath) -and
    (Select-String -LiteralPath $watchdogLogPath -SimpleMatch 'Restore succeeded' -Quiet)
$manualObserved = $manual -and $manual.Fan0RPM -ge ($TestRPM - 400) -and $manual.Fan1RPM -ge ($TestRPM - 400)
$recoveryObserved = $recovery -and $recovery.Fan0RPM -le ($TestRPM - 600) -and $recovery.Fan1RPM -le ($TestRPM - 600)
$summary = [ordered]@{
    Timestamp = (Get-Date).ToString('o')
    TestRPM = $TestRPM
    ManualModeEntered = $manualModeEntered
    ManualTargetObserved = [bool] $manualObserved
    WatchdogRestoreSucceeded = [bool] $watchdogRestored
    VendorRecoveryObserved = [bool] $recoveryObserved
    FallbackRestoreUsed = $fallbackRestoreUsed
    Before = $before
    Manual = $manual
    Recovery = $recovery
    Error = $testError
    WatchdogLogPath = $watchdogLogPath
}
$summary | ConvertTo-Json -Depth 4 | Out-File -LiteralPath $summaryPath -Encoding utf8
$summary | ConvertTo-Json -Depth 4
Write-Host "Summary: $summaryPath"
if ($testError -or -not $manualObserved -or -not $watchdogRestored -or -not $recoveryObserved -or $fallbackRestoreUsed) {
    exit 1
}
exit 0
