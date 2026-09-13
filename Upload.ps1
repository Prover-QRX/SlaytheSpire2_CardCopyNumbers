$ErrorActionPreference = 'Stop'
$uploaderDir = Join-Path $PSScriptRoot 'tools/ModUploader'
$uploaderExe = Join-Path $uploaderDir 'ModUploader.exe'
if (!(Test-Path -LiteralPath $uploaderExe)) { throw 'Official uploader missing. See README-zh.md.' }
if (!(Get-Process steam -ErrorAction SilentlyContinue)) { throw 'Start Steam and sign in first.' }
Push-Location $uploaderDir
try {
    & $uploaderExe upload -w (Join-Path $PSScriptRoot 'Workshop')
    if ($LASTEXITCODE -ne 0) { throw 'Upload failed. Check mod-uploader.log.' }
} finally { Pop-Location }
