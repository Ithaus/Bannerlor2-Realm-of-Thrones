using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// 342 (Jeff 06.11: "kazdy moze zalozyc armie, nie tylko glowa rodu, bo to bez sensu") - klucz AnyLordArmy342.
    ///  - WODZ (transpiler DefaultArmyManagementCalculationModel.CanLordCreateArmy, klasa deklarujaca - baza SCA, BK, NavalDLC): warunek gry
    ///    "Clan.Leader == LeaderHero" -> CanLead(party): glowa rodu jak gra ALBO (regula 342) lord krolestwa AI z druzyna lordowska, nie gracz i nie rod gracza, nie najem,
    ///    nie w armii, gdy wplyw rodu &gt; 100 x (armie prowadzone przez ludzi rodu + 1) - stala gry 100 (MinimumInfluenceNeededToCreateArmy) na kazda armie rodu.
    ///    Kolejka rodu (342r W1): w danej godzinie probuje tylko jedna druzyna rodu - pierwsza, ktora w tej godzinie zapyta w mysli gry (mysl wolnej druzyny co 6 h -
    ///    AiPartyThinkBehavior), z ustepowaniem druzynie rodu, ktora czekala, a byla obsluzona dawniej. Reszta warunkow gry bez zmian (morze, wplyw &gt; 100, jedzenie,
    ///    liczebnosc, wojna, kandydaci, limit 0.7, prog 1000, sito SCA i ArmsFirst). Wariant gry "pierwsza druzyna rodu, gdy glowa bez druzyny" zostaje.
    ///    Kotwica dokladnie 1 - inaczej nic nie zmieniamy ("BRAK").
    ///  - BUDZET (postfiks First na bazie i 342r W3 postfiks First na nadpisaniu czynnego modelu - SCA / BK dokladaja czlonkow po bazie; tylko wodz z reguly 342):
    ///    czlonkowie w kolejnosci gry, dopoki suma CalculatePartyInfluenceCost &lt;= budzet gry (GetInfluenceBudgetWhileCreatingArmy = 0.7 x wplyw rodu);
    ///    po przycieciu prog 1000 od nowa; pusta lista - false. Rod nie schodzi pod 0.
    ///  - BK (postfiks BKArmyManagementModel.CanCreateArmy - blokada Kingdom.CreateArmy i BKArmyBehavior): false BK -> true tylko dla druzyny z pelna zgoda gry
    ///    w tej godzinie (CanLordCreateArmy true po przycieciu), gdy BK zgadza sie dla glowy jej rodu (prawa ArmyPrivate / LimitedArmyPrivilege / rada - jedyna zniesiona
    ///    roznica to "nie glowa rodu") i przy wolnych miejscach BK (ceil(0.7 x druzyn) - w armiach &gt;= 2). Wewnatrz BK CalculatePartyInfluenceCost i CanLordCreateArmy
    ///    (koszt x2 / x5 i filtr ArmyLegion pytaja CanCreateArmy o czlonka) - bez zmian. Gracz: BK przepuszcza MainHero zawsze; rod gracza poza regula.
    /// </summary>
    internal static class AnyLordArmy342
    {
        private const string Tag = "Armie kazdego lorda (342): ";
        private const float InfluencePerArmy = 100f;   // gra: DefaultArmyManagementCalculationModel.MinimumInfluenceNeededToCreateArmy
        private const float ArmyFloor = 1000f;         // gra: prog sily oblezenia w CanLordCreateArmy
        private static Harmony _h;
        private static bool _tried;
        private static int _anchors = -1;
        private static string _wHead = "nie wpieta", _wBudget = "nie wpieta", _wBk = "nie wpieta", _wAct = "nie wpieta", _wBkIn = "nie wpieta";
        private static Func<DefaultArmyManagementCalculationModel, MobileParty, float> _budget;
        private static MethodInfo _bkCan;                  // BKArmyManagementModel.CanCreateArmy(Hero) - pytanie o glowe rodu (342r W2)
        private static readonly HashSet<MethodBase> _actDone = new HashSet<MethodBase>();
        [ThreadStatic] private static MobileParty _via;    // CanLead dal zgode z reguly 342 w tym wywolaniu CanLordCreateArmy
        [ThreadStatic] private static MobileParty _viaAct; // 342r (W3): ta sama zgoda - przyciecie jeszcze raz po nadpisaniu czynnego modelu
        [ThreadStatic] private static int _bkInner;        // 342r (W2): wewnatrz BK CalculatePartyInfluenceCost / CanLordCreateArmy - BkPost nic nie zmienia
        private static readonly object _lk = new object();
        private static int _dGrant, _dQueue, _dQueueFair, _dLimit, _dTrim, _dTrimEmpty, _dTrimFloor, _dTrimAct, _dBkPass, _dBkHead, _dBkNoGrant, _dHead, _dOther, _dLines, _dStum, _sStum, _dCohOther, _dDispOther;
        // 342r (W1): kolejka rodu "kto pierwszy w godzinie" - rod -> (godzina, druzyna), ktora zajela miejsce rodu w tej godzinie (tylko prawdziwe wywolanie z mysli gry);
        // sprawiedliwosc: druzyna obsluzona w ostatniej dobie ustepuje druzynie rodu, ktora czekala (odmowa kolejki w ostatnich 12 h) i byla obsluzona dawniej
        private static readonly Dictionary<Clan, KeyValuePair<int, MobileParty>> _slot = new Dictionary<Clan, KeyValuePair<int, MobileParty>>();
        private static readonly Dictionary<MobileParty, int> _lastGrant = new Dictionary<MobileParty, int>();
        private static readonly Dictionary<MobileParty, int> _waitH = new Dictionary<MobileParty, int>();
        // 342r (W2): pelna zgoda gry (CanLordCreateArmy true po przycieciu) - druzyna -> godzina; tylko ona przechodzi przez BK CanCreateArmy w tej godzinie
        private static readonly Dictionary<MobileParty, int> _granted = new Dictionary<MobileParty, int>();
        // kolejka rodu: druzyna wyslana na zbiorke (Zelazni w porcie) ma pierwszenstwo w swoim rodzie - 342r (W4): dopoki plynie do tego portu albo w nim stoi, bez armii;
        // okno = droga do portu + 24 h; koniec przy armii rodu i przy nieudanej zbiorce
        private sealed class Pref { public MobileParty P; public Settlement Port; public int Until; }
        private static readonly Dictionary<Clan, Pref> _pref = new Dictionary<Clan, Pref>();

        /// <summary>Druzyna p ma pierwszenstwo kolejki swojego rodu przez hours godzin, dopoki plynie do portu port albo w nim stoi (zbiorka w porcie). Nie rzuca.</summary>
        internal static void Prefer(MobileParty p, Settlement port, int hours)
        {
            try
            {
                var c = p != null && p.LeaderHero != null ? p.LeaderHero.Clan : null;
                if (c == null || port == null) return;
                lock (_lk) _pref[c] = new Pref { P = p, Port = port, Until = NowH + Math.Max(1, hours) };
            }
            catch { }
        }

        /// <summary>Koniec pierwszenstwa druzyny p (nieudana zbiorka). Nie rzuca.</summary>
        internal static void Unprefer(MobileParty p)
        {
            try
            {
                var c = p != null && p.LeaderHero != null ? p.LeaderHero.Clan : null;
                if (c == null) return;
                lock (_lk) { Pref pv; if (_pref.TryGetValue(c, out pv) && pv.P == p) _pref.Remove(c); }
            }
            catch { }
        }

        private static Settings S { get { return Settings.Current; } }
        internal static bool On { get { var s = S; return s != null && s.AnyLordArmy342; } }
        private static int NowH { get { return (int)Math.Floor(CampaignTime.Now.ToHours); } }

        private static void Stumble(string where, Exception e)
        {
            lock (_lk) { _dStum++; _sStum++; }
            try { if (_sStum <= 20) Log.Info(Tag + "potkniecie " + where + ": " + e.GetType().Name + " " + e.Message); } catch { }
        }

        internal static void Reset()
        {
            lock (_lk) { _pref.Clear(); _slot.Clear(); _lastGrant.Clear(); _waitH.Clear(); _granted.Clear(); ZeroDay(); _sStum = 0; }
        }

        private static void ZeroDay()
        {
            _dGrant = _dQueue = _dQueueFair = _dLimit = _dTrim = _dTrimEmpty = _dTrimFloor = _dTrimAct = _dBkPass = _dBkHead = _dBkNoGrant = _dHead = _dOther = _dLines = _dStum = 0;
            _dCohOther = _dDispOther = 0;
        }

        // ================================================================ regula
        /// <summary>Z transpilera (warunek wodza gry): glowa rodu jak gra albo regula 342. Nie rzuca.</summary>
        public static bool CanLead(MobileParty p)
        {
            try
            {
                var h = p != null ? p.LeaderHero : null;
                var c = h != null ? h.Clan : null;
                if (c == null) return false;
                if (c.Leader == h) return true;
                int why;
                if (Rule(p, h, c, true, true, out why)) { _via = p; return true; }
            }
            catch (Exception e) { Stumble("CanLead", e); }
            return false;
        }

        /// <summary>Wariant gry: pierwsza druzyna rodu, gdy glowa rodu bez druzyny (warunek wodza CanLordCreateArmy bez reguly 342).</summary>
        private static bool FirstOfClan(MobileParty p, Clan c)
        {
            return c.Leader != null && c.Leader.PartyBelongedTo == null && c.WarPartyComponents != null && c.WarPartyComponents.Count > 0 && c.WarPartyComponents[0] == p.WarPartyComponent;
        }

        /// <summary>Odczyt (Armoury: Zelazni, powody): czy gra da tej druzynie warunek wodza (glowa rodu, pierwsza druzyna rodu bez glowy w polu, regula 342
        /// z kolejka rodu w tej godzinie - miejsce rodu wolne albo jej). Bez licznikow i bez zajmowania miejsca. Nie rzuca.</summary>
        internal static bool LeadsNow(MobileParty p)
        {
            try
            {
                var h = p != null ? p.LeaderHero : null;
                var c = h != null ? h.Clan : null;
                if (c == null) return false;
                if (c.Leader == h || FirstOfClan(p, c)) return true;
                int why;
                return Rule(p, h, c, false, true, out why);
            }
            catch { return false; }
        }

        /// <summary>Odczyt: druzyna moglaby prowadzic armie wedlug reguly 342 poza kolejka rodu (Armoury: zbiorka Zelaznych w porcie planowana na kilka godzin).</summary>
        internal static bool MayLead(MobileParty p) { int why; return MayLead(p, out why); }

        /// <summary>Jak MayLead; why - powod odmowy (342r D2): 5 najem, 11 limit wplywu rodu, 1 poza regula (wylaczona, rod / krolestwo gracza, nie druzyna lorda, w armii).</summary>
        internal static bool MayLead(MobileParty p, out int why)
        {
            why = 1;
            try
            {
                var h = p != null ? p.LeaderHero : null;
                var c = h != null ? h.Clan : null;
                if (c == null) return false;
                if (c.Leader == h || FirstOfClan(p, c)) { why = 0; return true; }
                return Rule(p, h, c, false, false, out why);
            }
            catch { return false; }
        }

        /// <summary>Wodz z reguly 342 (nie glowa rodu, nie wariant gry "pierwsza druzyna rodu") - czlonkowie przycinani budzetem wplywu gry (IronCands - ten sam szacunek).</summary>
        internal static bool ByRule(MobileParty p)
        {
            try
            {
                var h = p != null ? p.LeaderHero : null;
                var c = h != null ? h.Clan : null;
                return On && c != null && c.Leader != h && !FirstOfClan(p, c);
            }
            catch { return false; }
        }

        /// <summary>Budzet wplywu gry przy zakladaniu armii (GetInfluenceBudgetWhileCreatingArmy czynnego modelu; zapas 0.7 x wplyw rodu).</summary>
        internal static float BudgetOf(MobileParty leader)
        {
            var inst = Campaign.Current != null && Campaign.Current.Models != null ? Campaign.Current.Models.ArmyManagementCalculationModel as DefaultArmyManagementCalculationModel : null;
            return BudgetOf(inst, leader);
        }

        private static float BudgetOf(DefaultArmyManagementCalculationModel inst, MobileParty leader)
        {
            if (_budget != null && inst != null) return _budget(inst, leader);
            var c = leader.LeaderHero != null ? leader.LeaderHero.Clan : null;
            return c != null ? c.Influence * 0.7f : 0f;
        }

        /// <summary>Regula 342 dla lorda, ktory nie jest glowa rodu. count - prawdziwe wywolanie z mysli gry (liczniki, zajecie miejsca rodu); queue - kolejka rodu.
        /// why: 0 zgoda, 1 poza regula, 5 najem, 11 limit wplywu rodu, 12 kolejka rodu.</summary>
        private static bool Rule(MobileParty p, Hero h, Clan c, bool count, bool queue, out int why)
        {
            why = 1;
            if (!On) return false;
            if (!p.IsLordParty || p.IsMainParty || p.Army != null || !p.IsActive) return false;
            var pc = Clan.PlayerClan;
            if (c == pc || c.Leader == null) return false;
            if (c.IsUnderMercenaryService) { why = 5; return false; }
            var k = c.Kingdom;
            if (k == null || p.MapFaction != k || (pc != null && k.RulingClan == pc)) return false;   // krolestwa AI; gracz-wladca decyduje sam
            int armies = 0;
            var al = k.Armies;
            for (int i = 0; i < al.Count; i++)
            {
                var a = al[i];
                var lh = a != null && a.LeaderParty != null ? a.LeaderParty.LeaderHero : null;
                if (lh != null && lh.Clan == c) armies++;
            }
            if (!(c.Influence > InfluencePerArmy * (armies + 1))) { why = 11; if (count) lock (_lk) _dLimit++; return false; }
            if (queue && !QueueOk(p, c, count)) { why = 12; return false; }
            why = 0;
            if (count) lock (_lk) _dGrant++;
            return true;
        }

        /// <summary>342r (W1): kolejka rodu "kto pierwszy w godzinie" (bez zaleznosci od rytmu mysli gry co 6 h i kolejnosci WarPartyComponents). count - zajmuje miejsce.</summary>
        private static bool QueueOk(MobileParty p, Clan c, bool count)
        {
            int hh = NowH;
            lock (_lk)
            {
                Pref pv;
                if (_pref.Count > 0 && _pref.TryGetValue(c, out pv))
                {
                    var q = pv.P;
                    bool live = pv.Until >= hh && q != null && q.IsActive && q.Army == null && q.LeaderHero != null && q.LeaderHero.Clan == c
                                && (q.CurrentSettlement == pv.Port || q.TargetSettlement == pv.Port);
                    if (!live) _pref.Remove(c);
                    else if (q != p) { if (count) _dQueue++; return false; }
                    else { if (count) Take(c, p, hh); return true; }
                }
                KeyValuePair<int, MobileParty> s;
                if (_slot.TryGetValue(c, out s) && s.Key == hh)
                {
                    if (s.Value == p) return true;
                    if (count) { _dQueue++; _waitH[p] = hh; }
                    return false;
                }
                if (!count) return true;
                int lg;
                if (_waitH.Count > 0 && _lastGrant.TryGetValue(p, out lg) && hh - lg < 24)
                {
                    foreach (var kv in _waitH)
                    {
                        var w = kv.Key;
                        if (w == p || kv.Value < hh - 12 || w == null || !w.IsActive || w.Army != null || w.LeaderHero == null || w.LeaderHero.Clan != c) continue;
                        int lw;
                        if (!_lastGrant.TryGetValue(w, out lw) || lw < lg) { _dQueue++; _dQueueFair++; _waitH[p] = hh; return false; }
                    }
                }
                Take(c, p, hh);
                return true;
            }
        }

        private static void Take(Clan c, MobileParty p, int hh)
        {
            _slot[c] = new KeyValuePair<int, MobileParty>(hh, p);
            _lastGrant[p] = hh;
            _waitH.Remove(p);
        }

        // ================================================================ latki
        internal static void SessionStart()
        {
            if (_h == null) _h = new Harmony("com.jeff.armoury.anylord342");
            var args = new[] { typeof(MobileParty), typeof(MBList<MobileParty>).MakeByRefType() };
            if (!_tried)
            {
                _tried = true;
                try
                {
                    var m = AccessTools.DeclaredMethod(typeof(DefaultArmyManagementCalculationModel), "CanLordCreateArmy", args);
                    if (m == null) { _wHead = "BRAK (metoda)"; _wBudget = "BRAK (metoda)"; }
                    else
                    {
                        _h.Patch(m, transpiler: new HarmonyMethod(typeof(AnyLordArmy342), nameof(HeadTranspiler)));
                        _wHead = _anchors == 1 ? "TAK (kotwica 1/1)" : "BRAK (kotwic " + _anchors + "/1)";
                        var bm = AccessTools.Method(typeof(DefaultArmyManagementCalculationModel), "GetInfluenceBudgetWhileCreatingArmy", new[] { typeof(MobileParty) });
                        if (bm != null) _budget = AccessTools.MethodDelegate<Func<DefaultArmyManagementCalculationModel, MobileParty, float>>(bm);
                        _h.Patch(m, postfix: new HarmonyMethod(typeof(AnyLordArmy342), nameof(BudgetPost)) { priority = Priority.First },
                                 finalizer: new HarmonyMethod(typeof(AnyLordArmy342), nameof(CreateFin)));
                        _actDone.Add(m);
                        _wBudget = "TAK" + (_budget != null ? "" : " (budzet 0.7 x wplyw - metoda gry BRAK)");
                    }
                }
                catch (Exception e) { _wHead = "BRAK (" + e.GetType().Name + ")"; Stumble("SessionStart.head", e); }
                try
                {
                    var t = AccessTools.TypeByName("BannerKings.Models.Vanilla.BKArmyManagementModel");
                    var m = t != null ? AccessTools.DeclaredMethod(t, "CanCreateArmy", new[] { typeof(Hero) }) : null;
                    if (t == null) { _wBk = "bez BK"; _wBkIn = "bez BK"; }
                    else if (m == null) _wBk = "BRAK (CanCreateArmy)";
                    else { _bkCan = m; _h.Patch(m, postfix: new HarmonyMethod(typeof(AnyLordArmy342), nameof(BkPost)) { priority = Priority.Last }); _wBk = "TAK"; }
                    if (t != null)
                    {
                        // 342r (W2): BK pyta CanCreateArmy o czlonka w koszcie wezwania (x2 / x5) i w filtrze ArmyLegion - tam BkPost nic nie zmienia
                        int n = 0;
                        var mc = AccessTools.DeclaredMethod(t, "CalculatePartyInfluenceCost", new[] { typeof(MobileParty), typeof(MobileParty) });
                        var ml = AccessTools.DeclaredMethod(t, "CanLordCreateArmy", args);
                        foreach (var mm in new[] { mc, ml })
                        {
                            if (mm == null) continue;
                            _h.Patch(mm, prefix: new HarmonyMethod(typeof(AnyLordArmy342), nameof(BkInPre)) { priority = Priority.First },
                                     finalizer: new HarmonyMethod(typeof(AnyLordArmy342), nameof(BkInFin)));
                            n++;
                        }
                        _wBkIn = n == 2 ? "TAK (2/2)" : "BRAK (" + n + "/2)";
                    }
                }
                catch (Exception e) { _wBk = "BRAK (" + e.GetType().Name + ")"; Stumble("SessionStart.bk", e); }
            }
            // 342r (W3): nadpisanie CanLordCreateArmy czynnego modelu (SCA, BK) - po jego tresci przyciecie budzetem jeszcze raz (BK AddInfluenceBonusParties doklada platnych)
            try
            {
                var model = Campaign.Current != null && Campaign.Current.Models != null ? Campaign.Current.Models.ArmyManagementCalculationModel : null;
                var am = model != null ? AccessTools.DeclaredMethod(model.GetType(), "CanLordCreateArmy", args) : null;
                if (model == null) _wAct = "BRAK (model)";
                else if (am == null || am.DeclaringType == typeof(DefaultArmyManagementCalculationModel)) _wAct = "model gry (bez nadpisania)";
                else if (_actDone.Add(am))
                {
                    _h.Patch(am, postfix: new HarmonyMethod(typeof(AnyLordArmy342), nameof(ActPost)) { priority = Priority.First },
                             finalizer: new HarmonyMethod(typeof(AnyLordArmy342), nameof(CreateFin)));
                    _wAct = "TAK (" + am.DeclaringType.FullName + ")";
                }
            }
            catch (Exception e) { _wAct = "BRAK (" + e.GetType().Name + ")"; Stumble("SessionStart.act", e); }
            try
            {
                var s = S;
                if (s != null && s.LogEnabled)
                    Log.Info(Tag + "start | klucz " + (On ? "TAK" : "NIE") + " | wodz gry (CanLordCreateArmy) " + _wHead + " | budzet wplywu " + _wBudget + " | budzet po modelu czynnym " + _wAct
                             + " | BK CanCreateArmy " + _wBk + " (wewnatrz kosztu/listy BK " + _wBkIn + ")"
                             + " | model armii " + (Campaign.Current != null && Campaign.Current.Models != null && Campaign.Current.Models.ArmyManagementCalculationModel != null ? Campaign.Current.Models.ArmyManagementCalculationModel.GetType().FullName : "-") + ".");
            }
            catch (Exception e) { Stumble("SessionStart.line", e); }
        }

        private static bool IsCall(CodeInstruction c, MethodInfo m)
        {
            return m != null && (c.opcode == OpCodes.Callvirt || c.opcode == OpCodes.Call) && Equals(c.operand, m);
        }

        /// <summary>ldarg.1; LeaderHero; Clan; Leader; ldarg.1; LeaderHero; beq L -> ldarg.1; call CanLead; brtrue L (etykiety zostaja). Kotwica dokladnie 1.</summary>
        public static IEnumerable<CodeInstruction> HeadTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var L = new List<CodeInstruction>(instructions);
            try
            {
                var gLH = AccessTools.PropertyGetter(typeof(MobileParty), "LeaderHero");
                var gC = AccessTools.PropertyGetter(typeof(Hero), "Clan");
                var gL = AccessTools.PropertyGetter(typeof(Clan), "Leader");
                var repl = AccessTools.Method(typeof(AnyLordArmy342), nameof(CanLead));
                int hits = 0, at = -1;
                for (int i = 0; i + 6 < L.Count; i++)
                {
                    if (L[i].opcode == OpCodes.Ldarg_1 && IsCall(L[i + 1], gLH) && IsCall(L[i + 2], gC) && IsCall(L[i + 3], gL) && L[i + 4].opcode == OpCodes.Ldarg_1
                        && IsCall(L[i + 5], gLH) && (L[i + 6].opcode == OpCodes.Beq_S || L[i + 6].opcode == OpCodes.Beq)) { hits++; at = i; }
                }
                _anchors = hits;
                if (hits != 1 || repl == null) return L;
                var target = L[at + 6].operand;
                L[at + 1].opcode = OpCodes.Call; L[at + 1].operand = repl;
                L[at + 2].opcode = OpCodes.Brtrue; L[at + 2].operand = target;
                for (int j = 3; j <= 6; j++) { L[at + j].opcode = OpCodes.Nop; L[at + j].operand = null; }
            }
            catch (Exception e) { _anchors = -2; Stumble("HeadTranspiler", e); }
            return L;
        }

        /// <summary>342r (W3): czlonkowie w kolejnosci listy, dopoki suma kosztow wezwania &lt;= budzet gry; potem prog 1000 od nowa. Zwraca nowy wynik. Wolajacy lapie wyjatki.</summary>
        private static bool TrimToBudget(DefaultArmyManagementCalculationModel inst, MobileParty leader, MBList<MobileParty> list, bool act)
        {
            float budget = BudgetOf(inst, leader);
            var am = Campaign.Current.Models.ArmyManagementCalculationModel;
            int sum = 0, keep = 0;
            for (; keep < list.Count; keep++)
            {
                int cost = am.CalculatePartyInfluenceCost(leader, list[keep]);
                if (sum + cost > budget) break;
                sum += cost;
            }
            if (keep >= list.Count) return true;
            lock (_lk) { _dTrim++; if (act) _dTrimAct++; }
            list.RemoveRange(keep, list.Count - keep);
            if (list.Count == 0) { lock (_lk) _dTrimEmpty++; return false; }
            var k = leader.MapFaction as Kingdom;
            if (k != null && k.Settlements.Count > 0)
            {
                float cs = leader.Party.GetCustomStrength(BattleSideEnum.Attacker, MapEvent.PowerCalculationContext.Siege);
                foreach (var m in list) if (m != null) cs += m.Party.GetCustomStrength(BattleSideEnum.Attacker, MapEvent.PowerCalculationContext.Siege);
                if (cs < ArmyFloor) { list.Clear(); lock (_lk) _dTrimFloor++; return false; }
            }
            return true;
        }

        private static void Grant(MobileParty p, bool ok)
        {
            lock (_lk) { if (ok) _granted[p] = NowH; else _granted.Remove(p); }
        }

        /// <summary>Postfiks (First, przed ArmsFirst) bazy: wodz z reguly 342 - czlonkowie do budzetu wplywu gry, potem prog 1000. Nie rzuca.</summary>
        public static void BudgetPost(DefaultArmyManagementCalculationModel __instance, MobileParty __0, ref MBList<MobileParty> __1, ref bool __result)
        {
            if (!ReferenceEquals(_via, __0)) return;
            _via = null;
            try
            {
                if (__result && __1 != null && __1.Count > 0) __result = TrimToBudget(__instance, __0, __1, false);
                bool act = __instance != null && __instance.GetType() != typeof(DefaultArmyManagementCalculationModel) && _wAct.StartsWith("TAK", StringComparison.Ordinal);
                _viaAct = __result && act ? __0 : null;
                Grant(__0, __result);
            }
            catch (Exception e) { Stumble("BudgetPost", e); }
        }

        /// <summary>342r (W3): postfiks (First) nadpisania czynnego modelu - po jego tresci (SCA zdejmuje, BK doklada czlonkow) budzet jeszcze raz. Nie rzuca.</summary>
        public static void ActPost(DefaultArmyManagementCalculationModel __instance, MobileParty __0, ref MBList<MobileParty> __1, ref bool __result)
        {
            if (!ReferenceEquals(_viaAct, __0)) return;
            _viaAct = null;
            try
            {
                if (__result && __1 != null && __1.Count > 0) __result = TrimToBudget(__instance, __0, __1, true);
                Grant(__0, __result);
            }
            catch (Exception e) { Stumble("ActPost", e); }
        }

        /// <summary>342r (D4): finalizer CanLordCreateArmy - wyjatek w tresci nie zostawia zgody w watku (wyjatek idzie dalej bez zmian).</summary>
        public static Exception CreateFin(Exception __exception)
        {
            if (__exception != null) { _via = null; _viaAct = null; }
            return __exception;
        }

        public static void BkInPre() { _bkInner++; }
        public static Exception BkInFin(Exception __exception) { if (_bkInner > 0) _bkInner--; return __exception; }

        /// <summary>Postfiks BKArmyManagementModel.CanCreateArmy: false BK -> true dla druzyny z pelna zgoda gry w tej godzinie (342r W2), gdy BK zgadza sie dla glowy
        /// jej rodu i ma wolne miejsca (jak BK: ceil(0.7 x druzyn) - w armiach &gt;= 2). Nie rzuca.</summary>
        public static void BkPost(object __instance, Hero __0, ref bool __result)
        {
            if (__result || __0 == null || _bkInner > 0 || !On) return;
            try
            {
                if (__0 == Hero.MainHero) return;
                var p = __0.PartyBelongedTo;
                var c = __0.Clan;
                if (p == null || c == null || p.LeaderHero != __0 || c.Leader == __0 || c.Leader == null) return;
                int g;
                lock (_lk) { if (!_granted.TryGetValue(p, out g) || g != NowH) { _dBkNoGrant++; return; } }
                var k = c.Kingdom;
                if (k == null) return;
                if (_bkCan != null && __instance != null)
                {
                    bool head;
                    _bkInner++;
                    try { head = (bool)_bkCan.Invoke(__instance, new object[] { c.Leader }); }
                    finally { _bkInner--; }
                    if (!head) { lock (_lk) _dBkHead++; return; }
                }
                var al = k.Armies;
                if (al.Count > 0)
                {
                    int n = k.WarPartyComponents != null ? k.WarPartyComponents.Count : 0, inA = 0;
                    for (int i = 0; i < al.Count; i++) { var a = al[i]; if (a != null && a.Parties != null) inA += a.Parties.Count; }
                    if ((int)Math.Ceiling(n * 0.7f) - inA < 2) return;
                }
                __result = true;
                lock (_lk) _dBkPass++;
            }
            catch (Exception e) { Stumble("BkPost", e); }
        }

        // ================================================================ zdarzenia i linia doby
        internal static void OnArmyCreated(Army a)
        {
            try
            {
                var lh = a != null && a.LeaderParty != null ? a.LeaderParty.LeaderHero : null;
                var c = lh != null ? lh.Clan : null;
                if (c == null || c == Clan.PlayerClan) return;
                bool head = c.Leader == lh;
                lock (_lk)
                {
                    _pref.Remove(c);   // 342r (W4): armia rodu powstala - koniec pierwszenstwa zbiorki
                    _granted.Remove(a.LeaderParty);
                    if (head) _dHead++; else _dOther++;
                    if (_dLines >= 20) return;
                    _dLines++;
                }
                var s = S;
                if (s != null && s.LogEnabled)
                    Log.Info(Tag + "armia | dzien " + (int)CampaignTime.Now.ToDays + " | " + lh.Name + " (rod " + c.Name + ", glowa rodu " + (head ? "TAK" : "NIE") + ") | " + (a.Kingdom != null ? a.Kingdom.StringId : "-")
                             + " | druzyn " + a.Parties.Count + " | wplyw rodu teraz " + c.Influence.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + ".");
            }
            catch (Exception e) { Stumble("OnArmyCreated", e); }
        }

        /// <summary>342r (D5): armie innych lordow (nie glow rodow) rozwiazane - wszystkie i z braku spojnosci (oplaty za spojnosc dziela wplyw rodu).</summary>
        internal static void OnArmyDispersed(Army a, Army.ArmyDispersionReason reason, bool isPlayers)
        {
            try
            {
                if (isPlayers || a == null || a.LeaderParty == null) return;
                var lh = a.LeaderParty.LeaderHero;
                var c = lh != null ? lh.Clan : null;
                if (c == null || c == Clan.PlayerClan || c.Leader == lh) return;
                lock (_lk) { _dDispOther++; if (reason == Army.ArmyDispersionReason.CohesionDepleted) _dCohOther++; }
            }
            catch (Exception e) { Stumble("OnArmyDispersed", e); }
        }

        private static void Prune(int hNow)
        {
            var old = new List<Clan>();
            foreach (var kv in _pref) if (kv.Key == null || kv.Value.Until < hNow || kv.Value.P == null || !kv.Value.P.IsActive) old.Add(kv.Key);
            foreach (var c in old) _pref.Remove(c);
            old.Clear();
            foreach (var kv in _slot) if (kv.Key == null || kv.Value.Key < hNow - 1) old.Add(kv.Key);
            foreach (var c in old) _slot.Remove(c);
            var oldP = new List<MobileParty>();
            foreach (var kv in _lastGrant) if (kv.Key == null || !kv.Key.IsActive || kv.Value < hNow - 48) oldP.Add(kv.Key);
            foreach (var p in oldP) _lastGrant.Remove(p);
            oldP.Clear();
            foreach (var kv in _waitH) if (kv.Key == null || !kv.Key.IsActive || kv.Value < hNow - 24) oldP.Add(kv.Key);
            foreach (var p in oldP) _waitH.Remove(p);
            oldP.Clear();
            foreach (var kv in _granted) if (kv.Key == null || kv.Value < hNow - 2) oldP.Add(kv.Key);
            foreach (var p in oldP) _granted.Remove(p);
        }

        internal static void Daily()
        {
            try
            {
                lock (_lk) Prune(NowH);
                var s = S;
                if (s == null || !s.LogEnabled || Campaign.Current == null) return;
                int armies = 0, ruleNow = 0, kWith = 0, maxK = 0, maxKn = 0, maxKin = 0, maxClan = 0, flood = 0, negHead = 0, negOther = 0;
                string maxKid = "-", maxClanId = "-";
                var pc = Clan.PlayerClan;
                var perClan = new Dictionary<Clan, int>();
                var negSeen = new HashSet<Clan>();
                foreach (var k in Kingdom.All)
                {
                    if (k == null || k.IsEliminated || k.Armies == null || k.Armies.Count == 0) continue;
                    if (pc != null && k.RulingClan == pc) continue;
                    int n = k.WarPartyComponents != null ? k.WarPartyComponents.Count : 0, inA = 0, ka = 0;
                    perClan.Clear();
                    foreach (var a in k.Armies)
                    {
                        if (a == null) continue;
                        ka++; if (a.Parties != null) inA += a.Parties.Count;
                        var lh = a.LeaderParty != null ? a.LeaderParty.LeaderHero : null;
                        var c = lh != null ? lh.Clan : null;
                        if (c == null) continue;
                        bool head = c.Leader == lh;
                        if (!head) ruleNow++;
                        int v; perClan.TryGetValue(c, out v); perClan[c] = v + 1;
                        if (v + 1 > maxClan) { maxClan = v + 1; maxClanId = c.StringId; }
                        if (c.Influence < 0f && negSeen.Add(c)) { if (head) negHead++; else negOther++; }
                    }
                    armies += ka; kWith++;
                    if (ka > maxK) { maxK = ka; maxKid = k.StringId; maxKn = n; maxKin = inA; }
                    // zalew: wiecej druzyn w armiach niz limit gry (ceil(0.7 x druzyn)) albo armie srednio ponizej 3 druzyn przy wiecej niz 2 armiach
                    if (inA > (int)Math.Ceiling(n * 0.7f) || (ka > 2 && ka * 3 > inA)) flood++;
                }
                lock (_lk)
                {
                    var inv = System.Globalization.CultureInfo.InvariantCulture;
                    var sb = new StringBuilder(500);
                    sb.Append(Tag).Append("dzien ").Append((int)CampaignTime.Now.ToDays).Append(" | klucz ").Append(On ? "TAK" : "NIE")
                      .Append(" | latki: wodz gry ").Append(_wHead).Append(", budzet ").Append(_wBudget).Append(", budzet po modelu czynnym ").Append(_wAct).Append(", BK ").Append(_wBk)
                      .Append(" (wewnatrz BK ").Append(_wBkIn).Append(")")
                      .Append(" | zgody reguly ").Append(_dGrant).Append(", poza kolejka rodu ").Append(_dQueue).Append(" (ustapil czekajacemu ").Append(_dQueueFair).Append("), limit wplywu rodu ").Append(_dLimit)
                      .Append(", rodow z miejscem kolejki ").Append(_slot.Count).Append(", druzyn obsluzonych 48 h ").Append(_lastGrant.Count)
                      .Append(" | przyciete budzetem ").Append(_dTrim).Append(" (po modelu czynnym ").Append(_dTrimAct).Append(", puste ").Append(_dTrimEmpty).Append(", prog 1000 ").Append(_dTrimFloor)
                      .Append("), BK przepuscil ").Append(_dBkPass).Append(" (BK nie dla glowy rodu ").Append(_dBkHead).Append(", bez zgody gry ").Append(_dBkNoGrant).Append(")")
                      .Append(" | zalozone dzis: glowy rodow ").Append(_dHead).Append(", inni lordowie ").Append(_dOther)
                      .Append(" | rozwiazane dzis armie innych lordow ").Append(_dDispOther).Append(" (brak spojnosci ").Append(_dCohOther).Append(")")
                      .Append(" | teraz: armii ").Append(armies).Append(" (inni lordowie ").Append(ruleNow).Append("), krolestw z armia ").Append(kWith)
                      .Append(", maks armii krolestwa ").Append(maxK).Append(" (").Append(maxKid).Append(", druzyn ").Append(maxKn).Append(", w armiach ").Append(maxKin)
                      .Append(", limit gry ").Append((int)Math.Ceiling(maxKn * 0.7f)).Append("), maks armii rodu ").Append(maxClan).Append(" (").Append(maxClanId).Append("), zalew ").Append(flood)
                      .Append(" | wplyw < 0 rodow wodzow: glowy ").Append(negHead).Append(", inni lordowie ").Append(negOther)
                      .Append(" | pierwszenstwa zbiorki ").Append(_pref.Count)
                      .Append(" | potkniecia ").Append(_dStum).Append(" (sesja ").Append(_sStum).Append(").");
                    Log.Info(sb.ToString());
                    ZeroDay();
                }
            }
            catch (Exception e) { Stumble("Daily", e); }
        }
    }
}
