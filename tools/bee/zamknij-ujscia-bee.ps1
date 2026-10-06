# zamknij-ujscia-bee.ps1
#
# Zamyka ujscia i kurki AI moda BetterEconomy ("Living Economy" v1.4.5) 13 wartosciami w pliku
#   Modules\BetterEconomy\ModuleData\better_economy_settings.xml
# (decyzja Jeffa 05.10; lista i uzasadnienie: docs\EKONOMIA-FUNDAMENT-2026-10-05.md, rozdz. 4.5; opis paczki: OPIS.md).
#
# Co robi, w tej kolejnosci:
#   1. sprawdza, ze gra jest zamknieta (procesy z listy -ProcesyGry);
#   2. czyta plik bajt w bajt, sprawdza kodowanie (UTF-8, z BOM albo bez) i to, ze jest poprawnym XML;
#   3. dla kazdego z 13 kluczy wymaga DOKLADNIE JEDNEJ kotwicy <Klucz>wartosc-dzis</Klucz> - w tekscie i w drzewie XML;
#      jakakolwiek niezgodnosc (brak, duplikat, inna wartosc, klucz w komentarzu, stan mieszany) = STOP bez zapisu;
#   4. podmienia 13 wartosci w pamieci i sprawdza wynik: rozni sie dokladnie 13 linii, liczba linii i znakow CR bez zmian,
#      w drzewie XML rozni sie dokladnie 13 elementow, kazda nowa wartosc czyta sie tak, jak czyta ja BetterEconomy
#      (SettingsLoader.cs:64-97: int.TryParse / float.TryParse, kultura niezmienna);
#   5. robi kopie pliku <plik>.bak-<data>-przed-BEE-ujscia, wymusza jej zrzut na dysk i sprawdza ja skrotem SHA-256;
#   6. zapisuje przez plik tymczasowy (zapis od razu na dysk, bez pamieci podrecznej systemu - dysk C tej maszyny zeruje
#      koncowki swiezych zapisow) + podmiane, czyta wynik z dysku i porownuje skrot; przy niezgodnosci przywraca kopie;
#   7. wypisuje tabele przed / po (kolumna "Po" czytana z dysku po zapisie).
# Kodowanie, BOM i konce linii pliku zostaja takie, jakie byly (podmieniane sa tylko znaki wartosci).
#
# Parametry:
#   -Path <plik>         plik ustawien; domyslnie prawdziwy plik gry. Do prob podaj kopie.
#   -ListaZFundamentu    trzeci klucz doslownie z listy 4.5: ArmoryRequiredArtisans = 1000000000. Ten klucz blokuje zbrojownie
#                        miasta takze GRACZOWI (TownEconomyCampaignBehavior.cs:542). Bez tego przelacznika skrypt zamyka
#                        zbrojownie tylko AI kluczem ArmoryAiCheckCooldownDays = 1000000000 (TownEconomyCampaignBehavior.cs:2083)
#                        - zgodnie z decyzja "funkcje gracza bez zmian". W obu wariantach zmienia sie dokladnie 13 wartosci.
#   -Cofnij              kierunek odwrotny: 13 wartosci wraca do stanu sprzed zmiany (wariant zbrojowni rozpoznawany sam).
#                        Wygodniej: cofnij-ujscia-bee.ps1.
#   -NaSucho             wszystkie kontrole i tabela, bez kopii i bez zapisu.
#   -ProcesyGry <wzory>  nazwy procesow uznawanych za gre (do prob kontroli "gra uruchomiona"); kilka wzorow: jeden napis
#                        z przecinkami, np. -ProcesyGry "Bannerlord*,Launcher.Native" (skrypt sam rozbija go na liste).
#
# Kody wyjscia: 0 = zmieniono albo plik byl juz w stanie docelowym (nic nie zapisano); 1 = STOP bez zapisu (kontrola);
#               2 = gra uruchomiona (bez zapisu); 3 = blad przy kopii albo zapisie (komunikat mowi, w jakim stanie jest plik).
#
# Uruchomienie (Windows PowerShell 5.1):
#   powershell -NoProfile -ExecutionPolicy Bypass -File zamknij-ujscia-bee.ps1 -NaSucho
#   powershell -NoProfile -ExecutionPolicy Bypass -File zamknij-ujscia-bee.ps1
#
# Plik jest czystym ASCII (komunikaty bez polskich znakow), zeby PowerShell 5.1 czytal go tak samo przy kazdej stronie kodowej.

[CmdletBinding()]
param(
    [string]$Path = 'C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\BetterEconomy\ModuleData\better_economy_settings.xml',
    [switch]$ListaZFundamentu,
    [switch]$Cofnij,
    [switch]$NaSucho,
    [string[]]$ProcesyGry = @('Bannerlord*', 'Launcher.Native', 'TaleWorlds.MountAndBlade*')
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

# ------------------------------------------------------------------ 13 kluczy (kolejnosc jak w fundamencie 4.5)
# Typ = typ pola w BetterEconomy.Config.BetterEconomySettings; Przed = wartosc w pliku dzis (i wbudowana domyslna BEE);
# Po = wartosc docelowa.
function Nowy-Klucz([string]$Klucz, [string]$Typ, [string]$Przed, [string]$Po, [string]$Kogo) {
    return [pscustomobject]@{ Klucz = $Klucz; Typ = $Typ; Przed = $Przed; Po = $Po; Kogo = $Kogo }
}

$ZbrojowniaTylkoAI = Nowy-Klucz 'ArmoryAiCheckCooldownDays' 'int'   '14'  '1000000000' 'AI'
$ZbrojowniaLista   = Nowy-Klucz 'ArmoryRequiredArtisans'    'float' '220' '1000000000' 'AI + gracz (blokuje zbrojownie gracza)'

$Wspolne = @(
    (Nowy-Klucz 'CastleAiLeaderReserveGold'              'int'   '25000' '1000000000' 'AI'),
    (Nowy-Klucz 'LordInvestmentReserveFlat'              'int'   '5000'  '1000000000' 'AI'),
    # tu wchodzi klucz zbrojowni (pozycja 3 listy)
    (Nowy-Klucz 'VillageDiversionRelationThreshold'      'float' '30'    '-101'       'swiat (takze wsie gracza)'),
    (Nowy-Klucz 'VillageDiversionGrievanceThreshold'     'float' '50'    '101'        'swiat (takze wsie gracza)'),
    (Nowy-Klucz 'VillageSecondaryRequiredStableDays'     'int'   '30'    '1000000000' 'swiat (takze wsie gracza)'),
    (Nowy-Klucz 'CaravanDeliveryMinGold'                 'int'   '1500'  '1000000000' 'swiat (takze karawany gracza)'),
    (Nowy-Klucz 'CaravanEscortHireMinGold'               'int'   '6000'  '1000000000' 'swiat (takze karawany gracza)'),
    (Nowy-Klucz 'CaravanRecruitPromotionEnabled'         'int'   '1'     '0'          'swiat (takze karawany gracza)'),
    (Nowy-Klucz 'RouteDangerMaxLossRatio'                'float' '0.38'  '0'          'swiat (takze karawany gracza)'),
    (Nowy-Klucz 'TradeAgreementCustomsMin'               'int'   '50'    '0'          'krolowie (gracz tylko jako krol)'),
    (Nowy-Klucz 'TradeAgreementCorridorProsperityPerDay' 'float' '0.15'  '0'          'swiat (miasta korytarzy)'),
    (Nowy-Klucz 'RaidPeasantFlightFraction'              'float' '0.08'  '0'          'swiat (takze wsie gracza)')
)

function Zloz-Liste($Zbrojownia) {
    # 13 kluczy: 2 pierwsze wspolne, klucz zbrojowni, 10 pozostalych
    $l = New-Object System.Collections.Generic.List[object]
    $l.Add($Wspolne[0]); $l.Add($Wspolne[1]); $l.Add($Zbrojownia)
    for ($i = 2; $i -lt $Wspolne.Length; $i++) { $l.Add($Wspolne[$i]) }
    return , $l.ToArray()
}

# ------------------------------------------------------------------ narzedzia
function Stop-Skrypt([int]$Kod, [string]$Tekst) {
    $ex = New-Object System.InvalidOperationException($Tekst)
    $ex.Data['Kod'] = $Kod
    throw $ex
}

function Policz([string]$Tekst, [string]$Wzor) {
    # liczba wystapien wzoru; porownanie znak w znak (bez kultury, z rozroznieniem wielkosci liter)
    $n = 0
    $i = 0
    while ($true) {
        $i = $Tekst.IndexOf($Wzor, $i, [System.StringComparison]::Ordinal)
        if ($i -lt 0) { break }
        $n++
        $i += $Wzor.Length
    }
    return $n
}

function Skrot([byte[]]$Bajty) {
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try { return ([System.BitConverter]::ToString($sha.ComputeHash($Bajty)) -replace '-', '').ToLowerInvariant() }
    finally { $sha.Dispose() }
}

function Zdekoduj([byte[]]$Bajty) {
    # zwraca: Tekst, Bom. Tylko UTF-8 (z BOM albo bez); bledne bajty = STOP.
    if ($Bajty.Length -eq 0) { Stop-Skrypt 1 'Plik jest pusty (0 bajtow).' }
    if ($Bajty.Length -ge 2 -and (($Bajty[0] -eq 0xFF -and $Bajty[1] -eq 0xFE) -or ($Bajty[0] -eq 0xFE -and $Bajty[1] -eq 0xFF))) {
        Stop-Skrypt 1 'Plik jest w UTF-16 - skrypt obsluguje tylko UTF-8.'
    }
    $bom = ($Bajty.Length -ge 3 -and $Bajty[0] -eq 0xEF -and $Bajty[1] -eq 0xBB -and $Bajty[2] -eq 0xBF)
    $start = 0
    if ($bom) { $start = 3 }
    $utf8 = New-Object System.Text.UTF8Encoding($false, $true)
    $tekst = $null
    try { $tekst = $utf8.GetString($Bajty, $start, $Bajty.Length - $start) }
    catch { Stop-Skrypt 1 'Plik nie jest poprawnym UTF-8 (bledne bajty) - nie ruszam.' }
    return [pscustomobject]@{ Tekst = $tekst; Bom = $bom }
}

function Zakoduj([string]$Tekst, [bool]$Bom) {
    $utf8 = New-Object System.Text.UTF8Encoding($false, $true)
    [byte[]]$b = $utf8.GetBytes($Tekst)
    if (-not $Bom) { return , $b }
    [byte[]]$w = New-Object byte[] ($b.Length + 3)
    $w[0] = 0xEF; $w[1] = 0xBB; $w[2] = 0xBF
    [System.Array]::Copy($b, 0, $w, 3, $b.Length)
    return , $w
}

function Zapisz-Trwale([string]$Plik, [byte[]]$Bajty) {
    # Zapis z ominieciem pamieci podrecznej systemu (WriteThrough) i wymuszonym zrzutem na dysk (FlushFileBuffers).
    # Powod: dysk C tej maszyny potrafi wyzerowac koncowki ostatnio zapisanych plikow (rozmiar i data zostaja) -
    # dane, ktore fizycznie leza juz na dysku, sa na to odporne. Blad zapisu = wyjatek (lapie go etap "zapis").
    $fs = New-Object System.IO.FileStream($Plik, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write, [System.IO.FileShare]::None, 4096, [System.IO.FileOptions]::WriteThrough)
    try {
        $fs.Write($Bajty, 0, $Bajty.Length)
        $fs.Flush($true)
    }
    finally { $fs.Dispose() }
}

function Zrzuc-Na-Dysk([string]$Plik) {
    # Wymusza zrzut na dysk pliku, ktory juz istnieje (kopia zrobiona przez File.Copy). Nie rzuca wyjatku:
    # $true = zrzucony, $false = nie udalo sie (np. plik tylko do odczytu) - wtedy skrypt tylko o tym pisze.
    try {
        $fs = New-Object System.IO.FileStream($Plik, [System.IO.FileMode]::Open, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::Read)
        try { $fs.Flush($true) }
        finally { $fs.Dispose() }
        return $true
    }
    catch { return $false }
}

function Te-Same-Bajty([byte[]]$A, [byte[]]$B) {
    if ($A.Length -ne $B.Length) { return $false }
    for ($i = 0; $i -lt $A.Length; $i++) { if ($A[$i] -ne $B[$i]) { return $false } }
    return $true
}

function Wczytaj-Elementy([byte[]]$Bajty) {
    # Tak jak BetterEconomy (SettingsLoader.cs:25-38): XmlDocument, dzieci korzenia typu Element, InnerText.
    # Akcesory get_...() zamiast wlasciwosci - PowerShell podmienia wlasciwosci wezla XML nazwami jego dzieci.
    $doc = New-Object System.Xml.XmlDocument
    $doc.set_XmlResolver($null)
    $ms = New-Object System.IO.MemoryStream(, $Bajty)
    try { $doc.Load($ms) }
    catch { Stop-Skrypt 1 ('Plik nie jest poprawnym XML: ' + $_.Exception.Message) }
    finally { $ms.Dispose() }
    $korzen = $doc.get_DocumentElement()
    if ($null -eq $korzen) { Stop-Skrypt 1 'XML bez elementu glownego.' }
    if ($korzen.get_Name() -cne 'BetterEconomySettings') {
        Stop-Skrypt 1 ('To nie jest plik ustawien BetterEconomy: element glowny <' + $korzen.get_Name() + '>, oczekiwany <BetterEconomySettings>.')
    }
    $lista = New-Object System.Collections.Generic.List[object]
    foreach ($w in $korzen.get_ChildNodes()) {
        if ($w.get_NodeType() -eq [System.Xml.XmlNodeType]::Element) {
            $lista.Add([pscustomobject]@{ Nazwa = $w.get_Name(); Wartosc = $w.get_InnerText() })
        }
    }
    return , $lista.ToArray()
}

function Stan-Klucza($Def, [string]$Tekst, $Elementy) {
    # PRZED / PO / INNA (wartosc spoza pary) / BRAK / NIEJEDNOZNACZNY (duplikat, komentarz, tekst niezgodny z drzewem XML)
    $otw = '<' + $Def.Klucz + '>'
    $zam = '</' + $Def.Klucz + '>'
    $nOtw = Policz $Tekst $otw
    $nZam = Policz $Tekst $zam
    $wXml = @($Elementy | Where-Object { $_.Nazwa -ceq $Def.Klucz })
    $stan = 'NIEJEDNOZNACZNY'
    $wartosc = ''
    $linia = 0
    $pozycja = -1
    $opis = ''
    if ($nOtw -eq 0 -and $nZam -eq 0 -and $wXml.Length -eq 0) {
        $stan = 'BRAK'
        $opis = 'klucza nie ma w pliku'
    }
    elseif ($nOtw -ne 1 -or $nZam -ne 1 -or $wXml.Length -ne 1) {
        $opis = ('w tekscie: {0} x {1}, {2} x {3}; w drzewie XML: {4} elementow' -f $nOtw, $otw, $nZam, $zam, $wXml.Length)
    }
    else {
        $p = $Tekst.IndexOf($otw, [System.StringComparison]::Ordinal)
        $k = $Tekst.IndexOf($zam, [System.StringComparison]::Ordinal)
        if ($k -lt $p + $otw.Length) {
            $opis = 'znacznik zamykajacy stoi przed otwierajacym'
        }
        else {
            $wartosc = $Tekst.Substring($p + $otw.Length, $k - $p - $otw.Length)
            $pozycja = $p
            $przedKluczem = $Tekst.Substring(0, $p)
            $linia = (Policz $przedKluczem "`n") + 1
            if ($wartosc -cne $wXml[0].Wartosc) {
                $opis = 'tekst miedzy znacznikami rozni sie od wartosci w drzewie XML (komentarz, encja albo zagniezdzenie)'
            }
            elseif ($wartosc -ceq $Def.Przed) { $stan = 'PRZED' }
            elseif ($wartosc -ceq $Def.Po) { $stan = 'PO' }
            else {
                $stan = 'INNA'
                $opis = ('wartosc "{0}" - oczekiwana "{1}" (przed) albo "{2}" (po)' -f $wartosc, $Def.Przed, $Def.Po)
            }
        }
    }
    return [pscustomobject]@{ Def = $Def; Klucz = $Def.Klucz; Stan = $stan; Wartosc = $wartosc; Linia = $linia; Pozycja = $pozycja; Opis = $opis }
}

function Czyta-Sie([string]$Typ, [string]$Wartosc) {
    # ta sama regula co BetterEconomy.Config.SettingsLoader.TryAssign (SettingsLoader.cs:64-97)
    $s = $Wartosc.Trim()
    if ($s.Length -eq 0) { return $false }
    $inv = [System.Globalization.CultureInfo]::InvariantCulture
    if ($Typ -ceq 'int') {
        $wi = 0
        return [int]::TryParse($s, [System.Globalization.NumberStyles]::Integer, $inv, [ref]$wi)
    }
    if ($Typ -ceq 'float') {
        $wf = [single]0
        return [single]::TryParse($s, [System.Globalization.NumberStyles]::Float, $inv, [ref]$wf)
    }
    return $false
}

function Pokaz-Stany($Stany) {
    $t = foreach ($s in $Stany) {
        [pscustomobject][ordered]@{ Klucz = $s.Klucz; Linia = $s.Linia; Stan = $s.Stan; Wartosc = $s.Wartosc; Uwaga = $s.Opis }
    }
    Write-Host (($t | Format-Table -AutoSize | Out-String -Width 260).TrimEnd())
}

# ------------------------------------------------------------------ przebieg
$kod = 0
$tmp = $null
$pelna = $null
$kopia = $null
$etap = 'kontrola'          # kontrola -> kopia -> zapis -> zapisano
try {
    $tryb = 'ZAMKNIECIE UJSC'
    if ($Cofnij) { $tryb = 'COFNIECIE' }
    if ($Cofnij -and $ListaZFundamentu) { Stop-Skrypt 1 'Przelaczniki -Cofnij i -ListaZFundamentu wykluczaja sie: przy cofaniu wariant zbrojowni jest rozpoznawany z pliku.' }

    $pelna = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Path)
    Write-Host ('== BetterEconomy, 13 kluczy: ' + $tryb + $(if ($NaSucho) { ' (NA SUCHO - bez zapisu)' } else { '' }))
    Write-Host ('Plik: ' + $pelna)
    if (-not (Test-Path -LiteralPath $pelna -PathType Leaf)) { Stop-Skrypt 1 'Nie ma takiego pliku.' }
    # Najdluzsza nazwa, jaka skrypt moze utworzyc obok pliku, to kopia z godzina przy cofaniu. Windows PowerShell 5.1
    # nie obsluzy sciezki powyzej 259 znakow - lepiej stanac teraz niz w polowie (kopia zrobiona, zapis nieudany).
    $najdluzszy = '.bak-0000-00-00-000000-przed-cofnieciem-BEE-ujscia'
    if ($pelna.Length + $najdluzszy.Length -gt 259) {
        Stop-Skrypt 1 ('Sciezka pliku jest za dluga ({0} znakow): kopia obok niego mialaby do {1} znakow, a granica to 259. Nic nie zapisano.' -f $pelna.Length, ($pelna.Length + $najdluzszy.Length))
    }

    # 1. gra zamknieta
    # Wywolanie "powershell -File ... -ProcesyGry a,b" przekazuje w PowerShell 5.1 JEDEN napis "a,b" (bez rozbicia na liste).
    # Get-Process nie znalazlby wtedy niczego i kontrola bylaby po cichu wylaczona - dlatego wzory sa tu rozbijane na przecinkach.
    $wzoryGry = @($ProcesyGry | ForEach-Object { if ($null -ne $_) { ([string]$_) -split ',' } } | ForEach-Object { $_.Trim() } | Where-Object { $_.Length -gt 0 })
    if ($wzoryGry.Length -eq 0) { Stop-Skrypt 1 'Pusta lista -ProcesyGry - kontrola gry bylaby wylaczona. Nic nie zapisano.' }
    $procesy = @(Get-Process -Name $wzoryGry -ErrorAction SilentlyContinue)
    if ($procesy.Length -gt 0) {
        $opisProcesow = (($procesy | ForEach-Object { '{0} (PID {1})' -f $_.ProcessName, $_.Id }) -join ', ')
        if ($NaSucho) {
            Write-Host ('UWAGA: gra jest uruchomiona: ' + $opisProcesow + '. Prawdziwy przebieg zatrzymalby sie tutaj.')
        }
        else {
            Stop-Skrypt 2 ('Gra jest uruchomiona: ' + $opisProcesow + '. Zamknij gre i launcher, potem uruchom skrypt ponownie. Nic nie zapisano.')
        }
    }
    else {
        Write-Host ('Gra zamknieta: tak (nie dziala zaden proces: ' + ($wzoryGry -join ', ') + ').')
    }

    # 2. odczyt i kontrola pliku
    [byte[]]$stare = [System.IO.File]::ReadAllBytes($pelna)
    $skrotStary = Skrot $stare
    $odczyt = Zdekoduj $stare
    [string]$tekst = $odczyt.Tekst
    [bool]$bom = $odczyt.Bom
    [byte[]]$powrot = Zakoduj $tekst $bom
    if (-not (Te-Same-Bajty $stare $powrot)) { Stop-Skrypt 1 'Odczyt i ponowny zapis tekstu nie daja tych samych bajtow - nie ruszam pliku.' }
    $elementy = Wczytaj-Elementy $stare
    $nCR = Policz $tekst "`r"
    $nLF = Policz $tekst "`n"
    $konce = 'LF'
    if ($nCR -eq $nLF -and $nCR -gt 0) { $konce = 'CRLF' } elseif ($nCR -gt 0) { $konce = 'mieszane (CR ' + $nCR + ', LF ' + $nLF + ')' }
    Write-Host ('Stan pliku: {0} bajtow, SHA-256 {1}, UTF-8 {2}, konce linii {3}, elementow {4}.' -f $stare.Length, $skrotStary, $(if ($bom) { 'z BOM' } else { 'bez BOM' }), $konce, $elementy.Length)
    $tryby = @($elementy | Where-Object { $_.Nazwa -ceq 'BannerKingsCompatibilityMode' })
    if ($tryby.Length -eq 1 -and $tryby[0].Wartosc.Trim() -ceq '1') {
        Write-Host 'BannerKingsCompatibilityMode = 1 (skrypt go nie zmienia).'
    }
    else {
        Write-Host 'UWAGA: BannerKingsCompatibilityMode nie jest rowne 1 - fundament 4.5 kaze zostawic 1 (skrypt go nie zmienia).'
    }

    # 3. stan 14 znanych kluczy (12 wspolnych + oba warianty zbrojowni)
    $wszystkie = @($Wspolne) + @($ZbrojowniaTylkoAI, $ZbrojowniaLista)
    $stany = @(foreach ($d in $wszystkie) { Stan-Klucza $d $tekst $elementy })
    $zle = @($stany | Where-Object { $_.Stan -cne 'PRZED' -and $_.Stan -cne 'PO' })
    if ($zle.Length -gt 0) {
        Pokaz-Stany $stany
        Stop-Skrypt 1 ('Kotwice niezgodne ({0} z {1} kluczy) - kazdy klucz musi wystapic dokladnie raz, z wartoscia "przed" albo "po". Nic nie zapisano.' -f $zle.Length, $stany.Length)
    }
    $stanWg = @{}
    foreach ($s in $stany) { $stanWg[$s.Klucz] = $s }
    $wspolnePo = @($Wspolne | Where-Object { $stanWg[$_.Klucz].Stan -ceq 'PO' }).Length
    $aiPo = ($stanWg[$ZbrojowniaTylkoAI.Klucz].Stan -ceq 'PO')
    $listaPo = ($stanWg[$ZbrojowniaLista.Klucz].Stan -ceq 'PO')

    $wybrane = $null       # 13 definicji do zmiany
    if (-not $Cofnij) {
        $zbrojownia = $ZbrojowniaTylkoAI
        $druga = $ZbrojowniaLista
        if ($ListaZFundamentu) { $zbrojownia = $ZbrojowniaLista; $druga = $ZbrojowniaTylkoAI }
        $wybrane = Zloz-Liste $zbrojownia
        $wybranePo = @($wybrane | Where-Object { $stanWg[$_.Klucz].Stan -ceq 'PO' }).Length
        $drugaPo = ($stanWg[$druga.Klucz].Stan -ceq 'PO')
        Write-Host ('Wariant zbrojowni: {0} ({1}).' -f $zbrojownia.Klucz, $zbrojownia.Kogo)
        if ($wybranePo -eq 13 -and -not $drugaPo) {
            Pokaz-Stany @($wybrane | ForEach-Object { $stanWg[$_.Klucz] })
            Write-Host 'WYNIK: plik jest juz w stanie docelowym (13 z 13 kluczy). Nic nie zapisano, kopii nie robiono.'
            exit 0
        }
        if ($wybranePo -ne 0 -or $drugaPo) {
            Pokaz-Stany $stany
            $rada = 'Stan mieszany - czesc kluczy jest juz zmieniona.'
            if ($wspolnePo -eq 12 -and $drugaPo -and -not ($stanWg[$zbrojownia.Klucz].Stan -ceq 'PO')) {
                $rada = 'Plik jest juz zamkniety w DRUGIM wariancie zbrojowni (' + $druga.Klucz + '). Zeby zmienic wariant, najpierw uruchom cofnij-ujscia-bee.ps1.'
            }
            Stop-Skrypt 1 ($rada + ' Nic nie zapisano.')
        }
    }
    else {
        if ($wspolnePo -eq 0 -and -not $aiPo -and -not $listaPo) {
            Pokaz-Stany $stany
            Write-Host 'WYNIK: nie ma czego cofac - wszystkie klucze maja wartosci sprzed zmiany. Nic nie zapisano, kopii nie robiono.'
            exit 0
        }
        if ($wspolnePo -ne 12 -or ($aiPo -eq $listaPo)) {
            Pokaz-Stany $stany
            Stop-Skrypt 1 'Stan mieszany - do cofniecia potrzeba 12 kluczy wspolnych i dokladnie jednego klucza zbrojowni w stanie "po". Nic nie zapisano.'
        }
        $zbrojownia = $ZbrojowniaTylkoAI
        if ($listaPo) { $zbrojownia = $ZbrojowniaLista }
        $wybrane = Zloz-Liste $zbrojownia
        Write-Host ('Wariant zbrojowni rozpoznany w pliku: {0}.' -f $zbrojownia.Klucz)
    }
    if (@($wybrane).Length -ne 13) { Stop-Skrypt 1 'Blad wewnetrzny: lista kluczy nie ma 13 pozycji.' }

    # 4. podmiana w pamieci: kazda kotwica dokladnie raz
    $plan = New-Object System.Collections.Generic.List[object]
    foreach ($d in $wybrane) {
        $z = $d.Przed
        $na = $d.Po
        if ($Cofnij) { $z = $d.Po; $na = $d.Przed }
        if (-not (Czyta-Sie $d.Typ $na)) { Stop-Skrypt 1 ('Wartosc docelowa "{0}" klucza {1} nie czyta sie jako {2}.' -f $na, $d.Klucz, $d.Typ) }
        $plan.Add([pscustomobject]@{ Def = $d; Klucz = $d.Klucz; Z = $z; Na = $na; Stara = ('<{0}>{1}</{0}>' -f $d.Klucz, $z); Nowa = ('<{0}>{1}</{0}>' -f $d.Klucz, $na); Linia = $stanWg[$d.Klucz].Linia })
    }
    [string]$nowyTekst = $tekst
    foreach ($p in $plan) {
        $ile = Policz $nowyTekst $p.Stara
        if ($ile -ne 1) { Stop-Skrypt 1 ('Kotwica {0} wystepuje {1} razy (ma byc 1). Nic nie zapisano.' -f $p.Stara, $ile) }
        $poz = $nowyTekst.IndexOf($p.Stara, [System.StringComparison]::Ordinal)
        $nowyTekst = $nowyTekst.Remove($poz, $p.Stara.Length).Insert($poz, $p.Nowa)
    }
    [byte[]]$nowe = Zakoduj $nowyTekst $bom
    $skrotNowy = Skrot $nowe

    # 4a. kontrola tekstu: te same linie poza dokladnie 13, znaki CR bez zmian, dlugosc zgodna z rachunkiem
    $linieA = $tekst.Split("`n")
    $linieB = $nowyTekst.Split("`n")
    if ($linieA.Length -ne $linieB.Length) { Stop-Skrypt 1 'Kontrola: zmienila sie liczba linii. Nic nie zapisano.' }
    $rozne = New-Object System.Collections.Generic.List[int]
    for ($i = 0; $i -lt $linieA.Length; $i++) { if ($linieA[$i] -cne $linieB[$i]) { $rozne.Add($i + 1) } }
    if ($rozne.Count -ne 13) { Stop-Skrypt 1 ('Kontrola: rozni sie {0} linii, ma byc dokladnie 13 (kazdy klucz w osobnej linii). Nic nie zapisano.' -f $rozne.Count) }
    foreach ($p in $plan) {
        if (-not $rozne.Contains([int]$p.Linia)) { Stop-Skrypt 1 ('Kontrola: linia {0} klucza {1} nie jest wsrod zmienionych. Nic nie zapisano.' -f $p.Linia, $p.Klucz) }
        if ($linieB[$p.Linia - 1].Trim() -cne $p.Nowa) { Stop-Skrypt 1 ('Kontrola: linia {0} ma inna tresc niz {1}. Nic nie zapisano.' -f $p.Linia, $p.Nowa) }
    }
    if ((Policz $nowyTekst "`r") -ne $nCR) { Stop-Skrypt 1 'Kontrola: zmienila sie liczba znakow CR. Nic nie zapisano.' }
    $delta = 0
    foreach ($p in $plan) { $delta += ($p.Nowa.Length - $p.Stara.Length) }
    if ($nowe.Length -ne $stare.Length + $delta) { Stop-Skrypt 1 'Kontrola: dlugosc pliku nie zgadza sie z rachunkiem podmian. Nic nie zapisano.' }

    # 4b. kontrola drzewa XML: te same elementy w tej samej kolejnosci, rozni sie dokladnie 13 wartosci
    $elementyNowe = Wczytaj-Elementy $nowe
    if ($elementyNowe.Length -ne $elementy.Length) { Stop-Skrypt 1 'Kontrola XML: zmienila sie liczba elementow. Nic nie zapisano.' }
    $naWg = @{}
    foreach ($p in $plan) { $naWg[$p.Klucz] = $p.Na }
    $rozneXml = 0
    for ($i = 0; $i -lt $elementy.Length; $i++) {
        if ($elementy[$i].Nazwa -cne $elementyNowe[$i].Nazwa) { Stop-Skrypt 1 'Kontrola XML: zmienila sie nazwa albo kolejnosc elementow. Nic nie zapisano.' }
        if ($elementy[$i].Wartosc -cne $elementyNowe[$i].Wartosc) {
            $rozneXml++
            $n = $elementy[$i].Nazwa
            if (-not $naWg.ContainsKey($n) -or $elementyNowe[$i].Wartosc -cne $naWg[$n]) { Stop-Skrypt 1 ('Kontrola XML: nieoczekiwana zmiana elementu {0}. Nic nie zapisano.' -f $n) }
        }
    }
    if ($rozneXml -ne 13) { Stop-Skrypt 1 ('Kontrola XML: rozni sie {0} elementow, ma byc dokladnie 13. Nic nie zapisano.' -f $rozneXml) }
    Write-Host ('Kontrole w pamieci: 13 kotwic po 1 wystapieniu, 13 linii roznych, {0} elementow XML ({1} bez zmian), kodowanie i konce linii zachowane.' -f $elementyNowe.Length, ($elementyNowe.Length - 13))

    $wiersze = foreach ($p in $plan) {
        [pscustomobject][ordered]@{ Nr = ($plan.IndexOf($p) + 1); Klucz = $p.Klucz; Linia = $p.Linia; Typ = $p.Def.Typ; Przed = $p.Z; Po = $p.Na; Dotyczy = $p.Def.Kogo }
    }

    if ($NaSucho) {
        Write-Host (($wiersze | Format-Table -AutoSize | Out-String -Width 260).TrimEnd())
        Write-Host ('WYNIK (NA SUCHO): zmieniloby sie 13 wartosci; SHA-256 po zmianie bylby {0}. Nic nie zapisano, kopii nie robiono.' -f $skrotNowy)
        exit 0
    }

    # 5. kopia pliku
    $etap = 'kopia'
    $przyrostek = '-przed-BEE-ujscia'
    if ($Cofnij) { $przyrostek = '-przed-cofnieciem-BEE-ujscia' }
    $kopia = $pelna + '.bak-' + (Get-Date -Format 'yyyy-MM-dd') + $przyrostek
    $kopiaIstniala = $false
    if (Test-Path -LiteralPath $kopia) {
        [byte[]]$wKopii = [System.IO.File]::ReadAllBytes($kopia)
        if ((Skrot $wKopii) -ceq $skrotStary) {
            $kopiaIstniala = $true
        }
        else {
            # kopia z dzisiejsza data juz jest i ma inna tresc - nie nadpisujemy jej, nowa dostaje godzine w nazwie
            $kopia = $pelna + '.bak-' + (Get-Date -Format 'yyyy-MM-dd-HHmmss') + $przyrostek
            if (Test-Path -LiteralPath $kopia) { Stop-Skrypt 1 ('Kopia o nazwie ' + $kopia + ' juz istnieje i ma inna tresc. Nic nie zapisano.') }
        }
    }
    if (-not $kopiaIstniala) {
        try { [System.IO.File]::Copy($pelna, $kopia, $false) }
        catch { Stop-Skrypt 3 ('Nie udalo sie zrobic kopii (' + $_.Exception.Message + '). Plik ustawien nietkniety.') }
    }
    $kopiaNaDysku = Zrzuc-Na-Dysk $kopia
    [byte[]]$wKopii = [System.IO.File]::ReadAllBytes($kopia)
    if ((Skrot $wKopii) -cne $skrotStary) { Stop-Skrypt 3 ('Kopia ' + $kopia + ' rozni sie od oryginalu. Plik ustawien nietkniety.') }
    Write-Host ('Kopia: ' + $kopia + $(if ($kopiaIstniala) { ' (istniala juz, identyczna z plikiem)' } else { ' (nowa, zgodna skrotem)' }) + $(if ($kopiaNaDysku) { ', zrzucona na dysk' } else { '; UWAGA: nie udalo sie wymusic jej zrzutu na dysk (plik tylko do odczytu?)' }))

    # 6. zapis: plik tymczasowy obok (zrzucony na dysk), kontrola, podmiana, zrzut, kontrola z dysku
    $etap = 'zapis'
    $tmp = $pelna + '.tmp-bee-ujscia'
    try {
        Zapisz-Trwale $tmp $nowe
        [byte[]]$wTmp = [System.IO.File]::ReadAllBytes($tmp)
        if ((Skrot $wTmp) -cne $skrotNowy) { throw 'plik tymczasowy rozni sie od zamierzonej tresci' }
        [System.IO.File]::Replace($tmp, $pelna, [NullString]::Value)
    }
    catch {
        $komunikat = $_.Exception.Message
        if (-not (Test-Path -LiteralPath $pelna -PathType Leaf)) {
            # Podmiana zdazyla usunac stary plik i nie wstawila nowego. Bez pliku BetterEconomy bierze wartosci wbudowane
            # (SettingsLoader.cs:20-24), czyli przepadlyby WSZYSTKIE wczesniejsze ustawienia - dlatego od razu wraca kopia.
            [System.IO.File]::Copy($kopia, $pelna, $false)
            [void](Zrzuc-Na-Dysk $pelna)
            [byte[]]$odtworzony = [System.IO.File]::ReadAllBytes($pelna)
            if ((Skrot $odtworzony) -ceq $skrotStary) { Stop-Skrypt 3 ('Zapis nieudany (' + $komunikat + '). Plik ustawien zniknal w trakcie podmiany - przywrocony z kopii ' + $kopia + ' (stara tresc, sprawdzona skrotem).') }
            Stop-Skrypt 3 ('Zapis nieudany (' + $komunikat + '). Plik ustawien zniknal w trakcie podmiany, a przywrocony z kopii rozni sie od niej. Przywroc recznie z ' + $kopia + '.')
        }
        [byte[]]$teraz = [System.IO.File]::ReadAllBytes($pelna)
        if ((Skrot $teraz) -ceq $skrotStary) { Stop-Skrypt 3 ('Zapis nieudany (' + $komunikat + '). Plik ustawien nietkniety.') }
        [System.IO.File]::Copy($kopia, $pelna, $true)
        [void](Zrzuc-Na-Dysk $pelna)
        Stop-Skrypt 3 ('Zapis nieudany (' + $komunikat + '). Plik ustawien przywrocony z kopii ' + $kopia + ' - sprawdz go.')
    }
    $plikNaDysku = Zrzuc-Na-Dysk $pelna
    [byte[]]$zDysku = [System.IO.File]::ReadAllBytes($pelna)
    if ((Skrot $zDysku) -cne $skrotNowy) {
        [System.IO.File]::Copy($kopia, $pelna, $true)
        [void](Zrzuc-Na-Dysk $pelna)
        [byte[]]$poPrzywroceniu = [System.IO.File]::ReadAllBytes($pelna)
        if ((Skrot $poPrzywroceniu) -ceq $skrotStary) { Stop-Skrypt 3 'Plik na dysku rozni sie od zamierzonej tresci. Przywrocono kopie - plik ustawien ma stara tresc.' }
        Stop-Skrypt 3 ('Plik na dysku rozni sie od zamierzonej tresci, a przywrocenie kopii sie nie powiodlo. Przywroc recznie z ' + $kopia + '.')
    }
    $etap = 'zapisano'

    # 7. tabela przed / po - "Po" z dysku
    $elementyZDysku = Wczytaj-Elementy $zDysku
    $zDyskuWg = @{}
    foreach ($e in $elementyZDysku) { $zDyskuWg[$e.Nazwa] = $e.Wartosc }
    $wiersze = foreach ($p in $plan) {
        [pscustomobject][ordered]@{ Nr = ($plan.IndexOf($p) + 1); Klucz = $p.Klucz; Linia = $p.Linia; Typ = $p.Def.Typ; Przed = $p.Z; Po = $zDyskuWg[$p.Klucz]; Dotyczy = $p.Def.Kogo }
    }
    Write-Host (($wiersze | Format-Table -AutoSize | Out-String -Width 260).TrimEnd())
    Write-Host ('WYNIK: zmieniono 13 wartosci. SHA-256 przed {0}, po {1}; {2} -> {3} bajtow; elementow {4}.' -f $skrotStary, $skrotNowy, $stare.Length, $zDysku.Length, $elementyZDysku.Length)
    if (-not $plikNaDysku) { Write-Host 'UWAGA: nie udalo sie wymusic zrzutu zmienionego pliku na dysk - po najblizszym wyjsciu z gry sprawdz go: zamknij-ujscia-bee.ps1 -NaSucho.' }
    if (-not $Cofnij) {
        Write-Host 'Dalej: uruchom gre; w Modules\BetterEconomy\bee_log.txt ma byc "settings loaded: 583 applied, 0 skipped". Nowa kampania nie jest potrzebna.'
        Write-Host 'Cofniecie: cofnij-ujscia-bee.ps1 (gra zamknieta).'
    }
}
catch {
    $wyj = $_.Exception
    if ($null -ne $wyj -and $null -ne $wyj.Data -and $wyj.Data.Contains('Kod')) {
        $kod = [int]$wyj.Data['Kod']
        Write-Host ('STOP (kod {0}): {1}' -f $kod, $wyj.Message)
    }
    elseif ($etap -ceq 'zapisano') {
        # plik jest juz zmieniony i sprawdzony skrotem; zawiodlo tylko wypisanie tabeli
        $kod = 3
        Write-Host ('UWAGA (kod 3): plik ustawien ZOSTAL zmieniony i sprawdzony skrotem, ale wypisanie tabeli sie nie powiodlo: ' + $wyj.Message)
        Write-Host ('Kopia sprzed zmiany: ' + $kopia + '. Stan pliku pokaze ponowne uruchomienie z -NaSucho.')
    }
    elseif ($etap -ceq 'kopia' -or $etap -ceq 'zapis') {
        $kod = 3
        Write-Host ('STOP (kod 3): nieoczekiwany blad na etapie "' + $etap + '": ' + $wyj.Message)
        Write-Host ('Stan pliku ustawien jest NIEZNANY - uruchom skrypt z -NaSucho, zeby go sprawdzic' + $(if ($null -ne $kopia) { '; kopia: ' + $kopia } else { '' }) + '.')
    }
    else {
        $kod = 1
        Write-Host ('STOP (kod 1): nieoczekiwany blad przed zapisem: ' + $wyj.Message + ' Nic nie zapisano.')
    }
}
finally {
    if ($null -ne $tmp -and (Test-Path -LiteralPath $tmp)) { Remove-Item -LiteralPath $tmp -Force -ErrorAction SilentlyContinue }
}
exit $kod
