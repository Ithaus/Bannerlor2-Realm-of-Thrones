# Przegląd wszystkich perków – 14.11.2026

Prośba Jeffa (14.11): „przejrzyj wszystkie perki, czy pasują do naszego całego systemu, czy są jakieś perki, które mają martwe bonusy, oraz medycyna… wszystkie w grze, nie tylko leczenia”.

To jest **raport z kodu, nie z gry**. Każda pozycja przeszła audyt i niezależną weryfikację. Pozycje obalone przez weryfikację zostały poprawione. Tam, gdzie weryfikacja nie potwierdziła wniosku, jest dopisek **(niepotwierdzone)**. Niczego nie zmieniano w kodzie.

Źródła: `scratchpad/rozpoznanie-14-11.md` (sekcje perki-walka, perki-kowal-dowodca, perki-zarzad-medycyna, leczenie + weryfikacje) i `scratchpad/perki-bk-rot-14-11.md` (BK, ROT/RBM/NavalDLC + weryfikacje). Ścieżki w kolumnie „Dowód” są skrócone: `sandbox/`, `tw/` = dekompilacja gry, `bk/` = BannerKings, `rbm/` = RBMCombat, `rbmai/` = RBMAI, `Armoury/`, `RC/`, `CrashScribe/` = nasz kod (kopia n367c).

Słowniczek stanów:
- **MARTWY** – perk nie robi nic (albo nie da się go zdobyć).
- **CZĘŚCIOWY** – jeden z dwóch efektów nie działa albo działa tylko w wąskim przypadku.
- **KOLIDUJE** – działa, ale źle: odwrotnie, za mocno, podwójnie, kłóci się z naszym systemem albo robi „coś z niczego”.

---

## 1. Podsumowanie w liczbach

| Grupa | Przejrzane perki | MARTWE | CZĘŚCIOWE | KOLIDUJĄCE | Działają poprawnie |
|---|---|---|---|---|---|
| 8 umiejętności walki (One-/TwoHanded, Polearm, Bow, Crossbow, Throwing, Riding, Athletics) | 164 | 3 | 33 | 2 (+4 niepotwierdzone, +1 niepewny) | ok. 121 |
| Smithing, Scouting, Tactics, Roguery, Charm, Leadership | ok. 105 | 3 | 7 | 10 | ok. 83 (w tym 2 świadomie przerobione przez CampDrill) |
| Trade, Steward, Engineering, Medicine | audyt nie podał liczby | 0 | 10 | 4 | reszta (liczby nie podano) |
| BannerKings (Lordship, Scholarship, Theology, style życia) | 99 | 15 | 31 | 6 | ok. 47 |
| NavalDLC (Mariner, Boatswain, Shipmaster) | 62 | 2 | 17 | 5 | ok. 38 |
| ROT / RBM / inne | 0 własnych perków | – | – | ROT zmienia 2 perki gry (liczone w Riding i Scouting) | – |
| **RAZEM** | **ok. 430 + grupa zarządu** | **23** | **98** | **27 (+4 niepotwierdzone)** | **ok. 290 + reszta grupy zarządu** |

### Najważniejsze wnioski

1. **Gra leczy w siodle.** Leczenie liczy się co godzinę, także w galopie. Dwa perki WalkItOff (Medicine i Athletics) leczą **szybciej w ruchu**, a TriageTent działa przy każdym zatrzymaniu, nie tylko w obozie. Nasz obóz i sen (NightRest) sam z siebie nie leczy wcale. To dokładnie odwrotnie, niż chce Jeff.
2. **Medycyna nie działa na najważniejszy stan, czyli łoże po ciężkiej ranie (HeroWounds).** Żaden perk ani umiejętność nie skraca łoża. Sama umiejętność chirurga zmniejsza tylko ryzyko śmierci przy przesileniu rany, a perki (np. CheatDeath) nie liczą się wcale.
3. **Po wygranej bitwie chirurg nie opatruje gracza.** Leczą tylko WŁASNE perki bohatera (PreventiveMedicine, WalkItOff).
4. **RBM jest największym zabójcą perków gry.** Liczy po swojemu pancerz, siłę ciosu, osłonę tarczy i prędkość konia. Przez błąd w jego kodzie giną też **wszystkie premie XP z perków po bitwie** (10 perków). W sumie ok. 25 efektów walki.
5. **Nasz własny błąd: trzy perki BK liczą się PODWÓJNIE.** CrashScribe ma dwie niezależne naprawy tej samej łatki BK i obie są włączone. CommanderWarband daje +50% wpływu zamiast +25%, MercenaryFamousSellswords +40% sławy zamiast +20%, a premia sławy stylu Cataphract +24% zamiast +12%. Dotyczy gracza i AI.
6. **Błędy w samym BK:**
   - FianRanger (styl Północy!) skraca ślad partii do 20% zamiast wydłużać go o 20%.
   - JawwalDuneRider (Dorne) daje +80% prędkości na pustyni zamiast +8%.
   - TheologyMatrimony podnosi koszt ślubu zamiast go obniżać.
   - ScholarshipTutor nigdy nie zadziała.
   - Cztery perki konne nie robią nic.
7. **Nasze systemy nadpisują perki.** Kuźnia Armoury kasuje PracticalSmith, IronMaker i ArtisanSmith przy zbrojach i łukach. Sufit kolumny MarchPace ucina perki prędkości zwiadowcy. RecruitCost zjada zniżki na werbunek z 6 perków. Okupy z Armoury pomijają RansomBroker i ManOfMeans. BuildFunding pomija ForcedLabor, SelfMadeMan i Confidence.
8. **„Nic z niczego”.** Kilka działających perków daje złoto, konie albo ludzi bez płatnika:
   - SpringOfGold, ArchitecturalCommisions, Veterinarian;
   - InsurancePlans i RapidDevelopment dla gracza;
   - 4 perki NavalDLC;
   - BeastWhisperer i Breeder w wersji ROT.
9. **Kolejność modeli wycina NavalDLC.** Modele BK wycinają z łańcucha modele NavalDLC (żołd, jedzenie, milicja, trening). Nasz RealisticCaptivity (HorseFlight) wycina morską ucieczkę NavalDLC: na morzu da się uciec bez utraty statków. Komentarze w `FoodChain.cs` i `BkrotChains.cs` opisują łańcuchy, których już nie ma.
10. **ScholarshipNaturalScientist (BK)** daje oba perki z każdej pary w drzewie Medicine. Bohater z tym perkiem ma więc jednocześnie TriageTent i WalkItOff.

---

## 2. MEDYCYNA – osobno i dokładnie

### 2.1 Jak dziś działa leczenie (stan kodu)

- **Czynny model:** BKROTPartyHealingModel → NavalDLCPartyHealingModel → DefaultPartyHealingModel. BKROT zmienia tylko leczenie szeregowych (+0.8 za błogosławieństwo) i szansę przeżycia. Leczenie bohaterów i leczenie po bitwie pochodzą wprost z gry. [log Armoury 13-09-10:665; BKROTPartyHealingModel.cs:17-124]
- **Gracz leczy się co godzinę, także w ruchu**, po 1/24 dziennej stawki. [PartyHealCampaignBehavior.cs:102-105]
- **Stawka bohatera:**
  - baza 11 HP/dobę, w mieście lub twierdzy +8;
  - umiejętność Medicine CHIRURGA: +0.5%/pkt;
  - nasze SlowHealing (gracz 50%): czynnik −0.5, **dodawany** do pozostałych czynników, a nie mnożony;
  - SelfMedication: osobno x1.3.
  [tw/DefaultPartyHealingModel.cs:232-300; Armoury/SlowHealing.cs:93-102; Armoury.json:307-308]
- **Przykłady przy ustawieniach Jeffa:**
  - chirurg z Medicine 0, w polu: 5.5 HP/dobę;
  - chirurg 50: 8.25 HP/dobę;
  - chirurg 100: 11 HP/dobę.
  Liczby zgadzają się z kodem, w grze ich nie sprawdzano **(niepotwierdzone)**.
- **Chirurg 100 z oboma perkami:** w drodze 11 × (1 + 0.5 + 0.15 − 0.5) = 12.65 HP/dobę, na postoju 11 × 1.3 = 14.3 HP/dobę. Bez perków ruch i postój leczą jednakowo.
- **Łoże HeroWounds (u Jeffa włączone):**
  - rana ciężka (Serious) 21 dni, rana bardzo ciężka (Grave) 63 dni; u gracza 50% tego czasu;
  - na łożu HP stoi na progu rany (20);
  - komunikat gry wprost mówi: „You can ride, but you cannot fight”.
  [HeroWounds.cs:262-292, 557; Settings.cs:5975-5999, 5983]
- **Niskie HP (<10) niczego nie blokuje.** Jedyny odczyt HP gracza w Armoury i CrashScribe jest w zużyciu sprzętu. RealisticCaptivity czyta HP, ale tylko w niewoli. [ArmouryBehavior.cs:3645, 3729]
- **Pogorszenia przy jeździe nie ma.** Rana krytyczna RealisticBannerlord (−4 HP/dobę) jest celowo wyłączona przez HeroWounds. [HeroWounds.cs:294-305]
- **Zakażenie RB (InfectionBehavior):**
  - dotyczy tylko rannych SZEREGOWYCH gracza, poza osadą;
  - przy chirurgu z Medicine < 20 ginie 5% rannych na dobę, od 20 ochrona jest pełna;
  - nie zależy od tego, czy partia jedzie, czy stoi;
  - mnożnik zimowy (x2) **(niepotwierdzone)**.
- **RealisticHealingModel z RB jest MARTWY**, mimo że w MCM RB widać `MedicineEnabled=true`. Model jest „samotny” i łańcuch go pomija. Martwe są jego reguły: „brak chirurga −80%”, „wprawny chirurg do +60%”, „odpoczynek w osadzie +50%”, „zima −20%”, zarówno dla szeregowych, jak i dla bohaterów. [BkrotChains.cs:51; rb/RealisticHealingModel.cs:13-126, :44]
- **Przeżycie w bitwie liczy prefiks RBM** (zastępuje metodę gry):
  - umiejętność chirurga i DoctorsOath działają w bitwach AI 4-10 razy mocniej niż w grze;
  - znika XP z DoctorsOath;
  - CheatDeath i PhysicianOfPeople zostają;
  - nowe z weryfikacji: znika warunek „tępy cios nie zabija” i gałąź bohatera-jeńca. Czy gra w ogóle woła tę metodę przy ciosach tępych **(niepotwierdzone)**.
  [rbm/CampaignChanges.cs:26-88]

### 2.2 Każdy perk medycyny

| Perk | Stan | Co robi w naszej grze | Znaczenie dla realnego leczenia | Dowód |
|---|---|---|---|---|
| Medicine.**WalkItOff** | **KOLIDUJE** | +15% leczenia bohaterów i szeregowych, gdy partia JEDZIE. Po bitwie zaczepnej dodatkowo +10 HP (perk własny bohatera). Działa tylko u chirurga (rola Surgeon albo przywódca). | Wprost sprzeczny z „leczeniem tylko w spoczynku”. Do zmiany efektu, np. „mniejsze pogorszenie ran w marszu”. | tw/DefaultPartyHealingModel.cs:162-172, 251-261, 302-314 |
| Athletics.**WalkItOff** | **KOLIDUJE** | +10% leczenia w ruchu (efekt wodza partii). Sumuje się z Medicine.WalkItOff. | Jak wyżej. HeroWounds przycina tylko rany Serious/Grave, więc lekko ranni i żołnierze dalej leczą się szybciej w marszu. | tw/DefaultPartyHealingModel.cs:164-171, 253-260 |
| Medicine.**TriageTent** | CZĘŚCIOWY | +30% leczenia, gdy partia NIE jedzie. Liczy się każde zatrzymanie, nie tylko obóz NightRest. | Dobry kandydat na „premię obozu”: przywiązać go do obozu i snu NightRest. | MobileParty.cs:1186-1196; Campaign.cs:845 |
| Medicine.**SelfMedication** | działa | Osobiste x1.3 do każdego leczenia bohatera. Druga premia (prędkość w walce) zależy od modelu statystyk Artem **(niepotwierdzone)**. | Zostawić. W nowym systemie działa także w obozie. | DefaultPartyHealingModel.cs:289-300 |
| Medicine.**PreventiveMedicine** | działa (wąsko) | Po bitwie z wynikiem bohater odzyskuje 30% utraconego HP, ale tylko gdy SAM ma perk. Czytają go też nasze CampFever i SiegeCamp. | Pasuje do „opatrunku po wygranej”. Warto, by liczył się też perk CHIRURGA, a nie tylko własny. | DefaultPartyHealingModel.cs:302-314; CampFever.cs:374-375 |
| Medicine.**BestMedicine** | działa | Premia przy wysokim morale (≥70). | Neutralny. Zostawić. | DefaultPartyHealingModel.cs:232-287 |
| Medicine.**GoodLodgings** | działa | Premia w osadzie. | Zgodny z „odpoczynek w osadzie”. Zostawić. | jw. |
| Medicine.**PristineStreets** | działa | Premia w mieście. Gubernator: dobrobyt; czyta go też nasze CampFever. | Zostawić. | jw.; CampFever.cs:267 |
| Medicine.**BushDoctor** | CZĘŚCIOWY (z założenia gry) | +20% tylko wtedy, gdy partia stoi tuż obok ostatnio odwiedzonej wsi, ale nie w niej. W samej wsi nie działa. | Raczej zostawić. Można rozważyć działanie w obozie przy wsi. | DefaultPartyHealingModel.cs:178-197, 267-283 |
| Medicine.**Sledges** | CZĘŚCIOWY | Zmniejsza o połowę karę prędkości za rannych. Gra nakłada tę karę dopiero przy ponad 1/4 rannych i wynosi ona najwyżej ok. −5%, więc perk daje ok. +2.5%, czyli prawie nic. Na nosze wodza dziś nie działa. | Naturalny kandydat do „noszy”: perk łagodzi spowolnienie kolumny niosącej leżącego wodza. | DefaultPartySpeedCalculatingModel.cs:392-410 |
| Medicine.**DoctorsOath** | CZĘŚCIOWY (zmieniony przez RBM) | Przeżycie: w bitwach AI pełna siła (w grze x0.1). Brak XP za operację. | Nie dotyczy leczenia, tylko przeżycia. Raczej zostawić. | rbm/CampaignChanges.cs:44-50 |
| Medicine.**CheatDeath** | CZĘŚCIOWY | Działa w bitwie (−50% śmierci nieprzytomnego, przez RBM). NIE działa przy śmierci z ciężkiej rany HeroWounds (przesilenie). Wpływ na śmierć ze starości **(niepotwierdzone)**. | Dać CheatDeath CHIRURGA do rzutu śmierci przy przesileniu. | HeroWounds.cs:810-827 |
| Medicine.**Veterinarian** | **KOLIDUJE** | Druga premia: 50% szansy na konia za każdego zabitego jeźdźca. Koń powstaje ze wzorca jednostki, czyli z niczego, i dubluje konie zwracane przez nasze HorseFate. Główna premia (30% dziennej szansy na wyleczenie kulawego konia) nie została oceniona. | Nie dotyczy leczenia ludzi. Patrz Decyzje. | CampaignBattleRecoveryBehavior.cs:83-104 |
| Medicine.**HelpingHands**, **PerfectHealth**, **PhysicianOfPeople**, **BattleHardened** | działają | Dotyczą tylko szeregowych (tempo, przeżycie T1-2, XP). Wpływ PhysicianOfPeople na lojalność **(niepotwierdzone)**. | Zostawić. Przy cięciu leczenia w ruchu dla szeregowych dalej mają sens. | DefaultPartyHealingModel.cs:78-81, 202-221 |
| Medicine.**SiegeMedic**, **CleanInfrastructure** | działają | Oblężenie i choroba obozowa CampFever; gubernator. | Zostawić. | CampFever.cs:374-375 |
| Medicine.**FortitudeTonic**, **HealthAdvise**, **MinisterOfHealth** | działają **(niepotwierdzone)** | Audyt uznał FortitudeTonic i HealthAdvise za działające bez weryfikacji. MinisterOfHealth zależy od modelu statystyk Artem (niesprawdzone). | – | – |
| Athletics.**AGoodDaysRest** | działa (tylko w osadzie) | +10% leczenia, ale tylko w osadzie. Nasz obóz w polu się nie liczy. | Rozszerzyć na nasz obóz (arm_camp_wait), żeby odpoczynek w polu był odpoczynkiem. | DefaultPartyHealingModel.cs:177-190, 268-279 |
| NavalDLC Boatswain.**Resilience** | CZĘŚCIOWY | P (30% utraconego HP po zwycięstwie) jest martwy: BKROT dziedziczy tę metodę z gry, a BkrotChains jej celowo nie wiąże. S (leczenie na morzu) działa. | Po wprowadzeniu „opatrunku chirurga” lepiej go zostawić martwym, żeby nie było drugiego opatrunku. | NavalDLCPartyHealingModel.cs:52-58; BkrotChains.cs:54-55 |
| BK ScholarshipNaturalScientist | działa | Daje OBA perki z każdej pary Medicine, czyli razem TriageTent i WalkItOff. | Po zmianie WalkItOff trzeba pamiętać o tej ścieżce. | bk/BKEducationBehavior.cs:113-126 |
| BK zadanie rady FamilyCare | **(niepotwierdzone)** | Czynnik leczenia bohaterów w osadzie. | Zgodne z „leczenie w spoczynku”. | bk/VanillaModelTweakPatches.cs:825-840 |
| BKROT błogosławieństwo Josimakazma | kosmetyczny błąd | +0.8 leczenia szeregowych, ale pod nazwą AquanTheRedBull. | Bez znaczenia. | BKROTPartyHealingModel.cs:64-66 |

### 2.3 Co to znaczy dla planowanego realnego leczenia

| Założenie Jeffa | Dziś | Które perki w tym przeszkadzają albo pomagają |
|---|---|---|
| Leczenie tylko w spoczynku (obóz, sen, osada) | Leczenie co godzinę wszędzie. Obóz NightRest nic nie daje. | **Przeszkadzają:** oba WalkItOff (premia w ruchu). **Pomagają po zmianie:** TriageTent (przywiązać do obozu), AGoodDaysRest (rozszerzyć na obóz), GoodLodgings i karczmy TownInns (już tylko w osadzie). |
| Nosze poniżej 10 HP (kolumna się wlecze) | Brak. Łoże HeroWounds wprost pozwala jeździć. | **Sledges** może łagodzić karę noszy. Dziś nie dubluje się z karą gry za rannych, bo ta nie dotyczy jednego rannego wodza. |
| Pogorszenie ran przy jeździe | Brak. Rana krytyczna RB wyłączona przez nas, zakażenie RB dotyczy tylko szeregowych i nie patrzy na ruch. | WalkItOff można przerobić na „mniejsze pogorszenie w marszu”. Zakażenie RB rozważyć jako „ryzyko w marszu bez obozu”. |
| Opatrunek chirurga po wygranej | Brak. Leczą tylko własne perki bohatera (PreventiveMedicine 30%, WalkItOff +10). | **PreventiveMedicine** chirurga powinien się liczyć. **Resilience** (Naval) zostawić martwy, żeby nie liczyć dwa razy. Leczenie przez Hero.Heal, żeby łoże dalej przycinało HP do 20. |
| Medycyna ma znaczenie przy ciężkiej ranie | Łoże stałe. Umiejętność chirurga działa tylko na rzut śmierci, perki wcale. | Propozycja: Medicine chirurga skraca łoże; CheatDeath chirurga tnie śmierć przy przesileniu; TriageTent i GoodLodgings dają dodatkowy dzień łoża w obozie lub twierdzy. |

Ryzyka, o których trzeba pamiętać (z audytu leczenia):
- Autotest (LogGate) pomija łoże gracza i nowe blokady muszą mieć ten sam wyjątek.
- Bitwa z Nocnym Królem zdejmuje łoże.
- Nie wolno wołać `GameMenu.SwitchToMenu` z menu oczekiwania.
- MarchPace ma sufit, więc kara noszy musi iść pod sufit.
- SlowHealing dodaje czynniki: nowy postfiks musi biec po nim, a przed StarvePostfix.
- Jedna zmiana naraz: najpierw leczenie tylko w spoczynku, potem nosze, potem pogorszenie i opatrunek.

---

## 3. Perki po umiejętnościach

Tabele zawierają tylko perki z problemem. Perki działające poprawnie są zliczone w jednym zdaniu pod tabelą.

### 3.1 Umiejętności walki (RBMCombat i RBMAI włączone)

**One Handed** (21 perków)

| Perk | Stan | Dlaczego | Propozycja | Dowód |
|---|---|---|---|---|
| ArrowCatcher | MARTWY | RBM ustawia każdemu stałą osłonę tarczy (0.01), więc premia perka (oba efekty) przepada. | Naprawić: postfiks po RBM dodający premię. | rbm/DamageRework.cs:922-930 |
| ShieldWall | CZĘŚCIOWY | Efekt dowódcy (większa osłona w szyku) przepada tak samo. Efekt osobisty działa. | Naprawić razem z ArrowCatcher. | sandbox/…StatCalculateModel.cs:1480-1482 |
| Trainer, LeadByExample, CorpsACorps | CZĘŚCIOWY | Premia XP po bitwie ginie przez błąd RBM (ExplainedNumber przekazany przez Invoke w tablicy i nieodczytany). Drugie efekty działają. | Naprawić: jedna łatka dla wszystkich perków XP. | rbm/CampaignChanges.cs:129-187 |
| ChinkInTheArmor | CZĘŚCIOWY | Zniżkę na werbunek zjada RecruitCost + LimitMin BK (przy RecruitCostDays 10). Przebicie pancerza działa. | Naprawić w RecruitCost. | bk/BKPartyWageModel.cs:383; RecruitCost.cs:85-104 |

Pozostałe 15 perków One Handed działa poprawnie.

**Two Handed** (18)

| Perk | Stan | Dlaczego | Propozycja | Dowód |
|---|---|---|---|---|
| RecklessCharge | CZĘŚCIOWY | Premia od prędkości (osobista) ginie, bo RBM liczy siłę ciosu sam. Efekt dowódcy działa. | Zmienić opis albo łatka siły ciosu (balans – decyzja). | rbm/MagnitudeChanges.cs:22-236 |
| BaptisedInBlood, ProjectileDeflection | CZĘŚCIOWY | XP po bitwie ginie (błąd RBM). Drugie efekty działają. | Naprawić (łatka XP). | rbm/CampaignChanges.cs:129-187 |
| ShowOfStrength | CZĘŚCIOWY | Zniżkę werbunku zjada RecruitCost. | Naprawić w RecruitCost. | jw. |
| Terror, Hope | CZĘŚCIOWY | Morale za zabójstwo nie działa przy obrońcach w oblężeniu (RBMAI). W polu działa. | Zostawić. | rbmai/SiegePatches.cs:161-200 |
| Confidence | CZĘŚCIOWY | Premia do budowy przepada, bo BuildFunding zastępuje moc budowy. | Dopisać do EngAdd w BuildFunding. | BuildFunding.cs:154-178 |

Pozostałe 11 perków Two Handed działa poprawnie.

**Polearm** (21)

| Perk | Stan | Dlaczego | Propozycja | Dowód |
|---|---|---|---|---|
| Lancer | CZĘŚCIOWY | Premia od prędkości działa tylko przy kopii w ataku z rozpędu. Aktywne ciosy liczy RBM. | Opis albo łatka (balans). | rbm/MagnitudeChanges.cs:22-236 |
| UnstoppableForce | CZĘŚCIOWY | Efekt dowódcy tylko przy kopii z rozpędu. Efekt osobisty działa. | jw. | jw. |
| Guards | CZĘŚCIOWY | XP garnizonu konnych w bitwie ginie (błąd RBM). | Łatka XP. | rbm/CampaignChanges.cs |
| HardyFrontline | CZĘŚCIOWY | Zniżkę werbunku zjada RecruitCost. | RecruitCost. | RecruitCost.cs:85-104 |

Pozostałe 17 perków Polearm działa poprawnie.

**Bow** (21)

| Perk | Stan | Dlaczego | Propozycja | Dowód |
|---|---|---|---|---|
| BullsEye | CZĘŚCIOWY | XP po bitwie ginie (błąd RBM). | Łatka XP. | rbm/CampaignChanges.cs |
| RenownedArcher | CZĘŚCIOWY | Zniżkę werbunku zjada RecruitCost. Tańszy awans działa. | RecruitCost. | RecruitCost.cs |
| DeepQuivers | KOLIDUJE **(niepotwierdzone)** | Dodatkowe strzały powstają z niczego. Jeśli nie wchodzą do ModifiedMaxAmount, nasz AmmoRecovery ich nie liczy jako straty. | Najpierw sprawdzić. | AmmoRecovery.cs:73-76 |

Pozostałe 18 perków Bow działa poprawnie.

**Crossbow** (21)

| Perk | Stan | Dlaczego | Propozycja | Dowód |
|---|---|---|---|---|
| MountedCrossbowman | CZĘŚCIOWY | XP po bitwie ginie (błąd RBM). | Łatka XP. | rbm/CampaignChanges.cs |
| Piercer | CZĘŚCIOWY | Zniżkę werbunku zjada RecruitCost. | RecruitCost. | RecruitCost.cs |
| Terror | CZĘŚCIOWY | Morale za zabójstwo nie działa przy obrońcach w oblężeniu. | Zostawić. | rbmai/SiegePatches.cs |
| Fletcher | KOLIDUJE **(niepotwierdzone)** | Dodatkowe bełty z niczego, jak DeepQuivers. | Sprawdzić. | AmmoRecovery.cs:73-76 |
| Pavise | niepewny **(niepotwierdzone)** | RBM może i tak blokować pociski tarczą na plecach, więc perk byłby zbędny. | Sprawdzić w grze. | rbm/RangedRework.cs:1446-1500 |

Pozostałe 16 perków Crossbow działa poprawnie.

**Throwing** (21)

| Perk | Stan | Dlaczego | Propozycja | Dowód |
|---|---|---|---|---|
| RunningThrow | CZĘŚCIOWY | Premia od prędkości (osobista) ginie, bo RBM liczy pociski sam. Efekt dowódcy działa. | Opis albo łatka (balans). | rbm/MagnitudeChanges.cs:240-301 |
| Resourceful | CZĘŚCIOWY | XP po bitwie ginie (błąd RBM). Dodatkowe oszczepy mogą być z niczego **(niepotwierdzone)**. | Łatka XP; amunicję sprawdzić. | rbm/CampaignChanges.cs |
| HeadHunter | CZĘŚCIOWY | Zniżkę werbunku zjada RecruitCost. | RecruitCost. | RecruitCost.cs |
| WellPrepared, Saddlebags | KOLIDUJE **(niepotwierdzone)** | Dodatkowa amunicja z niczego, jak DeepQuivers. | Sprawdzić. | AmmoRecovery.cs:73-76 |

Pozostałe 16 perków Throwing działa poprawnie.

**Riding** (20)

| Perk | Stan | Dlaczego | Propozycja | Dowód |
|---|---|---|---|---|
| ToughSteed | MARTWY | RBM liczy pancerz konia z uprzęży, więc oba efekty perka przepadają. | Naprawić: postfiks pancerza po RBM. | rbm/ArmorRework.cs:783-806 |
| DauntlessSteed | CZĘŚCIOWY | +5 pancerza konnym (dowódca) przepada przez RBM. Odporność na zachwianie działa. | Naprawić z IgnorePain. | rbm/ArmorRework.cs:13-17 |
| NomadicTraditions | CZĘŚCIOWY | Efekt dowódcy (obrażenia od prędkości) tylko w ataku biernym. Prędkość partii działa, także w naszym HorsePace. Możliwe podwójne liczenie w HorsePace **(niepotwierdzone)**. | Sprawdzić HorsePace. | HorsePace.cs:424 |
| SweepingWind | CZĘŚCIOWY | +5% prędkości konia przepada, bo RBM liczy prędkość konia od nowa. +2% prędkości partii działa. | Opis albo łatka. | rbm/HorseChanges.cs |
| CavalryTactics | CZĘŚCIOWY | +30% ochotników konnych nie działa, bo czynny jest BKVolunteerModel, który nie ma tego perka. Efekt gubernatora działa. | Naprawić: postfiks na model BK. | bk/BKVolunteerModel.cs:354-360 |
| WellStraped | CZĘŚCIOWY | +0.5 lojalności (gubernator) martwe w osadach z populacją BK, czyli prawie wszędzie. Czynny jest BKROTLoyaltyModel : BKLoyaltyModel. | Naprawić: postfiks. | bk/BKLoyaltyModel.cs:78-112 |
| ThunderousCharge, AnnoyingBuzz | CZĘŚCIOWY | Morale za zabójstwo nie działa przy obrońcach w oblężeniu. | Zostawić. | rbmai/SiegePatches.cs |
| Breeder | KOLIDUJE | ROT zastępuje rozmnażanie zwierząt: działa też na statku, wystarcza suma >1 zwierzęcia, a zwierzęta powstają z niczego. Księga Armoury liczy je prawdopodobnie tylko jako źródło anonimowe. Możliwy błąd NRE przy samych smokach **(niepotwierdzone)**. | Decyzja (patrz niżej). | rot/FoodConsumptionBehaviorPatch.cs:15-98 |

Pozostałe 11 perków Riding działa poprawnie.

**Athletics** (21)

| Perk | Stan | Dlaczego | Propozycja | Dowód |
|---|---|---|---|---|
| IgnorePain | MARTWY | Oba efekty (+pancerz) przepadają, bo RBM liczy pancerz wprost z ekwipunku. | Naprawić: postfiks pancerza po RBM. | rbm/ArmorRework.cs:13-17 |
| SurgingBlow | CZĘŚCIOWY | Premia od prędkości tylko w ataku biernym, a pieszo prawie nigdy. | Opis albo łatka. | rbm/MagnitudeChanges.cs |
| FormFittingArmor | CZĘŚCIOWY | W grze działa, ale nasze kary biegu i zadyszki liczą surowe kilogramy zbroi. | Uwzględnić perk w FieldCraft i BattleWind. | FieldCraft.cs:460; BattleWind.cs:623 |
| WalkItOff | **KOLIDUJE** | +10% leczenia w marszu (patrz Medycyna). | Zmienić efekt. | DefaultPartyHealingModel.cs:164-171 |

AGoodDaysRest działa, ale tylko w osadzie (patrz Medycyna). Pozostałe 17 perków Athletics działa poprawnie.

### 3.2 Kowal, zwiad, dowodzenie

**Smithing (Crafting)**

| Perk | Stan | Dlaczego | Propozycja | Dowód |
|---|---|---|---|---|
| SharpenedEdge | MARTWY | Premia do cięcia wykutej broni. RBM liczy siłę cięcia sam i pomija model gry. | Decyzja: łatka po RBM albo uczciwy opis. | rbm/MagnitudeChanges.cs:22-23, 234-236 |
| SharpenedTip | MARTWY | To samo dla pchnięcia. | jw. | rbm/MagnitudeChanges.cs:178-183 |
| IronMaker | MARTWY | Przy czynnej dymarce (u Jeffa włączona) formuła rudy nie ma premii perka. To świadome: „z łupy metalu nie przybywa”. | Decyzja: np. +1 sztaba przy przekuciu łupy albo opis. | MaterialLaw.cs:102, 125; log 13-09-10:485 |
| PracticalSmith | CZĘŚCIOWY | −50% staminy działa tylko dla broni białej z ekranu gry. Dla zbroi, łuków, kusz i amunicji stamina pochodzi z naszego przepisu, bez perka. Nie skraca też czasu pracy. | Naprawić w RangedStamina (bohater dostępny jako `__1`). | Patches.cs:280-296; ForgeClock.cs:29 |
| ArtisanSmith (Crafting) | CZĘŚCIOWY | Lepsza cena sprzedaży tylko dla broni wykutej w ekranie gry. Nasze zbroje i łuki się nie liczą. | Decyzja: rozszerzyć albo zostawić. | DefaultTradeItemPriceFactorModel.cs:42 |

Pozostałe perki Smithing działają. Nasz kod powtarza progi CharcoalMaker i stali (w logu: 6 węgli zamiast 4), a w kuźni zbroi także Experienced/Master/LegendarySmith i CuriousSmith/Smelter.

**Scouting**

| Perk | Stan | Dlaczego | Propozycja | Dowód |
|---|---|---|---|---|
| Pathfinder, DayTraveler, NightRunner, DesertBorn, UncannyInsight, ForcedMarch (gł.), Unburdened (gł.), Tracker (druga) | KOLIDUJE | Gra dodaje premię prędkości, a potem sufit kolumny MarchPace ucina ją do tempa najwolniejszego. Gdy sufit wiąże, perk nie daje nic. Skali nie zmierzono. | Najpierw pomiar w logu, potem decyzja (np. perk podnosi sufit). | MarchPace.cs:225-285 |
| BeastWhisperer | KOLIDUJE | ROT podmienia mechanizm na własny bez filtra „nie na handel”: perk może dać unikat, rydwan, słonia, z niczego. | Decyzja (patrz niżej). | rot/ROTDragonBehavior.cs:88-123 |
| ForcedMarch, Unburdened (XP dzienne) | przerobione | CampDrill świadomie przeniósł trening do obozu i podmienił opisy. Działa. | Zostawić. | CampDrill.cs:281-293 |

Pozostałe perki Scouting działają. ForestKin został odtworzony w TerrainEase.

**Tactics**

| Perk | Stan | Dlaczego | Propozycja | Dowód |
|---|---|---|---|---|
| CallToArms | KOLIDUJE | Premia prędkości ucinana sufitem MarchPace. | Jak Scouting. | MarchPace.cs |

Pozostałe perki Tactics działają (model armii, autobitwa, oblężenie).

**Roguery**

| Perk | Stan | Dlaczego | Propozycja | Dowód |
|---|---|---|---|---|
| DashAndSlash | CZĘŚCIOWY | Część osobista (siła ciosu) ginie przez RBM. Część kapitana działa **(niepotwierdzone)**. | Razem z decyzją o Sharpened. | sandbox/StrikeMagnitudeModel.cs:132, 195 |
| NoRestForTheWicked | CZĘŚCIOWY | XP bojowe bandytów ginie (błąd RBM). Część treningowa działa w CampDrill. | Łatka XP. | rbm/CampaignChanges.cs |
| RansomBroker | CZĘŚCIOWY | Przy lordach gracza cenę daje Armoury 178, które ignoruje cenę gry z perkiem. Działa w okupach AI-AI. | Doliczyć w RcLordPrice, z płatnikiem (ród jeńca). | Ransom178.cs:735-753; RC/FairRansom.cs:81-85 |
| Manhunter | CZĘŚCIOWY | W miastach z polityką Enslavement (domyślna BK) BK podmienia cenę na cenę niewolnika i premia ginie. | Postfiks po BK, z płatnikiem. | bk/VanillaModelTweakPatches.cs:1215-1225 |
| RogueExtraordinaire | CZĘŚCIOWY | W naszych skrytkach wsi nic nie dodaje (sufit 0.50 osiągnięty wcześniej). Łup w grze działa. | Zostawić. | RaidCache.cs:18-23, 109-117 |

Pozostałe perki Roguery działają (m.in. KnowHow, Scarface, SaltTheEarth czytane przez nasze RaidCache i CampInterrogation; FleetFooted działa, choć RealisticCaptivity dodaje swoje bramki).

**Charm** – żaden perk nie ma problemu. Wszystkie działają (relacje, wpływ, perswazja; nasza Persuasion.cs je liczy).

**Leadership** – CombatTips i RaiseTheMeek zostały świadomie przerobione przez CampDrill na trening w obozie i działają. Pozostałe perki działają (morale, armia). InspiringLeader czyta RBM; jego oceny w audycie nie było.

### 3.3 Zarząd i medycyna

**Trade**

| Perk | Stan | Dlaczego | Propozycja | Dowód |
|---|---|---|---|---|
| SpringOfGold | KOLIDUJE | 0.1% złota przywódcy dziennie (do 1000) z niczego, dla każdego rodu, także AI. | Płatnik albo 0 dla AI (jak SmallGold). | DefaultClanFinanceModel.cs:160-164 |
| InsurancePlans, RapidDevelopment | CZĘŚCIOWY | Dla AI wyzerowane (TradePerkGiftsAiOff). Gracz dalej dostaje 5000 z niczego. | Decyzja: zostawić jako wyjątek gracza albo płatnik. | SmallGold.cs:43-47 |
| ManOfMeans | CZĘŚCIOWY | −30% okupu gracza ginie, bo RealisticCaptivity podmienia kwotę na okup 178. | Mnożnik x0.7 w okupie 178. | RC/Patches.cs:153-172 |
| SelfMadeMan | CZĘŚCIOWY | Druga premia (+30% budowy targu i in.) martwa przez BuildFunding. | Dopisać do EngAdd. | BuildFunding.cs:154-178 |

Pozostałe perki Trade działają w łańcuchu cen. ArtisanCommunity i działanie perków handlowych w handlu AI na rynkach KS **(niepotwierdzone)**.

**Steward**

| Perk | Stan | Dlaczego | Propozycja | Dowód |
|---|---|---|---|---|
| ForcedLabor | CZĘŚCIOWY | Druga premia (budowa za jeńców) martwa przez BuildFunding. | Dopisać do EngAdd. | BuildFunding.Engineering.cs:50-80 |

Pozostałe perki Steward działają (żołd, jedzenie). Logistician: możliwe podwójne liczenie podatku wsi **(niepotwierdzone)**.

**Engineering**

| Perk | Stan | Dlaczego | Propozycja | Dowód |
|---|---|---|---|---|
| ArchitecturalCommisions | KOLIDUJE | 20 zł na dobę z niczego za ciągły projekt w mieście. | Płatnik (kasa miasta) albo 0. | DefaultClanFinanceModel.cs:459-466 |

Perki budowy (Carpenters, MilitaryPlanner, Stonecutters, Clockwork) są odbudowane w BuildFunding i działają. Perki inżynierii w misji zależą od modelu Artem **(niepotwierdzone)**.

**Medicine** – szczegóły w rozdziale 2. KOLIDUJE: WalkItOff, Veterinarian. CZĘŚCIOWE: TriageTent, Sledges, BushDoctor, CheatDeath, DoctorsOath. Pozostałe działają.

### 3.4 BannerKings – Lordship, Scholarship, Theology

| Perk | Stan | Dlaczego | Propozycja | Dowód |
|---|---|---|---|---|
| LordshipAristocraticRites | MARTWY | Ani Grace +4%, ani limit wasali +1 nie są zaimplementowane. Wybór tego perka marnuje parę. | Opis „no effect” albo prosty efekt. | bk/BKPerks.cs:609 |
| LordshipRogueConnections | MARTWY | Nic nie zmienia chęci najemników ani zaliczki. | jw. | bk/BKPerks.cs:595 |
| LordshipSellswordCareer | KOLIDUJE | Zaliczka i odprawa najemnika używają premii +15% zamiast +10%. | Łatka BK (mała). | bk/BKDiplomacyModel.cs:500 |
| LordshipTraditionalist, LordshipMilitaryAdministration | CZĘŚCIOWY **(niepotwierdzone)** | Militaryzm +1-2% względnie zamiast +1/+2 pkt, czyli prawie nic. Asymilacja i tempo poboru działają. | Zostawić albo łatka. | bk/BKVolunteerModel.cs:454-482 |
| LordshipEconomicAdministration | CZĘŚCIOWY **(niepotwierdzone)** | Przy BetterEconomy działa tylko przez warstwę BK z siłą 0.5, czyli w połowie. | Zostawić. | BKEconomyLayerInstaller.cs:287-320 |
| LordshipDiplomaticTies | CZĘŚCIOWY **(niepotwierdzone)** | Druga połowa (relacje z obcymi władcami) bez kodu. | Zostawić. | bk/BKDiplomacyModel.cs:852 |
| ScholarshipTutor | MARTWY | Błąd BK: sprawdza perk u dorastającego DZIECKA, nie u nauczyciela, więc nigdy nie zadziała. `docs/pytania-2026-10-14/ROZWOJ-POSTACI.md:168` błędnie podaje „działa”. | Łatka BK + poprawka dokumentu. | bk/BKEducationBehavior.cs:482 |
| ScholarshipPeerReview, ScholarshipPolyglot | CZĘŚCIOWY **(niepotwierdzone)** | Druga połowa (podwójne XP z książek / szybsza nauka języków) bez kodu. | Zostawić. | bk/BKInnovationsModel.cs:54; BKEducationModel.cs:47 |
| TheologyMatrimony | KOLIDUJE | Koszt wpływu przy ślubie ROŚNIE o 5% zamiast spaść o 15%. Ocena małżonka działa. | Łatka BK. | bk/BKMarriageModel.cs:425; Helpers.cs:205-222 |
| TheologyLithurgy | CZĘŚCIOWY **(niepotwierdzone)** | Losowe relacje z kapłanami bez kodu. | Zostawić. | bk/BKRelationsModel.cs:120 |

Pozostałe perki działają: większość z 15 perków Lordship, prawie całe Scholarship (14) i Theology (10). Do tego AugustDeJure i inne perki tytułów. Czy łatki Armoury na płatności tytułów zachowują rabat −5% z LordshipClaimant i AugustDeJure, nie sprawdzono.

### 3.5 BannerKings – style życia

| Perk | Stan | Dlaczego | Propozycja | Dowód |
|---|---|---|---|---|
| CourtierAppointee, CourtierCompanion, CourtierRoyalCouncillor | MARTWY | Stylu Courtier nie ma w XML, więc perków nie da się zdobyć. | Zostawić (nieosiągalne). | bk_lifestyles.xml |
| OutlawUnderworldKing | MARTWY | Nie ma go na liście stylu Outlaw. | Zostawić. | bk_lifestyles.xml:123-125 |
| CataphractKlibanophoros | MARTWY | Do zdobycia (styl empire), ale nikt nie czyta ani −5% obrażeń, ani +6% pchnięcia. | Opis albo efekt. | bk_lifestyles.xml:41 |
| JomsvikingShieldwall, JomsvikingSeaToughness, DrakkarSeaCommander, SjofarandiCoastalHunter | MARTWY | Style kultury nord (w ROT 17 lordów); żaden kod ich nie czyta. | Opis albo efekt. | bk_lifestyles.xml:226-251 |
| CataphractAdaptiveTactics, JawwalCamelMaster, KheshigOutrider | MARTWY | Błąd BK: mnożą statystyki konia na agencie JEŹDŹCA, a gra czyta je tylko na koniu (RBM i tak je przelicza). Drugie połowy bez kodu. Wniosek z kodu C#. | Naprawić (przenieść na konia) albo opis. | bk/VanillaModelTweakPatches.cs:139-195 |
| CataphractEquites | CZĘŚCIOWY | Szarża martwa (jak wyżej). Żołd konnych −10% działa. | jw. | bk/BKPartyWageModel.cs:355-357 |
| CommanderWarband | **KOLIDUJE** | Łatka BK nie wchodzi w 1.4.8, a CrashScribe wpina DWIE zastępcze naprawy, obie czynne. Wpływ z bitew wychodzi +50% zamiast +25%. Dotyczy też premii sławy stylu Cataphract (+24% zamiast +12%). | **Wyłączyć jedną z dwóch napraw.** | CrashScribe/Mends.cs:3214-3330; BkBattlePerks.cs:63-195; log CrashScribe :973, :990 |
| MercenaryFamousSellswords | **KOLIDUJE** | Ta sama przyczyna: sława +40% zamiast +20%. | jw. | jw. |
| CommanderInspirer | CZĘŚCIOWY **(niepotwierdzone)** | Kara morale za różnicę kultur bez kodu. | Zostawić. | bk/VanillaModelTweakPatches.cs:1657 |
| FianRanger | **KOLIDUJE** | Ślad partii trwa 20% czasu (−80%) zamiast +20%. Premia +8% do łuku bez kodu. Styl Fian ma kultura `battania`, czyli **Północ**. | Łatka BK (x1.2). | bk/VanillaModelTweakPatches.cs:654-656 |
| FianHighlander | CZĘŚCIOWY | Prędkość działa, obrażenia mieczem dwuręcznym bez kodu. | Zostawić. | :1065 |
| FianFennid | CZĘŚCIOWY **(niepotwierdzone)** | Obrażenia bronią dwuręczną bez kodu. | Zostawić. | :189 |
| JawwalDuneRider | **KOLIDUJE** | +80% prędkości na pustyni zamiast +8% (Dorne, kultura aserai). | Łatka BK (0.08). | bk/VanillaModelTweakPatches.cs:1070-1072 |
| JawwalGhazw, RitterIronHorses (zdrowie koni) | CZĘŚCIOWY **(niepotwierdzone)** | Działa tylko wtedy, gdy koń ma już jeźdźca w chwili liczenia statystyk. Ghazw daje 10% zamiast 12%. IronHorses (produkcja wsi) działa przez warstwę BEE w połowie. | Sprawdzić. | :98-131 |
| VaryagShieldBrother, VaryagDrengr, KheshigHonorGuard | CZĘŚCIOWY **(niepotwierdzone)** | Jedna połowa bez kodu (mur tarcz, obrażenia wręcz, werbunek u notabli). | Zostawić. | :1186, :252, :1712 |
| ArtisanSmith (BK) | CZĘŚCIOWY | −15% stawki kuźni martwe przy ForgeHireHistorical. −10% staminy kasuje nasz RangedStamina dla zbroi, łuków, kusz i amunicji strzeleckiej, ale dla tarcz i broni rzucanej działa. | Uwzględnić w RangedStamina (z PracticalSmith). | bk/BKSmithingModel.cs:32-34, 100-104; Patches.cs:280-309 |
| ArtisanCraftsman | CZĘŚCIOWY | Jakość warsztatów działa **(niepotwierdzone)**. Lepszy modyfikator kutej rzeczy bez znaczenia dla zbroi (jakość losuje Forge.RollQuality). | Zostawić. | ArmourQuality.cs:11-28 |
| ArtisanEntrepeneur, CivilManufacturer | CZĘŚCIOWY **(niepotwierdzone)** | Produkcja tylko przez warstwę BK na BEE z siłą 0.5. Podatek warsztatów (Entrepeneur) martwy przy NavalDLCWorkshopModel. | Zostawić. | BKEconomyLayerInstaller.cs |
| CivilCultivator, RitterPettySuzerain | CZĘŚCIOWY | +hearth zamieniane przez nasze PopulationLaw na mnożnik „opieki pana” (sufit x2). Rachunek x1.83 **(niepotwierdzone)**. Plon i szlachetni ochotnicy działają. | Zostawić (świadome). | PopulationLaw.cs:687-700 |
| CivilOverseer | CZĘŚCIOWY **(niepotwierdzone)** | Limit infrastruktury bez kodu. | Zostawić. | bk/BKStabilityModel.cs:231 |
| OutlawKidnapper | CZĘŚCIOWY | +30% okupu za lordów podmienia RealisticCaptivity/Armoury przy graczu. Czy LordPrice używa wyniku BK **(niepotwierdzone)**. Skrócenie dezorganizacji działa. | Razem z RansomBroker. | RC/FairRansom.cs:79-86 |
| JomsvikingBoardingFury, DrakkarHelmsman, DrakkarRaidMaster, SjofarandiPathfinder, SjofarandiSeaEyes | CZĘŚCIOWY **(niepotwierdzone)** | Tylko połowa zaimplementowana. SeaEyes działa zawsze, nie tylko nocą. | Zostawić. | bk/NavalPerkPatches.cs |
| GladiatorTourDeCalradia, GladiatorCrowdsFavorite | CZĘŚCIOWY **(niepotwierdzone)** | Zamiast „podwójnie” daje +150 do zakładu i +3 sławy. Zależy od GrandTourney i TXP. | Sprawdzić. | GetMaximumBetlPatch.cs |

Pozostałe perki stylów działają (ok. 47 z 99 BK): August*, Siege*, CivilEngineer, Caravaneer*, CommanderLogistician, OutlawPlunderer, OutlawNightPredator, KheshigRaider, MercenaryLocalConnections, MercenaryRansacker, GladiatorPromisingAthlete, RitterOathbound, VaryagRecognizedMercenary. Przy roku 364 dni VaryagRecognizedMercenary i RitterOathbound trafiają rzadziej w przeliczeniu na dobę.

### 3.6 ROT, RBM i inne mody

- **ROT** nie dodaje własnych perków. Zmienia dwa perki gry: Breeder (tabela Riding) i BeastWhisperer (tabela Scouting). Cechy z XML ROT (Manager, Politician, *FightingSkills, RomanHair…) są MARTWE, bo gra ich nie rejestruje. Wanilia ma ten sam problem, więc to nieszkodliwe.
- **RBM** nie dodaje perków, ale wyłącza ok. 25 efektów perków gry (pancerz, siła ciosu, osłona tarczy, prędkość konia, XP z perków). Szczegóły w tabelach walki.
- **NavalDLC** (62 perki):

| Perk | Stan | Dlaczego | Propozycja | Dowód |
|---|---|---|---|---|
| Boatswain.NavalBombardment | MARTWY | Nikt go nie czyta, nawet w samym DLC. | Zostawić. | NavalPerks.cs:94, 322 |
| Boatswain.WellStocked | MARTWY | Amunicja dodawana po rozstawieniu. Czynny model statystyk (Artem, pod nim słoń ROT) nie przekazuje tej metody dalej **(potwierdzone z kodu, bez logu)**. | Naprawić obejściem obu modeli albo zostawić. | NavalAgentStatCalculateModel.cs:87-118; log 13-09-10:11105 |
| Shipmaster.Commodore (+premie galionów), Boatswain.ShipwrightsInsight (S) | CZĘŚCIOWY | Ta sama przyczyna (Artem + słoń ROT). Część morale Commodore działa. | jw. | NavalAgentStatCalculateModel.cs:30-64, 120-122 |
| Shipmaster.GhostShip | **KOLIDUJE** | Nasz RealisticCaptivity HorseFlight wycina model ucieczki NavalDLC. Na morzu ucieczka nie kosztuje statków i nie wymaga 2 statków. | Naprawić w RC: na morzu oddawać do poprzedniego modelu. | RC/HorseFlight.cs:27, 138 |
| NavalHorde (P), Optimization (P), PopularCaptain (P) | CZĘŚCIOWY | Żołd i werbunek NavalDLC wycięte przez BKPartyWageModel. W logu różnica żołdu na morzu wynosi 0. | Decyzja: dołączyć do BkrotChains albo zostawić. | log 13-09-10:665, 1218 |
| SmoothOperator (S) + bazowe −20% jedzenia na morzu | CZĘŚCIOWY | Model jedzenia NavalDLC wycięty przez BK. | jw. | log :1217 |
| AccuracyTraining (S), NavalFightingTraining (S) | CZĘŚCIOWY | Milicja NavalDLC wycięta przez BKMilitiaModel. | jw. | BK Main.cs:192 |
| Mariner.Arr (S), PirateHunter (S) | CZĘŚCIOWY | XP po bitwie NavalDLC wycięte przez BKPartyTrainningModel. | jw. | log :450 |
| FairWinds (S) | CZĘŚCIOWY | Hearth liczy model RBL/Default, a u nas i tak PopulationLaw. | Zostawić. | log :1840 |
| Resilience (P) | CZĘŚCIOWY | Patrz Medycyna. | Zostawić martwy. | – |
| BruteForce (S) | CZĘŚCIOWY | Łup z najazdów liczy Default (BKROT). | Zostawić. | NavalDLCRaidModel.cs:53 |
| NightRaider | CZĘŚCIOWY | P (+3 wzroku nocą na morzu) martwy. S (ryby dla wsi) działa poza wsiami KS, a w KS jest celowo wyłączony. | Zostawić. | A17Fix.cs:251 |
| EnemyOfTheWood (P), ShockAndAwe (P), TerrorOfTheSeas (P) | CZĘŚCIOWY | Morale w bitwie morskiej idzie do Sandbox, z pominięciem NavalDLC (świadoma decyzja BkrotChains). | Zostawić. | BkrotChains.cs:55 |
| MerchantPrince (S), MasterShipwright (S), Salvage (S), ShipwrightsHand (S) | **KOLIDUJE** | Złoto, podatek i rekrut do garnizonu z niczego. | Płatnik albo wyłączyć. | ShipTradeCampaignBehavior.cs:411-458 |

Pozostałe ok. 38 perków NavalDLC działa, część tylko dzięki naprawie łańcuchów BKROT w Armoury.

- Pozostałe mody (RBL, BEE, SCAI, DTE, Diplomacy, TournamentsXPanded, RBMTournament, EpicSieges…) nie tworzą perków.
- **Dokumentacja:** komentarze w `FoodChain.cs:30,44` i `BkrotChains.cs:43,386,456` podają NavalDLC w łańcuchu żołdu i jedzenia, a log pokazuje, że go tam nie ma. Do poprawienia, żeby nie prowadziły do złych decyzji.

---

## 4. Decyzje dla Jeffa

Zgodnie z CLAUDE.md: jedna zmiana = jedno wgranie, każda z wyłącznikiem w MCM i wpisem w CHANGELOG.

### A. Nasze własne błędy (rekomendacja: naprawić najpierw)
1. **Podwójne perki BK z bitew** (CommanderWarband, FamousSellswords, styl Cataphract). Wyłączyć jedną z dwóch napraw w CrashScribe (`BkBattleRewardPerks` albo `BkBattlePerksFix`). Rekomendacja: **TAK, od razu**. To prosta zmiana konfiguracji, a dziś wszyscy lordowie dostają podwójny wpływ i sławę.
2. **RealisticCaptivity wycina morską ucieczkę NavalDLC** (GhostShip, utrata statków przy ucieczce). Na morzu HorseFlight powinien oddawać sterowanie poprzedniemu modelowi. Rekomendacja: TAK.
3. **Nieaktualne komentarze łańcuchów** w FoodChain i BkrotChains. Poprawić same komentarze. Rekomendacja: TAK, bez ryzyka.

### B. Błędy BannerKings (łatki na BK)
4. **FianRanger** (Północ): x0.2 → x1.2. Rekomendacja: TAK.
5. **JawwalDuneRider**: +0.8 → +0.08. Rekomendacja: TAK.
6. **TheologyMatrimony**: koszt −15% zamiast +5%. Rekomendacja: TAK.
7. **ScholarshipTutor**: sprawdzać perk u członków rodu; poprawić też `ROZWOJ-POSTACI.md`. Rekomendacja: TAK.
8. **LordshipSellswordCareer**: premia +10% zamiast +15%. Rekomendacja: niski priorytet.
9. **CavalryTactics** i **WellStraped** (perki gry zgubione w kopiach modeli BK): dwa małe postfiksy. Rekomendacja: TAK.
10. **Perki konne BK** (Equites, AdaptiveTactics, CamelMaster, Outrider): przenieść efekt na konia albo podmienić opis. Rekomendacja: opis, bo przy RBM premia na koniu i tak może zostać przeliczona.
11. **Martwe, ale wybieralne perki BK** (Klibanophoros, AristocraticRites, RogueConnections, 4 perki nord): podmienić opisy na uczciwe albo dać proste efekty. Courtier i UnderworldKing są nieosiągalne, więc zostawić.

### C. Błędy RBM (cudzy mod)
12. **XP z perków po bitwie** (10 perków: Trainer, LeadByExample, CorpsACorps, BaptisedInBlood, ProjectileDeflection, BullsEye, Resourceful, MountedCrossbowman, Guards, NoRestForTheWicked). Postfiks liczący premię jeszcze raz poprawnie. Rekomendacja: **TAK**, to czysty błąd RBM.
13. **Pancerz z perków** (IgnorePain, ToughSteed, DauntlessSteed): postfiks po RBM. Uwaga na NeckGuard415, RestedArmor416, HeroHp i HorseBarding419. Rekomendacja: TAK, po pkt 12.
14. **Osłona tarczy** (ArrowCatcher, ShieldWall). Rekomendacja: TAK, razem z 13.
15. **Premie od prędkości i siły ciosu** (RecklessCharge, SurgingBlow, Lancer, UnstoppableForce, NomadicTraditions, RunningThrow, SweepingWind, SharpenedEdge, SharpenedTip, DashAndSlash): to sprawa balansu RBM. Rekomendacja: na razie uczciwe opisy, łatka później albo wcale.

### D. Perki nadpisane przez nasze systemy (przenieść efekt do naszego systemu albo zostawić)
16. **Kuźnia:**
    - PracticalSmith i BK ArtisanSmith: doliczyć w RangedStamina. Rekomendacja: TAK.
    - IronMaker: premia przy przekuciu łupy albo opis. Decyzja Jeffa.
    - Crafting.ArtisanSmith: cena naszych zbroi i łuków. Decyzja Jeffa.
17. **MarchPace a perki prędkości** (9 perków Scouting/Tactics): najpierw pomiar w logu, potem decyzja. Rekomendacja: pomiar.
18. **RecruitCost a zniżki werbunku** (6 perków): zniżka perka obniża nasz cel i LimitMin BK. Złoto wraca do notabla przez LevyGold, więc ekonomia zostaje zamknięta. Rekomendacja: TAK.
19. **Okupy** (RansomBroker, ManOfMeans, OutlawKidnapper, Manhunter): doliczyć w okupie 178 i w cenie jeńca, zawsze z płatnikiem. Rekomendacja: TAK, ale osobno i z kontrolą płatnika.
20. **Budowa** (ForcedLabor, SelfMadeMan, TwoHanded.Confidence): dopisać do EngAdd w BuildFunding. Rekomendacja: TAK, mała zmiana.
21. **FormFittingArmor** w naszej zadyszce i biegu (FieldCraft, BattleWind). Rekomendacja: TAK, niski priorytet.
22. RogueExtraordinaire w RaidCache, CivilCultivator i PettySuzerain w PopulationLaw: rekomendacja **zostawić**, bo przeliczenie jest świadome.

### E. „Nic z niczego” (zamknięta ekonomia)
23. **SpringOfGold:** 0 dla AI (jak SmallGold) albo płatnik. Rekomendacja: TAK.
24. **ArchitecturalCommisions:** płatnikiem kasa miasta albo 0. Rekomendacja: TAK.
25. **Veterinarian** (druga premia): koń tylko z poległych koni zbrojowni (HorseFate) albo wyłączyć. Rekomendacja: TAK.
26. **InsurancePlans i RapidDevelopment dla gracza** (5000 zł). Decyzja Jeffa: zostawić jako wyjątek gracza albo dać płatnika.
27. **NavalDLC MerchantPrince, MasterShipwright, Salvage, ShipwrightsHand:** płatnik albo wyłączenie. Rekomendacja: TAK, osobny pakiet.
28. **ROT BeastWhisperer** (dodać filtr „nie na handel”) i **ROT Breeder** (rozmnażanie na statku, zwierzęta z niczego, możliwy NRE przy smokach). Rekomendacja: BeastWhisperer – filtr; Breeder – najpierw sprawdzić NRE.
29. **Perki dodatkowej amunicji** (DeepQuivers, Fletcher, WellPrepared, Saddlebags, Resourceful): najpierw sprawdzić ModifiedMaxAmount w AmmoRecovery **(niepotwierdzone)**.

### F. Perki a realne leczenie (wdrażać razem z nowym systemem, po kolei)
30. **WalkItOff (Medicine i Athletics):** zdjąć premię za ruch i zmienić efekt, np. „mniejsze pogorszenie ran w marszu” albo „szybsze gojenie lekkich ran w obozie”, z angielskim opisem jak w CampDrill. Rekomendacja: **TAK, razem z krokiem „leczenie tylko w spoczynku”**.
31. **TriageTent:** przywiązać do obozu i snu NightRest zamiast do każdego postoju. Rekomendacja: TAK.
32. **AGoodDaysRest:** liczyć też nasz obóz w polu. Rekomendacja: TAK.
33. **Opatrunek chirurga po wygranej:** chirurg leczy wg umiejętności, liczy się też PreventiveMedicine CHIRURGA. Resilience (Naval) zostaje martwy. Rekomendacja: TAK, osobny krok.
34. **Medycyna a łoże HeroWounds:** umiejętność chirurga skraca łoże, CheatDeath chirurga tnie ryzyko śmierci przy przesileniu. Decyzja Jeffa.
35. **Sledges → nosze:** perk łagodzi karę prędkości noszy. Rekomendacja: TAK, przy kroku „nosze”.
36. **RealisticHealingModel (martwy) i zakażenie RB:** zostawić martwy model (decyzja z BkrotChains) albo przenieść jego „brak chirurga −80%” do naszego postfiksu. Zakażenie RB zostawić albo zastąpić regułą „ryzyko w marszu bez obozu, maleje z Medicine, także dla AI”. Decyzja Jeffa.

### G. Łańcuchy modeli NavalDLC (BK je wycina)
37. **Żołd, jedzenie, milicja i trening NavalDLC** (NavalHorde, Optimization, PopularCaptain, SmoothOperator, AccuracyTraining, NavalFightingTraining, Arr, PirateHunter): dołączyć do BkrotChains albo zostawić. Rekomendacja: zostawić do czasu, aż morze będzie ważne w grze; poprawić tylko komentarze (pkt 3).
38. **Model statystyk Artem + słoń ROT** blokuje WellStocked i premie galionów (Commodore, ShipwrightsInsight). Rekomendacja: najpierw licznik w bitwie morskiej, bo to wniosek tylko z kodu.

**Proponowana kolejność:** 1 → 12 → 30-32 (leczenie w spoczynku) → 4-7 → 33 → 13-14 → 23-25 → 18-20 → reszta.
