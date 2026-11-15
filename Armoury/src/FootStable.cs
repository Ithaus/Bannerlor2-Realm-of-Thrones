using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Threading;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// 537 E6 (projekt docs/projekty/KONIE-UPRZEZE.md rozdz. 4 "osobna stajnia piechoty", 6 i 7; Jeff 15.11, D1: "konie uzywane przez piechote sa w stash,
    /// sa tam przenoszone aby nie mieszaly sie z konmi nieuzywanymi i uprzezami nieuzywanymi"). Tylko druzyna gracza (D3). Klucze FootmenMountsStable (6a-6c)
    /// i StableRidersFirst (6d).
    ///  6a - wlasny ItemRoster "Footmen's mounts" (kon i uprzaz par jadacych pod piechota z ladunku), zapis "1|id,mod,n;..." Z MODYFIKATOREM (DTE go gubi);
    ///       otwierany jak stash (tryb Stash - bez XP za "discard") opcja w menu miasta i naszego obozu (arm_camp_wait - zwykle menu, nie menu oczekiwania).
    ///  6b - przenoszenie TYLKO w bezpiecznych chwilach: Pump z paska mapy (watek glowny, co 0.2 s porownanie, najwyzej raz na 1 s), gdy aktywny jest stan
    ///       mapy (zaden ekran nie wisi), bez misji, bitwy, rozmowy i otwartej zbrojowni (Escrow). Powody: zamkniecie dowolnego okna ekwipunku/handlu/lupu
    ///       (postfiks CloseInventoryPresentation), zamkniecie naszego okna (tylko ZWROT sztuk spoza par - gracz mogl wyjac pary do sprzedazy), doba, koniec
    ///       bitwy gracza, zmiana skladu (werbunek, straty, awans - najwyzej raz na godzine gry), wejscie na morze i zejscie na lad, wylaczenie klucza.
    ///       Docelowa stajnia = sztuki z LADUNKU i STAJNI w parach jadacych pod piechota (MarchPairs.Rec.RiddenBy bez jazdy). Reszta wraca do ladunku.
    ///       Na morzu (nikt nie jedzie) i przy kluczu NIE - stajnia pusta (wszystko do ladunku).
    ///  6c - czytelnicy NumberOfMounts gry: kon w stajni wypada z licznika gry, wiec: udzwig "Footmen's mounts" (20 kg za konia - to, co gra dawala za luzaka;
    ///       juki przy siodle juz w "Ridden horses (saddlebags)" przez MarchPairs.CapRiders), pasza (WinterRealm.AnimalNeed - konie stajni jedza jak z ladunku),
    ///       predkosc/stado/kolumna z rekordu MarchPairs (zrodlo SStable), stado wsi / mostu / lupu miasta / Perk512 i dawna regula luzakow (MountsOf).
    ///       Ekran druzyny (awans na jezdzca placi koniem i uprzeza z ladunku - Stables/SaddleLaw): przed otwarciem stajnia wraca do ladunku, po zamknieciu
    ///       przeliczenie. Przegrana bitwa (D8): przed lupem gry stajnia wraca do ladunku - gra lupi ja jak ladunek (zwyciezca dostaje sztuki - nic nie znika).
    ///  6d - pierwszenstwo jazdy: para pod jezdzcem bez wlasnej pary (rodzaj KindCavWalk - jezdziec wzorca, ktoremu podglad DTE nie dal konia z uprzeza)
    ///       idzie ze stajni (i z ladunku przy pelnym przeliczeniu) do zbrojowni DTE jako wklad gracza (ksiega StockDeposit) - DTE posadzi go na niej w bitwie.
    ///       Tylko z podgladem DTE i przy TroopsFightWithOwnKitOnly. Ze zbrojowni nic nie wraca do stajni (sztuka wolna w zbrojowni to para D11).
    /// Bledy liczone (Stumble), bez wylacznika; nic co klatke (Pump: porownania, praca tylko przy powodzie).
    /// </summary>
    internal static class FootStable
    {
        private const string P = "Stajnia piechoty (537 E6): ";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // powody przeliczenia (bity)
        private const int RFull = 1, RReturn = 2, RDaily = 4, RBattle = 8, RMembers = 16, RSea = 32, ROff = 64, RAfterDump = 128, RLoad = 256;

        /// <summary>Migawka stajni dla innych watkow (udzwig, pasza, stado) - podmieniana w calosci na watku glownym po kazdej zmianie.</summary>
        internal sealed class Snap
        {
            public int Mounts, Harness, Other, Pieces;
            public ItemObject[] It; public ItemModifier[] Md; public int[] N;
            public static readonly Snap Empty = new Snap { It = new ItemObject[0], Md = new ItemModifier[0], N = new int[0] };
        }

        private static ItemRoster _roster = new ItemRoster();
        private static string _importText;                       // napis z zapisu - przedmioty przy Resolve (OnSessionLaunched)
        private static readonly List<string> _unresolved = new List<string>();   // wpisy, ktorych przedmiotu nie ma w grze - zapisywane dalej (nic nie ginie)
        private static volatile Snap _snap = Snap.Empty;
        private static bool _screen, _oursClosing, _told;
        private static int _pending;
        private static int _lastAt = Environment.TickCount - 60000;
        private static int _memberVer = int.MinValue;
        private static double _memberHour = -1;
        private static bool _seaKnown, _lastSea;
        private static int _rebal, _rebalMoved, _toStable, _toCargo, _toArm, _dumps, _dumpPieces, _fails, _logged, _resolvedN, _resolvedBad;
        private static int _dRebal, _dToStable, _dToCargo, _dToArm, _dDumps, _dAllow;
        private static int _stumbles;
        private static string _lastLine = "-";
        private static readonly HashSet<string> _errSites = new HashSet<string>();
        private static bool _hooked;
        private static string _hooks = "nie wpiete";
        private static Harmony _h;
        private static TextObject _txCap, _txName;
        private static TextObject TxCap { get { return _txCap ?? (_txCap = new TextObject("{=!}Footmen's mounts")); } }
        private static TextObject TxName { get { return _txName ?? (_txName = new TextObject("{=!}Footmen's mounts")); } }

        /// <summary>6a-6c: klucz FootmenMountsStable (NOWY). Stajnia dziala tylko razem z MarchPairsSpeed - bez niego gra liczylaby konie stajni jako brak koni.
        /// Recenzja 537 (4): takze z RiddenHarnessWeightless i TroopsFightWithOwnKitOnly - bez nich uprzaz w stajni nie wazylaby wcale (dawna regula wazy
        /// uprzaz par), a kon stajni mialby 20 kg zamiast 20 + juki (CapRiders -1, RidersOld wedlug wzorca) - udzwig zalezalby od tego, gdzie stoi kon.
        /// Przy wylaczonym ktoryms kluczu stajnia oddaje wszystko do ladunku w nastepnej bezpiecznej chwili (ROff).</summary>
        internal static bool On { get { var s = Settings.Current; return s != null && s.FootmenMountsStable && s.MarchPairsSpeed && s.RiddenHarnessWeightless && s.TroopsFightWithOwnKitOnly; } }
        /// <summary>6d: klucz StableRidersFirst (NOWY).</summary>
        private static bool RidersFirst { get { var s = Settings.Current; return s != null && s.StableRidersFirst; } }

        internal static void Reset()
        {
            _roster = new ItemRoster(); _importText = null; _unresolved.Clear(); _snap = Snap.Empty;
            _screen = false; _oursClosing = false; _told = false; _pending = 0; _lastAt = Environment.TickCount - 60000;
            _memberVer = int.MinValue; _memberHour = -1; _seaKnown = false; _lastSea = false;
            _rebal = 0; _rebalMoved = 0; _toStable = 0; _toCargo = 0; _toArm = 0; _dumps = 0; _dumpPieces = 0; _fails = 0; _logged = 0; _resolvedN = 0; _resolvedBad = 0;
            _dRebal = 0; _dToStable = 0; _dToCargo = 0; _dToArm = 0; _dDumps = 0; _dAllow = 0;
            _stumbles = 0; _lastLine = "-";
            lock (_errSites) _errSites.Clear();
        }

        private static void Stumble(string where, Exception e)
        {
            Interlocked.Increment(ref _stumbles);
            bool first; lock (_errSites) first = _errSites.Add(where);
            if (first) { try { Log.Error("FootStable." + where, e); } catch { } }
        }

        // ---------------------------------------------------------------- odczyt (rekord MarchPairs, czytelnicy NumberOfMounts)
        /// <summary>Roster stajni do rekordu MarchPairs (watek glowny). Rozwiazuje odlozony zapis przy pierwszym uzyciu.</summary>
        internal static ItemRoster ForRecord() { EnsureResolved(); return _roster; }

        /// <summary>Wersja stajni (klucz pamieci rekordu MarchPairs).</summary>
        internal static int Version { get { try { return _roster.VersionNo; } catch { return -1; } } }

        /// <summary>Migawka (dowolny watek).</summary>
        internal static Snap Current { get { return _snap; } }

        /// <summary>Konie wierzchowe w stajni partii (gracz; inne partie 0) - dopisywane tam, gdzie liczy sie konie partii z NumberOfMounts. Dowolny watek.</summary>
        internal static int MountsOf(MobileParty mp) { return mp != null && mp == MobileParty.MainParty ? _snap.Mounts : 0; }

        /// <summary>Okno stajni otwarte (panel "Ridden by" po stronie OtherInventory).</summary>
        internal static bool ScreenOpen { get { return _screen; } }

        private static void RebuildSnap()
        {
            try
            {
                var r = _roster;
                var it = new List<ItemObject>(); var md = new List<ItemModifier>(); var n = new List<int>();
                int mounts = 0, har = 0, oth = 0, pcs = 0;
                for (int i = 0; i < r.Count; i++)
                {
                    var el = r.GetElementCopyAtIndex(i);
                    var x = el.EquipmentElement.Item;
                    if (x == null || el.Amount <= 0) continue;
                    it.Add(x); md.Add(el.EquipmentElement.ItemModifier); n.Add(el.Amount); pcs += el.Amount;
                    if (KitPreview.RidingMount(x)) mounts += el.Amount;
                    else if (x.ItemType == ItemObject.ItemTypeEnum.HorseHarness) har += el.Amount;
                    else oth += el.Amount;
                }
                _snap = new Snap { Mounts = mounts, Harness = har, Other = oth, Pieces = pcs, It = it.ToArray(), Md = md.ToArray(), N = n.ToArray() };
            }
            catch (Exception e) { Stumble("RebuildSnap", e); }
        }

        // ---------------------------------------------------------------- 6a: zapis "1|id,mod,n;..." (z modyfikatorem)
        internal static string Export()
        {
            try
            {
                if (_importText != null) return _importText;   // zapis wczytany, a jeszcze nie rozwiazany - oddany bez zmian
                var sb = new StringBuilder("1|");
                var r = _roster;
                for (int i = 0; i < r.Count; i++)
                {
                    var el = r.GetElementCopyAtIndex(i);
                    var it = el.EquipmentElement.Item;
                    if (it == null || el.Amount <= 0 || string.IsNullOrEmpty(it.StringId)) continue;
                    var md = el.EquipmentElement.ItemModifier;
                    sb.Append(it.StringId).Append(',').Append(md != null ? md.StringId : "").Append(',').Append(el.Amount.ToString(Inv)).Append(';');
                }
                foreach (var u in _unresolved) sb.Append(u).Append(';');
                return sb.ToString();
            }
            catch (Exception e) { Stumble("Export", e); return _importText ?? ""; }
        }

        internal static void Import(string text)
        {
            _roster = new ItemRoster(); _unresolved.Clear(); _snap = Snap.Empty;
            _importText = string.IsNullOrEmpty(text) ? null : text;
            _pending |= RLoad;
        }

        private static void EnsureResolved()
        {
            if (_importText == null) return;
            string t = _importText;
            _importText = null;
            try
            {
                int ok = 0, bad = 0, noMod = 0;
                if (t.StartsWith("1|", StringComparison.Ordinal))
                {
                    var om = MBObjectManager.Instance;
                    foreach (var e in t.Substring(2).Split(';'))
                    {
                        if (string.IsNullOrEmpty(e)) continue;
                        var f = e.Split(',');
                        int n;
                        ItemObject it = null;
                        if (f.Length == 3 && int.TryParse(f[2], NumberStyles.Integer, Inv, out n) && n > 0 && om != null) it = om.GetObject<ItemObject>(f[0]);
                        else n = 0;
                        if (it == null) { _unresolved.Add(e); bad++; continue; }
                        ItemModifier md = null;
                        if (f[1].Length > 0) { md = om.GetObject<ItemModifier>(f[1]); if (md == null) noMod++; }
                        _roster.AddToCounts(new EquipmentElement(it, md), n);
                        ok += n;
                    }
                }
                else if (t.Length > 0) { _unresolved.Add(t); bad++; }
                _resolvedN = ok; _resolvedBad = bad;
                RebuildSnap();
                Log.Info(P + "wczytana z zapisu: sztuk " + ok + " (konie " + _snap.Mounts + ", uprzeze " + _snap.Harness + ", inne " + _snap.Other + ")"
                         + (bad > 0 ? ", wpisow bez przedmiotu w grze " + bad + " (zostaja w zapisie)" : "") + (noMod > 0 ? ", bez znanego modyfikatora " + noMod + " (sztuka bez stanu)" : "") + ".");
            }
            catch (Exception e) { Stumble("Resolve", e); }
        }

        // ---------------------------------------------------------------- start sesji: menu, latki, zdarzenia
        /// <summary>ArmouryBehavior.OnSessionLaunched (PO NightRest.AddMenus - menu arm_camp_wait juz stoi): opcje menu, latki (raz na proces), zdarzenie konca bitwy.</summary>
        internal static void SessionStart(CampaignGameStarter starter)
        {
            EnsureResolved();
            bool town = false, camp = false;
            if (starter != null)
            {
                try { starter.AddGameMenuOption("town", "arm_footmen_mounts_town", "{=!}Footmen's mounts", MenuCond, MenuCons, false, 10); town = true; }
                catch (Exception e) { Stumble("Menu.town", e); }
                try { starter.AddGameMenuOption("arm_camp_wait", "arm_footmen_mounts_camp", "{=!}Footmen's mounts", MenuCond, MenuCons, false, 6); camp = true; }
                catch (Exception e) { Stumble("Menu.camp", e); }
            }
            EnsureHooks();
            bool ev = false;
            try
            {
                object owner = null;
                try { owner = Campaign.Current != null ? Campaign.Current.GetCampaignBehavior<ArmouryBehavior>() : null; } catch { owner = null; }
                if (owner != null) { CampaignEvents.MapEventEnded.AddNonSerializedListener(owner, OnMapEventEnded); ev = true; }
            }
            catch (Exception e) { Stumble("Events", e); }
            _pending |= RLoad;
            try
            {
                Log.Info(P + "start sesji | klucze FootmenMountsStable " + Yes(Settings.Current != null && Settings.Current.FootmenMountsStable) + " (dziala: " + Yes(On)
                         + " - wymaga MarchPairsSpeed, RiddenHarnessWeightless i TroopsFightWithOwnKitOnly), StableRidersFirst " + Yes(RidersFirst) + " | opcja \"Footmen's mounts\" w menu miasta " + Yes(town)
                         + ", obozu arm_camp_wait " + Yes(camp) + " | latki: " + _hooks + " | koniec bitwy " + Yes(ev) + " | stajnia: konie " + _snap.Mounts
                         + ", uprzeze " + _snap.Harness + ", inne " + _snap.Other + (_unresolved.Count > 0 ? ", wpisow bez przedmiotu " + _unresolved.Count : "") + ".");
            }
            catch { }
        }

        private static string Yes(bool b) { return b ? "TAK" : "NIE"; }

        private static void EnsureHooks()
        {
            if (_hooked) return;
            _hooked = true;
            _h = new Harmony("com.jeff.armoury.footstable537");
            var parts = new List<string>();
            try
            {
                var m = AccessTools.DeclaredMethod(typeof(InventoryScreenHelper), "CloseInventoryPresentation");
                if (m == null) parts.Add("BRAK InventoryScreenHelper.CloseInventoryPresentation (zamkniecie okna nie przelicza - doba i sklad tak)");
                else { _h.Patch(m, postfix: new HarmonyMethod(typeof(FootStable), nameof(ClosePost)) { priority = Priority.Low }); parts.Add("zamkniecie okna ekwipunku"); }
            }
            catch (Exception e) { parts.Add("zamkniecie okna: blad " + e.GetType().Name); Stumble("Hook.Close", e); }
            try
            {
                var m = AccessTools.DeclaredMethod(typeof(MapEvent), "LootDefeatedPartyItems");
                if (m == null) parts.Add("BRAK MapEvent.LootDefeatedPartyItems (D8: stajnia nie ginie z ladunkiem)");
                else { _h.Patch(m, prefix: new HarmonyMethod(typeof(FootStable), nameof(LootPre)) { priority = Priority.Normal }); parts.Add("lup przegranej (D8)"); }
            }
            catch (Exception e) { parts.Add("lup: blad " + e.GetType().Name); Stumble("Hook.Loot", e); }
            try
            {
                var m = AccessTools.DeclaredMethod(typeof(PartyScreenHelper), "OpenScreenAsNormal");
                if (m == null) parts.Add("BRAK PartyScreenHelper.OpenScreenAsNormal (awans na jezdzca nie widzi koni stajni)");
                else { _h.Patch(m, prefix: new HarmonyMethod(typeof(FootStable), nameof(PartyPre)) { priority = Priority.Normal }); parts.Add("ekran druzyny"); }
            }
            catch (Exception e) { parts.Add("ekran druzyny: blad " + e.GetType().Name); Stumble("Hook.Party", e); }
            try
            {
                var m = AccessTools.DeclaredMethod(typeof(DefaultInventoryCapacityModel), "CalculateInventoryCapacity");
                if (m == null) parts.Add("BRAK DefaultInventoryCapacityModel.CalculateInventoryCapacity (udzwig koni stajni)");
                else { _h.Patch(m, postfix: new HarmonyMethod(typeof(FootStable), nameof(CapPostfix)) { priority = Priority.LowerThanNormal }); parts.Add("udzwig koni stajni"); }
            }
            catch (Exception e) { parts.Add("udzwig: blad " + e.GetType().Name); Stumble("Hook.Cap", e); }
            _hooks = string.Join(", ", parts.ToArray());
        }

        private static void OnMapEventEnded(MapEvent me)
        {
            try { if (me != null && me.IsPlayerMapEvent) _pending |= RBattle; } catch (Exception e) { Stumble("MapEventEnded", e); }
        }

        // ---------------------------------------------------------------- 6a: okno "Footmen's mounts"
        private static bool MenuCond(MenuCallbackArgs a)
        {
            try
            {
                if (!On) return false;
                a.optionLeaveType = GameMenuOption.LeaveType.Trade;
                var sn = _snap;
                string tip = "Horses and harness your footmen ride on the march, kept apart from your baggage: " + sn.Mounts + " horses, " + sn.Harness + " harness"
                             + (sn.Other > 0 ? ", " + sn.Other + " other pieces (back to the baggage when you close it)" : "") + ". They weigh nothing and eat like the herd. "
                             + "Take a pair out to sell or give it away - when you close, nothing more is moved in; the pairs are counted again later "
                             + "(on closing your inventory, at dawn, after a battle or when your men change).";
                if (QuartermasterEscrow.Active) { a.IsEnabled = false; tip = "The troops' armoury is open."; }
                else if (MobileParty.MainParty != null && MobileParty.MainParty.IsCurrentlyAtSea) { a.IsEnabled = false; tip = "At sea nobody rides - the horses and harness are with your baggage."; }
                else if (MobileParty.MainParty != null && Aside(MobileParty.MainParty)) { a.IsEnabled = false; tip = "While you march in another commander's army the horses and harness stay with your baggage, where his column counts them."; }
                a.Tooltip = new TextObject("{=!}" + tip);
                return true;
            }
            catch (Exception e) { Stumble("MenuCond", e); return false; }
        }

        private static void MenuCons(MenuCallbackArgs a) { try { Open(); } catch (Exception e) { Stumble("Open", e); } }

        private static IMarketData Market()
        {
            try
            {
                var m = AccessTools.DeclaredMethod(typeof(InventoryScreenHelper), "GetCurrentMarketDataForPlayer");
                var md = m != null ? m.Invoke(null, null) as IMarketData : null;
                if (md != null) return md;
            }
            catch (Exception e) { Stumble("Market", e); }
            try { var st = Settlement.CurrentSettlement; if (st != null && st.Town != null) return st.Town.MarketData; } catch { }
            return null;
        }

        /// <summary>Okno jak stash (tryb Stash: bez XP za "discard" i bez zdarzenia wyrzucenia - tryb Default z pusta druga partia to "Discard").
        /// Zamkniecie - OnDone (takze Cancel: gra cofa rostery sama).</summary>
        private static void Open()
        {
            var mp = MobileParty.MainParty;
            var gsm = Game.Current != null ? Game.Current.GameStateManager : null;
            if (!On || mp == null || gsm == null || _screen) return;
            EnsureResolved();
            var md = Market();
            if (md == null) { Log.Player("The footmen's mounts cannot be opened here.", true); return; }
            var state = gsm.CreateState<InventoryState>();
            state.InventoryMode = InventoryScreenHelper.InventoryMode.Stash;
            var logic = new InventoryLogic((PartyBase)null);
            logic.Initialize(_roster, mp, false, false, CharacterObject.PlayerCharacter, InventoryScreenHelper.InventoryCategoryType.None, md, false, state.InventoryMode, TxName);
            state.InventoryLogic = logic;
            state.DoneLogicExtrasDelegate = OnDone;
            _screen = true;
            gsm.PushState(state);
            var sn = _snap;
            Log.Info(P + "okno otwarte | konie " + sn.Mounts + ", uprzeze " + sn.Harness + ", inne " + sn.Other + ".");
        }

        private static void OnDone()
        {
            try
            {
                _screen = false; _oursClosing = true;
                _pending |= RReturn;   // tylko zwrot sztuk spoza par (gracz mogl wyjac pary do sprzedazy)
                RebuildSnap();
                var sn = _snap;
                Log.Info(P + "okno zamkniete | konie " + sn.Mounts + ", uprzeze " + sn.Harness + ", inne " + sn.Other + " (inne wroca do ladunku).");
            }
            catch (Exception e) { Stumble("OnDone", e); }
        }

        /// <summary>Postfiks InventoryScreenHelper.CloseInventoryPresentation (kazde okno ekwipunku, handlu, lupu, stash): po zamknieciu pelne przeliczenie.
        /// Tylko flaga - przenoszenie w Pump (po powrocie na mape, okno juz zdjete).</summary>
        public static void ClosePost()
        {
            try
            {
                var gsm = Game.Current != null ? Game.Current.GameStateManager : null;
                if (gsm != null && gsm.ActiveState is InventoryState) return;   // DoneLogic odmowil - okno wisi
                if (_oursClosing) { _oursClosing = false; return; }
                _pending |= RFull;
            }
            catch (Exception e) { Stumble("ClosePost", e); }
        }

        // ---------------------------------------------------------------- 6c / D8: przegrana bitwa i ekran druzyny - stajnia do ladunku
        /// <summary>Prefiks MapEvent.LootDefeatedPartyItems(winnerParties, defeatedParties): druzyna gracza pokonana - stajnia do ladunku PRZED lupem gry
        /// (D8: stajnia ginie tak jak ladunek - te same szanse, sztuki do zwyciezcy).</summary>
        public static void LootPre(MBReadOnlyList<MapEventParty> __1)
        {
            try
            {
                if (__1 == null || _snap.Pieces <= 0) return;
                var main = PartyBase.MainParty;
                foreach (var mep in __1) if (mep != null && mep.Party == main) { Dump("przegrana bitwa - stajnia do ladunku przed lupem gry (D8)"); return; }
            }
            catch (Exception e) { Stumble("LootPre", e); }
        }

        /// <summary>Prefiks PartyScreenHelper.OpenScreenAsNormal: awans na jezdzca placi koniem i uprzeza z ladunku (Stables.PayInHorses, SaddleLaw.PayGate) -
        /// stajnia do ladunku przed otwarciem ekranu (okno jeszcze nie stoi), przeliczenie po powrocie na mape.</summary>
        public static void PartyPre()
        {
            try
            {
                if (_snap.Pieces <= 0 || QuartermasterEscrow.Active) return;
                if (Dump("ekran druzyny - awanse na jezdzca widza konie i uprzeze stajni") > 0)
                {
                    try { MarchPairs.Main(true); } catch (Exception e) { Stumble("PartyPre.Main", e); }   // rekord par od razu zgodny z ladunkiem (waga, predkosc)
                }
            }
            catch (Exception e) { Stumble("PartyPre", e); }
        }

        /// <summary>Cala stajnia do ladunku (watek glowny). Zwraca liczbe sztuk.</summary>
        private static int Dump(string why)
        {
            EnsureResolved();
            var mp = MobileParty.MainParty;
            var bag = mp != null ? mp.ItemRoster : null;
            if (bag == null) return 0;
            int n = 0;
            var r = _roster;
            for (int i = r.Count - 1; i >= 0; i--)
            {
                var el = r.GetElementCopyAtIndex(i);
                if (el.EquipmentElement.Item == null || el.Amount <= 0) continue;
                r.AddToCounts(el.EquipmentElement, -el.Amount);
                bag.AddToCounts(el.EquipmentElement, el.Amount);
                n += el.Amount;
            }
            RebuildSnap();
            if (n > 0)
            {
                _dumps++; _dDumps++; _dumpPieces += n;
                _pending |= RAfterDump;
                Line(why + ": do ladunku " + n + " szt.");
            }
            return n;
        }

        // ---------------------------------------------------------------- 6b: bezpieczne chwile (pasek mapy)
        /// <summary>MarchHud.Sync (pasek mapy, watek glowny, co 0.2 s): porownania; przeliczenie tylko przy powodzie i najwyzej raz na 1 s.</summary>
        internal static void Pump()
        {
            try
            {
                if (Campaign.Current == null) return;
                var mp = MobileParty.MainParty;
                if (mp == null || !mp.IsActive || mp.MemberRoster == null || mp.ItemRoster == null) return;
                var gsm = Game.Current != null ? Game.Current.GameStateManager : null;
                if (gsm == null || !(gsm.ActiveState is MapState)) return;
                if (_screen) { _screen = false; _pending |= RReturn; }   // okno zdjete inna droga niz CloseInventoryPresentation
                if (TaleWorlds.MountAndBlade.Mission.Current != null || QuartermasterEscrow.Active || mp.MapEvent != null) return;
                var cm = Campaign.Current.ConversationManager;
                if (cm != null && (cm.IsConversationInProgress || cm.IsConversationFlowActive)) return;
                EnsureResolved();
                bool sea = Aside(mp);
                if (!_seaKnown || sea != _lastSea) { if (_seaKnown) _pending |= RSea; _seaKnown = true; _lastSea = sea; }
                int mv = mp.MemberRoster.VersionNo;
                if (mv != _memberVer)
                {
                    double h = CampaignTime.Now.ToHours;
                    if (_memberVer == int.MinValue || _memberHour < 0 || h - _memberHour >= 1.0) _pending |= RMembers;
                }
                if (!On && _snap.Pieces > 0) _pending |= ROff;
                if (_pending == 0) return;
                int now = Environment.TickCount;
                if (unchecked(now - _lastAt) < 1000) return;
                _lastAt = now;
                int why = _pending;
                _pending = 0;
                Rebalance(mp, why);
            }
            catch (Exception e) { Stumble("Pump", e); }
        }

        /// <summary>Stajnia pusta (wszystko w ladunku): na morzu (nikt nie jedzie) i w armii innego wodza - predkosc armii liczy model wodza z NumberOfMounts
        /// doczepionych (bez naszych wpisow MarchPairs), wiec konie stajni bylyby dla armii brakiem koni.</summary>
        private static bool Aside(MobileParty mp)
        {
            return mp.IsCurrentlyAtSea || (mp.AttachedTo != null && mp.AttachedTo != mp);
        }

        private static string WhyText(int w)
        {
            var l = new List<string>();
            if ((w & RFull) != 0) l.Add("zamkniete okno ekwipunku");
            if ((w & RReturn) != 0) l.Add("zamkniete okno stajni (tylko zwrot)");
            if ((w & RDaily) != 0) l.Add("doba");
            if ((w & RBattle) != 0) l.Add("koniec bitwy");
            if ((w & RMembers) != 0) l.Add("sklad");
            if ((w & RSea) != 0) l.Add("morze/lad albo armia innego wodza");
            if ((w & ROff) != 0) l.Add("klucz wylaczony");
            if ((w & RAfterDump) != 0) l.Add("po zrzucie do ladunku");
            if ((w & RLoad) != 0) l.Add("start sesji");
            return string.Join("+", l.ToArray());
        }

        /// <summary>Sztuka z ladunku albo ze stajni (nie ze zbrojowni DTE - ta zostaje na miejscu).</summary>
        private static bool Local(byte src) { return src == MarchPairs.SrcCargo || src == MarchPairs.SrcStable; }

        private struct Key : IEquatable<Key>
        {
            public ItemObject It; public ItemModifier Md;
            public bool Equals(Key o) { return ReferenceEquals(It, o.It) && ReferenceEquals(Md, o.Md); }
            public override bool Equals(object o) { return o is Key && Equals((Key)o); }
            public override int GetHashCode() { return (It != null ? It.GetHashCode() : 0) * 31 + (Md != null ? Md.GetHashCode() : 0); }
        }

        private static void Add(Dictionary<Key, int> d, ItemObject it, ItemModifier md, int n)
        {
            if (it == null || n <= 0) return;
            var k = new Key { It = it, Md = md };
            int v; d.TryGetValue(k, out v); d[k] = v + n;
        }

        private static int Have(ItemRoster r, EquipmentElement e)
        {
            int at = r.FindIndexOfElement(e);
            return at >= 0 ? Math.Max(0, r.GetElementNumber(at)) : 0;
        }

        private static int Move(ItemRoster from, ItemRoster to, EquipmentElement e, int n)
        {
            if (n <= 0) return 0;
            int k = Math.Min(n, Have(from, e));
            if (k <= 0) return 0;
            from.AddToCounts(e, -k);
            to.AddToCounts(e, k);
            return k;
        }

        /// <summary>6b + 6d: stajnia = sztuki z ladunku i stajni w parach jadacych pod piechota (rekord MarchPairs, policzony teraz). Watek glowny, stan mapy.</summary>
        private static void Rebalance(MobileParty mp, int why)
        {
            _rebal++; _dRebal++;
            bool on = On, sea = Aside(mp);
            MarchPairs.Rec r = null;
            if (on && !sea)
            {
                r = MarchPairs.Main(true);
                // potkniecie rekordu - bez ruchu; sklad oznaczony jako sprawdzony (nastepna proba przy innym powodzie, nie co sekunde)
                if (r == null || !r.Ok) { _fails++; _memberVer = mp.MemberRoster.VersionNo; _memberHour = CampaignTime.Now.ToHours; return; }
            }
            bool fill = on && !sea && (why & ~(RReturn)) != 0;   // samo zamkniecie okna stajni - tylko zwrot
            var want = new Dictionary<Key, int>();
            var arm = new Dictionary<Key, int>();
            var dte = QuartermasterLaw.DteArmory();
            bool six = on && !sea && RidersFirst && r != null && r.Preview && GarrisonKit.OwnKitOn && dte != null;
            // 6d: ile par wolno oddac zbrojowni - jezdzcy bez wlasnej pary na parach ze stajni/ladunku minus pary juz wolne w zbrojowni (DTE ich nie wydal,
            // wiec kolejne tez by nie wydal). Samoograniczenie: para oddana, a niewydana przez DTE, jest przy nastepnym liczeniu wolna w zbrojowni.
            int allow = 0;
            if (six)
            {
                int kcav = 0;
                foreach (var x in r.RiddenBy)
                    if (x != null && x.Kind == MarchPairs.KindCavWalk && x.N > 0 && (Local(x.HSrc) || Local(x.USrc))) kcav += x.N;
                allow = Math.Max(0, kcav - Math.Min(r.AFreeHorses, r.AFreeHarness));
                _dAllow += allow;
            }
            if (r != null)
                foreach (var x in r.RiddenBy)
                {
                    if (x == null || x.Kind == MarchPairs.KindCav || x.N <= 0) continue;
                    int toArm = six && x.Kind == MarchPairs.KindCavWalk && (Local(x.HSrc) || Local(x.USrc)) ? Math.Min(x.N, allow) : 0;
                    allow -= toArm;
                    int stay = x.N - toArm;
                    if (Local(x.HSrc)) { Add(arm, x.HIt, x.HMd, toArm); Add(want, x.HIt, x.HMd, stay); }
                    if (Local(x.USrc)) { Add(arm, x.UIt, x.UMd, toArm); Add(want, x.UIt, x.UMd, stay); }
                }
            var st = _roster;
            var bag = mp.ItemRoster;
            int toS = 0, toC = 0, toA = 0;
            int before = _snap.Pieces;
            // 6d: pary pod jezdzcami bez wlasnej pary - do zbrojowni DTE (najpierw ze stajni; z ladunku tylko przy pelnym przeliczeniu), wklad gracza w ksiedze
            if (six && arm.Count > 0)
                foreach (var kv in arm)
                {
                    var e = new EquipmentElement(kv.Key.It, kv.Key.Md);
                    int k = Move(st, dte, e, kv.Value);
                    if (fill && k < kv.Value) k += Move(bag, dte, e, kv.Value - k);
                    if (k <= 0) continue;
                    toA += k;
                    try { if (ArmouryBehavior.BookModOn()) ArmouryBehavior.StockDepositKey(e, k); else ArmouryBehavior.StockDeposit(kv.Key.It.StringId, k); }
                    catch (Exception ex) { Stumble("Rebalance.ksiega", ex); }
                }
            // zwrot: w stajni zostaje tyle, ile par jadacych (reszta do ladunku - takze sztuki spoza par i wszystko przy kluczu NIE / na morzu)
            for (int i = st.Count - 1; i >= 0; i--)
            {
                var el = st.GetElementCopyAtIndex(i);
                var it = el.EquipmentElement.Item;
                if (it == null || el.Amount <= 0) continue;
                var k = new Key { It = it, Md = el.EquipmentElement.ItemModifier };
                int w; want.TryGetValue(k, out w);
                int keep = Math.Min(el.Amount, w);
                want[k] = w - keep;
                int back = el.Amount - keep;
                if (back > 0) toC += Move(st, bag, el.EquipmentElement, back);
            }
            // dopelnienie: pary jadace z ladunku do stajni
            if (fill)
                foreach (var kv in want)
                {
                    if (kv.Value <= 0) continue;
                    toS += Move(bag, st, new EquipmentElement(kv.Key.It, kv.Key.Md), kv.Value);
                }
            _memberVer = mp.MemberRoster.VersionNo; _memberHour = CampaignTime.Now.ToHours;
            if (toS + toC + toA <= 0) return;
            _rebalMoved++; _toStable += toS; _toCargo += toC; _toArm += toA; _dToStable += toS; _dToCargo += toC; _dToArm += toA;
            RebuildSnap();
            // pierwsze napelnienie pustej stajni w sesji - jedno zdanie dla gracza (konie z ladunku nie zniknely)
            if (toS > 0 && before == 0 && !_told)
            {
                _told = true;
                try { Log.Player("Your footmen's horses and harness (" + _snap.Mounts + " horses, " + _snap.Harness + " harness) now stand apart in Footmen's mounts - town or camp menu."); } catch { }
            }
            // rekord par od razu zgodny z nowym ukladem (predkosc, waga, udzwig, HUD) - liczony teraz, podglad DTE z pamieci, gdy zbrojownia bez zmian
            MarchPairs.Rec r2 = null;
            try { r2 = MarchPairs.Main(true); } catch (Exception e) { Stumble("Rebalance.Main", e); }
            var sn = _snap;
            Line(WhyText(why) + ": do stajni " + toS + ", do ladunku " + toC + ", do zbrojowni DTE (6d) " + toA + " | stajnia teraz: konie " + sn.Mounts + ", uprzeze "
                 + sn.Harness + ", inne " + sn.Other + (r2 != null && r2.Ok ? " | jedzie piechoty " + r2.Ride + " (na koniach stajni " + r2.RideStableHorse + ", z ladunku "
                 + r2.RideCargoHorse + "), jazda z para " + r2.CavPairs : ""));
        }

        private static void Line(string s)
        {
            _lastLine = s;
            _logged++;
            if (_logged <= 30 || _logged % 50 == 0) Log.Info(P + s + (_logged > 30 ? " (wpis " + _logged + ")" : "") + ".");
        }

        // ---------------------------------------------------------------- 6c: udzwig koni stajni (postfiks DefaultInventoryCapacityModel.CalculateInventoryCapacity)
        /// <summary>Gra daje 20 kg (SpareMountsFactor 2 x waga srednia 10) za luzaka w ladunku (NumberOfMounts) - kon w stajni wypada z licznika, wiec ta sama
        /// kwota tu ("Footmen's mounts"); juki przy siodle (RiderHorseCarryKg) daje MarchPairs.CapRiders ("Ridden horses"), jak koniom zbrojowni. Na ladzie
        /// (jak gra), gracz i armia gracza z includeFollowers. Dowolny watek - tylko migawka.</summary>
        public static void CapPostfix(MobileParty __0, bool __1, bool __6, ref ExplainedNumber __result)
        {
            try
            {
                if (__0 == null || __1) return;
                var sn = _snap;
                if (sn.Mounts <= 0) return;
                var main = MobileParty.MainParty;
                if (main == null || main.IsCurrentlyAtSea) return;
                if (__0 != main && !(__6 && main.AttachedTo == __0)) return;
                __result.Add(sn.Mounts * 20f, TxCap);
            }
            catch (Exception e) { Stumble("CapPostfix", e); }
        }

        // ---------------------------------------------------------------- 6c: pasza (WinterRealm.AnimalNeed)
        /// <summary>Konie stajni gracza do paszy - ta sama stawka co kon z ladunku (zwykly / bojowy). Dowolny watek - migawka.</summary>
        internal static float FeedOf(MobileParty party, float horse, float warhorse, out int heads)
        {
            heads = 0;
            if (party == null || party != MobileParty.MainParty) return 0f;
            var sn = _snap;
            float need = 0f;
            for (int i = 0; i < sn.It.Length; i++)
            {
                var it = sn.It[i];
                if (it == null || sn.N[i] <= 0 || !it.HasHorseComponent || it.HorseComponent == null) continue;
                need += sn.N[i] * (it.ItemCategory == DefaultItemCategories.WarHorse ? warhorse : horse);
                heads += sn.N[i];
            }
            return need;
        }

        // ---------------------------------------------------------------- linia doby
        /// <summary>ArmouryBehavior.OnDailyTick (po MarchPairs.Daily): linia "Stajnia piechoty (537 E6)" i powod przeliczenia "doba".</summary>
        internal static void Daily()
        {
            try
            {
                var sn = _snap;
                var s = Settings.Current;
                var sb = new StringBuilder(600);
                sb.Append(P).Append("dzien ").Append((int)CampaignTime.Now.ToDays - 1)
                  .Append(" | klucze FootmenMountsStable ").Append(Yes(s != null && s.FootmenMountsStable)).Append(" (dziala ").Append(Yes(On)).Append(" - z MarchPairsSpeed, RiddenHarnessWeightless i TroopsFightWithOwnKitOnly)")
                  .Append(", StableRidersFirst ").Append(Yes(RidersFirst))
                  .Append(" | stajnia: konie ").Append(sn.Mounts).Append(", uprzeze ").Append(sn.Harness).Append(", inne ").Append(sn.Other).Append(" (rodzajow ").Append(sn.It.Length).Append(')');
                if (_unresolved.Count > 0) sb.Append(", wpisow zapisu bez przedmiotu ").Append(_unresolved.Count);
                sb.Append(" | doba: przeliczen ").Append(_dRebal).Append(", do stajni ").Append(_dToStable).Append(", do ladunku ").Append(_dToCargo).Append(", do zbrojowni (6d) ").Append(_dToArm).Append(" (dozwolonych par ").Append(_dAllow).Append(')')
                  .Append(", zrzutow do ladunku ").Append(_dDumps)
                  .Append(" | od startu sesji: przeliczen ").Append(_rebal).Append(" (z ruchem ").Append(_rebalMoved).Append(", nieudanych ").Append(_fails).Append("), do stajni ").Append(_toStable)
                  .Append(", do ladunku ").Append(_toCargo).Append(", do zbrojowni ").Append(_toArm).Append(", zrzutow ").Append(_dumps).Append(" (").Append(_dumpPieces).Append(" szt.)")
                  .Append(" | czytelnicy: udzwig Footmen's mounts +").Append((sn.Mounts * 20).ToString(Inv)).Append(" kg");
                try
                {
                    var mp = MobileParty.MainParty;
                    if (mp != null)
                    {
                        int heads; FeedOf(mp, 1f, 1f, out heads);
                        sb.Append(", pasza: koni stajni ").Append(heads).Append(", NumberOfMounts gry ").Append(mp.ItemRoster != null ? mp.ItemRoster.NumberOfMounts : 0)
                          .Append(" + stajnia ").Append(MountsOf(mp));
                    }
                }
                catch (Exception e) { Stumble("Daily.czytelnicy", e); }
                sb.Append(" | latki: ").Append(_hooks).Append(" | ostatni ruch: ").Append(_lastLine)
                  .Append(" | potkniecia ").Append(_stumbles);
                if (_errSites.Count > 0) sb.Append(" (").Append(string.Join(", ", _errSites)).Append(')');
                Log.Info(sb.Append('.').ToString());
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally { _dRebal = 0; _dToStable = 0; _dToCargo = 0; _dToArm = 0; _dDumps = 0; _dAllow = 0; _pending |= RDaily; }
        }
    }
}
