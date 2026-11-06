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
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// 342 (Jeff 06.11: "kazdy moze zalozyc armie, nie tylko glowa rodu, bo to bez sensu") - klucz AnyLordArmy342.
    ///  - WODZ (transpiler DefaultArmyManagementCalculationModel.CanLordCreateArmy, klasa deklarujaca - baza SCA, BK, NavalDLC): warunek gry
    ///    "Clan.Leader == LeaderHero" -> CanLead(party): glowa rodu jak gra ALBO (regula 342) lord krolestwa AI z druzyna lordowska, nie gracz i nie rod gracza, nie najem,
    ///    nie w armii, gdy wplyw rodu &gt; 100 x (armie prowadzone przez ludzi rodu + 1) - stala gry 100 (MinimumInfluenceNeededToCreateArmy) na kazda armie rodu.
    ///    Kolejka rodu: w danej godzinie probuje tylko jedna druzyna rodu (WarPartyComponents[godzina % liczba]) - po kolei, bez zalewu prob i bez kosztu petli
    ///    kandydatow gry dla wszystkich lordow naraz. Reszta warunkow gry bez zmian (morze, wplyw &gt; 100, jedzenie, liczebnosc, wojna, kandydaci, limit 0.7, prog 1000,
    ///    sito SCA i ArmsFirst). Wariant gry "pierwsza druzyna rodu, gdy glowa bez druzyny" zostaje. Kotwica dokladnie 1 - inaczej nic nie zmieniamy ("BRAK").
    ///  - BUDZET (postfiks First, tylko wodz z reguly 342): czlonkowie w kolejnosci gry, dopoki suma CalculatePartyInfluenceCost &lt;= budzet gry
    ///    (GetInfluenceBudgetWhileCreatingArmy = 0.7 x wplyw rodu - gra liczy go i wyrzuca); po przycieciu prog 1000 od nowa; pusta lista - false. Rod nie schodzi pod 0.
    ///  - BK (postfiks BKArmyManagementModel.CanCreateArmy - blokada Kingdom.CreateArmy i BKArmyBehavior): false BK -> true tylko dla wodza z reguly 342 w jego godzinie
    ///    i przy wolnych miejscach BK (ceil(0.7 x druzyn) - w armiach &gt;= 2, jak BK). Gracz: BK przepuszcza MainHero zawsze; rod gracza poza regula (myslenie AI go nie wola).
    /// </summary>
    internal static class AnyLordArmy342
    {
        private const string Tag = "Armie kazdego lorda (342): ";
        private const float InfluencePerArmy = 100f;   // gra: DefaultArmyManagementCalculationModel.MinimumInfluenceNeededToCreateArmy
        private const float ArmyFloor = 1000f;         // gra: prog sily oblezenia w CanLordCreateArmy
        private static Harmony _h;
        private static bool _tried;
        private static int _anchors = -1;
        private static string _wHead = "nie wpieta", _wBudget = "nie wpieta", _wBk = "nie wpieta";
        private static Func<DefaultArmyManagementCalculationModel, MobileParty, float> _budget;
        [ThreadStatic] private static MobileParty _via;   // CanLead dal zgode z reguly 342 w tym wywolaniu CanLordCreateArmy
        private static readonly object _lk = new object();
        private static int _dGrant, _dQueue, _dLimit, _dTrim, _dTrimEmpty, _dTrimFloor, _dBkPass, _dHead, _dOther, _dLines, _dStum, _sStum;
        private static readonly HashSet<Army> _ruleArmies = new HashSet<Army>();
        // kolejka rodu: druzyna wyslana na zbiorke (Zelazni w porcie) ma pierwszenstwo w swoim rodzie do godziny (bez czekania na swoja kolej w porcie)
        private static readonly Dictionary<Clan, KeyValuePair<MobileParty, int>> _pref = new Dictionary<Clan, KeyValuePair<MobileParty, int>>();

        /// <summary>Druzyna p ma pierwszenstwo kolejki swojego rodu przez hours godzin (zbiorka w porcie). Nie rzuca.</summary>
        internal static void Prefer(MobileParty p, int hours)
        {
            try
            {
                var c = p != null && p.LeaderHero != null ? p.LeaderHero.Clan : null;
                if (c == null) return;
                lock (_lk) _pref[c] = new KeyValuePair<MobileParty, int>(p, NowH + hours);
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
            lock (_lk) { _ruleArmies.Clear(); _pref.Clear(); ZeroDay(); _sStum = 0; }
        }

        private static void ZeroDay() { _dGrant = _dQueue = _dLimit = _dTrim = _dTrimEmpty = _dTrimFloor = _dBkPass = _dHead = _dOther = _dLines = _dStum = 0; }

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
                if (Rule(p, h, c, true)) { _via = p; return true; }
            }
            catch (Exception e) { Stumble("CanLead", e); }
            return false;
        }

        /// <summary>Odczyt (Armoury: Zelazni, powody): czy gra da tej druzynie warunek wodza (glowa rodu, pierwsza druzyna rodu bez glowy w polu, regula 342
        /// z kolejka rodu w tej godzinie). Bez licznikow. Nie rzuca.</summary>
        internal static bool LeadsNow(MobileParty p)
        {
            try
            {
                var h = p != null ? p.LeaderHero : null;
                var c = h != null ? h.Clan : null;
                if (c == null) return false;
                if (c.Leader == h) return true;
                if (c.Leader != null && c.Leader.PartyBelongedTo == null && c.WarPartyComponents != null && c.WarPartyComponents.Count > 0 && c.WarPartyComponents[0] == p.WarPartyComponent) return true;
                return Rule(p, h, c, false);
            }
            catch { return false; }
        }

        /// <summary>Odczyt: druzyna moglaby prowadzic armie wedlug reguly 342 poza kolejka rodu (Armoury: zbiorka Zelaznych w porcie planowana na kilka godzin).</summary>
        internal static bool MayLead(MobileParty p)
        {
            try
            {
                var h = p != null ? p.LeaderHero : null;
                var c = h != null ? h.Clan : null;
                if (c == null) return false;
                if (c.Leader == h) return true;
                if (c.Leader != null && c.Leader.PartyBelongedTo == null && c.WarPartyComponents != null && c.WarPartyComponents.Count > 0 && c.WarPartyComponents[0] == p.WarPartyComponent) return true;
                return Rule(p, h, c, false, false);
            }
            catch { return false; }
        }

        /// <summary>Regula 342 dla lorda, ktory nie jest glowa rodu. count - liczniki doby; queue - kolejka rodu (jedna druzyna rodu na godzine).</summary>
        private static bool Rule(MobileParty p, Hero h, Clan c, bool count, bool queue = true)
        {
            if (!On) return false;
            if (!p.IsLordParty || p.IsMainParty || p.Army != null || !p.IsActive) return false;
            var pc = Clan.PlayerClan;
            if (c == pc || c.IsUnderMercenaryService || c.Leader == null) return false;
            var k = c.Kingdom;
            if (k == null || p.MapFaction != k || (pc != null && k.RulingClan == pc)) return false;   // krolestwa AI; gracz-wladca decyduje sam
            if (queue)
            {
                var wp = c.WarPartyComponents;
                int n = wp != null ? wp.Count : 0;
                if (n == 0) return false;
                int hh = NowH;
                bool pref = false;
                lock (_lk)
                {
                    KeyValuePair<MobileParty, int> pv;
                    if (_pref.Count > 0 && _pref.TryGetValue(c, out pv) && pv.Value >= hh && pv.Key != null && pv.Key.IsActive) { pref = true; if (pv.Key != p) { if (count) _dQueue++; return false; } }
                }
                int i = ((hh % n) + n) % n;
                if (!pref && (wp[i] == null || wp[i].MobileParty != p)) { if (count) lock (_lk) _dQueue++; return false; }
            }
            int armies = 0;
            foreach (var a in k.Armies)
            {
                var lh = a != null && a.LeaderParty != null ? a.LeaderParty.LeaderHero : null;
                if (lh != null && lh.Clan == c) armies++;
            }
            if (!(c.Influence > InfluencePerArmy * (armies + 1))) { if (count) lock (_lk) _dLimit++; return false; }
            if (count) lock (_lk) _dGrant++;
            return true;
        }

        // ================================================================ latki
        internal static void SessionStart()
        {
            if (_h == null) _h = new Harmony("com.jeff.armoury.anylord342");
            if (!_tried)
            {
                _tried = true;
                var args = new[] { typeof(MobileParty), typeof(MBList<MobileParty>).MakeByRefType() };
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
                        _h.Patch(m, postfix: new HarmonyMethod(typeof(AnyLordArmy342), nameof(BudgetPost)) { priority = Priority.First });
                        _wBudget = "TAK" + (_budget != null ? "" : " (budzet 0.7 x wplyw - metoda gry BRAK)");
                    }
                }
                catch (Exception e) { _wHead = "BRAK (" + e.GetType().Name + ")"; Stumble("SessionStart.head", e); }
                try
                {
                    var t = AccessTools.TypeByName("BannerKings.Models.Vanilla.BKArmyManagementModel");
                    var m = t != null ? AccessTools.DeclaredMethod(t, "CanCreateArmy", new[] { typeof(Hero) }) : null;
                    if (t == null) _wBk = "bez BK";
                    else if (m == null) _wBk = "BRAK (CanCreateArmy)";
                    else { _h.Patch(m, postfix: new HarmonyMethod(typeof(AnyLordArmy342), nameof(BkPost)) { priority = Priority.Last }); _wBk = "TAK"; }
                }
                catch (Exception e) { _wBk = "BRAK (" + e.GetType().Name + ")"; Stumble("SessionStart.bk", e); }
            }
            try
            {
                var s = S;
                if (s != null && s.LogEnabled)
                    Log.Info(Tag + "start | klucz " + (On ? "TAK" : "NIE") + " | wodz gry (CanLordCreateArmy) " + _wHead + " | budzet wplywu " + _wBudget + " | BK CanCreateArmy " + _wBk
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

        /// <summary>Postfiks (First, przed ArmsFirst): wodz z reguly 342 - czlonkowie do budzetu wplywu gry, potem prog 1000. Nie rzuca.</summary>
        public static void BudgetPost(DefaultArmyManagementCalculationModel __instance, MobileParty __0, ref MBList<MobileParty> __1, ref bool __result)
        {
            if (!ReferenceEquals(_via, __0)) return;
            _via = null;
            try
            {
                if (!__result || __1 == null || __1.Count == 0) return;
                var c = __0.LeaderHero.Clan;
                float budget = _budget != null ? _budget(__instance, __0) : c.Influence * 0.7f;
                var am = Campaign.Current.Models.ArmyManagementCalculationModel;
                int sum = 0, keep = 0;
                for (; keep < __1.Count; keep++)
                {
                    int cost = am.CalculatePartyInfluenceCost(__0, __1[keep]);
                    if (sum + cost > budget) break;
                    sum += cost;
                }
                if (keep >= __1.Count) return;
                lock (_lk) _dTrim++;
                __1.RemoveRange(keep, __1.Count - keep);
                if (__1.Count == 0) { __result = false; lock (_lk) _dTrimEmpty++; return; }
                var k = __0.MapFaction as Kingdom;
                if (k != null && k.Settlements.Count > 0)
                {
                    float cs = __0.Party.GetCustomStrength(BattleSideEnum.Attacker, MapEvent.PowerCalculationContext.Siege);
                    foreach (var m in __1) if (m != null) cs += m.Party.GetCustomStrength(BattleSideEnum.Attacker, MapEvent.PowerCalculationContext.Siege);
                    if (cs < ArmyFloor) { __1.Clear(); __result = false; lock (_lk) _dTrimFloor++; }
                }
            }
            catch (Exception e) { Stumble("BudgetPost", e); }
        }

        /// <summary>Postfiks BKArmyManagementModel.CanCreateArmy: false BK -> true dla wodza z reguly 342 w jego godzinie, gdy BK ma wolne miejsca (jak BK: ceil(0.7 x druzyn) - w armiach &gt;= 2).</summary>
        public static void BkPost(Hero __0, ref bool __result)
        {
            if (__result || __0 == null || !On) return;
            try
            {
                if (__0 == Hero.MainHero) return;
                var p = __0.PartyBelongedTo;
                var c = __0.Clan;
                if (p == null || c == null || p.LeaderHero != __0 || c.Leader == __0) return;
                if (!Rule(p, __0, c, false)) return;
                var k = c.Kingdom;
                if (k.Armies.Count > 0)
                {
                    int n = k.WarPartyComponents != null ? k.WarPartyComponents.Count : 0, inA = 0;
                    foreach (var a in k.Armies) if (a != null && a.Parties != null) inA += a.Parties.Count;
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
                    if (head) _dHead++; else { _dOther++; _ruleArmies.Add(a); }
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

        internal static void Daily()
        {
            try
            {
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
                    var oldPref = new List<Clan>();
                    int hNow = NowH;
                    foreach (var kv in _pref) if (kv.Key == null || kv.Value.Value < hNow) oldPref.Add(kv.Key);
                    foreach (var c in oldPref) _pref.Remove(c);
                    _ruleArmies.RemoveWhere(a => a == null || a.LeaderParty == null || !a.LeaderParty.IsActive || a.Kingdom == null || !a.Kingdom.Armies.Contains(a));
                    var inv = System.Globalization.CultureInfo.InvariantCulture;
                    var sb = new StringBuilder(400);
                    sb.Append(Tag).Append("dzien ").Append((int)CampaignTime.Now.ToDays).Append(" | klucz ").Append(On ? "TAK" : "NIE")
                      .Append(" | latki: wodz gry ").Append(_wHead).Append(", budzet ").Append(_wBudget).Append(", BK ").Append(_wBk)
                      .Append(" | zgody reguly ").Append(_dGrant).Append(", poza kolejka rodu ").Append(_dQueue).Append(", limit wplywu rodu ").Append(_dLimit)
                      .Append(" | przyciete budzetem ").Append(_dTrim).Append(" (puste ").Append(_dTrimEmpty).Append(", prog 1000 ").Append(_dTrimFloor).Append("), BK przepuscil ").Append(_dBkPass)
                      .Append(" | zalozone dzis: glowy rodow ").Append(_dHead).Append(", inni lordowie ").Append(_dOther)
                      .Append(" | teraz: armii ").Append(armies).Append(" (inni lordowie ").Append(ruleNow).Append("), krolestw z armia ").Append(kWith)
                      .Append(", maks armii krolestwa ").Append(maxK).Append(" (").Append(maxKid).Append(", druzyn ").Append(maxKn).Append(", w armiach ").Append(maxKin)
                      .Append(", limit gry ").Append((int)Math.Ceiling(maxKn * 0.7f)).Append("), maks armii rodu ").Append(maxClan).Append(" (").Append(maxClanId).Append("), zalew ").Append(flood)
                      .Append(" | wplyw < 0 rodow wodzow: glowy ").Append(negHead).Append(", inni lordowie ").Append(negOther)
                      .Append(" | potkniecia ").Append(_dStum).Append(" (sesja ").Append(_sStum).Append(").");
                    Log.Info(sb.ToString());
                    ZeroDay();
                }
            }
            catch (Exception e) { Stumble("Daily", e); }
        }
    }
}
