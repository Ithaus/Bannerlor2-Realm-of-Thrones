## m103-lektor - lektor: okna wydarzen ROT czytane glosem Davida, plotki w karczmie czytane na zywo (Armoury)

**Dla Jeffa (prosto):** kiedy wyskakuje okno fabuly Realm of Thrones (egzekucja Neda, Krwawe Gody i reszta - 35 okien), lektor (glos David)
czyta je na glos z nagran, ktore ida razem z modem - bez internetu. Zamkniesz okno - glos milknie; nastepne okno zaczyna od nowa. Plotki w karczmie
i w zajezdzie czyta ten sam glos: plotke, ktora juz kiedys nagral, puszcza od razu z dysku; nowa plotke nagrywa w tle przez ElevenLabs (1-3 s opoznienia)
i zapisuje na dysku - ten sam tekst nigdy drugi raz nie idzie do ElevenLabs. Jest limit dzienny (1000 liter) i rezerwa planu (3000 liter zostaje zawsze
na koncie); po limicie raz na sesje pokaze sie "The narrator has read enough gossip for today." i lektor czyta juz tylko to, co ma na dysku.
Cene kufla i liczby w nawiasach ("(12 d)", "(3 more)") widac na ekranie, lektor ich nie czyta - dzieki temu wstep przy kuflu nagrywa sie raz.

**Problem / decyzje Jeffa (21.10):** lektor tylko po angielsku, glos David (ElevenLabs voice_id BNgbHR0DNeZixGQVzloa), model eleven_multilingual_v2,
stability 0.5, similarity_boost 0.78, style 0.15, use_speaker_boost true, speed 0.95, pcm_24000. Plotki czytane na zywo: "zostaje przy Starterze, ale
zapisuj kazda nagrana plotke, aby potem, jak jest cos nagrane, David nie czytal". Plan Starter ok. 30 tys. znakow/miesiac (zostalo ok. 21 tys., ok. 11 tys.
na 23 okna prologu) - potrzebny limit dzienny i rezerwa. Klucz API tylko w %USERPROFILE%/.elevenlabs/key.txt - nigdzie nie wypisywany ani kopiowany.

---

### K1 - dane (commit 07550d6f)

- `Armoury/ModuleData/Lektor/EN/`: 35 plikow `<KlasaROT>_<n>.wav` (PCM 24 kHz mono 16 bit, naglowek 44 B, razem 38 MB, 827 s) skopiowane z
  `.../7aee5f12-.../scratchpad/lektor/david-v2-rot` (md5 35/35 zgodne) + `arm_lektor.tsv` (klucz, tytul okna = EventPanel.EventTitle, plik) z NOWA
  4. kolumna md5 (kontrola zer z dysku C przy odtworzeniu i przy wgraniu). Brak kolumny (np. przyszle wiersze z `tools/lektor/nagraj_david.py` w repo glownym,
  ktory pisze 3 kolumny) = kontrola naglowka, rozmiaru i blokow zer.
- Pomiar 35 nagran: najdluzsza cisza cyfrowa (same zera) 528 probek = 1056 B; ostatnie 4096 B nigdy nie sa same zera - blok kontrolny 4096 B nie da
  falszywego "uszkodzony".

### K4 - klucze MCM, grupa "Narrator" (commit 31cdb30b)

| Klucz (Armoury.json) | Nazwa w MCM | Domyslnie | Co robi |
|---|---|---|---|
| LektorEnabled | Narrator Enabled | TAK | lektor w ogole (okna i plotki); NIE = jak przed paczka |
| LektorEventWindows | Narrator Event Windows | TAK | okna ROT z nagran |
| LektorGossipLive | Narrator Gossip Live | TAK | nowe plotki przez ElevenLabs; NIE = tylko pamiec na dysku |
| LektorGossipDailyChars | Narrator Gossip Daily Chars | 1000 (0..10000) | litery nowych plotek na dobe kalendarzowa komputera |
| LektorGossipReserveChars | Narrator Gossip Reserve Chars | 3000 (0..30000) | litery zawsze nietkniete na koncie ElevenLabs |

- `tools/gen_mcm.py`: slownik LABELS - nazwa pola w MCM po angielsku dla kluczy Lektor* (CLAUDE.md: napisy w grze po angielsku), id pola zostaje nazwa
  klucza; reszta 2243 pol bez zmian (diff McmSettings.cs: tylko 5 nowych pol i 5 linii ApplyTo). Zakresy w RANGES = przyciecie w kodzie.
- Glosnosci osobnego klucza NIE ma: dzwiek idzie przez silnik gry (zdarzenie event:/Extra/voiceover), glosnosc ustawiaja suwaki gry.

### K1b + K2 - okna wydarzen ROT (commit 59fdba42, `Armoury/src/Lektor.cs`)

- Wczytanie `arm_lektor.tsv` raz na proces (`Lektor.Install` w `OnBeforeInitialModuleScreenSetAsRoot`, wlasny try): tytul -> plik; zly plik (bez RIFF/WAVE,
  rozmiar != 44 + dane z naglowka, blok 4096 B zer na koncu albo w srodku) pominiety i policzony; dubel tytulu, zla linia, linia z zerami - policzone.
  Wyszukanie: dokladny tytul, potem luzny (bez kropki/wykrzyknika na koncu, bez wielkosci liter).
- Latka odbiciem (bez referencji do ROT.dll): `ROT.Screens.EventScreen` implementuje `IGameStateListener` JAWNIE - cele z `GetInterfaceMap`
  (zapas: `AccessTools.Method(t, "TaleWorlds.Core.IGameStateListener.OnActivate")`). Postfiks OnActivate (po nim `_dataSource` jest ustawione) ->
  `EventPanel.EventTitle` (zapas `_eventPopupState.Event.Title`) -> nagranie -> kontrola pliku (md5 z tsv) -> odtworzenie. Prefiks OnDeactivate -> stop
  (OnDeactivate zeruje `_dataSource`). Pustych OnInitialize/OnFinalize NIE latamy (pamiec: zawieszenie GC na malej metodzie z detourem). Po latce
  `Harmony.GetPatchInfo` - finalizer BKROTPatch polyka bledy `Harmony.Patch`, wiec "tak" w logu tylko z GetPatchInfo.
- Odtwarzanie jak powiadomienia z glosem w grze (`SandBoxGauntletGameNotification`): `SoundEvent.CreateEventFromExternalFile("event:/Extra/voiceover",
  sciezka, scena null, 2D, bez blokowania)` + `Play()`; koniec: `!IsValid || IsStopped()` -> `Release()`; stop = samo `Release()` (gra tak robi;
  `Stop()` zeruje uchwyt). Jeden klip naraz - nowe okno zatrzymuje poprzedni. Ten sam ekran aktywowany drugi raz - bez powtorki.
- `Lektor.AppTick` w `SubModuleMain.OnApplicationTick` (przed wylacznikiem wiosek, wlasny try z licznikiem bledu ladowania typu): koniec klipu, kontrola
  "grane po 0,5 s" (`IsPlaying`, `IsPaused`), straznik (gra klip okna, a aktywny stan gry to nie `EventPopupState` -> stop). Nic nie gra - dwa porownania.
- Sesja: `Lektor.SessionStart` (po `InnDrink.SessionStart`), doba: `Lektor.Daily` (po `InnDrink.Daily`), reset: osobna linia w konstruktorze
  `ArmouryBehavior` (`_m103LektorReset`, jak m102-tablica).
- Sprawdzone w dekompilacji: `GameStateManager.OnPushState/OnPopState` (ActiveState = szczyt listy przed `HandleActivate`; PopState: `HandleDeactivate`
  przed zdjeciem), `GameState.HandleActivate` (sluchacze tylko gdy `IsActive`), `RegisterActiveStateDisableRequest` (okno zapytania z pauza NIE wola
  OnDeactivate - okno ROT pod zapytaniem dalej gra), `SoundEvent.Release` (przy graniu Stop -> uchwyt -1).

### K3 + K6 - plotki na zywo (commit 06a6b39c, `Armoury/src/Lektor.Live.cs`, zmiana w `Rumours.ShowPage`)

- `Rumours.ShowPage` tuz przed `ShowInquiry`: `Lektor.GossipPage(place, pages[i], i)`; akcje okna: "Hear more" jak dotad (`next` -> nowa strona zastepuje
  stara), "Enough talk" (dotad null) -> `Lektor.GossipStopAction` (jeden delegat na proces - commit 396fbe64: `InquiryData.HasSameContentWith` porownuje
  akcje, dubel ostatniej strony rozpoznawany jak przed paczka). Klucz LektorEnabled NIE - oba wywolania nic nie robia.
- Kawalki strony: naglowek (tylko strona 1) bez nawiasow z liczbami (`(12 d)`, `(3 more)`) i podzielony na zdania - wstep z kuflem ma kilkanascie
  wariantow, kazdy nagrany raz; kazda plotka (bez "- "); tekst Empty. Bez tytulu okna i przyciskow. Liczby w samych plotkach zostaja.
- Pamiec: klucz = SHA1(voice_id|model|ustawienia|tekst po zlaczeniu bialych znakow) -> `Documents/Mount and Blade II Bannerlord/Armoury/Lektor/cache/<klucz>.wav`,
  indeks `cache/index.tsv` (klucz, czas, znaki, tekst, md5 - tylko dopisywany). Plik dobry (md5 z indeksu) -> gra od razu, zero sieci. Plik uszkodzony -
  usuniety i traktowany jak brak (najwyzej 2 wyslania tego samego tekstu w calej historii). Kopie na D: robi juz `tools/backup-bannerlord.ps1` (caly Documents).
- Brak w pamieci: prosba do JEDNEGO watku w tle (jedna naraz). Watek: strona nadal otwarta? -> limit dzienny (ksiega `Lektor/usage.tsv`, doba kalendarzowa
  komputera) -> klucz z pliku (w chwili zapytania; nigdy w logu) -> stan konta `GET /v1/user/subscription` raz na sesje (blad chwilowy - ponowienie po
  10 min; 401/403 albo odpowiedz bez liczb - do konca sesji) -> rezerwa -> wpis "wyslane" w ksiedze PRZED wyslaniem (Flush(true)) -> `POST
  /v1/text-to-speech/BNgbHR0DNeZixGQVzloa?output_format=pcm_24000` (HttpWebRequest z System.dll, TLS 1.2 dodane, limit 20 s calosci) -> naglowek RIFF 44 B jak
  `nagraj_david.py` -> zapis atomowy (.tmp, Flush(true), Move, md5 po zapisie) -> indeks i "ok" w ksiedze -> wynik kolejka do glownego watku (AppTick) ->
  odtworzenie, jesli strona dalej otwarta. HTTP 401/402/403/429 -> zapytania wstrzymane do konca sesji. Glowny watek nigdy nie czeka na siec.
- Stan konta nieznany (np. klucz bez uprawnienia "User: Read" daje 401 na /v1/user/subscription) = NIC NOWEGO, tylko pamiec (bezpieczny kierunek dla planu
  Starter - nie zjada znakow prologu). Linia w logu mowi to wprost.
- Zatrzymanie: "Enough talk", nowa strona, okno ROT, straznik w AppTick (zadne okno zapytania nie jest otwarte przez 0,3 s). Prosby niewyslane do zamknietej
  strony - porzucone bez kosztu.
- K6: zapytania tylko przy `LogGate.Mode == 0` (gra). Autotest (1/2) i tryb nie rozpoznany (3) - zero sieci, linia "API WYLACZONE (bez pliku
  armoury-lektor-live.flag)"; wyjatek: plik `Documents/Mount and Blade II Bannerlord/CrashScribe/armoury-lektor-live.flag`. Okna ROT graja w autotescie
  jak w grze (log pokazuje start / grane po 0,5 s / stop).

### K5 - linie logu i progi (commit d9d8c64d)

- `Lektor (m103): latka EventScreen: OnActivate tak, OnDeactivate tak | nagran okien 35 (827 s; ok, odrzucone 0, bez md5 0).` (menu glowne, raz na proces)
- `Lektor (m103): start sesji | klucze: lektor TAK, okna TAK, plotki na zywo TAK, limit dzienny 1000, rezerwa 3000 | nagran okien 35 (...) | latka EventScreen: ...
  | plotki: pamiec N plikow (w indeksie M), znaki dzis D / 1000 (doba RRRR-MM-DD), wyslan w ksiedze W | klucz API jest|brak | API na zywo: tak (...) / NIE - powod | tryb gra|autotest|...`
- Okno: `okno ROT "<tytul>" -> <plik> (40.4 s) - start.`, `- grane po 0,5 s: tak|NIE`, `- stop po X s z Y s (zamkniecie okna)` albo `- koniec po X s`
  (`ZA WCZESNIE` = silnik nie gra pliku), `- brak nagrania, cisza.`, `- plik ... uszkodzony (...), cisza.`
- Plotki: `plotki w <miejsce>, strona N: kawalkow K (Z znakow) - z pamieci A, do nagrania B (C znakow)[, bez glosu D (powod)].`, `nowe nagranie plotki <8 hex>: ...`,
  `konto ElevenLabs: zuzyte U z L znakow, zostalo R (rezerwa 3000), odnowienie RRRR-MM-DD.`, `plotka <8 hex> - blad API HTTP 401 (status) ...`,
  `limit dzienny: ...`, `rezerwa planu: ...`, `plotka <8 hex> - grane po 0,5 s: tak (pierwsza w sesji)` / `NIE (pauza silnika - okno plotek pauzuje gre)`.
- Doba: `Lektor (m103): dzien N | okna ROT: otwarte, z nagraniem, odtworzone (grane po 0,5 s tak/nie, koniec za wczesnie), bez nagrania, uszkodzone, ...
  | plotki: strony, kawalki, z pamieci, odtworzone, nowe nagrania (znaki), bez glosu: limit dzienny, rezerwa, API wylaczone, stan konta nieznany, ...
  | zapytania API Q (w sesji), bledy API (kody) | znaki dzis / limit (doba) | konto: ... | watek nagran: potkniecia | potkniecia (w sesji) | tryb.`
- `python -I tools/sprawdz_logi.py --grupa m103lektor <Armoury-*.log> [--crashscribe <session-*.log>]`: LK-0 start, LK-1 ERROR i potkniecia 0 (takze watek),
  LK-2 latka tak/tak, LK-3 nagran = wiersze tsv w repo (35), odrzucone 0, LK-4 autotest/tryb 3: 0 zapytan, 0 "nowe nagranie", 0 "konto ElevenLabs",
  LK-5 okna ROT z nagraniem odtworzone (z --crashscribe: okna zamkniete przez autotest z tytulem z tsv -> start > 0), LK-6 grane po 0,5 s NIE 0 i
  za wczesnie 0, LK-7 start = stop/koniec, LK-8 znaki dzis <= limit, LK-9 plotki (INFO). Sprawdzone na prawdziwych liniach z DLL (proba poza gra) i na logu
  syntetycznym z oknem ROT.

### K7 - skrypty wgrania (poza repo; w tym drzewie nie ma tools/wgranie)

Kopie przed zmiana: `wgraj-v5.ps1.bak-m103-przed`, `bieg-bitwa.ps1.bak-m103-przed` (obok skryptow w `.../6ba92574-.../scratchpad/test`). CRLF zachowane.

`wgraj-v5.ps1` (md5 po zmianie 8da42d06): pliki `Armoury\ModuleData\Lektor\**\*.wav` i `arm_lektor.tsv` na liste (kopia .bak, lustro na D:, md5 jak reszta);
kontrola zrodla .wav: RIFF/WAVE, rozmiar = 44 + dane, md5 = kolumna 4 tsv - inaczej STOP przed jakakolwiek podmiana.

```diff
@@ po petli arm_*.tsv, przed kontrola zrodel @@
+$lekDir = Join-Path $Tree 'Armoury\ModuleData\Lektor'
+$lekMd5 = @{}
+if (Test-Path -LiteralPath $lekDir -PathType Container) {
+    $lekFull = (Resolve-Path -LiteralPath $lekDir).Path.TrimEnd('\')
+    foreach ($f in @(Get-ChildItem -LiteralPath $lekDir -Recurse -File | Where-Object { ($_.Extension -eq '.wav' -or $_.Extension -eq '.tsv') -and $_.Name -notlike '*.bak-*' })) {
+        $r0 = 'Armoury\ModuleData\Lektor\' + $f.FullName.Substring($lekFull.Length + 1)
+        if (@($items | Where-Object { $_.name -eq $r0 }).Count -eq 0) { $items += [pscustomobject]@{ name = $r0; src = $f.FullName; dst = (Join-Path $GameDir ('Modules\' + $r0)); dll = $false } }
+        if ($f.Name -eq 'arm_lektor.tsv') { ... $lekMd5['Armoury\ModuleData\Lektor\EN\<plik>'] = <md5 z kolumny 4> }
+    }
+}
@@ w petli kontroli zrodel, po "zrodlo puste" @@
+    if ($i.name -like '*.wav') {
+        <44 B naglowka>; if (RIFF/WAVE zle albo 44 + dane != dlugosc) { STOP "nie jest dobrym plikiem WAV (zera albo uciety?)"; exit 2 }
+        if ($lekMd5.ContainsKey($i.name) -and (Md5 $i.src) -ne $lekMd5[$i.name]) { STOP "ma inny md5 niz w arm_lektor.tsv (zera?)"; exit 2 }
+    }
```

`bieg-bitwa.ps1` (md5 po zmianie 417db430): te same pliki jako `-ExtraFile` (autotest-bitwa.ps1 tworzy katalog, liczy md5, po tescie usuwa pliki, ktorych w grze
nie bylo - zostaje pusty `Lektor\EN`, lektor bez tsv milczy i pisze linie).

```diff
@@ przed $ef = @() @@
+$lekDir = Join-Path $Tree 'Armoury\ModuleData\Lektor'
+if (Test-Path -LiteralPath $lekDir -PathType Container) {
+    $lekFull = (Resolve-Path -LiteralPath $lekDir).Path.TrimEnd('\')
+    foreach ($f in @(Get-ChildItem -LiteralPath $lekDir -Recurse -File | Where-Object { ($_.Extension -eq '.wav' -or $_.Extension -eq '.tsv') -and $_.Name -notlike '*.bak-*' })) {
+        $r0 = 'Armoury\ModuleData\Lektor\' + $f.FullName.Substring($lekFull.Length + 1); if ($rel -notcontains $r0) { $rel += $r0 }
+    }
+}
```

Proby skryptow (gra Jeffa dzialala - zwykly `wgraj-v5.ps1 -DryRun` stanal na "gra dziala", czyli dobrze): kopia testowa bez bramki procesow na udawanym
katalogu gry w scratchpadzie: 36 wierszy Lektor "NA SUCHO: bylaby podmiana", kod 0; nagranie z zerami na koncu -> STOP md5, kod 2; naglowek z zer -> STOP WAV,
kod 2 (plik przywrocony, md5 jak w repo). `bieg-bitwa.ps1` w kopii bez uruchomienia gry: ExtraFile 61, w tym 36 Lektor.

### Ryzyko / co sprawdzic (regula 0)

- **Regresje:** `Rumours.ShowPage` - jedyna zmiana to wywolanie lektora i akcje "Enough talk" (dotad null) - kolejnosc stron, kryjowki (HideoutSight.Heard),
  ksiega plotek bez zmian; przy LektorEnabled NIE akcje nic nie robia. `OnApplicationTick` - nowy blok przed wylacznikiem wiosek (bez grania: dwa porownania).
  `ArmouryBehavior` - 3 nowe linie (reset, start sesji, doba) we wlasnych try.
- **Kolizje:** zaden inny mod nie lata EventScreen/EventPopupState (rozpoznanie: grep wszystkich DLL modulow); w Armoury `SoundEvent` tylko
  CraftPopup.PlaySound2D; siec i watki - tylko lektor (Log i StartCalibration maja wlasne watki, bez wspolnego stanu). `ServicePointManager.SecurityProtocol
  |= Tls12` dotyczy calego procesu (tylko dodaje TLS 1.2 - tak samo VoiceActingPatch). Muzyka ROT przy niektorych wydarzeniach (np. NedEvent) gra pod lektorem.
- **Spojnosc:** jeden klip naraz (okno ROT przerywa plotke), stan strony i liczniki czyszczone przy nowej kampanii / wczytaniu; pamiec nagran i ksiega
  zapytan - na caly proces (zapis na dysku, celowo miedzy kampaniami). Stan konta pytany raz na sesje (reset przy wczytaniu).
- **Cudzy kod:** EventScreen/EventPanel (ROT 8.1.8), SoundEvent, GauntletQueryManager (kolejka zapytan, pauza), InquiryData.HasSameContentWith,
  GameStateManager - sprawdzone w dekompilacji (patrz K2).
- **Nie sprawdzone bez gry (autotest / Jeff):**
  1. czy silnik gra pliki 24 kHz tak jak 44,1 kHz VoiceActingPatch - linie "grane po 0,5 s: tak", brak "ZA WCZESNIE";
  2. ktory suwak glosnosci steruje lektorem (najpewniej Voice Over + Master) - ucho Jeffa;
  3. czy okno plotek (`ShowInquiry(..., true)` pauzuje silnik) zatrzymuje glos - linia "plotka ... grane po 0,5 s: tak/NIE (pauza silnika)"; wskazowka z gry:
     SandBoxGauntletGameNotification sam pauzuje swoj glos, wiec silnik raczej nie robi tego za nas;
  4. pierwsze nagranie plotki u Jeffa: linia "konto ElevenLabs: zuzyte ... zostalo ..." - jesli "stanu nie da sie odczytac (HTTP 401 missing_permissions)",
     klucz nie ma uprawnienia "User: Read" i nowe plotki sa wstrzymane (tylko pamiec) - wtedy klucz z tym uprawnieniem albo decyzja Jeffa;
  5. opoznienie glosu nowej plotki (model v2 bez strumieniowania) - ok. 1-3 s.
- **Autotest:** zwykle biegi nie maja okien ROT (prolog przesuwa fabule; `at-101-*.out.txt` - 0 linii "wydarzenie ROT"), a plotek autotest nie klika.
  Do potwierdzenia LK-5/LK-6 potrzebny bieg z wymuszonym oknem ROT (np. CrashScribe autotestu wola odbiciem `ROTEventBehavior.TriggerEvent(NedEvent)` w
  kampanii testowej) - NIE zrobione w tej paczce (CrashScribe bez zmian). W kazdym biegu: LK-0..LK-4 (start, 0 bledow, latka tak/tak, 35 nagran, 0 zapytan API).
- Zmiana tekstu okna w aktualizacji ROT: inny tytul = cisza z linia "brak nagrania"; ten sam tytul z innym tekstem = stare nagranie.

**Status:** DO SPRAWDZENIA - nie wgrane, nie testowane w grze. Build 5 modow kod 0 (Armoury, RealisticCaptivity, GrandTourney, ForgeView, CrashScribe),
gen_mcm OK. Proba poza gra (odbicie na Armoury.dll): indeks 35 nagran, kontrola md5/zer/uciecia, naglowek WAV = nagraj_david.py, zapis atomowy, kawalki
strony, SHA1, JSON, odczyt bledu 401 i liczb konta z lokalnego serwera - wszystko OK.
