# PROJEKT: ZYCIE CODZIENNE - stany, koszyk dnia w kg, "Dostatek" i awans spoleczny (2026-10-16)

> ZASADA JEFFA 16.10 (WIAZACA, nadrzedna wobec liczb ponizej): "ma byc to system dynamiczny, ktory sam sie reguluje podaza i popytem, tak jak prawdziwa ekonomia".
> Zadnych sztywnych mnoznikow produkcji (x2.9 zboza itd. to tylko PRZEWIDYWANA rownowaga, nie ustawienie). Ustawiamy granice fizyczne (ziemia, ludzie, klimat, pora roku,
> pojemnosc pracy) i reguly decyzji (zysk przy cenie rynkowej, koszt zmiany, czas przejscia, bezwladnosc); produkcja i konsumpcja wynikaja z cen. Start neutralny:
> w dniu wlaczenia dzisiejsza mieszanka towarow kazdego okregu (bez skoku -20%), dalej zmiany tylko z cen i wolno (zasada zmiany zajecia Jeffa). AI lordowie inwestuja
> z wolnych pieniedzy ponad rezerwe wojny, gdy sie oplaca - wlaczane po tescie stabilnosci.


Zrodlo: workflow wf_c4f075f1-def (3 rozpoznania: towary, ludnosc, historia -> 2 modele -> ocena i synteza). STATUS: PROJEKT, NIC NIE WDROZONE.
Skrot w logach i paczkach: **ZC** (zycie codzienne). Tor pracy: **L** (pelny lancuch), paczki ZC0-ZC7 kazda jak tor M (1 workflow, recenzja, autotest 120 dob).

Prosba Jeffa (16.10, w skrocie): sprawdzic kazdy towar (ile sie produkuje, ile wazy, ile kosztuje kg); ustawic produkcje pod potrzeby 52 mln ludzi
(ok. 53 mln kg jedzenia dziennie, wedlug regionu i stanu: wyspy ryby, bogaci wiecej i lepiej, chlopi minimum); codzienne wydatki ludzi, zeby dobrobyt
naprawde podnosil zamoznosc obszaru i ludzie mogli awansowac; wojny niszcza dobrobyt; wskaznik zamoznosci na mieszkanca (nie "PKB"); pokazac, ile w regionie,
miescie, wsi i na calym obszarze jest chlopow, szlachty, mieszczan, arystokracji.

Oznaczenia pewnosci: **[K]** sprawdzone w kodzie (WGRANIE 72), **[L]** zmierzone w logach Jeffa (doby 108946-109027) lub tabelach, **[H]** historia (zrodla w
`historia.md` workflow), **[S]** moj szacunek / moje przeliczenie (skrypt `synteza/bilans_zc.py`).

---

## 0. Dla Jeffa - prostym jezykiem

1. **Sprawdzilem kazdy towar**: 61 towarow, 8 zwierzat i 60 ksiag. W obiegu jest 55. Tabela w rozdz. 1: produkcja na dobe, waga sztuki, cena za kg i ile kg dostaniesz za 1 zl.
   Ceny siegaja od 0.04 zl/kg (drewno) do 9 500 zl/kg (sztabka zlota). Garum, sztabek zlota, kamieni, purpury, atramentu, wolow, gesi, kur i ksiag nikt na swiecie nie robi.
2. **Codzienne zycie da sie policzyc.** 51.85 mln ludzi zjada dziennie 45.9 mln kg jedzenia i wypija 16.3 mln litrow, czyli na glowe 0.89 kg + 0.31 l, srednio 2 315 kcal.
   Twoje 53 mln kg sie zgadza: bez napojow wychodzi 46 mln, z napojami 62 mln. Sklad: zboze 26.3 tys. t, straczki z ogrodem i mlekiem 7.8 tys. t, owoce i warzywa 3.6 tys. t
   (jablka ok. 970 t), nabial 2.7 tys. t, ryby 2.65 tys. t, mieso 1.8 tys. t, sol 0.68 tys. t.
3. **Dzis towary gry to 1/300 tego** (171 t jedzenia na dobe). Chlopi jedza "racje" ze spichlerza, ktore nie maja ani kg, ani rodzaju. Po zmianie 77% jedzenia ludzie biora z wlasnego pola
   (spichlerz dostaje kg i sklad wedlug krainy), a 23% kupuja na targu. Targ jest probka: 1 worek na straganie to 37 prawdziwych workow. Tak bylo juz dotad, teraz ta liczba bedzie jawna
   i zapisana na stale. Dzieki temu pieniadze sie zgadzaja (koszyk z targu kosztuje 0.40 mln zl/d po cenach bazowych, a dzis lud miast placi 0.32 mln) i gra nie zwolni.
4. **Produkcja musi wzrosnac ok. 2.7 raza**: jedzenie i napoje z 19 tys. do 51 tys. sztuk na dobe. Zboze x2.9, ryby x2, mieso x2.2, ser x2.5, owoce x2.4-3.4, oliwa x12, garnki x13, opal x3.
   Piwa trzeba x25 (albo mieszczanie warza je sami z kupionego zboza), wina x78. Wino przyjdzie etapami, bo to nowy przemysl poludnia.
5. **7 stanow w naszej ksiedze ludzi**:

   | stan | swiat | Westeros | Essos |
   |---|---|---|---|
   | niewolni | 10.9% | 0.1% (tylko thralle Zelaznych Wysp) | 23.2% (Volantis 40%) |
   | chlopi | 71.1% | 86.5% | 53.5% |
   | biedota miast | 8.2% | 5.5% | 11.2% |
   | rzemieslnicy | 6.9% | 5.5% | 8.6% |
   | kupcy | 1.4% | 0.8% | 2.2% |
   | szlachta | 1.5% | 1.6% | 1.3% |
   | wielkie rody | ok. 10 tys. ludzi | | |

   BK liczy 3 mln ludzi, bo liczy tylko kropke na mapie. Nasza ksiega liczy cala kraine, 17 razy wiecej. Klasy BK zostaja tylko dla mechanik BK.
6. **Bogaci jedza wiecej i drozej.**

   | kto (Westeros) | kcal | jedzenie kg/d | mieso kg | ryby kg | piwo l | wino l | koszyk zl/rok |
   |---|---|---|---|---|---|---|---|
   | chlop | 2 330 | 0.90 (zboze 58%) | 0.025 | 0.03 | 0.30 | 0.01 | 224 |
   | kupiec | 3 040 | 1.13 | 0.20 | 0.16 | 0.90 | 0.21 | 680 |
   | szlachcic | 4 260 | 1.48 | 0.33 | 0.27 | 1.80 | 0.21 | 1 034 |
   | wielki rod | 5 620 | 2.00 | 0.55 | 0.45 | 2.00 | 0.60 | 1 953 |

   Ryby to 29% stolu na wyspach i 14% w Braavos. Na stepie stol to nabial i mieso. W Dorne i Volantis pija wino zamiast piwa.
7. **Wskaznik "Dostatek"** (po angielsku Livelihood) zastepuje PKB. Mowi, ile razy ludzi stac na koszyk przetrwania, czyli chleb, polewke, sol i opal.

   | Dostatek | co znaczy |
   |---|---|
   | 1 | ledwo zyja |
   | 2 | zwykly chlop |
   | 3 | rzemieslnik |
   | 7 | kupiec |
   | 10 i wiecej | szlachta |

   Swiat, ktory je swoje zwykle koszyki, ma Dostatek 2.9 (Anglia w 1300 r. ok. 2.5-2.9). Obok pokazujemy "Dochod na glowe" w zl na rok: swiat ok. 246, Anglia w 1300 r. 240.
8. **Wazne odkrycie**: dzis pieniadze ludu miast ida za zoldem wojska i za glowami BK, nie za ludzmi. Dochod na mieszczanina rozni sie miedzy krainami 92 razy:
   Zelazne Wyspy maja 12 razy srednia, a Braavos 0.2 sredniej. Dlatego zaczynamy od pomiaru, ktory nic w grze nie zmienia, a koszyk wlaczamy dopiero potem.
9. **Awans** zalezy od Dostatku i od oszczednosci. Na przyklad rzemieslnik zostaje kupcem przy Dostatku co najmniej 4 i zapasie na 60 dni.
   Tempo to ok. 3% luki na rok, spadek ok. 6% na rok. Pieniadze przechodza razem z ludzmi, nic nie znika.
10. **Wojna**: najazd (spalony spichlerz, zabrana kiesa wsi, brak rak do pracy) i zlupienie miasta juz dzis biora pieniadze i plon, wiec Dostatek spadnie sam.
   Dochodzi blizna: przy zlupieniu czesc kupcow spada do rzemieslnikow, a czesc rzemieslnikow do biedoty. Powrot trwa latami.
11. **Gdzie to zobaczysz**: w dymku wsi, w menu "People of this land", w "Look over the district" i w "Realm ledger" (krolestwo i swiat).
12. **Plan**: 8 paczek ZC0-ZC7. Kazda ma wlasny klucz w MCM i test 120 dob. Nic nie wgrywam, gdy grasz, a Ironman jest bezpieczny, bo nowe dane w zapisie maja start i wylacznik.

---

## 1. Kazdy towar w grze (pytanie 1)

Zrodla: wartosc i waga z CrashScribe items-dump (16.10, po naszych prawach), zgodne z linia ksiegi towarow dla 55 towarow [L]; produkcja, bilans i zapas - srednia
82 dob gry Jeffa z ksiegi towarow (GoodsLedger "Towary:") [L]; cena w miescie - mediana indeksu miast x wartosc (27 towarow), inaczej cena zaplacona przez wozy wsi
albo dwory [L]. Moneta: 1 zl gry = 1 pens (d). "kg za 1 zl" = ile kg towaru kupisz za 1 zl po wartosci bazowej. Pelna tabela 129 wierszy x 51 kolumn: `towary.tsv`.

| towar | grupa | kg/szt | wartosc d | d za kg | kg za 1 zl | cena w miescie d (mediana) | produkcja szt./d | produkcja t/d | bilans szt./d | zapas dni |
|---|---|---|---|---|---|---|---|---|---|---|
| zboze | zywnosc - zboze | 10 | 3 | 0.30 | 3.33 | 6.9 | 9036 | 90.4 | -534 | 13 |
| chleb | zywnosc - przetwor | 10 | 6 | 0.60 | 1.67 | 20.8 | 865 | 8.7 | -10 | 8 |
| placek | zywnosc - przetwor | 10 | 10 | 1.00 | 1.00 | 20.9 | 575 | 5.8 | +19 | 18 |
| mieso | zywnosc - mieso | 10 | 9 | 0.90 | 1.11 | 22.4 | 591 | 5.9 | -19 | 10 |
| mieso wieloryba | zywnosc - mieso | 10 | 7 | 0.70 | 1.43 | - | 561 | 5.6 | -3 | 9 |
| ryby | zywnosc - ryby | 10 | 10 | 1.00 | 1.00 | 15.5 | 2355 | 23.6 | -273 | 31 |
| ser | zywnosc - nabial | 10 | 13 | 1.30 | 0.77 | 31.6 | 411 | 4.1 | -62 | 14 |
| maslo | zywnosc - nabial | 10 | 24 | 2.40 | 0.42 | 78.7 | 304 | 3.0 | -38 | 20 |
| jaja | zywnosc - nabial | 10 | 8 | 0.80 | 1.25 | 51.8 | 255 | 2.5 | +50 | 30 |
| miod | zywnosc - slodkie | 10 | 14 | 1.40 | 0.71 | 107.0 | 64 | 0.6 | +5 | 12 |
| jablka | zywnosc - owoce/warzywa | 10 | 2 | 0.20 | 5.00 | 11.1 | 350 | 3.5 | +20 | 12 |
| pomarancze | zywnosc - owoce/warzywa | 10 | 10 | 1.00 | 1.00 | 53.9 (wozy) | 76 | 0.8 | +6 | 12 |
| marchew | zywnosc - owoce/warzywa | 10 | 2 | 0.20 | 5.00 | 10.3 (wozy) | 348 | 3.5 | +22 | 12 |
| winogrona | zywnosc - owoce/warzywa | 10 | 3 | 0.30 | 3.33 | 5.5 (wozy) | 390 | 3.9 | -68 | 13 |
| oliwki | zywnosc - owoce/warzywa | 10 | 5 | 0.50 | 2.00 | 5.8 (wozy) | 287 | 2.9 | -40 | 22 |
| daktyle | zywnosc - owoce/warzywa | 10 | 60 | 6.00 | 0.17 | 100.0 (wozy) | 92 | 0.9 | -27 | 32 |
| sol | przyprawa / konserwant | 10 | 2 | 0.20 | 5.00 | 15.2 | 1890 | 18.9 | +50 | 7 |
| przyprawy | przyprawa / luksus | 10 | 270 | 27.00 | 0.04 | 907.0 (wozy) | 4.1 | 0.0 | +0 | 37 |
| garum | przyprawa / sos | 10 | 20 | 2.00 | 0.50 | 76.0 | 0.0 | 0 | - | - |
| oliwa | oliwa (jedzenie i swiatlo) | 10 | 40 | 4.00 | 0.25 | 40.7 (dwory) | 32 | 0.3 | +5 | 63 |
| tran | olej (swiatlo) | 10 | 24 | 2.40 | 0.42 | 40.7 (wozy) | 5.6 | 0.1 | +0 | 63 |
| piwo | napoj | 10 | 3 | 0.30 | 3.33 | - | 452 | 4.5 | -18 | 3 |
| wino | napoj | 10 | 14 | 1.40 | 0.71 | 83.0 | 44 | 0.4 | +1 | 25 |
| miod pitny | napoj | 10 | 10 | 1.00 | 1.00 | 9.2 (wozy) | 84 | 0.8 | -5 | 34 |
| welna | wlokno | 10 | 83 | 8.30 | 0.12 | 177.0 | 354 | 3.5 | +13 | 16 |
| len | wlokno | 10 | 20 | 2.00 | 0.50 | 36.6 | 807 | 8.1 | +35 | 16 |
| jedwab surowy | wlokno luksusowe | 10 | 1000 | 100 | 0.01 | 756.0 (wozy) | 48 | 0.5 | +2 | 61 |
| plotno | tkanina | 10 | 100 | 10.00 | 0.10 | 120.0 | 255 | 2.5 | -6 | 12 |
| sukno (filc) | tkanina | 10 | 200 | 20.00 | 0.05 | 554.0 | 228 | 2.3 | +21 | 26 |
| aksamit | tkanina luksusowa | 10 | 4000 | 400 | 0.0025 | 40000.0 (sufit 10x) | 5.8 | 0.1 | +1 | 28 |
| skory surowe | surowiec zwierzecy | 10 | 10 | 1.00 | 1.00 | 16.2 | 567 | 5.7 | +5 | 4 |
| skora wyprawiona | polprodukt | 10 | 40 | 4.00 | 0.25 | 33.2 | 228 | 2.3 | +9 | 11 |
| futra | futro (luksus) | 10 | 200 | 20.00 | 0.05 | 756.0 | 106 | 1.1 | +34 | 54 |
| drewno (ladunek) | drewno | 100 | 4 | 0.04 | 25 | 2.6 | 2109 | 210.9 | -41 | 8 |
| wegiel drzewny | paliwo | 5 | 1 | 0.20 | 5.00 | - | 0.0 | 0 | -1 | 202 |
| deski | budulec | 10 | 2 | 0.20 | 5.00 | - | 9.9 | 0.1 | -0 | 6 |
| glina | surowiec | 10 | 1 | 0.10 | 10 | 1.3 (wozy) | 490 | 4.9 | +16 | 11 |
| garnki | rzemioslo (naczynia) | 10 | 8 | 0.80 | 1.25 | - | 25 | 0.2 | +0 | 7 |
| wapien | budulec | 10 | 1 | 0.10 | 10 | 1.3 (wozy) | 182 | 1.8 | -0 | 9 |
| marmur | budulec | 10 | 2 | 0.20 | 5.00 | 3.3 (wozy) | 36 | 0.4 | +0 | 10 |
| narzedzia | rzemioslo (narzedzia) | 10 | 40 | 4.00 | 0.25 | 98.4 | 110 | 1.1 | +3 | 14 |
| lupa zelaza (ladunek rudy) | kruszec | 15 | 38 | 2.53 | 0.39 | 41.0 | 246 | 3.7 | +1 | 21 |
| sztabki zelaza 1-6 | metal | 0.5 | 1-6 | 2-12 | 0.08-0.5 | - | 0.0 | 0 | +0 | - |
| ruda srebra | kruszec | 10 | 20 | 2.00 | 0.50 | 66.6 | 165 | 1.6 | +0 | 36 |
| ruda zlota | kruszec | 10 | 50 | 5.00 | 0.20 | 69.5 (wozy) | 4.2 | 0.0 | -0 | 31 |
| sztabka zlota | metal szlachetny | 0.5 | 4750 | 9500 | 0.0001 | - | 0.0 | 0 | - | - |
| klejnoty (wyroby) | luksus | 10 | 2400 | 240 | 0.0042 | 8880.0 | 17 | 0.2 | +2 | 43 |
| kamienie szlachetne | luksus | 1 | 1200 | 1200 | 0.0008 | - | 0.0 | 0 | - | - |
| kosc morsa | luksus | 10 | 300 | 30.00 | 0.03 | 69.9 (wozy) | 0.8 | 0.0 | +0 | 65 |
| purpura | barwnik (luksus) | 10 | 300 | 30.00 | 0.03 | - | 0.0 | 0 | - | - |
| atrament | pismo | 10 | 100 | 10.00 | 0.10 | - | 0.0 | 0 | - | - |
| papirus | pismo | 10 | 100 | 10.00 | 0.10 | 628.0 | 10 | 0.1 | +0 | 42 |
| krowa | zwierze hodowlane | 200 | 113 | 0.56 | 1.77 | 92.8 (wozy) | 95 | 18.9 | +1 | 19 |
| wol | zwierze pociagowe | 200 | 157 | 0.79 | 1.27 | - | 0.0 | 0 | - | - |
| swinia | zwierze hodowlane | 120 | 30 | 0.25 | 4.00 | 30.4 (wozy) | 245 | 29.4 | -0 | 7 |
| owca | zwierze hodowlane | 40 | 17 | 0.42 | 2.35 | 21.5 (wozy) | 204 | 8.2 | +2 | 8 |
| ges | drob | 5 | 4 | 0.80 | 1.25 | - | 0.0 | 0 | - | - |
| kura | drob | 5 | 1 | 0.20 | 5.00 | - | 0.0 | 0 | - | - |
| ksiegi (60 rodzajow) | pismo | 1-2.5 | 700-1500 | - | - | - | 0 | 0 | - | - |

Razem [L]: produkcja 25 600 szt./d = 498 t/d (w tym drewno 211 t w ladunkach po 100 kg); jedzenie i napoje 17 100 szt./d = 171 t/d; wartosc 420 tys. zl/d po cenach
bazowych, 1.10 mln zl/d po cenach miast. Konie (75 rodzajow, 71 wsi z ranczami) nie sa w ksiedze towarow - do dopisania (ZC1).
Najwazniejsze dziwactwa towarow i ich poprawki: rozdz. 13.

---

## 2. Jak jest dzis - trzy rzeczy, ktore trzeba wiedziec

**2.1 Dwie skale ludzi.** Nasza ksiega (PopulationLaw, PeopleBook) liczy 51.85 mln ludzi: wsie 41.42 mln (80%), miasta 10.43 mln (20%), zamki 0 (ich ludzie sa w ich wsiach) [K, L].
Westeros 27.61 mln (9.3% w miastach), Essos 24.23 mln (32.5% w miastach). Banner Kings liczy 2.98 mln, bo liczy tylko ludzi samej kropki na mapie: wies = hearth x 4-6,
miasto 8-15 tys. x (1 + dobrobyt/10 000), zamek 2-3 tys. [K: PopulationManager.GetDesiredTotalPop]. To nie blad, tylko inna skala - srednio 17.4 naszego czlowieka na 1 BK
(wsie x41, miasta x6.9) [L]. Naszej skali uzywaja: przyrost, glod, spichlerze, uchodzcy, pobor, renty. Skali BK: glowy kiesy ludu, popyt BK, ochotnicy, zadowolenie klas.

**2.2 Klasy ma dzis tylko BK** (szlachta 3.1%, rzemieslnicy 5.0%, chlopi 70.9%, dzierzawcy 3.0%, niewolni 18.0%) - z gory narzuconymi widelkami wedlug rodzaju osady,
nie od bogactwa [K: GetDesiredPopTypes]. Lamia lore: 329 tys. niewolnych BK w Westeros (w skali ksiegi ok. 5 mln), 3.5-5.5% szlachty we wsiach, brak kupcow i duchownych.
Nasza ksiega ma konta (w domu, uchodzcy, sluzba, jency, niewolni 31 tys. - glownie thralle Zelaznych Wysp), bez stanow [K, L].

**2.3 Jedzenie ludzi prawie nie jest towarem** [K, L]:
- wiesniacy (41.4 mln) jedza racje ze spichlerza okregu (Famine): plon 48.97 mln racji/d na 41.44 mln zjedzonych (118%), zapas na 727 dni; racja nie ma kg ani rodzaju;
- miasta jedza staty "Food" warowni (dobrobyt/40, ok. 13.5 tys./d) i towar z polek: mieszczanie 9 261 szt./d = 93 t (8.9 g na mieszczanina ksiegi);
- caly swiat produkuje ok. 171-184 t jedzenia-towaru na dobe; 52 mln ludzi potrzebuje ok. 46 000 t jedzenia stalego - dzis towar to 0.4% potrzeby.

**2.4 Pieniadz ludu nie idzie za ludzmi** [L, S]. Dochod kiesy ludu miast na mieszczanina ksiegi (CSV kiesy, doba 109027) wobec sredniej swiata:
Zelazne Wyspy 12.2x, Dorne 11.7x, Smocza Skala 8.9x, Ibben 3.6x, Dorzecze 3.6x, Dolina 3.5x, Reach 1.5x, Qarth 0.46x, Volantis 0.36x, Braavos 0.22x, Lys 0.18x, Tyrosh 0.13x
(rozpietosc 92 razy). Przyczyny: (1) 63% dochodu ludu miast to zold wydany przez zolnierzy (181 z 288 tys. zl/d), a zolnierz jest w grze 1:1, lud - probka;
(2) glowy kiesy liczone sa od BK (Yronwood: 22 tys. ludzi ksiegi, 12.5 tys. glow kiesy; Braavos: 432 tys. ludzi, 42 tys. glow). Wskaznik zamoznosci na glowe wlaczony dzis
pokazalby gestosc zaludnienia i garnizony, nie bogactwo. Projekt to naprawia przez obrot targu od ludzi (rozdz. 7) i liczenie Dostatku w skali ksiegi (rozdz. 9).

---

## 3. Ocena dwoch modeli i wybor

| kryterium | Model 1 "Koszyk stanow" (rozbudowa kiesy ludu, spichlerza, skala targu 1:37) | Model 2 "Spizarnia" (osobna ksiega kg, zakupy prawdziwa moneta) |
|---|---|---|
| sens dla gracza | dobry: stany, Dostatek, stol w kg w dymku; probka targu wymaga zdania wyjasnienia | bardzo dobry: prawdziwe kg, brak widac na polce, odbudowa daje popyt |
| obieg zamkniety | pieniadz bez zmian: koszyk z targu 0.40-0.56 mln zl/d wobec dzisiejszych ok. 0.33-0.59 mln | wymaga 11.7 mln zl/d obrotu (x20 dzis) przy 221 mln zl w swiecie; kiesy ludu 20.5 mln przy potrzebie 25-35 mln; renty panow do x33 - trzeba kotwic i renty w naturze |
| realizm | kg na stan i kraine, awans od Dostatku; wojsko 1:1 przy targu 1:37 (6% targu) | wyzszy: 8 stanow z duchownymi, pora roku, kapital (stada, narzedzia), drabina zakupow |
| ryzyko balansu | srednie: produkcja x2.7, napoje x25-80, transport x3 | wysokie: predkosc pieniadza x20, lordowie i armie, podwojne liczenie z BK i konsumpcja gry |
| wydajnosc | ok. 100 tys. dzialan na dobe, popyt O(1); sztuk na targu x2.7 | 83 tys. komorek + ceny 228 targow x 25 towarow + dolewanie 227 polek; zapis +150-250 KB |
| ryzyko wdrozenia | umiarkowane: korzysta z DemandPostfix, AnchorBudgetPostfix, GoodsRates, TownCrafts, Famine | wysokie: wymiana ItemConsumption i BK MakeConsumption, nowa ksiega, przelozenie kies w trakcie Ironmana |

**Wybor: scalenie.** Szkielet Modelu 1 (jedna skala targu sigma = 37, pieniadz bez nowej pompy, wszystko na istniejacych czesciach). Z Modelu 2 biore to, co jest tanie,
a podnosi sens i realizm:
1. **drabina zakupow** wedlug ceny za 1000 kcal (uogolnione FoodSubstitute): najpierw przetrwanie najtanszym, potem reszta; zabija tylko brak na pierwszym szczeblu;
2. **piwo i wino chlopa z domu** (chlop warzy i tloczy sam; z targu bierze 10%) - Model 1 kazal mu kupowac cale wino, co dawalo wino x212;
3. **warstwa "dom"** (straczki, ogrod, mleko) tylko w ksiedze, bez towaru;
4. **"Spizarnia" jako pomiar** (linia i CSV w kg: z pola + z targu x sigma), nie osobny zapas;
5. **oszczednosci jako warunek awansu**, **miejsca** (ile kupcow utrzyma obrot), **pieniadze ida z ludzmi**, oplaty cechowe i kupno ziemi do odbiorcy (zamkniete);
6. **powiazanie z dobrobytem kotwiczone do mediany krainy** (start neutralny dla AI);
7. **wielkie rody liczone z bohaterow** (1:1, rody gry x3 = ok. 10 tys.);
8. **blizna wojny** (zlupienie spycha kupcow i rzemieslnikow o stan nizej).
Odrzucam z Modelu 2: zakupy w prawdziwej skali monety, wymiane konsumpcji gry, dolewanie polek z ksiegi, renty w naturze, osobny stan duchownych (na pozniej).
Zmieniam w Modelu 1: wino chlopow z domu, czynniki produkcji wedlug krainy (nie jeden mnoznik swiata), arystokracja 10 tys. zamiast 5 tys., skala Dostatku dopasowana do kosztu koszykow.

---

## 4. Model scalony - zasada w jednym rysunku

```
      ludzie ksiegi (51.85 mln) --podzial--> 7 stanow (w domu, na osade)
                 |                                     |
      koszyk stanu x kraina x (pora roku)  [kg/os/dobe]
                 |
      +----------+--------------------------+
      | z wlasnego pola / domu (77% stalego) | -> spichlerz okregu (Famine): racja = stol stanu w kg, ten sam plon q (zima, najazd, rece)
      | z targu (23% stalego, 34% napojow)   | -> popyt kategorii = kg / sigma / kg sztuki  (sigma = 37, w zapisie)
      +--------------------------------------+         |
                                                        v
            kiesy stanow (W/R/P/S miasta, podzamcze, wies, majatki BK, dwory rodow) placa kasie kupcow  ->  wozy wsi, warsztaty, karawany
                                                        |
            Dostatek = dochod stanu (x sigma, plus wlasny plon) / koszyk przetrwania po cenach lokalnych  ->  awans, dobrobyt gry, widok
      wojsko, lordowie, gracz: 1:1 jak dzis (jedza i kupuja prawdziwe sztuki)
```

Trzy zasady:
- **Co gra pokazuje probka (lud miast i wsi), liczy sie w towarze x sigma. Co jest w grze 1:1 (lordowie, zolnierze, gracz, notable), liczy sie 1:1.**
- **Zadnego nowego zlota.** Stany zmieniaja tylko to, kto czego potrzebuje; Dostatek tylko mierzy. Pieniadz plynie istniejacymi drogami (rozdz. 8).
- **Najpierw towar, potem popyt.** Popyt stanow wlaczony przed produkcja da ceny na suficie i glod w miastach.

Sigma = 37 (widelki 28-73, wyznaczone w projekcie "gospodarka od ludzi" z jedzenia, rozdz. 4A [K: docs/gospodarka-od-ludzi]). Sprawdzenie pieniedzmi [S]: renta nalezna
od 51.85 mln ludzi to 5.70 mln zl/d, zaplacona 171.6 tys. - prawie dokladnie 5.70 mln / 37 = 154 tys. Pieniadz ludu juz dzis zyje w skali ok. 1:33-37.
Wrazliwosc (koszyk z targu cywili, ceny bazowe): sigma 28 - 531 tys. zl/d i 58 tys. szt./d jedzenia; sigma 37 - 402 tys. i 44 tys.; sigma 50 - 297 tys. i 33 tys. Zostaje 37.

---

## 5. Stany

**5.1 Definicje.** Stany to podzial konta DOM ("w domu") kazdej osady; ksiega ludzi zostaje jedynym licznikiem. Zamek ma 0 ludzi - jego ludzie sa w jego wsiach.

| stan (PL) | EN w grze | kto | kiesa (skad pieniadze) |
|---|---|---|---|
| niewolni | Slaves | kultury z niewola (lista LosersFlee.SlaveCultureIds [K]) i thralle Zelaznych Wysp; konto NIEWOLNI ksiegi (31 tys.) dopisane do tego stanu | koszyk placa wlasciciele (R 40% / P 60%, jak dzis) |
| chlopi | Peasants | chlopi, dzierzawcy, zagrodnicy, pasterze; w miescie 2% (pola miejskie) | kiesa wsi + wlasny plon |
| biedota miast | Labourers | wyrobnicy, sluzba, tragarze | kiesa W |
| rzemieslnicy | Craftsmen | mistrzowie i czeladnicy w miescie i na wsi (na razie z duchownymi) | kiesa R (miasto), podzamcze (zamek), kiesa wsi |
| kupcy | Merchants | kupcy i patrycjat; wies 0.2% (mlynarze, handlarze) | kiesa P |
| szlachta | Gentry | rycerze, ziemianie z majatkiem i ich domy | majatki BK (EstateIncome187), w miescie kiesa P |
| wielkie rody | Great houses | rody gry z lennem: bohaterowie x3 (krewni, dwor) = ok. 10 tys. | zloto rodu (1:1), zakupy przez dwory (CourtGoods) |

Mieszczanie w widoku = biedota + rzemieslnicy + kupcy. Duchowni (septoni, maesterzy, kaplani R'hllora) - osobny stan w ZC8, dzis w rzemieslnikach.

**5.2 Pasma startowe** (udzial ludzi w domu; niewolni zabieraja swoj udzial z calej reszty):

| gdzie | niewolni (miasto / wies) | chlopi | biedota | rzemieslnicy | kupcy | szlachta |
|---|---|---|---|---|---|---|
| wies Westeros (domyslnie) | 0 | 95.3 | - | 3.0 | 0.2 | 1.5 |
| wies Reach | 0 | 94.8 | - | 3.0 | 0.2 | 2.0 |
| wies Polnocy, Skagos, Nocnej Strazy | 0 | 95.8 | - | 3.0 | 0.2 | 1.0 |
| Wolni Ludzie | 0 | 99.0 | - | 0.5 | 0 | 0.5 (wodzowie) |
| Dothrakowie | 10 / 10 | 97.5 (pasterze) | - | 0.5 | 0 | 2.0 (starszyzna) |
| miasto Westeros | 0 | 2 | 60 | 30 | 6 | 2 |
| Krolewska Przystan (crownlands) | 0 | 2 | 57 | 30 | 7 | 4 (dwor) |
| miasto Wolnych Miast i Wschodu | jak nizej | 2 | 53 | 33 | 10 | 2 |
| Volantis | 70 / 30 | | | | | |
| Zatoka Niewolnicza (Ghis) | 55 / 35 | | | | | |
| Qarth | 40 / 20 | | | | | |
| Valyria | 30 / 25 | | | | | |
| Lys, Myr, Tyrosh | 35 / 20 | | | | | |
| Qohor | 25 / 12 | | | | | |
| Norvos | 15 / 8 | | | | | |
| Zelazne Wyspy (thralle) | 6 / 5 | | | | | |
| Braavos, Pentos, Lorath, Ibben, Yi Ti, Sarnor, Wyspy Letnie | 0 | | | | | |

Stary zapis i nowa kampania: pasma pochylone udzialem BK osady (x clamp(udzial BK / srednia BK, 0.7-1.3) dla rzemieslnikow i kupcow) - miasto, ktore BK ma za bogatsze,
startuje z wieksza liczba R i P. Konto NIEWOLNI ksiegi dopisane do stanu niewolnych osady. Niewolni to etykieta w DOM, nie nowe konto: pobor i renta ich nie biora
(placi wlasciciel). To zgodne z decyzja E4/9 "niewola tylko w kanonie" - pasma dotycza tylko kultur kanonicznie niewolniczych (wylacznik ZC: DailyLifeCanonSlaves).

**5.3 Wynik - swiat** [S, bilans_zc.py, doba 109027]:

| stan | ludzie | % | w miastach | Westeros | Essos |
|---|---|---|---|---|---|
| niewolni | 5.65 mln | 10.9% | 2.75 mln | 0.1% (thralle) | 23.2% |
| chlopi | 36.86 mln | 71.1% | 0.15 mln | 86.5% | 53.5% |
| biedota miast | 4.24 mln | 8.2% | 4.24 mln | 5.5% | 11.2% |
| rzemieslnicy | 3.59 mln | 6.9% | 2.46 mln | 5.5% | 8.6% |
| kupcy | 0.75 mln | 1.4% | 0.67 mln | 0.8% | 2.2% |
| szlachta | 0.76 mln | 1.5% | 0.16 mln | 1.6% | 1.3% |
| wielkie rody | ok. 10 tys. | 0.02% | - | - | - |

Historia [H]: szlachta i ziemianie Anglii 1436 ok. 1.6% gospodarstw; Domesday 1086 niewolni ok. 10%; Italia rzymska 15-25% niewolnych; miasta 15-20% ludzi.

**5.4 Wynik - krainy** (% ludzi) [S]:

| kraina | ludzie mln | niewolni | chlopi | biedota | rzemieslnicy | kupcy | szlachta |
|---|---|---|---|---|---|---|---|
| Reach | 7.98 | 0.0 | 85.5 | 6.0 | 5.7 | 0.8 | 2.0 |
| Volantis | 4.99 | 40.0 | 50.2 | 4.0 | 4.0 | 0.9 | 0.9 |
| Qarth | 3.99 | 27.4 | 48.5 | 11.8 | 8.8 | 2.3 | 1.2 |
| Westerlands | 3.98 | 0.0 | 87.8 | 4.8 | 5.2 | 0.7 | 1.5 |
| Dorzecze | 3.48 | 0.0 | 87.9 | 4.8 | 5.1 | 0.7 | 1.5 |
| Dolina | 2.99 | 0.0 | 89.7 | 3.6 | 4.6 | 0.5 | 1.5 |
| Polnoc | 2.98 | 0.0 | 88.3 | 4.8 | 5.1 | 0.7 | 1.1 |
| Zatoka Niewolnicza | 2.98 | 43.0 | 37.5 | 9.6 | 7.1 | 1.9 | 0.9 |
| Braavos | 2.49 | 0.0 | 65.4 | 17.0 | 12.6 | 3.3 | 1.7 |
| Krainy Burzy | 2.49 | 0.0 | 90.7 | 3.0 | 4.3 | 0.5 | 1.5 |
| Norvos | 1.49 | 10.8 | 53.3 | 18.0 | 12.9 | 3.5 | 1.5 |
| Pentos | 1.49 | 0.0 | 64.5 | 17.5 | 12.9 | 3.4 | 1.7 |
| Qohor | 1.49 | 16.3 | 56.6 | 13.1 | 9.9 | 2.6 | 1.4 |
| Dorne | 1.48 | 0.0 | 87.9 | 4.7 | 5.1 | 0.7 | 1.5 |
| Lys / Myr / Tyrosh | po 0.99 | 26.0 | 46.1 | 13.9 | 10.1 | 2.7 | 1.2 |
| Smocza Skala | 0.81 | 0.0 | 90.8 | 2.9 | 4.3 | 0.5 | 1.5 |
| Lorath | 0.79 | 0.0 | 66.2 | 16.5 | 12.4 | 3.3 | 1.7 |
| Dothrakowie | 0.74 | 10.0 | 87.7 | 0.0 | 0.4 | 0.0 | 1.8 |
| Ziemie Korony | 0.59 | 0.0 | 24.9 | 43.0 | 23.4 | 5.3 | 3.4 |
| Zelazne Wyspy | 0.47 | 5.0 | 86.2 | 2.8 | 4.1 | 0.5 | 1.4 |
| Ibben / Sarnor | 0.34 / 0.24 | 0.0 | 76.6 | 10.6 | 9.0 | 2.2 | 1.6 |
| Wolni Ludzie | 0.29 | 0.0 | 99.0 | 0.0 | 0.5 | 0.0 | 0.5 |
| Wyspy Letnie | 0.11 | 0.0 | 86.0 | 5.3 | 6.0 | 1.2 | 1.5 |
| Yi Ti | 0.10 | 0.0 | 76.8 | 10.5 | 8.9 | 2.1 | 1.6 |
| Skagos / Nocna Straz | 0.05 / 0.02 | 0.0 | 95.8 | 0.0 | 3.0 | 0.2 | 1.0 |
| Valyria | 0.05 | 25.0 | 71.5 | 0.0 | 2.2 | 0.1 | 1.1 |

---

## 6. Koszyki - kto ile je (kg na osobe na dobe, srednia z dziecmi)

**6.1 Koszyk "Zachodu"** (Westerlands, Krainy Burzy, Ziemie Korony) po dopelnieniu zbozem do kcal stanu [S z H: tabela A historia - Allen, Dyer, Stone; dwory - racje 0.9-1.4 kg chleba
i 4.5 l piwa na czlonka dworu, srednio ze sluzba i dziecmi]. "Dom" = straczki, ogrod, mleko (bez towaru w grze).

| stan | zboze | dom | mieso | ryby | nabial | owoce/warz. | oliwa | sol | miod | przyprawy | piwo l | wino l | miod pitny l | kcal |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| niewolni | 0.53 | 0.07 | 0.012 | 0.016 | 0.010 | 0.03 | 0.002 | 0.010 | 0 | 0 | 0.10 | 0 | 0 | 1 979 |
| chlopi | 0.52 | 0.18 | 0.025 | 0.032 | 0.060 | 0.06 | 0.002 | 0.013 | 0.002 | 0 | 0.30 | 0.006 | 0.002 | 2 328 |
| biedota miast | 0.50 | 0.08 | 0.030 | 0.040 | 0.030 | 0.05 | 0.003 | 0.012 | 0.002 | 0 | 0.35 | 0.006 | 0.002 | 2 122 |
| rzemieslnicy | 0.49 | 0.09 | 0.070 | 0.064 | 0.050 | 0.08 | 0.005 | 0.015 | 0.004 | 0.0003 | 0.55 | 0.024 | 0.008 | 2 369 |
| kupcy | 0.45 | 0.06 | 0.200 | 0.160 | 0.080 | 0.14 | 0.009 | 0.020 | 0.008 | 0.003 | 0.90 | 0.21 | 0.016 | 3 038 |
| szlachta | 0.57 | 0.04 | 0.330 | 0.272 | 0.080 | 0.12 | 0.012 | 0.028 | 0.015 | 0.006 | 1.80 | 0.21 | 0.040 | 4 262 |
| wielkie rody | 0.62 | 0.03 | 0.550 | 0.448 | 0.100 | 0.15 | 0.018 | 0.040 | 0.020 | 0.020 | 2.00 | 0.60 | 0.040 | 5 620 |

Do tego rocznie (kg na osobe): plotno chlop 1.2 / biedota 1.0 / rzemieslnik 1.5 / kupiec 3 / szlachta 4 / rod 6; sukno 0.8 / 0.5 / 1.2 / 4 / 6 / 10; skora 0.5-4;
narzedzia chlop 0.4, rzemieslnik 1.0; garnki 0.8-5; swiatlo (tran, oliwa) 0.3-15; futra, jedwab i klejnoty u kupcow, szlachty i rodow. Opal: mieszczanin kupuje 1.5 kg/d
(wies zbiera sama).

**Koszt koszykow** (ceny bazowe, indeks 1.0) [S]:

| stan | stale kg | napoje l | jedzenie zl/d | odziez i sprzet zl/d | opal zl/d | razem zl/d | zl/rok | ile koszykow przetrwania |
|---|---|---|---|---|---|---|---|---|
| niewolni | 0.68 | 0.10 | 0.263 | 0.036 | 0.040 | 0.339 | 124 | 1.3 |
| chlopi | 0.90 | 0.31 | 0.464 | 0.092 | 0.060 | 0.616 | 224 | 2.3 |
| biedota miast | 0.75 | 0.36 | 0.421 | 0.064 | 0.040 | 0.526 | 191 | 2.0 |
| rzemieslnicy | 0.86 | 0.58 | 0.624 | 0.138 | 0.060 | 0.822 | 299 | 3.1 |
| kupcy | 1.13 | 1.13 | 1.347 | 0.422 | 0.100 | 1.869 | 680 | 7.0 |
| szlachta | 1.48 | 2.05 | 2.003 | 0.678 | 0.160 | 2.841 | 1 034 | 10.7 |
| wielkie rody | 2.00 | 2.64 | 3.442 | 1.682 | 0.240 | 5.365 | 1 953 | 20.2 |

**Koszyk przetrwania** (Allen "bare bones" [H]): 0.47 kg zboza, 0.055 kg straczkow, 0.014 kg miesa, 0.008 kg tluszczu, 0.01 kg soli, 1 kg odziezy na rok, w miescie 1 kg opalu.
Kosztuje 0.226 zl/d na wsi (82 zl/rok) i 0.266 zl/d w miescie (97 zl/rok). Allen podaje ok. 98 d. Bogaci kupuja drozsze kalorie, a nie wiele wiecej kalorii [H: owsianka < 1 d,
chleb 2 d, mieso 3 d, piwo 4 d za 1000 kcal].

**6.2 Profile krain** (mnozniki Modelu 1; zboze dopelnia kcal; dodatek kazdej grupy ma gorny limit, np. ryby +0.30 kg, wino +1 l; na poludniu wino zastepuje piwo):

| profil | krainy | zmiana | ludzie mln | w miastach | kg stalego/os | ryby % | mieso % | nabial % | zboze % | napoje l/os |
|---|---|---|---|---|---|---|---|---|---|---|
| wyspy | Zelazne Wyspy, Skagos, Smocza Skala | ryby x15 | 1.32 | 5% | 1.04 | 29.1 | 3.1 | 5.6 | 43 | 0.34 |
| polnoc | Polnoc, Wolni Ludzie, Nocna Straz | ryby x2, mieso i nabial x1.3, miod pitny x1.5 | 3.30 | 7% | 0.89 | 5.2 | 4.6 | 8.6 | 57 | 0.34 |
| dorzecze | Dorzecze, Dolina | nabial x1.3 | 6.48 | 7% | 0.90 | 3.5 | 3.7 | 8.5 | 57 | 0.42 |
| zachod | Westerlands, Krainy Burzy, Ziemie Korony | baza (ryby x1.6) | 7.06 | 13% | 0.90 | 4.5 | 3.9 | 6.4 | 58 | 0.37 |
| reach | Reach | owoce x1.6, wino x2 | 7.98 | 10% | 0.91 | 2.8 | 4.3 | 5.8 | 56 | 0.34 |
| dorne | Dorne | owoce i oliwa x2, wino zamiast piwa | 1.48 | 8% | 0.91 | 3.4 | 2.2 | 3.2 | 57 | 0.26 |
| wm_polnoc | Braavos, Pentos, Lorath, Ibben | ryby x5 | 5.11 | 31% | 0.92 | 14.4 | 4.6 | 5.9 | 52 | 0.49 |
| wm_wnetrze | Norvos, Qohor | mieso x1.3 | 2.98 | 37% | 0.84 | 2.5 | 6.1 | 6.4 | 60 | 0.45 |
| wm_poludnie | Volantis, Lys, Myr, Tyrosh, Valyria | ryby x2 (Lys, Tyrosh x1.6 wiecej), oliwa x1.8, wino zamiast piwa | 8.00 | 30% | 0.83 | 5.9 | 2.9 | 3.4 | 61 | 0.21 |
| wschod | Qarth, Zatoka Niewolnicza, Yi Ti | daktyle, oliwki | 7.06 | 38% | 0.85 | 3.4 | 3.0 | 3.3 | 64 | 0.11 |
| letnie | Wyspy Letnie | ryby x12, owoce x2.5 | 0.11 | 10% | 1.14 | 22.3 | 1.2 | 1.5 | 45 | 0.02 |
| step | Dothrakowie, Sarnor | nabial x3, mieso x2.5 (kumys w "dom") | 0.98 | 5% | 0.93 | 0.5 | 8.2 | 18.0 | 53 | 0.00 |

Owoce wedlug profilu: na polnocy jablka i marchew; w Dorne i na poludniu pomarancze, oliwki, winogrona, daktyle. Przyklady stolu [S]: szlachcic Zelaznych Wysp 0.47 kg ryb
i 0.33 kg miesa; chlop Volantis 0.51 kg zboza, 0.16 l wina, 0.06 l piwa; szlachcic stepu 0.63 kg miesa i 0.24 kg nabialu.

**6.3 Pora roku** (ZC8, prosto): zima - swieze ryby, owoce i ogrod x0.5, konserwy (ser, solone mieso, sol) x1.3, opal x1.5-2 (Polnoc x2); jesien - sol x2 na solenie. Zboze dopelnia kcal.

---

## 7. Bilans swiata - moje przeliczenie (z ludnosc.tsv i towary.tsv)

**7.1 Potrzeba w prawdziwych kg** [S, bilans_zc.py]: jedzenie stale 45.9 mln kg/d (0.885 kg/os), napoje 16.3 mln l/d (0.314 l/os), srednio 2 315 kcal.

| grupa | potrzeba t/d | z wlasnego pola / domu t/d | z targu t/d | % z targu |
|---|---|---|---|---|
| zboze (ziarno, chleb, placek) | 26 309 | 20 129 | 6 180 | 23% |
| dom: straczki, ogrod, mleko | 7 768 | 7 768 | 0 | 0% |
| mieso | 1 797 | 991 | 806 | 45% |
| ryby | 2 653 | 1 381 | 1 272 | 48% |
| nabial i jaja | 2 716 | 2 152 | 564 | 21% |
| owoce i warzywa | 3 640 | 2 588 | 1 052 | 29% |
| oliwa i tluszcz | 222 | 79 | 142 | 64% |
| sol | 677 | 0 | 677 | 100% |
| miod | 114 | 68 | 47 | 41% |
| przyprawy | 8 | 0 | 8 | 100% |
| piwo (tys. l) | 13 117 | 8 990 | 4 127 | 31% |
| wino (tys. l) | 3 020 | 1 742 | 1 277 | 42% |
| miod pitny (tys. l) | 146 | 73 | 73 | 50% |
| **razem stale** | **45 904** | **35 155** | **10 748** | **23%** |
| **razem napoje (tys. l)** | **16 283** | **10 805** | **5 478** | **34%** |

Owoce i warzywa wedlug rodzaju (calosc / z targu, t/d): jablka 971 / 247, marchew 1 096 / 304, pomarancze 255 / 78, winogrona do jedzenia 562 / 164, oliwki 516 / 164, daktyle 239 / 93.
Miasta biora z targu 7.6 mln kg stalego. Zboze z targu w 69% potrzebuje Essos (Volantis, Qarth, Zatoka - 30-38% ludzi w miastach).

**7.2 Towary gry: cel na dobe** (sigma 37; cel = cywile z targu + dzisiejsze wojsko, zalogi zamkow, dwory, warsztaty i budowy 1:1; wsad = surowiec na przetwory) [S]:

| towar | cywile szt./d | wojsko, zamki, dwory, warsztaty, budowy | cel szt./d | wsad do przetworow | produkcja dzis | cel / dzis |
|---|---|---|---|---|---|---|
| zboze | 9 220 | 3 595 | 12 815 | +13 575 (chleb, placek, piwo) = 26 390 | 9 036 | x2.9 |
| chleb | 6 122 | 194 | 6 316 | - | 865 | x7.3 |
| placek | 1 360 | 267 | 1 628 | - | 575 | x2.8 |
| mieso (z miesem wieloryba) | 2 179 | 192 | 2 504 | - | 1 152 | x2.2 |
| ryby | 3 439 | 1 246 | 4 685 | - | 2 355 | x2.0 |
| ser | 762 | 276 | 1 038 | - | 411 | x2.5 |
| maslo | 381 | 142 | 523 | - | 304 | x1.7 |
| jaja | 381 | 87 | 469 | - | 255 | x1.8 |
| jablka | 669 | 174 | 842 | - | 350 | x2.4 |
| marchew | 822 | 187 | 1 010 | - | 348 | x2.9 |
| pomarancze | 211 | 46 | 258 | - | 76 | x3.4 |
| winogrona | 444 | 202 | 646 | +4 491 (wino) = 5 137 | 390 | x13.2 |
| oliwki | 444 | 211 | 655 | +1 931 (oliwa) = 2 586 | 287 | x9.0 |
| daktyle | 252 | 62 | 314 | - | 92 | x3.4 |
| oliwa | 385 | 1 | 386 | - | 32 | x12.2 |
| sol | 1 830 | 1 | 1 831 | - | 1 890 | x1.0 (zle rozwozona) |
| miod | 126 | 41 | 167 | - | 64 | x2.6 |
| przyprawy | 22 | 0 | 22 | - | 4 | x5.3 |
| piwo | 11 155 | 106 | 11 261 | - | 452 | x24.9 (albo zboze na piwo domowe) |
| wino | 3 452 | 2 | 3 455 | - | 44 | x78 (etapami) |
| miod pitny | 198 | 14 | 211 | - | 84 | x2.5 |
| plotno | 300 | 170 | 470 | len 1 410 (dzis 807) | 255 | x1.8 |
| sukno | 220 | 104 | 325 | welna 471 (dzis 354) | 228 | x1.4 |
| skora | 134 | 182 | 316 | - | 228 | x1.4 |
| narzedzia | 144 | 26 | 170 | - | 110 | x1.5 |
| garnki | 317 | 0 | 317 | glina ok. 320 (jest 490) | 25 | x12.9 |
| swiatlo (tran, oliwa lampowa) | 118 | 0 | 118 | - | 6 | x21 |
| drewno (ladunek 100 kg) | 4 230 (opal miast) | 2 066 | 6 296 | - | 2 109 | x3.0 |
| futra / aksamit / jedwab / klejnoty | ok. 0-10 | - | sterowane pieniedzmi (lista dostatku P3) | - | 106 / 6 / 48 / 17 | bez celu w kg |

**Razem jedzenie i napoje: 51 000 szt./d (dzis 19 100) = x2.7; 510 t gry na dobe = 18.9 mln prawdziwych kg.** Etap A (zboze z przetworami, mieso, ryby, nabial, sol; piwo jak dzis)
= 39 800 szt./d wobec 16 800 dzis.

**7.3 Pieniadze koszyka z targu** [S]: po cenach bazowych 402 tys. zl/d (miasta 249 tys., wsie 153 tys.). Dzis lud miast placi 323 tys. zl/d, a wsie kupuja za 9.3 tys.
(chca 67 tys.). Po dzisiejszych cenach miast (indeksy 2-7, bo towaru brak) koszyk kosztowalby 1.11 mln zl/d - dlatego najpierw towar, potem popyt. Gdy towar dojdzie,
indeksy maja zejsc do ok. 1.0-1.5; sufit 10 zostaje.

---

## 8. Popyt -> zakupy -> ceny -> pieniadz (punkty wpiecia)

**8.1 Potrzeba koszyka** (raz na dobe w bloku TownFolk.Daily): dla kazdego rynku (miasto + wsie zamrozone przy targu, PeopleScale) i kategorii towaru
Q_c [szt./d] = suma po stanach i osadach (ludzie_stanu x kg_grupy x udzial_z_targu x podzial_na_kategorie) / sigma / kg_sztuki. Wynik do slownika dnia (odczyt O(1)).
Udzial z targu: miasta 100% (poza chlopami miejskimi); chlopi i rzemieslnicy wiejscy - zboze 5%, mieso 20%, ryby 25%, nabial 10%, owoce 10%, oliwa 50%, sol i przyprawy 100%,
miod 20%, piwo 10%, wino 10%, miod pitny 30%, plotno i sukno 50%, narzedzia 100%, garnki 80%; szlachta wiejska - zboze 30%, mieso 40%, ryby 80%, wino 70%, reszta 30-100%;
niewolni wiejscy 5% (karmi wlasciciel), sol 100%; wielkie rody 100% przez dwory. W miescie zboze to chleb 45%, placek 10%, ziarno 45%.

**8.2 Popyt u zrodla** [K: HistoricalPrices.DemandPostfix - karmi budzet dnia UpdateDemandShift i wzor ceny GetEstimatedDemandForCategory]: dla kategorii z koszyka
popyt = Q_c x wartosc sztuki; przejscie lambda 0 -> 1 przez 28 dob (stary zapis bez skoku cen). Kategorie zbytkow zostaja przy dobrobycie i liscie dostatku P3.

**8.3 Kotwica budzetu** [K: TownFolk.AnchorBudgetPostfix, galaz "kotwica w gore" od GL-2]: budzet kategorii = min(potrzeba stanow x cena, co kiesa stanu moze zaplacic).

**8.4 Drabina zakupow** (uogolnione FoodSubstitute [K]): kiesa stanu wydaje po kolei, kazdy szczebel najtanszym zrodlem za 1000 kcal:
1. przetrwanie: kcal z najtanszego z ziarna / chleba / placka;
2. sol, minimum opalu i odziezy;
3. napoj: piwo z warzelni albo ziarno na piwo domowe (0.5 kg ziarna na litr - receptura warzelni gry 1 -> 2 [K: spworkshops.xml]); na poludniu wino, gdy brak - piwo, potem ziarno;
4. bialko: najtansze za kcal z miesa, ryb, sera (w dni brakow mieso -> ryby -> ser -> jaja);
5. owoce, oliwa, wino ponad minimum;
6. lepsze sukno, futra, klejnoty (lista dostatku P3).
Gdy budzet sie konczy, zakupy staja. Czego nie kupiono: tylko brak na szczeblu 1 uruchamia glod miast (C2 HungerDays, chleb biedoty M3) i zdanie o chlebie w menu;
braki 2-6 tylko obnizaja Dostatek.

**8.5 Zakupy** - bez nowych drog: ItemConsumption i BK MakeConsumption w miastach, BuyGoods wsi (W1: wozy wsi przywoza koszyk), dwory (CourtGoods) dla szlachty i rodow.
Koszyk wsi (dzis BV 0.07 zl na glowe: sol, narzedzia, len, garnki, piwo) zastepuje czesc rynkowa koszyka chlopow. Podwojnego liczenia nie ma: koszyk stanow zastepuje
koszyki BW/BR/BP/BS/BC/BV kiesy ludu (jeden klucz), konsumpcja gry tylko wykonuje zakup.

**8.6 Ceny** - wzor gry na cenach bazowych HistoricalPrices; dzis indeksy zawyzone brakiem (zboze 2.29, sol 7.58, jaja 6.47, miod 7.67); po dojsciu towaru ok. 1.0-1.5.
Cena sprzedazy wozu wsi ma isc za indeksem miasta docelowego (dzis sol 0.7 x wartosci przy 7.6 x w miastach) - ZC1.

**8.7 Droga pieniadza (zamknieta, zero nowego zlota)**:
1. kiesy stanow (W/R/P/S miasta, podzamcze, wies, majatki BK, dwory) placa za koszyk kasie kupcow miasta;
2. kasa kupuje od wozow wsi: 65% utargu do kiesy wsi (VillShare), majatki BK (EstateIncome187), dziesiecina (E5/C3), danina i renta panu jak dzis;
3. kasa kupuje od warsztatow (chleb, piwo, plotno, garnki): 35% place dla R (CraftShare), zysk notabli;
4. kiesa wsi kupuje swoj koszyk w miescie targowym - pieniadz wraca do kasy;
5. robocizna handlu (RhFood 20%, RhMid 10%) rosnie z obrotem i trafia do W i R - to jest ten dochod "od ludzi", ktory rozmywa dzisiejsza przewage zoldu (rozdz. 2.4);
6. zold wraca przez zakupy wojska; panowie i korony biora renty, cla i myto jak dzis;
7. awans: oplata cechowa i kupno ziemi ida do odbiorcy (kiesa R, pan ziemi) - zamkniete, linia MoneyLedger.
Dzis ok. 90-105 tys. zl/d "nie ma na co wydac" [L audyt] - pelny koszyk jest ujsciem.

**8.8 Plan targu (jak produkcja dochodzi do celu).** Regulator wsi HarvestValve nie rusza jedzenia [K: Settings HarvestValve "food is not touched"], wiec dla jedzenia
regulatorem jest plan targu ZC:
- co 28 dob, na kraine (kultura regionu) i towar: cel = potrzeba z targu rynkow tej krainy (Q_c) + wojsko, zalogi, dwory, warsztaty i budowy tej krainy (srednia 28 dob)
  + wsad przetworow; czynnik = cel / dzisiejsza produkcja targowa krainy;
- krok najwyzej x1.25 w gore i x0.8 w dol na 28 dob, granice czynnika 0.5-4; dostrajanie dniami zapasu (zboze cel 30 dni, swieze 7 dni);
- czynnik siedzi w MaterialLaw.ProdPostfix po kroku ludzi P1 (u_v), przed rekami spustoszenia [K: GoodsRates.ProdStep - wzor soli i futer]; zima, najazd i rece dzialaja dalej;
- rzemioslo miasta (TownCrafts) robi przetwory "wedle wartosci" z bramka zysku - rosnie tylko tam, gdzie jest popyt; rece = ludzie stanu rzemieslnikow rynku / sigma / norma;
- Essos (30-38% ludzi w miastach) dostaje wyzsze czynniki zboza niz Westeros, reszte dowoza karawany i statki (ShipsToPort) z Reach i Dorzecza.

---

## 9. Wskaznik zamoznosci: "Dostatek" (EN "Livelihood")

**Nazwa.** PL "Dostatek", EN "Livelihood". Nie "dobrobyt" (to statystyka gry Prosperity), nie "PKB". Pokazujemy jako krotnosc: "Livelihood 1.8x". Druga liczba obok:
**"Dochod na glowe"** (EN "Income per head") w zl na rok w skali prawdziwej. Historycy nazywaja to welfare ratio (R. Allen) [H].

**Wzor** (na stan e w osadzie s, srednia 28 dob):

D(e,s) = Y(e,s) / (L(e,s) x K_min(s))

- **Y** - dochod netto stanu na dobe w prawdziwych zl:
  - gotowka: wplywy do kiesy stanu x sigma (lud miast - zarobki z TownFolk EarnWin/EarnK; wies - doplyw do kiesy wsi; szlachta - dochod majatkow BK; podzamcze - jego kiesa);
  - plon zjedzony z wlasnego pola (chlopi, szlachta z majatku, niewolni wiejscy): zjedzone racje x stol w kg x cena u bramy (0.6 x cena targu) - bez czesci sprzedanej, ktora jest juz w gotowce;
  - minus renta, dziesiecina, podatek pana, podatek wojenny (x sigma);
  - niewolni: racja od wlasciciela - D ok. 1 z definicji;
  - wielkie rody: dochod rodu 1:1 na czlonka (sa w grze 1:1).
- **L** - ludzie stanu w osadzie (ksiega).
- **K_min** - koszyk przetrwania po cenach LOKALNYCH: miasto - wartosc x indeks targu; wies - cena u bramy miasta targowego (0.6 x). Bazowo 0.266 zl/d (miasto), 0.226 (wies).
- Osada = srednia stanow wazona ludzmi; **region** (warownia + jej wsie), krolestwo i swiat - tak samo. Obok sredniej pokazujemy mediane chlopow (bogaci ciagna srednia).
- "Dochod na glowe" = Y / L x 364.

**Skala slowna** (dopasowana do kosztu zwyklych koszykow z rozdz. 6):

| D | PL | EN | kto zwykle |
|---|---|---|---|
| < 0.8 | glod | Starving | glod i uchodzcy (Famine) |
| 0.8-1.2 | nedza | Destitute | tylko przetrwanie |
| 1.2-2.0 | skromnie | Modest | chlop po rencie, biedota |
| 2.0-4.0 | dostatnio | Comfortable | zwykly chlop przy pelnym plonie (2.3), rzemieslnik (3.1) |
| 4.0-10 | zamoznie | Well-off | kupcy (7.0) |
| > 10 | bogato | Rich | szlachta (10.7), wielkie rody (20) |

Kotwice historyczne [H]: przetrwanie 1.0; polnoc Anglii 1.5, poludnie 2, Londyn 3 (1700-60); Anglia 1300 - PKB 240 d/glowe przy koszyku 82-97 d = 2.5-2.9.
Nasz swiat przy zwyklych koszykach: zwykly koszyk 246 zl/rok, przetrwanie 85 zl/rok, **D = 2.9** [S].

**Spodziewany start** [S, do sprawdzenia w ZC0]:
- miasta: dochod ludu z zewnatrz 288 tys. zl/d x 37 / 10.43 mln = 1.02 zl/os/d = 372 zl/rok -> D ok. 3.8 srednio, ale rozrzut krain 92x (rozdz. 2.4);
- wsie: plon 1.18 racji/os x stol chlopa u bramy + gotowka wsi (60-150 tys. zl/d) - renta zaplacona (0.12 zl/os/d) -> D chlopow ok. 1.7-2.2;
- szlachta 6-12, kupcy 4-9, rzemieslnicy 2.5-4, biedota 1.5-2.5.

**Co zmienia Dostatek:** ceny (wiecej towaru = nizszy indeks = tanszy koszyk); dochody (obrot targu, place rzemiosla, zold w miescie, dwory, budowy, utarg wozow);
plon (zima WinterBite, spustoszenie, rece, stan wsi); pobory (renta, dziesiecina, podatek wojenny, danina P1, myto); liczba ludzi (wiecej ludzi przy tym samym dochodzie = nizszy D).

**Skutki Dostatku:** koszyk sie wzbogaca (drabina: przetrwanie -> zwykly -> lista dostatku), wiec popyt na mieso, wino, sukno i klejnoty rosnie tam, gdzie ludzie sa zamozni;
awans (rozdz. 10); dobrobyt gry idzie powoli za Dostatkiem regionu: P* = mediana dobrobytu krainy x (D_region / D_mediana_krainy)^0.7, ruch najwyzej 1% luki i 2 pkt na dobe
(ZC6). Za dobrobytem ida jego skutki w grze: milicja, Food warowni (dobrobyt/40), popyt na zbytki, BK, wartosc osady dla AI. Start neutralny - kotwica to dzisiejsza mediana.

---

## 10. Awans i spadek (od Dostatku i oszczednosci)

Liczone raz na tydzien na osade (co dobe 1/7 osad), w grupach z licznikiem ulamkow, nie pojedynczo.

**W gore** e -> e+1: przeplyw na rok = r_up x ludzie_e x clamp((D_e - U) / U, 0, 1) x min(1, zapas / zapas_potrzebny) x miejsce;
r_up = 3% na rok (historia: ok. 1% luki na rok, trwalosc statusu 0.7-0.8 na pokolenie [H: Clark]; w grze x3, w dobrych latach do 6%).

| przejscie | U (Dostatek stanu) | zapas kiesy | miejsce (sufit) | pieniadze |
|---|---|---|---|---|
| chlop -> rzemieslnik wiejski | 2.5 | kiesa wsi >= 60 dni koszyka | rzemieslnicy wsi <= 6% | udzial kiesy idzie z ludzmi |
| chlop -> biedota miasta targowego | D biedoty > D chlopa x 1.2, miasto nie gloduje | - | mieszkania miasta (E4/4) | istniejaca migracja wies -> miasto (E4/4), tylko etykieta |
| biedota -> rzemieslnik | 2.5 | W >= 30 dni | rzemieslnicy <= obrot rzemiosla / obrot na rzemieslnika, najwyzej 1.5 x pasma | oplata cechowa 30 dni koszyka R z W do R |
| rzemieslnik -> kupiec | 4.0 | R >= 60 dni | kupcy <= obrot targu / obrot na kupca, najwyzej 1.5 x pasma | udzial kiesy R -> P |
| kupiec (i zamozny chlop) -> szlachta | 10 | P >= 180 dni | szlachta <= 3% krainy | kupno ziemi: rok koszyka szlachty na glowe do pana ziemi (rod / korona) |
| szlachta -> wielki rod | - | - | - | tylko gra: nadanie lenna, nowy rod |
| niewolni -> wolni | 0.5% na rok (wyzwolenia) | - | - | do biedoty / chlopow |

**Wyzwolenie przez zdobywce** (ZC6, klucz DailyLifeFreedom): warownia kultury z niewola pod panem krolestwa, ktore niewoli nie zna (prawa BK albo kultura wladcy spoza listy),
uwalnia 10% niewolnych na miesiac do biedoty i chlopow (lore: Daenerys w Zatoce).

**W dol** e -> e-1, gdy D_e < L przez 28 dob i zapas < 14 dni: r_down = 6% na rok x clamp((L - D_e) / L, 0, 1).
Progi L: rzemieslnik 1.3 (-> biedota), kupiec 2.5 (-> rzemieslnik), szlachta 4.0 (-> kupiec w miescie / chlop na wsi, "sprzedane majatki").
Biedota i chlopi ponizej 0.8 - dzialaja istniejace reguly glodu i uchodzcow. Opcja (wylaczona): niewola za dlugi w kulturach z niewola, 1% na rok przy D < 0.8 bez zapasu.

**Granice:** udzial stanu w [0.5 x dol pasma, 1.5 x gora pasma]; w osadzie najwyzej 0.2% ludzi na tydzien; suma ludzi bez zmian (to przesuniecie etykiet).
Straty wojenne dziela sie proporcjonalnie, ale najazd bije najpierw chlopow w polu, a pobor bierze chlopow i biedote.
Przyklad [S]: zamozne miasto przez 3 lata gry - kupcy 6% -> ok. 7%, rzemieslnicy 30% -> ok. 33%; po zlupieniu w druga strone ok. 2 razy szybciej.

---

## 11. Wojna niszczy dostatek

Wiekszosc dziala juz dzis i obniza Y albo podnosi K_min sama:

| zdarzenie | co juz jest [K] | co dochodzi (ZC7) | skutek dla Dostatku |
|---|---|---|---|
| najazd na wies (Devastation, DistrictRaid, ScorchedEarth) | zabici i uchodzcy, spichlerz spalony tym samym ulamkiem, udzial s kiesy wsi do napastnika, plon x udzial ludzi w domu | blizna: rzemieslnicy wsi -20% f do chlopow; majatki BK bez utargu (D szlachty w dol) | D wsi -30-60% (historia: najazd scina zamoznosc regionu o 30-60% [H]) |
| oblezenie | Food warowni, uchodzcy w murach, glod przy Food 0 (TownDie) | - | dowoz odciety -> indeks w gore -> K_min w gore -> D wszystkich w dol; po 30 dobach D < 1 = glod (C2) |
| zlupienie miasta (CitySack.Coin, People) | kiesy W/R/P zabrane, jency w niewole | ruina: 10-20% kupcow -> rzemieslnicy, 10-20% rzemieslnikow -> biedota od razu; przez 30 dob spadki x4 | skok D w dol, miasto biednieje na lata |
| pobor (PeopleLevy) | ludzie z DOM | pobor z chlopow i biedoty | mniej rak -> mniejszy plon |
| podatek wojenny (WarS 3%) | jest | - | Y w dol |
| uchodzcy w obcym miescie | liczeni w regionie | w D biedoty gospodarza bez dochodu | D biedoty w dol (chleb biedoty M3) |
| powrot | spustoszenie mija, spichlerz sie napelnia, uchodzcy wracaja | sklad stanow wraca przez awans (lata) | blizna: mniej kupcow i rzemieslnikow; historia 10-25 lat po najazdach, 100+ po dlugiej wojnie [H], w grze szybciej |

Wojna moze tez wzbogacic miasta garnizonowe (zold wydany w miescie) - to historyczny "boom wojenny", ale dzis jest przesadzony (rozdz. 2.4, ryzyko 6).
Komunikat w menu: "Livelihood of the district fell 35% after the raid on X (12 days ago)".

---

## 12. Co widzi gracz (napisy po angielsku)

1. **Dymek wsi** (MapVillageTooltip, obok "People of this land"): "Peasants 88% | craftsmen 5% | gentry 1.5% | Livelihood 1.8x (Modest) | Daily table 0.9 kg: grain 58%, dairy 9%, fish 4%".
2. **Menu "People of this land"** (PeopleMenu, ShowInquiry [K]) - nowa sekcja "Estates and livelihood": wiersz na stan (ludzie, %, Livelihood + slowo + strzalka 28 dob,
   income per head, stol kg/d); linia regionu (warownia z wsiami); "Rose / fell in the last 28 days: 80 peasants became craftsmen, ..."; "Table of the district today: X t
   (own fields Y, market Z)"; linia wojny; jedno zdanie skali: "One sack on the market stands for about 37 sacks of the land's real trade."
3. **"Look over the district"** (DistrictMenu): sklad okregu dla wiosek pobocznych i glowny towar (razem z projektem Prace okregu).
4. **"Realm ledger"** (KingdomLedger, pozycja w menu miasta i zamku, ShowInquiry - bez SwitchToMenu): regiony krolestwa (ludzie, % stanow, Livelihood, income per head)
   i wiersz swiata.
5. **Dla Jeffa i testow**: linie logu (rozdz. 14) i CSV `stany-dostatek.csv` co 7 dob na osade. Kolor mapy wedlug D (MapVillagesView) - pozniej (ZC8).
Okna mowia "People of this land (book)", zeby nie mylic z liczba BK (3 mln) na ekranie BK.

---

## 13. Poprawki towarow (lista per towar)

| towar | problem dzis [L] | poprawka | paczka |
|---|---|---|---|
| zboze | 9 036/d, bilans -534/d, zapas 13 dni, indeks 2.29 | czynnik planu targu wedlug krainy ok. x2.9 (Essos wiecej, bo 30-38% w miastach); reszta przez karawany i statki | ZC3 |
| chleb, placek | 865 / 575 | pary rzemiosla miasta zboze -> chleb 1:1, zboze -> placek; drabina: brak chleba = ziarno | ZC3 |
| piwo | 452/d, zapas 3 dni | para zboze -> piwo 1:2 (warzelnia gry); brak = ziarno na piwo domowe | ZC3/ZC5 |
| receptury BK (chleb, piwo, mieso) | dodatkowe cykle biora juz wlasny wsad (ArtisanOwnInputs, linia 147: 143 zboza na 83 chleba + 124 piwa), ale z 1 worka wychodzi kilka sztuk | wydajnosc jak warsztaty gry (1 zboze -> 1 chleb, -> 2 piwa) | ZC1 |
| ryby | 2 355/d, -273/d | czynnik x2.0; wyspy wiecej wsi rybakow tylko w nowej kampanii | ZC3 |
| mieso wieloryba | 49% miesa, robione z uboju swin i krow w glebi ladu (kategoria "meat") | uboj daje "meat"; mieso wieloryba tylko od wielorybnikow | ZC1 |
| mieso, krowy, swinie, owce | uboj 95 / 245 / 204 szt./d | czynnik hodowli ok. x2.2 (AnimalsUnderValve stroi) | ZC3 |
| ser, maslo, jaja | 411 / 304 / 255, miasta placa 6.5 x za jaja | czynnik x2.5 / x1.7 / x1.8; drob (ges, kura) jako zrodlo jaj i miesa | ZC3 |
| jablka, marchew, pomarancze, daktyle | 76-350/d | D1 (plon za cena) z celem x2.4-3.4 | ZC5 |
| winogrona, wino | 390 / 44 szt./d | wino x78 w 2 etapach; winogrona x13 (D1 + winnice); poludnie pije wino, brak = piwo | ZC5 |
| oliwki, oliwa | 287 / 32 | para oliwki -> oliwa 5:1; oliwa 4 d/kg za droga (historia ok. 2 d/kg) | ZC5 |
| sol | 98% do wsi, wozy oddaja po 0.7 x, miasta placa 7.6 x | cena wozu za indeksem miasta docelowego | ZC1 |
| miod, miod pitny | miod pitny tylko od drwali | miod pitny z pasiek (para miod -> miod pitny) | ZC1 |
| garum | popyt jest, producenta nie ma | para ryby + sol -> garum w Lys i Volantis albo zdjac z popytu | ZC1 |
| len, plotno | 807 / 255 | czynnik lnu x1.75 (cel 1 410) | ZC3 |
| welna, sukno | 354 / 228 | czynnik welny x1.35 | ZC3 |
| jedwab surowy | mieszczanie jedza go wprost (42.7 z 46.5/d) | zdjac "cotton" z listy dostatku ludu, skierowac do tkalni aksamitu | ZC1 |
| aksamit | indeks na suficie 10 codziennie | po poprawce jedwabiu sam spadnie; cena dworu (1 210 d) jako kotwica | ZC1 |
| futra | +34/d, zapas rosnie | zostaje przy pieniadzach (P3); regulator obetnie | - |
| klejnoty | zapas x41 w rok; 10 kg za 2 400 d | waga 1 kg, wartosc 240 d za sztuke (ta sama cena za kg) - tylko nowa kampania | ZC1 |
| drewno, wegiel | wegiel bez producenta, 0.5 kg za 1 d (zaokraglenie x2.9) | wypal w wsiach drwali (6-7 kg drewna na 1 kg wegla), ladunek 100 kg (BulkUnit) | ZC1 |
| glina, wapien | zaokraglenie do pensa: x2 i x5 ceny | ladunek 100 kg (BulkUnit) | ZC1 (nowa kampania) |
| deski | budowy ich nie biora | do BuildFunding albo zdjac | ZC1 |
| sztabki zelaza | nikt nie robi ani nie zuzywa | zdjac z polek (zostaja u gracza) albo krok lupa -> sztabka | ZC1 |
| lupa zelaza | wozy po 0.57 x, 38 miast bez lupy; 15 kg za ladunek 100 kg | cena wozu za indeksem; waga 100 kg (nowa kampania) | ZC1 |
| ruda srebra | mieszczanie jedza 116/d | do zlotnika i mennicy | ZC1 |
| papirus, marmur, ruda zlota | papirus z farm pszenicy Westeros; marmur i zloto z kopaln srebra | papirus tylko Essos nad Rhoyne; marmur z kamieniolomow; zloto z kopalni (Westerlands) | ZC1 |
| purpura, atrament, kamienie, sztabka zlota, wol, ksiegi | nikt nie robi | purpura Tyrosh, atrament z sadzy, ksiegi skryptoria Oldtown - albo zdjac z popytu | ZC1 |
| garnki | 25/d | para glina -> garnki (cel 317) | ZC5 |
| swiatlo | 6/d | tran i oliwa lampowa (brak loju w grze) | ZC5 |
| konie | brak w ksiedze towarow | dopisac do GoodsLedger jako kapital | ZC1 |
| zrodla i ujscia bez nazwy | +34 kazdego jedzenia w miastach z "inne ticki dobowe", -17 w RemoveParty | linia K4 wskazuje BannerKings:BKPartyBehavior (osady, np. zboze +53/d); RemoveParty = konwoje DTE (CutTheirSupplyBehavior.CleanupCaravan) - wytropic i zamknac | ZC1 |
| dane | 2 wsie New Ibbish (Guntak, Volug; 54 tys. ludzi) bez regionu | sprawdzic OutlawLaw.RegionFor / BoundVillages | ZC0 |

Zgodnosc z projektem "Prace okregu" (docs/PROJEKT-INWESTYCJE-OKREGU-2026-10-16.md): plan targu ZC mowi, ILE towaru potrzebuje kraina; zajecia wiosek pobocznych mowia,
KTO go robi. Czynnik ZC dziala na stawke wsi, nie zmienia zajec - regula Jeffa "zmiana produkcji nie od tak" (+25% przez ok. 60 dni, okres przejsciowy) zostaje nietknieta.

---

## 14. Plan wdrozenia - paczki, pliki, klucze, linie logu, testy

Kazda paczka: osobna galaz, autor + recenzja, autotest 120 dob na zapisie F wobec bazy (WGRANIE 72, ta sama dlugosc), kopia przed wgraniem, wgranie dopiero po udanym
tescie i nigdy, gdy Jeff gra. Kazda czesc ma klucz MCM (domyslnie wedlug paczki); po dodaniu ustawien `python tools/gen_mcm.py`. Napisy w grze po angielsku.

**Progi wspolne kazdego testu 120 dob:** 0 ERROR i 0 potkniec ZC; czas bloku ZC <= 5 ms na dobe (mediana), cala doba <= +10% bazy; obieg zamkniety - kontrola MoneyLedger
0 roznicy, suma pieniadza swiata zmienia sie tylko o znane pompy (jak baza +-0.5%); lud 8-10% pieniadza swiata, korony 19-22% (baza ok. 20%); partie lordow w wojnie
>= 85 tys. ludzi; zatkane wsie <= 5%; zapis wczytany ponownie daje te same liczby ZC.

| paczka | co | pliki (Armoury/src) | klucze MCM (domyslnie) | linie logu / CSV | progi testu 120 dob (poza wspolnymi) |
|---|---|---|---|---|---|
| **ZC0** pomiar na sucho | stany z pasm, koszyki kg, Q_c, Spizarnia, Dostatek na stan/osade/region; gra bez zmian; poprawka New Ibbish | nowe DailyLife.cs, DailyLife.Estates.cs, DailyLife.Basket.cs, DailyLife.Livelihood.cs; GoodsLedger.cs (linia Spizarni) | DailyLifeMeasure (tak), DailyLifeMarketScale = 37 (zapisywane przy 1. wczytaniu) | "Zycie (ZC0): dzien N - stany, Dostatek swiat/stany, czas ms"; "Spizarnia (ZC): z pola / z targu wedlug grup, brak kg"; stany-dostatek.csv co 7 dob | linie 120/120; suma stanow = ksiega +-0; udzialy swiata +-1 pkt od rozdz. 5.3; Spizarnia z pola + z targu = potrzeba +-5%; D policzone dla 798/798; ksiega towarow +-2% bazy (nic nie zmienione) |
| **ZC1** drogi towarow | lista z rozdz. 13 (oznaczone ZC1), kazda poprawka osobno | GoodsRates.cs, ArtisanInputs.cs, MarketCarts.cs (sprzedaz wozu wsi), BuildFunding.cs, TownFolk.Luxury.cs (jedwab), BulkUnit.cs (nowa kampania), GoodsLedger.cs (konie, autorzy) | WhaleMeatFromWhalersOnly, SilkToWeavers, PlanksToBuilding, CharcoalBurners, IronIngotsOffShelf, BkSideGoodsOrigin, CartPriceByDestination, BkRecipeYieldByWorkshop (tak); BulkClayLimeCharcoal (tylko nowa kampania) | "Towary (ZC1): ..." na poprawke; K4 autorzy tickow | mieso wieloryba <= 10% miesa; indeks aksamitu < 10 w >= 50% miast; sol: cena wozu / cena miasta 0.6-1.2; zrodla "bez nazwy" w jedzeniu <= 2 szt./d na towar |
| **ZC2** stany w ksiedze + widok | stany w zapisie, zasiew z pasm x BK, widok (dymek, menu, Realm ledger); bez skutku w ekonomii | DailyLife.Estates.cs, DailyLife.View.cs, PeopleMenu.cs, MapVillageTooltip.cs, DistrictMenu.cs, KingdomLedger.cs; SaveText (klucz arm_estates, paczki 8 000 znakow) | DailyLifeEstates (tak) | "Stany (ZC2): zasiano N osad, zapis X KB" | zapis + wczytanie: stany identyczne; stary zapis zasiany 798/798; zapis +<= 60 KB; menu bez SwitchToMenu |
| **ZC3** produkcja etap A | plan targu wedlug krainy (zboze, ryby, mieso, nabial, len, welna), pary miasta zboze -> chleb / placek / piwo, ziarno na piwo domowe w drabinie | GoodsRates.cs (czynnik planu po kroku ludzi P1 w MaterialLaw.ProdPostfix, przed rekami i HarvestValve), TownCrafts.cs (pary), FoodSubstitute.cs | DailyLifeGoodsPlanA (tak), DailyLifeFoodCrafts (tak) | "Plan targu (ZC3): czynniki wedlug krain, cel / produkcja" | zboze >= 2.5 x bazy w 120. dobie; mediana indeksu zboza <= 1.6 (baza 2.29); zapas zboza 20-60 dni; ryby >= 1.8 x, mieso >= 2 x, ser >= 2 x bazy; miasta Essos z zapasem zboza < 7 dni <= 10%; wozy w miescie > 7 dob nie wiecej niz w bazie; ruch ksiegi towarow <= 70 tys. szt./d |
| **ZC4** popyt od koszyka | Q_c w DemandPostfix (lambda 28 dob), kotwica = min(potrzeba x cena, kiesa), drabina zakupow, koszyk wsi ze stanow; zastepuje BW/BR/BP/BS/BC/BV | HistoricalPrices.cs, TownFolk.Live.cs, TownFolk.cs, FoodSubstitute.cs, DailyLife.Basket.cs | DailyLifeBasketDemand (tak po tescie) | "Koszyk stanow (ZC4): popyt, kupione, nie kupione wedlug szczebla, lambda" | "stac na jedzenie" W/R/P >= 95% w miastach nieoblezonych; wsie kupuja >= 50% chcianego (baza 13.8%); "nie do wydania" <= 50 tys. zl/d (baza 90-105 tys.); wydatki/dochod ludu 0.9-1.1 (28 dob); zgony z glodu poza oblezeniami i najazdami 0; indeksy: sol <= 2.5, jaja <= 2.5, miod <= 3, mediana jedzenia 0.9-1.8 |
| **ZC5** produkcja etap B | napoje, owoce, oliwa, wino w 2 krokach, garnki, swiatlo, opal miast | GoodsRates.cs (D1 z celem), TownCrafts.cs (winogrona -> wino, oliwki -> oliwa, glina -> garnki, miod -> miod pitny) | DailyLifeGoodsPlanB (tak), DailyLifeWineStep (1 -> 2) | "Plan targu (ZC5): ..." | zapas piwa >= 7 dni; indeks wina w miastach poludnia <= 3; oliwa <= 2.5; garnki >= 200/d; opal miast: zapas drewna >= 10 dni |
| **ZC6** Dostatek czynny | awans i spadek, pieniadze z ludzmi, wyzwolenie, powiazanie z dobrobytem | DailyLife.Mobility.cs, DailyLife.Livelihood.cs, postfiks zmiany dobrobytu (model ROT/BK/gry - lancuch jak P4), MoneyLedger.cs (oplaty, kupno ziemi) | DailyLifeMobility (tak), DailyLifeFreedom (tak), DailyLifeProsperityLink (tak po tescie) | "Awans (ZC6): w gore / w dol wedlug przejsc, wyzwoleni, oplaty zl, ziemia zl" | mediany D: chlopi 1.3-2.5, biedota 1.4-2.5, rzemieslnicy 2.2-4.5, kupcy 4-10, szlachta 6-15; max/min median regionow <= 8 (dzis ok. 90); udzialy stanow w pasmach; ruch <= 0.2%/tydz. na osade, swiat netto w gore 0.5-3%/rok; srednia zmiana dobrobytu <= 0.5/d; mediany dobrobytu krain +-5% |
| **ZC7** wojna i blizna | ruina przy zlupieniu, blizna najazdu, komunikaty | CitySack.People.cs, TownFolk.Sack.cs, Devastation.cs / DistrictRaid.cs, DailyLife.View.cs | DailyLifeWarScar (tak) | "Blizna wojny (ZC7): ..." | po zlupieniu: kupcy -10-20% w miescie; po najezdzie D okregu -30-60% w 7 dob i >= 50% powrotu w rok bez wojny (na zdarzeniach z biegu) |
| ZC8 pozniej | duchowni jako stan, pora roku w koszykach, kolor mapy, czesc zoldu do kas notabli (skala zoldu), niewola za dlugi | - | - | - | osobno |

Kolejnosc: ZC0 -> ZC1 -> ZC2 -> ZC3 -> ZC4 -> ZC5 -> ZC6 -> ZC7. ZC1 i ZC2 moga isc rownolegle w kodzie, ale wgrywane po kolei (jedna rzecz naraz).
ZC4 dopiero, gdy ZC3 pokaze indeks zboza <= 1.6. ZC6 dopiero po 120 dobach ZC4 (dane D do strojenia progow).
Wydajnosc: ok. 798 osad x 7 stanow x 13 grup = 73 tys. dzialan na dobe w tablicach (bez LINQ i alokacji), popyt ze slownika O(1); awans co tydzien 1/7 osad.
Zapis: arm_estates (udzialy stanow w promilach, D 28 dob, liczniki awansu) ok. 50-60 KB przez SaveText.Sync (limit napisu 32 KB - paczki).

---

## 15. Ryzyka

1. **Transport x3.** Wozy i karawany powioza ok. 3 razy wiecej jedzenia (171 -> ok. 510 t gry na dobe). Lekarstwa: etapy (ZC3 przed ZC5), udzwig wozu za ladunkiem (122),
   statki (181), prog w tescie. Wariant zapasowy: ladunek 100 kg dla zboza na wozach wsi (waga ta sama, mniej sztuk) - tylko jesli test pokaze zatkane wsie.
2. **Kolejnosc.** Popyt przed towarem = ceny na suficie, glod, drogi chleb biedoty. Dlatego ZC4 tylko po progu ZC3 i z przejsciem lambda 28 dob.
3. **Moce warsztatow.** Chleb x7, piwo x25, garnki x13, oliwa x12, wino x78. Pary rzemiosla trzymaja receptury i bramke zysku, inaczej wraca "towar z niczego" albo zalew.
   Piwo domowe z ziarna i wino w 2 krokach zdejmuja wiekszosc ryzyka.
4. **Essos zalezy od dowozu.** 69% zboza z targu potrzebuje Essos; Qarth juz dzis sprowadza 3/4 jedzenia. Plan targu wedlug krainy podnosi wsie Essos; wojna na morzu da glod
   w miastach Essos - historyczne, ale ostre; prog testu "miasta Essos < 7 dni zapasu <= 10%".
5. **Dwie ksiegi jedzenia.** Spichlerz (racje w kg) i targ (sztuki) zostaja osobno; "46 mln kg" w 77% to wycena racji, nie towar na mapie. Ten sam plon q (zima, najazd) karmi oba,
   wiec ida razem; rozjechac moga sie tylko przy recznych stawkach.
6. **Zold 1:1 w kiesie 1:37.** 63% dochodu ludu miast to zold (rozdz. 2.4) - miasta garnizonowe beda mialy przesadzony Dostatek. Obrot targu od ludzi rozmyje przewage
   (szacunek: z 63% do ok. 50%), ale nie usunie. Region (warownia + wsie) jako glowna liczba lagodzi skutek; trwale wyjscie - czesc zoldu do kas notabli (karczmy) - ZC8,
   osobna decyzja po pomiarze ZC0.
7. **Kalibracja Dostatku.** Dochody stanow zaleza od stalych podzialu (LabW 0.9, RhFood 0.20, obrot P 3%, 65% utargu wsi). Najpierw ZC0 (D tylko w logu), progi awansu
   i skali dopiero po pomiarze.
8. **Niewolni w Essos** (5.65 mln, 23% Essos) - wedlug kanonu (Volantis "piec niewolnych na wolnego"). Ksiega ma 31 tys. konta NIEWOLNI; reszta to etykieta w DOM
   (pobor i renta ich nie biora). Wylacznik DailyLifeCanonSlaves.
9. **Dwie liczby ludzi.** BK dalej pokazuje 3 mln i swoje klasy. Okna mowia "(book)" i jednym zdaniem tlumacza skale.
10. **Petla Dostatek -> dobrobyt -> popyt** moze sie rozbujac. Granice: 1% luki i 2 pkt na dobe, srednia 28 dob, kotwica w medianie krainy, koszyk przetrwania bez wplywu dobrobytu.
11. **Awans prawie niewidoczny albo za szybki.** 3%/rok luki daje zmiany widoczne w 2-5 lat gry; sufity pasm i 0.2%/tydzien trzymaja przed skokiem.
12. **Wojsko 1:1.** Zolnierz je prawdziwe sztuki, cywil - probke; wojsko po zmianie to ok. 8% sztuk jedzenia na targu (z zalogami zamkow 14%), dzis ok. 20-25%.
    Znane, mniejsze niz dzis.
13. **Stary zapis i Ironman.** Bez cofniecia: kazda czesc z kluczem, stany zasiewane przy pierwszym wczytaniu, nowe typy wsi (rybacy na wyspach, winnice) i zmiany wag
    (glina, klejnoty, lupa) tylko w nowej kampanii; sigma w zapisie i nie ruszana w trakcie (jak BulkUnit).
14. **Koszyki to szacunek.** Kg na stan i kraine to rozpiska historii [H/S] - zwlaszcza kupcy, rody i Essos. Lore Westeros nie ma twardych dni postnych - ryby ida za geografia.
15. **Zbytki** (futra, jedwab, aksamit, klejnoty) nie maja celu w kg - zostaja przy pieniadzach i liscie dostatku; ich dziury naprawia ZC1, nie koszyk.

---

## 16. Co przyjalem bez pytania (z wylacznikiem)

Zgodnie z zasada "ekonomie projektuje Claude": sigma 37; 7 stanow (duchowni pozniej); pasma z rozdz. 5.2 (kanoniczna niewola tylko w kulturach z listy LosersFlee);
koszyki i profile z rozdz. 6; piwo i wino chlopa z domu (90%); progi awansu z rozdz. 10; nazwy "Dostatek" / "Livelihood" i "Dochod na glowe" / "Income per head";
kolejnosc paczek z rozdz. 14. Wszystko ma klucz w MCM i najpierw idzie przez pomiar na sucho (ZC0).

---

## 17. Pliki

- Ten projekt: `docs/PROJEKT-ZYCIE-CODZIENNE-2026-10-16.md`.
- Wejscie workflow (scratchpad `roz/zycie`): `towary.tsv` + `towary.md` (katalog 129 wierszy), `ludnosc.tsv` + `ludnosc.md` (1 118 wierszy: swiat, rodzaj, kultura, kraina, krolestwo,
  region, osada), `historia.md` (tabele A-I ze zrodlami), `model1/` (m1.py, koszyki, bilans, stany krain), `model2/` (model2.py, wynik).
- Moje przeliczenie: `roz/zycie/synteza/bilans_zc.py` (stany, potrzeba, cele towarow, pieniadze, koszyki, Dostatek startowy; wynik `bilans_zc.out.md`),
  `roz/zycie/synteza/katalog.py` (tabela rozdz. 1; wynik `katalog.out.md`). Uruchomienie: `python -I bilans_zc.py [sigma]`.
- Powiazane projekty: `docs/gospodarka-od-ludzi/PROJEKT-GOSPODARKA-OD-LUDZI-2026-10-13.md` (sigma, P1 u_v, m_t), `docs/kiesa-ludu/*` (kiesa ludu), `docs/PROJEKT-GLOD-2026-10-07.md`
  (spichlerz), `docs/PROJEKT-INWESTYCJE-OKREGU-2026-10-16.md` (zajecia wiosek pobocznych), `docs/AUDYT-EKONOMII-2026-10-16.md`.
