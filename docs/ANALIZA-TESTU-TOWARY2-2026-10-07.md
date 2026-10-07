# ANALIZA PIERWSZEGO TESTU GRUPY TOWARY 2 (115-120) - 07.10.2026

Log testu: `Modules\Armoury\Armoury-2026-10-07_03-18-52.log` (nowa kampania, 19 dob, dni 108836-108854). Porownanie:
`Armoury-2026-10-06_14-08-11.log` (wpisy 101-107, 20 dob). Kod = galaz robocza = `paczki/120-poprawki-po-audycie` (diff `Armoury` pusty).
Tylko odczyt - nic nie wgrane, nic nie zacommitowane. Oznaczenia: `L123` = linia logu 07.10, `S:L123` = linia logu 06.10.
Dekompilacje: `SCRATCH = ...\3cf3e0ac-...\scratchpad`; gra `SCRATCH\audyt-pieniadz\twall`, BK/BKROT `SCRATCH\ore-supply`.
SZACUNEK = liczba z rachunku/mechanizmu albo z pamieci (historia), nie z gry.

## 0. NAJWAZNIEJSZE (slowami)

1. **ZATKANE MAGAZYNY WSI - PILNE, regresja 119.** 1 -> 89 z 571 wsi stoi (L188 -> L1602), przybywa ok. 5 wsi na dobe (doby 6-19).
   Przyczyna policzona: woz jezdzi teraz srednio 109 jedn. w jedna strone (L1603), kurs z czekaniem trwa ok. 4.0-4.3 doby, a przy
   zwyklym zaladunku gry (59% stosow) magazyn wsi w stanie ustalonym dochodzi do 1.37-1.46 x pojemnosci - tuz pod bramka 1.5, przy
   ktorej gra wstrzymuje CALA produkcje wsi (takze zywnosci). Miernik 119 nie liczy czekania (ok. 0.5 doby). Kopalnie i drwale do tego
   nie mieszcza magazynu (ok. 4 t) w wozie x2 (ok. 2.9 t). Poprawka P1: **woz zabiera caly magazyn** (zawsze pelny, udzwig na miare ladunku,
   czekanie w mierniku). Skutek: zatkane ok. 1-3%, kopalnie 26 z 26, wywoz rudy z wsi 137 -> ok. 190 ladunkow/dobe.
2. **WARSZTATY - PILNE.** Wzor ceny (3 lata zysku + kapital) jest dobry, zly jest zysk: warsztat dostaje za wyrob cene kupna miasta bez
   limitu (latka BK), wsad prawie nic nie kosztuje (ruda srebra 20 d -> 2 x bizuteria 2400 d), a przy pustej polce indeks wyrobu siega 10.
   Placi kasa miasta, ktora odzyskuje to z "zakupow" mieszczan - zlota z niczego (407 tys./dobe, L1647); do notabli plynie 115 tys./dobe.
   JEDNA regula P2: **cena sprawiedliwa** - za wyrob cyklu warsztat dostaje najwyzej koszt cyklu (wsad po cenie miasta + place) + 25%
   (ta sama marza `SmithProfitPercent`, z ktorej juz liczymy cene broni); nadwyzka zostaje w kasie miasta. Piekarnia w Lannisporcie
   373 tys. -> ok. 15-25 tys., zlotnik 3.56 mln -> kilka tys., mediana 145 tys. -> ok. 15-30 tys.
3. **RUDA - WAZNE, nie regresja.** 51 miast bez rudy zamiast 25-30, ale kuznie zuzywaja 74 ladunki/dobe zamiast 32 (x2.3), a cykle kuzni
   odpuszczone z braku rudy spadly z ok. 1450 do ok. 900 dziennie (-38%). "Miast bez rudy" to migawka polek: dowieziona ruda jest
   zjadana. Symulacje byly optymistyczne (stale zuzycie 0.6/dobe w kazdym miescie, sam ladunek rudy, bez czekania, bez zatykania kopaln).
4. **WELNA - WAZNE, zastane (K13).** Tkalnie zuzywaja 1 welne na cykl (43/dobe) przy produkcji ok. 230 - polki 566 -> 2600. Receptura
   1 welna (10 kg) -> 2 filce (20 kg) jest fizycznie niemozliwa; to krok K13 (wsad wedle wartosci + produkcja wsi), po P1 i P2.
5. **Drobne:** kapital startowy warsztatow zostal w starej monecie (kolejnosc zdarzen, blad 116 wyciszony w 120); nadplata wozow 27.6%
   i 116 wozow/dobe to nie bledy; sakiewka zoldu rosnie, bo ludzie wydaja tylko w miastach; drewno "bez wyjasnienia" = znana dosypka RBL.

---

## 1. RUDA: dlaczego 51, a nie 25-30

### 1.1 Liczby (log 07.10, w nawiasie test 06.10)

| Pomiar | Doba 1 | Doba 19 | Zrodlo |
|---|---|---|---|
| Miast bez rudy | 76 | 51 (test 06.10: 74 -> 69) | L183, L1597 (S:L175, S:L1568) |
| Wydobycie wsi ("wsie dopisaly") | 188 w 26 wsiach | 130 w 18 wsiach (model 194 w 26) | L183, L1597 |
| Ruda w magazynach wsi | 395 | 1015 | L183, L1597 |
| Zapas miast / zamkow / taborow | 216 / 198 / 23 | 998 / 213 / 305 | L183, L1597 |
| Wywoz z wsi (produkcja - przyrost magazynow), doby 2-19 | | **136.6/dobe** (06.10: 160.2) | seria "Ruda:" |
| Zuzycie (warsztaty zbrojne + narzedzia), srednio | | **74.1/dobe** (06.10: 32.3) | seria "Ruda:" |
| Cykle kuzni odpuszczone "brak surowca [ruda]", doby 15-19 | | 756 / 1132 / 880 / 828 / 887 (06.10, doby 15-20: 1227..1482) | L1584, S:L1556 |
| Wozy z ruda / w tym do miasta bez rudy (19 dob) | | 112 / 63 | "Dowoz (wozy)", L1603 |
| Ladunek rudy na kurs (wywoz 2458 / 111 kursow, doby 2-19) | | ok. 22 ladunki | rachunek |
| Karawany: ruda kupiona / sprzedana miastom z brakiem (19 dob) | | 88 / 30 (mediana 4 i 1 na dobe) | L1604 i seria |
| Oczekiwany zysk karawany na kg | | ruda 0.076, len 2.43, skora 4.27, plotno 14.98 d/kg | L1604 |

### 1.2 Przyczyny (policzone)

(a) **Ruda dociera do kuzni i jest zjadana - miasta pustoszeja z powrotem.** Wywieziona ruda trafia tam, gdzie sa kuznie: zuzycie x2.3
wobec 06.10, cykle "brak rudy" -38%. 63 kursy do pustych miast daly netto -25 miast (0.40 miasta na kurs); w symulacji sym3 ten sam
ruch dawal ok. 0.92 (69 -> 21 przy 49-55 kursach). Przyczyna roznicy: `SCRATCH\dzien-4\karawany3\recenzja2\sym\sym2.py:89-92` - zuzycie
STALE 0.6 (+0.3 z kuznia) ladunku na dobe w KAZDYM miescie, niezaleznie od dostaw (22 ladunki starczaly w symulacji na 37 dob). W grze
zuzycie idzie za dostawa: rzemieslnicy (ukryci "artisans" w kazdym z 97 miast) i kuznie kupuja rude, gdy wyrob sie oplaca (WorkshopLaw),
wiec dowieziony ladunek znika w kilka dni. Latentny popyt kuzni (SZACUNEK): 887 odpuszczonych cykli x ok. 0.07-0.1 ladunku = 60-90
ladunkow/dobe ponad zuzyte 74 -> popyt kuzni ok. 130-160/dobe, wywoz 137 - **podazy nie starcza na zapas w dalszych miastach**.

(b) **Kopalnie sie zatykaja - wywoz spadl do 137/dobe (06.10: 160).** W dobie 19 kopie 18 z 26 wsi (L1597; srednio z 5 ostatnich dob 19.8,
06.10: 23.0). Udzwig wozu: `DefaultInventoryCapacityModel` = 10 + 20 kg x ludzie + 100 kg x zwierzeta juczne; zwierzat 0.5 x ludzie
(`VillagerPartyComponent.InitializeVillagerPartyProperties`); ludzi 12 + palenisk/38 (`DefaultPartySizeLimitModel.GetIdealVillagerPartySize`
:415-425) -> kopalnia (mediana 350 palenisk, mapa `recenzja2\sym\mapa.json`) 21 ludzi, woz x2 ok. 2.9 t (1.9-4.6 t). Magazyn kopalni przy
wyjezdzie: 37 ladunkow rudy po 100 kg (5 dob x 7.4) + ok. 28 sztuk zywnosci = ok. 4 t (opis 105: pojemnosc 65 sztuk). Kurs kopalni ok. 4.4
doby (26 wsi x 19 dob / 112 kursow) -> wies produkuje 7.4 ladunku/dobe, woz wywozi ok. 5 -> ok. +2.4/dobe w magazynie -> bramka 1.5 x
pojemnosci (ok. 97 sztuk) -> wies stoi. To efekt uboczny 119 (dlugie kursy) - mechanizm w p. 2.

(c) **Wybor miasta dziala jak w symulacji.** 112 kursow z ruda (sym3: 94), 63 do pustych miast (sym3: 49-55), "do innego miasta" 61% wozow
(dla wszystkich towarow; sym3 80% liczyla tylko 4 surowce). Bledu w `MarketCarts.Choose` nie ma - jest za malo rudy i za szybko znika.

(d) **Karawany woza rudy malo i to jest zgodne z regula Jeffa "wedle zysku na kg".** Ruda ma najmniejszy zysk na kg z surowcow (0.076 d/kg wobec
plotna 14.98, L1604) - kupowana jest ostatnia, w resztce jukow (zakup odpuszczony "brak miejsca" 3 z 10 wyjazdow z nadwyzka, L1606). Wyjazdow
z miast z nadwyzka rudy tylko ok. 10/dobe, bo nadwyzka jest w 14 miastach (L1599) - 119 rozwozi rude wozami, wiec nie piętrzy sie juz w 28
miastach przy kopalniach jak 06.10 (1740 ladunkow, S:L1568). Oczekiwanie 117 (35-55 dziennie) bylo liczone ze stanu 06.10 - kolizja 117 x 119
przewidziana jakosciowo w opisie 119 ("karawany wioza mniej - zamierzone"). Historycznie zgodne: rudy zelaza nie wozono daleko ladem -
wytapiano ja przy kopalni i lesie, w handlu szlo zelazo (SZACUNEK z pamieci: Weald, Forest of Dean, osmund szwedzki, zelazo hiszpanskie w Anglii XIV w.).

(e) **Dlawik BKROTPatch** (`SCRATCH\ore-supply\bkrot\BKROTPatch.Patches\BKCaravansHourlyThrottlePatch.cs:13, :72-73`): prefiks na
`BKCaravansBehavior.HourlyTickParty` przepuszcza decyzje BK karawany AI (zakup + wybor celu) RAZ NA 24 h, w godzinie wyznaczonej haszem id.
Karawana, ktora wjedzie do miasta, czeka na swoja godzine do 23 h (srednio ok. 12 h); w miastach stoi 274 z 755 karawan (L1607). Ogranicza
to przepustowosc karawan o kilkanascie procent, ale glowna przyczyna (d) jest zysk na kg - karawana wyjezdza srednio co ok. 8.5 doby
(89 wyjazdow/dobe na 755, L1604).

(f) Start kampanii: 76 -> 81 w dobach 1-3 (zjedzony zapas startowy, L183-L337). 16 miast lezy dalej niz 250 od kazdej kopalni (opis 119) -
tam ruda moze dojechac tylko karawana, a przy zysku 0.076 d/kg nie dojedzie.

### 1.3 Czy symulacja byla optymistyczna - tak

sym3 (opis 119): 20-23 miast; recenzent z ladunkiem mieszanym 25-30 (opis 119, "WRAZLIWOSC"). Brakowalo w niej: (1) zuzycia idacego za
dostawa (najwiekszy blad, p. 1.2a), (2) zatykania kopaln przez udzwig i zywnosc w magazynie (sym3 `sym3.py:153-176`: magazyn i woz to sama
ruda), (3) czekania ok. 0.5 doby na kurs (w miescie 20%/h = 5 h, w domu 15%/h = 6.7 h; sym3: 0.2 doby), (4) wyjazdu tylko z domu przy
zapasie >= pojemnosc. Oczekiwanie 117 (35-55 rudy dziennie w karawanach) - ze stanu 06.10, ktory 119 zmienil.

### 1.4 Regresja?

Nie. Ruda dociera lepiej niz 06.10 (kuznie x2.3, braki kuzni -38%). Czesciowy efekt uboczny 119: zatkane kopalnie (-15% wywozu).

### 1.5 Poprawka

- **P1 (p. 2.5)** podnosi wywoz z kopaln z 137 do ok. 190/dobe (26 z 26 kopie, ladunek ok. 37 zamiast 22, kursow z ruda ok. 4.9/dobe =
  26 / 5.3 doby). Dopiero wtedy podaz (ok. 190) przekracza popyt kuzni (ok. 130-160) i ruda zaczyna zostawac w dalszych miastach.
  SZACUNEK: 51 -> ok. 40-45 po 19 dobach nowej kampanii, ok. 30-35 po 40 dobach.
- **P4 (narzedzie)**: glowna liczba rudy = "cykle kuzni bez rudy" (L1584) + "wywoz rudy z wsi na dobe" + "kopalnie kopiace X z 26";
  "miast bez rudy" tylko pomocniczo (jest migawka polek i rosnie, gdy kuznie lepiej pracuja).
- **P5c (projekt, K13)**: wytop przy kopalni - wies gornicza oddaje zelazo (lupke), a kuznie miast kupuja zelazo zamiast rudy. Zelazo
  ok. 1-2 d/kg (SZACUNEK; ruda 0.08 d/kg) - zysk karawany na kg x10-20, wiec karawany dowioza je do 16 miast poza zasiegiem wozow.
  Zmienia to, co Jeff widzi na targu (zelazo zamiast rudy) - osobna paczka z opisem dla Jeffa.

---

## 2. ZATKANE MAGAZYNY WSI: 16.5% zamkowych, 14.8% miejskich

### 2.1 Liczby

Seria "Dowoz (skutki)" (zamkowe/miejskie, L188 ... L1602): 1/0, 0/0, 0/0, 2/3, 4/8, 10/15, 18/28, 19/31, 16/40, 22/54, 18/48, 32/50,
29/44, 26/39, 36/45, 35/46, 38/56, 39/47, **43/46** = 89 z 571 (15.6%). Test 06.10 (S:L179 ... S:L1572): 1/0 ... **12/23** (6.1%).
Opis 119 przewidywal +1-3 punkty (tylko kopalnie i drwale).
Kto stoi w dobie 19: kopalnie 8 z 26 (L1597: kopie 18), drwale 5 z 45 (L1598: 40 z 45), reszta ok. 76 - zboze, ryby, owce, len... -
**zjawisko ogolne, nie tylko ciezkie towary**. Woz x2 dla wsi miejskich (czesc 2 paczki 119) nie pomogl: wsie miejskie 7.4% -> 14.8%.
Wozy (L1603 i seria): 95-131 na dobe (mediana 116), srednio 94-139 jedn. drogi w jedna strone (mediana 113), pelny woz 29-52/dobe (ok. 34%),
w drodze ok. 218 (=> ok. 1.9 doby w jedna strone: 218 / 116).

### 2.2 Mechanizm (kod gry + rachunek)

- Gra (`VillagerCampaignBehavior.cs:131-171`): woz rusza TYLKO z domu, gdy zapas >= pojemnosc W, w kazdej godzinie z szansa 15% (srednio
  6.7 h); jeden woz na wies (gdy woz jest w drodze, `:138-140` konczy). Zaladunek `:205-236`: 4 x 20% kazdego stosu = 59% (do udzwigu).
  Z miasta wraca z szansa 20%/h (srednio 5 h, `:366-387`).
- Pojemnosc W = 5 dob produkcji (`Village.cs:248-257`); przy zapasie >= 1.5 x W gra wstrzymuje TickGoodProduction I TickFoodProduction
  (`VillageGoodProductionCampaignBehavior.cs:138-155`). Pomiar Armoury ta sama bramka (`MarketRoad.cs:160`).
- STAN USTALONY przy zwyklym zaladunku: zapas przy wyjezdzie S = 0.41 S + P x C, P = W/5, C = kurs tam i z powrotem + czekanie ->
  **S/W = C / 2.95**; bramka 1.5 W przy **C >= 4.42 doby**.
- W tescie: 2 x 109 / 61.7 = 3.53 doby drogi + 0.49 czekania = 4.0; z pomiaru w drodze 1.9 doby w jedna strone -> C ok. 4.3. Stad
  S ok. 1.37-1.46 W - **cala populacja wozow jedzie na granicy bramki**, kazdy kurs dluzszy od sredniej (> ok. 123 jedn.) zatyka wies.
  Stan ustalony osiaga sie po kilku kursach (po 4-5 dobach kazdy), stad wzrost prawie liniowy, ok. 5 wsi/dobe w dobach 6-19 (25 -> 89).
- Miernik 119 (`MarketCarts.cs:391, :410-418`): T = 2d / v BEZ czekania; zwykly ladunek, gdy T <= tfree (`:411`), tfree = 5 x (1.5 -
  zapas/W) liczone po zaladunku. W stanie ustalonym tfree = 7.5 - 0.695 C -> zwykly ladunek wybierany az do T = 4.22 doby, a wies
  zatyka sie juz od T = 3.92 (C = 4.42): **okno kursow 121-130 jedn.**, w ktorym miernik nie widzi kosztu; do tego kursy przyjete mimo
  kary (Rate = tf/T, `:418`), gdy miasto placi wiecej. Recenzent 119 sam zapisal przyblizenie (opis 119 "PRZYBLIZENIE": W wedle listy gry).
- Ciezkie towary (ruda, drewno - ladunek 100 kg): magazyn ok. 4 t przy wozie ok. 2.9 t - nawet "pelny woz" (`TopUpPlan` `:254-274`, limit
  udzwigu `:265`) zostawia ponad 1 t; nastepny wyjazd dopiero przy zapasie >= W - zapas rosnie z kursu na kurs.

### 2.3 Skutek dla produkcji

- 89 wsi (15.6%) nie produkuje NICZEGO, w tym zywnosci; bez poprawki po 40 dobach SZACUNEK 25-30%.
- Ruda -64 ladunki/dobe wobec modelu (194 -> 130, -33%); drewno -33 (270 -> 237, L1598).
- Zywnosc: zatkana wies nie dopisuje zboza ani ryb na targ. Spichlerze zamkow licza sie z przypisania wsi, nie z jej magazynu - bilans
  ujemny 1 z 130 jak 06.10 (L1602), glodu jeszcze nie widac; widac mniej zywnosci na polkach.
- Utarg wsi: wozy wybraly 7.74 mln d zamiast 5.18 mln we wlasnych miastach (pomiar narzedzia z L1603) - ale z 15% wsi nic juz nie wyjezdza.

### 2.4 Regresja

**119, czesc 1** (dlugie kursy; miernik bez czekania; okno zwyklego ladunku; udzwig kopaln i drwali). Czesc 2 (woz x2) za slaba, czesc 3
(cena sztuka po sztuce) bez zwiazku.

### 2.5 Poprawka P1 - WOZ ZABIERA CALY MAGAZYN

Regula (jedna, 100% wsi, wszystkie towary): **kiedy woz wyrusza z wlasnej wsi, zabiera caly magazyn.** Uzasadnienie: "wies" w ROT to symbol
wielu wsi (decyzja Jeffa 05.10), jej woz to wszystkie wozy okregu - okreg najmuje tylu wozakow, ilu trzeba; chlop wiozl na targ cala
nadwyzke (najem przewoznikow byl powszechny - SZACUNEK z pamieci). Zasada gry "59% stosu" (4 x 20%) nie ma uzasadnienia ekonomicznego.
Gdzie:
- (a) `MarketCarts.Choose` (`:393-399`, `:411`): plan doladunku zawsze przy wyjezdzie z domu (`useFull = full != null`), nie tylko gdy T > tfree;
- (b) `MarketCarts.TopUpPlan` (`:254-274`): bez limitu udzwigu (`:265`) - caly magazyn (zwierzeta jak dotad);
- (c) `MarketRoad.CartPostfix` (`:97-111`): udzwig taboru wsi = max(dotychczasowy x `MarketCartFactor`, waga ladunku) - bez kary
  przeciazenia, predkosc jak dzis (konwoj kilku wozow jedzie tym samym tempem);
- (d) miernik: T = 2d / v + 0.49 doby (stale gry: 5 h w miescie przy 20%/h, 6.7 h w domu przy 15%/h);
- wlacznik MCM `VillageCartWholeStore` (true); linia "Dowoz (wozy)" + "caly magazyn N wozow, srednio X kg".
Skutek (SZACUNEK z mechanizmu): po wyjezdzie magazyn = 0, nastepny wyjazd po 5 dobach + ok. 7 h, szczyt zapasu ok. 1.06 W; wies zatyka sie
tylko przy kursie z czekaniem > 7.5 doby (> ok. 215 jedn., gdy miernik wybierze go mimo kary). **Zatkane 89 -> ok. 5-15 (1-3%)**; wraca
produkcja ok. 75-80 wsi (ok. 13-14% produkcji wsi, z zywnoscia); kopalnie 26 z 26 (+ok. 60 ladunkow/dobe), **wywoz rudy ok. 190/dobe (+39%)**,
drewno ok. +33/dobe; wozow ok. 108/dobe (-7%, cykl 5.3 doby zamiast 4.9), ladunek na kurs +60-70%; lup z rozbitego taboru wiekszy (dzis
5 rozbitych/dobe, L1601). Towar sie nie mnozy (TopUp = magazyn -> juki, `:292-305`); zloto bez nowej sciezki.
Kontrola przed wgraniem: kolejka `paczki-na-120/110` (K5: `MarketRoad.TooFar`, `CastlePurse.CanPayCart`) i `/112` (K7) - konflikty
tekstowe w `MarketRoad.cs` / `Settings.cs`; "Ruda:" i "Drewno:" - towar przechodzi z "wsie" do "tabory".

---

## 3. WARSZTATY LUKSUSOWE I PIEKARNIE: ile kosztuje warsztat

### 3.1 Receptury i wartosci po HistoricalPrices

Wartosci: `HistoricalPrices.cs:41-52` (d za kg x waga 10 kg, ujemne = za sztuke), w logu L125: silver 100->20, jewelry 675->2400,
cotton (surowy jedwab) 80->1000, velvet 575->4000, wool 22->83, felt 230->200, grain 10->3, bread 0->6 (definicja BK 20, L123), pie 0->10.

| Warsztat | Receptura (plik) | Szybkosc | Wsad | Wyrob | Wyrob/wsad | Wynik d na warsztat (L1587) |
|---|---|---|---|---|---|---|
| zlotnik `silversmithy` | 1 silver -> 2 jewelry (`SandBox\ModuleData\spworkshops.xml:913`) | 0.75 | 20 | 4800 | **240x** | 2206 (Lannisport 3257 sr. 19 dob) |
| aksamit `velvet_weavery` | 1 cotton -> 2 velvet (`:424`) | 0.75 | 1000 | 8000 | 8x | **5107** |
| welna `wool_weavery` | 1 wool -> 2 garment (1.0); 1 wool -> 2 felt (2.0) (`:564`) | 3.0 | 83 | filc 400 | 4.8x | 1148 |
| piekarnia `bakery` (BK) | 1 grain -> 2 bread (3.0); grain + maslo/marchew/pomarancza/jablko -> 4 pie, grain + mieso -> 6 pie (5 x 0.5) (`BannerKings.Redux\ModuleData\workshops.xml:567`) | 5.5 | 3 | 12 | 4x (przy indeksie chleba 3.26: 13x) | 363.5 |
| lniarnia | 1 flax -> 2 linen (`:452`) | 2.0 | 20 | 200 | 10x | 155.7 |

Place cyklu (WorkshopTrade: 6 ludzi x 3 d / suma szybkosci linii): zlotnik 24 d (L1587: place 120 za 5 cykli), piekarnia 3.27 d (1598 za 488).

### 3.2 Kto placi i czy wyrob sie sprzedaje

- Warsztat dostaje za KAZDA sztuke cene KUPNA miasta, bez limitu: latka BK `ProduceOutputPrefix`
  (`SCRATCH\ore-supply\bk\BannerKings.Patches\EconomyPatches.cs:783`; cena `:818` z isSelling = false; kasa miasta -> warsztat `:837-838`)
  zastepuje gre, ktora placila najwyzej 1000 za sztuke (`WorkshopsCampaignBehavior.cs:851`). Wsad warsztat kupuje z polki po cenie miasta
  (`EconomyPatches.cs:868-885`).
- Kasa miasta odzyskuje to, gdy mieszczanie "zjadaja" wyrob: BK `ItemConsumptionPatch` (`EconomyPatches.cs:619` budzet, `:651`
  town.ChangeGold(+)) - **zloto z niczego**, "zakupy mieszkancow miast i zamkow" 407 tys./dobe (L1647).
- Budzet mieszczan na bizuterie (gra `DefaultSettlementEconomyModel.GetDailyDemandForCategory` :61-73; `DefaultItemCategories`
  jewelry BaseDemand 15, LuxuryDemand 32 x 0.001; przelicznik 118 "jewelry /0.3", L122): Lannisport (dobrobyt 5797)
  0.015 x 5797 + 0.032 x 2797 = 176.5 starej monety -> ok. **630 d/dobe**. Aksamit 15/32 i "/0.1" -> ok. 1230 d/dobe.
- Sprzedaje sie - skokami. Pusta polka = indeks 10 (L171: "wyrob jewelry x10.00, wsad silver x10.00"); pierwsza dostawa srebra = wyplata:
  zlotnik w Lannisporcie +41.5 tys. w dobie 4 (srednia 10 366 z 4 dob, L406), ok. 51 tys. w dobach 4-5, potem prawie nic -> srednia 19 dob
  3256.9 d -> **cena 3 x 364 x 3256.9 + 2323 = 3 558 909 d** (L1587, = zrzut Jeffa). Swiat: zlotnicy +100 887 w dobie 4 (5 cykli, ok. 20 tys.
  za cykl), aksamit +103 349 w dobie 5 (L484). Max ceny 12.27 mln = ok. 11.2 tys. d/dobe zysku (SZACUNEK: tkalnia aksamitu).
- Razem warsztaty towarowe +133 tys./dobe (L1587), z tego zlotnicy 33, aksamit 41, welna 21, piekarnie 28 tys. (95%); wyplata notablom
  114.8 tys./dobe (L1587).
- Linia dnia nie podaje polek bizuterii i aksamitu - dopisac (pomiar, P2).

### 3.3 Historyczne proporcje kosztu materialu (SZACUNEK z pamieci, 1300-1400)

- zlotnik / srebrnik: kruszec 70-90% ceny (wyroby sprzedawano wedle wagi + "fashion" 10-30%); w grze material 0.4%.
- tkacz aksamitu (Lukka, Florencja): surowy jedwab 30-50%, barwnik (kermes) i praca reszta; w grze 12.5%.
- sukno welniane (Flandria, Anglia; Munro): welna 40-65% kosztu; w grze filc 21%.
- piekarz: zboze 75-90% ceny chleba; Assize of Bread (1266) dawal piekarzowi staly dodatek na kwarte pszenicy - zysk ok. 4 d i otreby przy
  pszenicy ok. 60-70 d za kwarte, czyli kilka-kilkanascie procent; w grze 25% przy cenach bazowych, 7% przy indeksie chleba 3.75.
- dochod mistrza ok. 4-6 d dziennie; czynsz sklepu z izba w Londynie 10 s - 3 L rocznie; nieruchomosci miejskie szly za 10-20 lat czynszu.
  Zlotnik z 2206 d/dobe = ok. 3350 L rocznie (dochod hrabiego), piekarnia 363 d/dobe = ok. 550 L.

### 3.4 Diagnoza: receptury, ceny wyrobow czy wzor ceny?

- **Wzor ceny warsztatu (3 lata zysku + kapital) - dobry.** Dziala jak opisano (L1587 "3 x roczny zysk = 3 556 586"); 3 lata to malo wobec
  10-20 lat czynszu nieruchomosci (`Settings.cs:434` - uzasadnienie: warsztat zabiera wojna). Bledny jest zysk, ktory wzor mnozy.
- **Receptury gry**: wsad prawie nic nie kosztuje (240x, 8x, 10x). Gra liczyla je w starej monecie (nawet tam srebrnik 13.5x) i trzymala w
  ryzach limitem 1000 za sztuke - latka BK go zdjela.
- **Ceny wyrobow - renta z niedoboru**: 1-2 warsztaty danego typu w miescie (staly zbior), popyt mieszczan w zlocie (z niczego) przeliczony
  przez 118 (bizuteria x3.6, aksamit x7, chleb /3.3), przy pustej polce indeks do 10. Dla piekarni receptura nie jest glowna przyczyna:
  przy cenach bazowych linia chleba daje 12 - 3 - 3.27 = 5.7 d na cykl, przy indeksie 3.75 - ok. 38 d. Sama zmiana receptur NIE naprawi piekarni.

### 3.5 JEDNA regula - P2 "CENA SPRAWIEDLIWA WARSZTATU"

**Za wyrob cyklu warsztat dostaje najwyzej koszt cyklu (wsad po cenie miasta + place cyklu) powiekszony o marze mistrza
`SmithProfitPercent` (25%) - te sama, z ktorej juz liczymy cene broni (`ArmsPricing.cs:229`, L121 "zysk 25%"). Nadwyzke ceny (renta
z niedoboru) zatrzymuje kasa miasta.**
- Historycznie: assize chleba i piwa, ceny cechowe, iustum pretium = koszt + umiarkowany zysk. Ekonomicznie: przy swobodnym wejsciu
  konkurencja sprowadza cene wyrobu do kosztu; w grze liczba warsztatow jest stala, wiec bez reguly niedobor daje rente bez konca.
  Jedna zasada na jedno zjawisko: rzemioslo zbrojne i towarowe licza zarobek tak samo. 100% linii towarowych, bez listy.
- Gdzie: `WorkshopTrade.cs` - prefiks na `TickOneProductionCycleForNotableWorkshop` / `...ForPlayerWorkshop` (te same cele co
  `CyclePostfix` `:338`, wpiecie `:1068-1069`): migawka kapitalu warsztatu i kosztu wsadu (cena miasta x ilosc - jak
  `DetermineItemRosterHasSufficientInputs`, ktora placi latka BK `:868-885`); w `CyclePostfix` (cykl udany, effectCapital): utarg =
  przyrost kapitalu + wsad; dozwolone = (wsad + place) x 1.25; nadwyzka > 0 -> `workshop.ChangeGold(-n)`, `town.ChangeGold(+n)` - te same
  dwie kasy, ktore poruszyla latka BK (zamknieta ekonomia), wpis do ksiegi pieniadza. Prog cyklu (`Gate200`, `:229`) bez zmian - cykl rusza,
  gdy cena rynkowa pokrywa wsad i place; gdy rynek placi mniej niz koszt + 25%, warsztat dostaje cene rynkowa (bez zmiany). Wlacznik
  `WorkshopTradeFairPrice` (true). Linia "Warsztaty towarowe:" + "nadwyzka ceny w kasach miast X d" + polki bizuterii i aksamitu.
- Skutek (ceny doby 19, SZACUNEK):

| Warsztat | Zysk dzis d/dobe | Cena dzis | Zysk po regule | Cena po regule |
|---|---|---|---|---|
| piekarnia Lannisport | 338.7 | 372 715 | ok. 13-20 | ok. 15-25 tys. |
| zlotnik Lannisport | 3256.9 | 3 558 909 | ok. 5 | ok. 5-9 tys. (sprzet + kapital) |
| tkalnia aksamitu (srednio) | 5107 | ok. 5.6 mln | ok. 40-130 | ok. 45-150 tys. (zalezy od ceny jedwabiu) |
| tkalnia welny (srednio) | 1148 | ok. 1.25 mln | ok. 30 | ok. 35 tys. |
| mediana 291 warsztatow | ok. 130 | 144 641 | ok. 10-25 | ok. 15-30 tys. |

  Wynik warsztatow swiata +133 tys. -> ok. +10-20 tys./dobe; kasy miast ok. +115 tys./dobe (placa z tego renty panom - dzis 361 tys.
  z naleznych 6.03 mln, L1594; nadmiar ponad cel zdejmie regulator - mniej zlota z niczego zostaje w obiegu).
- RYZYKO: notable traca juz dzis 8-153 tys./dobe (seria "Pieniadz swiata", L1646: 30.26 mln, -13.6 tys.) MIMO 115 tys. z warsztatow; po
  regule spadek ok. 2x szybszy (SZACUNEK: 30 mln starczy na ok. 160 dob). Przed testem dluzszym niz 20 dob ustalic w ksiedze, kto zdejmuje
  notablom ok. 130-180 tys./dobe (GiveGoldAction w nicosc 846 tys./dobe, L1647) - sprawa grupy KASY. Zlotnik po regule tani (5-9 tys.),
  bo receptura (bizuteria z 20 d rudy) zostaje absurdalna - to naprawia K13 (P5a).
- Regresja: renta zlotnika i aksamitu ZASTANA (wartosci HistoricalPrices + receptury gry + latka BK bez limitu), ujawniona przez 116
  (cena z zarobku); renta piekarni NOWA z 116 (piekarnie ruszyly - prog w nowej monecie) + 118 (popyt chleba w nowej monecie).

---

## 4. WELNA ZALEGA ("KUPNO STOI" 8 dob)

- Na polkach miast 566 -> 2600 (L1592), w jukach karawan 1242 (L1605). "KUPNO STOI" (L1605-L1606): z 22 wyjazdow z nadwyzka welny
  21 odpuszczonych "w drodze dosc" - karawany wioza 1242 przy brakach 175 (L1605): zakaz kupna jest POPRAWNY, wiecej welny nie ma gdzie sprzedac.
- Zuzycie: 18 tkalni, 43 cykle/dobe x 1 welna = 43/dobe (L1587) + mieszczanie ok. 5.3 d/miasto/dobe = ok. 6 sztuk (L1599) wobec produkcji
  ok. 230/dobe (opis 115) - nadwyzka ok. 5x. Tak - **tkalnie zuzywaja za malo**: 1 welna (10 kg) -> 2 filce (20 kg) - fizycznie niemozliwe;
  wedle wagi i strat prania (15-40%) 2.5-3.5 welny, wedle udzialu materialu (welna 40-65% kosztu sukna) 400 x 0.6 / 83 = 2.9 -> 3 welny.
  Po zmianie receptur ok. 130/dobe; reszta (ok. 100/dobe) to produkcja wsi owczarskich wobec zuzycia - krok skali K13 (audyt 07.10, OTWARTE (1)).
  Historycznie Anglia wywozila ok. 2/3 welny za morze (SZACUNEK) - w zamknietym swiecie gry tego ujscia nie ma.
- Regresja: zastane (K13); 115 obnizylo cene welny poza miastami z tkalnia (indeks mediana 0.52, L1599); 117/119 tylko przeniosly welne.
- Poprawka: P5a (K13), nie samodzielnie - ta sama regula "wsad wedle wartosci" zjadlaby rude w linii narzedzi (0.6 x 160 / 8 = 12 ladunkow
  na 4 narzedzia = 300 ladunkow/dobe zamiast 25), bo wsadem kowala jest RUDA, a nie zelazo - rozwiazuje to dopiero wytop przy kopalni (P5c).

---

## 5. DROBNE

5.1 **Kapital startowy warsztatow w starej monecie** (UWAGA narzedzia: brak linii "WorkshopTrade: nowa kampania - kapital startowy").
`WorkshopTradeBehavior.OnSessionLaunched` (`WorkshopTrade.cs:1104-1109`) zostal wywolany PRZED `ArmouryBehavior.OnSessionLaunched`
(`ArmouryBehavior.cs:964`, HistoricalPrices.Apply): L105 (03:20:12) przed L117-L125 (03:20:13) - kolejnosc sluchaczy zdarzenia nie jest
kolejnoscia `AddBehavior` (`SubModuleMain.cs:167, :177`), wbrew komentarzowi w `:1107`. `SeedNewCampaign` (`:466-470`) wychodzi przez
`!HistoricalPrices.Applied` (dodane w 120) i milczy; przed 120 liczylby w starych cenach - nie dzialal nigdy. Skutek maly: 8 tkalni aksamitu
zaczyna z 2000 zamiast 10 000 (opis 116: ok. 14 warsztatow, +112 tys. daru startowego). Poprawka P3: wolanie z `ArmouryBehavior.OnSessionLaunched`
zaraz po `RawPrice.SeedNewCampaign` (`:966`). Regresja: 116 (zalozenie o kolejnosci), wyciszone przez 120.
5.2 **Nadplata wozow 27.6%** (L1603: 2.47 mln d w 19 dobach oddane kasom osad): wiecej niz "kilka-kilkanascie %", bo 119 kieruje wozy do pustych
miast, gdzie cena spada stromo - korekta dziala ("niezgodne" 0, "cena rosla" 0). Nie blad; prog narzedzia podniesc na ok. 35%.
5.3 **116 wozow/dobe** (oczekiwane 150-250): ograniczone regula gry - jeden woz, wyjazd tylko z domu przy pelnym magazynie; 571 / 116 = 4.9 doby
na wies. Nie blad; po P1 ok. 108/dobe.
5.4 **Najwieksza sakiewka zoldu rosnie 7 dob** (L1641: lord_WE9_l_party_1 20.6 -> 36.8 tys.): ludzie wydaja tylko w miastach (`MenPurse.cs:24`),
partia w polu zbiera; sakiewki razem 0.93 -> 1.59 mln (+80 tys./dobe: wplynelo 282.7, wyszlo 201.4 tys., L1583). Nie blad - do obserwacji;
gdyby rosly bez konca - markietanie (wydatek takze w obozie) w grupie PIENIADZ.
5.5 **Drewno "bez wyjasnienia" +3462 w dobie 2** (L259): ten sam skok w tescie 06.10 (+2989 w dobie 2) - dosypka RealisticBannerlord do pustych
miast na starcie (50 -> 20 miast bez drewna w dobie 2), potem 2482, 1785 ... mediana 1445. Zastane (STAN-PRAC OTWARTE (2)) - zamknac w fundamencie B1.
5.6 (poza lista) futro na suficie indeksu (pusto w 84 miastach, L1599) i "Paser: zloto z niczego zablokowane 0" (L1643) - tylko obserwacja.

---

## 6. PLAN (jedna zmiana = jedna paczka; najpierw to, co psuje gre najbardziej)

1. **P1 = 121 "pelny woz" (PILNE)** - woz zabiera caly magazyn (p. 2.5). Najpierw, bo zatkane wsie nie produkuja niczego, takze zywnosci,
   liczba rosnie codziennie (ok. 5 wsi/dobe), a to regresja 119; przy okazji +39% rudy z kopaln.
2. **P2 = 122 "cena sprawiedliwa warsztatu" (PILNE)** - p. 3.5. Drugie, bo to to, o co pyta Jeff (absurdalne ceny), i 115 tys. zlota z niczego
   dziennie do notabli; przed wgraniem: pomiar odplywu notabli (ksiega, bez zmiany gry) - ryzyko z p. 3.5.
3. **P3 = 123 "kapital startowy po przeliczeniu cen" (DROBNE)** - jedna linia wolania (p. 5.1); po P2, bo ten sam plik i ta sama nowa kampania.
4. **P4 - narzedzie `sprawdz_logi` (bez DLL)**: glowna liczba rudy = cykle kuzni bez rudy + wywoz z wsi + kopalnie kopiace; progi: nadplata
   wozow 35%, zatkane magazyny (cel po P1 <= 3%), cena warsztatow (cel po P2: mediana 10-40 tys.).
   TEST: P1 + P2 + P3 razem jako grupa (nowa kampania, 20 dob) - rozne pliki i rozne liczby w logu, kazda z wlacznikiem.
5. **Kolejka 108-114 (`paczki-na-120/*`)** - przeniesc na P3 (konflikty: `MarketRoad.cs` / `Settings.cs` przy 110 i 112).
6. **P5 = krok K13 (WAZNE, projekt - osobna analiza przed kodem)**: (a) wsad cyklu wedle wartosci (material 60% wartosci wyrobu w cenach
   bazowych: filc 3 welny, len 6, aksamit 5 jedwabiu, chleb 2 zboza, bizuteria 144 rudy srebra), (b) produkcja wsi owczarskich i lnu wobec
   zuzycia, (c) wytop przy kopalni - zelazo zamiast rudy w handlu (dopiero to otwiera 16 miast poza zasiegiem wozow i usuwa kolizje z linia
   narzedzi). Kolejnosc wewnatrz: (c) przed (a).
7. **P6 (DROBNE, zastane)** - zamkniecie dosypki drewna RealisticBannerlord (fundament B1).
8. **P7 (opcja)** - dlawik BKROTPatch: decyzja karawany takze przy wjezdzie do miasta (nie tylko w jej godzinie) - cudzy kod, koszt czasu;
   dopiero gdy po P1 i P5c karawany beda mialy co wozic.

## 7. Pliki i linie (dowody)

- Log 07.10: L28, L105 (WorkshopTrade start), L117-L125 (HistoricalPrices), L171 / L406 / L484 / L1587 (warsztaty), L183 / L1597 (ruda),
  L188 / L1602 (zatkane), L259 / L1598 (drewno), L1583 / L1641 (sakiewki), L1584 (kuznie), L1592 (rynek surowcow), L1594 (renty),
  L1599 (ceny), L1601 / L1603 (wozy), L1604-L1607 (karawany), L1646-L1650 (pieniadz).
- Log 06.10: S:L175 / S:L1568 (ruda), S:L179 / S:L1572 (zatkane), S:L1556 (kuznie).
- Kod Armoury: `MarketCarts.cs:254-305, :369-453`; `MarketRoad.cs:97-111, :127-201`; `WorkshopTrade.cs:225-256, :306-356, :466-500, :1065-1109`;
  `ArmouryBehavior.cs:960-966`; `HistoricalPrices.cs:41-52, :392-421`; `ArmsPricing.cs:229`; `Settings.cs:366-367, :409, :423, :434`.
- Gra: `VillagerCampaignBehavior.cs:131-236, :262-322, :366-387`; `VillageGoodProductionCampaignBehavior.cs:138-200`; `Village.cs:248-257`;
  `WorkshopsCampaignBehavior.cs:750-805, :844-885, :1387-1420`; `DefaultSettlementEconomyModel.cs:61-84`; `DefaultInventoryCapacityModel`;
  `TaleWorlds.Core.DefaultItemCategories` (ilspycmd, linie 349-412).
- BK / BKROT: `EconomyPatches.cs:578-705, :783-885`; `BKCaravansHourlyThrottlePatch.cs:13, :40-73`; `BannerKings.Redux\ModuleData\workshops.xml:567-623`;
  `SandBox\ModuleData\spworkshops.xml:396-939`; `SandBoxCore\ModuleData\items\horses_and_others.xml:3552-3680`.
- Symulacje: `dzien-4\karawany3\recenzja2\sym\sym2.py:62-92`, `dzien-5\wozy\sym\sym3.py:31-176`.
