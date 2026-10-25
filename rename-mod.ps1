# ============================================================
#  Stacklands Mod: rename your mod id / display name in one shot
#
#  Usage (new id: letters, digits and underscore only; must not start with a digit):
#     powershell -ExecutionPolicy Bypass -File .\rename-mod.ps1 -NewId MyCoolMod
#     powershell -ExecutionPolicy Bypass -File .\rename-mod.ps1 -NewId MyCoolMod -NewName "My Cool Mod"
#
#  Updates: manifest.json, *.csproj (AssemblyName), all *.cs files
#  Then run .\build.ps1 again; the mod will be deployed under the new id.
#
#  NOTE: keep this file ASCII-only. Windows PowerShell 5.1 reads BOM-less
#  .ps1 files using the system ANSI codepage (GBK on Chinese Windows),
#  so non-ASCII characters here would break parsing.
# ============================================================

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$NewId,
    [string]$NewName
)

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot
$root = $PSScriptRoot

# Windows PowerShell 5.1's Set-Content -Encoding UTF8 writes a BOM; the game may
# fail to parse a BOM-prefixed JSON file, so always write BOM-less UTF-8 here.
function Write-TextNoBom([string]$Path, [string]$Text) {
    [System.IO.File]::WriteAllText($Path, $Text, (New-Object System.Text.UTF8Encoding($false)))
}

if ($NewId -notmatch '^[A-Za-z_][A-Za-z0-9_]*$') {
    throw "Invalid mod id '$NewId': use letters/digits/underscore only, and do not start with a digit."
}

# current id comes from manifest.json
$manifestPath = Join-Path $root "manifest.json"
$manifestRaw  = Get-Content $manifestPath -Raw -Encoding UTF8
$manifest     = $manifestRaw | ConvertFrom-Json
$oldId        = $manifest.id
if ($oldId -eq $NewId) { throw "New id equals current id ($oldId)" }

$oldName = $manifest.name
if (-not $NewName) { $NewName = $NewId }

Write-Host "id:   $oldId  ->  $NewId"
Write-Host "name: $oldName  ->  $NewName"

# 1) manifest.json (text replace, keeps original formatting)
$newRaw = $manifestRaw.Replace('"id": "' + $oldId + '"', '"id": "' + $NewId + '"')
$newRaw = $newRaw.Replace('"name": "' + $oldName + '"', '"name": "' + $NewName + '"')
if ($newRaw -eq $manifestRaw) { Write-Warning "manifest.json unchanged - please check it manually" }
Write-TextNoBom $manifestPath $newRaw

# 2) .csproj (AssemblyName must equal the mod id)
$csproj = Get-ChildItem $root -Filter *.csproj -File | Select-Object -First 1
$csRaw  = Get-Content $csproj.FullName -Raw -Encoding UTF8
$csNew  = $csRaw.Replace("<AssemblyName>$oldId</AssemblyName>", "<AssemblyName>$NewId</AssemblyName>")
if ($csNew -eq $csRaw) { Write-Warning "AssemblyName in $($csproj.Name) unchanged - please check it manually" }
Write-TextNoBom $csproj.FullName $csNew

# 3) all .cs files (namespace prefix, class name, and any string containing the old id)
$csFiles = Get-ChildItem $root -Recurse -File -Filter *.cs |
    Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' }
foreach ($f in $csFiles) {
    $raw = Get-Content $f.FullName -Raw -Encoding UTF8
    $out = $raw -replace [regex]::Escape($oldId), $NewId
    if ($out -ne $raw) {
        Write-TextNoBom $f.FullName $out
        Write-Host "updated $($f.Name)"
    }
}

Write-Host ""
Write-Host "Rename done. Next:" -ForegroundColor Green
Write-Host "  1) run .\build.ps1"
Write-Host "  2) delete the old deployed folder: $env:USERPROFILE\AppData\LocalLow\sokpop\Stacklands\Mods\$oldId"
