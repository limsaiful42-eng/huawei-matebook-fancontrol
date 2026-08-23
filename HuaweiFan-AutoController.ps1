#Requires -Version 5.1

[CmdletBinding()]
param(
    [string] $CurvePath,

    [ValidateRange(1, 10)]
    [int] $SampleSeconds = 3,

    [ValidateRange(1, 8)]
    [int] $HysteresisC = 3,

    [ValidateRange(0, 1440)]
    [int] $MaxMinutes = 0,

    [ValidateRange(75, 95)]
    [int] $EmergencyTemperatureC = 85,

    [ValidateSet(0, 3200, 3800, 5100, 6300, 7400, 9300, 9800, 10500, 11200, 11600, 12000)]
    [int] $Fan0RPM = 0,

    [ValidateSet(0, 3200, 3800, 5100, 6300, 7400, 9300, 9800, 10500, 11200, 11600, 12000)]
    [int] $Fan1RPM = 0,

    [string] $StopSignalPath,

    [switch] $Apply
)

$ErrorActionPreference = 'Stop'
if (-not $CurvePath) {
    $CurvePath = Join-Path $PSScriptRoot 'quiet-balanced-curve.json'
}

$allowedTargets = @(3200, 3800, 5100, 6300, 7400, 9300, 9800, 10500, 11200, 11600, 12000)
$runtimePath = Join-Path $PSScriptRoot 'runtime'
if (-not (Test-Path -LiteralPath $runtimePath)) {
    New-Item -ItemType Directory -Path $runtimePath -Force | Out-Null
}

$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$csvPath = Join-Path $runtimePath "fan-controller-$stamp.csv"
$summaryPath = Join-Path $runtimePath "fan-controller-$stamp.json"
$heartbeatPath = Join-Path $runtimePath "heartbeat-$stamp.txt"
$stopPath = Join-Path $runtimePath "stop-$stamp.signal"
$watchdogLogPath = Join-Path $runtimePath "watchdog-$stamp.log"
$rows = [System.Collections.Generic.List[object]]::new()
$manualModeEntered = $false
$restoreSucceeded = $false
$watchdogStarted = $false
$watchdogProcess = $null
$controllerError = $null
$controlsApplied = 0
$currentPointIndex = -1
$downshiftSamples = 0
$lowTachSamples = 0
$startedAt = Get-Date
$controllerMutex = [System.Threading.Mutex]::new($false, 'Global\HuaweiMateBookFanControlController')
$controllerMutexOwned = $false
try {
    $controllerMutexOwned = $controllerMutex.WaitOne(0, $false)
}
catch [System.Threading.AbandonedMutexException] {
    $controllerMutexOwned = $true
}
if (-not $controllerMutexOwned) {
    throw 'Another Huawei fan controller is already running.'
}

function Test-IsAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Import-FanCurve([string] $Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Fan curve was not found: $Path"
    }
    $curve = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    $points = @($curve.Points | Sort-Object { [int] $_.MinTemperatureC })
    if (-not $curve.Name -or $points.Count -lt 2) {
        throw 'A fan curve needs a name and at least two points.'
    }
    if ([int] $points[0].MinTemperatureC -gt 0) {
        throw 'The first fan-curve threshold must be 0 C or lower.'
    }
    $lastTemperature = [int]::MinValue
    $lastTarget = 0
    foreach ($point in $points) {
        $temperature = [int] $point.MinTemperatureC
        $target = [int] $point.TargetRPM
        if ($temperature -le $lastTemperature) {
            throw 'Fan-curve temperature thresholds must be unique and strictly increasing.'
        }
        if ($allowedTargets -notcontains $target) {
            throw "Unsupported target $target RPM. Allowed targets: $($allowedTargets -join ', ')."
        }
        if ($target -lt $lastTarget) {
            throw 'Fan-curve targets must not decrease as temperature rises.'
        }
        $lastTemperature = $temperature
        $lastTarget = $target
    }
    [pscustomobject]@{ Name = [string] $curve.Name; Points = $points }
}

function Get-DesiredPointIndex([int] $TemperatureC, [object[]] $Points) {
    $selected = 0
    for ($index = 0; $index -lt $Points.Count; $index++) {
        if ($TemperatureC -ge [int] $Points[$index].MinTemperatureC) {
            $selected = $index
        }
        else {
            break
        }
    }
    $selected
}

if ($Apply -and -not (Test-IsAdministrator)) {
    throw 'Apply mode must be run from an Administrator PowerShell window.'
}

$fixedTargetMode = ($Fan0RPM -ne 0 -or $Fan1RPM -ne 0)
if (($Fan0RPM -eq 0) -xor ($Fan1RPM -eq 0)) {
    throw 'Fixed control requires both -Fan0RPM and -Fan1RPM. Manual mode is global, so both fan targets must be explicit.'
}
if ($fixedTargetMode -and -not $Apply) {
    throw 'Fixed fan targets require -Apply.'
}
$curve = if ($fixedTargetMode) {
    [pscustomobject]@{
        Name = "Fixed targets: Fan0=$Fan0RPM RPM, Fan1=$Fan1RPM RPM"
        Points = @()
    }
}
else {
    Import-FanCurve -Path $CurvePath
}
$oemWmi = Get-CimInstance -Namespace 'root\wmi' -ClassName OemWMIMethod |
    Where-Object { $_.InstanceName -eq 'ACPI\PNP0C14\HWMI_0' } |
    Select-Object -First 1
if (-not $oemWmi) {
    throw 'Huawei OemWMIMethod instance ACPI\PNP0C14\HWMI_0 was not found.'
}

function Invoke-HuaweiOemMethod {
    param([Parameter(Mandatory)][byte[]] $Command)

    if ($Command.Count -gt 64) {
        throw 'The Huawei WMI input buffer is limited to 64 bytes.'
    }
    $buffer = [byte[]]::new(64)
    [Array]::Copy($Command, $buffer, $Command.Count)
    $result = Invoke-CimMethod -InputObject $oemWmi -MethodName OemWMIfun -Arguments @{ u8Input = $buffer }
    if (-not $result.u8Output) {
        throw 'Huawei BIOS WMI method returned no output.'
    }
    [byte[]] $result.u8Output
}

function Invoke-HuaweiOemRead {
    param([Parameter(Mandatory)][byte[]] $Command)

    $output = Invoke-HuaweiOemMethod -Command $Command
    if ($output.Count -lt 3 -or $output[0] -ne 0) {
        $status = if ($output.Count) { '0x{0:X2}' -f $output[0] } else { 'missing' }
        throw "Huawei BIOS WMI read failed with status $status."
    }
    $output
}

function Set-HuaweiFanRpm {
    param(
        [ValidateSet(0, 1)][int] $Fan,
        [int] $RPM
    )

    if ($allowedTargets -notcontains $RPM) {
        throw "Refusing undeclared fan target $RPM RPM."
    }
    $low = [byte] ($RPM -band 0xFF)
    $high = [byte] (($RPM -shr 8) -band 0xFF)
    # BIOS 1.22 SFND: 02 11 <fan> 01 <RPM low> <RPM high>.
    $output = Invoke-HuaweiOemMethod -Command ([byte[]] @(0x02, 0x11, $Fan, 0x01, $low, $high))
    if ($output.Count -lt 1 -or $output[0] -ne 0) {
        $status = if ($output.Count) { '0x{0:X2}' -f $output[0] } else { 'missing' }
        throw "Setting fan $Fan to $RPM RPM failed with status $status."
    }
}

function Set-HuaweiBothFans([int] $RPM) {
    Set-HuaweiFansRpm -Fan0TargetRPM $RPM -Fan1TargetRPM $RPM
}

function Set-HuaweiFansRpm {
    param(
        [int] $Fan0TargetRPM,
        [int] $Fan1TargetRPM
    )

    Set-HuaweiFanRpm -Fan 0 -RPM $Fan0TargetRPM
    $script:manualModeEntered = $true
    Set-HuaweiFanRpm -Fan 1 -RPM $Fan1TargetRPM
    $script:controlsApplied++
}

function Disable-HuaweiFanManualMode {
    # BIOS 1.22 SFND mode 0 maps to the vendor EC command 04 01 01 00.
    $output = Invoke-HuaweiOemMethod -Command ([byte[]] @(0x02, 0x11, 0x00, 0x00, 0x00, 0x00))
    if ($output.Count -lt 1 -or $output[0] -ne 0) {
        $status = if ($output.Count) { '0x{0:X2}' -f $output[0] } else { 'missing' }
        throw "Exiting Huawei fan test mode failed with status $status."
    }
}

function Get-HuaweiFanSample {
    $temperatureOutput = Invoke-HuaweiOemRead -Command ([byte[]] @(0x02, 0x02, 0x05))
    $temperature = [int] $temperatureOutput[2]
    if ($temperatureOutput[1] -eq 1) {
        $temperature = -$temperature
    }
    $rpms = foreach ($fan in 0, 1) {
        $fanOutput = Invoke-HuaweiOemRead -Command ([byte[]] @(0x02, 0x08, $fan))
        [int] $fanOutput[1] -bor ([int] $fanOutput[2] -shl 8)
    }
    [pscustomobject]@{
        TemperatureC = $temperature
        Fan0RPM = $rpms[0]
        Fan1RPM = $rpms[1]
    }
}

function Update-Heartbeat {
    (Get-Date).ToString('o') | Out-File -LiteralPath $heartbeatPath -Encoding ascii -Force
}

function Start-FanWatchdog {
    Update-Heartbeat
    $watchdogPath = Join-Path $PSScriptRoot 'HuaweiFan-Watchdog.ps1'
    if (-not (Test-Path -LiteralPath $watchdogPath)) {
        throw "Watchdog script was not found: $watchdogPath"
    }
    $arguments = @(
        '-NoProfile',
        '-ExecutionPolicy', 'Bypass',
        '-File', ('"{0}"' -f $watchdogPath),
        '-HeartbeatPath', ('"{0}"' -f $heartbeatPath),
        '-StopPath', ('"{0}"' -f $stopPath),
        '-LogPath', ('"{0}"' -f $watchdogLogPath),
        '-ControllerProcessId', $PID,
        '-TimeoutSeconds', 12
    )
    $script:watchdogProcess = Start-Process -FilePath 'powershell.exe' -ArgumentList $arguments -WindowStyle Hidden -PassThru
    $script:watchdogStarted = $true
}

try {
    Write-Host ("Curve: {0}; mode: {1}" -f $curve.Name, $(if ($Apply) { 'APPLY' } else { 'MONITOR ONLY' })) -ForegroundColor Cyan
    if ($Apply) {
        Start-FanWatchdog
    }

    while ($true) {
        if ($StopSignalPath -and (Test-Path -LiteralPath $StopSignalPath)) {
            Write-Host 'External stop signal received; restoring vendor control.' -ForegroundColor Yellow
            break
        }
        if ($MaxMinutes -gt 0 -and ((Get-Date) - $startedAt).TotalMinutes -ge $MaxMinutes) {
            break
        }

        $sample = Get-HuaweiFanSample
        $controlChanged = $false
        if ($fixedTargetMode) {
            $targetFan0RPM = $Fan0RPM
            $targetFan1RPM = $Fan1RPM
            if ($currentPointIndex -lt 0) {
                $currentPointIndex = 0
                Set-HuaweiFansRpm -Fan0TargetRPM $targetFan0RPM -Fan1TargetRPM $targetFan1RPM
                $controlChanged = $true
            }
        }
        else {
            $desiredIndex = Get-DesiredPointIndex -TemperatureC $sample.TemperatureC -Points $curve.Points
            if ($currentPointIndex -lt 0) {
                $currentPointIndex = $desiredIndex
                if ($Apply) {
                    Set-HuaweiBothFans -RPM ([int] $curve.Points[$currentPointIndex].TargetRPM)
                    $controlChanged = $true
                }
            }
            elseif ($desiredIndex -gt $currentPointIndex) {
                $currentPointIndex = $desiredIndex
                $downshiftSamples = 0
                if ($Apply) {
                    Set-HuaweiBothFans -RPM ([int] $curve.Points[$currentPointIndex].TargetRPM)
                    $controlChanged = $true
                }
            }
            elseif ($desiredIndex -lt $currentPointIndex -and
                $sample.TemperatureC -le ([int] $curve.Points[$currentPointIndex].MinTemperatureC - $HysteresisC)) {
                $downshiftSamples++
                if ($downshiftSamples -ge 3) {
                    $currentPointIndex = $desiredIndex
                    $downshiftSamples = 0
                    if ($Apply) {
                        Set-HuaweiBothFans -RPM ([int] $curve.Points[$currentPointIndex].TargetRPM)
                        $controlChanged = $true
                    }
                }
            }
            else {
                $downshiftSamples = 0
            }
            $targetFan0RPM = [int] $curve.Points[$currentPointIndex].TargetRPM
            $targetFan1RPM = $targetFan0RPM
        }

        $row = [pscustomobject]@{
            Timestamp = Get-Date
            TemperatureC = $sample.TemperatureC
            RequestedRPM = if ($targetFan0RPM -eq $targetFan1RPM) { $targetFan0RPM } else { $null }
            RequestedFan0RPM = $targetFan0RPM
            RequestedFan1RPM = $targetFan1RPM
            Fan0RPM = $sample.Fan0RPM
            Fan1RPM = $sample.Fan1RPM
            ControlChanged = $controlChanged
            ApplyMode = [bool] $Apply
        }
        $rows.Add($row)
        if ($row.RequestedRPM -ne $null) {
            Write-Host ('{0:HH:mm:ss} | {1,2} C | Request {2,5} | Fan0 {3,5} | Fan1 {4,5}{5}' -f
                $row.Timestamp, $row.TemperatureC, $row.RequestedRPM, $row.Fan0RPM, $row.Fan1RPM,
                $(if ($controlChanged) { ' | applied' } else { '' }))
        }
        else {
            Write-Host ('{0:HH:mm:ss} | {1,2} C | Request F0 {2,5} F1 {3,5} | Fan0 {4,5} | Fan1 {5,5}{6}' -f
                $row.Timestamp, $row.TemperatureC, $row.RequestedFan0RPM, $row.RequestedFan1RPM,
                $row.Fan0RPM, $row.Fan1RPM, $(if ($controlChanged) { ' | applied' } else { '' }))
        }

        if ($sample.TemperatureC -ge $EmergencyTemperatureC) {
            throw "Emergency temperature threshold reached: $($sample.TemperatureC) C."
        }

        if ($Apply -and ($sample.Fan0RPM -lt 1200 -or $sample.Fan1RPM -lt 1200)) {
            $lowTachSamples++
            if ($lowTachSamples -ge 2) {
                throw "Fan tachometer safety check failed: Fan0=$($sample.Fan0RPM), Fan1=$($sample.Fan1RPM)."
            }
        }
        else {
            $lowTachSamples = 0
        }

        if ($Apply) {
            Update-Heartbeat
        }
        Start-Sleep -Seconds $SampleSeconds
    }
}
catch {
    $controllerError = $_.Exception.Message
    Write-Warning $controllerError
}
finally {
    if ($manualModeEntered) {
        try {
            Disable-HuaweiFanManualMode
            $restoreSucceeded = $true
            Write-Host 'Vendor automatic fan control restored.' -ForegroundColor Green
        }
        catch {
            $restoreSucceeded = $false
            if (-not $controllerError) {
                $controllerError = $_.Exception.Message
            }
            Write-Warning "Primary restore failed; watchdog remains armed: $($_.Exception.Message)"
        }
    }
    elseif (-not $Apply) {
        $restoreSucceeded = $true
    }

    if ($watchdogStarted -and $restoreSucceeded) {
        'normal-stop' | Out-File -LiteralPath $stopPath -Encoding ascii -Force
        if ($watchdogProcess) {
            $watchdogProcess.WaitForExit(5000) | Out-Null
        }
    }
    if ($controllerMutexOwned) {
        try { $controllerMutex.ReleaseMutex() } catch { }
        $controllerMutexOwned = $false
    }
    if ($controllerMutex) {
        $controllerMutex.Dispose()
    }
}

if ($rows.Count -gt 0) {
    $rows | Export-Csv -LiteralPath $csvPath -NoTypeInformation -Encoding UTF8
}
$summary = [ordered]@{
    Timestamp = (Get-Date).ToString('o')
    ApplyMode = [bool] $Apply
    ControlMode = if ($fixedTargetMode) { 'FixedTargets' } else { 'TemperatureCurve' }
    CurveName = $curve.Name
    Samples = $rows.Count
    ControlsApplied = $controlsApplied
    ManualModeEntered = $manualModeEntered
    RestoreSucceeded = $restoreSucceeded
    WatchdogStarted = $watchdogStarted
    TemperatureMinC = if ($rows.Count) { ($rows | Measure-Object TemperatureC -Minimum).Minimum } else { $null }
    TemperatureMaxC = if ($rows.Count) { ($rows | Measure-Object TemperatureC -Maximum).Maximum } else { $null }
    EndRequestedRPM = if ($rows.Count -and $null -ne $rows[-1].RequestedRPM) { [int] $rows[-1].RequestedRPM } else { $null }
    EndRequestedFan0RPM = if ($rows.Count) { [int] $rows[-1].RequestedFan0RPM } else { $null }
    EndRequestedFan1RPM = if ($rows.Count) { [int] $rows[-1].RequestedFan1RPM } else { $null }
    EndFan0RPM = if ($rows.Count) { [int] $rows[-1].Fan0RPM } else { $null }
    EndFan1RPM = if ($rows.Count) { [int] $rows[-1].Fan1RPM } else { $null }
    Error = $controllerError
    CsvPath = $csvPath
    WatchdogLogPath = if ($watchdogStarted) { $watchdogLogPath } else { $null }
}
$summary | ConvertTo-Json | Out-File -LiteralPath $summaryPath -Encoding utf8
$summary | Format-List
Write-Host "Samples: $csvPath"
Write-Host "Summary: $summaryPath"

if ($controllerError -or ($Apply -and (-not $manualModeEntered -or -not $restoreSucceeded))) {
    exit 1
}
exit 0
