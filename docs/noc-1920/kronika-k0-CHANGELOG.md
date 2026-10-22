# CHANGELOG galezi w-toku/kronika-00-silnik (Kronika Westeros - silnik K0)

Baza: n32-calosc 77c17f3c (kandydat WGRANIA 102) + scalone kronika-dane (6441d096) + nagrania lektora (fcf2b976, 9e02b0f2).
Plan: repo glowne docs/PLANY-E5-WOJNY-KRONIKA-2026-10-21.md "## PLAN kronika" (E, F, G); kontrakt danych: docs/KRONIKA-FORMAT.md (wygrywa przy sprzecznosci).
Silnik powstaje w 4 krokach lancucha: A1 dane i termy, A2 zachowanie/doba/okna/wybory/zapis (+ akcje T/R), A3 lektor i obrazki (polecenie lancucha 21.10;
wczesniejszy podzial mowil "A3 akcje swiata i obrazki, A4 lektor") - akcje swiata, progi m105kronika i autotest zostaja na kolejne kroki.

## 2026-10-22 - K0 KRYTYK KOMPLETNOSCI (stan galezi wobec KRONIKA-FORMAT 1-20 i PLAN kronika E, F, G, H, I)

**Problem:** przeglad kazdego rozdzialu kontraktu i punktow planu (termy, akcje, kolumny, sekcje, linie logu, progi, klucze MCM, flagi autotestu, zapis, okna
powracajace, para P1, lista chronionych, md5 lektora) wobec kodu i narzedzi po poprawkach (c779e57d) oraz wobec logu autotestu K0a/K0b z 0ffd5d9f (scratchpad
7aee5f12/logi-noc/k0-k0a: 13 okien, nagrania .ogg graja "po kluczu kr_NNN_w, md5 tekstu zgodny", m105kronika 22/22). Silnik ma wszystko z planu E/F/G
i kontraktu; braki: (1) kontrola md5 lektora w kronika_check czytala arm_lektor.tsv (okna ROT i prologu, bez kluczy kr_) innym wzorem tekstu - dla 293 nagran
Kroniki nie zadzialala nigdy (zmiana tekstu nagranego okna = cisza w grze bez slowa walidatora); (2) 016.2 `po` = `kr:016.1+7 & mv:m01 | mv:p10` - m01 (tylko
classic) i p10 (tylko prolog) sa w kampanii drugiego trybu pomijane w pierwszej dobie (CanonCompanions Res.Skip), wiec skladnik byl prawdziwy od startu w obu
trybach: Szepczacy Las mogl pojsc wariantem "Jaime uciekl" przed jego schwytaniem; (3) k00 nie sprawdzal w autotescie wiesci ani wariantu (plan I K0a: 000.1a
wymaga gracza na Polnocy, wiesc 000.1 - gracza w armii; bieg k0a: 0 wiesci, 0 wariantow); (4) KRONIKA-FORMAT 1/16.5/17 opisywaly nagrania w arm_lektor.tsv
(kolumna 5) i k00 z 2 oknami.
**Przyczyna:** walidator A-kroku danych pisany przed manifestem nagran (fcf2b976); dane 016.2 bez znajomosci pomijania wierszy drugiego trybu; k00 z kroku danych.
**Zmiana:**
- `tools/kronika/kronika_check.py`: manifest `Lektor/EN/arm_lektor_kronika.tsv` (World.lektor_kr); md5 tekstu wzorem nagraj_kronika.py = Chronicle.Voice (voice_text);
  BLAD - tekst inny niz nagrany, plik nagrania brak / md5 pliku inny (wyzerowany), dubel albo zly wiersz manifestu, klucz kr_ w arm_lektor.tsv (Lektor.LoadIndex
  czyta go przed manifestem - zaslania wiersz i gra bez zasady md5); UWAGA - nagranie `nieaktualne`, lektor=1 bez nagrania (poza wersja=pelna, ktorych skrypt nie
  nagrywa), nagranie bez wiersza lektor=1, lektor=1 w wariancie bez wlasnego tekstu (gra nagranie glownego; skrypt nagralby pusty tekst); UWAGA - `mv:X` wiersza
  tylko-classic/tylko-prolog w alternatywie ` | ` (World.move_mode z kolumny mode #move).
- Dane: 016.2 `po` = `kr:016.1+7 & mv:m01 & mv:p10` (okno za schwytaniem w obu trybach; tekst i md5 lektora bez zmian). k00: 000.5 (forma wiesc dla kazdego, akcje
  `wiesc:000.5_N1`, `plotka:000.5_P:vlandia:10`) i 000.6 (zmienne `{KING}`=@wladca:vlandia, `{ME}`=@gracz; wariant a `pelna` - przy kluczu Full Versions NIE nie
  moze wygrac; wariant b `obie` bez wlasnego tekstu - tekst i zmienne glownego, flaga kr_test_6b); oba w lancuchu kr:000.4+1 / kr:000.5+1 (dzien opowiesci ok. 12-14).
- `docs/KRONIKA-FORMAT.md`: 1 (k00 6 okien, manifest nagran), 10 (`mv:` wiersza z trybem), 16.5 (wzor tekstu nagrania, zmiana tekstu nagranego okna), 17 (nowe reguly).
- Bez zmian C# (silnik kompletny wobec planu E/F/G; build 0 bledow, 7 starych ostrzezen).
**Braki opisane, nie robione (waga):**
- (srednia, tresc) FORMAT 8: nowe wiersze #move dla Theona (072.3/087/108), Brienne i Podricka (083), Jaimego (107) "w paczce silnika" - nie dodane; okna maja
  warianty na brak tych zmian (098.2a/c, 111.1 przyjmuja Brienne w battania_1 albo battania_6), wiec dzialaja, ale swiat ich nie zmienia. Nowe zmiany stron = decyzja
  tresci przy paczkach K6-K10 (dni 1300+), nie K1.
- (srednia, swiat) `pop` POMINIETA (10 wierszy, 22 uzycia, K2-K10), `kod:zaraza` czesc (1), `kod:dzwony` czesci (1)(2) - ksiega ludzi bez przyczyn kronika/choroba/
  pozar; okna sie pokazuja, ludzie bez zmian (linia mowi o tym). Nowa przyczyna PeopleBook + droga do puli okregu = osobna galaz z testem ksiegi (zamknieta ekonomia).
- (niska) `task` bez wpisu tablicy ogloszen (wiadomosc) i `alliance` zawsze POMINIETA (P5, K9/K10) - swiadome, FORMAT 7 i 11.
- (niska, dokument) prog KW-12 w repo glownym (PLANY-E5-WOJNY-KRONIKA, H) dalej 24 KB, kod i sprawdz_logi 48 KB - poprawic przy scaleniu (inny katalog).
- (niska, test) K0b z flaga `b`: w 000.2 opcja b jest celowo wyszarzona - bieg sprawdza tylko zapas (bierna c); wymuszenie sprawdza flaga `a` (tez gold @gracz).
  "Zapis przy stanie 'a', wczytanie, lista wraca" (plan I K0a) - autotest odpowiada na liste w < 1 s, wiec w biegu prawie nigdy; sprawdzone harnessem (Build-Import-Build).
- (niska, repo glowne) tools/lektor/nagraj_kronika.py czyta komorki surowo: wariant z lektor=1 bez wlasnego tekstu dostalby nagranie pustego tekstu (dzis 0 takich
  wierszy - walidator ma UWAGE).
- (info) 5 okien bez glosu: 051.1a (brak nagrania), 075.3, 079.1, 090.1, 090.1a (nieaktualne) - nagrac po odnowieniu ElevenLabs (kronika_check: 5 UWAG).
**Ryzyko / co sprawdzic:**
- 016.2 w kampanii z prologiem idzie teraz dopiero po rozstrzygnieciu p10 (schwytanie Jaimego, war:battania:vlandia+5 - zwykle dni po NedEvent, dlugo przed od 130);
  p10 czekajace ponad 195+30 dni = okno przepada (wczesniej szlo wariantem a).
- k00 000.6: `{KING}` bez wladcy korony (krolestwo wyeliminowane) = okno czeka z powodem "zmienna KING bez wartosci" (k00 tylko z flaga testu).
- kronika_check liczy md5 293 plikow .ogg (ok. 90 MB) przy kazdym uruchomieniu - ok. 0,5 s.
- Testy: kronika_check WYNIK OK (0 bledow, 5 UWAG lektora - jak linia startu silnika: z nagraniem 289/311, bez 18 = 17 pelna + 051.1a, stary tekst 4);
  scratchpad 7aee5f12/kr-kryt/neg_lk.py 11/11 (7 regul + 4 bez falszywych, kod wyjscia 1); stare testy walidatora kr-final/t_neg, t_neg3, t_pos - 0 roznic,
  t_neg2 - ten sam licznik bledow (kr_005_1 w arm_lektor.tsv: dawniej "tekst zmieniony", teraz "klucz Kroniki w arm_lektor.tsv"); kronika_silnik WYNIK OK;
  harness A1 (prawdziwy Armoury.dll): k00 6/1/0, zle wiersze 0, nieznane termy 0, 000.6a/b z tekstem glownego; m105kronika na logu k0a 22/22 (bez zmian narzedzia).
**Status:** DO SPRAWDZENIA (autotest K0a: linie "wiesc 000.5", "akcja 000.5 #2 wiesc:000.5_N1 -> OK", "okno 000.6 ... wariant b" z imieniem wladcy w tytule; nic nie wgrane).

## 2026-10-22 - K0 POPRAWKI po trzech recenzjach (bezpieczenstwo, kontrakt, styk-api)

**Problem:** recenzje silnika K0 (HEAD 0ffd5d9f) i ich weryfikacja w dekompilacji 1.4.8 - stan zly: ukrycie postaci w drodze (opozniony teleport gry
przywracal ukrytego / robil dowodce w stanie Disabled), zbiorka `muster` do osady wroga (druzyna AI wchodzila do wrogiego miasta - 022.1 Ashemark, 023.1
Deepwood Motte, 085.1 Winterfell Boltonow), powrot z ukrycia do osady wroga (SeatOf krok 4 / PickTown), klucz ChronicleOfWesteros NIE trzymal ukrytych na
zawsze (HiddenDaily pomijal kazdy koniec, takze `rotday:`), `kr:` okna powracajacego nigdy nie bylo "nigdy" (Melisandre 092.1 do `kr:112.1` ukryta na zawsze),
pusty `gracz` wariantu = kazdy gracz (105.1a, 073.1a/b), wariant bez tekstu bez zmiennych glownego (045.1a/b/c "villages around  more than once"), smierc glowy
rodu bez innego dziedzica niszczyla rod razem z ukrytymi (gra pomija wylaczonych w GetHeirApparents). Drobne: zapis (wyjatek Export -> pusty stan, SaveText.Sync
poza try, przyciecie rejestru gubilo pierwszy dzien instancji i nie przycinalo wyborow, powod POMINIETE w zapisie ponad 100 B), lista wyboru znikajaca bez konca,
`p:vassal` u najemnika, `!term` z brakujacym tokenem = nigdy, zapis sprzed paczki pomijal okna z `do` przed soba, succeed przy wecie smierci (CanHeroDie),
`leader` rodu bez glowy (NRE), zaraza w trakcie bitwy, `plan:BlackwaterEvent` poza osia Fabuli; narzedzia: KW-0/KW-1 bez nieznanych termow, KW-6 szukal
napisu, ktorego silnik nie pisze, KW-15 bez die/succeed, KW-12 24 KB za nisko dla dlugiej kampanii; k00 bez okien hide/unhide (plan I K0a).
**Przyczyna:** kod K0 krokow A2-B2 (opis w uwagach recenzji; kazda uwaga zweryfikowana - raporty "WERYFIKACJA" w poleceniu lancucha).
**Zmiana:**
- `CanonCompanions.cs`: Hide - `IsTraveling` = CZEKA ("w drodze (opozniony teleport gry)"); Hostile(Settlement, Hero) - Unhide: osada z akcji i siedziba rodu
  bez osad wroga, PickTown(culture, hero, noEnemy) z filtrem tylko dla powrotu (wander bez zmian); BeforeHeroKilled (nowy sluchacz BeforeHeroKilledEvent
  w CanonCompanionsBehavior): umiera glowa rodu, GetHeirApparents puste - pierwszy ukryty dorosly lord rodu wraca (Unhide) i gra robi go dziedzicem; linia
  `Postacie kanonu (m101): KONIEC UKRYCIA (dziedzic) ...`.
- `Chronicle.Canon.cs`: muster - osada w wojnie z krolestwem: SetMoveGoToPoint(GatePosition) bez wejscia (linia "zbiorka pod brama"); succeed - `CanDie(Murdered)`
  przed zmiana glowy rodu (weto - POMINIETA bez sukcesji); leader - rod bez glowy POMINIETA; die/succeed z listy chronionych po wydarzeniu - `| lista
  chronionych: X spelnione` w linii akcji.
- `Chronicle.Actions.cs`: HiddenDaily(int) - koniec ukrycia liczony takze przy kluczu NIE (kr: = nigdy, FORMAT 3.3/11). `Chronicle.Codes.cs`: zaraza pomija
  partie w bitwie (InBattle).
- `Chronicle.cs`: CheckPacks - okno z `do` minione po `do`, bez `do` po `od` + 30; SyncData cale w try (potkniecie "SyncData"); wyjatek Export - LastGood()
  (ostatni zapis albo wczytany napis, linia "zapis arm_kronika: blad Build - ..."); SaveWarnBytes 48 KB.
- `Chronicle.State.cs`: Skip - powod w zapisie najwyzej 24 znaki (instancja) / 60 (okno); PruneInstances usuwa tez wybor (kc); zapis "ki NNN.w dzien" (pierwszy
  dzien instancji okna) i wczytanie (minimum z kr i ki; stare zapisy bez ki jak dotad).
- `Chronicle.Recurring.cs`: Exhausted(okno) - max:N zuzyty, zadna instancja w kolejce, nic rozstrzygniete; `Chronicle.Terms.cs`: KrTrue - wyczerpane = nigdy;
  `p:vassal` bez najemnika (IsUnderMercenaryService); brak tokenu: `!term` = prawda.
- `Chronicle.Data.cs`: CheckRows - wariant z pustym gracz bierze gracz glownego; wariant bez tekstu bierze zmienne glownego (wlasna NAZWA ma pierwszenstwo);
  ScanTerms nie liczy dziedziczonego gracz drugi raz. `Chronicle.Choice.cs`: Pend.Lost - po 5 zniknieciach listy opcja bierna ("lista znikala 5 razy").
- Dane: 092.1 `hide:lord_1_12:kr:112.1 | ev:NightKingDeathEvent | rotday:2600` (zapas, gdy 112.1 besieged:town_B2 nie ruszy); 032.1 `plan:BlackwaterSiegeEvent-25`
  (BlackwaterEvent nie ma na osi Fabuli - term byl zawsze prawdziwy); k00: 000.3 (log/nic, `po kr:000.2+2`: `hide:ROTwanderer29:kr:000.4 ; hide:ROTwanderer30`
  - Dick Crabb grupa C, Ser Harlan Blackhare grupa D, oba bez #move) i 000.4 (`po kr:000.3+3`: `unhide:ROTwanderer30`). Tekstow okien nie ruszalem (md5 lektora bez zmian).
- `tools/kronika/kronika_check.py`: koniec ukrycia = pelny warunek (parse_cond); UWAGA - koniec tylko na `kr:` okna powracajacego; BLAD - `kr:<powracajace>=x`;
  UWAGA - `plan:X` spoza osi (os z CrashScribe/src/Fabula.cs, zapas - lista); pusty gracz wariantu jak glowny przy UWADZE "inni bez kolumny gracz".
- `tools/sprawdz_logi.py` (m105kronika): KW-0 + nieznane termy w danych 0; KW-1 + linie NIEZNANY TERM i "nieznane termy dzis"; KW-6 - linie zlota
  z ", zdjete X - dopisane Y"; KW-12 < 48 KB; KW-15 + die/succeed z linii akcji; akcja w linii moze miec spacje; ukrycie z ' | ' w koncu; linia ukrytego dziedzica.
- `docs/KRONIKA-FORMAT.md`: 3.1 (gracz i zmienne wariantu), 3.2 p.4, 3.3 (klucz NIE konczy ukrycia z kr:), 7, 10 (kr:/nic/okna powracajace/=x, plan:, p:vassal,
  tokeny), 11 (hide - warunek, muster pod brama, alliance/pop stan K0, Melisandre z zapasem, osada wroga), 12 (stan zaraza/dzwony), 13 (odstep od okna, max,
  1 widoczna na dobe, "ki"), 15 (linia "lista chronionych"), 17 (nowe reguly), notka "Stan po poprawkach K0".
- Bez zmian (swiadomie): R6 (Vars akcji czekajacej - scenariusz niemozliwy, News nie czeka); kronika_silnik.py (nie czyta kolumny gracz); opcjonalna linia
  "UKRYCIE przerwane - rod niszczony" w OnClanDestroyed (HiddenDaily loguje to dobe pozniej); progi w repo glownym PLANY-E5-WOJNY-KRONIKA H (KW-12 24 KB) - inny
  katalog, do poprawy przy scaleniu.
**Ryzyko / co sprawdzic:**
- BeforeHeroKilled teleportuje ukrytego W TRAKCIE KillCharacterAction (przed wyborem dziedzica) - nowy wzorzec; w dekompilacji zdarzenie idzie bez zadnej petli po
  bohaterach, ale autotest musi zabic glowe rodu z jedynym dziedzicem ukrytym (tez sciezka ApplyByDeathMark po bitwie).
- R-1: pierwsza doba z kluczem NIE wypuszcza naraz wszystkich ukrytych z koncem `kr:` (zgodne z FORMAT 3.3/11) - decyzja Jeffa, gdyby to przeszkadzalo.
- muster pod brama osady wroga: druzyny stoja pod wrogim zamkiem (AI moze zaczac oblezenie albo odejsc przy nastepnej mysli) - linia konca zbiorki liczy
  odleglosc do bramy jak dotad.
- Exhausted liczy max:N z licznika "kl" (rosnie przy WYDANIU instancji) - instancja wydana i potem pominieta po 30 dniach wyczerpuje okno (tak ma byc).
- Zapis: "ki" to kilka linii po ok. 15 B; powody POMINIETE w zapisie skrocone - SkipWhy nikt nie czyta, pelny powod jest w linii logu.
- Testy (scratchpad 7aee5f12/kr-fix): harness poprawek 52/52 (prawdziwy DLL odbiciem: R-1, R8, R7, N-1, R-2, R-7, R-3, R-4, R4, R-16, R9, R3, styk R1, dane);
  stare harnessy na nowym DLL: A1 42/42, A2 33/35 (jak przed zmiana - oczekiwane od B1/B2), A3 44/44, A4 43/43 (wywolanie HiddenDaily/1), B1 67/67, B2 126/126;
  testy syntetyczne m105kronika test_kw_fix.py 46/46 (stary test_kw_b2 36/38 - kw6 'z niczego' i kw12 25 KB maja nowe oczekiwania); kronika_check WYNIK OK
  (0 bledow, 0 uwag po poprawce danych; przed poprawka danych 2 nowe UWAGI - 032.1 plan i 092.1 koniec ukrycia); kronika_silnik WYNIK OK; sprawdz_narzedzie czysto;
  build Armoury 0 bledow, 7 starych ostrzezen.
- NIEPEWNE do autotestu: wywrotka przy dowodcy Disabled (R1 - przed poprawka), oblezenie z wroga druzyna w srodku (R2 - przed poprawka), R-2 droga (b) (30 dni bez
  okna przy zatorze kolejki), czestosc pustego @cel u rodow bez lenna (R-7), rozmiar zapisu w dlugim biegu (R8/R-16).
**Status:** DO SPRAWDZENIA (autotest K0 z k00 000.3/000.4, 022.1 muster, smierc glowy rodu z ukrytym dziedzicem; nic nie wgrane).

## 2026-10-22 - K0 krok B2: bandy, zadania, akcje kanonu, tylko-pelne, turniej, zbiorka, uchwyty kod:, wyzwalacz brokenmen

**Problem:** po B1 w danych K1-K10 dalej "POMINIETA (akcja jeszcze niewspierana)": band 31 wierszy, die 30 (43 uzycia), succeed 17, prisoner 13 (18), task 12 (13),
muster 11, clan 10, free 6 (8), own 5, truce 5 (7), court 4, tourney 3, alliance 2 (3), wed 1, leader 1; uchwyty kod: wielka_rada 8, zaraza 1, dzwony 1 konczyly
sie "brak uchwytu"; okno powracajace 045 (wyzwalacz brokenmen) czekalo na zawsze ("wyzwalacz niewspierany w K0").
**Przyczyna:** nowa funkcja (KRONIKA-FORMAT 6, 7, 11, 12, 13; plan P5, P6, J K1-K10).
**Zmiana:**
- `Armoury/src/StoryActions.cs` (nowy, plan P6): pomocniki scen PRZENIESIONE 1:1 z prywatnych metod m104-e5 - BanditClan, BanditsById, SpawnBand
  (Prologue.World.Clans) i GtReady + GtProclaim/GtSetHeroes (GrandTourney.CanonApi odbiciem, Prologue.World.Tourney); Prologue wola je przez swoje stare nazwy
  (cienkie opakowania z WStumble - logika scen bez zmian). Nowe: wspolny sufit band fabularnych okregu (StoryBandsIn, RegionCap = 4; przedrostki armoury_ffraid,
  armoury_prl_mountain, armoury_prl_clan, armoury_kr_; okreg = okreg OutlawLaw osady domowej bandy).
- `Chronicle.Actions.cs`: klasa `Actions` partial; Impl ma procedury dla wszystkich 39 akcji FORMAT 11; Bands = rejestr band; WorldDaily (bandy, zadania, zbiorki -
  w dobie PRZED kluczem ChronicleOfWesteros, klucz NIE nic nie cofa); EventNo, RowVersion; zapis "kb", "kz", "km"; linia doby ", zadania n, zbiorki n"; kod: -> RunCode.
- `Chronicle.Bands.cs` (nowy): band:bNNN - ludzie z puli okregu (SpawnBand: OutlawLaw.RaidDraw, nikt z niczego), sufit 12 band Kroniki (MaxBands) i 4 band
  fabularnych okregu, klan spoza Clan.BanditFactions -> looters z nazwa herszta, zachowanie rabunek/patrol/oboz, koniec terminu -> FreeFolkRaids.Home (ludzie do puli
  okregu, zloto/towar do wsi), rozbita przez gracza (MapEventEnded: banda po stronie przegranej, druzyna gracza po stronie wygranej) -> raz po_rozbiciu + zadania
  defeat; list = 0 -> Chronicle.NoBounty (NoticeBoard.Watch pomija bande - jedna linia w NoticeBoard.Watch.cs). Linia "banda bNNN 'nazwa' (id) | ludzi ... (z puli a,
  zabrani z domow b) | ... | region r: band fabularnych m/4 | ...".
- `Chronicle.Tasks.cs` (nowy): task:tNNN - wlasny rejestr bez QuestBase (zapis "kz", najwyzej 8); deliver/prisoners przy wejsciu druzyny gracza do osady celu
  (SettlementEntered) albo w dobie, gdy w niej stoi - towar na rynek osady, jency do lochu osady (PeopleBook.Transfer); defeat - przez rozbicie bandy; termin ->
  NIEUDANE; nagroda zwykla droga akcji. Tablica ogloszen: BEZ wpisu (wpisy tablicy to QuestBase i listy goncze - nowy rodzaj wpisu = zmiany karty/warunkow/przyciskow,
  ryzyko bez potrzeby) - wiadomosc dla gracza + linia.
- `Chronicle.Canon.cs` (nowy): clan/court/lead/prisoner/free/wander/die/marry/wed przez CanonCompanions.ApplyAction (+ z Kroniki POMINIETA: postac z wierszami #move -
  CanonCompanions.HasMoveRows, die z listy chronionych przed wydarzeniem ROT, die glowy rodu/wladcy w chwili akcji z wiersza lekka/obie); succeed (tylko pelna, wyjatki
  42/49/52/80; dziedzic z rodu umierajacego glowa rodu przed smiercia - wzor Prologue.End); own/ruler/leader/truce (tylko pelna; truce bez wojny wymuszonej przez ROT -
  Prologue.RotWarForced, bez gracza-wladcy); alliance - zawsze POMINIETA z powodem (P5); tourney (GrandTourney CanonApi.ProclaimRoyal odbiciem; bez GrandTourney
  POMINIETA; turniej juz jest - OK bez nowego; oblezenie - CZEKA); muster (jednorazowy rozkaz SetMoveGoToSettlement wolnym druzynom lordow, zapis "km", linia konca
  "zbiorka ... koniec: na miejscu a z n").
- `Chronicle.Codes.cs` (nowy): RunCode - test (jak A2), zaraza (ranni 10-20% w partiach w promieniu 30 od town_K1, security -5; ludzie miasta bez zmian - ksiega
  ludzi bez przyczyny choroba), dzwony (30% kasy town_EW4 -> glowa rodu-wlasciciela akcja gold; ludzie i Devastation bez zmian - powody w linii), wielka_rada
  (lekka: relacje glow rodow krolestw z kolumny gracz okna 117.1; pelna: + glosy i flaga k117_wynik_*, korona bez zmian).
- `Chronicle.Recurring.cs`: wyzwalacz brokenmen (okreg z rabunkiem w 30 dniach - PeopleBook.LastStrike - i > 30% ludzi poza domem: uchodzcy + pula wyrzutkow wobec
  ludzi w domu we wsiach okregu; klucz okreg-okres 90 dni); Unsupported pusty.
- `Chronicle.cs`: WorldDaily w dobie, WorldText w linii startu, sluchacze MapEventEnded i SettlementEntered w ChronicleBehavior.
- `CanonCompanions.cs`: HasMoveRows(Hero), PlayerSideOf(Hero) - tylko odczyt. `Prologue.Wars.cs`: RotWarForced - tylko odczyt WarForced.
- `tools/sprawdz_logi.py` (m105kronika): KW-5b opis B2, KW-5c INFO (alliance, tylko pelna), KW-14 + sufit okregu i "z puli" z linii narodzin bandy, KW-14b losy band
  INFO, KW-16 zadania i zbiorki INFO. `tools/kronika/kronika_silnik.py` (nowy): kazda akcja z danych ma procedure albo jest swiadomie POMINIETA (procedura bez Res.Done),
  kazdy kod: ma galaz RunCode, kazdy wyzwalacz powtorz jest wspierany.
**Ryzyko / co sprawdzic:**
- W grze nic nie sprawdzone (zakaz gry). Autotest: 008.1 (band b008 - linia "banda", KW-14), 020.1/020.2 (band b020 + task t020 defeat), 014.1 (task t014 deliver -
  wejscie do town_B5 z 150 grain), 003.1 (tourney town_EW4 - linia GrandTourney), 014.1/022.1 (muster - linia konca), 042.1 (succeed lekki Balon), 049.2 (succeed Lysa),
  076.2 (kod zaraza), 115.1 (kod dzwony - linia zloto), 117.1 (kod wielka_rada), 045 (brokenmen - instancja w kolejce przy rabunkach Dorzecza).
- Banda z klanem pomniejszej frakcji idzie pod looters (CreateLooterParty tylko dla band) - tekst okna mowi o Bractwie/Stone Crows/Synach Harpii, na mapie "Looters" z
  wlasna nazwa. Klan bandycki z nazwa jest w stalej wojnie z kazdym krolestwem.
- muster: rozkaz jednorazowy - AI przy nastepnej mysli moze go zmienic (zgodnie z "miekko"); linia konca pokaze skutecznosc.
- zaraza/dzwony bez czesci ludzi (ksiega ludzi bez przyczyn choroba/pozar) - decyzja o nowych przyczynach PeopleBook jak przy pop.
- MapEventEnded: rozbicie liczone, gdy banda jest po stronie przegranej i druzyna gracza po stronie wygranej (tez gracz w cudzej armii). Banda scalona przez gre
  z inna (HideoutPurge) zniknie z rejestru jak rozbita bez gracza (zadania defeat NIEUDANE).
- Testy: harness B2 (scratchpad 7aee5f12/kr-b2/harness, prawdziwy DLL odbiciem) 126/126 - rejestr 39/39 procedur, klasa S, EventNo, zle argumenty 21 akcji, brak
  obiektow bez wyjatku, tylko-pelne (wiersz lekka / pelna + klucz NIE / pelna + klucz TAK / succeed 42 vs 43), World Changes NIE dla 19 akcji, zapis kb/kz/km tam
  i z powrotem + zle linie, doba swiata (zadanie po terminie NIEUDANE, bez definicji - usuniete), brokenmen wspierany, StoryActions/Prologue/CanonCompanions metody.
  Stare harnessy: A1 42/42, A3 44/44, A4 43/43, B1 67/67, A2 33/35 (oczekiwane: gold wspierane od B1, kod:zaraza ma uchwyt od B2). test_kw.py 35/35, test_kw_b2.py
  38/38 (+3: dobry log B2, sufit okregu, banda bez "z puli"). kronika_check WYNIK OK; kronika_silnik WYNIK OK (34 akcje: 32 z procedura, pop i alliance swiadomie
  POMINIETE; 3 uchwyty; 9 wyzwalaczy). Build Armoury: 0 bledow, 7 starych ostrzezen. sprawdz_narzedzie czysto.
**Status:** DO SPRAWDZENIA (autotest K0 z oknami band/zadan/kanonu przed wgraniem; nic nie wgrane).

## 2026-10-22 - K0 krok B1: akcje swiata i ekonomii (gold, security, loyalty, ware, garrison, item, wound, skill; pop - POMINIETA)

**Problem:** akcje swiata z danych K1-K10 konczyly sie "POMINIETA (akcja jeszcze niewspierana)": gold 25 wierszy (26 uzyc), security 30, loyalty 30, pop 10 (22),
wound 15 (16), garrison 5, item 4, skill 2 (4), ware 0 (skrypt scratchpadu 7aee5f12/kr-b1/akcje_uzycie.py: kolumny skutki okien i opcji, po_rozbiciu, nagroda;
k00 pominiety). Uchwyty kod: w danych: wielka_rada 8, zaraza 1, dzwony 1 (poza zakresem B1).
**Przyczyna:** nowa funkcja (plan E "Chronicle.Actions.cs", KRONIKA-FORMAT 11).
**Zmiana (`Chronicle.Actions.cs`, procedury w Impl, wszystkie klasy S - klucz ChronicleWorldChanges NIE pomija je z linia jak dotad):**
- `gold:<platnik>:<odbiorca>:<kwota|N%>` - kiesa (Purse): bohater (id i tokeny), rod (glowa; rod gracza - gracz), osada (kasa SettlementComponent), `@kasa:<osada>`,
  `@skarbiec:<k>` (KingdomBudgetWallet). Bohater martwy albo Disabled (ukryty - kiesa poza obiegiem), rod wyeliminowany / bez glowy, krolestwo wyeliminowane,
  platnik = odbiorca - POMINIETA. Zamknieta ekonomia: placi min(kwota, zloto platnika, miejsce w kiesie odbiorcy); N% - z biezacego zlota platnika. Bez skarbca -
  zdarzenie gry GiveGoldAction (bohater-bohater, bohater->osada, osada->bohater i osada->osada przez partie osady - gra obcina do kasy; ApplyForSettlementToCharacter
  celowo nie, bo nie obcina do kasy osady); ze skarbcem - arytmetyka "najpierw platnik, potem odbiorca dostaje dokladnie to, co zeszlo" + CrownIncome.ShiftSnapshot
  (przelew poza wplywami dnia), CirculationWindows.NoteHeroGold, ClanIncomeBook.NoteInflow(KRansom) dla bohatera AI (wzor CapitalTreasury) i wiadomosc gracza (EN).
  Linia przy kazdej probie przelewu: "zloto NNN.w: platnik -> odbiorca kwota (zaplacone k[, niedoplata n][, N% z m][, zdjete x - dopisane y])"; nic zaplacone - POMINIETA.
- `security`/`loyalty:<osada>:<d>` - Town.Security/Loyalty wprost (gra trzyma 0-100), tylko miasto i zamek. Sprawdzone w kodzie: DailyLife i PeopleBook tych
  wartosci nie zapisuja; zapisy wprost robia tylko jednorazowe ciosy (CitySack, WarLedger, TownDebt, KingdomTreasury.LordLevy, kredyty TownFolk, NoticeBoard);
  modele (BKLoyaltyModel, model bezpieczenstwa + postfiksy KsHunger) licza dobowy dryf.
- `pop:<osada>:<d>` - POMINIETA "akcja jeszcze niewspierana: ksiega ludzi bez przyczyny kronika ..." (KW-5b INFO) z liczba ludzi osady. PeopleBook ma drogi ruchu
  (Move, TownShift, pula OutlawLaw), ale bez przyczyny "kronika" (FORMAT 11) - cudza przyczyna (rabunek MRaidFlee, podloga TakeForFloor, migracja) zepsulaby
  rownania i progi ksiegi; nowa przyczyna (MKinds 54 + pula wyrzutkow z wlasnym licznikiem) to zmiana PeopleBook/OutlawLaw - osobna decyzja.
- `ware:<z>:<do>:<towar>:<ilosc>` - Settlement.ItemRoster z rynku do rynku (miasto/zamek/wies), najwyzej tyle, ile zrodlo ma (sztuki z modyfikatorami), bez zlota.
- `garrison:<osada>:<ludzi>:<skad>` - skad = osada: zdrowi zolnierze (bez bohaterow i wightow) z jej zalogi proporcjonalnie do skladu, PeopleBook.Transfer (FTransfer);
  skad = pula: ochotnicy notabli osady i jej wsi (sloty VolunteerTypes - ten sam zasob, co autowerbunek zalogi gry), kazdy rekrut przez ksiege ludzi jak
  PeopleBook.A5Post (Straz - WatchLaw.GiveLevy; pobor wylaczony - GiveNone; inaczej TakeLevy MLevyA5 + GiveNew FLevyA5, reszta ponizej podlogi - GiveNone FNoneFloor).
  Niedobor w linii; zaloga w bitwie - CZEKA; osada bez GarrisonParty - POMINIETA.
- `item:<bohater>:<przedmiot>:<skad>` - dragon_* POMINIETA (P4); posiadacz: bohater (wyposazenie, stroj, druzyna, ktora prowadzi), rod (zywi bohaterowie i ich druzyny),
  osada (rynek, schowek). Nikt nie ma - POMINIETA. Cel: gracz - ekwipunek druzyny; inna postac - pasujacy slot BattleEquipment (pusty, inaczej zamiana - zdjety
  przedmiot wraca na miejsce wzietego), bez slotu - druzyna postaci, inaczej cofniecie i POMINIETA. Juz u celu - OK bez zmian.
- `wound:<bohater>:<dni>` - zycie do 1 i rejestr ran (`_wounds`, zapis "kj bohater do zrodlo" w arm_kronika); doba Kroniki (Actions.Daily, przed czekajacymi) trzyma
  rane (zycie z powrotem do 1; gra leczy co godzine) do dnia konca, potem "rana ... koniec terminu"; smierc / brak postaci - wpis usuniety z linia; klucz World Changes
  NIE - rana nie trzymana, termin plynie; druga rana - dluzszy termin; bohater w bitwie - CZEKA. Linia doby: ", rany n".
- `skill:<bohater>:<umiejetnosc>:<d>` - HeroDeveloper.SetInitialSkillLevel(0-330) - poziom i XP razem (ChangeSkillLevel nie umie odjac; SetSkillValue bez XP cofalby
  sie przy nastepnym XP); bez AddSkillXp, wiec ksiega umiejetnosci (SkillLedger) tego nie liczy jako nauki.
- `tools/sprawdz_logi.py`: tylko opis INFO progu KW-5b (lista akcji wspieranych); sprawdz_narzedzie czysto, test_kw.py 35/35.
- `docs/KRONIKA-FORMAT.md` rozdz. 11: notka "Stan po K0 krok B1".
**Ryzyko / co sprawdzic:**
- W grze nic nie sprawdzone (zakaz gry). Autotest: okna z gold (np. 058.2 K5 - skarbiec -> kasa -> wladca Strazy), security/loyalty (009.1 town_EW4),
  garrison:pula (034.1 town_A2), item heartsbane (090.1), wound/skill (037.1 Jaime) - linie "akcja ... -> OK (przed -> po)" i "zloto ...", potkniecia 0, KW-6 bez "z niczego".
- Przelewy ze skarbcem i kasa osady przez arytmetyke (bez zdarzenia gry): ksiegi obiegu widza zmiane pul (suma swiata bez zmian); dobowe "Przeplywy osad"/kasy moga
  pokazac ja jako nierozpoznana - sprawdzic w pierwszym biegu z gold (te same wzory co CapitalTreasury i KingdomTreasury).
- garrison:pula bez kroku BK DeduceManpower (A5Post wola go przy GarrisonLevyFromBk, metoda prywatna w PeopleBook) - BK nie zdejmie ludzi z populacji BK; ksiega
  ludzi Armoury jest zamknieta. Rekruci Kroniki licza sie w liczniku "A5" ksiegi pochodzenia.
- item: wyposazenie AI moze potem zmienic AiGear/HeroGear albo inny mod (unikat moze zostac sprzedany/zdjety) - Kronika przenosi tylko w chwili akcji.
- wound trzyma zycie 1 co dobe: postac (tez gracz przy 050.2a) nie walczy przez caly termin (do 120 dni u lordow 4_21, 3_1, 1_5) - zgodne z danymi.
- Harness A2 (stary): test "gold - niewspierana (A3)" teraz ZLE - oczekiwana zmiana (gold jest wspierane); poza gra Hero.MainHero rzuca NRE, akcja konczy sie
  "POMINIETA (wyjatek ...)" z potknieciem - w grze tej sciezki nie ma.
- Testy: harness B1 (scratchpad 7aee5f12/kr-b1/harness) 67/67 - rejestr (9 procedur, klasa S, 39 akcji), zle argumenty kazdej akcji (linie POMINIETA ze skladnia),
  brak obiektow w swiecie bez wyjatku na zewnatrz (platnik/odbiorca/osada/bohater/przedmiot nieznany, dragon_*), klucz World Changes NIE dla wszystkich 9 (bez linii
  zlota), zapis kj tam i z powrotem (Build -> Import -> Build identyczny), zle linie kj, doba z rana nieznanej postaci (wpis usuniety z linia), Reset. Stare harnessy:
  A1 42/42, A2 34/35 (oczekiwane wyzej), A3 44/44, A4 43/43. kronika_check: WYNIK OK. Build Armoury: 0 bledow, 7 starych ostrzezen.
**Status:** DO SPRAWDZENIA (autotest K0 z oknami akcji swiata przed wgraniem; nic nie wgrane).

## 2026-10-22 - K0 krok A4: ukrycie i powrot postaci (hide/unhide), term kr: w zmianach stron, progi m105kronika, skrypty wgrania

**Problem:** (1) Kronika nie umiala ukryc postaci ani jej przywrocic (akcje hide/unhide "jeszcze niewspierane"; 31 hide i 15 unhide w danych K1-K10); wiersze
#move m11 Sandor, m20 Osha, m21 Sam zabijaly postac ('remove' = KillCharacterAction.ApplyByRemove), wiec powroty z okien 082 (Sam) i 089 (Sandor) nie mialy
kogo przywrocic. (2) Term kr:NNN.w w wyzwalaczach arm_canon_moves (pary 'kr': m02, m04, m18, m22) byl dla CanonCompanions nieznanym rodzajem ("nigdy") - okno
Kroniki nie moglo poprzedzic ruchu. (3) Brak progow logu Kroniki (plan H). (4) wgraj-v5.ps1 i bieg-bitwa.ps1 kopiowaly z Lektor tylko *.wav i *.tsv - 293 nagrania
kr_*.ogg nie trafilyby do gry ("nagran Kroniki 0 ... odrzucone 293 (brak pliku)", okna ciche bez bledu).
**Przyczyna:** nowa funkcja (plan P1, P3, H, L; KRONIKA-FORMAT 8, 11, 15).
**Zmiana:**
- `CanonCompanions.cs`: wykonanie akcji wydzielone do `internal static Res ApplyAction(Hero, akcja, arg, zrodlo, out why)` (Execute #move wola to samo; switch
  akcji bez zmian, zrodlo w linii remove "zmiana strony #mNN (remove)" jak dotad); `Res` internal. Wspolne kontrole: brak postaci / martwa / po stronie gracza -
  POMINIETA (unhide bez kontroli gracza); postac ukryta przez Armoury - kazda akcja poza hide/unhide CZEKA na powrot (#move: do 60 dob). Nowe akcje:
  `hide[:<term konca>]` - zakaz (POMINIETA): glowa rodu, wladca, wylaczony przez gre, lista chronionych przed jej wydarzeniem ROT (`Guarded` = kronika_check.PROTECTED,
  32 postacie, harness: 0 roznic); jeniec, gubernator, bitwa/oblezenie - CZEKA; potem LeaveParty (armia, zbieg, druzyna lorda StartDisband - ludzie nie przepadaja)
  i DisableHeroAction (dekompilacja 1.4.8: z osady, stan Disabled -> lista martwych i wylaczonych przez CampaignObjectManager.HeroStateChanged; heir: Clan.GetHeirApparents
  pomija IsDisabled); rejestr `_hid` (klucz postaci, zrodlo "#m11" / "Kronika 008.1", koniec, dzien) w zapisie arm_canon_companions (linie "hid", niezaleznie od klucza
  CanonCompanionsToClans); drugie hide na ukrytym = nowy koniec ("UKRYCIE: nowy koniec ..."). `unhide[:<osada>]` - tylko postac z rejestru (inaczej POMINIETA
  "nie ukryty", bez teleportu); cel: osada z akcji (nie oblezona, nie kryjowka, nie wroga), inaczej siedziba rodu (SeatOf), inaczej miasto kultury; TeleportHeroAction
  (Disabled -> Active - lista zywych); brak osady - CZEKA. Ratunek przy zniszczeniu rodu: ukryty dostaje tylko rod (bez teleportu, ktory by go przywrocil).
  API: GuardState, HiddenCount, HiddenRows, IsHidden, HiddenHero, DropHidden. KnownActions + hide, unhide. FindHeroId/FindOriginal juz szukaly w liscie wylaczonych.
  Term `kr:NNN.w[=x]` w BaseTrue -> `Chronicle.Resolved`; TermTrue: klucz ChronicleOfWesteros NIE = falsz "nigdy" takze z pamiecia +N (FORMAT 3.3), +N liczone od dnia
  rozstrzygniecia okna (Chronicle.ResolvedDay), nie od nastepnej doby. Linia zmiany strony z alternatywa w wyzwalaczu: "| wyzwalacz ... (zaszlo <term>) (dzien ...".
- `Armoury/ModuleData/arm_canon_moves.tsv` (TEN SAM commit c042d09d): m11, m20, m21 `remove` -> `hide` (p09 Benjen, prolog - dalej remove); komentarz akcji.
- `Chronicle.State.cs`: `Resolved(arg, out never)` i `ResolvedDay(arg)` (most dla CanonCompanions); `Chronicle.Terms.KrTrue` internal.
- `Chronicle.Actions.cs`: procedury `hide`/`unhide` przez ApplyAction (term konca sklejany z SplitArgs, kontrola skladni Terms.Syntax; linie "ukryty <postac> (id)
  (NNN.w) do <term> | ..." i "powrot <postac> (id) -> <osada> (NNN.w) | ..."); `HiddenDaily(day, termsOn)` w dobie Kroniki (przed kluczem NIE): postac zmarla /
  przywrocona przez gre - "UKRYCIE przerwane ... wpis usuniety"; ukryty wybrany przez gre na glowe rodu / wladce - powrot "KONIEC UKRYCIA (glowa rodu)"; klucz TAK:
  term konca prawdziwy albo "nigdy" (FORMAT 11) - powrot do siedziby "KONIEC UKRYCIA (term ...)"; klucz NIE - terminy czekaja (nic nie cofa)
  [ZMIENIONE w poprawkach K0 (R-1): koniec ukrycia liczony takze przy kluczu NIE - kr: = nigdy, wiec ukryci z koncem kr: wracaja]. Hidden/HiddenText
  z rejestru CanonCompanions ("n (Kronika a, #move b)") w liniach startu i doby.
- `Chronicle.cs`: doba wola Actions.HiddenDaily po Terms.Daily; linia "okno ... pokazane" z "| godzina kampanii H" (prog KW-4).
- `tools/sprawdz_logi.py`: grupa m105kronika (KW-0..KW-15 z planu H + KW-3b, KW-5b, KW-15b INFO), dispatch w main (osobny if/return), opis w pomocy; pary #para
  i okna z kotwica 'przed' czytane z danych repo, lista chronionych z tools/kronika/kronika_check.py (zapas - lista wbudowana), --crashscribe dla pary listy
  z 'AUTOTEST: OKNO wybor z listy'. sprawdz_narzedzie: czysto.
- Repo glowne (5b86d366): tools/autotest-2026-10-17/wgraj-v5.ps1 i bieg-bitwa.ps1 - takze Lektor/**/*.ogg (manifest arm_lektor_kronika.tsv szedl juz jako *.tsv);
  wgraj: kontrola OggS i md5 pliku z kolumny 5 manifestu (STOP przy zerach); oba: Armoury/GUI/Prefabs/Nameplate/*.xml, gdy sa w drzewie (galaz czcionek).
**Ryzyko / co sprawdzic:**
- W grze nic nie sprawdzone (zakaz gry w kroku). Pierwszy autotest K0a (hide/unhide na postaci testowej): linie "ukryty", "powrot", brak potkniec; bohater po hide
  w Hero.DeadOrDisabledHeroes, po unhide w osadzie (stan Active). Do sprawdzenia w grze: BK, ROTLords i DTE wobec bohatera Disabled (wyszukiwania po zywych go nie
  widza; dekompilacja tylko vanilla), CrashScribe UC (spis unikatow) - ukryta postac kanonu moze liczyc sie jako "usunieta".
- Jeniec i gubernator przy hide = CZEKA (FORMAT 11 mowi "zakaz"); po 30 dniach (Kronika) / 60 dobach (#move) - POMINIETA z powodem. Zamiana na natychmiastowe
  POMINIETE to jedna linia w CanonCompanions.Hide.
- m11/m20/m21 w kampanii Jeffa (prolog od 20.10) jeszcze nie wykonane - zmiana dziala od nastepnego WGRANIA; po wgraniu Sam/Sandor/Osha znikaja ukryciem, okna 082/089
  ich przywracaja (unhide z rejestru). Okna zalezne od zywych Sama/Sandora (064.2a, 082.2, 082.3, 089.1, 090.1, 101.1, 111.1a, 115.2d, 905.1) maja teraz szanse.
- Kolejnosc doby: CanonCompanions (ruchy #move, term kr:) przed Kronika - okno rozstrzygniete w AppTick dnia D, ruch z kr:NNN.w+1 rusza w dobie D+1 (pamiec od D).
- Nie zrobione w K0 (dalej "akcja jeszcze niewspierana"): gold, band, task, tourney, security, loyalty, pop, ware, garrison, muster, wound, skill, item, clan, court,
  lead, prisoner, free, wander, die, marry, wed, succeed, own, ruler, leader, alliance, truce; wyzwalacz brokenmen (045). ApplyAction gotowe dla clan/court/lead/
  prisoner/free/wander/die/marry/wed - podpiecie w Actions.Impl wymaga kontroli z FORMAT 8 (postac z wierszami #move - tylko para) i 11 (die glowy rodu w lekka -
  POMINIETA).
- Testy: harness A4 (scratchpad 7aee5f12/kr-a4/harness) 43/43 - parser (m11/m20/m21 = hide, p09 remove, 0 zlych wierszy), zapis hid tam i z powrotem, ApplyAction
  bez postaci, Resolved (okno, =x, inna opcja nigdy, pominiete nigdy, nieznane falsz), TermTrue +N od dnia okna, klucz NIE = nigdy (takze z pamiecia), Fires(m22)
  "zaszlo kr:008.1+1", 008.1 pominiete -> m22 nie odpala, ale nie "nigdy", hide/unhide - zle argumenty i klucz swiata; Guarded = PROTECTED (0 roznic). Stare
  harnessy: A1 42/42, A2 35/35, A3 44/44. Progi: test_kw.py 35/35 (log dobry bez NIE, kazdy KW-0..KW-15 z logiem zlym = NIE, log bez Kroniki i klucz NIE - INFO).
  wgraj-v5.ps1 -DryRun na drzewie kronika-k0: kod 0, 293 ogg + manifest "bylaby podmiana"; drzewo probne: ogg z zerami na koncu / w naglowku = STOP (kod 2),
  Nameplate/*.xml w tabeli; lista plikow bieg-bitwa.ps1 (fragment uruchomiony osobno): 293 ogg, manifest, 58 wav. kronika_check: WYNIK OK.
**Status:** DO SPRAWDZENIA (autotest K0a/K0b/K0c przed wgraniem; nic nie wgrane).

## 2026-10-21 - K0 krok A3: lektor okien Kroniki (klucz, zasada md5, .ogg) i obrazki z puli tematu

**Problem:** okna Kroniki (A2) pokazywaly sie bez glosu (Lektor szukal nagran tylko po tytule i tylko w .wav - 293 nagrania kr_*.ogg z manifestem
arm_lektor_kronika.tsv lezaly bez uzytku; warianty o tym samym tytule czytalyby zly tekst) i zawsze z pierwszym sprite'em tematu (Pictures.Pick = null).
**Przyczyna:** nowa funkcja (plan P8, P9; E: Lektor.cs _byKey + .ogg, Chronicle.Pictures.cs).
**Zmiana:**
- `Lektor.cs`: indeks PO KLUCZU (_byKey) przed indeksem po tytule - klucz z kolumny 1 arm_lektor.tsv (58 WAV okien ROT i prologu: po tytule dalej jak dotad)
  oraz z NOWEGO manifestu Lektor/EN/arm_lektor_kronika.tsv (klucz, tytul, plik, md5 tekstu, md5 pliku, paczka) - wiersze Kroniki tylko po kluczu. Klucz czytany
  z okna opowiesci Armoury (typ Armoury.ArmStoryEvent po nazwie: Key, VoiceMd5); okna ROT bez zmian (tytul). ZASADA md5: nagranie Kroniki gra tylko przy md5 tekstu
  z manifestu = VoiceMd5 okna; md5 nie-hex ("nieaktualne", 4 wiersze) = cisza z linia; okno Kroniki bez nagrania - cisza bez szukania po tytule.
  Pliki .ogg obok .wav (CheckAudio): CheckOgg - "OggS" + pakiet identyfikacji Vorbis (czestotliwosc) + bloki zer na koncu i w srodku przy wczytaniu, md5 pliku
  i dlugosc z granuli ostatniej strony przy odtworzeniu. Ta sama droga SoundEvent.CreateEventFromExternalFile("event:/Extra/voiceover") - w dekompilacji SandBox 1.4.8
  EavesdroppingMissionLogic gra tak pliki StoryMode VoicedLines/*.ogg (pelna sciezka). Linie: start/latka "| nagran Kroniki N (ogg n; ok, ze starym tekstem s,
  odrzucone r ...)" po czesci okien ROT (format "nagran okien N" bez zmian - LK-3); okno '... - start. Po kluczu kr_NNN_w (Kronika, md5 tekstu zgodny).';
  cisza '(klucz kr_..) - tekst okna inny niz nagrany (md5 okna x, nagrania y), cisza.'; doba: ", po kluczu n, Kronika z innym tekstem n" na koncu czesci okien.
  API: WindowsEnabled, KronikaText(), VoiceWhy(key, textMd5), CheckAudio/CheckOgg.
- `Chronicle.Voice.cs` (nowy): tekst nagrania DOKLADNIE jak tools/lektor/nagraj_kronika.py (tytul/gora/dol bez {=...}, tytul.rstrip('.!?') + "." + 

 + gora +
  

 + dol, "R'hllor" -> "the Red God"; '{' = zmienne = bez glosu), Md5, Why (zasada md5 przez Lektor.VoiceWhy), LineText (do linii okna).
- `StoryWindow.cs`: Spec.VoiceMd5, ArmStoryEvent.VoiceMd5 (publiczny - lektor czyta odbiciem).
- `Chronicle.Pictures.cs`: Pick - pula tematu (pliki Images/Kronika/kronika_<temat>_<n>, istniejace Images/Prolog/prolog_NN z kolumny prolog, sprite'y ROT bez
  poza rotacja i bez nieznanych w SpriteData), indeks = FNV-1a id okna mod n, poprzedni obrazek sesji -> nastepny, plik przez StoryPictures.Acquire (zly plik -
  nastepny element z powodem), temat z kolumna tylko (stannis_upadek = 081) poza swoim wydarzeniem - sprite zastepczy messenger-img, stannis-slain tylko w 081
  (twardo). Fallback (DefaultSprite), StartText (pliki w pulach), DayText, Reset (ResetCampaign), Restore (okno nie otworzylo sie - pamiec obrazka wraca).
- `Chronicle.cs`: bez ScanRecordings/_ogg (lektor z manifestu przez Voice); linia startu: "obrazki: plikow Kroniki ..., prolog_NN z tematow a/b" i "lektor: wierszy
  lektor=1 z nagraniem a/b (bez nagrania, tekst inny, stary tekst, plik odrzucony, zmienne; klucze lektora okien TAK/NIE) | nagran Kroniki ..."; linia
  "okno ... pokazane": "obrazek temat->nazwa (ROT|plik; pula n (...), indeks i -> nazwa)" i "lektor kr_NNN_w TAK | NIE (powod) | - (lektor=0)" (okno zapasu: NIE);
  doba: "| obrazki w sesji: wybrane n (z pliku n, pliki odrzucone n)".
**Ryzyko / co sprawdzic:**
- K0c (autotest, nie robiony - zakaz gry): czy silnik gra .ogg 24 kHz mono z naszej sciezki (glosy gry: .ogg 48 kHz mono) - linie "okno ROT ... -> kr_NNN_w.ogg
  (x s) - start." i "grane po 0,5 s: tak", koniec bez "ZA WCZESNIE"; "silnik nie przyjal" = 0. Jesli nie gra - nagraj_kronika.py moze dac .wav (manifest przyjmuje .wav).
- Skrypt wgrania (repo glowne tools/autotest-2026-10-17/wgraj-v5.ps1) kopiuje z Lektor tylko *.wav i *.tsv - bez zmiany (osobny commit, plan E) nagrania .ogg nie
  trafia do gry: linia startu lektora pokaze "nagran Kroniki 0 ... odrzucone 293: kr_001_1.ogg (brak pliku)", okna Kroniki beda bez glosu (bez bledu).
- Wczytanie manifestu przy Install (menu glowne): 293 pliki x (naglowek 64 B + 2 bloki 4 KB) - jednorazowo; md5 pliku (ok. 300 KB) dopiero przy odtworzeniu.
- 18 wierszy lektor=1 bez nagrania: 17 wersja=pelna (skrypt ich nie nagrywa) + 051.1a (brak w manifescie - nagrac). 4 "nieaktualne" (075.3, 079.1, 090.1, 090.1a).
- Obrazki z plikow: poza gra Acquire zwraca null (brak SpriteData) - w grze wejda prolog_01..03 (tematy dwor, polnoc, essos ...). Kronika/kronika_* dzis brak.
- Testy (scratchpad 7aee5f12/kr-a3): md5_test.py - prawdziwa funkcja windows() z nagraj_kronika.py: 289 md5 = manifest, 4 stare, 0 roznych; C# Chronicle.Voice.Md5
  vs python dla wszystkich 294 wierszy skryptu: 294 zgodne, 0 roznych; harness (prawdziwy Armoury.dll poza gra) 44/44 OK - manifest 293/ogg 293/stare 4/odrzucone 0,
  indeks okien ROT bez zmian (58), zasada md5 (zgodny / inny / nieaktualne / brak / prolog bez kontroli / zmienne), skladanie tekstu i R'hllor, CheckOgg (dobry, zera
  na koncu/naglowku/srodku, WAV jako .ogg, uciety - md5, za krotki, brak), manifest negatywny (2 wczytane, 3 odrzucone, 4 zle linie, dubel, linia z zerami), pule
  tematow, stannis_upadek tylko 081, sprite'y poza rotacja, staly skrot (FNV = python), bez powtorki z rzedu (346 okien; jedyna powtorka - pula 1 stannis_upadek),
  Fallback; harness A1 42/42 (zrzut modelu identyczny), A2 35/35. Gra NIE uruchamiana.
- Build Armoury 0 bledow, 7 ostrzezen (stare).
**Status:** DO SPRAWDZENIA (krok A3 z 4; bez wgrania).

## 2026-10-21 - K0 krok A2: zachowanie, okna, wybory, zapis

**Problem:** po A1 dane i termy byly w DLL, ale nic ich nie wolalo - Kronika nie miala doby, okien, list wyboru, skutkow ani zapisu.
**Przyczyna:** nowa funkcja (plan E: Chronicle.cs, Chronicle.Choice.cs, Chronicle.Actions.cs; FORMAT 3.2, 4, 11, 13).
**Zmiana:**
- `Armoury/src/Chronicle.cs` (nowy): `ChronicleBehavior` (OnNewGameCreated, OnSessionLaunched, DailyTick, BeforeHeroKilled, RaidCompleted, SyncData "arm_kronika"
  przez SaveText.Sync), rejestrowany w SubModuleMain PO CanonCompanionsBehavior. Silnik: `SessionStart` (Data.EnsureLoaded, paczki nowe w zapisie - "zapis sprzed
  paczki kNN" dla okien minionych przy wczytanym zapisie, kolejka okien, linia startu wg planu G), `Daily` (Terms.Daily, akcje czekajace, okna powracajace, plan
  okien, linia doby), `Plan`: po/warunki co dobe dla kazdego okna (pamiec +N), "nigdy" -> POMINIETE, kotwica przed / spoznienie > MaxLate po do -> wiesc (tekst _W)
  albo "nic" albo POMINIETE, nalezne -> PickRow (warianty w kolejnosci pliku z filtrem wersji, potem glowny; zmienne {NAZWA} z tokenow - brak wartosci = wiersz
  odpada) i forma (gracz -> forma, inaczej inni); okno do kolejki, wiesc/log/nic od razu. `AppTick` (SubModuleMain.OnApplicationTick, 4 razy na sekunde, osobny
  licznik bledow ladowania typu): Prologue.SceneGate + StoryWindow.IsOpen + StableSec 2 s, PerDay 2, GapHours 6 od okna Kroniki i od zejscia okna wydarzenia ze
  stosu stanow; ponowna kontrola warunkow przy pokazaniu; okno StoryWindow (Key = kr_NNN_w[war], tytul/gora/dol ze zmiennymi, obrazek Pictures.Pick -> null =
  pierwszy sprite ROT tematu); rozstrzygniecie przy pokazaniu (State.Resolve, skutki okna, wybor); 3 nieudane otwarcia = POMINIETE. Linie: "okno ... pokazane",
  "zamkniete po s (jak)", "wiesc/log/nic NNN.w (powod)", "POMINIETE okno NNN.w - powod", doba, start, "zapis arm_kronika n KB" przy kazdym zapisie gry.
- `Armoury/src/Chronicle.Choice.cs` (nowy): lista MultiSelection po zamknieciu okna (radio, bez wyjscia, opis z id okna, "Confirm"), ChoiceDelay 0,5 s (po
  wczytaniu StableSec), stan 'a' w zapisie ("ka") - lista wraca po wczytaniu; opcja bierna bez pytania przy formie innej niz okno, graczu poza kolumna gracz,
  kluczu Chronicle Player Choices NIE albo 0 dostepnych; plik CrashScribe/armoury-kronika-wybor.flag (a-d, LogGate 1/2) wymusza dostepna opcje; lista zniknieta bez
  wyboru wraca. Linia "wybor NNN.w = x 'etykieta' (z n; dostepne ..; wymuszone flaga TAK/NIE; bierna TAK/NIE)".
- `Armoury/src/Chronicle.Actions.cs` (nowy): parser ' ; ' + Terms.SplitArgs, rejestr 39 akcji FORMAT 11 z klasa T/R/S (klucz World Changes NIE pomija S z linia),
  wynik Done/Skip/Wait (CZEKA ponawiane co dobe do WaitActionDays 30, potem POMINIETA; zapis "kw"). Zaimplementowane: wiesc, plotka, flag, kod (tylko test; inny -
  POMINIETA "brak uchwytu"), rel, relp, relk, renown, influence. Reszta (gold, band, hide, unhide, security, loyalty, ...) - "POMINIETA (akcja jeszcze niewspierana)".
- `Armoury/src/Chronicle.Recurring.cs` (nowy): okna powracajace - kolejka instancji NNN.w#klucz ("kq"), max/odstep (licznik "kl"), prolog, wydanie najwyzej 1
  instancji widocznej na dobe, MaxLate, rejestr kluczy 400 (State.PruneInstances). Wyzwalacze: season, siegehunger, ironraid, oldage, others, captured, besieged,
  wizja; brokenmen niewspierany (okno czeka; linia startu go wymienia).
- `Armoury/src/Chronicle.Pictures.cs` (nowy): interfejs Pick (A3 wypelni; teraz null).
- `Chronicle.State.cs`: FormCounts, PruneInstances. `Prologue.Scenes.cs`: SceneGate private -> internal (logika bez zmian). `StoryWindow.cs`: ArmStoryEvent.Key.
- `Rumours.cs`: AddStoryNews(textId, osada|krolestwo, dni, noLive) - osobna lista plotek Kroniki (wlasny czas zycia, bez wypierania przez wiesci wojenne), 4. czesc
  zapisu arm_rumours (stary DLL czyta 3), sluchanie w Wars (krolestwo - karczmy jego ziem, osada - do 450 jedn. z opoznieniem krukow). `Lektor.Live.cs`:
  MarkNoLive - plotka Kroniki nie idzie do API na zywo (nagranie z pamieci gra), licznik w linii strony i doby.
- `SubModuleMain.cs`: AddBehavior(ChronicleBehavior), Chronicle.AppTick obok scen prologu, OnGameEnd -> Chronicle.GameEnd.
**Ryzyko / co sprawdzic:**
- Kampania Jeffa (prolog, n32): Kronika ruszy dopiero w dniu opowiesci >= od okien (K1 od dnia 10 x k); w prologu tylko okna powracajace z opcja prolog (015)
  i pomijanie "nigdy". Zapis Jeffa bez arm_kronika -> paczki nowe w zapisie: w prologu nic nie jest "minione" (dzien opowiesci ujemny) - zero pominiec.
- "nigdy" jest trwale: mv: przy kampanii bez postaci kanonu albo przy wyzerowanym arm_canon_moves.tsv (CanonCompanions bez wierszy) zwraca nigdy -> okna z mv:
  POMINIETE na stale (logika A1). Do obserwacji w pierwszym biegu: liczba POMINIETE "warunki swiata: mv:" w dobie 1 po starcie opowiesci.
- Skutki okna ida przy POKAZANIU (rozstrzygnieciu), nie przy zamknieciu - relp pokazuje powiadomienie gry nad otwartym oknem.
- Rumours: Wars bierze 2 najwyzej punktowane wiesci strony - plotki Kroniki (2.6+) wypieraja wiesci wojenne z tej kategorii, dopoki zyja (30-90 dni).
- Okna powracajace: oldage tylko glowy rodow z tierem >= 4 albo rodu wladcy (zgadywane "wazny rod"); ironraid = sturgia + warownia wsi z portem (zgadywane
  "wybrzeze"); odstep = dni od ostatniej instancji OKNA (nie klucza); limit 1 na dobe liczy tylko formy widoczne (okno/wiesc) - log/nic bez limitu.
- Brak w A2 (A3/A4): akcje swiata, obrazki z puli, lektor po kluczu i .ogg, hide/unhide w CanonCompanions + m11/m20/m21, term kr: w arm_canon_moves, progi
  m105kronika w sprawdz_logi.py, autotest K0a/K0b w grze.
- Testy: harness scratchpad 7aee5f12/kr-a2/harness (prawdziwy Armoury.dll poza gra): 35/35 OK - akcje (OK/POMINIETA, klucz World NIE, rejestr 39 = akcje danych),
  wybory (bierna bez pytania, stan 'a', bez dubla, forma wiesc, klucz Choices NIE), zapis Build -> Import -> Build tozsamy, stary zapis i zapis z bledami bez wyjatku,
  CheckPacks (11 paczek, dzien 0 bez pominiec), Plan i linia doby bez wyjatku, PickRow, tokeny, PruneInstances, Rumours 4. czesc zapisu tam i z powrotem, MarkNoLive.
  kr-a1/kronika_sim.py: akcje danych - wszystkie w Actions.KnownActions (w C#, a nie w danych: lead, marry, ruler, wander, ware). Gra NIE uruchamiana (zakaz kroku).
- Build Armoury 0 bledow (7 starych ostrzezen, zadne z plikow tej zmiany).
**Status:** DO SPRAWDZENIA (krok A2 z 4; bez wgrania).

## 2026-10-21 - K0 krok A1: dane Kroniki i warunki (termy)

**Problem:** 117 wydarzen Kroniki (212 okien z k00) lezy w Armoury/ModuleData/arm_kronika_kNN.tsv, ale gra ich nie czyta - silnika K0 nie ma.
Krok A1 = fundament: wczytanie plikow do modelu i liczenie warunkow (FORMAT rozdz. 2-10) - bez okien, bez doby, bez akcji (A2-A4).
**Przyczyna:** nowa funkcja (plan E: Chronicle.Data.cs, Chronicle.Terms.cs).
**Zmiana:**
- `Armoury/src/Chronicle.Data.cs` (nowy): `internal static partial class Chronicle` - Tag "Kronika Westeros (KW): ", klucze MCM (KeyOn/KeyWorld/KeyFull/KeyChoices),
  potkniecia (Stumble: Log.Error tylko 1-5), TestWanted (LogGate.Mode 1/2 + CrashScribe/armoury-kronika-test.flag); `Chronicle.Data`: EnsureLoaded() (pierwszy raz albo
  po zmianie rozmiaru/daty/zestawu plikow/flagi testu), paczki kNN (k00 tylko przy tescie), sekcje #okno #wybor #tekst #banda #zadanie #para #usuniete do klas modelu
  (WindowDef z wierszem glownym i wariantami, RowDef, OptionDef, BandDef, TaskDef, PairDef, RecurDef, VarDef), tematy z arm_kronika_tematy.tsv (TopicDef).
  Zly plik (pusty, bajt 0, nie UTF-8, brak naglowkow, nieznana sekcja, inne kolumny, dane przed naglowkiem) - linia "ZLY PLIK ... - paczka wylaczona", reszta paczek dziala.
  Zly wiersz - "ZLY WIERSZ plik:wiersz (powod)" (pierwsze 10, potem licznik w Summary()). Nieznany term w warunkach - wiersz ZOSTAJE (term = falsz), linia "NIEZNANY TERM".
  Teksty: surowe komorki {=ARM_KR_...}, TextObject swiezy przy kazdym odczycie (Tx). Dziedziczenie tekstow i tematu wariantu z wiersza glownego.
- `Armoury/src/Chronicle.Terms.cs` (nowy): wszystkie termy FORMAT 10 (ev, done, dead, alive, clanof, head, leads, prisoner[:x], war, peace, kexists, own, rotday,
  prolday, kr[=x], flag, mv[=done|skip], cw:reach|dorne, season, plan:X-N, p:region/in/near/vassal/army/rel), skladnia ' & ' / ' | ' (lub wiaze mocniej) / '!' / '+N'
  z pamiecia pierwszego spelnienia (zapis), wynik trojwartosciowy (prawda / falsz / NIGDY), nieznany term = falsz z linia raz na term. Wyszukiwanie bohater/rod/
  krolestwo/osada z tokenami (FindHero/FindClan/FindKingdom/FindSettlement, SplitArgs - wspolne z akcjami A3), indeksy odswiezane co godzine gry.
- `Armoury/src/Chronicle.State.cs` (nowy): stan okien czytany przez kr:/flag: (rozstrzygniete z forma i wierszem, pominiete z powodem, wybory, flagi, instancje
  okien powracajacych NNN.w#klucz) z Export/Import linii kr/ks/kc/kf (zapis podlaczy A2).
- `CanonCompanions.cs`: BaseTrue private -> internal (logika bez zmian; TermBase w kodzie nie istnieje); nowe internal MoveState(id) ("done"/"skip"/"wait"/null),
  MoveDay(id), FirstDay(baza), ClearEventCache() - tylko odczyt stanu.
- `Prologue.Crown.cs`: internal CrownGuardHolds(which, out never) - odczyt _rcHeld / _cdOurs (never: straznik zakonczony albo poza zakresem); logika straznika bez zmian.
- `Settings.cs` grupa "Chronicle of Westeros": ChronicleOfWesteros (TAK), ChronicleWorldChanges (TAK), ChronicleFullVersions (NIE), ChroniclePlayerChoices (TAK);
  gen_mcm Armoury 2279.
**Ryzyko / co sprawdzic:**
- Nic jeszcze nie wola Chronicle.* (ChronicleBehavior, OnSessionLaunched -> Data.EnsureLoaded, doba -> Terms.Daily, SyncData -> State/Terms Export/Import - krok A2):
  DLL zachowuje sie jak przed zmiana, poza 4 nowymi kluczami w MCM (bez dzialania do A2-A4). Zmiany w CanonCompanions i Prologue.Crown to nowe metody odczytu - stare
  sciezki bez zmian (BaseTrue tylko widocznosc).
- ClearEventCache czysci pamiec stanu wydarzen ROT CanonCompanions (_evDay) - ta pamiec sluzy tylko unikaniu powtornego odbicia; Moves() czysci ja i tak na poczatku przebiegu.
- kr: przy wylaczonej Kronice = NIGDY (FORMAT 3.3) - przy wylaczeniu klucza koniec ukrycia `kr:` w A3 skonczy ukrycie (zgodnie z FORMAT 11).
- `+N` liczy od pierwszego dnia prawdy: dla ev:/done: wszystkich 29 wydarzen zapis co dobe (Terms.Daily), dla kr: dzien rozstrzygniecia z State, dla mv: dzien z
  CanonCompanions; pozostale (dead, war, own ...) od pierwszego liczenia - A2 powinien liczyc warunek `po` codziennie (nie dopiero od `od`).
- plan:X-N bez CrashScribe (albo gdy AccessTools.TypeByName zawiedzie) = prawda (decyduje od) - linia startu A2 powinna mowic "plan Fabuli TAK/NIE" (Terms.PlanAvailable).
- Testy: python -I scratchpad 7aee5f12/kr-a1/kronika_sim.py (symulacja parsera na k00-k10: 0 zlych plikow/wierszy/termow, termy danych w C# Kinds - roznica tylko prolday
  nieuzyty, 34 akcje danych do obslugi w A3); harness 7aee5f12/kr-a1/harness (prawdziwy Armoury.dll poza gra, odbiciem): Summary jak wyzej, zrzut modelu C# == zrzut symulacji
  (485 linii, identyczne), 42 testy logiki termow OK (kr/flag/mv/plan/tokeny/nigdy/negacja/+N/zapis tam i z powrotem), pliki negatywne (bajt 0, pusty, nie UTF-8, nieznana
  sekcja, inne kolumny, dane przed naglowkiem, same komentarze) = ZLY PLIK, 18 zlych wierszy k02 zgodnie z oczekiwaniem. Gra NIE uruchamiana (zakaz kroku).
- Build Armoury 0 bledow (pelny rebuild: 7 ostrzezen, wszystkie stare, zadne z Chronicle*).
**Status:** DO SPRAWDZENIA (krok A1 z 4; bez wgrania).
