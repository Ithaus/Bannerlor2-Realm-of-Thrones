using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// KROK 3.4 (plan do konca moda, etap 3 - szczelnosc): BK BEZ ZLOTA Z NICZEGO I W NICOSC. Trzy przeplywy (dekompilacja BannerKings.dll
    /// i BKROTPatch.dll; rozpoznanie paczki 169, docs/rozpoznanie-2026-10-08/paczka-169-wynik-2.md E1-E3), kazdy za swoim wylacznikiem:
    ///
    ///  1. RYNEK WSI (BkVillageMarketPaysWhatItHas). BKBuildingsBehavior.OnDailyTickSettlement -> HandleVillage: wies z Rynkiem
    ///     (budynek bannerkings_marketplace) kupuje od miasta lub zamku Bound po 1 sztuce kazdego towaru z nadwyzka podazy (podaz > popyt),
    ///     ktorego sama nie wytwarza, za cene do 5% swojej kiesy. Budzet nie maleje w petli, wiec wies "placi" wiecej, niz ma:
    ///     SettlementComponent.ChangeGold obcina kiese do 0, a kasa Bound dostaje pelna cene - roznica z niczego (bieg 457 dob: srednio
    ///     +449 zl na dobe, ostatnie 8 tygodni +1 670, najwiecej +4 557). Teraz: ta sama petla co w BK (kolejnosc, budzet 5%, warunki,
    ///     cena targu), ale sztuka tylko wtedy, gdy wies MA jej cene; kasa dostaje dokladnie to, co wies zaplacila. Towar jak w BK znika
    ///     z polki (zjadaja go wiesniacy - zaplacili). Prefiks na OnDailyTickSettlement (tylko wsie; to sluchacz zdarzenia - JIT go nie wkleja)
    ///     zastepuje BK w calosci; miasta i zamki - BK bez zmian (dla nich HandleVillage nic nie robi).
    ///  2. SPRZEDAZ NIEWOLNIKOW (BkSlaveSalePaidByTown). BKPartyBehavior.SellSurplusSlaves (dekret "decision_slaves_sell"): nadwyzka niewolnikow
    ///     znika z ludnosci osady, a glowa rodu wlasciciela dostaje ChangeHeroGold(+cena) z niczego. Teraz placi kasa tej osady (miasto albo zamek;
    ///     najwyzej to, co moze wydac - CastlePurse.Spendable, ta sama regula co skup od partii): zloto BK cofniete, w zamian
    ///     GiveGoldAction osada -> wlasciciel. Kasa uboga - wlasciciel dostaje mniej (niewolnicy i tak odeszli - decyzja BK zapadla wczesniej).
    ///     Bieg 457 dob: dekret nie zapadl ani razu (okno ksiegi 0 wywolan) - zmiana dla 100% przypadkow, dzis glownie gracz.
    ///  3. WYPOSAZENIE RYCERZA (BkKnightKitPaidToTown). BKROTPatch EvaluateRecruitKnightPatch.Prefix (zawsze false na BKClanBehavior.
    ///     EvaluateRecruitKnight) placi za rynsztunek nowego rycerza clan.Leader.ChangeHeroGold(-cena) - w nicosc. Cena liczona jest na targu
    ///     Bound pierwszej wsi rodu (BKClanBehavior.GetPrice(val.Village.Bound, ...)), wiec tam idzie zaplata: transpiler zamienia to jedno
    ///     wywolanie na KnightKitPay (GiveGoldAction pan -> Bound). Bieg 457 dob: 20 599 wywolan, 0 rycerzy - dzis 0 zl.
    /// Ksiega obiegu: stare pozycje ("rynek wsi" netto, "sprzedaz niewolnikow" z niczego, "wyposazenie rycerza (BKROTPatch)" w nicosc) maja
    /// pokazac 0 - przelewy ida zdarzeniem gry (okna migawek odejmuja zdarzenia) albo w parze wies -> kasa w oknie "rynek-wsi"; para nowych
    /// przeplywow w czesci "szczelnosc BK (3.4)" linii "Obieg: BK i BetterEconomy" i w linii "Szczelnosc BK (3.4)".
    /// Wyjatki liczone per obiekt (potkniecia), pierwszy na miejsce do logu; zadnego globalnego wylacznika.
    /// </summary>
    internal static class BkSeal34
    {
        internal static bool VillageOn { get { var s = Settings.Current; return s != null && s.BkVillageMarketPaysWhatItHas; } }
        internal static bool SlavesOn { get { var s = Settings.Current; return s != null && s.BkSlaveSalePaidByTown; } }
        internal static bool KnightOn { get { var s = Settings.Current; return s != null && s.BkKnightKitPaidToTown; } }
        internal static bool VillageHooked, SlavesHooked, KnightHooked;
        private static int _knightIlCalls = -1;            // ile wywolan ChangeHeroGold znalazl transpiler (oczekiwane 1)

        // BK (refleksja, raz na sesje): BannerKingsConfig.Instance.PopulationManager.GetPopData / GetProductions, PopulationData.VillageData,
        // VillageData.GetBuildingLevel, DefaultVillageBuildings.Instance.Marketplace
        private static PropertyInfo _cfgInstance, _cfgPopMgr, _popVillageData, _vbInstance, _vbMarketplace;
        private static MethodInfo _getPopData, _getProductions, _getBuildingLevel;
        private static object _marketplace;

        // ------------------------------------------------------------ liczniki doby
        private static int _vBuys, _vVillages, _vSkip;
        private static long _vPaid, _vSkipValue, _vCastle;
        private static int _sSales; private static long _sGain, _sPaid;
        private static int _kKits, _kNoMarket; private static long _kPaid, _kFallback;
        private static int _stumbles;
        private static readonly HashSet<string> _errOnce = new HashSet<string>();

        private static void ClearDay()
        {
            _vBuys = _vVillages = _vSkip = 0; _vPaid = _vSkipValue = _vCastle = 0;
            _sSales = 0; _sGain = _sPaid = 0;
            _kKits = _kNoMarket = 0; _kPaid = _kFallback = 0;
            _stumbles = 0;
            BkSupplyMarket.ClearDay();
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_errOnce.Add(where)) Log.Error("BkSeal34." + where, e);
        }

        // ------------------------------------------------------------ 1. rynek wsi
        /// <summary>Prefiks BKBuildingsBehavior.OnDailyTickSettlement(Settlement): dla wsi zastepuje BK (HandleVillage) ta sama petla z jednym
        /// warunkiem wiecej - wies placi tylko to, co ma. Okno ksiegi obiegu "rynek-wsi" (Priority.First) mierzy kiese wsi i kase Bound wokol nas.</summary>
        public static bool VillageTickPrefix(Settlement __0)
        {
            if (!VillageOn || __0 == null || !__0.IsVillage) return true;
            try { RunVillage(__0); }
            catch (Exception e) { Stumble("RynekWsi", e); }
            return false;
        }

        private static void RunVillage(Settlement st)
        {
            var village = st.Village;
            if (village == null || village.VillageState != Village.VillageStates.Normal) return;   // BK: VillageState != 0 - nic
            object cfg = _cfgInstance.GetValue(null, null);
            object pm = cfg != null ? _cfgPopMgr.GetValue(cfg, null) : null;
            object data = pm != null ? _getPopData.Invoke(pm, new object[] { st }) : null;
            if (data == null) return;                                                               // BK: GetPopData == null - nic
            object vd = _popVillageData.GetValue(data, null);
            object mkt = Marketplace();
            if (vd == null || mkt == null) return;
            int level = Convert.ToInt32(_getBuildingLevel.Invoke(vd, new[] { mkt }), CultureInfo.InvariantCulture);
            if (level <= 0) return;
            var bound = village.Bound;
            var town = bound != null ? bound.Town : null;
            if (town == null) return;
            var md = town.MarketData;
            var made = Produced(_getProductions.Invoke(pm, new[] { data }));
            int budget = (int)(village.Gold * 0.05f);                                               // BK: 5% kiesy - nie maleje w petli (jak w BK)
            var roster = town.Owner.ItemRoster;
            bool any = false;
            for (int i = roster.Count - 1; i >= 0; i--)
            {
                if (budget <= 10) break;
                var e = roster.GetElementCopyAtIndex(i);
                var el = e.EquipmentElement;
                var item = el.Item;
                if (item == null || item.ItemCategory == null || made.Contains(item)) continue;
                var cd = md.GetCategoryData(item.ItemCategory);
                if (cd.Supply <= cd.Demand) continue;
                int price = md.GetPrice(el, null, false, null);
                if (budget < price) continue;
                if (village.Gold < price) { _vSkip++; _vSkipValue += price; continue; }             // 3.4: dotad kasa dostawala te cene z niczego
                village.ChangeGold(-price);
                town.ChangeGold(price);
                roster.AddToCounts(el, -1);
                _vBuys++; _vPaid += price; any = true;
                if (town.IsCastle) _vCastle += price;
            }
            if (any) _vVillages++;
        }

        private static object Marketplace()
        {
            if (_marketplace != null) return _marketplace;
            var inst = _vbInstance.GetValue(null, null);
            _marketplace = inst != null ? _vbMarketplace.GetValue(inst, null) : null;
            return _marketplace;
        }

        /// <summary>List&lt;(ItemObject, float)&gt; z PopulationManager.GetProductions - zbior towarow wsi (BK: productions.Any(x => x.Item1 == item)).</summary>
        private static HashSet<ItemObject> Produced(object list)
        {
            var set = new HashSet<ItemObject>();
            var en = list as System.Collections.IEnumerable;
            if (en == null) return set;
            foreach (var o in en)
            {
                if (o == null) continue;
                if (o is ValueTuple<ItemObject, float> vt) { if (vt.Item1 != null) set.Add(vt.Item1); continue; }
                var f = o.GetType().GetField("Item1");
                var it = f != null ? f.GetValue(o) as ItemObject : null;
                if (it != null) set.Add(it);
            }
            return set;
        }

        // ------------------------------------------------------------ 2. sprzedaz niewolnikow
        internal struct SlaveState { public bool On; public Hero H; public long G0; }

        public static void SlavesPre(Settlement __0, out SlaveState __state)
        {
            __state = default(SlaveState);
            try
            {
                if (!SlavesOn || __0 == null || __0.Town == null || __0.OwnerClan == null) return;
                var h = __0.OwnerClan.Leader;
                if (h == null) return;
                __state.H = h; __state.G0 = h.Gold; __state.On = true;
            }
            catch (Exception e) { Stumble("SlavesPre", e); }
        }

        /// <summary>Postfiks (biegnie przed finalizerem okna ksiegi obiegu): przyrost kiesy wlasciciela bez zdarzenia = cena BK z niczego.</summary>
        public static void SlavesPost(Settlement __0, SlaveState __state)
        {
            if (!__state.On) return;
            try
            {
                long d = (long)__state.H.Gold - __state.G0;   // w SellSurplusSlaves jedyna zmiana kiesy to ChangeHeroGold(+cena) BK
                if (d <= 0) return;
                int gain = (int)Math.Min(d, int.MaxValue);
                __state.H.ChangeHeroGold(-gain);                // zloto z niczego cofniete
                int pay = Math.Min(gain, Math.Max(0, CastlePurse.Spendable(__0.Town)));
                if (pay > 0) GiveGoldAction.ApplyForSettlementToCharacter(__0, __state.H, pay, true);   // kasa osady -> wlasciciel (zdarzenie gry)
                _sSales++; _sGain += gain; _sPaid += pay;
            }
            catch (Exception e) { Stumble("SlavesPost", e); }
        }

        // ------------------------------------------------------------ 3. wyposazenie rycerza (BKROTPatch)
        /// <summary>Transpiler EvaluateRecruitKnightPatch.Prefix (BKROTPatch): jedyne wywolanie Hero.ChangeHeroGold(int) -> KnightKitPay.
        /// Inna liczba wywolan (inna wersja BKROT) - kod bez zmian, w logu startowym BRAK.</summary>
        public static IEnumerable<CodeInstruction> KnightTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var list = new List<CodeInstruction>(instructions);
            var target = AccessTools.Method(typeof(Hero), "ChangeHeroGold", new[] { typeof(int) });
            var helper = AccessTools.Method(typeof(BkSeal34), nameof(KnightKitPay));
            int n = 0;
            if (target != null) foreach (var c in list) if (c.Calls(target)) n++;
            _knightIlCalls = n;
            if (n != 1 || helper == null) return list;
            foreach (var c in list) if (c.Calls(target)) { c.opcode = OpCodes.Call; c.operand = helper; }
            KnightHooked = true;
            return list;
        }

        /// <summary>Zamiast clan.Leader.ChangeHeroGold(-cena) w BKROT: zaplata za rynsztunek na targ Bound pierwszej wsi rodu (ten sam, na ktorym
        /// BKROT liczy cene). Wylaczone, dodatnia kwota albo brak targu - jak dotad (ChangeHeroGold).</summary>
        public static void KnightKitPay(Hero leader, int change)
        {
            if (leader == null) return;
            Settlement market = null;
            if (change < 0 && KnightOn)
            {
                try { market = KitMarket(leader.Clan); }
                catch (Exception e) { Stumble("KitMarket", e); market = null; }
                if (market == null) { _kNoMarket++; _kFallback += -(long)change; }
            }
            if (market == null) { leader.ChangeHeroGold(change); return; }
            int pay = Math.Min(-change, leader.Gold);
            if (pay > 0) GiveGoldAction.ApplyForCharacterToSettlement(leader, market, pay, true);   // pan -> kasa targu (zdarzenie gry)
            _kKits++; _kPaid += pay;
        }

        /// <summary>BKROT: val = clan.Settlements.FirstOrDefault(x => x.IsVillage); cena na val.Village.Bound.</summary>
        private static Settlement KitMarket(Clan clan)
        {
            if (clan == null || clan.Settlements == null) return null;
            foreach (var s in clan.Settlements)
            {
                if (s == null || !s.IsVillage) continue;
                var b = s.Village != null ? s.Village.Bound : null;
                return b != null && b.SettlementComponent != null ? b : null;
            }
            return null;
        }

        // ------------------------------------------------------------ log
        /// <summary>MoneyLedger.ObiegBkLine: para nowych przeplywow dnia (bez zerowania - zeruje Daily po ksiedze pieniadza).</summary>
        internal static string ObiegPart()
        {
            try
            {
                var inv = CultureInfo.InvariantCulture;
                long sup = BkSupplyMarket.DayTown + BkSupplyMarket.DayCastle + BkSupplyMarket.DayVillage;
                var sb = new StringBuilder(256);
                sb.Append(" | szczelnosc BK (3.4) [P] (przelewy w parze): zaopatrzenie BK rody -> kasy osad ").Append(Flag(BkSupplyMarket.Hooked, BkSupplyMarket.On, sup))
                  .Append(", rynek wsi kiesy wsi -> kasy ").Append(Flag(VillageHooked, VillageOn, _vPaid))
                  .Append(" (pominiete bez zlota ").Append(_vSkipValue.ToString(inv)).Append(" - dawniej z niczego)")
                  .Append(", niewolnicy kasy osad -> panowie ").Append(Flag(SlavesHooked, SlavesOn, _sPaid)).Append(" (cena BK ").Append(_sGain.ToString(inv)).Append(')')
                  .Append(", wyposazenie rycerza pan -> kasa targu ").Append(Flag(KnightHooked, KnightOn, _kPaid));
                return sb.ToString();
            }
            catch (Exception e) { Stumble("ObiegPart", e); return " | szczelnosc BK (3.4): blad"; }
        }

        private static string Flag(bool hooked, bool on, long v)
        {
            if (!hooked) return "- (BRAK latki)";
            if (!on) return "- (wylaczone)";
            return v.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>Linia doby (ArmouryBehavior, PO ksiedze pieniadza, ktora czyta te liczniki) i zerowanie licznikow.</summary>
        internal static void Daily()
        {
            try
            {
                if (Campaign.Current == null) return;
                var inv = CultureInfo.InvariantCulture;
                int day = (int)CampaignTime.Now.ToDays - 1;
                var sb = new StringBuilder(1024);
                sb.Append("Szczelnosc BK (3.4): dzien ").Append(day).Append(" | ").Append(BkSupplyMarket.Part());
                sb.Append(" | rynek wsi: ");
                if (!VillageHooked) sb.Append("latka nie jest wpieta (BK jak dotad)");
                else if (!VillageOn) sb.Append("WYLACZONE w ustawieniach (BK jak dotad)");
                else sb.Append(_vBuys).Append(" sztuk w ").Append(_vVillages).Append(" wsiach, kiesy wsi -> kasy ").Append(_vPaid.ToString(inv)).Append(" (w tym zamki ").Append(_vCastle.ToString(inv))
                       .Append("); pominiete, bo wies nie miala ceny: ").Append(_vSkip).Append(" sztuk za ").Append(_vSkipValue.ToString(inv)).Append(" (dawniej kasa dostawala je z niczego)");
                sb.Append(" | niewolnicy: ");
                if (!SlavesHooked) sb.Append("latka nie jest wpieta (BK jak dotad)");
                else if (!SlavesOn) sb.Append("WYLACZONE w ustawieniach (BK jak dotad)");
                else sb.Append(_sSales).Append(" sprzedazy, cena BK ").Append(_sGain.ToString(inv)).Append(", zaplacily kasy osad ").Append(_sPaid.ToString(inv))
                       .Append(" (niedoplata - uboga kasa ").Append((_sGain - _sPaid).ToString(inv)).Append(')');
                sb.Append(" | wyposazenie rycerza: ");
                if (!KnightHooked) sb.Append("latka nie jest wpieta (BKROT jak dotad; wywolan ChangeHeroGold w kodzie BKROT ").Append(_knightIlCalls).Append(')');
                else if (!KnightOn) sb.Append("WYLACZONE w ustawieniach (BKROT jak dotad)");
                else sb.Append(_kKits).Append(" rycerzy, pan -> kasa targu ").Append(_kPaid.ToString(inv)).Append("; bez targu (jak dotad, w nicosc) ").Append(_kNoMarket).Append(" za ").Append(_kFallback.ToString(inv));
                if (_stumbles > 0) sb.Append(" | potkniecia ").Append(_stumbles);
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally { ClearDay(); }
        }

        // ------------------------------------------------------------ wpiecie
        internal static void ApplyAll(Harmony h)
        {
            var done = new List<string>();
            var miss = new List<string>();
            // 1. rynek wsi
            try
            {
                var tBld = QuartermasterLaw.FindType("BannerKings.Behaviours.BKBuildingsBehavior");
                var tCfg = QuartermasterLaw.FindType("BannerKings.BannerKingsConfig");
                var tPm = QuartermasterLaw.FindType("BannerKings.Managers.PopulationManager");
                var tVb = QuartermasterLaw.FindType("BannerKings.Managers.Populations.Villages.DefaultVillageBuildings");
                var tick = tBld != null ? AccessTools.Method(tBld, "OnDailyTickSettlement", new[] { typeof(Settlement) }) : null;
                _cfgInstance = tCfg != null ? AccessTools.Property(tCfg, "Instance") : null;
                _cfgPopMgr = tCfg != null ? AccessTools.Property(tCfg, "PopulationManager") : null;
                _getPopData = tPm != null ? AccessTools.Method(tPm, "GetPopData", new[] { typeof(Settlement) }) : null;
                _getProductions = tPm != null && _getPopData != null ? AccessTools.Method(tPm, "GetProductions", new[] { _getPopData.ReturnType }) : null;
                _popVillageData = _getPopData != null ? AccessTools.Property(_getPopData.ReturnType, "VillageData") : null;
                _getBuildingLevel = _popVillageData != null ? AccessTools.Method(_popVillageData.PropertyType, "GetBuildingLevel", new[] { typeof(TaleWorlds.CampaignSystem.Settlements.Buildings.BuildingType) }) : null;
                _vbInstance = tVb != null ? (AccessTools.Property(tVb, "Instance") ?? (tVb.BaseType != null ? tVb.BaseType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static) : null)) : null;
                _vbMarketplace = tVb != null ? AccessTools.Property(tVb, "Marketplace") : null;
                var why = tick == null ? "BKBuildingsBehavior.OnDailyTickSettlement" : _cfgInstance == null || _cfgPopMgr == null ? "BannerKingsConfig.Instance/PopulationManager"
                        : _getPopData == null || _getProductions == null ? "PopulationManager.GetPopData/GetProductions" : _popVillageData == null || _getBuildingLevel == null ? "VillageData.GetBuildingLevel"
                        : _vbInstance == null || _vbMarketplace == null ? "DefaultVillageBuildings.Marketplace" : null;
                if (why != null) miss.Add("rynek wsi BRAK (" + why + ") - BK jak dotad");
                else
                {
                    h.Patch(tick, prefix: new HarmonyMethod(typeof(BkSeal34), nameof(VillageTickPrefix)));
                    VillageHooked = true; done.Add("rynek wsi");
                }
            }
            catch (Exception e) { miss.Add("rynek wsi BRAK (blad: " + e.Message + ")"); }
            // 2. sprzedaz niewolnikow
            try
            {
                var tParty = QuartermasterLaw.FindType("BannerKings.Behaviours.BKPartyBehavior");
                var sell = tParty != null ? AccessTools.Method(tParty, "SellSurplusSlaves", new[] { typeof(Settlement) }) : null;
                if (sell == null) miss.Add("niewolnicy BRAK (BKPartyBehavior.SellSurplusSlaves) - BK jak dotad");
                else
                {
                    h.Patch(sell, prefix: new HarmonyMethod(typeof(BkSeal34), nameof(SlavesPre)), postfix: new HarmonyMethod(typeof(BkSeal34), nameof(SlavesPost)));
                    SlavesHooked = true; done.Add("niewolnicy");
                }
            }
            catch (Exception e) { miss.Add("niewolnicy BRAK (blad: " + e.Message + ")"); }
            // 3. wyposazenie rycerza (BKROTPatch)
            try
            {
                var tKn = QuartermasterLaw.FindType("BKROTPatch.Patches.EvaluateRecruitKnightPatch");
                var pre = tKn != null ? AccessTools.Method(tKn, "Prefix") : null;
                if (pre == null) miss.Add("rycerz BRAK (BKROTPatch EvaluateRecruitKnightPatch.Prefix) - BKROT jak dotad");
                else
                {
                    h.Patch(pre, transpiler: new HarmonyMethod(typeof(BkSeal34), nameof(KnightTranspiler)));
                    if (KnightHooked) done.Add("rycerz");
                    else miss.Add("rycerz BRAK (wywolan ChangeHeroGold w BKROT: " + _knightIlCalls + ", oczekiwane 1) - BKROT jak dotad");
                }
            }
            catch (Exception e) { miss.Add("rycerz BRAK (blad: " + e.Message + ")"); }
            Log.Info("BkSeal34 (3.4): BK bez zlota z niczego i w nicosc - wpiete: " + (done.Count > 0 ? string.Join(", ", done.ToArray()) : "nic")
                     + (miss.Count > 0 ? "; " + string.Join("; ", miss.ToArray()) : "")
                     + "; ustawienia: rynek wsi " + (VillageOn ? "wl." : "WYL.") + ", niewolnicy " + (SlavesOn ? "wl." : "WYL.") + ", rycerz " + (KnightOn ? "wl." : "WYL.")
                     + "; linia doby \"Szczelnosc BK (3.4)\".");
        }
    }
}
