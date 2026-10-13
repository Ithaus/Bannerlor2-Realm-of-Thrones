# cofnij-ujscia-bee.ps1
#
# Cofa zmiane zrobiona przez zamknij-ujscia-bee.ps1: wartosci w pliku
#   Modules\BetterEconomy\ModuleData\better_economy_settings.xml
# wracaja do stanu sprzed zmiany - 13 kluczy ujsc (wariant zbrojowni - tylko AI albo lista z fundamentu - jest rozpoznawany
# z pliku) i klucz 14 (E8/E: EconomicEventDailySpawnChance, losowe zdarzenia BEE); cofana jest kazda grupa, ktora jest "po".
# UWAGA: bez -TylkoZdarzenia cofniecie otwiera z powrotem 13 ujsc BEE (zloto i towar z niczego / w nicosc u AI).
# Zeby cofnac sama paczke E8/E (przywrocic losowe zdarzenia), uzyj -TylkoZdarzenia.
#
# To jest cienka nakladka: cala robota (kontrola "gra zamknieta", kotwice po jednym wystapieniu, kopia
# <plik>.bak-<data>-przed-cofnieciem-BEE-ujscia, zapis z kontrola skrotu, tabela przed / po) siedzi w zamknij-ujscia-bee.ps1,
# ktory musi lezec w tym samym katalogu. Dzieki temu lista kluczy i wartosci istnieje w jednym miejscu.
#
# Cofanie zmienia TYLKO te wartosci (14, 13 albo 1) - inne reczne poprawki w pliku zostaja. (Stara tresc w calosci jest tez
# w kopii <plik>.bak-<data>-przed-BEE-ujscia zrobionej przy zamykaniu.)
#
# Parametry: -Path <plik> (domyslnie prawdziwy plik gry), -TylkoZdarzenia (tylko klucz 14, 13 kluczy ujsc zostaje),
#            -NaSucho (bez zapisu), -ProcesyGry <wzory> (do prob).
# Kody wyjscia jak w zamknij-ujscia-bee.ps1 (0 = cofnieto albo nie bylo czego cofac).
#
# Uruchomienie (Windows PowerShell 5.1):
#   powershell -NoProfile -ExecutionPolicy Bypass -File cofnij-ujscia-bee.ps1 -NaSucho
#   powershell -NoProfile -ExecutionPolicy Bypass -File cofnij-ujscia-bee.ps1
#   powershell -NoProfile -ExecutionPolicy Bypass -File cofnij-ujscia-bee.ps1 -TylkoZdarzenia

[CmdletBinding()]
param(
    [string]$Path,
    [switch]$TylkoZdarzenia,
    [switch]$NaSucho,
    [string[]]$ProcesyGry
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$katalog = Split-Path -Parent $MyInvocation.MyCommand.Path
$glowny = Join-Path $katalog 'zamknij-ujscia-bee.ps1'
if (-not (Test-Path -LiteralPath $glowny -PathType Leaf)) {
    Write-Host ('STOP (kod 1): nie ma pliku ' + $glowny + ' - oba skrypty musza lezec w jednym katalogu. Nic nie zapisano.')
    exit 1
}

$argumenty = @{ Cofnij = $true }
if ($PSBoundParameters.ContainsKey('Path')) {
    if ([string]::IsNullOrWhiteSpace($Path)) {
        Write-Host 'STOP (kod 1): pusty parametr -Path. Nic nie zapisano.'
        exit 1
    }
    $argumenty['Path'] = $Path
}
if ($TylkoZdarzenia) { $argumenty['TylkoZdarzenia'] = $true }
if ($NaSucho) { $argumenty['NaSucho'] = $true }
if ($PSBoundParameters.ContainsKey('ProcesyGry')) { $argumenty['ProcesyGry'] = $ProcesyGry }

& $glowny @argumenty
exit $LASTEXITCODE
