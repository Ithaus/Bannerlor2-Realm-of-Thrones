using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// ETAP 3.1, KAWALEK E3/2 (paczka 187) - MAJATKI BK: DOCHOD TYLKO ZE SPRZEDAZY PLONU (PROJEKT-ETAP3-SZCZELNOSC-STATKI-2026-10-10 rozdz. 2 R6
    /// i rozdz. 3 E3/2; rozpoznanie projekt31\A-majatki-bk.md; krytyka W7). Wylacznik BkEstateIncomeFromSalesOnly.
    ///
    /// BK (dekompilacja BannerKings.dll, Redux): EstateData.DailyProductionIncome (BKSettlementBehavior, krok "EstateProductionIncome", raz na dobe
    /// dla kazdej wsi) wyplaca kazdemu majatkowi "dochod z produkcji" GiveGoldAction nic -> wlasciciel (hektary x robotnicy, bez towaru) i nadpisuje
    /// Estate.LastIncome ta kwota (jedyne zerowanie). Ci sami robotnicy sa juz w produkcji wsi (BKVillageProductionModel liczy robotnikow calej wsi),
    /// a prawdziwy dochod majatku istnieje: przy powrocie taboru EstateData.AccumulateTradeTax dzieli podatek z utargu (placi kasa miasta, ktora
    /// kupila plon) i robi LastIncome += udzial. Log biegu 457 dob: ok. 293 tys. zl/dobe z niczego, 99.4% do notabli wiejskich.
    ///
    /// TERAZ (przy wylaczniku):
    ///  - prefiks bool na DailyProductionIncome: wyplata pominieta (return false; takze po wyjatku - licznik potkniec, bez globalnego wylacznika);
    ///    okno ksiegi obiegu "majatki-BK" (prefiks void Priority.First + finalizer) biegnie dalej i pokazuje wywolania bez trafien;
    ///  - w tym samym prefiksie LastIncome kazdego majatku = srednia biezaca 28 dob udzialu z utargu (W7): dzis = LastIncome - wpisane wczoraj
    ///    (to, co BK dopisalo przez AccumulateTradeTax), sr = sr + (dzis - sr) / 28, LastIncome = sr (w gore do 1 zl, gdy majatek mial dochod
    ///    w ostatnich 28 dobach - nie skacze do 0). Bez nowego stanu w zapisie: srednia siedzi w polu BK, slownik sesji po wczytaniu startuje od
    ///    LastIncome z zapisu. Cena majatku BK (LastIncome x DaysInYear) i model finansow BK (podglad) widza te sama srednia;
    ///  - postfiks na getter Estate.EstimatedDailyIncome (czytany tylko w oknach BK i cheatach): projekt 3.1 pisal "bez latki", bo bral getter
    ///    Income (zwraca LastIncome) za ten - EstimatedDailyIncome liczy wzor produkcji, wiec okno majatku pokazywaloby dochod, ktorego nie ma.
    ///    Teraz zwraca srednia (0 zostaje 0: majatek zablokowany albo bez robotnikow - jak w BK).
    /// Platnik -> odbiorca: kasa miasta (kupila plon) -> tabor -> majatek (to juz bylo); z niczego - nic.
    /// Linia logu "Majatki BK (187): dzien N" raz na dobe (po ksiedze pieniadza).
    /// </summary>
    internal static class EstateIncome187
    {
        internal static bool On { get { var s = Settings.Current; return s != null && s.BkEstateIncomeFromSalesOnly; } }
        private static bool Live { get { var c = Campaign.Current; return c != null && c.GameStarted; } }
        private const float Days = 28f;

        private static Harmony _harmony;
        internal static void SetHarmony(Harmony h) { _harmony = h; }
        private static bool _hooksTried, _prodWired, _estWired;
        private static string _hookNote = "-";

        // BK przez refleksje (raz na proces)
        private static PropertyInfo _pEstates, _pLast;

        // srednia na majatek (klucz - obiekt Estate BK, porownanie po referencji); stan sesji, nie w zapisie
        private sealed class Avg { public float A; public int W; public int LastDay; }
        private sealed class RefEq : IEqualityComparer<object>
        {
            public new bool Equals(object x, object y) { return ReferenceEquals(x, y); }
            public int GetHashCode(object o) { return RuntimeHelpers.GetHashCode(o); }
        }
        private static readonly Dictionary<object, Avg> _avg = new Dictionary<object, Avg>(new RefEq());

        // liczniki doby
        private static int _villages, _estates, _fresh, _paidToday, _with28, _zeroBad, _resets, _stumbles;
        private static long _addSum, _writtenSum;
        private static double _avgSum;
        private static readonly HashSet<string> _err = new HashSet<string>();

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_err.Add(where)) Log.Error("EstateIncome187." + where, e); } catch { }
        }

        private static void ZeroDay()
        {
            _villages = _estates = _fresh = _paidToday = _with28 = _zeroBad = _resets = _stumbles = 0;
            _addSum = _writtenSum = 0; _avgSum = 0;
        }

        // ------------------------------------------------------------ zakladanie latek (ArmouryBehavior.EnsureHooks, start sesji)
        internal static void EnsureHooks()
        {
            _avg.Clear(); ZeroDay();   // nowa sesja / kampania: srednie od zapisu BK, liczniki od zera (latki zostaja w procesie)
            if (_hooksTried || _harmony == null || Campaign.Current == null) return;
            _hooksTried = true;
            var wired = new List<string>(); var missing = new List<string>();
            try
            {
                var edT = AccessTools.TypeByName("BannerKings.Managers.Populations.Estates.EstateData");
                var esT = AccessTools.TypeByName("BannerKings.Managers.Populations.Estates.Estate");
                _pEstates = edT != null ? AccessTools.Property(edT, "Estates") : null;
                _pLast = esT != null ? AccessTools.Property(esT, "LastIncome") : null;
                var prod = edT != null ? AccessTools.Method(edT, "DailyProductionIncome", Type.EmptyTypes) : null;
                bool refl = _pEstates != null && _pLast != null && _pLast.CanWrite && _pLast.PropertyType == typeof(int);
                if (prod != null && refl)
                {
                    // priorytet zwykly: okno ksiegi obiegu (prefiks Priority.First) otwiera sie przed nami i domyka finalizerem
                    _harmony.Patch(prod, prefix: new HarmonyMethod(typeof(EstateIncome187), nameof(ProductionPrefix)));
                    _prodWired = true; wired.Add("wyplata z produkcji (EstateData.DailyProductionIncome)");
                }
                else missing.Add("wyplata z produkcji (EstateData.DailyProductionIncome" + (prod == null ? "" : " - brak Estates/LastIncome") + ")");
                var est = esT != null ? AccessTools.PropertyGetter(esT, "EstimatedDailyIncome") : null;
                if (est != null && est.ReturnType == typeof(float) && _pLast != null)
                {
                    _harmony.Patch(est, postfix: new HarmonyMethod(typeof(EstateIncome187), nameof(EstimatedPostfix)) { priority = Priority.Last });
                    _estWired = true; wired.Add("okno majatku (Estate.EstimatedDailyIncome)");
                }
                else missing.Add("okno majatku (Estate.EstimatedDailyIncome)");
            }
            catch (Exception e) { missing.Add("blad: " + e.Message); Stumble("EnsureHooks", e); }
            _hookNote = (wired.Count > 0 ? string.Join(", ", wired.ToArray()) : "-") + "; BRAK: " + (missing.Count > 0 ? string.Join(", ", missing.ToArray()) : "-");
            Log.Info("Majatki BK (187): latki - " + _hookNote + " | wylacznik BK Estate Income From Sales Only " + (On ? "WLACZONY" : "wylaczony") + ".");
        }

        // ------------------------------------------------------------ BK EstateData.DailyProductionIncome (kazda wies raz na dobe)
        /// <summary>Przy wylaczniku: wyplata "z produkcji" pominieta, LastIncome kazdego majatku wsi = srednia 28 dob udzialu z utargu.</summary>
        public static bool ProductionPrefix(object __instance)
        {
            if (!On) return true;
            try { Average(__instance); }
            catch (Exception e) { Stumble("ProductionPrefix", e); }
            return false;   // wyplata z niczego pominieta takze po wyjatku (licznik potkniec - mechanizmu nie gasimy)
        }

        private static void Average(object data)
        {
            var list = data != null ? _pEstates.GetValue(data, null) as IList : null;
            _villages++;
            if (list == null || list.Count == 0) return;
            int day = (int)CampaignTime.Now.ToDays;
            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                if (e == null) continue;
                try
                {
                    int li = (int)_pLast.GetValue(e, null);
                    Avg a;
                    if (!_avg.TryGetValue(e, out a))
                    {
                        // pierwszy raz w sesji: srednia startuje od LastIncome z zapisu BK (projekt E3/2)
                        a = new Avg { A = Math.Max(0, li), W = li, LastDay = li > 0 ? day : int.MinValue / 2 };
                        _avg[e] = a; _fresh++;
                    }
                    int add = li - a.W;   // to, co BK dopisalo od wczoraj (AccumulateTradeTax: LastIncome += udzial z utargu)
                    if (add < 0) { a.A = Math.Max(0, li); add = 0; _resets++; }   // ktos zmniejszyl LastIncome (BK ResetToFreshClaim) - od tej wartosci
                    a.A += (add - a.A) / Days;
                    if (a.A < 0f) a.A = 0f;
                    if (add > 0) { a.LastDay = day; _paidToday++; _addSum += add; }
                    bool recent = day - a.LastDay < (int)Days;
                    int w = (int)Math.Round(a.A);
                    if (w <= 0 && recent && a.A > 0f) w = 1;   // dochod w ostatnich 28 dobach - okno nie pokazuje 0 (W7)
                    if (w < 0) w = 0;
                    _pLast.SetValue(e, w, null);
                    a.W = w;
                    _estates++; _avgSum += a.A; _writtenSum += w;
                    if (recent) { _with28++; if (w == 0) _zeroBad++; }
                }
                catch (Exception ex) { Stumble("Average(majatek)", ex); }
            }
        }

        // ------------------------------------------------------------ BK Estate.EstimatedDailyIncome (tylko UI i cheaty)
        /// <summary>Okno majatku: przy wylaczniku zamiast wzoru produkcji (dochodu, ktorego nie ma) srednia 28 dob z LastIncome; 0 zostaje 0.</summary>
        public static void EstimatedPostfix(object __instance, ref float __result)
        {
            try
            {
                if (!On || __result <= 0f || __instance == null) return;
                __result = (int)_pLast.GetValue(__instance, null);
            }
            catch (Exception e) { Stumble("EstimatedPostfix", e); }
        }

        // ------------------------------------------------------------ raz na dobe (po ksiedze pieniadza)
        internal static void Daily()
        {
            try
            {
                if (Campaign.Current == null) return;
                int day = (int)CampaignTime.Now.ToDays;
                // sredni wplyw notabli wiejskich (pasmo gry: wplyw z nadwyzki kiesy) - prog testu 150-300 po zamknieciu
                double pw = 0; int pn = 0;
                foreach (var st in Settlement.All)
                {
                    if (st == null || !st.IsVillage || st.Notables == null) continue;
                    foreach (var h in st.Notables) { if (h == null || !h.IsAlive) continue; pw += h.Power; pn++; }
                }
                var inv = System.Globalization.CultureInfo.InvariantCulture;
                var sb = new StringBuilder(700);
                sb.Append("Majatki BK (187): dzien ").Append(day)
                  .Append(" | wylacznik ").Append(On ? "WL" : "WYL - BK placi \"dochod z produkcji\" z niczego jak dotad")
                  .Append(_prodWired ? "" : " | NIECZYNNE - BRAK latki EstateData.DailyProductionIncome").Append(_estWired ? "" : " | okno majatku bez latki (pokazuje wzor BK)")
                  .Append(" | wsie ").Append(_villages).Append(", majatki ").Append(_estates).Append(" - wyplata z produkcji pominieta (z niczego 0)")
                  .Append(" | udzial z utargu od wczoraj (AccumulateTradeTax, placi kasa miasta) ").Append(_addSum).Append(" w ").Append(_paidToday).Append(" majatkach")
                  .Append(" | srednia 28 dob razem ").Append(_avgSum.ToString("0", inv)).Append(" (wpisane do LastIncome ").Append(_writtenSum).Append(")")
                  .Append(", majatki z dochodem w ostatnich 28 dobach ").Append(_with28).Append(", z tego z LastIncome 0: ").Append(_zeroBad).Append(" (prog 0)")
                  .Append(", nowe w sesji (srednia od zapisu BK) ").Append(_fresh).Append(", wyzerowane przez BK ").Append(_resets)
                  .Append(" | sredni wplyw notabli wiejskich ").Append(pn > 0 ? (pw / pn).ToString("0.0", inv) : "-").Append(" (").Append(pn).Append(" notabli)")
                  .Append("; pasmo notabli - linia \"Obieg: notable i karawany\"")
                  .Append(" | potkniecia ").Append(_stumbles).Append(" | latki: ").Append(_hookNote).Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally { ZeroDay(); }
        }
    }
}
