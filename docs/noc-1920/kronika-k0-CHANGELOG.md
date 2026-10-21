# CHANGELOG galezi w-toku/kronika-00-silnik (Kronika Westeros - silnik K0)

Baza: n32-calosc 77c17f3c (kandydat WGRANIA 102) + scalone kronika-dane (6441d096) + nagrania lektora (fcf2b976, 9e02b0f2).
Plan: repo glowne docs/PLANY-E5-WOJNY-KRONIKA-2026-10-21.md "## PLAN kronika" (E, F, G); kontrakt danych: docs/KRONIKA-FORMAT.md (wygrywa przy sprzecznosci).
Silnik powstaje w 4 krokach lancucha: A1 dane i termy, A2 zachowanie/doba/okna, A3 wybory i akcje, A4 obrazki/lektor/progi/autotest.

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
