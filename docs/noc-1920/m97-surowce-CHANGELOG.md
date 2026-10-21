## m97-surowce (20.10) - czardrzewo i smocza kosc: surowce lukow z rzadkiego materialu, doplyw z konkretnych ziem (Armoury)

**Dla Jeffa (prosto).** Luk z czardrzewa i luk ze smoczej kosci znow da sie wykuc - ale tylko z prawdziwego surowca. W swiecie pojawily sie
dwa nowe towary: Weirwood (klepka czardrzewa, ok. 4000 d) - tylko we wsiach Wolnych Ludzi w lesie za Murem (Whitetree, Milkwater, Icecave,
Hauntwood, Walrus Bay, Myre), mniej wiecej jedna sztuka na 15 dni; i Dragonbone (kosc smoka, ok. 14000 d) - tylko w ruinach Valyrii (Usek, Esme,
Elyria) i na starych targach Volantis i Qarth, mniej wiecej jedna na 30 dni. Kupujesz je tam, gdzie sie pojawily (wiesniacy moga
zabrac sztuke na targ swojego miasta; karawany ich nie woza). Gra nie da ich za darmo w nagrodzie zadania ani w zdarzeniu "Trade Proposal".
Jeden luk bierze jedna cala sztuke; gdy luk peknie na kowadle, klepka przepada. Wzor dalej trzeba zdobyc
jak przy kazdym slawnym przedmiocie - z oryginalu. Bez surowca kowadlo mowi, czego brakuje i gdzie go szukac. Wylaczniki: "Rare Bows Need Material",
"Rare Material Supply" i "Rare Material Only From Source" (MCM, grupa Learning patterns).

**Problem.** Jeff 20.10 o wzorach lukow: "Luk z czardrzewa okay, trzeba miec czardrzewo, smoczej kosci - smocza kosc, ale Ravens Teeth
i Goldenheart to mozna, to zwykle luki. Pytanie, czy do lukow ze smoczej kosci i czardrzewa sa surowce ... aby dalo sie wszystko wykuc, nawet
jesli to bedzie trudne." Dzis (`RareBowsNotForged` + `RareBowsIds = "dragonbone_bow,weirwood_bow"`) te dwa luki sa zablokowane na zawsze
(`PatternGate.RareStuff`, napis "RARE STUFF - CANNOT BE FORGED").

**Rozpoznanie.**
- Surowca nie ma: grep `weirwood|dragonbone|dragon_bone` po ModuleData wszystkich modulow - tylko luki (`ROTassets.xml` 3067 `weirwood_bow`,
  3137 `dragonbone_bow`), napisy (`str_english.xml`) i scena Muru (`ROT_north.xml` `weirwood_the_wall`). Towary (Type="Goods") w XML: tylko
  SandBoxCore (welna, len, futro...), NavalDLC (`walrus_tusk`, `whale_oil`) i ROT (`poppy`, `dragon_thumbnail`); drewno, ruda i sztaby gra
  tworzy w kodzie (`DefaultItems`). Trzeba dodac dwa nowe towary.
- Kategoria: `ItemCategory` nie ma `Deserialize` (dekompilacja TaleWorlds.Core) - XML jej nie zdefiniuje. NavalDLC rejestruje swoje
  (`NavalItemCategories`: `RegisterPresumedObject` + `InitializeObject`) w `MBSubModuleBase.InitializeSubModuleGameObjects`; kolejnosc
  sprawdzona w `Campaign.InitializeDefaultCampaignObjects`: `InitializeDefaultGameObjects` (-> `GameManager.InitializeSubModuleGameObjects`),
  potem `LoadXML("Items")` - kategoria istnieje, zanim gra czyta `item_category` z naszego XML. Ta sama droga przy nowej grze i wczytaniu.
- Cena: `DefaultTradeItemPriceFactorModel.GetBasePriceFactor` - towar handlowy 0.1-10 x wartosci (przy pustym popycie 0.1), nie-towar
  0.8-1.3. Popyt: gra przy BaseDemand 0 bierze 1% dobrobytu; `BKEconomyModel.GetDailyDemandForCategory` dzieli przez `category.BaseDemand`
  (0 = NaN). Stad kategoria wlasna `arm_rare_stuff`: NIE towar handlowy (cena stabilna 0.8-1.3 x wartosci), popyt bazowy 0.02 (dodatni).
  Karawany gry i BK tej kategorii NIE kupuja (`CaravansCampaignBehavior` - tylko kategorie `IsTradeGood || IsAnimal`; `BKCaravansBehavior:1584` tak samo);
  wiesniacy (`VillagerCampaignBehavior.MoveItemsToVillagerParty`) biora kazdy przedmiot ze wsi na targ miasta (sprzedaz, gdy kiesa miasta starcza).
  Mieszczanie (ItemConsumptionBehavior.MakeConsumption) kupia ja rzadko - przy cenie 4000-14000 d i malym budzecie to jedna sztuka na kilkaset dni
  w miescie; wsie konsumpcji nie maja. Zaden warsztat (gra, BK, Armoury TownCrafts/WorkshopInputs - zakres kategorii) jej nie przerabia.
- Osady (ROT-Map settlements.xml): Wolni Ludzie (`Culture.freefolk`) - wsie za Murem: Whitetree (`castle_village_S5_2`, traperzy, w ksiazkach
  wioska z czardrzewem), Milkwater (`castle_village_S6_1`), Icecave (`village_S6_3`, drwale), Hauntwood (`castle_village_N6_2`), Walrus Bay i Myre
  (`ROT_castle45_village1/2`). Valyria (`Culture.valyrian`): Oros (`castle_K1`) z wsiami Usek i Esme (`castle_village_K1_1/2`), miasto Elyria
  (`ROT_town20`); Volantis (`town_ES2`), Qarth (`ROT_town35`). Lista id, nie kultura - kultura osady zmienia sie z wlascicielem (BK).
- Ceny lukow (log Jeffa 14-53-56, HistExoticBows): `weirwood_bow` 6000 d, `dragonbone_bow` 20000 d (oba unikaty ROT - `RotUniques`, wzor
  tylko z oryginalu przy `ForgeUniquesByStudy`). Luki waza 0.1 kg.
- Bezpieczenstwo zapisu: dodanie przedmiotu w XML do trwajacej kampanii - przedmioty powstaja z XML przy kazdym wczytaniu, zapis ich nie ma;
  kategoria zarejestrowana przed wczytaniem (jak NavalDLC). Pozniejsze usuniecie: `ItemRoster` przy wczytaniu wyrzuca przedmioty nie gotowe
  (`ItemRoster:524` `if (item != null && !item.IsReady)`), wiec sztuki w sakwach i na targach znikna; wpis kategorii w danych rynku miast
  (`TownMarketData._itemDict`) zostanie jako martwy klucz (ten sam stan co po wylaczeniu NavalDLC).

**Zasada.**
- Surowiec nie bierze sie z niczego: przychodzi z konkretnej ziemi (jak ruda z kopalni) - malo, rzadko, z pulapem. Raz na dobe los na region:
  czardrzewo 1/15, smocza kosc 1/30. Trafienie = 1 sztuka na targ jednej osady zrodla (wies spalona/grabiona i osada oblezona nie dostaja),
  ale tylko gdy zapas regionu (magazyny osad zrodla) jest ponizej pulapu (3 / 2), a zapas swiata (magazyny wszystkich osad i wszystkich partii,
  liczony tylko przy trafieniu i co 7 dob) ponizej pulapu swiata (10 / 6). Rok 364 dni: ok. 24 sztuki czardrzewa i 12 smoczej kosci, dopoki
  ktos je zabiera. Odplyw: kuznia gracza i rzadkie zakupy mieszczan. Los bez pamieci - wczytanie zapisu niczego nie gubi i nie podwaja.
  Zapas swiata liczy tez schowki osad (`Settlement.Stash`, jak ksiega towarow). Sztuka nie przyjedzie karawana - lezy tam, gdzie sie pojawila
  (wiesniacy moga ja zabrac na targ swojego miasta).
- Nic z niczego (po recenzji): dwie losowe nagrody gry z puli "kazdy towar handlowy" omijalyby zrodlo - zadanie Caravan Ambush (3 losowe towary
  z niczego dla gracza) i zdarzenie Trade Proposal (1 sztuka z sakw za 2 x wartosc zlotem bez platnika: 28 000 d za smocza kosc). Nasz surowiec
  z tych losowan wypada (klucz `RareMaterialOnlyFromSource`); sama regula gry dla innych towarow zostaje bez zmian.
- Cena: ok. 2/3 ceny luku (4000 i 14000 d); reszta to praca mistrza (prog i stamina legendy). Waga: klepka czardrzewa 2.5 kg, kosc 2 kg.
- Przepis: luk z listy `RareBowsIds`, dla ktorego surowiec istnieje, nie jest juz "rzadkim materialem" - wzor i nauka jak kazdego unikatu ROT
  (z oryginalu: przetop albo rozbiorka przy Smithing 200), a kowadlo zada surowca w sakwach gracza. Ilosc wedlug zasady przepisu z masy:
  klepka = 2 x masa luku (t5-6 jeszcze x2) = 0.4 kg, najmniej jedna cala sztuka (klepke tnie sie z jednego kawalka, reszta to odpad). Drewno
  i len z przepisu zostaja (raczka, nasadki, cieciwa - grosze). Pekniecie na kowadle: klepka przepada (pekla z lukiem). AI tych lukow nie kuje
  (warsztaty nie maja ich w produkcji, zlecenia kowalskie to bron z projektu gry).

**Zmiana** (klucze MCM w grupie "Learning patterns", oba domyslnie TAK: `RareBowsNeedMaterial` - NIE = blokada jak dotad (`RareBowsNotForged`);
`RareMaterialSupply` - NIE = nowych sztuk nie przybywa):
- `Armoury/ModuleData/arm_items_rare.xml` (nowy): `arm_weirwood` (Weirwood, siatka `hardwood`, 4000 d, 2.5 kg) i `arm_dragonbone` (Dragonbone,
  siatka `walrus_ivory` z NavalDLC, 14000 d, 2 kg), Type Goods, `item_category="arm_rare_stuff"`.
- `Armoury/ModuleData/arm_strings.xml` (nowy): nazwa kategorii `str_item_category.arm_rare_stuff` = "Rare Bowyer's Stuff".
- `Armoury/SubModule.xml`: `<Xmls>` - wezel `Items` (`arm_items_rare`, tylko Campaign i CampaignStoryMode) i `GameText` (`arm_strings`).
- `Armoury/src/RareMaterials.cs` (nowy): kategoria (`RegisterCategory`), luk -> surowiec (`Covers`, `PiecesFor`), brama (`Ready` - powod EN), kafelek
  (`TileText` - "PATTERN KNOWN - NEEDS WEIRWOOD / DRAGONBONE"), pobranie (`Take`), doplyw (`Daily`), linia startu (`StartLine`). Przedmiot bez
  kategorii `arm_rare_stuff` (rejestracja nie weszla) jest wylaczany - wtedy luk zostaje zablokowany jak dotad.
- Wpiecia (po 1 linii): `SubModuleMain.InitializeSubModuleGameObjects` (nowy override), `PatternGate.RareStuff` = `InRareList && !RareMaterials.Covers`
  (stare cialo jako `InRareList`), `PatternGate.BlockText` (kafelek), `Forge.LegendAllowed` (wspolna brama naszego menu, CRAFT BK - luki ida
  przez `Forge.Smith` - i podpowiedzi menu kowala), `Forge.Smith` (pobranie po `TakeMaterials`, pekniecie po `TakePartial`), `Forge.Begin` (pobranie
  na starcie roboty, jak reszta materialow), `SmithMenu` (szary wiersz z powodem), `ArmouryBehavior` (OnAfterSessionLaunched - linia startu; doba - `Daily`),
  `Settings.cs` + `McmSettings.cs` (gen_mcm), `tools/sprawdz_logi.py` (grupa `m97surowce`).
- Napisy (EN): "Needs weirwood - it is found only beyond the Wall, in the Free Folk villages of the haunted forest (Whitetree, ...). You carry none.",
  "Needs dragonbone - it is found only in the ruins of Valyria (Usek, Esme, Elyria) and the old markets of Volantis and Qarth. You carry none.",
  "The weirwood went into the bow (1 piece).", "The weirwood stave split with the bow - it is lost."
- Linie logu: `Surowce rzadkie (m97-surowce): start sesji | klucze RareBowsNeedMaterial TAK, RareMaterialSupply TAK, RareBowsNotForged TAK | surowce:
  arm_weirwood (Weirwood) cena 4000 d, waga 2.5 kg, kategoria arm_rare_stuff (towar handlowy NIE, popyt bazowy 0.020); arm_dragonbone ...; istnieje 2 z 2
  | zrodla: weirwood 6 z 6 osad [Whitetree (wies), ...]; dragonbone 5 z 5 osad [...] | doplyw dobowy TAK: weirwood 1 na 15 dob (pulap regionu 3, swiata 10);
  dragonbone 1 na 30 dob (2, 6); | luki z listy: dragonbone_bow <- 1 x arm_dragonbone (luk 0.10 kg, t6); weirwood_bow <- 1 x arm_weirwood (...); | zapas teraz:
  weirwood region 0, swiat 0, sakwy gracza 0; ... | potkniecia 0.` (po HistoricalPrices - cena w linii to cena po przeliczeniach). Co dobe:
  `Surowce rzadkie (m97-surowce): dzien N | weirwood: przybylo 1 (Whitetree), zapas regionu 1/3, swiata 1/10 (od startu sesji: losowan, trafien, przybylo,
  pulap regionu, pulap swiata, bez miejsca) | dragonbone: ... | dzis przybylo 1 | potkniecia 0.` Kucie (pierwsze 40, potem co 25.): `Surowce rzadkie
  (m97-surowce): kucie weirwood_bow | zuzyte 1 x arm_weirwood z sakw gracza (potrzeba 1, bylo 1), zostalo 0 | pekniecie NIE | od startu sesji: ...`.
- Zapis: bez nowego klucza (los bez pamieci; sztuki leza w magazynach osad i partii - zapis gry).

**Poprawki po recenzjach (gra, technika - 20.10).**
- (wazne, PRAWDA) Caravan Ambush: `CaravanAmbushIssueBehavior.cs:575-578` losuje 3 nagrody z `Items.All` (`IsTradeGood && !NotMerchandise`), a
  `OnQuestSucceeded` (`:582-590`) dodaje je graczowi z niczego - nasze dwa przedmioty sa w tej puli. Poprawka: prefiks `AmbushPre` na
  `CaravanAmbushIssueQuest.OnQuestSucceeded` (`AccessTools.DeclaredMethod`) - w liscie `_rewardItems` (pole zapisu zadania - dziala tez dla zadan
  wylosowanych wczesniej) nasz surowiec zamieniony na inny losowy towar z tej samej puli gry; pula pusta (niemozliwe) - pozycja zdjeta. NIE ustawione
  `is_merchandise="false"` (NotMerchandise wyklucza przedmiot z lupu `MapEvent.LootDefeatedPartyItems` - sztuki ginelyby z rozbitymi taborami).
- (wazne, PRAWDA) Trade Proposal: `IncidentsCampaignBehaviour.cs:1217-1240` - `incident_trade_proposal` zdejmuje 1 sztuke i placi `ItemValue * 2` od `null`;
  wybor `GetTradeProposalGood` (`:3416-3419`) to dowolny stos towaru handlowego z sakw (ziarno zdarzenia). Poprawka: postfiks `TradePost` na funkcji
  lokalnej `<InitializeIncidents>g__GetTradeProposalGood|25_208` (nazwa kompilatora sprawdzona odbiciem na DLL gry 1.4.8: metoda instancji, bez
  parametrow, zwraca `ItemRosterElement`; w kodzie szukana po fragmencie `g__GetTradeProposalGood|`, wymagana dokladnie jedna, potem
  `AccessTools.DeclaredMethod`): gdy wybor trafil nasz surowiec - wybor z tej samej listy bez naszych surowcow i z tym samym ziarnem
  (`Helpers.IncidentHelper.GetSeededRandomElement`, pole `_activeIncidentSeed`), wiec warunek, podpowiedz i skutek zdarzenia widza ten sam towar; brak innego
  towaru - pusty wybor (warunek zdarzenia zwraca false, zdarzenie nie wypada).
- (drobne, PRAWDA) Napis MCM "Villagers and caravans carry them on" - karawany tej kategorii nie kupuja. Poprawiony napis (`RareMaterialSupply`), opis w
  `RareMaterials.cs`, akapit "Zasada" i "Dla Jeffa".
- (drobne, PRAWDA - obie recenzje) S97-4 dawal NIE w poprawnym stanie (sztuki sprzedane w osadzie zrodla, nagrody). Teraz NIE tylko, gdy w dobie z `przybylo 1`
  zapas po dodaniu jest ponad pulapem (kod na to nie pozwala); zapas ponad pulapem bez przyrostu - S97-4b INFO. Sprawdzone na logach syntetycznych.
- (drobne, PRAWDA) Ksiega towarow: doplyw (`Daily`) i zuzycie klepki (`Take`) w ramce `FZc1` "drogi towarow (ZC1)" z opisem
  ("surowce rzadkie (m97): weirwood z ziemi zrodla", "surowce rzadkie (m97): klepka w luku gracza") - nie "bez wyjasnienia".
- (drobne, PRAWDA) Pekniecie po czasie (`Forge.Finish`) gubilo klepke po cichu: teraz `RareMaterials.NoteLost` - napis "The weirwood stave split with the bow -
  it is lost.", licznik z "w lukach" do "przepadlo" i linia `kucie ... | pekniecie po czasie (Forge.Finish)`.
- (drobne, PRAWDA) Zapas swiata bez schowkow osad - `WorldStock` liczy teraz `Settlement.Stash` (jak `GoodsLedger`).
- (drobne, PRAWDA, BEZ ZMIANY KODU) Kafelek CRAFT (ForgeView `ArmourItemMixin.Block`) pamieta napis na pare kowal + Smithing i nie odswieza sie po zuzyciu
  ostatniej klepki w tej samej wizycie (BK `OnRefresh` nie przebudowuje wierszy). Brama `Forge.Smith` dziala (kolejny klik: "Needs weirwood ... You carry none."),
  kafelek wraca po ponownym wejsciu do CRAFT. Poprawka wymagalaby zmiany ForgeView i powiadomien VM - nie warta ryzyka; opis w tescie (krok 5).
- (recenzja gra, pkt 3 - sprostowanie ryzyka) Brak pliku z wezla `<Xmls>` gra pomija (`GetMergedXmlForManaged` - pusta pozycja, walidacja `ValidationType.None`):
  SubModule.xml bez dwoch XML nie wywraca gry, luki zostaja zablokowane jak dotad.
- Reszta uwag recenzji technika (po "Zapas swiata ... Settlement.Stash") nie doszla w calosci do tej poprawki (tekst uciety) - do przejrzenia w nastepnej rundzie.
- Klucz `RareMaterialOnlyFromSource` (nowy, domyslnie TAK; NIE = losowania gry moga wziac surowiec jak kazdy towar). Linia startu: `RareMaterialOnlyFromSource TAK |
  nagrody gry bez surowca: Caravan Ambush wpieta, Trade Proposal wpieta`; linia doby: `nagrody gry bez surowca (od startu sesji): Caravan Ambush podmienione N,
  zdjete N, Trade Proposal pominiete (wywolan wyboru) N (bez innego towaru N)`; pierwsze 20 zdarzen: `Surowce rzadkie (m97-surowce): nagroda zadania Caravan
  Ambush - wylosowany ... zamieniony na ...` i `... zdarzenie Trade Proposal - wybor ... pominiety, zamiast niego ...`. Grupa `m97surowce`: S97-7 (latki wpiete
  przy kluczu TAK), S97-7b (podmiany, INFO).

**Ryzyko / co sprawdzic (kontrola calosci).**
- Regresje: `RareStuff` czytaja `RangedLore` (Teachable, Seed, CanLearnFrom, Learn), `FletchForge.BkCraftPrefix`, `Forge.LegendAllowed`, `PatternGate.BlockText`,
  `SmithMenu` - dla dwoch lukow z surowcem zachowuja sie teraz jak unikat ROT (nauka z oryginalu przy `ForgeUniquesByStudy`; bez tego klucza -
  zwykly wzor). Ravens' Teeth, Goldenheart, Giant Bow - bez zmian (nie ma ich na liscie od 20.10). Brak pliku XML albo klucz NIE = blokada jak dotad.
  `BlockText`: surowiec sprawdzany przed `LegendAllowed`, wiec kafelek nie pisze falszywie "LEGEND ALREADY FORGED". Koszt: dla 2 id z listy jedno
  `ReferenceEquals` i petla po 2 surowcach; reszta przedmiotow - jak dotad (HashSet). Watek glowny.
- Nic z niczego: sztuka powstaje tylko na targu osady zrodla, z pulapem regionu i swiata; zloto przechodzi jak przy kazdym towarze (gracz -> osada,
  mieszczanie -> kasa miasta - regula gry). Skala: 12-24 sztuki rocznie, 10 + 6 naraz w swiecie najwyzej.
- Kolizje z ekonomia Armoury: `HistoricalPrices` przelicza towary handlowe po kategorii (`cat.IsTradeGood`) i bron/zbroje - nasza kategoria nie jest
  towarem handlowym, nie ma jej w tabelach (cena w linii startu potwierdzi). `RawPrice` (wzor ceny surowcow) - tylko towary handlowe. Przedmiot jest
  Type Goods (`IsTradeGood` przedmiotu TAK): `CaravanGoods` i `TravellerTrade36b` moga go wozic (karawany gry i BK go jednak nie kupuja - kategoria niehandlowa; po recenzji), `HarvestValve` liczy tylko
  produkcje z ticku wsi gry (nasz przyrost jej nie dotyczy), gnicie BK i `RawNoRot` - stosy ponad 500 sztuk (nie dotyczy).
- Niesprawdzone w grze: czy wiesniacy gry/BK zabieraja towar z niehandlowej kategorii z targu wsi do miasta (jesli nie - lezy we wsi, gracz kupuje na miejscu);
  jak szybko mieszczanie BK go kupuja (linia doby pokaze zapas; pulap swiata i tak trzyma gore); czy ekran handlu wsi pokazuje go od razu (powinien - targ wsi
  to magazyn osady).
- Siatka `walrus_ivory` jest z NavalDLC: bez NavalDLC smocza kosc nie ma obrazka w ekwipunku (nazwa i handel dzialaja). Siatka `hardwood` - gra.
- Usuniecie paczki pozniej: sztuki znikaja z sakw i targow przy wczytaniu (gra wyrzuca przedmioty nie gotowe), w danych rynku miast zostaje martwy wpis
  kategorii (jak po NavalDLC). Wylaczenie kluczy w MCM - bez skutkow dla zapisu (sztuki zostaja zwyklym towarem).
- `PiecesFor`: przy luku ciezszym niz klepka (nie ma takiego na liscie - oba 0.1 kg) wiecej sztuk; linia startu pokazuje liczbe na kazdy luk.
- Konfiguracja Jeffa: klucze nowe, nie ma ich w Armoury.json - obowiazuja domyslne TAK.
- PLIKI DO WGRANIA: `Armoury/SubModule.xml` (zmieniony), `Armoury/ModuleData/arm_items_rare.xml` i `Armoury/ModuleData/arm_strings.xml` (nowe) - skrypt
  wgrywania kopiuje dzis tylko DLL, SubModule.xml i `arm_*.tsv`; bez tych dwoch XML luk zostaje zablokowany jak dotad (gra pomija brakujacy plik z wezla
  `<Xmls>` - recenzja; mimo to wgrywac trzy pliki razem).
- Latki "nic z niczego" (po recenzji): cele zadeklarowane w swoich klasach (bez pulapki czynnego modelu z `PatchTarget`), raz na proces z linii startu (w kampanii);
  w Armoury, ROT i BK nikt inny ich nie latuje (grep `CaravanAmbush`, `trade_proposal`, `IncidentsCampaignBehaviour` - u nas tylko WesterosClimate na porach roku).
  Funkcja lokalna zdarzenia: przy innej wersji gry nazwa kompilatora moze sie zmienic - wtedy linia startu pisze `Trade Proposal BRAK (...)`, S97-7 = NIE, reszta
  paczki dziala. Koszt: prefiks raz na udane zadanie; postfiks przy kazdym wyborze zdarzenia (porownanie id, przy trafieniu jedna petla po sakwach).
  Inne losowania gry z towarow: `LordsNeedsTutorIssue` (tylko Jewelry), NavalDLC `SetSailAndEscortTheFortuneSeekersQuest` (Oil, WalrusTusk) - kategoria nie nasza;
  grep `IsTradeGood` z losowaniem w BK, Spoils of War, BetterEconomy, ROT_Hideouts, turniejach - brak. AIInfluence (kod zaciemniony) - nie sprawdzone.

**Status.** DO SPRAWDZENIA (build Armoury kod 0 - takze po poprawkach recenzji; ForgeView nieruszany; w grze nie testowane; `sprawdz_logi --grupa m97surowce`
na logach syntetycznych: zapas ponad pulap bez przyrostu - S97-4 TAK + S97-4b INFO; przyrost ponad pulap swiata - S97-4 NIE; S97-7 TAK z latkami wpietymi;
logi Jeffa 12-25-19 i 14-53-56 - INFO, bez wyjatku).

**Test w grze krok po kroku.**
1. Wgrac DLL + SubModule.xml + `arm_items_rare.xml` + `arm_strings.xml`; wczytac zapis. W `Armoury.log` linia `Surowce rzadkie (m97-surowce): start sesji ...
   istnieje 2 z 2 | zrodla: weirwood 6 z 6 osad ... dragonbone 5 z 5 osad ... | luki z listy: dragonbone_bow <- 1 x arm_dragonbone ... weirwood_bow <- 1 x arm_weirwood`;
   w CrashScribe bez bledow XML (`arm_items_rare`).
2. Przeczekac kilka dni: co dobe linia `Surowce rzadkie (m97-surowce): dzien N`; po ok. 15-30 dobach `przybylo 1 (<osada>)`.
3. Pojechac do tej wsi (np. Whitetree za Murem) - "Buy products": Weirwood ok. 3200-5200 d (0.8-1.3 x 4000). Kupic.
4. Kuznia, CRAFT -> Bows: Weirwood Bow bez wzoru - "PATTERN NOT LEARNED" (zamiast "RARE STUFF"); klik - "This famed piece cannot be worked out at the anvil.
   Seize the original ...". Ze wzorem (oryginal przetopiony/rozebrany) i bez surowca: kafelek "PATTERN KNOWN - NEEDS WEIRWOOD", klik - "Needs weirwood - ...".
5. Z surowcem: luk wykuty, wiadomosc "The weirwood went into the bow (1 piece).", w logu `kucie weirwood_bow | zuzyte 1 x arm_weirwood`. Kafelek tego luku
   zostaje bez napisu "NEEDS WEIRWOOD" do ponownego wejscia do CRAFT (pamiec kafelka ForgeView) - klik bez surowca i tak daje "Needs weirwood ... You carry none.".
   W menu kowala z czasem (robota kilkudniowa) pekniecie przy odbiorze daje "The weirwood stave split with the bow - it is lost." i linie `pekniecie po czasie`.
6. MCM "Rare Bows Need Material" NIE: kafelek znow "RARE STUFF - CANNOT BE FORGED".
7. W linii startu: `RareMaterialOnlyFromSource TAK | nagrody gry bez surowca: Caravan Ambush wpieta, Trade Proposal wpieta`. Gdy wypadnie zadanie
   Caravan Ambush i gra wylosuje surowiec - linia `nagroda zadania Caravan Ambush - wylosowany arm_... zamieniony na ...` (rzadkie, ok. 14% udanych zadan).
8. `python -I tools/sprawdz_logi.py --grupa m97surowce <Armoury-*.log>`: S97-0..7 bez NIE (S97-3 przy logu krotszym niz 30 dob - INFO).
