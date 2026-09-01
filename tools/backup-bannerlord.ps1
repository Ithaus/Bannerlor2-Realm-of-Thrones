# backup-bannerlord.ps1 - kopia 1:1 na drugi dysk fizyczny (D:), odporna na
# awarie dysku C z 2026-09-01 (dysk gubi ostatnie klastry zapisu -> pliki
# w calosci albo od granicy 4096 B wypelnione zerami).
#
# Zasada: mirror NIGDY nie nadpisuje slepo. Kazdy plik, ktory mialby byc
# nadpisany lub usuniety, najpierw laduje w graveyard z data - wiec nawet
# jesli backup pojedzie PO awarii, dobra kopia przezyje w graveyard.
#
# Co chronimy:
#   1. Documents\Mount and Blade II Bannerlord  (savy, configi, MCM, logi)
#   2. ...\Bannerlord\Modules                   (ROT + wszystkie mody, ~105 GB)
#
# Uruchamianie: recznie `powershell -File tools\backup-bannerlord.ps1`
# albo z harmonogramu (zadanie Backup-Bannerlord przy logowaniu).
# Repo modow ma gita + GitHub - nie backupujemy go tutaj.

$ErrorActionPreference = 'Continue'
$dest    = 'D:\Backup-Bannerlord'
$stamp   = Get-Date -Format 'yyyy-MM-dd_HH-mm'
$logDir  = Join-Path $dest 'logs'
$null = New-Item -ItemType Directory -Force $dest, $logDir

$pairs = @(
    @{ Src = "$HOME\Documents\Mount and Blade II Bannerlord"
       Dst = Join-Path $dest 'Documents' },
    @{ Src = 'C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules'
       Dst = Join-Path $dest 'Modules' }
)

foreach ($p in $pairs) {
    $src = $p.Src; $dst = $p.Dst
    if (-not (Test-Path $src)) { Write-Output "POMIJAM (brak): $src"; continue }
    $name = Split-Path $dst -Leaf
    $log  = Join-Path $logDir "$stamp-$name.log"
    $null = New-Item -ItemType Directory -Force $dst

    # 1. Lista tego, co mirror by nadpisal/usunal (robocopy /L = na sucho).
    #    Newer/Older/Changed = plik rozni sie, EXTRA = jest tylko w backupie
    #    (EXTRA Dir = caly katalog do usuniecia - tez ratujemy w calosci).
    $dry = robocopy $src $dst /MIR /L /NP /NJH /NJS /FP 2>$null
    $doomed = @()
    foreach ($line in $dry) {
        if ($line -match '^\s*(Newer|Older|Changed|\*EXTRA File|\*EXTRA Dir)\s+(-?[\d.]+[a-z]?)\s+(.+)$') {
            $full = $Matches[3].Trim().TrimEnd('\')
            if ($full.StartsWith($dst)) { $doomed += $full }                    # EXTRA - sciezka w backupie
            elseif ($full.StartsWith($src)) {                                    # zmieniony - stara wersja w backupie
                $rel = $full.Substring($src.Length).TrimStart('\')
                $old = Join-Path $dst $rel
                if (Test-Path $old) { $doomed += $old }
            }
        }
    }

    # 2. Stare wersje do graveyard (przenoszenie, nie kopiowanie - szybkie).
    if ($doomed.Count -gt 0) {
        $grave = Join-Path $dest "graveyard\$stamp-$name"
        foreach ($f in $doomed) {
            $rel = $f.Substring($dst.Length).TrimStart('\')
            $to  = Join-Path $grave $rel
            $null = New-Item -ItemType Directory -Force (Split-Path $to)
            try { Move-Item -LiteralPath $f $to -Force } catch { }
        }
        Add-Content $log "graveyard: $($doomed.Count) plikow -> $grave"
    }

    # 3. Wlasciwy mirror.
    robocopy $src $dst /MIR /R:1 /W:2 /NP /NDL /LOG+:$log | Out-Null
    $rc = $LASTEXITCODE
    Write-Output "$name : robocopy exit $rc (0-7 = OK), do graveyard: $($doomed.Count), log: $log"
}

# 4. Porzadki: graveyard i logi starsze niz 21 dni wylatuja.
$cut = (Get-Date).AddDays(-21)
foreach ($d in (Get-ChildItem (Join-Path $dest 'graveyard') -Directory -ErrorAction SilentlyContinue)) {
    if ($d.CreationTime -lt $cut) { Remove-Item -LiteralPath $d.FullName -Recurse -Force -Confirm:$false }
}
foreach ($f in (Get-ChildItem $logDir -File -ErrorAction SilentlyContinue)) {
    if ($f.CreationTime -lt $cut) { Remove-Item -LiteralPath $f.FullName -Force -Confirm:$false }
}
Write-Output "GOTOWE $stamp"
