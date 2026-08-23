#Requires -Version 5.1

[CmdletBinding()]
param(
    [string] $OutputDirectory
)

$ErrorActionPreference = 'Stop'
if (-not $OutputDirectory) {
    $OutputDirectory = Join-Path $PSScriptRoot 'dist'
}
$compilerCandidates = @(
    "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe",
    "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe"
)
$compiler = $compilerCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $compiler) {
    throw 'The Windows .NET Framework C# compiler was not found.'
}

$sourcePath = Join-Path $PSScriptRoot 'launcher\HuaweiFanControlLauncher.cs'
$manifestPath = Join-Path $PSScriptRoot 'launcher\app.manifest'
$controllerPath = Join-Path $PSScriptRoot 'HuaweiFan-AutoController.ps1'
$watchdogPath = Join-Path $PSScriptRoot 'HuaweiFan-Watchdog.ps1'
$quietCurvePath = Join-Path $PSScriptRoot 'quiet-balanced-curve.json'
$fullCurvePath = Join-Path $PSScriptRoot 'full-speed-curve.json'
foreach ($requiredPath in @($sourcePath, $manifestPath, $controllerPath, $watchdogPath, $quietCurvePath, $fullCurvePath)) {
    if (-not (Test-Path -LiteralPath $requiredPath)) {
        throw "Required build input was not found: $requiredPath"
    }
}

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$outputPath = Join-Path $OutputDirectory 'HuaweiFanControl.exe'
$arguments = @(
    '/nologo',
    '/target:exe',
    '/platform:anycpu',
    '/optimize+',
    "/win32manifest:$manifestPath",
    "/out:$outputPath",
    "/resource:$controllerPath,HuaweiFanControl.Resources.Controller.ps1",
    "/resource:$watchdogPath,HuaweiFanControl.Resources.Watchdog.ps1",
    "/resource:$quietCurvePath,HuaweiFanControl.Resources.QuietCurve.json",
    "/resource:$fullCurvePath,HuaweiFanControl.Resources.FullSpeedCurve.json",
    $sourcePath
)
& $compiler @arguments
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $outputPath)) {
    throw "C# compiler failed with exit code $LASTEXITCODE."
}

$hash = Get-FileHash -LiteralPath $outputPath -Algorithm SHA256
$checksumPath = "$outputPath.sha256"
('{0}  {1}' -f $hash.Hash, (Split-Path $outputPath -Leaf)) |
    Out-File -LiteralPath $checksumPath -Encoding ascii -Force
[pscustomobject]@{
    OutputPath = $outputPath
    Length = (Get-Item -LiteralPath $outputPath).Length
    SHA256 = $hash.Hash
    ChecksumPath = $checksumPath
    Compiler = $compiler
}
