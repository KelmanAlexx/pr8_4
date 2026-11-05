$ErrorActionPreference = 'Stop'
$ProjectDir = Split-Path -Parent $PSScriptRoot
Set-Location $ProjectDir
dotnet run --project (Join-Path $ProjectDir 'SatBackupToolkit.csproj') -- sat
if ($LASTEXITCODE -ne 0) { throw 'Не удалось сформировать SAT-протокол.' }
