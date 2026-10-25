# ============================================================
#  Stacklands Mod: build + deploy script
#  (PowerShell replacement for the template's build.py -- no Python needed)
#
#  Usage (this machine's execution policy is Restricted by default):
#     powershell -ExecutionPolicy Bypass -File .\build.ps1
#     powershell -ExecutionPolicy Bypass -File .\build.ps1 -Configuration Release
#     powershell -ExecutionPolicy Bypass -File .\build.ps1 -SkipBuild   # sync assets only
#
#  Optional one-time setup so you can just run .\build.ps1 :
#     Set-ExecutionPolicy -Scope CurrentUser RemoteSigned
#
#  Compatible with Windows PowerShell 5.1 and PowerShell 7 (pwsh).
#  NOTE: keep this file ASCII-only. Windows PowerShell 5.1 reads BOM-less
#  .ps1 files using the system ANSI codepage (GBK on Chinese Windows),
#  so non-ASCII characters here would break parsing.
# ============================================================

[CmdletBinding()]
param(
    [string]$Configuration = "Debug",
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot
$root = $PSScriptRoot

# ---- Assets synced into the game's Mods folder (add/remove as you like) ----
$SyncFolders = @("Blueprints", "Boosterpacks", "Cards", "Icons", "Sounds")
# Files kept in the repo root and copied into the mod folder on every deploy.
#   icon.png    : Steam Workshop thumbnail. The game reads it from THIS mod folder when you
#                 press Upload, so it is synced here as a release asset (512x512).
#   workshop.txt: the Workshop item id. The game writes it on the FIRST upload and reads it on
#                 every later upload to update that same item - so it MUST be present in the
#                 deployed folder, otherwise the next upload would create a DUPLICATE item.
#                 It therefore lives in the repo and is restored here. (Yes, it does get packed
#                 into the upload: it is ~10 bytes of item id, harmless.)
$CopyFiles   = @("manifest.json", "localization.tsv", "icon.png", "workshop.txt")
# ---- Release hygiene: never ship these to the Mods folder, and delete them if present ----
#   config.json : per-player settings written by the game. Shipping it would hand every
#                 subscriber MY test settings (and can mis-apply when keys are renamed).
$ExcludeFiles = @(".gitkeep", ".DS_Store", "config.json")
# Files that must never be pulled from the game's Mods folder back into the source repo
# (guard against the edit-in-place trap: editing Mods\<id>\config.json and then syncing).
$NeverImport = @("config.json", "workshop.txt")

# ---- Local mods folder (Windows) ----
$ModsRoot = Join-Path $env:USERPROFILE "AppData/LocalLow/sokpop/Stacklands/Mods"

# ---- 0. Pre-check: the game locks the mod dll while it is running ----
$runningGame = Get-Process -Name Stacklands -ErrorAction SilentlyContinue
if ($runningGame) {
    Write-Host ""
    Write-Host ("WARNING: Stacklands is running (PID " + (($runningGame | ForEach-Object { $_.Id }) -join ', ') + ").") -ForegroundColor Yellow
    Write-Host "         The game maps Mods\<ModId>\<ModId>.dll into memory and locks it," -ForegroundColor Yellow
    Write-Host "         so this script cannot overwrite it. Changes need a game restart anyway -" -ForegroundColor Yellow
    Write-Host "         please fully exit the game before running this script." -ForegroundColor Yellow
    Write-Host ""
}

# ---- 1. Read mod id + assembly name ----
$manifestPath = Join-Path $root "manifest.json"
if (-not (Test-Path $manifestPath)) { throw "manifest.json not found" }
$manifest = Get-Content $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
$modId = $manifest.id
if ([string]::IsNullOrWhiteSpace($modId)) { throw "manifest.json: 'id' is empty" }

$csproj = Get-ChildItem $root -Filter *.csproj -File | Select-Object -First 1
if (-not $csproj) { throw "no .csproj file found" }
[xml]$proj = Get-Content $csproj.FullName -Raw -Encoding UTF8
$asmName = $proj.Project.PropertyGroup.AssemblyName
if ([string]::IsNullOrWhiteSpace($asmName)) { $asmName = $modId }

$modPath = Join-Path $ModsRoot $modId
$dll     = Join-Path $root "bin/$Configuration/netstandard2.1/$asmName.dll"

# ---- 2. Build ----
if (-not $SkipBuild) {
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    & dotnet build $csproj.FullName -c $Configuration --nologo
    if ($LASTEXITCODE -ne 0) { throw "dotnet build failed (exit code $LASTEXITCODE)" }
    $sw.Stop()
    Write-Host ("Build OK in {0:N2}s" -f $sw.Elapsed.TotalSeconds) -ForegroundColor Green
}
if (-not (Test-Path $dll)) { throw "build output not found: $dll" }

# ---- 3. Deploy ----
# Copy the dll first: while the game runs it locks this file, and failing here exits early,
# so you never end up with a "new JSON + old DLL" mixed state.
New-Item -ItemType Directory -Force -Path $modPath | Out-Null
try {
    Copy-Item $dll (Join-Path $modPath "$asmName.dll") -Force -ErrorAction Stop
}
catch {
    Write-Host ""
    if ($runningGame) {
        Write-Host "DEPLOY FAILED: Stacklands is running and has $asmName.dll locked, so it cannot be overwritten." -ForegroundColor Red
        Write-Host "Please fully exit the game and run this script again." -ForegroundColor Red
        Write-Host "(No other files were synced this run, so you never end up with a new JSON + old DLL mix.)" -ForegroundColor Red
    }
    else {
        Write-Host "DEPLOY FAILED: could not overwrite $asmName.dll" -ForegroundColor Red
        Write-Host $_.Exception.Message -ForegroundColor Red
    }
    Write-Host ""
    exit 1
}

foreach ($f in $CopyFiles) {
    $src = Join-Path $root $f
    if (Test-Path $src) { Copy-Item $src (Join-Path $modPath $f) -Force }
}

# ---- 3b. Release hygiene: drop files that must not live in the deployed mod folder ----
foreach ($name in $ExcludeFiles) {
    $stale = Join-Path $modPath $name
    if (Test-Path $stale -PathType Leaf) {
        Remove-Item $stale -Force
        Write-Host ("Removed from deploy folder: " + $name)
    }
}

foreach ($folder in $SyncFolders) {
    $src = Join-Path $root $folder
    if (-not (Test-Path $src)) { continue }
    $dst = Join-Path $modPath $folder
    New-Item -ItemType Directory -Force -Path $dst | Out-Null

    # remove files that no longer exist in the source (and excluded placeholders)
    Get-ChildItem $dst -Recurse -File | ForEach-Object {
        $rel = $_.FullName.Substring($dst.Length).TrimStart([char[]]@('\', '/'))
        if ($ExcludeFiles -contains $_.Name -or -not (Test-Path (Join-Path $src $rel))) {
            Remove-Item $_.FullName -Force
        }
    }
    # remove empty directories, then make sure the top-level folder still exists
    Get-ChildItem $dst -Recurse -Directory |
        Sort-Object { $_.FullName.Length } -Descending | ForEach-Object {
            if (-not (Get-ChildItem $_.FullName -Recurse -File)) { Remove-Item $_.FullName -Recurse -Force }
        }
    New-Item -ItemType Directory -Force -Path $dst | Out-Null
    # copy new / updated files (skip editor/VCS placeholders and never-import files)
    Get-ChildItem $src -Recurse -File |
        Where-Object { $ExcludeFiles -notcontains $_.Name -and $NeverImport -notcontains $_.Name } |
        ForEach-Object {
            $rel       = $_.FullName.Substring($src.Length).TrimStart([char[]]@('\', '/'))
            $target    = Join-Path $dst $rel
            $targetDir = Split-Path $target -Parent
            New-Item -ItemType Directory -Force -Path $targetDir | Out-Null
            if (-not (Test-Path $target) -or (Get-Item $target).LastWriteTime -lt $_.LastWriteTime) {
                Copy-Item $_.FullName $target -Force
            }
        }
}

Write-Host ""
Write-Host "Deployed to: $modPath" -ForegroundColor Green
Write-Host ("Mod: {0} (id: {1}, v{2})" -f $manifest.name, $modId, $manifest.version)
Write-Host "Launch the game and check the Mods menu."
