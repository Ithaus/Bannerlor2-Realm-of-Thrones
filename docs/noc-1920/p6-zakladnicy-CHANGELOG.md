## 2026-10-20 (fala 2, zadanie "zakladnicy", pomysl R3-03c z POMYSLY-35 nr 5) - WYCHOWANKOWIE PO PRZEGRANEJ WOJNIE

**Mod:** Armoury | **Galaz:** `w-toku/p6-zakladnicy` (od 91bf9c63 = n22-calosc: WGRANIE 92 + 15 galezi fali 1) | **Tor:** M (bez gry - zakaz uruchamiania;
build Armoury: kod 0, 0 Error(s)) |
**Klucz (nowy, domyslnie TAK; w `Armoury.json` Jeffa go nie ma - wejdzie domyslny z kodu):** `WardsOfPeace` ("Wards Of Peace", grupa MCM "Ransoms by wealth (stage 2)",
zaraz po "Peace Release After Day"). | **Zapis:** SaveText `arm_wards` ("w1|pokoje czekajace|wychowankowie|liczniki"); brak klucza (kampania save040) = brak wychowankow.

**Dla Jeffa (prosto):**
- Gdy krolestwo przegra wojne i zawrze pokoj, a ten pokoj przetrwa dobe, mlody czlonek rodu krolewskiego przegranych (10-20 lat, bez malzonka, nie glowa rodu
  i nie jedyny dziedzic, bez wlasnej druzyny) jedzie na 2 lata do siedziby rodu krolewskiego zwyciezcow jako wychowanek - jak Theon w Winterfell.
- Zostaje w swoim rodzie (bez zmian w slubach i dziedziczeniu), tylko mieszka w obcym zamku; AI nie daje mu druzyny ani urzedu. Po 2 latach wraca do domu,
  a wychowanek i gospodarz lubia sie bardziej (+10, glowy rodow +5).
- Jesli jego krolestwo zerwie pokoj i wypowie wojne zwyciezcy - chlopak laduje w lochu jako jeniec. Jesli wojne wypowie zwyciezca - wychowanek wraca do domu
  wolny. Krolestwa AI, ktorych krew siedzi u wroga, rzadziej zrywaja pokoj.
- Dotyczy tez Ciebie: Twoje dziecko moze pojechac do dawnego wroga, a u Ciebie moze zamieszkac obcy mlodzian (porozmawiasz z nim w zamku). Dostaniesz krotki
  komunikat przy wyjezdzie, powrocie i zamianie w jenca. Wojny fabularne ROT i "pokoje na jeden dzien" nikogo nie wysylaja.

**Problem / zrodlo pomyslu:** POMYSLY-35 nr 5 (R3-03c), Jeff 20.10: "28 - nie, 29 - nie. POzostale tak", potem "Rob od razu fale 2". Audyt
`docs/audyt-2026-10-09/10-pomysly.md` Z17, `04-wojna-armie-lupy-okupy.md` N2 (zakladnik po pokoju), `11-trybut-reparacje.md:74` (w GoT wojny konczy sie zakladnikami
i malzenstwami - Theon). W kodzie przed paczka: nic (grep hostage/zakladni/wychowan w Armoury/src = 0). Dowod z logu testu 160 dob na WGRANIU 91
(`logi-wzorcowe/Armoury-2026-10-20_00-44-29.log`, linie "Pokoj a jency (M6a-3)"): 13 pokojow krolestw, z tego 11 "znow w wojnie" po dobie (fabularne ROT),
2 prawdziwe (dzien 109079 Dragonstone - House Baratheon of King's Landing, dzien 109125 House Targaryen, Aegon - House Targaryen). Spodziewana skala: 1-2 wychowankow
na 160 dob (rzadkie zdarzenie fabularne, nie ekonomia).

**Zmiana:**
- `Armoury/src/PeaceWards.cs` (NOWY):
  - Pokoj: prefiks `MakePeaceAction.ApplyInternal` (jedyna droga pokoju gry, takze Diplomacy `AcceptPeace -> ApplyByKingdomDecision`; nic nie zmienia, tylko zapamietuje).
    Przegrany ustalany PRZED pokojem, bo `FactionManager.SetNeutral` zeruje statystyki lacznika (`StanceLink.ResetStats`), a Diplomacy czysci zmeczenie wojna w zdarzeniu
    `MakePeace` (`WarExhaustionBehavior.ClearWarExhaustion`) - sprawdzone w dekompilacji gry 1.4.8 i Diplomacy 1.4.7. Kolejnosc: (1) Diplomacy
    `WarExhaustionManager.GetWarResult` (Loss jednej strony przy zwyciestwie drugiej; Tie = remis); (2) postep wojny gry `DiplomacyModel.GetWarProgressScore`
    obu stron, roznica >= 75 (prog gry z `GetDailyTributeToPay` i `ApplyWarProgressToRiskScore`); (3) trybut dzienny pokoju (placi przegrany). Inaczej - remis.
  - Wyjatki: wojna fabularna ROT (`ROTStorylineWars.IsWarForced`, odbicie jak w CrashScribe StorylineReparations) - nic; pokoj czeka 24 h czasu kampanii (jak PeaceHold
    M6a-3) i tylko pokoj, ktory przetrwal, wysyla wychowanka.
  - Kandydat: rod panujacy przegranego, Occupation Lord, 10-20 lat (BK nie zmienia `AgeModel` - w dekompilacji BK brak modelu wieku), zywy, nie glowa, nie gracz,
    nie jeniec, bez partii, nie namiestnik, bez malzonka, stan Active albo NotSpawned (dzieci gry), nie wychowanek; w rodzie jeszcze ktos zywy poza glowa i jej
    malzonkiem (nie jedyny dziedzic). Najmlodszy. Jeden na pokoj; rod z wychowankiem u tego zwyciezcy nie wysyla drugiego.
  - Siedziba: `HomeSettlement` rodu panujacego zwyciezcy (miasto/zamek krolestwa zwyciezcy), inaczej miasto, zamek rodu, miasto krolestwa.
    `TeleportHeroAction.ApplyImmediateTeleportToSettlement`.
  - Pobyt: 2 lata x `CampaignTime.DaysInYear` (364 dni z Calendar.cs = 728 dob). Blokady bez Harmony - sluchacze zdarzen gry `CanHeroLeadPartyEvent`,
    `CanMoveToSettlementEvent` (gra codziennie przenosi bezczynnych doroslych lordow po osadach wlasnego krolestwa - `HeroSpawnCampaignBehavior.CanHeroMoveToAnotherSettlement`),
    `CanBeGovernorOrHavePartyRoleEvent` = false dla wychowanka. Postfiks `HeroSpawnCampaignBehavior.GetBestAvailableCommander` (druga petla gry dla rodow AI nie pyta
    `CanLeadParty`): gdy wybrala wychowanka - ten sam wybor gry bez wychowankow (wynik przez `GetHeroPartyCommandScore`). Codziennie: poza siedziba - z powrotem;
    siedziba w obcych rekach - nowa siedziba zwyciezcy.
  - Wojna: `CampaignEvents.WarDeclared` pary (dom, zwyciezca): wypowiada dom -> w najblizszej godzinie `TakePrisonerAction.Apply` (loch twierdzy zwyciezcy, w ktorej
    jest); wypowiada zwyciezca albo wojna fabularna ROT -> wraca wolny. Wojna bez zdarzenia (bezpiecznik) - wolny.
  - Koniec: po 2 latach powrot do siedziby wlasnego rodu, `ChangeRelationAction` wychowanek - glowa rodu gospodarza +10, glowy rodow +5. Smierc, slub, zmiana rodu,
    niewola u kogos innego, druzyna - wpis znika (bez ruchu). Zostal glowa rodu, upadek krolestwa, rod poza krolestwem z chwili pokoju, klucz wylaczony - powrot bez relacji.
  - AI rzadziej zrywa pokoj: postfiks `BannerKings.Models.Vanilla.BKDiplomacyModel.GetScoreOfDeclaringWar` (wersja ExplainedNumber z 6 parametrami - na niej stoi nadpisanie
    float gry x10, `BKWarBarterable` i decyzje wojny; wpinana na klasie deklarujacej metode modelu, ktorego gra uzywa). Krolestwo wypowiadajace, ktorego krew jest
    wychowankiem u celu: -300 na wychowanka (najwyzej 2). Jednostka BK `num3` >= 600 (pakt handlowy -0.25, "nie stac na wojne" -0.4/-0.8 jednostki) - kara to pol jednostki.
    W wyjasnieniu BK linia "Kin held as ward by X".
  - Gracz: komunikaty EN ("X of Y goes to Z as a ward.", "X, a ward at Z, returns home.", "X, a ward at Z, is now a prisoner.", "X leaves Z and goes home free.")
    gdy dotyczy rodu albo krolestwa gracza.
  - Linie: "Wychowankowie (R3-03c): start sesji" (latki), zdarzenia "... dzien N | pokoj: / wyjazd: / wojna: / powrot: / jeniec: / koniec: / siedziba:",
    linia doby "Wychowankowie (R3-03c): dzien N | klucz Wards Of Peace ..." (aktywni, pokoje, przegrany wedlug czego, wyjazdy i powody braku, powroty, jency,
    wyczyszczone, kara oceny wojny, od poczatku, potkniecia, lista).
- `Armoury/src/Settings.cs` (+ `McmSettings.cs` z `tools/gen_mcm.py`): klucz `WardsOfPeace` po `PeaceReleaseAfterDay`.
- `Armoury/src/ArmouryBehavior.cs` - 4 wpiecia: osobna linia `_p6WardsReset` (jak `_p5UrodzajReset`), `SyncData` klucz `arm_wards` (po `arm_fairs`, wlasny try),
  `PeaceWards.RegisterEvents(this)` (po AiDowry), `PeaceWards.OnSessionLaunched()` (po AiDowry).
- `tools/sprawdz_logi.py`: grupa `--grupa p6zakladnicy` (funkcje `p6zakladnicy_main`, `p6zakladnicy_num`, stala `P6ZAK_DAY`), wiersze PZK-0..PZK-6
  (sprawdzone na sztucznym logu - 8 progow TAK/INFO; na logu wzorcowym bez paczki - INFO).

**Ryzyko / co sprawdzic (kontrola calosci):**
- Regresje / kolizje: `MakePeaceAction.ApplyInternal` - nikt z naszych modow go nie latal (PeaceHold lata `PrisonerReleaseCampaignBehavior.OnMakePeaceEvent`, CrashScribe
  StorylineReparations - kwote reparacji Diplomacy); prefiks nie zmienia argumentow ani wyniku. Wychowanek-jeniec po pokoju wychodzi przez PeaceHold (po dobie) jak
  kazdy jeniec; okup - Ransom178 / RealisticCaptivity jak kazdy lord. Kara oceny wojny jest jedna na wywolanie (cache BK trzyma wynik bez kary, postfiks dodaje ja
  przy kazdym zwrocie - takze z cache) - bez podwojnego liczenia.
- Zloto: brak przeplywu (nic do ksiegi Obieg).
- Watki: sluchacze Can* i postfiks oceny wojny czytaja tylko migawki (`HashSet<Hero>`, tablica par) podmieniane w calosci na watku glownym; licznik kary przez
  `Interlocked`; bez alokacji poza wyjasnieniem BK dla ekranu.
- Dziecko gry jest NotSpawned (bez miejsca na mapie; `HeroCreator.DeliverOffSpring` nie zmienia stanu). Przeniesienie robi je Active i stawia w twierdzy; po powrocie
  zostaje Active w domu (gra przy pelnoletnosci przenosi do domu tylko nie-Active - u nas jest juz w domu). Nowosc dla gry: dziecko widoczne w zamku i do rozmowy.
  Sprawdzic w grze rozmowe z dzieckiem-wychowankiem i dziecko w lochu (TakePrisonerAction na dziecku - gra nie ma blokady wieku).
- ROT: wychowankiem moze zostac postac fabularna ROT (np. dzieci Starkow) - jesli fabula ROT jej szuka w domu, moze jej nie znalezc przez 2 lata. Nie sprawdzone (brak w logu).
- Gracz-wladca przegranego: jego dziecko jedzie bez pytania (tak opisuje pomysl). Jesli Jeff zabierze je do druzyny - wpis znika (bez kary).
- Wojny wypowiadane przez sojusz (call to war) licza sie jak wypowiedziane przez krolestwo, ktore przystapilo.
- W tescie 160 dob zaden wychowanek nie wroci po 2 latach (728 dob) - powrot po czasie sprawdzi dopiero dluzszy bieg.

**Status:** DO SPRAWDZENIA (nie wgrane; build kod 0, 0 Error(s); bez autotestu - zakaz).

**Decyzje (projektuje Claude, wylacznik `WardsOfPeace`):**
- Przegrany: Diplomacy -> postep wojny gry (>= 75) -> trybut; remis = bez wychowanka.
- Kara oceny wojny: -300 BK na wychowanka, najwyzej 2 (pol jednostki BK).
- Najmlodszy kandydat; "nie jedyny dziedzic" = w rodzie zywy jeszcze ktos poza glowa i jej malzonkiem.
- Wojna fabularna ROT w czasie pobytu (wymuszona przez ROT) - wychowanek wraca wolny (nie jeniec): to nie wybor rodu.
- Relacje konca pobytu: +10 wychowanek - glowa gospodarza, +5 glowy rodow.
- Klucz wylaczony w trakcie kampanii - wszyscy wracaja do domu bez relacji.

**Test w grze (autotest):** `python -I tools/sprawdz_logi.py --grupa p6zakladnicy <Armoury-*.log>`; w logu:
- `Wychowankowie (R3-03c): start sesji` z "pokoj (MakePeaceAction.ApplyInternal) wpiety", "dowodca partii (GetBestAvailableCommander) wpiety", "ocena wojny BK (...) wpieta";
- codzienna `Wychowankowie (R3-03c): dzien N | klucz Wards Of Peace tak | aktywni ...`;
- po pokoju krolestw: `... | pokoj: X przegralo z Y (wedlug ...)` albo "remis" / "wojna fabularna ROT"; po dobie `... | wyjazd: ... powrot dnia ...` albo "znow w wojnie przed uplywem doby";
- przy zerwaniu pokoju: `... | wojna: ...` i `... | jeniec: ...` albo `... | powrot: ... wolny`.
