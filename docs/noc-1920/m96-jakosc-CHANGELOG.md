# m96-jakosc - legendarne i mistrzowskie rzadkie, limit swiata (20.10)

**Problem (Jeff 20.10, gra na WGRANIU 95):** "za duzo jest legendary przedmiotow, legendarne przedmioty to kilka sztuk na swiecie, masterwork juz wiecej itp.,
a w sklepach za latwo mozna znalezc legendary i masterwork". Chodzi o jakosc (modyfikatory gry: Legendary, Masterwork / Lordly, Fine / Balanced ...),
nie o legendy z imienia (te pilnuje LegendaryLaw).

**Dowod:**
- Zaden log nie liczyl jakosci (grep "legendary_|masterwork_|lordly_" w biegu 160 dob 12-25-19: jedno trafienie, nazwa przedmiotu). Liczby z innych linii:
- Jeff 20.10 (Armoury-2026-10-20_13-28-19.log, 14:14:07, dzien 108861): "Zakupy i nadwyzki AI (E8/Z): dodatnie stany: odrzucenia kandydatow 3505 w 166 wizytach,
  kupione dodatnie stany 0 szt. ... brak zostal przy polce ze sztuka nie dla AI: dodatni stan 2070 szt." - na polkach ok. 21 sztuk z dodatnim stanem na jedna wizyte
  zakupow, AI nie kupuje zadnej (AiSkipFineGoods - zbrojownia DTE to Dictionary<ItemObject, int>, jakosc by przepadla), wiec leza na zawsze.
- Ta sama doba: "Uzbrojenie wedlug zrodla (E6/0): zrodla: warsztaty 2030" sztuk na dobe; bieg 160 dob (12-25-19): warsztaty 1005/d, polki osad 209 638 szt.
- Wyliczenie z wag (ponizej): u Jeffa ok. 88 legendarnych i 177 mistrzowskich NA DOBE z samych warsztatow AI; w biegu 160 dob ok. 44 i 87 na dobe - tysiace
  legendarnych na polkach po kilku miesiacach. Dokladna liczba - pierwsza linia nowej paczki po wczytaniu ("spis przy wczytaniu" / "spis przed wyrownaniem").

**Przyczyna (dekompilacja + XML):**
- `ItemModifierGroup.GetRandomItemModifierProductionScoreBased` = losowanie wedlug `production_drop_score`. Native item_modifiers_groups.xml: brak modyfikatora 75;
  RBM (RBMCombat_item_modifiers.xml, scala sie z Native atrybut po atrybucie): legendary 5, masterwork/lordly 10, fine 15, inferior 5, poor 5 -> **legendarna 4.3%,
  mistrzowska 8.7%, dobra 13% kazdej sztuki**, bez wzgledu na tier i na to, kto kuje.
- **Jedyne realne zrodlo na polkach w zestawie Jeffa: Armoury `WorkshopLaw.cs:334`** (kazda gotowa sztuka warsztatu AI; WorkshopLaw.cs:344 to jedyne miejsce,
  gdzie gotowa sztuka z modyfikatorem trafia na polke - recenzja "gra" 20.10).
- Pozostale miejsca, ktore tak losuja (sprawdzone w recenzjach):
  - gra `WorkshopsCampaignBehavior.GetRandomItemAux` (warsztaty gracza, linie gry; `CraftRealm.FilterRandom` losuje tak samo) - w zestawie Jeffa jakosc NIE dochodzi
    na polke: BK `EconomyPatches.ProduceOutputPrefix` (latka na ProduceAnOutputToTown, `return false`) kladzie sztuke `new EquipmentElement(item, null)`; rzut gry
    zmienia tylko cene: szacunek utargu cyklu (`GetItemsToProduce` -> `CanPlayer/NotableWorkshopProduceThisCycle`) i zaplate BK miasto -> warsztat
    (`basePrice = GetItemPrice(new EquipmentElement(outputItem))` - konstruktor kopiujacy, z modyfikatorem z rzutu gry) - miasto placilo 5x za zwykla sztuke;
  - lup gracza z poleglych (`DefaultBattleRewardModel.GetRandomItem`, wagi `loot_drop_score`: brak 45, legendary 2, masterwork 7, fine 10 - legendarna 2.2%,
    mistrzowska 7.6%; AI dostaje tylko zloto, MapEvent:1862) - w bitwie polowej lup bierze Spoils of War (`SkipVanillaLootPatch` na DoLootInventory: "field battle
    ... mod will handle loot", `return false`; lup = kopia sprzetu trupa), rzut gry liczy sie tylko w oblezeniach, kryjowkach i na morzu;
  - `HeroGear.cs:315` i ROT `ROTGankBehavior.GetRandomItem` (zamiennik broni jenca), ROT `ROTEnlistmentBehavior.GetRandomItem` (nagroda zaciagu, wagi lupu);
  - RBMTournament `GivePrizeToWinnerPrefix` (nagroda turnieju gracza: kolejno legendary ok. 5%, masterwork ok. 10%, fine ok. 15%, z komunikatem "you successfully
    rolled for ... item modifier"); gdy sztuki nie ma na polce, `PlayerFees8.SettlePrize` przy TourneyPrizeCommissioned (u Jeffa TAK) zostawia ja graczowi, a miasto
    placi rzemieslnikom cene ZWYKLEJ (`MarketData.GetPrice(el.Item)`).
- Bez jakosci: TownFletchers, ColdStart, warsztaty BK, zbrojownie DTE lordow i zalog (bez stanu), GarrisonCarts (dowoza do zbrojowni DTE - jakosc i tak przepada).
  Kuznia gracza (Forge.RollQuality - ta sama bramka tieru: legendarna od 5, mistrzowska od 4, Forge.cs:213-214; ArmourQuality, BK CraftingMixin) i ulepszenie
  u kupca ROT (gracz placi) - sprawa gracza, bez zmian.
- Miasto placi warsztatowi cene ZWYKLEJ sztuki (`WorkshopLaw.Revenue(town, ItemObject)`) - jakosc byla wartoscia z niczego.

**Zmiana (nowy plik `Armoury/src/QualityScarcity.cs`, klucz Quality Scarcity, domyslnie TAK):**
1. Powstanie sztuki: rzut gry Legendary/Masterwork przepada (sztuka zwykla) we wszystkich miejscach wyzej. WLASNY rzut tylko tam, gdzie sztuke ktos naprawde robi
   i na pewno ja ktos dostaje: warsztaty AI (WorkshopLaw), zamiennik broni jenca ROT (HeroGear, sztuka podana jencowi), nagroda turnieju zamowiona u rzemieslnikow
   (PlayerFees8). Legendarna tylko z tieru 5-6 (Quality Legendary Chance 0.001 = 1 na 1000 takich sztuk), mistrzowska z tieru 4-6 (Quality Masterwork Chance
   0.004 = 1 na 250), i tylko gdy swiat ma mniej niz limit: **legendarne 6, mistrzowskie 60** (Quality Legendary/Masterwork World Cap). Droga gry, lup i nagroda
   zaciagu - tylko odbior rzutu gry, bez wlasnego rzutu. Fine, slabe, zepsute - jak dzis; fuszerka nie staje sie arcydzielem; stany bez etykiety (konie, zuzycie,
   lup Spoils) bez zmian.
2. Spis raz na dobe (i przy wczytaniu): polki wszystkich osad, juki partii, rynsztunek bohaterow (bojowy, cywilny, skradania), wozy kupcow w drodze (TradeCarts),
   gracz (sakwy, schowek, zbrojownia DTE wojska gracza, partie i bohaterowie rodu; zuzyta legendarna/mistrzowska rzecz gracza liczy sie wedlug oryginalu z ksiegi
   zuzycia - GoodOriginal). Limit przy powstaniu = spis + nowe od spisu. **Bez stanu w zapisie** - zapis Jeffa wczytuje sie bez zmian, cofniecie DLL tez.
3. Nadmiar (stary swiat Jeffa): przy rozliczeniu doby, TYLKO na polkach osad: sztuki ponad limit (limit minus to, co trzymaja juki, bohaterowie, wozy i gracz) schodza
   o stopien - legendarna -> mistrzowska (gdy jest miejsce w limicie mistrzowskich), inaczej -> dobra; mistrzowska -> dobra (grupa bez Fine -> zwykla). Pociski
   i bron miotana: cel o liczbie w stosie nie wiekszej niz zrodlo (RBM: legendarne strzaly maja stos +0, "large bag" dobra +15 - schodza do zwyklych, nie do duzego
   kolczana). Zostaja najdrozsze. Ta sama liczba sztuk na tej samej polce, zloto nie rusza (spada tylko wartosc towaru - linia podaje ile). Nigdy: sakwy, schowek
   i bohaterowie gracza, bron wykuta przez gracza (IsCraftedByPlayer).
- Nagroda turnieju zamowiona: gdy regula zmieni jakosc z rzutu RBM, gracz dostaje komunikat (EN), ze rzemieslnicy miasta zrobili zwykla sztuke.
- Wpiecia: 1 linia w WorkshopLaw.cs, 2 w HeroGear.cs, 1 w PlayerFees8.cs (sciezka TourneyPrizeCommissioned), 2 w SubModuleMain.cs (ApplyAll po ArmourQuality,
  AddBehavior po HeroGearBehavior); latki Postfix Priority.Last przez AccessTools.DeclaredMethod: WorkshopsCampaignBehavior.GetRandomItemAux (po WorkshopLaw
  .RandomItemPostfix i CraftRealm), DefaultBattleRewardModel.GetLootedItemFromTroop (po BK LootScale), ROTEnlistmentBehavior.GetRandomItem(int, Settlement).
  Settings.cs + gen_mcm (RANGES).
- Linie: "Jakosc przedmiotow (m96jakosc): wpiete ..." (menu glowne), "... start sesji | ... spis przy wczytaniu", "... dzien N | regula, limit, spis przed
  wyrownaniem, swiat po (polki/juki/bohaterowie/gracz/wozy), dobre/slabe/zepsute, zwykle uzbrojenie na polkach, nowe od wczoraj wedlug zrodla, rzut gry odebrany,
  zatrzymane limitem, obnizki na polkach (+ pominiete pociski, nieudane zdjecie), wartosc, wyroby gracza (legendarne, mistrzowskie), zuzyte rzeczy gracza wedlug
  ksiegi, nagrody turnieju po regule, koszt, potkniecia". Grupa `python -I tools/sprawdz_logi.py --grupa m96jakosc <log>` (JQ-0..JQ-9).

**Poprawki po recenzjach (20.10, recenzje "gra" i "technika" - same drobne; krytycznych i waznych brak):**
- (gra 1, technika 1) Wlasny rzut na drodze gry, w lupie i w pierwszym przebiegu jenca ROT robil sztuki-widma: BK kladzie wyrob drogi gry bez modyfikatora, Spoils
  of War bierze lup z trupa, MapEvent odrzuca lup tego samego typu, HeroGear podmienia bron jenca na zamiennik - a licznik "nowe" i rezerwa limitu rosly. PRAWDA ->
  `Filter` (postfiksy) tylko odbiera rzut gry; wlasny rzut zostal w WorkshopLaw, w zamienniku jenca i w nagrodzie zamowionej. Nagroda zaciagu ROT tez tylko odbior
  (wagi lupu; w symulowanej bitwie ekran lupu sie nie otwiera).
- (gra 2) Nagroda turnieju RBM poza regula (ok. 5% legendarna, 10% mistrzowska; przy zamowieniu u rzemieslnikow miasto placi cene zwyklej). PRAWDA -> 1 linia w
  PlayerFees8.SettlePrize (sciezka TourneyPrizeCommissioned) -> `QualityScarcity.Commissioned` (wlasny rzut warsztatow w limicie, podmiana w sakwach, komunikat).
  Sciezki "zdjeta z polki" (sztuka juz istniala) i "cena zamiast przedmiotu" bez zmian.
- (gra 3) Demote nie sprawdzal, czy zdjecie z polki sie udalo. PRAWDA (zabezpieczenie) -> `AddToCounts(el, -n) < 0` = nic nie dopisujemy, licznik "nieudane
  zdjecie z polki".
- (gra 4) Obnizka pociskow zmieniala liczbe w stosie (legendarne strzaly +0 -> large_bag +15). PRAWDA -> `Target`: cel o stosie <= zrodla, inaczej zwykla; zrodlo
  z ujemnym stosem bez celu - zostaje (licznik "pominiete"; w danych Native/RBM nie ma takiej legendarnej ani mistrzowskiej). Mistrzowskie toporki "large bag" (+3)
  schodza do zwyklych - stos mniejszy o 3, to jest obnizka jakosci (wartosc w linii), nie znikniecie sztuki.
- (gra 5, technika 4) Spis nie widzial: StealthEquipment, zbrojowni DTE gracza, wozow, zuzytej rzeczy gracza. PRAWDA -> dodane (wozy jako miejsce "wozy", DTE gracza
  i oryginal z ksiegi zuzycia jako "gracz"). Zostaje znana luka: rzeczy towarzyszy zdjete w niewoli RC (`_companionGear` - napis w zapisie RC) i GarrisonCarts
  (dowoza do zbrojowni DTE bez jakosci - AI i tak nie kupuje sztuk z dodatnim stanem).
- (gra 6, technika 2) JQ-2/JQ-3 dawaly falszywe NIE przy wyrobach gracza na polkach. PRAWDA -> linia podaje wyroby gracza osobno (legendarne, mistrzowskie), progi
  odejmuja je (i pociski pominiete) od polek; stara linia (tylko suma) - suma jako tolerancja. Sprawdzone na syntetycznym logu (m96-jakosc-tmp/synth2.log):
  dobra doba z wyrobami gracza - TAK, celowo zla doba (mistrzowskie na polkach ponad limit) - NIE.
- (technika 3) Dwa oczekiwania byly bledne: (a) "AI kupi wiecej z polek" - nie od razu: legendarne i mistrzowskie schodza do dobrych, a dobra (PriceMultiplier 1.5)
  to dalej dodatni stan, ktory AiSkipFineGoods odrzuca (AiBuyRules.cs:16, :105); spada tylko udzial dodatnich stanow w NOWEJ produkcji (z ok. 26% do ok. 13%);
  (b) "kolejne doby obnizki ~0" - nie: u Jeffa w wozach jedzie 8095 szt. uzbrojenia (13-28-19, 14:27:09, E6/0 "w drodze wozem (215) 8095"); wozy sa teraz w spisie
  (zajmuja limit, nowe sie nie rodza), ale po dostawie na polke stare legendarne/mistrzowskie schodza o stopien - obnizki przez kilka-kilkanascie dob, az wozy
  sie oproznia.
- (technika 1, dopisek) Odbior rzutu gry na drodze gry zmienia cene, nie polke: szacunek utargu cyklu gry spada (RBM ok. 1.32x -> 1.02x ceny zwyklej), wiec
  CanPlayer/NotableWorkshopProduceThisCycle rzadziej przepuszcza linie uzbrojenia, ktore przy zwyklej cenie sa nieoplacalne, a BK placi warsztatowi cene zwyklej
  sztuki (razy wlasny mnoznik jakosci BK) zamiast ceny z rzutu gry. Dotyczy warsztatow gracza z wsadem (linie notabli uzbrojenia ida przez WorkshopLaw). To
  zgodne z "cena wedlug jakosci" (sztuka na polce i tak jest zwykla), ale utarg warsztatow uzbrojenia gracza moze spasc - patrzec w wielkim tescie.

**Ryzyko / co sprawdzic (kontrola calosci):**
- Regresje: kto czyta modyfikatory sztuk z polek - AiBuyRules (dodatnie stany - legendarne i mistrzowskie schodza do dobrych, wiec "odrzucenia dodatnich stanow"
  w E8/Z w pierwszej dobie sie NIE zmienia; z czasem mniej, bo nowa produkcja ma o polowe mniej sztuk z dodatnim stanem), SellByCondition, Wyceny20, ShelfIndex
  (koszyk typ x tier - obnizka nie zmienia koszyka, tylko wersje polki; indeks przebudowuje sie przy zmianie VersionNo), GoodsLedger (liczy wedlug ItemObject -
  obnizka netto 0), TradeCarts (wozy wioza stan dalej; teraz w spisie jako "wozy", wyrownuja sie na polce celu).
- Kolizje: GetRandomItemAux ma juz postfiks WorkshopLaw (Priority normal) - nasz Last idzie po nim; GetLootedItemFromTroop ma postfiks BK (moze wyzerowac lup) -
  nasz po nim; ROTGankBehavior.GetRandomItem wola sam siebie - drugi przebieg tej samej sztuki nie odbiera jakosci danej przez regule (_lastItem). PlayerFees8:
  regula biegnie tylko na sciezce zamowienia, przed zaplata rzemieslnikom (cena liczona z ItemObject - bez zmian).
- Zamknieta ekonomia: przy powstaniu nic sie nie zmienia w zlocie (miasto i tak placi za zwykla). Obnizka na polce zmniejsza wartosc towaru bez przeplywu zlota.
  Stare sztuki leza dalej, dopoki ktos ich nie kupi (ludzie lordow i zalogi nie kupuja dodatnich stanow, lordowie z polek tylko unikaty) - 6 legendarnych i 60
  mistrzowskich po pierwszym wyrownaniu zajmuje limit na dlugo, wiec nowe prawie nie powstaja. **Kazda nowa legendarna u gracza** spoza reguly (kuznia, ulepszenie u kupca ROT,
  lup Spoils of War w stanie poleglego) przy pelnym swiecie zbije o polnocy jedna legendarna na polce (miasto traci wartosc towaru, zloto nie plynie) - to nie jest przypadek rzadki,
  tylko zasada "kilka sztuk na swiecie, razem z Twoimi". Sztuki sprzedane przez gracza z kuzni lukow/zbroi (nie IsCraftedByPlayer) moga zejsc o stopien na polce.
- Koszt: spis raz na dobe (osady ok. 2400, partie ok. 3300, bohaterowie x 3 komplety, linie wozow) - linia podaje ms; prog JQ-8 250 ms. Goraca sciezka (Roll) bez
  alokacji; spis i wyrownanie bez alokacji poza linia logu (GoodOriginal gracza - 12 slotow raz na dobe).

**Status:** DO SPRAWDZENIA (build Armoury kod wyjscia 0 po poprawkach; gry ani autotestu nie uruchamialem).

**Test w grze / autotest:** zapis Jeffa (save040) wczytac -> menu glowne: "wpiete" bez BRAK; "start sesji | ... spis przy wczytaniu: legendarne N (polki, juki,
bohaterowie, gracz, wozy)" (stan przed paczka). Po pierwszej polnocy linia "dzien N": "spis przed wyrownaniem" -> "swiat po: legendarne <= 6, mistrzowskie <= 60"
(ponad limit tylko poza polkami - wozy, gracz, bohaterowie - albo w wyrobach gracza) i obnizki w tysiacach. Kolejne doby: obnizki maleja przez kilka-kilkanascie dob
(wozy dowoza stare sztuki), "rzut gry odebrany" ok. 4.3% / 8.7% sztuk warsztatow, nowe legendarne 0-1 na kilka tygodni (tylko przy wolnym miejscu), mistrzowskie
ok. 1-2 na dobe do limitu. `--grupa m96jakosc`: JQ-1..JQ-5, JQ-7, JQ-8 TAK. W grze: sklepy kilku miast - legendarnych prawie brak, mistrzowskie pojedyncze; liczba
sztuk na polce ta sama; turniej wygrany przy braku nagrody na polce - nagroda zwykla z komunikatem (rzadko legendarna/mistrzowska z limitu).

**Dla Jeffa:** Warsztaty w miastach robily co 23. sztuke legendarna i co 12. mistrzowska (tak losuje gra z RBM), a lordowie takich nie kupuja - wiec lezaly
w sklepach tysiacami. Teraz legendarna moze wyjsc tylko z najlepszych wyrobow (tier 5-6) i na calym swiecie, razem z Twoimi rzeczami, jest ich najwyzej 6,
mistrzowskich najwyzej 60. Nadmiar, ktory juz lezy w sklepach, po polnocy spada o stopien (legendarna na dobra albo mistrzowska) - nic nie znika, przedmioty
zostaja na polkach; to, co jeszcze jedzie wozami kupcow, wyrowna sie po dowiezieniu. Twoje rzeczy, Twoich towarzyszy i to, co wykujesz sam, nie sa ruszane;
nagroda turniejowa, ktora miasto zamawia u rzemieslnikow, bedzie zwykle zwykla.
