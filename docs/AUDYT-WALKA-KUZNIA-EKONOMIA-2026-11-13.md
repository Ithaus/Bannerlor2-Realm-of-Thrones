# Audyt walki, kuzni i ekonomii - 13.11 (stan WGRANIE 226)

Polecenie Jeffa (13.11): sprawdzic pozostale sprawy ekonomii, czy walka i obrazenia broni, lukow i kusz
maja sens wzgledem pancerza, oraz czy kucie broni, pancerzy i lukow ma sens (materialy, ich ilosc, naprawa,
koszt, wyzsze tiery). Dlugosc kucia - bez zmian, nie oceniana.

Jak to zrobiono: tylko czytanie kodu (galaz n367c = WGRANIE 226), XML gry i modow, dekompilacji RBM i logow.
Gry nie uruchamialem, kodu ani plikow gry nie zmienialem. Kazdy tor mial audytora i niezaleznego weryfikatora.
W raporcie sa tylko ustalenia POTWIERDZONE albo CZESCIOWO potwierdzone. Tam, gdzie liczba jest wyliczeniem,
a nie pomiarem z bitwy, jest to napisane.

Legenda wielkosci zmiany: **S** - kilka linii / jeden klucz, **M** - jedna latka lub galaz przepisu, **L** - zmiana w Planie KS lub kilku plikach.

---

## 1. WALKA

### Co dziala sensownie

Werdykt ogolny: po serii 413-423 (WGRANIE 207) walka jest w miare sensowna. Nie ma absurdu typu
"chlopski luk zabija rycerza w plycie jedna strzala", "plyta odporna na wszystko" ani "kopia nic nie robi".

Pancerz korpusu po drabinie 423 (potwierdzone w logu sesji Jeffa 21-55-34): ubranie 12, przeszywanica 26,
ciezka skora 48, kolczuga 55, luska 62, plyta 91. RBM ma fabryczny `config.xml`.

Obrazenia w HP, trafienie w piers, pelny zamach (wyliczenie wzorami RBM 4.3.5):

| Bron (umiejetnosc 60) | goly | ubranie | przeszyw. | sk. ciezka | kolczuga | luska | plyta |
|---|---|---|---|---|---|---|---|
| miecz 1h ciecie | 54 | 21 | 8 | 6 | 6 | 5 | 4 |
| miecz 1h pchniecie | 97 | 72 | 40 | 17 | 16 | 14 | 11 |
| topor 2h | 120 | 90 | 50 | 18 | 17 | 15 | 12 |
| buzdygan 1h | 63 | 45 | 28 | 22 | 20 | 19 | 15 |
| mlot / buzdygan 2h | 134 | 115 | 83 | 47 | 44 | 41 | 33 |
| wlocznia 1h pchniecie | 108 | 87 | 59 | 18 | 18 | 16 | 12 |
| kopia 12 m/s | 207 | 187 | 159 | 112 | 97 | 81 | 25 |

Pociski z 20 m (goly / przeszywanica / kolczuga / plyta):
- luk 60 lb, grot szeroki: 60 / 5 / 3 / 2
- luk 100 lb, iglica: 86 / 54 / 18 / 4
- dlugi luk 160-180 lb, iglica: 113-127 / 81-96 / 45-59 / 5-6
- kusza 300 lb: 191 / 160 / 123 / 36

Co sie zgadza z historia:
- ciecie nie bierze kolczugi ani plyty, przeszywanica dobrze trzyma miecz 1h;
- obuch najlepszy na plyte: mlot 2h zabija rycerza w 2-3 ciosach, buzdygan w 4-7, miecz w 10-25;
- plyta zatrzymuje kazdy luk (max 6 HP), rusza ja dopiero ciezka kusza z bliska albo kopia w galopie;
- kolczuga slaba na ciezkie luki z bliska, mocna na lekkie; 20 -> 100 m zabiera ok. 25% energii.

Nasze latki 415, 416, 423b laczą sie z RBM poprawnie (mnoza to samo pole pancerza, nic nie liczy sie dwa razy).

### Co odstaje od sensu

1. **Kusza ROT jest slabsza od luku (POTWIERDZONE).** Najczestsza kusza w ROT, `crossbow_a`, ma w RBM naciag
   120 lb (`RBM/ModuleData/RBMCombat_ranged.xml:53`). Energia wzorem RBM: kusza 120 lb 119 J, luk 100 lb 127 J,
   dlugi luk lowland_yew 160 lb 189 J. W kolczuge belt daje ok. 18 HP, dlugi luk 45-59 HP. Ze 100 m: kusza 5 HP,
   dlugi luk 180 lb 32 HP. `crossbow_a` to ponad 75% kusz w zestawach ROT (266 gniazd; reszta kusz razem ok. 79).
   Kusznik ROT jest w praktyce slabym lucznikiem z wolnym przeladowaniem. Liczby HP to wyliczenie - w logach nie
   ma ani jednej linii HIT belt -> kolczuga.
   Haczyk: w RBM `crossbow_a` to swiadomie lekka kusza (crossbow_fast, waga 1, flagi Civilian i Stealth, cena 2000).
2. **Tarcze: strzaly prawie nic, oszczep rozbija w 1-2 rzutach (CZESCIOWO - to celowe ustawienie, nie blad).**
   `Settings.cs:218-220`: strzaly w tarcze 1%, miotane 100%. RBM mnozy obrazenia tarczy od oszczepu x25, od
   topora rzucanego x10, plus x2 przy fladze BonusAgainstShield. Tarcze maja pancerz 4-6 przy HP 300-510.
   Ile strzal trzeba na tarcze (audytor szacowal ok. 3700) - nie przeliczone dokladnie.
3. **Szyja nadal moze byc smiercia od jednej strzaly (CZESCIOWO).** Ulga 423b (75% pancerza wobec iglicy)
   dziala tylko na piers i brzuch (`BodkinSoft423.cs:145`). W szyi zostaje polowa pancerza wedlug RBM.
   Wyliczenie: luk 100 lb w szyje zbrojnego w kolczudze ok. 100 HP. Pomiaru brak - w logach po 415 nie ma
   ani jednego trafienia strzala w szyje (sa tylko 3 od oszczepu: 5, 22, 47 HP). Przed 415 mediana byla 101 HP.

Drobne (gust, nie absurd):
- ciezka skora (48), kolczuga (55) i luska (62) w walce wrecz roznia sie o 1-3 HP na cios;
- zbroja Nieskalanych (99) i Dothrakow (72-81) chronia wrecz lepiej niz plyta rycerska (91) - skutek Twojej
  decyzji 4, klucz `ArmourLadderDothrakiUnsullied` = TAK to odwraca (99 -> 58, 77 -> 39);
- latki 415/416/423b nie dzialaja przy trafieniu w twarz i pod pache (RBM nadpisuje tam pancerz) - rzadkie;
- 291 zestawow nadal strzela grotem szerokim (hunting_bow, steppe_bow) - historycznie sensowne;
- RBM: naciag zawsze pelny, umiejetnosc strzelca nie zmienia energii - nie ruszac teraz.

### Propozycje (od najwazniejszej)

1. **Mocniejsze kusze w zestawach wojsk** - podmiana `crossbow_a` na `crossbow_b` (160 lb, ok. 159 J) albo
   `crossbow_c` (200 lb) w zestawach ROT, wzorem 420 (zmieniamy zestawy, nie dane broni).
   Dlaczego: kusznik ma bic kolczuge mocniej niz lucznik, a nie 3x slabiej. Ryzyko: zmiana danych samej
   `crossbow_a` ruszylaby tez bron cywilna i kuznie (`ForgeBill.cs:534`, `Recipes.Mass.cs:698`) - dlatego
   wariant zestawow. Wielkosc: M. Przed decyzja: pomiar HIT belt -> kolczuga w bitwie.
2. **Pomiar szyi** - w nastepnym autotescie bitwy policzyc mediane HIT Arrow/Bolt [Neck]. Dopiero przy medianie
   ok. 90-100 rozszerzyc 423b o szyje (jeden warunek w `BodkinSoft423.cs:145`) albo podniesc
   `NeckFromHelmetShare` 0.20 -> 0.30. Ryzyko: male. Wielkosc: S.
3. **Tarcze (gust)** - jesli chcesz mniejszej przepasci: strzaly 1% -> 5-15%, miotane 100% -> 40-60%.
   Ryzyko: ten sam procent steruje zuzyciem tarczy gracza (FieldCraft). Wielkosc: S (klucze MCM).

### Decyzje dla Jeffa (walka)
- Kusze: podmiana w zestawach na `crossbow_b` czy `crossbow_c`, czy zostawic?
- Tarcze: zostaje "tarcza nie jest do rozstrzeliwania" (1% / 100%), czy zblizyc?
- Nieskalani/Dothrakowie: zostaje Twoja decyzja 4, czy wlaczyc klucz drabiny?

---

## 2. KUZNIA

### Co dziala sensownie

Ceny surowcow (log sesji Jeffa): sztaba Iron1-2 po 1 d, Iron3 2, Iron4 3, Iron5 4, Iron6 (stal zamkowa) 6 d,
kazda 0.5 kg; skora 40 d / 10 kg, len 100 d / 10 kg, drewno 4 d / 100 kg.

1. **Pancerze, luki, kusze, strzaly i belty licza sie z masy.** W zadnej grupie surowiec nie kosztuje
   wiecej niz sztuka. Wyzszy tier = lepsza stal, ilosc rosnie z waga (surowiec / wartosc):
   - napiersnik plytowy t5 (ok. 25 kg): 270 / 1361 d; t6 (ok. 34 kg): 514 / 2835 d;
   - helm plytowy t4 12/39, t5 25/66, t6 45/111 d;
   - kolczuga t4 113/864, t5 212/1402, t6 304/1621 d.
   Plyta = 80% metalu swojego tieru + 20% tieru nizej (`Recipes.Mass.cs:275-280`). Tier 6 nigdzie nie jest
   tanszy od tieru 5 przy tej samej wadze.
2. **Luki bez metalu**, klepka z drewna: od x2 przy t1 do x18 przy t6. noble_long_bow t6 = 19.8 kg drewna +
   0.3 kg skory + 75 g lnu.
3. **Kusza** = lozysko z drewna, zamek z Iron2, lek stalowy przy mocnych (crossbow_g: 1.98 kg drewna +
   0.31 kg Iron2 + 0.62 kg Iron6).
4. **Amunicja tania**: 3 kolczany strzal t1 = 6 d przy wartosci 33; strzaly t6 = 34 d przy wartosci 66.
5. **Naprawa** bierze najwyzej 20% przepisu razy szkode; pelna naprawa nie przekracza ok. 20% wartosci.
6. **Przetop** daje najwyzej 45% metalu przepisu - nie ma petli kucie-przetop.
7. **Bron reczna** (czesci kuzni gry): miecz t5 za 104 d, material ok. 25-30 d.
8. Legendy: kazdy kg x4 i caly metal z Iron6; rzadkie luki zablokowane.

### Co odstaje od sensu

1. **TARCZE - prawdziwy absurd (POTWIERDZONE).** Tarcze zostaly na starym przepisie w CALYCH sztukach lnu
   i skory po 10 kg (`Recipes.Mass.cs:82`: tarcza wypada z przepisu z masy; `Recipes.cs:498-508`).
   Przyklady (waga, wartosc -> surowiec):
   - desert_round_shield t2, 7.3 kg, wartosc 19 d -> 2 bele lnu = 20 kg = **200 d**;
   - steel_round_shield t6, 4 kg, wartosc 128 d -> 4 skory = 40 kg = **160 d** (log: "aksamit 0, skora 4");
   - stark_shield t6, 4.7 kg, wartosc 132 d -> 5 skor = 50 kg = **200 d**.
   Surowiec przewyzsza wartosc w 178 z 183 tarcz (3-14x). Ten sam absurd jest w CRAFT BK
   (`FletchForge.cs:256-257, 450-468`) i w naprawie wlasnymi rekami (`Recipes.Mass.cs:643-646`):
   naprawa 75% szkody tarczy t2 za 15 d zjada ok. 30 d lnu. Tanie "hides" nie zastapia skory.
   Do tego wycena tarczy (`ArmsPricing.cs:377-378`: metal 10%, drewno 80%, skora 10% wagi) liczy zupelnie
   inaczej niz przepis - w skorze rozjazd ok. 100x (0.4 kg wobec 40 kg).
   Linia startu ShieldBillCheck porownuje tylko metal i drewno, wiec tego nie wylapala (`FletchForge.cs:205-224`).
2. **Kucie pancerzy jest bardzo zyskowne (CZESCIOWO - to skutek Twojej decyzji o szybkiej kuzni).**
   Wartosc sztuki = (surowiec + praca mistrza) x 1.25 (`HistoricalPrices.cs:301-307`). Napiersnik plytowy t6
   26 kg to wyceniona praca 123 dni mistrza (`ArmsPricing.cs:359`), a gracz kuje go w 7.5 doby. Surowiec to
   ok. 15-25% wartosci. Kwota "ok. 2000 d zysku na sztuce" to wartosc minus surowiec - realnej ceny skupu
   u kupca nikt nie zmierzyl. Oplata kuzni zalezy od zamoznosci miasta (ForgeHireHistorical).
   Prawdziwa bariera: rafinacja Iron6 (perk Steel Maker 3, ok. 110 akcji rafinacji na pancerz).

Drobne (gust):
- skora t6 ma nity ze stali zamkowej Iron6 (`Recipes.Mass.cs:267, 282`) - zamyka kucie skory t6 za perkiem;
- crossbow_g "tier I" wymaga Iron6 i Smithing 165, bo kuznia liczy ja jak luk tej samej sily (zamierzone,
  ekonomia OK: material 14 d przy wartosci 106 d);
- luki t6: surowiec ok. 3 d przy wartosci 46-48 d - jedyna bariera to 165 Smithing i stamina, nie material.

### Propozycje (od najwazniejszej)

1. **Tarcze do przepisu z masy** (`MassCovers`: Shield = true) z ta sama regula co wycena: okucia/umbo 10% wagi
   x1.4 metalu wedlug tieru, drewno 80% x1.5 w kg, skora 10% w kg z otwartego ladunku.
   Tarcza t6 4 kg: ok. 0.56 kg metalu (2 sztaby, 12 d) + 4.8 kg drewna + 0.4 kg skory (ok. 2 d) zamiast 40 kg skory.
   Dlaczego: naprawia naraz kucie, CRAFT BK, naprawe wlasnymi rekami, przetop i "jedna wycena".
   Ryzyko: sprawdzic ShieldBillCheck, ShieldVelvetNeed, BkShieldSpendPostfix i BkMassSpendPostfix (kg otwartych
   ladunkow nie moga isc podwojnie z kwitem BK) oraz linie startu "Kuznia przetop". Wielkosc: M.
   Przy okazji: ShieldBillCheck ma porownywac tez skore i len (S).
2. **Zysk z pancerzy - najpierw pomiar**: ile gracz realnie dostaje przy sprzedazy zbroi t5-6 (skup wobec
   wartosci, glebokosc rynku, kasa miasta). Przepisow i czasu nie ruszac. Wielkosc: S (linia logu).
3. **Nity skory najwyzej z Iron3** (gust) - jedna regula w przepisie i wycenie. Ryzyko: rusza naprawe,
   przetop i warsztaty AI (WorkshopLaw.Needs). Wielkosc: S-M.
4. **Menu kuzni: "as tier VI" przy kuszach** (gust, kosmetyka). Wielkosc: S.

### Decyzje dla Jeffa (kuznia)
- Tarcze: zgoda na przepis z masy? (rekomendacja: TAK - to jedyny prawdziwy absurd w kuzni).
- Zysk z pancerzy: zostaje (Twoja decyzja 17.10 o szybkiej kuzni), czy po pomiarze ograniczac od strony sprzedazy?
- Luki t6: wystarcza bariera umiejetnosci, czy chcesz rzadkiego surowca (klepka cisowa, rog z targu)?
- Nity skory t6: zostaje Iron6 czy zelazo?

---

## 3. EKONOMIA

Zrodlo: log at-220z (`Armoury-2026-11-13_20-05-48.log`, 176387 linii) i kod n367c.

### Co dziala sensownie
- Zloto swiata stale: 219.45 -> 219.71 mln (ekonomia zamknieta).
- Ludnosc wsi rosnie: 1.06 -> 1.085 mln.
- Gersuma: kontrola "zdjete - panom - koronie = 0"; glod ludu z braku pieniedzy 0; spichlerze "z niczego" 0.
- Glod Mantarys (99.5%, bunt) to skutek oblezenia, nie bledu.
- Skarbce koron sie nasycaja (ODRZUCONE jako problem): 58.84 mln (d108648) -> 76.82 mln, ale przyrost dobowy
  spada 233 tys. -> 91 -> 53 -> 46 -> 27 -> ok. 10 tys. Kiesy wsi w sesji wzrosly 1.45 -> 5.75 mln.

### Co odstaje od sensu

1. **Wspolny korzen: Plan KS nie widzi tego, co wsie naprawde dowoza na targ (POTWIERDZONE diagnoza).**
   Ksiega dzienna (d108865): mieso W 607 szt. wobec Planu 763 (0.80), E 919 wobec 1076 (0.85); zboze W 12737
   wobec 10907 (1.17), E 17541 wobec 16217 (1.08). Polki: zboze E 5.5x zapasu docelowego, mieso W 0.23x.
   Plan mimo to uwaza zboze za brakujace (narzut x3, cena cienia W 1.65, E 2.77), a mieso za tanie
   (E 0.05, W 0.56). `Deliver.cs:89`: Plan liczy produkcje "bez zimy i czynnikow rak", a gra doklada
   ScaleRates i domieszke beta - rozjazd ma kilka zrodel.
2. **Swinie nie rosna (CZESCIOWO).** Cel z cen cienia wskazuje zboze; hamulec 479-e (10% rak zajecia na sezon,
   `A7s.cs:81, 120-122`) i zawrot 479-g cofaja przejscia wskazane przez polki. Log d108866: zaczete 102 bloki
   (0.70%), hamulec wstrzymal 179 (583894 rak). Przy okazji: Thenn i Craster's Keep maja 100% udzialu regionu
   przy progu 5% - do sprawdzenia, czy to zamierzone dla malych regionow.
3. **Essos: mieso na podlodze ceny 0.05 mimo braku (POTWIERDZONE mechanizm).** E mieso popyt 926224 > podaz
   904293, a cena 0.050. Normalizacja cen jest zdominowana przez zboze (ok. 15x wiekszy popyt), wiec kazdy
   towar o mniejszym wzglednym braku traci i spada na podloge. Na 0.05 stoi 8 towarow (mieso, jajka, jablka,
   marchew, pomarancze, oliwki, miod, skory).
4. **Skoki miary zbieznosci Planu (CZESCIOWO).** Towary przy granicy ceny "mrugaja" z runde na runde,
   wzmocnione przez miare targu. Mechanizm prawdopodobny, ale nie zmierzony.
5. **Gersuma 483 prawie nic nie zdejmuje (POTWIERDZONE diagnoza).** 336 przegladow, 309 konczy sie
   "zapas okna do progu". Podstawa to najnizsza kasa z 14 dob (`DsTowns.cs:1148`), a raty tygodniowe spychaja
   to minimum ponizej progu. Kasy miast 17.15 mln, ponad prog gersumy tylko 3.23 mln (30 miast). Reszta to
   z zalozenia 1.5 x zapas kupcow (8.48 mln) plus 28 dob firm.
6. **Duzy przelew na poczatku sesji (nowe, pominiete przez audytora).** d108648 -> d108687: kasy miast
   25.57 -> 6.61 mln (-19 mln), kiesy wsi 1.45 -> 8.54 mln, skarbce 58.84 -> 67.93 mln. Zloto swiata stale,
   wiec to przelew, nie wyciek. Jesli to rozruch po wczytaniu - powtarza sie przy kazdym wczytaniu.

### Propozycje (od najwazniejszej)

1. **Pomiar zrodla rozjazdu Plan - gra** dla miesa i zboza: jedna linia logu Plan.Produce -> x zima ->
   x czynnik rak -> ScaleRates/beta -> ksiega. Potem poprawic przyczyne (np. zima i czynnik rak w Produce).
   Dlaczego: to jeden korzen spraw 1, 2, 3 i nadmiaru zboza. Ryzyko: male (pomiar). Wielkosc: S, potem M.
   Plan B: klucz "Ks Plan Realized Market" (wspolczynnik z ksiegi, wygladzenie 28 dob, krok 5%, pasmo 0.7-1.3).
   Ryzyko B: sprzezenie Plan-gra. Wielkosc: L.
2. **Wyjecie z koszyka normalizacji towarow z narzutem na maksimum** (zboze) - lokalne, czyste, zdejmuje
   mieso z podlogi w Essos. Ryzyko: zmienia poziom cen cienia - pilnowac zboza (nie ponizej 0.98) i liczby
   towarow przy granicach. Wielkosc: S-M. Dopiero po propozycji 1.
3. **Wyjasnic przelew startowy** (d108648-108687): ktora sciezka oprozniła kasy miast. Wielkosc: S (log).
4. **Gersuma**: podstawa = min(srednia kasy okna, kasa dzis) - rata - rachunki - prog, zamiast najnizszej kasy
   (bezpieczniejsze niz sama kasa dzis). Ryzyko: zabranie zlota, ktore zaraz zejdzie na raty. Wielkosc: S.
5. **Pomiar skokow Planu**: top 5 towarow wedlug odchylenia i ich przyciecie w kazdym punkcie kontrolnym.
   Wielkosc: S.
6. Hamulca swin (479-e) nie ruszac, dopoki nie ma propozycji 1. Klucz "Ks Switch Cap Follows Plan" tylko,
   jesli po tescie swinie dalej stoja.

### Decyzje dla Jeffa (ekonomia)
- Kolejnosc: najpierw pomiar rozjazdu Plan - gra (1) i przelewu startowego (3), potem poprawki?
- Kasy miast: nie zejda ponizej ok. 13 mln przy obecnym progu (1.5 x zapas kupcow + 28 dob firm).
  Nizej = decyzja o OverReserve / KsFarmCatchUpKeepDays, nie blad.
- Skarbce koron: zostawiamy (same sie nasycaja) - wrocic, jesli w dluzszym tescie przyrost znow rosnie.

---

## Podsumowanie

- Walka: sensowna. Jedyna wyrazna niespojnosc - kusza ROT (`crossbow_a` 120 lb) slabsza od luku 100 lb.
- Kuznia: sensowna poza tarczami - tarcze kosztuja w surowcu 3-14x swoja wartosc (do naprawy).
- Ekonomia: zamknieta i stabilna; glowny problem to Plan KS, ktory nie widzi realnej dostawy miesa i zboza.
