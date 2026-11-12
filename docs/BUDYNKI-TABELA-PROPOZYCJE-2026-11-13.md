# Budynki - co daja dzis i co proponuje (13.11)

Zrodlo: audyt 12.11 (`docs/AUDYT-BUDYNKI-2026-11-12.md`), kod gry 1.4.8, BannerKings, nasze mody, log 60 dob.
Ceny w zlocie przy dzisiejszym koszcie punktu budowy (cywilny ok. 31 zl, wojskowy ok. 63 zl); czas przy typowym tempie (miasto ok. 7 pkt/d, zamek ok. 3 pkt/d, rok = 364 doby).
Ocena: SENS / SLABY / BEZ SENSU / MARTWY. Propozycja: ZOSTAW / ZMIEN (efekt albo koszt) / WYLACZ (AI nie buduje, zbudowane zostaja).

## Miasto

| Budynek | Koszt i czas na poziom | Co daje dzis naprawde | Ocena | Propozycja |
|---|---|---|---|---|
| Fortifications (mury) | robi zespol 472 | obrona, poziom murow | SENS | zakres 472 (2-3 lata / 4-5 lat) |
| Barracks (koszary) | 113/189/265 tys., 0.7-1.6 roku | nizszy zold zalogi | SLABY | ZOSTAW, ale razem z biurem kasztelana zniżka max 40% |
| Training Fields | 95/132/170 tys. | kilka XP na dobe na zolnierza | SLABY | ZMIEN koszt na poziom zamkowy (ok. 1/3) |
| Guard House | 95/132/170 tys. | patrole, mniej band w okolicy | SENS | ZOSTAW |
| Siege Workshop | 37/56/93 tys. (cywilny) | machiny w obronie | SENS, zla klasa | ZMIEN na budowle wojskowa (nie staje w wojnie) |
| Tax Office | 56/93/130 tys. | doplata z nadwyzki kasy miasta do rat budow | SENS | ZOSTAW |
| Marketplace (targ) | 74/112/149 tys. | licznik cel, realnie prawie nic | SLABY | ZMIEN: prowizja od prawdziwego obrotu targu do kasy miasta |
| Warehouse (magazyn) | 56/74/93 tys. | wiekszy zapas jedzenia + BK produkcja | SENS | ZOSTAW |
| Mason (murarz) | 74/93/149 tys. | darmowe punkty budowy (ok. 38% mocy swiata, z niczego) | SENS, za mocny | ZMIEN: punkt budowy tanszy o 10/20/30% zamiast darmowych punktow |
| Waterworks (wodociagi) | 56/112/167 tys. | mniejsze zuzycie jedzenia | SENS | ZOSTAW |
| Courthouse (sad, gra) | 74/112/167 tys. | +0.2 do +1 wplywu na dobe | SLABY | ZMIEN: scalic z BK Court House w jeden sad, L1 silniejszy |
| Roads and Paths (drogi) | 74/112/149 tys. | +2.8 szt./d produkcji kazdej wsi okregu | SENS | ZOSTAW |
| Harbor (port BK) | 78/140/186 tys. | ryby prawie 0, troche dobrobytu | SLABY | ZMIEN: rybacy z ludnosci miasta, ryby na targ; koszt nizszy |
| Mines (kopalnie BK) | 47/78/124 tys. | "+0.5 budowy" nie dziala; ruda ok. 50-90 zl/d na wszystkie miasta razem | BEZ SENSU | WYLACZ teraz; pozniej: gornicy z ludnosci tylko tam, gdzie jest zloze |
| Theater (teatr BK) | 62/93/124 tys. | lojalnosc tylko w osadzie obcej kultury | SLABY | ZMIEN: AI buduje tylko w osadzie obcej kultury; pozniej przerobic na swiatynie/sept |
| Armory (zbrojownia BK) | 47/62/78 tys. | +1 XP na kazdym poziomie (Training Fields daje +3 za 1/3 ceny) | BEZ SENSU | WYLACZ |
| Court House (sad BK) | 62/81/99 tys. | +0.02 stabilnosci na poziom | BEZ SENSU | WYLACZ (scalony z sadem gry) |
| Shipyard (stocznia) | 149/186 tys. | statki (NavalDLC) | SENS | ZOSTAW |
| Projekty dzienne | 0 | zwerbunek, asymilacja, festyny | SENS | ZOSTAW |

## Zamek

| Budynek | Koszt i czas na poziom | Co daje dzis naprawde | Ocena | Propozycja |
|---|---|---|---|---|
| Fortifications (mury) | robi zespol 472 | obrona | SENS | zakres 472 (2 lata / 4 lata) |
| Barracks (koszary) | 26/44/71 tys., ok. 0.4-1 roku | nizszy zold, zwrot <1.5 roku | SENS | ZOSTAW (sufit zniżki 40% z biurem) |
| Castellan's Office | 35/53/79 tys. | nizszy zold + autowerbunek | SENS | ZOSTAW |
| Training Fields | 26/35/44 tys. | XP zalogi (tylko z bronia w zbrojowni) | SENS | ZOSTAW |
| Guard House | 22/31/39 tys. | milicja broni zamku, miejsca w lochu | SLABY | ZOSTAW (tani) |
| Siege Workshop | 18/26/44 tys. | machiny w obronie | SENS | ZOSTAW |
| Granary (spichlerz) | 13/17/22 tys. | +1000/2000/3000 zapasu - dluzsza obrona | SENS | ZOSTAW (wazny przy 471) |
| Farmlands (pola) | 13/20/26 tys. | +6/12/18 jedzenia + BK +50%/poziom (z niczego) | SENS | pytanie 3 do Jeffa |
| Craftmans Quarters | 11/15/20 tys. | **0** (martwy) | MARTWY | WYLACZ teraz; pozniej: rzemieslnicy przerabiaja surowiec wsi na towar |
| Mason (murarz) | 18/22/35 tys. | darmowe punkty (jak w miescie) | SLABY | ZMIEN jak w miescie (tanszy punkt) |
| Roads and Paths | 18/26/35 tys. | +2.8 szt./d na wies | SENS | ZOSTAW |
| Castle Mines (BK) | 47/78/125 tys., razem ok. 7 lat | ok. **4 zl/d** na zamek | BEZ SENSU | WYLACZ teraz; pozniej gornicy wg zloza |
| Mines (miejskie BK w zamku) | jw. | **0** (urobek liczy sie tylko w miastach) | MARTWY | WYLACZ |
| Retinue Barracks (BK) | 31/62/93 tys. (cywilny!) | elita orszaku, ale w wojnie stoi i blokuje kolejke | SENS, zla klasa | ZMIEN: budowla wojskowa, koszt w punktach x0.5 |
| Warhorse Studs (BK) | 31/56/75 tys. | ok. 1 kon bojowy na zamek na rok, lezy bez odbiorcy | BEZ SENSU | WYLACZ teraz; pozniej: zrebieta z pastwisk wg poziomu, konie na targ/do zalogi |
| Port (BK w zamku) | 47/93/140 tys., 8 lat | handel martwy, zaden zamek go nie ma | MARTWY | WYLACZ |
| Harbor (BK w zamku) | 78/141/188 tys., 12 lat | jak wyzej | BEZ SENSU | WYLACZ |
| Theater (BK w zamku) | 63/94/125 tys., 8 lat | lojalnosc w obcej kulturze | SLABY | jak w miescie |
| Armory (BK w zamku) | 47/63/78 tys. | +1 XP | BEZ SENSU | WYLACZ (zjada 11% punktow zamkow) |
| Court House (BK w zamku) | 63/81/100 tys., 7 lat | +0.02 stabilnosci | BEZ SENSU | WYLACZ (zjada 15% punktow zamkow) |
| Projekty dzienne | 0 | zold, werbunek, cwiczenia | SENS, ale prawie nigdy czynne | ZOSTAW (po wylaczeniu martwych beda czesciej czynne) |

## Wies (BannerKings, budowane praca wsi - dzis za 0 zlota i 0 materialu)

| Budynek | Czas | Co daje dzis naprawde | Ocena | Propozycja |
|---|---|---|---|---|
| Farming / Mining Infra / Animal Housing / Sawmill / Fish Farm | 1-2 lata na poziom | ok. +2 szt./d produkcji wsi | SENS | ZOSTAW |
| Mines (wies nie-gornicza) | 1.4-2.7 roku na poziom | ruda zamiast zboza o tej samej wartosci - netto **0** | BEZ SENSU | WYLACZ |
| Mill (mlyn) | 1.2-2.4 roku | zboze -> chleb, malo | SLABY | ZOSTAW tylko we wsiach zbozowych |
| Bakery (piekarnia) | 0.7-1.7 roku | jak mlyn | SLABY | ZOSTAW tylko we wsiach zbozowych |
| Fur Tannery (garbarnia) | 1-2 lata | **0** skory (wsie bez surowca) | BEZ SENSU | WYLACZ (albo tylko wsie ze skorami) |
| Bee Skeps (ule) | 0.7-1.7 roku | ok. 1 szt. miodu na dobe | SLABY | ZOSTAW (tanie) |
| Smith (kuznia wiejska) | 0.7-1.7 roku | narzedzia - niepotwierdzone, brak licznika | SENS | ZOSTAW + licznik w logu (narzedzi brakuje w lancuchu) |
| Marketplace (targ wiejski) | 0.4-1.4 roku | wies dokupuje nadwyzki z miasta | SLABY | ZOSTAW |
| Tax Office (wies) | 1-2.4 roku | **0** (sami wylaczylismy ten podatek) | MARTWY | WYLACZ |
| Courier Post | 0.7-1.7 roku | **0** | MARTWY | WYLACZ |
| Manor (dwor) | 2.7-5.5 roku, razem ok. 12 lat | +0.1-0.3 wplywu na dobe, maly orszak | SLABY | ZMIEN koszt x0.4 |
| Palisade (palisada) | 2-4.8 roku | wolniejszy rabunek wsi | SLABY | ZMIEN koszt x0.5 (wazne przy furazu 471) |
| Training Grounds | 1-2 lata | milicja wsi | SLABY | ZMIEN koszt x0.5 |
| Projekty akrow (Farmland, Pasture, Woodland) | 0 | **szkodza**: nikt nie czyta akrow, a wylaczaja +15% produkcji | SZKODLIWE | WYLACZ |

## Nowe (brakuje w grze)

| Budynek | Propozycja |
|---|---|
| Sept / swiatynia / godswood | budynek wiary wg kultury: waga wiary pana w osadzie, mniej napiecia religijnego; placony z dziesieciny (dzis septy to sam obieg zlota bez efektu) - pytanie 4 do Jeffa |

## Razem do wylaczenia teraz (paczka 1)
Mines (miasto, zamek, wies), Castle Mines, Craftmans Quarters, Warhorse Studs, Port i Harbor w zamku, Armory BK, Court House BK, Tax Office wsi, Courier Post, Fur Tannery (poza wsiami ze skorami), projekty akrow wsi. Zbudowane zostaja w zapisie; AI przestaje je wybierac. Kopalnie, stadnina, rzemieslnicy i port wracaja pozniej z prawdziwymi efektami (paczka 6).
