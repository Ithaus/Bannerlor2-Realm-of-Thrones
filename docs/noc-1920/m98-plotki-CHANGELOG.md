## m98-plotki - plotki za kufel, plotek na kufel wedlug Uroku, turnieje i swaty w plotkach, pijanstwo i kac (Armoury)

**Dla Jeffa (prosto):** w karczmie miasta i w zajezdzie opcja plotek to teraz **Buy a tankard of ale and listen to the gossip (N d)** - placisz za kufel
(zwykle 1 d, zloto idzie do karczmarzy) i dopiero wtedy ludzie gadaja. Za jeden kufel slyszysz tyle plotek, na ile pozwala Twoj Urok (ponizej 50 - jedna,
od 50 - dwie, od 110 - trzy, od 170 - cztery, od 230 - wszystkie); kolejny kufel = kolejne plotki, a te juz uslyszane w tej karczmie nie wracaja. W plotkach
sa teraz tez **turnieje** (gdzie, jak daleko, od kiedy, jaka nagroda) i **swaty** (pan albo pani rodu w okolicy szuka meza dla corki albo zony dla syna) -
tylko prawdziwe, z biezacego stanu gry. Jak wypijesz za duzo (zwykle ponad 3 kufle, wiecej przy dobrej Wytrzymalosci i Atletyce), jestes pijany: druzyna
wolniej idzie po mapie, az to odespisz (sen trwa dluzej), a potem budzisz sie z kacem do zmierzchu. Z kacem lozko w karczmie nie daje premii
"wypoczety" - kto zapil noc, ten rano nie jest wypoczety. Ta sama wiesc (ten sam turniej, ta sama banda w tym samym miejscu) nie wraca za kolejny kufel.

**Problem:** Jeff 20.10 ok. 15:20: "W gossip tez powinny byc turnieje, gdzie jest turniej itp. [...] ze ktos wydaje zone albo szuka dla meza dla swojej corki
jakis lord czy ktos, i jak biore gossip, to musze kupic piwo lub ale cokolwiek, aby sluchac, nie slucham przeciez z pustym jezykiem, bo by mnie wyrzucili
z karczmy." Ok. 15:25: "za kazde wypite piwo moge dostac tyle plotek, ile moja charyzma [...] max moge dostac wszystkie plotki po jednym piwie, ale to zalezne
od mojego przekonywania [...] jak wypije za duzo piwa, mam minus do predkosci i musze dospac, i nastepny dzien kac".

**Stan przed zmiana (z kodu):** `Rumours.TownGossipConsequence` / `RoadGossipConsequence` - okno z cala pula (najwyzej 6 plotek: bandy 2, kryjowki 1, wojska 1,
karawany 1, ceny 1, wojny 2) za darmo; turniejow i swatow w plotkach nie bylo. Turnieje gry: `TournamentManager.GetTournamentGame(town)` (te same liczy linia
"Turnieje (E7/0)" i przejmuje GrandTourney; `TournamentGame.CreationTime`, `Prize`). Swaty: `MarriageModel.IsSuitableForMarriage` (domyslny: zywy, bez
malzonka, pan rodu, nie z rodu pomniejszego, nie zareczony, nie w bitwie/armii, wiek >= MinimumMarriageAge; BK moze nadpisac - bierzemy czynny model).
Piwo: `beer` (SandBoxCore, beczka 10 kg, wartosc 50; po HistoricalPrices u Jeffa 3 d - log 20.10 20:18:46 "beer 50->3").

**Zasada z liczbami:**
- **Cena kufla** = 1/10 ceny beczki piwa dla gracza w miescie (`Town.GetItemPrice` - ta sama co w handlu; beczka ok. 20 kufli, karczmarz bierze x2),
  najmniej 1 d (najmniejsza moneta), bez piwa w grze 2 d. Zajazd - miasto handlowe wsi okregu (`Village.TradeBound`), inaczej najblizsze. U Jeffa (beczka ok. 3 d)
  kufel = **1 d**; drogie piwo (beczka 15-24 d) = 2 d.
- **Kto dostaje:** karczma miasta - gracz -> kasa miasta -> kiesa ludu miasta (`TownInns.HeroPays`, `TownFolk.EarnFromTown` KLife - ta sama droga co nocleg);
  zajazd - gracz -> kiesa wsi okregu, podatek pana na licznik podatku wsi, udzial korony DS (`TownInns.RoadHeroPays` - jak nocleg w zajezdzie). Kontrola
  obiegu przy kazdym kuflu (suma posiadaczy przed i po) - roznica ma byc 0. Komunikat z powodem: "You paid 1 for a tankard of ale at X." (PayNotes).
- **Plotek na kufel wedlug Uroku (Charm):** `1 + (Urok + 10) / 60`, od 230 - wszystkie: **Urok 20 -> 1, 80 -> 2, 150 -> 3, 200 -> 4, 250 -> wszystkie**
  (progi 50 / 110 / 170 / 230). Pula miejsca jak dotad (6 dawnych) + do 2 turniejow i do 2 swatow = najwyzej 10. Kolejnosc od najwazniejszej (jak dotad).
  Uslyszane w tym miejscu (karczma miasta - id miasta, zajazd - uid) nie wracaja, poki nie ma NOWEJ WIESCI (ksiega skrotow tozsamosci wiesci - po recenzji,
  patrz nizej; wpis zyje 10 dob, najwyzej 400 miejsc x 60 wpisow); wszystko uslyszane - opcja szara "You have heard all the talk here. Come back when there
  is fresh news." (bez placenia).
  **5 XP Uroku** za kazda uslyszana plotke.
- **Turnieje:** turniej gry w toku do 450 jedn. od karczmy (najblizsze 2), daleki dopiero, gdy moglby doleciec kruk (`Ravens.NotYet`, jak wiesci wojenne);
  tresc: miasto ("here in X" do 6 jedn.), odleglosc i kierunek, kiedy ogloszony (dzis / wczoraj / kilka dni / tygodnie), nagroda i jej cena na targu tego miasta.
- **Swaty:** dorosle niezamezne dziecko rodu (bez rodu gracza i band, nie glowa rodu, najwyzej 40 lat), z zywym rodzicem (ojciec, inaczej matka - on/ona
  "szuka"), dopuszczone przez czynny model malzenstw; siedziba rodu do 200 jedn. od karczmy (wiesci z bliska); 2 najlepsze wedlug tieru rodu i bliskosci.
- **Prog upicia:** `DrunkAfterTankards` (3) **+1 przy Wytrzymalosci 5, +1 przy Atletyce 150** = 3-5 kufli od ostatniego odespania (4 h odpoczynku po ostatnim
  kuflu zeruje licznik trzezwego). Ostrzezenie przy kuflu rownym progowi ("One more tankard and you will be drunk."). Karczmarz nie naleje ponad **prog + 3**.
- **Pijany** (kufel ponad prog): predkosc na mapie **-15%** za pierwszy kufel ponad prog, **+5%** za kazdy nastepny (sufit `DrunkSpeedPenaltyPercent` x 2 = 30%;
  przy domyslnych najwyzej **-25%**, bo karczmarz nie naleje ponad prog + 3); morale bez zmian. Do odespania trzeba **6 h (baza snu) + 1 h na kufel ponad prog** odpoczynku od ostatniego kufla - liczonego tak samo jak ksiega snu
  NightRest (noc 1/h, dzien DayRestFactor, sen w menu karczmy x SleepFactor). Dlug snu rosnie o te godziny: `NightRest.NeededHours` dolicza brakujace godziny,
  wiec sen w menu ("Bed down", nocleg) trwa dluzej - gracz dosypia. "You are drunk - your party moves 15% slower on the map until you sleep it off (7 hours of rest)."
- **Kac** (po odespaniu): predkosc **-10%** i morale **-10%** (czynnik, jak kara snu) do **18:00** dnia odespania (odespane od poludnia - 6 h).
  "You wake with a hangover - your head pounds: ..." po pobudce z menu snu; odespane w postoju albo obozie na mapie (bez menu snu) - "The ale wears off, but
  a hangover follows - ..."; "Your head clears - the hangover is gone." Kufel w czasie kaca zdejmuje kac (pijany zamiast).
- **Pijany albo skacowany nie jest wypoczety:** premia noclegu "Slept in a bed" / "Slept under a roof" (predkosc +5..12%, morale +2..8 do zmierzchu) nie dziala,
  dopoki trwa kara kufli; komunikat pobudki w karczmie to mowi ("But the ale still pounds in your head - the bed does you no good until the hangover passes.").
  Kac trwa do 18:00, premia tez do zmierzchu (18:00) - w praktyce noc zapita w karczmie = bez premii lozka.
- Tylko gracz. Kary licza latki NightRest (SpeedPostfix / MoralePostfix, raz na lancuch modeli) z pol int liczonych na watku glownym.

**Zmiana (Armoury):**
- `InnDrink.cs` (nowy): stan gracza (kufle, pijany, ponad prog, odpoczynek od kufla, koniec kaca), progi, krzywa Uroku, kary, ksiega uslyszanych, zapis
  `arm_drink` (SaveText.Sync, v1), linie logu "Kufle (m98):" (start sesji, upicie, odespanie/kac, koniec kaca, doba).
- `Rumours.Drink.cs` (nowy, czesc Rumours): turnieje i swaty (kategorie 7 i 8), cena kufla, pula miejsca raz na godzine gry, warunek i skutek opcji z kuflem,
  linia "Kufle (m98): kufel w ..." (cena, beczka, komu, obieg, Urok -> plotek, rodzaje, turnieje i swaty sprawdzone w chwili sluchania).
- `TownInns.Drink.cs` (nowy): `DrinkPayTown` / `DrinkPayRoad` / `DrinkWorld*` - opakowania drog zaplaty i kontroli obiegu noclegu (bez nowych przeplywow).
- `Rumours.cs`: `partial`; `Item.Ref`; liczniki kategorii 9; `Build` - turnieje i swaty na wlasnych miejscach (dawne kategorie dalej najwyzej 6 - przy
  wylaczonych kluczach pula bez zmian); `Show` z gotowa lista i naglowkiem; warunki/skutki opcji - przy InnGossipNeedsDrink sciezka kufla, etykieta przez
  zmienna `ARM_GOSSIP_TOWN` / `ARM_GOSSIP_ROAD`; linia "sluchane w" z turniejami i swatami.
- `TownInns.cs`, `RoadsideInns.cs`: etykieta opcji plotek ze zmiennej (warunek ustawia "Listen to the gossip" albo "Buy a tankard of ale ... (N d)").
- `NightRest.cs`: `NeededHours` + `InnDrink.ExtraSleepH`; `SpeedPostfix` / `MoralePostfix` - kara pijanego/kaca dla partii gracza (raz na lancuch);
  `OnHourly` - `InnDrink.RestHour` z odpoczynkiem tej godziny (ta sama regula co `_restTonight`, linia bez zmian).
- `ArmouryBehavior.cs`: Reset, SyncData `arm_drink`, SessionStart, Daily, Hourly (po NightRest.OnHourly).
- `Settings.cs` + `McmSettings.cs` (gen_mcm.py; zakresy w RANGES): **InnGossipNeedsDrink, GossipPerDrinkByCharm, GossipTournaments, GossipMatchmaking,
  DrinkDrunkHangover** (domyslnie TAK; NIE = jak dotad), **DrunkAfterTankards 3, DrunkSpeedPenaltyPercent 15, HangoverSpeedPenaltyPercent 10,
  HangoverMoralePenaltyPercent 10**. Grupa MCM "A night's rest" (tam gdzie Inn Gossip).
- `tools/sprawdz_logi.py --grupa m98plotki` (KP-0..KP-7).

**Ryzyko / co sprawdzic (kontrola calosci):**
- Regresje: przy wylaczonych nowych kluczach `Build` zwraca te same plotki (dawne kategorie najwyzej 6, nowe miejsca puste), okno i linia "Plotki (PL)" jak
  dotad (linia doby bez zmian formatu - parser nzajazdy). Etykieta opcji to teraz zmienna tekstu - warunek ustawia ja zawsze jako pierwsza rzecz.
- NightRest: `NeededHours` dla trzezwego bez zmian (ExtraSleepH = 0). Pijany: dluzszy cel snu w menu, pozniejsza splata starego dlugu od reki (CreditRest),
  rzadziej pomijane pytanie o nocleg (NightPromptSkipWhenRested) - zamierzone ("musze dospac"). Swit dalej liczy dlug od bazy (SleepHoursNeeded), wiec
  picie samo dlugu nocy nie robi. Kara pijanego jest od wyniku przed kara dlugu (skladaja sie); premia karczmy "wypoczeci" wstrzymana, dopoki trwa kara kufli
  (po recenzji). Pobudka (LeaveSleep / EndSleepQuiet) wola InnDrink.OnWake przed TownInns - kac jest juz ustawiony, gdy karczma pisze komunikat. Kowadlo -
  NeededHoursFresh (trzezwy bez zmian).
- Zamknieta ekonomia: kufel = przelew gracz -> karczmarze tymi samymi metodami co nocleg; piwo z polek nie znika (jak wino w noclegu - usluga, nie towar).
  Cena 1 d przy beczce 3 d to x6 hurtu (najmniejsza moneta) - przy drogim piwie x2.
- "Uslyszane" liczy sie po tozsamosci wiesci (po recenzji): banda wraca dopiero w innym miejscu (inna najblizsza osada / kratka) albo po spaleniu wsi; wojsko -
  po zmianie celu; jesli Jeff uzna, ze banda "w drodze" zbyt rzadko wraca albo zbyt czesto - do strojenia promienia miejsca (25 / 50 jedn.).
- Swaty: rodzic "szuka" to nasz opis faktu (dziecko wolne i dopuszczone do slubu) - gra nie ma stanu "szuka meza". Model BK moze miec inne warunki niz domyslny.
- Turnieje GrandTourney w fazie zjazdu: plotka mowi o turnieju gry (TournamentGame) - jesli GrandTourney trzyma zjazd bez TournamentGame, plotki o nim nie ma.
- Koszt: pula liczona najwyzej raz na godzine gry na miejsce (warunek opcji tez ja czyta); swaty - przeglad rodow z siedziba do 200 jedn.; linia doby - jeden
  przeglad wszystkich rodow (kandydaci w swiecie).
- Nie testowane w grze (zakaz: trwa test 97b). Build Release kod 0.

**Poprawki po recenzjach (gra, technika) - kazde zgloszenie sprawdzone w kodzie:**
- **[technika, wazne] PRAWDZIWE - pobudka z pelnym paskiem zostawiala gracza pijanym.** Dowod: `InnDrink._slept` rosl tylko pelnymi tikami godzinowymi
  (`RestHour` z `NightRest.OnHourly`), a sen w menu konczy sie na pasku `_menuRest >= _menuTarget` (ulamki godzin). Przyklad z recenzji: 4 kufle przy progu 3
  (odespac 7 h), sen od 21:00, pobudka ok. 04:40 z pelnym paskiem, tiki 6 x 1.0 + 0.6 = 6.6 < 7 - pijany, a w marszu tik juz nie dolicza. Zmiana:
  `InnDrink.OnSleepInit` (NightRest.SleepInit) zapamietuje odpoczynek od kufla przy polozeniu sie, `InnDrink.OnWake` (NightRest.LeaveSleep i EndSleepQuiet,
  przed TownInns) ustawia `_slept = max(_slept, przy polozeniu + pasek)` i trzezwi od razu (kac od pobudki, komunikat od razu). Pelny pasek zawsze = odespane
  (cel paska >= brakujace godziny). Linia logu "Kufle (m98): pobudka (pobudka|sen przerwany) - pasek X h, pelny pasek tak/nie, ... -> odespane|dalej pijany";
  nowy prog **KP-8**: pobudka z pelnym paskiem i "dalej pijany" = 0.
- **[gra, wazne] PRAWDZIWE - kac znosil sie z premia lozka.** Dowod: NightRest.SpeedPostfix najpierw kara kaca (-10%), potem przy dlugu 0 `TownInns.SpeedBonus`
  (+10% porzadna karczma, +12% dobry dom): 0.90 x 1.10 = 0.99. Zmiana: `InnDrink.SpoilsRest` (pijany albo kac) - SpeedPostfix / MoralePostfix nie dodaja premii
  lozka; `TownInns.Wake` dopisuje do komunikatu pobudki zdanie o kacu (albo "you are still drunk") i do logu "kara kufli (m98) - premia wstrzymana". Jedna zasada:
  kto zapil noc, temu lozko nie daje wypoczynku, dopoki trwa kara kufli.
- **[gra, wazne] PRAWDZIWE - "uslyszane" po tekscie wracalo.** Dowod: tekst turnieju zmienia sie z doba ("only today" / "yesterday" / "a few days ago") i cena
  nagrody, bandy i wojska maja pozycje z biezacej godziny (Where), przesada z ziarna doby. Zmiana: `Item.Id` - tozsamosc wiesci, skrot ksiegi z Id (tekst tylko
  zapasowo): turniej = miasto + doba ogloszenia; swaty = bohater; banda = partia + miejsce (najblizsza osada do 25 jedn., dalej kratka 50 jedn.) + liczba
  spalonych wsi; kryjowka = osada; wojsko = partia przywodcy + cel (oblega X / idzie na X / zbiera sie przy X / w marszu + miejsce); duzy oddzial pana = partia +
  miejsce; karawana = partia + cel; ceny = towar + dwa miasta; wiesc wojenna = rodzaj + doba + strony + miejsce; trwajace oblezenie = osada + oblegajacy;
  "wojna/pokoj krolestwa" = krolestwo + lista wrogow. Ta sama banda w tym samym miejscu nie wraca; gdy przejdzie w inne miejsce albo spali wies - to nowa wiesc
  (warta kufla). Zapis arm_drink bez zmian formatu (skroty uint) - stare wpisy z tekstu po prostu nie pasuja (galaz nie byla wgrana).
- **[gra, technika, drobne] PRAWDZIWE - "A tourney is being held at here in X".** Zmiana: przyimek w galezi - "held here in X" / "held at X, a day's ride to the ...".
- **[gra, drobne] PRAWDZIWE - swaty pod BK sprawdzaly za malo.** Dowod (dekompilacja): `BKMarriageModel.IsSuitableForMarriage` sprawdza tylko zywy, nie w niewoli,
  rod nie banda, nie w bitwie/armii, wiek; `DefaultMarriageModel` dodatkowo IsActive, IsLord, !IsMinorFactionHero, !IsNotable, !IsTemplate i
  `IMarriageOfferCampaignBehavior.IsHeroEngaged`. U Jeffa czynny ROT > BK. Zmiana: `Candidate` sprawdza te warunki domyslnego modelu sam (przed modelem czynnym).
- **[gra, drobne] PRAWDZIWE - sen przy kowadle po kuflach za dlugi.** Dowod: `SmithMenu.AnvilShift` bral `NightRest.NeededHours()` (u pijanego = max(potrzeba,
  reszta + odpoczynek doby) - menu snu odejmuje odpoczynek doby, kowadlo nie). Zmiana: `NightRest.NeededHoursFresh()` = potrzeba + ExtraSleepH(potrzeba, 0)
  = max(potrzeba, brakujace do odespania); trzezwy - jak dotad.
- **[gra, drobne] PRAWDZIWE - zajazd wroga albo rabowany nalewal kufel.** Zmiana: `TownInns.RoadDrinkBarred` - te same warunki wojny i rabunku co nocleg
  (`RoadAllowed`), te same napisy; opcja szara z powodem, skutek tez sprawdza.
- **[gra, drobne] PRAWDZIWE - "You wake with a hangover" o 2:00 w obozie na mapie.** Zmiana: po menu snu komunikat daje `OnWake` ("You wake with a hangover");
  odespane w postoju lub obozie bez menu snu - "The ale wears off, but a hangover follows - ...".
- **[gra, drobne] PRAWDZIWE - budowa puli w warunku opcji psula srednia "budowa srednio".** Zmiana: licznik budow `_dBuilds` (Show bez kufla i Pool kufla),
  "budowa srednio" = czas / liczba budow (bez kufla to samo co dotad: jedna budowa na sluchanie); etykieta ZJ-5b w sprawdz_logi poprawiona. Zostaje (opisane, nie
  zmieniane): budowa puli przy samym wejsciu do karczmy nadaje imiona herszcom (_dNameNew) i liczy plotki wstrzymane przez kruki (Ravens) takze bez sluchania -
  to liczniki i ksiega imion, bez wplywu na zloto i swiat.
- sprawdz_logi --grupa m98plotki: linia "kufel w ... - zaplata nie przeszla" (kufel bez zaplaty = bez plotek) nie jest juz liczona jako nieczytelna w KP-3
  (dotad sztuczny log z ta linia dawal KP-3 NIE); liczba w opisie progu.
- Tekst recenzji technika byl w zadaniu uciety po drugim zgloszeniu - poprawione sa zgloszenia, ktore byly widoczne.

**Status:** DO SPRAWDZENIA (galaz w-toku/m98-plotki, nie wgrane).

**Test w grze krok po kroku:**
1. Wczytaj zapis, wejdz do miasta -> "Go to the tavern district" -> karczma. Opcja ma napis "Buy a tankard of ale and listen to the gossip (1 d)"; najedz -
   podpowiedz mowi cene, ile plotek za kufel przy Twoim Uroku i ile kufli do upicia.
2. Kliknij: komunikat "You paid 1 for a tankard of ale at X.", okno z naglowkiem "You buy a tankard of ale (1 d) ... Over it you hear N pieces of news
   - another round would loosen more tongues (M more)". N wedlug Uroku (ponizej 50 - 1).
3. Kliknij jeszcze raz - nowe plotki, bez powtorek. Po wysluchaniu wszystkiego opcja szara: "You have heard all the talk here...".
4. Sprawdz turnieje (miasto blisko trwajacego turnieju) i swaty - nazwy i miasta zgadzaja sie z encyklopedia i menu turnieju.
5. Wypij ponad prog (np. w kilku karczmach) - "You are drunk - your party moves 15% slower...". Na mapie najedz na predkosc - pozycja "Drunk".
6. Poloz sie spac (O -> Bed down / nocleg) - sen trwa dluzej; od razu przy pobudce (pelny pasek) "You wake with a hangover..."; predkosc "Hangover" -10%,
   morale -10% do 18:00, potem "Your head clears - the hangover is gone.". W logu "Kufle (m98): pobudka (pobudka) - ... pelny pasek tak ... -> odespane".
   Nocleg w karczmie po kuflach: komunikat "Rested in a bed ... But the ale still pounds in your head - the bed does you no good until the hangover passes.",
   w rozpisce predkosci nie ma "Slept in a bed", jest "Hangover".
7. Zapisz w stanie pijanym, wczytaj - stan zostaje. Log: `python -I tools/sprawdz_logi.py --grupa m98plotki <Armoury-*.log>` - wszystkie progi TAK.
8. Zajazd przy szlaku: to samo, zaplata do wsi okregu (linia kufla: "kiesa wsi ..., podatek pana ..., korona (DS) ..."). Zajazd pana, z ktorym jestes
   na wojnie, albo wies rabowana - opcja szara z tym samym powodem co nocleg.
9. Ten sam turniej nastepnego dnia w tej samej karczmie nie wraca jako nowa plotka; banda wraca dopiero, gdy jest gdzie indziej.
10. Kuznia: praca przy kowadle po kuflach - sen przy kowadle najwyzej tyle, ile brakuje do odespania (nie kilkanascie godzin).
