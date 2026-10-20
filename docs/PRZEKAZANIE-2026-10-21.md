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
