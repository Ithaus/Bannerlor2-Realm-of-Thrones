# bieg.ps1 - uruchamia autotest na DLL z podanego katalogu i z plikami danych z drzewa
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
    [string]$ArmouryDll = ''                            # inny Armoury niz w DllDir (np. z proba drog)
)
$AT = 'C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\3cf3e0ac-5529-4b68-a794-0edec69cfda7\scratchpad\dzien-6\autotest\repo\tools\autotest.ps1'
$rel = @(
    'Armoury\GUI\Prefabs\ArmBurnedVillageLabels.xml',
    'Armoury\GUI\Prefabs\ArmMapVillageTooltip.xml',
    'Armoury\ModuleData\arm_map_roads_probe.tsv',
    'Armoury\ModuleData\arm_map_village_lore.tsv',
    'Armoury\Prefabs\arm_map_roads.xml',
    'CrashScribe\ModuleData\CrashScribe.settings.xml'
)
$ef = @()
foreach ($r in $rel) {
    $src = Join-Path $Tree $r
    if (-not (Test-Path -LiteralPath $src)) { throw "brak pliku danych $src" }
    $ef += ($r + '=' + $src)
}
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
& $AT @a *>&1 | Tee-Object -FilePath $Out
exit $LASTEXITCODE
