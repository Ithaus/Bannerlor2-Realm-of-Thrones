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
        }

        /// <summary>Modele lataamy dopiero gdy wszystkie mody sa zaladowane.</summary>
        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();
            try
            {
                if (_modelsPatched) return;
                _modelsPatched = true;
                if (_harmony == null) _harmony = new Harmony(HarmonyId);
                WeaponXpPatch.ApplyAll(_harmony);
                ScrapFloorPatch.ApplyAll(_harmony);
                BkPenaltyOnce.ApplyAll(_harmony);    // cena sprzedazy sprzetu (A): kara handlowa BK raz (x5) - dubel postfiksu BK zdjety z modelu, ktory wola base; BK wpina go wczesniej, w OnSubModuleLoad
                SellByCondition.ApplyAll(_harmony);  // cena sprzedazy sprzetu (B): ksiega skupu (tylko log) - kto sprzedaje: ekran handlu gracza, SellItemsAction
                TrueArmourCost.ApplyAll(_harmony);
                ThrownWobblePatch.ApplyAll(_harmony);
                FairXpPatch.ApplyAll(_harmony);
                DonationXpLaw.ApplyAll(_harmony);   // audyt 13 Z1 (Jeff 09.10): oddany sprzet nie uczy - XP perkow Giving Hands / Paid in Promise = 0, migawka handlu BK przy kazdym otwarciu handlu
                ChargeTemperPatch.ApplyAll(_harmony);
                CastleSteel.ApplyAll(_harmony);   // 177-1: wedrowcy BK bez sztab i wegla z niczego (dawny ValyrianSteel.DearRefine zdjety - stal zamkowa to zwykly stopien)
                SmithAudit.ApplyAll(_harmony);
                QuartermasterLaw.ApplyAll(_harmony);
                try { GarrisonKit.ApplyAll(_harmony); } catch (Exception e) { Log.Error("GarrisonKit.ApplyAll", e); }   // K1 (A10): zaloga w bitwie gracza walczy sprzetem ze swojej zbrojowni (latka DTE)
                MarketGlut.ApplyAll(_harmony);
                SupplyDemand.ApplyAll(_harmony); // prawo podazy i popytu dla uzbrojenia - PO MarketGlut (Jeff 04.10)
                MaterialLaw.ApplyAll(_harmony);  // surowce: przetopy, XP przetopu, wydobycie (Jeff 04.10)
                MineralOnce.ApplyAll(_harmony);  // lista produkcji wsi BK bez powtorzen: mineral wsi gorniczej dopisywany raz (Jeff 05.10); MUSI isc po MaterialLaw.ApplyAll (zdjety wpis oddaje jego postfix modelu)
                MarketRoad.ApplyAll(_harmony);   // wpis 100: wsie zamkowe woza plon na targ miasta, nie do zamku
                MarketCarts.ApplyAll(_harmony);  // poprawka 119: cena ladunku taboru sztuka po sztuce (wybor miasta wola MarketRoad.RoutePrefix)
                RoadMemoryFix.ApplyAll(_harmony); // paczka 131: pamiec drog mapy ROT uzupelniona po wczytaniu mapy (sciany siatki bez wpisu sciana -> wejscie) - u zrodla, dla kazdej partii
                CartTownExit.ApplyAll(_harmony); // paczka 130: wozy wsi wyjezdzaja z osad, z ktorych straznik drog BK nie wypuszczal (bramy poza pamiecia drog ROT) - po RoadMemoryFix siatka bezpieczenstwa
                IslandRoads.ApplyAll(_harmony);  // paczka 154: wyspy - rozkazy BK bez drogi ladowej (ocena miast karawan BK, uczta, gentry; regula w strazniku BK wpieta przez CartTownExit)
                CaravanBulk.ApplyAll(_harmony);  // wpis 103: karawany woza surowce masowe wedle brakow miast, nie wedle indeksu ceny
                try { CaravanAmmo.ApplyAll(_harmony); } catch (Exception e) { Log.Error("CaravanAmmo.ApplyAll", e); }   // paczka 172b: karawany nie kupuja strzal i beltow (wycena BK, gabka BK)
                // K1: ksiega pieniadza i przeplywow osad - same postfiksy-liczniki (tylko log); we wlasnym try - jej wywrotka nie moze zatrzymac latek ponizej
                try { MoneyLedger.ApplyAll(_harmony); } catch (Exception e) { Log.Error("MoneyLedger.ApplyAll", e); }
                // 110 (etap 2 krok B): zawor kasy zamku - regulator gry dla zamku tylko w dol, "zakupy" ludnosci zamku bez zlota z niczego (te same cele co ksiega, postfiksy First); we wlasnym try
                try { CastlePurse.ApplyAll(_harmony); } catch (Exception e) { Log.Error("CastlePurse.ApplyAll", e); }
                try { ArmsLeaks.ApplyAll(_harmony); } catch (Exception e) { Log.Error("ArmsLeaks.ApplyAll", e); }   // paczka 174.0: kasowanie 5% stosow bez uzbrojenia (prefiks DeleteOverproducedItems; postfiks MoneyLedger biegnie dalej)
                try { MaterialOrders.ApplyAll(_harmony); } catch (Exception e) { Log.Error("MaterialOrders.ApplyAll", e); }   // 174.2: BK ReleaseCaravanFromHold nie zmienia celu karawany z kontraktem
                AiGear.ApplyAll(_harmony);       // zakupy armii AI zamiast darmowego sprzetu DTE (Jeff 04.10)
                WorkshopLaw.ApplyAll(_harmony);  // warsztaty uzbrojenia jako firmy (Jeff 04.10)
                try { WorkshopTrade.ApplyAll(_harmony); } catch (Exception e) { Log.Error("WorkshopTrade.ApplyAll", e); }   // warsztaty towarowe w nowej monecie: utrzymanie i place do kas miast, cena z zarobku
                try { TownFletchers.ApplyAll(_harmony); } catch (Exception e) { Log.Error("TownFletchers.ApplyAll", e); }   // paczka 172: strzelarze miasta po warsztatach (postfiks doby miasta) i sonda linii arrows
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
                WesterosClimate.ApplyAll(_harmony);
                OutlawLaw.ApplyAll(_harmony);
                try { LosersFlee.ApplyAll(_harmony); } catch (Exception e) { Log.Error("LosersFlee.ApplyAll", e); }   // H3: przegrani uchodza zamiast ginac (wynik bitwy AI w polu) + licznik wcielonych jencow AI
                try { PrisonerLaw.ApplyAll(_harmony); } catch (Exception e) { Log.Error("PrisonerLaw.ApplyAll", e); }   // I1: prawo jenca wedlug krainy - w krainie bez niewoli jeniec do domu / na Mur, nie niewolnik BK
                PopulationLaw.ApplyAll(_harmony);
                MapClock.ApplyAll(_harmony);
                Levy.ApplyAll(_harmony);
                VolunteerKit.ApplyAll(_harmony);
                LevyGold.ApplyAll(_harmony);
                WinterSource.ApplyAll(_harmony);
                KingdomTreasury.ApplyAll(_harmony);
                try { SoldierPay.ApplyAll(_harmony); } catch (Exception e) { Log.Error("SoldierPay.ApplyAll", e); }   // zold do obiegu: sakiewki ludzi, kasy osad, zwrot ze skarbca
                WearKeep.ApplyAll(_harmony);
                HistoricalPrices.ApplyAll(_harmony);
                RawPrice.ApplyAll(_harmony);     // cena surowcow od niedoboru: stala wzoru ceny w nowej monecie, popyt z prawdziwego zuzycia miasta
                AmmoRecovery.ApplyAll(_harmony);
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
                PopulationLaw.ApplyTownTax(_harmony);
                Rations.ApplyAll(_harmony);      // dlugi marsz, dlugie racje - zuzycie jedzenia w dol (gracz i AI)
                ScorchedEarth.ApplyAll(_harmony);
                Wayfinder.ApplyAll(_harmony);
                MarchPace.ApplyAll(_harmony);
                TerrainEase.ApplyAll(_harmony);
                // NightRest PO MarchPace: kara snu ma ciac REALNA predkosc
                // kolumny (po suficie marszu), nie teoretyczna baze
                NightRest.ApplyAll(_harmony);
                FletchForge.ApplyAll(_harmony);
                ForgeClock.ApplyAll(_harmony);   // wpis 83: jeden zegar kuzni
                ArmourQuality.ApplyAll(_harmony); // zbroja z zakladki CRAFT jak bron: prog, pekniecie i jakosc z kowalstwa (PO FletchForge i ForgeClock)
                SmeltTab.ApplyAll(_harmony);
                DressCode.ApplyAll(_harmony);
                UniqueLaw.ApplyAll(_harmony);        // unikaty imienne: nabor do magazynow DTE zamienia kopie na zamienniki
                try { UniqueSpoils.ApplyAll(_harmony); } catch (Exception e) { Log.Error("UniqueSpoils.ApplyAll", e); }     // 177-2: przebranie bohatera przez gre zostawia unikaty i stal valyrianska w rekach
                try { ValyrianBlades.ApplyAll(_harmony); } catch (Exception e) { Log.Error("ValyrianBlades.ApplyAll", e); } // 177-2: przetop VS zablokowany, karawana DTE i nagroda turniejowa bez legend z niczego
                MountMeshGuard.ApplyAll(_harmony);   // uprzaz z cudzej rodziny = natywny crash w AddMountMesh
                CaptiveRags.ApplyAll(_harmony);
                SightRange.ApplyAll(_harmony);
                BkArmourList.ApplyAll(_harmony);
                BowStats.ApplyAll(_harmony);
                BattlefieldLaw.ApplyAll(_harmony);
                SpoilsSeal.ApplyAll(_harmony);   // paczka 128: Spoils of War - koniec sprzedazy automatycznej magazynu wojennego i zlota z niczego (platnik: kasa miasta / skarbiec klanu)
                SpoilsCompany.ApplyAll(_harmony);   // klan najemnikow Spoils tylko z prawdziwych zolnierzy (bez szablonu, dosypki i ochotnikow z mapy; zaplata za ludzi do kiesy klanu)
                try { BeeSeal.ApplyAll(_harmony); } catch (Exception e) { Log.Error("BeeSeal.ApplyAll", e); }   // paczka 170: BetterEconomy bez zlota w nicosc i bez towaru, ludzi, XP z niczego (akcje gracza zamkniete z powodem, bierne zrodla zatrzymane)
                BattleWind.ApplyAll(_harmony);
                BkSupplyTemper.ApplyAll(_harmony);
                DragonUnmount.ApplyAll(_harmony);
                SlowMuster.ApplyAll(_harmony);
                SlowHealing.ApplyAll(_harmony);
                HungerLaw.ApplyAll(_harmony);
                SkillsDecide.ApplyAll(_harmony);
                MusterOut.ApplyAll(_harmony);
                CraftPopup.ApplyAll(_harmony);
                // paczka 169: ksiega obiegu (tylko log) - okna pomiaru zlota; PO naszych latkach na te same metody (BuildFunding, WorkshopTrade,
                // MoneyLedger), PRZED ksiega towarow, ktora musi byc ostatnia; kazde okno wpinane we wlasnym try
                try { CirculationWindows.ApplyAll(_harmony); } catch (Exception e) { Log.Error("CirculationWindows.ApplyAll", e); }
                Measure169c.SetHarmony(_harmony);   // 169c: tylko zapamietanie - okna pomiaru wpina EnsureHooks w kampanii
                // paczka 146: ksiega towarow (tylko log) - NA KONCU: ramki (prefiks + finalizer) na metodach, ktore wolaja metody juz
                // zalatane wyzej i przez BK, i podsluch ItemRoster.AddToCounts dopiero gdy wszystkie ramki sa wpiete
                try { GoodsLedger.ApplyAll(_harmony); } catch (Exception e) { Log.Error("GoodsLedger.ApplyAll", e); }
            }
            catch (Exception e) { Log.Error("OnBeforeInitialModuleScreenSetAsRoot", e); }
        }

        private static int _mapVillagesLoadFails;

        /// <summary>Wioski na mapie (W2): rejestracja komponentu widoku, gdy mapa kampanii ma juz swoje menedzery (tani test co klatke).
        /// Wylacznik sprawdzany TUTAJ: przy "Map Villages Enabled" = off typ MapVillagesView (dziedziczy po SandBox.View) w ogole
        /// sie nie laduje. Wywolanie przez osobna metode NoInlining: blad ladowania typu (inna wersja SandBox.View po aktualizacji
        /// gry) wylatuje wtedy w tym try, a nie z OnApplicationTick do petli modulow gry (CTD). Wszystko inne lapie EnsureRegistered.</summary>
        protected override void OnApplicationTick(float dt)
        {
            base.OnApplicationTick(dt);
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

        public override void OnMissionBehaviorInitialize(Mission mission)
        {
            base.OnMissionBehaviorInitialize(mission);
            try
            {
                // F1 (H3 w autobitwie gracza): bitwa gracza z misja nie jest autobitwa, nawet gdy potem "wyslij wojsko"
                if (Campaign.Current != null) LosersFlee.NoteMission();
                if (Settings.Current.FieldCraftEnabled && Campaign.Current != null && mission != null)
                    mission.AddMissionBehavior(new FieldCraft());
                if (Settings.Current.HitScribeEnabled && mission != null)
                    mission.AddMissionBehavior(new HitScribe());
                if (Settings.Current.AutoParryEnabled && mission != null)
                    mission.AddMissionBehavior(new GuardMaster());
                if (Settings.Current.WoundedFleeEnabled && mission != null)
                    mission.AddMissionBehavior(new BrokenMen());
                if (Settings.Current.CampBattlePropsEnabled && Campaign.Current != null && mission != null
                    && NightRest.PlayerCamped)
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

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);
            try
            {
                var starter = gameStarterObject as CampaignGameStarter;
                if (starter == null || !(game.GameType is Campaign)) return;
                McmSettings.Apply();
                // dlugosc roku MUSI wejsc, zanim gra przeliczy swoj zegar
                LongYearTimeModel.Install(gameStarterObject);
                TierDesertionModel.Install(starter);   // prawo dezercji (Jeff 17.09) - patrz DesertionLaw.cs
                starter.AddBehavior(new ArmouryBehavior());
                starter.AddBehavior(new Uniques());
                starter.AddBehavior(new HideoutPurge());
                starter.AddBehavior(new BattleMuster());
                starter.AddBehavior(new LegendaryLaw());
                starter.AddBehavior(new UniqueLaw());
                starter.AddBehavior(new DesertionLaw());
                starter.AddBehavior(new MusterBook());
                starter.AddBehavior(new HideoutSpotter());
                starter.AddBehavior(new HouseLevies());
                starter.AddBehavior(new WorkshopTradeBehavior());   // warsztaty towarowe: linia dnia, rozmowa kupna, srednie zysku w zapisie
                starter.AddBehavior(new TownCraftsBehavior());      // paczka 148: rzemioslo miasta wedle wartosci (doba miasta, stan w zapisie)
                starter.AddBehavior(new TownFletchersBehavior());   // paczka 172: strzelarze miasta - stan w zapisie (doba miasta przez latke po warsztatach)
                starter.AddBehavior(new HeroGearBehavior());        // Z16: samotest ekranu przy wczytaniu (tylko log, bez zapisu)
                Log.Info("Behavior dodany do kampanii.");
            }
            catch (Exception e) { Log.Error("OnGameStart", e); }
        }
    }
}
