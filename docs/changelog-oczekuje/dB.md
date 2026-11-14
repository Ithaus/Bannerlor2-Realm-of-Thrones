# Grupa B (galaz noc/d-gospodarka z origin/noc/scal-perki e46a8c9a): decyzje Jeffa D3, D5, D6, D7, D8, D12, D13, D14 - Armoury

Jeff 14.11 (czat, odpowiedz na D1-D16 po serii 505-515): "D1 tak / D3 - tak, pol porcji / D5 - tak z miasta / D6 - C / D7 - o 25% krocej zrobmy, o polowe to za duzo / D8 - a / D9 - tak / D12 - tak do 75% / D13 - tak / D14 - tak / D16 - odzysk". Opis pytan: docs/audyty-2026-11-14/PERKI-DECYZJE.md:66-67 i sekcje "Decyzje dla Jeffa" wpisow 509-514 w CHANGELOG.md. D1, D9, D16 - grupa A; D2, D4, D10, D11, D15 - czekaja na Jeffa (nie ruszane).

Build: `dotnet build Armoury/Armoury.csproj -c Release` - 0 bledow, Armoury.dll md5 97df3024. Klucze recznie w Settings.cs i McmSettings.cs (bez gen_mcm.py); wszystkie nowe - Armoury.json Jeffa ich nie ma, dzialaja wartosci domyslne z kodu. Jedyny usuniety klucz: IronMakerBloomBonus (511, niewgrany - nie ma go w Armoury.json).

---

## D3: jency przy pracy jedza pol porcji, glod oslabia prace, smierc z glodu po tygodniu laski (PrisonLabor509)

**Problem:** decyzja Jeffa "D3 - tak, pol porcji" (509, pytania 1 i 2: "jency jedza pol porcji - czy pracujacy pelna? glod (Food 0) nie zmienia pracy ani zdrowia jencow - czy glodni maja pracowac slabiej / umierac?"; PERKI-DECYZJE.md:67 "glod oslabia prace; smierc/ucieczki - zgodnie z propozycja").
**Przyczyna:** 509 karmi jencow lochu z zapasow twierdzy (DungeonPrisonersEat, pol porcji), ale przy Food 0 nic sie z nimi nie dzieje - jedza "z niczego" i pracuja jak syci.
**Zmiana:**
- Pol porcji - bez zmian (DungeonPrisonerFoodShare 0.5 zostaje, pracujacy nie jedza wiecej).
- Armoury/src/PrisonLabor509.cs:129 `HungerWork(town)` - przy DungeonPrisonersEat i Food 0 (gra `Settlement.IsStarving` = FoodStocks <= 0) mnoznik pracy PrisonLaborHungryWorkPercent (50%); :122 `Share` mnozy przez niego - jedna liczba dla punktow budowy (EngExtra, AddGame/AddGameInt), dymka "Prisoners at work" i wypadkow przy pracy.
- :308 `HungerDay` (z Daily :374, kazda twierdza we wlasnym try): licznik kolejnych dob glodu twierdzy z jencami (slownik StringId -> doby, stan sesji; czyszczony przy nowej kampanii i przy DungeonPrisonersEat wylaczonym); po PrisonHungerGraceDays (7) dobach co dobe ginie PrisonHungerDeathPercent (5%) szeregowych jencow lochu (RoundRandomized; :281 `StarveOne` - najpierw ranni, potem zdrowi; bez bohaterow - gra ich nie glodzi; bez Innych - nie jedza). Zdjeci z lochu -> PeopleBook.DieShares(Take(..., FDieHunger)) - nowa przyczyna ksiegi ludzi PeopleBook.Origin.cs:60 `FDieHunger = 66` (FKinds 67, FName "glod jencow lochu (D3)", linia ksiegi: "wyjscia ... glod jencow lochu N" i "dokad: polegli ... z glodu w lochu N"). Komunikat dla lochu gracza (EN): "N prisoners starved to death in the dungeon of X - its food stores are empty."
- PROPOZYCJA (opis, bo propozycja 509 nie miala liczb): tydzien laski i 5%/dobe = te same wartosci co glod zalogi GL (SiegeWalls2: SiegeStarveGraceDays 7, SiegeStarveLossPercent 5) - jedna zasada dla glodu w murach. Ucieczek z glodu NIE ma (509 pkt 4: praca nie zwieksza ucieczek; ucieczka przez mur GL dotyczy zalogi oblezonej twierdzy, nie jencow w lochu).
- Klucze (Settings.cs:1356-1358, McmSettings.cs grupa "Army purchases" po Dungeon Prisoner Food Share): PrisonLaborHungryWorkPercent 50, PrisonHungerGraceDays 7, PrisonHungerDeathPercent 5.
**Linie logu:** start "Praca jencow (509): start sesji | ... | glod lochu (D3, przy Food 0): praca x0.5, po 7 dobach laski umiera 5%/dobe szeregowych jencow, ucieczek z glodu brak | ..."; doba "Praca jencow (509): dzien N | ... | jedzenie lochow (...): ... Food/dobe | glod lochu (D3): twierdz z Food 0 i jencami A (w tygodniu laski B), z glodnymi robotnikami (praca x0.5) C, zmarlych z glodu D | Perk509: ...". Ksiega ludzi: "glod jencow lochu (D3) N", "z glodu w lochu N".
**Ryzyko / co sprawdzic - kontrola calosci:**
- Share czytaja: BuildFunding.EngExtra (dymek, PowerIntPostfix, Walls :160/:386, EngPaidNeed), AddGame/AddGameInt, Daily (wypadki) - wszystkie dostaja ten sam mnoznik glodu (liniowy - wzor EngPaidNeed dalej dokladny). IsStarving to odczyt pola - mozna z watkow AI.
- Zgony z glodu jencow vs inne systemy: Famine zabija mieszczan (TownDie), SiegeWalls2 zaloge i milicje, DungeonFate - goraczka lochu gracza; zaden nie dotyka glodu jencow (grep PrisonRoster + glod/starv: brak; RealisticCaptivity - brak IsStarving). Bez podwojnego liczenia.
- Glod tylko gdy jency naprawde jedza z zapasow (DungeonPrisonersEat i latka jedzenia wpieta) - przy wylaczonym kluczu stan glodu czyszczony, jency nie gloduja (jedna zasada: kto nie je z zapasu, ten nie gloduje). Poprawka po recenzji: HungerWork tez sprawdza `_foodWired` (dotad tylko HungerDay i Daily).
- Oblezona twierdza z Food 0: SiegeWalls2 zabija zaloge, my jencow - dwie grupy ludzi, ta sama stopa i laska. Jency umieraja z lochu (mniej ust - wolniejszy spadek zapasu; zgodne).
- Stan sesji bez zapisu: po wczytaniu laska liczy sie od nowa (najwyzej tydzien zwloki) - swiadomie, bez nowego klucza zapisu.
- Wyjatki: HungerDay w try per twierdza, PeopleBook w osobnym try - potkniecia liczone, bez wylacznika. Koszt: petla po rosterze lochu raz na dobe tylko w twierdzach z Food 0.
**Status:** DO SPRAWDZENIA (nie wgrane).

---

## D5: ShipwrightsHand (NavalDLC) - rekrut z ochotnikow miasta (Perk510Gold)

**Problem:** decyzja Jeffa "D5 - tak z miasta" (510: "ShipwrightsHand: rekrut z niczego wylaczony bez platnika - czy chce rekruta z ochotnikow miasta").
**Przyczyna:** NavalDLC ShipTradeCampaignBehavior.OnSettlementEntered (:442-462) dopisuje zalodze BasicTroop z niczego za kazdy konwoj; 510 to wylaczyl (perk martwy).
**Zmiana:**
- Armoury/src/Perk510Gold.cs:388 w prefiksie NavalEnteredPrefix (warunek gry bez zmian: GarrisonWagePaymentLimit > zold + 5) - `HandVolunteer(st, want)` (:422), want = SecondaryBonus perka (1). Warunki: osada nie w bitwie / oblezeniu (warunek gry dla werbunku zalog), rod pana z glowa i zlotem > 0 (jak zaloga frontu 326c - bez werbunku na kredyt), kraina z wolnymi rekami (FreeHands.Braked), notabl MIASTA ma ochotnika w slotach dostepnych zalodze (MaximumIndexGarrisonCanRecruitFromHero - jak gra). Wtedy wywolanie autowerbunku gry GarrisonRecruitmentCampaignBehavior.TickAutoRecruitmentGarrisonChange (odbiciem, z flaga ArmsFromPeople.FrontGarrisonCall - jak 326c: bez licznikow A5/K8) z limitem sztuk = perk: postfiks :453 `HandCapPostfix` (Priority.Last) na GetAutoRecruitmentGarrisonChangeExplainedNumber - LimitMax(want) tylko w tym wywolaniu tej twierdzy ([ThreadStatic]). POPRAWKA PO RECENZJI: gra trzyma kandydatow w SortedSet od najnizszego zoldu (miasto i wsie razem), wiec sam limit 1 bral zwykle najtanszego rekruta ze wsi - teraz postfiks `HandTownOnlyPostfix` na RepopulateVolunteerListCache (tylko w tym wywolaniu tej twierdzy) usuwa z listy ochotnikow notabli spoza miasta; bez tej latki (albo bez pola listy) - nikt. Przed werbunkiem warunek gry CanSettlementAutoRecruit (autowerbunek zalogi wlaczony, bilans jedzenia miasta > 0) - jak dzienny werbunek gry i zaloga frontu 326c.
- Droga gry daje cala reszte: slot notabla oprozniony; ksiega ludzi (A5Pre/A5Post) zdejmuje czlowieka z DOM; rod pana dopisuje koszt werbunku (AutoRecruitmentExpenses) -> GarrisonRecruitFee oddaje go notablowi; limity zoldu i miejsca w zalodze dzialaja.
- Wpiecie: Perk510Gold.cs:517 (ApplyAll, przy starcie gry). Klucz PerkShipwrightsHandVolunteer (Settings.cs:609, McmSettings.cs grupa "The law of the battlefield"), wymaga PerkNavalPortGoldPaid.
**Linie logu:** start "Perki 510 (okupy i zloto): latki przy starcie gry - wpiete: ..., D5 ShipwrightsHand - limit ochotnikow (GarrisonRecruitmentCampaignBehavior.GetAutoRecruitmentGarrisonChangeExplainedNumber), ..."; doba "... ShipwrightsHand - rekrut z niczego nie dany: A wjazdow, do B ludzi; ochotnik miasta (D5, tak): wzieto C ludzi przy D wjazdach, bez ochotnika w miescie E, zablokowane (bitwa/oblezenie, pusta kiesa rodu, kraina bez wolnych rak, limity gry) F, autowerbunek twierdzy wylaczony albo bilans jedzenia <= 0 (warunek gry) G, ochotnikow wsi zdjetych z listy (tylko miasto) H, bez filtra listy (pole gry) I"; start: wpiete "D5 ShipwrightsHand - tylko ochotnicy miasta (GarrisonRecruitmentCampaignBehavior.RepopulateVolunteerListCache)".
**Ryzyko / co sprawdzic - kontrola calosci:**
- TickAutoRecruitmentGarrisonChange latkuja: PeopleBook A5Pre/A5Post (ksiega ludzi), GarrisonRecruitFee TickPre/Post (oplata do notabla), ArmsFromPeople A5MeasurePre/Fin, ClanBudget GarRecruit (Straz), GarrisonPatrol364 - wszystkie dzialaja tak samo jak przy zalodze frontu 326c, ktora od dawna wola te metode tym samym sposobem (FrontGarrisonCall wylacza tylko liczniki A5/K8 i 364 InA5Window). Nowa latka na GetAutoRecruitmentGarrisonChangeExplainedNumber obok A5LimitPostfix i 364 - dziala tylko przy _handCap > 0 i tej twierdzy (poza tym return).
- Ochotnik nie z niczego: czlowiek z DOM (ksiega), oplata rodu do notabla (GarrisonRecruitFee) - zamknieta ekonomia. Wiecej ludzi w zalodze = wiecej zoldu (limit zoldu gry pilnuje).
- Wywolanie w zdarzeniu wjazdu konwoju (watek glowny, zdarzenie kampanii) - gra sama zmienia roster zalogi w tym samym zdarzeniu (NavalDLC). Koszt: odbicie raz na wjazd konwoju do portu z zarzadca z perkiem.
- Gdy inna latka zablokuje autowerbunek (np. BK w pokoju) - nikt nie przychodzi, licznik "zablokowane".
- Filtr listy (RepopulateVolunteerListCache): nikt inny w Armoury ani BetterEconomy nie latkuje tej metody (grep + dekompilacja); lista jest budowana od nowa przy kazdym wywolaniu GetAutoRecruitmentGarrisonChangeExplainedNumber, wiec skrocenie jej w oknie HandVolunteer nie dotyka dziennego werbunku gry, dymka zalogi ani zalogi frontu 326c. Latka zakladana przed latka D5 na wolajacym (zastepnik wolajacego budowany po niej). Limit zoldu gry liczy sie wtedy na ochotnikach miasta - to samo, co bralby werbunek z samego miasta.
- CanSettlementAutoRecruit wolany odbiciem - wersja zalatana (TownGranary pkt 6: bilans miasta, ktore ma zapas, nie spada ponizej +0.01 w tym tescie) - ta sama co dzienny werbunek gry i 326c.
**Status:** DO SPRAWDZENIA (nie wgrane; NavalDLC i perk Boatswain u zarzadcy portu - w autotescie rzadkie).

---

## D6: IronMaker - przekucie lupy stratne, perk zmniejsza strate (zamiast +20% z niczego)

**Problem:** decyzja Jeffa "D6 - C" (511 decyzja 1, wariant c: "podstawa stratna (np. 24 sztaby = 80% masy) i perk zmniejsza strate do najwyzej 30").
**Przyczyna:** 511 MaterialLaw: lupa (15 kg surowki) -> 30 sztab po 0.5 kg = cala masa; z IronMaker x1.2 = 36 sztab = 18 kg z 15 kg (3 kg metalu z niczego na lupe).
**Zmiana:**
- Armoury/src/Perk511Smithy.cs:175 `BloomLossPct` i :190 `BloomBars`: sztaby = masa lupy w sztabach (30) x (1 - strata / 100), co najmniej 1, NIGDY ponad mase lupy. Strata BloomForgeLossPercent (20) - 24 sztaby; z IronMaker IronMakerBloomLossPercent (0) - 30 sztab (nie wiecej niz bez perka).
- Uzasadnienie 20%: przekucie surowej lupy na sztaby (zgrzewanie, wyciskanie zuzla, zgorzelina przy kazdym nagrzaniu) tracilo w kuzni historycznej od ok. 20 do ponad 40% masy; 20% to dolna granica i ta sama strata co kazdy dalszy stopien naszej kuzni ("5 sztabek + wegiel -> 4", MaterialLaw.cs:128-133, decyzja Jeffa) - jedna zasada na przekucie. IronMaker ("sprawny hutnik") wydobywa z lupy cale zelazo - 30 sztab = 15 kg, ani grama ponad.
- MaterialLaw.cs:124-126 - komentarz i wywolanie (bez zmiany formuly poza BloomBars). Klucz IronMakerBloomBonus usuniety (Settings.cs, McmSettings.cs - kopia i pole MCM); nowe BloomForgeLossPercent 20, IronMakerBloomLossPercent 0 (Settings.cs:140-141, McmSettings.cs grupa "Mending it yourself").
**Linie logu:** start "Perki kuzni (511): ..., przekucie lupy (D6): strata 20% masy, z IronMaker 0%, ..."; doba "Perki kuzni (511): doba | ... | IronMaker tak (przekucie lupy D6: strata 0% masy) | ..."; przetop (NightEcon "stopnie z weglem"): "IronOre 1 + wegiel 1 -> Iron1 24" (bez perka), 30 z IronMaker.
**Ryzyko / co sprawdzic - kontrola calosci:**
- BloomBars wola tylko MaterialLaw.RefinePostfix (galaz lupy przy cenach historycznych + Bloomery); galaz zwyklej rudy (x1.25 IronMaker, stara) i vanilla - bez zmian. GetRefiningFormulas czytaja: ekran kuzni, NightEcon (linia), Probes306 (sonda XP) - widza nowa liczbe.
- XP za przetop (RefineXpPostfix) rosnie z liczba sztab - bez perka mniej XP za lupe (24 zamiast 30) - zgodne z mniejszym uzyskiem.
- REGRES dla gracza bez perka: z lupy 24 sztaby zamiast 30 (strata 3 kg metalu na lupe idzie w zuzel - nie powstaje i nie znika zloto). Cena lupy (Bloomery: surowka 15 kg + wegiel) liczy cala mase - kucie lupy przez gracza bez perka jest teraz o 20% mniej oplacalne niz zakup sztab. Kuznie miast (WorkshopLaw.Needs, CountKg) licza metal lupy BEZ straty - dwie zasady przekucia (gracz / warsztat) - do decyzji Jeffa, czy warsztatom tez dac strate (osobna zmiana; tu tylko kuznia gracza, jak 511).
**Status:** DO SPRAWDZENIA (nie wgrane).

---

## D7: PracticalSmith - zbroje i luki kuja sie o 25% krocej (nie o polowe)

**Problem:** decyzja Jeffa "D7 - o 25% krocej zrobmy, o polowe to za duzo" (511 decyzja 2: z PracticalSmith godziny lawy = stamina / 6 po perku - o polowe krocej).
**Przyczyna:** 511 Perk511.Stamina tnie stamine x0.5 (perk gry), a godziny lawy (ForgeLikeTownSmith = false) liczyly sie z tej samej, pobranej staminy.
**Zmiana:**
- Armoury/src/Perk511Smithy.cs:205 `HoursStamina(spent, h)`: stamina do godzin = pobrana x (1 - PracticalSmithTimeCutPercent / 100) / (1 + PrimaryBonus perka) = x0.75 / x0.5 = x1.5 pobranej (czyli 75% staminy przepisu); BK Smith (x0.9) dalej tnie i stamine, i czas (jak w BK). Bez PracticalSmith / klucz PerkSmithStamina wylaczony - bez zmian. Stamina (zmeczenie) dalej -50%.
- Forge.cs:669 (Smith - luki, kusze, amunicja na lawe) i ForgeClock.cs:175 (CraftPost - zbroja z zakladki CRAFT BK): godziny z HoursStamina. "Dawne godziny" dla NoteQueued (licznik _bkQueued, ktory SkipBkWait odejmuje od odsiadki BK = miara BK: stamina pobrana / 6) - BEZ zmiany (Forge.cs:672, ForgeClock.cs:178) - inaczej przetop i rafinacja tracilyby godziny odsiadki.
- Klucz PracticalSmithTimeCutPercent 25 (Settings.cs:143, McmSettings.cs grupa "Mending it yourself").
**Linie logu:** start "Perki kuzni (511): ..., czas lawy z PracticalSmith (D7) -25% (stamina dalej -50%), ..."; doba "... | czas lawy D7: sztuk z godzinami po PracticalSmith N | ..."; przy sztuce "Kuznia jak u kowali (C5)" / "On the bench: ... X hours of work" - godziny = stamina przepisu x0.75 / 6.
**Ryzyko / co sprawdzic - kontrola calosci:**
- HoursOf wola tez bron z ekranu gry ("van") - nie ruszane (tam gra sama tnie stamine perkiem; zakres Jeffa "zbroje i luki"). Amunicja idzie ta sama droga co luki (Forge.Smith) - tez -25%.
- PYTANIE DO JEFFA (z recenzji): po D7 jeden perk ma dwie stawki czasu - zbroje, luki, kusze i amunicja -25%, a bron z ekranu kowalstwa gry dalej -50% (godziny z staminy, ktora gra sama tnie o polowe). Pytanie 511 mowilo "tak samo jak bron z ekranu gry", odpowiedz "o polowe to za duzo" mogla dotyczyc tez broni. Jesli bron tez ma byc -25%: w ForgeClock przy godzinach dla broni z ekranu gry uzyc HoursStamina na staminie pobranej przez gre (jedna zmiana, jedna linia logu). Nie zmienione bez decyzji.
- ForgeLikeTownSmith = true (dni kowala z warsztatu AI) - perk nie dziala na czas jak dotad (u Jeffa false).
- Stamina pobierana, XP pracy (305d - przepis bez perka), okno kowadla (SmithMenu) - bez zmian.
**Status:** DO SPRAWDZENIA (nie wgrane).

---

## D8: zwierzeta jedza pasze przez caly rok (Breeder 512)

**Problem:** decyzja Jeffa "D8 - a" (512 decyzja 5: "Breeder: zwierzeta jedza tylko w zimie (poza stepem) i na pustyni/w sniegu - przez reszte roku rozmnazanie nic nie kosztuje; pasza caly rok = osobna zmiana, albo poza zima nie rodzic wcale?" - wariant a: pasza caly rok).
**Przyczyna:** WinterBite.FoodPostfix doliczal pasze (WinterRealm.HorseFeed) tylko w zimie; poza zima pasze liczy tylko regula BK na pustyni i w sniegu.
**Zmiana:**
- Armoury/src/WinterRealm.cs:342 `YearShare` (klucze AnimalsEatAllYear, FodderOutsideWinterShare 0.25) i :356 `HorseFeed(party, realm, winter, ...)` - ta sama regula co zima (AnimalNeed: konie partii / konni po glowie x stawka BK + zwierzeta taboru x stawka; step bez paszy - wypas; pustynia i snieg - tylko roznica ponad pasze BK); poza zima udzial YearShare, w zimie udzial = wiekszy z (WinterHorseFeedShare x kraina) i YearShare (zima nigdy lzejsza niz reszta roku; Wyspy Letnie i cieple krainy placa co najmniej pasze roczna). Stara sygnatura HorseFeed(party, realm, ...) = winter true. Przy AnimalsEatAllYear wylaczonym - jak dotad.
- WinterBite.cs:195-205 FoodPostfix: poza zima pozycja "Animals eat fodder" = -HorseFeed(winter false) (baza jedzenia - te same mnozniki co ludzie: perki, Rations).
- WinterRealm.cs:503-510 Daily: poza zima linia paszy rocznej (YearLine :628).
- Wartosc 0.25 = polowa paszy zimowej (0.5): w marszu kon dostaje ziarno, reszte daje trawa; kon ok. 1.25 czlowieka. Do zmiany w MCM.
- Klucze (Settings.cs:1123-1124, McmSettings.cs grupa "The marching column" po Winter Horse Feed Share). Opis Breeder Realistic (Settings/MCM) i komentarz Perk512.cs:41 - "mlode jedza jak reszta stada (caly rok)".
**Linie logu:** poza zima co dobe "Pasza przez caly rok (D8, Animals Eat All Year 0.25 pelnej paszy poza zima): dzien N | partii jedzacych A, z pasza B (konnych C, zwierzat w taborach D), +X jedzenia/d razem przed mnoznikami (pelna pasza tych partii Y/d) | bez paszy: step S, pustynia (...) P, snieg/zamiec (...) Q, bez zwierzat Z; doplata roznicy ponad pasze BK: ... | potkniecia dzis 0 | czas T ms."; w zimie linia "Zima wedlug krain (E4/1)" jak dotad (pasza = wieksza z zimowej i rocznej).
**Ryzyko / co sprawdzic - kontrola calosci:**
- ZMIANA BILANSU SWIATA (wazne): kazda partia jedzaca (lordowie AI, karawany z jucznymi, gracz) placi pasze przez caly rok - np. lord 150 ludzi / 60 konnych: ludzie 7.5, pasza +3.75 jedzenia/d (+50%); karawana 30 mulow +1.1/d. Wiecej zakupow jedzenia (HungerLaw, FoodThrift), czesciej glod partii (DesertionLaw, morale) - sprawdzic w autotescie liczbe partii glodnych przed/po (linie HungerLaw i E4/1). Gdy za ciezko - FodderOutsideWinterShare nizej.
- Jedna zasada paszy: ta sama AnimalNeed i ta sama regula terenu (step, BK na pustyni/sniegu bez dubla - roznica ponad BkNeed) co zima E4/1; zima = max(zimowa, roczna) - bez sumowania dwoch pasz.
- Latka jedzenia WinterBite wpinana tylko przy WinterBiteEnabled (start gry) - bez niej pasza roczna nie dziala (linia D8 tez nie).
- Breeder: mlode (512) jedza od urodzenia - rozmnazanie ma koszt caly rok (cel decyzji). Bramka "partia glodna nie rodzi" bez zmian.
- Watki: HorseFeed czyste (odczyt), jak dotad w zimie.
**Status:** DO SPRAWDZENIA (nie wgrane; wymaga testu bilansu jedzenia AI).

---

## D12: Athletics.WalkItOff i Medicine.WalkItOff sumuja sie do 75% (513)

**Problem:** decyzja Jeffa "D12 - tak do 75%" (513 decyzja 2: "Medicine.WalkItOff = to samo co Athletics, bez sumowania - czy ma sie sumowac (np. oba = 75%)?").
**Zmiana:** Armoury/src/Perk513Healing.cs:192 MarchPct (wolane z SlowHealing.RestCut): wodz z Athletics.WalkItOff I chirurg z Medicine.WalkItOff (MobileParty.HasPerk - te same role co gra) -> stawka marszu lekko rannych bohaterow WalkItOffBothPercent (75) zamiast WalkItOffLightWoundPercent (50); jeden perk - jak dotad. Ciezko ranni (HP <= 20) dalej przycinani do MovingHealPercent tym samym oknem (skala _raiseLo / _raiseHi = 10/75). Klucz WalkItOffBothPercent 75 (Settings.cs:324, McmSettings.cs grupa "The slow mending"); opis WalkItOffLightWoundPercent poprawiony ("both together give Walk It Off Both Percent").
**Linie logu:** start "Perki leczenia (513): start sesji - ... | D12 oba WalkItOff naraz 75% stawki | D13 Bush Doctor w samej wsi TAK | ..."; doba "Perki leczenia (513): doba N - ... | D12 oba WalkItOff (75%) podniesien K | ...".
**Ryzyko / co sprawdzic:** okno 37 (WinPrefix/HealPrefix) skaluje ciezko rannych przez _raiseLo/_raiseHi - dziala dla dowolnego hi (75 tez). Linia 500-a "sciete" spada dla partii z oboma perkami (zamierzone). Gracz i AI - ta sama regula.
**Status:** DO SPRAWDZENIA (nie wgrane).

---

## D13: BushDoctor dziala tez w samej wsi (513)

**Problem:** decyzja Jeffa "D13 - tak" (513 decyzja 3: "BushDoctor w SAMEJ wsi (opis perku 'while waiting in villages', gra go tam nie liczy) - dopisac?").
**Przyczyna:** DefaultPartyHealingModel :179-197 / :267-283 - partia w osadzie idzie galezia osady (AGoodDaysRest, GoodLodgings), BushDoctor tylko poza osada przy ostatnio odwiedzonej wsi.
**Zmiana:** Armoury/src/Perk513Healing.cs:410 `InVillage` i :426 w CampPostfix (postfiks dziennego leczenia bohaterow i szeregowych, bez jencow, tylko dodatni wynik): partia w samej wsi (CurrentSettlement wies, nie pladrowana, nie w najezdzie, bez bitwy) z chirurgiem BushDoctor -> AddFactor SecondaryBonus (+20%, wartosc gry), pozycja "Bush Doctor (in the village)". Gracz i AI. Klucz BushDoctorInVillage (Settings.cs:325, McmSettings.cs grupa "The slow mending"), niezalezny od CampHealingPerks.
**Linie logu:** doba 513 "... 63 Bush Doctor w obozie A, D13 w samej wsi B (wywolania modelu) ...".
**Ryzyko / co sprawdzic:** bez dubla - gra w osadzie BushDoctor nie liczy, galaz obozu (Camped) wymaga CurrentSettlement == null. Leczenie w osadzie = spoczynek (500/500-a) - premia liczy sie w pelnej stawce. Garnizony i milicja pominiete (jak 63/68).
**Status:** DO SPRAWDZENIA (nie wgrane).

---

## D14: DiplomaticTies gracza - +1 relacji z obcymi wladcami raz w tygodniu (514)

**Problem:** decyzja Jeffa "D14 - tak" (514: "BK HeroRelations.UpdateRelations pomija Hero.MainHero - perk dziala dla lordow AI, gracz nic nie zyska").
**Zmiana:** Armoury/src/Perk514.cs:462 `TiesWeekly(day)` (z Daily :681): co 7 dob (doba % 7 == 0), gdy GRACZ ma LordshipDiplomaticTies i BkPerkHalvesCourt - kazdy zywy wladca obcego krolestwa (Kingdom.All, nie wyeliminowane, nie krolestwo gracza) +1 relacji z graczem (ChangeRelationAction, jak BK), tylko gdy relacja < cel BK tego wladcy wobec gracza (BKRelationsBehavior.GetRelations(wladca).GetRelationsTarget(gracz) odbiciem - ten sam cel, do ktorego BK dryfuje lordow; zawiera +3 perka z RelModPost). Nigdy ponad cel. Komunikat (EN) "Diplomatic Ties: your standing with N foreign rulers rose a little this week." Klucz DiplomaticTiesPlayerWeekly (Settings.cs:4058, McmSettings.cs grupa "Banner Kings perks as described (514)"); opis BkPerkHalvesCourt odsyla do niego.
**Linie logu:** raz w tygodniu "Perki BK (514): D14 Diplomatic Ties gracza (tydzien): dzien N: wladcow obcych A, +1 relacji B, juz na celu BK albo wyzej C (imie x->y (cel z), ...) | sesja: +1 razem S."; brak odbicia BK - "BRAK odbicia BK (...)".
**Ryzyko / co sprawdzic:** cel BK liczony modelem BK (frakcja wiary, cechy, wasalstwo, perk) - gdy cel ponizej obecnej relacji, nic (perk nie obniza). Bez zlota. Koszt: ok. 20 wywolan modelu BK raz na tydzien. Odbicie BKRelationsBehavior odswiezane przy nowej kampanii (Campaign.Current). Wyjatek per wladca - potkniecie, bez wylacznika.
**Status:** DO SPRAWDZENIA (nie wgrane).

---

## Recenzja (14.11, commit 0b0b68ad) - co sprawdzone i co poprawione

Sprawdzone w dekompilacji gry (tw-campaign GarrisonRecruitmentCampaignBehavior) i w kodzie Armoury.

1. **D5 - ochotnik ze wsi zamiast z miasta (sredni) - PRAWDZIWE, POPRAWIONE.** RepopulateVolunteerListCache wklada do `_volunteerListCache` (SortedSet, VolunteerTroop.CompareTo = od najnizszego zoldu) notabli miasta i wsi przypisanych (Normal); TickAutoRecruitmentGarrisonChange bierze ElementAt(0..n). Przy limicie 1 szedl najtanszy z miasta i wsi razem. Poprawka: Perk510Gold `HandTownOnlyPostfix` (postfiks RepopulateVolunteerListCache, Priority.Last, tylko przy `_handCapTown == town` w watku wywolania) - RemoveWhere ochotnikow, ktorych notabl nie jest w `town.Settlement.Notables`. Gdy latka sie nie wepnie - HandVolunteer nie werbuje (licznik "zablokowane"); gdy pola listy nie ma albo postfiks rzuci - HandCapPostfix daje LimitMax(0) (licznik "bez filtra listy"). Komentarz w kodzie, opis MCM/Settings (EN) i sekcja D5 poprawione.
2. **D5 - brak CanSettlementAutoRecruit (sredni) - PRAWDZIWE, POPRAWIONE.** Gra (OnDailySettlementTick) i 326c sprawdzaja warunek (GarrisonAutoRecruitmentIsEnabled, FoodChange > 0, bez bitwy/oblezenia). HandVolunteer wola go teraz odbiciem przed werbunkiem; false -> licznik "autowerbunek twierdzy wylaczony albo bilans jedzenia <= 0". Port gracza z wylaczonym autowerbunkiem i glodne miasto nie dostaja ludzi.
3. **D7 - dwie stawki jednego perka (niski) - PRAWDZIWE jako niespojnosc, BEZ ZMIANY KODU.** Zakres decyzji Jeffa (zbroje i luki) spelniony; czy bron z ekranu gry tez -25% - pytanie dopisane w sekcji D7 (do decyzji Jeffa, zasada 3: nie dodawac przy okazji).
4. **D3 - HungerWork bez `_foodWired` (niski) - PRAWDZIWE, POPRAWIONE.** PrisonLabor509.HungerWork zwraca 1 przy `!_foodWired` - ten sam warunek co HungerDay i Daily.

Build po poprawkach: Armoury 0 bledow, Armoury.dll md5 997c7399. Status calosci: DO SPRAWDZENIA (nie wgrane).
