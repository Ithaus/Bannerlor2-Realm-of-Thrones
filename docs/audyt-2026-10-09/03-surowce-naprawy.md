# 03 - Surowce, naprawy i bilans produkcji kazdego surowca (audyt 09.10, tylko odczyt)

Pytanie Jeffa: "jesli sa naprawy zbroi i mieczy, to ilosc surowcow jest minimalna do naprawy - sprawdz, czy nie ma za duzo surowcow
potrzebnych do naprawy broni i uzbrojenia, oraz czy ilosc produkcji kazdego surowca jest dostosowana".

Oznaczenia: [K] kod (wersja w grze, Armoury c01a54ba = 2e235ea, sciezki wzgledem `Armoury/src`), [P] pomiar z logu, [H] historia albo lore
ze zrodlem, [S] szacunek. Pomiar glowny: autotest ROCZNY `Armoury-2026-10-08_08-18-38.log` (364 doby, TOWARY 3; naprawy w 160/161 sie
nie zmienily), sprawdzony na najnowszej kampanii `Armoury-2026-10-08_11-40-05.log` (ten sam obraz: doba 3 - 42 naprawione, 287 czeka,
z tego 274 na metal). Skrypty liczace: scratchpad sesji `s03/towary.py`, `s03/ai.py`, `s03/needs.py` (poza repo).

Co juz bylo zbadane i nie robie od nowa: koszt ROBOCIZNY naprawy (docs/HISTORIA-KOSZT-NAPRAWY-2026-10-07.md, PROJEKT-KOSZT-NAPRAWY -
Jeff 07.10: "nasza robocizna jest lepsza", robocizna zostaje), model materialow (MODEL-MATERIALOW.md), bilans zjadania przez miasta
(BILANS-ZUZYCIA.md), skala wydobycia (AUDYT-SUROWCE.md), plan K13. Ten audyt dotyczy tylko MATERIALU napraw i bilansu surowcow.

---

## 0. Dla Jeffa

Masz racje: naprawa zjada za duzo surowca. Dzis kowal, ktory czysci zardzewiala zbroje plytowa, zuzywa na nia 14% zelaza nowej zbroji -
to ok. 100 kg rudy i pol tony drewna na jedno odrdzewienie. W sredniowieczu rdze zdejmowano piaskiem i octem w beczce, wgniecenia
klepano mlotkiem, a nowego zelaza dawano 1-3% (nity, kilka plytek, kolka z kolczug na zlom). Przez to naprawy w ciagu roku zjadaja
prawie tyle rudy, co wszystkie kuznie broni w miastach (36 wobec 52 ladunkow dziennie w drugim polroczu), a mimo to lordowie nie
nadazaja: codziennie ok. 2 200 sztuk czeka na zelazo, a stos obitego sprzetu u lordow rosnie do 134 tys. sztuk po roku.
Surowcow na swiecie jako takich nie brakuje - zapas KAZDEGO surowca rosnie caly rok (ruda, drewno, skory, len, welna); brakuje ich
tylko w czesci miast. Proponuje na dzis jedna mala zmiane: przy naprawie metal liczony wedle tego, jak naprawiano naprawde (plyta 1% / 3%,
kolczuga 1.5% / 4%, miecz prawie nic), a robocizna bez zmian - Ty prawie tego nie odczujesz, rachunek u kowala spadnie o kilka procent.
Przy okazji znalazlem dwie dziury "z niczego / w nicosc": wedrowni rzemieslnicy z BannerKings rodza sztaby (takze stal valyrianska,
ktorej wedlug kanonu nikt juz nie umie wytopic), a BannerKings co dzien kasuje 2% kazdego stosu towaru powyzej 500 sztuk (ruda tez).

---

## 1. Stan dzis

### 1.1 Jedna regula materialu dla wszystkich napraw u kowali miasta [K]

- `MendMaterial.Share` (MendMaterial.cs:76-82): udzial materialu = `MendMaterialMaxShare` (0.20, Settings.cs:748; brak w Armoury.json Jeffa
  - dziala domyslne) x (1 - stan). Ta sama liczba sluzy tez ROBOCIZNIE (`LaborF` :127-136, wolajacy podaja `Share`).
- `NeedsShare` (:98-106): material = udzial x PELNA receptura sztuki z `ArmsPricing.CostOf` (ArmsPricing.cs:165-233), tej samej, z ktorej
  warsztaty licza nowa sztuke (`WorkshopLaw.Needs`, WorkshopLaw.cs:154-169):
  - metal w kg surowki = waga x udzial metalu x 1.4 (strata kucia) x 1.25^stopien gatunku (t3 x1.56, t4 x1.95, t5-t6 x2.44);
  - drewno = drewno sztuki + `WorkshopForgeWoodPerMetalKg` 12.5 kg na kg metalu (wegiel kuzni, Settings.cs:444);
  - skora, plotno (len albo welna).
- Skad metal (`Bench`, :366-407, `Pick` :433-469): sztaby z polki, ZLOM z wrakow na polce (polowa metalu receptury, :395-406) albo RUDA przez
  dymarke: ladunek rudy 100 kg (HistoricalPrices.cs:350-358, `HistBulkUnitFactor` 10) daje 15 kg surowki i spala 5 ladunkow drewna = 500 kg
  (:384-385; Settings.cs:448-449).
- Wolajacy (wszystkie przez `Needs`/`NeedsFor`): lawa i "Pick a piece" (SmithMenu.cs:491, :549, :978, :1045), polki wojska (:1237),
  uprzaz bohatera (ArmouryBehavior.cs:2399, `NeedsFor` z ciaglego braku), ludzie gracza (TroopSelfMend.cs:74, :164, :277), kwatermistrz
  Spoils (SpoilsSeal.cs:759), lordowie AI (AiWear.cs:261, :329). Jedno miejsce zmiany = `NeedsShare`.
- Wlasne rece gracza - DWIE INNE reguly: `Forge.SelfRepair` bierze `SelfRepairMaterialFactor` 0.25 x brak (Forge.cs:505-509; 0.25 jest w
  Armoury.json), `SmithMenu.SelfMendParts` 0.20 x brak (SmithMenu.cs:706-725, zaokraglenie na czesc - drobna naprawa bywa darmowa) - i to
  z INNEJ receptury (`Recipes.For`, Recipes.cs:242-263: plyta = 1 kg sztab na 1 kg wagi, bez 1.4 i bez wegla).

### 1.2 Stany w grze (z RBM) i co trafia do naprawy [K]

Z RBM (RBMCombat_item_modifiers.xml) grupy plate/chain/leather/cloth maja stany: 0.6 (dented / loose / worn), 0.3 (rusty / battered / ripped),
0.1 (`*_damage_*` = wrak). Bron: 0.6 (dull, bent, dented) i 0.3 (rusty, cracked, splintered).
- Lup (`PickWornModifier`, ArmouryBehavior.cs:1807-1828; LootWearBase 45 +- 25 z Armoury.json): zbroja 30% stan 0.6, 50% stan 0.3, 20% wrak;
  bron 30% stan 0.6, 70% stan 0.3.
- Bitwa (`MildWornModifier` :1794-1805): zawsze najlzejszy stan 0.6.
- Wrak (stan <= 0.1, LootPrices.cs:43-49) - kowale miasta nie naprawiaja, idzie na zlom (paczka 158).
Udzial materialu dzis: stan 0.6 -> 8% receptury, stan 0.3 -> 14%.

### 1.3 Ile to jest na sztuke - dzis wobec nowej sztuki [K + S]

Wagi: mediany z ROT (ROT-Content/ModuleData/ROTassets.xml): zbroja plytowa 34 kg (115 szt.), kolczuga 18 kg, skorzana 9 kg, helm plytowy 1.8 kg,
rekawice 2.2 kg, buty skorzane 1.15 kg, tarcza 4 kg, kropierz plytowy 140 kg. Liczone wzorami z 1.1 (s03/needs.py).

| Sztuka | Nowa: surowka / drewno | Stan 0.6 dzis | Stan 0.3 dzis | Stan 0.3 dzis z rudy |
|---|---|---|---|---|
| Zbroja plytowa t5 34 kg | 104.6 kg / 536 kg | 8.4 kg metalu + 43 kg drewna | 14.6 kg + 75 kg | ok. 1 ladunek rudy (100 kg) + ok. 560 kg drewna |
| Kolczuga t4 18 kg | 44.3 / 283 | 3.5 + 23 | 6.2 + 40 | 0.4 ladunku + ok. 250 kg drewna |
| Zbroja skorzana t3 9 kg | 3.0 / 24 + 9.2 kg skory | 0.24 + 1.9, skora 0.7 | 0.41 + 3.3, skora 1.3 | - |
| Helm plytowy t4 1.8 kg | 4.4 / 28 | 0.35 + 2.3 | 0.62 + 4.0 | ok. 25 kg drewna |
| Miecz t4 1.2 kg | 2.8 / 18 | 0.22 + 1.4 | 0.39 + 2.5 | ok. 15 kg drewna |
| Tarcza t3 4 kg | 0.9 / 12 | 0.07 + 0.9 | 0.12 + 1.7 | - |
| Kropierz plytowy t5 140 kg (uprzaz gracza) | 431 / 2 205 | 34 + 176 | 60 + 309 | 4 ladunki rudy + ok. 2.3 t drewna |

Wniosek [K]: naprawa bierze zawsze 8% albo 14% materialu NOWEJ sztuki, bez wzgledu na to, czy to rdza (czyszczenie), wgniecenie
(klepanie) czy pekniete drzewce. Odrdzewienie plyty t5 kosztuje tyle zelaza, ile 5 nowych mieczy.

### 1.4 Pomiar roczny: naprawy lordow AI [P]

Linia "Zuzycie AI" (AiWear.cs:355-371), srednie dzienne:

| Doby | Lup obity | Zuzyte w bitwach | Naprawione | Metal kg (na szt.) | Drewno kg | Skora kg | Czeka na material (w tym metal) | Obitych na koniec okna |
|---|---|---|---|---|---|---|---|---|
| 1-40 | 428 | 305 | 218 | 40 (0.18) | 297 | 19 | 322 (253) | 9 784 |
| 41-180 | 3 270 | 975 | 993 | 407 (0.41) | 2 633 | 131 | 2 726 (1 980) | 97 651 |
| 181-364 | 4 792 | 804 | 1 304 | 843 (0.65) | 5 030 | 209 | 4 705 (2 902) | 134 354 |
| rok | 3 727 | 815 | 1 065 | 587 (0.55) | 3 588 | 158 | 3 462 (2 256) | 134 354 w 617 partiach |

- Doplyw obitych 4 542/dobe, naprawy 1 065/dobe -> zaleglosc rosnie o ok. 370 szt. dziennie. 82% doplywu to LUP (DTE doklada go do
  zbrojowni, AiWear.cs:119-124), nie zuzycie sprzetu w uzyciu.
- Kolejnosc: najgorsze najpierw (AiWear.cs:290) - pierwsze ida sztuki o najwiekszym udziale materialu; brak metalu zatrzymuje caly stos (:319-331).
- To samo w najnowszej kampanii (11-40-05, doba 3): 42 naprawione, czeka 287, z tego na metal 274.

Ksiega towarow 146 ("Towary:"), pozycja "naprawy kowali miasta (135)", srednio na dobe [P]:

| Surowiec | Doby 2-364 | Doby 181-364 | Udzial w calym zuzyciu swiata (181-364) | Dla porownania: warsztaty zbrojne (nowe sztuki) |
|---|---|---|---|---|
| Ruda (ladunek 100 kg) | 27 | 36 | 19% | 52-53 |
| Drewno (ladunek 100 kg) | 169 | 230 | 15% | 326-330 |
| Skora | 16 | 21 | 14% | 12-17 |
| Plotno | 3 | 4 | 3% | 25 |

Drewno napraw to w ok. 80% opal dymarki: 27 ladunkow rudy x 5 = 135 ladunkow drewna (MendMaterial.cs:441, `WoodUnits`) - zgadza sie z 169.
Warsztaty zbrojne w tym czasie robia 565 nowych sztuk dziennie (rok; linia "Warsztaty:"), w tym ZBROI KORPUSU tylko 10/dobe, i odpuszczaja
370 cykli dziennie "brak surowca: ruda" [P]. Naprawy i nowe sztuki biora rude z tej samej polki.

Na sztuke: naprawa 0.025 ladunku rudy i 0.16 drewna, nowa sztuka z warsztatu 0.094 rudy i 0.58 drewna [P, rok] - naprawa kosztuje
dzis ok. 1/4 surowca nowej sztuki (przy innym skladzie: naprawia sie wiecej zbroi, warsztaty robia glownie drzewca i tarcze).

### 1.5 Bilans KAZDEGO surowca - rok [P]

Z ksiegi towarow 146 (`s03/towary.py`), srednio na dobe dla dob 2-364; "zapas" = caly swiat (miasta, zamki, wsie, wozy, karawany, partie).

| Surowiec | Produkcja (glowne zrodla) | Zuzycie (glowne ujscia) | Zapas start -> d40 -> d180 -> koniec | Ocena |
|---|---|---|---|---|
| Ruda | 202 (kopalnie wsi 190, kopalnie BK w miastach 12) | 163: warsztaty zbrojne 53, NAPRAWY 27, linie narzedzi 26, przepadlo z rozbitymi partiami 22, BEE 15, BK kasuje 10, rzemieslnicy BK 8 | 762 -> 4 109 -> 10 666 -> 12 651 | swiat: nadwyzka +33/d; miast bez rudy 25-28 z 97 (linia "Ruda:"), warsztaty: brak rudy 370 cykli/d - rozklad, nie ilosc |
| Drewno | 1 568 (las wsi 126: 1 261, drwale 292) | 1 441: budowy 579, warsztaty zbrojne 330, BK kasuje 179, NAPRAWY 169, mieszczanie 71, przepadlo 69 | 7 359 -> 20 313 -> 44 324 -> 46 856 | nadwyzka +109/d; plateau dopiero po d180 dzieki kasowaniu BK (w nicosc) |
| Wegiel drzewny | 52 (tylko wedrowcy BK - z niczego) | 52 (przepadaja z nimi) | ok. 150 | nie jest towarem obiegu: wegiel liczymy w drewnie (dobrze) |
| Sztaby (surowka..stal szl.) | ok. 168 (tylko wedrowcy BK - z niczego) | ok. 168 (przepadaja) | ok. 150-200 kazdej | dziura "z niczego / w nicosc", patrz 1.6 |
| Stal valyrianska (ironIngot6) | 45 (wedrowcy BK - z niczego) | 44 (przepadaja) | 2 -> 376 -> 231 -> 331, w tym miasta 104, zamki 13 | sprzeczne z kanonem (patrz 2.4) |
| Skory surowe | 350 (rzeznie BK 294 - w tym mnoznik rzemieslnikow, warsztaty 50) | 285: rzemioslo miasta 148: 239, garbarnie 23 | 188 -> 5 773 -> 12 810 -> 15 200 | nadwyzka +41/d |
| Skora | 146 (rzemioslo miasta 100, garbarnie 45) | 136: odziez wojska 48, mieszczanie 34, warsztaty zbrojne 17, BEE 17, NAPRAWY 16 | 127 -> 1 315 -> 1 946 -> 2 259 | NAJCIASNIEJSZY: +6/d, zapas ok. 15 dob; waskie gardlo to garbowanie, nie skory |
| Len (flax) | 409 (wsie) | 333: rzemioslo miasta 205, BK kasuje 60, linie 26, mieszczanie 22 | 3 795 -> 11 407 -> 19 724 -> 24 836 | nadwyzka +58/d |
| Plotno | 120 (rzemioslo 68, tkalnie 51) | 111: odziez wojska 43, mieszczanie 35, warsztaty zbrojne 26, naprawy 3 | 843 -> 1 428 -> 3 175 -> 3 875 | lekka nadwyzka +8.5/d |
| Welna | 325 (wsie 280, rzemieslnicy BK 44) | 279: rzemioslo miasta 197, tkalnie 48 | 2 302 -> 6 527 -> 11 431 -> 14 956 | nadwyzka +35/d |
| Bawelna | 122 (wsie) | 96 (mieszczanie 80) | 1 517 -> 10 512 | nadwyzka +25/d, nikt jej nie przerabia |
| Futro | 40 | 34 (mieszczanie) | 309 -> 2 151 | lekka nadwyzka |
| Narzedzia | 121 (kuznie 103) | 91 (mieszczanie 57, budowy 15) | 279 -> 9 235 | nadwyzka +25/d |
| Deski | 36 (wedrowcy BK 25, warsztaty 11) | 36 (przepadaja 25) | 324 -> 91 | prawie nie istnieja w obiegu |
| Rogi, sciegna, konopie | - | - | - | w grze nie ma takich towarow (luk = drewno + metal + len) |

Ocena ogolna [P]: przez caly rok zapas KAZDEGO surowca rosnie liniowo (produkcja 4-21% ponad zuzycie). Swiat ma surowca dosc;
braki sa lokalne (ruda w ok. 28 miastach) albo w PRZEROBIE (skora - garbowanie). Naprawy sa jedynym duzym odbiorca metalu, ktory
przy tym zle odwzorowuje historie (1.3, 2.1).

### 1.6 Dwie dziury znalezione przy bilansie [K + P]

(a) WEDROWCY BK RODZA TOWAR. `PopulationPartyComponent.CreateTravellerParty` (BK, bk/BannerKings.Components/PopulationPartyComponent.cs:320-405,
wolane z BKPartyBehavior.cs:1043): "wedrowni rzemieslnicy" dostaja przy narodzinach sztaby i wegiel (`Materials` :84-96, losowanie WAZONE
WARTOSCIA - najdrozsza stal valyrianska wypada najczesciej), a kazdy wedrowiec towary handlowe (len, welna, skora, plotno, narzedzia,
bawelna...). Po wejsciu do miasta (BKPartyBehavior.cs:845-858) miasto PLACI za ladunek (zloto znika z kasy - to jest juz w projekcie
ekonomii, tabela zrodel wiersz 16 / 169 B7) i dostaje KOPIE towaru na polke, a partia jest niszczona RAZEM z ladunkiem (:429-435).
W ksiedze: "inne ticki dobowe (osady)" = "przepadlo z rozbitymi partiami (inne partie)"; stal valyrianska 45 sztab dziennie, w miastach
104 sztaby po roku.
(b) BK KASUJE NADWYZKI W NICOSC. `BKSettlementBehavior.DeleteOverProduction` (bk/BannerKings.Behaviours/BKSettlementBehavior.cs:314-338,
`DeleteOverProduction=True` w BannerKings.json Jeffa): kazdy towar handlowy (nie zywnosc, nie zwierze) w stosie > 500 szt. traci 2% dziennie.
W ksiedze "BK osady (gnicie i nadprodukcja)": drewno 179/d (235 w drugim polroczu), len 60 (83), ruda 10 (16), welna 10, skory 6. Ruda nie
gnije. To samo miejsce kasuje z polek kazda rzecz `IsCraftedByPlayer` (wyroby gracza sprzedane miastu znikaja; AUDYT-PRODUKCJI-MODY.md:433).

---

## 2. Jak bylo w sredniowieczu i w lore

### 2.1 Material naprawy wobec nowej sztuki [H]

Z docs/HISTORIA-KOSZT-NAPRAWY-2026-10-07.md (zrodla tam: Richardson, rachunki Tower 1353-1399; Derby 1390-93; ffoulkes; Rogers) - dotad czytane
pod katem robocizny, tu pod katem MATERIALU:
- Rdza: kolczugi czyszczono toczac je w beczce z piaskiem i octem (Tower 1362-4: 4 ludzi x 45 dni na ok. 320 koszul) - zero nowego metalu.
- Kolczuga: Tower 1399 - 500 kolczug naprawiono i powiekszono materialem z 22 starych haubergeonow -> ok. 4% kolczugi na sztuke, i to ze
  ZLOMU kolczego (stare kolczugi wyceniano jako zlom na 7-14% nowych, Tower lata 1330.). Ciezka naprawa = wymiana ok. 1/4 kolek ze zlomu.
- Plyta, brygantyna: klepanie wgniecen, nity (100 gwozdzi za 3 d, 1373-5), kilka nowych plytek, rzemienie - w 1-2 dni roboty; metalu na
  oko 0.1-1 kg na kilkunastokilogramowy pancerz [S]. Drogo bylo dopiero NOWE pokrycie lub podszycie (tkanina, skora).
- Miecz, grot: Tower 1375-7 - 125 mieczy w 24 dniach jednego robotnika (oczyszczenie, ostrzenie, olej) - nowego metalu nie ma; ciezko = nowa
  rekojesc albo pochwa (drewno, skora). Drzewce kopii: nowe drzewce z jesionu to tani material i kilka godzin.
- Skora i tkanina: odwrotnie - przy ciezkiej naprawie (podzelowanie butow, nowe pokrycie przeszywanicy) material to 60-75% rachunku
  (Colne Priory 1442, Mortimer 1393-4).
Podsumowanie [H/S]: metal przy naprawie 0-1% (lekka), 1-5% (ciezka; kolczuga do ok. 4% ze zlomu) masy nowej sztuki; skora i tkanina
5-30%; drewno trzonka/tarczy 10-50%. Dzisiejsze 8% / 14% metalu jest wiec 5-10x za duze przy lekkim i 3-5x przy ciezkim zuzyciu metalu.

### 2.2 Dymarka i wegiel [H]

- Wydajnosc dymarki: od ok. 15-20% (uboga ruda, Tvaaker - 19 kg zelaza na 100 kg zuzla) do ok. 50% masy rudy (bogaty hematyt; bilans zuzli z
  okolic Kanalu Bristolskiego, efektywnosc 74%); rekonstrukcja Wareham: 24.6 kg rudy -> 8.5 kg lupy. Rachunek z Glamorgan 1531: dwie lupy po
  50 kg dziennie z jednej dymarki. Gra: 15% (100 kg rudy -> 15 kg surowki) - dolna granica, w porzadku dla ubogich rud.
- Wegiel: ok. 1 kg wegla na 1 kg rudy (do 1.5 w rekonstrukcji); w eksperymentach 10-18 kg wegla na kg zelaza; wegiel z drewna 1:4-7 masy.
  Gra: 500 kg drewna (ok. 70-125 kg wegla) na 100 kg rudy - w przedziale.
- Kuznia (przerob sztaby na wyrob): rzetelnej liczby dla sredniowiecznej kuzni nie znalazlem; w literaturze podkresla sie, ze kucie zjada
  wegla duzo mniej niz wytop (studium walonskie: ok. 3 t wegla na t zelaza razem z rafinacja). Gra: 12.5 kg drewna (ok. 2-3 kg wegla) na kg
  metalu - rozsadne dla NOWEJ sztuki; przy naprawie opal idzie za godzinami ognia, nie za kg nowego metalu [S].
Zrodla: https://en.wikipedia.org/wiki/Bloomery ; https://historicalmetallurgy.org/media/pjwd2b5o/hmsnews59.pdf ;
https://www.warehamforge.ca/ironsmelting/smelt/smelt10.html ; https://tidsskrift.dk/Hikuin/article/view/110760 ;
https://hmsjournal.org/index.php/home/article/view/682/678 ; https://hal.archives-ouvertes.fr/hal-03446934 .

### 2.3 Skala [H/S]

Anglia ok. 1300: ok. 1 000 t zelaza rocznie na ok. 4.75 mln ludzi, 0.2-0.4 kg na glowe (AUDYT-SUROWCE.md 4; https://www.namho.org/research/SECTION_5_Iron_20131209.pdf).
Gra: 202 ladunki rudy x 15 kg = ok. 3 t surowki na dobe = ok. 1 100 t rocznie (rok 364 dni) na 52.6 mln ludzi = 0.02 kg na glowe, czyli ok.
1:10-1:20 historii [S]. Wojsko gry ok. 100 tys. (test roczny) wobec ok. 0.5-1 mln mozliwych przy tej ludnosci - ok. 1:5-1:10. Zelazo jest wiec
w podobnej skali co wojsko, troche skapiej - dlatego nie wolno go marnowac na "odrdzewianie z nowej rudy". Podnoszenie wydobycia nie jest
potrzebne (zapas i tak rosnie, 1.5).

### 2.4 Lore GoT [H]

- Stali valyrianskiej nie umie sie juz wytapiac - tajemnica przepadla z Zaglada Valyrii; zachowala sie tylko sztuka PRZEKUWANIA istniejacej
  stali w Qohorze (Tobho Mott przekul Lod na dwa miecze). Kazda sztaba valyrianska, ktora rodzi sie w swiecie z niczego, lamie kanon.
  Zrodla: https://awoiaf.westeros.org/index.php/Valyrian_steel ; https://awoiaf.westeros.org/index.php/Qohorik ;
  https://bookanalysis.com/game-of-thrones/valyrian-steel/
- Kowale i zbrojmistrze Westeros pracuja na zelazie z kopaln (Lannisport, Wyspy Zelazne - "zelazo" jako sedno ich kultury); naprawa sprzetu
  po bitwie u zbrojmistrza to klepanie, nity i rzemienie - ten sam obraz co w Europie [S].

---

## 3. Luki i bledy logiki

L1. MATERIAL NAPRAWY = STALY ULAMEK NOWEJ SZTUKI. `NeedsShare` (MendMaterial.cs:98-106) bierze 8% / 14% calej receptury bez wzgledu na rodzaj
    szkody i rodzaj sztuki. Dowod: tabela 1.3 [K]; historia 2.1 [H] - metal 0-5%. Skutek [P]: naprawy zjadaja 19% rudy swiata i 15% drewna
    (1.4), w drugim polroczu 36 ladunkow rudy dziennie wobec 52 dla wszystkich warsztatow zbrojnych.

L2. METAL Z RUDY NA KAZDA LATKE. Gdy brak zlomu i sztab, `Pick` (:433-469) kupuje ladunek rudy + 5 ladunkow drewna (dymarka w miescie) na
    latke helmu. Dowod [P]: 80% drewna napraw to dymarka (169 ~ 27 x 5 + reszta). Historycznie latano zlomem i kolkami starych kolczug (2.1).
    Sam mechanizm (zlom z wrakow pierwszy, gdy tanszy) jest dobry - zly jest rozmiar potrzeby z L1.

L3. LORDOWIE NAPRAWIAJA KAZDY LUP, NAJGORSZE NAJPIERW. AiWear.cs:289-291 - wszystkie obite sztuki zbrojowni w kolejce, posortowane od
    najgorszej; nie liczy sie, ile sztuk ludzie naprawde nosza (`AiGear.NeedBuckets`, uzywane przy zuzyciu w bitwie :184-194, tu nie).
    Dowod [P]: 82% doplywu to lup; zaleglosc 134 354 sztuk po roku; "czeka na metal" 2 256/dobe. Skutek uboczny: zapis AiWear 1.8 MB i
    przegladanie zbrojowni w MenPurse.OnEntered (STAN-PRAC "ZWALNIANIE") - zaleglosc to tez koszt wydajnosci.

L4. TRZY REGULY DLA TEGO SAMEGO. Kowal miasta 0.20 x brak z receptury `ArmsPricing` (x1.4 i x1.25^stopien); kowadlo gracza `Forge.SelfRepair`
    0.25 x brak z `Recipes.For` (Forge.cs:505-509); "Mend it yourself" 0.20 z `Recipes.For` z zaokragleniem na czesc (SmithMenu.cs:716-720).
    Ta sama zbroja t5 34 kg w ciezkim stanie: u kowala 14.6 kg surowki, u siebie 0.25 x 0.7 x (54 sztab t5 + 14 t4) = ok. 12 sztab (6 kg).

L5. OPAL KUZNI IDZIE ZA KG METALU, NIE ZA GODZINAMI. `NeedsShare` dolicza 12.5 kg drewna na kg metalu receptury x udzial (:104), wiec
    czyszczenie plyty "spala" 75 kg drewna, a wymiana drzewca 0. [K] Drobne wobec L1 - samo zejdzie, gdy metal spadnie (P0-A).

L6. WEDROWCY BK: TOWAR I STAL VALYRIANSKA Z NICZEGO (1.6a). [K] PopulationPartyComponent.cs:320-405, BKPartyBehavior.cs:845-858, :429-435.
    [P] ok. 213 sztab i 52 wegla dziennie (w tym 45 valyrianskich), 25 desek, drobne ilosci lnu, welny, skory, narzedzi; kopia ladunku
    trafia na polke miasta, oryginal znika z partia. Lamie Z "nic z niczego" i kanon (2.4).

L7. BK KASUJE 2% NADWYZKI DZIENNIE (1.6b). [K] BKSettlementBehavior.cs:314-338. [P] drewno 179, len 60, ruda 10, welna 10 dziennie w nicosc;
    to dzis jedyny hamulec puchnacych polek (zapas drewna wyplaszcza sie dopiero po d180). Ruda i narzedzia nie gnija.

L8. WASKIE GARDLO SKORY TO GARBOWANIE. [P] skory surowe +41/d (15 200 po roku), skora wyprawiona +6/d (zapas ok. 15 dob). Kazde zwiekszenie
    udzialu skory w naprawach (albo wiecej napraw po P0-A) moze wyczerpac skore szybciej niz len czy drewno. Rzemioslo miasta 148 przerabia
    skory, ale za malo [P].

L9. BRAK TOWARU "ZLOM". Zlom jest tylko jako wraki na polce (MendMaterial.cs:395-406) i w przetopie gracza; nikt go nie skupuje ani nie wozi.
    Historycznie stare zbroje sprzedawano jako zlom do latania (2.1). [K] Nie blad - brakujacy element (sekcja 4, P2-J).

Kolizje z paczkami w toku:
- 169 (log obiegu): wiersz 16 / B7 (BKPartyBehavior.cs:845-858) mierzy ZLOTO wedrowcow; L6 dodaje strone TOWARU - prosze dopisac do listy
  pomiarow 169 (sztaby, wegiel, deski, towary wedrowcow; kopia na polce + przepadek z partia). Nie dubluje.
- 170 (BEE): BEE w ksiedze zjada rude 15/d, drewno 18/d, skore 17/d, narzedzia 9/d ("miasto: zbrojownia i warsztaty") - domkniecie BEE moze
  zmienic te liczby; nie koliduje z P0-A.
- 171 (zbrojenie zalog): ROT strzela drugim zdarzeniem werbunku -> podwojny komplet w zbrojowni (171 wynik 1) - pompuje zbrojownie AI;
  P1-B (naprawy tylko tego, co nosza) i 171 dotykaja tej samej zbrojowni DTE - robic po 171, nie rownolegle.

---

## 4. Propozycje

### P0-A. Metal przy naprawie wedle rodzaju sztuki i stanu (robocizna bez zmian)
- CO: w `MendMaterial.NeedsShare` (MendMaterial.cs:98-106) udzial METALU (i idacy za nim opal kuzni `c.MetalKg x 12.5`) brac z tabeli wedle rodzaju
  i braku, zamiast `0.2 x brak`. Drewno sztuki (`c.WoodKg`), skora i plotno - BEZ ZMIAN (0.2 x brak) do czasu P2-E. `Share` (robocizna, `LaborF`)
  NIE ZMIENIA SIE (decyzja Jeffa 07.10). Rodzaj z `ItemType` + `ArmorComponent.MaterialType`, jak w ArmsPricing.cs:176-213.
- LICZBY (udzial metalu receptury przy braku 0 / 0.4 [stan 0.6] / 0.7 [stan 0.3] / 1.0 [wrak - tylko wlasne rece], liniowo miedzy punktami):
  | Rodzaj | 0.4 | 0.7 | 1.0 | Uzasadnienie (jedno zdanie) |
  |---|---|---|---|---|
  | plyta, helm, rekawice, kropierz plytowy | 1% | 3% | 10% | nity i kilka plytek; rdze zdejmuje piasek, wgniecenia mlot (Tower, Derby). |
  | kolczuga | 1.5% | 4% | 12% | Tower 1399: 22 stare kolczugi na 500 naprawionych = ok. 4% kolczugi na sztuke. |
  | zbroja skorzana, tkanina (metal okuc) | 2% | 5% | 10% | sprzaczki i cwieki wymienia sie czesciej niz plyty. |
  | miecz, topor, buzdygan, dwureczne | 0% | 0.5% | 5% | szlif i olej zabieraja metal, nie dodaja (Tower 1375-7: 0.2 dnia na miecz). |
  | drzewcowa, miotana (grot) | 0% | 1% | 5% | grot sie ostrzy; pekniete jest drzewce, nie zelazo. |
  | tarcza (okucia, umbo) | 3% | 10% | 20% | okucie brzegu i umbo gna sie przy kazdym ciosie. |
  | luk, kusza (okucia, zamek) | 2% | 8% | 20% | zamek i strzemie kuszy to czesci kute (naprawy kusz Tower 1362-4). |
- SKUTEK [S, s03/needs.py]: zbroja plytowa t5 w stanie 0.3: 14.6 -> 3.1 kg surowki i 75 -> 16 kg opalu; kolczuga t4: 6.2 -> 1.8 kg; helm 0.62 -> 0.13;
  miecz 0.39 -> 0.01. Srednio metal na naprawe AI ok. 4-6x mniej; "czeka na metal" spada wielokrotnie; ruda napraw ok. 27 -> 5-10 ladunkow/d,
  drewno ok. 169 -> 40-60; wiecej rudy dla warsztatow zbrojnych (mniej "brak surowca: ruda"). Naprawianych sztuk wiecej (limit: godziny kowali
  `SmithHours`, potem skora - patrz ryzyko).
- Priorytet P0, wielkosc MALA (jedna funkcja + wylacznik), rozgrywka gracza: TAK, nieznacznie - material u kowala tanieje (zbroja t5 ciezka:
  ok. 30 d -> ok. 7 d materialu przy ok. 190 d robocizny, rachunek ok. -10% [S]); nowa kampania: NIE (zapas kowali `arm_mendstock` bez zmian
  formatu). Ryzyko: wiecej napraw = wiecej skory z L8 (zuzycie skory przez naprawy moze wzrosnac 2-3x przy tym samym udziale) - obserwowac
  zapas skory; zaleznosci: brak (paczki 162-168 nie dotykaja materialu; rezerwa sakiewki `OutstandingCost` maleje sama, AiWear.cs:244-269 -
  zgodne z projektem ekonomii, tabela sakiewki ludzi :235).

### P1-B. Lordowie naprawiaja najpierw to, co ludzie nosza; nadmiar lupu idzie na sprzedaz w stanie, w jakim jest
- CO: w `AiWear.MendInTown` (AiWear.cs:286-291) kolejka: najpierw sztuki do liczby ludzi w koszyku (`AiGear.NeedBuckets`, jak :184-194),
  lzejsze przed ciezszymi; obity lup ponad potrzebe nie wchodzi do kolejki - sprzedaje go `MenPurse` (juz bierze najgorsze, :210-222).
- LICZBY: limit naprawy nadmiaru = 0 [S]: historycznie lup sprzedawano i topiono, nie naprawiano na zapas (HISTORIA-RABUNKU-I-BITEW).
- SKUTEK [S]: doplyw do naprawy z 4 542 do ok. 800-1 500/d (zuzycie w bitwach 815 + czesc lupu przydatna); zaleglosc 134 tys. przestaje rosnac;
  mniejszy zapis AiWear i szybszy MenPurse.OnEntered (lista "optymalizacja na koniec" Jeffa).
- P1, SREDNIA, gracz: nie bezposrednio (wiecej obitego lupu na targach - tanszy uzywany sprzet), nowa kampania: nie. Ryzyko: wiecej towaru
  "zbroja obita" na polkach (cena sprzedazy 155) i wiecej wrakow jako zlom - dobrze dla L9. Zaleznosc: PO 171 (ta sama zbrojownia DTE).

### P1-C. Wedrowcy BK bez towaru z niczego (L6)
- CO: postfiks na `PopulationPartyComponent.CreateTravellerParty` (BK): zdjac z ladunku sztaby i wegiel (wszystkie `CraftingMaterials`), w kazdym
  razie STAL VALYRIANSKA; towary handlowe - albo zdjac, albo brac z polki miasta pochodzenia (wtedy przewoz prawdziwy). Na wejsciu
  (BKPartyBehavior.cs:851-855) - przelozyc ladunek z partii na polke (nie kopie), zaplata do pochodzenia/notabla zgodnie z projektem ekonomii.
- LICZBY: 213 sztab + 52 wegla + 45 valyrianskich dziennie -> 0 (kanon: 2.4).
- P1, MALA-SREDNIA (CrashScribe, latka na cudzy mod), gracz: nie zauwazy (poza znikajacymi sztabami valyrianskimi na targach), nowa kampania: nie
  (to co juz jest, zostaje). Ryzyko: BK moze czytac ladunek wedrowcow gdzie indziej (sprawdzic w dekompilacji `Trading`); zaleznosc: zloto
  wedrowcow = projekt ekonomii wiersz 16 i pomiar 169 - jedna paczka razem z nimi.

### P1-D. Kasowanie nadwyzki BK zastapic prawdziwym zaworem (L7)
- CO: prefiks na `BKSettlementBehavior.DeleteOverProduction`: nie kasowac rudy, metali, narzedzi, skory, plotna (nie gnija); drewno i len - zostawic
  male realne psucie (np. 0.2%/d [S] - drewno na skladzie bez dachu), reszte nadwyzki ma lapac zawor produkcji wsi z planu K13 (zawor przy
  zuzyciu < 50% produkcji) i ceny.
- LICZBY: dzis 2%/d powyzej 500 szt.; propozycja 0% (ruda, metale, wyroby), 0.2%/d (drewno, len, welna) [S].
- P1, SREDNIA, gracz: tak (wyroby gracza sprzedane miastu przestana znikac nastepnego dnia - AUDYT-PRODUKCJI-MODY.md:433), nowa kampania: nie.
  Ryzyko: bez zaworu polki drewna i lnu rosna szybciej -> najpierw zawor (K13), potem to. Zaleznosc: K13 zawor; "jak znika - zamykamy" (Z Jeffa).

### P2-E. Skora, tkanina i drewno sztuki przy naprawie wedle rodzaju
- CO: ta sama tabela co P0-A dla skory, plotna i drewna sztuki (drzewce, deski tarczy, lub luku).
- LICZBY [H/S] (brak 0.4 / 0.7): zbroja skorzana skora 8% / 20%; tkanina plotno 5% / 20%; plyta i kolczuga podszycie plotno 5% / 15-20%;
  drzewcowa drewno 10% / 50% (nowe drzewce); tarcza drewno 10% / 35%, skora 15% / 40%; luk drewno 5% / 40% (pekniete lubie = nowy luk).
- P2, MALA, gracz: tak (skora i tkanina drozsze przy ciezkich naprawach, drzewca tansze), nowa kampania: nie. Ryzyko: skora (L8) - robic dopiero
  po P2-G. Zaleznosc: P0-A, P2-G.

### P2-F. Jedna regula dla wlasnych rak gracza
- CO: `Forge.SelfRepair` (Forge.cs:505-509) i `SmithMenu.SelfMendParts` (:716) - te same udzialy co P0-A/P2-E na czesciach `Recipes.For`
  (sztaby -> metal, skora, len, drewno); koniec 0.25 wobec 0.20.
- P2, MALA, gracz: tak (wlasna naprawa tansza w sztabach), nowa kampania: nie. Ryzyko: male; autotest tego nie przecwiczy (test reczny).

### P2-G. Wiecej garbowania (L8)
- CO: rzemioslo miasta 148 i garbarnie - wiekszy udzial skor przerabianych na skore tam, gdzie skory leza (15 tys. nadwyzki).
- LICZBY [S]: celem zapas skory wyprawionej rosnacy co najmniej tak, jak przyrost napraw po P0-A (+10-20 skory/d), czyli przerob skor +30-60/d
  z ich nadwyzki +41/d.
- P2, SREDNIA (K13 rzemioslo miasta), gracz: tak (wiecej skory na targach), nowa kampania: nie.

### P2-H. Opal kuzni przy naprawie za godziny ognia
- CO: drewno kuzni przy naprawie = godziny roboty (`MendLootHoursPerPiece` itd.) x zuzycie paleniska na godzine, nie kg metalu.
- P2, MALA, ryzyko: brak dobrej liczby historycznej (2.2) - robic tylko, gdy znajdzie sie zrodlo; po P0-A roznica jest mala.

### P2-J. Zlom jako towar (cos, czego nie mamy) [S]
- CO: kowale miasta skupuja wraki z polek i od lordow jako "zlom" (wartosc 7-14% nowej, Tower 1330.), trzymaja go w zapasie kowali i lataja
  nim kolczugi i plyty przed sieganiem po rude; karawany moga wozic zlom do miast bez rudy. Domyka obieg metalu bez nowej kopalni.
- P2, SREDNIA, gracz: tak (wraki maja realnego kupca), nowa kampania: nie.

### P2-K. Inne rzeczy, ktorych nie mamy, a pasuja do surowcow [S]
- Rog i sciegno do lukow kompozytowych (Dothrakowie, Essos) jako produkt uboju (dzis luk = drewno + metal).
- Konopie i liny (statki NavalDLC, kusze) - dzis len robi za wszystko.
- Stal valyrianska jako skonczony zasob swiata (tylko przekuwanie w Qohorze) - lore 2.4; po P1-C liczba sztab w swiecie nie rosnie.
- Sezonowosc strzyzy i lnu (raz w roku), zapas surowca na caly rok - zgodne z historia, wtedy rosnacy zapas welny i lnu jesienia jest wlasciwy.

---

## 5. Do zrobienia tej nocy vs na pozniej

### TEJ NOCY (mala, bezpieczna, sprawdzalna autotestem 40 dob): tylko P0-A

Dokladne miejsce:
1. `Armoury/src/MendMaterial.cs`:
   - nowa funkcja `static float MetalShare(ItemObject it, float loss)` - tabela z P0-A (punkty 0 / 0.4 / 0.7 / 1.0, liniowo), rodzaj z `it.ItemType`
     i `it.ArmorComponent.MaterialType` jak w ArmsPricing.cs:176-213; nieznany rodzaj -> stara regula `0.2 x loss`;
   - `NeedsShare(ItemObject it, float share)` (:98-106) -> `NeedsShare(ItemObject it, float share, float loss)`: przy wlaczniku
     `ms = MetalShare(it, loss)`, inaczej `ms = share`;
     `crude = c.MetalKg x 1.25^stopien x ms`; `wood = c.WoodKg x share + c.MetalKg x WorkshopForgeWoodPerMetalKg x ms`; skora i plotno x `share` (bez zmian);
   - `Needs(el)` (:86-89): loss = 1 - PriceMultiplier (przyciete 0..1, jak w `Share` :78-79); `NeedsFor(it, missing)` (:92-96): loss = missing.
   - `Share` i `LaborF` - NIE RUSZAC (robocizna Jeffa).
2. `Armoury/src/Settings.cs` obok :748-751: `public bool MendMetalByKind = true;` z opisem po angielsku (np. "Mending takes new metal as smiths
   really did: rivets and a few plates for plate, rings for mail, almost none for blades - not a fixed share of a new piece; the work is paid
   as before"); potem `python3 tools/gen_mcm.py` (pulapka MCM: Jeffa Armoury.json nie ma klucza - dziala domyslne z kodu, dobrze).
3. Build (kod wyjscia 0), DLL probny tylko na czas autotestu (zgoda Jeffa 07.10), potem przywrocic zatwierdzony DLL; wgranie na stale tylko na "wgraj".

Co sprawdzic w autotescie 40 dob (punkt odniesienia: doby 1-40 testu rocznego [P]):
- "Zuzycie AI": metal kg na naprawiona sztuke 0.18 -> oczekiwane ok. 0.03-0.06; "czeka na material (metal)" 253/d -> ponizej ok. 80;
  naprawione 218/d -> wiecej (ok. 300-450 [S]); "obitych razem" w d40 ponizej 9 784.
- "Towary: iron ... naprawy kowali miasta (135)" 4/d -> 0-2; "hardwood ... naprawy" 22/d -> ponizej 10.
- "Warsztaty:" brak surowca ruda 847/d (d1-40) -> nie gorzej, raczej mniej.
- "Towary: leather" - zapas nie moze przestac rosnac (d40 rocznego: 1 315); "naprawy kowali miasta" skory - zanotowac (ryzyko L8).
- ERROR 0, potkniecia 0; zapis i wczytanie (klucz arm_mendstock).

### NA POZNIEJ (kolejnosc)
1. P1-B (naprawy tylko tego, co nosza) - po 171; jeden autotest roczny lub 40 dob z liczeniem zaleglosci.
2. P1-C (wedrowcy BK bez towaru z niczego, stal valyrianska) - razem z wierszem 16 projektu ekonomii i pomiarem 169.
3. P2-G (garbowanie) -> P2-E (skora, tkanina, drewno sztuki w naprawie) -> P2-F (wlasne rece gracza, test reczny).
4. P1-D (kasowanie BK) - dopiero gdy bedzie zawor K13.
5. P2-J (zlom jako towar), P2-H (opal za godziny), P2-K - pomysly do decyzji Jeffa tam, gdzie zmieniaja rozgrywke (zlom, rog, konopie).

Pytania do Jeffa (tylko te, ktore zmieniaja rozgrywke albo kanon):
- Czy lordowie AI maja w ogole naprawiac lup na zapas, czy tylko to, co nosza ich ludzie (P1-B)? Historycznie - tylko to drugie.
- Czy stal valyrianska ma byc skonczona (tylko istniejace sztaby i miecze, przekuwanie w Qohorze), zgodnie z kanonem (P1-C)?
- Zlom jako towar u kowali (P2-J) - chcesz to widziec w grze?
