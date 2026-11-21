#Requires -Version 5.1
$ErrorActionPreference = "Continue"

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
Set-Location $RepoRoot

$failed = $false

Write-Host "dotnet test Tests/Net.Tests/Net.Tests.csproj"
dotnet test Tests/Net.Tests/Net.Tests.csproj
if ($LASTEXITCODE -ne 0) {
    $failed = $true
}

Write-Host "python Tools/gdscript-check/check.py"
python Tools/gdscript-check/check.py
if ($LASTEXITCODE -ne 0) {
    $failed = $true
}

if ($failed) {
    exit 1
}

Write-Host "OK"
exit 0
