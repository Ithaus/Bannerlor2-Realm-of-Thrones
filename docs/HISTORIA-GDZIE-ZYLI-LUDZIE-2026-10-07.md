# HISTORIA: gdzie zyli ludzie w sredniowieczu i ilu z nich liczyc do wiosek na mapie (07.10)

**Pytanie Jeffa (07.10):** "nie wszyscy ludzie zyja w wioskach - sa ludzie w miastach, podrozni, w samotniach, klasztorach itp.
... sprawdz, jaki byl rozklad ludzi w sredniowieczu, gdzie, co i jak zyli ludzie". Zapisane do wersji wiosek wedlug ludnosci
(v3, PROJEKT-WIOSKI-NA-MAPIE-2026-10-07.md): liczba wiosek liczona tylko z ludzi mieszkajacych we wsiach; nie licza sie
mieszkancy miast, podrozni, pustelnicy, klasztory i septy, mlynarze, karczmarze, samotne zagrody ani ludzie w lesie; udzial
zalezy od krainy.

**Jak powstal.** Cztery badania (1: Anglia, Francja pn., Niderlandy, Niemcy; 2: Polnoc i wyspy - Szkocja, pln. Anglia, Walia,
Irlandia, Skandynawia; 3: Poludnie i Wschod - Wlochy, Hiszpania, Prowansja, Bizancjum, Egipt, Lewant, Maghreb, step; 4: lore
Martina i mapa ROT), potem synteza z krytyka: sprawdzenie liczb miedzy badaniami i u zrodla (rozdz. 3), jedna tabela krain
(rozdz. 4), wniosek dla gry (rozdz. 6). **Tylko dokument:** kod, gra i sejw nie ruszane; nic nie zbudowane, nic nie zacommitowane.

**Oznaczenia.**
- [Z] = liczba ze zrodla (albo prosty rachunek na niej, "rach."); [Z-w] = ze zrodla wtornego (wiki, blog, recenzja);
  [S] = szacunek (tej syntezy albo badania); [L] = lore Martina (ksiazka i rozdzial za przypisem AWOIAF);
  [KOD] / [ROT] = odczyt kodu Armoury / pliku mapy ROT; [SPR] = sprawdzone ponownie u zrodla w tej syntezie.
- Wszystkie udzialy w tabelach to procent CALEJ ludnosci krainy, chyba ze napisano "procent okregu".
- SCR = `C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\3cf3e0ac-5529-4b68-a794-0edec69cfda7\scratchpad\dzien-6`
  (notatki badan: `SCR\gdzie-zyli-ludzie`); rachunek syntezy: `C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\fa2fd7a6-a098-46e5-8d1b-3c099c38c1f8\scratchpad\gdzie-zyli\synteza.py`.

---

## 0. W skrocie

1. **Historia (ok. 1300, przed Czarna Smiercia):** na wsi zylo 80-90% ludzi, ale nie wszyscy na wsi mieszkali "we wsi".
   - Miasta: Anglia 15-20% [Z], Szkocja 7% [Z], Walia 9% [Z], Wlochy 15% w miastach od 10 tys. [Z], Flandria 35-40% [Z-w],
     Andaluzja 47.5% w 32 duzych osadach (glownie agromiasta rolnikow) [Z].
   - Klasztory: zakonnicy 0.25-0.55% ludnosci (Wyspy Brytyjskie ok. 1290 [Z SPR]), z konwersami i sluzba ok. 0.5-1.2% [S];
     Bizancjum ok. 1% [Z]. Pustelnicy i rekluzi: setne czesci procenta [Z].
   - Dwory, zamki i ich domownicy 0.5-2% [S]; podrozni i wloczedzy 1-2%, na szlakach karawan 3-5% [S]; mlyny, karczmy i kuznie
     poza wsia ok. 0.5-1.5% [S]; ludzie lasu (weglarze, lesnicy, smolarze) 0.2-0.5% [S].
   - **Najwieksza grupa poza wsia to samotne zagrody** - i to zalezy od krainy: w pasie wsi skupionych (Midlands, Basen Paryski)
     2-5% ludzi wsi, w krainach wzgorz i przysiolkow (Devon, Walia, Cumbria, Highlands) 20-35%, w Norwegii 45-65%, na Islandii
     prawie 100% [S na opisach Z].
2. **Ksiega 108 (PopulationLaw) liczy w okregu wszystkich poza miastami z mapy** [KOD]: wsie, przysiolki, zagrody, miasteczka
   targowe spoza mapy, klasztory, pustelnikow, mlyny, karczmy, ludzi lasu, domownikow zamkow (zamki maja w ksiedze 0 ludzi),
   podroznych i koczownikow. Poza okregiem sa tylko zolnierze (konto "w sluzbie") i bandy (pula wyrzutkow).
3. **Do wiosek na mapie liczyc wsie skupione i przysiolki (3 i wiecej domow ze wspolnym polem)** - nic wiecej. Udzial w okregu
   (rozdz. 4 i 6.3): Reach 82%, Dorzecze 77%, Dorne 74%, Zatoka Niewolnicza i Volantis 73-75%, Zelazne Wyspy 72%, Dolina,
   Ziemie Zachodnie i Qarth 70%, Wolne Miasta 66%, Lhazar 65%, Polnoc 64%, Ziemie Burzy i Smocza Skala 63%, okolice KL 60%,
   Braavos i Lorath 60%, Dar Nocnej Strazy 56%, Skagos 35%, Za Murem 25%, Dothrakowie 3%.
4. **Wzor:** liczba osad okregu `n = max(1, round(L0 x W_kultury / 250))` zamiast `round(L0 / 250)`. Reszta wzoru zostaje
   (`S_v = L / n`): kazda osada niesie swoj udzial zagrod, mlynow i karczm okolicy, wiec rabunek pustoszy tyle samo ludzi na dobe
   (R x N), co dzis - zmienia sie tylko liczba osad i ludzi na osade (Reach 305, Polnoc 391, Za Murem 1 000).
5. **Skutek** (rachunek na okregach d-ksiegi, rozdz. 6.4): osad na swiecie 168 145 -> 117 294 (-30%); mediana okregu 243 -> 159;
   plan wiosek bez sufitu terenu 7 002 -> 4 878. Postawionych wiosek ubedzie mniej niz 30%, bo w wiekszosci okregow liczbe wiosek
   tnie dzis teren (v2: plan 6 778, po sufitach 2 462, postawione 1 483) - do przeliczenia generatorem v2 (ok. 25 s).
6. **Osobne obrazki** (septrie, gospody, mlyny): historycznie za duzo, zeby rysowac kazde (mlyn na ok. 480 ludzi, klasztor
   wiejski na ok. 9 tys.). Zalecenie: nie teraz; do rozwazenia pozniej jedna septria na okreg Wiary Siedmiu i nazwy "... Inn"
   dla wiosek na skrzyzowaniach (rozdz. 6.6).

---

## 1. Kogo liczy ksiega ludzi okregu (kod Armoury, galaz robocza)

- `Armoury/src/PopulationLaw.cs` (tabela `Table`, `Calibrate`, `PeopleOf`) [KOD]:
  - ludnosc krainy to CALA ludnosc (docs/POPULACJA-WESTEROS-ESSOS.md: "estymacje z wielkosci armii");
  - miasta z mapy dostaja staly udzial `Urban` (Polnoc 0.08, Dorzecze 0.08, Reach 0.10, Dorne 0.08, Zachod 0.08, Dolina 0.06,
    Ziemie Burzy 0.05, Zelazne Wyspy 0.05, crownlands 0.75, dragonstone 0.05, Wolni Ludzie / Straz / Skagos / Dothrakowie 0,
    Volantis 0.25, Braavos 0.32, Norvos i Tyrosh / Myr / Lys / Zatoka 0.40, Pentos i Qohor 0.33, Lorath 0.31, Qarth 0.37,
    Sarnor, Ibben, Yi Ti 0.20, Wyspy Letnie 0.10), dzielony wedlug dobrobytu;
  - **cala reszta idzie do wsi gry wedlug hearth** (`hearth x k`); **zamki maja 0 ludzi** (`PeopleOf` zwraca 0).
- Paczka 108 (docs/paczki/108-ludzie-jednostka.md) i 113: konta "w domu / w sluzbie / wyrzutki / uchodzcy / polegli". Rekrut i
  wyrzutek wychodza z "w domu"; L0 projektu (PROJEKT-RABUNEK pkt 1) = "w domu" + uchodzcy.
- **Wniosek:** L0 okregu zawiera wszystkie grupy z pytania Jeffa: wsie i przysiolki, samotne zagrody, miasteczka targowe, ktorych
  nie ma na mapie, domownikow zamkow i dworow, klasztory i septrie, pustelnikow, mlynarzy, karczmarzy, przewoznikow, ludzi lasu,
  podroznych (kazdy ma gdzies dom), koczownikow. Nie zawiera: miast z mapy, wojska, band.
- BannerKings ma osobna ludnosc miast, zamkow i wsi (wlasny system BK); ksiega Armoury jej nie czyta i nie zmienia.

---

## 2. Historia: gdzie mieszkali ludzie ok. 1300

### 2.1 Kraje (% calej ludnosci)

| Kraj, okres | W miastach razem | W duzych miastach | Wsie i przysiolki | Samotne zagrody | Zakonnicy (bez sluzby) | Gestosc os./km2 | Zrodlo |
|---|---|---|---|---|---|---|---|
| Anglia ok. 1290-1300 | 15-20 [Z SPR] | ok. 5 w 10 tys.+ (15 miast) [Z]; 9 w 2 tys.+ [Z SPR] | 62-72 [S] | 6-12 [S] | 0.53 [Z SPR] | 30.5 [Z SPR] | Campbell 2008 tab. 1 i 16; Dyer 2002; Palliser 2000 |
| Anglia, pas wsi skupionych (Midlands, Central Province) | - | - | 85-95 ludnosci wiejskiej [S] | 2-5 ludnosci wiejskiej [S] | - | 30-40 [S] | Roberts i Wrathmell 2000 [Z SPR podzial] |
| Anglia, wyzyny pln. i zach. (Devon, Kornwalia, Cumbria, Pennin) | - | - | 55-80 ludnosci wiejskiej [S] | 19-33 [S] | - | 11-12 Cumberland, Westmorland [Z SPR] | Roberts i Wrathmell; Broadberry i in. 2011 |
| Szkocja ok. 1290 | 7 [Z SPR] (wiki: 10 [Z-w]) | 3 w 2 tys.+ [Z SPR] | niziny 70-80, gory 45-70 [S] | niziny 5-15, gory 25-50 [S] | 0.25 [Z SPR] | 10, niziny 26 [Z SPR] | Campbell 2008 |
| Walia ok. 1290 | 9 [Z SPR] | ponizej 1 w 2 tys.+ [Z SPR] | 50-70 [S] | 20-35 [S] | 0.40 [Z SPR] | 14 [Z SPR] | Campbell 2008; prawo Hywela: przysiolek najwyzej 9 domow [Z-w] |
| Irlandia ok. 1290 | 7 (kolonia ang. ok. 14) [Z SPR] | Dublin ok. 11 tys. [Z] | kolonia 55-75, gaelicka 40-65 [S] | 5-10 / 30-50 [S] | 0.44 [Z SPR] | 15 [Z SPR] | Campbell 2008 |
| Francja pn. 1328 | 12-18 [S] | Paryz ok. 200 tys. [Z-w] | wies bardzo skupiona, 80-90 ludnosci wiejskiej [S] | 2-5 ludnosci wiejskiej [S] | - | ok. 35 (7.7 ogniska/km2) [Z-w rach.] | Etat des paroisses 1328 (Lot 1929) |
| Flandria pol. XIV w. | ok. 36-40 [Z-w] | Gandawa 60 tys., Brugia 45 tys. [Z-w] | 40-60 ludnosci wiejskiej [S] | 20-30 ludnosci wiejskiej [S] | - | 72 w 1470 [Z-w] | Stabel 1997, Blockmans i Prevenier 1999 |
| Niemcy ok. 1300 | 10-20 z miasteczkami rolniczymi [S] | 4-6 [S] | 170 tys. osad, srednio ok. 80 ludzi [Z-w rach.] | pn.-zach.: zagrody i drubble 3-15 zagrod [Z-w] | - | - | Abel (za Luebke); LWL |
| Wlochy 1300 / 1400 | - | 15 / 9 w 10 tys.+ [Z SPR] | - | - | - | rejon Florencji 40 [Z] | Bosker i in. 2007 (Malanima) |
| Toskania 1427 (po zarazie) | ponizej 28 (11 miast) [Z] | Florencja 14 [Z] | - | mezzadria z domami w polu dopiero od XV-XVI w. [Z-w] | - | - | Herlihy i Klapisch-Zuber (za Brown Univ.) |
| Andaluzja ok. 1490 | 47.5 w 32 osadach od 1 000 vecinos [Z SPR] | Sewilla, Kordowa, Grenada | agromiasta rolnikow + wsie [S] | ok. 3 [S] | - | - | Flores Varela 2005 |
| Bizancjum XIV w. | 7-15 [S] | Konstantynopol, Tesaloniki | wsie (chorion) zwarte [S] | - | mnisi ok. 1 [Z] | 20-30, Macedonia 34 [Z] | Charanis 1971; Preiser-Kapeller 2010 |
| Egipt ok. 1340 | Kair 5-14 [Z] | - | wsie na kopcach nad wylewem, zwarte [Z-w] | prawie 0 [Z-w] | - | - | Black Death in the Middle East (wiki) |
| Norwegia ok. 1300 | ok. 4 (15 tys. z 350 tys.) [Z-w] | Bergen ok. 7 tys. | gromady domow na gospodarstwie (klyngetun) [Z-w] | 45-65 [S] | 0.2-0.3 [S] | ok. 1 [Z-w] | Benedictow; Oye 1999 |
| Islandia | 0 [Z] | 0 | 0 (brak wsi do XVIII w.) [Z] | prawie 100 [Z] | 9 klasztorow w calym okresie [Z-w] | ok. 0.5 [S] | spis 1703: 50 358 ludzi, 8 191 domostw [Z] |
| Step (Mongolowie, Kumanowie) | 0 | - | 0 | grupy 2-6 jurt, duzy oboz tylko u wodza [Z-w] | - | ponizej 1 [S] | Cartwright 2019; WWF |

### 2.2 Grupy poza wsia (wszedzie male - poza zagrodami)

- **Klasztory.** Wyspy Brytyjskie i Irlandia ok. 1290: ok. 1 500 domow zakonnych, ok. 30 000 (+-2 700) zakonnikow i zakonnic;
  Anglia 64-77% z nich, czyli 19-23 tys. = 5.3 na 1 000 ludzi; Szkocja 2.5, Irlandia 4.4 na 1 000 [Z SPR Campbell tab. 1 i 16].
  Srednio na dom w Anglii: benedyktyni 14, kanonicy augustianscy 11, **cystersi z konwersami 74**, dominikanie 32, franciszkanie 29,
  mniszki 22 [Z SPR]. Bracia zebrzacy siedza w miastach; cystersi, kartuzi i czesc kanonikow z grangiami poza miastem i wsia [S].
  Z czeladzia i sluzba (ok. 1:1) wychodzi 0.5-1.2% ludnosci [S], z tego w okregu (poza miastami z mapy) ok. polowa.
- **Ksieza parafialni mieszkaja we wsi** i licza sie do wsi: Anglia 8 230 beneficjow (1291), czyli 1 na ok. 490 ludzi;
  Szkocja 960 parafii, 1 na 835 ludzi [Z SPR Campbell]. W Westeros: septon wiejski mieszka we wsi; mala wies odwiedza septon
  z sasiedniej dwa razy w roku [L TSS].
- **Pustelnicy, rekluzi:** w Anglii najwyzej ok. 200 naraz w XIII w. [Z Warren 1985], czyli ponizej 0.01%.
- **Dwory, zamki, holdfasty:** 1 000-2 000 wielkich domostw ze stala sluzba (Woolgar 1999) [Z]; ok. 6 000 miejsc z fosa, glownie
  1250-1350, najczesciej w krainach rozproszonych [Z HE]; domownicy razem 0.5-1.5% [S], w krajach klanow i wiezy (Polnoc,
  pogranicze, Niemcy z 18-25 tys. zamkow) do 2% [S].
- **Podrozni i wloczedzy:** 1-2% [S]. Kotwice: pod Calais 1347 do 32 tys. zolnierzy i 24 tys. marynarzy, ok. 1.2% w roku
  najwiekszej wyprawy [Z rach.]; 1569: 13 000 "ludzi bez pana", ok. 0.4% [Z]; Florencja 1338: 1 500 obcych, podroznych i
  zolnierzy na ok. 90 000 [Z-w], czyli 1.7% miasta. Na szlakach karawan (Qarth, Dorne) 3-5% [S].
- **Mlyny, karczmy, kuznie, przewozy:** ok. 10 000 mlynow w 1300 (Langdon 2004) = 1 na ok. 480 ludzi [Z]; 30-50% z nich poza wsia
  [S]; karczmy prawie zawsze w miescie albo we wsi przy drodze [S]. Razem poza wsia 0.5-1.5% [S].
- **Ludzie lasu** (weglarze, smolarze, lesnicy, pszczelarze, mysliwi): 0.2-0.5% [S]; las krolewski zajmowal duzo ziemi, ale staly w
  nim wsie, a rzemieslnicy lesni mieszkali we wsiach i przysiolkach na skraju [Z-w Birrell]. Banici i bandy w ksiedze sa osobno
  (pula wyrzutkow) - nie liczyc drugi raz.
- **Targ:** w XIII w. targi blizej niz 6 2/3 mili (10.7 km) uznawano za szkodliwe dla siebie [Z Letters]; w praktyce targ co
  8-15 km [S]. Miasteczek targowych 1 na ok. 7-8 tys. ludzi (Anglia 1377: ok. 540 miast, wiekszosc 500-2 000 ludzi [Z Tiller]).

### 2.3 Wielkosc wsi i odstepy (kotwice)

- Wies Anglii ok. 1300: zwykle 30-60 domow, 150-300 ludzi [S]; Wharram Percy ok. 30-40 domow [Z]; MSRG: wies od 6 gospodarstw [Z].
- Vill (wies z przysiolkami): 1334 ok. 14 000 miejsc w spisie podatkowym [Z SPR Glasscock], czyli ok. 270 ludzi na vill [S rach.]
  - prawie dokladnie osada projektu (250 ludzi).
- Odstep wsi: Leicestershire zwykle 1.2 mili (1.9 km) [Z Busby]; w pasie wsi skupionych 1.5-3 km [S]; Francja 1328 ok. 13.5 km2 na
  parafie, ok. 3.7 km miedzy kosciolami [Z-w rach.].
- Przysiolek: Devon 2-6 domow, ok. 10 osad na jedno miejsce z Domesday [Z]; fermtoun i clachan Szkocji 4-6 dzierzawcow z
  komornikami, 20-40 ludzi (Fife do 8, pn.-zach. Highlands 10-12 domow) [Z-w ScARF]; drubbel Westfalii 3-15 zagrod [Z-w].
- Alqueria Walencji 4-5 do 50-70 domow [Z]; 32 osady Andaluzji od 1 000 vecinos (ok. 4 tys.+ ludzi) [Z SPR].
- Lore: wies przy gospodzie na rozstajach ma ok. 50 bialych domow i maly sept [L AGOT 28] - ok. 250 ludzi [S].

---

## 3. Krytyka: zgodnosc badan i co poprawiono

| Sprawa | Badania | Rozstrzygniecie |
|---|---|---|
| Co to przysiolek | B1: 2-20 domow; B2: 3-9; B4: wies "od ok. 5 domow" (mniejsze do zagrod) | **Przysiolek = 3 i wiecej domow ze wspolnym polem** (fermtoun, clachan, baile, township) - to wspolnota wiejska; 1-2 domy = zagroda. B4 zanizal przez to wsie Polnocy i Burzy. |
| Mianownik "procent okregu" | B1 liczyl "bez miast" i wyrzucal tez miasteczka targowe (Reach 87-93%); B4 odejmowal miasta i zamki | W ksiedze miasteczka spoza mapy i domownicy zamkow SA w okregu (rozdz. 1). Liczymy W = (wsie + przysiolki) / (100 - miasta z mapy). Reach po poprawce 82%. |
| Polnoc | B2: niziny Szkocji 70-80% calosci (ok. 84% bez miast), gory 45-70%; B1: 60-75% z przysiolkami; B4: 48% okregu, ludzie lasu i gor 14% | B4 przecenia ludzi lasu i gor: klany gorskie wystawia 2-3 tys. wojownikow [L ADWD wg AWOIAF], czyli kilkadziesiat tysiecy ludzi z 3 mln (ok. 1%); do tego crannogmeni i lesnicy - razem ok. 6%. Przyjete 64% (55-72). |
| Ziemie Burzy | B1: 74-84%; B4: 51% | Lore "ludnosc rozproszona" [L RPG 2005, pol-kanon], deszczowe lasy, chaty [L TWOIAF]; analog Devon i Weald (przysiolki licza sie do wsi). Przyjete 63% (52-72). |
| Zakonnicy Anglii | B1: 17 500 (zrodlo wtorne, Knowles i Hadcock); B2: 30 tys. na Wyspach | Sprawdzone u Campbella (tab. 1, oparta na Knowles i Hadcock): 19-23 tys. w Anglii ok. 1290, 0.53% [Z SPR]. Liczba 17 500 odrzucona jako niesprawdzona. |
| Miasta Anglii | B1: Dyer 20%, Palliser do 15%; B2: Campbell 15% | Zakres 15-20% (Campbell 15% sprawdzony). |
| Miasta Szkocji | Campbell 7%, wiki 10% | Campbell 7% [SPR]; wiki jako gorna granica. |
| Ludnosc Anglii 1290 | Campbell 2008: 4.0 mln; Broadberry i in. 2011: 4.75 mln | Udzialy od tego nie zaleza; rachunki na osobach (vill 270 ludzi) na 3.7-4.0 mln ludzi wsi. |
| Toskania (B3) | DM 20% "miasta od 10 tys." | W 1427 tylko Florencja miala ponad 10 tys. (14%) [Z]; reszta to miasta male. To tez stan PO zarazie, a domy w polu (mezzadria) to XV-XVI w. - Toskania sluzy tylko za wzor winnic Arbor i willi pod Wolnymi Miastami, nie za wzor calego Reach. |
| Palermo "do 12% niewolnikow" (B3) | tylko z wyniku wyszukiwarki | Odrzucone. Zostaje: 4-5% niewolnikow w miastach pn. Wloch XV w., Genua 7 223 w 1381 [Z-w Barker]. |
| Paryz, Etat 1328 (B1) | 23 671 parafii, 2 469 987 ognisk, Paryz 61 098 ognisk | Nie udalo sie potwierdzic u zrodla (fr. wiki 404); zostaje jako [Z-w], uzyte tylko jako kotwica odstepu wsi. |
| Northumberland 1290 -> 1377 | B2: 148 084 -> 30 389 | Sprawdzone w tab. 8B Broadberry i in. 2011 [SPR]. To rekonstrukcja modelowa (wojny + zaraza). |
| Lore (B4) | "speckled with tiny villages and holdfasts" (Polnoc), Reach "numerous and well-populated villages and towns", Zelazne Wyspy "seven out of every ten families are fisherfolk", Lhazar - pasterze owiec, trzy miasta | Sprawdzone w pobranym tekscie AWOIAF [SPR]. |
| ROT (B4) | 571 wsi, 97 miast, 130 zamkow; Polnoc 57 wsi, Reach 40 | Zgodne z PROJEKT-WIOSKI 2.8 i plikiem okregow d-ksiegi. Lhazar nie ma wlasnej kultury: wsie Akiser, Kamshar (zamek Hesh), Grazosh, Lhuza, Rhaja (miasto Lhazosh) maja kulture `ghiscari` [ROT SPR]. |
| Po Czarnej Smierci (B1) | -2-5 pkt | Westeros nie ma Czarnej Smierci; bierzemy stan ok. 1300 (pokoj, przed zaraza). |
| Garnizony, Nocna Straz (B2) | garnizony 0.1-0.2% nawet w wojnie | Wojsko jest w ksiedze na koncie "w sluzbie", nie w okregu - do wiosek i tak sie nie liczy. Domownicy zamku (sluzba, rodziny) - w okregu, kolumna DW. |

---

## 4. Tabela krain (% calej ludnosci krainy)

**Kolumny:**
- **DM** - miasta z mapy (= udzial `Urban` z PopulationLaw, poza okregiem); **MM** - miasteczka targowe, ktorych nie ma na mapie;
- **WS** - wsie skupione (10 i wiecej domow; tu tez agromiasta rolnikow, osady gornicze i osady majatkow z niewolnikami);
  **PRZ** - przysiolki 3-9 domow ze wspolnym polem; **WS + PRZ = ludzie "we wsiach"**;
- **ZAG** - samotne zagrody (1-2 domy), samotne wieze i holdfasty bez wsi; **DW** - dwory, zamki, holdfasty: domownicy i sluzba
  (bez wojska); **KL** - klasztory, septrie, domy macierzyste poza wsia, z czeladzia (septoni wiejscy sa we wsi);
  **PU** - pustelnicy, rekluzi, "woods witches"; **PD** - podrozni i wloczedzy (kupcy, woznice, flisacy, pielgrzymi, rycerze
  wedrowni, septoni wedrowni, grajkowie, karawany);
- **LA** - las, gory, bagna, pustynia, koczownicy i ludzie na lodziach (lesnicy, weglarze, mysliwi, klany gorskie, crannogmeni,
  pasterze wedrowni, khalasary); **MK** - mlyny, karczmy, przewozy, kuznie i huty poza wsia;
- **W okregu** = (WS + PRZ) / (100 - DM) - procent ludzi OKREGU (L0) liczony do wiosek na mapie; w nawiasie zakres.
- Wszystko w wierszach [S] na kotwicach z rozdz. 2 i lore z rozdz. 5; kazdy wiersz sumuje sie do 100 (synteza.py).

| Kraina (kultura ROT) | DM | MM | WS | PRZ | ZAG | DW | KL | PU | PD | LA | MK | W okregu |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Polnoc (battania) | 8 | 1 | 15 | 44.25 | 22 | 2 | 0.2 | 0.05 | 1 | 6 | 0.5 | **64** (55-72) |
| Za Murem (freefolk) | 0 | 0 | 8 | 17 | 20 | 1 | 0 | 0.5 | 3 | 50.5 | 0 | **25** (15-35) |
| Nocna Straz / Dar (nightswatch) | 0 | 0 | 20 | 36.5 | 28 | 1 | 0 | 0.5 | 3 | 10 | 1 | **56** (40-65) |
| Dorzecze (river) | 8 | 5 | 56.75 | 14 | 7 | 2 | 1.2 | 0.05 | 2.5 | 2 | 1.5 | **77** (70-83) |
| Dolina (vale) | 6 | 3 | 47.75 | 18 | 12 | 2 | 1.2 | 0.05 | 2 | 7 | 1 | **70** (62-76) |
| Ziemie Zachodnie (vlandia) | 8 | 4 | 40.45 | 24 | 13 | 2 | 1 | 0.05 | 2 | 4 | 1.5 | **70** (62-78) |
| Reach (reach) | 10 | 5 | 63.75 | 10 | 4 | 1.5 | 1.2 | 0.05 | 2 | 1.5 | 1 | **82** (75-88) |
| Ziemie Burzy (stormlands) | 5 | 3 | 25 | 35.1 | 22 | 2 | 0.8 | 0.1 | 1.5 | 4.5 | 1 | **63** (52-72) |
| Ziemie Korony: okolice KL (crownlands) | 75 | 3 | 10 | 5 | 2.48 | 0.5 | 0.5 | 0.02 | 1.5 | 1.5 | 0.5 | **60** (52-70) |
| Ziemie Korony: Smocza Skala, Pazur, Massey's Hook (dragonstone) | 5 | 3 | 35 | 25 | 18 | 2.5 | 0.8 | 0.1 | 2 | 7.6 | 1 | **63** (55-72) |
| Dorne (aserai) | 8 | 3 | 56 | 12 | 4 | 2 | 0.8 | 0.1 | 2.5 | 11 | 0.6 | **74** (65-82) |
| Zelazne Wyspy (sturgia) | 5 | 2 | 34 | 34 | 16 | 3 | 0.2 | 0.3 | 2 | 2 | 1.5 | **72** (60-80) |
| Wolne Miasta: Pentos, Qohor, Norvos, Tyrosh, Myr, Lys (wzor: Pentos) | 33 | 3 | 34 | 10 | 8 | 1.5 | 1 | 0.1 | 4 | 4 | 1.4 | **66** (56-75) |
| Volantis (volantine) | 25 | 3 | 46 | 10 | 6 | 1.5 | 1 | 0.1 | 4 | 2 | 1.4 | **75** (65-85) |
| Braavos i Lorath (empire, nord) | 32 | 3 | 26 | 15 | 12 | 1 | 1 | 0.1 | 4 | 4.5 | 1.4 | **60** (50-70) |
| Zatoka Niewolnicza (ghiscari) | 40 | 2 | 36 | 8 | 3 | 2 | 0.5 | 0.05 | 3 | 5 | 0.45 | **73** (65-85) |
| Qarth (qartheen) | 37 | 2 | 38 | 6 | 3 | 1.5 | 0.8 | 0.2 | 5 | 6 | 0.5 | **70** (60-80) |
| Dothrakowie (khuzait) | 0 | 0 | 2 | 1 | 0 | 0 | 0.5 | 0 | 2 | 94.5 | 0 | **3** (1-6) |
| Lhazar (wsie ghiscari przy Hesh i Lhazosh) | 10 | 3 | 48.5 | 10 | 3 | 1 | 1 | 0.1 | 2 | 20.5 | 0.9 | **65** (55-72) |
| Skagos (skagosi) | 0 | 0 | 5 | 30 | 20 | 1 | 0 | 0.5 | 0.5 | 43 | 0 | **35** (20-45) |

Uwagi do tabeli:
- DM jest wziete z ksiegi, nie z historii. Tam, gdzie ksiega daje miastom wiecej niz lore (Polnoc 8%, choc wiele "miast" ROT na
  Polnocy to w ksiazkach zamki: Dreadfort, Deepwood Motte, Last Hearth, Karhold [L]), miasteczka MM sa mniejsze, zeby razem
  zgadzalo sie z analogiem (Szkocja 7-10%).
- Lhazar ma w tabeli DM 10 (miasta Lhazaru), choc ksiega liczy jego miasta razem z Zatoka (`ghiscari` 0.40) - liczy sie tylko
  stosunek wsi do reszty, ktory od tego nie zalezy.
- Krainy spoza listy Jeffa (do kodu, rozdz. 6.3) maja wartosci robocze, niebadane osobno.

---

## 5. Kraina po krainie: analog, uzasadnienie, wielkosc wsi i odstep

Wielkosci i odstepy to [S] na kotwicach z rozdz. 2.3; "odstep" = miedzy sasiednimi osadami tej klasy.

- **Polnoc** - analog: Szkocja (niziny Lothian, Fife, Angus wokol White Harbor i Barrowlands; Highlands w gorach), Cumbria i
  Pennin (wzgorza Wilczego Lasu), wybrzeze jak Norwegia (Stony Shore, Bear Island).
  Lore: kraina "speckled with tiny villages and holdfasts" [L TWOIAF The North, SPR]; zagrodnicy, lesnicy i mysliwi nawet w glebi
  Wilczego Lasu [L ASOS 9]; male farmy i wsie wokol holdfastow przy trakcie [L AGOT 13, 70]; klany w wysokich dolinach [L ADWD];
  crannogmeni w plywajacych wioskach z trzciny [L ACOK 21]. Wiara starych bogow - prawie bez klasztorow (KL 0.2: septy White Harbor).
  Wies: przysiolek-fermtoun 4-8 domow (20-50 ludzi), wies przy holdfascie 15-40 domow; przysiolki co 0.5-2 km, wsie co 5-10 km;
  gestosc 5-15 os./km2 (Szkocja 10, niziny 26 [Z]). Zima czesc ludzi schodzi do winter town [L AGOT 37] - liczyc stan letni.
- **Za Murem** - analog: Islandia i Laponia (zagrody, obozy) oraz gaelicka Irlandia i Highlands (przysiolki klanow).
  Lore: wsie (Whitetree), hale wodzow, samotne siedziby (Craster), polkoczownicy; Thennowie w dolinie [L]. Wies 5-30 domow,
  odstep dziesiatki km, gestosc ponizej 1. Polowa ludzi w ruchu (LA 50.5).
- **Nocna Straz / Dar** - analog: pogranicze Northumberland po wojnach szkockich (1290 -> 1377: 148 tys. -> 30 tys. [Z SPR]),
  rozproszone zagrody i wieze. Lore: Dar wraca do dziczy, Mole's Town w 3/4 pod ziemia [L]. Wies 5-20 domow, co 5-15 km;
  gestosc 1-3. Bracia Strazy sa w sluzbie, nie w okregu.
- **Dorzecze** - analog: Midlands z polami otwartymi i doliny rzek (Flandria bez wielkich miast), zachodnie wzgorza jak Marchia
  Walijska. Lore: "rich, fertile and populous" [L SSM 1200, SPR]; brak wielkiego miasta, sa miasteczka [L TWOIAF]; handel rzekami,
  barki i lodzie rybackie [L ASOS 1, 11]; w zachodnich wzgorzach wsie i holdfasty mniejsze i rzadsze [L ACOK 9]; liczne gospody
  (Crossroads Inn, Inn of the Kneeling Man) i septria 44 braci [L ASOS 39]. Wies 30-80 domow (150-400 ludzi), nad rzeka wieksze;
  co 2-3 km; targ co 10-15 km; gestosc 25-35.
- **Dolina** - analog: zyzna dolina jak Vale of York (wsie skupione), gory Ksiezycowe jak Highlands i Walia. Lore: klany gorskie
  nie sluchaja Orlego Gniazda i napadaja wsie [L] - LA 7 (klany w gorach, gdzie wioska nie stanie). Wies w dolinie 30-60 domow co
  2-3 km, w gorach przysiolki 3-8 domow; gestosc dolina 25-30, gory 2-5.
- **Ziemie Zachodnie** - analog: Devon i Kornwalia (wzgorza, cyna), gory kruszcowe Niemiec (osady gornicze), niziny pod
  Lannisportem. Lore: kopalnie zlota i srebra, zyzne pola, troche rybolowstwa [L]. Osady gornicze 10-40 chat licza sie do wsi.
  Wies 20-50 domow w dolinach, co 2-4 km; gestosc 15-25.
- **Reach** - analog: Midlands (Central Province Robertsa i Wrathmella) i Basen Paryski - wsie skupione z polami otwartymi;
  winnice Arbor jak Toskania (wiecej domow w polu). Lore: "most fertile part of Westeros", "numerous and well-populated villages and
  towns" [L TWOIAF, SPR]; serce Wiary - septy, septrie i domy macierzyste Merle'a I [L] (KL 1.2); Roseroad i pielgrzymi do Oldtown
  (PD 2). Wies 40-100 domow (200-500 ludzi), nad Manderem do 150-200; co 1.5-3 km; targ co 8-12 km; gestosc 30-45.
- **Ziemie Burzy** - analog: Devon, Kornwalia i Weald (przysiolki, zagrody, las i pastwiska). Lore: ludnosc rozproszona, ok. 30
  tys. wojska [L RPG 2005, pol-kanon]; porosniete mchem chaty w deszczowych lasach [L TWOIAF]. Przysiolki 3-10 domow co 0.5-1.5 km,
  wsie 20-40 domow co 3-5 km; gestosc 15-20.
- **Ziemie Korony: okolice KL** - analog: okolice Londynu i Paryza (Home Counties, Ile-de-France): duze wsie przy traktach,
  ogrody targowe, gospody, woznice. Lore: wzdluz traktu male wsie, zatloczone miasteczka targowe i holdfasty [L ACOK 5]; w Kingswood
  "lesny lud" [L AFFC 30]. Wies 50-100 domow wzdluz traktow, co 2-3 km; gestosc wokol KL 40-60.
- **Ziemie Korony: Smocza Skala, Pazur, Massey's Hook** - analog: wybrzeze Kornwalii i Bretanii, wyspy: wsie rybackie i przysiolki,
  na Pazurze sosnowe lasy i bagna (LA 7.6). Wies 20-40 domow na brzegu, co 2-5 km; gestosc 10-20.
- **Dorne** - analog: Andaluzja (47.5% w duzych osadach, agromiasta rolnikow [Z SPR]), huerta Walencji, oazy Maghrebu.
  Lore: najmniej ludne krolestwo [L AFFC 40]; zyzne tylko nad rzekami, studnie strzezone, Panowie Studni [L TWOIAF]; "kamienni"
  w Czerwonych Gorach, "piaskowi" na pustyni i w dolinach rzek [L ASOS 38]; Sieroty Greenblood na lodziach [L AFFC 21] (LA 11 z
  koczownikami i lodziami). Agromiasteczka i duze wsie 100-500 domow nad rzekami i przy studniach (licza sie do wsi - poziom 3
  wioski), co 5-15 km wzdluz rzeki, miedzy nimi pustka; gestosc w dolinach 40-80, na pustyni ok. 0.
- **Zelazne Wyspy** - analog: Orkady i Szetlandy (townships wielodzierzawcze 70-85% [S na Z]), zachodnia Norwegia, kopalnie
  Kornwalii. Lore: "seven out of every ten families are fisherfolk" [L TWOIAF, SPR]; chuda, kamienista gleba [L ACOK 24]; thralle w
  kopalniach zelaza, olowiu i cyny [L]. Wsie rybackie 15-40 domow na brzegu co 2-5 km, zagrody na wzgorzach; gestosc 8-15.
- **Wolne Miasta** (Pentos, Qohor, Norvos, Tyrosh, Myr, Lys) - analog: contado Toskanii i Lombardii, Prowansja (zwarte wsie na
  wzgorzach [Z-w]). Lore: Norvos - tarasowe pola i biale wsie [L AGOT 3]; w Pentos na majatkach "wolni sludzy", w Lys, Myr i Tyrosh
  3 niewolnikow na 1 wolnego [L]. Osady majatkow licza sie do wsi. Wies 30-100 domow, osady majatkow 20-60 chat, wille (ZAG);
  co 2-4 km w promieniu 30-50 km od miasta; gestosc pod miastem 30-60.
- **Volantis** - jak wyzej, ale wiecej wielkich majatkow niewolniczych nad Rhoyne (5 niewolnikow na 1 wolnego [L]): wiecej osad
  majatkow, mniej zagrod.
- **Braavos i Lorath** - analog: Wenecja i dogado (laguna, ok. 120 tys. w miescie, 160 tys. z dogado [Z-w]), wyspy; Braavos bez
  niewolnikow [L]. Wiecej przysiolkow rybackich, wzgorz z lasem i willi niz duzych wsi.
- **Zatoka Niewolnicza** - analog: latyfundia Sycylii i Andaluzji oraz pas nawadniany Egiptu i Lewantu. Lore: Astapor sprzedaje
  tez niewolnikow do pracy w polu [L ASOS 27]. Osady majatkow 50-300 niewolnikow i wsie 50-150 domow przy rzekach i brzegu; co 3-6
  km; gestosc w pasie nawadnianym 40-80. Niewolnik domowy liczy sie do miasta (DM), polowy do osady majatku (WS).
- **Qarth** - analog: Egipt (wsie na kopcach, prawie bez zagrod [Z-w]), oazy, Lewant; karawany (PD 5). Wsie ogrodowe i oazowe
  50-200 domow przy brzegu i oazach, co 3-10 km; Czerwone Pustkowie puste.
- **Dothrakowie** - analog: step Mongolow i Kumanow: grupy 2-6 jurt, duze obozy tylko u wodza [Z-w]. Lore: Vaes Dothrak to
  jedyna stala osada; khalasar Drogo ok. 40 tys. jezdzcow [L]. Wsi brak; WS + PRZ 3% to niewolnicy i osiadli przy Vaes Dothrak [S].
- **Lhazar** - analog: pasterskie wyzyny Lewantu i Anatolii: wsie z gliny ze swiatynia, pasterze z owcami sezonowo na pastwiskach
  (LA 20.5). Lore: "a people of sheepherders", trzy miasta Hesh, Lhazosh, Kosrak [L, SPR]. Wies 40-150 domow przy rzece i studni,
  co 5-10 km; gestosc 5-15.
- **Skagos** - analog: Hebrydy i Highlands sprzed clachanow (Dodgshon 1993 [Z-w streszczenie]): przysiolki i zagrody, duzo gor.

---

## 6. Wniosek dla gry

### 6.1 Kto liczy sie do wiosek na mapie (osad)

- **Tak:** ludzie wsi skupionych i przysiolkow (3 i wiecej domow ze wspolnym polem), razem z ich septonem, kowalem i karczma
  stojaca we wsi; agromiasta rolnikow (Dorne, Lhazar); osady gornicze (Zachod, Zelazne Wyspy); osady majatkow z niewolnikami
  (Essos). To sa skupiska z polami i drogami, takie jak obrazek wioski na mapie.
- **Nie:** miasteczka targowe spoza mapy, samotne zagrody i wieze, domownicy zamkow i dworow, klasztory, septrie i domy
  macierzyste, pustelnicy, podrozni, mlyny, karczmy i przewozy poza wsia, ludzie lasu, gor i bagien, koczownicy i ludzie na lodziach.
  Wojska i band nie ma w okregu (osobne konta) - nie odejmowac drugi raz.

### 6.2 Wzor (decyzja projektu)

- `n = max(1, round(L0 x W / 250))` - liczba osad z ludzi we wsiach (W z 6.3 wedlug kultury wsi gry).
- `S_v = L / n` **bez zmian** (PROJEKT-RABUNEK pkt 1): na jedna osade przypada 250 ludzi wsi i jej udzial zagrod, mlynow, karczm
  i klasztorow okolicy (razem 250 / W: Reach 305, Dorzecze 325, Polnoc 391, Ziemie Burzy 397, Za Murem 1 000, Dothrakowie 8 333).
  Historycznie tak bylo: najezdzca palil wies i wszystko, co stalo miedzy wsiami.
- Dlaczego tak: (1) ksiega zostaje jedynym zrodlem prawdy i nie potrzebuje nowego konta; (2) rabunek pustoszy tyle samo ludzi na
  dobe (R x N), wiec bilans ludzi i rabunku z PROJEKT-RABUNEK sie nie zmienia; (3) zmienia sie tylko liczba osad (menu, obrazki) i
  czas spalenia jednej osady (x 1/W).
- Dymek i menu: osada ma "about X souls" = ludzie wsi (n_w x 250 x L / L0); w menu okregu dodatkowa linia
  "Farms, mills, inns and septries around: about N souls" (N = L x (1 - W)) - zeby bylo widac, ze reszta okregu istnieje.

### 6.3 Lista do kodu: kultura wsi gry -> W

| Kultura | W | | Kultura | W | | Kultura | W |
|---|---|---|---|---|---|---|---|
| reach | 0.82 | | crownlands | 0.60 | | volantine | 0.75 |
| river | 0.77 | | dragonstone | 0.63 | | ghiscari | 0.73 (Lhazar 0.65) |
| aserai | 0.74 | | battania | 0.64 | | qartheen | 0.70 |
| sturgia | 0.72 | | nightswatch | 0.56 | | pentoshi, qohorik, norvos, tyroshi, myrish, lyseni | 0.66 |
| vale | 0.70 | | skagosi | 0.35 | | empire (Braavos), nord (Lorath) | 0.60 |
| vlandia | 0.70 | | freefolk | 0.25 | | khuzait | 0.03 |
| stormlands | 0.63 | | | | | sarnor 0.55, ibbenese 0.55, summer 0.70, yiti 0.75, valyrian 0.40 | robocze, niebadane |

- Lhazar: 5 wsi `ghiscari` przypisanych do Hesh (`castle_K2`) i Lhazosh (`ROT_town19`) - wyjatek po zwiazanej osadzie (0.65); bez
  wyjatku dostana 0.73 (roznica ok. 10 pkt na 5 wsiach, do przyjecia).
- Kultura spoza listy: W = 0.70 (srednia swiata, rozdz. 6.4) - nigdy 0 ani 1.
- Do MCM (po angielsku) wystarczy jeden mnoznik calosci "Share of district people living in villages (x)", domyslnie 1.0.

### 6.4 Skutek (rachunek na okregach d-ksiegi: `SCR\wioski-na-mapie\d-ksiega\okregi.csv`, synteza.py)

| Kraina | Okregi | Ludzi w okregach | Osad dzis (L0/250) | Osad z W | Plan wiosek dzis (n/24) | Plan z W |
|---|---|---|---|---|---|---|
| Reach | 40 | 7 200 000 | 28 809 | 23 622 | 1 203 | 976 |
| Wolne Miasta (6) | 74 | 4 710 008 | 18 840 | 12 434 | 787 | 522 |
| Volantis | 18 | 3 750 005 | 15 003 | 11 253 | 624 | 466 |
| Ziemie Zachodnie | 33 | 3 679 995 | 14 722 | 10 306 | 614 | 426 |
| Dorzecze | 43 | 3 219 994 | 12 881 | 9 922 | 528 | 421 |
| Dolina | 31 | 2 820 006 | 11 285 | 7 898 | 473 | 328 |
| Polnoc | 57 | 2 760 006 | 11 025 | 7 073 | 469 | 296 |
| Qarth | 11 | 2 519 999 | 10 080 | 7 052 | 416 | 291 |
| Ziemie Burzy | 31 | 2 374 996 | 9 496 | 5 991 | 397 | 255 |
| Braavos i Lorath | 25 | 2 251 992 | 9 009 | 5 403 | 375 | 222 |
| Zatoka Niewolnicza (bez Lhazaru) | 21 | 1 458 980 | 5 839 | 4 258 | 237 | 174 |
| Dorne | 38 | 1 379 996 | 5 514 | 4 080 | 229 | 175 |
| Smocza Skala, Pazur | 23 | 807 503 | 3 233 | 2 033 | 132 | 83 |
| Dothrakowie | 17 | 750 000 | 3 005 | 91 | 127 | 0 |
| Wyspy Letnie | 3 | 675 000 | 2 700 | 1 890 | 114 | 78 |
| Zelazne Wyspy | 28 | 475 001 | 1 902 | 1 366 | 84 | 56 |
| Lhazar | 5 | 341 020 | 1 365 | 885 | 55 | 35 |
| Ibben | 11 | 280 000 | 1 120 | 618 | 47 | 29 |
| Za Murem | 18 | 150 001 | 598 | 146 | 23 | 0 |
| okolice KL | 11 | 149 999 | 597 | 360 | 27 | 15 |
| Sarnor, Yi Ti, Valyria, Skagos, Dar Strazy | 33 | 280 003 | 1 122 | 613 | 41 | 30 |
| **Swiat** | **571** | **42 034 496** | **168 145** | **117 294** | **7 002** | **4 878** |

- Mediana okregu: 243 -> 159 osad (okreg-mediana 60 652 ludzi). Okregow z planem 0 wiosek: 11 -> 46 (Za Murem, Dothrakowie, Dar,
  male okregi Skagos i Sarnoru) - tam wszystko trzyma wies gry, jak juz przewiduje PROJEKT-WIOSKI 1.7.
- Ludzi poza wsiami w okregach swiata: 12.7 mln z 42.0 mln (30%) - historycznie Anglia ok. 1300 miala poza wsiami i przysiolkami
  ok. 28-38% calej ludnosci [S], wiec zgodnie.
- Plan to gorna granica. W v2 liczbe wiosek tnie najpierw teren (sufit 16 na okreg, 1 na 60 jedn.^2 ladu, widoczne linie), wiec
  postawionych ubedzie mniej niz 30% - przeliczyc generatorem v2 z nowym `n` (W0, ok. 25 s), zanim cokolwiek pojdzie do gry.
- Wariant "wioski 2200" (W2, galaz w2-wioski-2200) to sam widok na liczbach z gry - tej zmiany nie dotyczy.

### 6.5 Co z ludzmi poza wsiami (zostaja w ksiedze)

- Placa rente, daja rekrutow, jedza ze spichlerza i uciekaja jak wszyscy - liczy ich ksiega jak dzis.
- Rabunek: jeden ulamek dla calego okregu (6.2) - zagrody, mlyny i karczmy pala sie razem z osadami, wsrod ktorych stoja.
  Klasztorow i septrii nie wyrozniamy (historycznie bywaly i palone, i oszczedzane za okup - za malo, zeby to liczyc osobno, ok. 1%).
- Glod: uchodzcy z glodu oprozniaja najpierw ubocze (PROJEKT-WIOSKI 6.2) - bez zmian.

### 6.6 Osobne obrazki: septrie, gospody, mlyny (do rozwazenia)

| Co | Ile bylo historycznie | Na okreg-mediane (60 tys. ludzi) | Zalecenie |
|---|---|---|---|
| Klasztor / septria poza miastem | ok. 1 000 domow w Anglii, ok. polowa poza miastami: 1 na ok. 9 tys. ludzi [S na Z] | ok. 6-7 (Reach ok. 20) | Nie teraz. Pozniej (po W3): najwyzej 1 septria na okreg Wiary Siedmiu, w miejscu ustronnym (wyspa, wzgorze, brzeg jeziora), jako wioska klasy F z nazwa "... Septry"; rabunek septrii = osobny komunikat. Na Polnocy, za Murem, na Zelaznych Wyspach i w Essos - brak. |
| Gospoda | przy kazdym trakcie co ok. dzien drogi (25-35 km) [S] | 1-3 | Bez osobnego obrazka: wioska na skrzyzowaniu drog namalowanych (klasa H) moze nosic nazwe "... Inn" (jak Crossroads Inn w ROT). |
| Mlyn | 1 na ok. 480 ludzi [Z] | ok. 125 | Nie - mlyny stoja we wsiach i przy nich; ROT ma juz wsie-mlyny (Mousedown Mill, Giftmill). |
| Samotne zagrody | 2-50% ludzi wedlug krainy | tysiace | Nie - to "przestrzen miedzy wioskami"; widac je tylko w linii menu (6.2). |
| Holdfast / dwor | kilkadziesiat na okreg [S] | kilkadziesiat | Nie teraz; wies gry jest glowna wioska z dworem (PROJEKT-WIOSKI 3). |

---

## 7. Czego nie sprawdzono i ryzyka

1. Wszystkie udzialy w tabeli krain to [S]. Zadne zrodlo nie podaje gotowego "procentu ludzi we wsiach" - ani dla Europy, ani w
   lore. Roberts i Wrathmell mapuja osadnictwo rozproszone na danych z XIX w.; udzial przysiolkow i zagrod ok. 1300 to szacunek.
2. Krainy Essos maja tylko analogi z poludnia Europy i Bliskiego Wschodu; Wyspy Letnie, Ibben, Sarnor, Yi Ti i Valyria - wartosci
   robocze bez badania.
3. Niesprawdzone u zrodla (zostaja jako [Z-w] albo odrzucone): Etat 1328 (liczby parafii i ognisk), Flandria 36-40%, Niemcy 170 tys.
   osad i 14 mln, Villani 1338, Wenecja z dogado, Palermo (odrzucone), Knowles i Hadcock 17 500 (zastapione Campbellem).
4. Liczba wiosek na mapie po zmianie - tylko plan (bez terenu). Rozstrzyga ponowny przebieg generatora v2.
5. Czas spalenia jednej osady rosnie x 1/W (Reach x1.22, Polnoc x1.56, Za Murem x4, Dothrakowie x33). Tempo pustoszenia w ludziach
   bez zmian, ale okienko rabunku pokaze mniej spalonych osad na dobe - sprawdzic w tekstach PROJEKT-RABUNEK (liczby dob na osade).
6. Dothrakowie: przy W 0.03 okreg ma 3-10 osad po ok. 8 tys. ludzi; wies gry trzyma wszystko. Gdyby to razilo w menu, mozna dla
   khuzait liczyc osady jak dawniej (W = 1) i tylko nie stawiac wiosek - decyzja przy W3.
7. Polnoc zima: winter town wchlania czesc wsi i zagrod [L]; ksiega tego nie modeluje - przyjety stan letni.

---

## 8. Zrodla

**Sprawdzone ponownie w tej syntezie [SPR]:**
- Bruce M. S. Campbell, "Benchmarking medieval economic development: England, Wales, Scotland, and Ireland, c.1290", Economic History
  Review 61(4), 2008, 896-945; tab. 1 (domy zakonne i zakonnicy wg Knowles i Hadcock), tab. 16 (ludnosc, gestosc, miasta, parafie):
  https://pureadmin.qub.ac.uk/ws/files/1641214/Benchmarking_paper.pdf
- S. Broadberry, B. M. S. Campbell, B. van Leeuwen, "English Medieval Population: Reconciling Time Series and Cross Sectional
  Evidence", Warwick 2011, tab. 8B (hrabstwa 1290 i 1377):
  https://warwick.ac.uk/fac/soc/economics/seminars/seminars/conferences/venice3/programme/english_medieval_population.pdf
- R. E. Glasscock (red.), The Lay Subsidy of 1334, British Academy 1975 ("some 14,000 places"; opis w Lincolnshire HER SLI653):
  https://heritage-explorer.lincolnshire.gov.uk/Source/SLI653
- Historic England, "Introductions to Heritage Assets: Medieval Settlements", 2011 (Roberts i Wrathmell, Central Province, miejsca
  z fosa): https://medieval-settlement.com/wp-content/uploads/2022/01/engher-medievalsettlements.pdf ; B. K. Roberts, S. Wrathmell,
  An Atlas of Rural Settlement in England, 2000
- M. Bosker, S. Brakman, H. Garretsen, H. de Jong, M. Schramm, "The Development of Cities in Italy 1300-1861", CESifo WP 1893, 2007,
  tab. 1 (dane P. Malanima): https://www.ifo.de/sites/default/files/docbase/docs/cesifo1_wp1893.pdf
- C. Flores Varela, "La evolucion de la poblacion urbana de Andalucia en los siglos XV y XVI", En la Espana Medieval 28, 2005
  (47.5% w 32 osadach, za Collantes de Teran 1990): https://revistas.ucm.es/index.php/ELEM/article/view/ELEM0505110097A
- A Wiki of Ice and Fire (awoiaf.westeros.org), strony North, Reach, Riverlands, Iron Islands, Lhazar, Northern mountain clans,
  Septry, Winter town, Crossroads inn (wikitext pobrany 07.10 przez api.php; kopie: `SCR\gdzie-zyli-ludzie\src\awoiaf-b4`).
- ROT-Map `ModuleData\settlements.xml` (wsie Lhazaru, kultury) i `Armoury/src/PopulationLaw.cs` (udzial miast, zamki 0).

**Z badan 1-4 (niesprawdzane ponownie; pelne listy w notatkach `SCR\gdzie-zyli-ludzie`):**
- C. Dyer, Making a Living in the Middle Ages, 2002 (rec. J. Langdon: https://reviews.history.ac.uk/review/294/print/); C. Dyer,
  "How Urban was Medieval England?", History Today: https://www.historytoday.com/archive/how-urban-was-medieval-england
- D. M. Palliser (red.), The Cambridge Urban History of Britain I, 2000; K. Tiller, English Local History, 1992 (za
  https://htt.herefordshire.gov.uk/herefordshires-past/the-medieval-period/towns/governance-and-population)
- M. Busby, Leicestershire poll tax 1377-81, MSRG 2007: https://www.medievalists.net/2012/10/leicestershire-settlements-through-the-late-fourteenth-century-poll-tax-records-urban-or-rural/
- English Heritage, Wharram Percy: https://www.english-heritage.org.uk/visit/places/wharram-percy-deserted-medieval-village/history
- J. Langdon, Mills in the Medieval Economy, 2004: https://muse.jhu.edu/article/186647/pdf
- A. K. Warren, Anchorites and their Patrons in Medieval England, 1985 (za https://hermitary.com/bookreviews/warren.html)
- C. M. Woolgar, The Great Household in Late Medieval England, 1999 (rec. https://scholarworks.iu.edu/journals/index.php/tmr/article/view/15036)
- A. L. Beier, Masterless Men, 1985; Siege of Calais 1346-47 (za Sumption): https://en.wikipedia.org/wiki/Siege_of_Calais_(1346%E2%80%931347)
- S. Letters, Gazetteer of Markets and Fairs in England and Wales to 1516: https://archives.history.ac.uk/gazetteer/fullintro.html
- F. Lot, "L'etat des paroisses et des feux de 1328", BEC 1929 (za https://www.histoire-genealogie.com/Les-denombrements-et-les-recensements-sous-l-Ancien-Regime)
- P. Stabel 1997, W. Blockmans i W. Prevenier 1999 (za https://flemish.wp.st-andrews.ac.uk/2015/11/27/medieval-and-early-modern-migration-from-flanders/);
  W. Blockmans, Holland i Zelandia: https://scholarlypublications.universiteitleiden.nl/access/item%3A2728156/download
- W. Abel, Wuestungen (za D. Luebke): https://pages.uoregon.edu/dluebke/Reformations441/Wuestungen.html ; LWL, formy osad Westfalii:
  https://www.lwl.org/westfalen-regional-download/PDF/S098_Siedlungsformen.pdf
- ScARF, Medieval Rural Settlement: https://scarf.scot/regional/sesarf/9-medieval/9-2-settlement-and-domesticity/9-2-1-rural-settlement/ ;
  R. A. Dodgshon, PSAS 123, 1993: https://journals.socantscot.org/index.php/psas/article/view/9478
- North West Regional Research Framework: https://researchframeworks.org/nwrf/?p=2102 ; CPAT Middle Wye (Walia):
  https://heneb.org.uk/archive/cpat/projects/longer/histland/midwye/mwsettle.htm
- I. Oye, "Norway in the Middle Ages: farms or hamlets - and villages too?", Ruralia 3, 1999:
  https://www.medievalists.net/2010/12/norway-in-the-middle-ages-farms-or-hamlets-%E2%80%93-and-villages-too/ ; Black Death in Norway
  (za O. J. Benedictow): https://en.wikipedia.org/wiki/Black_Death_in_Norway
- Spis Islandii 1703: https://en.wikipedia.org/wiki/1703_Icelandic_census ; H. P. Strom, Wyspy Owcze: https://trap.fo/en/history/population-trends-1327-2022/
- D. Herlihy, C. Klapisch-Zuber, Tuscans and Their Families (Catasto 1427); Brown Univ. Online Catasto:
  https://cds.library.brown.edu/projects/catasto/newsearch/note_on_herlihy_files.html
- P. Charanis, "The Monk as an Element of Byzantine Society", DOP 25, 1971: https://www.myriobiblos.gr/texts/english/charanis_monk.html ;
  J. Preiser-Kapeller 2010: https://historicalnetworkresearch.org/wp-content/uploads/2013/01/Preiser_WorkingPapersIV_ComplexCrisis.pdf
- Universitat de Valencia, Alquerias: https://www.uv.es/horta-valencia-chair/en/heritage-catalogue/alquerias/alquerias.html ;
  Village perche (za F. Benoit): https://en.wikipedia.org/wiki/Village_perch%C3%A9
- Black Death in the Middle East: https://en.wikipedia.org/wiki/Black_Death_in_the_Middle_East ; M. Cartwright, "Yurt", World History
  Encyclopedia 2019: https://www.worldhistory.org/Yurt/
- H. Barker, That Most Precious Merchandise, 2019 (za G. Comai, OBC Transeuropa 2021: https://www.balcanicaucaso.org/en/?p=238858)
- G. R. R. Martin: A Game of Thrones (rozdz. 3, 13, 28, 37, 70), A Clash of Kings (5, 9, 21, 24), A Storm of Swords (1, 9, 11, 27,
  38, 39), A Feast for Crows (21, 25, 30, 40), A Dance with Dragons, The Sworn Sword; The World of Ice & Fire (2014); Fire & Blood
  (2018) - cytowane za przypisami AWOIAF. A Game of Thrones RPG (Guardians of Order 2005) - pol-kanon.
