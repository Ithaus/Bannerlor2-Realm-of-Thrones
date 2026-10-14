# DROGI - "A co z drogami, miales drogi tez dodac?" (rozpoznanie 14.10, tylko odczyt; gra nie uruchamiana, kod moda bez zmian)

Oznaczenia: [P] pomiar (plik, log, md5), [S] szacunek, [W] wniosek z kodu (niesprawdzony w grze). Skroty sciezek:
ZB = `C:\Users\GAME\AppData\Local\Temp\claude\C--Users-GAME-Bannerlor2-Realm-of-Thrones\ae222cb7-a7c2-473a-8fc1-1dc1d6f688e3\scratchpad\zb\int` (WGRANIE 51, aca89cc);
SCR = `C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\dbc3d3d1-6a1e-4825-afe7-38583389c73c\scratchpad`;
SIEC = `C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\7016f733-d379-418e-b700-f66fd52e4d2b\scratchpad\drogi\siec`;
DR = `C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\3cf3e0ac-5529-4b68-a794-0edec69cfda7\scratchpad\dzien-6\wioski-v4\DROGI.md`;
G = folder gry; L51 = `G\Modules\Armoury\Armoury-2026-10-14_00-49-15.log`; TEN = katalog tego raportu.

## 0. Dla Jeffa - prostym jezykiem

1. Masz racje: drog w grze jeszcze NIE ma. Przesunely sie za biegiem dwuletnim i nikt Ci nie pokazal nawet proby - to moj blad.
2. Gotowa jest PROBA: dwa kawalki mapy - wokol Fairmarket i wokol Winterfell, kazdy ok. 140 x 140 km. Na nich 641 "odciskow" sciezki
   (te same naklejki, ktorymi ROT rysuje uliczki w swoich osadach): drogi do wiosek, drogi polne i lesne (w lesie ciemniejsze).
   Proba jest juz wgrana do gry, ale WYLACZONA - wiec nic nie widziales.
3. Teraz zdjecia: autotest sam zrobi 30 zdjec (6 miejsc x 5 wysokosci kamery), ok. 15 minut, na osobnej probnej DLL.
   Twoich ustawien nie ruszamy, po tescie wraca Twoja DLL. Polecenie jest gotowe.
4. Po zdjeciach mowisz: "wyglada dobrze" albo "zle". Jesli dobrze - robimy to, co wybrales 12.10: trakty tam, gdzie ROT nie namalowal
   drog (Polnoc, Dorne, Essos), droga do kazdej wioski, a w Essos ciemne drogi valyrianskie (Pentos - Ghoyan Drohe - Norvos - Qohor
   i "droga demonow" Meereen - Mantarys - Volantis).
5. Cala siec jest juz policzona (ok. 30 tys. jedn. drog, ok. 69 tys. odciskow; 95.7% wiosek ma polaczenie, 106 na wyspach i w Qarth nie).
   Brakuje kodu, ktory pokazuje drogi kawalkami tylko tam, gdzie patrzy kamera, i testu klatek. To ok. 1.5-2.5 dnia pracy.
6. Drogi beda TYLKO do patrzenia - nikt po nich nie jedzie szybciej (Twoje "nie" z 12.10).
7. Ryzyko: nie wiemy, jak lancuch naklejek wyglada w grze (moze jak kropkowana linia) - po to sa zdjecia, zanim zrobimy calosc.

## 1. Co jest gotowe i co pokazuje proba

| Rzecz | Stan | Dowod |
|---|---|---|
| kod proby `MapRoadsProbe.cs` (659 linii) w WGRANIU 51 | w grze, klucz `MapRoadsProbeEnabled` = false (kod i MCM) | ZB\Armoury\src\MapRoadsProbe.cs:1-21; Settings.cs:1903; McmSettings.cs:5457; md5 ZB\Armoury\bin\Release\Armoury.dll = gra = fce3a3d9 [P] |
| przy false nic sie nie laduje | "MapRoadsProbe: wylaczona ... nic nie stawiam" w kazdym biegu; 0 linii "wlaczona"/"postawione" w logach w G\Modules\Armoury | L51:279; Armoury-2026-10-14_00-43-17.log:301 [P] |
| plik stempli `arm_map_roads_probe.tsv` | w grze = w ZB, md5 843a6713; naglowek: 2 okna 50 x 50 jedn., `scene_xml_crc` 146201570, `layout_crc32` 42a7528e | G\Modules\Armoury\ModuleData; plik:1-8 [P] |
| prefab `Prefabs\arm_map_roads.xml` (6 prefabow z samym `decal_component`) | w grze = w ZB (md5 43e830e3); gra go wczytuje przy starcie bez bledu | rgl_log_30948.txt:3606 "Loading xml file: $BASE/Modules/Armoury/Prefabs/arm_map_roads.xml"; rgl_log_errors_30948.txt - 0 wpisow arm_/prefab [P] |
| zgodnosc z mapa i ukladem wiosek | CRC sceny gry = 146201570 (zgodne); crc wierszy `arm_map_villages.tsv` w grze (2 446 wiosek) = 42a7528e = naglowek proby | L51:359 "CRC sceny: zgodne"; TEN\crc.py [P] |
| Ustawienia Jeffa | `Armoury.json` NIE zawiera klucza `MapRoadsProbeEnabled` (0 trafien grep) - MCM bierze domyslna z DLL | Documents\...\Configs\ModSettings\Global\Armoury\Armoury.json [P] |

**Co narysuje proba [P z pliku, TEN\stamps.py]:** 641 stempli: **Fairmarket 493** (drogi wiejskie 38, polne 45, sciezki do wiosek 410),
**Winterfell 148** (wiejskie 49, polne 19, sciezki 80); "traktow" w tych oknach brak (glowne drogi sa tam namalowane przez ROT).
Wyglad: sciezka 348, lesna 201 (przyciemnione `FF9A8A70`), wiejska 78, polna 14; material `decal_battania_path_a` 40 / `_b` 601 (uliczki ROT).
Okna: Fairmarket x 367.4-417.4, y 507.1-557.1 (srodek = miasto town_B1 392.404/532.106, ROT-Map settlements.xml:5714); Winterfell
x 371.0-421.0, y 819.9-869.9 (town_B2 395.954/844.879, :5805) - **w Winterfell stemple tylko na poludnie od zamku** (y 823.5-842.3; na polnocy gory, bez wiosek).
Zachowanie [W]: stawia wszystkie stemple raz, budzet 8 ms / tick 0.25 s, max 400 na tick (MapRoadsProbe.cs:202-204, :401-486); LOD: odleglosc
kamery <= 60 wszystkie, 60-110 tylko wiejskie, > 110 nic (:205-206, :546-567); co klatke tylko licznik klatki (:356-366 - zgodnie z pulapka CLAUDE.md 7);
log: plik/CRC, "postawione N z M ... w X ms", "kadr nad oknem ...", klatki z drogami / bez (sredn., mediana, p95). Rysunki planu (nie z gry): SIEC\proba-fairmarket.png, proba-winterfell.png.

## 2. Zdjecia proby autotestem - bez dotykania ustawien Jeffa

- **Sposob: DLL probny z domyslna true**, nie zmiana `Armoury.json`. Uzasadnienie: klucza nie ma w pliku Jeffa [P], wiec MCM uzyje domyslnej z DLL
  (MCM nie dopisuje brakujacych kluczy - klucz jest w DLL od W34, a w pliku go nie ma [P]); `autotest.ps1` nie ma parametru do zmiany MCM
  (param :43-77 [P]), za to i tak kopiuje i przywraca cale Configs (at-w51-40.out.txt:51, :108-110 [P]). Zmiana: 2 linie - Settings.cs:1903
  i McmSettings.cs:5457 `false -> true` (`TEN\wlacz_probe.py`, sprawdzone na kopii: diff = dokladnie te 2 linie [P]). Wzor "DLL do zdjec" juz byl (W4Photo, Armoury.csproj).
  `bieg.ps1` ma juz na to parametr `-ArmouryDll` "(np. z proba drog)" (SCR\bieg.ps1:12).
- **CrashScribe z trybem zdjec:** `SCR\test\CrashScribe-at-hamulec.dll` (md5 ef7ee537; ma `photo_shots` [P]; ten sam, ktory jechal w tescie WGRANIA 51 - at-w51-40.out.txt:52).
  Budowac nie trzeba (zrodlo: SCR\cs-at2\CrashScribe\src\AutotestPhotos.cs). Tryb zdjec dzialal 08.10: Documents\...\CrashScribe\zdjecia\at-20261008-* (PNG) [P].
- **Cele** (`TEN\zdjecia-drogi.json`): fairmarket-miasto (town_B1), fairmarket-wsie-pn (387, 542), fairmarket-las-pd-zach (372, 512 - stemple lesne),
  winterfell-miasto (town_B2), winterfell-pd (383, 832), winterfell-pd-wsch (414, 828) - punkty wedlug skupisk stempli [P]. Ujecia
  `8,17,25,44,70` = odleglosc kamery 14/28/38/59/82 (wzor AutotestPhotos.cs:179-193) - cztery w LOD "wszystkie", 70 pokazuje LOD "tylko wiejskie" [P z wzoru].
- **GOTOWE POLECENIE** (PowerShell; Steam wlaczony, gra zamknieta, Jeff nie gra):
  `powershell -NoProfile -ExecutionPolicy Bypass -File "C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\c36b73ca-1de0-4b1b-80ce-71bd02afe545\scratchpad\pytania14\drogi\zdjecia-drogi.ps1"`
  Skrypt (sprawdzona skladnia, ASCII, NIE uruchomiony): sprawdza md5 WGRANIA 51 w grze; `git archive aca89cc` z ZB do TEN\wt (ZB nietkniete),
  `wlacz_probe.py`, `dotnet build ... -p:GameLibs=C:\Users\GAME\Bannerlor2-Realm-of-Thrones\libs` -> TEN\Armoury-drogi-zdjecia.dll; potem
  `SCR\bieg.ps1 -DllDir SCR\test\fin23 -CsAt SCR\test\CrashScribe-at-hamulec.dll -Tree ZB -Out TEN\at-drogi-z.out.txt -Days 2 -PhotosFile TEN\zdjecia-drogi.json -ArmouryDll TEN\Armoury-drogi-zdjecia.dll`;
  na koncu md5 Armoury w grze (ma byc fce3a3d9), linie "MapRoadsProbe" z logu i katalog zdjec. Z `-BezDrog` - te same 30 zdjec na DLL z gry (porownanie "przed").
- Czas [S]: ok. 10-15 min (start ok. 1.5 min; 6 celow x ok. 20 s; 2 doby; W51: 40 dob = 11 min [P] at-w51-40.out.txt). Limit autotest.ps1 = 1 + 10 + 12 = 23 min (autotest.ps1:495-496).
- Ocena: `python -I ZB\tools\sprawdz_logi.py --grupa etap7 <Armoury-*.log>` - blok `etap7_d0` (D0-2a plik i CRC, D0-2b "postawione N z N", D0-3 potkniecia 0;
  sprawdz_logi.py:192-195); zdjecia w Documents\...\CrashScribe\zdjecia\at-<run>\drogi-*-z08.png ... -z70.png.
- Gdyby pudla byly obrocone w poprzek drogi: druga proba bez nowej DLL - kopia tsv z `# yaw_offset_deg: 90` (albo `# method: code`) podana w bieg.ps1 przez inne `-Tree` (MapRoadsProbe.cs:6-11, :128-132).

## 3. Pelna siec (E7/D1): trakty + droga do kazdej wioski + Valyria

**Zrodlo danych:** NIE sciezki ROT i NIE siatka nawigacji gry. Drogi ROT to farba w `terrain.bin` (warstwa 13 "road", ok. 10 tys. jedn.), w grze nie da sie jej
dorysowac (DR rozdz. 1, 2.4). Nasza siec to model: najtansze sciezki po rastrze terenu 1 jedn. ("a-teren": `teren.npz`, `flora.npz`, `drogi_raster.npz`,
`drogi.json`, mosty/brody) + meandry, generator `SIEC\s6_siec_gra.py` (9.8 s) na ukladzie wiosek Z GRY. Wynik juz jest [P, SIEC\s6-wynik.txt]:
`arm_map_roads.tsv` (3 155 linii) i `arm_map_road_stamps.tsv` (76 627 stempli); `layout_crc32` 42a7528e = plik w grze -> uwaga RB7 "843 z 2 446"
dotyczyla starej sieci (DR, 08.10 03:01) i jest NIEAKTUALNA [P]. Klasy: trakt 9 012 jedn. / 16 581 st., wiejska 9 983 / 20 981, sciezka do wioski 11 157 / 31 835,
polna 2 652 / 7 230. Polaczone 2 340 z 2 446 wiosek (95.7%), 106 bez drogi (wyspy, Qarth). **Wariant Jeffa (b) = bez polnych: ok. 30 150 jedn., ok. 69 400 stempli [P sumy].**
Essos: trakty z modelu (graf twierdz), BEZ lore - Valyrii w danych nie ma [P: grep "valyr" w s6_siec_gra.py = 0]. Konce tras lore sa w ROT [P]: Pentos town_EN4,
Ghoyan Drohe castle_ES1, Norvos town_ES1, Qohor town_ES6, Volantis town_ES2, Mantarys town_K2, Meereen town_K1 (trasy: a08:162-163).

**Praca [S]:**
| Paczka | Co | Godziny |
|---|---|---|
| D1-0 | zabezpieczyc generator + dane (SIEC, a-teren) w repo i na D: - dzis TYLKO na dysku C (find na D:\Backup-Bannerlord - 0; git ls-files - 0) [P] | 0.5 |
| D1-a dane | wariant (b); Essos: trakty tylko na trasach lore jako "valyrianska" (ciemniejszy `factor`), reszta Essos szlaki; ponowny przebieg + rysunki kontrolne | 3-5 |
| D1-b kod | `MapRoadsView`: komorki 50 x 50, leniwe stawianie w promieniu kamery (r = odl. + 40, max 150), budzet stempli na tick, zdejmowanie poza 2r, LOD jak proba, odbudowa po powrocie z misji/miasta (scena mapy od nowa), MCM `MapRoadsVisible`; ramka/prefab/pomiar z proby; recenzja, CHANGELOG | 6-10 |
| D1-c test | zdjecia kilku regionow (Dorzecze, Polnoc, Reach, Essos z droga valyrianska), `-Profile` klatka z/bez drog, bieg 10-40 dob, zapis/wczytanie, miasto/bitwa i powrot | 3-5 |
| razem | | **ok. 12-20 h (1.5-2.5 dnia)**; DR szacowal "ok. 2 dni" |
D2 (premia +15% za droge) - NIE robimy (Jeff 12.10, projekt E7 :874; R3 domyslnie 0).

**Ryzyka:**
1. Wyglad niesprawdzony w grze (lancuch decali moze wygladac jak kropki albo pudla w poprzek) - stad zdjecia D0 przed D1 (DR rozdz. 9).
2. Wydajnosc mapy [S z DR rozdz. 4 / SIEC\s6-wynik.txt]: w kadrze przy zwyklej kamerze 150-380 stempli, przy oddaleniu z LOD do ok. 1 100; zywych encji 1.6-5.5 tys.
   (scena ROT ma 53 550) - klatka NIEZMIERZONA (proba ma licznik). Pulapka CLAUDE.md 7 ("nie dotykaj wizerunkow co klatke"): stawianie/zdejmowanie tylko przy
   zmianie komorki kamery, z budzetem; co klatke wylacznie licznik - tak jak w probie; nic na partiach.
3. Scena mapy budowana od nowa po misji - komponent musi sie przebudowac (wzor: EnsureRegistered/OnFinalize proby, :236-263, :330-348).
4. Dane tylko na C: (dysk zeruje pliki - pamiec projektu) - D1-0 najpierw.
5. Siec to model, nie lore ROT: trakty Polnocy/Essos wymyslone przez algorytm; nowa wersja mapy ROT albo ukladu wiosek = nowy przebieg generatora (CRC w naglowku zatrzyma stary plik).
6. Snieg (zima na Polnocy) na decalach - nie wiem.

## 4. Czego nie wiem
- Jak proba wyglada w grze i ile kosztuje klatke - nikt jej nie uruchomil (0 linii "postawione" w logach [P]).
- Czy rejestracja widoku zdazy przed pierwszym zdjeciem - [W] tak (stawianie 641 stempli ok. 2-3 ticki po 0.25 s, pierwsze ujecie czeka 6 s - AutotestPhotos.cs NewTargetWaitS).
- Wyglad decali pod sniegiem; czy 70 (odl. 82) nie bedzie juz za daleko, by cokolwiek rozroznic.

Pliki: TEN\RAPORT.md, TEN\zdjecia-drogi.ps1, TEN\zdjecia-drogi.json, TEN\wlacz_probe.py, TEN\stamps.py, TEN\crc.py, TEN\strs.py.
