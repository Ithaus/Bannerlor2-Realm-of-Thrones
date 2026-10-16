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
        }

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
                WeaponXpPatch.ApplyAll(_harmony);
                ScrapFloorPatch.ApplyAll(_harmony);
                BkPenaltyOnce.ApplyAll(_harmony);    // cena sprzedazy sprzetu (A): kara handlowa BK raz (x5) - dubel postfiksu BK zdjety z modelu, ktory wola base; BK wpina go wczesniej, w OnSubModuleLoad
                try { A16Signals.ApplyAll(_harmony); } catch (Exception e) { Log.Error("A16Signals.ApplyAll", e); }   // A16: premia pokoju RBL po 30 dobach spokoju, dubel warstwy dobrobytu BK, pomiar ludnosci BK (latki pomiaru zawsze)
                SellByCondition.ApplyAll(_harmony);  // cena sprzedazy sprzetu (B): ksiega skupu (tylko log) - kto sprzedaje: ekran handlu gracza, SellItemsAction
                TrueArmourCost.ApplyAll(_harmony);
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
                TravellerGoods36.ApplyAll(_harmony);   // 3.6: wedrowcy BK bez towaru, rudy i drewna z niczego (postfiks po CastleSteel - Priority.Low)
                CastleSteel.ApplyAll(_harmony);   // 177-1: wedrowcy BK bez sztab i wegla z niczego (dawny ValyrianSteel.DearRefine zdjety - stal zamkowa to zwykly stopien)
                TravellerTrade36b.ApplyAll(_harmony);   // 36b: wedrowcy BK z prawdziwym towarem - zywnosc drogi z polki pochodzenia, bez jucznych z niczego, towar w komis miasto -> miasto
                SmithAudit.ApplyAll(_harmony);
                QuartermasterLaw.ApplyAll(_harmony);
                try { GarrisonKit.ApplyAll(_harmony); } catch (Exception e) { Log.Error("GarrisonKit.ApplyAll", e); }   // K1 (A10): zaloga w bitwie gracza walczy sprzetem ze swojej zbrojowni (latka DTE)
                MarketGlut.ApplyAll(_harmony);
                SupplyDemand.ApplyAll(_harmony); // prawo podazy i popytu dla uzbrojenia - PO MarketGlut (Jeff 04.10)
                MaterialLaw.ApplyAll(_harmony);  // surowce: przetopy, XP przetopu, wydobycie (Jeff 04.10)
                MineralOnce.ApplyAll(_harmony);  // lista produkcji wsi BK bez powtorzen: mineral wsi gorniczej dopisywany raz (Jeff 05.10); MUSI isc po MaterialLaw.ApplyAll (zdjety wpis oddaje jego postfix modelu)
                // GL-1 (P1): linia "People of this land" w dymku osady (wies) - postfiks dymka gry i NavalDLC (ten rejestruje dymek osady od nowa); wlasny try,
                // linia dziala tylko przy kluczu VillageOutputByPeople
                try { VillageByPeople.ApplyTip(_harmony); } catch (Exception e) { Log.Error("VillageByPeople.ApplyTip", e); }
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
                // E4/3 (108n): 1 czlowiek = 1/k osady (tabory wsi, lodzie rybackie, pobor wymuszony, zadania gracza); we wlasnym try - jej wywrotka nie moze zatrzymac latek ponizej
                try { PeopleUnit.ApplyAll(_harmony); } catch (Exception e) { Log.Error("PeopleUnit.ApplyAll", e); }
                MapClock.ApplyAll(_harmony);
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
                // m6b (4): juczne nowego taboru wsi ze stada albo z targu (postfiks InitializeVillagerPartyProperties), juczne konwoju DTE bez kopii do zbrojowni,
                // nazwane pozycje ksiegi koni (zapas drogi DTE i BK, konie przy RemoveParty) i towar partii usunietych bez zniszczenia (ksiega towarow, klucz FoodSourcesInLedger)
                try { PackSources.ApplyAll(_harmony); } catch (Exception e) { Log.Error("PackSources.ApplyAll", e); }
                // m6b (6): kasy zaulkow - rozbicie zmiany kasy miast ponizej 1/4 zapasu kupcow na kanaly (tylko log)
                try { AlleyProbe.ApplyAll(_harmony); } catch (Exception e) { Log.Error("AlleyProbe.ApplyAll", e); }
                MarchPace.ApplyAll(_harmony);
                TerrainEase.ApplyAll(_harmony);
                // NightRest PO MarchPace: kara snu ma ciac REALNA predkosc
                // kolumny (po suficie marszu), nie teoretyczna baze
                NightRest.ApplyAll(_harmony);
                // E7/O1 (228): sen do switu - prefiksy First na 10 MobileParty.SetMove* (spiacy w obozie / snem dlugu nie dostaje rozkazu ruchu; rozkaz czeka
                // na swit albo koniec snu dlugu); w menu glownym jak Wayfinder (przed JIT wolajacych), wpiete zawsze, dziala przy SleepersHoldOrders; we wlasnym try
                try { NightRest.ApplySleepGate(_harmony); } catch (Exception e) { Log.Error("NightRest.ApplySleepGate", e); }
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
                Drobne39.ApplyAll(_harmony);     // 3.9: zlomowanie zbrojowni wojska gracza przez DTE zablokowane (linia startowa "Drobne (3.9)")
                SpoilsCompany.ApplyAll(_harmony);   // klan najemnikow Spoils tylko z prawdziwych zolnierzy (bez szablonu, dosypki i ochotnikow z mapy; zaplata za ludzi do kiesy klanu)
                try { BeeSeal.ApplyAll(_harmony); } catch (Exception e) { Log.Error("BeeSeal.ApplyAll", e); }   // paczka 170: BetterEconomy bez zlota w nicosc i bez towaru, ludzi, XP z niczego (akcje gracza zamkniete z powodem, bierne zrodla zatrzymane)
                BattleWind.ApplyAll(_harmony);
                BkSupplyTemper.ApplyAll(_harmony);
                try { BkSupplyMarket.ApplyAll(_harmony); } catch (Exception e) { Log.Error("BkSupplyMarket.ApplyAll", e); }   // 3.4: zakupy zaopatrzenia BK wedlug zapasu partii, zaplata do kasy osady (PRZED ksiega towarow)
                try { BkSeal34.ApplyAll(_harmony); } catch (Exception e) { Log.Error("BkSeal34.ApplyAll", e); }               // 3.4: rynek wsi BK, sprzedaz niewolnikow, wyposazenie rycerza BKROT - bez zlota z niczego (PRZED oknami ksiegi obiegu)
                DragonUnmount.ApplyAll(_harmony);
                SlowMuster.ApplyAll(_harmony);
                SlowHealing.ApplyAll(_harmony);
                HungerLaw.ApplyAll(_harmony);
                try { FoodThrift.ApplyAll(_harmony); } catch (Exception e) { Log.Error("FoodThrift.ApplyAll", e); }         // noc 15/16.10 (B3): syta partia AI nie przeplaca za jedzenie (te same modele co HungerLaw)
                try { FoodSubstitute.ApplyAll(_harmony); } catch (Exception e) { Log.Error("FoodSubstitute.ApplyAll", e); }   // noc 15/16.10 (B2): zamiennik jedzenia przy pustym straganie (UpdateDemandShift, PRZED nawiasem kiesy ludu TownFolk)
                try { NoMakerBudget.ApplyAll(_harmony); } catch (Exception e) { Log.Error("NoMakerBudget.ApplyAll", e); }
                try { CaravanGoods.ApplyAll(_harmony); } catch (Exception e) { Log.Error("CaravanGoods.ApplyAll", e); }   // noc-ekonomia3 (E3-T1, E3-G3): ksiega kosztu karawan i myto karawan na SellItemsAction.ApplyInternal   // noc 16.10 (N2-E2): budzet kategorii bez producenta na zbytki (UpdateDemandShift, priorytet 450 - PO B2, PRZED nawiasem kiesy ludu)
                try { VillageFruit.ApplyAll(_harmony); } catch (Exception e) { Log.Error("VillageFruit.ApplyAll", e); }   // poprawka po recenzji noc-ekonomia2 16.10: magazyn wsi + 5 dob owocow, miodu i jaj z listy BK (GetWarehouseCapacity)
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
                Measure169c.SetHarmony(_harmony);
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
                EstateIncome187.SetHarmony(_harmony); // 3.1/3.3 (31b) E3/2: tylko zapamietanie - latki majatkow BK w kampanii
                Seal31B.SetHarmony(_harmony);        // 3.1/3.3 (31b) E3/4, E3/8, E3/9: tylko zapamietanie - latki gry i BK w kampanii
                MercTenure.SetHarmony(_harmony);    // M6a-1: tylko zapamietanie - latki najmu gry i BK w kampanii
                PeaceHold.SetHarmony(_harmony);     // M6a-3: tylko zapamietanie - latka pokoju gry w kampanii
                MercContract.SetHarmony(_harmony);  // 185: tylko zapamietanie - latki modelu finansow w kampanii   // 169c: tylko zapamietanie - okna pomiaru wpina EnsureHooks w kampanii
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
                starter.AddBehavior(new SmithWoodBehavior());   // Jeff 15.10: drewno w kuzni w kg - zapas otwartego ladunku w zapisie
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
                starter.AddBehavior(new A16Behavior());             // A16: linie dobrobytu, bohaterow i ludnosci BK; start sesji - wylacznik warstwy BK
                starter.AddBehavior(new ReliefBehavior());          // OB5 odsiecz oblezonych twierdz (linie, stan oblezen) i S11 rody z lennem zmieniajace krolestwo (linia)
                Log.Info("Behavior dodany do kampanii.");
            }
            catch (Exception e) { Log.Error("OnGameStart", e); }
        }
    }
}
