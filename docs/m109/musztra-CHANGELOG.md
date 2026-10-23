# m109-musztra - dziennik galezi w-toku/m109-musztra (od 44c03a05 = WGRANIE 103.5)

## 2026-10-23 (2) - Weryfikacja recenzji (ekonomia + kod) i poprawki

Commity: 45336867 (R2 - K3 tylko miasto), 945271c4 (R1, R4, kod R3, ekonomia R3 i R6 - licznik postoju, AI na poranek, odtworzenie po wczytaniu, rezerwa
napraw w miescie, samotest zasady, autotest), 23f9eec6 (progi m109muszt). Build Armoury 0 bledow (kod wyjscia 0), gen_mcm (Armoury 2320 ustawien, zmienil sie tylko
opis DrillTownStay), sprawdz_narzedzie OK, MUSZT-S i MUSZT-S2 (dobry, zly, "zly 2" z sygnaturami bledow recenzji, log sprzed m109) OK, na logu Jeffa 22.10 0 NIE.
Gry nie uruchamialem. Liczby samotestu sprawdzone lustrem arytmetyki w pythonie dla switu 3, 6, 0 i 23 (wszystkie przebiegi TAK); ta sama arytmetyka ze stara
zasada daje A 4/4/3/2/1, B doba 2, C doba 5 - samotest wykrylby oba bledy.

**Werdykty uwag (kazda najpierw proba obalenia w kodzie):**
- R1 (obie recenzje, ZLY STAN) - PRAWDA. `TownStay.Step` zerowal "24 h w polu" kazda godzina w dowolnej osadzie, a `Day = (now - from)/24 + 1` liczyl zegar od
  pierwszego wejscia, takze godziny w polu. Przeliczone recznie i lustrem: wies 1 h, pole 15 h, miasto 2 h, pole 20 h, zamek -> w zamku doba 2 (stara zasada);
  miasto 2 doby, pole 20 h, wies 2 h, pole 20 h, miasto -> doba 5. POPRAWIONE (nizej "Licznik postoju").
- R2 (obie, ZLY STAN) - PRAWDA. `MenPurse.LifeTo` wolal `CrownIncome.NoteOwnSpend` dla kazdej osady; przed m109 wszystkie wolajace (OnLeft, InnPay, markietani)
  byly tylko miejskie, a zamki maja regule B-2 (`CastlePurse.ComesHome`/`LordDuesToday` - tnie sie to, co naprawde wrocilo). POPRAWIONE: warunek `st.IsTown`
  (OnLeft tylko miasta - 1:1).
- Ekonomia R3 (ZLY STAN, mala skala) - PRAWDA. `MenPurse.CampMend` = `OutstandingCost(null)` - sama robocizna poziomu 1; OnLeft, BuyPlayerGaps, MenUpgrade, TownInns,
  AiWear w miescie biora `OutstandingCost(st)` (material z polki przy MendMaterial.MenAndLordsOn, robocizna przy placach miasta). POPRAWIONE: w miescie
  `TroopSelfMend.OutstandingCost(st)` (gracz) / `AiWear.OutstandingCost(mp, st)` (AI); zamek dalej `CampMend` (bez polki).
- R4 (obie, DROBNE) - PRAWDA. Wejscie o 14:00: doba postoju 1 trwala do 14:00 nastepnego dnia, a limit gracza liczy sie od switu - 4/4/3/2/1 (lustro: dokladnie
  tak); AI bralo jeden limit z ostatniej godziny postoju na cale okno 24 h (inaczej niz gracz). POPRAWIONE: doba postoju = poranek; AI liczy na poranek jak gracz.
- Kod R3 (DROBNE) - PRAWDA. `RestoreMasks` tworzy Track z maska z zapisu i `SMask = 0`, wiec godziny postoju sprzed wczytania szly jako pole (bez limitu) -
  pierwszy tick po wczytaniu do 8 h zamiast limitu. POPRAWIONE: `Drill.RestoreBits` (nizej).
- Ekonomia R5 (DROBNE) - PRAWDA (zdanie w "Ryzyko" bylo nieprawdziwe). Nasze +3 dopelnia tylko do 5 wedlug stanu z polnocy, ale `ArmyRestDay` dodaje +5 bez
  pulapu o swicie (armia po >= 6 dobach marszu w miescie: 3 + 5 = 8), a premia lozka karczmy (`TownInns.MoraleBonus` +2/+5/+8) to osobny skladnik modelu, nie
  "ostatnie zdarzenia". PRZYJETE jako zamierzone (dzien odpoczynku w miescie i nocleg w lozku to inne zdarzenia), poprawiony wpis w "Ryzyko" nizej. Gorna granica:
  nasze nigdy nie podnosi powyzej 5, wiec z dniem odpoczynku najwyzej ok. 10 (jak pulap uczty markietanow), z lozkiem dodatkowo +8 w modelu.
- Ekonomia R6 (DROBNE) - PRAWDA czesciowo. MUSZT-2, 4, 5 sa spelnione z konstrukcji kodu; MUSZT-3 nie calkiem (liczy z odczytow kasy i sakiewki przed/po, wiec
  wykryje np. obcinanie `ChangeGold` czy cudza latke) - ale zaden nie wykrywal R1-R4. POPRAWIONE: MUSZT-8 (samotest zasady w C# na przebiegach z recenzji wobec liczb
  z opisu MCM), MUSZT-6 rozszerzony o sygnatury R1 i R4 w autotescie, MUSZT-1..5 opisane jako straznicy regresji, pomiar powrotow AI z kontynuacja postoju (MUSZT-7).
- NIEPRAWDA: brak. NIEPEWNE: skutki w grze (czestosc powrotow AI z kontynuacja, odczucie progu 12 h) - do planu autotestu nizej.

**Zmiana - licznik postoju (`TownStay.Stay`, `Step`, `DayNow`, `DayAt`, `Mornings`, `Restore`; gracz i AI ta sama funkcja):**
- doba postoju = kolejny poranek (godziny cwiczen jednej doby od switu - jeden blok od switu) spedzony w osadzie z postojem. Wejscie po poludniu: 1. doba do konca
  porannych godzin, nastepny poranek to 2. doba (4/3/2/1 od kazdej godziny wejscia). Wejscie wieczorem: pierwszy poranek to 1. doba. Godziny w polu nie postarzaja postoju;
- postoj konczy doba w polu: 24 h poza osada, liczone narastajaco. Krotki postoj po drodze (ponizej 12 h, `VisitHours`) ich nie zeruje; pobyt od 12 h zaczyna godziny
  w polu od nowa (dluzszy przystanek to znow postoj, wczesniejsze pole sie nie sumuje z pozniejszym);
- godziny przegapione przez obserwacje (`Drill.Observe` - partia nieaktywna, niewola) licza sie jako pole (jak w masce ruchu);
- AI (`Drill.StayHour`): w kazdym poranku pierwsze godziny w osadzie do limitu doby postoju (licznik poranka `MUsed`), reszta - maska `XMask`; `DayDrillHours` =
  min(8, pole + osada - ponad limit). Dotad jeden limit z ostatniej godziny na cale okno 24 h;
- po wczytaniu (`Drill.StayInit` + `RestoreBits`): postoj ze StationaryStartTime (`Restore` - ten sam wynik co `Step` godzina po godzinie, samotest R 42/42), godziny
  postoju w oknie maski z zapisu dostaja bity `SMask`/`XMask` wedlug doby postoju tej godziny (poranek zaczety przed oknem liczy swoje godziny sprzed okna);
- zapis gracza `arm_campdrill`: 12 pol "1|dzis|doba|owed|postoj od|godzin w polu|w osadzie od switu|w osadzie do policzenia|limit osady od switu|poranki|doba od switu
  poranka|godzin w osadzie bez przerwy" (wersja "1" - stara DLL czyta 4 pierwsze; zapis 4-polowy - postoj od nowa; 8-9 pol z wczesnej wersji galezi, nigdy w grze -
  postoj od nowa);
- `Track` AI dostaje `XMask`, `St`, `LastIn`, poranek `M*` (tylko w pamieci, Export M=/T= bez zmian).

**Zmiana - wydatki:** rezerwa napraw postoju w miescie jak przy wyjezdzie (robocizna przy placach miasta + szacunek materialu z polki); K3 tylko w miescie.

**Opis MCM DrillTownStay (EN, nowy):** "... drill by themselves in the morning, but less than in camp and less each morning they stay: in a town 4 hours on the first
morning, then 3, 2 and from the fourth morning 1; in a castle or village 6, then 5, then 4. Only mornings spent in a settlement count. A full day in the field restores
their discipline; a short ride out or a short stop on the way (under half a day) does not break that day, it adds up. ..." Komunikaty w grze bez zmian ("Day N in X"
to teraz numer poranka).

**Linie logu (zmienione/nowe):**
- start sesji: "doba postoju = poranek w osadzie, koniec po 24 h w polu (narastajaco; postoj ponizej 12 h ich nie zeruje)", gracz: "postoj od X (doba N), w polu F h";
- NOWA "Postoj w osadzie (m109): samotest zasady | A ... | B ... | C ... | D ... | E ... | F ... | R ... | wynik TAK." (raz na start sesji, prog MUSZT-8);
- "musztra dzien": koszyki to teraz PORANKI ("doba 1 (limit 4) N porankow, srednio, najwiecej" - poranek zakonczony, liczony w StayHour), "poranki mieszane (miasto
  i zamek albo wies)", "wejscia AI: nowy postoj N (ze StationaryStartTime E, roznica ...), kontynuacja C (w polu przed powrotem srednio/najwiecej; doba postoju
  srednio/najwiecej)";
- linia gracza m97: "postoj (m109): poza osada, w polu F h (doba postoju N trwa do 24 h w polu)"; linia wydatkow (gracz): "poza osada, w polu F h ...";
- autotest: "F2 wejscie ... doba jak przy wyjsciu TAK/NIE (przy wyjsciu N, w polu F h)", podsumowanie "doby kolejne TAK/NIE, limit doby TAK/NIE ... doba jak przy
  wyjsciu TAK/NIE".

**Progi m109muszt (zmiany):** MUSZT-2 koszyki porankow + straznik "powrot z kontynuacja po >= 24 h w polu = 0"; MUSZT-4 opis (rezerwa miejska z materialem); MUSZT-6
+ F1 doby kolejne (kazdy poranek +1) i limit doby z opisu, F2 doba = doba przy wyjsciu z F1; MUSZT-7 + nowe postoje i powroty z kontynuacja (INFO); MUSZT-8 NOWY -
samotest "wynik TAK" i 7 przebiegow TAK (brak linii - INFO); test syntetyczny "zly 2" (R4: doba 1 dwa razy, R1: doba po wypadzie inna niz przy wyjsciu i powrot po
30 h w polu, samotest NIE) -> MUSZT-2, 6, 8 NIE, reszta TAK.

**Ryzyko - co sprawdzic (dopisane do kontroli calosci):**
- Kto czyta i pisze ten sam stan: `TownStay.Step` - gracz (`CampDrill.AutoHour`) i AI (`Drill.StayHour`), nikt inny; `Drill.Track.XMask` czyta tylko
  `DayDrillHours`; `RestHour` i maska ruchu `Mask` bez zmian (sen NightRest, NightMarch, ksiega T10). `CampDrill.PlayerStay` oddaje obiekt tylko do odczytu (TownStay).
- AI: nowy limit na poranek zmienia XP AI wobec poprzedniej wersji galezi (okno z dwoma porankami: dotad jeden nizszy limit, teraz suma dwoch czesci porankow) - moze byc
  troche wiecej XP niz w wersji przed recenzja, mniej niz bez zasady. MUSZT-7 mierzy dokladnie.
- Koszt: `StayHour` - kilka operacji na partio-godzine; `RestoreBits` raz na partie po wczytaniu (24 iteracje); samotest raz na sesje (ok. 600 krokow arytmetyki);
  rezerwa miejska `AiWear.OutstandingCost(mp, st)` buduje lawe (`MendMaterial.Bench`) dla kazdej partii w miescie raz na dobe - jak OnLeft przy kazdym wyjezdzie;
  koszt bloku w linii wydatkow ("koszt X ms") i w DayProfile ("TownStay.Daily").
- Rezerwa miejska wieksza niz dotad -> wydatki postoju w miescie mniejsze (zwlaszcza po bitwie, z duzym zuzyciem) - zamierzone (kowale miasta i tak wezma material
  z sakiewki co godzine).
- K3: wydatki postoju w zamku nie tna juz zwrotu korony; zostaje regula B-2 (zawor zamku -> LordDuesToday -> ciecie zwrotu zoldu zalog), jak dla zoldu zalog.
  Pozostaje rzecz sprzed m109: K3 liczy sie od calej kwoty "zycia" razem z 25% dla kiesy ludu (OnLeft, markietani), a InnPay tylko od czesci w kasie - TownStay to
  dziedziczy z OnLeft (jedna droga), poza zakresem.
- Zapis gracza: nowe pola tylko w napisie `arm_campdrill` (SaveText.Sync), bez nowych typow. Stara DLL czyta 4 pierwsze pola.

**Plan autotestu - dopisane (NIEPEWNE do sprawdzenia w grze):**
1. "postoj": MUSZT-6 z nowymi warunkami (F1 doby kolejne i limit doby, F2 doba jak przy wyjsciu) i MUSZT-8 "wynik TAK". Warunek F2 zaklada swit przed 12:00 (domyslnie
   3:00 - CampEndHour Jeffa); przy innym swicie wyjscie o 12:00 moze wypasc w innej dobie od switu niz powrot - wtedy NIE bez bledu zasady (sprawdzic profil MCM testu).
   Warunek "doby kolejne" zaklada, ze gracz w kazdym poranku F1 dostal choc 1 godzine (bez glodu i bez dlugu snu o swicie - w miescie odpoczywa).
2. p20: MUSZT-7 - ile powrotow AI z kontynuacja postoju (krotkie postoje po drodze), srednie godziny w polu przed nimi i najwyzsza doba postoju przy powrocie;
   jesli kontynuacji jest duzo, a doby wysokie - prog 12 h (`VisitHours`) do decyzji. MUSZT-2 "poranki mieszane" (miasto i zamek/wies w jednym poranku) - ile ich jest.
   Linia "koszt X ms" wydatkow (rezerwa miejska z lawa).
3. wczyt: MUSZT-8 TAK po wczytaniu; pierwszy tick AI po wczytaniu w osadzie - w linii "musztra dzien" doba 1 po wczytaniu godziny z limitem <= bez limitu i bez
   skoku XP (porownac x z nastepna doba); "odtworzone ze StationaryStartTime" > 0.

## 2026-10-23 - Postoj w osadzie: slabsza musztra, zolnierze wydaja zold w miescie (pkt 6 przekazania 22.10 ok. 21:30)

UWAGA: zasade licznika postoju, rezerwe napraw w miescie i K3 w zamku zmienil wpis wyzej (weryfikacja recenzji); ponizej opis pierwszej wersji z poprawionymi zdaniami,
ktore byly nieprawdziwe.

Commity: 36a296f3 (MenPurse.LifeTo), d15fc35e (musztra, klucz DrillTownStay), 3a7ba75c (wydatki i morale, klucz TownStaySpend), 02a371f0 (autotest postoju),
68a63a88 (progi m109muszt + m97szkolenie), 2a688bd3 (licznik "oblezone"). Build Armoury 0 bledow po kazdym commicie z kodem. Gry nie uruchamialem.

**Problem (Jeff 22.10 ok. 21:50, zatwierdzone 23.10 "Ok"):** "jak czekam w miescie, wojsko ma rozkaz drill, ale mniej, bo wydaja kase, pija, chodza na kobiety itd.,
wiec nie w glowie im musztra". Dowod z logu Jeffa (D:/Backup-Bannerlord/logi-jeffa/2026-10-22/Armoury-2026-10-22_22-28-22.log, doby 108673-108699): linia
"Szkolenie w obozie (m97): gracz dzien N | godzin cwiczen 0.00 (z menu obozu ... 0.00, samoczynnie ... 0.00 ...)" w kazdej dobie - grupa m97szkolenie: "dob bez cwiczen
27 z 27". Ludzie Jeffa czekajacego w miescie nie cwicza wcale, a lord AI w tym samym miescie cwiczy pelne 8 h. Zold ludzie wydaja w miescie tylko przy wyjezdzie.

**Przyczyna:**
- `CampDrill.AutoHour` (m97, recenzja 2) dawal graczowi godziny samoczynne tylko w obozie oblezenia i w cudzej armii - postoj w osadzie nie byl ani obozem, ani marszem: 0 h.
- `Drill.DayRestHours` liczyl AI godzine w osadzie jak godzine postoju - do 8 h na dobe. Dwie sprzeczne zasady na jedno zjawisko.
- Sakiewka ludzi schodzi "na zycie" tylko w `MenPurse.OnLeft` (wyjazd z miasta). Markietani (`Sutlers.One`) pomijaja partie w osadzie, zalogi m96 to inne partie,
  wiec partia stojaca tygodniami w zamku nie wydawala nic, a w miescie - dopiero przy wyjezdzie.

**Zmiana (jedna zasada dla gracza, strony gracza i lordow AI; bez nowych latek Harmony; nowy plik `Armoury/src/TownStay.cs`):**
- Musztra w osadzie (klucz `DrillTownStay`, domyslnie TAK; NIE = 1:1 jak 44c03a05):
  - ludzie cwicza sami w godzinach cwiczen (rano, 3:00-17:59), we wspolnym limicie 8 h od switu z menu obozu "Train the men";
  - limit godzin w osadzie maleje z dobami postoju. Miasto: 1. doba 4 h (pol dnia w obozie), potem 3 h, 2 h, od 4. doby 1 h (poranny apel), reszte dnia spedzaja
    w karczmach. Zamek i wies: 6 h, 5 h, od 3. doby 4 h (mniej pokus);
  - doba postoju liczy sie od chwili wejscia. Wypad za brame krotszy niz 24 h nie odnawia dyscypliny, dopiero doba w polu zeruje licznik. W obozie od razu pelne 8 h
    [POPRAWIONE w wpisie wyzej (recenzja R1, R4): doba postoju = poranek w osadzie, doba w polu liczona narastajaco, postoj < 12 h jej nie zeruje];
  - oblezona twierdza (karczmy zamkniete, ludzie na murach) i rabowana wies: bez postoju - jak oboz oblezenia, do 8 h (gracz przy Drill Siege Army);
  - gracz: `CampDrill.AutoHour` (licznik dob postoju co godzine, takze noca i w karze). AI: `Drill.Observe` (maska godzin w osadzie `SMask`, doba postoju, limit)
    i `Drill.DayDrillHours` w `CtxOf` (godziny pola + najwyzej limit godzin w osadzie). `Drill.RestHour` bez zmian (sen NightRest i NightMarch);
  - partia AI widziana pierwszy raz juz w osadzie (wczytanie, nowa partia): doba postoju z `MobileParty.StationaryStartTime` (gra je zapisuje);
  - zapis gracza: napis `arm_campdrill` "1|dzis|doba|owed|postoj od|poza od|w osadzie od switu|w osadzie do policzenia|limit osady od switu" (wersja "1": stara DLL
    czyta 4 pierwsze pola, nowa ze starego zapisu bierze postoj od nowa). AI tylko w pamieci.
- Wydatki i morale (klucz `TownStaySpend`, domyslnie TAK; NIE = jak dotad):
  - co dobe o polnocy partia rodu (AI i gracz) w miescie albo zamku wydaje czesc wolnej sakiewki: 10% (`SutlerDailyShare`), gdy sakiewka gruba do 30%
    (x min(3, dni wolnej sakiewki / 10) przy `SutlersByPurse`) - ta sama miara co markietani N6 i zalogi m96;
  - tylko z tego, co zostaje po naprawach, odziezy i oszczednosciach na sprzet (`MenPurse.CampFree` - rezerwy markietanow). W miescie reszte wydaja dalej przy wyjezdzie;
  - odbiorca jak przy wyjezdzie (`MenPurse.LifeTo` - wydzielone z `OnLeft` bez zmian): kasa osady i kiesa ludu (75/25), te same liczniki (169c, K3, tarcza zoldu, lup L0)
    [POPRAWIONE wyzej (R2): K3 tylko w miescie];
  - morale: +3 na dobe, gdy wydadza choc 1 zl na glowe (mniej pieniedzy - mniej, splukani nic), "ostatnie zdarzenia" najwyzej do +5 (polowa pulapu uczty markietanow 10;
    gra sama zdejmuje 10% dziennie);
  - wies, oblezona twierdza, okno lupu (`CitySack.StayWindow`), bitwa, gracz przy otwartym ekranie zbrojowni - nic;
  - blok dobowy `ArmouryBehavior`: `SoldierPay` -> `Sutlers` -> `SavingsByNeed` -> `TownStay.Daily` -> `Mark(MSutlers)` (pozycja ksiegi "markietani"; opis w `MoneyLedger`).
- Napisy w grze (EN): raz na dobe przy postoju, np. "Day 3 in Winterfell: your men spent 312 denars on drink, dice and company (morale +3). Here they drill only
  2 hours each morning (8 in camp), and fewer each day they stay."; we wsi "Day 2 in X: the men idle about the village - they drill only 5 hours each morning here
  (8 in camp)."; w napisie dnia musztry "... h of drill since the last count (Y h of it in town - the men spend the rest of the day in the taverns)"; dzien bez
  cwiczen: "... or a few morning hours while you wait in a town, castle or village ...". Opisy MCM obu kluczy.
- Linie logu (Log.Info, prefiks "Postoj w osadzie (m109): "): start sesji; "musztra dzien N" (pod Drill Log: koszyki dob postoju miasto 1/2/3/4+ i zamek-wies 1/2/3+,
  godziny w osadzie bez limitu i z limitem, XP zasady obozu bez limitu i z limitem - dokladnie, wzor m97 jest liniowy w godzinach, strona gracza, wejscia AI wobec
  StationaryStartTime); "wydatki dzien N" (pod Log Enabled: odbiorcy, kontrola obiegu z odczytow przed/po, rezerwy, morale, pominiete, gracz). Linia gracza m97:
  dopiski ", w osadzie (m109) Y" i "| postoj (m109): miasto X doba 3, limit 2 h, dzis w osadzie 2.00 h".
- Autotest (plik `Documents/Mount and Blade II Bannerlord/CrashScribe/armoury-postoj.flag` + DLL autotestu, LogGate.Mode 1/2; w grze Jeffa nic nie robi): linie
  "Postoj w osadzie (m109): autotest - <faza> | menu, osada, godzina, doba postoju, limit, godzin w osadzie", podsumowanie "wynik TAK/NIE".
- Progi: nowa grupa `--grupa m109muszt` (MUSZT-S, S2, 0, 0b, 1-7, 9); grupa m97szkolenie czyta opcjonalny dopisek m109 (XP i potkniecia na grupach 6 i 7, SZ-2 liczy
  godziny w osadzie). Na logu Jeffa 22.10 werdykty m97szkolenie bez zmian (przed i po), m109muszt - INFO (log sprzed m109).

**Pliki:** Armoury/src/TownStay.cs (nowy), CampDrill.cs, Drill.cs, MenPurse.cs, ArmouryBehavior.cs (1 linia w bloku dobowym), MoneyLedger.cs (opis pozycji),
Settings.cs + McmSettings.cs (gen_mcm: Armoury 2320 ustawien), tools/sprawdz_logi.py.

**Odstepstwa od projektu (i dlaczego):**
1. Autotest - DLL autotestu (EnsureWaiting) sama osadza gracza w miescie i czeka tam; poza osada zatrzymuje zegar, a na czystej mapie po 5 s przenosi gracza do
   "najbezpieczniejszego" miasta. TickEvent biegnie tylko przy plynacym czasie. Dlatego:
   - F0 bez teleportu (kod bylby martwy) - driver czeka na `town_wait_menus` w miescie DLL;
   - wyjscie z miasta w jednym takcie, nie w dwoch: w menu "town" zegar stoi (TickEvent nie przyjdzie), a DLL po 2 s klika znowu "town_wait". Driver blokuje zegar
     na biegu (`SetTimeControlModeLock`), robi to co opcja gry "Leave" i od razu otwiera menu obozu (gra robi ExitToLast + ActivateGameMenu w jednym wywolaniu
     w `PlayerEncounter.Finish`);
   - w polu gracz stoi caly czas w menu obozu `arm_camp_wait` z zablokowanym zegarem (inaczej DLL zatrzyma zegar i przeniesie gracza); blokada zdejmowana przy wejsciu
     do miasta (menu "town") i przy przerwaniu;
   - F3: powrot o 2:00 (przed switem) po >= 24 h w polu, zamiast "34 h"; F4: trzy poranki zamiast "50 h". Doba postoju liczy sie od godziny wejscia, wiec przy
     wejsciu o 6:00 godziny 3:00-6:00 nastepnego ranka naleza jeszcze do poprzedniej doby postoju (4/3/3 zamiast 4/3/2) - wejscie przed switem daje czyste 4/3/2
     [NIEAKTUALNE po recenzji R4: doba postoju = poranek, kazda godzina wejscia daje 4/3/2; powrot o 2:00 zostal];
   - F4 bez koncowego wyjscia: gracz zostaje w miescie (DLL czeka tam dalej). Wyjscie oddaloby druzyne DLL, ktora przeniosloby ja do innego miasta.
2. [ZMIENIONE wyzej: 12 pol] Zapis `arm_campdrill`: 9 pol zamiast 8 - dziewiate to najwyzszy limit osady w biezacej dobie od switu (do kontroli MUSZT-1 takze po wczytaniu). Zgodnosc w obie strony
   jak w projekcie (stara DLL czyta 4 pierwsze pola; nowa czyta zapis 4-, 8- i 9-polowy).
3. Odtworzenie doby postoju AI ze StationaryStartTime: przy pierwszej pelnej obserwacji partii (`Drill.StayInit`) - obejmuje obie drogi tworzenia sladu (Observe
   i maski z zapisu `RestoreMasks`, ktore biegna po linii startu). Linia startu podaje spis partii AI w osadach (N, srednia doba, Never), linia doby - licznik
   "odtworzone ze StationaryStartTime".
4. Gracz w oblezonej twierdzy - jak oboz oblezenia tylko przy kluczu TAK (bramka `s.DrillTownStay && Besieged`), zeby klucz NIE dawal 1:1.
5. Napis dnia musztry: "(Y h of it in town ...)" zalezy od osady, w ktorej gracz jest teraz (in town / at the castle / in the village / while you waited in a settlement).
6. Komunikat gracza przy wylaczonej musztrze postoju, a wlaczonych wydatkach: "In X: your men spent ..." (bez "Day N").
7. MUSZT-7: INFO przy spadku XP AI <= 30%, DECYZJA przy > 30% (projekt: "INFO; spadek > 30% do decyzji Jeffa").
8. MUSZT-S2 "bez linii startu" = log sprzed m109 (stare linie m97 bez dopisku) - kontrola, ze stare linie dalej sie czytaja, a progi m109 daja INFO.

**Ryzyko - co sprawdzic (kontrola calosci, CLAUDE.md 8.0):**
- Kto czyta i pisze ten sam stan:
  - `CampDrill._dayHours/_owed`: menu "Train the men" dzieli z postojem jeden limit 8 h od switu (rano 2 h w miescie - w obozie zostaje 6 h). Zamierzone;
  - `Drill.Track`: nowe pola tylko w pamieci, `Export` M=/T= bez zmian; `RestHour` bez zmian (NightRest, NightMarch, ksiega AI T10). Godzina w osadzie dalej jest
    odpoczynkiem w ksiedze snu; godziny samoczynne nie sa "praca" (jak oboz oblezenia);
  - `DayRestHours` zostaje (dzis bez wolajacych - zastapione przez `DayDrillHours`, przy kluczu NIE wynik ten sam).
- Sakiewki ludzi: bierzemy tylko wolna czesc po rezerwach (MUSZT-4), 10-30% na dobe. Inni platnicy z tej samej sakiewki: noclegi TownInns, zaopatrzenie BK D11-4,
  VetPurse, zakupy dzienne gracza `OnDailyTickParty`. Skutek uboczny: nowe oszczednosci K1 przy wyjezdzie mniejsze (jak dzis w polu) - wolniejsze dozbrajanie po dlugim
  postoju. Kara BK za brak napitkow (H3) - niskie ryzyko.
- Kolizje: `Sutlers.One` pomija partie w osadzie w tym samym bloku (zbiory rozlaczne); `OnLeft` wydaje reszte bez podwojnego liczenia; `SavingsByNeed` - zalogi
  (inne partie); `ArmyRestDay` (+5 bez pulapu) i uczta (pulap 10) - nasze dopelnia tylko do 5 wedlug stanu z polnocy, ale to nie znaczy, ze nic sie nie kumuluje
  [POPRAWIONE po recenzji R5: armia po >= 6 dobach marszu w miescie dostaje o polnocy nasze +3, o swicie +5 dnia odpoczynku - razem 8; gracz po noclegu w karczmie ma
  premie lozka modelu +2/+5/+8 obok "ostatnich zdarzen" do +5, razem do +13. Przyjete jako zamierzone - inne zdarzenia; nasze nigdy nie podnosi powyzej 5]. `MenPurse.CampFree` wola `SavingsByNeed.Cap` - licznik m96 "limit
  oszczednosci partii: wywolan" w linii nastepnej doby zawiera tez wywolania postoju (sam licznik, bez progu). `MenPurse.WageOf` -> `TotalWage` (losowanie BK przy
  zoldzie bohaterow) dla ok. 50 partii na dobe - jak markietani.
- Zamki dostaja nowy doplyw (zawor 110 - 7% panom, regulator, tarcza zoldu jak przy zalogach m96) - sprawdzic "Pieniadz swiata" i linie m96 (kontrola zamkow 0).
- Martwy kod staje sie zywy: `AutoHour` dla gracza w osadzie, nowe napisy `DayMessage`, nowy komunikat dobowy przy postoju (1 linia na dobe). Koszt: kilka operacji na
  partio-godzine (`StayHour`) i `CampMend` dla ok. 50 partii w osadach raz na dobe.
- AI: spadek XP przy dlugich postojach (H2) mierzony dokladnie (MUSZT-7); posrednio mniej awansow AI i mniejszy popyt na lepszy sprzet - sprawdzic zakupy AI w biegu
  "wczyt". H1: StationaryStartTime moze byc wczesniejsze od wejscia (stanie pod brama) - linia "wejscia AI ... roznica do StationaryStartTime".
- Pierwsza doba po wczytaniu: maska godzin w osadzie AI nie jest w zapisie - godziny w osadzie sprzed wczytania licza sie przez <= 24 h jak pole (bez limitu).
  Okno 24 h z dwiema dobami postoju bierze limit ostatniej godziny postoju (nizszy). [POPRAWIONE wyzej: kod R3 - RestoreBits; R4 - limit na poranek, jak gracz]
- Gracz w cudzej armii w miescie dostaje limit postoju (dotad pelne 8 h) i wydaje zold jak kazda partia w osadzie.
- Autotest: blokada zegara tylko w driverze testu, zdejmowana przy wejsciu do miasta i przy przerwaniu. Spotkanie albo bitwa w polu - "przerwany" (DLL prowadzi dalej).
  Koniec biegu w trakcie fazy pola - DLL zapisuje gre z zegarem na biegu (bieg ma byc dosc dlugi, patrz plan).

**Status:** DO SPRAWDZENIA (autotesty robi glowna sesja; Jeff gra, wiec ja gry nie uruchamialem).

**Plan autotestu:**
1. "postoj" (nowy): jak bieg "wczyt", zapis autotest-F-d240 (34 ludzi), plik flagi `armoury-postoj.flag` w Documents/Mount and Blade II Bannerlord/CrashScribe
   (zaklada glowna sesja na czas biegu i usuwa po nim), co najmniej 14 dob od wczytania (F1 ok. 5 dob, F2 ok. 1.5, F3 ok. 1.3, F4 ok. 2.3 + zapas na osadzenie
   i pierwsza dobe). Grupy: `m109muszt` (MUSZT-6 TAK, MUSZT-1..5, 9), `m97szkolenie`, markietani E5/A3, `m96sakiewki`, obieg / "Pieniadz swiata".
2. p20 (nowa kampania): regresja i pomiar AI (MUSZT-2, 3, 7; SZ-4/5), zapis z 9 polami.
3. wczyt (autotest-dlugi + 20 dob): stary zapis `arm_campdrill` (4 pola) sie wczytuje, AI odtworzone ze StationaryStartTime, grupy ekonomii bez nowych NIE;
   porownac zakupy AI i awanse z logami 103.5.
4. bitwa: niepotrzebna (kod bitwy nietkniety, partie w bitwie pominiete).
Opcjonalnie p20 z oboma kluczami NIE (kontrola 1:1), jesli glowna sesja ma profil MCM testu.
