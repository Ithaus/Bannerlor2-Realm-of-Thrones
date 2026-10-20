## m96-wzory (20.10) - luki tieru 6 znane od poczatku gry i pokrewne bledy wiedzy kuzni (Armoury + ForgeView)

**Dla Jeffa (prosto).** Goldenheart, Giant Bow i luk oraz strzaly Ravens' Teeth byly "znane" od pierwszego dnia, bo kuznia rozdawala wzory
tieru 1 chwile przed tym, jak Realm of Thrones (przy RBM) podbija tym lukom naciag do tieru 6. Teraz wzory rozdaje sie dopiero po wszystkich
zmianach, a Twoj zapis naprawi sie sam przy wczytaniu: te 4 wzory znikna z ksiegi, a to, czego naprawde sie nauczyles, zostaje. Przy okazji: nowy
wzor z nauki wypada najwyzej o tier ponad to, co Twoj kowal juz umie wykuc (nie od razu tier 6), tarcze w CRAFT wymagaja Kowalstwa jak w naszym menu,
wielkie luki i imienne zbroje ROT uczysz sie tylko z przetopu albo rozbiorki zdobytego oryginalu, a kafelek mowi bursztynem "PATTERN KNOWN - NEEDS
165 SMITHING", gdy wzor znasz, ale reka jeszcze nie siega. Klik "Craft" na zamknietym wzorze juz nie daje punktow nauki.

**Problem.** Jeff 20.10 (zrzut kuzni, zakladka CRAFT, Bows): "[VI] 200 Pound Goldenheart Longbow - PATTERN KNOWN, 250 Crafting Difficulty", jasne
tez [VI] 500 Pound Giant Bow i [VI] 200 Pound Ravens' Teeth Longbow, Kowalstwo 30, nowa kampania (Seagard, dzien kilka): "jest blad, ze moge
stworzyc luki tier 6 od poczatku gry, zobacz, czy nie ma wiecej takich bledow".

**Dowod.**
- Log Jeffa `Armoury-2026-10-20_13-28-19.log`, ta sama sesja, bez wczytania: linia 495 `BOWS: 19/65 | t1 19/19 | t2 0/12 | t5 0/7 | t6 0/12`,
  linia 6231 `BOWS: 19/65 | t1 15/15 | t2 0/9 | t5 0/8 | t6 4/18` - znanych dalej 19, ale 4 z nich przeszly z t1 do t6. Ksiega (495) stoi przed
  `HistoricalPrices: ROT-RBM (ROTRBMCompatibility) nadpisal ceny lukow ROT` (507). Sesja 14-33-54: to samo (linie 489 i 501).
- Start kampanii `Armoury-2026-10-20_10-24-16.log`: `rozdano wzory tieru 1 (582 sztuk)` linia 525, ROT-RBM linia 539 - ta sama sekunda 10:25:29.
- Kod: `ItemObject.Tierf` = `ItemValueModel.CalculateTier` na zywo; RBM: luk (MissileSpeed-60) x 0.049, strzaly z obrazen i wagi.
  `ROT.CampaignBehaviors.ROTRBMCompatibility` (dekompilacja ROT.dll): tylko `OnSessionLaunched -> ModifyBowsAndArrows` (goldenheart 87->200,
  ravens_teeth_longbow 87->210, giant_bow 90->500, dragonbone 95->180, weirwood 93->170, giant_arrows i ravens_teeth_arrows - obrazenia).
  `RangedLore.Seed` (warunek TierOf == 1) biegl z `ReportLedger` w `ArmouryBehavior.OnSessionLaunched`, czyli przed ROT. Rachunek z ROTassets.xml
  daje dokladnie t1 -4, t2 -3, t5 +1, t6 +6 jak w logu. W logach Jeffa nie ma zadnej nauki tych 4 wzorow (brak "zdjety z gotowej" i "odkryto losowo").

**Przyczyna.** Kolejnosc: wzory tieru 1 rozdawane przed ostatecznymi statystykami lukow ROT-RBM; `LorePurgeOnce` (siatka bezpieczenstwa) tez
biegla przed ROT i jest jednorazowa. Dotyczy KAZDEJ kampanii zaczetej z RBM i integracja ROT-RBM. Autotesty tego nie widzialy, bo ksiega startowa
drukowala sie przed zmiana ROT.

**Zmiana** (nowe pliki `Armoury/src/LoreSeedFix.cs`, `Armoury/src/PatternGate.cs`; grupa MCM "Learning patterns", 5 kluczy, wszystkie domyslnie TAK):
1. `LoreSeedAfterFinalStats` - (a) `RangedLore.Seed` nic nie robi, dopoki statystyki tej kampanii nie sa ostateczne; rozdanie, `LorePurgeOnce`
   i linie ksiegi ida w `CampaignEvents.OnAfterSessionLaunchedEvent` (po OnSessionLaunched wszystkich modow, takze ROT). (b) JEDNORAZOWA naprawa
   zapisu, znacznik `#m96seedfix1` w liscie Known (format `arm_rangedlore` "id|id;pkt" bez zmian; build 95 znacznik pomija, bo `GetObject` = null).
   Zdejmowane jest tylko to, co dal blad: wzory z bloku rozdania (miedzy `#seeded2` a `__seed_ammo_v1` - tam stoi wylacznie to, co dal Seed;
   wszystko zdobyte potem stoi ZA `__seed_ammo_v1`), ktore sa dzis tierem 2+, a przy wczytaniu byly tierem 1 (migawka tierow w SyncData, przed ROT)
   albo sa na liscie ROT-RBM (7 id, zapas, gdy migawka jest juz po zmianie - drugie wczytanie w tym samym procesie). Opanowane unikaty (UniqueLore)
   nie sa ruszane. Wzory zdobyte (rozbiorka, przetop, losowanie) zostaja - sa za znacznikiem amunicji. (c) Co sesje dopisanie brakujacych wzorow
   tieru 1 po ostatecznych statystykach (log: BARDING `t1 27/27` na starcie kampanii, `t1 27/28` w kolejnych sesjach - wzor, ktory stal sie
   tierem 1 / wzorem do nauki po rozdaniu). (d) Nowa kampania w tym samym procesie gry: Known, punkty nauki, `UniqueLore` i `Legends` od zera
   (`OnNewGameCreatedEvent`; statyczne listy przeciekaly - SyncData przy nowej grze nie biegnie). (e) Klik Craft bez kucia nie uczy:
   `FletchForge.BkCraftPostfix` czyta `__runOriginal` (Harmony 2.4.2: `MethodCreator` - zmienna `runOriginalVariable` takze dla postfiksow).
   Bylo: przy odmowie z `BkCraftPrefix` (wzor nieznany) `ArmourQuality.CraftPrefix` zerowal `Refused`, wiec postfiks dawal punkty nauki i okno
   wyniku za klik w zamkniety wzor pancerza.
2. `PatternDrawBySkill` - gorna granica losowania nowego wzoru = Smithing kowala / `SmithingSkillPerTier` + 2 (Smithing 30 przy progu 35: tier 2;
   35: 3; 140: 6), nigdy ponizej tieru wykutej sztuki; legenda do puli dopiero, gdy kowalowi brakuje najwyzej jednego progu do
   `LegendarySkillNeeded`. Kowal = ten przy kowadle (`Forge.Smith` - `hero`; CRAFT BK - `CurrentCraftingHero`). Bylo: od tieru sztuki do 6 z rowna
   szansa (`RangedLore.cs` 386) - log 13-28-19 linia 6239: `odkryto losowo GRE_vlandic_arrows (BOWS, t4, zakres 1-6)` po 3 lukach t1. Nauka
   z cudzej sztuki (`Study`, przetop znanego) bez zmian: najwyzej tier tej sztuki.
3. `ShieldCraftSkillGate` - tarcza w CRAFT BK: `ArmourQuality.CraftPrefix` (przy `CraftOn`) pyta `PatternGate.ShieldCraftPrefix`: Smithing kowala
   < `Recipes.For(tarcza).SkillNeeded` (Need(tier, 0): t2 35 ... t6 175 - ten sam prog co nasze menu kowala `Forge.Begin`) = odmowa, nic nie
   schodzi, `ArmourQuality.Refused` (bez nauki i okna). Kosci BK (pekniecie) bez zmian - "Crafting Difficulty" tarczy zostaje wzorem BK, bo BK liczy
   z niej szanse pekniecia (`BKSmithingModel.CalculateBotchingChance(hero, difficulty)`); prog pokazuje kafelek (pkt 5).
4. `ForgeUniquesByStudy` - przedmiot z `RotUniques` w klasie nauki (luki, strzaly, zbroje, tarcze, kropierze) jest unikatem kuzni jak `UniqueGear`:
   `Teachable` = false (nie rozdaje sie, nie losuje, nie liczy w ksiedze), `KnownOf` / `CanLearnFrom` / `Learn` ida przez `UniqueLore`
   (przetop albo rozbiorka zdobytego oryginalu; rozbiorka unikatu wymaga Smithing 200 i nie pali sztuki - regula Jeffa 31.08, `SmithMenu`),
   `FletchForge.BkCraftPrefix` mowi "seize the original". Bylo: Goldenheart, Giant Bow, Weirwood, Dragonbone i Giant Arrows w puli nauki (Teachable
   dla luku nie patrzyl na unikaty), a imienne zbroje spoza listy `UniqueGear` (lyanna_armor, loras_*, mace_*, gold_cloak_*, blackfish_*...) zamkniete
   na zawsze (Teachable odrzuca NotMerchandise, a droga unikatu ich nie znala). Lista `UniqueGear` (wspolna z CrashScribe/Mends) bez zmian.
5. `PatternTileShowsSkill` (Armoury `PatternGate.BlockText` + ForgeView) - trzeci stan kafelka w CRAFT: znany wzor, ktorego kowal z ekranu jeszcze nie
   moze zaczac - bursztynowy napis "PATTERN KNOWN - NEEDS 165 SMITHING" albo "PATTERN KNOWN - LEGEND ALREADY FORGED" i lzejsza plachta (0.32) na
   wierszu listy. Ta sama regula co droga kucia: strzeleckie - `Forge.Smith` (jedyna legenda, prog z kusza wedlug luku), zbroja - `ArmourQuality`
   (gdy `CraftOn`), tarcza - pkt 3. Prog z pamieci podrecznej na kampanie (`Recipes.For` raz na sztuke); w ForgeView raz na kafelek, kowal z
   `ArmorItemVM.armorCrafting.Hero` (refleksja, bez twardej zaleznosci - brak Armoury = jak dotad).
- Wpiecia: `ArmouryBehavior.cs` (SyncData: migawka po `RangedLore.Import`; RegisterEvents: 2 nasluchy; OnSessionLaunched: `LorePurgeOnce`
  i `ReportLedger` tylko przy kluczu wylaczonym), `RangedLore.cs` (brama Seed, Teachable, unikaty, `OnCrafted(item, smith)`, granica losowania),
  `FletchForge.cs`, `Forge.cs` (kowal do `OnCrafted`), `ArmourQuality.cs` (1 linia - tarcza), `SmithMenu.cs` (1 linia - rozbiorka unikatu),
  `Settings.cs` + `McmSettings.cs` (gen_mcm), ForgeView `ArmourItemMixin.cs`, `TableauExtension.cs`, `ArmourListColour.cs`,
  `tools/sprawdz_logi.py` (grupa `m96wzory`).
- Linie logu: `Wiedza (m96-wzory): start sesji | statystyki ostateczne TAK | migawka tierow wczytanie|nowa kampania | tiery zmienione od migawki N
  (t1->wyzej M: ...) | <nowa ksiega - rozdanie ... | naprawa zapisu: zdjete N [...] | zapis juz naprawiony>, dopisane tieru 1 K [...] | klucze ... |
  potkniecia 0.`, potem linie ksiegi (BOWS...). Dalej: `losowanie wedlug reki - kowal X Smithing S, prog na tier 35 -> zakres tierow a-b`
  (pierwsze 20), `tarcza w CRAFT - odmowa ...` (20), `klik Craft bez kucia ... - bez nauki` (10), `nowa kampania - ksiega ... od zera (...)`.

**Oczekiwane liczby.**
- Zapis Jeffa (Seagard): `tiery zmienione od migawki 7 (t1->wyzej 4: ...)`, `naprawa zapisu: zdjete 4 [goldenheart_longbow t6, giant_bow t6,
  ravens_teeth_longbow t6, ravens_teeth_arrows t6], dopisane tieru 1 1 [<kropierz>]`, potem `BOWS: 15/60 patterns | t1 15/15 | t2 0/9 | t3 0/6 |
  t4 0/9 | t5 0/7 | t6 0/14` (16/60 i `t4 1/9`, jesli zapis ma juz GRE_vlandic_arrows z 14:25:48 - zapis wczytany o 14:35 go nie mial).
  Mianownik 60 zamiast 65: 5 unikatow ROT (goldenheart, giant_bow, weirwood, dragonbone, giant_arrows) wychodzi z ksiegi (pkt 4). BARDING `t1 28/28`.
- Drugie wczytanie: `zapis juz naprawiony (znacznik #m96seedfix1), dopisane tieru 1 0 []`.
- Nowa kampania: `rozdano wzory tieru 1 (578 sztuk)` (bylo 582) PO linii ROT-RBM, `BOWS: 15/60 | t1 15/15 | ... | t6 0/14`.
- Smithing 30, luk t1: `zakres tierow 1-2`. Tarcza t2 w CRAFT przy Smithing 30: odmowa (prog 35), nic nie schodzi.

**Znaleziska audytu - co zrobione, co nie i dlaczego.**
| Znalezisko | Stan |
|---|---|
| Wzory t1 rozdane przed ROT-RBM (krytyczne) | NAPRAWIONE - pkt 1a/1b (+ naprawa zapisu Jeffa) |
| Losowanie do tieru 6 po 3 robotach t1 (wazne) | NAPRAWIONE - pkt 2 |
| Kafelek mowi "PATTERN KNOWN", a kowadlo odmawia (wazne) | NAPRAWIONE - pkt 5 |
| Tarcze w CRAFT bez progu (wazne) | NAPRAWIONE - pkt 3 |
| Unikaty strzeleckie ROT w puli, imienne zbroje nie do nauczenia (wazne) | NAPRAWIONE - pkt 4 |
| Unikat z UniqueGear po przetopie kuty bez limitu | NIE ZMIENIAM - to regula Jeffa 30.08 ("moge kuc wiecej niz jeden, jak juz opanuje", `Forge.LegendAllowed`); pkt 4 rozciaga ja tylko na unikaty ROT |
| Przeciek wiedzy i legend przy nowej kampanii bez restartu gry | NAPRAWIONE - pkt 1d |
| Rozjazd w kropierzach (t1 27/28) | NAPRAWIONE - pkt 1c (dopisanie co sesje); ktory kropierz i czemu staje sie wzorem dopiero po wczytaniu - nazwa wyjdzie w linii "dopisane tieru 1" |
| Klik w zamkniety wzor daje punkty nauki (znalezione przy pracy, dowod z kodu) | NAPRAWIONE - pkt 1e |
| 6 kusz o mocy tieru 2-6 widac jako [I] i sa znane od startu | CZESCIOWO - kowadlo juz wymagalo progu wedlug luku tej sily (`XbowForgeEquivTier`), teraz kafelek mowi to wprost ("NEEDS N SMITHING"). Tieru WIEDZY kusz nie zmieniam: etykieta [I] (BkArmourList), filtr tierow ForgeView i ksiega licza tier gry - zmiana jednego bez reszty dalaby "t1 12/18" i kusze [I] pod filtrem IV; to osobny krok (wszystkie trzy razem) |
| Ravens' Teeth (ok. 5000 d) z kilku sztabek, bez limitu | ODLOZONE - to kwit materialow wobec ceny (temat ekonomii przepisu i `HistoricalPrices`, ktore przywraca ceny ROT-RBM), nie wiedzy; po naprawie wzor wymaga nauki (t6) i Smithing 165 |
| Czesci klingi valyrianskiej do miecza w parze nie sa ukryte | ODLOZONE - inny system (czesci broni gry / QohorMasters, nie ksiega RangedLore); w tym przebiegu niesprawdzone - bez dowodu nie ruszam |
| Uwaga: tekst audytu, ktory dostalem, byl uciety po 5. znalezisku; trzy ostatnie wiersze sa z jego podsumowania | - |

**Ryzyko / co sprawdzic (kontrola calosci).**
- Kto wola `Seed`/`KnownOf` miedzy wczytaniem a `OnAfterSessionLaunched`: tylko UI kuzni, menu kowala i nauka (grep: BkArmourList, FletchForge,
  Forge, SmithMenu, SmeltTab, ForgeView) - w tym czasie ekranow nie ma; w nowej kampanii do konca startu sesji ksiega jest pusta (nic nie znane).
  AI nie czyta RangedLore.
- Zapis: format bez zmian, znacznik `#m96seedfix1` czyta build 95 jako nieznane id (pomija). Wylacznik `LoreSeedAfterFinalStats` przywraca stara
  kolejnosc, ale zapisu juz naprawionego nie cofa (zdjete wzory nie wracaja) - to swiadome: byly bledne.
- Naprawa zdejmuje tylko z bloku rozdania; w starych kampaniach sprzed 02.09 (rozdanie, nauka, potem `__seed_ammo_v1`) wzor zdobyty wtedy, ktory
  przy wczytaniu jest tierem 2+, zostaje (warunek "byl tierem 1"); wyjatek - 7 id ROT-RBM zdobytych przed 02.09 (zbyt rzadkie, by liczyc).
- `PatternDrawBySkill`: wolniejsze odkrywanie wysokich tierow przy niskim Kowalstwie (zamierzone). Liczy sie kowal przy kowadle - towarzysz z 200
  odkrywa do tieru 6.
- `ShieldCraftSkillGate`: Jeff (Smithing 30) nie wykuje w CRAFT tarczy t2 (prog 35), choc zna worn_kite_shield - tak samo jak w naszym menu kowala.
  Brama dziala tylko przy `CraftOn` (ArmourCraftLikeWeapons i CraftingEnabled), jak brama zbroi.
- `ForgeUniquesByStudy`: rozbiorka unikatu ROT wymaga teraz Smithing 200 (bez spalenia), jak UniqueGear; przetop uczy jak dotad.
- ForgeView: nowy `TextWidget` w podgladzie 3D i druga plachta w wierszu listy - ten sam wzor wezlow co istniejace (bez SetAttribute).
- Watki: wszystko na watku glownym; `KnownOf` (goraca sciezka listy) dostal tylko odczyt klucza i HashSet - bez alokacji; petle po wszystkich
  przedmiotach raz na sesje.
- Scalenie 8 galezi m96: wpiecia jednolinijkowe w ArmouryBehavior (SyncData, RegisterEvents, OnSessionLaunched), FletchForge, Forge, ArmourQuality,
  SmithMenu; grupa logu `m96wzory`.
- Konfiguracja Jeffa: nowe klucze nie sa w jego Armoury.json - obowiazuja domyslne (TAK), nic do zmiany.

**Status.** DO SPRAWDZENIA (build Armoury i ForgeView kod 0; w grze nie testowane).

**Test w grze.** (1) Wczytac zapis Jeffa: linia `Wiedza (m96-wzory): start sesji ... naprawa zapisu: zdjete 4 [...]` i `BOWS: 15/60 ... t6 0/14`;
w kuzni CRAFT -> Bows: Goldenheart, Giant Bow, Ravens' Teeth szare "PATTERN NOT LEARNED"; mocne kusze bursztynowe "PATTERN KNOWN - NEEDS N SMITHING".
(2) Zapisac, wczytac jeszcze raz: `zapis juz naprawiony`. (3) Nowa kampania (najlepiej po poprzedniej bez restartu): linia `nowa kampania - ... od
zera`, `rozdano wzory tieru 1 (578 sztuk)` po linii ROT-RBM. (4) Wykuc luk t1 przy Smithing 30: `zakres tierow 1-2`. (5) Tarcza t2 w CRAFT: odmowa.
(6) Klik Craft na zamknietym pancerzu: `klik Craft bez kucia ... bez nauki`, bez linii ksiegi. (7) `python -I tools/sprawdz_logi.py --grupa
m96wzory <Armoury-*.log>`: W96-0..7 bez NIE.
