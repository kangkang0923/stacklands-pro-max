# Offline verification for the StacklandsProMax mod.
#  1) localization.tsv: parse it the way SokLoc does, check columns / duplicate terms / missing terms
#  2) Cards & Blueprints JSON: every referenced term exists; every _field exists on the script class
#  3) every card id referenced by blueprints / card bags exists
# Keep this file ASCII-only (Windows PowerShell 5.1 reads BOM-less .ps1 as ANSI/GBK).
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

$problems = New-Object System.Collections.Generic.List[string]
function Fail([string]$msg) { $script:problems.Add($msg) | Out-Null; Write-Host "  FAIL: $msg" -ForegroundColor Red }
function Ok([string]$msg)   { Write-Host "  ok  : $msg" -ForegroundColor Green }

# ---------------------------------------------------------------- localization
Write-Host "`n== localization.tsv =="
$raw = [System.IO.File]::ReadAllText((Join-Path $PSScriptRoot "localization.tsv"), [System.Text.Encoding]::UTF8)
$lines = $raw -split "`n"
$header = $lines[0].TrimEnd("`r") -split "`t"
Write-Host ("  header: " + ($header -join " | "))
$terms = @{}
$colCount = $header.Count
for ($i = 1; $i -lt $lines.Count; $i++) {
    $line = $lines[$i].TrimEnd("`r")
    if ([string]::IsNullOrWhiteSpace($line)) { continue }
    $cells = $line -split "`t"
    if ($cells.Count -ne $colCount) { Fail "line $($i+1): $($cells.Count) columns, expected $colCount" }
    $term = $cells[0].ToLowerInvariant()
    if ([string]::IsNullOrWhiteSpace($term)) { Fail "line $($i+1): empty term" }
    if ($terms.ContainsKey($term)) { Fail "line $($i+1): duplicate term '$term'" }
    $terms[$term] = $true
}
Ok ("parsed {0} terms, {1} columns each" -f $terms.Count, $colCount)

# ---------------------------------------------------------------- card / blueprint JSON
Write-Host "`n== Cards & Blueprints =="
$cardIds = @{}
$scripts = @{}
foreach ($file in Get-ChildItem (Join-Path $PSScriptRoot "Cards") -Filter *.json) {
    $json = Get-Content $file.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
    if (-not $json.id) { Fail "$($file.Name): no id" ; continue }
    $cardIds[$json.id] = $json
    if ($json.script) { $scripts[$json.id] = $json.script }
    foreach ($prop in $json.PSObject.Properties) {
        if ($prop.Name -like "_*") {
            Write-Host ("  note: {0} sets field {1}" -f $file.Name, $prop.Name)
        }
    }
    foreach ($termProp in @("nameTerm", "descriptionTerm")) {
        $t = $json.$termProp
        if ($t -and -not $terms.ContainsKey($t.ToLowerInvariant())) { Fail "$($file.Name): term '$t' missing from localization.tsv" }
    }
    Ok "$($file.Name) (id=$($json.id), script=$($json.script))"
}

foreach ($file in Get-ChildItem (Join-Path $PSScriptRoot "Blueprints") -Filter *.json) {
    $json = Get-Content $file.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($json.nameTerm -and -not $terms.ContainsKey($json.nameTerm.ToLowerInvariant())) {
        Fail "$($file.Name): term '$($json.nameTerm)' missing from localization.tsv"
    }
    if (-not $json.subprints) { Fail "$($file.Name): no subprints"; continue }
    foreach ($sp in $json.subprints) {
        foreach ($id in ($sp.RequiredCards -split ",")) {
            $id = $id.Trim()
            if ($id -and -not $cardIds.ContainsKey($id)) { Write-Host "  info: $($file.Name) requires vanilla card '$id'" }
        }
        foreach ($id in ($sp.CardsToRemove -split ",")) {
            $id = $id.Trim()
            if ($id -and -not $cardIds.ContainsKey($id)) { Write-Host "  info: $($file.Name) removes vanilla card '$id'" }
        }
        if ($sp.ResultCard -and -not $cardIds.ContainsKey($sp.ResultCard)) {
            Fail "$($file.Name): ResultCard '$($sp.ResultCard)' has no Cards/<id>.json"
        }
        if ($sp.StatusTerm -and -not $terms.ContainsKey($sp.StatusTerm.ToLowerInvariant())) {
            Fail "$($file.Name): StatusTerm '$($sp.StatusTerm)' missing from localization.tsv"
        }
    }
    Ok "$($file.Name) (id=$($json.id))"
}

# ---------------------------------------------------------------- settings terms in ModConfig.cs
Write-Host "`n== ModConfig term suffixes =="
$cfgSource = [System.IO.File]::ReadAllText((Join-Path $PSScriptRoot "ModConfig.cs"), [System.Text.Encoding]::UTF8)
# Two Register overloads exist:
#   Register(config, "key", "suffix", delegate ...)                 <- default true
#   Register(config, "key", "suffix", false, delegate ...)          <- explicit default (e.g. shift-drag is off by default)
#   Register(config, "key", "suffix", 4f, delegate ...)             <- float value
#   Register(config, "key", "suffix", "R", delegate ...)            <- string value (e.g. the hotkey)
$suffixes = [regex]::Matches($cfgSource, '"([a-z_]+)",\s*(?:true|false|[-0-9.]+f|"[^"]*")?,?\s*delegate') | ForEach-Object { $_.Groups[1].Value }
$suffixes = $suffixes | Sort-Object -Unique
foreach ($s in $suffixes) {
    foreach ($kind in @("name", "tip")) {
        $term = "stacklandspro_cfg_${s}_$kind"
        if (-not $terms.ContainsKey($term)) { Fail "ModConfig suffix '$s' -> missing term '$term'" }
    }
}
Ok ("checked {0} config suffixes: {1}" -f $suffixes.Count, ($suffixes -join ", "))

# ---------------------------------------------------------------- JSON "_field" targets
# The game writes JSON keys starting with "_" straight onto the script class via reflection
# (public fields only). Verify each of them really exists, walking the base classes.
Write-Host "`n== JSON _field targets =="
$managed = $null
if (Test-Path env:Stacklands) { $managed = Join-Path $env:Stacklands "Stacklands_Data/Managed" }
if (-not $managed -or -not (Test-Path $managed)) {
    $gameRoot = (Get-Content (Join-Path $PSScriptRoot "Mod.csproj.user") -Raw -Encoding UTF8 |
        Select-String -Pattern "<stacklands>(.*?)</stacklands>").Matches.Groups[1].Value
    if ($gameRoot) { $managed = Join-Path $gameRoot "Stacklands_Data/Managed" }
}
$ourDll = Join-Path $PSScriptRoot "bin/Debug/netstandard2.1/StacklandsProMax.dll"
if ((Test-Path $managed) -and (Test-Path $ourDll)) {
    Add-Type -Path (Join-Path $managed "Mono.Cecil.dll")
    $game = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $managed "GameScripts.dll"))
    $ours = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($ourDll)

    function Resolve-CardData([string]$typeName) {
        if ([string]::IsNullOrWhiteSpace($typeName) -or $typeName -eq "CardData") {
            return $game.MainModule.GetType("CardData")
        }
        $t = $ours.MainModule.GetType($typeName)
        if (-not $t) { $t = $game.MainModule.GetType($typeName) }
        return $t
    }
    # Walk the base chain by FullName (NO .Resolve(): Mono.Cecil cannot resolve
    # UnityEngine / mscorlib here, and Resolve() would throw).
    function Resolve-CardDataStep($type) {
        if (-not $type -or -not $type.BaseType) { return $null }
        $baseName = $type.BaseType.FullName
        $t = $ours.MainModule.GetType($baseName)
        if (-not $t) { $t = $game.MainModule.GetType($baseName) }
        return $t
    }
    function Has-PublicField($type, [string]$fieldName) {
        while ($type -ne $null) {
            foreach ($f in $type.Fields) {
                if ($f.Name -eq $fieldName) { return $f.IsPublic }
            }
            $type = Resolve-CardDataStep $type
        }
        return $false
    }

    foreach ($file in Get-ChildItem (Join-Path $PSScriptRoot "Cards") -Filter *.json) {
        $json = Get-Content $file.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
        $type = Resolve-CardData $json.script
        if (-not $type) { Fail "$($file.Name): script class '$($json.script)' not found"; continue }
        foreach ($prop in $json.PSObject.Properties) {
            if ($prop.Name -notlike "_*") { continue }
            $fieldName = $prop.Name.Substring(1)
            if (-not (Has-PublicField $type $fieldName)) {
                Fail "$($file.Name): '$($prop.Name)' -> no public field '$fieldName' on $($type.FullName)"
            }
        }
        # ConnectionType / CardDirection enums: verified against GameScripts.dll
        #   CardDirection: input = 0, output = 1
        #   ConnectionType: LV = 1, HV = 3, Sewer = 4, Transport = 5
        if ($json._EnergyConnectors) {
            foreach ($c in $json._EnergyConnectors) {
                if ($c.EnergyConnectionType -ne 0 -and $c.EnergyConnectionType -ne 1) {
                    Fail "$($file.Name): EnergyConnectionType=$($c.EnergyConnectionType) is not a valid CardDirection (0 input / 1 output)"
                }
                $validConnection = @(1, 3, 4, 5)
                if ($validConnection -notcontains $c.EnergyConnectionStrength) {
                    Fail "$($file.Name): EnergyConnectionStrength=$($c.EnergyConnectionStrength) is not a valid ConnectionType (1 LV / 3 HV / 4 Sewer / 5 Transport)"
                }
                if ($c.EnergyConnectionAmount -lt 0) {
                    Fail "$($file.Name): EnergyConnectionAmount must be >= 0"
                }
                Ok "$($file.Name): connector dir=$($c.EnergyConnectionType) type=$($c.EnergyConnectionStrength) amount=$($c.EnergyConnectionAmount)"
            }
        }
        Ok "$($file.Name): all _fields exist on $($type.FullName)"
    }

}
else {
    Write-Host "  skip: game assembly or our dll not found" -ForegroundColor Yellow
}

# ---------------------------------------------------------------- patch classes registered
# A patch class that has [HarmonyPatch] but is NOT listed in Mod.PatchClasses silently does
# nothing in game (Harmony.PatchAll is only called for the registered list). That slipped
# through once, so check it here: every declared patch class must be registered, and every
# registered name must exist.
Write-Host "`n== Patch classes (Patches\*.cs vs Mod.PatchClasses) =="
$modSource = [System.IO.File]::ReadAllText((Join-Path $PSScriptRoot "Mod.cs"), [System.Text.Encoding]::UTF8)
$registered = @()
foreach ($m in [regex]::Matches($modSource, 'typeof\((\w+)\)')) { $registered += $m.Groups[1].Value }
$registered = @($registered | Sort-Object -Unique)

$declared = @()
foreach ($file in Get-ChildItem (Join-Path $PSScriptRoot "Patches") -Filter *.cs) {
    $src = [System.IO.File]::ReadAllText($file.FullName, [System.Text.Encoding]::UTF8)
    $chunks = $src -split '\[HarmonyPatch'
    for ($i = 1; $i -lt $chunks.Count; $i++) {
        if ($chunks[$i] -match 'class\s+(\w+)') { $declared += $Matches[1] }
    }
}
$declared = @($declared | Sort-Object -Unique)

foreach ($name in $declared) {
    if ($registered -notcontains $name) {
        Fail "$name has [HarmonyPatch] but is NOT registered in Mod.PatchClasses -> it would silently do nothing"
    }
}
foreach ($name in $registered) {
    if ($name -like '*Patch' -and $declared -notcontains $name) {
        Fail "$name is registered in Mod.PatchClasses but no [HarmonyPatch] class with that name exists"
    }
}
Ok ("declared {0} patch classes, {1} of them registered in Mod.PatchClasses" -f $declared.Count, ($declared | Where-Object { $registered -contains $_ }).Count)

# ---------------------------------------------------------------- summary
Write-Host ""
if ($problems.Count -gt 0) {
    Write-Host ("VERIFY FAILED: {0} problem(s)" -f $problems.Count) -ForegroundColor Red
    exit 1
}
Write-Host "VERIFY OK" -ForegroundColor Green
