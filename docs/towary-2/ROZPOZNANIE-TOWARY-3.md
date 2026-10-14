# Rozpoznanie "towary 3": regulator lnu, plotno i skora dla wojska, skora i zwierzeta (K5), narzedzia wsi - 14.10.2026, bez zmian kodu

**Logi** (okna srednich jak `sprawdz_logi.py`: d91-120, d120 = dzien gry 108955; "F:46391" = numer linii w pliku fin29):
- **fin29** = `D:\Backup-Bannerlord\logs\2026-10-14-noc\Armoury-2026-10-14_05-22-14.log` - towary-2 K1-K4 wlaczone, nowa kampania, 120 dob.
- **N** = `...\Armoury-2026-10-14_02-50-28.log` - przed towary-2 (to samo okno dob, inna kampania).
- **D** = `...\Armoury-2026-10-13_21-14-13.log` - bieg dwuletni sprzed plotna (zima od d459).
- [P] = pomiar (log albo kod), [S] = szacunek. Wynik `--grupa towary2` na fin29: 22 progi, 7 NIE (T2-K1-6, T2-K2-2, T2-K2-3, T2-K2-5, T2-K3-2, T2-K3-3, T2-K3-4); `--grupa plotno`: 1 NIE (PL-4).

**Kod:** drzewo `zb\tw3` (galaz `w-toku/towary3`, 6e6313c5). **Armoury.json Jeffa:** zaden klucz proponowany nizej (`SutlerClothing`, `AnimalsUnderValve`) w nim nie istnieje.

---

## 0. Dla Jeffa - prostym jezykiem

1. Lnu nikt nie przycial: regulator go nie podniosl, bo po zmianie wsi plotna jest za duzo, nie za malo - zapas plotna rosnie (3 800 szt., +91 dziennie na koniec), a plotno kosztuje 0.63 ceny.
2. Zolnierze zalog w miastach i zamkach dostaja 100% plotna i skory. Brakuje tylko druzynom w polu: kupuja odziez wylacznie przy wyjezdzie z miasta, a w polu jest 9 na 10 druzyn.
3. Dlatego druzyny czekaja na plotno i skore za ok. 34 dni zuzycia, choc to samo plotno lezy na straganach, w karawanach i na wozach.
4. Naprawa: markietani, ktorzy juz dzis jezdza za wojskiem i co dzien placa w najblizszym miescie, beda stamtad przywozic tez buty, odziez i plotno - z sakiewek zolnierzy, po cenie targu.
5. Skora: garbarze przerabiaja juz prawie wszystkie skory surowe (94-100%). "Bez zysku" w 60 miastach znaczy tylko, ze skor surowych nie starcza dla wszystkich garbarzy.
6. Skor surowych brakuje, bo brakuje zwierzat na rzez: wieprze i owce sa zjadane w calosci, mieso schodzi szybciej niz przybywa - a regulator wsi zwierzat w ogole nie obejmuje.
7. Dlatego K5 (zwierzeta pod regulator) jest potrzebny juz teraz, nie dopiero w zimie: da ok. 1/5 wiecej skor surowych i wiecej miesa.
8. Narzedzia wsi (65.5 przy progu 66.7) to nie problem: kuznie w tej kampanii zrobily o 13% mniej narzedzi, a budowy biora tylko o 4 sztuki dziennie wiecej.

---

## 1. Tabela pozycji

| nr | pozycja | problem (fin29, d91-120) | przyczyna (dowod) | regula |
|---|---|---|---|---|
| T3-1 | Regulator lnu x1.00 "(bylo x1.30) - len ciety" (T2-K2-5, PL-4 NIE) | **NIE.** Len nie byl ciety - nigdy nie byl podniesiony: decyzje 1/2/3 (d57/d85/d113) len x1.00 "bez zmian", zuzycie/produkcja 0.90 / 0.87 / 0.84, stos swiata 8 176 -> 11 497 -> 14 427 = 12 -> 19 -> 24 dni zuzycia (F:22043, F:32837, F:43623). W N decyzja 2 dala x1.30 przy 1.03 (N:32253). | **[P]** Krok w gore wymaga zuzycia >= 98% produkcji (`HarvestValve.cs:575`). Po K2 wsie kupuja 1.7 szt./d plotna zamiast 148 (dopisek "Kiesa ludu" F:46505), plotno potanialo: indeks 1.48 (d1-15) -> 0.63 (d106-120), N 2.0-3.4; tkacze koncza "bez zysku" w 35 miastach (N 9), plotna powstaje 211/d (N 313; PL-8), lnu zuzyto 602-690/d przy produkcji 713 (len na polkach miast 7 898, N 2 040 - F:46186). Ciecia ponizej x1 i tak nie bedzie: wymaga stosu > 60 dni (`:547`), a bezpiecznik P3/Z2 (`:555`, CZYNNY) wstrzymuje ciecie, gdy lnu nie ma w > 15 miastach (jest 45-68). | **Bez zmian.** Liczenie kolejki wojska jako zuzycia podnioslo by len przy rosnacym zapasie plotna (3 791, +91 szt. w d120; karawany 1 223 z "KUPNO STOI", F:46336) - wiecej lnu = wiecej plotna w tych samych stosach, a N5-D przycialby krok po pelnym oknie przy nowym mnozniku (kolysanie). Regulator ma liczyc to, co swiat naprawde zuzywa (jedna regula). Gdy wojsko zacznie kupowac (T3-2), zuzycie lnu wzrosnie i regulator podniesie len sam. Miara narzedzia do wymiany (rozdz. 3, T3-N-1). |
| T3-2 | Plotno do wojska 69/d (prog 75), kolejka 2 052 -> 2 417 (+18%) | **TAK.** Narosla 95.1 szt./d: zalogi 23.4 -> wziely 23.5 (miasta 13.7 bez zlota, zamki 9.8) = **100%**; druzyny 71.7 -> kupily 45.5 = **63%**. Kolejka 2 417 = ok. 34 doby potrzeby druzyn; 27 druzyn i zalog juz na suficie 120 dob (F:46391). | **[P]** Druzyna kupuje odziez tylko przy wyjezdzie z miasta (`MenPurse.OnLeft` -> `ArmyClothing.BuyForParty`, `MenPurse.cs:290`): 44.5 wizyt z potrzeba na dobe przy 627 druzynach, w polu 556 (linia "Markietani" F:46006). Markietani w polu odkladaja pieniadze na odziez (rezerwa `ArmyClothing.PendingCost`, `MenPurse.cs:377`: "rezerwy: naprawy i odziez 1 173 207"), ale odziezy nie dowoza. Nie cena i nie kasy: "za chuda sakiewka" 0.4 wizyty/d, plotno po 0.63 ceny; plotna nie brakuje: zapas 3 791 (miasta 1 941, karawany 1 223, wozy 579 - F:46289), miast bez plotna 12.6, z nadwyzka 28 (F:46323). Na wizyte druzyna bierze plotna za 8.9 doby i skory za 8.2 doby swojego zuzycia, a kolejka obu to ok. 34 doby - to samo dla towaru dobrze (plotno) i gorzej (skora, 18-23 miast bez) dostepnego, wiec waskim gardlem sa wizyty, nie polki [S z P]; sukno (polki puste) - 3.2 doby na wizyte, kolejka 74 doby. | **T3-K1 (`SutlerClothing`):** markietan przywozi odziez. Druzyna w polu (te same, ktore obsluguja markietani: `Sutlers.One`, `Sutlers.cs:203` - nie w osadzie, nie w bitwie, nie na morzu) raz na dobe kupuje cale sztuki swojej potrzeby (skora, sukno, plotno) z polki tego miasta, do ktorego markietani wplacaja (`NearestTown` - najblizsze miasto niebedace w wojnie z druzyna; bez takiego, oblezone albo w oknie lupu L3 - nic), ta sama metoda zakupu co przy wyjezdzie (`ArmyClothing.Buy`: najtansza sztuka, cena targu tego miasta dla tej druzyny), z sakiewki ponad rezerwe napraw, PRZED czescia markietanow (jak w OnLeft: odziez przed "zyciem"). Zloto sakiewka -> kasa miasta (te same liczniki: MoneyLedger NCloth, TownFolk.Sale, SoldierPay.Hold). Wyjazd z miasta - bez zmian. |
| T3-3 | Skora do wojska 62/d (prog 75) | **TAK.** Narosla 85.4: zalogi 28.05 -> wziely 28.4 = **100%**; druzyny 57.4 -> 33.5 = **58%** (kolejka 1 965, +19%). | **[P]** Ten sam mechanizm co plotno (T3-2). Do tego skory jest na styk (T3-5): zrobiono 183/d, zuzyto 198/d (d106-120), zapas 2 304 -> 2 074 (F:46287: w miastach 812, karawany 808, wozy 422); po T3-K1 druzyny wezma ok. +24 szt./d - zapas skory w miastach zejdzie w ok. 3-5 tygodni [S]. | **T3-K1** (dowoz) **+ T3-K2 / K5** (wiecej skor surowych). Sam T3-K1 przesunie brak skory z kolejki wojska na polki miast. |
| T3-4 | Garbarze "bez zysku" 60.5 miast (prog 50), cykle warsztatow bez skory 52.6/d (prog 50) | **NIE jako osobny problem** (garbarze), **TAK na granicy** (cykle 52.6, N 108). K3 dziala: garbowane dzieki cenie wywozu 182 szt./d w 21 miastach, skora wywieziona kontraktami i wozami 57.5/d (N 0), miast bez skory 18.3 (N 25.8). | **[P]** Skory surowe sa juz zuzyte: produkcja 493/d, zuzycie 463/d (0.94; okno d57-112: 1.00), zapas 6-7 dni (N 8 037 = 18 dni), indeks 1.41-1.53 (N 0.85). Skora zrobiona 183-191/d = 2.4 x 440-460 skor surowych, czyli tyle, ile skor jest. Garbarz "bez zysku" ma skory, ale przy skorze 0.62 i skorach surowych 1.5 ceny nie oplaca mu sie - to podzial skor rzadkich miedzy miasta, nie strata; wiecej zysku garbarzy nie da ani sztuki skory wiecej. Cykle bez skory: w 18-23 miastach bez skory. | **Bez zmian** dla garbarzy (miara narzedzia do wymiany - T3-N-2). Cykle warsztatow - skutek z T3-K2 (wiecej skor surowych). |
| T3-5 | K5: zwierzeta na rzez pod regulator - czy teraz | **TAK (juz w lecie).** Wieprze: produkcja 201, zuzycie 205/d (1.02), stos 7 dni; owce 161/161 (1.00), 7 dni; krowy 87/85 (0.98), 19 dni; mieso: zuzycie 1.06 x produkcja, zapas 6 429 (d57) -> 4 171 (d120) (wedlug ksiegi towarow; regulator liczy wlasnym spisem stosu - wynik moze roznic sie o kilka %; F:46283, F:46300, F:46272, F:46292). Garbarze "brak surowca" 34.5 miast (d91-105: 38, d106-120: 31) - warunek K5 z CHANGELOG (> 30 miast przez >= 28 dob) spelniony juz w lecie. | **[P]** Regulator wsi pomija zwierzeta: `HarvestValve.Eligible` = `IsTradeGood && !IsFood && !IsAnimal` (`HarvestValve.cs:175`); w decyzji 3 (F:43623) 17 towarow, bez wieprzy, krow i owiec. Wlasna regula kroku regulatora (zuzycie >= 98% produkcji i stos < 30 dni) jest spelniona przez wieprze i owce w oknach d29-84 i d57-112 (0.99-1.01 przy 7-8 dniach) i przez krowy w d91-120. Rzemieslnicy BK (uboj) sa ograniczeni wsadem, nie rekami: stos wieprzy 7 dni i zuzycie = produkcja [S z P]. Zima (D): zwierzeta -37%, skory surowe 491 -> 305/d (ROZPOZNANIE-TOWARY-2 A6-5). | **T3-K2 = K5 (`AnimalsUnderValve`):** przy kluczu `Eligible` dopuszcza zwierzeta rzezne - `IsAnimal`, nie wierzchowce (`IsMountable`) i nie juczne (kategoria PackAnimal); w ROT to wieprze, krowy, owce. Ta sama regula co towary (krok x1.3 przy >= 98% i stosie < 30 dni, sufit `VillageOutputStep`, ciecie dopiero przy stosie > 60 dni, N5-D przycina krok do zuzycia). Wlaczenie klucza w biegu pomiaru: zwierzeta startuja z SA/SB = -1 (pierwsza decyzja po pelnym oknie) - nie z "stos 0", ktore `Get` daje towarowi nowemu w swiecie (`:194`). Skutek [S]: wieprze i owce x1.3 od decyzji 2 -> ok. +60 wieprzy i +48 owiec/d -> ok. +95 skor surowych (+20%) -> ok. +40 skory/d; mieso ok. +80/d. |
| T3-6 | Narzedzia wsi 65.5/d (prog 66.7, T2-K1-6 NIE) | **NIE.** Wsie: 60.7 (d91-105) -> 70.3/d (d106-120), rosna; budowy 25.0/d (N 20.8; straz <= 41.6 - daleko). | **[P]** Wsie kupuja tyle, ile kuznie zrobia: narzedzia z warsztatow smithy 110.7/d (N 128.1), cykle 27.7 vs 32.0/d, pracuje 19.4 vs 21.3 z 22-23 kuzni - roznica -17.4/d to prawie caly brak wsi (-17.8/d). K1 doklada budowom +4.2/d. Czemu kuznie pracuja rzadziej - nie wiem (inna kampania; ruda: miast bez rudy 12 vs 13). Kuznia narzedzi bierze rude, nie drewno - K1 jej nie dotyczy. | **Bez zmian.** Polowa "wsie >= 66.7" progu T2-K1-6 mierzy produkcje kuzni, nie K1 - do wymiany (T3-N-3). Obserwacja poza zakresem: 91% narzedzi swiata lezy w jukach karawan (2 030 z 2 223, N 3 131 z 3 324). |

---

## 2. Kolejnosc kodu

Kazda zmiana z wlasnym kluczem MCM (off = 6e6313c5), progi w `tools/sprawdz_logi.py` tylko w grupie `towary3` (`towary3_main`).

1. **T3-K1 - markietan przywozi odziez** (`SutlerClothing`; T3-2, T3-3; posrednio T3-1). Najwiekszy skutek: oba progi wojska. `Sutlers.One`: po warunkach (aktywna, poza osada, bez bitwy, nie na morzu) i przed `MenPurse.CampPlan` -
   zakup jak `ArmyClothing.BuyForParty(mp, town, sakiewka - rezerwa napraw)` w miescie markietanow (tylko niewrogie, nie oblezone, `CitySack.NoSpendCloth`); gracz - ta sama regula (napis po angielsku, np. "Sutlers brought your men ... from X for N denars").
   Liczniki: dopisek w linii "Odziez wojska (150)": `markietani (SutlerClothing) tak: druzyn z zakupem N, kupily skora/sukno/plotno a/b/c za X zl w M miastach; druzyn z potrzeba bez towaru w miescie markietanow: skora a, sukno b, plotno c; miasto oblezone N`
   i rozbicie "potrzeba narosla: druzyny X, zalogi Y" oraz brak per towar przy wyjezdzie (dzis log podaje tylko "bez towaru" dla dowolnego towaru). Uwaga: `ArmyClothing.Daily` pisze linie PRZED `Sutlers.Daily` (`ArmouryBehavior.cs:1797` / `:1805`) - zakupy markietanow wejda do linii nastepnej doby (srednia 30 dob bez zmian) albo licznik osobny.
   Skutek [S]: druzyny plotno 45.5 -> 62-70/d, skora 33.5 -> 45-55/d (skora ograniczona polkami); wojsko razem plotno 69 -> 85-93, skora 62 -> 74-83 (z T3-K2); kolejki nie rosna (nowa kampania - nie narastaja); zapas plotna przestaje rosnac, tkacze "bez zysku" 35 -> 20-25 miast, zuzycie lnu 0.84 -> 0.95-1.0 - regulator podniesie len sam; zloto: ok. 3-5 tys. zl/d z czesci markietanow na odziez (to samo zloto, inna pozycja).
   Ryzyko: polki miast przy wojskach zejda szybciej (cena rosnie od zapasu - hamulec); zalogi tych miast biora z tej samej polki (straz T3-K1-6); warsztaty zbrojne - straz T3-K1-7; sukno (polki puste) dalej czeka; wlaczenie na starym zapisie z kolejka 2-6 tys. - pierwsza doba wykupi polki przy wojskach (nowa kampania - brak).
2. **T3-K2 = K5 - zwierzeta rzezne pod regulator** (`AnimalsUnderValve`; T3-5, T3-3, T3-4). `HarvestValve.Eligible` (`:175`) + start pomiaru zwierzat przy wlaczeniu klucza (SA/SB = -1). Pierwszy krok mozliwy w decyzji 2 (d85) - w biegu 120 dob skutek w d86-120.
   Ryzyko: wieprze i krowy sa tez w taborach lordow (krowy 611 - jedzenie partii) - wchodza do stosu swiata jak kazdy towar; gdy uboj nie nadazy, N5-D przytnie krok do zuzycia w nastepnej decyzji. Moze isc w tym samym biegu co T3-K1 (inne lancuchy, inne miary: T3-K1 - zakupy druzyn, T3-K2 - produkcja skor surowych i mnozniki zwierzat), kazda z osobnym kluczem i progiem.
3. **T3-N - narzedzie (bez zmiany gry):** grupa `towary3` z progami z rozdz. 3, w tym trzy miary zamiast mylacych (T2-K2-5 / PL-4 len >= 1.20, T2-K3-4 garbarze "bez zysku", polowa "wsie" T2-K1-6).
4. **Warunkowo T3-K4 (po biegu z T3-K1, nie teraz):** zuzycie miasta liczy tez odziez wojska kupiona w tym miescie (`CaravanBulk.Use` - srednia z ok. 14 dob: zalogi, wyjazdy, markietani) - jedno miejsce dla zapasu docelowego karawan, progu nadwyzki i popytu ceny (`RawPrice.UseDemand` dzis: mieszczanie + rzemioslo, bez wojska - `RawPrice.cs:122`). Tylko gdy po T3-K1 miast bez plotna > 20 albo bez skory > 25, albo "druzyn bez towaru w miescie markietanow" > 30% prob.

**Bez zmian:** T3-1 (regulator lnu), T3-4 (garbarze "bez zysku" jako taki), T3-6 (narzedzia wsi).

---

## 3. Progi grupy `towary3` (propozycja; srednie ostatnich 30 dob, wiaza przy dopisku "tak")

| id | miara | prog | fin29 |
|---|---|---|---|
| T3-K1-0 | dopisek markietanow w "Odziez wojska (150)" | codziennie, klucz tak | - |
| T3-K1-1 | plotno do wojska (Towary linen, "odziez wojska (150)") | >= 85 szt./d | 69.0 (potrzeba 95) |
| T3-K1-2 | skora do wojska (Towary leather) | >= 75 szt./d | 61.9 (potrzeba 85) |
| T3-K1-3 | druzyny: plotno kupione / narosle (dopisek) | >= 85% | 63% |
| T3-K1-4 | kolejka plotna wojska d120 i wzrost d90 -> d120 | <= 1 200 i <= +2% | 2 417, +18% |
| T3-K1-5 | kolejka skory wojska d120 i wzrost | <= 1 000 i <= +2% | 1 965, +19% |
| T3-K1-6 | straz zalog: zalogi miast + zamkow plotno / skora | >= 22 / >= 26 szt./d | 23.5 / 28.4 |
| T3-K1-7 | straz warsztatow: cykle bez skory (Warsztaty) | <= 65/d | 52.6 |
| T3-K1-8 | INFO: zapas plotna d90 -> d120, tkacze "bez zysku", zuzycie/produkcja lnu w ostatniej decyzji, zloto markietanow na odziez | INFO | 3 491 -> 3 791; 35; 0.84 |
| T3-K2-0 | regulator: wieprze, krowy, owce w linii decyzji (klucz tak) | tak | brak |
| T3-K2-1 | skory surowe - produkcja (Towary hides) | >= 530 szt./d | 493 |
| T3-K2-2 | skora zrobiona (Towary leather, produkcja) | >= 200 szt./d | 187 |
| T3-K2-3 | straz: wieprze, krowy, owce - stos <= 30 dni zuzycia; mieso - INFO | <= 30 dni | 7 / 19 / 7 |
| T3-K2-4 | garbarze "brak surowca" (Rzemioslo 148) | <= 25 miast | 34.5 |
| T3-N-1 | len: mnoznik w kazdej decyzji (zamiast >= 1.20) | >= x1.00 | x1.00 (TAK) |
| T3-N-2 | skory surowe zuzyte / produkcja (zamiast "garbarze bez zysku") | >= 0.90 | 0.94 (TAK) |
| T3-N-3 | narzedzia: budowy / produkcja narzedzi (zamiast "wsie >= 66.7") | <= 25% | 19% (N 14%) |

---

## 4. Czego nie wiem

- Brak per towar przy wizytach druzyn - log podaje tylko "wizyt bez towaru na polce" dla dowolnego z trzech; wniosek "waskie gardlo to wizyty" stoi na rownych 34 dobach kolejki plotna i skory i na 100% zalog (T3-K1 dopisze liczniki per towar).
- Ktore druzyny nie bywaja w miastach (brak danych per druzyna); 27 druzyn i zalog na suficie 120 dob w d120.
- Czy rzemieslnicy BK ubija +30% wieprzy i owiec - wnioskuje z 7-dniowego stosu i zuzycia = produkcji; jesli nie, N5-D przytnie krok.
- Definicja `ItemObject.IsAnimal` (TaleWorlds.Core) nie jest w dekompilacji `dec32\cs_full` - przy kodzie sprawdzic, ktore przedmioty ROT ja maja (gra uzywa obok niej osobno `IsMountable` i `PackAnimal`).
- Czemu kuznie narzedzi pracuja w fin29 o 13% rzadziej (T3-6).
- Poza zakresem, do obserwacji: po K2 wsie wydaja 5.7% koszyka (4.1 z 73 tys. zl/d; N 19%), plotno zatrzymane progiem zostawia w kiesach wsi ok. 40 tys. zl/d (szacunek K2: 5-7 tys.) - skutek zamierzonego pierwszenstwa wojska; sukno dla druzyn 23% potrzeby (polki).
