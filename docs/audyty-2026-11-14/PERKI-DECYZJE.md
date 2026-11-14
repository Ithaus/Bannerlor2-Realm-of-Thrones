# Decyzje Jeffa - perki (14.11.2026)

Numeracja jak w rozmowie (tabele po 3 umiejetnosci). Wdrozenie po krokach leczenia (500 -> nosze -> ...), jako seria poprawek perkow.

## One Handed / Two Handed / Polearm
1. ArrowCatcher: NIE oslona tarczy ("oslona tarczy jest taka jak tarcza daje"). Nowy efekt (propozycja 14.11, czeka na potwierdzenie): osobisty - tarcza +20% wytrzymalosci w bitwie i -25% zuzycia (Armoury); dowodca - tarcze ludzi w szyku +10% wytrzymalosci.
2. ShieldWall (dowodca): dziala TYLKO w szyku sciany tarcz - zolnierze lepiej lacza tarcze i chronia sie przed strzalami.
3. Trainer, LeadByExample, CorpsACorps - XP po bitwie (blad RBM): naprawic.
4. ChinkInTheArmor - znizka werbunku w RecruitCost: ok.
5. RecklessCharge - premia od predkosci po RBM: ok.
6. BaptisedInBlood, ProjectileDeflection - XP: ok.
7. ShowOfStrength - RecruitCost: ok.
8. Terror, Hope (oblezenie, obroncy) - zostawic: ok.
9. Confidence - wyjasnione (osobisty +15% obrazen 2H przy >90% HP dziala; gubernator +30% szybkosci budowy wojskowych - martwy przez BuildFunding); propozycja: dopisac do BuildFunding - czeka.
10. Lancer, 11. UnstoppableForce - premia od predkosci po RBM: ok.
12. Guards - XP: ok.
13. HardyFrontline - RecruitCost: ok.
Pytanie Jeffa: ProjectileDeflection dziala? Kod: tak (flaga agenta 0x4000000 z SandboxAgentStatCalculateModel:78, RBM nie rusza) - tylko miecz dwureczny, przy bloku; w grze niewidziane.

## Potwierdzenia 14.11 (druga runda)
1 ArrowCatcher nowy efekt: ok. 2 ShieldWall tylko w scianie tarcz: ok. 9 Confidence do BuildFunding: ok.

## Bow / Crossbow / Throwing
14 BullsEye XP: ok. 15 RenownedArcher RecruitCost: ok. 17 MountedCrossbowman XP: ok. 18 Piercer RecruitCost: ok. 19 Terror (oblezenie) zostawic: ok.
22 RunningThrow po RBM: ok. 23 Resourceful XP: ok. 24 HeadHunter RecruitCost: ok.
16 DeepQuivers, 20 Fletcher, 25 WellPrepared/Saddlebags: Jeff "dodatkowe strzaly to glupota" - nowe efekty. 21 Pavise: tarcza na plecach i tak chroni (RBM) - zmienic.
Propozycje (czekaja na potwierdzenie): 16 odzysk +30% wlasnych strzal po wygranej / lucznicy +10%; 20 kusza i belty -30% zuzycia, kusznicy odzysk +10%; 21 tarcza na plecach +30% wytrzymalosci, -25% zuzycia (balista gubernatora zostaje); 23 (osobisty) odzysk +30% wlasnej broni rzucanej; 25a WellPrepared bron rzucana -30% zuzycia, druzyna odzysk +10%; 25b Saddlebags udzwig partii +5% (XP piechoty zostaje).

## Riding / Athletics / Smithing - propozycje 26-42 wyslane 14.11, czekaja.

## Potwierdzenia 14.11 (trzecia runda)
Nowe efekty 16, 20, 21, 23, 25a, 25b: zaakceptowane.
26 ToughSteed, 27 DauntlessSteed (pancerz po RBM): ok. 28 NomadicTraditions: ok, sprawdzic HorsePace (podwojne liczenie). 29 SweepingWind: ok. 31 WellStraped: ok. 32 ThunderousCharge/AnnoyingBuzz zostawic: ok. 33 Breeder urealnic (lad, para tego samego gatunku, bez smokow, zwierzeta jedza): tak.
34 IgnorePain, 35 SurgingBlow, 36 FormFittingArmor, 37 WalkItOff (lekko ranny HP > 20 leczy sie w marszu w polowie tempa), 38 SharpenedEdge, 39 SharpenedTip, 40 IronMaker (+20% sztab z tej samej rudy), 41 PracticalSmith (cala kuznia), 42 ArtisanSmith (nasze zbroje i luki): ok.
30 CavalryTactics: Jeff pyta, o co chodzi - wyjasnione 14.11 (DefaultVolunteerModel.GetDailyVolunteerProductionProbability: +30% szansy dziennej na ochotnika w slocie z konnym, w miastach i wsiach przy miastach rodu gracza; BKVolunteerModel ma wlasna formule i perk przepada). Czeka.

## Leczenie - decyzje Jeffa 14.11 (do 500-a)
- Statek: "plywanie statkiem to nie marsz, statek moze plynac a ja moge odpoczywac w kajucie" - morze = spoczynek (tratwa: ujemne bez zmian).
- "jency i partie AI tez maja miec ten sam efekt" - leczenie tylko w spoczynku takze dla partii AI i jencow (gracza i AI).
- Nosze (501): Jeff "tak zrob nosze".
- Jeff 14.11: "lordowie stoja az sie wylecza a potem jada do zamku bo tam sie szybciej wylecza" -> 500-b (po 500-a): AI ciezko poturbowane (lord HP <= 10 albo duzy udzial rannych) staje obozem; po podleczeniu (lord > 15 HP, czesc rannych wstala) jedzie do najblizszej swojej/sojuszniczej twierdzy dokonczyc leczenie. Uzyc HeroWounds.Ai (juz wiezie rannego lorda do twierdzy). Kolizje: StrategicCampaignAI, BK, NightRest AI.
- Jeff 14.11: "to dobrze ze wojna sie spowolni, dodaj tez zeby AI wiedzialo ze rany sa powazne i zeby lordowie jak glupi nie biegali i ryzykowali gdy to niepotrzebne" -> 500-b rozszerzone: ranny lord / poturbowana partia ostrozniejsza w decyzjach mapy (nie szuka bitew, nie goni, unika silniejszych, nie dolacza do ryzykownych akcji, chetniej odchodzi do twierdzy); ostroznosc rosnie z ranami (HP lorda, udzial rannych). Zwiazane: pamiec "AI walczy o zycie".

## Potwierdzenia 14.11 (czwarta runda)
30 CavalryTactics (+30% do formuly BK dla konnych ochotnikow w osadach rodu): ok.
43 perki predkosci Scouting (pomiar, potem perk podnosi sufit kolumny): ok. 44 BeastWhisperer (tylko zwykle konie/juczne, rzadziej): ok. 45 ForcedMarch/Unburdened XP zostawic: ok. 46 CallToArms jak 43: ok.
47 DashAndSlash po RBM: ok. 48 NoRestForTheWicked XP: ok. 49 RansomBroker do naszego okupu (placi rod jenca): ok. 50 Manhunter po BK (placi kupiec): ok. 51 RogueExtraordinaire zostawic: ok.

## Potwierdzenia 14.11 (piata runda) - Trade / Steward / Engineering
52 SpringOfGold (odsetki placi kiesa kupcow miasta z warsztatami rodu), 53 InsurancePlans (wyplaca gildia miasta karawany do wysokosci kiesy), 54 RapidDevelopment (odkup placi nowy pan miasta), 55 ManOfMeans (x0.7 w naszym okupie), 56 SelfMadeMan (do BuildFunding), 58 Logistician (sprawdzic podwojny podatek), 59 ArchitecturalCommisions (placi kasa miasta): ok.
57 ForcedLabor - Jeff z zastrzezeniem: "jency zawsze jak sa budowy w danym zamku sa wykorzystywani do budowy i pracuja zwiekszajac tempo budowy, ten perk sprawia ze pracuja duzo lepiej np. normalnie za kazdego jenca +0.1% budowy (3 jencow 0.3%), a przy perku 0.2%. Jest minimalna szansa codziennie ze jakis jeniec zostanie ranny podczas pracy oraz naprawde znikoma szansa ze umrze, trzeba ich karmic i dawac im jesc. Ma to byc realne."
-> NOWY SYSTEM "praca jencow przy budowie" (osobna zmiana w serii perkow): jency w lochu osady z budowa w toku pracuja zawsze: +0.1% punktow budowy doby za jenca (ForcedLabor zarzadcy: 0.2%); codziennie mala szansa rany przy pracy, znikoma szansa smierci (PeopleBook); jency jedza z zapasow osady (sprawdzic, czy loch juz je - DungeonFate 402/404 liczy choroby, karmienia nie widac). Kolizje: DungeonFate, BuildFunding, BK, okupy, ucieczki z lochu.

## Propozycje 60-77 (medycyna, Lordship, Scholarship) wyslane 14.11 - czekaja.

## Szosta runda 14.11 (medycyna, Lordship, Scholarship)
69 Resilience (NavalDLC): Jeff "co zamiast tego skoro zostawic martwe" -> propozycja: +30% do opatrunku chirurga po wygranej bitwie MORSKIEJ (mnoznik tego samego opatrunku 503, bez drugiego opatrunku). Czeka.
Lordship: Jeff chce dokladnego wyjasnienia (wasale, grace, najemnicy) zanim zdecyduje - wyjasnione 14.11. Czeka.
76 ScholarshipTutor (naprawic + poprawic docs/pytania-2026-10-14/ROZWOJ-POSTACI.md:168), 77 PeerReview / Polyglot (dopisac drugie polowy): ok.
60-68 (medycyna poza 69): brak odpowiedzi - dopytac.
Potwierdzenia: 60-68 (medycyna) ok, 69 Resilience (+30% opatrunku po wygranej bitwie morskiej) ok, 70-75 (Lordship: 70/71/75 dopisac z opisu, 72 +15% -> +10%, 73 militaryzm sprawdzic i poprawic, 74 zostawic) ok.
Potwierdzenia 14.11: 78-105 ok (teologia, style zycia BK, NavalDLC - wedlug propozycji; 102 podlaczyc DLC pod modele BK). Jeff: "rob po kolei".
Kolejnosc wdrozen: podwojne perki BK -> XP z perkow (RBM) -> latki bledow BK -> pancerz/tarcze po RBM -> RecruitCost -> budowy (+ praca jencow) -> okupy i zloto z platnikiem -> kuznia -> reszta.
- Jeff 14.11: premie perkow XP (i InspiringLeader, Naval) maja MNOZYC wynik FairXp, nie dodawac sie do jego wagi ("tak, mnozyc"). Zmiana po 504 (FairXp.cs: waga obrazen jako mnoznik na koncu albo wyciagnac czynniki perkow i pomnozyc). Kolizja: 503 PerkXp503 (warunek "perki juz w wyniku").

## Decyzje D1-D16 (Jeff 14.11, po serii 505-515)
D1 mnozenie XP takze arena i turniej: TAK. D3 jency: pol porcji (TAK - glod oslabia prace; smierc/ucieczki - zgodnie z propozycja). D5 ShipwrightsHand: rekrut z ochotnikow miasta. D6 IronMaker: (c) przekucie lupy stratne, perk zmniejsza strate. D7 PracticalSmith: zbroje/luki o 25% krocej (nie o polowe). D8 Breeder: pasza przez caly rok. D9 Pavise: tez ciosy wrecz w tarcze na plecach. D12 WalkItOff obu: sumuja sie do 75%. D13 BushDoctor: tez w samej wsi. D14 DiplomaticTies gracza: +1 relacji z obcymi wladcami raz w tygodniu. D16 amunicja NavalDLC: odzysk (jak 512).
D4 RapidDevelopment - Jeff: "jak przejmuje wrogie warsztaty nikomu nic nie place, to wojna; warsztatow osob, z ktorymi nie tocze wojny, nie zabieram" -> zdobywca NIE placi; nowy efekt do propozycji (odczyt).
D2, D10, D11, D15 - Jeff prosi o wyjasnienie na przykladzie.
D4 (Jeff): przy zdobyciu miasta warsztaty notabli/gang leaderow i osob, z ktorymi nie ma wojny - zostaja; warsztat wrogiego lorda - zdobywca przejmuje bez zaplaty. RapidDevelopment: wlasciciel z perkiem "zdazyl wywiezc zloto" (kapital warsztatu wraca do niego) - "dobry pomysl".
D2: przepuscic WSZYSTKIE znizki werbunku przez podloge BK; pokazac na przykladzie.
D10: "nie sa wystarczajaco szybkie" - premie predkosci konia z perkow moga przekraczac sufit RBM.
D11: wersja realna - po przegranej wlasne perki (PreventiveMedicine, WalkItOff) daja najwyzej 10 HP (albo inny efekt).
D15: Lithurgy trwala u wszystkich - jedna zasada.
D10 korekta (Jeff 14.11): "moga ale max o 10% limitu RBM" - premia perkow moze podniesc predkosc konia najwyzej do sufitu RBM x 1.10 (nadpisuje wersje bez sufitu z workflow d-reszta - poprawic po jego zakonczeniu).
500-b decyzje (Jeff 14.11): E1 lord w postoju broni wlasnej osady tylko gdy ma sens i jest szansa wygrac; E2 skrot moze prowadzic do twierdzy sojusznika - tak; E3 postoj AI od progu rannego gry ok. 20 HP (nie 10); E4 wodz armii ranny - tylko ostroznosc w ataku (bez rozwiazywania armii).
Jeff 14.11 po WGRANIU 234: D16b kolczany/bron miotana/tarcze okretu - odzysk po wygranej bitwie morskiej: TAK; D7b bron z ekranu kowalstwa gry tez -25% (nie o polowe): TAK; 509 praca jencow zwieksza szanse ucieczki: TAK.

## Drogi 520 i waga pieniedzy - decyzje Jeffa 14.11
520: Valyria tez +30% (nie 50); odsiecz drogami - tak, gdy nie wolniej; drogami trzymaja sie patrole i milicja ("bo bandyci poluja przeciez na kupcow i karawany"), lordowie patrolujacy ziemie - nie wymuszac.
Waga pieniedzy (521): 1 sklad sakiewki - tak (propozycja: pensy/srebro/zloto, udzial zlota wedlug mennicy); 2 zloto w skarbcu - tak; 3 "lordowie nie woza ze soba 3 milionow, maja skarbiec i tam trzymaja zloto lub gdzies" - AI lordowie wioza tylko sakiewke robocza, reszta w skarbcu.
521 decyzje (Jeff 14.11): 1 skarbiec utraconej osady -> zdobywca: tak; 2 lokata w Banku z odsetkami: najpierw projekt; 3 waga kies karawan i band: tak (pyta, jaki to procent ich ladownosci); 4 udzial zlota Essos/Westeros: tak; 5 wierzyciele siegaja do wszystkich skarbcow: tak.
Jeff 14.11 po logu 237: D10 - tak (perk konia daje do +10% predkosci samego konia wedlug RBM, bez sufitu RBM); drogi - tak (laczyc 413 kawalkow drog bocznych w siec, poprawic pomiar Coverage (rzut na odcinek), stroic szacunek objazdu, cel ~60% jazdy AI droga).
Panika (Jeff 14.11): 1 kompanie po 10 ludzi (nie 25/50); 2 bez rzutu przy pierwszej 1/6 strat, gdy kompanie prowadzi lord albo przed zwarciem - tak; 3 mnoznik stanu bitwy max 1.25 (zamiast 1.5) - tak.
Panika - korekta (Jeff 14.11): "ostrzal ktory masakruje ludzi moze doprowadzic do paniki - jesli straca 1/2 w wyniku ostrzalu rzucamy test na panike". Czyli przed zwarciem / pod lordem: bez rzutow przy malych stratach, ale przy stracie 1/2 kompanii od ostrzalu - rzut na panike. Dopisac po workflow 529.
Chorazy (Jeff 14.11): po raporcie (gra zdejmuje luk/tarcze, RBM trzyma chorazego z tylu bez celu, nasze choragwie do 6 na formacje; plan E1-E4) - "na razie olej to". Odlozone; plan w transkrypcie workflow chorazy-rozpoznanie (wf_4355efff-720).
Jeff 14.11: przy wgraniu 531 - po wczytaniu zapisu przeliczyc od nowa oferty werbunku wszystkich wsi pobocznych (jednorazowo).
