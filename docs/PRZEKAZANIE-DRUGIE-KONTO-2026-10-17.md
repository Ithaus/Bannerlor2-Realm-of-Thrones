# PRZEKAZANIE NA DRUGIE KONTO - 17.10.2026 (stan ok. 13:00 zegara komp. PDT)

Powod: limit tygodnia konta 94% (zasada Jeffa: przy 99% przekazanie). Punkt startu dla Claude na drugim koncie. Czytaj po kolei:
1. `CLAUDE.md` w repo.
2. Ten plik.
3. `docs/STAN-PRAC.md` - wpisy ### 61-75 (noc 16/17 i dzien 17.10).
4. Projekty: `docs/PROJEKT-DANINY-SREDNIOWIECZNE-2026-10-17.md`, `docs/PLAN-KS-RUNDA4-2026-10-17.md`, `docs/PROJEKT-KALIBRACJA-STARTU-2026-10-16.md`,
   `docs/PROJEKT-ZYCIE-CODZIENNE-2026-10-16.md`, `docs/PROJEKT-INWESTYCJE-OKREGU-2026-10-16.md`.

Godziny w dokumentach = zegar komputera (PDT, UTC-7). Jeff jest w Polsce (CEST = PDT + 9 h).

---

## 1. Co jest w grze: WGRANIE 75 (17.10 ok. 12:40)

- Kod: galaz **`w-toku/noc-wgranie-75`** `fa4416f5` (na GitHub). = WGRANIE 74 (`fe1b271e`) + daniny sredniowieczne wsi (DS F1).
- Armoury.dll **ce1e0343** (bylo 1d4daf68); CrashScribe da755a8a, RealisticCaptivity 7e0e462e, GrandTourney 035878c4, ForgeView 19aa052a; pliki danych jak w 74
  (arm_map_villages.tsv v5 crc 68BB06AD, lore v5, oba _v4, arm_houses_canon.tsv).
- Kopie: `*.bak-2026-10-17-wgranie75-przed` obok plikow i `D:\Backup-Bannerlord\wgrane\2026-10-17-wgranie75-przed`.
- Zapis Jeffa przed wgraniem: `D:\Backup-Bannerlord\zapisy-jeffa\IronmanjCg6MOpv28iN-2026-10-17-przed-wgraniem75.sav` (md5 2ad40e43...).
- Wlaczone mody bitewne: ROTBattleAtmosphere v1.5.0 i ImmersiveBattlefields v3.6.2 (koniec LauncherData), MoreBloodPoolMod WYLACZONY (crash przy ciosie).
- DS F1 w grze: klucz MCM Armoury "Dues Medieval" (domyslnie TAK). Rampa 28 dob od pierwszej doby z nowa DLL. DsTowns (F3) WYL. KS (kalibracja startu) NIE ma w tej galezi.

## 2. Jeff gra - kampania IRONMAN

- Zapis `Documents\Mount and Blade II Bannerlord\Game Saves\IronmanjCg6MOpv28iN.sav`. NIGDY nie wczytywac go w autotescie (autotest tylko `autotest-F-d240`).
- Gdy gra dziala (`tasklist | grep -i bannerlord`) - nic nie wgrywac, nie testowac.
- Przed autotestem skopiowac logi ostatniej sesji Jeffa (Armoury trzyma 12 logow) do `D:\Backup-Bannerlord\logi-jeffa\<data>`.
- Stala zgoda Jeffa: wgrywac po udanym autotescie z kopia. ALE duze zmiany ekonomii (DS F3, KS) - zapytac Jeffa przed wgraniem (tak bylo przy WGRANIU 75).
- Pierwsze wczytanie WGRANIA 75 u Jeffa: sprawdzic w logu linie "Daniny wsi (DS)" (start rampy), "Korona: wplywy dnia (165)" (zwrot zoldu 100%), "Przeplywy osad (kiesy wsi)".

## 3. Drzewa, galezie, narzedzia

Klon roboczy: `C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\3cf3e0ac-5529-4b68-a794-0edec69cfda7\scratchpad\lancuch`
(origin = repo `C:\Users\GAME\Bannerlor2-Realm-of-Thrones`, ktorego origin to GitHub Ithaus/Bannerlor2-Realm-of-Thrones). Worktree'y w
`C:\Users\GAME\AppData\Local\Temp\claude\C--Users-GAME-Bannerlor2-Realm-of-Thrones\ae222cb7-a7c2-473a-8fc1-1dc1d6f688e3\scratchpad\zb\`:

| worktree | galaz | HEAD | co |
|---|---|---|---|
| n-ds | w-toku/noc-wgranie-75 (od w-toku/noc-ds 0a5842e7) | fa4416f5 | W GRZE. DS F1 + F3 (F3 wylaczone kluczem DsTowns) |
| n-ks | w-toku/noc-ks3 | 5c895704 | KS runda 3 final (NIE wgrane; baza fe1b271e = 74, BEZ DS) |
| int | w-toku/zbior-e8 | ed949c36 | 74 + KS r3 (NIE wgrane) |
| t-r3 | detached | 909aba37 | KS r3 posrednie (tylko test) |

Wszystkie galezie wypchniete na GitHub (17.10 13:00). Jesli scratchpad zniknie: `git worktree add` z galezi w repo.

Build: `dotnet build Armoury/Armoury.csproj -c Release -v q --nologo "-p:GameLibs=<lancuch>/libs"` (libs: `...\3cf3e0ac-...\scratchpad\lancuch\libs`) - rc 0;
po zmianie Settings.cs: `python -I tools/gen_mcm.py`.

Autotest (kopie w repo `tools/autotest-2026-10-17/` i `D:\Backup-Bannerlord\scratch-2026-10-17\`):
```
powershell -NoProfile -ExecutionPolicy Bypass -File bieg-bitwa.ps1 -DllDir <katalog 5 DLL> -CsAt CrashScribe-at-bitwa.dll -Tree <drzewo zrodel>
  -Out <plik.out.txt> -Days 120 -LoadSave autotest-F-d240 -Battles 1 -BattleSec 120 -SwitchWaitSec 720
```
Kod wyjscia: 0 OK, 1 nie dotarl, 2 stop, 3 przywracanie nieudane, 4 misje nie sprawdzone. Katalog DLL: kopia `test/fin68` (CrashScribe da755a8a itd.) + nowy Armoury.dll.
Wgranie: `wgraj-v5.ps1 -DllDir <katalog> -Tree <drzewo> -Tag <data-wgranieNN>` (kopie + md5).

Pliki-przelaczniki autotestu (Documents\Mount and Blade II Bannerlord\CrashScribe\) - dzialaja TYLKO w trybie autotestu, USUWAC po tescie:
- `armoury-ks-wlacz.flag` - KS Apply (galezie z KS); `armoury-ks-przerwij.flag` - pisze KS przy D+7 poza pasmem (tylko sygnal).
- `armoury-ds-wlacz.flag` - DS F1 (galaz noc-ds; w WGRANIU 75 i tak domyslnie wlaczone). Dla DsTowns (F3) przelacznika jeszcze NIE MA - dopisac jak w DsVillage.AutotestFlag.

Progi: `python -I tools/sprawdz_logi.py --grupa <g> <Armoury.log>`; grupy: nks (KS, w galezi n-ks/int), nds (DS, w n-ds; `--baza-doby D:/Backup-Bannerlord/logi-testow/Armoury-2026-10-16_18-23-34.log`
= bieg WGRANIA 74 bez DS), nzc0, nzc2, nbitwa, nturniej, nkarczmy, etap7.
Logi testow: `D:\Backup-Bannerlord\logi-testow\` (KS r1 22-15-00, r2 01-15-30, r3 06-07-51, r3 final 09-07-09, DS 11-47-55, smoke 75 12-30-06).

Symulatory (kopie zrodel w repo `tools/ekonomia-symulatory-2026-10-17/`, pelne z danymi `D:\Backup-Bannerlord\scratch-2026-10-17\`):
- `ksim/` - symulator rynku 97 miast (sim/r3port.py itd.); `kmoney/` - uprzaz Planu KS w C# (h, h3port, h4); `daniny/final/` - model pieniadza DS (dsmodel.py, dsparse.py).

## 4. Co dalej - kolejnosc (ZAKRES: wszystko ZC0-ZC7 + kalibracja; Jeff: "chce abys to skonczyl i nie przerywal pracy")

1. **DS F3 - miasta, zamki, korona, kredyt** (kod jest na n-ds, klucz DsTowns WYL.; projekt rozdz. 6-8):
   - ZMIANA wiazaca (zasada Jeffa "dlugi trzeba splacac"): zaliczki mieszczan L1 NIE sa darowane po 90 dniach pokoju - splata z wplywow w pokoju; zwloka = miasta-wierzyciele nie pozyczaja,
     lojalnosc spada, dlug zostaje (dzis w kodzie: darowanie z lojalnoscia -10 - zmienic).
   - Przelacznik autotestu dla DsTowns; symulator pieniadza F3 (dynamika nadan g co kwartal, firmy, pomoc od sprzedazy, dziesiata od ruchomosci); test 120 dob (okno pomiaru firm 28 dob
     + rampa 28 dob); bramki nds ND3-*; pytac Jeffa przed wgraniem.
   - Model statyczny: korona 82-87% dzisiejszych wplywow, g = 2 w 13-19 krolestwach, renty 180 spadaja o ok. polowe, luki 1.6-12.4 tys./d pokrywa skarbiec -> mieszczanie -> Bank.
2. **KS runda 4** (plan `docs/PLAN-KS-RUNDA4-2026-10-17.md` + dopiski STAN-PRAC 72), NA BAZIE WGRANIA 75 (scalic noc-ks3 do linii noc-wgranie-75; ryzyko: >10 wspolnych plikow):
   - budzet wsi z praw DS (`DsVillage.BudgetShare`) zamiast alfy netto (alfa netto zapadala sie do 0 w petli - pobory resztowe, STAN-PRAC 68);
   - KS-H kupcy jedzenia na wlasnym kapitale (miasto placi przy dostawie, reszta = rachunek; 30 dni zwloki = brak dostaw);
   - rzeznie miast wedlug Planu (dzis 6/2/1 miesa na sztuke zamiast 10/6/2 - mieso 50% planu);
   - B2 FoodSubstitute wylaczony przy KS (sztuczny popyt: ser +45%, winogrona +33%, mieso +29%);
   - "najpierw wyzywic siebie" + prawdziwe plony hodowli/ryb/polowan w zimnym klimacie (Wolni Ludzie 0.047 kg jedzenia/glowe, Nocna Straz to samo, Polnoc trapper 638k) - jedna tabela swiata;
   - DECYZJE JEFFA: glod Polnocy/Wolnych Ludzi/Nocnej Strazy = "dowoz jak w ksiazkach" (kupcy i statki z poludnia, danina zbozowa lordow Polnocy dla Muru, glod pcha Wolnych Ludzi
     do najazdow, reszta = prawdziwy glod); biedni Essos = "tanszy koszyk" (chleb, kasza, ryby zamiast miesa i wina; place rosna przy braku rak);
   - rynek wsi BK wedlug potrzeb (sol, zelazo, sukno/len, garnki; zbytki tylko bogate wsie) i tylko z nadwyzki (Jeff "ok");
   - bez darmowej podlogi spichlerza; koszt doby (KS <= 3 ms, z hakami <= 10 ms, KS-H <= 1.5 ms); niestabilnosc okregu Arbor.
   - Najpierw symulator (S-0 kalibracja do gry r3 final, S-1 Plan h5, S-2 prototypy), potem 1 test 120 dob. Oczekiwane 8-12 z 53 nks niezaliczonych (r3 final: 24).
3. **ZC6/ZC7** (mobilnosc, wolnosc, blizny wojny, ProsperityLink) - kod jest (WGRANIE 74), klucze WYL.; wlaczyc po KS, test, wgranie.
4. Kolejka pozniej: inwestycje w okreg + specjalizacja wiosek (projekt zatwierdzony, AI nie inwestuje), przysiolki na pustkowiach, zajazdy + plotki, +15% na drogach ROT,
   uchodzcy do miast (czeka na slowo Jeffa), limit 160 tekstur proporczykow, mapa CK3 (ABUV Expanded, Nexus 13460 - Jeff pobiera sam; kod przeczytac, test z bitwa i mapa ROT).

## 5. Lekcje z 16/17.10

- Bilans swiata KS nie wystarcza - towar musi dojechac do miast; kazda poprawke KS najpierw na symulatorze 97 miast, dopiero potem 40-minutowy test w grze.
- Pobory "od nadwyzki ponad zapas" zjadaja kazda monete - Plan widzi netto 0 (alfa w petli). Jeff: daniny jak w sredniowieczu, bez sztucznych regul.
- Zwolnione pieniadze wsi bez celu chomikuja sie (+1.3%/d) - potrzebny odplyw z towarem lub praca (roboty gminy).
- Workflow L zjada 2-7 mln tokenow (2-5% tygodnia) - przy limicie tygodnia planowac tylko jeden naraz.
- Bash heredoc z pythonem: backslash przed literami ginie lub robi znak sterujacy (\a = 0x07) - sciezki przez `/` albo chr(92); sprawdzac plik po zapisie.
- Jeff nie chce agentow do prostych pytan - proste sprawdzenia wprost z logow.
