#Requires -Version 5.1
#Requires -RunAsAdministrator

$ErrorActionPreference = 'Stop'
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
Write-Output 'Vendor automatic fan control restored.'
