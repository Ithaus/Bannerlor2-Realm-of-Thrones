# m109-musztra - dziennik galezi w-toku/m109-musztra (od 44c03a05 = WGRANIE 103.5)

## 2026-10-23 - Postoj w osadzie: slabsza musztra, zolnierze wydaja zold w miescie (pkt 6 przekazania 22.10 ok. 21:30)

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
  - doba postoju liczy sie od chwili wejscia. Wypad za brame krotszy niz 24 h nie odnawia dyscypliny, dopiero doba w polu zeruje licznik. W obozie od razu pelne 8 h;
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
  - odbiorca jak przy wyjezdzie (`MenPurse.LifeTo` - wydzielone z `OnLeft` bez zmian): kasa osady i kiesa ludu (75/25), te same liczniki (169c, K3, tarcza zoldu, lup L0);
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
     wejsciu o 6:00 godziny 3:00-6:00 nastepnego ranka naleza jeszcze do poprzedniej doby postoju (4/3/3 zamiast 4/3/2) - wejscie przed switem daje czyste 4/3/2;
   - F4 bez koncowego wyjscia: gracz zostaje w miescie (DLL czeka tam dalej). Wyjscie oddaloby druzyne DLL, ktora przeniosloby ja do innego miasta.
2. Zapis `arm_campdrill`: 9 pol zamiast 8 - dziewiate to najwyzszy limit osady w biezacej dobie od switu (do kontroli MUSZT-1 takze po wczytaniu). Zgodnosc w obie strony
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
  (inne partie); `ArmyRestDay` (+5 bez pulapu) i uczta (pulap 10) - nasze dopelnia tylko do 5. `MenPurse.CampFree` wola `SavingsByNeed.Cap` - licznik m96 "limit
  oszczednosci partii: wywolan" w linii nastepnej doby zawiera tez wywolania postoju (sam licznik, bez progu). `MenPurse.WageOf` -> `TotalWage` (losowanie BK przy
  zoldzie bohaterow) dla ok. 50 partii na dobe - jak markietani.
- Zamki dostaja nowy doplyw (zawor 110 - 7% panom, regulator, tarcza zoldu jak przy zalogach m96) - sprawdzic "Pieniadz swiata" i linie m96 (kontrola zamkow 0).
- Martwy kod staje sie zywy: `AutoHour` dla gracza w osadzie, nowe napisy `DayMessage`, nowy komunikat dobowy przy postoju (1 linia na dobe). Koszt: kilka operacji na
  partio-godzine (`StayHour`) i `CampMend` dla ok. 50 partii w osadach raz na dobe.
- AI: spadek XP przy dlugich postojach (H2) mierzony dokladnie (MUSZT-7); posrednio mniej awansow AI i mniejszy popyt na lepszy sprzet - sprawdzic zakupy AI w biegu
  "wczyt". H1: StationaryStartTime moze byc wczesniejsze od wejscia (stanie pod brama) - linia "wejscia AI ... roznica do StationaryStartTime".
- Pierwsza doba po wczytaniu: maska godzin w osadzie AI nie jest w zapisie - godziny w osadzie sprzed wczytania licza sie przez <= 24 h jak pole (bez limitu).
  Okno 24 h z dwiema dobami postoju bierze limit ostatniej godziny postoju (nizszy).
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
