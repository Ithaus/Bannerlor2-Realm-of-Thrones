# ROZPOZNANIE KODU: co w gospodarce zalezy dzis od liczby ludzi, a co od plaskich miar gry

Data: 2026-10-13. Praca tylko do odczytu (bez gry, bez builda, bez zmian kodu).
Kod: galaz `w-toku/zbior-e8`, drzewo `...\ae222cb7-...\scratchpad\zb\int\Armoury\src` (dalej **A\**).
Dekompilacje: gra `...\scratchpad\dec32\cs_full` (**G\**), BannerKings `...\scratchpad\e4tools\bkfull` (**BK\**),
ROT `...\scratchpad\dec37\rot` (**ROT\**), RealisticBannerlord `...\scratchpad\rev41\rbl` (**RBL\** - tej sciezki nie bylo w poleceniu,
znaleziona w scratchpadzie; czynny model dobrobytu jest wlasnie z RBL).
Bieg roczny: log `D:\Backup-Bannerlord\logs\2026-10-13-noc34-35\Armoury-2026-10-13_06-24-10.log` (**L:numer linii**),
tabele `...\Logs\2026-10-13_06-24-10\` (**CSV**). Doby 108836-109199 (364 doby).
Znaczniki: **[P]** pomiar (log, CSV, kod), **[S]** szacunek / wyliczenie z pomiarow. "Nie wiem" = nie sprawdzone.

---

## 0. Najkrocej (prostym jezykiem)

1. **Ksiega ludzi wie, ilu ludzi oznacza kazda wies i kazde miasto** (Reach: wies = srednio 180 tys. ludzi, Polnoc 48 tys.,
   Dorne 36 tys., Zelazne Wyspy 16 tys., Qarth 229 tys. [P]). Stosunek Reach : Polnoc = 3.72 - prawie dokladnie przyklad Jeffa
   (37 : 10).
2. **Ale gospodarka tej liczby nie czyta.** Gra i BannerKings licza wszystko z "hearth" wsi (wszedzie ok. 335-500) i "dobrobytu"
   miasta (wszedzie 4-8 tys.) albo z wlasnych glow BannerKings (wies ok. 1 500-2 000, miasto 12-19 tys. - wszedzie tyle samo).
   Dlatego wies zboza w Reach i na Polnocy daje dzis TE SAMA liczbe sztuk (520 z 569 wsi ma ten sam mnoznik produkcji [P]).
3. **Od ludzi ksiegi liczy sie dzis tylko kilka rzeczy:** spichlerz okregu i glod (racje 1:1 z ludzmi), rece rzemieslnikow broni
   w miastach (0.10 na 1000 ludzi rynku - TAK, juz od ludzi), pula wyrzutkow, przyrost naturalny, liczba wiosek pobocznych okregu,
   sufit poboru (20% mezczyzn krainy - wiaze tylko male krainy, w duzych nigdy) i "renta nalezna" (tylko liczba w logu).
4. **Plaskie na osade:** plon i wydobycie wsi, zywnosc warowni, dobrobyt, popyt miasta, kasa i zapas kupcow, wielkosc klas kiesy
   ludu i ich koszyk, zarobek ludu, podatki pana (bo ida za utargiem i kasa osady), oplata stala wsi (liczona od glow BK, nie ksiegi),
   tempo werbunku (miejsca u notabli), milicja, zalogi, karawany, warsztaty notabli, kopalnie; posrednio pulap zoldu i druzyny.
   Dowod liczbowy: renty zaplacone panom to 134 tys. zl/d z 5.66 mln "naleznych od ludzi" (2.4%); Reach placi 6.7 tys., Polnoc 19.2 tys. [P L:128992].
5. **Skale da sie wpiac bez ruszania hearth i dobrobytu**: gotowa miara juz jest w kodzie (`PeopleUnit.Base` = ludzie wsi / srednia
   swiata na hearth), a kazdy towar wsi przechodzi przez JEDNO miejsce (`MaterialLaw.ProdPostfix`). Zmiana samego hearth / dobrobytu
   poruszylaby ok. 40 cudzych wzorow (BK, gra) i nasza ksiege - odradzam.
6. **Uwaga na miasta:** ludzie miast w ksiedze sa bardzo nierowni i miejscami niewiarygodni (6 "miast" ma 0 ludzi, miasta Zelaznych
   Wysp po 3.9 tys., Qarth 627 tys., New Ghys wiecej niz Krolewska Przystan). Dla miast lepsza podstawa jest "ludnosc rynku"
   (miasto + wsie, ktore w nim handluja) - tak juz licza rece rzemieslnikow.

---

## 1. SKAD SA LUDZIE

### 1.1 Wzor (jedno miejsce: `A\PopulationLaw.cs`)

- **Tabela krain wedlug KULTURY OSADY** (30 kultur), ludnosc i udzial miast: `A\PopulationLaw.cs:42-74`
  (np. battania 3.0 mln / 8%, reach 8.0 mln / 10%, aserai 1.5 mln / 8%, sturgia 0.5 mln / 5%, volantine 5.0 mln / 25%,
  qartheen 4.0 mln / 37%, crownlands 0.6 mln / 75%). To NIE jest tabela krolestw ani regionow - liczy sie kultura osady.
- **Kalibracja raz na kampanie** (`Calibrate`, `A\PopulationLaw.cs:91-147`):
  - `k_wsi(kultura) = ludnosc x (1 - udzial miast) / suma hearth wsi tej kultury` (linia 123),
  - `k_miasta(kultura) = ludnosc x udzial miast / suma dobrobytu miast tej kultury` (linia 124),
  - pulap 650 ludzi na hearth (`PopulationMaxPerHearth`, linie 97, 125-132; `A\Settings.cs:738`),
  - Zelazne Wyspy: 27 tys. niewolnych odjete z tabeli na konto NIEWOLNI (linie 116-120, `A\PeopleBook.Canon.cs:217-269`).
- **k zamrozone NA OSADE** w zapisie (`PeopleBook.SetUnitsFromCalibration`, `A\PeopleBook.cs:790-799`, odczyt `TryUnit` :820-831,
  uzycie `KFor` `A\PopulationLaw.cs:317-325`): zmiana kultury osady nie zmienia ludzi.
- **Ludzie osady** (`PeopleOf`, `A\PopulationLaw.cs:224-233`): wies = `hearth x k_v`; miasto = STAN zapisany raz
  (`TownPeople` :247-256; `A\PeopleBook.cs:424-448`), zamek = 0 (zamek nie ma ludzi w ksiedze; jego ludzie to wsie zamku).
- Nie ma osobnej tabeli ludnosci miast: miasto dostaje czesc "miejska" swojej kultury proporcjonalnie do dobrobytu z dnia kalibracji.

### 1.2 Liczby z biegu rocznego [P]

Kalibracja (L:209): battania wies 132 ludzi/hearth, miasto 5/dobrobyt; river 196 / 6; reach 444 / 26; aserai 88 / 4; vlandia 274 / 21;
vale 222 / 7; stormlands 182 / 6; sturgia 47 / 0 (+ niewolni 27 000); crownlands 27 / 38; dragonstone 88 / 2; freefolk 41 / 0;
nightswatch 3 / 0; volantine 551 / 64; empire (Braavos) 371 / 74; ghiscari 194 / 40; qartheen 622 / 80; summer OBCIETE 4500 -> 650
(0.75 mln -> 0.11 mln, "poza mapa" 0.64 mln). Srednia swiata 189.2 ludzi wsi na hearth (L:214). Miasta: 89 miast, 10.47 mln (L:211).

Tabela wedlug kultury z dnia startu (CSV `ludzie-regiony.csv` i `ludnosc-bk.csv`, doba 108836 i 109199) [P]:

| kultura (kraina) | wsie | miasta | ludzie tys. | ludzi na wies tys. | hearth na wies | glow BK na wies | ludzi na miasto tys. | glow BK na miasto | dobrobyt miasta start / koniec |
|---|---|---|---|---|---|---|---|---|---|
| reach | 40 | 6 | 7 999 | 180.0 | 405 | 1 992 | 133.3 | 15 102 | 5 052 / 7 005 |
| volantine | 18 | 4 | 4 999 | 208.3 | 378 | 1 771 | 312.5 | 15 688 | 4 827 / 6 105 |
| qartheen | 11 | 3 | 4 000 | 229.0 | 368 | 1 702 | 493.3 | 17 430 | 6 135 / 7 180 |
| vlandia (Zachod) | 33 | 3 | 3 999 | 111.5 | 407 | 2 010 | 106.7 | 16 624 | 4 968 / 6 615 |
| river | 43 | 9 | 3 499 | 74.9 | 381 | 1 791 | 31.1 | 15 543 | 4 791 / 6 296 |
| vale | 31 | 5 | 2 999 | 90.9 | 409 | 2 049 | 36.0 | 16 163 | 4 943 / 6 548 |
| battania (Polnoc) | 57 | 9 | 2 999 | 48.4 | 365 | 1 757 | 26.7 | 15 997 | 4 680 / 6 717 |
| stormlands | 31 | 4 | 2 499 | 76.6 | 420 | 1 925 | 31.2 | 15 844 | 5 102 / 7 224 |
| aserai (Dorne) | 38 | 5 | 1 500 | 36.3 | 412 | 1 908 | 24.0 | 16 576 | 4 922 / 6 538 |
| crownlands | 11 | 2 | 600 | 13.6 | 497 | 2 459 | 225.0 | 19 107 | 5 855 / 8 113 |
| sturgia (Zelazne Wyspy) | 28 | 6 | 473 | 16.0 | 335 | 1 571 | 3.9 | 12 766 | 4 185 / 5 381 |

Wniosek z tabeli: **hearth (335-497), glowy BK na wies (1 571-2 459), glowy BK na miasto (12.8-19.1 tys.) i dobrobyt (4.2-6.1 tys.
na starcie, 5.4-8.1 tys. po roku) sa wszedzie prawie takie same**; rozni sie tylko liczba ludzi ksiegi (wies 13.6-229 tys.,
miasto 3.9-493 tys.). Stosunek "ludzie ksiegi / glowy BK" swiata = 17.78 (L:656).

### 1.3 Skad 153.6 tys. na wies w Reach i 16.0 tys. na Wyspach

- Reach wedlug KULTURY: 8.0 mln x 0.90 / (40 wsi x 405 hearth = 16 200 hearth) = 444 ludzi/hearth; x 405 = **180.0 tys. na wies** [P].
- 153.6 tys. z polecenia to liczba wedlug KROLESTWA "The Reach" (8 345 tys. - 7 miast x 117 tys.) / 49 wsi: krolestwo ma tez wsie innych
  kultur (nizsze k), stad mniej [S]. Skrypt `docs\ludnosc\kr-ludnosc-a-zarobek.py:16-19` sumuje po kolumnie `krolestwo`.
- Zelazne Wyspy: (500 000 - 27 000 niewolnych) x 0.95 / (28 wsi x 335 hearth) = 47.9 ludzi/hearth; x 335 = **16.0 tys. na wies** [P].
- Miasto Reach: 8.0 mln x 0.10 / (6 x 5 052 dobrobytu) = 26.4 ludzi na punkt dobrobytu = 133 tys. na miasto [P].

### 1.4 Zrodla w swiecie (lore) i wiarygodnosc

Zrodlo tabeli: `docs\POPULACJA-WESTEROS-ESSOS.md`. Sam dokument mowi (linie 16-18): "GRRM nigdy nie podal populacji zadnego regionu";
jedyna liczba kanoniczna to Krolewska Przystan ok. 500 tys. Liczby krain = armia z RPG 2005 (semi-canon) x 50-100; Essos = fanowski
blog Atlas of Ice and Fire "z zapleczem" (tabela w rozdz. 2 i 4 tego dokumentu).

| kraina | w tabeli | zakres dokumentu | ocena |
|---|---|---|---|
| Polnoc | 3.0 mln | 2.3-4.5 mln, fan 4 mln | [S] rozsadne; kanon: najwieksza i najrzadziej zaludniona |
| Dorne | 1.5 mln | 1-2.5 mln, fan 3 mln | [S] slabsze (liczba wloczni sporna); kanon: najmniej ludna z Siedmiu Krolestw - zgodne. W grze krolestwo Dorne ma 1.116 mln, bo czesc osad kultury aserai lezy w innych krolestwach |
| Zelazne Wyspy | 0.5 mln | 0.4-0.7 mln, fan 1.5 mln | [S] niepewne x3; skutek: 28 wsi po 16 tys. i 6 miast po 3.9 tys. ludzi - symbole bardzo male |
| Volantis | 5.0 mln | fan 1.2 mln miasto / 5 mln z zapleczem | [S] fanowskie, bez kanonu |
| Qarth | 4.0 mln | fan 1.5 mln / 4 mln | [S] fanowskie; tylko 11 wsi i 3 miasta -> NAJWIEKSZE mnozniki swiata (wies 229 tys., miasto Qarth 627 tys.) |
| Reach | 8.0 mln | 4-10 mln, fan 12 mln | [S] rozsadne; kanon: "zdecydowanie najludniejsza" - zgodne |

Slabe punkty rozkladu WEWNATRZ krainy [P]:
- podzial na osady idzie za hearth / dobrobytem gry z dnia startu (dane mapy ROT), nie za lore: Krolewska Przystan i Duskendale
  dostaja po ok. 225 tys. (kanon: KP ok. 500 tys.), 9 "miast" Polnocy po 26.7 tys. (kanon: jedno miasto - Bialy Port);
- **6 miast ma 0 ludzi w ksiedze** (udzial miejski kultury = 0): Vaes Dothrak, Frostfang's Camp, Castle Black, Driftwood Hall,
  Vaes Diaf, Thenn (CSV `ludnosc-bk.csv` d108836) - a BK trzyma tam 11-18 tys. glow;
- najwieksze miasta ksiegi: Qarth 627 tys., New Ghys 467 tys., Braavos 434 tys., Qarkash 386 tys., Rhyos 366 tys., Volantis 330 tys. -
  Krolewska Przystan nie jest w pierwszej szostce;
- Wyspy Letnie obciete pulapem (0.64 mln "poza mapa").
Wniosek: **liczby krain sa szacunkiem spojnym z kanonem co do KOLEJNOSCI krain (wies jako symbol okolicy - wiarygodne);
liczby POJEDYNCZYCH MIAST nie sa wiarygodne** i nie nadaja sie na mnoznik "wprost".

### 1.5 Jak ludzie rosna i maleja

| zjawisko | wzor | miejsce |
|---|---|---|
| przyrost naturalny wsi | hearth x g / 100 / dni roku; g = 0.5 + 0.6 x W(dobrobyt wobec mediany krainy, -0.5..+0.5) - 0.6 x U(niebezpieczenstwo 0..1) - 1.5 x H(glod), korytarz -1.3..+0.8 % rocznie; premie gry/BK tylko mnoza dodatnie g (x0.5..2) | `A\PopulationLaw.cs:576-613`, podmiana wyniku gry :656-687; stale `A\Settings.cs:812-818`; stala gry +4 / +1.2 / +0.2 hearth dziennie (`G\...GameComponents\DefaultSettlementProsperityModel.cs:41-46`) jest zastapiona |
| miasta | stopa wsi regionu - 1 pkt; migracja z wlasnych wsi do udzialu miejskiego w ok. 364 doby | `A\TownGrowth.cs:88-136`, `A\PopulationLaw.cs:555-573` |
| glod | zgony do 70%/rok ponad prog 10% braku; uchodzcy z glodu | `A\Famine.cs:29-34` (opis), :562-620 |
| wojna: rabunek i zerowanie | trafieni -> zabici / pojmani / las / uchodzcy; powrot w rok | `A\Devastation.cs`, konto UCHODZCY `A\PeopleBook.cs:499-786` |
| pobor | rekrut = 1 czlowiek = 1/k_v hearth, podloga 50 hearth wsi | `A\PeopleBook.Origin.cs:1128-1166`, `A\PeopleUnit.cs:86-94` |
| powroty (rozbici, zwolnieni, weterani, wyrzutki) | +1/k_v hearth | `A\PeopleBook.cs:77-137` (lista przyczyn), `Move` :849-872 |
| podloga gry 10 hearth | dopisek gry schodzi z uchodzcow / puli wyrzutkow | `G\...Settlements\Village.cs:231-234`, `A\PopulationLaw.cs:700-746` |

Pomiar roku [P]: przyrost naturalny ok. +0.50% rocznie (L:772); zmiana ludzi krain po roku od -0.01% (Reach) do -7.55% (dragonstone),
Dorne -2.43%, Wyspy -0.82% (CSV `ludzie-regiony.csv`).

### 1.6 Jak to wraca do hearth i dobrobytu gry

- **Wies: hearth JEST stanem ludzi** (ludzie = hearth x k_v; kazdy ruch ludzi idzie przez `PeopleBook.Move` -> `PeopleUnit.Shift`,
  `A\PeopleBook.cs:849-872`, `A\PeopleUnit.cs:118-130`). Poniewaz k_v jest stale na osade, a hearth startuje wszedzie z 335-497,
  **hearth niesie wielkosc wsi tylko w jednostkach lokalnych**: 405 hearth w Reach = 180 tys. ludzi, 365 hearth na Polnocy = 48 tys.
  Wzory gry i BK widza samo hearth - wiec widza wszedzie taka sama wies.
- **Miasto: ludzie sa osobnym stanem ("t:"), dobrobyt chodzi wlasnym modelem** (od E4/4, `A\PopulationLaw.cs:235-256`; `TownGrowth`
  nie dotyka `Town.Prosperity`). Ludzie nie zmieniaja dobrobytu, dobrobyt nie zmienia ludzi (tylko wskaznik W w przyroscie).
- **Dwa niezalezne "spisy ludzi"**: ksiega (51.5 mln) i ludnosc BannerKings (wies = hearth x 4-6 na starcie, sufit hearth x 4 + ziemia;
  miasto 8-15 tys. x (1 + dobrobyt/10 000); `BK\BannerKings.Managers\PopulationManager.cs:469-494`, `BK\...BKModels\BKGrowthModel.cs:70-126`).
  BK zyje swoim zyciem: glowy BK wsi spadly w rok z ok. 1 900 do ok. 1 500 przy stalej ksiedze [P CSV `ludnosc-bk.csv`].

---

## 2. OGNIWA GOSPODARKI: wzor i od czego zalezy

Czynne modele [P L:706, L:464, L:771]: produkcja wsi `NavalDLCVillageProductionCalculatorModel -> DefaultVillageProductionCalculatorModel`
(NIE model BK), kasa osad `DefaultSettlementEconomyModel`, podatek `BKTaxModel`, zywnosc `ROTSettlementFoodModel`,
dobrobyt `RealisticProsperityModel` (RBL) na bazie gry + latki BK i BKROT, ochotnicy `BKVolunteerModel`, zaloga `ROTSettlementGarrisonModel`.

### Tabela zbiorcza

| ogniwo | dzis zalezy od | werdykt |
|---|---|---|
| (a) plon i wydobycie wsi (towar na targ) | typ wsi x prog hearth 0.5 / 1.0 / 1.5 x nasze mnozniki (ruda, drewno, zima, rece, regulator swiata) | **PLASKIE na wies** |
| (a) ubytek rak po spustoszeniu i wojnie | udzial ludzi w domu / wolne rece krainy | czesciowo (tylko W DOL, wzglednie) |
| (b) zywnosc wsi w modelu gry | poziom hearth + 1 | PLASKIE |
| (b) spichlerz okregu i glod (nasze racje) | 1.2 x ludzie x q, zjadaja ludzie | **JUZ OD LUDZI** (osobna ksiega racji, nie towar) |
| (b) zywnosc miast i zamkow | zuzycie dobrobyt/40 + zaloga/20; doplyw 15 (10) + (poziom+1) x 6 na wies + jedzenie sprzedane na targu | PLASKIE |
| (c) dobrobyt miasta | koszty mieszkan (miekki sufit 6-21 tys.), nadwyzka jedzenia, towary, budowle, x0.5 RBL, glowy BK | PLASKIE (wszedzie 5-8 tys.) |
| (d) renta "nalezna" od ludzi | ludzie x 40 zl/rok | od ludzi, ale to TYLKO LICZBA |
| (d) to, co pan naprawde dostaje z miasta | 7% nadwyzki kasy miasta ponad zapas kupcow + 1/2/3% zarobku ludu + 1/3 z 7% gminy | PLASKIE (idzie za obrotem miasta) |
| (d) to, co pan dostaje ze wsi | podatek BK od utargu taborow (70%) + oplata stala 4 zl/rok na GLOWE BK | PLASKIE |
| (d) powinnosci wobec korony, danina kas, podatek 184, danina P1 | % dochodu rodu / kasy / zarobku ludu / oszczednosci ludu | PLASKIE (pochodne) |
| (e) rece rzemieslnikow broni, sukna, skory, strzelarzy | 0.10 x ludnosc RYNKU / 1000 x wskaznik placy, podloga z dobrobytu, sufit 1000 | **JUZ OD LUDZI** (czesciowo: podloga i wskaznik z dobrobytu) |
| (e) warsztaty notabli (towarowe) | stale 6 roboczodni, liczba warsztatow na miasto stala | PLASKIE |
| (e) kopalnie (wsie rudy, srebra) | jak (a) x 3 x 1.5 | PLASKIE |
| (f) kiesa ludu: liczebnosc klas W / R / P | glowy BK z kalibracji x r (r = zmiana ludzi ksiegi od kalibracji) | PLASKIE (BK), tylko wzgledny ruch od ksiegi |
| (f) koszyk i wydatki | min(wydatek gry z dobrobytu, glowy BK x koszyk) | PLASKIE |
| (f) zarobek ludu | z prawdziwych przeplywow osady (zold, robocizna handlu, 35% wartosci wyrobow) | PLASKIE (idzie za obrotem) |
| (g) tempo werbunku | liczba miejsc u notabli x szansa BK 0.5 x chec | PLASKIE na osade |
| (g) sufit werbunku | wolne rece: 20% mezczyzn (0.27 x ludzie) krainy | OD LUDZI, ale wiaze tylko w malych krainach |
| (g) milicja, zalogi | 2 (0.5) + hearth/400 + dobrobyt/1000; zaloga tylko z platnego werbunku 2/d | PLASKIE |
| (h) popyt miasta | popyt bazowy x dobrobyt (+ zbytki ponad 3000) | PLASKIE |
| (h) kasa kupcow i zapas | 10 000 + 12 x dobrobyt | PLASKIE |
| (h) karawany | liczba notabli-kupcow x limit, zapas docelowy z rak (dawny wzor) | PLASKIE na miasto |
| (i) pulap zoldu, druzyny | 0.28 / 0.60 dochodu stalego rodu + skrzynia wojenna | posrednio PLASKIE (od liczby osad) |
| inne juz od ludzi | pula wyrzutkow, liczba wiosek pobocznych okregu (ludzie / 250), wagi poboru, niewolni | JUZ OD LUDZI |

### (a) Plon i wydobycie wsi

- Gra: `ilosc = stawka typu wsi x (poziom hearth + 1) x 0.5` - `G\TaleWorlds.CampaignSystem.GameComponents\DefaultVillageProductionCalculatorModel.cs:31`
  (tylko wies Normal :20; premia budowli warowni :63-66). Poziom: hearth < 200 -> 0, 200-599 -> 1, >= 600 -> 2
  (`G\TaleWorlds.CampaignSystem.Settlements\Village.cs:320-331`). Czyli mnoznik 0.5 / 1.0 / 1.5.
- Pomiar [P L:772]: wsie wedlug progow 12 / 520 / 37 - **91% wsi swiata ma mnoznik dokladnie 1.0**. Model czynny = model gry
  (iloraz "czynny / pod nim" = 1.00 dla 10 typow wsi, np. wheat_farm zboze 55.00 na dobe, L:464).
- BannerKings: jego model produkcji (`BK\BannerKings.Models.Vanilla\BKVillageProductionModel.cs:39-124`: sila robocza chlopow x 0.0045,
  zywnosc z ziemi, sufit 200 :175) **nie jest czynny** [P L:464, L:706]. BK podmienia tylko petle ticku
  (`BK\BannerKings.Patches\EconomyPatches.cs:940-982`: wlasna lista towarow, ale ilosc bierze z czynnego modelu gry, linia 962).
- Nasze czynniki - WSZYSTKIE w jednym postfiksie `MaterialLaw.ProdPostfix` (`A\MaterialLaw.cs:173-238`; wpiety w 5 klas modeli L:25):
  ruda x `MineOutputMultiplier` 3 x `MineOutputStep` 1.5, drewno x 3 (:183-184; `A\Settings.cs:504-505, 1524`);
  rece po spustoszeniu `Devastation.YieldFactor` = (ludzie w domu / (w domu + uchodzcy)) ^ 1 (:205-210; `A\Devastation.cs:578-599`);
  rece w wojnie `FreeHands.YieldFactor` (:212-217; `A\FreeHands.cs:166-172`, 200-210);
  regulator swiata `HarvestValve.Factor(towar)` - jeden mnoznik NA TOWAR dla calego swiata, 0.1..1.3, decyzja co 28 dob wedlug zuzycia
  swiata (:220-225; `A\HarvestValve.cs:18-41`); hodowla, wioski rybackie, sufit dymarki (:228-234).
- Zima: `WinterBite` (ciecie plonu wedlug pory i krainy, `A\WinterBite.cs:13-33`) - na modelu bazowym.
- Prog hearth: `Devastation.LevelPostfix` tylko PODNOSI poziom wsi z uchodzcami (`A\Devastation.cs:605-620`).
- **Werdykt: PLASKIE na wies.** Zadna z tych liczb nie czyta k_v ani ludzi ksiegi; czynniki rak sa wzgledne (ile ludzi ubylo), nie
  mowia, ilu ich jest.

### (b) Zywnosc

- Wies (model gry): `poziom + 1` jednostek (`G\...\DefaultVillageProductionCalculatorModel.cs:76-88`); nasze postfiksy: zima, rece po
  spustoszeniu (`A\Devastation.cs:623-628`), rece w wojnie (`A\FreeHands.cs:380-395`). PLASKIE.
- Miasto / zamek (gra): zuzycie `dobrobyt / 40 + zaloga / 20` (`G\...\DefaultSettlementFoodModel.cs:47-48`), doplyw `15` miasto /
  `10` zamek + na kazda wies Normal `(poziom + 1) x 6` (:65-75) + jedzenie sprzedane na targu (:82-91), zapas do 300 (:30; BK 500,
  `BK\...\BKFoodModel.cs:29`). ROT dodaje +100 / +120 dwom osadom (`ROT\ROT.Models\ROTSettlementFoodModel.cs:35-44`).
  Nasze: skladnik wsi x czynnik rak (`A\VillageBook.cs:1653-1669`). Ktory model stoi pod ROT (BK czy gra) - **nie wiem** (w logu jest
  tylko wierzch lancucha); skladnik wsi liczy klasa gry i tam siedza nasze postfiksy (L:135). PLASKIE.
- **Spichlerz okregu (nasz, E4/6) - JUZ OD LUDZI**: racje 1:1 z ludzmi; `ludzie = hearth x k` (`A\Famine.cs:571-573`),
  `plon = 1.2 x ludzie x q` (:579), q = zywnosc wsi z modelu gry / (poziom + 1) - dzielnik zdejmuje skok progow (:577),
  sufit spichlerza = 2 lata x ludzie (:581). Pomiar [P CSV `spichlerze-regiony.csv` d108836]: The Eyrie ludzie 205 184, plon 246 221,
  zjedli 205 184; Highgarden 761 494 / 913 793 / 761 494. To jednak OSOBNA ksiega racji: nie jest towarem na targu i nie karmi miasta gry.
- Przelicznik miasta juz istnieje: `Famine.QOf` = ludzie miasta / (dobrobyt / 40) (`A\Famine.cs:505-515`).

### (c) Dobrobyt miasta

- Gra: koszty mieszkan +6..+1 ponizej 1500 i -1..-6 powyzej 6000..21000 (`G\...\DefaultSettlementProsperityModel.cs:83-130`),
  nadwyzka jedzenia x 0.1 (:132-137), towary "BonusToProsperity" x 0.1 (:138-145), budowle (:160), lojalnosc (:168-175).
- RBL: wynik x 0.5, glod -20, zima, pokoj (`RBL\RealisticBannerlord.Systems.Demographics\RealisticProsperityModel.cs:13-59`, linia 31).
- BK: skladniki od GLOW BK (rzemieslnicy x 0.0005, chlopi x -0.00004..., `BK\...\BKProsperityModel.cs` ok. :45-81) - plaskie.
- Nasze: dobrobytu nie ruszamy; ludzie miasta odlaczeni od dobrobytu (1.6).
- Pomiar [P]: dobrobyt miast wedlug kultur 4.2-6.1 tys. na starcie, 5.4-8.1 tys. po roku, bez zwiazku z ludzmi (133 tys. w Reach,
  27 tys. na Polnocy, 493 tys. w Qarth). **Werdykt: PLASKIE; miekki sufit 6-9 tys. trzyma wszystkie miasta w jednym pasie.**
  Dobrobyt jest wiec miara "zamoznosci na glowe", nie wielkosci.

### (d) Podatki i daniny

- Renta "nalezna": `ludzie x PopulationRentPerHead (40) / dni roku` (`A\PopulationLaw.cs:1038-1042`, `A\Settings.cs:735`) - od ludzi.
  Ale to tylko gorna liczba: wies placi `min(nalezne, czesc kiesy)`, a przy kiesie czynnej (dzis) w ogole nie rente, tylko oplate stala
  (`A\PopulationLaw.cs:1237-1259`); miasto placi przez zawor kasy "bez pulapu naleznej" (:1238-1240, :1294-1316).
- Zawor miasta: 7% nadwyzki kasy ponad zapas kupcow (`A\TownPurse.cs:397-457`, `TownRentShare` `A\Settings.cs:745`); 5% z tego ludowi,
  reszta pan / korona. Zalezy od kasy miasta, czyli od obrotu.
- Podatek ludnosci miasta BK (`BK\...\BKTaxModel.cs:157-210`: glowy BK x stawka) - wylaczony, zostaja warsztaty cudzych wlascicieli,
  kopalnie, materialy budow (`A\PopulationLaw.cs:1077-1096`, 1102-1144).
- Od zarobku ludu: pan 1% / 2% / 3% (W / R / P) zarobku okna, cech 1% (`A\TownFolk.Live.cs:764-770`, stale `A\TownFolk.cs:56`);
  farma gmin 1/3 z 7% stanu gminy (:807-812, stale `A\TownFolk.cs:58`); zamek 3% (:892-897).
- Wies: podatek BK od utargu taborow, 70% (50 / 90 wedlug dekretu) - `BK\...\BKTaxModel.cs:212-284`; nasze domkniecie utargu
  `A\VillageTakings.cs:16-55`. Oplata stala: `VillageDuesPerHeadYear (4) / 364 x VillHeads` (`A\TownFolk.Live.cs:1021-1034`),
  gdzie `VillHeads = glowy BK (HW + HR) x r` (:927) - **"na glowe" znaczy tu glowe BannerKings (ok. 1 500-2 000 na wies), nie ksiegi**.
- Korona: powinnosci 2% / 3% dziennego dochodu rodu (`A\KingdomTreasury.cs:40-48`, `A\Settings.cs:951-952`); podatek 184 od zarobku ludu
  (`PayCrown`, `A\TownFolk.Live.cs:771`); danina P1 2.5%/d z oszczednosci ludu ponad zapas (:827-846, `A\Settings.cs:1665`).
- Pomiar [P]:
  - L:128992 (doba 109200): renty zaplacone **134 232 zl z naleznych 5 661 983** (2.4%); reach 6 746 / 878 901, battania 19 212 / 328 497,
    aserai 10 000 / 160 790, sturgia 5 126 / 51 521, qartheen 5 282 / 437 689, volantine 8 341 / 548 701.
  - L:129240: od zarobku ludu miast 10 992 zl/d, podzamcza 1 461, farma gmin 3 508, oplaty stale wsi 9 905 (519 wsi = 19.1 zl na wies -
    zgodne z 4 zl x ok. 1 740 glow BK / 364); utarg wsi 331 906 zl/d, z tego wsiom zostalo 118 610.
- **Werdykt: wszystko, co pan naprawde dostaje, jest PLASKIE** (idzie za utargiem wsi, obrotem i kasa miasta - a te sa plaskie).

### (e) Warsztaty, rece, kopalnie

- Rece rzemieslnikow broni (ukryty warsztat miasta): `max(dawny wzor, min(1000, 0.10 x ludnosc rynku / 1000 x wskaznik placy)) x 1.3`
  (`A\WorkshopLaw.cs:799-812`; `A\Settings.cs:1520-1521, 1863`). Ludnosc rynku = miasto + wsie z TradeBound do niego, ludzie ksiegi
  (:829-857; `PopulationLaw.TablePeopleOf` `A\PopulationLaw.cs:158-202`). Dawny wzor = dobrobyt / 170, 6..60 (:780-785).
  Wskaznik placy = dobrobyt / 4800, 0.5..1.5 (`A\TownWage.cs:19-25`).
- Od tych samych rak: rzemioslo miasta x 2 (`A\TownCrafts.cs:282`), strzelarze x 0.6 (`A\TownFletchers.cs:480`), kowale napraw
  (`A\SmithHours.cs:39`), zuzycie surowca w zamowieniach karawan (`A\CaravanBulk.cs:267-277`).
- Pomiar [P L:305]: rece swiata 5 920 (dawny wzor 2 740); The Reach 178 -> 888, The North 248 -> 333, Dorne 145 -> 162, Iron Islands
  148 -> 148 (na podlodze), Qarth 108 -> 487, Volantis 114 -> 466; 35 z 97 miast siedzi na dawnym wzorze. Na 1000 ludzi krolestwa:
  Reach 0.106, Polnoc 0.103 [S]. Pod koniec roku rece zajete w 98% (bezczynne 2.0%), brak surowca 140 cykli/d (ruda 41, drewno 33,
  skora 91) [P L:128929] - rece sa JUZ od ludzi, ale surowiec dla nich pochodzi z plaskich wsi.
- Warsztaty notabli: `WorkshopWorkers` = 6 roboczodni na warsztat (`A\WorkshopLaw.cs:817-822`, `A\Settings.cs:581`); liczba warsztatow na
  miasto ustalona przez gre; linie towarowe na cyklu gry (`A\WorkshopLaw.cs:40-41`, `A\WorkshopTrade.cs:21-48`). PLASKIE.
- Kopalnie: wsie iron_mine (26) i silver_mine (30) [P L:464] - jak (a); kopalnie zamkow BK (budowla) - plaskie.
- **Werdykt: rece zbrojne JUZ OD LUDZI (z podloga, sufitem i wskaznikiem z dobrobytu); reszta PLASKA.**

### (f) Kiesa ludu

- Liczebnosc klas: z BK przy kalibracji - miasto `HP = szlachta BK, HR = rzemieslnicy BK, HW = chlopi + dzierzawcy (+ niewolni)`,
  zamek i wies podobnie (`A\TownFolk.Probe.cs:711-727`); `PeopleCal = ludzie ksiegi z dnia kalibracji` (:641).
- "skala_r" = `ludzie ksiegi dzis / ludzie ksiegi w dniu kalibracji` (`A\TownFolk.cs:428-434`) - mnozy glowy BK; zamek zawsze 1.
  Czyli ksiega daje tylko WZGLEDNA zmiane (-10% ludzi -> -10% glow), nie poziom.
- Koszyk: `B = (HW x 0.27 + HR x 0.50 + HP x 1.46) x r x indeks cen` (`A\TownFolk.Live.cs:725-728, 854-855`; stale `A\TownFolk.cs:49`),
  potrzeba = `min(wydatek gry, B)` (:736); wydatek gry = budzet konsumpcji gry z dobrobytu (patrz h).
- Zarobek: z prawdziwych przeplywow osady - 25% wydatkow zolnierzy i zalog, dwor, place budow, 35% wartosci wyrobow, robocizna handlu od
  sprzedazy kupcow (`A\TownFolk.cs:14-21, 62`, `A\TownFolk.Live.cs:774-800`).
- Wsie: zakupy `0.07 zl x glowy BK` dziennie (`A\TownFolk.Live.cs:929-964`, `A\Settings.cs:1652`).
- Pomiar [P CSV `kiesa-ludu.csv`, doba 109199, srednio na miasto]:

| kultura | ludzie ksiegi | glowy BK (kalibracja) | skala_r | koszyk zl | wydatek gry zl | dochod ludu zl/d | dochod na 1000 ludzi ksiegi |
|---|---|---|---|---|---|---|---|
| qartheen | 491 310 | 17 430 | 0.995 | 21 219 | 5 309 | 4 524 | 9.2 |
| volantine | 311 826 | 15 688 | 0.998 | 15 884 | 3 843 | 6 221 | 20.0 |
| reach | 132 532 | 15 102 | 0.994 | 19 372 | 4 474 | 4 468 | 33.7 |
| vale | 35 586 | 16 163 | 0.988 | 21 334 | 5 136 | 7 370 | 207 |
| battania | 26 401 | 15 997 | 0.988 | 22 407 | 5 018 | 6 231 | 236 |
| aserai | 22 766 | 16 576 | 0.949 | 18 711 | 5 717 | 5 049 | 222 |
| sturgia | 3 592 | 12 766 | 0.903 | 15 364 | 3 106 | 3 457 | 963 |

  Razem (L:731): glowy BK miast 1 503 402 (srednio 15.5 tys. na miasto), ksiega miast 10 469 983.
- **Werdykt: PLASKIE na osade** - miasto Polnocy zarabia i wydaje wiecej niz miasto Reach, choc ma 5 razy mniej ludzi.

### (g) Pobor, milicja, zalogi

- Zrodlo rekruta: miejsca u notabli (stala gry); puste miejsce wypelnia sie z szansa BK `0.5 x (1 + moc/150) x ...`
  (`BK\...\BKVolunteerModel.cs:334-345`) x nasza chec `0.05 + nadwyzka rak + 0.2 x nedza`, sufit 1.5 (`A\Levy.cs:98-113, 136-137`;
  `A\Settings.cs:671-674`). Liczba notabli i miejsc na osade nie zalezy od ludzi -> **tempo PLASKIE na osade**.
- Hamulec od ludzi: obciazenie `a = (sluzba + polegli...) / (0.27 x ludzie regionu z dnia siewu)`; przy `a >= f` (f = 20%, w zniwa x 0.5;
  Wyspy do 40%; Wolni Ludzie bez hamulca) puste miejsce sie nie wypelnia (`A\FreeHands.cs:119-128, 157-183`; `A\Levy.cs:126-133`;
  `MenShare` 0.27 `A\PeopleBook.Origin.cs:87`). Kazdy rekrut schodzi z DOM (wies -1/k_v hearth, podloga 50 hearth;
  `A\PeopleBook.Origin.cs:1128-1166`).
- Pomiar [P CSV `ludzie-regiony.csv`, rok]: rekruci na 1000 ludzi - reach 2.46, volantine 1.55, qartheen 2.70, battania 5.39,
  aserai 14.4, sturgia 24.7, river 9.87 (wojna). Najwyzsze obciazenie w roku: reach 2.0%, battania 5.8%, aserai 13.6%, sturgia 14.2%,
  crownlands 21.4%, sarnor 25.6%, nightswatch 172%.
- Pomiar hamulca [P, 364 linie "Pobor: dzien" logu]: 843 040 losowan miejsc z checia (srednio x0.37 szansy BK) i 131 694 odmowy hamulca
  (13.5%), z tego 44 035 w 57 dobach okna zniw. Odmowy wedlug krain: sarnor 96 058, ibbenese 14 243, dragonstone 10 183,
  crownlands 2 582, lyseni 2 211, khuzait 2 088, river 1 870, valyrian 1 637, tyroshi 564. **Zadnej odmowy w Reach, na Polnocy,
  w Dorne, na Zelaznych Wyspach, w Volantis, Qarth, na Zachodzie, w Dolinie i Krainach Burzy - sufit od ludzi wiaze tylko male
  krainy.** Reach ma 2.16 mln mezczyzn [S: 0.27 x 8.0 mln], a krolestwo The Reach trzyma 11.6 tys. wojska [P z polecenia].
- Milicja: gra `2 (miasto, zamek) / 0.5 (wies) + hearth / 400 + dobrobyt / 1000` (`G\...\DefaultSettlementMilitiaModel.cs:104-120`),
  BK `hearth / 400` (`BK\...\BKMilitiaModel.cs:99`); u nas tylko z wolnych rak regionu (`A\ArmsFromPeople.cs:22-39`). PLASKIE.
- Zalogi: przyrost z modelu = 0, tylko platny werbunek od notabli, limit 2 na dobe na twierdze (`A\ArmsFromPeople.cs:25-27`,
  `A\Settings.cs:876`). PLASKIE (i ograniczone zoldem).
- **Werdykt: tempo PLASKIE, sufit od ludzi (nie wiaze).** Stad [P z polecenia] wojsko The Reach 11.6 tys., The North 17.6 tys.

### (h) Handel

- Popyt miasta: `popyt bazowy kategorii x dobrobyt + popyt zbytkow x max(0, dobrobyt - 3000)`
  (`G\...\DefaultSettlementEconomyModel.cs:61-73`; wolane z `G\...CampaignBehaviors\ItemConsumptionBehavior.cs:183`). Konsumpcje robi
  latka BK (`BK\BannerKings.Patches\EconomyPatches.cs:500-603`), budzet wiersza `popyt x indeks^0.15 + dobrobyt / 1000 / indeks`
  (:1085-1100), a nasza kotwica kiesy ludu mnozy ten budzet czynnikiem `min(1, koszyk / wydatek) x na-ile-stac`
  (`A\TownFolk.Live.cs:413-429`, opis :25-26). Wszystko z dobrobytu i glow BK. PLASKIE.
- Kasa kupcow: cel regulatora gry `10 000 + 12 x dobrobyt`, 25% roznicy na dobe (`G\...\DefaultSettlementEconomyModel.cs:75-79`);
  nasz zapas kupcow ten sam wzor (`A\Seal31.cs:100`); limit BK `50 000 + 25 x dobrobyt` miasto, `10 000 + 3 x hearth` wies
  (`BK\...\BKEconomyModel.cs:610-626`); kiese wsi gra obcinalaby co dobe do 1 000 (`G\...Settlements\Village.cs:236-239`), ale tick wsi
  zastepuje latka BK bez tego ciecia (`BK\BannerKings.Patches\NotablePatches.cs:207-225`; opis w `A\VillageTakings.cs:539`) - zostaje
  limit BK. Pomiar [P CSV `kasy-miast.csv`]: The Eyrie zapas 80 859 przy dobrobycie 5 905; Highgarden 82 016
  przy 6 001 (158 tys. ludzi wobec 43 tys.). PLASKIE.
- Tabor wsi: wielkosc `minimum + hearth / num2` (`G\...\DefaultPartySizeLimitModel.cs:415-425`); magazyn wsi = 5 dni produkcji z modelu,
  produkcja staje przy 1.5 magazynu (`G\...Settlements\Village.cs:248-257`, `G\...\VillageGoodProductionCampaignBehavior.cs:138-155`).
- Karawany: limit na notabla (kupiec 2, rzemieslnik 1 w BK - `BK\...\BKEconomyModel.cs:600-608`; u nas limit kupca 3 od WGRANIA 35),
  cel zapasu surowca z rak (dawny wzor z dobrobytu - `A\CaravanBulk.cs:223`), zuzycie z obecnych rak (:267-277 - to juz od ludzi).
- **Werdykt: PLASKIE na miasto** (poza zamowieniami surowca, ktore ida za rekami).

### (i) Limity druzyn i pulap zoldu

- Pulap zoldu rodu: pokoj `0.28 x D`, wojna `0.60 x D + 0.8 x (G - R) / 45` (`A\ClanBudget.cs:193-212`; `A\Settings.cs:1035-1042`),
  D = dochod staly rodu (ziemia + korona + kontrakt + majatek, `A\ClanBudget.cs:22-27`).
- Limit druzyny: model ROT / BK od rangi rodu i perkow - w `ROT\ROT.Models\ROTPartySizeLimitModel.cs` brak odczytu dobrobytu i hearth
  (szczegolow nie czytalem).
- **Werdykt: posrednio PLASKIE** - D idzie za liczba osad, nie za ludzmi (teza wyjsciowa: 23.6 zl na 1000 ludzi srednio, Reach 9.9,
  Polnoc 33.8, Dorne 92.4 [P z polecenia]).

---

## 3. GDZIE WPIAC SKALE

### 3.1 Gotowa miara w kodzie (nic nowego nie trzeba wymyslac)

- `PeopleUnit.Base(wies)` = ludzie wsi / srednia swiata ludzi na hearth (`A\PeopleUnit.cs:96-110`,
  `PopulationLaw.WorldPeoplePerHearth` `A\PopulationLaw.cs:351-383`) - "hearth sredniej wsi swiata". Juz uzywana: pula wyrzutkow
  (`A\OutlawLaw.cs:310-315, 507, 813, 1008`) i wagi poboru (`A\PeopleBook.Origin.cs:1149-1154`).
- Czynnik wsi `c_v = Base / hearth = k_v / 189.2` [S z P]: reach 2.35, volantine 2.91, qartheen 3.29, vlandia 1.45, vale 1.17,
  river 1.04, stormlands 0.96, battania 0.70, aserai 0.47, sturgia 0.25, crownlands 0.14, freefolk 0.22, nightswatch 0.02.
  Rozklad po 571 wsiach [S z P CSV]: min 0.02, 10% wsi ponizej 0.25, mediana 0.96, 90% ponizej 2.35, max 3.44;
  **srednia po wsiach (kazda wies liczona raz) 1.02** - czyli suma produkcji swiata przy takim czynniku zmienia sie o ok. +2%
  (rozklad na towary - nie liczony).
- To samo liczone od ludzi wsi wobec sredniej wsi swiata (72.8 tys.): min 0.017, mediana 0.81, 90% ponizej 2.13, max 4.15;
  79 wsi ponizej 0.25, 152 ponizej 0.5, 75 powyzej 2, 15 powyzej 3 [S z P].
- Przyklad Jeffa: wies zboza dzis 55 sztuk/d wszedzie [P L:464]; z czynnikiem c_v x (hearth / hearth sredni): Polnoc ok. 37, Reach ok. 136,
  stosunek 3.72 [S].
- Miasta: ludzie miasta wobec sredniego miasta (108 tys.) - min 0.0, 10% miast ponizej 0.03, mediana 0.31, 90% ponizej 2.7, max 5.8 [S z P].
  **Za ostre i niewiarygodne (1.4) - dla miast uzyc ludnosci rynku** `WorkshopLaw.MarketPeople` (`A\WorkshopLaw.cs:829-857`;
  52.04 mln w rynkach 97 miast = srednio 536 tys., L:305), ktora nie zeruje miast bez wlasnych ludzi.

### 3.2 Ktore wzory da sie przeskalowac jednym czynnikiem w jednym miejscu

| ogniwo | miejsce | jeden czynnik? | uwagi |
|---|---|---|---|
| towar i wydobycie wsi | `A\MaterialLaw.cs:173-238` (obok czynnikow rak, wzor "czynnik x suma") | **TAK** | magazyn wsi i tabor wolaja ten sam model, wiec skaluja sie same (`Village.cs:248-257`, `DefaultPartySizeLimitModel.cs:415-425`); drugie miejsce: las wsi `VillageWoodlot.PiecesPerDay` (`A\HarvestValve.cs:37-38`) |
| zywnosc wsi (model gry) | 3 nasze postfiksy na `CalculateDailyFoodProductionAmount` | TAK, ale KOLIZJA | spichlerz dzieli ten wynik przez (poziom + 1) i mnozy przez ludzi (`A\Famine.cs:577-579`, :443) - skala wlozona tutaj policzylaby ludzi DWA razy; trzeba ja tam zdjac |
| zywnosc miasta: skladnik wsi | `A\VillageBook.cs:1653-1669` | TAK | druga strona (zuzycie dobrobyt/40) plaska - sam doplyw x4 da Reach wieczna nadwyzke i premie dobrobytu (:132-137 modelu gry); trzeba skalowac obie strony albo zadnej |
| rece rzemieslnikow | juz od ludzi | - | zdjac mozna podloge "dawny wzor" (35 z 97 miast) i sufit 1000 |
| warsztaty notabli | `A\WorkshopLaw.cs:820` | TAK | bez surowca nic nie da |
| popyt miasta | slownik budzetu w `UpdateDemandShift` + `AnchorBudgetPostfix` (`A\TownFolk.Live.cs:332, 413-429`) | NIE sam | kotwica bierze min(wydatek gry, koszyk) - trzeba naraz: budzet gry, koszyk (glowy), zapasy startowe kies, kase kupcow |
| glowy klas kiesy ludu | `A\TownFolk.Probe.cs:723-725` (kalibracja) albo `Scale` `A\TownFolk.cs:428-434` | TAK (jedna funkcja `Scale`) | glowy sa w ZAPISIE (`arm_townfolk`, `A\TownFolk.Live.cs:1272-1287`) - zmiana tylko od nowej kampanii albo przez r |
| oplata stala i zakupy wsi | `VillHeads` `A\TownFolk.Live.cs:927` | TAK | dzis glowy BK; przejscie na ludzi ksiegi = x10..x130 (Reach 180 tys. wobec 1 992) - kiesa wsi tego nie udzwignie bez skali utargu |
| podatki pana, korona, daniny | brak wlasnego miejsca | nie trzeba | ida za utargiem i kasa - przeskaluja sie same, gdy przeskaluje sie produkcja i handel |
| tempo werbunku | `Levy.ProbabilityPostfix` `A\Levy.cs:115-141` | CZESCIOWO | szansa na miejsce nie przekroczy 1 na dobe; liczba miejsc u notabli stala - sufit gry |
| milicja | modele milicji (gra, BK) | TAK | u nas juz bramka wolnych rak |
| kasa i zapas kupcow | `A\Seal31.cs:100` + regulator gry + limit BK (3 miejsca, 3 wlascicieli) | NIE | trzy wzory z dobrobytu musza zmienic sie razem |
| dobrobyt | - | **nie ruszac** | to miara zamoznosci na glowe, nie wielkosci |

### 3.3 Sufity i progi do zdjecia albo obejscia

1. **Prog hearth 200 / 600** (`Village.cs:320-331`): skok x0.5 / x1 / x1.5 w produkcji, zywnosci wsi i zywnosci warowni. Przy ciaglym
   czynniku ludzi ten skok jest zbedny - wzorzec zdjecia juz jest (dzielenie przez poziom + 1, `A\Famine.cs:577`).
2. **Bramka magazynu 1.5 x** i udzwig taboru wsi: magazyn skaluje sie z modelem, ale tabor rosnie tylko do `hearth / 20` ludzi przy
   produkcji >= 40 sztuk (`DefaultPartySizeLimitModel.cs:423-424`) - wies Reach z x2-x3 moze sie zatykac. Mamy `A\MarketCarts.cs` i
   diagnoze `A\VillageClogDiag.cs` - czy wozy targowe to pokryja, **nie wiem** (do sprawdzenia przed projektem).
3. **Regulator swiata HarvestValve**: mnoznik na towar dla calego swiata, podloga 0.1, w gore najwyzej x1.3 (`A\HarvestValve.cs:31-36`).
   Jesli skala zmieni sume swiata, regulator w 56 dob sciagnie ja z powrotem do zuzycia - skala musi byc znormalizowana (srednia 1)
   albo popyt musi urosnac razem z podaza.
4. **Kasa kupcow 10 000 + 12 x dobrobyt** (ok. 70-110 tys. na miasto) i kiesa wsi (limit BK 10 000 + 3 x hearth, ok. 11 tys.): przy
   wiekszym obrocie pierwsze zabraknie zlota w kasie ("brak zlota/kupca 288" cykli warsztatow dziennie juz dzis [P L:128929]).
5. **Popyt od dobrobytu** (`DefaultSettlementEconomyModel.cs:61-73`) i **koszyk od glow BK** - dwa sufity popytu naraz.
6. **Zywnosc warowni**: zuzycie dobrobyt / 40, zapas do 300 / 500 - nie widzi ludzi.
7. **Dobrobyt**: koszty mieszkan ciagna kazde miasto do 6-9 tys.; wpisanie "duzego" dobrobytu nie utrzyma sie.
8. **Rece**: podloga dawnego wzoru (Zelazne Wyspy 148 rak przy 0.47 mln ludzi = 0.32 na 1000, trzy razy wiecej niz Reach) i sufit 1000.
9. **Werbunek**: liczba notabli x miejsca x szansa <= 1; zaloga 2 rekrutow na dobe.
10. **Spichlerz**: q najwyzej x3 (`A\Famine.cs:57`), sufit 2 lata.
11. **Pulap ludzi na hearth 650** (Wyspy Letnie).

### 3.4 Co zrobi BannerKings i gra, gdy zmienimy hearth / dobrobyt zamiast mnozyc wynik

Hearth czytaja [P grep po dekompilacjach]:
- gra (ok. 25 miejsc): przyrost pasmami 300 / 600 (`DefaultSettlementProsperityModel.cs:45` - u nas zastapiony), milicja hearth / 400
  (`DefaultSettlementMilitiaModel.cs:114`), tabor (`DefaultPartySizeLimitModel.cs:424`, `VillagerCampaignBehavior.cs:154-187`),
  szkody rabunku 0.5 x hearth (`MapEvents\RaidEventComponent.cs:174-175`), wymuszenia gracza hearth x 0.15 i hearth / 30
  (`VillageHostileActionCampaignBehavior.cs:555, 604-619`), wartosc osady 100 000 + hearth x 250 (`Settlements\Settlement.cs:805`,
  `DefaultSettlementValueModel.cs:255-260`), sila frakcji (`Helpers\FactionHelper.cs:237`), zadania (+30..+80 hearth), incydenty;
- BK (ok. 15 miejsc): ludnosc startowa hearth x 4-6 i sufit hearth x 4 (`PopulationManager.cs:478`, `BKGrowthModel.cs:75`) - **BK
  dorobi sobie ludzi do nowego sufitu**, a za nimi pojda jego podatki klas, pula poboru, produkcja zywnosci miast, milicja hearth / 400
  (`BKMilitiaModel.cs:99`), limit kasy wsi hearth x 3 (`BKEconomyModel.cs:617`), majatki (`BKEstatesModel.cs:362`,
  `BKLandGrantBehavior.cs:196`), jency z rabunku hearth x 4 (`BKRaidCaptureModel.cs:38`), wynik wojny (`BKWarModel.cs:372`),
  liczba z hearth / 200 w `VillageData.cs:115` (czego dotyczy - nie czytalem), progi 400 / 1000 (`Patches\VanillaModelTweakPatches.cs:687-691`);
- nasza ksiega: ludzie = hearth x k_v z k_v w zapisie ("v:") - zmiana hearth ZMIENIA LICZBE LUDZI, chyba ze k_v odwrotnie
  przeliczymy na kazdej osadzie; podlogi w hearth (10, 50), powrot uchodzcow, kroki floata (`A\PeopleUnit.cs:112-130`), prog
  spustoszenia - wszystko jest w punktach hearth.
Dobrobyt czyta 25 plikow BK oraz gra (zywnosc / 40, popyt, milicja / 1000, cel kasy x 12, limit BK x 25, rece dawne / 170, wskaznik placy
/ 4800, wielkosc zalogi) - i model sam sciaga go z powrotem.
**Wniosek: mnozyc WYNIK, nie ruszac hearth ani dobrobytu.** Hearth zostaje "ludzmi w jednostkach lokalnych", k_v jest mostem.

### 3.5 Kolizje z istniejacymi zmianami (zasada 0)

- Podwojne liczenie ludzi: spichlerz (`A\Famine.cs:577-579`) i zywnosc warowni (`A\VillageBook.cs:1663-1667`) czytaja ten sam model
  zywnosci wsi; czynniki rak (spustoszenie, wojna) mnoza sie z nowa skala - to zamierzone, ale ksiega rudy (`OreLedger.NoteModel`,
  `A\MaterialLaw.cs:235`), regulator wsi (pomiar produkcji okna 56 dob) i progi narzedzia `sprawdz_logi` maja bazy sprzed skali.
- `HamletKinds` (ryby = zdjete zboze, ostatni krok, `A\MaterialLaw.cs:229-231`) i sufit dymarki (`Bloomery.Cap` :232-234) musza zostac
  PO nowym czynniku.
- Kiesa ludu trzyma glowy w zapisie i ma kontrole zamkniecia co do zlotowki - zmiana glow w biegu kampanii zmienia potrzeby, zapasy dni,
  Afford i kary glodu C2 (`A\TownFolk.Live.cs:852`).
- Regula budzetu rodow (`A\ClanBudget.cs:193-212`) uczy sie D z 28 dob - skok dochodu Reach przejdzie do zoldu dopiero po miesiacu.
- Zelazne Wyspy, Wolni Ludzie, Dothrakowie przy czynniku 0.2-0.25 traca wiekszosc dochodu z ziemi - ich D ma juz skladnik lupu
  (`A\ClanBudget.cs:23-24, 54`), ale czy wystarczy, **nie wiem**.
- Stary zapis: k_v i glowy kiesy zamrozone - skala oparta o k_v dziala od razu takze na starym zapisie (Jeff i tak zaczyna nowa kampanie).

### 3.6 Ryzyka dla wydajnosci

- `MaterialLaw.ProdPostfix` biegnie dla kazdej pary (wies, towar) w ticku, w dymkach, w `GetWarehouseCapacity` (takze co godzine przy
  wysylce taboru - `A\HamletKinds.cs:45`) i moze isc z watkow AI (`A\PeopleBook.cs:153`). Sam odczyt `hearth x k_v` to jeden slownik
  (`A\PeopleBook.cs:820-831`) - tani; `Devastation.YieldFactor` robi to samo juz dzis.
- Wzorzec bezpieczny: pamiec doby podmieniana jedna referencja, czytana bez zamka (`A\FreeHands.cs:45, 187`); ludnosc rynku liczona raz na
  dobe (`A\WorkshopLaw.cs:829-857`).
- Nie wolac BK w goracej sciezce: `GetPopData` przeszukuje slownik liniowo (`BK\...\PopulationManager.cs:178`).
- Dzis bieg roczny: 12.0 s na dobe [P STAN-PRAC, WGRANIE 37]. Skala jako mnoznik = pomijalny koszt [S]; drozsze bylyby tylko nowe
  pelne spisy (ich nie potrzeba).

### 3.7 Poprawki wydajnosci na czlowieka - co ma miare w swiecie gry

| poprawka | miara w kodzie | granice dzis | ocena |
|---|---|---|---|
| pora roku / zima | `WinterBite` (ciecie plonu wedlug pory, szerokosci i krainy: Wyspy Letnie 0, cieple poludnie 0.4, reszta 1; `A\WinterBite.cs:13-33`), pory wieloletnie `A\WesterosClimate.cs:13-32` | bieg zimy: plon -40.8% [P STAN-PRAC] | **juz jest** - nie dodawac drugi raz |
| klimat staly (co moze rosnac) | typ wsi poprawiony wedlug klimatu `A\VillageClimate.cs:13-51` | zmiana typu, nie ilosci | jest; zyznosc jako ILOSC: BK ma `LandData.fertility` 0.5-1.0 z terenu mapy (`BK\...Populations\LandData.cs:242-286`) - miara istnieje, ale w nieczynnym modelu BK; do rozwazenia |
| bezpieczenstwo | `PopulationLaw.DangerOf` 0..1 (bezpieczenstwo warowni, wojna, spalone wsie; `A\PopulationLaw.cs:520-538`), rece po spustoszeniu, rece w wojnie | 0..1 | jest w przyroscie i w plonie (ubytek rak) - wystarczy |
| dobrobyt / rozwoj | `PopulationLaw.ProsperityIndex` -0.5..+0.5 wobec mediany krainy (`A\PopulationLaw.cs:443-485`); `TownWage.Index` 0.5..1.5 (`A\TownWage.cs:19-25`); premia budowli warowni do produkcji wsi juz w modelu gry (`DefaultVillageProductionCalculatorModel.cs:63-66`) | jak obok | **miara jest**, dzis dziala na przyrost i place, nie na plon |
| narzedzia zelazne | wies kupuje w miescie targowym sol, narzedzia, len, garnki, piwo; kod zapisuje potrzebe i zakup kazdej wsi na dobe (`DVNeed`, `DVBuy`: `A\TownFolk.cs:120`, `A\TownFolk.Live.cs:945-958`, kategorie :966); linia narzedzi warsztatow wedlug zapasu (`A\ToolsByUse.cs:13-22`) | 0..1 (udzial zaspokojonej potrzeby) | **miara jest gotowa**, ale liczona dla calego koszyka, nie osobno dla narzedzi - wymaga rozdzielenia |
| technologia kultury | BK `InnovationsManager`: ciezki plug x1.12, chomato x1.25, trojpolowka x1.4 w `LandData.GetAcreOutput` (`BK\...\LandData.cs:334-353`) | - | miara w BK istnieje, ale siedzi w nieczynnym modelu; czy innowacje w tej konfiguracji w ogole postepuja - **nie wiem**; zostawic na pozniej |

### 3.8 Czego nie wiem (do sprawdzenia przed projektem)

1. Ktory model zywnosci stoi pod `ROTSettlementFoodModel` (BK +15% lokalnej ziemi czy sama gra).
2. Czy wozy targowe (`MarketCarts`) wywioza wies z produkcja x2-x4 bez zatkania magazynu.
3. Rozklad czynnika wedlug TOWAROW (gdzie leza kopalnie, len, konie) - suma swiata +2% to srednia po wsiach, nie po towarach.
4. Czy innowacje BK postepuja; jak dokladnie liczy sie limit druzyn (ROT / BK).
5. Ludnosc rynku na miasto (rozklad) - znam tylko sume 52.04 mln i rece min / mediana / max 6 / 41 / 354 [P L:305].
6. Wiarygodnosc 4 mln Qarthu i 5 mln Volantis (fanowskie) - przy skali od ludzi beda to najbogatsze osady swiata.
7. Jak liczone sa "wplyw rodow" i "zarobek mieszczan" w tabeli z polecenia poza tym, co widac w skrypcie
   `docs\ludnosc\kr-ludnosc-a-zarobek.py` (srednie 28 dob z `budzet-rodow.csv` i 4 probek `kiesa-ludu.csv`) - sam skrypt czytalem,
   liczb nie przeliczalem ponownie.
