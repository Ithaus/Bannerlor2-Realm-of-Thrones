# wgraj.ps1 - trwale wgranie pieciu modow i plikow danych Armoury / CrashScribe do gry (stala zgoda Jeffa 09.10: po udanym autotescie).
# Kroki: STOP gdy gra dziala; kopia tego, co jest w grze (obok pliku *.bak-<Tag> i na D:\Backup-Bannerlord\wgrane\<Tag>-przed);
# podmiana; kontrola md5 kazdego pliku. Plikow ustawien Jeffa (Documents\...\Configs) NIE rusza.
param(
    [Parameter(Mandatory = $true)][string]$DllDir,   # katalog z Armoury.dll, CrashScribe.dll (BEZ autotestu), RealisticCaptivity.dll, GrandTourney.dll, ForgeView.dll
    [Parameter(Mandatory = $true)][string]$Tree,     # drzewo zrodel (pliki danych poza DLL)
    [Parameter(Mandatory = $true)][string]$Tag,      # np. 2026-10-13-noc34
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord',
    [string]$MirrorRoot = 'D:\Backup-Bannerlord\wgrane',
    [switch]$DryRun
)
$ErrorActionPreference = 'Stop'
function Md5([string]$p) { if (Test-Path -LiteralPath $p -PathType Leaf) { (Get-FileHash -Algorithm MD5 -LiteralPath $p).Hash.ToLowerInvariant() } else { $null } }
$procs = @(Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.ProcessName -like 'Bannerlord*' -or $_.ProcessName -like '*BLSE*' -or $_.ProcessName -like 'TaleWorlds.MountAndBlade.Launcher*' })
if ($procs.Count -gt 0) { Write-Host ("STOP: gra dziala (" + (($procs | ForEach-Object { $_.ProcessName }) -join ', ') + ") - nic nie ruszam."); exit 2 }

$items = @()
foreach ($m in 'Armoury', 'CrashScribe', 'RealisticCaptivity', 'GrandTourney', 'ForgeView') {
    $items += [pscustomobject]@{ name = $m + '.dll'; src = (Join-Path $DllDir ($m + '.dll')); dst = (Join-Path $GameDir ('Modules\' + $m + '\bin\Win64_Shipping_Client\' + $m + '.dll')); dll = $true }
}
foreach ($r in 'Armoury\GUI\Prefabs\ArmBurnedVillageLabels.xml', 'Armoury\GUI\Prefabs\ArmMapVillageTooltip.xml', 'Armoury\ModuleData\arm_map_roads_probe.tsv',
               'Armoury\ModuleData\arm_map_villages.tsv', 'Armoury\ModuleData\arm_map_village_lore.tsv', 'Armoury\Prefabs\arm_map_roads.xml', 'CrashScribe\ModuleData\CrashScribe.settings.xml') {
    $items += [pscustomobject]@{ name = $r; src = (Join-Path $Tree $r); dst = (Join-Path $GameDir ('Modules\' + $r)); dll = $false }
}
# Jeff 16.10 (uklad wiosek v5, galaz w-toku/noc-wioski): uklad wiosek i lore ida PARAMI (bramka layout_crc). arm_map_villages.tsv zawsze (wyzej); pliki _v4
# obowiazkowe, gdy uklad w drzewie to v5 (klucz VillageLayoutV5 = false i zapis v4 wczytany na v5 - VillageBook.OldLayout); starsze drzewo bez v5 - pomijane
$lay = Join-Path $Tree 'Armoury\ModuleData\arm_map_villages.tsv'
$v5tree = (Test-Path -LiteralPath $lay -PathType Leaf) -and ((Get-Content -LiteralPath $lay -TotalCount 1) -like '*layout v5*')
foreach ($r in 'Armoury\ModuleData\arm_map_villages_v4.tsv', 'Armoury\ModuleData\arm_map_village_lore_v4.tsv') {
    $src = Join-Path $Tree $r
    if (-not $v5tree -and -not (Test-Path -LiteralPath $src -PathType Leaf)) { continue }
    $items += [pscustomobject]@{ name = $r; src = $src; dst = (Join-Path $GameDir ('Modules\' + $r)); dll = $false }
}
# 16.10 herby kanon (noc-herby): plik rodow idzie z DLL, gdy jest w drzewie
$kl = Join-Path $Tree 'Armoury\ModuleData\arm_ks_land.tsv'   # 16.10 KS kalibracja startu
if (Test-Path -LiteralPath $kl -PathType Leaf) { $items += [pscustomobject]@{ name = 'Armoury\ModuleData\arm_ks_land.tsv'; src = $kl; dst = (Join-Path $GameDir 'Modules\Armoury\ModuleData\arm_ks_land.tsv'); dll = $false } }
$hc = Join-Path $Tree 'Armoury\ModuleData\arm_houses_canon.tsv'
if (Test-Path -LiteralPath $hc -PathType Leaf) { $items += [pscustomobject]@{ name = 'Armoury\ModuleData\arm_houses_canon.tsv'; src = $hc; dst = (Join-Path $GameDir 'Modules\Armoury\ModuleData\arm_houses_canon.tsv'); dll = $false } }
foreach ($i in $items) {
    if (-not (Test-Path -LiteralPath $i.src -PathType Leaf)) { Write-Host ("STOP: brak zrodla " + $i.src); exit 2 }
    if ((Get-Item -LiteralPath $i.src).Length -eq 0) { Write-Host ("STOP: zrodlo puste " + $i.src); exit 2 }
    if ($i.dll) {
        $b = [IO.File]::ReadAllBytes($i.src)
        if ($b.Length -lt 2 -or $b[0] -ne 0x4D -or $b[1] -ne 0x5A) { Write-Host ("STOP: " + $i.src + " nie jest plikiem PE"); exit 2 }
        if (-not (Test-Path -LiteralPath $i.dst)) { Write-Host ("STOP: brak w grze " + $i.dst); exit 2 }
    }
}
$cs = [Text.Encoding]::Unicode.GetString([IO.File]::ReadAllBytes(($items | Where-Object { $_.name -eq 'CrashScribe.dll' }).src))
if ($cs.Contains('autotest.json')) { Write-Host 'STOP: CrashScribe.dll do wgrania ma tryb autotestu - to wersja probna, nie do gry.'; exit 2 }

$mirror = Join-Path $MirrorRoot ($Tag + '-przed')
$rows = @()
foreach ($i in $items) {
    $before = Md5 $i.dst
    $srcMd5 = Md5 $i.src
    $bak = $i.dst + '.bak-' + $Tag + '-przed'
    $row = [pscustomobject]@{ plik = $i.name; bylo = $(if ($before) { $before.Substring(0, 8) } else { 'brak' }); nowe = $srcMd5.Substring(0, 8); wynik = '' }
    if ($before -eq $srcMd5) { $row.wynik = 'bez zmiany (juz ta wersja)'; $rows += $row; continue }
    if ($DryRun) { $row.wynik = 'NA SUCHO: bylaby podmiana'; $rows += $row; continue }
    if ($before) {
        Copy-Item -LiteralPath $i.dst -Destination $bak -Force
        if ((Md5 $bak) -ne $before) { Write-Host ("STOP: kopia " + $bak + " ma inny md5 - przerywam przed podmiana"); exit 3 }
        $mi = Join-Path $mirror $i.name
        $null = New-Item -ItemType Directory -Force -Path (Split-Path -Parent $mi)
        Copy-Item -LiteralPath $i.dst -Destination $mi -Force
        if ((Md5 $mi) -ne $before) { Write-Host ("UWAGA: lustro " + $mi + " ma inny md5") }
    }
    $null = New-Item -ItemType Directory -Force -Path (Split-Path -Parent $i.dst)
    Copy-Item -LiteralPath $i.src -Destination $i.dst -Force
    $after = Md5 $i.dst
    $row.wynik = $(if ($after -eq $srcMd5) { 'WGRANE, md5 OK' } else { 'BLAD: md5 po wgraniu ' + $after })
    $rows += $row
}
$rows | Format-Table -AutoSize | Out-String -Width 200 | Write-Host
$bad = @($rows | Where-Object { $_.wynik -like 'BLAD*' })
if ($bad.Count -gt 0) { Write-Host 'WGRANIE NIEPELNE - patrz wiersze BLAD'; exit 3 }
Write-Host ("Kopie poprzednich wersji: obok plikow (*.bak-" + $Tag + "-przed) i w " + $mirror)
exit 0
