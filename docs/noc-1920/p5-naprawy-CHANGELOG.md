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

**Poprawka n22f-naprawy2 (20.10, do WGRANIA 93) - naprawy najpierw z rak bez roboty (`MendIdleHandsFirst`).**

*Dla Jeffa (prosto).* Naprawy u cechow zostaja realistyczne (godziny rzemieslnika jak dotad, cena bez zmian), ale teraz najpierw robia je ludzie, ktorzy
i tak nie maja roboty: warsztat miasta, ktory dzis nic nie robi, cech bez wlasnej linii i linie, ktorych praca przepada. Z rzemieslnikow, ktorzy robia nowa
bron i zbroje, lordowie AI i sakiewka Twoich ludzi biora najwyzej jedna dziesiata dziennie (dotad polowe), a Twoje wlasne zlecenia dalej moga zajac kazde wolne rece.

*Problem z liczbami.* Biegi 160 dob z zapisu autotest-F-d240 (x8), srednie ostatnich 28 dob, segment "rece (174.1)" i "cechy [przydzielone/zrobione]":
n22a-160 (bez p5-naprawy, `Armoury-2026-10-20_06-03-49.log`) - rece 10601, liniom 9998, naprawy i konserwacja 76, bezczynne 526; siodlarz 1217/1050,
krawiec 2552/451, platnerz 3795/3136, miecznik 1396/591, lucznik 616/303, tarczownik 421/211. n22b-160 (z p5-naprawy R3-02, `Armoury-2026-10-20_07-52-35.log`) -
liniom 9413, naprawy i konserwacja 473 (+400 roboczodni), bezczynne 655; siodlarz 1103/908 (-14% zrobionych), krawiec 2276/400 (-11%), miecznik 1309/514 (-13%),
lucznik 384/177 (-42%), tarczownik 414/210, platnerz 3925/3296 (+5%). Linia R3-02 (srednia 28 dob, parse.py w n22f-naprawy2-tmp): AI 1513 szt./d, godziny
kowale/siodlarze/krawcy/lukarze/tarczownicy 3436/3268/518/255/122 (razem 7599 h = 475 roboczodni przy 16 h), stop na polowie rak 6598 szt. w 97 wizytach (ze 150 zlecen).
Rece cechow swiata (TownHands 9823 x wagi 0.4/0.15/0.3/0.1/0.05): siodlarze 1473 roboczodni = 23 575 h/d - naprawy AI zjadly 13.9% ich rak, a zrobionych
siodlarzy ubylo 13.5% (zgodne). Kowale: +2350 h wobec dawnych 0.6 h/szt. = 3.7% rak; krawcy 1.1%, lukarze 1.6%, tarczownicy 1.6% - spadki krawcow (-11%)
i lukarzy (-42%) NIE wynikaja z godzin napraw (patrz Ryzyko). Bezczynne rece swiata ok. 530-650 roboczodni, a "przydzielone - zrobione" (rece przy liniach,
ktorych praca przepada przy pelnym banku) ok. 4300 roboczodni (krawcy 82%, miecznicy 58%, lukarze 51%, siodlarze 14%) - tych rak naprawy dotad nie widzialy.
Zuzycie AI: zuzyte w bitwach 378-715/d, naprawione w miastach 1513-1897/d (28 dob); obitych razem 150-175 tys. szt. w kazdym biegu (n21, n22a i n22b - ten sam
zakres, zaleglosc nie rosla bez konca mimo spadku napraw w n22b).

*Zmiana.* Nowy klucz `MendIdleHandsFirst` (TAK, dziala przy `MendHoursByGuild`; NIE = polowa rak `AutoMendHalfGuild` co do godziny jak dotad).
- Plan doby ukrytego warsztatu rzemieslnikow (`WorkshopLaw.BuildPlan`, raz na miasto na dobe, `NotePlanForMend`) podaje GuildMend rece kazdego cechu napraw:
  bezczynne (caly warsztat bez czynnej linii), cech bez linii (jego rece idace do innych cechow - "przeniesione z cechow bez roboty"), rece przy liniach cechu.
  Cykl linii (`CyclePrefix`) zglasza linie z pelnym bankiem roboty (dzisiejsza robota przepada) - jej rece sa dzis bez roboty (`GuildMend.NoteStuck`).
- Kazda godzina napraw (AI, ludzie gracza, zlecenia gracza, dlug cechu) bierze kolejno: bezczynne -> cech bez linii -> linie z pelnym bankiem -> rece linii (`Split`).
- Naprawy automatyczne (AI + sakiewka ludzi): trzy pierwsze zrodla bez limitu, z rak linii najwyzej 10% na dobe (`AutoLineShare`, `AutoRoom`); lawa ludzi gracza
  co godzine w tym samym tempie (dzienny przydzial / godziny zmiany). Zlecenia gracza - jak dotad wszystkie wolne godziny cechu (`Available`, od reki).
- Jutrzejsze ciecie linii (`ManDaysYesterday`, kowale `SmithHours.ManDaysYesterday - SmithFreeManDaysYesterday`): bez godzin z rak bezczynnych i z linii
  z pelnym bankiem (te rece i tak nic nie robily, wiec nic nie powstaje z niczego); godziny cechu bez linii odchodza od rak przeniesionych (jak dotad), z rak linii - od linii.
- Dlaczego 10%: model (sim.py w n22f-naprawy2-tmp, na liczbach z linii) - limit s rak linii w wizytach, w ktorych dzis wiaze polowa; siodlarze s=0.10 -> ciecie linii
  ok. 5-6.6% (wobec 13.9%), s=0.20 -> 8.9%; kowale 2-3%; reszta ponizej 1.5%. Sztuk AI bez nowych zrodel: s=0.10 ok. 870/d, s=0.20 ok. 1190/d; nowe zrodla
  (linie z pelnym bankiem: kowale ok. 28% rak linii, krawcy 82%) dokladaja godziny bez ciecia produkcji - szacunek 1100-1300 szt./d przy s=0.10.
  Wyzsze s nie miesci siodlarzy w 5%, wiec 10% (jak propozycja prowadzacego).
- Linia logu (dopisek na koncu `Naprawy wg cechow (R3-02): doba`): `| zrodlo godzin (MendIdleHandsFirst TAK, k/s/k/l/t): bezczynne a/b/c/d/e, cech bez linii
  (rece przeniesione) ..., linie z pelnym bankiem ..., rece linii ... (w tym automatyczne ..., limit 10% rak linii) | rece cechow wg planow N miast (roboczodni,
  k/s/k/l/t): bezczynne ... (M miast), cech bez linii ..., linie z pelnym bankiem ..., pozostale rece linii ...`. Licznik "stop na polowie rak cechu" - nazwa bez
  zmian (porownywalnosc), przy TAK liczy sztuki zatrzymane na nowym limicie.
- Pliki: GuildMend.cs (zrodla rak, AutoRoom/AutoDaily/Split, linia doby), WorkshopLaw.cs (NotePlanForMend, pelny bank w CyclePrefix, kowale bez godzin bezczynnych),
  Settings.cs + McmSettings.cs (gen_mcm; opis `AutoMendHalfGuild` - "dziala przy MendIdleHandsFirst NIE").

*Ryzyko / co sprawdzic (kontrola calosci).*
- Spadki lukarzy (-42%) i krawcow (-11%) w n22b nie wynikaja z godzin napraw (16 i 32 roboczodni/d = 4% i 1.4% ich rak); u lukarzy spadly rece przeniesione
  wedlug braku (165 -> 29). n22b mialo tez p5-jednostki i p5-kruki - jesli spadek zostanie po tej poprawce, przyczyna jest gdzie indziej (osobna sprawa, nie naprawy).
- Model jest szacunkiem swiata (logi nie maja rozkladu na miasta): udzial wizyt z wiazacym limitem i zrodla wedlug miast - do sprawdzenia nowym dopiskiem linii.
  Jesli AI < 1000 szt./d przy siodlarzach w 5% - nastepny krok: limit linii osobno dla siodlarzy (nie wieksze s dla wszystkich).
- Linia z pelnym bankiem: w dobie, w ktorej znow zacznie sztuke, jej robota moze byc policzona raz w naprawach i raz w banku (najwyzej jedna dniowka linii;
  bank to 60 dni roboty). Linie z cyklem rzadszym niz co 2 doby zglaszaja sie rzadziej (ostroznie - mniej rak bez roboty).
- Odwrocenie stanu miasta z dnia na dzien (wczoraj warsztat bezczynny, dzis pracuje): godziny z rak bezczynnych zostaja bez ciecia; godziny cechu bez linii, ktory
  dzis ma linie, odchodza od jego linii (moze przekroczyc 10% jednego dnia). Cech nieobecny w planie warsztatu - jego czesc rak liczona jak rece linii (ostroznie).
- Kowale: godziny cechu bez linii (np. miecznicy bez rudy) ciete dalej wedlug wag platnerz/miecznik (jak od wpisu 91) - czesc moze pojsc od linii platnerzy.
- Wspolny stan: `_plan`, `_stuck`, `_src` tylko w pamieci (doba; plan starszy niz 2 doby = brak planu), czyszczone w `GuildMend.Reset` (z `SmithHours.Reset`);
  nic do zapisu. Wszystko na watku glownym (cykle warsztatow - dzienny tik osady; naprawy - DailyTickParty, HourlyTick, menu). Alokacje: wiersz na miasto na dobe.
- Zamknieta ekonomia: cena naprawy i zaplaty bez zmian (decyzja Jeffa 07.10); suma godzin cechu dalej najwyzej `Capacity` (rece cechu x 16 h).
- Wylacznik: `MendIdleHandsFirst` NIE - AutoAvailable, lawa ludzi i ciecia jak dotad (polowa przy `AutoMendHalfGuild`); zostaja tylko liczniki nowego dopisku.
- Status: build Release 0 bledow (po gen_mcm); gra nieuruchamiana (zakaz w zadaniu) - DO SPRAWDZENIA w autotescie.

*Oczekiwane liczby w autotescie (160 dob z autotest-F-d240, x8, srednie 28 dob).*
- `Warsztaty: dzien` - rece (174.1): naprawy i konserwacja ok. 150-250 (n22b 473, n22a 76); liniom >= ok. 9750; siodlarz zrobione >= ok. 1000 (n22a 1050,
  n22b 908); platnerz, miecznik, tarczownik w 5% od n22a; krawiec i lucznik - jesli dalej ponizej, przyczyna poza naprawami (Ryzyko).
- `Naprawy wg cechow (R3-02): doba` - AI ok. 1100-1300 szt./d; dopisek: wiekszosc godzin krawcow i lukarzy oraz duza czesc kowali w "linie z pelnym bankiem",
  "rece linii (w tym automatyczne)" na cech nie wiecej niz ok. 10% "pozostalych rak linii" z dopisku planow x 16 h (siodlarze ok. 1000-1200 h/d).
- `Zuzycie AI: dzien` - naprawione w miastach >= ok. 1100/d i wiecej niz zuzyte w bitwach; obitych razem w zakresie 110-180 tys. (jak n21/n22a/n22b), bez stalego wzrostu.
