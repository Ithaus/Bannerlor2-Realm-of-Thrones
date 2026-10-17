# PRZEKAZANIE NA PRACE NOCNA (drugie konto) - 17.10.2026, ok. 16:25 zegara komp. (PDT) = 01:25 CEST

Jeff (doslownie): "przerwij test, zrob dokument przekazania i puscimy reszte prac na innym koncie praca nocna".
Ten plik jest JEDYNYM punktem startu. Czytaj: `CLAUDE.md` w repo -> ten plik -> `docs/STAN-PRAC.md` wpisy ### 61-81 -> projekty z rozdz. 6.
Godziny = zegar komputera (PDT, UTC-7); Jeff w Polsce (CEST = PDT + 9 h). Jezyk z Jeffem: polski, prosto, slowami gracza.

---

## 1. Stan gry Jeffa: WGRANIE 76 - NIE RUSZAC bez testu

- Galaz **`w-toku/noc-wgranie-76`** `725150ad` (GitHub). Armoury.dll **d84f7b66**, CrashScribe da755a8a, RealisticCaptivity 7e0e462e, GrandTourney 035878c4, ForgeView 19aa052a.
  Pliki danych jak WGRANIE 74 (arm_map_villages.tsv v5 crc 68BB06AD, lore v5, oba _v4, arm_houses_canon.tsv). arm_ks_land.tsv w grze NIE MA (KS nie wgrany).
- = WGRANIE 74 + **DS F1** (daniny sredniowieczne wsi, klucz MCM "Dues Medieval") + **DS F3** (miasta, zamki, korona, podatki wojenne, kredyt; klucz "Ds Towns") + dlugi korony
  u miast bez umorzenia (klucz CrownTownLoanNoWriteOff). W grze Jeffa: rampa F1 28 dob od pierwszego wczytania -> okno pomiaru firm 28 dob -> rampa F3 28 dob -> sesja nadan "po rampie".
- Kopie: `*.bak-2026-10-17-wgranie76-przed` obok plikow, `D:\Backup-Bannerlord\wgrane\2026-10-17-wgranie76-przed`; zapis Jeffa
  `D:\Backup-Bannerlord\zapisy-jeffa\IronmanjCg6MOpv28iN-2026-10-17-przed-wgraniem76.sav` (md5 2ad40e43; Jeff nie gral od 17.10 06:05).
- 16:20 PDT test fin79 PRZERWANY na prosbe Jeffa: przywrocone `autotest-bitwa.ps1 -RestoreOnly` (Armoury d84f7b66, CrashScribe da755a8a, arm_ks_land.tsv usuniety),
  engine_config.txt z kopii sprzed testu, flagi usuniete. Gra czysta.
- Mody bitewne: ROTBattleAtmosphere v1.5.0, ImmersiveBattlefields v3.6.2 wlaczone; MoreBloodPoolMod WYLACZONY (crash).

## 2. Zasady (wiazace)

- NIGDY nie wczytywac zapisu Jeffa w autotescie (tylko `autotest-F-d240`). Gdy gra dziala (`tasklist | grep -i bannerlord`) - nic nie wgrywac i nie testowac.
- Przed autotestem: kopia logow ostatniej sesji Jeffa (Armoury trzyma 12 logow) do `D:\Backup-Bannerlord\logi-jeffa\<data>`.
- Stala zgoda: wgrywac po udanym autotescie z kopia. ALE duze zmiany ekonomii (KS) - zapytac Jeffa przed wgraniem (tak bylo przy 75 i 76).
- Testy 120 dob BEZ bitwy (Jeff 17.10); bitwa tylko w tescie 5 dob dokladnej DLL przed wgraniem (`-Battles 1 -BattleSec 90`).
- Ekonomia (Jeff): zamknieta (kazda moneta i towar maja platnika i odbiorce), dynamiczna (podaz/popyt), start rynku 95-105% kazdego towaru, produkcja tylko z prawdziwych rak
  i ziemi (jedna tabela swiata, bez mnoznikow okregu), daniny i oplaty jak w sredniowieczu (bez poborow resztowych), krol nigdy bez pieniedzy na armie (umiarkowane podatki
  wojenne + pozyczki mieszczan i Banku; dlugi trzeba splacac), zwrot zoldu nietykalny. Receptury przetworow wedlug wagi (realny uzysk).
- Decyzje Jeffa 17.10: glod Polnocy/Wolnych Ludzi/Nocnej Strazy = "dowoz jak w ksiazkach" (kupcy i statki z poludnia, danina zbozowa lordow Polnocy dla Muru, glod pcha
  Wolnych Ludzi do najazdow, reszta = prawdziwy glod); biedni Essos = "tanszy koszyk" (chleb, kasza, ryby zamiast miesa i wina; place rosna przy braku rak);
  rynek wsi BK wedlug potrzeb (sol, zelazo, sukno/len, garnki; zbytki tylko bogate) i tylko z nadwyzki; Polnoc/za Murem - najpierw wyzywic siebie (hodowla, ryby, owies).
- Limity konta: pauza przy 90% limitu 5 h; przy 99% tygodnia - przekazanie (STAN-PRAC + push). Workflow L kosztuje 2-7% tygodnia - jeden naraz.

## 3. Galezie i drzewa

Klon: `C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\3cf3e0ac-5529-4b68-a794-0edec69cfda7\scratchpad\lancuch`
(origin = repo `C:\Users\GAME\Bannerlor2-Realm-of-Thrones` -> GitHub Ithaus/Bannerlor2-Realm-of-Thrones). Worktree'y w
`C:\Users\GAME\AppData\Local\Temp\claude\C--Users-GAME-Bannerlor2-Realm-of-Thrones\ae222cb7-a7c2-473a-8fc1-1dc1d6f688e3\scratchpad\zb\`. Jesli zniknely - `git worktree add` z GitHub.

| worktree | galaz | HEAD | co |
|---|---|---|---|
| n-ds | w-toku/noc-wgranie-76 | 725150ad | W GRZE (DS F1 + F3) |
| **n-ks4** | **w-toku/noc-ks4** | **efbbf0dc** | **KS runda 4 w toku** (od 76 + scalony KS r3) - TU PRACOWAC |
| n-ks | w-toku/noc-ks3 | 5c895704 | KS runda 3 final (historia, scalona do noc-ks4) |
| int | w-toku/zbior-e8 | ed949c36 | 74 + KS r3 (historia) |

Commity noc-ks4 (od 725150ad): e91f8063 scalenie noc-ks3; 5d56ff24 alfa wsi z praw DS + B2 wyl. przy KS; a6c56999 rzeznie (zastapione); b0d3914e + bc557e9d
**KsRecipes** (receptury jedzenia i surowcow wedlug wagi, klucz KsRealRecipes); efbbf0dc skrzynia wsi w drabinie 2 + spichlerz bez podlogi (klucz KsGranaryNoFloor).

Build: `dotnet build Armoury/Armoury.csproj -c Release -v q --nologo "-p:GameLibs=<lancuch>/libs"` (rc 0); po zmianie Settings.cs `python -I tools/gen_mcm.py`.

## 4. Narzedzia testu

Skrypty (repo `tools/autotest-2026-10-17/`, kopia `D:\Backup-Bannerlord\scratch-2026-10-17\`; robocze w scratchpad sesji `...\6ba92574-...\scratchpad\test\`):
```
powershell -NoProfile -ExecutionPolicy Bypass -File bieg-bitwa.ps1 -DllDir <katalog 5 DLL> -CsAt CrashScribe-at-bitwa.dll -Tree <drzewo>
  -Out <plik.out.txt> -Days 120 -LoadSave autotest-F-d240 -SwitchWaitSec 720
```
- Katalog DLL = kopia `test/fin68` (CrashScribe da755a8a, RC, GT, ForgeView) + nowy Armoury.dll. Kod wyjscia 0 OK (4 = bez bitwy - dla testu ekonomii OK).
- Przerwany bieg: zamknac gre, `autotest-bitwa.ps1 -RestoreOnly`, porownac Configs z kopia biegu (`%USERPROFILE%\autotest-kopie\<bieg>\Configs`).
- Wgranie: `wgraj-v5.ps1 -DllDir <katalog> -Tree <drzewo> -Tag <data-wgranieNN>`; przed nim test 5 dob + bitwa dokladnej DLL.
- Pliki-przelaczniki (Documents\Mount and Blade II Bannerlord\CrashScribe\, tylko autotest, USUWAC po tescie): `armoury-ks-wlacz.flag` (KS Apply), `armoury-ds-wlacz.flag`,
  `armoury-dst-wlacz.flag` (DS - w 76 i tak domyslnie wlaczone); `armoury-ks-przerwij.flag` pisze KS przy D+7 (tylko sygnal).
- Progi: `python -I tools/sprawdz_logi.py --grupa nks <log>` i `--grupa nds --baza-doby D:/Backup-Bannerlord/logi-testow/Armoury-2026-10-16_18-23-34.log <log>`; nzc0, nzc2.
- Logi testow: `D:\Backup-Bannerlord\logi-testow\` (KS r3 final 09-07-09, DS 11-47-55, F3 12-57-44 / 13-37-03 / 14-24-13, KS r4 15-37-31).
- Symulatory: repo `tools/ekonomia-symulatory-2026-10-17/` (ksim - rynek 97 miast, kmoney - uprzaz Planu, daniny/final - model pieniadza DS).

## 5. CO ZOSTALO - kolejnosc (zakres: wszystko ZC0-ZC7 + kalibracja; "skoncz moda")

**A. KS runda 4 (galaz noc-ks4) - cel: nks wszystkie progi (dzis 24 z 53 nie), nds 41/41.**
Ostatni pelny test: fin78 (Armoury-2026-10-17_15-37-31.log, stan a6c56999): alfa z praw stabilna 0.29-0.31, mieso 1.1-1.4, zboze 1.02; NIE: kiesy wsi -0.37%/d,
podloga spichlerza 99% wsi, ser/maslo 2-2.2x, sol na suficie 32-39% miast, KS-H kasa celu za mala, korytarze 69/80 < 0.8, Essos sterowanie x0.85, koszt 4.57/14.31/1.90 ms,
korona 0.945, panowie 0.948 (prog 0.95).
1. **Pierwszy krok: test 120 dob stanu efbbf0dc** (KsRecipes + skrzynia + bez podlogi) - przerwany fin79. Flaga armoury-ks-wlacz.flag, drzewo n-ks4, bez bitwy.
   Sprawdzic w logu linie "KS: receptury jedzenia wedlug wagi" (lista zmienionych linii) i glod (NKS-15, zgony; spichlerze).
2. **KS-H na kredycie kupcow** (KsFoodCarts.cs Ship ~600, Back ~447, Arrivals ~400; KsFoodCore.Decide budzet ~70): przy wysylce towar bez zaplaty, fracht z kasy ZRODLA;
   przy dostawie kasa celu placi, ile ma, reszta = rachunek (zapis nowej sekcji w arm_ks_kupcy); splata codziennie z kasy celu ponad zapas kupcow; zwloka > 30 dni = brak
   nowych dostaw; budzet KsFoodCore = limit kredytu (np. 2 x kasa celu) - rachunki otwarte. Klucz MCM. Back: woz bez zaplaty - towar wraca bez zwrotu.
3. **Najpierw wyzywic siebie + zimny klimat**: Plan (StartCalibration.Core / Rule) - okreg najpierw stawia rece przy jedzeniu o najwiekszych kcal na reke na swojej ziemi,
   nadwyzka rak na targ; ziemia snieg (arm_ks_land.tsv + LandEff) - hodowla bydla/owiec/koz, ryby, owies/jeczmien, polowanie na pustkowiach z prawdziwymi plonami
   (Wolni Ludzie dzis 0.047 kg jedzenia/glowe, Nocna Straz tak samo; Polnoc trapper 638k ludzi).
4. **Dowoz na Polnoc + danina zbozowa dla Muru** (decyzja Jeffa): KS-H statki do White Harbor/Eastwatch; lordowie Polnocy oddaja Murowi czesc zboza w naturze (zamkniety
   przeplyw: spichlerze/okregi lordow -> Nocna Straz); glod Wolnych Ludzi -> najazdy (bandy).
5. **Tanszy koszyk biednych Essos** (decyzja Jeffa): drabina koszyka klas R/P - substytucja na tansze kcal; place rosna przy braku rak.
6. **Rynek wsi BK wedlug potrzeb** (BkSeal34): tylko sol, zelazo/narzedzia, len/sukno, garnki; zbytki tylko bogate wsie; tylko z nadwyzki ponad skrzynie (DS).
7. **Koszt doby** (KS <= 3 ms, z hakami <= 10 ms, KS-H <= 1.5 ms) - plan w `docs/PLAN-KS-RUNDA4-2026-10-17.md` (rozpoznanie kosztu).
8. Sprawdzac kazdy krok w grze 120 dob (bez bitwy); po przejsciu: test 5 dob + bitwa dokladnej DLL, ZAPYTAC Jeffa, wgrac (WGRANIE 77).
   Arm KS w grze Jeffa: LandGateReady/Arm/Confirm (StartCalibration) - Jeff musi wiedziec, ze KS przelaczy rynek w jego zapisie (rampa D..D+14).

**B. ZC6/ZC7** (mobilnosc spoleczna, wolnosc, blizny wojny, ProsperityLink) - kod jest od WGRANIA 74, klucze WYL.; wlaczyc po KS, test, wgranie.

**C. Kolejka pozniej**: inwestycje w okreg + specjalizacja wiosek (projekt zatwierdzony, AI nie inwestuje), przysiolki na pustkowiach, zajazdy + plotki (bandy, kryjowki,
skarby), +15% predkosci na drogach ROT (dane geo w scratch), uchodzcy do miast (czeka na slowo Jeffa), limit 160 tekstur proporczykow, mapa CK3 (ABUV Expanded, Nexus 13460
- Jeff pobiera sam; edycja zwykla, nie Bellum Civile - gryzie sie z Banner Kings; najpierw przeczytac kod i test z mapa ROT).

## 6. Dokumenty projektow

`docs/PROJEKT-DANINY-SREDNIOWIECZNE-2026-10-17.md`, `docs/PLAN-KS-RUNDA4-2026-10-17.md`, `docs/PROJEKT-KALIBRACJA-STARTU-2026-10-16.md`, `docs/PROJEKT-ZYCIE-CODZIENNE-2026-10-16.md`,
`docs/PROJEKT-INWESTYCJE-OKREGU-2026-10-16.md`, `docs/DANINY-TERAZ-KROLESTWA-2026-10-16.csv`, `docs/RAPORT-NOCNY-2026-10-17.md` (dla Jeffa), `docs/PRZEKAZANIE-DRUGIE-KONTO-2026-10-17.md` (13:00).

## 7. Lekcje 16/17.10

- Kazda poprawke KS sprawdzac w grze 120 dob (40 min, prawie bez kosztu limitu); workflow tylko do duzych projektow (kosztuja 2-7% tygodnia).
- Bilans swiata nie wystarcza - towar musi dojechac do miast; prognozy z okna pomiaru zawyzaja stare pobory i jednorazowe lupy (mediana zamiast sredniej).
- Pobory "od nadwyzki ponad zapas" zjadaja kazda monete - alfa w petli; zwolnione pieniadze bez celu chomikuja sie (roboty gminy).
- Receptury gry nie sa fizyczne (wszystkie towary 10 kg, oliwa 1:1/1:2) - przetwory wedlug wagi jak Plan.
- Bash heredoc z pythonem gubi backslashe (\a -> 0x07) - skrypty pisac narzedziem Write albo sciezki przez `/`; sprawdzac znaki sterujace.
- Production (WorkshopType) to struct - `p == null` nie kompiluje; listy Inputs/Outputs to MBList (List) - mozna podmieniac liczby.
