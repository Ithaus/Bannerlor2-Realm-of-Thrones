## m96-wzory (20.10) - luki tieru 6 znane od poczatku gry i pokrewne bledy wiedzy kuzni (Armoury + ForgeView)

**Dla Jeffa (prosto).** Goldenheart, Giant Bow i luk oraz strzaly Ravens' Teeth byly "znane" od pierwszego dnia, bo kuznia rozdawala wzory
tieru 1 chwile przed tym, jak Realm of Thrones (przy RBM) podbija tym lukom naciag do tieru 6 - tak samo szesc mocnych kusz (do 300 funtow) bylo
"tierem I", choc kowadlo zada za nie Kowalstwa jak za luk tieru 2-6. Teraz wzory rozdaje sie po wszystkich zmianach, kusze licza sie wedlug sily
(kafelek [VI] przy 300-funtowej kuszy), a Twoj zapis naprawi sie sam przy wczytaniu: znikna te 4 luki i 6 kusz, a to, czego naprawde sie nauczyles,
zostaje. Luki z rzadkiego materialu (czardrzewo, smocza kosc, zlotoserce, luk olbrzyma, Ravens' Teeth) nie daja sie juz wykuc z drewna i kilku
sztabek - kafelek mowi "RARE STUFF - CANNOT BE FORGED" - a blizniacze klingi legend ("Dual" - Dark Sister, Oathkeeper, stal valyrianska) zniknely
z projektowania broni. Nowy wzor z nauki wypada najwyzej o tier ponad to, co Twoj kowal uniesie, tarcze w CRAFT wymagaja Kowalstwa, a kafelek mowi
bursztynem "PATTERN KNOWN - NEEDS 165 SMITHING", gdy wzor znasz, ale reka jeszcze nie siega.

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
- Kusze (po recenzjach): ten sam log, linia 338 `Kusze wedlug luku (XbowLikeBow): start sesji`: `crossbow_f ... t2->t6`, `crossbow_g/d/e ... t1->t6`,
  `crossbow_c ... t1->t5`, `crossbow_b ... t1->t3`, `crossbow_a ... t1->t2` (tier rownowazny luku tej samej sily; o ten tier kuznia pyta Smithing -
  `Recipes`: `ft = Math.Max(tier, XbowPrice.ForgeTier(item)); Need(ref r, ft, 10)`), a ksiega linia 496 `CROSSBOWS: 18/38 | t1 18/18` - szesc kusz
  mocy tieru 2-6 rozdanych jako tier 1.

**Przyczyna.** Kolejnosc: wzory tieru 1 rozdawane przed ostatecznymi statystykami lukow ROT-RBM; `LorePurgeOnce` (siatka bezpieczenstwa) tez
biegla przed ROT i jest jednorazowa. Dotyczy KAZDEJ kampanii zaczetej z RBM i integracja ROT-RBM. Autotesty tego nie widzialy, bo ksiega startowa
drukowala sie przed zmiana ROT. Kusze: ksiega liczyla tier gry (0-2), a kuznia tier wedlug sily - dwie miary jednego wzoru.

**Zmiana** (nowe pliki `Armoury/src/LoreSeedFix.cs`, `Armoury/src/PatternGate.cs`, `Armoury/src/LegendTwins.cs`; grupa MCM "Learning patterns",
8 kluczy, wszystkie domyslnie TAK; lista `RareBowsIds` tylko w ustawieniach):
1. `LoreSeedAfterFinalStats` - (a) `RangedLore.Seed` nic nie robi, dopoki statystyki tej kampanii nie sa ostateczne; rozdanie, `LorePurgeOnce`
   i linie ksiegi ida w `CampaignEvents.OnAfterSessionLaunchedEvent` (po OnSessionLaunched wszystkich modow, takze ROT). Po recenzjach: najpierw
   naprawa/rozdanie, POTEM sanacja (blednie rozdany wzor to nie przeciek - sanacja nie wyzeruje przez niego uczciwej wiedzy; po resecie znaczniki
   naprawy wracaja). (b) JEDNORAZOWA naprawa zapisu, znacznik `#m96seedfix1` w liscie Known (format `arm_rangedlore` "id|id;pkt" bez zmian; build 95
   znacznik pomija, bo `GetObject` = null). Zdejmowane jest tylko to, co dal blad: wzory z bloku rozdania (miedzy `#seeded2` a `__seed_ammo_v1` - tam
   stoi wylacznie to, co dal Seed; wszystko zdobyte potem stoi ZA `__seed_ammo_v1`), ktore sa dzis tierem 2+, a przy wczytaniu byly tierem 1 (migawka
   tierow GRY w SyncData, przed ROT) albo sa na liscie ROT-RBM (7 id, zapas). Po recenzjach (luka zapisow sprzed 02.09): takze ciagly ciag amunicji
   tuz ZA `__seed_ammo_v1` - tam migracja amunicji dopisala kolczany tieru 1 (w tym ravens_teeth_arrows); w zapisach od 02.09 ten ciag to amunicja
   zdobyta pozniej, a ta nie byla tierem 1 w migawce (tier 1 rozdawano w calosci), wiec warunek jej nie zdejmie. Opanowane unikaty (UniqueLore)
   nie sa ruszane. (c) Co sesje dopisanie brakujacych wzorow tieru 1 po ostatecznych statystykach (wzor, ktory stal sie wzorem do nauki po rozdaniu).
   (d) Nowa kampania w tym samym procesie gry: Known, punkty nauki, `UniqueLore` i `Legends` od zera (`OnNewGameCreatedEvent`; statyczne listy
   przeciekaly - SyncData przy nowej grze nie biegnie). (e) Klik Craft bez kucia nie uczy: `FletchForge.BkCraftPostfix` czyta `__runOriginal`.
   Mechanizm starego bledu (poprawiony po recenzji): Harmony 2.4.2 (`MethodCreator.AddPrefixes`: `Ldloc runOriginal, Brfalse` przed wywolaniem)
   POMIJA prefiks zwracajacy bool, gdy wczesniejszy prefiks zwrocil false - po odmowie `FletchForge.BkCraftPrefix` (Normal) prefiks
   `ArmourQuality.CraftPrefix` (Priority.Low) w ogole nie biegl (jego `if (!__runOriginal) return true;` to zapas), `Refused` zostawal z
   POPRZEDNIEGO kliku (zwykle false) i postfiks dawal punkty nauki i okno wyniku za klik w zamkniety wzor. Postfiksy `__runOriginal` dostaja.
2. `PatternDrawBySkill` - gorna granica losowania nowego wzoru = Smithing kowala / `SmithingSkillPerTier` + 2 (Smithing 30 przy progu 35: tier 2;
   35: 3; 140: 6), nigdy ponizej tieru wykutej sztuki; legenda do puli dopiero, gdy kowalowi brakuje najwyzej jednego progu do
   `LegendarySkillNeeded`. Kowal = ten przy kowadle (`Forge.Smith` - `hero`; CRAFT BK - `CurrentCraftingHero`). Bylo: od tieru sztuki do 6 z rowna
   szansa (`RangedLore.cs` 386) - log 13-28-19 linia 6239: `odkryto losowo GRE_vlandic_arrows (BOWS, t4, zakres 1-6)` po 3 lukach t1. Tier
   porownywany z granica to tier WZORU (pkt 6 - kusza wedlug sily), wiec przy Smithing 30 nauka na kuszy nie da juz crossbow_f (sila t6, prog 165).
3. `ShieldCraftSkillGate` - tarcza w CRAFT BK: `ArmourQuality.CraftPrefix` (przy `CraftOn`) pyta `PatternGate.ShieldCraftPrefix`: Smithing kowala
   < `Recipes.For(tarcza).SkillNeeded` (Need(tier, 0): t2 35 ... t6 175 - ten sam prog co nasze menu kowala `Forge.Begin`) = odmowa, nic nie
   schodzi, `ArmourQuality.Refused` (bez nauki i okna). Kosci BK (pekniecie) bez zmian. Po recenzji: pamiec progu (`PatternGate.NeedOf`) liczona od
   nowa takze po zmianie w MCM `SmithingSkillPerTier`, `LegendarySkillNeeded` albo `XbowForgeEquivTier` (dotad tylko przy nowej sesji).
4. `ForgeUniquesByStudy` - przedmiot z `RotUniques` w klasie nauki (luki, strzaly, zbroje, tarcze, kropierze) jest unikatem kuzni jak `UniqueGear`:
   `Teachable` = false (nie rozdaje sie, nie losuje, nie liczy w ksiedze), `KnownOf` / `CanLearnFrom` / `Learn` ida przez `UniqueLore`
   (przetop albo rozbiorka zdobytego oryginalu; rozbiorka unikatu wymaga Smithing 200 i nie pali sztuki - regula Jeffa 31.08, `SmithMenu`),
   `FletchForge.BkCraftPrefix` mowi "seize the original", po recenzji tak samo podpowiedz naszego menu kowala (`SmithMenu.ShowItems`: unikat nieznany -
   "A famed piece - seize the original...", legenda opanowana w UniqueLore - "A LEGEND you have mastered - you may forge it again", bez "Only one may
   ever exist"). Imienne zbroje spoza listy `UniqueGear` (lyanna_armor, loras_*, mace_*, gold_cloak_*, blackfish_*...) przestaja byc zamkniete na zawsze.
5. `PatternTileShowsSkill` (Armoury `PatternGate.BlockText` + ForgeView) - trzeci stan kafelka w CRAFT: znany wzor, ktorego kowal z ekranu jeszcze nie
   moze zaczac - bursztynowy napis "PATTERN KNOWN - NEEDS 165 SMITHING" albo "PATTERN KNOWN - LEGEND ALREADY FORGED" i lzejsza plachta (0.32) na
   wierszu listy; po recenzjach takze "RARE STUFF - CANNOT BE FORGED" (pkt 7) - ten napis rowniez przy nieznanym wzorze, zamiast czerwonego
   "PATTERN NOT LEARNED" (nauka go nie otworzy). Po recenzji: wynik w pamieci na pare kowal + jego Smithing - inny kowal w ekranie albo przekroczony
   prog w tej samej wizycie = liczone od nowa przy nastepnym odczycie (lista BK nie powstaje od nowa przy zmianie kowala: `CraftingVM.UpdateCraftingHero`
   odswieza tylko projektowanie, uszlachetnianie i przetop). Gauntlet czyta wlasciwosc przy wiazaniu: po przebudowie listy (filtr, odswiezenie BK)
   albo wyborze kafelka - kafelek juz narysowany zmienia sie dopiero wtedy (bez nowej latki na `UpdateCraftingHero`). Bramy kucia zawsze czytaja
   aktualnego kowala. Po recenzji (technika): opanowany unikat strzelecki nie ma juz drugiego wiersza (InjectMasterworks pomija wyroby z InjectRanged).
6. (po recenzjach) `XbowPatternTierByPower` - tier WZORU kuszy = tier, o ktory kuznia pyta Smithing (`PatternGate.LoreTier` = max(tier gry,
   `XbowPrice.ForgeTier`); reszta przedmiotow - tier gry jak dotad). Jedna miara dla: rozdania (Seed, TopUp), naprawy zapisu, losowania (granica z
   pkt 2), punktow nauki i ksiegi (`RangedLore.TierOf`), etykiety [I]..[VI] na liscie CRAFT (`BkArmourList`), filtra tierow ForgeView (delegat do
   `RangedLore.TierOf`, bez alokacji) i naszego menu kowala (`SmithMenu` - etykieta "(tier N)", filtr, liczniki). Metal i materialy kuszy - tier gry
   jak dotad (XbowForgeEquivTier). Naprawa zapisu kusz - osobny jednorazowy znacznik `#m96xbowfix1`, tylko gdy przeliczenie kusz (XbowPrice) obowiazuje
   w tej sesji: zdejmuje kusze tieru GRY 1 z tierem wzoru 2+ (kazda kusza tieru gry 1 byla rozdana przez stary Seed, ktory dawal WSZYSTKIE wzory
   tieru 1 - uczciwie wylosowanej nie ma, bo byla juz znana). Migawka i licznik "tiery zmienione od migawki" licza teraz tier GRY (Recipes.Grade) -
   tier wzoru kuszy zalezy od XbowPrice, liczonego dopiero w OnSessionLaunched.
7. (po recenzjach) `RareBowsNotForged` + lista `RareBowsIds` (dragonbone_bow, weirwood_bow, ravens_teeth_longbow, goldenheart_longbow, giant_bow,
   ravens_teeth_arrows) - luki z rzadkiego materialu sa wycenione jak skonczony zasob (HistExoticBows: log 13-28-19 linia 342
   `ravens_teeth_longbow 6 -> 5000, goldenheart_longbow 24 -> 3000 ...`), a kwit kuzni to drewno, kilka sztabek i len (`Recipes`, luk: 75% wagi
   w drewnie, 25% w metalu, x2 przy t5-6) - kuznia robila 5000 d "z niczego". Teraz: `Teachable` = false (nie ma wzoru, nie rozdaje sie, nie losuje,
   nie liczy w ksiedze ani w licznikach menu), `CanLearnFrom` / `Learn` = false (przetop daje tylko punkty nauki - `Study`; rozbiorki nie ma w liscie),
   `Forge.LegendAllowed` (wspolna brama `Forge.Begin` i `Forge.Smith`) odmawia napisem "No smith can make this any more - the stuff it is made of
   cannot be had...", `FletchForge.BkCraftPrefix` tak samo przed pytaniem o wzor, menu kowala - szary wiersz z tym powodem. Qarth longbow zostaje
   (w HistExoticBows tylko z powodu ceny wedlug sily). Sztuki w swiecie zostaja.
8. (po recenzjach) `LegendTwinPiecesHidden` (`LegendTwins.Hide`, wpiecie 1 linia w `LegendaryLaw.OnSession` zaraz po `LockLegendPieces`) - czesc,
   ktorej nie uzywa zaden przedmiot, a ma TE SAMA SIATKE co dedykowana (ukryta) czesc legendy, jest ukrywana w projektowaniu broni jak sama czesc
   legendy. Czesci uzywane przez zwykla bron zostaja.
- Wpiecia: `ArmouryBehavior.cs` (SyncData: migawka po `RangedLore.Import`; RegisterEvents: 2 nasluchy; OnSessionLaunched: `LorePurgeOnce`
  i `ReportLedger` tylko przy kluczu wylaczonym), `RangedLore.cs` (brama Seed, Teachable, unikaty, rzadki material, `TierOf` -> `PatternGate.LoreTier`,
  `OnCrafted(item, smith)`, granica losowania), `FletchForge.cs`, `Forge.cs` (kowal do `OnCrafted`, rzadki material w `LegendAllowed`),
  `ArmourQuality.cs` (1 linia - tarcza, komentarz o Harmony), `SmithMenu.cs` (rozbiorka unikatu, podpowiedzi), `BkArmourList.cs` (etykieta - tier wzoru),
  `LegendaryLaw.cs` (1 linia), `Settings.cs` + `McmSettings.cs` (gen_mcm), ForgeView `ArmourItemMixin.cs`, `TableauExtension.cs`, `ArmourListColour.cs`,
  `ArmourFilterMixin.cs`, `tools/sprawdz_logi.py` (grupa `m96wzory`).
- Linie logu: `Wiedza (m96-wzory): start sesji | statystyki ostateczne TAK | migawka tierow wczytanie|nowa kampania | tiery zmienione od migawki N
  (t1->wyzej M: ...) | kusze wedlug sily: tier wzoru ponad tier gry N (z tieru 1 M: ...) | <nowa ksiega - rozdanie ... (N wzorow tieru 1), kusze wedlug
  sily: w rozdaniu | naprawa zapisu: zdjete N [...], kusze wedlug sily: zdjete N [...] | zapis juz naprawiony (...), kusze wedlug sily: juz naprawione
  (...)>, dopisane tieru 1 K [...] | klucze ... (w tym "kusza wedlug sily w ksiedze TAK", "rzadki material bez kucia TAK (6 z 6 id w grze)") |
  potkniecia 0.`, potem linie ksiegi (BOWS...). `Czesci-blizniaki legend (m96-wzory): ukryte w projektowaniu broni N (domyslnie otwartych M) - ...: [...]
  | siatek czesci legend S, czesci zwyklej broni C | potkniecia 0.` (OnSessionLaunched). Dalej: `losowanie wedlug reki - kowal X Smithing S, prog na
  tier 35 -> zakres tierow a-b` (pierwsze 20), `tarcza w CRAFT - odmowa ...` (20), `klik Craft bez kucia ... - bez nauki` (10), `nowa kampania - ksiega
  ... od zera (...)`. ForgeView: `Armoury RangedLore (filtr tierow - m96-wzory): PODPIETY`, `Armoury PatternGate (kafelek ...): PODPIETY`.

**Oczekiwane liczby.**
- Zapis Jeffa (Seagard), pierwsze wczytanie: `tiery zmienione od migawki 7 (t1->wyzej 4: ...)`, `kusze wedlug sily: tier wzoru ponad tier gry 7
  (z tieru 1 6: ...)`, `naprawa zapisu: zdjete 4 [ravens_teeth_arrows t6, ravens_teeth_longbow t6, giant_bow t6, goldenheart_longbow t6], kusze wedlug
  sily: zdjete 6 [crossbow_a t2, crossbow_b t3, crossbow_c t5, crossbow_e t6, crossbow_d t6, crossbow_g t6], dopisane tieru 1 0 []` (nazwy w kolejnosci
  odwrotnej do zapisu - kolejnosc w nawiasach dowolna). Potem `BOWS: 15/58 patterns | t1 15/15 | t2 0/9 | t3 0/6 | t4 0/9 | t5 0/7 | t6 0/12` (16/58 i `t4 1/9`, jesli zapis ma juz
  GRE_vlandic_arrows z 14:25:48). Mianownik 58 zamiast 65: 5 unikatow ROT (goldenheart, giant_bow, weirwood, dragonbone, giant_arrows - pkt 4)
  i 2 rzadkie nie-unikaty (ravens_teeth_longbow, ravens_teeth_arrows - pkt 7). `CROSSBOWS: 12/38 patterns | t1 12/12 | t2 0/3 | t3 0/3 | t4 0/4 |
  t5 0/3 | t6 0/13`. `BARDING: 27/103 | t1 27/27` (POPRAWKA po recenzji: rozjazd 27/28 z sesji 13-28-19 to nie zmiana statystyk, tylko
  `UniqueSpoils._common` - unikaty ROT noszone przez wiecej niz 3 postaci (rot_horse_armor19, white_walker_saddle; unikaty.log 13:31:32) nie dostaja
  NotMerchandise i w pozniejszych sesjach staja sie Teachable; przy ForgeUniquesByStudy TAK sa StudyOnly (surowa lista RotUniques.Ids), wiec
  wychodza z ksiegi i nic sie nie dopisuje. Przy ForgeUniquesByStudy NIE TopUpT1 rozda taki wspolny unikat tieru 1 jako zwykly wzor.)
- Drugie wczytanie: `zapis juz naprawiony (znacznik #m96seedfix1), kusze wedlug sily: juz naprawione (znacznik #m96xbowfix1), dopisane tieru 1 0 []`.
- Nowa kampania: `nowa kampania - ... od zera`, potem PO linii ROT-RBM `rozdano wzory tieru 1 (572 sztuk)` (582 - 4 luki/strzaly ROT-RBM - 6 kusz;
  plus/minus wspolne unikaty ROT tieru 1, jesli UniqueSpoils uzna jakis za wspolny juz przy starcie), `nowa ksiega - rozdanie ... (572 wzorow tieru 1),
  kusze wedlug sily: w rozdaniu`, `BOWS: 15/58 | t1 15/15 | ... | t6 0/12`, `CROSSBOWS: 12/38 | t1 12/12 | ... | t6 0/13`.
- `Czesci-blizniaki legend`: ok. 21 ukrytych, ok. 19 domyslnie otwartych (przeglad XML: bolton_blade_duel, darksister_blade_duel, forlorn_blade_duel,
  forlorn_blade2_duel, lamentation_blade_duel, lightbringer_blade_duel, needle_blade_duel, oathkeeper_blade_duel, red_rain_blade_duel, renly_blade_duel,
  skull_blade_duel, truth_blade_duel, tyrell_blade_duel, val_blue_blade_duel, val_red_blade_duel, valsteelblade_1_duel, valsteelblade_2_duel,
  valyrian_short_blade_duel, vigilance_blade_duel, widowswail_blade_duel, ww2_blade_duel); w grze decyduje zbior legend LegendaryLaw (41 broni), wiec
  liczba moze byc mniejsza - valyrian_short_blade_duel i darksister_blade_duel musza byc na liscie. "129 dedykowanych czesci legend" bez zmian.
- Smithing 30, luk t1: `zakres tierow 1-2`. Tarcza t2 w CRAFT przy Smithing 30: odmowa (prog 35), nic nie schodzi.

**Znaleziska audytu i recenzji - co zrobione, co nie i dlaczego.**
| Znalezisko | Stan |
|---|---|
| Wzory t1 rozdane przed ROT-RBM (krytyczne) | NAPRAWIONE - pkt 1a/1b (+ naprawa zapisu Jeffa) |
| Losowanie do tieru 6 po 3 robotach t1 (wazne) | NAPRAWIONE - pkt 2 (po recenzji takze kusze wedlug sily - pkt 6) |
| Kafelek mowi "PATTERN KNOWN", a kowadlo odmawia (wazne) | NAPRAWIONE - pkt 5 (po recenzji: przelicza sie przy innym kowalu / Smithing) |
| Tarcze w CRAFT bez progu (wazne) | NAPRAWIONE - pkt 3 |
| Unikaty strzeleckie ROT w puli, imienne zbroje nie do nauczenia (wazne) | NAPRAWIONE - pkt 4 (wielkie luki dodatkowo pkt 7) |
| 6 kusz o mocy tieru 2-6 widac jako [I] i sa znane od startu (wazne; recenzja "gra" i "technika") | NAPRAWIONE po recenzjach - pkt 6. Wczesniejszy powod odlozenia (etykieta [I], filtr ForgeView i ksiega licza tier gry) usuniety: wszystkie trzy licza teraz ten sam tier wzoru |
| Ravens' Teeth (5000 d) z kilku sztabek, bez limitu (wazne; recenzja "technika") | NAPRAWIONE po recenzjach - pkt 7. Wczesniejszy powod ("temat ceny, nie wiedzy") nie trzymal sie zasady "nic z niczego": cena skonczonego zasobu przy kwicie z drewna i zelaza |
| Czesci klingi valyrianskiej do miecza w parze nie sa ukryte (wazne; recenzje) | NAPRAWIONE po recenzjach - pkt 8. Wczesniejszy powod ("niesprawdzone, bez dowodu") byl nieprawdziwy: dowod w ROT_crafting_pieces.xml:958 (`valyrian_short_blade_duel`, is_default=true), grep ROTassets.xml (zaden przedmiot), crafting_templates.xml:399 (OneHandedSwordDual), WeaponDesignVM (wszystkie szablony z CraftingTemplate.All, czesc widoczna przy !IsHiddenOnDesigner), CraftingCampaignBehavior.IsOpened (is_default = otwarta). Ten sam blad mialo 20 innych blizniakow legend |
| W96-2 w sprawdz_logi: falszywe NIE w kazdej nowej kampanii (recenzje, wazne) | NAPRAWIONE - W96-2 sprawdza, czy w oknie sesji linia rozdania nie stoi PRZED linia ROT-RBM (bez RBM - nic do porownania); sprawdzone na logach syntetycznych: nowa kampania w dobrej kolejnosci TAK, stara kolejnosc NIE, zapis Jeffa TAK, logi recenzentow TAK |
| Rozjazd w kropierzach - zla przyczyna i liczby (drobne) | POPRAWIONE w tym wpisie (Oczekiwane liczby) |
| Kafelek liczony raz na zawsze, nie po zmianie kowala (drobne) | NAPRAWIONE - pkt 5 |
| Menu kowala: "the craft itself will teach you" dla unikatow, "Only one may ever exist" po opanowaniu (drobne) | NAPRAWIONE - pkt 4 |
| Zly opis mechanizmu kliku Craft (drobne) | POPRAWIONE - pkt 1e, komentarze w FletchForge.cs i ArmourQuality.cs |
| Zdublowane wiersze unikatow strzeleckich po nauce (drobne, technika) | NAPRAWIONE - pkt 5 (InjectMasterworks) |
| Pamiec progu tarcz na cala kampanie (drobne, technika) | NAPRAWIONE - pkt 3 (stempel ustawien) |
| Luka naprawy w zapisach sprzed 02.09 (drobne, technika) | NAPRAWIONE - pkt 1b (ciag amunicji za `__seed_ammo_v1`) |
| Unikat z UniqueGear po przetopie kuty bez limitu | NIE ZMIENIAM - regula Jeffa 30.08 ("moge kuc wiecej niz jeden, jak juz opanuje", `Forge.LegendAllowed`); materialy zwykle (stal, sukno), koszt legendarny zostaje |
| Przeciek wiedzy i legend przy nowej kampanii bez restartu gry | NAPRAWIONE - pkt 1d |
| Klik w zamkniety wzor daje punkty nauki (znalezione przy pracy, dowod z kodu) | NAPRAWIONE - pkt 1e |
| Drobne znaleziska audytu nr 9 i 12 (recenzja "technika": "brak w CHANGELOG") | NIE ZNAM TRESCI - tekst audytu, ktory dostalem (dwa razy), byl uciety po 5. znalezisku; tresci punktow 9-12 nie ma tez w plikach roboczych. Zaden z nich nie zostal wiec ani naprawiony, ani odrzucony - do sprawdzenia przy scaleniu, jesli audyt jest gdzies zapisany |

**Ryzyko / co sprawdzic (kontrola calosci).**
- Kto wola `Seed`/`KnownOf` miedzy wczytaniem a `OnAfterSessionLaunched`: tylko UI kuzni, menu kowala i nauka (grep: BkArmourList, FletchForge,
  Forge, SmithMenu, SmeltTab, ForgeView) - w tym czasie ekranow nie ma; w nowej kampanii do konca startu sesji ksiega jest pusta. AI nie czyta RangedLore.
- Zapis: format bez zmian, znaczniki `#m96seedfix1` i `#m96xbowfix1` build 95 czyta jako nieznane id (pomija). Wylaczniki nie cofaja juz naprawionego
  zapisu (zdjete wzory nie wracaja) - swiadomie: byly bledne. Wyjatek przez TopUpT1: po wylaczeniu `XbowPatternTierByPower` kusze tieru gry 1 wracaja
  do ksiegi przy nastepnej sesji; po ponownym wlaczeniu (znacznik juz jest) zostaja znane - przelaczanie klucza w trakcie kampanii jest jednokierunkowe.
- Naprawa zdejmuje tylko z bloku rozdania (i ciagu amunicji za znacznikiem w starych zapisach); w kampaniach sprzed 02.09 wzor zdobyty wtedy, ktory
  przy wczytaniu jest tierem 2+, zostaje (warunek "byl tierem 1"); wyjatek - 7 id ROT-RBM zdobytych przed 02.09 (zbyt rzadkie, by liczyc).
- Sanacja po naprawie: w mlodej kampanii z niezapisanym jeszcze `arm_lore_purge_v1` blednie rozdane wzory nie wywolaja juz resetu calej ksiegi.
- `XbowPatternTierByPower`: zalezy od `XbowPrice` (RBM + ceny historyczne + `XbowForgeEquivTier`); bez przeliczenia - tier gry jak dotad (linia startu:
  "nieczynne"), a naprawa kusz czeka na sesje z przeliczeniem. Kusza wedlug sily daje teraz tyle punktow nauki co luk tej sily (crossbow_g: 6 zamiast 1)
  i przetop takiej kuszy odkrywa do jej tieru wzoru - spojne z progiem i stamina, ktorych kuznia za nia zada. Etykieta [VI] przy kuszy, ktorej
  metal to sztabki niskiego tieru (metal - tier gry, jak dotad) - etykieta mowi o wzorze i progu, kwit o materiale.
- `RareBowsNotForged`: Goldenheart, Giant Bow, Weirwood i Dragonbone (unikaty) nie daja sie wykuc nawet po opanowaniu oryginalu (dotad - po nauce
  z oryginalu bez limitu, za legendarny kwit ze zwyklej stali); zdobyte sztuki zostaja i mozna je nosic, przetopic na punkty. Nie ruszam produkcji
  miast (TownFletchers bierze kandydatow spoza NotMerchandise - strzaly Ravens' Teeth jako towar miasta nie sprawdzone w tym przebiegu: w logu Jeffa
  linia startu strzelarzy ich nie wymienia).
- `LegendTwinPiecesHidden`: tylko czesci bez zadnego przedmiotu i z siatka czesci legendy; zwykla bron i czesci wspolne bez zmian. Zlecenia kowalskie
  i projekty BK, gdyby siegaly po ukryte czesci - ten sam stan co dla 129 czesci ukrywanych od dawna przez LockLegendPieces.
- `PatternDrawBySkill`: wolniejsze odkrywanie wysokich tierow przy niskim Kowalstwie (zamierzone). Liczy sie kowal przy kowadle - towarzysz z 200
  odkrywa do tieru 6.
- `ShieldCraftSkillGate`: Jeff (Smithing 30) nie wykuje w CRAFT tarczy t2 (prog 35), choc zna worn_kite_shield - tak samo jak w naszym menu kowala.
  Brama dziala tylko przy `CraftOn` (ArmourCraftLikeWeapons i CraftingEnabled), jak brama zbroi.
- `ForgeUniquesByStudy`: rozbiorka unikatu ROT wymaga teraz Smithing 200 (bez spalenia), jak UniqueGear; przetop uczy jak dotad.
- ForgeView: nowe wlasciwosci `FvBlockShown`, `FvNotLearned` (tylko widocznosc napisow w podgladzie 3D), plachty listy jak dotad; filtr tierow przez
  delegat (brak Armoury = tier gry). Kafelek juz narysowany po zmianie kowala odswieza sie dopiero przy przebudowie listy albo wyborze kafelka.
- Watki: wszystko na watku glownym; `KnownOf` (goraca sciezka listy) bez nowych alokacji; `TierOf` dla kusz - odczyt slownika XbowPrice pod blokada,
  dla reszty bez zmian; petle po wszystkich przedmiotach i czesciach raz na sesje.
- Scalenie 8 galezi m96: wpiecia jednolinijkowe w ArmouryBehavior (SyncData, RegisterEvents, OnSessionLaunched), FletchForge, Forge, ArmourQuality,
  SmithMenu, BkArmourList, LegendaryLaw; m96-czaskuzni tez zmienia SmithMenu.cs i ArmouryBehavior.cs (inne miejsca - do sprawdzenia przy scaleniu);
  grupa logu `m96wzory`.
- Konfiguracja Jeffa: nowe klucze nie sa w jego Armoury.json - obowiazuja domyslne (TAK), nic do zmiany.

**Status.** DO SPRAWDZENIA (build Armoury i ForgeView kod 0; w grze nie testowane).

**Test w grze.** (1) Wczytac zapis Jeffa: linia `Wiedza (m96-wzory): start sesji ... naprawa zapisu: zdjete 4 [...], kusze wedlug sily: zdjete 6 [...]`,
`BOWS: 15/58 ... t6 0/12`, `CROSSBOWS: 12/38 | t1 12/12 ...`; w kuzni CRAFT -> Bows: Goldenheart, Giant Bow, Ravens' Teeth bursztynowe "RARE STUFF -
CANNOT BE FORGED"; Crossbows: 300-funtowa kusza "[VI]", szara "PATTERN NOT LEARNED"; znane, za trudne wzory bursztynowe "PATTERN KNOWN - NEEDS N
SMITHING"; filtr VI pokazuje mocne kusze. W projektowaniu broni (szablon miecza w parze) nie ma klingi "Valyrian Short Sword Blade Dual" ani "Dark Sister
Blade Dual". (2) Zapisac, wczytac jeszcze raz: `zapis juz naprawiony ..., kusze wedlug sily: juz naprawione`. (3) Nowa kampania (najlepiej po poprzedniej
bez restartu): linia `nowa kampania - ... od zera`, `rozdano wzory tieru 1 (ok. 572 sztuk)` po linii ROT-RBM. (4) Wykuc luk t1 przy Smithing 30: `zakres
tierow 1-2`. (5) Tarcza t2 w CRAFT: odmowa. (6) Klik Craft na zamknietym pancerzu: `klik Craft bez kucia ... bez nauki`, bez linii ksiegi. (7) `python -I
tools/sprawdz_logi.py --grupa m96wzory <Armoury-*.log>`: W96-0..8 bez NIE.
