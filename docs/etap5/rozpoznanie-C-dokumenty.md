# Etap 5 - rozpoznanie C: dokumenty i decyzje Jeffa (11.10.2026)

Zakres: wszystko, co dokumenty mowia o etapie 5 PLANU ("Kiesa ludu, kasy miast i glod", pozycje 5.1-5.13 i "Skonczone, gdy"):
decyzje Jeffa z data i miejscem (plik:linia), co jest zdecydowane, co czeka na Jeffa, co nieaktualne po etapach 2-4.
Tylko odczyt: kod, gra i zapisy nietkniete. Linie podane wedlug stanu repo z 11.10 (commit 990dae7).

Skroty plikow:
- PLAN = `docs/PLAN-DO-KONCA-MODA-2026-10-09.md`
- SP = `docs/STAN-PRAC.md`
- KL = `docs/PROJEKT-KIESA-LUDU-2026-10-09.md` (projekt kiesy ludu, wersja 2 po krytyce, 09.10)
- A15 = `docs/audyt-2026-10-09/15-BILANS-PANOW-ZAMKOW.md` (wyrownanie Z15-5, targ przy zamku Z15-6, wersja 2)
- ES = `docs/audyt-2026-10-09/ESENCJA-AUDYTOW-2026-10-09.md`
- OB = `docs/PROJEKT-EKONOMIA-OBIEG-2026-10-08.md` (111', 162, 163, 164c, Z9, bezpiecznik)
- E2 = `docs/PROJEKT-ETAP2-BANKRUCTWA-2026-10-09.md` (184 podatek wojenny, haki kiesy, poprawki do PLAN)
- E3 = `docs/etap3/PROJEKT-ETAP3-SZCZELNOSC-STATKI-2026-10-10.md` (statki 181, co odlozone do etapu 5)
- E4 = `docs/etap4/PROJEKT-ETAP4-KSIEGA-LUDZI-ZIMA-2026-10-10.md`, E410 = `docs/etap4/PLAN-KODU-E410.md`
- P111 = `docs/paczki/111-k6-kasy-miast.md`, PRZ = `docs/PRZEKAZANIE-2026-10-08.md`, JD = `docs/JAK-DZIALA-GRA-ETAPY-1-3.md`
- HISTORY.md i ERRORS.md - przejrzane; o etapie 5 nic (HISTORY.md:80-86 tylko ogolna zasada Jeffa z 04.10 "gospodarka od ludnosci").

---

## 0. Najwazniejsze w 10 punktach

1. **Na oba pytania "Ty" z etapu 5 Jeff juz odpowiedzial (09.10).** 5.7 wyrownanie: "E ... takze u gracza - TAK" (SP:844, 04:25; takze A15 Q1 = E, PRZ:278).
   5.11 targ przy zamku: "F targ przy zamku - TAK" (SP:844) i doprecyzowanie Q2 "woz wsi zamkowej najpierw sprzedaje swojemu zamkowi jedzenie dla podzamcza
   na 10 dni, reszta do miasta - TAK (razem z kiesa ludu, etap 5)" (SP:852-853, 04:55). W PLAN:160 i :164 dalej stoi "Ty" - nieaktualne (wskazane juz w E2:889).
2. **Wszystkie decyzje kierunkowe etapu 5 sa podjete:** glod ma skutki (A), dziesiecina (B), podatek wojenny zamiast mniejszego wojska (C), obciazenie wsi 30-50%
   (pan + sept), wyrownanie, targ przy zamku, BEE "prawdziwa wplata + dar dla notabli" (G), statki razem z podatkiem wojennym (11.10). Nie ma pytania, ktore blokuje projekt.
3. **Waga zamku w rentach: 1.5, nie 1.** PLAN:160 ("zamek 1") jest bledem przepisania - Jeff odpowiedzial na pytanie z waga 1.5 (A15:351, ES:166-168; E2:888 kaze poprawic).
4. **Wies zatrzymuje 60% utargu, nie 30%** (pan 35% + sept 5% = decyzja 15 "30-50%", A15:272, ES:166). PLAN:158 i ES:67 ("30%") to liczba z KL sprzed decyzji 15.
5. **Projekt KL jest stary wobec gry:** liczony na kodzie 2e235ea (sprzed etapow 2-4, KL:14), nazywa paczke "172" (KL:47, :398; w PLAN i SP to 173), zaklada
   wojsko 95 tys. i jego spadek o 5% (KL:41, pytanie 7.3) - Jeff to odrzucil (C, SP:766-772; 10:45 "4% mniej wojska - NIE", SP:909). Model (L1-L9, przeplywy, stawki)
   zostaje; liczby, kolejnosc paczek (KL:387-396) i miejsca w kodzie trzeba przeliczyc na dzisiejsza gre.
6. **Warunki wstepne kiesy ludu z KL sa juz spelnione przez etapy 3-4:** szczelnosc i reszta ksiegi < 10 tys./d (KL:500; E3 20/20, reszta d728 -0.7 tys./d, SP:1116-1117);
   pomiar ludnosci i wzrost < 2%/rok (KL:510-513; E4/4 przyrost +0.5%/rok, SP:1185).
7. **Styki z etapem 4 (w toku):** glod 5.9 dzieli definicje z E4/6 Famine (E4:456-457); liczba mieszczan dla kiesy - miasta "zamrozone" do E4/8 (migracja do miast P2,
   SP:1174, `TownPeopleFrozen`); karczma i weterani E4/10 zmieniaja zloto do kas miast (E410:56, :60, :386-387). Odpowiedz z PRZ:778-779: projekt teraz,
   kod porcji niezaleznych rownolegle, porcje z jedzeniem/karczma po E4.
8. **Do etapu 5 odlozono tez rzeczy spoza listy 5.1-5.13** (E3:696-705, E2:806-809): statki 181, BK `HandleMarketGold`, tarcza zoldu karawan, rabat AI przy kupnie statku,
   bezpiecznik korony, BEE G, sakiewka weterana i "do kasy miasta karczmy" (E410). Trzeba je wpisac do projektu etapu 5.
9. **"Skonczone, gdy" w PLAN:168 ma dwa progi do poprawy** (E2:882, :890; A15:341-342): kiesa panow zamkow -2% na 28 dob (nie -10%) i udzial panow zamkow
   w D swiata >= 30%; wojsko "jak w 2.7" = 95-115 tys. w wojnie, podloga 85 tys. (nie 85-100).
10. **Kalendarz 168 dni nie dotyczy zrodel etapu 5:** KL, A15, OB, E2, E3, P111 licza rok = 364 doby. Jedyne liczby na 168 w okolicy to AUDYT-2026-10-05 l. 924, 1085
    (straty i nabor wojska - nie wchodza do etapu 5).

---

## 1. Decyzje Jeffa dotyczace etapu 5 (chronologicznie)

| Data, godz. | Decyzja (cytat lub skrot) | Plik:linia | Pozycja | Stan |
|---|---|---|---|---|
| 04.10 | gospodarka od ludnosci; pobor zabiera ludzi z pola | HISTORY.md:80-86 | tlo 5.5 | spelnione w etapie 4 (ksiega ludzi) |
| 05.10 | (1) "wojsko zostaje ok. 108 tys., gospodarke dociagamy do niego"; (2) "plon wsi zamkowych wiezie chlop sam na targ miasta" | SP:203-204 | 5.5, 5.11 | (1) wiazace; (2) zmienione 09.10 tylko dla jedzenia podzamcza (Q2) |
| 06.10 pkt 5 | tarcza zoldu w kasie miasta (`TownWageShield`) | CHANGELOG.md:425 | 5.1 | w grze; do przegladu z 111' (KL:523 "martwy kod") |
| 08.10 11:50 | gracz-wasal z lennem placi koronie jak AI; **dwor graczowi nie**; zalogi AI w pokoju o polowe; karczmy z pula starych zolnierzy | SP:710-712, OB:586-593 | 5.2, 5.7 | wiazace |
| 08.10 (noc) | zlecenie kiesy ludu: "ludzie zarabiaja, a miasto i lordowie pobieraja tylko podatki albo wynagrodzenia" | KL:6-9 | 5.5 | podstawa KL |
| 08.10 12:15 | "jak znika to zamykamy, ma byc logiczny system ekonomii" (BEE wariant b) | SP:723 | G | 170 w grze (akcje gracza BEE wylaczone) |
| 09.10 (KIESA) | (A) glod ma skutki - TAK (tydzien bez stac na jedzenie -> zadowolenie BK -> dobrobyt; nie w pierwszych 120 dniach; osobny wylacznik); (B) dziesiecina - TAK (5% utargu wsi, 1-3% dochodu ludzi; polowa jalmuzna, polowa budowa septow) | SP:763-765 | 5.9, 5.10 | zdecyd. |
| 09.10 (C) | **podatek wojenny korony od ludzi - TAK** zamiast ok. 5% mniej wojska AI; warunek: "im wyzsze podatki, tym wplyw na gospodarke i dobrobyt" (najpierw zbytki, potem odziez, jedzenie na koncu; obciazenie ponad znosne -> zadowolenie BK; z glodem -> niepokoje) | SP:766-771 | 5.6 | zdecyd. |
| 09.10 00:10 | lista A-L: A TAK, B TAK, C "mniej wojska AI NIE" -> podatek wojenny | SP:771-772 | 5.6, 5.9, 5.10 | zdecyd. |
| 09.10 03:45 | 11 statki rozbitych do portu - TAK; 13 bez minimow okupu; **14 miasto bez wojska ubozeje - TAK**; **15 obciazenie wsi: "bierze 30-50%, a nie 2/3" -> cel 30-50% (pan + sept), 2/3 tylko przejsciowo** | SP:812-813 | 3.3, 5.5, 5.12 | zdecyd. |
| 09.10 03:55 | "(15) obciazenie wsi docelowo 30-50% - OK" | SP:827 | 5.5 | potwierdzone |
| 09.10 04:25 | **E wyrownanie dla panow zamkow (1/2 nadwyzki, renty), takze u gracza - TAK; F targ przy zamku - TAK; G BetterEconomy: prawdziwa wplata do kasy miasta + dar dla notabli - TAK** | SP:844 | 5.7, 5.11, G | zdecyd. (PLAN dalej "Ty") |
| 09.10 04:55 | Q2: woz wsi zamkowej najpierw sprzedaje zamkowi jedzenie dla podzamcza na 10 dni, reszta do miasta - TAK (razem z kiesa ludu, etap 5); Q3b: cala kiesa rozbitej karawany dla zwyciezcy | SP:852-855 | 5.11; 3.2 | 5.11 zdecyd.; Q3b zrobione w 3.2 (WGRANIE 25) |
| 09.10 08:05 | "bede gral, jak skonczymy projekt" - Jeff nie gra do konca, potem nowa kampania; paczki pod nowa kampanie | SP:888 | NK calego etapu | wiazace (KL 5.3 "stary zapis" drugorzedny) |
| 09.10 10:45 | "4% mniej wojska - NIE; ma byc dynamiczne wedlug tego, czy stac" | SP:907-911 | 5.5, 5.6 | wiazace (przeczy KL 7.3) |
| 10.10 (WGRANIE 23) | progi etapu 2 "DECYZJA": regulator kas miast (zostaje do etapu 5) | SP:1064 | 5.1 | informacja |
| 11.10 02:45 | **(1) statki 3.3 razem z etapem 5 (podatek wojenny 184); wylaczniki ShipsToPort / ShipTradeThroughPort / ShipPrizesToPort OFF do etapu 5** | SP:1133 | 3.3 -> 5.6 | zdecyd. |
| 11.10 02:45 | (5) podatek BK od warsztatow lordow/gracza w cudzych miastach placi wlasciciel panu miasta; napitki i drewno wojska z sakiewek zolnierzy | SP:1139-1140 | (E3:697 odkladal do etapu 5) | zrobione w D11 (WGRANIE 30) - wykreslic z listy etapu 5 |
| 11.10 03:05 | **zasada testow**: kod kilku paczek -> jeden test 120 dob + zapis -> jedno wgranie | SP:1150-1152 | caly etap 5 | wiazace |
| 11.10 09:30 | E4/8 P2 miasta dopelniane ze wsi do udzialu miejskiego w ok. rok (`TownMigrationRefillDays` 364); E4/10 rekrut t1-2 do domu, karczma od t3 | SP:1173-1176 | liczba mieszczan dla 5.5; zloto karczmy | zdecyd. (etap 4) |
| 11.10 11:20 | Jeff: "czy mozesz zaczac etap 5 czy dopiero po etapie 4" - odpowiedz: projekt teraz, kod porcji niezaleznych rownolegle, porcje z jedzeniem/karczma po E4 | PRZ:778-779 | caly etap 5 | w toku (ten workflow) |

Zasady ogolne, ktore wiaza etap 5 (z zadania i dokumentow): zamknieta ekonomia "nic z niczego, nic w nicosc"; jedna regula dla gracza i AI; jedna zasada na jedno zjawisko;
wojska nie zmniejszamy (C, 10:45); kazda paczka z wylacznikiem MCM; rok 364 doby; napisy w grze po angielsku; Z8 - nikt nie zarabia na wlasnym wydatku (E2:147-148).

---

## 2. Pozycje 5.1-5.13: zrodla, stan, co sie zmienilo

| # | Co (PLAN:154-166) | Status PLAN | Stan faktyczny | Zrodla projektowe | Uwagi po etapach 2-4 |
|---|---|---|---|---|---|
| 5.1 | Kasy miast (111'): bez kasowania i "zakupow z niczego"; najpierw z liczona dosypka | zdecyd. | zdecyd. | OB:166, :169, :420-422, :528, :536; P111 (kod na galezi `paczki/111-k6-kasy-miast`, lancuch 102-114 z 06.10, nigdy w grze) | kod 111 trzeba przeniesc na dzisiejsza baze (jak 110/112 w etapie 2). Pomiar z31-A d120 (jedna doba): "zakupy" mieszkancow +425 tys., regulator dosypal 20 tys. / skasowal 248 tys. (log `Armoury-2026-10-11_08-09-11.log`:31722). Po E3 regulator kasuje +160 tys./d wiecej (SP:1110). |
| 5.2 | Dwor pana pelny (162) | zdecyd. | zdecyd. | OB 6.1; KL:96 (80% kupcy / 20% lud), E2:356-406 (162m minimalny + tarcza dworu w grze) | graczowi dworu nie (SP:710). "Pelny" = dwor kupuje prawdziwy towar i 20% do ludu (KL tab. 3 w. 5). |
| 5.3 | Markietani (163) i notable (164c): zolnierze wydaja w polu, nowy notabl bez 10 tys. z niczego | zdecyd. | zdecyd. | OB:171 (10% sakiewki/d w polu: 85% miasto, 15% wies), OB:528; E3:698 (pasmo notabli, kapital karawan notabli 105.6 tys./d z niczego) | z31-A d120: pasmo notabli 69 tys., wyplata dochodu notablom 128 tys. z niczego (log:31718). Od etapu 2-4 sakiewki placa juz odziez (150), napitki i drewno (D11), dozbrajanie (K1) - markietani nie moga liczyc tego drugi raz. |
| 5.4 | Drobne kas: oblezone miasto, prog podatkow zamku, 10 miast bez placenia panu | zdecyd. | zdecyd. | "10 miast" = EKONOMIA-FUNDAMENT P6/K12 (:161-164, :860; Lotus Bay, Ifequeveron - kasa trzymana ponizej progu renty 20 000) | zrodla "oblezone miasto" i "prog podatkow zamku" nie znalezione grepem w docs - do ustalenia w projekcie (PLAN nie ma listy zrodel, choc :3 ja zapowiada). |
| 5.5 | Kiesa ludu (173): koszyk za swoje, podatki z czynszem ok. 9%, wies zatrzymuje 30% utargu, prawa zamku osobno - jedno wgranie z 5.6 i 5.7 | zdecyd. | zdecyd. | KL caly; A15 Z15-0 (:272), Z15-4 (:276) | **wies 60%, pan 35%, sept 5%** (decyzja 15); KL liczy pana 70% (KL:35-36, :542-543) - nieaktualne. Nazwa w KL "172" -> 173. Liczba ludzi: KL z BK `PopulationData` (KL:403), dzis ksiega ludzi (PeopleBook) - jedna liczba. |
| 5.6 | Podatek wojenny korony (cel ok. 100 tys./d w wojnie): im wyzszy, tym wolniej rosnie dobrobyt | zdecyd. | zdecyd. + statki 181 w tym samym wgraniu (SP:1133) | SP:766-771; E2:787-794 (znosny 3-5% gotowki rocznie = 32-54 tys./d, 6% = granica buntu ok. 65 tys.; do 3% mniejsze zakupy, 3-6% ciete zbytki i sukno, > 6% przez kwartal - zadowolenie BK i ucieczka); E2:312 (miejsce w 165); A15:279 (Z15-7) | **sprzecznosc liczb**: PLAN "ok. 100 tys./d" (A15:279) a E2:792-793 "potrzebne 30-60 tys./d" i E3:532 "32-54 tys./d". Dzis danina wojenna 1% kas miast / 1.5% kies wsi (`LaySubsidy*`, Settings.cs:763-766) - 184 ma ja zastapic albo rozszerzyc (jedno zjawisko = jedna regula). |
| 5.7 | Wyrownanie: pan miasta 1/2 nadwyzki kupcow; renty miasto 2, zamek 1, wies 0.25 | **Ty** | **zdecyd. 09.10 04:25 (E TAK, takze gracz)** | A15 Z15-5 (:277), Q1 (:351-359), tab. :245-248; ES:166-168; E2:518, :888 | **zamek 1.5**, nie 1 (A15, ES, E2:888). Dzis w kodzie 3 / 1 / 0.25 (`CrownRentWeight*`, Settings.cs:885-887), zawor miast 7% (`TownRentShare` :642). PLAN E (:250) "9% / 17%" to liczby sprzed A15 v2 - jest -14% / -32% bez wyrownania, -1% / -4% z Z15-5 (A15:245-247). |
| 5.8 | Korona bez bezpiecznika, kasy miast bez dosypki - gdy reszta < 10 tys. przez 28 dob | zdecyd. | zdecyd. | OB:154 (Z9), :255, :363 (bezpiecznik korony: dopelnia kase < 50% zapasu, najwyzej 2% zapasu/d), :420-422 (tryb 1 -> tryb 2) | warunek reszty spelniony juz w etapie 3 (SP:1107-1108, :1116-1117), ale po kiesie ludu trzeba go zmierzyc od nowa (KL:346-347 - obieg z kiesa ludu jest czulszy na dziury). |
| 5.9 | Glod ma skutki (nie w pierwszych 120 dniach) | zdecyd. | zdecyd. (A, SP:763) | KL:428-430 (Afford_jedzenie < 0.95 przez 7 dob -> `EconomicData.UpdateSatisfaction(Food, -0.0015)`), KL 7.1 | **jedna definicja glodu z E4/6** (Famine.cs, galaz w-toku/e4-6 0cd60f1): E4:456-457. 5.9 = brak pieniedzy na jedzenie; E4/6 = brak jedzenia w spichlerzu. Tez RBL glod miasta (-2 dobrobytu) i BK `BKFoodModel` (E4:456). |
| 5.10 | Dziesiecina: polowa jalmuzna, polowa budowa septow | zdecyd. | zdecyd. (B) | KL:225-226 (tab. w. 26), KL 7.2 | **sprzecznosc kolejnosci**: PLAN "po roku testu 5.5" i KL:536 ("po roku autotestu"), a A15 liczy sept 5% od razu w 30-50% (A15:9, :239 `temples=True`, :272). Przy zasadzie testow zbiorczych (SP:1150) prosciej z 5.5. |
| 5.11 | Targ przy zamku: wsie najpierw sprzedaja zamkowi jedzenie na 10 dni | **Ty** | **zdecyd. 09.10 04:25 (F) i 04:55 (Q2)** | A15 Z15-6 (:278), Q2 (:361-366) | "cena wsi" wymaga osobnego mechanizmu; przed wdrozeniem sprawdzic Z8 gracza (A15:278, :366). Zmienia decyzje 05.10 (SP:204) tylko dla jedzenia podzamcza. |
| 5.12 | Wiekszy udzial wsi w dochodzie pana, mniej z nadwyzki miast | zdecyd. | **do skreslenia** | A15:289 (D-12 traci warunek po decyzji 15, zastepuje go Z15-5); E2:888 ("5.12 skreslic") | po decyzji 15 wsie placa panom mniej, nie wiecej. |
| 5.13 | Napis w menu miasta: czy ludzi stac na chleb (napis po angielsku) | zdecyd. | zdecyd. | ES:205 (pyt. 13: "najpierw tylko liczyc, gdzie ludzi nie stac"), KL:396 | po roku testu 5.5 (PLAN). |

---

## 3. Rzeczy odlozone do etapu 5, ktorych nie ma na liscie 5.1-5.13

| Co | Skad | Uwagi |
|---|---|---|
| Statki 181 (E3/10-12): wlaczenie ShipsToPort, ShipTradeThroughPort, ShipPrizesToPort | SP:1133 (Jeff 11.10), E3:527-543, :696; JD:48; Settings.cs:1257-1258 | w kodzie, OFF. Bez wyrownania po roku wojsko -9..-11%, 12-19 biednych glow (E3:539-540). Z31-A d120: "statki NavalDLC inne" 371 tys./d z niczego (log:31718). |
| Rabat AI 0.01 przy kupnie statku (181d) i wycena A | E3:705 | niezgodny z "jedna regula"; z 184 i K6. |
| BK "rynek osady" `HandleMarketGold` (-82 tys./d netto) | E3:700, OB:179 | jedno zamkniecie razem z K6 (111'). |
| Tarcza zoldu dla sakiewek karawan | E3:701 | z K6; zmienia budzet 166 (+100-150 tys./d dla panow miast). |
| Bezpiecznik korony dla kas miast | OB:255, :363, :530 | to drugi bieg 5.8. |
| BEE G: prawdziwa wplata gracza do kasy miasta/zamku + "dar dla notabli" | SP:844; tools/bee/OPIS.md:83-91 | dzis te akcje sa szare (170, CHANGELOG.md:7). Brak pozycji w PLAN - dopisac (najblizej 5.1/5.3). |
| Sakiewka weterana do kasy miasta; "do kasy miasta karczmy" (OB 6.2) | E410:56, :60, :386-387 | po E4/10 do kas miast ok. 30-37 tys./d mniej za najem. |
| Haki kiesy ludu "z wartoscia 0" w paczkach etapu 2 (110: 15% zaworu dla podzamcza, 112: renta ustepuje oplatom, 162: 80/20 lud) | E2:151-152 | sprawdzic w kodzie, ktore naprawde sa (slad: komentarz `PopulationLaw.cs:1173` w drzewie e4/bledy). |
| Podatek warsztatow lordow w cudzych miastach | E3:697 | **nieaktualne** - zrobione w D11 (SP:1139, WGRANIE 30). |

---

## 4. Nieaktualne albo sprzeczne miejsca (do poprawy w projekcie etapu 5)

1. PLAN:160, :164 - 5.7 i 5.11 "Ty": Jeff odpowiedzial 09.10 (SP:844, :852). PLAN:0 tabela "Ty 6" tez nieaktualna. (E2:889)
2. PLAN:160 - "zamek 1" -> 1.5 (A15:277, :351; ES:168; E2:888).
3. PLAN:158 i ES:67 - "wies zatrzymuje 30% utargu" -> 60% (pan 35% + sept 5%, A15:272).
4. PLAN:165 - 5.12 skreslic (A15:289, E2:888).
5. PLAN:250 - "bez niego panowie zamkow traca ok. 9% ludzi w wojnie i 17% w pokoju" -> A15 v2: -14% / -32% (A15:10, :245).
6. PLAN:168 "Skonczone, gdy": "mediana kiesy panow zamkow nie spada o wiecej niz 10% na 28 dob" -> -2% na 28 dob + udzial panow zamkow w D swiata >= 30% (A15:341-342,
   E2:890); "wojsko jak w 2.7" -> 95-115 tys. w wojnie, podloga 85 (E2:882); "ich udzial w stalym dochodzie 30-35% (dzis 24%)" - zgodne z A15.
7. PLAN:159 "cel ok. 100 tys./d" a E2:792-793 i E3:532 "30-60 / 32-54 tys./d" - do jednej liczby w projekcie (z rachunku po etapach 2-4).
8. KL:47, :393, :398 - paczka "172 KIESA LUDU" -> 173 (172 to paczka strzal z 08.10, SP:743).
9. KL:35-36, :542-543 - "pan bierze ok. 2/3 gotowki wsi, stawka BK 70% zostaje" -> decyzja 15: 30-50% (SP:813); ES:206 (pyt. 14 "zostawic 2/3 do budzetu rodu") tez nieaktualne.
10. KL:41, :537-538 (pytanie 7.3 "mniejsze wojsko AI - zgoda?") -> odrzucone (C, SP:771; 10:45 SP:909); zamiast tego 184.
11. KL:14, :387-396 - kod 2e235ea i kolejnosc "grupa C" (110, 111', 162, 112, 163, 114, 164c + 172) - 110, 112, 114, 162m, 165, 166, 168 sa juz w grze (etap 2); 111', 162 pelny,
    163, 164c przeszly do etapu 5 (E2:807, :891). Numery linii w KL:445-469 do odswiezenia.
12. KL:260, :474-477 - start kies ludu z "przycinanego daru startowego miast (6.72 mln), ktory 110/111 i tak kasuja" - 111 nie jest w grze; zrodlo zapasow startowych
    do sprawdzenia (dar startowy kas 12 mln, OB:186).
13. KL:69-72, :391, :403 i pomiar A (ludnosc BK) - dzis jest ksiega ludzi etapu 4 (PeopleBook, E4/0, E4/4); miasta "zamrozone" (`TownPeopleFrozen`, Settings.cs:706) do E4/8.
14. KL:183-190 (zold, place, ceny) i KL 4.x (rachunek) - na stanie sprzed 169 i etapow 2-4 (rody 83 mln, wojsko 95 tys.); dzis wojsko lordow d120 110.4 tys. (SP:1185).
    Rachunek do powtorzenia (pliki KL rozdz. 8 leza w scratchpadzie innej sesji `7016f733...\kiesa\`).
15. PLAN:27, :154 "zakupy mieszczan ok. 450 tys." / E3:699 "ok. 560 tys." / OB:166 "390 tys. (rok)" / z31-A d120 425 tys. - rozne biegi; w projekcie brac srednia 28 dob z z31.
16. OB:169 "regulator kasuje 194 tys., dosypuje 71 tys." - z31-A d120: kasuje 248 tys., dosypuje 20 tys. (jedna doba).
17. E3:697 (podatek warsztatow do etapu 5) - zrobione w D11.
18. Kalendarz: brak liczb na 168 dni w zrodlach etapu 5 (wszystkie licza 364; KL:16, A15:39, P111:7). AUDYT-2026-10-05:924, :1085 licza na 168, ale nie dotycza etapu 5.

---

## 5. Co czeka na Jeffa

**Z PLAN - nic.** 5.7 i 5.11 odpowiedziane 09.10. Projekt moze isc bez pytan.

Do potwierdzenia tylko, jesli projekt zmieni liczby wobec tego, na co Jeff sie zgodzil (moja ocena, nie decyzja Jeffa):
- waga zamku 1.5 przyjeta razem z E (Jeff odpowiadal na pytanie A15 Q1 z waga 1.5) - nie pytac ponownie, chyba ze nowy rachunek da inna wage;
- dziesiecina od razu z 5.5 (jak w rachunku A15) czy po roku testu (PLAN, KL) - decyzja projektu, Jeff dal tylko "tak" bez terminu (SP:765);
- stawka podatku wojennego - Jeff dal kierunek ("im wyzsze, tym wplyw na gospodarke"), liczby dobiera projekt (E2:792-794);
- gdyby kiesa ludu mimo 184 zmniejszala wojsko ponizej 95 tys. - to wymaga Jeffa (C i 10:45: bez stalego spadku).

Pytania kierunku z 11.10, ktore NIE dotycza etapu 5: E4/6 uchodzcy, E4/7 najemnik z karczmy, niewzieci z poboru (PRZ, commit 990dae7).

---

## 6. Czy etap 5 mozna zaczac przed koncem etapu 4 (wedlug dokumentow)

Zaleznosci z PLAN:154-166 wobec stanu gry:
- 5.1 <- 3.1 (w grze), 5.2 <- 2.8 (w grze, 162m), 5.3 <- 3.1 (w grze), 5.6 <- 2.3 (w grze), 5.7 <- 2.3 (w grze), 5.5 <- 2.1 (pomiar ludnosci - zastapiony ksiega ludzi etapu 4) i 5.1/5.6/5.7;
  5.8 <- 5.5; 5.9 <- 5.5; 5.10/5.13 <- rok testu 5.5; 5.11 <- 5.5; 5.4 <- 5.1.
- Warunki KL (szczelnosc, reszta < 10 tys., wzrost ludnosci < 2%/rok) - spelnione (pkt 0.6).
- Styki z etapem 4 w toku: E4/6 (glod, spichlerz miasta - 5.9 i koszyk jedzenia), E4/8 (liczba mieszczan, migracja, karawany z ludzi - podstawa kiesy), E4/10 (zloto karczmy
  do kas miast). Zgodnie z PRZ:778-779: projekt etapu 5 teraz; kod porcji niezaleznych (5.1 przeniesienie 111', 5.2, 5.4, 5.7, 5.12 skreslenie, statki/184 bez kiesy - nie,
  bo 184 placi kiesa ludu, E2:789) mozna pisac rownolegle; porcje zalezne od jedzenia i karczmy (5.5 koszyk, 5.9, 5.11, 5.3 weterani/sakiewki) - po scaleniu E4.
- Zasada testow Jeffa (SP:1150): etap 5 jako jeden albo dwa zbiory kodu z jednym testem 120 dob + zapis; PLAN wymaga potem dwoch testow rocznych (PLAN:168).
