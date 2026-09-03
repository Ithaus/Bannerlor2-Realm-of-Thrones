# Wgrywa paczke swiata ROT (Nexus 11008: world/rules/NPC) do folderu kampanii AI Influence.
# AI Influence 6.x trzyma dane per kampania w Modules\AIInfluence\save_data\<UniqueGameId>\
# i tworzy ten folder dopiero przy pierwszym wczytaniu zapisu z wlaczonym modem.
# Uruchom PO pierwszym wczytaniu zapisu (gra moze byc zamknieta):
#   powershell -ExecutionPolicy Bypass -File tools\ai-influence-pack.ps1
# Opcjonalnie: -CampaignId <id folderu>  (domyslnie: najnowszy folder w save_data)
# Nadpisywane pliki laduja w <kampania>\_before_rot_pack\ (cofniecie = skopiuj z powrotem).
param([string]$CampaignId = "", [string]$Modules = "C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules")

$ErrorActionPreference = "Stop"
$src = Join-Path $PSScriptRoot "ai-influence-pack"
$sd = Join-Path $Modules "AIInfluence\save_data"
if (-not (Test-Path $sd)) { Write-Host "BRAK $sd - wczytaj najpierw zapis z wlaczonym AI Influence, odczekaj minute, wyjdz z gry."; exit 1 }

if ($CampaignId -ne "") { $camp = Join-Path $sd $CampaignId }
else {
    $dirs = Get-ChildItem $sd -Directory | Sort-Object CreationTime -Descending
    if ($dirs.Count -eq 0) { Write-Host "save_data jest pusty - kampania jeszcze nie utworzyla folderu."; exit 1 }
    $camp = $dirs[0].FullName
    if ($dirs.Count -gt 1) { Write-Host ("UWAGA: " + $dirs.Count + " kampanii w save_data, biore najnowsza: " + $dirs[0].Name + " (inne: " + (($dirs | Select-Object -Skip 1 | ForEach-Object { $_.Name }) -join ", ") + ")") }
}
if (-not (Test-Path $camp)) { Write-Host "BRAK folderu kampanii: $camp"; exit 1 }
Write-Host "Kampania: $camp"

$log = Join-Path $Modules "AIInfluence\logs\mod_log.txt"
if (Test-Path $log) { Select-String -Path $log -Pattern "Created save directory" | Select-Object -Last 2 | ForEach-Object { Write-Host ("  log: " + $_.Line.Trim()) } }

$bak = Join-Path $camp "_before_rot_pack"
function Put($from, $toDir) {
    if (-not (Test-Path $toDir)) { New-Item -ItemType Directory -Force $toDir | Out-Null }
    $n = 0; $over = 0
    foreach ($f in Get-ChildItem $from -File) {
        $dst = Join-Path $toDir $f.Name
        if (Test-Path $dst) {
            $rel = $dst.Substring($camp.Length).TrimStart('\')
            $bdst = Join-Path $bak $rel
            $bdir = Split-Path $bdst
            if (-not (Test-Path $bdir)) { New-Item -ItemType Directory -Force $bdir | Out-Null }
            if (-not (Test-Path $bdst)) { Copy-Item $dst $bdst }
            $over++
        }
        Copy-Item $f.FullName $dst -Force
        $n++
    }
    Write-Host ("  " + $toDir.Substring($camp.Length) + ": " + $n + " plikow (" + $over + " nadpisanych, kopie w _before_rot_pack)")
}

Put (Join-Path $src "world_data") (Join-Path $camp "prompts\world_data")
Put (Join-Path $src "rules")      (Join-Path $camp "prompts\rules")
Put (Join-Path $src "npc")        $camp
Write-Host "Gotowe. playerdescription.txt (opis TWOJEJ postaci dla NPC) uzupelnij sam: $camp\prompts\playerdescription.txt - wzor w tools\ai-influence-pack\PLAYERDESCRIPTION_GUIDE.txt"
