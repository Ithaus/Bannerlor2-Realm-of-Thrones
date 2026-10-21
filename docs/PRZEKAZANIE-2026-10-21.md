# PRZEKAZANIE 21.10.2026 (pisane 20.10 ok. 17:40 PDT, aktualizowane w nocy) - START TUTAJ, jesli to nowe konto/sesja

Jeff 20.10 17:40: "Pilnuj limitu, na biezaco zapisuj postep prac, aby nic nie stracic, najwyzej jutro nowe konto bedzie kontynuowac prace, jak skonczy sie limit."
Jeff 17:00: "Ok skonczylem grac, masz cala noc, aby teraz wszystko wdrozyc, wgrac etc., rano za 12 godzin czekam na raport, ze wszystko gotowe."

## Gdzie co jest
- Repo: C:/Users/GAME/Bannerlor2-Realm-of-Thrones (CLAUDE.md, docs/STAN-PRAC.md ### 100-102, docs/PLAN-NOC-2026-10-21.md, docs/ZADANIA-NOC-2026-10-20.md,
  docs/ZADANIA-OBOZ-NOC.md, docs/ROZPOZNANIE-OBOZ-2026-10-20.md, docs/PLAN-OPTYMALIZACJA-NOC.md). Push: galaz claude/bannerlord-rot-setup-o75kvo.
- Klon roboczy "lancuch": C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\3cf3e0ac-5529-4b68-a794-0edec69cfda7\scratchpad\lancuch (origin = repo powyzej; push dwustopniowy: lancuch -> repo -> GitHub).
- Drzewa robocze: C:\Users\GAME\AppData\Local\Temp\claude\C--Users-GAME-Bannerlor2-Realm-of-Thrones\ae222cb7-a7c2-473a-8fc1-1dc1d6f688e3\scratchpad\zb (n25-calosc = kandydat 96; m96-*, m97-*; n22-at = autotestowy CrashScribe).
- Testy: C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\6ba92574-15a3-4209-a065-ed325b84b1b4\scratchpad\test (bieg-bitwa.ps1, wgraj-v5.ps1, katalogi DLL n24b/n95a..., CrashScribe-at-*.dll); skrypty testow i strazy: C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\f59fb043-7e94-4369-a0b2-b140798da059\scratchpad
  (testy-95.ps1 - wzor: bitwa 5 dob x4 + nowa + 160 z autotest-F-d240; safe-mode-guard.ps1; straz-jeffa.sh - obserwator sesji Jeffa; gt-straz.sh - kopie GrandTourney.log;
  rozwiaz.py - konflikty dopisow przy scalaniu; licz_wzorce.py).
- W grze: WGRANIE 95a (Armoury edd0c0f8, CrashScribe 76ec2034, GrandTourney e7caa02f, RealisticCaptivity 9e72fc80, ForgeView 19aa052a).
- Kampania Jeffa: IronmanNXRiZVlMjPu3.sav - NIGDY w autotescie; kopia D:/Backup-Bannerlord/zapisy-jeffa/2026-10-20-wieczor. Logi dnia: D:/Backup-Bannerlord/logi-jeffa/2026-10-20.

## Kandydat WGRANIA 96 = w-toku/n25-calosc (drzewo zb/n25-calosc, HEAD 25f24c17 o 17:40)
Scalone: m96-kuznia (CrashScribe - crash projektowania broni), m96-zamek (menu zamku), m96-valyria (drogi Valyrii +25%, szlaki karawan Essos), m96-sakiewki, m96-koszyk,
m96-len (czesciowo), m96-bandy (bandy jedza, zapas i kiesa), m96-czaskuzni (jeden pasek kuzni; wytop zablokowany i najpierw; noca kowale spia - potwierdzone przez Jeffa),
m96-jakosc (rzadkosc L/MW), m96-wzory (wzory po ostatecznych statystykach, naprawa zapisu #m96seedfix1), m96-dlugi (dlugi miast, czesciowo), licznik turniejow tylko rycerze,
RareBowsIds = dragonbone_bow,weirwood_bow (Ravens Teeth/Goldenheart/olbrzymi do wykucia).
DO DOLACZENIA, gdy workflow skoncza (wyniki: tasks/*.output albo journal.jsonl w subagents/workflows/<run>):
- m96-wies (wf_72028594-b56): menu wsi jak miasto/zamek + komunikaty platnosci z powodem ("You paid 2 for your night at the inn ...");
- m96-wzory2 (wf_39026e72-fa6): naprawa zapisu #m96seedfix2 (lowland_yew_bow t5 wylosowany przez zle t6);
- m96-receptury (wf_3eb8bfa3-879, skrypt workflows/scripts/receptury-kuzni-2.js): przepisy w kg z otwartymi ladunkami jak drewno/wegiel, przetop ze stratami bez zysku;
- m96-jakosc2 (wf_f0791615-a57): limit jakosci na rodzaj przedmiotu (L 1-6, MW 6-60 losowane raz na kampanie).
Scalanie: cd zb/n25-calosc; git merge --no-ff --no-commit w-toku/<galaz>; konflikty: McmSettings.cs -> checkout --ours, reszta python -I rozwiaz.py <plik>; commit;
na koncu python -I tools/gen_mcm.py, build 5 modow (dotnet build <Mod>/<Mod>.csproj -c Release -v q --nologo -p:GameLibs=<lancuch/libs>), sprawdz_narzedzie.py.
Test: CrashScribe-at = zb/n22-at + git merge n25 HEAD, build CrashScribe; katalog DLL test/n25 (Armoury, CrashScribe, GrandTourney, RealisticCaptivity, ForgeView z n25
- niezmienione mody kopiowac z ostatnio wgranych); skrypt wzor testy-95.ps1 (podmienic katalogi, nazwy, Tree); progi: python -I tools/sprawdz_logi.py --grupa <g> <log>
(wszystkie grupy + m96*) wobec poprzednich biegow (scratchpad progi/). Gra musi byc zamknieta (tasklist). Wgranie: wgraj-v5.ps1 -DllDir test/n25 -Tree zb/n25-calosc
-Tag 2026-10-21-wgranie96; potem safely_exited = 1 w Documents/.../Configs/engine_config.txt; CHANGELOG (w drzewie n25), STAN-PRAC, push.

## Dalej (kolejnosc Jeffa)
1. WGRANIE 96 (wyzej).  2. m97-wioski (wf_a63458ba-100 w toku: wioski poboczne jak wsie - wjazd, inwestycje, handel, nocleg/oboz, rabunek osobno, okup)
+ OBOZ etap 1 (oboz 21-3, wejscie i chodzenie po obozie - wzor Practice Drills public domain w zb/practicedrills-1.3, woz + kon juczny, siodla) + surowce czardrzewo
(zza Muru) i smocza kosc (Valyria/Volantis/Qarth) w naszym module -> WGRANIE 97.  3. Tablica ogloszen + plotki (turnieje, swaty, piwo wedlug Uroku, kac) -> 98.
4. OPTYMALIZACJA na koncu (docs/PLAN-OPTYMALIZACJA-NOC.md).  5. Raport rano: docs/RAPORT-NOCNY-2026-10-21.md (prosty jezyk; co wgrane i sprawdzone, co nie).

## Zasady (pamiec i CLAUDE.md)
- Wgrywac tylko po udanym autotescie, nie gdy Jeff gra; kopie .bak + D:; nigdy nie ladowac zapisu Ironman Jeffa w autotescie.
- Kazda zmiana pod kluczem MCM; Armoury.json Jeffa nadpisuje domyslne (sprawdzic klucze przy wgraniu).
- Bash heredoc gubi backslashe w pythonie - sciezki przez chr(92). Workflow - skrypty bez CR.
- Limity: pauza przy 90% 5 h; tydzien do ok. 97-99%, potem zaktualizowac TEN plik i STAN-PRAC i zakonczyc.

## Dopisek 17:45
- Obroncy wiosek pobocznych (Jeff): krok 2 po m97-wioski - obroncy z milicji wsi glownej, napad = bitwa z tymczasowym oddzialem, polegli z milicji, bez stalych partii.
- 18:00 PDT m96-wzory2 44e724df SCALONE do n25 (naprawa zapisu #m96seedfix2: lowland_yew_bow t5 zdjety, +3 pkt; drabina tierow PatternDrawOneTierUp - nowy wzor najwyzej 1 tier ponad najlepszy znany). Przed pierwszym wczytaniem kampanii Jeffa na 96 - kopia Ironman (jest: D:/Backup-Bannerlord/zapisy-jeffa/2026-10-20-wieczor).
- 18:20 PDT m96-wies d30ab829 SCALONE do n25 3ce70794 (+gen_mcm: Armoury 2170, GT 44, RC 115; 5 modow build 0): menu wsi (VillageMenuTidy - Village affairs, Look for paid work; wrogie czyny na wierzchu), komunikaty platnosci z powodem (PlayerPaymentReasons w Armoury/RC/GT). "You paid 2" Jeffa = DOBOWY WYNAJEM KUZNI w Seagard (DayPass x5 przy przetopie z odsiadka BK), nie nocleg. Naprawiony krytyczny blad GT PayPurse (pula turnieju dwa razy). Czekaja na 96: m96-receptury (wf_3eb8bfa3-879), m96-jakosc2 (wf_f0791615-a57).
- 18:35 PDT m96-jakosc2 4490e2f3 SCALONE do n25 (limit na rodzaj: L 1-6, MW 1-60 losowane raz, arm_qualitycaps w zapisie; nadmiar na polkach od razu do zwyklej jakosci). Czeka tylko m96-receptury (wf_3eb8bfa3-879), potem testy i WGRANIE 96.
- 18:40 PDT start OBOZ etap 1: galaz w-toku/m97-oboz od n25 2f38304b (drzewo zb/m97-oboz) - oboz 21-3, scena obozu (wejscie/chodzenie, namioty ROT wedlug ludzi, ogniska; wzor zb/practicedrills-1.3), woz + kon juczny, siodla bojowe/transportowe. Limity 18:40: tydzien 83%, 5 h 3%.
- 18:45 PDT W TOKU: m96-receptury wf_3eb8bfa3-879 (ostatnia do 96), m97-wioski wf_a63458ba-100, m97-oboz wf_281158a5-2e3 (oboz-etap-1). Po receptury: scalic do n25 -> testy (testy-95.ps1 jako wzor) -> WGRANIE 96. Potem: obroncy wiosek pobocznych (krok 2), surowce czardrzewo/smocza kosc, scalenie m97 -> test -> WGRANIE 97; tablica+plotki; optymalizacja; RAPORT-NOCNY-2026-10-21.md.- 19:05 PDT KANDYDAT 96 KOMPLETNY: n25-calosc 03e82bcb (+ m96-receptury 4d2998f9: RecipesByMass, kg + otwarte ladunki SmithStock, przetop max 50% metalu przepisu) - 2173 ustawien Armoury,
  GT 44, RC 115, 5 modow build 0, sprawdz_narzedzie OK. DLL testu: test/n96 (Armoury 6e17caa7, CrashScribe 5c81079b, ForgeView f7257c22, GrandTourney 40689947,
  RealisticCaptivity 634ecbb4), CrashScribe-at-n96.dll (zb/n22-at + 03e82bcb). TESTY W TOKU od 19:05: testy-96.ps1 (96-bitwa, 96-nowa 180 dob, 96-160), ok. 1.5-2 h.
  Po testach: progi (sprawdz_logi wszystkie grupy + m96*), potem wgraj-v5 -DllDir test/n96 -Tree zb/n25-calosc -Tag 2026-10-21-wgranie96, safely_exited = 1, CHANGELOG
  (wpis 96 w drzewie n25), STAN-PRAC ### 103, push galezi (lancuch -> repo -> GitHub).
- 17:42 PDT (uwaga: wczesniejsze godziny 18:xx/19:05 w tych wpisach byly zle - zegar komputera to 17:4x) pierwszy start testow 96 STOP kod 3: znacznik w-toku.json po przerwanym tescie 95a (14:31) - odlozony do autotest-kopie/w-toku.json.nieaktualny-95a-1739 (gra ma sprawdzone 95a z wgraj-v5); flaga armoury-ks-przerwij.flag z 95-160 (12:42, "ceny poza pasmem 19 z 24" w D+7 po ks-auto) odlozona do autotest-kopie. Testy 96 ponownie od 17:39 (PID 748, bitwa -> nowa 180 -> 160).
- 17:50 m97-wioski GOTOWE (wf_a63458ba-100): w-toku/m97-wioski c578f4e7 (baza 25f24c17), klucz SideVillagesEnterable (TAK), build 0, 2 recenzje + poprawki. Obrona wiosek pobocznych = cala milicja wsi glownej + oddzialy w niej (bez osobnych garnizonow - odpowiedz na pytanie Jeffa o obroncow: rabunek/okup wioski dopiero po rozbiciu milicji bitwa przy wsi glownej albo wieksza sila). Rabunek wsi glownej przez gracza pali tylko jej osady; kazda wioska osobno z jej menu; okup wioski chroni ja w lancuchu krolestwa. Grupa sprawdz_logi --grupa m97wioski. Pliki wspolne do scalenia: VillageBook.cs, DistrictRaid.cs, RaidTribute.cs, TownInns.Road.cs, Settings/McmSettings (gen_mcm), sprawdz_logi.py. Krok 2 (osobna bitwa w wiosce pobocznej) - NIE robiony (limit; obecna obrona milicja wsi glownej wystarcza) - do decyzji Jeffa.
- 17:50 m97-surowce START (wf_9c2c1406-af7): drzewo zb/m97-surowce z n25-calosc 03e82bcb - czardrzewo (za Murem) i smocza kosc (Essos/Valyria) jako rzadkie towary z doplywem w regionie, luki z RareBowsIds do wykucia z surowcem (klucz RareBowsNeedMaterial). Uwaga przy wgraniu: jesli dojdzie nowy plik XML w Armoury/ModuleData - dopisac do wgraj-v5.ps1 i bieg-bitwa (dzis kopiuja DLL, SubModule.xml, arm_*.tsv).
- 17:50 limit tygodnia 84% (5h: 7%).
- 18:20 JEFF: "dopisz do kolejki po obozie" - osobna bitwa w wiosce pobocznej (obroncy wioski). Kolejka: oboz (m97-oboz, w toku) -> BITWA W WIOSCE POBOCZNEJ (galaz w-toku/m97-wioski, drzewo zb/m97-wioski, krok 2: przy rabunku/okupie wioski pobocznej bez rozbitej milicji - bitwa z czescia milicji wsi glownej jako tymczasowa druzyna przy wiosce; po bitwie druzyna wraca/znika, straty schodza z milicji wsi glownej; pod kluczem MCM) -> scalenie m97 -> test -> WGRANIE 97. Koszt ok. 2-3% tygodnia.
