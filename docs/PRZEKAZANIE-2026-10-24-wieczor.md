# PRZEKAZANIE 24.10.2026 (aktualne 22:30 CEST) - kontynuacja na drugim koncie (limit tygodnia tego konta 100%)

## DOPISEK 26.10 ok. 21:30 CET - WGRANE 126 (muzyka bitwy 164 + muzyka mapy 165/165b); Jeff sprawdza uchem
- WGRANE 126 (21:25): Armoury f67326e2 (build scalenia 8574a3ad; kod C# = 11d6fcfa) + 80 ogg z drzewa n161 (Lektor\Muzyka\Bitwa\lufsNN\bit_*, Lektor\Muzyka\Mapa\lufsNN\map_*).
  Kopie: Armoury.dll.bak-2026-10-26-przed-wgranie126 i D:\Backup-Bannerlord\wgrane\2026-10-26-wgranie126-przed (8e488bd6 = 125, lista nowych plikow nowe-pliki-126.txt).
  Wycofanie: przywrocic DLL 125 i usunac pliki z listy (stara wersja ich nie czyta, wiec mozna je zostawic).
- Autotest 126b: kod 0, ERROR 0; sprawdz_logi m109muz + m165mapa 0 NIE (MAP-7: psai 0 co klatke, motyw mapy gry zatrzymywany, 1 nasze zdarzenie). Recheck bezpieczenstwa
  (wf_96b60ce9-683): StopFree/ReleaseById bez zastrzezen; poprawiony tylko MAP-7 w sprawdz_logi (resztki odcinkow bitwy, 20788b14).
- Statusy 164/165 w CHANGELOG n161 eac436b3; push n161/n164/n165 (ok. 100 MB muzyki i glosow). Zapis testowy przeniesiony na D:\Backup-Bannerlord\zapisy-testowe-archiwum.
- DO SPRAWDZENIA u Jeffa: czy na mapie gra jedna muzyka (126a: "leca dwie naraz"), glosnosc mapy -23 LUFS, muzyka bitwy. Wylacznik MCM "Our Map Music" / "BattleMusicOurs".
- ZNANE DROBNE: 159c - po wczytaniu zapisu partia z zegarem po terminie dostaje raz wyjazd po bron zamiast odsylki; leftover glos psai po bitwie (fade 1.00) slyszalny
  dopiero, gdy oddamy muzyke grze (jak w czystej grze). Kredyty ElevenLabs ok. 9 tys. (200 nowych kwestii glosu czeka).

## DOPISEK 26.10 ok. 21:05 CET - 165b (dwie muzyki) zbudowane; AUTOTEST 126b + recheck bezpieczenstwa w toku
- Diagnoza (wf_9cd7649b-016): nasz MapHold pisal psai 0 tylko raz (odczyt przed zapisem); psai co 30-120 s startuje nowy odcinek mapy na innym glosie
  i natywny kanal najpewniej nie zachowuje 0 -> muzyka gry pod naszym utworem; odczyt "psai 0.00" to tylko pole managed (nie mikser). Inne mody wykluczone.
- 165b (zb\n165): 981f17c5 + a86cc5d1, recenzja 5ad02d06 + 40c923ac: psai 0 co klatke, ForceStopThemeWithFadeOut motywu mapy gry co >= 5 s, trzymanie 0 przy
  ladowaniu misji (20 s), linia "kontrola" co 30 s, sprawdz_logi MAP-7 (jedno zrodlo), StopFree (Stop + zwolnienie po id - TEZ dla dzwiekow bitwy!).
- Scalone do n161: 8574a3ad, build kod 0, Armoury f67326e2. 21:03 AUTOTEST 126b (m113\seria-126b.out.txt) + workflow wf_96b60ce9-683 (recheck: bezpieczenstwo
  natywne StopFree/ReleaseById, koszt, ForceStop). Po obu: sprawdz_logi --grupa m165mapa i m109muz na logach 126b, potem odsluch Jeffa -> WGRANIE 126.

## DOPISEK 26.10 ok. 20:25 CET - 126 WSTRZYMANE: Jeff slyszal dwie muzyki naraz na mapie (okno autotestu 126a)
- Autotest 126a: bitwaN + probyN kod 0, ERROR 0, gra przywrocona 20:22 (w grze 125: Armoury 8e488bd6, CS bef9db58; po tescie zostaly puste foldery Lektor\Muzyka\Mapa\lufsNN - bez znaczenia).
- Jeff ok. 20:10 patrzyl na okno autotestu (myslal, ze to jego gra): "muzyki na mapie kampanii na siebie nachodza, leci nasza muzyka a w tle muzyka z gry", "i leca dwie naraz".
  Log twierdzi, ze psai = 0.00 i gra jeden nasz utwor (20:07-20:12 map_07, map_08, map_03) - druga muzyka spoza naszej kontroli albo psai podnoszone przez gre/mod.
- Workflow wf_9cd7649b-016 (naprawa-165-dwie-muzyki): 3 sledztwa (silnik/psai, inne mody, nasz kod) -> diagnoza -> poprawka 165b w zb\n165 + linia kontrolna co 30 s
  + test sprawdz_logi (jedno zrodlo) -> recenzja -> fix. Potem: scalic n165 do n161, autotest, odsluch Jeffa (jego ucho rozstrzyga), dopiero wtedy WGRANIE 126.

## DOPISEK 26.10 ok. 20:05 CET - 164 + 165 scalone do n161; AUTOTEST 126a W TOKU (muzyka bitwy i mapy)
- 10 utworow mapy nagrane (zostalo ok. 9 tys. kredytow realnie; licznik z opoznieniem 13.5 tys.), wyslane Jeffowi. Przerobione bitwa_v2.py --prefiks map -> 40 ogg OK.
- 164 recheck 7fe8c6d0 (motyw ROT stop 50 ms), 165 e96eb615..bc0010c1 + pliki 16f37999. Scalone do n161: 604cb213 (CHANGELOG: wpisy 165, 164 nad 159).
  Build n161 kod 0, Armoury md5 cda4a309 (build z 604cb213). gen_mcm bez zmian, selftesty m109muz i m165mapa OK.
- WAZNE: n161 mial STARE glosy bitwy (WGRANE 123 bylo ze scratchpad, nie z repo) - 315 plikow Lektor\Bitwa innych niz w grze. Naprawione: 4c63bc38 = glosy 123
  z gry do repo (md5 zgodne z manifestem 395/395). Teraz drzewo n161 = gra (832 takie same, 0 roznych) + 80 nowych (bit_*, map_*). NIE wgrywac wgraj-v5 z drzew
  sprzed 4c63bc38 (cofnelyby glosy 123).
- 20:01 AUTOTEST 126a (m113\seria-126a.out.txt; -Dll mfinal126a -Tree n161, bitwaN + probyN 6 dob). Po TAK -> WGRANIE 126 = DLL cda4a309 + 80 ogg
  (Lektor\Muzyka\Bitwa|Mapa\lufs17-26) z drzewa n161; potem statusy 164/165 w CHANGELOG, push n161/n164/n165; zapis autotest-przekierowany-* na D:.

## DOPISEK 26.10 ok. 19:35 CET - WGRANE 125 (159 + 159b + 159c); muzyka mapy nagrywana; 165 w toku
- WGRANE 125 (19:27): Armoury 8e488bd6 = build scalenia f5048698 noc/161-integracja (status 6674fb98, wypchniete; noc/159-nadzy-lordowie tez wypchniete).
  Kopie: Armoury.dll.bak-2026-10-26-przed-wgranie125 i D:\Backup-Bannerlord\wgrane\2026-10-26-wgranie125-przed (05503a8c = 124). CrashScribe bef9db58, flag brak.
  Autotest 163c: bitwaN + probyN 6 dob kod 0, ERROR 0, potkniecia 0; (2b) 32 -> 18-20 gotowych ponad % po wyjazdach, do domu 161 ludzi w 5 partiach (163b: 120).
- Muzyka mapy: Jeff 19:15 "to zrob pozostale 9" - generuj.py --wszystkie (muzyka_mapy\probka, log generuj.log). Kredyty przed: 26 302.
- W toku: wf_d370822d-ca4 (recheck 164 -> paczka 165 w zb\n165). Potem: bitwa_v2/mapa na 10 mp3 -> scalic 164+165 do n161 -> autotest -> WGRANIE 126
  (DLL + Lektor\Muzyka\Bitwa\lufsNN\bit_*.ogg + Lektor\Muzyka\Mapa\lufsNN\map_*.ogg). Zapis testowy autotest-przekierowany-* w Game Saves przeniesc na D: po serii 126.

## DOPISEK 26.10 ok. 19:07 CET - 159c ZBUDOWANE i scalone; AUTOTEST 163c W TOKU
- 159c (zb\n159): c7c00ae2 + 20aacfb7 (recenzja 2), ed31a2c2 + 8c65f8bb (recenzja 3: 4 potwierdzone + drobne poprawione). Recheck: tylko 1 drobna (po wczytaniu
  zapisu partia z zegarem po terminie dostaje raz wyjazd po bron zamiast odsylki, takze gdy przed zapisem nie byla w drodze) - zostawiona, do pozniejszej poprawki.
- Scalone do n161: f5048698 (konflikt CHANGELOG rozwiazany skryptem). Build n161 kod 0, Armoury md5 8e488bd6.
- 19:05 AUTOTEST 163c (m113\seria-163c.out.txt; -Dll mfinal163c, bitwaN + probyN 6 dob). Jesli urwany: gra wedlug pamieci przerwany-autotest-sprzatanie
  (w grze ma byc Armoury 05503a8c), potem powtorzyc tym samym poleceniem z -Prefix 163d. Po TAK -> WGRANIE 125 = DLL 8e488bd6 (kopia .bak + D:, md5, statusy, push).

## DOPISEK 26.10 ok. 18:55 CET - 164 ZBUDOWANE po recenzji; 165 muzyka mapy (Jeff 18:30); 159c w toku
- 164 (zb\n164, noc/164-muzyka-bitwy): 3d1d6b18 + cc9d4cda + recenzja 5171df53 (Armoury md5 8ebc3f2f). Recenzja: "Stop Event Themes On Map" nie dzialal (ROT gra motywy
  402-415 przez PlaySegment na glosach 7/8 warstwy psai) - teraz refleksja psai m_logik/m_playbackChannels + startFade. Pliki bit_* (40 ogg + 10 mp3) w repo galezi.
- Jeff 18:30: "zrob mi jeszcze 10 utworow na mape kampanii w stylu gry o tron lub sredniowiecza minimum 2 minuty ... nie zbyt intensywna, nie rozprasza, dobry podklad".
  Zadania: scratchpad 1e1c276a...\scratchpad\muzyka_mapy (zrob_zadania.py, zadania\map_01..10_*.json po 125 s, generuj.py - pomija gotowe, STOP < 2500 kredytow).
  Probka map_01_krolewski_trakt.mp3 (2:05) wyslana Jeffowi do odsluchu; reszta 9: po jego OK albo (bez odpowiedzi) po WGRANIU 125. Kredyty przed: 26 302.
- Workflow wf_d370822d-ca4: recheck poprawki 164 -> paczka 165 muzyka mapy w zb\n165 (galaz noc/165-muzyka-mapy od 5171df53): nasze utwory map_* tylko na mapie
  kampanii (psai wyciszone), losowo bez powtorek, przerwa 20-40 s, okna wydarzen / misje przejmuja, klucz MCM "Our Map Music".
- Kolejnosc wgran: 125 (159+159b+159c, po autotescie 163c) -> 126 (164 + 165, pliki bit_* i map_* do Lektor\Muzyka\Bitwa|Mapa\lufsNN, po autotescie).

## DOPISEK 26.10 ok. 18:20 CET - NOWE KONTO (poprzednie: limit tygodnia o 15:17); 159c i 164 w toku
- Test 163b (159 + 159b-fix, build 9650e63d = merge 9263c755 w n161): bitwaN + probyN kod 0, ERROR 0, gra przywrocona 15:22 (Armoury w grze 05503a8c = 124).
- Druga recenzja 159b (wf_e0014686-1ec): 2 prawdziwe usterki -> paczka 159c. Wynik recenzji: scratchpad sesji 1e1c276a...\scratchpad\recenzja-159b-fix2-wynik.json.
- Workflowy z 15:17 (159c wf_0e9e7939-ff8, 164 wf_57fa2a26-aee) padly na limicie - 159c nie ruszyl; 164 zostawil NIEZACOMMITOWANE zmiany w zb\n164
  (EventMusic.Bitwa.cs, tools/muzyka/bitwa_v2.py); ogg 10 utworow x 4 lufs gotowe w scratchpad fffc991e...\muzyka_bitwy\out.
- 18:15 nowe konto: workflow 159c wf_f37a28aa-e17 (zb\n159: poprawka, 3 recenzje + obalanie, fix2, recheck) i 164 wf_3f58b257-583 (zb\n164: dokonczenie, recenzja, fix).
  Skrypty: C:\Users\GAME\.claude\projects\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\1e1c276a-7a4b-41a8-acfd-5b40353b59ab\workflows\scripts\
  Po 159c: merge noc/159-nadzy-lordowie -> noc/161-integracja (zb\n161), build, autotest 163c (bitwaN,probyN 6 dob), dopiero po TAK -> WGRANIE 125 (159+159b+159c).
  Po 164: merge do n161 po 125, autotest, WGRANIE 126 (DLL + bit_*.ogg do Modules\Armoury\ModuleData\Lektor\Muzyka\Bitwa\lufsNN).
- Wrapper testow: scratchpad 6570af7b...\scratchpad\m113\testy-m114.ps1 (163b: -Dll test\mfinal163b -NowyArmoury ... -Prefix 163b -Runs bitwaN,probyN).

## DOPISEK 26.10 ok. 13:45 CET - STAN NA WYPADEK URWANIA (Jeff: "zapisz stan, zaraz moze urwac sie tokeny")
**W GRZE (zatwierdzone):** WGRANE 124 = Armoury 05503a8c (commit 87aaa764 galezi noc/161-integracja), CrashScribe bef9db58, glosy 123 (dane).
Gra posprzatana po URWANYM tescie 163a (13:04): przywrocone z C:\Users\GAME\autotest-kopie\2026-10-26_13-04-00 (Armoury, CrashScribe, arm_canon_moves.tsv,
BannerlordConfig.txt LatestSaveGameName, engine_config.txt safely_exited), flaga usunieta. Procedura: pamiec przerwany-autotest-sprzatanie.md.
**UWAGA:** HEAD noc/161-integracja (worktree zb\n161) ma JUZ scalone 159 (merge, build 9650e63d) - NIE wgrane. Wgrane 124 = 87aaa764.
**W TOKU (jesli urwie sie - co zrobic):**
1. Autotest 163b (159 na 124; bitwaN + probyN 6 dob): m113\seria-163b.out.txt. Jesli urwany: SPRAWDZ GRE (md5 Armoury 05503a8c, CS bef9db58, flagi, Configs)
   wedlug pamieci przerwany-autotest-sprzatanie.md, potem powtorz: testy-noc.ps1 -Dll mfinal163c -NowyArmoury zb\n161\Armoury\bin\Release\Armoury.dll
   -Prefix 163c -Runs bitwaN,probyN -ProbyDays 6 -CsAt test\CrashScribe-at-n103.dll -ExtraFlags armoury-dzwiek-bitwy.flag (run_in_background).
   Kryteria: kod 0, ERROR 0 (bledy 27 first-chance = latki BKROTPatch/BK, tak samo w kazdym tescie); w logu linia dobowa "Nowe druzyny i sprzet (159)".
2. Recenzja 159b fix2 (workflow wf_e0014686-1ec): wznowic Workflow(scriptPath=...\workflows\scripts\recenzja-159b-fix2-wf_e0014686-1ec.js, resumeFromRunId).
   Gdy recenzja i test OK -> WGRANIE 125 (159+159b): kopia .bak + D:, md5, statusy w CHANGELOG n161, push.
3. Muzyka: GOTOWE wszystkie 10 utworow (13:50) w scratchpad\muzyka_bitwy\probka\bit_01..10_*.mp3 (01 = 120 s, reszta 150 s). Jeff o probce bit_04:
   "tak swietny klimat". Kredyty ElevenLabs po muzyce: ok. 26.3k (licznik aktualizuje sie z opoznieniem).
4. Paczka 164 (muzyka tylko w bitwie, motywy wojenne off z mapy) - workflow wf_57fa2a26-aee w zb\n164 (galaz noc/164-muzyka-bitwy od 87aaa764).
   Po nim: tools/muzyka/bitwa_v2.py na 10 mp3 -> ogg w 4 zestawach lufs -> wgrac pliki + DLL 164 (scalony z 159 jesli 125 wgrane) po autotescie.
**DECYZJE JEFFA 26.10:** nadzy lordowie "Pozyczka + mniejsze" + max 10% bez broni (brak pancerza OK); ubrania chlopow barwy ziemi bez szlacheckich;
glosy dziko (v4, bloki, RRRAAAH rzadko); muzyka 10 utworow tylko w bitwie, rozdzielic od mapy; wybor glosow zostawil Claude.
**OTWARTE / POZNIEJ:** pytanie do Jeffa "spotkate koszule" = kolory (zrobione) czy dlugosc?; 200 nowych kwestii glosu (ranni hp/pn/dy, sztandar bl/bg/bt/eb, chor ou -
scratchpad\glosy_v4\klipy_v4_nowe.csv, blok_v4.py) nagrac razem z paczka runtime (ranni krzycza z miejsca 50%, max 4 naraz); sztandar B (przejecie, odbijanie)
i C (piesn zwyciezcow); muzykant w szyku (rog/bodhran, raport w transkrypcie: cornu + bodhran z grajkow karczmy, 2 linie action_sets); drobne: sprawdz_logi ND-7
(13 probek zamiast 6), 146 falszywa UWAGA JIT, Fall Back za mape = PANIKA, phantom wpis DTE w SpawnPost (sprzed 159).

## DOPISEK 26.10 ok. 12:35 CET - WGRANE 121-124 (ubrania, jezdziec, glosy v4, sztandar); w toku 159b, muzyka
- LINIA GLOWNA: noc/161-integracja (worktree scratchpad\zb\n161) = 120 + 160 + 157 + 158 (+ wpis 123). Kolejne paczki scalac tu.
- WGRANE 121 (11:45): Armoury 72b07d42 = 160 ubrania chlopow w barwach ziemi (bez barw rodu - UseTeamColor, bez farbowanych ROT; tekstury sprawdzone).
- WGRANE 122 (12:14): Armoury 2d20df52 = 157 jezdziec: tarcza pod ostrzalem (bez zmiany w szarzy szyku), kontra 1-2 jezdzcow na strzelca (20 s), pomiar konnicy AI.
- WGRANE 123 (12:15, DANE): 314 glosow bitwy na nowo (eleven_v4, bloki, 14 glosow z konta, Karda fonetycznie "Kar-da"); kopie starych: Modules\Armoury\kopie\Bitwa-przed-wgranie123 i D:.
  Narzedzia: scratchpad\glosy_v4 (blok_v4.py, nagraj_v4.py, roles_v4.json, klipy_v4.csv, klipy_v4_nowe.csv - 200 nowych NIE nagranych).
- WGRANE 124 (12:29): Armoury 05503a8c = 158 biegacz po upuszczony sztandar (w autotescie niesprawdzony - szyki puste).
- W TOKU: 159b (nadzy lordowie, regula Jeffa: tylko bez broni, max 10%) w zb\n159; muzyka: probka bit_04_szarza czeka na odsluch Jeffa
  (scratchpad\muzyka_bitwy, zadania/*.json, probka_muzyki.py); pytanie do Jeffa: "spotkate" koszule = kolory czy dlugosc.

## DOPISEK 26.10 ok. 11:05 CET - WGRANE 119 (pasek konia) i 120 (dzwiek 3D); w toku 157-160, glosy v4, muzyka
- LINIA GLOWNA: noc/156-dzwiek-3d-na-119 (worktree scratchpad\zb\n156) = 118 + 155 + 154. Nowe galezie 157-160 odgalezione od niej.
- WGRANE 119 (10:49): Armoury 4372c1cb = 155 pasek staminy konia pod paskiem zdrowia konia (UIExtenderEx, klucz HorseStaminaBar). Autotest 155a TAK.
- WGRANE 120 (11:03): Armoury 7b6dc24c = 154 dzwiek bitwy 3D: 395 klipow przez event:/mod/mission/voice_shout / voice / ambient_3d_big
  (Extra/voiceover bylo 2D - stad "werble przy uchu"), odleglosci przy zrodle, pozycja co klatke, blad 'Save the banner!' przy smierci dowodcy.
  Autotest 156a: kod 0, ERROR 0, zdarzenia 3D odrzucone 0. Slyszalnosc 3D - tylko ucho Jeffa. tools/sprawdz_logi.py ND-7 do poprawy (13 probek, nie 6).
- Uwagi Jeffa z gry 25.10 22:45 (przez sesje SKYRIM) i sledztwo: scratchpad\sledztwo-2245.txt. Zold: nie blad (druzyna 331 + karawana 313, regula BK).
- Decyzje Jeffa 26.10: nadzy lordowie = "Pozyczka + mniejsze" + najwyzej 10% nieuzbrojonych; ubrania chlopow - kolory ziemi, dluzsze, bez barw szlacheckich;
  glosy - dziko jak runda 2 (eleven_v4, bloki, wybor glosow zostawil Claude); muzyka - 10 utworow tylko w bitwie, rozdzielic od mapy; muzykant w szyku - pozniej.
- W TOKU: workflow paczek 157 (jezdziec: tarcza, kontra na strzelca, pomiar konnicy), 158 (biegacz po sztandar), 159 (nadzy lordowie), 160 (ubrania chlopow);
  glosy v4: scratchpad\glosy_v4 (klipy_v4.csv, roles_v4.json, blok_v4.py - nagrywanie blokami z with-timestamps, probka bloki_probka czeka na odsluch Jeffa);
  muzyka: scratchpad\muzyka_bitwy\RAPORT.md (10 utworow, ElevenLabs Music, ok. 22-28k kredytow; najpierw 1 probka).

## DOPISEK 25.10 ok. 22:20 CET - WGRANE 118 (fala "AI walczy o zycie" 139-149 + 153 + 117)
- WGRANE 118 (22:13): Armoury 86195e02 z galezi noc/139-ai-zycie (worktree scratchpad\zb\n139, build 8995f2ab, wpis 86fc8054). Galaz zawiera scalone noc/138-calosc (117 + 153),
  wiec to jest TERAZ linia glowna - nowe prace odgalezniac od noc/139-ai-zycie. CrashScribe bez zmian (bef9db58). Kopie: Armoury.dll.bak-2026-10-25-przed-wgranie118, D:\Backup-Bannerlord\wgrane\2026-10-25-wgranie118-przed.
- Autotest 118a: kod 0, ERROR 0, 4/4 bitwy; kontrola fali: python -I tools/sprawdz_logi.py --grupa ai139 <Armoury-*.log> (galaz n139) - 9/10 TAK.
  NIE: tarcza pod ostrzalem po 1.5 s 24/36 (67%, prog 90%) - nastepna paczka: pomiar po 3 s i powod braku tarczy.
- Po pierwszej grze Jeffa: --grupa ai139 na jego logu; 141/146/148/149 i oblezenia (145, 147) autotest nie sprawdza. Odlozone drobne: 146 falszywa UWAGA JIT przy bandytach,
  Fall Back za mape = PANIKA. Decyzje Jeffa 25.10 ok. 22:25: odwrot rannego z bronia w oblezeniach/kryjowkach - NIE (zostaje tylko bitwa polowa, bez zmian kodu);
  poscig za armia AI w odwrocie na rozkaz wodza - "zobacze jak pogram" (czekamy na jego uwagi po grze, na razie NIE).

## DOPISEK 25.10 ok. 18:30 CET - WGRANE 117 (151 + 152 + 150a + 150b + 117r); fala AI 139-149 nadal w toku
- WGRANE 117 (18:23): Armoury 2c95388a (galaz noc/138-calosc, commit c55b2c58; statusy 746e87a2/7cc6ba66), CrashScribe bez zmian (bef9db58). Kopie: Armoury.dll.bak-2026-10-25-przed-wgranie117
  obok DLL i D:\Backup-Bannerlord\wgrane\2026-10-25-wgranie117-przed.
  - 151: odnowa oddechu wykladniczo od Atletyki (wolny 2x szybciej co 100 pkt od 100; 300 - po wyczerpaniu ok. 3 min, 10 - ponad 20 min).
  - 152: ponad 300 pula RBM 2800 x 2^((A-300)/31) (310 ok. 3500, 330 ok. 5500); oddech i bieg razem z nia; sufit sprawnosci biegu 2.5.
  - 150a: proba albumu obrazkow okna lupu (tylko z flaga armoury-zdjecia-obrazkow.flag). 150b: obrazek okna lupu wedlug miejsca (region kultury osady, Inni, dzicy,
    morze, turniej - sala tronowa tylko w King's Landing, bez Joffreya). 117r: 6 poprawek po niezaleznej recenzji (Skagos to nie dzicy - kultura rodu; bez Ship1/Ship2;
    osada obrazka od miejsca bitwy; osady Innych; krzywa 1.0 + mistrzostwo; opis suwaka).
  - Autotesty 152a, 117a, 117b: kod 0, ERROR 0, 4/4 bitwy, zdjecia okien 22/22. Zdjecia okien lupu: docs/zdjecia-okien-2026-10-25/okna-132-[5-7]-*-117.jpg.
- Znane, niepoprawione (nastepna paczka): tekst okna "Near X" bierze Hero.LastKnownClosestSettlement (rzadko odswiezane) - w autotescie bitwa pod Riverrun, tekst "Near Pendric";
  obrazek juz liczy osade od miejsca bitwy (117r), tekst jeszcze nie.
- W TOKU: workflow fali AI 139-149 (galaz noc/139-ai-zycie, worktree zb\n139) - po nim scalic noc/138-calosc (151/152/150/117r) do 139, test bitwaN, wgranie 118.

## DOPISEK 25.10 ok. 17:20 CET - WGRANE 116; w toku fala AI 139-149 i obrazki lupu 150
- WGRANE 116 (17:15): Armoury 25f69127 + CrashScribe bef9db58 z galezi noc/138-calosc (worktree scratchpad\zb\n138): 132 proba zdjec okien, 133a-g okno Review the army
  (Kit for the next battle z podgladu DTE, Last battle, konie, Speed, Herd, Pay x3, Recent messages; 133a CrashScribe - cisza podgladu), 136 stamina koni (HorseWind), 137 oddech dwufazowy.
  Autotest 138a kod 0, ERROR 0; zdjecia okien w docs/zdjecia-okien-2026-10-25. 133a NIE sprawdzony autotestem (testowy CrashScribe-at) - w logu CS Jeffa linia
  'Mends: podglad gotowosci DTE (133a) - pole _isReadinessPreview JEST'. T1 konia gracza (czy MountSpeed spowalnia) - tylko gra Jeffa: linia 'Konie (136)' v/(Vbase x m) ok. 1.
- Drobne uwagi z przegladu 138 (do poprawy w nastepnej paczce, wpis 138 w CHANGELOG): liczniki NB2 przy koncu odpoczynku konnicy, linia 'BattleWind (130)' z martwa regeneracja,
  opisy MCM po 136/137, licznik E oddechu, Atletyka biegu 127 surowa.
- W TOKU: workflow fali "AI walczy o zycie" (galaz noc/139-ai-zycie od 138, worktree zb\n139; kroki 139 pomiar i przelacznik paniki, 140 tarcza pod ostrzalem, 141 obronca naprzod,
  142 panika bez powrotu, 143 ranny <20% odwrot z bronia, 144 AI oszczedza sily + animacje/glos zadyszki, 145 ranny ostrozny, 146 szyk pod ostrzalem, 147 panika do wyczerpania,
  148 poscig konnicy AI, 149 bohaterowie ostrozni). Decyzje Jeffa 25.10: ranny <20% odwrot z bronia; panika bez powrotu (zbiorka pozniej); lordowie ostrozni; poscig z limitem.
- W TOKU: 150 obrazki okien lupu wedlug miejsca - krok 1 album 48 sprite'ow ROT (galaz noc/150-obrazki, flaga armoury-zdjecia-obrazkow.flag), potem dobor (region, przeciwnik, rodzaj).
- Wrapper testow: m113\testy-noc.ps1 rozbija -ExtraFlags po przecinku (od 13:00).

## DOPISEK 25.10 ok. 13:15 CET - WGRANE 114 i 115; w toku: konie, regeneracja, zdjecia okien, raport armii
- WGRANE 114 (12:31): Armoury 3cab5bea z noc/final (125 okno lupu po turnieju, 126 przypinanie typow wojska). Crash 126a w bitwie 4/4 = AccessViolation w natywnym Agent.MakeVoice
  z Immersive_Battlefields.PlayOrderGesturesPatch.Prefix (IB i gra wolaja Owner.MakeVoice bez IsActive; autotest ponawia rozkaz szarzy co 15 s) - powtorka 126b czysta.
- WGRANE 115 (13:10): Armoury 70246086 z galezi noc/127-bitwa-cialo (worktree scratchpad\zb\n127; CHANGELOG 127-131) + 16 klipow mo_karda w Lektor/Bitwa/motto i manifest.
  127 bieg wedlug fizjologii (jedna regula, w biegu bez oddechu), 128 szarza z daleka marszem (bieg od 60 m), 129 okrzyki krolestwa gracza "Karda" (gdy Jeff wlada krolestwem),
  130 stamina tylko z Atletyki (bez Endurance bohaterow) + krzywa 1.12, 131 udzwig +5 kg za punkt Endurance kazdego bohatera w partii.
- BLAD WRAPPERA (naprawiony 13:00): testy-noc.ps1 '-ExtraFlags a,b' przez powershell -File = JEDNA flaga 'a,b' - dlatego w nocy proby P4/T14/T15 nie ruszaly. Teraz rozbicie po przecinku.
  Proby P4 (przeglad armii), T14/T15 (przesluchanie), test okna turnieju - DO PUSZCZENIA jeszcze raz (biegi probyN / turniejN).
- W TOKU (workflowy tej sesji): rozpoznanie staminy koni (Jeff: "konie tez powinny miec stamine ... zalezna od cech konia"), regeneracja wedlug fizjologii (Jeff: "wyjscie z zadyszki
  jak w prawdziwej fizjologii"), paczka 132 proba zdjec nowych okien (worktree zb\n132, galaz noc/132-zdjecia - flaga armoury-zdjecia-okien.flag), rozpoznanie 133
  raportu w przegladzie armii (Jeff: braki sprzetu i dlaczego nie zaklada, zold, konie, predkosc, herding).
- Jeff wie: stamina koni, regeneracja i 133 beda osobnymi paczkami; zdjecia okien wysle po biegu 132.

## DOPISEK 25.10 ok. 06:30 CET - WGRANE 113 (nocny pakiet gotowy przed 12:00)
- W grze: Armoury 6b4f188b = galaz noc/final 1e7593e5 (CHANGELOG 203ac63a): 114 marsz i doktryny kultur (+S1 ostrzal, S3 flanka, S4 lucznicy za piechota), 115 choragwie, 116 przeglad armii w obozie, 117 okno lupu (bitwa, oblezenie),
  118 friendly fire (strzaly 100% / oblezenie 50%, wrecz 50%, szarza 50%), 119 dzwiek bitwy, 120 przesluchanie (juz w 112), 121 podatek warsztatow (juz w 112), 122 gotowosc AI (sila z uzbrojeniem, dozbrajanie), 123 szturm murow i bebny oblezenia, 124 domyslne + nagrania.
- Pliki poza DLL w grze: Modules\Armoury\ModuleData\Lektor\Bitwa (379 ogg + manifest.tsv, 15 MB) i Modules\Armoury\GUI\Prefabs\ArmSpoilsWindow.xml. Kopia przed: Armoury.dll.bak-2026-10-25-przed-wgranie113, D:\Backup-Bannerlord\wgrane\2026-10-25-wgranie113-przed.
- Nagrania ElevenLabs: 298 glosow + 81 SFX, 16 glosow z konta Jeffa (bez dodawania z biblioteki - konto 33 glosy przy limicie 30), kredyty 88 120 -> 76 203. Narzedzia tools/lektor/nagraj_bitwa.py, sfx_bitwa.py na galezi noc/p2a (19a871a5); kopia nagran D:\Backup-Bannerlord\nagrania-bitwa-2026-10-25.
- Autotesty 124b/124d: ERROR 0, 4/4 bitwy. NIE pokryte: oblezenie (szturm, ogien, tlum, bebny), okno lupu po oblezeniu, proba P4 i T14/T15 (nie odpalily w 124c - przyczyna nieustalona), sluch i wyglad.
- Do sprawdzenia w logu Jeffa po pierwszej sesji: ERROR 0; "Dzwiek bitwy (noc-p2): misja" (wyciek 0, odrzucenia wedlug grup), "FF (pole|oblezenie)" (udzial FF), "Choragwie (CH) ekonomia" (odlozone == oddane), "Okno lupu (117)", "Gotowosc do bitwy (p9)" linia doby, "Przesluchanie (m109): pochodzenie - banda", D11-3 "w tym gracz zaplacil".
- Decyzje Jeffa otwarte: dubel SiegeAssaultCrowd z ROTBA Contextual Combat Shouts; glosy z biblioteki zamiast narratorow konta (wymaga zwolnienia miejsc); splity RBM a doktryny (Polnoc, Zelazne, Dorne, Lorath); przeglad armii v2 (przypinanie typow, pelne okno); okno lupu po turnieju (v1.1); wyzwiska (v1.1).
- Zapis testowy autotest-przekierowany przeniesiony na D:\Backup-Bannerlord\zapisy-testowe-archiwum.

## DOPISEK 25.10 ok. 04:05 CET - WGRANE 112
- W grze: Armoury 68f74d0f = galaz noc/wgranie112 (a14c76d3 + noc/p7-przesluchanie 443b13f5 + noc/p8-podatek 8be4aeca; CHANGELOG 120, 121). Kopia przed: ...\Armoury.dll.bak-2026-10-25-przed-wgranie112 i D:\Backup-Bannerlord\wgrane6-10-25-wgranie112-przed.
- Autotest 112a: ERROR 0, 4/4 bitwy. Do sprawdzenia w grze Jeffa: "Przesluchanie (m109): pochodzenie - banda", "bez migawki 0", opcja szara; D11-3 "w tym gracz zaplacil X".
- KOLEJNA INTEGRACJA (noc/calosc z workflow noc-bitwa-kod) MUSI scalic noc/wgranie112 i noc/p9-gotowosc (gotowosc AI) + dzwiek szturmu murow i bebny oblezenia (do dopisania).
- Logi sesji Jeffa 02:11-03:46: D:\Backup-Bannerlord\logi-jeffa6-10-25_03-50-po-grze. Zapis testowy = kopia jego zapisu z 03:46 (md5 0ea55e89).

## DOPISEK 25.10 ok. 02:35 CET - NOC W TOKU (workflow noc-bitwa-kod, run wf_d049918d-367)
- Projekty z rozpoznania: scratchpad fffc991e...\scratchpad\noc\projekty\ (ai-marsz-taktyki.json, dzwiek-glosy.json, choragwie.json, przeglad-armii.json, okno-lupu.json, friendly-fire.txt, krytyk.txt - podzial P0-P5, kolizje K1-K17, zakres v1).
- Galezie: noc/p0 (baza: klucze, wpiecia, kontrakty) -> noc/p1-marsz, noc/p2a (nagrania ElevenLabs, budzet 25k znakow + 120 SFX), noc/p2b-dzwiek, noc/p3-choragwie, noc/p4-przeglad, noc/p5-lup, noc/p6-ff -> noc/calosc (scalenie, CHANGELOG 114-119) -> audyt.
  Drzewa: scratchpad\zb\noc-<nazwa>. Baza a14c76d3 (= wgrane 111).
- Potem sesja glowna: autotest bitew (UWAGA zmiana czasu), wgranie DLL + plikow poza DLL (Armoury/ModuleData/Lektor/Bitwa/**, Armoury/GUI/Prefabs/ArmSpoilsWindow.xml) przed 12:00.
- Osobno: diagnoza warsztatu Jeffa ("czemu moj warsztat nie daje kasy") - agent tylko czyta logi z D:\Backup-Bannerlord\logi-jeffa\2026-10-25_01-00-po-grze.

## DOPISEK 25.10 ok. 02:15 CET (po zmianie czasu) - WGRANE 111
- W grze: Armoury 9e301ed6 = galaz w-toku/m117-ubranie b74049c9 (111 ubranie cywilne v3, 112 szyk armii na postoju, 113 kij z domu; CHANGELOG na galezi, a14c76d3). Reszta modow bez zmian
  (CrashScribe 84205371, RC 8528f098, GrandTourney 900e32bc, ForgeView f8c2796e). Kopia przed: Modules\Armoury\bin\...\Armoury.dll.bak-2026-10-25-przed-wgranie111 i D:\Backup-Bannerlord\wgrane\2026-10-25-wgranie111-przed.
- Autotest 117a: 4 bitwy, KONIEC OK, ERROR 0; kij 58/58 potwierdzone tymczasowe; szyk 4.05 -> 0.42-0.50 jedn.; ubrania 0 przypadkow (sprawdzic w grze Jeffa). PULAPKA: zmiana czasu 03:00 -> 02:00 myli wrapper (szuka logu po czasie startu).
- Zapis testowy Game Saves\autotest-przekierowany-IronmanKkUpHftX7ZIq.sav (kopia zapisu Jeffa z 00:59, md5 9a0b4ab9) zostaje na nocne testy - po serii przeniesc na D:.
- NOC (Jeff "start" 02:55): pakiet bitwy sredniowiecznej + przeglad armii + okno lupu + friendly fire + dzwiek (przemowy rodow, motta przy szarzy, jeki rannych po bitwie, konie, szczek broni, deszcz/wiatr,
  ptaki i kruki po bitwie, ogien przy oblezeniu) - gotowe na 25.10 12:00. Rozpoznanie: workflow noc-bitwa-rozpoznanie (5 czesci + krytyk) i osobne rozpoznanie friendly fire.

## DOPISEK 25.10 00:20 (drugie konto)
- Kontrola 110 w sesji Jeffa (start 22:24, Armoury-2026-10-24_22-24-27.log): OK - wojna korona-Dorzecze raz (108869, E7/0 + kruk), sojusz Polnoc+Dorzecze
  (108870, do dnia 586), zwrot Neda 292902 raz (nr=done), Cape Wrath jedno oblezenie (bez petli), Stoneknoll 31 przy powrocie wozu, zloto przy wczytaniu +616,
  ERROR Armoury 0. Jeszcze nie bylo: ekran trofeow (bitwa gracza 22:58 jest - sprawdzic linie m114), chleb (po zapisie i wczytaniu), okno ROT o Dorzeczu (dzien 108).
- m117-ubranie (Jeff 23:05: goli zolnierze maja dostac najtansza tunike i buty swojej kultury): galaz w-toku/m117-ubranie (bb367a94, od 6cd80130, wypchnieta),
  drzewo scratchpad fffc991e...\scratchpad\zb\m117-ubranie, Armoury.dll md5 14e193c7 (tylko Armoury; CrashScribe bez zmian). CHANGELOG 111 na galezi.
  Status: ZBUDOWANE, recenzja 2x (2 uwagi minor poprawione), CZEKA na zamkniecie gry -> autotest bitwy na kopii zapisu Jeffa -> wgranie.
- Pytanie Jeffa o rozjechana armie (Gregor Clegane 1577 przy Mousedown Mill, dzien odpoczynku R3-08a): nasz kod nie rusza pozycji doczepionych; szyk gry
  (Army.GetRelativePositionForParty) stawia druzyny za wodzem do ok. 1.5-2 jedn. i nie dociaga ich na postoju. Czeka na Jeffa: najechac na grupe i ewentualnie
  zgoda na sciaganie figurek na poczatku postoju (tylko obraz, raz przy zmianie stanu).

## NAJWAZNIEJSZE - STAN NA 22:30
- W grze: WGRANIE 110 (Armoury ae412d24; galaz w-toku/m116-fabula 6cd80130, wypchnieta; drzewo scratchpad 6570af7b...\scratchpad\zb\m116-fabula). Reszta modow: CrashScribe 84205371, RC 8528f098, GrandTourney 900e32bc, ForgeView f8c2796e. Lancuch galezi: 107 (n36 d3cada05) -> 108 (n37 9300d259) -> 109 (n38 34f0db38) -> 109.1 (m115-okno e2417cd3) -> 110 (m116-fabula 6cd80130).
- Zapis Jeffa: Game Saves\IronmanKkUpHftX7ZIq.sav (21:03, md5 35d84a9c; kopia D:\Backup-Bannerlord\logi-jeffa\2026-10-24_21-05-po-grze). W Game Saves brak zapisow autotest-* (archiwum D:\Backup-Bannerlord\zapisy-testowe-archiwum, 426 plikow).
- 110 przy pierwszym wczytaniu w kampanii Jeffa: Dorzecze w wojnie z korona, sojusz Polnoc+Dorzecze, zwrot 292 902 Polnocy (raz) - SPRAWDZIC w jego logu (linie 'Wojny kanonu (m104): (4) Dorzecze', 'nr=done:292902', ERROR 0).

## DO ZROBIENIA (kolejnosc)
1. Kontrola 110 w logu Jeffa po jego pierwszej sesji (wyzej) + ekran trofeow ('Ekran trofeow (m114): bitwa ... poza ekranem 0') + wyplaty Stoneknoll przy powrocie wozu.
2. Raport swiata 24.10 (szczegoly nizej): dryf skarbcow koron (36% pieniedzy, cel 22%, bezpiecznik oszczednosci na suficie 3.7), zloto znikajace przy wczytaniu (-43..-84 tys.) i 3 doby z RESZTA ok. -55 tys., martwy front Joffrey-Robb (19 dni bez bitwy; 1 zamek zdobyty w 215 dni), biedne wsie Polnocy i Reach (wsie < 1000 zl 10 -> 51).
3. Mniejsze: ROT wojna #2 EndDay 0 (Dorzecze nie poddaje sie po Krwawych Godach - sprawdzic przy Godach), Beric w Essos, obrazki scen prologu z ROT, nagrody turniejow lokalnych 7-85 zl, Nocna Straz za silna / Dothrakowie za slabi, wywrotka silnika przy wyjsciu (zrzut).
4. Pytanie bez odpowiedzi Jeffa: limit taboru w kolumnie marszowej (MarchPackAllowance 0.25 = 1 zwierze juczne/hodowlane na 4 ludzi; Jeff pytal o -0.62 'Marching column: baggage train') - zaproponowano 1 na 2, czeka na decyzje.

## PULAPKI TESTOW (24.10)
- Wrapper scratchpad\m113\testy-m114.ps1 trzyma zdjecie bazy w m113\zapisy\baza-autotest-przekierowany-*.sav i PRZYWRACA je na starcie, jesli plik w Game Saves jest inny - przed seria na nowszym zapisie Jeffa usunac stare zdjecie (bieg 116a poszedl przez to na zapisie z 02:26).
- Gra testowa wisi przy wyjsciu po 'KONIEC OK' - zamknac PID gry testowej (Bannerlord.BLSE.Standalone), wrapper sam przywraca pliki.
- Katalog DLL z wrappera nie ma CrashScribe.dll - do wgrania dolozyc produkcyjny CrashScribe (84205371) z poprzedniego katalogu.
- Po serii przeniesc autotest-*.sav na D: (NIGDY przy wlaczonej grze).

---
## (dalej: tresc z 21:10)

## Stan gry Jeffa
- W grze: WGRANIE 109.1 (Armoury feb5abdb; galaz w-toku/m115-okno e2417cd3 = w-toku/n38-calosc 34f0db38 + okno inwestycji). Reszta modow jak 106.1 (CrashScribe 84205371, RC 8528f098, GrandTourney 900e32bc, ForgeView f8c2796e).
- Kampania Jeffa: Ironman Documents\Mount and Blade II Bannerlord\Game Saves\IronmanKkUpHftX7ZIq.sav (ostatni zapis 21:03, kopia D:\Backup-Bannerlord\logi-jeffa\2026-10-24_21-05-po-grze).
- 425 zapisow testowych przeniesione na D:\Backup-Bannerlord\zapisy-testowe-archiwum (Game Saves: 105 zapisow, Ithauser tylko jeden). Kopie Ithauser z testow: D:\Backup-Bannerlord\zapisy-testowe-ithauser.
- Jeff skonczyl grac o 21:03. Gra zamknieta.

## Co dzis wgrano (CHANGELOG 107, 108, 109, 109.1)
- 107: walka w bitwach (m113-walka2), pobor we wsiach pobocznych (pula 10%, odrost z przyrostu naturalnego E4/4), kupno ziemi (cena 14-20 lat wedlug relacji), GrowthMaxPercent 1.3 (tez w Armoury.json Jeffa).
- 108: caly lup z bronia na ekranie trofeow (SpoilsSoldierSplitOff - wylaczony ukryty podzial Spoils EnablePrioritySystem), bron ze stanem z ciala (LootWeaponsKeepCondition), dochod prac na zielono (WorksIncomeShown). Potwierdzone w grze: ekran 70/70 i 79/79 sztuk.
- 109: wyplata prac z udzialu dodatku w targu (WorksPayMarketShare; Stoneknoll 1.7 -> 4.5/dobe), komunikat musztry wedlug przyczyny, Lordship i zamek wlaczone po tescie 114a-ziemiaD (T11 Sealbay, T12 Ramsgate TAK).
- 109.1: okno 'Invest in its works' - 5 linii opisu, lista kwot widoczna.

## m116-fabula - WGRANE 22:24 jako 110 (patrz CHANGELOG); ponizej opis zakresu
Jeff 21:00 (wsciekly): "Dorzecze jest w pokoju - co to za bzdura, to bylo pierwsze, co zostalo zaatakowane". Kanon: po pojmaniu Tyriona Gora pali Dorzecze, Jaime bije lordow Dorzecza i oblega Riverrun PRZED egzekucja Neda; potem Polnoc i Dorzecze razem przeciw koronie. U nas plan wpuszczal Dorzecze do wojny ok. 108 dnia opowieci - blad.
Workflow wf_4fdd61ac-eb2 (task waydblgky) - skrypt: C:\Users\GAME\.claude\projects\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\6570af7b-c8f5-4f3e-827c-790a5efcedd4\workflows\scripts\m116-dorzecze-w-wojnie-i-fabula-wf_4fdd61ac-eb2.js (kopia: docs/przekazanie-m116-workflow.js). Worktree (jesli powstal): scratchpad 6570af7b...\scratchpad\zb\m116-fabula, galaz w-toku/m116-fabula od e2417cd3 (w klonie scratchpad\zb\n35-calosc, origin = to repo).
Zakres (jedna paczka):
1. Dorzecze w wojnie z korona od konca prologu (nowe kampanie) + JEDNORAZOWY catch-up w zapisie Jeffa przy wczytaniu: Dorzecze wypowiada wojne Joffreyowi, Polnoc i Dorzecze razem (Diplomacy jesli sie da), kruk; stary termin ~108 ma byc no-op. Sprawdzic cala mape wojen 298-300 z kanonem (Renly/Stannis vs Joffrey itd.); wojny ROT wybrane przez Jeffa (Dolina-Zelazne, Reach-Dorne) zostaja.
2. Okup Neda: blokada wykupu od konca prologu do egzekucji (Prologue.World.cs RansomDailyPrefix / EndCaptivity tylko p01/p10); jednorazowy zwrot 292 902 ze skarbca korony do Polnocy (log [20:06:26] 'Okupy (178): AI-AI - Eddard Stark ... wolny za 292902').
3. Petla oblezenia Cape Wrath (armia Renly'ego zaczyna i zwija oblezenie 60+ razy - linie 'Koniec oblezenia (KO) ... ROT_castle18 ... odejscie z obozu (zmiana celu ...)'): minimum trwania / histereza celu + kruk 'oblezone' raz na (armia, osada).
4. Chleb biedoty (TownFolk.Bread.cs:17 'pamiec sesji') - zapisac w SaveText.Sync.
5. Kruki: smierc Roberta z pustym tekstem ([18:49:27] Kruki (R3-05c)) - tekst; pojmanie Jaimego (arm_canon_moves.tsv p10 bez wiadomosci, przypisane Dorzeczu) - wiadomosc i Robb/Polnoc.
Potem: build, autotest na kopii zapisu Jeffa 2-3 doby (wojna jest, zwrot raz, ERROR 0), wgranie, CHANGELOG 110.

## Raport swiata (24.10 21:00, pelny w odpowiedzi czatu) - do zrobienia pozniej
- Dryf skarbcow koron (36% pieniedzy, cel 22%), bezpiecznik oszczednosci na suficie 3.7 od 108732; wsie < 1000 zl 10 -> 51; Polnoc i Reach biednieja.
- Pieniadze przy wczytaniu: -43..-84 tys. przy 3 z 4 wczytan; 3 dni z RESZTA ok. -55 tys. (108781, 108817, 108844).
- Wojna Joffrey-Robb bez bitew 19 dni; 1 zamek zdobyty w 215 dni (martwy swiat).
- Beric jeniec, potem w Essos; obrazki scen prologu z ROT; nagrody turniejow lokalnych 7-85 zl; Nocna Straz za silna (7900), Dothrakowie za slabi (173).
- Wywrotka silnika przy wyjsciu 0xC0000005 (znana, ERRORS.md) - zrzut pamieci.

## Jak testowac i wgrywac (sprawdzone dzis)
- Kopia zapisu Jeffa do testu: Copy IronmanKkUpHftX7ZIq.sav -> autotest-przekierowany-IronmanKkUpHftX7ZIq.sav (Ironman zapisuje pod ta nazwa); PO SERII przeniesc autotest-*.sav na D: (NIGDY gdy gra dziala - 17:19 crash przy wczytaniu).
- Wrapper: scratchpad 6570af7b...\scratchpad\m113\testy-m114.ps1 (biegi: kopia, bitwa, bitwaB, bitwaOFF, ziemia, ziemiaD, pobor, poborF, wczyt; -Dll <nowy katalog w test\>, -NowyArmoury <dll>, -Tree <drzewo>, -CsAt test\CrashScribe-at-n103.dll, -Prefix, -BezKopii, -PoborDays). Plan: m113\PLAN-TESTOW.md.
- Wgranie: test\wgraj-v5.ps1 -DllDir test\<katalog z 5 DLL> -Tree <drzewo> -Tag 2026-10-xx-wgranieNNN (katalog test = scratchpad 6ba92574...\scratchpad\test). Gry nie moze byc wlaczonej.
- Monitoring gry Jeffa: Monitor na najnowszym Modules\Armoury\Armoury-*.log (ERROR, 'Ekran trofeow (m114): bitwa', 'Ziemia (m113): KUPNO').

## Zasady Jeffa (pamiec)
- Wersje jak najszybciej ("ciagle cos naprawiamy zamiast pograc"): jedna seria testow, potem wgranie; male zmiany wgrywac po zamknieciu gry bez testu ("wgraj jak wylacze gre, na moim logu sprawdzisz").
- Luzne pytania - krotka odpowiedz bez agentow. Liczby z istniejacych modeli moda (nie nowe stale). Jedna regula gracz/AI. Nic nie wgrywac/testowac, gdy gra dziala.
