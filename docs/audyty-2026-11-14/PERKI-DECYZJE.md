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
