# Paczka "bee": BetterEconomy - 13 kluczy zamykajacych ujscia i kurki AI (sam plik ustawien, bez kodu)

Stan na 06.10.2026, po drugim przejsciu autora i drugim niezaleznym przegladzie (pierwsze przejscie: autor + recenzent,
noc 05/06.10 - historia w rozdz. 10). Drugi recenzent skryptow NIE zmienial (skroty jak w rozdz. 4); poprawil ten opis w
trzech miejscach: co zostaje w pozycji "GiveGoldAction w nicosc" (5.2), brakujaca akcja gracza (1.2), uwaga o pliku
`better_economy_user.cfg` (rozdz. 2).
Decyzja Jeffa (docs/STAN-PRAC.md, "Decyzje Jeffa 05.10 - komplet"): "BetterEconomy - zamknac ujscia 13 kluczami (fundament,
rozdz. 4.5); funkcje gracza bez zmian." Krok K2 fundamentu, piaty w kolejce porannych testow (docs/PLAN-NOCNY-2026-10-05.md).

**Werdykt: gotowe do uruchomienia przez Jeffa. Czeka tylko na jego "tak" dla wariantu zbrojowni (rozdz. 1.1).**

**W grze i w repo NIC nie zostalo zmienione.** Prawdziwy plik ustawien byl tylko czytany: SHA-256 przed i po wszystkich probach
ten sam (`1f963cfa000b64bb3deb6ff28885171084bc0a46d509b45b3161599c27546848`, 41 154 bajty, data pliku 04.10 14:49), w katalogu
gry nie powstal zaden plik. Skrypty nie byly uruchamiane na prawdziwym pliku - takze nie "na sucho".

Skroty: "be/" = dekompilacja `scratchpad\ore-supply\be` (BetterEconomy.dll v1.4.5 "Living Economy"); XML =
`Modules\BetterEconomy\ModuleData\better_economy_settings.xml`; BK = BannerKings.Redux; BEE = BetterEconomy.

## 0. W skrocie

- **Gotowe:** `zamknij-ujscia-bee.ps1` (zmienia dokladnie 13 wartosci, z kopia pliku i kontrolami) i `cofnij-ujscia-bee.ps1`
  (cofa te 13 wartosci). Przetestowane na kopiach, wszystko powtorzone dzis na ostatecznej wersji skryptu:
  **227 sprawdzen w 51 probach, 0 niezaliczonych** (113 autora, 37 pierwszego recenzenta, 36 z drugiego przejscia autora,
  41 drugiego recenzenta - w tym replika calego katalogu `ModuleData` gry), plus proba prawdziwego kodu
  wczytujacego BEE poza gra: **"583 applied, 0 skipped"** przed i po zmianie, 13 pol ma wartosci docelowe.
- **Wszystkie 13 kluczy z listy 4.5 sprawdzone w kodzie** - dzialaja tak, jak pisze fundament (tabela w rozdz. 1). Dekompilacja,
  na ktorej stoi fundament, jest identyczna z dekompilacja DLL lezacego dzis w grze (102 pliki bez roznic; DLL md5
  `267ba08de27928ceeb81c401e50f5b9e`).
- **Jedna sprzecznosc w samej decyzji - rozstrzygnieta w skrypcie, do potwierdzenia przez Jeffa.** Klucz nr 3 listy,
  `ArmoryRequiredArtisans = 1000000000`, zamyka zbrojownie miasta takze GRACZOWI (ten sam warunek w obu sciezkach), a decyzja
  mowi "funkcje gracza bez zmian" (fundament BEE-9 wymienia zbrojownie wsrod funkcji gracza). Pozostale 12 kluczy nie zabiera
  graczowi zadnej akcji. Dlatego skrypt **domyslnie** zamyka zbrojownie tylko AI innym kluczem:
  `ArmoryAiCheckCooldownDays = 1000000000` (sciezka AI nigdy nie rusza, akcja gracza "Build / Upgrade Armory" zostaje).
  Lista doslowna z fundamentu jest pod przelacznikiem `-ListaZFundamentu`. **W obu wariantach zmienia sie dokladnie 13 wartosci**
  i te same ujscia AI sa zamkniete.
- **Nowa kampania NIE jest potrzebna.** BEE czyta plik raz, przy starcie gry; klucze sa czytane przy kazdym ticku, wiec dzialaja
  od razu na kazdym zapisie. Na naszych zapisach (12 i 28 dob) ogona wyplat nie ma - rachunek w 5.3; zostaja tylko gotowe obozy AI.
- **Plik nie ma konkurenta:** BEE nie uzywa MCM, w `Dokumenty\...\Configs` nie ma zadnego pliku BEE, zaden inny mod nie zapisuje
  tych pol. BEE niczego nie przycina do zakresu; brak klucza = wartosc wbudowana (rowna dzisiejszej, czyli ujscie otwarte).
- **Co doszlo w drugim przejsciu:** (1) skrypt zapisuje plik i kopie z wymuszonym zrzutem na dysk - dysk C tej maszyny zeruje
  koncowki swiezych zapisow (w samym `bee_log.txt` jest dzis 5 933 bajtow zerowych); (2) 6 nowych prob, m.in. plik z koncowka
  wyzerowana od granicy 4096 B i wyzerowana stara kopia; (3) poprawiony rachunek "ogona" na starym zapisie (5.3);
  (4) wszystkie twierdzenia tego opisu sprawdzone ponownie w kodzie i na plikach (rozdz. 10).

## 1. 13 kluczy: wartosc dzis, docelowa, co robi, kogo dotyczy

"Dzis" = wartosc w prawdziwym pliku (i zarazem wartosc wbudowana w DLL - sprawdzone proba loadera). Linie kodu: be/BetterEconomy.Behaviors,
o ile nie napisano inaczej. Pole = be/BetterEconomy.Config/BetterEconomySettings.cs.

| Nr | Klucz (linia XML; pole, typ) | Dzis | Docelowo | Co robi (plik:linia) | Kogo dotyczy | Czy zabiera funkcje graczowi |
|---|---|---|---|---|---|---|
| 1 | `CastleAiLeaderReserveGold` (XML:621; :1089 int) | 25000 | 1000000000 | Rezerwa kiesy pana przy dwoch wyplatach AI w nicosc: wplata do wirtualnego skarbca zamku `min(kiesa - rezerwa, 12 000, 80 000 - skarbiec)` (CastleEconomyCampaignBehavior.cs:351-358) i oboz szkoleniowy 30/60/100 tys. (:1069-1074). Oba przez `GiveGoldAction(pan, null)`. | Tylko AI (`TickAi` :326 pomija zamki rodu gracza). | NIE. Wplata gracza (:1086-1158), oboz (:547-625) i szkolenie (:626-733) nie czytaja klucza. |
| 2 | `LordInvestmentReserveFlat` (XML:33; :35 int) | 5000 | 1000000000 | Stala czesc rezerwy `ComputeReserve` (LordInvestmentCampaignBehavior.cs:262-287). Od niej zalezy: inwestycja pana we wies 5/15/30 tys. w nicosc (VillageInvestmentCampaignBehavior.cs:317-318, :375) i doplata pana do zbrojowni AI (TownEconomyCampaignBehavior.cs:2130-2137). | Tylko AI (:313 i :2083 pomijaja rod gracza). | NIE. Inwestycja gracza we wies (VillageInvestment :98, :274) nie liczy rezerwy. |
| 3 | domyslnie: `ArmoryAiCheckCooldownDays` (XML:405; :707 int) | 14 | 1000000000 | Przerwa miedzy probami budowy zbrojowni przez AI - pierwszy warunek `TryApplyAiArmory` (TownEconomyCampaignBehavior.cs:2083; stan startowy -999: Core/TownEconomyState.cs:143). Przy 1e9 AI nigdy nie dochodzi do budowy (ani z kiesy pana, ani z wirtualnego skarbca miasta). | Tylko AI (jedyne uzycie klucza). | NIE. |
| 3' | `-ListaZFundamentu`: `ArmoryRequiredArtisans` (XML:403; :703 float) | 220 | 1000000000 | Wymagana liczba rzemieslnikow miasta do zbrojowni: sciezka AI (:2098-2101) ORAZ sciezka gracza (:542-546, komunikat "Not enough artisans. Need 1000000000, have N."). | AI i gracz. | **TAK** - akcja "Build / Upgrade Armory" (menu BEE: SettlementMenuBehavior.cs:922; ekrany BK: MilitaryVM.cs:632, OverviewVM.cs:481) przestaje byc dostepna. |
| 4 | `VillageDiversionRelationThreshold` (XML:133; :203 float) | 30 | -101 | Prog relacji notabli z panem, ponizej ktorego wies "odplywa" handlem (VillageDevelopmentCampaignBehavior.cs:468-475, :489). Relacja ma zakres -100..100 (srednia; 50 gdy brak notabli, :1004), wiec -101 nigdy nie zachodzi. | Swiat, takze wsie gracza (brak warunku wlasciciela). | NIE. Akcja gracza "Negotiate market access" (:230-282) nie czyta progow - zostaje. Wsie gracza przestaja "odplywac" (to bylo na jego niekorzysc). |
| 5 | `VillageDiversionGrievanceThreshold` (XML:134; :205 float) | 50 | 101 | Prog zalu wsi (zakres 0-100, :488): odplyw (:489, :506) i oplata AI 5 000 za "dostep do targu" (:599, :625). Razem z kluczem 4: galaz odplywu (:498-511) jest nieosiagalna, wiec `ApplyDiversionStock` (:514-573 - towar z niczego na targ innego miasta, :551) nie rusza; oplata AI wymaga juz tylko `DiversionFraction >= 0.1`, a ta po zmianie tylko maleje (0.03 na tick). | Swiat; oplata 5 000 tylko AI (:599 pomija rod gracza). | NIE (jak wyzej). |
| 6 | `VillageSecondaryRequiredStableDays` (XML:127; :191 int) | 30 | 1000000000 | Liczba "stabilnych tickow" do odblokowania drugiej produkcji wsi (:668); licznik ma sufit 10 000 (:369), wiec nigdy. Druga produkcja = do 8 szt. na tick z niczego do skladu wsi (:396-456). | Swiat, takze wsie gracza. | NIE (brak akcji gracza; to mechanizm bierny). Kosmetyka: panel wsi pokaze "Locked (N/1000000000 stable days)" (:142). |
| 7 | `CaravanDeliveryMinGold` (XML:446; :783 int) | 1500 | 1000000000 | Minimalna kiesa karawany do "kontraktu" BEE (CaravanCampaignBehavior.cs:650). Kontrakt = towar ze skladu wsi na targ miasta bez zaplaty + 12% marzy z niczego do kiesy karawany (:924-927). Zamyka wszystkie rodzaje dostaw BEE (zwykle, deficytowe, traktatowe, wojskowe, eksport zbrojowni). | Wszystkie karawany, takze gracza. | NIE (brak akcji gracza). Karawany gracza traca bierna marze BEE z niczego i zielone komunikaty "[LivingEconomy] ... delivered ..."; zwykly handel karawan (BK) bez zmian. |
| 8 | `CaravanEscortHireMinGold` (XML:455; :797 int) | 6000 | 1000000000 | Minimalna kiesa do najmu eskorty (:1003; Core/CaravanEscortBalance.cs:10): 95/190 d z kiesy karawany w nicosc, zbrojni z niczego (:1040-1046). | Wszystkie karawany, takze gracza. | NIE (najem byl samoczynny). Juz najeta eskorta zostaje (kod tylko liczy zbrojnych w partii, nikogo nie zwalnia: :424-467). |
| 9 | `CaravanRecruitPromotionEnabled` (XML:469; :821 int) | 1 | 0 | Wlacznik awansu rekrutow karawany za 80 d x tier w nicosc (:1076; :1155-1174). | Wszystkie karawany, takze gracza. | NIE (samoczynne). |
| 10 | `RouteDangerMaxLossRatio` (XML:87; :115 float) | 0.38 | 0 | Mnoznik "straty na szlaku": do 7.6% kiesy karawany dziennie w nicosc, zima x1.25 (:1377-1393). Drugie uzycie: wskaznik `LastTransitLossRatio` wsi (RouteDangerCampaignBehavior.cs:84-85) - czyta go tylko BEE (wirtualne sklady zamku CastleEconomy :215, ekrany, modele BEE nie zarejestrowane w trybie BK). | Wszystkie karawany, takze gracza. | NIE - gracz zyskuje (jego karawany przestaja tracic zloto w nicosc). |
| 11 | `TradeAgreementCustomsMin` (XML:483; :845 int) | 50 | 0 | Podloga cla od dostawy traktatowej (:943-950): `GiveGoldAction(null, krol)` po polowie dla dwoch krolow (TradeAgreementCampaignBehavior.cs:400-422). Stawka jest juz 0 (wpis 49), wiec przy podlodze 0 clo = 0 i `AccrueCustoms` wraca na :405. | Krolowie stron umowy; gracz tylko jako krol. | NIE (brak akcji). Gracz-krol traci 25 d z niczego na dostawe. |
| 12 | `TradeAgreementCorridorProsperityPerDay` (XML:509; :897 float) | 0.15 | 0 | Dobrobyt z niczego dla 4 miast koncowych korytarza poziomu >= 4 (TradeAgreementCampaignBehavior.cs:836-848, :858). | Miasta korytarzy (takze gracza). | NIE (bierne). |
| 13 | `RaidPeasantFlightFraction` (XML:81; :107 float) | 0.08 | 0 | Ucieczka 8% chlopow po najezdzie (VillageSupplyCampaignBehavior.cs:121-124): w trybie BK odejmuje ludnosc BK wsi w nicosc (Compatibility/BannerKingsAdapter.cs:138-160; przy 0 wraca na :140). | Swiat, takze wsie gracza. | NIE - wsie gracza przestaja tracic ludzi w nicosc. |

### 1.1 Zbrojownia: lista z fundamentu a "funkcje gracza bez zmian"

- Fundament sam to zglasza (4.5 "Uboczne", K2 "Ryzyko", pytanie 6 w rozdz. 7: "Zbrojownie gracza zablokuje ten sam klucz co AI -
  czy to akceptujesz?"). Zapisana decyzja odpowiada tylko "funkcje gracza bez zmian", a BEE-9 wymienia jako funkcje gracza:
  wplate do skarbca zamku, oboz, inwestycje we wies i zbrojownie.
- Kod: `ArmoryRequiredArtisans` stoi w `CanPlayerBuildOrUpgradeArmory` (:542) i w `TryApplyAiArmory` (:2098). Rzemieslnicy w
  trybie BK to liczba rzemieslnikow BK miasta (EconomySaveBehavior.cs:86, BannerKingsAdapter.cs:98), wiec dzis zbrojownia gracza
  jest osiagalna - klucz 1e9 by ja odebral.
- `ArmoryAiCheckCooldownDays` ma jedno uzycie: pierwszy warunek sciezki AI (:2083). `today - (-999) < 1 000 000 000` jest zawsze
  prawda (bez przepelnienia; stan sprzed -999 jest wyrownywany do -999, :224-226), wiec AI nie dochodzi ani do doplaty z kiesy
  pana, ani do budowy z samego wirtualnego skarbca miasta (ta druga droga zostalaby otwarta, gdyby polegac tylko na kluczu 2).
- Skutek obu wariantow dla AI jest ten sam. Roznica: w wariancie domyslnym gracz nadal moze zbudowac zbrojownie (100-400 tys. w
  nicosc, potem bron z niczego i wsad z targu bez zaplaty - TownEconomyCampaignBehavior.cs:2152-2266) - dokladnie tak jak dzis.
- **Do potwierdzenia przez Jeffa:** wariant domyslny (zbrojownia gracza zostaje) czy `-ListaZFundamentu` (zbrojownia zamknieta
  wszystkim). Zmiana wariantu pozniej: `cofnij-ujscia-bee.ps1`, potem `zamknij-ujscia-bee.ps1` z przelacznikiem albo bez.

### 1.2 Co po 13 kluczach zostaje otwarte w BEE (swiadomie - "funkcje gracza bez zmian")

Akcje gracza nadal wrzucaja zloto w nicosc: wplata do skarbca zamku (Castle :1149) i miasta (Town :463), oboz (:614), szkolenie
(:718), inwestycja we wies (VillageInvestment :274, daje hearth +10/25/50), dostep do targu 5 000 (VillageDevelopment :625),
zbrojownia (Town :577 - w wariancie domyslnym) oraz - dopisane przez drugiego recenzenta - **inwestycja gracza we wlasne miasto
10 / 50 / 100 tys.** (menu BEE "Invest 10,000 / 50,000 / 100,000": SettlementMenuBehavior.cs:964-1000 -> UI/SettlementActionService.cs:664
-> LordInvestmentCampaignBehavior.cs:140; w zamian dobrobyt, zywnosc i towar z niczego, :157-176). Zaden z 13 kluczy tej akcji
nie dotyka (warunek `CanPlayerInvest`, Town :853, nie liczy rezerwy). Do tego to, co juz istnieje w starym zapisie: gotowe obozy AI daja zalodze
1-3 XP na tick (Castle :486-545), gotowe zbrojownie produkuja dalej (Town :2152-2266 nie pyta o zaden z 13 kluczy; fundament w
sesjach 12-dobowych zadnej zbrojowni AI nie stwierdzil, w zapisach nie sprawdzano).

## 2. Jak BEE czyta plik i co moze go nadpisac

- **Kiedy czyta.** Raz przy starcie gry (`BetterEconomySubModule.OnSubModuleLoad`, be/BetterEconomy/BetterEconomySubModule.cs:35)
  i na zadanie Ctrl+Shift+M w trakcie kampanii (be/BetterEconomy.UI/HotkeyHandler.cs:22, :31-33, :53-58). Sciezka: katalog modulu
  `BetterEconomy` + `ModuleData\better_economy_settings.xml` (Config/SettingsLoader.cs:99-115) - zadnej innej lokalizacji.
- **Jak czyta** (SettingsLoader.cs:25-97): kazdy element-dziecko korzenia -> pole statyczne `BetterEconomySettings` o tej samej
  nazwie; `int.TryParse` / `float.TryParse` z kultura niezmienna (kropka dziesietna). **Nie ma zadnego przycinania do zakresu.**
  Nieznana nazwa albo zla wartosc = ostrzezenie w `bee_log.txt` i pominiecie (licznik "skipped").
- **Brak klucza.** Pole zostaje, jak bylo: przy starcie gry to wartosc wbudowana (dla wszystkich 13 kluczy rowna dzisiejszej,
  czyli ujscie OTWARTE), przy Ctrl+Shift+M - ostatnia wczytana. Dlatego klucze musza zostac w pliku, a po aktualizacji BEE plik
  trzeba sprawdzic ponownie (skrypt `-NaSucho` pokaze stan).
- **Sprawdzone prawdziwym kodem BEE poza gra** (`test\proba-loadera.ps1`, wynik `test\wynik-loadera.txt`, powtorzone 06.10): metoda
  `SettingsLoader.TryAssign` z DLL gry wywolana dla kazdego elementu: plik dzisiejszy "583 applied, 0 skipped"; plik po zmianie
  (oba warianty) "583 applied, 0 skipped", 13 pol ma wartosci docelowe (1000000000 jako int bez przepelnienia, -101 i 101 nieprzyciete);
  3000000000 dla pola int = "bad value", pole bez zmian; "0,5" i tekst = "bad value"; -5 wczytane doslownie; brak klucza przy
  przeladowaniu = wartosc z pamieci.
- **Co moze nadpisac - nic:**
  - MCM: BEE nie ma odwolania do MCM (be/BetterEconomy.csproj - 14 odwolan, bez MCM); w
    `Dokumenty\Mount and Blade II Bannerlord\Configs\ModSettings\Global` nie ma katalogu BetterEconomy (jest
    `ImprovedEconomyForAILords` - pozostalosc po innym, nie zainstalowanym modzie).
  - `Modules\BetterEconomy\better_economy_user.cfg` (dzis: `LordWealthRealism=0`) to 9 osobnych przelacznikow
    (Config/RuntimeSettings.cs:76-105), zaden z 13 kluczy. Uwaga drugiego recenzenta (ryzyko istniejace, nie skutek tej zmiany,
    ale po niej wazniejsze): ten plik ma 21 bajtow i lezy na tym samym dysku C; gdyby zniknal albo sie wyzerowal, przelacznik
    wraca do wartosci wbudowanej WLACZONY (RuntimeSettings.cs:13) i BEE zaczyna zdejmowac panom AI "nadmiarowe" zloto w nicosc
    (WealthAuditCampaignBehavior.cs:76, :147, :197, :346) - a po zamknieciu ujsc panowie beda mieli zlota wiecej. Skrypt tego pliku
    nie rusza i nie sprawdza; przy kontroli po aktualizacji BEE zajrzec takze do niego.
  - Inne mody: skan wszystkich DLL w `Modules` - do `BetterEconomySettings` odwoluje sie tylko BannerKings.dll i zapisuje jedno
    pole, `BannerKingsCompatibilityMode = 0` (bk/BannerKings/Main.cs:331; XML przywraca 1, bo BK laduje sie przed BEE - dzis
    12. i 13. pozycja listy `LauncherData.xml`, czyli 11. i 12. wsrod wlaczonych modulow; fundament pisze "12 i 13" - to ta
    sama kolejnosc). BKROTPatch i Armoury nie czytaja ani nie zapisuja zadnego z 13 kluczy. Zaden mod nie ma wlasnej
    kopii pliku ani latki na `SettingsLoader`; w katalogu warsztatu Steam nie ma drugiego BetterEconomy.
  - W katalogu `ModuleData` leza 3 stare kopie `.bak-2026-10-04-*` - BEE ich nie czyta.

## 3. Skrypty

Katalog paczki: `scratchpad\noc-2\bee`. Oba pliki musza lezec razem (cofanie wola skrypt glowny - lista kluczy jest w jednym miejscu).
W zwyklym oknie PowerShella uruchamianie skryptow jest na tej maszynie zablokowane (zasada domyslna), dlatego wywolanie w tej postaci:

```
powershell -NoProfile -ExecutionPolicy Bypass -File zamknij-ujscia-bee.ps1 -NaSucho     # same kontrole i tabela, bez zapisu
powershell -NoProfile -ExecutionPolicy Bypass -File zamknij-ujscia-bee.ps1              # zmiana (zbrojownia: tylko AI)
powershell -NoProfile -ExecutionPolicy Bypass -File zamknij-ujscia-bee.ps1 -ListaZFundamentu   # lista doslowna z 4.5
powershell -NoProfile -ExecutionPolicy Bypass -File cofnij-ujscia-bee.ps1               # cofniecie 13 wartosci
```

`zamknij-ujscia-bee.ps1` (parametry: `-Path`, `-ListaZFundamentu`, `-Cofnij`, `-NaSucho`, `-ProcesyGry`):
1. gra zamknieta - procesy `Bannerlord*`, `Launcher.Native`, `TaleWorlds.MountAndBlade*` (lapia wszystkie 10 plikow .exe gry i
   BLSE, w tym `Bannerlord.BLSE.LauncherEx`, pod ktorym dziala gra Jeffa - proba A05); gdy ktorys dziala: kod 2, nic nie
   zapisane. Wlasna lista `-ProcesyGry` moze byc jednym napisem z przecinkami; pusta lista = kod 1;
2. plik czytany bajt w bajt; tylko UTF-8 (z BOM albo bez); musi byc poprawnym XML z korzeniem `<BetterEconomySettings>`
   (plik wyzerowany w calosci albo od granicy 4096 B - znana usterka dysku C - staje tutaj);
3. kazdy z 14 znanych kluczy (12 wspolnych + oba klucze zbrojowni) musi wystapic DOKLADNIE RAZ - w tekscie (takze komentarze sie
   licza) i w drzewie XML - z wartoscia dokladnie "przed" albo "po"; inaczej STOP bez zapisu (kod 1) z tabela stanu;
4. podmiana 13 kotwic `<Klucz>stara</Klucz>` -> `<Klucz>nowa</Klucz>` (kazda policzona: 1 wystapienie) i kontrola wyniku w
   pamieci: 13 linii roznych, liczba linii i znakow CR bez zmian, 583 elementy w tej samej kolejnosci, 13 zmienionych wartosci,
   kazda czyta sie regula BEE;
5. kopia `better_economy_settings.xml.bak-<rrrr-mm-dd>-przed-BEE-ujscia` (przy cofaniu `...-przed-cofnieciem-BEE-ujscia`),
   zrzucona na dysk i sprawdzona skrotem; istniejaca kopia z ta sama data i inna trescia (takze wyzerowana) NIE jest
   uzywana ani nadpisywana - nowa dostaje godzine w nazwie;
6. zapis przez plik tymczasowy pisany od razu na dysk (z pominieciem pamieci podrecznej systemu) i podmiane, odczyt z dysku,
   porownanie skrotu; przy niezgodnosci przywrocenie kopii (kod 3). Gdyby nieudana podmiana zostawila katalog BEZ pliku
   ustawien, skrypt od razu odtwarza go z kopii i sprawdza skrotem (bez pliku BEE bierze wartosci wbudowane,
   SettingsLoader.cs:20-24 - przepadlyby takze ustawienia z wpisow 18 i 49);
7. tabela przed / po (kolumna "Po" czytana z dysku).
Kodowanie, BOM i konce linii zostaja (prawdziwy plik: UTF-8 bez BOM, LF, 666 linii, 9 bajtow spoza ASCII w komentarzach).
Drugie uruchomienie: "plik jest juz w stanie docelowym", nic nie zapisuje. Stan mieszany (czesc kluczy zmieniona recznie, inny
wariant zbrojowni): STOP.

`cofnij-ujscia-bee.ps1` (`-Path`, `-NaSucho`, `-ProcesyGry`): cofa TYLKO 13 wartosci do stanu "przed" (wariant zbrojowni
rozpoznaje z pliku), z tymi samymi kontrolami i wlasna kopia. Inne reczne poprawki w pliku zostaja. Wartosci "przed" sa
zapisane w skrypcie, wiec cofanie nie zalezy od kopii (kopia tez moze pasc ofiara dysku). Pelna stara tresc jest dodatkowo
w kopii `.bak-...-przed-BEE-ujscia` i w lustrze `D:\Backup-Bannerlord\Modules\BetterEconomy\ModuleData`.

Kody wyjscia: 0 = zrobione albo nie bylo nic do zrobienia; 1 = STOP przed zapisem; 2 = gra uruchomiona; 3 = blad kopii albo
zapisu (komunikat mowi, w jakim stanie jest plik).

Sprawdzone statycznie (bez uruchamiania): domyslna sciezka w skrypcie istnieje, ma 134 znaki (z najdluzsza kopia 184, granica
259), plik nie jest tylko do odczytu, konto GAME ma pelne prawa do katalogu `ModuleData` (podnoszenie uprawnien niepotrzebne).
Oba skrypty: czyste ASCII, LF, bez bledow skladni (parser PowerShella, proba A05).

## 4. Wynik prob na kopii (06.10, ostateczna wersja skryptu; pelne zapisy w `test\wynik-*.txt`)

Prawdziwy plik jest kopiowany do `test\wzorzec`, kazde wywolanie dostaje jawne `-Path` do katalogu roboczego paczki (strazik w
skryptach prob odmawia innej sciezki). Skrot skryptu, na ktorym szly proby: `zamknij-ujscia-bee.ps1` SHA-256 `6cb86498...c678`
(31 218 bajtow), `cofnij-ujscia-bee.ps1` `92c02145...12b2`.

| Zestaw | Prob | Sprawdzen | Niezaliczonych | Zapis |
|---|---|---|---|---|
| `test\uruchom-testy.ps1` (autor) | 29 | 113 | 0 | `test\wynik-testow.txt` |
| `test\proby-recenzenta.ps1` (recenzent: awarie w srodku zapisu) | 12 | 37 | 0 | `test\wynik-recenzenta.txt` |
| `test\proby-autora-2.ps1` (drugie przejscie) | 6 | 36 | 0 | `test\wynik-autora-2.txt` |
| `test\proby-recenzenta-2.ps1` (drugi recenzent: replika katalogu gry, postaci XML) | 4 (Q03 w 9 odmianach) | 41 | 0 | `test\wynik-recenzenta-2.txt` |
| `test\proba-loadera.ps1` (prawdziwy kod BEE) | 5 wczytan | 11 wnioskow | 0 | `test\wynik-loadera.txt` |

Drugi recenzent (06.10, ok. 02:30) puscil wszystkie cztery wczesniejsze zestawy jeszcze raz na tych samych skryptach: wyniki
identyczne z zapisanymi przez autora (roznia sie tylko godziny i numery procesow); `skoki-bee.py` na trzech CSV daje wynik
identyczny co do znaku z `test\skoki-przed-zmiana.txt`.

| Proba | Co | Wynik |
|---|---|---|
| T01 | `-NaSucho` na czystej kopii | kod 0, plik bez zmian, brak kopii |
| T02 | zamkniecie, wariant domyslny | kod 0; dokladnie 13 linii roznych; 41 154 -> 41 186 bajtow; LF i brak BOM zachowane; kopia `.bak-2026-10-06-przed-BEE-ujscia` = wzorzec; `ArmoryRequiredArtisans` 220 i `BannerKingsCompatibilityMode` 1 nietkniete |
| T03 | drugie uruchomienie | kod 0, "juz w stanie docelowym", nic nie zapisane |
| T04 | cofniecie | kod 0; plik **bajt w bajt jak wzorzec**; kopia sprzed cofniecia = plik zamkniety |
| T05 | drugie cofniecie | kod 0, "nie ma czego cofac" |
| T06 | zamkniecie `-ListaZFundamentu` | kod 0; 13 linii; `ArmoryRequiredArtisans` 1000000000, `ArmoryAiCheckCooldownDays` 14 |
| T07 | zmiana wariantu bez cofniecia | kod 1, plik bez zmian |
| T08 | cofniecie wariantu listy | kod 0, wariant rozpoznany z pliku, plik bajt w bajt jak wzorzec |
| T09-T15 | brak klucza; duplikat; inna wartosc (30000); klucz powtorzony w komentarzu; jeden klucz juz zmieniony; "0.380" zamiast "0.38"; dwa klucze w jednej linii | za kazdym razem kod 1, plik bez zmian, brak kopii |
| T16 | `-Cofnij -ListaZFundamentu` razem | kod 1 |
| T17 | plik z CRLF | CRLF zachowane, 13 linii, cofniecie bajt w bajt |
| T18 | plik z BOM | BOM zachowany (raz), cofniecie bajt w bajt |
| T19-T20 | "gra uruchomiona" (za gre uznany proces powershell) | kod 2, plik bez zmian; z `-NaSucho` kod 0 i ostrzezenie |
| T21-T25 | uciety XML; UTF-16; plik wyzerowany; inny korzen; brak pliku | kod 1, plik bez zmian |
| T26 | plik tylko do odczytu | kod 3, "Plik ustawien nietkniety", plik tymczasowy posprzatany (kopia zrobiona, z ostrzezeniem, ze nie dalo sie jej zrzucic na dysk) |
| T27 | zamknij -> cofnij -> reczna poprawka innego klucza -> zamknij -> cofnij (ten sam dzien) | stara kopia nietknieta, nowa z godzina; reczna poprawka przetrwala oba kierunki |
| T28 | sciezka za dluga na kopie (240 znakow) | kod 1 PRZED jakimkolwiek zapisem |
| T29 | katalog ze spacjami, nawiasami i znakiem `&` (jak prawdziwa sciezka gry) | zamkniecie kod 0, wynik bajt w bajt jak w T02; cofniecie bajt w bajt jak wzorzec |
| R01-R03 | `-ProcesyGry` jednym napisem z przecinkami; pusta lista | kontrola gry dziala (kod 2); pusta lista = kod 1 |
| R04-R05 | plik otwarty przez inny program (bez prawa zapisu; bez prawa odczytu) | kod 3 "nietkniety" / kod 1; po zwolnieniu pliku drugie uruchomienie kod 0 |
| R06 | plik ZNIKA tuz przed podmiana | kod 3, plik odtworzony z kopii bajt w bajt |
| R07 | plik WYZEROWANY zaraz po zapisie, przed kontrola z dysku | kod 3, przywrocona stara tresc |
| R08-R10 | wartosc ze spacjami; oba klucze zbrojowni zmienione; pozostawiony plik tymczasowy tylko do odczytu | kod 1; kod 1 w obu kierunkach; kod 3 i sprzatniecie |
| R11-R12 | sciezka wzgledna; `cofnij` bez skryptu glownego obok | dziala; kod 1 |
| A01 | **koncowka wyzerowana od bajtu 4096 i od 36864, rozmiar bez zmian** (postac usterki dysku C) | zamknij i cofnij: kod 1 "nie jest poprawnym XML", plik bez zmian, brak kopii |
| A02 | **kopia z dzisiejsza data istnieje, ale jest wyzerowana** | nie jest uzyta ani nadpisana; nowa kopia z godzina w nazwie = oryginal |
| A03 | komunikaty o zrzucie na dysk | kopia "zrzucona na dysk", brak ostrzezen |
| A04 | `-NaSucho` na pliku juz zamknietym, `cofnij -NaSucho` | nic nie zapisuja; cofniecie zapowiada skrot rowny wzorcowi |
| A05 | domyslne wzory procesow wobec plikow .exe gry; skladnia obu skryptow | wszystkie 10 .exe zlapane; 0 bledow skladni |
| A06 | **wynik obu wariantow sprawdzony innym narzedziem** (porownanie bajtow linia po linii z wzorcem, bez logiki skryptu) | rozni sie dokladnie 13 linii (33, 81, 87, 127, 133, 134, 405 albo 403, 446, 455, 469, 483, 509, 621), kazda ma oczekiwana tresc; 0 CR, bez BOM, 9 bajtow spoza ASCII jak we wzorcu |
| Q01 | **replika CALEGO katalogu `ModuleData` gry** (10 plikow: 4 XML, 3 stare kopie `.bak-2026-10-04-*`, jezyki): na sucho, zamkniecie, powtorka, cofniecie | w katalogu zmienia sie WYLACZNIE plik ustawien i przybywaja WYLACZNIE dwie kopie skryptu; stare kopie, profile i jezyki bajt w bajt bez zmian; zadnego pliku tymczasowego; po cofnieciu wszystko jak w grze |
| Q02 | to samo dla `-ListaZFundamentu` | jak wyzej; skroty wynikow obu wariantow rowne `77fd1f5a...e9ba` i `3a24554b...d850` |
| Q03a-i | postaci XML, ktore BEE czyta inaczej niz prosty tekst: spacja w znaczniku; **duplikat ze spacjami w znacznikach (BEE wzialby ostatnia wartosc)**; CDATA; encja liczbowa; atrybut; element pusty; klucz zagniezdzony poza korzeniem; zero wiodace; drugi klucz zbrojowni z obca wartoscia | za kazdym razem kod 1, plik bez zmian, brak kopii |
| Q04 | obok prawdziwego klucza element o innej wielkosci liter (BEE go pomija) | kod 0, 13 wartosci, obcy element nietkniety; cofniecie bajt w bajt |
| wynik innym jezykiem | oba pliki wynikowe sprawdzone Pythonem (wlasny parser XML, porownanie bajtow linia po linii) | 583 elementy w tej samej kolejnosci, zmienionych dokladnie 13 i tylko oczekiwane; 13 linii roznych (numery jak w A06); rachunek bajtow +32 / +31 zgodny z dlugoscia wartosci |
| koniec kazdego zestawu | prawdziwy plik gry | skrot przed = skrot po; w katalogu gry nie powstal zaden plik (Q: caly katalog `ModuleData` gry - 10 plikow bez zmian) |

Tabela przed / po z proby T02 (to samo wypisze skrypt na prawdziwym pliku - numery linii sa z prawdziwego pliku):

```
== BetterEconomy, 13 kluczy: ZAMKNIECIE UJSC
Gra zamknieta: tak (nie dziala zaden proces: Bannerlord*, Launcher.Native, TaleWorlds.MountAndBlade*).
Stan pliku: 41154 bajtow, SHA-256 1f963cfa...6848, UTF-8 bez BOM, konce linii LF, elementow 583.
BannerKingsCompatibilityMode = 1 (skrypt go nie zmienia).
Wariant zbrojowni: ArmoryAiCheckCooldownDays (AI).
Kontrole w pamieci: 13 kotwic po 1 wystapieniu, 13 linii roznych, 583 elementow XML (570 bez zmian), kodowanie i konce linii zachowane.
Kopia: ...\better_economy_settings.xml.bak-2026-10-06-przed-BEE-ujscia (nowa, zgodna skrotem), zrzucona na dysk

Nr Klucz                                  Linia Typ   Przed Po         Dotyczy
 1 CastleAiLeaderReserveGold                621 int   25000 1000000000 AI
 2 LordInvestmentReserveFlat                 33 int   5000  1000000000 AI
 3 ArmoryAiCheckCooldownDays                405 int   14    1000000000 AI
 4 VillageDiversionRelationThreshold        133 float 30    -101       swiat (takze wsie gracza)
 5 VillageDiversionGrievanceThreshold       134 float 50    101        swiat (takze wsie gracza)
 6 VillageSecondaryRequiredStableDays       127 int   30    1000000000 swiat (takze wsie gracza)
 7 CaravanDeliveryMinGold                   446 int   1500  1000000000 swiat (takze karawany gracza)
 8 CaravanEscortHireMinGold                 455 int   6000  1000000000 swiat (takze karawany gracza)
 9 CaravanRecruitPromotionEnabled           469 int   1     0          swiat (takze karawany gracza)
10 RouteDangerMaxLossRatio                   87 float 0.38  0          swiat (takze karawany gracza)
11 TradeAgreementCustomsMin                 483 int   50    0          krolowie (gracz tylko jako krol)
12 TradeAgreementCorridorProsperityPerDay   509 float 0.15  0          swiat (miasta korytarzy)
13 RaidPeasantFlightFraction                 81 float 0.08  0          swiat (takze wsie gracza)
WYNIK: zmieniono 13 wartosci. SHA-256 przed 1f963cfa...6848, po 77fd1f5a...e9ba; 41154 -> 41186 bajtow; elementow 583.
```
Cofniecie (T04) wypisuje te sama tabele z kolumnami zamienionymi i konczy "SHA-256 przed 77fd1f5a...e9ba, po 1f963cfa...6848;
41186 -> 41154 bajtow". Wariant `-ListaZFundamentu`: wiersz 3 to `ArmoryRequiredArtisans 403 float 220 -> 1000000000`, skrot po
`3a24554b...d850`, 41 185 bajtow. Pliki wynikowe obu wariantow: `test\po-zamknieciu-wariant-domyslny.xml`,
`test\po-zamknieciu-wariant-lista.xml` (na nich szla proba loadera).

## 5. Czego oczekiwac w logach po zmianie

### 5.1 Punkt odniesienia "przed" (pomiar z trzech ostatnich sesji, `test\skoki-przed-zmiana.txt`; 06.10 odtworzony co do znaku)

`python skoki-bee.py <economy-*.csv>` - dobowa zmiana kiesy glowy rodu AI minus bilans z modelu, zliczana w oknach kwot BEE
(metoda weryfikatora fundamentu; SZACUNEK po kwotach). Kazde okno ma tlo innych wydatkow; miara tla to zdarzenia u rodow,
ktorych dane ujscie dotknac nie moze: dla okien zamkowych rody bez zamku (186-194 z ok. 320), dla wiejskich rody bez wsi (98-106).

| Sesja | Dob | Kiesy glow rodow AI | Okna zamkowe (12k, 42k i zlozenia): rody z zamkiem / tlo | Okna wiejskie (5k, 15k, 30k): rody ze wsia / tlo | Razem nominalnie |
|---|---|---|---|---|---|
| 07:40 | 12 | 50.66 -> 48.79 mln (-156 tys. na dobe) | 100 / 9 | 87 / 1 | 2.89 mln = 241 tys. na dobe (40% spadkow) |
| 11:19 | 12 | 50.85 -> 49.65 mln (-100 tys. na dobe) | 108 / 10 | 92 / 3 | 3.29 mln = 274 tys. na dobe (46%) |
| 15:22 | 28 | 50.22 -> 42.53 mln (-275 tys. na dobe) | 234 / 25 | 211 / 4 | 6.81 mln = 243 tys. na dobe (39%) |

Skoki w oknach wiejskich trafiaja prawie wylacznie w rody ze wsia (211 wobec 4) - to mocny znak, ze sa to inwestycje i oplaty BEE,
a nie przypadkowe wydatki. W sesji 15:22 widac juz fale oplat 5 000 za "dostep do targu" przewidziana przez fundament na
21.-30. dobe: okno 5k ma ok. 4 zdarzenia na dobe w dobach 1-20 i 10, 4, 6, 14, 8, 4, 9, 11 w dobach 21-28.

### 5.2 Po zmianie

**Log BEE** (`Modules\BetterEconomy\bee_log.txt`), zaraz po starcie gry: `settings loaded: 583 applied, 0 skipped
(better_economy_settings.xml)`. Inna liczba albo linia "bad value" / "unknown setting" / "settings load failed" = plik zostal
podmieniony albo uszkodzony. Uwaga 1: ta linia wyglada tak samo przed i po zmianie - mowi tylko, ze plik sie wczytal, nie ze
klucze dzialaja. Uwaga 2: `bee_log.txt` ma dzis 5 933 bajty zerowe w srodku - szukac narzedziem czytajacym pliki binarne
(`grep -a`).

**Ksiega pieniadza Armoury (wpis 102)** - log `Modules\Armoury\Armoury-<data>.log`, od drugiej doby po wczytaniu:
- `Pieniadz swiata (bilans): ... GiveGoldAction w nicosc N` - tu siedza wszystkie "znikajace" wyplaty panow BEE: wplata do
  skarbca zamku (po ok. 12 000), oboz (30 / 60 / 100 tys.), inwestycja we wies (5 / 15 / 30 tys.), dostep do targu (5 000),
  doplata do zbrojowni. Kazda to `GiveGoldAction(pan, null)`, czyli zdarzenie `HeroOrPartyTradedGold` z pusta druga strona
  (gra: GiveGoldAction.cs:39; Armoury\src\MoneyLedger.cs:218-243 -> `_worldToNothing`, pozycja w :504). **Oczekiwane: N jest
  o ok. 240-320 tys. d na dobe NIZSZE niz w tej samej dobie tej samej kampanii bez zmiany** (241-274 tys. wg pomiaru wyzej).
  Ksiega podaje sume dobowa, nie pojedyncze wyplaty - pojedyncze kwoty 12 000 i 30-100 tys. widac w CSV CrashScribe (nizej).
  - **POPRAWKA drugiego recenzenta - N nie spadnie w poblize zera i nie wolno porownywac roznych dob.** Poprzednia wersja opisu
    podawala, ze poza BEE zostaje w tej pozycji "glownie werbunek". To nieprawda: do tej samej pozycji trafia **ujemny dobowy
    bilans kazdego rodu z modelu finansow**. Gra rozlicza rod wywolaniem `GiveGoldAction.ApplyBetweenCharacters(null, glowa, wynik)`
    (ClanVariablesCampaignBehavior.cs:414) takze wtedy, gdy wynik jest ujemny, a ksiega odwraca ujemna kwote na "glowa -> nicosc"
    (MoneyLedger.cs:226, :240-243). Dodatnie bilanse rodow ida tak samo do "GiveGoldAction z niczego". Poza tym w pozycji siedza:
    werbunek (RecruitmentCampaignBehavior.cs:619-630), awanse wojska (PartyUpgraderCampaignBehavior.cs:144-149) i zloto stracone
    w bitwie (MapEventParty.cs:516).
  - Wielkosc tej czesci (SZACUNEK z kolumny `bilans_model` tych samych trzech CSV - suma ujemnych bilansow rodow AI na dobe,
    w tys. d; CrashScribe pyta model bez wyplat, gra rozlicza z wyplatami, wiec kwoty w ksiedze moga sie nieco roznic):

    | Sesja | Srednio na dobe | Doba 1 | Doba 6 | Doba 12 | Dalej |
    |---|---|---|---|---|---|
    | 07:40 (12 dob) | 259 tys. | 35 | 290 | 376 | - |
    | 11:19 (12 dob) | 214 tys. | 31 | 225 | 303 | - |
    | 15:22 (28 dob) | 317 tys. | 33 | 224 | 265 | doby 18-22 ok. 300-310, doby 23-28: 388, 402, 1123, 654, 513, 450 |

    Czyli w 12. dobie nowej kampanii "GiveGoldAction w nicosc" to dzis co najmniej ok. 550-700 tys. (300-380 bilanse rodow +
    240-320 BEE + werbunek i awanse), a po zmianie co najmniej ok. 300-380 tys. Udzial BEE to mniej wiecej polowa pozycji i
    maleje z kazda doba, bo ujemne bilanse rodow rosna razem z wojskiem. Sesja "po" wczytana z pozniejszego zapisu moze pokazac N
    WYZSZE niz sesja "przed" z pierwszych dob - i nie bedzie to znaczylo, ze klucze nie dzialaja.
  - Jak porownywac: (a) ten sam zapis wczytany dwa razy - raz bez zmiany, raz ze zmiana - i te same doby; albo (b) dwie nowe
    kampanie i te same numery dob (sesje 07:40 i 11:19 - dwie nowe kampanie bez zadnej zmiany w BEE - roznia sie w tej czesci
    doba do doby o 4-78 tys., wiec roznice 240-320 tys. widac, ale lepiej brac srednia z kilku dob). Pewniejsze od tej pozycji
    sa: zmiana kies "glowy rodow" (nizej), `skoki-bee.py` i dziennik szczegolowy BEE.
  - Uwaga dla wlasciciela wpisu 102 (poza zakresem tej paczki, nic tu nie zmieniano): skoro bilanse rodow z modelu finansow ida
    przez `GiveGoldAction`, to w linii "Pieniadz swiata (bilans):" sa juz policzone w pozycjach "GiveGoldAction z niczego" /
    "w nicosc", choc opis reszty wymienia je jako "niezmierzone"; zold partii glowy rodu jest wtedy w tej linii dwa razy (raz
    w "zold", raz w bilansie rodu). Do sprawdzenia na pierwszym logu z ksiega.
- `Pieniadz swiata: ... bohaterowie: glowy rodow X (zmiana)` - dobowy ubytek kies glow rodow maleje o te sama kwote: w warunkach
  sesji 15:22 z ok. -275 tys. do ok. -30 tys. na dobe (SZACUNEK: 275 - 243; bogatsi panowie moga wydac wiecej gdzie indziej).
- `... GiveGoldAction z niczego` - znika clo traktatowe (25 d + 25 d na dostawe); kwota niezmierzona, mala.
- `... partie bez wodza: karawany` i `reszta [R]` - BEE przestaje ruszac kiesy karawan (marza +12%, eskorta, awans, strata na
  szlaku ida wprost przez `PartyTradeGold`, bez GiveGoldAction, wiec dzis siedza w "reszcie"). Wielkosci nikt nie zmierzyl -
  bez prognozy; kierunek: mniejsze wahania pozycji "karawany".
- Ksiega wpisu 102 nie byla jeszcze uruchomiona w grze (ostatni log, 15:22, jest sprzed niej), wiec **dzis nie ma linii "przed"**.
  Powstana same: klucze sa piate w kolejce porannych testow (po wozie z nowymi logami, karawanach, zapasie startowym i mineralach),
  wiec wczesniejsze sesje dadza punkt odniesienia. Warunek: nie uruchamiac skryptu przed pierwsza sesja z ksiega - i
  porownywac te same doby tej samej kampanii (patrz poprawka wyzej); najprosciej zachowac zapis, z ktorego ruszy sesja "przed".

**CSV CrashScribe** (`python skoki-bee.py <nowy economy-*.csv>`), kolumna "u wlascicieli":
- okna zamkowe u rodow z zamkiem: z 100-108 na 12 dob (234 na 28 dob) do poziomu tla, czyli ok. 6-7 na 12 dob (tlo 9-10 u ok. 190
  rodow bez zamku, przeliczone na 130 rodow z zamkiem) - SZACUNEK;
- okna wiejskie u rodow ze wsia: z 87-92 na 12 dob (211 na 28 dob) do ok. 2-7 na 12 dob (tlo 1-3 u ok. 100 rodow bez wsi,
  przeliczone na 218 rodow ze wsia) - SZACUNEK; rody z ziemia wydaja wiecej, wiec tlo moze byc u nich wyzsze;
- fala 5 000 od 21. doby nie rusza.

**Linie "Ruda:" / "Drewno:"** (OreLedger): z "bez wyjasnienia" znika udzial BEE - kontrakty karawan przenoszace towar ze wsi na
targ bez zaplaty i odplyw wsi (do ok. 57 szt. wszystkich towarow dziennie przy dlawiku BKROT). Glownym zrodlem drewna "bez
wyjasnienia" pozostaje RealisticBannerlord (fundament B1), wiec duzej zmiany tej liczby nie oczekiwac.

**W grze, bez logow** (potwierdza, ze nowe wartosci sa w pamieci): panel wsi BEE pokazuje "Locked (N/1000000000 stable days)";
w wariancie `-ListaZFundamentu` zbrojownia miasta gracza: "Not enough artisans. Need 1000000000, have N."

**Pewne potwierdzenie u zrodla (opcjonalne, decyzja Jeffa - zmienia plik BEE):** dopisac `VerboseLogging=1` do
`Modules\BetterEconomy\better_economy_user.cfg`; w `bee_verbose_log.txt` nie moze byc linii `castle-ai-contribute:`,
`castle-training-ai-build-start:`, `lord-village-invest:`, `armory-build-ai:`, `village-trade-diversion:`,
`village-secondary-production: unlock`, `caravan-delivery:`, `caravan-escort:`, `caravan-promotion:`, a dzienne podsumowania maja pokazywac
`village-development: ... diverting=0 marketAccess=0` i `caravans: ... 0 hit by danger ..., 0 deliveries ..., 0 escort hires`.

### 5.3 Czy test wymaga nowej kampanii - NIE

- Kolejnosc: zamknac gre -> skrypt -> uruchomic gre -> wczytac dowolny zapis albo zaczac nowa kampanie. Wartosci dzialaja od
  pierwszego ticku, bo kod czyta pola przy kazdym sprawdzeniu, a w zapisie gry ustawien nie ma.
- Ctrl+Shift+M (przeladowanie pliku w trakcie gry, o ktore pyta fundament) dziala, ale nie jest tu potrzebny: skrypt i tak
  wymaga zamknietej gry, a przy starcie gra czyta plik sama. Skrot przydaje sie tylko przy recznej edycji w trakcie sesji;
  wymaga otwartej kampanii i zamknietej ksiegi BEE.
- Dlugosc testu: co najmniej 10-12 dob gry - BKROTPatch przepuszcza tick osady BEE raz na 10 dni, wiec dopiero po 10 dobach kazdy
  zamek mial swoja kolej.
- **Ogon na starym zapisie** (nie blad zmiany - skutki tego, co BEE zrobil wczesniej):
  - oplata 5 000 za wies. Po zmianie ulamek odplywu wsi juz tylko maleje (0.03 na tick, :491), a AI placi tylko, gdy po tym
    odjeciu jest >= 0.1 (:599) - czyli najwyzej RAZ za wies, ktora w chwili zmiany ma ulamek >= 0.13 (zaplata zeruje go, :630).
    Rachunek z kodu (:507-508; przy dlawiku 1 tick wsi na 10 dob): ulamek rosnie 0.0525 -> 0.0866 -> 0.1088 (3. tick, doba
    21-30: pan placi i ulamek wraca do 0) -> 0.1232 -> 0.1326 (5. tick, doba 41-50 - tylko gdy pan wczesniej nie mial
    17 000). **Na zapisach do ok. 40. doby (nasze maja 12 i 28) ogona nie ma**; na starszych - tylko we wsiach panow, ktorzy
    przez co najmniej dwa ticki nie mieli czym zaplacic. Wygasa samo w 2 tickach wsi, w najgorszym razie (ulamek 0.35 przy
    zalu ponad 50) w 8. Gdyby dlawik BKROT byl wylaczony (da sie go przelaczyc z konsoli - fundament 4.4), te same progi
    wypadaja w dobach 3 i 5;
  - obozy w budowie zostana ukonczone, gotowe obozy AI nadal daja zalodze 1-3 XP na tick; zaplacone zloto nie wraca;
  - wsie z juz odblokowana druga produkcja nadal ja maja (przy dlawiku pierwsza moglaby sie odblokowac po ok. 300 dobach - w
    naszych zapisach nie ma zadnej); juz najete eskorty karawan zostaja.
- Nowa kampania daje obraz czysty i jest i tak potrzebna dla K3 (zapas startowy) i "sesji bazowej" - fundament kaze zamknac ujscia
  PRZED sesja bazowa. Dodatkowa korzysc na nowej kampanii: znika bezwarunkowa strata 130 x 12 000 = 1.56 mln w pierwszych 10 dobach.

## 6. Kontrola wg zasady 0

- **Zachowanie cudzego kodu (dekompilacja).** Wszystkie uzycia 14 kluczy przeczytane: 27 linii w 9 plikach be/ (lista w
  tabeli 1; poza nimi klucze wystepuja tylko jako deklaracje pol). Wszystkie to porownania, odejmowania albo mnozenia - zadnego
  dzielenia przez klucz, zadnej petli zaleznej od klucza. Przepelnienia int: `kiesa - 1e9` i
  `1e9 + wojsko x 15` mieszcza sie w int; suma `CaravanEscortHireMinGold + CaravanDeliveryMinGold + 1000` = 2 000 001 000 <
  2 147 483 647 (Caravan :1088 - i tak nieosiagalne przy wylaczonym awansie). Nie podnosic kluczy karawan ponad 1e9.
- **Pelnosc.** Wszystkie miejsca, w ktorych czynne zachowania BEE zmieniaja prawdziwy stan gry (zloto, towar, ludzie, dobrobyt,
  XP), wypisane przeszukaniem calego be/ (GiveGoldAction, AddToCounts, PartyTradeGold, Prosperity, Hearth, FoodStocks,
  AddXpToTroop): kazda sciezka AI ma swoj klucz z listy albo jest juz zamknieta wpisami 18 i 49; zostaja tylko akcje gracza (1.2).
- **Regresje.** Wirtualne stany BEE (skarbiec zamku i miasta, gotowosc, patrole, zal wsi, wskazniki szlaku) nie maja sciezki do
  prawdziwego stanu poza wyliczonymi wyzej - zubozenie wirtualnych skarbcow AI nie oslabi garnizonow ani dobrobytu. Sprawdzone
  06.10 ponownie: poza zachowaniami zamku i miasta gotowosc, patrole i skarbce czytaja tylko ekrany, wywiad osad BEE i
  nieczynna w trybie BK warstwa feudalna; 5 latek Harmony BEE (trening, werbunek, wymiana, najazd, warsztat) nie czyta zadnego
  z 13 kluczy poza najazdem (klucz 13). `RouteDangerMaxLossRatio = 0` ustawia wsiom wskaznik przeplywu 1.0 - czytaja go tylko
  wirtualne sklady zamku i ekrany BEE (modele BEE nie sa zarejestrowane w trybie BK; BK i BKROTPatch go nie czytaja).
- **Kolizje z innymi modami i naszymi latkami.** BK, BKROTPatch i Armoury nie czytaja zadnego z 13 kluczy (grep po
  dekompilacjach i `Armoury\src`). Armoury siega do BEE tylko po zegar por roku (WesterosClimate.cs:319-344, WinterSource.cs:47).
  Pozostale paczki nocy (paser, zold, karawany): w ich zrodlach BEE wystepuje tylko w tych samych, niezmienionych plikach
  Armoury (klimat, komentarze) - zadna nie dotyka ustawien BEE. Karawanom zmiana pomaga: BEE przestaje przerzucac surowce ze
  wsi na targ obok prawdziwych partii.
- **Zamknieta ekonomia.** Zamykane po stronie AI i swiata: zloto w nicosc (wplata do skarbca zamku, oboz, inwestycja we wies,
  dostep do targu, doplata do zbrojowni; z kies karawan: eskorta, awans, strata na szlaku), zloto z niczego (marza kontraktu,
  clo), towar z niczego albo bez zaplaty (odplyw wsi, druga produkcja, kontrakt karawany, zbrojownia AI), dobrobyt z niczego
  (korytarz), ludzie w nicosc (ucieczka 8%) i z niczego (eskorta). Nie powstaje zaden nowy kurek ani ujscie.
- **Skutek systemowy do obserwacji (zamierzony):** rody AI przestaja tracic 240-320 tys. d dziennie. Wiecej zlota w kiesach to
  wiecej zakupow sprzetu (AiGear), budow (BuildFunding - drewno, narzedzia), werbunku i mniejszy popyt na Bank Zelazny. Fundament
  (BEE-4): strojenie wyplacalnosci lordow, Banku i pocztu pokojowego ma sens dopiero po tej zmianie.

## 7. Ryzyka i czego nie sprawdzono

- **W grze nic nie bylo uruchamiane.** Skutki w kampanii (liczby z rozdz. 5) to wniosek z kodu i z CSV trzech sesji.
- **Wariant zbrojowni** - domyslny odbiega od listy 4.5 jednym kluczem (uzasadnienie 1.1); czeka na "tak" Jeffa.
- **Aktualizacja BEE albo "sprawdz spojnosc plikow" w Steam** przywroci plik - ujscia otworza sie bez ostrzezenia. Po kazdej
  aktualizacji: `zamknij-ujscia-bee.ps1 -NaSucho` (pokaze stan), potem zwykle uruchomienie. Jesli nowa wersja zmieni zapis
  ktorejs wartosci (np. "0.380"), skrypt stanie z tabela - wtedy poprawic recznie albo dopisac nowa wartosc "przed" w skrypcie.
  Numery linii kodu w tym opisie dotycza wersji v1.4.5 (md5 `267ba08d...`); po aktualizacji DLL klucze trzeba sprawdzic w kodzie od nowa.
- **Tryb zgodnosci BK.** Gdyby kolejnosc ladowania sie odwrocila (BEE przed BK - fundament BEE-8), BEE zarejestruje swoje
  zachowanie inwestycji w miasta; przy rezerwie 1e9 panowie AI nigdy nie zainwestuja, a "zaniedbanie" zacznie zdejmowac miastom AI
  do 4.5 dobrobytu i 1.2 zywnosci dziennie (LordInvestmentCampaignBehavior.cs:192-219; XML:45-50). Dzis nieczynne (zachowanie nie
  jest rejestrowane, BetterEconomySubModule.cs:94-97). Kolejnosc BK (11) przed BEE (12) pilnowac tak jak dotad.
- **Gracz traci bierne dodatki BEE:** marze kontraktow swoich karawan, samoczynna eskorte i awans ich ludzi, druga produkcje
  swoich wsi, clo i dobrobyt korytarza jako krol; umowa handlowa gracza-krola da sie zawrzec, ale korytarz nie urosnie (dostawy
  traktatowe to kontrakty karawan). Zyskuje: koniec strat karawan na szlaku, koniec ucieczki 8% chlopow, koniec odplywu wsi.
- **Karawany:** ile kontraktow, eskort i strat BEE robi dziennie - niezmierzone (fundament: "nieznane"), wiec zmiana pozycji
  "karawany" w ksiedze jest bez prognozy.
- **Dysk C zeruje pliki** (znana usterka): skrypt pisze plik i kopie z wymuszonym zrzutem na dysk, sprawdza zapis skrotem i
  zostawia kopie obok; pozniejszego wyzerowania nie wykryje - pokaze je `-NaSucho` ("nie jest poprawnym XML") albo
  `bee_log.txt` ("settings load failed" - wtedy BEE gra na wartosciach wbudowanych, czyli z ujsciami OTWARTYMI i bez ustawien z
  wpisow 18 i 49). Po zmianie warto od razu odswiezyc lustro na D: (`tools\backup-bannerlord.ps1`). Wymuszony zrzut nie daje sie
  sprawdzic proba (wymagalby odciecia zasilania) - sprawdzone jest tylko, ze sciezka zapisu dziala i niczego nie psuje.
- Nie sprawdzono: jak launcher traktuje wpis BK "BetterEconomy LoadBeforeThis" (ryzyko z fundamentu, bez zmian).

## 8. Jak wprowadzic (dla Jeffa albo dla tego, kto wgrywa)

1. Najpierw co najmniej jedna sesja z ksiega wpisu 102 BEZ tej zmiany (da linie "przed" - 5.2); zachowac zapis, z ktorego
   ta sesja ruszyla - sesja "po" ma isc z tego samego zapisu i przez te same doby (inaczej pozycji "GiveGoldAction w nicosc"
   nie da sie porownac). Gra i launcher zamkniete.
2. `powershell -NoProfile -ExecutionPolicy Bypass -File "<katalog paczki>\zamknij-ujscia-bee.ps1" -NaSucho` - ma wypisac tabele
   13 wierszy i "zmieniloby sie 13 wartosci".
3. To samo bez `-NaSucho` (albo z `-ListaZFundamentu`, jesli Jeff wybierze liste doslowna). Kod 0, tabela przed / po, obok pliku
   kopia `.bak-<data>-przed-BEE-ujscia`.
4. Uruchomic gre, w `bee_log.txt` sprawdzic "583 applied, 0 skipped"; grac 10-12 dob; `skoki-bee.py` na nowym CSV (to jest
   glowny sprawdzian) oraz linie "Pieniadz swiata:" (zmiana kies "glowy rodow") i "Pieniadz swiata (bilans):" w logu Armoury.
5. Wpis do CHANGELOG: `CHANGELOG-wpis.md` (numer i Status do uzupelnienia). Skrypty warto przeniesc do repo (np. `tools\bee\`) -
   przydadza sie po kazdej aktualizacji BEE. Lustro na D: - `tools\backup-bannerlord.ps1`.
Cofniecie: gra zamknieta, `cofnij-ujscia-bee.ps1`.

## 9. Pliki paczki

- `zamknij-ujscia-bee.ps1`, `cofnij-ujscia-bee.ps1` - skrypty (czyste ASCII, LF).
- `skoki-bee.py` - pomiar skokow kies w CSV CrashScribe (tylko odczyt).
- `OPIS.md`, `CHANGELOG-wpis.md`.
- `test\uruchom-testy.ps1` + `test\wynik-testow.txt` - proby skryptow na kopiach (29 prob, 113 sprawdzen).
- `test\proby-recenzenta.ps1` + `test\wynik-recenzenta.txt` - awarie w srodku zapisu i sposob uruchomienia (12 prob, 37 sprawdzen).
- `test\proby-autora-2.ps1` + `test\wynik-autora-2.txt` - drugie przejscie (6 prob, 36 sprawdzen).
- `test\proby-recenzenta-2.ps1` + `test\wynik-recenzenta-2.txt` - drugi recenzent: replika calego katalogu `ModuleData` gry
  i postaci XML czytane przez BEE inaczej niz tekst (4 proby, 41 sprawdzen).
- `test\proba-loadera.ps1` + `test\wynik-loadera.txt` - prawdziwy kod wczytujacy BEE na pliku przed i po.
- `test\wzorzec\better_economy_settings.xml` - kopia prawdziwego pliku (stan "przed", SHA-256 `1f963cfa...6848`).
- `test\po-zamknieciu-wariant-domyslny.xml`, `test\po-zamknieciu-wariant-lista.xml` - wynik skryptu w obu wariantach.
- `test\skoki-przed-zmiana.txt` - punkt odniesienia z sesji 07:40, 11:19 i 15:22.
- `test\poprzednie-2026-10-05\` - skrypt, opis i wyniki z pierwszego przejscia (do porownania).
- Katalogi robocze prob (`t\`, `r\`, `a\`, `x\`), kopia dekompilacji pierwszego recenzenta (`rec\`) i zapisy konsoli drugiego
  recenzenta (`rec2\`, w tym `rec2\przed\` - wyniki autora sprzed ponownego uruchomienia zestawow) - do skasowania po wdrozeniu.

## 10. Historia przegladow

**Pierwsze przejscie (05.10, autor + niezalezny recenzent).** Recenzent potwierdzil tabele kluczy na wlasnej dekompilacji DLL
z gry (`rec\`, identyczna z ta, na ktorej stoi fundament) i wprowadzil dwie poprawki do skryptu: (1) `-ProcesyGry` podane przez
`-File` jako "a,b" bylo jednym wzorem i kontrola gry byla po cichu wylaczona - teraz napis jest rozbijany, pusta lista = STOP;
(2) gdy nieudana podmiana zostawia katalog bez pliku ustawien, skrypt od razu odtwarza go z kopii. Wczesniej sam autor
poprawil usterke znaleziona w probach: za dluga sciezka jest wykrywana przed jakimkolwiek zapisem (skrypt stawal dopiero na
kopiowaniu). Werdykt recenzenta: poprawione i gotowe. Proba R13 (PowerShell 7) pominieta - na tej maszynie nie ma `pwsh.exe`.

**Drugie przejscie (06.10, autor).** Sprawdzone od nowa, niezaleznie od poprzedniego opisu:
- kazdy z 14 kluczy w dekompilacji `ore-supply\be` (wszystkie 27 uzyc, sciezki gracza, przepelnienia, zakres relacji i zalu,
  stan startowy -999) - tabela 1 bez poprawek;
- jak BEE czyta plik (SettingsLoader, RuntimeSettings, HotkeyHandler), brak MCM, brak plikow w `Dokumenty\...\Configs`, skan DLL
  wszystkich modow (do ustawien BEE siega tylko BannerKings.dll), kolejnosc ladowania 11 / 12, brak BEE w warsztacie Steam;
- stan prawdziwego pliku (skrot, kodowanie, LF, 583 elementy, 9 bajtow spoza ASCII), prawa do katalogu, dlugosc sciezki,
  identycznosc dekompilacji i md5 DLL, pomiar `skoki-bee.py` (wynik identyczny co do znaku);
- sciezka ksiegi pieniadza: `GiveGoldAction` -> zdarzenie -> `MoneyLedger.OnGoldTraded` -> "GiveGoldAction w nicosc".
Zmiany: zapis z wymuszonym zrzutem na dysk (plik tymczasowy, kopia, plik po podmianie i po kazdym przywroceniu); 6 nowych prob
(A01-A06); poprawiony rachunek ogona oplat 5 000 (poprzedni opis podawal "zapisy starsze niz ok. 21 dob" - z kodu wychodzi
ok. 40 dob i tylko wsie niewyplacalnych panow); dopisane, ze linia "583 applied" niczego nie dowodzi o samych kluczach, i jak
je zobaczyc w grze; brakujacy dotad rozdzial 10. Po zmianie skryptu wszystkie trzy zestawy prob i proba loadera puszczone od
nowa na ostatecznej wersji.

**Drugi niezalezny przeglad (06.10, ok. 02:30 - proba obalenia wg zasady 0).** Werdykt: poprawione i gotowe. Skrypty bez zmian.
- *Sprawdzone od nowa w zrodlach:* wszystkie 27 uzyc 14 kluczy w `ore-supply\be` (tabela 1 - bez poprawek), `SettingsLoader`,
  `HotkeyHandler`, `RuntimeSettings`, wszystkie 17 wywolan `GiveGoldAction` w BEE i wszystkie miejsca zmiany prawdziwego stanu
  (towar, XP, hearth, dobrobyt, relacje); md5 DLL w grze = `267ba08d...`, dekompilacja 102 pliki bez roznic; skan 357 plikow
  DLL / EXE wszystkich modow (nazwy 14 kluczy ma tylko BetterEconomy.dll, do `BetterEconomySettings` siega tylko
  BannerKings.dll); `Dokumenty\...\Configs` bez pliku BEE; kolejnosc BK 12 / BEE 13 na liscie (11 / 12 wsrod wlaczonych);
  drugiej instalacji gry ani BEE w warsztacie Steam nie ma; prawa do katalogu, zasada uruchamiania skryptow (Restricted),
  10 plikow .exe gry wobec wzorow procesow; 5 933 bajty zerowe w `bee_log.txt` (jeden ciag od bajtu 233 472 = 57 x 4096).
- *Proby:* cztery zestawy autora i pierwszego recenzenta powtorzone - wyniki identyczne; `skoki-bee.py` - identyczne; 41 nowych
  sprawdzen (Q01-Q04) i kontrola obu plikow wynikowych Pythonem. Skryptu nie udalo sie zmusic ani do zapisu zlej tresci, ani
  do ruszenia innego pliku w katalogu.
- *Bledy znalezione i poprawione w opisie:* (1) 5.2 - w pozycji "GiveGoldAction w nicosc" poza BEE NIE zostaje "glownie
  werbunek": trafiaja tam tez ujemne dobowe bilanse rodow z modelu finansow, srednio 214-317 tys. d na dobe i rosnace z
  kazda doba, wiec pozycja nie spadnie w poblize zera, a porownywac wolno tylko te same doby tej samej kampanii; (2) 1.2 - na
  liscie akcji gracza, ktore zostaja, brakowalo inwestycji w miasto 10 / 50 / 100 tys.; (3) rozdz. 2 - dopisane, ze utrata
  pliku `better_economy_user.cfg` wlacza "LordWealthRealism" (zdejmowanie zlota panom). Te same poprawki w `CHANGELOG-wpis.md`.
- *Nie podwazone:* wybor `ArmoryAiCheckCooldownDays` jako domyslnego klucza zbrojowni (dla AI skutek ten sam co klucz z listy,
  gracz zachowuje akcje - zgodnie z "funkcje gracza bez zmian"; nadal do potwierdzenia przez Jeffa), rachunek ogona oplat
  5 000 (ulamki 0.0525 / 0.0866 / 0.1088 / 0.1232 / 0.1326 przeliczone), przepelnienia int, brak nowej kampanii.
