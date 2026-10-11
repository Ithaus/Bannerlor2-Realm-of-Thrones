# PROJEKT ETAP 6 - rzemioslo, surowce i konie (PLAN 6.1-6.19) - 12.10.2026, wersja 2

Status: SAM PROJEKT. Kod, gra, ustawienia i zapisy nietkniete, nic nie budowane, bez commita. Wzor: `docs\etap5\PROJEKT-ETAP5-KIESA-LUDU-KASY-MIAST-2026-10-11.md`.
**v2 (12.10, po dwoch krytykach):** 31 uwag sprawdzonych w kodzie i dekompilacji - wszystkie przyjete (5 z inna albo szersza poprawka lub z zastrzezeniem: K1-01, K1-03, K1-04, K1-05/06, KB K0-model-gry); zmiany w miejscu oznaczone "v2";
wykaz `docs\etap6\KRYTYKA-E6.md`, podsumowanie w rozdz. 8. Najwazniejsze: wegiel dymarki z prawdziwego drewna (R1), konie wedlug kategorii z podaza i regulatorem w obie
strony (K1-K5), jeden regulator wsi zamiast W1 + W3, cel jazdy z szablonow 175 (K3), ryby jako dopisek a nie czynnik (W2), sprzedaz koni przez `MenPurse` (K2), R2 warunkowo.
Decyzje Jeffa 11.10 17:30 (SP:1190-1192): 6.1 wytop przy kopalni + przetop - TAK; 6.14 rycerz z wlasnym koniem, jazda AI ok. 20% (Dothrakowie konni, Zelazne Wyspy
malo) - TAK; NOWE 6.19 kowale kuja wedlug kultury swojej krainy, nikt nie kuje unikatow; 6.18 wioski poboczne wedlug rodzaju (13:50-14:00, SP:1182-1183).

Podstawa (dowody nie sa tu powtarzane w calosci):
- rozpoznania `docs\etap6\rozpoznanie-A-logi.md` (**RA6**), `rozpoznanie-B-kod.md` (**RB6**), `rozpoznanie-C-dokumenty.md` (**RC6**), `rozpoznanie-D-styki.md` (**RD6**);
- kod: SCR = `C:\Users\GAME\AppData\Local\Temp\claude\C--Users-GAME-Bannerlor2-Realm-of-Thrones\ae222cb7-a7c2-473a-8fc1-1dc1d6f688e3\scratchpad`; **K:** = `SCR\e4\koniec\Armoury\src`
  (galaz `w-toku/zbior-e4-koniec` 7658247 = etap 4 + E5/0); **CS:** = repo `CrashScribe\src`; porcja A etapu 5: `SCR\e5\a1`, `SCR\e5\a25`, `SCR\e5\a6` (nie scalone z K);
- dekompilacje: **G:** = `SCR\dec32\cs_full` (TaleWorlds.CampaignSystem 1.4.8), **BK:** = `SCR\e4tools\bkfull`, DTE = `SCR\e410\dte`, BEE = `SCR\e3\dec36\bee`;
- logi: **L:N** = `SCR\kopia-z31-A\Armoury-2026-10-11_08-09-11.log` (120 dob nowej kampanii, zbior 31 = kod w grze); **L728:N** = `SCR\kopia-728\Armoury-2026-10-10_20-58-02.log`
  (728 dob, starszy kod - tylko tendencja); d91-120 = srednia dob 91-120 (RA6);
- dane: `SCR\e6B\items-culture.csv` (2560 sztuk uzbrojenia i koni z towaru, kultura z XML modulow - RB6), ROT `Modules\ROT-Map\ModuleData\settlements.xml` (97 miast),
  `ROT-Content\ModuleData\ROT-Troops.xml`; symulacja lancucha 6.19 do tego projektu: `SCR\e6P\chain.py` -> `SCR\e6P\wynik.txt` (wariant v4 = regula M1);
- dokumenty (docs\): **PLAN** = PLAN-DO-KONCA-MODA-2026-10-09.md (etap 6: :171-197), **SP** = STAN-PRAC.md (DECYZJE JEFFA - wiazace), **K13** = PLAN-K13-2026-10-07.md
  (kroki 131-139; to NIE numery paczek), **P174/P175** = PROJEKT-174/175, **A14** = audyt-2026-10-09\14-ARMIE-KROLESTW.md, **LW** = przekazanie-lawa-2026-10-07\PRZEKAZANIE-DLA-DRUGIEGO-CZATU.md,
  **P5** = etap5\PROJEKT-ETAP5-...md, **CH** = `SCR\e4\koniec\CHANGELOG.md`.

Oznaczenia: [K] kod plik:linia, [P] pomiar z logu, [S] szacunek, [W] wniosek z kodu (niesprawdzony w grze), [D] decyzja Jeffa (data). Rok gry = 364 doby (kalendarz Armoury),
pora roku = 91 dob. Napisy w grze i opisy MCM - po angielsku; linie logu pomiaru - po polsku jak wszystkie. Kultury ROT (CLAUDE.md 2): `battania` = Polnoc, `vlandia` = Westerlands,
`reach` = Reach (wlasne id), `aserai` = Dorne, `sturgia` = Zelazne Wyspy, `empire` = Braavos, `nord` = Lorath, `khuzait` = Dothrakowie; KROLESTWO `vlandia` = Zelazny Tron
(KRYTYKA-E4:240) - wszedzie wiazemy przez obiekt kultury osady, nigdy przez StringId krolestwa.

---

## 0. Dla Jeffa - prostym jezykiem

1. **Zbroi na tulow juz starcza "na sztuki", brakuje lepszych.** Cos na tulowiu ma dzis 93.6% zolnierzy AI (prog etapu to 75% - spelniony, L:31426). Ale partie lordow
   maja korpus swojego szczebla tylko w 63%, a korpus t3+ w 25%. Na polkach lezy 9 840 korpusow t1-2 i tylko 175 lepszych (L:31427). Warsztaty kuja dziennie ok. 110
   korpusow, a AI kupuje 652 - reszte daje lup t1-2 (RA6 rozdz. 2). Najczesciej warsztatom brakuje rudy (584 z 752 odpuszczonych cykli dziennie).
2. **6.19 - kazda kraina kuje swoje.** Dzis miasto kuje wyroby swojej kultury i "bez kultury", a gdy w danej linii nie ma nic swojego - cala obca pule. Przez to Winterfell kuje
   dzis Miecz Lannisterow, Kingslayer's Sword i katane z Yi Ti (bron rodow w ROT nie ma kultury), Polnoc kuje tarcze Westerlands i Braavos, a Czarny Zamek kuje wszystko.
   Kazde miasto kuje tez stroje bandytow (Gilded Raider). Po zmianie: najpierw wzory wlasnej krainy, potem kraina pokrewna (np. Nocna Straz i Skagos od Polnocy), potem
   wspolny region (np. Wolne Miasta), potem rzeczy wspolne dla calej strony morza (proste buty Pierwszych Ludzi, gambesony, piki). **Zbroje rodow (Lannister, Bolton,
   Tyrell...) kuje tylko ich wlasna kraina.** v2: Dothrakowie nie kuja stali (tylko skora, luki, arakhy z ROT), Wolni Ludzie nie kuja stali nigdzie (poza brazem Thennow),
   Wolne Miasta nie kuja rzymskich i nordyckich wzorow wanilii (te zostaja tylko w Braavos i Lorath). Czego kraina nie umie, tego nie kuje - przywiozy to wozy kupcow. Unikatow (helm Ramsaya i caly spis 157 sztuk) nie kuje nikt,
   takze droga gry, ktora dzis jeszcze przecieka (Twoj warsztat z wsadem). Kraina = kraina z mapy (z dnia kalibracji), wiec zdobyte miasto dalej kuje wedlug swojej ziemi.
3. **6.1 - wytop przy kopalni.** Ruda (ladunek 100 kg za 8 d) zamienia sie w lupe zelaza "Iron Bloom" (15 kg, ok. 21 d + wegiel), wytapiana we wsi gorniczej. v2: wegiel
   dymarki to prawdziwe drewno - najpierw las wsi wokol kopalni, reszte woz wsi kupuje na targu swojego miasta (tam, gdzie dzis pala je dymarki miejskie) - wiec drewna nie
   przybywa ani nie ubywa z niczego; bez drewna kopalnia daje mniej lup. Zelazo jest lzejsze i drozsze, wiec karawany wioza je dalej (dzis 30-34 z 97 miast bez rudy). **Przetop** = zlom i wraki
   wracaja jako lupy (Twoje "zlom = przetapianie", SP:810) - u kowali miast i w Twojej kuzni ta sama jednostka. Tylko nowa kampania.
4. **6.14 - wiecej jazdy, ale tylko z prawdziwym koniem.** Dzis 8.3% (L:31440), cel ok. 20%. Dzis hamuja zakupy: lord kupuje najtansze konie, garstke raz na 4 doby, a gra
   potrafi mu sprzedac najcenniejsze konie przy wizycie w miescie. v2: na polkach leza glownie konie zwykle (11 622), a stadniny daja dziennie tylko ok. 20-60 koni bojowych
   i 2-7 szlachetnych [S] - rycerze t4-6 dostana konie tak szybko, jak urosna stadniny (regulator hodowli podnosi konie bojowe, gdy brakuje). Po zmianie: rod kupuje
   rycerzowi konia wlasciwej kategorii (najpierw ze stada wsi przy osadzie postoju, potem na targu) i kon idzie do zbrojowni jezdzca; udzial jazdy partii = udzial jazdy
   w jej szablonie armii z 175 (Dothrakowie konni, Zelazne Wyspy tylko Harlaw, Polnoc 24%, typowa kraina ok. 21%) - jedna liczba zamiast osobnej tabeli (pytanie 3).
   Wiecej jazdy = wiekszy zold (ok. +10%) i wiecej paszy zima.
5. **6.11 - kon ginie tylko zabity.** Powstaje ksiega koni: skad kazdy kon (stadniny, lup, targ) i dokad (polegl, kupiony, lup zwyciezcy). Zamykamy konie z niczego: kon najemnika
   z wzorca (najemnik ma konia, jesli mial go jako zolnierz), konie startowe nowej druzyny, juczne nowych karawan (kupuja z targu). Egzotycznych wierzchowcow nikt juz
   nie kasuje z polek (dzis 9.5 dziennie w nicosc) - po prostu nie rodza sie tam, gdzie ich byc nie powinno.
6. **6.13 i 6.15:** konie po poleglych jezdzcach lordowie sprzedaja na targu (dzis leza w zbrojowniach - 2 018 wolnych) - v2: ponad jeden zapas liczony wedlug kategorii
   (ten sam przy kupnie i sprzedazy, wiec bez obiegu kup-sprzedaj); kon ze zdobyczy (np. "zmeczony") niesie piechura jak kazdy inny (dzis gra go nie liczy) - v2: naprawiamy
   to w samym liczniku gry, wiec liczy go tez szybkosc gry i udzwig; pasza liczy te same konie.
7. **6.3, 6.4, 6.18 - wies i towary.** Tkalnie i garbarnie biora tyle surowca, ile wynika z fizyki (6 lnu na 2 plotna, 5 skor na 2 skory) - wtedy len i welna przestaja sie
   gromadzic (dzis rosna co miesiac, prog etapu NIE). v2: jedna regula dla wsi: co 4 tygodnie wies robi wiecej towaru, ktory swiat zjada do konca (x1.3), a mniej towaru,
   ktory lezy (do ok. 95% zuzycia) - stos zostaje miedzy 30 a 60 dniami zuzycia; kasowanie (dzis 135 sztuk dziennie, glownie sol) konczy sie, gdy regulator ma dane (d56).
   Wioska rybacka daje ryby zamiast czesci zboza (tyle samo jedzenia), mlyn - zboze.
8. **6.9, 6.10 - warsztaty wedlug surowca i lore.** Tlocznia tylko tam, gdzie rosna winogrona (naprawiamy blad "strawy Polnocy" w CrashScribe), klimat wsi ustawiany przed
   rozdaniem warsztatow nowej gry, Dorne hoduje piaskowe rumaki (nie wielblady) - v2: wielblady rodza sie tylko w Qarthu; Twoich warsztatow nikt nie przestawia.
9. **6.17 - naprawy:** Twoje rece biora material ta sama regula co kowal (dzis 0.25 zamiast 0.20); AI naprawia tylko to, co nosi - lup obity idzie na targ i na zlom. To uwalnia
   skore, na ktora dzis czeka 1 700 sztuk w kolejce napraw i 2 149 w odziezy wojska.
10. **Kolejnosc (v2).** Najpierw pomiar (E6/0: ksiega uzbrojenia wedlug zrodla, ksiega koni, "co kuje kazda kraina") - nic nie zmienia w grze, kodowany na galezi scalonej
    porcji A i jedzie w tescie zbioru A etapu 5. Rownolegle teraz: 6.15 (konie a piechota), 6.9/6.10 (warsztaty, klimat, wierzchowce) i poprawka CrashScribe; 6.19 - gdy
    pomiar zamrozi funkcje krainy; 6.18 - po scaleniu pomiaru. Konie, wytop, dziury - po porcji A. Receptury, naprawy, regulator wsi i hodowli, rycerz z koniem - po kodzie
    kiesy ludu (B1). Wiecej rak i kopaln - dopiero po tescie etapu 6 (warunkowo, z liczb wytopu). **Test etapu 6 raz, na koniec** (120 dob nowej kampanii + bieg zimowy),
    po wgraniu zbioru B etapu 5. Hodowla: w tym tescie czynne podnoszenie koni bojowych, a przycinanie tylko "na sucho" - wlaczamy je po biegu rocznym, bo popyt na
    konie odwraca sie dopiero po ok. 360 dobach (L728).
11. **Wojska nie zmniejszamy** (Twoje 09.10 C): jesli zold jazdy albo zakupy koni zbija wojsko lordow ponizej 95 tys. w wojnie - poprawiamy przeplywy pieniedzy, nie wojsko.
12. **Trzy pytania** (rozdz. 6): czy zakaz unikatow dotyczy tez Twojej kuzni; co z konmi, ktore dzis "zjadaja" mieszczanie; v2: czy udzial jazdy krainy ma byc ten
    z szablonow armii 175 (jedna liczba). Reszte decyduje projekt.

---

## 1. Co dzis (liczby)

Z31 (L:N), doba 120 albo d91-120 (RA6). "Cel" - PLAN:197 albo propozycja projektu (rozdz. 4).

| Pozycja | Dzis | Cel etapu 6 | Dowod |
|---|---:|---|---|
| **cos na tulowiu (partie + zalogi AI)** | **93.6%** (partie 91%) | >= 75% (spelniony) | L:31426; RA6 rozdz. 1 |
| korpus wedlug szczebla: razem / partie / zalogi; t3+ w partiach | 74.5% / 63% / 89%; 25% | bez spadku; t3+ w gore (R1, R2) | L:31426 |
| brakuje sztuk w zbrojowniach AI (korpus / bron) | 261 058 (48 064 / 64 385), korpus +105/d w d90-120 | - (odczyt) | L:31426; RA6 rozdz. 3 |
| warsztaty: razem / korpus / helmy / tarcze / bron biala / luki, szt./d | 1694 / 110 / 404 / 149 / 387 / 173 (d31-60 korpus 313) | wedlug P174 4.1: 1700-2220 / 120-190 / 200-230 / 150-240 / 340-375 / 130-170 | L:23857-31497 |
| z tego t5-6 | 79/d | - | j.w. |
| odpuszczone cykle: brak surowca / w tym ruda | 752 / 584 (doba 120: 795 / 640) | ruda w dol (R1, R2) | L:31497 |
| nowe zbroje na polkach wedlug zrodla, szt./d | warsztaty 814, kupcy (przerzut) 1301, sakiewki ludzi 1183, lup 2141, zalogi 270 | jedna ksiega (6.6) | L:23774-31427 |
| polki: korpus t1-2 / t3-4 / t5-6 | 9 840 / 109 / 66 | - | L:31427 |
| AI kupuje korpusow/d | 652 (warsztaty ok. 110) | - | L:23775-31428 |
| **produkcja wedlug krainy i kultury** | **brak linii** | linia E6/0 | RA6 rozdz. 2 |
| sztuki obce w puli warsztatow (dzis, regula :525/:530) | Polnoc 61, Dorzecze 133, Korona 183, Nocna Straz 249, Wyspy Letnie 249; bandyckie 21-22 w kazdym miescie | 0 | `SCR\e6P\wynik.txt` [X] |
| ruda: wsie / zuzycie (zbrojne, narzedzia, strzelarze) | 301 / 242, 32, 12 ladunkow | jednostka lupa (R1) | L:31544 |
| miasta bez rudy | 30 (174b) - 34 (Ruda) z 97; Pyke 1151, Ten Towers 916, Barrowton 853, Stonehelm 715 ladunkow bez wywozu | <= 15 [S K13] | L:31434, L:31544 |
| drewno spalone w warsztatach zbrojnych | 1472 ladunki/d (dymarki w miastach) | v2: ok. 260 w miastach + dymarki przy kopalniach (5 x lupy) z lasu okregu i polki miasta handlowego - popyt na drewno bez zmiany | L:31545 |
| len / welna: zmiana zapasu w miesiacach 2-4 | +35 / +26 / +24; +42 / +27 / +3.8 /d | nie rosnie 3 miesiace z rzedu (dzis **NIE**) | L:7901-31561, L:31593 |
| skora / plotno (zapas d30 -> d120) | 622 -> 560; 879 -> 948 | nie ubywa > 10/d (spelniony) | L:7912, L:31572, L:31574 |
| odziez wojska czeka: skora / sukno / plotno | 2 149 / 3 047 / 2 887 (rosnie) | w dol | L:31674 |
| filc w taborach karawan | 8 801 z 9 862 | - (odczyt) | L:31559 |
| kasowanie towaru (BK `DeleteOverProduction` przez nasze `RawNoRot`) | 135/d (sol 83, drewno 40, glina 12) | 0 (zawor W3) | L:23939-31598 |
| kasowanie gry: uzbrojenie / konie | 0 / 0 | 0 | L:31596 |
| **jazda w partiach AI** | **8.3%** (9 224 z 110 505; d2 12.3%) | ok. 20% | L:31440 |
| jazda wedlug krolestw | Dothrakowie 45% (d5 61%), Targaryen 30%, Dolina 13%, Polnoc 5%, Zelazne Wyspy 1%, Wolni Ludzie 1% | v2: cel = udzial J+KL szablonu partii (175) | L:31431 |
| konie na polkach miast | 2 171 -> 11 622 (+79/d); L728: szczyt ok. 19.5 tys. (d360) -> 5 263 (d728) | v2: bez trendu w biegu rocznym (K4) | L:367 -> L:31539; L728 "Rynek broni" |
| podaz stadnin wedlug kategorii (71 stadnin, baza G x serca) | bojowe ok. 20-60/d, szlachetne 2.5-7/d [S] | v2: linia E6/0 (zmierzona) | G DefaultVillageTypes.cs:173-235 |
| czeka na konia swojej kategorii | 1 510 ludzi w 117 partiach | v2: nie rosnie w d91-120 (prog z rachunku koni E6/0) | L:31441 |
| konni bez konia w zbrojowni / wolne konie w zbrojowniach | 2 914 z 14 255 (20%) / 2 018 (zwykle 1897, bojowe 113, szlachetne 8) | v2: nadwyzka ponad zapas kategorii <= 2% konnych | L:31441 |
| egzotyczne wierzchowce kasowane z polek (LegendaryLaw) | 9.5/d w nicosc | 0 | L:23591-31253 |
| **linia "Konie" (bilans koni swiata)** | **brak** | reszta niewyjasniona ~0 | RA6 rozdz. 6 |
| naprawy: naprawione / lup obity / czeka na material (skora) | 1741/d / 3667/d / 2070 (1700) | czeka <= 1000 | L:23842-31511 |
| D728 (tendencja): produkcja warsztatow; konie na polkach; skory; kolejka napraw | 1660 -> 782 szt./d; 19 567 (d361) -> 5 353 (d728); 7 706 -> 1 476; 7 936 (skora 6 316) | - | L728:176329-183655, :90785, :183440, :183718, :183645 |

**Najwazniejsze przyczyny** (RB6 i sprawdzone w kodzie do tego projektu):
- **Kultura w warsztatach:** `WorkshopLaw.Candidates` - lokalne = kultura przedmiotu == `town.Culture` albo brak albo `neutral_culture` (K:WorkshopLaw.cs:525), pusto w calej
  LINII -> cala obca pula (:530); ta sama regula w `TownFletchers.CandidatesOf` (K:TownFletchers.cs:396, obce per koszyk :407), `ColdStart.Markets` (K:ColdStart.cs:154, :171 - bez obcych)
  i w grze `IsItemPreferredForTown` (G WorkshopsCampaignBehavior.cs:875-882) z zapasem na cala kategorie (`GetRandomItem` :1039-1047). `town.Culture` zmienia asymilacja BK
  (BK CultureData.cs:190-207). Kraina "na stale" juz jest: `PeopleBook.CultureAtCalibration` (K:PeopleBook.cs:834-840), uzywana przez `FreeHands.RealmOf` (K:FreeHands.cs:83-100).
- **Unikaty:** warsztaty notabli je odcinaja (`ArmsPricing.IsUnique` K:ArmsPricing.cs:85-93, K:WorkshopLaw.cs:523; spis K:RotUniques.cs:11-41 z kompletem `ramsay_*`). Droga gry
  (`GetRandomItemAux`, G :1049-1072) nie zna unikatow, a slownik `_itemsInCategory` gra buduje PRZED naszym `UniqueSpoils` (G :131-139, :161-183). Idzie nia: warsztat GRACZA z linia
  z wsadem (K:WorkshopLaw.cs:1516 przepuszcza), [v2: BK `WorkshopData` to martwy kod - `Tick()` pusty, prywatne `DoProduction` bez wolajacych, BK WorkshopData.cs:33-37] i warsztaty przed `GameStarted` (K:WorkshopLaw.cs:233;
  kopie startowe unikatow zdejmuje potem `UniqueSpoils`). `lion_sword` "Kingslayer's Sword" nie jest w spisie (RB6 1.3).
- **Ruda:** wydobycie 26 wsi `iron_mine` x `MineOutputMultiplier` 3 x `MineOutputStep` 1.5 / `BulkScale` (K:MaterialLaw.cs:175, :178); dymarka w MIESCIE - 5 ladunkow drewna na ladunek
  rudy + 12.5 kg drewna na kg metalu (K:WorkshopLaw.cs:160-176; Settings.cs:534, :538-539). **v2:** drewno na ten wegiel juz produkuje las wsi - `WoodlotStep` 1.6 dodany w 174.3
  "for the charcoal of the smiths" (K:Settings.cs:1273, K:VillageWoodlot.cs:54); cena wyrobu liczy dymarke w `FuelPerMetalKg` (K:HistoricalPrices.cs:251, :293-297).
- **Konie a pasza i udzwig (v2):** pasza zimy liczy konnych po glowie (takze bez konia), bez wolnych koni zbrojowni (K:WinterRealm.cs:287-300); udzwig gry daje 20 kg za kazdego
  wierzchowca z `NumberOfMounts` (G DefaultInventoryCapacityModel.cs:54, :78).
- **Kasowanie towaru:** wbrew RB6 4 (6.4) nasza latka juz siedzi na BK `DeleteOverProduction`: `RawNoRot.Prefix` (K:RawNoRot.cs:45-71, paczka 174.3) - ruda, sztabki, narzedzia,
  skora, plotno 0%, drewno/len/welna 0.2%, reszta 2% stosow > 500 (K:RawNoRot.cs:36-41). 6.4 zmienia tylko stawke.
- **Konie u lordow:** `Stables.OnSettlementEntered` (K:Stables.cs:698) - przerwa `AiMountBuyCooldownDays` 4, najwyzej `AiMountMaxPerVisit` (10 u Jeffa), 25% polki, najtansze
  konie bez kategorii, z sakiewki lorda (15% u Jeffa); **gra niezaleznie** (`PartiesBuyHorseCampaignBehavior.OnSettlementEntered`, G :60-135, nie latany przez nas) kupuje lordom
  losowo konie (:74-87) i **sprzedaje najcenniejsze konie z taboru, gdy stado jest warte > 10% kiesy** (:108-134) - nowe ustalenie tego projektu.
- **Kon a piechota:** gra liczy `NumberOfMounts` przy zmianie rosteru tylko dla koni bez modyfikatora (G ItemRoster.cs:431-456), a przy pelnym przeliczeniu - wszystkie (:546-576);
  `MarchPace.CountColumn` i model predkosci gry czytaja te liczbe (K:MarchPace.cs:38, :50; G DefaultPartySpeedCalculatingModel.cs:233-236).
- **Konie w nicosc / z niczego:** `LegendaryLaw.SweepMarkets` (K:LegendaryLaw.cs:205-226, `AddToCounts(-n)` :220); kon najemnika z wzorca (K:RecruitKit.cs:504); dary startowe
  nowej druzyny (`GiveInitialItemsToParty`, tylko licznik E4/8 - K:ArmsFromPeople.cs:970); juczne nowych karawan (G CaravanPartyComponent.cs:71-82 wedlug RD6); BK `RunStuds`
  (BK BKBuildingsBehavior.cs:313-340: 2%/d jeden kon bojowy do skrytki zamku bez rancza).

---

## 2. Reguly (jedna na zjawisko: skad sztuka, dokad sztuka)

**Zasady wspolne** (z etapow 1-5 i decyzji Jeffa): kazda sztuka uzbrojenia, towaru i kazdy kon ma zrodlo i ujscie w ksiedze (ksiega towarow 146 `GoodsLedger`, nowa ksiega
uzbrojenia i ksiega koni E6/0); zakup przez kase miasta - zawsze przez `TownPurse.BuyFloor` z A1 (RD6 W3); place rzemiosla od wartosci dodanej (D6-1, regula B1); jedna regula
gracz i AI; kazda paczka z wylacznikiem MCM (wylaczony = stan sprzed paczki); wyjatek per obiekt - licznik potkniec, bez globalnych wylacznikow (CLAUDE.md 7); zmiany
"tylko nowa kampania" (NK) wlaczone domyslnie (SP:888-890), w starym zapisie nieczynne (flaga w zapisie).

| # | Zjawisko | Regula (skad -> dokad) | Dzis | Paczka |
|---|---|---|---|---|
| R1 | **zelazo** | wies gornicza wydobywa rude i wytapia ja na miejscu: **1 lupa "Iron Bloom" = 15 kg surowki = metal ze 100 kg rudy** (liczba sztuk z kopalni bez zmian - stala 100 kg rudy na lupe, nie `BulkScale(iron)`); **v2:** wegiel dymarki = prawdziwe drewno (5 ladunkow na lupe): najpierw las wsi okregu kopalni tej doby (zdjety z wozu na targ), reszta kupiona wozem wsi z polki miasta handlowego (`TradeBound`) na nastepna dobe; brak drewna = mniej lup (ruda niewydobyta - nic z niczego); pozycja ksiegi drewna "dymarki przy kopalniach (las okregu a / targ b)"; cena lupy = metal + wegiel dymarki (czlon przeniesiony z `FuelPerMetalKg`); lupa -> woz wsi / kontrakt 174 / karawana -> kuznia miasta, ktora pali tylko wegiel kowalski (12.5 kg drewna na kg metalu) | ruda 100 kg do miasta, dymarka w miescie (1472 ladunki drewna/d) | R1 |
| R2 | **przetop** | zlom (stare uzbrojenie ponad rok popytu, wraki) i przetop gracza wracaja jako **lupy** (kg metalu / 15) - ta sama jednostka co R1; zadnej "nowej stali" (177: stal valyrianska tylko przekuciem w Qohorze) | `ArmsScrap` 0.5 rudy ze sztuki, `SmeltTab` gracza 1 ruda -> 3 surowki | R1 |
| R3 | **co kuje kowal miasta** | lancuch na koszyk (typ x tier): (1) wlasna kraina, (2) kraina pokrewna, (3) region lore, (4) wspolne strony morza (neutralne, noszone przez jednostki >= 4 kultur tej strony; dla lukow, kusz i tarcz t1-2 - kazda kraina tej strony), (5) nic; **wzory rodowe tylko w (1)**; stroje bandytow - nikt; **v2 lore:** khuzait - tylko wyroby ROT i wanilia bez metalu (stal i lamelki wanilii - nikt), freefolk - bez stali na kazdym poziomie (wyjatek braz Thennow), region WOLNE MIASTA na (3) - tylko wzory modulu ROT; kraina = `CultureAtCalibration` osady; ta sama regula dla warsztatow notabli, strzelarzy, dorobku startowego i drogi gry (warsztat gracza) | kultura miasta albo "bez kultury", przy pustej linii cala obca pula | M1 |
| R4 | **unikaty** | nikt ich nie wytwarza: warsztaty (wszystkie drogi), start gry; jedna funkcja `ArmsPricing.IsUnique`; **v2:** `lion_sword` - kraina "nikt" w `CraftRealm` (tylko kucie, pod kluczem M1), dopisanie do spisu unikatow osobno przez generator (rozdz. 7); kuznia gracza - pytanie 1 | droga gry przecieka | M1 |
| R5 | towar bez zbytu **i towar chciany (v2: jeden regulator, takze krok x1.3 z R6)** | `HarvestValve`: okno 56 dob, decyzja co 28 dob od d56, na towar (konie - na kategorie, K4); **w dol:** zuzycie < 90% produkcji i stos > 60 dni zuzycia -> mnoznik = 0.95 x zuzycie/produkcja, najwyzej x0.5 na decyzje, podloga 0.1; **w gore:** zuzycie >= 98% produkcji i stos < 30 dni zuzycia -> x1.3 (do 1 po cieciu, najwyzej `VillageOutputStep` ponad baze); pomiedzy - bez zmian (martwa strefa, bez zapadki); **nikt nie kasuje** (`RawNoRot` stawka 0 od pierwszej decyzji regulatora) | 135 szt./d w nicosc | W3 |
| R6 | len, welna, skory | warsztaty biora wsad wedlug wartosci i fizyki (K13 131); **v2:** krok x1.3 (K13 132) robi regulator R5 - **tylko produkcja, bez mnoznika popytu** (D6-4) | len i welna rosna | W1 (receptury), W3 (krok) |
| R7 | wioski poboczne | rodzaj wioski (VillageBook) zmienia tylko TOWAR okregu na targ: rybacka - ryby zamiast czesci zboza (**v2:** ryby dopisane liczba = u x zboze koncowe tej wsi, nie czynnik - wies bez ryb w typie ma z modelu 0), mlyn/wiatrak/spichlerz/farma - zboze; spichlerz i zywnosc warowni dalej od ludzi (D6-5) | rodzaj nieuzywany | W2 |
| R8 | typ warsztatu | warsztat towarowy tylko tam, gdzie jego wsad rosnie w okregu (wsie `TradeBound`) albo przychodzi kontraktem; klimat wsi ustalany PRZED rozdaniem warsztatow nowej gry | tlocznie bez winogron, klimat po rozdaniu | M2 |
| R9 | **kon - skad** | hodowla wsi (stadniny) i BK stadniny zamkow - jako hodowla z przyczyna, wedlug popytu **na kategorie** (regulator R5 w obie strony: ciecie kategorii bez zbytu, krok x1.3 kategorii chcianej, **v2:** przesuniecie do 25% produkcji zwyklych na bojowe w tej samej stadninie - te same sztuki); **nigdy z niczego**: najemnik ma konia tylko, jesli mial go jako zolnierz (pula koni weteranow), nowa druzyna - konie i zywnosc z polki siedziby za zloto rodu albo bez (v2: takze zywnosc), juczne karawan i taborow - z targu z kapitalu | 4 zrodla z niczego (+ zywnosc daru) | K1, K4, K5 |
| R10 | **kon - dokad** | **ginie tylko zabity**: w misji - prawdziwy zgon; w autobitwie AI-AI kon pada z zabitym jezdzcem x wspolczynnik W z bitew w misji (W = zgony koni / zgony jezdzcow, srednia z zapisu; do 50 obserwacji W = 1.0 - "AI-AI z proporcji z prawdziwych bitew", LW:102); ocalaly kon pokonanej partii - lup zwyciezcy (zbrojownie: DTE, tabor: nasza regula R14); nadmiar w zbrojowni AI -> targ (R11); kupiony przez mieszczan - pytanie 2; egzotyczny poza swoja kraina nie jest kasowany (nie rodzi sie tam - M2; wojsko go nie kupuje - `MountLaw`) | wolne konie zostaja w zbrojowni, 9.5/d w nicosc | K1, K2, M2 |
| R11 | nadmiar koni AI | **v2: jeden zapas koni** `Stables.Reserve(partia)` wedlug kategorii (tabor + zbrojownia razem) = brak jezdzcow kategorii + max(`AiMountSpareBuffer`, `AiSpareHorseShare` 0.10 x jezdzcy kategorii) - czytany przez kupno (Stables, K3) i sprzedaz (K2); konie ponad zapas x 1.5 (histereza) -> tabor -> sprzedaz na targu sciezka nadwyzek lordow i zalog (**`MenPurse.SellArmorySurplus`**, osobna galaz koni; kasa miasta przez `BuyFloor`); gra nie sprzedaje koni lordow sama (`PartiesBuyHorse` - zakup i sprzedaz koni pominiete, sprzedaz towaru zostaje) | konie leza (2018), gra sprzedaje najcenniejsze, trzy definicje zapasu | K2 |
| R12 | **jazda AI** | awans na konnego tylko z koniem (30.08, A14:1698); **v2: udzial jazdy partii = udzial konnych (J + KL) w szablonie, z ktorego partia sie rodzi** (`Clan.DefaultPartyTemplate`, przez obiekt; rod bez szablonu - szablon kultury) - ta sama liczba, ktora ustawia 175; decyduje, czy AI wybiera cel konny czy pieszy; rod kupuje konia **wlasciwej kategorii** (zwykly/bojowy/szlachetny): linia szlachecka i rodowa - stado wsi lenna przypietej do osady postoju (`BoundVillages`), potem targ, zaplata z budzetu sprzetu rodu (166); kon -> zbrojownia jezdzca (`Stables.Consume`) | najtansze konie, 4 doby przerwy | K3 |
| R13 | kon a piechota | **v2:** kazdy wierzchowiec w taborze (takze ze stanem i kulawy - jak pelne przeliczenie gry) i kazdy wolny kon zbrojowni niesie piechura albo juki (najpierw piechura); licznik gry `NumberOfMounts` naprawiony u zrodla - jedna liczba dla predkosci gry, udzwigu i MarchPace; pasza liczy te same konie | kon ze stanem i kon zbrojowni nie licza sie | K0, K5 |
| R14 | **dobytek rozbitej partii** | towar i konie z taboru rozbitej partii - lup zwyciezcy (jak zloto A6); bez zwyciezcy (rozwiazanie) - do najblizszej przyjaznej osady | "przepadlo z rozbitymi partiami" (ruda 34/d, drewno 89/d) | K1 |
| R15 | przewoz miedzy osadami | kazdy towar jedzie wozem: prawdziwa partia albo "woz bez partii" z czasem drogi (wzor `GarrisonCarts`); koniec przerzutu natychmiastowego (`SupplyDemand.DailyTrade`) | 1301 szt./d zbroi przerzutem | R3 |
| R16 | naprawy | material = `MendMaterialMaxShare` x (1 - stan) z receptury wedlug rodzaju (metal, drewno, skora, plotno) - **takze Twoje rece**; AI naprawia tylko to, co nosi, lup obity -> nadwyzki (`MenPurse.AiSurplusPlan`, v2); zaplata kowalom do kiesy ludu (jedno miejsce z B1) | gracz 0.25, AI naprawia lup | N1 |
| R17 | rece i kopalnie | wiecej rak rzemiosla = wiekszy udzial ludzi miasta (`WorkshopHandsPer1000People`), wiecej wydobycia = wydajnosc wsi gorniczych (czynnik W1) - bez nowego konta ludzi (D6-2); "ruda razem z rekami" (05.10, SP:572-574) | rece 0.10, kopalnie x1.5 | R2 (v2: warunkowo po tescie etapu 6) |
| R18 | **ksiegi** | kazda sztuka uzbrojenia i kazdy kon: zrodlo -> stan -> ujscie; reszta niewyjasniona liczona codziennie (wzor `MoneyLedger`, `GoodsLedger`) | brak dla uzbrojenia i koni | E6/0 |

**Pogodzone bez pytania Jeffa / poprawki do PLAN i rozpoznan:**

| Miejsce | Bylo | Jest (dowod) |
|---|---|---|
| PLAN:255-260 (H, I/J, K, N) | pytania "Ty" | rozstrzygniete: H wytop tylko NK - TAK; I/J Dorne piaskowe rumaki; K rycerz z koniem - TAK; N kuznia "zostawic filtr" (SP:841-848, :1190; RC6 pkt 1) |
| RC6 5.1 "co znaczy przetop" | pytanie | R2: zlom i wraki -> lupy w jednym rachunku metalu; Jeff 09.10 03:45 "zlom = przetapianie" (SP:810); bez nowego etapu "stal" |
| RB6 6 pkt 3 `lion_sword` | pytanie | unikat wedlug reguly Jeffa 05.10 (nazwa z przydomkiem postaci, K:RotUniques.cs:6-7) - v2: w M1 tylko kraina "nikt" w `CraftRealm` (kucie); do spisu unikatow osobno przez `tools/rot_uniques.py` (rozdz. 7) |
| RB6 6 pkt 2 stroje bandytow | pytanie | zadne miasto ich nie kuje - zgodnie z P175:84 (przedmioty kultur bandytow tylko dla jednostek Bandit) |
| RB6 6 pkt 5 "wspolne" | pytanie | wspolne = noszone przez jednostki >= 4 kultur jednej strony morza (76 sztuk, `SCR\e6P\chain.py`) - kuje je kazda kraina tej strony na poziomie (4) |
| RB6 6 pkt 1, RC6 5.4 pokrewienstwo i stroje rodow | do akceptacji | tabela M1 (lore); wzory rodowe tylko we wlasnej krainie (482 sztuk z rodem w nazwie/id) |
| RC6 5.3 / D6-6 "kultura krainy" | pytanie | `CultureAtCalibration` - ta sama definicja co wolne rece E4/7 i zima E4/1 (jedna zasada) |
| RB6 6 pkt 6 (6.4 latka czy MCM BK) | pytanie | latka juz jest (`RawNoRot`, K:RawNoRot.cs:85-98) - zawor W3 ustawia stawke 0; MCM BK Jeffa bez zmian |
| RB6 4 (6.4) "nie latamy DeleteOverProduction" | blad rozpoznania | latamy od 174.3 (`RawNoRot.Prefix`, L:31598 "oszczedzone wobec BK") |
| RB6 1.6 pkt 6 "bez nowej latki Harmony" (6.19) | - | potrzebne okno kontekstu miasta: zapas gry `GetRandomItemAux(kategoria)` bez miasta (G :1046) - prefiks + finalizer na `GetItemsToProduce` (G :820; v2: jedyny wolajacy `GetRandomItem` to G :829, wiec latka na `GetRandomItem` zbedna) |
| RB6 1.3 przeciek "start gry" | otwarty | zamkniety: kopie startowe unikatow zdejmuje `UniqueSpoils.OnSessionLaunched`; otwarty zostaje warsztat gracza z wsadem - M1 (v2: BK WorkshopData to martwy kod) |
| D6-1 place 35% | do Jeffa | regula B1 (projekt etapu 5): 35% **wartosci dodanej**; etap 6 tylko z niej korzysta (parametr ekonomii - SP:471-473) |
| D6-2 kto pracuje przy wytopie | do Jeffa | praca wsi gorniczej, wegiel z lasu okregu i z polki miasta handlowego (v2, K1-01), utarg przez woz wsi (pan 35 / wies 65 po B1), bez nowego konta ludzi |
| D6-4 krok popytu x1.3 | do Jeffa | tylko produkcja (po B1 mnoznik budzetu bylby zakupem z niczego) |
| D6-5 rodzaj wioski a spichlerz | propozycja | przyjeta (R7) |
| P175 "kupowanie koni wedlug kategorii - po pomiarze" (K:Stables.cs:731-736) | odlozone | K3: wedlug kategorii (1 510 czeka na konia swojej kategorii, L:31441) |
| CH:2092 pkt 4 "wielblad za awans AI" | otwarte | kon wolno, gdy `MountLaw.Allowed(jednostka, zwierze)` (K:MountLaw.cs:117) - jedna regula wierzchowcow |
| PLAN:197 "Skonczone" | bez progow 6.14 i 6.19 | dopisane (rozdz. 4) |

---

## 3. PACZKI etapu 6 w kolejnosci

Numeracja robocza 206-223 (wedlug RD6 wolne od 206; przed kodem grep CHANGELOG/STAN-PRAC/docs - "wpis 202" itp. to stare numery). Kazda paczka: osobny commit,
wpis CHANGELOG, wlasny wylacznik. Wielkosci: S <= 1 noc pracy, M, D (RD6 5).

| Paczka | Nr | PLAN | Co | Wiel. | NK | Zalezy od | Kod | Test |
|---|---|---|---|---|---|---|---|---|
| **E6/0** | 206 | 6.6, 6.11 (linia), 6.19 (na sucho) | POMIAR: ksiega uzbrojenia wedlug zrodla (zrodla/ujscia osobno od przeniesien); ksiega koni wedlug kategorii z podaza stadnin; "co kuje kazda kraina" wedlug lancucha M1 (odczyt); rozbicie mnoznikow wsi; konie mieszczan; zgony koni w misji; okno DTE | M | nie | scalenie A | **teraz, na galezi scalonego zbioru A** (v2) | w tescie zbioru A etapu 5 |
| E6/M1 | 207 | 6.19 | kucie wedlug kultury krainy, bez unikatow (R3, R4) | M | nie | E6/0 (zamrozony `CraftRealm`) | po E6/0 | etap 6 |
| E6/M2 | 208 | 6.9, 6.10 | warsztaty wedlug surowca, klimat przed warsztatami, wierzchowce kanonu (wielblady tylko Qarth), blad strawy Polnocy (R8) | S | tak | - (Stables.cs - koordynacja z E6/0) | **teraz, rownolegle** | etap 6 |
| E6/K0 | 209 | 6.15 | stan koni partii (pamiec dobowa), licznik gry u zrodla, kon zbrojowni niesie piechura, pasza z tego stanu (R13) | M | nie | - | **teraz, rownolegle** | etap 6 |
| E6/W2 | 210 | 6.18 | wioski poboczne wedlug rodzaju (R7) - dopisek ryb, ostatni krok `ProdPostfix` | S | nie | E4/11, E6/0 (ProdPostfix) | po scaleniu E6/0 | etap 6 |
| E6/K1 | 211 | 6.11 | kon ginie tylko zabity, lup zwyciezcy, dobytek rozbitych, konie i zywnosc z niczego zamkniete (R9, R10, R14) | M | nie | E6/0, A6 | po A | etap 6 |
| E6/K2 | 212 | 6.13 | jeden zapas koni wedlug kategorii; nadmiar koni AI na targ przez `MenPurse`; gra nie handluje konmi lordow (R11) | S | nie | K1, A1 | po A | etap 6 |
| E6/K5 | 213 | 6.16 | juczne z targu, udzwig 60 kg zamiast 20 (R9, R13); zapasowe konie = zapas K2 | S | nie | K1, K2, A3 | po A | etap 6 |
| E6/R1 | 214 | 6.1 | wytop przy kopalni z prawdziwym weglem + przetop (R1, R2) | D | **tak** | A1 (tekst w 6 sciezkach) | po A | etap 6 |
| E6/R3 | 215 | 6.7 | narzedzia wedlug zuzycia, uprzaz z kupcem, przewoz wozem (R15) | M | nie | R1, K1 | po A + R1 | etap 6 |
| E6/W1 | 216 | 6.3 | receptury wedlug wartosci (R6); krok x1.3 - v2 w W3 | S | nie | B1 | po B1 | etap 6 |
| E6/N1 | 217 | 6.17 | naprawy jedna regula, AI tylko noszone (R16) | S | nie | B1, A3 | po B1 | etap 6 |
| E6/K3 | 219 | 6.14 | jazda wedlug szablonu partii (175), rycerz z koniem rodu (R12) | M | nie | K1, K2 (czynne), B1, rachunek koni E6/0 | po B1 | etap 6 |
| E6/K4 | 220 | 6.12 | konie w regulatorze W3 wedlug kategorii (ciecie, krok, przesuniecie zwykle -> bojowe) + przeglad 71 stadnin | S | tak | K3, W3 | po W3 | krok i przesuniecie czynne w etapie 6, ciecie na sucho; ciecie czynne po biegu rocznym |
| E6/W3 | 221 | 6.3 (krok), 6.4 | jeden regulator wsi: w dol zamiast kasowania, w gore x1.3 dla towaru chcianego (R5, R6); stan w zapisie | S | nie | W1, R1 (drewno) | po W1 | etap 6 |
| E6/R2 | 218 | 6.2 | WARUNKOWO (v2): wiecej rak i kopaln (R17) | D | czesciowo | R1 i wynik testu etapu 6, B1 | po tescie etapu 6 | osobno |
| E6/W4 | 222 | 6.5 | WARUNKOWO: chleb/piwo/wino/deski, srebro, rzemioslo wsi BK | D | nie | B1, E4/6, wynik testu | po tescie etapu 6 | osobno |
| E6/R4 | 223 | 6.8 | WARUNKOWO: tarczownicy, placa w cenie, strzaly, helmy, dobytek osady | S | nie | test etapu 6, B1 | po tescie etapu 6 | osobno |

**Wspolny plik W1 (v2):** `MaterialLaw.ProdPostfix` (K:MaterialLaw.cs:166-212) - R1, W3 (z krokiem W1) i K4 dopisuja czynniki wzorem `(m - 1) x (1 + suma)` (przemienne, RD6 W1);
**W2 nie jest czynnikiem** (wies bez ryb w typie ma z modelu gry 0 - G DefaultVillageProductionCalculatorModel.cs:22-27): zboze x (1 - u) i dopisek ryb liczone jako OSTATNI krok.
Scalac raz, w kolejnosci E6/0 (licznik rozbicia) -> R1 -> W3 -> K4 -> W2, kazdy czynnik z wlasnym napisem (`TextObject`). Galezie: E6/0 na scalonym zbiorze A (te same pliki
co A: ArmouryBehavior.cs, CastlePurse.cs, MenPurse.cs, MoneyLedger*.cs); `McmSettings.cs` generowac `python tools/gen_mcm.py` po kazdym scaleniu, nie scalac recznie.
Rownolegle teraz: K0 (MarchPace, Stables - stan koni, WinterRealm), poprawka CS (osobny DLL), M2 (VillageClimate, WorkshopInputs, LegendaryLaw, MountLaw; Stables.cs - koordynacja z E6/0). **Pulapka MCM** (CLAUDE.md 7) - w `Armoury.json` Jeffa SA:
`SelfRepairMaterialFactor` 0.25, `MendMaterialMaxShare` 0.2, `AiMountMaxPerVisit` 10, `AiMountPurseShare` 0.15, `AiMountSpareBuffer` 2, `AiMountBuyCooldownDays` 4.0,
`CavalryNeedsMounts`, `AiBuysMounts`, `NorthernFareEnabled` (sprawdzone 12.10) - ich domyslnych NIE zmieniamy, kazda nowa regula ma nowy klucz.

---

### E6/0 - Pomiar etapu 6 (206, tylko log) - M - KOD TERAZ NA GALEZI SCALONEGO ZBIORU A (v2)

**Cel.** Stan wyjsciowy dla progow etapu, ktorych dzis nie da sie zmierzyc (RA6 rozdz. 1: "Konie" - brak linii; produkcja wedlug krainy - brak linii), i liczby do kalibracji
K1, K3, K4, W3. Gra bez zmian: zadna latka nie zwraca false, nie zmienia wyniku, roster ani zlota.

**Pliki**
| Plik | Zmiana |
|---|---|
| NOWY `ArmsBook.cs` | ksiega uzbrojenia (6.6): rodzaj (korpus, helm, nogi, rece, plaszcz, tarcza, bron biala, strzelecka, amunicja, uprzaz) x szczebel (t1-2, t3-4, t5-6); stan = polki osad + tabory + zbrojownie DTE. **v2 (K1-09): trzy grupy.** ZRODLA pierwotne: warsztaty (`WorkshopLaw` Flush, `Measure174b` ArrWorkshop), strzelarze (`TownFletchers`), start i `ColdStart`, kuznia gracza, komplety i dary z niczego (`RecruitKit`), Spoils (jesli z niczego); UJSCIA: zuzycie (wrak), zlom (`ArmsScrap`), `ArmsLeaks` (174), kasowanie gry, netto DTE (strata lupu - okno DTE ponizej); PRZENIESIENIA (wiersz informacyjny, kontrola netto 0): przerzut kupcow (`Measure174b` ArrTrade), sakiewki (ArrPurse), zalogi (ArrGarrison), lup sprzedany, zakupy `AiGear`, rozdanie lupu DTE miedzy zbrojowniami. Reszta = zmiana stanu - (zrodla - ujscia); mianownik = zrodla + ujscia |
| NOWY `HorseBook.cs` | ksiega koni (6.11) **wedlug kategorii** (zwykly / bojowy / szlachetny / juczny / egzotyczny): stan (polki osad, magazyny wsi, tabory, zbrojownie, skrytki zamkow `Settlement.Stash`, **v2: konie bohaterow** `Hero.BattleEquipment`/`CivilianEquipment` slot konia); ZRODLA pierwotne: produkcja wsi (`OnItemProduced` z G VillageGoodProductionCampaignBehavior.cs:168, kategoria Horse - **to jest podaz stadnin wedlug kategorii, rachunek dla K3/K4**), BK `RunStuds` (migawka skrytki), juczne nowych karawan (migawka w `CreateCaravanParty`, okno O17), dary startowe (`GiveInitialItemsToParty` - juz liczone K:ArmsFromPeople.cs:970), kon najemnika z wzorca (K:RecruitKit.cs:504), zamiany ROT (`HorseCensus`), kon nowego bohatera z szablonu; UJSCIA: polegle (misja - `OnAgentRemoved` wierzchowca; autobitwa - okno DTE), mieszczanie (migawka w nawiasie konsumpcji 110 `CastlePurse.ShelfPostfix`/`ConsumePostfix`), `LegendaryLaw.SweepMarkets`, rozbite partie (`GoodsLedger.OnPartyDestroyed` K:GoodsLedger.cs:790), BK `BKCaravansBehavior` po bitwie (RB6 6.11), zlom DTE (`ScrapCapPerCategory`), kon zmarlego bohatera, kasowanie gry (0); PRZENIESIENIA (informacyjnie, netto 0): zakupy i sprzedaze gry (`PartiesBuyHorse`), zakupy `Stables`, lup DTE miedzy zbrojowniami, Remount/PayInHorses |
| `WorkshopLaw.cs` + NOWY `CraftRealm.cs` | funkcja krainy (M1) w trybie odczytu: dla kazdego wyrobu warsztatu/strzelarzy/drogi gry poziom lancucha (wlasne, pokrewne, region, wspolne, OBCE dzis) i unikat na drodze gry - tylko licznik |
| `MaterialLaw.cs` | rozbicie mnoznikow `ProdPostfix` na pozycje (kopalnie/las, ladunek, MineralOnce, spustoszenie, rece L) i zima (`WinterBite`) - tylko licznik |
| `Stables.cs`, `HorseCensus.cs` | zakupy i sprzedaze gry (`PartiesBuyHorse`) wedlug kategorii - licznik; kategoria konia czekajacych; **v2:** rachunek koni wedlug kategorii raz na 7 dob (podaz stadnin + BK, zgony, zakupy wobec czekajacych wedlug kategorii) - podstawa celu i progu K3 |
| NOWY `DteWindow.cs` (v2) | prefiks + postfiks Harmony na prywatnym DTE `EveryoneCampaignBehavior.OnMapEventEnded` (AccessTools, jak K:GarrisonKit.cs:480): migawka zbrojowni i taborow tylko partii z `mapEvent` przed i po rozdaniu DTE; ten sam punkt dla K1 (zgon konia w prefiksie) |
| `ArmouryBehavior.cs` | `ArmsBook.Reset/HorseBook.Reset` w konstruktorze (:395 jak E5/0), `Daily` po `MoneyLedger.Daily` (stan liczony po wszystkich przeplywach dnia) |
| `tools/sprawdz_logi.py` | grupa `--grupa etap6` (progi rozdz. 4) |
| `tools/` (v2) | `szablony_jazdy.py` - udzial J+KL w szablonach partii po 175 (z XML ROT i XSLT CS), srednia wazona ludzmi partii z logu - do K3; przeliczenie `chain.py` z filtrem `MetalKg` i lore M1 v2 |

**Okna (dowod):** `VillageGoodProductionCampaignBehavior` -> `CampaignEventDispatcher.OnItemProduced` (G :168, :199) - nasluch jak `OreLedger.OnProduced` (K:ArmouryBehavior.cs:629);
`MapEvent` - istniejace okna 3.2/E4/7 (polegli) biegna PRZED `MapEventEnded` (G MapEvent.cs:1053, :2079); **v2: lup DTE** rozdaje sluchacz `MapEventEnded` DTE (DTE EveryoneCampaignBehavior.cs:139,
:529-562 -> `DistributeLootRandomly` :751), a kolejnosc naszych sluchaczy wobec DTE nie jest gwarantowana - dlatego latka na samej metodzie DTE (`DteWindow`); misja - `MissionBehavior.OnAgentRemoved` (wierzchowiec `IsMount`, stan Killed) tylko w bitwach gracza; `GetRandomItemAux` - istniejacy
postfiks (K:WorkshopLaw.cs:1479, wpiety :1584-1585) dopisuje klasyfikacje; `PartiesBuyHorseCampaignBehavior.OnSettlementEntered` (G :60-135) - prefiks/postfiks void z migawka taboru.
**Liczniki "na sucho" w tym samym biegu (v2, K1-08)** - dla progow paczek, ktorych nie da sie porownac z innym biegiem: M1 - cykl uzbrojenia, w ktorym stara regula dalaby wyrob, a lancuch nie
("cykle utracone przez M1"); K0 - sufit tempa kolumny bez K0 i z K0 w tym samym wywolaniu; R1 - lupy = sztuki z modelu kopalni tej doby; W2 - zboze zdjete = ryby dodane; regulator W3/K4 -
"mnoznik by byl" przy kluczu na sucho. Liczniki zostaja w paczkach (nie tylko w E6/0).

**Linie logu** (raz na dobe):
- `Uzbrojenie wedlug zrodla (E6/0): dzien N | korpus t1-2/t3-4/t5-6: zrodla: warsztaty a, strzelarze -, start b, kuznia gracza g, z niczego e | ujscia: zuzycie h, zlom i, przecieki 174 j, netto DTE k, kasowanie 0 | przeniesienia (netto ~0): przerzut kupcow f, sakiewki d, zalogi z, lup sprzedany c, rozdanie DTE l | stan: polki, zbrojownie, tabory | reszta x (y% zrodel + ujsc)` (wiersz na rodzaj; CSV `Logs\<sesja>\uzbrojenie.csv` co 7 dob).
- `Konie (E6/0): dzien N | kategoria zwykly/bojowy/szlachetny/juczny/egzotyczny: stan S (polki a, wsie b, tabory c, zbrojownie d, skrytki e, bohaterowie h) | zrodla: stadniny wsi f, BK stadniny g, juczne nowych karawan, dary startowe i, kon z wzorca j, zamiany ROT k, nowi bohaterowie | ujscia: polegle (misja l / autobitwa m), mieszczanie n, LegendaryLaw o, rozbite partie p, BK karawany q, zlom DTE r, zmarli bohaterowie | przeniesienia (netto ~0): gra kupila s / sprzedala t, Stables u, lup DTE w | reszta niewyjasniona x (7 dob: X, y% zrodel + ujsc)`.
- `Rachunek koni (E6/0, co 7 dob): kategoria: podaz/d (stadniny + BK), zgony/d, zakupy wojska/d, czeka (ludzi), polki - zapas w dniach | jazda partii a% (cel szablonow b%)`.
- `Warsztaty wedlug krainy (E6/0, na sucho): dzien N | sztuk: wlasne a, pokrewne b, region c, wspolne d, OBCE e, bandyckie f | unikatow na drodze gry u | najwiecej obcych: [miasto: n]` + `krainy: [kultura: korpus t1..t6, helm, tarcza]` co 7 dob.
- `Mnozniki wsi (E6/0): dzien N | ruda: kopalnie x3x1.5, ladunek /10, MineralOnce, spustoszenie, rece L, zima - srednie | drewno | len, welna, skory | konie`.
- `Zgony koni w misji (E6/0): bitew n, jezdzcow polegl a, koni padlo b, W = b/a (od poczatku kampanii)`.

**Klucz:** `Stage6MeasureLog` = true (`// counts every piece of arms and every horse from where it came to where it went, and what each realm's smiths would make - written to the log only, changes nothing in the game`).

**Test:** w tescie zbioru A etapu 5 (log): 0 bledow naszych modow; koszt <= 10 ms/dobe; **T1** reszta ksiegi uzbrojenia i koni policzona (bez progu - stan wyjsciowy);
**T2** "OBCE" > 0 w Winterfell (lannister_sword - kontrola, ze pomiar widzi dzisiejszy blad); **T3 (v2)** przeniesienia netto ~0 (+-1% ich sumy) w obu ksiegach; **T4 (v2)** okno DTE
wpiete ("DteWindow OK" w linii startowej), netto DTE policzone; **T5 (v2)** rachunek koni wedlug kategorii wypisany (podaz stadnin zmierzona - zastepuje szacunek 20-60 / 2.5-7).

**Ryzyka:** (1) migawki zbrojowni DTE w kazdej bitwie - koszt (licznik czasu jak `Cost174`, tylko partie z `mapEvent`); (2) misja w autotescie nie wystepuje - W z misji tylko z gry Jeffa (K1: W = 1.0 do 50 obserwacji);
(3) v2: latka na prywatnej metodzie DTE - zmiana nazwy w aktualizacji DTE = brak okna (linia startowa "BRAK", licznik zostaje bez netto DTE, bez wywrotki).
**E4/E5:** tylko odczyt; nasluch obok `TownFolk.OnItemProduced` (K:TownFolk.cs:192).

---

### E6/M1 - Kucie wedlug kultury krainy, bez unikatow (207, PLAN 6.19) - M - PO ZAMROZENIU CraftRealm Z E6/0 (v2)

**Zakres (R3, R4).**
1. `CraftRealm.CultureOf(ItemObject)` - kraina wyrobu TYLKO do produkcji (bez ruszania `Item.Culture`, ktora czyta DTE `GetItemsByTypeTierAndCulture`, kaskada 175 w CS,
   `UniqueLaw`, `HeroGear` K:HeroGear.cs:330-331); slownik raz na sesje:
   - literowki XML: `riverlands` -> river, `volantene` -> volantine, `Empire` -> empire, `Vlandia` -> vlandia;
   - bron rodow bez kultury wedlug prefiksu id: `lannister_`, `lion_sword` -> vlandia; `stark_` -> battania; `martell_`, `yronwood_`, `dorne_` -> aserai; `baratheon_` -> stormlands;
     `velaryon_`, `dragonstone_` -> dragonstone; `unsullied_`, `kopesh`, `serpent_kopesh` -> ghiscari; `katana`, `yiti_` -> yiti; `bronze_sword` -> freefolk (Thenn); `rapier` -> empire;
     `lyseni_` -> lyseni; `golden_company_` -> volantine;
   - bandyckie (`bandit_`, `tacky_bandit`, `looter`, kultury looters/sea_raiders/wights/whitewalker) -> "nikt";
   - **rod** wyrobu (`HouseOf`): lista rodow i instytucji z nazwy/id (Stark, Lannister, Casterly, Bolton, Karstark, Glover, Cerwyn, Manderly, Mormont, Umber, Tully, Blackwood,
     Bracken, Mallister, Frey, Arryn, Royce, Grafton, Tyrell, Hightower, Tarly, Redwyne, Florent, Baratheon, Dondarrion, Tarth, Martell, Dayne, Yronwood, Greyjoy, Harlaw,
     Velaryon, Celtigar, Targaryen, Westerling, Kingsguard, City Watch, Bloodrider, Golden Company) - 482 sztuk; tylko poziom (1);
   - "wspolne strony morza" = noszone przez jednostki >= 4 kultur tej strony (wzorce ROT-Troops, raz na sesje jak `CivilianOnly` K:WorkshopLaw.cs:603) - 76 sztuk
     (First Men Boots, Andal Boots, gambesony, Broad Arming Sword, luki "War Longbow"...); strona morza z `MountLaw.IsEssosCulture` (K:MountLaw.cs:30-41).
2. Kraina miasta = `PeopleBook.CultureAtCalibration(town.Settlement)` (K:PeopleBook.cs:834); stary zapis - zapas `Measure40.CultureAtStart` (jak `FreeHands.RealmOf`).
3. Lancuch **na koszyk typ x tier** (koszyk jak `TierOf` K:WorkshopLaw.cs:584), wybierany najnizszy dostepny poziom: (1) wlasna -> (2) pokrewne (tabela nizej, w kolejnosci) ->
   (3) region -> (4) wspolne i neutralne tej strony (luki, kusze i tarcze t1-2 - z kazdej krainy tej strony) -> (5) koszyk nie powstaje. **Filtry lore (v2, K1-10):**
   - Wolni Ludzie: na KAZDYM poziomie tylko wyroby bez metalu (`ArmsPricing.CostOf.MetalKg == 0`), wyjatek braz Thennow (`thenn_*`, `bronze_*`) - "Wolni Ludzie nie kuja stali";
     `wildling_armor3` "Wildling Chain Cloak" t5 i `wildling_sword` t5 - nikt (przychodza z lupu);
   - Dothrakowie (khuzait): poziom (1) = wyroby modulu ROT (`dothraki_*`, arakhy) i wanilia khuzait bez metalu (luki, skora, lekkie tarcze); stal i lamelki wanilii
     (`khuzait_heavy_armor`, `brass_lamellar_over_mail`, `eastern_lamellar_armor`, helmy `eastern_vendel_helmet`, `spiked_helmet*`, `steel_round_shield`, kopie `khuzait_lance_*`) - nikt;
     "Dothrakowie nie kuja" - reszte przywozi handel Vaes Dothrak;
   - region WOLNE MIASTA na poziomie (3): tylko wzory modulu ROT krain regionu; pule wanilii `empire` (206) i `nord` (144) zostaja wylacznie dla Braavos i Lorath na poziomie (1)
     (Volantis, Myr, Tyrosh, Valyria nie kuja rzymskich i nordyckich wzorow; puste koszyki - handel).
   Unikaty, legendy, `WorkshopForbiddenIds` - jak dzis odciete na kazdym poziomie. Tabela pokrycia nizej jest z `wynik.txt` v4 BEZ tych filtrow - przed kodem przeliczyc
   `chain.py` z `MetalKg` i filtrami (freefolk, khuzait, Wolne Miasta stracza koszyki - [S] kilka do kilkunastu na kraine).
4. Te sama funkcja w czterech miejscach: `WorkshopLaw.Candidates` (:518-530 - usunac `if (pool.Count == 0) pool = foreign;`), `TownFletchers.CandidatesOf` (:387-410, :407),
   `ColdStart.Markets` (:141-188 - pula na kraine miasta zamiast kultury), **droga gry**: `WorkshopLaw.RandomItemPostfix` (:1479) dostaje argumenty `ItemCategory __0, Town __1` -
   wynik spoza lancucha albo unikat -> losowanie od nowa z tej samej kategorii wedlug lancucha (wagi gry 1/(max(100, wartosc)+100), G :1058); pusto -> pusty `EquipmentElement`.
5. **v2 (KB M1-lion-sword):** `lion_sword` - kraina "nikt" w slowniku `CraftRealm` (pod kluczem M1, tylko kucie); `RotUniques.cs` BEZ zmian w tej paczce (statyczny spis czyta ok. 25 miejsc
   spoza kuzni i nie ma wylacznika; dopisanie przez `tools/rot_uniques.py` + `docs/ROT-UNIKATY.md` - osobna zmiana danych, rozdz. 7).

**Okna latek (dowod):** `WorkshopsCampaignBehavior.GetRandomItemAux(ItemCategory, Town)` - private, nasz postfiks juz wpiety (K:WorkshopLaw.cs:1584-1585); BK go nie lata (woluje
refleksja w wylaczonym `HandleItemAvailability`). **Nowe:** prefiks + finalizer na `GetItemsToProduce(Production, Workshop, out int)` (G :820-842; wolany z cyklu gracza :610 i notabla
:759; duza metoda - bez ryzyka wbudowania) - ustawia `[ThreadStatic]` miasto, bo zapas gry wola `GetRandomItemAux(kategoria)` bez miasta (:1046). **v2:** latki na `GetRandomItem` NIE ma -
jedynym wolajacym jest `GetItemsToProduce` (G :829), a BK `WorkshopData` to martwy kod (BK WorkshopData.cs:33-37: `Tick()` pusty, prywatne `DoProduction` bez wolajacych); mala metoda
grozilaby tez wbudowaniem przez JIT. Pusty wynik: w cyklu gracza dochod 0 -> `CanPlayerWorkshopProduceThisCycle` (:611) nie przepuszcza i wsad nie schodzi (:614-624); dla pewnosci
`PlayerGatePostfix` (K:WorkshopLaw.cs:1536) daje false przy dochodzie 0. Notabl - linie uzbrojenia ida nasza droga (`CyclePrefix` :206), gra ich nie losuje. `Debug.FailedAssert`
(G :837) w Shipping - no-op (`MBDebugManager.Assert` z pustym cialem, wg KB) - zamkniete.

**Tabela pokrewienstwa i regionow (lore; projekt decyduje, Jeff moze zmienic):**

| Region (3) | Kraina (id) | Pokrewne (2), w kolejnosci | Uzasadnienie |
|---|---|---|---|
| POLNOC | battania (Polnoc) | - | 367 wzorow, pelne pokrycie |
| POLNOC | nightswatch (Nocna Straz) | battania | kuznia Czarnego Zamku zbroi braci z Polnocy |
| POLNOC | skagosi (Skagos) | battania | wyspa Polnocy |
| (sam) | freefolk (Wolni Ludzie) | - | kosc, skora, braz Thennow; stali nie kuja (v2: filtr na kazdym poziomie) |
| (sam) | sturgia (Zelazne Wyspy) | - | "zelazna cena" - reszta z lupu |
| ANDAL (poludnie Westeros) | river (Dorzecze) | vale, crownlands | Trojzab miedzy Dolina a Korona |
| ANDAL | vale (Dolina) | river, crownlands | rycerstwo andalskie |
| ANDAL | crownlands (Korona) | dragonstone, stormlands, river | Krolewska Przystan |
| ANDAL | stormlands (Ziemie Burzy) | crownlands, reach | Marchia |
| ANDAL | reach (Reach) | stormlands, vlandia | rycerstwo poludnia |
| ANDAL | vlandia (Westerlands) | reach, river | - |
| ANDAL | dragonstone (Smocza Skala) | crownlands, valyrian | Velaryon i Celtigar - krew Valyrii |
| DORNE | aserai (Dorne) | stormlands, reach | Marchia Dornijska |
| WOLNE MIASTA | empire (Braavos) | nord, pentoshi | polnocne Wolne Miasta |
| WOLNE MIASTA | nord (Lorath) | empire, ibbenese | - |
| WOLNE MIASTA | pentoshi (Pentos) | empire, myrish | - |
| WOLNE MIASTA | myrish (Myr) | lyseni, tyroshi | Sporne Ziemie |
| WOLNE MIASTA | lyseni (Lys) | myrish, tyroshi, volantine | - |
| WOLNE MIASTA | tyroshi (Tyrosh) | myrish, lyseni | - |
| WOLNE MIASTA | norvos (Norvos) | qohorik | sasiedzi nad Noyne |
| WOLNE MIASTA | qohorik (Qohor) | norvos | - |
| WOLNE MIASTA | volantine (Volantis) | lyseni, valyrian | - |
| WOLNE MIASTA | valyrian (Valyria) | volantine | - |
| WSCHOD | ghiscari (Zatoka Niewolnicza) | volantine, qartheen | - |
| WSCHOD | qartheen (Qarth) | yiti, ghiscari | szlak jedwabny |
| WSCHOD | yiti (Yi Ti) | qartheen | - |
| (sam) | ibbenese (Ibben) | nord, empire | wielorybnicy Morza Drzacego |
| STEP | khuzait (Dothrakowie) | - | Dothrakowie nie kuja - targ Vaes Dothrak; v2: tylko wzory ROT i wanilia bez metalu |
| STEP | sarnor (Sarnor) | qohorik, khuzait | rydwany Sarnoru |
| (sam) | summer (Wyspy Letnie) | - | - |

**Kultura ROT -> kraina -> co kuje (lore) i uzupelnienie tierow** (wzory wlasne z RB6 1.4: helm/tulow/tarcza/bron biala; "po 6.19" = zrodlo koszykow korpusu T i helmu H tier 1-6
wedlug `SCR\e6P\wynik.txt` v4: w wlasne, p pokrewne, r region, n wspolne, . nie kuje - przywozi handel):

| id ROT | Kraina (miasta) | Co kuje wedlug lore | Wzory wlasne H/T/S/bron | Po 6.19: T / H | Uwagi |
|---|---|---|---|---|---|
| battania | Polnoc (9: Winterfell, White Harbor, Barrowton, Dreadfort, Deepwood Motte, Last Hearth, Karhold, Mormont Keep, Flint's Finger) | futra, skora gotowana, kolczuga, tartany gorali; plyta tylko rody (Bolton, Karstark, Glover, Cerwyn, Manderly); okragle tarcze Pierwszych Ludzi, topory, dlugi luk | 56/103/19/44 | wwwwww / wwwwww | dzis kuje Lannister Sword, katane, rapier, tarcze Westerlands - koniec; tarcze t1 ze strony morza |
| vlandia | Westerlands (4: Lannisport, Kayce, Hornvale, Surusbec) | karmazyn i zloto: kolczuga z wapenrokiem, brygantyna, plyta Casterly Rock; tarcze migdalowe; kusze; Lannister Sword, Kingslayer's Sword = unikat | 82/54/43/30 | wwwwww / wwwwww | jedyna kusza t1-2 Westeros |
| reach | Reach (5: Oldtown, Arbor, Ashford, Tumbleton, Lord Hewett's Town) | najliczniejsze rycerstwo: plyta Tyrell/Hightower, przeszywanice Tarly; lucznicy | 12/26/1/0 | wwwwpw / pppwww | bron cala od Ziem Burzy i Westerlands (bez rodowej) |
| stormlands | Ziemie Burzy (4: Storm's End, Weeping Town, Evenfall Hall, Stonehelm) | Baratheon, Dondarrion, Tarth: plyta, skora z kolkami; mloty i wlocznie | 14/17/2/6 | wpwwww / wwrwww | - |
| river | Dorzecze (9: Riverrun, Saltpans, Maidenpool, Stoney Sept, Fairmarket, The Twins, Harroway, Harrenhal, Seagard) | Tully, Blackwood, Bracken, Mallister: kolczuga z wapenrokiem, luskowa (Scalemail with Tabard - literowka riverlands) | 19/28/1/3 | wpwwww / wwrwww | Ravens' Teeth - zakazane (`WorkshopForbiddenIds`) |
| vale | Dolina (5: Eyrie, Gulltown, Sisterton, Heart's Home, Wickenden) | ciezka jazda: plyta Arryn, Royce, Grafton | 12/26/2/3 | wpwwww / pprwww | - |
| crownlands | Korona (2: King's Landing, Duskendale) | Zloci Plaszcze (City Watch Armor), Gwardia Krolewska (Kingsguard - tylko tu), stroje dworu | 14/24/0/1 | wwpwww / pprwww | tarcze z regionu |
| dragonstone | Smocza Skala (4: Dragonstone, Hull, Celtigar Keep, Sharp Point) | Velaryon, Celtigar: czern, plyta Dragonstone, kordelasy Velaryonow | 8/15/0/3 | wpwwww / wrrwww | velaryon_cutlass tylko tu |
| aserai | Dorne (5: Sunspear, Vaith, Planky Town, Starfall, Yronwood) | lekkie lamelki, jedwab i skora, wlocznie, adargi, dornijski luk; plyta tylko Dayne i Yronwood; piaskowe rumaki (M2) | 60/75/15/34 | w.wwww / wwwwww | korpus t2 - nie kuje (brak wzoru bez rodu w krainie i pokrewnych) |
| sturgia | Zelazne Wyspy (6: Lordsport, Pyke, Blacktyde, Ten Towers, Lonely Light, Pebbleton) | kolczuga i natluszczona skora, przeszywanice Greyjoy/Harlaw, plyta Harlaw; topory, okragle tarcze | 30/52/19/28 | www.ww / w.wwww | korpus t4 i helm t2 - nie kuje (lup) |
| freefolk | Wolni Ludzie (3: Frostfang's Camp, Thenn, Hardhome) | skory, kosc, braz Thennow; bez stali | 12/8/3/5 | w.wwww / wwwww. (v1, bez filtra - zawyzone) | v2: bez metalu na kazdym poziomie (poza brazem Thennow) - przeliczyc |
| nightswatch | Nocna Straz (1: Castle Black) | czern: przeszywanica, brygantyna Strazy, zwiadowcy | 4/5/0/0 | wpwwwp / w.ppwp | dzis kuje cala obca pule (249 obcych) |
| skagosi | Skagos (1: Driftwood Hall) | skory, Skagosi Armor | 2/2/2/2 | pppwwp / p.ppwp | jednorozce (koni) nie kuje sie |
| empire | Braavos (2: Braavos, Rhyos) | bravos: lekkie, rapier; Braavosi Plate; tarcze migdalowe | 49/44/29/26 | wwwwww / wwwwww | rapier tylko tu |
| nord | Lorath (2: Lorath, Tolero Aeksio) | zeglarze polnocy: kolczuga, skora, topory, Lorathi Plate | 27/27/11/41 | wwwwww / wwwwww | - |
| pentoshi | Pentos (2: Pentos, Bolozo) | ozdobna plyta kupcow, najemnicy | 7/4/2/2 | wppppw / pppwww | - |
| myrish | Myr (2: Myr, Myrth) | lekkie, Myrish Plate | 2/4/1/0 | wrwppw / prrpww | - |
| lyseni | Lys (2: Lys, Aquos Dhaen) | lekkie, Lyseni Elite Armor, lyseni_sword | 6/5/1/1 | wpwwpw / prrwww | - |
| tyroshi | Tyrosh (2: Tyrosh, Mhysa Faer) | piraci (Pirate Leather), Tyroshi Steel Armor | 5/10/1/3 | wrwwww / wrrwww | - |
| norvos | Norvos (2: Norvos, Menetragos) | brodaci kaplani (Bearded Priest Armor), dlugie topory | 5/3/0/2 | wrpwwp / rrwwww | - |
| qohorik | Qohor (2: Qohor, Draconys) | najlepsi kowale Essos (Tempered, Infused Armor; przekucie stali valyrianskiej - 177) | 9/7/2/3 | wrwwpw / rrpwww | - |
| volantine | Volantis (4: Volantis, Volon Therys, Valysar, Selhorys) | Zlota Kompania (Golden Company Armor), straz tygrysa | 9/9/2/1 | wwpwww / wrrwww | literowka volantene -> volantine |
| valyrian | Valyria (1: Elyria) | ruiny: Valyrian Soldier Armor; wyroby z "valyrian" w id - zakazane | 3/6/2/0 | pprwpw / rrrwrw | - |
| ghiscari | Zatoka Niewolnicza (6: Astapor, Yunkai, Meereen, Mantarys, Lhazosh, Tolos) | Nieskalani (Unsullied Armor), braz Ghis; kopesh | 8/5/3/2 | wprwwp / nrwwwp | tarcza t4 - nie kuje |
| qartheen | Qarth (3: Qarth, New Ghys, Qarkash) | Qartheen Armor, luk Qarthu | 2/2/2/3 | p.pwwp / npppww | korpus t2 i tarcze t3-4 - nie kuje |
| yiti | Yi Ti (1: Ifequeveron) | katana, miecze i lamelki Yi Ti | 3/5/2/3 | w.wwww / nwrrww | korpus t2, tarcze t3-4 - nie kuje |
| ibbenese | Ibben (3: Ibben, New Ibbish, Omber) | wielorybnicy: skora, topory, Ibbenese Armor | 3/3/2/3 | pppwww / pwppww | - |
| khuzait | Dothrakowie (2: Vaes Dothrak, Vaes Diaf) | skora, arakhy, luki kompozytowe | 19/31/13/23 | wwwwww / wwwwww (v1, z plyta wanilii) | tarcza t4 - nie kuje; v2: stal i lamelki wanilii (t4-6) - nikt, korpus/helm t4-6 pewnie puste - przeliczyc |
| sarnor | Sarnor (2: Kyth, Saath) | rydwany, Sarnori | 2/2/1/0 | wwpppp / pppwwp | - |
| summer | Wyspy Letnie (1: Lotus Bay) | lekkie, plaszcze z pior (luk z goldenheart = unikat) | 5/3/0/0 | w.ww.. / n.nww. | 32 z 66 koszykow nie kuje - handel |

Bilans lancucha (66 koszykow na kraine, v4): wlasne + pokrewne + region + wspolne = 52-55 w Westeros (Wolni Ludzie 46) i 55 w Wolnych Miastach (Dothrakowie 47,
Qarth/Yi Ti 45, Wyspy Letnie 34); dzis 46-55 koszykow, ale z obcymi (RB6 1.4, `wynik.txt`). Cale linie bez zadnego wzoru (poza uprzeza t1/t4, ktorej nie ma w danych):
tarcza t4 - Dothrakowie, Zatoka, Qarth, Yi Ti, Sarnor; tarcza t3 - Qarth, Yi Ti, Wyspy Letnie. **Uzupelnienie brakujacych tierow** - bez dorabiania wzorow: koszyk pusty w krainie
dostarcza (a) przewoz kupcow (R15 - wozem), (b) zakup AI koszyka typ x tier i zastepczy t-1 (K:AiGear.cs:564, `GarrisonCarts` Bucket), (c) kontrakty 174. Nowych przedmiotow
do XML nie dodajemy (to zmiana danych ROT, poza Armoury).

**Pliki:** NOWY `CraftRealm.cs` (z E6/0 - tryb czynny), `WorkshopLaw.cs` (:518-530, :1479, wpiecie okna kontekstu `GetItemsToProduce`), `TownFletchers.cs` (:387-410), `ColdStart.cs` (:141-188),
`Settings.cs` + `McmSettings.cs` (`python tools/gen_mcm.py`). v2: bez `RotUniques.cs`.

**Klucz:** `WorkshopCraftByRealm` = true (`// each realm's smiths make the arms and armour of their own land - their own patterns first, then a kindred realm's, then those common to their region and their side of the sea, never a great house's livery from another realm and never a unique piece; what a realm cannot make, the traders bring. The realm of a town is the one of its land, not of its current lord`).

**Linie logu:** `Warsztaty wedlug krainy (207): ...` (format E6/0, "OBCE" ma byc 0) + dopisek do "Warsztaty: dzien" (K:WorkshopLaw.cs:1144): `koszykow bez wzoru w krainie n (najwieksze: ...)`;
start: `CraftRealm (207): krain 31, wyrobow z kraina z prefiksu a, literowek b, rodowych 482, wspolnych 76, bandyckich c, odcietych lore (freefolk stal d, khuzait stal e, wanilia Wolnych Miast f), nikt (lion_sword) 1; okno kontekstu: GetItemsToProduce OK; BRAK: -`.

**Test i progi (v2 - w tym samym biegu, K1-08):** obce = 0 kazdej doby; unikaty z drogi gry = 0; **cykle utracone przez M1** (licznik na sucho: stara regula dalaby wyrob, lancuch nie)
<= 5% cykli uzbrojenia, a rece z pustej linii przechodza do innej linii cechu (L:31497); korpus t3+ w partiach nie spada ponizej 25% (bezwzglednie); "cos na tulowiu >= 75%" (Skonczone);
w zapisie sprzed E4 - kraina z `Measure40.CultureAtStart`, bez bledu.
**Ryzyka:** (1) male krainy (Wyspy Letnie, Qarth, Yi Ti) kuja mniej roznych sztuk - linia pusta = rece do innej linii cechu (przeniesione z cechow bez roboty, L:31497); (2) bez
strojow bandytow linia heavy (t4) traci jedyna neutralna sztuke (RB6 1.4) - zostaja wlasne/pokrewne; (3) kategorie warsztatu sa z tieru PRZED prawami CS (`ArmorTierLaw`) - liczby
`wynik.txt` przyblizone, w kodzie koszyk z `TierOf`; (4) `HouseOf` po nazwie - lista w kodzie (jak `RotUniques`), narzedzie `tools/` generuje spis do przegladu.
**NK:** nie (dzialanie od wczytania; dorobek startowy `ColdStart` - tylko nowa kampania). **E4/E5:** kraina z E4/7 (D6-6); `WorkshopLaw.cs` zmienia tez A1 (:317-319 - daleko, tekst).

---

### E6/M2 - Warsztaty wedlug surowca i lore, klimat, wierzchowce (208, PLAN 6.9 + 6.10) - S - NK - KOD TERAZ, ROWNOLEGLE

- **Zakres (R8):** (a) **blad strawy Polnocy** - CS:Mends.cs:2134 i :2140 biora `st.Village.Bound` (zamek przy wsi zamkowej) zamiast `TradeBound` (miasto targowe), wiec tlocznia w
  miescie targowym zostaje bez winogron; poprawka w CrashScribe (osobny DLL - osobny commit i wpis); **v2:** w tej samej poprawce petla :2145-2150 pomija `ws.Owner == Hero.MainHero`
  (dzis przestawia tez tlocznie gracza). (b) **bramka "przerobka tylko przy surowcu"** (LWA A3): postfiks na `FindTotalInputDensityScore` (G WorkshopsCampaignBehavior.cs:1116; wybor typu
  `DecideBestWorkshopType` :1278 przy starcie :1258, przy bankructwie :1097 i przy przejeciu warsztatu gracza :1086) - **v2: wynik x0.01 (nie 0)** dla typu, ktorego wsad nie rosnie we
  wsiach `TradeBound` miasta ani nie przychodzi kontraktem 174 - przy samych zerach gra losuje typ calkiem na slepo (G :1319-1336, `WorkshopType.All[RandomInt(1, ...)]`), a tak wybierze
  typ z wsadem, jesli jest, i najmniej zly, jesli nie ma zadnego; przy wczytaniu jednorazowe przestawienie warsztatow bez wsadu (jak CS NorthernFare, kapital - K:WorkshopTrade.cs:172) -
  **v2:** bez warsztatow gracza (`ws.Owner == Hero.MainHero` - licznik), flaga "zrobione" `arm_e6_inputs` w `SyncData` (5.2 pkt 4).
  (c) **6.10 klimat przed warsztatami:** `VillageClimate`/`WesterosClimate`/NorthernFare biegna dzis w `OnSessionLaunched`, PO `BuildWorkshopsAtGameStart` (LWA:337-344; G :131-139 przy
  i == 10) - prefiks na `BuildWorkshopsAtGameStart` wola przestawienie typow wsi najpierw (nowa kampania). (d) **wierzchowce kanonu (v2, K1-11):** w Dorne nie ma "wsi wielbladow" -
  4 stadniny `desert_horse_ranch` daja rumaki (`aserai_horse` "Dornish Rounsey", `t2_aserai_horse` "Sand Steed", `t3_aserai_horse` "Thoroughbred Sand Steed") RAZEM z wielbladami
  (camel 0.3, war_camel 0.08, pack_camel 0.3 - G DefaultVillageTypes.cs:225-235); te same stadniny sa w Lys, Myr, Tyrosh, Volantis (3) i Zatoce (2), a Qarth (jedyny z jezdzcami na
  wielbladach - `qartheen_camel_rider`, `qartheen_cameleer`, `qartheen_master_cameleer`) ma tylko `steppe_horse_ranch` bez wielbladow. Regula: **wielblad = qartheen** (`MountLaw` :171 bez
  `aserai`) [D 09.10 I/J]; postfiks listy produkcji wsi (BK `GetProductions`, istniejacy `MineralOnce` - jedna latka) przenosi wage wielbladow ze stadnin pustynnych poza Qarthem na
  konie tej stadniny (Dorne: `aserai_horse`; reszta: `sumpter_horse` i kon kultury), a stadninom Qarthu dodaje wielblady w tej samej wadze kosztem `khuzait_horse` (ta sama suma sztuk,
  nic z niczego); mamut tylko giganci, jednorozce Skagos, smoki Daenerys i misja gracza - bez zmian
  (`MountLaw`, `DragonUnmount`); `LegendaryLaw.SweepMarkets` przestaje kasowac (K:LegendaryLaw.cs:220) - zostaje licznik "egzotyczne na polkach nie u swoich" (kupuja je karawany
  jako juczne, Jeff 30.08 "karawany okej"); kupcy wojskowi juz je pomijaja (`Stables` wielblad/rydwan tylko u swoich, K:Stables.cs:797-798; rozszerzyc na `MountLaw.AllowedForSettlement`).
- **Pliki:** CS:`Mends.cs` (:2134, :2140, :2145-2150), `VillageClimate.cs`, NOWY `WorkshopInputs.cs` (bramka), `LegendaryLaw.cs`, `MountLaw.cs` (:171), `MineralOnce.cs` (lista produkcji - wielblady), `Stables.cs`, `Settings.cs`, `ArmouryBehavior.cs` (`SyncData` flaga).
- **Klucze:** `WorkshopsFollowInputs` = true (`// a town's trade workshop is the kind its own villages feed - no wine press where no grapes grow; at the start of a new campaign and once when a game loads; your own workshops are never changed`), `ClimateBeforeWorkshops` = true, `SandSteedsInDorne` = true (`// camels are bred only in Qarth; the desert studs of Dorne and of the Free Cities breed horses in their place`); poprawka CS pod istniejacym `NorthernFareEnabled` (bez nowego klucza - to blad).
- **Linie:** `Warsztaty wedlug surowca (208): przestawiono n warsztatow [miasto: typ -> typ], warsztaty gracza bez wsadu g (nietkniete), miasta bez typu z wsadem m, typow bez wsadu przy starcie 0`; `Wierzchowce kanonu (208): wielblady przeniesione na konie w stadninach a (sztuk/d b), wielblady w Qarthu c/d; egzotyczne na polkach nie u swoich e (kasowane 0)`.
- **Test:** nowa kampania: tloczni bez winogron w okregu 0, warsztatow bez wsadu 0 (poza miastami bez zadnego typu z wsadem - licznik); LegendaryLaw kasuje 0; **wielbladow wyprodukowanych poza Qarthem 0** (produkcja `OnItemProduced`, nie polki); warsztaty gracza nietkniete.
- **Ryzyka:** przestawienie typu zmienia utarg warsztatu i place (kiesa ludu B1) - jednorazowo; NK - stary zapis tylko (a) i jednorazowe (b) przy wczytaniu. **E4:** `VillageClimate` ma krok E4/1 (D.5 Skagos owce) - scalanie tekstowe.
  **v2:** przed kodem (d) sprawdzic, ktora lista produkcji czyta dobowy tick wsi przy BK (BK `GetProductions` czy G `VillageType.Productions` - E6/0 `OnItemProduced` to pokaze) i latac te,
  ktora produkuje; `GiveInitialItemsToParty` czyta `VillageType.Productions` - zamyka ja K1; jezdzcy na wielbladach poza Qarthem (jesli sa w szablonach) - lista z narzedzia przed kodem.

### E6/K0 - Kon ze stanem i kon zbrojowni niesie piechura; stan koni partii; pasza (209, PLAN 6.15) - M - KOD TERAZ, ROWNOLEGLE

- **Zakres (R13), v2:**
  (a) **licznik gry u zrodla** (KB K0-model-gry): postfiks `ItemRoster.OnRosterUpdated` (G ItemRoster.cs:431-456) dolicza do `NumberOfMounts`/`NumberOfPackAnimals` wierzchowce i juczne
  Z modyfikatorem dokladnie tak, jak liczy je pelne przeliczenie gry (`CalculateCachedStats` :546-576) - wynik przestaje zalezec od historii rosteru, a jedna liczba sluzy modelowi predkosci
  gry ("Footmen on horses" G DefaultPartySpeedCalculatingModel.cs:131-136, :233-236), udzwigowi gry (G DefaultInventoryCapacityModel.cs:54, :78) i MarchPace. Kon kulawy liczy sie jak w
  pelnym przeliczeniu gry (wyjatek "poza kulawym" z v1 porzucony - jedna liczba, kulawy niesie piechura stepa). Goraca sciezka (kazda zmiana kazdego rosteru) - postfiks wychodzi od razu,
  gdy przedmiot nie ma `HorseComponent` albo modyfikator jest null; licznik kosztu.
  (b) **stan koni partii w pamieci** (KB K0-koszt): `Stables.HorseState(partia)` = wolne konie zbrojowni wedlug kategorii, jezdzcy bez konia, jezdzcy z koniem - liczony raz na dobe
  i przy `Remount`/`PayInHorses`/zakupie/sprzedazy (te i tak licza `Balance`), z pieczatka doby; `MarchPace.SpeedPostfix` (wolany przy kazdej zmianie sciany siatki - G MobileParty.cs:3056-3070)
  tylko CZYTA: luzaki = `NumberOfMounts` (z (a)) + wolne konie zbrojowni z pamieci. Ten sam stan czytaja K2 (`Stables.Reserve`), K5 i pasza.
  (c) **pasza z tego samego stanu** (K1-12): `WinterRealm.AnimalNeed` (K:WinterRealm.cs:287-300) = konie w taborze + jezdzcy Z koniem + wolne konie zbrojowni (dzis: konni po glowie, takze
  2 914 bez konia, bez 2 018 wolnych koni zbrojowni).
  Nasz sufit kolumny (`SpeedPostfix` Priority.Last, K:MarchPace.cs:59) tylko OBNIZA (:24) - dlatego (a): bez niego kon ze stanem nie przyspieszalby kolumny tam, gdzie naturalna predkosc gry jest nizsza niz sufit.
- **Dowod bledu:** G ItemRoster.cs:431-456 (przyrost liczy tylko `ItemModifier == null`) wobec :546-576 (pelne przeliczenie liczy wszystkie) - wynik zalezy od historii rosteru.
- **Klucze:** `SpareHorsesCarryFootmen` = true (`// every spare horse in the baggage - also a worn or lame one from the spoils - and every free horse in the troops' armoury carries a footman on the march; the game counts such horses the same way everywhere`), `FodderFromHorseState` = true (`// winter fodder is eaten by the horses a party really has - in the baggage, under its riders and free in the armoury - not by riders who have lost theirs`).
- **Linia:** dopisek do linii marszu: `luzaki: bez modyfikatora a, ze stanem b (w tym kulawe c), wolne w zbrojowni d | sufit tempa AI bez K0 / z K0 (na sucho w tym samym wywolaniu) | koszt: OnRosterUpdated n wywolan, x ms; SpeedPostfix y ms`; dopisek do linii zimy: `pasza: konie partii a (dawniej po glowie b)`.
- **Test (v2, w tym samym biegu):** sufit z K0 >= sufit bez K0 w kazdej partii (licznik na sucho); koszt K0 <= 2 ms/dobe; 0 bledow; po wczytaniu `NumberOfMounts` = wynik `CalculateCachedStats` (kontrola raz na dobe na 20 losowych partiach, roznica 0). **Ryzyko:** szybsze kolumny - styk z etapem 7 (tempo z ksiag, 7.1-7.2); udzwig gry +20 kg za kazdego konia ze stanem (zgodnie z pelnym przeliczeniem gry); pasza ok. -900 koni swiata - prog regresji E4 (glod lordow zima) przeliczyc w biegu B.
  **E4/E5:** `WinterRealm` (E4/1) - jedna funkcja `AnimalNeed`, wariant BK (a) tez z niej korzysta.

### E6/W2 - Wioski poboczne wedlug rodzaju (210, PLAN 6.18) - S - PO SCALENIU E6/0 (wspolny ProdPostfix; liczby wiosek po tescie koncowym E4)

- **Zakres (R7, D6-5):** `arm_map_villages.tsv` kolumna `model` (fishing 400, mill 358, windmill 98, granary 91, farm 446, village 1053 - RB6 4) czytana przez `VillageBook`
  (K:VillageBook.cs:69, :225). Okreg z wioskami rybackimi: u = udzial ludzi wiosek rybackich w ludziach okregu (VillageBook) x 0.5 [projekt]. **v2 (KB W2-ryby-z-zera):** ryb NIE da
  sie zrobic czynnikiem - wies, ktorej typ nie ma ryb w `VillageType.Productions`, dostaje z modelu gry 0 (G DefaultVillageProductionCalculatorModel.cs:22-27), a czynnik razy 0 = 0
  (zboze by zniknelo bez ryb). Wiec, jako OSTATNI krok `ProdPostfix` (po E6/0, R1, W3, K4): zboze wsi -> czynnik x (1 - u) liczony od wyniku koncowego, a wynik zapamietany (wies, doba);
  ryby tej wsi -> dopisek bazy tak, by wynik wzrosl dokladnie o T = u x zboze_koncowe: `Add(T / (1 + suma czynnikow ryb))` (`ExplainedNumber` mnozy baze przez 1 + suma, a po nas nikt
  juz nie mnozy - `_depth` 1, Priority ostatni). W liscie BK zboze jest przed dopisana ryba (BK EconomyPatches.cs:944-975), wiec pamiec zboza jest gotowa; brak pamieci (inny wolajacy,
  np. podpowiedz) - ryby bez dopisku, licznik. Ryby dopisane do listy produkcji wsi postfiksem BK `GetProductions` (istniejacy `MineralOnce`) tylko gdy u > 0.
  Mlyn, wiatrak, spichlerz, farma - zboze (jak typ wsi gry). Spichlerz E4/6 (`Famine` q z modelu zywnosci, K:Famine.cs:398) i zywnosc warowni E4/11 - bez zmian.
- **Klucz:** `HamletKindsInDistrict` = true (`// the hamlets of a district shape what it sends to market: fishing hamlets land fish in place of part of the grain, mills and granaries keep it grain`).
- **Linia:** `Wioski wedlug rodzaju (210): okregow z rybakami n, zboza -a, ryb +b, kontrola a - b = 0; wsi bez pamieci zboza c`. **Test (v2, tozsamosc w biegu):** zboze zdjete = ryby dodane
  kazdej doby (kontrola 0, +-1 sztuka na zaokraglenia); ryby na targach okregow rybackich > 0; glod warowni i lordow - bez regresji wobec testu koncowego E4 (bezwzglednie).
- **Ryzyko:** cena ryb i zboza w miastach nadmorskich; `GetProductions` - cudzy kod (BK), juz latany przez nas; NavalDLC i BEE owijaja model wsi (BaseModel x mnozniki) - nasz krok
  na najbardziej zewnetrznym poziomie (`_depth` 1), jak dzis. **E4:** E4/11 umowilo "bez trzeciego czynnika" w ProdPostfix (PLAN-KODU-E411.md:343) - swiadomie dopisany krok, jedna linia opisu.

---

### E6/K1 - Kon ginie tylko zabity; dobytek rozbitych; konie z niczego zamkniete (211, PLAN 6.11) - M - PO SCALENIU ZBIORU A

- **Zakres (R9, R10, R14):**
  1. **Zgony koni w bitwie:** misja - prawdziwy zgon wierzchowca zdejmuje konia ze zbrojowni wlasciciela (sprawdzic w DTE przy kodzie, czy zabity kon juz schodzi - [W]);
     autobitwa AI-AI - **v2 (KB E60-okno-po-DTE): w prefiksie `DteWindow` na DTE `EveryoneCampaignBehavior.OnMapEventEnded`** (DTE :139, :529-562; nasze sluchacze `MapEventEnded`
     nie maja gwarantowanej kolejnosci wobec DTE), czyli przed rozdaniem zbrojowni pokonanych: za kazdego zabitego jezdzca (polegli E4/7 z okna `MapEvent`, ktore biegnie wczesniej -
     G MapEvent.cs:1053, :2079) partia traci W koni jego kategorii ze zbrojowni (ulamek - losowanie z rowna szansa, licznik); W z linii "Zgony koni w misji", do 50 obserwacji 1.0
     (w autotescie przez caly bieg 1.0 - misji nie ma). Rzad wielkosci [S]: 501 zabitych/d (d91-120: 769) x 8-20% jazdy = ok. 40-150 koni/d - wchodzi do rachunku koni (K3, K4).
  2. **Dobytek rozbitej partii (R14):** `GoodsLedger.OnPartyDestroyed` (K:GoodsLedger.cs:790-810) - zamiast "przepadlo": tabor (towar i konie) do zwyciezcy wedlug wkladu (ta sama
     zasada co zloto A6 `BattlePlunder`), bez zwyciezcy - na polke najblizszej przyjaznej osady (kasa osady nie placi - to porzucony dobytek; licznik). BK `BKCaravansBehavior`
     wyrzucajacy zwierzeta po bitwie (RB6 6.11) - ta sama regula.
  3. **Kon najemnika** (K:RecruitKit.cs:504): pula koni weteranow - konny zolnierz zwolniony do karczmy (E4/10 `VetDisband`) zabiera swojego konia ze zbrojowni (kon wlasnoscia
     zolnierza, paczka 160); najem konnego - kon z puli tej karczmy; brak konia - najemnik przychodzi jako pieszy (E4/10 juz ma typ pieszy, `Veterans.TypeFor` K:Veterans.cs:249)
     [D 08.10 "albo najemnik ma konia ... albo przychodzi bez konia", SP:656-659].
  4. **Dary startowe nowej druzyny** (`GiveInitialItemsToParty`, dzis tylko licznik K:ArmsFromPeople.cs:970): **v2 (K1-14): konie i zywnosc** (ta sama metoda daje zywnosc z niczego -
     G HeroSpawnCampaignBehavior.cs:280-309, `IsFood` 0.1) - z polki siedziby za zloto rodu (budzet sprzetu 166) albo bez; jedna latka, jedna regula.
  5. **Zamiany ROT** na typ konny (HorseCensus "echo werbunku"): jak awans - kon z taboru/zbrojowni, inaczej typ pieszy (rozszerzenie `RotHorseGuard`).
- **Pliki:** `HorseBook.cs` (tryb czynny: przyczyny), `Stables.cs`, `GoodsLedger.cs`, `RecruitKit.cs`, `Veterans.cs`/`PeopleBook.Veterans.cs` (pula koni w zapisie `arm_vethorses` przez `SaveText.Sync`),
  `ArmsFromPeople.cs`, `RotHorseGuard.cs`, `CasualtyLoot.cs` (okno poleglych), `Settings.cs`.
- **Pliki (v2):** + `DteWindow.cs` (z E6/0 - prefiks czynny: zgon konia).
- **Klucze:** `HorsesDieOnlyKilled` = true (`// a horse dies only when it is killed: in your battles as it falls, in battles between AI lords along with its killed rider at the rate seen in real battles; a horse that lives through a lost battle is the victors' spoil`), `HorseDeathRatioMinBattles` = 50, `SpoilsOfBrokenParties` = true, `VeteranKeepsHorse` = true, `NewPartyHorsesFromSeat` = true (`// a new lord's party gets its first horses and food from the shelves of its seat, paid by the house - or none`).
- **Linie:** "Konie" (z E6/0) z pozycjami: `polegle (misja/autobitwa, W)`, `lup zwyciezcow`, `porzucone do osad`, `pula weteranow +a/-b`, `dary startowe z polki: konie c, zywnosc e / brak d`; reszta.
- **Test (v2, K1-13):** reszta niewyjasniona koni - suma 7 dob <= max(5, 1% (zrodla pierwotne + ujscia) tych 7 dob), kazde okno d31-120 (przeniesienia poza mianownikiem; konie bohaterow w stanie); kon z wzorca 0; dary startowe z niczego 0 (konie i zywnosc); "przepadlo z rozbitymi" 0 (towary i konie).
- **Ryzyka:** (1) mniej koni (dzis nie gina wcale w autobitwie) -> wiecej "czeka na konia" - podaz koni bojowych jest waska (rachunek E6/0, regulator K4 w gore); (2) zwyciezca z pelnym taborem - towar ponad udzwig do osady; (3) A6 i R14 w tym samym
  oknie - jedna funkcja lupu. **E4:** E4/10 pula weteranow (nowe pole), E4/8 dary. **E5:** A6 (`BattlePlunder`, migawka po 3.2).

### E6/K2 - Jeden zapas koni; nadmiar koni AI na targ; gra nie handluje konmi lordow (212, PLAN 6.13) - S - PO A

- **Zakres (R11), v2:** (0) **jeden zapas koni** (K1-04): `Stables.Reserve(partia)` wedlug kategorii (zwykly / bojowy / szlachetny), liczony z taboru i zbrojowni razem (stan koni
  partii z K0): zapas_k = brak jezdzcow kategorii k + max(`AiMountSpareBuffer` jako minimum calosci, ceil(`AiSpareHorseShare` x jezdzcy kategorii k)); te sama funkcje czyta kupno
  (`Stables.OnSettlementEntered` - `want` z K:Stables.cs:738-756 zastapione zapasem; `AiMountSpareBuffer` przestaje byc dodawany osobno, K3) i sprzedaz (a). Histereza: kupno do zapasu,
  sprzedaz tylko ponad 1.5 x zapas kategorii - zakup i sprzedaz na jednym postoju nigdy sie nie spotkaja (lekcja pompy K:Stables.cs:711-717).
  (a) **sprzedaz koni ponad zapas sciezka nadwyzek lordow i zalog - `MenPurse.SellArmorySurplus`** (K:MenPurse.cs:214, :496; v1 wskazywal `GarrisonArmory.SellSurplus`, ktory od sklad7
  sprzedaje tylko tabor rozwiazanej partii - K:GarrisonArmory.cs:303-311): osobna galaz koni w `SellArmorySurplus` czytajaca `Stables.Reserve` (`AiSurplusPlan` zostaje bez koni -
  K:MenPurse.cs:480), najgorsze konie kategorii najpierw; kasa miasta placi przez `TownPurse.BuyFloor` (A1), odbiorca jak dla innych nadwyzek. (b) `PartiesBuyHorseCampaignBehavior.OnSettlementEntered`
  (G :60-135): prefiks ustawia kontekst; zakup koni gry (:62-87) pominiety (kupuje `Stables`), sprzedaz koni (:108-134) pominieta (sprzedaje tylko (a)); sprzedaz towaru (:96-99)
  bez zmian - prefiks na `SellItemsAction.Apply` w tym kontekscie przepuszcza wszystko poza wierzchowcami.
- **Klucze:** `AiSellsSpareHorses` = true, `AiSpareHorseShare` = 0.10 (`// a lord keeps a tenth more horses of each kind than his riders need - never fewer than Ai Mount Spare Buffer - and buys up to that; the horses of his fallen riders beyond half as many again he sells at the next market`).
- **Linia:** "Konie AI (175)" + `zapas wedlug kategorii: cel a/b/c, ma d/e/f; sprzedane z nadwyzki g za h; zakupy/sprzedaze gry pominiete i/j`. **Test (v2):** nadwyzka ponad 1.5 x zapas kategorii po postoju w miescie <= 2% konnych (d91-120); sprzedaze gry 0; zakup i sprzedaz tej samej kategorii w jednej partii w 7 dobach - 0 (kontrola pompy).
- **Ryzyko:** kasy miast placa za konie (BuyFloor - "brak kupca" liczony). **E5:** sciezka A1 (`MenPurse.SellArmorySurplus` w A1 - scalanie tekstowe).

### E6/K5 - Juczne z targu, zapasowe konie, udzwig (213, PLAN 6.16) - S - PO A

- **Zakres (R9, R13):** (a) nowa karawana i tabor: juczne gry (0.5 x ludzie, G CaravanPartyComponent.cs:71-82 wedlug RD6) zdjete w postfiksie `CreateCaravanParty` i kupione na targu
  miasta startu z kapitalu karawany (A3); brak - mniej jucznych (mniejszy udzwig); reszta 3.11 (BK GiveMounts/GivePackAnimals) ta sama regula. **v2 (KB K5):** postfiks Priority.VeryLow
  (jawnie po E4/8 `CaravanPost` Priority.Low - K:ArmsFromPeople.cs:971); `CaravanCapital.CreateFin` (K:CaravanCapital.cs:258) i O17 `CapFin` (K:CirculationWindows.cs:1771) to finalizery,
  wiec biegna PO K5 - K5 widzi kapital ustawiony w metodzie (`InitializePartyTrade`), a rozliczenie ceny BK z `CreateFin` przychodzi pozniej (zakup jucznych z kapitalu przed nim - zapisane).
  (b) **v2:** zapasowe konie lordow = zapas `Stables.Reserve` z K2 (osobnego `LordSpareHorsesPerRider` nie ma - to bylaby trzecia definicja zapasu); jedza jak kazdy kon (pasza z K0).
  (c) **udzwig (v2, K1-07):** gra juz daje 20 kg za kazdego wierzchowca z `NumberOfMounts` ("Spare Mounts", G DefaultInventoryCapacityModel.cs:78) - luzak, ktory nie niesie piechura
  (stan K0), niesie `SpareHorseCarryKg` 60 kg ZAMIAST 20: postfiks dopisuje roznice (60 - 20) tylko za takie luzaki; luzak pod piechurem zostaje przy 20 kg gry. Przed kodem sprawdzic
  lancuch modeli pojemnosci (BK `VanillaModelTweakPatches`, `BKCaravansBehavior`; nasz `MarketRoad.CartPostfix` K:MarketRoad.cs:226).
- **Klucze:** `PackAnimalsFromMarket` = true, `SpareHorseCarryKg` = 60 (nie zmieniamy `AiMountSpareBuffer` z `Armoury.json` - wchodzi do zapasu K2 jako minimum).
- **Linia:** `Juczne (213): nowe karawany kupily a, z niczego 0, brak b | udzwig: luzakow pod jukami c (+40 kg), pod piechurem d`. **Test:** juczne z niczego 0; liczba nowych karawan >= 90% licznika "na sucho" (ile karawan powstaloby z jucznymi gry) w tym samym biegu.
- **Ryzyka:** karawana bez jucznych wozi mniej - utarg kupcow; 4 wpiecia na `CreateCaravanParty` (kolejnosc w 5.1). **E4/E5:** E4/8 straz z ludzi, A3 kapital - ten sam cel.

---

### E6/R1 - Wytop przy kopalni + przetop (214, PLAN 6.1) - D - NK - PO A

- **Zakres (R1, R2; projekt K13 133, K13:213-234):** `iron` staje sie lupa "Iron Bloom": 15 kg, wartosc 15 x `HistCrudeIronPerKg` 1.4 = 21 d (K:Settings.cs:596), nazwa po angielsku;
  kopalnie: liczba sztuk bez zmian - **w `ProdPostfix` stala "100 kg rudy na lupe" zamiast `/ BulkScale(iron)`** (PULAPKA K13:227-229: `BulkScale` liczy sztuki - K:MaterialLaw.cs:178,
  K:WorkshopLaw.cs:126, K:WorkshopTrade.cs:256, K:StartStock.cs:101; przy 15 kg kopalnie dalyby 6.7 x zelaza); `WorkshopLaw.Needs` (K:WorkshopLaw.cs:160-176): metal w kg lupy,
  bez czlonu `oreKg x WorkshopWoodPerOre` (dymarka jest w okregu), zostaje wegiel kowalski `WorkshopForgeWoodPerMetalKg`; narzedzia 1 lupa -> wedlug masy (K:WorkshopTrade.cs:233-256);
  rzemieslnicy BK `iron>2tools`; przetop gracza 1 lupa -> 30 surowek + wegiel (MaterialLaw refine :105, `GetRefiningFormulas` :218); `ArmsScrap` i wraki `MendMaterial` zwracaja lupy
  (kg metalu / 15); `StartStock` w lupach; `TownFletchers`, `CaravanBulk`, `MaterialOrders`, `Drill`, `OutlawLaw`, `CastleSteel`, `TravellerTrade36b`, `RawNoRot`, `GoodsLedger`,
  `OreLedger` ("Zelazo (lupy)"), ceny `HistoricalPrices` (:350-357 waga; :471, :599, :602, :637 przeliczenia z `BulkScale`) - 18 plikow z "iron" (grep).
  **Wegiel dymarki (v2, K1-01):** v1 robil go z niczego i liczyl drugi raz - drewno na wegiel kowali juz produkuje las wsi (`WoodlotStep` 1.6 z 174.3 "for the charcoal of the smiths",
  K:Settings.cs:1273, K:VillageWoodlot.cs:54), a bez dymarek miejskich miasta stracilyby popyt na ok. 1 210 ladunkow/d (242 ladunki rudy x 5, L:31544-31545). Regula v2: lupa wymaga
  5 ladunkow PRAWDZIWEGO drewna: (1) las wsi okregu kopalni tej doby (`VillageWoodlot` wsi tego samego `TradeBound`) - zdjety z wozu na targ, zanim pojedzie; (2) reszta - woz wsi
  gorniczej kupuje drewno z polki miasta handlowego (`TradeBound`) jako towar powrotny, na nastepna dobe (zgodnie z R15 - zadnego przerzutu natychmiastowego), za zloto wsi (`BuyFloor`
  nie dotyczy - to kupno z polki po cenie targu); (3) brak drewna -> lup tylu, na ile starczy wegla, reszta rudy niewydobyta (licznik "kopalnia bez wegla", nic z niczego). Las okregu
  da okolo 4 ladunki na wies (2.5 x 1.6), dymarka ok. 58 na kopalnie [S] - wiekszosc drewna przyjdzie z targu tego samego miasta, ktore dzis pali je w dymarce, wiec popyt na drewno
  zostaje tam, gdzie jest (stos drewna bez skoku). `WoodlotStep` bez zmian. Ksiega drewna: "dymarki przy kopalniach: las okregu a, targ b". **Cena:** czlon dymarki
  (`WorkshopWoodPerOre` x 10 / `WorkshopCrudeKgPerOre`) przeniesiony z `FuelPerMetalKg` (K:HistoricalPrices.cs:293-297) do ceny lupy (15 kg x (surowka + wegiel dymarki na kg)) - suma
  `HistCost` wyrobu bez zmian, nic liczone dwa razy.
- **NK:** flaga w zapisie przy nowej kampanii (wzor `arm_castlepurse`); stary zapis - ruda jak dotad (stara ruda stalaby sie lupami: ok. +57 tys. d wartosci, K13). Sprawdzic
  przy kodzie kolejnosc `SyncData` -> `OnSessionLaunched` (`HistoricalPrices.Apply` K:ArmouryBehavior.cs:1175).
- **Cudzy kod:** BK `IsMineral`/`MineralData` i BEE `Contains("iron")` czytaja id, nie wage - zmiana znaczenia ich nie psuje (RB6 2); waga `iron` w DTE/BKROT - sprawdzic dekompilacja przed kodem.
- **Klucz:** `BloomeryAtMine` = true (`// new campaigns only: the mining village smelts its ore on the spot and sends out iron blooms (15 kg) instead of 100 kg loads of ore; the bloomery burns real wood - from the woods of the district first, then bought at its market town - and without wood the mine yields less; the town forge burns only smith's charcoal, and scrap and wrecks are smelted back into blooms`).
- **Linie:** "Ruda:" -> `Zelazo (lupy) (214): wsie a lup (model kopalni tej doby a', kontrola a - a' = 0; wegiel dymarek: las okregu b1, targ b2 ladunkow; kopalnia bez wegla c lup), zuzycie: zbrojne d, narzedzia e, strzelarze f; przetop: zlom g, wraki h, gracz i; miast bez zelaza n`.
- **Rachunek [S K13], v2:** drewno warsztatow zbrojnych w miastach 1472 -> ok. 260 ladunkow/d, a ok. 1 210 spala sie przy kopalniach (las okregu ok. 100, reszta z polek tych samych miast) -
  popyt na drewno swiata bez zmiany; lupa indeks 2.2-2.6; miast bez zelaza 30-34 -> 10-15; narzedzia ok. 50-55 d.
- **Test (v2, w tym samym biegu):** lupy = sztuki z modelu kopalni tej doby (tozsamosc, nie wobec E6/0); drewno spalone w dymarkach = 5 x lupy (tozsamosc); stos drewna miast bez wzrostu
  w d61-120 (bezwzglednie); "kopalnia bez wegla" <= 5% lup; miasta bez zelaza <= 15; cykle "brak zelaza" <= 584/d (bezwzglednie, liczba z31A).
- **Ryzyka:** (1) wyspy (Pyke, Ten Towers - 2 067 ladunkow bez wywozu, L:31434) - lupa nie zalatwi drogi morskiej (8.12); (2) W3 (BuyFloor): ladunek x2.6 drozszy w kontraktach i karawanach;
  (3) place (D6-1/D6-2) - wytop to praca wsi (utarg 65/35 po B1), `TownFolk.OnItemProduced` wsi nie liczy (K:TownFolk.cs:196). **E4:** czynnik rak L i spustoszenie mnoza lupy jak rude (W1).

### E6/R3 - Dziury po 174: narzedzia, uprzaz, przewoz wozem (215, PLAN 6.7) - M - PO A + R1

- **Zakres:** (a) linia narzedzi (K:WorkshopTrade.cs:233-256) kuje tylko przy zapasie miasta < 30 dni zuzycia narzedzi (stos rosnie +23/d, RA6 5); (b) uprzaz (polki 2 450, L:31539):
  AiGear dokupuje uprzaz jezdzcom tieru >= 4 (slot HorseHarness w koszyku, jak gracz) - po K3 wiecej jazdy; (c) **przewoz (R15):** `SupplyDemand.DailyTrade` (K:SupplyDemand.cs:581,
  `SupplyDemandTradePercent` 15%, zasieg 250 - Settings.cs:1041-1042) przestaje przenosic natychmiast - towar schodzi z polki zrodla przy zakupie i przychodzi "wozem bez partii"
  po czasie drogi (`MarketCarts.PerDay`, wzor `GarrisonCarts` K:GarrisonCarts.cs:3-16), w drodze - pozycja ksiegi; prawdziwa partia - odlozone (7). (d) ruda rozbitych partii - zrobione w K1.
- **Klucze:** `ToolsByUse` = true, `AiBuysBarding` = true, `ArmsTradeByCart` = true. **Linie:** "PodazPopyt" + `w drodze a szt. (srednio b dni)`; "Uprzaz: kupiona c".
- **Test (v2, w tym samym biegu):** przerzut natychmiastowy 0; ksiega "w drodze": kupione = dowiezione + w drodze (reszta 0), sredni czas drogi wypisany; polki uprzezy nie rosna d91-120.
- **Ryzyko:** M1 zwieksza role przewozu (puste koszyki) - opoznienie dostaw do malych krain; kolejnosc: R3 po M1 i R1. **E5:** A1 BuyFloor przy zakupie (kasa celu placi przy przyjezdzie? - zostaje jak dzis: przy zakupie).

---

### E6/W1 - Receptury wedlug wartosci (216, PLAN 6.3; krok x1.3 - v2 w W3) - S - PO B1

- **Zakres (R6; K13 131-132, K13:173-212):** NOWY `WorkshopRecipes.cs` - raz na sesje po `HistoricalPrices.Apply` (K:ArmouryBehavior.cs:1175): sztuk wsadu = 0.6 x wartosc wyrobow /
  wartosc wsadu w granicach fizyki (filc 3 welny -> 2, plotno 6 lnu -> 2, skora 5 skor -> 2). **v2 (K1-03, KB W1-ksiega):** krok x1.3 NIE jest w W1 - robi go regulator W3 (jedna
  regula w gore i w dol, bez zapadki 0.8 = 1/1.25); v1 czytal ksiege towarow 146, ktora jest tylko logiem (`GoodsLedgerEnabled && LogEnabled`, bez zapisu - K:GoodsLedger.cs:22, :176-179).
- **Klucze:** `WorkshopRecipesByValue` = true. **Linia:** `Receptury (216): [warsztat: wsad -> wyrob, sztuk wsadu dawniej a, teraz b]`.
- **Test:** len i welna: co najmniej jeden z miesiecy 2-4 bez wzrostu zapasu (prog etapu); odziez wojska - kolejka nie rosnie; skora i plotno nie ubywa > 10/d.
- **Ryzyka:** wiecej lnu na plotno (x3) - plotno waskim gardlem napraw/odziezy do czasu kroku x1.3 (W3, pierwsza decyzja d56); place (W2, D6-1). **E5:** utarg wsi 65/35, zatkane wsie zamkowe (prog B4).

### E6/N1 - Naprawy jedna regula, AI naprawia tylko noszone (217, PLAN 6.17) - S - PO B1

- **Zakres (R16):** `Forge.SelfRepair` (K:Forge.cs:498-520; :513, :517 - `SelfRepairMaterialFactor` 0.25) bierze udzial z `MendMaterial.ShareOf` (`MendMaterialMaxShare` x (1 - stan),
  K:MendMaterial.cs:75-81) i ten sam podzial na metal/drewno/skore/plotno z receptury - `SelfRepairMaterialFactor` (jest w `Armoury.json` Jeffa) przestaje byc czytany (opis MCM:
  "unused since stage 6 - see Mend Material Max Share"); AI: `AiWear.MendWithMaterial` (K:AiWear.cs:427) bierze tylko sztuki przypisane zolnierzom (ksiega `AddWorn` :77), lup obity
  ponad potrzebe - sprzedaz nadwyzek lordow i zalog (**v2: `MenPurse.SellArmorySurplus` / `AiSurplusPlan`** K:MenPurse.cs:477-496, nie `GarrisonArmory` - ten od sklad7 sprzedaje
  tylko tabor rozwiazanej partii, K:GarrisonArmory.cs:303-311) jako obity/wrak -> zlom (R2); zaplata kowalom (K:AiWear.cs:410, :450; K:TroopSelfMend.cs:154, :217) do kiesy ludu przez to
  samo `TownFolk.Earn` co B1 (jedno miejsce).
- **Klucze:** `SelfRepairSameRule` = true, `AiMendsWornOnly` = true. **Linia:** "Zuzycie AI" + `pominiete (lup nienoszony) a`.
- **Test:** czeka na material <= 1000 (skora <= 500); odziez wojska - skora nie rosnie; naprawy noszonych bez spadku. **Ryzyko:** A3 `CampReserve`/`OutstandingCost` (K:AiWear.cs:355) - ta sama funkcja kosztu (RD6 W6).

### E6/R2 - WARUNKOWO (v2): wiecej rak i kopaln (218, PLAN 6.2) - D - PO TESCIE ETAPU 6 (krok z liczb R1)

- **Zakres (R17, D6-2; P174 S4 :629):** `WorkshopHandsPer1000People` 0.10 -> 0.15 (klucza nie ma w `Armoury.json` - zmiana domyslnej dziala), `MineOutputStep` 1.5 -> 3.0 (kopalnie x2,
  K:Settings.cs:1272); nowe wsie gornicze tylko tam, gdzie lore (przeglad typow wsi jak `VillageClimate`, NK). **Warunek:** test zbioru A etapu 5 - "brak zlota/kupca" w linii
  "Warsztaty" = 0 (inaczej waskim gardlem jest kasa miast, nie surowiec - RD6 W3) i po R1 cykle "brak zelaza" > 300/d. **v2 (KB R2-warunek):** warunku "po R1" nie da sie sprawdzic, gdy R1
  i R2 jada w jednym tescie (obie paczki zmieniaja podaz zelaza naraz) - R2 jest WARUNKOWA: kodowana i testowana osobno po tescie etapu 6, decyzja o x2 kopaln na liczbach R1 z tego testu.
- **Klucz:** `MineStepStage6` = true (zmiana stopnia przez nowy klucz - wylacznik). **Test:** korpus t3+ w partiach > 25%; wyroby >= 1700/d; ludnosc i wolne rece w granicach etapu 4.
- **Ryzyka:** wiecej wyrobow z tej samej nadwyzki kas (W3); rece rzemiosla = udzial ludzi miasta (E4/4, E4/8 zmieniaja liczbe). **E4:** czynnik rak L (W1) nie liczony drugi raz.

### E6/K3 - Jazda wedlug szablonu partii (175), rycerz z koniem rodu (219, PLAN 6.14) - M - PO B1, TYLKO Z CZYNNYM K2

- **Zakres (R12):**
  1. **Udzial jazdy partii = udzial konnych w jej szablonie (v2, K1-05, K1-06, KB K3-klucze):** cel = udzial ludzi jednostek konnych (`IsMounted`, J + KL) w stosach szablonu, z ktorego
     partia sie rodzi - `Clan.DefaultPartyTemplate` rodu przywodcy (przez obiekt; rod bez szablonu - `Culture.DefaultPartyTemplate`); liczony raz na sesje PO zmianach 175 (CS). To ta sama
     liczba, ktora ustawia 175 (Polnoc 24%, Smocza Skala 10%, Wolni Ludzie 6% / Rayder 11%, Dothrakowie 100%, Zelazne Wyspy 0 / Harlaw 21% - PROJEKT-175 :285, :346-348, :361, :386-391),
     wiec nie ma drugiej reguly na udzial jazdy i nie ma klucza po StringId krolestwa (v1: "kultura klanu" nie pasowala do celow "Daenerys"/"Aegon" - klan Targaryen ma kulture valyrian,
     klany Daenerys glownie ghiscari, ROT spclans.xml). W `Stables.FilterTargets` (K:Stables.cs:591) - partia ponizej celu i cel konny w liscie -> cel pieszy odpada (awans konny; brak konia
     -> "czeka" i zakup); partia na celu albo powyzej -> cel konny odpada, gdy jest pieszy (zadnych koni ponad kwote). Gracz bez zmian (sam wybiera).
  2. **Zakup wedlug kategorii** (`NeedByCategory`, K:Stables.cs:740) do zapasu `Stables.Reserve` z K2: kupowana kategoria, ktorej brakuje (zwykly t2-3, bojowy t4-5, szlachetny t6), nie najtansza.
  3. **Rycerz z koniem rodu:** cel z linii szlacheckiej/rodowej (`EliteBasicTroop` i linie rodow ROT - zbior jak CS Army175/HouseLevies): przy postoju w osadzie - najpierw stado wsi
     lenna rodu, **ktora jest `BoundVillages` osady postoju** (v2, KB K3-przerzut - jak dzis K:Stables.cs:826-848; dalekie lenna bez przerzutu natychmiastowego, R15; zamowienie wozem -
     rozdz. 7), magazyn wsi, zloto do wsi bez narzutu; potem targ tej osady do `KnightHorseMarketShare` polki (zamiast 25%), zaplata z budzetu sprzetu rodu (ClanBudget 166).
     **Przerwa `AiMountBuyCooldownDays` (v2, KB K3-pompa):** zdjeta dla czekajacych awansow TYLKO gdy czynne jest pominiecie sprzedazy koni przez gre z K2(b) (`AiSellsSpareHorses`);
     inaczej przerwa zostaje (pompa: gra sprzedaje najcenniejsze konie przy postoju - G PartiesBuyHorseCampaignBehavior.cs:108-134) i linia "K3 bez K2 - przerwa zostaje".
     Kon -> tabor -> `PayInHorses` (K:Stables.cs:648) -> zbrojownia jezdzca.
  4. Wierzchowiec wolno, gdy `MountLaw.Allowed(jednostka, zwierze)` (wielblad Qarthu tak, wielblad u Polnocy nie).
- **Cele (v2):** tabela v1 (khuzait 80%, vale/reach 30%, ..., freefolk 1%) USUNIETA - cel kazdej partii z jej szablonu (pkt 1). Srednia celow wazona ludzmi partii [S] ok. 20%
  (typowy szablon ROT ma J 21-24%: 38/41/21, 34/45/21, 31/48/21, 45/31/24 - PROJEKT-175) - policzy `tools/szablony_jazdy.py` (E6/0) przed kodem K3; Wyspy Letnie bez zmian
  [D 09.10] wynikaja z ich szablonu. Pytanie 3 do Jeffa (kierunek).
- **Klucze (nowe - stare `AiMount*` sa w `Armoury.json`):** `CavalryShareByTemplate` = true (`// an AI lord's party grows its horse to the share of riders in the army its realm fields by the books - all riders among the Dothraki, almost none on the Iron Islands, about one in five in most realms - and upgrades men on foot once it has enough`), `KnightHorseFromHouse` = true (`// a knight's horse is bought by his house - from the herds of its villages by the place where he stops first, then at the market - and goes into his own armoury`), `KnightHorseMarketShare` = 0.5.
- **Linia:** `Rycerze i jazda (219): jazda partii a% (cel szablonow b%), wedlug krain [..]; konie kupione wedlug kategorii: ze stad wsi postoju c, z targu d, za e zl (budzet sprzetu); czeka f (w tym kategoria g); przerwa: zdjeta h / zostaje (K3 bez K2) i`.
- **Rachunek [S], v2 (K1-02):** +12 tys. koni pod jezdzcami partii przy 20% (8.3% -> 20% ze 107 tys.) - ale wiekszosc tej jazdy to linie t4-6, ktore chca koni BOJOWYCH i szlachetnych,
  a stadniny daja ich ok. 20-60 i 2.5-7 na dobe [S, zmierzy E6/0] wobec zgonow K1 ok. 40-150 koni/d; na polkach leza glownie zwykle (11.6 tys.; w zbrojowniach wolne 1897/113/8).
  W 120 dobach realnie: jazda t2-3 z koni zwyklych z polek + tyle t4-6, ile da podaz bojowych (regulator K4: krok x1.3 i przesuniecie do 25% zwyklych na bojowe w stadninach) - [S]
  ok. 12-15% jazdy w d91-120, pelne ok. 20% w biegu rocznym. Koszt koni 5-15 mln zl w 120 dob z budzetow sprzetu (kon 400-1100 zl, bojowy 1100-2400); zold konnych x1.5
  (`MountedWageFactor`) - dzis +45.8 tys./d przy 9.2 tys. konnych (L:31440) -> ok. +60 tys./d przy 20% (+10% zoldu partii); pasza zimowa: kon = 2.5 czlowieka zima (E4/1) -> do +28%
  jedzenia partii zima (gorna granica, przy 20%).
- **Test (v2 - prog z rachunku koni E6/0, ustalany przed kodem K3; ponizej wartosci [S]):** jazda partii AI >= 12% w d91-120 i nie spada w d61-120; Dothrakowie >= 60%; Zelazne Wyspy <= 5%;
  Wolni Ludzie <= cel szablonu + 3 pp; czeka na konia swojej kategorii nie rosnie w d91-120; pompa koni 0 (K2); wojsko lordow w wojnie 95-115 tys.; bankruci 0. Bieg roczny: jazda 18-22%.
- **Ryzyka:** (1) zold i konie z budzetow rodow (progi etapu 2) - przy spadku wojska poprawiamy przeplywy (Jeff 09.10 C); (2) glod lordow zima (bieg B) - regresja E4; (3) wiecej zbytu
  koni = wiecej utargu wsi zamkowych (54% wartosci, RD6 W4) - dobrze dla progu B3 etapu 5; (4) `khuzait_footman` t2 stoi na zawsze (PROJEKT-175 2.1) - Dothrakowie nie dojda do 100%,
  prog 60%; (5) bisekcja wylacznikami: K3 bez K2 nie zdejmuje przerwy (pkt 3) - bez pompy.
  **E5:** zakupy Stajni (K:Stables.cs:842, :861) wpiete w kiese (B1 - place hodowcow przez utarg wsi).

### E6/K4 - Hodowla wedlug popytu (regulator W3 na kategorie koni) + przeglad 71 stadnin (220, PLAN 6.12) - S - NK - W TESCIE: KROK I PRZESUNIECIE CZYNNE, CIECIE NA SUCHO

- **Zakres (v2, K1-02):** regulator W3 (`HarvestValve`, ta sama funkcja i stan w zapisie) obejmuje konie **wedlug kategorii** (zwykly / bojowy / szlachetny / juczny) i dziala w OBIE
  strony (v1 umial tylko ciac, a L728 pokazuje spadek polek z ok. 19.5 tys. do 5.3 tys. w drugim roku juz bez K1 i K3): zuzycie kategorii (zakupy wojska i karawan, zgony K1, mieszczanie
  wedlug pytania 2) wobec produkcji stadnin z okna 56 dob i zapas w dniach zuzycia; (a) **w dol** - jak W3 (kategoria bez zbytu, stos > 60 dni); (b) **w gore** - krok x1.3 dla kategorii
  chcianej (zuzycie >= 98% produkcji, stos < 30 dni); (c) **przesuniecie** - gdy bojowe sa chciane, a zwykle w nadmiarze (stos zwyklych > 60 dni), stadnina przenosi do 25% swojej produkcji
  zwyklych na konie bojowe swojej listy (`t2_*`) - te same sztuki, inna kategoria (nic z niczego; hodowla z tych samych klaczy). Mnoznik w `ProdPostfix` dla `Horse`; BK `RunStuds`
  (BK BKBuildingsBehavior.cs:313-340) liczony jako hodowla zamkow (zrodlo z przyczyna) i pomijany przy przycietej kategorii bojowej (prefiks). Przeglad 71 stadnin wobec lore (RB6 6.12:
  vale 7, vlandia 7, battania 7, ghiscari 5...; Dorne - rumaki i wielblady z M2) - NK.
- **Tryb (v2):** `HorseBreedingByDemand` = true - czynne (b) i (c) (podaz koni bojowych dla K3 i zgonow K1 jest potrzebna od razu, a krok w gore przy niskim stosie nie grozi
  puchnieciem); (a) ciecie tylko "na sucho" (liczy i loguje "ile by przycial") - czynne pod osobnym kluczem `HorseBreedingCutLive` dodanym po biegu rocznym (lekcja z E5: zmiana domyslnej nie
  dziala przez `Armoury.json`). Powod: popyt na konie odwraca sie po ok. d360 (L728: polki 19.6 tys. d361 -> 5.3 tys. d728) - 120 dob nie wystarczy do kalibracji ciecia (RD6 7).
- **Linia:** `Hodowla (220): kategoria: produkcja/zuzycie, stos w dniach, mnoznik m (czynny) | przesuniete zwykle -> bojowe a/d | ciecie na sucho: mnoznik by byl c`. **Test (v2):** w etapie 6 -
  podaz koni bojowych w d61-120 > w d1-60 (gdy bojowe chciane); stos zwyklych nie spada ponizej 30 dni zuzycia przez przesuniecie; bieg roczny: polki koni bez trendu +-10%.

### E6/W3 - Jeden regulator wsi: mniej towaru bez zbytu, x1.3 towaru chcianego (221, PLAN 6.4 + krok 6.3) - S - PO W1 (i R1)

- **Zakres (R5, R6; K13 132, 134, K13:235-248), v2 (K1-03, KB W1-ksiega):** NOWY `HarvestValve.cs` - JEDEN regulator wsi (krok W1 i zawor W3 razem, jedna zasada: wies robi tyle,
  ile swiat zuzywa); stan w `SyncData` (`arm_harvestvalve`: okna produkcji i zuzycia na towar, mnozniki, doba ostatniej decyzji), reset w konstruktorze ArmouryBehavior; okna liczone
  WLASNYMI licznikami niezaleznie od `LogEnabled` (ksiega 146 tylko je wypisuje). Towary wsi poza zywnoscia i zwierzetami (konie - K4, ta sama funkcja).
  - okno 56 dob, decyzja co 28 dob, pierwsza w d56 (wczesniej mnozniki 1 i kasowanie 174.3 jak dzis - stos nie rosnie bez kasowania, zanim regulator ma dane);
  - **w dol:** zuzycie < 90% produkcji i stos swiata (polki + magazyny wsi + tabory) > 60 dni zuzycia -> mnoznik = 0.95 x zuzycie/produkcja (cel PONIZEJ zuzycia - stos maleje), najwyzej
    x0.5 na jedna decyzje, podloga 0.1; towar bez zadnego odbiorcy -> podloga 0.1 do czasu, gdy stos < 60 dni produkcji;
  - **w gore:** zuzycie >= 98% produkcji i stos < 30 dni zuzycia -> x1.3 (najpierw do 1 po cieciu, potem najwyzej `VillageOutputStep` 1.3 ponad baze - krok K13 132, **tylko produkcja,
    bez mnoznika popytu** D6-4);
  - pomiedzy - bez zmian (martwa strefa; v1: cel 1.25 x zuzycia trafial w prog kroku W1 0.8 = 1/1.25 - zapadka 1.25 -> 1.625; teraz w gore tylko przy stosie < 30 dni, w dol tylko > 60 dni,
    wiec stos zostaje miedzy 30 a 60 dniami zuzycia, a regulator nie kolysze sie co decyzje);
  - mnoznik w `ProdPostfix` (tez las wsi `VillageWoodlotLoads` i drwale; po R1 v2 popyt na drewno bez zmian - brak nadwyzki 1 200/d z v1); `RawNoRot.Rate` (K:RawNoRot.cs:36-41) = 0
    od pierwszej decyzji regulatora - nic w nicosc (wyroby gracza i sztandary BK kasuje dalej, jak gra - K:RawNoRot.cs:60; poza etapem 6).
- **Klucze:** `HarvestValve` = true (`// every four weeks a village makes less of a good the world does not use up and more of a good it does - instead of the stock rotting away on the stalls; the stock stays between one and two months of use`), `VillageOutputStep` = 1.3, `ValveStockDaysLow` = 30, `ValveStockDaysHigh` = 60.
- **Linia:** `Regulator wsi (221, doba N): [towar: mnoznik, zuzycie/produkcja, stos w dniach, decyzja w gore/w dol/bez zmian]; skasowano 0 (od d56)`. **Test (v2):** kasowanie towaru 0 od d56;
  sol i glina - stosy bez wzrostu w d91-120 (osiagalne: pierwsze ciecie w d56 do 0.95 x zuzycia); len i welna - prog etapu (z W1); zaden towar nie zmienia kierunku w dwoch kolejnych decyzjach
  (brak kolysania); miasta kupuja >= 98% plonu.
- **Ryzyka:** wsie solne/gliniane biednieja (utarg); stos swiata liczony razem - lokalne nadwyzki (wyspy) nie sa osobno (8.12). **E4/E5:** regulator nie zmienia `FreeHands` (RD6 W1); zatkane wsie zamkowe.

### E6/W4 - WARUNKOWO: chleb, piwo, wino, deski; srebro; rzemioslo wsi BK (222, PLAN 6.5) - D

Warunki z ksiegi towarow (K13 136-138): zboze w nadwyzce i warownie glodne 0 (136); produkcja srebra (137); rzemioslo wsi BK > 5% skory lub narzedzi (138). Po tescie etapu 6.
Styki: D6-1 (wyroby posrednie), koszyk kiesy (chleb, piwo), zboze spichlerzy E4/6.

### E6/R4 - WARUNKOWO: tarczownicy, placa w cenie, strzaly, helmy, dobytek osady (223, PLAN 6.8) - S

Po tescie etapu 6 (P174 rozdz. 9 :1032-1038). Uwaga: tarcza wedlug szczebla w partiach dzis 31% (L:31426) - ponizej progu P174 "35%"; decyzja po M1 (zmienia pule tarcz).
"Placa miasta w cenie" = place B1 (R11 etapu 5) - jedna regula.

---

## 4. Test i progi etapu ("Skonczone, gdy" z PLAN:197 + progi projektu)

**Standard** (P5 rozdz. 3): autotest 40 dob + zapis 9 dob (0 bledow naszych modow); bieg A - 120 dob NOWEJ kampanii (NK: R1, M2, K4); bieg B - 120 dob z proba zimy od 8. doby
(regresja E4 z wieksza liczba koni i nowa pasza K0). **Kiedy:** po wgraniu zbioru B etapu 5 (kiesa ludu czynna - inaczej W2/W3 i D6-1/D6-4 mierzylyby stan, ktorego nie bedzie), jeden zbiorczy test dla
wszystkich paczek bez warunkowych (v2: R2 warunkowa) [D 11.10 03:05, 17:20]; E6/0 wczesniej - w tescie zbioru A etapu 5. Niezdany prog - bisekcja wylacznikami, nie nowy kod (v2: K3 bez K2
nie zdejmuje przerwy zakupow - bisekcja nie wlacza pompy). K4: krok i przesuniecie czynne, ciecie - po biegu rocznym.
**v2 (K1-08): zadnego progu "wobec E6/0 z innego biegu"** - test etapu 6 jedzie po zbiorze B (place B1, inna kampania, inne wojny), wiec progi paczek sa liczone w TYM SAMYM biegu
(liczniki "na sucho" i tozsamosci - E6/0 "Liczniki na sucho") albo bezwzglednie.

| Prog | Jak mierzony | Paczka | Dzis |
|---|---|---|---|
| **cos na tulowiu >= 75% zolnierzy AI** (PLAN) | "Pokrycie zbrojowni AI (171)" partie + zalogi, d120 | wszystkie | 93.6% |
| (projekt) korpus wedlug szczebla w partiach >= 63%, t3+ >= 25% | j.w., bezwzglednie | M1, R1 | 63% / 25% |
| **skory i plotna nie ubywa > 10/d** (PLAN) | "Towary": srednia zmiany d91-120 | W1, N1 | -4.5 / -1.4 |
| **len i welna nie rosnie 3 miesiace z rzedu** (PLAN) | "Towary": zmiana w miesiacach 2, 3, 4 - co najmniej jeden <= 0 dla kazdego | W1, W3 | NIE (+35/+26/+24; +42/+27/+3.8) |
| **w linii "Konie" kazdy znikajacy kon ma przyczyne** (PLAN) | v2: reszta niewyjasniona - suma kazdych 7 dob w d31-120 <= max(5, 1% (zrodla pierwotne + ujscia) tych 7 dob); przeniesienia poza mianownikiem; stan z konmi bohaterow; LegendaryLaw kasuje 0 | E6/0, K1, M2 | brak linii; 9.5/d w nicosc |
| (projekt 6.19) obce = 0, bandyckie = 0, unikaty z drogi gry = 0 | "Warsztaty wedlug krainy" | M1 | Polnoc 61 obcych, NS 249 |
| (projekt 6.19, v2) cykle utracone przez M1 <= 5% cykli uzbrojenia | licznik na sucho w tym samym biegu | M1 | - |
| (projekt 6.14, v2) jazda partii AI >= 12% w d91-120 i nie spada w d61-120 [S - prog ostateczny z rachunku koni E6/0 przed kodem K3]; Dothrakowie >= 60%; Zelazne Wyspy <= 5%; Wolni Ludzie <= cel szablonu + 3 pp; bieg roczny 18-22% | "Zold konnych (160)", "Musztra AI wedlug krolestw", "Rycerze i jazda (219)" | K3, K4 | 8.3%; 45%; 1%; 1% |
| (projekt, v2) czeka na konia swojej kategorii nie rosnie w d91-120; nadwyzka ponad 1.5 x zapas kategorii <= 2% konnych; pompa koni 0 | "Konie AI (175)" | K2, K3 | 1510; - |
| (projekt 6.12, v2) podaz koni bojowych d61-120 > d1-60 (gdy chciane) | "Hodowla (220)" | K4 | - |
| (projekt 6.1, v2) lupy = model kopalni tej doby; drewno dymarek = 5 x lupy; stos drewna miast bez wzrostu d61-120; kopalnia bez wegla <= 5% lup; miasta bez zelaza <= 15 | "Zelazo (lupy)", "Drewno" | R1 | 30-34 |
| (projekt 6.4, v2) kasowanie towaru 0 od d56; sol i glina bez wzrostu d91-120; brak kolysania regulatora | "Regulator wsi (221)", "BK gnicie surowcow (174.3)" | W3 | 135/d |
| (projekt 6.17) czeka na material <= 1000 (skora <= 500) | "Zuzycie AI" | N1 | 2070 (1700) |
| (projekt 6.7, v2) przerzut natychmiastowy 0; ksiega "w drodze": kupione = dowiezione + w drodze (reszta 0) | "PodazPopyt" | R3 | 1301 szt./d przerzutem |
| (projekt 6.16, 6.11) konie i zywnosc z niczego 0 (wzorzec, dary, juczne) | "Konie", "Juczne (213)" | K1, K5 | 4 zrodla + zywnosc daru |
| (projekt 6.15, v2) sufit tempa z K0 >= bez K0 (na sucho); `NumberOfMounts` = pelne przeliczenie gry; koszt K0 <= 2 ms/dobe | linia marszu | K0 | - |
| (projekt 6.18, v2) zboze zdjete = ryby dodane kazdej doby (kontrola 0) | "Wioski wedlug rodzaju" | W2 | - |
| (projekt 6.9, v2) tloczni bez winogron 0; wielbladow wyprodukowanych poza Qarthem 0; warsztaty gracza nietkniete | "Warsztaty wedlug surowca", "Wierzchowce kanonu" | M2 | - |
| **regresja E2/E5:** wojsko lordow w wojnie 95-115 tys. (podloga 85), bankruci 0 | "Ludzie:" | K3, K5, K1 | 110.4 tys. |
| **regresja E4** (bieg B): lordowie w glodzie i bez jedzenia <= test koncowy E4; zadna warownia AI nie glodzi przed 30. doba zimy; mnoznik wsi z 1/3 uchodzcow <= 0.67 (z czynnika); v2: pasza z K0 (konie partii, nie konni po glowie) - prog przeliczony w biegu B | bieg zimowy | K0, K3, K5, W1-W3 | z31B: 20 / 16 |
| **regresja E5:** miasta kupuja >= 98% plonu; zatkane wsie zamkowe <= 5.6% (lato); udzial panow zamkow w D >= 30%; zloto swiata < 0.1%/28 dob; stac na jedzenie >= 95%, koszyk >= 98%; "brak zlota/kupca" warsztatow <= test B | linie etapu 5 | R1, W1, W3, K3 | z testu B |

---

## 5. Ryzyka / kontrola calosci (zasada 0)

### 5.1 Wspolne cele latek i stan (plik:linia)

| Cel / stan | Kto dzis | Kto w etapie 6 | Co pilnowac |
|---|---|---|---|
| `WorkshopsCampaignBehavior.TickOneProductionCycleForNotableWorkshop` | `WorkshopLaw.CyclePrefix` (K:WorkshopLaw.cs:206) - linie uzbrojenia nasza droga | M1 (`Candidates`), R1 (`Needs`) | bez nowej latki |
| `GetRandomItemAux(ItemCategory, Town)` | `RandomItemPostfix` (K:WorkshopLaw.cs:1479) - wegiel/sztabki | M1 (lancuch, unikaty) | jedyny postfiks (BK nie lata) |
| `GetItemsToProduce` | nikt | M1 (kontekst miasta - nowe) | prefiks + finalizer, tylko `[ThreadStatic]`; v2: bez latki na `GetRandomItem` (jedyny wolajacy G :829; BK WorkshopData martwy) |
| `CanPlayerWorkshopProduceThisCycle` / `TickOneProductionCycleForPlayerWorkshop` | `PlayerGatePostfix`, `PlayerCyclePrefix` (K:WorkshopLaw.cs:1516) | M1 (dochod 0 -> false) | wsad nie schodzi bez wyrobu |
| `CalculateDailyProductionAmount` (kazdy model wsi) | `MaterialLaw.ProdPostfix` (+ E4/5, E4/7); `WinterBite.VillageProdPostfix` (Default, przed nami) | E6/0 (licznik), R1, W3 (z krokiem W1), K4, W2 | v2: czynniki przemienne R1/W3/K4; W2 (zboze x(1-u) i dopisek ryb) OSTATNI - nie czynnik; scalac E6/0 -> R1 -> W3 -> K4 -> W2; progi E4 z czynnika, nie z wyniku |
| BK `PopulationManager.GetProductions` | `MineralOnce` postfiks | W2 (ryby), M2 (wielblady -> Qarth, przeniesienie wagi) | jeden postfiks |
| `ItemRoster.OnRosterUpdated` (v2) | nikt | K0 (konie z modyfikatorem jak `CalculateCachedStats`) | goraca sciezka - wyjscie od razu bez `HorseComponent`/modyfikatora; licznik kosztu |
| DTE `EveryoneCampaignBehavior.OnMapEventEnded` (private, v2) | nikt (nasze sluchacze `MapEventEnded` - kolejnosc wobec DTE nieustalona) | E6/0 (migawki), K1 (zgon konia w prefiksie) | jedno okno `DteWindow`; brak metody - linia "BRAK", bez wywrotki |
| `WorkshopsCampaignBehavior.DecideBestWorkshopType` (przez `FindTotalInputDensityScore`, v2) | nikt | M2 (x0.01, nie 0) | przy samych zerach gra losuje typ na slepo (G :1335) |
| `DefaultInventoryCapacityModel.CalculateInventoryCapacity` (v2) | `MarketRoad.CartPostfix` (K:MarketRoad.cs:226); BK `VanillaModelTweakPatches` - sprawdzic | K5 (+40 kg za luzak pod jukami) | "Spare Mounts" 20 kg gry zostaje - dopisujemy tylko roznice |
| `WinterRealm.AnimalNeed` (v2) | E4/1 zima, wariant BK (a) | K0 (pasza z jednego stanu koni) | prog regresji E4 przeliczyc (bieg B) |
| `DefaultSmithingModel.GetRefiningFormulas` | `MaterialLaw` | R1 (lupa) | NK |
| BK `BKSettlementBehavior.DeleteOverProduction` | `RawNoRot.Prefix` (K:RawNoRot.cs:45, return false) | W3 (stawka 0) | wylaczony zawor = stawki 174.3 |
| BK `BKBuildingsBehavior.RunStuds` | nikt | K4 (licznik + pomijanie) | tylko prefiks |
| getter `CharacterObject.UpgradeRequiresItemFromCategory` | DTE (null) -> `RanksNeedHorses` Priority.Last | K3 (bez zmian) | - |
| `PartyUpgraderCampaignBehavior.GetPossibleUpgradeTargets` / `UpgradeTroop` | `Stables.FilterTargets` / `PayInHorses`, `HorseCensus` | K3 (kwota, kategoria) | jedna klasa |
| `PartiesBuyHorseCampaignBehavior.OnSettlementEntered` | nikt (gra kupuje i sprzedaje konie lordom) | E6/0 (licznik), K2 (pominiecie koni) | sprzedaz towaru zostaje (`Ransom178` lata `PartiesSellLoot` - inny cel) |
| `Stables.OnSettlementEntered` (nasze zdarzenie) | zakupy lordow, `Remount` | K2 (`Stables.Reserve`), K3 | v2: jeden zakup na postoj do jednego zapasu wedlug kategorii (K5 nie ma osobnego zapasu); histereza 1.5 wobec sprzedazy K2 |
| `MenPurse.SellArmorySurplus` / `AiSurplusPlan` (v2) | nadwyzki lordow i zalog (K:MenPurse.cs:214, :477-496; konie wylaczone :480) | K2 (galaz koni z `Stables.Reserve`), N1 (lup obity) | jedna sciezka nadwyzek; `GarrisonArmory.SellSurplus` zostaje tylko dla rozwiazanych partii i D4 |
| `MapEvent` polegli / zbrojownie DTE po bitwie | E4/7 polegli, `CasualtyLoot`, DTE (EveryoneCampaignBehavior.cs:529-562) | K1 (zgony koni przed DTE), E6/0 (migawki) - przez `DteWindow` | kolejnosc: polegli (`MapEvent`) -> zgon konia (prefiks DTE) -> rozdanie DTE |
| `MobilePartyDestroyed` | `GoodsLedger.OnPartyDestroyed` (K:GoodsLedger.cs:790), DTE (:510) | K1 (lup zwyciezcy) | jedna funkcja z A6 |
| `CaravanPartyComponent.CreateCaravanParty` | `CaravanCapital` (prefiks + finalizer), okno O17 (finalizer), E4/8 postfiks Low, A3 kapital | K5 (juczne, postfiks VeryLow) | v2: postfiksy E4/8 (Low) -> K5 (VeryLow), potem finalizery O17 i `CaravanCapital.CreateFin` (Harmony: finalizery po wszystkich postfiksach) |
| `HeroSpawnCampaignBehavior.GiveInitialItemsToParty` | E4/8 licznik (K:ArmsFromPeople.cs:970) | K1 (konie i zywnosc z polki siedziby) | licznik E4/8 zostaje |
| `RecruitKit` kon z wzorca (:504), pula weteranow E4/10 | 160, E4/10 | K1 (pula koni) | nowe pole w zapisie |
| `CalculateFinalSpeed` | `MarchPace.SpeedPostfix` Priority.Last | K0 (tylko odczyt stanu koni z pamieci dobowej) | v2: zadnego `Stables.Balance` w tej sciezce (wolana przy kazdej zmianie sciany siatki); etap 7 (tempo) |
| `FindTotalInputDensityScore` / `BuildWorkshopsAtGameStart` | nikt; CS NorthernFare po starcie | M2 | NK; flaga `arm_e6_inputs` |
| CS `Mends.NorthernFare` | CS | M2 (TradeBound, v2: bez warsztatow gracza) | osobny DLL |
| `SupplyDemand.DailyTrade` | przerzut (K:SupplyDemand.cs:581) | R3 (woz) | ksiega "w drodze" |
| `Forge.SelfRepair`, `AiWear.MendWithMaterial`, `OutstandingCost` | T3/135, A3 `CampReserve` | N1 | jedna funkcja kosztu |
| `OnItemProduced` (miasta, zamki, wsie) | `OreLedger`, `TownFolk` (miasta/zamki) | E6/0 (konie, uzbrojenie), R1 (lupy we wsi) | tylko odczyt |
| nawias konsumpcji 110 (`CastlePurse.ShelfPostfix`/`ConsumePostfix`) | 110, A1, B1 | E6/0 (konie mieszczan), pytanie 2 | migawka wspolna |

### 5.2 Spojnosc logiki

1. **Zamknieta ekonomia - kazda sztuka:** zelazo (wydobycie -> lupa -> wyrob -> zlom -> lupa), drewno (las -> targ; **v2:** wegiel dymarek z lasu okregu i z polki miasta handlowego -
   prawdziwe drewno, nie zrodlo pierwotne; brak drewna = mniej lup), uzbrojenie (ksiega E6/0, przeniesienia netto 0), konie (ksiega E6/0 wedlug kategorii, K1: zgon tylko zabity, lup,
   sprzedaz; zrodla z niczego zamkniete K1/K5; **v2:** podaz z regulatora w obie strony K4), zywnosc daru startowego (v2, K1), towar bez zbytu (mniej produkcji, zero kasowania W3 od d56).
   Jedyne ujscia bez platnika: zgon konia (przyczyna "polegl"), zuzycie sprzetu (wrak -> zlom), wyroby gracza kasowane przez BK jak w grze (poza etapem 6).
2. **Jedna zasada na zjawisko:** wybor wyrobu - jeden lancuch w czterech miejscach (M1); **produkcja wsi wedlug zuzycia - jeden regulator (W3) w gore i w dol, takze dla koni wedlug
   kategorii (K4)** (v2: krok W1 i zawor W3 to jedna regula); **zapas koni - jedna funkcja `Stables.Reserve`** dla kupna, sprzedazy i zapasowych (K2, K3, K5); **udzial jazdy - szablon partii
   (175 = K3)**; **stan koni partii - jeden** (K0: predkosc, udzwig, pasza, zapas); lup - jedna funkcja dla zlota (A6), towaru i koni (K1); sprzedaz nadwyzek - jedna sciezka
   `MenPurse.SellArmorySurplus` (K2, N1; v1 blednie `GarrisonArmory`); przewoz - woz z czasem drogi (R3, `GarrisonCarts`; drewno dymarek woz wsi); kraina - `CultureAtCalibration` (E4/7, M1).
3. **Kolejnosc w dobie:** produkcja wsi (ProdPostfix; W2 ostatni krok) -> warsztaty (cykle godzinowe) -> zakupy przy postojach (Stables, `MenPurse`) -> bitwy (polegli `MapEvent` ->
   zgon konia -> rozdanie DTE) -> `CastlePurse`/`TownPurse` -> `MoneyLedger.Daily` -> `ArmsBook`/`HorseBook.Daily` (stan po wszystkim) -> stan koni partii (K0) -> regulator co 28 dob
   (doba decyzji, przed ProdPostfix nastepnej doby).
4. **Stan miedzy kampaniami (v2 uzupelniony):** `CraftRealm` (sesja), `ArmsBook`/`HorseBook` (sesja + CSV), stan koni partii K0 (pamiec dobowa, odbudowa przy wczytaniu), **`HarvestValve`
   (`SyncData` `arm_harvestvalve`: okna zuzycia i produkcji W3/K4 z wlasnych licznikow, niezaleznych od `LogEnabled`; mnozniki; doba decyzji)**, flaga jednorazowego przestawienia
   warsztatow M2 `arm_e6_inputs`, pula koni weteranow (`SaveText`), flaga R1 (zapis), W z misji (zapis) - reset w `ArmouryBehavior` konstruktorze (:395 jak E5/0); `_rank` warsztatow
   (dzien) - bez zmian. Ksiega towarow 146 dalej tylko log - zadna decyzja z niej nie czyta.
5. **Nie liczyc drugi raz:** czynnik rak L (E4/7) mnozy lupy - R2 nie dodaje drugiego czynnika rak; **v2:** wegiel dymarek - z tego samego drewna, ktore produkuje las wsi (`WoodlotStep`),
   cena - czlon dymarki przeniesiony z `FuelPerMetalKg` do lupy; udzwig konia - 60 kg ZAMIAST 20 kg gry (K5); pasza - z jednego stanu koni (K0), K3/K5 nie dodaja paszy; zapas koni -
   `AiMountSpareBuffer` wewnatrz `Stables.Reserve`, nie dodawany osobno; place od wartosci dodanej (D6-1) - wytop (wies) i kuznia (miasto) nie licza wsadu dwa razy; zold konnych (160) -
   K3 nie dodaje nowej premii; udzial jazdy - tylko szablon (bez tabeli K3).
6. **Martwy kod ozywiony:** zakup koni bez przerwy dla czekajacych (K3) - tylko przy czynnym K2(b) (v2), liczba zakupow na postoj ograniczona zapasem wedlug kategorii z histereza
   (lekcja pompy koni, K:Stables.cs:711-717); regulator W3 przy starcie - do d56 bez zmian, `RawNoRot` jak 174.3 (brak danych); licznik `NumberOfMounts` (K0) zaczyna liczyc konie ze stanem -
   udzwig gry +20 kg za kazdego (zgodnie z pelnym przeliczeniem gry, ktore robi to juz po wczytaniu).
7. **Kalendarz:** pora = 91 dob (364/4); dokumenty z 04.10 licza na 168 dniach (RC6 4.4) - ich liczb "na dobe" nie uzywamy.

### 5.3 Cudzy kod (sprawdzone w dekompilacji do tego projektu)

- G `WorkshopsCampaignBehavior`: `GetItemsToProduce` :820-842, `GetRandomItem` :1039-1047 (zapas bez miasta :1046), `GetRandomItemAux` :1049-1072 (wagi :1058), `IsItemPreferredForTown`
  :875-882, cykl gracza :584-640 (wsad schodzi dopiero po `CanPlayerWorkshopProduceThisCycle` :611-624), cykl notabla :750-773, `FillItemsInAllCategories` :970-988 wolany :138, :183.
- BK `WorkshopData.cs` - **v2: martwy kod** (`Tick()` pusty :33-35, prywatne `DoProduction` :37 bez wolajacych - czwartej drogi wyrobow nie ma); BK `BKBuildingsBehavior.RunStuds` :313-340; BK `DeleteOverProduction` :294 (nasz `RawNoRot`).
- G `WorkshopsCampaignBehavior.DecideBestWorkshopType` :1278-1337 - ruletka z sumy wynikow, przy samych zerach losowy typ (:1335); wolajacy :1086, :1097, :1258 (v2, M2).
- DTE `EveryoneCampaignBehavior` :139 (sluchacz `MapEventEnded`), :529-562 `OnMapEventEnded`, :751 `DistributeLootRandomly` (v2 - okno E6/0 i K1).
- G `DefaultInventoryCapacityModel` :54, :78 (20 kg za wierzchowca), :79-92 (juczne) (v2, K5); G `DefaultVillageProductionCalculatorModel` :22-27 (0 dla towaru spoza typu wsi - v2, W2);
  G `HeroSpawnCampaignBehavior.GiveInitialItemsToParty` :280-309 (konie i zywnosc z niczego - v2, K1); G `MobileParty.IsLastSpeedCacheInvalid` :3056-3070 (v2, koszt K0);
  G `DefaultVillageTypes` :173-235 (podaz stadnin wedlug kategorii, wielblady w `desert_horse_ranch` - v2, K4, M2).
- G `PartiesBuyHorseCampaignBehavior` :60-135 - zakup losowy do 8% kiesy (:74-87), sprzedaz wszystkiego poza jedzeniem i konmi (:96-99), sprzedaz najcenniejszych koni do 10 sztuk,
  gdy stado > 10% kiesy (:108-134) - dzis czynne, nie latane.
- G `ItemRoster` :431-456 wobec :546-576 (konie z modyfikatorem - v2: naprawa u zrodla w K0); G `DefaultPartySpeedCalculatingModel` :131-136, :233-236.
- G `VillageGoodProductionCampaignBehavior` :168, :199 - `OnItemProduced` wsi (zrodlo koni i lup w ksiedze).
- ROT: kultury 97 miast (`settlements.xml`), rumaki Dorne (`aserai_horse`, `t2_aserai_horse` "Sand Steed", `t3_aserai_horse`), jednorozce `unicorn1-3` kultury skagosi.
- **Do sprawdzenia przed kodem:** DTE - czy zabity kon w misji schodzi ze zbrojowni i czy rozdanie zbrojowni pokonanych obejmuje konie (K1); waga `iron` w DTE i BKROT (R1);
  juczne `VillagerPartyComponent` (RD6 8); kod BK, ktory usuwa konie kupione przez mieszczan (RD6 8 - E6/0 zmierzy skutek); **v2:** lancuch modeli pojemnosci (BK `VanillaModelTweakPatches`,
  `BKCaravansBehavior`) przed K5; ktora lista produkcji czyta tick wsi przy BK (M2 wielblady, W2); udzial J+KL szablonow po 175 (`tools/szablony_jazdy.py`) przed K3.
  `Debug.FailedAssert` w Shipping - zamkniete (no-op wg KB).

### 5.4 Gra Jeffa

- Klucze w `Armoury.json` (12.10): sa `SelfRepairMaterialFactor` 0.25, `MendMaterialMaxShare` 0.2, `AiMountMaxPerVisit` 10, `AiMountPurseShare` 0.15, `AiMountSpareBuffer` 2,
  `AiMountBuyCooldownDays` 4.0, `CavalryNeedsMounts`, `AiBuysMounts`, `NorthernFareEnabled`; brak: `OldStockToScrap`, `MineOutputStep`, `MineOutputMultiplier`, `WorkshopHandsPer1000People`,
  `VillageWoodlotLoads`, `BkRawNoRot`, `SupplyDemandTradePercent`, `MountedWageFactor`, `WorkshopForbiddenIds`, `AiMountMarketSharePercent`. Projekt nie zmienia domyslnych kluczy z pierwszej listy.
- NK: R1 (lupa), M2 (typy warsztatow i wsi przy starcie; poprawka CS i jednorazowe przestawienie - takze w starym zapisie, bez warsztatow Jeffa), K4 (przeglad stadnin). Jeff gra po zakonczeniu projektu (SP:888-890).
- v2 nowe klucze (zadnego nie ma w `Armoury.json`): `AiSpareHorseShare`, `FodderFromHorseState`, `CavalryShareByTemplate` (zamiast `CavalryShareByRealm`), `ValveStockDaysLow/High`,
  `HorseBreedingCutLive` (po biegu rocznym); usuniete z projektu: `LordSpareHorsesPerRider`, `ConsumerShareForStep`. `VillageOutputStep` przechodzi z W1 do W3.
- Widoczne dla Jeffa: towar "Iron Bloom" zamiast rudy; inne wyroby na targach krain (bez obcych zbroi, bez strojow bandytow); wiecej jazdy AI; ryby w okregach rybackich; tlocznie
  przestawione na browary; Twoja kuznia - wedlug pytania 1.

---

## 6. Pytania do Jeffa (tylko kierunek; liczby wybiera projekt)

1. **"Nikt nie kuje unikatow" - takze Twoja kuznia?** 30.08 pozwoliles sobie nauczyc sie unikatu przez przetop i kuc go bez limitu po opanowaniu (CH:10520-10523, :10567-10570).
   **Rada:** zakaz tylko dla kowali miast i AI; Twoja kuznia bez zmian (kujesz, co znasz, bez filtra kultury - "zostawic filtr", 09.10). Alternatywa: zakaz tez dla Ciebie (wtedy
   znika nauka unikatow i ich wzory w CRAFT).
2. **Konie, ktore "zjadaja" mieszczanie i ludnosc zamkow** (ok. 55 tys. zl koni dziennie w zamkach - CH:1770; ile w miastach - zmierzy E6/0). Twoja regula "kon ginie tylko zabity".
   **Rada:** mieszczanie kupuja tylko konie robocze na miejsce padlych ze starosci (stado robocze osady wedlug liczby ludzi, pada 1/15 stada rocznie) - reszta zostaje na targu dla
   wojska i karawan; przyczyna "starosc koni roboczych" w ksiedze. Alternatywa: mieszczanie nie kupuja koni wcale. (Wplyw: mniejszy zbyt stadnin wsi zamkowych - kompensuje go
   wieksza jazda K3.)
3. **(v2) Udzial jazdy krainy = szablon armii z 175?** Krytyka pokazala, ze osobna tabela K3 (Dolina i Reach 30%, Polnoc 20%, Smocza Skala 15%, Wolni Ludzie 1%) przeczy szablonom,
   ktore juz ustawiles w 175 (Polnoc 24%, Smocza Skala 10%, Wolni Ludzie 6%), i nie pasuje do rodow ROT (Targaryen ma kulture valyrian). **Rada:** TAK - jedna liczba: partia dazy do
   udzialu jezdzcow w swoim szablonie (Dothrakowie konni, Zelazne Wyspy tylko Harlaw, typowa kraina ok. 21%, srednio ok. 20% [S]). Do 20% dojdziemy tak szybko, jak stadniny dadza
   konie bojowe: w 120 dobach ok. 12-15% [S], pelny cel w biegu rocznym. Alternatywa: tabela wlasna K3 (wtedy 175 trzeba poprawic do tych samych liczb).

---

## 7. Odlozone (i dlaczego)

| Rzecz | Dlaczego nie teraz | Kiedy |
|---|---|---|
| hodowla czynna (K4) - **v2: tylko ciecie** (krok w gore i przesuniecie na bojowe sa czynne od testu etapu 6) | popyt na konie odwraca sie po ok. d360 (L728) | po biegu rocznym |
| (v2) zamowienie koni ze stad dalekich lenn wozem (K3 - dzis tylko wsie przy osadzie postoju) | przerzut natychmiastowy przeczy R15; woz z koniem to nowy przeplyw | po tescie etapu 6, razem z R3 |
| (v2) `lion_sword` w spisie unikatow (`tools/rot_uniques.py`, `docs/ROT-UNIKATY.md`) | spis czyta ok. 25 miejsc poza kuznia bez wylacznika - osobna zmiana danych z wpisem CHANGELOG; w M1 tylko "nikt nie kuje" | osobno, poza etapem 6 |
| (v2) R2 - wiecej rak i kopaln | warunek "po R1" sprawdzalny dopiero po tescie z R1 | po tescie etapu 6 (warunkowo) |
| przewoz prawdziwymi partiami zamiast "wozu bez partii" (R3, `GarrisonCarts`) | wiele partii na mapie - koszt i AI; ta sama regula co dzis dla zalog | po tescie etapu 6, razem z 8.12 (wozy, wyspy) |
| wywoz lup z Zelaznych Wysp i innych wysp | brak drogi ladowej - 8.12 | etap 8 |
| nowe wzory zbroi dla malych krain (Wyspy Letnie, Qarth, Yi Ti, Sarnor) | to dane ROT (XML), nie Armoury; handel dostarcza | tylko na slowo Jeffa |
| wzory rodowe wedlug siedziby rodu (Bolton tylko z Dreadfort) | lancuch dziala na poziomie krainy; siedziba = mapa rodow -> osad | pomysl |
| wlasny miecz z Qohoru (177-2) | "TAK, osobna paczka pozniej" (SP:880) | po etapie 6 |
| zdobycz noszona przez lorda a "unikat tylko u wlasciciela" (CH:2092 pkt 1) | dotyczy noszenia, nie kucia | etap 8 |
| wyroby gracza i sztandary kasowane przez BK (K:RawNoRot.cs:60) | jak w grze (kazdy wyrob gracza to nowy ItemObject - rozrost zapisu) | etap 8 (8.9) |
| W4, R4 | warunkowe | po tescie etapu 6 |
| udzial jazdy w zalogach (6.2% dzis) | Jeff mowil o armiach (partie) | po tescie etapu 6 - odczyt |

---

## 8. Krytyka - co przyjeto/odrzucono (v2, 12.10)

Dwie niezalezne krytyki (KA: ekonomia, konie, progi - 14 uwag; KB: kod, latki, kolejnosc - 17 uwag). Kazda sprawdzona samodzielnie w kodzie K, dekompilacji (G, BK, DTE), danych ROT
i logach; pelny wykaz z dowodami: `docs\etap6\KRYTYKA-E6.md`. **Przyjete wszystkie 31; odrzucone tylko czesci:** "K2 nie sprzedalby ani jednego konia" (K2 liczy zapas na partie, nie
na sume swiata - przesada, choc prog globalny byl nieosiagalny) i czysta poprawka "wegiel dymarki tylko z lasu okregu" (okreg daje ok. 4 ladunki na wies, dymarka potrzebuje ok. 58 na kopalnie).

| Uwaga | Werdykt | Gdzie w v2 |
|---|---|---|
| K1-01 wegiel dymarki z niczego i liczony dwa razy | przyjeta, inna poprawka: las okregu + polka miasta handlowego (woz wsi), `WoodlotStep` bez zmian, czlon dymarki przeniesiony do ceny lupy | R1 (rozdz. 2), E6/R1 |
| K1-02 konie bez strony podazy | przyjeta: podaz wedlug kategorii (E6/0), regulator w obie strony + przesuniecie zwykle -> bojowe (K4), cel K3 z rachunku | R9, E6/0, K3, K4 |
| K1-03 zawor nie zatrzymuje stosu, zapadka z W1 | przyjeta, rozszerzona: jeden regulator W1+W3, cel 0.95 x zuzycia, stos 30-60 dni, decyzja co 28 dob od d56, `RawNoRot` 0 od d56 | R5, R6, W1, W3 |
| K1-04 zapas koni bez kategorii, trzy definicje | przyjeta z zastrzezeniem: jedna `Stables.Reserve` wedlug kategorii, histereza 1.5, bez `LordSpareHorsesPerRider` | R11, K2, K5 |
| K1-05, K1-06, KB K3-klucze - klucz i tabela K3 | przyjete, wspolna poprawka: cel = udzial J+KL szablonu partii (175), tabela usunieta; pytanie 3 | R12, K3, rozdz. 6 |
| K1-07, KB K5 - udzwig podwojny, kolejnosc | przyjete: 60 kg zamiast 20, Priority.VeryLow, finalizery po K5 | K5 |
| K1-08 progi wobec E6/0 z innego biegu | przyjeta: liczniki na sucho i tozsamosci w tym samym biegu | E6/0, rozdz. 4 |
| K1-09 przeniesienia jako zrodla | przyjeta: zrodla / ujscia / przeniesienia (netto 0) | E6/0 ArmsBook, HorseBook |
| K1-10 lore Dothrakow, Wolnych Ludzi, Wolnych Miast | przyjeta: filtry lore w M1, tabele do przeliczenia z `MetalKg` | R3, M1 |
| K1-11 wielblady | przyjeta: wielblad = qartheen, przeniesienie wagi w listach produkcji | M2(d) |
| K1-12 pasza a stan koni | przyjeta: pasza z jednego stanu koni partii | K0(c) |
| K1-13 prog "Konie" | przyjeta: suma 7 dob, mianownik zrodla + ujscia, konie bohaterow w stanie | E6/0, K1, rozdz. 4 |
| K1-14 zywnosc z niczego w darach | przyjeta: konie i zywnosc z polki siedziby albo bez | K1 pkt 4 |
| KB K2-zla-sciezka | przyjeta: `MenPurse.SellArmorySurplus` (K2) i `AiSurplusPlan` (N1) | R11, K2, N1, 5.1, 5.2 |
| KB W2-ryby-z-zera | przyjeta: dopisek `Add(T / (1 + suma))`, ostatni krok, tozsamosc w tescie | R7, W2, 5.1 |
| KB M2-bramka | przyjeta: x0.01 zamiast 0 | M2(b) |
| KB K0-koszt | przyjeta: stan koni w pamieci dobowej, `SpeedPostfix` tylko czyta, prog kosztu | K0(b) |
| KB E60-okno-po-DTE | przyjeta: `DteWindow` - prefiks/postfiks na DTE `OnMapEventEnded` | E6/0, K1, 5.1 |
| KB K3-przerzut-z-lenn | przyjeta: tylko wsie `BoundVillages` osady postoju; dalekie - odlozone | K3 pkt 3, rozdz. 7 |
| KB K3-pompa-bez-K2 | przyjeta: przerwa zdjeta tylko z czynnym K2(b) | K3 pkt 3 |
| KB R2-warunek-z-R1 | przyjeta: R2 warunkowa po tescie etapu 6 | tabela paczek, R2 |
| KB W1-ksiega-tylko-log | przyjeta: okna w stanie `HarvestValve` w zapisie | W3, 5.2 pkt 4 |
| KB M1-zbedna-latka-GetRandomItem | przyjeta: tylko `GetItemsToProduce` + `GetRandomItemAux`; BK WorkshopData martwy | M1, 5.1, 5.3 |
| KB M1-lion-sword | przyjeta: "nikt" w `CraftRealm`; spis unikatow osobno | R4, M1, rozdz. 7 |
| KB K0-model-gry | przyjeta, wybrana naprawa u zrodla (`ItemRoster.OnRosterUpdated`), kulawe jak w grze | R13, K0(a) |
| KB M2-przestawienie-gracza | przyjeta: bez warsztatow gracza, flaga `arm_e6_inputs` | M2(a)(b), 5.2 pkt 4 |
| KB kolejnosc-galezie | przyjeta: E6/0 na scalonym A; kolejnosc scalania `ProdPostfix`; rownolegle K0, CS, M2 | rozdz. 3, 5.1 |
