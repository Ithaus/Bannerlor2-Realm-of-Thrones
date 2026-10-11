# PROJEKT ETAP 4 - ksiega ludzi i zima (PLAN 4.1-4.19) - 10.10.2026, wersja 2 (po krytyce)

Status: SAM PROJEKT. Kod, gra, ustawienia i zapisy nietkniete, nic nie budowane.
Wersja 1: `scratchpad\e4\PROJEKT-ETAP4-KSIEGA-LUDZI-ZIMA-2026-10-10.v1.md`. Krytyka: `scratchpad\e4\KRYTYKA-E4.md` (U1-U28) - kazda uwaga sprawdzona
w kodzie, dekompilacji albo logu; co przyjeto, co odrzucono i dlaczego - **rozdz. 8**. Zmienione miejsca oznaczone w tekscie numerem uwagi (np. "(U3)").

Podstawa (dowody nie sa tu powtarzane w calosci):
- rozpoznania `scratchpad\e4\A-kod.md` (A), `B-dokumenty.md` (B), `C-logi.md` (C), krytyka `KRYTYKA-E4.md` (KR);
- kod Armoury/CrashScribe: galaz `w-toku/e3-31m` (a939cd5) = gra Jeffa + etap 2 + etap 3: skrot **K:** = `scratchpad\e3\31m\Armoury\src`, **CS:** = `...\31m\CrashScribe\src`;
- dekompilacje (ilspycmd 9.1): **dec:** = `scratchpad\e4tools\dec\` (typy gry, ROT, RBL, BEE, NavalDLC), **dec2:** = `scratchpad\e4tools\dec2\` (Fishing.cs = NavalDLC
  `FishingPartyCampaignBehavior`, Raid.cs = gra `RaidEventComponent`), **BK:** = `scratchpad\e4tools\bkfull\` (caly BannerKings.Redux),
  **e4dec:** = `scratchpad\e4\dec\` (Villager.cs, BK.all.cs, BetterEconomy.all.cs, BKROTPatch.all.cs), **G:** = `scratchpad\dec32\cs_full` (pelna TaleWorlds.CampaignSystem);
- stare paczki ludzi w klonie `lancuch`: 108 `e548266` (= m108 1ffe021), 109 `f95452e` (= 86395b5), 113 `5cbb0d6` (= 86f3305) na `origin/paczki-na-120/*`, baza n119 0c7aa2d;
  opisy `docs\paczki\108-ludzie-jednostka.md`, `109-ludzie-przyrost.md`, `113-ludzie-spustoszenie.md`;
- log 457 dob (nowa kampania, gra Jeffa przed WGRANIEM 24): `kopia-okup1-457\Armoury-2026-10-10_10-38-24.log` = **log:N**; doby gry 108836-109294;
  **csv-bk** = `kopia-okup1-457\2026-10-10_10-38-24\ludnosc-bk.csv`;
- dokumenty (docs\): PLAN = PLAN-DO-KONCA-MODA-2026-10-09.md (etap 4: :120-146), SP = STAN-PRAC.md (DECYZJE JEFFA - wiazace), DOK = DEMOGRAFIA-SILA-ROBOCZA-2026-10-05.md
  (kroki 1-9: :560-640), AN = DEMOGRAFIA-ANEKS-2026-10-05.md, RK = REGULY-KRAIN-I-DLUGU-2026-10-06.md, GL = PROJEKT-GLOD-2026-10-07.md, HR = HISTORIA-RABUNKU-I-BITEW-2026-10-07.md,
  PP = PLAN-POBOR.md, ES = audyt-2026-10-09\ESENCJA-AUDYTOW-2026-10-09.md, A02 = audyt-2026-10-09\02-wies-produkcja-krainy.md, P10 = audyt-2026-10-09\10-pomysly.md.

Oznaczenia: [K] kod plik:linia, [P] pomiar z logu, [S] szacunek, [D] decyzja Jeffa (data). Rok gry = 364 doby. "/d" = na dobe (srednia d1-d457, chyba ze napisano inaczej).
PL = PopulationLaw ("Ludnosc:", "Ludzie:"), BK = BannerKings.Redux, BEE = BetterEconomy, RBL = RealisticBannerlord, k_v = ludzi na punkt hearth (wies) / dobrobytu (miasto) osady v.

---

## 0. Dla Jeffa - prostym jezykiem

1. **Etap 4 ma policzyc kazdego czlowieka.** Dzis "ludnosc" w logu rosnie o ok. 58% rocznie (52.5 -> 93.0 mln w 457 dniach), bo gra dopisuje kazdej
   wsi co dzien staly kawalek ludzi z powietrza. Wojna jej nie zmniejsza: 414 tys. zabitych nie ubylo z niej ani jednego czlowieka. Wojsko, zalogi,
   milicje i karawany biora ok. 1 000 ludzi dziennie znikad. Wyspy Letnie (3 wioski) "maja" dzis drugie miejsce na swiecie - 9.8 mln.
2. **Po etapie 4:**
   - ludzi przybywa powoli, jak w sredniowieczu (0.2-0.8% rocznie w pokoju), a wojna, glod i zaraza ich ubywaja. Uwaga: w dzisiejszej wojnie ginie
     0.6-0.8% ludzi rocznie - tyle, ile wynosi caly przyrost; stad pytanie 4;
   - rekrut wychodzi z konkretnej wsi, wraca do niej po zwolnieniu albo ucieczce z bitwy, polegly nie wraca nigdy. Dopoki z okolicy pod bronia
     (i w grobach) jest najwyzej 20% mezczyzn - "wolne rece", Twoje "czesc byla wolna" - plon nie spada; ponad to spada (30% mezczyzn: plon -6.5%).
     W czasie zniw ten prog jest o polowe nizszy (10%) (U4);
   - zalogi, milicje i nowe druzyny lordow nie rosna z powietrza - tylko z ludzi swojej okolicy;
   - rabunek wygania czesc ludzi okolicy (uchodzcy), czesc ginie, czesc idzie do lasu, a u ludow z niewola czesc idzie w niewole - to ci sami ludzie,
     nie dodatkowi (U14). **Wszyscy uchodzcy wracaja najpozniej rok po ostatnim najezdzie w okolicy** (Twoje "odbudowa 1 rok"; U13). Wies, z ktorej
     uciekla 1/3 ludzi, daje 1/3 mniej plonu (a nie zero przez dwa tygodnie, jak dzis);
   - spichlerz okregu napelnia prawdziwa produkcja zywnosci; glod zabija wedlug historii;
   - **ksiege kazdej okolicy zobaczysz w menu osady** (ludzie w domu, w sluzbie, w niewoli, wyrzutki, uchodzcy, polegli) - od paczki E4/7 (U21).
3. **Zima wedlug krain:** Dorne i cieple Essos tylko lekko (-15% plonu zamiast -38%), Wyspy Letnie bez zimy, Skagos hoduje owce zamiast zboza,
   konie jedza w zimie obrok (poza stepem). Zima zalezy od miejsca na mapie, nie od tego, czyja kultura rzadzi wsia (U18). Dluzszej nocy w obozie nie ma (Twoje "nie").
4. **Inni** rosna tylko z tych, ktorych sami zabija za Murem i przy Murze, oraz z ich jencow - takze z jencow, ktorych odbija pokonanym (dzis ci
   wchodza do bandy jako zywi ludzie; Twoja decyzja 07.10 - U6). Nic z powietrza.
5. **Nocna Straz** werbuje z wyrzutkow Westeros (3% puli dziennie) i z ludnosci; nie pali wsi za Murem; **sluzba dozywotnia - nikogo nie zwalnia do domu**
   (dzis zwalnia nadmiar ponad pulap; U5). **Wolni Ludzie** 300 tys., walcza wszyscy dorosli. **Niewola** tylko w krainach z kanonu, jency z rabunku to prawdziwi
   ludzie zabrani z napadnietej wsi - u AI i u Ciebie wedlug tej samej reguly (U22).
6. **Pierwsza paczka (E4/0) niczego w grze nie zmienia.** To pomiar: kazdy punkt ludnosci, milicji i dobrobytu z przyczyna, jency (skad i dokad), Straz i Wolni
   Ludzie w tabeli strat i przyrostu, Inni wedlug miejsca, zmarli z zarazy, zmiany kultury osad. Do tego **pierwsza proba zimy**: 120 dni zimy od startu w autotescie
   (nie w Twojej grze). Bez niej nikt jeszcze zimy nie widzial - w 457 dniach testu zima by nie przyszla (zaczelaby sie ok. 543. dnia).
7. **Czego sie spodziewac po calym etapie:** zalogi przestaja rosnac same - w 457-dniowym biegu w dniu 364 mialy 88 tys. ludzi, z czego 41 tys. dopisal model gry z niczego;
   bez niego najwyzej ok. 47 tys., czesc odrobi platny werbunek z ludzi. Prog etapu: wojsko lordow w wojnie 95-115 tys. (Twoje 108 tys.) i zalogi co najmniej 85% dzisiejszych
   w dniu 364 - jesli nie, poprawiamy werbunek zalog z ludzi, nie zmniejszamy wojska (Twoje "mniej wojska AI - NIE"; U20). W karczmach o 81-97% mniej najemnikow
   (wynika z Twoich decyzji 05.10 i 08.10; na start pula starych zolnierzy); male krainy (Straz, Wolni Ludzie, Sarnor, Smocza Skala) moga slabnac latami; Wyspy Letnie skurcza sie
   do ok. 0.11 mln (zaden punkt mapy nie znaczy wiecej ludzi niz w najgesciej zaludnionej krainie - Qarth). Do paczki E4/4 ludnosc w logu dalej rosnie ok. +57% rocznie (U28).
8. **Pelny skutek tylko w nowej kampanii:** tabela krain, start Zelaznych Wysp (27 tys. niewolnych) i Wolnych Ludzi, karczmy, wioski poboczne. Reszta dziala tez w starym zapisie.
9. **12 paczek** (rozdz. 3), kazda z wlasnym testem 120 dni i wlasnym wylacznikiem w menu. **4 krotkie pytania** (rozdz. 6) - jedno nowe (4: prog ludnosci a smierc w wojnie);
   pytanie o Twoich jencow z rabunku skreslone (obowiazuje jedna regula dla Ciebie i AI). Reszte decyduje projekt.

---

## 1. Co dzis (liczby)

Bieg 457 dob (nowa kampania, jedna sesja, kalendarz 364 dni; log:145, :150, :183). Po kolei: stan na starcie, na koncu, na dobe, cel etapu 4.

| Pozycja | Start (d0) | Koniec (d457) | Na dobe | Cel etapu 4 | Dowod |
|---|---:|---:|---:|---|---|
| **Ludnosc PL ("Ludzie:")** | 52.51 mln | 92.98 mln | +88 550 (+57.6%/rok) | +0.2-0.8%/rok netto (290-1 150/d) - patrz pytanie 4 | log:519, :123833 |
| w tym wsie / miasta | 41.98 / 10.54 mln | 78.29 / 14.70 mln | | | log:519, :123833 |
| tempo d0-91 / d273-364 | | | 189% / 15%/rok | | C 2 (progi stalej gry 300/600) |
| Wyspy Letnie (k = 4 655 ludzi na hearth) | 0.75 mln | 9.78 mln (x13) | 22% przyrostu swiata | ok. 0.11 mln, bez skokow | log:325, :123620 |
| hearth wsi swiata | 215 393 | 356 837 | +310 netto | ruch tylko z ludzi | C 2 (`ludzie-regiony.csv`) |
| stala gry +4/+1.2/+0.2 na wies (szacunek z rozkladu wsi) | ok. 723/d | ok. 351/d | | 0 (zamiast niej stopa g) | dec DefaultSettlementProsperityModel.cs:43-46 |
| **Ludnosc BK (klasy BK = BEE)** | 2 911 444 | 2 815 349 | -210 (-2.6%/rok) | najwyzej +2%/rok | log:489, :123794 |
| **zabici w bitwach** | | 413 930 | 906 | trwaly ubytek regionu pochodzenia | C 6; z PL nie ubywa nikt |
| zabici wedlug okresu (d1-91 / d92-182 / d183-273 / d274-364 / d365-457) | | | 480 / 925 / 938 / 1 074 / 1 103 | = 0.33 / 0.64 / 0.65 / 0.74 / 0.76% ludnosci startowej rocznie | [P] linie "Ludzie: ... dzis: zabici N" (458 dob) |
| zmarli z zarazy (CampFever, tylko oblezenia) | | 157 | 0.34 | do ksiegi jako polegli | C 12; K:CampFever.cs:184 |
| **ludzie pod bronia z niczego** (szablon klanu 401, zalogi z modelu 114, autowerbunek 128, milicja >= 96, karawany >= 132, inne >= 33, Inni 55, bez osady 44) | | | ok. 1 000 | 0 | C 4 (Z1-Z9) |
| z ludnosci BK (ochotnicy 885, karczma 105) / z hearth (tabory 453, bieda 25) | | | 990 / ok. 480 | z ksiegi ludzi (wies werbunku) | C 4 (Z10-Z13) |
| ujscia w nicosc (zabici 906, rozbici "z szablonu" 37, wighty 11, Inni zniknieci 20) | | | ok. 981 | tylko smierc (nazwana) | C 4 (U1-U6) |
| **jency** (U1) | w partiach 22, w lochach 0 | 2 048 / 52 | do domu 111, sprzedani 76, na Mur 0.5 | konto JENCY z pochodzeniem | C 4 "Bilans", U9-U13 |
| **reszta niewyjasniona ludzi pod bronia** | | | ok. +283 | nazwana | C 4 "Bilans" |
| zalogi | 46 422 | 78 403 (d364: 88 076) | +70 netto | przyrost bez werbunku = 0 | log:485, :123790; [P] linie "Zalogi (169c)" |
| zalogi: przyrost z modelu gry / autowerbunek A5 (do d364) | | +52 037 / +58 638 (+41 459 / +48 900) | 114 / 128 | 0 / z ludzi regionu | log:123790; C 5 |
| milicje | 62 643 | 80 794 | +40 netto (z modelu >= 96) | tylko z ludzi regionu | C 5 |
| nowe partie lordow z szablonu klanu | | 183 371 (3 559 partii) | 401 | z ludzi | log "Przegrani (H3)" |
| tabory wsi | 2 487 | 18 930 | | 1 czlowiek = 1/k hearth | log:519, :123833 |
| pula wyrzutkow (w tym wighty, przed W4b) | 363 | 22 338 (5 708) | +48 | bez wightow | log:475, :123781 |
| **Inni** | 616 | 2 920 (szczyt 6 506 d414) | | tylko z poleglych za Murem i przy nim | log:492, :123797 |
| Inni: nekromancja ROT / nowe partie / zniknely | | +23 186 / +1 777 / -9 186 | 51 / 4 / 20 | region nieznany (log go nie podaje) | C 9 |
| Nocna Straz w ludziach / pulap | | 1 702 / 5 056 | | rekrut z wyrzutkow i ludnosci | log:123784 |
| Wolni Ludzie (tabela / ludnosc PL na koncu) | 150 tys. (decyzja: 300) | 0.21 mln | | 300 tys. | K:PopulationLaw.cs:52; log:123620 |
| zmiany kultury osad (U3) | | 0 | 0 | ludzie osady od kultury niezalezni | csv-bk (365 tys. wierszy), `e4\krytyka_tools\culture_changes.py` |
| warownie glodne / zapas min / mediana | 0 | 0 / 121-136 dni / 801 dni | | po 30 dobach zimy nikt nie gloduje, mediana >= rok | log:123833; C 10 |
| **zima** | | 0 dni (lato 130 + jesien 412 -> zima od d543) | | test 120 dob z zima od startu | log:150, :35089; K:Settings.cs:693-699 |
| szansa zimy w biegu 457 / 120 / 364 dob przy dzisiejszych ustawieniach | | ok. 3% / 0% / 0% | | | C 11 |

**Najwazniejsze przyczyny** (A 1, C 3, KR):
- **Trzy ludnosci i zadna nie jest ksiega.** PL liczy ludzi jako hearth x k albo dobrobyt x k, z k zamrozonym w pierwszej kalibracji NA KULTURE i czytanym z biezacej
  kultury osady (K:PopulationLaw.cs:85-113, :170-179, zapis `arm_population` K:ArmouryBehavior.cs:466-468). Klasy BK to kopia BEE (BK:PopulationData.cs:394, :397-423), ktora rosnie
  sama 0.22%/d z podlogami 50/300 (dec BetterEconomy.Behaviors.PopulationCampaignBehavior.cs:99, :116). Hearth zmienia gra i nasz kod.
- **Dwie jednostki.** Gra i nasz kod zdejmuja/oddaja 0.5 hearth za czlowieka (e4dec Villager.cs:179, :187; K:LosersFlee.cs:66-67; K:OutlawLaw.cs:275, :289, :566;
  K:Settings.cs:669), a PL liczy 1 hearth = k ludzi (Polnoc 134, Reach 454, Wyspy Letnie 4 655; log:325). Jeden taborzysta wracajacy do wsi Reach dopisuje ok. 227 osob.
- **Nic nie zdejmuje ludzi z PL**: smierc, pobor i zaraza nie ruszaja hearth ani dobrobytu (C 5-7). Werbunek zdejmuje tylko z BK (BK:RecruitmentApplyInternalPatch.cs:20,
  gracz BK:RecruitmentOnDonePatch.cs:33).
- **Powroty ida trzema drogami, nie jedna** (U2): `LosersFlee.SendHome`, `ClanBudget.ToVillage` (zwolnienia 166, przepelnienie W4c, rycerze 179) i `OutlawLaw.ReturnHome` (rozdz. 2 R0).
- **Jency zmieniaja strony bez zdarzenia** (U1): jency pokonanego ida wprost do skladu zwyciezcy (G MapEvent.cs:1529-1567, MapEventParty.cs:75-85) - takze do bandy Innych.
- **Dekret BK "Growth" (+0.5 hearth/d, BK:VillageDecreeManager.cs:47) uruchamia tylko konsola** (BK:BannerKingsCheats.cs:4307 to jedyne wywolanie `StartDecree`) - to nie przeciek AI.

---

## 2. Reguly (jedna na zjawisko: skad czlowiek, dokad czlowiek)

**Ksiega (R0).** Jedna ksiega na region (warownia + jej wsie, ta sama geografia co pula wyrzutkow - K:OutlawLaw.cs:219-220).
- **Konta ludzi** (U1): **DOM** (wsie: hearth x k_v; miasto: stan w zapisie), **SLUZBA** (pochodzenie przy partii, zalodze, milicji, karawanie, taborze, bandzie),
  **JENCY** (pochodzenie przy rosterze jencow partii i lochu osady), **WYRZUTKI** (pula OutlawLaw), **UCHODZCY** (konto wsi pochodzenia + miejsce pobytu), **NIEWOLNI**
  (w osadzie niewoli, region pochodzenia zapamietany), **WETERANI** (pula karczmy miasta - E4/10) i licznik trwaly **POLEGLI**.
  **Konto nie-ludzi: CIALA** (pula Innych; zasilana tylko z R3 w miejscach R13; nigdy nie wraca do ludzi - wight nie jest czlowiekiem z niczego).
- **Rownanie kontrolne regionu** (DOK krok 5, z kontami U1):
  `dom + sluzba + jency + wyrzutki + uchodzcy + niewolni + weterani = start + narodziny - zgony +- migracja`;
  zgony = polegli (bitwa, zaraza, stracenie, spustoszenie, glod, odbici przez Innych) + smierc naturalna kont bez narodzin (uchodzcy, niewolni);
  migracja = R2 (do miast), R10 (uchodzcy z glodu w innym regionie), R19 (konwoje BK), przenosiny miedzy regionami.
- **Wlasciciel ksiegi (U23):** nowa klasa `PeopleBook` (Armoury) - jedyne miejsce stanu (`arm_people`) i jedyna droga przesuniecia:
  `Move(region, z_konta, do_konta, n, przyczyna)`, `Born(region, n)`, `Die(region, konto, n, przyczyna)`. Moduly (OutlawLaw, LosersFlee, ClanBudget, PrisonerLaw, Devastation, Levy,
  OthersRise...) tylko ja wolaja; rownanie, linia "Ludzie (ksiega)" i liczniki "z niczego / w nicosc" licza sie w niej. Hearth wsi pisze tylko `PeopleUnit.Shift` (108), wolany z `Move`.
  PeopleLedger zostaje logiem i czyta `PeopleBook` (jego kontrakt "niczego nie zmienia w grze", K:PeopleLedger.cs:14-15, zostaje). `PeopleBook` powstaje w E4/2 (konto CIALA),
  w E4/3 dostaje DOM i WYRZUTKI, kolejne konta - kazde w swojej paczce.
- **Jednostka i k (U3):** k zamrozone **na osade** przy kalibracji (wies: ludzi na hearth, miasto: stan) i zapisane w `arm_population` razem z kultura z dnia kalibracji.
  Zmiana kultury osady - BK przy asymilacji >= 0.55 (BK Managers.Populations\CultureData.cs:200-206) i przy wczytaniu (BK Behaviours\BKManagerBehavior.cs:171-179), ROT przy
  zdobyciu przez Innych (dec ROTOthers.cs:732, :749; powrot :649, :669) - nie zmienia ani liczby ludzi, ani wagi czlowieka w hearth. Reguly geografii i demografii (prog wolnych rak R5,
  zima R11, tabela krain) czytaja kulture z dnia kalibracji (region); prawo niewoli I1/I1b - biezaca kulture osady, jak dotad (K:PrisonerLaw.cs:47-48; rozdz. 8 U3).
  1 czlowiek = 1/k_v hearth (108); miasto - 1 czlowiek stanu.
- **Trzy punkty wejscia do DOM (U2)** - kazdy przez `PeopleBook.Move` z regionem pochodzenia i przyczyna:
  (1) `LosersFlee.SendHome` (K:LosersFlee.cs:387-450: rozbici H3, jency I1, pula -> dom; tabor :413, `ToBk` :464-495, prosci przez `ToCommon` :452-462);
  (2) `ClanBudget.ToVillage` (K:ClanBudget.cs:796-806; wolany z `ReleaseFrom` :711-738 - zwolnienia 166 - i z `ToVillagePop` :791-794 - przepelnienie W4c `OverflowHome.SendHome`
  K:OverflowHome.cs:127-154, rycerze 179 `GentryService` K:GentryService.cs:647);
  (3) `OutlawLaw.ReturnHome` (K:OutlawLaw.cs:282-290; z `ToCommon` i z powrotu z puli :777).
  Do NIEWOLNI: `LosersFlee.AddSlaves` (K:LosersFlee.cs:580-) i lustro BK w `BkSeal34` (K:BkSeal34.cs:31, :211). Na Mur: `LosersFlee.ToWall`.
  `CartTownExit.SendHome` to tylko rozkaz ruchu wozu (K:CartTownExit.cs:216-226) - nie przenosi ludzi.
- **Klasy BK/BEE nie sa ludzmi** - to model checi i skladu: BK `DeduceManpower` dalej zdejmuje klase przy werbunku (sklad: klasa, notabl), a ILOSC rekrutow trzyma ksiega (R5).
  Linia "Pobor:" mowi, ktory hamulec zadzialal - ksiega (wolne rece) czy pula BK (U17); dopasowanie przyrostu BEE do ksiegi - odlozone z progiem liczbowym (rozdz. 7).

| # | Zjawisko | Skad czlowiek -> dokad (regula) | Dzis | Paczka |
|---|---|---|---|---|
| R1 | narodziny i zgony naturalne (wies) | stopa roczna g = 0.5 + 0.6 W - 0.6 U - 1.5 H [% rocznie], korytarz [-1.3; +0.8]; W - dobrobyt warowni wobec mediany krainy, U - bezprawie + wojna + spalone wsie, H - glod okregu (od spichlerza); premie gry/BK tylko mnoza dodatnie g (x0.5-2); rosnie tylko wies w stanie Normal; uchodzcy, sluzba, jency i niewolni bez narodzin; uchodzcy -1.3%/rok (HR 2.5) | stala +4/+1.2/+0.2 hearth z niczego | E4/4 (H od E4/6) |
| R2 | miasto (stan) | narodziny g_wsi - 1 pkt (miasta sredniowieczne mialy wiecej zgonow niz urodzen - DOK krok 9) + migracja ze wsi regionu wyrownujaca ubytek, gdy region nie gloduje | dobrobyt x k | E4/4 (stan), E4/8 (migracja) |
| R3 | smierc w wojnie | polegly w bitwie, zmarly z zarazy obozowej, stracony, zabity przy spustoszeniu, zmarly z glodu, jeniec odbity przez Innych (R13) = ubytek trwaly regionu POCHODZENIA (partia: proporcjonalnie do skladu pochodzenia) | z PL nie ubywa nikt | E4/7 (E4/5, E4/6 dla rabunku i glodu; E4/2 dla Innych) |
| R4 | wejscie do sluzby | kazdy zolnierz partii, zalogi, milicji, karawany, taboru i bandy jest czlowiekiem z: domu (wies werbunku -1/k; miasto -1 stanu), puli wyrzutkow, jencow (zmiana stron po bitwie - G MapEvent.cs:1529-1567 - albo werbunek z lochu: jeniec niesie pochodzenie, R20), puli weteranow (karczma) albo puli cial (Inni). Punkt zdjecia ochotnika: BK `DeduceManpower` (wspolny dla AI i gracza - U7). Szablon gry = pobor z regionu siedziby; region bez wolnych rak -> mniej ludzi | ok. 1 000/d z niczego | E4/7, E4/8 |
| R5 | wolne rece | **jedna definicja "pod bronia z regionu" (U19)** - `PeopleBook.UnderArms(region)`, ta sama w linii, w hamulcu i w R9(b): S = SLUZBA z regionu (partie, zalogi, milicje, karawany, bandy; bez taborow wsi i lodzi - to praca wsi) + JENCY z regionu u wroga; K_m = polegli mezczyzni regionu od siewu + 0.27 x zmarli cywile - 0.27 x narodziny od siewu (nie mniej niz 0); **a = (S + K_m) / (0.27 x L_ref)**, L_ref = ludzie regionu w dniu siewu (dom + sluzba + jency). Prog f: 20% mezczyzn (domyslnie); Zelazne Wyspy f = 0.20 + min(0.20, niewolni / (0.27 x ludzie)) (RK 2.3 pkt 6, RK:401 - "ok. 40%, pracuja niewolni", SP:362); Wolni Ludzie 1.0 (pula = polowa doroslych); Straz bez progu (rekrut z calego Westeros); Dar tylko do plonu; okno zniw ok. 8 tygodni (dni 196-252) w latach lata i jesieni - f x 0.5, w latach zimy bez okna. ILOSC: nowy rekrut tylko przy a < f (hamulec - pytanie 2); SKLAD: BK (klasa, notabl) | stala 0.20 (K:PeopleLedger.cs:32), bez okna | E4/7 |
| R6 | wyjscie ze sluzby | rozbity, zwolniony (budzet rodu 166, pokoj), wypuszczony jeniec, rozwiazana partia (takze tabor i lodz rozwiazane przez gre - R18) -> DOM regionu pochodzenia przez jeden z trzech punktow R0 (+1/k); dezerter -> pula wyrzutkow regionu pochodzenia; jeniec wedlug I1 (Westeros do domu, przestepcy na Mur, krainy z niewola - niewolnik osady); Straz nie jest zwalniana (R14) | czesc do BK, czesc "z szablonu" w nicosc | E4/3 (jednostka), E4/7 (pochodzenie) |
| R7 | wyrzutki | wejscie z domu (nedza, wojna, ucieczka przy spustoszeniu) -1/k, powrot +1/k; siew i naplyw liczone od ludnosci wsi (108 wariant C); siew startowy - jednorazowy, w bilansie otwarcia, nie w ruchu doby (U10); wight nigdy | 0.5 hearth/czl. | E4/3 |
| R8 | spustoszenie (rabunek, zerowanie armii) | trafieni = (pulap 90% okregu - juz spustoszeni) x (1 - e^(-R x osobodni / pulap)), R = 0.5 rabunek / 0.2 zerowanie; z trafionych: 2% ginie (5% gdy najezdzca z kultury z niewola), **10% pojmani - tylko najezdzca z kultury z niewola (HR 2.3; ci sami ludzie, nie dodatkowi - U14)**, 2% + 3% x niebezpieczenstwo do lasu, reszta uchodzcy (HR 2.8); kontrola: trafieni = zabici + pojmani + las + uchodzcy. **Powrot (U13):** nikt nie wraca, gdy w regionie w ostatnich 30 dobach byl rabunek albo zerowanie (HR 2.5); potem dziennie `reszta uchodzcow wsi / max(1, 364 - dni od ostatniego rabunku w regionie)` - wszyscy zywi uchodzcy w domu najpozniej 364 doby po ostatnim rabunku (Jeff 09.10 "odbudowa spalonej wioski: 1 rok", SP:760; pytanie 3); uchodzcy -1.3%/rok (HR 2.5); stan Looted nie zeruje produkcji okregu | -39.7% hearth za rabunek, +0.5 ponizej 40 z niczego | E4/5 |
| R9 | plon | (a) spustoszenie: towar i zywnosc wsi x (dom / (dom + uchodzcy tej wsi))^1.0 - tylko uchodzcy, cale gospodarstwa z ziemia (HR 2.6: e = 1.0, bo z ludzmi gina woly i ziarno; Jeff 07.10 "spladrowana wies produkuje wedlug tego, co zostalo", SP:459-460); progi poziomu 200/600 od dom + uchodzcy; (b) **rece (U4)**: L = 1 przy a <= f, inaczej L = ((1 - a) / (1 - f))^0.5, a z R5 (w mezczyznach), a <= 0.9, L >= 0.5 (DOK:53, DOK krok 6 poprawka krytyka :615; e = 0.5: -10% rak = -5% rolnictwa przy calym kapitale, DOK:52); (a) i (b) mnoza sie w jednym czynniku `MaterialLaw.ProdPostfix` - bez podwojnego liczenia, bo (a) liczy tylko uchodzcow, (b) tylko sluzbe i zmarlych | progi 200/600, Looted = 0 przez 8-17 dob; pobor i smierc bez skutku | E4/5 (a), E4/7 (b) |
| R10 | glod | spichlerz okregu w racjach, napelniany prawdziwa produkcja zywnosci wsi (zwykly rok ok. 1.2 x potrzeby); glod = spichlerz pusty i plon < potrzeba; zgony rocznie 70% x ((u - 0.10)/0.90)^2; **tylu, ilu umiera, rusza w droge -> UCHODZCY wsi pochodzenia z miejscem pobytu w najblizszym regionie tego samego (albo niewrogiego) krolestwa z zapasem > 180 dni; brak takiego - zostaja (GL 2.6; U15a)**; powrot jak R8, rok liczony od dnia, w ktorym spichlerz wsi pochodzenia ma >= 90 dni (GL 2.6); warownia przy Food 0 - gina tez mieszczanie | 0 glodnych warowni w 458 dobach | E4/6 |
| R11 | zima | jedno zrodlo (WinterBite); ciecie plonu i dodatek jedzenia = baza x czynnik krainy x gradient polnocy: Wyspy Letnie 0, cieple (Dorne, Qarth, Zatoka, Lys, Tyrosh, Myr, Volantis, Valyria) 0.4, reszta 1.0 (A02 B2); **kraina = kultura REGIONU z dnia kalibracji; partia - region pod partia (`OutlawLaw.RegionAt`, raz na dobe na partie) (U18)**; kon je w zimie pol paszy BK, chyba ze stoi na stepie albo BK juz liczy snieg/pustynie | gradient tylko wedlug Y mapy | E4/1 |
| R12 | zaraza | tylko oblezenia (CampFever); zmarli -> R3 (liczeni po kazdym `Strike` - partia, frakcja, pochodzenie; U9); cywile oblezonej warowni tylko gloduja (R10), nie choruja (decyzja projektu, GL 2.9) | zgony poza ksiega | E4/7 |
| R13 | Inni | wight tylko z poleglego w bitwie z udzialem Innych za Murem albo przy Murze (region kultury nightswatch) i z jenca Innych - **takze z jenca odbitego pokonanym (G MapEvent.cs:1529-1567: dzis wchodzi do bandy zywy; U6)**: kazdy taki czlowiek = polegly regionu pochodzenia (R3), wight tylko za Murem / przy Murze, reszta spalona (Jeff 07.10 pyt. 1-2: "pojmany i odbity jeniec to polegly swojego regionu i cialo dla Innych, tylko za Murem i przy Murze", "0% cial niespalonych", SP:435-437); bitwy zywych z zywymi daja 0 cial; nowa banda z puli CIALA (616 dawnych umarlych na start), pusta pula = sam wodz; rajd i przejecie osady - tylko zabici | nekromancja z zywych na starcie bitwy, odbici jency zywi w bandzie, rajd Hearth/2, szablon 53 + wodz | E4/2 |
| R14 | Nocna Straz | 3% puli wyrzutkow kazdego regionu Westeros dziennie ("przywdzial czern") + z ludnosci Westeros proporcjonalnie do ludzi; przestepcy z I1 (przyjmowani zawsze - wyrok); dozywotnio: **zwolnienia 166 dla Strazy = 0 (dzis K:ClanBudget.cs:633-643 zwalnia nadmiar puli do wsi przez ReleaseFrom; U5)** - nadmiar ponad pulap zatrzymuje tylko blokada werbunku i przyrostu zalog (K:ClanBudget.cs:1051-1079) i znika przez smierc; 3% puli i ludnosc tylko ponizej pulapu; polegly -> R3 regionu pochodzenia; dezerter -> pula wyrzutkow Polnocy; nie pali wsi (H1); jeden pulap Strazy (166) | pulap ludzi ze zwolnieniami, rekrut jak u innych | E4/9 |
| R15 | niewola | tylko kultury kanonu (bez Pentos i Lorath; Qarth i Qohor z niewola - I1b); sprzedany jeniec = NIEWOLNI osady (region pochodzenia zapamietany); jency z rabunku = pojmani z R8 (jedno zdjecie z wsi; BK `ExecuteCapture` daje dokladnie te liczbe, gracz tak samo - U14, U22); w regionie pochodzenia to ubytek "uprowadzeni", nie polegli (RK:399-400); Zelazne Wyspy 27 tys. niewolnych na start = czesc 500 tys. tabeli wysp (bilans otwarcia, RK:348-349), ubytek 5%/rok = smierc naturalna niewolnych (konto bez narodzin) (RK:350; U15b) | BK daje jencow z niczego | E4/9 |
| R16 | weterani | najemnik z karczmy tylko z puli WETERANI (zwolnieni, rozbici, wypuszczeni jency - z bronia, zbroja i koniem); niewynajety przez 120 dob wraca do domu (R6) - jedna pora roku czekania (U15c); start: pula starych zolnierzy z ludnosci miast | 48 657 najemnikow z niczego | E4/10 |
| R17 | podloga 10 hearth (gra Village.cs:231-234; BK NotablePatches.cs:218-221) | to, co podloga dopisze, schodzi najpierw z uchodzcow tej wsi (wracaja), potem z puli wyrzutkow regionu - nic "z niczego"; oczekiwane 0 (pulap spustoszenia 90%, nasze podlogi 50 - K:OutlawLaw.cs:272, :564) (U15d) | z niczego (pojedyncze wsie) | E4/0 pomiar, E4/4 |
| R18 | tabor i lodz rozwiazane przez gre bez bitwy (G VillagerCampaignBehavior.cs:253-259 - 0 zdrowych, :268-272 - sierota; dec2 Fishing.cs:344-349 - nadmiar lodzi) | caly sklad (zdrowi i ranni) -> DOM wsi (+n/k) (U15e) | w nicosc | E4/0 pomiar, E4/3 |
| R19 | konwoje i wedrowcy BK (`BannerKings.Components.PopulationPartyComponent`, K:CirculationWindows.cs:1781) | z DOM osady wyjazdu do DOM osady przyjazdu (migracja); rozbici -> R6 (U15f) | z niczego (C Z6 "inne" 32.5/d) | E4/0 pomiar, E4/8 |
| R20 | przenosiny ludzi miedzy rosterami | partia <-> zaloga (okna 169c `LeaveTroopsToGarrison` / `TakeTroopsFromGarrison`, K:Measure169c.cs:94-95), ekran druzyny, laczenie partii, jency wcieleni po bitwie i z lochu - udzial pochodzenia idzie z ludzmi; jeniec niesie pochodzenie; `LawHomePos` (K:LosersFlee.cs:404) tylko dla ludzi bez pochodzenia (siew) (U16) | pochodzenia brak | E4/7 |

Wspolne zasady (jak w etapie 3): jedna regula dla gracza i AI; kazde przesuniecie czlowieka przez `PeopleBook.Move` z licznikiem (z niczego / w nicosc = 0 poza
nazwanymi: start, narodziny, smierc); kwota zapisana = to, co naprawde zeszlo (migawka przed/po, nie wynik modelu); wyjatki per obiekt - licznik potkniec, bez
globalnych wylacznikow (CLAUDE.md 7); **kazde wywolanie pomiaru albo ksiegi w zywym kodzie ma wlasny try i nigdy nie stoi miedzy zdjeciem a dopisaniem (U24)**;
kazda paczka ma wlasny wylacznik w MCM (wylaczony = stan sprzed). Zloto: etap 4 nie rusza zadnego przeplywu zlota (wyjatek: weterani - zold konnego, juz w 160).

**Pogodzone bez pytania Jeffa** (B 4, KR):
- PLAN 4.7 "jeniec zostaje mieszczaninem" - nieaktualne; obowiazuje I1 z 09.10 (w grze, CHANGELOG 31m :2650): Westeros do domu, przestepcy na Mur, krainy z niewola - niewolnik.
- 174.3 `TablePeopleOf` (stala suma krain, czyta tylko K:WorkshopLaw.cs:815) i 109 "miasta jako stan" to dwie definicje ludzi miasta. **Jedna:** od E4/4 `TablePeopleOf`
  zwraca stan ksiegi (`PeopleOf`); `WorkshopLaw.TownHands` bierze max(dawny wzor, z ludzi) (K:WorkshopLaw.cs:775-781), wiec nikt nie traci rak w dniu przejscia.
- Wolni Ludzie 300 tys. - wiersz tabeli w E4/3 (K:PopulationLaw.cs:52; nowa kampania, bez migracji zapisu - SP:888-890).
- Prog wolnych rak wedlug krain zamiast jednej stalej 0.20 (K:PeopleLedger.cs:32) - E4/7; Zelazne Wyspy wzorem RK (prog rosnie z liczba niewolnych).
- Straz 3% (Jeff) zamiast 5% (RK:208).
- Elastycznosc plonu 1.0 po spustoszeniu (HR) zamiast 0.5 (113) - bo zarazem znika zerowa produkcja w stanie Looted (decyzja Jeffa 07.10); dla rak (pobor, smierc) 0.5 (DOK).
- Wyspy Letnie: pulap gestosci k wsi 650 ludzi na hearth (najwyzsze k innej krainy: Qarth 629, log:325) - 0.75 -> ok. 0.11 mln [S: 3 wsie x 50 hearth x 650 / 0.9];
  nadwyzka tabeli to "poza mapa" (A02/AN 12: decyzja projektu). Yi Ti (523) bez zmian.
- **Jency gracza z rabunku (dawne pytanie P2 - U22):** jedna regula gracz/AI (zasada stala Jeffa) + Jeff 07.10 pkt 6a "jency z rabunku biora tylko kultury z niewolnictwem
  w swiecie Martina, ludzie zdjeci z napadnietej wsi - TAK" (SP:444-445). Gracz bierze jencow tylko, gdy jego krolestwo zna niewole (kanon), i sa to pojmani z R8; przelacznik BK
  "nie bierz" (BK BKRaidCaptureBehavior.cs:235) zostaje - to wybor, zeby nie brac. RK:397 (06.10) zostawial to Jeffowi przed jego decyzja 07.10 pkt 6a.
- **"Odbudowa 1 rok" (SP:760)** = linia R8 (powrot liniowy do 364 doby po ostatnim rabunku); wioski poboczne (E4/11) tak samo.

---

## 3. PACZKI etapu 4 w kolejnosci wgrywania

Kolejnosc: najpierw to, co niezalezne od ksiegi (pomiar, zima, Inni), potem rdzen ksiegi w kolejnosci dokumentow (jednostka -> przyrost -> spustoszenie ->
spichlerz -> pobor -> wojsko z ludzi), na koncu krainy kanonu, weterani i wioski. **Dlaczego 4.3 nie jest osobna paczka przed ksiega:** bez jednostki 1/k
odplywy hearth (tabory, wyrzutki, zerowanie armii) zostaja w skali 0.5 hearth za czlowieka - dzis ok. 100-200 hearth/d [S: stala gry 723/351 minus zmierzone
netto +625/+153, C 3] = ok. 17-20% hearth rocznie; zatrzymanie samej stalej skurczyloby wsie o 6-7% w 120 dobach. Po E4/3 te odplywy spadaja ok. 100 razy,
wiec "wies nie rosnie sama" wchodzi bezpiecznie razem z przyrostem g w E4/4 (4.2 i 4.3 = jedna paczka; informacja dla Jeffa - rozdz. 6). **4.12 przed 4.11** (PLAN:137
mowi "zalezy od 4.11"): zima wedlug krain nie czyta spichlerza, a spichlerz (E4/6) potrzebuje ciecia zimy z E4/1 - kolejnosc odwrocona celowo (U28).

| Paczka | Nr | PLAN | Co | Zalezy od | Wiel. | NK | Test |
|---|---|---|---|---|---|---|---|
| **E4/0** | 188 | 4.15, 4.13 (log), 4.18 (pomiar), 1.14 | pomiar ludzi, jencow i zimy - gra bez zmian; nasluchy PeopleLedger rozszerzone; proba zimy | - | S | nie | 364 d (progi w d120) + 120 d zimy + zapis 9 d |
| E4/1 | 189 | 4.12 | zima wedlug krain (wedlug regionu), Skagos owce, konie jedza zima | E4/0 (bieg zimowy) | S | nie | 120 d zimy |
| E4/2 | 190 | 4.18 | Inni tylko z poleglych za Murem / przy Murze, z jencow i z odbitych jencow; banda z puli CIALA; `PeopleBook` (konto CIALA) | E4/0 (region) | S | czesciowo | 120 d |
| E4/3 | 108n | 4.1 (cz. 1), 4.10, 4.17 (tabela) | jedna jednostka 1/k (108) na osade; trzy punkty powrotu przez `PeopleBook`; tabory/lodzie rozwiazane wracaja; tabela krain | E4/0, E4/2 | D | tabela tak | 120 d |
| E4/4 | 109n | 4.1 (cz. 2), 4.2, 4.3 | przyrost naturalny g (109) + miasta jako stan; jedna definicja ludzi miasta; podloga 10 bez ludzi z niczego | E4/3 | D | czesciowo | 120 d |
| E4/5 | 113n | 4.4 | spustoszenie (113, parametry HR, pojmani), powrot do roku, plon od uchodzcow, Looted bez zera | E4/4 | D | czesciowo | 120 d |
| E4/6 | 191 | 4.11 | spichlerz okregu, glod i uchodzcy z glodu (dokad - GL 2.6), H w przyroscie | E4/5, E4/1 | D | czesciowo | 120 d + 120 d zimy |
| E4/7 | 164b-1 | 4.8, 4.13, 4.17 (prog) | pochodzenie (partie, jency, przenosiny), pobor z wsi (BK `DeduceManpower`), polegly trwale, wolne rece w mezczyznach, okno zniw; ksiega w menu osady | E4/5 | D | siew przy wlaczeniu | 120 d |
| E4/8 | 164b-2 | 4.7, 4.9 | zalogi, milicje, nowe druzyny, karawany, konwoje BK z ludzi; migracja do miast | E4/7 | D | nie | 120 d |
| E4/9 | 192 | 4.15-4.17, 4.19 | Straz (dozywotnio, bez zwolnien), Wolni Ludzie, niewola, jency z rabunku (pojmani z R8), dluznik rozpuszcza poczet | E4/7, E4/8 | D | start Zelaznych i Wolnych | 120 d |
| E4/10 | 167 | 4.14 | weterani: karczma z prawdziwych zolnierzy; niewynajeci wracaja | E4/7 | S | karczmy na start | 120 d |
| E4/11 | 193 | 4.5, 4.6 | ksiega wiosek pobocznych, plon okregu, odbudowa wioski w rok | E4/5, E4/6 | D | tak | 120 d + koniec etapu |

Numery: 188-193 sa wolne (CHANGELOG 31m i STAN-PRAC; "194" wystepuje tylko jako odnosnik w tabeli owiec CHANGELOG 31m:3811 - pominiety). 108/109/113, 164b i 167 to numery
z planu (n = nowa wersja na dzisiejszym kodzie). Kazda paczka = osobny commit + wpis CHANGELOG + wlasny wylacznik; standard kazdego wgrania (PLAN etap 1):
autotest 40 dob + zapis z doby 362 (9 dob) - 0 bledow naszych modow, potem test 120 dob z progami paczki i progami etapow 1-3 (rozdz. 4).

---

### E4/0 - Pomiar ludzi, jencow i zimy (188, tylko log) - S - GOTOWE DO KODU (po poprawkach v2)

**Cel.** Zmierzyc wszystko, czego brakuje, zanim cokolwiek zmienimy w ludziach (luki C 13, KR): budzet hearth wedlug przyczyny, dobrobyt miast, przyrost milicji
z modelu, partie tworzone i znikajace z ludzmi (takze tabory i lodzie rozwiazane przez gre, konwoje BK), **jency - skad i dokad (U1)**, Inni wedlug miejsca i z odbitych
jencow (U6), zmarli z zarazy wedlug frakcji (U9), **zmiany kultury osad (U3)**, podloga 10 (U15d), tabela krain i tabela strat Strazy i Wolnych Ludzi (4.15, Jeff 07.10:
"chce tabeli strat i przyrostu Strazy i Wolnych Ludzi", SP:439), ciecie plonu zima wedlug krain - i **pierwsza proba zimy** (PLAN 1.14, zgoda Jeffa 09.10 J, SP:773).
Gra bez zmian: zadna latka nie zwraca false, nie zmienia wyniku, parametrow, hearth, ludzi ani zlota.
**Zasada pomiaru (U8):** nowe okna tylko tam, gdzie pomiaru dzis nie ma (W1-W7, W9-W11). To, co juz licza PeopleLedger (nasluchy bitew, werbunku, dezercji - K:PeopleLedger.cs:111-179;
wiersze regionow :250-276; CSV z hearth :34-37), OutlawLaw (`_inRaid`, `_inMisery`, `_outReturn` - K:OutlawLaw.cs:82, :568, :708, :778), LosersFlee (`_toVillageHearth`,
`_toCommonHearth` - K:LosersFlee.cs:414, :458) i 169c (zalogi i milicja - K:Measure169c.cs:281-297), jest **czytane** (przekazanie licznika tuz przed jego zerowaniem), nie liczone drugi raz.

**Baza kodu:** `w-toku/e3-31m` (a939cd5). Paczka nie zalezy od 3.1+3.3 - te same zmiany nakladaja sie tez na galaz gry Jeffa (WGRANIE 26).

#### Pliki
| Plik | Zmiana |
|---|---|
| NOWY `Armoury/src/Measure40.cs` (ok. 600 linii) | wzorem `Measure169c` (K:Measure169c.cs:20-130: `Wire`, `Stumble`, `Reset`, liczniki sesji bez zapisu): `ApplyAll(Harmony)` - okna W1-W7, W10, W11, **wpinane zawsze, liczone tylko przy On (U26)**; `Daily()`; `Reset()`; przekazania `TakeOutlaw(raid, misery, back, per)`, `TakeFlee(toVillageH, toCommonH)`, `TakeSeed(men)` (U10); `NoteHearth(src, delta, Settlement)` (tylko ScorchedEarth - tam licznika nie ma), `NoteScar(d)`, `NoteDisease(MobileParty, int zmarli)` (U9); migawka kultury osad z pierwszej doby sesji `CultureAtStart` (U3); **kazde Take/Note z wlasnym try i licznikiem potkniec (U24)**; `[ThreadStatic] InVillageTick` |
| `SubModuleMain.cs` | `Measure40.ApplyAll(_harmony)` we wlasnym try zaraz po `ScorchedEarth.ApplyAll` (K:SubModuleMain.cs:133), **bez warunku na klucz** (ScorchedEarth.ApplyAll nie wpina przy wylaczonym kluczu - K:ScorchedEarth.cs:120-121 - wlaczenie w MCM nic by nie dalo do restartu; U26) |
| `ArmouryBehavior.cs` | `Measure40.Reset()` w konstruktorze (K:ArmouryBehavior.cs:395); `Measure40.Daily()` w `OnDailyTick` zaraz PRZED `PeopleLedger.Daily()` (:1535), po `Measure169c.Daily()` (:1501); 3 nasluchy partii (`MobilePartyCreated`, `MobilePartyDestroyed`, `OnPartyRemovedEvent`) obok nasluchow PeopleLedger (:689-692) - do PeopleLedger, nie do nowej klasy (U8) |
| `PeopleLedger.cs` | istniejace nasluchy (:111-179) dostaja podzial wedlug frakcji: Straz (`CrownGifts.IsWatch`, K:CrownGifts.cs:35), Wolni Ludzie (krolestwo "freefolk", wzor K:Leaks310.cs:218), Inni (`Undead.Party`, K:Undead.cs:50); nowe nasluchy partii (linia "Partie"); linie "Krainy (4.15)" i "Straz i Wolni Ludzie (4.15)" z wierszy regionow (:250-276) zgrupowanych po kulturze z pierwszej doby sesji; `ludzie-regiony.csv` dostaje kolumny budzetu hearth (z Measure40), `gracz` (0/1 - warownia rodu gracza; U27) i `kultura_start` - bez nowych plikow CSV (U8) |
| `Measure169c.cs` | okno "Inni: nekromancja ROT" (:104-105, :678-679): prefiks liczy dodatkowo miejsce bitwy i jencow Innych przed `Clear`; linia "Wzrost Innych (169c)" (:681-702) dostaje podzial wedlug miejsca, "jency Innych skasowani" i "odbici jency wcieleni do bandy" (z W10); nowe okno W9 "Inni: rajd ROT"; linia "Zalogi" (:281-297) dostaje odcinek "milicja: z modelu w ticku +a (wsie b, miasta c, zamki e)" z Measure40 (U8 - bez osobnej linii Milicja); `PopulationLine` (:526) odklada niewolnych BK wedlug kultury (slownik do odczytu) |
| `CampFever.cs` | po kazdym `Strike` (:87 oblegajacy, :109 obroncy): `Measure40.NoteDisease(mp, stan przed - stan po)` - roster przed/po, wiec dokladnie zmarli (HitRandom moze nie trafic, K:CampFever.cs:165-185), z partia (frakcja, kultura); zamiast jednej linii po :117 z samymi sumami stron (U9) |
| `OutlawLaw.cs` | `Seed` (:292-308): po `Log.Info` `Measure40.TakeSeed(sum)` (siew osobno - U10); `Daily`: tuz przed zerowaniem (:882) `Measure40.TakeOutlaw(_inRaid, _inMisery, _outReturn, OutlawHearthPerMan)`. **W `TakeCommoners`, `ReturnHome`, `OnVillageLooted` - zadnej nowej linii** (U8, U24: w `TakeCommoners` wyjatek miedzy `v.Hearth -=` :275 a `Add` :278 zgubilby ludzi) |
| `LosersFlee.cs` | `Daily`: w `finally` przed `ClearDay()` (:1475) `Measure40.TakeFlee(_toVillageHearth, _toCommonHearth)` (we wlasnym try) - bez nowej linii przy :413 |
| `ScorchedEarth.cs` | :74 (zerowanie armii): `NoteHearth(SrcForage, nowy - stary, v.Settlement)` wewnatrz istniejacego try (tu licznika nie ma); `HearthScarPostfix` (:95-113; +0.5 :104, blizna :110): roznica `__result.ResultNumber` przed/po do `NoteScar` - tylko gdy `Measure40.InVillageTick`, wewnatrz istniejacego try |
| `VillageTakings.cs` | `HooksLine` (:466): `capOff` liczy tylko prefiksy spoza naszych Harmony id (wlasciciel bez przedrostka "com.jeff.") - inaczej nasz W1 dawalby "jest" takze bez BK (U25) |
| `WinterBite.cs` | NOWE `internal static float VillageCut(Village v)` - tylko odczyt, ten sam rachunek co `VillageProdPostfix` (:108-119). (109 dodawal identyczna metode - przy E4/4 NIE dublowac; po E4/1 ten sam czynnik regionu.) |
| `Settings.cs` + `McmSettings.cs` | 1 klucz (nizej), `python tools/gen_mcm.py` |
| `tools/sprawdz_logi.py` | grupa `--grupa etap4` (progi nizej; wzor parsera: `scratchpad\e4\parse_c.py`, `anal_c.py`; ksiega ludzi pod bronia + konto JENCY; krainy z `ludzie-regiony.csv` po `kultura_start`; zapas warowni wedlug U27) |

#### Okna pomiaru (prefiks `void` Priority.First + finalizer; stan w `__state`; kazde cialo w try, potkniecia liczone)
| # | Cel (dekompilacja) | Co mierzy |
|---|---|---|
| W1 | gra `Village.DailyTick()` (dec Village.cs:223-240; u nas zastepuje go prefiks BK `VillageDailyTicktPatch`, BK:NotablePatches.cs:207-225: `Hearth += HearthChange`, podloga 10 (:218-221), `Militia += MilitiaChange` (:222)). Prefiks First biegnie przed prefiksem BK (bool, zwraca false); finalizer biegnie zawsze | prefiks: hearth, milicja, wynik modelu `CalculateHearthChange` (wolany przez nas przed ustawieniem `InVillageTick`, wiec ScorchedEarth go nie liczy), potem `InVillageTick = true`; finalizer: `InVillageTick = false`, dH i dMil. Rozklad dH (U11): **stala gry** = baza pasma ze stanu sprzed (Normal: 4 / 1.2 / 0.2 ponizej 300 / 600 / wyzej; Looted: -1 - dec DefaultSettlementProsperityModel.cs:43-50) - w linii osobno "baza" i "z mnoznikami" = baza x (1 + `SumOfFactors`) (wynik = (baza + dodatki) x (1 + suma mnoznikow), pola publiczne takze bez opisow - G ExplainedNumber.cs:126, :154, :163; bez kosztu `includeDescriptions`); **nasze w modelu** (`NoteScar`); **podloga 10** = po - (przed + wynik), gdy > 0.0001 (wsie i hearth - R17); **reszta modelu** (perki, budynki, polityki, BK 400, BKROT 200, zadania) = wynik - stala z mnoznikami - nasze |
| W2 | gra `Town.DailyTick()` (internal; dec Town.cs:596-640; `Prosperity += ProsperityChange` :637, `Militia +=` :638) - miasta i zamki | dP (model, z RBL - czynny model dobrobytu to RBL, log:515), dMil, osobno miasta i zamki |
| W3 | gra `VillagerCampaignBehavior.AddVillagersToParty(MobileParty, int)` (private; e4dec Villager.cs:173-182, `hearth -= (n+1)/2` :179) | hearth wsi przed/po; ludzie = n po obcieciu do (int)hearth (:175-177) |
| W4 | gra `VillagerCampaignBehavior.CreateVillagerParty(Village)` (private; Villager.cs:184-189, :187) | hearth przed/po; ludzie = stan nowej partii (najnowsza partia taborowa tej wsi - jak w 108) |
| W5 | NavalDLC `FishingPartyCampaignBehavior.OnDailySettlementTick(Settlement)` (dec2 Fishing.cs:180-189, :186) i `TryReinforceParty(FishingPartyComponent)` (:207-230, :227) - typ po nazwie, brak = "BRAK" | lodzie rybackie: hearth i ludzie jak W3/W4 |
| W6 | gra `RaidEventComponent.Update(ref bool)` (internal override; dec2 Raid.cs:160-190; `Hearth -= krok x 0.5 x hearth` :174-175, zloto `4 zl x utracony hearth` z niczego :179-188) | hearth wsi przed/po kazdego kroku; liczba krokow i wsi; lup gracza/AI w zl (tylko informacja - ksiega pieniadza) |
| W7 | BK `VillageDecreeManager.OnDailyTickSettlement(Settlement)` (BK:...Economy.Layered\VillageDecreeManager.cs:27-57, `+0.5` :47) - typ po nazwie | hearth przed/po (oczekiwane 0: dekret tylko z konsoli, BK:BannerKingsCheats.cs:4307) |
| W8 | (w Measure169c) ROT `ROTOthersCampaignBehavior.OnMapEventEnded(MapEvent)` (dec ROTOthers.cs:587-616) - istniejace okno | dodatkowo: miejsce bitwy (`ROT.Misc.ROTUtilities.IsBeyondTheWall(Vec2)` przez AccessTools - wzor CS:NightKingCall.cs:296; "przy Murze" = region `OutlawLaw.RegionAt` kultury nightswatch; Westeros = kultura regionu w `PopulationLaw.WesterosCultures` K:PopulationLaw.cs:122; reszta Essos); jency Innych przed `RosterToReceiveLootPrisoners.Clear()` (:612-615) |
| W9 | (w Measure169c) ROT `ROTOthersCampaignBehavior.OnRaidCompleted(BattleSideEnum, RaidEventComponent)` (dec ROTOthers.cs:327-341, `Hearth/2 x mnoznik` :339) | wighty z rajdu (z niczego) i wies |
| **W10** (U1, U6) | gra `MapEvent.LootDefeatedPartyPrisoners(MBReadOnlyList<MapEventParty>, MBReadOnlyList<MapEventParty>)` i `MapEvent.CaptureDefeatedPartyMembers(` te same `)` (oba private void; G MapEvent.cs:1529, :1955; wolane po kolei :1423-1427, przed `OnMapEventEnded` :2079) | prefiks: suma szeregowych w `RosterToReceiveLootMembers` i `RosterToReceiveLootPrisoners` kazdej partii zwyciezcy (G MapEventParty.cs:75-95); finalizer: **odbici jency wcieleni do skladu** i **wzieci do lochu**, wedlug rodzaju zwyciezcy (partie rodow, zalogi, milicje, bandy, Inni osobno); usunieci bez odbiorcy (:1546) - z istniejacego licznika H3 `SrcFreed` (K:LosersFlee.cs:1105-1113), nie drugi raz |
| **W11** (U1) | BK `BKRaidCaptureBehavior.ExecuteCapture(MobileParty, Hero, Clan, Village, int, bool)` (private; BK Behaviours.Raids\BKRaidCaptureBehavior.cs:233-) - typ po nazwie | `PrisonRoster` napastnika przed/po = jency z rabunku BK (dzis z niczego, :285); trzecie okno void na tej metodzie (sa juz CirculationWindows :1788 i RealPayer :289 - zadne nie zmienia wyniku) |

Nasluchy: **zadnych nowych w Measure40 (U8)**. PeopleLedger: podzial frakcji w istniejacych czterech (zabici, ranni, rozbici wedlug frakcji; werbunek wedlug frakcji werbujacego;
dezercja wedlug frakcji) + `MobilePartyCreated`, `MobilePartyDestroyed` (z niszczycielem = w bitwie, bez = rozwiazana; banda rozwiazana - wighty w skladzie i lochu, ktore
`OutlawLaw.PartyDestroyed` dopisze do puli, K:OutlawLaw.cs:587-590 - "druga droga wightow" z C 8), `OnPartyRemovedEvent` (usuniete bez zniszczenia - wzor E3/0).

#### Linie logu (raz na dobe przy LogEnabled + PeopleMeasureEnabled)
- start: `Pomiar ludzi (E4/0): okna wpiete: tick wsi, tick warowni, tabor wsi - nowy, tabor wsi - uzupelnienie, lodz rybacka - nowa, lodz rybacka - uzupelnienie, rabunek (krok), dekret BK, jency po bitwie (odbici, wzieci), jency z rabunku BK; BRAK: -; wlasciciele latek na 11 celach: <cel: Harmony id, ...> (U25); dane 169c (niewolni BK, Inni, milicja w linii Zalogi): TAK/BRAK - ClanIncomeBookEnabled + StableD (U28); linie raz na dobe przy LogEnabled + PeopleMeasureEnabled (teraz: TAK).`
- `Hearth (E4/0): dzien N | wsie 571, hearth H (zmiana od wczoraj +d) = tick wsi +a [stala gry: baza +b0, z mnoznikami +b, blizna i uchodzcy ScorchedEarth +c, reszta modelu (perki, budynki, polityki, BK, BKROT, zadania) +e, podloga 10 +f w g wsiach] + dekret Growth BK +h - tabory wsi -j (nowe n1, uzupelnienia n2; ludzi L; gra: (n+1)/2) - lodzie rybackie -k (ludzi L2) - rabunek gry -m (krokow s, wsi w; lup z niczego Z zl) + nasze (z licznikow modulow): wyrzutki w las (bieda) -o1, spalona wies w las -o2, powrot do wsi (OutlawLaw :777 + LosersFlee ToCommon) +o3, rozbite tabory do swojej wsi +o4, zerowanie armii -o5 | siew puli wyrzutkow (tylko pierwsza doba kampanii, poza RESZTA) -s0 | zmiana kultury osad: n (lista: osada stara -> nowa), ludzie tabeli +-X (U3) | RESZTA r (zadania, pobor wymuszony i incydenty gracza, inwestycje BEE - BetterEconomy.all.cs:27705, konsola, inne mody) | wsie wedlug progow 200/600: x / y / z | w ludziach tabeli (hearth x k): tick +A, tabory -B, rabunek -C, nasze +-D, RESZTA R | czas T ms.`
- `Dobrobyt (E4/0): dzien N | miasta M: dobrobyt P (+d) = tick +a, reszta r (RBL ekspansja i glod - dec RealisticBannerlord...DemographicsExpansionBehavior.cs; zdobycie; zdarzenia) | w ludziach tabeli (dobrobyt x k): +A | zamki: dobrobyt Q (+d) = tick +a, reszta r.`
- milicja - odcinek linii `Zalogi: przyrost bez werbunku (169c)` (U8): `... | milicja X (+d) = z modelu w ticku +a (wsie b, miasta c, zamki e) - zabici f (z PeopleLedger) + reszta r (powrot wsi do Normal, przejecie osady, po oblezeniu, ROT).`
- `Partie (E4/0): dzien N | nowe: partie rodow a (A ludzi), karawany b (B), tabory wsi c (C), lodzie d (D), bandy e (E), konwoje BK g (G), inne f (F) | zniknely: rozbite w bitwie (ostatni stan), rozwiazane (w tym tabory i lodzie rozwiazane przez gre bez bitwy: n, ludzi zdrowych z, rannych r - R18), usuniete bez zniszczenia; w tym wighty z rozwiazanych band do puli W.` (PeopleLedger; migawka dobowa partii jak `OthersLine`, K:Measure169c.cs:681-702)
- **NOWA** `Jency (E4/0): dzien N | stan: w partiach P, w lochach L (zmiana +d) | po bitwach: wzieci do lochu +a (w tym przez Innych - skasowani przez ROT -i), odbici i wcieleni do skladu zwyciezcy -w (do partii rodow, zalog, milicji, band, do Innych -wi) | z rabunku BK +r (z niczego) | wypuszczeni, sprzedani, na Mur, straceni - linia "Prawo jenca (I1)"; bez odbiorcy - linia "Przegrani (H3)" SrcFreed.` (U1; rownanie jencow - narzedzie T7 z trzech linii, bez dotykania PrisonerLaw)
- `Krainy (4.15): dzien N | kraina: ludzie / mezczyzni (27%) / obciazenie PeopleLedger ((zaloga + wyrzutki + bandy) / mezczyzni, K:PeopleLedger.cs:312 - nazwane wprost; od E4/7 jedna definicja R5) = % [prog wedlug decyzji: 20, Zelazne 40, Wolni Ludzie 100, Straz -] / partie rodow krolestw tej kultury (krolestwo -> kultura przez obiekt `Kingdom.Culture`, nigdy przez StringId krolestwa - U19) / wyrzutki / niewolni BK (z 169c albo "brak danych 169c") / zabici dzis / zmarli z zarazy dzis | battania ...; ...` (PeopleLedger; kultura z pierwszej doby sesji; pelne dane - kolumny `ludzie-regiony.csv`, bez `krainy.csv` i `hearth-regiony.csv`).
- `Straz i Wolni Ludzie (4.15): dzien N | Nocna Straz: stan S (partie a, zalogi b), zmiana +d = zwerbowani r - zabici k - zmarli z zarazy z - dezercja e + nowe partie n + reszta s (zalogi z modelu, jency, "na Mur" z I1, wymiana, zwolnieni przez 166 do wsi) | od startu sesji: przybylo P, zgineli K, zdezerterowali E | Wolni Ludzie: to samo.` (PeopleLedger; "tabela strat i przyrostu" Jeffa)
- `Zima (E4/0): dzien N | pora P, dzien pory D z L | ciecie plonu wsi dzis (WinterBite.VillageCut): swiat srednio X%, krainy: battania a%, aserai b%, summer c%, ... | zywnosc wsi swiata dzis F (pierwsza doba sesji F0, zmiana %) | dodatek jedzenia partii zima srednio +Y% | warownie - linia "Zapasy warowni (T6)".` (poza zima krotko: pora i ile dni do konca)
- rozszerzona `Wzrost Innych (169c)`: `... nekromancja ROT +X (za Murem a, przy Murze b, Westeros c, Essos d; bitew n), jency Innych skasowani przez ROT j (w nicosc), odbici jency wcieleni do bandy +o (zywi ludzie - dzis w "reszcie"; U6), rajd ROT (Hearth/2 z niczego) +r w q rajdach ...`

#### Klucz Settings
`PeopleMeasureEnabled = true` (bool) w `Settings.cs` obok `PopulationScale` (K:Settings.cs:622); komentarz = podpowiedz MCM (po angielsku):
`// log only: daily lines accounting for every hearth, prosperity point and militia man gained or lost and why, prisoners taken, freed and changing sides, the realms' free hands, the Night's Watch and the Free Folk, the Others by place, and the bite of winter by realm - changes nothing in the game`
Uzasadnienie domyslnego "true": tylko log, koszt ok. 10-30 ms/dobe [S: 571 wsi x 2 wywolania modelu + migawka ok. 5 tys. partii, jak dzisiejsza petla PeopleLedger],
progi calego etapu 4 stoja na tych liniach. W `Armoury.json` Jeffa klucza nie ma (nowy) - wejdzie z kodu (przy kodzie `grep '"PeopleMeasureEnabled"'` - CLAUDE.md 7).

#### Proba zimy (bez kodu)
W `Armoury.json` **probnej kopii autotestu** (nie u Jeffa): `ClimateSummerDaysLeftMin` = `ClimateSummerDaysLeftMax` = 1, `ClimateFirstAutumnDaysMin` =
`ClimateFirstAutumnDaysMax` = 7. Lato konczy sie w 1. dobie (pierwszy odcinek nie ma minimum, K:WesterosClimate.cs:64-67), jesien 7 dob (minimum `Math.Max(7, ...)`
K:WesterosClimate.cs:120), zima od ok. 8. doby na 3-5 lat (K:WesterosClimate.cs:88-89). Zakresy MCM tych kluczy zaczynaja sie od 0 (tools\gen_mcm.py:3-4: `0..max(10, 4 x domyslna)`) - 1 i 7
mieszcza sie. Zablokowalo to juz raz zabezpieczenie (RAPORT-NOCNY-2026-10-09.md:60, "zmiana ustawien probnej kopii"); jesli znowu - Jeff dostaje
gotowe polecenie z tymi 4 kluczami (rozdz. 6, akcja).

#### Test
- **Bieg A** (nowa kampania, ustawienia jak u Jeffa) - **364 doby**: progi T1-T8 i T10 w d120; d121-364 = baza progow konca etapu (wojsko lordow, zalogi, ludnosc, zabici - U20).
  **Bieg B** (to samo + proba zimy, 120 dob): T1, T9. **Zapis z doby 362, 9 dob**: T1 (paczka nie ma kluczy zapisu).
- T1: 0 linii ERROR naszych modow; linia startowa `Pomiar ludzi (E4/0)` bez BRAK, z wlascicielami latek 11 celow; potkniecia 0 (najwyzej 5 w biegu, z nazwa miejsca).
- T2 (cel paczki): budzet hearth zamkniety - |RESZTA| <= 1 hearth/d w >= 95% dob i srednio <= 0.2 hearth/d; siew puli osobno, poza RESZTA (U10) (autotest gra bez gracza: zadania,
  pobor wymuszony i incydenty = 0; inwestycje BEE zamkniete kluczami K2 - "Obieg: BK i BetterEconomy" 0 zl, log:123830). Wiecej = nieznane zrodlo hearth -> nazwac PRZED E4/3.
- T3: oczekiwane wielkosci [S z C 3-4]: stala gry - **baza** ok. 720/d w d1-30, malejaca (U11: szacunek 723 liczono z samych pasm); tabory ok. -220 hearth/d (ok. 450 ludzi),
  powroty taborow (H3) ok. +165 hearth/d, bieda ok. -13 hearth/d, dekret BK 0, podloga 10 - pojedyncze wsie. Rzad wielkosci inny o ponad 3x = blad pomiaru (sprawdzic okno) albo przyczyna do opisania.
- T4: Inni - suma miejsc = nekromancja razem (kontrola 0); jency skasowani i odbici jency wcieleni policzeni (W8 = W10 dla bitew z Innymi, kontrola 0).
- T5: zmarli z zarazy w "Krainy (4.15)" = suma linii "CampFever:" tej doby (kontrola 0; NoteDisease liczy z rosteru, CampFever z zadanych - roznica = nietrafione HitRandom, nazwana).
- T6: Straz i Wolni Ludzie - reszta rownania stanu <= 10% obrotu (srednia 28 dob) albo nazwana w linii.
- T7 (narzedzie `--grupa etap4`): reszta niewyjasniona ksiegi ludzi pod bronia z kontem JENCY (zrodla - ujscia - zmiana stanu, metoda C 4) ponizej 100/d (dzis ok. +283); rownanie jencow
  (linie Jency, I1, H3) - reszta <= 10% obrotu; jesli nie - przed E4/7 dodatkowe okno (kandydaci: wymiana partia-zaloga, patrole BK, ucieczki jencow, okupy).
- T8: czas linii `Hearth` + `Partie` + `Jency` <= 30 ms/dobe; s/dobe biegu najwyzej +3% wobec ostatniego biegu 120 dob tego drzewa.
- T9 (bieg B, bez progu - dane do E4/1 i E4/6): "Klimat: ... nowa pora Winter" najpozniej w 10. dobie; linia `Zima (E4/0)` codziennie; zapisac: warownie glodne AI
  (najwiecej, pierwsza doba glodu), **zapas wedlug jednej definicji (U27)** - wszystkie warownie AI, dni = Food / ubytek dzienny, bez ubytku = 364 (w medianie), gracz osobno
  (narzedzie z `ludzie-regiony.csv`: `zywnosc`, `bilans_zywnosci`, `glod`, `gracz`) - mediana i minimum w d30/d60/d120; zywnosc wsi swiata -% wobec d1, ciecie plonu wedlug krain,
  glod partii (HungerLaw), "WinterSource" - pory BEE neutralne, RBL WinterFoodPenalty 0 (log:63). Dzisiejszy prog etapu - odczyt bazowy.
- T10 (odczyt, bez progu): zmiany kultury osad (oczekiwane 0; > 0 = dowod do E4/3), podloga 10 (wsie, hearth), konwoje BK (partie, ludzie), tabory i lodzie rozwiazane przez gre (ludzie) -
  liczby do E4/3, E4/4, E4/8.

#### Ryzyko / co sprawdzic (kontrola calosci, zasada 0)
1. **Wspolne cele latek:** `Village.DailyTick` - prefiks BK (bool) i odczyt listy latek w `VillageTakings.HooksLine` (K:VillageTakings.cs:461-473) - poprawiony, liczy tylko obce
   prefiksy (U25). `Town.DailyTick`, `RaidEventComponent.Update`, `AddVillagersToParty`, `CreateVillagerParty`, lodzie NavalDLC, `LootDefeatedPartyPrisoners`, `CaptureDefeatedPartyMembers` -
   dzis bez latek w K, CS, BK, BEE, BKROT (grep); **w pozostalych modach (RBL, ROT, Diplomacy, StrategicCampaignAI...) sprawdza to linia startowa** (`Harmony.GetPatchInfo` na 11 celach - U25).
   `ExecuteCapture` - trzecie okno void (CirculationWindows, RealPayer - oba bez zmiany wyniku). Okno W8 jest juz wpiete przez Measure169c - tylko rozszerzone.
2. **Przyszle latki na tych samych celach:** 108 (E4/3) lata W3-W5, 113 (E4/5) - W6, 109 (E4/4) - model hearth, ktory W1 czyta, E4/2 (f) - W10. Finalizery biegna po
   postfiksach, wiec okna mierza wynik koncowy - sa miara kazdej nastepnej paczki, a nie przeszkoda.
3. **Model wolany dwa razy** w W1 (nasz odczyt + BK): czysty rachunek, latki BK/BKROT/ScorchedEarth bez stanu (K:ScorchedEarth.cs:95-113 tylko `__result`); licznik blizny
   tylko w oknie, wiec bez podwojnego liczenia.
4. **Nic nie zmienia gry**: przy recenzji - zadna latka nie ma `bool`, `ref` na wyniku ani zapisu pola gry; `Take*` dostaja wartosci, ktore kod juz policzyl; **zadne wywolanie
   Measure40 nie stoi miedzy zdjeciem a dopisaniem ludzi** (U24 - lista miejsc: SubModuleMain, ArmouryBehavior, OutlawLaw x2, LosersFlee x1, ScorchedEarth x2, CampFever x2).
5. **Przekazanie licznikow:** OutlawLaw zeruje swoje liczniki po linii "Wyrzutki" (K:OutlawLaw.cs:882), LosersFlee w `ClearDay` (:1475); Measure40.Daily biegnie pozniej (po :1501),
   wiec liczniki przychodza przez `Take*` tuz przed zerowaniem. Miedzy tymi miejscami a migawka Measure40 w tej samej dobie hearth zmieniaja tylko okna W1-W7 (tick osad gry) - liczone wprost.
6. **Linie dlugie**: "Krainy" ma 30 pozycji (jak "Ludnosc:", log:123620) - pelne dane w CSV.
7. **Watki**: liczniki doby pod jedna blokada (tick wsi bywa liczony rownolegle - sprawdzic przy kodzie; 109 mial probe 8 watkow).
8. Bez zapisu - liczniki sesji od zera po wczytaniu (pierwsza doba sesji: "pierwsza doba sesji", jak 169c); migawka kultury osad tez z pierwszej doby sesji.
9. Okna wpiete zawsze (U26): koszt przy wylaczonym kluczu - jedno sprawdzenie flagi na wywolanie.

---

### E4/1 - Zima wedlug krain (189, PLAN 4.12) - S

- **Zakres:** (a) `WinterBite.Northness` (K:WinterBite.cs:49-55) -> czynnik krainy x gradient Y: Wyspy Letnie 0; aserai (Dorne), qartheen, ghiscari, lyseni, tyroshi, myrish,
  volantine, valyrian 0.4; reszta 1.0 (A02 B2, Jeff 09.10 "b, c TAK po probie zimy", SP:815; Dorne: 50% x 0.4 x 0.75 = 15% zamiast 37.5%). **Kraina = kultura regionu z dnia
  kalibracji (wies: region wsi; partia: region pod partia - `OutlawLaw.RegionAt`, wynik trzymany na dobe na partie, bo jedzenie partii liczy sie wiele razy na dobe) (U18)** - wies Dorne
  przejeta kulturowo przez Reach zostaje w klimacie Dorne. Jeden czynnik dla czterech skutkow zimy: jedzenie partii (:95-104), produkcja wsi (:108-119), zywnosc wsi (:121-133),
  spichlerz miasta (:135-146); `VillageCut` z E4/0 ten sam. Do E4/3 (zanim kultura kalibracji jest w zapisie) - kultura regionu z pierwszej doby sesji (jak E4/0).
  (b) Skagos "owce i kozy zamiast zboza" (ES:211 d): wiersz D.5 w tabeli T8 `VillageClimate.Table` (K:VillageClimate.cs:53-): `village_N1_3` (Ravikayr, ROT-Map settlements.xml:15305)
  `wheat_farm` -> `sheep_farm` (typu "koza" gra nie ma); reszta Skagos to rybacy, wielorybnicy, morsy - bez zmian. (c) Konie jedza zima (P10 Z4; ES:211 e "kon jak 2.5 czlowieka"): w zimie
  `-0.5 x BK CalculateAnimalFoodNeed(party, false) x czynnik krainy` (BK:BKPartyConsumptionModel.cs:45), **chyba ze** BK juz to liczy (snieg/zamiec pol paszy :208-211,
  pustynia :200-203) albo partia stoi na stepie (`TerrainType.Steppe` - wypas zimowy; jedna regula wedlug terenu, nie kultury - Dothrakowie na stepie bez doplaty).
- **Klucze:** `WinterByRealm` (true), `WinterWarmRealmFactor` 0.4, `WinterHorsesEat` (true), `WinterHorseFeedShare` 0.5; Skagos - istniejacy wylacznik T8 (`VillageClimateFix`).
- **Test:** bieg zimowy 120 dob wobec biegu B z E4/0: ciecie plonu Dorne/poludnie Essos 15%, Wyspy Letnie 0, Polnoc bez zmian (linia `Zima (E4/0)`); Ravikayr - "Mends"/"T8" linia
  z nowym wierszem; jedzenie partii konnych zima wyzsze; warownie glodne nie wiecej niz w biegu B; progi etapu 2 (glowy rodow < 5 000 <= 10, 0 bankructw) i etapu 1
  (Dothrakowie najwyzej 3 bankrutow) bez pogorszenia; czas doby +1% najwyzej (region partii z pamieci doby).
- **Ryzyka:** AI z duza jazda glodnieje zima (HungerLaw kupuje drozej) - dlatego step i snieg BK wylaczone; kolejnosc z `Rations` (ciecie -40% ma objac pasze, P10 Z4);
  T8 typ wsi bez zapisu i klasa BK wyrownywana przy kazdym starcie (K:VillageClimate.cs:36-48) - nie NK; ROT tekst Ravikayr mowi o zbozu (decyzja Jeffa ma pierwszenstwo);
  jedno zrodlo zimy (WinterSource, K:WinterSource.cs) - BEE i RBL nie dokladaja; `RegionAt` dla partii - koszt (227 wezlow) - stad pamiec doby.

### E4/2 - Inni tylko z poleglych (190, PLAN 4.18) - S

- **Zakres** (R13; RK rozdz. 9 pyt. 1-2 [D 07.10], H2 [D 07.10]; po T2b, ktore juz blokuje +100, +2/d i ochotnikow z mapy - CS:OthersGrowth.cs:96, :113, :126):
  (a) nekromancja: prefiks `bool` na `ROTOthersCampaignBehavior.OnMapEventEnded` (dec ROTOthers.cs:587-616) - wzor ROT (zdrowi przegranych NA STARCIE x udzial, po kazdej
  bitwie, takze przegranej, bez miejsca) zastapiony: wighty = polegli (`DiedInBattle`) strony przeciwnej + jency, ktorych ROT kasowal (:612-615 - "jency Innych wstaja"),
  x udzial partii w walce, do miejsca w partii (:605), mnoznik ROT najwyzej 1; tylko gdy bitwa za Murem albo przy Murze (W8 z E4/0); bitwy bez Innych - 0;
  (b) rajd (:327-341): 0 do E4/5, potem zabici przy spustoszeniu tej wsi; (c) nowa banda: szablon 53 + wodz -> z puli CIALA (616 dawnych umarlych w bilansie
  otwarcia + nadwyzka ponad miejsce w partiach; pusta = sam wodz; RK R4); (d) przejecie osady (`ChangeMilitiaCulture` :804-840): milicja zywych = polegli, wstaje tylko
  za Murem/przy Murze; (e) druga droga wightow do puli: `OutlawLaw.PartyDestroyed` (K:OutlawLaw.cs:589-590) pomija `Undead.Character`, jak W4b dla dezercji (:396);
  **(f) odbici jency (U6):** finalizer na `MapEvent.LootDefeatedPartyPrisoners` (G MapEvent.cs:1529-1567; ten sam cel co W10) - gdy zwyciezca to partia Innych: przybyli do skladu
  zywi (nie-wight) sa zdejmowani; kazdy = polegly regionu pochodzenia (R3: region miejsca bitwy do E4/7, potem pochodzenie jenca); wight (`AddToCounts(_wight)` do miejsca w partii) tylko
  za Murem / przy Murze, reszta - cialo spalone (Jeff 07.10 pyt. 1-2, SP:435-437). Finalizer biegnie przed `OnMapEventEnded` (G MapEvent.cs:1425 vs :2079), wiec nekromancja (a)
  widzi juz sklad bez zywych.
  **Miejsce kodu (rozstrzygniete - U23):** Armoury - nowy `OthersRise.cs` + `PeopleBook` (pierwsze konto: CIALA, sekcja zapisu `arm_people` "c:"); CrashScribe `OthersGrowth`
  zostaje wlascicielem blokad z T2b (+100, +2, ochotnicy z mapy) bez zmian; okno 169c W8 zostaje oknem pomiaru (void First biegnie mimo naszego `bool`).
- **Klucze:** `OthersRiseOnlyFromFallen` (true), `OthersRaidWights` (false), `OthersBandFromCorpsePool` (true), `OthersRescuedAreFallen` (true).
- **Test 120 dob:** nekromancja tylko w miejscach "za Murem / przy Murze" (Westeros i Essos = 0); wighty = polegli + jency (kontrola w linii); **odbici jency wcieleni do Innych zywi = 0
  (W10), wighty z odbitych tylko za Murem / przy Murze**; pula CIALA w linii; T2 (warownie od 728, Mur od 2184) bez zmian; stan Innych w d120 - odczyt (patrz ryzyko).
- **Ryzyka:** Inni slabna (dzis nekromancja 51/d z zywych na starcie, "reszta" -29.5/d) - fabula ROT (Zew Nocnego Krola CS:NightKingCall, Mur w 6. roku) moze nie ruszyc. Prog:
  jesli Inni spadna ponizej 300 przed doba 728 - pokretlo "ciala niespalone" (RK pyt. 2: rekomendacja byla 50%, Jeff wybral 0%) i pytanie do Jeffa dopiero wtedy.
  LosersFlee H3 pomija bitwy z Innymi (K:LosersFlee.cs:22-43) - bez zmian. `LootDefeatedPartyPrisoners` biegnie dla kazdej bitwy - warunek "zwyciezca Inni" sprawdzany pierwszy (tanio).
- **NK:** pula 616 tylko w nowej kampanii; stary zapis - pula od 0 (log), bandy w polu zostaja.

### E4/3 - Jedna jednostka, punkty powrotu i tabela krain (108n, PLAN 4.1 cz. 1, 4.10, 4.17 tabela) - D

- **Zakres:** przeniesienie 108 (`e548266`) commit po commicie (`git merge-file`, baza n119 - A 3.3: OutlawLaw 1 konflikt, ArmouryBehavior 6, SubModuleMain 2, Settings 4;
  `PeopleUnit.cs` wchodzi bez zmian): 1 czlowiek = 1/k_v hearth dla wyrzutkow (K:OutlawLaw.cs:259-290, :298, :556-570), taborow i lodzi (W3-W5: hearth sprzed - n/k),
  poboru wymuszonego gracza (n/k), zadan gracza (x 2/k), incydenty - licznik; kalibracja przed pierwszym zdjeciem hearth (`EnsureCalibrated`); wyrzutki liczone od ludnosci (wariant C).
  **k na osade (U3):** `arm_population` dostaje sekcje "v:" (osada -> k_v i kultura z dnia kalibracji); nowa kampania - z kalibracji; stary zapis - k_v = k kultury osady z dnia
  pierwszego wczytania tej wersji (bez skoku). `PeopleOf` czyta k_v, nie `_k[s.Culture.StringId]` (K:PopulationLaw.cs:170-179). Kultura `whitewalker` (spoza tabeli) nie zeruje juz ludzi osady.
  **`PeopleBook` (U23):** konta DOM i WYRZUTKI; `PeopleUnit.Shift` wolany tylko z `Move`.
  **Trzy punkty powrotu (U2; kolizja A 3.4 pkt 1):** `LosersFlee.SendHome` (`VillagerHearthPerMan` K:LosersFlee.cs:66-67, :412-413 i `ToCommon` :452-460 -> `PeopleBook.Move`;
  inaczej kazdy powrot taboru = hearth z niczego x ok. 100), `ClanBudget.ToVillage` (K:ClanBudget.cs:796-806 - 166, W4c, 179: dzis tylko ludnosc BK; od E4/3 takze `Move` SLUZBA -> DOM
  wsi, ktora przyjela) i `OutlawLaw.ReturnHome` (:282-290). PrisonerLaw (K:PrisonerLaw.cs:197, :449) idzie przez `LosersFlee.SendHome`; `CartTownExit.SendHome` to rozkaz ruchu - nie dotyczy.
  **Tabory i lodzie rozwiazane przez gre (R18, U15e):** nasluch `MobilePartyDestroyed` bez niszczyciela dla taboru/lodzi - caly sklad (zdrowi i ranni) `Move` do DOM wsi (+n/k);
  jesli sklad jest juz pusty w chwili zdarzenia (sprawdzic przy kodzie) - prefiks na `DestroyPartyAction.ApplyInternal` (private static, G DestroyPartyAction.cs:10), nie na jednolinijkowym
  `Apply` (:29 - JIT moze go wkleic).
  **Tabela:** pulap k wsi 650 (Wyspy Letnie 0.75 -> ok. 0.11 mln; linia kalibracji "obcieto: summer ..., poza mapa ..."), freefolk 150 -> 300 tys. (Jeff 05.10, SP:361),
  Sarnor wedlug P1 (bez odpowiedzi 100 tys.).
- **Klucze:** z 108: `PeopleUnitEnabled` (true), `OutlawsCountedByPeople` (true); nowe `PopulationMaxPerHearth` 650 (0 = bez pulapu), `DisbandedVillagersGoHome` (true).
- **Test 120 dob (progi 108 + nasze):** linia `Hearth (E4/0)`: tabory i "nasze" ok. 100 razy mniejsze niz dotad, RESZTA ~0; "Ludnosc:" w dobach 2-4 bez spadku 0.6-1%;
  kalibracja PRZED pula wyrzutkow; Wyspy Letnie ok. 0.11 mln, Wolni Ludzie 0.30 mln; bandy: nowe / puste usuniete w d1 wobec d1 biegu E4/0 (108 ryzyko 1 - geografia band);
  "Partie": tabory i lodzie rozwiazane - ludzie wrocili = ludzie w skladzie (kontrola 0); powroty 166/W4c/179 widoczne w `PeopleBook` (linia "Ludzie (ksiega)": wedlug punktu).
- **Ryzyka:** geografia band (pule za ludnoscia: Reach 486 -> 1 130, Nocna Straz 163 -> 3; 40 kryjowek z < 60 ludzmi w zasiegu - 108 ryzyko 1); renty nalezne Wysp Letnich
  1.07 mln/d spadna (renty PL K:PopulationLaw.cs:343-409; zaplacone i tak 3 988 zl - log:123620); `KingdomTreasury`/`BuildFunding` czytaja tylko renty zaplacone;
  hearth przestaje ubywac na starcie (+2-4% hearth wsi) - progi 200/600 37 wsi. **Wolni Ludzie 300 tys.** podwaja k freefolk (21 -> 42/hearth): renty i pule - male liczby.
  `ClanBudget.ToVillage` dalej pisze tez do BK (sklad BK - R0) - ksiega i BK rozne konta, nie podwojenie.
- **NK:** k i tabela tylko w nowej kampanii (`arm_population` w zapisie); jednostka dziala w starym zapisie od pierwszej doby (hearth zdjety dawniej nie wraca).

### E4/4 - Przyrost naturalny i miasta jako stan (109n, PLAN 4.1 cz. 2, 4.2, 4.3) - D

- **Zakres:** przeniesienie 109 (`f95452e`): postfiks `PopulationLaw.GrowthPostfix` Priority.Last na `DefaultSettlementProsperityModel.CalculateHearthChange`
  (dec :34-39), wpiety po `ScorchedEarth.ApplyAll` (K:SubModuleMain.cs:133); g z R1 (H wylaczony do E4/6); premie mnoza dodatnie g; rosnie tylko Normal; blizna
  ScorchedEarth wylaczona przy czynnym przyroscie; "+0.5 ponizej 40" w jednym miejscu (`ScorchedEarth.RefugeeReturn`) do E4/5, w logu osobno; 9a: ludnosc miast jako
  stan (`arm_people` sekcja "t:" w `PeopleBook`); `TablePeopleOf` = `PeopleOf` (jedna definicja, rozdz. 2); `WinterBite.VillageCut` juz jest (E4/0) - nie dublowac.
  **Podloga 10 (R17, U15d):** finalizer W1 (E4/0) widzi, ile dopisala podloga; od E4/4 ta liczba schodzi `PeopleBook.Move` z UCHODZCOW tej wsi (od E4/5), potem z WYRZUTKOW regionu;
  gdy obu brak - licznik "podloga z niczego" (prog testu 0).
- **Klucze:** z 109 (9): `NaturalGrowthEnabled` (true), `GrowthBasePercent` 0.5, `GrowthProsperityWeight` 0.6, `GrowthDangerWeight` 0.6, `GrowthHungerEnabled` (false),
  `GrowthHungerWeight` 1.5, `GrowthMaxPercent` 0.8, `GrowthMaxDeclinePercent` 1.3, `TownPeopleFrozen` (true). Parametry z historii (DOK H1: +0.16..+0.83%/rok, glod -1.3%/rok).
- **Test 120 dob (U12):** **netto "Ludnosc:" d28-d120 = 290-1 150 ludzi/d** (0.2-0.8%/rok od 52.5 mln: 52.5 mln x 0.2% / 364 = 288, x 0.8% = 1 154; smierc w ksiedze dopiero od E4/7, wiec
  tu netto ~ przyrost naturalny); "przyrost naturalny +N ludzi" w linii osobno; wsie wedlug progow stale; RESZTA hearth ~0; podloga z niczego 0; linia pierwszej doby: kolejnosc
  postfiksow BK(400) -> BKROT(200) -> ScorchedEarth -> GrowthPostfix (ostatni); renty nalezne staja (zaplacone prawie bez zmian: sufit 20% kiesy); progi etapu 2-3.
- **Ryzyka:** spalona wies nie odrasta do E4/5 (krotki odstep E4/4 -> E4/5; do tego czasu +0.5 ponizej 40 zostaje); wspolny cel latek (A 3.4 pkt 5: BK VanillaModelTweakPatches.cs:1557-1625,
  BKROT, ScorchedEarth.cs:118-124); renty: hak 112 `VillageTakings.VillageRentYieldsToFees` (K:PopulationLaw.cs:371), Seal31B/CouncilWages35 w `TownTaxPostfix` (:231-232) - 109 zmienia
  te sama metode (scalic recznie); czytelnicy `PeopleOf` (K:PeopleLedger.cs:257, :270; K:Measure169c.cs:541); rzeczy idace za hearth staja: poziomy produkcji (522 wsie poziom 1),
  pojemnosc BK (Hearth x 4, dec BKGrowthModel :75), milicja BK (Hearth/400, dec BKMilitiaModel :99), WorkshopLaw (max chroni).
- **NK:** stary zapis - hearth napuchniety zostaje (bez przeliczania wstecz); stan miast zakladany z dobrobytu chwili wgrania (bez skoku).

### E4/5 - Spustoszenie, powrot do roku, plon od uchodzcow (113n, PLAN 4.4) - D

- **Zakres:** przeniesienie 113 (`5cbb0d6` bez 22 linii 111 - A 3.3) - `Devastation.cs`: rabunek i zerowanie armii pustosza ulamek okregu (R8), konto UCHODZCY w `PeopleBook`
  (`arm_people` "u:"), koniec "+0.5 ponizej 40"; **parametry z HR 2.8 zamiast 113:** zabici 2% (5% przy najezdzcy z kultury z niewola), las 2% + 3% x niebezpieczenstwo,
  **pojmani 10% trafionych - tylko najezdzca z kultury z niewola (HR 2.3; U14)** - czynni dopiero razem z E4/9 (`RaidCaptivesFromVillage`); do tego czasu udzial pojmanych = 0 (zostaja
  uchodzcami), bo BK dalej daje swoich jencow z niczego i nie wolno zdjac ludzi drugi raz; uchodzcy -1.3%/rok.
  **Powrot (U13, P3):** 30 dob bez rabunku i zerowania w regionie (HR 2.5), potem dziennie `reszta / max(1, 364 - dni od ostatniego rabunku w regionie)` - liniowo, wszyscy zywi w domu
  najpozniej w 364. dobie po ostatnim rabunku (Jeff 09.10 "odbudowa 1 rok", SP:760); bez czynnika niebezpieczenstwa w tempie (lamalby "rok"); kolejny rabunek w regionie zaczyna liczenie od nowa.
  **Plon:** R9 (a) z elastycznoscia 1.0, tylko uchodzcy (dom / (dom + uchodzcy)), **bez zera w stanie Looted** (model produkcji zwraca 0 poza Normal - GL 1.1; postfiks: Looted = Normal x udzial
  ludzi w domu - SP:459-460); progi poziomu od dom + uchodzcy; 3.10 Forage zostaje (zboze z zapasu wsi, K:ScorchedEarth.cs:75-78, `Leaks310.Forage`) w przerobionym bloku zerowania (A 3.4 pkt 3);
  lup "4 zl za hearth" co do monety jak w grze (z niczego - ksiega pieniadza, 7.11).
- **Klucze:** z 113 (`DevastationEnabled`, `DevastationPerRaiderDay` 0.5, `DevastationPerForagerDay` 0.2, `DevastationMaxPercent` 90) + zmienione domyslne:
  `DevastationKilledPercent` 2, `DevastationOutlawBasePercent` 2 + `DevastationOutlawDangerPercent` 3, nowe `DevastationCaptivesPercent` 10, `RefugeeReturnQuietDays` 30,
  `RefugeeReturnDays` 364 (zamiast `RefugeeReturnPercent`), `RefugeeYearlyLossPercent` 1.3, `DevastationYieldElasticity` 1.0, `LootedProducesByPeople` (true).
- **Test 120 dob:** linia "Ludzie (spustoszenie)": trafieni = zabici + pojmani + las + uchodzcy (kontrola 0), osobodni lupienia; powrot rusza po 30 dobach spokoju i jest liniowy (uchodzcy wsi
  w d(ostatni rabunek + 182) ok. polowa - kontrola w linii); **wies z >= 1/3 uchodzcow: mnoznik plonu <= 0.67** ("Skonczone, gdy" pkt 2); RESZTA hearth ~0 (rabunki przeszly do "ruch ludzi");
  Ruda/Drewno/Towary w d1 bez skoku; wsie Looted produkuja.
- **Ryzyka:** spirala na pograniczu w dlugiej wojnie (powrot wstrzymany) - pomiar w biegu; MaterialLaw.ProdPostfix (6a) mnozy plon wszystkich towarow (etap 6 - towary, ruda); ta sama metoda
  zywnosci wsi co `WinterBite.VillageFoodPostfix` (mnoza sie - dobrze); BK jency z rabunku (hearth x 4 przy braku danych) i nekromancja ROT czytaja hearth gry w `OnBeforeFinalize` -
  113 podaje tam hearth gry; usuniecie zera Looted zwieksza podaz zywnosci i towarow wsi spladrowanych (pomiar: Towary, zywnosc warowni); E4/2 (b) rajd Innych dostaje zabitych;
  szybszy powrot (rok zamiast lat - HR 2.5 mowilo o 5-7 latach przy pojedynczym najezdzie) = mniej uchodzcow w spichlerzach sasiadow (E4/6).
- **NK:** stary zapis bez uchodzcow (od wgrania liczone).

### E4/6 - Spichlerz okregu i glod (191, PLAN 4.11) - D

- **Zakres:** GL rozdz. 2 z poprawka Jeffa (GL:1-5, SP:457-460) - spichlerz okregu w racjach (`arm_people` "f:"), plon = prawdziwa produkcja zywnosci wsi (IsFood) x jedna stala
  (zwykly rok ok. 1.2 x potrzeby), zjadaja ludzie w domu i uchodzcy; zima (E4/1), spustoszenie (E4/5), brak rak obnizaja sami; glod = spichlerz pusty i plon < potrzeba;
  zgony R10; **uchodzcy z glodu wedlug GL 2.6 (U15a)**: tylu, ilu umiera, rusza w droge - konto UCHODZCY wsi pochodzenia + miejsce pobytu w najblizszym regionie tego samego (albo
  niewrogiego) krolestwa ze spichlerzem > 180 dni; brak - zostaja; przeprowadzka najwyzej raz na 30 dob; powrot jak R8 (rok od dnia, w ktorym spichlerz wsi pochodzenia ma >= 90 dni); z warowni
  oblezonej nikt nie wychodzi; H w przyroscie wlaczony (`GrowthHungerEnabled`); warownia przy Food 0 - gina zaloga i mieszczanie (konto miasta); spichlerz miasta zywiony przez okregi
  (GL 2.13 krok B) - odlozone; zyznosc krain (GL, A02 B1).
- **Test:** bieg zimowy 120 dob: **zadna warownia AI nie glodzi przed 30. doba zimy; mediana zapasu >= 364** (prog etapu) wedlug jednej definicji (U27): wszystkie warownie AI, dni =
  (Food warowni + spichlerz okregu) / ubytek dzienny, bez ubytku = 364 (w medianie), gracz osobno - ta sama w narzedziu od E4/0 B (tam bez spichlerza); bieg zwykly 120 dob: glodne okregi
  tylko spustoszone/oblezone; zgony z glodu w ksiedze (R3); uchodzcy z glodu - miejsce pobytu w linii.
- **Ryzyka:** 5.9 (glod ma skutki - zadowolenie BK, dobrobyt) dzieli ten sam glod - jedna definicja; HungerLaw (glod partii), RBL glod miasta (-2 dobrobytu), BK `BKFoodModel` i model ROT
  owijajacy - ktory jest ostatni, sprawdzic (GL rozdz. 3); etap 5 (kiesa ludu - zakupy jedzenia mieszczan) - ten sam spichlerz miasta.
- **NK:** stan spichlerza zakladany przy wlaczeniu (pol roku potrzeby - parametr GL).

### E4/7 - Pochodzenie, pobor z wsi, polegly trwale, ksiega w menu (164b-1, PLAN 4.8, 4.13, 4.17 prog) - D

- **Zakres:** DOK kroki 5 i 7 z poprawkami krytyka (DOK:600-640) i krytyki E4:
  - **pochodzenie (U16, R20):** slownik region -> liczba per partia (ok. 2 500 partii) **oraz per roster jencow partii i loch osady** (konto JENCY); `arm_people` "s:"/"j:" przez `SaveText`
    w kawalkach (161). Przenosiny niosa udzial pochodzenia: 169c okna `LeaveTroopsToGarrison` / `TakeTroopsFromGarrison` (K:Measure169c.cs:94-95), ekran druzyny (GarrisonArmory), laczenie partii,
    jency wcieleni po bitwie (W10) i zwerbowani z lochu. I1 i H3 wysylaja do regionu pochodzenia; `LawHomePos` (K:LosersFlee.cs:404, :529) tylko dla ludzi bez pochodzenia (siew).
  - **punkt zdjecia ochotnika (U7):** postfiks na BK `MilitaryData.DeduceManpower(PopulationData, int, CharacterObject, Hero)` (BK MilitaryData.cs:151) - wspolny dla AI (BK RecruitmentApplyInternalPatch.cs:20)
    i gracza (BK UI.Patches\RecruitmentOnDonePatch.cs:33), z osada (`data.Settlement`) i liczba; **niezalezny od ustawien zlota** (dzis `LevyGold.ApplyInternalPostfix` wychodzi przy
    `!RecruitGoldToSeller`, K:LevyGold.cs:75). DOM osady -1/k (miasto -1 stanu) -> SLUZBA partii. Karczma AI (notabl null, miasto) - stan miasta do E4/10, potem pula WETERANI; karczma gracza
    (wlasny dialog gry, nie BK) - osobne okno, sprawdzic przy kodzie; A5 autowerbunek zalogi (gra `TickAutoRecruitmentGarrisonChange` dec GarrisonRecruitmentCampaignBehavior.cs:76-95) - osobno,
    zdejmuje tez BK `DeduceManpower` i 1/k.
  - polegly, zmarly z zarazy (po kazdym `Strike`, U9), stracony - `Die` regionu pochodzenia (proporcjonalnie do skladu); powroty - trzy punkty R0 (z pochodzeniem); dezerter do puli wyrzutkow
    regionu pochodzenia.
  - **R5 i R9 (b) w mezczyznach (U4):** `PeopleBook.UnderArms(region)` = a z R5; jedna funkcja dla linii, hamulca i plonu (U19); PeopleLedger "obciazenie" przechodzi na nia. Prog wedlug krain
    (zamiast K:PeopleLedger.cs:32), Zelazne Wyspy wzorem RK; okno zniw w `Levy`; nadwyzka rak x (ludzie / start) (krytyk DOK krok 7); hamulec "kraina bez wolnych rak nie wystawia
    nowego wojska" (pytanie 2); czynnik rak L w `MaterialLaw.ProdPostfix` obok (a) z E4/5 (jeden wspolny czynnik; L_ref = dom + sluzba + jency regionu w dniu siewu - poprawka krytyka krok 6).
  - **linia "Pobor:" (U17):** rekruci odmowieni przez ksiege (a >= f) i przez pule BK (BK manpower 0 przy wolnych rekach) - osobno, wedlug krain.
  - **menu osady (U21; Jeff 05.10 "ksiega ma byc do sprawdzenia ... pozycja w menu osady", SP:347):** pozycja w menu wsi, miasta i zamku "People of this land" -> okno informacyjne
    (`InformationManager.ShowInquiry`; bez `SwitchToMenu` - CLAUDE.md 7) z kontami regionu: "At home", "Under arms", "Held captive", "Outlaws", "Refugees", "Slaves", "Fallen (all time)",
    "Free hands used: x% of y%". Napisy po angielsku, wylacznik `PeopleMenuEntry` (true).
  - siew: istniejace wojsko -> region siedziby rodu (rody bez ziemi - koszyk "bez ojczyzny"), bez zdejmowania; cywile oblezenia - tylko glod (R12).
- **Klucze:** `PeopleOriginEnabled` (true), `LevyFromVillages` (true), `FreeHandsShare` 0.20, `FreeHandsHarvestFactor` 0.5, `FreeHandsElasticity` 0.5, `FreeHandsIronIslandsExtraMax` 0.20,
  `FreeHandsBrake` (wedlug pytania 2; bez odpowiedzi - true), `PeopleMenuEntry` (true).
- **Test 120 dob:** rownanie per kraina (R0, wszystkie konta) - odchylka < 0.1%; ludzie z niczego / w nicosc = 0 poza nazwanymi (narzedzie); "Pobor: chec" x0.3-0.4, hamulec ksiegi i BK osobno;
  wojsko lordow w wojnie 95-115 tys. (podloga 85 tys., etap 2); "Ludnosc BK" najwyzej +2%/rok; czynnik L w d1 = 1.00 wszedzie poza regionami ponad progiem (DOK krok 6 ryzyko); pozycja menu
  otwiera okno w autotescie gracza (reczna proba Jeffa).
- **Ryzyka:** rozmiar stanu w zapisie (napis > 32767 B psul zapis - 161; `SaveText`); Straz - wyjatek (rekrut z calego Westeros, bez progu); BK pula dalej osobno (R0, linia "Pobor:");
  koszt slownikow jencow (ok. 2 100 jencow - male); Levy (K:Levy.cs:58-91) - chec traci zroznicowanie poza nedza (krytyk krok 7); postfiks na metodzie BK - linia startowa "BRAK", gdy typu nie ma.

### E4/8 - Zalogi, milicje, nowe druzyny, karawany i konwoje z ludzi (164b-2, PLAN 4.7, 4.9) - D

- **Zakres:** przyrost zalog z modelu = 0 (gra `TickGarrisonChangeForTown` dec GarrisonRecruitmentCampaignBehavior.cs:97-108; BK Enlistment +1 BK:VanillaModelTweakPatches.cs:1517-1536,
  wlaczany AI w kazdej warowni w wojnie BK:BKSettlementBehavior.cs:418-428; gra +2 buntownicy dec DefaultSettlementGarrisonModel :50) - zaloga rosnie tylko werbunkiem A5 (z ludzi,
  platnym) i z partii; milicja: przyrost z modelu (BK +Hearth/400, ROT +10/+5 - dec ROTSettlementMilitiaModel :25, :41) = ochotnicy z wolnych rak regionu (-1/k), bez rak 0,
  ubytek bez walki - do domu (okno W1/W2 z E4/0 daje miejsce); nowe druzyny lordow (szablon klanu 401/d) - z zalogi siedziby albo z wolnych rak regionu siedziby;
  odrodzony lord bez 55 ludzi i zboza (`ClanBudget.SpawnPrefix/Postfix` K:ClanBudget.cs:906, :971-1003); karawany z ludzi miasta wlasciciela (stan "t:"), powrot przy rozwiazaniu;
  **konwoje i wedrowcy BK (R19, U15f)** - z DOM osady wyjazdu, do DOM osady przyjazdu (migracja), rozbici - R6; R2 migracja do miast (9b).
- **Test 120 dob:** **"Zalogi: przyrost bez werbunku (169c)" - przyrost z modelu = 0 kazdej doby** (prog etapu); ludzie pod bronia z niczego = 0 (narzedzie); zalogi w d120 >= 85%
  biegu A z E4/0 (d120: 78 924 w biegu 457 - odczyt); etap 1: zadne krolestwo > 85% bitew lordow ani 3 twierdze; oblezenia - pomiar. Prog 364 dob - koniec etapu (U20, rozdz. 4).
- **Ryzyka:** slabsze zalogi -> latwiejsze oblezenia (w 457 dobach model dal 52 tys.; do d364 41 tys.); jesli zalogi < 85% - A5 dostaje cel zalogi 166 jako bramke (pomiar w E4/0 A: ile A5
  odmowil z braku ochotnikow, ile z limitu gry) - nie zmniejszamy wojska (Jeff 09.10 C, SP:771); Straz - pulap i blokada przyrostu juz sa (K:ClanBudget.cs:909, :1051-1079); patrole BK wracaja do zalogi
  (+271 824 w 457 dobach) - z zalogi, nie z niczego; OutlawLaw `RosterPostfix` (szablony band) - juz z puli.

### E4/9 - Krainy kanonu: Straz, Wolni Ludzie, niewola, dluznik (192, PLAN 4.15-4.17, 4.19) - D

- **Zakres:** R14 Straz: 3% puli wyrzutkow regionow Westeros dziennie + z ludnosci Westeros (RK:207-216 rozklad), przestepcy z I1 (juz), jeden pulap (166, K:ClanBudget.cs:84, :407);
  **dozywotnio (U5): w `ClanBudget.Releases` dla Strazy (`b.Zero`) zwolnienia = 0** (dzis :633-643 liczy nadmiar puli i zwalnia przez `ReleaseFrom` -> `ToVillage`, :651-656, :691/:705);
  3% puli i ludnosc tylko ponizej pulapu, przestepcy zawsze; H1 Straz nie pali wsi (postfiks na `CampaignEventDispatcher.AiHourlyTick` - RK:511, `WatchDoesNotRaid`); Wolni Ludzie:
  walcza wszyscy dorosli (prog 100% z E4/7, tabela 300 tys. z E4/3); R15: `ThrallLaw` (RK R12/R13) - sprzedany jeniec = NIEWOLNI tylko w kulturach kanonu (I1/I1b juz);
  **jency z rabunku (U14, U22):** prefiks `bool` na BK `BKRaidCaptureBehavior.ExecuteCapture` (BK ...Raids\BKRaidCaptureBehavior.cs:233) - BK nie dopisuje jencow z niczego; napastnik dostaje
  dokladnie pojmanych z R8 (E4/5 - te same osoby, jedno zdjecie z wsi) jako `villager_<kultura wsi>` do `PrisonRoster` (konto JENCY z pochodzeniem); dla AI i gracza ta sama regula (krolestwo
  z niewola wedlug kanonu; przelacznik BK gracza "nie bierz" zostaje); wylaczona tez dzialka w zlocie z niczego z tej metody (RK:400); Zelazne Wyspy 27 tys. NIEWOLNI na start = czesc
  500 tys. tabeli wysp (rachunek RK:348-349: 500 tys. x 27% mezczyzn x 20% = 27 tys. - odpowiedz na pytanie Jeffa "skad 27 tys.", B 1.8), ubytek 5%/rok = smierc (U15b);
  DLUG 4 (RK R13): dluznik rozpuszcza poczet - ludzie do regionow pochodzenia.
- **Klucze:** `WatchLifelong` (true), `WatchDoesNotRaid` (true), `ThrallLawEnabled` (true), `RaidCaptivesFromVillage` (true), `IronIslandsStartThralls` 27000, `ThrallYearlyLossPercent` 5.
- **Test 120 dob:** linia `Straz i Wolni Ludzie (4.15)`: przyrost Strazy wedlug zrodla (pula 3%, ludnosc, Mur); **zwolnieni Strazy 0 (linia 166)**; 0 rabunkow Strazy; niewolni Zelaznych ok. 27 tys. w d1;
  jency z rabunku tylko w kulturach kanonu (kontrola), pojmani w linii spustoszenia = jency z rabunku w linii Jency (kontrola 0); "z rabunku BK (z niczego)" = 0.
- **Ryzyka:** E3/1 placi za jencow z rabunkow BK z kasy najblizszego miasta (okno K:CirculationWindows.cs:1786) - platnik zostaje, zmienia sie zrodlo ludzi; polityka karna BK (K:PrisonerLaw.cs:21, :221);
  dar z Westeros dla Strazy (182, CrownGifts) - bez zmian; pule wyrzutkow w Westeros po 108 wariant C (wieksze tam, gdzie ludzie) - 3% puli daje Strazy wiecej z Reach, mniej z Polnocy;
  Straz ponad pulapem (np. po spadku pulapu) czeka na ubytek przez smierc - pulap nie zwalnia, tylko nie przyjmuje.
- **NK:** start Zelaznych Wysp i Wolnych Ludzi tylko w nowej kampanii.

### E4/10 - Weterani (167, PLAN 4.14) - S
Karczma tylko z puli WETERANI (zwolnieni, rozbici, wypuszczeni jency - z bronia, zbroja i koniem, SP:656-660); zwolniony: 2/3 do domu, 1/3 do karczmy najblizszego miasta
(AN 25 - kierunek Jeffa "do wsi albo karczmy", OB:151; podzial = projekt); **niewynajety przez 120 dob wraca do domu regionu pochodzenia (R16, U15c)**; start: pula starych zolnierzy z ludnosci
miast (SP:711, NK). Najemnikow o 81-97% mniej (AN 21 - informacja dla Jeffa). Kolizje: MercContract 185, LevyGold (zloto werbunku do miasta), klan najemnikow Spoils (133), 160 (najemnik z wlasnym
koniem), punkt zdjecia E4/7 (karczma przestaje zdejmowac stan miasta). Klucze: `VeteransEnabled` (true), `VeteranShareToTavern` 0.33, `VeteranWaitDays` 120. Test: "z karczmy" w "Ludzie:" tylko z puli;
pula w linii; niewynajeci do domu po 120 dobach (kontrola w linii).

### E4/11 - Ksiega wiosek i plon okregu (193, PLAN 4.5, 4.6) - D
Wioski poboczne (gromady po ok. 250 ludzi, SP:430-431; WM, RB) z wlasnymi ludzmi i czescia plonu okregu; plonie / spalona / odbudowa 1 rok (SP:760 - ta sama regula co R8); menu okregu
(napisy po angielsku; rozszerza pozycje z E4/7); plon okregu = suma pracy wiosek; zywnosc warowni tak samo; `arm_district`. Laczy sie z etapem 7 (7.9 napisy nad spalonymi, 7.11 rabunek
osada po osadzie). NK. Test: koniec etapu (rozdz. 4).

---

## 4. Test i progi etapu ("Skonczone, gdy" z PLAN:146)

Kazda paczka: autotest 40 dob + zapis d362 9 dob (0 bledow) i test 120 dob z progami paczki (rozdz. 3) **oraz progami etapow 1-3** (zadne krolestwo > 85% bitew lordow
ani 3 twierdze; glowy rodow < 5 000 najwyzej 10; 0 bankructw; wojsko lordow w wojnie 95-115 tys., podloga 85; reszta ksiegi pieniadza < 10 tys. zl/d). Prog nie przechodzi -
wylaczamy kawalek w `Armoury.json` autotestu i powtarzamy (jedna zmiana naraz).

| Prog etapu (PLAN) | Jak mierzony | Od paczki | Dzis |
|---|---|---|---|
| ludnosc rosnie 0.2-0.8% rocznie | **netto "Ludnosc:" d28-d364 biegu konca etapu = 290-1 150 ludzi/d** (U12); przyrost naturalny i smierc w wojnie osobno w linii; co, jesli w wojnie netto wyjdzie nizej - **pytanie 4** (do odpowiedzi prog bez furtki) | E4/4 (smierc od E4/7) | +57.6%/rok |
| w pomiarze BK najwyzej 2% | "Ludnosc BK (169c)" d1 -> d364 przeliczone na rok | caly etap | -2.6%/rok |
| wies, z ktorej uciekla 1/3 ludzi, daje mniej plonu | mnoznik plonu wsi z >= 1/3 uchodzcow <= 0.67 (linia spustoszenia) | E4/5 | progi 200/600: 0% |
| spalona wioska (takze poboczna) wraca po roku | wsie spalone w d1-d364 biegu 728 dob, w ktorych regionie nie bylo kolejnego rabunku przez 364 doby: **>= 95% ludzi w domu (zywi uchodzcy 0) i wioska poboczna odbudowana** (U13; regula daje 100% minus smierc uchodzcow ok. 1%) | E4/5, E4/11 | odrost stala gry |
| przyrost zalog bez werbunku = 0 | "Zalogi: przyrost bez werbunku" - przyrost z modelu 0 kazdej doby | E4/8 | +114/d |
| test z zima od poczatku 120 dob: zadna warownia AI nie glodzi przed 30. doba zimy, mediana zapasu >= rok | bieg zimowy: pierwsza doba glodu warowni AI > 30 doba zimy; mediana dni zapasu >= 364 wedlug jednej definicji (U27: wszystkie warownie AI, (Food + spichlerz okregu) / ubytek, bez ubytku = 364, gracz osobno) | E4/6 (baza: E4/0 B) | nie bylo zimy |
| (projekt) kazdy czlowiek policzony | narzedzie `--grupa etap4`: ludzie z niczego / w nicosc = 0 poza startem, narodzinami i smiercia; reszta ksiegi ludzi < 1% ruchu brutto (srednia 28 dob); rownanie krain z kontami JENCY i WETERANI < 0.1% (U1) | E4/7, E4/8, E4/10 | ok. 1 000/d z niczego, reszta 283/d |
| (projekt, U20) wojsko nie maleje | bieg konca etapu: wojsko lordow w wojnie 95-115 tys. srednio d300-364 (Jeff 05.10 "ok. 108 tys.", SP:329) i zalogi w d364 >= 85% biegu A z E4/0 (09.10 C "mniej wojska AI - NIE", SP:771) | E4/8, E4/10 | partie rodow 104 282, zalogi d364 88 076 (bieg 457) |

**Koniec etapu:** nowa kampania 364 dob (wszystkie paczki, ustawienia jak u Jeffa) + bieg zimowy 120 dob + bieg 728 dob dla "spalona wioska wraca po roku"
(ok. 4 h w nocy; PLAN: rok ok. 2 h). Wynik do raportu dla Jeffa przed jego nowa kampania.

---

## 5. Ryzyka / kontrola calosci (zasada 0)

### 5.1 Wspolne cele latek i stan (plik:linia)
| Cel / stan | Kto dzis | Kto w etapie 4 | Co pilnowac |
|---|---|---|---|
| `Village.DailyTick` | BK prefiks bool (BK:NotablePatches.cs:207-225); odczyt listy latek K:VillageTakings.cs:461-473 | E4/0 okno W1; E4/4 podloga (R17) | prefiks First przed BK; HooksLine liczy tylko obce prefiksy (U25) |
| `DefaultSettlementProsperityModel.CalculateHearthChange` | BK postfiks 400 (BK:VanillaModelTweakPatches.cs:1557-1625), BKROT 200, ScorchedEarth Last (K:ScorchedEarth.cs:118-124) | E4/4 GrowthPostfix Last (po ScorchedEarth) | linia pierwszej doby z kolejnoscia; RBL dziedziczy hearth (dec RealisticProsperityModel) |
| `VillagerCampaignBehavior.AddVillagersToParty` / `CreateVillagerParty`, lodzie NavalDLC | brak | E4/0 okna, E4/3 jednostka | finalizer pomiaru po postfiksie 108 |
| `RaidEventComponent.Update` / `OnBeforeFinalize` | brak (6 modow slucha `RaidCompleted`) | E4/0 okno, E4/5 Devastation | lup gry co do monety; cudze nasluchy widza hearth gry |
| `MapEvent.LootDefeatedPartyPrisoners` / `CaptureDefeatedPartyMembers` (U1, U6) | brak (ROT czysci jencow Innych pozniej, dec ROTOthers.cs:612-615) | E4/0 W10, E4/2 (f) finalizer | warunek "zwyciezca Inni" pierwszy; W10 mierzy po E4/2 |
| BK `BKRaidCaptureBehavior.ExecuteCapture` | CirculationWindows (:1788), RealPayer (:289) - okna | E4/0 W11, E4/9 prefiks bool | po E4/9 okna E3 widza "nic nie dopisano" - platnik bez zmian (RK:400) |
| BK `MilitaryData.DeduceManpower` (U7) | BK: AI RecruitmentApplyInternalPatch.cs:20, gracz RecruitmentOnDonePatch.cs:33 | E4/7 postfiks (punkt zdjecia) | karczma osobno; LevyGold dalej tylko zloto |
| `ClanBudget.Releases` / `ReleaseFrom` / `ToVillage` | 166 (K:ClanBudget.cs:627-738), W4c, 179 | E4/3 `Move` w `ToVillage`, E4/9 Straz 0 (U5) | ksiega i BK - rozne konta |
| `DestroyPartyAction` (tabory, lodzie) | nasluchy E3/7, OutlawLaw.PartyDestroyed | E4/3 powrot skladu (R18) | nasluch, gdy sklad jeszcze jest; inaczej `ApplyInternal`, nie `Apply` |
| `CalculateDailyFoodProductionAmount` | `WinterBite.VillageFoodPostfix` (K:WinterBite.cs:121-133) | E4/1 (czynnik regionu), E4/5 (6a), E4/6 (spichlerz czyta) | mnoza sie - kolejnosc bez znaczenia |
| `MaterialLaw.ProdPostfix` | wpis 98, K13 | E4/5 (a), E4/7 (b) | jeden wspolny czynnik (DOK krok 6 ryzyko) |
| ROT `OnMapEventEnded` | Measure169c okno (K:Measure169c.cs:104-105) | E4/0 rozszerzenie, E4/2 prefiks bool | okno void First biegnie mimo false |
| `ScorchedEarth.OnDaily` blok zerowania | 3.10 Forage (K:ScorchedEarth.cs:74-78) | E4/0 licznik, E4/5 Devastation.Forage | zboze dalej z zapasu wsi |
| `PopulationLaw.Daily` (renty) | hak 112 (K:PopulationLaw.cs:371), Seal31B/CouncilWages35 (:231-232) | E4/4 (9a) | scalic recznie (A 3.4 pkt 4) |
| `PeopleOf` / `TablePeopleOf` | PeopleLedger.cs:257/:270, Measure169c.cs:541, WorkshopLaw.cs:815 | E4/3 k na osade, E4/4 jedna definicja | WorkshopLaw max chroni rece |
| **trzy punkty powrotu** (U2) | `LosersFlee.SendHome` (H3, I1), `ClanBudget.ToVillage` (166, W4c, 179), `OutlawLaw.ReturnHome` (H3 prosci, pula) | E4/3 (1/k, `Move`), E4/7 (pochodzenie) | kazdy przez `PeopleBook.Move`; `CartTownExit.SendHome` nie dotyczy |
| `OutlawLaw` (TakeCommoners, ReturnHome, Seed, OnVillageLooted, PartyDestroyed) | H3, W4b, W4c, paser | E4/0 przekazania (bez linii w srodku), E4/2 (wighty), E4/3, E4/5 | 331 linii zmian od n119 (A 3.2) - przejrzec rece |
| **`PeopleBook`** (U23) - stan ksiegi | brak (dzis: `arm_population` K:ArmouryBehavior.cs:466-468 - k kultur) | E4/2 (CIALA), E4/3 (DOM, WYRZUTKI, k na osade), E4/4 ("t:"), E4/5 ("u:"), E4/6 ("f:"), E4/7 ("s:", "j:"), E4/9 (NIEWOLNI), E4/10 (WETERANI) | `SaveText` w kawalkach (161); `Reset` w konstruktorze `ArmouryBehavior` (:395); jedno `Move` |

### 5.2 Spojnosc logiki
1. **Kolejnosc obowiazkowa:** jednostka (E4/3) przed przyrostem (E4/4) - inaczej odplywy 0.5 hearth wykrwawia wsie; przyrost przed spustoszeniem - inaczej powrot uchodzcow liczylby
   sie razem ze stala gry; spustoszenie przed spichlerzem i poborem (GL:322, DOK kroki 5-6). E4/1 i E4/2 sa poza ta osia (nie czytaja ksiegi; E4/2 tylko region z E4/0 i konto CIALA).
2. **Nic drugi raz:** "+0.5 ponizej 40" - jedno miejsce (`ScorchedEarth.RefugeeReturn`), znika w E4/5; "3% w las przy spaleniu" (K:OutlawLaw.cs:556-570) ustepuje spustoszeniu (113 pkt 5);
   `VillageCut` raz (E4/0); zima - jedno zrodlo (WinterSource); pobor zdejmuje BK `DeduceManpower` (sklad) i 1/k z ksiegi (ilosc) - to nie podwojenie, bo BK nie jest ludzmi (R0);
   **pojmani przy rabunku zdejmowani z wsi raz (R8), E4/9 tylko przenosi ich do JENCY (U14)**; **E4/0 czyta liczniki modulow, nie liczy ich drugi raz (U8)**; R9 (a) liczy tylko uchodzcow,
   (b) tylko sluzbe i zmarlych (U4).
3. **Martwy kod staje sie zywy:** blizna ScorchedEarth gasnie przy czynnym przyroscie (109 pkt 7); dekret BK Growth - martwy (konsola) i zostaje martwy; `OutlawHearthPerMan`
   (K:Settings.cs:669) martwy przy czynnej jednostce. Wlaczenie produkcji w stanie Looted (E4/5) to nowa praca: ok. 30-80 wsi naraz w wojnie [S: rabunki 457 dob] - pomiar Towary.
   Okna E4/0 wpiete zawsze, ale liczace tylko przy kluczu (U26).
4. **Zamknieta ekonomia ludzi:** po E4/10 jedyne zrodla ludzi z niczego to start (bilans otwarcia: tabela krain, 27 tys. niewolnych, 616 cial - nie-ludzi) i narodziny (R1/R2), jedyne ujscie -
   smierc (R3, smierc naturalna kont bez narodzin); kazde inne przesuniecie ma konto (R0: DOM, SLUZBA, JENCY, WYRZUTKI, UCHODZCY, NIEWOLNI, WETERANI) i jedno `Move`.
   Zloto: etap 4 go nie rusza (lup rabunku, zold - bez zmian), wiec progi etapu 3 nie powinny sie ruszyc; wyjatek posredni - mniejsze zalogi i druzyny = mniej zoldu.
5. **Ta sama kolejnosc w dobie:** tick wsi gry (DailyTickSettlement - kolejnosc wobec naszego ticku obojetna, pomiar liczy od migawki do migawki); nasze `OnDailyTick` (CampFever :1409, klimat :1418-1419,
   zerowanie :1421, H3 :1432, wyrzutki :1492, 169c :1501, E4/0, PeopleLedger :1535). Spustoszenie (E4/5) w ticku dobowym po zerowaniu; spichlerz (E4/6) po spustoszeniu, przed przyrostem nastepnej doby.
6. **Gracz = AI:** jednostka i spustoszenie dzialaja tez dla gracza (pobor wymuszony, zadania - 108); jency z rabunku gracza - ta sama regula (U22); punkt zdjecia ochotnika wspolny (U7);
   konie zima - takze gracz (Jeff TAK).
7. **Kultura osady a ludzie (U3):** po E4/3 zmiana kultury nie przelicza ludzi; prawo niewoli idzie za biezaca kultura (I1b), demografia i klimat - za kultura z dnia kalibracji.

### 5.3 Cudzy kod (sprawdzone w dekompilacji do tego projektu)
- BK dekret Growth tylko z konsoli (BK:BannerKingsCheats.cs:4307, jedyne `StartDecree`); BK zastepuje `Village.DailyTick`, ale czyta model (BK:NotablePatches.cs:213).
- `Village.Hearth` w BK pisza tylko: tick (:213, :220) i dekret (:47); w BEE inwestycje wsi (e4dec BetterEconomy.all.cs:27705; zamkniete K2); w BKROT nic (grep e4dec BKROTPatch.all.cs).
- NavalDLC lodzie: `(n+1)/2` (dec2 Fishing.cs:186, :227), rozwiazanie nadmiaru po rozladunku (:344-349). Rabunek: `Hearth -= krok x 0.5 x hearth`, zloto z niczego (dec2 Raid.cs:174-188).
- Gra: tabor z 0 zdrowymi i sierota rozwiazywane bez zwrotu (G VillagerCampaignBehavior.cs:253-259, :268-272); jency pokonanego do skladu zwyciezcy (G MapEvent.cs:1529-1567, MapEventParty.cs:75-85).
- ROT: nekromancja z `HealthyManCountAtStart` przegranych, takze po przegranej, bez miejsca; jency Innych `Clear` (dec ROTOthers.cs:599-615); rajd `Hearth/2` (:339); +100 i +2 (:571-584 - zablokowane T2b);
  kultura osady zdobytej przez Innych `whitewalker` (:732, :749), powrot (:649, :669).
- BK: asymilacja kultury osady (CultureData.cs:200-206) i przy wczytaniu (BKManagerBehavior.cs:171-179); `DeduceManpower` wolany przez AI i gracza (MilitaryData.cs:151); jency z rabunku
  `ExecuteCapture` (BKRaidCaptureBehavior.cs:233-285: gracz wedlug przelacznika, AI wedlug niewoli krolestwa BK).
- BK zwierzeta: pol paszy w sniegu (BK:BKPartyConsumptionModel.cs:208-211), pelna na pustyni (:200-203).
- Skagos: 4 wsie po 311 hearth - rybacy, wielorybnicy, zboze (Ravikayr), morsy (ROT-Map settlements.xml:15277-15318).

### 5.4 Gra Jeffa
Jeff gra dopiero po skonczeniu projektu, w nowej kampanii (SP 09.10 ok. 08:05). E4/0, E4/1, E4/2 moga wejsc do trwajacego zapisu bez skutkow wstecz (E4/2 bez puli 616 - log);
E4/3-E4/9 dzialaja w starym zapisie czesciowo (rozdz. 3, NK); pelny obraz - nowa kampania (SP:888-890: rzeczy NK domyslnie wlaczone). Przy kazdym nowym kluczu: `grep` w jego `Armoury.json`
(CLAUDE.md 7 - zapisane wartosci nadpisuja domyslne z kodu); dzis zaden klucz etapu 4 tam nie istnieje (108/109 sprawdzaly 06.10; przy kodzie powtorzyc).

---

## 6. Pytania do Jeffa (tylko kierunek; reszte decyduje projekt)

1. **Sarnor:** zostawic jak w ROT - ok. 100 tys. ludzi (kraina sie trzyma), czy kanon - ok. 20 tys. (przy dzisiejszych stratach znika w ok. 1.5 roku)?
   Rada: 100 tys. Blokuje tylko jeden wiersz tabeli w E4/3 (nowa kampania); bez odpowiedzi - 100 tys.
2. **Kraina bez wolnych rak nie wystawia nowego wojska:** gdy z okolicy pod bronia i w grobach jest juz tylu, ilu moze odejsc od pola (20% mezczyzn, Zelazne do 40% - im wiecej niewolnych, tym wiecej;
   w czasie zniw polowa), nie ma nowych rekrutow, az przyrost uzupelni straty. Male krainy (Wolni Ludzie, Sarnor, Smocza Skala) moga slabnac latami; Straz ma doplyw z calego Westeros.
   Zgoda? Rada: tak. Blokuje hamulec w E4/7-E4/8 (bez hamulca ponad progiem spada tylko plon).
3. **"Spalona wioska wraca po roku" - rok liczony od ostatniego najazdu w okolicy** (Twoje 09.10: "odbudowa spalonej wioski: 1 rok (nie 2-3)"): dopoki w okolicy trwaja rabunki, ludzie nie wracaja;
   po 30 dniach spokoju wracaja rowno, wszyscy najpozniej rok po ostatnim najezdzie. Tak? Rada: tak. Blokuje tempo powrotu w E4/5 (bez odpowiedzi - tak).
4. **NOWE - ludnosc a smierc w wojnie (U12):** warunek konca etapu mowi "ludnosc rosnie 0.2-0.8% rocznie". Twoje decyzje mowia tez: przyrost w granicach historii (w pokoju ok. 0.5% rocznie,
   najwyzej 0.8%) i polegly ubywa na zawsze. W 457-dniowym biegu w bitwach ginelo 900 ludzi dziennie, w drugim roku 1 100 - to 0.6-0.8% ludzi rocznie, czyli tyle, ile caly przyrost.
   Przy takiej wojnie ludnosc stoi albo lekko spada. Co ma byc warunkiem? (a) **przyrost w pokoju 0.2-0.8%, a w ciaglej wojnie ludnosc moze stac albo spadac (do -0.5% rocznie)** -
   tak bylo w wojnie stuletniej; (b) ludnosc zawsze rosnie co najmniej 0.2% - wtedy przyrost musialby byc wyzszy niz w historii albo bitwy mniej krwawe. Rada: (a). Blokuje tylko prog testu
   konca etapu (zadnego kodu); bez odpowiedzi - prog netto 0.2-0.8% bez furtki, wynik biegu konca etapu pokazemy z liczbami.

**Akcja (nie pytanie):** proba zimy w autotescie to tylko 4 ustawienia probnej kopii (rozdz. 3, E4/0). Jesli zabezpieczenie znow ja zablokuje, dostaniesz gotowe polecenie do uruchomienia
(gra zamknieta).

**Informacja (bez pytania, decyzje projektu albo Twoje wczesniejsze - mozesz zmienic jednym slowem):**
- Twoi jency z rabunku - wedlug tej samej reguly co AI (Twoja zasada "jedna regula" i 07.10 pkt 6a); przelacznik "nie bierz" zostaje.
- Nocna Straz nikogo nie zwalnia do domu (Twoje 05.10 "sluzba dozywotnia"); nadmiar ponad pulap czeka na ubytek.
- Jency odbici przez Innych to polegli swojego regionu (Twoje 07.10); w Westeros i Essos nie wstaja.
- Wyspy Letnie 0.75 -> ok. 0.11 mln (zaden punkt mapy nie znaczy wiecej ludzi niz w Qarth); cywile w oblezeniu tylko gloduja, nie choruja (zaraza obozowa - wojsko);
  najemnikow w karczmach o 81-97% mniej (Twoje decyzje 05.10 i 08.10, na start pula starych zolnierzy); zwolniony zolnierz w 2/3 wraca do domu, w 1/3 idzie do karczmy, a niewynajety po 120 dniach do domu.
- Zalogi: w 457-dniowym biegu w dniu 364 - 88 tys., z czego 41 tys. z niczego; po etapie mniej, prog 85% pilnuje, zeby to odrobil platny werbunek z ludzi.
- "Do czasu ksiegi wies nie rosnie sama" (Twoja decyzja 4.3) wchodzi razem z przyrostem w E4/4 - do tego czasu ludnosc w logu dalej rosnie ok. +57% rocznie (zrobienie tego wczesniej wykrwawiloby wsie).

---

## 7. Odlozone (i dlaczego)

| Rzecz | Dlaczego nie teraz | Kiedy |
|---|---|---|
| dopasowanie przyrostu BEE/BK (0.22%/d z podlogami 50/300) do ksiegi (DOK krok 10) | BK nie jest ksiega ludzi (R0); prog etapu "BK najwyzej 2%" spelniony (-2.6%/rok) | **prog liczbowy (U17):** rozjazd ksiega/BK w regionie > 30% albo hamulec BK > 30% prob werbunku (linia "Pobor:" od E4/7) albo BK > +2%/rok |
| spichlerz miasta zywiony przez okregi (GL 2.13 krok B), rytm zniw w plonie (A02 B3) | najpierw pomiar kroku A (E4/6) | po E4/6 |
| rabunek osada po osadzie (RB), lup "4 zl za hearth" z niczego | po spichlerzu i 108-113 (RB:5); lup to ksiega pieniadza | etap 7.11 |
| losowa zaraza regionu (DOK krok 8) | krytyk: BLEDNE; Jeff: zaraza tylko z oblezen | nie robimy |
| choroby cywilow w oblezeniu | decyzja projektu: tylko glod (rozdz. 6 informacja) | na slowo Jeffa |
| dluzsza noc obozu zima | Jeff 09.10: NIE (SP:815) | nie robimy |
| uboj jesienny i sol, rok urodzaju z przyczyny (audyt 00 E20, Z6-Z7) | pomysly | rozdz. 3 PLAN |
| Sarnor kanon 20 tys. | P1 | na slowo Jeffa |
| hamulec H4 (wojna falami) | Jeff: NIE (SP:440-441) | nie robimy |
| 111 kasy miast (TownPurse) z lancucha | etap 5 | etap 5 |

---

## 8. Krytyka - co przyjeto/odrzucono

Krytyka `KRYTYKA-E4.md` (U1-U28) - kazda uwaga sprawdzona ponownie w kodzie (K, CS), dekompilacji (G, BK, dec, dec2) albo logu; skrypt krytyki `krytyka_tools\culture_changes.py`
powtorzony (wynik ten sam: 0 zmian kultury). Werdykt: 24 przyjete w calosci, 4 przyjete z inna poprawka albo czesciowo (U3, U4, U11, U14), 0 odrzuconych w calosci.
Do tego 3 wlasne uzupelnienia znalezione przy sprawdzaniu (koniec rozdzialu).

**WYSOKIE**
- **U1 (R0 bez kont JENCY i WETERANI) - PRZYJETE.** Sprawdzone: G MapEvent.cs:1529-1567 (nie-bohater z lochu pokonanego -> `RosterToReceiveLootMembers` zwyciezcy), MapEventParty.cs:75-85 (dla NPC =
  `Party.MemberRoster`); bez odbiorcy usuwani (:1546). Zmiana: R0 konta JENCY, WETERANI + konto nie-ludzi CIALA, rownanie z nimi; E4/0 okno W10 + linia "Jency (E4/0)"; narzedzie T7 z kontem jencow;
  prog etapu "rownanie z JENCY i WETERANI".
- **U2 ("jedno miejsce powrotow" nieprawda) - PRZYJETE.** Sprawdzone: `OverflowHome.SendHome` -> `ClanBudget.ToVillagePop` (K:OverflowHome.cs:127-154), `GentryService.cs:647` -> `ToVillagePop`,
  `ToVillagePop`/`ReleaseFrom` -> `ToVillage` -> BK `UpdatePopFromSoldiers` (K:ClanBudget.cs:711-738, :791-806); `CartTownExit.SendHome` = rozkaz ruchu (:216-226). Zmiana: R0 "trzy punkty wejscia
  do DOM" (+ NIEWOLNI, Mur), E4/3 i tabela 5.1 poprawione, zdanie o "jednym miejscu" usuniete.
- **U3 (k na kulture, kultura osady sie zmienia) - PRZYJETE CZESCIOWO.** Sprawdzone: K:PopulationLaw.cs:170-179 (`_k[s.Culture.StringId]`), tabela bez whitewalker :39-71; BK CultureData.cs:200-206
  i BKManagerBehavior.cs:171-179; ROT :732, :749 (powrot :649, :669); bieg 457: 0 zmian (skrypt powtorzony). Przyjete: k na osade (E4/3), klimat i prog wolnych rak z kultury z dnia kalibracji,
  pozycja "zmiana kultury osad" w E4/0. **Odrzucone w czesci "niewola z kultury z dnia kalibracji":** prawo jenca I1b jest w grze (WGRANIE 7) i swiadomie idzie za biezaca kultura osady
  ("Kraina = biezaca kultura osady (BK zmienia ja po asymilacji i przy wczytaniu - prawo idzie za nia)", K:PrisonerLaw.cs:47-48) - prawo to obyczaj ludzi, ktorzy tam teraz rzadza;
  zmiana kultury nie tworzy ani nie kasuje ludzi, wiec ksiega sie zamyka niezaleznie od tego.
- **U4 (czynnik rak w zlych jednostkach) - PRZYJETE, z inna poprawka w jednym punkcie.** Sprawdzone: DOK:53 ("prog f = 20% mezczyzn regionu (5.4% ludnosci) ... ((1-a)/(1-f))^0.5"), DOK:283, P v1:116.
  Przyjete: a w mezczyznach, f = 0.20 (zniwa 0.10), e = 0.5, obciecia a <= 0.9, L >= 0.5 (DOK:615), zdanie dla Jeffa w rozdz. 0 pkt 2. **Nieprzyjete: "mezczyzni uchodzcy" w liczniku a** -
  uchodzcy sa juz w R9 (a) (cale gospodarstwa, e = 1.0); w a bylyby policzeni drugi raz. Zamiast tego R9 (a) liczy tylko uchodzcow (dom / (dom + uchodzcy)), (b) tylko sluzbe i zmarlych;
  zmarli liczeni netto po narodzinach (K_m), zeby "przyrost uzupelnia" (pamiec Jeffa) dzialal.

**SREDNIE**
- **U5 (Straz dozywotnio a 166 zwalnia) - PRZYJETE.** Sprawdzone: K:ClanBudget.cs:633-643 (`b.Zero` -> `over` z puli Strazy, `left` w ludziach), :651-656, :691/:705 (`ReleaseFrom` -> `ToVillage`); SP:359
  ("sluzba dozywotnia"). Zmiana: R14, E4/9 (`WatchLifelong`, test "zwolnieni Strazy 0").
- **U6 (odbici jency Innych zywi w bandzie) - PRZYJETE.** Sprawdzone: G MapEvent.cs:1529-1567 + MapEventParty.cs:75-85; ROT czysci tylko loch (dec ROTOthers.cs:612-615); CS OthersGrowth.cs:23, :131 i CH:2867
  ("zostaje ... odbici jency"); SP:435-436. Zmiana: R13, E4/2 (f) z kluczem `OthersRescuedAreFallen`, E4/0 W10 i licznik w "Wzrost Innych".
- **U7 (zdjecie rekruta tylko u AI i za bramka zlota) - PRZYJETE.** Sprawdzone: K:LevyGold.cs:75 (`!s.RecruitGoldToSeller` -> return); BK RecruitmentApplyInternalPatch.cs:20 (AI) i RecruitmentOnDonePatch.cs:33
  (gracz) - jedyne dwa wywolania `DeduceManpower(PopulationData, int, CharacterObject, Hero)` w calym BK. Zmiana: E4/7 postfiks na `DeduceManpower`; dopisane: karczma AI/gracza i A5 osobno.
- **U12 (prog ludnosci po cichu oslabiony) - PRZYJETE, z poprawiona liczba.** Sprawdzone: v1 :58, :333, :432; PLAN:146; 52.5 mln x 0.2%/364 = 288/d, x 0.8% = 1 154/d. Przyjete: prog etapu na netto
  d28-d364, test E4/4 290-1 150/d. **Poprawka liczby z krytyki:** "polegli 0.16-0.25%/rok (DOK L3)" pochodzi z 12-dobowych sesji 05.10 (DOK:329, :399-406); bieg 457 dob daje 906/d srednio
  (0.63%/rok od 52.5 mln), w drugim roku 1 100/d (0.76%) - skrypt na liniach "Ludzie:". Przy takiej smierci prog "0.2-0.8%" zderza sie z dwiema decyzjami Jeffa (przyrost historyczny, polegly
  na zawsze) - dlatego to nie "informacja", tylko **pytanie 4** (kierunek: co jest warunkiem konca etapu).
- **U13 ("1 rok" Jeffa a 85-90% w rok + 30 dob) - PRZYJETE.** Sprawdzone: SP:760 ("ODBUDOWA SPALONEJ WIOSKI: 1 ROK (nie 2-3) - dotyczy wiosek pobocznych ... i glownej"); v1: 1 - 0.993^334 = 90%.
  Zmiana: R8 i E4/5 powrot liniowy do 364. doby po ostatnim rabunku w regionie (30 dob spokoju wliczone w rok), bez czynnika niebezpieczenstwa w tempie; prog etapu ">= 95% w domu, wsie bez kolejnego
  rabunku przez 364 doby"; pytanie 3 jako potwierdzenie z cytatem.
- **U14 (pojmani przy rabunku zdjeci drugi raz) - PRZYJETE, z inna poprawka.** Sprawdzone: v1 R8 bez pojmanych; HR 2.3 i 2.8 maja juz "jency 10% trafionych" dla kultur z niewola (projekt v1 to pominal).
  Przyjete: czwarty wynik R8 i kontrola "trafieni = zabici + pojmani + las + uchodzcy"; E4/9 tylko przenosi do JENCY. **Nieprzyjete: "liczba z BK ExecuteCapture"** - BK liczy `ProjectedCaptives`
  (BKRaidCaptureBehavior.cs:241), wedlug HR 2.3 2.5-6 razy wiecej niz u reiverow; jedna regula = liczba z R8 (HR), a BK `ExecuteCapture` dostaje prefiks, ktory oddaje dokladnie te liczbe.
  Do E4/9 udzial pojmanych = 0 (BK dalej daje swoich z niczego - inaczej podwojne zdjecie).
- **U15 (szesc przeplywow bez zrodla/ujscia) - PRZYJETE (a-f).** Sprawdzone: (a) GL 2.6 juz mowi dokad (projekt v1 tego nie przepisal) - R10, E4/6; (b) RK:348-350 - R15 (start = czesc 500 tys. wysp,
  ubytek = smierc); (c) R16 - 120 dob czekania; (d) gra Village.cs:231-234 i BK NotablePatches.cs:218-221 - R17, E4/4 (podloga schodzi z uchodzcow, potem z puli); (e) G VillagerCampaignBehavior.cs:253-259,
  :268-272, dec2 Fishing.cs:344-349 - R18, E4/3; (f) K:CirculationWindows.cs:1781 (PopulationPartyComponent), C Z6 - R19, E4/8, pomiar w E4/0.
- **U16 (pochodzenie bez jencow i przenosin) - PRZYJETE.** Sprawdzone: G MapEvent.cs:1529-1567 (wcieleni), K:LosersFlee.cs:404, :529 (`LawHomePos` - najblizsza osada kultury), K:Measure169c.cs:94-95 (okna
  przenosin). Zmiana: R20, E4/7 (slowniki jencow, przenosiny, I1/H3 do regionu pochodzenia).
- **U17 (dwa hamulce poboru) - PRZYJETE.** Sprawdzone: BK MilitaryData.cs:170-189 (pula +1% sufitu dziennie, sufit = klasa x waga BK), BEE PopulationCampaignBehavior.cs:99, :116; DOK krok 7 ryzyko. Zmiana: R0 i R5 -
  ksiega steruje iloscia, BK skladem; linia "Pobor:" z dwoma hamulcami (E4/7); prog liczbowy dla odlozonego dopasowania BEE (rozdz. 7). Puli BK nie zmieniamy teraz (osobna ksiega checi - R0).
- **U20 (wojsko sprawdzane tylko 120 dob) - PRZYJETE.** Sprawdzone: SP:329, SP:771; log: zalogi d364 88 076, model do d364 +41 459 (skrypt na liniach "Zalogi (169c)"). Zmiana: bieg A E4/0 = 364 doby (baza),
  prog konca etapu "wojsko lordow 95-115 tys. d300-364, zalogi d364 >= 85%", liczba w rozdz. 0 pkt 7, droga naprawy przez A5, nie przez mniejsze wojsko.
- **U23 (stan ksiegi bez wlasciciela) - PRZYJETE, z inna nazwa.** Zmiana: nowa klasa `PeopleBook` z `Move/Born/Die` (R0, E4/2, E4/3, tabela 5.1). **Nie PeopleLedger** (jak sugerowala krytyka "np."):
  PeopleLedger ma w kodzie kontrakt "sam log ... niczego nie zmienia w grze i nie ma stanu w zapisie" (K:PeopleLedger.cs:14-15) - lepiej dac ksiedze nowa klase niz zmieniac znaczenie starej;
  PeopleLedger czyta `PeopleBook`. Miejsce kodu E4/2 rozstrzygniete: Armoury.

**NISKIE**
- **U8 (E4/0 dubluje nasluchy) - PRZYJETE.** Sprawdzone: K:ArmouryBehavior.cs:689-692 -> PeopleLedger (:111-179); wiersze regionow :250-276, CSV z hearth :34-37; milicja w linii 169c :281-297; liczniki
  LosersFlee :414, :458 i OutlawLaw :568, :708, :778. Zmiana: zero nasluchow w Measure40, podzial frakcji i nasluchy partii w PeopleLedger, kolumny w `ludzie-regiony.csv` zamiast 2 nowych CSV, milicja
  w linii 169c, przekazania licznikow (`Take*`) zamiast linii w TakeCommoners/ReturnHome/OnVillageLooted/LosersFlee:413.
- **U9 (licznik zarazy bez frakcji) - PRZYJETE, dokladniej.** Sprawdzone: K:CampFever.cs:84-89, :104-110, :165-185. Zmiana: `NoteDisease(mp, przed - po)` po kazdym `Strike` - roster przed/po, bo HitRandom
  moze nie trafic (wtedy `dead` zawyza).
- **U10 (siew i bieda w jednym zrodle) - PRZYJETE.** Sprawdzone: K:OutlawLaw.cs:292-308 (Seed -> TakeCommoners), :708 (bieda -> `_inMisery`). Zmiana: `TakeSeed(sum)` z istniejacej sumy, poza RESZTA.
- **U11 (stala gry bez mnoznikow) - PRZYJETE, inna metoda.** Sprawdzone: dec DefaultSettlementProsperityModel.cs:41-70; G ExplainedNumber.cs:126, :154, :163 (wynik = (baza + dodatki) x (1 + suma mnoznikow)).
  Zamiast `includeDescriptions: true` - publiczne `BaseNumber`/`SumOfFactors` (bez kosztu opisow); w linii "baza" i "z mnoznikami".
- **U18 (klimat wedlug kultury osady) - PRZYJETE.** Zmiana: R11, E4/1 (region; partia - region pod partia z pamiecia doby - koszt `RegionAt`).
- **U19 (trzy definicje "pod bronia", id krolestw) - PRZYJETE.** Sprawdzone: K:PeopleLedger.cs:312; K:CrownGifts.cs:36-38 (krolestwa: vlandia = Zelazny Tron, empire = Dolina, empire_w = Reach) wobec
  K:PopulationLaw.cs:46, :56 (kultury: vlandia = Westerlands, empire = Braavos). Zmiana: `PeopleBook.UnderArms` (R5) dla linii, hamulca i plonu; "Krainy" przez `Kingdom.Culture`; do E4/7 linia nazywa
  definicje PeopleLedger wprost.
- **U21 (menu osady dopiero w E4/11) - PRZYJETE.** Sprawdzone: SP:347. Zmiana: pozycja menu w E4/7 (okno informacyjne, bez `SwitchToMenu`), E4/11 ja rozszerza.
- **U22 (P2 rozstrzygniete) - PRZYJETE.** Sprawdzone: SP:444-445 (07.10 pkt 6a), zasada "jedna regula gracz/AI"; RK:397 (06.10) sprzed decyzji 07.10. Zmiana: P2 skreslone, decyzja w "Pogodzone", informacja w rozdz. 6.
- **U24 (liczniki w zywym kodzie nie moga rzucac) - PRZYJETE.** Sprawdzone: K:OutlawLaw.cs:262-280 (brak try w petli; `v.Hearth -=` :275 przed `Add` :278). Zmiana: w TakeCommoners nic nie dopisujemy,
  kazde Take/Note z wlasnym try, lista miejsc do recenzji (E4/0 ryzyko 4).
- **U25 (diagnoza 112, brak latek tylko w naszym kodzie) - PRZYJETE.** Sprawdzone: K:VillageTakings.cs:466-467 (`capOff = ti.Prefixes.Count > 0`). Zmiana: HooksLine liczy prefiksy spoza "com.jeff.";
  linia startowa E4/0 wypisuje wlascicieli latek 11 celow.
- **U26 (okna tylko przy wlaczonym kluczu) - PRZYJETE.** Sprawdzone: K:ScorchedEarth.cs:120-121. Zmiana: wpinane zawsze, liczone przy On.
- **U27 (dwie definicje mediany zapasu) - PRZYJETE.** Sprawdzone: K:PeopleLedger.cs:192-205 ("bez ubytku" poza mediana), :322 (`IsStarving` wszystkich, takze gracza). Zmiana: jedna definicja
  w E4/0 T9, E4/6 i rozdz. 4; kolumna `gracz` w CSV.
- **U28 (drobne) - PRZYJETE (wszystkie trzy).** 4.3 razem z E4/4 - informacja dla Jeffa w rozdz. 0 pkt 7 i rozdz. 6; 4.12 przed 4.11 - zdanie w rozdz. 3; zaleznosc od 169c (K:Measure169c.cs:31) -
  "dane 169c: TAK/BRAK" w linii startowej i "brak danych 169c" w "Krainy".

**Rzeczy sprawdzone przez krytyke bez uwag** (KR rozdz. 1 i 4) - potwierdzone i zostawione: sygnatury i miejsca 9 okien v1, model hearth Default w RBL, Town.DailyTick bez cudzych latek
w BK/BEE/BKROT, proba zimy (4 klucze), pisarze `Village.Hearth`, dekret BK tylko z konsoli, liczby rozdz. 1, numery 188-193.

**Wlasne uzupelnienia przy sprawdzaniu (nie z krytyki):**
- **W1 (smierc w wojnie).** Zabici wedlug okresu w biegu 457 (480 -> 1 103/d) - wiersz w rozdz. 1; podstawa pytania 4 (patrz U12).
- **W2 (Zelazne Wyspy - prog z niewolnych).** v1 R5 dawal stale 40%; RK 2.3 pkt 6 (RK:401) liczy `f = 0.20 + min(0.20, T / M)` - tak realizuje sie decyzja Jeffa "prog ok. 40% (pracuja niewolni)" (SP:362),
  a bez niewolnych prog wraca do 20%. Wpisane w R5 i E4/7 (`FreeHandsIronIslandsExtraMax`).
- **W3 (zalogi w liczbach).** d364 biegu 457: 88 076 zalog, model +41 459 do tej doby - do rozdz. 0 pkt 7 i progu U20 (bez tej liczby "moga byc mniejsze" nic Jeffowi nie mowilo).
