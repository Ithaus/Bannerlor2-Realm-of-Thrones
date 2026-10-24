using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// r4-dom (20.10) - WIOSKA GRACZA JAK PRAWDZIWA WIES. Jeff: "do wioski ktora nalezy do mnie ja moge do niej wjechac, tak jak do normalnej wioski i tam dokonac
    /// sen, inwestycje, zakup rzeczy z tej wioski, przespacerowanie sie po wiosce i w tej wiosce jest moj dom wtedy". Menu wioski (SideVillageFief, arm_side_fief) dostaje:
    ///  H1 HANDEL (klucz SideVillageFiefTrade): "Trade" - towar z targu wsi okregu (Settlement.ItemRoster wsi gry, ceny jej targu Village.MarketData, zloto do kiesy
    ///     wsi okregu - SettlementComponent.Gold), jak "Buy products" w menu wsi gry:
    ///     - wejscie z menu wsi gry ("Visit X") - partia JEST w wsi okregu (PlayerEncounter, Settlement.CurrentSettlement = wies okregu): DOKLADNIE wywolanie gry
    ///       (InventoryScreenHelper.OpenScreenAsTrade(st.ItemRoster, st.Village) - PlayerTownVisitCampaignBehavior "village"/"trade"), warunek z modelu dostepu gry;
    ///     - wejscie z mapy (partia poza osada, bez spotkania): OpenScreenAsTrade gry wziela by ceny NAJBLIZSZEGO MIASTA (GetCurrentMarketDataForPlayer: CurrentSettlement
    ///       null -> FindNearestTownToMobileParty), a InventoryLogic.DoneLogic placi kupcowi tylko przy Settlement.CurrentSettlement != null (dekompilacja: bez osady
    ///       zloto gracza szloby w nicosc, a sprzedaz bralaby je z niczego). Dlatego wlasny ekran: ten sam InventoryLogic(wies okregu) i Initialize co gra, ale ceny
    ///       targu wsi okregu, sluchacz kiesy wsi (jak MerchantInventoryListener gry), a po zamknieciu (DoneLogicExtrasDelegate - tylko po udanym DoneLogic) kiesa wsi
    ///       zmienia sie DOKLADNIE o to, co gra dala/zabrala graczowi: min(-TotalAmount, zloto wsi) - zamknieta ekonomia.
    ///  H2 DOM (klucz SideVillageFiefHouse): "Your house" - dwor gracza w jego wiosce (menu arm_side_home): nocleg we wlasnym lozku za darmo (stan noclegu karczmy
    ///     jakosci "dobry dom" - najlepszy: predkosc +12%, morale +8, sen x1.25, gojenie +50% w czasie snu; bez premii cen; TownInns.HouseSleep), skrzynia domu (ekran
    ///     schowka gry InventoryMode.Stash - wymiana druzyna <-> skrzynia; zapis arm_side_home przez SaveText). Domy klanowe RealisticCaptivity (Homes) NIE pasuja:
    ///     klucz domu to Settlement.StringId, menu rc_home czyta Settlement.CurrentSettlement (wioska poboczna nie jest osada gry, a z mapy partia nie stoi w osadzie),
    ///     zakladka Houses pomija klucze bez Settlement.Find; do tego inna DLL (RC moze byc wylaczony) - stad prosty wlasny dom.
    ///     Rzeczy kute w grze (IsCraftedWeapon - przedmiot tworzony w kampanii, bez stalego id) nie zostaja w skrzyni: zapis tekstowy nie odtworzy ich po wczytaniu -
    ///     wracaja do taboru z komunikatem.
    ///  H3 SPACER (klucz SideVillageFiefWalk): "Take a walk" - wioska poboczna nie ma wlasnej sceny; spacer = scena wsi okregu, ta sama droga co "Take a walk through
    ///     the lands" gry: (PlayerEncounter.LocationEncounter as VillageEncounter).CreateAndOpenMissionController(LocationComplex.Current.GetLocationWithId("village_center")).
    ///     Tylko przy wejsciu z menu wsi gry ("Visit X") - wtedy stan spotkania jest DOKLADNIE taki, jak przy spacerze gry (spotkanie z osada, LocationComplex wsi okregu),
    ///     a po wyjsciu ze sceny gra wraca do menu, z ktorego weszlismy (arm_side_fief). Z mapy (bez spotkania) opcja szara z podpowiedzia, jak wejsc - misja wsi
    ///     bez PlayerEncounter i LocationComplex to wywrotka (zachowania misji SandBox czytaja oba). Plan na pozniej w docs/noc-1920/r4-dom-CHANGELOG.md.
    /// Linia logu: "Wioska gracza - dom (r4-dom): ..." (wejscia w handel, dom: noce i skrzynia, spacery, potkniecia; linia doby, test autotestu).
    /// </summary>
    internal static partial class SideVillageHome
    {
        private const string P = "Wioska gracza - dom (r4-dom): ";
        internal const string HomeMenu = "arm_side_home";
        private const string SaveVer = "sh1";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // ------------------------------------------------------------ stan kampanii (zapis arm_side_home): skrzynia domu
        private static string _chestUid;                 // uid wioski, do ktorej nalezy skrzynia
        private static ItemRoster _chest;                // skrzynia (rzeczy gracza; nic z niczego - tylko przelozone z taboru)
        private static readonly List<string> _chestRaw = new List<string>();   // wpisy z zapisu bez przedmiotu w grze (mod wylaczony) - wracaja do zapisu bez zmian

        // ------------------------------------------------------------ sesja
        private static int _st, _stDay;
        private static readonly HashSet<string> _err = new HashSet<string>(StringComparer.Ordinal);
        private static bool _testDone;
        private static int _dTradeMap, _dTradeVil, _dBuyG, _dSellG, _dNights, _dCamp, _dChest, _dChestBack, _dWalk, _dWalkNo;
        private static string _savedLogged;   // recenzja r4-dom: sklad skrzyni ostatnio wypisany do logu przy zapisie (null - jeszcze nie w tej sesji)
        private static bool _quiet;           // samotest D4: Import na kopii bez linii "skrzynia z zapisu"

        /// <summary>SideVillageFief.Reset (konstruktor ArmouryBehavior, kazda kampania): stan od zera (zapis wczyta Import).</summary>
        internal static void Reset()
        {
            _chestUid = null; _chest = null; _chestRaw.Clear();
            _st = _stDay = 0; lock (_err) _err.Clear();
            _testDone = false; _savedLogged = null;
            DayZero();
        }

        private static void DayZero() { _dTradeMap = _dTradeVil = _dBuyG = _dSellG = _dNights = _dCamp = _dChest = _dChestBack = _dWalk = _dWalkNo = 0; _stDay = 0; }

        private static void Stumble(string where, Exception e)
        {
            _st++; _stDay++;
            bool first; lock (_err) first = _err.Add(where);
            if (first || _st % 200 == 0) Log.Error("SideVillageHome." + where + " (potkniecie " + _st + ")", e);
        }

        private static bool Menu { get { var s = Settings.Current; return s != null && s.SideVillageFiefMenu && SideVillageFief.HasFief; } }

        // m113-zakup: wies okregu wioski OTWARTEGO menu (dom, skrzynia i spacer tylko przy pierwszej wiosce - wtedy to ta sama wies)
        private static Settlement District { get { var d = SideVillageFief.MenuDistId; return d != null ? Settlement.Find(d) : null; } }

        /// <summary>Partia stoi w wsi okregu (wejscie "Visit X" z menu wsi gry): spotkanie z ta osada gry - stan jak w menu "village".</summary>
        private static bool InDistrict(Settlement st)
        {
            var mp = MobileParty.MainParty;
            return st != null && mp != null && ReferenceEquals(mp.CurrentSettlement, st) && ReferenceEquals(Settlement.CurrentSettlement, st);
        }

        /// <summary>Partia na mapie przy swojej wiosce, bez osady, spotkania i bitwy (wejscie z mapy).</summary>
        private static bool OnMapAtVillage()
        {
            var mp = MobileParty.MainParty;
            if (mp == null || mp.CurrentSettlement != null || mp.MapEvent != null || PlayerEncounter.Current != null || Settlement.CurrentSettlement != null) return false;
            return TownInns.HostNearNow(SideVillageFief.MenuDistId, SideVillageFief.MenuUid);   // m113-zakup: wioska menu
        }

        /// <summary>Wojna gracza z krolestwem wsi okregu (jak dochod i nocleg: w wojnie wioska nie przyjmuje).</summary>
        private static bool AtWar(Settlement st)
        {
            IFaction mf = Clan.PlayerClan != null ? Clan.PlayerClan.MapFaction : null, df = st != null ? st.MapFaction : null;
            return mf != null && df != null && !ReferenceEquals(mf, df) && FactionManager.IsAtWarAgainstFaction(mf, df);
        }

        private static bool Burned()
        {
            VillageBook.VState vs;
            return VillageBook.StateOf(SideVillageFief.Uid, out vs) && (vs.State == VillageBook.SBurned || vs.State == VillageBook.SBurning || vs.State == VillageBook.SAbandoned);
        }

        private static void Mesh(MenuCallbackArgs a)
        {
            try
            {
                var st = District;
                string mesh = st != null && st.SettlementComponent != null ? st.SettlementComponent.WaitMeshName : null;
                a.MenuContext.SetBackgroundMeshName(string.IsNullOrEmpty(mesh) ? "wait_fallback" : mesh);
            }
            catch (Exception e) { Stumble("Tlo", e); }
        }

        // ============================================================ menu (SideVillageFief.AddMenus - po opcjach wioski, przed "Leave")
        internal static void AddMenus(CampaignGameStarter starter)
        {
            try
            {
                // kolejnosc w menu wioski: nocleg 0, prace 1, przeglad 2, handel 3, dom 4, spacer 5, wyjazd (dodany wczesniej z indeksem 9 - na koncu)
                starter.AddGameMenuOption(SideVillageFief.Menu, "arm_side_fief_trade", "{=!}Trade", TradeCondition, TradeConsequence, false, 3);
                starter.AddGameMenuOption(SideVillageFief.Menu, "arm_side_fief_house", "{=!}Your house", HouseCondition,
                    delegate (MenuCallbackArgs a) { try { Log.Info(P + "dom w " + SideVillageFief.Name + " - wejscie" + (InDistrict(District) ? " (z wsi okregu)" : " (z mapy)") + "."); GameMenu.SwitchToMenu(HomeMenu); } catch (Exception e) { Stumble("House", e); } },
                    false, 4);
                starter.AddGameMenuOption(SideVillageFief.Menu, "arm_side_fief_walk", "{=!}Take a walk", WalkCondition, WalkConsequence, false, 5);

                starter.AddGameMenu(HomeMenu, "{=!}{ARM_SIDE_HOME_TEXT}", delegate (MenuCallbackArgs a)
                    {
                        try { MBTextManager.SetTextVariable("ARM_SIDE_HOME_TEXT", HomeText()); } catch (Exception e) { Stumble("HomeInit", e); }
                        Mesh(a);
                    }, GameMenu.MenuOverlayType.None);
                starter.AddGameMenuOption(HomeMenu, "arm_side_home_bed", "{=!}Sleep in your bed", BedCondition, BedConsequence, false, 0);
                starter.AddGameMenuOption(HomeMenu, "arm_side_home_chest", "{=!}Open the chest",
                    delegate (MenuCallbackArgs a)
                    {
                        a.optionLeaveType = GameMenuOption.LeaveType.Manage;
                        if (!SideVillageFief.HasFief) return false;
                        // recenzja r4-dom: skrzynia zyje tylko w kluczu zapisu arm_side_home - starsza DLL Armoury albo wylaczony Armoury go nie zapisze (rzeczy przepadna)
                        a.Tooltip = new TextObject("{=!}Put things away in the chest of your house, or take them out. What lies here stays here until you come back for it."
                                                   + " (Mod note: empty the chest before you switch Armoury off or go back to an older Armoury - older versions do not know this chest, and what is in it would be lost on the next save.)");
                        return true;
                    },
                    delegate (MenuCallbackArgs a) { try { OpenChest(); } catch (Exception e) { Stumble("Chest", e); } }, false, 1);
                starter.AddGameMenuOption(HomeMenu, "arm_side_home_leave", "{=!}Step outside",
                    delegate (MenuCallbackArgs a) { a.optionLeaveType = GameMenuOption.LeaveType.Leave; return true; },
                    delegate (MenuCallbackArgs a) { try { GameMenu.SwitchToMenu(SideVillageFief.Menu); } catch (Exception e) { Stumble("HomeLeave", e); } }, true, 9);
            }
            catch (Exception e) { Stumble("AddMenus", e); }
        }

        // ============================================================ H1: handel
        private static bool TradeCondition(MenuCallbackArgs a)
        {
            a.optionLeaveType = GameMenuOption.LeaveType.Trade;
            try
            {
                var s = Settings.Current;
                if (!Menu || !s.SideVillageFiefTrade) return false;
                var st = District;
                if (st == null || st.Village == null) return false;
                string dn = st.Name.ToString();
                if (AtWar(st)) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}At war with its lord's realm, the market of " + dn + " will not trade with you."); return true; }
                if (st.Village.VillageState != Village.VillageStates.Normal) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}Raiders have been through " + dn + " - there is no market to trade at now."); return true; }
                if (InDistrict(st))
                {
                    // ta sama zgoda co "Buy products" gry (PlayerTownVisitCampaignBehavior.game_menu_village_buy_good_on_condition)
                    bool dis; TextObject why;
                    bool can = Campaign.Current.Models.SettlementAccessModel.CanMainHeroDoSettlementAction(st, SettlementAccessModel.SettlementAction.Trade, out dis, out why);
                    if (!can) { if (!dis) return false; a.IsEnabled = false; if (why != null) a.Tooltip = why; return true; }   // jak MenuHelper.SetOptionProperties
                }
                else if (!OnMapAtVillage()) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}You are not at the village."); return true; }
                a.Tooltip = new TextObject("{=!}Buy and sell at the market of " + dn + ": what your village and its district make, at its prices. The coin goes to the village's purse ("
                                           + st.Village.Gold.ToString("N0", Inv) + " now).");
                return true;
            }
            catch (Exception e) { Stumble("TradeCondition", e); return false; }
        }

        private static void TradeConsequence(MenuCallbackArgs a)
        {
            try
            {
                var st = District;
                if (st == null || st.Village == null) return;
                if (InDistrict(st))
                {
                    _dTradeVil++;
                    Log.Info(P + "handel w " + SideVillageFief.MenuName + " - z wsi okregu " + st.Name + " (" + st.StringId + "): droga gry (OpenScreenAsTrade), zloto wsi " + st.Village.Gold + ".");
                    InventoryScreenHelper.OpenScreenAsTrade(st.ItemRoster, st.Village);
                    return;
                }
                if (!OnMapAtVillage()) return;
                OpenMapTrade(st);
            }
            catch (Exception e) { Stumble("Trade", e); }
        }

        /// <summary>Handel z mapy: ekran jak OpenScreenAsTrade gry, ale ceny targu wsi okregu i kiesa wsi rozliczona po zamknieciu (gra bez osady nie placi kupcowi).</summary>
        private static void OpenMapTrade(Settlement st)
        {
            var v = st.Village;
            var gsm = Game.Current != null ? Game.Current.GameStateManager : null;
            if (gsm == null || v.Owner == null) return;
            var listener = new VillageListener(v);
            var state = gsm.CreateState<InventoryState>();
            state.InventoryMode = InventoryScreenHelper.InventoryMode.Trade;
            var logic = new InventoryLogic(v.Owner);
            logic.Initialize(st.ItemRoster, PartyBase.MainParty.ItemRoster, PartyBase.MainParty.MemberRoster, true, true, CharacterObject.PlayerCharacter,
                             InventoryScreenHelper.InventoryCategoryType.None, v.MarketData, false, state.InventoryMode,
                             new TextObject("{=!}" + SideVillageFief.MenuName + " - market of " + st.Name));
            logic.SetInventoryListener(listener);
            state.InventoryLogic = logic;
            int heroGold0 = Hero.MainHero.Gold, vilGold0 = v.Gold;
            state.DoneLogicExtrasDelegate = delegate { MapTradeDone(st, logic, listener, heroGold0, vilGold0); };
            _dTradeMap++;
            Log.Info(P + "handel w " + SideVillageFief.MenuName + " - z mapy: towar i ceny targu wsi okregu " + st.Name + " (" + st.StringId + "), zloto wsi " + vilGold0 + ", gracza " + heroGold0 + ".");
            gsm.PushState(state);
        }

        /// <summary>Po udanym DoneLogic (gra dala graczowi min(-TotalAmount, zloto kupca) - InventoryLogic.DoneLogic): kiesa wsi okregu o tyle samo w druga strone.
        /// Przy osadzie (nie powinno sie zdarzyc z mapy) gra placi sama przez sluchacza - wtedy nic.</summary>
        private static void MapTradeDone(Settlement st, InventoryLogic logic, VillageListener l, int heroGold0, int vilGold0)
        {
            try
            {
                var v = st.Village;
                if (Settlement.CurrentSettlement != null && Settlement.CurrentSettlement.SettlementComponent != null)
                {
                    Log.Info(P + "handel z mapy zamkniety w osadzie " + Settlement.CurrentSettlement.StringId + " - zloto rozliczyla gra (sluchacz), bez naszego przelewu.");
                    return;
                }
                int t = logic.TotalAmount;
                int g = l.LastGold >= 0 ? l.LastGold : v.Gold;
                int toPlayer = Math.Min(-t, g);              // to samo, co gra przelala graczowi (GiveGoldAction z niczego)
                if (toPlayer != 0) v.ChangeGold(-toPlayer);   // kiesa wsi: kupno gracza (toPlayer < 0) - wies dostaje; sprzedaz - wies placi
                if (toPlayer < 0) _dBuyG += -toPlayer; else _dSellG += toPlayer;
                Log.Info(P + "handel z mapy zamkniety: gracz " + (toPlayer >= 0 ? "+" : "") + toPlayer + " (kiesa " + heroGold0 + " -> " + Hero.MainHero.Gold + "), wies okregu "
                         + st.Name + " " + (toPlayer <= 0 ? "+" : "") + (-toPlayer) + " (kiesa " + vilGold0 + " -> " + v.Gold + "), suma rachunku " + t + ".");
            }
            catch (Exception e) { Stumble("MapTradeDone", e); }
        }

        /// <summary>Kiesa wsi okregu jako kupiec (jak MerchantInventoryListener gry, ktory jest prywatny). LastGold - ostatni odczyt (ten, ktorym DoneLogic ograniczyl wyplate).</summary>
        private sealed class VillageListener : InventoryListener
        {
            private readonly Village _v;
            internal int LastGold = -1;
            internal VillageListener(Village v) { _v = v; }
            public override int GetGold() { LastGold = _v.Gold; return LastGold; }
            public override TextObject GetTraderName() { return _v.Owner != null ? _v.Owner.Name : _v.Name; }
            public override void SetGold(int gold) { _v.ChangeGold(gold - _v.Gold); }
            public override PartyBase GetOppositeParty() { return _v.Owner; }
            public override void OnTransaction() { }
        }

        // ============================================================ H2: dom
        private static bool HouseCondition(MenuCallbackArgs a)
        {
            a.optionLeaveType = GameMenuOption.LeaveType.Submenu;
            try
            {
                var s = Settings.Current;
                if (!Menu || !s.SideVillageFiefHouse || !SideVillageFief.CurIsPrimary) return false;   // m113-zakup: dom tylko w pierwszej wiosce
                var st = District;
                if (st != null && AtWar(st)) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}While you are at war with its lord's realm, you cannot stay in your house here."); return true; }
                if (st != null && st.Village != null && st.Village.VillageState == Village.VillageStates.BeingRaided) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}Raiders are on these lands - not now."); return true; }
                a.Tooltip = new TextObject("{=!}Your own house in " + SideVillageFief.Name + ": sleep in your own bed (the best rest there is, free) and keep things in its chest.");
                return true;
            }
            catch (Exception e) { Stumble("HouseCondition", e); return false; }
        }

        internal static string HomeText()
        {
            if (!SideVillageFief.HasFief) return "This house is not yours.";
            var sb = new StringBuilder(300);
            sb.Append("Your house in ").Append(SideVillageFief.Name).Append(" - a hall with a hearth, a bed of your own, barns for the horses and a chest by the wall.");
            if (Burned()) sb.Append(" Fire has been through the village; the house is blackened and cold.");
            int n = 0, k = 0;
            var c = ChestFor(SideVillageFief.Uid, false);
            if (c != null) for (int i = 0; i < c.Count; i++) { int x = c.GetElementNumber(i); if (x > 0) { n++; k += x; } }
            sb.Append('\n').Append(n == 0 ? "The chest is empty." : "The chest holds " + k.ToString("N0", Inv) + " things (" + n + " kinds).");
            if (_chestRaw.Count > 0) sb.Append(" ").Append(_chestRaw.Count).Append(" more kinds no one here knows any more lie at the bottom.");
            return sb.ToString();
        }

        /// <summary>Nocleg we wlasnym domu: klucze, karczmy czynne (stan noclegu TownInns), partia przy wiosce, bez wojny, rabunku i pozaru. why - powod po angielsku.</summary>
        private static bool BedOk(out string why)
        {
            why = null;
            var s = Settings.Current;
            if (s == null || !s.SideVillageFiefHouse) { why = "your house is switched off (mod settings)."; return false; }
            if (!TownInns.On) { why = "a night in a bed needs Town Inns and Night Rest (mod settings)."; return false; }
            if (!SideVillageFief.HasFief) { why = "this house is not yours."; return false; }
            var st = District;
            if (st == null || st.Village == null) { why = "the village is not known."; return false; }
            if (!TownInns.HostNearNow(SideVillageFief.DistId, SideVillageFief.Uid)) { why = "you are not at the village."; return false; }
            if (AtWar(st)) { why = "while you are at war with its lord's realm, you cannot stay here."; return false; }
            if (st.Village.VillageState == Village.VillageStates.BeingRaided) { why = "raiders are on these lands."; return false; }
            if (Burned()) { why = "your house burned with the village - there is no bed left to sleep in."; return false; }
            return true;
        }

        private static bool BedCondition(MenuCallbackArgs a)
        {
            a.optionLeaveType = GameMenuOption.LeaveType.Wait;
            if (!SideVillageFief.HasFief) return false;
            try
            {
                var mp = MobileParty.MainParty;
                if (NightRest.EnlistedNow) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}Your lord decides when the men sleep."); return true; }
                if (mp != null && mp.Army != null && mp.Army.LeaderParty != mp) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}You march in another lord's army - he decides where the host sleeps."); return true; }
                string why;
                // m110-sam: bez zolnierzy - bez ludzi w sali; sam - "you sleep your fill"
                if (BedOk(out why)) a.Tooltip = new TextObject("{=!}You sleep in your own bed" + PartyWords.Pick(" and the men in the hall and the barns", "", " and your companions in the hall")
                                                              + " - no charge. Nobody stirs before dawn, and if " + PartyWords.Pick("they sleep their fill: ", "you sleep your fill: ", "you all sleep your fill: ")
                                                              + TownInns.HouseBonusText() + " (the best rest there is).");
                else { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}Not tonight: " + why + " 'Rest the night here' in the village menu still works."); }
                return true;
            }
            catch (Exception e) { Stumble("BedCondition", e); return false; }
        }

        private static void BedConsequence(MenuCallbackArgs a)
        {
            try
            {
                string why;
                if (!BedOk(out why) || !TownInns.HouseSleep(SideVillageFief.DistId, SideVillageFief.Uid, SideVillageFief.Name)) { _dCamp++; Log.Info(P + "nocleg w domu niemozliwy (" + (why ?? "stan noclegu") + ") - bez snu."); return; }
                _dNights++;
                Log.Info(P + "nocleg we wlasnym domu w " + SideVillageFief.Name + " (stan noclegu jakosci dobry dom, bez oplaty).");
                NightRest.BeginInnSleep();   // zwykle menu (nie oczekiwania) - SwitchToMenu do snu wolno (jak "Rest the night here")
            }
            catch (Exception e) { Stumble("Bed", e); }
        }

        /// <summary>Skrzynia tej wioski (create - nowa pusta, gdy brak). Skrzynia innej wioski (po zmianie wioski) - ta sama skrzynia przechodzi (rzeczy gracza nie gina).</summary>
        private static ItemRoster ChestFor(string uid, bool create)
        {
            if (_chest == null && create) _chest = new ItemRoster();
            if (_chest != null && uid != null) _chestUid = uid;
            return _chest;
        }

        private static void OpenChest()
        {
            var gsm = Game.Current != null ? Game.Current.GameStateManager : null;
            if (gsm == null || !SideVillageFief.HasFief) return;
            var st = District;
            IMarketData md = st != null && st.Village != null ? (IMarketData)st.Village.MarketData : null;   // wartosci w ekranie schowka - targ wsi okregu (gra: najblizsze miasto)
            if (md == null) { Log.Info(P + "skrzynia domu: wies okregu " + SideVillageFief.DistId + " nieznana - skrzynia nie otwarta."); return; }
            var chest = ChestFor(SideVillageFief.Uid, true);
            // jak InventoryScreenHelper.OpenScreenAsStash gry (Stash, bez handlu), plus DoneLogicExtrasDelegate - rzeczy kute w kampanii wracaja do taboru (zapis tekstowy)
            var state = gsm.CreateState<InventoryState>();
            state.InventoryMode = InventoryScreenHelper.InventoryMode.Stash;
            var logic = new InventoryLogic(null);
            logic.Initialize(chest, MobileParty.MainParty, false, false, CharacterObject.PlayerCharacter, InventoryScreenHelper.InventoryCategoryType.None,
                             md, false, state.InventoryMode, new TextObject("{=!}Chest"));
            state.InventoryLogic = logic;
            state.DoneLogicExtrasDelegate = delegate { ChestDone(); };
            _dChest++;
            Log.Info(P + "skrzynia domu otwarta w " + SideVillageFief.Name + " (" + Count(chest) + " sztuk, " + chest.Count + " rodzajow).");
            gsm.PushState(state);
        }

        private static int Count(ItemRoster r) { int k = 0; if (r != null) for (int i = 0; i < r.Count; i++) k += Math.Max(0, r.GetElementNumber(i)); return k; }

        /// <summary>m113-zakup (odkup pierwszej wioski, LandSale Z7): skrzynia domu pusta - takze bez wpisow spoza gry z zapisu. Nie rzuca.</summary>
        internal static bool ChestEmpty() { try { return (_chest == null || Count(_chest) == 0) && _chestRaw.Count == 0; } catch { return false; } }

        private static void ChestDone()
        {
            try
            {
                var c = _chest;
                if (c == null) return;
                var back = new List<ItemRosterElement>();
                for (int i = 0; i < c.Count; i++)
                {
                    var el = c[i];
                    var it = el.EquipmentElement.Item;
                    if (el.Amount > 0 && it != null && !Saveable(el.EquipmentElement)) back.Add(el);
                }
                foreach (var el in back)
                {
                    c.AddToCounts(el.EquipmentElement, -el.Amount);
                    MobileParty.MainParty.ItemRoster.AddToCounts(el.EquipmentElement, el.Amount);
                    _dChestBack += el.Amount;
                    Log.Player(el.EquipmentElement.Item.Name + " is smith-made and stays with you - the chest keeps only common goods and gear.", true);
                }
                Log.Info(P + "skrzynia domu zamknieta: " + Count(c) + " sztuk, " + c.Count + " rodzajow" + (back.Count > 0 ? ", " + back.Count + " rodzajow kutych w kampanii wrocilo do taboru" : "") + ".");
            }
            catch (Exception e) { Stumble("ChestDone", e); }
        }

        /// <summary>Przedmiot z id w obiektach gry (odtworzy sie po wczytaniu z napisu): bez rzeczy kutych w kampanii i bez obiektow nie zarejestrowanych.</summary>
        private static bool Saveable(EquipmentElement e)
        {
            var it = e.Item;
            if (it == null || string.IsNullOrEmpty(it.StringId) || it.IsCraftedWeapon || it.IsCraftedByPlayer) return false;
            try
            {
                var o = MBObjectManager.Instance != null ? MBObjectManager.Instance.GetObject<ItemObject>(it.StringId) : null;
                if (!ReferenceEquals(o, it)) return false;
                if (e.ItemModifier != null && string.IsNullOrEmpty(e.ItemModifier.StringId)) return false;
            }
            catch { return false; }
            return true;
        }

        // ============================================================ H3: spacer
        private static bool WalkCondition(MenuCallbackArgs a)
        {
            a.optionLeaveType = GameMenuOption.LeaveType.Mission;
            try
            {
                var s = Settings.Current;
                if (!Menu || !s.SideVillageFiefWalk || !SideVillageFief.CurIsPrimary) return false;   // m113-zakup: spacer tylko z pierwszej wioski
                var st = District;
                if (st == null || st.Village == null) return false;
                string dn = st.Name.ToString();
                string why;
                if (!WalkReady(st, out why))
                {
                    a.IsEnabled = false;
                    a.Tooltip = new TextObject("{=!}" + why);
                    return true;
                }
                // recenzja r4-dom: zadania gry przypiete do menu "village" (np. Family Feud) rozstawiaja sie tylko przy spacerze z menu wsi okregu
                a.Tooltip = new TextObject("{=!}Walk the lanes and fields of your village. (Your village has no scene of its own - you walk the lands of " + dn + ".)"
                                           + " For quest business in " + dn + ", take the walk from " + dn + "'s own menu.");
                return true;
            }
            catch (Exception e) { Stumble("WalkCondition", e); return false; }
        }

        /// <summary>Spacer tylko w stanie spaceru gry: partia w wsi okregu, spotkanie VillageEncounter tej osady, LocationComplex z "village_center" i scena, wies nie rabowana.</summary>
        private static bool WalkReady(Settlement st, out string why)
        {
            why = null;
            string dn = st.Name.ToString();
            if (!InDistrict(st)) { why = "To walk your village, ride into " + dn + " and choose 'Visit " + SideVillageFief.Name + " (your village)' there - your village has no scene of its own on the map."; return false; }
            if (st.Village.VillageState != Village.VillageStates.Normal) { why = "Raiders have been through " + dn + " - not now."; return false; }
            var le = PlayerEncounter.LocationEncounter as VillageEncounter;
            var lc = LocationComplex.Current;
            var loc = lc != null ? lc.GetLocationWithId("village_center") : null;
            if (le == null || loc == null || !ReferenceEquals(le.Settlement, st)) { why = "There is nowhere to walk here."; return false; }
            string scene = null;
            try { scene = loc.GetSceneName(1); } catch { }
            if (string.IsNullOrEmpty(scene)) { why = "There is nowhere to walk here."; return false; }
            return true;
        }

        private static void WalkConsequence(MenuCallbackArgs a)
        {
            try
            {
                var st = District;
                string why;
                if (st == null || st.Village == null || !WalkReady(st, out why)) { _dWalkNo++; Log.Info(P + "spacer odmowiony (stan spotkania nie jak w menu wsi gry)."); return; }
                var le = (VillageEncounter)PlayerEncounter.LocationEncounter;
                var loc = LocationComplex.Current.GetLocationWithId("village_center");
                _dWalk++;
                Log.Info(P + "spacer w " + SideVillageFief.Name + " - scena wsi okregu " + st.Name + " (" + st.StringId + ", " + loc.GetSceneName(1) + "), droga gry (VillageEncounter.CreateAndOpenMissionController).");
                le.CreateAndOpenMissionController(loc);
            }
            catch (Exception e) { Stumble("Walk", e); }
        }

        // ============================================================ zapis (arm_side_home): "sh1|uid|item~modifier~n;..."
        internal static string Export()
        {
            var c = _chest;
            if ((c == null || c.Count == 0) && _chestRaw.Count == 0) return "";
            var sb = new StringBuilder(64 + (c != null ? c.Count * 40 : 0));
            sb.Append(SaveVer).Append('|').Append((_chestUid ?? "").Replace("|", "/")).Append('|');
            bool first = true;
            if (c != null)
                for (int i = 0; i < c.Count; i++)
                {
                    var el = c[i];
                    var it = el.EquipmentElement.Item;
                    if (el.Amount <= 0 || it == null || !Saveable(el.EquipmentElement)) continue;
                    if (!first) sb.Append(';');
                    first = false;
                    sb.Append(Clean(it.StringId)).Append('~').Append(el.EquipmentElement.ItemModifier != null ? Clean(el.EquipmentElement.ItemModifier.StringId) : "").Append('~').Append(el.Amount.ToString(Inv));
                }
            foreach (var raw in _chestRaw) { if (!first) sb.Append(';'); first = false; sb.Append(raw); }
            return sb.ToString();
        }

        /// <summary>ArmouryBehavior.SyncData (zapis): Export i linia logu ze skladem skrzyni - przy pierwszym zapisie sesji i przy kazdej zmianie. Recenzja r4-dom:
        /// skrzynia zyje tylko w kluczu arm_side_home (rzeczy zdjete z taboru) - starsza DLL Armoury albo wylaczony Armoury nie zapisze klucza i rzeczy przepadna;
        /// z tej linii da sie je odtworzyc recznie (przedmiot~modyfikator~sztuk).</summary>
        internal static string ExportSave()
        {
            string x = Export();
            try
            {
                if (_savedLogged == null && x.Length == 0) { _savedLogged = x; return x; }   // pusta skrzynia od poczatku sesji - bez linii
                if (!string.Equals(x, _savedLogged, StringComparison.Ordinal))
                {
                    _savedLogged = x;
                    Log.Info(P + "skrzynia w zapisie (" + Count(_chest) + " sztuk, " + (_chest != null ? _chest.Count : 0) + " rodzajow, " + _chestRaw.Count
                             + " wpisow spoza gry; przedmiot~modyfikator~sztuk): " + (x.Length == 0 ? "pusta" : x) + ".");
                }
            }
            catch (Exception e) { Stumble("ExportSave", e); }
            return x;
        }

        private static string Clean(string s) { return (s ?? "").Replace("|", "/").Replace(";", ",").Replace("~", "-"); }

        internal static void Import(string data)
        {
            _chestUid = null; _chest = null; _chestRaw.Clear();
            if (string.IsNullOrEmpty(data)) return;
            try
            {
                var f = data.Split(new[] { '|' }, 3);
                if (f.Length < 3 || f[0] != SaveVer) { Log.Info(P + "zapis arm_side_home nieznany (" + data.Length + " znakow) - pominiety."); return; }
                _chestUid = f[1].Length > 0 ? f[1] : null;
                _chest = new ItemRoster();
                int ok = 0, lost = 0;
                foreach (var e in f[2].Split(';'))
                {
                    if (e.Length == 0) continue;
                    var p = e.Split('~');
                    int n;
                    if (p.Length < 3 || !int.TryParse(p[2], NumberStyles.Integer, Inv, out n) || n <= 0) { lost++; continue; }
                    var it = MBObjectManager.Instance.GetObject<ItemObject>(p[0]);
                    ItemModifier mod = p[1].Length > 0 ? MBObjectManager.Instance.GetObject<ItemModifier>(p[1]) : null;
                    if (it == null || (p[1].Length > 0 && mod == null)) { _chestRaw.Add(e); lost++; continue; }   // przedmiot spoza gry teraz - wpis zostaje w zapisie
                    _chest.AddToCounts(new EquipmentElement(it, mod), n);
                    ok++;
                }
                if (!_quiet && (ok > 0 || lost > 0))
                    Log.Info(P + "skrzynia z zapisu: " + ok + " rodzajow (" + Count(_chest) + " sztuk)" + (_chestRaw.Count > 0 ? ", " + _chestRaw.Count + " wpisow bez przedmiotu w grze (zostaja w zapisie)" : "")
                             + (lost > _chestRaw.Count ? ", " + (lost - _chestRaw.Count) + " wpisow uszkodzonych (pominiete)" : "") + ".");
            }
            catch (Exception e) { Stumble("Import", e); }
        }

        // ============================================================ doba, test
        /// <summary>SideVillageFief.Daily: linia doby (tylko z ruchem albo potknieciem).</summary>
        internal static void Daily()
        {
            try
            {
                var s = Settings.Current;
                if (s == null) return;
                if (_dTradeMap + _dTradeVil + _dNights + _dCamp + _dChest + _dWalk + _dWalkNo + _stDay == 0) return;
                Log.Info(P + "dzien " + ((int)CampaignTime.Now.ToDays - 1) + " | klucze: handel " + (s.SideVillageFiefTrade ? "WL" : "WYL") + ", dom " + (s.SideVillageFiefHouse ? "WL" : "WYL")
                         + ", spacer " + (s.SideVillageFiefWalk ? "WL" : "WYL") + " | handel: z mapy " + _dTradeMap + ", z wsi okregu " + _dTradeVil + " (z mapy: wies dostala " + _dBuyG + ", zaplacila " + _dSellG + ")"
                         + " | dom: noce " + _dNights + ", odmowy " + _dCamp + ", skrzynia otwarta " + _dChest + " (w niej " + Count(_chest) + " sztuk), kute do taboru " + _dChestBack
                         + " | spacery " + _dWalk + ", odmowy " + _dWalkNo + " | potkniecia doby " + _stDay + ", sesji " + _st + ".");
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally { DayZero(); }
        }

        /// <summary>SideVillageFief.Hourly: furtka autotestu (DLL autotestu CrashScribe + plik armoury-wioska-gracza.flag) - raz na sesje, gdy wioska juz nadana.</summary>
        internal static void Hourly()
        {
            try
            {
                if (_testDone || !SideVillageFief.HasFief) return;
                int m = LogGate.Mode;
                if (m != 1 && m != 2) return;
                var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string p = System.IO.Path.Combine(System.IO.Path.Combine(System.IO.Path.Combine(docs, "Mount and Blade II Bannerlord"), "CrashScribe"), SideVillageFief.AutoFlagName);
                if (!System.IO.File.Exists(p)) return;
                _testDone = true;
                Test();
            }
            catch (Exception e) { _testDone = true; Stumble("Hourly", e); }
        }

        /// <summary>Samotest bez klikania: menu i opcje, handel (towar, ceny targu wsi, kiesa), skrzynia (zapis tam i z powrotem na kopii), nocleg w domu (stan i zapis
        /// arm_inns, stan gracza przywrocony), spacer (scena "village_center" wsi okregu). Nic nie zmienia w stanie gracza.</summary>
        private static void Test()
        {
            var sb = new StringBuilder(800);
            int pass = 0, fail = 0;
            Action<string, bool, string> add = (n, ok, t) => { sb.Append(" | ").Append(n).Append(' ').Append(ok ? "TAK" : "NIE").Append(" (").Append(t).Append(')'); if (ok) pass++; else fail++; };
            try
            {
                var gm = Campaign.Current.GameMenuManager;
                var home = gm != null ? gm.GetGameMenu(HomeMenu) : null;
                var vil = gm != null ? gm.GetGameMenu(SideVillageFief.Menu) : null;
                add("D1 menu domu i opcje wioski", home != null && vil != null, (home != null ? "arm_side_home" : "BRAK domu") + ", " + (vil != null ? "arm_side_fief" : "BRAK wioski"));
                var st = District;
                add("D2 wies okregu", st != null && st.Village != null, st != null ? st.StringId : "brak");
                if (st != null && st.Village != null)
                {
                    var v = st.Village;
                    int kinds = st.ItemRoster != null ? st.ItemRoster.Count : -1;
                    string sample = "-";
                    bool priced = false;
                    if (st.ItemRoster != null)
                        for (int i = 0; i < st.ItemRoster.Count; i++)
                        {
                            var it = st.ItemRoster.GetItemAtIndex(i);
                            if (it == null || st.ItemRoster.GetElementNumber(i) <= 0) continue;
                            int pr = v.MarketData.GetPrice(it, MobileParty.MainParty, false, st.Party);
                            sample = it.StringId + " " + pr; priced = pr > 0; break;
                        }
                    add("D3 handel: towar, ceny targu wsi, kiesa", v.MarketData != null && kinds >= 0, "rodzajow " + kinds + ", przyklad " + sample + (priced ? "" : " (bez ceny)") + ", kiesa wsi " + v.Gold);
                    // D4 skrzynia: zapis tam i z powrotem na kopii (stan gracza przywrocony)
                    string c0 = Export(); string u0 = _chestUid; var ch0 = _chest; var raw0 = new List<string>(_chestRaw);
                    ItemObject probe = null;
                    var pr0 = MobileParty.MainParty.ItemRoster;
                    for (int i = 0; i < pr0.Count && probe == null; i++) { var e = pr0[i].EquipmentElement; if (e.Item != null && Saveable(e)) probe = e.Item; }
                    if (probe == null) probe = MBObjectManager.Instance.GetObject<ItemObject>("grain");
                    _chest = new ItemRoster(); _chestUid = SideVillageFief.Uid; _chestRaw.Clear();
                    if (probe != null) _chest.AddToCounts(probe, 3);
                    string e1 = Export(); _quiet = true; try { Import(e1); } finally { _quiet = false; } string e2 = Export();
                    bool same = e1 == e2 && (probe == null || (_chest != null && _chest.GetItemNumber(probe) == 3));
                    _chest = ch0; _chestUid = u0; _chestRaw.Clear(); _chestRaw.AddRange(raw0);
                    add("D4 skrzynia: zapis tam i z powrotem", same && Export() == c0, e1.Length > 120 ? e1.Substring(0, 120) : e1);
                    // D5 nocleg w domu: stan noclegu jakosci dobry dom z uid wioski, zapis arm_inns tam i z powrotem, stan gracza przywrocony
                    string i0 = TownInns.Export();
                    bool made = TownInns.HouseSleep(SideVillageFief.DistId, SideVillageFief.Uid, SideVillageFief.Name);
                    string i1 = TownInns.Export();
                    TownInns.Import(i1); string i2 = TownInns.Export();
                    bool house = made && TownInns.HouseNow();
                    TownInns.Import(i0);
                    add("D5 nocleg w domu (stan i zapis)", made ? i1 == i2 && house && TownInns.Export() == i0 : !TownInns.On,
                        made ? (i1 == i2 ? "zapis tam i z powrotem" : "zapis ROZNY") + ", dobry dom " + (house ? "tak" : "NIE") + ", stan gracza przywrocony" : "bez stanu (karczmy " + (TownInns.On ? "WL" : "WYL") + ")");
                    // D6 spacer: scena village_center wsi okregu (bez wejscia - wejscie tylko z menu wsi gry, recznie)
                    var loc = st.LocationComplex != null ? st.LocationComplex.GetLocationWithId("village_center") : null;
                    string scene = null; try { scene = loc != null ? loc.GetSceneName(1) : null; } catch { }
                    add("D6 spacer: scena wsi okregu", !string.IsNullOrEmpty(scene), scene ?? "brak");
                    add("D7 tekst domu", HomeText().Length > 40, HomeText().Replace('\n', ' '));
                }
            }
            catch (Exception e) { add("wyjatek", false, e.GetType().Name + ": " + e.Message); Stumble("Test", e); }
            Log.Info(P + "test (autotest, plik " + SideVillageFief.AutoFlagName + ") | wynik TAK " + pass + ", NIE " + fail + sb.ToString() + ".");
        }
    }

    internal static partial class TownInns
    {
        internal const int HouseQ = 2;   // r4-dom: nocleg we wlasnym domu w wiosce gracza - jakosc "dobry dom" (najlepsza), bez oplaty i bez premii cen
        private static readonly TextObject _txtHouse = new TextObject("{=!}Slept in your own house");

        /// <summary>Stan noclegu z uid wioski gracza i jakoscia dobrego domu = nocleg we wlasnym domu (bez nowego pola w zapisie: Q i HostUid sa w arm_inns).</summary>
        private static bool IsHouse(Rest r) { return r != null && r.HostUid != null && r.Q == HouseQ; }

        /// <summary>Autotest r4-dom: biezacy stan noclegu to nocleg we wlasnym domu.</summary>
        internal static bool HouseNow() { return IsHouse(_rest); }

        internal static string HouseBonusText()
        {
            return "+" + QSpeed[HouseQ].ToString("0", Inv) + "% speed and +" + QMorale[HouseQ].ToString("0", Inv) + " morale until dusk, the wounded heal faster while you sleep";
        }

        /// <summary>"Sleep in your bed" w domu gracza (zwykle menu): stan noclegu jak HostSleep, ale jakosci dobrego domu. false - bez stanu.</summary>
        internal static bool HouseSleep(string distId, string uid, string name)
        {
            try
            {
                if (!On || distId == null || uid == null) return false;
                long now = NowMin();
                _rest = new Rest { Phase = 1, Q = HouseQ, PlayerQ = HouseQ, TownId = distId, TownName = name ?? uid, HostUid = uid, CommitMin = now,
                                   Speed = QSpeed[HouseQ], Morale = QMorale[HouseQ], Sleep = QSleep[HouseQ], Heal = QHeal[HouseQ], Price = false };
                return true;
            }
            catch (Exception e) { Stumble("HouseSleep", e); return false; }
        }

        /// <summary>Teksty noclegu w wiosce gracza: dom (dobry dom) albo dach wiesniakow (wspolna izba).</summary>
        private static string HostWhere(Rest r) { return IsHouse(r) ? "in your own house" : "under your villagers' roofs"; }
    }
}
