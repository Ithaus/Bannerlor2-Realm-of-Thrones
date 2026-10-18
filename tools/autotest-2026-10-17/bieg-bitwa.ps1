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
    [string]$ExtraAdd = ''                                # 16.10: dodatkowe pliki 'wzgledna\sciezka=zrodlo' (np. sonda geografii - test only)
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
    'CrashScribe\ModuleData\CrashScribe.settings.xml'
)
# 16.10 uklad wiosek v5 (noc-wioski): pliki _v4 obowiazkowe przy drzewie v5 (jak tools/wgranie/wgraj.ps1 d5c37b2f)
# 17/18.10 sw-przysiolki: uklad v6 (przysiolki) - arm_map_villages.tsv to v6, pliki _v5 (stary uklad v5: klucz VillageLayoutV6 = false i zapis v5 Jeffa
# przechodzacy na v6 po uid - VillageBook.OldLayout) i _v4 obowiazkowe przy drzewie v6
$lay = Join-Path $Tree 'Armoury\ModuleData\arm_map_villages.tsv'
$layHead = $(if (Test-Path -LiteralPath $lay -PathType Leaf) { [string](Get-Content -LiteralPath $lay -TotalCount 1) } else { '' })
$v6tree = $layHead -like '*layout v6*'
$v5tree = ($layHead -like '*layout v5*') -or $v6tree
if ($v5tree) { $rel += 'Armoury\ModuleData\arm_map_villages_v4.tsv'; $rel += 'Armoury\ModuleData\arm_map_village_lore_v4.tsv' }
if ($v6tree) { $rel += 'Armoury\ModuleData\arm_map_villages_v5.tsv'; $rel += 'Armoury\ModuleData\arm_map_village_lore_v5.tsv' }
if (Test-Path -LiteralPath (Join-Path $Tree 'Armoury\ModuleData\arm_houses_canon.tsv')) { $rel += 'Armoury\ModuleData\arm_houses_canon.tsv' }   # 16.10 herby kanon (noc-herby)
if (Test-Path -LiteralPath (Join-Path $Tree 'Armoury\ModuleData\arm_ks_land.tsv')) { $rel += 'Armoury\ModuleData\arm_ks_land.tsv' }   # 16.10 KS kalibracja startu
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
& $AT @a *>&1 | Tee-Object -FilePath $Out
exit $LASTEXITCODE
