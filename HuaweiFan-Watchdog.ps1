#Requires -Version 5.1
#Requires -RunAsAdministrator

[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $HeartbeatPath,
    [Parameter(Mandatory)][string] $StopPath,
    [Parameter(Mandatory)][string] $LogPath,
    [Parameter(Mandatory)][int] $ControllerProcessId,
    [ValidateRange(6, 60)][int] $TimeoutSeconds = 12
)

$ErrorActionPreference = 'Stop'

function Write-WatchdogLog([string] $Message) {
    ('{0:o} {1}' -f (Get-Date), $Message) | Out-File -LiteralPath $LogPath -Encoding utf8 -Append
}

function Disable-HuaweiFanManualMode {
    $oemWmi = Get-CimInstance -Namespace 'root\wmi' -ClassName OemWMIMethod |
        Where-Object { $_.InstanceName -eq 'ACPI\PNP0C14\HWMI_0' } |
        Select-Object -First 1
    if (-not $oemWmi) {
        throw 'Huawei OemWMIMethod instance was not found.'
    }
    $buffer = [byte[]]::new(64)
    [byte[]] $command = @(0x02, 0x11, 0x00, 0x00, 0x00, 0x00)
    [Array]::Copy($command, $buffer, $command.Count)
    $result = Invoke-CimMethod -InputObject $oemWmi -MethodName OemWMIfun -Arguments @{ u8Input = $buffer }
    if (-not $result.u8Output -or $result.u8Output[0] -ne 0) {
        $status = if ($result.u8Output) { '0x{0:X2}' -f $result.u8Output[0] } else { 'missing' }
        throw "BIOS automatic-control restore returned $status."
    }
}

Write-WatchdogLog "Started controllerPid=$ControllerProcessId timeout=$TimeoutSeconds"
while ($true) {
    if (Test-Path -LiteralPath $StopPath) {
        Write-WatchdogLog 'Normal stop signal received.'
        exit 0
    }

    $controllerAlive = $null -ne (Get-Process -Id $ControllerProcessId -ErrorAction SilentlyContinue)
    $heartbeatFresh = $false
    if (Test-Path -LiteralPath $HeartbeatPath) {
        $age = (Get-Date) - (Get-Item -LiteralPath $HeartbeatPath).LastWriteTime
        $heartbeatFresh = $age.TotalSeconds -le $TimeoutSeconds
    }

    if (-not $controllerAlive -or -not $heartbeatFresh) {
        Write-WatchdogLog "Restore triggered controllerAlive=$controllerAlive heartbeatFresh=$heartbeatFresh"
        for ($attempt = 1; $attempt -le 5; $attempt++) {
            try {
                Disable-HuaweiFanManualMode
                Write-WatchdogLog "Restore succeeded on attempt $attempt."
                exit 0
            }
            catch {
                Write-WatchdogLog "Restore attempt $attempt failed: $($_.Exception.Message)"
                Start-Sleep -Seconds 2
            }
        }
        Write-WatchdogLog 'Restore failed after five attempts.'
        exit 2
    }

    Start-Sleep -Seconds 2
}
