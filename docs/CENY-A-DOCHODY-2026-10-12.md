# Ceny a dochody - czy wszystko jest za tanie? (2026-10-12, tylko odczyt)

Pytanie Jeffa: "czy aktualne zarobki krolestwa do cen w sklepach sa na odpowiednim poziomie wedle modelu ekonomii,
czy nie jest teraz wszystko za tanie?"

**Zrodla.** Log ostatniego testu 120 dob `Modules/Armoury/Armoury-2026-10-12_02-44-09.log` (36 104 linii; liczby =
srednie z OSTATNICH 28 DOB, numery linii podane przy kazdej liczbie); wartosci przedmiotow po przeliczeniu:
`Documents/.../CrashScribe/items-dump.csv` (12.10 03:22, ta sama sesja); kod: scratchpad `e5/wsie/Armoury/src`
(najnowszy); wzor ceny gry: dekompilacja `dec32/cs_full/TaleWorlds.CampaignSystem.GameComponents/DefaultTradeItemPriceFactorModel.cs`.
Model historyczny: `docs/CENY-HISTORYCZNE.md` (1 zl = 1 pens, Anglia 1300-1349), `docs/PLAN-CENY-HISTORYCZNE.md`.
**Kalendarz:** dochody roczne z historii dziele przez **364** (rok Armoury, `WeeksPerSeason = 13`). Tabele historyczne
sa roczne (L/rok), wiec nie dotyczy ich stary blad "168 dni".
Oznaczenia: **[S]** = szacunek (moj albo oznaczony SZACUNEK w CENY-HISTORYCZNE), reszta = log, kod albo seria Clarka.

---

## 0. Odpowiedz w skrocie

- **Ceny przedmiotow SA juz historyczne** - etapy 1-3 planu sa w kodzie i dzialaja w tym tescie (uzbrojenie 3 264 szt.,
  suma wartosci 34 337 341 -> 1 601 561, czyli srednio /21; log l.223). Kolczuga, plyta, miecze, strzaly, zboze, mieso, piwo,
  wino, konie juczne i destriery stoja w widelkach z 1300-1349.
- **Zold zolnierza tez jest historyczny** (5.0 zl/dobe w partiach lordow). Dla zolnierza ceny sa W NORMIE: kolczuga = ok. 3 miesiace
  zoldu po wartosci, ok. 7 miesiecy po cenie faktycznie placonej (sredniej zbroi brakuje na polkach).
- **"Za tanie" jest prawda wzgledem dochodow LORDOW i GRACZA**, nie wzgledem historii:
  - mediana rodu AI ma 3 206 zl/dobe "D" (ok. 4 860 L/rok = dochod duzego hrabiego, nie barona) - plyta t6 to dla niego 0.9 dnia
    (historyczny hrabia: 1.5-3 dni, baron: 6-29 dni, rycerz: ok. 100-200 dni);
  - ale 42% tego "D" to przeplyw z korony (zwrot zoldu + renty), a po zoldzie 228 z 242 rodow jest NA MINUSIE - lordowie nie
    plywaja w pieniadzach, tylko utrzymuja duze armie;
  - gracz dostaje kwoty stale w skali "lorda" (zadania 250-5 000, sakiewki turniejow 3 000-15 000, okupy), ktorych nikt nie
    zmniejszyl, a sprzet potanial 21 razy wobec vanilli - stad wrazenie, ze "wszystko jest tanie".
- **Za drogie** (wobec historii): rzad konski (siodla 10-25x, barde 5-24x), helmy t4-6 (bascinet 3.8x), luki wojenne t3-6 (3-10x),
  palafreje/rounceye t3 (do 6x wobec rounceya), oraz towary z prawdziwym niedoborem (ser 7.5x, maslo 4.6x, drewno u wozow 10x,
  plotno - indeks na suficie 10).
- **Korona** jest w normie: srednie krolestwo 16 183 zl/dobe = 0.8 x skarbu Anglii ok. 1300 (19 780 zl/dobe), Polnoc 2.5 x.

---

## 1. Co z planu cen historycznych jest naprawde w kodzie

Kod: `scratchpad/e5/wsie/Armoury/src`. Kolejnosc przy starcie sesji: `ArmouryBehavior.cs:1216`
`MaterialLaw.Apply(); ArmsPricing.Build(); HistoricalPrices.Apply(); StartStock.Run(); ...`. Wlacznik `HistoricalPricesEnabled = true`
(`Settings.cs:608`); w MCM Jeffa (`Configs/ModSettings/Global/Armoury/Armoury.json`, 8.10) **nie ma zadnego klucza Hist*** - obowiazuja
domyslne z kodu.

| Etap planu | Co | Gdzie (plik:linia) | Stan | Dowod w logu |
|---|---|---|---|---|
| 1. Surowce kuzni | ruda 0.075, drewno 0.035, wegiel 0.07, surowka 1.4, zelazo kute 2.5, zelazo 3.5, stal 6, stal szl. 8, stal zamkowa 12 zl/kg; ruda i drewno w ladunkach 100 kg | `HistoricalPrices.cs:350-380, 408-415`; ceny `Settings.cs:609-617`; ladunek `Settings.cs:628` | **ZROBIONE** | l.222-223: iron 50->8 (100 kg), hardwood 25->4, charcoal 50->1, ironIngot1-6 20/30/60/100/160/260 -> 1/1/2/3/4/6 |
| 1b. Popyt rynku w nowej monecie | popyt kategorii / przelicznik (srednia geometryczna stara/nowa), budzet BK, polka kategorii mieszanych | `HistoricalPrices.cs:433-481, 816-825, 832-854, 586-687` | **ZROBIONE** | l.220: 76 kategorii (np. iron /23.5, light_armor /25.2, heavy_armor /46.3) |
| 2. Bron i zbroje | Value = koszt historyczny: kg metalu x cena gatunku + paliwo + skora/len/drewno + dni pracy x dniowka mistrza (3 zl t1 + 1.5 zl/tier) x jakosc, zysk 25%; unikaty x4; wykute w trakcie sesji tez | `HistoricalPrices.cs:245-258` (HistCost), `:417-432` (Apply krok 2), `:302-319` (PriceOne); dniowka `Settings.cs:621-623`; ilosci fizyczne `ArmsPricing.cs:188-256`; blokada cudzych nadpisan `HistoricalPrices.cs:78-107, 116-134` | **ZROBIONE** | l.223: 3 264 szt., 34 337 341 -> 1 601 561; l.296: luki ROT-RBM przywrocone (weirwood 90 000 -> 61) |
| 2b. XP kowalstwa od dawnej wartosci | postfiks na DefaultSmithingModel | `HistoricalPrices.cs:897-906, 953-959` | ZROBIONE | l.83 |
| 3. Towary | 44 towary za kg (skora, plotno, skory i len - w kroku 1) (zboze 0.31, piwo 0.3, wino 1.4, mieso 0.9, ser 1.3, welna 8.3, aksamit 400, przyprawy 27...) + zywy inwentarz (wol 157, krowa 113, swinia 30, owca 17, ges 4, kura 1) | `HistoricalPrices.cs:41-56, 65-68, 384-406`; wlaczniki `Settings.cs:629-630` | **ZROBIONE** | l.223 (grain 10->3, beer 50->3, wine 90->14, velvet 575->4000...), l.224 |
| 4. Stale kwoty | spis stalych (SPIS-CEN-GRY 2.0); kwoty-ceny uznane za juz pensowe (nagrody zadan, karawany, okupy, sakiewki turniejow); okna nagrod turniejowych i pulap zamowien porownywane z wartoscia SPRZED przeliczenia | `FixedSums38.cs:15-40` (opis), `HistoricalPrices.cs:912-918`; spis: `scratchpad/e5/wsie/docs/SPIS-CEN-GRY.md:245-276` | **PRAWIE** - zostaly kwoty tylko gracza (lapowka strazy 5 000/3 000, golibroda 100, karczmarz 500 - krok 3.9) | - |
| 5. Nasze ustawienia | kuznia 3 zl/dzien, placa warsztatu 3 zl, praca jenca 1-1.5 zl, straz 2 zl, zloto kryjowki 150 | Armoury `Settings.cs:68-69, 219, 548`; RC `Settings.cs:134-144` | **ZROBIONE** (w wiekszosci) | - |
| (poza planem) Konie | **Value z gry - celowo bez zmian** ("konie juz historyczne") | `HistoricalPrices.cs:28` | NIE RUSZANE | items-dump: sumpter 99, saddle 140, t3_ destriery 10 000, charger 10 973 |

**Odpowiedz na pytanie 1:** Value broni i zbroi jest dzis w **skali historycznej**, nie w skali gry (wzor 100-120 x 2.75^tier
przestal byc cena - `ArmsPricing.BaseOf` przy wlaczonych cenach historycznych zwraca juz Value bez widelek, `ArmsPricing.cs:278`).
Mediany Value po przeliczeniu (items-dump, merchandise): zbroja tulowia t1 29 / t2 92 / t3 166 / t4 348 / t5 1 212 / t6 2 798;
helm t1 24 ... t6 298; bron 1h t1 8 / t3 30 / t5 116 / t6 182; luk t1 12 ... t6 159; strzaly 11-14 za stos.

---

## 2. Dochody (log, srednie 28 dob)

### 2.1 Korona (`Korona: wplywy dnia`, l.27856-35959)

| | zl/dobe | L/rok (x364/240) | wobec skarbu Anglii ok. 1300 (30 000 L/rok = 19 780 zl/dobe) [S - Dyer] |
|---|---|---|---|
| razem 29 krolestw | 469 319 | 711 800 | - |
| srednia na krolestwo | 16 183 | 24 500 | 0.8 x |
| mediana | 11 931 | 18 100 | 0.6 x |
| najwieksze: The North | 49 356 | 74 900 | 2.5 x |
| dalej: Baratheon KL 42 596, Vale 33 301, Stormlands 30 792, Reach 29 763, Dorne 27 210 | | | 1.4-2.2 x |
| najmniejsze: Skagos 235, Yi Ti 420, Summer Isles 654, Ibben 3 717 | | | 0.01-0.2 x |

Wiekszosc wplywow korona oddaje: zwrot zoldu rodom srednio **277 102**, renty (180) **240 077** zl/dobe (l.27856-35959).
Skarbce: mediana ok. 1.6 mln, kiesa krola mediana ok. 268 tys. (l.27858-35989; regex objal 25 z 29 krolestw).

### 2.2 Rody (`Budzet rodow (na sucho)`, l.27898-36000; 312 rodow AI)

| D - dochod staly 28 dob | zl/dobe | L/rok | odpowiednik historyczny (CENY-HISTORYCZNE rozdz. 9) |
|---|---|---|---|
| mediana | **3 206** | 4 860 | duzy hrabia (hrabia 400-11 000 L; baron 200-500 L = 132-330 zl/dobe; rycerz 20-40 L = 13-26 zl/dobe) |
| 10% | 7 | 11 | nic (rody bez lenna, rycerze BK) |
| 90% | 8 999 | 13 650 | wiecej niz Lancaster (ok. 11 000 L) |
| razem | 1 243 477 | | |

- "D" liczy wplywy z zewnatrz rodu: dochod brutto modelu + renty + **zwrot zoldu korony** + trzecie, okupy... (`ClanIncomeBook.cs:12-18`).
  Zwrot i renty to 277 102 + 240 077 = **42%** D razem (l.27856-35959 / l.27898-36000). Mediana bez nich [S]: ok. 1 860 zl/dobe
  (= hrabia ok. 2 800 L/rok).
- Linia (166) liczy D waziej: razem 691 847 zl/dobe dla 312 rodow, srednio 2 217 (l.27895-35997).
- **Po zoldzie:** saldo dodatnie u 14 rodow (+8 952), ujemne u **228** (-585 650 zl/dobe); zold naliczony 634 352 przy pulapie
  budzetu 1 063 809 (l.27898-36000). Zold to ok. 51% D razem.
- Kontrola z `economy-2026-10-12_02-46-19.csv` (te same 28 dob): `dochod_model` gry (bez rent i przeplywow naszych modow) -
  mediana rodu krolestwa 16 zl/dobe, krola 686; kiesa glowy rodu mediana 101 266, krola 234 669. Czyli dochod z samego modelu gry
  prawie zniknal - rody zyja z rent PopulationLaw i z przeplywow korony.
- Na glowe ludnosci, ktora rody reprezentuja (`Miara historyczna cz. 2`, l.27910-36012): panowie **4.82 zl rocznie na glowe tabeli**
  (86.8 na glowe BK) wobec zalozenia modelu `PopulationRentPerHead = 40` (`Settings.cs:648`). Rod jest bogaty, bo jest WIELKI
  (312 rodow na ok. 52 mln ludzi tabeli [S: 701 899 x 364 / 4.9]), a nie dlatego, ze bierze duzo z glowy.

### 2.3 Zold (`Zold`, l.27887-35990; `Zold konnych`, l.27937-36039; `Rycerze`, l.35948)

| | ludzi | zold naliczony / stawki modelu | zl na glowe na dobe |
|---|---|---|---|
| partie rodow AI | 89 583 | 450 378 (naliczony) | **5.03** (model bez mnoznikow BK 5.30) |
| garnizony | 76 444 | 185 829 (naliczony, garnizon -50% BK) | 2.43 (model 4.64) |
| karawany | 23 965 | 233 145 (model) | 9.73 (polowa konna) |
| rycerze BK w sluzbie | - | 24 zl/dobe | 24 (= rycerz 1316/1340s) |

Historia (CENY-HISTORYCZNE rozdz. 8): piechur 2, lucznik 2-3, hobelar 4-6, zbrojny 12, rycerz 24, banneret 48 zl/dobe.
**W normie** - srednia 5 zl to armia z przewaga piechoty i lucznikow, z domieszka konnych.
Zold wplacony do sakiewek ludzi 696 335/dobe; ludzie wydali na zycie w miastach 79 881 + u markietanow 206 571 = ok. 1.7 zl na
zolnierza dziennie (34% zoldu; l.27732-35831) - zgodne z wyzywieniem 1-3 zl (pachol/yeoman, Hodges). Sakiewki ludzi razem ok.
11.2 mln (l.27887-35990).

### 2.4 Lud miast (`Kiesa ludu`, l.27963-36066)

- Ludzie ksiegi 10 421 301 (BK: 1 442 738); lud zaplacil srednio **417 958 zl/dobe** (wydatek gry 422 407, w tym jedzenie 111 176);
  kiesy ludu 17.8 mln.
- Na glowe BK: **0.29 zl/dobe = 105.5 zl/rok**; na osobe ksiegi: 0.040 zl/dobe = 14.6 zl/rok.
- Historia [S]: gospodarstwo chlopskie 480-1 200 zl/rok brutto na ok. 4.5 osoby = ok. 100-270 zl/rok na osobe. Na glowe BK lud jest
  na dolnej granicy normy; na osobe ksiegi wydaje 7-18 x mniej - to sprawa skali ksiegi (1 glowa BK = ok. 7 osob ksiegi), nie cen.
- `Miara historyczna cz. 2` (l.27910-36012): lud 60.5 zl rocznie na glowe BK, panowie / lud 1.4-1.6.

### 2.5 Inne

- Okupy AI-AI: 3.75 dziennie, srednio **40 104 zl za okup** (= 167 L, okup barona [S]; l.27922-36024).
- Kupcy wywozacy nadwyzke uzbrojenia: 72 725 szt. za 9 774 860 = 134 zl za sztuke (l.27930-36032).
- Skup sprzetu: lordowie AI dostaja 42.9% wartosci, inne partie 69.0%, sakiewki ludzi 8.3% (wraki), notable 24.2% (l.27933-36035).

---

## 3. Ceny faktycznie placone

### 3.1 Zywnosc i surowce - miasta placa wozom wsi (`Towary (utarg wozow)`, l.27814-35920)

| Towar (sztuka) | Value | placone (zl/szt.) | x Value | historia (CENY-HIST. rozdz. 1, 12) | placone / historia |
|---|---|---|---|---|---|
| zboze (10 kg) | 3 | **5.15** | 1.75 | pszenica 0.31 zl/kg -> 3.1 | 1.7 |
| ryby (10 kg) | 10 | **6.83** | 0.68 | sledz ok. 1 zl/kg [S] -> 10 | 0.7 |
| ser (10 kg) | 13 | **97.0** | 7.77 | 1.3 zl/kg -> 13 | **7.5** |
| maslo (10 kg) | 24 | **110.1** | 4.94 | 2.4 zl/kg -> 24 | **4.6** |
| jajka | 8 | 58.8 | 7.44 | ok. 0.8 zl/kg | ok. 7 |
| sol (10 kg) | 2 | 3.88 | 2.01 | 0.18 zl/kg -> 1.8 | 2.2 |
| drewno (ladunek 100 kg) | 4 | **40.5** | 10.15 | 0.035 zl/kg -> 3.5 | **11.6** |
| ruda (ladunek 100 kg) | 8 | 2.63 | 0.33 | 0.075 zl/kg [S] -> 7.5 | 0.35 |
| welna (10 kg) | 83 | 53.0 | 0.65 | 8.3 zl/kg -> 83 | 0.64 |
| krowa / swinia / owca | 113 / 30 / 17 | 99.0 / 32.0 / 22.8 | 0.9 / 1.1 / 1.35 | 113 / 24-36 / 17 | 0.9 / 1.0 / 1.3 |
| przyprawy (10 kg) | 270 | 623 | 2.41 | 27-80 zl/kg | 0.8-2.3 |
| jedwab surowy "cotton" (10 kg) | 1 000 | 403 | 0.43 | 800-1 000 zl/kg (tkanina [XV]) | za tanio [S] |

Piwo (Value 3 = 0.3 zl/kg; historia 0.2-0.4), wino (14 = 1.4 zl/kg; historia 1.4) i mieso (9 = 0.9 zl/kg; historia 0.7-1.1) nie szly
wozami wsi w tych 28 dobach - podaje Value; ich cena w sklepie = Value x indeks miasta (wzor 3.5).
Mediany indeksow surowcow w miastach (`Ceny surowcow`, l.27826-35932): ruda 0.59, drewno 0.60, skory 1.07, skora 1.46, len 1.20,
welna 1.20, **plotno 10.0 (sufit)** - plotna brakuje prawie wszedzie.

### 3.2 Uzbrojenie wedlug kupujacego (`ZakupyAI wedlug kupujacego`, l.27927-36029; suma 28 dob, zl na sztuke)

| Kupujacy | korpus | helm | reszta zbroi | tarcza | bron biala | bron strzelecka | amunicja | konie i rzedy |
|---|---|---|---|---|---|---|---|---|
| lordowie | 66.5 (7 720 szt.) | 78.1 | 40.2 | 67.6 | 45.2 | 44.0 | 15.9 | - |
| zalogi miast | **344.9** (1 604) | 131.6 | 83.0 | 71.4 | 82.8 | 57.4 | 15.9 | - |
| zamowienia zamkow | 305.3 (745) | 68.9 | 49.0 | 54.9 | 53.8 | 37.5 | 14.2 | - |
| notable dla ochotnikow | 258.4 (2 997) | 66.6 | 51.8 | 61.5 | 71.6 | 28.4 | 11.9 | **788.8** (174) |
| nadwyzki z sakiewek ludzi (sprzedaz) | 3.1 | 2.6 | 1.2 | 2.3 | 1.1 | 1.4 | 1.0 | - |

Lordowie kupuja glownie sprzet t1-t2 dla szeregowych; zalogi i notable - srednia zbroje (stad 250-350 zl za korpus).

### 3.3 Pojedyncze sztuki u lordow - cena / Value (`Dozbrajanie: <lord> w <miasto>`, l.27694-35840; Value z items-dump)

| Typ x tier (nowej sztuki) | n | cena mediana (zl) | cena / Value (mediana) |
|---|---|---|---|
| zbroja tulowia t1 / t2 / t3 / t4 / t5 | 162 / 6 / 12 / 1 / 1 | 7 / 62 / **401** / 688 / 1 535 | 0.52 / 1.11 / **2.25** / 2.41 / 4.50 |
| helm t1 / t2 / t3 | 142 / 24 / 7 | 25 / 58 / 111 | 1.38 / 2.34 / 2.13 |
| bron 1h t1 / t2 / t3 / t4 / t5 | 50 / 30 / 16 / 8 / 3 | 2 / 34 / 57 / 111 / 217 | 0.33 / 2.00 / 2.04 / 1.81 / 1.68 |
| luk t1 / t2 / t3 / t4 / t5 | 40 / 150 / 103 / 43 / 28 | 16 / 24 / 43 / 72 / 116 | 1.45 / 1.05 / 1.16 / 1.15 / 1.17 |
| strzaly t2-t5 | 162 | 9-13 | 0.82-1.08 |
| tarcza t3 / t4 / t5 | 25 / 21 / 17 | 43 / 78 / 81 | 1.70 / 2.02 / 1.42 |

Sztuki t1 ida taniej niz Value (zawalone polki: korpus t1-2 srednio 12 759 szt. na polkach), srednia zbroja 2-4.5 x drozej
(korpus t3-4 srednio **126 szt. na 97 miast**, t5-6 59; `Zbroja na polkach`, l.27926-36028). To jest niedobor, nie poziom cen.

### 3.4 Konie (`Konie AI`, l.27938-36040; Value z items-dump - konie NIE sa przeliczone)

- Lordowie kupili 643 konie za 888 588 = **1 382 zl za konia** (do zapasu: 381 zwyklych, 262 bojowe, 0 szlachetnych); sprzedali
  nadwyzke 379 za 115 293 = 304 zl/szt. Notable kupili 174 konie i rzedy po 789 zl (l.27927-36029).
- Value: juczny (sumpter) 99, wierzchowy (saddle_horse) 140, palafreje/rounceye t3 748-1 177 (mediana 1 049), rumaki t2_* (t4) 1 221-2 380,
  destriery t3_* 10 000, charger 10 973, szlachetne (noble_horse_*, nie na handel) 20 000, jednorozce 30 000, rydwany 22 000.

### 3.5 Co widzi gracz w sklepie (wzor)

- **Uzbrojenie i konie** (kupno w miescie/zamku): cena = Value sztuki ze stanem x **1.10** (`RetailMarkupPercent 10`, `Settings.cs:1087`;
  `SupplyDemand.cs:396-397`) x mnoznik polki ((popyt+1)/(na polce+1))^0.5 w granicach **0.25-4** (`SupplyDemand.cs:337-346`,
  `Settings.cs:1084-1088`) x wskaznik surowcow 0.5-1.5 (`ArmsPricing.cs:372-383`, `Settings.cs:468-469`; konie bez niego -
  `ArmsPricing.cs:377`). Cudze narzuty modeli cen pominiete (`RetailFromWorth`).
  Przyklady: kolczuga t4 (Value 468): 515 przy rownowadze, 129 na zawalonym kramie, do 2 060 na pustym - lordowie placili za t3-t4
  ok. 2.2-2.4 x Value, czyli kramy sa zwykle polpuste; plyta t6 (2 798): ok. 3 080; destrier (10 000): 11 000 (2 750-44 000).
- **Towary**: wzor gry `DefaultTradeItemPriceFactorModel.cs:176-181, 184-193` - czynnik = (popyt / (0.1 x podaz + 0.04 x wartosc polki + 2))^0.6
  (zwierzeta ^0.3), przyciety 0.1-10; cena kupna = sufit(Value x czynnik x (1 + kara)), kara ok. 0.06 x mnoznik umiejetnosci
  handlu (`:29-79`). U nas popyt w nowej monecie (`HistoricalPrices.cs:816-825`) i stala "2" w nowej monecie (`PriceFormulaInNewCoin`,
  `Settings.cs:645`, `RawPrice.cs:60`). Gracz placi mniej wiecej to, co miasta placa wozom (3.1), plus kilka procent [S]:
  zboze ok. 5-6 zl za 10 kg, ser ok. 100.

---

## 4. Tabela: cena, model historyczny, stosunek, ile dni dochodu

Dochody: **gra** - zolnierz 5.03 zl/dobe (l.27887-35990), mediana rodu D 3 206 (l.27898-36000), srednie krolestwo 16 183 (l.27856-35959);
**historia** - piechur 2 / zbrojny 12 zl/dobe, rycerz ok. 20 zl/dobe (20-40 L/rok), hrabia ok. 1 300 zl/dobe (ok. 2 000 L/rok [S]),
korona 19 780 zl/dobe.

| Rzecz | gra (zl) | historia (zl) | gra / hist. | zolnierz: dni zoldu gra / hist. (piechur; zbrojny) | lord: dni dochodu gra / hist. (hrabia; rycerz) | krolestwo: dni gra / korona hist. |
|---|---|---|---|---|---|---|
| zboze 10 kg | 5.15 (placone) | 3.1 | 1.7 | 1.0 / 1.6; 0.3 | ~0 | ~0 |
| ryby 10 kg | 6.83 (placone) | 10 [S] | 0.7 | 1.4 / 5; 0.8 | ~0 | ~0 |
| mieso 10 kg | 9 (Value) | 7-11 [S] | 0.8-1.3 | 1.8 / 4-6 | ~0 | ~0 |
| ser 10 kg | 97 (placone) | 13 | **7.5** | 19 / 6.5; 1.1 | 0.03 / 0.01 | - |
| piwo / wino 10 l | 3 / 14 (Value) | 2-4 / 14 | 1.0 / 1.0 | 0.6 / 1-2; 2.8 / 7 | ~0 | ~0 |
| przeszywanica t1 (aketon) | 48 | 36-72 [S] | 0.7-1.3 | 9.5 / 18-36; 3-6 | 0.01 / 0.03-0.06; 2-4 | - |
| **kolczuga t4** (hauberk 10 kg) | 468 (Value); ok. 1 075 placone [S x2.3] | 240-1 200 [S] (haubergeon 160-240) | 0.4-2.0 (placone 0.9-4.5) | **93 (placone 214) / 120-600; 20-100** | 0.15 / 0.18-0.92; 12-60 | 1 000 sztuk: 29 / 12-61 |
| **plyta t6** (mediana) | 2 798 | 1 920-3 920 | 0.7-1.5 | 556 / 960-1 960; 160-327 | **0.87 / 1.5-3.0; 96-196** | 0.17 / 0.10-0.20 |
| bascinet t5 | 182 | 48 | **3.8** | 36 / 24; 4 | 0.06 / 0.04; 2.4 | - |
| miecz t3 / t5 | 30 / 116 | 12-36 / 60-240 [S] | ok. 1 | 6 / 6-18; 23 / 30-120 | 0.01-0.04 / 0.01-0.18 | - |
| luk t2 / luk wojenny t4 | 22 / 63 | 12-18 | 1.2-1.8 / **3.5-5.3** | 4.4 / 6-9; 12.5 / 6-9 | ~0 | - |
| strzaly (stos ~ snop 24) | 11-14 | 15 | 0.8-0.9 | 2.4 / 7.5; 1.2 | ~0 | - |
| kon juczny | 99 | 60-160 | 0.6-1.7 | 20 / 30-80 | 0.03 / 0.05-0.12 | - |
| palafrej / rouncey t3 | 1 049 (mediana) | rouncey 160-480 [S], palafrej 480-2 400 [S] | 2.2-6.6 / 0.4-2.2 | 209 / 80-1 200 | 0.33 / 0.12-1.85 | - |
| rumak bojowy t4 (t2_*) | 1 221-2 380 (ok. 1 800) | kon zbrojnego 1 820-3 940 | 0.5-1.0 | 358 / 910-1 970; 152-328 | 0.56 / 1.4-3.0 | 0.11 / 0.09-0.20 |
| kon kupowany przez lordow (sredni) | 1 382 (placone) | 160-3 940 | - | 275 | 0.43 | 0.09 |
| **destrier** (t3_*) | 10 000 | 9 600-24 000 | 0.4-1.0 | 1 988 / 4 800-12 000; 800-2 000 | **3.1 / 7.4-18.5; 480-1 200** | 0.62 / 0.49-1.21 |
| siodlo / rzad t0 (mediana, 30 kg) | 588 | 24-60 [S] | **10-25** | 117 / 12-30 | 0.18 / 0.02-0.05 | - |
| barda t6 (140 kg ROT) | 11 522 | 480-2 400 [S] | **5-24** | 2 291 / 240-1 200 | 3.6 / 0.4-1.9 | 0.71 / 0.02-0.12 |

Z mediany rodu bez przeplywu korony [S: ok. 1 860 zl/dobe]: plyta = 1.5 dnia, destrier = 5.4 dnia - prawie dokladnie historyczny hrabia.

---

## 5. Wniosek (prostym jezykiem)

**Czy "wszystko jest za tanie"? Nie - ceny sa tam, gdzie mialy byc. Za tanie jest wzgledem tego, ile zarabiaja lordowie i gracz.**

1. **W normie** (wobec historii i wobec zoldu): zboze (1.7x, transport), mieso, piwo, wino, przeszywanica, kolczuga, plyta, miecze,
   strzaly, kon juczny, destrier; zold (5 zl), korona (srednio 0.8 x Anglii); wydatki ludu na glowe BK (105 zl/rok, dolna granica).
   Zolnierz potrzebuje ok. 3 miesiecy zoldu na kolczuge (po wartosci) albo ok. 7 (po cenie z polpustego kramu) - tak, jak
   piechur w XIV w. musial na nia pracowac miesiacami.
2. **Za tanie wzgledem dochodu - lordowie AI:** mediana rodu ma dochod duzego hrabiego, a 312 takich rodow to "hrabstwa" po ok.
   170 tys. ludzi [S]. Plyta to dla niego mniej niz dzien dochodu, destrier 3 dni (hrabia ok. 1300: 1.5-3 i 7-18 dni). Ale:
   42% jego "D" to zwrot zoldu i renty korony, a po zoldzie 228 z 242 rodow traci dziennie - tanszy sprzet nie robi z lordow
   bogaczy, armie zjadaja wszystko. Nie trzeba tu nic zmieniac.
3. **Za tanie wzgledem dochodu - gracz:** jego wplywy to kwoty stale w skali pensa, ale "panskie": zadania 250-5 000 zl (5 000 = 21 L =
   roczny dochod rycerza), sakiewki turniejow 3 000 / 8 000 / 15 000, start 1 000, okupy (srednio 40 104 u AI). Sprzet potanial 21 razy
   wobec vanilli, a te kwoty zostaly - jedno zadanie kupuje dzis dwie plyty. To jest zrodlo wrazenia "wszystko za tanio".
4. **Za drogie (wobec historii):** rzad konski - siodla 588 zl (10-25x), barde t6 11 522 (5-24x), bo ROT daje bardom 140-145 kg,
   a model liczy 5 dni pracy na kg kolczugi (`ArmsPricing.cs:208-212`); helmy t4-6 (bascinet 182 zamiast 48, kapalin t4 100 zamiast
   12-36); luki wojenne t3-6 (37-173 zamiast 12-18); palafreje/rounceye t3 (ok. 1 000 wobec rounceya 160-480).
5. **Drogie przez niedobor (nie przez cennik):** srednia zbroja t3-t5 (2-4.5 x Value; 126 korpusow t3-4 na 97 miast), ser, maslo,
   jajka (5-8x), drewno sprzedawane przez wsie (10x), plotno (mediana indeksu na suficie 10; 83 z 97 miast bez towaru, l.35932). Tu trzeba produkcji, nie cen.
6. **Za tanie (wobec historii):** ruda (0.35x - nadwyzka), welna (0.64x), ryby (0.7x), jedwab surowy (0.43 x Value); rumaki t4 na dolnej
   granicy konia zbrojnego (0.5-1.0x).

**Rada (kierunek, nie kod):**

- **Nie ruszac ogolnego poziomu cen** - etapy 1-3 planu sa zrobione i trafiaja w historie oraz w zold. Podnoszenie cen "bo za tanio"
  zepsuloby zolnierza i lud (oni sa w normie).
- **Dokonczyc odchylki w etapie 2** (poprawki modelu kosztu, jedna po drugiej): (a) rzad konski z realnej wagi/pracy (siodlo 24-60,
  barda 480-2 400); (b) dni pracy helmow t4-6 i lukow wojennych. Skutek w zamknietej ekonomii: te sztuki tanieja, mniej zlota plynie
  z kies lordow i notabli do warsztatow i kas miast (warsztaty zarobia mniej na tych liniach) - nic nie powstaje z niczego.
- **Konie (poza planem):** tabela wedlug kategorii jak zywy inwentarz (paczka 144): rouncey 160-480, palafrej 480-2 400, rumak
  zbrojnego 1 800-4 000, destrier 9 600-24 000. Skutek: zmiana cen, nie przeplywow; palafreje tanieja, rumaki t4 drozeja ok. 1.5-2x -
  konni lordow kosztuja wiecej, co pasuje do ich dochodu.
- **Jesli gracz ma czuc, ze sprzet kosztuje:** ruszyc strone dochodu gracza, nie ceny - nagroda zadania z kiesy zleceniodawcy
  (prawdziwy platnik zamiast "z niczego"), mierzona w dniach zoldu druzyny; sakiewki turniejow z kiesy gospodarza (juz tak jest).
  Zadnych doplat; skutek: mniej zlota z niczego, zadanie za 5 000 przestaje byc rocznym dochodem rycerza.
- **Resztki etapu 4 (krok 3.9):** lapowki 5 000/3 000, golibroda 100, karczmarz 500 - za drogie, tylko gracz, zloto w nicosc.
- **Niedobory** (srednia zbroja, nabial, plotno, drewno u wsi) - osobny temat produkcji; ceny tylko je pokazuja.

---

## 6. Zastrzezenia

- Ceny historyczne broni, zbroi i koni poza lukami/strzalami 1341, bascinetem, zbroja rycerza 1374 i srednimi Aytona to szacunki
  (CENY-HISTORYCZNE rozdz. 13). "Hrabia ok. 1 300 zl/dobe" to przyjeta wartosc srodkowa [S] (hrabiowie 400-11 000 L/rok).
- Ceny placone za korpus t4-t5 u lordow to pojedyncze sztuki (n = 1); "kolczuga placona ok. 1 075" = Value x 2.3 [S] z mediany t3-t4.
- Linia `Dozbrajanie` wypisuje najwyzej 6 sztuk z wizyty - probka, nie pelna suma.
- Ceny koni wedlug kategorii nie sa logowane (tylko suma zakupow lordow); podaje Value.
- Brak danych o handlu gracza w tym autotescie (gracz nie handlowal) - cena w sklepie gracza liczona wzorem (3.5).
- Skarbce: regex objal 25 z 29 krolestw (rozne formaty linii) - mediana orientacyjna.
