# Audyt 04 - ekonomia wojny, wojsko i armie, lupy, okupy (09.10.2026, tylko odczyt)

Zakres: zold i kto placi, werbunek i straty, dezercja, armie AI, zaopatrzenie (krotko), lupy (z cial, Spoils, DTE, sprzedaz), oblezenia, jency
i okupy (RealisticCaptivity, gra, BK). Na koncu - obok tematu, bo Jeff o to prosil w tej samej wiadomosci - godziny obozu nocnego (0-6).

Podstawa: kod w grze (Armoury c01a54ba = 2e235ea, RealisticCaptivity, CrashScribe) w `scratchpad/audyt/repo`; dekompilacje gry 1.4.8 (`ore-supply/cs`),
BK (`ore-supply/bk`), DTE 1.4.7, StrategicCampaignAI145, Bannerlord: Prison Is Not Fun (ilspycmd, `scratchpad/w04`); autotest ROCZNY
`Modules/Armoury/Armoury-2026-10-08_08-18-38.log` (364 doby), CSV `CrashScribe/economy-2026-10-08_08-20-02.csv`, sesja CrashScribe
`session-2026-10-08_08-18-37.log`; ustawienia Jeffa `Armoury.json`, `DiplomacySettings_v1.2.json` (pliku MCM RealisticCaptivity nie ma - czynne sa
wartosci z kodu). Projekt bazowy: `docs/PROJEKT-EKONOMIA-OBIEG-2026-10-08.md` (dalej PROJEKT; zasady Z1-Z9, paczki 162-168).
Kalendarz: rok = 364 dni. Oznaczenia: [K] kod, [P] pomiar z logu/CSV, [H] historia/lore ze zrodlem, [S] szacunek.

Nie dubluje: 169 (sam log obiegu), 170 (BetterEconomy), 171 (zbrojenie zalog). Kolizje - rozdz. 6.

---

## 0. Dla Jeffa

1. **Wojsko kosztuje dzis 2-4 razy wiecej, niz rod zarabia** (zold w polu ok. 605 tys. dziennie na 100 tys. ludzi). AI placi to z zapasow, pozyczek i skarbca korony - to juz naprawia projekt ekonomii (budzet rodu, paczka 166). Nowe tutaj: o tym, czy powstaje armia, decyduje tylko sila (mod StrategicCampaignAI i BK) - pieniadze nie graja zadnej roli.
2. **Armie wykrwawiaja sie za szybko.** W roku testu partie lordow stracily 185 tys. zabitych przy stanie ok. 100 tys. - czyli cale wojsko gnie prawie dwa razy w roku. Przegrany traci 46% zabitych, zwyciezca 14% (w historii 15-40% i 1-5%). **Zgodziles sie 07.10, zeby przegrani uciekali zamiast ginac (H3) - to jeszcze nie jest zrobione.** To najwazniejsza rzecz w tym raporcie: mniej trupow = mniej werbunku, mniej zlota na rekrutow (dzis ok. 56 tys. dziennie), wiecej ludzi na wsi.
3. **Okupy za lordow sa dwa rozne swiaty.** Gdy Ty trzymasz lorda, glowa rodu kosztuje ok. 50-80 tys. (nasze stawki). Gdy lord AI trzyma lorda AI, placi sie wedlug gry - glowa sredniego rodu wychodzi za ok. 4-6 tys., czyli za 10-15 dni dochodu rodu. W sredniowieczu okup mial byc "na miare majatku" - zwykle rzad rocznego dochodu, czasem duzo wiecej. Proponuje jedna regule dla wszystkich: okup wedlug dochodu rodu i jego pozycji, placony z kiesy rodu, a reszta na raty (jak Twoj "dlug honorowy").
4. **Twoj wlasny okup znika w nicosc** - gra zabiera Ci zloto i nikt go nie dostaje (porywacz nic nie ma). To mala poprawka: zloto ma trafic do tego, kto Cie trzyma.
5. **Krol nie dostaje nic z wojny**, choc placi polowe zoldu. W XIV wieku dzialalo "prawo trzecich": z lupu i okupow trzecia czesc szla wyzej, az do krola. Proponuje: korona, ktora placi zold, bierze trzecia czesc okupow i sprzedanego lupu swoich wasali (to zasila skarbiec z biezacych wplywow, czego potrzebuje projekt ekonomii).
6. **Lup z bitew AI to dzis glownie sprzet, nie zloto** - i dobrze. Zostaly dwa male zrodla zlota z niczego (zloto "z cial" dla AI i lup z oblezenia), oba sa juz w planie paczki 164 - nie ruszam ich osobno.
7. **Rzeczy, ktorych nie mamy, a gra pozwala je dodac:** pomoc wasali na okup pana (jedna z trzech "pomocy feudalnych"), okup miasta zamiast szturmu, okup okolicy zamiast palenia wsi (historyczne "appatis"), jency z Westeros oddawani Nocnej Strazy zamiast sprzedawani paserom (lore: Yoren zabiera ludzi z lochow), okup na raty z zakladnikiem z rodziny, zmeczenie wojna rosnace od pustego skarbca i zaleglego zoldu.
8. **Oboz 0-6:** dzis AI rozbija oboz 22-4, Ty spisz liczony od 21 do 5, bandy 23-5 - trzy rozne "noce". Na dzis w nocy proponuje jedna zmiane: wszyscy maszeruja do polnocy i stoja od 0 do 6, jak chciales.
9. **Pytania do Ciebie (zmieniaja Twoja gre):** czy okup ma zalezec od dochodu rodu (bogaty placi wiecej, biedny mniej) zamiast stalych stawek; czy jako wasal oddajesz koronie trzecia czesc okupow i lupu; czy Twoj oboz od 0 do 6 tez (pytanie o zmierzchu przesuniete na polnoc). Liczby dobralem sam - rozdz. 4.

---

## 1. Stan dzis

### 1.1 Zold i kto placi

- [K] Zold partii rodu -> sakiewki jej ludzi (MenPurse), zold zalogi -> kasa jej osady: `Armoury/src/SoldierPay.cs:13-41` (opis), przyciecie, gdy saldo rodu nie miesci sie w kiesie glowy. Karawany bez zmian (zold w nicosc, PROJEKT wiersz 10).
- [K] Premia konnych x1.5 (paczka 160): `MountedWage.cs:14-38`, `Settings.cs:303`. Stawki: t1 2, t2 3, t3 6, t4 10 (konny 15), t6 21 (konny 31) zl/dzien (BK BaseWage 1.25).
- [K] Korona w wojnie zwraca 50% zaplaconego zoldu z zapasu skarbca: `KingdomTreasury.cs:83-84, 134-213`, `Settings.cs:641` (`CrownWageRefundPercent` 50 - decyzja Jeffa 05.10).
- [P] Doba 364: zold partii rodow naliczony 605 318, zaplacony 581 714 (649 partii); zalogi 193 830 / 184 777; przyciete 33 419 w 58 rodach; nieumarli 6 882;
  karawany 20 386 w nicosc; sakiewki ludzi 17.93 mln (linia "Zold: dzien 109200"). Zwrot korony 252 827 z naleznych 335 925, 6 skarbcow za malo (linia "Korona: dzien 109200").
- [P] CSV, doba 364, rody bez gracza i dworzan BK, suma zoldu / suma dochodu modelu wedlug tieru: t3 2.05, t4 2.26, t5 2.78, t6 4.44
  (mediana t4: dochod 379/d, zold 2 075/d, 379 ludzi, rodzina 202 tys.). Krolowie: mediana dochodu 636/d, zoldu 5 100/d, 754 ludzi. [S] Uwaga: "dochod modelu"
  nie obejmuje wszystkich naszych wplywow (renty, zwrot korony); PROJEKT liczy wydatki wojska na 105% dochodu rodow. Rzad wielkosci ten sam: wojsko > dochod.
- [P] Koszt dnia: 605 tys. na ok. 100 tys. ludzi w polu = **6.0 zl na glowe** (zalogi 2.4 - BK -50% dla garnizonu).

### 1.2 Werbunek, straty, dezercja (rok autotestu, sumy z linii "Ludzie:", "Bitwy:", "Werbunek:")

| Miara (364 doby) | Liczba [P] |
|---|---|
| zabici we wszystkich starciach | 389 675 (partie rodow 185 533, tabory wsi 89 971, bandy 44 325, milicje 23 994, karawany 20 713, zalogi 12 422) |
| ranni | 400 354 |
| starc / w tym pogromow i nierownych | 10 109 / 8 645 |
| "prawdziwe bitwy" (1 313): zabitych u zwyciezcy / u przegranego | **13.6% / 46.2%** (historia 1-5% / 15-40%, linia sama to wypisuje) |
| dezercja razem / z partii rodow | 70 122 / 27 109 |
| z tego WarLedger (zalegly zold, AI x0.5) | 5 710 ludzi (suma wpisow "WarLedger: ... traci N ludzi", 4 366 wpisow) |
| zwerbowani: od notabli do partii rodow / z karczmy / "bez osady" (jency, ochotnicy z mapy, duplikaty ROT) | 258 204 / 32 026 / 326 889 |
| zloto AI za werbunek: do notabli / do miast za najemnikow | 8.01 mln / 12.35 mln = **56 tys. dziennie** |
| wojsko w partiach rodow (stan) | 100 547 w 678 partiach (doba 364) |

- [K] Dezercja AI: vanilla (morale < 10, limit zoldu, przepelnienie) - `DesertionLaw.cs:12-41`, `Settings.cs:232` (`DesertionLawForAi` false).
  WarLedger: po 2 dniach zaleglosci 0.5% ludzi dziennie za dzien zwloki, elita pierwsza, AI x0.5 (`WarLedger.cs:12-24, 80`).
- [K] Wniosek: **partie lordow traca 1.85 x swojego stanu zabitymi w roku** i odbudowuja sie werbunkiem. To nie jest koszt zoldu, to koszt ludzi
  i rekrutacji (56 tys. zl dziennie plus sprzet) - i najwiekszy pojedynczy odplyw ludnosci wojennej.

### 1.3 Armie AI (zbieranie, spojnosc, rozwiazywanie, oblezenia)

- [K] Zalozenie armii: vanilla `DefaultArmyManagementCalculationModel` (wplyw, nie zloto; `MinimumNeededFoodInDaysToCallToArmy` 15; rozpad przy spojnosci 10,
  `cs/.../DefaultArmyManagementCalculationModel.cs:32-46`), BK `BKArmyManagementModel.cs:43-160` (tytuly, wplyw, prog 0.4 limitu wplywu),
  **StrategicCampaignAI145** `StrategicArmyManagementModel` (dekompilacja `w04/scai.cs:1227-1300`): armia, gdy sila >= 260 i >= 2 czlonkow, bez "cooldownu"
  po porazce; **spojnosc -3.5 dziennie po 5 dniach na wrogiej ziemi** ("Overextended supply lines", `StrategicAiTuning.SupplyGraceDays` 5).
- [K] **Zaden z tych modeli nie patrzy na pieniadze** (zold, rezerwe rodu, skarbiec). Koszt armii dla wodza to wplyw; koszt dla czlonkow - wlasny zold
  (SoldierPay) minus 50% zwrotu korony, ten sam w armii i poza nia.
- [P] Wojna przez caly rok w 27-31 z 35 krolestw (PROJEKT 1.1); KRONIKA WOJEN na koniec roku - 17 par wojujacych. Diplomacy: zmeczenie wojna wlaczone
  (`DiplomacySettings_v1.2.json`: 0.25/dzien, 0.02 za ofiare, 10 za oblezenie, 15 za okupacje) - **bez skladnika pieniedzy**.
- [P] Oblezenia: 51 osad zdobytych w roku (18 miast) - linie "WarLedger: ... wziete obleczeniem" (prosperity -15%, lojalnosc -15, `WarLedger.cs:141`).
  CampFever dziala (117 linii oblezen w roku).

### 1.4 Zaopatrzenie (krotko - pelny temat w AUDYT-WOJNA-LOGISTYKA 10 i PROJEKT-GLOD)

- [K] Racje -40% (`Rations.cs:51-71`): 0.3 kg na czlowieka dziennie, historycznie 1.5-2 kg. Glodny AI kupuje bez limitu ceny (`HungerLaw.cs:10-24`) - placi miastu.
  Jedzenie nie ogranicza wojny (AUDYT-WOJNA-LOGISTYKA 1.3, 10). Do PROJEKT-GLOD, nie tutaj.

### 1.5 Lupy

| Droga | Stan [K] | Zloto z niczego? | Liczba [P] |
|---|---|---|---|
| Bitwa gracza (scena) | dzialka gracza 33% z magazynu DTE (`Settings.cs:213`, Armoury.json 33 - "trzecia kapitana"); loteria gry za poleglych wycieta (`BattlefieldLaw.cs:233-237, 559-569`) | nie | - |
| Bitwy AI-AI: sprzet | DTE `EveryoneCampaignBehavior.DistributeLootRandomly` (`w04/dte_ecb.cs:751-827`) - cala zbrojownia pokonanych do zwyciezcow; AiWear obija lup | nie (przenosi) | "lup obity 11 362 szt." doby 364 |
| Bitwy AI-AI: zloto "z cial" | gra `MapEvent.LootCasualtyCharacter` (`cs/.../MapEvent.cs:1850-1867`): 7.25 x poziom^2 / 55 do kiesy wodza zwyciezcy, **dla AI bez naszej latki** | **tak** | [S] 10-25 tys./d (PROJEKT wiersz 14) |
| Spoils of War | sprzedaz auto, monety z cial, zloto taboru, zdarzenia (okupy, skrzynie) zablokowane (128, `SpoilsSeal.cs`) | nie | 0 zl z niczego (linia "Spoils of War (128)") |
| Sprzedaz lupu przez lordow AI | gra `PartiesSellLootCampaignBehavior` -> `SellItemsAction`, placi kasa miasta | nie | 116 szt. za 47 046 zl doby 364 (44.7% wartosci) |
| Sprzedaz nadwyzek z sakiewek ludzi | MenPurse, placi miasto, sufit 10% ceny nowej (`SellByCondition.cs:211`) | nie | 7 155 szt. za 11 334 zl (1.9% wartosci) |
| Lup band (paser) | 106: miasto placi z kasy, dzialka pasera zostaje w miescie | nie | 12 922 zl doby 364 |
| Oblezenie (pillage / devastate) | gra `SiegeAftermathCampaignBehavior.cs:158-167, 739-748`: zloto = 15 x spadek dobrobytu, `GiveGoldAction(null, ...)` | **tak** | [S] ok. 51 x 2-15 tys. = 0.1-0.75 mln rocznie (mniej niz 2 tys./d) |
| Pole po bitwie z udzialem garnizonu | DTE kopiuje zbrojownie pokonanych, a kasuje ja tylko przy zniszczeniu partii (`dte_ecb.cs:510-520, 828-853`) - zaloga zdobytej osady zyje dalej | **mozliwy dubel sprzetu** (niesprawdzone w grze) | - |

### 1.6 Jency i okupy

**Szeregowi.** [K] Gra: lord AI w twierdzy sprzedaje jencow osadzie (`PartiesSellPrisonerCampaignBehavior.cs:24-39`), loch sprzedaje 10% dziennie w nicosc
(`:41-86`), w obu przypadkach `SellPrisonersAction.cs:66-89` - **zloto z niczego** dla sprzedajacego, a szeregowy jeniec po prostu znika z gry (nie trafia
do osady). Cena: 25% kosztu werbunku (`DefaultRansomValueCalculationModel.cs:30`), BK w osadzie z polityka "Enslavement" - cena niewolnika
(`bk/BannerKings.Patches/VanillaModelTweakPatches.cs:1211-1232`). RealisticCaptivity: podloga = stawka posrednika (`FairRansom.cs:32`); w Westeros gracz
dostaje 20% (paser), w Essos 100% (`FairRansom.cs:41-51`, `Settings.cs:42-45`). [P] Jencow w partiach 4-6 tys., w lochach 90-380 (linie "Ludzie:").
To wszystko jest w PROJEKT (wiersz 6, paczka 164b: kase placi miasto, czlowiek zostaje mieszczaninem) - nie dubluje.

**Lordowie - cztery drogi i cztery ceny [K]:**

| Droga | Kto ustala cene | Kto placi -> kto dostaje |
|---|---|---|
| AI trzyma AI | gra: `(tier rodu + 2) x 200 x (1; glowa 2.5; krol 6) + 6 x sqrt(zloto jenca)`, razy wielkosc krolestwa (`DefaultRansomValueCalculationModel.cs:9-30`); barter x0.9, rosnie +30% za tydzien niewoli do x3.4 (`SetPrisonerFreeBarterable.cs:39-52`); proba 10% dziennie (`RansomOfferCampaignBehavior.cs:102-109`) | barter AI miedzy glowami rodow, zloto prawdziwe (`BarterManager.cs:91-119`) |
| gracz trzyma AI (kurier) | RC `LordPrice`: krol 100 000, glowa 25 000, lord 8 000, x(1 + 0.25 x tier) + 10 000/miasto, 5 000/zamek (`FairRansom.cs:71-139`, `Settings.cs:48-54`; tylko gdy jenca trzyma rod gracza - `:82`) | rod jenca -> gracz, prawdziwe (`RansomOfferCampaignBehavior.cs:178`); **ale gdy placacy AI ma za malo, gra dosypuje mu do ceny + 1000 z niczego** (`:174-176`) |
| gracz sprzedaje posrednikowi w karczmie / z ekranu | j.w. | **czesc gry z niczego**, nadwyzke RC zdejmuje z kiesy glowy rodu jenca (`FairRansom.cs:107-118, 161-186`) |
| AI trzyma gracza | gra: `(0.5-1) x (5% zlota + 300) x 1/2/4 x 1/2` (`PlayerCaptivity.cs:169-172`), RC x5 + 30 x renoma, sufit 95% majatku z domami (`Patches.cs:141-171`) | **gracz -> nikt**: `GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, ...)` (`PlayerCaptivityCampaignBehavior.cs:241-246`). Tylko "dlug honorowy" RC splaca porywaczowi (`CaptivityBehavior.cs:438-443`) |

[S] Przyklad (doba 364): glowa rodu tier 4 z 46 tys. w kiesie (mediana glow), krolestwo ok. 20 lenn:
- AI-AI: (6 x 200 x 2.5) + 6 x sqrt(46 225) = 3 000 + 1 290 = 4.3 tys. x 1.35 = **ok. 5.8 tys.** (do ok. 18 tys. po 8 tygodniach niewoli)
  = 11-15 dni dochodu modelu rodu tier 4 (379-526/d), ok. 3-4% rocznego.
- gracz trzyma: 25 000 x 2 + lenna = **50-80 tys.** = 0.3-0.55 roku dochodu.
- krol AI-AI: 8 x 200 x 6 + 6 x sqrt(260 tys.) = 12.7 tys. x ok. 1.3 = **ok. 16 tys.**; u gracza 250-400 tys.

[K] Inne mody: **Bannerlord: Prison Is Not Fun 1.3** (dzialajacy modul) - bohater w niewoli moze zginac; szansa przezycia rosnie z jego zlotem
(`zloto / 10 000`, `w04/pinf.cs:599-665`) - bogaty jeniec jest "wart trzymania". BK `RansomDuty` - pomoc na okup suzerena tylko dla gracza (ZRODLA-DOCHODU:217).
[P] Linii o okupach AI-AI w logach nie ma (RealisticCaptivity loguje tylko lordow gracza; w roku autotestu 0 wpisow) - **czestosci okupow nie znamy**.

### 1.7 Oboz nocny (obok tematu - Jeff prosil w tej samej wiadomosci)

- [K] Cztery rozne "noce" w jednym pliku `NightRest.cs`: gracz (sen, pytanie o oboz) 21-5 (`:107, :250, :1188, :1206`); AI lordowie i karawany
  **22-4** (`:395, :406`, namioty `:852`, trzymanie spiacych `:861`; opis MCM `Settings.cs:693` "(22-4)"); bandy nocne 10-16 i dzienne 23-5 (`:511-512`);
  pora "dawn" 5-7 (`:65`). 15% kolumn AI idzie przez noc (`AiCampSkipPercent`, `Settings.cs:697`, Armoury.json 15). Eskorta armii idzie za wodzem (`:448`),
  wodz armii obozuje jak lord.
- Realne dystanse marszu i oboz w marszu to temat osobnego raportu (w tym katalogu jest dzis 06 i 08) - tu tylko godziny, bo zmiana jest mala.

---

## 2. Jak bylo w sredniowieczu i w lore GoT [H]

1. **Indentures i zold.** Krol zawieral kontrakt z kapitanem, kapitan z ludzmi i placil im z pieniedzy krola (Essex Record Office; indenture Edwarda III
   z Czarnym Ksieciem 10.07.1355, Cambridge). Stawki kampanii Crecy-Calais: rycerz 24 d, zbrojny 12 d, lucznik konny 6 d, pieszy 2-3 d (Ayton 1994,
   Prestwich 1996 - jak w `MountedWage.cs:22-24`). Armia 10 tys. ok. 45 tys. d dziennie (AUDYT-WOJNA-LOGISTYKA 1.4) = **ok. 4.5 d na glowe** - gra 6.0, w zgodzie.
2. **Sluzba lenna 40 dni na koszt pana**, potem zold krola (AUDYT-WOJNA-LOGISTYKA 1.4, PROJEKT Z3/WarChestDays). Baron z 200-500 L rocznie trzymal caly rok
   10-25 zbrojnych (tamze) - [S] rod tier 4 w grze (140-190 tys. zl rocznie = 580-800 L) moglby trzymac ok. 30-60 zbrojnych albo ok. 100 ludzi mieszanych,
   a trzyma 380.
3. **System "trzecich".** Z lupu wartego ponad 10 marek kapitan bral 1/3 od kazdego czlowieka; krol bral 1/3 zyskow kapitana i 1/3 z jego trzecich
   (czyli 1/9 zdobyczy szeregowego) (Historical Britain Blog, "Indentures and the King's Army"; D. Hay, "The Division of the Spoils of War in Fourteenth-Century
   England", TRHS 1954). **Wielcy jency ("chief of the war", krolowie, ksiazeta) byli zastrzezeni dla krola za "stosownym odszkodowaniem"** dla tego,
   kto pojmal (indenture 1355, Cambridge). Logika: **kto placi zold, ten ma udzial w zdobyczy**.
4. **Ile wart byl okup.** Konwencja rycerska: okup "w proporcji do majatku ziemskiego, zeby nie zrujnowac" - w praktyce czesto wiecej (Ambuhl 2013,
   omowienie w "Ransom Brokerage in the Fifteenth Century", OpenEdition). Przyklady: Thomas Rempston (pojmany 1429) 18 000 ecu = ok. 3 000 L przy rocznym
   dochodzie 60 L (**50 lat dochodu**), wiezony do 1435; John Clifton 800 marek - wiecej niz wartosc calych jego ziem; Robert Moleyns 6 000 L + 3 870 L kosztow;
   John Holland, hrabia Huntingdon 20 000 marek; Jan II 4 mln ecu (1358), obnizone do 3 mln (Bretigny 1360), splacane latami (Wikipedia, Ransom of John II);
   du Guesclin po Auray 1364: 40-100 tys. frankow (zrodla sie roznia), placil Karol V; Karol z Blois ok. 0.5 mln ecu (1356). Okup splacano z ziemi rodu
   (sprzedaz dworow - Knyvett 1441), z poreczen (Moleyns oddal 945 L porecznikom), z podatkow poddanych i wasali, a kupcy skupowali jencow na spekulacje
   (John Cornwall 1421).
5. **Pomoc feudalna na okup.** Jedna z trzech "zwyczajowych pomocy" wasala: okup ciala pana, pasowanie najstarszego syna, slub najstarszej corki
   (Magna Carta kl. 12 - "rozsadna" kwota; Wikipedia "Feudal aid"). Pomoc wasali czesto nie starczala, jeniec szukal wielu zrodel (omowienie Ambuhla).
6. **Prosty zolnierz** do ok. 1400 rzadko byl wart okupu; okupy dla prostych upowszechnily sie w XV w., pierwsza skala "taryf" - Azincourt (Ambuhl,
   medievalists.net 2013). Okup byl tez dochodem szeregowych (Southampton 2013).
7. **Straty w bitwie:** zwyciezca 1-5%, przegrany 15-40% (typowo), 38-63% przy poscigu konnicy/pulapce terenu (HISTORIA-RABUNKU-I-BITEW-2026-10-07.md 4.1).
8. **Lore GoT.** Turniej: przegrany traci konia i zbroje na rzecz zwyciezcy i musi je wykupic - Dunk nie mial na wykup u ser Uthora (The Hedge Knight;
   AWOIAF "Tourneys", "Thunder (horse)"). Jaime trzymany w Riverrun prawie rok bez okupu (polityka, nie pieniadze); Jaime obiecuje panom Dorzecza, ze wszyscy
   jency z Blizniakow zostana wykupieni (AFFC); Brienne ma byc "warta okupu w szafirach" Tarthu. Lannisterowie: "Lannister zawsze placi swoje dlugi".
   Nocna Straz bierze ludzi z lochow (Yoren w AGOT) - w Westeros niewolnictwo jest zakazane, wiec jeniec-prostak nie ma rynku.

---

## 3. Luki i bledy logiki

| # | Luka | Dowod | Skutek |
|---|---|---|---|
| L1 | **H3 (przegrani uchodza) zatwierdzone 07.10, niezrobione** | STAN-PRAC:438-440 (decyzja), brak w CHANGELOG i kodzie; [P] 46.2% zabitych u przegranych, 13.6% u zwyciezcow w 1 313 bitwach | 185 tys. zabitych lordom rocznie = 1.85 x stanu; 56 tys. zl/d na werbunek; ludnosc wykrwawiana |
| L2 | **Zwyciezca traci za duzo** (13.6% w "prawdziwych bitwach") | linie "Bitwy:" (srednia wazona z roku) | rekrutacja i zold rosna po obu stronach; H3 dotyczy tylko przegranych |
| L3 | **Twoj okup w nicosc** | `PlayerCaptivityCampaignBehavior.cs:244`; RC mnozy go x5 (`Patches.cs:153`) | najwieksze pojedyncze ujscie gracza; porywacz nic nie zyskuje (Z1) |
| L4 | **Dosypka do okupu z niczego** (kurier, gdy AI zbiedniala w 2 dni) | `RansomOfferCampaignBehavior.cs:174-176` | maly, ale zloto z niczego (Z1) |
| L5 | **Czesc ceny u posrednika z niczego** | `SellPrisonersAction.cs:83, 88`; RC zdejmuje tylko nadwyzke (`FairRansom.cs:107-118`) | AUDYT-DZIURY B4; w PROJEKT wiersz 6 - 164 |
| L6 | **Okup AI-AI oderwany od majatku** (3-4% rocznego dochodu glowy rodu, sqrt z kiesy) | rozdz. 1.6 przyklad | wojna nic nie kosztuje rodow przegranych; jency AI niemal darmowi; brak przeplywu, ktory w historii przesuwal majatek |
| L7 | **Dwie skale okupu** (gracz 10-20 x wiecej niz AI-AI za tego samego lorda) | `FairRansom.cs:82` (tylko gracz) | ten sam jeniec ma dwie ceny - "jedna zasada na jedno zjawisko" zlamana |
| L8 | **Stawki RC stale, nie od majatku**: lord bez lenna z biednego rodu tier 4 = 16 tys., tyle co syn bogatego | `FairRansom.cs:101-104` | biedny rod placi nadmiernie (do 0 kiesy przez kurier), bogaty za malo |
| L9 | **Korona nie ma udzialu w zdobyczy**, choc placi 50% zoldu | `KingdomTreasury.cs:134-213` - tylko wyplata | korona finansuje wojne z zapasu (puste skarbce Westeros, PROJEKT 1.2); brak biezacego wplywu z wojny |
| L10 | **Armia powstaje bez rachunku pieniedzy** | SCAI `scai.cs:1235-1258`, BK `BKArmyManagementModel.cs:43-160`, vanilla model | AI zwoluje armie, gdy ma sile, nawet z pusta kiesa i zaleglym zoldem (58 rodow przycietych doby 364) |
| L11 | **Zloto "z cial" dla AI** | `MapEvent.cs:1850-1867` | PROJEKT wiersz 14 (164) - nie dubluje |
| L12 | **Lup z oblezenia z niczego** | `SiegeAftermathCampaignBehavior.cs:158-167, 739-748` | maly (rozdz. 1.5); PROJEKT wiersz 6 (164); przy okazji: historycznie lupienie miasta to ogromny lup z MIASTA (towar, kasa), u nas nic z miasta |
| L13 | **Szeregowi jency sprzedani znikaja z gry** (i zloto z niczego) | `SellPrisonersAction.cs:19-25, 66-89`, `PartiesSellPrisonerCampaignBehavior.cs:41-86` | PROJEKT 164b; w Westeros brak sensownego odbiorcy (niewolnictwo zakazane) |
| L14 | **Mozliwy dubel sprzetu pokonanej zalogi** | DTE kopiuje zbrojownie pokonanych (`dte_ecb.cs:828-853`), kasuje tylko przy zniszczeniu partii (`:510-520`) | do sprawdzenia w logu DTE przy oblezeniu; 51 oblezen rocznie - maly |
| L15 | **Brak licznika okupow i jencow-lordow** | RC log 0 linii w roku; CrashScribe brak | nie da sie zmierzyc przeplywu okupow - w 169 |
| L16 | **Trzy rozne "noce"** (21-5, 22-4, 23-5) | rozdz. 1.7 | zasada "jedna zasada na jedno zjawisko"; Jeff chce 0-6 |

---

## 4. Propozycje

Kazda z wylacznikiem MCM i linia w logu. Liczby w zl (1 zl ~ 1 d). D = dochod staly rodu (PROJEKT 6.1, srednia 28 dob); G = kiesy rodziny.

### W1. H3: przegrani uchodza, zwyciezca traci mniej (P0, duza)
- **Co:** wzor z HISTORIA-RABUNKU-I-BITEW 4.3 bez zmian: przegrany zabity z p = 0.15 + 0.35 C + 0.15 T + 0.15 O + 0.20 Q (+0.25 N), 5-65%;
  jeniec: bohaterowie jak dzis, t4+ 30%, reszta 5%; reszta uchodzi. **Do tego zwyciezca: zabici nie wiecej niz 5%** jego strat ludzi (reszta strat to ranni).
- **Liczby:** cel przegranych 15-30% srednio (dzis 46%), zwyciezcy 2-5% (dzis 13.6%). [S] zabici lordow z 185 tys. do ok. 90-110 tys. rocznie;
  werbunek ok. -30-40% (ok. 17-22 tys. zl/d mniej do notabli i miast - to tez mniejszy wplyw do tych kas, sprawdzic w "Obieg").
- **Gdzie uchodza:** do ksiegi ludzi swojego regionu (108) - do czasu 108: do puli weteranow karczmy najblizszego miasta swojej kultury (167; Z6:
  "zwolniony do wsi albo karczmy"), nigdy do puli wyrzutkow (to dezerterzy). Sprzet uchodzacego zostaje z nim (nie przechodzi do lupu - PROJEKT wymaga tego samego).
- **Gdzie w kodzie:** szanse pojmania i zgonu w `MapEvent` (HISTORIA 4.5 wskazuje `MapEvent.CaptureDefeatedPartyMembers`) - do potwierdzenia w dekompilacji
  przed kodem; autorozstrzyganie RBM/vanilla - sprawdzic, ktory model liczy straty zwyciezcy.
- Zmienia gre gracza: tak (bitwy automatyczne, mniej jencow). Nowa kampania: nie. Ryzyko: wiecej ludzi w regionach -> wiecej ochotnikow; mniej jencow ->
  mniej okupow; zbrojownie pokonanych mniejsze (lup). Zaleznosci: 108 (najlepiej), 167; decyzja Jeffa juz jest (H3 TAK).

### W2. Okup wedlug majatku - jedna regula dla AI i gracza (P1, srednia)
- **Co:** cena bohatera-jenca (wszystkie drogi: barter AI-AI, kurier, posrednik, ekran) = max(cena gry; W x D x 364), gdzie
  W = **lord/dama 0.15, dziedzic albo malzonek glowy 0.3, glowa rodu 0.5, krol 0.5 x (D rodu krola + biezace wplywy korony)**; towarzysze x0.3 BK bez zmian.
  Placacy: glowa rodu jenca z G ponad 5 000 (FamilyPurseFloor PROJEKT); **gotowka najwyzej 50% (G - 5 000)**, reszta jako dlug okupu na raty
  **10% D dziennie** wobec rodu porywacza (ta sama ksiega co 168; RC ma juz "dlug honorowy" gracza - `CaptivityBehavior.cs:380-450`). Zakaz wzrostu
  x3.4 za czas niewoli dla kwoty platnej (czas zwieksza tylko szanse zgody), bo cena wynika z majatku, nie z cierpliwosci.
- **Uzasadnienie liczb:** 0.5 roku dla glowy - konwencja "na miare majatku, bez ruiny" (Ambuhl) i decyzja Jeffa "nikt nie bankrutuje" (historyczne 1-50 lat
  dochodu rujnowaly rody - tego nie chcemy w AI); lord 0.15 i dziedzic 0.3 - czlonek rodu jest wart ulamek glowy; 50% gotowki i raty 10% D -
  rata miesci sie w czesci budowy/dworu jak w IronBankMaxInstalmentShare (PROJEKT 8.2). Przyklad [S]: rod tier 4 z D ok. 600/d: glowa 109 tys.
  (dzis AI-AI ok. 6 tys., u gracza 50-80 tys.), lord 33 tys. (dzis AI-AI ok. 2-3 tys., u gracza 16 tys.).
- **Gdzie:** `FairRansom.LordPrice` (`FairRansom.cs:71-139`) - usunac ograniczenie `:82` (tylko gracz) za wylacznikiem `LordRansomByIncome`;
  D z 169/166 (do tego czasu D(0) = G/60, PROJEKT K14). Kurier: prefiks na `RansomOfferCampaignBehavior.AcceptRansomOffer` - placi gotowke + raty, bez dosypki (L4).
- Zmienia gre gracza: **tak** (Twoje okupy: bogaci drozej, biedni taniej) - pytanie do Jeffa. Nowa kampania: nie. Ryzyko: AI-AI barter z wieksza cena moze
  czesciej sie nie domykac (zloto w barterze ograniczone kiesa) - dlatego gotowka 50% + raty; sprawdzic w logu liczbe lordow w niewoli > 60 dni (cel: nie wiecej
  niz dzis + 20%). Prison Is Not Fun: bogatszy jeniec dluzej trzymany zyje - spojne. Zaleznosci: 169 (D, licznik okupow), 168 (ksiega dlugow), 165 (krol).

### W3. Twoj okup do porywacza (P1, mala)
- **Co:** prefiks na `PlayerCaptivityCampaignBehavior.game_menu_captivity_end_by_ransom_on_consequence` (`:241-246`): zamiast `GiveGoldAction(gracz, null)`
  - do wodza partii porywacza; partia bez wodza - glowa jej rodu; loch osady - pan osady; banda - kasa jej kryjowki (OutlawLaw "HoardChange").
- Kwota bez zmian (RC). Zmienia gre: nie (placisz tyle samo). Nowa kampania: nie. Ryzyko: male (jedna metoda gry, prywatna - nazwa po `AccessTools`).
  Test: reczny (autotest nie wpada w niewole). Zaleznosci: brak.

### W4. Prawo trzecich - korona bierze trzecia z wojny (P1, srednia)
- **Co:** rod w krolestwie, ktore jest w wojnie i placi mu zwrot zoldu, oddaje skarbcowi **1/3** z: okupow otrzymanych (W2), sprzedanego lupu
  (`SellItemsAction` lordow AI z lupu - dzis ok. 47 tys./d) i lupu z oblezenia (po 164 - z kasy miasta). Jeniec-krol albo wodz armii wroga: **2/3 do skarbca,
  1/3 dla tego, kto pojmal** ("chief of the war" za odszkodowaniem, indenture 1355).
- **Liczby:** 1/3 - kapitan oddawal krolowi 1/3 swoich zyskow (Hay 1954); [S] z lupu ok. 15 tys./d do skarbcow + okupy (nieznane, po 169).
- **Gdzie:** jeden przelew w miejscu wyplaty (postfiks na sprzedaz lupu AI, nasz W2, 164 oblezenie), do linii "Obieg" jako wplyw korony.
- Zmienia gre gracza: **tak, gdy jestes wasalem** (Twoje okupy i lup -1/3) - pytanie do Jeffa; Jeff 08.10 powiedzial, ze jako wasal placi koronie jak AI -
  to ta sama logika. Nowa kampania: nie. Ryzyko: korona z wieksza kasa w wojnie (zamierzone - biezacy wplyw do zwrotu, PROJEKT 7.1). Zaleznosci: 165, W2.

### W5. Armia tylko za pieniadze, ktore sa (P2, mala, po 166)
- **Co:** postfiks na `CanLordCreateArmy` (najbardziej zewnetrzny - SCAI dziedziczy po vanilli, BK ma wlasny): nie, gdy rod wodza ma G < R (rezerwa wojny,
  PROJEKT 6.1) albo zalegly zold; czlonek nie wchodzi do armii, gdy jego rod jest w zaleglosci. Spojnosc armii **-2 dziennie za kazdy rod czlonka w zaleglosci**
  (historycznie nieoplacona armia sie rozchodzila; SCAI ma juz -3.5 za linie zaopatrzenia - ta sama skala).
- Zmienia gre: AI rzadziej zbiera armie w biedzie. Nowa kampania: nie. Ryzyko: mniej oblezen (StrategicCampaignAI - PROJEKT ryzyko 6). Zaleznosci: 166.

### W6. Zmeczenie wojna od pieniedzy (P2, srednia)
- **Co:** codziennie do zmeczenia wojna Diplomacy (wlaczone u Jeffa) dodac: +0.5 gdy skarbiec krolestwa < 0.25 mln, +0.02 na kazdy procent zoldu
  krolestwa przycietego/zaleglego. To nie "wojna falami" (H4 odrzucone), tylko skutek: pusty skarbiec pcha do pokoju jak straty.
- **Liczby:** 0.5/d = dwa razy dzisiejsze tempo czasu (0.25/d) - bieda konczy wojne w ok. pol roku zamiast roku.
- Gdzie: API Diplomacy (`WarExhaustionManager` - **niesprawdzone w dekompilacji**, sprawdzic przed). Zmienia gre: tak (krotsze wojny biednych). Pytanie do Jeffa.
  Zaleznosci: 165 (skarbiec z biezacych wplywow).

### W7. Jedna noc 0-6 (P1, mala) - patrz rozdz. 5, T1.

### Nowe rzeczy, ktorych nie mamy (gra pozwala)

| # | Co (slowami gracza) | Historia / lore | Jak w grze | Prio |
|---|---|---|---|---|
| N1 | **Pomoc wasali na okup pana**: gdy glowa rodu albo krol nie ma na okup, wasale (rody krolestwa z lennami) daja do 5% swojego G ponad rezerwe, proporcjonalnie do lenn | jedna z trzech pomocy feudalnych (Magna Carta kl. 12) | rozszerzenie W2: brak gotowki -> zbiorka u wasali przed ratami; BK `RansomDuty` (gracz) juz to robi dla gracza - ta sama zasada dla AI | P2 |
| N2 | **Okup na raty z zakladnikiem**: przy racie dlugu czlonek rodu (nie glowa) zostaje u porywacza do splaty | poreczyciele i zakladnicy (Moleyns, Ambuhl); Theon jako "wychowanek" Starkow | dlug okupu (W2) + jeniec z rodziny w lochu porywacza; ucieczka zakladnika = zerwany dlug | P2 |
| N3 | **Okup miasta**: oblegajacy proponuje miastu wykup (kasa miasta + notable) zamiast szturmu; miasto placi i oblezenie zdjete na 60 dni | miasta wykupywaly sie od szturmu i lupu (wojna stuletnia) | nowa opcja AI oblezenia: gdy kasa miasta > koszt szturmu w ludziach x zold; zloto prawdziwe z kasy miasta (Z1) | P2 |
| N4 | **Appatis - okup okolicy zamiast palenia wsi**: zaloga albo armia na wrogiej ziemi pobiera od wsi okupu (kiesa wsi) zamiast rabunku; wies nie plonie | appatis w Normandii i Bretanii (zalogi zyly z okupu okolicy) | alternatywa do rabunku osada po osadzie (projekt wiosek): wies placi 10-20% kiesy, plony zostaja | P2 |
| N5 | **Jency z Westeros dla Nocnej Strazy**: szeregowy jeniec w Westeros nie idzie do pasera, tylko "przywdziewa czern" (pan zyskuje relacje albo wplyw, nie zloto) | Yoren zabiera ludzi z lochow; niewolnictwo zakazane | `SellPrisonersAction` w osadzie Westeros -> ludzie do Strazy (ksiega ludzi, decyzja Jeffa: Straz bez zoldu); Essos - targ niewolnikow jak dzis | P1 (lore, maly) |
| N6 | **Turniejowy okup konia i zbroi** (GrandTourney): przegrany kopijnik oddaje konia i zbroje albo wykupuje je za ich wartosc w stanie | The Hedge Knight (Dunk, ser Uthor) | GrandTourney: przy porazce w kopii gracza i lordow AI - przelew rzeczy albo zlota | P2 |
| N7 | **Udzial ludzi w lupie**: z ceny lupu sprzedanego przez lorda AI 1/2 do sakiewek jego ludzi (2/3 zostaje zolnierzowi w historii, z tego czesc oddaje) | trzecie (Hay 1954) | po 163 (markietani), bo sakiewki dzis puchna | P2 |
| N8 | **Licznik okupow i jencow-lordow** w logu | - | dopisac do 169: jency-lordowie (liczba, dni w niewoli), okupy AI-AI (kwoty, kto komu), okupy gracza, dosypki gry | P0 (w 169) |

### Tabela zbiorcza

| # | Co | Prio | Wielkosc | Zmienia gre gracza | Nowa kampania | Ryzyko | Zalezy od |
|---|---|---|---|---|---|---|---|
| W1 | H3 przegrani uchodza, zwyciezca <= 5% | P0 | duza | tak | nie | srednie | 108 / 167, decyzja Jeffa jest |
| N8 | licznik okupow i jencow | P0 | mala | nie | nie | male | 169 |
| W3 | Twoj okup do porywacza | P1 | mala | nie | nie | male | - |
| W7/T1 | noc 0-6 dla wszystkich | P1 | mala | tak (godziny) | nie | male | - |
| W2 | okup wedlug majatku, raty | P1 | srednia | tak | nie | srednie | 169, 168, 165 |
| W4 | trzecia dla korony | P1 | srednia | tak (wasal) | nie | male | 165, W2 |
| N5 | jency Westeros do Strazy | P1 | mala-srednia | tak | nie | male | 108, 164b |
| W5 | armia tylko z pieniedzmi | P2 | mala | nie | nie | srednie | 166 |
| W6 | zmeczenie wojna od biedy | P2 | srednia | tak | nie | srednie | 165, Diplomacy |
| N1-N4, N6, N7 | pomoc wasali, zakladnik, okup miasta, appatis, turniej, udzial ludzi | P2 | srednie | tak | nie | srednie | W2, 113/wioski, 163 |

---

## 5. Do zrobienia tej nocy vs na pozniej

### Tej nocy (male, bezpieczne, sprawdzalne autotestem 40 dob)

**T1. Jedna noc 0-6 (prosba Jeffa "od 24 do 6").**
- Kod: `Armoury/src/NightRest.cs` - jedna stala (albo dwa ustawienia `CampFromHour` 0, `CampToHour` 6) i warunek `h < 6` w:
  `:406` (AI obozuje), `:852` (namioty przy graczu), `:861` (trzymanie spiacych), `:107`, `:250`, `:1188`, `:1206` (noc gracza: sen i pytanie o oboz -
  dzis 21-5). Opisy MCM po angielsku: `Settings.cs:693` "(22-4)" -> "(0-6)", `:703` ("at dusk" -> "at midnight"). Bandy (`:511-512`, natury dzienne/nocne)
  **bez zmian** - to ich zwyczaj, nie oboz armii. `TerrainEase` (kara nocy wg `Campaign.IsNight`) bez zmian.
- Uwaga sen: noc 6 h przy `SleepHoursNeeded` 6 (Armoury.json) - kolumna, ktora przespi cala noc, nie ma dlugu snu; dzis noc gracza ma 9 h okna.
- Sprawdzenie w logu 40 dob: linie "AiNightCamp: H:00 - spi N" tylko dla H = 0..5; liczba spiacych lordow podobna jak dzis w szczycie; "Audyt predkosci"
  - dobowy przebieg AI rosnie o ok. 1/18 (7 h postoju -> 6 h) [S]; zero bledow NightRest.
- Ryzyko: male (zmiana warunku godzin). Kontrola zasady 0: `RefreshNearbyTents`/`HoldSleepers` musza miec ten sam warunek (inaczej namioty bez spiacych);
  zapis gry bez nowych kluczy (pytanie o oboz gracza trzyma wybor w zapisie - bez zmian). Jesli osobny raport o obozach (ten sam katalog) proponuje
  inne godziny - wybrac jedna wersje, nie wgrywac dwoch.

**T2 (tylko propozycja dla 169, nie osobna paczka).** N8: licznik okupow i jencow-lordow - dopisac do linii 169 (`OnGoldTraded` juz widzi okupy - rozpoznanie
169 wynik 1:215); bez tego W2/W4 nie maja pomiaru.

Nic wiecej na dzis: L11/L12/L5/L13 (zloto z niczego z cial, oblezen, posrednika, jencow) **nie wylaczac osobno** - PROJEKT K8: zrodla zdejmujemy dopiero po
zatkaniu ujsc (164a), inaczej swiat traci zloto.

### Male, ale test reczny (nie autotest)
- **W3** Twoj okup do porywacza (`PlayerCaptivityCampaignBehavior.cs:244`, RealisticCaptivity) - test: dac sie pojmac, zaplacic, sprawdzic kiese porywacza w logu.
- **L4** kurier bez dosypki z niczego (`RansomOfferCampaignBehavior.cs:174-176`): gdy placacy AI ma za malo - placi, ile ma ponad 5 000, reszta raty (z W2) albo oferta przepada.

### Na pozniej (kolejnosc)
1. W1 (H3) - po 108 albo z tymczasowym ujsciem do karczmy (167); przed kodem sprawdzic w dekompilacji, gdzie gra losuje zgon/pojmanie/ucieczke w bitwie automatycznej.
2. W2 + W4 - po 169 (D, licznik) i 165; razem z 168 (raty).
3. N5 - z 164b (jency jako ludzie).
4. W5, W6 - po 166 / 165.
5. N1-N4, N6, N7 - po W2 i projekcie wiosek (N4) / 163 (N7).
6. L14 - sprawdzic w logu DTE (`Modules/DynamicTroopEquipmentReupload/.../log.txt`) przy zdobyciu osady, czy zbrojownia zalogi zostaje i jednoczesnie idzie do zwyciezcy.

---

## 6. Kolizje z praca rownolegla i z PROJEKT

- **169 (log obiegu):** W2/W4 potrzebuja z 169 D na rod i licznika okupow (N8) - prosba o dopisanie, nie osobna paczka. Brak kolizji kodu.
- **170 (BetterEconomy):** brak styku.
- **171 (zbrojenie zalog):** W1 zmienia, ile sprzetu pokonanych przechodzi do zwyciezcow (uchodzacy zabieraja swoje) - mniej "darmowego" sprzetu w zbrojowniach,
  wiecej zakupow w miastach (zamki kupuja w miescie - 171). Kierunek zgodny, ale po W1 sprawdzic popyt na bron w "Rynek broni".
- **PROJEKT 164a:** L11 (zloto z cial z sakiewek poleglych) i L12 (oblezenie z kasy miasta) - zostaja tam. W4 dokleja do 164 jeden przelew (1/3 do skarbca).
- **PROJEKT 165:** W4 i W6 wzmacniaja "biezace wplywy korony" - nowy wiersz w tabeli 4.1 (trzecie z wojny). **Uwaga Z8:** gracz-wasal nie zarabia na trzecich
  (oddaje, nie dostaje) - Z8 nie zagrozone.
- **PROJEKT 166:** W5 uzywa R i zaleglosci z budzetu; zwolnienia 6.2 a W1 - uchodzacy z pola to nie zwolnieni (nie liczyc w "Budzet rodow: zwolnieni").
- **PROJEKT 168:** dlug okupu (W2, N2) - ten sam wiersz dlugu, wierzyciel = rod porywacza; zajecie (D3) obejmuje tez dlug okupu; bez umorzenia (decyzja Jeffa).
- **PROJEKT 8.2:** AI moze pozyczac na okup glowy/dziedzica - W2 zaklada najpierw gotowke 50% i raty, Bank dopiero, gdy rod ma nadwyzke.
- **RealisticCaptivity:** W2 przepisuje `LordPrice` - stare stawki (`LordRansomKing/ClanLeader/Lord`) zostaja jako podloga przy wylaczonym `LordRansomByIncome`.
  Pliku MCM RC u Jeffa nie ma - zmiana domyslnych dziala od razu (inaczej niz Armoury.json).

---

## 7. Zrodla

Kod [K]: `Armoury/src/SoldierPay.cs:13-41`; `MountedWage.cs:14-38`; `KingdomTreasury.cs:83-84, 134-213`; `DesertionLaw.cs:12-41`; `WarLedger.cs:12-24, 80, 141`;
`BattlefieldLaw.cs:233-237, 559-569`; `SellByCondition.cs:211`; `Rations.cs:51-71`; `HungerLaw.cs:10-24`; `NightRest.cs:65, 107, 250, 395, 406, 448, 511-512, 852, 861, 1188, 1206`;
`Settings.cs:213, 232, 303, 641, 693, 697, 703`; RealisticCaptivity `FairRansom.cs:32, 41-51, 71-139, 161-186`, `Patches.cs:141-171`, `CaptivityBehavior.cs:380-450`,
`Settings.cs:37-54`. Gra 1.4.8: `DefaultRansomValueCalculationModel.cs:9-55`; `SellPrisonersAction.cs:10-116`; `SetPrisonerFreeBarterable.cs:39-70`;
`SetPrisonerFreeBarterBehavior.cs:20-54`; `RansomOfferCampaignBehavior.cs:64-180`; `BarterManager.cs:91-119`; `PlayerCaptivity.cs:164-172`;
`PlayerCaptivityCampaignBehavior.cs:241-246`; `PartiesSellPrisonerCampaignBehavior.cs:24-86`; `MapEvent.cs:1850-1867`; `DefaultBattleRewardModel.cs:173-190`;
`SiegeAftermathCampaignBehavior.cs:95-118, 158-167, 646-665, 739-753`; `DefaultArmyManagementCalculationModel.cs:32-46`. BK: `VanillaModelTweakPatches.cs:1211-1232`;
`BKArmyManagementModel.cs:43-160`; `BKPrisonerModel.cs`. StrategicCampaignAI145 (ilspy): `StrategicArmyManagementModel`, `StrategicAiTuning`. DTE 1.4.7 (ilspy):
`EveryoneCampaignBehavior` (`DistributeLootRandomly`, `GetAllLootItems`, `OnMobilePartyDestroyed`). Prison Is Not Fun 1.3 (ilspy): `HeroTakenPrisoner.GetChance`.

Pomiary [P]: `Armoury-2026-10-08_08-18-38.log` (linie Zold, Korona, Bitwy, Ludzie, Werbunek, WarLedger, Spoils of War (128), Skup sprzetu, Zuzycie AI, Paser,
Wyrzutki, AiNightCamp); `economy-2026-10-08_08-20-02.csv` (skrypt `scratchpad/w04/csv1.py`); `session-2026-10-08_08-18-37.log` (KRONIKA WOJEN);
`RealisticCaptivity-2026-10-08_08-18-38.log` (brak okupow); `Armoury.json`, `DiplomacySettings_v1.2.json`.

Historia i lore [H]:
- [Indentures and the King's Army - Historical Britain Blog](https://historicalbritainblog.com/indentures-and-the-kings-army/) (trzecie, jency wysokiej rangi dla krola)
- [D. Hay, The Division of the Spoils of War in Fourteenth-Century England, TRHS (Cambridge)](https://www.cambridge.org/core/journals/transactions-of-the-royal-historical-society/article/division-of-the-spoils-of-war-in-fourteenthcentury-england/6B3E7955D263072DCF5D7560DDB44527)
- [Indenture Edward III - Czarny Ksiaze, Gaskonia 10.07.1355 (Journal of Medieval Military History, Cambridge)](https://www.cambridge.org/core/books/abs/journal-of-medieval-military-history/indenture-between-edward-iii-and-the-black-prince-for-the-princes-expedition-to-gascony-10-july-1355/FA54D74F190B50E51F066E796CCA3C2E)
- [Fighting the Hundred Years' War: war indentures - Essex Record Office](https://www.essexrecordofficeblog.co.uk/tag/archers/)
- [Ransom Brokerage in the Fifteenth Century (OpenEdition)](https://books.openedition.org/irhis/1147) (Rempston, Clifton, Moleyns, Holland, Knyvett, Cornwall)
- [R. Ambuhl, Prisoners of War in the Hundred Years War - medievalists.net](https://www.medievalists.net/2013/01/ransoming-prisoners-of-war-became-widespread-in-the-hundred-years-war-new-book/);
  [Southampton 2013](https://www.southampton.ac.uk/news/2013/01/soliders-in-the-late-middle-ages.page)
- [Ransom of John II of France - Wikipedia](https://en.wikipedia.org/wiki/Ransom_of_John_II_of_France); [Battle of Auray - Wikipedia](https://en.wikipedia.org/wiki/Battle_of_Auray);
  [Bertrand du Guesclin - Britannica](https://www.britannica.com/print/article/248370); [Charles, Duke of Brittany - Wikipedia](https://en.wikipedia.org/wiki/Charles,_Duke_of_Brittany)
- [Feudal aid - Wikipedia](https://en.wikipedia.org/wiki/Feudal_aid); [Magna Carta kl. 12 - Magna Carta Project](https://magnacarta.cmp.uea.ac.uk/read/magna_carta_1215/Clause_12?com=aca)
- [AWOIAF: Tourneys](https://awoiaf.westeros.org/index.php/Tourneys), [Thunder (horse)](https://awoiaf.westeros.org/index.php/Thunder_(horse)),
  [Plot to free Jaime Lannister](https://awoiaf.westeros.org/index.php/Plot_to_free_Jaime_Lannister), [A Feast for Crows - Chapter 44](https://awoiaf.westeros.org/index.php/A_Feast_for_Crows-Chapter_44)
- W repo: `docs/HISTORIA-RABUNKU-I-BITEW-2026-10-07.md` (4.1-4.5 straty w bitwach), `docs/AUDYT-WOJNA-LOGISTYKA.md` (1.4 koszt wojny, 10 jedzenie),
  `docs/ZRODLA-DOCHODU.md` (pomoce feudalne, okupy), `docs/PROJEKT-EKONOMIA-OBIEG-2026-10-08.md`.
