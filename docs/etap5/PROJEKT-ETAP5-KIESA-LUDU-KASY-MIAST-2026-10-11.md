# PROJEKT ETAP 5 - kiesa ludu, kasy miast i glod (PLAN 5.1-5.13) - 11.10.2026, wersja 2

Status: SAM PROJEKT. Kod, gra, ustawienia i zapisy nietkniete, nic nie budowane, bez commita. **v2 (11.10): po dwoch krytykach** (E5K1-01..12 i K1-K9,
lista i rozstrzygniecia: `docs\etap5\KRYTYKA-E5.md`, skrot - rozdz. 8). Zmiany w tekscie oznaczone "**v2**". Najwazniejsze: podatek wojenny 184 dochodzi do daniny
(nie zastepuje jej), kotwica i pomiar budzetu raz na (miasto, kategorie, dobe), zadowolenie R14 przez migawke, notable przez kase miasta (takze wiejscy)
i bilans otwarcia przy tworzeniu swiata, targ przy zamku liczony od polki podzamcza i razem z E4/11, B1 wlaczany nowym kluczem (pulapka MCM), bez `HomePart`.

Podstawa (dowody nie sa tu powtarzane w calosci):
- rozpoznania `docs\etap5\rozpoznanie-A-logi.md` (**RA**), `rozpoznanie-B-kod.md` (**RB**), `rozpoznanie-C-dokumenty.md` (**RC**), `rozpoznanie-D-styki-E4.md` (**RD**);
- kod w grze: drzewo `SCR\e4\bledy` (galaz `w-toku/e4-bledy` 457f73e = zbior 31 + poprawka NightRest; WGRANIE 31 = dfb12df) - skrot **K:** = `SCR\e4\bledy\Armoury\src`;
  E4/6 **K46:** = `SCR\e4\46\Armoury\src` (galaz `w-toku/e4-6`, scalona w `w-toku/zbior-e4-koniec`), E4/7 **K47:** = `SCR\e4\47\Armoury\src` (`w-toku/e4-7`);
- dekompilacje: **G:** = `SCR\dec32\cs_full` (TaleWorlds.CampaignSystem 1.4.8), **BK:** = `SCR\e4tools\bkfull` (BannerKings.Redux), BEE = `SCR\e3\dec36\bee`, RBL = `SCR\r2dec\proj_RealisticBannerlord`;
- log biegu A zbioru 31 (120 dob nowej kampanii, wszystko co w grze): `SCR\kopia-z31-A\Armoury-2026-10-11_08-09-11.log` = **L:N**; bieg zimowy B: `SCR\kopia-z31-B\`;
  **sr28** = srednia dob 93-120 (RA), **d31-120** = srednia dob 31-120 (RB);
- dokumenty (docs\): **PLAN** = PLAN-DO-KONCA-MODA-2026-10-09.md (etap 5: :148-168), **SP** = STAN-PRAC.md (DECYZJE JEFFA - wiazace), **KL** = PROJEKT-KIESA-LUDU-2026-10-09.md
  (model kiesy ludu, wersja 2), **A15** = audyt-2026-10-09\15-BILANS-PANOW-ZAMKOW.md (Z15-0..Z15-8), **OB** = PROJEKT-EKONOMIA-OBIEG-2026-10-08.md, **E2** = PROJEKT-ETAP2-BANKRUCTWA-2026-10-09.md,
  **E3** = etap3\PROJEKT-ETAP3-SZCZELNOSC-STATKI-2026-10-10.md, **P4** = etap4\PROJEKT-ETAP4-KSIEGA-LUDZI-ZIMA-2026-10-10.md, **E410** = etap4\PLAN-KODU-E410.md,
  **P111** = paczki\111-k6-kasy-miast.md (stara 111 z 06.10, kod na galezi `paczki-na-120/111-k6-kasy-miast` 2ac5e72 - nigdy w grze).

Oznaczenia: [K] kod plik:linia, [P] pomiar z logu, [S] szacunek, [R] rachunek (symulacja KL/A15 na starym kodzie - do powtorzenia), [D] decyzja Jeffa (data).
Rok gry = 364 doby (kalendarz Armoury). "/d" = zl na dobe. Napisy w grze - po angielsku. W = wyrobnicy, R = rzemieslnicy, P = patrycjat (klasy kiesy ludu z KL 1.2).

---

## 0. Dla Jeffa - prostym jezykiem

1. **Dzis miasto zyje ze zlota z niczego.** Mieszczanie "kupuja" dziennie towar za ok. 420 tys. zl, ale nikt za niego nie placi - gra dopisuje zloto do kasy miasta
   z powietrza (L:31717). Druga reka gry kasuje z kas ok. 295 tys. zl dziennie (L:31686). Na tej pompie stoi renta miast dla panow (ok. 490 tys. dziennie).
   Lud nie ma wlasnych pieniedzy wcale. Pan bierze ok. 86% calej gotowki, ktora wplywa do wsi (RA rozdz. 4). Zamki jedza towar bez placenia (ok. 50 tys. dziennie).
   Zloto swiata rosnie o ok. 660 tys. zl dziennie (ok. 8% na miesiac); 430 tys. z tego to statki, ktore wlaczamy razem z etapem 5.
2. **Po etapie 5 ludzie zarabiaja i kupuja za swoje.** Kazde miasto i zamek dostaje "kiese ludu" (wyrobnicy, rzemieslnicy, patrycjat; ludzie zamku osobno).
   Ludzie dostaja pieniadze za prace i uslugi: czesc tego, co zolnierze wydaja w miescie (nocleg, gospoda), place w warsztatach, robota przy towarze, budowy, sluzba.
   Placa podatki (pan, gmina, w wojnie korona), czynsz, a za reszte kupuja chleb, piwo, sukno. Kasa miasta to odtad kasa kupcow, ktorzy handluja towarem.
   **Nic z niczego, nic w nicosc:** "zakupy" z powietrza i kasowanie znikaja.
3. **Wies:** pan bierze 35% utargu wozow (polityka podatkowa BK: niska 25%, zwykla 35%, wysoka 45%), wies zatrzymuje reszte i wydaje ja w miescie (sol, zelazo,
   sukno, garnki). Zamiast dzisiejszej renty 20% kiesy wsi kazdego dnia - stale oplaty panskie (sad, mlyn, targ). Twoje "30-50%" (09.10, pkt 15).
   Gdy wejdzie dziesiecina (ostatnia porcja), z utargu idzie jeszcze 5% na sept.
4. **Panowie zamkow nie traca** (Twoje "E", 09.10): pan miasta dzieli nadwyzke kupcow z korona po polowie, a korona oddaje to rentami wedlug rodzaju lenna
   (miasto 2, zamek 1.5, wies 0.25) - takze u Ciebie. Bez tego panowie zamkow traciliby 14% ludzi w wojnie i 32% w pokoju; z tym 1% i 4% (A15:245-247).
5. **Podatek wojenny od ludzi** (Twoje "C", 09.10) - **v2: dochodzi do dzisiejszej daniny, nie zastepuje jej.** W wojnie korona bierze od zarobku ludzi miast
   i podzamczy tyle, ile potrzebuje na zold - najwyzej 3%, a z polityka War Tax najwyzej 6%. "Potrzebuje" = korona musi pozyczac w Banku albo siegac do starego
   skarbu, zeby oddac rodom zold. Dzisiejsza danina z nadwyzki kas kupcow i kies wsi (ok. 97-106 tys. dziennie, L:31633) zostaje jak jest - rachunek, ktory obiecuje
   panom zamkow "-1%", liczyl ja razem z polowa zaworu dla korony (pkt 4), a podatek wojenny dokladal na wierzch; bez daniny korona w wojnie mialaby mniej niz dzis.
   Skutek, jak chciales: im wyzszy podatek, tym mniej ludzie kupuja (najpierw zbytki, potem sukno, chleb na koncu), spada ich zadowolenie i wolniej rosnie dobrobyt.
6. **Statki rozbitych druzyn kupuja porty** - wlaczone razem z podatkiem wojennym (Twoja decyzja 11.10).
7. **Zolnierze wydaja tez w polu** (markietani): kazdego dnia czesc sakiewki idzie do najblizszego miasta i wsi. Dzis sakiewki leza: 22.5 mln zl = 29 dni zoldu (L:31726);
   po zmianie ok. 5-12 dni. Notable nie dostaja ani nie traca zlota z powietrza (wymiana sily na zloto ma druga strone - **v2: kase miasta, w ktorym notabl handluje:**
   miasto domowe albo miasto targowe jego wsi), nowy notabl nie dostaje 10 000 z niczego. Wyjatek: notable i karawany z dnia tworzenia swiata - to bilans otwarcia (jak dar startowy).
8. **Targ przy zamku** (Twoje "F" i Q2, 09.10): woz wsi zamkowej najpierw sprzedaje swojemu zamkowi jedzenie, **v2: ktorego brakuje na polce targu zamku do 10 dni
   jedzenia podzamcza**, reszte wiezie do miasta. Robimy to razem z "zywnoscia warowni od ludzi okregu" z etapu 4 (E4/11) - to jedno zjawisko.
9. **Ostatnia porcja, po roku testu:** kasy miast bez zadnej doplaty z niczego, glod ma skutki (tydzien bez pieniedzy na chleb - spada zadowolenie; nie w pierwszych
   120 dniach), dziesiecina (polowa jalmuzna, polowa budowa septow), napis w menu osady "czy ludzi stac na chleb".
10. **Odpowiedz na Twoje pytanie "czy moge zaczac etap 5 czy dopiero po etapie 4":** projekt - teraz (ten dokument). **Teraz mozna tez pisac kod** pomiaru (E5/0)
    i porcji A (kasy miast, dwor, markietani i notable, drobne kas, BetterEconomy) - nie dotykaja ksiegi ludzi. **v2: rdzen (kiesa ludu, podatek, wyrownanie, statki) -
    kod tez mozna pisac rownolegle z etapem 4**, zaraz po scaleniu E4/7b do galezi koncowej etapu 4 (kiesa czyta tylko liczbe ludzi z ksiegi, a E4/8 zmienia liczby,
    nie sposob ich czytania). Na etap 4 czekaja tylko liczby (stawki ustawiamy z pomiaru) i testy. **Targ przy zamku - po kodzie E4/11** (to samo zjawisko), pieniadze
    weteranow - po E4/10. Testy etapu 5 - po tescie koncowym etapu 4; sam pomiar E5/0 jedzie w tym tescie (nic nie zmienia w grze).
11. **Wojska nie zmniejszamy** (Twoje 09.10 C i 10:45): prog 95-115 tys. w wojnie. Gdy jakas porcja go nie przejdzie, poprawiamy przeplywy pieniedzy, nie wojsko.
12. **Pelny skutek - w nowej kampanii** (dar startowy, kiesy ludu od pierwszego dnia). **Jedno pytanie** (rozdz. 6); reszte decyduje projekt.

---

## 1. Co dzis (liczby)

Bieg A zbioru 31 (120 dob nowej kampanii, kalendarz 364 dni), sr28 = doby 93-120, d120 = ostatnia doba (RA). "Cel" - z PLAN:168 z poprawkami RC rozdz. 4.

| Pozycja | Dzis (sr28; d120) | Cel etapu 5 | Dowod |
|---|---:|---|---|
| **"zakupy" mieszczan - zloto z niczego do kas miast** | 421 538; 425 318 (d2 334 824, d40 410 033 - rosna z dobrobytem, ludzi BK -2.6%) | 0 (placi lud) | L:31717, L:784, L:10660; RA rozdz. 9 |
| zamki: "zakupy" cofniete = towar zjedzony bez zaplaty | 50 105; 56 923 | < 1% konsumpcji | L:31541 |
| regulator kas gry: kasuje / dosypuje | 295 208 / 17 057 (+ zamki 953); d120 248 065 / 20 220 | 0 / 0 (5.8) | L:31686, L:31723 |
| netto "z niczego" kas miast (zakupy - kasowanie + dosypka) | ok. +143 tys. | 0 | RA rozdz. 1 [S] |
| kasy miast (97) wobec celu regulatora | 12.56 mln / 7.22 mln (start 17.4 mln wobec 6.5 mln) | stan ustalony = zapas + doplyw netto / 7% | L:31686, L:505 |
| renta (zawor) miast do panow, 100% pan | 487 312; 512 470 | od nadwyzki; pan 2/3 (A), 1/2 (B) | L:31726, L:31686; K:PopulationLaw.cs:1184-1196 |
| "pod tarcza zoldu" (obejscie regulatora) | 2.86 mln (d120) | 0 (tarcze zbedne) | L:31686; K:SoldierPay.cs:604-760 |
| BK rynek osady (1% kasy w nicosc) | -34 610 (d120 netto) | 0 | L:31718; BK BKSettlementBehavior.cs:463-490 |
| utarg taborow wsi: pan / majatki BK / kiesa wsi | 270 562: 59.0% / 11% / 30% | pan 35% (z majatkami), wies 65% (60% + sept 5% od C3) | L:31721 |
| renta 20% kiesy wsi dziennie | 115 754 (142% tego, co wies dostaje z utargu) | 0 (oplaty stale 4 zl na glowe rocznie) | L:31724; K:PopulationLaw.cs:1173-1192 |
| **pan bierze z gotowki wsi** | 86% (z majatkami 95%, z korona 97%) | **v2:** w utargu pan z majatkami = stawka polityki (25/35/45%); w gotowce G: pan z majatkami + oplaty - zwykla 28-45%, kazda polityka <= 50% (rozdz. 4) | RA rozdz. 4 [obliczone] |
| kiesy wsi: mediana / ponizej 1 000 zl | 276 zl / 436 z 571 | - (rosna do zapasu 15 dni zakupow) | L:31724 |
| panowie samych zamkow: udzial w D stalym rodow | 24.3% (d1 33.6%, d40 27.4%) | 30-35% | `budzet-rodow.csv` (RA rozdz. 5) |
| ... udzial w D swiata (28 dob) | 30.1% | >= 30% | RA rozdz. 5 |
| ... mediana kiesy, zmiana na 28 dob | -2.5% (bloki: -2.8 / +12.3 / +13.5 / -3.2%) | **v2:** srednia 4 blokow d252-364, rody w wojnie, >= -2% - odczyt (rozrzut blokow 16 pkt > wykrywana roznica 2.2 pkt) | RA rozdz. 5 |
| zatkane magazyny wsi zamkowych | 4.9% (12.8 z 260; 3.5-6.2%); d120 5.4%; zima B 6.5% | <= 5.6% | L:31610; RA rozdz. 10 |
| zold naliczony: partie / zalogi / karawany lordow | 536 318 / 204 705 / 14 383 | bez zmian | L:31721 |
| **sakiewki zolnierzy** | 22.5 mln = ok. 29 dni zoldu (d1 0.16 mln, d40 7.5 mln - przybywa ok. 100-190 tys./d) | <= 15 dni | L:31673, L:31726 |
| wydatek sakiewek "na zycie" (tylko przy wyjezdzie z miasta) | 384 239 (245-867 tys.) | + w polu (markietani) | L:31722; K:MenPurse.cs:258-310 |
| zold karawan notabli | nalezny 271 452, "w nicosc 0" - idzie do sakiewek ludzi karawan (E3/4), karawana wydaje cala sakiewke w miescie | czesc miejska 75/25 kupcy/lud (B) | L:31727; K:Seal31B.cs:27-40, :351-380 |
| pasmo notabli 4 500-10 500: w nicosc / z niczego | -132 804 / +93 571 | 0 / 0 (kasa osady) | L:31727 |
| nowy notabl +10 000 z niczego; kapital nowych karawan notabli | ok. 0.1 tys.; 69 821 (d120 30 000) | 0 / z kiesy notabla i kasy osady | RB 0.8; L:31718 |
| korona: wplywy dnia; danina wojenna z kas osad | 256 287; 96 763 (miasta ok. 90.4 tys.) | **v2:** danina zostaje (od nadwyzki D4) + podatek wojenny 184 od zarobku ludu wedlug potrzeby | L:31642, L:31632-31633 |
| korona: zwrot zoldu / renty (180); skarbce | 225 212 (100%) / 80 164; 57.9 -> 48.8 mln | zwrot >= 70% w wojnie, bez trendu skarbcow | L:31642, L:31716 |
| **zloto swiata, zmiana** | +662 448/d (ok. 7.8% na 28 dob); w tym statki NavalDLC 429 987, bitwy 89 976 (zrodla) | < 0.1% na 28 dob (ok. 8.5 tys./d przy 238 mln) | L:31716-31718 |
| reszta niewyjasniona ksiegi pieniadza | -1 302 (d120 -507) | < 10 tys. | L:31718; `progi-etap3.txt` |
| ludzie miast: ksiega ludzi / klasy BK | 10 471 333 (stale - `TownPeopleFrozen`) / 1 515 755 | jedna definicja (D1) | L:31696, L:31689 |
| konsumpcja miast: budzet gry / zuzycie BK (= "zakupy") | 795 180 / 425 318 (0.28 zl na glowe BK) | kotwica: najwyzej koszyk ludzi; **v2:** liczona wobec wydatku (ConsumedValue), nie budzetu - wydatek ogranicza polka (d120: 482 241 wobec 1 005 941) | L:31689 |
| czy ludzi stac na jedzenie / pelny koszyk | **nie do policzenia** (lud bez kiesy) | >= 95% / >= 98% | RA rozdz. 9 |
| warownie glodne; mediana zapasu | 0; 302 dni | bez zmian (progi E4) | L:31742 |
| wojsko lordow w wojnie (partie rodow) | 110 407 (d120) | 95-115 tys., podloga 85 tys. | L:31742 |

**Najwazniejsze przyczyny** (RB, sprawdzone w kodzie do tego projektu):
- **"Zakupy" z niczego:** prefiks BK `ItemConsumptionPatch.Prefix` (BK EconomyPatches.cs:501-600) zdejmuje towar z polki i robi `town.ChangeGold(num6)` (:555) - nikt nie placi.
  Budzet kategorii liczy BK `CalculateBudget` (:1085-1101): popyt x indeks^0.15 + dobrobyt / 1000 / indeks - od dobrobytu, nie od ludzi (dlatego rosnie, choc ludzi ubywa).
  **v2 (K1/E5K1-09):** BK wola `CalculateBudget` przy KAZDEJ pozycji polki (petla :514), z reszta budzetu kategorii po poprzedniej pozycji (:552 `categoryDemand[kat] = num2 - num3 x cena`),
  i za kazdym razem dodaje czlon dobrobytu; pozycji, ktorej nie ma na polce, petla nie odwiedza. Budzet kategorii na dobe powstaje raz - w `ItemConsumptionBehavior.UpdateDemandShift`
  (G :178-205, slownik przekazany do `MakeConsumption`; BK tej metody nie lata, a dla zamkow BK wola cale `MakeConsumptionInTown` refleksja - BK BKSettlementBehavior.cs:631).
  Na `CalculateBudget` siedzi juz nasz `HistoricalPrices.BudgetPostfix` (K:HistoricalPrices.cs:832, wpiety :1004-1005: bron 0 - 174.0, `TownUse` surowcow, czlon dobrobytu / przelicznik),
  a `RawPrice.HouseBudget` wola te metode refleksja do ceny surowcow (K:RawPrice.cs:226-236).
- **Regulator:** `ItemConsumptionBehavior.MakeConsumptionInTown` (G :61-71) po konsumpcji wola `UpdateTownGold` = `GetTownGoldChange` = 0.25 x (10 000 + 12 x dobrobyt - kasa)
  (G DefaultSettlementEconomyModel.cs:75-79; czynny model vanilla - L:537). Dla zamkow zamyka to 110 (K:CastlePurse.cs:236-286), dla miast nic.
- **Renta miasta:** min(nalezna od ludzi, 7% kasy ponad 20 000) x dekret, 100% panu (K:PopulationLaw.cs:1184-1196); osada spoza tabeli kultur (:1164) albo z ludnoscia 0 (:1166) nie placi -
  stad "10 miast bez placenia" (RB 5.4).
- **Cztery definicje "zapasu kupcow"** (RB rozdz. 3): staly 20 000 (`TownRentFloorGold`, K:Settings.cs:643 - renta, danina, polityki, Spoils), 10 000 + 12 x dobrobyt
  (`Seal31.TownReserve`, K:Seal31.cs:97, `Spare` :101 - RealPayer, porty 181), zamek `CastlePurse.Reserve/Spendable/PayFloor` (K:CastlePurse.cs:191, :377, :401), BK 50 000 + 25 x dobrobyt.
- **Wies:** BK `CalculateVillageTaxFromIncome(Village,int)` = 70% (Low 50, High 90) (BK BKTaxModel.cs:271-284), BK `VillagerSettlementEnterPatch` daje wsi polowe reszty (BK EconomyPatches.cs:918),
  112 dopisuje druga polowe; potem renta 20% kiesy dziennie (K:PopulationLaw.cs:1173-1196) zabiera wiecej, niz wies dostaje z utargu.
- **Notable:** gra zamienia zloto ponad 10 500 na sile (zloto w nicosc) i sile na zloto ponizej 4 500 (zloto z niczego) (G NotablePowerManagementBehavior.cs:47-61); nowy notabl
  +10 000 z niczego (G NotablesCampaignBehavior.cs:41-47).
- **Bitwy:** pula lupu zwyciezcow = suma `GoldLost` pokonanych (wodz: 5% kiesy, najwyzej 10 000 - G DefaultBattleRewardModel.cs:104-107, :197-210), dzielona wedlug wkladu
  (G MapEvent.cs:1497-1526); do `PlunderedGold` dochodzi zloto za statki zatopione w bitwie morskiej (G MapEvent.cs:1800-1817 - to zamyka 3.3 `ShipPrizesToPort`).
  Wyplata `MapEventParty.CommitGoldChanges` (G MapEventParty.cs:499-524). Netto z niczego w "bitwach": RB +72 tys./d (d31-120), d120 ok. +41 tys. (zrodla 85 762 - netto 45 147, L:31718) - do rozbicia (E5/0).

---

## 2. Reguly (jedna na zjawisko: skad zloto, dokad zloto)

**Zasady wspolne** (z etapow 2-4 i decyzji Jeffa): kazdy przeplyw ma platnika i odbiorce (przelew `GiveGoldAction` albo para `ChangeGold` z licznikiem w `MoneyLedger`);
zloto z niczego / w nicosc tylko nazwane i liczone (do 5.8: dosypka regulatora ponizej zapasu; jednorazowo: dar startowy - bilans otwarcia); jedna regula dla gracza i AI;
kazda paczka ma wylacznik MCM (wylaczony = stan sprzed paczki); wyjatek per obiekt - licznik potkniec, bez globalnych wylacznikow (CLAUDE.md 7); kwota zapisana = to, co naprawde
zeszlo (migawka przed/po, nie wynik modelu). **Odbiorca zapasowy** (KL S13): pan bez zywej glowy rodu -> gmina osady (do B: kasa osady); rod bez krolestwa -> czesc korony do pana;
brak kaplana -> jalmuzna do W, budowa do gminy; wies bez pana -> korona, bez krolestwa - zostaje w kiesie wsi; kazdy przypadek liczony w logu.

**D1 - kogo liczy kiesa ludu (jedna definicja ludzi; RD D1, P4 R0 "klasy BK nie sa ludzmi").** Liczba ludzi osady = ksiega ludzi (`PopulationLaw.PeopleOf`, K46 PopulationLaw.cs:215;
miasta - stan "t:" E4/4, wsie - DOM). Skala pieniadza i udzialy klas - z BK, zamrozone na osade w dniu kalibracji (jak k_v w E4/3): `koszyk(osada, dzis) = ludzie_BK(osada, kalibracja)
x koszyk_na_glowe(klasa) x PeopleOf(dzis) / PeopleOf(kalibracja)`; tak samo "wolne glowy wsi" dla oplat i zakupow wsi. Pobor (E4/7), glod (E4/6), migracja i karawany z ludzi miasta
(E4/8) zmieniaja koszyk; suma pieniadza zostaje w skali rachunku KL (KL L9). Kalibracja w pierwszej dobie z wlaczonym kluczem; osada bez danych BK - wzor BK `GetDesiredTotalPop` (KL 1.2).

**D4 - jedna "nadwyzka kasy osady".** `TownPurse.Spare(osada)` = kasa - zapas kupcow (miasto: 10 000 + 12 x dobrobyt = dzisiejszy cel regulatora; zamek: `CastlePurse.Reserve`).
**v2 (E5K1-12):** lokata ludu NIE lezy w kasie osady - jest czescia kiesy W (osobny posiadacz poza `SettlementComponent.Gold`, przelew przy odkladaniu i zwrocie), wiec nie ma jej
w `Spare`, a ksiega nie liczy jej dwa razy. Uzywaja `Spare` WSZYSCY, ktorzy biora z kasy "z nadwyzki": zawor (renta), podatki korony i polityk, Road Tolls, Spoils (skup lupu),
RealPayer E3/1 (dzis `Seal31.Spare`), porty 181, notable (164c). **v2 (E5K1-03) - zakupy towaru: jedna regula dla miast i zamkow (B-2).** Nasze sciezki zakupu przez kase
(zakup sprzetu od ludzi `MenPurse`/`MenUpgrade`, `ArmyClothing`, `SupplyDemand`/`ShelfBuy`, skup niewolnikow BK `BkSeal34`, paser, kontrakty 174, wywoz kupcow) placa tylko z nadwyzki
ponad zapas kupcow - `TownPurse.Spendable` = `Spare` dla miasta, `CastlePurse.Spendable` dla zamku (dzis zamek, K:CastlePurse.cs:371-391: "zakup z zapasu oplacala w praktyce dosypka
z niczego"). Sciezki gry i BK (tabory wsi przez BK `SellGoodsPatch`, karawany) placa jak dotad z calej kasy - plon dalej kupiony; ich czesc ponizej zapasu oplaca dosypka trybu 1
i ta czesc jest osobnym licznikiem ("dosypka finansujaca zakupy ponizej zapasu") z progiem C1. Dno 20 000 (`TownRentFloorGold`) zostaje tylko w trybie 0 (A1 wylaczone).
Tarcze zoldu i dworu znikaja (R7).

| # | Zjawisko | Platnik -> odbiorca (regula) | Dzis | Paczka |
|---|---|---|---|---|
| R1 | konsumpcja mieszczan (towar z polki) | **lud (kiesy W/R/P) -> kupcy (kasa miasta) 98% + gmina 2% (akcyza - jedyne miejsce akcyzy, v2)**; ile towaru: najmniej z (budzet gry, koszyk ludzi D1 x indeks cen) - kotwica KL L3; **v2 (K1):** m = min(1, budzet ludu / przewidywany wydatek kategorii) - wydatek, nie budzet, bo wydatek ogranicza polka; m stosowane **raz na (miasto, kategorie, dobe)**: postfiks na `UpdateDemandShift` mnozy slownik budzetu przed `MakeConsumption`, a postfiks `CalculateBudget` (po `HistoricalPrices.BudgetPostfix`) mnozy tylko czlon dobrobytu / 1000 / indeks (wynik BK nie jest mnozony przy kazdej pozycji polki - inaczej m^k); obie latki czynne tylko po `Campaign.GameStarted` (gra konsumuje tez przy tworzeniu swiata - G :22-40); czego lud nie zaplaci - towar znika bez zlota ("zjedzone bez zaplaty", licznik). Do B1: caly towar "bez zaplaty" (jak zamki od 110) | 421 tys./d z niczego | A1 (cofniecie), B1 (placi lud) |
| R2 | regulator kasy gry | tryb 1: wynik ujemny = 0 (nic nie znika), dosypka tylko do zapasu kupcow (liczona - jedyne zrodlo z niczego kas); tryb 2: regulator nic (5.8); miasta i zamki ta sama regula | -295 / +17 tys. | A1, C1 |
| R3 | nadwyzka kasy (D4) | jedna funkcja `TownPurse.Spare`; **v2:** nasze zakupy towaru tylko ze `Spendable` (= `Spare`; miasto i zamek jedna regula B-2), sciezki gry/BK z calej kasy - czesc ponizej zapasu liczona | 4 definicje | A1 |
| R4 | zawor kasy miasta | co dobe 7% nadwyzki (R3): (od B1) najpierw 5% dla patrycjatu (KL L6), reszta **pan / korona: A1 2/3 : 1/3 (jak zamek, klucz 114), B3 1/2 : 1/2 (Z15-5)**; czesc pana -> `PopulationLaw.RentToday` (D staly 166, powinnosci, budowy), `RentOf` (podstawa rady 3.5, K:CouncilWages35.cs:193); dekret BK skaluje czesc pana; miasto rodu bez krolestwa - calosc panu; martwy pan - nic nie schodzi (czeka); oblezone - nic (R22); bez pulapu "naleznej od ludzi" (zostaje liczba w logu). **v2 (E5K1-08b) - styk z K3:** zwrot korony tnie wydatki ludzi partii we wlasnych miastach rodu (K:KingdomTreasury.cs:246-253) dzis w 100% (`CrownRefundOwnTownsCut` 1 - "wrocilo zaworem"); przy czynnym A1 ciecie = udzial pana w zaworze (`TownRentLordShare` x (1 - 5% P od B1)), bo tyle naprawde wraca panu; markietani (R9) we wlasnym miescie rodu wchodza do tego samego licznika (ta sama regula co `OnLeft`) | 7% ponad 20 000, pulap naleznej, 100% pan, 10 miast nic | A1, B3 |
| R5 | dar startowy kas osad | bilans otwarcia (jednorazowo, w pierwszej dobie przed rentami): (od B1) najpierw zapasy startowe kies ludu tej osady (KL 5.3: W 10, R 21, P 30, podzamcze 10, wsie 15 dni), reszta ponad zapas kupcow przycieta raz - jak zamki w 110 (`TrimStartGift`, K:CastlePurse.cs:466); stary zapis - nic | 17.4 mln wobec 6.5 mln zapasu w d1 | A1, B1 |
| R6 | BK rynek osady | gorna galaz (kasa ponad limit BK: 1% w nicosc, z bledem znaku pol z kiesy notabla) pominieta dla miast i zamkow; dolna (notabl z 30 000 doplaca 1 000 pustej kasie) zostaje - przelew | -34.6 tys./d | A1 |
| R7 | tarcze zoldu (107b) i dworu (162m) | zbedne przy R2 (nic nie kasuje): bez znacznikow, znaczniki z zapisu porzucone. **v2 (K6/E5K1-08a): bez `HomePart`** - wycofany w B-2 jako bledny (przewidywal 17.7 tys./d, wracalo 5.9 tys. - K:CastlePurse.cs:45-52, K:SoldierPay.cs:446-451), a przy czynnym 165 zold zalog w ogole nie jest podstawa zwrotu korony (K:SoldierPay.cs:452, K:CrownIncome.cs:28); styk zaworu ze zwrotem - tylko K3 (R4) | 2.86 mln pod tarczami | A1 |
| R8 | dwor pana | glowa -> kasa siedziby (miasto albo zamek) jak 162m; niewydany przydzial sprzetu po `AiGearDaysCap` (30 dni) -> dwor; (od B1) 80% kupcy / 20% lud (KL 1.3); gracz bez dworu [D 08.10] | sprzet tylko lezy | A2, B1 |
| R9 | zycie zolnierzy | sakiewka -> przy wyjezdzie z miasta (jak dzis) **i w polu: 10% sakiewki dziennie (markietani)** - 85% kasa najblizszego niewrogiego miasta, 15% kiesa najblizszej niewrogiej wsi (OB :171); ta sama kolejnosc co `OnLeft`: rezerwa na naprawy, odziez (150), oszczednosci K1, potem zycie - nic drugi raz; (od B1) czesc miejska 75% kupcy / 25% lud (uslugi, KL 1.3), takze karawany (E3/4) | tylko przy wyjezdzie; 29 dni zoldu w sakiewkach | A3, B1 |
| R10 | notable | **v2 (E5K1-04):** wymiana zlota i sily ma druga strone - **kase miasta, w ktorym notabl handluje**: notabl miasta - kasa tego miasta; notabl wsi (ok. 89% notabli: 2 379 z ok. 2 681, L:31735) - kasa miasta targowego wsi (`Village.TradeBound`; brak albo zamek - `Village.Bound`, zamek przez `CastlePurse.Spendable`). Kiesa wsi nie jest strona: nie ma definicji nadwyzki do B1 (`Seal31.Spare` wsi = 0, K:Seal31.cs:97-104; mediana kiesy 267 zl), a do B1 renta 20% oddawalaby wplaty notabli panom. Nadwyzka ponad 10 500 -> kasa miasta (przelew; sila jak w grze), brak ponizej 4 500 -> z `Spare` tej kasy (brak - sila nie schodzi, zlota nie ma); nowy notabl - 10 000 z `Spare` tej kasy (zloto zmarlego notabla zostaje w osadzie - E3/8c), brak - mniej; kapital nowej karawany notabla: z kiesy notabla ponad 4 500, reszta ze `Spare` kasy miasta startu (kupcy inwestuja), brak - mniejszy kapital (ta sama regula co E3/7). **v2 (E5K1-05/K4) - bilans otwarcia:** przed `Campaign.GameStarted` (tworzenie swiata: `SpawnNotablesAtGameStart` +10 000 kazdemu, ok. 26.8 mln) i dla karawan z `isInitialSpawn` regula gry zostaje, zloto liczone raz w ujsciach jak dar startowy R5 (wzor K:CaravanCapital.cs:36 `Live`); liczba pominietych w linii startowej | +93.6 / -132.8 tys., 10 000, kapital 30-70 tys. z niczego | A3 |
| R11 | kiesa ludu miasta (W/R/P) | jedyne wejscie `TownFolk.Earn` (KL 1.3: uslugi 25%, dwor 20%, budowy 75%, place rzemiosla 35% wartosci wyrobu, robocizna handlu 20/10/4%, obrot P 3% + 5% zaworu, praca u bogatszych, sluzba, czynsz, leczenie, roboty gminy, kopalnie); jedyne wyjscie na towar `PayForGoods` (R1, tam akcyza 2%); podatki przy Earn (KL 1.4: pan 1/2/3%, **v2 (E5K1-11): gmina tylko cech 1% od zarobku R** - akcyza nie przy Earn, korona R15); zapas 10/21/30 dni, nadwyzka 7%/d na towar (W) albo 70% place / 30% towar (R, P); lokata W najwyzej 30 dni, gdy brak towaru; niewolni tylko w krolestwach z niewola w kanonie (KL L8) | brak | B1 |
| R12 | kiesa podzamcza | wejscia: zaloga 25%, dwor 20%, robocizna zamku, 15% zaworu kasy zamku (`CastleDuesSuburbShare`, K:CastlePurse.cs:92); wyjscia: towar z kasy zamku 0.18 zl na glowe (D1), pan 3% zarobku (Z15-4) - **placi za to, co dzis zamki zjadaja bez zaplaty** | 50 tys./d zjedzone bez zaplaty | B1 |
| R13 | wies | utarg taborow: **pan 35%** (postfiks x0.5 na stawce BK; niska / zwykla / wysoka = 25 / 35 / 45%; majatki BK swoj udzial z czesci pana - BK EconomyPatches.cs:927), **wies reszta** (65%; od C3 60% + sept 5%); renta 20% kiesy -> **oplaty stale 4 zl rocznie na wolna glowe (D1)** do pana (Z15-4); wies kupuje w miescie targowym 0.07 zl na glowe dziennie + nadwyzka 7%/d ponad 15 dni zakupow (sol, narzedzia, sukno, garnki, piwo; KL 1.5). **v2 (E5K1-06) - jedna definicja udzialu:** "pan" = pan z majatkami BK (majatki biora z czesci pana, BK EconomyPatches.cs:925-927); G = utarg + markietani + przelewy gry + sakiewki zwolnionych; udzial = (czesc pana z utargu + oplaty stale) / G, osobno wedlug polityki BK wsi; sept dopiero od C3 | pan 86% gotowki wsi | B1 |
| R14 | zadowolenie ludu z konsumpcji | zadowolenie BK idzie za tym, co ludzie **naprawde kupili** wobec pelnego koszyka (Afford), nie za popytem obnizonym kotwica. **v2 (K2) - mechanizm "zamiast" doslownie:** BK daje +0.001 za KAZDA pozycje polki z budzetem (BK EconomyPatches.cs:549; w grupie General dziesiatki pozycji dziennie), suma obcieta do 0..1 (BK EconomicData.cs:151), a na dobrobyt dziala dopiero ponizej 0.85 (BK BKProsperityModel.cs:60) - jedno -0.0015 dopisane po BK niczego by nie zmienilo. Dlatego: migawka publicznej tablicy `EconomicData.Satisfactions` (BK EconomicData.cs:62) w istniejacym nawiasie 110 (`CastlePurse.ShelfPostfix` na `DeleteOverproducedItems` = przed konsumpcja), po konsumpcji (`ConsumePostfix`) dla grupy z Afford < 0.98: wartosc = migawka + min(0, delta BK) - 0.0015 (ujemna delta BK z galezi dokupu jedzenia zostaje); Afford >= 0.98 - wynik BK bez zmian; bez latki na `UpdateSatisfaction` (krotka metoda - JIT moze ja wbudowac). **Sprostowanie v2:** BK NIE karze za brak towaru na polce (petla nie odwiedza pozycji, ktorej nie ma) - -0.0015 tylko w galezi dokupu jedzenia przy zapasie <= 10% limitu (:532-544). Skala: -0.0015/d = ok. 100 dob od 1.0 do progu 0.85 - kalibracja z E5/0 (rozdz. 5.2 pkt 9); jedzenie (5.9): Afford_jedzenie < 0.95 przez 7 dob i kampania >= 120 dob, osobny wylacznik [D 09.10 A] | BK +0.001 za kazda pozycje polki | B1 (bez jedzenia), C2 (jedzenie) |
| R15 | **podatki wojenne korony (danina + 184)** | **v2 (E5K1-01, E5K1-02) - jedna zasada "wojna kosztuje poddanych", kazdy platnik od swojej podstawy (tabela KL 1.4, kolumna korony):** (a) **danina zostaje** jak dzis (K:KingdomTreasury.cs:392-404): kasy kupcow 1%/d nadwyzki - od A1 ponad zapas D4 (nie ponad 20 000); kiesy wsi 1.5%/d - od B1 tylko nadwyzki ponad 15 dni zakupow (KL 1.4); x2 przy War Tax; War Tax 5% podatku miast (`RulingIncomePostfix` :502-516) - jak dzis. Rachunek A15 (Z15-5: "-1% / -4%"), progi B3 i "wojsko W -2%" liczono z danina (sim_kiesa2.py:405-415: `lay` obok `(1 - s_lord) x val_r`), a podatek wojenny dokladano na wierzch (A15 rozdz. 5) - zdjecie daniny "bo korona ma 1/2 zaworu" liczyloby zawor dwa razy (korona ok. -48 tys./d w wojnie, panowie miast +48 tys.). Scalenie daniny z 184 - dopiero po wyniku testu B, osobna decyzja. (b) **184 dochodzi:** przy `Earn` od zarobku ludu (W 0.5 s, R 1.0 s, P 1.5 s; podzamcze 1.0 s); **stawka s** co 7 dob wedlug potrzeby: **potrzeba** = w ostatnich 7 dobach korona wziela kredyt 186 (CrownBorrow - dzis Bank pokrywa brak zwrotu tego samego dnia, wiec "niedoplata zwrotu" jest ~0 az do zaleglosci, K:CrownBorrow.cs:12-21; L:31642 "w tym z kredytu Banku 19 467") albo nalezny zwrot byl wiekszy niz wplywy dnia bez 1/360 zapasu, albo skarbiec < zapas wojny -> +0.5 pkt; **zapas wojny** = max(`CrownReserveGold` 500 000, 60 dob sredniego naleznego zwrotu z 28 dob); skarbiec > 2 x zapas wojny, bez kredytu i bez siegania do zapasu przez 7 dob, dlug 186 = 0 -> -0.5 pkt; **0-3%, z polityka War Tax 0-6%**; pokoj - s do 0 w 4 tygodnie; odbiorca `Kingdom.KingdomBudgetWallet`; skutki - przez kiese (mniej zakupow, zbytki pierwsze) i R14 (zadowolenie -> dobrobyt BK -> W w przyroscie R1 etapu 4) | danina z kas kupcow i wsi 96.8 tys./d + polityka | A1 (podstawa D4), B1 (podstawa wsi), B2 (184) |
| R16 | renty korony (180) | reszta wplywow dnia rodom wedlug stalych wag lenn: **miasto 2, zamek 1.5, wies 0.25** (Z15-5; nigdy wedlug zalogi - Z8), gracz z lennem tak samo [D 09.10 E i 10] | 3 / 1 / 0.25 | B3 |
| R17 | warownia je z okregu (5.11; D3) | **v2 (E5K1-07, K5):** woz wsi przypisanej do zamku jedzie najpierw do swojego zamku, gdy **jedzenia na polce targu zamku jest mniej niz na 10 dni zuzycia podzamcza** (zuzycie = jedzenie zjedzone przez ludnosc BK zamku w konsumpcji - `SoldItems` kategorii jedzenia, srednia 7 dob; Jeff Q2 "jedzenie dla podzamcza na 10 dni", SP:852) - **nie** spichlerz zalogi (`FoodStocks / -FoodChange`: mediana 302 dni, ujemny bilans 4 z 130 zamkow - L:31610, warunek bylby martwy albo, bez znaku, zawsze czynny); spichlerz zalogi zostaje poza R17 (zasila go danina wsi z E4/11 pkt 8). Sprzedaje tylko brakujace jedzenie do 10 dni zuzycia; zamek placi cene swojego targu ze `Spendable` (K:CastlePurse.cs:377) - **wlasna sprzedaz** (prefiks na vanilla `SellGoodsForTradeAction.ApplyInternal` dla zamku, zwraca false; vanilla placi z calej `town.Gold`, G SellGoodsForTradeAction.cs:43-58); znacznik "byl w zamku" na wozie (gra po sprzedazy kieruje woz z > 1 pozycja znow do `SendVillagerPartyToTradeBoundTown`, G VillagerCampaignBehavior.cs:286-296 - bez znacznika petla); reszta ladunku do miasta (MarketRoad / MarketCarts jak dzis); **jedno zjawisko z E4/11 pkt 8** (danina jedzenia wsi dla warowni od ludzi okregu - PLAN-KODU-E411.md:64-68): jedna funkcja `CastleMarket.FeedFromDistrict`, jedna liczba jedzenia okregu; ta sama funkcja pozniej dla miast (GL 2.13 krok B - rozdz. 7) | caly plon do miasta [D 05.10, zmienione 09.10 Q2] | B4 |
| R18 | statki (3.3) | rozbita druzyna, handel z portem i pryzy - przez kase portu z nadwyzki (R3) (paczka 181, K:ShipsToPort181.cs) | ok. 300-430 tys./d z niczego | B5 |
| R19 | pieniadze weterana i najmu (D5) | sakiewka idzie z czlowiekiem: do domu -> kiesa wsi (jak dzis), do karczmy -> zostaje przy nim w puli WETERANI i jest wydawana w miescie karczmy jak "zycie" (R9); zaplata za najem -> sakiewka najetego (KL 1.3 "docelowo sakiewka najetego"), nie kasa miasta | sakiewka zawsze do wsi; najem do kasy miasta (ok. 30-44 tys./d) | B6 |
| R20 | lup w bitwie | wodzowie zwyciezcow dostaja tylko to, co naprawde zeszlo z pokonanych (migawka; wzor 3.2 `BeatenPurse`); zloto za statki zatopione - R18 | ok. +41-72 tys./d z niczego (do rozbicia) | E5/0 pomiar, A6 (warunkowo) |
| R21 | dziesiecina (5.10) | 5% utargu wsi (z czesci wsi: wies 60%) i od zarobku ludu W 1% / R 2% / P 3% przy `Earn` -> kaplan osady (BK `Religion.GetClergyman`, BK Religion.cs:156) -> 50% jalmuzna (kiesa W miasta / kiesa wsi), 50% budowa septow (75% place ludu, 25% material - NoteSale) [D 09.10 B] | brak | C3 |
| R22 | osada oblezona (5.4) | nie placi nic: zawor, podatki, danina/184, renta, oplaty - jedna regula dla miasta, zamku i wsi (zamek i danina juz tak: K:CastlePurse.cs:573, K:KingdomTreasury.cs:389) | renta miasta placona w oblezeniu | A4 |
| R23 | BetterEconomy, akcje gracza (G) | wplata gracza do skarbca miasta/zamku BEE -> **kasa osady** (przelew, BEE niczego nie dopisuje z niczego); "dar dla notabli" -> gracz -> notable osady wedlug sily (relacja wedlug gry) [D 09.10 G]; pozostale akcje BEE zamkniete jak w 170 | akcje szare (170) | A5 |

**Pogodzone bez pytania Jeffa / poprawki do PLAN** (RC rozdz. 4 - zrodla tam):

| Miejsce | Bylo | Jest |
|---|---|---|
| PLAN 5.7, 5.11 | "Ty" | zdecydowane 09.10 04:25 (E, F) i 04:55 (Q2) - SP:844, :852-853 |
| PLAN 5.7 | renty "zamek 1" | zamek 1.5 (A15:277, :351; ES:166-168; E2:888) |
| PLAN 5.5 | "wies zatrzymuje 30%" | pan 35%, wies 65% (60% + sept 5% od C3) - decyzja 15 (SP:813, :827; A15:272) |
| PLAN 5.12 | wiekszy udzial wsi | **skreslone** - zawarte w Z15-5 (A15:289, E2:888) |
| PLAN:250 (pytanie E) | panowie zamkow -9% / -17% | -14% / -32% bez wyrownania, -1% / -4% z Z15-5 (A15:245-247) |
| PLAN:168 "Skonczone" | kiesa panow zamkow -10% na 28 dob; wojsko "jak 2.7" | >= -2% na 28 dob wojny i udzial w D swiata >= 30% (A15:341-342, E2:890); wojsko 95-115 tys., podloga 85 (E2:882) |
| PLAN 5.6 | "cel ok. 100 tys./d" | **v2:** stawka 184 wedlug potrzeby (R15) NA WIERZCH daniny; wielkosc 184 nie jest progiem (wynika z potrzeby); oczekiwane 30-60 tys./d (E2:792-793, E3:532) [S] - do sprawdzenia rachunkiem przed testem B (E5/B2) |
| danina wojenna z kas kupcow + War Tax 5% | (v1: "dwie reguly - scalic w 184") | **v2:** jedna zasada z podstawa wedlug platnika (R15): kupcy i wsie - danina od nadwyzki, lud - 184 od zarobku; zawor 1/2 dla korony (B3) jest kompensata dla panow zamkow, nie zamiennikiem daniny (E5K1-01) |
| KL | "172", pan 70%, wojsko -5% (pytanie 7.3), ludzie z BK, start z daru 111 | 173; pan 35% (R13); zamiast -5% wojska - 184 [D 09.10 C, 10:45]; D1; R5 |
| PLAN 5.10 | "po roku testu 5.5" (KL 7.2) a A15 liczy sept od razu | po roku testu B (C3) - mniejsze jedno wgranie B; do tego czasu wies 65%, pan 35% (w widelkach 30-50%) |
| E3:697 podatek warsztatow lordow | etap 5 | zrobione w D11 (WGRANIE 30) - skreslone |
| E3:701 tarcza zoldu karawan | etap 5 z K6 | skreslone - tarcze znikaja w A1 (R7) |
| OB bezpiecznik korony dla kas miast | drugi bieg 5.8 | nie robimy (PLAN 5.8 "korona bez bezpiecznika"); zapasowo w ryzykach C1 |
| RA "do sprawdzenia: zold karawan notabli" | gdzie idzie | sprawdzone: do sakiewek ludzi karawan (E3/4, K:Seal31B.cs:27-40); napis "po planowanej zmianie" w linii (K:MoneyLedger.Obieg.cs:654) jest nieaktualny - poprawic przy A1 |

---

## 3. PACZKI etapu 5 w kolejnosci

Kolejnosc wedlug styku z etapem 4 (RD rozdz. 2): najpierw pomiar (bez zmian w grze), potem pieniadz bez ludzi (A - kod teraz), potem rdzen na ksiedze ludzi (B - jedno wgranie
5.5-5.7 wedlug PLAN:158 i [D 11.10] "statki razem z podatkiem wojennym"), na koncu to, co PLAN wiaze z rokiem testu B (C).

| Paczka | Nr | PLAN | Co | Zalezy od | Kod | Wiel. | NK | Test |
|---|---|---|---|---|---|---|---|---|
| **E5/0** | 195 | 5.x (pomiar) | kiesa ludu "na sucho" (D1), nadwyzka D4, bitwy rozbite, modele czynne, jedzenie z targu do spichlerza, gotowka wsi, sakiewki w dniach; narzedzie `--grupa etap5` | - | **teraz** | D | nie | **jedzie w tescie koncowym E4** |
| E5/A1 | 111' | 5.1 (+5.4 "10 miast") | kasy miast: R1 (cofniecie), R2 tryb 1, R3, R4 (2/3), R5, R6, R7 | - | teraz | D | dar startowy | zbior A |
| E5/A2 | 162 | 5.2 | dwor pelny: R8 (sprzet niewydany -> dwor; hak 80/20 = 0 do B1) | - | teraz | S | nie | zbior A |
| E5/A3 | 163 + 164c | 5.3 | markietani R9, notable R10 (pasmo przez kase miasta - takze wiejscy, nowy notabl, kapital karawan notabli; bilans otwarcia przy tworzeniu swiata) | - (test po scaleniu z E4/8 - wspolny cel `CreateCaravanParty`) | teraz | **M (v2)** | **tak (v2): bilans otwarcia; rozruch markietanow w starym zapisie** | zbior A |
| E5/A4 | 196 | 5.4 | osada oblezona R22; prog podatkow zamku = D4 | A1 | teraz | M | nie | zbior A |
| E5/A5 | 197 | G (PLAN:251) | BEE: prawdziwa wplata gracza i dar dla notabli R23 | A1 | teraz | S | nie | zbior A |
| E5/A6 | 198 | (prog "zloto swiata") | lup wodza tylko z kiesy pokonanego R20 - **tylko gdy E5/0 pokaze > 5 tys./d na ladzie** | E5/0 | po E5/0 | S | nie | zbior A albo B |
| **E5/B1** | 173 | 5.5 | kiesa ludu: R1 (placi lud), R11-R14 (bez jedzenia), R5 (zapasy startowe), hak R8/R9 75/25 | A1-A3; **v2: kod po scaleniu E4/7b do `zbior-e4-koniec`** (rownolegle z E4/8); stawki z E5/0 biegu A | **v2: kod teraz-po E4/7b; kalibracja po biegu A** | D | tak | zbior B |
| E5/B2 | 184 | 5.6 | **v2:** podatek wojenny R15 od zarobku ludu, NA WIERZCH daniny (danina i War Tax jak dzis) | B1 (czynna kiesa) | jw. | S | tak | zbior B |
| E5/B3 | Z15-5 | 5.7 | wyrownanie: R4 1/2, R16 2/1.5/0.25 | B1 (czynna kiesa) | jw. | M | tak | zbior B |
| E5/B4 | 199 | 5.11 | targ przy zamku R17 (polka podzamcza; jedna funkcja z E4/11 pkt 8) | B1, E4/6, E4/8, **E4/11 (v2)** | **po kodzie E4/11** | **M (v2)** | nie | zbior B |
| E5/B5 | 181 ON | 3.3 | statki: 3 wylaczniki ON, porty z D4 | A1, B2 [D 11.10] | kod teraz (zmiana domyslnych) | M | nie | zbior B |
| E5/B6 | 201 | (E410:56, :60) | pieniadze weterana i najmu R19 | **E4/10** | po E4/10 | S | karczmy | zbior B |
| E5/C1 | 202 | 5.8 | regulator tryb 2 (kasy bez dosypki) | rok B: reszta < 10 tys./d przez 28 dob | po roku B | M | nie | zbior C |
| E5/C2 | 203 | 5.9 | glod ma skutki: galaz jedzenia R14 + jedna definicja glodu z E4/6 (D2) | B1, E4/6 | po roku B | S | nie | zbior C |
| E5/C3 | 204 | 5.10 | dziesiecina R21 | B1 | po roku B | S | tak | zbior C |
| E5/C4 | 205 | 5.13 | napis "czy ludzi stac na chleb" w oknie E4/7 | B1, E4/7 | po roku B | M | nie | zbior C |
| - | - | 5.12 | skreslone (Z15-5) | - | - | - | - | - |

Numery: 194 pominiety jak w P4 (odnosnik w tabeli owiec CHANGELOG 31m); 195-205 wolne (grep CHANGELOG, STAN-PRAC, docs\etap4: "200" i "205" wystepuja tylko jako priorytet Harmony
i liczba w tabeli). Kazda paczka = osobny commit + wpis CHANGELOG + wlasny wylacznik; **zbiory testowe wedlug zasady Jeffa (11.10 03:05)**: zbior A (A1-A5, ew. A6) - jeden test,
zbior B (B1-B6) - jeden test i jedno wgranie, zbior C - jeden test. Baza kodu E5/0 i A: `w-toku/e4-bledy` (457f73e); scalenie z galezia koncowa etapu 4 przed testem
(pliki wspolne - rozdz. 5.1). **v2 (K9):** baza kodu B1-B3 i B5: `w-toku/zbior-e4-koniec` po scaleniu E4/7b (ba3a07c) - rownolegle z E4/8 (zmienia tylko liczby stanu miast w
`PopulationLaw.Daily` przy tym samym `PeopleOf`, K:PopulationLaw.cs:214-222); B4 po kodzie E4/11, B6 po E4/10. **Przelaczniki (v2, K3):** B2, B3 (czesc korony z zaworu 1/2
i wagi rent) i podzial 75/25 dzialaja tylko przy czynnej kiesie (`TownFolkLive && TownFolkMode >= 1`) - inaczej 184 nic, zawor 2/3 i renty jak przed B; haki
`CastleDuesSuburbShare` (K:CastlePurse.cs:92) i `VillageRentYieldsToFees` (K:VillageTakings.cs:59) - dzis `static readonly` - zamienione na wlasciwosci liczone z tego samego
warunku (wylaczenie B1 cofa je w tej samej dobie). Standard testu: autotest 40 dob + zapis z doby 362 (9 dob) - 0 bledow naszych modow, potem bieg z progami paczek i progami etapow 2-4.

---

### E5/0 - Pomiar etapu 5 (195, tylko log) - D - KOD TERAZ

**Cel.** Przeliczyc KL na dzisiejsza gre na prawdziwym swiecie zamiast na rachunku z 2e235ea (RC pkt 5, 14): kiesa ludu liczona co dobe wedlug regul R11-R15 "na sucho" -
z prawdziwych przeplywow, ale bez ruszania zlota. Do tego braki pomiaru z RB rozdz. 6-7 i RD rozdz. 6. Gra bez zmian: zadna latka nie zwraca false, nie zmienia wyniku ani zlota.

**Pliki**
| Plik | Zmiana |
|---|---|
| NOWY `TownFolk.cs` | klasa kiesy ludu z trybem `TownFolkMode`: 0 = nic, **1 = na sucho (E5/0)**; czynna (B1) = **v2:** nowy klucz `TownFolkLive` z B1 przy `TownFolkMode` >= 1 (K3). Na sucho: stan kies W/R/P, podzamcza, wsi, gminy, lokaty **w pamieci sesji** (bez zapisu), `Earn`/`NoteSale`/`PayForGoods` tylko licza; kalibracja D1 w pierwszej dobie sesji; koszyk, kotwica m, Afford (KL 5.2 z poprawka S4: mianownik = pelna potrzeba); podatki R15 z s = 3% "na probe"; przelewy, ktore B1 zrobi naprawde, jako liczby. **Kazde wywolanie z zywego kodu we wlasnym try, po przelewie gry, nigdy miedzy zdjeciem a dopisaniem** (P4 U24) |
| `MenPurse.cs` (:299), `SoldierPay.cs` (:473), `ClanBudget.cs` (:621), `BuildFunding.cs` (:98, :182, :185), `WorkshopLaw.cs` (:321-322, :453), `WorkshopTrade.cs` (:329, :503), `AiGear.cs` (:705, :747), `ArmyClothing.cs` (:181, :237-238) | jedna linia po kazdym przelewie: `TownFolk.Note*(osada, kwota, rodzaj)` (tryb 1 = licznik) - te same miejsca, ktore B1 zamieni na `Earn`/`NoteSale` (RB 5.5 tabela) |
| `CirculationWindows.cs` | okno O14 "bitwy" (:656-662, wpiecie :1768) rozbite: pula lupu z `GoldLost` pokonanych (wodzowie / partie bez wodza) wobec `PlunderedGold` zwyciezcow; czesc za statki zatopione (z `Measure31` - K:Measure31.cs:35, :424) osobno |
| `PopulationLaw.cs` | odczyt `PeopleOf` w kalibracji D1 (bez zmian logiki) |
| `MoneyLedger.Obieg.cs` | linia "Gotowka wsi (E5/0)" z liczb juz liczonych (utarg, przelewy, sakiewki zwolnionych, renta, majatki, korona); poprawka napisu :654 |
| `ArmouryBehavior.cs` | `TownFolk.Reset()` w konstruktorze (:395); `TownFolk.Daily()` po `CastlePurse.Daily` (:1459), przed `BuildFunding`; linie po `MoneyLedger.Daily` (:1542) |
| `SubModuleMain.cs` | `TownFolk.ApplyAll` (okna tylko do odczytu, **v2 (K1):** postfiks void na `ItemConsumptionBehavior.UpdateDemandShift(Town, Dictionary<ItemCategory,float>)` - slownik budzetu kategorii dnia, raz na miasto i zamek; postfiks void na BK `CalculateBudget` tylko do sumy czlonu dobrobytu, i tylko wewnatrz nawiasu konsumpcji 110 - wywolania `RawPrice.HouseBudget` refleksja (K:RawPrice.cs:231-234) nie licza sie; oba czynne po `Campaign.GameStarted`) we wlasnym try |
| `tools/sprawdz_logi.py` (drzewo `SCR\e4\bledy\tools`) | grupa `--grupa etap5` (progi rozdz. 4; panowie zamkow z `budzet-rodow.csv`, rodzaj=zamek - wzor `SCR\e5A\csv.py`) |

**Okna (dowod):** BK `EconomyPatches.CalculateBudget(Town, float, ItemCategory)` - private static w klasie zewnetrznej (BK EconomyPatches.cs:1085-1101), wolana z prefiksu BK (:523)
**przy kazdej pozycji polki** z reszta budzetu (v2, K1/E5K1-09) oraz refleksja z `RawPrice.HouseBudget` - dlatego **v2: budzet gry dnia = slownik z `UpdateDemandShift`
(G ItemConsumptionBehavior.cs:178; wolany tuz przed `MakeConsumption`, :66) + suma czlonow dobrobytu z `CalculateBudget` w nawiasie konsumpcji**, nie suma `__result`;
kategorie z pusta polka sa w slowniku (budzet jest, zakupu nie ma). Na `CalculateBudget` siedzi juz `HistoricalPrices.BudgetPostfix` (K:HistoricalPrices.cs:832) - odczyt Last, po nim.
Osady bez PopulationData - vanilla `CalculateDailySettlementBudgetForItemCategory` (G DefaultSettlementEconomyModel.cs:81-84), odczyt (d120: 0 takich osad, L:31689). `MapEventParty.CommitGoldChanges`
(G :499-524) - istniejace okno O14. `DefaultSettlementFoodModel` - odczyt `Town.SoldItems` z kategoria `BonusToFoodStores` (G DefaultSettlementFoodModel.cs, galaz `includeMarketStocks`): ile jedzenia
z konsumpcji targu wchodzi do zapasu warowni (styk S4 - rozdz. 5).

**Linie logu** (raz na dobe przy LogEnabled + `TownFolkMode` >= 1; napisy logu po polsku jak wszystkie linie pomiaru):
- start: `Kiesa ludu (E5/0): tryb na sucho | okna: budzet BK, budzet gry, bitwy, jedzenie z targu; BRAK: - | kalibracja D1: osad z danymi BK n, bez danych m (wzor zapasowy) | modele czynne: lojalnosc X, dobrobyt Y (postfiksow warstwy BK na lancuchu: k), zywnosc Z`
  (rozstrzyga RB 7 pkt 2 i 4: czy `BKLoyaltyModel` jest czynny, czy delty BK licza sie dwa razy na RBL i Default).
- `Kiesa ludu (na sucho): dzien N | miasta: dochod Y wedlug pochodzenia (wojsko / dwor / rzemioslo i budowy / handel / karawany / zysk kupcow / wewnatrz ludu), wydatki Z (towar, czynsz, podatki), kiesa X (W / R / P), stac jedzenie / pelny W / R / P %, miast ze stac < 95%: n, kotwica m (srednia, min), zjedzone bez zaplaty A | podzamcze ... | wsie: zakupy, oplaty, nadwyzka | gmina | podatek wojenny przy s = 3%: T | kasy miast po B1 (szacunek): K`.
- `Nadwyzka kas (E5/0): dzien N | kasy, zapas kupcow (D4), ponad zapasem; pod tarczami; kto wzial z nadwyzki: renta, danina, polityki, Road Tolls, Spoils, RealPayer, porty | ponizej dna 20 000: n`
  + **v2:** `zakupy kas miast z pasa ponizej zapasu: nasze sciezki X (po A1 = 0), gry/BK (tabory, karawany) Y | K3: ciecie zwrotu Z wobec czesci pana z zaworu W | korona: kredyt 186 dzis, zwrot ponad wplywy bez 1/360, zapas wojny (60 dob zwrotu)`.
- **v2:** `Notable (E5/0): dzien N | pasmo 4 500-10 500 - miasta: w nicosc a / z niczego b; wsie: c / d (kasa miasta targowego: nadwyzka e) | notable wiejscy n z N`.
- `Bitwy (E5/0): dzien N | pula z pokonanych: wodzowie a (5%, najwyzej 10 000), partie bez wodza b | zwyciezcy dostali c | statki zatopione d | z niczego netto c - a - b - d`.
- `Gotowka wsi (E5/0): dzien N | wplynelo G = utarg + przelewy + sakiewki zwolnionych | pan (licznik + renta) x%, majatki BK y%, korona z%, wies w% | wedlug polityki BK: niska / zwykla / wysoka`.
- `Jedzenie z targu (E5/0): dzien N | do zapasu warowni z konsumpcji: miasta a, zamki b (szt.) wobec zuzycia warowni c`
  + **v2 (dla B4):** `zamki: jedzenie na polce targu d szt., zuzycie podzamcza e szt./d (srednia 7 dob), polka < 10 dni zuzycia: f z 130 (mediana g dni)`.
- **v2:** `Zadowolenie BK (E5/0): dzien N | srednie Food / Industrial / General / Luxury (0..1), osad ponizej 0.85: n; BK +0.001 pozycji a, -0.0015 b` (odczyt do kalibracji R14).
- dopisek w "Obieg": `sakiewki ludzi = D dni zoldu naliczonego`.
- CSV `Logs\<sesja>\kiesa-ludu.csv` (osada na wiersz, co 7 dob).

**Klucz Settings:** `TownFolkMode = 1` (int; **v2: 0..1**) w grupie "The townsfolk's purse":
`// 0 off; 1 = the townsfolk's purse is counted every day from the real flows (wages, services, the basket by the people of each town) and written to the log; it changes nothing in the game unless Town Folk Live is on`.
Domyslnie 1 - tylko log, progi B stoja na tych liniach. W `Armoury.json` Jeffa klucza nie ma (sprawdzone 11.10: z kluczy etapu 5 jest tylko `WeeksPerSeason`).
**v2 (K3):** kiesa czynna NIE przez zmiane domyslnej tego klucza (kazdy zapis MCM po wgraniu E5/0 przyszpila 1 w `Armoury.json`), tylko przez nowy klucz bool `TownFolkLive`,
ktory powstaje dopiero w B1 (domyslnie true); czynna = `TownFolkLive && TownFolkMode >= 1`. Opis `TownFolkMode` bez "2 = live".

**Test:** w tescie koncowym etapu 4 (364 doby + zima 120 + zapis 9 dob): T1 0 bledow naszych modow, linia startowa bez BRAK; T2 koszt <= 15 ms/dobe (**v2:** liczony na wywolaniach
na pozycje polki, nie na 30 kategoriach); T3 suma "na sucho" zamknieta (dochod ludu - wydatki - zmiana kies = 0 +- 1%); T4 rozbicie bitew = zrodla "bitwy" z ksiegi +- 2%;
T5 odczyty (bez progu): stac jedzenie / pelny, m, zjedzone bez zaplaty, udzial pana w gotowce wsi, nadwyzka D4, sakiewki w dniach, modele czynne. **v2 (K7):** z biegu E4 bierzemy
tylko: bledy, koszt, rozbicie bitew (decyzja o A6) i gotowke wsi; **stawki B1-B4 kalibrujemy z linii E5/0 z biegu A** (E4 + porcja A - dochod patrycjatu z zaworu, uslugi
z markietanow, zapasy startowe R5 i zysk kupcow zaleza od A1 i A3).

**Ryzyka:** (1) "na sucho" liczy przeplywy, ktore B1 zmieni (np. kupcy po B1 maja mniej robocizny i place) - to stan wyjsciowy, nie przewidywanie; rachunek KL `sim2` (katalog `7016f733...\kiesa\sim2` istnieje)
powtorzyc na danych E5/0 **z biegu A, przed testem B** (**v2, E5K1-01:** z danina jak dzis, 184 wedlug potrzeby z pulapem 3/6%, wlaczonymi B5 i A6 - z tego progi B3, wojska i stawki;
wojsko dla A6 osobnym wierszem). (2) Nowe wywolania w zywym kodzie - lista miejsc do recenzji (10 plikow wyzej), kazde w try. (3) Okno na `CalculateBudget` - prywatna metoda BK; brak = "BRAK" w linii startowej.

**Zaleznosci od E4:** brak w kodzie (czyta `PeopleOf`); liczby maja sens dopiero na kodzie koncowym E4 - stad jazda w tescie koncowym E4.

---

### E5/A1 - Kasy miast (111', PLAN 5.1 + "10 miast bez placenia" z 5.4) - D - KOD TERAZ

- **Zakres:** NOWY `TownPurse.cs` na dzisiejszym kodzie (wzor: stara 111 - P111 pkt 1-11 i jej recenzja - oraz 110 `CastlePurse`; **przepisac, nie przenosic** - stara jest sprzed 110 S14/B-2/B-4,
  114, 162m, 164a, 165, 166, 181; RB 0.1): R1 (cofniecie "zakupow" - migawka kasy przed i po konsumpcji, jak zamki), R2 tryb 1, R3 (`Spare`, `PayFloor` dla miast), R4 (zawor 7% nadwyzki,
  `ValveSplit.Split` z udzialem pana 0.67 - K:ValveSplit.cs:22-28), R5 (`BeforeRents`, flaga `arm_townpurse`), R6, R7.
- **Pliki:** `TownPurse.cs` (NOWY); `CastlePurse.cs` - trzy latki 110 (`ShelfPostfix` :236, `ConsumePostfix` :251, `RegulatorPostfix` :272) oddaja miasta do `TownPurse.OnShelf/OnConsumed/OnRegulator`
  (jedno miejsce wpiecia, jak w starej 111); `PopulationLaw.cs` - przy czynnym A1 miasto nie idzie przez rente z :1184-1196 (zawor `TownPurse.CollectRent` pisze `RentToday`, `_rentBy`, `DayTownRent`
  - te same pola, wiec 166, 169c i rada 3.5 widza to samo), filtr :1164/:1166 nie dotyczy miast; `SoldierPay.cs` - `Hold` (:648), `HoldCourt` (:604), `RegulatorPostfix` (:694) bez znacznikow przy A1,
  `HeldOf`/`CourtOf` (:803, :809) = 0, **v2: bez `HomePart` (K6)**; `KingdomTreasury.cs` K3 (:246-253) - ciecie = udzial pana w zaworze przy czynnym A1 (R4); `Seal31.cs` - `Spare` (:101) -> `TownPurse.Spare`;
  **v2:** nasze sciezki zakupu (`MenPurse` :525, `MenUpgrade` :470, `ArmyClothing` :233, `SupplyDemand` :562, `BkSeal34` :192/:210) - `CastlePurse.Spendable` -> `TownPurse.Spendable` (miasto = `Spare`; E5K1-03); `ShipsToPort181.cs` - nadwyzka portu przez `TownPurse.Spare`;
  `KingdomTreasury.cs` - danina (:371), polityki (:473), Road Tolls (:528) od `TownPurse.TaxFloor` zamiast 20 000; `SpoilsSeal.cs` (:133, :413, :476) - miasta przez `PayFloor`; `CrownIncome.cs` -
  czesc korony z zaworu miast do "wplywy dnia" (jak `CastlePurse.CrownToday`); `MoneyLedger*.cs` - "zakupy" +0, pozycje ticku `MTownTrim`, `MTownCrown` (przenumerowac `Marks`, K:MoneyLedger.cs:86-95),
  dar startowy w ujsciach raz; `ArmouryBehavior.cs` - `TownPurse.Reset` w konstruktorze (:395), `arm_townpurse` we wlasnym try w `SyncData`, `BeforeRents` przed `PopulationLaw.Daily` (:1453),
  `TownPurse.Daily` zaraz po nim, przed `CastlePurse.Daily` (:1459); `SubModuleMain.cs` - `TownPurse.ApplyAll` po `CastlePurse.ApplyAll`; `Settings.cs` + `McmSettings.cs` (`python tools/gen_mcm.py`).
- **Okna latek (dowod):** `ItemConsumptionBehavior.MakeConsumption(Town, Dictionary<ItemCategory,float>, Dictionary<ItemCategory,int>)` - private static (G ItemConsumptionBehavior.cs:142); prefiks BK zwraca false
  dla osady z PopulationData (BK EconomyPatches.cs:505-600), wiec **tylko postfiksy** (zasada 110, K:CastlePurse.cs:29-31). `DeleteOverproducedItems(Town)` (G :79) - prefiks BK return false (BK :606-609),
  nasz postfiks = migawka kasy przed konsumpcja. `SettlementEconomyModel.GetTownGoldChange(Town)` (G DefaultSettlementEconomyModel.cs:75-79; wolane z `UpdateTownGold` G :70-77 po konsumpcji) - latac
  kazda deklaracje (jak K:CastlePurse.cs:307-321). BK `BKSettlementBehavior.HandleMarketGold(Settlement)` - private (BK :463-490, z `RunStep` :241-243) - prefiks bool `Priority.Last` (po oknie O18,
  K:CirculationWindows.cs:1789); recenzja starej 111 sprawdzila, ze prefiks lapie wolanie z lambdy BK (P111 pkt 56).
- **Klucze Settings** (nazwy ze starej 111): `TownPurseRegulator` = 1 (`// 0 = the game's daily regulator as before; 1 = it never deletes coin from a town purse and tops it up only up to the merchants' reserve, counted in the log; 2 = it does nothing`),
  `TownPurseFloorGold` = 10000, `TownPurseFloorPerProsperity` = 12, `TownFolkBuyWithoutMinting` = true, `TownRentLordShare` = 0.67 (`// of what a town purse gives up each day its lord keeps this share and the kingdom treasury the rest - you and the AI alike`),
  `TownPurseTrimAtStart` = true, `TownPurseNoBkSkim` = true (**v2: bez `CrownRefundSkipsHomeGarrisons`** - K6); nowe opisy `TownRentShare`, `TownRentFloorGold` (**v2:** "with Town Purse Regulator 0 only: floor of a town purse for rents and taxes, as before"), `TownWageShield`, `CrownRefundOwnTownsCut` (**v2:** "with the town purse valve on, the cut follows the lord's share of the valve").
- **Linie logu:** start `TownPurse: ... CZYNNA, tryb regulatora 1, latki: ...; BRAK: -`; raz `TownPurse: poczatek kampanii - dar startowy: kasy A -> B (zdjeto C w 97 miastach)`; **rozszerzona** "Kasy miast (169c)"
  (L:31686 - bez nowej linii): `zawor: panom L + koronie K | regulator: dosypal X (tryb 1, n miast ponizej zapasu), kasowanie zablokowane Y | "zakupy" cofniete C | 10 miast bez placenia: 0 | BK rynek osady pominiety w a miastach (Q)`;
  "Przeplywy osad (kasy miast)": "zakupy" +0, "regulator" tylko dosypka, "udzial korony z zaworu"; "Pieniadz swiata (bilans)": "regulator kas skasowal 0", dar startowy w ujsciach.
- **Rachunek [S]** (sr28, bez zmian poza A1): doplyw netto kas miast przed zaworem = zmiana +54 + zawor 487 - zakupy 422 + regulator netto 278 = **ok. 398 tys./d**; w stanie ustalonym zawor = doplyw netto
  (P111 "nadwyzka = doplyw / 7%"), czyli panom ok. 265 tys. + koronie ok. 133 tys. zamiast 487 tys. panom (rody -89 tys./d, ok. -8.6% D stalego). **A3 oddaje kasom miast wiecej, niz A1 zabiera** -
  rozdz. E5/A3; bilans zbioru A [S]: kasy miast ok. +50-100 tys./d wobec dzis, zloto swiata ok. -100 tys./d mniej przyrostu (zakupy -422, kasowanie +295, pasmo notabli -94 +133, BK rynek +35, kapital karawan -50).
- **Test:** zbior A (rozdz. 4).
- **Ryzyka:** (1) rody tracace "pompe" w pokoju (P111 ryzyko 2) - prog wojska i glow; (2) dosypka trybu 1 rosnie w miastach-zaulkach (liczona; cel 5.8); (3) danina wojenna od nadwyzki ponad zapas (nie ponad 20 000) -
  poczatkowo nizsza, korona dostaje 1/3 zaworu; (4) MCM: zaden z kluczy nie istnieje w `Armoury.json` Jeffa (sprawdzone 11.10); (5) kolizje - rozdz. 5.1 (te same trzy latki co 110/114, tarcze 107b/162m,
  `Seal31.Spare`, 181, 165, 166, 3.5, 169c, SpoilsSeal, 36b, 174.2, 171, SupplyDemand - RB 5.1 "Kolizje").
- **NK:** dar startowy tylko w nowej kampanii (stary zapis - nic, jak 110).
- **E4:** niezalezne w kodzie (pliki etapu 4 poza `PopulationLaw.cs` - tam inne metody, scalanie tekstowe z E4/4, E4/6 +23, E4/7 +1, E4/8). **v2 (K7):** zawor 7% to regula, nie kalibracja;
  test zbioru A osobno po tescie koncowym E4, bo koniec etapu 4 to 364 doby + zima 120 + 728 dob (P4:553) - kazdy niezdany prog A w tym tescie wymuszalby powtorke dlugich biegow E4.

### E5/A2 - Dwor pelny (162, PLAN 5.2) - S - KOD TERAZ

- **Zakres:** R8 - (a) przydzial sprzetu niewydany po `AiGearDaysCap` (K:Settings.cs:848; trzymany w K:ClanBudget.cs:387-388) przechodzi na dwor (OB :277); (b) hak `CourtFolkShare` = 0.20 czynny dopiero przy czynnej kiesie (v2: `TownFolkLive`)
  (KL 1.3: 80% kupcy / 20% lud); (c) bez tarczy dworu przy A1 (K:ClanBudget.cs:622 -> `SoldierPay.HoldCourt`); (d) siedziba-zamek - wplata do kasy zamku (zawor ja oddaje; podzamcze 15% przy B1).
- **Pliki:** `ClanBudget.cs` (`Court` :598-624, `Seat` :553), `Settings.cs`. **Latki cudzego kodu:** brak (przelew wlasny); oplaty dworu BK (`CourtGrace.AddExpense`) juz ida do kasy siedziby (E3/8b).
- **Klucze:** `CourtGetsLapsedArmsShare` = true (`// arms money a lord's house has not spent within the cap of days goes to its court - the kitchens, stables and servants of its seat - instead of lying idle`), `CourtFolkShare` = 0.2.
- **Linie:** "Budzet rodow (166)" + `sprzet niewydany -> dwor X`; "Obieg": dwor do kas siedzib (miasta / zamki).
- **Test:** zbior A - dwor rosnie o przelew z sprzetu, nic w nicosc; gracz bez dworu; D staly bez zmian definicji.
- **Ryzyka:** wiecej zlota do kas siedzib = wiekszy zawor = wiecej renty panom miast - mierzyc w udziale panow zamkow (prog B). **E4:** `ClanBudget.cs` zmieniaja E4/7 (:739-822), plany E4/8-E4/10 - inne metody; scalanie.

### E5/A3 - Markietani (163) i notable (164c) (PLAN 5.3) - M (v2) - KOD TERAZ

- **Zakres:** R9 i R10 (**v2:** notable wiejscy przez kase miasta targowego; bilans otwarcia przed `Campaign.GameStarted` i dla karawan `isInitialSpawn` - regula gry, zloto raz w ujsciach;
  markietani we wlasnym miescie rodu do licznika K3).
  - **Markietani:** raz na dobe, petla po partiach rodow (w tym gracza - jedna regula) i ich ludziach poza osada: 10% sakiewki po rezerwach (`AiWear.OutstandingCost`, oszczednosci K1 `MenUpgrade`) -
    85% kasa najblizszego niewrogiego miasta (zamek nie - zamek ma podzamcze od B1), 15% kiesa najblizszej niewrogiej wsi w stanie Normal; te same liczniki co `OnLeft` (`ClanIncomeBook.NoteOwnPaid`,
    `CrownIncome.NoteOwnSpend` K3, nowa pozycja "markietani" - dzis drukowana jako "-", K:MoneyLedger.Obieg.cs:567). **Rozruch** w zapisie, gdzie sakiewki maja > 15 dni zoldu: stawka od 1% do 10%
    w 28 dob (flaga `arm_sutlers`) - inaczej jednorazowy splyw ok. 15 mln do miast (stary zapis Jeffa, zapis testowy z doby 362).
  - **Notable:** prefiks + finalizer (kontekst, wzor `RealPayer` - prefiks Low na `GiveGoldAction.ApplyInternal`) na `NotablePowerManagementBehavior.BalanceGoldAndPowerOfNotable(Hero)` (G :47-61:
    `GiveGoldAction` notabl -> null :52, null -> notabl :58) i `NotablesCampaignBehavior.OnHeroCreated(Hero, bool)` (G :41-47, +10 000 :47); kapital nowej karawany notabla - kontekst na
    `CaravanPartyComponent.CreateCaravanParty` (okno O17 "nowe-karawany", K:CirculationWindows.cs:1771) i postfiks `GetInitialTradeGold` jak E3/7 (K:CaravanCapital.cs:15-32). Kolejnosc: okna 169
    (prefiks First + finalizer) -> nasz kontekst (Low) -> `GiveGoldAction`. **v2 (K8):** na `CreateCaravanParty` sa juz `CaravanCapital` (prefiks + finalizer, K:CaravanCapital.cs:258)
    i okno O17, a E4/8 dodaje postfiks Low (ludzie karawany z ludzi miasta, PLAN-KODU-E48.md:259-266): zloto - CaravanCapital/A3, ludzie - E4/8; wynik jednych nie zalezy od drugich.
    **v2 (E5K1-05/K4):** kontekst czynny dopiero od `Campaign.GameStarted` (gra tworzy notabli przy nowej kampanii przez ten sam `OnHeroCreated` - G NotablesCampaignBehavior.cs:80-83,
    :385-418; latki sa czynne od menu glownego, K:SubModuleMain.cs:42) i nie dla karawan z `isInitialSpawn` (G CaravansCampaignBehavior.cs:342-354).
- **Pliki:** `MenPurse.cs` (NOWE `CampSpend`), `ArmouryBehavior.cs` (petla raz na dobe przed `ClanBudget.Daily` :1514; flaga w `SyncData`), NOWY `NotableGold.cs` (albo w `Seal31B.cs` - decyzja przy kodzie), `CaravanCapital.cs`,
  `MoneyLedger.Obieg.cs` (linia "Obieg: notable i karawany" L:31727 - pozycje "do kasy osady", "z kasy osady"), `Settings.cs`.
- **Klucze:** `SutlersEnabled` = true (`// soldiers in the field spend a tenth of their purses each day with the sutlers: most of it reaches the nearest friendly town, the rest the nearest village - you and the AI alike`),
  `SutlerDailyShare` = 0.10, `SutlerTownShare` = 0.85, `SutlerRampDays` = 28; `NotableGoldReal` = true (**v2:** `// a notable's gold above or below what the game keeps him at goes to or comes from the purse of the town he trades in - his own town, or his village's market town - instead of vanishing or appearing; a new notable is set up from that purse. The notables of a new world keep the game's starting gold`),
  `NotableCaravanCapitalReal` = true.
- **Test:** zbior A (nowa kampania - **v2:** takze zloto notabli w d1 = bieg bez A3 +-1%, pominieci w linii startowej): sakiewki <= 15 dni zoldu w d120 (oczekiwane 5-12 [S]); pasmo notabli z niczego 0, w nicosc 0
  (miasta i wsie osobno); nowi notable 0 z niczego (po starcie); kapital karawan notabli z niczego 0; **liczba karawan notabli d1-d40 i d120 >= 90% biegu bez A3** (E3:698 - ryzyko ciecia karawan; po scaleniu z E4/8);
  markietani bez odbiorcy (brak miasta/wsi) - kasa najblizszego miasta, licznik.
- **Rachunek [S]:** dzis sakiewkom przybywa ok. 100-190 tys./d (d40 7.5 mln -> d120 22.5 mln); przy 10%/d stan ustala sie na ok. 4-8 mln, a te 100-190 tys./d ida do miast (85%) i wsi (15%) -
  wiecej, niz A1 zabiera kasom miast (-89 tys.). Pasmo notabli: +133 tys. do kas, -94 tys. z kas - **v2:** w calosci kasy miast (wiejscy przez miasto targowe), podzial miasto/wies nieznany
  (log liczy pasmo lacznie, L:31727) - z linii "Notable (E5/0)"; kiesy wsi nie dostaja nic z pasma (do B1 nie ma nowego dochodu panow przez rente 20%).
- **Ryzyka:** (1) markietani nie moga liczyc drugi raz odziezy (150), napitkow i drewna (D11), dozbrajania (K1) - ta sama kolejnosc rezerw co `OnLeft`; (2) koszt petli ok. 700 partii raz na dobe; (3) notable w osadzie
  bez nadwyzki zostaja ponizej 4 500 (sila nie schodzi) - odczyt "notable ponizej pasma" w linii. **E4:** brak wspolnego stanu (RD S14); **v2:** wspolny cel latki `CreateCaravanParty` z E4/8 (K8, tabela 5.1).

### E5/A4 - Drobne kas (196, PLAN 5.4) - M - KOD TERAZ

- **Zakres:** R22 (oblezona osada nie placi nic - jeden warunek `IsUnderSiege` w zaworze miasta, rencie/oplatach wsi, 184, politykach); "10 miast bez placenia" - z definicji A1 (zawor od nadwyzki, bez filtra ludnosci);
  "prog podatkow zamku" - B-4 `PayFloor` juz w Crown Duty, War Tax i Spoils (K:KingdomTreasury.cs:483-484, :511; K:SpoilsSeal.cs:413, :476), A1 rozszerza na Road Tolls i miasta (D4). Zrodel w dokumentach
  dla "oblezonego miasta" i "progu zamku" nie znaleziono (RC pkt 10) - definicja z kodu (RB 5.4).
- **Pliki:** `TownPurse.cs`, `KingdomTreasury.cs` (`RulingIncomePostfix`), `PopulationLaw.cs`. **Klucz:** `BesiegedPayNothing` = true (`// a besieged town, castle or village pays no rents, dues or war taxes while the siege lasts`).
- **Test:** zbior A: wplaty z oblezonych osad 0; miasta z zywym panem i nadwyzka > 0 bez zaworu: 0; wplaty z kas ponizej zapasu: 0. **E4:** E4/6 schronienie w oblezeniu dotyczy jedzenia, nie zlota (RD S15).

### E5/A5 - BetterEconomy: prawdziwa wplata i dar dla notabli (197, PLAN rozdz. 2 G) - S - KOD TERAZ

- **Zakres:** R23 [D 09.10 04:25 G]. Dzis `BeeSeal` blokuje postfiksami `CanPlayer*` wplate gracza do skarbca miasta (BEE Town :463) i zamku (BEE Castle :1149) (K:BeeSeal.cs:15-40; tools\bee\OPIS.md rozdz. 1.2).
  Zmiana: obie akcje otwarte, ale prefiks na akcji BEE zamienia "wplate do wirtualnego skarbca" na przelew gracz -> kasa osady (`GiveGoldAction.ApplyForCharacterToSettlement`); premie BEE wyliczane
  z tej wplaty (dobrobyt, towar) - **tylko jesli nie powstaja z niczego** (sprawdzic w BEE przy kodzie: jesli dopisuja dobrobyt/towar z niczego, zostaje sam przelew i napis po angielsku, co daje).
  "Dar dla notabli": nowa opcja w menu osady BEE albo nasza (decyzja przy kodzie) - gracz -> notable wedlug sily, relacja wedlug zwyklego daru gry.
- **Pliki:** `BeeSeal.cs`. **Klucz:** `BeePlayerGiftsReal` = true. **Test:** zbior A + reczne sprawdzenie Jeffa (autotest nie klika menu): przelew w ksiedze, nic z niczego.
- **Ryzyko:** wersja BEE inna niz v1.4.5 - nic sie nie wpina (jak 170). **E4:** brak.

### E5/A6 - Lup wodza tylko z kiesy pokonanego (198, warunkowo) - S

- **Zakres:** R20 - tylko gdy E5/0 pokaze, ze na ladzie (bez statkow) z niczego powstaje > 5 tys./d. Postfiks First na `MapEvent.CalculatePlunderedAndLostGoldAmounts` (G MapEvent.cs:1497-1526) albo kontekst na
  `CommitGoldChanges` (G MapEventParty.cs:499-524): suma `PlunderedGold` zwyciezcow = suma tego, co naprawde zejdzie z pokonanych (wodz bez zlota - 0), wzor 3.2 `BeatenPurse` (K:BeatenPurse.cs:24-26, :196).
- **Klucz:** `BattlePlunderFromLoserOnly` = true. **Test:** "Bitwy (E5/0)" netto z niczego = 0 +- 1 tys./d. **E4:** brak.

---

### E5/B1 - Kiesa ludu (173, PLAN 5.5) - D - KOD PO SCALENIU E4/7b (v2; rownolegle z E4/8), STAWKI Z BIEGU A

- **Zakres:** kiesa czynna (**v2:** `TownFolkLive && TownFolkMode >= 1`): R1 (placi lud; kotwica), R11 (kiesy W/R/P, gmina, lokata), R12 (podzamcze), R13 (wies 35/65, oplaty stale, zakupy wsi), R14 bez jedzenia, R5 (zapasy startowe z daru),
  haki: `CastleDuesSuburbShare` 0.15, `VillageRentYieldsToFees` true (K:VillageTakings.cs:59), `CourtFolkShare` (A2), 75/25 w `MenPurse.OnLeft` / `CampSpend` / `SoldierPay.Route`, 5% zaworu dla P w `TownPurse`
  - **v2 (K3):** dzis `static readonly` (K:CastlePurse.cs:92, K:VillageTakings.cs:59) - zamienione na wlasciwosci liczone z warunku kiesy czynnej (wylaczenie B1 = stan sprzed paczki w tej samej dobie).
  Model i stawki - KL rozdz. 1 (L1-L9, tabele 1.3-1.5) z poprawkami tego projektu: D1, pan 35%, bez -5% wojska (184), zapisy przez `SaveText.Sync` (ok. 35 KB - kawalki, paczka 161), wszystko w ksiedze.
- **Pliki:** `TownFolk.cs` (z E5/0, tryb 2: stan w zapisie `arm_townfolk`), 10 plikow z E5/0 (`Note*` -> `Earn`/`NoteSale`), `PopulationLaw.cs` (galaz wsi: oplaty z `TownFolk.Villages`), `VillageTakings.cs` (112 liczy
  druga polowe od nowej stawki), `CastlePurse.cs`, `TownPurse.cs` (**v2:** lokata nie lezy w kasie - zawor i `Spare` bez zmian), `MoneyLedger*.cs` (nowi posiadacze "kiesy ludu (miasta / podzamcza)", "gminy", "lokaty ludu"
  (poza `SettlementComponent.Gold`, E5K1-12) w `ReadHolders` K:MoneyLedger.cs:670-706 i `WorldTotal` :717 - jedna suma swiata), `ArmouryBehavior.cs` (kolejnosc: `MoneyLedger.BlockOpen` -> `TownPurse.BeforeRents` -> `PopulationLaw.Daily` -> `TownPurse.Daily` ->
  `CastlePurse.Daily` -> `TownFolk.Daily` -> `BuildFunding.Daily`), `SubModuleMain.cs`, `Settings.cs`.
- **Okna latek (dowod):**
  - kotwica - **v2 (K1/E5K1-09), raz na (miasto, kategorie, dobe):** (1) postfiks na prywatnej `ItemConsumptionBehavior.UpdateDemandShift(Town, Dictionary<ItemCategory,float>)` (G :178; wolana tuz przed
    `MakeConsumption` :66, takze dla zamkow BK przez `MakeConsumptionInTown` refleksja - BK BKSettlementBehavior.cs:631; BK jej nie lata) mnozy slownik budzetu kategorii przez m x Afford grupy - czlon popytu;
    (2) postfiks na BK `EconomyPatches.CalculateBudget` (Priority po `HistoricalPrices.BudgetPostfix`, K:HistoricalPrices.cs:832) mnozy tylko czlon dobrobytu / 1000 / indeks (liczony od nowa przy kazdej
    pozycji) i tylko w nawiasie konsumpcji 110 - wywolania `RawPrice.HouseBudget` (K:RawPrice.cs:231-234) dostaja ten sam budzet przez jawne `TownFolk.Anchor(town, kat)`, nie przez przypadek; (3) m liczone wobec
    przewidywanego wydatku (`ConsumedValue` kategorii z wczoraj), nie budzetu (wydatek ogranicza polka: d120 482 241 wobec 1 005 941, L:31689); oba czlony skalowane tym samym m (KL S12); osady bez
    PopulationData - postfiks na kazdej deklaracji `CalculateDailySettlementBudgetForItemCategory` (G :81-84; d120: 0 takich osad); czynne po `Campaign.GameStarted`;
  - zaplata: w `CastlePurse.ConsumePostfix` (First, K:CastlePurse.cs:251) -> `TownPurse.OnConsumed` -> `TownFolk.PayForGoods` zamiast cofniecia; kupowanie jedzenia przez BK przy zapasie < 10% (BK EconomyPatches.cs:532-536)
    placi lud, potem gmina (spichlerz miejski), reszta - "zjedzone bez zaplaty";
  - zadowolenie R14 - **v2 (K2):** migawka `EconomicData.Satisfactions` (publiczna tablica, BK EconomicData.cs:62) w `CastlePurse.ShelfPostfix` (przed konsumpcja), zapis "migawka + min(0, delta BK) - 0.0015"
    w `ConsumePostfix` dla grup z Afford < 0.98 (R14); bez latki na `UpdateSatisfaction` (BK EconomicData.cs:148 - krotka, JIT moze ja wbudowac w prefiks BK);
  - wies: postfiks na BK `BKTaxModel.CalculateVillageTaxFromIncome(Village, int)` (BK BKTaxModel.cs:271-284) x `VillageLordScale` 0.5 - **po pelnej sygnaturze** (druga metoda `(Village, bool, bool)` :212;
    AmbiguousMatch z audytu 04.10, K:PopulationLaw.cs:985-988); wolajacy: BK `VillagerSettlementEnterPatch` (BK EconomyPatches.cs:904-935): wies dostaje polowe reszty (:918), majatki BK z czesci pana (:925).
- **Klucze (grupa "The townsfolk's purse"; opisy po angielsku):** **v2 (K3): NOWY `TownFolkLive` = true** (`// the townsfolk's purse is live: townsfolk, castle folk and villages earn, pay their taxes and dues and buy their goods with their own coin; the war tax on the people and the lords' share of the town dues follow it. Off = as before stage 5`) - nie zmiana domyslnej `TownFolkMode`; stawki KL 5.2 (`TownFolkServiceShare` 0.25, `TownFolkCourtServiceShare` 0.20,
  `TownFolkLabourFood/Goods/Arms` 0.20/0.10/0.04, `TownFolkCraftWageShare` 0.35, `TownFolkMerchantDraw` 0.03, `TownFolkMerchantProfitShare` 0.05, `TownFolkCastleProfitShare` 0.15, `TownFolkExcise` 0.02,
  `TownFolkReserveDays` 10, `TownFolkAnchorBasket` true, `TownFolkSlaveRealms`), `VillageLordScale` = 0.5 (`// the lord takes this share of what BK would give him from a village's market takings - 35% under the usual tax policy, 25% low, 45% high; the village keeps the rest`),
  `VillagePurchasePerHead` 0.07, `VillageDuesPerHeadYear` 4.0, `TownFolkSatisfactionFromBasket` = true. **Liczby stawek - po E5/0 i powtorzonym rachunku KL** (projekt wybiera; Jeffa nie pytamy).
- **Linie logu:** "Kiesa ludu:" (format E5/0, bez "na sucho"), "Podstawa robocizny: z czego", "Prawa panskie" (oplaty stale wsi i 3% podzamcza), "Gotowka wsi" (z E5/0), "Zadowolenie ludu (R14): korekty a, BK sam b".
  **v2 (K3):** linia startowa podaje `TownFolkLive`, `TownFolkMode`, `TownRentLordShare`, wagi rent i 3 klucze statkow z wartosciami czynnymi; `sprawdz_logi --grupa etap5` zglasza blad, gdy bieg testu B
  idzie bez czynnej kiesy albo z 0.67 / wylaczonymi statkami.
- **Test:** zbior B (rozdz. 4) - progi "stac", "zjedzone bez zaplaty", "plon", "pan w utargu i w gotowce wsi", "panowie zamkow", "wojsko".
- **Ryzyka:** KL rozdz. 6 (szczelnosc - obieg z kiesa jest czulszy na dziury: 10 tys./d dziury = -2.8 tys. wojska w 4 lata [R]; towar - popyt przesuwa sie z broni na jedzenie, sol, sukno, zelazo; druga konsumpcja BEE -
  profile klas BEE: sprawdzic przy kodzie, czy po 170 jest martwa; zadowolenie BK odwrocone - R14); S4 (bieda = mniej jedzenia z targu do zapasu warowni - rozdz. 5.2 pkt 5); D1 (osady bez danych BK).
- **NK:** zapasy startowe kies z daru (R5) - nowa kampania; stary zapis: kiesy z kasy kupcow ponad zapas, najwyzej polowa nadwyzki (KL 5.3).
- **E4:** **v2 (K9): kod po scaleniu E4/7b do `w-toku/zbior-e4-koniec`** (D1 czyta tylko `PopulationLaw.PeopleOf` - jest w zbiorze 31, K:PopulationLaw.cs:214-222 - i wolne rece E4/7, FreeHands.cs jest
  w `SCR\e4\koniec` 392c519); E4/8 zmienia tylko liczby stanu miast w `PopulationLaw.Daily` przy tym samym API - rownolegle, scalenie `PopulationLaw.Daily` raz (RD S1, S2).
  Stawki z linii E5/0 biegu A (K7); test zbioru B po tescie A.

### E5/B2 - Podatek wojenny korony (184, PLAN 5.6) - S

- **Zakres (v2, E5K1-01/02):** R15 (b). Stawka s krolestwa w `KingdomTreasury` (co 7 dob, zapis w kluczu 165 - szoste pole, `CrownIncome.Import` pomija w starszej wersji), pobor w `TownFolk.Earn` (miasto,
  podzamcze). **Danina (`Levies` :392-404) i War Tax 5% (`RulingIncomePostfix` :502-516) zostaja jak dzis** (danina miast od A1 od `Spare` D4, danina wsi od B1 od nadwyzki ponad 15 dni zakupow); polityka War Tax
  dalej x2 daniny i podnosi pulap s z 3% do 6%. Potrzeba korony: kredyt 186 w ostatnich 7 dobach albo nalezny zwrot > wplywy dnia bez 1/360 zapasu, albo skarbiec < zapas wojny (max(500 000, 60 dob
  naleznego zwrotu)) - liczby z linii "Korona: wplywy dnia (165)" i "Kredyt korony (186)". AI krola nie decyduje osobno - regula potrzeby jest decyzja korony (gracz-krol tak samo).
  **Przed testem B (nie przed kodem):** rachunek `sim_kiesa2` na liczbach E5/0 z biegu A z: danina, 184 wedlug potrzeby (pulap 3/6%), B3, B5 i A6 - z niego progi B3 i wojska; wojsko dla A6 osobnym wierszem
  (statki i bitwy zabieraja lordom w wojnie ok. 0.45-0.5 mln/d z niczego - L:31718 statki 370 870, bitwy 85 762 - a 184 to jedyne nowe wyrownanie, E3:529-536).
- **Pliki:** `KingdomTreasury.cs`, `TownFolk.cs`, `CrownIncome.cs` (linia "Korona: wplywy dnia (165)" - nowa pozycja "podatek wojenny ludu (184)" obok "danina wojenna"), `Settings.cs`.
- **Klucze:** `WarTaxFromFolk` = true (**v2:** `// in war the crown also taxes the townsfolk's and castle folk's earnings, on top of the war subsidy from the merchants' and villages' spare coin - up to 3%, up to 6% under the War Tax policy, raised each week while the crown has to borrow or spend its old hoard to repay the war wages, lowered when it no longer has to; the higher it is, the less the people buy and the slower prosperity grows`),
  `WarTaxMax` = 0.03, `WarTaxMaxWithPolicy` = 0.06, `WarTaxStep` = 0.005, `WarTaxReserveDays` = 60; czynny tylko z kiesa czynna (K3); `LaySubsidyEnabled` bez zmian.
- **Linie:** "Korona: ..." + `podatek wojenny ludu (184): s krolestw (srednia, max), pobrano P (ludzie miast, podzamcza) | potrzeba: kredyt 186 a krolestw, zwrot ponad wplywy b, skarbiec < zapas wojny c`.
- **Test (v2):** zbior B: s rosnie w krolestwach, ktore pozyczaja (kredyt 186 > 0 w tygodniu -> s tydzien pozniej wyzsze, az do pulapu); dlug 186 w drugim polroczu biegu rocznego nie rosnie (d364 <= d182 na krolestwo
  w wojnie); brak zaleglosci 186 u krolestw z s < pulapu; wplywy dnia korony w wojnie po B >= w biegu A (danina + 1/2 zaworu + 184 wobec danina + 1/3 zaworu); zwrot zoldu >= 70% naleznego w wojnie;
  "stac pelny" przy s = 6% >= 95% swiat. Wielkosc 184 (30-60 tys./d [S]) - odczyt, nie prog (wynika z potrzeby).
- **Ryzyka:** korona w wojnie bierze wiecej niz dzis (danina + 184) - zamierzone (Jeff C: podatek zamiast -5% wojska; E3 (b): statki razem z podatkiem); scalenie daniny z 184 dopiero po wyniku testu B (osobna decyzja projektu);
  podatek obniza dobrobyt, a przez W - przyrost ludzi etapu 4 (RD S7) - mierzyc w biegu rocznym;
  AI BK sam zmienia polityke podatkowa osady wedlug lojalnosci (BK BKSettlementBehavior.cs:400-415) - to stawka pana (R13), nie 184 (bez dublowania). **E4:** niezalezne w kodzie; test osobno (diagnoza).

### E5/B3 - Wyrownanie dla panow zamkow (Z15-5, PLAN 5.7) - M

- **Zakres:** R4 (`TownRentLordShare` 0.67 -> 0.5) i R16 (`CrownRentWeightTown` 3 -> 2, `CrownRentWeightCastle` 1 -> 1.5, wies 0.25; K:Settings.cs:885-887, K:CrownRents.cs:186-193), takze gracz [D 09.10 E].
- **Rachunek [R, A15 3.5]:** panowie zamkow W -1% / P -4% (bez wyrownania -14% / -32%), ich udzial w D 33% / 36%, wojsko swiata W -2%; placa panowie miast (D -9% / -15%) i krolowie (-7% / -10%);
  gracz z monety wlasnego zoldu odzyskuje 0.40-0.49 (Z8 < 1).
- **Klucze:** tylko zmiana domyslnych (opisy bez zmian); **v2 (K3):** czynne tylko z kiesa czynna (bez kiesy - 2/3 i 3/1/0.25 jak przed B, bo wyrownanie kompensuje skutek kiesy). **Pulapka MCM (CLAUDE.md 7):**
  przy wgraniu B `grep '"TownRentLordShare"\|"CrownRentWeight'` w `Armoury.json` Jeffa i autotestu - po A1 klucz `TownRentLordShare` moze sie tam zapisac (gdy ktos zapisze MCM); jest - poprawic w pliku
  (gra zamknieta, kopia .bak); linia startowa podaje wartosci czynne.
- **Test:** zbior B - progi panow zamkow (rozdz. 4: **v2** udzial w D swiata glowny, mediana kiesy - srednia 4 blokow, odczyt). **E4:** brak.

### E5/B4 - Targ przy zamku (199, PLAN 5.11) - M (v2) - KOD PO E4/11

- **Zakres:** R17 [D 09.10 F, Q2]. **v2 (E5K1-07, K5):** pierwszy przystanek: `MarketRoad.RoutePrefix` (K:MarketRoad.cs:52, prefiks First na `VillagerCampaignBehavior.SendVillagerPartyToTradeBoundTown`) kieruje woz
  wsi zamkowej do jej zamku, gdy **jedzenia na polce targu zamku < 10 dni zuzycia podzamcza** (zuzycie = jedzenie zjedzone w konsumpcji ludnosci BK zamku, `SoldItems` kategorii jedzenia, srednia 7 dob),
  woz wiezie jedzenie i nie ma znacznika "byl w zamku" (gra po sprzedazy kieruje woz z > 1 pozycja znow do miasta - G VillagerCampaignBehavior.cs:286-296; bez znacznika petla zamek-zamek).
  Spichlerz zalogi (`FoodStocks`) NIE jest warunkiem (mediana 302 dni, ujemny bilans 4 z 130 - warunek bylby martwy) - zasila go danina wsi z E4/11 pkt 8 (od ludzi okregu).
  Sprzedaz: BK `VillagerSettlementEnterPatch` wola dla zamku vanilla `SellGoodsForTradeAction.ApplyByVillagerTrade` (BK EconomyPatches.cs:911-912; BK `SellGoodsPatch` tylko dla `IsTown`, :817-870);
  vanilla kladzie towar na polke i placi z calej `town.Gold` (G SellGoodsForTradeAction.cs:43-58) - dlatego **wlasna sprzedaz**: prefiks na `ApplyInternal` dla zamku (zwraca false) sprzedaje tylko brakujace
  jedzenie do 10 dni zuzycia, po cenie targu zamku, ze `Spendable`; reszta ladunku zostaje na wozie. Drugi odcinek: woz ze znacznikiem przez zwykle `SendVillagerPartyToTradeBoundTown` -> `MarketRoad`/`MarketCarts`
  jak dzis (`CastlePurse.CanPayCart` K:CastlePurse.cs:419). Cena = cena targu zamku (jedna regula; "cena wsi" z A15 Z15-6 odrzucona - druga cena dla jednego towaru).
  **Jedno zjawisko z E4/11 pkt 8** (PLAN-KODU-E411.md:64-68 - postfiks na `CalculateTownFoodStocksChange`, danina jedzenia wsi od ludzi okregu, klucz `TownFoodByVillagePeople`): `CastleMarket.FeedFromDistrict`
  projektowana razem z planem kodu E4/11 - danina w naturze (spichlerz zalogi) + woz (polka podzamcza), jedna liczba jedzenia okregu w linii.
- **Pliki:** `MarketRoad.cs`, `MarketCarts.cs`, `CastlePurse.cs`, NOWY `CastleMarket.cs`, `Settings.cs`. **Klucze:** `CastleMarketFirst` = true (`// a castle's own villages sell it food for its people first - enough for ten days - and carry the rest of their load on to the town`), `CastleMarketFoodDays` = 10.
- **Linie:** `Targ przy zamku (5.11): dzien N | wizyt a, jedzenie sprzedane zamkom b szt. (c zl), dalej do miasta d wozow, odmowy (brak Spendable) e | zamki z polka < 10 dni zuzycia podzamcza: f z 130 (mediana g dni) | powtorne wizyty bez znacznika: 0`.
- **Test (v2):** zbior B: zamki z polka jedzenia < 10 dni zuzycia podzamcza - mniej niz w linii E5/0 biegu A (ten sam pomiar); sprzedaz do 10 dni > 0; powtorne wizyty 0; zatkane magazyny wsi zamkowych
  **osobno dla biegu letniego (<= 5.6%) i zimowego (<= wynik zimowy biegu A, dzis 6.5%)**; zakup plonu >= 98%; rozbite tabory nie wiecej niz +10%; Z8 gracza (wlasny zamek, wlasne wsie: z 1 zl
  zaplaconego przez kase zamku wraca do pana mniej niz 1 - 35% utargu + 2/3 x 7% nadwyzki).
- **Ryzyka:** to samo zjawisko co odlozony "spichlerz miasta zywiony przez okregi" (GL 2.13 krok B) - jedna funkcja `CastleMarket.FeedFromDistrict`, wlaczona tylko dla zamkow (D3); ten sam zapas warowni czytaja
  E4/6 (`ShelterPostfix` K46 Famine.cs:386) i E4/8 (zboze nowej druzyny z `FoodStocks` siedziby) - nie liczyc jedzenia dwa razy; okno powrotu taboru 112, `CartTownExit` (130).
  **E4:** **po E4/6 (jest w galezi koncowej), E4/8 i v2: po kodzie E4/11** (pkt 8 zmienia `FoodChange` zamkow i jest tym samym zjawiskiem; RD pisano przed planem E4/11 - RD:40, :153).

### E5/B5 - Statki do portow (181 ON, PLAN 3.3) - M

- **Zakres:** domyslne `ShipsToPort`, `ShipTradeThroughPort`, `ShipPrizesToPort` = true (K:Settings.cs:1259, :1261, :1262; 9 latek K:ShipsToPort181.cs:920-938 - w grze od WGRANIA 28); porty placa z `TownPurse.Spare` (A1).
  Rabat AI 0.01 przy kupnie statku (181d) zostaje do pomiaru (rozdz. 7).
- **Rachunek [P/S]:** zloto swiata ok. -300..-430 tys./d (statki NavalDLC 429 987 + sprzedaz osadom 18 298 sr28, L:31718); bez wyrownania wojsko po roku -9..-11% (E3:539-540) - dlatego razem z B2 i B3.
- **Test:** zbior B: "Statki (181)" - wyplaty portow, kasy portow ponizej zapasu nie dluzej niz 28 dob; zloto swiata wobec biegu bez B5. **Pulapka MCM:** grep 3 kluczy w `Armoury.json` (Jeffa i autotestu) przy wgraniu;
  **v2:** linia startowa podaje wartosci czynne, `--grupa etap5` zglasza bieg B ze statkami OFF. Wojsko po B5 - z rachunku przed testem B (E5/B2). **E4:** brak (RD S8).

### E5/B6 - Pieniadze weterana i najmu (201) - S - KOD PO E4/10

- **Zakres:** R19 (RD D5). Sakiewka zwolnionego (dzis zawsze do kiesy wsi, K:ClanBudget.cs:749-759) idzie tam, gdzie czlowiek: weteran w karczmie (E4/10) - jego czesc sakiewki zostaje w puli WETERANI miasta
  i jest wydawana jak "zycie" (R9: 10%/d, kasa miasta, od B1 75/25); zaplata za najem (`LevyGold`, K:LevyGold.cs:118-160) - do sakiewki najetego, nie do kasy miasta (OB 6.2 "do kasy miasta karczmy" odrzucone:
  najmuje sie prawdziwego czlowieka).
- **Pliki:** `ClanBudget.cs`, `LevyGold.cs`, `MenPurse.cs`, `PeopleBook` (tylko odczyt puli). **Klucz:** `VeteranPurseFollowsMan` = true.
- **Test:** zbior B: sakiewki zwolnionych do karczmy = 1/3 od tieru 3 (E4/10), najem do kas miast 0 (dzis ok. 30-44 tys./d). **E4:** po kodzie E4/10 (pula WETERANI musi istniec).

---

### E5/C1 - Kasy miast bez dosypki (202, PLAN 5.8) - M - PO ROKU TESTU B

- **Zakres:** regulator bez dosypki (miasta i zamki - jeden przelacznik; **v2 (K3): nowy klucz bool `TownPurseNoTopUp`, nie zmiana domyslnej `TownPurseRegulator` 1 -> 2** - pulapka MCM), gdy w roku testu B
  reszta ksiegi < 10 tys./d przez 28 dob (z nowymi posiadaczami - KL:346-347) **i v2 (E5K1-03): licznik "dosypka finansujaca zakupy ponizej zapasu" (sciezki gry/BK) < 2 tys./d** [S: ok. 0.7% zakupu plonu
  283 tys./d - miesci sie w progu "plon >= 98%"] (gdy wiekszy - C1 odebralby te zakupy; najpierw liczba miast i towar). Bezpiecznik korony (OB :255, :363) - nie.
- **Test:** zbior C: zakup plonu >= 98% takze w miastach-zaulkach; miasta z kasa < 1 000: 0; zloto swiata < 0.1% na 28 dob. **Ryzyko:** zaulek kupuje mniej (P111 decyzja "dosypka zostaje") - gdy prog plonu nie przejdzie,
  zapasowo: platnik dla zaulka (gmina albo korona z wplywow dnia, najwyzej 2% zapasu dziennie) - wtedy pytanie do Jeffa.

### E5/C2 - Glod ma skutki (203, PLAN 5.9) - S - PO ROKU TESTU B

- **Zakres:** galaz jedzenia R14 [D 09.10 A]: Afford_jedzenie < 0.95 przez 7 dob i kampania >= 120 dob -> -0.0015 zadowolenia jedzenia (tylko gdy jedzenie jest na polce). **D2 - jedna definicja glodu:** funkcja
  `Hunger.Of(osada)` zwraca przyczyne: **brak jedzenia** (E4/6: spichlerz okregu, Food 0 warowni - K46 Famine.cs:234 `HungerOf`) albo **brak pieniedzy** (Afford); skutki: zgony i uchodzcy - tylko E4/6; zadowolenie -
  tylko brak pieniedzy (brak jedzenia karze juz BK "Food Shortage" przy `IsStarving`, BK BKProsperityModel.cs:68-81, i RBL "Severe Starvation" -20 dobrobytu, RBL RealisticProsperityModel.cs:55 - obu nie dublujemy).
- **Klucz:** `TownFolkHungerHurts` = true (`// when the townsfolk cannot afford their bread for a week, their contentment falls - not in the first 120 days of a campaign`).
- **Test:** zbior C: zadna osada nie dostaje obu kar tej samej doby; w biegu bez oblezen i drozyzny korekty jedzenia ~0 (KL: jedzenia stac 100% dob [R]); przyrost etapu 4 - H (E4/6) i W (dobrobyt) w linii osobno.
- **E4:** po E4/6 (jest w galezi koncowej). Skutki glodu RBL/BEE przy naszym lancuchu jedzenia - sprawdzic w dekompilacji przy kodzie (RD rozdz. 6).

### E5/C3 - Dziesiecina (204, PLAN 5.10) - S - PO ROKU TESTU B

- **Zakres:** R21 [D 09.10 B]; od tej chwili wies 60% (pan 35% + sept 5% = 40%, w widelkach 30-50%). **Pliki:** `TownFolk.cs`, `VillageTakings.cs`. **Klucz:** `TempleTithe` = true, `TitheVillageShare` 0.05.
- **Test:** zbior C: **v2:** pan (z majatkami) + sept w utargu = 30 / 40 / 50% wedlug polityki BK (+-2 pp), w gotowce wsi G (R13) <= 50% kazda polityka, zwykla >= 30%; jalmuzna i budowa septow w ksiedze; wojsko >= 95 tys. (KL: -0.4 tys. [R]). **Ryzyko:** kaplan BK bez zywego duchownego - odbiorca zapasowy (rozdz. 2).

### E5/C4 - Napis "czy ludzi stac na chleb" (205, PLAN 5.13) - M - PO ROKU TESTU B

- **Zakres:** jedno zdanie w oknie "People of this land" z E4/7 (`ShowInquiry`, bez `SwitchToMenu` - K47 PeopleMenu.cs:10-26; CLAUDE.md 7), nie nowa pozycja menu (RD S11). Tekst po angielsku, np.
  `Bread: 97 in 100 townsfolk can afford their daily bread, 88 in 100 their full basket.` (zamek: "castle folk", wies: "villagers" - zakupy wsi). Zapasowo postfiks na `PlayerTownVisitCampaignBehavior.SetIntroductionText` (G :672) - nie uzywamy.
- **Klucz:** `PeopleMenuShowsBread` = true. **Test:** reczne sprawdzenie Jeffa (autotest nie otwiera menu); w logu liczba = liczba z "Kiesa ludu:". **E4:** po E4/7.

---

## 4. Test i progi etapu ("Skonczone, gdy" z PLAN:168 z poprawkami RC)

Kazdy zbior: autotest 40 dob + zapis d362 9 dob (0 bledow naszych modow) i bieg z progami paczek **oraz progami etapow 2-4** (glowy rodow < 5 000 najwyzej 10; 0 bankructw; wojsko lordow w wojnie
95-115 tys., podloga 85; reszta ksiegi pieniadza < 10 tys./d; etap 4: ludnosc netto, zapas warowni F6/F7 w biegu zimowym). Prog nie przechodzi - wylaczamy kawalek w `Armoury.json` autotestu
i powtarzamy (jedna zmiana naraz); **wojsko ponizej 95 tys. naprawiamy przeplywem (pulap 184, renty), nie wojskiem** - a gdyby i to nie wystarczylo, pytanie do Jeffa.

| Prog | Jak mierzony | Od paczki | Dzis |
|---|---|---|---|
| kazdego dnia w kazdym miescie >= 95% ludzi stac na jedzenie | "Kiesa ludu:" miast ze "stac jedzenie" < 95%: 0 kazdej doby | B1 | nie do policzenia |
| pelny koszyk >= 98% (swiat), miast < 95% najwyzej 5% | "Kiesa ludu:" (KL 5.4) | B1 | - |
| zjedzone bez zaplaty < 1% konsumpcji (miasta i zamki) | "Kiesa ludu:" + "Zawor zamkow (110)" | B1 | miasta 100% (421 tys. z niczego), zamki 50 tys./d |
| miasta kupuja >= 98% plonu | "Przeplywy osad": towar niesprzedany po wizycie / dowoz | kazdy zbior | ponizej 0.1% niesprzedane |
| **v2 (E5K1-06):** pan (z majatkami BK) w utargu = stawka polityki 25 / 35 / 45% (+-2 pp); w gotowce wsi G (R13, z markietanami) pan + oplaty: zwykla 28-45%, kazda polityka <= 50% (niska ok. 21-25% - wynika z decyzji 15: A15 Z15-0 "przy niskiej polityce ok. 28-31%" z septem) | "Gotowka wsi" wedlug polityki BK | B1 (sept C3: +5 pp utargu) | 86% |
| **v2 (E5K1-10):** udzial panow zamkow w D swiata >= 30% (srednia d252-364) - **prog glowny** | narzedzie z `budzet-rodow.csv` (rodzaj=zamek) | B3 | 30.1% |
| udzial panow zamkow w D stalym 30-35% | jw. | B3 | 24.3% |
| (odczyt, v2) mediana kiesy panow samych zamkow: srednia 4 blokow 28 dob d252-364, tylko rody, ktorych krolestwo bylo w wojnie >= 21 z 28 dob; >= -2% - przy niezdanym, a udziale w D zdanym, B3 NIE jest wylaczane (rozrzut blokow dzis 16 pkt) | jw. + `wojna` w CSV | B3 | -2.5% (d93-120) |
| zatkane magazyny wsi zamkowych - **v2: osobno** bieg letni <= 5.6%, bieg zimowy <= wynik zimowy biegu A | "Dowoz (skutki)" sr28 | B4 | 4.9% (A), 6.5% (zima B) |
| **v2:** zamki z polka jedzenia < 10 dni zuzycia podzamcza - mniej niz w biegu A | "Targ przy zamku" / "Jedzenie z targu (E5/0)" | B4 | z E5/0 |
| **v2 (E5K1-03):** nasze zakupy kas miast z pasa ponizej zapasu kupcow = 0; dosypka finansujaca zakupy gry/BK ponizej zapasu - odczyt (prog C1) | "Nadwyzka kas (E5/0)" | A1 | z E5/0 |
| wojsko lordow w wojnie 95-115 tys. (podloga 85) | "Ludzie:" partie rodow, d300-364 biegu rocznego | kazdy zbior | 110 407 (d120) |
| sakiewki zolnierzy <= 15 dni zoldu | "Obieg": stan / zold naliczony | A3 | 29 dni |
| zloto swiata zmienia sie < 0.1% na 28 dob | "Pieniadz swiata": srednia 28 dob, |zmiana| <= ok. 8.5 tys./d | C1 (po B5, A6) | +662 tys./d |
| (projekt) reszta ksiegi < 10 tys./d z nowymi posiadaczami | `--grupa etap3` + kiesy ludu w sumie swiata | kazdy zbior | -1.3 tys. |
| (projekt) podatek wojenny <= 3% / 6% (War Tax), zwrot korony >= 70% naleznego; **v2:** s rosnie, gdy korona pozycza (186); dlug 186 d364 <= d182; wplywy korony w wojnie po B >= bieg A | "Korona: ..." + "Kredyt korony (186)" | B2 | danina z kas kupcow |
| (projekt) przyrost ludnosci w granicach etapu 4 (pokoj 0.2-0.8%/rok, wojna do -0.5%) | "Ludnosc:" netto, H i W osobno | B2, C2 | +0.5%/rok |
| (projekt) zapas warowni: zadna warownia AI nie glodzi przed 30. doba zimy, mediana >= 364 | bieg zimowy (P4 U27) | B1, B4 | 0 glodnych; 302 dni (z31) |

**Zbiory:** A - po tescie koncowym E4: 40 dob + zapis 9 dob + 120 dob (**v2:** ok. 50-85 min przy 17.4-30.6 s/dobe; z biegu A - stawki B i rachunek przed testem B). B - nowa kampania 40 dob + 364 doby + bieg zimowy 120 dob + zapis 9 dob (PLAN: "nowa kampania 40 dob i dwa testy roczne" - pierwszy).
C - drugi bieg 364 doby (z B i C) = koniec etapu. Wynik do raportu dla Jeffa przed jego nowa kampania (PLAN rozdz. 4: zalecana po etapie 7).
**Oczekiwany przebieg zlota swiata [S]:** dzis +662 tys./d -> po A ok. +560 (zostaja statki, bitwy, dosypka) -> po B ok. +50..+150 (dosypka, bitwy bez A6) -> po C1 (i A6) ok. 0.

---

## 5. Ryzyka / kontrola calosci (zasada 0)

### 5.1 Wspolne cele latek i stan (plik:linia)
| Cel / stan | Kto dzis | Kto w etapie 5 | Co pilnowac |
|---|---|---|---|
| `ItemConsumptionBehavior.MakeConsumption` | BK prefiks bool (BK EconomyPatches.cs:505); `CastlePurse.ConsumePostfix` First (K:CastlePurse.cs:251); `MoneyLedger.ConsumePostfix` | A1 (miasta przez CastlePurse), B1 (PayForGoods, R14) | tylko postfiksy; jedna klasa wpiecia; ksiega widzi wynik po nas |
| `DeleteOverproducedItems` | BK prefiks false (:606-609); `CastlePurse.ShelfPostfix` (:236), `MoneyLedger.ShelfPostfix` | A1 (migawka miast) | migawka przed konsumpcja wspolna |
| `GetTownGoldChange` (kazdy model) | `CastlePurse.RegulatorPostfix` First (:272), `SoldierPay.RegulatorPostfix` First, leniwie (K:SoldierPay.cs:694); `MoneyLedger` | A1 (tryb 1), C1 (tryb 2) | tarcza SoldierPay bez znacznikow przy A1 - dwa First nie zmieniaja wyniku tego samego miasta dwa razy |
| BK `CalculateBudget` + vanilla `CalculateDailySettlementBudgetForItemCategory` | **v2 (bylo "brak" - blad):** `HistoricalPrices.BudgetPostfix` (K:HistoricalPrices.cs:832, wpiety :1004-1005: bron 0 - 174.0, `TownUse` surowcow, czlon dobrobytu / przelicznik); `RawPrice.HouseBudget` wola metode refleksja (K:RawPrice.cs:231-234) | E5/0 (odczyt czlonu dobrobytu w nawiasie konsumpcji), B1 (czlon dobrobytu x m) | wolana przy kazdej pozycji polki - nic, co mnozy caly wynik (m^k); kolejnosc: HistoricalPrices -> B1 -> odczyt E5/0 (Last); wywolania RawPrice poza pomiarem |
| **v2:** `ItemConsumptionBehavior.UpdateDemandShift` (private; G :178, wolana :66 i przy tworzeniu swiata :38) | brak (BK nie lata) | E5/0 (odczyt slownika), B1 (czlon popytu x m) | raz na (miasto, dobe); po `Campaign.GameStarted` |
| **v2:** BK `EconomicData.Satisfactions` (publiczna tablica) | BK +0.001 / -0.0015 w prefiksie konsumpcji | B1 (R14 migawka w nawiasie 110), C2 (galaz jedzenia) | jeden zapis po BK na grupe i dobe |
| BK `HandleMarketGold` | okno O18 (K:CirculationWindows.cs:1789) | A1 prefiks bool Last | okno mierzy przed prefiksem |
| BK `CalculateVillageTaxFromIncome(Village,int)` | (BK sam, 0.7) | B1 x0.5 | 112 liczy druga polowe od nowej stawki; majatki BK z czesci pana |
| `PopulationLaw.Daily` (renty, stan miast) | renty miast i wsi (K:PopulationLaw.cs:1144-1212); E4/4 `TownsDaily` (:1148) | A1 (miasta -> TownPurse), B1 (wsie -> oplaty); E4/6, E4/7, E4/8 (inne metody) | scalic recznie; `RentToday` czyszczony raz (:1146) |
| `KingdomTreasury.Levies` / `RulingIncomePostfix` / K3 w `WageRefund` | danina, clo, polityki (K:KingdomTreasury.cs:362-447, :462-530); K3 (:246-253) | A1 (TaxFloor = D4; K3 = udzial pana w zaworze), A4 (oblezone), B1 (danina wsi od nadwyzki 15 dni), B2 (184 obok daniny - v2) | clo bez zmian (licznik cel); danina i War Tax bez zmian w B2 |
| `MenPurse.OnLeft` / nowe `CampSpend` | zycie, odziez, K1, E3/4 karawany (K:MenPurse.cs:258-310, :267) | A3, B1 (75/25), B6 | jedna kolejnosc rezerw |
| `SoldierPay.Route` | zold zalog do kas (K:SoldierPay.cs:425-473) | A1 (tarcze bez znacznikow; **v2: bez `HomePart`**), B1 (75/25) | zwrot korony liczony raz (przy 165 zalogi nie sa podstawa zwrotu) |
| **v2:** `CastlePurse.Spendable` / nowe `TownPurse.Spendable` | B-2 - zamki (MenPurse :525, MenUpgrade :470, ArmyClothing :233, SupplyDemand :562, BkSeal34 :192/:210) | A1 (te same miejsca dla miast) | jedno dno zakupow dla miast i zamkow |
| `ClanBudget.Court` / zwolnienia | 162m (:598-624), 166 (:739-822) | A2, B1, B6; E4/7-E4/10 | rozne metody, jeden plik |
| `NotablePowerManagementBehavior.BalanceGoldAndPowerOfNotable`, `OnHeroCreated`, `CreateCaravanParty` | okna O03/O04/O17 (K:CirculationWindows.cs:1757-1758, :1771); E3/7 `CaravanCapital` (prefiks + finalizer, K:CaravanCapital.cs:258) | A3 (kontekst Low; **v2:** po `GameStarted`, bez `isInitialSpawn`); **v2 (K8): E4/8 postfiks Low na `CreateCaravanParty`** (ludzie karawany, PLAN-KODU-E48.md:259-266, :469) | kontekst Low po oknach First; zloto - CaravanCapital/A3, ludzie - E4/8, wynik jednych nie zalezy od drugich; test A3 po scaleniu z E4/8 |
| `MapEvent.CalculatePlunderedAndLostGoldAmounts` / `CommitGoldChanges` / `LootDefeatedPartyShips` | 3.2 `BeatenPurse` (partie bez wodza), okno O14, `Measure31`, 181 (OFF) | E5/0, A6, B5 | statki - tylko 181; wodzowie - tylko A6 |
| `VillagerCampaignBehavior.SendVillagerPartyToTradeBoundTown` / `SellGoodsForTradeAction.ApplyInternal` | `MarketRoad.RoutePrefix` First (K:MarketRoad.cs:52); BK `SellGoodsPatch` (miasta) | B4 (**v2:** wlasna sprzedaz w zamku - prefiks false, znacznik "byl w zamku") | zamek - tylko vanilla ApplyInternal; brak petli zamek-zamek |
| zapas jedzenia warowni (`FoodStocks`, `SoldItems`) | WinterBite, E4/6 `ShelterPostfix` (K46 Famine.cs:386); **v2:** E4/11 pkt 8 postfiks na `CalculateTownFoodStocksChange` (danina wsi od ludzi) | B1 (kotwica jedzenia), B4 (**v2:** czyta polke targu i zuzycie podzamcza, nie `FoodStocks`) | jedzenie liczone raz (S4, S6); B4 i E4/11 pkt 8 - jedna funkcja |
| suma zlota swiata (`MoneyLedger.ReadHolders` / `WorldTotal`) | K:MoneyLedger.cs:670-706, :717 | E5/0 (pomiar), B1 (nowi posiadacze) | jedna definicja; posiadacze w "Pieniadz swiata" przed pierwszym przelewem |

### 5.2 Spojnosc logiki
1. **Jedna zasada na zjawisko:** zapas kupcow - jedna funkcja (D4) i **v2: jedno dno zakupow miast i zamkow** (`Spendable`); podatki wojenne - jedna zasada "wojna kosztuje poddanych" z podstawa wedlug platnika
   (v2: kupcy i wsie - danina od nadwyzki, lud - 184 od zarobku; War Tax mnozy obie, R15); zadowolenie z konsumpcji - jedna funkcja dla podatku (5.6) i glodu (5.9) (R14);
   glod - jedna definicja z nazwana przyczyna (D2); "warownia je z okregu" - jedna funkcja dla zamku teraz i miasta pozniej (D3) - **v2: razem z E4/11 pkt 8**; ludzie - jedna liczba z ksiegi (D1);
   **v2:** notable - jedna strona wymiany (kasa miasta handlu) dla notabli miast i wsi; bilans otwarcia - jedna regula dla daru kas (R5), notabli i karawan startowych (R10).
2. **Nic drugi raz:** markietani po rezerwach naprawy/odziezy/K1 (jak `OnLeft`); robocizna od broni 4% (praca jest w placach warsztatow - KL S10); kara zadowolenia jednym zapisem po BK (R14, migawka);
   zwrot korony - K3 tnie tylko to, co naprawde wraca panu zaworem (v2, R4; bez `HomePart`); jedzenie z targu do zapasu warowni liczy gra raz (`SoldItems`), B4 nie dopisuje drugi raz;
   **v2:** kotwica raz na (miasto, kategorie, dobe), nie przy kazdej pozycji polki; akcyza tylko przy `PayForGoods`; lokata poza kasa osady (ksiega liczy ja raz); zawor korony 1/2 to kompensata panow zamkow,
   nie zamiennik daniny (E5K1-01).
3. **Martwy kod staje sie zywy:** tarcze 107b/162m - martwe przy A1 (kod zostaje dla trybu 0); `PopulationRentMaxShare` (K:Settings.cs:637) traci znaczenie przy B1; hak `CastleDuesSuburbShare`
   i `VillageRentYieldsToFees` ozywaja w B1 (**v2:** jako wlasciwosci od warunku kiesy czynnej, nie stale); `LaySubsidy*` **v2: zostaje zywe** (danina). **Nowa praca:** markietani (ok. 700 partii/d), `TownFolk.Daily`
   (ok. 800 osad), kotwica - **v2:** postfiks `UpdateDemandShift` raz na osade (227) + mnozenie czlonu dobrobytu przy kazdej pozycji polki (setki pozycji na miasto - koszt T2 z E5/0) - ponizej 20 ms/dobe [S].
4. **Zamknieta ekonomia:** po C1 i A6 jedyne zrodlo/ujscie poza przelewami - bilans otwarcia (dar startowy, zapasy startowe kies ludu). Kazdy nowy posiadacz (kiesy ludu, gmina, lokata) w sumie swiata od pierwszej doby.
5. **S4 - bieda a spichlerz:** jedzenie kupione przez ludzi z targu gra dolicza do zapasu warowni (`SoldItems` z `BonusToFoodStores`, G DefaultSettlementFoodModel.cs). Gdy ludzi nie stac, kupuja mniej jedzenia
   i zapas warowni maleje - przy pustym zapasie gina ludzie (E4/6). To ta sama droga dla dwoch przyczyn (bieda, brak plonu) - zamierzona; w rachunku KL jedzenia stac 100% dob [R], wiec w zwyklej grze bez skutku;
   prog F6/F7 etapu 4 w biegu zimowym B pilnuje.
6. **Ta sama kolejnosc w dobie:** tick osady gry (konsumpcja, regulator) w `DailyTickTownEvent` - poza naszym blokiem, mierzony postfiksami; nasz blok: `MoneyLedger.BlockOpen` -> `TownPurse.BeforeRents` -> `PopulationLaw.Daily`
   (stan miast E4/4 i migracja E4/8 przed rentami) -> `TownPurse.Daily` -> `CastlePurse.Daily` -> `TownFolk.Daily` -> `BuildFunding` -> ... -> `KingdomTreasury.Levies` (danina jak dzis; 184 pobiera przy Earn - tu tylko linia; v2) ->
   `CrownIncome` -> `CrownRents` -> ... -> markietani -> `ClanBudget.Daily` -> ... -> `MoneyLedger.Daily`. Koszyk dnia czyta stan miasta po migracji tej doby; schronienie E4/6 - stan Famine z poprzedniej doby (RD rozdz. 2).
7. **Gracz = AI:** markietani, notable, 184, wyrownanie (E), targ przy zamku, wies 35% - takze u gracza; wyjatek z decyzji: dwor graczowi nie [D 08.10]. Z8: z monety zoldu gracza wraca 0.40-0.62 (A15, KL 4.5) - zawsze < 1.
8. **Stan czyszczony miedzy kampaniami:** `TownPurse.Reset`, `TownFolk.Reset`, flagi `arm_townpurse`, `arm_sutlers`, `arm_townfolk` - w konstruktorze `ArmouryBehavior` (:395), klucze zapisu we wlasnych try.
9. **v2 - przelaczniki i pulapka MCM (K3):** kazda paczka wylaczona = stan sprzed paczki, takze w zaleznosciach: B2, B3, 75/25 i haki B1 tylko przy kiesie czynnej; nowe zachowanie istniejacego klucza
   wlaczane nowym kluczem (`TownFolkLive` w B1, `TownPurseNoTopUp` w C1), nie zmiana domyslnej; zmiany domyslnych istniejacych kluczy (B3, B5) - grep w `Armoury.json` Jeffa i autotestu przy wgraniu,
   wartosci czynne w linii startowej, `--grupa etap5` zglasza bieg B z wartosciami sprzed B. **Skala R14:** -0.0015/d to ok. 100 dob od 1.0 do progu 0.85 (BK BKProsperityModel.cs:60) - skutek podatku
   i biedy dla dobrobytu jest powolny; liczbe ustawic z linii "Zadowolenie BK (E5/0)" biegu A (projekt wybiera).

### 5.3 Cudzy kod (sprawdzone w dekompilacji do tego projektu)
- BK `ItemConsumptionPatch.Prefix` (BK EconomyPatches.cs:501-600): zdejmuje towar, `ChangeGold(num6)` :555, `ConsumedValue` :554, zadowolenie +0.001 za kazda pozycje :549 / -0.0015 tylko w galezi dokupu
  jedzenia :544 (**v2**), dokupienie jedzenia przy zapasie <= 10% :532-536; petla po pozycjach polki :514, `CalculateBudget` przy kazdej pozycji z reszta budzetu :523/:552 (**v2**);
  drugi prefiks (`DeleteOverproducedItems`) return false :606-609. `CalculateBudget` :1085-1101 (popyt x indeks^y, y = 0.15 / High 0.1 / Low 0.2, + dobrobyt / 1000 / indeks).
  `EconomicData.UpdateSatisfaction` :148-152 (Clamp 0..1), `Satisfactions` publiczne :62 (**v2**). BK obniza polityke wsi z hearth < 300 tylko, gdy miasto/zamek okregu ma zarzadce (BK BKSettlementBehavior.cs:376-396, **v2**).
- BK `VillagerSettlementEnterPatch` (:904-935): sprzedaz w osadzie z `Town` (takze zamek) :911-912, wies dostaje 50% reszty :918, majatki BK `AccumulateTradeTax` :927. `BKTaxModel.CalculateVillageTaxFromIncome(Village,int)`
  :271-284 (0.7 / Low 0.5 / High 0.9); przeciazenie `(Village, bool, bool)` :212.
- BK `HandleMarketGold` :463-490 (dolna galaz +1 000 od notabla z 30 000; gorna -1% i blad znaku: kasa -0.5%, notabl -0.5%).
- Gra: `MakeConsumptionInTown` :61-71 (kolejnosc: DeleteOverproduced -> popyt -> MakeConsumption -> GetFoodFromMarket -> SellLog -> UpdateTownGold); `GetTownGoldChange` :75-79; budzet vanilla :81-84;
  `DefaultSettlementFoodModel` - `SoldItems` z `BonusToFoodStores` dodawane do zmiany zywnosci przy `includeMarketStocks`; `NotablePowerManagementBehavior` :47-61; `NotablesCampaignBehavior.OnHeroCreated` :41-47;
  `DefaultBattleRewardModel` :104-107 (5% kiesy wodza, najwyzej 10 000), :197-210 (partia bez wodza 10%, banda 50% - dzis 100% przez 3.2); `MapEvent` :1497-1526 (pula), :1800-1817 (statki -> `PlunderedGold`);
  `MapEventParty.CommitGoldChanges` :499-524.
- **v2 sprawdzone po krytykach:** `ItemConsumptionBehavior.UpdateDemandShift` :178 (slownik budzetu przed `MakeConsumption` :66; przy tworzeniu swiata :22-40); BK `TickCastle` wola `MakeConsumptionInTown` refleksja
  (BKSettlementBehavior.cs:631); `SellGoodsForTradeAction` :43-58 (polka, cala `town.Gold`); `VillagerCampaignBehavior` :286-296 (woz z > 1 pozycja znow do miasta); `NotablesCampaignBehavior` :41-47, :80-83;
  `Campaign.GameStarted` :894-896 i :1357 (wczytany zapis); `Village.TradeBound` :103.
- **Nie sprawdzone (do planu kodu):** BEE - co daje wplata do skarbca (A5); czy `BKLoyaltyModel` jest czynny i czy postfiks warstwy dobrobytu BK liczy sie dwa razy (RBL -> base) - E5/0 wypisze;
  skutki glodu RBL/BEE przy naszym lancuchu jedzenia (C2); czy profile konsumpcji klas BEE sa martwe po 170 (B1).

### 5.4 Gra Jeffa
Jeff gra dopiero po skonczeniu projektu, w nowej kampanii [D 09.10 08:05]. Zbior A moze wejsc do trwajacego zapisu (dar startowy - nic; markietani z rozruchem 28 dob). Zbior B i C - pelny skutek tylko
w nowej kampanii (kiesy ludu ze zapasami startowymi); w starym zapisie kiesy z kasy kupcow (KL 5.3). `Armoury.json` Jeffa (sprawdzone 11.10, plik z 08.10, 9 875 B): z kluczy etapu 5 jest tylko
`WeeksPerSeason` 13 - zmiany domyslnych zadzialaja; przy kazdym wgraniu powtorzyc grep (szczegolnie B3 i B5 - zmiany domyslnych istniejacych kluczy; **v2:** takze `TownFolkMode`, `TownPurseRegulator`,
`TownRentLordShare`, `CrownRentWeight*`, `ShipsToPort`, `ShipTradeThroughPort`, `ShipPrizesToPort` - w pliku Jeffa i autotestu). **v2:** zbior A w trwajacym zapisie - notable i karawany juz istnieja,
bilans otwarcia nie dotyczy (regula od nastepnej doby).

---

## 6. Pytania do Jeffa (tylko kierunek; reszte decyduje projekt)

1. **Test porcji A (kasy miast, dwor, markietani, notable, BetterEconomy): osobno czy w tescie koncowym etapu 4?**
   (a) **osobny test zaraz po tescie koncowym etapu 4** (40 dob + zapis 9 dob + 120 dob, **v2:** ok. 50-85 min) - gdy cos nie przejdzie (wojsko, glowy rodow), wiadomo od razu, czy to etap 4 (zalogi, nowe druzyny),
   czy kasy miast; a z tego biegu ustawiamy stawki kiesy ludu (zaleza od kas miast i markietanow);
   (b) razem z testem koncowym etapu 4 - krocej, ale koniec etapu 4 to 364 doby + zima 120 + 728 dob (ok. 4 h): kazdy niezdany prog porcji A wymuszalby powtorke tych dlugich biegow.
   Rada: **(a)**. Pomiar E5/0 i tak jedzie w tescie koncowym etapu 4 (nic nie zmienia w grze). Blokuje tylko kolejnosc testow; bez odpowiedzi - (a).

**Informacja (bez pytania - decyzje projektu albo Twoje wczesniejsze; mozesz zmienic jednym slowem):**
- **v2:** Podatek wojenny od zarobku ludzi (Twoje "od ludzi", 09.10 C) **dochodzi** do dzisiejszej daniny wojennej z nadwyzki kupcow i wsi (ok. 97 tys. dziennie) - nie zastepuje jej. Obietnica "panowie zamkow
  -1% / -4%" i wojsko -2% byly policzone z danina i z polowa nadwyzki kupcow dla korony; gdyby danine zdjac, korona mialaby w wojnie mniej niz dzis, a statki i tak zabiora lordom ok. 0.4 mln dziennie z powietrza.
  Stawka wedlug potrzeby korony (rosnie, gdy korona musi pozyczac w Banku albo siegac do starego skarbu na zold): do 3%, z polityka War Tax do 6%. Czy danine pozniej scalic z podatkiem - po tescie rocznym.
- **v2:** Przy niskiej polityce podatkowej wsi (BK ustawia ja wsiom ponizej 300 domow, gdy zamek/miasto ma zarzadce) pan bierze 25% utargu, czyli ok. 21-25% calej gotowki wsi - ponizej Twoich "30-50%".
  Tak bylo w rachunku, ktory przyjales (decyzja 15: "przy niskiej polityce ok. 28-31%" z septem); przy zwyklej polityce 28-45%, nigdy ponad 50%.
- **v2:** Targ przy zamku liczy jedzenie dla podzamcza (polka targu zamku), nie spichlerz zalogi - ten ma dzis zapas na ok. 300 dni i zasila go danina wsi z etapu 4.
- Liczbe ludzi dla kiesy ludu bierzemy z ksiegi ludzi etapu 4 (pobor, glod i migracja zmieniaja koszyk), a skale pieniadza z Banner Kings z dnia startu (jak w etapie 4).
- Dziesiecina wchodzi w ostatniej porcji, po roku testu (jak w planie); do tego czasu wies zatrzymuje 65% utargu, pan bierze 35%.
- Pkt 5.12 planu skreslony (zawiera go wyrownanie); w 5.7 waga zamku 1.5 (odpowiadales na pytanie z 1.5).
- Dar startowy kas miast ponad zapas kupcow jest raz przycinany w pierwszym dniu kampanii (jak kasy zamkow od paczki 110); po kiesie ludu najpierw daje ludziom zapasy na start.
- Gdy ludzi nie stac na chleb (oblezenie, drozyzna, wysoki podatek), kupuja mniej jedzenia z targu, a gra liczy to jedzenie do spichlerza miasta - przy pustym spichlerzu zaczyna sie glod z etapu 4.
  W rachunku kiesy ludu jedzenia stac w kazdym krolestwie kazdego dnia, wiec to tylko sytuacje skrajne.
- Zolnierze wydaja 10% sakiewki dziennie w polu - takze Twoi (jedna regula). Notable dostaja i oddaja zloto przez kase **v2:** miasta, w ktorym handluja (wiejscy - miasta targowego swojej wsi);
  nowy notabl dostaje 10 000 z tej kasy, nie z powietrza; notable nowego swiata zaczynaja ze zlotem gry (bilans otwarcia).
- Zaplata za najem w karczmie idzie do sakiewki najetego weterana, nie do kasy miasta (najmujesz prawdziwego czlowieka).

---

## 7. Odlozone (i dlaczego)

| Rzecz | Dlaczego nie teraz | Kiedy |
|---|---|---|
| spichlerz miasta zywiony przez okregi (GL 2.13 krok B) | ta sama funkcja co B4 (D3), ale budzi BEE (-0.5% chlopow/d przy zmianie zywnosci < -3, GL 2.13) - najpierw pomiar E4/6 | po tescie B (wlaczenie funkcji B4 dla miast) |
| rabat AI 0.01 przy kupnie statku (181d) i wycena A | niezgodny z "jedna regula", ale bez niego AI nie stac na floty (E3:705); wycena A = -15.8% wojska | po tescie B - liczby flot AI i kas portow |
| bezpiecznik korony dla kas miast (OB :255, :363) | PLAN 5.8 "korona bez bezpiecznika" | tylko gdy C1 nie przejdzie progu plonu (wtedy pytanie) |
| tarcza zoldu karawan (E3:701) | tarcze znikaja w A1 | skreslone |
| podatek warsztatow lordow w cudzych miastach (E3:697) | zrobione w D11 | skreslone |
| 5.12 | zawarte w Z15-5 | skreslone |
| moneta z kopaln przez mennice (KL ryzyko 7) | potrzebna tylko, gdy ludnosc rosnie > 2%/rok (dzis +0.5%, SP:1185) | prog liczbowy: ludnosc > 2%/rok w biegu rocznym |
| ceny reagujace na ilosc pieniadza (KL 4.7) | gra ma stala cene z podazy/popytu; drozyzna jako wariant | pomysl (PLAN rozdz. 3) |
| kasy miast z dlugami, pozyczkami, podatek nadzwyczajny | pomysl Jeffa "tylko na slowo" (PLAN rozdz. 3) | na slowo Jeffa |
| plon wiosek pobocznych (E4/11) w utargu | tylko przez API plonu, bez kodu etapu 5 | z E4/11 |
| sprzet zwolnionego ze zbrojowni pana (E4/10 pyt. 2) | osobny krok po etapie 4 | po etapie 4 |
| **v2:** scalenie daniny z podatkiem 184 w jeden pobor od ludzi | rachunek A15 i progi B3 stoja na danine obecnej (E5K1-01) | po tescie B - z liczbami wplywow korony i wojska |
| **v2:** zakupy gry/BK (tabory, karawany) tylko ze `Spare` | plon musi byc kupiony (prog 98%); ich czesc ponizej zapasu jest liczona | C1 albo gdy licznik "dosypka finansujaca zakupy" > 2 tys./d |
| **v2:** bieg kontrolny bez B3 (porownanie kies panow zamkow) | dodatkowe 364 doby, przebieg niepowtarzalny (czas rzeczywisty, AI) | tylko gdy udzial panow zamkow w D nie przejdzie |

---

## 8. Krytyka - co przyjeto/odrzucono (v2, skrot; pelna lista z dowodami: `KRYTYKA-E5.md`)

Dwie krytyki (E5K1-01..12 - ekonomia i progi; K1-K9 - kod, latki, kolejnosc). Kazda uwaga sprawdzona w kodzie, dekompilacji i logu z31-A. 17 przyjetych w calosci,
4 z innym rozwiazaniem albo poprawka, faktow falszywych brak; 3 wlasne uwagi (W1-W3).

| Uwaga | Werdykt | Zmiana w projekcie |
|---|---|---|
| E5K1-01 (krytyczna) 184 zamiast daniny liczy zawor dwa razy | przyjeta | danina i War Tax zostaja, 184 na wierzch (R15, B2); rachunek sim_kiesa2 z danina, 184, B3, B5, A6 przed testem B; prog: wplywy korony w wojnie >= bieg A |
| K1 = E5K1-09 (krytyczna) kotwica/pomiar w petli BK po pozycjach | przyjeta | m raz na (miasto, kategorie, dobe): `UpdateDemandShift` + czlon dobrobytu; m wobec wydatku; budzet E5/0 ze slownika (R1, E5/0, B1) |
| E5K1-02 potrzeba 184 zawsze 0 (Bank) | przyjeta | potrzeba = kredyt 186 / zwrot ponad wplywy bez 1/360 / skarbiec < zapas wojny (60 dob zwrotu); prog: s rosnie przy pozyczaniu, dlug 186 nie rosnie |
| E5K1-03 zakupy do 20 000 finansuje dosypka | przyjeta (wariant `Spare`) | nasze sciezki zakupu miast ze `Spendable` jak zamki B-2; sciezki gry/BK liczone, warunek C1; wariant "dosypka do 20 000" odrzucony (sciezki gry ida do 0) |
| E5K1-04 notable wiejscy bez definicji | przyjeta (inne rozwiazanie) | strona wymiany = kasa miasta handlu (wies -> `TradeBound`), nie kiesa wsi (R10, A3) |
| E5K1-05 = K4 notable/karawany z tworzenia swiata | przyjeta | bilans otwarcia przed `GameStarted` i dla `isInitialSpawn`; A3 NK "tak", M |
| E5K1-06 prog 30-50% przy niskiej polityce | przyjeta czesciowo | jedna definicja (pan z majatkami, G z markietanami, wedlug polityki); progi w utargu i w G; pytanie do Jeffa odrzucone - decyzja 15 przyjeta na Z15-0 z "28-31% przy niskiej" |
| E5K1-07 B4 na spichlerzu zalogi | przyjeta | warunek = polka targu zamku / zuzycie podzamcza (R17, B4); zatkane magazyny lato/zima osobno |
| E5K1-08 = K6 HomePart, K3 | przyjeta | bez HomePart i klucza; K3 = udzial pana w zaworze, markietani w K3 (R4, R7, A1) |
| E5K1-10 prog mediany kies < rozrzut | przyjeta czesciowo | prog glowny = udzial w D swiata; mediana jako srednia 4 blokow, rody w wojnie, odczyt; bieg kontrolny odrzucony (koszt, niepowtarzalny) |
| E5K1-11 akcyza dwa razy | przyjeta | akcyza tylko w `PayForGoods`, przy Earn cech 1% (R11) |
| E5K1-12 lokata dwa razy | przyjeta | lokata poza `Gold` (czesc kiesy W) (D4, B1) |
| K2 R14 nie dziala | przyjeta | migawka `Satisfactions` w nawiasie 110; sprostowanie "BK karze brak towaru"; ryzyko skali (R14, B1, 5.2 pkt 9) |
| K3 przelaczniki | przyjeta | `TownFolkLive` (B1), `TownPurseNoTopUp` (C1), zaleznosci B2/B3 od kiesy, haki jako wlasciwosci, linia startowa i `--grupa etap5` (5.2 pkt 9) |
| K5 B4 a E4/11, sprzedaz, petla | przyjeta | B4 po E4/11, M, jedna funkcja z E4/11 pkt 8, wlasna sprzedaz, znacznik wozu |
| K7 test A i kalibracja | przyjeta | argument kosztu dlugich biegow E4, 50-85 min; stawki B z biegu A |
| K8 `CreateCaravanParty` z E4/8 | przyjeta | tabela 5.1, test A3 po scaleniu z E4/8 |
| K9 rdzen B rownolegle z E4/8 | przyjeta | kod B1-B3, B5 po scaleniu E4/7b; czeka kalibracja i test (pkt 0.10, tabela paczek) |
| W1 (wlasna) na `CalculateBudget` siedzi juz `HistoricalPrices.BudgetPostfix`, `RawPrice` wola refleksja | - | tabela 5.1 poprawiona; kolejnosc latek; pomiar tylko w nawiasie konsumpcji |
| W2 (wlasna) `TownUse` tez mnozy przy kazdej pozycji | - | odnotowane, bez zmian (dzis bez skutku) |
| W3 (wlasna) gra konsumuje przy tworzeniu swiata | - | bramka `GameStarted` na kotwicy i pomiarze |

**Kolejnosc po v2:** E5/0 (D) i A1 (D), A2 (S), A3 (M), A4 (M), A5 (S) - kod teraz; A6 (S) po wyniku E5/0; B1 (D), B2 (S), B3 (M), B5 (M) - kod po scaleniu E4/7b, rownolegle z E4/8;
B4 (M) po kodzie E4/11; B6 (S) po E4/10; C1 (M), C2 (S), C3 (S), C4 (M) - po roku testu B. Testy: E5/0 w tescie koncowym E4, zbior A osobno zaraz po nim, rachunek przed testem B, zbior B, zbior C.
