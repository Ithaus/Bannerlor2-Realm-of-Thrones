# PROJEKT: BUDZET RODU I KORONY (A3, A4, A5, A15) - 14.10.2026

KAT: zmiany regul WYDATKOW rodu i korony (pulapy zoldu, zalogi, najemnicy w pokoju, wezwania sojusznikow, doplaty korony) tak, by rod i korona nie wydawaly ponad dochod, a wojsko trzymalo prog; oszczednosci ludu tylko tam, gdzie juz sa kanaly. Bez kodu, bez commitow, gra nieuruchamiana.
Znaczniki: **[P]** pomiar z logu / tabel albo odczyt kodu, **[S]** szacunek albo wniosek. Baza (klucze "off") = `w-toku/zbior-e8` **449d453** (= W53 w grze: W52 + szybkie wlaczenia + drobne bledy).

**Zrodla.** Rozpoznania `ROZPOZNANIE-RODY.md` i `ROZPOZNANIE-LUD.md` (ten folder); bieg dwuletni `Armoury-2026-10-13_21-14-13.log` (+ `Logs\2026-10-13_21-14-13\kiesa-obieg.csv`, `budzet-rodow.csv`); Z.1 na zapisie d720 + 60 dob `Armoury-2026-10-14_02-30-30.log`; W52 120 dob `Armoury-2026-10-14_02-50-28.log` (wszystkie w `D:\Backup-Bannerlord\logs\2026-10-14-noc\`); kod drzewa `zb\pr2` (`ClanBudget.cs`, `KingdomTreasury.NeedAid.cs`, `KingdomTreasury.WarAid.cs`, `CrownIncome.cs`, `CrownCallToWar.cs`, `MercContract.cs`, `Settings.cs`); `BIEG-DWULETNI` rozdz. 2.2-2.6 i 7; raport roczny rozdz. 6 (nr 3, 5, 10, 15, 20, 21, 33, 36); rachunek 85 tys. rozdz. 7.6; STAN-PRAC 13-14.10 (decyzje Jeffa). Rachunki projektu: skrypty w rozdz. 9.
Doba = dzien gry - 108836 + 1. Kwartaly Q1-Q8 po 91 dob (Q5-Q8 = rok 2). Kwoty: tys. zl na dobe, chyba ze napisano inaczej.

---

## 0. Dla Jeffa - prosto

1. Pieniadz to mniejsza czesc klopotu roku 2: wojsko spadlo ze 109 do 55 tys., z czego ok. 44 tys. to krolestwa, ktore zawarly pokoj (mapa wojen), a tylko ok. 8-11 tys. to pieniadz [P, BD 2.2]. Zadna regula budzetu nie odda 30 tys. ludzi - to robi Z.1 (w grze) i zmeczenie wojna /4.33 (w grze od 14.10).
2. Proponuje piec zmian regul wydatkow. Wszystkie przesuwaja pieniadze, ktore juz sa, zadna nie tworzy nowych, zadna nie zadluza korony.
3. **Korona w wojnie placi najpierw zold, potem renty** (B1): dzis do doplaty idzie tylko danina, a reszta wplywow dnia idzie rentami do panow wedlug lenn - nawet gdy ich wlasni ludzie stoja bez zoldu. To dokladnie Twoja zasada z 13.10 ("pieniadze wojenne najpierw dopelniaja zold, dopiero reszta wedlug ziem"), tylko domknieta na calych wplywach.
4. **Sojusznik dostaje rate dopiero po wlasnych zolnierzach** (B2) i **tylko za dni, w ktorych naprawde walczy** (B3): Zelazne Wyspy zaplacily Dorzeczu 1.39 mln w roku 2, gdy ich panowie nie mieli na zold; Skagos wzial 151 tys. zl bez dnia wojny.
5. **Zalogi w pokoju 70% zamiast 50% zalogi wojennej** (B4): razem z Z.1 zalogi swiata dochodza do ok. 70 tys. (prog 69 tys.); placi rod z wlasnego pulapu.
6. **Danina tylko od tych, co dzis zarabiaja ponad potrzebe** (B5): miasto, ktorego lud zarabia mniej, niz wydaje (Highgarden, Oldtown, Braavos), nie oddaje koronie oszczednosci - to jego zapas na zime.
7. Skutek w liczbach (szacunek z biegu dwuletniego): brak na zold, ktorego nikt nie pokrywa, w roku 2 z 54 do ok. 42 tys. zl dziennie; wojsko w polu +0.5..1.3 tys.; zalogi +6 tys.; korona Wysp przestaje placic obcym, gdy jej panowie glodza druzyny.
8. Czego budzet nie zrobi: Zelazne Wyspy i Wolni Ludzie zostaja biedne (razem ok. 35 tys. zl dziennie braku) - ich lud zarabia razem 19 tys. zl dziennie, a ma 1% oszczednosci swiata. Bez przelewu MIEDZY krajami (nowe zrodlo - nie w tym KAT) one nie wystawia 60% druzyn; to wiem z liczb, nie zakladam.

---

## 1. Co budzet moze, a czego nie - liczby, na ktorych stoi projekt

| fakt | liczba | dowod |
|---|---|---|
| spadek wojska w wojnie, rok 1 -> rok 2 | 108.9 -> 55.3 tys.; z tego ok. 44 tys. rody, ktore wyszly z wojny; rody w wojnie w obu oknach -4.1..-5.9 tys. | [P] BD 2.1-2.2 |
| pieniadz rodow w wojnie w roku 2 | spada glownie przez wygasanie zapasu startowego koron 1/360: 64.5 -> 12.0 tys. zl/d swiata (w roku 3 ok. 2.4) | [P] RODY rozdz. 1, 3 pkt 10 |
| brak W5 roku 2 (niepokryte po W5/W5b/W6) | 53.9 tys. zl/d; Wyspy 23.9 + Wolni Ludzie 20.7 = 83% | [P] RODY rozdz. 1 |
| krolestwa z brakiem - przeplyw korony, rok 2 (srednia dobowa) | wplywy dnia 50.5, danina 21.9, zwrot 70% 15.9, **doplata W5 11.7, renty 8.9, wezwania 5.9**, kontrakty 2.9 | [P] `dw.pkl` (linie W5 i 165 na krolestwo), skrypt `b0` rozdz. 9 |
| koszt czlowieka (wojna) | druzyna 5.7 zl/d (312.1 tys. zl / 54.9 tys. ludzi), zaloga 2.49 zl/d (126.0 / 50.6), zaloga w pokoju 3.13 | [P] Z.1 d752-781 (`a4_z1.out.txt`), STAN-PRAC 09:46 |
| sprawnosc przesuniecia | renta wchodzi do D (pulap 0.60 D, przy G < R jeszcze x0.8-1.0); doplata W5 jest znaczona na druzyny 1:1 -> przesuniecie renta -> W5 daje netto ok. +0.4 zl zoldu na 1 zl | [P] kod `ClanBudget.Ceiling`, `NeedAid` "ZNACZONA NA DRUZYNY"; [S] rachunek |
| rody z doplata nie werbuja wiecej | W5-4: wypelnienie 43.4% -> 40.5%; "rody z doplata nie werbuja wiecej" (A16) | [P] BD 7 nr 31 - podniesienie celu W5 (60%) nic nie da |
| zalogi z Z.1 | 58.7 -> 64.3 tys. w 60 dob; w pokoju 6.8 -> 14.6 tys. (= ok. 50% zalogi wojennej tych rodow, 29.4 tys. w d337-364) | [P] RODY 5.1, log Z.1 d781 "w zalogach 14600"; [S] ze cel 50% wiaze |
| pulap pokoju z Z.1 | 194.2 tys. zl/d przy zoldzie 138.2 tys. (luz 56 tys.); skrzynia 153.6 tys. u 110 rodow | [P] log Z.1 d781 linia "Budzet rodow (166)" |
| kiesy rodow w pokoju z Z.1 | 28.3 -> 26.2 mln w 60 dob (ok. -35 tys. zl/d) | [P] `z1.pkl`, linia "Skarbce" |

Wniosek [S]: budzet moze (a) przestac wydawac na rzeczy, ktore nie daja zolnierza (rata dla sojusznika, ktory nie walczy; renta przy glodnych druzynach), (b) nie zabierac zapasu tym, ktorzy traca, (c) trzymac zalogi w pokoju z wlasnego pulapu. Nie moze zrobic pieniedzy dla Wysp i Wolnych Ludzi bez nowego przelewu miedzy krolestwami.

---

## 2. Reguly - zestawienie

| # | regula (klucz MCM, off = 449d453) | inwentarz | kto placi -> kto dostaje | skutek rok 2 [S] | ryzyko rok 1 |
|---|---|---|---|---|---|
| B1 | **Garnek W5 z calej reszty wplywow dnia** (`CrownWarAidFromDayIncome`, bool, on) | A3 | skarbiec -> glowy rodow z potrzeba (zamiast: skarbiec -> glowy rodow wedlug lenn) | do W5 +7.0 tys. zl/d; brak 53.9 -> ok. 46.9; zold netto +2.8 tys. zl/d (ok. +0.5 tys. ludzi) | Q2-Q4 przesuwa 4.8 / 8.4 / 12.0 tys. zl/d; w 120 dob 0-1 tys. |
| B2 | **Wezwanie po wlasnym zoldzie, bez zerwan** (`CallToWarAfterOwnWages`, bool, on) | A15, A3 | skarbiec wzywajacego -> portfel wezwanego tylko z reszty po rezerwie (wczorajszy zwrot + wczorajsza niepokryta potrzeba); reszta raty skreslona, porozumienie trwa | wstrzymane 1.78 mln (4.9 tys. zl/d): Wyspy 1.39, Smocza Skala 0.37; z B1 idzie do W5; brak -> ok. 42; ok. +0.9 tys. ludzi u wzywajacych, do -0.3 u wezwanych | rok 1 wstrzymane 0.16 mln; 6 zerwan z pokojem sojusznika -> 0 |
| B3 | **Rata tylko za doby wojny wezwanego z celem** (`CallToWarPaidOnlyAtWar`, bool, on) | A15 | jw.; doba bez wojny = bez raty | 0 w roku 2 w tym biegu; zamyka "Skagos" | rok 1: 151 tys. zl (Straz -> Skagos d13-70) zostaje w skarbcu Strazy |
| B4 | **Zalogi w pokoju 70% zalogi wojennej** (`GarrisonPeaceShare` 0.50 -> **0.70**; istniejacy klucz) | A15 | glowa rodu -> zold zalogi (w pulapie rodu, najwyzej 80% pulapu - bez zmian) | zalogi w pokoju 14.6 -> ok. 20.4 tys.; swiat (stan Z.1 d781) 64.3 -> ok. 70 tys. (prog 69); koszt ok. 18 tys. zl/d z luzu pulapu pokoju (56 tys.) | start kampanii (pokoj d1-23): zalogi ciete do 70%, nie do 50% - wiecej zalog w d120 |
| B5 | **Danina tylko od zarabiajacych ponad potrzebe** (`FolkWarAidFromProfitOnly`, bool, on) | A4 (A5) | nic nie plynie: oszczednosci zostaja u ludu miasta, ktorego zarobek 28 dob < potrzeba 28 dob | zostaje u ludu 1.0-4.6 tys. zl/d (0.5-2.3% daniny miast; 1.2 mln w roku 2): Qarth 0.19, Highgarden 0.17, Pentos 0.16, Arbor 0.09 mln; Highgarden d728 ok. 177 tys. zamiast 9.8 tys. zapasu (ok. +65 dob) | 0.5-5.2 tys. zl/d (0.9-1.4% daniny miast) |
| - | A15 bez zmiany zachowania: pokoj fabularny (0 reparacji), najemnicy w pokoju, "+373 tys." - tylko linie logu | A15 | - | - | - |

Lacznie [S]: niepokryte po W5/W5b/W6 w roku 2 **53.9 -> ok. 42 tys. zl/d** (Wyspy 23.9 -> ok. 16.6, Wolni Ludzie 20.7 -> ok. 18.4); wojsko w polu **+0.5..1.3 tys.**; zalogi **+5.8 tys.** (z Z.1); ani jednej zlotowki z niczego, ani jednej zlotowki dlugu wiecej (B2 wrecz zmniejsza pozyczki korony na zwrot w Banku).

---

## 3. Reguly szczegolowo

### B1. Garnek W5 z calej reszty wplywow dnia (A3)

**Problem [P].** `KingdomTreasury.NeedAid.cs`: `row.Pot = Math.Max(0, Math.Min(row.Aid, CrownIncome.LeftFor(k)))` - garnek doplaty = danina P1 tej doby, najwyzej reszta wplywow dnia po zwrocie. Co zostaje ponad danine, idzie rentami 180 wedlug lenn (`CrownRents`) - takze w krolestwie, w ktorym rody stoja ponizej 60% limitu druzyn. W roku 2 krolestwa z brakiem wyplacily renty **8.9 tys. zl/d** przy niepokrytej potrzebie 53.9; korona Wysp w Q6: renty 0.1, ale w Q8 5.9 przy braku 18.2 [P, RODY rozdz. 2].

**Regula.** W krolestwie W WOJNIE garnek W5 = cala reszta wplywow dnia po zwrocie zoldu (`LeftFor(k)`), nie tylko danina. Szczeble dalej bez zmian: danina+reszta -> W5b (skarbiec ponad zapas wojny) -> W6 (tarczowe) -> W7 (stawka pana); renty 180 dostaja to, co zostanie. Pokoj - bez zmian (W5 tylko w wojnie). Krolestwo bez braku (potrzeba <= danina) - co do zlotowki jak dzis.

**Kto placi -> kto dostaje.** Skarbiec -> kiesa glowy rodu z potrzeba (jak W5 dzis: znaczone na druzyny, poza D). Renty 180 maleja dokladnie o to (ta sama `CrownIncome.Spent`). Zloto nie powstaje i nie ginie.

**Liczby (decyzja).** Bez parametru: cala reszta. Uzasadnienie: zasada Jeffa z 13.10 mowi "najpierw zold, dopiero reszta wedlug ziem" - polowiczny udzial (np. 50% reszty) zostawialby te sama sprzecznosc w mniejszej skali. Warunek rent zostaje dzwignia: W5 dostaje tylko rod ze spelnionym warunkiem rent (`NeedAidKingdom`: `if (... !ok[i]) continue`) [P kod].

**Skutek rok 2 [S z P].** Skrypt `b1.py` (min(niepokryte, renty) na krolestwo i dobe): Q5-Q8 8.7 / 2.9 / 6.5 / 9.9 tys. zl/d, srednio **7.0 tys. zl/d** (2.55 mln w roku 2); Wyspy 1.28 mln, Wolni Ludzie 0.83 mln. Netto dla zoldu ok. +0.4 zl na 1 zl (renta w D dawala 0.6 pulapu) = +2.8 tys. zl/d = ok. **+0.5 tys. ludzi** w druzynach (5.7 zl/czlowieka). Okno testowe (Z.1 d721-781): 10.4-10.6 tys. zl/d [S z P]. W7 u Wysp i Wolnych Ludzi bez zmian (ich W7 jest na suficie 3% ludu, a brak po W7 16-18 tys. zostaje) [P RODY rozdz. 2]; tam, gdzie W7 domykal maly brak, B1 zastapi W7 - lud zatrzyma te pieniadze.

**Ryzyko rok 1.** Q2-Q4 B1 przesuwa 4.8 / 8.4 / 12.0 tys. zl/d (Wyspy, Wolni Ludzie, Lys, Tyrosh, Polnoc w Q2-Q3) - kierunek ten sam co w roku 2. W 120 dobach nowej kampanii (W52): 0.0-1.0 tys. zl/d - test "rok 1 bez pogorszenia" praktycznie nieczuly [P `b1.py` na `w52.pkl`]. Bogaty rod w krolestwie z brakiem traci czesc rent (D ponizej) - [S] do ok. -0.2 tys. ludzi u Wysp.

**Gracz.** Wasal w krolestwie z brakiem dostaje mniej rent (ta sama regula co AI); jesli sam ma potrzebe i W6b - dostaje doplate. Bez nowego komunikatu (renta to ta sama linia finansow).

**Klucz MCM.** `CrownWarAidFromDayIncome` (bool, domyslnie true; grupa W5). Opis (EN): "In war the crown's need-based wage aid is paid from the whole rest of the day's income, not only from the people's war aid; rents by fiefs get what is left. Off = only the war aid feeds the wage aid". Off = 449d453 (garnek = danina).

**Linia logu.** "Doplata wedlug potrzeby (W5)" na krolestwo: `garnek G (danina D, z reszty wplywow dnia R)`; w sumie doby: `garnek z reszty wplywow poza danina (B1) X zl w N krolestwach; renty 180 krolestw z niepokryta potrzeba Y zl`.

**Prog (grupa rok2).** R2B-1: w krolestwach z niepokryta potrzeba W5 > 0 renty 180 <= 5% ich wplywow dnia (dzis 8.9 / 50.5 = 18%) - okno testu roku 2. R2B-2 (razem z B2): niepokryte swiata d781-840 <= 30 tys. zl/d (baza Z.1 d751-781: 37.3 [P]).

**Kod (dla autora).** Jedna linia w `NeedAidKingdom` (Pot), pole `row.Rest` do linii; `CrownSurplusPlan`, `ScutagePlan`, `row.Uncov` (W5b regula 1) i `LvPlan` (W7) czytaja `row.Pot` - dostosuja sie same [P kod]. Bez zapisu.

### B2. Wezwanie sojusznika po wlasnym zoldzie, bez zerwan (A15, A3)

**Problem [P].** `CrownCallToWar.Daily`: rata wezwania placona z reszty wplywow dnia PRZED zwrotem zoldu i doplata W5 (kolejnosc 165); gdy reszty brak - porozumienie zrywane i sojusznik zawiera pokoj (`EndCallToWarAgreement` + `MakePeaceAction`). Skutki: Wyspy zaplacily Dorzeczu 1.39 mln w roku 2 przy braku 16-21 tys. zl/d po W7 (Q6: renty 0.1, zold przyciety 10.0) [P RODY 2, 5.2]; Smocza Skala 1.26 mln, Pentos 0.59 mln; zerwania z pokojem sojusznika: 6 w roku 1 (4 wojny jednodniowe, raport roczny nr 20), 2 w roku 2 [P]. Gdy wezwanie zjada wplywy przed zwrotem, brak zwrotu korona w wojnie pozycza w Banku (186) - placi sojusznikowi z kredytu [P kod `CrownIncome` naglowek].

**Regula.** Czesc dnia ceny = cena / termin (jak dzis). Korona placi ja najwyzej z `LeftFor(k) - rezerwa`, rezerwa = wczorajszy zwrot zoldu tego krolestwa + wczorajsza niepokryta potrzeba W5 tego krolestwa (po wszystkich zrodlach). Czego nie ma - nie jest placone: niedoplata dnia przepada (licznik), porozumienie TRWA (bez zerwania, bez pokoju sojusznika); reszte ceny skresla gra przy koncu porozumienia (dzisiejszy `EndPostfix`). Jedna regula dla wszystkich koron (takze bez braku - wtedy rezerwa = sam zwrot).

**Kto placi -> kto dostaje.** Skarbiec wzywajacego -> portfel wezwanego (gra dzieli na jego rody) - tylko to, co naprawde wplynelo; niezaplacona czesc nigdzie nie istnieje (wezwany nie dostal jej z gory - latka `StartCallToWarAgreement`).

**Liczby (decyzja).** Rezerwa z dnia poprzedniego, bo 168 biegnie przed zwrotem i W5 (dzisiejsze liczby jeszcze nie istnieja); 1 doba opoznienia = ten sam rytm co W5 (pulap wczoraj). Bez wspolczynnika: "najpierw wlasni" w calosci - ta sama zasada co B1. Rozwazone "nie podpisywac, gdy rata > wplyw dnia" (raport roczny nr 20) - odrzucone: porozumienie zawiera gra/Diplomacy, a blokada zmniejszylaby liczbe krolestw w wojnie (prog 85 tys. liczy tylko krolestwa w wojnie).

**Skutek rok 2 [S z P].** Skrypt `b2.py`: wstrzymane **1.78 mln** (d365-546: 1.20, d547-728: 0.58) = 4.9 tys. zl/d; Wyspy 1.39, Smocza Skala 0.37, Pentos 0.01 mln; Dorzecze i inni 0. Z B1 te pieniadze ida do W5 tego samego krolestwa (u Wysp niepokryte 24 tys. >> 3.8) - ok. **+0.9 tys. ludzi** w druzynach wzywajacych; wezwani (Dorzecze w wojnie stale) dostaja 1.78 mln mniej - ich rody sa bogatsze (G > R), wiec [S] do -0.3 tys. ludzi, jesli pulapy wiaza. Brak swiata z B1+B2: **ok. 42 tys. zl/d** (Wyspy ok. 16.6). Mniej pozyczek koron w Banku na zwrot (alarm 6 rachunku) [S].

**Ryzyko rok 1.** Wstrzymane 0.16 mln (d183-364; Pentos 0.12); 10 z 445 i 66 z 198 dni-porozumien z niedoplata [P `b2.py`]. 6 zerwan z pokojem sojusznika -> 0: sojusznik walczy do konca terminu (wiecej wojny, mniej "wojen jednodniowych") [S]. Niedoplacony sojusznik moze nie byc zadowolony - gra/Diplomacy nie czyta portfela przy decyzjach wojny [S: do sprawdzenia przez autora w dekompilacji `AllianceCampaignBehavior` / Diplomacy, zasada 0].

**Gracz.** Ta sama regula, gdy krolestwo gracza wzywa albo jest wezwane; komunikat raz na porozumienie (EN): "<A> can pay <B> only part of today's due for the war against <C> - its own soldiers' wages come first. The unpaid part is struck off; the agreement goes on." Exploit: gracz-krol nie steruje niepokryta potrzeba rodow AI dzien po dniu - [S] brak sensownego naduzycia.

**Klucz MCM.** `CallToWarAfterOwnWages` (bool, true). Off = 449d453 (rata przed zwrotem, zerwanie i pokoj sojusznika przy braku). `CallToWarBreakRestoresWallet` zostaje (dziala tylko przy off).

**Linia logu.** "Wezwania do wojny (168)": `| B2 rezerwa wlasnego zoldu (zwrot + niepokryte wczoraj): rat skroconych N, niedoplata X zl (wzywajacy: Iron Islands 7581/0, ...)`.

**Prog.** R2B-3: zerwania z braku wplywow = 0 i "pokoj sojusznika" z 168 = 0 (oba testy); rata zaplacona ponad (`LeftFor` - rezerwa) = 0 (kontrolka); R2B-2 wspolny z B1.

**Kod.** `CrownCallToWar.Daily` (galaz `avail >= due` / zerwanie); rezerwa: slownik krolestwo -> (zwrot wczoraj, `NaK.Uncov` wczoraj) zapisywany na koncu `NeedAidKingdom` i `WageRefund` (stan sesji; po wczytaniu pierwsza doba rezerwa 0 = jak dzis). Bez nowego pola zapisu.

### B3. Rata tylko za doby, w ktorych wezwany walczy z celem (A15)

**Problem [P].** Kod 168 przed rata nie sprawdza, czy wezwany jest w wojnie z celem. Straz -> Skagos przeciw Wolnym Ludziom d13-70: 2 647 zl/d x 57 dob = **150 879 zl**, Skagos 0 dob wojny (bieg dwuletni; w biegu rocznym to samo porozumienie 117 565 zl) [P RODY 5.2]. W52 (nowa kampania): Straz wzywa Skagos w d18 (3 213 zl/d, termin 182 doby) i drugi raz pozniej (3 413 zl/d) [P log W52] - czy Skagos walczyl, log Armoury nie mowi.

**Regula.** Doba, w ktorej `!called.IsAtWarWith(against)` - bez raty (nie skraca reszty, nie zrywa); gra skresla reszte przy koncu porozumienia jak dzis.

**Kto placi -> kto dostaje.** Nikt nikomu w doby bez wojny; zloto zostaje w skarbcu wzywajacego (idzie dalej zwrotem / W5 / rentami).

**Skutek.** Rok 1: +151 tys. zl dla skarbca Strazy (Straz bez zoldu - idzie w jej renty), Skagos -151 tys. Rok 2 tego biegu: 0 (drugie wezwanie Skagos d657 - Skagos walczy 73/182 dob) [P]. Wartosc glownie porzadkowa: "placi za wojne, nie za obietnice" - ta sama zasada co W5 ("doplata idzie za ludzmi, nie za obietnica").

**Ryzyko rok 1.** Wezwany wchodzi do wojny dobe pozniej - jedna rata mniej [S]. Brak innego.

**Klucz.** `CallToWarPaidOnlyAtWar` (bool, true); off = 449d453. **Linia:** w "Wezwania do wojny (168)": `| B3 wezwany bez wojny z celem: rat N, wstrzymane X zl (Skagos/Free Folk ...)`. **Prog:** R2B-4 raty zaplacone przy wezwanym bez wojny z celem = 0 (nowa kampania 120 dob - przypadek Skagos; rok 2 - INFO).

### B4. Zalogi w pokoju 70% zalogi wojennej (A15 "zalogi 58 przy 69 tys.")

**Problem [P].** Zalogi swiata d728: 58.4 tys. (prog E4/8 N4 69 020); w wojnie ok. 50 tys. (3.4 tys. na krolestwo), w pokoju 6.8 tys. Z.1 (w grze) podnosi pokojowe do 14.6 tys. w d781 = ok. 50% zalogi wojennej tych rodow (29.4 tys. w d337-364) - po Z.1 wiaze CEL `GarrisonPeaceShare` 0.50, nie pulap: pulap pokoju 194.2 tys. zl/d przy zoldzie 138.2 tys. [P log Z.1; S ze to cel]. Korona do zalog nie doplaca (zwrot i W5 tylko druzyny) [P kod `WageRefund`].

**Regula.** `GarrisonPeaceShare` 0.50 -> **0.70**: w pokoju zaloga trzymana na 70% sredniej zalogi wojennej (w zlocie); ograniczenie `GarrisonMaxShareOfBudgetPeace` 0.8 pulapu - bez zmian; w wojnie bez zmian (`GarrisonWarFull`).

**Kto placi -> kto dostaje.** Glowa rodu -> zold zalogi (w pulapie pokoju: 0.28 D + 0.4 x skrzynia wojenna Z.1); werbunek zalog (K8) placi oplate sprzedajacym jak dzis. Korona nic.

**Liczby (decyzja).** 0.70 to najmniejsza okragla wartosc, ktora z Z.1 przekracza prog: 14.6 x 0.70/0.50 = 20.4 tys. -> swiat 64.3 + 5.8 = **ok. 70.1 tys.** >= 69 [S]; 0.60 daje 67.2 (za malo); 0.80 zjadalby luz druzyn pokoju. W swiecie ASOIAF zamek w pokoju dalej ma zaloge; to tez "zachowanie z biezacego budzetu" - placi pulap rodu, nie korona. Odrzucone: doplata korony do zalog (raport roczny nr 3: pelna 143.7 tys. zl/d - za drogo; w roku 3 korony bez 1/360) i zwrot 70% takze dla zalog (wiekszy zwrot = wiecej pozyczek korony w Banku w wojnie).

**Skutek rok 2 [S].** Zalogi w pokoju +5.8 tys. ludzi za ok. 18 tys. zl/d (3.13 zl/czlowieka) z luzu pulapu pokoju (56 tys. zl/d) - druzyny pokoju (14.0 tys. w d781) w sumie bez ciecia; u rodow na pulapie pokoju druzyny ustapia zalodze [S]. Bez Z.1 (pulap 0.28 D) regula prawie nic nie daje - zalogi pokoju wiazal wtedy pulap (6.8 tys.) [S]. Kiesy rodow w pokoju: -35 tys. zl/d dzis -> ok. -53 tys. zl/d (26 mln - wystarczy na lata; skrzynia maleje razem z G - R) [S].

**Ryzyko rok 1.** Start kampanii (krolestwa w pokoju d1-23): cel 70% zamiast 50% stanu z pierwszej doby -> mniej zwolnien z zalog na starcie, wiecej zalog w d120 (prog N4 wiaze w d120) [S, kod `Garrisons`: "przed pierwsza wojna - stan z pierwszej doby"]. W Q2-Q5 w pokoju 0.2-0.9 tys. zalog - skutek pomijalny.

**Klucz.** `GarrisonPeaceShare` (istniejacy float) domyslna 0.70; off = 0.50. W `Armoury.json` Jeffa klucza NIE ma [P grep 14.10] - nowa domyslna zadziala bez poprawiania pliku.

**Linia logu.** "Budzet rodow (166)", czesc Z.1: `| zalogi pokoju (B4, udzial 0.70): cel X zl, zold zalog pokoju Y zl, zalog na celu N z M, ponad 80% pulapu K`.

**Prog.** R2B-5: zalogi swiata koniec testu roku 2 (d840) >= 69 020; zalogi rodow w pokoju >= 1.25 x baza (14.6 -> >= 18.2 tys.); ludzie w partiach rodow w pokoju >= 0.9 x baza (14.0 -> >= 12.6 tys.); nowa kampania: zalogi d120 >= 69 020 i >= baza W53.

### B5. Danina tylko od zarabiajacych ponad potrzebe (A4, styk z A5)

**Problem [P].** Danina P1 bierze z oszczednosci ponad 10 dni potrzeby (`WarAid.cs:180`), bez wzgledu na to, czy lud miasta dzis zarabia, czy traci. Highgarden: danina zdjela kiese W z 419 do 247 tys. (d588-637), potem wydatek wiekszy od zarobku zjadl reszte do 9.8 tys. (d728, stac 0.33) [P LUD 7]. W roku 2 danina z okien, w ktorych zarobek miasta < potrzeba (W+R+P): 1.0 / 4.6 / 3.9 tys. zl/d (Q6-Q8), w sumie 1.2 mln; Qarth 191, Highgarden 168, Pentos 161, Arbor 91, Lannisport 85, Storm's End 85, Braavos 62, Oldtown 37 tys. zl [P `a4.py` na `kiesa-obieg.csv`].

**Regula.** Miasto, ktorego lud przez ostatnie 28 dob (4 okna kiesy) zarobil mniej, niz wynosi jego potrzeba za ten czas, nie placi daniny P1 (nowy powod w `WaRate`, jak oblezenie i P1b). Zapas i reszta szczebli (zbytki, budowy) bez zmian. Zasada Jeffa "zachowania z biezacego zysku": danina jest z zysku, nie z zapasu tych, co traca.

**Kto placi -> kto dostaje.** Nikt: zloto zostaje w kiesie ludu tego miasta (bufor na zime). Korona tego krolestwa ma o tyle mniej daniny.

**Liczby (decyzja).** 28 dob - ten sam horyzont co D staly i W5 (pierscien 28 dob); krotsze okno (7 dob) przelaczaloby brame co tydzien z ruchem cen zimy. Prog = 1.00 x potrzeby (zarobek ponizej wydatku to dokladnie "traci"); bez strefy szarej.

**Skutek rok 2 [S z P].** Zostaje u ludu 1.0-4.6 tys. zl/d (0.5-2.3% daniny miast). Highgarden w d728 ok. 177 tys. zamiast 9.8 tys. (przy deficycie ok. 2.6 tys. zl/d - ok. 65 dob wiecej "stac"). Glebokiej przyczyny A4 (zarobek nie idzie za dobrobytem i m_t, LUD 8.2) ta regula nie leczy - kupuje czas; leczenie to KAT przeplywu. Korony: Reach, Qarth, Pentos, Zachod, Krainy Burzy -0.1..-1.3 tys. zl/d; Pentos (z brakiem W5) -0.44 tys. zl/d -> niepokryte Pentos +0.4 [S].

**Ryzyko rok 1.** Q1-Q4: 0.5 / 2.5 / 4.4 / 5.2 tys. zl/d (0.9-1.4% daniny miast) zostaje u ludu; alarm 2 rachunku (danina < 170 tys. w 2 oknach) - w roku 2 jedno okno 152.5 [P BD 2.6]; B5 obniza je o ok. 1-4 tys. [S] - pilnowac.

**Gracz.** W menu ludu (PeopleMenuShowsSavings, "War aid") nowy powod (EN): "No war aid from <town>: its people earned less than they needed over the last 28 days." Dotyczy tez lenna gracza (jedna regula).

**Klucz.** `FolkWarAidFromProfitOnly` (bool, true); off = 449d453. **Linia:** "Danina z oszczednosci (P1)": `| miast bez daniny - zarobek 28 dob ponizej potrzeby (B5) N, nadwyzka ponad zapas w nich X zl`. **Prog:** R2B-6 danina z miast z zarobkiem 28 dob < potrzeby = 0; danina swiata d91-120 (nowa kampania) >= 0.95 x baza W53; P1-6 "stac pelny koszyk" (nowa kampania, okno 64 dob do d120) >= baza - 0.3 pkt; rok 2: miast < 0.95 "stac" na koniec testu <= baza.

**Kod.** `KingdomTreasury.WarAid.cs` `WaRate` (nowy `why`), pierscien 4 okien zarobku i potrzeby na miasto w `TownFolk` (stan sesji; po wczytaniu brama nieczynna do pierwszego pelnego okna = jak dzis). Bez zapisu.

---

## 4. A15 - reszta: decyzje bez zmiany zachowania (z liczbami)

| sprawa | decyzja | dlaczego [P/S] | co w logu |
|---|---|---|---|
| zwyciezca pokoju fabularnego dostaje 0 | **bez zmian** | latka T9 to decyzja Jeffa 08.10; ROT wznowil 111 z 111 takich wojen w ciagu 2 dob, wiec reparacja bylaby przelewem za pokoj na 1-2 doby; zwykle pokoje placa (1.9 mln w roku 2) [P RODY 5.3]; fabuly ROT nie ruszamy | linia 165: rozbic "raty reparacji" na `reparacje X / okupy krolow (178) Y` - dzis ok. 90% to okupy (rok 2: 16.3 mln rat przy 1.9 mln reparacji) [P kod `AddCrownDebt`] |
| kraje w pokoju placa najemnikom z zapasu | **bez zmian** | rok 2: kontrakty w pokoju 0.3-1.8 tys. zl/d, z zapasu 1/360 najwyzej 0.1-0.6 [P RODY 5.4]; zapas sam gasnie (rok 3 ok. 2.4 tys. zl/d swiata); ciecie zmniejszyloby najemnikow, a P3W3-3 juz jest pod progiem (1 612 < 2 000) [P BD 7 nr 18] | INFO w linii "Kontrakty najemnikow (185)": `w pokoju X, z tego ponad wplywy bez 1/360 Y` |
| "+373 tys." z rozliczen rodow | **bez zmian logiki** | ksiega ma 0 zrodel bez platnika; netto +0.65 mln w roku 2 to roznica kwot naliczonych i przeniesionych (wezwania przyjete 6 528 tys. wobec 6 537 wplaconych) [P RODY 5.5] | `MoneyLedger.ClanTickPrefix/Postfix`: zmiana WorldTotal osobno dla zoldu i reszty (raport roczny nr 36) |
| korona nie doplaca do zalog | zamiast doplaty - B4 | rozdz. 3 B4 | - |
| wezwania, na ktore nie stac / bez wojny | B2, B3 | rozdz. 3 | - |

## 5. Odrzucone w tym KAT (z liczbami - zeby sedziowie i autor wiedzieli, ze bylo liczone)

1. **Zalogi nie pierwsze u biednych w wojnie** (cel pokojowy zalog przy G < R): przesunelaby ok. 10 tys. zl/d z zalog na druzyny Wysp i Wolnych Ludzi [S RODY 6], ale zaloga kosztuje 2.49 zl/czlowieka, druzyna 5.7 [P] - za te same pieniadze ok. 4 tys. ludzi w zalogach mniej, ok. 1.8 tys. w polu wiecej, a zalogi juz sa pod progiem; biedny kraj traci twierdze -> traci dochod. **Nie.**
2. **W5 przed zwrotem zoldu** (w krolestwach z brakiem zwrot 15.9 tys. zl/d idzie przed doplata [P]): netto +0.4 zl/zl, a brak zwrotu w wojnie korona pozycza w Banku (186) - wydatek ponad dochod. **Nie.**
3. **Wyzszy cel W5** (`CrownWarAidFillTarget` 0.60 -> wiecej): rody z doplata nie werbuja wiecej (W5-4 40.5%, A16) [P] - pieniadz nie jest ich waskim gardlem. **Nie.**
4. **Wspolny garnek miedzy krolestwami / pomoc sojusznicza** (renty krolestw bez braku 166-280 tys. zl/d [P]): miedzy krolestwami w wojnie daje tylko +0.4 zl zoldu na 1 zl (bogaty traci 0.6 pulapu, biedny zyskuje 1.0) - 20 tys. zl/d = ok. +0.6 tys. ludzi; Wolni Ludzie nie maja sojusznikow; Wyspy walcza z Polnoca i Dolina, wiec "garnek swiata" placilby wrogowi. To nowe zrodlo (KAT przeplywu, ewentualnie decyzja Jeffa) - nie regula wydatku. **Nie w tym KAT.**
5. **Bank dla biednych koron**: pozyczka przesuwa brak na rok 3, kiedy i tak znika 1/360; strukturalny brak (Wyspy, Wolni Ludzie) nie ma z czego splacic. **Nie.**
6. **Danina w pokoju** (A5: lokata krolestw w pokoju 16.6 mln [P LUD 4.2]): podatki na ludzi tylko "na czas wojny" (Jeff 13.10) i danina tylko przy potrzebie korony (zasada 09.10, STAN-PRAC 13.10) - to nowy kanal, nie istniejacy. **Nie bez slowa Jeffa.**
7. **Pulap biedy wedlug kiesy** (zeby zold przyciety Wysp 4.8 i Wolnych Ludzi 2.8 tys. zl/d spadl do 0): zwolnienia zmniejszylyby wojsko; zalegly zold to 0.12% zlota (raport roczny nr 31). **Nie.**

## 6. A5 i A4 w tym KAT - stanowisko

- **A5 (lezace oszczednosci):** budzet nie ma kanalu do lokaty w pokoju (pkt 5.6). Projekt jest z kazdym rozwiazaniem przeplywu ZGODNY i je wzmacnia: z B1 kazda dodatkowa zlotowka, ktora trafi do skarbca krolestwa w wojnie z brakiem (z jakiegokolwiek kanalu), idzie najpierw w zold druzyn, nie w renty [S]. Zmeczenie /4.33 (w grze) wydluza wojny, a wiec i danine - lokata w pokoju powinna rosnac wolniej niz 34-48 tys. zl/d [S, nie wiem ile - rozdz. 8]. Prog P1-1 (6 mln) jest nieosiagalny przy dzisiejszej nadwyzce ludu (rownowaga reguly ok. 15 mln w pelnej wojnie [P LUD 4.4]) - proponuje go rozbic: `lokata W krolestw w wojnie <= 10 mln` (Q8: 8.39 [P] - mierzy, czy dziala istniejacy kanal) + `lokata w pokoju` jako INFO do decyzji o kanale.
- **A4 (biedota):** B5 zatrzymuje zapas u ludzi, ktorzy traca (1-5 tys. zl/d); B4 i Z.1 trzymaja zolnierzy w pokoju, a ci wydaja 25% w miastach (zarobek ludu z wojska -79 tys. zl/d w Q5 -> Q8 [P LUD 8.1]) - Aquos Dhaen, Selhorys, Volon Therys stracily zarobek wlasnie przez pokoj [P LUD 7]. Przyczyny Oldtown (m_t 5.7), Braavos i Rhyos (pusta kasa kupcow) budzet nie dotyka.

## 7. Progi grupy `rok2` (dla `tools/sprawdz_logi.py`) i plan testu

| id | co | test | prog | baza |
|---|---|---|---|---|
| R2B-1 | renty 180 / wplywy dnia w krolestwach z niepokryta potrzeba W5 | rok 2 (zapis `autotest-rok2-d780-fin24` + 60 dob) | <= 5% | dzis 18% [P] |
| R2B-2 | niepokryte po W5/W5b/W6 swiata, d781-840 | rok 2 | <= 30 tys. zl/d | Z.1 d751-781 37.3 [P] |
| R2B-3 | 168: zerwania z braku wplywow, pokoj sojusznika z 168, rata ponad (wplywy - rezerwa) | oba | 0 / 0 / 0 | rok 1: 6 zerwan [P] |
| R2B-4 | rata przy wezwanym bez wojny z celem | nowa kampania 120 dob | 0 | Skagos d13-70 [P] |
| R2B-5 | zalogi swiata d840; zalogi w pokoju; ludzie w partiach w pokoju | rok 2 | >= 69 020; >= 18.2 tys.; >= 12.6 tys. | 64.3 / 14.6 / 14.0 tys. [P] |
| R2B-5n | zalogi d120 | nowa kampania | >= 69 020 i >= W53 | E4/8 N4 |
| R2B-6 | danina z miast z zarobkiem 28 dob < potrzeby; danina d91-120; P1-6 | oba / nowa | 0; >= 0.95 x W53; >= W53 - 0.3 pkt | `kiesa-obieg.csv` |
| R2B-7 | wojsko W5-7 d93-120 (rok 1 bez pogorszenia) | nowa kampania | >= W53 - 2.0 tys. | log W53 `Armoury-2026-10-14_03-24-46.log` |
| R2B-8 | wojsko W5-7 d813-840 (rok 2) | rok 2 | >= Z.1 d752-781 + 0.5 tys. (INFO przy innej liczbie krolestw w wojnie) | Z.1 54.9 tys. [P] |
| R2B-9 | zrodla bez platnika (MoneyLedger), Armoury ERROR | oba | 0 / 0 | - |

Plan testu (sesja glowna): (1) zapis `autotest-rok2-d780-fin24` + 60 dob - R2B-1, -2, -3, -5, -6 (rok 2), -8, -9; (2) nowa kampania 120 dob - R2B-3, -4, -5n, -6, -7, -9 oraz istniejace grupy w5, w6, w7, z1, kiesa1 (bez nowych NIE). Kazdy klucz osobno wylaczalny - przy NIE w R2B-7 najpierw off B5, potem B4 (zasada "jedna zmiana naraz" w diagnozie).

## 8. Kontrola calosci (zasada 0) i czego nie wiem

**Regresje / kolizje [P kod].**
- B1: `row.Pot` czytaja W5b (`CrownSurplusPlan`: `row.Need - row.Pot`), W6 (`sc.Uncovered`), W5b regula 1 (`row.Uncov` -> stawka daniny), W7 (`LvPlan`) - wszystkie licza "ponad garnek", wiec dostosuja sie same; W5 gotowka nastepnej doby zdejmowana z podstawy zwrotu (`NeedAidRefundCut`) - 1 zl = 1 zl bez zmian. Linia 165: "W5" rosnie, "renty" maleja o te sama kwote.
- B2: 168 biegnie przed zwrotem i W5 - rezerwa z wczoraj (bez petli w dobie). Sciezka zerwania (`EndCallToWarAgreement`, `MakePeaceAction`, `WorldClock7.Mark168`, P3/W4 wyrownanie portfela) przy kluczu on nieuzywana; `Measure31.CtwPay` (ksiega) bez zmian; `EndPostfix` przy koncu przez gre - jak dzis. Do sprawdzenia przez autora: czy Diplomacy/gra czyta `CallToWarWallet` wezwanego przy decyzji o pokoju (cudzy kod).
- B4: istniejacy klucz; `Releases` krok 1 (w pokoju zalogi ponad cel zwalniane pierwsze) - mniej zwolnien; werbunek zalog K8 - wiecej oplat do sprzedajacych (przelew); ludzie z ksiegi BK krainy (ok. 6 tys. z 51.6 mln).
- B5: ten sam wzor co oblezenie (R22) i P1b (`why`); okno menu ludu czyta `FolkWarAidRate` - potrzebny napis nowego powodu.
- Zapis: zadna regula nie dodaje pola zapisu; stan sesji po wczytaniu = zachowanie jak przy off przez 1 dobe (B2) albo do pierwszego pelnego okna (B5).
- Gracz: B1 (mniej rent wasala w krolestwie z brakiem), B2/B3 (komunikat), B5 (menu ludu) - te same reguly co AI.

**Czego nie wiem (+ linia diagnozy).**
1. Czy po Z.1 zalogi pokoju wiaze cel 50%, a nie werbunek (wynik "14.6 tys. = 50% z 29.4 tys." to zgodnosc, nie dowod). Linia B4 (cel, zold, "na celu N z M").
2. Ile wojska i braku przyniesie zmeczenie /4.33 w roku 2 - brak biegu dwuletniego po zmianie; skutek B1/B2 rosnie z liczba krolestw w wojnie z brakiem. Linia W5 "na krolestwo" juz wystarcza.
3. Czy wezwany niedoplacony (B2) zachowuje sie inaczej (pokoj, zmiana strony) - zalezy od cudzego kodu; linia B2 + CS "KRONIKA WOJEN" (pokoje wezwanych w trakcie porozumienia).
4. Jaka czesc "potrzeby" biednych miast to dzielenie przez czynnik kotwicy (LUD 10 pkt 5) - B5 porownuje z ta liczba; przy zawyzonej potrzebie brama obejmie wiecej miast niz trzeba. Linia: w `kiesa-obieg.csv` potrzeba przed dzieleniem.
5. Skutek B1 na pulapy bogatych rodow w krolestwach z brakiem (mniej rent -> mniej D) - szacunek do -0.2 tys. ludzi u Wysp; linia `budzet-rodow.csv` (renta_180, doplata_w5) na rod.

## 9. Pliki i polecenia

Skrypty: `C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\c36b73ca-1de0-4b1b-80ce-71bd02afe545\scratchpad\budzet\` (dysk C - tymczasowy); wejscia `..\rody2\dw.pkl`, `z1.pkl`, `w52.pkl` (wyciag `x1_log.py` z rozpoznania RODY).
```
python -I b1.py ..\rody2\dw.pkl                      (B1 + B2 wariant "brak wczoraj": min(niepokryte, renty) kwartalami; z1.pkl "721-750,751-781"; w52.pkl "2-60,61-121")
python -I b2.py ..\rody2\dw.pkl                      (B2 z rezerwa = wczorajszy zwrot + wczorajsza niepokryta potrzeba)
python -I a4.py D:\Backup-Bannerlord\logs\2026-10-14-noc\Logs\2026-10-13_21-14-13\kiesa-obieg.csv   (B5: danina z miast z zarobkiem < potrzeby)
```
Przeplyw korony w krolestwach z brakiem (rozdz. 1, wiersz "krolestwa z brakiem") - polecenie `python -I -c` na `dw.pkl` w tej sesji (srednia d365-728 pol `k165`: wpl, danina, zwrot, w5, renty, wezw, kontr dla krolestw z `w5k.u > 0`).
