# ETAP 7 - ROZPOZNANIE C: kod pozycji 7.8-7.12 (dane lore wiosek, dymek i menu okregu, plonaca wioska, rabunek osada po osadzie, Bliznaki)

12.10, tylko odczyt (kod, dekompilacja, logi, dokumenty). Nic nie zmienione w kodzie, nic nie zbudowane, nic nie commitowane, gra nie uruchamiana.

Oznaczenia:
- KOD = najnowsze drzewo `SCRATCH(ae222cb7)\e5\wsie\Armoury\src` (galaz w-toku/e5-wsie 396c033 = WGRANIE 33 + e5-wsie);
- GRA = dekompilacja 1.4.8 `SCRATCH(ae222cb7)\dec32\cs_full`; BK = `SCRATCH(ae222cb7)\e4tools\bkfull`;
- WL = dane i szkice lore `D:\Backup-Bannerlord\wioski-lore-2026-10-08` (kopia; oryginal w Temp sesji 3cf3e0ac `dzien-6\wioski-lore` - Temp moze zniknac);
- LOG120 = `Modules\Armoury\Armoury-2026-10-12_02-44-09.log` (120 dob, WGRANIE 33);
- projekty: WL-P = `docs/PROJEKT-WIOSKI-LORE-2026-10-08.md`, WM = `docs/PROJEKT-WIOSKI-NA-MAPIE-2026-10-07.md`, RB = `docs/PROJEKT-RABUNEK-OSADA-PO-OSADZIE-2026-10-07.md`,
  TECH = `docs/dane/wioski-lore/TECHNIKA.md`, A08 / A10 = `docs/audyt-2026-10-09/08-drogi-wioski-widok.md` / `10-pomysly.md`, E411 = `docs/etap4/PLAN-KODU-E411.md`.
- [K] sprawdzone w kodzie dzis, [L] w logu dzis, [D] z dokumentu (nie sprawdzane ponownie), [AUTOTEST] do sprawdzenia w grze.

---

## 0. W skrocie

| # | Co dzis jest | Co zostaje do zrobienia | Wielk. | Ryzyko | Koszt na dobe |
|---|---|---|---|---|---|
| 7.8 | dane B (zajecie, wiara, historia) gotowe 2 446 / 2 446 na ukladzie w grze; A (rycerze) na starym ukladzie (687 wspolnych uid); nic w ModuleData | L0 offline (Python): A od nowa na v4, 13 poprawek P1-P13, jeden plik `arm_map_village_lore.tsv` z bramka ukladu | M (Python) | zero dla gry | 0 (raz na sesje wczytanie ok. 1 MB) |
| 7.9 | linia pana na zywo (T7) [K]; sekcja okregu w oknie "People of this land" [K]; teksty "Look over the district" sa, nieuzywane | dymek z herbem (wlasny prefab), Alt (zajecie, wiara, historia), rycerz; menu okregu z herbami; (proporczyk - patrz pytanie J2); napisy nad spalonymi (decyzja L) | S-M | prefab dymka [AUTOTEST]; proporczyki = klatki | 0 (tylko przy najechaniu / kliknieciu); proporczyki - klatki, nie doba |
| 7.10 | ogien i dym na wiosce [K]; teksty komunikatow sa, NIEUZYWANE [K]; w grze/ROT/BK zadnego komunikatu o rabunku wsi gracza [K] | komunikat (nasluch `VillageStateChanged`), pamiec wojny (kto, kiedy, ilu), wyglad spalonej i mniejszej wioski | S | wyglad = wizerunki mapy (CLAUDE.md 7) | 0 (zdarzenia ok. 3 / dobe na swiat) |
| 7.11 | E4/5 liczy ludzi z osobodni, ale rabunek, postep, zloto i towar dalej gry [K]; lup z niczego 79 103 zl / 120 dob [L] | przejecie fazy lupienia (krok = m osad), okienko gracza po kazdej osadzie, AI jeden krok, czujnosc, lup z prawdziwych zasobow, jedzenie ze spichlerza, kara relacji | D | zakleszczenie menu / CTD (K1, K2 RB), tempo wojny | maly (zastepuje petle lupu gry) |
| 7.12 | tylko "wejscia do osady" Bliznaki w Measure169c: 503 w 120 dobach [L]; petla `CrossingLaw.AiTick` martwa dla AI [K] | licznik przejsc przez most (strona rzeki), linia dobowa z kiesami i "myto gdyby bylo" | M -> S | zero (sam log) | < 0.1 ms / dobe |

Najwazniejsze odkrycia (nowe wobec projektow 07-09.10):
1. **[K][L] Wioska prawie nigdy nie zostaje "spalona".** Ksiega wiosek (E4/11) ma prog 50% osad; przy tempie E4/5 LOG120 ma przez 120 dob
   0 spalonych (max plonie 10, czesciowo puste 70, 97.6 pustych osad na swiat). Wszystko, co w 7.9/7.10 zalezy od stanu "spalona"
   (napisy nad spalonymi - decyzja L, wyglad spalonej, komunikat "has been burned"), bez 7.11 bedzie w grze prawie niewidoczne.
   Komunikat 7.10 musi wiec isc od zdarzenia gry (poczatek / koniec rabunku), nie od progu ksiegi.
2. **[K] Uklad wiosek w grze = v4 z badania lore, wiersz w wiersz** (rozni sie tylko linia "# scene_xml_crc"), wiec dane B v4-proba
   pasuja w 100%. Bramka `layout_crc32` z projektu (crc calego pliku) by je odrzucila - trzeba crc ukladu takiego jak w ksiedze wiosek
   (uid:osady), w grze dzis `5A8C2735` [L].
3. **[K] `CrossingLaw.AiTick` (zawracanie wrogich lordow przy przeprawach) jest wolany tylko, gdy GRACZ stoi we wrogiej strefie przeprawy**
   (CrossingLaw.cs:73-76 `return` przed :100 `AiTick(s)`); LOG120 - 0 linii "CrossingLaw". To blad -> etap 8 (nie naprawiac przy okazji;
   naprawa obudzi martwy kod = CLAUDE.md 8 pkt 4). Licznik 7.12 NIE moze sie podpinac pod ta petle (A10 T2 tak zakladal).
4. **[K] Menu okregu juz jest** (E4/11) jako sekcja okna "People of this land" we wsi i we wsi spladrowanej. Drugie menu "Look over the
   district" z tymi samymi wierszami = dwa miejsca na jedno zjawisko -> pytanie J3.
5. **[K] RB (07.10) pisany przed E4/5, E4/9, E4/11 i spichlerzem** - polowa jego czesci juz jest w kodzie (jency z rabunku, spalony
   spichlerz, ksiega osad, lancuch palenia, sesja rabunku); zostaje przejecie lupienia, okienko, czujnosc, lup i kara relacji.

---

## 1. Stan wyjsciowy w kodzie (co juz jest i gdzie)

| Element | Gdzie [K] | Uwagi |
|---|---|---|
| Widok wiosek (W2): kopie siatek, ogien / dym, dymek | `MapVillagesView.cs` (4 664 linie); dymek `Visual.OnHover` :4604-4635 | `List<TooltipProperty>`: nazwa, linia pana / okregu, ludzie, stan z ksiegi, "X is putting it to the torch" |
| Linia pana na zywo (T7) | `HeldLine` / `HeldLineSafe` :4438-4520, `HouseName`, `LandsText`; MCM `VillageTipHeldBy` | pamiec tekstu do zmiany pana (O(1)); "the Night's Watch", "the khalasar of X", Qarth bez "House" |
| Ogien na wlasciwej wiosce | `PollRaids` :1874-1920 + `VillageBook.FxUid` :991 | 4 razy na sekunde, przelaczenie tylko przy zmianie stanu |
| Ksiega wiosek (E4/11) | `VillageBook.cs` (1 556 linii): n osad (`DistrictSettlementPeople` 250), puste osady `Br`/`Bf`, stany, sesja rabunku `Sess`, lancuch `Burn` :648 | zapis `arm_district` (ArmouryBehavior.cs:461); `StateOf(uid)` :938 dla widoku |
| Spustoszenie (E4/5) | `Devastation.cs`: prefiks/finalizer `RaidEventComponent.Update` :330-391, `OnBeforeFinalize` :405-440, `Strike` :233 | gra dalej liczy postep, zloto i towar od "hearth wirtualnego" |
| Menu okregu (sekcja) | `PeopleMenu.cs:29-32` ("People of this land" w village / town / castle / village_looted), `VillageBook.MenuSection` :1392 | okno `ShowInquiry`, bez przelaczania menu |
| Teksty | `VillageTexts.cs` (89 linii, plik generatora XML nie ma w repo - dopisywane recznie) | NIEUZYWANE: `VilLog*` (7 komunikatow), `DistOption`, `DistTitle`, `Raid*` (9 tekstow okienka rabunku) |
| Przeprawy | `CrossingLaw.cs` (ROT_town3 Bliznaki, castle_B1 Fosa Cailin, castle_EN2 Krwawa Brama; promien 3) | patrz odkrycie 3 |
| Wejscia do Bliznakow | `Measure169c.cs:891-929` (nasluch `SettlementEntered`), linia "(e) Bliznaki" | 503 wejsc / 120 dob, glownie karawany [L] |
| Wlasne okno Gauntlet | `CraftPopup.cs:216-217` (`GauntletLayer` + film gry) | precedens dla warstwy napisow; Armoury nie ma katalogu `GUI` [K: `Modules\Armoury` bez GUI] |
| UIExtenderEx | tylko ForgeView (`SubModule.xml` zalezy od UIExtenderEx) | Armoury go NIE potrzebuje (patrz 3.4) - i nie powinno |

---

## 2. Pozycja 7.8 - dane lore 2 446 wiosek (13 poprawek)

### 2.1 Skad sa dane [K]
- Uklad wiosek: `Modules\Armoury\ModuleData\arm_map_villages.tsv` (v4, 2 446 wiosek, 600 209 B, crc32 pliku 89612ca7, crc ukladu ksiegi
  5A8C2735 - ten sam w LOG120). Kopia w KOD `Armoury\ModuleData\` identyczna.
- Lore powstalo 08.10 w trzech badaniach (WL-P rozdz. 13): A rody i rycerze (`skrypty\generator.py`, `build_rody.py`, `rody.csv` 508 rodow
  z AWOIAF), B zajecie / wiara / historia (`b-skrypty\b_*.py`, `wiara.csv`, `zajecia.csv`, `historia.csv`), C technika i szkice kodu
  (`szkice\*.cs` 1 144 linie, prefab `szkice\GUI\Prefabs\ArmMapVillageTooltip.xml`, proba kompilacji). Kopia CSV i raportow A/B/C w repo
  `docs/dane/wioski-lore/` (bez skryptow i bez plikow wynikowych).
- Pokrycie na ukladzie w grze [K, policzone dzis]:
  - B `v4-proba\arm_map_village_lore-B.tsv`: 2 446 / 2 446 uid wspolnych. Uklad v4 z badania i plik w grze roznia sie TYLKO linia
    "# scene_xml_crc: 146201570" (2 452 pozostale linie identyczne) - dane B sa wlasciwe; zly jest tylko ich naglowek `layout_crc32: 2594a95d`
    (crc calego pliku, ktory po dopisaniu linii ma 89612ca7) - A08 3.2 to samo.
  - A `podglad_rycerze.tsv`: liczony na starym ukladzie 2 449 - wspolnych uid 687 -> A trzeba przeliczyc na v4 (P12).
  - Wersja B na starym ukladzie (`arm_map_village_lore-B.tsv` w katalogu glownym WL) - 687 wspolnych, nie uzywac.
- Sciezki w skryptach: `generator.py` czyta stala `GAME\Armoury\ModuleData\arm_map_villages.tsv` (dzis to juz v4 - przypadkiem dobrze),
  `b_dane.py` wskazuje SCRATCH sesji 3cf3e0ac w Temp. Przed L0: skopiowac WL do repo (np. `tools/wioski-lore/`) albo przepiac na D:.

### 2.2 13 poprawek (WL-P rozdz. 10) - co robia i stan
| Nr | Poprawka | Stan dzis | Uwaga z rozpoznania |
|---|---|---|---|
| P1 | imie rycerza rodu lore z listy krainy (nie z AWOIAF - 9 z 10 martwych / glowne postacie) | nie zrobione | obowiazkowe |
| P2 | Hollard -> wymarly (zamek w ruinie od 277 AC); przejrzec "nearly extinct" / "formerly" | nie | - |
| P3 | herby wymyslone unikalne w krainie (jeden hash na pole / barwe / godlo) | nie | potrzebne dla menu z herbem w wierszu |
| P4 | herb Essos + kody herbow Bannerlorda (`knight_banner`) | nie | kod musi przejsc `Banner.IsValidBannerCode`; ikony CCsBanners tylko gdy mod jest (jest u Jeffa); brak = herb pana |
| P5 | "Kept for them by {KNIGHT}" zamiast "{KNIGHT}, sworn to House X" | nie (podglad ma stara linie) | - |
| P6 | `lands` na okreg ("the X lands" / "the lands of X") | **w kodzie juz liczone na zywo** (`LandsText`, T7) | kolumna `lands` zbedna - jedna regula w kodzie; usunac z kontraktu albo kod ma ja ignorowac |
| P7 | historia omija temat zdania zajecia / miejsca / wiary (ok. 90 wiosek powtorek) | nie | - |
| P8 | westerlands_11: "struck Lannisport hard" | nie | - |
| P9 | usunac Haerega ("seven families in ten") albo [STYL] | nie | - |
| P10 | north_10 "never conquered the North"; dragonstone_12 bez "its lord" | nie | - |
| P11 | `war_lore` dla Sherrer, Mummer's Ford, Wendish Town | nie | zgrac z 7.10 (pamiec wojny) |
| P12 | generator A czyta plik ukladu z argumentu; A i B na ostatecznym v4 | nie | v4 jest juz zamrozony w grze (od 08.10, WGRANIA 28-33 bez zmiany pliku) |
| P13 | jeden plik `arm_map_village_lore.tsv` (A + B po uid) z bramka ukladu; kontrole: 100% pokrycia, 0 wstawek, ASCII, dlugosci | nie | **bramka = crc ukladu ksiegi wiosek (uid:osady, VillageBook.cs:214-217), nie crc calego pliku** - odporna na komentarze; dzis 5A8C2735 |

### 2.3 Co zmienic w modzie (po L0)
- Nowy plik danych `Armoury\ModuleData\arm_map_village_lore.tsv` (ok. 1-1.3 MB: B 748 KB + A ok. 400 KB) - wgranie razem z DLL
  (skrypt wgrania kopiuje dzis DLL i ModuleData? - sprawdzic przy skladaniu; plik ukladu lezy tam od 07.10).
- Nowy czytnik `MapVillageLore` (szkic WL `MapVillageLore.cs` 111 linii): jedno wczytanie na sesje, slownik uid -> wiersz, bramka
  `layout_crc` == `VillageBook` crc ukladu (zly uklad = lore wylaczone, dymek jak dzis). Bez stanu, bez zapisu. Statyczna kopia wspolna
  dla dymka i menu (dzis plik ukladu czyta i widok, i ksiega - osobno; lore niech czyta jedno miejsce).
- Latki: brak. Koszt: wczytanie raz (szac. 20-50 ms), pamiec ok. 2-3 MB napisow.
- "Skonczone": kontrole L0 przechodza (2 446 / 2 446, 0 imion z listy AWOIAF postaci martwych/glownych, 0 powtorzen herbow w krainie,
  ASCII, dlugosci <= 125 / 110), 20 przykladowych dymkow do przejrzenia przez Jeffa (opcjonalnie).

---

## 3. Pozycja 7.9 - dymek wioski (herb, rycerz, wiara, historia) i menu okregu

### 3.1 Dymek - co jest i co dopisac
- Dzis [K]: 3-5 linii `TooltipProperty` w `InformationManager.ShowTooltip(typeof(List<TooltipProperty>), ...)` (rejestr gry,
  `PropertyBasedTooltip`). Linia pana juz na zywo. Brak herbu (zwykly dymek nie umie obrazka herbu - `<img>` tylko dla sprite'ow,
  herb to tekstura z kodu; TECH 1.1 [D]).
- Dopisac (WL-P 1.1-1.2):
  1. **herb pana** (`Clan.Banner`; dla rodu panujacego gra zwraca herb krolestwa) obok nazwy;
  2. **linia rycerza** "Kept for them by Ser Guthor Bridges of Summer Mill" (z L0; Polnoc bez "Ser"; Dothrakowie "takes its tribute");
     gdy BK ma prawdziwego posiadacza tytulu wsi (lordship u kogos innego niz glowa rodu pana) - ta osoba zamiast rycerza z lore
     (refleksja BK `TitleManager.GetTitle(settlement).deJure`, TECH 1.2) - jeden rycerz na wioske, BK ma pierwszenstwo, bo jest w grze;
  3. **po Alt** (albo ustawienie gry "dlugie dymki"): zajecie, wiara, historia (L0), pamiec wojny (7.10); linia "Hold Alt for more info"
     (tekst gry `str_map_tooltip_info`);
  4. **wiara z BK na zywo**: BK ma dla osady `PopulationData.ReligionData.Religions` (slownik wiara -> udzial; `DominantReligion`,
     BK `ReligionData.cs:21-40`) [K]. Armoury juz czyta `PopulationManager.GetPopData` refleksja (`BkSeal34.cs:165`, :353-383).
     Propozycja: linia "Faith: {nazwa wiary BK} ({udzial}%)" z BK, a zdanie lore o swietym miejscu (sept / czardrzewo) tylko gdy
     klucz wiary lore (`faith_key` w `wiara.csv`: old_gods / seven / drowned ...) zgadza sie z dominujaca wiara BK - inaczej samo BK
     (wiara moze sie zmienic w grze; dymek nie moze przeczyc BK). Mapowanie klucz lore -> id wiary BK - tabela w L0.
- Technika (TECH 1.2, szkic `MapVillageTooltip.cs` 200 linii + prefab):
  - wlasny typ klucza `MapVillageTip` + `MapVillageTooltipVM : PropertyBasedTooltipVM` z `BannerImageIdentifierVM Banner`;
    rejestracja `InformationManager.RegisterTooltip<MapVillageTip, MapVillageTooltipVM>(cb, "ArmMapVillageTooltip")` w
    `OnBeforeInitialModuleScreenSetAsRoot`, `UnregisterTooltip` przy wyladowaniu;
  - PULAPKA: delegat musi byc `Action<PropertyBasedTooltipVM, object[]>` (kontrawariancja) - inaczej pusty dymek bez bledu (TECH 1.2);
  - prefab `Modules\Armoury\GUI\Prefabs\ArmMapVillageTooltip.xml` = kopia ROT `PropertyBasedTooltip.xml` + herb 52x52 w `ListPanel`
    (sprawdzony parserem 08.10). **Nowy katalog w instalacji** - skrypt wgrania musi go kopiowac (dzis kopiuje DLL i ModuleData).
  - zapas (MCM off albo rejestracja nieudana): dzisiejszy dymek + linia pana w kolorze rodu (`TooltipProperty.TextColor`).
- Wpiecie w KOD: tylko `Visual.OnHover` (:4604) - zamiast listy buduje `MapVillageTip` (te same dane + lore + herb); `StateLine`,
  `HeldLine`, `RaiderName` bez zmian (jedno zrodlo).
- Koszt: zero na dobe; przy najechaniu jak dzis (raz na najechanie). Herb: jedna tekstura na kod herbu (cache gry).
- Ryzyko: prefab nigdy nie widziany w grze [AUTOTEST]; blad prefabu = brak dymka, nie wywrotka (gra laduje w try/catch, TECH 1.1).
  Gra czysci tekstury herbow przy wejsciu w misje - dymek budowany od nowa przy kazdym najechaniu, wiec nie dotyczy (dotyczy proporczykow).

### 3.2 Proporczyk (punkt 2 decyzji Jeffa 08.10) - poza lista 7.9 w PLANIE
- WL-P rozdz. 2: `MobilePartyVisual.GetBannerOfCharacter(banner, "campaign_flag")` jako osobna encja przy wiosce, tylko z bliska
  (kamera <= 90, najblizsze 80, budowa 12 na cwierc sekundy), porownanie kodu herbu raz na sekunde, przebudowa po bitwie (`ClearVisualMemory`).
- PLAN 7.9 wymienia tylko herb, rycerza, wiare, historie i menu; Jeff 08.10 wzial "wszystkie 8" (z proporczykiem). Proporczyk to nowe encje
  na mapie (CLAUDE.md 7: wizerunki mapy, historia AccessViolation W2) i jedyny element 7.9 z kosztem klatek -> pytanie J2 (rada: osobna,
  ostatnia paczka 7.9 z wlasnym wylacznikiem; jesli autotest pokaze spadek klatek > 5% - zostawic wylaczony).

### 3.3 Menu okregu
- Dzis [K]: "People of this land" (village / town / castle, index 6; village_looted index 0) -> okno `ShowInquiry` z kontami regionu
  i sekcja okregu z ksiegi (`MenuSection`: naglowek, podsumowanie, do 12 wierszy wiosek ze stanem i liczbami, plon).
- Projekt (WL-P 7, TECH 3): osobna opcja "Look over the district" w `village` i `village_looted` -> `MBInformationManager.ShowMultiSelectionInquiry`
  z herbem rycerza w kazdym wierszu (`InquiryElement` z `BannerImageIdentifier`, ramka 86 x 63 w prefabie ROT [AUTOTEST - herb moze byc
  rozciagniety]), dymek wiersza (rycerz, herb tekstem, zajecie, pamiec wojny), "Hear more" -> pelny opis, "Back to the villages".
  Bez przelaczania menu (pulapka CLAUDE.md 7 nie dotyczy - `village` i `village_looted` to zwykle menu, TECH 3.1).
- Kolizja: dwa okna z ta sama lista wiosek i stanem. Rada (pytanie J3): "Look over the district" = lista z herbami i lore, wiersze
  budowane przez TA SAMA funkcje stanu co `MenuSection` (jedno zrodlo tekstu stanu); w "People of this land" zostaje tylko naglowek
  i podsumowanie okregu (bez wierszy wiosek). Bez zgody - zostawic "People..." bez zmian i dodac drugie okno (duplikat, ale bez regresji).
- Kolejnosc opcji: BK i ROT dokladaja opcje do `village` - index nowej opcji sprawdzic w autotescie (WL-P ryzyko 7).
- Koszt: zero na dobe (tylko po kliknieciu).

### 3.4 Napisy na mapie (decyzja Jeffa L, 09.10: "tylko nad SPALONYMI wioskami")
- WM 5.6: wlasna warstwa Gauntlet (jak `MapNameplateLayer`), pozycja `MBWindowManager.WorldToScreenInsideUsableArea`, tylko z bliska,
  najwyzej ok. 30 w kadrze; tekst "{VILLAGE} has been burned" (jest `VilBurned`). Precedens wlasnej warstwy: `CraftPopup.cs:216`.
- Wymaga prefabu (`GUI\Prefabs`) - bez UIExtenderEx (UIExtenderEx tylko do zmian CUDZYCH prefabow; ForgeView na nim - nie dotykac).
- **Przy dzisiejszym tempie E4/5 napisow prawie nie bedzie (0 spalonych w 120 dobach [L]).** Rada: napisy w tej samej paczce co wyglad
  spalonej (7.10, ten sam stan), wlaczone dopiero razem z 7.11 albo z poprawionym progiem (pytanie J4).

---

## 4. Pozycja 7.10 - komunikat, gdy plonie Twoja wioska; kto spalil; wyglad spalonej

### 4.1 Komunikat
- Gra [K]: zadnej `MapNotification` dla rabunku (sa tylko oblezenia: `SettlementUnderSiegeMapNotification`); `CommentOnChangeVillageStateBehavior`
  dopisuje tylko wpis dziennika (`VillageStateChangedLogEntry`, gdy gracz rabuje albo jest panem, i dla krolestw). ROT, BK, BKROT, Diplomacy,
  BEE - brak napisu o rabunku wsi gracza (przeszukane napisy DLL). Nowy komunikat niczego nie dubluje.
- Zrodlo zdarzenia [K]: `CampaignEvents.VillageStateChanged(village, old, new, raiderParty)` (ChangeVillageStateAction.cs:8-14) - wolane
  przy `ApplyBySettingToBeingRaided` (RaidEventComponent.OnInitialize) z partia napastnika i przy `ApplyBySettingToLooted` (OnBeforeFinalize).
  Przy milicji rabunek to 2 zdarzenia (walka, potem lupienie - RB K12) -> filtr: jedno "plonie" na wies na sesje (klucz: `MapEvent.Component`
  albo doba + napastnik).
- Kto dostaje: wies rodu gracza (`Village.Bound.OwnerClan == Clan.PlayerClan` - to samo, co "your fief" w `HeldLine`) i rabunek gracza.
  Swiat ma ok. 3 rabunki na dobe (LOG120: 308 zakonczonych w 104 dobach linii) - dla wszystkich to spam (A08 W-3). MCM: `Map Village Messages`
  (None / Own fiefs / Kingdom), domyslnie Own fiefs (WM 10).
- Teksty sa gotowe [K]: `VilLogBurningOwn` "Smoke over {VILLAGE}: {RAIDER} is burning your village in the {DISTRICT} district.",
  `VilLogBurned` "{VILLAGE} in the {DISTRICT} district has been burned by {RAIDER}.", `VilLogBurnedPlayer`, `VilLogRebuilt`, `VilLogAbandoned`.
  {VILLAGE} = wioska, na ktorej stoi ogien (`VillageBook.FxUid` - ten sam wybor co widok), {RAIDER} = przywodca (`RaiderName`).
- Postac: `MBInformationManager.AddQuickInformation` (pasek u gory, z portretem napastnika) + linia dziennika (`Log.Player`, jak CrossingLaw).
- Koniec rabunku: "has been burned" od zdarzenia gry Looted (rabunek udany), NIE od progu 50% ksiegi - inaczej nie wyswietli sie prawie nigdy
  (odkrycie 1). Prog ksiegi zostaje dla trwalego stanu (wyglad, napisy).
- Koszt: zero (zdarzenie rzadkie, filtr po rodzie). Latki: brak (nasluch). Stan: brak (dedup w pamieci sesji).

### 4.2 Kto spalil - pamiec wojny (punkt 7 decyzji 08.10; WL-P 8, szkic `WarMemory.cs` 257 linii)
- Gra pamieta sprawce w `VillageStateChangedLogEntry`, ale wpis ginie z dziennika; ksiega E4/11 trzyma tylko `EvDay` i nazwisko w liscie
  logu (`CheckUp` :630-640: `raider` idzie tylko do `_dBurnedList`).
- Najprosciej i bez drugiego zrodla: dopisac do ksiegi na wioske "ostatni napastnik" (nazwa) i licznik spalen - VillageBook ma juz
  `EvDay`/`RebuiltDay` w sekcji `w:` zapisu `arm_district`; liczby "ilu ucieklo / zginelo" z `Devastation.Strike` (refugees, dead) przez
  `RaidState` (2 pola) - sesja juz ma klucz komponentu rabunku.
- **Kolizja zapisu [K]:** klucz `arm_district` pisze w calosci `VillageBook.Export` (ArmouryBehavior.cs:461). Projekt WL-P chcial "sekcji m:
  wspolnego klucza" z osobnej klasy - to musi isc PRZEZ VillageBook.Export/Import (sekcja zarezerwowana jak `r:`/`c:` E411:201), nie drugim
  wolaniem `SaveText.Sync` na ten sam klucz. Stary zapis: brak sekcji = brak pamieci (bez bledu).
- Tekst: "Burned by {RAIDER} in the {SEASON} of {YEAR} AC; about {FLED} souls fled and {DEAD} were killed." - pora i rok z kalendarza
  Armoury (364 dni); jak liczyc rok "AC" (start ROT 298/299 + lata kampanii) - sprawdzic w kodzie przy pisaniu.

### 4.3 Wyglad spalonej (i mniejszej) wioski
- Dzis [K]: poziom obrazka staly z pliku (`Create` :2102 `s.R.Level`); ogien tylko w czasie rabunku, dym tylko w czujnosci (`IsRaided`);
  potem wioska wyglada na cala (A08 3.7). Zgliszcza wsi-matki (`*_looted`) celowo NIE kopiowane (`MapVillagesView.cs:1268-1275`) -
  odczyt nazw prefabu na `*_village_looted` konczyl sie naruszeniem pamieci (08.10, CrashScribe).
- Propozycja (bez nowych odczytow silnika):
  1. wioska **czesciowo pusta** - nizszy poziom obrazka (3 -> 2 -> 1) wedlug udzialu pustych osad z `StateOf` (WM 1.4 "wioska maleje");
  2. **spalona** (stan ksiegi `SBurned`, >= 50% osad) - przyciemnienie `GameEntity.SetFactorColor` na kopiach (WM 5.4) + zdjete detale
     (stog, woz) + dym `map_icon_village_plunder_fx` przez czujnosc;
  3. przebudowa obrazka TYLKO przy zmianie stanu ksiegi (zdarzenie raz na dni) - oznaczenie slotu jako brudnego, przebudowa przy
     najblizszym pokazaniu (te same `Create` / `RemoveAll`). Nic co klatke (CLAUDE.md 7).
  4. wies gry trwale "looted" przy polowie JEJ osad (WM 5.5, postfiks `SettlementVisual.RefreshLevelMask`) - najwieksze ryzyko (latka na widok
     gry); rada: osobny wylacznik, na koncu.
- Wazne: przy E4/5 stan `SBurned` nie wystepuje (odkrycie 1) - punkty 2-4 zobaczy sie dopiero z 7.11 (albo nizszym progiem).
- Koszt: przebudowa kilku obrazkow na dobe; klatki bez zmian. [AUTOTEST + zdjecia]: czy `SetFactorColor` dziala na kopiach (skrypt
  "Town Entity Manager" moze nadpisywac - WM 5.4; kopie W2 nie maja skryptu, wiec powinno).

---

## 5. Pozycja 7.11 - rabunek osada po osadzie, AI tak samo (D, NK)

### 5.1 Jak dzis dziala rabunek [K]
- Gra: `RaidEventComponent.Update` (faza lupienia gdy `DefenderSide.TroopCount == 0`) co krok 0.05 punktow osady zdejmuje 0.5 x krok
  x hearth, daje zloto za hearth i towar z niczego, przy punktach 0 konczy (Looted). E4/5 prefiks tylko zbiera osobodni i podmienia
  hearth na "wirtualny" (Devastation.cs:330-361), finalizer oddaje prawdziwy hearth i rozlicza `Strike` (ludzie -> ksiega, `VillageBook.OnStrike`
  -> `Burn` lancuchem wiosek).
- Wynik LOG120: 308 rabunkow (75 udanych = Looted, 233 przerwane), srednio 132 napastnikow, ok. 460 trafionych ludzi na dobe swiata,
  "lup z niczego" 79 103 zl (gracz 0) [L].
- AI: cel rabunku tylko wies Normal bez MapEvent (AiMilitaryBehavior.cs:426, :431), wynik celu x (1 + (1 - punkty osady))
  (DefaultTargetScoreCalculatingModel.cs:301); po wygranej walce z milicja wraca rabowac (AiMilitaryBehavior.cs:99-104); ROT podmienia
  godzinne myslenie AI (`AIThinkPatch`, takze na morzu - RB K12) i przerywa rabunki. Menu gracza: `raiding_village` (menu oczekiwania),
  "End Raiding", `village_player_raid_ended` - nazwy potwierdzone w 1.4.8 (VillageHostileActionCampaignBehavior.cs:99-122).

### 5.2 Co z RB jest juz zrobione (mapowanie na dzisiejszy kod)
| RB | Dzis |
|---|---|
| liczba osad okregu n = ludzie / 250, zamrozona | VillageBook `EnsureN` (`DistrictSettlementPeople` 250), zapis `n:` |
| puste osady, stany, odbudowa z powrotu uchodzcow | VillageBook `Br`/`Bf`, `StateAt`, powroty E4/5 (rok od ostatniego najazdu) |
| lancuch palenia od strony wroga | `SessionFor` (warownia frakcji napastnika) + `PickFirst` / `NextFrom` |
| jency z rabunku tylko kultury z niewola | E4/9 `ThrallLaw.CapturePrefix` (prefiks BK `ExecuteCapture`) |
| spichlerz pali ulamek trafionych | `Famine.Burn` w `Strike` (GL 2.2 pkt 6) |
| zabici / las / uchodzcy z trafionych | `Devastation.Strike` |
| produkcja wedlug rak w Looted | `LootedProducesByPeople` (E4/5) |

### 5.3 Co zostaje (nowa paczka `DistrictRaid`, wedlug RB 0-11 po poprawkach K1-K12)
1. **Przejecie fazy lupienia**: prefiks `RaidEventComponent.Update` zwraca false w lupieniu (gdy regula czynna i wies ma przelicznik);
   krok = m osad (m = min(Z, max(1, N / D)), D = 200), postep w ludziach przeszukanych (R x N x f x u x dt), punkty osady = 1 - 0.99 x postep
   (nigdy 0 w kroku - K1), co 5% kroku `Strike` na LICZBE OSAD: ludzie = m x (L / n) -> `OnStrike` -> `Burn` (wpiecie przewidziane w E411:365).
   **Zamiast drugiej sciezki** - nowa metoda `Devastation.StrikePeople(v, ludzie, napastnik)` obok `Strike` (wspolny rachunek zabitych /
   lasu / uchodzcow / spichlerza); straznik `if (DistrictRaid.Owns(raid)) return;` w RaidPrefix i EndPrefix E4/5 (RB K11) - inaczej ludzie
   liczeni 2 razy.
2. **Gracz**: po kazdej osadzie okienko `ShowInquiry` z ticku (wzorzec NightRest - flaga, okienko w ticku menu; teksty `RaidStep*`, `RaidNext`,
   `RaidEnd` juz w VillageTexts); "End the raid" = punkty 0 + `SetOverrideWinner(Attacker)` + StartWait (K2), dalej droga gry
   (`village_player_raid_ended`); postfiks init raidu po wczytaniu (StartWait). ZAKAZ `SwitchToMenu` z menu oczekiwania (CLAUDE.md 7).
3. **AI**: jeden krok na sesje, potem koniec przez droge gry (punkty 0 -> Looted -> RaidCompleted).
4. **Czujnosc** (Looted) ceil(1 + 100 x d) dob, max 17 (d = trafieni sesji / ludzie okregu); koniec czujnosci w dobie; prefiks
   `VillageHealCampaignBehavior.DailyTickSettlement` dla wsi w czujnosci (bez +20 milicji z niczego).
5. **Kara relacji** - `RaidDamage = q` przed `RaidCompleted` (K3; inaczej rabunek bez kary).
6. **Lup z prawdziwych zasobow** (zamyka "lup z niczego" 79 tys. zl / 120 dob): udzial s = ludzie kroku / ludzie w domu z magazynu
   wsi (`ItemRoster`) i kiesy wsi (`GiveGoldAction.ApplyForSettlementToCharacter`); napastnicy jedza ze spichlerza okregu tyle, ile zjadaja (K4).
   Zgodne z E45:538 ("7.11: lup liczony od trafionych").
7. **Zapis**: sekcje `r:` (sesje), `c:` (czujnosc) w `arm_district` przez VillageBook.Export (zarezerwowane, E411:201); brak = sesja od nowa
   z punktow gry (stary zapis wczytuje sie bez bledu).
8. MCM (EN): `Raid By Settlement Enabled`, `Raid Detachment Men` 200, `Raid Alert Max Days` 17 itd. (RB 10); R = istniejace `DevastationPerRaiderDay`.

### 5.4 Kolizje i ryzyka (kontrola calosci, CLAUDE.md 8.0)
- **Nasluchy konca rabunku** widza jeden `RaidCompleted` na sesje jak dzis: E4/5 EndPrefix (podmiana hearth), E4/9 jency (BK ExecuteCapture),
  `WatchLaw.OnRaidCompleted` (ArmouryBehavior.cs:755), `OutlawLaw.OnVillageLooted` (:715), ROT nekromancja (E4/2 R13 - "rajd i przejecie
  osady - tylko zabici"), BKROT piety, BEE (-8% chlopow i przerwa dowozu na KAZDE `VillageBeingRaided`, z milicja 2 razy), Diplomacy.
  Dluzsze sesje = rzadsze zdarzenia; liczyc w autotescie.
- **Tempo wojny**: lord 112 ludzi pali 1 osade w ok. 4.6 doby (gra dzis ok. 2.4); armia 2 000 - 10 osad w 2.6 doby (RB). Juz dzis 233 z 308
  rabunkow przerywa AI/odsiecz [L] - przy krokach 2-5 dob czesc AI nie skonczy zadnej osady (czesc kroku w ksiedze, bez czujnosci).
  Pomiar: doby w rabunku na lorda, udzial przerwanych, oblezenia.
- **Spustoszenie swiata** ok. 4x dzisiejszego na rabunek (1 osada = 250 ludzi vs dzis ok. 57 na rabunek), wiec wioski zaczna dochodzic do
  "spalona" - to wlasnie warunek widocznosci 7.9/7.10 (odkrycie 1). Zmierzyc spalone / 120 dob.
- **ROT `AIThinkPatch`** przerywa rabunki co godzine (takze na morzu); NavalDLC rabunek z morza ten sam komponent.
- **Menu/CTD** (RB ryzyko 7): pasek 99% przy okienku, "End the raid", "End Raiding" po 1 osadzie i przy 0, wczytanie w stanie decyzji, armia
  gracza / gracz w armii AI, odsiecz w trakcie okienka, Escape - wszystko [AUTOTEST] (autotest gra AI; czesc tylko reczny test Jeffa).
- **NK**: wedlug kodu stary zapis sie wczyta (nowe sekcje tekstowe, brak nowych typow zapisu); pelny efekt (tempo wojny od startu, osady n)
  w nowej kampanii - oznaczenie NK zostaje.
- **Kolejnosc** (RB 11): wymaga E4/5 + spichlerza (sa) i E4/11 (jest) - mozna pisac teraz. Linie gry w RB z dekompilacji sprzed 1.4.8
  (`ore-supply\cs`) - przy pisaniu sprawdzic na dec32.
- Koszt na dobe: prefiks Update na ok. 7 rownoczesnych rabunkow (zastepuje petle lupu gry), 20 `Strike` na krok, petla partii napastnika przy
  jedzeniu i lupie - rzad pojedynczych ms / dobe; do zmierzenia (zasada Jeffa: koszt na dobe).

### 5.5 Wielkosc i podzial
D (najwieksza pozycja etapu). Rada: dwie paczki z osobnymi wylacznikami - (a) AI + przejecie lupienia + czujnosc + lup + kara relacji
(autotest gra to w calosci), (b) okienko gracza (tylko reczny test Jeffa; bez (b) gracz rabuje jak AI - jeden krok i koniec).

---

## 6. Pozycja 7.12 - licznik ruchu przez Bliznaki

### 6.1 Stan [K][L]
- Bliznaki = `ROT_town3` (pos 374.07 / 630.77, brama 373.94 / 632.28, ROT-Map settlements.xml); jedyny most przez Zielone Widly (lore).
- Dzis liczone sa tylko WEJSCIA do osady (`Measure169c.OnSettlementEntered`, linia "(e) Bliznaki: wejscia do osady N (karawany ...,
  lordowie ..., wiesniacy ...)"): LOG120 120 linii, 503 wejscia (ok. 4 / dobe, najczesciej karawany). Partia, ktora przechodzi most bez
  wejscia, nie jest liczona - a to ona placilaby myto.
- `CrossingLaw.AiTick` - martwy dla AI (odkrycie 3). Audyt 00 (E3, S14) widzial tylko "co 4. wywolanie", nie warunek gracza.

### 6.2 Jak liczyc przejscia (propozycja)
- Raz na godzine gry (`CampaignEvents.HourlyTickEvent`, nie tick klatki): `MobileParty.StartFindingLocatablesAroundPosition(brama, 8)` /
  `FindNextLocatable` (siatka gry, precedens `NightMarch.cs:564`) - tylko partie w promieniu 8 od mostu, bez petli po `MobileParty.All`.
- Strona rzeki = znak iloczynu wektorowego (pozycja - most) x kierunek rzeki. Kierunek rzeki liczony RAZ przy wczytaniu z siatki nawigacyjnej:
  64 punkty na okregu r = 4 wokol mostu, `GetFaceTerrainType` (woda / lad - precedens `TerrainEase.cs:76`, `MapVillagesView` brzeg), os = prosta
  przez srodki dwoch lukow wody; zapas: os z autotestu wpisana stala. Promien 8 przy predkosci ok. 5 jedn./h = co najmniej 2 probki w strefie.
- Pamiec: slownik partia -> ostatnia strona (tylko partie w strefie; wpis znika, gdy partia wyjdzie) - przejscie = zmiana strony.
  Bez zapisu (po wczytaniu liczy od nowa), czyszczenie w `Reset`.
- Linia dobowa (zamiast albo obok "(e) wejscia" w Measure169c, ten sam przelacznik `On`): "Przeprawa Bliznaki: przejsc N - karawany N
  (kiesy X zl, ludzi M, zwierzat K), wozy wsi N, partie rodow N (krolestwo pana / obce), bandy N, gracz T/N; myto gdyby bylo Z zl
  (stawki ESENCJA 21: 2 zl od czlowieka, 1 zl od konia/zwierzecia; rody krolestwa Freyow za darmo)".
- Bez nowej latki, bez stanu w zapisie, bez napisow w grze (sam log). Koszt: 24 zapytania siatki na dobe, kilkadziesiat partii - < 0.1 ms / dobe.
- Ryzyko: brod albo drugi most w promieniu 8 (ROT moze miec przejscie przez rzeke poza Bliznakami) - [AUTOTEST]: wypisac w pierwszych
  3 dobach pozycje przejsc (x/y) do logu; jesli przejscia sa daleko od mostu - zmniejszyc promien albo liczyc tylko w pasie wzdluz mostu.

### 6.3 Po liczniku
- Myto (ESENCJA 21: Jeff "Tak, po pomiarze ruchu") NIE jest osobna pozycja PLANU - po 7.12 potrzebna osobna paczka (zamknieta ekonomia:
  kiesa partii -> kasa Bliznakow, bez zlota z niczego; nie dublowac BK `CaravanFee`; jedna regula myta z A08 N-1 / audyt 00 S16).
  Rada: zapytac Jeffa o stawki dopiero z liczbami z autotestu 120 dob (pytanie J5).
- Naprawa `CrossingLaw.AiTick` - etap 8 (wlasna paczka; obudzenie zmieni gre: wrogie partie przy 3 przeprawach beda zawracane).

---

## 7. Kolizje miedzy pozycjami 7.8-7.12 i z reszta (kontrola calosci)
1. **Jeden stan "spalona"**: dymek (stan), menu, komunikat koncowy, wyglad, napisy, pamiec wojny czytaja `VillageBook.StateOf` / zdarzenie
   gry - zadnych drugich progow. Komunikat "has been burned" od zdarzenia Looted (gra), trwaly wyglad od progu ksiegi (50%).
2. **Jeden zapis `arm_district`** - nowe sekcje (pamiec wojny, `r:`, `c:`) wylacznie przez `VillageBook.Export/Import`.
3. **Jedno wczytanie lore** - `MapVillageLore` statyczny dla dymka i menu; bramka = crc ukladu ksiegi.
4. **Jeden wybor wioski z ogniem** - `VillageBook.FxUid` dla widoku, komunikatu i pamieci wojny.
5. **UI**: wlasne prefaby Armoury (dymek, napisy) w `Modules\Armoury\GUI\Prefabs` - bez UIExtenderEx (ForgeView dalej jedyny uzytkownik);
   menu okregu na prefabie gry/ROT (`MultiSelectionQueryPopup`), bez zmian cudzych prefabow.
6. **Wizerunki mapy** (wyglad spalonej, proporczyki): tylko przy zmianie stanu, licznik potkniec per obiekt, nigdy globalny wylacznik
   (CLAUDE.md 7, `_tentBroken`), zadnych odczytow nazw prefabu (AccessViolation 08.10).
7. **Etap 8 osobno**: `CrossingLaw.AiTick` i nieuzywane teksty sa zgloszone, nie naprawiane przy okazji.

---

## 8. Pytania do Jeffa (z rada)
- **J1 (7.8)**: przejrzec 20 przykladowych dymkow przed zamrozeniem danych? Rada: tak, ale nie blokuje (teksty [STYL] poprawia sie w Pythonie).
- **J2 (7.9)**: proporczyk w barwach pana nad wioskami (punkt 2 z 08.10) - robic w etapie 7? Rada: tak, jako ostatnia paczka 7.9 z wylacznikiem;
  zostaje wylaczony, jesli autotest pokaze spadek klatek > 5%.
- **J3 (7.9)**: menu okregu - osobne "Look over the district" (z herbami i opisem wiosek), a w "People of this land" tylko podsumowanie okregu?
  Rada: tak (jedno miejsce na liste wiosek).
- **J4 (7.9/7.10)**: wyglad spalonej i napisy nad spalonymi zobaczysz dopiero, gdy wioski beda naprawde plonac do polowy - dzis (bez 7.11)
  przez 120 dob nie splonela zadna. Wlaczyc razem z 7.11 (rada), czy obnizyc prog "spalona" z 50% osad?
- **J5 (7.12)**: myto na Bliznakach - stawki po liczbach z licznika (120 dob). Rada: najpierw licznik, potem jedno pytanie z liczbami.
- **J6 (7.11)**: Twoj rabunek osada po osadzie z okienkiem po kazdej osadzie jest nieoplacalny (zold 2-10 dob na osade, prawie zero zlota,
  RB ryzyko 5) - tak ma byc? Rada: tak (decyzja 07.10 "rabunek sluzy wojnie, nie zarobkowi"), tylko potwierdzic.

---

## 9. Proponowane "Skonczone, gdy" dla 7.8-7.12 (zamiast nieaktualnego warunku "Winterfell - KP 77 dni")
- 7.8: plik lore przechodzi kontrole L0 (2 446 / 2 446, bramka ukladu = crc ksiegi, 0 imion martwych / glownych postaci, unikalne herby w krainie, ASCII).
- 7.9: w autotescie (tryb zdjec) dymek z herbem, rycerzem i Alt na 3 krainach; 0 potkniec "MapVillagesView.OnHover" i "lore"; menu okregu otwiera
  sie we wsi i we wsi spladrowanej, 0 bledow; (proporczyki: spadek klatek < 5%).
- 7.10: komunikat dla wsi rodu gracza przy kazdym rabunku (licznik w logu = liczba rabunkow wsi gracza), pamiec wojny przechodzi zapis ->
  wczytanie; stary zapis wczytuje sie bez bledu.
- 7.11: autotest 120 dob: 0 bledow, lup z niczego 0 zl, "trafieni = zabici + las + jency + uchodzcy" (kontrola 0), doby w rabunku na lorda i udzial
  przerwanych w progach z RB (lord <= 5 dob na osade), wioski "spalone" > 0; reczny test Jeffa okienka i "End the raid".
- 7.12: linia "Przeprawa Bliznaki" co dobe przez 120 dob, przejsc > 0, pozycje przejsc przy moscie.
- Calosc: koszt naszych modow na dobe nie rosnie o wiecej niz ok. 1% (pomiar 8.17), progi etapow 2 i 5 w tescie zbiorczym bez zmian.

---

## 10. Kolejnosc i wielkosc (propozycja)
1. 7.8 L0 (Python, offline) - od razu; nic nie dotyka gry.
2. 7.12 licznik (S, sam log) - maly, niezalezny.
3. 7.10 komunikat + pamiec wojny (S) - nasluch, sekcja w ksiedze.
4. 7.9 dymek z herbem + Alt + wiara BK (S-M), potem menu okregu (S), na koncu proporczyk (S, J2).
5. 7.11 (D) w dwoch paczkach (AI / gracz).
6. 7.10 wyglad spalonej + napisy nad spalonymi (S) - po 7.11 (J4).
Kazda: autor + recenzent, wylacznik MCM (EN, `python tools/gen_mcm.py`), wpis CHANGELOG z "Ryzyko / co sprawdzic", test zbiorczy wedlug zasady Jeffa z 12.10.

## 11. Pliki zrodlowe uzyte w rozpoznaniu
- KOD: `MapVillagesView.cs` (:1-60, :1355-1460, :1750-1945, :2095-2110, :4438-4664), `VillageBook.cs` (:1-130, :441-460, :560-760, :930-1030,
  :1392), `VillageTexts.cs`, `Devastation.cs` (:1-120, :233-262, :300-440, :1003-1060), `PeopleMenu.cs`, `CrossingLaw.cs`, `Measure169c.cs`
  (:880-930), `Settings.cs` (:724-799, :1263-1268), `ArmouryBehavior.cs` (:461, :715, :755, :790), `CraftPopup.cs`, `BkSeal34.cs`.
- GRA: `VillageStateChangedLogEntry.cs`, `CommentOnChangeVillageStateBehavior.cs`, `ChangeVillageStateAction.cs`, `Village.cs:50-77`,
  `RaidEventComponent.cs:105-160`, `AiMilitaryBehavior.cs:90-105, :380-431`, `DefaultTargetScoreCalculatingModel.cs:301`,
  `VillageHostileActionCampaignBehavior.cs:94-122, :344`, `MobileParty.cs:4061-4066`, katalog `MapNotificationTypes`.
- BK: `ReligionData.cs:14-60`, `ReligionsManager.cs:375`, `PopulationData.cs:53, :83`.
- Dane: `ROT-Map\ModuleData\settlements.xml:8077` (Bliznaki), `Modules\Armoury\ModuleData\arm_map_villages.tsv`, WL (`v4-proba`, `podglad_rycerze.tsv`,
  `skrypty`, `b-skrypty`, `szkice`), LOG120 (linie "Ludzie (spustoszenie)", "Ksiega wiosek (E4/11)", "(e) Bliznaki", "crc ukladu").
