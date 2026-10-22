# bieg-bitwa.ps1 - kopia bieg.ps1 (AT5): to samo, ale przez autotest-bitwa.ps1 i z -Battles / -BattleSec (bitwy w polu przed pierwsza doba)
param(
    [Parameter(Mandatory = $true)][string]$DllDir,      # katalog z Armoury.dll, RealisticCaptivity.dll, ForgeView.dll
    [Parameter(Mandatory = $true)][string]$CsAt,        # CrashScribe z trybem autotestu
    [Parameter(Mandatory = $true)][string]$Tree,        # drzewo zrodel (pliki danych poza DLL)
    [Parameter(Mandatory = $true)][string]$Out,         # plik wyjscia
    [int]$Days = 10,
    [string]$LoadSave = '',
    [switch]$Profile,
    [string]$PhotosFile = '',
    [string[]]$Photos = @(),
    [string]$ArmouryDll = '',                           # inny Armoury niz w DllDir (np. z proba drog)
    [int]$Battles = 0,                                  # AT5: tyle bitew w polu przed pierwsza doba (0 = brak)
    [int]$BattleSec = 150,                              # AT5: sekundy rzeczywiste na jedna bitwe
    [switch]$NoArena,                                   # AT5: bez walki cwiczebnej na arenie przed bitwami
    [int]$SwitchWaitSec = 0,                            # 16.10: dluzsze czekanie na przelacznik (bank BA 600 MB laduje sie ok. 4 min); 0 = domyslne skryptu (480 s)
    [string]$ExtraAdd = '',                                # 16.10: dodatkowe pliki 'wzgledna\sciezka=zrodlo' (np. sonda geografii - test only)
    [double]$Speed = 0                                    # 19.10 (Jeff: testy jak najszybciej): wymuszony mnoznik czasu autotestu (0 = jak w grze; max 64)
)
# kod wyjscia jak autotest-bitwa.ps1: 0 OK (przy -Battles misje sprawdzone), 1 nie dotarl / wywrotka, 2 stop przed testem,
# 3 przywracanie nieudane, 4 doby OK - misje NIE sprawdzone (takiego DLL nie wgrywac jako sprawdzonego w misjach)
$AT = 'C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\6ba92574-15a3-4209-a065-ed325b84b1b4\scratchpad\test\autotest-bitwa.ps1'
$rel = @(
    'Armoury\GUI\Prefabs\ArmBurnedVillageLabels.xml',
    'Armoury\GUI\Prefabs\ArmMapVillageTooltip.xml',
    'Armoury\ModuleData\arm_map_roads_probe.tsv',
    'Armoury\ModuleData\arm_map_villages.tsv',
    'Armoury\ModuleData\arm_map_village_lore.tsv',
    'Armoury\Prefabs\arm_map_roads.xml',
    'CrashScribe\ModuleData\CrashScribe.settings.xml',
    'Armoury\SubModule.xml'   # 20.10 m97-surowce: wezly Xmls (arm_items_rare, arm_strings) - jak wgraj-v5
)
# 16.10 uklad wiosek v5 (noc-wioski): pliki _v4 obowiazkowe przy drzewie v5 (jak tools/wgranie/wgraj.ps1 d5c37b2f)
$lay = Join-Path $Tree 'Armoury\ModuleData\arm_map_villages.tsv'
$v5tree = (Test-Path -LiteralPath $lay -PathType Leaf) -and ((Get-Content -LiteralPath $lay -TotalCount 1) -like '*layout v5*')
if ($v5tree) { $rel += 'Armoury\ModuleData\arm_map_villages_v4.tsv'; $rel += 'Armoury\ModuleData\arm_map_village_lore_v4.tsv' }
if (Test-Path -LiteralPath (Join-Path $Tree 'Armoury\ModuleData\arm_houses_canon.tsv')) { $rel += 'Armoury\ModuleData\arm_houses_canon.tsv' }   # 16.10 herby kanon (noc-herby)
if (Test-Path -LiteralPath (Join-Path $Tree 'Armoury\ModuleData\arm_ks_land.tsv')) { $rel += 'Armoury\ModuleData\arm_ks_land.tsv' }   # 16.10 KS kalibracja startu
# 17.10 noc: kazdy plik danych Armoury\ModuleData\arm_*.tsv z drzewa (zajazdy, drogi geo, uklad v6 ...), ktorego jeszcze nie ma na liscie
foreach ($f in @(Get-ChildItem -LiteralPath (Join-Path $Tree 'Armoury\ModuleData') -Filter 'arm_*.tsv' -File -ErrorAction SilentlyContinue)) { $r0 = 'Armoury\ModuleData\' + $f.Name; if ($rel -notcontains $r0) { $rel += $r0 } }
# 20.10 m97-surowce: takze pliki arm_*.xml w Armoury\ModuleData (arm_items_rare.xml, arm_strings.xml - nowe, w grze ich nie ma; po tescie znikaja, wgranie bez kopii .bak)
foreach ($f in @(Get-ChildItem -LiteralPath (Join-Path $Tree 'Armoury\ModuleData') -Filter 'arm_*.xml' -File -ErrorAction SilentlyContinue)) { $r0 = 'Armoury\ModuleData\' + $f.Name; if ($rel -notcontains $r0) { $rel += $r0 } }
# 21.10 m103-sceny: obrazki okien opowiesci Armoury\Images\**\*.png|jpg|jpeg (Prolog\prolog_NN, ROT\rot_<Klasa>_<n>, Kronika\kronika_<id>) - gdy sa w drzewie
# (bez nich okna biora obrazki ROT); nowe pliki po tescie znikaja jak inne pliki danych
$imgRoot = Join-Path $Tree 'Armoury\Images'
if (Test-Path -LiteralPath $imgRoot -PathType Container) {
    $imgFull = (Get-Item -LiteralPath $imgRoot).FullName.TrimEnd('\')
    foreach ($f in @(Get-ChildItem -LiteralPath $imgFull -Recurse -File -ErrorAction SilentlyContinue | Where-Object { @('.png', '.jpg', '.jpeg') -contains $_.Extension.ToLowerInvariant() })) {
        $r0 = 'Armoury\Images\' + $f.FullName.Substring($imgFull.Length).TrimStart('\')
        if ($rel -notcontains $r0) { $rel += $r0 }
        # recenzja m103: gra bierze pierwszy z .png, .jpg, .jpeg - stary plik tego okna w innym formacie w grze wygra z nowym z drzewa (test go nie usuwa)
        $stem = [IO.Path]::GetFileNameWithoutExtension($f.Name)
        foreach ($ox in '.png', '.jpg', '.jpeg') {
            if ($ox -eq $f.Extension.ToLowerInvariant() -or (Test-Path -LiteralPath (Join-Path $f.DirectoryName ($stem + $ox)) -PathType Leaf)) { continue }
            $o = Join-Path 'C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules' ((Split-Path -Parent $r0) + '\' + $stem + $ox)
            if (Test-Path -LiteralPath $o -PathType Leaf) { Write-Host ("UWAGA: w grze lezy " + $o + " - gra wezmie go przed " + $f.Name + " z drzewa (test pokaze stary obrazek); wgraj-v5.ps1 przenosi go do .bak") }
        }
    }
}
# 21.10 m103-lektor: nagrania lektora - Armoury\ModuleData\Lektor\**\*.wav i arm_lektor.tsv (podkatalogi; po tescie autotest-bitwa.ps1 usuwa pliki, ktorych w grze nie bylo -
# zostaje pusty katalog Lektor\EN, co nie szkodzi: lektor bez tsv milczy i pisze linie)
# 21/22.10 Kronika K0: takze nagrania Kroniki *.ogg i manifest arm_lektor_kronika.tsv (jak wgraj-v5.ps1)
$lekDir = Join-Path $Tree 'Armoury\ModuleData\Lektor'
if (Test-Path -LiteralPath $lekDir -PathType Container) {
    $lekFull = (Resolve-Path -LiteralPath $lekDir).Path.TrimEnd('\')
    foreach ($f in @(Get-ChildItem -LiteralPath $lekDir -Recurse -File | Where-Object { ($_.Extension -eq '.wav' -or $_.Extension -eq '.ogg' -or $_.Extension -eq '.tsv') -and $_.Name -notlike '*.bak-*' })) {
        $r0 = 'Armoury\ModuleData\Lektor\' + $f.FullName.Substring($lekFull.Length + 1); if ($rel -notcontains $r0) { $rel += $r0 }
    }
}
# 21/22.10 galaz czcionek + dziennik (noc 21/22.10): WSZYSTKIE prefaby Armoury\GUI\Prefabs\**\*.xml z drzewa (podkatalogi: Nameplate, dziennik)
$npDir = Join-Path $Tree 'Armoury\GUI\Prefabs'
if (Test-Path -LiteralPath $npDir -PathType Container) {
    $npFull = (Get-Item -LiteralPath $npDir).FullName
    foreach ($f in @(Get-ChildItem -LiteralPath $npDir -Filter '*.xml' -File -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.Name -notlike '*.bak-*' })) { $r0 = 'Armoury\GUI\Prefabs\' + $f.FullName.Substring($npFull.Length + 1); if ($rel -notcontains $r0) { $rel += $r0 } }
}
$ef = @()
foreach ($r in $rel) {
    $src = Join-Path $Tree $r
    if (-not (Test-Path -LiteralPath $src)) { throw "brak pliku danych $src" }
    $ef += ($r + '=' + $src)
}
foreach ($x in ($ExtraAdd -split ';')) { if ($x) { $ef += $x } }   # srednik miedzy plikami (tablica przez -File sie rozsypuje)
$arm = $(if ($ArmouryDll) { $ArmouryDll } else { Join-Path $DllDir 'Armoury.dll' })
$a = @{
    CrashScribeDll = $CsAt
    ArmouryDll     = $arm
    ExtraDll       = @(@('RealisticCaptivity.dll', 'ForgeView.dll', 'GrandTourney.dll') | ForEach-Object { Join-Path $DllDir $_ } | Where-Object { Test-Path -LiteralPath $_ })
    ExtraFile      = $ef
    Days           = $Days
}
if ($LoadSave) { $a.LoadSave = $LoadSave }
if ($Profile) { $a.Profile = $true }
if ($PhotosFile) { $a.PhotosFile = $PhotosFile }
if ($Photos.Count -gt 0) { $a.Photos = $Photos }
if ($Battles -gt 0) { $a.Battles = $Battles; $a.BattleSec = $BattleSec; if ($NoArena) { $a.NoArena = $true } }
if ($SwitchWaitSec -gt 0) { $a.SwitchWaitSec = $SwitchWaitSec }
if ($Speed -gt 0) { $a.Speed = $Speed }
& $AT @a *>&1 | Tee-Object -FilePath $Out
exit $LASTEXITCODE
