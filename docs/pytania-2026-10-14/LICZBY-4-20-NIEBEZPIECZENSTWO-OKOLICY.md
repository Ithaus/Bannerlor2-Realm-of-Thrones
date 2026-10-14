# Liczby 4.20: niebezpieczenstwo okolicy z pustych osad zamiast "spladrowanej" wsi gry (bieg dwuletni 13/14.10)

Oznaczenia: [P] pomiar z logu / tabeli / kodu, [S] szacunek z wzoru. Doba N = "dzien 108835 + N" Armoury. Rok = 364 doby.
Bieg: `Armoury-2026-10-13_21-14-13.log` (fin21 = 43af413, nowa kampania, 728 dob, 0 ERROR) + tabele `Logs\2026-10-13_21-14-13\ludzie-regiony.csv`
(227 regionow x 728 dob) i `wioski-okregi.csv` (okregi wsi z pustymi osadami); porownanie: bieg 120 dob `Armoury-2026-10-13_20-41-38.log`. Wszystko z kopii na
`D:\Backup-Bannerlord\logs\2026-10-14-noc\`. Kod: drzewo `zb\int` (w-toku/zbior-e8 449d453). Nic nie zmieniano, gry nie uruchamiano.
Zadnego z kluczy ponizej nie ma w `Armoury.json` Jeffa - dzialaja wartosci domyslne z `Settings.cs` [P].

## 0. Dla Jeffa (prosto)

1. **Co to jest dzis "niebezpieczenstwo okolicy".** U nas to trzy rzeczy razem: slaby porzadek w miescie / zamku, wojna krolestwa i **ile wsi regionu gra pokazuje jako "spladrowane"**.
   Dziala na: przyrost ludzi we wsiach, ilu uciekajacych przed najazdem idzie do lasu (bandy), ilu biedakow codziennie idzie w las (bandy) i checi chlopow do zaciagu.
   Na ceny i ruch (handel, karawany, wedrowke wojsk) ta miara nie dziala wcale - to robi gra sama ze swojego stanu wsi i plon, ktory juz liczymy z ludzi [P].
2. **"Spladrowana" z gry to sygnal krotki i mocny:** srednio 14 z 569 wsi naraz w ok. 10 regionach (najwyzej 35 wsi w 20 regionach), zwykle 2 dni (srednio 9) [P].
   Wtedy cala wies liczy sie jak spalona - w regionie z 2-3 wsiami to od razu 33-50% "spalonej okolicy".
3. **Puste osady to sygnal slaby i dlugi:** przez 2 lata poza domem bylo najwyzej **0.39% ludzi wsi** (161 tys. z 41 mln, doba 487), pustych osad najwyzej **713** z ok. 166 tys. [P/S].
   Jeden najazd zabiera okregowi zwykle ok. 2% ludzi. Region z co najmniej 10% pustych - najwyzej **3 naraz**, ponad 25% - **jeden** (Duskendale, do 30%) [P].
   Za to ludzie wracaja do roku: okresy "5% i wiecej pustych" trwaly srednio 190 dni [P].
4. **Gdyby zamienic jedno na drugie:** niebezpieczenstwo **spadloby w 106 regionach i wzroslo tylko w 4** (Duskendale, Rosby, Omber, Sweetport) [P].
   Band byloby **ok. 1% mniej, nie wiecej** (ok. 2.5 czlowieka dziennie mniej do lasu z ok. 215; ok. 1 800 ludzi przez 2 lata) [S]. Napady, ceny, ruch - bez zmian [P].
   Ochotnicy w swiezo spladrowanej okolicy: wyraznie mniej (dzis spalona wies pcha chlopow do wojska) [S]. Przyrost ludzi: +400 ludzi rocznie na 41 mln - nic [S].
5. **Wersja "dluzszy ogon"** (okolica niebezpieczna, dopoki spalona ALBO pusta - co wieksze): bandy +0.04% (ok. 60 ludzi w 2 lata) - w grze niewidoczne [S].

**Rada: NIE** - zostawic "spladrowana" z gry. Zamiana nic by nie dala, a tam, gdzie cos zmienia, dziala odwrotnie, niz chciales ("wojna = wiecej bandytow").
"Dluzszy ogon" jest zgodny z Twoja zasada, ale w liczbach to zero - szkoda 2-4 h kodu i testu.

**Pytanie (jedno slowo):** Zamykamy 4.20 bez zmian w grze - TAK / NIE?

## 1. Jak dziala dzis (kod)

| Gdzie | Regula | Skad "spalone" |
|---|---|---|
| `PopulationLaw.DangerOf` (PopulationLaw.cs:532-550) | niebezpieczenstwo U = 0.4 x (1 - bezpieczenstwo/100) + 0.3 x wojna (bez wiecznych wojen z Wolnymi Ludzmi i Innymi) + 0.3 x udzial wsi regionu Looted / BeingRaided, 0..1 | stan wsi gry |
| przyrost wsi (PopulationLaw.cs:600-607) i miast (:574-577) | g [%/rok] = 0.5 + 0.6 x dobrobyt - **0.6 x U** - glod; korytarz -1.3..+0.8 | przez U |
| `Devastation` (Devastation.cs:302) | z ludzi trafionych najazdem / zerowaniem idzie w las **(2 + 3 x U)%** | przez U |
| `OutlawLaw.Daily` (OutlawLaw.cs:1028-1047) | codziennie w las: hearth / 1000 x 0.1 x nedza; nedza = (1 - dobrobyt/5000) + 0.5 x (1 - bezp.) + 0.5 x wojna (Z wiecznymi) + **2 x udzial spalonych** + 1.5 x glod | stan wsi gry (wlasna petla) |
| `OutlawLaw.MiseryOf` (OutlawLaw.cs:978-1004) -> `Levy.Willingness` (Levy.cs:109) | chec do zaciagu = 0.05 + nadwyzka rak + **0.2 x nedza** (najwyzej 1.5) | stan wsi gry (trzecia kopia tej petli) |

Gra (bez nas, dekompilacja `dec32/cs_full`): `DefaultSettlementSecurityModel` -2 bezpieczenstwa dziennie, gdy choc jedna wies Looted; `VillagerCampaignBehavior:125`
tabory wsi (handel wies -> miasto) tylko ze wsi Normal; `DefaultVillageProductionCalculatorModel:20` plon tylko Normal (u nas `LootedProducesByPeople`: plon wedlug ludzi w domu);
`DefaultSettlementMilitiaModel:97` milicja wsi nie rosnie; `DefaultClanFinanceModel:472` bez podatku ze wsi; `AiMilitaryBehavior:426` napastnicy omijaja wsie juz Looted.
Czas stanu Looted ustawia nasz `DistrictRaid`: ceil(1 + 100 x trafieni/ludzie okregu) dob, najwyzej 17 (`RaidAlertMaxDays`), potem leczenie gry [P].
BEE "niebezpieczna droga" karawan (`ApplyDangerPressure`) zamknieta (`RouteDangerMaxLossRatio` = 0, BeeSeal) [P]. W naszym kodzie zadna cena ani ruch nie czyta U ani nedzy [P].

Dzis w biegu: czlon niebezpieczenstwa w przyroscie wsi srednio **-0.157 pkt %/rok**, prawie caly z wojny (wsi w wojnie srednio 421 z 569; wsi w regionach ze spalonymi
srednio 10), bezpieczenstwo srednio 95.5 [P, linie "Ludzie: przyrost naturalny", np. L139898 d364: -0.18, 503 w wojnie, 16 przy spalonych].

## 2. Ile osad i ludzi stoi pustych (bieg dwuletni)

Udzial "pusto" regionu = uchodzcy / (ludzie wsi w domu + uchodzcy) z ksiegi ludzi (kolumna `spustoszenie_proc`). Na poziomie okregu to ta sama liczba co udzial pustych
osad ksiegi wiosek: puste osady / n = ludzie poza domem x **1.06** (mediana; p10 1.04, p90 1.17; 105 tys. okrego-dni) [P, wioski-okregi.csv]. Glod nie oproznil zadnej osady (glod 0) [P].

| doby | wsie gry spalone / lupione (z 569) | regionow z taka wsia | uchodzcy (tys.) | udzial pustych | regionow >= 5% | >= 10% | >= 25% | pustych osad (okregow z nimi) | wiosek spalonych |
|---|---|---|---|---|---|---|---|---|---|
| 1-28 | 1.5 | 1.2 | 0.6 | 0.00% | 0 | 0 | 0 | 3 (3) | 0 |
| 85-112 | 14.5 | 11.7 | 26.7 | 0.06% | 0 | 0 | 0 | 116 (78) | 0 |
| 197-224 | 21.5 | 15.3 | 73.0 | 0.18% | 2.0 | 0 | 0 | 322 (132) | 3.6 |
| 337-364 | 22.0 | 15.6 | 118.4 | 0.29% | 5.9 | 0.1 | 0 | 526 (176) | 11.2 |
| 421-448 | 26.9 | 17.5 | 151.0 | 0.37% | 8.0 | 3.0 | 1.0 | 670 (190) | 20.7 |
| 477-504 | 13.6 | 7.5 | 159.4 | 0.39% | 5.0 | 3.0 | 1.0 | 706 (197) | 23.7 |
| 533-560 | 4.7 | 3.9 | 147.1 | 0.36% | 5.2 | 2.8 | 0 | 650 (188) | 21.0 |
| 645-672 | 3.9 | 3.2 | 109.9 | 0.27% | 4.1 | 2.0 | 0 | 484 (163) | 9.3 |
| 701-728 | 3.4 | 3.1 | 92.4 | 0.22% | 4.2 | 1.0 | 0 | 405 (160) | 6.0 |
| **1-728** | **13.9** | **9.8** | **96.8** | **0.23%** | **3.7** | **1.1** | **0.2** | **428 (145)** | **9.7** |

[P: ludzie-regiony.csv; linie "Ksiega wiosek (E4/11): dzien", szczyt L186647 d487: 712.6 osad w 198 okregach, 24 wioski spalone, 185 czesciowo pustych; L139589 d364: 543.5 w 180.]
W regionach z co najmniej 5% pustych mieszka srednio 0.21 mln ludzi wsi (najwiecej 0.42 mln, d361) z 41.3 mln [P]. Najwiekszy okreg: 32.8% pustych osad, nigdy 50% [P].
Bieg 120 dob: uchodzcow najwyzej 29 tys. (0.07%), zaden region nie dobil 5% [P].

Ile trwa jedno i drugie (epizody region-dni) [P]: "spladrowana" z gry 760 razy, srednio 9.4 doby, mediana **2**, najdluzej 127 (najazdy jeden po drugim);
"5% i wiecej pustych" 14 razy, srednio **190** dob (mediana 119, najdluzej 498); "1% i wiecej" 50 razy, srednio 342.

Regiony (227): kiedykolwiek >= 5% pustych 11, >= 10% 4, >= 25% 1; kiedykolwiek ze spalona wsia gry 106 [P]. Region-dni: spalona wies gry 7 107 (4.3%), pustych >= 5% 2 661,
>= 10% 836, >= 25% 117; "ukryte" (>= 5% pustych, zadnej spalonej wsi gry) 1 912; spalona wies gry przy < 5% pustych 6 358 [P].
W chwili najazdu (3 175 region-dni z najazdem tego dnia): pustych srednio 2.1%, spalonych wsi gry 48.9% [P].

Gdzie najbardziej pusto (srednio przez 2 lata; wlasciciel z konca biegu) [P]:

| region | krolestwo / kraina | pusto srednio | najwiecej | spalona wies gry srednio | dni >= 10% | dni spalonej |
|---|---|---|---|---|---|---|
| Duskendale | Baratheon z Krolewskiej Przystani / crownlands | 10.6% | 30.5% | 13.5% | 366 | 127 |
| Rosby | j.w. / crownlands | 7.1% | 20.0% | 12.4% | 299 | 109 |
| Omber | Joth / ibbenese | 6.6% | 12.2% | 21.6% | 140 | 261 |
| Kingsgrave | Dorne / aserai | 4.5% | 8.5% | 22.9% | 0 | 273 |
| Sweetport | Dragonstone | 4.5% | 10.1% | 2.1% | 31 | 29 |
| Willow Wood | Baratheon KP / river | 3.5% | 9.8% | 19.3% | 0 | 211 |
| Mardosh | Sarnor | 3.2% | 5.4% | 4.6% | 0 | 60 |
| Vaes Diaf | Dothrakowie / khuzait | 3.0% | 6.2% | 16.9% | 0 | 248 |
| Sandstone | Reach / aserai | 2.9% | 5.5% | 19.8% | 0 | 239 |
| Ulentor, Bolozo, Stoney Sept | Norvos, Pentos, Dorzecze | 1.9-2.6% | do 5.6% | 20-27% | 0 | 240-297 |

Krainy (wazone ludzmi): crownlands 3.2% (spalone gry 5.0%), ibbenese 2.7% (8.8%), norvos 1.1% (11.8%), nord 1.05%, dragonstone 0.9%, sarnor 0.75%, aserai 0.7%,
khuzait 0.6%, river 0.6% (7.5%); reszta ponizej 0.5% [P].

## 3. Co by sie zmienilo (warianty, bieg dwuletni)

Rachunek na tych samych dobach: zmiana U = 0.3 x (nowa miara - udzial spalonych wsi gry); zmiana nedzy = 2 x to samo. Odtworzony z tabeli naplyw "bieda/wojna" = 16.5/d,
log mowi 16.6/d - wzor sie zgadza [P: linie "Wyrzutki: dzien", srednia 728 dob]. W las przy najezdzie: trafionych 277 080 w 2 lata (380.6/d), "rabunki" 12.7/d = 3.3% [P].

| wariant | regionow z czlonem > 0 (sr.) | region-dni U wyzej o >= 0.03 / nizej | w las z biedy | w las przy najazdach | razem przez 2 lata | przyrost wsi | chec do zaciagu w spladrowanej okolicy |
|---|---|---|---|---|---|---|---|
| dzis (gra) | 9.8 | - | 16.6/d (z tego czlon spalonych 0.95/d) | 12.7/d | - | - | +0.4 x udzial spalonych (ok. +0.2) |
| A: puste osady (= ludzie poza domem) | 75 (>= 10%: 1.1) | 615 w 4 regionach / 7 091 w 106 | -0.85/d | -1.60/d | **-1 790 ludzi** | +396 ludzi/rok | -0.23 |
| B: spalone / opuszczone wioski ksiegi (z wsia gry) | 9.6 (>= 10%: 4.1) | 1 517 w 10 / 6 932 w 102 | -0.81/d | -1.35/d | -1 572 | +384/rok | -0.21 |
| C: wieksze z (gra, A) | 75 (>= 10%: 10.6) | 615 / 0 | +0.07/d | +0.01/d | **+62** | -127/rok | 0 |
| D: wieksze z (gra, B) | 14.5 (>= 10%: 11.8) | 1 517 / 0 | +0.06/d | +0.01/d | +50 | -98/rok | 0 |

[P: przeliczenie tabel; skrypty `eff.py`, `reg.py`, `vb.py`, `sum.py` w `C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\c36b73ca-1de0-4b1b-80ce-71bd02afe545\scratchpad\a420\` (tymczasowe).]
Bieg 120 dob: A -1.83/d, C +0.01/d (+1 czlowiek) [P].

Bandy [S]: naplyw do puli wyrzutkow (bez rozwiazanych band) srednio 214.5/d (dezercja 61.6, rozbitkowie 123.6, rabunki 12.7, bieda 16.6) [P]; bandy srednio 852 (16.8 tys. ludzi),
pod koniec 1 097 (21.5 tys.) [P; np. L186424 d487: pula 8 035, bieda/wojna 13.2, rabunki 34]. A: -2.46/d = -1.15% naplywu -> ok. **-190 ludzi, ok. 10 band mniej**; C: +0.04% -> ok. +7 ludzi. Napady na wsie: zadna regula gry ani nasza
nie czyta U - zmieniaja sie tylko przez liczbe band (+-1%) [S]. Ceny i ruch: bez zmian (sekcja 1) [P].
Gdzie A podnosi U: Duskendale 269 dni, Rosby 227, Omber 89, Sweetport 30. Gdzie najbardziej obniza: Bolozo 297, Stoney Sept 278, Kingsgrave 273, Omber 261, Vaes Diaf 248 [P].
Wielkosc: najwiekszy wzrost U w A = 0.3 x 0.305 = 0.09 (Duskendale) -> przyrost -0.05 pkt %/rok, las przy najezdzie +0.3 pkt % [S].

## 4. Ryzyka i uwagi

- **Kierunek odwrotny do zamierzonego (A, B):** najazd zabiera ok. 2% okregu (DevastationPerRaiderDay 0.5), wiec miara z ludzi jest 20-25 razy slabsza od stanu gry w chwili najazdu;
  zamiana = prawie wylaczenie czlonu "spalone" -> mniej wyrzutkow i ochotnikow w wojnie. Sprzeczne z Jeffa "jak jest wojna, jest duzo bandytow" (11.10).
- **Ochotnicy (A, B):** jesli zamiana obejmie tez nedze (jedna regula w trzech miejscach - powinna), chec w spladrowanej okolicy spada o ok. 0.2 przy sredniej x0.28-0.36 [P: "Pobor:" L139544, L186611].
- **Gra zostaje przy swoim stanie:** -2 bezpieczenstwa, brak taborow, podatku, milicji we wsi Looted - zmiana 4.20 tego nie rusza; "jedna zasada" i tak nie bylaby pelna bez latek modeli gry.
- **Zaleznosc:** miara z ludzi istnieje tylko przy Devastation Enabled + People Unit; bez nich = 0 (trzeba by wracac do stanu gry).
- **Koszt:** U liczone na kazda wies co dobe - uchodzcy regionu pod blokada RefGate; potrzebna pamiec dobowa (jak mediany dobrobytu) - male.
- Przy okazji (bez zmian, tylko do wiadomosci): nedza wyrzutkow liczy wojne Z wiecznymi wojnami (Wolni Ludzie / Inni), U - bez nich; "spalone" policzone w 3 osobnych petlach.

## 5. Gdyby jednak TAK (plan S, 2-4 h)

Wariant C (wieksze z: spalona wies gry, puste osady) - jedyny, ktory niczego nie oslabia. Jedna funkcja `RegionBurnt(Settlement)` z pamiecia dobowa w trzech miejscach
(DangerOf, OutlawLaw.Daily, MiseryOf); bez Devastation - stan gry. Klucz MCM `DangerFromEmptySettlements` (false = 449d453). Linia dobowa: regionow z U wyzej, srednia zmiana U,
dodatkowy naplyw w las. Prog `sprawdz_logi.py --grupa jeff14`: srednia zmiana U >= 0 i dodatkowy naplyw w las < 1% naplywu puli. Test: 120 dob nic nie pokaze (0 regionow >= 5%) - tylko bieg roczny.
