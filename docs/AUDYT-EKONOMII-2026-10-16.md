# Audyt ekonomii i surowcow - raport dla Jeffa (15.10.2026)

Zakres: Twoje pytanie z 15.10 ("czy produkcja jest odpowiednia, czy ilosc warsztatow jest odpowiednia, czy czegos nie
jest za duzo a czegos za malo, drewno 1 kg czy 100 kg, czy ceny pasuja do masy, czy ekonomia zzera kase lordow i krolow,
czy da sie zrobic, zeby bylo na co wydawac, zeby system byl spojny i mniej sztuczny").

Na czym to stoi:
- Twoja gra: log `Armoury-2026-10-15_10-56-30.log`, doby 108903-108945 = doby 67-109 kampanii, lato, WGRANIE 61
  (juz z kiesa2, obieg2, korona2).
- Test 4-letni (wersja 54, BEZ paczek z 15.10): `D:/Backup-Bannerlord/logs/2026-10-15-6lat/`, doby 1-1458, zima ROT od doby 456.
- Testy 2-letnie z 15.10 rano oraz kod (`zb/int/Armoury/src`), dekompilacje gry, BK i ROT, items-dump.csv.
- 6 niezaleznych przegladow: produkcja i zuzycie, warsztaty, jednostki i masa, ceny, obieg pieniadza, sztuczne mechanizmy.
  Ich tabele i skrypty leza w podkatalogach obok tego pliku. Najwazniejsze liczby sprawdzilem sam jeszcze raz (rozdz. 5).

Pewnosc: **K** = sprawdzone w kodzie, **L** = zmierzone w logu albo CSV, **P** = przypuszczenie albo szacunek.
"d" i "zl" to ta sama moneta (denar). "Na dobe" = na jeden dzien gry (rok = 364 dni, CLAUDE.md 7).

---

## 0. Najkrocej

1. **Zloto sie nie gubi.** W Twojej grze suma swiata 221,85 -> 221,68 mln w 42 doby (-628 zl w ostatniej dobie). W tescie
   4-letnim 219,37 -> 217,78 mln (-0,7% w 4 lata). "Zzeranie kasy" to PRZESUWANIE zlota, nie znikanie [L].
2. **Pieniedzy jest wiecej niz rzeczy do kupienia.** Tylko w 15 kategoriach jedzenia i surowcow mieszczanie maja
   budzet 136 275 d na dobe, a polki pozwalaja wydac 30 823 d. 105 452 d na dobe nie ma na co wydac [L, sprawdzone].
   Wsie chca kupic za 67 291 d na dobe, kupuja za 2 605 d (3,9%), 523 z 571 wsi nie znajduje towaru [L].
3. **Dokad wiec plynie zloto.** W tescie bez paczek z 15.10 lud miast i wsie urosly z 8,2 do 88,8 mln. Rody spadly
   z 75,1 do 55,3 mln (-26%), korony z 57,9 do 14,9 mln [L]. W Twojej grze straznik obiegu (podatek od oszczednosci,
   dzis 5,03% nadwyzki na dobe) zawraca to zloto do panow i koron. Ale nowa skrzynia rosnie u zolnierzy: sakiewki ludzi
   9,66 -> 15,55 mln (+140 tys. na dobe), bo nie maja na polkach lepszego sprzetu do kupienia [L].
4. **Produkcja w liczbach prawie zawsze rowna sie zuzyciu**, ale to nie znaczy, ze jest dobrze. Zuzycie jest
   ograniczone tym, co lezy na polce. O stanie towaru mowia: dni zapasu, puste miasta i cena.
5. **Za malo:** sol (cena x8), jablka, marchew, pomarancze, miod (0 sztuk w 97 miastach, x10), jaja (x10), futra (x5),
   papirus, kamien i ruda zlota (x3-6). Garum, atrament, barwniki i kamienie szlachetne nie maja w swiecie zadnego
   producenta. Ciasno jest tez ze skorami, welna, miesem, piwem, serem i maslem (zapas topnieje) [L].
6. **Za duzo:** ryby (98 tys., zapas rosnie), drewno w zimie (zapas 41,6 -> 96,3 tys. w 4. roku testu), bron i zbroje
   (2 580 sztuk robionych na dobe, 135 idzie na zlom, w swiecie 1,10 mln sztuk), konie (hodowla scieta do x0,25-0,5) [L].
7. **Warsztatow nie trzeba wiecej.** Trzeba je inaczej rozlozyc i dowiezc do nich surowiec. Kazde z 97 miast ma
   3 warsztaty notabli i 1 ukryty, niezaleznie od ludnosci (Oldtown 3,08 mln ludzi ma tyle samo co Driftwood Hall
   49,9 tys.). Codziennie stoi 123 z 291 warsztatow (42%), a rzemioslo miast ma zajete tylko 24% rak [K, L].
8. **Drewno: zostaw 1 sztuke = ladunek 100 kg.** Kuznia od WGRANIA 61 liczy drewno w kg i to dziala (12 kuc zuzylo
   14,8 kg). Przy 1 kg cena sztuki musialaby byc 1 d zamiast 0,035 d, czyli 28,6 x historii. Prawdziwy blad jednostki
   zostal na weglu: 0,5 kg za 1 d daje z ladunku drewna za 4 d 40 wegli za 40 d [K, L].
9. **Ceny broni i zbroi trzymaja historie** (material 15-27% ceny, reszta praca). Do poprawy: kusze (tansze od lukow),
   helmy (t4 tanszy od t3), luki t5-t6 (za drogie), wegiel i sztabki (zaokraglenie do pensa) [K].
10. **Co jest sztuczne.** Nasze latki na gre (zold, regulatory kas, BK) sa konieczne i zostaja. Sztuczne jest ok. 15
    "pomp", ktore przesuwaja zloto z urzedu: razem ok. 1,8 mln zl na dobe, czyli 2,3 x caly zold. Leczyc to nalezy
    TOWAREM (zeby bylo co kupic) i prawdziwymi wydatkami panow, a nie coraz wiekszym podatkiem.
11. **Plan:** najpierw 13 malych zmian (S), bez psucia zapisu Twojej kampanii; potem 14 wiekszych (M/L), z ktorych
    najwazniejsze to owoce, miod i jaja z wsi, len, welna i skory z wsi do miast, typ warsztatu wedlug ceny i zysku,
    budowy i dwor pana jako prawdziwe zakupy. 6 spraw czeka na Twoja decyzje (rozdz. 3.3). Gdy grasz, nic nie wgrywam.

---

## 1. Odpowiedzi wprost na Twoje pytania

### 1.1 Czy produkcja jest odpowiednia?

Nie do konca. Liczby sie "zgadzaja", ale tylko dlatego, ze kupic mozna tylko to, co lezy na polce.

Przyklad z Twojej gry (srednia z 43 dob, sprawdzone skryptem `raport/avg.py`):

| Towar | Produkcja / zuzycie na dobe | Zapas swiata | Co naprawde widac |
|---|---|---|---|
| sol | 900 / 896 | 6 316 -> 5 687 (6 dni) | w 97 miastach razem 46 sztuk, cena x8 |
| skory surowe | 559 / 561 | 1 479 -> 1 635 (3 dni) | prawie calosc w miastach, brak w 21 miastach |
| len | 709 / 675 | 7 762 -> 9 450 | 82% lezy we wsiach i na wozach, 69 miast bez lnu |
| welna | 338 / 313 | 3 246 -> 4 207 | brak w 50 miastach, sufit regulatora |
| piwo | 755 / 773 | 3 694 -> 2 697 (3 dni) | zapas topnieje |
| ser | 457 / 533 | 14 952 -> 11 764 | zapas topnieje (-76 na dobe) |
| maslo | 332 / 361 | 11 080 -> 9 887 | zapas topnieje |
| jaja | 128 / 156 | 3 207 -> 2 090 | x10, pusto w 88 miastach |
| zboze | 9 593 / 9 402 | 161 200 -> 169 175 | dobrze (spichlerze na 461 dni w tescie) |
| ryby | 2 546 / 2 471 | 92 745 -> 97 723 | za duzo, 6,5 x wzorca |

Wniosek: zywnosc podstawowa (zboze, ryby) jest w porzadku albo w nadmiarze. Brakuje towarow "drugiego kregu":
owocow, miodu, jaj, futer, soli, nabialu, skor i welny. Jablka, marchew, pomarancze i miod w ogole nie powstaja we wsiach
(gra daje 0 dla towaru spoza listy typu wsi). Te, ktore sa, BK tworzy z niczego w swoich partiach i znikaja razem z nimi
(jablka: 265 z 283 na dobe przepada z rozbitymi partiami) [K, L].

### 1.2 Czy ilosc warsztatow jest odpowiednia?

Liczba jest zla w rozkladzie, a nie w sumie.

- Kazde miasto ma 4 warsztaty: 3 notabli + 1 ukryty rzemieslnikow (291 + 97 = 4 x 97; gra
  `DefaultWorkshopModel.cs:27`) [K, sprawdzone w logu: "warsztaty [artisans 97, bakery 73, ...]"].
- Oldtown (3,08 mln ludzi rynku) ma tyle warsztatow co Driftwood Hall (49,9 tys.). 20 najwiekszych rynkow ma 48% ludzi
  i 21% warsztatow [K].
- W Twojej grze w dobie 109 pracowalo 168 z 291 warsztatow notabli. Wszystkie razem zarobily +9 044 d na dobe, czyli
  31 d na warsztat [L, sprawdzone]. Martwe przez cala gre: kopalnie w miastach (11, 0 cykli), tkalnie lnu (16, 0 cykli),
  garbarnie (9, 0 cykli), garncarnie (8, pracuje 1).
- Piekarni jest 73 (25% wszystkich). Typ warsztatu zmienia sie tylko po bankructwie (stojacy warsztat bankrutuje po ok.
  roku). Gra wybiera nowy typ wedlug produkcji okolicznych wsi, nie wedlug tego, czego miastu brakuje i co sie oplaca.
  BK przy powtorzonym typie wstawia w 30% kopalnie, ktora w miescie nie pracuje [K].
- Rak nie brakuje: rzemioslo miast ma zajete 24% rak (3 978 z 16 738 roboczodni) [L, sprawdzone].

Odpowiedz: nie dokladac warsztatow ani rak. Zrobic trzy rzeczy: (1) linie towarowe warsztatu pracuja wedlug ludzi rynku
(duze miasta wiecej, male mniej, suma swiata bez zmian), (2) typ warsztatu zmienia sie wedlug ceny i zysku, (3) len,
welna i skory ze wsi trafiaja do miast, gdzie stoja tkacze i garbarze.

### 1.3 Czego jest za duzo, a czego za malo?

**Za malo** (Twoja gra, doba 109, plus test 4-letni):

| Towar | Indeks ceny (mediana miast) | Puste miasta z 97 | Przyczyna |
|---|---|---|---|
| jablka, marchew, pomarancze | 9,8-10 | 97 | zadna wies ich nie robi; BK z niczego w partiach [K] |
| miod | 10,0 | 97 | jak wyzej |
| jaja | 10,0 | 88 | tylko z kur w partiach BK (5% na kure dziennie) [K] |
| sol | 8,05 | prawie wszystkie (46 sztuk razem) | wsie wykupuja kazda sztuke, 12 wsi solnych za malo [L] |
| futra | 4,95 | 73 | 39 sztuk na dobe na caly swiat, regulator na suficie |
| papirus, wapien, marmur, ruda zlota | 3,4-5,5 | 82-92 | malo wsi, kamien zjadaja budowy |
| garum, atrament, barwniki, kamienie szlachetne | 5,9-7,2 | 97 przez 4 lata | brak jakiegokolwiek producenta |
| chleb | 4,07 | 25 | piekarnie robia tyle samo w malym i duzym miescie |
| mieso | 1,9 latem, 3,6-4,0 zima | 32 bez wiersza | zywiec (owce, swinie) na suficie regulatora x1,30 |
| welna, len, skory | 1,8 / 1,35 / 1,85 | 50 / 69 / 21 | welna i skory: za malo; len: lezy we wsiach (rozdz. 4) |
| wino, oliwa | - | 76 / 79 bez wiersza | 80-85% zapasu w jukach karawan |

**Za duzo:**

| Towar | Liczby | Co z tym |
|---|---|---|
| ryby | 97 723 sztuk i +118 na dobe; w 4. roku testu 28% z nieznanego zrodla | mniej wsi rybackich, sonda autora |
| drewno w zimie | test, 4. rok: zapas 41 555 -> 96 326, indeks 0,46-0,56 | opal dla ludu w zimie, budowy bez desek |
| bron i zbroje | 2 580 sztuk na dobe, zlom 135, stan 1 103 626 (zbrojownie DTE 1,03 mln) | hamulec wedlug zapasu w zasiegu |
| konie | hodowla x0,25-0,50, na polkach 14 348 | regulator juz tnie - nic nie robic |
| bawelna, srebro, zelazo w zimie | regulator x0,51-0,78 | regulator juz tnie |

### 1.4 Czy liczby produkcji sa dobre?

Czesciowo. Trzy rodzaje bledow:

1. **Receptury warsztatow robia mase z niczego.** Gra: 1 lupa (15 kg) -> 4 narzedzia (40 kg); 1 srebro (10 kg, 20 d)
   -> 2 klejnoty (20 kg, 4 800 d, czyli x240 wartosci); ladunek drewna 100 kg -> 2 deski 20 kg (80 kg ginie)
   [K, `spworkshops.xml`, items-dump]. Nasza regula 216 (wsad wedlug wartosci i masy) obejmuje tylko welne, len i skory.
2. **Ilosci towarow koncowych sa smiesznie male wobec 52 mln ludzi.** Na dobe na caly swiat: narzedzia 100, garnki 28,
   klejnoty 14, aksamit 5, wino 47, oliwa 32, deski 10 (srednie z Twojej sesji) [L].
3. **Zrodla "z niczego" i ujscia "w nicosc" w towarach.** Partie BK tworza ok. 1 290 sztuk jedzenia na dobe z niczego
   (marchew 186, jablka 178, winogrona 148, piwo 144, ...), a ok. 2 090 sztuk znika z rozbitymi albo usunietymi partiami
   [L]. Spichlerze kasuja plon ponad sufit: 8,6 mln racji na dobe (17% plonu) [L].

### 1.5 Drewno: 1 sztuka = 1 kg czy 100 kg?

**Rekomendacja: zostaw 100 kg w handlu i na mapie. W kg liczy tylko kuznia (tak jest od WGRANIA 61).**

Dlaczego:
- Gra zna tylko cale pensy: cena sztuki na targu wynosi najmniej 1 d (`DefaultTradeItemPriceFactorModel.cs:188-193`) [K].
  Kilogram drewna historycznie kosztowal 0,035 d. Przy 1 kg cena wyszlaby 1 d, czyli 28,6 x historii. Ladunek 100 kg
  kosztuje 4 d (historia 3,5 d) [K].
- Wszystko, co wozi i liczy mase (udzwig partii, wozy wsi, karawany, warsztaty, dymarki), liczy juz w kg z wagi przedmiotu [K].
- Kuznia byla jedynym prawdziwym bledem: trzonek mlota bral 3 "drewna" = 300 kg. Naprawione (SmeltNoWood WGRANIE 58,
  SmithWood WGRANIE 61). W Twojej sesji 12 kuc zuzylo 14,8 kg, zapas otwartego ladunku spadl z 99 do 84,25 kg.
  Po staremu poszloby ok. 16 ladunkow, czyli 1,6 t [L].
- Koszt zmiany na 1 kg: budowy kupilyby ok. 25 x mniej drewna (placa pieniedzmi), magazyny i wozy wsi licza sztuki
  (raz juz 10 x sztuk zatkalo wsie), 44 pliki z jednostka "ladunek", a w zapisie Twojej kampanii 17 884 ladunkow trzeba by
  pomnozyc x100 w kazdym rosterze. Wysilek L, ryzyko wysokie, zysk tylko w napisie [K, L].
- Ten sam blad jednostki widac juz dzis na weglu: 0,5 kg za 1 d to 28,6 x historii; z ladunku drewna za 2-4 d kuznia
  robi 40 wegli wartych 40 d. To naprawiamy (N2), a nie drewno.

### 1.6 Czy ceny pasuja do ilosci i masy?

- **Bron i zbroje: tak.** Ceny wynikaja z kg i dni pracy. Miecz t1 13 d/kg, t6 130 d/kg; zbroja t6 83 d/kg (33 kg za
  2 798 d). Historycznie: sprzet zolnierski 10-40 d/kg, panski 50-200 d/kg [K]. Rynek broni wraca do ceny bazowej:
  lordowie placili 1,43 x wartosci w 1. polroczu testu, a od 2. roku 1,0-1,05 x [L].
- **Dziury w cenniku broni** [K]: kusze 9-27 d sa tansze od lukow t4-t6 (25-48 d), a historycznie kusza to 3-10 lukow
  (10 z 12 kusz ma w grze tier "ponizej pierwszego"). Helm t3 58 d drozszy od t4 40 d. Luk t6 45-48 d, a historyczny
  luk wojenny 12-18 d (liczony dniowka platnerza).
- **Towary pojedynczo maja dobra cene za kg** (zboze 0,31, wino 1,4, welna 8,3, aksamit 400 d/kg) [K].
- **Receptury warsztatow juz nie.** Wartosc dodana na jeden dzien pracy warsztatu (placa 3 d): deski 0 d, piwo 1,8 d,
  garnki 5 d, chleb 18 d, narzedzia 31 d, filc 50 d, klejnoty 598 d, aksamit 875 d [K]. Cena sprawiedliwa (paczka 123)
  placi warsztatowi najwyzej koszt x 1,25. Reszte trzymaja kasy miast: 81 tys. zl na dobe w Twojej grze, z czego same
  srebrniki 36 tys. [L].
- **Ceny rynkowe trwale odjezdzaja tam, gdzie brakuje towaru, i przez 4 lata same sie nie prostuja**: jaja 6-7 x
  wartosci, maslo 4 x, papirus 3-4 x, futra 2-3 x [L]. Wysoka cena nie sciaga produkcji, bo typ warsztatu nie patrzy na
  cene (1.2). To jest wazna czesc odpowiedzi "na co wydawac".
- **Zaokraglenie do pensa** psuje najtansze surowce: wegiel 0,5 kg = 1 d, surowka i zelazo kute po 1 d, wapien 10 kg =
  1 d (x5 historii) [K].

Rada: ogolnego poziomu cen nie ruszac (zold, lud i sprzet trzymaja sie historii). Poprawic kusze, helmy, luki i wegiel.
Polaczyc cene z wyborem typu warsztatu.

### 1.7 Czy ekonomia zzera kase lordow i krolow? Kto gromadzi?

Zloto nie znika, tylko plynie w jedna strone: od kas miast, koron i lordow do ludu i wsi. Droga wyglada tak:
lord placi zold -> zolnierze wydaja w miastach -> kasa miasta placi ludowi robocizne i doplate do 35% wartosci wyrobow ->
lud nie ma czego kupic, wiec zloto zostaje w kiesie.

**Test 4-letni (wersja 54, BEZ straznika obiegu)** - linie "Pieniadz swiata", doba 1 i 1458 [L, sprawdzone]:

| Skrzynia | Doba 1 | Doba 1458 | Zmiana |
|---|---|---|---|
| kiesy ludu miast | 6,37 mln | 46,17 mln | x7,2 |
| kiesy podzamczy | 0,67 mln | 6,94 mln | x10 |
| kiesy wsi | 1,11 mln | 35,57 mln | x32 |
| glowy rodow + pozostali lordowie | 63,12 + 11,96 = 75,08 mln | 45,92 + 9,38 = 55,30 mln | -26% |
| skarbce krolestw | 57,90 mln | 14,91 mln | -74% |
| kasy miast | 26,14 mln | 7,77 mln | -70% |
| notable | 31,29 mln | 18,59 mln | -41% |
| sakiewki ludzi (zolnierzy) | 0,41 mln | 11,05 mln | x27 |
| razem swiat | 219,37 mln | 217,78 mln | -0,7% |

**Twoja gra (doby 67-109, Z kiesa2, obieg2, korona2)** [L, sprawdzone]:

| Skrzynia | Doba 67 | Doba 109 | Na dobe |
|---|---|---|---|
| kiesy ludu miast | 17,08 mln | 13,05 mln | -96 tys. |
| kiesy wsi | 6,57 mln | 4,63 mln | -46 tys. |
| glowy rodow | 67,38 mln | 68,07 mln | +16 tys. |
| skarbce krolestw | 52,61 mln (23,7%) | 52,44 mln (23,6%) | -4 tys. |
| kasy miast | 9,17 mln | 9,74 mln | +14 tys. |
| notable | 28,27 mln | 26,79 mln | -35 tys. |
| **sakiewki ludzi** | **9,66 mln** | **15,55 mln** | **+140 tys.** |
| razem swiat | 221,85 mln | 221,68 mln | -4 tys. |

Wniosek:
- W Twojej grze lud juz nie gromadzi. Straznik obiegu zabiera mu 5,03% nadwyzki na dobe (mnoznik 5,028 przy kiesach
  1,577 x poziomu normalnego), a glowy rodow i korony stoja [L, sprawdzone].
- Ale to dziala podatkiem, a zloto ucieka do nowej skrzyni: sakiewek zolnierzy (7% zlota swiata). Zolnierze odkladaja
  na lepszy sprzet, ktorego nie ma na polkach: 27 112 koszykow na dobe "brak lepszej na polce" [L].
- Wsrod lordow zloto skupia sie u garstki: 10 najbogatszych rodow mialo 18,7% zlota rodow w dobie 721 i 36,0% w dobie
  1459 testu. Straz Nocna trzyma 3,54 mln przy 680 ludziach, bo nie placi zoldu i nie ma zadnego ujscia [L].
- Krolowie nie wydaja nic na siebie: korona oddaje tego samego dnia 99,98% wplywow (doba 109: do wydania 553 631 zl,
  zostalo 90 zl) [L, `CrownIncome.cs:15-22`].

### 1.8 Czy da sie zrobic, zeby bylo na co wydawac?

Tak, i to jest glowna droga zamiast podatkow. Dzis lordowie wydaja w 65-73% na zold i w 25-30% na dwor. Przy tym ponad
polowa dworu to przymusowy przelew niewydanego przydzialu na sprzet. Na budowy ida 1-2%. Tytuly BK, uczty, inwestycje
BetterEconomy: 0 zl przez 4 lata [L].

Na co zloto moze isc naturalnie (szczegoly w planie):
- **lud miast i wsi**: owoce, miod, jaja, futra, sol, chleb, opal w zimie (dzis 105 tys. d na dobe budzetu bez towaru,
  plus 64,7 tys. d na dobe koszyka wsi);
- **zolnierze**: lepszy sprzet na polkach i markietani, ktorzy biora wiecej od pelnej sakiewki;
- **lordowie**: dwor jako prawdziwe zakupy (stol, wino, sukno, zbytki), budowy, ktore nie stoja na deskach i narzedziach,
  utrzymanie zbudowanych murow i portow, uczty;
- **krolowie**: wlasny dwor w stolicy, roboty krolewskie (mury, porty), straz krola;
- **bogaci notable i patrycjat**: przebudowa warsztatow na brakujacy towar.

Tabela "105 452 d na dobe bez towaru - co to pokryje" (Twoja gra, doba 109; obejmuje 100% tej kwoty):

| Kategoria | Nie do wydania na dobe | Pozycja planu |
|---|---|---|
| jaja | 26 331 | P1 (jaja z wsi rolniczych) |
| futra | 20 020 | P8 (wiecej futer z wsi traperskich) |
| miod | 10 407 | P1 |
| jablka | 9 840 | P1 |
| chleb | 9 371 | P4 (piekarnie wedlug ludzi rynku) |
| papirus | 8 318 | P8 |
| garum | 5 029 | P8 (brak producenta - przeniesc budzet) |
| ruda zlota | 4 845 | P8 (przeniesc budzet na klejnoty) |
| barwniki | 4 106 | P8 |
| atrament | 3 422 | P8 |
| placek | 3 275 | P1 + P4 |
| reszta (miod pitny, wapien, marmur, kamienie) | 488 | P8 |
| **razem** | **105 452** | P1 46 578 (44%), P8 46 228 (44%), P4 12 646 (12%) |

Do tego osobno: koszyk wsi (sol, narzedzia, plotno, garnki, piwo) 64,7 tys. d na dobe bez towaru -> N3, P2, P3, P4;
"kupione z oferty" 24,2 tys. d na dobe bez towaru (miod, bawelna, maslo, klejnoty, aksamit, srebro, wino) -> P1, P10, P11;
zbytki "bez kategorii" 31 tys. d na dobe -> P8.

### 1.9 Czy system jest spojny i mniej sztuczny?

Jeszcze nie.
- **Konieczne i zostaje** [K]: nasze latki na gre i BK, ktore zamknely ekonomie (zold w nicosc 768 tys. na dobe, regulator
  kas zamkow kasowalby 391 tys. na dobe, dosypka kas miast gry, "zakupy" mieszczan z niczego, 1% kasy BK, pasmo notabli).
- **Sztuczne** [L]: ok. 15 przelewow "z urzedu", razem ok. 1,8 mln zl na dobe:
  - kasa miasta -> lud: robocizna 205 tys., doplata do 35% 90 tys.;
  - lud -> trzy rodzaje podatkow od oszczednosci: 449 + 124 + 8 tys.;
  - zawor kas miast i zamkow, danina z kas i utarg wsi do pana i korony: 521 tys.;
  - korona -> rody tego samego dnia: zwrot zoldu 320 tys. i renty 223 tys.
- Dubluja sie: 7 podatkow siega do tej samej kiesy ludu, kazdy z inna regula; korona ma 4-5 pokretel na jeden skarbiec;
  "wyrownanie panow zamkow" to regulator, ktory godzi dwa inne regulatory.
- Miara sukcesu: gdy naturalne ujscia zadzialaja, mnoznik straznika sam spadnie z 5,03 do ok. 1, a "nie do wydania"
  z 105 tys. d na dobe w strone zera.

---

## 2. Tabela problemow

Waga: W = wysoka, S = srednia, N = niska. "Jeff" = Twoja gra doba 109; "test" = test 4-letni.

| # | Problem | Waga | Liczby | Dowod | Pewnosc |
|---|---|---|---|---|---|
| 1 | Lud i wsie maja budzet na towary, ktorych nie ma | W | Jeff: 136 275 d budzetu, do wydania 30 823, bez towaru 105 452 d/dobe; test: 116,6 -> 162,4 tys. d/dobe; wsie 2 605 z 67 291 (3,9%) | log Jeffa l.19922 (Ceny surowcow), "Kiesa ludu: dzien 108945"; test l.302131; `RawPrice.cs:340-344` | L (sprawdzone) |
| 2 | Zloto plynie w jedna strone: do ludu i wsi | W | test: lud+wsie 8,2 -> 88,8 mln, rody 75,1 -> 55,3 mln, korony 57,9 -> 14,9 mln | "Pieniadz swiata: dzien 108836" (15-55-37) i "110294" (20-17-37) | L (sprawdzone) |
| 3 | Nowa skrzynia: sakiewki zolnierzy | W | Jeff: 9,66 -> 15,55 mln w 42 doby (+140 tys./dobe, 7% swiata); 27 112 koszykow/dobe "brak lepszej na polce" | "Pieniadz swiata" d108903/108945; "Dozbrajanie", "Markietani (E5/A3)"; `Sutlers.cs`, `MenPurse.cs:87-88, 295` | L (sprawdzone), K |
| 4 | Owoce i miod: 0 sztuk w 97 miastach, BK robi je z niczego | W | jablka 283/dobe, 265 przepada z partiami; indeks 9,8-10 | log l.19856, 19860, 19909; BK `PopulationManager.cs:337-352`, `BannerKingsComponent.cs:57-70` | K, L |
| 5 | Sol: stale za malo | W | Jeff 900/896 na dobe, w miastach 46, indeks 8,05; test 4. rok 926/924, sufit regulatora x1,30 | log l.19896, l.19922; test l.302556; `GoodsRates.cs:47` (SaltRate 2) | K, L (sprawdzone) |
| 6 | Warsztaty nie rosna z ludnoscia (3+1 w kazdym miescie) | W | Oldtown 3,08 mln vs Driftwood Hall 49,9 tys.; 20 najwiekszych: 48% ludzi, 21% warsztatow | gra `DefaultWorkshopModel.cs:27`; `WorkshopLaw.cs:818-822` vs `WorkshopTrade.cs:223-231` | K |
| 7 | 42% warsztatow stoi; typ zmienia sie tylko po bankructwie i nie wedlug ceny | W | pracowalo 168 z 291; martwe kopalnie 11, tkalnie lnu 16, garbarnie 9; piekarni 73 | log l.19797 (Warsztaty towarowe); gra `WorkshopsCampaignBehavior.cs:794-805, 1278-1338`; BK `EconomyPatches.cs:748-779` | K, L (sprawdzone) |
| 8 | Len, welna, skory nie docieraja do tkaczy i garbarzy | W | len: 82% we wsiach i wozach, 69 miast bez lnu; welna brak w 50 miastach; rece rzemiosla zajete 24% | log l.19869, 19905, "Rzemioslo miasta (148): dzien 108945"; `MaterialOrders.cs:18-33` (zrodlo tylko miasto) | L (sprawdzone), K |
| 9 | Zywiec na suficie regulatora: mieso, skory, welna, ser, maslo | S | owce i swinie x1,30 przy 7-8 dniach zapasu; ser 14 952 -> 11 764, maslo 11 080 -> 9 887; mieso x4 w zimie | "Regulator wsi (221)" l.8218; avg.py | L |
| 10 | Bron: robi sie wielokrotnie wiecej niz sie zuzywa | S | 2 580/dobe, zlom 135, stan 1 103 626 (DTE 1 025 681); w tescie polki 68 -> 264 tys., potem warsztaty staja "brak zlota" (7 -> 1 323 cykli) | "Uzbrojenie wedlug zrodla (E6/0): dzien 108946"; test "Rynek broni" | L (sprawdzone) |
| 11 | Doplata do 35% wartosci wyrobu tez za towar, ktory lezy | S | 87-103 tys. zl/dobe z kas miast do ludu | `TownFolk.cs:243-251`, `TownFolk.Live.cs:828`; "Kiesa ludu" | K, L |
| 12 | Straznik obiegu: regulator z pamiecia i histereza | S | mnoznik 2,26 -> 5,03 i stoi, kiesy 2,14 -> 1,58 x poziomu | "Straznik obiegu (obieg2): dzien 108945"; `TownFolk.Guard.cs:25, 44, 60` | K, L (sprawdzone) |
| 13 | 7 podatkow na jedna kiese ludu, korona jako przepompownia | S | podatki od oszczednosci ok. 581 tys./dobe; korona: 553 631 do wydania, zostalo 90 zl | "Korona: wplywy dnia (165): dzien 108946"; `CrownIncome.cs:16-22`, `CrownRents.cs:13-15` | K, L (sprawdzone) |
| 14 | Lordowie nie moga wydac przydzialow (sprzet, budowy) | S | test: sprzet 17% wydane, budowy 22%; Jeff: budowy 16,5 z 77,1 tys./dobe | "Budzet rodow (166)", "Budowy oplacone"; `CourtFull.cs:17-24`, `ClanBudget.cs:26-48` | L, K |
| 15 | Budowy stoja na narzedziach i deskach, drewna pelno | S | narzedzia < 5 szt. w 95 z 97 miast; deski 10/dobe na swiat | "Roboty ludu (P2): dzien 108944"; `BuildFunding.cs:13-27` | L, K |
| 16 | Wegiel 0,5 kg = 1 d: x28,6 historii, zarobek z niczego w kuzni | S | ladunek drewna 4 d -> 40 wegli = 40 d | `Settings.cs:94` (CharcoalWeight 0,5), Armoury.json Jeffa l.213; `MaterialLaw.cs:108-118` | K (sprawdzone) |
| 17 | Kusze, helmy, luki zle wycenione | S | kusze 9-27 d < luki 25-48 d; helm t3 58 > t4 40; luk t6 45-48 d | `ArmsPricing.cs:84-87, 242, 275`; `HistoricalPrices.cs:286-289`; log l.349 | K |
| 18 | Receptury: wartosc dodana od 0 do 875 d na osobodzien; renta 81 tys./dobe w kasach miast | S | srebrnik x117,6 kosztu (36 tys./dobe) | log l.19797 (cena sprawiedliwa); `WorkshopRecipes.cs:32` | K, L |
| 19 | Karawany trzymaja wiecej skapych towarow niz targi | S | sol 1 172 / 46; narzedzia 941 / 76; wino 821 / 188; maslo 7 044 / 623; plotno 2 258 z 4 136 | linie "Towary: dzien 108945" (posiadacze) | L (sprawdzone dla soli i plotna) |
| 20 | Ryby: za duzo, czesc z nieznanego zrodla | S | 97 723, +118/dobe; test 4. rok 687/dobe (28%) z "innych tickow" | log l.19868, 19909 | L |
| 21 | Drewno w zimie bez ujscia | S | test 4. rok: zapas 41 555 -> 96 326, indeks 0,46-0,56; budowy 1 391 -> 780/dobe | avg.py na 20-17-37; "Drewno: dzien" | L (sprawdzone) |
| 22 | Suwak MCM "Hist Bulk Unit Factor" zmienia jednostke drewna i rudy w trwajacej kampanii | S | jedno przesuniecie = x2 albo /10 drewna i rudy swiata | `McmSettings.cs:2463-2465`, `HistoricalPrices.cs:468-481`, `StartStock.cs:26-36`; klucza nie ma w Armoury.json (sprawdzone) | K |
| 23 | Pozyczki od ludu prawie martwe | N | ok. 90% kandydatow "lud lenn bez nadwyzki"; dlug 62 -> 143 tys. | "Pozyczki od ludu (obieg2)"; `ArmouryBehavior.cs:1772` przed `:1867` | K, L |
| 24 | Kopalnie BK w miastach: 0 cykli przez 4 lata | N | 18 -> 2 warsztatow, 0 cykli od doby 180 | test "Warsztaty towarowe"; BK `EconomyPatches.cs:748` | L, K |
| 25 | Zima ROT trwa latami (glodu nie ma) | N | zima 1 678 dni, plon -40,8%, spichlerze na 461 dni | test "Zima (E4/0)", "Spichlerze" l.303329 | L |
| 26 | RAPORT-4-LATA zawyza zloto rodow ok. 2 x | N | raport 101,7 mln; linie logu 45,92 + 9,38 = 55,30 mln | RAPORT-4-LATA l.30, l.42 vs "Pieniadz swiata: dzien 110294" | L (sprawdzone) |

---

## 3. Plan zmian

Zasady wdrazania (Twoje decyzje i CLAUDE.md):
- Gdy grasz, nic nie wgrywam i nic nie testuje. Wszystko czeka, az zamkniesz gre.
- Jedna zmiana naraz, kazda z autotestem, kopia zatwierdzonej DLL i wpisem w CHANGELOG ("Ryzyko / co sprawdzic").
- Kazda zmiana ma wylacznik w MCM (domyslnie wlaczony) i linie w logu, ktora pokazuje jej dzialanie.
- Zmiana domyslnej w `Settings.cs` nie dziala, gdy klucz jest w Armoury.json - sprawdzic plik (CLAUDE.md 7).

### 3.1 Najpierw: male zmiany, male ryzyko, bez psucia zapisu (S)

**N1. Pomiary przed zmianami (tylko log).**
- Co: (a) jedna linia "Pompy - sztuczne przelewy" (suma przymusowych przelewow na dobe i % zoldu; uspione mechanizmy
  oznaczone); (b) "Karawany - zapas wedlug towaru" (dni w jukach dla soli, narzedzi, wina, masla, srebra); (c) sonda autora
  ryb z "innych tickow"; (d) okno uczt BK (ile, koszt, towar).
- Dlaczego: bez jednej liczby nie widac, czy kolejne zmiany zmniejszaja przymus. Karawany i ryby maja nieznana przyczyne.
- Jak: `MoneyLedger.Obieg.cs` (liczniki juz istnieja), `GoodsLedger` (K4), latka na BK `Feast` (start, Finish);
  prog w `tools/sprawdz_logi.py`.
- Wysilek S. Ryzyko: brak (koszt doby < 1 ms). Zapis: brak.

**N2. Wegiel: waga z gry 5 kg zamiast 0,5 kg.**
- Dlaczego: dzis 0,5 kg kosztuje 1 d (x28,6 historii), a z ladunku drewna za 4 d kuznia gracza robi 40 wegli za 40 d.
  Receptury przetopu juz licza wegiel z jego wagi (`MaterialLaw.cs:118`: wegli z drewna = round(100 x 0,2 / waga)), wiec
  przy 5 kg same dadza 4 wegle z ladunku (4 d -> 4 d) i 0,2 d/kg.
- Jak: `Settings.cs:94` CharcoalWeight 0,5 -> 0 (zostawia wage gry); klucz JEST w Armoury.json (l.213: 0.5), wiec poprawic
  go w pliku przy zamknietej grze, z kopia .bak. Poprawic komentarze `MaterialLaw.cs:108`, `HistoricalPrices.cs:21`.
- Wysilek S. Ryzyko: Twoje 58 wegli bedzie wazyc 290 kg zamiast 29 kg. Przetop lupy zostaje bez zmian (1 lupa + 1 wegiel
  -> 30 sztab, bo liczy sie waga sztaby, nie wegla: `MaterialLaw.cs:120-124`); kazdy dalszy stopien bierze 1 wegiel = 5 kg
  na 2,5 kg metalu (blizej historii). Autotest formul przetopu. Zapis: waga to definicja przedmiotu, liczba sztuk zostaje.

**N3. Wiecej soli: SaltRate 2 -> 3,5.**
- Dlaczego: sol x8 przez 4 lata, w 97 miastach razem 46 sztuk. Wsie wydaja na koszyk 3,9% tego, co chca, wiec wykupia
  kazda dodatkowa sztuke.
- Jak: `GoodsRates.cs:47` (SaltRate), opis w Settings (GoodsRatesByPattern: "twice" -> nowa liczba), `tools/gen_mcm.py`.
  Regulator 221 sam przytnie produkcje, gdy zapas przekroczy 60 dni.
- Wysilek S. Ryzyko: kasy miast zarobia mniej na soli, 12 wsi solnych wiecej (obieg zamkniety). Ile spadnie cena - P
  (szacunek przegladu: do 2-3; moj: do 4-5, bo czesc soli i tak lezy w wozach i karawanach). Zapis: brak nowych pol.

**N4. Doplata do 35% tylko od towaru, ktory ktos kupil.**
- Dlaczego: kasa miasta placi ludowi 35% wartosci kazdego wyrobu, takze broni, ktora lezy na zapchanej polce
  (87-103 tys. zl na dobe). To jedna z glownych drog zlota z kas miast do kiesy ludu, ktora nie ma na co go wydac.
- Jak: `TownFolk.cs:243-251` (OnItemProduced) - ProdV liczyc przy sprzedazy z polki (`TownFolk.Sale`, f.Sale juz liczy
  sprzedaz), `TownFolk.Live.cs:828`. Klucz FolkCraftWageOnSale.
- Wysilek S. Ryzyko: dochod ludu miast spada; sprawdzic w "Kiesa ludu", czy biedne miasta dalej stac na jedzenie.
  Straznik sam obnizy stawke. Zapis: brak.

**N5. Hamulec warsztatow broni wedlug zapasu w zasiegu.**
- Dlaczego: 2 580 sztuk na dobe, zlom 135; kasy miast placa za bron, ktora lezy, a potem warsztaty staja z braku zlota.
- Jak: `WorkshopLaw.cs` (ranking dnia): koszyk, ktorego zapas na polkach w zasiegu wywozu PodazPopyt (250) przekracza
  90 dob zakupow AI i zalog, dostaje kare albo jest pomijany (licznik "pelne polki"). Klucz WorkshopWorldStockDays.
- Wysilek S. Ryzyko: nie hamowac ponizej zuzycia; wojna musi dalej nakrecac produkcje. Zapis: brak (pamiec doby).

**N6. Markietani wedlug zasobnosci sakiewki.**
- Dlaczego: sakiewki zolnierzy rosna +140 tys. na dobe w Twojej grze - to dzis glowna nowa skrzynia. Zolnierz z pelna
  sakiewka wydaje wiecej na zycie.
- Jak: `Sutlers.cs`: udzial dzienny x min(3, dni wolnej sakiewki / 10); `MenPurse.cs:87-88, 295`: limit oszczednosci K1
  nizszy, gdy partia od 14 dob nie kupila sprzetu - nadwyzka do markietanow. Odbiorcy jak dzis (kasa miasta 85% / wies).
- Wysilek S. Ryzyko: kasy miast +100-150 tys. na dobe, wiecej zaworu do panow; rozruch 28 dob jak flaga arm_sutlers, bez
  skoku pierwszego dnia (15,5 mln w sakiewkach). Zapis: bez nowego klucza.

**N7. Straznik bez pamieci.**
- Dlaczego: mnoznik 5,03 stoi, choc kiesy ludu spadly z 2,14 do 1,58 x poziomu; spada dopiero ponizej 1,25 x i 3% na
  dobe, wiec zejscie do 1 trwaloby ok. 54 doby.
- Jak: `TownFolk.Guard.cs` (GuardBegin, _gdM, :25, :60): stawka = baza x clamp(1 + 8 x (P/N - 1,5), 0,25, GdMax),
  liczona co dobe z odchylenia. Grupa progow obieg2 (OB2-3) na nowej regule.
- Wysilek S. Ryzyko: przy 1,6 x glowy rodow dostana ok. 50-100 tys. mniej na dobe - sprawdzic "Budzet rodow (166)".
  Zapis: mnoznik w arm_obieg2 zostaje, ale nie jest czytany (zgodne w obie strony).

**N8. Pozyczka przed darem.**
- Dlaczego: chciales, zeby lordowie pozyczali od mieszczan. Dzis ok. 90% kandydatow odpada "bez nadwyzki", bo podatek
  straznika bierze nadwyzke wczesniej w tej samej dobie (`ArmouryBehavior.cs:1772` przed `:1867`).
- Jak: `TownFolk.Loans.cs:107` czyta migawke z GuardBegin; podatek straznika z lenna, ktore tej doby pozyczylo, pomniejszony
  o pozyczke (`TownFolk.Hoard.cs`). Kolejnosci doby nie zmieniac.
- Wysilek S. Ryzyko: wojsko w wojnie szybciej rosnie, splaty w pokoju obnizaja pulapy. Zapis: format arm_obieg2 bez zmian.

**N9. Cennik broni: kusze, helmy, luki (trzy osobne kroki, po kolei).**
- Kusze: tier do wyceny co najmniej 3 (`ArmsPricing.cs:84-87`, `HistoricalPrices.cs:286-289`); kusza ok. 40-60 d.
- Helmy: jedna regula dni pracy dla helmow plytowych wszystkich tierow (`ArmsPricing.cs:275`: t >= 4 -> t >= 1) i waga do
  kosztu z pancerza glowy (`ArmsPricing.CostWeight`, :140); t3 ok. 20-25 d, t4 ok. 50 d, t6 ok. 145 d.
- Luki: dniowka lukmistrza (`HistoricalPrices.cs:289`: dla Bow tier dniowki najwyzej 2); luk t6 ok. 25 d.
- Wysilek S kazdy. Ryzyko: kusznicy AI drozsi (36 kusz/dobe), luki tansze (437/dobe - ranking przesunie rece); sprawdzic
  "Cennik (E8/C)" i przelicznik popytu kategorii. Zapis: wartosci liczone przy starcie sesji, bez migracji.

**N10. Bez kopalni BK w miastach.**
- Dlaczego: 11-18 kopalni w miastach przez 4 lata dalo 0 cykli; BK wstawia je w 30% powtorzen typu.
- Jak: latka na BK `EconomyPatches.DecideBestWorkshopTypePostfix` (:748): wynik "mines" w miescie zamienic na typ
  o najlepszym wyniku z cen targu (ta sama funkcja co w P3). Klucz NoTownMines. Sprawdzic kolejnosc Harmony.
- Wysilek S. Ryzyko: male (tylko Town.IsTown). Zapis: dotyczy tylko przyszlych zmian typu.

**N11. Blokada jednostki ladunku na czas kampanii.**
- Dlaczego: suwak MCM "Hist Bulk Unit Factor" (0-40, dzis domyslne 10) przelicza wage i liczenie kazdego stosu drewna
  i rudy w biegnacej kampanii bez przeliczenia stosow.
- Jak: `HistoricalPrices.Apply` (:464-481) bierze czynnik z zapisu (nowy klucz arm_bulk_unit obok arm_startstock);
  gdy MCM sie rozni - log "MCM X ignorowane, kampania ma Y". Suwak do grupy cen historycznych z opisem "new campaigns only".
- Wysilek S. Ryzyko: niskie. Zapis: nowy klucz; stary zapis dostaje 10, czyli to, co juz ma.

**N12. Drobne w kuzni.**
- Kg otwartego ladunku drewna na ekranie kucia ("Open load of hardwood: 84 kg left") ze `SmithWood.OpenKg`.
- Naprawa wlasnymi rekami: drewno wedlug kg naprawy (`Recipes.cs:640-664`: `SmithWood.LoadsFor(r.WoodKg x share)`),
  zeby nie zadac calego ladunku, gdy otwarty pokrywa 0,5 kg.
- Wysilek S. Ryzyko: niskie (UI i jedna regula). Zapis: brak.

**N13. Decyzje i dokumenty bez kodu.**
- Drewno w handlu zostaje 100 kg - zapisac w STAN-PRAC i jako pulapke w CLAUDE.md 7 ("kazdy nowy uzytkownik drewna liczy
  kg z item.Weight i bierze cale ladunki z zapasem resztek; kg nigdy nie idzie wprost do ceny").
- BkWorkshopInputsFix zostaje wylaczony (`Settings.cs:847`), dopoki jablek nie bedzie na polkach (inaczej stanie ok.
  480 placka na dobe). Klucza nie ma w Armoury.json (sprawdzone), wiec dziala domyslne false.
- Nie dokladac rak (WorkshopHandsStep 1,3, TownCraftHandsPerArmsHand 2 zostaja).
- Poprawic RAPORT-4-LATA (zloto rodow 55,3 mln, nie 101,7; rozdz. 4 S5) i CENY-HISTORYCZNE (jedwab surowy, plotno 10 d/kg).
- Wysilek S. Ryzyko: brak.

### 3.2 Potem: wieksze zmiany (M/L), w tej kolejnosci

**P1. Owoce, miod i jaja z wsi; zamkniecie "jedzenia z niczego" BK.** (M)
- Dlaczego: 46,6 tys. d na dobe budzetu ludu na jaja, miod, jablka i placek bez towaru; ok. 1 290 sztuk jedzenia na dobe
  powstaje z niczego i ok. 2 090 znika. BK sam projektuje owoce jako plon wsi.
- Jak: (a) na starcie sesji dopisac do `VillageType.Productions` to, co BK deklaruje w `PopulationManager.cs:334-352`
  (wheat_farm: apple 2, carrot 2; olive_trees: orange 2; lumberjack i trapper: honey 0,5; wsie rolnicze: Egg z kur),
  wzorem `VillageClimate.Apply` / `GoodsRates.Apply` (`GoodsRates.cs:140`). (b) Flaga z `TravellerTrade36b.cs:180-196`
  (GiveFood pominiete) rozszerzona na karawany niewolnikow i zywnosci, Estate, Militia, FreeCompany; zywnosc drogi z polki
  osady pochodzenia, reszta po usunieciu partii wraca na polke. (c) Znalezc "17 sztuk kazdego towaru" w RemoveParty.
- Ryzyko: GL1 (VillageByPeople) kalibruje g(typ) z sumy typu - nowe pozycje nie moga zmienic g zboza; MineralOnce i
  GetProductions BK (podwojne dopisanie); wozy wioza wiecej rodzajow; HarvestValve.Eligible. Ceny owocow spadna z x9 do
  ok. x1-2 (P).
- Zapis: lista typu wsi nie idzie do zapisu (jak klimat T8) - dziala od wczytania w Twojej kampanii.

**P2. Len, welna i skory z wsi do tkaczy i garbarzy miast.** (M)
- Dlaczego: len lezy w 82% we wsiach i na wozach (7 753 z 9 450), a 69 miast nie ma lnu; kontrakty koncza sie "brak
  zrodla z nadwyzka w zasiegu" (len 83, skory 80, welna 68 miast); rzemioslo ma 76% wolnych rak.
- Jak: wariant prostszy, bez zapisu: woz wsi z zapasem lnu lub welny ponad N dni sprzedaje do miasta targowego takze
  ponizej ceny bazowej (VillageTakings). Wariant pelny: `MaterialOrders.cs` - zrodlem moze byc wies z zapasem, przewoz
  wozem kupcow (ta sama zaplata: kasa miasta-celu -> kiesa wsi).
- Ryzyko: podwojne liczenie ladunku wozu w ksiedze towarow; tlok na drogach. Odziez wojska i naprawy dostana wiecej.
- Zapis: wariant wozu - brak; wariant kontraktu - nowy rodzaj rekordu w arm_matorders (starsza DLL zwolni kontrakt).

**P3. Typ warsztatu wedlug ceny i zysku (jedna regula).** (M)
- Dlaczego: 42% warsztatow stoi, a drogi towar (jaja x7, maslo x4, zbytki x6) nie dostaje nowych warsztatow.
- Jak: jedna funkcja "oczekiwany wynik z cen targu" uzyta w dwoch miejscach: (a) przy bankructwie - latka juz jest
  (`WorkshopInputs.cs:131-139`, DecideBestWorkshopType): wynik typu x wskaznik ceny jego wyrobow w miescie (0,5-3);
  (b) nowy krok w `WorkshopTrade.cs`: warsztat notabla, ktory traci od 60 dob (Rec.Avg i Rec.Slow < 0), zmienia typ przez
  `ChangeProductionTypeOfWorkshopAction.Apply`, gdy cel daje > 0 i wiecej niz obecny + prog; najwyzej 3 zmiany na dobe na
  swiat; warsztatow gracza nie ruszac. Klucz WorkshopRetool.
- Ryzyko: wahadlo typow (stad 60 dob i prog); typy zbrojne zmieniaja rece broni (6 x m_t).
- Zapis: typy zmieniaja sie trwale akcja gry, stopniowo; wylacznik zatrzymuje dalsze zmiany.

**P4. Linie towarowe warsztatu wedlug ludzi rynku.** (M)
- Dlaczego: chleb, narzedzia, wino i garnki robia w kazdym miescie tyle samo; jedna zasada dla jednego warsztatu (rece broni
  juz licza m_t). Pokrywa 12,6 tys. d na dobe chleba i placka.
- Jak: postfiks na czynnym `WorkshopModel.GetEffectiveConversionSpeedOfProduction` (sprawdzic, czy czynny jest
  NavalDLCWorkshopModel), mnoznik clamp(PeopleMarket.WorkersScale, 0,3, 4) tylko dla linii towarowych, zalozony w kampanii
  (wzor `WorkshopTrade.ApplyInCampaign`); place cyklu w `WorkshopTrade.CycleLabour` x ten sam mnoznik. Suma swiata x1,01.
- Ryzyko: wiecej wsadu w duzych miastach (zboze 9 593 / 9 402 na dobe - sprawdzic Famine); Gate200 nie moze liczyc m_t
  podwojnie.
- Zapis: brak; dziala od wczytania.

**P5. Budowy, ktore nie stoja.** (M)
- Dlaczego: przydzial budow 46-88 tys. d na dobe, wydane 11-22%; budowy stoja na narzedziach i deskach, drewna 96 tys.
- Jak: `BuildFunding.cs:19-27`: (a) brak desek -> drewno za ta sama wartosc plus place tracza do ludu (K1 bez zmian);
  (b) narzedzia jako sprzet budowy w schowku osady, zuzycie 10% na 28 dob zamiast wsadu w kazda porcje; (c) w pokoju AI
  najpierw budowle dochodowe (`BuildFunding.Held.cs`).
- Ryzyko: wiecej plac -> wiecej zlota w kiesie W, dopoki lud nie ma co kupic (robic po P1). Zelazo: narzedzia konkuruja
  z bronia (lupa 245 / 231 na dobe).
- Zapis: (b) schowek to towar w zapisie gry - bezpieczne.

**P6. Dwor pana jako prawdziwe zakupy; niewydany sprzet zostaje w kiesie rodu.** (M, razem z P5)
- Dlaczego: dwor (182-314 tys. na dobe) to dzis przelew bez towaru do kasy siedziby; ponad polowa to przymus (111 i
  141,5 tys. na dobe niewydanego sprzetu).
- Jak: `ClanBudget.cs` krok 162m "Court" (ok. :974): koszyk z targu siedziby (jedzenie i napoje 50%, sukno 20%, zbytki 30%)
  wzorem BK `Feast.Automanage`; `CourtFull.cs` - przeterminowany przydzial zostaje u glowy. Plotno i skora z rezerwa
  wojska (FolkClothAboveReserve). Dopisek w linii "Obieg".
- Ryzyko: przed E5/A2 kiesy glow rosly o 585 tys. na dobe (`CourtFull.cs:18-19`) - dlatego razem z P5 i po P1; straznik sam
  obnizy mnoznik.
- Zapis: brak nowego klucza.

**P7. Opal dla mieszczan, wiecej w zimie.** (M)
- Dlaczego: w zimie drewno nie ma ujscia (budowy 1 391 -> 780 na dobe), zapas rosnie do 96 tys.; mieszczanie pala 55-100
  ladunkow na dobe wobec ok. 11 500 we wzorcu.
- Jak: kategoria hardwood w koszyku ludu miast (`TownFolk.Live.cs`, jak VillIds :42), zuzycie na 1000 mieszczan, w zimie
  x2; start 150-300 ladunkow na dobe; latem maly (drewno latem na suficie x1,30).
- Ryzyko: konkuruje z kuznia i budowami latem. Zapis: brak.

**P8. Futra, papirus i kategorie bez producenta.** (S-M)
- Dlaczego: 46,2 tys. d na dobe budzetu bez towaru (futra 20 tys., papirus 8,3 tys., garum, atrament, barwniki, kamienie,
  ruda zlota) i 31 tys. d na dobe zbytkow "bez kategorii".
- Jak: (a) futra: stawka wsi traperskich jak sol (`GoodsRates.cs`, FurRate ok. x3; dzis 39 sztuk na dobe przy popycie ok.
  150); (b) dla kategorii bez zadnego zrodla przeniesc budzet w `HistoricalPrices.BudgetPostfix` (wzor :1054, bron ma
  budzet 0) na istniejace kategorie zbytku (sukno, lepszy stol, dom); ruda zlota -> klejnoty.
- Ryzyko: przeniesiony budzet podnosi ceny sukna i stolu - najpierw sprawdzic, czy tam jest towar. Zapis: brak.

**P9. Jeden podatek od oszczednosci zamiast siedmiu.** (M; koszt lojalnosci - decyzja D5)
- Dlaczego: kiesa2, wsie, straznik, danina P1, M4 i 184 siegaja do tej samej kiesy, trzeba je od siebie odejmowac
  (21 klaso-osad na dobe); ten sam czyn raz kosztuje lojalnosc (P1: 86,5 pkt na dobe), raz nie (kiesa2).
- Jak: `TownFolk.Hoard.cs` (PayHoard) jako jedyny pobor; czesc korony w wojnie zastepuje P1, w pokoju M4; wylaczyc P1 i M4
  (`TownFolk` PayAid, `KingdomTreasury.PeaceTribute.cs`); 184 tylko od zarobku. Nowa grupa progow.
- Ryzyko: P1 to ok. 25% wlasnych wplywow korony - test 182 dob. Zapis: bez nowego klucza.

**P10. Karawany nie trzymaja w jukach wiekszosci gotowych towarow.** (M)
- Dlaczego: w jukach 79% narzedzi, 80% wina, 85% oliwy, 78% filcu, 76% srebra, 55% plotna. Lud i wsie ich nie kupia.
- Jak: najpierw pomiar N1(b). Potem `CaravanBulk.cs` i latka "gabki" BK (BKPartyBehavior.OnSettlementLeft): limit w jukach
  (ile sprzeda w 2-3 najblizszych miastach z brakiem), nadmiar sprzedany w miescie wjazdu.
- Ryzyko: dochod karawan i cel spadnie albo przesunie sie w czasie. Zapis: brak.

**P11. Receptury wedlug wartosci dla pozostalych linii towarowych.** (M, dopiero po P1-P4)
- Dlaczego: wartosc dodana od 0 do 875 d na osobodzien; renta 81-90 tys. zl na dobe w kasach miast bez niczyjej pracy.
- Jak: `WorkshopRecipes.cs:32` (Scope) rozszerzyc tylko na linie, ktorych wsadu jest pod dostatkiem (oliwki, drewno na
  deski: 5 desek z ladunku zamiast 2); dla srebra zmienic cene rudy (`HistoricalPrices.cs:54`), a nie ilosc wsadu.
- Ryzyko: wiecej wsadu na sztuke = mniej narzedzi i klejnotow, ktorych juz brakuje (rozdz. 4 S12). Zapis: receptury
  liczone przy starcie sesji.

**P12. Wyzszy sufit ceny zbytkow (FolkLadderCeilingMax 4 -> 8).** (S, ale po P1 i P8)
- Dlaczego: 946 par miasto-kategoria ponad sufitem przy indeksie 6; kupione 38% oferty. Bogaty lud placi drozej za rzadki
  towar, zloto idzie do kas miast i wytworcow handlem, nie podatkiem.
- Jak: `TownFolk.Luxury.cs:101-111` lub klucz w Settings (w Armoury.json go nie ma) + gen_mcm. Filc, plotno, skora 1,5.
- Ryzyko: zbytki dla Ciebie drozsze (do 6-8 x w 20 najbogatszych miastach); efekt ograniczony podaza. Zapis: brak.

**P13. Wapien i glina jako ladunek 100 kg.** (M, niska waga, nowa kampania)
- `HistoricalPrices.cs:469` i `StartStock.cs:40` (Ids); linie garncarzy w kg. Wapien x5 i glina x2 historii znikaja.
- Zapis: tylko nowa kampania albo jednorazowe /10 stosow z flaga.

**P14. Spichlerze: nadwyzka plonu do miasta zamiast w nicosc.** (L)
- Dziennie 8,6 mln racji (17% plonu) znika ponad sufitem spichlerzy. Sprzedawac taborem do miasta przez utarg wsi (112).
- Ryzyko: balans zboza calego swiata; potrzebny pelny lancuch L.

### 3.3 Do Twojej decyzji

**D1. Drewno 1 kg.** Rekomendacja: NIE (1.5). Zostaje ladunek 100 kg; kuznia w kg juz dziala. Potrzebne tylko Twoje
"zostaw" albo "zmien" (zmiana = L, ryzyko wysokie, Twoj zapis wymagalby przeliczenia 17 884 ladunkow x100).

**D2. Wiecej wsi hodowlanych zamiast czesci rybackich.** Owce i swinie stoja na suficie x1,30 od 1. do 4. roku,
ryb jest 6,5 x wzorca. Zmiana 8-10 wsi (Reach, Volantis, Qarth, KL) na sheep_farm / swine_farm / cattle_farm
(`GoodsRates.cs` od :56, wzor 16 wsi lnianych). Pytanie: w Twojej trwajacej kampanii (wymaga przeliczenia g(typ) przy
wczytaniu - recenzja GL-B pokazala przy zmianie typow ryby +55% i len -62%) czy dopiero w nowej? Wysilek M.

**D3. Korona wydaje na siebie.** Dzis oddaje 99,98% wplywow tego samego dnia. Propozycja: maly dwor krolewski w stolicy
(np. 5% wplywow, tylko z nadwyzki ponad zapas wojny), roboty krolewskie (mury stolicy, porty), a renty z uwolnienia zapasu
rozlozone na 90 dob zamiast tego samego dnia. Zwrot zoldu zostaje nietkniety (Twoja decyzja z 15.10). Skutek dla gry:
lordowie dostaja renty rowniej, ale w nowej wojnie mniej od razu. `CrownIncome.cs`, `CrownRents.cs`. Wysilek M.

**D4. Utrzymanie twierdz, murow, koszar i portow.** Staly koszt (1/50 kosztu poziomu co 28 dob, 75% place, 25% material).
Najwiecej placa najbogatsi (top 10 rodow ma 36% zlota rodow). Pytanie: czy po 182 dobach zaleglosci poziom ma spadac
(poza oblezeniem i swiezo zdobyta twierdza)? Wysilek M, nowy klucz zapisu arm_utrzymanie (stary zapis = zaleglosci 0).

**D5. Jeden podatek od oszczednosci (P9) i lojalnosc.** Czy podatek nadzwyczajny ma kosztowac lojalnosc (jak dzis P1),
czy nic (jak dzis kiesa2)? Propozycja: koszt tylko od czesci ponad stawke bazowa.

**D6. Drobne sprawy swiata.** (a) Czynsz gruntowy: 1/3 czynszu W i R do pana miasta (ok. 8-10 tys. d na dobe).
(b) Straz Nocna: dochod wydawany na zaopatrzenie i naprawy Muru zamiast gromadzenia (3,54 mln przy 680 ludziach).
(c) Uczty i wesela AI po zwyciestwie, slubie i narodzinach dziedzica (po pomiarze N1(d)). Wszystkie S.

### 3.4 Czego nie robic

- Nie podnosic dalej podatku od skrzyni ani gornej granicy straznika (FolkGuardMaxTimes 10) - leczyc towarem.
- Nie dokladac warsztatow ani rak.
- Nie zmieniac drewna na 1 kg ani 10 kg.
- Nie wlaczac BkWorkshopInputsFix przed P1.
- Nie ruszac zwrotu zoldu 165.
- Nie robic P11 (wiecej wsadu) przed P1-P4.
- Odrzucone warianty: wegiel jako worek 15 kg (zastapiony N2), doplata 35% z kapitalu warsztatu (robi nowa petle z pasmem
  notabli), inwestycje notabli w warsztaty jako osobny mechanizm L (zawiera sie w P3).

---

## 4. Sprzecznosci miedzy przegladami - rozstrzygniecia

**S1. Len, plotno i skora: "lnu za duzo, plotna i skory brakuje" (warsztaty) czy "len ciasno" (produkcja)?**
Przeglad warsztatow wzial jedna dobe (108945): len +348, plotno -80, skora wyprawiona -85 na dobe. Srednie z 43 dob
(sprawdzone, `raport/avg.py`): len 709 / 675 (zapas 7 762 -> 9 450, +40 na dobe), plotno 224 / 203 (3 562 -> 4 136),
skora wyprawiona 227 / 214 (1 409 -> 1 891), skory surowe 559 / 561 (1 479 -> 1 635, 3 dni).
Rozstrzygniecie: lnu w swiecie jest dosc (zapas rosnie, 82% lezy we wsiach i na wozach), brakuje go w MIASTACH - to
sprawa dowozu (P2), nie produkcji. Plotno i skora wyprawiona nie maleja, tylko leza w karawanach (55% i 47%). Skory
surowe sa naprawde ciasne (3 dni zapasu, zywiec na suficie) - tu racje ma przeglad produkcji. [L]

**S2. Drewno w Twojej grze: "-177 do -219 na dobe" (jednostki, warsztaty) czy "2 225 / 2 201" (produkcja)?**
Jedna doba kontra srednia. Srednia z 43 dob: 2 225 / 2 201, zapas 15 967 -> 17 884 (+45 na dobe) [L, sprawdzone].
Latem drewno jest w rownowadze na suficie regulatora x1,30 (ciasno), w zimie testu (4. rok) 1 994 / 1 843 i zapas
41 555 -> 96 326. Oba przeglady maja racje, kazdy o innej porze roku.

**S3. Sol: "zuzycie 24 na dobe" (obieg, test doba 110293) czy "896-924" (produkcja)?**
Wsie kupuja sol falami, gdy dojada wozy: dzienne zuzycie skacze od 5 do 6 502 w 4. roku testu i od 24 do 2 804 w Twojej
grze. Srednia 4. roku: 926 / 924; Twoja sesja: 900 / 896 [L, sprawdzone]. Rozstrzygniecie: sol jest zuzywana prawie
w calosci (97% wsie), jednodniowa liczba z przegladu obiegu jest mylaca. Wsie kupuja za 3,9% tego, co chca, wiec
dodatkowa sol tez wykupia (N3).

**S4. Wegiel: worek 15 kg z kg przy kowadle (jednostki, M) czy waga z gry 5 kg (ceny, S)?**
Kod przetopu czyta prawdziwa wage wegla (`MaterialLaw.cs:115-118`), a komentarz :108 mowi "PRAWDZIWA waga z gry (5 kg)".
Przy 5 kg z ladunku wychodza 4 wegle (4 d -> 4 d) bez zadnego nowego kodu. Worek 15 kg wymagalby liczenia kg przy kowadle,
bo kazdy krok przetopu bierze cale sztuki wegla. Rozstrzygniecie: 5 kg (N2). [K]

**S5. Zloto rodow: 101,7 mln (RAPORT-4-LATA) czy 55,3 mln (przeglad obiegu)?**
Linia "Pieniadz swiata: dzien 110294": glowy rodow 45 920 557 + pozostali lordowie 9 383 432 = 55,30 mln [L, sprawdzone].
2 x 45,9 + 9,4 = 101,2 mln - raport najpewniej liczyl zloto glowy rodu dwa razy (zloto rodu = zloto glowy); to samo pasuje
do King's Landing: 2 x 17,5 + ok. 1,1 = 36,1 mln [P dla przyczyny, L dla liczb]. Rozstrzygniecie: rody spadly o 26%
(75,08 -> 55,30 mln), nie o 7%. Raport do poprawy (N13).

**S6. Kiesa ludu: 37,4 -> 51,0 mln (raport), 46,2 mln, 53,1 mln czy 88,8 mln?**
Rozne zakresy, ta sama tendencja. Z linii "Pieniadz swiata" doba 1458: kiesy ludu miast 46,17, podzamcza 6,94, gminy
0,09, wsie 35,57 mln. "53" = miasta + podzamcza; "88,8" = wszystko z wsiami; "27,5 -> 42,7" (warsztaty) = tylko lokata
ponad zapas dni. Bez sprzecznosci; w raporcie zawsze podawac zakres.

**S7. Kasy miast: 11,2 -> 7,8 mln (raport) czy 26,1 -> 7,9 mln (obieg)?**
Raport liczy od doby 720, obieg od doby 1. Obie prawdziwe (doba 1: 26,14 mln; doba 1458: 7,77 mln) [L].

**S8. "Do notabli z niczego +119 tys. na dobe" (warsztaty, P) - czy to dziura?**
Nie. Linia "Pieniadz swiata (bilans - przyczyny): dzien 108945": "wyplata dochodu notablom 94 656 ... minus przelew
z kapitalu warsztatow i kas karawan notabli zgloszony jako zloto z niczego 94 656". To przelew z kapitalu warsztatow,
ksiega go odejmuje; suma swiata zmienila sie o -628 zl [L, sprawdzone].

**S9. Czy lud gromadzi? (produkcja, warsztaty, obieg: tak; sztuczne: w Twojej grze juz nie)**
Obie prawdy, rozne wersje. Test v54 (bez paczek 15.10): lud i wsie 8,2 -> 88,8 mln. Twoja gra (z kiesa2, obieg2,
korona2): kiesy ludu miast 17,08 -> 13,05 mln, wsie 6,57 -> 4,63 mln, ale sakiewki zolnierzy 9,66 -> 15,55 mln
[L, sprawdzone]. Dla Twojej kampanii najwazniejsze sa dzis: sakiewki zolnierzy (N6) i to, ze rownowage trzyma silny
podatek (N7, P9), a nie handel.

**S10. Doplata do 35%: trzy rozne poprawki.**
Warsztaty: od sprzedanego (S). Obieg: mnoznik wedlug zasobnosci klasy W (S). Sztuczne: z kapitalu warsztatu (M).
Rozstrzygniecie: N4 "od sprzedanego" jako pierwsza - jedyna przestaje placic za towar, ktorego nikt nie chce (bron na
polkach). Wariant obiegu tylko wtedy, gdy pomiar pokaze, ze to za malo. Wariant z kapitalu odrzucony: dochod notabli -90
tys. na dobe zepchnie ich pod pasmo 4 500, a pasmo dobiera z kas miast - nowa petla.

**S11. Zmiana typu warsztatu: trzy propozycje (warsztaty: aktywna wymiana; ceny: cena w wyborze typu; obieg: inwestycje L).**
To jedno zjawisko - jedna regula (P3) z jedna funkcja "oczekiwany wynik z cen targu" dla bankructwa, wymiany i BK (N10).

**S12. Receptury wedlug wartosci (ceny: wiecej wsadu) kontra "brakuje wyrobow" (warsztaty, produkcja).**
Wiecej wsadu na sztuke = mniej narzedzi i klejnotow; narzedzi jest dzis 100 na dobe na swiat, a lupy 245 / 231 na dobe
[L, sprawdzone]. Rozstrzygniecie: P11 dopiero po P1-P4 i tylko dla linii z nadmiarem wsadu; srebro przez cene rudy,
nie przez ilosc (srebro jest kupowane przez lud jako zbytek - 76% zapasu w karawanach).

**S13. Sol: o ile spadnie cena po N3?**
Przeglad produkcji: indeks z 8 do 2-3. Moj szacunek: 4-5, bo czesc soli lezy w wozach (824) i karawanach (1 172), a wsie
wykupuja ja zaraz po dowozie. Obie liczby to P; rozstrzygnie pomiar po wgraniu.

---

## 5. Co sprawdzilem sam w zrodlach (nie przepisane z przegladow)

| Teza | Wynik | Zrodlo |
|---|---|---|
| Budzet mieszczan bez towaru | 15 kategorii: 136 275 d, do wydania 30 823, roznica 105 452 - zgadza sie | log Jeffa l.19922, skrypt `raport/sumbud.py` |
| Sol: produkcja i zuzycie | Jeff srednio 900 / 896 (43 doby), 4. rok testu 926 / 924; w miastach 46; jednodniowe zuzycie 24-2 804 | log Jeffa l.19896, l.1135-12944; test 20-17-37; `raport/avg.py` |
| Len, plotno, skory, drewno | srednie 709/675, 224/203, 559/561, 2 225/2 201 (jednodniowe liczby z przegladu warsztatow i jednostek to doba 108945) | `raport/avg.py` |
| Warsztaty | 291 notabli + 97 ukrytych; pracowalo 168; wynik +9 044 | log l.19797, "Rynek surowcow" |
| Zloto swiata i skrzynie | test 219,37 -> 217,78 mln; rody 75,08 -> 55,30 mln; Jeff sakiewki 9,66 -> 15,55 mln, lud miast 17,08 -> 13,05 mln | "Pieniadz swiata" d108836, 110294, 108903, 108945 |
| Straznik | mnoznik 5,028, kiesy 1,577 x poziomu, pasmo 1,25-1,75 | "Straznik obiegu (obieg2): dzien 108945" |
| Notable "z niczego" | przelew z kapitalu warsztatow 94 656, odliczony w bilansie | "Pieniadz swiata (bilans - przyczyny): dzien 108945" |
| Korona | do wydania 553 631; P1 132 118 | "Korona: wplywy dnia (165): dzien 108946" |
| Wegiel | CharcoalWeight 0,5 w Settings.cs:94 i w Armoury.json l.213; przetop czyta wage (MaterialLaw.cs:115-118) | kod zb/int, Armoury.json |
| Klucze MCM | HistBulkUnitFactor, BkWorkshopInputsFix, FolkLadderCeilingMax - brak w Armoury.json (dzialaja domyslne) | grep Armoury.json |
| Wsie: koszyk | zakupy 2 605 z 67 291 (3,9%), 523 wsi bez towaru | "Kiesa ludu: dzien 108945" |

---

## 6. Pliki

- Ten raport: `scratchpad/roz/audyt-ekonomii/RAPORT-AUDYT-EKONOMII.md`.
- Moje skrypty sprawdzajace: `scratchpad/roz/audyt-ekonomii/raport/` (ln.py, sumbud.py, avg.py).
- Przeglady szczegolowe (tabele i skrypty): `produkcja-zuzycie/` (analiza.txt), `warsztaty/` (w4lata.txt, analiza2.out),
  `jednostki-masa/` (ceny.py), `ceny/py/` (fair.py, va.py, drift.py, lux.py, armor.py), `obieg/` (pws.py, bud.py, rody.py),
  `sztuczne/` (inwentarz.csv - 45 mechanizmow z wielkoscia dobowa).
- Logi: Twoja gra `Modules/Armoury/Armoury-2026-10-15_10-56-30.log`; test 4-letni
  `D:/Backup-Bannerlord/logs/2026-10-15-6lat/Armoury-2026-10-14_15-55-37.log` i `_20-17-37.log`.
