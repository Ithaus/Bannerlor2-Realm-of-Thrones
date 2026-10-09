# AUDYT 12 - DOCHODY RODOW: czy sa odpowiednie, gdzie sie gubia i co poprawic (09.10.2026, wersja 2 po krytyce)

Prosba Jeffa 09.10: "zaudytuj dochody, czy sa odpowiednie" - w kontekscie fali bankructw, ktora dalej rusza (test 120 dob: 15 bankrutow,
31-32 glowy rodow ponizej 5 000 zl, skarbce Polnocy i Krolewskiej Przystani puste ok. doby 104-109).

Status: **TYLKO ODCZYT.** Nic nie zmienione w kodzie, w grze, w ustawieniach ani w zapisach; gra nieuruchamiana; nic nie wypchniete.
Jedyny zapisany plik w repo to ten raport. Rachunek (Python) i jego wyniki leza w katalogu sesji (rozdz. 7).

**Wersja 2:** dwie krytyki (32 uwagi) sprawdzone w kodzie, logu i rachunku; 29 przyjetych w calosci albo czesciowo, 1 odrzucona w glownej tezie -
rozdz. 8 "Krytyka i odpowiedzi". Najwazniejsze zmiany wobec wersji 1: paczka 110 oddaje panu **cala** nadwyzke zamku (nie 2/3); kasa zamku
oddaje dalej ok. 44% tego, co dostaje; ok. 90% "dochodu" z zaworu zamku to wlasny zold zalogi pana, wiec nie moze podnosic jego budzetu;
lordowie placa za jedzenie wojska; kontrakt najemnika nie moze rosnac z jego zoldem; wynik podany takze wedlug krolestw, na 2 lata i od doby 120.

Na czym stoi:
- **pomiar** - `7016f733...\scratchpad\dochody\pomiar.md` (314 rodow AI, test T9 120 dob);
- **kod** - `...\dochody\kod.md` (kazde zrodlo i wydatek rodu, U1-U16, P1-P12) + kod sprawdzony przy tej wersji (rozdz. 7);
- **historia** - `...\dochody\historia.md` (Anglia/Francja 1300-1450, lore GoT, H-1..H-7);
- projekt `docs/PROJEKT-EKONOMIA-OBIEG-2026-10-08.md` (rozdz. 5-8, 12), raporty 01, 04, 05, 09, 11, decyzje Jeffa w `docs/STAN-PRAC.md`;
- **rachunek v2** - `...\dochody\sim\sim_dochody2.py` + `run_all.py` -> `wynik-v2.txt`; `gracz2.py` (Z8 dla gracza). Wersja 1 (`sim_dochody.py`) zostaje do porownania.

Dane: test 120 dob wersji z gry (Armoury 0f8a80b0 + CrashScribe 269cc980), kopia `...\scratchpad\kopiaT9-120\` (log Armoury, `budzet-rodow.csv`,
`economy-2026-10-08_22-25-23.csv` - kopia z CrashScribe, 9 248 953 B). Srednie "na dobe" to doby 31-120 (wojna u 94-97% rodo-dob), chyba ze napisano inaczej.

Oznaczenia: **[K]** kod (plik:linia), **[P]** pomiar, **[H]** historia/lore ze zrodlem (adresy w `historia.md` rozdz. 8 i raporcie 09; **[H?]** - zrodlo
z innej epoki albo z drugiej reki), **[S]** szacunek/rachunek (rzad wielkosci, +-30%). Przelicznik (raport 09): **1 zl gry = 1 pens ok. 1300**;
1 L rocznie = 0.66 zl na dobe; rok = 364 doby. Uwaga do historii: wiele liczb dochodow pochodzi z 1436-1527 (po Czarnej Smierci place ok. 2 x wyzsze,
renty panow nizsze) - proporcje z mieszanych epok oznaczam [H?], a pasma rang moga sie przesuwac nawet ok. 2 x.

---

## 0. Dla Jeffa (prostym jezykiem)

1. **Krolowie i panowie miast zarabiaja dosc** - sama ich ziemia placi cale ich wojsko.
2. **Fala bankructw to panowie samych zamkow** (13 z 15 bankrutow): z wsi maja ok. 640 zl dziennie, **zamek nie daje im nic**, a trzymaja ok. 520 ludzi
   za ok. 2 000 zl dziennie; dziure lata zloto z niczego (glownie wyplata za statki rozbitych i rozwiazanych druzyn) i zwrot korony z kurczacych sie zapasow.
3. **Przyczyna to glownie za duzo wojska przez caly rok, nie za maly dochod:** same paczki 165/166/168 zbijaja liczbe rodow na minusie w rok z ok. 75-99
   do kilkunastu, same poprawki dochodow - tylko o ok. 1/3.
4. Krytyka znalazla w moim rachunku bledy, ktore poprawilem: paczka 110 oddaje panu cala nadwyzke zamku, zamek wydaje dalej prawie polowe tego,
   co dostaje, prawie caly "dochod z zamku" to wlasny zold zalogi pana, ktory do niego wraca, a lordowie musza tez kupowac jedzenie dla wojska.
5. **Po poprawkach pierwsza grupa** (165 + 166 + 168 + zamek oddaje panu nadwyzke + wies nie gubi utargu + skromny dwor) daje w nowej kampanii
   **zero rodow na minusie**, wojsko w wojnie **ok. 4% mniejsze niz dzis**, w pokoju **ok. 36% wojennego** (Twoje 35-40%) - ale przy udziale pokojowym
   0.28, a nie 0.22, jak pisalem.
6. W kampanii, ktora juz trwa, ok. 7 rodow (glownie dzisiejsi bankruci: Royce, Harlaw, Volentin, Marbrand) zostaje na minusie - po paczce 168 to zajecie
   dochodu wsi przez wierzyciela, nie bankructwo.
7. **Najemnik dostaje staly kontrakt ustalony przy najmie** (zold + jedzenie + sprzet), a nie "zold + 15%" od biezacego wojska - tamto bylo zarabianiem
   na wlasnym wydatku i kompanie by topnialy; Ciebie to nie dotyczy (zostajesz na kontrakcie gry).
8. **Uczciwie wobec Twojej decyzji C ("nie" dla 5% mniej wojska AI):** caly projekt, po zamknieciu zlota z niczego, daje w wojnie ok. 9% mniej wojska AI
   niz dzis; zatwierdzony podatek wojenny musi dac koronom ok. 100 tys. zl dziennie (wtedy -4%), a ok. 200 tys. - zeby bylo jak dzis.
9. **Biedne krolestwa beda wyraznie slabsze:** Zelazne Wyspy ok. -29% w pierwszej grupie i ok. -46% po calosci, Sarnor i Smocza Skala po calosci ok. -30%,
   Polnoc ok. -22-24%; Straz i Dothrakow ratuja dary znane z lore (Polnoc dla Strazy, Wolne Miasta dla khalasarow) - to pytanie Q4.
10. Dwie rzeczy nazywam wprost: **wojna caly rok to wybor gry** (wojsko kosztuje pana w roku 2-8 razy wieksza czesc jego dochodu niz pana historycznego, ktory trzymal je 40-80 dni), a **renty korony dla panow**
    to mechanizm gry, ktory oddaje do obiegu nadwyzke skarbca - historycznie dostawali je tylko wybrani magnaci.
11. Cztery pytania do Ciebie sa w rozdz. 6: oddzialy rycerzy BK, statki rozwiazanych druzyn, renty korony takze dla Ciebie, slabsze biedne krolestwa.

---

## 1. Grupy rodow: dochod w grze, dochod historyczny, wydatki, wojsko, werdykt

### 1.1 Tabela glowna

Liczby gry [P]: srednio na rod i dobe (doby 31-120), D = mediana z doby 120 (ksiega 169). "Jednorazowe" = zdarzenia spoza rodu (lup, jency, statki,
majatki BK) + kasy partii (przelew syn -> ojciec, nie nowy pieniadz). "Wojsko powinien" = rachunek v2: **E2** (poprawiona pierwsza grupa) i **S3**
(caly projekt), rok wojny / rok pokoju, nowa kampania (rozdz. 5). Ranga historyczna - tylko w monecie (sposob A z `historia.md`), bo ranga "na glowe"
stoi na tabeli ludnosci, ktora rosnie bez sensu o 37% w 120 dob.

| Grupa (rodow) | Dochod w grze: D i skladniki [P] | Dochod historyczny w monecie [H] | Wydatki w grze [P] | Wojsko dzis / ile powinien [S] | Werdykt |
|---|---|---|---|---|---|
| **Krol** (29) | D mediana **10 486**. Ziemia 5 652 (podatek wsi 787, cla i resztki podatku miasta "Walled Demesnes" 391, renty wsi + zawor miasta 4 474), zwrot korony 2 362 (21%), jednorazowe 3 025 (**26%**), trzecia 370. Kiesa rodziny 354 tys. -> 222 tys. | rod krola = wielki magnat: Lancaster 1313-14 ok. 7 250, Jan z Gandawy 5 300-6 600; zwykly dochod Edwarda III 19 780 na cala Anglie | zold 5 237 (partie 3 884, zalogi 1 353), karawany 170, sprzet/handel/werbunek ok. 4 929 | dzis **1 149** na rod; ziemia / zold 1.08. E2: wojna ok. **1 200**, pokoj ok. **480**; S3: 1 130 / 450 | **OK.** Klopot tylko u kroli z mala ziemia (Stane, Greyjoy - zold 2 x D, Straz, Stannis) - za duzo wojska, nie za maly dochod |
| **Pan miasta** (71) | D mediana **8 946**. Ziemia 5 243 (podatek wsi 615, "Walled Demesnes" 185, renty + zawor 4 442), zwrot 2 020, jednorazowe 2 297, trzecia 135. Kiesa 238 tys. -> 320 tys. | hrabia / magnat 880-7 250 (Salisbury 880, Warwick 3 956, Gandawa 5 275-6 593) | zold 4 489 (3 165 + 1 325), karawany 151, reszta 3 909 | dzis **1 056**; ziemia / zold 1.17. E2: **1 090 / 440**; S3: 1 020 / 410 | **OK, nawet hojny.** Warunek: kasy miast dostaja dzis 445 tys./dobe "zakupow z niczego" przy zaworze do panow 477 tys. - po ich zamknieciu (164) zastapi to dwor (162) i zolnierze (163) |
| **Pan zamku** (118) | D mediana **2 095** (wplyw srednio 3 506). Ziemia srednio **641**, mediana **319** (podatek wsi 438, renta wsi 203; **zamek 0**), zwrot 917 (26%), jednorazowe 1 810 (**52%**: zdarzenia 1 669 = 48%, z nich ok. 86% z niczego; kasy partii 141 = przelew w rodzie), trzecia 127. Kiesa **205 tys. -> 67 tys.** | baron 66-330, par srednio 570 [H?], nowy hrabia 879 | zold **1 976** (1 517 + 459), karawany 120 (zysku nie oddaja), rada 44, reszta 1 351; saldo **-215/dobe** (mediana -586) | dzis **517** na rod; ziemia / zold 0.32 (mediana 0.20; 113 ze 118 < 1). E2: **435 / 110**; S3: 420 / 135 | **W monecie na poziomie barona-para (srednia) albo barona (mediana) - to w porzadku dla jego rangi, ale za malo na jego wojsko.** Dochod zle zbudowany (zamek 0, polowa wplywu z niczego, 1/4 z zapasu korony), wojska 3 x tyle, ile placi ziemia, przez caly rok. 13 z 15 bankrutow |
| **Rycerz BK** ("gentry", 85; jeden majatek, bez lenna) | D mediana **99** (bez podwojnie liczonego majatku **52**). Majatek 45 (produkcja **z niczego**), zwrot 58, jednorazowe 37. Kiesa 40 tys. -> 26 tys. | giermek 13-26; rycerz "mniejszy" 26-66 [H?] (1436) | zold 116, reszta 173 | dzis **24** ludzi (48 z 85 ma druzyne); E2/S3: **0-10** | **Dochod OK dla giermka albo ubogiego rycerza** (po zamknieciu produkcji z niczego zostaje ok. 20-30 [S]); **oddzialu z tego nie utrzyma** - pytanie Q1 |
| **Najemnik** (11: 5 wolnych kompanii z lore + 6 frakcji pomniejszych, rozdz. 4.1 D-6) | D mediana **793**. Model 850 (kontrakt gry tylko 100, dochod "za tier" z niczego ok. 450, przelew w rodzie 295), jednorazowe 1 589 (**64%**). Kiesa 270 tys. -> 544 tys. | kompania kondotiera: Hawkwood 590-7 900 z kontraktow miast (tylko dla wolnych kompanii) | zold 921 | dzis **171**. E2/S3 (staly kontrakt): wojna **171**, pokoj **93** ("w oczekiwaniu", polowa) | **Staly dochod za maly, bogaci sie z lupu z niczego** - kontrakt staly z dnia najmu (D-6) |
| Rod bez lenna (inny niz rycerz i najemnik) | na starcie testu 0 rodow; 5 stracilo lenno w tescie | domownik pana / rycerz na umowie | - | budzet 166 daje 5-15 ludzi | **OK** w projekcie (H-6) |
| Pan samej wsi | brak w tescie | wies daje ok. 320/dobe [P] = **baron** (66-330) | - | - | jak baron; bez osobnej reguly |

### 1.2 Co daje jedno lenno [P]

| Lenno | Pan dostaje dziennie | Z czego | Uwagi |
|---|---|---|---|
| Wies | ok. **320** (podatek wsi ok. 220 + renta wsi ok. 100) | utarg taboru (miasto kupuje plon) i kiesa wsi | podatek wsi spada w wojnie: pan zamku 403 (doby 1-30) -> 204 (91-120) |
| Miasto | ok. **4 100-4 600** (zawor 7% nadwyzki kasy) | kasa miasta | kasa zasilana dzis w 40-90% "zakupami z niczego" (B15) |
| Zamek | **0** (116 rodow z zamkiem razem: 11 zl/dobe) | - | po 110: **ok. 300 zl/dobe**, z czego ok. 90% to wlasny zold zalogi i dwor pana wracajacy przez kase zamku; cudzych pieniedzy ok. 30-35 zl (rozdz. 3.2) |

### 1.3 Struktura dochodu wobec historii [P][H]

- Historycznie pan swiecki zyl w ok. 3/4 ze wsi (czynsze, folwark, mlyny - Campbell), miasto dawalo mu kilka procent. W grze odwrotnie: z kas miast
  idzie do panow **477 tys.** zl/dobe, ze wsi **39 tys.** (renta wsi) + podatek wsi. Dlatego rod bez miasta tonie. Porownanie "na glowe" (wies 0.24 zl
  na mieszkanca rocznie wobec historycznych 10-12) jest **niepewne** - tabela ludnosci rosnie bez sensu, a mapa jest symbolem krainy - i nie moze byc celem
  (rozdz. 4.1 D-12).
- Renta "od ludnosci" jest zawsze ucieta kiesa osady: zaplacono **6.5%** naleznego [P] - wies placi najwyzej 20% swojej kiesy, a kiesy wsi sa puste
  (mediana 76 zl). Panowie juz dzis biora ok. 60-73% gotowki, ktora przechodzi przez wsie (udzial pana w utargu taboru 70% minus majatki + renta) [P][S].
  **W zamknietym obiegu dochod z ziemi wyznacza to, co przechodzi przez kasy osad, a nie liczba ludzi.**
- Korona: zwykly dochod rodu krola ok. 1.6 zl na glowe rocznie (= Edward III); zwrot zoldu 321 tys./dobe przy wplywach skarbcow ok. 151 tys. [P].

---

## 2. Gdzie dochod sie gubi albo jest liczony zle (kod)

Kod = wersja w grze (Armoury 0f8a80b0 = `sklad4\Armoury\src`). Dekompilacje: gra `3cf3e0ac...\ore-supply\cs`, BK `...\bk`, NavalDLC
`3cf3e0ac...\dzien-2\k7\dec\naval`. Paczki 110/114 - galezie w repo (`paczki/110-k5-kasa-zamku` = 39f5bdf, `paczki/114-porzadki` = a14efe8).
Typ: **Z** = zloto z niczego, **N** = w nicosc, **R** = zly rachunek (nic nie powstaje ani nie znika, ale ktos dostaje albo placi za duzo).

| # | Co | Gdzie [K] | Ile [P] | Kogo dotyka | Typ | Naprawa (paczka) |
|---|---|---|---|---|---|---|
| **B1** | **Zamek nic nie daje panu, a gra kasuje kase zamku.** Renta Armoury bierze tylko wsie i miasta; regulator gry zdejmuje 25% nadwyzki kasy zamku dziennie; tarcza zoldu chroni tylko miasta. Opis klasy mowi "Zamki bez zmian", a postfiks zeruje tez podatek zamku (ten byl z niczego - zerowanie dobre, zly jest opis) | `PopulationLaw.cs:296`, `:27`, `:157`; regulator `DefaultSettlementEconomyModel.cs:75-79` przez BK `BKSettlementBehavior.cs:673-688`; tarcza `SoldierPay.cs:446, 499` | doba 120 (l. 26118): do kas zamkow wplywa zold zalog **60 130**, sprzet AI **22 964**, "zakupy" z niczego **59 915**; odplywa: inne moduly ticku (do kas miast) **23 280**, karawany **9 068**, odziez **3 293**, tabory **1 193** (razem **44%** doplywu prawdziwego); regulator kasuje **104 977**, dosypuje **3 709** | 118 panow zamkow | **N** | **110** (gotowa): regulator zamku = 0 w obie strony, "zakupy z niczego" w zamku cofniete, zawor 7% nadwyzki ponad zapas - **100% do pana** (`CastlePurse.cs`, 39f5bdf: `GiveGoldAction.ApplyForSettlementToCharacter(st, lord, pay)`); podzial 2/3 pan, 1/3 skarbiec to dopiero klucz z 114 (`CastleDuesSplitWithCrown`, `CastleDuesLordShare` 0.67) - D-1 |
| **B2** | **Zaloga we wlasnym miescie prawie darmowa.** Zold zalogi idzie do kasy jej osady, wraca panu zaworem, a korona zwraca jeszcze 50% zoldu zalog | `SoldierPay.cs:374-388` (zold zalogi do kasy osady `:383`); `Settings.cs:657` (`CrownWageRefundGarrisons = true`); `KingdomTreasury.cs:149-232` | zold zalog do miast 130 tys./dobe, zwrot od niego ok. 65 tys. [S] | panowie miast i krolowie (za dobrze) | **R** | zwrot bez zalog (165); udzial korony w zaworach (114 zamki, K6 miasta) - D-1, D-5 |
| **B3** | **Wyplata za statki druzyny rozbitej na ladzie albo rozwiazanej** - statki najpierw do innych druzyn rodu, reszta zamieniona na zloto z niczego dla glowy rodu; AI dostaje 100% wartosci, gracz z kara sprzedazy | NavalDLC `NavalShipDistributionCampaignBehavior.cs:28-33` (`!party.IsCurrentlyAtSea`), `:42-48` (rozwiazanie), `:57-71` (`GetShipSellingPenalty` tylko dla gracza, `GiveGoldAction(null, glowa, wartosc)`) | **ok. 269 tys./dobe**, ok. 53% do panow zamkow (ok. 1 200/dobe = **34%** ich wplywu); pojedynczy rod 300-550 tys. jednego dnia | panowie zamkow, panowie miast | **Z** | statki zostaja w rodzie albo sprzedaz portowi z jego kasy, jedna kara dla AI i gracza (164) - **dopiero po 166** (D-11, Q2) |
| B4 | Lup "z cial", sprzedaz jencow, sprzedaz statkow osadzie, dochod "za tier" | gra `MapEvent.cs:1850-1867`, `SellPrisonersAction.cs:83, 88`, `ChangeShipOwnerAction.cs:19-64`, `DefaultClanFinanceModel.cs:133-137` (gracz wylaczony) | 32 + 18 + 33 + 5 tys./dobe | wszyscy wojujacy, najemnicy | **Z** | 164 i 165 (tier -> kontrakt staly, D-6) |
| **B5** | **Zwrot zoldu korony z zapasu, nie z wplywow** | `KingdomTreasury.cs:191` (`have = KingdomBudgetWallet`) | zwrot **321 tys.** przy wplywach **151 tys.** (doba 120); skarbce -170..-190 tys./dobe | wszyscy w wojnie | **R** | 165 (D-5) |
| **B6** | **15% utargu wsi znika** | BK `EconomyPatches.cs:1099` (`PartyTradeGold = 0`) | **41 tys./dobe** | panowie wsi | **N** | 112 K7 (gotowa) - D-2 |
| B7 | Zakup lorda we wsi: 100% zaplaty w nicosc | gra `DefaultSettlementTaxModel.cs:21`, `SellItemsAction.cs:85-90` | 6 tys./dobe | panowie wsi | **N** | 112 K7 - D-2 |
| B8 | Clo korony kasuje licznik cel i drugi raz bierze z kasy miasta | `KingdomTreasury.cs:279-284` | 11 tys. w nicosc + 11 tys. podwojnie | panowie miast | **N** | D-7 (164/165) |
| B9 | Pensje rady BK od podatku sprzed wyzerowania | BK `BKTaxModel.cs:223-225`; `PopulationLaw.cs:157` | 9.6 tys./dobe (panowie zamkow 44 na rod) | panowie zamkow | **R** | D-8 (164) |
| B10 | Karawany pana bez miasta prawie nie oddaja zysku, zold karawan w nicosc | BK `BKPartyBehavior.cs:941-978`; `SoldierPay.cs:405` | zold 27 tys./dobe, wplaty <= 7 tys. | panowie zamkow, rycerze | **N** + **R** | D-9 (164) |
| B11 | Majatki BK: "produkcja" z niczego | BK `EstateData.cs:106-193`; udzial w utargu (prawdziwy) `:80-104` | **286 tys./dobe**; rycerz BK 45 | notable, rycerze BK | **Z** | D-10 (164) |
| **B12** | **Ksiega D (169) liczy jako dochod rzeczy, ktore nim nie sa**: przelew 10% kiesy syna-dowodcy do ojca, majatek BK dwa razy, zloto z niczego, zwrot ponad wplywy korony; po 110 doszedlby **wlasny zold zalogi pana wracajacy zaworem zamku** (`CastlePurse.cs` dopisuje do `PopulationLaw.RentToday`, a ta do D) | `ClanIncomeBook.cs:290, 299-301` (`inflow = a + b + Today`, `b` = RentToday); `AiGear.cs:178-179` (sprzet zalogi placi pan osady) | przelewy 18-44 tys./dobe; z niczego 25% calego D | wszyscy | **R** | D staly bez jednorazowych i bez wlasnych pieniedzy (166, W-1) |
| B13 | Limit Banku z dochodu modelu jednego dnia x 60 | `IronBank.cs:147, 150` | 83 dluznikow, 3.34 mln | panowie zamkow | **R** | 168 (W-5) |
| B14 | Zapomoga dla biednych rodow nie dziala (BK ja wylacza) | BK `EconomyPatches.cs:242`; `KingdomTreasury.cs:404-406` | 0 | biedne rody | - | 165 musi wyplacac sama (D-5) |
| **B15** | **Zawor miast stoi na "zakupach mieszczan z niczego"** | BK `EconomyPatches.cs:579-651`, gra `ItemConsumptionBehavior.cs:61-71` | **445 tys./dobe** z niczego; regulator kasuje 200 tys., dosypuje 27 tys. | krolowie, panowie miast | **Z** + **N** | 111' + 162 + 163 |
| B16 | Renta wsi: licznik podatku rosnie +37 tys./dobe | BK `BKTaxModel.cs:257-270` | licznik 180 tys. | panowie przy froncie | **R** (opoznienie) | bez zmian |
| B17 | Tytul de iure wsi u kogos innego | BK `EconomyPatches.cs:452-458` | maly | zdobywcy | **R** | 164 (maly) |
| B18 | BEE: wydatki panow w nicosc | BEE `CastleEconomyCampaignBehavior.cs:343-361` i in. | **133 tys./dobe w T9** | panowie zamkow | **N** | **zamkniete skryptem 08.10 23:40** (po T9) - scenariusze bez budzetu licze tez bez tego ujscia (S0b, S1b, S6b) |

Bilans dla rodow w dobie 120 (l. 26121) [P]: wplywy **1.17 mln/dobe** = dochod modelu 185 tys. + renta wsi 39 tys. + zawor miast 477 tys. + zwrot 321 tys.
+ trzecia 5 tys. + wplywy spoza rozliczenia 141 tys. (z niczego 78 tys.); zold rodow ok. 815 tys. (partie 593 + zalogi 195 + karawany 27).
(Suma D z linii "Budzet rodow", 1.47 mln, to srednia 28 dob - inna podstawa.) **Ok. 40% tego, co rody nazywaja dochodem, to zloto z niczego
albo zwrot z zapasu korony** (zdarzenia z niczego, "za tier", nadwyzka zwrotu ponad wplywy, przelewy w rodzie, majatki BK) [S].

---

## 3. Przyczyna bankructw: za niskie dochody, za duze wydatki, czy jedno i drugie?

### 3.1 Pan zamku - rachunek jednej doby (srednio na rod, doby 31-120) [P]

| Pozycja | zl/dobe | Uwagi |
|---|---|---|
| Ziemia (podatek wsi 438 + renta wsi 203; zamek 0) | **641** | trwale (mediana 319) |
| Zwrot polowy zoldu od korony | 917 | z zapasu skarbcow, ktory sie konczy (B5) |
| Zdarzenia spoza rodu | 1 669 | w ok. 86% z niczego (B3, B4) [S] |
| Kasy partii (przelew syn -> ojciec) | 141 | nie nowy pieniadz - zmienia tylko kiese w rodzie |
| Trzecia lorda i inne | 138 | |
| **Wplyw razem** | **3 506** | |
| Zold partii + zalog | **1 976** | 517 ludzi (308 + 209) |
| Karawany, rada, inne linie modelu, powinnosci | 394 | |
| Jedzenie, sprzet, handel, werbunek, raty ("reszta") | 1 351 | w tym zywnosc ok. 250 [S] |
| **Saldo** | **-215** (mediana **-586**) | 83 ze 118 traci kiese |

### 3.2 Skladniki przyczyny

**Wydatki (wojsko) - glowna przyczyna:**
- Zold pana zamku to **3.1 x** jego ziemi (mediana 5 x). Historycznie: stale wojsko w pokoju 10-25% dochodu (Lancaster ok. 20%), swita wojenna
  1.4-2.7 x dochodu, ale **tylko 40-80 dni** i w ok. polowie za pieniadze krola [H]; 5. hrabia Percy wydawal na swity 65% dochodu i popadl w dlugi [H?].
- Wojsko nie zalezy od dochodu [P]: Dorzecze w pokoju do ok. doby 110 rozbudowalo wojsko z ok. 200 do 400-560 ludzi na rod - 5 z 7 zbankrutowalo;
  Polnoc 440 -> 803 ludzi na rod przy stalym wplywie.
- Nadmiar wobec pulapu 166 (0.55 D) [P]: panowie zamkow 15.6 tys. ludzi, panowie miast 6.8 tys., krolowie 4.0 tys. - swiat nie ma za duzo wojska,
  ma je **zle rozlozone**.

**Dochody - przyczyna druga:**
- Zamek 0 (B1). **Ale zawor zamku z paczki 110 nie jest nowym dochodem:** do kasy zamku plynie glownie zold jego wlasnej zalogi (`SoldierPay.cs:383`) i sprzet
  jego zalogi, ktory placi pan osady (`AiGear.cs:179`); kasa oddaje dalej ok. 44% (l. 26118). W rachunku v2 pan zamku dostaje z zaworu ok. **270-310/dobe**,
  z czego ok. **235-280 to jego wlasne pieniadze**, a ok. **33 cudze** (sprzet kupowany w zamku przez cudze druzyny). Zawor robi wiec glownie jedno:
  **zaloga w zamku kosztuje pana ok. 63% zoldu zamiast 100%** (przy kluczu 2/3; przy 110 jak zbudowanej - ok. 44%).
- Wies gubi 15% utargu + prowizje (B6-B7: ok. +130/dobe na 2 wsie), rada od fantomowego podatku (44), karawany (ok. 60-120).
- Dzis dziure lataja zloto z niczego (ok. 1 450/dobe) i zwrot z zapasu (917). Po zamknieciu obiegu (164) i zwrocie z biezacych wplywow (165)
  brakuje ok. 1 800/dobe - **tego zadna poprawka dochodu nie zastapi; zrobi to tylko pulap wojska (166).**

### 3.3 Rachunek: co zatrzymuje fale (rok wojny, nowa kampania, 314 rodow AI) [S]

"Na minusie" = kiesa rodziny ponizej zera w ciagu roku. Kolumna "bez BEE" - wydatki bez ujsc BEE zamknietych 08.10 23:40 (dzisiejsza gra).

| Scenariusz | Na minusie po roku (bez BEE) | w tym panowie zamkow | Wojsko w wojnie | Wniosek |
|---|---|---|---|---|
| S0 nic nie zmieniamy | 99 (**75**) | 54 (42) | 173 tys. | dolne oszacowanie - rachunek nie widzi zrywu werbunku z dob 1-40 (rozdz. 5.6) |
| S6 same poprawki dochodow (110 jak zbudowana + 112 + renty korony), bez 166 | 67 (**47**) | 26 (13) | 173 tys. | dochody zbijaja problem o ok. 1/3 - **dopoki** plynie zloto z niczego |
| S1 zamkniecie zlota z niczego (164) + 165, bez 166 | 187 (**176**) | 110 (104) | 173 tys. | zla kolejnosc = katastrofa |
| S4b same 165 + 166 + 168 (D staly, z zywnoscia) | **14** | 14 | 150 tys. (-14%) | wydatki zbijaja problem o ok. 80% |
| E1 pierwsza grupa **jak zbudowana dzis** (110 = 100% pan, bez dworu 162) | **0** | 0 | 166 tys. (-4%) | dziala, ale pokoj tylko 29% wojny, a dwor zostaje w kiesach |
| **E2 pierwsza grupa poprawiona** (rozdz. 4.4) | **0** | 0 | **166 tys. (-4%)** | **zalecana**; pokoj 36% wojny |
| S3 caly projekt z poprawkami | **0** | 0 | 157 tys. (-9%) | stan docelowy; z podatkiem wojennym 100 tys./dobe: 167 tys. (-4%) |

**Odpowiedz:** jedno i drugie, ale **glownie wydatki** - wojsko wielkosci wojennej trzymane caly rok bez zwiazku z dochodem. Dochody pana zamku sa w monecie
na poziomie barona-para - wlasciwe dla jego rangi - ale **za male na jego dzisiejsze wojsko i zle zbudowane** (zamek 0, polowa wplywu z niczego, 1/4 z kurczacego
sie zapasu korony). Poprawki dochodow sa potrzebne, zeby budzet 166 nie zjechal panom zamkow do ok. 42 tys. ludzi (S4b) i zeby w pokoju starczylo na polowe
zalog (Twoja decyzja 08.10).

---

## 4. Propozycje

Kazda pozycja: liczba, uzasadnienie, **platnik -> odbiorca** (zamkniety obieg), paczka i co zmienic w projekcie (`PROJEKT-EKONOMIA-OBIEG`, rozdz. 6-8, 12).
Liczby "pan zamku" to srednia na rod z rachunku v2 [S].

### 4.1 Dochody

| # | Zmiana | Liczba | Uzasadnienie | Platnik -> odbiorca | Paczka / zmiana w projekcie |
|---|---|---|---|---|---|
| **D-1** | **Zawor zamku zamiast kasowania** (110 K5) **z kluczem podzialu z 114** (2/3 pan, 1/3 skarbiec) i **z dosypka trybu 1** (do zapasu, liczona) do spelnienia Z9 | dzis gra kasuje 105 tys./dobe. Po 110: pan zamku ok. **+270-310/dobe**, z czego ok. 90% to jego wlasny zold zalogi i dwor (rozdz. 3.2); zaloga w zamku kosztuje go ok. 63% zoldu. Bez zaworu (S3): panowie zamkow w wojnie 41 tys. ludzi zamiast 50 tys. Gracz (Z8, `gracz2.py`, lenno z miastem i zamkiem - najgorszy przypadek, bo odplyw zamku i plon wsi wracaja przez jego miasto): z monety zoldu zalogi w zamku wraca 0.66-0.70 przy kluczu 2/3, 0.84-0.88 przy 110 jak zbudowanej - zawsze < 1 | zamek byl osrodkiem domeny (mlyny, piece, targ, sad - "banalia" 10-13% dochodu panow [H]); nic nie znika | kasa zamku (zold zalogi, sprzet, dwor pana; 110 cofa "zakupy z niczego" w zamku, wiec innego doplywu nie ma) -> pan 2/3, skarbiec 1/3 | **110** + z 114 tylko `CastleDuesSplitWithCrown`/`CastleDuesLordShare` 0.67 + w 110 regulator zamku zerowany **tylko w dol** (dosypka do zapasu zostaje, 3.7 tys./dobe, w logu) - Z9; poprawic opis `PopulationLaw.cs:27` |
| **D-2** | **Wies zatrzymuje 15% utargu i zaplate za zakupy lordow** (K7) | +47 tys./dobe do kies wsi -> renta: ok. +65 zl na wies dziennie; pan zamku ok. +130 | chlop sprzedaje plon i z tego placi czynsz [H] | kasa miasta (zakup plonu) -> kiesa wsi -> renta 20%/dobe -> pan | **112** (gotowa) |
| D-3 | **Markietani** (zolnierze wydaja w polu, 15% we wsi) | +100 zl na wies w wojnie [S]; najwieksza niepewnosc rachunku (MK 0 / 240: -13% / -5% wojska) | wojsko kupowalo zywnosc u chlopow [H] | sakiewki zolnierzy -> kiesy wsi -> renta -> pan | **163** |
| **D-4** | **Renty korony wedlug lenn** (miasto 3, zamek 1, wies 0.25) z tego, co zostaje z biezacych wplywow po kontraktach, zwrocie i splacie dlugu korony; **warunek sluzby:** rod trzyma w swoich zamkach i miastach co najmniej zaloge pokojowa (polowa wojennej) i stawil sie na ostatnie wezwanie do wojny | renta na **jeden udzial** (mediana krolestw): E2 wojna ok. 35, pokoj ok. 330 (146-954); S3 wojna ok. 390 (80-1 156), pokoj ok. 740 (277-1 264). Rok 1 jest wspierany zejsciem zapasow skarbcow (58 -> 20 mln, ok. 104 tys./dobe), potem renty spadaja. **Bez rent** (S3): skarbce rosna do 137 mln w rok, wojsko -19% w wojnie, zalogi pokojowe zamkow 34% | **to mechanizm gry, nie historia:** zwraca do obiegu nadwyzke korony i przenosi czesc zaworow miast do panow zamkow. Najblizszy wzor: renty z nadania krola dla wybranych magnatow (Salisbury 1337: 1 000 L z dochodow korony do czasu nadania ziemi; renty Yorka "rzadko placone") [H]. Konstabl Bristolu i Conwy (z wersji 1) to zamki **krolewskie** - nie pasuje, usuniete | skarbiec (1/3 zaworow, danina, clo, powinnosci, podatek BK, prawo trzecich) -> rody wedlug lenn | **165** (jest w 7.1) + **dopisac** warunek sluzby; renty licza sie do D, takze pokojowego; gracz - Q3 |
| **D-5** | **Korona z biezacych wplywow** + **1/3 zaworow do skarbca** + zwrot bez zalog + wlasna zapomoga + **niewyplacony zwrot jako dlug korony** (H-2) + **stale dary miedzy koronami z lore** | zwrot wyplacony w wojnie: E2 75% naleznego, S3 100%; reszta zapisana jako dlug korony wobec rodu, splacany najwyzej 30% wplywow dziennie przed rentami, liczony do D dopiero po wplynieciu. Dary: Polnoc -> Straz zold zalog Muru, najwyzej 25% biezacych wplywow Polnocy (ok. 3.5-3.9 tys./dobe); Wolne Miasta -> Dothrakowie 10% biezacych wplywow (ok. 3-10 tys./dobe) | korona placila wojne z podatkow i pozyczek; York 38 666 L i Percy - korona byla winna panom [H]; Straz zyje z darow panow Polnocy i korony, Wolne Miasta oplacaja khalasary, by nie zlupily miast [H?] (awoiaf) | kasy miast i zamkow (1/3), danina, clo -> skarbiec -> kontrakty -> zwrot -> dlug -> renty; skarbiec Polnocy -> rody Strazy; skarbce Wolnych Miast -> skarbiec Dothrakow | **165**: `CrownWageRefundGarrisons` -> false; zapomoga pisana od nowa (B14); **dopisac** H-2 i "trybut staly korona-korona" (Polnoc->Straz, Wolne Miasta->Dothrakowie) obok trybutu z 7.1; linia kredytu Banku dla koron - dopiero gdy Bank ma kapital (dzis 1.88 mln, -73 tys./dobe; Q4) |
| **D-6** | **Kontrakt najemnika staly z dnia najmu:** K = 1.3 x zold kompanii w dniu podpisania (zold 1.0 + jedzenie ok. 0.15 + sprzet ok. 0.15); w pokoju kontrakt "w oczekiwaniu" - polowa K i polowa ludzi; przeglad co 28 dob tylko w dol (gdy kompania ma mniej niz 75% ludzi z umowy, K do stanu faktycznego); pulap 166 najemnika = ludzie z umowy, nie 0.55 D; bez udzialu "dwor"; zold oplacony kontraktem bez zwrotu 50%; dochod "za tier" z niczego - 0. **Tylko AI**; gracz-najemnik zostaje na kontrakcie gry (wplyw x mnoznik, placony z `MercenaryWallet`, ktory splacaja wasale - prawdziwy platnik, `DefaultClanFinanceModel.cs:331-337, 504-513`) | 11 rodow: kontrakty ok. 13 tys./dobe w wojnie, 6.5 tys. w pokoju; najemnicy trzymaja 1 885 ludzi w wojnie i ok. 1 025 w pokoju, kiesa stabilna (S3: 291 tys. -> 300 tys.). Kontrakt "1.15 x biezacy zold" z wersji 1 **odrzucony**: kazde zwiekszenie wojska podnosilo kontrakt (Z8), a pod 166 kompanie topnialy (rachunek krytyka: w pokoju 1 885 -> 0 ludzi w ok. 90 dob; v2 dla S3 z wersji 1: 0 ludzi po roku pokoju) | kondotier zyl z kontraktu na okreslona liczbe kopii, sprawdzanej na przegladach; w pokoju "condotta in aspetto" za nizsza stawke [H?]. **Lore:** tylko 5 z 11 to wolne kompanie (Company of the Cat, Brave Companions, Long Lances, Windblown, Bright Banners); Faith Militant, Bractwo bez Choragwi, Moon Brothers, Stone Crows, Wild Hares, Synowie Harpii to frakcje - w grze sluza jak najemnicy, wiec kontrakt dostaja tylko w sluzbie; ich dochod poza sluzba (sponsor, najazdy) - pozniej | skarbiec pracodawcy (biezace wplywy, przed zwrotem) -> rod najemny | **165** (wiersz "dochod za tier" w 7.1 -> kontrakt staly); **166** (pulap najemnika = umowa) |
| D-7 | Clo z licznika cel, bez drugiego poboru z kasy miasta | +11 tys./dobe zostaje w kasach miast | licznik cel jest prawdziwym posiadaczem | licznik cel -> skarbiec | 164/165 |
| D-8 | Pensje rady od tego, co pan naprawde dostaje z osady | -9.6 tys./dobe wydatkow | radny dostawal udzial w tym, co pan zbieral [H] | (wydatek mniejszy) | 164 |
| D-9 | Karawana pana bez miasta oddaje zysk w najblizszym miescie jego krolestwa; zold karawan do sakiewki jej ludzi | pan zamku ok. +60-120/dobe | zamek nie jest targiem | kasa karawany -> pan | 164 |
| D-10 | Rycerz BK: zamknac "produkcje" majatku z niczego | rycerz ok. 20-30 zl/dobe (giermek / ubogi rycerz); notable -282 tys./dobe z niczego | rycerz zyl z czynszu swojego dworu [H] | utarg wsi -> majatek | 164; oddzialy - Q1 |
| D-11 | Statki druzyny rozbitej na ladzie albo rozwiazanej bez zlota z niczego - **dopiero z 166 albo po** | -269 tys./dobe z niczego; panowie zamkow ok. -1 200/dobe (34% wplywu) | statki nie zginely - stoja w porcie; zostaja wlasnoscia rodu albo sa sprzedane z prawdziwa zaplata | statki do innych druzyn rodu (jak dzis) albo sprzedaz portowi: kasa portu (najwyzej 25% nadwyzki) -> glowa rodu, ta sama kara sprzedazy dla AI i gracza | **164 - wyjac ze 164a i przesunac za 166**; forma - Q2 |
| **D-12** | (po kiesie ludu 173) **wiekszy udzial wsi w dochodzie pana, zawor miast w dol** - cele jako **udzialy prawdziwych przeplywow**, nie "na glowe" | cel: renta i podatek wsi = ok. 2/3 dochodu pana zamku z ziemi (dzis ok. 1/2 bez zamku); pan bierze najwyzej tyle, ile przechodzi przez wsie (utarg taborow 276 tys./dobe + kiesa ludu po 173). Cele z wersji 1 ("10-12 zl na mieszkanca wsi rocznie" = 1.6-2.0 mln/dobe, wiecej niz cale D swiata 1.47 mln, i "900-2 000/dobe z wlasnych wsi") **wycofane** - sprzeczne i nieosiagalne w zamknietym obiegu | Campbell: u panow swieckich czynsze i folwark ok. 3/4-9/10 dochodu [H]; w zamknietym obiegu pan bierze z tego, co plynie przez osady (`kod.md` P10) | kiesa ludu wsi -> pan; obnizka `TownRentShare` dopiero po 173 i dopiero o tyle, ile wsie zaplaca wiecej | **173**, potem korekta renty (H-3 przepisac na udzialy) |
| **D-13** | (nowe, lore) **Regale gornicze**: pan wsi gorniczej bierze udzial w sprzedazy rudy z jej utargu (dzis to ten sam podatek wsi 70% co z plonu) | do pomiaru: dochod z ziemi na rod Westerlands 1 530 - mniej niz Polnoc 1 952 i Dolina 1 966; "Lannister >> inni" sie dzis nie dzieje | stannaries, Kutna Hora: korona/pan 1/10-1/4 wartosci kruszcu [H?]; Lannisterowie z kopaln [H] | kupiec rudy (kasa miasta) -> pan wsi gorniczej | luka wobec lore - do raportu 02 (produkcja kruszcow w Westerlands); nie liczone |

### 4.2 Wydatki i budzet (166, 168)

| # | Zmiana | Liczba | Uzasadnienie | Paczka / zmiana w projekcie |
|---|---|---|---|---|
| **W-1** | **D staly do budzetu 166**: D = ziemia (podatek wsi, renta wsi, zawory miast i zamkow, cla) + korona (zwrot z biezacych wplywow, splata dlugu korony, renty) + kontrakt; **bez** przelewow w rodzie, bez podgladu majatkow BK, **bez jednorazowych** (lup, jency, statki, trzecia - ida do kiesy G i wracaja przez zapas wojny `0.8 x (G - R) / 45`) i **bez wlasnych pieniedzy wracajacych zaworem zamku** (zold i sprzet zalogi, dwor - inaczej petla: wiecej zalogi -> wieksze D -> wyzszy pulap). Wyjatek lore: u najezdzcow (Zelazne Wyspy, Dothrakowie, Wolni Ludzie) lup z **prawdziwego** platnika liczy sie do D ("zelazna cena"; rachunek: efekt maly, bo prawdziwego lupu jest malo) | pan zamku E2: D ok. 1 000 w wojnie (z wlasnymi pieniedzmi ok. 1 200-1 300); w pokoju bez tej zasady panowie zamkow trzymaja ok. 15% wiecej ludzi (S3: 18.2 zamiast 15.9 tys.), niz pozwala ich dochod z zewnatrz | pan budzetowal na czynszach; lup byl przypadkiem [H]; Z8 | **166, 6.1:** z definicji D usunac "skup lupu i jencow" i "dodatnie saldo modelu"; dopisac "zawory zamkow minus wlasne pieniadze rodu" (licznik: zold zalogi rodu + sprzet zalogi + dwor wplacone wczoraj do tej kasy x udzial pana x udzial odplywu); `ClanIncomeBook.cs:290, 301` |
| **W-2** | **Pokoj: pulap = udzial x D pokojowego** (D bez zwrotu i bez wlasnych pieniedzy; z rentami) - **w grupie E' 0.28, po grupie 2 ok. 0.23**; ostatecznie kalibruje autotest na "pokoj 35-40% wojny" | E2: 0.22 -> 30%, **0.28 -> 36%**; S3: **0.23 -> 38%**. Historycznie 10-25% dochodu na stalych ludzi (Percy 1/3 - granica) [H]: 0.28 jest troche ponad, to cena Twojej decyzji 35-40% i polowy zalog | renty korony podnosza dochod pokojowy; stale wojsko z czynszow (H-1) | **166, 6.4:** `PeaceWageShare` 0.25 -> 0.28 (E'), potem kalibracja |
| W-3 | Zold karawan rodu w budzecie | -120/dobe u pana zamku poza budzetem | kazdy wydatek rodu w jednym budzecie | 166, 6.1: wiersz "karawany" |
| W-4 | Kiesa rodziny (K12) - bez zmian, ale **pierwsza** | 19 glow panow zamkow < 5 000, gdy ich rodziny maja 5-36 tys. [P] | rod placi z rodzinnych pieniedzy | 166 (jest) |
| **W-5** | **Bank: limit 15 dni D z ziemi** + 10 000 za miasto, 5 000 za zamek; rata najwyzej 10% D; **okup i trybut poza budzetem tylko na raty** (najwyzej 10% D dziennie) | dzis 60 dni dochodu modelu z jednego dnia (`IronBank.cs:147, 150`) | wierzyciel pozyczal pod staly dochod z ziemi [H] (H-7); okup "wedlug majatku" (decyzja 09.10) to pol roku dochodu glowy - bez rat zniszczy rod mimo 166 | **168, 8.2** + **165/168** (okupy) |
| **W-6** | **Zalogi w pokoju 50%** (decyzja 08.10) - w pokoju zaloga ma pierwszenstwo: do **80%** pulapu (w wojnie dalej 60%) | E2: zalogi zamkow w pokoju 45% wojennych, S3: 48% (przy 60% pulapu: 43%) | oblezenia potrzebuja obroncow od 1. dnia wojny; w pokoju pan trzymal straz zamku, nie druzyne [H] | **166, 6.4:** nowy klucz `GarrisonMaxShareOfBudgetPeace` 0.8 |
| W-7 | Uwaga do 6.3 projektu: "171 z 324 rodow na suficie partii" **nie potwierdza sie w T9** - partie maja 55-67% limitu [P] | budzet ogranicza wiecej rodow | - | 166, 6.3: poprawic opis |
| **W-8** | **Jedzenie wojska w budzecie:** udzial "dwor" staje sie "dwor i wyzywienie partii" - najpierw jedzenie partii, reszta do kasy siedziby; w wojnie: zold **0.60**, dwor i wyzywienie 0.20, sprzet i werbunek **0.17** + 0.2 zapasu, powinnosci 0.03 | jedzenie ok. 0.6-1.1 zl na czlowieka partii dziennie [P][S] (lordowie do kas miast 64.7 tys. w dobie 120 + wsie ok. 18 tys. / 102 tys. ludzi); bez tej linii rachunek byl za dobry (S5 w wersji 1: 0 na minusie, z jedzeniem na wierzch 3 - Royce, Vance, Waynwood; krytyk: 4). Sprzet 0.17 D = ok. 200 tys./dobe, wiecej niz dzisiejsze zakupy sprzetu (144 tys.) | jedzenie to prawdziwy wydatek do kas miast i wsi | **166, 6.1 i 6.4:** wiersz "dwor" -> "dwor i wyzywienie"; `WarWageShare` 0.55 -> 0.60, `GearShare War` 0.22 -> 0.17 |
| **W-9** | **Dwor 162 w wersji minimalnej juz w grupie E'** (przelew glowa -> kasa siedziby raz na dobe) i linia w logu "niewydane udzialy budzetu" | bez niego udzial dworu (0.20-0.35 D) zostaje w kiesach: krolowie 354 tys. -> 1.06 mln w rok pokoju, panowie miast -> 0.93 mln (E1) - pieniadz wypada z obiegu | zamkniety obieg: pieniadz ma krazyc | **162** (minimalny) do grupy E' |
| W-10 | **Przejscie pokoj -> wojna:** bez nowej reguly; autotest mierzy | po 60 dobach pokoju: 90% stanu wojennego po **13 dobach** wojny (partie 29 -> 117 tys. w ok. 15 dob, zalogi 37 -> 71 tys. w ok. 20 dob) | Twoja decyzja: zalogi pokojowe polowa - zamki maja obroncow od 1. dnia | do rozwazenia po autotescie: zamki przygraniczne 75% zalogi w pokoju |
| W-11 | **Ponowna kalibracja po zmianie tempa swiata** (Twoja decyzja 09.10: tempo ksiazkowe po naprawach ekonomii) | `WarChestDays` 45 = "40 dni sluzby" i D z 28 dob sa skalibrowane na dzisiejszy marsz (5-15 x za szybki); kampanie wydluza sie w dniach, lupu na dobe bedzie mniej | - | plan: po tempie - `WarChestDays`, R, udzialy |

### 4.3 Rozwazone i odrzucone (z powodem)

| Pomysl | Dlaczego nie teraz |
|---|---|
| Renty z miast "od dochodu, a nie od nadwyzki" | w zamknietym obiegu zawor oddaje w dlugim czasie to, co wplywa do kasy ponad zapas; inna formula zmienia tylko tempo, nie ilosc |
| Podniesc `PopulationRentPerHead`, `TownRentShare` albo zwrot korony | przesuniecie, nie nowe pieniadze: wiecej dla pana z kasy miasta = mniej na zakup plonu wsi i mniej dla korony |
| "Udzial pana zamku w podatkach okregu" jako osobne zrodlo | wsie zamku naleza do tego samego pana; realna droga to zawor zamku (D-1) i renty korony (D-4) |
| Myto zamku od karawan | brak pomiaru, koszt wydajnosci, zabiera notablom; wrocic po 164. Freyowie i most - kandydat na wyjatek lore |
| Osobny "dochod z sadow" | placili go chlopi - czesc renty wsi; po 173 wchodzi w D-12 |
| Majatki BK "do panow" w dzisiejszej postaci | produkcja z niczego (B11); prawdziwy udzial w utargu juz trafia do wlasciciela |
| Kontrakt najemnika = 1.15 x biezacy zold (wersja 1) | zarabianie na wlasnym wydatku (Z8), kompanie topnieja pod 166 - zastapiony D-6 |
| Cele "na glowe" (10-12 zl na mieszkanca wsi, podatek wojenny 2 zl na glowe) | wieksze niz cala gotowka, ktora przechodzi przez wsie; zastapione udzialami prawdziwych przeplywow (D-12) |

### 4.4 Kolejnosc paczek (projekt rozdz. 12) - co zmienic

Projekt kladzie 164a (zamkniecie zlota z niczego, w tym statkow) i grupe 110/111'/162/112/163 **przed** 165/166/168. W tym oknie panowie zamkow tracilyby
ok. 1 800 zl dziennie bez hamulca wojska (S1: 104-110 ze 118 na minusie w rok). 165, 166 i 168 nie otwieraja ani nie zamykaja zadnego ujscia
(zmieniaja zachowanie i zrodlo zwrotu); **110 i 112 zamykaja ujscia** (netto ok. +40 tys./dobe w zamkach i +47 tys. we wsiach), a wszystkie zrodla zlota
z niczego (statki ok. 266 tys., majatki BK 286 tys., zakupy miast 445 tys.) zostaja w E' otwarte - pieniadz swiata, dzis -41 tys./dobe, zacznie rosnac
o ok. +50 tys./dobe. W logu E' sledzic "Pieniadz swiata".

Zalecana kolejnosc:
1. **Grupa E': 165 + 166 + 168 + 110 (z kluczem 2/3 ze 114 i dosypka trybu 1) + 112 + 162 minimalny** - z W-1..W-9, D-4..D-6 (kontrakt staly, dlug
   korony, dary Polnoc->Straz i Wolne Miasta->Dothrakowie). Rachunek E2: 0 rodow na minusie w nowej kampanii, wojna 166 tys. (-4%), pokoj 59 tys. (36%).
2. 164a **bez statkow**, 108/109, 164b, 111' (K6) + 162 pelny + 163 + reszta 114 + 164c; potem PeaceWageShare ok. 0.23.
3. **Statki NavalDLC (D-11)** - gdy budzet juz dziala.
4. 173 kiesa ludu i podatek wojenny (decyzja C) - **cel: ok. 100 tys. zl/dobe w wojnie** (wtedy wojsko AI -4%), potem D-12.

---

## 5. Przewidywany skutek (rachunek w Pythonie)

### 5.1 Jak liczone [S]

`sim_dochody2.py` (python -I, czyta tylko kopie danych), `run_all.py` -> `wynik-v2.txt`. Kazdy z 314 rodow AI osobno, doba po dobie:
- **wplywy rodu** = jego prawdziwe dzienne szeregi z dob 31-120 T9 (dochod modelu, renty, zwrot, zdarzenia, trzecia), powtarzane w kolko, przerobione wedlug
  scenariusza: zamkniete jako "z niczego" (prawdziwa czesc zdarzen: krol 22%, miasto 24%, zamek 14%, rycerz 50%, najemnik 30%); przelew syn -> ojciec
  nie zmienia kiesy rodziny; kontrakt i majatek BK liczone raz;
- **zawor zamku** = (zold zalogi + sprzet + dwor do kasy zamku) x 0.557 (odplyw kasy 44%, l. 26118) x udzial pana (110: 1.0; z kluczem 114: 2/3); dwor
  panow miast wraca przez zawor miasta tak samo (w E');
- **korona** osobno dla 29 krolestw: wplywy -> dar dla Strazy / dary Wolnych Miast -> kontrakty najemnikow -> zwrot 50% zoldu partii -> splata dlugu korony
  -> renty wedlug lenn;
- **budzet 166** wedlug tabeli 6.1 projektu z poprawkami W-1, W-2, W-6, W-8, W-9; zwolnienia 15%/dobe po 3 dobach, werbunek 5%/dobe, zalogi 3%/dobe;
  bez budzetu - dzisiejsze wydatki rodu (zold + zmierzona "reszta");
- start "nowa kampania" = kiesy i skarbce z doby 1 (D(0) = G/60 przez 28 dob; G/120 daje to samo); "dalej od doby 120" = stan swiata po T9.

Czego rachunek nie widzi: kas miast, wsi i sakiewek (stan ustalony przez wspolczynniki **TV** = zawor miast po K6 i dworze, 0.8, i **MK** = markietani,
100 zl na wies), utraty lenn, okupow i trybutow, rat Banku (W-5), wojny i pokoju na przemian (osobno rok wojny, rok pokoju i przejscie 60 dob pokoju -> wojna).

### 5.2 Wynik - nowa kampania, rok (wojna caly rok / pokoj caly rok)

| Scenariusz | Na minusie / kiesa < 10 tys. | Wojsko w wojnie (dzis 173 tys.) | Wojsko w pokoju | Zwrot wyplacony | Panowie zamkow: wojna / pokoj (dzis 61.0 tys.) |
|---|---|---|---|---|---|
| **dzis T9 [P]** (120 dob) | glowy < 5 000: 31; bankrutow 15 | 173 tys. | - | 87% (z zapasu) | 61.0 tys. |
| S0 nic nie zmieniamy (bez BEE) | 99 / 108 (75 / 89) | 173 tys. | - | 72% | - |
| S1 164 + 165 bez 166 (bez BEE) | 187 / 199 (176 / 186) | 173 tys. | - | 62% | - |
| S6 poprawki dochodow bez 166 (bez BEE) | 67 / 78 (47 / 60) | 173 tys. | - | 75% | - |
| S4b 165 + 166 + 168, D staly | 14 / 11 | 150 tys. (-14%) | 45 tys. = 30% | 83% | 42.0 / 6.0 tys. |
| S5 z wersji 1 (po poprawce K8) | 0 / 4 | 166 tys. (-4%) | 55 tys. = 33% | 89% | 55.5 / 15.9 tys. |
| E1 E' jak zbudowana (110 = 100% pan, bez 162, z jedzeniem) | 0 / 0 | 166 tys. (-4%) | 48 tys. = 29% | 76% | 52.4 / 10.5 tys. |
| **E2 E' poprawiona** (PeaceWageShare 0.28) | **0 / 0** | **166 tys. (-4%)** | **59 tys. = 36%** | 75% (reszta dlugiem korony) | **51.3 / 12.8 tys.** (zalogi pokojowe 45%) |
| S3 z wersji 1 | 0 / 0 | 158 tys. (-9%) | 63 tys. = 39% | 100% | 54.1 / 20.7 tys. |
| **S3 docelowy poprawiony** (0.23) | **0 / 0** | **157 tys. (-9%)** | **59 tys. = 38%** | 100% | **49.6 / 15.9 tys.** (zalogi 48%) |
| S3 + podatek wojenny 100 tys./dobe | 0 / 0 | 167 tys. (-4%) | 59 tys. = 35% | 100% | 54.3 / 15.9 tys. |
| S3 + podatek wojenny 200 tys./dobe | 0 / 0 | 174 tys. (0%) | 59 tys. = 34% | 100% | 57.9 / 15.9 tys. |

Kampania kontynuowana od stanu po 120 dobach T9 (wojna): S0 125 na minusie (bez BEE 105); S6 99 (78); **E2 - 7** (Harlaw, Royce, Volentin, Marbrand,
Banu Ayan, Garner, Banu Nir - prawie wszyscy juz bankruci w T9), wojsko -5%, pokoj 35%; **S3 - 6** (Greyjoy, Harlaw, Marbrand, Banu Ayan, Garner, Banu Nir),
-10%. W pokoju od doby 120 na minusie zostaje 6-10 rodow, ktore juz wtedy maja ujemna kiese. Po 168 to zajecia, nie bankructwa.

**Dwa lata (728 dob, nowa kampania):** E2 - 1 rod na minusie (Volentin), wojna -5%, zwrot 66%, pokoj 35%; S3 - 0, wojna -12%, pokoj 37%;
S5 z wersji 1 - 4 (Royce, Vance, Volentin, Waynwood), zwrot 82%. Rok 1 jest wspierany zejsciem zapasow skarbcow (58 -> 20 mln), w roku 2 zwrot i renty spadaja.

### 5.3 E2 i S3 wedlug grup (nowa kampania, rok; srednio na rod i dobe)

| Grupa | D mediana W / P | Ziemia (zawor zamku; w tym wlasne) W | Zwrot W | Renty W / P | Kontrakt W | Jednorazowe (do kiesy) W | Zold W / P | Ludzi W / P (dzis) | Kiesa mediana d120 / d364 (W) |
|---|---|---|---|---|---|---|---|---|---|
| E2 krol | 7 811 / 6 498 | 6 539 (70; 619*) | 1 619 | 485 / 1 382 | - | 3 117 | 5 633 / 2 180 | 34.7 / 14.0 tys. (33.3) | 475 / 575 tys. |
| E2 pan miasta | 6 565 / 5 995 | 5 976 (13; 492*) | 1 325 | 131 / 989 | - | 2 200 | 4 820 / 1 933 | 77.1 / 31.5 tys. (75.0) | 273 / 305 tys. |
| **E2 pan zamku** | **999 / 716** | 1 061 (267; 234) | 544 | 43 / 390 | - | 1 786 | 1 894 / 423 | **51.3 / 12.8 tys.** (61.0) | 131 / 79 tys. |
| E2 rycerz BK | 16 / 0 | 6 | 26 | - | - | 82 | 71 / 7 | 0.8 / 0.0 tys. (2.0) | 30 / 25 tys. |
| E2 najemnik | 1 321 / 589 | - | - | - | 1 197 | 2 062 | 921 / 508 | 1.9 / 1.0 tys. (1.9) | 539 / 761 tys. |
| S3 krol | 8 323 / 7 704 | 5 525 (79; 70) | 1 975 | 1 584 / 2 719 | - | 973 | 5 298 / 1 967 | 32.9 / 13.1 tys. | 298 / 271 tys. |
| S3 pan miasta | 7 617 / 6 951 | 4 993 (13; 12) | 1 624 | 1 194 / 2 362 | - | 465 | 4 548 / 1 732 | 72.3 / 29.1 tys. | 189 / 168 tys. |
| **S3 pan zamku** | **1 550 / 1 359** | 1 308 (314; 281) | 696 | 400 / 878 | - | 283 | 1 851 / 479 | **49.6 / 15.9 tys.** | 105 / 59 tys. |
| S3 rycerz BK | 30 / 20 | 26 | 27 | - | - | 13 | 53 / 9 | 0.4 / 0.06 tys. | 29 / 21 tys. |
| S3 najemnik | 1 321 / 589 | - | - | - | 1 197 | 342 | 921 / 508 | 1.9 / 1.0 tys. | 291 / 300 tys. |

\* w E2 u krolow i panow miast "wlasne" to ich dwor wracajacy zaworem wlasnego miasta (162 w E', bez K6).

**Proporcje na jednej podstawie (D bez wlasnych pieniedzy):** pan zamku w S3 placi w wojnie sam ok. 1 155/dobe (zold 1 851 minus zwrot 696) = ok. 75% swojego
D **przez caly rok**; historyczny pan placil sam ok. 0.7-1.35 x dziennego dochodu przez 40-80 dni = ok. **8-30% rocznego dochodu** [H][S]. Gra trzyma wiec
wojne **2-8 razy ciezsza niz historia** - to wybor gry (w T9 wojna u 94-97% rodo-dob), nie blad budzetu. W pokoju pulap 0.23-0.28 x D = troche ponad
historyczne 10-25%. Kiesa pana zamku schodzi w wojnie do rezerwy R = 20 dni D (ok. 20-30 tys.) - tak dziala zapas wojenny.

### 5.4 Wedlug krolestw (wojna: zmiana wobec dzis / pokoj: % dzisiejszego wojska) [S]

Proponowany prog autotestu: **zadne krolestwo ponizej -15% w wojnie i ponizej 25% dzisiejszego wojska w pokoju**, poza krolestwami, ktore Jeff uzna
za biedne z lore (Q4).

| Krolestwo (dzis, tys.) | S3 z wersji 1 | E1 (E' jak zbudowana) | **E2** | **S3 poprawiony** | S3 + podatek 100 tys. |
|---|---|---|---|---|---|
| Zelazne Wyspy (10.0) | -47% / 17% | -29% / 14% | **-29% / 18%** | **-46% / 15%** | -37% |
| Sarnor (4.2) | -31% / 25% | -13% / 19% | -15% / 25% | -34% / 24% | -27% |
| Smocza Skala (6.8) | -29% / 19% | -2% / 14% | -2% / 20% | -31% / 18% | -22% |
| Straz (3.8) | -39% / 16% | -21% / 8% | **-4% / 25%** (dar Polnocy) | **-24% / 43%** | -18% |
| Polnoc (19.3) | -19% / 33% | -22% / 25% | -24% / 30% | -22% / 28% | -15% |
| Dothrakowie (4.5) | -31% / 18% | -24% / 13% | -22% / 20% | **-8% / 28%** (dary Wolnych Miast) | +4% |
| Dolina (11.9) | -13% / 36% | -17% / 28% | -17% / 33% | -13% / 33% | -8% |
| Dorne (11.2) | -8% / 31% | +3% / 21% | +3% / 27% | -13% / 26% | -6% |
| Dorzecze (9.9) | -3% / 40% | -13% / 30% | -14% / 36% | -4% / 36% | +5% |
| Krolewska Przystan (16.7) | -9% / 40% | -11% / 31% | -11% / 36% | -10% / 37% | -5% |
| Wolni Ludzie (3.2) | -7% / 23% | +11% / 18% | +11% / 22% | -11% / 20% | -3% |
| Reach (12.2) | +3% / 42% | +6% / 31% | +6% / 38% | +1% / 38% | +6% |
| Wolne Miasta (Lys, Tyrosh, Volantis, Braavos, Qarth) | +15..+18% / 44-59% | +8..+17% / 34-48% | +5..+17% / 41-62% | +12..+17% / 40-60% | +14..+17% |
| krolestw ponizej -15% w wojnie / ponizej 30% w pokoju | 6 / 6 | 5 / 14 | 5 / 8 | 5 / 7 | 5 / 7 |

Dlaczego: krolestwa, ktore dzis zyja ze zdarzen i z zapasu skarbca (Zelazne Wyspy 27%, Dothrakowie 30%, Straz 38% wplywu ze zdarzen; Smocza Skala 58%),
traca to przy W-1 i zamknieciu (164). Wolne Miasta maja bogate miasta i male armie - rosna. Lore czesciowo to popiera (Zelazne Wyspy, Smocza Skala, Polnoc
"wielka, ale uboga"), ale rownowaga gry sie zmienia - **Q4**. Straz i Dothrakowie: dary z lore (D-5) wyrownuja ich w S3.

### 5.5 Wrazliwosc (S3 poprawiony; w kazdym wariancie 0 rodow na minusie)

| Zmiana | Wojsko w wojnie | Pokoj / wojna | Panowie zamkow W / P | Uwagi |
|---|---|---|---|---|
| S3 (jedzenie 0.8, odplyw zamku 44%, TV 0.8, MK 100) | 157 tys. (-9%) | 38% | 49.6 / 15.9 tys. | |
| jedzenie 0.6 / 1.08 zl na czlowieka | 158 / 156 tys. | 38% | 50.1 / 48.9 tys. | maly wplyw, bo jedzenie idzie z udzialu dworu |
| odplyw kasy zamku 55% / 30% | 156 / 159 tys. | 38% | 48.1 / 51.7 tys. | |
| zawor miast TV 0.6 / 1.0 | **147 / 165 tys.** | 35 / 40% | 48.3 / 51.5 tys. | najwieksza niepewnosc (kasy miast po 164) |
| markietani MK 0 / 240 | 151 / 165 tys. | 39 / 36% | 45.8 / 54.5 tys. | |
| bez zaworu zamku | 148 tys. | 38% | 41.4 / 14.8 tys. | |
| bez rent korony | 140 tys. | 30% | 41.6 / 9.3 tys. | **skarbce 137 mln** - pieniadz poza obiegiem |
| bez K6 | 146 tys. | 36% | 41.4 / 11.7 tys. | |
| D z wlasnymi pieniedzmi (bez W-1 czesc 2) | 157 tys. | 39% | 48.9 / 18.2 tys. | roznica glownie w pokoju (petla) |
| udzialy wojny jak w projekcie (0.55 / 0.22) | 151 tys. (-13%) | 39% | 47.6 / 15.9 tys. | W-8 daje ok. +4% wojska |
| kontrakt najemnika 1.2 x | 157 tys. | 38% | - | najemnicy biedniejsi, liczba ta sama |

Wniosek: wynik "zero rodow na minusie" daje sama regula budzetu (wydatki zwiazane z dochodem), nie precyzja wspolczynnikow; od wspolczynnikow zalezy
wielkosc armii (+-6%). Ryzyko lezy w wydatkach poza budzetem (okupy, trybut, raty - W-5).

### 5.6 Kalibracja rachunku (uwaga krytyki) [P][S]

S0 (bez zmian) startowany z kiesami doby 1 trafia w dobie 120 za wysoko: pan zamku 143 tys. wobec zmierzonych **67 tys.** (2.1 x) - rachunek nie widzi
**zrywu werbunku z dob 1-40** (kiesy panow zamkow 205 -> 109 tys. w 40 dob; Polnoc 440 -> 803 ludzi na rod). Startowany z kiesami doby 40 trafia blizej:
pan zamku 87 tys. wobec 67 tys., krolowie 278 wobec 222 tys., panowie miast 381 wobec 320 tys. (19-30% za wysoko), rycerze 27 wobec 26 tys., najemnicy
533 wobec 544 tys. Wniosek: **liczby "na minusie" w scenariuszach bez budzetu (S0, S1, S6) to dolne oszacowanie** - w grze bedzie gorzej. W scenariuszach
z budzetem werbunek i sprzet sa ograniczone udzialem D (tez na starcie: D(0) = G/60 albo G/120 daje to samo), wiec zryw jest w rachunku zaplacony z budzetu.

### 5.7 Co sprawdzic autotestem po wgraniu grupy E'

- rodziny < 5 000 (bez dworzan BK) <= 10; zajecia Banku <= 5 w roku; zaden skarbiec w wojnie ponizej 0.25 mln dluzej niz 28 dob;
- **krolestwa:** zadne ponizej -15% wojska w wojnie i ponizej 25% dzisiejszego w pokoju (poza lista z Q4);
- **nowe linie w logu:** "zawor zamkow" (pan / korona / w tym wlasne pieniadze rodu), D staly obok D ksiegi w `budzet-rodow.csv` (ziemia, korona,
  jednorazowe, wlasne), "niewydane udzialy budzetu", "dlug korony wobec rodow", "dary miedzy koronami", "Pieniadz swiata" (przyrost w E');
- panowie zamkow: zold / D w wojnie 0.6-0.8, w pokoju <= 0.3; zalogi pokojowe ok. 45-50% wojennych;
- wojsko: wojna 160-170 tys., pokoj 35-40% wojny; po wybuchu wojny 90% stanu w ok. 2 tygodnie;
- najemnicy: liczba ludzi w wojnie i w pokoju stala (+-10%), kontrakty w logu.

---

## 6. Pytania do Jeffa (tylko zmiany rozgrywki)

**Q1. Rycerze Banner Kings (85 rodow "gentry" z jednym majatkiem) - czy w wojnie maja dalej wlasne oddzialy?**
Z dochodem rycerza (ok. 20-30 zl dziennie) budzet 166 zostawi im 0-10 ludzi; dzis maja po ok. 24.
(a) **bez wlasnych oddzialow - jada w druzynie swojego pana albo w armii krolestwa** (w jego pulapie);
(b) wlasny oddzial do ok. 25 ludzi w wojnie, a korona zwraca im polowe zoldu jak panom (nie caly - inaczej rycerz dostawalby z kazdej monety wiecej niz pan);
(c) jak dzis - budzet ich nie dotyczy (wtedy dalej bankrutuja, jak Garner).
Rekomendacja: **(a)** - historycznie rycerz szedl na wojne zwykle w swicie magnata, w ramach jego umowy z krolem (Clarence 1415: 14 rycerzy w kontrakcie
ksiecia); mniej druzyn na mapie to tez mniej pracy dla gry.

**Q2. Statki druzyny rozbitej na ladzie albo rozwiazanej - co z nimi?**
Dzis gra daje je najpierw innym druzynom rodu, a reszte zamienia na zloto z niczego dla glowy rodu (AI 100% wartosci, Ty - z kara sprzedazy; pojedynczy
rod 300-550 tys. jednego dnia). Statki nie zginely - stoja w porcie.
(a) statki zostaja wlasnoscia rodu i czekaja w porcie na nastepna druzyne;
(b) **sprzedaz portowi: zaplata z kasy najblizszego portu krolestwa (najwyzej 1/4 jej nadwyzki), ta sama kara sprzedazy dla AI i dla Ciebie**.
Rekomendacja: **(b)** (prostsze; (a) wymaga przechowalni statkow), i dopiero po budzecie 166.

**Q3. Renty od korony wedlug lenn - takze dla Ciebie?** Jako wasal placisz koronie jak AI (1/3 zaworow, powinnosci - Twoja decyzja 08.10). Renta na jeden
udzial lenna (miasto 3, zamek 1, wies 0.25) wychodzi w rachunku: w pierwszej grupie ok. 35 zl dziennie w wojnie i ok. 330 w pokoju; po calym projekcie ok. 390
w wojnie i ok. 740 w pokoju (rozrzut miedzy krolestwami w pokoju ok. 4-5 x, w wojnie wiekszy; w roku 2 mniej). Lenno 1 miasto + 1 zamek + 4 wsie (5 udzialow): po calosci ok. 1 900 zl dziennie
w wojnie i ok. 3 700 w pokoju; 2 miasta + 3 zamki + 10 wsi (11.5 udzialu): ok. 4 500 / 8 500 - tyle, ile D krola AI.
(a) **tak, na tych samych warunkach co AI** (zaloga co najmniej pokojowa, stawienie sie na wojne);
(b) nie - renty tylko dla AI.
Rekomendacja: **(a)** - jedna regula dla wszystkich; przy duzym lennie jest hojnie, ale placisz tez 1/3 zaworow swoich miast.

**Q4. Biedne krolestwa beda slabsze niz dzis - zgoda?**
Po zamknieciu zlota z niczego wojsko krolestwa zalezy od jego ziemi: Zelazne Wyspy ok. -29% w pierwszej grupie i ok. -46% po calym projekcie, Sarnor
i Smocza Skala ok. -30%, Polnoc ok. -22%; Wolne Miasta rosna o 12-17%. Caly swiat w wojnie: pierwsza grupa -4%, caly projekt -9% (Twoja decyzja C: nie
"5% mniej", tylko podatek wojenny - musi dac ok. 100 tys. zl dziennie).
(a) **zgoda tam, gdzie lore mowi "biedni"** (Zelazne Wyspy, Smocza Skala, Sarnor), a Straz i Dothrakowie dostaja dary z lore (Polnoc dla Strazy, Wolne Miasta
dla khalasarow) - juz w rachunku;
(b) wyrownac: korona biednego krolestwa pozycza w Zelaznym Banku na wojne (jak Robert), a Bank bierze kapital z lokat bogatych Wolnych Miast - nowa paczka,
po 168;
(c) zostawic jak dzis - wtedy biedne krolestwa dalej zyja ze zlota z niczego.
Rekomendacja: **(a) teraz, (b) pozniej**.

Bez pytania (parametry, decyzja projektu z uzasadnieniem): zawor zamku 7% z kluczem 2/3, `PeaceWageShare` 0.28 w E' (potem kalibracja), udzialy wojny
0.60 / 0.20 / 0.17 / 0.03, zaloga do 80% pulapu w pokoju, D staly bez jednorazowych i bez wlasnych pieniedzy, kontrakt najemnika AI staly 1.3 x zold z umowy,
dlug korony (H-2), Bank 15 dni D z ziemi, okupy i trybuty na raty, kolejnosc paczek z 4.4.

---

## 7. Pliki i kontrola

- Ten raport: `docs/audyt-2026-10-09/12-DOCHODY-RODOW.md` (jedyny plik zapisany w repo; nic nie zacommitowane).
- Badania zrodlowe: `C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\7016f733-d379-418e-b700-f66fd52e4d2b\scratchpad\dochody\`
  `pomiar.md`, `kod.md`, `historia.md` (+ `pomiar.json`, `klany.json`, skrypty).
- Rachunek v2: `...\dochody\sim\sim_dochody2.py` (opcje `start=d1|d40|d120`, `DAYS=`, `PEACE=`, `WWS=`, `WCOURT=`, `WGEAR=`, `GMAXP=`, `FOOD=`, `CVN=`,
  `KM=`, `TV=`, `MK=`, `WARTAX=`, `GIFT_FC=`, `D0DAYS=`, `only=`, `detail=1`), `run_all.py` -> **`wynik-v2.txt`** (kalibracja, nowa kampania, od doby 120,
  2 lata, grupy, krolestwa, przejscie, wrazliwosc); `gracz2.py` (Z8 dla gracza przy podzialach E' i S3). Wersja 1: `sim_dochody.py` i `wynik-*.txt`.
- Dane: `...\scratchpad\kopiaT9-120\` (log Armoury, `2026-10-08_22-23-58\budzet-rodow.csv`, `economy-2026-10-08_22-25-23.csv`).
- Kod sprawdzony osobiscie [K]: `PopulationLaw.cs:27, 157, 296`; `ClanIncomeBook.cs:285-305`; `KingdomTreasury.cs:191, 279-284`; `SoldierPay.cs:374-388, 405,
  446, 499`; `AiGear.cs:170-185`; `IronBank.cs:147, 150`; `Settings.cs:545-553, 657`; paczka 110 `CastlePurse.cs` (39f5bdf: zawor 100% do pana, regulator
  zamku 0 w obie strony, "zakupy" zamku cofniete), `Settings.cs:623-627` tej galezi; paczka 114 `Settings.cs:637-639` (a14efe8: `CastleDuesSplitWithCrown`,
  `CastleDuesLordShare` 0.67); gra `DefaultClanFinanceModel.cs:125-140, 331-337, 497-513`, `DefaultSettlementTaxModel.cs:21`; BK `EconomyPatches.cs:242,
  424-445, 1099`; NavalDLC `NavalShipDistributionCampaignBehavior.cs:20-80`. Pozostale [K] za `kod.md`.
- Log [P]: `Armoury-2026-10-08_22-23-58.log` l. 26105 (budzet rodow), 26111 (pieniadz swiata), 26117 (kasy miast), 26118 (kasy zamkow), 26121 (Obieg),
  linie "Skarbce".

---

## 8. Krytyka i odpowiedzi

Dwie niezalezne krytyki wersji 1 (32 uwagi). Kazda sprawdzona w kodzie, logu albo rachunku. **P** = prawda (wprowadzone), **C** = czesciowo,
**F** = falsz (odrzucone z powodem).

| # | Uwaga (skrot) | Werdykt | Co zrobione |
|---|---|---|---|
| 1 | 110 oddaje 100% panu, nie 2/3; S5 dawal koronie 1/3 zamkow, puszczal dwor 162 przez zawor i liczyl zawor od doplywu brutto (odplyw 44%); E' jak zbudowana: pokoj 27-29%, ponizej 35-40% | **P** | potwierdzone w `CastlePurse.cs` (39f5bdf) i l. 26118; E1 = E' jak zbudowana: wojna -4%, pokoj 29%, zwrot 76%, D pana zamku w pokoju 831 (jak u krytyka); D-1 z kluczem 114, W-2 0.28, `gracz2.py` powtorzony (zawsze < 1) |
| 2 | Kontrakt 1.15 x zold = zarabianie na wlasnym wydatku (Z8); pod 166 kompania sie kurczy; nie wiadomo, czy dotyczy gracza | **P** | D-6 przepisane: kontrakt staly z dnia najmu, pokoj "w oczekiwaniu", pulap = umowa, tylko AI; nie jest pytaniem, bo gry gracza nie zmienia |
| 3 | Zawor zamku placony w wiekszosci z wlasnych pieniedzy pana; W-1 wlicza go do D (petla); proporcje wojny i pokoju na roznych podstawach | **P** (z zastrzezeniem: zdanie 2 Z8 dotyczy podstawy podzialu rent, ale petla jest prawdziwa) | W-1: D bez wlasnych pieniedzy z zaworu; proporcje na jednej podstawie (5.3); W-6 przeliczone (45-48%) |
| 4 | W E' bez 162 udzial dworu zostaje w kiesach - pieniadz wypada z obiegu | **P** | E1: krolowie 354 tys. -> 1.06 mln w rok pokoju; W-9: 162 minimalny do E', linia "niewydane udzialy" |
| 5 | W scenariuszach z budzetem brak jedzenia wojska; rachunek niesymetryczny | **P** | W-8: jedzenie z udzialu "dwor i wyzywienie"; S4b z jedzeniem 14 na minusie; wrazliwosc 0.6-1.08 |
| 6 | S0 od doby 1 nie trafia w T9 (pan zamku 143 tys. wobec 67 tys.); liczby "na minusie" optymistyczne | **P** | 5.6: kalibracja od doby 40 (19-30% za wysoko), S0/S1/S6 jako dolne oszacowanie; D(0) = G/120 sprawdzone - bez roznicy w rachunku |
| 7 | D-12: dwa sprzeczne cele (10-12 zl na glowe = 2-5 x wiecej niz 900-2 000 dla zamku; 1.6-2.0 mln/dobe dla swiata) | **P** | cele "na glowe" wycofane; D-12 jako udzialy prawdziwych przeplywow |
| 8 | Podwojne liczenie w dfix (przelew syn->ojciec, kontrakt, majatek BK) | **P** | poprawione; S5 z wersji 1 po poprawce: mediana kiesy pana zamku 99 tys. (jak u krytyka) |
| 9 | Bledy arytmetyki i opisow (52% nie 48%, krol 26%, "cla 391", 34% nie 40%, bilans na dwoch podstawach) | **P** (C co do "cla": "Walled Demesnes" to clo miasta / 5 **i** resztki podatku miasta, `EconomyPatches.cs:424-445`) | poprawione w 1.1, 2 (B3, bilans), 3.1 |
| 10 | Q3: 505/990 to srednia na pana zamku (1.5 udzialu), nie na zamek; renta zalezy od krolestwa i spada po roku 1 | **P** | Q3 i D-4 podaja kwote na udzial, rozrzut krolestw i spadek w roku 2 |
| 11 | Q1 (b) + zwrot 50% = 1.5 z monety | **P** | Q1 (b) przepisane na zwrot 50%; zold oplacony przez korone nie jest podstawa zwrotu (D-6) |
| 12 | S0, S1, S6 zawieraja ujscia BEE zamkniete 08.10 | **P** | warianty "bez BEE" (S0 75, S1 176, S6 47 na minusie) |
| 13 | Wyniki tylko na rok 1 (wspierany zejsciem zapasow) | **P** | 5.2: dwa lata (E2: 1 na minusie, zwrot 66%) |
| 14 | 110 zdejmuje tez dosypke kasy zamkow (Z9); 110 i 112 zamykaja ujscia; zdanie 4.4 za szerokie | **P** | `RegulatorPostfix` zeruje w obie strony - D-1: dosypka trybu 1 zostaje; 4.4 zawezone, "Pieniadz swiata" w logu |
| 15 | Brak tabeli wedlug krolestw; Zelazne Wyspy -47%, Straz -39% itd. | **P** | 5.4: tabela i prog autotestu; dary z lore (Straz, Dothrakowie) w D-5; lup najezdzcow w D (W-1); reszta - Q4 |
| 16 | Kontrakt 1.15 x zold: topnienie geometryczne, kontrakt w pokoju, gracz | **P** | jak 2; w pokoju kompania trzyma polowe ludzi |
| 17 | Do kas zamkow plynie 61 882/dobe "zakupow z niczego" - D-1 przed 164a oddalby 2/3 tego panom | **F** w glownej tezie: 110 sama cofa "zakupy" ludnosci zamku (`CastlePurse.cs`, punkt 2: postfiks na `MakeConsumption`), wiec do zaworu nic z niczego nie trafia; **P** co do "1/3 zoldu jak w miescie" | zdanie o "zalodze za 1/3 zoldu" usuniete; koszt zalogi w zamku i Z8 policzone (`gracz2.py`) |
| 18 | S5 bez przeplywu dworu: pokoj 28%, 2 rody na minusie | **P** | jak 1 i 4 |
| 19 | "Obie proporcje w historycznym przedziale" nieuczciwe: wojna caly rok = 2-8 x historii; pokoj liczony z wlasnymi pieniedzmi | **P** | 5.3 i "Dla Jeffa" pkt 10; W-11 ponowna kalibracja po tempie swiata |
| 20 | D-12 i H-4: cele wieksze niz cala gotowka wsi; ranga "hrabia" na rosnacej tabeli ludnosci | **P** | jak 7; rangi tylko w monecie; podatek wojenny podany jako potrzeba w zl/dobe (100-200 tys.) do sprawdzenia przy 173 |
| 21 | D-4: konstabl Bristolu/Conwy to zamki krolewskie; renty w pokoju wieksze niz w wojnie; lore odwrotne | **C**: konstabl - prawda (usuniety); ale renty z nadania krola dla magnatow istnialy (Salisbury 1337, York) | D-4 opisane jako mechanizm gry z warunkiem sluzby; "bez rent" policzone (skarbce 137 mln) |
| 22 | Q3: renta gracza nie policzona; typowe lenno = D krola AI | **P** | Q3 z kwotami na udzial i dla dwoch lenn gracza |
| 23 | Q2: statki nie sa "utracone"; wyplata tylko poza morzem i przy rozwiazaniu; kara sprzedazy tylko dla gracza | **P** | B3, D-11, Q2 przepisane (`NavalShipDistributionCampaignBehavior.cs:28-71`) |
| 24 | "Lannister >> inni" nie realizuje zadna propozycja; Westerlands biedniejsze niz Polnoc | **P** | D-13 (regale gornicze) jako luka do raportu 02; nie liczone |
| 25 | -9% wojska w S3 sprzeczne z decyzja C; brak wariantu z podatkiem wojennym | **P** | "Dla Jeffa" pkt 8, Q4; S3 + podatek 100 tys. (-4%) i 200 tys. (0%) |
| 26 | D-5 bez kredytu korony; H-2 nie trafil do propozycji; Bank za maly | **C**: H-2 - prawda (wprowadzone, liczone); linia kredytu w Banku - Bank ma 1.88 mln i traci 73 tys./dobe, wiec dzis nie ma z czego pozyczac | D-5: dlug korony; kredyt Banku dla koron - Q4 (b), po 168 |
| 27 | Z 11 "najemnikow" tylko 5 to kompanie z lore | **C**: prawda co do lore; w grze wszystkie 11 sluza jak najemnicy, wiec kontrakt w sluzbie dostaja wszystkie | opis w 1.1 i D-6; dochod frakcji poza sluzba - pozniej |
| 28 | Q1 (b) slabo uzasadnione; zacheta odwrocona; wiecej druzyn | **P** | rekomendacja (a) |
| 29 | Rycerz BK z 20 zl to giermek, nie rycerz 26-66; pan samej wsi to baron, nie dziedzic | **P** | 1.1 poprawione |
| 30 | Srednia 641 wobec mediany 319; dwa rozne werdykty | **P** | jedna miara (obie podane), jeden werdykt |
| 31 | Przelicznik z ok. 1300 mieszany z liczbami z 1415-1527 | **P** | uwaga w naglowku, oznaczenia [H?] |
| 32 | Brak przejscia pokoj -> wojna; gracz bez 166 moze trzymac armie wojenna w pokoju | **P** | 5.2/W-10: 90% stanu wojennego po 13 dobach; zalogi pokojowe 45-48%; zamki przygraniczne do rozwazenia po autotescie |
