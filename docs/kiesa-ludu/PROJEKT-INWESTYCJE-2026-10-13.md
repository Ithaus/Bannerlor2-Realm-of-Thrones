# PROJEKT (kierunek B): LUD INWESTUJE - "miasto buduje sie z kiesy mieszczan" (13.10.2026)

Status: SAM PROJEKT. Kod, gra, ustawienia i zapisy nietkniete, nic nie budowane, bez commita. Jeden z trzech niezaleznych kierunkow
odpowiedzi na pytanie Jeffa z 13.10 ("co zrobic z kasa, ktora lezy ... ludzie moga oszczedzac, ale tez wydaja, inwestuja").

**Zrodla.** ROZP = `docs\kiesa-ludu\ROZPOZNANIE-OBIEG-KIESY-2026-10-13.md`. LOG = `D:\Backup-Bannerlord\logs\2026-10-13-noc34-35\Armoury-2026-10-13_02-15-21.log`
(bieg roczny 364 doby, WGRANIE 36; `:NNN` = numer linii). CSV = `...\Logs\2026-10-13_02-15-21\` (kiesa-ludu.csv, budowy.log). SRC = kod WGRANIA 36
(`...\scratchpad\zb\int\Armoury\src`). G = dekompilacja gry (`dec32\cs_full`), BK = dekompilacja BannerKings (`e4tools\bkfull`). TRIAZ = `docs\test-zbiorczy\TRIAZ-3-bieg-roczny-2026-10-13.md`.
KL = `docs\PROJEKT-KIESA-LUDU-2026-10-09.md`. Skrypty rachunku: `...\scratchpad\kiesa\inw\b1.py` (lokata miast), `b4.py` (budowy w kolejce), `b6.py` (material budow),
`b5.py`, `b7.py` (rachunek trzech lat, miasto po miescie). Kwoty w zl na dobe calego swiata, 1 zl = 1 pens, rok = 364 doby.
**[S]** = szacunek, nie pomiar.

---

## 0. Krotko, prostym jezykiem

1. **Regula:** mieszczanie trzymaja zapas na czarna godzine jak dzis (wyrobnik 10 dni, rzemieslnik 21, rodzina kupiecka 30). Z pieniedzy ponad zapas
   co dobe 2% idzie na budowe, ktora wlasnie czeka w kolejce ich miasta (mury, targ, spichlerz, port, wodociag). Gmina kupuje za to drewno i kamien z targu,
   najmuje ludzi z miasta, a panu oddaje trzecia czesc wydatku - tak jak dzis oddaje mu trzecia czesc akcyzy.
2. **To nie jest nowy system.** Gmina juz istnieje i juz "robi roboty" za 5.6-6.3 tys. zl dziennie - tylko ze dzis nic z nich nie powstaje, a material
   jest oplacany bez zdjecia jednej sztuki z polki. Budowy za pieniadze tez juz sa (placi je pan, 10% dochodu), ale w wojnie stoi 198 z 227.
   Projekt laczy te dwie rzeczy: roboty gminy staja sie prawdziwymi punktami budowy.
3. **Co gracz zobaczy:** bogate miasto rosnie samo (ok. 6-10 poziomow budowli rocznie w najbogatszych), biedne stoi. W Twoim miescie budowe z kolejki
   oplacaja mieszczanie, a Ty dostajesz trzecia.
4. **Uczciwie o wielkosci.** To jest dobry sposob na pieniadz, ktory lezy, ale MALY. Z kazdych 3 zl wydanych przez lud 1.5 zl to place - wracaja do
   tych samych mieszczan. Naprawde wychodzi 1 zl do pana i 0.5 zl za material. A materialu (drewna, kamienia) swiat robi tyle, ile robi: wolne jest
   ok. 800 ladunkow drewna dziennie.
5. **Liczby:** z lokaty ludu wyjdzie ok. 26 tys. zl/d w pierwszym roku i ok. 17 tys. pozniej; netto z kies ubywa 13 i 9 tys. zl/d. Nadwyzka ludu to dzis
   37 tys. zl/d (koniec roku) do 84 tys. (srednia roku). Czyli sam kierunek B zatrzymuje jedna czwarta do jednej trzeciej koncowej nadwyzki
   (a sredniej rocznej - jedna dziesiata do jednej szostej).
   Z jednym parametrem wiecej (wsie tna do 1.8 x zwyklego drewna, gdy swiat go chce) - ok. 24 tys. zl/d netto, czyli dwie trzecie koncowej nadwyzki.
6. **Dla wojska:** do panow dochodzi tyle, ile netto wychodzi z kies: 9-24 tys. zl/d. To realnie 0.3-1.1 tys. ludzi w partiach (gorna granica 0.9-3.1 tys.).
   Kierunek B nie odbuduje armii z 62 do 93 tys. - zaden kierunek tego nie zrobi z samej kiesy ludu, bo cala nadwyzka ludu to 37-84 tys. zl/d.
7. **Czego NIE proponuje, choc bylo w zadaniu:** udzialow w warsztatach i karawanach. Powod jest prosty: inwestycja, ktora przynosi zysk, sciaga do
   inwestora WIECEJ zlota, niz wlozyl. Kiesy ludu by rosly szybciej, a panom ubylo. Liczby w rozdz. 2.
8. **Rada:** brac B jako "druga noge" - tania (wielkosc M), bez zapisu, naprawia trzy stare dziury - obok kierunku, ktory da glowny odplyw.
   Sam B wystarczy tylko wtedy, gdy nadwyzka ludu sama zgasnie ponizej ok. 10-25 tys. zl/d (ROZP 1.2 przewiduje zero ok. doby 420-450 - to ekstrapolacja).

---

## 1. Regula

### 1.1 Slowami

| Kto | Co robi | Kiedy | Ile |
|---|---|---|---|
| lud miasta (W, R, P) | odklada na roboty miejskie | co dobe, gdy ma pieniadz ponad zapas dni i miasto ma budowe w kolejce | 2% tego, co ponad zapas |
| gmina miasta | kupuje material budowy z polki targu (drewno, kamien, glina, narzedzia, marmur, deski) | zaraz potem | 1/6 wydatku; kazda sztuka po cenie targu, nie drozej niz 2 x zwykla cena |
| gmina miasta | placi murarzom i robotnikom (ludziom tego miasta) | zaraz potem | 1/2 wydatku; najwyzej 3 pensy plac na pens materialu (regula Jeffa 05.10); nie wiecej rak niz co dwudziesty mieszkaniec |
| gmina miasta | oddaje panu trzecia czesc (farma) | zaraz potem | 1/3 wydatku - ta sama trzecia, ktora pan dzis bierze z wydatkow gminy |
| budowla | dostaje punkty budowy | nastepny tick gry | (material + place / poziom plac miasta) / cena punktu (24 zl cywilne, 48 zl wojskowe) - ta sama funkcja co przy robotach pana |
| nikt | - | brak budowy w kolejce, oblezenie, pusta polka materialu, lud bez nadwyzki | pieniadz zostaje w kiesach, pan nie dostaje nic |

Zamek: ta sama regula, placi podzamcze (nie ma gminy - wydaje wprost); material z najblizszego miasta jak dzis przy robotach pana.
Wies: bez zmian (budowle wsi BK ida z sily roboczej, to osobny mechanizm - rozdz. 6).

### 1.2 Parametry z rachunkiem

| Klucz (nowy) | Wartosc | Skad |
|---|---:|---|
| `TownFolkWorks` (wylacznik) | wlaczony | - |
| `TownFolkWorksShare` | 0.02 na dobe | ma nie byc waskim gardlem: 2% z 23.8 mln lokaty = 476 tys. zl/d oferty wobec 26 tys. zl/d, ktore naprawde da sie wydac (rozdz. 3); wezsze sa material, rece i koszt poziomu |
| `TownFolkWorksHands` | 0.05 | najwyzej 5% glow wyrobnikow miasta przy robotach gminy x dniowka 3 zl (`WorkshopWagePerDay`, SRC Settings.cs:601) x poziom plac miasta; Riverrun: 15 178 x 0.05 x 3 = 2 277 zl plac dziennie = roboty 3 036 zl/d, czyli poziom budowli (3 470 pkt srednio w miastach, b4.py; 83-104 tys. zl) w ok. miesiac - z przerwa na wybor nastepnej budowy 6-10 poziomow rocznie, tempo gry bez modow. Wielkie budowy sredniowiecza zatrudnialy 1-3% miasta [S]; 5% to gorna granica |
| `BuildMaterialPriceCap` | 2.0 x wartosc sztuki | gmina z pelna kiesa kupilaby ostatnia belke za 10 x cene, a punkty licza sie z zaplaty - wiec bez sufitu budowaloby sie z samego zlota. Sufit dla OBU platnikow (pan i gmina) w `BuyMaterials`; dzis indeks cen materialu budow 0.72-1.17 (b6.py), wiec panom nic sie nie zmienia |
| trzecia pana | 1/3 | istniejaca stala `GFarm` (SRC TownFolk.cs:58; KL :122 "1/3 jako ryczalt (farma) panu miasta") - bez nowego klucza |
| material / place | 25% / 75% robot | istniejacy `BuildMaterialShare` (SRC Settings.cs:566) |
| zapas dni | 10 / 21 / 30 | istniejace `TownFolkReserveDays` i stale `RdR`, `RdP` (SRC Settings.cs:1613, TownFolk.cs:54) |

Jedna regula na zjawisko: roboty gminy i roboty pana to ta sama funkcja (`BuildFunding`) z dwoma platnikami. Wojna wstrzymuje cywilne budowy pana
(Jeff 05.10: "w wojnie 0, chyba ze mury") - nie wstrzymuje mieszczan, bo to ich pieniadz i ich miasto.

### 1.3 Co juz jest w kodzie, a czego brakuje

- Gmina miasta: kiesa `KG`, wplywy z akcyzy 2% i cechu 1%; wydaje 7% stanu dziennie: 1/3 panu, 2/3 "roboty" (75% place W/R, 25% "material")
  (SRC TownFolk.Live.cs:793-808). Dzis: farma 2 797-3 135 zl/d, place 4 206-4 659 zl/d, material ok. 1.4-1.6 tys. zl/d (ROZP 1.1; LOG :129003 "gminy (place) 4659").
  **Dziura 1:** te roboty nie daja ani jednego punktu budowy. **Dziura 2:** "material" to przelew do kasy kupcow bez zdjecia sztuki z polki
  (`town.ChangeGold(mat)`, :805) - to samo przy budowie septow z dziesieciny (SRC TownFolk.Tithe.cs:129, :172; 2 990 zl/d). KL obiecywal "nikt nie placi za nic" (KL :228-229).
- Budowy za pieniadze pana: `BuildFunding.Daily` (SRC BuildFunding.cs:121-202): 10% dochodu, 25% material z polki (`BuyMaterials` :239-266, sztuka po sztuce `ShelfBuy.Take`),
  75% place do kiesy ludu (`TownFolk.Earn(st, labourI, KBuildLabour)` :187), punkty `PointsFor` (:209-212), moc budowy gry i BK zastapiona (`PowerPostfix` :62-74).
  Bieg roczny: wydano 13 738 zl/d (material 3 472), 319 pkt/d, 69 poziomow w roku; d364: idzie 28, stoi 199, z tego 176 to cywilne wstrzymane wojna (LOG :128411-:128412; b6.py).
  **Dziura 3:** 87% budow swiata stoi, bo jedyny platnik ma wojne.
- KL od poczatku pisal: "gmina buduje mury i bruki i placi panu ryczalt (farma)" (KL :32, :105, :122-123) oraz "nadwyzka ponad zapas kupuje PRAWDZIWY towar albo prace"
  (KL L4, :60). Projekt B to dokonczenie tej decyzji, nie nowa.

---

## 2. Dlaczego tylko budowa - ocena pozostalych pomyslow z zadania

Zasada, ktora wyszla z rachunku i dotyczy calego kierunku B: **zloto wraca do pana tylko tym, co lud zaplaci komus spoza ludu.** Place wracaja do ludu.
Zysk z inwestycji wraca do ludu z nawiazka.

| Pomysl | Stan w silniku (sprawdzone) | Liczby | Werdykt |
|---|---|---|---|
| **Budowa miasta** | gotowe (1.3); efekty budowli zywe: mury +5 000 pojemnosci ludnosci na poziom (BK BKGrowthModel.cs:101), magazyn +5/10/15% produkcji warsztatow (G DefaultBuildingTypes.cs:220-223, DefaultWorkshopModel.cs:48) i +30% produkcji zywnosci ludu na poziom (BK BKFoodModel.cs:155), drogi +5/10/15% produkcji wsi (G :239-242, DefaultVillageProductionCalculatorModel.cs:65), port i przystan +8-10% sily handlu na poziom (BK BKEconomyModel.cs:229-234, :466-471), dobrobyt i lojalnosc (BK BKProsperityModel.cs:112, BKLoyaltyModel.cs:308) | rozdz. 3 | **TAK - caly projekt** |
| Kapital obrotowy warsztatow | kapital nie jest waskim gardlem: "bez kapitalu na utrzymanie 0", "niezaplacone (pusty kapital) 0", kapital razem 2.57 -> 2.53 mln przez rok (LOG :128727; ROZP 3.1). Warsztat stoi, gdy utarg <= wsad + place, gdy kasa miasta nie ma na wyrob albo gdy wsadu nie ma na polce (G WorkshopsCampaignBehavior.cs:776-792, :884-909) | 104 z 291 stoi, z tego 35 to zbrojne na zamowienie (fletcher 10, armorsmithy 4, weaponsmithy 10, barding 11) | NIE - dolozenie zlota nie ruszy ani jednego cyklu |
| Kupno warsztatu przez cech | wlascicielem musi byc bohater (`Workshop.Owner`); zysk idzie do wlasciciela z modelu finansow (G DefaultClanFinanceModel.cs:869-872, :908-920) - trzeba by przechwytywac wyplate | 291 warsztatow x mediana ceny 9 145 zl = 2.7 mln jednorazowo do notabli; potem zysk 4.8-7.6 tys. zl/d plynie DO ludu (LOG :128727) | NIE - jednorazowy maly odplyw, potem staly doplyw do kies |
| Nowe warsztaty | liczba miejsc stala od zalozenia swiata (`InitializeWorkshops(DefaultWorkshopCountInSettlement)`, G WorkshopsCampaignBehavior.cs:1340-1346, DefaultWorkshopModel.cs:27); AI zmienia typ tylko przy bankructwie (:1093-1099), a w d364 bankructw 0 (LOG :128727) | - | NIE - tablica w zapisie i w oknach gry; duze ryzyko |
| Karawany (udzial w zysku) | kapital nowej karawany notabla: kiesa notabla ponad 4 500 + nadwyzka kasy miasta (SRC NotableGold.cs:386-414) - latwo wstawic kiese ludu jako platnika. Wyplata wlascicielowi = (kasa karawany - kapital poczatkowy) / wygladzenie (G DefaultClanFinanceModel.cs:864-867) | kapital nowych 29 tys. zl/d rocznie, 57 tys. w d364, z kas miast 25-47 tys. (ROZP 4 poz. 13; LOG :128981). Wyplaty wlascicielom ok. 67-74 tys. zl/d, a doplaty notabli do zoldu karawan 56-90 tys. zl/d: flota jako calosc wychodzi na zero | NIE jako srodek obiegu. Udzial w wyplatach proporcjonalny do kapitalu dalby ludowi 57-67 tys. zl/d za 25-47 tys. wlozonych - kiesy rosna, kasy kupcow (i zawor panow) chudna. Uczciwy udzial w zysku netto to ok. 0 - wtedy to nie inwestycja, tylko palenie zlota |
| Statki | port, przystan i stocznia to budowle miasta (BK BKBuildings.cs:76-85; w kolejkach d364: Harbor 4 + 12, Port 3 + 6, Shipyard 1 - b4.py) | w robotach gminy | objete budowa |
| Cechy BK | typ `Guild` istnieje, `MakeGuild(Town)` nikt nie wola i nikt nie przypisuje (BK Guild.cs:58; przeszukanie calego BK) | - | martwe - nie opieram sie |
| Budowle wsi BK (mlyn, tartak +5/10/15% drewna, infrastruktura rolna) | osobna kolejka i moc z sily roboczej (BK DefaultVillageBuildings.cs:89-111, VillageData.cs:205-229) | kiesy wsi 10.2 mln | pozniej, osobny projekt (L) - trzeba wejsc w dane BK |

---

## 3. Przeplywy z liczbami

### 3.1 Ile da sie naprawde wydac - trzy waskie gardla

**a) Koszt budow w kolejce.** d364: miasta 97 - wojskowe 22 (116 601 pkt = 5.60 mln zl), cywilne 75 (166 884 pkt = 4.01 mln zl); zamki 130 - 3.93 mln zl (b4.py).
50 miast ma lokate i budowe; ich biezace poziomy kosztuja 5.39 mln zl wobec 23.78 mln lokaty. Najbogatsze: Riverrun - Tax Office, zostalo 12 456 zl, lokata 1.29 mln (104 x);
Harrenhal - Mason 115 200 zl, lokata 1.35 mln (12 x); Dreadfort - mury 359 040 zl, lokata 1.20 mln (3 x). Po poziomie AI wybiera nastepny z szansa 10% na dobe
(G BuildingsCampaignBehavior.cs:75-80), czyli srednio 10 dob przerwy. Cale drzewo budowli miasta to ok. 175 tys. pkt = ok. 5 mln zl; do zbudowania zostalo ok. 380 mln zl
we wszystkich miastach [S - log podaje tylko biezacy poziom].

**b) Rece.** Pulap 5% glow x 3 zl: swiat 1.34 mln wyrobnikow -> najwyzej 200 tys. zl plac dziennie; wiaze tylko w miastach z wielka lokata (b5.py: 8-22 miast).

**c) Material - to jest prawdziwa granica.** Zuzycie budow panow w biegu rocznym (b6.py, linie "Towary" :128778-:128811):

| Okno | drewno na budowy / produkcja / cale zuzycie (ladunki na dobe) | polki miast | wydano na budowy | zl robot na ladunek drewna |
|---|---|---:|---:|---:|
| d1-31 | 1 098 / 1 689 / 1 605 | 1 307 | 25 976 | 23.7 |
| d91-181 | 902 / 2 122 / 1 798 | 10 192 | 15 368 | 17.0 |
| d271-364 | 572 / 1 811 / 1 307 | 103 180 | 8 454 | 14.8 |
| rok | 798 / 1 992 / 1 581 | 41 143 | 13 738 | 17.2 |

Regulator wsi (221) juz dzis dopasowuje wyrab do zuzycia: drewno szlo na sufit x1.30 do d308, potem x0.90 i x0.82, bo "stos 137 991 = 66 dni zuzycia",
"zuzycie/produkcja 0.73 (117 469 / 161 692)" w oknie 56 dob (LOG :109252, :119181; SRC HarvestValve.cs, `VillageOutputStep` = 1.3, Settings.cs:1690).
Przy suficie x1.30 wsie daja 161 692 / 56 = 2 887 ladunkow na dobe, swiat zuzywa 2 098 - **wolne 790 ladunkow na dobe**, do tego stos 117 tys. ladunkow na polkach miast
(322 na dobe przez rok). Glina x0.83, wapien x0.64 - tez przyciete z braku zbytu (LOG :99269).
Z tego prawdziwe roboty z wlasnych pieniedzy gminy (dzis 5.6-6.3 tys. zl/d "na niby") wezma ok. 300 ladunkow. **Dla lokaty zostaje ok. 800 ladunkow na dobe w pierwszym roku
i ok. 490 potem**, przy 23 zl robot na ladunek [S: srednia z okien z indeksem ceny materialu ok. 1.0-1.2] = roboty 18 i 11 tys. zl/d.
W pokoju panowie buduja wszystko - budowy zuzywaly wtedy ok. 1 170-1 320 ladunkow drewna na dobe (pomiar z testow 07.10, SRC VillageWoodlot.cs:15-16) - i wolnego drewna prawie nie ma.

### 3.2 Rachunek trzech lat, miasto po miescie (b7.py)

Start = stan d364 (kiesa W 31.6 mln, lokata 23.8 mln, 97 miast). Nadwyzka miasta = jego srednia z 52 migawek tygodniowych, skalowana do sumy swiata.
Tysiace zl na dobe; "z lokaty" = wszystko, co lud wyplaca; "netto" = to, co naprawde opuszcza lud (trzecia pana + 87% materialu + 3.5% plac w podatkach;
13% materialu wraca jako robocizna handlu i obrot, place wracaja w calosci).

| Wariant | rok 1: z lokaty = pan + material + place; netto | rok 2 i 3: z lokaty; netto | kiesa W po 1 / 3 latach przy nadwyzce 37 tys. | ... przy 18 tys. | ... przy 0 |
|---|---|---|---|---|---|
| bez zmian | 0 | 0 | 46.2 / 75.8 mln | 38.8 / 53.5 | 31.6 / 31.6 |
| **B1: roboty gminy, dzisiejszy regulator wsi (x1.3)** | 26.4 = 8.8 + 4.4 + 13.2; **netto 13.1** | 17.4 = 5.8 + 2.9 + 8.7; **netto 8.6** | 41.4 / 64.8 | 34.1 / 42.5 | 26.9 / 20.6 |
| **B1 + B2: sufit wyrebu wsi 1.3 -> 1.8** | 48.0 = 16.0 + 8.0 + 24.0; **netto 23.8** | to samo | 37.5 / 49.8 | 30.2 / 27.5 | 23.0 / 8.2 |
| gdyby materialu nie brakowalo (gorna granica reguly) | 134.6 = 44.9 + 22.4 + 67.3; netto 66.8 | 96.2 i 86.4; netto 47.7 i 42.8 | 21.9 / 18.6 | 16.8 / 10.9 | 12.5 / 7.9 |

Przy sredniej rocznej nadwyzce 84 tys. zl/d: B1 bierze 13.1 -> 8.6, B1 + B2 23.8, gorna granica 77.1 (b7.py).

**Stan rownowagi (przyrost kies ok. 0):** B1 trzyma kiesy w miejscu przy nadwyzce ludu do ok. 9-13 tys. zl/d, B1 + B2 do ok. 24 tys. zl/d.
Dzis nadwyzka to 37 tys. (d301-364) i gasnie o ok. 13 tys. na miesiac (ROZP 1.2). Jesli zgasnie tak, jak przewiduje ROZP (zero ok. d420-450 [S]), B zamienia lezacy zapas w mury:
24 mln lokaty schodzi w 3 lata do 12.7 mln (B1) albo do 0.3 mln (B1 + B2). Jesli nie zgasnie - B zwalnia przyrost o jedna czwarta do dwoch trzecich i potrzebny jest drugi kierunek.

**Zapas, ktory zostaje i ma sens:** 10 / 21 / 30 dni potrzeb - dzis 7.8 mln u wyrobnikow (31.6 - 23.8) i ok. 4.9 mln u R i P; to jest "na czarna godzine" i regula go nie rusza.

### 3.3 "Ile z 80 tys. zl/d nadwyzki trafia do kogo" (B1 + B2, stan trwaly)

| Dokad | zl/d | Uwaga |
|---|---:|---|
| lud wyplaca z lokaty | 48 000 | |
| - place murarzy i robotnikow (W 60 / R 40) | 24 000 | wraca do ludu tego samego miasta; od tego podatek pana 1-2% i korony do 3% (ok. 840) |
| - pan miasta (trzecia) | 16 000 | wprost do kiesy glowy rodu, liczy sie do dochodu z ziemi |
| - material do kas kupcow | 8 000 | ok. 1 400-1 600 ladunkow drewna, gliny i kamienia; z tego 13% wraca do ludu (robocizna handlu 10%, obrot 3%) |
| **netto ubywa ludowi** | **23 800** | 16 000 + 6 960 + 840 |
| zostaje w kiesach z 80 tys. nadwyzki | ok. 56 000 | tego kierunek B nie rusza |

Pieniadz za material idzie dalej znanymi drogami: kupcy placa wsiom za drewno z wozow (podatek pana z utargu 35-55%, danina wojenna korony 2.5%/d nadwyzki wsi),
reszta to nadwyzka kasy - zawor 7% dziennie panu i koronie po polowie (ROZP 4 poz. 4, 6); korona tego samego dnia oddaje rodom (renty 180, zwrot zoldu 165).

### 3.4 Ile dochodzi do panow i ile to wojska

Do panow i koron razem dochodzi tyle, ile netto wychodzi z kies: **B1 13.1 tys. zl/d w roku 1, 8.6 tys. potem; B1 + B2 23.8 tys.**
Przelicznik TRIAZ O1-7 pkt 5: 10 tys. zl/d = realnie 0.3-0.45 tys. ludzi w partiach (bo tylko 117 z 332 rodow stoi na pulapie zoldu), gorna granica 1-1.3 tys.

| Wariant | do panow zl/d | wojsko realnie | gorna granica |
|---|---:|---:|---:|
| B1 rok 1 / potem | 13 100 / 8 600 | +0.4-0.6 tys. / +0.3-0.4 tys. | +1.3-1.7 / +0.9-1.1 tys. |
| B1 + B2 | 23 800 | +0.7-1.1 tys. | +2.4-3.1 tys. |
| gorna granica reguly (rok 1) | 66 800 | +2.0-3.0 tys. | +6.7-8.7 tys. |

Sufit kazdego kierunku jest ten sam: nadwyzka ludu 37-84 tys. zl/d to najwyzej 1.1-3.8 tys. ludzi realnie. Spadek 93 -> 62 tys. ma inne glowne przyczyny (TRIAZ O1-1: drozszy zolnierz, kompanie najemne).

---

## 4. Dowod zamknietego obiegu

| # | Przelew | Platnik -> odbiorca | Jak w kodzie | Sztuka |
|---|---|---|---|---|
| 1 | skladka na roboty | kiesa W / R / P -> gmina tego miasta | `Move(f, c, 3, x)` (SRC TownFolk.Live.cs:274-281) - wewnatrz ludu, najwyzej stan kiesy; tylko tyle, ile roboty zaraz wydadza | - |
| 2 | material | gmina -> kasa kupcow miasta | `ShelfBuy.Take` zdejmuje sztuke z polki, cena liczona od nowa po kazdej (SRC ShelfBuy.cs; BuildFunding.cs:259), potem `Town.ChangeGold(+)`, `TownFolk.Sale(MixBuild)` (podstawa robocizny), `OreLedger.NoteBuild` | z polki targu; ksiega "Towary" pozycja "budowy" |
| 3 | place | gmina -> kiesy W 60% / R 40% tego miasta | `TownFolk.Earn(st, x, KBuildLabour)` (SRC TownFolk.Live.cs:156, :122) - zarobek okna, od ktorego jutro ida podatki pana, korony i dziesiecina | praca |
| 4 | trzecia | gmina -> glowa rodu pana | `PayLord(f, 3, x, d)` (SRC TownFolk.Live.cs:298-319): `ChangeHeroGold`, `RentToday` (dochod z ziemi), licznik gracza | - |
| 5 | punkty budowy | - | `PointsFor(material, place, cena punktu, miasto)` dopisane do `_funded[town]` obok punktow pana (SRC BuildFunding.cs:193, :209); gra dodaje je w `TickCurrentBuildingForTown` (G BuildingsCampaignBehavior.cs:100-123) | - |

Nic z niczego: kazda zlotowka schodzi z kiesy `long` do zera i ani grosza dalej; wyplata dnia najwyzej tyle, ile kosztuje reszta biezacego poziomu
(zadnych punktow, ktore przepadaja po awansie). Nic w nicosc: czego nie wydano, zostaje w kiesie klasy. Nowych posiadaczy zlota nie ma (kiesy klas, gmina i podzamcze sa w ksiedze od B1 - posiadacze H22-H24),
suma "Pieniadz swiata" i kontrola "zmiana kies = wplywy - wydatki, r = 0" bez zmian definicji.

Przy okazji znika dziura 2: wlasne roboty gminy (akcyza) przestaja placic kupcom za material, ktorego nie zdjeto z polki.

Przypadki brzegowe (100%): brak zywego pana - trzecia zostaje w gminie (istniejacy odbiorca zapasowy, :306-310); oblezenie - bez robot i bez trzeciej (`IsUnderSiege`, SRC BuildFunding.cs:134);
kolejka pusta albo projekt dzienny (festyn, domy - w grze darmowe i bez punktow, G DefaultBuildingTypes.cs:297-329) - bez robot; miasto zmienia pana - gra czysci kolejke (G BuildingsCampaignBehavior.cs:23-29),
punkty poziomu zostaja; pusta polka materialu - bez robot tego dnia (licznik); miasto bez ludzi BK albo przed kalibracja - bez robot; kiesa wylaczona (`TownFolkLive`) - regula martwa.

---

## 5. Miasta biedne, zamki, wsie

- **Biedne miasta:** 47 z 97 miast nie ma ani zlotowki ponad zapas (b1.py), 31 najbiedniejszych ma razem 1.5% zlota ludu (ROZP 2). U nich regula nie robi nic:
  nie bierze i nie buduje. Buduja tylko tyle, ile pan (jak dzis) plus prawdziwe roboty z akcyzy - po kilkadziesiat zl dziennie. Rachunek: roboty powyzej 30 tys. zl w 3 lata
  ma 12-29 miast z 97 (b7.py). To jest zamierzone ("biedne stoi"), ale rozjazd bedzie rosl: bogate miasto dostaje magazyn i port, wiec zarabia jeszcze wiecej.
- **Glod:** regula bierze tylko ponad zapas dni, wiec "stac na chleb" sie nie zmienia. Place robot trafiaja do wyrobnikow - w miescie z robotami biedniejsi maja wiecej.
- **Zamki:** podzamcza maja 3.0 mln, mediana 1 824 zl (ROZP 2) - 60 ze 130 ponizej dnia koszyka. Ta sama regula, bez gminy; w praktyce buduje ok. 30 podzamczy z zapasem ponad 20 dni.
  Material kupuja w najblizszym miescie, wiec konkuruja o to samo drewno.
- **Wsie:** bez zmian. Zyskuja posrednio: sprzedaja wiecej drewna, gliny i kamienia (regulator wsi podniesie wyrab z x0.82 z powrotem do sufitu).

---

## 6. Gracz

- **Te same reguly.** W miescie gracza mieszczanie oplacaja budowe z JEGO kolejki; gracz dostaje trzecia jak kazdy pan. `PaidConstructionPlayer` (10% dochodu gracza na budowy)
  dziala jak dotad obok. Gdy gracz ma ten klucz wylaczony, punkty gminy dopisuja sie do mocy budowy gry zamiast ja zastepowac (dzis `Covered` :40-45 odcina takie lenna).
- **Decyzja gracza:** to on uklada kolejke, wiec on decyduje, na co idzie pieniadz jego mieszczan. Kolejka pusta = lud nic nie wydaje i gracz nie dostaje trzeciej.
- **Jego inwestycje i dlug:** bez zmian (warsztaty, karawany, Bank jak dzis). Przyspieszenie budowy zlotem z gry dziala dalej (`Boost`, SRC BuildFunding.cs:49-58).
- **Nowy powod, zeby dbac o miasto:** bogaty lud = darmowe budowle i trzecia. Oblezenie, glod i bieda mieszczan zatrzymuja roboty.

---

## 7. Co widzi gracz (napisy po angielsku)

- Zarzadzanie miastem, dymek mocy budowy - druga linia obok "Paid works (wages and materials from the owner's purse)": **"Town works (paid by the townsfolk)"**.
- Okno "People of this land" (`PeopleMenu`, zdanie pod "Bread:", ta sama droga co `BreadLine`, SRC TownFolk.Live.cs:1468-1505):
  - "Works: the townsfolk put 1,240 d into the Marketplace today - 310 d for timber and stone, 930 d in wages; a further 620 d went to the lord as his third."
  - "Works: the townsfolk have coin to build with, but the market has no timber or stone."
  - "Works: the townsfolk have nothing to spare for building."
  - "Works: nothing is waiting to be built here." / "Works: stopped - the town is under siege."
- Komunikat w dzienniku, tylko dla miast gracza, po awansie budowli: "The townsfolk of Riverrun have finished the Marketplace (level 2) at their own cost."
- MCM: "Townsfolk Build Their Town", "Townsfolk Works Share", "Townsfolk Works Hands", "Building Material Price Cap".
- Log doby (po polsku): "Roboty gmin (B): dzien N | miast z robota X (bez budowy a, oblezone b, bez materialu c, na pulapie rak d, bez nadwyzki e) | z kies W / R / P i z akcyzy |
  material: sztuki wedlug towaru, zl, sredni indeks ceny | place | trzecia pana (w tym gracz) | punkty, poziomy ukonczone | lokata po".

---

## 8. Skutki uboczne i ryzyka

| Co | Skutek | Ocena |
|---|---|---|
| ceny drewna, gliny, kamienia | dzis indeks 0.72-0.91 i stos 66-76 dni zuzycia; roboty zjedza stos w ok. rok, cena wroci do ok. 1.0-1.3; sufit 2 x chroni przed licytacja | korzystne - regulator wsi przestaje ciac wyrab |
| narzedzia | budowy biora 5-20 szt./d ze 84-110 produkowanych (b6.py); wsie kupuja 42, mieszczanie 31 - polka miast 151 szt. | **ryzyko**: roboty gminy moga wykupic narzedzia wsiom; prog testu ponizej. Wyjscie zapasowe: gmina nie kupuje narzedzi, gdy na polce jest mniej niz 5 |
| budowy pana | ten sam material - w wojnie wolny, w pokoju panowie zuzywali 1 098 ladunkow i wtedy roboty gminy kurcza sie prawie do zera | zamierzone: kto pierwszy na targu; kolejnosc w bloku - pan przed gmina |
| kasy kupcow | +4-8 tys. zl/d sprzedazy; zawor panu i koronie rosnie o ok. polowe tego | drobne |
| dochod panow z ziemi | +6-16 tys. zl/d trzeciej (dzis ziemia 161-224 tys., ROZP 3.2) = +3-10%; trafia do panow BOGATYCH miast | **ryzyko**: jeszcze wiekszy rozjazd rodow (gorne 10% ma juz 57% zlota - TRIAZ O1-7) |
| renty korony | bez zmian reguly; korona dostaje troche wiecej z zaworu i podatku wojennego od plac (do 3%) | drobne |
| lojalnosc, dobrobyt, ludnosc | ukonczone budowle podnosza je regulami BK; mury +5 000 pojemnosci ludnosci, magazyn +30% zywnosci | korzystne, ale to takze wiecej gab - sprawdzic ksiege ludzi |
| produkcja | magazyn +5/10/15% cykli warsztatow, drogi +5/10/15% plonu wsi - wiecej towaru na pustych polkach (ROZP 1.3: piwo 0.2 doby, plotno 0.3) | jedyny kierunek, ktory rusza przyczyne nadwyzki; wielkosc nieznana [S] |
| koniec drzewa budowli | najbogatsze miasto przy 1.1-1.4 mln zl robot rocznie skonczy wszystko w ok. 3-4 lata [S]; potem lokata znow rosnie | **ryzyko dlugiej kampanii**: ujscie jest skonczone |
| "trzecia pana" | bez niej lud placi tylko za material: netto 28-32 tys. zl/d nawet bez limitu drewna (b5.py "bez trzeciej pana"), a z limitem 3-8 tys. (material 2.9-8.0 tys. x 0.975) | to ona niesie zloto do panow; jesli Jeff uzna ja za sztuczna, kierunek B nie ma sensu dla wojska |
| punkty z zaplaty | drozszy material = mniej sztuk na punkt (istniejaca cecha `PointsFor`); przy sufcie 2 x najwyzej dwukrotnie | znane; nie poglebiam |
| oferta "lepszego towaru" | bez zmian (15% nadwyzki jako budzet na jutro); roboty biora 2% tej samej nadwyzki dopiero po niej | bez kolizji |

Dwa najwieksze ryzyka: **(1) wielkosc** - material i wracajace place sprawiaja, ze B zatrzymuje 9-24 tys. z 37-84 tys. zl/d; **(2) trzecia pana** - caly doplyw do panow
stoi na jednej istniejacej stalej, a korzystaja glownie panowie najbogatszych miast.

---

## 9. Wpiecia w kod

| Plik | Miejsce | Zmiana |
|---|---|---|
| `TownFolk.Live.cs` | `TownLive` krok 4 (:793-808) | roboty gminy z akcyzy: zamiast `Move` plac i `ChangeGold(mat)` - budzet robot przekazany do `BuildFunding` (pole doby w `Folk`, bez zapisu); farma jak dzis |
| `TownFolk.Live.cs` | `TownLive` krok 5 (:809-829) | obok `Offer[c]`: `Works[c] = TownFolkWorksShare x nadwyzka klasy` (liczba, nie przelew) |
| `TownFolk.Live.cs` | `CastleLiveDay` (:890-893) | to samo dla podzamcza |
| `TownFolk.Live.cs` | nowa `PayWorks(st, material, place, trzecia)` | zdjecie z kies proporcjonalnie do `Works[c]` (najpierw z `KG`), `Earn`, `PayLord`, liczniki `_ctlOut` |
| `BuildFunding.cs` | `Daily` (:121-202) | druga petla po miastach i zamkach z budzetem ludu: te same `BuyMaterials`, `PointsFor`; pulap rak; pulap reszty poziomu; `_funded[town] += pkt`; bez warunku wojny |
| `BuildFunding.cs` | `BuyMaterials` (:239-266) | sufit ceny (parametr `cap` w `ShelfBuy.Take` juz istnieje - SRC ShelfBuy.cs, E8/Z); sztuki do 5 zl w wozach po 10 (jedna wycena na woz) |
| `BuildFunding.cs` | `PowerPostfix` (:62-74), `Covered` (:40-45) | druga linia dymka; lenna gracza bez `PaidConstructionPlayer` - dopisanie do mocy gry |
| `TownFolk.Live.cs` | `BreadLine` (:1468) + `PeopleMenu` | zdanie "Works:" |
| `Settings.cs`, `tools/gen_mcm.py` | 4 klucze | po dodaniu `gen_mcm.py`; sprawdzic `Armoury.json` Jeffa (CLAUDE.md 7) |
| B2: `Settings.cs:1690` | `VillageOutputStep` 1.3 -> 1.8 | sama wartosc; dotyczy wszystkich towarow wsi poza zywnoscia (len i ruda zlota tez stoja dzis na suficie x1.30 - LOG :119181) |

Kolejnosc doby bez zmian: `CastlePurse.Daily` -> `TownFolk.Daily` -> `BuildFunding.Daily` -> korona -> `ClanBudget.Daily` (SRC ArmouryBehavior.cs:1595-1672) - trzecia pana wchodzi do `RentToday` przed budzetem rodow tej samej doby.

**Koszt czasu.** Dzis `BuildFunding.Daily` 8.7-9.6 ms (LOG :128377, :128730), `ShelfBuy` 2 mikrosekundy na sztuke (LOG :128961). Material gmin to ok. 800-1 600 ladunkow dziennie:
sztuka po sztuce 1.6-3.2 ms, w wozach po 10 ok. 0.2-0.3 ms. Reszta (97 + 130 osad, po kilka dzialan) ponizej 0.2 ms. **Razem ok. +0.5 ms na dobe**; bez wozow do +3.5 ms.
Wozy po 10 to odstepstwo od "kazda sztuka po swojej cenie" (Jeff 09.10) o najwyzej jedna wycene na 10 tanich sztuk - blad ceny do zmierzenia w linii "Ceny hurtu" [S: ponizej 2%].

**Wylacznik:** `TownFolkWorks` - wylaczony = stan dzisiejszy co do zlotowki (roboty gminy "na niby" jak teraz). **Zapis:** bez nowych pol (`KG` i kiesy sa w `arm_townfolk`,
budzet robot liczony w tej samej dobie). Stary zapis dziala bez migracji.

---

## 10. Wielkosc i paczki

| Paczka | Co | Wielkosc | Uwaga |
|---|---|---|---|
| B0 | sam pomiar w logu: lokata miast z budowa w kolejce, material budowy na polce miasta (sztuki, cena, doplyw doby), ile oferty nadwyzki naprawde kupilo (brak wskazany w ROZP 6 i TRIAZ) | S | bez zmiany gry; daje liczby do progow B1 |
| B1 | roboty gminy w miastach i na podzamczach, sufit ceny materialu, napisy, linia logu | M | 1 workflow z 1 recenzja; nowa kampania niepotrzebna |
| B2 | `VillageOutputStep` 1.3 -> 1.8 | S | tylko po biegu z B1, jesli material okaze sie granica (linia "bez materialu") |

Calosc: **M**. Odrzucone z zakresu (rozdz. 2): warsztaty, karawany - nie buduje.

---

## 11. Progi do testu rocznego (bieg 364 doby z B1; porownanie z biegiem 13.10)

| # | Prog | Dzis |
|---|---|---|
| 1 | "Pieniadz swiata": suma bez skoku, kontrola kies r = 0 kazdej doby | r = 0 |
| 2 | roboty gmin z lokaty (d91-364): >= 8 tys. zl/d (B1), >= 20 tys. (B1 + B2); material zaplacony = sztuki zdjete x cena (ksiega "Towary", pozycja "budowy") | 0 |
| 3 | zaplata za material bez sztuki: 0 zl (gmina); punkty oplacone ponad koszt poziomu: 0 | ok. 1.5 tys. zl/d |
| 4 | poziomy ukonczone w roku >= 120; budow stojacych <= 170 z 227 | 69; 199 |
| 5 | trzecia pana 4-16 tys. zl/d, w tym gracz w liczniku | 0 |
| 6 | odplyw netto z kies (trzecia + 87% materialu + podatki od plac) >= 8 tys. zl/d; przyrost kies ludu mniejszy od biegu 13.10 o co najmniej tyle | - |
| 7 | drewno: stos swiata 30-60 dni zuzycia (pasmo regulatora), indeks ceny materialu budow mediana <= 1.5, zadna sztuka ponad 2 x | 76 dni; 0.82 |
| 8 | narzedzia: polki miast nie ponizej 5 szt. w wiecej niz 10% miast; zakupy wsi nie mniejsze niz dzis (42 szt./d) | 151 szt. |
| 9 | "stac na jedzenie": miast ponizej 95% nie wiecej niz w biegu 13.10 + 1 | 10 |
| 10 | `BuildFunding.Daily` <= 11 ms | 8.7-9.6 ms |
| 11 | wojsko (tylko odczyt, nie prog): oczekiwane +0.3-1.1 tys. | 62.5 tys. |

---

## 12. Slabosci kierunku B - uczciwie

1. **Nie zamyka nadwyzki.** 9-24 tys. zl/d netto wobec 37-84 tys. Budowa to w 3/4 place, a place wracaja do tych samych kies. To nie blad reguly, tylko natura inwestycji w robocizne.
2. **Granica to drewno, nie pieniadz.** Swiat jest wyceniony historycznie: budowy panow kupuja materialu za ok. 3.5 tys. zl/d, a cala produkcja materialu budowlanego swiata
   to ok. 12.5 tys. zl/d wedlug wartosci (drewno 1 992 ladunki x 4 zl, narzedzia 100 x 40 zl, glina 351, wapien 111, marmur i deski - b6.py). 23.8 mln zl lokaty to ponad 5 lat
   CALEJ tej produkcji. Realnej gospodarki nie da sie zalac pieniadzem - mozna tylko podniesc ceny albo wyrab.
3. **Zysk szkodzi celowi.** Kazda inwestycja z prawdziwym zwrotem (warsztat, karawana) w zamknietym swiecie przesuwa zloto DO inwestora. Dlatego z zadania zostala sama budowa,
   ktorej zwrot jest w naturze (ludnosc, zywnosc, produkcja, handel), nie w zlocie.
4. **Bez trzeciej pana kierunek nie karmi wojska.** A trzecia to w gruncie rzeczy oplata, nie inwestycja - granica z kierunkiem "podatki" jest cienka.
5. **Skonczone.** Drzewo budowli bogatego miasta wystarczy na 3-4 lata.
6. **Pokoj.** Gdy panowie znow buduja wszystko, drewna dla gmin nie ma - a wlasnie w pokoju nie dziala danina i podatek wojenny, wiec kiesy moga rosnac najszybciej.
7. **Bogaci bogatsi.** Trzecia i budowle trafiaja do 12-29 miast i ich panow.

**Czego nie sprawdzilem:** ile drewna wsie dadza przy trwale wyzszym zuzyciu (regulator decyduje co 28 dob na oknie 56 dob - odpowiedz o 1-2 miesiace spozniona);
jak AI uklada kolejke, gdy poziomy koncza sie co miesiac (czy nie wybierze samych wojskowych po 48 zl za punkt); ile towaru przybedzie na polkach po magazynach i drogach;
skladu nadwyzki pojedynczego miasta (CSV ma tylko sume - nadwyzka miasta w b7.py to srednia z 52 migawek, potrzeba dnia z kalibracji do lokaty 23 783 334 zl z LOG :129003);
ktore miasta dostana material (w rachunku b7.py bierze go najpierw miasto z najwieksza lokata - w grze decyduje polka kazdego miasta z osobna);
zachowania w pokoju (bieg mial 25 z 30 krolestw w wojnie).

**Pytanie o kierunek do Jeffa (jedno):** czy mieszczanie maja budowac miasto pana z wlasnej kiesy i oddawac mu przy tym trzecia - takze w Twoich miastach?
Reszta to parametry z rachunku powyzej.
