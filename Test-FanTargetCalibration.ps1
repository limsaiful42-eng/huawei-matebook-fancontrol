#Requires -Version 5.1
#Requires -RunAsAdministrator

[CmdletBinding()]
param(
    [ValidateRange(10, 60)][int] $SecondsPerTarget = 20,
    [ValidateRange(1, 5)][int] $SampleSeconds = 2,
    [ValidateRange(75, 95)][int] $EmergencyTemperatureC = 85
)

$ErrorActionPreference = 'Stop'
$targets = @(7400, 9300, 12000)
$runtimePath = Join-Path $PSScriptRoot 'runtime'
if (-not (Test-Path -LiteralPath $runtimePath)) {
    New-Item -ItemType Directory -Path $runtimePath -Force | Out-Null
}
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$csvPath = Join-Path $runtimePath "target-calibration-$stamp.csv"
$summaryPath = Join-Path $runtimePath "target-calibration-$stamp.json"
$heartbeatPath = Join-Path $runtimePath "target-calibration-heartbeat-$stamp.txt"
$stopPath = Join-Path $runtimePath "target-calibration-stop-$stamp.signal"
$watchdogLogPath = Join-Path $runtimePath "target-calibration-watchdog-$stamp.log"
$rows = [System.Collections.Generic.List[object]]::new()
$manualModeEntered = $false
$restoreSucceeded = $false
$watchdog = $null
$testError = $null

$oemWmi = Get-CimInstance -Namespace 'root\wmi' -ClassName OemWMIMethod |
    Where-Object { $_.InstanceName -eq 'ACPI\PNP0C14\HWMI_0' } |
    Select-Object -First 1
if (-not $oemWmi) { throw 'Huawei OemWMIMethod instance was not found.' }

function Invoke-HuaweiMethod([byte[]] $Command) {
    $buffer = [byte[]]::new(64)
    [Array]::Copy($Command, $buffer, $Command.Count)
    $result = Invoke-CimMethod -InputObject $oemWmi -MethodName OemWMIfun -Arguments @{ u8Input = $buffer }
    if (-not $result.u8Output -or $result.u8Output[0] -ne 0) {
        $status = if ($result.u8Output) { '0x{0:X2}' -f $result.u8Output[0] } else { 'missing' }
        throw "Huawei BIOS command failed with status $status."
    }
    [byte[]] $result.u8Output
}

function Set-BothFans([int] $RequestedRPM) {
    $low = [byte] ($RequestedRPM -band 0xFF)
    $high = [byte] (($RequestedRPM -shr 8) -band 0xFF)
    foreach ($fan in 0, 1) {
        Invoke-HuaweiMethod -Command ([byte[]] @(0x02, 0x11, $fan, 0x01, $low, $high)) | Out-Null
        $script:manualModeEntered = $true
    }
}

function Restore-VendorControl {
    Invoke-HuaweiMethod -Command ([byte[]] @(0x02, 0x11, 0x00, 0x00, 0x00, 0x00)) | Out-Null
}

function Get-Sample([string] $Phase, [int] $RequestedRPM) {
    $temperature = Invoke-HuaweiMethod -Command ([byte[]] @(0x02, 0x02, 0x05))
    $fanValues = foreach ($fan in 0, 1) {
        $output = Invoke-HuaweiMethod -Command ([byte[]] @(0x02, 0x08, $fan))
        [int] $output[1] -bor ([int] $output[2] -shl 8)
    }
    $row = [pscustomobject]@{
        Timestamp = Get-Date
        Phase = $Phase
        RequestedRPM = $RequestedRPM
        TemperatureC = [int] $temperature[2]
        Fan0RPM = $fanValues[0]
        Fan1RPM = $fanValues[1]
    }
    $rows.Add($row)
    Write-Host ('{0:HH:mm:ss} | {1,-8} | Request {2,5} | {3,2} C | Fan0 {4,5} | Fan1 {5,5}' -f
        $row.Timestamp, $row.Phase, $row.RequestedRPM, $row.TemperatureC, $row.Fan0RPM, $row.Fan1RPM)
    if ($row.TemperatureC -ge $EmergencyTemperatureC) {
        throw "Emergency temperature threshold reached: $($row.TemperatureC) C."
    }
    (Get-Date).ToString('o') | Out-File -LiteralPath $heartbeatPath -Encoding ascii -Force
}

try {
    (Get-Date).ToString('o') | Out-File -LiteralPath $heartbeatPath -Encoding ascii -Force
    $watchdogPath = Join-Path $PSScriptRoot 'HuaweiFan-Watchdog.ps1'
    $arguments = @(
        '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', ('"{0}"' -f $watchdogPath),
        '-HeartbeatPath', ('"{0}"' -f $heartbeatPath), '-StopPath', ('"{0}"' -f $stopPath),
        '-LogPath', ('"{0}"' -f $watchdogLogPath), '-ControllerProcessId', $PID,
        '-TimeoutSeconds', 12
    )
    $watchdog = Start-Process powershell.exe -ArgumentList $arguments -WindowStyle Hidden -PassThru

    Get-Sample -Phase 'Baseline' -RequestedRPM 0
    foreach ($target in $targets) {
        Set-BothFans -RequestedRPM $target
        $sampleCount = [Math]::Ceiling($SecondsPerTarget / $SampleSeconds)
        for ($index = 0; $index -lt $sampleCount; $index++) {
            Start-Sleep -Seconds $SampleSeconds
            Get-Sample -Phase "Target$target" -RequestedRPM $target
        }
    }
}
catch {
    $testError = $_.Exception.Message
    Write-Warning $testError
}
finally {
    if ($manualModeEntered) {
        try {
            Restore-VendorControl
            $restoreSucceeded = $true
            'normal-stop' | Out-File -LiteralPath $stopPath -Encoding ascii -Force
            if ($watchdog) { $watchdog.WaitForExit(5000) | Out-Null }
        }
        catch {
            if (-not $testError) { $testError = $_.Exception.Message }
        }
    }
}

if ($restoreSucceeded) {
    for ($index = 0; $index -lt 5; $index++) {
        Start-Sleep -Seconds $SampleSeconds
        Get-Sample -Phase 'Recovery' -RequestedRPM 0
    }
}
$rows | Export-Csv -LiteralPath $csvPath -NoTypeInformation -Encoding UTF8
$results = foreach ($target in $targets) {
    $targetRows = @($rows | Where-Object RequestedRPM -eq $target)
    $steadyRows = @($targetRows | Select-Object -Last ([Math]::Min(5, $targetRows.Count)))
    [pscustomobject]@{
        RequestedRPM = $target
        Samples = $targetRows.Count
        Fan0SteadyAverageRPM = [Math]::Round(($steadyRows | Measure-Object Fan0RPM -Average).Average)
        Fan1SteadyAverageRPM = [Math]::Round(($steadyRows | Measure-Object Fan1RPM -Average).Average)
        Fan0MaximumRPM = ($targetRows | Measure-Object Fan0RPM -Maximum).Maximum
        Fan1MaximumRPM = ($targetRows | Measure-Object Fan1RPM -Maximum).Maximum
    }
}
$summary = [ordered]@{
    Timestamp = (Get-Date).ToString('o')
    SecondsPerTarget = $SecondsPerTarget
    Results = @($results)
    RestoreSucceeded = $restoreSucceeded
    TemperatureMaxC = ($rows | Measure-Object TemperatureC -Maximum).Maximum
    Error = $testError
    CsvPath = $csvPath
    WatchdogLogPath = $watchdogLogPath
}
$summary | ConvertTo-Json -Depth 5 | Out-File -LiteralPath $summaryPath -Encoding UTF8
$summary | ConvertTo-Json -Depth 5
Write-Host "Summary: $summaryPath"
if ($testError -or -not $restoreSucceeded) { exit 1 }
exit 0
