# CHANGELOG galezi w-toku/kronika-00-silnik (Kronika Westeros - silnik K0)

Baza: n32-calosc 77c17f3c (kandydat WGRANIA 102) + scalone kronika-dane (6441d096) + nagrania lektora (fcf2b976, 9e02b0f2).
Plan: repo glowne docs/PLANY-E5-WOJNY-KRONIKA-2026-10-21.md "## PLAN kronika" (E, F, G); kontrakt danych: docs/KRONIKA-FORMAT.md (wygrywa przy sprzecznosci).
Silnik powstaje w 4 krokach lancucha: A1 dane i termy, A2 zachowanie/doba/okna/wybory/zapis (+ akcje T/R), A3 akcje swiata i obrazki, A4 lektor/progi/autotest.

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
