# Paczka 170 - BEE DOMKNIETE: akcje gracza BetterEconomy w nicosc i bierne zrodla z niczego zamkniete latkami Armoury

Status: WYKONANE w drzewie roboczym (`Armoury/src/BeeSeal.cs`, build kod 0, rozdz. 12) - NIEWGRANE, DO SPRAWDZENIA razem ze skryptem
`zamknij-ujscia-bee.ps1 -ListaZFundamentu`. Projekt po krytyce (8 uwag krytyka sprawdzonych w kodzie - rozdz. 11) i po recenzji wykonania (4 uwagi - rozdz. 11.2).
Baza: galaz `w-toku/170-bee-domkniecie` od 2e235ea (= Armoury c01a54ba w grze).
Decyzja Jeffa 08.10 (wiazaca, PRZEKAZANIE-2026-10-08 rozdz. 7): "jak znika to zamykamy, ma byc logiczny system ekonomii, ze wszystko
z czegos wynika". Wariant zbrojowni BEE: **(b)** - `zamknij-ujscia-bee.ps1 -ListaZFundamentu` (zbrojownia zamknieta takze graczowi).
Zrodla projektu: `tools/bee/OPIS.md`, `tools/bee/zamknij-ujscia-bee.ps1`, `docs/rozpoznanie-2026-10-08/paczka-170-wynik-1.md` (R1: wejscia
gracza) i `paczka-170-wynik-2.md` (R2: zrodla i ujscia BEE). Kazde zdanie "plik:linia" ponizej sprawdzone ponownie w dekompilacji
`scratchpad/ore-supply/be` (BEE v1.4.5, DLL md5 267ba08d - ten sam co w grze, sprawdzone 08.10 wieczorem), `ore-supply/bk`, `ore-supply/cs`
oraz w 0Harmony 2.4.2.0 z `Modules/Bannerlord.Harmony` (semantyka prefiksow, rozdz. 6).

Skroty: be/ = ore-supply/be; B/ = be/BetterEconomy.Behaviors/; Town = TownEconomyCampaignBehavior, Castle = CastleEconomyCampaignBehavior,
VInv = VillageInvestmentCampaignBehavior, VDev = VillageDevelopmentCampaignBehavior, Car = CaravanCampaignBehavior, SMB = SettlementMenuBehavior,
SAS = be/BetterEconomy.UI/SettlementActionService.cs, Bridge = bk/BannerKings.Utils/BetterEconomyBridge.cs. Typy BEE bez przestrzeni nazw
sa w `BetterEconomy.Behaviors`; stany w `BetterEconomy.Core`.

## 0. W skrocie

- **Po stronie AI i swiata** wszystko zamykaja: 13 kluczy pliku BEE (`-ListaZFundamentu`) + wpisy 18 i 49 (juz w pliku). 170 tego nie dubluje.
- **170 = nowy plik `Armoury/src/BeeSeal.cs`** (latki Harmony przez refleksje, bez BEE nic sie nie wpina) + 1 ustawienie MCM + 1 linia w
  `SubModuleMain`. 16 zaczepow (15 domykajacych + 1 tylko do logu):
  - 8 postfiksow na `CanPlayer*` (akcje gracza: `__result = false`, `reason` = powod po angielsku) - zamykaja gracza na KAZDYM wejsciu
    (menu BEE, pickery BEE, ekrany BK, konsola BK), bo kazda sciezka placaca najpierw pyta `CanPlayer*`;
  - 1 postfiks na `SMB.OnSessionLaunched` - owija warunki 12 opcji menu BEE, zeby byly WIDOCZNE i NIEAKTYWNE z podpowiedzia (sam postfiks
    `Can*` chowa 7 z nich bez slowa);
  - 1 para prefiks + postfiks na `BetterEconomy.UI.Gauntlet.LedgerVM.AddToggle` - przelacznik "Lord wealth realism" w ksiedze BEE pokazany
    jako OFF z powodem, klik nic nie wlacza;
  - 5 prefiksow `return false` na biernych zrodlach (gotowe zbrojownie, XP gotowych obozow AI, odblokowana druga produkcja wsi, oplata 5 000
    za dostep do targu, zdejmowanie zlota panom); oplate AI blokujemy tylko przy zamknietych K4/K5 (11.2 pkt 3);
  - 1 postfiks tylko do logu na `Castle.CompletePlayerTrainingSession` - dokonczenie sesji szkolenia oplaconej w starym zapisie (D9) widac
    w linii doby (11.2 pkt 1).
- Zadnych nowych kluczy zapisu; stan BEE w zapisie nie jest ani razu pisany przez nasz kod. Wylacznik MCM wylaczony = BEE jak dawniej.
- Linia logu raz na uruchomienie gry (co wpiete), raz na kampanie (menu + wylacznik + tryb zgodnosci BK w BEE + stan 19 wartosci pliku
  BEE + user.cfg) i linia dnia tylko wtedy, gdy latki biernych zrodel cos zablokowaly.
- Ekrany BK (Demesne) i konsola BK: swiadome odstepstwo od litery "opcja nieaktywna" - przyciski BK nie maja wiazania IsEnabled, wiec
  zostaja klikalne; zloto nie schodzi, powod pada po zatwierdzeniu okna (D5).

## 1. Decyzje (kazda z uzasadnieniem)

| Nr | Decyzja | Uzasadnienie (jedno zdanie) |
|---|---|---|
| D1 | Akcje gracza zamyka postfiks na `CanPlayer*`, nie prefiks na `Try*` ani na `GiveGoldAction` | Wszystkie 13 wejsc (menu, pickery, BK MilitaryVM/OverviewVM, konsola BK) pytaja najpierw `CanPlayer*` (sprawdzone grepem po be/ i bk/, rozdz. 4.2), a `Can*` niesie powod, ktory kazde z tych wejsc juz wyswietla. |
| D2 | Wszystkie 8 akcji ZAMKNIETE, takze wplaty do skarbca miasta i zamku (bez "prawdziwego platnika") | Opcja BEE opisuje i limituje WIRTUALNY skarbiec (menu i picker mowia "local treasury" / "castle treasury", SMB:898, :1198; `Can` liczy cooldown i limit pojemnosci ze stanu BEE, Town:423-445), wiec prawdziwa wplata pod ta opcja (technicznie prosta: prefiks pomijajacy oryginal na publicznym `TryPlayerContributeTreasury` - Town:449, `Can` :454, zloto :463; Castle:1135, :1140, :1149 - z `__result = true` i `GiveGoldAction.ApplyForCharacterToSettlement`) zostawilaby graczowi opis i limity cudzego, papierowego skarbca, a ustawienie cooldownu wymagaloby pisania stanu BEE (zakazane, 3.7); prawdziwa wplata do kasy miasta to tania osobna funkcja Armoury, nie przerobka BEE - jesli Jeff zechce (P1). |
| D3 | Dostep do targu (5 000) zamkniety, bez zamiany na "oplata dla notabli" | Po kluczach 4 i 5 korzysc akcji (tlumienie odplywu) jest zerowa, zostaje tylko relacja z niczego, a zamiana na przelew do notabli to nowa funkcja gry (pytanie P2), nie domkniecie. |
| D4 | Opcje menu BEE owijane podmiana publicznego pola `GameMenuOption.OnCondition` w postfiksie `SMB.OnSessionLaunched`, NIE postfiks na `GameMenuOption.GetConditionsHold` | Zero kosztu dla wszystkich innych opcji wszystkich menu (GetConditionsHold biegnie dla kazdej opcji przy kazdym odswiezeniu), brak latki na metodzie gry uzywanej przez wszystkie mody, odpornosc na wklejenie przez JIT malych lambd BEE. |
| D5 | Ekranow BK (MilitaryVM, OverviewVM, EconomyVM) i konsoli BK NIE latamy - SWIADOME ODSTEPSTWO od litery "opcja nieaktywna": przyciski BK zostaja klikalne, okna zbrojowni i obozu pokazuja cene ("Upgrade {TOWN}'s armory for {COST}g?", MilitaryVM.cs:626, OverviewVM.cs:472; oboz MilitaryVM.cs:669), wplaty i inwestycja we wies pytaja o kwote (MilitaryVM.cs:646, :714, OverviewVM.cs:445, :496), a powod "Closed: ..." pada dopiero po zatwierdzeniu; start szkolenia (MilitaryVM.cs:682) odmawia od razu | Przyciski BEE w prefabach BK nie maja wiazania IsEnabled (Modules/BannerKings.Redux/GUI/Prefabs/Settlements/Management/MilitaryPanel.xml:285-340 - tylko `Command.Click`), wiec szarosci nie da sie dac bez zmiany cudzego prefabu; zloto nie schodzi przed odmowa (4.2) i powod jest pokazany, a 7 prefiksow na `Execute*` BK (MilitaryVM :616, :639, :659, :707; OverviewVM :438, :462, :489) zamieniajacych okno z cena na sam komunikat byloby tylko kosmetyka z dodatkowa powierzchnia na aktualizacje BK (zasada 3 CLAUDE.md). Odstepstwo wpisane w CHANGELOG (3.1 pkt 5), test 7.3.4 i ryzyko 3. |
| D6 | Przelacznik "Lord wealth realism": prefiks + postfiks na `LedgerVM.AddToggle` (klucz "lordwealth") ORAZ prefiks na `WealthAuditCampaignBehavior.TryRemoveHeroGold` | Zadanie wymaga widocznej opcji z powodem; samo zamkniecie efektu zostawiloby przelacznik klamiacy "ON", a sam przelacznik nie chroni przed wyzerowanym 21-bajtowym `better_economy_user.cfg` (wartosc wbudowana `LordWealthRealism = true`, be/BetterEconomy.Config/RuntimeSettings.cs:13). |
| D7 | Bierne zrodla B1-B5: prefiks zwracajacy `false` (oryginal pominiety w calosci), z `Priority.Last` | Kazda z tych metod robi wylacznie rzecz z niczego / w nicosc (rozdz. 2, B1-B5 - przeczytane w calosci), a `Priority.Last` zostawia okna pomiaru paczki 169 nienaruszone (rozdz. 6). |
| D8 | Gotowa zbrojownia: produkcja ZATRZYMANA, bez szukania platnika | Prawdziwy platnik oznaczalby nowy warsztat (kto placi za wsad, kto bierze utarg), a w wariancie (b) nowych zbrojowni i tak nie bedzie; bron w swiecie robia kowale i warsztaty Armoury. |
| D9 | Oplacona w starym zapisie sesja szkolenia: anulowanie zostaje (bez latki na `CanPlayerCancelTraining`), dokonczenie tez zostaje | Zloto juz zniknelo i nie wroci, a odebranie skutku karaloby gracza bez logiki; to jednorazowy ogon najwyzej kilku dob (sesja postepuje tylko, gdy gracz stoi w zamku, Castle:465-469). Ogon jest widoczny w logu: postfiks tylko do logu na `CompletePlayerTrainingSession` (Castle:474 -> :879, XP :924) dopisuje do linii doby "dokonczone stare sesje szkolenia (D9, nie blokowane, XP z niczego): N, zolnierzy T, XP M" (11.2 pkt 1). |
| D10 | `BannerKingsAdapter.TryAddVillagePeasants` i `LordInvestmentCampaignBehavior.ApplyInvestment` BEZ latki | Jedyni wolajacy sa juz zamknieci wyzej (gracz: `Can*`; AI: klucz 2, a LordInvestment AI w trybie zgodnosci BK nie jest rejestrowany, BetterEconomySubModule.cs:94-97 - utrate tego trybu zglasza linia kampanii, D13) - dodatkowy bezpiecznik = kod "na wszelki wypadek" (zasada 3). |
| D11 | Wszystko pod jednym wylacznikiem MCM `LivingEconomySealed` (domyslnie wlaczony) | Jedna zasada na jedno zjawisko: "BEE nie tworzy i nie niszczy zlota, towaru, ludzi ani XP"; dwa wylaczniki pozwalalyby na stan polowiczny (np. akcje zamkniete, bierne zrodla otwarte). |
| D12 | Bramka wersji: latki tylko przy `BetterEconomy.BetterEconomySubModule.Version == "v1.4.5"` (odczyt odporny na zmiane `const` -> `static readonly`, 3.3 pkt 2); kazdy cel dodatkowo szukany z DOKLADNA lista typow parametrow | Prefiks `return false` na zmienionej metodzie innej wersji moglby blokowac cos innego; aktualizacja BEE i tak wymaga ponownego sprawdzenia kluczy (OPIS 7), a linia logu ma to powiedziec glosno takze wtedy, gdy nowa wersja zmieni rodzaj pola wersji. |
| D13 | Kontrola trybu zgodnosci BK w BEE (`BannerKingsAdapter.IsActive`), 19 wartosci pliku BEE i `LordWealthRealism` - tylko ODCZYT, jedna linia na kampanie | 170 zaklada, ze klucze sa wgrane (skrypt dziala na pliku, ktory aktualizacja Steam moze przywrocic, OPIS 7) i ze BEE jest w trybie zgodnosci BK (inaczej BEE rejestruje LordInvestment AI z zaniedbaniem, Population/Migration, WorkshopProductionPatch i swoje modele - BetterEconomySubModule.cs:72-111, WorkshopProductionPatch.cs:17); bez tej linii nikt nie zauwazy, ze AI znow wrzuca zloto w nicosc. |
| D14 | Linia dnia z licznikami WYWOLAN zablokowanych przez prefiksy B1-B5 (nie "zatrzymanej produkcji"), wypisywana leniwie przy pierwszym wywolaniu w nowej dobie i tylko gdy suma > 0 | "Nie mow, ze dziala, dopoki nie widac w logu" (CLAUDE.md 1), bez dotykania `ArmouryBehavior` (latwe scalenie z 169 i 171); licznik dowodzi, ze latka stoi na drodze - dokladne "co by powstalo" wymagaloby kopii warunkow BEE (wsad na targu, garnizon, hearth, chlopi, najazd), a brak towaru z niczego pokazuje GoodsLedger (7.2). |
| D15 | Bez nowych kluczy zapisu | Latki niczego nie pamietaja miedzy sesjami; stan BEE zostaje nietkniety, wiec wylaczenie latki przywraca BEE bez naprawiania zapisu. |

## 2. Tabela: kazde zrodlo i ujscie BEE z obu raportow - czym zamkniete

Kolumna "Czym": **K** = klucz pliku BEE (`-ListaZFundamentu`, wartosc docelowa), **W18/W49** = wpis 18 / 49 (juz w pliku), **L170** = latka
tej paczki (szczegoly kodu w rozdz. 3), **ZOSTAJE** = prawdziwy przeplyw albo stan tylko na papierze (dowod).

### 2.1 Strona AI i swiata (R2 tabela A) - zamkniete plikiem, 170 nic nie dodaje

| Nr | Miejsce | Co | Kto | Czym |
|---|---|---|---|---|
| A1 | Castle.TryAiContribute B/Castle...:343-364 (zloto :358) | zloto pana do null, wirtualny skarbiec | AI | K1 `CastleAiLeaderReserveGold = 1000000000` |
| A2 | Castle.TryAiBuildTrainingCamp :1055-1084 (:1074) | 30/60/100 tys. do null | AI | K1 |
| A3 | VInv.OnClanDailyTick / TryApplyLordInvestment / ApplyVillageEffects :311-443 (zloto :375, hearth :400, chlopi BK :403) | zloto do null, hearth i chlopi z niczego | AI | K2 `LordInvestmentReserveFlat = 1000000000` (:317 return) |
| A4 | Town.TryApplyAiArmory :2081-2145 (:2137) | doplata pana do null, zbrojownia z wirtualnego skarbca | AI | K3' `ArmoryRequiredArtisans = 1000000000` (:2098); dodatkowo K2 (:2129). `ArmoryAiCheckCooldownDays` zostaje 14 (wariant b). |
| A5 | Town.TryPayTreasurySurplus :1953-2000 (:1990) | wirtualny skarbiec -> prawdziwe zloto wlasciciela z null | swiat, takze gracz | W49 (oba ulamki wyplaty = 0) |
| A6 | Town.EnsureSnapshot / ConsumeInputs :2702-2725, :3034-3083 | wsad warsztatow z targu w nicosc | swiat | W18 (`WorkshopDailyInputDrawPerShop = 0`) |
| A7 | VDev.TryUnlockSecondaryProduction :380-394, :660-671 | NOWE odblokowania drugiej produkcji | swiat | K6 `VillageSecondaryRequiredStableDays = 1000000000` (licznik ma sufit 10 000, :369). Juz odblokowane -> B3. |
| A8 | VDev.TickSecondaryProduction - kopia 45% dla miasta :438-446 | towar z niczego na targ miasta | swiat | W18 (`VillageSecondaryBoundTownShare = 0`); cala metoda i tak -> B3 |
| A9 | VDev.TickGrievanceAndDiversion / ApplyDiversionStock :458-573 (:551) | towar z niczego na targ innego miasta | swiat | K4 `VillageDiversionRelationThreshold = -101` + K5 `VillageDiversionGrievanceThreshold = 101` |
| A10 | Car.TryDeliverContract :640-650, :924-928 (+ procurement Town :2626, Castle :1448, eksport zbrojowni Town :653) | towar bez zaplaty, marza 12% z niczego | karawany | K7 `CaravanDeliveryMinGold = 1000000000` |
| A11 | Car.TryHireEscort :1001-1046 | zloto w nicosc, zbrojni z niczego | karawany | K8 `CaravanEscortHireMinGold = 1000000000` |
| A12 | Car.TryPromoteRoster :1069-1174 | zloto w nicosc, XP z niczego | karawany | K9 `CaravanRecruitPromotionEnabled = 0` |
| A13 | Car.ApplyDangerPressure :1366-1393 | do 7.6% kiesy dziennie w nicosc | karawany | K10 `RouteDangerMaxLossRatio = 0` |
| A14 | TradeAgreement.AccrueCustoms :400-422 | clo z null dla krolow | krolowie | K11 `TradeAgreementCustomsMin = 0` + W49 (`TradeAgreementCustomsRate = 0`) |
| A15 | TradeAgreement.ApplyEndpointProsperity :836-858 | dobrobyt z niczego | miasta korytarzy | K12 `TradeAgreementCorridorProsperityPerDay = 0` |
| A16 | VillageSupply.ApplyRaidImpact :121-124 -> BannerKingsAdapter.TryApplyRaidFlight :138-160 | 8% chlopow BK w nicosc | swiat | K13 `RaidPeasantFlightFraction = 0` |
| A17 | VDev.TickAiMarketAccess :575-613 -> ApplyMarketAccess :615-636 | 5 000 do null, relacje z niczego | AI | K4+K5 (placi tylko przy `DiversionFraction >= 0.1`, :599); **ogon** starego zapisu -> B4. Bez K4/K5 B4 oplaty AI NIE blokuje (przepuszcza oryginal): wtedy tylko ta oplata tlumi odplyw towaru z niczego (DiversionSuppressedUntilDay :628 -> :484-489), a zablokowana przedluzylaby A9 (11.2 pkt 3) |
| A18 | Feudal.TickEstateRent / PayEstateOwner :894-973 | renta z null | swiat | nieczynne przy BK (`IsEnabled = !HasBannerKings`, :39) + W49 (`EstateOwnerPayoutFraction = 0`) |
| A19 | WealthAudit.OnDailyTick -> TryRemoveHeroGold :72-80, :197, :338-353 | zloto panow do null | AI | dzis tylko user.cfg `LordWealthRealism=0`; -> B5 + L170 przelacznik |

### 2.2 Akcje gracza (R1 pkt 1-9, R2 G1-G8) - L170, postfiks `CanPlayer*`

Wszystkie postfiksy: `static void X(ref bool __result, ref string reason)` (parametr `out string reason` oryginalu Harmony podaje jako `ref`
po nazwie - nazwa "reason" jest w kazdym z 8 celow), `Priority.Last`, cialo: `if (!On) return; __result = false; reason = TEKST;`.
Postfiks ustawia false takze wtedy, gdy oryginal juz odmowil (np. brak wlasnosci) - powod "Closed" jest wtedy prawdziwy i prostszy.

| Nr | Akcja (ujscie) | Cel latki - pelna nazwa, sygnatura | Tekst | Wejscia, ktore przez niego przechodza |
|---|---|---|---|---|
| G1 | Wplata do skarbca ZAMKU (zloto do null Castle:1149) | `BetterEconomy.Behaviors.CastleEconomyCampaignBehavior.CanPlayerContributeTreasury(Settlement castle, Hero playerHero, int amount, out string reason) : bool` (Castle:1086) | T_TREASURY | menu `bee_castle_treasury_contribute` (SMB:1198) -> SAS:357 picker / SAS:392 Try (Castle:1135, Can :1140); BK Bridge:1071 <- MilitaryVM.ExecuteContributeCastleTreasury :720 |
| G2 | Wplata do skarbca MIASTA (Town:463) | `BetterEconomy.Behaviors.TownEconomyCampaignBehavior.CanPlayerContributeTreasury(Settlement settlement, Hero playerHero, int amount, out string reason) : bool` (Town:390) | T_TREASURY | menu `bee_town_treasury_contribute` (SMB:898) -> SAS:204 / SAS:237 Try (Town:449, Can :454); BK Bridge:513 <- MilitaryVM :652, OverviewVM :454, konsola `bannerkings.be_contribute_treasury` (BannerKingsCheats.cs:687) |
| G3 | Zbrojownia (Town:577, potem B1) | `...TownEconomyCampaignBehavior.CanPlayerBuildOrUpgradeArmory(Settlement settlement, Hero playerHero, out string reason) : bool` (Town:497) | T_ARMORY | menu `bee_town_armory` (SMB:922) -> SAS:262 / :287 Try (Town:568, Can :571); BK Bridge:541 <- MilitaryVM :632, OverviewVM :481, konsola `be_upgrade_armory` (:716). Klucz 3' zamyka juz sam (powod "Not enough artisans. Need 1000000000, have N."); postfiks daje czytelny powod i niezaleznosc od pliku. |
| G4 | Inwestycja w MIASTO 10/50/100 tys. (LordInvestment:140; dobrobyt :157, zywnosc :158, towar :257, relacje SAS:678 - z niczego) | `...TownEconomyCampaignBehavior.CanPlayerInvest(Settlement settlement, Hero playerHero, int amount, out string reason) : bool` (Town:853) - TYLKO przeciazenie 4-argumentowe; 3-argumentowe (:848) deleguje do niego | T_TOWN_INVEST | menu `bee_town_invest_10k/50k/100k` (SMB:964/982/1000, warunek OnTownInvestmentCondition SMB:1320-1327 = wynik Can -> bez D4 opcja ZNIKA); SAS.TryExecuteTownInvestment :633-680 (Can przed ApplyInvestment :664). BK: brak wejscia. |
| G5 | Inwestycja we WIES 5/15/30 tys. (VInv:274; hearth :400, chlopi BK :403, relacje :432 - z niczego) | `BetterEconomy.Behaviors.VillageInvestmentCampaignBehavior.CanPlayerInvest(Settlement villageSettlement, Hero playerHero, int amount, out string reason) : bool` (VInv:98) | T_VILLAGE_INVEST | menu `bee_village_invest_5k/15k/30k` (SMB:1090/1108/1126, warunek SMB:1329-1335 = wynik Can -> ZNIKA bez D4); SAS:700 -> TryApplyPlayerInvestment (VInv:255, Can :258); BK Bridge.TryVillagePlayerInvest :1082-1110 (refleksja, `reason` wraca w ostatnim elemencie tablicy) <- OverviewVM.ExecuteInvestVillage :505, konsola `be_village_invest` (:748). Bridge.CanVillagePlayerInvest :573-590 - bez wywolan w BK. |
| G6 | Dostep do targu 5 000 (VDev:625, relacje :650) | `BetterEconomy.Behaviors.VillageDevelopmentCampaignBehavior.CanPlayerNegotiateMarketAccess(Settlement villageSettlement, Hero playerHero, out string reason) : bool` (VDev:230) | T_MARKET | menu `bee_village_market_access` (SMB:1072, warunek lambda SMB:351-357 = wynik Can -> ZNIKA bez D4); SAS:596 / :622 -> TryPlayerNegotiateMarketAccess (VDev:273, Can :275). BK: brak. |
| G7 | Oboz szkoleniowy - budowa/rozbudowa (Castle:614) | `...CastleEconomyCampaignBehavior.CanPlayerBuildOrUpgradeTrainingCamp(Settlement castle, Hero playerHero, out string reason) : bool` (Castle:547) | T_CAMP | menu `bee_castle_training_camp` (SMB:1246) -> SAS:467 / :490 Try (Castle:600, Can :604); BK Bridge:987 <- MilitaryVM.ExecuteUpgradeTrainingCamp :675 |
| G8 | Szkolenie wojsk (Castle:718; XP z niczego po zakonczeniu, :924) | `...CastleEconomyCampaignBehavior.CanPlayerStartTraining(Settlement castle, Hero playerHero, out string reason) : bool` (Castle:626) | T_TRAIN | menu `bee_castle_train_troops` (SMB:1270) -> SAS:515 / :544 Try (Castle:696, Can :700); BK Bridge:1015 <- MilitaryVM.ExecuteStartOrCancelTraining :700 (galaz bez aktywnej sesji) |
| R1-9 | Przelacznik "Lord wealth realism" w ksiedze BEE (LedgerVM.cs:1720-1724) -> WealthAudit w nicosc | `BetterEconomy.UI.Gauntlet.LedgerVM.AddToggle(string key, string name, string hint, bool current, Action<bool> apply, string rowColor) : void` (prywatna, LedgerVM.cs:1792) - prefiks + postfiks, tylko dla `key == "lordwealth"` | T_WEALTH (podpowiedz w wierszu), T_WEALTH_CLICK (komunikat po kliknieciu) | ksiega BEE, zakladka ustawien (BuildSettings, LedgerVM.cs:1709). Efekt i tak zamyka B5. |

### 2.3 Bierne zrodla po 13 kluczach (R2 B1-B5) - L170, prefiks `return false`

Wszystkie: `Priority.Last`; gdy wylacznik wylaczony - `return true` (oryginal biegnie) bez liczenia; gdy wlaczony - licznik, `return false`.
Parametry typow BEE w prefiksie deklarowac jako `object` po nazwie (Harmony przyjmuje `object` dla parametru typu referencyjnego).

| Nr | Cel - pelna nazwa, sygnatura (dokladne typy do `AccessTools.Method`) | Co robi (przeczytane w calosci) | Kto | Prefiks i licznik |
|---|---|---|---|---|
| B1 | `TownEconomyCampaignBehavior.TickArmoryProduction(Settlement settlement, BetterEconomy.Core.TownEconomyState state, int today) : void`, prywatna (Town:2152-2266, wolana z :1696) | gotowa zbrojownia: zelazo, drewno, skora, narzedzia z targu miasta w nicosc (:2220, nikt nie dostaje zaplaty) i bron z niczego na targ (:2254); bez warunku wlasciciela | swiat, AI i gracz (stare zapisy) | `static bool ArmoryPre(object state)`: gdy `state != null` i pole `ArmoryLevel` (public int, Core/TownEconomyState.cs:128) > 0 -> `_armory++`; `return false`. Oryginal i tak wychodzi przy poziomie 0 (:2154), wiec licznik = WYWOLANIA zablokowane w miastach z gotowa zbrojownia - nie kazde by cos wytworzylo (oryginal wychodzi bez skutku przy braku wsadu na targu :2190-2197 albo gdy nic nie zuzyl :2225-2227). |
| B2 | `CastleEconomyCampaignBehavior.ApplyAiTrainingCampPassive(Settlement castle, BetterEconomy.Core.CastleEconomyState state) : void`, prywatna (Castle:486-545, wolana z TickTraining :457-460 tylko dla zamkow spoza rodu gracza z `TrainingCampLevel > 0`) | 1-3 XP na zdrowego zolnierza tieru < progu na tick, z niczego (`AddXpToTroop` :533) | AI | `static bool CampPre()`: `_camps++`; `return false`. Licznik = WYWOLANIA zablokowane w zamkach AI z gotowym obozem (takze z pustym garnizonem albo zerowym XP poziomu - oryginal wychodzi wtedy bez skutku, Castle:512, :517). |
| B3 | `VillageDevelopmentCampaignBehavior.TickSecondaryProduction(Settlement settlement, BetterEconomy.Core.VillageDevelopmentState state, BetterEconomy.Core.SettlementPopulation pop, BetterEconomy.Core.VillageSupplyLink link, int today) : void`, prywatna (VDev:396-456, wolana z OnSettlementDailyTick :348) | do 8 szt. na tick z niczego do skladu wsi (:418), + kopia dla miasta (:438-446, juz 0 przez W18); poza tym tylko `LastSecondaryProductionDay` i licznik dnia | swiat, takze wsie gracza | `static bool SecondPre(object state)`: gdy pole `SecondaryItemId` (public string, Core/VillageDevelopmentState.cs:8) niepuste -> `_second++`; `return false`. Licznik = WYWOLANIA zablokowane we wsiach z odblokowana druga produkcja - takze tych, w ktorych oryginal nic by nie zrobil (`!IsSecondaryActive`, VDev:398: zawieszenie >= grace :679, hearth < 85% / chlopi < 75% progu :684, najazd :686). Bez dodatkowych warunkow (D14). |
| B4 | `VillageDevelopmentCampaignBehavior.ApplyMarketAccess(Settlement settlement, Hero payer, bool player) : void`, prywatna (VDev:615-636) | 5 000 od platnika do null (:625), zal -, odplyw 0, relacje z niczego (:650) | AI (ogon A17), gracz (tylko gdyby G6 nie stal) | `static bool MarketPre(bool player)`: gdy `!player` i NIE (K4 `VillageDiversionRelationThreshold <= -101` i K5 `VillageDiversionGrievanceThreshold >= 101`, odczyt pol przy kazdym wywolaniu - rzadkie) -> `_marketOpen++`, `return true` (oryginal biegnie, 11.2 pkt 3); inaczej `_market++`, gdy `player` -> `_marketPlayer++`; `return false`. Wolajacy AI i tak ustawia `LastAiMarketAccessDay` (:610) - brak petli prob co tick. |
| B5 | `WealthAuditCampaignBehavior.TryRemoveHeroGold(Hero hero, int amount) : bool`, prywatna statyczna (WealthAudit:338-353) | `GiveGoldAction(pan, null, amount)` (:346) - zloto panow AI w nicosc | AI | `static bool WealthPre(ref bool __result, int amount)`: `_wealthN++; _wealthGold += amount; __result = false; return false;` Wolajacy liczy "affected" tylko przy true (:197), wiec log BEE pokaze 0. Metoda ma try/catch - JIT jej nie wkleja. |

### 2.4 Zostaje bez zmian (prawdziwy przeplyw, stan tylko na papierze albo nieczynne) - dowody

| Co | Dlaczego zostaje (plik:linia) |
|---|---|
| Wirtualny skarbiec zamku z hearth (Castle:119-128, :142-155) i miasta z podatku BK (Town:1858-1878) | Wydaja go tylko projekty (skutki wirtualne: gotowosc, patrole, mnozniki - Castle:289-322, Town:2002-2047), procurement (K7) i wyplata (W49 = 0); brak drogi do prawdziwego zlota, towaru, ludzi (R2 C, OPIS 6 "Regresje"). |
| Projekty miasta i zamku gracza (`CanPlayerStartProject` Town:936 / Castle:1160; BK Bridge:846 -> EconomyVM:796) | Placi wirtualny skarbiec, nie kiesa gracza (R1 D). |
| Polityka miasta i podatki (`TrySetPolicy` Town:895, `CanPlayerSetTaxPolicy` Town:777; BK EconomyVM:703, :744) | Tylko stan wirtualny (R1 D). |
| `CanPlayerCancelTraining` (Castle:734) i wejscia anulowania (SMB:1294 `bee_castle_cancel_training`, SAS:565/575, Bridge:1043 <- MilitaryVM:695) | Nie rusza zlota; pozwala zakonczyc sesje oplacona w starym zapisie (D9). |
| Majatki (FeudalEconomy.CanApplyEstateAction :516) | Nieczynne przy BK (`IsEnabled = !HasBannerKings`, :39, :521). |
| Wyplaty skarbcow wlascicielom | W49: `TownTreasurySurplusPayoutFraction = 0`, `TownTreasuryWarPayoutFraction = 0`, `EstateOwnerPayoutFraction = 0`. |
| Ksiega BEE poza przelacznikiem (LedgerVM/RowVM/TabVM: ExecuteClose, ExecuteSelect -> SettlementDetailView.Show, ExecuteSort) | Same odczyty (R1 D). |
| Umowa handlowa przez barter (TradeAgreementBarterPatch, TradeAgreementBarterable.Apply) | Nie rusza zlota; clo zamyka K11 (R1 D). |
| Juz najeta eskorta karawan (Car:424-467) | Prawdziwi ludzie, kod tylko liczy (R2 C). |
| RosterSanitizer :171/:181, PartyTrainingPatch | Techniczna naprawa uszkodzonych wpisow armii (R2 C). |
| Pobor przy werbunku (`RecruitmentPatch.ApplyRecruitmentDrain`, RecruitmentPatch.cs:125-130) | Nieczynny ZAWSZE przy obecnym BK: warunek `ModCompatibility.HasBannerKings` (:127, sama obecnosc BK - ModCompatibility.cs:173), a nie `BannerKingsAdapter.IsActive`, wiec niezaleznie od trybu zgodnosci i kolejnosci ladowania. |
| WorkshopProductionPatch (WorkshopProductionPatch.cs:17), Population / Migration / LordInvestment AI (z `ApplyNeglectDecay`, LordInvestmentCampaignBehavior.cs:92, :192-219) / RecruitmentAudit, modele BEE (prosperity, produkcja wsi, ceny) | Nieczynne TYLKO w trybie zgodnosci BK: `BannerKingsAdapter.IsActive` = `HasBannerKings && BannerKingsCompatibilityMode != 0` (BannerKingsAdapter.cs:49-59), sprawdzane w BetterEconomySubModule.cs:72-111 i w kazdym wywolaniu WorkshopProductionPatch (:17). BK zeruje ten klucz w swoim OnSubModuleLoad (bk/BannerKings/Main.cs:331), a BEE przywraca 1 z XML (better_economy_settings.xml:23) tylko, gdy laduje sie PO BK - pilnuje tego linia kampanii (3.6, "tryb zgodnosci BK"). |
| Modele PartyWage, PartySizeLimit, MobilePartyFoodConsumption, PartySpeed; EconomicEvent, CulturalMarket (konwersja kultury wylaczona), RouteDanger, SettlementIntel, Stats | Opakowania modeli gry, mnozniki i odczyty - nie tworza zrodel (R2 C). |
| `LordInvestmentCampaignBehavior.ApplyInvestment` (:128) | Jedyny zywy wolajacy to gracz po `Can` (SAS:664); AI :118 nierejestrowane w trybie BK (D10). |
| `BannerKingsAdapter.TryAddVillagePeasants` (:114) | Jedyny wolajacy ApplyVillageEffects :403: gracz zamkniety G5, AI zamkniety K2 (D10). |
| `TickTrainingCampConstruction` (Castle:420-446) | Konczy budowe obozu ze starego zapisu - tylko poziom, bez zlota i XP; XP z gotowego obozu AI zamyka B2. |
| Inne miejsca BK wolajace BEE (DemesneVM, EstateVM, ClanIncomeEstateVM, BKEstatesModel, BKManagerBehavior, PopulationData, Estate, EstateData, BKEconomyLayerInstaller) | Odczyty i synchronizacja, zaden nie wola akcji placacych (R1 D). |
| Dialogi i komendy konsoli BEE | BEE ich nie ma (zero AddPlayerLine/AddDialogLine, R1 D). |

Kontrola pelnosci (sprawdzone 08.10 wieczorem): w calym Modules tylko `BetterEconomy.dll` i `BannerKings.dll` zawieraja nazwy celow 170
(`grep -a` po */bin/Win64_Shipping_Client/*.dll); BK nie lata zadnego z nich (grep bk/ po nazwach: tylko wywolania w Bridge).

## 3. Kod - co dokladnie napisac

### 3.1 Pliki (zwarte bloki - scalanie 169 -> 170 -> 171)

1. **NOWY `Armoury/src/BeeSeal.cs`**: `internal static class BeeSeal` + zagniezdzona `private sealed class MenuGate`. Komentarz naglowkowy po
   polsku bez polskich znakow (decyzja Jeffa, lista 15 zaczepow, odwolanie do tej specyfikacji), wzor: `SpoilsSeal.cs`.
2. **`Armoury/src/Settings.cs`**: blok wstawiony BEZPOSREDNIO PRZED linia `        // --- Plague shield ---` (po `SpoilsQuartermasterRepair`):
   ```
           // --- The living economy ---
           public bool LivingEconomySealed = true;            // BetterEconomy (Living Economy) may not make gold vanish nor conjure goods, men or experience out of nothing: its actions that would pay your gold into nothing (contributions to a town or castle treasury, town and village investments, market access, armory, training camp, paid drill) stay in its menus but are closed and say why, and the Lord wealth realism switch in its ledger stays off; finished armories stop turning market iron into weapons from nothing, AI training camps stop handing out free experience, villages stop their second production from nothing, and the 5000 market-access fee and the taking of lords' gold stop (off = BetterEconomy as before; the log shows what was stopped)
   ```
   Naglowek jest potrzebny, bo `gen_mcm.py` przypisuje grupe wedlug ostatniego naglowka; nastepny naglowek "Plague shield" przywraca grupe
   kolejnym polom. Napis bez cudzyslowow (generator zamienia `"` na `'`). Grupa w MCM: "The living economy".
3. **`Armoury/src/McmSettings.cs`**: NIE edytowac recznie - `python tools/gen_mcm.py` z korzenia drzewa (przy scalaniu z 169/171: rozwiazac
   konflikt w Settings.cs, potem wygenerowac McmSettings.cs od nowa, zamiast scalac go recznie).
4. **`Armoury/src/SubModuleMain.cs`**: JEDNA linia w `OnBeforeInitialModuleScreenSetAsRoot`, bezposrednio po `SpoilsCompany.ApplyAll(_harmony);`:
   ```
                   try { BeeSeal.ApplyAll(_harmony); } catch (Exception e) { Log.Error("BeeSeal.ApplyAll", e); }   // paczka 170: BetterEconomy bez zlota w nicosc i bez towaru, ludzi, XP z niczego (akcje gracza zamkniete z powodem, bierne zrodla zatrzymane)
   ```
   (po MoneyLedger.ApplyAll - wazne dla okien 169, rozdz. 6; przed GoodsLedger, ktory musi zostac na koncu.)
5. **`CHANGELOG.md`**: wpis na gorze wg wzoru (Problem / Przyczyna / Zmiana / Ryzyko - co sprawdzic / Status: DO SPRAWDZENIA), jeden zwarty blok.
   W "Ryzyko" obowiazkowo zdanie o swiadomym odstepstwie (D5): "Ekrany BK (Demesne: Military, Overview) i konsola BK nie sa latane -
   przyciski zostaja klikalne, okno pyta o cene albo kwote, a odmowa z powodem 'Closed: ...' pada po zatwierdzeniu; zloto nie schodzi."
   oraz zdanie o linii kampanii: "tryb zgodnosci BK: NIE = kolejnosc ladowania BK/BEE odwrocona - wiele sciezek BEE otwartych mimo kluczy".
6. **NIE ruszac**: `ArmouryBehavior.cs` (Reset i linia dnia sa w BeeSeal, rozdz. 3.6), SaveDefiner, SaveText (brak kluczy zapisu).

### 3.2 Stale tekstow (po angielsku; krotkie, bo w pickerach BEE trafiaja do tytulu "Contribute 10000g - {STATUS}")

```
T_TREASURY       = "Closed: gold paid into this treasury would simply vanish - in this economy gold must go to someone."
T_ARMORY         = "Closed: this armory would make weapons out of nothing - arms come from smiths and workshops."
T_TOWN_INVEST    = "Closed: this gold would vanish and conjure prosperity and goods out of nothing - invest through the town's buildings instead."
T_VILLAGE_INVEST = "Closed: this gold would vanish and conjure homes and peasants out of nothing - in this economy gold must go to someone."
T_MARKET         = "Closed: the brokers' fee would simply vanish - in this economy gold must go to someone."
T_CAMP           = "Closed: the camp's cost would simply vanish - in this economy gold must go to someone."
T_TRAIN          = "Closed: paid drill would burn gold and make experience out of nothing - troops learn by fighting."
T_WEALTH         = "Closed: gold taken from lords would simply vanish - in this economy a lord's purse changes only by real payments."
T_WEALTH_CLICK   = "Lord wealth realism is closed: gold taken from lords would simply vanish."
```
`string` dla `reason`; `TextObject` dla podpowiedzi menu tworzyc RAZ przy owijaniu (rozdz. 3.4), nie w inicjalizatorze statycznym i nie
przy kazdym wywolaniu warunku. Teksty bez nawiasow klamrowych (TextObject).

### 3.3 Wpiecie (`BeeSeal.ApplyAll(Harmony h)`, raz na uruchomienie gry)

1. `_wired.Clear(); _missing.Clear();` Typy przez `QuartermasterLaw.FindType(pelnaNazwa)` (petla po zestawach; to samo co AccessTools.TypeByName,
   wzor SpoilsSeal). `tSub = FindType("BetterEconomy.BetterEconomySubModule")`; gdy null -> linia "BetterEconomy nieobecny - nie ma czego
   domykac" i return.
2. Bramka wersji (D12), odczyt we WLASNYM try/catch (dzis `public const string Version`, BetterEconomySubModule.cs:22; `GetRawConstantValue()`
   rzuca wyjatek, gdy pole nie jest stala - a bramka jest wlasnie na aktualizacje):
   ```
   string ver = null;
   try
   {
       var f = tSub.GetField("Version", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
       object v = f == null ? null : (f.IsLiteral ? f.GetRawConstantValue() : f.GetValue(null));
       ver = v as string;
   }
   catch { ver = null; }
   ```
   Gdy `ver != "v1.4.5"` (takze null: brak pola, nie-string, wyjatek) -> linia
   `BEE domkniecie (170): BetterEconomy w wersji <ver albo "nieczytelna"> (latki pisane pod v1.4.5) - NIC nie wpiete; akcje gracza i bierne zrodla BEE OTWARTE - sprawdzic dekompilacje i klucze pliku (tools/bee/OPIS.md rozdz. 7)`
   i return.
3. Pomocnik `Wire(h, Type t, string method, Type[] args, string prefix, string postfix, string label)`:
   - `m = t != null ? AccessTools.Method(t, method, args) : null`; gdy null -> `_missing.Add(label + " (brak " + nazwa + ")")`, return;
   - `h.Patch(m, prefix: prefix != null ? new HarmonyMethod(typeof(BeeSeal), prefix) { priority = Priority.Last } : null, postfix: ... analogicznie)`;
     dla SMB.OnSessionLaunched i LedgerVM.AddToggle priorytet domyslny (bez `priority`);
   - wyjatek przy `h.Patch` -> `_missing.Add(label + " (" + e.Message + ")")`, `Log.Error("BeeSeal.Wire " + label, e)`, dalej nastepny cel;
   - sukces -> `_wired.Add(label)`.
4. Tablice typow (dokladne - sygnatury sprawdzone w be/, rozdz. 2):
   - `S = typeof(Settlement)`, `H = typeof(Hero)`, `I = typeof(int)`, `RS = typeof(string).MakeByRefType()`, `B = typeof(bool)`;
   - `tTownState = FindType("BetterEconomy.Core.TownEconomyState")`, `tCastleState = ...CastleEconomyState`, `tVilState = ...VillageDevelopmentState`,
     `tPop = ...SettlementPopulation`, `tLink = ...VillageSupplyLink` (gdy ktorys null -> odpowiedni cel do BRAK, bez wyjatku);
   - `tStarter = typeof(CampaignGameStarter)`.
5. 16 zaczepow (etykieta w logu po polsku; 15 domykajacych + 1 tylko do logu):

| Etykieta | Typ | Metoda, args | Rodzaj | Metoda latki |
|---|---|---|---|---|
| "wplata do skarbca zamku" | Castle | CanPlayerContributeTreasury, {S,H,I,RS} | postfiks | `TreasuryPost` |
| "wplata do skarbca miasta" | Town | CanPlayerContributeTreasury, {S,H,I,RS} | postfiks | `TreasuryPost` |
| "zbrojownia gracza" | Town | CanPlayerBuildOrUpgradeArmory, {S,H,RS} | postfiks | `ArmoryPost` |
| "inwestycja w miasto" | Town | CanPlayerInvest, {S,H,I,RS} | postfiks | `TownInvestPost` |
| "inwestycja we wies" | VInv | CanPlayerInvest, {S,H,I,RS} | postfiks | `VillageInvestPost` |
| "dostep do targu (gracz)" | VDev | CanPlayerNegotiateMarketAccess, {S,H,RS} | postfiks | `MarketPost` |
| "oboz szkoleniowy" | Castle | CanPlayerBuildOrUpgradeTrainingCamp, {S,H,RS} | postfiks | `CampPost` |
| "szkolenie wojsk" | Castle | CanPlayerStartTraining, {S,H,RS} | postfiks | `TrainPost` |
| "menu BEE (12 opcji)" | SMB | OnSessionLaunched, {tStarter} | postfiks | `SessionPost` |
| "przelacznik Lord wealth realism" | `BetterEconomy.UI.Gauntlet.LedgerVM` | AddToggle, {string,string,string,bool,typeof(Action<bool>),string} | prefiks + postfiks | `TogglePre`, `TogglePost` |
| "produkcja gotowych zbrojowni" | Town | TickArmoryProduction, {S,tTownState,I} | prefiks | `ArmoryPre` |
| "XP gotowych obozow AI" | Castle | ApplyAiTrainingCampPassive, {S,tCastleState} | prefiks | `CampPre` |
| "druga produkcja wsi" | VDev | TickSecondaryProduction, {S,tVilState,tPop,tLink,I} | prefiks | `SecondPre` |
| "oplata za dostep do targu" | VDev | ApplyMarketAccess, {S,H,B} | prefiks | `MarketPre` |
| "zdejmowanie zlota panom" | `BetterEconomy.Behaviors.WealthAuditCampaignBehavior` | TryRemoveHeroGold, {H,I} | prefiks | `WealthPre` |
| "dokonczenie starej sesji szkolenia (tylko log)" | Castle | CompletePlayerTrainingSession, {S,tCastleState,RI,RI,RI,RI} (`RI = typeof(int).MakeByRefType()`, parametry `out int trained, wounded, xpTotal, retinues`) | postfiks (priorytet domyslny) | `TrainDonePost(ref int trained, ref int xpTotal)` |

6. Pola pomocnicze (raz, w ApplyAll): `_fArmoryLevel = AccessTools.Field(tTownState, "ArmoryLevel")`, `_fSecondItem = AccessTools.Field(tVilState,
   "SecondaryItemId")`, `_fAllRows = AccessTools.Field(tLedgerVM = FindType("BetterEconomy.UI.Gauntlet.LedgerVM"), "_allRows")`, `_fOnClick = AccessTools.Field(FindType("BetterEconomy.UI.Gauntlet.RowVM"),
   "OnClick")`, pola kluczy w `tSettings = FindType("BetterEconomy.Config.BetterEconomySettings")` (rozdz. 3.6) i `BetterEconomy.Config.RuntimeSettings` (`Instance`
   - wlasciwosc statyczna, `LordWealthRealism` - pole), tryb zgodnosci BK: `_pBkActive = AccessTools.Property(FindType(
   "BetterEconomy.Compatibility.BannerKingsAdapter"), "IsActive")` (publiczna wlasciwosc statyczna w klasie internal, BannerKingsAdapter.cs:49;
   getter tylko czyta `ModCompatibility.HasBannerKings` i pole `BannerKingsCompatibilityMode` - bez skutkow ubocznych) oraz `_fBkMode =
   AccessTools.Field(tSettings, "BannerKingsCompatibilityMode")` (public static int, BetterEconomySettings.cs:23 - tylko do nawiasu w linii
   kampanii). Brak ktoregos pola NIE
   blokuje latki - wtedy tylko licznik albo raport mowi "?".
7. Linia startowa (raz na uruchomienie gry, do glownego logu; BEZ stanu wylacznika - ta linia powstaje przed pierwszym `McmSettings.Apply`
   w OnGameStart, SubModuleMain.cs:205, wiec pokazywalaby wartosc z Armoury.settings.xml, nie z MCM; stan wylacznika ma linia kampanii):
   `BEE domkniecie (170): BetterEconomy v1.4.5 - wpiete N/16: <etykiety> | BRAK: <...> (te sciezki BEE BEZ ZMIAN); stan wylacznika i trybu zgodnosci BK - w linii "BEE domkniecie (170): kampania" (zmiana wylacznika w MCM dziala w ciagu godziny gry); linie dnia "BEE domkniecie (170) doba".`

### 3.4 Menu BEE: opcja widoczna i nieaktywna z powodem (`SessionPost`)

`static void SessionPost()` - postfiks `SMB.OnSessionLaunched(CampaignGameStarter)` (biegnie raz na start / wczytanie kampanii, PO dodaniu
opcji: `CampaignGameStarter.AddGameMenuOption` od razu wpisuje opcje do menedzera, cs/CampaignGameStarter.cs:93-108). Cialo w try/catch:

1. `Reset()` licznikow (rozdz. 3.6) - nowy zapis albo nowa kampania zaczyna od zera.
2. `mgr = Campaign.Current?.GameMenuManager`; dla kazdej pozycji tabeli ponizej: `menu = mgr.GetGameMenu(menuId)` (publiczne, null gdy brak,
   cs/GameMenuManager.cs:534); w `menu.MenuOptions` znalezc opcje o `IdString == optionId`; gdy `opt.OnCondition?.Target is MenuGate` - pominac
   (ochrona przed podwojnym owinieciem); inaczej `opt.OnCondition = new GameMenuOption.OnConditionDelegate(new MenuGate(opt.OnCondition, kind,
   new TextObject(tekst), optionId).Condition)` (pole publiczne, cs/GameMenuOption.cs:87). Policzyc owiniete / brakujace.

| menuId | optionId | kind (regula widocznosci przy zamknieciu) | Tekst |
|---|---|---|---|
| bee_town_menu | bee_town_treasury_contribute | 0 = jak oryginal (miasto rodu gracza) | T_TREASURY |
| bee_town_menu | bee_town_armory | 0 | T_ARMORY |
| bee_town_menu | bee_town_invest_10k / _50k / _100k | 1 = `s.IsTown && s.OwnerClan == Clan.PlayerClan` | T_TOWN_INVEST |
| bee_village_menu | bee_village_market_access | 3 = `s.IsVillage && (s.OwnerClan == Clan.PlayerClan \|\| s.Village?.Bound?.OwnerClan == Clan.PlayerClan)` (= BEE IsOwnerEligible, VDev:937-957) | T_MARKET |
| bee_village_menu | bee_village_invest_5k / _15k / _30k | 2 = `s.IsVillage && !(Clan.PlayerClan?.MapFaction != null && s.MapFaction != null && s.MapFaction.IsAtWarWith(Clan.PlayerClan.MapFaction))` (= BEE IsHostileToPlayer, VInv:564-574) | T_VILLAGE_INVEST |
| bee_castle_menu | bee_castle_treasury_contribute | 0 | T_TREASURY |
| bee_castle_menu | bee_castle_training_camp | 0 | T_CAMP |
| bee_castle_menu | bee_castle_train_troops | 0 | T_TRAIN |

NIE owijac: `bee_castle_cancel_training` (D9), `bee_*_root`, `_view`, `_policy`, `_tax_policy`, `_project`, `_estates`, `_back`.
(`s = Settlement.CurrentSettlement`.) Reguly 1-3 sa potrzebne, bo przy wlaczonej latce oryginalny warunek tych opcji zwraca wynik `Can*`,
czyli zawsze false (SMB:1320-1335, :351-357).

`MenuGate.Condition(MenuCallbackArgs args)`:
```
bool vis = false;
try { vis = _orig != null && _orig(args); }            // oryginal ustawia optionLeaveType (18 = Manage) - zostaje
catch (Exception e) { Stumble("menu " + _id, e); vis = false; }
if (!On) return vis;                                    // wylacznik wylaczony: dokladnie BEE
try
{
    if (!vis) vis = Visible(_kind);                     // kind 0: zostaje wynik oryginalu
    if (vis) { args.IsEnabled = false; args.Tooltip = _why; }
}
catch (Exception e) { Stumble("menu " + _id, e); }
return vis;
```
`MenuCallbackArgs.IsEnabled` i `.Tooltip` to pola publiczne (cs/MenuCallbackArgs.cs:10,14); `GameMenuOption.GetConditionsHold` kopiuje je do
opcji (cs/GameMenuOption.cs:127-139). Koszt: jedna sesja = jedno owiniecie 12 opcji; warunek biegnie tylko przy odswiezeniu menu BEE.

Linia kampanii (raz, na koncu SessionPost, rozdz. 3.6).

### 3.5 Ksiega BEE: przelacznik "Lord wealth realism"

- Oba zaczepy dzialaja tylko, gdy B5 jest wpiety (`_wealthWired`, ustawiane w ApplyAll po `Wire` B5; 11.2 pkt 4) - bez B5 ksiega zostaje
  dokladnie jak w BEE (prawdziwy stan i mozliwosc wylaczenia), a linia kampanii mowi "NIEWPIETA".
- `static void TogglePre(string key, ref string hint, ref bool current, ref Action<bool> apply)`: gdy `On && key == "lordwealth"`:
  `hint = T_WEALTH; current = false; apply = _noApply;` (`_noApply = v => ClosedClick()` - pole statyczne tworzone raz).
- `static void TogglePost(object __instance, string key)`: gdy `On && key == "lordwealth"`: `rows = _fAllRows?.GetValue(__instance) as System.Collections.IList`;
  gdy `rows != null && rows.Count > 0` -> `_fOnClick?.SetValue(rows[rows.Count - 1], (Action)ClosedClick)` (wiersz dodany wlasnie przez
  AddToggle, LedgerVM.cs:1815; `RowVM.OnClick` - pole publiczne `Action`, RowVM.cs:16). Dzieki temu klik NIE przestawia napisu na ON.
- `ClosedClick()`: `Log.Player(T_WEALTH_CLICK)`. Nic nie zapisuje (`RuntimeSettings.Save` nie jest wolane - user.cfg nietkniety).
- Wszystko w try/catch z `Stumble`. Gdyby postfiks nie dzialal (zmiana pola w BEE), klik przestawi tylko napis, efekt i tak zamyka B5.

### 3.6 Liczniki, linie logu, potkniecia

- Pola statyczne: `_day = -1`, `_armory, _camps, _second, _market, _marketPlayer, _wealthN` (int), `_wealthGold` (long), `_stumbles` (int),
  `_stumbleLogged` (HashSet<string> - pierwszy wyjatek kazdego miejsca do `Log.Error`, kolejne tylko licznik; zasada "licz potkniecia,
  nie gas funkcji").
- `On` = `Settings.Current != null && Settings.Current.LivingEconomySealed` (wlasciwosc; czytana przy kazdym wywolaniu - MCM jest
  przepisywany co godzine gry, ArmouryBehavior.cs:517-523).
- `_marketOpen` (przepuszczone oplaty AI przy otwartych K4/K5), `_trainDone`, `_trainTroops` (int), `_trainXp` (long) - 11.2 pkt 1 i 3.
- `Tick()` na poczatku kazdego prefiksu B1-B5 (tylko gdy `On`) i w postfiksie `TrainDonePost` (zawsze): `int d = (int)CampaignTime.Now.ToDays; if (d != _day) { Flush(); _day = d; }`.
  `Flush()`: gdy suma licznikow > 0 -> jedna linia, potem zerowanie:
  `BEE domkniecie (170) doba <_day>: wywolania zablokowane - produkcja zbrojowni: <_armory> (miasta z gotowa zbrojownia), XP obozow AI: <_camps> (zamki AI z gotowym obozem), druga produkcja: <_second> (wsie z odblokowana druga produkcja), oplata za dostep do targu: <_market> (w tym gracz <_marketPlayer>), zdejmowanie zlota panom: <_wealthN> razy, <_wealthGold> zl[; PRZEPUSZCZONE oplaty AI za dostep do targu (K4/K5 otwarte - tylko oplata tlumi odplyw towaru z niczego): <_marketOpen>][; dokonczone stare sesje szkolenia (D9, nie blokowane, XP z niczego): <_trainDone>, zolnierzy <_trainTroops>, XP <_trainXp>][; potkniecia <_stumbles>]`.
  Pierwsze trzy liczby to wywolania, nie "zatrzymana produkcja": oryginal czesc z nich i tak zakonczylby bez skutku (2.3, B1-B3);
  dwie ostatnie sa praktycznie dokladne (B4 i B5 wychodza bez skutku tylko przy null albo kwocie <= 0 - VDev:620, WealthAudit:340;
  `_wealthGold` to kwota zadana przez BEE).
  Ostatnia doba sesji nie ma linii (swiadomie - brak zaczepu w ArmouryBehavior).
- `Reset()` (z SessionPost): wszystkie liczniki 0, `_day = -1` (bez Flush - poprzednia kampania nie miesza sie z nowa).
- Linia kampanii (koniec SessionPost; czyta tylko statyczne pola BEE i jedna wlasciwosc statyczna `IsActive` przez refleksje):
  `BEE domkniecie (170): kampania - menu BEE: zamkniete z powodem <n>/12 (bee_town_menu a/5, bee_village_menu b/4, bee_castle_menu c/3)[; BRAK: ...]; wylacznik: wlaczony/wylaczony; tryb zgodnosci BK w BEE: TAK | NIE - OTWARTE: LordInvestment AI z zaniedbaniem, WorkshopProductionPatch, Population/Migration i modele BEE czynne (BannerKingsCompatibilityMode=<n>) - sprawdzic kolejnosc ladowania: BK przed BEE | ?; klucze pliku BEE: zamkniete <k>/19 (wariant zbrojowni: b / a / brak)[; OTWARTE: <Klucz>=<wartosc> (ma byc <docelowa>), ...][; B4 WSTRZYMANA - K4/K5 otwarte: oplata AI 5 000 za dostep do targu PRZEPUSZCZANA (zloto w nicosc), bo bez K4/K5 tylko ona tlumi odplyw towaru wsi z niczego - naprawa: skrypt kluczy, nie kod]; user.cfg LordWealthRealism=<0/1> (efekt i tak zamyka latka 170)`.
  Tryb zgodnosci BK: `(bool)_pBkActive.GetValue(null)` w try/catch; `_pBkActive == null` albo wyjatek -> "?" (nie przerywa). Przy NIE
  dodatkowo odczyt pola `BetterEconomySettings.BannerKingsCompatibilityMode` (pole tej samej klasy co 19 kluczy, `_fBkMode` pobrane raz w ApplyAll) do nawiasu.
  Osobny czlon, a nie 20. pozycja licznika kluczy: skrypt `zamknij-ujscia-bee.ps1` tego klucza nie ustawia (zostaje 1 z XML, OPIS:237),
  a o jego wartosci w grze decyduje kolejnosc ladowania, nie plik - mieszanie dwoch przyczyn w jednej liczbie "k/19" zaciemnialoby diagnoze.
  19 wartosci i warunek "zamkniete" (pola statyczne `BetterEconomy.Config.BetterEconomySettings`):
  - int >= 1000000000: `CastleAiLeaderReserveGold`, `LordInvestmentReserveFlat`, `VillageSecondaryRequiredStableDays`, `CaravanDeliveryMinGold`, `CaravanEscortHireMinGold`;
  - float >= 1e9: `ArmoryRequiredArtisans` (wariant b); gdy nie, a `ArmoryAiCheckCooldownDays >= 1000000000` -> "wariant a" (zbrojownia
    gracza zamknieta i tak przez G3); gdy zaden -> OTWARTE;
  - `VillageDiversionRelationThreshold <= -101`, `VillageDiversionGrievanceThreshold >= 101`;
  - `CaravanRecruitPromotionEnabled == 0`; `RouteDangerMaxLossRatio <= 0`; `TradeAgreementCustomsMin <= 0`;
    `TradeAgreementCorridorProsperityPerDay <= 0`; `RaidPeasantFlightFraction <= 0`;
  - wpisy 18/49: `WorkshopDailyInputDrawPerShop <= 0`, `VillageSecondaryBoundTownShare <= 0`, `TownTreasurySurplusPayoutFraction <= 0`,
    `TownTreasuryWarPayoutFraction <= 0`, `TradeAgreementCustomsRate <= 0`, `EstateOwnerPayoutFraction <= 0`.
  Pole nieznalezione -> w liscie OTWARTE jako "<Klucz>=?" (nie przerywa).
- Postfiksy `Can*` niczego nie licza (wolane przy kazdym odswiezeniu menu i otwarciu pickera - liczba bez znaczenia).

### 3.7 Kontrakt bezpieczenstwa

- Kazde cialo latki w try/catch (`Stumble(miejsce, e)`); w prefiksach B1-B5 przy wyjatku i tak `return false`, gdy `On` (domyslnie zamkniete).
- Nasz kod NIGDY nie pisze stanu BEE (`TownEconomyState`, `CastleEconomyState`, `VillageDevelopmentState`, `RuntimeSettings`, pliki BEE) -
  jedyny zapis do obiektu BEE to `RowVM.OnClick` (obiekt ekranu, ginie z ekranem).
- Bez probkowania stosu, bez przegladow swiata; koszt: postfiks O(1), prefiks O(1) + jeden odczyt pola przez refleksje (B1, B3; wolane raz
  na osade na tick BEE - BKROTPatch przepuszcza tick osady BEE raz na 10 dni, OPIS 5.3).

## 4. Co zobaczy gracz (wylacznik wlaczony)

### 4.1 Wejscia

| Wejscie | Wplata miasto / zamek | Zbrojownia | Inwestycja miasto | Inwestycja wies | Dostep do targu | Oboz | Szkolenie |
|---|---|---|---|---|---|---|---|
| Menu BEE "Living Economy" (miasto/wies/zamek) | opcja szara, podpowiedz T_TREASURY | szara, T_ARMORY | 3 opcje szare, T_TOWN_INVEST (tylko w miescie rodu gracza) | 3 opcje szare, T_VILLAGE_INVEST (wies nie wroga) | szara, T_MARKET (wies wlasna albo wies miasta gracza) | szara, T_CAMP | szara, T_TRAIN |
| Pickery BEE (SAS) | gdyby otwarte: pozycje nieaktywne, "Contribute 10000g - Closed: ..." | "Status: Closed: ..."; Apply -> zolty "[LivingEconomy] Closed: ..." | - (brak pickera) | - | "Status: Closed: ..."; Apply -> zolty komunikat | "Status: Closed: ..."; Apply -> zolty | jak oboz |
| BK Demesne: MilitaryVM | okno kwoty -> czerwony "Couldn't contribute: Closed: ..." | okno "Upgrade ... for Xg?" -> czerwony "Couldn't upgrade armory: Closed: ..." | - | - | - | okno -> "Couldn't upgrade camp: Closed: ..." | "Couldn't start training: Closed: ..." (anulowanie dziala) |
| BK Demesne: OverviewVM | jak wyzej | jak wyzej | - | okno kwoty -> "Couldn't invest: Closed: ..." | - | - | - |
| BK EconomyVM | bez zmian (polityka, podatki, projekty - wirtualne) | | | | | | |
| Konsola BK | `bannerkings.be_contribute_treasury` -> "Couldn't contribute: Closed: ..." | `be_upgrade_armory` -> "Couldn't upgrade armory: Closed: ..." | - | `be_village_invest` -> "Couldn't invest: Closed: ..." | - | - | - |
| Ksiega BEE, ustawienia | wiersz "Lord wealth realism": OFF, podpowiedz T_WEALTH; klik -> komunikat T_WEALTH_CLICK, zostaje OFF | | | | | | |

Dlaczego opcje sa widoczne, a nie znikaja: w menu BEE 7 opcji (inwestycje i targ) bez rozdz. 3.4 zniknelaby bez slowa (warunek = wynik `Can*`).
Pickery BEE pokazuja powod same (SAS:204, :262, :357, :467, :515, :596). Ekrany BK nie maja wiazania IsEnabled (bk GUI MilitaryPanel.xml:285-340,
OverviewPanel.xml:152-179) - pytaja o kwote / potwierdzenie (okno zbrojowni i obozu z cena) i dopiero potem pokazuja powod. To SWIADOME
ODSTEPSTWO od litery "opcja nieaktywna" (D5): w BK opcja jest widoczna, nie znika po cichu i zloto nie schodzi, ale przycisk nie jest szary.

### 4.2 Zadna sciezka nie pobiera zlota przed odmowa (dowody)

- Town.TryPlayerContributeTreasury :449-475 - `Can` w :454, zloto w :463; Town.TryPlayerBuildOrUpgradeArmory :568-583 - `Can` :571, zloto :577.
- Castle.TryPlayerContributeTreasury :1135-1158 - `Can` :1140, zloto :1149; TryPlayerBuildOrUpgradeTrainingCamp :600-624 - `Can` :604, zloto :614;
  TryPlayerStartTraining :696-732 - `Can` :700, zloto :718.
- VInv.TryApplyPlayerInvestment :255-282 - `Can` :258, zloto :274; VDev.TryPlayerNegotiateMarketAccess :273-282 - `Can` :275, ApplyMarketAccess :279.
- SAS.TryExecuteTownInvestment :633-680 - `CanPlayerInvest` (:658) przed `LordInvestmentCampaignBehavior.ApplyInvestment` (:664) i relacjami (:678).
- BK: MilitaryVM :625-730, OverviewVM :445-520, BannerKingsCheats :684-756 - pytaja tylko o kwote (ShowTextInquiry / ShowInquiry), zloto
  rusza wylacznie przez Bridge -> Try* BEE. Bridge.TryVillagePlayerInvest wola TryApplyPlayerInvestment refleksja (:1098) - postfiks dziala.
- Wszyscy wolajacy platne metody BEE (grep po be/ i bk/): tylko powyzsze + AI LordInvestment :118 (nierejestrowane przy BK) i AI ApplyMarketAccess
  :609 (B4) oraz ApplyVillageEffects :377 (K2).

## 5. Stare zapisy

| Stan w zapisie (BEE) | Z latka wlaczona | Po wylaczeniu latki (MCM) albo bez Armoury |
|---|---|---|
| Gotowa zbrojownia (`ArmoryLevel` 1-3, gracza albo AI) | Poziom zostaje, produkcja stoi (B1); `ArmoryLastProductionDay` sie nie zmienia; BEE pokazuje poziom w ekranach (kosmetyka; gotowosc wojenna z niego jest wirtualna) | Produkcja wraca od najblizszego ticku, bez nadrabiania zaleglosci (metoda liczy tylko "dzis", Town:2154) |
| Gotowy oboz AI | Poziom zostaje, XP stoi (B2) | XP wraca |
| Oboz w budowie (gracz albo AI) | Budowa sie konczy (Castle:420-446, bez zlota i XP); dalej jak gotowy | - |
| Gotowy oboz gracza | Nowej sesji szkolenia nie da sie zaczac (G8) | Szkolenie znow mozliwe |
| Oplacona sesja szkolenia gracza | Anulowanie dziala (bez zwrotu - tak jest w BEE); dokonczenie dziala, gdy gracz stoi w zamku (D9) - widoczne w linii doby ("dokonczone stare sesje szkolenia") | - |
| Odblokowana druga produkcja wsi (`SecondaryItemId`) | Wpis zostaje, produkcja stoi (B3); ekran BEE moze pokazywac "active" (kosmetyka) | Produkcja wraca od najblizszego ticku |
| Ulamek odplywu wsi AI >= 0.1 (ogon A17) | Przy K4/K5 w pliku: oplata zatrzymana (B4), wolajacy ustawia cooldown (:610); ulamek maleje 0.03 na tick (K4/K5) i znika sam. Bez K4/K5: oplata przepuszczona (jak w BEE), linia kampanii "B4 WSTRZYMANA" | Najwyzej jedna oplata na wies (OPIS 5.3) |
| Patronat gracza we wsi, cooldowny inwestycji, zablokowane granty skarbca | Mnozniki wirtualne, wygasaja same | - |
| `better_economy_user.cfg` | Nietkniety; `LordWealthRealism=1` (gdyby plik zniknal) nic nie zdejmuje (B5), BEE dalej robi dobowy spis kies (koszt BEE, nie nasz) | - |

Nasz kod nie pisze zadnego pola stanu BEE i nie dodaje kluczy zapisu, wiec zapis z latka wczytuje sie bez latki i odwrotnie bez zadnej
naprawy. Nowa kampania niepotrzebna.

## 6. Kolejnosc i zgodnosc z 169 (okna pomiaru) i 171

- **Harmony 2.4.2 (sprawdzone w 0Harmony z gry, `MethodCreator.AddPrefixes` + `AffectsOriginal`):** gdy prefiks zwroci false, kolejne prefiksy,
  ktore "wplywaja na oryginal" (zwracaja bool albo maja parametr ref/out albo typu referencyjnego), sa POMIJANE; prefiksy void bez takich
  parametrow, postfiksy i finalizery biegna zawsze.
- Dlatego nasze prefiksy blokujace maja `Priority.Last` - okna pomiaru 169 (domyslny priorytet) biegna przed nimi, zapisuja `__state`, a ich
  postfiks/finalizer widzi brak przeplywu (zmiana 0). Okno 169 "C4" na `VDev.ApplyMarketAccess` (flaga w prefiksie bez parametrow +
  finalizer, paczka-169-wynik-2.md C4) pokaze 0 - to oczekiwany dowod zamkniecia. Okna C1-C3 (TryAiContribute, TryAiBuildTrainingCamp,
  TryApplyLordInvestment) sa na metodach, ktorych 170 nie lata (zamyka je plik).
- Gdyby 169 wpinal okno na ktoryms z celow B1-B5 TAKZE z `Priority.Last` i parametrem referencyjnym: przy rownym priorytecie Harmony bierze
  kolejnosc rejestracji, wiec `BeeSeal.ApplyAll` MUSI byc wolane PO ApplyAll paczki 169 (linia w SubModuleMain po SpoilsCompany jest po
  MoneyLedger.ApplyAll - przy scalaniu sprawdzic, ze tak zostalo).
- GoodsLedger (146) ma ramki na zewnetrznych `OnSettlementDailyTick` BEE (GoodsLedger.cs:1079-1084), nie na naszych celach - bez kolizji;
  po 170 ramki "BetterEconomy TownEconomyCampaignBehavior / VillageDevelopmentCampaignBehavior" pokaza mniej towaru z niczego.
- Kolejnosc wgrania (PRZEKAZANIE rozdz. 8, 11): 169 (autotest 40 dob) -> scalenie 170 na 169 -> skrypt `zamknij-ujscia-bee.ps1 -ListaZFundamentu`
  (gra zamknieta, kopia, NIE w tej paczce) + 170 razem w jednym tescie -> "wgraj" Jeffa -> 171. 171 (oboz BEE zamyka 170, szkolenie wlasna
  bronia) nie dotyka celow 170; konflikty tylko w Settings.cs / McmSettings.cs / SubModuleMain.cs / CHANGELOG.md (zwarte bloki).

## 7. Testy

### 7.1 Wykonawca (bez gry - TWARDY ZAKAZ uruchamiania gry i autotestu w tej paczce)

1. `python tools/gen_mcm.py` -> w `Armoury/src/McmSettings.cs` jest `LivingEconomySealed` z grupa "The living economy" i przypisanie w `ApplyTo`.
2. Build (komenda z zadania), kod wyjscia 0, `build.log` bez ostrzezen z BeeSeal.cs.
3. Kontrola statyczna w kodzie: 16 wpisow `Wire(...)` z dokladnie tymi tablicami typow co w rozdz. 3.3; brak zapisu do pol stanu BEE (grep
   `SetValue` w BeeSeal.cs = tylko `_fOnClick`); brak `SaveText`/`SyncData`; `Priority.Last` na 13 latkach (8 postfiksow Can + 5 prefiksow);
   teksty po angielsku, komentarze i log bez polskich znakow (`grep -P "[^\x00-\x7F]" Armoury/src/BeeSeal.cs` = pusto).
4. Kontrola sygnatur na DLL z gry (tylko odczyt): `ilspycmd -t <typ> BetterEconomy.dll` dla 7 typow - kazda z 16 sygnatur jak w tabeli 3.3.

### 7.2 Autotest (pozniej, po scaleniu z 169, z probnym DLL; sesja glowna)

Bieg A: `tools/autotest.ps1 -LoadSave autotest-rok-360 -Days 12` (zapis z doby 360, robiony przy OTWARTYCH ujsciach BEE - sa w nim gotowe
zbrojownie, obozy i druga produkcja). Bieg B: nowa kampania `-Days 12` po skrypcie kluczy. W `Modules/Armoury/Armoury-<data>.log`:
- start: `BEE domkniecie (170): BetterEconomy v1.4.5 - wpiete 16/16` i brak "BRAK";
- kampania (oba biegi): `menu BEE: zamkniete z powodem 12/12`, `wylacznik: wlaczony`, `tryb zgodnosci BK w BEE: TAK` (NIE = kolejnosc
  ladowania BK/BEE odwrocona - test niewazny, najpierw kolejnosc); w biegu B `klucze pliku BEE: zamkniete 19/19 (wariant zbrojowni: b)`;
  w biegu przed skryptem kluczy - lista OTWARTE i czlon "B4 WSTRZYMANA - K4/K5 otwarte" (oczekiwane dla testu, ale ZNACZY: oplata AI
  5 000 dalej znika, a 170 nie domyka ani odplywu, ani oplaty - stan przejsciowy, gra nie moze tak zostac); w biegu B tego czlonu NIE ma;
- bieg A: linie `BEE domkniecie (170) doba N: wywolania zablokowane - ...` WARUNKOWO, wedlug tego, co jest w zapisie (liczby to wywolania,
  nie zatrzymana produkcja - 3.6): `produkcja zbrojowni` > 0 tylko, jesli zapis ma miasto z gotowa zbrojownia; `XP obozow AI` > 0 tylko,
  jesli ma zamek AI z gotowym obozem; `druga produkcja` > 0 tylko, jesli ma wies z `SecondaryItemId` (ekran BEE wsi: druga produkcja
  "<towar> (active|suspended, N% extra)", a nie "Locked (d/N stable days)", VDev:139-147) - przy dlawiku BKROT pierwsze odblokowanie wypada najwczesniej ok. 300. doby (OPIS 5.3), wiec 0 na zapisie
  z doby 360 NIE jest bledem; gdy zadnej linii dnia nie ma - sprawdzic te trzy rzeczy w ekranach BEE, zanim uzna sie test za nieudany;
  `oplata za dostep do targu (w tym gracz 0)`; `zdejmowanie zlota panom: 0` przy user.cfg = 0; w biegu B brak "PRZEPUSZCZONE oplaty AI";
  "dokonczone stare sesje szkolenia" tylko, jesli zapis ma oplacona sesje gracza i gracz stoi w tym zamku (autotest zwykle nie - brak = OK);
- zero linii `ERROR in BeeSeal` i zero `potkniecia` w liniach dnia;
- ksiega 169 / MoneyLedger: okno BEE "C4" = 0; "GiveGoldAction w nicosc" nie zawiera juz oplat 5 000 (porownac te same doby tego samego
  zapisu z wylacznikiem off, OPIS 5.2 - nie porownywac roznych dob);
- GoodsLedger: ramka "BetterEconomy VillageDevelopmentCampaignBehavior" bez dodatnich wpisow towaru z niczego (bieg A).
- `bee_log.txt` (grep -a): `settings loaded: 583 applied, 0 skipped`.

### 7.3 Recznie w grze (Jeff, ok. 5 minut)

1. Wejdz do swojego miasta -> "Living Economy": "Contribute to local treasury", "Build / Upgrade Armory" i "Invest 10,000 / 50,000 / 100,000"
   sa SZARE, najechanie myszka pokazuje "Closed: ..."; "Change policy", "Change tax policy", "Start infrastructure project" dzialaja jak dawniej.
2. Wies (dowolna nie-wroga): "Invest 5,000 / 15,000 / 30,000" szare z powodem; we wlasnej wsi takze "Negotiate market access".
3. Swoj zamek: "Contribute to castle treasury", "Build / Upgrade Training Camp", "Train troops" szare z powodem.
4. Ekran BK (Demesne -> Military): "Contribute" / "Upgrade armory" -> NAJPIERW okno z kwota albo cena (tak ma byc - swiadome odstepstwo,
   D5: przyciski BK nie umieja byc szare), po zatwierdzeniu czerwony "Couldn't ...: Closed: ..."; zloto bez zmian.
5. Ksiega BEE (klawisz ksiegi BEE) -> ustawienia: "Lord wealth realism" = OFF z "Closed: ..."; klik -> komunikat, dalej OFF
   (tylko gdy linia startowa ma "zdejmowanie zlota panom" wsrod wpietych; inaczej przelacznik jak w BEE).
6. MCM -> Armoury -> "The living economy" -> "Living Economy Sealed" off -> po chwili (do godziny gry) opcje znow aktywne; wlacz z powrotem.

## 8. Ryzyka / co sprawdzic

1. **Aktualizacja BEE**: bramka wersji wylaczy cala 170 (linia "NIC nie wpiete") - akcje gracza i bierne zrodla OTWARTE do czasu przegladu;
   ta sama aktualizacja zwykle przywraca tez plik ustawien (13 kluczy) - linia kampanii pokaze OTWARTE. Po kazdej aktualizacji BEE: przeglad
   dekompilacji + `zamknij-ujscia-bee.ps1 -NaSucho`.
2. **Gracz traci 8 akcji BEE** (zmiana rozgrywki - zgodna z decyzja Jeffa 08.10): jedyna droga rozwoju miasta to budowle miasta gry/BK
   (place i materialy z kiesy wlasciciela - BuildFunding, `PaidConstructionPlayer` domyslnie wlaczone), wsi - jej ludnosc BK. Projekt BEE
   "Start infrastructure project" zostaje czynny, ale placi wirtualny skarbiec i ma tylko skutki wirtualne (Town:2002-2047; mnozniki
   FeudalEconomy przy BK = 1, FeudalEconomyCampaignBehavior.cs:296, :334) - dlatego T_TOWN_INVEST kieruje do budowli, nie do "projektow".
   Pytania P1, P2.
3. **Ekrany BK** najpierw pytaja o kwote / potwierdzenie z cena, dopiero potem mowia "Closed" (D5) - swiadome odstepstwo od litery
   "nieaktywna" (przyciski BK bez wiazania IsEnabled), zloto nie schodzi; wpisane w CHANGELOG. Gdyby Jeffowi przeszkadzalo okno z cena -
   7 prefiksow na `Execute*` BK (lista w D5) zamieni je na sam komunikat, osobna mala zmiana.
4. **Dlugie powody w tytulach pickerow BEE** ("Contribute 10000g - Closed: ...") moga sie zawijac - kosmetyka.
5. **Stary zapis**: oplacona sesja szkolenia daje jeszcze raz XP z niczego (D9) - widac to w linii doby ("dokonczone stare sesje szkolenia: N, zolnierzy T, XP M"); gotowe zbrojownie/obozy/druga produkcja widoczne w ekranach BEE,
   choc stoja.
6. **Okna 169**: zgodnosc zalezy od `Priority.Last` i kolejnosci ApplyAll (rozdz. 6) - sprawdzic przy scalaniu.
7. **WealthAudit przy utraconym user.cfg**: zdejmowanie zatrzymane, ale BEE dalej robi dobowy spis wszystkich bohaterow (koszt BEE) - na liste
   optymalizacji na koniec (decyzja Jeffa 08.10), nie teraz.
8. **Kolejnosc ladowania BK (11) przed BEE (12)** - bez zmian wobec OPIS 7. Przy odwrotnej BK zeruje `BannerKingsCompatibilityMode`
   po BEE (bk/BannerKings/Main.cs:331), BEE wypada z trybu zgodnosci i rejestruje LordInvestment AI z zaniedbaniem (dobrobyt i zywnosc
   miast w nicosc, LordInvestmentCampaignBehavior.cs:192-219), Population/Migration, swoje modele, a WorkshopProductionPatch mnozy zloto
   warsztatow z niczego (WorkshopProductionPatch.cs:17-53) - zadna latka 170 tego nie zamyka, a 13 kluczy w pliku by to przegapilo. Wykrywa
   to linia kampanii ("tryb zgodnosci BK w BEE: NIE - OTWARTE ..."); naprawa = kolejnosc w launcherze, nie kod.
8a. **170 bez kluczy K4/K5** (skrypt niepuszczony albo Steam "verify" przywrocil XML bez zmiany wersji BEE): B4 przepuszcza oplate AI
   5 000 (zloto w nicosc), bo zablokowana zdjelaby jedyny hamulec odplywu towaru z niczego (A9) - mniejsze zlo; linia kampanii "B4
   WSTRZYMANA - K4/K5 otwarte", linia doby "PRZEPUSZCZONE oplaty AI". Naprawa = skrypt kluczy, nie kod.
9. **Podpowiedz w menu**: zalozenie, ze UI gry pokazuje Tooltip opcji nieaktywnej (tak dziala w menu gry, np. odmowy w menu miasta) -
   sprawdzic w kroku 7.3.1.
10. **Nie sprawdzono w grze niczego** - wszystko z kodu (dekompilacja BEE v1.4.5, BK, gra 1.4.8, Harmony 2.4.2).

## 9. Pytania do Jeffa (tylko zmiany rozgrywki; praca nie czeka na odpowiedz - domyslnie "nie")

- **P1.** Wplata do skarbca miasta/zamku BEE jest zamknieta (zloto znikalo). Czy chcesz w zamian prawdziwa wplate "do kasy miasta"
  (Twoje zloto trafia do kasy targu, z ktorej miasto placi warsztatom, rzemieslnikom i kupuje towar) jako nowa opcje Armoury?
- **P2.** "Negotiate market access" (5 000) jest zamkniete (zloto znikalo, korzysc i tak zerowa po kluczach). Czy chcesz w zamian "dar dla
  starszyzny wsi" - zloto trafia do notabli wsi, Ty dostajesz relacje?

## 10. Kontrola wg zasady 0 (CLAUDE.md 8.0)

- **Regresje:** cele czytane/pisane przez nasz kod - zaden inny plik Armoury nie lata tych 16 metod (grep `Armoury/src` po nazwach: tylko
  komentarze o BEE w GoodsLedger/RawPrice/WesterosClimate/WinterSource); GoodsLedger i MoneyLedger dalej widza swoje ramki i zdarzenia.
- **Kolizje:** 169 - rozdz. 6; 171 - rozne cele; BK nie lata celow 170; zaden inny mod nie zawiera ich nazw (grep po DLL Modules).
- **Spojnosc:** jedna zasada (BEE nie tworzy i nie niszczy zlota, towaru, ludzi, XP) dla AI (plik) i gracza (170); nic nie liczy sie dwa razy;
  martwy kod nie ozywa (B1-B5 tylko przestaja dzialac; projekty wirtualne bez zmian).
- **Cudzy kod:** kazda sygnatura, kazdy wolajacy i kolejnosc "Can przed zlotem" sprawdzone w dekompilacji (rozdz. 2, 4.2); semantyka
  prefiksow - w 0Harmony 2.4.2 z gry. Zalozenie "BEE w trybie zgodnosci BK" (od niego zalezy polowa ujsc BEE) nie jest juz ciche - pilnuje
  go linia kampanii (D13, rozdz. 11 pkt 1).

## 11. Krytyka i odpowiedzi

Kazda uwaga sprawdzona ponownie w dekompilacji (`ore-supply/be`, `ore-supply/bk`, `ore-supply/cs`), w drzewie roboczym i w plikach BK
w folderze gry (tylko odczyt). Wynik: 8/8 uwag prawdziwych co do faktow; 7 wprowadzonych tak, jak proponowal krytyk (albo w rownowaznej
formie), 1 (pkt 8) wprowadzona w pierwszym z dwoch proponowanych wariantow (jawne odstepstwo zamiast nowych latek na BK).

| Nr | Waga | Uwaga (skrot) | Sprawdzenie w kodzie | Werdykt i co zmieniono |
|---|---|---|---|---|
| 1 | wazne | Linia kampanii nie sprawdza trybu zgodnosci BK; przy odwrotnej kolejnosci ladowania napisze "19/19", choc LordInvestment AI z zaniedbaniem, WorkshopProductionPatch, Population/Migration i modele BEE sa czynne | `BannerKingsAdapter.IsActive` = `ModCompatibility.HasBannerKings && BetterEconomySettings.BannerKingsCompatibilityMode != 0` (be/BetterEconomy.Compatibility/BannerKingsAdapter.cs:49-59); BK zeruje pole w swoim OnSubModuleLoad (bk/BannerKings/Main.cs:331); BEE ustawia je z XML (better_economy_settings.xml:23 = 1; SettingsLoader.cs:40, :79) w swoim OnSubModuleLoad (BetterEconomySubModule.cs:35) - wygrywa ten, kto laduje sie pozniej; OnGameStart czyta tryb raz (:72) i od niego zalezy rejestracja Population/RecruitmentAudit/Migration (:79-84), LordInvestment AI (:94-97, zaniedbanie LordInvestmentCampaignBehavior.cs:92, :192-219) i modeli BEE (:100-111); WorkshopProductionPatch.cs:17 czyta tryb przy kazdym wywolaniu; BEE tego trybu sam nie loguje (LogCompatibilityDetections :203-212 mowi tylko o wykryciu BK) | PRAWDA - wprowadzone: czlon "tryb zgodnosci BK w BEE: TAK / NIE - OTWARTE ... / ?" w linii kampanii (3.6), pole `_pBkActive` (3.3 pkt 6), D13, D10, wiersz 2.4, oczekiwanie "TAK" w 7.2, ryzyko 8, zdanie w CHANGELOG (3.1 pkt 5). Jedna roznica wobec propozycji: osobny czlon, nie 20. pozycja w "k/19" - skrypt tego klucza nie ustawia (OPIS:237), a jego wartosc zalezy od kolejnosci ladowania, nie od pliku. |
| 2 | drobne | Uzasadnienie D2 ma bledna przeslanke: prawdziwa wplata nie wymaga transpilera ani kopii ciala BEE | `TryPlayerContributeTreasury` jest publiczna i najpierw wola `Can`, potem placi: Town:449 / `Can` :454 / zloto :463; Castle:1135 / :1140 / :1149; `GiveGoldAction.ApplyForCharacterToSettlement(Hero, Settlement, int, bool)` istnieje (cs/TaleWorlds.CampaignSystem.Actions/GiveGoldAction.cs:47). `Can` liczy cooldown i limit ze stanu BEE (Town:423-445), menu mowi "Contribute to local treasury" / "castle treasury" (SMB:898, :1198) | PRAWDA - decyzja (zamkniecie) zostaje, uzasadnienie D2 przepisane: opcja opisuje i limituje wirtualny skarbiec, cooldown wymagalby pisania stanu BEE; prawdziwa wplata to tania osobna funkcja Armoury (P1), nie przerobka BEE. |
| 3 | drobne | T_TOWN_INVEST kieruje do "the town's projects", a projekt BEE w tym samym menu jest bez skutku przy BK | `bee_town_project` "Start infrastructure project" (SMB:874) zostaje czynny; `TickInfrastructureProject` wydaje tylko `LocalTreasuryGold` (Town:2002-2047); WorkshopDistrict dziala przez FeudalEconomy (:311, :349), a `GetProductionEfficiency` / `GetProductionQuality` zwracaja 1f przy `!IsEnabled` (:296, :334). Prawdziwe budowy: BuildFunding (place i materialy z kiesy wlasciciela, Armoury/src/BuildFunding.cs:13-23; `PaidConstructionPlayer = true`, Settings.cs:413) | PRAWDA - T_TOWN_INVEST konczy sie teraz "- invest through the town's buildings instead." (3.2); ryzyko 2 dopowiada, ze projekt BEE zostaje, ale jest wirtualny. |
| 4 | drobne | Liczniki B1-B3 licza wywolania, a linia dnia nazywa je "zatrzymane" | B1: oryginal wychodzi bez skutku przy braku wsadu (Town:2190-2197) i gdy nic nie zuzyl (:2225-2227); B2: pusty garnizon / zerowe XP (Castle:512, :517); B3: `!IsSecondaryActive` (VDev:398 -> :673-687: zawieszenie :679, hearth/chlopi :684, najazd :686) | PRAWDA - wprowadzone uczciwe nazwy: "wywolania zablokowane - produkcja zbrojowni: N (miasta z gotowa zbrojownia) ..." (3.6), opis w 2.3 i D14, oczekiwania 7.2. Dodatkowego warunku dla B3 nie dodano: `SecondarySuspendedDays` pokrylby tylko 1 z 4 warunkow, a wywolanie `IsSecondaryActive` refleksja wola `GetOrCreatePopulation` (moze tworzyc stan BEE - zakaz 3.7); dowod braku towaru z niczego daje GoodsLedger (7.2). B4 i B5 sa praktycznie dokladne (VDev:620, WealthAudit:340). |
| 5 | drobne | Linia startowa podaje stan wylacznika przed pierwszym `McmSettings.Apply` i pisze "dziala od razu" | Linia powstaje w `OnBeforeInitialModuleScreenSetAsRoot` (SubModuleMain.cs:42), `McmSettings.Apply` jest w OnGameStart (:205) i co godzine gry (ArmouryBehavior.cs:521-523) | PRAWDA - stan wylacznika usuniety z linii startowej (3.3 pkt 7; ma go linia kampanii, ktora powstaje po OnGameStart), dopisek "zmiana wylacznika w MCM dziala w ciagu godziny gry". |
| 6 | drobne | `GetRawConstantValue()` rzuca, gdy pole nie jest stala - bramka wersji zgubilaby czytelna linie przy zmianie `const` na `static readonly` | Dzis `public const string Version = "v1.4.5"` (BetterEconomySubModule.cs:22); w .NET Framework `GetRawConstantValue` na polu nie-literalnym rzuca `InvalidOperationException`, a wyjatek wylecialby do zewnetrznego `Log.Error("BeeSeal.ApplyAll")` | PRAWDA - bramka przepisana (3.3 pkt 2): wlasny try/catch, `IsLiteral ? GetRawConstantValue() : GetValue(null)`, wynik nie-string albo wyjatek = "nieczytelna" i ta sama linia "NIC nie wpiete". |
| 7 | drobne | Test 7.2 bieg A wymaga "druga produkcja wsi > 0", a w zapisie z doby 360 moze nie byc zadnej odblokowanej | tools/bee/OPIS.md:374-375: przy dlawiku BKROT pierwsze odblokowanie najwczesniej ok. 300. doby, w naszych zapisach zadnego; zapis autotest-rok-360 nie byl pod tym katem sprawdzany | PRAWDA - oczekiwania 7.2 warunkowe dla zbrojowni, obozow i drugiej produkcji, z instrukcja, jak sprawdzic w ekranie BEE wsi ("<towar> (active/suspended, N% extra)" vs "Locked", VDev:139-147); 0 nie jest bledem. |
| 8 | drobne | Ekrany BK nie spelniaja litery "opcja widoczna, ale nieaktywna, z powodem" - przyciski aktywne, okna z cena | MilitaryVM.cs:626 (zbrojownia z cena), :669 (oboz z cena), :646 i :714 (kwota), OverviewVM.cs:445, :472, :496; start szkolenia :682 odmawia od razu; prefaby BK bez wiazania IsEnabled (MilitaryPanel.xml:285-340, OverviewPanel.xml:152-179 - tylko `Command.Click`); zloto rusza tylko w `Try*` BEE po `Can` (4.2) | PRAWDA co do faktu - wybrany pierwszy wariant krytyka: D5 zostaje i jest jawnie opisany jako swiadome odstepstwo (D5, rozdz. 0, 4.1, CHANGELOG 3.1 pkt 5, test 7.3.4, ryzyko 3). 7 prefiksow na `Execute*` BK odrzucone: szarosci i tak by nie bylo (brak wiazania w cudzym prefabie), zysk to tylko pominiecie okna z cena, koszt - nowa powierzchnia na aktualizacje BK (zasada 3 CLAUDE.md); zostawione jako mozliwa osobna mala zmiana, gdyby okno przeszkadzalo Jeffowi. |

Zakres paczki bez zmian: dalej 15 zaczepow, zero nowych kluczy zapisu, zero zapisow stanu BEE; doszedl tylko jeden odczyt (tryb zgodnosci
BK, raz na kampanie) i poprawione teksty / linie logu / oczekiwania testu.

### 11.2 Recenzja wykonania (commit 503ebc6) - 4 uwagi, wszystkie prawdziwe

Kazda sprawdzona ponownie w `ore-supply/be` i w DLL BEE z gry (`ilspycmd -t`, md5 267ba08d). Zakres po poprawkach: 16 zaczepow
(15 domykajacych + 1 tylko do logu), dalej zero kluczy zapisu i zero zapisow stanu BEE.

| Nr | Waga | Uwaga (skrot) | Sprawdzenie w kodzie | Werdykt i co zmieniono |
|---|---|---|---|---|
| 1 | drobne | D9 zostawia dokonczenie oplaconej sesji szkolenia (XP z niczego), ale w logu Armoury go nie widac | `TickTraining` (Castle:448-483) -> `CompletePlayerTrainingSession` (:474 -> :879, `AddXpToTroop` :924); jedyny wolajacy; BEE pokazuje tylko komunikat na ekranie i `BEELog.Verbose` | PRAWDA - D9 zostaje; NOWY postfiks tylko do logu `TrainDonePost(ref int trained, ref int xpTotal)` (sygnatura w DLL z gry: `out int trained, out int wounded, out int xpTotal, out int retinues`), liczony zawsze; linia doby dopisuje "dokonczone stare sesje szkolenia (D9, nie blokowane, XP z niczego): N, zolnierzy T, XP M". |
| 2 | drobne | Tabela 2.4: pobor przy werbunku nieczynny nie "tylko w trybie zgodnosci", lecz zawsze przy BK; zle numery linii wolajacych B1 i B2 | RecruitmentPatch.cs:127 `if (ModCompatibility.HasBannerKings || ...) return;` (wolajacy :26, :94, RecruitmentAudit:157); Town:1696 `TickArmoryProduction(...)`; Castle:457-460 warunek i wywolanie `ApplyAiTrainingCampPassive` | PRAWDA - osobny wiersz 2.4 dla RecruitmentPatch (HasBannerKings, niezaleznie od trybu i kolejnosci); B1 "wolana z :1696", B2 "TickTraining :457-460". Kod bez zmian (linia kampanii slusznie nie wymienia RecruitmentPatch). |
| 3 | drobne | B4 bez K4/K5 zamienia jedna dziure na druga: blokada oplaty AI zdejmuje jedyny hamulec odplywu towaru z niczego | `ApplyMarketAccess` ustawia `DiversionSuppressedUntilDay` (VDev:628); tylko on w `TickGrievanceAndDiversion` (:484-489) gasi odplyw, gdy K4/K5 otwarte; `ApplyDiversionStock` (:511 -> :551) dodaje towar do targu obcego miasta bez zdjecia z wsi | PRAWDA - wybrany wariant kodowy: `MarketPre` przy `!player` sprawdza K4 (`<= -101`) i K5 (`>= 101`) przy kazdym wywolaniu (rzadkie - cooldown AI; uwzglednia przeladowanie pliku BEE Ctrl+Shift+M); gdy otwarte -> `return true` i licznik `_marketOpen`. Gracz blokowany zawsze (G6 i tak zamyka go wczesniej). Linia kampanii: "B4 WSTRZYMANA - K4/K5 otwarte ..."; linia doby: "PRZEPUSZCZONE oplaty AI ...". Uzasadnienie: bez kluczy 170 nie moze domknac obu dziur naraz; zablokowana oplata nie daje wsi 14 dob tlumienia, wiec towar z niczego plynalby do obcego miasta co tick - mniejszym zlem jest oplata (jak w BEE), do czasu skryptu kluczy. Opis wylacznika w MCM dopowiada ten warunek. |
| 4 | drobne | Przelacznik "Lord wealth realism" zamykany bez sprawdzenia, czy B5 jest wpiety - ksiega pokazuje OFF, a BEE dalej zdejmuje zloto | `TogglePre`/`TogglePost` warunkowaly tylko `On` i klucz; `RuntimeSettings.LordWealthRealism = true` domyslnie (RuntimeSettings.cs:13), WealthAudit:197, :346 | PRAWDA - `_wealthWired` (ustawiane w ApplyAll po `Wire` B5) w obu zaczepach: bez B5 ksiega dokladnie jak w BEE; linia kampanii przy NIEWPIETA dopisuje "przelacznik w ksiedze BEE zostaje jak w BEE". Opcja "przepuscic klik w strone OFF" odrzucona: wymaga zapisu user.cfg przez BEE, a spec (3.5, 3.7) i zadanie tego zabraniaja; przy wpietym B5 skutek i tak zamyka latka. |

## 12. Wykonanie (08.10)

- Pliki: NOWY `Armoury/src/BeeSeal.cs`; `Settings.cs` (blok "The living economy" przed "Plague shield"); `McmSettings.cs` z
  `python tools/gen_mcm.py` (Armoury 680 -> 681; RealisticCaptivity i GrandTourney bez zmian); `SubModuleMain.cs` (jedna linia po
  `SpoilsCompany.ApplyAll`, przed `BattleWind` ... `GoodsLedger`); `CHANGELOG.md` (wpis 170 na gorze). `ArmouryBehavior`, SaveDefiner,
  SaveText - nietkniete.
- Testy 7.1: (1) MCM - `LivingEconomySealed`, grupa "The living economy", przypisanie w `ApplyTo`; (2) build kod 0, jedyne ostrzezenie
  stare (BattlefieldLaw.cs, CS0169); (3) 15 `Wire`, 13 z `Priority.Last`, jedyny `SetValue` = `_fOnClick`, zero `SaveText`/`SyncData`,
  `grep -P "[^\x00-\x7F]"` po BeeSeal.cs pusto; (4) `ilspycmd -t` na `Modules/BetterEconomy/bin/Win64_Shipping_Client/BetterEconomy.dll`
  (md5 267ba08de27928ceeb81c401e50f5b9e) dla 14 typow: 15 sygnatur jak w 3.3, 12 identyfikatorow opcji menu, 21 pol
  `BetterEconomySettings`, `IsActive`, `Version`, `ArmoryLevel`, `SecondaryItemId`, `_allRows`, `OnClick`, `RuntimeSettings`.
- Drobne roznice wobec litery 3.x (bez zmiany zakresu):
  1. Koncowka linii kampanii przy user.cfg mowi prawde o stanie B5: "(efekt i tak zamyka latka 170)" tylko, gdy B5 wpiety i wylacznik
     wlaczony; inaczej "(latka 170 na zdejmowanie zlota NIEWPIETA / wylaczona wylacznikiem - przy 1 BEE zdejmuje zloto panom w nicosc)".
  2. Linia doby pada takze wtedy, gdy w dobie byly same potkniecia (bez zablokowanych wywolan); licznik potkniec jest zerowany co dobe
     jak inne liczniki. Pierwszy wyjatek kazdego miejsca idzie do `Log.Error` raz na uruchomienie gry (`_stumbleLogged` nie jest
     czyszczony w `Reset`).
  3. `_noApply` to pole `static readonly` (tworzone raz przy inicjalizacji typu), nie w `ApplyAll` - to samo zachowanie.
  4. `WealthPre` dolicza do `_wealthGold` tylko `amount > 0` (oryginal i tak wychodzi przy `amount <= 0`, WealthAudit:340).
  5. `Wire` ma parametr `last` (Priority.Last dla 13 latek, domyslny priorytet dla menu i przelacznika) i zglasza brak typu parametru
     (np. `TownEconomyState`) jako BRAK z etykieta, bez wyjatku.
- Nic nie uruchomione w grze, nic nie skopiowane do folderu gry; plik ustawien BEE i user.cfg nietkniete.
- Poprawki po recenzji (11.2): `BeeSeal.cs` - 16. zaczep `TrainDonePost` (tylko log), `MarketPre` warunkowy na K4/K5 (`DiversionClosed`,
  wspolny `KeyClosed` z linia kampanii), `_wealthWired` w `TogglePre`/`TogglePost`, czlon "B4 WSTRZYMANA" w linii kampanii, dopiski
  w linii doby; `Settings.cs` - opis wylacznika (warunek oplaty AI) + `McmSettings.cs` z `python tools/gen_mcm.py` (Armoury 681, bez
  nowych ustawien); build kod 0 (jedyne ostrzezenie stare, BattlefieldLaw.cs CS0169); sygnatura `CompletePlayerTrainingSession` i typ
  pol K4/K5 (public static float) sprawdzone `ilspycmd -t` na DLL z gry (md5 267ba08d). Nic nie uruchomione w grze.
