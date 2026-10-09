using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// POLNOC TWARDSZA U SIEBIE - paczka 175.4b (decyzja Jeffa 09.10 pkt 1, wariant C; projekt rozdz. 3.3; suwak NorthHomeEdgePercent,
    /// dom. 10, 0 = wylaczone). Jeff: "wedlug lore Polnocnych jest MNIEJ, ale sa twardsi ... zeby nie zepsuc rownowagi gry, zeby nagle
    /// jedna armia nie bila wszystkich". Lore: ludzie Polnocy lepiej znosza marsz w sniegu niz poludniowcy (ADWD), ale w polu przegrali
    /// z Tywinem (AGOT) - "twardsi, zwlaszcza w zimie i u siebie", nie "lepsi wszedzie".
    ///
    /// Gdzie: postfiks (Priority.Low - po BK Normal, przed CS ValyrianWardSim Last) na bazowym DefaultCombatSimulationModel.SimulateHit
    /// (cios w autobitwie; ROT-owy model deleguje do bazowego - latanie obu cieloby podwojnie). Wzor ciosu gry: 40 x (moc bijacego /
    /// moc trafianego)^0.7, wiec +P% mocy = x(1+P)^0.7 na cios ZADANY przez jednostke ze zbioru i /(1+P)^0.7 na cios OTRZYMANY
    /// (obaj ze zbioru - bez zmian). Nie GetContextModifier: ten zmienia tez PartyBase.CurrentStrength (decyzje AI na mapie:
    /// kogo atakowac, przed kim uciekac) - przewaga ma rozstrzygac cios, nie zmieniac zachowania AI.
    /// Warunek bitwy: bitwa polowa na ladzie (oblezenie - nigdy) i kontekst symulacji SnowBattle (wszedzie - snieg z terenu albo
    /// z pogody) albo ForestBattle w regionie Polnocy (najblizsza twierdza kultury battania, OutlawLaw.RegionAt) - odswiezany
    /// raz na godzine gry na bitwe (ConditionalWeakTable).
    /// Zbior: piechota (default_group Infantry) t3-t6 kultury battania z drzew BasicTroop (wies) i EliteBasicTroop (szlachta) kultury
    /// i 9 szablonow Polnocy, bez milicji - ta sama regula co CS NorthSet, liczona przy starcie sesji (ma byc 43), i partia bijacego
    /// nalezy do rodu kultury battania (zaloga - rod wlasciciela osady; zamyka przeciek umber_houseguard w partiach Clegane).
    /// Dziala tylko w bitwach prawie rownych (autobitwe wygrywa prawie zawsze silniejszy) - miara w KingdomBalance (linia B175).
    /// </summary>
    internal static class NorthHomeEdge
    {
        internal const int ExpectedSet = 43;   // projekt 3.2 / 6.1 pkt 4 (jednostki.csv + szablony.csv)
        private static readonly string[] Templates =
        {
            "kingdom_hero_party_battania_template", "clan_stark_party_template", "clan_bolton_party_template", "clan_karstark_party_template",
            "clan_glover_party_template", "clan_manderly_party_template", "clan_umber_party_template", "clan_mormont_party_template",
            "clan_cerwyn_party_template"
        };

        private static HashSet<CharacterObject> _set;
        private static int _stumbles;
        private static bool _patched;
        private static long _hitsOut, _hitsIn;   // ciosy z mnoznikiem: zadane przez zbior / otrzymane przez zbior (sesja)

        internal sealed class EdgeState
        {
            public double Hour = -1; public bool Active; public string Why = "";
            public long HitsA, HitsD;   // ciosy z mnoznikiem na korzysc strony atakujacej / broniacej
        }
        private static readonly ConditionalWeakTable<MapEvent, EdgeState> _state = new ConditionalWeakTable<MapEvent, EdgeState>();

        internal static int Percent { get { var s = Settings.Current; return s == null ? 0 : Math.Max(0, Math.Min(25, s.NorthHomeEdgePercent)); } }

        internal static void Reset() { _set = null; _stumbles = 0; _hitsOut = _hitsIn = 0; }

        internal static bool InSet(CharacterObject c) { var s = _set; return c != null && s != null && s.Contains(c); }
        internal static int SetCount { get { var s = _set; return s != null ? s.Count : 0; } }

        private static void Tree(CharacterObject root, HashSet<CharacterObject> into)
        {
            if (root == null) return;
            var stack = new Stack<CharacterObject>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                var c = stack.Pop();
                if (c == null || !into.Add(c)) continue;
                var ups = c.UpgradeTargets;
                if (ups != null) foreach (var u in ups) if (u != null) stack.Push(u);
            }
        }

        /// <summary>
        /// Zbior 43 (regula z projektu 3.2). PRZEGLAD 175: DOKLADNIE ta sama regula co CS Army175.NorthSet (dotad Armoury mialo na
        /// sztywno korzenie battanian_volunteer / battanian_highborn_youth i nie wykluczalo drzew milicji - dzis oba daja 43, ale po
        /// zmianie danych ROT +25 (CS) i +10% (tu) moglyby objac rozne jednostki): drzewa Culture.BasicTroop i EliteBasicTroop kultury
        /// battania + 9 szablonow Polnocy, bez drzew milicji (Melee/Ranged(Elite)MilitiaTroop) i bohaterow, kultura battania,
        /// default_group Infantry, tier 3-6. Liczba inna niz 43 - OSTRZEZENIE w logu (tu i w KingdomBalance.SessionStart).
        /// </summary>
        internal static void BuildSet()
        {
            var set = new HashSet<CharacterObject>();
            var tree = new HashSet<CharacterObject>();
            var mil = new HashSet<CharacterObject>();
            int missing = 0;
            try
            {
                var om = MBObjectManager.Instance;
                var cu = om.GetObject<CultureObject>("battania");
                if (cu != null)
                {
                    if (cu.BasicTroop != null) Tree(cu.BasicTroop, tree); else missing++;
                    if (cu.EliteBasicTroop != null) Tree(cu.EliteBasicTroop, tree); else missing++;
                    Tree(cu.MeleeMilitiaTroop, mil); Tree(cu.RangedMilitiaTroop, mil); Tree(cu.MeleeEliteMilitiaTroop, mil); Tree(cu.RangedEliteMilitiaTroop, mil);
                }
                else missing += 2;
                foreach (var tid in Templates)
                {
                    var t = om.GetObject<PartyTemplateObject>(tid);
                    if (t == null || t.Stacks == null) { missing++; continue; }
                    foreach (var st in t.Stacks) Tree(st.Character, tree);
                }
                foreach (var c in tree)
                {
                    if (c == null || mil.Contains(c)) continue;
                    if (c.IsHero || c.Culture == null || c.Culture.StringId != "battania") continue;
                    if (c.DefaultFormationClass != FormationClass.Infantry) continue;
                    int tier = c.Tier;
                    if (tier < 3 || tier > 6) continue;
                    set.Add(c);
                }
            }
            catch (Exception e) { Log.Error("NorthHomeEdge.BuildSet", e); }
            _set = set;
            var byTier = new SortedDictionary<int, List<string>>();
            foreach (var c in set) { List<string> l; if (!byTier.TryGetValue(c.Tier, out l)) { l = new List<string>(); byTier[c.Tier] = l; } l.Add(c.StringId); }
            var sb = new StringBuilder();
            foreach (var kv in byTier) { kv.Value.Sort(StringComparer.Ordinal); sb.Append(" t").Append(kv.Key).Append(" (").Append(kv.Value.Count).Append("): ").Append(string.Join(", ", kv.Value.ToArray())).Append(';'); }
            Log.Info("NorthHomeEdge (175): zbior piechoty Polnocy t3-t6 (wies, szlachta, rody; regula CS NorthSet, bez milicji) - " + set.Count + " jednostek"
                     + (set.Count == ExpectedSet ? " (jak ma byc - 43, jak CS NorthHardy)" : " - OSTRZEZENIE: ma byc " + ExpectedSet + " (dane ROT albo regula rozjechane; porownaj z linia CS NorthHardy)")
                     + ", drzewa " + tree.Count + " (milicji wykluczonych " + mil.Count + ")" + (missing > 0 ? ", BRAK " + missing + " korzeni/szablonow" : "") + "; przewaga w autobitwie " + Percent + "% (snieg wszedzie, las Polnocy, nigdy oblezenie)"
                     + (_patched ? "" : " - LATKA NIEWPIETA, przewaga spi") + ";" + sb);
        }

        /// <summary>Rod partii: partia rodu - ActualClan; zaloga i milicja - rod wlasciciela osady; partia osady - jej wlasciciel.</summary>
        internal static Clan ClanOf(PartyBase p)
        {
            if (p == null) return null;
            try
            {
                var mp = p.MobileParty;
                if (mp != null)
                {
                    if (mp.IsGarrison || mp.IsMilitia)
                    {
                        var st = mp.CurrentSettlement ?? mp.HomeSettlement;
                        return st != null ? st.OwnerClan : null;
                    }
                    if (mp.ActualClan != null) return mp.ActualClan;
                    if (mp.LeaderHero != null) return mp.LeaderHero.Clan;
                    return p.Owner != null ? p.Owner.Clan : null;
                }
                return p.Settlement != null ? p.Settlement.OwnerClan : null;
            }
            catch { return null; }
        }

        internal static bool NorthClan(PartyBase p)
        {
            var cl = ClanOf(p);
            return cl != null && cl.Culture != null && cl.Culture.StringId == "battania";
        }

        private static EdgeState StateOf(MapEvent me)
        {
            var st = _state.GetValue(me, _ => new EdgeState());
            double h = Math.Floor(CampaignTime.Now.ToHours);
            if (st.Hour != h)
            {
                st.Hour = h;
                string why;
                st.Active = Cond(me, out why);
                st.Why = why;
            }
            return st;
        }

        private static bool Cond(MapEvent me, out string why)
        {
            why = "";
            if (!me.IsFieldBattle || me.IsNavalMapEvent) { why = "nie pole"; return false; }
            var ctx = me.SimulationContext;
            if (ctx == MapEvent.PowerCalculationContext.SnowBattle) { why = "snieg"; return true; }
            if (ctx == MapEvent.PowerCalculationContext.ForestBattle)
            {
                var r = OutlawLaw.RegionAt(me.Position.ToVec2());
                bool north = r != null && r.Culture != null && r.Culture.StringId == "battania";
                why = north ? "las Polnocy" : "las poza Polnoca";
                return north;
            }
            why = ctx.ToString();
            return false;
        }

        /// <summary>Odczyt dla linii B175 (KingdomBalance): czy przewaga dzialala w tej bitwie i ile ciosow z mnoznikiem.</summary>
        internal static bool Read(MapEvent me, out long hitsA, out long hitsD)
        {
            hitsA = hitsD = 0;
            EdgeState st;
            if (me == null || !_state.TryGetValue(me, out st)) return false;
            hitsA = st.HitsA; hitsD = st.HitsD;
            return st.HitsA + st.HitsD > 0;
        }

        internal static string SessionHits() { return "zadane " + _hitsOut + ", otrzymane " + _hitsIn; }

        /// <summary>Postfiks SimulateHit(strikerTroop __0, struckTroop __1, strikerParty __2, struckParty __3, ..., battle __5, ...).</summary>
        public static void SimPostfix(CharacterObject __0, CharacterObject __1, PartyBase __2, PartyBase __3, MapEvent __5, ref ExplainedNumber __result)
        {
            try
            {
                if (__result.ResultNumber <= 0f) return;
                var set = _set;
                if (set == null || set.Count == 0) return;
                bool a = __0 != null && set.Contains(__0);
                bool d = __1 != null && set.Contains(__1);
                if (a == d) return;                                   // zaden albo obaj ze zbioru - bez zmian
                int pct = Percent;
                if (pct <= 0) return;
                if (a && !NorthClan(__2)) return;
                if (d && !NorthClan(__3)) return;
                var me = __5 ?? (__2 != null ? __2.MapEvent : null);
                if (me == null) return;
                var st = StateOf(me);
                if (!st.Active) return;
                float k = (float)Math.Pow(1.0 + pct / 100.0, 0.7);
                __result = new ExplainedNumber(a ? __result.ResultNumber * k : __result.ResultNumber / k);
                var favoured = a ? __2 : __3;
                if (favoured != null && favoured.Side == BattleSideEnum.Attacker) st.HitsA++; else st.HitsD++;
                if (a) _hitsOut++; else _hitsIn++;
            }
            catch { _stumbles++; }
        }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var tDef = typeof(TaleWorlds.CampaignSystem.GameComponents.DefaultCombatSimulationModel);
                System.Reflection.MethodInfo mSim = null;
                foreach (var m in tDef.GetMethods())
                {
                    if (m.Name != "SimulateHit" || m.DeclaringType != tDef) continue;
                    var ps = m.GetParameters();
                    if (ps.Length >= 6 && ps[0].ParameterType == typeof(CharacterObject) && ps[1].ParameterType == typeof(CharacterObject)
                        && ps[2].ParameterType == typeof(PartyBase) && ps[3].ParameterType == typeof(PartyBase) && ps[5].ParameterType == typeof(MapEvent))
                    { mSim = m; break; }
                }
                if (mSim != null)
                {
                    h.Patch(mSim, postfix: new HarmonyMethod(typeof(NorthHomeEdge), nameof(SimPostfix)) { priority = Priority.Low });
                    _patched = true;
                    Log.Info("NorthHomeEdge (175.4b): przewaga Polnocy w autobitwie wpieta (DefaultCombatSimulationModel.SimulateHit, Priority.Low); suwak NorthHomeEdgePercent, zbior przy starcie sesji.");
                }
                else Log.Info("NorthHomeEdge (175.4b): DefaultCombatSimulationModel.SimulateHit nieznaleziony - przewaga Polnocy spi.");
            }
            catch (Exception e) { Log.Error("NorthHomeEdge.ApplyAll", e); }
        }

        internal static int Stumbles { get { return _stumbles; } }
    }
}
