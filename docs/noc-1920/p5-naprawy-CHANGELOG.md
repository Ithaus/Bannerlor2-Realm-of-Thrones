## p5-naprawy (20.10) - naprawy wedlug cechow (R3-02) i platnerz w taborze (R3-04c) (Armoury)

**Dla Jeffa (prosto).** Cena naprawy zostaje taka jak dzis (Twoja decyzja z 07.10), zmienia sie czas i to, kto pracuje: lekko obity miecz to godzina-dwie,
ciezko obita brygantyna ok. 140 roboczogodzin, a buty i kaftany robia siodlarze i krawcy, nie kowale. Lordowie AI i Twoi ludzie biora razem najwyzej polowe rak
kazdego cechu w miescie, a Twoje duze zlecenie zajmuje rece cechu od pierwszej godziny czekania, wiec nikt nie dostaje pracy "z powietrza". Kazde zlecenie
(takze u kwatermistrza Spoils) ma rabat hurtowy 0,5% za sztuke, do 30%, tylko od robocizny. W polu towarzysz z najlepszym Kowalstwem lata metalowy sprzet
Twoich ludzi z materialu z taboru (gdy czegos brak, bierze nastepna sztuke), a kwatermistrz druzyny z dobrym Zarzadzaniem przyspiesza mu robote do 25%
- obaj ucza sie tylko za godziny naprawde przepracowane.

**Problem / zrodlo pomyslu.** Lista 35 pomyslow (docs/POMYSLY-35-2026-10-20.md, poz. 3 = R3-02 i poz. 7 = R3-04c); Jeff 20.10: "28 - nie, 29 - nie. POzostale tak".
Projekt: docs/PROJEKT-KOSZT-NAPRAWY-2026-10-07.md (decyzja Jeffa na gorze: cena bez zmian; Regula pkt 5 godziny, 6 rabat, 9 czas; Tabela godzin).
Stan przed zmiana (log biegu 160 dob na WGRANIU 91, `Armoury-2026-10-20_00-44-29.log`, doba 109235): `Zuzycie AI: ... zuzyte w bitwach 1175, naprawione w miastach 1222
za 5906 ...`, `E8/P2: ... zlecen 89 ..., sztuk do naprawy 2438; stoja - ... godziny kowali 1 partii / 455 szt.` - kazda sztuka 0.6 h (MendLootHoursPerPiece) i wszystko
u kowali; Twoja uprzaz 1.5 h na czesc; rabat hurtowy tylko na polkach wojska; w polu nikt nie lata sprzetu ludzi.

**Przyczyna.** Stale godziny na sztuke (`SmithHours` + `MendLootHoursPerPiece`), jedna pula kowali dla wszystkich rodzajow, rabat tylko w `TroopPieceCost`.

**Zmiana.** Nowe pliki `Armoury/src/GuildMend.cs`, `Armoury/src/FieldArmourer.cs`; wpiecia: AiWear (dzien lorda), TroopSelfMend (ludzie gracza co godzine + tick
platnerza), SmithMenu (lup z sakw, "Pick a piece", uprzaz, polki wojska), SmithHours.Reset, WorkshopLaw (godziny wczoraj od linii cechu), ArmouryBehavior (3 linie:
rabat uprzezy w RepairCost/PlanRepair, dostep do stanu czesci).
- (1) `MendHoursByGuild` (TAK): roboczogodziny sztuki = udzial z tabeli projektu (rodzaj x ubytek, liniowo miedzy kotwicami 0.45/0.60/0.75/0.90) x dni wykonania sztuki
  (`HistoricalPrices.HistDays` - te same co w cenie nowej) x `WorkHoursPerManDay`. Cech: kowale (bron, drzewcowa, miotana, kolczuga, plyta, helm, metalowy kropierz -
  dalej `SmithHours`), siodlarze (skora, siodla), krawcy (sukno, przeszywanice, plaszcze), lukarze (luki, kusze), tarczownicy (tarcze). Rece cechu = rece rzemieslnikow
  miasta x waga cechu (`WorkshopLaw.GuildWeight`, te same co w warsztatach). Zlecenie gracza trwa tyle, ile potrzeba wolnym rekom cechow (cechy rownolegle); czego nie
  zmieszcza dzisiejsze wolne godziny - kolejne dni pelnej zmiany. Godziny z pul tylko za zrobione sztuki (przerwane zlecenie - proporcjonalnie, nieudana sztuka - zero).
  Uprzaz i sztuka na grzbiecie: ubytek z ksiegi zuzycia (procenty). Godziny wczoraj odchodza dzis od linii tego cechu w warsztacie rzemieslnikow (jak dotad kowale).
  Ludzie gracza w miescie: kazdy cech ma wlasna lawe godzin; sztuka konczy sie, gdy na lawie jej cechu zbierze sie tyle godzin, ile trwa (najgorsze najpierw).
- (2) `AutoMendHalfGuild` (TAK, dziala przy (1)): naprawy automatyczne (lordowie AI + sakiewka Twoich ludzi) biora razem najwyzej 50% dziennych godzin kazdego cechu
  w miescie (`_auto` w GuildMend); zlecenia gracza moga brac wszystko, co wolne.
- (3) `MendBulkEveryOrder` (TAK): rabat hurtowy polek wojska (`TroopMendBulkDiscountPP` 0.5% / `TroopMendBulkDiscountMax` 30%) przy kazdym zleceniu: lup z sakw
  (liczba sztuk z planu bez rabatu), uprzaz (czesci bez wrakow), dzien lorda w miescie (sztuki zlecenia), ludzie gracza (sztuki czekajace rano w tym miescie).
  Tylko od robocizny - nie od materialu i nie od godzin. Pojedyncza sztuka "Pick a piece" bez rabatu (0.5% od kilku pensow ginie w zaokragleniu). Zamknieta ekonomia:
  placacy placi mniej, kasa miasta / kiesa ludu dostaje mniej o te sama kwote (te same sciezki zaplaty co dotad).
- (4) `CampArmourer` (TAK): platnerz w taborze. Towarzysz z najlepszym Kowalstwem (min. 20; bez gracza, rannych, jencow) co godzine zegara, gdy druzyna stoi poza miastem
  (nie w marszu, nie w bitwie, nie noca przy WorkshopNightRest, nie w niewoli), doklada `0.25 + Kowalstwo/400` roboczogodziny (najwyzej 0.75 - zawsze wolniej niz warsztat)
  do jednej sztuki metalu ludzi z magazynu wojska (`TroopSelfMend.MenWorn` - bez Twojej czesci, bez wrakow i koni; tylko robota kowala z tabeli i z receptura kowala).
  Godziny sztuki te same co u kowali miasta (`GuildMend.Hours`). Zaczeta sztuka zostaje na kowadle do konca (nowy lup nie kasuje pracy). Material jak przy "Mend it
  yourself" dla magazynu (`MendMaterial.SelfPartsOf`, pamiec ulamkow, `MendTakeByKind`) - zdejmowany z taboru przy ukonczeniu; brak materialu = nie pracuje i nie dostaje XP.
  Robocizna 0 zl (towarzysz bierze zold). XP Kowalstwa 8 na przepracowana godzine, sufit 100 na dobe.
- (5) `CampQuartermasterSteward` (TAK, dziala przy (4)) - drugi czlon R3-04c, "kwatermistrz zbrojowni wedlug Zarzadzania": kwatermistrz druzyny (rola gry
  `MobileParty.EffectiveQuartermaster`; bez przydzialu - dowodca) wydaje platnerzowi sztuki z magazynu, odmierza material i sortuje robote. Robota platnerza
  x (1 + Steward/800), najwyzej +25% (Steward 200); razem najwyzej 0.94 roboczogodziny na godzine - wciaz wolniej niz warsztat. XP Zarzadzania 2 za godzine,
  w ktorej kuznia naprawde pracowala, sufit 25 na dobe. Kwatermistrz bedacy platnerzem, ranny albo w niewoli - bez premii i bez XP. Material bez zmian.
- Linie logu (log_grep): `Naprawy wg cechow (R3-02): doba N | AI: X szt., godz. k/s/k/l/t, stop na polowie rak cechu Y szt. w Z wizytach | ludzie gracza: ... | gracz
  (zlecenia): ... | rabat hurtowy razem G zl`; `GuildMend ludzie: <miasto> - naprawiono ...` (22:00 w miescie); `Platnerz w taborze (R3-04c): doba N | <imie> - naprawil
  X szt. metalu ludzi, przepracowal H roboczogodz., XP Kowalstwa P (sufit 100/dobe), godzin bez materialu W | kwatermistrz (Steward): <imie> - G godz. pomocy,
  premia do +B%, XP Zarzadzania Z (sufit 25/dobe).`; w linii R3-02 na koncu `| dlug cechow: splacono H h, zostaje R h w M miastach`; u kwatermistrza Spoils
  `Kwatermistrz (Spoils) - naprawa (...) ...; godziny cechow (R3-02): kowale X h, ..., bez wolnych rak N, rabat hurtowy P% przy S szt.`
- Klucze wylaczone - jak dotad co do godziny i pensa (stare sciezki nietkniete; licznik P2Mend przy wylaczonym (1) - stary wzor).

**Ryzyko / co sprawdzic (kontrola calosci).**
- Przepustowosc AI: na sztuke srednio ok. 10 roboczogodzin zamiast 0.6, a AI ma najwyzej pol rak cechu. Dzis AI naprawia 1222 szt./dobe przy 1175 obitych w bitwach.
  W autotescie porownac `Zuzycie AI: ... zuzyte w bitwach` z `naprawione w miastach` i licznik `stop na polowie rak cechu`; jesli zaleglosci (`obitych razem`) rosna
  szybciej niz dzis - pierwszy krok to `AutoMendHalfGuild` NIE (jedna zmiana naraz), potem dopiero wagi cechow. Liczba rak cechow to szacunek (pomysl, Ryzyko).
- Warsztaty: godziny napraw siodlarzy/krawcow/lukarzy/tarczownikow wczoraj odchodza dzis od ich linii w ukrytym warsztacie rzemieslnikow - produkcja tych cechow
  (`Warsztaty: dzien N - wykonano ...`) moze spasc o tyle, ile naprawiaja; przy (2) najwyzej pol ich rak idzie na naprawy automatyczne.
- Zlecenia gracza: duzy lup (40+ sztuk) moze trwac dobe-dwie zegara (czekanie w menu, jak dotad przy kazdej pracy na czas); `TroopMendMaxHours` przy (1) juz nie skraca
  polek wojska (napisane w MCM). Uprzaz w dobrym stanie robi sie szybciej niz dotad (1.5 h na czesc -> min. 0.5 h na zlecenie).
- Wspolny stan: `SmithHours` (kowale) dzieli rece z QohorMasters i starymi sciezkami - GuildMend korzysta z tej samej puli dla kowali (bez drugiego licznika);
  pozostale cechy maja wlasne pule w GuildMend (stan tylko dzienny, `Reset` z `SmithHours.Reset`). Wszystko na watku glownym (DailyTickParty, SettlementEntered,
  HourlyTick, menu) - bez PartiesThink. Nic do zapisu (lawa platnerza ginie przy wczytaniu - najwyzej kilka godzin pracy).
- Platnerz: XP za godziny pracy nad sztuka, ktora potem naprawia kowal miasta, zostaje (godziny naprawde przepracowane, sufit 100/dobe). Material sprawdzany co godzine,
  zdejmowany przy ukonczeniu - gracz moze sprzedac zelazo w trakcie; wtedy sztuka czeka (bez XP).
- Kolizje z galeziami: ArmouryBehavior.cs - 3 linie (RepairCost, PlanRepair, nowy `ConditionQuietOf`); Settings.cs - 4 pola w dwoch blokach; SmithMenu, AiWear,
  TroopSelfMend, WorkshopLaw, SmithHours - wpiecia z komentarzem R3-02 / R3-04c.
- Poza zakresem (jak dotad): naprawa wlasnymi rekami - czas z SelfRepairHoursPerPiece (to nie robota cechu). Kwatermistrz Spoils jest juz w zakresie (ponizej).

**Poprawki po recenzji (20.10, commit a5cc517f).**
- Platnerz (wazne): brak materialu na najgorsza NIEZACZETA sztuke - bierze nastepna, na ktora material w taborze jest (najwyzej 60 sztuk sprawdzanych na godzine,
  najgorsze najpierw); "godzina bez materialu" liczy sie tylko, gdy zadna sztuka nie ma materialu. Zaczeta sztuka (postep > 0) czeka na swoj material.
  Platnerz ranny albo brak towarzysza z Kowalstwem 20+: postep na kowadle zostaje (dotad zerowany).
- Zlecenia gracza wielodniowe (wazne): `GuildMend.PlayerStart` (zamiast PlayerClock w 4 wywolaniach StartTimedWork: lup, sztuka, polki wojska, uprzaz) zaczyna
  rezerwacje; co godzine czekania (HourTick, przed naprawami ludzi) kazdy cech zlecenia bierze swoje wolne godziny (najwyzej rece cechu na godzine), wiec AI
  i sakiewka ludzi widza zajete rece od razu. Koniec roboty: godziny gotowych sztuk minus juz zarezerwowane. Czego dzis juz nie ma (AI wzielo rece w tym czasie,
  koniec przed rezerwacja) - dlug cechu (`_carry`), ktory nowego dnia zajmuje rece cechu przed wszystkimi innymi (najwyzej cala zmiana; reszta dalej). Zegar bez
  zmian. Przerwanie: zarezerwowane godziny byly przepracowane - nie wracaja; doliczana jest tylko roznica. Praca na czas ma numer (`SmithMenu.WorkSerial`), wiec
  inna praca (przetop, wlasne rece) nie przedluza rezerwacji.
- Pelny zakres R3-02/R3-04c (wazne): kwatermistrz Spoils (`SpoilsSeal.MakePlan/Apply`) - godziny cechow (bierze wolne godziny dzis, bez limitu polowy; reszta
  "waits for free hands"), rabat hurtowy od liczby sztuk zlecenia (przy budzecie - najwyzej jego limit sztuk), godziny z pul za zrobione sztuki, ta sama wycena
  w menu. Stara regula ludzi (`TroopSelfMend.Run`, sakiewki wylaczone): godziny cechow jako naprawa automatyczna (polowa rak) i rabat. Drugi czlon R3-04c -
  kwatermistrz wedlug Steward, punkt (5) wyzej.
- Rabat AI (czesc "wazne" o warsztatach): liczony od sztuk, na ktore cechy maja dzis godziny (`Budget.Preview`), nie od calej zaleglosci partii; przy wylaczonych
  godzinach cechow - od tylu sztuk, ile zmieszcza godziny kowali (cap).
- Drobne: okno "How will it be made whole?" pokazuje godziny cechu (te same co praca na czas) i nazwe cechu; "Pick a piece" na grzbiecie - godziny ze stanu ksiegi;
  stara sciezka ludzi (MendHoursByGuild NIE) - z rabatem hurtowym zlecenia dnia (`GuildMend.MenBulkN`); lup z sakw - procent rabatu w dymku z tej samej liczby
  sztuk, od ktorej liczona robocizna; opisy MCM zgodne z tym.

**Recenzja - co uznane, co nie.** Wszystkie cztery "wazne" i piec "drobnych" uznane za prawdziwe i poprawione jak wyzej. Uwaga o warsztatach siodlarzy jest
szacunkiem (nie pomiarem) - poprawiony rabat AI; spadek produkcji siodlarzy do sprawdzenia w autotescie (punkt 6 testu), bez zmiany udzialu AI na zapas.

**Status.** DO SPRAWDZENIA (build Release 0 bledow po poprawkach; gen_mcm stabilny; gra nieuruchamiana - zakaz w zadaniu).

**Decyzje (Claude, wedlug projektu; wylaczniki w nawiasach).**
- Godziny wedlug tabeli projektu i dni wykonania sztuki (`MendHoursByGuild`); cena bez zmian (decyzja Jeffa 07.10).
- Polowa rak cechu dla napraw automatycznych (`AutoMendHalfGuild`); zlecenia gracza bez limitu.
- Rabat przy kazdym zleceniu, z kluczy rabatu polek wojska (`MendBulkEveryOrder`); pojedyncza sztuka bez rabatu.
- Platnerz: tylko metal (robota kowala), tylko poza miastem i na postoju, 0.25 + Kowalstwo/400 roboczogodz. na godzine (max 0.75), XP 8/h, sufit 100/dobe, min. Kowalstwo 20
  (`CampArmourer`).
- Kwatermistrz wedlug Steward: premia do robot platnerza (nie do ceny, nie do materialu), bo jedyna robota "naszego kwatermistrza zbrojowni" bez rak cechu to oboz;
  w miescie rece ludzi ogranicza polowa rak cechu i Steward jej nie podnosi (`CampQuartermasterSteward`).
- Zlecenie gracza ponad wolne rece: rezerwacja co godzine + dlug cechu na nastepne dni (bez nowego klucza - czesc `MendHoursByGuild`).
- Kwatermistrz Spoils robi od reki, wiec bierze tylko wolne godziny dzis (bez czekania), reszta czeka (`MendHoursByGuild`).

**Test w grze.**
1. W miescie po bitwie: "Restore worn loot in your bags" (kuznia miasta) - dymek pokazuje "About X hours (Y work-hours of the town's trades). Bulk rate for N pieces: P% off the work.";
   praca trwa X godzin; w logu po dobie `Naprawy wg cechow (R3-02): ... gracz (zlecenia): N szt.`.
2. "Pick a piece" -> sztuka kowala: linia pokazuje "Xh of the smiths/saddlers/..." wedlug rodzaju.
3. Postoj w miescie z obitym sprzetem ludzi: o 22:00 "The town trades of <miasto> mended N pieces..."; w logu `GuildMend ludzie:`.
4. Oboz/oblezenie z towarzyszem z Kowalstwem i zelazem w taborze: nastepnego dnia "<imie> mended N pieces of your men's kit at the camp forge yesterday.";
   w logu `Platnerz w taborze (R3-04c): doba ...`, XP Kowalstwa towarzysza rosnie.
5. Autotest 120+ dob: linia `Naprawy wg cechow (R3-02): doba` co dobe z `AI: X szt.` > 0; porownac z `Zuzycie AI: ... naprawione w miastach` (powinny sie zgadzac
   co do sztuk) i trend `obitych razem`.
6. Warsztaty po cechach: w `Warsztaty: dzien N` porownac segment `cechy [krawiec .../..., ..., siodlarz .../..., lucznik, tarczownik]` z biegiem na WGRANIU 91
   (doba 109235: `siodlarz 1121/1054`, `krawiec 2326/561`, `lucznik 645/309`, `tarczownik 432/158`). Spadek ponad 10% u siodlarzy - pierwszy krok
   `AutoMendHalfGuild` NIE albo mniejszy udzial AI u siodlarzy (osobna zmiana).
7. Duze zlecenie gracza (lup 40+ szt.) w miescie: w czasie czekania `Available` cechow spada co godzine (lordowie AI w tym miescie dostaja mniej - licznik
   `stop na polowie rak cechu`); po skonczeniu w linii doby `dlug cechow: zostaje 0 h` albo splacany nastepnego dnia.
8. Kwatermistrz Spoils: dymek i komunikat z `Bulk rate for N pieces` i ewentualnie `pieces wait for free hands`; w logu `godziny cechow (R3-02)`.
9. Oboz z platnerzem i kwatermistrzem (inny towarzysz, Steward 50+): w linii platnerza `kwatermistrz (Steward): <imie> - ... premia do +6%` i XP Zarzadzania
   <= 25/dobe; sztuka bez materialu (np. t5 bez zelaza t5) nie blokuje lzejszych.

**Poprawka n22f-naprawy2 (20.10, do WGRANIA 93) - naprawy najpierw z rak bez roboty (`MendIdleHandsFirst`); wersja po recenzjach (ekonomia + technika).**

*Dla Jeffa (prosto).* Naprawy dalej licza prawdziwe godziny rzemieslnika i kosztuja tyle samo, ale najpierw robia je ludzie, ktorzy dzis i tak nie maja roboty:
warsztat miasta, ktory nic nie robi, i linie, ktorych praca i tak przepada. Z rzemieslnikow, ktorzy robia nowa bron i zbroje (takze tych pozyczonych do innego cechu),
lordowie AI i sakiewka Twoich ludzi biora najwyzej 15% dziennie zamiast polowy, a Twoje wlasne zlecenia dalej zajmuja kazde wolne rece. Cena: lordowie naprawiaja
troche mniej (szacunek ok. 1000-1200 sztuk dziennie zamiast 1500), wiec ich obity sprzet czeka dluzej - nastepny autotest pokaze, czy ta kolejka nie rosnie za szybko.

*Problem z liczbami.* Biegi 160 dob z zapisu autotest-F-d240 (x8), srednie ostatnich 28 dob (parse.py, trend.py w n22f-naprawy2-tmp), segment "rece (174.1)"
i "cechy [przydzielone/zrobione]": n22a-160 (bez p5-naprawy, `Armoury-2026-10-20_06-03-49.log`) - rece 10601, liniom 9998, naprawy i konserwacja 76, bezczynne 526;
siodlarz 1217/1050, krawiec 2552/451, platnerz 3795/3136, miecznik 1396/591, lucznik 616/303, tarczownik 421/211. n22b-160 (z p5-naprawy R3-02,
`Armoury-2026-10-20_07-52-35.log`) - liniom 9413, naprawy i konserwacja 473 (+400 roboczodni), bezczynne 655; siodlarz 1103/908 (-14% zrobionych), krawiec 2276/400
(-11%), miecznik 1309/514 (-13%), lucznik 384/177 (-42%), tarczownik 414/210, platnerz 3925/3296 (+5%). Linia R3-02: AI 1513 szt./d, godziny kowale/siodlarze/krawcy/
lukarze/tarczownicy 3436/3268/518/255/122 (7599 h = 475 roboczodni przy 16 h), stop na polowie rak 6598 szt. w 97 wizytach (ze 150 zlecen) - popyt AI jest wiekszy niz
rece. Wlasne rece cechow swiata (rece swiata ok. 9800 x wagi 0.4/0.15/0.3/0.1/0.05): godziny AI = kowale 5.5%, siodlarze 13.9%, krawcy 1.1%, lukarze 1.6%,
tarczownicy 1.6% rak; siodlarzy zrobionych ubylo 13.5% (zgodne). Spadki krawcow (-11%) i lukarzy (-42%) NIE wynikaja z godzin napraw (patrz Ryzyko).
"Przydzielone - zrobione" w n22a (linie, ktorych praca przepada przy pelnym banku, najwyzej wlasne rece linii): kowale 28%, siodlarze 10%, krawcy 64%, lukarze 23%,
tarczownicy 35% wlasnych rak cechu - tych rak naprawy dotad nie widzialy.
Zaleglosc AI (srednie 28 dob: pierwsze / srodkowe / ostatnie) - obitych razem: n21 119320 / 137510 / 136430, n22a 116109 / 148387 / 142500, n22b 115983 / 161700 / 170829
(max 185486); "Zuzycie AI - pomiary (E8/P2)" sztuk do naprawy w zleceniach: n21 2632 / 1868 / 2535, n22a 2474 / 3186 / 2698, n22b 3424 / 5110 / 8344, w tym stoja na
godzinach 2244 / 3561 / 6598. Doplyw obitych = lup obity + zuzyte w bitwach: n21 5216 + 555, n22a 6445 + 715, n22b 3279 + 378 na dobe; naprawione 1897 / 1814 / 1513.
Czyli juz przy polowie rak (n22b) zaleglosc rosla - wolniej pod koniec (+690/d, potem +140/d), ale kolejka sztuk noszonych do naprawy (P2) rosla coraz szybciej.

*Zmiana.* Nowy klucz `MendIdleHandsFirst` (TAK, dziala przy `MendHoursByGuild`; NIE = polowa rak `AutoMendHalfGuild` co do godziny jak dotad).
- Plan doby ukrytego warsztatu rzemieslnikow (`WorkshopLaw.BuildPlan` -> `NotePlanForMend`, raz na miasto na dobe) podaje GuildMend WLASNE rece kazdego cechu napraw
  (H x waga / wagi cechow planu - ta sama czesc co "own" w BuildPlan): cech z linia - rece linii, cech bez linii - rece przeniesione do linii innych cechow,
  caly warsztat bez czynnej linii - rece bezczynne. Cykl linii (`CyclePrefix`) zglasza linie z pelnym bankiem roboty (`GuildMend.NoteStuck`; najwyzej wlasne rece linii).
- Kazda godzina napraw (AI, ludzie gracza, zlecenia gracza, dlug cechu) bierze kolejno (`Split`): bezczynne -> linie z pelnym bankiem -> rece przeniesione -> rece linii.
- Naprawy automatyczne (AI + sakiewka ludzi, `AutoRoom`): bezczynne i linie z pelnym bankiem bez limitu; z rak pracujacych (przeniesione + linie) razem najwyzej
  15% wlasnych rak cechu na dobe (`AutoLineShare`); reszta czeka (zaleglosc, jak dotad). Lawa ludzi gracza co godzine w tym samym tempie (dzienny przydzial / zmiana).
  Zlecenia gracza - jak dotad wszystkie wolne godziny cechu (`Available`, od reki, dlug cechu na kolejne dni).
- Jutrzejsze ciecie (`ManDaysYesterday`, kowale `SmithHours.ManDaysYesterday - SmithFreeManDaysYesterday`): bez godzin z rak bezczynnych i z linii z pelnym bankiem
  (te rece nic nie robily - nic nie powstaje z niczego); godziny cechu bez linii odchodza od rak przeniesionych, godziny z rak linii - od linii (jak dotad).
- Dlaczego 15% (sim2.py w n22f-naprawy2-tmp; logi nie maja rozkladu na miasta, wiec 4 zalozenia: linie z pelnym bankiem w miastach z naprawami jak srednia n22a albo
  polowa tego; stop jak w 2/3 wizyt albo wszedzie). AI szt./d i godziny siodlarzy z rak pracujacych (= jutrzejsze ciecie, % ich rak):
  10% - 670-1104 szt., siodlarze 587-710 h (2.5-3.0%); 15% - 802-1185 szt., 881-1038 h (3.7-4.4%); 20% - 932-1262 szt., 1174-1367 h (5.0-5.8%).
  Kowale przy 15%: 484-1115 h z rak pracujacych (0.8-1.8% rak), reszta z linii z pelnym bankiem; krawcy, lukarze, tarczownicy ponizej 0.7%. 20% wypycha siodlarzy
  ponad 5%, 10% daje w gorszych zalozeniach ok. 700-870 szt. AI - 15% jako jedyne miesci oba cele w zalozeniu srodkowym.
- Linia logu (dopisek na koncu `Naprawy wg cechow (R3-02): doba`): `| zrodlo godzin (MendIdleHandsFirst TAK, k/s/k/l/t): bezczynne ..., linie z pelnym bankiem ...,
  cech bez linii (rece przeniesione) ..., rece linii ...; naprawy automatyczne z rak pracujacych (przeniesione + linie) ..., limit 15% tych rak | rece cechow wg planow
  N miast (wlasne rece cechu, roboczodni, k/s/k/l/t): bezczynne ... (M miast), linie z pelnym bankiem ..., cech bez linii ..., pozostale rece linii ...`.
  Licznik "stop na polowie rak cechu" - nazwa bez zmian (porownywalnosc), przy TAK liczy sztuki zatrzymane na nowym limicie.
- Pliki: GuildMend.cs (zrodla rak, Tiers/AutoRoom/AutoDaily/Split, linia doby), WorkshopLaw.cs (NotePlanForMend, pelny bank w CyclePrefix, kowale bez godzin
  bezczynnych), Settings.cs + McmSettings.cs (gen_mcm; opis `AutoMendHalfGuild` - "dziala przy MendIdleHandsFirst NIE").

*Recenzje (ekonomia, technika) - co prawdziwe i co zrobione.*
- Rece "cecha bez linii" nie sa bezczynne (obie recenzje, wazne) - PRAWDA. `BuildPlan`: `freed = H x (wAll - wAct) / wAll` idzie jako extra do linii innych cechow
  (n22b: 2065 z 2555 do platnerzy), a wczorajsze naprawy cechu bez linii schodza z `freed` (`cutI`). Pierwsza wersja dawala je AI bez limitu (w miescie bez linii
  miecznikow AI u kowali moglo wziac wiecej niz dawna polowa). Poprawione: rece przeniesione ida pod ten sam limit co rece linii (`AutoRoom`: limit od przeniesione +
  linie, licznik `[20..24]` liczy oba), w `Split` po liniach z pelnym bankiem. Przyklad recenzji (H=100, miecznik bez linii, platnerz 20 wlasnych + 15 przeniesionych):
  dawna polowa 20 roboczodni AI, pierwsza wersja 23.5, teraz 0.15 x 40 = 6 (+ ewentualnie pelny bank) - jutro platnerz traci ok. 3 + 0.8 x 3 = 5.4 z 35 rak (ok. 15%).
- Kowale: limit od sumy linii platnerzy i miecznikow, ciecie po wagach 0.5/0.5 (technika, wazne) - PRAWDA dla pierwszej wersji (baza = hg z rekami przeniesionymi,
  miecznicy do ok. 20% ich rak linii). Poprawione bez zmiany ciecia z wpisu 91: baza limitu to teraz wlasne rece cechu (H x waga / wagi planu), a ciecie wedlug wag
  dzieli dokladnie w tej proporcji - kazdy z dwoch cechow kowali traci najwyzej 15% wlasnych rak, czynny miecznik najwyzej 15% swoich rak linii (wlasne <= hg).
- Ciesle przy tarczach w rekach linii tarczownikow (ekonomia, drobne) - PRAWDA, poprawione tym samym (wlasne rece cechu, bez `carp` z R3-09a).
- Prognoza "AI 1100-1300 szt./d" niewykazana, model liczyl rece przeniesione za darmo (ekonomia, technika) - PRAWDA. Nowy model sim2.py (wyzej) liczy pelny bank
  z danych n22a i rece przeniesione pod limitem; wynik jako przedzial, nie jedna liczba. Dawny sim.py zostaje tylko jako slad pierwszej wersji.
- Zaleglosc rosla w n22b, a porownanie pomijalo lup obity (ekonomia, technika) - PRAWDA; opis wyzej poprawiony (trend w 3 odcinkach, doplyw z lupem, kolejka P2).
  Szacunek stanu ustalonego (inne ubytki ~ c x obitych, c z biegow: n21 0.029, n22a 0.038, n22b 0.012/d): przy 1100-1200 naprawionych - n21/n22a ok. 156-164 tys.,
  n22b ok. 209-218 tys. (przy polowie rak ok. 183 tys.) - wyzej, ale nie bez konca. Wplyw na wojsko: nie znalazlem uzycia ksiegi obitych lordow AI w bitwie (zbrojownia DTE
  bez stanu; stan czytaja zakupy, sprzedaz, awanse i kit: AiGear, MenPurse, MenUpgrade, Sutlers, Drill, BkSupplyMarket, GarrisonArmory; GarrisonKit A10 - zalogi
  gracza w bitwie gracza, poza tym tematem) - obity sprzet AI to mniej zaplaconych napraw i tansza sprzedaz, a nie slabsi ludzie w walce (do potwierdzenia, nie sprawdzalem w grze).
- Rabat hurtowy AI spadnie (ekonomia, drobne) - PRAWDA, bez zmiany kodu: wzor ceny bez zmian, ale rabat liczy sie od sztuk, ktore cech wezmie dzis (`Budget.Preview`),
  a tych bedzie mniej. n22b: 620 / 1050 / 973 zl na dobe (odcinki 28 dob); oczekiwane ok. 500-900 zl.

*Ryzyko / co sprawdzic (kontrola calosci).*
- Glowne zalozenie modelu: linie z pelnym bankiem stoja tez w miastach, w ktorych lordowie naprawiaja. Dopisek planow w linii R3-02 to pokaze; jesli "linie z pelnym
  bankiem" kowali w planach < ok. 500 roboczodni, AI spadnie do ok. 800 szt./d. Nastepny krok wtedy: osobny limit siodlarzy albo wyzszy limit dla kowali, nie wieksze
  15% dla wszystkich (siodlarze sa najblizej 5%).
- Kolejka AI (P2 "sztuk do naprawy") wzrosnie ponad n22b (8344) - to cena ochrony produkcji. Jesli w ostatnich 28 dobach przekroczy ok. 12 tys. i dalej przyspiesza,
  albo obitych razem > ok. 220 tys. - wrocic do limitu (np. 20%) i przyjac ok. -5..-6% siodlarzy.
- Spadki lukarzy (-42%) i krawcow (-11%) w n22b nie wynikaja z godzin napraw (16 i 32 roboczodni/d); u lukarzy spadly rece przeniesione wedlug braku (165 -> 29),
  n22b mialo tez p5-jednostki i p5-kruki - jesli spadek zostanie, przyczyna jest gdzie indziej (osobna sprawa).
- Linia z pelnym bankiem: w dobie, w ktorej znow zacznie sztuke, jej robota moze byc policzona raz w naprawach i raz w banku (najwyzej jedna dniowka linii; bank to 60 dni).
  Linie z cyklem rzadszym niz co 2 doby zglaszaja sie rzadziej (ostroznie). Pelny bank liczony od udzialu linii, ale najwyzej do wlasnych rak cechu (ostroznie).
- Odwrocenie stanu miasta z dnia na dzien: godziny z rak bezczynnych zostaja bez ciecia; godziny cechu bez linii, ktory dzis ma linie, schodza z jego linii (jednego dnia
  moze to przekroczyc 15%). Cech nieobecny w planie warsztatu - jego czesc rak jak rece linii pod limitem (dzis jego naprawy i tak nie sa ciete - stan sprzed tej zmiany).
- Wspolny stan: `_plan`, `_stuck`, `_src` tylko w pamieci (doba; plan starszy niz 2 doby = brak planu, wszystkie rece jak rece linii), czyszczone w `GuildMend.Reset`;
  nic do zapisu. Watek glowny (cykle warsztatow - dzienny tik osady; naprawy - DailyTickParty, HourlyTick, menu). Alokacje: wiersz na miasto na dobe, dopisek raz na dobe.
- Zamknieta ekonomia: cena naprawy i zaplaty bez zmian (decyzja Jeffa 07.10); suma godzin cechu dalej najwyzej `Capacity` (rece cechu x 16 h).
- Wylacznik: `MendIdleHandsFirst` NIE - AutoAvailable, lawa ludzi i ciecia jak dotad (polowa przy `AutoMendHalfGuild`); zostaja tylko liczniki dopisku.
- Status: build Release kod 0, 0 bledow (po gen_mcm); gra nieuruchamiana (zakaz w zadaniu) - DO SPRAWDZENIA w autotescie.

*Oczekiwane liczby w autotescie (160 dob z autotest-F-d240, x8, srednie 28 dob).*
- `Warsztaty: dzien` - rece (174.1): naprawy i konserwacja ok. 100-180 (n22b 473, n22a 76); liniom >= ok. 9750; siodlarz zrobione >= ok. 1000 (n22a 1050, n22b 908);
  platnerz, miecznik, tarczownik w 5% od n22a (3136 / 591 / 211); krawiec i lucznik - jesli dalej ponizej, przyczyna poza naprawami (Ryzyko).
- `Naprawy wg cechow (R3-02): doba` - AI ok. 1000-1200 szt./d (model 800-1185); dopisek zrodel: naprawy automatyczne z rak pracujacych siodlarze ok. 850-1050 h/d,
  kowale ok. 500-1100 h/d, reszta kowali w "linie z pelnym bankiem"; krawcy, lukarze, tarczownicy prawie cale w "bezczynne" + "linie z pelnym bankiem". Dopisek
  planow (roboczodni): linie z pelnym bankiem kowale ok. 700-1100, krawcy ok. 1500-1900, siodlarze ok. 100-150.
- `Zuzycie AI: dzien` - naprawione w miastach >= ok. 1000/d; obitych razem: ostatnie 28 dob <= ok. 1.15 x srodkowe 28 i <= ok. 220 tys.; P2 "sztuk do naprawy"
  ostatnie 28 dob <= ok. 12 tys. (n22b 8344); rabat hurtowy razem ok. 500-900 zl/d.
