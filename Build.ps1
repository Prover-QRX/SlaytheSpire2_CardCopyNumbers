param([switch]$Install)
$ErrorActionPreference = 'Stop'
$gameDir = Split-Path $PSScriptRoot -Parent
dotnet build (Join-Path $PSScriptRoot 'src/CardCopyNumbers.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
dotnet run --project (Join-Path $PSScriptRoot 'tests/Tests.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
dotnet run --project (Join-Path $PSScriptRoot 'integration/Integration.csproj') -c Release -- (Join-Path $gameDir 'data_sts2_windows_x86_64')
if ($LASTEXITCODE -ne 0) { throw 'Game assembly integration checks failed.' }
$contentDir = Join-Path $PSScriptRoot 'Workshop/content'
$manifestPath = Join-Path $contentDir 'CardCopyNumbers.json'
$manifestData = Get-Content -LiteralPath $manifestPath -Raw -Encoding utf8 | ConvertFrom-Json
$manifestData.description = (Get-Content -LiteralPath (Join-Path $PSScriptRoot 'GameDescription.bbcode.txt') -Raw -Encoding utf8).Trim()
$manifestData | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $manifestPath -Encoding utf8
$workshopPath = Join-Path $PSScriptRoot 'Workshop/workshop.json'
$workshopData = Get-Content -LiteralPath $workshopPath -Raw -Encoding utf8 | ConvertFrom-Json
$workshopData.description = (Get-Content -LiteralPath (Join-Path $PSScriptRoot 'WorkshopDescription.bbcode.txt') -Raw -Encoding utf8).Trim()
$workshopData | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $workshopPath -Encoding utf8
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'src/bin/Release/net9.0/CardCopyNumbers.dll') -Destination $contentDir -Force
if ($Install) {
    $installDir = Join-Path $gameDir 'mods/CardCopyNumbers'
    New-Item -ItemType Directory -Path $installDir -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $contentDir 'CardCopyNumbers.dll'), (Join-Path $contentDir 'CardCopyNumbers.json') -Destination $installDir -Force
    Write-Host "Installed: $installDir"
}
