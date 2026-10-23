# m109-muzyka (4b) - nasze 6 utworow w bitwie zamiast muzyki gry

Galaz `w-toku/m109-muzyka` od 44c03a05 (WGRANIE 103.5). Commity: 8f9164f0 (pliki), 7ffc089b (kod, klucze), fc6cacb1 (grupa m109muz), ten wpis.
Gry nie uruchamialem; nic nie wgrane.

## Problem

Jeff 22.10 ok. 21:40: "muzyka w czasie bitwy jest strasznie slaba, zastap ja naszymi 6 utworami, ktore leca losowo". Wczesniej (pkt 1b przekazania 22.10):
"w bitwie muzyka chwile gra i nagle cichnie" - naprawione w 103.5 (EventMusic bez SetPaused psai, tylko glosnosc 0). W logu Jeffa 22.10 22:28-23:59
(Armoury-2026-10-22_22-28-22.log) linie "Muzyka (m108)" sa tylko przy oknach i plotkach (8 okien, wszystkie zamkniete) - w bitwie gra muzyka gry (psai), nasza nie.

## Przyczyna

Nie blad - nowa funkcja. Muzyke bitwy wlacza gra: widok `MusicBattleMissionView` (Native) / `MusicNavalBattleMissionView` (NavalDLC) w misji ->
`MBMusicManager.ActivateBattleMode` -> psai gra segmenty bitewne. Nasze utwory wydarzen sa -28 LUFS z EQ pod glos lektora (2.5 kHz -3 dB) - do bitwy za ciche.

## Zmiana

1. **Pliki (krok 1, 8f9164f0).** Oryginaly ElevenLabs (mp3, md5 jak w rozpoznaniu: 398605ac, 79d3036c, 168704eb, a7daf225, e4acd715, 5c37a980) w
   `tools/muzyka/zrodla/` (kopia tez na D:/Backup-Bannerlord/lektor/muzyka-mp3/). `tools/muzyka/bitwa_glosnosc.py`: dla poziomow -17/-20/-23/-26 LUFS samo
   wzmocnienie `volume=XdB` (cel - I zrodla), bez EQ, kompresji i limitera; libvorbis -q:a 4 44.1 kHz stereo (jak pliki wydarzen); pomiar gotowego ogg
   (ebur128 peak=true): I = cel +-0.5 LU, TP <= -1.0 dBTP, inaczej mniejsze wzmocnienie (najwyzej 3 proby) albo kod != 0. Wynik: 24 pliki
   `Armoury/ModuleData/Lektor/Muzyka/Bitwa/lufs{17,20,23,26}/muz_{zaloba,wojna,intryga,polnoc,korona,plotka}.ogg` (27 MB), raport `tools/muzyka/bitwa_pomiar.tsv`.
   Generator: kod 0, wszystkie 24 w warunkach za pierwsza proba. Przy -17 LUFS: TP od -2.2 (korona) do -5.9 dBTP (plotka), wzmocnienie plotki +24.4 dB
   (oryginal -41.4 LUFS). Dlugosci 90.0-90.1 s.
2. **Kod (krok 2, 7ffc089b), Armoury.** Bez latek Harmony.
   - `EventMusic.cs` -> `partial`; `AppTick`: `BattleStartCheck()` (linia startu raz na kampanie), `battle = !win && !gossip && BattleWanted()`, w catch przy bitwie
     `BattleStumble` bez `RestoreNow`; `Step`: galaz bitwy `BattleStep + FadeDown`, okno w czasie bitwy konczy bitwe ("okno wydarzenia", wyciszenie zostaje),
     zwolnienie po 0,6 s -> `BattleEnd(powod)` zamiast linii "koniec wydarzenia"; `FadeUp` / `RestoreNow` - linia powrotu muzyki gry po bitwie, `RestoreNow`
     konczy bitwe i zeruje jej stan. `StartTrack`, `LoopTrack`, `StopTrack`, `DuckStart`, `FadeDown`, `PsaiResolve` bez zmian. Komentarz klasy poprawiony (bez SetPaused).
   - `EventMusic.Bitwa.cs` (nowy): bitwa = misja z widokiem `MusicBattleMissionView` / `MusicNavalBattleMissionView` w `Mission.MissionBehaviors` (nazwa typu,
     takze bazowe), sprawdzane raz na misje w pierwszej klatce `Continuing`; gra, poki ta sama misja jest w `Continuing` / `EndingNextFrame` i trwa kampania.
     psai na 0 tym samym stanem co okna (jeden wlasciciel glosnosci, bez SetPaused). Utwor losowy z zestawu glosnosci, inny niz poprzedni (takze z poprzedniej
     bitwy), `SoundEvent.CreateEventFromExternalFile("event:/Extra/external", ...)` jak okna; po koncu 3 s przerwy i nastepny. Koniec utworu: silnik
     (`!IsValid` / `IsStopped`) albo czas pliku + 5 s (dlugosc z naglowka Ogg; bez dlugosci 150 s), czas liczony bez pauzy gry. Po misji (takze wyjscie do menu,
     wczytanie, wywrotka misji) muzyka gry wraca w 2 s, przy `RestoreNow` od razu. Bledy liczone na misje: po 3 rezygnacja tylko z tej misji (muzyka gry wraca),
     nastepna od nowa; w logu pierwsze 3 i co 50. Pliki: 4 zestawy raz na sesje, pliki > 4 KB z naglowkiem OggS (inne - "odrzucone").
   - Klucze (grupa Narrator, zaraz po `EventMusicEnabled`; czytane na zywo; MCM Jeffa ich nie ma - dzialaja domyslne): `BattleMusicOurs` (TAK) - "Battle Music
     Ours"; `BattleMusicLoudness` (1-4, 4) - "Battle Music Loudness" (4 = -17 LUFS, kazdy krok w dol 3 dB, 1 = -26 LUFS; nowe ustawienie od nastepnego utworu).
     Klucz NIE = bitwy z muzyka gry jak w 103.5 (sciezka okien bez zmian; jedyna roznica - linia startu "klucz NIE" raz na kampanie). gen_mcm: RANGES (1, 4).
   - Linie "Muzyka bitwy (m109): ..." (Log.Info): start (klucz, glosnosc, 4 zestawy, dlugosci, odrzucone, psai, test), bitwa (misja, typ mapy, scena, oboz, widok,
     glosnosc), utwor N po 1 s (TAK/NIE, poprzedni i jak sie skonczyl; pierwsze 8 na bitwe, pod flaga wszystkie), nie grany, silnik nie przyjal (pierwsze 3),
     koniec bitwy (powod, czas, liczniki), muzyka gry wrocila (glosnosc teraz / zapamietana), misja bez zmian (pierwsze 20, potem co 50.), rezygnacja, brak psai /
     utworow (raz). Dalej linia m108 "muzyka gry wyciszona (bez pauzy psai; ... teraz 0.00 ...)" - teraz w kazdej bitwie.
   - Autotest: plik `Documents/Mount and Blade II Bannerlord/CrashScribe/armoury-muzyka-bitwy.flag` przy trybie CrashScribe 1/2 (wzor MapChatLog.TestWanted):
     utwor konczony po 4 s realnych, przerwa 1 s, linia kazdego utworu. W zwyklej grze nic nie robi.
   - Build Armoury: kod 0, 0 bledow, bez nowych ostrzezen.
3. **Progi (krok 3, fc6cacb1).** Grupa `m109muz` w `tools/sprawdz_logi.py` (opis w `--help` po m103lektor, dispatch osobnym `if` po m103lektor, funkcje po
   `m103lektor_main`): MUZ-S self-test wyrazen na liniach kontrolnych jak w C#, MUZ-S2 test syntetyczny (dobry, zly, bez linii m109, klucz NIE, log uciety w bitwie),
   MUZ-0 start, MUZ-1 wyciszenie kazdej bitwy (+ z `--crashscribe` liczba bitew = linie "AUTOTEST BITWA n/N [..]: misja ruszyla"), MUZ-2 utwory po kolei,
   MUZ-3 koniec bitwy i powrot muzyki gry, MUZ-4 misje bez zmian, MUZ-5 bledy, MUZ-6 regresja okien m108. `sprawdz_narzedzie.py`: OK. Na logu Jeffa 22.10 (sprzed
   m109): MUZ-S/S2 TAK, MUZ-5 TAK, MUZ-6 TAK (8 okien zamknietych, SetPaused 0), reszta INFO.

**Odstepstwa od projektu:**
- Linia startu raz na kampanie przez slaby odnosnik do obiektu `Campaign` (`BattleStartCheck`) zamiast flagi `_batStartLogged` zerowanej w `RestoreNow` - flaga
  zerowana w `OnGameEnd` moglaby wypisac linie jeszcze dla konczacej sie kampanii i pominac ja dla nastepnej (nie wiadomo, kiedy `Campaign.Current` staje sie null).
  `RestoreNow` zeruje tylko stan bitwy.
- Linia startu ma dodatkowe pole "odrzucone N" (pliki bez naglowka OggS - dysk C zeruje pliki w miejscu; taki plik silnik i tak by odrzucil).
- "Nie grany" (koniec przed kontrola 1 s) tylko, gdy od startu minelo < 10 s realnych - dluga pauza gry (Esc) przed kontrola nie liczy sie jako blad misji.
- Linia bitwy ma pole "widok <typ>" (ktory widok muzyki rozpoznal bitwe); linia konca - licznik "grane po 1 s NIE".
- Nastepna misja bitwy bez przerwy (bez `Mission.Current == null` pomiedzy) - koniec poprzedniej i start nowej linia (w projekcie nie bylo). Wtedy miedzy bitwami nie ma linii "wrocila" (wyciszenie trwa) i MUZ-3 da NIE - w grze nie widzialem takiego przejscia (miedzy misjami jest mapa).
- MUZ-1 z `--crashscribe` liczy linie "AUTOTEST BITWA n/N [..]: misja ruszyla" (w logach autotestu nie ma napisu "AUTOTEST BITWA START").
- MUZ-6 "zadnej linii z SetPaused" - poza linia `psai TAK (GetVolume/SetVolume/SetPaused)` (PsaiResolve bez zmian, ta linia jest w kazdym logu 103.5).
- wgraj-v5.ps1 nie ma w repo (jest w scratchpadzie 6ba92574.../test) - nic nie poprawialem; petle kopiujace obejmuja pliki (patrz nizej).

## Ryzyko - co sprawdzic (kontrola calosci, pkt 8.0)

- **Kto pisze glosnosc psai:** w DLL gry i modow brak SetVolume / GetVolume / SetPaused psai (dekompilacja w rozpoznaniu); pisze tylko EventMusic, bitwa uzywa
  tego samego stanu (`_ducked`, `_saved`, `_cur`, `_active`, `_mus`) - okno i bitwa nie przywracaja sie nawzajem. Okno w czasie bitwy: bitwa konczy sie linia
  "okno wydarzenia", wyciszenie zostaje, po oknie powrot (linia "wrocila" po oknie). `event:/Extra/external` - jedno gniazdo `_mus`, dwa nasze utwory naraz nie zagraja.
- **Klucz NIE:** `BattleWanted` konczy sie na pierwszym warunku; okna jak w 44c03a05 (linie m108 te same; tylko kolejnosc `_paused = false` i linii "koniec
  wydarzenia" w galezi zwolnienia - bez skutku).
- **Wydajnosc:** co klatke na mapie - odczyt Settings, `Campaign.Current`, `Mission.Current`, slaby odnosnik; w misji - stan misji; skan zachowan misji raz na
  misje; w bitwie - jak okno (SetVolume 0 odbiciem) + IsValid / IsStopped / IsPaused zdarzenia; pliki czytane raz na sesje (24 x 4 KB naglowka + 64 KB konca).
- **Co celowo ucichnie:** motyw zwyciestwa / porazki gry w bitwie i muzyka zdarzen ROT w misji (psai na 0). Lektor (voiceover) w misji nie gra.
  ROTBattleAtmosphere - wlasny bank, bez kolizji.
- **Misje:** nasza muzyka - Battle (pole, bandyci, karawany, oboz, napad na wies / obrona), SiegeMissionWithDeployment / NoDeployment, NavalBattle, NavalRaid.
  Bez zmian - turniej, arena, kryjowka (cisza / skradanie gry), miasto, wies, zaulki, rozmowy, spacer po obozie ("Camp" bez widoku muzyki), bitwa wlasna (bez
  kampanii). Nieznane misje z widokiem bitewnym (np. fabularne NavalDLC) - nasza muzyka; w logu nazwa misji (MUZ-1 INFO).
- **m109-oboz:** bitwa w obozie to dalej misja "Battle" przez `OpenBattleMission` - przy scalaniu sprawdzic, czy oboz nie otwiera innej misji z widokiem bitewnym.
  **m109-crash** i inne: Settings.cs / McmSettings.cs - przy scalaniu McmSettings wygenerowac na nowo (`python -I tools/gen_mcm.py`); klucze obok `EventMusicEnabled`.
- **Zapis gry:** nic nowego; stare zapisy sie wczytaja.
- **Wgranie plikow:** petle w `bieg-bitwa.ps1` (6ba92574.../test, linie 64-69) i `wgraj-v5.ps1` (linie 81-104) biora `Lektor -Recurse` *.ogg ze sciezka wzgledna,
  wiec obejma `Muzyka/Bitwa/lufsNN/` (5 plikow `muz_wojna.ogg` w roznych katalogach sie nie zderzy); `EventMusic.LoadTracks` bierze `muz_*.ogg` tylko z katalogu
  `Muzyka`, wiec podkatalog `Bitwa` nie zmienia muzyki wydarzen. Bez plikow w grze: linia "brak utworow bitewnych" i muzyka gry (nie cisza).
- **Do odsluchu u Jeffa:** plotka (+24.4 dB z bardzo cichego oryginalu, utwory robione jako tlo pod lektora); -17 LUFS to nadal 3-4 dB ciszej niz segmenty bitewne
  ROT (-12.9..-14.5) - glosniej sie nie da bez limitera przy TP <= -1 dBTP (korona -2.2 dBTP przy -17); 3 s ciszy miedzy utworami.
- **Nie sprawdzi autotest (gra Jeffa):** H1 naturalny koniec utworu 90 s (linia "poprzedni ... skonczony po 90.x s (silnik)"), H2 czy muzyke slychac w misji,
  H3 ktory suwak gry dziala na `Extra/external`.

## Status

**DO SPRAWDZENIA** - autotest nie uruchomiony (Jeff gra), nic nie wgrane.

## Plan autotestu (2 biegi, testy-1033.ps1 z 7aee5f12)

1. **bitwa:** `-Runs bitwa -ExtraFlags armoury-muzyka-bitwy.flag` (4 bitwy w polu z zapisu autotest-F-d240, `-BattleSec 90`; bitwy 103d trwaly realnie 9-23 s,
   pod flaga 2-5 utworow na bitwe). Progi: kod wyjscia 0 + `python -I tools/sprawdz_logi.py --grupa m109muz <Armoury-*.log> --crashscribe <session-*.log>`
   (MUZ-0..MUZ-6 TAK; MUZ-4 INFO, gdy arena nie ruszy) + grupy zwykle liczone na biegu bitwa. W logu: "bitwa - misja Battle ... test autotestu TAK", m108
   "muzyka gry wyciszona ... teraz 0.00", "utwor 1..N ... grany po 1 s TAK; poprzedni ... (test 4 s)", "koniec bitwy (koniec misji ...)", "muzyka gry wrocila".
2. **p20:** prolog 20 dob z flaga dziennika - regresja wspolnego `Step` w oknach wydarzen. Progi: m105dziennik, m103lektor, m103sceny, m101prolog + m109muz
   (MUZ-0 TAK, MUZ-6 TAK; MUZ-1..4 INFO bez bitew).

Biegu "wczyt" nie trzeba: brak stanu w zapisie i nowych typow zapisu.
