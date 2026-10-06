# cofnij-ujscia-bee.ps1
#
# Cofa zmiane zrobiona przez zamknij-ujscia-bee.ps1: 13 wartosci w pliku
#   Modules\BetterEconomy\ModuleData\better_economy_settings.xml
# wraca do stanu sprzed zmiany (wariant zbrojowni - tylko AI albo lista z fundamentu - jest rozpoznawany z pliku).
#
# To jest cienka nakladka: cala robota (kontrola "gra zamknieta", kotwice po jednym wystapieniu, kopia
# <plik>.bak-<data>-przed-cofnieciem-BEE-ujscia, zapis z kontrola skrotu, tabela przed / po) siedzi w zamknij-ujscia-bee.ps1,
# ktory musi lezec w tym samym katalogu. Dzieki temu lista kluczy i wartosci istnieje w jednym miejscu.
#
# Cofanie zmienia TYLKO te 13 wartosci - inne reczne poprawki w pliku zostaja. (Stara tresc w calosci jest tez w kopii
# <plik>.bak-<data>-przed-BEE-ujscia zrobionej przy zamykaniu.)
#
# Parametry: -Path <plik> (domyslnie prawdziwy plik gry), -NaSucho (bez zapisu), -ProcesyGry <wzory> (do prob).
# Kody wyjscia jak w zamknij-ujscia-bee.ps1 (0 = cofnieto albo nie bylo czego cofac).
#
# Uruchomienie (Windows PowerShell 5.1):
#   powershell -NoProfile -ExecutionPolicy Bypass -File cofnij-ujscia-bee.ps1 -NaSucho
#   powershell -NoProfile -ExecutionPolicy Bypass -File cofnij-ujscia-bee.ps1

[CmdletBinding()]
param(
    [string]$Path,
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
if ($NaSucho) { $argumenty['NaSucho'] = $true }
if ($PSBoundParameters.ContainsKey('ProcesyGry')) { $argumenty['ProcesyGry'] = $ProcesyGry }

& $glowny @argumenty
exit $LASTEXITCODE
