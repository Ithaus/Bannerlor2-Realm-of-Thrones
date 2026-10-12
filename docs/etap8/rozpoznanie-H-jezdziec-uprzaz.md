# ROZPOZNANIE E8/H - jezdziec = kon + uprzaz (12.10.2026)

Status: SAMO ROZPOZNANIE. Kod, gra, ustawienia i zapisy nietkniete, nic nie budowane, gra nieuruchamiana.
Decyzja Jeffa 12.10 (wiazaca, PROJEKT-ETAP8 rozdz. 6 odp. 7): "sam kon nie wystarczy - musi miec tez uprzaz (siodlo), zeby wojsko na nim jezdzilo
i mozna bylo stworzyc jezdzca". Regula docelowa: jezdziec = kon + UPRZAZ (ItemTypeEnum.HorseHarness), jedna regula dla AI i gracza, uprzaz z prawdziwej
produkcji, zamknieta ekonomia, pomiar skutku dla udzialu jazdy.

Oznaczenia: **K:** = `SCR\zb\58\Armoury\src` (galaz `w-toku/zbior-5-8`, glowa 35fb7da), **KC:** = `SCR\zb\58\CrashScribe\src`,
**DTE:** = `SCR\audyt-zloto\DTE` (dekompilacja DLL v1.4.7 z 21.08 - ten sam plik, ktory jest dzis w grze; nie dekompilowalem drugi raz),
**G:** = `SCR\dec32\cs_full` (gra 1.4.8), SCR = scratchpad sesji ae222cb7. **L:N** = `Modules\Armoury\Armoury-2026-10-12_02-44-09.log` (120 dob, WGRANIE 33),
**LK** = `Modules\Armoury\Logs\2026-10-12_02-44-09\konie-krolestwa.csv`, **LW:N** = `...\warsztaty.log`, **ID** = `CrashScribe\items-dump.csv`,
**XML** = `Modules\ROT-Content\ModuleData\ROT-Troops.xml`. [K] kod, [P] pomiar z logu, [S] szacunek, [W] wniosek z kodu (niesprawdzony w grze).

**UWAGA O LOGU:** WGRANIE 33 NIE zawiera paczek E6/R3 (215: uprzaz dla jezdzcow t4+, przewoz wozem) ani E6/K3 (219: jazda wedlug szablonu, kon rycerza).
W zadnym logu na dysku nie ma linii "Uprzaz (215)" ani "Rycerze i jazda (219)" (0 trafien). Liczby ponizej to stan PRZED R3 i K3 - zakupy uprzezy przez
lordow nie byly jeszcze nigdy zmierzone w grze.

---

## 0. Streszczenie prostym jezykiem

1. **Dzis sam kon wystarcza wszedzie.** Uprzaz jest wymagana tylko w jednym miejscu: gdy notabl awansuje ochotnika na konnego (kupuje mu konia I rzad).
   Lord AI awansujacy piechura na jezdzca oddaje tylko konia. Gracz na ekranie druzyny tez tylko konia. W bitwie jezdziec z koniem, ale bez uprzezy,
   jedzie na oklep; pieszo idzie tylko ten, kto nie ma konia.
2. **Nie ma jednego miejsca decyzji "jedzie konno".** Sa trzy: (a) rachunek stanu koni partii na mapie (`Stables.Balance`), (b) bitwa gracza
   (rozdanie DTE + nasza straz przy wystawieniu czlowieka `GarrisonKit.SpawnGuard`), (c) tworzenie jezdzca - 7 drog (awans AI, awans gracza, notabl,
   najemnik, zamiana ROT, dorobek startowy, przeniesienie z zalogi). W autobitwach AI z AI gra liczy "konnego" wedlug typu jednostki - tam ani brak konia,
   ani brak uprzezy nie ma zadnego skutku.
3. **Uprzezy jest za malo na wszystkich jezdzcow.** Doba 120: jezdzcow w partiach lordow i zalogach 10 799, z koniem 7 711, uprzezy w zbrojowniach
   najwyzej ok. 6 470, na polkach 1 253, warsztaty robia 13 sztuk na dobe, a ok. 9 sztuk na dobe znika. Przy samym wymogu, bez zmian w podazy, konno jechaloby
   najwyzej ok. 84% dzisiejszych jezdzcow z koniem, a jazda swiata nie mialaby z czego rosnac (K3 chce ok. 20%, czyli ok. +11 tys. jezdzcow).
4. **Podaz da sie podniesc, ale tylko razem z E8/C.** Siodlarze maja rece (688 roboczodni na dobe), ale w 41-49 miastach linia siodel stoi, bo siodlo ROT
   "kosztuje" 152 dni pracy (zla waga z danych ROT) i nikt go nie kupuje. E8/C (waga prawdziwa) plus prawdziwe zakupy lordow to zmieniaja.
5. **Zalecenie: wariant A** - para kon + uprzaz sprawdzana jedna funkcja w trzech punktach (tworzenie jezdzca, wystawienie w bitwie, potrzeba zakupu),
   razem z zakupem najtanszego siodla dla KAZDEGO jezdzca bez uprzezy i z domknieciem przeciekow uprzezy. Kolejnosc: po E8/C, E8/Z, E8/T, E8/L.

---

## 1. Jak dzis powstaje i utrzymuje sie jezdziec

### 1.1 Fundament - cudzy kod

| Fakt | Dowod |
|---|---|
| Gra zna JEDEN wymog przedmiotu przy awansie: kategorie (`UpgradeRequiresItemFromCategory`). Czytaja ja: bramka ekranu druzyny, zaplata, model awansu AI | G PartyScreenLogic.cs:597, :666-677, :923-925; G DefaultPartyTroopUpgradeModel.cs:107-123 |
| DTE zeruje ten wymog (prefiks zwraca null) - u DTE kon ma byc tylko w zbrojowni | DTE Patches\GetUpgradeRequiresItemFromCategoryPatch.cs:7-14 |
| "Jezdziec" dla DTE = wzorzec jednostki ma konia w slocie 10 (i misja wpuszcza konie) | DTE Assignment.cs:209-220 (`IsMounted`) |
| DTE w bitwie paruje konie z uprzeza wedlug RODZINY (kon/wielblad/slon), pary z uprzeza ida pierwsze; kon BEZ uprzezy tez jest rozdawany - uprzaz dochodzi tylko "jesli jest" | DTE PartyEquipmentDistributor.cs:465-473, :529-537, :559-598, :662-694; HorseAndHarness.cs:33-62 |
| DTE dla partii innych niz gracza dopelnia puste sloty wzorcem (takze kon i uprzaz z niczego) | DTE Assignment.cs:377-438 (`FillEmptySlots`); Patches\SpawnAgentPatch.cs:85-88 |
| Komplet rekruta DTE zawiera konia i uprzaz ze wzorca (gracz dostaje je za darmo przy kazdym werbunku) | DTE Patches\RecruitmentPatch.cs:56-72, :119-131; TavernMercenaryEquipmentPatch.cs:15-22; TownMenuMercenaryEquipmentPatch.cs:36-43 |
| W oblezeniu, kryjowce i na morzu koni sie nie uzywa: DTE nie dopelnia slotow 10-11 i nie zdejmuje ich ze zbrojowni (na morzu w ogole nie paruje) | DTE Assignment.cs:380-382, :426-437; PartyEquipmentDistributor.cs:100, :627-639, :1317-1337; ArmyArmory.cs:167-195 |
| Autobitwa: sila "konnego" z typu jednostki (`troop.HasMount()`), nie ze zbrojowni | G DefaultMilitaryPowerModel.cs:260-271; K: brak latki na konie w symulacji (jedyna latka symulacji: K:NorthHomeEdge.cs:233-247) |
| CrashScribe: kon ponad Jazde zolnierza schodzi, uprzaz idzie za koniem; uprzaz innej rodziny wraca na polke; wielblady tylko u swoich | KC:Mends.cs:1379-1443, :565-587 |
| Uprzaz spoza rodziny wierzchowca zdejmowana przed budowa siatki (inaczej AccessViolation) | K:MountMeshGuard.cs:20-32; L:142 |

### 1.2 Sciezki - czy sam kon wystarcza

| Sciezka | Jak dzis | Sam kon wystarcza? | Dowod |
|---|---|---|---|
| **(a) AI: awans piechura na jezdzca** | lista celow: cel konny odpada bez konia wlasciwej kategorii (t2-3 zwykly, t4-5 bojowy, t6 szlachetny albo bojowy); przy awansie kon z taboru idzie do zbrojowni partii; awans konny -> konny za darmo | **TAK** - uprzezy nikt nie sprawdza | K:Stables.cs:202-212, :682-730, :740-774, :148-155; L:54 |
| (a) AI: stan koni partii | `Stables.Balance`: konie pod siodlo w zbrojowni DTE wobec konnych szeregowych (z rannymi), wynik "bez konia" i "wolne" | TAK - liczy tylko konie | K:Stables.cs:301-354; K:HorseState.cs:148-155 |
| (a) AI: Remount (dosadzanie) | przy kazdym postoju lord przenosi konie z taboru do zbrojowni pod jezdzcow bez konia, nadmiar wraca do taboru; ma WLASNY rachunek (wzorzec z koniem pod siodlo wobec koni w zbrojowni), nie `Balance` | TAK | K:Stables.cs:527-544, :550-618 |
| (a) AI: Stajnia (zakup koni) | miasto i wies, z kiesy wodza; zapas wedlug kategorii `Stables.Reserve` (brak + luz 10%); przerwa miedzy zakupami | TAK - kupuje tylko konie | K:Stables.cs:391-427, :791-1019 |
| (a) AI: K3 kon rycerza (`KnightBuy`) | stado wsi rodu, potem targ; z budzetu sprzetu rodu; cel udzialu jazdy = udzial typow konnych t3+ w szablonie | TAK; udzial jazdy liczony po TYPIE jednostki, nie po koniu | K:Stables.cs:1032-1142; K:CavalryTemplate.cs:97-143, :149-186 |
| (a) AI: uprzaz R3 (`AiBuysBarding`) | lord (nie zaloga) dokupuje uprzaz jezdzcom tieru >= 4, ktorych WZORZEC ma uprzaz; koszyk = typ x tier uprzezy wzorca; kupowana na koncu, z reszty budzetu; tylko w miescie | uprzaz NIE jest wymogiem - jest dokupowana, gdy stac; jezdziec t3 pominiety; siodlo t1 nie pokrywa potrzeby rycerza (kandydaci tylko tier t i t-1) | K:AiGear.cs:305-311, :319-384, :583-600, :781-782, :816-817; K:Settings.cs:1497 |
| (a) AI: werbunek ochotnika od notabla | do zbrojowni ida zapisane rzeczy ochotnika: komplet wzorca (dorobek startowy puli) albo dobytek + to, co kupil notabl | zalezy od zapisu | K:RecruitKit.cs:474-530, :533-563 |
| (a) notabl awansuje ochotnika na konnego (VolunteerKit) | kon I uprzaz sa czesciami KLUCZOWYMI - bez obu na polce (ten sam typ, tier nie nizszy niz we wzorcu) awans cofniety | **NIE - tu uprzaz jest juz wymagana** (jedyne takie miejsce) | K:VolunteerKit.cs:166-188 (:170-172), :238-270; L:36029 "notable ... konie i rzedy 6/6701 zl"; L:36040 "ochotnicy na konnego u notabli: kupiony kon 3, cofnieci 68" |
| (a) AI: najemnik konny z karczmy | kon z puli koni weteranow miasta (K1), uprzaz ze WZORCA (z niczego - "poza zakresem K1") | kon prawdziwy, uprzaz z niczego | K:RecruitKit.cs:502-519; K:HorseFate.cs:759-801; L:35819 |
| (a) AI: jeniec przyjety do wojska, ochotnik bez zapisu | bez niczego / dobytek; konny typ bez konia | bez konia i bez uprzezy | K:RecruitKit.cs:482-488, :498-501 |
| (a) AI: zamiana ROT pieszy -> konny | kon z taboru albo wolny ze zbrojowni, inaczej pieszy tego samego tieru | TAK - sam kon | K:RotHorseGuard.cs (naglowek :15-36); LK kolumny rot_plus / straz_* |
| (a) AI: nowa partia | bez darmowego kompletu DTE; pierwsze konie i zywnosc z polki siedziby za zloto wodza | sam kon; uprzezy brak | K:LevyGold.cs:171-184; K:HorseFate.cs:50-52; L:341 |
| (a) start kampanii (ColdStart) | partie lordow i zalogi dostaja brakujace sztuki kompletu ludzi; przy `TroopsFightWithOwnKitOnly` takze konia (slot 10) i uprzaz (slot 11, tylko z koniem) | **para z dorobku startowego juz dzis** | K:ColdStart.cs:76-126 (:81, :95); L:231 |
| **(b) zalogi** | Stajnia ich nie obsluguje (Remount i zakup tylko partia lorda z wodzem); uprzezy nie kupuja (`!garrison`); konie i uprzaz maja z dorobku startowego i od ludzi zostawionych przez lorda (komplet idzie z czlowiekiem, przy `TroopsFightWithOwnKitOnly` z koniem i rzedem); nadwyzki koni i uprzezy NIE sa sprzedawane | sam kon; w oblezeniu i tak nikt nie jedzie | K:Stables.cs:554, :797; K:AiGear.cs:781; K:GarrisonArmory.cs:783-794, :838-845; K:MenPurse.cs:101, :530, :557 |
| **(c) gracz: awans** | nasz postfiks przywraca wymog konia po prefiksie DTE; gra zabiera konia z taboru; po zatwierdzeniu ekranu kon trafia do zbrojowni DTE gracza | **TAK** - uprzezy nikt nie sprawdza | K:Stables.cs:96-111, :47-89, :1398-1407 |
| (c) gracz: werbunek | DTE doklada do zbrojowni gracza caly komplet wzorca, z koniem i uprzeza, za darmo (E8/T to zmienia) | kon i uprzaz z niczego | DTE RecruitmentPatch.cs:56-72; K:RecruitKit.cs:479; K:AiGear.cs:230 |
| (c) gracz: kwatermistrz | potrzeba "kon" i "uprzaz" = liczba jezdzcow (tylko raport brakow, bez skutku) | - | K:QuartermasterLaw.cs:150-152, :299-301, :834 |
| (c) gracz: StartKit | zamiana sztuk, ktorych bohater nie udzwignie; konia zamienia bez patrzenia na uprzaz w slocie 11 | dotyczy BOHATERA, nie wojska | K:StartKit.cs:56-71, :82-94 |
| **(d) bitwa gracza (misja)** | DTE rozdaje pary; nasze latki: bez dopelniania wzorcem i bez zestawu awaryjnego; straz przy wystawieniu: kazda sztuka slotow 0-3 i 5-11 musi miec pokrycie w zbrojowni partii | patrz 1.3 | K:GarrisonKit.cs:896-942, :951-966, :1063-1069, :1207-1271; K:DressCode.cs:50, :117 |

Ustawienia, na ktorych to stoi: `TroopsFightWithOwnKitOnly` true (K:Settings.cs:198; w pliku Jeffa klucza nie ma - dziala domyslna), `CavalryNeedsMounts` true (J:334),
`AiBuysMounts` true (J:327), `WarHorseFromTier` 4 (J:336), `NobleHorseFromTier` 6 (J:335), `AiBuysBarding` true, `AiSellsSpareHorses` true, `CavalryShareByTemplate` true,
`KnightHorseFromHouse` true, `VeteranKeepsHorse` true, `HorsesDieOnlyKilled` true (K:Settings.cs:1475-1521; w pliku Jeffa brak - dzialaja domyslne). L:276 potwierdza: "kon tylko ze zbrojowni partii".

### 1.3 Co sie dzieje z jezdzcem bez konia / bez uprzezy

| Sytuacja | Bez konia | Z koniem, bez uprzezy | Dowod |
|---|---|---|---|
| bitwa gracza w polu: druzyna gracza, partie lordow AI (takze towarzysze i rod gracza), zalogi | **pieszo** (slot 10 pusty; gdy kon schodzi, rzad schodzi razem z nim) | **jedzie na oklep** - slot 11 po prostu pusty, kon zostaje | K:GarrisonKit.cs:1229-1245 (:1240-1244), :720-729; DTE PartyEquipmentDistributor.cs:680-692 |
| bitwa gracza: komunikat | "N riders no horse" na starcie bitwy | brak komunikatu | K:GarrisonKit.cs:698, :706-713 |
| oblezenie, kryjowka, morze | wszyscy pieszo (DTE nie rozdaje koni) | bez znaczenia | K:GarrisonKit.cs:826-839 |
| autobitwa (AI z AI, "Send troops") | walczy jak konny - gra liczy typ jednostki | walczy jak konny | G DefaultMilitaryPowerModel.cs:260-271 |
| mapa: pasza zimowa | nie je (K0) | je | K:HorseState.cs:125-144; K:WinterRealm.cs:295 |
| mapa: predkosc | gra liczy go jako konnego (typ jednostki) | jak wyzej | K:MarchPace.cs:37 (`NumberOfMenWithoutHorse` gry) |
| zold x1.5 "konnego" | dostaje (typ jednostki) | dostaje | K:MountedWage.cs:61 |
| udzial jazdy K3, linia "Zold konnych" | liczony jako jazda (typ jednostki) | liczony jako jazda | K:CavalryTemplate.cs:124-137; K:MountedWage.cs:224 |
| zakupy | Stajnia dokupuje konia (zapas K2) | t4+ z uprzeza we wzorcu: AiGear dokupuje uprzaz (R3); t3: nic | K:Stables.cs:401-402; K:AiGear.cs:336 |

Bohaterowie (gracz, towarzysze, lordowie): DTE ich nie ubiera, jada na tym, co maja zalozone; uprzaz bohaterom w ogole nie jest sprawdzana
(K:ItemReq.cs:48-54). Decyzja Jeffa mowi o WOJSKU - bohaterow to rozpoznanie nie obejmuje (zalozenie do potwierdzenia przy projekcie).

---

## 2. Gdzie jest decyzja "ten zolnierz jedzie konno"

Nie ma jednego miejsca. Jest 3 + 7:

| # | Miejsce | Co rozstrzyga | Kto czyta wynik | Dowod |
|---|---|---|---|---|
| S1 | `Stables.Balance(mp)` | stan na mapie: jezdzcy wobec koni zbrojowni -> `Unhorsed`, `Free` | `HorseState` (pasza, kolumna marszu), `Stables.Reserve` (zakup i sprzedaz K2), `HorseCensus` (linia 175), `HorseFate.AutoDeaths` (zgony koni), `FreeArmory` (awanse, straz ROT) | K:Stables.cs:301-354; K:HorseState.cs:151; K:HorseCensus.cs:292-293; K:HorseFate.cs:442 |
| S1b | `Stables.Remount` | drugi, wlasny rachunek "jezdzcy - konie w zbrojowni" | zakup Stajni (`lack`) | K:Stables.cs:559-563, :583 |
| S2 | DTE `AssignHorseAndHarness` + KC `SkillLawWard` + K `GarrisonKit.SpawnGuard` (ostatnie slowo, Priority.Last na `Mission.SpawnAgent`) | bitwa gracza: kon w slocie 10 albo pieszo | agent w misji; licznik `RiderOnFoot` | DTE :662-694; KC:Mends.cs:1379-1443; K:GarrisonKit.cs:1207-1271, :721-729; K:DressCode.cs:117 |
| S3 | gra: typ jednostki (`IsMounted` / `HasMount`) | autobitwa, predkosc, zold, udzial jazdy | gra i nasze pomiary | G CharacterObject.cs:318-326; G DefaultMilitaryPowerModel.cs:262 |
| T1 | `Stables.FilterTargets` + `PayInHorses` | tworzenie: awans AI | - | K:Stables.cs:682-774 |
| T2 | getter + `PartyScreenLogic` | tworzenie: awans gracza | - | K:Stables.cs:96-111; G PartyScreenLogic.cs:597-677 |
| T3 | `VolunteerKit.IsKey` / `BuyCore` | tworzenie: awans ochotnika u notabla (kon + uprzaz) | - | K:VolunteerKit.cs:166-188, :211-290 |
| T4 | `RecruitKit.OnRecruited` | tworzenie: werbunek AI (najemnik: kon z puli, uprzaz ze wzorca) | - | K:RecruitKit.cs:474-530 |
| T5 | `RotHorseGuard` | tworzenie: zamiana ROT na konnego | - | K:RotHorseGuard.cs |
| T6 | `ColdStart.FillToTemplate` | tworzenie: dorobek startowy (kon + uprzaz) | E8/T rozszerza na partie z dob 1-3 | K:ColdStart.cs:76-126 |
| T7 | `GarrisonArmory` (ekran druzyny, MoveKits) | przeniesienie jezdzca z kompletem miedzy partia a zaloga | - | K:GarrisonArmory.cs:783-794, :838-845 |

**Wniosek [W]:** jedna regule da sie wpiac JEDNA nowa funkcja (np. `Stables.Pairs(mp)`: ile koni zbrojowni ma uprzaz swojej rodziny, ilu jezdzcow zostaje
bez pary, ile uprzezy jest wolnej), wolana z trzech punktow: T1/T2/T5 (brama tworzenia), S2 `SpawnGuard` (bitwa: kon bez uprzezy po strazy -> pieszo),
`AiGear` (potrzeba zakupu). `Stables.Balance` (S1) lepiej zostawic bez zmian - czyta go piec modulow, a kon bez siodla dalej je pasze i dalej jest koniem.
S3 (typ jednostki w grze) jest poza zasiegiem bez nowej mechaniki - w autobitwie brak uprzezy nie zmieni walki, wiec realny skutek dla swiata AI
daje tylko BRAMA TWORZENIA (mniej jednostek typu konnego) i zakupy.

---

## 3. Podaz uprzezy i rachunek

### 3.1 Kto produkuje i ile

| Pozycja | Liczba | Dowod |
|---|---:|---|
| producenci | ukryci rzemieslnicy `artisans` (kazde miasto, 5 linii horse_equipment..._5) i warsztaty BK `barding-smithy` ("Lorimer", 7 linii); produkcje prowadzi `WorkshopLaw` (cech "siodlarz", udzial 0.15) | `BannerKings.Redux\ModuleData\workshops.xml`, `SandBox\ModuleData\spworkshops.xml`; K:WorkshopLaw.cs:688, :756; K:Settings.cs:556 |
| uprzaz wykonana, 120 dob | 1 360 szt. = 11.3 na dobe (doby 1-30: 8.1; 31-60: 11.6; 61-90: 12.2; 91-120: 13.4); doba 120: 16 szt. | L:358 ... L:35833 (pole HorseHarness linii "Warsztaty") |
| w tym wedlug tieru (119 dob) | t1-2: 978, t3-4: 377, t5-6: 1 | linie "Uzbrojenie wedlug zrodla (E6/0)", L:896 ... L:36057 |
| rece siodlarzy, doba 120 | 688 roboczodni przydzielone, 671 w zrobionych sztukach = ok. 42 dni na sztuke (robia drogie ladry) | L:35833 |
| linie, ktore stoja, doba 120 | `artisans/horse_equipment` 49 miast, `horse_equipment_2` 41, `_3` 35 (pula pusta), `_4` 66 (pula pusta), `_5` 7; `barding-smithy` 10/9/6/10/2 | LW:359 |
| powod | siodlo ROT krainy jest jedynym wzorem linii i "kosztuje" wiecej, niz daje: `tyroshi_saddle` wartosc 1 626, cena 2 019, koszt 2 199, **152 dni pracy** (waga z danych ROT); tanie siodlo `light_harness` wartosc 44, cena 45, koszt 46 (3.6 dnia + 0.5 szt. skory) - brak zysku przy pelnej polce | LW:359; K:ArmsPricing.cs:204-212; K:WorkshopLaw.cs:550-560 |
| popyt na uprzaz w rankingu brakow | 0 linii "najwiekszy brak" z HorseHarness przez 120 dob | LW (caly plik) |
| dorobek startowy na polkach | 477 szt. uprzezy w 97 miastach | L:232 |
| skora (wsad siodla) | wyprawiono 107 szt. na dobe swiata, na polkach 322 (indeks 1.54 - droga), skor surowych 3 038; rzemioslo miasta ma 79% rak bez roboty | L:35832 |

### 3.2 Ile lezy i ile kosztuje

| Pozycja | Doba 1-2 | Doba 120 | Dowod |
|---|---:|---:|---|
| uprzaz na polkach miast i zamkow | 1 492 | 1 253 (minimum 1 029 w dobie 24; srednia dob 91-120: 1 217) | L:403, L:7171, L:35865 |
| uprzaz razem (polki i schowki osad + tabory + zbrojownie DTE) | 7 436 (t1-2 5 663 / t3-4 582 / t5-6 1 191) | 7 727 (5 569 / 957 / 1 201) | L:896, L:36057 |
| uprzaz w zbrojowniach DTE (roznica; log nie podaje jej wprost) | ok. 6 010 | **ok. 6 470** | L:36057 minus L:35865; tabory razem 18 szt. calego uzbrojenia |
| zmiana w 119 dob | - | +291; zrodla +1 356, "reszta niewyjasniona" **-1 118 (t1-2: -1 125)** = ok. 9.4 szt. na dobe znika | L:896 ... L:36057 |
| cena: tanie siodla (pole tier 0, trudnosc brak) | 12 wzorow po 44-127 zl (`light_harness` 44, `northern_light_harness` 75, `battania_horse_harness` 127) | po E8/C cel 24-60 (tolerancja 90) | ID; PROJEKT-ETAP8 E8/C test C-1 |
| cena: siodla ROT (pole tier 0 / 2) | mediana 588 / 1 626 | po E8/C ok. 145 / 237 | ID; PROJEKT-ETAP8 rozdz. 1 |
| cena: ladry (pole tier 5-6) | 7 861 / 11 522 (maks. 46 089) | po E8/C ok. 2 531 (t6) | ID |
| uprzaz we WZORCACH jednostek konnych ROT | 151 jednostek konnych, wszystkie tier >= 3 (t3: 7, t4: 52, t5: 59, t6: 24 zolnierzy + 9 bandytow i najemnikow); 147 ma uprzaz, 4 nie (`giant_rider` na mamucie, 3 jednostki Skagosu na jednorozcu); mediana ceny uprzezy wzorca: t3 75, t4 1 462, t5 i t6 **11 522** | XML + ID (policzone skryptem; inne moduly - bandyci SandBox, BK, NavalDLC - niesprawdzone) |

### 3.3 Ilu jest jezdzcow i koni

| Pozycja | Doba 2 | Doba 120 | Dowod |
|---|---:|---:|---|
| ludzie w partiach lordow i zalogach AI | 76 212 | 167 462 | L:590, L:36040 |
| jednostki konne (typ) | 7 391 | **10 799** (6.45% ludzi) | L:590, L:36040; LK |
| - partie rodow AI | 3 758 z 29 672 (12.7%) | 6 760 z 89 948 (**7.5%**) | L:589, L:36039 |
| - zalogi | 3 633 z 46 540 (7.8%) | 4 039 z 77 514 (5.2%) | L:589, L:36039 |
| jezdzcy bez konia w zbrojowni | 2 082 | **3 088 = 28.6%** (doby 91-120 srednio 3 026 = 28.0%; szczyt 4 008 ok. doby 31) | L:590, L:36040, L:9418 |
| jezdzcy z koniem | 5 309 | **7 711 = 4.60% ludzi** | jw. |
| wolne konie w zbrojowniach | 741 | 1 510 (zwykle 1 374, bojowe 132, szlachetne 4) | L:590, L:36040 |
| konie pod siodlo: zbrojownie DTE / polki miast i zamkow / stadniny na dobe | 6 050 / 1 509 / - | 9 229 / 5 549 / 235 | L:595, L:36058 |
| czekaja na konia swojej kategorii | 6 | 2 052 w 115 partiach (z tego 1 723 Dothrakowie) | L:36040; LK |
| nowi jezdzcy do partii lordow, doby 91-120, na dobe | - | **ok. 50**: zamiany ROT +37.6 / -10.9, awanse 8.2, jency 6.7, ochotnicy 6.1, najemnicy 2.2 | LK (sumy kolumn) |
| awanse AI na jezdzca, 120 dob | - | 823 (6.9 na dobe); proby odrzucone z braku konia 117 898 | LK |
| zabici jezdzcy w autobitwach, doba 120 | - | 19 (koni padlo 17) | L:36063 |
| zakupy koni przez lordow, 120 dob | - | 3 951 koni za 6 742 604 zl (ok. 1 700 zl za konia) | LK |

Najwiecej jezdzcow bez konia: Dothrakowie 480 z 1 964, `empire_s` 812 z 1 400 (58%), Qarth 253 z 268 (94%), Westerlands/Reach (`vlandia`) 264 z 824 (LK, doba 120).

### 3.4 Rachunek: czy wystarczy

**Czego log NIE mierzy:** ilu jezdzcow ma konia, a nie ma uprzezy W SWOJEJ partii. Ksiega uzbrojenia podaje uprzaz tylko lacznie dla swiata, a zadna linia
nie liczy par. Dlatego ponizej sa tylko granice, nie wynik. Brakujaca linia - rozdz. 6.

1. **Granica gorna (gdyby uprzaz byla idealnie rozlozona miedzy partie):** uprzezy w zbrojowniach ok. 6 470, jezdzcow z koniem 7 711 -> konno najwyzej
   6 470, czyli **84% dzisiejszych jezdzcow z koniem**; co najmniej 1 240 dzisiejszych jezdzcow z koniem zeszloby na piechote. Udzial "naprawde konnych":
   4.60% -> najwyzej 3.86% ludzi (L:36040, L:36057, L:35865). W rzeczywistosci gorzej: uprzaz lezy tam, gdzie byla na starcie (227 zalog i 34 partie z dorobkiem,
   L:231), a 823 jezdzcow z awansu AI dostalo konia bez uprzezy; jezdzcy z zamian ROT (rot_plus 7 063, rot_minus 1 677 w 120 dob) i z jencow przychodza bez konia
   i bez uprzezy - gdy Stajnia dosadzi ich na konia, uprzezy dalej nie maja [W].
2. **Z wykupieniem calej polki (1 253):** 7 727 sztuk wobec 7 711 jezdzcow z koniem - nominalnie na styk, ale bez zapasu, bez uwzglednienia rodzin
   (siodla wielbladow), cen (ladry po 7-11 tys.) i tego, ze lordowie kupuja tylko w miastach.
3. **Wszyscy jezdzcy (10 799):** brakuje 10 799 - 7 727 = **ok. 3 070 sztuk** nawet po wykupieniu polek. Przy dzisiejszej produkcji 13.4 na dobe
   i ubytku 9.4 na dobe przyrost netto to ok. 2.5 sztuki na dobe - luki nie da sie zamknac.
4. **Doplyw:** ok. 50 nowych jezdzcow na dobe wobec 13.4 nowej uprzezy. Czesc potrzeb pokryje uprzaz po poleglych jezdzcach (zostaje w zbrojowni),
   ale zamiany ROT i awanse tworza jezdzcow w partiach, ktore uprzezy nie maja.
5. **Cel K3 (ok. 20% jazdy w partiach lordow):** 20% z ok. 90 tys. = ok. 18 tys. jezdzcow, czyli **+11 tys. uprzezy** ponad dzisiejszy stan
   (projekt etapu 6 liczy "+12 tys. koni", docs\etap6\PROJEKT-ETAP6...:628). Przy 13.4 na dobe to ponad 800 dob. **Bez podniesienia produkcji siodel
   wymog uprzezy zatrzyma jazde swiata mniej wiecej na dzisiejszym poziomie albo nizej, a K3 nie dojdzie do celu.**
6. **Moc produkcyjna jest [S]:** 688 roboczodni siodlarzy na dobe / 3.6 dnia na tanie siodlo = do ok. 190 siodel na dobe, gdyby robili same siodla
   (dzis 16 sztuk, glownie drogich). Wsad: 0.5 szt. skory na siodlo -> 100 siodel na dobe zjadloby 50 z 107 sztuk skory wyprawianej dziennie
   (garbarze maja wolne rece, skor surowych jest 3 038). Wezlem jest wiec nie liczba rak, tylko: (i) zla waga siodel ROT (E8/C), (ii) brak sygnalu popytu
   (dzis nikt uprzezy nie kupuje - w WGRANIU 33 nie ma R3), (iii) skora.
7. **Koszt dla lordow [S]:** 50 siodel na dobe x ok. 50-130 zl = 2.5-6.5 tys. zl na dobe wobec 136 316 zl dziennych zakupow sprzetu AI (L:35836) -
   2-5%. Pieniadze nie sa przeszkoda, o ile potrzeba jest "dowolne siodlo", a nie "uprzaz wzorca" (lader za 11 522 zl dla kazdego rycerza t5-6
   to 60 x tyle).

---

## 4. Kolizje z paczkami etapu 8 - co H musi uszanowac

| Paczka | Wspolne miejsce | Co H musi zrobic |
|---|---|---|
| **E8/C** cennik (uprzaz z wagi prawdziwej `wr`) | `ArmsPricing.Compute` (:204-212), `Recipes`, `MendMaterial`; cena i dni pracy siodla | **H PO E8/C.** Bez E8/C linie siodel stoja (LW:359). Progi H liczyc w cenach po E8/C. H nie rusza cennika. |
| **E8/Z** zakupy i nadwyzki AI ("uprzaz poleglych" `AiHarnessSurplusSell`, sufit ceny, bez dodatnich stanow) | `AiGear.cs` (:146, :608-610, :789-800), `MenPurse.cs` (:101, :530, :557 - dzis "konie i rzedy - Stajnia", uprzaz nigdy nie jest nadwyzka) | Jedna definicja potrzeby uprzezy dla zakupu (H) i dla nadwyzki (Z): potrzeba = jezdzcy partii + luz, jak `Stables.Reserve` dla koni (kupno do zapasu, sprzedaz dopiero ponad 1.5 x zapas - inaczej petla kup-sprzedaj). Kolejnosc commitow w `AiGear.cs`: P2 -> W -> Z -> **H** -> O1. Sufit `AiGearMaxPriceFactor` i `AiSkipFineGoods` obejmuja tez zakup siodla. Prog Z-3 ("uprzaz w zbrojowniach AI d120 <= d60 x 1.2") po H trzeba przeliczyc - H celowo podnosi stan uprzezy. |
| **E8/T** start kampanii, rekruci gracza | `ColdStart.cs`, `LevyGold.PartyCreatedPrefix`, `StartKit.cs`, `RecruitKit.cs`, DTE werbunek / karczma | (1) `ColdStart.FillToTemplate` juz daje konia z uprzeza - po E8/T dostana je tez partie z dob 1-3 (dzis 541 partii bez kompletu, L:341): to jest jawny dorobek startowy uprzezy, H niczego nie doklada. (2) `ColdStartTierUp` - nie zgubic slotu 11. (3) Gracz po E8/T: ochotnik z zapisem notabla (kon i uprzaz kupione), najemnik "dobytek oraz kon i rzad ze wzorca" - H zmienia zrodlo rzedu najemnika na pule weteranow (para), dla AI i gracza razem. (4) `StartKit.Substitute`: po decyzji Jeffa tylko, jesli wynika z tej samej reguly - StartKit dotyczy BOHATERA; wspolna jest jedynie rodzina kon-uprzaz. Zalecenie: zostawic w E8/T jako porzadek rodziny (albo skreslic), nie laczyc z H. |
| **E8/L** lup i dary Spoils (`LootIsTransfer`) | `HorseFate.cs` okno DTE `OnMapEventEnded` (:479-520), `SpoilsOfBrokenParties` (:646-679) | Dzis oba miejsca ruszaja TYLKO konie (`IsHorse`, :140, :475, :661): uprzaz jest lupem-kopia, a uprzaz rozbitej partii znika ze zbrojownia. E8/L robi przeniesienie dla wszystkich rodzajow w oknie lupu - H NIE dodaje drugiej latki DTE. H dopisuje tylko: uprzaz zbrojowni rozbitej partii idzie razem z konmi (`armMoves`: `IsHorse` -> `MenPurse.HorseKind`) - jedna linia w istniejacej petli; uzgodnic z autorem E8/L, zeby nie zrobic tego dwa razy. |
| **E8/C konie wedlug kategorii** (`HistHorsePrices`) | ceny koni; `Stables.Level` / `RequiredMountFor` po `ItemCategory` | H nie zalezy od cen koni. Przy liczeniu par brac te same konie co `IsPlainMount`. |
| **E8/S** porzadek stanu (`Stables.Reset`: `_lastBuy.Clear()`) | `Stables.cs` - dzis NIE MA `Reset` (pola `_lastBuy`, `_riderOnly`, `_dStamp` zyja miedzy kampaniami) | Nowy stan H (liczniki doby, pamiec potrzeby) dopisac do TEGO `Reset`, nie tworzyc drugiego. H po E8/S. |
| **E8/O1** delegaty DTE, polka po wlasnym zdjeciu | `AiGear._add`, `_blTest`; `ShelfBuy.Take` (9 miejsc) | Zakup siodla ma isc istniejaca droga `BuyLoop` -> `ShelfBuy.Take` -> `Deliver` -> `AddToArmory` (zadnego nowego `AddToCounts` na polce). Jesli H musi dodac wlasne zdjecie z polki - przez `ShelfBuy`, a O1 dopisuje je do listy. |
| **E8/W** zuzycie AI | `AiWear.cs` pomija konie i uprzaz (:124, :155, :178, :275) | Bez zmian - uprzaz AI sie nie zuzywa; H tego nie rusza (osobna decyzja). |
| **E8/P2** pomiary | linie zakupow AI | Linia H jako osobna linia doby (nie dopisek do "Uprzaz (215)"), blok `etap8_h` w `sprawdz_logi`. |
| E6/R3 (juz w galezi) | `AiGear.AddBardingNeed`, `BardingMinTier` 4, `TradeCarts` | H rozszerza potrzebe (kazdy jezdziec, dowolny tier, rodzina konia); "lader wzorca" R3 zostaje jako ulepszenie po siodle. Uprzaz w drodze wozem (`TradeCarts`, `arm_tradecarts`) liczyc do podazy. |
| E6/K3 (juz w galezi) | `CavalryTemplate.Share` / `Steer` (udzial po typie jednostki), `Stables.KnightBuy` | Udzial jazdy pokazac w dwoch liczbach: typ jednostki (jak dzis) i z para. Kon rycerza kupiony ze stada wsi albo na zamku nie ma tam siodla - awans poczeka do miasta (ryzyko spowolnienia K3, prog H-5). |
| CrashScribe (drugi watek) | KC:Mends.cs `SkillLawWard` :1379-1443, `CamelCulling` :565-587 - ruszaja sloty 10-11 przed nasza straza | H wpina sie PO nich (`SpawnGuard`, Priority.Last). Zmiana w CrashScribe niepotrzebna. |

Pliki H (wariant A): `Stables.cs`, `AiGear.cs`, `GarrisonKit.cs`, `RecruitKit.cs`, `HorseFate.cs`, `RotHorseGuard.cs`, `HorseCensus.cs` (linia), `Settings.cs` +
`McmSettings.cs` (gen_mcm), `ArmouryBehavior.cs` (Reset, doba), `tools/sprawdz_logi.py`. Zadna paczka E8 poza Z, T, L, S, O1 ich nie dotyka w tych samych metodach.

---

## 5. Propozycja reguly - trzy warianty

Wspolne dla wszystkich:
- **"Uprzaz" = dowolna sztuka `HorseHarness` rodziny wierzchowca** (tak paruje DTE: `ArmorComponent.FamilyType` = rodzina potwora konia; siodlo bez pancerza pasuje
  do kazdego). Tier dowolny - najtansze siodlo robi jezdzca; lader wzorca to juz "lepsze". Bez tego rycerz t5-6 potrzebowalby ladra za 11 522 zl.
- **Wyjatek z danych:** jednostka konna, ktorej WZORZEC nie ma uprzezy (4 w ROT: mamut olbrzyma, jednorozce Skagosu), jedzie bez niej. Wielblady, slonie,
  mamuty, smoki - jak dzis poza Stajnia (`MountLaw`, `DragonUnmount`).
- **Bohaterowie poza regula** (decyzja mowi o wojsku; do potwierdzenia).
- **Klucz** `RiderNeedsHarness` (nowy, R13). **Stary zapis:** brak nowych sekcji zapisu (para liczona ze zbrojowni DTE na biezaco) - wczytuje sie bez bledu;
  jezdzcy bez uprzezy po prostu schodza z koni w bitwie gracza i kupuja siodla przy postoju.
- **Linia logu** (rozdz. 6) w kazdym wariancie, takze przy wylaczonym kluczu - bez niej nie ma "przed / po".

### Wariant C - tylko zakup

- **Co:** `AiGear.AddBardingNeed` dla kazdego jezdzca (dzis t4+) - potrzeba "dowolna uprzaz rodziny konia", kandydaci od najtanszych. Zadnego wymogu.
- **Istniejacy jezdzcy / start:** bez zmian (jada na oklep, dopoki pan nie dokupi). Start: dorobek ColdStart jak dzis.
- **Platnik / odbiorca:** sztuka polka miasta -> zbrojownia partii; zloto sakiewka ludzi, potem kiesa wodza -> kasa miasta (istniejace `Deliver`, K:AiGear.cs:788-812).
- **Koszt:** 0 ms ponad R3; zloto 2-7 tys. zl na dobe [S].
- **Ryzyko:** male. **Wada: nie spelnia decyzji** - sam kon dalej wystarcza, zeby jechac i zeby stworzyc jezdzca. Gracza nie dotyczy w ogole.

### Wariant B - wymog tylko przy tworzeniu jezdzca

- **Co:** C + brama w T1 (awans AI: cel konny odpada albo jest przyciety takze przy braku wolnej uprzezy; przy awansie uprzaz z taboru idzie do zbrojowni),
  T2 (gracz: postfiks na publicznym `PartyScreenLogic.ValidateCommand` - awans na jezdzca tylko, gdy w sakwach albo wolna w zbrojowni jest uprzaz; komunikat
  po angielsku; przy zatwierdzeniu ekranu uprzaz z sakw do zbrojowni, jak konie w `BankPaidHorses`), T5 (zamiana ROT: bez pary pieszy tego samego tieru),
  T4 (najemnik: para z puli weteranow zamiast rzedu ze wzorca). T3 (notabl) juz dziala.
- **Istniejacy jezdzcy:** nietknieci. Jezdziec, ktoremu uprzaz przepadla, dalej jedzie na oklep.
- **Start:** ColdStart (para z dorobku) + E8/T.
- **Platnik / odbiorca:** jak C; przy awansie nic nie zmienia wlasciciela (sztuka partii przechodzi z taboru do zbrojowni).
- **Koszt:** < 2 ms na dobe [S] (jedno dodatkowe przejscie zbrojowni przy liscie celow awansu).
- **Ryzyko:** srednie. Awanse na jezdzca moga stanac jak dzis z braku koni (117 898 odrzuconych prob w 120 dob). Ekran druzyny gry pokazuje tylko JEDEN wymog -
  strzalka awansu bedzie wygladala na czynna, a klikniecie da komunikat (do sprawdzenia w grze razem z latka ROT `DisableUpgradeIfOnlyDragons`).
  **Wada: polowa decyzji** ("mozna bylo stworzyc jezdzca" tak, "zeby wojsko na nim jezdzilo" nie).

### Wariant A - para kon + uprzaz (ZALECANY)

**Co:** B + C + stan pary, wszystko przez JEDNA funkcje `Stables.Pairs(mp)` (konie zbrojowni, uprzaz rodziny konia, jezdzcy; wynik: z para / z koniem bez
uprzezy / bez konia / wolna uprzaz), obok `Stables.Balance`, bez zmiany `Balance`:

1. **Tworzenie** - jak wariant B (T1, T2, T4, T5; T3 juz jest).
2. **Jazda w bitwie gracza** - `GarrisonKit.SpawnGuard`: po strazy kon w slocie 10 i pusty slot 11 -> kon schodzi (pieszo), osobny licznik; jedna regula dla
   druzyny gracza, lordow AI i zalog (ta sama metoda juz dzis obsluguje wszystkich trzech). `BattleNotice`: "N riders no saddle". DTE i tak daje pary pierwsze.
3. **Zakup** - jak C, plus ten sam luz co konie (10% jezdzcow, K2), zeby strata jednej sztuki nie zsadzala jezdzca. Brak na polce -> zamowienie
   (`SupplyDemand.NoteUnmetOnce`, istniejaca droga K:AiGear.cs:889) -> popyt -> siodlarze robia siodla.
4. **Uprzaz idzie za koniem (zamkniecie przeciekow):** weteran odchodzacy do karczmy zabiera pare (pula miasta), najemnik dostaje pare z puli (koniec
   rzedu ze wzorca z niczego, K:RecruitKit.cs:511-517); uprzaz zbrojowni rozbitej partii idzie z konmi do zwyciezcow albo na polke (dzis znika, K:HorseFate.cs:661);
   lup - E8/L; uprzaz po poleglym jezdzcu i po padlym koniu zostaje w zbrojowni jako wolna (ponad zapas - E8/Z na polke za zaplata).
5. **Poza zakresem (swiadomie):** autobitwa (gra liczy typ jednostki - zmiana to nowa mechanika), pasza i predkosc (kon bez siodla dalej je i idzie),
   zold x1.5 (pytanie nizej), zalogi nie kupuja uprzezy (nie maja Stajni, w oblezeniu nikt nie jedzie).

| Pytanie | Odpowiedz |
|---|---|
| Istniejacy jezdzcy na starcie NOWEJ kampanii | maja pare z dorobku startowego: `ColdStart.FillToTemplate` daje konia i uprzaz kazdemu jezdzcowi wzorca (K:ColdStart.cs:81, :95; L:231) - po E8/T takze partie z dob 1-3. Ochotnicy t2+ w pulach notabli maja komplet wzorca z para (1 535 zapisow, L:233). Pula weteranow startuje pusta - pierwsi najemnicy konni dopiero po pierwszych zwolnionych (jak dzis konie K1). |
| Skad uprzaz startowa | jawny dorobek stuleci, raz, tylko nowa kampania (R14) - ten sam mechanizm i ta sama linia co dzis; H nie tworzy ani sztuki. Na polkach 477 szt. z dorobku kupieckiego (L:232). |
| Stary zapis | wczytuje sie; brak sekcji; jezdzcy bez pary: w bitwie gracza pieszo, przy postoju w miescie kupno siodla. Bez dosypki. |
| Platnik i odbiorca sztuki | polka miasta -> zbrojownia partii (zakup); tabor -> zbrojownia tej samej partii (awans); zbrojownia -> pula weteranow miasta -> zbrojownia najemcy; zbrojownia rozbitych -> zwyciezcy albo polka warowni; nadwyzka -> polka miasta (E8/Z). Zadnej sztuki z niczego ani w nicosc. |
| Platnik i odbiorca zlota | sakiewka ludzi partii, potem kiesa wodza (budzet sprzetu rodu 166) -> kasa miasta; gracz - zwykly handel; przy sprzedazy nadwyzki kasa miasta z nadwyzki -> trzecia lordowi, reszta sakiewce ludzi (regula K2 / E8/Z). Bez doplat. |
| Koszt na dobe | `Pairs` przy postoju i awansie: +1 przejscie zbrojowni, < 3 ms na dobe [S] (stan koni K0 dzis 11.7 ms, L:36062); straz w misji +1 warunek na czlowieka; linia logu < 2 ms. |
| Wylacznik | `RiderNeedsHarness` (true). Wylaczony = dokladnie stan sprzed H (R3 kupuje po staremu, brama i straz pomijaja uprzaz); linia logu liczy dalej "zeszloby z koni N". |
| Linia logu | rozdz. 6. |

**Progi do testu (`etap8_h`, nowa kampania 120 dob, wobec Z33):**
- H-1: jezdzcy z koniem bez uprzezy <= 10% jezdzcow z koniem w dobach 61-120 (doby 1-30 INFO).
- H-2: udzial jezdzcow Z PARA w ludziach partii rodow, doba 120 >= udzial jezdzcow Z KONIEM w Z33 liczony ta sama linia (swiat lordowie + zalogi Z33: 4.60%, L:36040);
  spadek ponizej 80% tej liczby = NIE.
- H-3: uprzaz wykonana >= 40 szt. na dobe w dobach 31-120 (Z33: 13.4) albo linie siodel z pustym rankingiem <= 10 miast (Z33: 41-49, LW:359).
- H-4: uprzaz z niczego 0 (rzad najemnika ze wzorca 0, pary przy zamianach ROT tylko z taboru i zbrojowni), uprzaz w nicosc 0 (rozbite partie: przeniesiona = stan
  zbrojowni); "reszta" uprzezy w ksiedze uzbrojenia |suma 120 dob| <= 10% zrodel (Z33: -1 118 przy zrodlach 1 356).
- H-5: awanse AI na jezdzca >= 50% Z33 (823 w 120 dob); "czeka na uprzaz" nie rosnie w dobach 91-120.
- H-6: polki uprzezy doba 120 w przedziale 300 - 2 500 (Z33: 1 253) - ani puste, ani puchnace; zakup i sprzedaz uprzezy tej samej partii w 7 dobach = 0.
- H-7: koszt <= 5 ms na dobe; potkniecia 0; wczytanie zapisu sprzed H bez bledu (procedura "zapis 9 dob").
- Reczne (8.16): awans piechura na jezdzca u gracza bez siodla - komunikat; bitwa z jezdzcami bez siodla - ida pieszo, komunikat "riders no saddle".

**Ryzyka wariantu A:**
1. **Podaz** (rozdz. 3.4) - najwieksze. Bez E8/C i bez sygnalu popytu jazda swiata spadnie i K3 nie dojdzie do 20%. Dlatego H dopiero po E8/C, a prog H-3
   pilnuje produkcji. Gdy H-2 / H-3 nie przejda - poprawia sie podaz (wagi, skora), nie wylacza reguly.
2. **Zamiany ROT** to 3/4 doplywu jednostek typu konnego (37.6 z ok. 50 na dobe, LK), ale juz dzis wiekszosc z nich nie dostaje konia (doba 120: z koniem 3,
   na pieszego 4, zostal konny bez konia 24 - L:36063). Brama pary dotknie tylko tych "z koniem". Wniosek: liczba jednostek typu konnego rosnie w swiecie glownie
   przez ROT, niezaleznie od koni i uprzezy - "udzial jazdy" liczony po typie (7.5%, L:36039) jest zawyzony; po H trzeba go czytac jako "z para" (Dothrakowie:
   480 z 1 964 bez konia, 1 723 czeka - pokazac osobno w linii).
3. **Ekran druzyny gracza** - jeden wymog w interfejsie gry; druga brama tylko komunikatem.
4. **Jezdziec bez pary dalej bierze zold konnego i liczy sie do udzialu jazdy K3** (typ jednostki). Partia moze "miec 20% jazdy" na papierze i isc pieszo.
   H pokazuje obie liczby; zmiana zoldu to osobna decyzja (pytanie 2).
5. **Petla kup-sprzedaj** z E8/Z - jedna definicja zapasu i histereza 1.5 (jak konie K2).
6. **Cudzy kod:** `PartyScreenLogic.ValidateCommand` (gra), `Assignment` / `PartyEquipmentDistributor` (DTE - juz latane, bez nowych celow), ROT `ExchangeClanTroops`
   (juz latane w `RotHorseGuard`). Linie "wpiete N/M" (R11).

**Dlaczego A, a nie B albo C:** tylko A spelnia oba czlony decyzji; C nie wprowadza wymogu wcale, B zostawia jazde na oklep. A nie dodaje nowego stanu do zapisu
i nie zmienia `Stables.Balance`, wiec nie rusza paszy, marszu, zapasu koni K2 ani zgonow koni K1 - regresje ograniczone do trzech punktow wpiecia.

**Kolejnosc:** E8/C -> E8/Z -> E8/T -> E8/L -> E8/S -> **E8/H** -> E8/O1. Tor M-L (7 plikow Armoury, brama awansu gracza i AI, cudzy kod gry) - pelny lancuch
z recenzja; H ma sens tylko w tescie zbiorczym na nowej kampanii.

---

## 6. Brakujace pomiary (linie, ktorych log dzis nie ma)

1. **"Jezdzcy i uprzaz (E8/H)"** - raz na dobe, z petli, ktora juz liczy konie (`MountedWage.Daily` / `HorseCensus`), osobno partie rodow AI, zalogi, rod gracza:
   `jezdzcy N | z para a | z koniem bez uprzezy b | bez konia c | uprzaz w zbrojowniach u (rodzina konia u1, inna u2), wolna w | udzial jazdy: typ jednostki x%,
   z koniem y%, z para z% | brama: awanse wstrzymane z braku uprzezy p, zamiany ROT na pieszego z braku pary q, najemnicy bez pary r | zakupy: siodel s za t zl,
   brak po zakupach v, zamowienia do warsztatow o | bitwa gracza: zeszlo z koni z braku uprzezy g / l / z | wzorce konne bez uprzezy m | potkniecia`.
   Przy wylaczonym kluczu te same liczby "na sucho" - to jest pomiar "przed".
2. **Uprzaz wedlug miejsca** w ksiedze uzbrojenia: polki miast / zamkow / wsi, tabory, zbrojownie lordow, zbrojownie zalog, w drodze wozem - dzis tylko suma swiata (L:36057).
3. **Linia "Uprzaz (215)"** (K:AiGear.cs:386-399) nigdy nie pojawila sie w logu - pierwszy odczyt bedzie dopiero w tescie zbiorczym.
4. **Produkcja uprzezy wedlug wzoru** (siodlo / lader, tier) i liczba linii siodel z pustym rankingiem - dzis tylko w `warsztaty.log` jako tekst diagnozy.
5. **Skora:** zuzycie przez siodlarzy osobno od platnerzy (dzis "brak surowca skora 168" lacznie, L:35833).

---

## 7. Pytania do rozstrzygniecia przy projekcie H (kierunek)

1. **Bohaterowie** (gracz, towarzysze, lordowie) - regula tylko dla wojska? Rozpoznanie zaklada: tak.
2. **Zold konnego x1.5** dla jezdzca bez pary (dzis takze dla jezdzca bez konia) - zostaje po typie jednostki czy tylko dla jezdzca z para? Rozpoznanie: bez zmian w H, sama liczba w linii.
3. **Zalogi** - bez zakupu uprzezy i bez bramy (nie tworza jezdzcow sama Stajnia, nie jezdza w oblezeniu)? Rozpoznanie: tak, tylko licznik.
4. **Ekwipunek wzorca bez uprzezy** (mamut, jednorozce Skagosu) - wyjatek z danych czy wymog rowniez dla nich? Rozpoznanie: wyjatek.
5. **Kolumna marszu** ("Footmen on spare horses", K0) - piechur na luzaku bez siodla dalej jedzie? Rozpoznanie: bez zmian (inaczej nowa mechanika).

Skrypty pomocnicze tego rozpoznania (tylko odczyt): `...\dbc3d3d1-...\scratchpad\h-rozp\` (`seria.py`, `konie.py`, `wzorce.py`, `wz2.py`, `dump.py`, `wlog.py`).
