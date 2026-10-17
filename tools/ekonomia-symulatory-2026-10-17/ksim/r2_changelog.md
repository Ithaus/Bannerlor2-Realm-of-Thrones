## 2026-10-17 (poludnie, KS-R3 - port symulatora, runda 2) - KALORIE CHCIANE WSI RAZ, KUPCY ZYWNOSCI Z DALSZYCH MIAST I Z ZAPASU KUPCOW PRZY PUSTEJ POLCE, PRACA DOMOWA NAJPIERW NA JEDZENIE I CHLEB ZAMIAST MIESA

**Mod:** Armoury | **Galaz:** `w-toku/noc-ks3` (po 909aba37) | **Tor:** L (NIC nie wgrane; Jeff gra na WGRANIU 74 bez KS; gry nie uruchamialem, folderu gry, zapisow i ustawien nie ruszalem) | **Klucze:** bez nowych | **Zapis:** bez zmian (ks4)

**Dla Jeffa (prosto):**
- Druga runda symulatora z regulami przepisanymi z kodu. Znalazlem i poprawilem cztery rzeczy:
  1. Blad licznika: gdy wies szukala reszty koszyka w miastach obok, kazde miasto obok liczylo jej potrzebe jeszcze raz. Wychodzilo, ze wies chce 2-3 razy wiecej jedzenia, niz chce - "bez pokrycia" bylo o polowe zawyzone, a racje z pracy domowej za wysokie. Teraz potrzeba liczy sie raz.
  2. Kupcy jedzenia szukali zboza tylko w 12 najblizszych miastach. Gdy zadne z nich nie mialo nadwyzki (Qarth, Meereen, Polnoc), nikt nie jechal dalej, choc w Volantis i w Reach zboze lezalo. Teraz, gdy 12 najblizszych nic nie da, kupiec sprawdza do 4 dalszych miast w zasiegu drogi.
  3. Kupcy placili tylko z pieniedzy ponad zapasem kupcow, a kasy miast stoja zwykle przy samym zapasie (nadwyzka idzie do pana). Przy pustej polce (mniej niz zapas celu) moga teraz placic z calej kasy, najwyzej 15% na dobe - pieniadze wracaja do kasy, gdy jedzenie kupia mieszczanie i wsie.
  4. Plan: biedna wies, ktorej rak nie starcza na caly wlasny koszyk, glodzila sie przy pelnym koszyku (mieso, ser, wino, plotno). Teraz rece w domu ida najpierw na jedzenie, a gdy i tego za malo - wies robi kalorie najtanszym swoim towarem (chleb zamiast miesa). Wydajnosc ta sama, nic z niczego.
- Wyniki symulatora (start jak w rundzie 3): miast bez zboza 12% -> 5%, wsie bez pokrycia Westeros 14.4% -> 4.3%, Essos 11.3% -> 2.0%, kontrola D+7 42% -> 55% cen w pasmie, kupcom brakuje kasy przy 12% zamowien (bylo 41%). W Planie wsie bez pokrycia Westeros 7.5% -> 3.7%, Essos 3.2% -> 1.7%.
- Uczciwie: nie wszystkie bramki przechodza. Za duzo miast ma zboza na mniej niz 2 dni (18%, cel 10%), ceny sera, masla, ryb i oliwek w Westeros sa nadal za wysokie, kiesy wsi w oknie spadaja (wsie wydaja oszczednosci z rundy 3 na reszte koszyka). To wszystko jest tylko w symulatorze - w grze niesprawdzone.
- Nic nie jest wgrane. Twoja gra i zapis sa bez zmian.

**Problem (port `ksim/sim/r3port.py` + harness `kmoney/h3port`, 3 ziarna, okno D+22..D+49, poczatek A = doba A z polkami i stara gospodarka rundy 3 do D):**
(1) `TownFolk.Ks.cs` `ZcTake2` dopisywal kcal i wartosc chciane przy kazdym miescie obok (drabina 2, F2) - `KcalW` wsi do 2-3 x koszyka: "kupione" w linii ZC4 i NKS-8c za nisko, `_zvBought` za nisko, `HomeKcal` (racje) za wysoko; w porcie bez pokrycia W 14.4% / E 11.3% przy tych samych zakupach (po poprawce 7.6% / 3.4%);
(2) `KsFoodCarts.cs` - kandydaci zrodla tylko 12 najblizszych miast: Qarth 191 z 240 prob "bez nadwyzki", Meereen, Deepwood Motte, Last Hearth podobnie, nadwyzka Volantis i Reach poza lista;
(3) budzet lotu 15% x (kasa - zapas kupcow) - kasy miast przy zapasie (zawor): Sunspear (kasa ponizej zapasu), Planky Town, Meereen, Lorath, Arbor - "kasa celu za mala" 41% zamowionych;
(4) `StartCalibration.Core.cs` `MoneyStep` - przy surowiec / V0 > HomeMax czesc pokryta = hw x V0 / surowiec calego niezaplaconego koszyka (z plotnem, suknem, skora) i bez zamiany drogich kcal: 54 okregi z bez pokrycia > 20% (oliwnik w Reach: koszyk 4550 zl/d przy sprzedazy 1114, surowiec / V0 1.31), Plan bez pokrycia W 7.5%.

**Zmiany:**
- **ZC4 (`TownFolk.Ks.cs`):** `ZcBuy.Alt` - drabina 2 w miastach obok bez drugiego liczenia `KcalW` i `ZlW` (pomiar i racje; zakupy bez zmian).
- **KS-H (`KsFoodCarts.cs`):** `_candFar` (reszta miast rynku wedlug dob); gdy z 12 najblizszych nie ma kandydata (nadwyzka, zasieg, wojna, cena) - najwyzej `NCandFar` = 4 dalsze w zasiegu, te same warunki; budzet lotu przy polce + w drodze < T* x popyt = 15% calej kasy na dobe (`budgetAll`), inaczej jak dotad 15% ponad zapasem; `spent` - wydane dzis na wszystkie towary celu. Linia KS-H (przed "probka"): "z dalszych miast ... wozow, z zapasu kupcow ... wozow, ... zl" - regex NKS-22 bez zmian (sprawdzone).
- **Krok 8c (`StartCalibration.Core.cs` `MoneyStep`, klucz Ks Alpha Net jak 8b):** czesc pokryta kcal = hw x V0 / surowiec JEDZENIA (rece najpierw na kcal, potem rzeczy); gdy surowiec jedzenia > HomeMax x V0 - pozycje drozsze za kcal niz towar zastepczy okregu (zboze, ryby, dziczyzna, daktyle) robione jako towar zastepczy kcal za kcal, od najdrozszej, tylko do HomeMax x V0; kcal 8c w "kcal zastepcze" linii pieniedzy Planu.

**Symulator (tylko scratchpad):** ZcTake2 jak C# (kwfix), KS-H dalsze miasta (candx) i budzet z calej kasy przy pustej polce (res_short 0), liczniki NKS-22 jak 909aba37; kalibracja symulatora: popyt ceny towarow innych niz zboze na rynek = popyt zaplacony Planu ON (qvcal 3 - w grze Zc4Build bierze koszyki Planu; dopasowanie world.json nie obnizalo czesci zaplaconych rownych 1 - ryby wsi 3035 wobec 1834 szt./d w ZC4), drogi morskie Lys i Evenfall Hall (world_r2.json - w swiecie symulatora nie mialy zadnej drogi).

**Port symulatora (3 ziarna, Plan harness z 8c, okno D+22..D+49):**
- FINAL, poczatek A: S-G1 zboze W 0.986 E 0.991 TAK; S-G2 12/28 poza pasmem NIE; S-G3 < 0.5 dnia 5% (TAK), < 2 dni 18% (NIE, cel 10%), q90 8.9 d, > 30 dni poza krainami wojny 0.3 miasta; S-G4 zboze W 1.25 E 1.25, reszta 12/26 NIE; S-G5 0/28 TAK; S-G6 W 4.3% E 2.0% TAK; S-G7 -0.39 / -0.55 %/d NIE (kiesy rundy 3 wydawane w drabinie 2); S-G8 16.4k szt./d, 10.1 dob, 171k w drodze, kasa za mala 12%, tozsamosci 0 TAK; S-G9 55% TAK.
- FINAL, poczatek start: S-G6 W 4.0% E 1.6% TAK, S-G9 83% TAK, S-G3 7% / 14% / q90 12.4 NIE, S-G7 -0.16 / -0.29 NIE.
- 909aba37 (te same kalibracje symulatora), poczatek A: S-G3 8% / 18%, S-G6 W 13.1% E 11.8%, kasa za mala 20%.
- Bez kupcow (kontrola): miast bez zboza 34% (runda 3 w grze 39%).
- Czulosc: bez tygodniowego pomiaru alfa w biegu (Plan z harnessu trzymany) - wsie W bez kiesy 21%, bez pokrycia W 12.3% (symulator nie liczy Planu co tydzien od nowa - nie odtworzy przesuniecia rak za alfa).
- Harness (ks_dump + ks-budzety rundy 3, alfa netto rundy 1, 5 Planow cieplych): Plan bez pokrycia W 3.7% (cel 3.0), E 1.7% (cel 3.5); na targ W 0.98-1.04 poza mieso 1.68, jaja 2.92, marchew 1.29, miod 1.56 (towary uboczne); E 0.98-1.01 poza mieso 1.34, jaja 1.78, miod 2.05; Plan 12-13 s.

**Ryzyka / co sprawdzic:** (1) towary inne niz zboze: popyt ceny w symulatorze to kalibracja (popyt zaplacony Planu); ser, maslo, ryby, oliwki Westeros w symulatorze drozsze 1.5-5 x - ryby Westeros w grze daja 84% podazy Planu (ksiega rundy 3), wiec brak ryb moze byc prawdziwy; (2) budzet z calej kasy przy pustej polce - kasa miasta moze zejsc ponizej zapasu kupcow na kilka dob (zakupy z wozow wsi placi kasa miasta) - patrzec na linie kas miast; (3) dalsze miasta - dluzsze wozy (srednio 10 dob, bramka 12); (4) 8c zmienia racje wsi (czesc pokryta z jedzenia) - patrzec na NKS-15 (glod) i NKS-26 (podloga spichlerza); (5) kiesy wsi spadaja w oknie (drabina 2 wydaje oszczednosci ponad 7 dob budzetu) - NKS-13 dryf kies moze nie przejsc.

