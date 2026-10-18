using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Threading;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// r4-glodbunt (noc 17/18.10; Jeff, wiazace: "nie robmy sztucznego prawa, jak gloduja to znaczy ze musza jesc tansze jedzenie, a jak nie ma jedzenia to bedzie bunt";
    /// jedna regula dla kazdego miasta swiata, bez regul jednej krainy) - GLOD MIASTA -> NIEPOKOJ. Klucz KsHungerUnrest (wylaczony = tylko zasady gry co do punktu).
    ///  - POMIAR (postfiks ItemConsumptionBehavior.UpdateDemandShift, Priority.Last - tuz przed konsumpcja doby miasta, tzn. "w porze posilku"): kcal koszyka mieszczan
    ///    (koszyk ZC4 zaplacony z Planu x lambda - StartCalibration.KsTownMeal) wobec kcal jedzenia koszyka na polce w tej chwili. Kcal wszystkich pozycji sa wymienne -
    ///    biedny je tansze (ziarno zamiast miesa), wiec brak pieniedzy na drozsze jedzenie NIE jest glodem (to kotwica kiesy, drabina ZC4 i chleb M3); glodem jest brak
    ///    jedzenia na rynku: brak rynku u_r = max(0, 1 - kcal na polce / kcal koszyka). Srednia krocząca ok. 3 dob (waga 1/3) - jednodniowy dolek polki nie robi buntu.
    ///    Tylko miasta z koszykiem ZC4 po rampie (kalibracja zastosowana, lambda >= 0.999); zamki i miasta poza KS - bez zmian.
    ///  - SPICHLERZ GRY (zapasy): miasto z Food 0 (gra IsStarving) - brak u_s = niedobor / spozycie (ten sam wzor co Famine.cs przy zgonach mieszczan: dobrobyt / 40 +
    ///    zaloga / 20; bez schronionych uchodzcow - sa tylko w oblezeniu). Glod miasta u = max(u_r, u_s).
    ///  - SKUTEK (postfiksy na CalculateLoyaltyChange / CalculateSecurityChange CZYNNEGO modelu - u Jeffa BKLoyaltyModel i NavalDLCSettlementSecurityModel; PatchTarget.OfActive,
    ///    klasa deklarujaca; zakladane w kampanii - BKLoyaltyModel ma pola statyczne z GameTexts): czesc kary f = (u - prog) / (1 - prog) ponad KsHungerTolerance (0.1);
    ///    lojalnosc H = KsHungerLoyaltyMax (4) x f, bezpieczenstwo KsHungerSecurityMax (2) x f na dobe, linia "Hunger". Dryf gry i BK ciagnie lojalnosc do 50 o 0.1 x odleglosc
    ///    (bezpieczenstwo o 1/15), wiec pelny glod to ok. -40 lojalnosci i -30 bezpieczenstwa trwale; BK dolicza do lojalnosci niskie bezpieczenstwo (do -2) - lancuch gry.
    ///    Bunt robi gra/BK sama (RebellionsCampaignBehavior: lojalnosc <= 15 i milicja >= 1.4 x zaloga z druzynami) - zadnego nowego kodu buntu; 50 000 dla przywodcy z kasy
    ///    zbuntowanego miasta (RotLeaks37 RebelPurseFromTown) - pieniadze pana z KsLordFood nie leza w kasie poza nawiasem zakupow KS-H, wiec bunt ich nie bierze.
    ///  - BEZ PODWOJNEGO LICZENIA: lojalnosc gry ma juz czlon jedzenia z Food (BK: "Starving" -2 przy Food 0, "Well fed populace" +0.5 przy Food >= 80% limitu; model gry:
    ///    -1, po 14 dobach -2). Nasza linia = min(0, -H - czlon gry): laczny czlon jedzenia = min(czlon gry, -H) przy karze gry i -H przy premii (premia "dobrze nakarmieni" nie
    ///    dziala, gdy ludzie gloduja). Bezpieczenstwo gry czlonu jedzenia nie ma. Rodzaj modelu z lancucha typu (BK fallback dla osady bez danych BK - rzadki - liczony jak BK).
    ///  - Zapis gry: nic (srednia krocząca w pamieci sesji - po wczytaniu pierwsza doba bez linii, potem od pomiaru). Linia doby "Glod miast (KS): dzien N".
    /// Wyjatki per miasto / wywolanie - licznik potkniec (CLAUDE.md 7), nic nie gasimy.
    /// </summary>
    internal static class KsHunger
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const double EmaW = 1.0 / 3.0;          // srednia krocząca braku rynku (ok. 3 dob)
        private const int StaleDays = 3;                // miasto bez posilku od tylu dob - poza migawka
        private const float RebelLoyGate = 25f;         // stan buntu gry (RebelliousStateStartLoyaltyThreshold 25) - kontrola "bunt z niczego"
        private const double LamFull = 0.999;           // tylko po rampie koszyka ZC4 (przed nia polke dzieli jeszcze budzet BK)
        internal const string HarmonyId = "com.jeff.armoury.ksglod";

        private static Harmony _h;
        private static readonly HashSet<MethodBase> _patched = new HashSet<MethodBase>();
        private static bool _mealHooked;
        private static string _mealWhy = "nie zakladany";
        private static RuntimeMethodHandle _loyActive, _secActive;
        private static bool _loyWired, _secWired, _bkLoy;
        private static string _loyName = "-", _secName = "-";
        private static TextObject _txt;                 // leniwie (bez TextObject w konstruktorze statycznym)
        private static int _main = -1;

        // pomiar posilku (watek glowny): ostatni posilek miasta od poprzedniej doby
        private sealed class Meal { public double Need, Shelf; public bool Fresh; }
        private static readonly Dictionary<Town, Meal> _meal = new Dictionary<Town, Meal>();
        // stan miasta (pamiec sesji): srednia braku rynku, doba ostatniego posilku, luka kcal, wczorajsza lojalnosc i glod, wlasciciel buntownik
        private sealed class TS { public double U = -1, Gap; public int LastDay = -1; public float LoyY = -1, UY; public bool Rebel, RebelKnown; }
        private static readonly Dictionary<Town, TS> _ts = new Dictionary<Town, TS>();

        /// <summary>Migawka doby (czytana przez postfiksy i KsLordFood): u - glod 0..1, Umk - brak rynku (srednia), Ust - spichlerz gry przy Food 0, F - czesc kary 0..1,
        /// GapKcal - luka kcal dzisiejszego posilku, GapValue - jej wartosc w ziarnie po cenie polki miasta (zl/d).</summary>
        internal sealed class Row { public float U, Umk, Ust, F; public double GapKcal, GapValue; }
        private static Dictionary<Town, Row> _snap = new Dictionary<Town, Row>();

        private static int _cLoy, _cSec, _mealOff, _mealN;   // Interlocked (postfiksy)
        private static int _lastDay = int.MinValue;
        private static int _stumbles;
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static readonly object _lock = new object();

        private static bool UnrestOn(Settings s) { return s != null && s.KsHungerUnrest; }
        private static bool MeasureOn(Settings s) { return s != null && (s.KsHungerUnrest || s.KsLordFundsFood); }
        private static double Clamp(double v, double lo, double hi) { return double.IsNaN(v) ? lo : (v < lo ? lo : (v > hi ? hi : v)); }
        internal static double Tol(Settings s) { return Clamp(s != null ? s.KsHungerTolerance : 0.1, 0, 0.9); }
        private static double LMax(Settings s) { return Clamp(s.KsHungerLoyaltyMax, 0, 20); }
        private static double SMax(Settings s) { return Clamp(s.KsHungerSecurityMax, 0, 20); }
        private static int Today() { return (int)CampaignTime.Now.ToDays; }

        private static void Stumble(string where, Exception e)
        {
            lock (_lock)
            {
                _stumbles++;
                try { if (_err.Add(where)) Log.Error("KsHunger." + where, e); } catch { }
            }
        }

        /// <summary>Nowa kampania i wczytanie (z EnsureHooks - OnSessionLaunched): pamiec sesji od nowa.</summary>
        private static void Reset()
        {
            _meal.Clear(); _ts.Clear(); _snap = new Dictionary<Town, Row>();
            _cLoy = _cSec = _mealOff = _mealN = 0; _lastDay = int.MinValue;
            lock (_lock) { _stumbles = 0; _err.Clear(); }
        }

        // ------------------------------------------------------------ latki (kampania)
        /// <summary>Z ArmouryBehavior.OnSessionLaunched (kampania istnieje, modele zarejestrowane): pomiar posilku (raz na proces) i postfiksy na metody CZYNNYCH modeli
        /// lojalnosci i bezpieczenstwa (ta sama metoda najwyzej raz na proces; postfiks liczy tylko na metodzie czynnego modelu tej sesji). Stan sesji od nowa (takze KsLordFood).</summary>
        internal static void EnsureHooks()
        {
            Reset();
            try { KsLordFood.Reset(); } catch (Exception e) { Stumble("KsLordFood.Reset", e); }
            if (Campaign.Current == null) return;
            _main = Environment.CurrentManagedThreadId;
            try { if (_h == null) _h = new Harmony(HarmonyId); }
            catch (Exception e) { Stumble("Harmony", e); return; }
            if (!_mealHooked)
            {
                try
                {
                    var m = AccessTools.Method(typeof(ItemConsumptionBehavior), "UpdateDemandShift", new[] { typeof(Town), typeof(Dictionary<ItemCategory, float>) });
                    if (m == null) _mealWhy = "BRAK metody UpdateDemandShift";
                    else
                    {
                        _h.Patch(m, postfix: new HarmonyMethod(typeof(KsHunger), nameof(MealPostfix)) { priority = Priority.Last });
                        _mealHooked = Ours(m);
                        _mealWhy = _mealHooked ? "wpiety (UpdateDemandShift, Last)" : "BRAK (Harmony nie zalozyl latki)";
                    }
                }
                catch (Exception e) { _mealWhy = "wyjatek"; Stumble("EnsureHooks(posilek)", e); }
            }
            _loyWired = Wire(true, out _loyName);
            _secWired = Wire(false, out _secName);
            var s = Settings.Current;
            Log.Info("Glod miast (KS): start sesji | posilek " + _mealWhy + " | lojalnosc " + (_loyWired ? "wpieta: " + _loyName : "BRAK (" + _loyName + ")")
                     + (_bkLoy ? " (czlon jedzenia BK: Starving -2, Well fed +0.5)" : " (czlon jedzenia gry: Starving -1, po 14 dobach -2)")
                     + " | bezpieczenstwo " + (_secWired ? "wpiete: " + _secName : "BRAK (" + _secName + ")")
                     + " | Ks Hunger Unrest " + (UnrestOn(s) ? "tak" : "nie") + ", Ks Lord Funds Food " + (s != null && s.KsLordFundsFood ? "tak" : "nie") + ".");
        }

        /// <summary>Harmony.GetPatchInfo na metodzie deklarujacej: czy nasza latka (wlasciciel HarmonyId) naprawde wisi (finalizer BKROTPatch polyka bledy Patch - PatchTarget.cs).</summary>
        private static bool Ours(MethodBase m)
        {
            try
            {
                var pi = Harmony.GetPatchInfo(m);
                if (pi == null) return false;
                foreach (var p in pi.Postfixes) if (p != null && p.owner == HarmonyId) return true;
                return false;
            }
            catch { return false; }
        }

        private static bool Wire(bool loyalty, out string name)
        {
            name = "-";
            try
            {
                var models = Campaign.Current.Models;
                object model = models == null ? null : (loyalty ? (object)models.SettlementLoyaltyModel : models.SettlementSecurityModel);
                if (model == null) { name = "brak modelu"; return false; }
                string meth = loyalty ? "CalculateLoyaltyChange" : "CalculateSecurityChange";
                var m = PatchTarget.OfActive(model, meth, new[] { typeof(Town), typeof(bool) }, loyalty ? "glod miast - lojalnosc (r4-glodbunt)" : "glod miast - bezpieczenstwo (r4-glodbunt)");
                if (loyalty)
                {
                    _bkLoy = false;
                    for (var t = model.GetType(); t != null && t != typeof(object); t = t.BaseType) if (t.Name == "BKLoyaltyModel") { _bkLoy = true; break; }
                }
                if (m == null) { name = "brak metody " + meth + "(Town, bool) w " + model.GetType().FullName; return false; }
                if (!_patched.Contains(m))
                {
                    _h.Patch(m, postfix: new HarmonyMethod(typeof(KsHunger), loyalty ? nameof(LoyaltyPostfix) : nameof(SecurityPostfix)));
                    _patched.Add(m);
                }
                name = (m.DeclaringType != null ? m.DeclaringType.FullName : "?") + "." + meth + " (czynny model " + model.GetType().FullName + ")";
                if (!Ours(m)) { name += " - Harmony nie zalozyl latki"; return false; }
                if (loyalty) _loyActive = m.MethodHandle; else _secActive = m.MethodHandle;
                return true;
            }
            catch (Exception e) { name = "wyjatek " + e.GetType().Name; Stumble(loyalty ? "Wire(lojalnosc)" : "Wire(bezpieczenstwo)", e); return false; }
        }

        // ------------------------------------------------------------ pomiar posilku
        /// <summary>Postfiks UpdateDemandShift (tuz przed konsumpcja doby miasta): kcal koszyka mieszczan i kcal jedzenia na polce (StartCalibration.KsTownMeal).</summary>
        public static void MealPostfix(Town __0)
        {
            try
            {
                if (__0 == null || !__0.IsTown) return;
                var s = Settings.Current;
                if (!MeasureOn(s)) return;
                if (Environment.CurrentManagedThreadId != _main) { Interlocked.Increment(ref _mealOff); return; }
                double need, shelf, lam;
                if (!StartCalibration.KsTownMeal(__0, out need, out shelf, out lam) || lam < LamFull || !(need > 0)) return;
                Meal m;
                if (!_meal.TryGetValue(__0, out m)) { m = new Meal(); _meal[__0] = m; }
                m.Need = need; m.Shelf = Math.Max(0, shelf); m.Fresh = true;
                _mealN++;
            }
            catch (Exception e) { Stumble("MealPostfix", e); }
        }

        /// <summary>Spichlerz gry: miasto z Food 0 - niedobor / spozycie (wzor Famine.cs: dobrobyt / 40 + zaloga / 20), 0..1; inaczej 0.</summary>
        private static double StockShort(Town t)
        {
            if (t.Settlement == null || !t.Settlement.IsStarving) return 0;
            var gp = t.GarrisonParty;
            double cons = Math.Max(0f, t.Prosperity) / 40.0 + (gp != null && gp.MemberRoster != null ? gp.MemberRoster.TotalManCount : 0) / 20.0;
            double deficit = Math.Max(0.0, -(double)t.FoodChange);
            return cons > 0 ? Clamp(deficit / cons, 0, 1) : 0;
        }

        /// <summary>Czlon jedzenia lojalnosci czynnego modelu (te same warunki co BKLoyaltyModel / DefaultSettlementLoyaltyModel.GetSettlementLoyaltyChangeDueToFoodStocks).</summary>
        private static double GameFood(Town t)
        {
            var st = t.Settlement;
            if (st == null) return 0;
            if (_bkLoy)
            {
                int full = (int)(t.FoodStocksUpperLimit() * 0.8f);
                if (t.FoodStocks >= full) return 0.5;
                return st.IsStarving ? -2.0 : 0.0;
            }
            if (!st.IsStarving) return 0;
            double v = -1;
            if (st.Party != null && st.Party.DaysStarving > 14f) v -= 1;
            return v;
        }

        /// <summary>Nasza linia lojalnosci przy czesci kary f: min(0, -H - czlon gry) - laczny czlon jedzenia = min(czlon gry, -H) (premia gry - -H).</summary>
        private static double LoyLine(Town t, double f, Settings s, out double game)
        {
            game = 0;
            double h = LMax(s) * f;
            if (!(h > 0)) return 0;
            game = GameFood(t);
            return Math.Min(0, -h - game);
        }

        // ------------------------------------------------------------ postfiksy modeli
        /// <summary>Postfiks CalculateLoyaltyChange(Town, bool) czynnego modelu: pozycja "Hunger" (migawka doby, czlon jedzenia gry odjety - bez podwojnego liczenia).</summary>
        public static void LoyaltyPostfix(MethodBase __originalMethod, Town __0, bool __1, ref ExplainedNumber __result)
        {
            try
            {
                if (__0 == null || __originalMethod == null || !__originalMethod.MethodHandle.Equals(_loyActive)) return;
                var snap = _snap; Row r;
                if (snap.Count == 0 || !snap.TryGetValue(__0, out r) || !(r.F > 0)) return;
                var s = Settings.Current;
                if (!UnrestOn(s) || !StartCalibration.Applied) return;
                double g;
                double x = LoyLine(__0, r.F, s, out g);
                if (!(x < 0)) return;
                if (_txt == null) _txt = new TextObject("Hunger");
                __result.Add((float)x, _txt);
                Interlocked.Increment(ref _cLoy);
            }
            catch (Exception e) { Stumble("LoyaltyPostfix", e); }
        }

        /// <summary>Postfiks CalculateSecurityChange(Town, bool) czynnego modelu: pozycja "Hunger" = -KsHungerSecurityMax x f (bezpieczenstwo gry nie ma czlonu jedzenia).</summary>
        public static void SecurityPostfix(MethodBase __originalMethod, Town __0, bool __1, ref ExplainedNumber __result)
        {
            try
            {
                if (__0 == null || __originalMethod == null || !__originalMethod.MethodHandle.Equals(_secActive)) return;
                var snap = _snap; Row r;
                if (snap.Count == 0 || !snap.TryGetValue(__0, out r) || !(r.F > 0)) return;
                var s = Settings.Current;
                if (!UnrestOn(s) || !StartCalibration.Applied) return;
                double x = -SMax(s) * r.F;
                if (!(x < 0)) return;
                if (_txt == null) _txt = new TextObject("Hunger");
                __result.Add((float)x, _txt);
                Interlocked.Increment(ref _cSec);
            }
            catch (Exception e) { Stumble("SecurityPostfix", e); }
        }

        // ------------------------------------------------------------ dla KsLordFood
        /// <summary>Miasta glodne tej doby (czesc kary f > 0) z luka rynku i jej wartoscia - KsLordFood (pan miasta placi za dowoz). Kolejnosc wedlug StringId (stala).</summary>
        internal static void Hungry(List<KeyValuePair<Town, Row>> outList)
        {
            outList.Clear();
            foreach (var kv in _snap) if (kv.Key != null && kv.Value != null && kv.Value.F > 0) outList.Add(kv);
            outList.Sort((a, b) => string.CompareOrdinal(a.Key.Settlement != null ? a.Key.Settlement.StringId : "", b.Key.Settlement != null ? b.Key.Settlement.StringId : ""));
        }

        // ------------------------------------------------------------ doba (KsLordFood.Before - SupplyDemand.DailyTrade, przed kupcami zywnosci KS-H)
        /// <summary>Raz na dobe: posilki od wczoraj -> srednia braku rynku, spichlerz gry, migawka dla postfiksow, bunty od wczoraj, linia "Glod miast (KS)".</summary>
        internal static void Daily()
        {
            if (Campaign.Current == null) return;
            int day = Today();
            if (day == _lastDay) return;
            _lastDay = day;
            long t0 = Stopwatch.GetTimestamp();
            var s = Settings.Current;
            if (s == null) return;
            bool measure = MeasureOn(s) && StartCalibration.Applied, unrest = UnrestOn(s);
            double tol = Tol(s);
            var snap = new Dictionary<Town, Row>();
            int nKs = 0, nFresh = 0, nHungry = 0, nStarv = 0, nStarvHungry = 0, nGame = 0, nWell = 0, nRebState = 0, newReb = 0, newRebHungry = 0, newRebHigh = 0;
            double loySum = 0, secSum = 0, red = 0, ctl = 0, loyHungry = 0, loyMin = double.NaN, uMax = -1;
            string minName = "-", maxName = "-";
            var us = new List<double>(128);
            var rebNames = new List<string>();
            try
            {
                foreach (var t in Town.AllTowns)
                {
                    try
                    {
                        if (t == null || !t.IsTown || t.Settlement == null) continue;
                        TS ts;
                        if (!_ts.TryGetValue(t, out ts)) { ts = new TS(); _ts[t] = ts; }
                        // bunt od wczoraj: wlasciciel miasta stal sie rodem buntownikow (gra/BK StartRebellionEvent) - kontrola "bunt z niczego"
                        bool rebel = t.OwnerClan != null && t.OwnerClan.IsRebelClan;
                        if (ts.RebelKnown && rebel && !ts.Rebel)
                        {
                            newReb++;
                            if (ts.UY > tol) newRebHungry++;
                            if (ts.LoyY > RebelLoyGate) newRebHigh++;
                            if (rebNames.Count < 6) rebNames.Add(NameOf(t) + " (lojalnosc wczoraj " + (ts.LoyY >= 0 ? ts.LoyY.ToString("0", Inv) : "-") + ", glod " + (100 * ts.UY).ToString("0", Inv) + "%)");
                        }
                        ts.Rebel = rebel; ts.RebelKnown = true;
                        Meal m;
                        if (measure && _meal.TryGetValue(t, out m) && m.Fresh)
                        {
                            double ur = Clamp(1 - m.Shelf / m.Need, 0, 1);
                            ts.U = ts.U < 0 ? ur : ts.U + EmaW * (ur - ts.U);
                            ts.Gap = Math.Max(0, m.Need - m.Shelf);
                            ts.LastDay = day;
                            m.Fresh = false;
                            nFresh++;
                        }
                        if (!measure || ts.LastDay < 0 || day - ts.LastDay > StaleDays || ts.U < 0)
                        {
                            if (!measure) { ts.U = -1; ts.LastDay = -1; }
                            ts.LoyY = t.Loyalty; ts.UY = 0;
                            continue;
                        }
                        if (ts.LastDay != day) ts.Gap = 0;                   // bez posilku dzis - luki dzisiejszej nie ma (srednia zostaje)
                        nKs++;
                        double ust = StockShort(t);
                        double u = Math.Max(ts.U, ust);
                        double f = u > tol ? (u - tol) / (1 - tol) : 0;
                        var r = new Row { U = (float)u, Umk = (float)ts.U, Ust = (float)ust, F = (float)f, GapKcal = ts.Gap };
                        if (ts.Gap > 0)
                        {
                            double kp; int pr;
                            if (StartCalibration.KsTownGrain(t, out kp, out pr)) r.GapValue = ts.Gap / kp * pr;
                        }
                        snap[t] = r;
                        us.Add(ts.U);
                        if (ts.U > uMax) { uMax = ts.U; maxName = NameOf(t); }
                        if (ust > 0) { nStarv++; if (ts.U > tol) nStarvHungry++; }
                        if (f > 0)
                        {
                            nHungry++;
                            double game, x = LoyLine(t, f, s, out game), h = LMax(s) * f;
                            if (unrest)
                            {
                                loySum += x; secSum += -SMax(s) * f;
                                if (game < 0) { nGame++; red += Math.Min(h, -game); }
                                else if (game > 0) nWell++;
                                // kontrola: nasza linia + czlon gry = min(czlon gry, -H) przy karze gry, -H przy premii albo bez czlonu (oczekiwane 0)
                                double expect = game < 0 ? Math.Min(game, -h) : -h;
                                ctl += Math.Abs(x + game - expect);
                            }
                            double loy = t.Loyalty;
                            loyHungry += loy;
                            if (double.IsNaN(loyMin) || loy < loyMin) { loyMin = loy; minName = NameOf(t); }
                            if (t.InRebelliousState) nRebState++;
                        }
                        ts.LoyY = t.Loyalty; ts.UY = (float)u;
                    }
                    catch (Exception e) { Stumble("Daily(miasto)", e); }
                }
            }
            catch (Exception e) { Stumble("Daily", e); }
            _snap = snap;
            if (!s.LogEnabled) return;
            try
            {
                us.Sort();
                double med = us.Count > 0 ? us[us.Count / 2] : 0, q90 = us.Count > 0 ? us[Math.Min(us.Count - 1, (int)(0.9 * us.Count))] : 0;
                int cL = Interlocked.Exchange(ref _cLoy, 0), cS = Interlocked.Exchange(ref _cSec, 0), off = Interlocked.Exchange(ref _mealOff, 0), mn = _mealN; _mealN = 0;
                var sb = new StringBuilder(1400);
                sb.Append("Glod miast (KS): dzien ").Append(day)
                  .Append(" | klucze: Ks Hunger Unrest ").Append(unrest ? "tak" : "nie").Append(", Ks Lord Funds Food ").Append(s.KsLordFundsFood ? "tak" : "nie")
                  .Append(", kalibracja ").Append(StartCalibration.Applied ? "zastosowana" : "NIE zastosowana (bez pomiaru)")
                  .Append("; prog ").Append((100 * tol).ToString("0", Inv)).Append("%, kara do ").Append(LMax(s).ToString("0.0", Inv)).Append(" lojalnosci i ")
                  .Append(SMax(s).ToString("0.0", Inv)).Append(" bezpieczenstwa na dobe")
                  .Append(" | latki: lojalnosc ").Append(_loyWired ? "wpieta" : "BRAK").Append(", bezpieczenstwo ").Append(_secWired ? "wpiete" : "BRAK")
                  .Append(", posilek ").Append(_mealHooked ? "wpiety" : "BRAK")
                  .Append(" | miasta w KS ").Append(nKs).Append(" (z posilkiem dzis ").Append(nFresh).Append(", posilkow od wczoraj ").Append(mn).Append(", poza watkiem ").Append(off).Append(')')
                  .Append(" | brak jedzenia na rynku (srednia 3 dob): mediana ").Append((100 * med).ToString("0.0", Inv)).Append("%, q90 ").Append((100 * q90).ToString("0.0", Inv))
                  .Append("%, najwiekszy ").Append(uMax >= 0 ? maxName + " " + (100 * uMax).ToString("0.0", Inv) + "%" : "-")
                  .Append(" | glodne ").Append(nHungry).Append(": kara lojalnosci razem ").Append(loySum.ToString("0.00", Inv))
                  .Append(" (srednio ").Append((nHungry > 0 ? loySum / nHungry : 0).ToString("0.00", Inv)).Append("), bezpieczenstwa razem ").Append(secSum.ToString("0.00", Inv))
                  .Append(" | Food 0 gry ").Append(nStarv).Append(" miast (z brakiem rynku ").Append(nStarvHungry).Append(')')
                  .Append("; z kara gry Starving ").Append(nGame).Append(" - nasza linia mniejsza razem o ").Append(red.ToString("0.00", Inv))
                  .Append(" pkt; premia Well fed zdjeta ").Append(nWell).Append("; kontrola podwojnego liczenia ").Append(ctl.ToString("0.00", Inv))
                  .Append(" | lojalnosc glodnych srednio ").Append(nHungry > 0 ? (loyHungry / nHungry).ToString("0.0", Inv) : "-")
                  .Append(", najnizsza ").Append(double.IsNaN(loyMin) ? "-" : minName + " " + loyMin.ToString("0.0", Inv)).Append(", w stanie buntu ").Append(nRebState)
                  .Append(" | bunty od wczoraj ").Append(newReb).Append(" (glodne ").Append(newRebHungry).Append("; lojalnosc wczoraj > ").Append(RebelLoyGate.ToString("0", Inv)).Append(": ").Append(newRebHigh).Append(')');
                if (rebNames.Count > 0) sb.Append(": ").Append(string.Join("; ", rebNames.ToArray()));
                sb.Append(" | wywolania modelu od wczoraj: lojalnosc ").Append(cL).Append(", bezpieczenstwo ").Append(cS)
                  .Append(" | czas ").Append(((Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency).ToString("0.000", Inv)).Append(" ms | potkniecia ");
                lock (_lock) { sb.Append(_stumbles).Append('.'); _stumbles = 0; }
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Daily(linia)", e); }
        }

        private static string NameOf(Town t)
        {
            try { return t.Settlement.Name != null ? t.Settlement.Name.ToString() : t.Settlement.StringId; }
            catch { return "?"; }
        }
    }
}
