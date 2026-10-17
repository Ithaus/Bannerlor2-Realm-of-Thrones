# PROJEKT: KALIBRACJA STARTU (KS) - wersja po recenzji (2026-10-16)

> ZASADY JEFFA 16.10 (WIAZACE): (1) "system dynamiczny, ktory sam sie reguluje podaza i popytem jak prawdziwa ekonomia" - zadnych stalych mnoznikow
> jako celu; (2) "musimy zaczac rynkowo, ze generalnie jest mniej wiecej bilans wszystkiego, czegos moze byc mniej o 5%, czegos wiecej o 5% ... a dopiero potem
> ekonomia i wydarzenia w swiecie zaczynaja tym bujac"; (3) AI lordowie na razie nie inwestuja; (4) Ironman w toku - kalibracja raz, bezpiecznie, z logiem,
> z wylacznikiem przed startem; (5) NAJWAZNIEJSZE: "zrob tak, ze nie jakos sztucznie dodajesz ... obszar, ktory ma 100 000 ludzi produkuje np. 200 zboza, a obszar
> z 10 000 ludzmi na Zelaznych Wyspach tez produkuje 200, bo postanowiles wyrownac - to ma sie zgadzac z realnej produkcji i rak do pracy".
> Dopisek Jeffa do Prac okregu (wiazacy): zmiana zajecia wioski dopiero przy +25% oplacalnosci utrzymanej ok. 60 dni, z kosztem przebudowy i okresem przejsciowym,
> najwyzej 1 wioska na okreg naraz, maly udzial regionu na sezon.

STATUS: PROJEKT. Nic w grze ani w repo nie zmienione (poza tym plikiem). Skrot w logu: **KS**. Tor pracy: **L** (pelny lancuch).
Poprzednia wersja (v1, ten sam dzien) przeszla recenzje: 5 uwag krytycznych, 12 waznych, 6 drobnych. Ta wersja poprawia wszystkie (tabela w rozdz. 1).
Model policzylem od nowa (v2): `scratchpad/roz/kalibracja/ks_model2.py`, `ks_tabele2.py`, `ks_raport2.py`, `ks_kiesy2.py` (sciezki w rozdz. 17).

Oznaczenia: **[K]** kod WGRANIE 72, **[L]** log i CSV Jeffa (16.10, doby 109018-109028), **[M]** moj model v2, **[H]** historia, **[S]** moje zalozenie.
Jednostki: "t/d" = prawdziwe tony na dobe; "szt." = sztuki gry na targu (sztuka = prawdziwe kg / sigma 37 / kg sztuki); "d" = zl (denary) gry.

---

## 0. Dla Jeffa - prostym jezykiem

1. **Kazdy okreg robi tyle, ile daja jego rece na jego ziemi.** Rece = ludzie razy udzial pracy ich stanu. Ziemia = cala powierzchnia okregu z mapy
   (klimat, teren, rzeka, brzeg i ile jest hektarow), a nie jeden punkt wsi. Jedna tabela wydajnosci dla calego swiata. Zadnej liczby "dla krainy".
2. **Dowod na Twojej kampanii** (prawdziwe kg na mieszkanca na dobe):

   | kraina | ludzie | zboze | ryby | inne | samowystarczalnosc jedzenia |
   |---|---|---|---|---|---|
   | Reach | 7,98 mln | 0,75 | 0,10 | winogrona 0,23 | 1,18 - sprzedaje |
   | Volantis | 4,99 mln | 0,88 | 0,01 | winogrona 0,37 | 1,63 - spichlerz Essos |
   | Zelazne Wyspy | 0,47 mln | 0,16 | 0,72 | | 0,60 - kupuja ziarno morzem |
   | Dorne | 1,48 mln | 0,33 | 0,18 | oliwki 0,50 | 0,87 |
   | Qarth | 3,98 mln | 0,32 | 0,20 | oliwki 0,24 | 0,73 - kupuje |
   | Ziemie Korony | 0,59 mln (75% w miastach) | 0,28 | 0 | | 0,46 - Krolewska Przystan kupuje |

   Okreg Oakendale w Reach (143 tys. ludzi) daje 132 t zboza dziennie i sprzedaje na targ 132 worki gry. Vorfjall na Zelaznych Wyspach (14,6 tys. ludzi)
   daje 2,5 t zboza, kupuje 14 workow i sprzedaje sol (+23) oraz ryby (+10).
3. **Kto co robi, ludzie wybieraja sami**: rece ida tam, gdzie rodzina wiecej zarobi po zaplacie za stado, lodz albo gaj, i jesli starcza ziemi
   (owczarz z 200 owcami potrzebuje ok. 120 ha, rolnik ok. 4 ha). Ta sama regula dziala potem w grze, z Twoim hamulcem: zmiana tylko przy +25% przez ok. 60 dni.
4. **Bilans startu**: na targu Westeros i Essos kazdy towar, ktory robia osobne rece (zboze, ryby, mieso, ser, maslo, wino, oliwki, welna, len, konie), miesci sie
   w 0,97-1,03. Poza pasmem tylko to, co rosnie przy okazji: marchew w Westeros x1,36 i jablka w Essos x1,07 (przy kazdym gospodarstwie), futra Polnocy x1,2,
   miod z uli przy chatach (0,91 / 1,12). Wsie Westeros maja ok. 6% rak wiecej, niz trzeba - ta nadwyzka zostaje na ich stole (wiecej chleba, piwa, miesa)
   i podnosi ich Dostatek. Nic nie jest "uciete".
5. **Wazne odkrycie: ceny gry nie zgadzaja sie z praca.** Przy cenach bazowych rolnik zarabia 1,06 d dziennie, rybak 3,15, oliwiarz 3,65, kopacz gliny 80.
   Ceny gry to ceny Anglii z 1300 r., gdzie oliwki, wino i ryby byly towarem z daleka. Rynek sam to wyprostuje: zboze bedzie drozsze, oliwki, ryby i welna
   tansze. Start robi tylko czesc tej drogi (zboze 1,5, oliwki 0,7), reszte zrobi rynek powoli, pod Twoim hamulcem zmiany zajec.
6. **Zloza, las na sprzedaz, oazy i ogrody przypraw naleza do pana** (tak bylo naprawde: prawo gornicze i lesne krola). Kopalnia zatrudnia tyle ludzi,
   ile targ kupi: gdy cena rosnie, przyjmuje, gdy spada, zwalnia.
7. **Handel jest konieczny.** Bez wozow i statkow miedzy krainami swiatu brakuje ok. 10% jedzenia, a Ziemie Korony, Zelazne Wyspy i Skagos maja polowe
   albo mniej. KS w Planie sprawdzi, kto naprawde wozi. Kraina bez przewoznika liczy sie sama, a spichlerz kazdej wsi ma podloge: przez KS nikt nie zacznie glodowac.
8. **Twoja kampania ma teraz koniec lata** (ok. doby 109030), potem jesien 300-450 dni, potem zime 3-5 lat. KS nie "wygladza" zimy: zima to wydarzenie swiata.
   Najlepiej wlaczyc KS w jesieni.
9. **Zadna moneta nie powstaje.** Kies ludu KS nie rusza (krok "wyrownania" przenioslby pieniadze od biedoty do kupcow). Zapasy rosna tylko z prawdziwej nadwyzki.
10. **Ironman**: najpierw tydzien "Plan" - tylko liczy i pisze log. Wlaczenie uzbraja sie w samym zapisie (inny zapis z tym samym kluczem nic nie zrobi).
    Wylaczyc mozna zawsze: produkcja i popyt wracaja do dzisiejszych nastepnej doby.

---

## 1. Co zmienilem po recenzji (23 uwagi -> poprawka)

| # | waga | uwaga recenzji (skrot) | co jest teraz w projekcie | rozdz. |
|---|---|---|---|---|
| 1 | krytyczne | rownowage trzymaly ceny cienia zamrozone jako referencje A7; przy cenach 1 wybor nie widzial dochodu | jedna regula na start i na gre: dochod rodziny na pracujacego przy cenie targu (kg x ziemia x cena) minus koszt stada, lodzi, gaju (K) minus renta ziemi; zadnych referencji; start = rownowaga tej reguly; ceny startu = ceny rownowagi w pasmie 0,7-1,5 (dniami polki); niezgodnosc cen gry z praca pokazana (rozdz. 3.4); P0 poprawione tylko w widelkach | 3 |
| 2 | krytyczne | zapas ze spichlerza racji = towar z niczego (1,81 mln szt.) | zadnej zamiany racji na towar; polka rosnie tylko z prawdziwej nadwyzki (wyprzedzenie podazy, wyliczone towar po towarze); spichlerz zostaje racjami | 6 |
| 3 | krytyczne | cele 30-90 dni przecza wzorowi ceny | polka = cena (3,6-12,7 dnia wedlug ceny startu; towar psujacy sie najwyzej 7 dni); rezerwa = tylko spichlerz okregu (racje, poza cena) i Food warowni; zadnej rezerwy towaru | 6 |
| 4 | krytyczne | krainy z brakiem moga glodowac; przewozu nikt nie mierzyl; ceny splaszczone | gradient ceny startu (kraina z brakiem = cena zrodla + droga); w Planie pomiar przewoznikow na kazdym korytarzu; kraina bez przewoznika liczy sie sama (plan zapasowy "kraina"); podloga Famine; bramka krainy; zboze wlasne bez progu | 5, 8 |
| 5 | krytyczne | klucz Apply globalny w MCM - ruszy na kazdym zapisie | uzbrojenie siedzi w zapisie (arm_ks: id kampanii, skrot planu, dzien D, potwierdzenie); klucz MCM uzbraja tylko wczytany zapis i sam wraca na Off; inny zapis = linia w logu i nic; autotest ma osobny klucz | 12 |
| 6 | wazne | Undo krotkie i niepelne | Undo produkcji i popytu bez terminu (KS i ZC4 jednym kluczem w obie strony); kies KS nie rusza; racji nie zamienia; lista nieodwracalnego prostymi slowami przed Apply; wersja w arm_ks; ostrzezenie w CHANGELOG | 12 |
| 7 | wazne | ciecia 1/1,08 i 1,05 x popytu = mnoznik plonu | ciecia usuniete; podloga ceny cienia 0,05 (regula sama zdejmuje rece z towaru bez zbytu); daktyle, oliwki, pomarancze w "lepszym stole"; wspolna nadwyzka rak jawnie na stole wsi (koszyk wsi w gore) | 3.3 |
| 8 | wazne | wariant zalecany nie trzymal +-5%; konie poza regula; Waskie Morze | z polami mieszczan i ziemia z mapy oba rynki trzymaja +-5%; premia mapy 1,25 i 1,05 daja ten sam bilans, 1,25 zmienia mniej wiosek - zostaje; stadniny w tej samej regule (popyt z HorseCensus); przewoz przez Waskie Morze mierzony w ZC0, wariant "swiat" policzony | 4 |
| 9 | wazne | zajecia lekkie z celem = popyt | zloza, las na sprzedaz, oazy, ogrody przypraw = regale: wolumen = rece x P0, rece ida za cena towaru, sufit = polowa rak bloku ze zlozem; start = rownowaga przy cenie 1; uzasadnienie w tabeli 3.4 | 3.2 |
| 10 | wazne | ubytek rak liczony dwa razy | stawka KS od wszystkich ludzi okregu (dom + jego konta uchodzcow) x LAMBDA; czynniki E4/5 i E4/7 zostaja i dzialaja raz; test na probce wsi z uchodzcami | 11 |
| 11 | wazne | brak kodu, na ktorym projekt stoi; produkcja BK poza typem | twarde bramki przed KS-A; jedna funkcja KS dla wszystkich towarow wsi z tym samym lancuchem czynnikow i nazwanym zrodlem w GoodsLedger; produkcja BK poza typem zdjeta i zastapiona przez KS | 10, 11 |
| 12 | wazne | nie sprawdzono przeplywu pieniedzy | Plan liczy na rynek i dobe: dochod ludu wobec koszyka po cenach startu, kasy kupcow wobec zakupow; odbior: kg koszyka niekupione z braku pieniedzy <= 5% | 9 |
| 13 | wazne | dwa sprzeczne wyniki kies | KS nie przesuwa kies (decyzja, uzasadnienie w rozdz. 9); jeden skrypt ks_kiesy2.py do wgladu | 9 |
| 14 | wazne | pory roku przekreslaja okno odbioru | poprawione fakty: pory roku w grze sa wieloletnie; bez normalizacji zimy; odbior wobec oczekiwanej podazy tej pory; D najlepiej w jesieni | 7 |
| 15 | wazne | od D do A7 nic nie reguluje | A7 (regula dochodu z hamulcem Jeffa) dziala od D; regale od D; HarvestValve = 1 dla towarow KS; odbior 7 mierzy nacisk i prawdziwe zmiany | 11 |
| 16 | wazne | ziemia wsi glownej z jednego punktu, progi | ziemia z calego okregu (okregi.npz), 19 blokow wedlug mieszanki + punkt wsi; dolina rzeki w goracu = pole; zboze wlasne ciagle (L - 0,2) / 0,4; powierzchnia w ha | 2.2 |
| 17 | wazne | nie sprawdzono, czy jest czym przewiezc | Plan: na kazdym korytarzu potrzebne sztuki wobec zmierzonych przewoznikow; brak = kraina docelowa liczy sie sama; predkosc i udzwig z ZC0 | 5 |
| 18 | drobne | usterki solvera | regale poza tatonnement i numeraire (ich ceny wracaja do 1); test zbieznosci 2%; towary niezbiezne w raporcie; Plan co 7 dob z cieplego startu | 11 |
| 19 | drobne | bloki wedlug indeksu | bloki wedlug stalego id (uid wioski, id wsi#mainK, id miasta#polaK) + skrot plikow ukladu i ziemi; niezgodnosc = liczyc od nowa | 12 |
| 20 | drobne | rampa sie nie spina | wyprzedzenie liczone towar po towarze; towar psujacy sie najwyzej 7 dni polki | 6 |
| 21 | drobne | 1:1 x sigma zawyza jedzenie partii; dwory podwojnie | decyzja przed Planem: 1:1 x sigma zostaje, udzial pokazany; dwory = wielkie rody 1:1, nie ma ich w koszyku szlachty ksiegi - sprawdzenie w ZC0 | 4.7 |
| 22 | drobne | KS-K zmienia popyt pod podaz | KS-K uzasadnione historia, zapisane przed bilansem, decyzja w ZC0; policzone oba warianty | 4.6 |
| 23 | drobne | skutki uboczne zamiany konsumpcji | Plan i odbior: stabilnosc i satysfakcja BK, Food miast przed i po na kopii zapisu | 14 |

Dwa dodatkowe bledy znalazlem sam przy nowym rachunku i tez sa poprawione:
- **Pola mieszczan.** v1 nie dawal miastom ziemi ani rak w polu (chlopi miast, niewolni, wyrobnicy przy zniwach staly bezczynne). To byla glowna przyczyna
  "braku 6% w Essos" (37-43% ludzi w miastach). Teraz rdzen twierdzy z mapy (okregi.npz `fort_core`) to pola mieszczan: 1,39 mln pracujacych w 89 miastach.
- **Lowy.** Tabela nie miala dziczyzny, wiec Wolni Ludzie "kupowali" zboze z Reach przez 18 dni drogi. Dopisalem zajecie `hunter` (1,2 kg miesa na dobe w lesie
  i sniegu).

---

## 2. Regula produkcji: rece x wydajnosc x ziemia

**produkcja(blok b, zajecie c, towar g) [kg/d] = R_b x x(b,c) x P0[c,g] x L(c, ziemia_b) x pora roku (WinterBite jak dzis) x prace okregu (PO) x czynniki rak (E4/5, E4/7)**

### 2.1 Rece
- R_b = ludzie okregu x udzial bloku x LAMBDA stanu: chlopi 0,50, niewolni 0,65, rzemieslnicy 0,50 (miasto 0,55), szlachta i kupcy 0.
- Ludzie okregu = ludzie w domu + konta uchodzcow tego okregu (ten sam mianownik co Devastation.YieldFactor). Ubytek rak (uchodzcy, sluzba, polegli) robia
  dalej czynniki E4/5 i E4/7 - raz (uwaga 10).
- **Pola mieszczan** (nowe): chlopi miasta x 1,0, niewolni miasta x 0,5 (latyfundia, ogrody Volantis i Zatoki), wyrobnicy x 0,2 (zniwa, ogrody podmiejskie) [H: Rzym,
  agro-miasta Sycylii i Andaluzji]. Swiat [M]: 21,6 mln pracujacych w polu (20,2 mln we wsiach + 1,39 mln na polach mieszczan) na 51,85 mln ludzi.
- Kolejnosc rak w bloku: (1) dom - straczki, ogrod, mleko, kury, ul (rece = potrzeba / (4 kg x L_dom)); (2) zboze wlasne z piwem domowym; (3) reszta: zajecia bloku.

### 2.2 Ziemia z mapy (uwaga 16)
- **Blok 0** = punkt wsi gry (jej typ ma tam co najmniej L 0,3). **Bloki 1-19** = mieszanka ziemi calego okregu: wszystkie komorki okregu z `okregi.npz`
  (teren, brzeg do 3 jedn., rzeka do 3 jedn., gory do 2 jedn., strefa klimatu z kultury) wazone przydatnoscia (ludzie siedza tam, gdzie ziemia karmi),
  rozdzielone najwiekszymi resztami. Wioski poboczne - kazda swoj punkt (arm_map_villages.tsv). Pola mieszczan - 20 blokow z rdzenia twierdzy.
- **Powierzchnia**: komorka mapy = 2,84 x 2,84 km = 807 ha (skala wedlug Muru). Liczy sie tylko ziemia, na ktorej cos rosnie (najlepsze L >= 0,3). Blok dostaje
  powierzchnie okregu wedlug swojego udzialu rak. Ha na pracujacego w polu [M]: Reach 8,5, Westerlands 9,7, Dorzecze 21, Polnoc 55, Qarth 4,8, Zelazne Wyspy 10.
  Gdy ziemi brakuje, rosnie renta ziemi bloku i rece ida do zajec, ktore z hektara daja wiecej. Ziemia bywa pelna w 33% blokow [M].
- **Dolina rzeki w goracu**: piasek przy rzece w strefach srodziemnomorskiej, goracej i tropiku liczy sie jak trawa (namuly: Nil, Rhoyne, Skahazadhan).
  Zboze dostaje tam dalej nawadnianie x1,5.
- **Zboze wlasne bez progu**: udzial wlasnej potrzeby ziarna = (L_zboza - 0,2) / 0,4, obciete do 0-1 (v1: progi 0,4 i 0,6 dawaly zero na Zelaznych Wyspach).
  Teraz Zelazne Wyspy siewa 30% swojego ziarna, Dorne 69%, Polnoc 90%.

### 2.3 Tabele swiata: wydajnosc P0, koszt K, ziemia
kg na pracujacego na dobe na ziemi 1,0, srednia zwyklego roku (ks_tabele.py + ks_tabele2.py). Widelki i zrodla przy kazdym wierszu w plikach.

| zajecie | P0 (glowne towary) | zmiana po recenzji | K d/dobe | ha na pracujacego |
|---|---|---|---|---|
| farma zbozowa | zboze 3,0 netto; zywiec 0,33; jaja, miod, owoce drobno | - | 0,10 (zaprzeg, plug) | 4,0 |
| zboze wlasne | jak farma | bez progu | - | 4,0 |
| dom | 4 kg straczkow, mleka, ogrodu; jaja 0,25; miod 0,03 | - | - | 0,3 |
| rybak | ryby 4,0 | 6 -> 4 (widelki 3-10: lodz przybrzezna ok. 1 t/rok, buss tylko w sezonie) | 0,85 (lodz, sieci, sol do solenia) | 0 |
| owczarz | zywiec 2,4, welna 0,4, ser 0,2, maslo 0,06 | -20% (strzyzenie, wykoty, mycie runa to tez praca) | 1,70 (stado ok. 200 owiec x 17 d, 18%/rok) | 120 |
| obora (ser albo maslo) | zywiec 0,8, ser 1,2 / 0,4, maslo 0,32 / 0,64 | - | 0,60 (12 krow) | 18 |
| swiniopas | zywiec 5,0 | - | 0,65 | 0 (las) |
| winnica | winogrona 8 | - | 0,30 | 1,2 |
| gaj oliwny | oliwki 8, pomarancze 0,3 | 10 -> 8 (widelki 6-15) | 0,65 (gaj 10-15 lat do plonu) | 3,5 |
| sad pomaranczowy | pomarancze 4 | 6 -> 4 (sredniowiecze nizej) | 0,50 | 1,0 |
| ogrod targowy | jablka 4, marchew 5 | - | 0,05 | 1,0 |
| len / jedwab | len 1,5 / jedwab 0,025 | - | 0,10 / 0,30 | 1,4 / 1,0 |
| trapera | futra 0,20 | 0,25 -> 0,20 | 0,10 | 0 |
| **mysliwy (nowe)** | mieso dziczyzny 1,2, futra 0,03 | nowe | 0,10 | 0 |
| stadnina | konie 0,01 szt., owce 0,6, ser 0,1 | konie 120 d/szt. [H] | 0,60 | 25 |
| wielorybnik / morsy | mieso 3, tran 0,6 / kiel 0,1 | - | 1,60 / 1,00 | 0 |
| regale (zloza, las, oazy, przyprawy) | sol 60 (morska 50), glina 800, wapien 300, marmur 150, srebro 15, zloto 0,3, zelazo 2, drewno 400, daktyle 8, przyprawy 1 | zelazo, daktyle i przyprawy przeszly do regale | - (robotnik ma zaplate jak w polu) | 0 |

Przetwory w miastach jak w v1 (chleb 60 kg, piwo 150 l, wino 80 l, oliwa 15 kg, plotno 0,25 kg, sukno 0,25 kg, skora 6 kg, narzedzia 1,5 kg, garnki 15 kg,
garum 20 kg na rzemieslnika; wsady 1:1 masowo). Przetwory potrzebuja ok. 1,7 mln rak z 3,3 mln pracujacych rzemieslnikow (plotno 0,84, sukno 0,65) [M v1, popyt sie nie zmienil].

---

## 3. Regula wyboru zajecia (jedna na start i na gre)

### 3.1 Dochod rodziny przy cenie targu
- **wynik(b,c) = ln( L(b,c) x Suma_g P0[c,g] x wartosc_g x cena_g(rynek bloku) - K_c - renta_b x ha_c )**, dalej logit bloku z beta 10
  (blok rozdziela rece; zajecie z mapy ma premie x1,25 = prog Jeffa).
- Zadnych referencji ani cen zapamietanych z planu. W grze ta sama funkcja liczy dochod przy 28-dobowej sredniej cenie wozow w miescie wsi (GoodsLedger).
- Start = rownowaga tej funkcji: ceny cienia rynku tak dlugo, az podaz = popyt (+-2%), z renta ziemi bloku tak dlugo, az ziemi starcza. Ceny cienia nie wchodza do gry;
  do gry idzie podzial rak i ceny startu z rozdz. 6.

### 3.2 Regale: zloza, las na sprzedaz, oazy, ogrody przypraw (uwaga 9)
- Kopalnia soli, gliny, wapna, marmuru, srebra, zlota i zelaza, wyrab drewna na sprzedaz, oaza daktylowa i ogrod przypraw to prawo pana (regalia gornicze,
  prawo lesne, prawo wody w oazie) [H]. Pan zatrudnia tylu ludzi, ile targ kupi. Robotnik dostaje zaplate jak w polu, reszta to renta (w grze: podzial utargu wozu, paczka 112).
- **Wolumen = rece x P0 x L, a rece ida za cena towaru**: gdy 28-dobowy indeks towaru w miescie wsi > 1,2, zloze przyjmuje +10% rak na cykl; < 0,8 - zwalnia -10%;
  sufit = polowa rak bloku ze zlozem, podloga 0. Start = tyle rak, ile targ kupuje przy cenie 1 (najpierw zloza wlasnej krainy, brak - z innych krain rynku).
- Dlaczego nie ta sama regula co pole: przy cenach gry kopacz gliny zarobilby 80 d dziennie, solownik 12, drwal 16, rolnik 1,06 (tabela 3.4). Ta sama regula wpedzilaby
  caly okreg w gline, cene na podloge i zatkane wsie. Historycznie robotnik przy zlozu nie bral wartosci urobku; bral ja wlasciciel prawa.
- Swiat [M]: zelazo 104 tys. rak, daktyle 94 tys., drewno 56 tys., sol 16 tys., przyprawy 15 tys., zloto 5 tys., srebro 0,8 tys. Sufit zloza jest od 2 do 13 000 razy
  wiekszy od popytu; najciasniej: daktyle w Essos x2 i zelazo x3-4 (tam cena moze naprawde rosnac).

### 3.3 Lepszy stol (uwaga 7)
- **R8 - nadwyzki uboczne gospodarstw** (mieso, jaja, ser, ryby, jablka, marchew, pomarancze, daktyle, oliwki, miod, skory) zostaja na stole okregu, ktory je zrobil,
  zamiast ziarna (ta sama kaloria), do +30-200% jego koszyka. Swiat [M]: mieso 0,89 kt/d, jaja 0,28, skory 0,19, ryby 0,13, ser 0,11, marchew 0,09, jablka 0,10.
- **R8b - wspolna nadwyzka rak** (wszystkie towary jedzenia naraz ponad popyt): koszyk wsi tego rynku rosnie o mediane nadwyzki, najwyzej +10%. Wynik [M]:
  Westeros x1,061, Essos x1,016. To jawne jedzenie wsi i jawny Dostatek, nie ciecie stawki. Brak nie jest zmniejszany - to prawdziwy niedobor.
- Ciec "1/1,08" i "1,05 x popytu" z v1 nie ma. Podloga ceny cienia 0,05 (v1: 0,2) - towar bez zbytu traci rece sam.

### 3.4 Co pokazal rachunek: ceny gry nie zgadzaja sie z praca (uwaga 1)
Dochod rodziny na pracujacego po koszcie K, ziemia 1,0, d na dobe [M]:

| zajecie | przy cenach bazowych gry | przy cenach rownowagi | pracujacy |
|---|---|---|---|
| kopalnia gliny (regale) | 80,0 | - | 404 |
| oaza daktylowa (regale) | 48,3 | - | 93 685 |
| wapiennik / kamieniolom / srebro (regale) | 30,0 | - | 1 073 |
| ogrod przypraw (regale) | 27,0 | - | 15 090 |
| drwal (regale) | 16,1 | - | 56 141 |
| sol (regale) | 10-12 | - | 15 535 |
| kopalnia zelaza (regale) | 5,1 | - | 103 845 |
| trapera | 3,90 | 0,81 | 152 283 |
| gaj oliwny | 3,65 | 0,67 | 531 541 |
| sad pomaranczowy | 3,50 | 0,95 | 49 060 |
| owczarz | 3,35 | 2,25 | 668 426 |
| rybak | 3,15 | 1,02 | 853 836 |
| len | 2,90 | 2,77 | 221 974 |
| obora (ser / maslo) | 2,23 / 1,96 | 3,01 / 2,93 | 1 710 259 |
| jedwab | 2,20 | 1,34 | 309 218 |
| winnica | 2,10 | 1,40 | 781 096 |
| ogrod targowy | 1,75 | 2,11 | 314 359 |
| mysliwy | 1,58 | 0,63 | 131 194 |
| stadnina | 1,06 | 2,38 | 559 829 |
| farma zbozowa | 1,06 | 2,66 | 7 750 172 |

- Przy cenach gry praca w zbozu jest 3-4 razy gorzej platna niz w rybach, oliwkach czy owcach. Historycznie wszyscy ci ludzie zarabiali ok. 1-2 d dziennie;
  ceny bazowe gry (HistoricalPrices) to ceny Anglii ok. 1300 r., gdzie wino, oliwa, ryby solone i futra byly towarem z daleka albo z drogim kapitalem.
- **Rownowaga tej reguly** (srednia wazona popytem = 1): zboze 2,27 w Westeros i 3,14 w Essos, oliwki 0,20 / 0,42, ryby 0,42 / 0,52, welna 0,74 / 0,62,
  winogrona 0,85 / 0,55, ser 1,05 / 1,38, maslo 1,28 / 1,81, konie 2,2. To sa ceny, do ktorych gra bedzie dazyc, jesli regula dziala.
- **Co robie, a czego nie**: P0 poprawilem tylko w widelkach (ryby, owce, gaj, sad, futra - tabela 2.3). Cen bazowych NIE zmieniam (zmiana wartosci przedmiotow w trwajacym
  Ironmanie przeliczylaby wszystkie zapasy i utargi, a tabele maja widelki 2-3 razy). Start bierze ceny rownowagi w pasmie, ktore polka gry utrzyma: 0,7-1,5
  (rozdz. 6). Reszte drogi zrobi rynek: regula A7 z hamulcem Jeffa (+25% przez ok. 60 dni, 1 wioska na okreg, maly udzial regionu na sezon) przesunie najpierw
  troche rak, potem ceny same dojda do kosztow pracy. Prognoza [M]: zboze drozeje w ciagu sezonu-dwoch, oliwki, ryby i welna tanieja.
- **Mozliwosc na pozniej (nie teraz): KS-W "ceny bazowe z pracy"** - jesli po sezonie gry zmierzone ceny potwierdza kierunek, wartosci bazowe oliwek, ryb,
  welny, pomaranczy, daktyli i przypraw mozna zblizyc do cen u producenta (z historii, nie z mojego modelu). Osobna paczka, osobna decyzja.

### 3.5 Dlaczego nie "pasmo A7 przy cenie 1" i dlaczego nie referencje
- Sprawdzilem tez wariant, w ktorym start to stan, z ktorego regula A7 przy cenie 1 nic nie zmienia (zajecie musi miec ziemie >= 80% najlepszej).
  Nie trzyma bilansu: oliwki x3-4,9, maslo 0,66 i len 0,43 w Essos (`ks_wynik_v2p_*`). Tabela ziemi i ceny gry nie pasuja do koszyka - przy cenie 1 kazda regula zachowania
  od razu chce cos zmieniac.
- Dlatego start = rownowaga tej samej reguly co w grze, ceny startu jak najblizej jej cen, a roznica jawnie w raporcie i w logu ("KS: nacisk na zmiane").
  Zadna liczba nie jest ukryta w wadze.

---

## 4. Wyniki modelu (wariant zalecany: rynki Westeros / Essos, KS-K, premia mapy 1,25, mysliwi)

### 4.1 Bilans: podaz / popyt na rynku [M]

| towar | popyt swiata t/d | Westeros | Essos | swiat | uwagi |
|---|---|---|---|---|---|
| zboze (z ziarnem na piwo) | 31 753 | 1,00 | 1,01 | 1,00 | |
| ryby | 3 389 | 1,01 | 1,01 | 1,01 | z wsadem garum |
| mieso | 2 937 | 1,00 | 1,00 | 1,00 | lepszy stol 0,89 kt/d |
| ser / maslo | 1 627 / 755 | 1,00 / 1,00 | 1,01 / 1,01 | 1,00 | |
| jaja | 1 025 | 0,96 | 1,03 | 0,98 | towar domowy, cena bez wplywu na rece |
| jablka / marchew | 1 180 / 1 296 | 1,00 / **1,36** | 1,07 / 1,01 | 1,02 / 1,23 | marchew: prawdziwa nadwyzka (przy kazdej farmie) |
| pomarancze | 302 | 1,00 | 1,00 | 1,00 | |
| winogrona (z winem) | 4 679 | 1,01 | 1,01 | 1,01 | |
| oliwki (z oliwa) | 4 324 | 1,01 | 1,00 | 1,01 | |
| daktyle, sol, drewno, glina, wapien, marmur, zelazo, srebro, zloto | regale | 1,00 | 1,00 | 1,00 | rece za cena |
| miod | 196 | **0,91** | **1,12** | 0,99 | towar domowy (ul przy chacie) |
| przyprawy | 9 | 1,03 | 1,13 | 1,08 | uboczne z oaz |
| welna / len / jedwab | 234 / 262 / 4 | 0,99 / 1,00 / 1,02 | 1,01 / 1,01 / 1,01 | 1,00 | |
| skory | 450 | 1,00 | 1,01 | 1,00 | |
| futra | 35 | **1,20** | 1,01 | 1,12 | prawdziwa nadwyzka Polnocy |
| tran | 9 | 1,00 | **0,88** | 0,97 | swiatlo poludnia z oliwy |
| konie | 5,6 (=150 szt. gry/d) | 1,00 | 1,01 | 1,00 | stadniny w regule |

- Zbieznosc: 1 800 rund, zadnego towaru rak poza pasmem 2%.
- Poza +-5% tylko: marchew Westeros 1,36, jablka Essos 1,07 i futra Polnocy 1,20 (prawdziwe nadwyzki - regula zdjela z nich wszystkie osobne rece, reszte
  robia farmy zbozowe i mysliwi przy okazji), miod 0,91 / 1,12 (ul przy chacie - cena go nie rusza), przyprawy Essos 1,13 (uboczne z oaz), tran Essos 0,88
  (w Essos swiatlo daje oliwa). Jaja 0,96 / 1,03 sa w pasmie.
- Lekarstwo dla marchwi: popyt (KS-K w ZC0: wiecej warzyw w koszyku chlopa Westeros) albo mniejszy dodatek marchwi na farmie zbozowej (0,05 kg - zostaje do pomiaru).

### 4.2 Krainy - kg na mieszkanca na dobe [M]

| kraina | ludzie mln | w miastach | ha na prac. | zboze | ryby | mieso | nabial | owoce | winogrona | oliwki | samowystarczalnosc kcal |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Reach | 7,98 | 10% | 8,5 | 0,751 | 0,101 | 0,049 | 0,037 | 0,056 | 0,227 | 0,061 | 1,18 |
| Volantis | 4,99 | 25% | 8,1 | 0,882 | 0,011 | 0,061 | 0,027 | 0,085 | 0,368 | 0,008 | 1,63 |
| Qarth | 3,98 | 37% | 4,8 | 0,324 | 0,202 | 0,023 | 0,013 | 0,044 | 0,057 | 0,239 | 0,73 |
| Westerlands | 3,98 | 8% | 9,7 | 0,703 | 0,006 | 0,054 | 0,121 | 0,125 | - | - | 1,11 |
| Dorzecze | 3,48 | 8% | 21,1 | 0,720 | 0,004 | 0,064 | 0,165 | 0,023 | - | - | 1,15 |
| Dolina | 2,99 | 6% | 11,1 | 0,675 | 0,019 | 0,058 | 0,109 | 0,145 | - | - | 1,02 |
| Polnoc | 2,98 | 8% | 54,9 | 0,588 | 0,031 | 0,129 | 0,056 | 0,012 | - | - | 0,90 |
| Zatoka Niewolnicza | 2,77 | 43% | 21,9 | 0,339 | 0,035 | 0,058 | 0,020 | 0,137 | 0,061 | 0,216 | 0,86 |
| Braavos | 2,49 | 32% | 12,4 | 0,516 | 0,016 | 0,042 | 0,094 | 0,039 | - | - | 0,82 |
| Krainy Burzy | 2,49 | 5% | 14,2 | 0,795 | 0,012 | 0,050 | 0,042 | 0,093 | 0,189 | 0,007 | 1,13 |
| Norvos | 1,49 | 40% | 42,1 | 0,563 | 0,000 | 0,056 | 0,136 | 0,045 | - | - | 0,98 |
| Pentos | 1,49 | 33% | 38,4 | 0,647 | 0,027 | 0,042 | 0,045 | 0,033 | 0,011 | - | 0,90 |
| Qohor | 1,49 | 33% | 45,6 | 0,521 | 0,000 | 0,059 | 0,158 | 0,028 | - | - | 0,99 |
| Dorne | 1,48 | 8% | 36,6 | 0,327 | 0,182 | 0,023 | 0,018 | 0,137 | 0,003 | 0,502 | 0,87 |
| Lys | 0,99 | 40% | 20,9 | 0,505 | 0,161 | 0,040 | 0,018 | 0,056 | 0,058 | 0,522 | 1,21 |
| Myr | 0,99 | 40% | 35,0 | 0,734 | 0,022 | 0,070 | 0,027 | 0,076 | 0,077 | 0,052 | 1,33 |
| Tyrosh | 0,99 | 40% | 22,4 | 0,321 | 0,200 | 0,040 | 0,017 | 0,058 | 0,046 | 0,949 | 1,17 |
| Smocza Skala | 0,81 | 5% | 16,7 | 0,559 | 0,023 | 0,076 | 0,103 | 0,277 | - | - | 0,95 |
| Lorath | 0,79 | 31% | 31,7 | 0,629 | 0,011 | 0,044 | 0,048 | 0,014 | - | - | 0,87 |
| Dothrakowie | 0,74 | 0% | 95,5 | 0,944 | 0,000 | 0,080 | 0,128 | 0,095 | 0,002 | - | 1,55 |
| Ziemie Korony | 0,59 | 75% | 89,4 | 0,277 | 0,000 | 0,028 | 0,061 | 0,068 | - | - | 0,46 |
| Zelazne Wyspy | 0,47 | 5% | 10,0 | 0,163 | 0,722 | 0,051 | 0,048 | 0,001 | - | - | 0,60 |
| Ibben | 0,34 | 20% | 100,5 | 0,432 | 0,015 | 0,061 | 0,051 | 0,005 | - | - | 0,66 |
| Wolni Ludzie | 0,29 | 0% | 121 | 0,000 | 0,094 | 0,296 | 0,027 | - | - | - | 0,31 |
| Valyria | 0,25 | 0% | 6,7 | 0,440 | 0,808 | 0,019 | 0,014 | 0,019 | 0,005 | 0,033 | 1,08 |
| Sarnor | 0,24 | 20% | 199 | 0,497 | 0,000 | 0,086 | 0,238 | 0,015 | - | - | 1,14 |
| Wyspy Letnie | 0,11 | 10% | 2,0 | 0,057 | 0,715 | 0,020 | 0,010 | 0,052 | - | - | 0,47 |
| Yi Ti | 0,10 | 20% | 164 | 0,499 | 0,024 | 0,067 | 0,152 | 0,012 | - | - | 1,01 |
| Skagos | 0,05 | 0% | 25,5 | 0,028 | 0,474 | 0,152 | 0,044 | - | - | - | 0,41 |

- Zelazne Wyspy: 34% rak to rybacy, 26% zboze wlasne (siewa 30% potrzeby na cienkiej ziemi), reszta dom, bydlo, owce. Kupuja ok. 520 szt. zboza dziennie z Reach morzem.
- Dorne: jedwab 26%, zboze 26%, gaje 14%, rybacy 11% - zboze z dolin rzek i oaz (0,33 kg na glowe zamiast 0,02 w v1).
- Wolni Ludzie i Skagos zyja z dziczyzny i ryb; ich koszyk w ZC jest jednak zbozowy (0,31 i 0,41 kalorii z wlasnej ziemi) - do poprawy w KS-K (koszyk lowcow).

### 4.3 Przyklad z pytania Jeffa [M]

| okreg | ludzie | w polu | ziemia uzytkowa | zboze kg/d | kg zboza na glowe | ryby kg/d | targ (szt. gry/d) |
|---|---|---|---|---|---|---|---|
| Oakendale (Reach, drwale) | 142 781 | 67 678 | 221 tys. ha | 132 470 | 0,93 | 1 127 | zboze +132, winogrona +44, oliwki -37, drewno +18 |
| Redgrass Field (Reach, stadnina) | 133 342 | 63 204 | 2 242 tys. ha | 86 373 | 0,65 | 0 | zboze +25, drewno +32, winogrona -62 |
| Vorfjall (Zelazne Wyspy, sol) | 14 604 | 7 085 | 82 tys. ha | 2 520 | 0,17 | 10 406 | sol +23, ryby +10, zboze -14 |
| Sealskin Point (Zelazne Wyspy) | 14 604 | 7 085 | 180 tys. ha | 1 106 | 0,08 | 16 855 | ryby +27, zboze -18 |

Ten sam rolnik w Reach robi 3 kg ziarna dziennie na ziemi 1,0; na Zelaznych Wyspach ziemia zbozowa ma 0,35, wiec ten sam czlowiek woli lodz.
Okreg 10 razy mniejszy robi kilkadziesiat razy mniej zboza - bo ma mniej rak i gorsza ziemie, nie dlatego, ze ktos tak ustawil.

### 4.4 Praca swiata [M] (mln pracujacych, razem 21,6)
farma zbozowa 7,75; zboze wlasne 5,42; dom 2,10; obora 1,06 + "na maslo" 0,65; rybacy 0,85; winnice 0,78; owczarze 0,67; gaje 0,53; ogrody 0,31; jedwab 0,31;
len 0,22; stadniny 0,56; trapery 0,15; mysliwi 0,13; zelazo 0,10; daktyle 0,09; swiniopasi 0,06; drwale 0,06; sady pomaranczowe 0,05.

### 4.5 Zmiany zajec wobec mapy [M]
- Premia mapy 1,25: wioski poboczne z wiekszoscia rak przy zajeciu startowym 1 194 z 1 953 (61%); we wsiach glownych 25% rak robi typ gry (zloza i las sa w regale osobno).
- Premia 1,05: ten sam bilans, ale tylko 39% wiosek zostaje przy swoim. **Zostaje 1,25** (= prog Jeffa; mniej zmian na mapie przy tym samym bilansie).
- Typ wsi gry zostaje (klasa BK, magazyn, nazwa, ikona do decyzji wedlug PO A9).

### 4.6 Warianty [M]

| wariant | bilans | uwagi |
|---|---|---|
| zalecany (strefy, KS-K, 1,25) | 0,97-1,03 | jak 4.1 |
| premia 1,05 | jak zalecany (te same wyjatki) | 61% wiosek zmienia zajecie |
| jeden rynek swiata (Waskie Morze) | 1,00 (marchew 1,18, futra 1,69) | zboze 2,54, oliwki 0,25, ryby 0,44 wobec sredniej |
| bez KS-K | Westeros sie nie domyka (oliwki, welna, mieso) | koszyk bez oliwy i daktyli nie przyjmuje oliwek Dorne i Reach |
| wyspy osobno (krainy tylko morskie: Zelazne Wyspy, Skagos, Wyspy Letnie, Valyria) | Zelazne Wyspy zboze 0,35, Skagos 0,05, Wyspy Letnie 0,18 | bez statkow wyspy maja prawdziwy glod ziarna |
| kazda kraina sama (bez handlu) | swiat: zboze 0,90, ryby 0,87, ser 0,88 | Ziemie Korony 0,45, Skagos 0,37, Zelazne Wyspy 0,56, Qarth 0,72, Dorne 0,72, Pentos 0,78 kcal |

Warianty 1,05, swiat, wyspy i bez KS-K liczylem przed dopisaniem mysliwych - roznia sie tylko za Murem i na Polnocy.
**KS-K** (poprawki koszyka) - uzasadnienie niezalezne od bilansu [H]: oliwa 10-20 l na osobe rocznie na poludniu (Rzym ok. 20 l; nasz koszyk 1,5 l); daktyle chlebem oaz;
kozuchy i futra zima na Polnocy. Zapisane przed liczeniem; decyzja Jeffa w ZC0 razem z koszykami.

### 4.7 Skala 1:1 (wojsko, zamki, dwory) - decyzja przed Planem (uwaga 21)
- Wojsko i zalogi sa w grze 1:1, a targ jest probka 1:37. Sztuka kupiona przez partie to sztuka, ktora musi byc na targu, czyli 37 prawdziwych sztuk produkcji.
  **Zostaje 1:1 x sigma** (zasada ZC: "co jest w grze 1:1, liczy sie 1:1"), bo inaczej targ zabraknie dokladnie tego, co gra zabiera. Udzial w popycie [M]: zboze 5%,
  ryby 14%, ser 6%, drewno 33% (budowy i kuznie), zelazo 64% (kuznie). To zawyza prace na "ogon" wojska (ok. 11-18 kg zywnosci na zolnierza dziennie);
  zmiana tempa jedzenia partii zmienilaby oblezenia i glod wojska - nie w KS.
- Dwory (CourtGoods) to wielkie rody 1:1 (ok. 10 tys. osob), nie ma ich w koszyku szlachty ksiegi (szlachta = 1,5% ludzi ksiegi). Najwiekszy udzial dworow: ryby
  155 szt./d = 2% popytu ryb. ZC0 sprawdza, ze ZC2 nie liczy rodow drugi raz.
- Jesien: AI trzyma 8 dni zapasu zamiast 4 (BkSupplyDaysCap 4 x AutumnStockMultiplier 2 [K]) - Plan mierzy 1:1 po pierwszych 14 dobach jesieni.

---

## 5. Handel, zasieg i przewoznicy (uwagi 4 i 17)

- Graf z `geo-trasy.tsv`: 564 trasy ladowe twierdza-twierdza, 23 morskie, 569 wies-twierdza; w grze to samo daje MapDistanceModel. Czas = droga / predkosc
  (zalozenie 70 jedn. na dobe, morze x2 - predkosc i udzwig mierzy ZC0). Zasieg: jedzenie psujace sie najwyzej 7 dob (CaravanGoods), reszta 30 dob.
- **Potrzebny przewoz miedzy krainami [M]: 35 355 szt. gry dziennie**, z czego morzem 2 176. Najwieksze: zboze Volantis -> Qarth 2 302 szt./d (7 dni), oliwki
  Tyrosh -> Volantis 1 519, zboze Volantis -> Braavos 974 (9 dni), oliwki Dorne -> Reach 880, zboze Dothrakowie -> Zatoka 828, zboze Reach -> Dorne 707,
  zboze Reach -> Polnoc 649 (11 dni), zboze Reach -> Zelazne Wyspy 524 (12 dni, morze), drewno 2 812 ladunkow. Ryby 1,05 kt/d i mieso 0,28 kt/d jechalyby dalej niz 7 dob -
  ten przewoz odpada (zostaje w krainie zrodla jako lepszy stol albo nadwyzka).
- **Zmierzone dzis [L]**: wywoz kupcow wozem 3 341 szt./d (601 wozow, srednio 3,6 doby drogi, zasieg 250 jedn.), karawany: 87 wjazdow do miast na dobe
  (22 ze sprzedaza surowcow), statki: 7 statkow rozbitych druzyn w porty. **Potrzeba ok. 10 razy wiecej przewozu miedzy krainami, niz dzis jezdzi.**
- **Regula KS**: kraina, ktorej plan zakupow z innych krain przekracza 1,5 x zmierzonego przewozu na jej korytarzach, **liczy sie sama** (plan zapasowy "kraina"
  - jej ludzie przesuwaja rece do jedzenia przy wlasnych, wyzszych cenach). Plan liczy oba plany dla kazdej krainy.
- **Co musi sie zmienic, zeby przewoz byl** (warunek KS-A, osobny punkt paczki ZC1/KS-A):
  1. gradient cen startu: miasto krainy z brakiem dostaje cene zrodla x (1 + 3% za dobe drogi), najwyzej 1,5 - kupcy maja marze, wiec jada (dzis ceny splaszczone = nikt nie jedzie);
  2. zasieg wywozu kupcow (E8/O3, dzis 250 jedn.) do 30 dob drogi dla towarow trwalych i 7 dob dla psujacych sie;
  3. statki: KS-H (kasy portow i karawany BK ze statkami z NavalDLC) - ZC0 mierzy, ile dzis plynie przez Waskie Morze i na Zelazne Wyspy.
- **Kontrola w rampie**: dla kazdej krainy z zakupami co dobe: dowiezione / plan. Ponizej 80% przez 7 dob - popyt ZC4 tej krainy staje (lud kupuje jak dzis),
  a kraina przechodzi na plan "kraina". Linia "KS: korytarz X -> Y dowiezione p% planu".
- Bez przewozu (wariant "kraina") swiatu brakuje ok. 10% jedzenia, a Ziemie Korony (0,45), Skagos (0,37), Zelazne Wyspy (0,56), Wyspy Letnie (0,53) i Qarth (0,72) maja
  prawdziwy glod. Dlatego rozdz. 8 (podloga Famine i bramka krainy) jest twardym warunkiem.

---

## 6. Ceny startu, polki i wyprzedzenie (uwagi 2, 3, 20)

- Wzor gry [K: RawPrice]: indeks = (Popyt / (0,1 x Podaz + 0,04 x WartoscPolki + 2))^0,6; polka 7 dni zakupow = indeks 1, polka 3,5 dnia = 1,52, 14 dni = 0,66.
- **Cena startu = cena rownowagi reguly (rozdz. 3.4), obcieta do 0,7-1,5**; towar psujacy sie (ryby, mieso, jaja, maslo, owoce, winogrona) - najwyzej 7 dni polki,
  czyli indeks co najmniej 1,0. Polka = 7 x indeks^(-1/0,6) dni [M]:

| towar | Westeros: rownowaga / start / polka | Essos: rownowaga / start / polka |
|---|---|---|
| zboze | 2,27 / 1,50 / 3,6 dnia | 3,14 / 1,50 / 3,6 |
| ryby | 0,42 / 1,00 / 7,0 | 0,52 / 1,00 / 7,0 |
| mieso | 0,35 / 1,00 / 7,0 | 0,79 / 1,00 / 7,0 |
| ser | 1,05 / 1,05 / 6,4 | 1,38 / 1,38 / 4,1 |
| maslo | 1,28 / 1,28 / 4,6 | 1,81 / 1,50 / 3,6 |
| winogrona | 0,85 / 1,00 / 7,0 | 0,55 / 1,00 / 7,0 |
| oliwki | 0,20 / 0,70 / 12,7 | 0,42 / 0,70 / 12,7 |
| welna | 0,74 / 0,74 / 11,7 | 0,62 / 0,70 / 12,7 |
| len | 0,88 / 0,88 / 8,7 | 1,05 / 1,05 / 6,5 |
| futra | 0,05 / 0,70 / 12,7 | 0,43 / 0,70 / 12,7 |
| konie | 2,18 / 1,50 / 3,6 | 2,05 / 1,50 / 3,6 |
| regale (sol, drewno, zelazo, daktyle ...) | 1 / 1 / 7 | 1 / 1 / 7 |

  Towary domowe (jaja, miod) i uboczne (marchew, jablka, skory) dostaja indeks 1,0 - ich cena nie przesuwa rak. Gradient drogi z rozdz. 5 dochodzi na miasta krain z brakiem.
- **Polka a rezerwa**: polka robi cene i tylko ona jest celem KS. Rezerwa = spichlerz okregu (racje Famine, poza wzorem ceny, FamineBarnYears 2 lata [K]) i Food warowni.
  KS nie robi rezerwy towaru i nie ma celow 30-90 dni.
- **Zapas tylko z prawdziwej nadwyzki**: podaz KS rusza przed popytem ZC4 o tyle dob, ile trzeba, zeby roznica (podaz KS - podaz dzis) zbudowala polke celu.
  Wyliczone [M]: zboze, ryby, maslo, welna, len, skory, zelazo, sol - 0 dob (dzisiejszy zapas wystarcza na polke krotsza niz 7 dni); ser 0,8; marchew 1,0; glina 2,4;
  daktyle 4,4; jablka 5,8; pomarancze 6,2; drewno 6,4; winogrona 6,5; mieso 9,2; oliwki 12,1; miod 13,4 (w dobach pelnego przyrostu podazy).
  **Rampa**: podaz KS rusza w D-14 i dochodzi do pelnej w D; popyt ZC4 rusza w D i dochodzi w D+14. Dwa trojkaty daja razem 14 dob pelnego przyrostu nadwyzki,
  czyli wiecej, niz potrzebuje najbardziej wymagajacy towar (miod 13,4). Ceny schodza przy tym z dzisiejszych (indeksy 1,5-7,6, bo dzis brakuje wszystkiego [L])
  do cen startu. Spichlerze nie oddaja ani racji, ani towaru.
- W dniu D srednia Podazy rynku (gra wygladza ja 15% dziennie) dostaje wartosc polki startu - to jedyne "ustawienie" ceny; nie tworzy towaru ani zlota i wygasa samo.

---

## 7. Pory roku (poprawione fakty, uwaga 14)

- W tej grze pory roku sa **wieloletnie** (WesterosClimate [K]). Kampania Jeffa [L, "Klimat: ... Summer od dnia 105196, planowo 3834 dni"]: lato konczy sie ok. doby 109030.
  Potem pierwsza jesien 300-450 dob, potem pierwsza zima 3-5 lat (ClimateFirstAutumnDays 300-450, ClimateFirstWinterYears 3-5 [K]). Opis "zima 91 dni co roku"
  z v1 i z recenzji byl nieaktualny.
- Zima (WinterBite [K]): wsie -50% x gradient polnocy (+-25%) x czynnik krainy (Wyspy Letnie 0, cieple poludnie 0,4, reszta 1); wojsko je +50%; jesienia AI trzyma 8 dni zapasu.
- **KS nie normalizuje zimy.** P0 to zwykly rok bez zimy Westeros; zima tnie stawke KS tak jak dzis tnie produkcje gry. Zima to wydarzenie swiata (zasada 2 Jeffa).
- **Kiedy D**: najlepiej jesien, co najmniej 14 dob po jej starcie (po zrywie zapasow AI) i co najmniej 60 dob przed jej koncem (dlugosc jesieni zna lista por klimatu
  w zapisie). Logi 15-16.10 obejmuja doby 108946-109028 [L], czyli kilkadziesiat dob gry na dzien grania - jesien to kilka dni grania Jeffa. Jesli paczki nie zdaza,
  KS-A wolno wlaczyc w zimie, ale tylko z podloga Famine i bramka krainy (rozdz. 8), a odbior liczy sie wobec zimowej podazy.
- **Odbior wobec oczekiwanej podazy tej pory**: dla kazdego towaru i rynku cel = podaz planu x (1 - ciecie zimy tej krainy) w tych samych dobach, popyt planu x (1 + zryw jesieni 1:1).
  Rowny bilans calego roku sprawdza sie dopiero po pierwszej pelnej porze bez zimy.
- Rezerwa Polnocy na zime = spichlerze okregow (2 lata potrzeby). Zima 3-5 lat z -50..-62% plonu wyczerpie je na Polnocy po ok. 2-4 latach - to sprawa Famine (dzis tak samo), nie KS.

---

## 8. Glod: podloga spichlerza i bramka krainy (uwaga 4)

- Spichlerz (Famine [K]): dzis plon = 1,2 x ludzie x q (q z modelu zywnosci gry). Pod KS racje = kcal czesci domowej okregu (zboze wlasne, dom, lepszy stol) / kcal racji.
- **Podloga**: dopoki zakupy ZC4 danej wsi nie dzialaja 14 dob z rzedu (wies kupila >= 80% swojego braku), racje = max(racje KS, racje z dzisiejszego wzoru).
  Ziarno kupione przez wies (ZC4) wpada do jej spichlerza.
- **Bramka krainy**: jesli po D w krainie pojawi sie wies z glodem (brak u > 10%, poza oblezeniem i najazdem) albo spichlerz krainy spada szybciej niz 1% potrzeby na dobe
  przez 7 dob, KS tej krainy sam wraca do dzisiejszych stawek i popytu (linia "KS: powrot krainy X - glod we wsiach N"). Druga proba tylko recznie.
- Zgony z glodu sa nieodwracalne, dlatego te dwa zabezpieczenia dzialaja bez wzgledu na klucze i nie maja wylacznika poza calym KS.

---

## 9. Pieniadze (uwagi 12 i 13)

1. **Zadna moneta nie powstaje.** KS nie ma wlasnych przelewow. Towar placi kasa kupcow przy sprzedazy wozem (VillageTakings, podzial 112: kiesa wsi ok. 55%, pan,
   majatki BK, dziesiecina, myto). Polki napelnia zwykly handel. Spichlerz nic nie oddaje.
2. **Przeplyw na dobe przy cenach startu [M]** (zl gry):

   | rynek | wozy wsi sprzedaja | do kies wsi (ok. 55%) | wsie kupuja | lud miast kupuje | dochod kies miast dzis [L] | lud miast / koszyk |
   |---|---|---|---|---|---|---|
   | Westeros | 226 tys. | 125 tys. | 121 tys. | 48 tys. | 389 tys. | 8,0 razy |
   | Essos | 328 tys. | 181 tys. | 114 tys. | 162 tys. | 229 tys. | 1,4 raza |

   - Lud miast stac na koszyk w obu strefach (w Westeros z duzym zapasem, bo 63% dochodu ludu miast to zold). Wsie zarabiaja wiecej, niz kupuja.
   - Kupcy placa wozom 554 tys. zl/d, a lud i wsie oddaja im 445 tys.; reszte (ok. 110 tys.) musza dac kupujacy 1:1 (wojsko, zamki, dwory) i warsztaty.
     **Plan sprawdza to co dobe na rynek**: kasy kupcow wobec zakupow od wozow i wartosci polki do zbudowania. Kasa kupcow spadajaca 7 dob z rzedu = linia
     "KS: kupcy traca" i stop rampy popytu na tym rynku.
3. **Kiesy ludu: KS ich nie rusza (decyzja).** Jeden skrypt `ks_kiesy2.py` (dane kiesa-ludu.csv doby 109024): krok "w osadzie miedzy W/R/P" przesunalby 4,08 mln zl,
   najwiecej z kies biedoty do kies kupcow (Oldtown biedota -162 tys., kupcy +103 tys.; Stoney Sept -168 / +96 tys.), bo zold zolnierzy wpada do kies biedoty.
   Przyczyna wrocilaby po kilku tygodniach. Rozjazd (Westeros 2,67 x norm, Essos 0,74) poprawia przeplyw: ZC4 (obrot od ludzi) i ZC8 (czesc zoldu do kas notabli).
   Kies wsi znamy tylko w sumie (4,52 mln) - VillageBook do pomiaru w ZC0.
4. **Odbior pieniedzy**: MoneyLedger "bez odbiorcy" = 0; pieniadz swiata +-0,5% bazy; kg koszyka niekupione z braku pieniedzy najwyzej 5% (rynek, stan); lud 8-10%
   pieniadza swiata, korony 19-22%.
5. KS nie rusza: lordow, skarbcow koron, notabli, kas kupcow (poza zwyklym handlem), sakiewek zolnierzy, Banku.

---

## 10. Towary bez producenta i towary z niczego (uwaga 11)

- **Garum**: para TownCrafts ryby + sol -> garum w Lys, Volantis, Myr, Tyrosh i Pentos; cel 82 szt./d (budzet ludu 1 637 zl/d); wsad ryb i soli liczony w popycie.
- **Wegiel drzewny**: wypal w okregach lesnych (6 kg drewna na kg wegla), regale jak drewno.
- **Poza bilansem** (ani popytu, ani producenta): sztabka zlota, kamienie, purpura, atrament, 60 ksiag, wol, ges, kura, sztabki zelaza.
- **Twarde bramki przed KS-A** (bez nich KS-A sie nie uzbroi):
  1. ZC1: BkRecipeYieldByWorkshop i WhaleMeatFromWhalersOnly czynne - ukryci rzemieslnicy BK (ok. 1 460 szt./d z niczego [L]) licza sie jako przetworstwo z wsadem;
  2. zamkniete +34 sztuk kazdego jedzenia dziennie i RemoveParty -17;
  3. w GoodsLedger zrodla bez nazwy najwyzej 2 szt./d na towar przez 7 dob Planu;
  4. PO etap 0-1: HamletKinds W2 naprawiony, CraftStep i arm_village_crafts.tsv w grze.
- **Produkcja BK poza typem wsi** (jablka 257 z 350, marchew 257 z 348, jaja 177 z 255 szt./d [L]): pod KS zdjeta (lista MineralOnce z waga 0) i zastapiona przez KS -
  te towary robia teraz gospodarstwa wedlug tabeli. Linia "KS: BK poza typem zdjete N szt./d".

---

## 11. Algorytm w grze

### 11.1 KS-P "Plan" (co 7 dob, gra bez zmian)
- Zrzut na watku glownym: ludzie okregow z kontami uchodzcow (PeopleBook), stany ZC2 i koszyki ZC0 w kg, zajecia wiosek (arm_village_crafts), ziemia okregow i pol mieszczan
  (plik `arm_ks_land.tsv` zapieczony skryptem z okregi.npz i teren.npz, ze skrotem; jak arm_village_crafts), zuzycie 1:1 z GoodsLedger (28 dob), wsady, popyt koni (HorseCensus),
  przewoz zmierzony (wozy wsi, wywoz kupcow 215, karawany, statki 181), pory roku (lista por klimatu), spichlerze.
- Watek roboczy, same tablice, wynik zawsze ten sam: ok. 98 tys. par blok-zajecie, cieply start z poprzedniego Planu (zwykle 200-500 rund), najwyzej 30 s.
- Kroki: (1) popyt krain; (2) regale; (3) rownowaga reguly 3.1 (ceny cienia, renta ziemi, test 2%); (4) lepszy stol R8, R8b; (5) korytarze: przewoz wobec 1,5 x zmierzonego,
  krainy ponad - plan "kraina"; (6) ceny startu i polki; (7) wyprzedzenie; (8) pieniadze; (9) zapis wyniku i logu.

### 11.2 KS-A "Apply" (od doby D - 14, po uzbrojeniu w zapisie, rozdz. 12)
- **Stawka wsi gry na dobe** = Suma po blokach okregu: udzial x rece x P0 x L x zima (WinterBite jak dzis) x czynniki rak E4/5 i E4/7 (raz) x prace okregu PO
  x sufit dymarki, RockGold, OreLedger - minus czesc domowa (to, co zjedzone w okregu i zostawione na stole) / (sigma 37 x kg sztuki).
- Krok KS w MaterialLaw.ProdPostfix zastepuje: stawke typu x poziom hearth x u_v x g(typ) z VillageByPeople, GoodsRates (sol x3,5, futra x3), Mine/LumberOutputMultiplier,
  HostFactor z VillageFruit i CamelLaw, A6. HarvestValve = 1 dla towarow KS. Towary spoza listy typu dopisuje CraftStep (PO A5, wzor W2), z nazwanym zrodlem
  w GoodsLedger "KS: zajecie X" i z tym samym lancuchem czynnikow (jedna funkcja dla wszystkich towarow wsi).
- **A7 od D** (uwaga 15): regula 3.1 przy 28-dobowej cenie wozow w miescie wsi; hamulec Jeffa: zmiana, gdy inne zajecie daje >= 1,25 x dochodu przez ok. 60 dob,
  koszt przebudowy do rzemieslnikow, okres przejsciowy (winnice, gaje, sady 364 doby), najwyzej 1 wioska na okreg naraz, najwyzej 5% rak regionu na sezon.
  Pierwsze zmiany najwczesniej ok. D+60. Linia "KS: nacisk na zmiane - wiosek ponad progiem N, najdluzej d dob".
- **Regale od D**: rece przy zlozu +-10% na 28 dob za cena (rozdz. 3.2).
- **Spichlerz**: racje z czesci domowej, z podloga (rozdz. 8).
- **Przetwory**: pary TownCrafts (zboze -> chleb, placek, piwo; winogrona -> wino; oliwki -> oliwa; miod -> miod pitny; ryby + sol -> garum; glina -> garnki; zelazo -> narzedzia),
  tempo WorkshopPace z rak rzemieslnikow, BK wedlug receptury (ZC1).
- **Rampa**: podaz od D-14 do D (14 dob, liniowo), popyt ZC4 od D do D+14. KS i ZC4 wlaczaja sie i wylaczaja jednym kluczem (KS bez ZC4 = zalew, ZC4 bez KS = sufit cen).
- **Okno odbioru KS-M**: D+22 .. D+49 (rozdz. 14).

### 11.3 Wydajnosc
Plan najwyzej 30 s na watku roboczym co 7 dob; doba KS (stawki, A7, regale) najwyzej 3 ms; cala doba gry najwyzej +10% wobec bazy.

---

## 12. Ironman: uzbrojenie w zapisie, Undo, zapis (uwagi 5, 6, 19)

- **Uzbrojenie siedzi w zapisie, nie w MCM.** McmSettings to AttributeGlobalSettings [K] - jeden zestaw dla calej gry, wiec klucz MCM nie moze sam niczego wlaczac.
  - Klucz MCM "Start Calibration: arm this save" (domyslnie Off). W nastepnej dobie KS zapisuje w arm_ks: id kampanii (Campaign.UniqueGameId [K]), dobe uzbrojenia,
    skrot planu, stan "uzbrojony", i **sam przestawia klucz z powrotem na Off**.
  - Apply wymaga co najmniej 7 dob Planu na tym zapisie i drugiego potwierdzenia ("Start Calibration: confirm") na tym samym zapisie, ze zgodnym id i skrotem planu.
  - Zapis bez arm_ks albo z innym id: raz na dobe linia "KS: klucz bez uzbrojenia tego zapisu - nic nie robie".
  - Autotest ma osobny klucz (czynny tylko w autotescie CrashScribe), nigdy klucza Jeffa.
- **Undo bez terminu**: klucz KS Off -> nastepna doba stawki gry, popyt ZC4, spichlerz i zajecia wiosek (stan z D-15 w arm_ks) wracaja; wygladzona Podaz rynkow wraca sama
  (15% dziennie). KS i ZC4 jednym kluczem w obie strony.
- **Czego nie da sie cofnac** (Jeff dostaje to prostymi slowami przed potwierdzeniem): towar juz wyprodukowany i sprzedany oraz zloto, ktore przy tym przeszlo zwyklym
  handlem; ludzie, ktorzy w tym czasie sie przeniesli albo zgineli z innych przyczyn (wojna, najazd - jak dzis). Kies KS nie rusza, racji nie zamienia.
- **arm_ks** (SaveText.Sync, paczki < 32 KB, razem najwyzej 150 KB): wersja formatu, stan (plan / uzbrojony / rampa / odbior / zwykly / wylaczony), id kampanii, dzien D,
  udzialy zajec ok. 12 tys. blokow wedlug stalego id (uid wioski, wies#mainK, miasto#polaK) ze skrotem arm_map_villages.tsv i arm_ks_land.tsv, stan rampy, zrzut do cofniecia.
  Niezgodny skrot albo wersja = plan liczony od nowa, nic ze starego nie jest stosowane. Brak arm_ks = gra jak dzis.
- CHANGELOG: ostrzezenie - starsza DLL na zapisie po Apply daje stan mieszany (stare stawki, nowe zajecia wiosek); powrot tylko przez Undo w nowej DLL.
- **Log**: "Kalibracja startu (KS): plan doba N | rynki ... | poza pasmem ...", "KS: uzbrojony zapis ...", "KS: rampa t/28 ...", "KS: korytarz X -> Y ...",
  "KS: nacisk na zmiane ...", "KS: powrot krainy ...", "KS: odbior k/28 ...". CSV: ks-plan.csv, ks-towary.csv, ks-korytarze.csv, ks-krainy.csv.
- Nic na zywym zapisie bez wgranej DLL po tescie; nic, gdy Jeff gra.

---

## 13. Kolejnosc paczek

1. **ZC0 + KS-P**: pomiar i plan na sucho (koszyki, VillageBook, przewoz, predkosc wozu, statki przez Waskie Morze, popyt koni, zryw jesieni). Decyzje Jeffa w ZC0: KS-K.
2. **ZC1**: drogi towarow (BK z wsadem, mieso wieloryba, +34, RemoveParty) - bramka KS-A.
3. **ZC2 + PO etap 0-1**: stany; HamletKinds W2, CraftStep, arm_village_crafts (bez A6 i A7).
4. **Przewoz**: gradient cen, zasieg wywozu kupcow, KS-H (porty, statki) - inaczej krainy z brakiem licza sie same.
5. **KS-A + ZC4 + A7** jednym kluczem. KS zastepuje ZC3 i ZC5 (ich czynniki planu targu wedlug krainy to odrzucone wyrownanie).
6. **KS-M**: 28 dob odbioru.
7. ZC6, ZC7; PO etap 3 (inwestycje gracza). AI nie inwestuje.
8. Pozniej, osobno: KS-W (ceny bazowe z pracy) tylko po pomiarze cen po sezonie.

Test kazdego kroku: autotest 120 dob na zapisie F-d240 wobec bazy WGRANIA 72, potem kopia zapisu Jeffa. Wgranie tylko po tescie i nigdy wtedy, gdy Jeff gra.

---

## 14. Odbior KS-M (doby D+22 .. D+49; wobec oczekiwanej podazy tej pory roku)

1. **Towary**: kazdy rynek i towar rak: podaz targu / (zuzycie + 1:1 + wsad), srednia 28 dob, w 0,95-1,05 wobec celu pory; wyjatki tylko z listy planu (marchew, jablka, futra,
   miod, przyprawy, tran) i najwyzej +-10% planu. Zrodla bez nazwy <= 2 szt./d na towar; BK ponad recepture 0.
2. **Polki**: kazdy towar 0,7-1,4 x polki startu (w dniach zakupow); zmiana polki najwyzej 2% dziennego zuzycia na dobe; miasta bez zboza <= 5%; miasta z polka zboza ponizej 2 dni <= 10%.
3. **Ceny**: mediana indeksu w pasmie startu +-0,3; zaden towar w wiecej niz 20% miast na suficie albo podlodze; nachylenie najwyzej +-0,5% na dobe.
4. **Pieniadze**: rozdz. 9 pkt 4; kasy kupcow nie spadaja 7 dob z rzedu na zadnym rynku.
5. **Rece i spichlerz**: suma stanow = ksiega; rece = ludzie x LAMBDA (probka 20 wsi, w tym 5 z uchodzcami - jeden czynnik rak); racje = kcal czesci domowej +-5% (z podloga);
   zgonow z glodu poza oblezeniami i najazdami: 0; zadna kraina nie wrocila przez bramke.
6. **Przewoz**: dowiezione / plan >= 0,8 na kazdym korytarzu planu; zatkane wsie <= 5%; wozow w miescie ponad 7 dob nie wiecej niz w bazie; jedzenie sprzedane na stracie <= 2 x bazy.
7. **Stabilnosc**: nacisk A7 (wioski ponad progiem 1,25) - raport, bez progu; prawdziwe zmiany A7 w oknie <= 1% wiosek; HarvestValve dla towarow KS = 1; regale: rece zloza +-20% w oknie.
8. **Dostatek**: liczony dla 798 z 798 osad; mediana D chlopow i biedoty >= 1,0 (wsie Westeros z lepszym stolem wyzej).
9. **Skutki uboczne** (uwaga 23): stabilnosc i satysfakcja osad BK oraz Food miast i zamkow - przed i po na kopii zapisu, odchylenie najwyzej jak w bazie +-10%.
10. **Wydajnosc**: rozdz. 11.3.
11. **Zapis i Undo**: zapis i wczytanie daja te same wyniki; stary zapis bez arm_ks wczytuje sie bez zmian; Plan -> Off nic nie zmienia; inny zapis z kluczem nic nie robi;
    Undo przywraca stawki bazowe z dokladnoscia 1% i zajecia wiosek z D-15; arm_ks <= 150 KB.
12. **Plan na sucho**: kazdy towar rynku w 0,95-1,05 albo na liscie wyjatkow; w kazdej krainie zboze wlasne + dowoz w zasiegu z przewoznikiem >= 0,95 potrzeby, a jesli nie - plan "kraina".

---

## 15. Ryzyka

1. **Ceny gry nie pasuja do pracy** (rozdz. 3.4) - po starcie zboze drozeje, oliwki, ryby i welna tanieja, a A7 bedzie naciskac na zmiany. Lekarstwo: hamulec Jeffa,
   start w pasmie, log nacisku, pozniej ewentualnie KS-W.
2. **Przewoz x10** (rozdz. 5) - najwiekszy warunek. Lekarstwo: gradient, zasieg, statki, kontrola korytarzy, plan "kraina", podloga Famine, bramka.
3. **Widoczna zmiana w Ironmanie**: 39% wiosek pobocznych zmienia zajecie, we wsiach glownych 3/4 rak robi co innego niz typ; wina x23, oliwek x31 na wozach.
   Lekarstwo: tydzien Planu, rampa, typ i ikona zostaja do decyzji wedlug PO A9, Undo.
4. **Tabele maja widelki 2-3 razy** (P0, K, ha). Zmienia to podzial rak i ceny rownowagi, nie zasade. Przed Apply wrazliwosc x0,7 i x1,3 dla ryb, zboza, bydla, owiec.
5. **Wozy wsi x5** (75 tys. szt./d wobec 15 tys. sprzedawanych dzis). Lekarstwo: sufit magazynu, udzwig wozu (122), BulkUnit dla zboza, prog w tescie.
6. **Wyspy** (Zelazne, Skagos, Letnie) bez statkow maja polowe ziarna - zalezne od KS-H.
7. **Zima 3-5 lat** wkrotce po starcie: odbior wobec profilu zimy; glod Polnocy po wyczerpaniu spichlerzy to sprawa Famine.
8. **Koszyki lowcow** (Wolni Ludzie, Skagos) sa zbozowe - do poprawy w KS-K.
9. **1:1 x sigma** zawyza "ogon" wojska - swiadoma decyzja (rozdz. 4.7).
10. **Kupcy** moga nie miec gotowki na zapas i przewoz - kontrola w rozdz. 9 i stop rampy.
11. **Brak scipy** w srodowisku - rownowage licze tatonnement (bez programowania liniowego); zbieznosc sprawdzona testem 2%.
12. **Nic na zywym zapisie**: KS-P dopiero po wgraniu DLL po tescie i nigdy wtedy, gdy Jeff gra.

---

## 16. Decyzje przyjete bez pytania (kazda z wylacznikiem)

| decyzja | dlaczego | wylacznik |
|---|---|---|
| regula wyboru = dochod rodziny po K i rencie ziemi | zasada 1 Jeffa i recenzja | KS Off |
| premia mapy 1,25 | prog Jeffa; ten sam bilans co 1,05, mniej zmian | StartCalibrationMapInertia |
| pola mieszczan (chlopi 1, niewolni 0,5, wyrobnicy 0,2) | historia agro-miast; bez nich Essos -6% | StartCalibrationTownFields |
| zloza, las, oazy, przyprawy = regale | glina 80 d/dobe przy cenach gry | StartCalibrationRegale |
| ceny bazowe bez zmian (KS-W pozniej) | Ironman i widelki tabel | - |
| kiesy ludu bez zmian | krok 1 przenosilby od biedoty do kupcow | - |
| 1:1 x sigma zostaje | gra zabiera te sztuki z targu | - |
| lepszy stol R8b najwyzej +10% | jawna konsumpcja zamiast ciecia | StartCalibrationTableCap |
| D w jesieni, zima bez normalizacji | pory wieloletnie | - |
| podloga Famine i bramka krainy | zgonow nie da sie cofnac | tylko caly KS |

---

## 17. Pliki

Model v2 (tylko odczyt, uruchom `python -I ks_raport2.py`, zmienne KS_RYNEK = strefa / swiat / wyspy / kraina, KS_INERT, KS_KOSZ, KS_REGULA = dochod / pasmo, KS_DWORY):
`C:/Users/GAME/AppData/Local/Temp/claude/C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord/6ba92574-15a3-4209-a065-ed325b84b1b4/scratchpad/roz/kalibracja/`
- `ks_model2.py` (regula, ziemia z rastra, pola mieszczan, regale, lepszy stol), `ks_tabele2.py` (P0 v2, K, ha, lowy), `ks_raport2.py` (krainy, handel, rynek, ceny, pieniadze),
  `ks_kiesy2.py` (kiesy, tylko krok 1, do wgladu); stare v1: `ks_model.py`, `ks_tabele.py`, `ks_raport.py`, `ks_kiesy.py`.
- Wynik zalecany: `ks_raport_v2_strefa_kosz_in1.25.txt`, `ks_bilans_...`, `ks_krainy_...`, `ks_okregi_...`, `ks_handel_...`, `ks_ceny_...`, `ks_dochod_...` (ten sam przyrostek).
- Warianty: `*_v2_strefa_kosz_in1.05.*`, `*_v2_swiat_kosz_in1.25.*`, `*_v2_wyspy_kosz_in1.25.*`, `*_v2_strefa_in1.25.*` (bez KS-K), `*_v2_kraina_kosz_in1.25_lowy.*`,
  `*_v2p_strefa_kosz_in1.25.*` (pasmo A7 przy cenie 1); logi przebiegow w `runs/`.
- Dane: `roz/zycie` (towary.tsv, ludnosc.tsv), `roz/geo-dane` (trasy), `roz/inwestycja` (teren_wiosek.tsv, symulacja_upraw.tsv), raster
  `.../3cf3e0ac-.../scratchpad/dzien-6/wioski-na-mapie/a-teren/` (teren.npz, flora.npz, okregi.npz, skala.json), logi Jeffa `D:/Backup-Bannerlord/logi-jeffa/2026-10-16/`.
- Projekty powiazane: `docs/PROJEKT-ZYCIE-CODZIENNE-2026-10-16.md` (ZC0-ZC8), `docs/PROJEKT-INWESTYCJE-OKREGU-2026-10-16.md` (PO, A7, dopisek Jeffa).
