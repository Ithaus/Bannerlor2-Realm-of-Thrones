using System;
using System.IO;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    public class SubModuleMain : MBSubModuleBase
    {
        private const string HarmonyId = "com.jeff.armoury";
        private static bool _patched;
        private static bool _modelsPatched;
        private static Harmony _harmony;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            try
            {
                var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);   // bin/Win64_Shipping_Client
                var moduleRoot = Path.GetFullPath(Path.Combine(dir, "..", ".."));
                Log.Init(moduleRoot);
                Settings.Load(Path.Combine(moduleRoot, "ModuleData"));
                Log.Info("Ustawienia wczytane. CraftingEnabled=" + Settings.Current.CraftingEnabled + " WearEnabled=" + Settings.Current.WearEnabled);
                // HT (15.10 noc): straznik celu latek na PatchProcessor.Patch PRZED wszystkimi naszymi latkami (Armoury 31. - przed ForgeView, GT i BKROTPatch);
                // wlasny try - jego wywrotka nie zabiera latek ponizej
                try { PatchTarget.InstallGuard(); } catch (Exception e) { Log.Error("PatchTarget.InstallGuard", e); }

                if (!_patched)
                {
                    _harmony = new Harmony(HarmonyId);
                    _harmony.PatchAll(Assembly.GetExecutingAssembly());
                    _patched = true;
                    Log.Info("Harmony: patche zaaplikowane.");
                    SaveText.InstallRescue(_harmony);   // 161: zapis z napisem > 32767 B wczytuje sie (przed pierwszym wczytaniem)
                }
            }
            catch (Exception e) { Log.Error("OnSubModuleLoad", e); }
            // f9-predkosc: przyciski biegu na pasku mapy (UIExtenderEx: wstawka prefabu MapBar + mixin MapTimeControlVM) - rejestracja przed pierwszym ruchem paska,
            // wlasny try i metoda NoInlining (brak zestawu UIExtenderEx = linia w logu, reszta modulu bez zmian)
            try { SpeedUiRegister(); } catch (Exception e) { Log.Error("MapSpeedUi.Register (UIExtenderEx)", e); }
            // r4-wioska: wioska poboczna gracza w zakladce Fiefs ekranu rodu (UIExtenderEx: wstawka prefabu ClanFiefs + mixin ClanFiefsVM) - osobny UIExtender, wlasny try
            try { SideTabRegister(); } catch (Exception e) { Log.Error("SideVillageFiefUi.Register (UIExtenderEx)", e); }
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void SpeedUiRegister() { MapSpeedUi.Register(); }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void SideTabRegister() { SideVillageFiefUi.Register(); }

        /// <summary>Modele lataamy dopiero gdy wszystkie mody sa zaladowane.</summary>
        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();
            // E7/W2 (233): typ dymka wioski z herbem (wlasny prefab Armoury\GUI\Prefabs) - raz na proces, we wlasnym try i metodzie NoInlining (blad ladowania typow VM
            // zostaje tutaj); niezalezny od latek ponizej - ich wywrotka nie zabiera dymka
            try { TipRegister(); } catch (Exception e) { Log.Error("MapVillageTooltip.Register (ladowanie)", e); }
            try
            {
                if (_modelsPatched) return;
                _modelsPatched = true;
                if (_harmony == null) _harmony = new Harmony(HarmonyId);
                // m101-prolog: okno wyboru startu nowej kampanii (Prologue 298 / Classic 299) - prefiks MBGameManager.StartNewGame, raz na proces; wlasny try
                try { Prologue.ApplyAll(_harmony); } catch (Exception e) { Log.Error("Prologue.ApplyAll", e); }
                // m103-sceny: obrazki Jeffa w oknach wydarzen ROT (prefiks / postfiks EventScreen OnActivate / OnDeactivate, odbicie - bez typow ROT); wlasny try
                try { RotWindowPictures.ApplyAll(_harmony); } catch (Exception e) { Log.Error("RotWindowPictures.ApplyAll", e); }
                // m103-lektor: nagrania okien ROT (ModuleData/Lektor/EN) i latka ROT.Screens.EventScreen odbiciem (OnActivate po, OnDeactivate przed) - raz na proces; wlasny try
                try { Lektor.Install(_harmony); } catch (Exception e) { Log.Error("Lektor.Install", e); }
                // m105-czcionki: nazwy osad na mapie czcionka ROT - postfiks SettlementNameplateWidget (odbicie; tabliczki buduje gotowy kod gry, prefaby i UIExtenderEx nie dzialaja); wlasny try
                try { NameplateFont.Install(_harmony); } catch (Exception e) { Log.Error("NameplateFont.Install", e); }
                WeaponXpPatch.ApplyAll(_harmony);
                ScrapFloorPatch.ApplyAll(_harmony);
                BkPenaltyOnce.ApplyAll(_harmony);    // cena sprzedazy sprzetu (A): kara handlowa BK raz (x5) - dubel postfiksu BK zdjety z modelu, ktory wola base; BK wpina go wczesniej, w OnSubModuleLoad
                try { A16Signals.ApplyAll(_harmony); } catch (Exception e) { Log.Error("A16Signals.ApplyAll", e); }   // A16: premia pokoju RBL po 30 dobach spokoju, dubel warstwy dobrobytu BK, pomiar ludnosci BK (latki pomiaru zawsze)
                SellByCondition.ApplyAll(_harmony);  // cena sprzedazy sprzetu (B): ksiega skupu (tylko log) - kto sprzedaje: ekran handlu gracza, SellItemsAction
                TrueArmourCost.ApplyAll(_harmony);
                // m110-animacje: latka HP sprzetu na Agent.InitializeSpawnEquipment - TU, nie w PatchAll z OnSubModuleLoad (tam psula indeksy akcji: postac lezy w ekwipunku)
                try { HeroHp.Install(_harmony); } catch (Exception e) { Log.Error("HeroHp.Install", e); }
                ThrownWobblePatch.ApplyAll(_harmony);
                FairXpPatch.ApplyAll(_harmony);
                try { RaceLaw.ApplyAll(_harmony); } catch (Exception e) { Log.Error("RaceLaw.ApplyAll", e); }   // Jeff 10.10: sluby i ciaze tylko w obrebie rasy (olbrzymy z olbrzymami) - model slubu, MarriageAction, ciaza, porod
                // CR (zasada Jeffa 14.10 "1 rok = 1 rok, 1 dzien = 1 dzien"): czas ciazy PregnancyDays (getter modelu gry) i szansa poczecia x 84 / rok
                // (FertilityYearScale) - postfiksy na DefaultPregnancyModel (czynny BKROTPregnancyModel dziedziczy, metode szansy gry wola niewirtualnie); wlasny try
                try { PregnancyYear.ApplyAll(_harmony); } catch (Exception e) { Log.Error("PregnancyYear.ApplyAll", e); }
                DonationXpLaw.ApplyAll(_harmony);   // audyt 13 Z1 (Jeff 09.10): oddany sprzet nie uczy - XP perkow Giving Hands / Paid in Promise = 0, migawka handlu BK przy kazdym otwarciu handlu
                // RP (decyzja Jeffa 14.10): punkt cechy co AttributePointEveryLevels, dodatkowe skupienie co ExtraFocusEveryLevels - getter modelu rozwoju,
                // HeroDeveloper.OnGainLevel i SetupDefaultPoints (prywatne - TU, w menu glownym, zanim JIT skompiluje wolajacych); klucze na zywo; wlasny try
                try { LevelPoints.ApplyAll(_harmony); } catch (Exception e) { Log.Error("LevelPoints.ApplyAll", e); }
                ChargeTemperPatch.ApplyAll(_harmony);
                // noc 25.10 P6 (Jeff 25.10: "i dodaj friendly fire po obu stronach", "cios wrecz nie tylko ostrzaly"): transpilery Mission.MeleeHitCallback /
                // MissileHitCallback / ChargeDamageCallback i postfiks CalculateDamage - TU, obok ChargeTemperPatch, zanim JIT skompiluje wolajacych; wlasny try.
                // Recenzja P6: klucz Friendly Fire wylaczony przy starcie gry (plik MCM) = ApplyAll nic nie wpina (sprawdza sam)
                try { FriendlyFire.ApplyAll(_harmony); } catch (Exception e) { Log.Error("FriendlyFire.ApplyAll", e); }
                TravellerGoods36.ApplyAll(_harmony);   // 3.6: wedrowcy BK bez towaru, rudy i drewna z niczego (postfiks po CastleSteel - Priority.Low)
                CastleSteel.ApplyAll(_harmony);   // 177-1: wedrowcy BK bez sztab i wegla z niczego (dawny ValyrianSteel.DearRefine zdjety - stal zamkowa to zwykly stopien)
                TravellerTrade36b.ApplyAll(_harmony);   // 36b: wedrowcy BK z prawdziwym towarem - zywnosc drogi z polki pochodzenia, bez jucznych z niczego, towar w komis miasto -> miasto
                SmithAudit.ApplyAll(_harmony);
                QuartermasterLaw.ApplyAll(_harmony);
                try { GarrisonKit.ApplyAll(_harmony); } catch (Exception e) { Log.Error("GarrisonKit.ApplyAll", e); }   // K1 (A10): zaloga w bitwie gracza walczy sprzetem ze swojej zbrojowni (latka DTE)
                try { AutotestBattleKit.ApplyAll(_harmony); } catch (Exception e) { Log.Error("AutotestBattleKit.ApplyAll", e); }   // f5-bitwa: zestaw bitwy autotestu (tylko DLL autotestu CrashScribe)
                MarketGlut.ApplyAll(_harmony);
                SupplyDemand.ApplyAll(_harmony); // prawo podazy i popytu dla uzbrojenia - PO MarketGlut (Jeff 04.10)
                MaterialLaw.ApplyAll(_harmony);  // surowce: przetopy, XP przetopu, wydobycie (Jeff 04.10)
                MineralOnce.ApplyAll(_harmony);  // lista produkcji wsi BK bez powtorzen: mineral wsi gorniczej dopisywany raz (Jeff 05.10); MUSI isc po MaterialLaw.ApplyAll (zdjety wpis oddaje jego postfix modelu)
                // GL-1 (P1): linia "People of this land" w dymku osady (wies) - postfiks dymka gry i NavalDLC (ten rejestruje dymek osady od nowa); wlasny try,
                // linia dziala tylko przy kluczu VillageOutputByPeople
                try { VillageByPeople.ApplyTip(_harmony); } catch (Exception e) { Log.Error("VillageByPeople.ApplyTip", e); }
                try { DistrictIcon.ApplyAll(_harmony); } catch (Exception e) { Log.Error("DistrictIcon.ApplyAll", e); }   // inwest: ikona okregu (tabliczka, dymek osady, lista produkcji miasta) - latki UI raz na proces
                // m105-dziennik: prefiks tabliczek osad (wiersze handlu karawan i wiesniakow - klucz MapHideTradeNotices) i odbicie widoku dziennika - raz na proces, przed
                // pierwszym VM tabliczek; wlasny try (linia "Dziennik (m105): start")
                try { MapChatLog.ApplyAll(_harmony); } catch (Exception e) { Log.Error("MapChatLog.ApplyAll", e); }
                // ZC2 (16.10): linie "Estates (book)" i "Livelihood" w dymku osady (miasto, zamek - region, wies) - postfiks Low dymka gry i NavalDLC, PO GL1 (wstawia
                // pod jego linie); wlasny try, linie tylko przy kluczu DailyLifeEstates
                try { DailyLife.ApplyTip(_harmony); } catch (Exception e) { Log.Error("DailyLife.ApplyTip", e); }
                MarketRoad.ApplyAll(_harmony);   // wpis 100: wsie zamkowe woza plon na targ miasta, nie do zamku
                MarketCarts.ApplyAll(_harmony);  // poprawka 119: cena ladunku taboru sztuka po sztuce (wybor miasta wola MarketRoad.RoutePrefix)
                try { CastleMarket.ApplyAll(_harmony); } catch (Exception e) { Log.Error("CastleMarket.ApplyAll", e); }   // E5/B4 (199): targ przy zamku - sprzedaz w zamku (prefiks High po 119) i straz przed BK VillagersCastleTick
                RoadMemoryFix.ApplyAll(_harmony); // paczka 131: pamiec drog mapy ROT uzupelniona po wczytaniu mapy (sciany siatki bez wpisu sciana -> wejscie) - u zrodla, dla kazdej partii
                CartTownExit.ApplyAll(_harmony); // paczka 130: wozy wsi wyjezdzaja z osad, z ktorych straznik drog BK nie wypuszczal (bramy poza pamiecia drog ROT) - po RoadMemoryFix siatka bezpieczenstwa
                IslandRoads.ApplyAll(_harmony);  // paczka 154: wyspy - rozkazy BK bez drogi ladowej (ocena miast karawan BK, uczta, gentry; regula w strazniku BK wpieta przez CartTownExit)
                CaravanBulk.ApplyAll(_harmony);  // wpis 103: karawany woza surowce masowe wedle brakow miast, nie wedle indeksu ceny
                try { CaravanAmmo.ApplyAll(_harmony); } catch (Exception e) { Log.Error("CaravanAmmo.ApplyAll", e); }   // paczka 172b: karawany nie kupuja strzal i beltow (wycena BK, gabka BK)
                // K1: ksiega pieniadza i przeplywow osad - same postfiksy-liczniki (tylko log); we wlasnym try - jej wywrotka nie moze zatrzymac latek ponizej
                try { MoneyLedger.ApplyAll(_harmony); } catch (Exception e) { Log.Error("MoneyLedger.ApplyAll", e); }
                // 110 (etap 2 krok B): zawor kasy zamku - regulator gry dla zamku tylko w dol, "zakupy" ludnosci zamku bez zlota z niczego (te same cele co ksiega, postfiksy First); we wlasnym try
                try { CastlePurse.ApplyAll(_harmony); } catch (Exception e) { Log.Error("CastlePurse.ApplyAll", e); }
                try { TownPurse.ApplyAll(_harmony); } catch (Exception e) { Log.Error("TownPurse.ApplyAll", e); }   // E5/A1 (111'): kasy miast przez latki 110 (wyzej) + prefiks Last na BK HandleMarketGold; linia startowa
                // 112 (etap 2 krok B): utarg wsi bez znikania (powrot taboru, towar kupiony we wsi) - prefiks First / postfiks Last wokol latki BK; we wlasnym try
                try { VillageTakings.ApplyAll(_harmony); } catch (Exception e) { Log.Error("VillageTakings.ApplyAll", e); }
                try { MineRegale.ApplyAll(_harmony); } catch (Exception e) { Log.Error("MineRegale.ApplyAll", e); }   // R3-09f: regale gornicze - ladunek kruszcu wozow wsi przy sprzedazy
                // 3.2 (etap 3): cala kiesa rozbitej partii bez wodza (tabor wsi, banda, karawana) dla zwyciezcy - postfiks modelu lupu i podzialu MapEvent; we wlasnym try
                try { BeatenPurse.ApplyAll(_harmony); } catch (Exception e) { Log.Error("BeatenPurse.ApplyAll", e); }
                // E5/A6 (198): lup w bitwie tylko z kiesy pokonanego - migawka po 3.2 (postfiks Last), rozliczenie przed wyplata, obciazenie wodza w oknie ksiegi; we wlasnym try
                try { BattlePlunder.ApplyAll(_harmony); } catch (Exception e) { Log.Error("BattlePlunder.ApplyAll", e); }
                try { DefeatMorale.ApplyAll(_harmony); } catch (Exception e) { Log.Error("DefeatMorale.ApplyAll", e); }   // D11-1: kara morale za przegrana bitwe i runde o polowe (postfiksy modeli Default)
                try { ArmsLeaks.ApplyAll(_harmony); } catch (Exception e) { Log.Error("ArmsLeaks.ApplyAll", e); }   // paczka 174.0: kasowanie 5% stosow bez uzbrojenia (prefiks DeleteOverproducedItems; postfiks MoneyLedger biegnie dalej)
                try { UniqueShelf.ApplyAll(_harmony); } catch (Exception e) { Log.Error("UniqueShelf.ApplyAll", e); }   // A17 (poz. 4): unikaty zdjete z polki na czas konsumpcji osady (prefiks First / finalizer Last MakeConsumptionInTown)
                try { MaterialOrders.ApplyAll(_harmony); } catch (Exception e) { Log.Error("MaterialOrders.ApplyAll", e); }   // 174.2: BK ReleaseCaravanFromHold nie zmienia celu karawany z kontraktem
                AiGear.ApplyAll(_harmony);       // zakupy armii AI zamiast darmowego sprzetu DTE (Jeff 04.10)
                WorkshopLaw.ApplyAll(_harmony);  // warsztaty uzbrojenia jako firmy (Jeff 04.10)
                try { CraftRealm.ApplyAll(_harmony); } catch (Exception e) { Log.Error("CraftRealm.ApplyAll", e); }   // E6/M1 (207): okno kontekstu GetItemsToProduce dla lancucha krainy na drodze gry (klucz czytany na zywo)
                try { WorkshopInputs.ApplyAll(_harmony); } catch (Exception e) { Log.Error("WorkshopInputs.ApplyAll", e); }   // E6/M2 (208): bramka wsadu przy wyborze typu warsztatu (klucz czytany na zywo)
                try { NoTownMines.ApplyAll(_harmony); } catch (Exception e) { Log.Error("NoTownMines.ApplyAll", e); }   // N10 (noc 16.10): kopalnia BK w miescie -> najlepszy typ z cen targu (postfiks Low: po BK, przed licznikiem WorkshopInputs)
                try { GoodsPaths.ApplyEarly(_harmony); } catch (Exception e) { Log.Error("GoodsPaths.ApplyEarly", e); }   // ZC1: wsad gliny i wapna w ladunkach (ConsumeInputFromTownMarket, koszt w progu cyklu) - PRZED latkami cyklu warsztatu
                try { KsHides.ApplyEarly(_harmony); } catch (Exception e) { Log.Error("KsHides.ApplyEarly", e); }   // f4-ksiega-i-skory: skory z uboju rzeznikow wedlug tabeli swiata (postfiks GetItemsToProduce) - PRZED latkami cyklu warsztatu
                try { WorkshopTrade.ApplyAll(_harmony); } catch (Exception e) { Log.Error("WorkshopTrade.ApplyAll", e); }   // warsztaty towarowe w nowej monecie: utrzymanie i place do kas miast, cena z zarobku
                try { TownFletchers.ApplyAll(_harmony); } catch (Exception e) { Log.Error("TownFletchers.ApplyAll", e); }   // paczka 172: strzelarze miasta po warsztatach (postfiks doby miasta) i sonda linii arrows
                try { F7Workshops.ApplyAll(_harmony); } catch (Exception e) { Log.Error("F7Workshops.ApplyAll", e); }   // f7-browary K1: rzemioslo miasta po warsztatach (postfiks High doby warsztatow - przed strzelarzami; klucz czytany na zywo)
                Stables.ApplyAll(_harmony);
                // paczka 175: spis koni AI i straz konia Dothrakow (zamiany ROT), przewaga Polnocy w autobitwie, miara balansu krolestw;
                // wpinane zawsze - wylaczniki (Army175Measure, AiUpgradeHorseToArmory, Army175DothrakiHorseGuard, NorthHomeEdgePercent) czytane na zywo
                try { HorseCensus.ApplyAll(_harmony); } catch (Exception e) { Log.Error("HorseCensus.ApplyAll", e); }
                try { NorthHomeEdge.ApplyAll(_harmony); } catch (Exception e) { Log.Error("NorthHomeEdge.ApplyAll", e); }
                try { KingdomBalance.ApplyAll(_harmony); } catch (Exception e) { Log.Error("KingdomBalance.ApplyAll", e); }
                ShieldGuard.ApplyAll(_harmony);  // strzaly przestaja lupic tarcze (RBM liczy je x1.5)
                SpeedDepth.ApplyAll(_harmony);   // licznik zagniezdzenia - PRZED wszystkimi latkami predkosci/morale
                WorldPace.ApplyAll(_harmony);
                ManLedger.ApplyAll(_harmony);   // ksiega ludzi - kazdy ubytek z partii gracza z nazwa winowajcy
                WinterBite.ApplyAll(_harmony);
                HarvestYear.ApplyAll(_harmony);   // R3-05e: zywnosc wsi x rok urodzaju (klucz na zywo)
                WesterosClimate.ApplyAll(_harmony);
                OutlawLaw.ApplyAll(_harmony);
                try { LosersFlee.ApplyAll(_harmony); } catch (Exception e) { Log.Error("LosersFlee.ApplyAll", e); }   // H3: przegrani uchodza zamiast ginac (wynik bitwy AI w polu) + licznik wcielonych jencow AI
                try { PrisonerLaw.ApplyAll(_harmony); } catch (Exception e) { Log.Error("PrisonerLaw.ApplyAll", e); }   // I1: prawo jenca wedlug krainy - w krainie bez niewoli jeniec do domu / na Mur, nie niewolnik BK
                PopulationLaw.ApplyAll(_harmony);
                // E4/3 (108n): 1 czlowiek = 1/k osady (tabory wsi, lodzie rybackie, pobor wymuszony, zadania gracza); we wlasnym try - jej wywrotka nie moze zatrzymac latek ponizej
                try { PeopleUnit.ApplyAll(_harmony); } catch (Exception e) { Log.Error("PeopleUnit.ApplyAll", e); }
                MapClock.ApplyAll(_harmony);
                try { MapSpeed.ApplyAll(_harmony); } catch (Exception e) { Log.Error("MapSpeed.ApplyAll", e); }   // f9-predkosc: przyciski biegu x1 / x4 / x8 - postfiks MapTimeControlVM.Tick (po zegarze paska)
                Levy.ApplyAll(_harmony);
                try { PeopleLevy.ApplyAll(_harmony); } catch (Exception e) { Log.Error("PeopleLevy.ApplyAll", e); }   // GL-4 (P4): tylko Harmony - latki modelu milicji gry i BK zaklada PeopleLevy.EnsureHooks W KAMPANII (CLAUDE.md 7: pole statyczne GameTexts w modelu gry)
                VolunteerKit.ApplyAll(_harmony);
                try { GearFit.ApplyAll(_harmony); } catch (Exception e) { Log.Error("GearFit.ApplyAll", e); }   // GearFit (noc 15/16.10): komplet DTE tieru 1 (GetRecruitEquipments) i podloga broni w bitwie (DoAssignAsync, Priority.Last)
                LevyGold.ApplyAll(_harmony);
                try { GarrisonRecruitFee.ApplyAll(_harmony); } catch (Exception e) { Log.Error("GarrisonRecruitFee.ApplyAll", e); }   // E5 decyzja 9: oplata za werbunek zalog do sprzedajacego
                WinterSource.ApplyAll(_harmony);
                KingdomTreasury.ApplyAll(_harmony);
                try { CrownIncome.ApplyAll(_harmony); } catch (Exception e) { Log.Error("CrownIncome.ApplyAll", e); }   // 165: reparacje Diplomacy jako dlug korona-korona, splata dlugu wobec korony do skarbca
                try { SoldierPay.ApplyAll(_harmony); } catch (Exception e) { Log.Error("SoldierPay.ApplyAll", e); }   // zold do obiegu: sakiewki ludzi, kasy osad, zwrot ze skarbca
                WearKeep.ApplyAll(_harmony);
                HistoricalPrices.ApplyAll(_harmony);
                try { FixedSums38.ApplyAll(_harmony); } catch (Exception e) { Log.Error("FixedSums38.ApplyAll", e); }   // 3.8: okna wartosci w starej monecie (nagroda turnieju gry i TXP)
                RawPrice.ApplyAll(_harmony);     // cena surowcow od niedoboru: stala wzoru ceny w nowej monecie, popyt z prawdziwego zuzycia miasta
                try { TownFolk.ApplyAll(_harmony); } catch (Exception e) { Log.Error("TownFolk.ApplyAll", e); }   // E5/0 (195): okna pomiaru kiesy ludu - tylko odczyt (budzet dnia, wiersze polki BK, konsumpcja, bitwy, notable)
                try { TownFolk.ApplyCraftSale(_harmony); } catch (Exception e) { Log.Error("TownFolk.ApplyCraftSale", e); }   // N4 (poprawka po recenzji 16.10): do puli doplaty 35% tylko sztuki, ktore trafily na polke (po WorkshopTrade - wyrob do miasta wpiety wczesniej)
                try { TownFolk.ApplySkup(_harmony); } catch (Exception e) { Log.Error("TownFolk.ApplySkup", e); }   // GL-B / 11c: robocizna od skupu plonu wsi (zaplata kasy taborom wsi; klucz FolkEarnFromVillageBuys czytany na zywo)
                AmmoRecovery.ApplyAll(_harmony);
                try { ReinforcementThanks.ApplyAll(_harmony); } catch (Exception e) { Log.Error("ReinforcementThanks.ApplyAll", e); }   // n20-relacje: odsiecz DR - jedno okienko relacji zamiast okienka na lorda (transpiler RewardHelpers)
                DrMarchArrival.ApplyAll(_harmony);   // m97-posilki: tylko zapamietuje Harmony - latki na DR (Evaluate, ComputeArrivalTime) w kampanii (ArmouryBehavior.OnSessionLaunched)
                RecruitCost.ApplyAll(_harmony);
                try { RecruitSources.ApplyAll(_harmony); } catch (Exception e) { Log.Error("RecruitSources.ApplyAll", e); }   // 171: echo werbunku ROT, jency, autowerbunek zalog
                try { GarrisonArmory.ApplyAll(_harmony); } catch (Exception e) { Log.Error("GarrisonArmory.ApplyAll", e); }   // 171: sprzet idzie z ludzmi miedzy partia a zaloga i przy rozwiazaniu partii
                try { ArmsDrill.ApplyAll(_harmony); } catch (Exception e) { Log.Error("ArmsDrill.ApplyAll", e); }             // 171: cwiczenia wlasna bronia (latki modeli przy starcie kampanii)
                try { Drill.ApplyAll(_harmony); } catch (Exception e) { Log.Error("Drill.ApplyAll", e); }                     // musztra: tick treningu partii (zuzycie, kontrola gracza), ekrany zapasu cwiczebnego
                try { MountedWage.ApplyAll(_harmony); } catch (Exception e) { Log.Error("MountedWage.ApplyAll", e); }   // paczka 160: konny bierze wiekszy zold (zold jednostki, kontekst werbunku AI)
                StartKit.ApplyAll(_harmony);
                HeroGear.ApplyAll(_harmony);     // Z16: ekran ekwipunku - pancerz (Atletyka), strzaly (Luk), belty (Kusza) u bohaterow; linia Requires w opisie
                BuildFunding.ApplyAll(_harmony);
                FreeSupplies.ApplyAll(_harmony); // paczka 125: koniec dosypki drewna i narzedzi z niczego (RealisticBannerlord)
                try { RawNoRot.ApplyAll(_harmony); } catch (Exception e) { Log.Error("RawNoRot.ApplyAll", e); }   // 174.3: BK nie kasuje stosow rudy, metali, narzedzi, skory i plotna; drewno, len, welna 0.2%
                VillageWoodlot.ApplyAll(_harmony); // paczka 126: las wsi - drewno kazdej wsi bez drwali zamiast dosypki RBL (PO FreeSupplies: RblFeeds)
                try { Bloomery.ApplyAll(_harmony); } catch (Exception e) { Log.Error("Bloomery.ApplyAll", e); }   // E6/R1 (214): wytop przy kopalni - tick produkcji wsi (lupy wedlug drewna dymarki)
                try { HarvestValve.ApplyAll(_harmony); } catch (Exception e) { Log.Error("HarvestValve.ApplyAll", e); }   // E6/W3 (221): regulator wsi - pomiar produkcji w ticku wsi (PO lesie wsi i dymarce: postfiks Priority.Last)
                PopulationLaw.ApplyTownTax(_harmony);
                Rations.ApplyAll(_harmony);      // dlugi marsz, dlugie racje - zuzycie jedzenia w dol (gracz i AI)
                try { FoodChain.ApplyAll(_harmony); } catch (Exception e) { Log.Error("FoodChain.ApplyAll", e); }   // E4/6a: RBL liczy pory roku na wyniku lancucha ROT -> NavalDLC -> BEE -> BK (jeden lancuch modelu jedzenia)
                try { WinterRealm.ApplyBkFeed(_harmony); } catch (Exception e) { Log.Error("WinterRealm.ApplyBkFeed", e); }   // wariant (a) Jeffa 11.10: pasza BK po glowie (warunki terenu BK bez zmian)
                ScorchedEarth.ApplyAll(_harmony);
                // E4/4 (109n): przyrost naturalny wsi - ten sam cel i priorytet co blizna ScorchedEarth, wpiety PO niej (biegnie ostatni); podloga 10 (R17) - okno
                // na Village.DailyTick wpiete PRZED oknem W1 Measure40; we wlasnym try
                try { PopulationLaw.ApplyGrowth(_harmony); } catch (Exception e) { Log.Error("PopulationLaw.ApplyGrowth", e); }
                try { Measure40.ApplyAll(_harmony); } catch (Exception e) { Log.Error("Measure40.ApplyAll", e); }   // E4/0 (188): pomiar ludzi, jencow i zimy - okna wpiete zawsze (U26), licza przy PeopleMeasureEnabled; gra bez zmian
                try { OthersRise.ApplyAll(_harmony); } catch (Exception e) { Log.Error("OthersRise.ApplyAll", e); }
                try { OthersDuel.ApplyAll(_harmony); } catch (Exception e) { Log.Error("OthersDuel.ApplyAll", e); }   // A17 (poz. 10): pojedynek z Nocnym Krolem - wstaja tylko polegli tej bitwy (prefiks First MapEvent.FinalizeEventAux)
                // E4/7 (164b-1): ksiega pochodzenia - punkt zdjecia BK DeduceManpower, okna werbunku i przenosin, zywnosc wsi (rece); PO OthersRise (finalizer lupu jencow Last
                // po RescueFin High) i PO PrisonerLaw (prefiks powrotu patrolu BK Low); latki wpiete zawsze (U26), licza przy PeopleOriginEnabled
                try { PeopleBook.ApplyOrigin(_harmony); } catch (Exception e) { Log.Error("PeopleBook.ApplyOrigin", e); }   // E4/2 (190): Inni tylko z poleglych za Murem / przy Murze - latki wpiete zawsze, wylaczniki na zywo; PO Measure40 (finalizer (f) High przed W10)
                // E4/10 (167): karczma tylko z weteranow - L1 bez dosypki z niczego, okna najmu AI (ApplyInternal, Priority.First jak E4/7) i gracza; PO ApplyOrigin; we wlasnym try
                try { Veterans.ApplyAll(_harmony); } catch (Exception e) { Log.Error("Veterans.ApplyAll", e); }
                // E5/B6 (201): zaplata gracza za najem do sakiewki najetych - prefiks First / postfiks Last na BuyMercenaries i buy_mercenaries_on_consequence
                // (postfiks PO RecruitCost.PlayerBuyPostfix - kon z polki i zwrot doplaty, wpiety wyzej); we wlasnym try
                try { VetPurse.ApplyAll(_harmony); } catch (Exception e) { Log.Error("VetPurse.ApplyAll", e); }
                // E4/5 (113n): spustoszenie jako ulamek okregu - krok i koniec rabunku, progi produkcji, zywnosc wsi, produkcja wsi spladrowanej; wpiete zawsze (U26), PO przyroscie
                // naturalnym (czynne tylko razem z nim) i PO Measure40/OthersRise; MUSI zostac tutaj, w menu glownym - JIT wkleja male metody (GetHearthLevel), jesli wolajacy
                // skompiluje sie przed latka (113 ryzyko 13); we wlasnym try
                try { Devastation.ApplyAll(_harmony); } catch (Exception e) { Log.Error("Devastation.ApplyAll", e); }
                // E7/R1 (235): rabunek osada po osadzie dla AI - prefiks First na RaidEventComponent.Update (przed Devastation.RaidPrefix i oknem W6), koniec rabunku, czujnosc;
                // PO Devastation (StrikePeople, wspolny stan rabunku); wpiete zawsze, czynne przy kluczu RaidBySettlementEnabled
                try { DistrictRaid.ApplyAll(_harmony); } catch (Exception e) { Log.Error("DistrictRaid.ApplyAll", e); }
                try { Famine.ApplyAll(_harmony); } catch (Exception e) { Log.Error("Famine.ApplyAll", e); }   // E4/6 (191): spichlerze okregow i glod - postfiks spichlerza warowni (schronieni w murach oblezonej warowni)
                // E4/8 (164b-2): wojsko z ludzi - zalogi bez przyrostu z modelu, milicja z wolnych rak, nowe druzyny i karawany z ludzi, wedrowcy BK; PO PeopleBook.ApplyOrigin
                // (E4/7 - ksiega pochodzenia) i PO Measure40 (okna W1/W2 - nasz postfiks milicji biegnie przed ich finalizerami); latki wpiete zawsze (U26), licza przy kluczach
                try { ArmsFromPeople.ApplyAll(_harmony); } catch (Exception e) { Log.Error("ArmsFromPeople.ApplyAll", e); }
                try { VillageBook.ApplyAll(_harmony); } catch (Exception e) { Log.Error("VillageBook.ApplyAll", e); }   // E4/11 (193): zywnosc warowni od ludzi (postfiks DefaultSettlementFoodModel) - wpiety zawsze, liczy przy kluczu (U26)
                try { PeopleMarket.ApplyAll(_harmony); } catch (Exception e) { Log.Error("PeopleMarket.ApplyAll", e); }   // GL-2 (P2 e): neutralizacja licznika Food warowni (postfiks DefaultSettlementFoodModel) - wpiety zawsze, liczy przy kluczu MarketByPeople
                try { TownGranary.ApplyAll(_harmony); } catch (Exception e) { Log.Error("TownGranary.ApplyAll", e); }   // p6-spichlerz (R3-06f): bilans jedzenia miasta (postfiks Last DefaultSettlementFoodModel) i Town.DailyTick - wpiete zawsze, licza przy kluczu TownGranaryFromVillages
                // P4 (zbytki, targ widzi sakiewke): oferta ludu w szacunku popytu targu - prefiks / postfiks / finalizer na modelach gry i BetterEconomy (jak RawPrice, bez pol
                // statycznych z tekstow); wpiete zawsze, licza przy kluczach FolkLuxuryList i MarketSeesPurse
                try { TownFolk.LadderApplyAll(_harmony); } catch (Exception e) { Log.Error("TownFolk.LadderApplyAll", e); }
                // E4/9 (192): Straz nie pali wsi (postfiks Low na CampaignEventDispatcher.AiHourlyTick) i jency z rabunku z ludzi wsi (prefiks bool First na BK ExecuteCapture -
                // PO prefiksie okna W11 Measure40 (ten sam priorytet, wpiety wczesniej; W11 przy czynnym prawie nie liczy - BkCapPre), PRZED O12 CirculationWindows i RealPayer
                // (prefiksy void z parametrem referencyjnym - po naszym false pominiete, ich finalizery obsluguja stan domyslny); wpiete zawsze (U26), dzialaja przy kluczach; kazde we wlasnym try
                try { WatchLaw.ApplyAll(_harmony); } catch (Exception e) { Log.Error("WatchLaw.ApplyAll", e); }
                // WW0/WW3 (wyspy-wolni): decyzje AI najezdzcow (Zelazne Wyspy, Wolni Ludzie) - postfiks Low na tym samym AiHourlyTick co H1 (inna frakcja); wpiety zawsze, liczy zawsze, mnozy przy kluczu
                try { ReaverLaw.ApplyAll(_harmony); } catch (Exception e) { Log.Error("ReaverLaw.ApplyAll", e); }
                // OB5 (decyzja Jeffa 14.10, odsiecz z pierwszenstwem): postfiks Low na CampaignEventDispatcher.AiHourlyTick PO WatchLaw (widzi rabunek Strazy z wynikiem -1);
                // wpiety zawsze, dziala przy kluczu BesiegedReliefFirst
                try { BesiegedRelief.ApplyAll(_harmony); } catch (Exception e) { Log.Error("BesiegedRelief.ApplyAll", e); }
                try { ThrallLaw.ApplyAll(_harmony); } catch (Exception e) { Log.Error("ThrallLaw.ApplyAll", e); }
                try { HideoutSight.ApplyAll(_harmony); } catch (Exception e) { Log.Error("HideoutSight.ApplyAll", e); }   // m100-kryjowki: bramka wypatrzenia i miejsce nowych kryjowek
                Leaks310.ApplyAll(_harmony);   // 3.10: herszt kryjowki z wlasnych ludzi (latka metody gry); zold zaciagu ROT - w kampanii
                Wayfinder.ApplyAll(_harmony);
                // E6/K0 (209): licznik koni gry u zrodla (postfiks ItemRoster.OnRosterUpdated - liczy dopiero przy kluczu, flaga ustawiana w kampanii),
                // odswiezenie stanu koni partii po oplaconym awansie, pomiar koni porzuconych przez gre; PRZED MarchPace (ten czyta stan koni partii)
                try { HorseState.ApplyAll(_harmony); } catch (Exception e) { Log.Error("HorseState.ApplyAll", e); }
                // E6/K1 (211): okno DTE OnMapEventEnded (zgony koni autobitwy przed lupem, lup koni jako przeniesienie), zgony koni misji po zwrotach DTE (OnEndMission),
                // BK karawany (wyrzucony dobytek na polke), dary startowe nowej druzyny z polki siedziby (GiveInitialItemsToParty, Last - po pomiarze E4/8)
                try { HorseFate.ApplyAll(_harmony); } catch (Exception e) { Log.Error("HorseFate.ApplyAll", e); }
                // E6/K2 (212): gra nie handluje konmi lordow - kontekst PartiesBuyHorseCampaignBehavior.OnSettlementEntered, zakup BuyHorses i sprzedaz
                // wierzchowcow przez SellItemsAction.Apply pominiete w tym kontekscie (sprzedaz towaru zostaje); dziala przy kluczu AiSellsSpareHorses
                try { SpareHorses.ApplyAll(_harmony); } catch (Exception e) { Log.Error("SpareHorses.ApplyAll", e); }
                // E6/K5 (213): juczne nowych karawan z targu miasta startu (postfiks InitializeCaravanOnCreation + postfiks CreateCaravanParty VeryLow - po E4/8 Low,
                // przed finalizerami CaravanCapital/O17) i udzwig luzaka pod jukami (postfiks DefaultInventoryCapacityModel.CalculateInventoryCapacity); dziala przy kluczach
                try { PackMarket.ApplyAll(_harmony); } catch (Exception e) { Log.Error("PackMarket.ApplyAll", e); }
                // m97-oboz: woz taborowy i siodla transportowe - postfiks pojemnosci (jak PackMarket) i CalculateFinalSpeed wszystkich modeli predkosci (licznik glebokosci)
                try { CampCart.ApplyAll(_harmony); } catch (Exception e) { Log.Error("CampCart.ApplyAll", e); }
                // 131: udzwig z Endurance bohaterow (postfiks pojemnosci, osobna linia "Endurance")
                try { EnduranceCarry.ApplyAll(_harmony); } catch (Exception e) { Log.Error("EnduranceCarry.ApplyAll", e); }
                // m6b (4): juczne nowego taboru wsi ze stada albo z targu (postfiks InitializeVillagerPartyProperties), juczne konwoju DTE bez kopii do zbrojowni,
                // nazwane pozycje ksiegi koni (zapas drogi DTE i BK, konie przy RemoveParty) i towar partii usunietych bez zniszczenia (ksiega towarow, klucz FoodSourcesInLedger)
                try { PackSources.ApplyAll(_harmony); } catch (Exception e) { Log.Error("PackSources.ApplyAll", e); }
                // A17 (noc 17/18.10, stare zaleglosci): F1 konie partii BK bez daru z niczego, F2 ryby tylko z rak (KS) i polow zalogi w ksiedze, F4 wynik bitwy przerwany
                // wyjatkiem (finalizer CalculateAndCommitMapEventResults), F5 stocznia z drewna i za place; F3 (unikaty u band) - w OutlawLaw.Equip
                try { A17Fix.ApplyAll(_harmony); } catch (Exception e) { Log.Error("A17Fix.ApplyAll", e); }
                // ZC1 (16.10, drogi towarow): tick produkcji wsi (wielorybnicy, miod lesny, marmur glinanek, wegiel drwali), konwoje DTE i karawany BK bez jedzenia z niczego
                try { GoodsPaths.ApplyAll(_harmony); } catch (Exception e) { Log.Error("GoodsPaths.ApplyAll", e); }
                // m6b (6): kasy zaulkow - rozbicie zmiany kasy miast ponizej 1/4 zapasu kupcow na kanaly (tylko log)
                try { AlleyProbe.ApplyAll(_harmony); } catch (Exception e) { Log.Error("AlleyProbe.ApplyAll", e); }
                MarchPace.ApplyAll(_harmony);
                // SW-DROGI (noc 17/18.10): premia drogi ROT jedzie w latce MarchPace; tu tylko postfiks pamieci predkosci gry (MobileParty.IsLastSpeedCacheInvalid) i straznik zapisu stanu (CalculateSpeedForPartyUnified, getter SpeedExplained)
                try { MapRoads.ApplyAll(_harmony); } catch (Exception e) { Log.Error("MapRoads.ApplyAll", e); }
                // p6-sztormy (R3-05d): zima wiecej burz NavalDLC - postfiksy szansy narodzin i sufitu burz na NavalDLCStormModel, gettery listy burz (kara predkosci w latce MarchPace)
                try { SeaStorms.ApplyAll(_harmony); } catch (Exception e) { Log.Error("SeaStorms.ApplyAll", e); }
                // p6-drogi (R3-07d): AI wybiera drogi - postfiks MobileParty.ComputePath (objazd droga) i straznik ucieczki RecalculateLongTermPath; wpiete zawsze, dziala przy kluczu MapRoadAiRoutes
                try { AiRoadRoutes.ApplyAll(_harmony); } catch (Exception e) { Log.Error("AiRoadRoutes.ApplyAll", e); }
                TerrainEase.ApplyAll(_harmony);
                // NightRest PO MarchPace: kara snu ma ciac REALNA predkosc
                // kolumny (po suficie marszu), nie teoretyczna baze
                NightRest.ApplyAll(_harmony);
                // E7/O1 (228): sen do switu - prefiksy First na 10 MobileParty.SetMove* (spiacy w obozie / snem dlugu nie dostaje rozkazu ruchu; rozkaz czeka
                // na swit albo koniec snu dlugu); w menu glownym jak Wayfinder (przed JIT wolajacych), wpiete zawsze, dziala przy SleepersHoldOrders; we wlasnym try
                try { NightRest.ApplySleepGate(_harmony); } catch (Exception e) { Log.Error("NightRest.ApplySleepGate", e); }
                FletchForge.ApplyAll(_harmony);
                ForgeClock.ApplyAll(_harmony);   // wpis 83: jeden zegar kuzni
                try { ForgeJob.ApplyAll(_harmony); } catch (Exception e) { Log.Error("ForgeJob.ApplyAll", e); }   // m96-czaskuzni: jedna robota na ekran kuzni, jeden pasek po wyjsciu
                ArmourQuality.ApplyAll(_harmony); // zbroja z zakladki CRAFT jak bron: prog, pekniecie i jakosc z kowalstwa (PO FletchForge i ForgeClock)
                try { QualityScarcity.ApplyAll(_harmony); } catch (Exception e) { Log.Error("QualityScarcity.ApplyAll", e); }   // m96-jakosc: rzadkosc legendarnych i mistrzowskich (droga gry, lup gracza)
                SmeltTab.ApplyAll(_harmony);
                DressCode.ApplyAll(_harmony);
                // n20-herby (Jeff 19.10): ludzie band w bitwie z herbami prawdziwych rodow na tarczach (prefiks High na Mission.SpawnAgent; klucz Band Shields Stolen Arms)
                try { BandShields.ApplyAll(_harmony); } catch (Exception e) { Log.Error("BandShields.ApplyAll", e); }
                UniqueLaw.ApplyAll(_harmony);        // unikaty imienne: nabor do magazynow DTE zamienia kopie na zamienniki
                try { UniqueSpoils.ApplyAll(_harmony); } catch (Exception e) { Log.Error("UniqueSpoils.ApplyAll", e); }     // 177-2: przebranie bohatera przez gre zostawia unikaty i stal valyrianska w rekach
                try { ValyrianBlades.ApplyAll(_harmony); } catch (Exception e) { Log.Error("ValyrianBlades.ApplyAll", e); } // 177-2: przetop VS zablokowany, karawana DTE i nagroda turniejowa bez legend z niczego
                MountMeshGuard.ApplyAll(_harmony);   // uprzaz z cudzej rodziny = natywny crash w AddMountMesh
                CaptiveRags.ApplyAll(_harmony);
                SightRange.ApplyAll(_harmony);
                try { NightScout.ApplyAll(_harmony); } catch (Exception e) { Log.Error("NightScout.ApplyAll", e); }   // R3-10a: +5 XP Zwiadu za godzine nocnego marszu (postfiks OnTraverseTerrain), linia startu
                BkArmourList.ApplyAll(_harmony);
                BowStats.ApplyAll(_harmony);
                BattlefieldLaw.ApplyAll(_harmony);
                SpoilsSeal.ApplyAll(_harmony);   // paczka 128: Spoils of War - koniec sprzedazy automatycznej magazynu wojennego i zlota z niczego (platnik: kasa miasta / skarbiec klanu)
                Drobne39.ApplyAll(_harmony);     // 3.9: zlomowanie zbrojowni wojska gracza przez DTE zablokowane (linia startowa "Drobne (3.9)")
                SpoilsCompany.ApplyAll(_harmony);   // klan najemnikow Spoils tylko z prawdziwych zolnierzy (bez szablonu, dosypki i ochotnikow z mapy; zaplata za ludzi do kiesy klanu)
                try { BeeSeal.ApplyAll(_harmony); } catch (Exception e) { Log.Error("BeeSeal.ApplyAll", e); }   // paczka 170: BetterEconomy bez zlota w nicosc i bez towaru, ludzi, XP z niczego (akcje gracza zamkniete z powodem, bierne zrodla zatrzymane)
                BattleWind.ApplyAll(_harmony);
                // NB2 czesc 2 (16.10): weterani wedlug tieru - postfiks SetAiRelatedProperties po RBM (Priority.Last, after com.rbmai); wlasny try
                try { VeteranAi.ApplyAll(_harmony); } catch (Exception e) { Log.Error("VeteranAi.ApplyAll", e); }
                // m111-walka (24.10): haki postury w pojedynku (RBM calculate*PostureDamage, handleDefender/handleAttacker, HitStopLogic.TriggerHitStop, Artem OnAgentHit)
                try { DuelGuard.ApplyAll(_harmony); } catch (Exception e) { Log.Error("DuelGuard.ApplyAll", e); }
                BkSupplyTemper.ApplyAll(_harmony);
                try { BkSupplyMarket.ApplyAll(_harmony); } catch (Exception e) { Log.Error("BkSupplyMarket.ApplyAll", e); }   // 3.4: zakupy zaopatrzenia BK wedlug zapasu partii, zaplata do kasy osady (PRZED ksiega towarow)
                try { BkSeal34.ApplyAll(_harmony); } catch (Exception e) { Log.Error("BkSeal34.ApplyAll", e); }               // 3.4: rynek wsi BK, sprzedaz niewolnikow, wyposazenie rycerza BKROT - bez zlota z niczego (PRZED oknami ksiegi obiegu)
                DragonUnmount.ApplyAll(_harmony);
                SlowMuster.ApplyAll(_harmony);
                SlowHealing.ApplyAll(_harmony);
                // KA (16.10): karczmy - gojenie w dobrym domu PO SlowHealing (te same metody, priorytet nizszy) i ceny ekranu handlu w miescie noclegu; wlasny try
                try { TownInns.ApplyAll(_harmony); } catch (Exception e) { Log.Error("TownInns.ApplyAll", e); }
                HungerLaw.ApplyAll(_harmony);
                try { FoodThrift.ApplyAll(_harmony); } catch (Exception e) { Log.Error("FoodThrift.ApplyAll", e); }         // noc 15/16.10 (B3): syta partia AI nie przeplaca za jedzenie (te same modele co HungerLaw)
                try { FoodSubstitute.ApplyAll(_harmony); } catch (Exception e) { Log.Error("FoodSubstitute.ApplyAll", e); }   // noc 15/16.10 (B2): zamiennik jedzenia przy pustym straganie (UpdateDemandShift, PRZED nawiasem kiesy ludu TownFolk)
                try { NoMakerBudget.ApplyAll(_harmony); } catch (Exception e) { Log.Error("NoMakerBudget.ApplyAll", e); }   // noc 16.10 (N2-E2): budzet kategorii bez producenta na zbytki (UpdateDemandShift, priorytet 450 - PO B2, PRZED nawiasem kiesy ludu)
                try { CaravanGoods.ApplyAll(_harmony); } catch (Exception e) { Log.Error("CaravanGoods.ApplyAll", e); }   // noc-ekonomia3 (E3-T1, E3-G3): ksiega kosztu karawan i myto karawan na SellItemsAction.ApplyInternal, blokada odkupienia BK
                try { VillageFruit.ApplyAll(_harmony); } catch (Exception e) { Log.Error("VillageFruit.ApplyAll", e); }   // poprawka po recenzji noc-ekonomia2 16.10: magazyn wsi + 5 dob owocow, miodu i jaj z listy BK (GetWarehouseCapacity)
                try { StartCalibration.ApplyAll(_harmony); } catch (Exception e) { Log.Error("StartCalibration.ApplyAll", e); }   // KS (kalibracja startu): wies zastosowana bez zywnosci wsi gry z niczego (TickFoodProduction)
                try { FreeFolkRaids.ApplyAll(_harmony); } catch (Exception e) { Log.Error("FreeFolkRaids.ApplyAll", e); }   // fix-mur: Wolni Ludzie nie bija wlasnych band najazdu (postfiks ShouldConsiderAttacking; klucz Free Folk Raid Kin Peace)
                SkillsDecide.ApplyAll(_harmony);
                MusterOut.ApplyAll(_harmony);
                CraftPopup.ApplyAll(_harmony);
                try { ShipsToPort181.ApplyAll(_harmony); } catch (Exception e) { Log.Error("ShipsToPort181.ApplyAll", e); }   // 3.1/3.3 (33) 181: statki do portu (NavalDLC, ChangeShipOwnerAction, MapEvent) - E5/B5: wylaczniki domyslnie WL (porty placa z nadwyzki D4); WYL w MCM = sam pomiar; PRZED oknami ksiegi obiegu (okna O07/O08 maja Priority.First)
                // paczka 169: ksiega obiegu (tylko log) - okna pomiaru zlota; PO naszych latkach na te same metody (BuildFunding, WorkshopTrade,
                // MoneyLedger), PRZED ksiega towarow, ktora musi byc ostatnia; kazde okno wpinane we wlasnym try
                try { CirculationWindows.ApplyAll(_harmony); } catch (Exception e) { Log.Error("CirculationWindows.ApplyAll", e); }
                try { Seal31.ApplyAll(_harmony); } catch (Exception e) { Log.Error("Seal31.ApplyAll", e); }   // 3.1 (31): szczelnosc etapu 3 - PO oknach ksiegi (prefiksy zmieniajace zloto maja nizszy priorytet niz okna First)
                try { NotableGold.ApplyAll(_harmony); } catch (Exception e) { Log.Error("NotableGold.ApplyAll", e); }   // E5/A3 (164c): zloto notabli przez kase miasta handlu - PO oknach ksiegi (O03/O04 First) i PO Seal31
                try { SmallGold.ApplyAll(_harmony); } catch (Exception e) { Log.Error("SmallGold.ApplyAll", e); }   // E5/A7: drobne zrodla gry (rabunek z kiesy wsi, kiesa nowej zalogi, perki AI) - PO NotableGold (prefiksy Low na GiveGoldAction)
                try { BkDipGold.ApplyAll(_harmony); } catch (Exception e) { Log.Error("BkDipGold.ApplyAll", e); }   // E5/A8: BK i Diplomacy bez platnika albo odbiorcy (rod szlachty, herszt, uczta BK, koszt paktu Diplomacy) - PO SmallGold
                try { GentryNames.ApplyAll(_harmony); } catch (Exception e) { Log.Error("GentryNames.ApplyAll", e); }   // trop gentry (audyt modow 14.10, rozdz. 7): nazwa rodu BK wolna w calym swiecie, zapas z nazwy wsi - przy starcie gry, PRZED tworzeniem gentry w nowej kampanii
                try { Stage8World.ApplyAll(_harmony); } catch (Exception e) { Log.Error("Stage8World.ApplyAll", e); }   // E8/P3 (245): pomiary swiata (tylko log) - postfiksy DTE (kara Underequipped), prefiksy / postfiksy BK (GiveFood, FinancialCompromise, DrainGold, BuyGoods); niczego nie zmieniaja
                try { CaravanFlow.ApplyAll(_harmony); } catch (Exception e) { Log.Error("CaravanFlow.ApplyAll", e); }   // E8/G1 (260): postfiks na prefiksie BKROT (dlawik karawan tylko w drodze), pomiar tiku BK, postfiks BK ShouldHaveCaravan (lord nie kupuje karawany bez statkow w slepym zaulku); klucze czytane na zywo
                try { MercFeast.ApplyAll(_harmony); } catch (Exception e) { Log.Error("MercFeast.ApplyAll", e); }   // E8/G2 (261): najemnicy i uczta BK - bez nowych latek (okna SpoilsCompany i BkDipGold), odczyt metody szansy BK i linia "wpiete N/M"; PO SpoilsCompany i BkDipGold
                try { CraftOrders8.ApplyAll(_harmony); } catch (Exception e) { Log.Error("CraftOrders8.ApplyAll", e); }   // E8/K (255): zlecenia kowalskie gry (nagroda, platnik, bron, szablon) i kupno u ksiegarza BK
                try { CraftOrdersPurse.ApplyAll(_harmony); } catch (Exception e) { Log.Error("CraftOrdersPurse.ApplyAll", e); }   // NZ (16.10): postfiks CreateTownOrder - zlecenie tylko od zleceniodawcy z kiesa na sume swoich zlecen; PO CraftOrders8 (ta sama metoda ma jego transpiler)
                Measure169c.SetHarmony(_harmony);
                NightHooks.SetHarmony(_harmony);   // noc 25.10 P0 (K3): tylko zapamietanie - latki misji P1/P2 przy pierwszej bitwie (BattleSense.AfterStart), latki P3 w kampanii
                ClanBudget.SetHarmony(_harmony);    // 166: tylko zapamietanie - latki gry wpina EnsureHooks w kampanii
                OverflowHome.SetHarmony(_harmony);  // W4c: tylko zapamietanie - okno dezercji gry wpina EnsureHooks w kampanii
                GentryService.SetHarmony(_harmony); // 179: tylko zapamietanie - latki BK (wezwanie rycerza, partia rycerza, majatki) w kampanii
                Ransom178.SetHarmony(_harmony);      // 178: tylko zapamietanie - latki okupow i 1/9 w kampanii
                CrownCallToWar.SetHarmony(_harmony); // 168 dodatek: tylko zapamietanie - latki w kampanii
                FolkAidLoyalty.SetHarmony(_harmony); // P1b: tylko zapamietanie - latka na czynny model lojalnosci w kampanii (CLAUDE.md 7)
                WorldTime7.SetHarmony(_harmony);     // E7/K (226): tylko zapamietanie - latki turniejow, sojuszy i rozejmu gry w kampanii
                DebtLadder.SetHarmony(_harmony);    // 168: tylko zapamietanie - latki modelu finansow (portfele) i BK (B5) w kampanii
                CouncilWages35.SetHarmony(_harmony); // 3.5: tylko zapamietanie - latki BK (rada: platnik i urzad radnego) w kampanii
                FiefGrants.SetHarmony(_harmony);     // PROJEKT-LENNA (14.10): tylko zapamietanie - latki modelu BK / BKROT i decyzji "Owner of X" w kampanii
                BkrotChains.SetHarmony(_harmony);    // P4 audytu (AM-13, AM-09, AM-19): tylko zapamietanie - lancuchy modeli pod BKROT wpina EnsureHooks w kampanii (BKROTPatch laduje sie po Armoury)
                SideVillageFief.SetHarmony(_harmony);   // n20-wioska: tylko zapamietanie - latki (BK start, podpowiedz zlota) w kampanii
                EstateIncome187.SetHarmony(_harmony); // 3.1/3.3 (31b) E3/2: tylko zapamietanie - latki majatkow BK w kampanii
                Seal31B.SetHarmony(_harmony);        // 3.1/3.3 (31b) E3/4, E3/8, E3/9: tylko zapamietanie - latki gry i BK w kampanii
                PerfD2.SetHarmony(_harmony);         // perf-d2d3: tylko zapamietanie - prefiks podatku miast BK (PerfTownTaxSkipBk) w kampanii, po straznika
                MercTenure.SetHarmony(_harmony);    // M6a-1: tylko zapamietanie - latki najmu gry i BK w kampanii
                PeaceHold.SetHarmony(_harmony);     // M6a-3: tylko zapamietanie - latka pokoju gry w kampanii
                MercContract.SetHarmony(_harmony);  // 185: tylko zapamietanie - latki modelu finansow w kampanii   // 169c: tylko zapamietanie - okna pomiaru wpina EnsureHooks w kampanii
                try { CanonCompanions.ApplyAll(_harmony); } catch (Exception e) { Log.Error("CanonCompanions.ApplyAll", e); }   // m101-postacie: dworzanie bez druzyny (postfiks GetBestAvailableCommander, jak BK)
                // paczka 146: ksiega towarow (tylko log) - NA KONCU: ramki (prefiks + finalizer) na metodach, ktore wolaja metody juz
                // zalatane wyzej i przez BK, i podsluch ItemRoster.AddToCounts dopiero gdy wszystkie ramki sa wpiete
                try { GoodsLedger.ApplyAll(_harmony); } catch (Exception e) { Log.Error("GoodsLedger.ApplyAll", e); }
            }
            catch (Exception e) { Log.Error("OnBeforeInitialModuleScreenSetAsRoot", e); }
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void TipRegister() { MapVillageTooltip.Register(); }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void TipUnregister() { MapVillageTooltip.Unregister(); }

        /// <summary>E7/W2 (233): wyrejestrowanie typu dymka wioski przy wyladowaniu modulu.</summary>
        protected override void OnSubModuleUnloaded()
        {
            try { TipUnregister(); } catch { }
            Log.Close();   // E8/O6 (270): pisarze logu zamykani przy wyjsciu z gry (kazda linia jest juz w pliku - flush po linii; wlasny try w srodku)
            base.OnSubModuleUnloaded();
        }

        private static int _mapVillagesLoadFails;

        /// <summary>Wioski na mapie (W2): rejestracja komponentu widoku, gdy mapa kampanii ma juz swoje menedzery (tani test co klatke).
        /// Wylacznik sprawdzany TUTAJ: przy "Map Villages Enabled" = off typ MapVillagesView (dziedziczy po SandBox.View) w ogole
        /// sie nie laduje. Wywolanie przez osobna metode NoInlining: blad ladowania typu (inna wersja SandBox.View po aktualizacji
        /// gry) wylatuje wtedy w tym try, a nie z OnApplicationTick do petli modulow gry (CTD). Wszystko inne lapie EnsureRegistered.</summary>
        protected override void OnApplicationTick(float dt)
        {
            base.OnApplicationTick(dt);
            // E7/D0 (239): proba drog (tylko bieg zdjec autotestu, klucz MapRoadsProbeEnabled domyslnie false) - osobna rejestracja przed wylacznikiem
            // wiosek, ta sama ochrona przed bledem ladowania typu (po 3 probach proba drog nie startuje do konca sesji); przy false typ widoku sie nie laduje
            if (_mapRoadsLoadFails < 3 && MapRoadsProbe.On)
            {
                try { MapRoadsTick(); }
                catch (Exception e)
                {
                    _mapRoadsLoadFails++;
                    Log.Error("MapRoadsProbe: komponent widoku nie laduje sie (proba " + _mapRoadsLoadFails + "/3)", e);
                }
            }
            // f11-drogi: boczne drogi do dalszych wiosek (klucz MapSideRoads domyslnie NIE albo plik-przelacznik autotestu) - osobna rejestracja przed wylacznikiem
            // wiosek, ta sama ochrona przed bledem ladowania typu (po 3 probach drogi boczne nie startuja do konca sesji); przy NIE typ widoku sie nie laduje
            if (_sideRoadsLoadFails < 3 && MapSideRoads.On)
            {
                try { SideRoadsTick(); }
                catch (Exception e)
                {
                    _sideRoadsLoadFails++;
                    Log.Error("MapSideRoads: komponent widoku nie laduje sie (proba " + _sideRoadsLoadFails + "/3)", e);
                }
            }
            MapFields.AppTick();   // p6-pola (R3-07b): pola wokol wiosek wedlug pory roku - wlasna ochrona przed bledem ladowania typu (MapFields.cs); osobno od dopisow innych widokow (scalenie fali 2)
            // sw-zajazdy: zajazdy przy szlakach - osobna rejestracja widoku przed wylacznikiem wiosek, ta sama ochrona przed bledem ladowania typu (po 3 probach
            // zajazdy na mapie nie startuja do konca sesji; menu i plotki dzialaja dalej); przy kluczu RoadsideInns = off typ widoku sie nie laduje
            if (_innsLoadFails < 3 && Settings.Current.RoadsideInns)
            {
                try { RoadsideInnsTick(); }
                catch (Exception e)
                {
                    _innsLoadFails++;
                    Log.Error("RoadsideInns: komponent widoku nie laduje sie (proba " + _innsLoadFails + "/3)", e);
                }
            }
            // p6-szubienice (R3-07a): szubienice przy rozstajach - osobna rejestracja widoku przed wylacznikiem wiosek, ta sama ochrona przed bledem ladowania typu
            // (po 3 probach szubienice na mapie nie startuja do konca sesji; lista i zapis dzialaja dalej); przy kluczu MapGallows = off typ widoku sie nie laduje
            if (_gallowsLoadFails < 3 && Settings.Current.MapGallows)
            {
                try { GallowsTick(); }
                catch (Exception e)
                {
                    _gallowsLoadFails++;
                    Log.Error("Gallows: komponent widoku nie laduje sie (proba " + _gallowsLoadFails + "/3)", e);
                }
            }
            // m95-zajazd: straznik pobytu w zajezdzie (4 razy na sekunde; poza pobytem i przy wylaczonym kluczu - nic; bez typow SandBox.View, wlasny try w srodku) -
            // tu tylko blad ladowania typu, liczony (po 3 straznik nie startuje do konca sesji, gra bez zmian)
            if (_innStayFails < 3)
            {
                try { RoadsideInnStay.AppTick(); }
                catch (Exception e) { _innStayFails++; Log.Error("RoadsideInns.Stay: straznik nie laduje sie (proba " + _innStayFails + "/3)", e); }
            }
            // m103-sceny: straznik scen prologu 298 i okien opowiesci (4 razy na sekunde; zamkniecie okna bez przycisku, kolejka zwolnien obrazkow z plikow - w kazdej
            // kampanii; sceny tylko w kampanii z prologiem; wlasny try w srodku) - tu tylko blad ladowania typu (okno dziedziczy po typie ROT), liczony; po 3 straznik
            // nie startuje do konca sesji (gra bez zmian). PRZED returnem wiosek - inaczej klucz Map Villages = off wylaczylby sceny.
            if (_storyTickFails < 3)
            {
                try { StoryTick(); }
                catch (Exception e) { _storyTickFails++; Log.Error("Prologue.Scenes: straznik nie laduje sie (proba " + _storyTickFails + "/3)", e); }
            }
            // m105-kronika K0: straznik okien i list wyboru Kroniki Westeros (4 razy na sekunde, obok scen prologu; bez kampanii, bez okien w kolejce albo przy kluczu
            // Chronicle Of Westeros NIE - od razu wyjscie; wlasny try w srodku) - tu tylko blad ladowania typu, liczony; po 3 straznik nie startuje do konca sesji.
            // PRZED returnem wiosek - inaczej klucz Map Villages = off wylaczylby Kronike.
            if (_chronicleTickFails < 3)
            {
                try { ChronicleTick(); }
                catch (Exception e) { _chronicleTickFails++; Log.Error("Chronicle.AppTick: straznik nie laduje sie (proba " + _chronicleTickFails + "/3)", e); }
            }
            // m103-lektor: lektor - koniec klipu, kontrola 0,5 s, straznik okna, wyniki watku nagran plotek (wlasny try w srodku, nic nie gra - od razu wyjscie);
            // tu tylko blad ladowania typu, liczony (po 3 lektor nie startuje do konca sesji, gra bez zmian)
            if (_lektorFails < 3)
            {
                try { Lektor.AppTick(); }
                catch (Exception e) { _lektorFails++; Log.Error("Lektor.AppTick: nie laduje sie (proba " + _lektorFails + "/3)", e); }
            }
            // m108-muzyka: muzyka wydarzen - przez okno wydarzenia i strone plotek muzyka gry cichnie, gra nasz utwor (EventMusic; wlasny try w srodku,
            // nic nie gra i nic nie czeka - od razu wyjscie); tu tylko blad ladowania typu, liczony
            if (_musicFails < 3)
            {
                try { EventMusicTick(); }
                catch (Exception e) { _musicFails++; Log.Error("EventMusic: nie laduje sie (proba " + _musicFails + "/3)", e); }
            }
            // m105-dziennik: dziennik wiadomosci obok menu osady i ramka (4 razy na sekunde; poza taktem tylko licznik czasu; wlasny try w srodku) - tu tylko blad
            // ladowania typu, liczony (po 3 dziennik zostaje jak w grze do konca sesji). PRZED returnem wiosek - klucz Map Villages = off nie wylacza dziennika.
            if (_chatLogFails < 3)
            {
                try { ChatLogTick(dt); }
                catch (Exception e) { _chatLogFails++; Log.Error("MapChatLog: nie laduje sie (proba " + _chatLogFails + "/3)", e); }
            }
            CampWalk.TestTick();   // m109-oboz: test spaceru pod plikiem autotestu (armoury-oboz-spacer.flag; w grze zamyka sie po pierwszym sprawdzeniu, wlasny try)
            // m109-kuznia: autotest ekranu kucia (ForgeBillTest - tylko plik armoury-kuznia.flag; bez niego od razu wyjscie, wlasny try w srodku) - tu tylko blad
            // ladowania typu, liczony (po 3 test nie startuje do konca sesji). PRZED returnem wiosek.
            if (_forgeTestFails < 3)
            {
                try { ForgeTestTick(dt); }
                catch (Exception e) { _forgeTestFails++; Log.Error("ForgeBillTest.AppTick: nie laduje sie (proba " + _forgeTestFails + "/3)", e); }
            }
            // m110-ekwipunek: autotest podgladu postaci w ekwipunku (InventoryLookTest - tylko plik armoury-ekwipunek.flag; wlasny try w srodku) - tu tylko blad ladowania
            if (_invTestFails < 3)
            {
                try { InvTestTick(dt); }
                catch (Exception e) { _invTestFails++; Log.Error("InventoryLookTest.AppTick: nie laduje sie (proba " + _invTestFails + "/3)", e); }
            }
            // noc 25.10 P5: okno lupu po bitwie, oblezeniu i turnieju (SpoilsWindow.AppTick - bramka SceneGate, warstwa Gauntlet; wlasny try w srodku) - tu tylko blad
            // ladowania typu, liczony (po 3 okno lupu nie startuje do konca sesji, gra bez zmian). PRZED returnem wiosek - klucz Map Villages = off nie wylacza okna.
            if (_spoilsFails < 3)
            {
                try { SpoilsTick(); }
                catch (Exception e) { _spoilsFails++; Log.Error("SpoilsWindow.AppTick: nie laduje sie (proba " + _spoilsFails + "/3)", e); }
            }
            if (_mapVillagesLoadFails >= 3 || !Settings.Current.MapVillagesEnabled) return;
            try { MapVillagesTick(); }
            catch (Exception e)
            {
                // tu dochodzi tylko blad ladowania typu / zestawu (TypeLoad, MissingMethod, FileNotFound) - powtarzalby sie co klatke,
                // wiec po 3 probach wioski nie startuja do konca sesji (gra bez zmian); to nie jest blad jednej wioski
                _mapVillagesLoadFails++;
                Log.Error("MapVillages: komponent widoku nie laduje sie (proba " + _mapVillagesLoadFails + "/3"
                          + (_mapVillagesLoadFails >= 3 ? " - wioski na mapie wylaczone do konca sesji, gra bez zmian" : "") + ")", e);
            }
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void MapVillagesTick()
        {
            MapVillagesView.EnsureRegistered();
        }

        private static int _mapRoadsLoadFails;
        private static int _innsLoadFails;
        private static int _innStayFails;   // m95-zajazd: blad ladowania typu straznika pobytu (RoadsideInnStay.AppTick)
        private static int _storyTickFails;   // m103-sceny: blad ladowania typu straznika scen prologu (Prologue.ScenesAppTick)

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void StoryTick()
        {
            Prologue.ScenesAppTick();
        }

        private static int _chatLogFails;   // m105-dziennik: blad ladowania typu dziennika (MapChatLog.AppTick)

        private static int _musicFails;   // m108-muzyka: blad ladowania typu EventMusic

        private static int _forgeTestFails;   // m109-kuznia: blad ladowania typu testu ekranu kucia (ForgeBillTest.AppTick)
        private static int _invTestFails;     // m110-ekwipunek: blad ladowania typu testu podgladu postaci (InventoryLookTest.AppTick)

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void InvTestTick(float dt)
        {
            InventoryLookTest.AppTick(dt);
            FiefsShotTest.AppTick(dt);   // m112-inwest: autotest zakladki Fiefs (tylko plik armoury-fiefs-test.flag)
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void ForgeTestTick(float dt)
        {
            ForgeBillTest.AppTick(dt);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void EventMusicTick()
        {
            EventMusic.AppTick();
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void ChatLogTick(float dt)
        {
            MapChatLog.AppTick(dt);
        }

        private static int _chronicleTickFails;   // m105-kronika: blad ladowania typu straznika Kroniki (Chronicle.AppTick)
        private static int _spoilsFails;          // noc 25.10 P5: blad ladowania typu okna lupu (SpoilsWindow.AppTick)

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void SpoilsTick()
        {
            SpoilsWindow.AppTick();
            WindowPhotos.AppTick();   // paczka 132: proba zdjec nowych okien (tylko DLL autotestu i plik armoury-zdjecia-okien.flag; wlasny try, potem od razu wyjscie)
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void ChronicleTick()
        {
            Chronicle.AppTick();
        }

        /// <summary>m103-sceny: wyjscie z kampanii - okno opowiesci zerowane, obrazki z plikow zwolnione od razu (StoryPictures.ReleaseAll).</summary>
        public override void OnGameEnd(Game game)
        {
            base.OnGameEnd(game);
            try { RotWindowPictures.Reset(); } catch (Exception e) { Log.Error("OnGameEnd (obrazki okien ROT)", e); }
            try { Prologue.ScenesGameEnd(); } catch (Exception e) { Log.Error("OnGameEnd (sceny prologu, obrazki)", e); }
            try { MapChatLog.Reset(); } catch (Exception e) { Log.Error("OnGameEnd (dziennik m105)", e); }
            try { EventMusic.RestoreNow("wyjscie z kampanii"); } catch (Exception e) { Log.Error("OnGameEnd (muzyka m108)", e); }   // m108-muzyka: muzyka gry z powrotem   // m105-dziennik: linia kampanii i test od nowa
            try { Chronicle.GameEnd(); } catch (Exception e) { Log.Error("OnGameEnd (Kronika Westeros)", e); }   // m105-kronika: stan kampanii Kroniki od zera
            try { SpoilsWindow.Close("wyjscie z kampanii"); } catch (Exception e) { Log.Error("OnGameEnd (okno lupu)", e); }   // noc 25.10 P5: warstwa zdjeta, obrazek zwolniony
            try { SpoilsReport.Reset(); } catch (Exception e) { Log.Error("OnGameEnd (raport lupu)", e); }                     // noc 25.10 P5: raport w pamieci od zera
        }
        private static int _lektorFails;    // m103-lektor: blad ladowania typu lektora (Lektor.AppTick)

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void RoadsideInnsTick()
        {
            RoadsideInnsView.EnsureRegistered();
        }

        private static int _gallowsLoadFails;

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void GallowsTick()
        {
            GallowsView.EnsureRegistered();
        }

        private static int _sideRoadsLoadFails;

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void SideRoadsTick()
        {
            MapSideRoadsView.EnsureRegistered();
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void MapRoadsTick()
        {
            MapRoadsProbeView.EnsureRegistered();
        }

        public override void OnMissionBehaviorInitialize(Mission mission)
        {
            base.OnMissionBehaviorInitialize(mission);
            try
            {
                // F1 (H3 w autobitwie gracza): bitwa gracza z misja nie jest autobitwa, nawet gdy potem "wyslij wojsko"
                if (Campaign.Current != null) LosersFlee.NoteMission();
                // E6/K1 (211): zgony koni w misji (agent konia zabity) - zdjete ze zbrojowni wlasciciela po zwrotach DTE; bitwa z misja bez reguly W autobitwy
                if (Campaign.Current != null && mission != null) HorseFate.OnMissionInit(mission);
                // n20-drobne (recenzja): pomiar - kto w bitwie gracza nosi bron Dothrakow (klucz Dothraki Arms Census Log; tylko log)
                if (Campaign.Current != null && mission != null) DothrakiArms.OnMissionInit(mission);
                if (Settings.Current.FieldCraftEnabled && Campaign.Current != null && mission != null)
                    mission.AddMissionBehavior(new FieldCraft());
                // 136 (25.10): stamina koni - tylko bitwa gracza na mapie (turniej, arena, cwiczenia, spacer - nie; rodzaj misji i morze sprawdza HorseWind.AfterStart).
                // Za FieldCraft i przed BattleSense: srednia S szykow z tego taktu czyta potem MarchToContact (odpoczynek konnicy AI)
                if (Settings.Current.HorseStaminaEnabled && Campaign.Current != null && mission != null && HorseWind.Wanted(mission))
                    mission.AddMissionBehavior(new HorseWind());
                // m110-stan (LootStateFromWounds): ksiega obrazen czesci zbroi wrogow w bitwie - stan lupu z prawdziwych obrazen (bez latki, sama misja)
                if (LootWear.On && Campaign.Current != null && mission != null)
                    mission.AddMissionBehavior(new LootWear.Logic());
                // m110-stan krok 2: ochrona z dokladnego HP i zuzycie sprzetu towarzyszy - pomiar haka testu, przeciek kopii i koniec misji (kopie robi postfiks spawnu)
                if (Campaign.Current != null && mission != null && (Settings.Current.WornExactHpProtection || Settings.Current.CompanionGearWear))
                    mission.AddMissionBehavior(new HeroHp.Logic());
                // n20-herby: linia "Tarcze band (herby):" po rozstawieniu i na koniec misji (sam herb idzie w prefiksie SpawnAgent)
                if (Settings.Current.BandShieldsStolenArms && Campaign.Current != null && mission != null)
                    mission.AddMissionBehavior(new BandShieldsWatch());
                if (Settings.Current.HitScribeEnabled && mission != null)
                    mission.AddMissionBehavior(new HitScribe());
                if (Settings.Current.AutoParryEnabled && mission != null)
                    mission.AddMissionBehavior(new GuardMaster());
                if (Settings.Current.WoundedFleeEnabled && mission != null)
                    mission.AddMissionBehavior(new BrokenMen());
                // NB2 (16.10): zlamani raz na bitwe (BrokenMen + FieldCraft przez RoutHold.Break) i linia "Bitwa (NB2)" z postura wedlug tieru
                // czesc 2: linia NB2 takze dla taktyki wedlug dowodcy, weteranow wedlug tieru, krwi przez pancerz i glosow (BattleSense ponizej)
                bool nb2b = Settings.Current.CommanderTactics || Settings.Current.VeteransByTier || Settings.Current.BloodThroughArmour
                            || Settings.Current.BattleCriesAndMoans || Settings.Current.GroundBloodPools   // czesc 3 (16.10): kaluze krwi
                            || BattleSense.NightKeys(Settings.Current);   // noc 25.10 P0 (K2): marsz i doktryny (P1), dzwiek (P2), choragwie (P3) - BattleSense i RoutKeeper (linia NB2, RoutHold.On)
                if (mission != null && ((Settings.Current.BrokenMenNeverRally && (Settings.Current.WoundedFleeEnabled
                        || (Settings.Current.FieldCraftEnabled && Settings.Current.AiFleeWhenNearDeath && Campaign.Current != null)))
                        || Settings.Current.SoldierPostureByTier || nb2b))
                    mission.AddMissionBehavior(new RoutKeeper());
                // m111-walka (24.10): BattleSense niesie tez flage pojedynku (turniej, arena, pojedynek ROT) dla DuelAi / DuelCraft / DuelGuard / BattleWind
                // m113-walka2 (24.10): i dla rozszerzen bitwy fazy 2 (bohaterowie-pojedynkowicze, tier z umiejetnosci, roznica umiejetnosci gracza, zrywy)
                // po recenzji: takze przy fladze testu bitwaOFF (FieldDuel.TestOff, tylko DLL autotestu) - linia misji z kluczami NIE
                if (mission != null && (nb2b || DuelGuard.AnyKey(Settings.Current) || FieldDuel.AnyKey(Settings.Current) || FieldDuel.TestOff))
                    mission.AddMissionBehavior(new BattleSense());
                // noc 25.10 P3 (krytyk K2): choragwie - kompanie, wielu chorazych, sztandar polowy, zwrot sprzetu chorazego; samo sprawdza bitwe polowa i logike sztandarow
                if ((Settings.Current.BannerCompaniesEnabled || Settings.Current.StandardBearerKitSafe || Settings.Current.FieldStandards)
                    && Campaign.Current != null && mission != null)
                    mission.AddMissionBehavior(new BannerCompaniesMission());
                // noc 25.10 P4: linia kontrolna przegladu armii (formacje gracza wobec Order of Battle; tylko odczyt) - samo sprawdza bitwe polowa gracza
                if (Settings.Current.ArmyReviewEnabled && Campaign.Current != null && mission != null)
                    mission.AddMissionBehavior(new ArmyReviewCheck());
                // noc 25.10 P6: bramka friendly fire (pole, oblezenie, kryjowka, morze - liczona raz w EarlyStart; turniej i arena poza)
                if (Settings.Current.FriendlyFireEnabled && Campaign.Current != null && mission != null)
                    mission.AddMissionBehavior(new FriendlyFire.Logic());
                if (Settings.Current.CampBattlePropsEnabled && Campaign.Current != null && mission != null
                    && (NightRest.PlayerCamped || CampScene.TestWanted()))   // m109-oboz: test autotestu (plik armoury-oboz-bitwa.flag) - kazda bitwa w polu jak w obozie
                    mission.AddMissionBehavior(new CampScene());
                if (Settings.Current.HideoutAlarmEnabled && Campaign.Current != null && mission != null
                    && HideoutAlarm.IsHideout(mission))
                    mission.AddMissionBehavior(new HideoutAlarm());
                if (Settings.Current.HideoutArmouryGear && Campaign.Current != null && mission != null
                    && HideoutAlarm.IsHideout(mission)
                    && mission.GetMissionBehavior<IMissionAgentSpawnLogic>() == null)
                {
                    mission.AddMissionBehavior(new HideoutSpawnShim());
                    Log.Info("HideoutSpawnShim: brama DTE otwarta - wojsko idzie do kryjowki w sprzecie z magazynu.");
                }
            }
            catch (Exception e) { Log.Error("OnMissionBehaviorInitialize", e); }
        }

        /// <summary>m95-menu: po OnSessionLaunched i OnAfterSessionLaunched wszystkich zachowan (Campaign.DoLoadingForGameType -> GameManager), przed pierwszym
        /// pokazaniem menu - porzadek menu miasta (TownMenus.Apply, klucz TownMenuTidy). Nowa kampania i wczytanie zapisu - ta sama droga.</summary>
        public override void OnAfterGameInitializationFinished(Game game, object starterObject)
        {
            base.OnAfterGameInitializationFinished(game, starterObject);
            try
            {
                if (game != null && game.GameType is Campaign && Campaign.Current != null)
                    TownMenus.Apply(starterObject as CampaignGameStarter);
            }
            catch (Exception e) { Log.Error("OnAfterGameInitializationFinished (TownMenus)", e); }
            try
            {
                if (game != null && game.GameType is Campaign && Campaign.Current != null)
                    CastleMenus.Apply(starterObject as CampaignGameStarter);   // m96-zamek: porzadek menu zamku (klucz CastleMenuTidy), ta sama chwila co miasto
            }
            catch (Exception e) { Log.Error("OnAfterGameInitializationFinished (CastleMenus)", e); }
            try
            {
                if (game != null && game.GameType is Campaign && Campaign.Current != null)
                    VillageMenus.Apply(starterObject as CampaignGameStarter);   // m96-wies: porzadek menu wsi (klucz VillageMenuTidy), ta sama chwila co miasto i zamek
            }
            catch (Exception e) { Log.Error("OnAfterGameInitializationFinished (VillageMenus)", e); }
            try
            {
                if (game != null && game.GameType is Campaign && Campaign.Current != null)
                    PayNotes.StartLine();   // m96-wies: komunikaty platnosci z powodem (klucz PlayerPaymentReasons) - liczniki od zera, linia startu
            }
            catch (Exception e) { Log.Error("OnAfterGameInitializationFinished (PayNotes)", e); }
        }

        /// <summary>m97-surowce: kategoria arm_rare_stuff (czardrzewo, smocza kosc) przed wczytaniem XML przedmiotow (arm_items_rare.xml) - ta sama chwila co NavalDLC.</summary>
        public override void InitializeSubModuleGameObjects(Game game)
        {
            base.InitializeSubModuleGameObjects(game);
            RareMaterials.RegisterCategory(game);
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);
            try
            {
                var starter = gameStarterObject as CampaignGameStarter;
                if (starter == null || !(game.GameType is Campaign)) return;
                McmSettings.Apply();
                // m101-prolog: wybor startu z menu glownego przypiety do tej kampanii PRZED zegarem kampanii (model czasu czyta Prologue.CalendarShifted)
                Prologue.OnGameStart(game);
                // dlugosc roku MUSI wejsc, zanim gra przeliczy swoj zegar
                LongYearTimeModel.Install(gameStarterObject);
                TierDesertionModel.Install(starter);   // prawo dezercji (Jeff 17.09) - patrz DesertionLaw.cs
                starter.AddBehavior(new ArmouryBehavior());
                starter.AddBehavior(new Uniques());
                starter.AddBehavior(new SmithWoodBehavior());   // Jeff 15.10: drewno w kuzni w kg - zapas otwartego ladunku w zapisie
                starter.AddBehavior(new HideoutPurge());
                starter.AddBehavior(new BattleMuster());
                starter.AddBehavior(new LegendaryLaw());
                starter.AddBehavior(new UniqueLaw());
                starter.AddBehavior(new DesertionLaw());
                starter.AddBehavior(new MusterBook());
                starter.AddBehavior(new HideoutSpotter());
                starter.AddBehavior(new HideoutSight());       // m100-kryjowki: wypatrzenie ze Zwiadu, plotka odslania, miejsce nowych kryjowek (zapis arm_hideout_sight)
                starter.AddBehavior(new HouseLevies());
                starter.AddBehavior(new WorkshopTradeBehavior());   // warsztaty towarowe: linia dnia, rozmowa kupna, srednie zysku w zapisie
                starter.AddBehavior(new TownCraftsBehavior());      // paczka 148: rzemioslo miasta wedle wartosci (doba miasta, stan w zapisie)
                starter.AddBehavior(new TownFletchersBehavior());   // paczka 172: strzelarze miasta - stan w zapisie (doba miasta przez latke po warsztatach)
                starter.AddBehavior(new HeroGearBehavior());        // Z16: samotest ekranu przy wczytaniu (tylko log, bez zapisu)
                starter.AddBehavior(new QualityScarcityBehavior()); // m96-jakosc: spis jakosci raz na dobe, wyrownanie nadmiaru na polkach, linia logu; zapis: limity rodzajow arm_qualitycaps (m96-jakosc2)
                starter.AddBehavior(new A16Behavior());             // A16: linie dobrobytu, bohaterow i ludnosci BK; start sesji - wylacznik warstwy BK
                starter.AddBehavior(new ReliefBehavior());          // OB5 odsiecz oblezonych twierdz (linie, stan oblezen) i S11 rody z lennem zmieniajace krolestwo (linia)
                starter.AddBehavior(new BookHoldingsBehavior());    // f19-kanon-b: jednorazowe przywrocenie wlosci z ksiazki (flaga arm_book_holdings w zapisie)
                starter.AddBehavior(new CanonCompanionsBehavior()); // m101-postacie: postacie kanonu do rodow, usuniete, zmiany stron (flaga arm_canon_companions, tylko nowa kampania)
                starter.AddBehavior(new PrologueBehavior());        // m101-prolog: wybor startu w zapisie (arm_prologue), zdarzenia prologu 298
                starter.AddBehavior(new CanonWarsBehavior());       // m104-wojny: wojny wedlug ksiazki - Essos na kalendarzu, Reach i Dorne z korona, smoki Daenerys (zapis arm_m104)
                starter.AddBehavior(new ChronicleBehavior());       // m105-kronika K0: Kronika Westeros - okna z arm_kronika_kNN.tsv (zapis arm_kronika); PO CanonCompanions (doba po dobie postaci kanonu)
                Log.Info("Behavior dodany do kampanii.");
            }
            catch (Exception e) { Log.Error("OnGameStart", e); }
        }
    }
}
