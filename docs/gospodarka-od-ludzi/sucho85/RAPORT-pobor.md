# POBOR po regule "produkuja ludzie" (nakladka P4) - rachunek na papierze (13.10)

Pytanie: czy po regule wojsko krolestw w ogole zdazy sie zwerbowac, i co robi milicja.
Tylko odczyt logow i tabel dwoch biegow; nic w grze, kodzie ani ustawieniach nie zmienione.
**[P]** pomiar (log, tabela, kod), **[S]** szacunek na pomiarach. "Nie wiem" = nie da sie ustalic z tych danych.

Biegi: **W37** = bieg roczny `Armoury-2026-10-13_06-24-10.log` (srednia dob 337-364); **W43** = bieg 120 dob `Armoury-2026-10-13_13-28-08.log`
(to, co jest w grze; srednia dob 93-120 - to jeszcze rozruch, nie koniec roku).

## 0. Wynik w 10 liniach

1. Dzis werbunek NIE wiaze. 90% miejsc na ochotnika u notabli stoi pelnych; wodzowie i zalogi zabieraja 8-10% tego, co notable moga dac na dobe [P].
2. Wyjatek jest jeden: Sarnor. Hamulec wolnych rak odmawia tam 294-328 ochotnikow dziennie, a jego panowie maja miejsce w pulapie na 0.6-1.5 tys. ludzi [P].
3. Pozostale krolestwa trzyma pieniadz (pulap zoldu) albo limit druzyn, nie brak ludzi. Tak samo pisal raport roczny: 71% luki to pulap.
4. P4 tnie szanse ochotnika w pustych krainach 1.5-10 razy (Dorne x0.5, Dragonstone x0.3-0.45, Ibben x0.2-0.35, Wyspy x0.3-0.45 mimo udzialu 2) [S].
5. Mimo to w spokojniejszej wojnie notable nadal daja wiecej, niz trzeba. W ciezkiej wojnie (W43, doby 93-120) za malo maja Ibben i Dragonstone; na styk sa Dorne, Lorath, Lys i Sarnor [S].
6. Koszt P4 dla progu 85 tys. na koniec roku: S0 0.4-3.9 tys., S1 0.3-3.8 tys., S2 i S3 od +0.1 do -2.3 tys. ludzi lordow w wojnie [S]. To malo przy tym, co robi pieniadz.
7. Ludne krolestwa (Reach, Volantis, Qarth, Braavos) z P4 prawie nic nie zyskuja w druzynach: tam wiaze pieniadz albo limit [S].
8. Milicja: swiat +10-12% (prog P4 to 0.9-1.1 bazy - nie wyjdzie bez poprawki). Reach, Qarth, Volantis x2.2-2.7; Sarnor, Skagos, Ibben, Wyspy Letnie -70 do -90%; Dragonstone i Wyspy Zelazne ok. -63%; Dorne -49%; Polnoc -35% [S].
9. Milicja nie liczy sie do 85 tys., ale broni twierdz: obroncow na twierdze w pustych krolestwach o 12-62% mniej, w ludnych (Reach, Qarth, Volantis, Braavos, Dothrakowie) o 37-51% wiecej [S]. Puste krolestwa beda latwiej tracic zamki.
10. Pyt. 3: Sarnor 0.25 mln zdejmuje hamulec (obciazenie 20-25% -> 8-10%, odmowy -> 0), ale P4 przy skali 0.2 zjada wiekszosc tego zysku. Straz P4 dotyka, jesli w kodzie nie bedzie jawnego wyjatku [S].

## 1. Droga werbunku lorda AI - z kodu

Skroty: **A\** = nasz kod (galaz w-toku/zbior-e8, WGRANIE 43) `...\ae222cb7-...\scratchpad\zb\int\Armoury\src`; **G\** = dekompilacja gry `...\scratchpad\dec32\cs_full`; **BK\** = BannerKings `...\scratchpad\e4tools\bkfull`.

| ogniwo | jak dziala | plik:linia |
|---|---|---|
| miejsca u notabla | gra co dobe przechodzi 6 miejsc kazdego notabla (BK robi tablice na 10, ale petla gry idzie do 6); puste miejsce zapelnia sie z szansa z modelu | G\...CampaignBehaviors\RecruitmentCampaignBehavior.cs:216-251; BK\...Behaviours\BKNotableBehavior.cs:144-170 (zamek: tylko pierwszy notabl, 6 miejsc) |
| szansa BK | 0.5 x (1 + moc notabla / 150 + stabilnosc - 0.5 + polityka, rzad, rada...) | BK\...Vanilla\BKVolunteerModel.cs:334-414 |
| nasza chec do sluzby | szansa BK x chec; chec = 0.05 + wolne rece max(0, f - a) + 0.2 x nedza regionu, najwyzej 1.5; tylko PUSTE miejsce (stojacy ochotnik - bez zmian) | A\Levy.cs:98-113, :115-141 (:123 pelne miejsce, :136-137 mnoznik); A\Settings.cs:671-674 |
| hamulec wolnych rak | kraina, w ktorej sluzy i zginelo co najmniej f mezczyzn (a >= f), nie daje nowego ochotnika (szansa 0); f = 20%, Wyspy 20-40% (z niewolnymi), Wolni Ludzie bez hamulca (f = 100%), Straz bez hamulca | A\Levy.cs:126-134; A\FreeHands.cs:119-128 (FOf), :166-182 (a, Brake, Excess), :230-236; A\Settings.cs:863, :867 |
| ksiega ludzi | kazdy rekrut schodzi z domu swojego regionu; milicja, nowe druzyny (szablon) i karawany tez z wolnych rak; kraina z hamulcem nie dostaje nowej milicji | A\ArmsFromPeople.cs:22-34 |
| wizyta wodza | przy wejsciu do osady 7 prob, potem co godzine pobytu 1 proba; w jednej probie najwyzej 1 ochotnik od notabla: losowe miejsce startowe, trzeba trafic w zapelnione miejsce ponizej dostepnego indeksu, przyjecie maleje z wypelnieniem druzyny | G\...RecruitmentCampaignBehavior.cs:564-605, :292-305, :505-562 |
| dostepny indeks | 1 + relacja / 10 + prawo poboru (wolne kontrakty: +5 dla wlasnego krolestwa) - dla swoich zwykle wszystkie 6 miejsc | BK\...BKVolunteerModel.cs:42-87, :214-218 |
| pieniadz | wodz nie werbuje, gdy przekroczony limit zoldu albo brak budzetu na zold rekruta; limit partii z wolnego miejsca w pulapie rodu | G\...RecruitmentCampaignBehavior.cs:461, :498, :554; A\ClanBudget.cs:30, :191-212 (pulap: wojna 0.60 x D x f + skrzynia), :512 |
| krok werbunku | doplata W5 daje rodowi miejsce w pulapie najwyzej na 10% limitu druzyn na dobe | A\KingdomTreasury.NeedAid.cs:13, :34-35, :114 |
| koszt rekruta | 10 dni zoldu jednostki (najemnik x2) | A\RecruitCost.cs:19, :93; A\Settings.cs:752 |
| zalogi (A5) | zaloga werbuje od notabli swojej twierdzy, najwyzej 2 na dobe (K8) | A\ArmsFromPeople.cs:25-27 |
| milicja | przyrost: 2 (twierdza) / 0.5 (wies) + dobrobyt / 1000 albo hearth / 400; ubytek 2.5% stanu na dobe; u nas z wolnych rak | G\...GameComponents\DefaultSettlementMilitiaModel.cs:94-120; A\ArmsFromPeople.cs:28-29 |
| Straz | kanaly gry (notable i karczma Daru, A5, szablon, zalogi z modelu, milicja) decyduja ILU braci przybywa; WatchLaw - SKAD (Mur, pule wyrzutkow, ludnosc 10 krain Westeros) | A\WatchLaw.cs:19-22 |

Wniosek z kodu: ochotnik rodzi sie u notabla (6 miejsc), a wodz go zabiera tylko wtedy, gdy zajrzy do osady i ma pieniadz. P4 mnozy tylko szanse zapelnienia pustego miejsca (A\Levy.cs:136-137) i przyrost milicji - nie dotyka nowych druzyn z szablonu, karczmy (weterani), jencow ani limitu.

## 2. Pomiar w biegach [P]

Skrypt `p1_pomiar.py`, wyniki `p1_W37.out.txt`, `p1_W43.out.txt` (+ .json).

| miara na dobe (srednia 28 dob) | W37 koniec roku | W43 doby 93-120 |
|---|---:|---:|
| notabli / miejsc na ochotnika | 3 372 / 20 230 | 3 467 / 20 803 |
| puste miejsca (losowania szansy) | 1 610 | 2 100 |
| odmowy hamulca wolnych rak | 370 (Sarnor 328, Ibben 22, Crownlands 20) | 306 (Sarnor 294, Ibben 6, Crownlands 5) |
| miejsc pelnych | 90.2% | 88.4% |
| chec do sluzby (mnoznik szansy BK) | x0.37 | x0.39 |
| werbunek wodzow od notabli | 434 | 716 |
| werbunek zalog od notabli (A5) | 210 | 262 |
| szansa zapelnienia pustego miejsca (pobrane / puste) | 0.40 | 0.47 |
| wykorzystanie tego, co notable moga dac | 8.0% | 10.1% |
| WEJSCIA druzyn rodow: karczma / jency i mapa / nowe druzyny (szablon) / z zalog | 76 / 38 / 448 / 302 | 52 / 29 / 410 / 318 |
| WYJSCIA druzyn rodow: zabici / niewola / zwolnieni z pulapu / do zalog / dezercja | 398 / 321 / 332 / 153 / 17 | 337 / 139 / 50 / 258 / 8 |
| bilans druzyn rodow | -213 | +270 |
| milicja swiata (stan / przyrost / odmowy / ubytek bez walki) | 73.6 tys. / 231 / 30 / 127 | 66.3 tys. / 321 / 18 / 188 |
| wojsko lordow w wojnie (miara progu, linie Skarbce) | 79.4 tys. | 109.0 tys. |

Udzial notabli w doplywie do druzyn rodow (phi): wprost 33% (W37) / 47% (W43); razem z ludzmi zalog, ktorych zaloga wziela od notabli: 47% / 57% [S]. Reszta to szablon nowych druzyn z wolnych rak, karczma i jency - P4 ich nie dotyka.

Wedlug krolestw (tabele `ludzie-regiony.csv` i `bitwy.log`): werbunek w regionach krolestwa 1-160 ludzi na dobe, straty druzyn lordow krolestwa w bitwach (zabici + rozbici + jency) 0-169 na dobe; pelne tabele w `p1_*.out.txt` i `wynik_p2_*.out.txt` (kolumny "popyt", "straty"). Przyklady W37: Dorne 33 / 100, Polnoc 34 / 6, Reach 63 / 42, Lys 23 / 57. Werbunek jest zapisany wedlug MIEJSCA (osada notabla), straty wedlug KROLESTWA wodza - to przyblizenie: w obcym regionie werbuja tylko wodzowie krolestw w pokoju z wlascicielem.

Czy dzis wiaze werbunek czy pieniadz: **pieniadz**. W kazdym krolestwie poza Sarnorem pelnych jest 80-97% miejsc [S z P], a raport roczny (wojsko.md 5.1-5.5) mierzy 71% luki jako pulap zoldu. Sarnor: 100% notabli za hamulcem, 0 nowych ochotnikow; jego rody maja miejsce w pulapie na 1.5 tys. (W37) / 0.6 tys. (W43) ludzi [S z P].

## 3. P4 - czy po regule wojsko zdazy sie zwerbowac

Skrypt `p2_p4.py`, wyniki `wynik_p2_*.out.txt` (+ .json). Regula P4 (projekt 3.3, 3.5): szansa pustego miejsca x s, s = min(3, max(0.1, skala x udzial zbrojnych)); skala wsi u = ludzie na wies regionu / srednia wies swiata (72-73 tys.), skala miasta m = ludzie krolestwa na miasto / srednia swiata; udzial: Wyspy 2, Wolni Ludzie 5, Dothrakowie 3, reszta 1; Straz poza regula.

Model [S]: puste miejsce zapelnia sie z szansa q x s; wodzowie zabieraja w tempie proporcjonalnym do liczby pelnych miejsc (tak dziala losowe miejsce startowe przy indeksie 6). Tempo zabierania kalibrowane z dzisiejszego werbunku regionu. Druzyna rodu = mniej z dwoch: pulap zoldu w ludziach albo rownowaga werbunku; ubytek ochotnikow przenosi sie na druzyne w czesci phi. Widelki: **nisko** (mniejsza strata) - phi wprost, rody na pulapie maja zapas werbunku do 64% limitu; **wysoko** (wieksza strata) - phi z zalogami, popyt skupiony na polowie notabli, bez zapasu.

### 3.1 Skala P4 i moc notabli (W37 / W43)

| krolestwo | s wsi | s miast | moc dzis -> z P4 (ochotnikow / dobe, gdyby wszystkie miejsca brac od razu) | straty druzyn w bitwach / dobe W37 / W43 | ocena |
|---|---:|---:|---|---|---|
| Ibben | 0.33 | 0.18 | 113-158 -> 33-43 | 11 / 51 | W43: ponizej strat (moc 43, potrzeba z notabli ok. 29, realnie bierze sie 30-50% mocy) |
| Dragonstone | 0.43-0.45 | 0.25-0.33 | 170-302 -> 64-124 | 27 / 106 | W43: ponizej strat w ciezkiej wojnie |
| Dorne | 0.49 | 0.52 | 331-548 -> 166-274 | 100 / 33 | na styk przy stratach W37 |
| Lorath | 0.60-0.63 | 0.63-0.73 | 128-201 -> 78-133 | 49 / 68 | na styk |
| Lys | 0.68 | 0.82-0.90 | 128-205 -> 94-154 | 57 / 4 | na styk przy stratach W37 |
| Sarnor (0.25 mln) | 0.20 | 0.22 | 151-185 -> 31-39 | 4 / 17 | na styk; patrz rozdz. 5 |
| Wyspy Zelazne | 0.43-0.44 | 0.30-0.31 | 576-646 -> 227-251 | 8 / 3 | wystarczy (malo strat, duza checi: prog 40%) |
| Polnoc | 0.66-0.68 | 0.61-0.62 | 832-934 -> 550-608 | 6 / 0 | wystarczy |
| Wolni Ludzie | 1.10-1.12 | 0.93-0.94 | ok. 650 -> ok. 670 | 8 / 5 | bez zmian (chec x1.5 - brak hamulca) |
| Reach / Volantis / Qarth / Braavos | 1.7-3.0 | 2.2-2.5 | x2-3 wiecej | 14-69 | wiecej ochotnikow, ale wiaze pieniadz albo limit |

### 3.2 Ile to kosztuje druzyny lordow w wojnie na koniec roku [S]

Zmiana od samego P4 (ujemne = strata), tys. ludzi, widelki nisko-wysoko. Dwie podstawy: W37 (koniec roku, spokojniejsza wojna) i W43 (doby 93-120, ciezka wojna i wiekszy popyt).

| scenariusz | baza W37 | baza W43 | razem |
|---|---|---|---|
| S0 (dzis + P4) | -0.4 do -2.2 | -1.2 do -3.9 | **-0.4 do -3.9** |
| S1 (pierwszy krok) | -0.3 do -2.0 | -0.9 do -3.8 | **-0.3 do -3.8** |
| S2 (granica) | +0.1 do -0.8 | -0.3 do -2.3 | **+0.1 do -2.3** |
| S3 (nowa kampania wedlug ludzi) | jak S2 | jak S2 | **+0.1 do -2.3** |

Do tego Sarnor przy pyt. 3 (0.25 mln): P4 zabiera mu dodatkowo ok. 0.5-1.5 tys. ludzi, ktorych zyskalby po zdjeciu hamulca (rozdz. 5). Straz bez jawnego wyjatku: dodatkowo -0.5 do -1.5 tys. (faza W43).

Podstawa scenariuszy - moje przyblizenie, tylko do policzenia popytu na ochotnikow [S]: S0 = 79.4 tys. [P, W37 koniec roku] albo ok. 90 tys. (W43 mial w dobach 93-120 o 14% wiecej niz W37 w tych samych dobach), widelki 80-95 tys.; S1 = S0 x 0.99-1.00 (79-94 tys.); S2 = S0 x 0.88-0.92 (70-87 tys.); S3 na koniec roku 1 ~ S2. Z P4: S1 76-94, S2 i S3 68-87 tys.

Dlaczego w S2 mniej: krolestwa puste traca w S2 pieniadz, wiec ich druzyny i tak maleja do pulapu - mniej werbuja i brak ochotnikow mniej je boli. P4 boli najbardziej wtedy, gdy puste krolestwo ma jeszcze pieniadz (S0, S1).

Kto traci (S0, najwieksze z obu baz, ludzi): Dragonstone 280-780, Dorne 160-730, Ibben 0-760 (W43), Wyspy Zelazne 0-610 (W43), Polnoc 40-300, Lorath 40-170, Tyrosh, Lys, Myr, Targaryen (Aegon) po 30-170. Kto zyskuje: Reach do +220, Volantis do +170 (S2), Qarth do +90, Braavos do +80, Dothrakowie do +70 - malo, bo tam wiaze pieniadz albo limit.

Pod progiem 85 tys. P4 rozstrzyga tylko wtedy, gdy swiat jest juz o 1-4 tys. od progu. Pieniadz (S1/S2) przesuwa wojsko o kilka razy wiecej.

## 4. Milicja [S]

P4: przyrost milicji x s, ubytek 2.5% stanu bez zmian - milicja w rownowadze idzie razy s (dojscie ok. 40 dob). Milicja regionu rozdzielona na twierdze i wsie wedlug przyrostu z modelu gry (2 + dobrobyt / 1000 na twierdze, 0.5 + hearth / 400 na wies).

| | dzis [P] | po P4 [S] |
|---|---:|---:|
| milicja swiata W37 | 73.2 tys. | 82.0 tys. (+12%) |
| milicja swiata W43 | 65.9 tys. | 72.6 tys. (+10%) |

Swiat ROSNIE, bo w ludnych krolestwach skala dochodzi do 2-3, a dolna granica 0.1 i udzial zbrojnych podnosza puste. Prog P4 z projektu (milicja swiata 0.9-1.1 bazy) przy W37 nie wyjdzie. Prosta poprawka do projektu: podzielic skale milicji przez srednia swiata wazona milicja (ok. 1.10-1.12) - suma zostaje, podzial idzie za ludzmi.

Wedlug krolestw (W37; obroncy twierdzy = zaloga + milicja samej twierdzy, srednio na twierdze):

| krolestwo | milicja dzis -> po | obroncow na twierdze dzis -> po |
|---|---|---|
| Qarth | 1 496 -> 4 073 (+172%) | 763 -> 1 155 |
| Volantis | 2 155 -> 5 606 (+160%) | 614 -> 898 |
| Reach | 6 406 -> 13 901 (+117%) | 493 -> 726 |
| Braavos | 2 015 -> 3 908 (+94%) | 556 -> 786 |
| Dothrakowie | 2 423 -> 4 584 (+89%) | 541 -> 740 |
| Polnoc | 7 573 -> 4 921 (-35%) | 609 -> 533 |
| Dorne | 3 634 -> 1 868 (-49%) | 570 -> 482 |
| Wyspy Zelazne | 3 620 -> 1 391 (-62%) | 709 -> 564 |
| Dragonstone | 1 843 -> 644 (-65%) | 716 -> 572 |
| Ibben | 1 222 -> 286 (-77%) | 634 -> 460 |
| Sarnor (0.25 mln) | 1 119 -> 234 (-79%) | 275 -> 164 |
| Skagos / Wyspy Letnie / Yi Ti | -72 do -87% | 287-307 -> 109-112 |

Co to zmieni w oblezeniach: w pustych krolestwach twierdze broni o 12-62% mniej ludzi, wiec szturm jest tanszy i szybszy; twierdze Reach, Qarthu, Volantis, Braavos i Dothrakow beda bronione przez 37-51% wiecej ludzi. Do tego slabsza milicja wsi gorzej broni sie przed rabunkiem - w pustych krainach wiecej spalonych wsi, a spalona wies daje mniej (sprzezenie, ktorego nie policzylem). Ile zamkow wiecej zmieni wlasciciela - **nie wiem** (to pokaze tylko bieg). Milicja nie wchodzi do progu 85 tys.

## 5. Pytanie 3: Sarnor 0.25 mln i Nocna Straz

**Sarnor.** Obciazenie regionow Sarnoru dzis 20-25% mezczyzn przy progu 20% - wszystkie 5 regionow za hamulcem [P, ludzie-regiony.csv]. Przy 2.5 raza wiecej ludzi: 8-10% [S] - hamulec znika, odmowy 294-328/d spadaja do 0 (dopoki sluzba i polegli z Sarnoru nie wzrosna 2-2.5 raza; w rok nie powinny) [S].
Ale z P4 skala Sarnoru wynosi tylko 0.2 (0.08 x 2.5): moc ochotnikow 31-39/d zamiast 151-185/d bez P4. Luka "ma miejsce w pulapie, nie werbuje" (1.5 tys. W37, 0.6 tys. W43) bez P4 zamknie sie w 2-5 tygodni, z P4 w ok. 10 miesiecy (W37) albo wcale przy stratach W43 (17/d) [S, przy zabieraniu 30% mocy]. P4 zjada wiec wiekszosc zysku z pyt. 3: ok. 0.5-1.5 tys. ludzi Sarnoru na koniec roku. Jesli Sarnor ma realnie zyskac, P4 potrzebuje dla niego wyzszej dolnej granicy (np. 0.3-0.5) - decyzja do projektu, nie do mnie.

**Nocna Straz.** Ludzie Strazy przychodza przez WatchLaw z 10 krain Westeros, ale o LICZBIE braci decyduja kanaly gry w osadach Strazy: notable i karczma Daru (W43: 20/d), A5 (4/d), milicja (5/d) [P, linia "Straz z Westeros"]. Levy.ProbabilityPostfix (A\Levy.cs:115-141) nie ma dzis wyjatku dla Strazy - jesli P4 wejdzie tam bez wyjatku, wies Daru ma skale 0.02 -> s = 0.1: moc ochotnikow Strazy 94 -> 9 na dobe, milicja Daru -90% [S]. Straz traci malo w bitwach (4/d W43) i stoi na pulapie w ludziach (92-94%), wiec w rownowadze utrzyma sie, ale po kazdej porazce bedzie sie odbudowywac kilka razy wolniej; model daje -0.5 do -1.5 tys. w fazie W43 [S]. Wniosek: P4 dotyka Strazy, jesli w kodzie nie bedzie jawnego wyjatku (osada krainy nightswatch -> s = 1 dla werbunku i milicji) - zgodnie z decyzja "Straz - jawny wyjatek".

## 6. Zalozenia, od ktorych wynik zalezy najbardziej

1. Wodzowie zabieraja ochotnikow proporcjonalnie do pelnych miejsc i nie wydluzaja pobytu, gdy ochotnikow jest malo [S z G\...RecruitmentCampaignBehavior.cs:505-562]. Gdyby szukali dluzej - strata mniejsza; gdyby werbowali tylko w kilku osadach przy froncie - wieksza (to wariant "wysoko").
2. Notabli: wies 4.1-4.2, miasto 9.5-9.9, zamek 1 [S z P: linia "Notable (E5/0)" - 2 321 z 3 372 wiejskich; podzial miasto / zamek zalozony].
3. Udzial notabli w doplywie do druzyn 33-57% [S z P]; szablon nowych druzyn, karczma i jency nie ida przez P4.
4. Werbunek przypisany krolestwu wedlug regionu osady, nie wodza [S].
5. Skala wsi = srednia regionu (nie kazda wies osobno) - dolna granica 0.1 dziala na srednia [S].
6. Rod "z luzem" trzyma rownowage werbunku, rod na pulapie ma zapas do 64% limitu [P raport roczny 5.2].
7. S1/S2/S3: druzyny wedlug D krolestw z r04 (pierwszy krok / granica) ze sprezystoscia 0.75 i do 64% limitu - tylko po to, by policzyc popyt na ochotnikow; wlasciwe liczby S1-S3 daja inne rachunki.

## 7. Czego nie wiem

- ilu notabli ma kazde miasto i zamek z osobna (log daje tylko sume i notabli wiejskich);
- jak szybko wodzowie oprozniaja miejsca, gdy ochotnikow jest malo (czy zostaja dluzej w osadzie) - tego log nie liczy;
- ile zamkow zmieni wlasciciela przez slabsza milicje pustych krolestw - tylko bieg;
- straty "jency" w bitwach licze tylko dla przegranych z linia H3 (dlatego ok. 40/d zamiast 140-320/d z linii "Jency") - straty pustych krolestw moga byc wieksze, niz w tabeli;
- koniec roku dla kodu WGRANIA 43 - bieg roczny jeszcze trwa; liczby W43 to doby 93-120.

## 8. Jak puscic ponownie (nowy bieg roczny)

```
cd <ten katalog>
python -I p1_pomiar.py --log <Armoury-*.log> --tabele <Logs\<data>> --okno 28 --out p1_NOWY.json
python -I p2_p4.py --log <Armoury-*.log> --tabele <Logs\<data>> --okno 28 --p1json p1_NOWY.json
python -I p2_p4.py ... --sarnor 2.5                     (pyt. 3)
python -I p2_p4.py ... --sarnor 2.5 --straz-wyjatek 0   (P4 bez wyjatku Strazy)
```
`p2_p4.py` bez `--p1json` sam czyta log i tabele. Wynik: `wynik_p2_<log>_sarnor<x>[_bezwyjatku].out.txt` i `.json` (druzyny bez P4 / z P4 na krolestwo dla S0-S3, milicja). Plik r04 (D krolestw: dzis / pierwszy krok / granica) - `--r04`, domyslnie rachunek recenzji `...\dbc3d3d1-...\scratchpad\gosp-ludzie\skrypty-rec\r04_dochod.json`.
