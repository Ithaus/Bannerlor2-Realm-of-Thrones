## m99-markietani - rzad markietanow w obozie gracza: cyrulicy, platnerze, zabawa, sklep (Armoury)

**Dla Jeffa (prosto):** w Twoim obozie (klawisz O) jest nowa opcja **Visit the sutlers' row**. Gdy jestes w cudzej armii, ta sama opcja jest w menu armii.
Dziala tylko w duzym obozie: od 150 ludzi w Twojej druzynie albo w calej armii, w ktorej jestes. Im wiekszy oboz, tym wiecej kramow (1 na 100 ludzi, najwyzej 12),
a z kramami rosnie oferta. Sa cztery rzeczy: **cyrulicy** opatruja rannych za oplate (wracaja od razu do sluzby, najpierw weterani), **platnerze** naprawiaja
zuzyty sprzet Twoich ludzi (po cenie kowali najblizszego miasta plus 30%), **zabawa** (piwo, kosci, minstrel) raz na dobe podnosi morale Twojej druzyny - najwyzej
do +10, wiec codzienne picie nie robi z wojska wiecznie szczesliwego, a **sklep** sprzedaje jedzenie, piwo, wino, narzedzia, strzaly i belty z targu najblizszego
przyjaznego miasta po cenie miasta plus 30% za dowoz (skupuja od Ciebie po cenie miasta minus 30%, najwyzej tyle gotowki dziennie, ile maja kramy).
Towar naprawde znika z targu tego miasta, a Twoje pieniadze ida do tego miasta i do pobliskiej wsi, tak jak pieniadze zolnierzy u markietanow.

**Problem:** Jeff 20.10 (`docs/ZADANIA-OBOZ-NOC.md` w repo glownym, lista pkt 7): "Ogolnie mocno rozbudowac obozowanie armii: im wieksza armia, tym wiekszy
oboz, wrecz miasteczko obozowe z markietankami"; DECYZJE JEFFA PO RAPORCIE (14:20) pkt 4: "MARKIETANI: uslugi, rozrywka i sklep - tylko w duzych obozach
(armia)". Rozpoznanie `docs/ROZPOZNANIE-OBOZ-2026-10-20.md` rozdz. 7: "Nasi markietani (Sutlers) to tylko obieg zlota [...] Markietani nie maja obrazka,
menu, sklepu ani postaci"; plan: "Sklep markietanow w menu obozu - towary z polek tego miasta, do ktorego i tak plynie ich zloto, z narzutem za dowoz".

**Dowod (stan przed zmiana, z kodu):** `Sutlers.cs` (E5/A3) - ludzie partii w polu wydaja codziennie czesc sakiewki ludzi, pieniadz do kasy najblizszego
niewrogiego miasta (85%, hak 75/25 TownFolk.EarnService) i kiesy najblizszej wsi (z danina pana VillageTakings.Split) - bez menu i bez towaru.
Menu obozu gracza `arm_camp_wait` (NightRest) - ZWYKLE menu (nie menu oczekiwania), juz z opcjami "Walk through the camp" (CampWalk) i "Train the men"
(CampDrill, SwitchToMenu z opcji - bezpieczne w zwyklym menu). Wzorzec handlu z pola: `SideVillageVisit` V4 (koszyk leniwie, InventoryLogic z wlasnym
IMarketData/listenerem, po "Done" magazyn o roznice koszyka). Gra (`InventoryLogic.DoneLogic`, dekompilacja 1.4.8): bez osady i bez spotkania jedyny przelew to
`GiveGoldAction.ApplyBetweenCharacters(null, gracz, min(-TotalAmount, listener.GetGold()))` - druga strone placimy sami (jak SideVillageVisit.TradeDone).
W cudzej armii gracz stoi w menu oczekiwania gry `army_wait` (DefaultEncounterGameMenuModel.GetGenericStateMenu: `mainParty.AttachedTo != null` -> "army_wait"),
a klawisz O dziala tylko na czystej mapie (NightRest: `st.AtMenu` -> return).

**Zasada (liczby - decyduje Claude):**
- **Kiedy:** oboz gracza poza osada, na ladzie, bez bitwy/oblezenia/spotkania. Oboz = kolumna jak w CampWalk: druzyna gracza, a w armii (gracz wodzem albo
  doczepiony do wodza) wodz i wszystkie doczepione partie; liczeni zdrowi i ranni. Rzad stoi od **SutlerRowMinMen = 150** ludzi (ponizej opcja szara
  z podpowiedzia). Kramy = oboz / **SutlerRowMenPerStall 100**, najmniej 1, najwyzej **SutlerRowMaxStalls 12**.
- **Wejscie:** wlasny oboz / wlasna armia - `arm_camp_wait` (klawisz O). Cudza armia - ta sama opcja w `army_wait` (menu oczekiwania gry): opcja tylko podnosi
  flage (i rusza zegar, gdy stal - jak CampDrill), a przelaczenie na `arm_camp_sutlers` robi `CampaignEvents.TickEvent` (EndWait + SwitchToMenu - ta sama droga,
  ktora gra zmienia menu z army_wait: PlayerArmyWaitBehavior.OnTick w TickEvent, ArmyWaitMenuTick). "Back to the camp" w cudzej armii - do menu stanu gry
  (`GetGenericStateMenu`, czyli army_wait) ze zwyklego menu. W zwyklym menu czas stoi (Campaign.Tick: `_dt = 0` przy menu bez oczekiwania) - armia nie idzie dalej.
- **Zrodlo:** najblizsze miasto niebedace w wojnie z graczem (to samo `Sutlers.NearestTown`, do ktorego plynie obieg AI; brak - najblizsze w ogole).
- **Zaplata uslug** (cyrulik, robota platnerzy, zabawa, narzut sklepu) = obieg markietanow jak `Sutlers.One`: `SutlerTownShare` (85%) do kasy miasta-zrodla
  z hakiem 75/25 (`TownFolk.EarnService` KLife) i licznikiem `TownFolk.Note`; reszta do kiesy najblizszej niewrogiej wsi w stanie Normal (z danina pana
  `VillageTakings.Split` przy Village Tribute From Sutlers; wies rodu gracza bez daniny), od czesci w kiesie wsi udzial korony DS (`DsVillage.WarShareSutlers`,
  jak Sutlers.One); brak wsi - calosc do miasta. Wplaty poza tickiem dobowym nazwane w ksiedze przeplywow osad (`MoneyLedger.Note169`).
- **Cyrulicy:** ranni zolnierze (bez bohaterow) wracaja do sluzby, najwyzej **SutlerSurgeonPerStall 8** na kram na dobe, najpierw najwyzszy tier;
  cena **SutlerSurgeonFee 15 x (tier + 1)** za czlowieka (T1 30, T3 60, T5 90). Bez latki modelu leczenia gry.
- **Platnerze polowi:** zuzyte sztuki LUDZI z magazynu kwatermistrza (`TroopSelfMend.MenWorn` - bez Twojej czesci, bez wrakow, bez koni i zwierzat), najgorsze
  najpierw, najwyzej **SutlerArmourerPerStall 6** sztuk na kram na dobe. Robota = stawka kowali miasta-zrodla (`TroopSelfMend.UnitCost(ee, miasto)` - ta sama co
  przy naprawie w tym miescie) x (1 + narzut 30%). Przy naprawach z materialem (Smith Mend From Market + Mend Material Men And Lords) material z polki TEGO miasta
  (`MendMaterial.Order` / `Bench` - ta sama regula co kowale miasta: brak materialu - sztuka czeka, bez receptury - nie robota kowala). Pieniadz: material do kasy
  miasta (towar z jego polki), robota z narzutem obiegiem uslug. Placi gracz z kiesy; sakiewka ludzi nietknieta.
- **Zabawa:** co 24 h (odstep, nie doba kalendarzowa), **SutlerRevelPerMan 2** denary na zdrowego czlowieka druzyny gracza za pelna kolejke; morale
  **+SutlerRevelMorale 6** przez RecentEventsMorale, ale najwyzej do **SutlerRevelMoraleCap 10** (dodatek = min(6, 10 - obecne), cena w tej samej proporcji);
  dodatek ponizej 1 - opcja szara ("the men are still merry"). Gra zdejmuje 10% na dobe. Tylko druzyna gracza (to on placi).
- **Sklep:** koszyk liczony przy kliknieciu z **polki miasta-zrodla**: jedzenie (IsFood), piwo, wino, narzedzia, strzaly, belty; kazdego rodzaju najwyzej
  **SutlerRowStockPerStall 5 x kramy** sztuk na dobe (mniej kupione dzis, nie wiecej niz na polce). Cena kupna = cena miasta x (1 + **SutlerRowMarkupPercent 30%**),
  skup = cena skupu miasta x (1 - 30%). Gotowka na skup = min(kasa miasta, **SutlerRowCashPerStall 1500 x kramy NA DOBE** - mniej wydane dzis na skup; licznik w zapisie).
  Po "Done": kupione schodzi z polki miasta (dokladnie roznica koszyka), sprzedane wchodzi na polke. Pieniadz: gracz netto D (zmiana jego kiesy, ktora zrobila gra);
  narzut kupionego M = wartosc po cenie markietanow - po cenie miasta (w 0..D) - obiegiem uslug; D - M do kasy miasta (towar po cenie miasta; ujemne = miasto placi
  za skup). Suma = D.
- **Oboz 150 / 400 / 1000 ludzi:** kramy 1 / 4 / 10; cyrulicy 8 / 32 / 80 rannych na dobe; platnerze 6 / 24 / 60 sztuk na dobe; towar 5 / 20 / 50 sztuk kazdego
  rodzaju na dobe; gotowka skupu 1500 / 6000 / 15000 na dobe; narzut 30% (ziarno za 10 w miescie - 13 u markietanow, skup 7; robota kowala za 50 - 65 w polu).
  Zabawa (druzyna 150 zdrowych): 1. dzien +6 za 300; po 24 h morale ok. 5.4 -> +4.6 za 230; potem ok. +1 dziennie za ok. 50 - premia z zabaw stoi przy +10, nie rosnie.
- **Uzasadnienie:** markietani wozili towar z najblizszych targow i doliczali dowoz i ryzyko - 20-40% ponad cene targu (30% w srodku); cyrulik opatrywal
  kilku-kilkunastu ludzi dziennie (8 na kram); platnerz polowy latal kilka sztuk dziennie (nity, laty, ostrzenie - 6 na kram), material z targu, z ktorego woza towar;
  oplata cyrulika rosnie z tierem. Zabawa za mniej niz dzienny zold - drobny, przemijajacy dodatek do morale, z pulapem.
- **AI bez zmian** - obieg AI liczy dalej `Sutlers.cs`.

**Zmiana (Armoury):**
- NOWY `Armoury/src/CampSutlers.cs`: opcja "Visit the sutlers' row" w `arm_camp_wait` i w `army_wait` (cudza armia; flaga + TickEvent) -> zwykle menu
  `arm_camp_sutlers` ("Pay the barber-surgeons to tend N wounded (X denars)", "Have the field armourers mend N pieces of the men's gear (X denars)",
  "Stand the men a round - ale, dice and a minstrel (X denars)", "Trade with the sutlers", "Back to the camp"); ekran handlu gry (InventoryState Trade, wlasne
  `RowMarket : IMarketData` i `RowListener : InventoryListener`), rozliczenie w `DoneLogicExtrasDelegate`. Stan dnia (doba, godzina zabawy, opatrzeni dzis,
  kupione dzis wedlug rodzaju, skup dzis, naprawione dzis) w zapisie `arm_sutlerrow` przez `SaveText.Sync` (osobny try; format "2|..."; stary "1|..." czytany;
  brak klucza = pusty dzien). Kazda opcja we wlasnym try - wyjatek = licznik potkniec + ERROR raz na miejsce, menu dziala dalej.
- `Sutlers.cs`: `NearestTown` / `NearestVillage` z private na internal (bez zmiany dzialania).
- `ArmouryBehavior.cs`: jedna linia `CampSutlers.RegisterEvents(this)` + blok SyncData `arm_sutlerrow`.
- `Settings.cs` (grupa MCM "Camp and baggage (night 20/21.10)"), `McmSettings.cs` z `gen_mcm.py`, zakresy suwakow w `gen_mcm.py`. Klucze NOWE (13): **CampSutlersRow**
  (TAK), **SutlerRowMinMen** (150), **SutlerRowMenPerStall** (100), **SutlerRowMaxStalls** (12), **SutlerRowMarkupPercent** (30), **SutlerRowStockPerStall** (5),
  **SutlerRowCashPerStall** (1500 na dobe), **SutlerSurgeonPerStall** (8), **SutlerSurgeonFee** (15), **SutlerRevelPerMan** (2), **SutlerRevelMorale** (6),
  **SutlerRevelMoraleCap** (10, 0..100), **SutlerArmourerPerStall** (6, 0..100).
- `tools/sprawdz_logi.py --grupa m99markietani`: MK-0 start (klucz), MK-1 ERROR CampSutlers i potkniecia 0, MK-2 uslugi i sklep tylko w duzym obozie (z platnerzem),
  MK-3 pieniadz w obiegu (kazda zaplata = suma odbiorcow, roznica 0 - cyrulik, platnerz, zabawa, sklep; linia doby roznica 0), MK-4 towar z polki miasta = towar
  w sklepie, MK-5 uzycie (INFO), MK-6 skup w limicie dnia, MK-7 zabawa w pulapie morale (po <= pulap, dodatek >= 1). Sprawdzone na liniach probnych (8/8 TAK;
  zle linie -> MK-6 i MK-7 NIE) i na logu biegu 98 (INFO - brak paczki).
- Linie logu: `Markietani obozu (m99): start sesji | ... w menu obozu (arm_camp_wait) i w menu cudzej armii (army_wait) | klucz Camp Sutlers Row TAK | prog obozu ...`,
  `... cyrulik | oboz N ludzi (prog P), kramy K | ... | kontrola: X = Y (roznica 0)`, `... platnerz | oboz ... | naprawione n z w zuzytych sztuk ludzi (dzis a z b) |
  robota L (stawka kowali town x 1.30) + material M (...) | gracz zaplacil X -> material do kasy miasta M + robota -> ... | kontrola: X = M + S (roznica 0)`,
  `... zabawa | ... | morale RecentEvents a -> b (dodatek g, pulap 10) | ... | kontrola ...`, `... sklep otwarty | ... gotowka skupu G (dzis wydane s z limitu) ...`,
  `... sklep zamkniety | ... | kontrola ... | skup dzis s z limitu L.`, doba (tylko gdy cos sie dzialo): `... dzien N | wizyty v (z cudzej armii a) | cyrulik |
  platnerze | zabawa | sklep | obrot uslug i narzutu -> ... (w tym korona DS c) (roznica 0) | potkniecia 0.`

**Poprawki po recenzji (PELNA, 21.10):**
1. *Wazne - limit gotowki skupu na otwarcie ekranu, nie na dobe* - PRAWDA (nowy RowListener przy kazdym Trade(), w stanie brak licznika skupu). Poprawione:
   `_soldToday` (zapis), gotowka = min(kasa miasta, kramy x 1500 - skup dzis), TradeDone dolicza skup netto; linia `skup dzis s z limitu L`, MK-6.
2. *Wazne - zabawa bez pulapu (zbiega do +60)* - PRAWDA (MobileParty.DailyTick zdejmuje 10%, nic nie przycina ponizej 100). Poprawione: pulap SutlerRevelMoraleCap 10,
   dodatek = min(6, pulap - obecne), cena w proporcji, odstep 24 h zamiast flagi doby (23:00 i 01:00 to juz nie dwie zabawy); log `dodatek g, pulap 10`, MK-7.
3. *Wazne - w cudzej armii rzedu nie da sie otworzyc* - PRAWDA (army_wait, klawisz O tylko na czystej mapie). Poprawione: opcja w `army_wait` (flaga + TickEvent,
   bez SwitchToMenu z opcji menu oczekiwania), powrot do menu stanu gry. Precedens gry: PlayerArmyWaitBehavior.OnTick (TickEvent) zmienia menu z army_wait.
4. *Wazne - brak uslugi naprawy* - PRAWDA (TroopSelfMend/MendMaterial sa). Poprawione: platnerze polowi (wyzej). Praczki/odziez (ArmyClothing) - celowo NIE:
   odziez ma wlasny obieg (zakup i zuzycie), a pranie nie ma w modzie zadnego stanu do poprawy - nic, za co uczciwie mozna by brac pieniadze.
5. *Drobne - Spread nie jak Sutlers.One, ksiega* - czesciowo poprawione: udzial korony DS od czesci wsi (`DsVillage.WarShareSutlers`, wliczony do "danina", suma
   kontroli bez zmian), wplaty do kas miasta i wsi (i towar sklepu, i material platnerzy) nazwane `MoneyLedger.Note169` (inne / naprawy). Zostaje: zloto gracza
   schodzi `GiveGoldAction(gracz, null)` (jak Pay.ToSettlement poza osada) - przelew do osady gry dalby podwojna wplate (EarnService i VillageTakings.Split
   wplacaja same). `CitySack.NoSpend` celowo nie: regula dotyczy wydatkow zolnierzy w lupionym miescie; tu placi gracz z wlasnej woli.
6. *Drobne - kiesa markietanow pokazuje 0 do pierwszej transakcji* - PRAWDA (SPInventoryVM.UpdateLeftCharacter przy LeftRosterName != null nie ustawia
   LeftInventoryOwnerGold; bez nazwy poza osada tez nie - galaz "Loot"). NIE poprawione: wymagaloby latki VM ekranu; kosmetyka (pojedyncze sprzedaze dzialaja,
   przycisk "sprzedaj wszystko" przed pierwsza transakcja nic nie robi) - to samo w handlu SideVillageVisit (m97).

**Ryzyko / co sprawdzic (kontrola calosci):**
- **Regresje:** Sutlers.cs - tylko widocznosc dwoch metod; AI i obieg sakiewek bez zmian. Sakiewka ludzi gracza (MenPurse) dalej wydaje u markietanow AI-regula -
  to pieniadz ludzi, a rzad markietanow bierze zloto z kiesy GRACZA (Hero.Gold) - nic liczone dwa razy. Menu `arm_camp_wait` - nowa opcja (indeks 3), stare bez zmian;
  `army_wait` - jedna nowa opcja na koncu (tylko w cudzej armii, przy kluczu TAK).
- **Cudza armia (DO SPRAWDZENIA w grze, najwazniejsze):** przelaczenie z army_wait w TickEvent - droga gry, ale nasza pierwsza; gdy zegar stoi, opcja rusza go
  (jak CampDrill). Jesli PlayerEncounter w army_wait nie jest pusty, opcja bedzie szara ("Not while in a battle or siege") - bez wywrotki. W razie klopotu:
  klucz CampSutlersRow NIE wylacza obie opcje.
- **Zamknieta ekonomia:** uslugi - zloto gracza = kasa miasta + lud + kiesa wsi + danina (+ korona DS), w jednym kroku; platnerze - material do kasy miasta + robota
  obiegiem, material zdjety z polki miasta (Bench.Commit, ksiega towarow FMend); sklep - gra zmienia kiese gracza, my: polka miasta o roznice koszyka, kasa miasta
  o (D - M), narzut M obiegiem. **Do sprawdzenia w grze**: czy `Town.ChangeGold` ujemne nie przycina ponizej zera (limit gotowki to wyklucza).
- **Skup dowolnego towaru:** ekran handlu przyjmuje od gracza kazdy przedmiot (takze lup) po 70% ceny skupu miasta, ale najwyzej gotowka kramow na DOBE (1500 na kram) -
  "targ w polu" dla lupu z dziennym limitem. Za latwo - zmniejszyc SutlerRowCashPerStall.
- **Platnerze:** stawka i material jak kowale miasta-zrodla (bez godzin cechow GuildMend - to nie warsztat miasta, tylko wozy w obozie); naprawiaja czesc ludzi,
  nie Twoja (Twoja - u kowala). Podglad w warunku opcji liczony raz na godzine/kiese (pamiec), naprawa zawsze liczona od nowa.
- **Leczenie od reki:** ranni wracaja natychmiast (za oplate, z dziennym limitem kramow); bohaterowie (towarzysze) nie sa leczeni.
- **Miasto po lupie (CitySack):** zrodlo nie pomija lupionego miasta - patrz poprawka 5.
- **Napisy opcji** ustawiane w warunku (MBTextManager) - jak w CampDrill/SideVillageVisit; pusty napis przy pierwszym wejsciu - kosmetyka.
- **Kiesa markietanow na ekranie handlu** - 0 do pierwszej transakcji (poprawka 6, kosmetyka).
- **Zapis:** klucz `arm_sutlerrow` (maly napis); stary zapis - pusty dzien; wczytanie nie odnawia zabawy, cyrulikow, platnerzy ani gotowki skupu.

**Status:** DO SPRAWDZENIA (build Release kod 0; nie uruchamiane w grze).

**Test w grze krok po kroku:**
1. Wczytac zapis; w `Armoury.log` linia `Markietani obozu (m99): start sesji | ... klucz Camp Sutlers Row TAK | ... platnerze 6 ... do pulapu 10, co 24 h ...`.
2. Druzyna < 150 ludzi, bez armii: klawisz O -> "Visit the sutlers' row" szara, podpowiedz "Only a big camp draws sutlers - 150 men or more (your camp: N)".
3. Druzyna >= 150 albo wlasna armia (razem >= 150): opcja czynna -> menu z opisem kramow i miasta-zrodla.
4. Z rannymi: "Pay the barber-surgeons to tend N wounded (X denars)" -> ranni spadaja o N, kiesa o X; w logu `cyrulik` z `kontrola: X = X (roznica 0)`.
5. Ze zuzytym sprzetem ludzi: "Have the field armourers mend N pieces of the men's gear (X denars)" -> kiesa o X, w magazynie mniej sztuk w zlym stanie;
   w logu `platnerz | ... kontrola: X = M + S (roznica 0)`. Po limicie dnia opcja szara.
6. "Stand the men a round" -> komunikat "(Morale +6.0)" i "Recent events" w morale; od razu drugi raz - szara (24 h). Po 24 h - dodatek mniejszy (do +10) i tansza kolejka.
7. "Trade with the sutlers" -> ekran handlu "Sutlers' row"; ceny ok. 30% wyzsze niz w miescie-zrodle. Sprzedac cos -> Done -> otworzyc znowu: gotowka mniejsza
   o to, co dostales (podpowiedz "left in hand today"). W logu `sklep zamkniety ... skup dzis s z limitu L`.
8. CUDZA ARMIA: dolaczyc do armii lorda (razem >= 150) -> w menu armii opcja "Visit the sutlers' row" -> po chwili menu rzedu; usluga; "Back to the camp" -> menu
   armii jak przedtem (armia idzie dalej). Bez wywrotki; w linii doby `wizyty v (z cudzej armii 1)`.
9. Po dobie: linia `Markietani obozu (m99): dzien N | ... (roznica 0) | potkniecia 0.`; `python -I tools/sprawdz_logi.py --grupa m99markietani <Armoury-*.log>` -
   MK-0..MK-4, MK-6, MK-7 TAK.
