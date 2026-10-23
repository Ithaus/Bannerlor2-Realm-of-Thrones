## m109-oboz - bitwa w obozie stawia ten sam oboz co spacer po obozie (23.10)

**Problem:** Jeff 22.10 21:20 (zrzut, wersja 103.5): "inny jest oboz, jak wchodze do obozu - jedno polanie, a inny, jak jestem atakowany
w obozie; moze przez jencow? jency dodaja namioty?". Log `Armoury-2026-10-22_20-54-10.log` 21:20:01: `CampScene: postawiono 10 elementow obozu
na polu bitwy` (staly zestaw), spacer 20:18:13 i 22:31:47 (`Armoury-2026-10-22_19-54-56.log`, `..._22-28-22.log`): `ludzi zdrowych 0 ... namioty 0,
poslania 1, ogniska 1 ... konie 1` / `konie 2`.

**Przyczyna:** dwa niezalezne kody obozu. Spacer (`CampWalk.cs`, m97) buduje oboz wedlug kolumny: namioty ROT po jednym na Camp Walk Men Per Tent (6)
zdrowych ludzi, wodza od 6, oficerski od 60, ogniska, poslania, wozy CampCart, konie taboru; sam gracz = poslanie, ognisko i kon. Bitwa w obozie
(`CampScene.cs`, E7/O2) stawia STALY zestaw Native: 3 namioty tent_vlandia_c, ognisko, 4 pochodnie, 2 ploty - bez wzgledu na liczbe ludzi.
Jency NIE dodaja namiotow ani tu, ani tu: `CampScene` nie czyta zadnego rostera, spacer czyta tylko `MemberRoster` bez bohaterow (jedyne slowo
o jencach to `Hero.MainHero.IsPrisoner` w `CanWalk`).

**Zmiana (klucz `CampBattleLikeWalk`, domyslnie TAK; NIE = stary zestaw Native 1:1 jak 44c03a05):**
- `Armoury/src/CampWalk.cs`: nowa klasa `CampLayout` - caly uklad scenografii spaceru przeniesiony bez zmian wartosci i kolejnosci FreeSpot/Occupy
  (stale, listy prefabow, obrysy Footprints, Probe, Blocked, FreeSpot, Put, konie taboru i jezdzcow, uwiazanie, pilnowanie). Uklad lokalny obozu:
  srodek C, os Up, Right = (Up.y, -Up.x); obrysy w tym ukladzie (spacer: Up = (0, 1) - obrysy dawnego spaceru przesuniete o srodek, test nakladania
  ten sam). Plan wedlug regul (`CampLayout.Count` / `Plan.Recount`: namioty, zolnierskie, poslania, ogniska, wozy, konie taboru) - te same liczby,
  ktore uklad probuje postawic. Ludzie spaceru (gracz, siedzacy, chorazy) zostaja w `CampWalkLogic` (haki OnPlayerSpot, OnHeroHorseSpot,
  OnLeaderTent); linia "Spacer po obozie (m97): wejscie" bez zmian.
- `Armoury/src/CampScene.cs`: `BuildLikeWalk` po rozstawieniu wojska (ten sam wyzwalacz co E7/O2): strona gracza = aktywni ludzie wszystkich druzyn
  tej strony, kierunek do wroga (`FrontYaw`), os obozu od wroga, srodek obozu 21 m za ostatnim zolnierzem (6 m odstepu + 15 m - rzad wozow 12 m, obrys
  woza 2.4 m, zapas), namioty wejsciem do wroga. Bez fizyki (jak dotad), nic blizej niz 6 m od kazdego agenta (migawka) i od linii wojska (zaden obrys
  przed y = -15 w ukladzie obozu); bez ludzi przy ogniskach, bez konia gracza i koni jezdzcow (miejsca gracza i jego konia zarezerwowane, puste).
  Malo miejsca za linia: rzedy namiotow szersze (6..12 obok siebie). Limity jak w spacerze: 40 namiotow zolnierskich, 14 ognisk, 6 wozow, 12 koni taboru.
  Konie taboru uwiazane (bez wedrowania i wsiadania, niesmiertelne, na miejsce co 1.5 s). Linie `CampScene (m109): ` - start sesji, "bitwa w obozie
  nr k", "oboz jak spacer nr k" (plan wedlug regul, postawione, pominiete z przyczyna, rzad, glebokosc, wodz, rzad 1, najblizej gracz / wojsko,
  front obozu za linia, kierunek, encje, czas, potkniecia), "bez obozu (powod)"; bledy `ERROR in CampScene.<miejsce>` (pierwsze 3 i co 50 na miejsce).
- `Armoury/src/SubModuleMain.cs`: CampScene dopinana takze pod plikiem testu; `CampWalk.TestTick()` w OnApplicationTick (test spaceru).
- `Armoury/src/Settings.cs` + `McmSettings.cs` (gen_mcm): klucz "Camp Battle Like Walk" obok "Camp Battle Props Enabled" (grupa "A night's rest").
- Haki autotestu (tylko LogGate.Mode 1/2 + plik; w grze nic nie robia): `CrashScribe/armoury-oboz-bitwa.flag` - kazda bitwa w polu jak w obozie
  (NightRest bez zmian), linie "bitwaK: ludzi=N konie=N wozy=N" albo "bitwaK: stary" zmieniaja tylko plan i scenografie (roster i tabor bez zmian;
  brakujace konie - juczny sumpter_horse), zdjecie obozu kamera nad linia (`CrashScribe/zdjecia/oboz-m109-k-*.png`); `CrashScribe/armoury-oboz-spacer.flag`
  - jeden spacer na proces przy pierwszej wolnej chwili na mapie po wyjsciu z osady, linia "Spacer po obozie (m109): test - uklad ...", wyjscie po 8 s.
- `tools/sprawdz_logi.py`: grupa `m109oboz` (OBOZ-S, OBOZ-S2, OBOZ-0..OBOZ-11; opis w --help).

Slowami gracza: oboz staje, gdy klikniesz "Rozpocznij bitwe", za twoim wojskiem, wejsciami namiotow do wroga - tyle namiotow, ognisk, poslan, wozow
i koni taboru, ile widzisz w spacerze po obozie. Nikt w nim nie grzeznie, strzaly przelatuja, konie stoja uwiazane. Jeff walczy sam z towarzyszami
(0 zdrowych zolnierzy) - zobaczy ognisko, poslanie i konia taboru, bez namiotow (tak samo jak dzis w spacerze).

**Ryzyko / co sprawdzic (kontrola calosci):**
1. Spacer po refaktorze: przenos bez zmiany stalych i kolejnosci FreeSpot/Occupy/Put/Tether; obrysy w ukladzie lokalnym = dawne przesuniete o srodek
   (w spacerze krawedzie obrysow sie nie stykaja). Sprawdzic linie "wejscie" (te same liczby co przed m109 na tej samej druzynie) i OBOZ-10/OBOZ-11.
2. Stan gry tylko czytany: `NightRest.PlayerCamped` (pisza go dalej TentCore, zwiniecie, straznik, reset, ForcedMarch - bez zmian), `CampCart.Carts`,
   `MemberRoster`, `ItemRoster`, `CampWalkMenPerTent`. Scenografia i konie obozu nie wychodza z rostera - ekonomia zamknieta, bez nowego stanu i typow
   zapisu (stare zapisy sie wczytaja).
3. Agenci-konie w bitwie (najwyzej 12, `Mission.SpawnMonster` - `OnAgentBuild` u wszystkich zachowan misji; konie Native tez nie maja druzyny):
   nasze miejsca z IsMount/IsHuman sa bezpieczne (DothrakiArms, HorseFate - tylko ludzie; BloodPools, FieldCraft, BattlefieldLaw, GrandTourney - wierzchowiec
   z jezdzcem). Zabieraja 12 miejsc z limitu agentow silnika (posilki). Posilki / uciekajacy moga otrzec sie o konia (zderzenie agentow) - tylko wyglad.
   Przy scalaniu z w-toku/m109-lup: jesli zbiera zywe konie z misji - pomijac konie obozu (Character null, Team null, bez flagi Mountable).
4. Koszt: do ok. 510 encji scenografii w jednej klatce przy "Rozpocznij bitwe" (OBOZ-5: <= 250 ms, <= 520 encji); przekroczenie - budowa na klatki.
5. Malo miejsca za linia (srodek obozu za granica sceny): elementy pomijane z przyczyna "granica", glebokosc w linii; czeste porazki OBOZ-7 - oboz z boku
   jako osobna zmiana.
6. Test spaceru w biegu bitwa: autotest czeka na mape najwyzej 60 s (StepSpawn) - spacer 8 s + wczytanie sceny; gdy nie zdazy - bitwy "nie ruszyly"
   (wtedy zdjac plik armoury-oboz-spacer.flag i puscic test spaceru osobno).
7. E7/O2: linia startu bez zmian, T0 liczy takze bledy ukladu w bitwie; przy kluczu TAK wiersz "postawiono" - INFO tylko z bitew "stary".
8. Punkty scalania z innymi m109: SubModuleMain (warunek CampScene i linia w OnApplicationTick), Settings obok CampBattlePropsEnabled, McmSettings
   (wygenerowac ponownie po scaleniu), dispatch i --help w sprawdz_logi.py (po m107gosp).

**Status:** DO SPRAWDZENIA (autotest bieg `oboz`; nie wgrane).

**Plan autotestu:** build Armoury (kod wyjscia 0), gen_mcm, sprawdz_narzedzie, `sprawdz_logi --grupa m109oboz` na dowolnym logu (OBOZ-S i OBOZ-S2 OK).
Bieg `oboz` = bieg `bitwa` (zapis autotest-F-d240, `-Days 5 -Battles 4 -BattleSec 90 -Speed 20`, flaga armoury-wioska-gracza.flag) plus pliki:
`CrashScribe/armoury-oboz-spacer.flag` (pusty) i `CrashScribe/armoury-oboz-bitwa.flag`:
```
bitwa1: konie=12 wozy=3
bitwa2: ludzi=400
bitwa3: ludzi=0
bitwa4: stary
```
(bitwa 1 - prawdziwa druzyna, konie uwiazane i wozy; 2 - limity i koszt; 3 - przypadek Jeffa: ognisko i poslanie; 4 - zachowanie przy NIE, 10 elementow).
Ocena: `--grupa m109oboz` na logu Armoury biegu; E7/O2 bez regresji (T1, T0, INFO "postawiono" z 1 bitwa i 10 elementami); zwykle progi biegu bitwa
(AUTOTEST BITWA BLAD 0, HorseFate, BloodPools, BattlefieldLaw bez nowych ERROR); obejrzec zdjecia `zdjecia/oboz-m109-*.png` (oboz za linia, wejscia do wroga,
nic na zolnierzach). Pliki flag USUNAC po biegu.

**Do decyzji Jeffa (nie robione):** czy towarzysze maja sie liczyc jako ludzie obozu (dzis i w spacerze, i w bitwie nie - stad jedno poslanie);
zagroda jencow (pominieta - nie bylo decyzji).
