using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// E4/9 (192, PLAN 4.16, 4.17; projekt etapu 4 R15, U14, U15b, U22; plan kodu docs/etap4/PLAN-KODU-E49.md rozdz. 5-6): NIEWOLA TYLKO W KANONIE I JENCY Z RABUNKU.
    ///  - konto NIEWOLNI (PeopleBook.Canon): jeniec sprzedany w krainie z niewola (I1b, patrol BK) zostaje niewolnym tej krainy z pochodzeniem; bez dzieci, bez wyzwolen,
    ///    ThrallYearlyLossPercent (5%) rocznie umiera (Daily); bilans otwarcia NK - Zelazne Wyspy IronIslandsStartThralls (27 tys.) jako czesc 500 tys. tabeli wysp (OpeningFor
    ///    z PopulationLaw.Calibrate - to samo wywolanie co k wsi wysp);
    ///  - jency z rabunku (RaidCaptivesFromVillage): prefiks bool Priority.First na BK BKRaidCaptureBehavior.ExecuteCapture (BK wola go tylko po wygranym rabunku) - BK nie
    ///    dopisuje jencow z niczego i nie placi "dzialki" z niczego (zawsze false przy czynnym prawie i spustoszeniu); nasza regula: najezdzca z kultury z niewola
    ///    (RaidState.Slaver - ta sama lista co H3/I1), 10% trafionych calego rabunku (R8, DevastationCaptivesPercent) przechodzi z konta UCHODZCY napadnietej wsi do lochu
    ///    najezdzcy jako villager_kultura z udzialem pochodzenia = region wsi; przelacznik gracza BK Take/Leave zostaje. Zerowanie armii - bez pojmanych.
    /// Stan w zapisie - PeopleBook ("ns:" "nw:"). Kazde cialo we wlasnym try, potkniecia liczone (bez globalnych wylacznikow).
    /// </summary>
    internal static class ThrallLaw
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static Settings S { get { return Settings.Current; } }
        /// <summary>Klucz (i ksiega pochodzenia w ustawieniach) - bilans otwarcia NK sprawdza tylko to (siew E4/7 jest po kalibracji).</summary>
        internal static bool KeyOn { get { var s = S; return s != null && s.ThrallLawEnabled && s.PeopleOriginEnabled; } }
        /// <summary>Prawo czynne: klucz i ksiega pochodzenia po siewie (konto z udzialami pochodzenia).</summary>
        internal static bool On { get { return KeyOn && PeopleBook.OriginLive; } }

        internal static bool Patched;
        internal static string MissingNote;
        internal static MethodBase Target;
        private static int _stumbles, _stumblesDay;
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static readonly Dictionary<string, CharacterObject> _villager = new Dictionary<string, CharacterObject>();
        private static PropertyInfo _pPolicies;
        private static MethodInfo _mGet;
        private static FieldInfo _fMode;
        private static bool _polTried;

        private static void Stumble(string where, Exception e)
        {
            try { _stumbles++; _stumblesDay++; if (_err.Add(where)) Log.Error("ThrallLaw." + where, e); } catch { }
        }

        /// <summary>Konstruktor ArmouryBehavior: stan jednej kampanii (latka wpieta raz na proces - nie zerowana).</summary>
        internal static void Reset() { _stumbles = _stumblesDay = 0; _err.Clear(); _villager.Clear(); }

        // ================================================================ bilans otwarcia NK (5.4)
        /// <summary>
        /// PopulationLaw.Calibrate (nowa kampania, przed k wsi i miast krainy): ilu z ludzi tabeli krainy to niewolni na start - Zelazne Wyspy (sturgia)
        /// IronIslandsStartThralls x PopulationScale, nie wiecej niz polowa krainy; tylko przy wsiach wysp (hasVillages), czynnym kluczu, bez bilansu w ksiedze i przed
        /// uplywem 2 dob kampanii (ta sama granica co bilans cial E4/2). Inaczej 0 (stary zapis - _k jest z zapisu, Calibrate nie biegnie).
        /// </summary>
        internal static float OpeningFor(string culture, float pop, bool hasVillages)
        {
            try
            {
                var s = S;
                if (s == null || culture != "sturgia" || !hasVillages || !KeyOn || PeopleBook.ThrallsSeeded || !(pop > 0f) || float.IsInfinity(pop)) return 0f;
                if (Campaign.Current == null || Campaign.Current.Models == null || Campaign.Current.Models.CampaignTimeModel == null) return 0f;
                float elapsed = Campaign.Current.Models.CampaignTimeModel.CampaignStartTime.ElapsedDaysUntilNow;
                if (!(elapsed < 2f)) return 0f;
                float want = Math.Max(0, s.IronIslandsStartThralls) * Math.Max(0f, s.PopulationScale);
                return Math.Max(0f, Math.Min(want, 0.5f * pop));
            }
            catch (Exception e) { Stumble("OpeningFor", e); return 0f; }
        }

        // ================================================================ jency z rabunku (6.2)
        internal static void ApplyAll(Harmony h)
        {
            if (h == null || Patched) return;
            try
            {
                var t = AccessTools.TypeByName("BannerKings.Behaviours.Raids.BKRaidCaptureBehavior");
                var m = t != null ? AccessTools.Method(t, "ExecuteCapture") : null;
                Target = m;
                if (m == null) { MissingNote = "BKRaidCaptureBehavior.ExecuteCapture"; return; }
                var ps = m.GetParameters();
                if (ps.Length != 6 || ps[0].ParameterType != typeof(MobileParty) || ps[2].ParameterType != typeof(Clan) || ps[3].ParameterType != typeof(Village))
                { MissingNote = "BKRaidCaptureBehavior.ExecuteCapture (inne parametry)"; return; }
                h.Patch(m, prefix: new HarmonyMethod(typeof(ThrallLaw), nameof(CapturePrefix)) { priority = Priority.First });
                Patched = true;
            }
            catch (Exception e) { MissingNote = "BKRaidCaptureBehavior.ExecuteCapture (" + e.GetType().Name + ")"; Log.Error("ThrallLaw.ApplyAll", e); }
        }

        /// <summary>BK RaidCapturePolicyManager.Get(clan).Mode == Leave (przelacznik gracza "Captives: Take/Leave"); blad odczytu - Take (domyslny BK).</summary>
        private static bool PlayerLeaves(object behavior, Clan clan)
        {
            try
            {
                if (behavior == null || clan == null) return false;
                if (!_polTried)
                {
                    _polTried = true;
                    _pPolicies = AccessTools.Property(behavior.GetType(), "Policies");
                }
                var pm = _pPolicies != null ? _pPolicies.GetValue(behavior, null) : null;
                if (pm == null) return false;
                if (_mGet == null) _mGet = AccessTools.Method(pm.GetType(), "Get", new[] { typeof(Clan) });
                var pol = _mGet != null ? _mGet.Invoke(pm, new object[] { clan }) : null;
                if (pol == null) return false;
                if (_fMode == null) _fMode = AccessTools.Field(pol.GetType(), "Mode");
                var mode = _fMode != null ? _fMode.GetValue(pol) : null;
                return mode != null && mode.ToString() == "Leave";
            }
            catch (Exception e) { Stumble("PlayerLeaves", e); return false; }
        }

        /// <summary>Postac pojmanego: villager_kultura (kultura biezaca wsi - ta sama droga co BK), zapasowo looter; null - brak obu.</summary>
        private static CharacterObject VillagerOf(Village v)
        {
            string c = v != null && v.Settlement != null && v.Settlement.Culture != null ? v.Settlement.Culture.StringId : "";
            CharacterObject ch;
            if (_villager.TryGetValue(c, out ch)) return ch;
            ch = null;
            try { if (c.Length > 0) ch = MBObjectManager.Instance.GetObject<CharacterObject>("villager_" + c); } catch { ch = null; }
            if (ch == null) { try { ch = MBObjectManager.Instance.GetObject<CharacterObject>("looter"); } catch { ch = null; } }
            _villager[c] = ch;
            return ch;
        }

        /// <summary>Linia startowa (T3): kultury wsi bez postaci villager_kultura (pojmani jako looter, jak w BK).</summary>
        internal static string MissingVillagers()
        {
            try
            {
                var miss = new HashSet<string>();
                foreach (var st in Settlement.All)
                {
                    if (st == null || !st.IsVillage || st.Culture == null || st.Culture.StringId == null) continue;
                    string c = st.Culture.StringId;
                    if (miss.Contains(c)) continue;
                    CharacterObject ch = null;
                    try { ch = MBObjectManager.Instance.GetObject<CharacterObject>("villager_" + c); } catch { ch = null; }
                    if (ch == null) miss.Add(c);
                }
                return miss.Count + (miss.Count > 0 ? " (" + string.Join(", ", miss.OrderBy(x => x).ToArray()) + ")" : "");
            }
            catch (Exception e) { Stumble("MissingVillagers", e); return "?"; }
        }

        /// <summary>
        /// Prefiks bool Priority.First na BK ExecuteCapture(attackerParty, leader, capturingClan, village, totalAttackerTroops, fromCheat). Prawo nieczynne (klucz albo
        /// spustoszenie E4/5) - true (BK jak dotad). Czynne - zawsze false (BK nie robi jencow ani dzialki z niczego); pojmani J = round(10% trafionych rabunku przy
        /// najezdzcy z niewola), nie wiecej niz uchodzcy wsi, z konta UCHODZCY do lochu najezdzcy (villager_kultura) z udzialem pochodzenia = region wsi.
        /// Kolejnosc bez luki (U24): konto -> loch; loch nie przyjal (brak postaci, wyjatek) - ludzie wracaja na konto.
        /// </summary>
        public static bool CapturePrefix(object __instance, MobileParty __0, Hero __1, Clan __2, Village __3, int __4, bool __5)
        {
            if (!Devastation.CaptivesLive) return true;
            try
            {
                double plan; bool slaver;
                bool found = Devastation.TakeCaptives(__3, out plan, out slaver);
                bool player = __2 != null && __2 == Clan.PlayerClan;
                bool leave = player && PlayerLeaves(__instance, __2);
                int j = 0;
                if (found && !leave) j = (int)Math.Round(plan);
                int few = 0;
                if (j > 0)
                {
                    int refs = (int)Math.Floor(PeopleBook.RefugeesOf(__3) + 1e-6);
                    if (refs < j) { few = j - refs; j = refs; }
                }
                int taken = 0; bool noChar = false;
                if (j > 0 && __0 != null && __0.PrisonRoster != null)
                {
                    var ch = VillagerOf(__3);
                    if (ch == null) noChar = true;
                    else
                    {
                        double t = PeopleBook.RefugeesToCaptives(__3, j);
                        int ti = (int)Math.Floor(t + 1e-6);
                        if (t - ti > 1e-9) PeopleBook.RefugeesBack(__3, t - ti);       // ulamek (nie powinno sie zdarzyc) - z powrotem na konto
                        if (ti > 0)
                        {
                            try
                            {
                                __0.PrisonRoster.AddToCounts(ch, ti);
                                taken = ti;
                            }
                            catch (Exception e) { Stumble("CapturePrefix(loch)", e); PeopleBook.RefugeesBack(__3, ti); }
                            if (taken > 0)
                            {
                                try { PeopleBook.GiveNew(__0.PrisonRoster, OutlawLaw.RegionFor(__3.Settlement), taken, PeopleBook.FCapturedRaid); } catch (Exception e) { Stumble("CapturePrefix(pochodzenie)", e); }
                                try { VillageBook.OnRefugeesTaken(__3, taken); } catch (Exception e) { Stumble("CapturePrefix(E4/11)", e); }     // E4/11 Z11: oczekiwane konto wioski w dol
                            }
                        }
                    }
                }
                Devastation.NoteCapture(found, plan, taken, leave, noChar, few, slaver, player);
                if (player && taken > 0)
                {
                    try { InformationManager.DisplayMessage(new InformationMessage(taken + " villagers of " + __3.Name + " were taken captive.")); } catch { }
                }
            }
            catch (Exception e) { Stumble("CapturePrefix", e); }
            return false;
        }

        // ================================================================ doba (5.2) i linia "Niewolni (E4/9)"
        private static string F0(double v) { return v.ToString("0", Inv); }

        /// <summary>ArmouryBehavior.OnDailyTick po WatchLaw.Daily: smierc niewolnych (przy czynnym prawie), odcinek konta, linia. Przy wylaczonym - konto zamrozone (w zapisie).</summary>
        internal static void Daily()
        {
            var s = S;
            int day = 0; try { day = (int)CampaignTime.Now.ToDays - 1; } catch { }
            double pct = s != null ? Math.Max(0, Math.Min(100, s.ThrallYearlyLossPercent)) : 0;
            int yd = Math.Max(28, CampaignTime.DaysInYear);
            try { if (On && pct > 0) PeopleBook.ThrallsDaily(pct / 100.0 / yd); } catch (Exception e) { Stumble("Daily(smierc)", e); }
            PeopleBook.ThrallDay g;
            try { g = PeopleBook.TakeThrallDay(); } catch (Exception e) { Stumble("Daily(odcinek)", e); return; }
            if (s == null || !s.LogEnabled) { _stumblesDay = 0; return; }
            try
            {
                var sb = new StringBuilder(1600);
                sb.Append("Niewolni (E4/9): dzien ").Append(day);
                if (!On) sb.Append(" | prawo ").Append(KeyOn ? "NIECZYNNE (ksiega pochodzenia przed siewem)" : "WYLACZONE (Thrall Law Enabled albo People Origin Enabled) - konto zamrozone");
                var held = new Dictionary<string, double>(); var origin = new Dictionary<string, double>(); double unknown;
                PeopleBook.ThrallsByRealm(held, origin, out unknown);
                sb.Append(" | stan ").Append(F0(g.T)).Append(" (bilans otwarcia ").Append(PeopleBook.ThrallsSeeded ? F0(PeopleBook.ThrallsOpening) : "brak - stary zapis").Append(") w krainach:");
                bool f = true;
                foreach (var kv in held.OrderByDescending(x => x.Value))
                {
                    sb.Append(f ? " " : ", ").Append(kv.Key).Append(' ').Append(F0(kv.Value));
                    if (kv.Key == "sturgia") sb.Append(" (prog wolnych rak wysp ").Append((100.0 * FreeHands.IronF).ToString("0.#", Inv)).Append("% z ").Append(FreeHands.IronFromBook ? "ksiegi" : "BK 169c").Append(')');
                    f = false;
                }
                if (f) sb.Append(" -");
                sb.Append(" | dzis: ze sprzedazy jencow +").Append(g.Sale.ToString("0.#", Inv)).Append(" (osad ").Append(g.SaleHolds).Append("), z patroli BK +").Append(g.Patrol.ToString("0.#", Inv))
                  .Append(", bilans otwarcia +").Append(F0(g.Seed)).Append(", smierc -").Append(g.Dead.ToString("0.###", Inv)).Append(" (").Append(pct).Append("%/rok");
                if (g.DeadRealm != null && g.DeadRealm.Count > 0) sb.Append(": ").Append(string.Join(", ", g.DeadRealm.OrderByDescending(x => x.Value).Take(6).Select(x => x.Key + " " + x.Value.ToString("0.###", Inv)).ToArray()));
                sb.Append(") | wedlug pochodzenia (top 8): ").Append(origin.Count > 0 ? string.Join(", ", origin.OrderByDescending(x => x.Value).Take(8).Select(x => x.Key + " " + F0(x.Value)).ToArray()) : "-")
                  .Append("; bez pochodzenia ").Append(F0(unknown))
                  .Append(" | poza kanonem (prawo wylaczone albo osada bez niewoli) ").Append(g.Outside.ToString("0.#", Inv))
                  .Append(" | kontrola: T - Twczoraj - (s + b + bilans - d) = ").Append(g.Check.ToString("0.00", Inv)).Append(" (wczoraj ").Append(F0(g.T0)).Append(')')
                  .Append(" | potkniecia ").Append(_stumblesDay).Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Daily(linia)", e); }
            _stumblesDay = 0;
        }
    }
}
