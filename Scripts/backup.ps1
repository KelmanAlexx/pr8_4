$ErrorActionPreference = 'Stop'
$ProjectDir = Split-Path -Parent $PSScriptRoot
Set-Location $ProjectDir
dotnet run --project (Join-Path $ProjectDir 'SatBackupToolkit.csproj') -- backup (Join-Path $ProjectDir 'ExampleData') (Join-Path $ProjectDir 'Backups')
if ($LASTEXITCODE -ne 0) { throw 'Не удалось создать резервную копию.' }
