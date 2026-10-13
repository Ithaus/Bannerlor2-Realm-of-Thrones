# ustawienia-jeffa.ps1 - dwa kroki wgrania koncowego w plikach ustawien Jeffa (decyzje Jeffa 09.10 i 12.10):
#  (1) Armoury.json: usuniecie linii "CampBattlePropsEnabled": false - scenografia obozu przy napadzie (E7/O2) dziala wtedy
#      z domyslnej nowego DLL (true); po cofnieciu do starego DLL wraca domyslna starego (false) - audyt 13.10;
#  (2) DiplomacySettings_v1.2.json: czasy w dniach przeliczone z roku 84 dni na rok 364 dni (x 364/84), w granicach suwakow moda:
#      min. wojna 21 -> 91, przerwa przed nowa wojna 21 -> 91, min. sojusz 42 -> 182, pakt o nieagresji 84 -> 364,
#      przerwa miedzy wojnami domowymi 240 -> 1000 (suwak konczy sie na 1000; 1040 poza zakresem), frakcja najdluzej 120 -> 520.
# Gra musi byc zamknieta. Kazdy plik: kopia obok (*.bak-<Tag>) i na D:, zmiana tylko gdy kazda kotwica wystepuje DOKLADNIE raz,
# zapis w tym samym kodowaniu i z tymi samymi koncami linii, kontrola po zapisie. -NaSucho = tylko pokazuje, co by zmienil.
param(
    [string]$Tag = '2026-10-13-noc34',
    [string]$Root = (Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'Mount and Blade II Bannerlord\Configs\ModSettings\Global'),
    [string]$MirrorRoot = 'D:\Backup-Bannerlord\wgrane',
    [switch]$NaSucho
)
$ErrorActionPreference = 'Stop'
$prawdziwe = $Root.StartsWith([Environment]::GetFolderPath('MyDocuments'), [StringComparison]::OrdinalIgnoreCase)
$procs = @(Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.ProcessName -like 'Bannerlord*' -or $_.ProcessName -like '*BLSE*' -or $_.ProcessName -like 'TaleWorlds.MountAndBlade.Launcher*' })
# kontrola gry dotyczy prawdziwych plikow; proba na kopiach (inny -Root) moze biec przy dzialajacej grze
if ($prawdziwe -and $procs.Count -gt 0) { Write-Host 'STOP: gra dziala - nic nie ruszam.'; exit 2 }

function Zmien([string]$plik, [object[]]$zmiany) {
    if (-not (Test-Path -LiteralPath $plik -PathType Leaf)) { Write-Host ("STOP: brak pliku " + $plik); return $false }
    $bajty = [IO.File]::ReadAllBytes($plik)
    $bom = ($bajty.Length -ge 3 -and $bajty[0] -eq 0xEF -and $bajty[1] -eq 0xBB -and $bajty[2] -eq 0xBF)
    $enc = New-Object Text.UTF8Encoding($bom)
    $tekst = $enc.GetString($bajty, $(if ($bom) { 3 } else { 0 }), $bajty.Length - $(if ($bom) { 3 } else { 0 }))
    $nowy = $tekst
    $ile = 0
    foreach ($z in $zmiany) {
        $n = ([regex]::Matches($nowy, [regex]::Escape($z.stare))).Count
        $m = $(if ($z.nowe) { ([regex]::Matches($nowy, [regex]::Escape($z.nowe))).Count } else { 0 })
        if ($n -eq 0 -and ($m -eq 1 -or $z.bezSladu)) { Write-Host ("  juz zrobione: " + $z.opis); continue }
        if ($n -ne 1) { Write-Host ("STOP: kotwica '" + $z.stare.Trim() + "' wystepuje " + $n + " razy w " + $plik + " (oczekiwane 1) - plik bez zmian"); return $false }
        $nowy = $nowy.Replace($z.stare, $z.nowe)
        $ile++
        Write-Host ("  " + $(if ($NaSucho) { 'NA SUCHO: ' } else { '' }) + $z.opis)
    }
    if ($ile -eq 0) { Write-Host ("  " + (Split-Path -Leaf $plik) + ": nic do zmiany"); return $true }
    try { $null = $nowy | ConvertFrom-Json } catch { Write-Host ("STOP: po zmianie " + $plik + " nie bylby poprawnym JSON - plik bez zmian"); return $false }
    if ($NaSucho) { return $true }
    $bak = $plik + '.bak-' + $Tag
    Copy-Item -LiteralPath $plik -Destination $bak -Force
    $h0 = (Get-FileHash -Algorithm SHA256 -LiteralPath $plik).Hash
    if ((Get-FileHash -Algorithm SHA256 -LiteralPath $bak).Hash -ne $h0) { Write-Host ("STOP: kopia " + $bak + " rozni sie od oryginalu - plik bez zmian"); return $false }
    try {
        $mi = Join-Path (Join-Path $MirrorRoot ($Tag + '-przed\Configs')) (Split-Path -Leaf $plik)
        $null = New-Item -ItemType Directory -Force -Path (Split-Path -Parent $mi)
        Copy-Item -LiteralPath $plik -Destination $mi -Force
    } catch { Write-Host ("  UWAGA: kopia na D: nieudana: " + $_.Exception.Message) }
    $wy = $enc.GetBytes($nowy)
    if ($bom) { $wy = [byte[]](0xEF, 0xBB, 0xBF) + $wy }
    [IO.File]::WriteAllBytes($plik, $wy)
    $spr = [IO.File]::ReadAllBytes($plik)
    if ($spr.Length -ne $wy.Length -or (Compare-Object $spr $wy -SyncWindow 0)) {
        Copy-Item -LiteralPath $bak -Destination $plik -Force
        Write-Host ("STOP: zapis " + $plik + " nie zgadza sie z zamierzonym - przywrocona kopia")
        return $false
    }
    Write-Host ("  zapisane " + (Split-Path -Leaf $plik) + " (" + $ile + " zmian), kopia " + $bak)
    return $true
}

$ok = $true
Write-Host 'Armoury.json:'
$ok = (Zmien (Join-Path $Root 'Armoury\Armoury.json') @(
    @{ stare = "  `"CampBattlePropsEnabled`": false,`n"; nowe = ''; bezSladu = $true; opis = 'usunieta linia "CampBattlePropsEnabled": false (dziala domyslna z DLL: true)' }
)) -and $ok
Write-Host 'DiplomacySettings_v1.2.json:'
$ok = (Zmien (Join-Path $Root 'Diplomacy\DiplomacySettings_v1.2.json') @(
    @{ stare = '"MinimumWarDurationInDays": 21,'; nowe = '"MinimumWarDurationInDays": 91,'; opis = 'min. czas wojny 21 -> 91 dni' },
    @{ stare = '"DeclareWarCooldownInDays": 21,'; nowe = '"DeclareWarCooldownInDays": 91,'; opis = 'przerwa przed nowa wojna 21 -> 91 dni' },
    @{ stare = '"MinimumAllianceDuration": 42,'; nowe = '"MinimumAllianceDuration": 182,'; opis = 'min. czas sojuszu 42 -> 182 dni' },
    @{ stare = '"NonAggressionPactDuration": 84,'; nowe = '"NonAggressionPactDuration": 364,'; opis = 'pakt o nieagresji 84 -> 364 dni' },
    @{ stare = '"MinimumTimeSinceLastCivilWarInDays": 240,'; nowe = '"MinimumTimeSinceLastCivilWarInDays": 1000,'; opis = 'przerwa miedzy wojnami domowymi 240 -> 1000 dni (koniec suwaka)' },
    @{ stare = '"MaximumFactionDurationInDays": 120,'; nowe = '"MaximumFactionDurationInDays": 520,'; opis = 'frakcja najdluzej 120 -> 520 dni' }
)) -and $ok
if ($ok) { Write-Host 'GOTOWE.'; exit 0 } else { Write-Host 'NIEPELNE - patrz STOP wyzej.'; exit 1 }
