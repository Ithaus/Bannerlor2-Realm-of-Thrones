# ROZPOZNANIE: KOMU KROL DAJE ZDOBYTY ZAMEK (14.10)

Rozpoznanie do sprawy Jeffa z 14.10: "jeszcze do poprawy jest logika przyznawania podbitych zamkow - teraz w kolko dostaje
jedna, max dwie postacie; jak gralem Polnoca, ciagle dostawal Tormund - mial chyba z 6 miast i x zamkow".
Tylko kod i pomiar - bez zmian w kodzie, bez commitow, bez projektu naprawy.
Znaczniki: [K] kod (plik:linia), [IL] kod maszynowy DLL, [P] pomiar (plik), [U] ustawienie, [L] lore / historia, [S] szacunek.

## 0. W skrocie (prostym jezykiem)

1. Kto dostaje zdobyty zamek, rozstrzyga glosowanie w krolestwie dzien po zdobyciu (u AI od razu, u gracza po 2 dobach) - i na liscie do glosowania sa zawsze tylko 3 rody.
2. Te 3 rody mialy byc wybrane za zaslugi (prawo do tytulu, kto zdobyl, ile juz ma ziemi). Przez blad w BKROTPatch kazdy rod dostaje 0 punktow, wiec gra bierze po prostu 3 pierwsze rody z listy krolestwa.
3. Ta lista ma stala kolejnosc wpisow z plikow ROT, nie zaslugi. Polnoc w biegu fin30: Bolton, Karstark, Stark - Bolton dostal kazda zdobycz Polnocy (Krolewska Przystan, Rosby, Duskendale, Lonely Light; Harrenhal tez, ale ten moze isc z fabuly ROT).
4. W Twojej kampanii Polnoca Boltonow i Karstarkow w krolestwie juz nie bylo, wiec pierwsza trojka to: Twoj rod (Karda), Tormund (Giantsbane), Bran Stark. Stad Tormund z 3 miastami, 3 zamkami i 15 wsiami, a reszta Polnocy po 1-3 twierdze.
5. Tak jest w kazdym krolestwie: Dorzecze - 10 z 10 zdobyczy dla Freyow, Reach - 11 dla Ashford i Footly, Krolewska Przystan - 7 dla Lannisterow. W dwoch biegach 99 na 99 zdobyczy trafilo do rodu z pierwszej trojki listy.
6. Kto z trojki wygrywa, decyduja glosy lordow (sympatie, stronnictwa BK); trojka sie nie zmienia, wiec wygrywa w kolko ten sam. Liczba posiadanych lenn prawie nie przeszkadza - jedyny hamulec zmniejsza tylko glos rodu na samego siebie.
7. Gracz-wasal widzi glosowanie "Owner of X" z tymi samymi 3 nazwiskami i moze poprzec jedno; gracz-krol wybiera, ale tez tylko z tych 3; zamek zdobyty wlasna armia gracz moze od razu wziac (Diplomacy).
8. W lore i w historii krol dzieli zdobycze miedzy wielu (zdobywca, wierni bez ziemi, wlasna domena w kluczowych miejscach), a nie daje wszystkiego jednemu - nasz stan jest z tym sprzeczny, a przyczyna to blad cudzego moda, nie czyjs zamysl.

## 1. Zrodla i umowy

| Co | Gdzie |
|---|---|
| tabele fin30 (T30) | `D:\Backup-Bannerlord\logs\2026-10-14-noc\tabele-06-01-15\` - `ludzie-regiony.csv` (wlasciciel kazdej twierdzy co dobe), `budzet-rodow.csv` (rody co dobe, kolejnosc wierszy = `Clan.All`) |
| tabele fin21 (T21) | `D:\Backup-Bannerlord\logs\2026-10-14-noc\Logs\2026-10-13_21-14-13\` (te same pliki) |
| kampania Jeffa Polnoca (J) | `Documents\...\CrashScribe\economy-2026-10-04_05-47-36.csv` (gracz Ithauser, rod Karda, The North; doby 183389-183391); naglowek zapisu `Game Saves\IronmanPEh4X2ByGArW.sav` (Ithauser, d1237, lista modulow) |
| kod gry 1.4.8 (G) | `...\ae222cb7...\scratchpad\dec32\cs_full` |
| kod BK (BK) | `...\ae222cb7...\scratchpad\e4tools\bkfull` |
| kod BKROTPatch (BR) | dekompilacja `...\ae222cb7...\scratchpad\audyt-zloto\BKROTPatch`; IL z zainstalowanego `Modules\BKROTPatch\bin\Win64_Shipping_Client\BKROTPatch.dll` (ilspycmd -il) |
| kod ROT (R) | `...\ae222cb7...\scratchpad\audyt-zloto\ROT` (ROT.dll z 16.08, ROT 8.1.8) |
| kod Diplomacy (D) | `...\ae222cb7...\scratchpad\audyt-zloto\Diplomacy` (1.4.7) |
| tytuly BK-ROT (U) | `Modules\BKROTPatch\ModuleData\titles_6_3.xml` |
| ustawienia Diplomacy (U) | `Configs\ModSettings\Global\Diplomacy\DiplomacySettings_v1.2.json` |
| nasz kod | drzewo `...\scratchpad\zb\ln` (w-toku/lenna od 7bc5dd0b) - zadna nasza latka nie dotyka decyzji o lennie (grep `SettlementClaimant`, `KingdomElection`, `CalculateHeroFiefScore`: tylko pokoj w `CrashScribe\src\PovertyPeace.cs`) |

- Doba wzgledna: d = dzien gry - 108835 (jak w `docs\oblezenia\ROZPOZNANIE-SILY.md`; Harrenhal d128-129, Krolewska Przystan d530).
- "Zdobycie" w pomiarze = twierdza zmienia krolestwo i rod; "rod po decyzji" = pierwszy inny rod tego samego krolestwa do 10 dob pozniej (zwykle nastepnego dnia). Skrypty i wyniki: rozdz. 9.
- Szukane w DLL wszystkich modulow w `Modules` (`SettlementClaimant`, `CalculateMeritOfOutcome`, `CalculateHeroFiefScore`, `ApplyByKingDecision`, `IsOwnerUnassigned`, `LastCapturedBy`, `GrantFief`, `KingdomElection`, `DetermineSupport`): decyzje o lennie ruszaja tylko gra, BK, BKROTPatch (model oceny), ROT (Krolewska Przystan) i Diplomacy (prawo pierwszenstwa gracza). AIInfluence ma akcje `GrantFief`, ale jest wylaczony [U] `Configs\LauncherData.xml` (`AIInfluence` IsSelected=false; byl wlaczony w zapisie Ithauser - naglowek zapisu).

## 2. Droga: od szturmu do nowego pana

1. **Szturm wygrany** [K] G `KingdomManager.cs:239-258`: nowym wlascicielem zostaje **krol** zdobywcy (`newOwner = kingdom.Leader`), a `LastCapturedBy` = rod dowodcy armii [K] G `ChangeOwnerOfSettlementAction.cs:111-118`. Zmiana "przez oblezenie" otwiera roszczenia (:60) i gra stawia `IsOwnerUnassigned = true`, gdy krolestwo ma wiecej niz 1 rod [K] G `SettlementClaimantCampaignBehavior.cs:39-42`.
2. **Nastepna doba** [K] G `SettlementClaimantCampaignBehavior.cs:17-27`: krolestwo dostaje decyzje "Owner of X" (`SettlementClaimantDecision`), wnioskodawca = rod krolewski, bez kosztu wplywu; naraz najwyzej jedna taka decyzja na krolestwo.
3. **Kiedy rozstrzyga** [K] G `Kingdom.cs:696-714`: krolestwo AI - glosowanie od razu (`StartElection`); krolestwo gracza - decyzja czeka na liscie co najmniej 48 h [K] G `KingdomDecision.cs:98-117`. Po 48 h, gdy gracz nie jest krolem, gra rozstrzyga bez niego (`StartElectionWithoutPlayer`) [K] G `KingdomDecisionProposalBehavior.cs:167-192`; gdy gracz jest krolem, musi wybrac sam (`NeedsPlayerResolution`).
4. **Kandydaci** [K] BK `BannerKings.Patches.Government\FiefOwnerPatches.cs:18-50` (prefiks zamiast gry): rody krolestwa bez najemnikow, wymarlych i z zywym przywodca, i tylko te z pelnym parostwem (`Peerage.CanHaveFief`); pelne parostwo = przy zakladaniu rady rod mial jakiekolwiek lenno albo tytul wyzszy niz lordship [K] BK `BannerKings.Managers.Court\Peerage.cs:69-84`. Rody bez lenna (gentry BK) odpadaja.
5. **Zwezenie do 3** [K] G `KingdomElection.cs:65-83` -> `KingdomDecision.cs:190-198`: kazdy kandydat dostaje ocene (`CalculateMeritOfOutcome`), lista jest sortowana malejaco i gra bierze **3 pierwsze**. Tylko o tych trzech sie glosuje.
6. **Glos** [K] G `KingdomDecision.cs:201-210` (glosuja wszystkie rody krolestwa poza najemnikami), `:274-350` (kazdy rod popiera tego z trojki, ktorego ocenia najwyzej; sile poparcia 1/2/3 punkty kupuje wplywem 20/60/100 [K] G `SettlementClaimantDecision.cs:290-306`, `KingdomElection.cs:393-410`).
7. **Wybor** [K] G `KingdomElection.cs:343-370`: wygrywa trojkowicz z najwiekszym poparciem; krol moze przeforsowac swojego, gdy woli go bardzo mocno i ma wplyw (> 300 + koszt przeforsowania) - wtedy z losowaniem. Nadanie: `ApplyByKingDecision` [K] G `SettlementClaimantDecision.cs:285-288`.

Drogi poboczne (nie przez glosowanie):
- ROT: Czerwona Twierdza idzie za Krolewska Przystania do tego samego pana [K] R `ROT.CampaignBehaviors\ROTOthersCampaignBehavior.cs:308-315`; Harrenhal po fabule "Roose Bolton Besieges Harrenhal" wprost do Roose'a [K] R `ROT.Events\HarrenhalEvent.cs:68-71`; smierc Renly'ego, Krwawe Gody, inwazje - wprost `ApplyByDefault` (R `ROT.Events\*.cs`).
- Diplomacy: "prawo pierwszenstwa" - gdy twierdze zdobyla armia gracza, okno "Settlement Captured: claim this fief?" - Accept = lenno gracza, Decline = glosowanie [K] D `Diplomacy.CampaignBehaviors\KeepFiefAfterSiegeBehavior.cs:22-41`, [U] `EnableFiefFirstRight: true`. "Grant Fief" - tylko gracz-krol oddaje **swoje** lenna [K] D `Diplomacy.Actions\GrantFiefAction.cs:26-40`. `EnableFiefRepatriation: true` - zwrot zdobyczy przy pokoju miedzy krolestwami, nie podzial miedzy wasali.
- BK: prawo petycji o lenno z kontraktu (cel gracza `PetitionRightGoal`) [K] BK `BannerKings.Managers.Titles.Governments\DefaultContractAspects.cs:82-117`; oddawanie nadmiaru ziemi - rozdz. 6.

### 2.1 Co widzi gracz

- **Gracz-wasal** (Jeff na Polnocy): w zakladce krolestwa decyzja "Vote for the new owner of X", 3 nazwiska, mozna poprzec jedno za 20/60/100 wplywu. Podpowiedz BK "Claim Strength" pokazuje u kazdego **0** - liczy ja ten sam zepsuty model [K] BK `BannerKings.UI\UIHelper.cs:626-635`. Po 48 h bez glosu gracza gra rozstrzyga sama.
- **Gracz-krol**: po 48 h okno "As king you must decide who will own X" - wybor tylko z tych samych 3. "Wstrzymaj sie" = wybor AI [K] G `KingdomElection.cs:457-460`. Zamek zdobyty wlasna armia gracz moze wziac od razu (Diplomacy, wyzej).
- Rod gracza jest pierwszy w `Clan.All` [P] T30 `budzet-rodow.csv` (wiersz 1 `player_faction`), J (wiersz 1 Karda), wiec po wczytaniu zapisu stoi na liscie krolestwa na poczatku i - jesli ma pelne parostwo (lenno albo tytul) - zawsze jest w trojce. W kampanii Jeffa trojka = Jeff, Tormund, Bran (5.3).

## 3. Wzor oceny kandydata - co mial liczyc, co liczy

### 3.1 Gra (nieczynny - BK go zastepuje)
[K] G `SettlementClaimantDecision.cs:170-229`: (tier rodu x 30 + sila rodu / 10 + 30 gdy rod nie ma twierdzy + 30 dla zdobywcy + 60 dla krola + 30 dla gracza + do 30 dla biednego) / (wartosc twierdz, ktore rod juz ma + wartosc tej) x czynnik odleglosci od wlasnych twierdz. Czyli: im wiecej masz, tym mniej dostajesz; bez ziemi i blisko - wiecej.

### 3.2 BK (zamiar - tez nieczynny, patrz 3.4)
[K] BK `BannerKings.Models.Vanilla\BKDiplomacyModel.cs:493-561`, `CalculateHeroFiefScore`:
- +150 obecny wlasciciel (:499) - po szturmie to krol; +400 tytul de jure (:506), +250 roszczenie (:510);
- prawo podboju krolestwa (z rodzaju wladzy w tytulach BK-ROT [U] `titles_6_3.xml`, przypisanie [K] BK `DefaultContractAspects.cs:44-77`, `BannerKings.Managers.Titles\FeudalContract.cs:90-112`):
  - Feudal (Polnoc, Reach, Dorzecze, Krolewska Przystan, Dolina, Dorne, Smocza Skala, Stormlands, Nocna Straz, Sarnor) = "Conquest by Claim": +1000 de jure, +500 roszczenie (:520-533);
  - Tribal (Wolni Ludzie, Zelazne Wyspy, Dothrakowie, Skagos, Wyspy Letnie) = "Conquest by Might": +1000 dla rodu, ktory zdobyl (:520);
  - Imperial / Republic (Wolne Miasta, Targaryen Dany, Valyria, Yi Ti) = "Distributed": -150 za kazde miasto, -75 za zamek, -30 za wies, ktore rod juz ma (:543-551);
- zawsze: 500 x (1 - zajeta domena / limit domeny) (:559) - rod ponad limitem dostaje minus. Domena: miasto 2, zamek 1, wies 0.5 (x0.75 gdy tytul de jure glowy rodu, 0 gdy innego czlonka rodu); limit 2 + tytul (hrabstwo 1 ... cesarstwo 4) + zarzadzanie/100 (do 3), najwyzej 10 [K] BK `BannerKings.Models.BKModels\BKStabilityModel.cs:241-280`, `:302-343`.

### 3.3 ROT
Krolewska Przystan: ocena rodu krolewskiego x 999 [K] R `ROT.HarmonyPatches.Core\KingdomDecisionPatches.cs:21-34` (postfiks; dziala po prefiksie BK). Przy ocenie 0 daje 0 - zamiar "stolica dla krola" tez nie dziala: w fin30 Krolewska Przystan poszla do Boltona, nie do Starka [P] T30.

### 3.4 BKROTPatch - przyczyna: ocena zawsze 0
- BKROTPatch podmienia model dyplomacji BK na swoj [K] BR `BKROTPatch\BKROTPatch.cs:187` (`BannerKingsConfig.Instance.DiplomacyModel = new BKROTDiplomacyModel()`; BK go pozniej nie nadpisuje - jedyne przypisanie w BK to wartosc domyslna `BannerKingsConfig.cs:257`).
- `BKROTDiplomacyModel.CalculateHeroFiefScore` [K] BR `BKROTPatch.Models\BKROTDiplomacyModel.cs:15-43`: wewnatrz wola **sam siebie**, nie `base` - [IL] `IL_0022-0026: ldarg.0 ... callvirt DiplomacyModel::CalculateHeroFiefScore` (wywolanie wirtualne na `this` = znowu ta sama metoda). Drugie wejscie zatrzymuje straznik rekursji i zwraca `new ExplainedNumber(0)` (`IL_0006-001f`). Wynik zewnetrzny = 0 (x1.2 za blogoslawienstwo Nieznajomego = nadal 0). Kod BK z 3.2 nie wykonuje sie nigdy.
- Prefiks BK bierze ten wynik jako ocene kazdego kandydata [K] BK `FiefOwnerPatches.cs:52-60` -> wszyscy maja 0.
- Sortowanie przy rownych ocenach zachowuje kolejnosc (LINQ `OrderByDescending` jest stabilne) [K] G `KingdomDecision.cs:402-405` -> **3 pierwsze rody w kolejnosci listy krolestwa** (`kingdom.Clans`, po niej idzie prefiks BK).
- Kolejnosc `kingdom.Clans`: przy wczytaniu kazdy rod dopisuje sie do krolestwa po kolei [K] G `Clan.cs:748-806` (`PreAfterLoad` -> `AddClanInternal`), rod, ktory dolacza w trakcie gry, idzie na koniec [K] G `Kingdom.cs:817-822`. W danych kolejnosc zgadza sie z `Clan.All` = kolejnosc wpisow rodow ROT: `clan_empire_*`, `clan_sturgia_*`, `clan_aserai_*`, `clan_vlandia_*`, `clan_battania_*`, `clan_nord_*`, `ROTclan_*`, dwory [P] T30 `budzet-rodow.csv` (wiersze w kolejnosci `Clan.All` [K] `zb\ln\Armoury\src\ClanIncomeBook.cs:310`). Dlatego Bolton (`clan_empire_south_3`) i Karstark (`clan_sturgia_6`) stoja przed Starkiem (`clan_battania_1`), a Tormund (`clan_sturgia_7`) tez przed Starkiem.
- Ten sam wzor bledu (wywolanie siebie + straznik zwracajacy 0) ma jeszcze 10 metod BKROTPatch - rozdz. 8.

## 4. Glosowanie w trojce - dlaczego wygrywa w kolko ten sam

Poparcie rodu V dla kandydata C (gra [K] G `SettlementClaimantDecision.cs:250-275` przy ocenie 0, plus postfiks BK [K] BK `FiefOwnerPatches.cs:70-111`):
- **glos na siebie**: 0.2 x wartosc twierdzy dla rodu x przelicznik zloto->wplyw x (0.1-0.7 wg cechy Calculating) x 2; gdy rod jest ponad limitem domeny - x limit/zajeta (najmniej x0.15);
- **glos na innego**: 2 x relacja V z C + 200, gdy V i C sa w tym samym stronnictwie BK (+ maly czlon gry: relacja x 0.2 x Calculating); kandydat z rodu dworzan BK ("Courtiers of X") - -100 000.
- Hamulec "ma za duzo" (x0.15-1) dziala tylko na glos rodu na siebie i jest liczony **przed** dodaniem relacji i stronnictwa - glosy innych rodow nie zmniejszaja sie wcale.

Wniosek: trojka jest stala (pkt 3.4), wiec co glosowanie startuja ci sami trzej, z tymi samymi relacjami i stronnictwami - wygrywa ten sam. Ktory z trojki i dlaczego dokladnie on - **nie mierzone** (relacji, stronnictw i wplywu w chwili glosu nie logujemy). Fakt [P] T30: w 4 krolestwach, gdzie rod krolewski jest w trojce obok innych (Polnoc, Dorzecze, Reach, Krolewska Przystan), z 20 zdobyczy, ktore zmienily rod po zdobyciu (19, jesli Harrenhal poszedl fabula), krol nie dostal zadnej.

## 5. Pomiar

### 5.1 fin30 (728 dob) [P] T30 -> `robocze\fin30.txt`, `robocze\ranga30.txt`
65 zdobyc; 5 z d536 to fabula (smierc Renly'ego). Z pozostalych 60 **wszystkie** trafily do rodu z pierwszej trojki listy krolestwa (Blackhaven d646 dla Seawortha: 4. w `Clan.All`, ale Tarth dolaczyl do Smoczej Skaly w trakcie biegu, wiec stoi na koncu listy - Seaworth jest 3.). 29 z nich zmienilo rod w glosowaniu (pozycja zwyciezcy: 1. - 9 razy, 2. - 9, 3. - 11).

| Krolestwo | Trojka (kolejnosc listy) | Zdobyczy | Komu | Rodow z twierdza na koniec |
|---|---|---|---|---|
| The Reach | Tyrell, Footly, Ashford | 11 | Ashford 7, Footly 4 | 16 |
| Riverlands | Tully, Frey, Tully-Blackfish | 10 | Frey 10 | 9 |
| KP (Baratheon, Joffrey) | Lannister, Baratheon-Joffrey, Clegane | 7 | Lannister 7 | 13 |
| The North | Bolton, Karstark, Stark | 6 | Bolton 5 (w tym Harrenhal - moze z fabuly ROT), Stark 1 (Czerwona Twierdza za KP) | 20 |
| Stormlands | Baratheon-Renly, Buckler, Tarth | 5 | Renly 4, Tarth 1 | - |
| Volantis | Maegyr, Paenymion, Vhassar | 3 | Vhassar 3 | 6 |
| House Targaryen | Second Sons, Targaryen, Stormcrows | 4 | Second Sons 2, Targaryen 1, Stormcrows 1 | 6 |

Bolton (Polnoc), twierdze przed -> po kazdym nadaniu: Harrenhal d129 1->2, Krolewska Przystan d530 2->3, Rosby d541 3->4, Duskendale d560 4->5, Lonely Light d612 5->6 (5 miast). Na koniec Polnoc: Bolton 6, Stark 3, Manderly 2, pozostale 17 rodow po 1. Frey 1 -> 9 twierdz, Ashford 1 -> 7, Footly 1 -> 5.

### 5.2 fin21 (729 dob) [P] T21 -> `robocze\fin21.txt`, `robocze\ranga21.txt`
46 zdobyc, 7 z d536 (fabula); z 39 pozostalych **wszystkie** w pierwszej trojce (Clegane = 3. w KP); 13 zmienilo rod w glosowaniu (pozycja 1. - 4, 2. - 7, 3. - 2). Reach: Footly 4, Tyrell 2; KP: Clegane 3, Lannister 3; Dorzecze: Frey 2, Blackfish 1; Polnoc: tylko Harrenhal (Bolton).

### 5.3 Kampania Jeffa Polnoca [P] J -> `robocze\kampania-jeffa-polnoc-183389.csv`
Rody Polnocy w kolejnosci `Clan.All` (dzien 183389): **Karda (gracz)** - 0 miast / 1 zamek, **Giantsbane (Tormund, "Baron of Moat Cailin")** - 3 miasta / 3 zamki / 15 wsi, **Stark (Bran, krol)** - 1 / 0, potem Reed 1 / 2, Glover 1 / 0, Manderly 1 / 1, Mormont 1 / 0, pozostali po 1 twierdzy. Bolton i Karstark - poza Polnoca. Pierwsza trojka = gracz, Tormund, Bran - zgodnie z tym, co Jeff zapamietal. W zapisie tej kampanii byl wlaczony AIInfluence (czy cos nadal - nie sprawdzalem).

### 5.4 Nadania wewnatrz krolestwa
W obu biegach przez ok. 2 lata **zadna** twierdza nie zmienila wlasciciela w obrebie krolestwa poza zdobyciem (brak darow, aneksji, oddawania nadmiaru) [P] `robocze\fin30.txt`, `fin21.txt` (rozdzial "Zmiany wlasciciela w obrebie krolestwa" pusty). Wsi nie mierzylem.

## 6. Co gra robi, gdy jeden rod ma za duzo

| Hamulec | Gdzie | Czy dziala |
|---|---|---|
| ocena gry dzielona przez wartosc posiadanych twierdz; +30 dla rodu bez twierdzy | [K] G `SettlementClaimantDecision.cs:174-228` | nie - zastapiona przez BK |
| BK: 500 x (1 - domena/limit), ponizej zera ponad limitem; prawo "Distributed" -150/-75/-30 za posiadane | [K] BK `BKDiplomacyModel.cs:543-559` | nie - ocena = 0 (3.4) |
| BK: glos rodu na siebie x limit/domena (min. 0.15) | [K] BK `FiefOwnerPatches.cs:87-92` | tak, ale tylko glos na siebie (rozdz. 4) |
| BK: stabilnosc lenn rodu ponad limitem -0.18 za kazdy punkt ponad | [K] BK `BKStabilityModel.cs:176-181` | tak - kara dla miast, nie blokada nadan |
| BK: lord AI ponad limitem oddaje co tydzien do 2 posiadlosci, najpierw najlzejsze (wsie 0.5, potem zamki 1, miasta 2; nigdy siedziby dworu), rodowi z wolnym limitem; z tytulem de jure glowy - przez nadanie tytulu, miasto/zamek bez tytulu - darem, wies bez tytulu - wcale | [K] BK `BannerKings.Behaviours\BKTitleBehavior.cs:38-45`, `:93-167`, `:170-226` | w danych 0 twierdz oddanych w 2 biegach (5.4) |
| gra: aneksja (wniosek rodu z wplywem >= 300 i relacja < -25 z wlascicielem) | [K] G `KingdomDecisionProposalBehavior.cs:402-455` | 0 zmian w danych (5.4) |
| ROT, Diplomacy | - | brak |

## 7. Lore i historia - jak krol dzieli zdobycze

Lore Westeros [L] (ASOIAF / Fire & Blood, z pamieci - do sprawdzenia, jesli bedzie na tym opierany projekt):
- Aegon Zdobywca: dorzecze dal pierwszemu lordowi, ktory sie do niego przylaczyl (Edmyn Tully), Harrenhal - swojemu dowodcy (Quenton Qoherys), Storm's End - Orysowi Baratheonowi, ktory je zdobyl; lordowie, ktorzy uklekli, zatrzymali ziemie (Torrhen Stark). Jedna duza nagroda na jednego zasluzonego.
- Robert po Rebelii: Smocza Skala dla Stannisa (ktory ja zdobyl), Storm's End dla Renly'ego; Griffin's Roost po wygnanym Jonie Conningtonie - dla kuzyna z tego samego rodu.
- Tywin po Krwawych Godach: Polnoc dla Boltona (Namiestnik), Riverrun dla Emmona Freya, Harrenhal dla Littlefingera (wczesniej Janos Slynt) - kazdy sojusznik dostaje swoja nagrode, nie jeden wszystko. Stolica i Smocza Skala zostaja przy koronie.

Historia [L]:
- Wilhelm Zdobywca po 1066: okolo jednej piatej ziemi zostaje przy krolu, reszta dla okolo 190 baronow i Kosciola; najwieksi baronowie dostaja duzo, ale rozrzucone po wielu hrabstwach, zeby nikt nie mial zwartej krainy (wyjatek: marchie graniczne).
- Aragonia, Jakub I (Majorka 1229-1232, Walencja 1238) i Kastylia (Sewilla 1253, "repartimiento"): zdobycz dzielona miedzy krola, magnatow, Kosciol, zakony i miasta mniej wiecej wedlug tego, ile kto wystawil wojska i pieniedzy.
- Filip August po 1204: Normandia do domeny krola, zamki pod krolewskimi kasztelanami. Edward I w Walii (1282-1283): nowe zamki krolewskie z konstablami korony, a pogranicza (Denbigh, Ruthin, Bromfield i Yale) dla dowodcow, ktorzy prowadzili wojne.

Wspolny wzor: (1) zwyciezca / glowny dowodca dostaje nagrode, (2) wierni bez ziemi i mlodsze rody tez, (3) kluczowe miejsca (stolica, twierdze graniczne) zostaja przy koronie, (4) unika sie skupiania wielu twierdz w jednych rekach. Stan gry (jedna z trzech stalych rodzin bierze wszystko, krol nic) jest z tym sprzeczny.

## 8. Czego nie wiemy / sprawy poboczne

1. Ktory z trojki wygrywa i dlaczego (relacje, stronnictwa BK, wplyw) - nie logujemy; do zmierzenia trzeba by zapisywac glos kazdego rodu.
2. Czy decyzje bywaly anulowane i ponawiane (anulowanie, gdy rod krolewski jest w trojce i "stoi z boku" [K] G `KingdomDecision.cs:232-272`) - nie mierzone; przypadki "krol zatrzymal" w `robocze\ranga30.txt` moga byc wygranym glosem albo petla anulowan.
3. AIInfluence w kampanii Ithauser - czy przyznawal lenna (`ExecuteGrantFiefSingle`), nie sprawdzalem; dzis wylaczony.
4. **Poboczne, ten sam blad BKROTPatch**: metody, ktore wolaja same siebie i przy drugim wejsciu zwracaja 0 [K] BR `BKROTPatch.Models\*.cs`, lista `robocze\bkrot-wywolania-siebie.txt`, [IL] sprawdzone dla 5: `BKROTArmyModel.CalculatePartyInfluenceCost`, `BKROTClanFinanceModel.CalculateOwnerIncomeFromCaravan`, `BKROTConstructionModel.CalculateDailyConstructionPowerWithoutBoost`, `BKROTInfluenceModel.CalculateInfluenceCap`, `BKROTInfluenceModel.CalculateInfluenceChange`; z dekompilacji takze `BKROTCultureModel.CalculateAcceptanceGain` / `CalculateCultureWeight`, `BKROTLegitimacyModel.CalculateKingdomLegitimacy`, `BKROTLoyaltyModel.CalculateLoyaltyChange`, `BKROTMarriageModel.GetSpouseScore`. Czy i gdzie gra je naprawde wola (czesc siedzi tylko w `BannerKingsConfig`, nie w modelach gry) i co to zmienia - nie sprawdzalem; osobne rozpoznanie. Metody, ktore przy drugim wejsciu wolaja `base` (np. smierc, stabilnosc, dobrobyt), licza poprawnie.
5. To rozpoznanie zamyka punkt "Dlaczego lenna Polnocy zawsze dostaje Bolton - nie liczone" z `docs\oblezenia\ROZPOZNANIE-SILY.md` rozdz. 9 pkt 2.

## 9. Pliki robocze (`docs\lenna\robocze\`)

| Plik | Co |
|---|---|
| `lenna.py` | zdobycia, kto dostal, twierdze rodow przed/po, koncentracja (`python -I lenna.py <ludzie-regiony.csv> <etykieta>`) |
| `ranga.py` | pozycja zwyciezcy na liscie krolestwa w kolejnosci `Clan.All` (`python -I ranga.py <budzet-rodow.csv> <ludzie-regiony.csv> <etykieta>`) |
| `guard.py` | wyszukanie w dekompilacji BKROTPatch metod wolajacych same siebie pod straznikiem |
| `fin30.txt`, `fin21.txt`, `ranga30.txt`, `ranga21.txt` | wyniki dla obu biegow |
| `kampania-jeffa-polnoc-183389.csv` | wycinek `economy-2026-10-04_05-47-36.csv` (rody Polnocy, kampania Ithauser) |
| `BKROTDiplomacyModel.CalculateHeroFiefScore.il` | IL metody z zainstalowanego BKROTPatch.dll (dowod 3.4) |
| `bkrot-wywolania-siebie.txt` | lista z `guard.py` |
