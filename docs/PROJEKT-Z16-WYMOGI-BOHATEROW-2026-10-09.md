# PACZKA Z16 - WYMOGI SPRZETU U BOHATEROW (projekt dla wykonawcy, 09.10.2026)

Status: PROJEKT. Kod nieruszany, nic nie zbudowane, gra i autotest nieuruchamiane, nic nie wgrane, nic nie wypchniete.
Paczka idzie PO 175 (te same miejsca w Mends.cs) - na galeziach `w-toku/175-armie-cs` (HEAD 537e93f) i `w-toku/175-armie-arm` (HEAD 1402107).

Decyzje Jeffa (STAN-PRAC 09.10: 02:50, 03:05 SPROSTOWANIE, 03:55) i zasada z 29.08:
- 02:50: **"jak nie mam danej umiejetnosci, np. atletyki, nie moge zalozyc pancerza, ktory ma takie wymaganie"**;
- 29.08 (ItemReq.cs:8-9): "umiejetnosci sa swiete - jesli ich nie masz, nie mozesz uzywac. Konie i pancerze tez, CALY ekwipunek!";
- 03:05 (z tego): (1) gracz, towarzysze i lordowie nie zaloza pancerza (Atletyka) ani amunicji (strzaly - Luk, belty - Kusza) ponad swoja umiejetnosc,
  ta sama regula ItemReq co u zolnierzy, na ekranie czerwony powod po angielsku jak w grze; (2) lordom AI i towarzyszom Atletyka/Luk/Kusza
  podniesiona do wymogu ich WLASNEGO obecnego sprzetu bojowego (SkillSinew dla bohaterow, tylko w gore, przy wczytaniu) - GRACZ bez podnoszenia;
  (3) zbroja juz noszona NIE jest zdejmowana na sile (blokada tylko przy zakladaniu);
- 03:55: (4) BEZ dodatkowego treningu Atletyki w zbroi (Atletyka rosnie z marszu pieszo).

Podstawa: audyt `docs/audyt-2026-10-09/13-UMIEJETNOSCI.md` (wiersz Athletics, Z16, pytanie b; recenzja pkt 13-14).

Oznaczenia zrodel (numery linii z ilspycmd 9.1 na DLL z gry 1.4.8, katalog `SCR` = `C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\7016f733-d379-418e-b700-f66fd52e4d2b\scratchpad`):

| znak | co | gdzie |
|---|---|---|
| [G] | gra, TaleWorlds.CampaignSystem.dll | `SCR\z16\cs` |
| [GV] | gra, TaleWorlds.CampaignSystem.ViewModelCollection.dll | `SCR\z16\vm` |
| [BK] | BannerKings.dll | `SCR\z16\bk` |
| [BKR] | BKROTPatch.dll | `SCR\bkrot-dec` |
| [ROT] | ROT.dll (ROT-Core) | `SCR\umiejetnosci\dec\rot` |
| [SPO] | Spoils of War (RealisticLoot.dll) | `SCR\umiejetnosci\dec\spoils` |
| [DTE] | DynamicTroopEquipmentReupload 1.4.7 | `SCR\umiejetnosci\dec\dte` |
| [A] | Armoury, drzewo 175 (HEAD 1402107) | `SCR\noc2\a175arm\Armoury\src` |
| [CS] | CrashScribe, drzewo 175 (HEAD 537e93f) | `SCR\noc2\a175cs\CrashScribe\src` |
| [RC] | RealisticCaptivity (drzewo 175 Armoury) | `SCR\noc2\a175arm\RealisticCaptivity\src` |
| [S] | szacunek skryptem (XML + `items-dump.csv` z sesji 09.10 07:26) | `SCR\z16\py\heroreq2.py` |

W [G] i [GV] skracam katalogi dekompilacji: `CampaignBehaviors/` = `TaleWorlds.CampaignSystem.CampaignBehaviors/`, `GameComponents/`,
`Actions/`, `Inventory/`, `TournamentGames/`, `CharacterDevelopment/` tak samo; `Hero.cs`, `HeroCreator.cs`, `Campaign.cs`, `CharacterData.cs` =
`TaleWorlds.CampaignSystem/...`; `Helpers/` bez zmian; w [GV] `Inventory/` = `TaleWorlds.CampaignSystem.ViewModelCollection.Inventory/`.

SandBox.dll, StoryMode.dll, NavalDLC.dll (`SCR\z16\sb`, `sm`, `nv`) i pozostale mody (Diplomacy, RBM, TournamentsXPanded, StrategicCampaignAI,
DynamicReinforcements, BirthAndDeath) przeszukane: nie zakladaja sprzetu bohaterom (poza kreatorem postaci StoryMode, ktorego ROT nie uzywa).

---

## 0. Dla Jeffa - prostym jezykiem

1. Od Z16 Ty i Twoi towarzysze nie zalozycie na ekranie ekwipunku zbroi, do ktorej brakuje Wam Atletyki, ani strzal i beltow ponad Luk i Kusze:
   karta przedmiotu robi sie czerwona, gra pisze jak przy mieczach "You don't have enough Athletics skill to equip this item", a w opisie
   przedmiotu widac "Requires: Athletics 175".
2. To, co juz masz na sobie, zostaje na Tobie (takze gdy w niewoli spadnie Ci Atletyka) - ale jak to zdejmiesz, zalozysz z powrotem dopiero,
   gdy Atletyka urosnie (rosnie z marszu pieszo, bez osobnego treningu).
3. Ta sama regula pilnuje AI wszedzie tam, gdzie ktos wybiera nowa sztuke: auto-ekwipunek towarzyszy w Spoils, lord kupujacy albo zdobywajacy
   unikat, zamienniki unikatow, zestaw z zaciagu ROT - czego nie udzwignie, idzie do taboru, nic nie znika.
4. Zeby nikt nie stracil zbroi, lordom, towarzyszom i Twojej doroslej rodzinie (Tobie nigdy) podnosze Atletyke, Luk i Kusze do ich wlasnego
   zestawu bojowego: na nowej kampanii to ok. 740 z 890 doroslych lordow (Atletyka srednio +50, prawie zawsze do 175, bo prawie kazdy lord ma
   choc jedna sztuke tieru 6), ok. 220 lordow Luk (+48), 22 Kusze (+69) i ok. 70 ze 188 rodzajow wedrowcow (towarzyszy).
5. W bitwie tacy lordowie biegaja do ok. 9% szybciej i maja do ok. 17% wiecej oddechu, a ok. 220 rycerzy wiozacych strzaly t3-t4 bedzie lepiej
   strzelac; autobitwa gry Atletyki nie liczy, a dokladne liczby z Twojego zapisu poda log przy pierwszym wczytaniu (wszystko ma wylacznik w MCM).

---

## 1. Regula (jedna)

### 1.1 Wymog przedmiotu - bez zmian, ItemReq

Jeden egzekutor: `[A] ItemReq.cs:18-33` (`Meets`) i `:49-58` (`SkillFor`). Wymog = `ItemObject.Difficulty` PO prawach CrashScribe przy starcie
sesji (`[CS] Mends.cs:2026` WeightLaw 0.25 kg/pkt, `:2305` ArmorTierLaw (tier-1) x 35, `:2356` WeaponTierLaw (tier-1) x 35, takze strzaly i belty).

| przedmiot | umiejetnosc | kto dzis pilnuje u bohatera |
|---|---|---|
| bron, tarcza | jej RelevantSkill (gra) | gra, tylko ekran: `[G] Helpers/CharacterHelper.cs:566-577` |
| kon | Jazda (gra) | gra, tylko ekran (ta sama metoda) |
| pancerz: helm, korpus, nogi, rece, peleryna | Atletyka | **nikt** (RelevantSkill pancerza = null, `ItemObject.RelevantSkill` w TaleWorlds.Core) |
| strzaly | Luk | **nikt** |
| belty | Kusza | **nikt** |
| ladry konskie | (patrz 7.1) | nikt - **poza Z16** |

Z16 dodaje u bohaterow tylko dwa brakujace wiersze (pancerz, amunicja) - bron i konie dalej pilnuje gra, ta sama metoda, ten sam komunikat.
Sito bohaterow = `ItemReq.Meets` z jednym wyjatkiem: ladry (`ItemType == HorseHarness`) przepuszcza, bo `ItemReq.SkillFor` liczy je dzis jako
Atletyke (7.1) - jedna mala metoda `ItemReq.MeetsHero(ch, it)` w ItemReq.cs, uzywana przez wszystkie drogi z rozdz. 2.

### 1.2 Trzy rodzaje zdarzen

| zdarzenie | przyklady | co robi Z16 |
|---|---|---|
| **ZAKLADANIE** - ktos wybiera nowa sztuke | ekran ekwipunku, auto-ekwipunek Spoils, lord kupuje/zdobywa unikat, zamiennik unikatu, zestaw z zaciagu ROT, zamiennik broni zabranej przez gracza | sito `ItemReq.Meets`; czego nie udzwignie - do taboru (albo zostaje, gdzie lezalo) |
| **NOSZENIE** - sztuka juz byla na tym bohaterze | to, co nosi; zwrot wlasnego rynsztunku po niewoli; przycisk Reset/Cancel ekranu; naprawa i stan tej samej sztuki | nic nie zdejmujemy (decyzja 3) |
| **PRZYDZIAL Z SZABLONU** - gra albo mod daje bohaterowi AI jego komplet | start kampanii, pelnoletnosc, nowy bohater (wedrowiec, rycerz i towarzysz BK, ziemianin BK), nowy wladca, towarzysz zostaje lordem, unikat z kanonu (DressTheNamesakes) | podniesienie Atletyki/Luku/Kuszy do tego kompletu (decyzja 2, ta sama zasada co SkillSinew u zolnierzy z 15.09: "nie zmieniaj sprzetu, podnies umiejetnosci, zeby DEFAULTOWY sprzet spelnial wymagania") |

Przydzial z szablonu traktuje jak "obecny sprzet" z decyzji 2: przy nastepnym wczytaniu i tak zostalby podniesiony, wiec podniesienie od razu
zmienia tylko chwile, nie wynik - i nie ma okna, w ktorym bohater AI nosi cos ponad umiejetnosc.

### 1.3 Kogo dotyczy

- **Gracz** (`Hero.MainHero`): tylko sito. Nigdy podnoszenie. Jego start pilnuje juz StartKit (`[A] StartKit.cs:85-101`, oba zestawy).
- **Bohaterowie AI** = kazdy zywy bohater poza graczem, dorosly (`Age >= AgeModel.HeroComesOfAge`), nie notable: lordowie AI, towarzysze
  (gracza i AI), dorosla rodzina gracza (zona, dzieci - to nie Ty), rycerze i ziemianie BK, bohaterowie band BK: sito + podniesienie.
- Dzieci (143 na starcie [S]) - bez podnoszenia: dostaja sprzet i umiejetnosci przy pelnoletnosci (`[G] AgingCampaignBehavior.cs:252-283`), wtedy
  dziala przydzial z szablonu.
- Notable (kupcy, soltysi, kaplani - ok. 2 700) - poza: nie wychodza w pole.

### 1.4 Zestawy

- Sito: zestaw bojowy, cywilny i skrytobojczy (ekran nie rozroznia; tak samo robi StartKit - oba zestawy). Uwaga: suknie i szaty postaci ROT maja
  w danych pancerz 99-132, czyli tier 6 i wymog 175 (rozdz. 7.2) - takze w stroju cywilnym.
- Podniesienie: tylko zestaw BOJOWY (decyzja 2 "sprzetu bojowego"), sloty 5-9 (pancerz) i 0-3 (strzaly, belty). Bez broni, koni i ladr.

---

## 2. Wszystkie drogi zakladania sprzetu przez bohaterow

"Dzis" = stan drzew 175. "Po Z16" = co zrobi paczka (szczegoly w rozdz. 5).

### 2.1 Ekran gracza i towarzyszy (gracz wybiera)

| # | droga | plik:linia | dzis | po Z16 |
|---|---|---|---|---|
| E1 | ekran ekwipunku (przeciaganie, dwuklik, zakup z zalozeniem) | `[GV] Inventory/SPInventoryVM.cs:3475` ProcessEquipItem -> `:4130` EquipEquipment -> `:4029` IsItemEquipmentPossible -> `:4094-4098` CanCharacterUseItem + komunikat -> `:4120-4128` -> `[G] Helpers/CharacterHelper.cs:566-587` (2-arg `:560-564` wola 3-arg) | blokuje tylko RelevantSkill (`:571-577`); pancerz i amunicja przechodza | postfiks na `CharacterHelper.CanUseItem` (3-arg): pancerz/strzaly/belty przez `ItemReq.Meets`, powod: tekst gry `{=rgqA29b8}You don't have enough {SKILL_NAME} skill to equip this item` z SKILL_NAME = Athletics/Bow/Crossbow |
| E2 | czerwona karta na liscie i na postaci | `[GV] SPInventoryVM.cs:4468-4477` RefreshCharacterCanUseItem, `:4394`, `:4519`, `:4527`, `:3880-3905` | j.w. | ta sama metoda - czerwien przyjdzie sama |
| E3 | opis przedmiotu (dymek) | `[GV] Inventory/ItemMenuVM.cs:616-623` (linia "Requires" tylko gdy RelevantSkill), `:631-647` AddSkillRequirement | pancerz i amunicja bez linii wymogu | postfiks na `ItemMenuVM.SetItem` (obok `[A] TooltipCondition.cs:84`): "Requires: Athletics 175" zielono/czerwono wedlug `ItemReq.Meets` dla `_character` |
| E4 | Reset / Cancel ekranu | `[G] Inventory/InventoryLogic.cs:74-90` ResetEquipment | przywraca stan sprzed ekranu | NOSZENIE - bez sita (wraca to, co juz nosil) |
| E5 | Spoils "Auto-equip companions" (Kwatermistrz w twierdzy) | `[SPO] RealisticLoot.Behaviors/QuartermasterBehavior.cs:354` (opcja), `:1510-1539` -> `RealisticLoot.Models/AutoEquipPlanner.cs:17-53` Execute -> `:85-163` TryUpgradeSlot (sito `:126`, zapis `:153`, kopia do cywilnego `:154-157`) | **zadnej kontroli** - nawet broni; towarzysz dostaje plyte t6 z taboru gracza | sito w TryUpgradeSlot (prefiks podmienia tabor na kopie z samymi sztukami, ktore ten towarzysz udzwignie; postfiks oddaje roznice prawdziwemu taborowi) |
| E6 | Spoils "Equip and train the leader" (podklan) | `[SPO] RealisticLoot.Behaviors/SubClanBehavior.cs:294` (opcja), `:1580` -> `:1670` OnEquipLeaderScreenClosed -> `:1722` do taboru partii bohatera | rzeczy ida do TABORU; nikt ich nie zaklada (gra nie ubiera AI z taboru - rozdz. 2.2, G10) | bez zmian (napis bez obietnicy - Z1, wgrane 17a700d7) |
| E7 | StartKit (pierwsza godzina po kreatorze) | `[A] StartKit.cs:85-101` (wlasna kopia regul `:37-56`, ta sama co ItemReq) | juz sito, oba zestawy i tabor | bez zmian |
| E8 | ROT zaciag (gracz w sluzbie lorda): na bitwe bohater klanu gracza dostaje zestaw jednostki swojej rangi | `[ROT] ROT.HarmonyPatches.Core/EnlistmentPatches.cs:548-550` patch24 (prefiks Mission.SpawnAgent) -> `:609-619` (zaciag, zestaw jednostki) -> `:637-646` AgentOverridenEquipment | zestaw jednostki bez kontroli (tylko na misje) | sito przy spawnie: sztuka wydana, ktorej bohater nie udzwignie, zastapiona jego wlasna z tego slotu (galaz bohatera `[A] DragonUnmount.cs:176`, prefiks po ROT) |
| E9 | RC niewola: zabranie i zwrot rynsztunku gracza i towarzyszy | `[RC] CaptivityBehavior.cs:161-197` StripGear, `:199-220` StripCompanionGear, `:222-247` RestoreCompanionGear (`:243`), `:735-757` RestoreGear (`:750-751`) | zwraca wlasne sztuki | NOSZENIE - bez sita |
| E10 | RC zanik miesni w lochu obniza Atletyke gracza | `[RC] Captivity2.cs:84-120` (`:116` SetInitialSkillLevel w dol) | - | zbroja zostaje na graczu (decyzja 3); na ekranie bedzie czerwona |
| E11 | kowal, naprawa, ulepszenie jakosci tej samej sztuki | `[A] SmithMenu.cs:1135`, `[A] ArmouryBehavior.cs:1914-1945` CleanseAmmo, `[ROT] ROT.CampaignBehaviors/ROTTownTradersBehavior.cs:945-948` | ta sama sztuka, inny stan | bez zmian |
| E12 | nagrody: turniej, questy, kuznia, lupy gracza | `[G] TournamentGames/TournamentManager.cs:180-193` (gracz: do taboru `:186-188`, AI: zloto `:189-192`) | do taboru | zakladanie przez ekran (E1) |

### 2.2 AI - gra (gardlo: `EquipmentHelper.AssignHeroEquipmentFromEquipment`)

`[G] Helpers/EquipmentHelper.cs:8-16` przepisuje 12 slotow bez zadnej kontroli. To jedyne gardlo gry i modow (16 wywolan w grze + BK, BKROTPatch,
StoryMode); CrashScribe juz ma na nim prefiks DressedOrNot (`[CS] Mends.cs:2738-2751`, metoda `:3568`).

| # | droga | plik:linia | rodzaj | po Z16 |
|---|---|---|---|---|
| G1 | nowy bohater (wedrowiec, rycerz BK, ziemianin BK, krewny-notable, wedrowcy ROT) | `[G] HeroCreator.cs:297` InitializeHeroFromSettings: umiejetnosci `:328-331`, sprzet `:341-344` | przydzial | podniesienie (kolejka z postfiksu gardla) |
| G2 | pelnoletnosc (18 lat) | `[G] CampaignBehaviors/AgingCampaignBehavior.cs:252` OnHeroComesOfAge: umiejetnosci `:258-264` PRZED sprzetem `:270-283` | przydzial | podniesienie |
| G3 | nastolatek, dzieci na starcie | `AgingCampaignBehavior.cs:196-204` (stroj cywilny), `InitialChildGenerationCampaignBehavior.cs:70-81` | przydzial dziecku | poza (dzieci) |
| G4 | nowy wladca krolestwa (i stary wladca) | `[G] CampaignBehaviors/NPCEquipmentsCampaignBehavior.cs:17-33` (model `GameComponents/DefaultEquipmentSelectionModel.cs:42-55`) | przydzial | podniesienie |
| G5 | towarzysz zostaje lordem (lenno, wlasny klan) | `[G] CampaignBehaviors/CompanionRolesCampaignBehavior.cs:291-318` AdjustCompanionsEquipment (sztuka szablonu, gdy wyzszy tier) | przydzial | podniesienie |
| G6 | zwolniony wedrowiec wraca do swojego szablonu | `[G] Actions/RemoveCompanionAction.cs:47-50` -> `Hero.cs:2286-2291` ResetEquipments (OMIJA gardlo) | przydzial | dzienny przeglad (rozdz. 3.3) |
| G7 | sztuka usunieta z gry -> losowa z szablonu | `[G] Hero.cs:2353-2389` HandleInvalidItem, wolane z `Campaign.cs:1672-1679` PO OnSessionStart (OMIJA gardlo) | przydzial | dzienny przeglad |
| G8 | start kampanii: bohater z XML dostaje umiejetnosci szablonu + szum 5-9 i losowy zestaw | `[G] Hero.cs:1804-1813` Deserialize, `GameComponents/DefaultHeroCreationModel.cs:361-379`, `:462-466`; zestaw `Hero.cs:2237`, `:2252-2284` | przydzial | przeglad przy wczytaniu + pierwszy dzien |
| G9 | poprawka starych zapisow (< 1.4.1) | `[G] Hero.cs:1722-1760` | - | nie dotyczy (zapisy 1.4.8) |
| G10 | AI nie ubiera sie samo z taboru ani z lupow | jedyne zapisy sprzetu bohaterow w calym TaleWorlds.CampaignSystem to G1-G9, ekran (E1, E4) i cheat importu postaci (`CharacterData.cs:178-225`); `CanUseItem` wolaja tylko DLL gry z ekranem (przeszukane wszystkie DLL gry i modow) | - | nic |

### 2.3 AI - BannerKings, BKROTPatch, DTE

| # | droga | plik:linia | po Z16 |
|---|---|---|---|
| B1 | BK: klan AI werbuje towarzysza (kupuje mu komplet w miescie) | `[BK] BannerKings.Behaviours/BKClanBehavior.cs:990` EvaluateRecruitCompanion -> `:1026-1031` | przez gardlo -> podniesienie |
| B2 | BK: klan AI pasuje rycerza (losowy komplet IsLordTemplate kultury) | `[BK] BKClanBehavior.cs:1080` EvaluateRecruitKnight -> `:1134-1136`; wersja BKROTPatch `[BKR] BKROTPatch.Patches/EvaluateRecruitKnightPatch.cs:127` | przez gardlo -> podniesienie |
| B3 | BK: ziemianie (gentry) i ich dzieci | `[BK] BKGentryBehavior.cs:431` InitializeGentry (`:444`), `:485` CreateGentryClan -> `:492`, `:548`, `:586-589` | przez gardlo -> podniesienie |
| B4 | BK: bohater bandy | `[BK] BKBanditBehavior.cs:190` CreateBanditHero -> `:230` | przez gardlo -> podniesienie |
| B5 | BK poza tym nie zmienia sprzetu bohaterow i nie pyta CanUseItem (przeszukane 757 plikow) | - | nic |
| D1 | DTE pomija bohaterow wszedzie | `[DTE] Patches/SpawnAgentPatch.cs:88`, `PartyEquipmentDistributor.cs:219`, `EveryoneCampaignBehavior.cs:492`, `ArmyArmory.cs:338` | nic |

### 2.4 AI - nasze mody i ROT

| # | droga | plik:linia | dzis | po Z16 |
|---|---|---|---|---|
| U1 | zwyczaj wojenny: kto pojmie albo zabije w bitwie bohatera z unikatem, ten go zaklada | `[A] UniqueSpoils.cs:114-129` (zdarzenia `ArmouryBehavior.cs:583-584`) -> `:131-156` Take (zamiennik dla ofiary `:144-145`, zalozenie `:147`) -> `:159-170` Wear | lord AI zaklada bez kontroli | zaklada, gdy udzwignie; inaczej do taboru jego partii; bez taboru - unikat zostaje na ofierze; zamiennik ofiary w granicach jej umiejetnosci (U3) |
| U2 | lord AI kupuje unikat z polki miasta | `[A] UniqueSpoils.cs:190-220` (`:204` sprawdza tylko RelevantSkill - zbroje unikatowe bez kontroli; `:213` Wear) | zbroja Brienne t6 dla kazdego, kogo stac | `:204` -> `ItemReq.MeetsHero(lord.CharacterObject, it)` |
| U3 | prawo unikatow: nie-wlasciciel oddaje unikat, dostaje zamiennik (takze gracz) | `[A] UniqueLaw.cs:461-500` SweepHeroes (zapis `:480`, wolane przy wczytaniu `:143`), `:182-237` StandInFor (pamiec "unikat\|kultura" `:186-190`) | zamiennik tier <= unikatu, ale bez patrzenia na umiejetnosc (waga moze byc wieksza) | StandInFor z noszacym: tylko sztuki, ktore on udzwignie; klucz pamieci + jego umiejetnosc |
| U4 | unikaty kanonu: Ramsay, Cersei, Stannis, Dany, Renly... zakladaja swoje (raz na kampanie) | `[CS] Mends.cs:705-765` DressTheNamesakes (zapis `:744`), `:768-781` FindAliveHero (gracz NIE jest wylaczony) | bez kontroli | AI: przydzial z kanonu -> podniesienie; gracz (gdyby sie tak nazywal): zaklada tylko, gdy udzwignie, inaczej sztuka na polke miasta |
| U5 | bitwa: wierzchowiec wedlug geografii i pancerz olbrzyma na bohaterze | `[A] DragonUnmount.cs:38`, galaz bohatera `:176-200` (TopMount w granicy Jazdy) | juz w regule | tu dochodzi E8 |
| U6 | straznik bitwy SkillLawWard | `[CS] Mends.cs:1160`, bohaterowie pomijani `:1184` | nie zdejmuje bohaterom | bez zmian (decyzja 3) |
| U7 | zakupy AI dla wojska | `[A] AiGear.cs` | tylko zolnierze | nic |
| R1 | ROT: gracz zabiera jencowi legendarna bron, jeniec dostaje losowa bron tego typu | `[ROT] ROT.CampaignBehaviors/ROTGankBehavior.cs:109-138` TakeWeapon (`:137`), `:196` GetRandomItem | bron bez kontroli (gra u AI broni nie pilnuje) | sito na wyniku GetRandomItem (najlepsza bron tego typu, ktora jeniec udzwignie) - maly zasieg |
| R2 | ROT: smok Daenerys wraca do slotu konia po uwolnieniu; kon upiora | `ROTGankBehavior.cs:45-66`, `ROT.HarmonyPatches.Core/OthersPatches.cs:146` | wierzchowce lore | bez zmian |
| R3 | ROT: wedrowcy unikatowi - tylko stany sztuk | `ROT.CampaignBehaviors/ROTSpawnUniqueWanderers.cs:80-108` | sam bohater przez G1 | nic |

### 2.5 Smierc i dziedziczenie

| # | zdarzenie | co sie dzieje ze sprzetem | po Z16 |
|---|---|---|---|
| S1 | smierc w bitwie | unikaty -> zwyciezca (U1); reszta odchodzi z bohaterem (gra nic nie przekazuje) | U1 |
| S2 | smierc gracza, dziedzic przejmuje gre | dziedzic zostaje w swoim sprzecie; byl bohaterem AI klanu, wiec mial juz podniesienie; od chwili przejecia - tylko sito | nic |
| S3 | nowy wladca | G4 | podniesienie |
| S4 | spadkobierca unikatu kanonu (np. korona Roberta -> Stannis) | U4 (raz na kampanie) | podniesienie |
| S5 | nowa glowa klanu | sprzet bez zmian | nic |

Poza zakresem (sprzet tylko na czas misji, wydawany wszystkim): stroje turniejowe i areny (`SandBox.Tournaments.MissionLogics/TournamentFightMissionController.cs:179`),
bijatyki w zaulkach (`SandBox.Missions.MissionLogics/MissionFightHandler.cs:134-135`), bron podniesiona z ziemi w bitwie.

---

## 3. Podnoszenie umiejetnosci bohaterow AI (HeroSinew, CrashScribe)

### 3.1 Funkcja (jedna, dla wszystkich chwil)

`Mends.HeroSinew(Hero h)` obok `SkillSinew` (`[CS] Mends.cs:2409-2490`, ktore zostaje dla zolnierzy z `co.IsHero continue` `:2422`):

- pomija: gracza, martwych, dzieci (`Age < HeroComesOfAge`), notabli, bohatera bez HeroDeveloper;
- wymog = maksimum `Difficulty` w ZESTAWIE BOJOWYM: Atletyka - sloty 5-9 z typem HeadArmor/BodyArmor/LegArmor/HandArmor/Cape; Luk - sloty 0-3 Arrows;
  Kusza - sloty 0-3 Bolts (to samo mapowanie co `ItemReq.SkillFor` i `Mends.ReqSkill` `:1033-1042`; bez broni, koni i ladr);
- gdy `h.GetSkillValue(s) < wymog`: `h.HeroDeveloper.SetInitialSkillLevel(s, wymog)` (`[G] CharacterDevelopment/HeroDeveloper.cs:190-196`: ustawia
  umiejetnosc i XP pod nia, bez awansu poziomu, bez perkow i bez komunikatu - tak jak RC przy zaniku `[RC] Captivity2.cs:116`). Nie
  `ChangeSkillLevel` (`:176-188`) - ten idzie przez AddSkillXp i zdarzenie OnHeroGainedSkill;
- tylko w gore; nigdy nie zdejmuje sprzetu.

### 3.2 Kiedy

1. **Przy wczytaniu i na nowej kampanii**: w lancuchu `[CS] Mends.cs:4705` PO `DressTheNamesakes` (U4), wszystkie zywe bohatery; na nowej
   kampanii jeszcze raz z pierwszym dniem (jak `SinewApplied` `:4718`).
2. **Po przydziale z szablonu**: postfiks na `EquipmentHelper.AssignHeroEquipmentFromEquipment` (to samo gardlo co DressedOrNot) tylko DOPISUJE
   bohatera do kolejki; kolejka liczona w najblizszej godzinie. Dlaczego kolejka: BK przy rycerzu ubiera dwa razy (HeroCreator `:341-344`, potem
   `BKClanBehavior.cs:1136`) - liczymy tylko komplet koncowy, wiec nikt nie dostaje Atletyki pod zbroje, ktorej juz nie nosi. Harmony puszcza
   postfiks takze po prefiksie DressedOrNot zwracajacym false - postfiks ma straznika nulli.
3. **Dzienny przeglad**: ta sama funkcja dla wszystkich bohaterow AI raz na dobe (ok. 1 000-1 500 bohaterow x 7 slotow - pomijalne). Lapie drogi
   omijajace gardlo (G6, G7) i przyszle mody. Linia w logu z imionami - normalnie 0; liczba > 0 oznacza nieznana droge do zbadania.

### 3.3 Zapis i ustawienia

- Bez nowego stanu w zapisie: podniesienie jest trwale w umiejetnosciach bohatera i idempotentne (drugi raz nic nie robi), wiec zadnego SyncData
  (gdyby w wykonaniu wyszedl napis do zapisu - tylko przez `SaveText.Sync`).
- MCM Armoury (po angielsku, `tools/gen_mcm.py`): `HeroGearRequirements` (dom. TAK) - sito u bohaterow (rozdz. 4 etapy 2-4);
  `HeroSkillToOwnGear` (dom. TAK) - podniesienie bohaterow AI. CrashScribe czyta je refleksja jak `ArmouryFloat` (`[CS] Mends.cs:1807`).
  Wylaczenie podniesienia nie obniza juz podniesionych (tylko w gore).

---

## 4. Liczby: kto dostanie podniesienie i o ile [S]

Skrypt `SCR\z16\py\heroreq2.py`: wymogi przedmiotow z `Documents\Mount and Blade II Bannerlord\CrashScribe\items-dump.csv` (sesja 09.10 07:26, PO prawach
wagi i tieru), umiejetnosci z XML (szablon SkillSet + skille postaci + szum gry +7, sprawdzone tez +5 i +9 - roznice ponizej 2 pkt), zestawy bojowe
z rosterow ROT. To stan **nowej kampanii**; w zapisie Jeffa liczby beda nieco mniejsze (umiejetnosci urosly) - dokladne poda log przy pierwszym
wczytaniu. Bohaterowie w `ROT_heroes.xml`: 1 034 zywych (bez gracza), z tego 143 dzieci -> **890 doroslych lordow** (1 bez zestawu bojowego).

| grupa | Atletyka | Luk (strzaly) | Kusza (belty) |
|---|---|---|---|
| lordowie dorosli (890) | **740** (83%); mediana +38, srednia +50, max +98 | **224** (25%); srednia +48, max +148 | **22**; srednia +69 |
| wedrowcy unikatowi ROT (63) | **29**; mediana +48, srednia +64, max +153 | 0 | 0 |
| szablony wedrowcow ogolnych (125) | **42**; mediana +35, srednia +80, max +173 | 0 | 0 |

Rozklad Atletyki u lordow: 0 - 150 lordow; +1..25 - 354; +26..50 - 77; +51..75 - 85; ponad +75 - 224.

Wedlug szablonu lorda (Atletyka startowa -> wymog zestawu, prawie zawsze 175, bo 846 z 890 zestawow ma choc jedna sztuke t6):

| szablon | lordow | Atletyka na starcie | podniesienie |
|---|---|---|---|
| rycerz (knight) | 365 | 157 | 345 osob, srednio +18 |
| mlody szlachcic (dandy) | 232 | 77 | 232 osoby, srednio +96 |
| pani zamku (chatelaine) | 70 | 107 | 70 osob, srednio +68 |
| zeglarz Zelaznych Wysp (sailor_viking) | 66 | 177 | 0 |
| jezdziec (cavalry) | 34 | 137 | 34, srednio +38 |
| kwatermistrz glowy rodu | 22 | 137 | 22, srednio +36 |

Luk: glownie rycerze z Lukiem 27, ktorzy wioza `vlandic_arrows` (t3, wymog 70; 149 zestawow) albo `bodkin_arrows_a` (t4, 105; 111 zestawow).
Kusza: belty t4 (105) w 25 zestawach. Najwieksze podniesienia wedrowcow: Wun Wun +153 (szata olbrzyma, wymog 200), Varys i Kinvara +145 (ich
szaty maja pancerz 99 -> tier 6 -> 175).

Porownanie z audytem 13 ("co najmniej 268 z 811 lordow ponad wymog"): tamten rachunek liczyl tylko Prawo Wagi (0.25 kg/pkt); dzis dziala tez Prawo
Tieru pancerza (35 na tier, `Mends.cs:2305`), stad 740.

Skutek w bitwie (wzory z audytu 13, wiersz Athletics): predkosc biegu 0.7 x (1 + 0.001 x Atletyka) - dla szlachcica 77 -> 175 ok. +9%, dla rycerza
157 -> 175 ok. +2%; oddech BattleWind x(1 + Atletyka/500) - odpowiednio ok. +17% i +3%. Luk 27 -> 70-105 u ok. 220 rycerzy: celniej strzelaja.
Autobitwa gry Atletyki nie liczy (`[G] GameComponents/DefaultCombatSimulationModel.cs` - tylko Taktyka `:308`); poziom bohatera bez zmian.

---

## 5. Plan wykonania (kazdy etap osobno: build kod 0, wpis CHANGELOG "Z16-n, NIEWGRANE", commit lokalny)

**Etap 1 - CrashScribe, podniesienie (drzewo `a175cs`, `Mends.cs`).** `HeroSinew` (rozdz. 3.1); wywolanie w lancuchu `:4705` po DressTheNamesakes
i z pierwszym dniem nowej kampanii; postfiks na gardle (obok DressedOrNot `:2738-2751`) z kolejka i godzinnym przeliczeniem; dzienny przeglad
w `MendsBehavior` (`:4699`, linia dnia `:4713`); DressTheNamesakes: gracz jako noszacy tylko, gdy udzwignie (inaczej polka). Log: "Z16: bohaterowie AI ponad wymog
przed: Atletyka N1, Luk N2, Kusza N3; podniesiono X lordow, Y towarzyszy, Z innych (srednio +A Atletyki); po: 0; gracz bez zmian (Atletyka P)".

**Etap 2 - Armoury, ekran (drzewo `a175arm`).** Postfiks na `CharacterHelper.CanUseItem(BasicCharacterObject, EquipmentElement, out TextObject)`:
gdy wynik gry to "moze", a sztuka ma `RelevantSkill == null` i `!ItemReq.MeetsHero(...)` (pancerz bez ladr, strzaly, belty) - wynik "nie moze"
i powod z tekstu gry (rozdz. 2.1 E1).
Nigdy nie zmienia "nie" na "tak". Postfiks na `ItemMenuVM.SetItem` dopisujacy "Requires: Athletics N" (pancerz) / "Bow N" (strzaly) / "Crossbow N"
(belty) kolorem `GetColorFromBool` (prywatne `CreateColoredProperty` i `_requiresText` przez refleksje), takze dla porownywanej sztuki.
Ustawienia MCM (rozdz. 3.3) + `python tools/gen_mcm.py`.

**Etap 3 - Armoury, drogi AI.** U1/U2 (`UniqueSpoils.cs:144-147`, `:159-170`, `:204`); U3 (`UniqueLaw.StandInFor` z noszacym, wolania `:479`
i `UniqueSpoils.cs:144`); E5 Spoils (prefiks/postfiks `RealisticLoot.Models.AutoEquipPlanner.TryUpgradeSlot`, typ szukany refleksja - bez Spoils
nic sie nie dzieje).

**Etap 4 - Armoury, drogi wojenne (male).** E8 zaciag ROT w galezi bohatera `DragonUnmount.cs:176` (prefiks z `Priority.Low`, zeby szedl po
`patch24` ROT); R1 postfiks na `ROTGankBehavior.GetRandomItem`.

Ryzyko / co sprawdzic (zasada glowna z 05.10):
- **Regresje**: CanUseItem woluja tylko ekrany (przeszukane DLL) - zadna logika AI na nim nie stoi; postfiks tylko dodaje warunek. SkillSinew
  zolnierzy, SinewApplied i bonusy 175 (NorthHardy, DothrakiRiders) - nietkniete (osobna funkcja). StartKit bez zmian.
- **Kolizje**: DressedOrNot (ten sam cel - prefiks i postfiks wspolistnieja); ArmouryStatsPatch na SetItem (osobna klasa, bez wspolnego warunku
  ShowConditionPercent); kolejnosc CS przed Armoury przy wczytaniu - UniqueLaw.SweepHeroes po HeroSinew, ale zamiennik i tak w granicy umiejetnosci.
- **Spojnosc**: jedna regula (ItemReq / jej lustro w CS), jedna funkcja podnoszenia w trzech chwilach, nic z niczego (to, czego AI nie udzwignie,
  idzie do taboru albo zostaje; zadna sztuka nie powstaje).
- **Cudzy kod**: Spoils TryUpgradeSlot (sygnatura 1.0 z dekompilacji), ROT patch24 (prefiks SpawnAgent), BK podwojne ubieranie rycerza - opisane wyzej.
- **Skala**: ok. 740 lordow +50 Atletyki - zmiana w bitwie (rozdz. 4); pomiar w autotescie miara balansu krolestw z 175.

---

## 6. Plan testu

Teraz: tylko build obu DLL (kod 0) i `python tools/gen_mcm.py`. Autotest i reczne proby - gdy bedzie wolno (dzis zakaz uruchamiania gry).

**Autotest (nowa kampania 40 dob + zapis doby 362, 8 dob):**
1. Linia "Z16 przed/po" przy wczytaniu: przed - rzad wielkosci z rozdz. 4 (nowa kampania: ok. 740 / 224 / 22 lordow), po - **0** w kazdej z trzech
   umiejetnosci; gracz: Atletyka, Luk, Kusza przed = po.
2. Zaden bohater nie traci sprzetu: suma zajetych slotow 5-9 i strzal/beltow u wszystkich bohaterow AI przed i po przegladzie - rowna.
3. Dzienny przeglad: liczba podniesien na dobe; zero albo pojedyncze z nazwanym zrodlem (pelnoletnosc, rycerz BK, nowy wladca). Kazde inne - do zbadania.
4. Samotest przy wczytaniu: 500 losowych par (bohater, sztuka) - wynik CanUseItem (po postfiksie) zgodny z `ItemReq.Meets` w 500/500.
5. Kronika unikatow: zaden lord nie zaklada unikatu, ktorego nie udzwignie ("do taboru" zamiast "zaklada"); unikaty w obiegu - ta sama liczba.
6. 0 bledow Z16 w logu, czas wczytania i doby bez zauwazalnej zmiany; miara balansu krolestw (175) - bez dominacji jednego krolestwa.

**Recznie (Jeff, jedna sesja):**
1. Ekwipunek, Twoja Atletyka np. 60: plyta t6 (175) - czerwona karta, przeciagniecie odrzucone z napisem "You don't have enough Athletics skill to
   equip this item", w opisie "Requires: Athletics 175" na czerwono; helm t2 (35) - zaklada sie normalnie.
2. Strzaly t4 (105) przy Luku ponizej 105 - jak wyzej, "Bow"; belty - "Crossbow".
3. To, co juz nosisz, zostaje; zdejmij - nie zalozysz; Cancel/Reset ekranu przywraca to, co miales.
4. Towarzysz na ekranie - ta sama regula wedlug JEGO umiejetnosci; Spoils "Auto-equip companions" - nikt nie dostaje sztuki ponad umiejetnosc.
5. Stroj cywilny: suknia/szata t6 tez wymaga 175 (rozdz. 7.2) - do Twojej oceny.

---

## 7. Uwagi poboczne (poza Z16 - do wiadomosci, bez zmian w tej paczce)

7.1 **Ladry konskie liczone jako Atletyka.** `ItemReq.SkillFor` (`[A] ItemReq.cs:54`) i `Mends.ReqSkill` (`[CS] Mends.cs:1033-1042`) daja kazdej sztuce
z ArmorComponent Atletyke - takze ladrom (HorseHarness, RelevantSkill gry = null). 87 ladr ma w XML wymog 30-150 (np. `kingsguard_horse_armor` 150),
wiec zolnierze dzis potrzebuja Atletyki do ladr konia, a SkillSinew zolnierzy (`Mends.cs:2439`, sloty 0-11) podnosi im pod nie Atletyke - choc prawo
tieru pancerza zaklada Jazde (`Mends.cs:2325` "ladry konskie: Riding, nie Atletyka"). Z16 ladr nie dotyka (sito i podniesienie bohaterow bez slotu 11).
Naprawa = osobna decyzja (zmienia dobor zolnierzy).

7.2 **Glowa szlachty i stroje postaci ROT maja tier 6.** `noble_default` (glowa lordow ROT, 1 kg, pancerz glowy 125) -> tier 6 -> wymog 175; jest
w 697 zestawach lordow i sam odpowiada za ok. 135 z 740 podniesien (bez niego 605, srednio +46). Szaty Varysa, Tyriona, Baelisha i suknie Cersei,
Margaery, Melisandre maja pancerz korpusu 99-110 -> 175, suknia Daenerys 132 -> 200. To dane ROT, nie Z16 - ale przez nie gracz nie zalozy takiej
sukni bez Atletyki 175, takze w stroju cywilnym.

7.3 **Spoils podwaja sztuke w auto-ekwipunku.** `[SPO] AutoEquipPlanner.cs:154-157`: przy opcji UpdateCivilianEquipment sztuka cywilna trafia do
zestawu bojowego I cywilnego, a z taboru schodzi raz (`:148`) - jedna sztuka z niczego. Etap 3 przy podmianie taboru tego nie naprawia (osobny wpis).

7.4 **StartKit ma wlasna kopie regul** (`[A] StartKit.cs:37-56`) - dzis identyczna z ItemReq; zostaje (bez zmian "przy okazji").
