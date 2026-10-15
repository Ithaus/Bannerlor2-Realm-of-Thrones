# PRZEKAZANIE NA DRUGIE KONTO - 15.10.2026 (stan ok. 11:10 UTC)

Ten dokument jest punktem startu dla Claude na drugim koncie. Czytaj po kolei:
1. `CLAUDE.md` w repo (zasady pracy z Jeffem, srodowisko, build).
2. Ten plik.
3. `docs/PRZEKAZANIE-2026-10-08.md` - rozdzialy 34-40 (szczegoly kazdego wgrania z 15.10).
4. `docs/STAN-PRAC.md` - ostatnie wpisy (dziennik pracy).

Repo: `C:\Users\GAME\Bannerlor2-Realm-of-Thrones`, galaz dokumentow `claude/bannerlord-rot-setup-o75kvo`.
Kod: galaz integracyjna **`w-toku/zbior-e8`** (wszystkie zmiany scalone, wypchniete na GitHub).

---

## 1. Co jest teraz w grze

**WGRANIE 61** = `w-toku/zbior-e8` **39f8fe72**.

| Plik | md5 (poczatek) |
|---|---|
| Armoury.dll | 3599e049 |
| CrashScribe.dll | 1b213bdc |
| CrashScribe.settings.xml | 09152495 |
| RealisticCaptivity / GrandTourney / ForgeView | bez zmian od wgrania 55 |

Kopie kazdej poprzedniej wersji: obok plikow `*.bak-2026-10-15-wgranieNN-przed` i `D:\Backup-Bannerlord\wgrane\2026-10-15-wgranieNN-przed`.

**Religia BK wlaczona** (BannerKings.json `EnableReligion: true`, kopie `.bak-2026-10-15-*`).

## 2. Jeff GRA - kampania IRONMAN

- Zapis: `Documents\Mount and Blade II Bannerlord\Game Saves\IronmanjCg6MOpv28iN.sav` (rok 299, ok. dzien 66).
- Kopie zapisu: `D:\Backup-Bannerlord\zapisy-jeffa\` (robione przed kazdym wgraniem).
- **NIGDY nie wczytuj zapisu Jeffa w autotescie** - Ironman zapisuje sie sam do tego pliku i test nadpisalby kampanie.
  Autotesty tylko na zapisie testowym `autotest-F-d240.sav` (kopia testu F z doby 240).
- **Gdy gra dziala - nic nie wgrywaj i nie puszczaj testu** (sprawdz `tasklist | grep -i bannerlord`).
- Jeff zglasza bledy ze zrzutami ekranu; odpowiedz krotko, prostym jezykiem, z dowodem z logu.

## 3. Jak pracowac (narzedzia, sciezki)

Skrypty (scratchpad poprzedniej sesji - kopiuj, jesli zniknie):
`C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\dbc3d3d1-6a1e-4825-afe7-38583389c73c\scratchpad` (dalej SD)
- `SD\bieg.ps1 -DllDir <katalog DLL> -CsAt <CrashScribe z autotestem> -Tree <drzewo> -Out <plik> -Days N [-LoadSave autotest-F-d240]` - autotest
  (sam podmienia DLL, po biegu przywraca zatwierdzone i Configs; kod 0 = OK).
- `SD\wgraj.ps1 -DllDir <katalog> -Tree <drzewo> -Tag 2026-10-15-wgranieNN` - trwale wgranie z kopia i kontrola md5.
- Zestawy DLL: `SD\test\finNN` (fin49 = wgranie 61). CrashScribe do autotestu: `SD\test\CrashScribe-at-68a98cb5.dll`
  (budowany z drzewa + `git apply -3 ...\7016f733-...\scratchpad\test\at1-cs.diff`, konflikty csproj/SubModuleMain = suma obu stron).

Drzewo kodu: `C:\Users\GAME\AppData\Local\Temp\claude\C--Users-GAME-Bannerlor2-Realm-of-Thrones\ae222cb7-a7c2-473a-8fc1-1dc1d6f688e3\scratchpad\zb\int`
(klon repo; push: drzewo -> repo lokalne -> GitHub).

Build: `dotnet build Armoury/Armoury.csproj -c Release -v q --nologo "-p:GameLibs=<lancuch>\libs"`
(lancuch = `...\3cf3e0ac-5529-4b68-a794-0edec69cfda7\scratchpad\lancuch`). Po zmianie `Settings.cs`: `python -I tools/gen_mcm.py`.

Zdekompilowane zrodla (do czytania kodu gry i modow):
- gra: `...\3cf3e0ac-...\scratchpad\audyt-pieniadz\twall\`
- ROT: `...\3cf3e0ac-...\scratchpad\audyt-pieniadz\rotall\`
- BK: `...\c36b73ca-...\scratchpad\dec-bk\`, SCA: `...\c36b73ca-...\scratchpad\dec-sca\`, UI kuzni: `...\c36b73ca-...\scratchpad\dec-vmc\`

Logi Jeffa: `Modules\Armoury\Armoury-*.log`, `Modules\Armoury\Logs\<sesja>\ludzie-regiony.csv`,
`Documents\Mount and Blade II Bannerlord\CrashScribe\session-*.log`, `...\CrashScribe\items-dump.csv` (wszystkie przedmioty z cena).

## 4. Zasady Jeffa (wiazace)

- Rozmowa po polsku, prosto; napisy w grze po angielsku; komentarze w kodzie po polsku bez diakrytykow.
- Ekonomie projektuje Claude - nie pytaj o parametry, tlumacz slowami gracza.
- Wgrywaj sam po udanym autotescie (z kopia), nie gdy Jeff gra. Wyjatek 15.10: Jeff powiedzial "przetestuje, tylko wgraj".
- Nie zakladaj przyczyn - dowod z kodu albo logu. Nie pisz "gotowe", gdy cos zostalo.
- Jeden czat, zadnych rownoleglych sesji. Przy 99% limitu tygodnia - zapisz wszystko do przekazania.
- Proste sprawdzenia logow rob sam, bez agentow ("sprawdz to z logow, po co znowu uruchamiac agenta").

## 5. Co zrobiono 15.10 (wgrania 56-61)

| Wgranie | Zmiana |
|---|---|
| 56 | Oblezenia 3: budowa obleganych x1 (SiegeBesiegerPrepPercent 100); MB - trebusz w budowie nie wstrzymuje szturmu AI z gotowym taranem i wieza; drabiny AI 4:1 po 21 dobach (dzialaja tez bez machin); AC wiek (linia "Spojnosc armii (AC wiek)") |
| 57 | Tron Daenerys: StoryThrone szukal krolestw przez MBObjectManager (null zawsze) - teraz Kingdom.All / Hero.FindFirst; blokuje zmiane ustroju BK na republike |
| 58 | Kuznia: przetop i rafinacja znow zabieraja czas (jeden zegar kuzni odejmuje tylko godziny kutych sztuk); przetop broni bez drewna (SmeltNoWood); jedna poboznosc BK na pasku mapy (CrashScribe BkPietyBar) |
| 59 | Sen gracza: swit rozliczany raz na 12 h gry (tick nadrabiany liczyl noc 2 razy); nagroda turnieju: gdy brak na polce - miasto zamawia u rzemieslnikow, gracz dostaje przedmiot (TourneyPrizeCommissioned) |
| 60 | Luki z rzadkiego materialu (HistExoticBows: dragonbone 20000, weirwood 6000, Krucze Zeby 5000, zlotoserce 3000, olbrzym 2000, strzaly Kruczych Zebow 60, luk z Qarthu 18); drewno w kuzni w kg (SmithWood) |
| 61 | SmithWood: latka kosztu na metode z klasy deklarujacej (patrz pulapka nizej) + samosprawdzenie i diagnostyka w logu |

## 6. Otwarte sprawy (kolejnosc wedlug waznosci)

1. **Drewno w kuzni - SPRAWDZIC w grze Jeffa** (wgranie 61 bez autotestu). W logu Armoury szukaj:
   `Drewno w kuzni (kg) - samosprawdzenie: ... koszt drewna przy zapasie 50 kg 0 (oczekiwane 0)` - musi byc **0**.
   Potem linie `Drewno w kuzni (kg) - koszt:` (do 12) i `Drewno w kuzni (kg): <bron> X kg, otwartych ladunkow N, zapas Y kg`.
   Jesli dalej -1: latka kosztu nie jest wolana - sprawdzic `Harmony.GetPatchInfo` dla DefaultSmithingModel.GetSmithingCostsForWeaponDesign.
   Uwaga: zapas Jeffa w kg z poprzedniej sesji (99 kg) przepadl przy wyjsciu bez zapisu? - jest w zapisie (`arm_smith_wood_kg`), jesli gra zapisala po kuciu.
2. **Pulapka Harmony do przejrzenia** (znaleziona 15.10): `AccessTools.Method(model.GetType(), nazwa)` dla metody zadeklarowanej w klasie
   BAZOWEJ zwraca MethodInfo z ReflectedType typu pochodnego - Harmony wpina bez bledu, a postfiks NIE jest wolany. Sprawdzic inne latki "czynnego
   modelu": SiegeWalls2 (ActionPost na GetLogicalActionForStrategy, FoodPost na CalculateTownFoodStocksChange), ArmyCohesionYear, i grep `model.GetType()`
   / `.GetType(), "` w Armoury/src. Naprawa: `AccessTools.DeclaredMethod(mc.DeclaringType, ...)`.
3. **Dlug snu AI**: jednej nocy 203 partie AI dostaly dlug naraz (log 07-16-10, doba 108858) - nadrabianie tickow nocy przy szybkim czasie;
   ksiega AI ma straznik switu (T10-R R7), ale `campPast = InCamp(h-1)` przy nadrabianiu moze nie liczyc snu. NightMarch.AiHourly.
4. **Kusze** - propozycja czeka na "tak" Jeffa: cena kuszy 1.5-2 x luk o podobnej sile (dzis najmocniejsza kusza 27, najlepszy dlugi luk 45-48).
5. **Ceny placka i marchwi** x8 przez niedobor (BK: placek tylko z piekarni wsi, marchew z wsi pszenicznych) - propozycja wyrownania popytu czeka.
6. **Kiesa ludu 12%** swiata (pasmo 7.2-10.1%) - straznik obiegu podniosl podatek od nadwyzki do 2.2%/d; obserwowac, czy wraca do pasma.
7. **Bandyci rosna**: 6.5 tys. -> 8 tys. w ok. 25 dni (482 bandy); najwiecej w Reach (Sunhouse). Obserwowac.
8. **Armie**: 50 z 51 rozwiazan to NotEnoughParty w dniu zalozenia (SCA OnArmyCreated: wodz < 22 zdrowych / sila < 80 / 0.75 doby po poprzedniej
   armii) - armie nie "rozpadaja sie", tylko nie startuja. Ewentualna naprawa: nie zakladac armii (BK AIArmyFormation), ktore SCA i tak skasuje.
9. **Oblezenia**: MB dziala (odblokowania w logu), ale w krotkich testach miasto nie padlo; Jeff zaakceptowal odparcie OB2 przy 22% strat
   ("poczekaja do nastepnego szturmu, chyba ze przybedzie odsiecz") - bez zmian. Obserwowac "Oblezenia 2 (zdobycie)" w jego kampanii.
10. **2 wsie bez tytulu BK** (AUD-P1-5): BkTitles.cs:337 liczy, nie nazywa; naprawa - nazwy w linii + dorobka lordship w biegnacej kampanii (~1 h).
11. Joffrey: wg kodu ROT zabity na Purpurowych Godach (wczesniejsze "zyje" to nazwa rodu "Baratheon, Joffrey") - sprawdzic w kampanii Jeffa.
12. Zawieszenie w kreatorze postaci (05:43, AppHangB1, brak sladu) - nie powtorzylo sie.
13. Porzadki koncowe D1-D10 (wydajnosc, logi, MCM, instrukcja) - na koniec projektu.
14. **Rekruci ze sprzetem, ktorego nie umieja uzyc** (Jeff 15.10 ok. 12:15: "rekrutuje ludzi, maja uzbrojenie, ktorego nie moga uzywac, bo nie maja umiejetnosci - oddaj bron").
    [K] RecruitKit.cs i VolunteerKit.cs nie sprawdzaja trudnosci przedmiotu (Difficulty / GetSkillValue) wobec umiejetnosci zolnierza.
    Do zrobienia: przy wyborze kompletu tylko rzeczy, ktorych zolnierz moze uzyc; rzecz juz nieuzywalna - oddana (Jeff: "oddaj bron") do zbrojowni gracza albo sprzedana.
15. **Zbrojownia gracza - czyje co jest** (Jeff 15.10): nie widac, ktory sprzet jest gracza, a ktory zolnierzy (wlasnosc zolnierza - placi sie za niego).
    Jeff chce: "zabierz wszystko" - pierwsze klikniecie zabiera sprzet GRACZA, drugie caly sprzet zolnierzy (z cena); pojedyncza rzecz - pokazuje cene.
    Do rozpoznania: gdzie jest ekran/menu zbrojowni (Armoury src: grep "zbrojowni gracza", PlayerArmory, schowek) i jak liczona jest cena.
    **DECYZJA JEFFA 15.10 (zastepuje pomysl z dwoma kliknieciami) - ZADANIE:**
    (a) zbrojownia pokazuje TYLKO sprzet gracza (do wziecia za darmo); lup i sprzet bedacy wlasnoscia zolnierzy NIE sa w niej widoczne;
    (b) zolnierze sami sprzedaja swoj lup w pierwszym miescie i, jesli ich stac, dokupuja sobie lepszy sprzet, a stary sprzedaja (jak lordowie AI - Dozbrajanie);
    (c) gracz moze dozbroic zolnierzy lepszym sprzetem ze swojej zbrojowni - WYMIANA: jego lepszy pancerz idzie do zolnierza (znika ze zbrojowni),
        a na jego miejsce do zbrojowni gracza trafia stary, gorszy pancerz zolnierza (o ile zolnierz jakis mial).
    Do sprawdzenia przed praca: obecna wlasnosc sprzetu zolnierza (Konie rekrutow 160 "kon wlasnoscia zolnierza", sakiewka ludzi, Dozbrajanie gracza 0/0 w logu).
16. Kryjowka (Jeff 15.10): log 12:10:41-49 "HideoutPurge: przeszukanie 17.5 h (14 ludzi) ... zloto 0, renown 5, osad z wdziecznoscia 3" - przeszukanie bylo,
    kryjowka byla pusta (wszystkie kryjowki swiata maja razem ok. 2 tys. zlota - bandy sprzedaja lup paserom, zloto idzie do miast; kiesy band przechodza na
    zwyciezce w samej bitwie). Mozliwa poprawa: komunikat dla gracza wprost "kryjowka pusta / znaleziono X" (dzis tylko log).
    Dowod (log 10-56-30, "Kiesy rozbitych (3.2)" doba 108945): tego dnia pokonana 1 banda (kryjowka Jeffa), w kiesie 362 zl - wyplacone zwyciezcy w bitwie
    ("do wodzow 488" = bandy 362 + karawany 126). Jeff 15.10: "bandyci sprzedaja rzeczy - gdzie trzymaja zloto? to podstawowa logika" - PROPOZYCJA (czeka na tak):
    banda z kryjowka odklada czesc kiesy (np. po sprzedazy paserowi) do skarbca kryjowki; przeszukanie kryjowki daje ten skarbiec; komunikat gracza po bitwie
    "the band carried X" i po przeszukaniu "the hideout held Y". Dzis bandy sa biedne: paser placi im ok. 1/3 wartosci (doba 108945: 713 z 1992), reszta zostaje w kasach miast.

## 7. Pulapki techniczne (z doswiadczenia)

- Bash: komenda > 8 KB sie ucina - dluzsze skrypty pisz plikiem (Write) i uruchamiaj.
- Heredoc w bash gubi backslashe w pythonie (`\2026` -> znak 0x82) - sciezki przez `chr(92)`, sprawdzaj wynik.
- Skrypt Workflow nie moze miec CRLF.
- Linie konczace sie CRLF/LF: edycje pythonem zachowuja koncowke pliku (wzorzec `nl = '\r\n' if '\r\n' in t else '\n'`).
- Autotest: po KONIEC OK silnik czasem wywraca sie przy wyjsciu (0xC0000005) - znane, skrypt zamyka gre i przywraca pliki.
- Przerwanie autotestu (TaskStop): potem `autotest.ps1 -RestoreOnly` i recznie przywroc Configs z `C:\Users\GAME\autotest-kopie\<data>\Configs`
  (BannerlordConfig.txt, engine_config.txt, DynamicReinforcements.reputation.cfg).
