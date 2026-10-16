using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// AC (Jeff 14.10: "rozpad armii powinien byc wolniejszy, bo teraz 1 dzien to 1 dzien"; zasada CR: wszystko "na dobe" dobrane pod rok gry
    /// 84 doby mnozymy x 84 / rok kalendarza, przy roku 364 dob = 0,2308 - jak PregnancyYear).
    ///
    /// Gra 1.4.8 (dec32): Army.Tick co godzine Cohesion += DailyCohesionChange / 24; armia AI rozwiazuje sie przy spojnosci < 30 (poza bitwa
    /// i oblezeniem; ponizej 50 dowodca moze kupic spojnosc za wplyw - ThinkAboutCohesionBoost), armia gracza przy <= 0,1.
    /// DefaultArmyManagementCalculationModel.CalculateDailyCohesionChange: -2 na dobe, do tego -1 za kazda dolaczona druzyne i po -(n + 1) / 2
    /// za druzyny glodne / z morale <= 25 / z <= 10 ludzmi; armia AI liczy te czlony x0,25. Czynny model (StrategicCampaignAI145
    /// StrategicArmyManagementModel) dodaje -3,5 na dobe po 5 dobach na ziemi wroga (linie zaopatrzenia). Armia AI z 5 druzynami: -3,6 na dobe
    /// (rozpad po ok. 19 dobach), na ziemi wroga -7,1 (ok. 12 dob); armia gracza z 5 druzynami -8,5 (ok. 12 dob).
    ///
    /// Latka: postfiks najbardziej pochodnej CalculateDailyCohesionChange CZYNNEGO modelu (wpiecie przy starcie sesji, gdy modele sa znane; ta sama
    /// metoda raz na proces) - wynik x 84 / rok przez AddFactor((f - 1) x (1 + SumOfFactors)) (ExplainedNumber: wynik = Baza x (1 + suma
    /// czynnikow)), z linia w objasnieniu. Czlon SCA i wszystkie skladniki gry skalowane razem (nie podwojnie: metoda bazowa nie jest latana).
    /// Koszt kupna spojnosci za wplyw bez zmian (kupuje sie rzadziej). Klucz ArmyCohesionYearScale (false = gra); przy roku <= 84 dob - x1.
    /// Log: linia startu sesji (wpieta metoda, mnoznik) i linia doby: armie (AI / gracz), srednia spojnosc, srednia zmiana na dobe teraz i bez
    /// skali, rozwiazane wedlug przyczyny i sredni wiek armii przy rozwiazaniu (ArmyCreated / ArmyDispersed). Bez zapisu (wiek armii z zapisu
    /// nieznany - liczone tylko armie zalozone w tej sesji).
    /// AC-W (Jeff 15.10 "czemu sie rozchodzi?"): linia "Spojnosc armii (AC wiek)" - od startu sesji rozwiazane armie wedlug przyczyny z wiekiem z pola gry
    /// Army._creationTime (takze armie z zapisu i te rozwiazane, zanim nasz nasluch ArmyCreated je zobaczyl) i liczba rozwiazanych w dniu zalozenia.
    /// Powod pomiaru [K]: NotEnoughParty wola nie tylko gra (Army.RemoveParty - odeszla ostatnia druzyna), ale i StrategicCampaignAI145
    /// (OnArmyCreated: wodz z < 22 zdrowymi albo sila < 80, albo 0.75 doby po poprzedniej armii - armia kasowana od razu po zalozeniu).
    /// </summary>
    internal static class ArmyCohesionYear
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly TextObject Label = new TextObject("{=!}Days of the long year (1 day is 1 day)");
        private static readonly HashSet<MethodBase> _patched = new HashSet<MethodBase>();
        private static string _hook = "nie wpieta";
        private static int _errors;
        private static Harmony _h;

        // sesja: armie zalozone w tej sesji -> doba zalozenia
        private static readonly Dictionary<Army, double> _born = new Dictionary<Army, double>();
        private static readonly Dictionary<string, int> _dReasons = new Dictionary<string, int>();
        private static int _dDisp, _dAgedN, _tDisp;
        // AC-W: od startu sesji, przyczyna -> [rozwiazane, w dniu zalozenia (< 1 doby), suma wieku w dobach]
        private static readonly SortedDictionary<string, double[]> _sAge = new SortedDictionary<string, double[]>(StringComparer.Ordinal);
        private static AccessTools.FieldRef<Army, CampaignTime> _ctRef;
        private static bool _ctFail;
        private static double _dAgeSum;
        // AR0 (oblezenia 3, tylko log): NotEnoughParty w dniu zalozenia wedlug wolajacego (rzut stosu): 0 BK wezwanie (CallBannersGoal), 1 SCA (OnArmyCreated),
        // 2 gra (Army.OnRemovePartyInternal - odeszla ostatnia druzyna), 3 inne
        private static readonly int[] _dWho = new int[4];

        /// <summary>AR0: wolajacy DisbandArmyAction.ApplyByNotEnoughParty z rzutu stosu (pierwsza pasujaca ramka od srodka; ramka DMD Harmony - nazwa metody).</summary>
        private static int Who()
        {
            var tr = new System.Diagnostics.StackTrace(2, false);
            for (int i = 0; i < tr.FrameCount; i++)
            {
                var f = tr.GetFrame(i);
                var m = f != null ? f.GetMethod() : null;
                if (m == null) continue;
                string s = m.DeclaringType != null ? m.DeclaringType.FullName + "." + m.Name : m.Name;
                if (string.IsNullOrEmpty(s)) continue;
                if (s.Contains("CallBannersGoal")) return 0;
                if (s.Contains("StrategicCampaignAI145")) return 1;
                if (s.EndsWith(".Army.OnRemovePartyInternal", StringComparison.Ordinal) || s.Contains("Army::OnRemovePartyInternal")) return 2;
            }
            return 3;
        }

        internal static bool On { get { var s = Settings.Current; return s != null && s.ArmyCohesionYearScale; } }

        internal static double Factor() { return On ? PregnancyYear.YearFactor() : 1.0; }

        private static void Stumble(string where, Exception e)
        {
            _errors++;
            if (_errors <= 5 || _errors % 100 == 0) Log.Error("ArmyCohesionYear." + where + " (potkniecie " + _errors + ")", e);
        }

        /// <summary>Postfiks CalculateDailyCohesionChange czynnego modelu: wynik x 84 / rok.</summary>
        public static void CohesionPostfix(ref ExplainedNumber __result)
        {
            try
            {
                double f = Factor();
                if (f >= 1.0) return;
                __result.AddFactor((float)((f - 1.0) * (1.0 + __result.SumOfFactors)), Label);
            }
            catch (Exception e) { Stumble("CohesionPostfix", e); }
        }

        internal static void RegisterEvents(CampaignBehaviorBase owner)
        {
            CampaignEvents.ArmyCreated.AddNonSerializedListener(owner, OnCreated);
            CampaignEvents.ArmyDispersed.AddNonSerializedListener(owner, OnDispersed);
        }

        private static void OnCreated(Army a)
        {
            try { if (a != null) _born[a] = CampaignTime.Now.ToDays; } catch (Exception e) { Stumble("OnCreated", e); }
        }

        private static void OnDispersed(Army a, Army.ArmyDispersionReason reason, bool isPlayers)
        {
            try
            {
                string r = reason.ToString() + (isPlayers ? " (gracz)" : "");
                _dReasons.TryGetValue(r, out int n);
                _dReasons[r] = n + 1;
                _dDisp++; _tDisp++;
                double age = -1.0;
                if (a != null && !_ctFail)
                {
                    try
                    {
                        if (_ctRef == null) _ctRef = AccessTools.FieldRefAccess<Army, CampaignTime>("_creationTime");
                        var ct = _ctRef(a);
                        if (ct != default(CampaignTime))
                        {
                            age = CampaignTime.Now.ToDays - ct.ToDays;
                            double[] v;
                            if (!_sAge.TryGetValue(r, out v)) _sAge[r] = v = new double[3];
                            v[0]++; if (age < 1.0) v[1]++; v[2] += age;
                        }
                    }
                    catch (Exception e) { _ctFail = true; Stumble("OnDispersed.CreationTime", e); }
                }
                if (reason == Army.ArmyDispersionReason.NotEnoughParty && age >= 0.0 && age < 1.0)
                {
                    try { _dWho[Who()]++; } catch (Exception e) { Stumble("OnDispersed.Who", e); }
                }
                if (a != null && _born.TryGetValue(a, out double d0))
                {
                    _dAgeSum += CampaignTime.Now.ToDays - d0; _dAgedN++;
                    _born.Remove(a);
                }
            }
            catch (Exception e) { Stumble("OnDispersed", e); }
        }

        /// <summary>Start sesji (po McmSettings.Apply): wpiecie postfiksu na metode czynnego modelu (raz na metode na proces) i linia startu.</summary>
        internal static void SessionStart()
        {
            if (_h == null) _h = new Harmony("com.jeff.armoury.armycohesionyear");
            var harmony = _h;
            _born.Clear(); _dReasons.Clear(); _dDisp = _dAgedN = _tDisp = 0; _dAgeSum = 0; _sAge.Clear(); for (int i = 0; i < _dWho.Length; i++) _dWho[i] = 0;
            try
            {
                var model = Campaign.Current != null && Campaign.Current.Models != null ? Campaign.Current.Models.ArmyManagementCalculationModel : null;
                if (model == null) { _hook = "BRAK (model armii nieznany)"; }
                else
                {
                    var m = AccessTools.Method(model.GetType(), "CalculateDailyCohesionChange", new[] { typeof(Army), typeof(bool) });
                    if (m == null) _hook = "BRAK (" + model.GetType().FullName + ".CalculateDailyCohesionChange)";
                    else if (_patched.Contains(m)) _hook = "wpieta " + m.DeclaringType.FullName + "." + m.Name + " (juz wczesniej w tym procesie)";
                    else
                    {
                        harmony.Patch(m, postfix: new HarmonyMethod(typeof(ArmyCohesionYear), nameof(CohesionPostfix)) { priority = Priority.Last });
                        _patched.Add(m);
                        _hook = "wpieta " + m.DeclaringType.FullName + "." + m.Name + " (model czynny " + model.GetType().FullName + ")";
                    }
                }
            }
            catch (Exception e) { _hook = "wyjatek " + e.GetType().Name; Stumble("SessionStart", e); }
            try
            {
                Log.Info("Spojnosc armii (AC): start sesji | klucz ArmyCohesionYearScale " + (On ? "TAK" : "NIE") + " | rok " + SafeYear() + " dob, mnoznik x"
                         + Factor().ToString("0.0000", Inv) + " | latka: " + _hook + " | spadek spojnosci na dobe x mnoznik (rozpad AI ponizej 30, gracza przy 0).");
            }
            catch (Exception e) { Stumble("SessionStart.Line", e); }
        }

        private static int SafeYear() { try { return CampaignTime.DaysInYear; } catch { return 0; } }

        internal static void Daily()
        {
            try
            {
                int ai = 0, pl = 0;
                double coh = 0, now = 0, raw = 0;
                double f = Factor();
                var all = new List<Army>();
                foreach (var k in Kingdom.All)
                {
                    if (k == null || k.Armies == null) continue;
                    foreach (var a in k.Armies) if (a != null && a.LeaderParty != null) all.Add(a);
                }
                foreach (var a in all)
                {
                    if (a.LeaderParty == MobileParty.MainParty) pl++; else ai++;
                    coh += a.Cohesion;
                    double ch = a.DailyCohesionChange;
                    now += ch;
                    raw += f > 0 && f < 1.0 ? ch / f : ch;
                }
                int n = all.Count;
                var sb = new System.Text.StringBuilder();
                foreach (var kv in _dReasons) { if (sb.Length > 0) sb.Append(", "); sb.Append(kv.Key).Append(' ').Append(kv.Value); }
                Log.Info("Spojnosc armii (AC): dzien " + (int)CampaignTime.Now.ToDays + " | armie " + n + " (AI " + ai + ", gracz " + pl + ")"
                         + " | spojnosc srednio " + (n > 0 ? (coh / n).ToString("0.0", Inv) : "-")
                         + " | zmiana na dobe srednio teraz " + (n > 0 ? (now / n).ToString("0.00", Inv) : "-") + ", bez skali " + (n > 0 ? (raw / n).ToString("0.00", Inv) : "-")
                         + " | rozwiazane dzis " + _dDisp + (sb.Length > 0 ? " (" + sb + ")" : "")
                         + " | wiek przy rozwiazaniu srednio " + (_dAgedN > 0 ? (_dAgeSum / _dAgedN).ToString("0.0", Inv) + " dob (" + _dAgedN + " armii z tej sesji)" : "-")
                         + " | od startu sesji rozwiazanych " + _tDisp + " | potkniecia " + _errors
                         + " | NotEnoughParty w dniu zalozenia wedlug wolajacego: BK wezwanie " + _dWho[0] + ", SCA " + _dWho[1] + ", gra " + _dWho[2] + ", inne " + _dWho[3] + ".");
                if (_sAge.Count > 0)
                {
                    var sw = new System.Text.StringBuilder();
                    foreach (var kv in _sAge)
                    {
                        if (sw.Length > 0) sw.Append("; ");
                        sw.Append(kv.Key).Append(' ').Append((int)kv.Value[0]).Append(" (w dniu zalozenia ").Append((int)kv.Value[1])
                          .Append(", srednio ").Append((kv.Value[2] / Math.Max(1.0, kv.Value[0])).ToString("0.0", Inv)).Append(" dob)");
                    }
                    Log.Info("Spojnosc armii (AC wiek): dzien " + (int)CampaignTime.Now.ToDays + " | od startu sesji rozwiazane wedlug przyczyny (wiek z daty zalozenia w grze): " + sw
                             + " | 'w dniu zalozenia' = armia skasowana zaraz po zalozeniu (np. StrategicCampaignAI: wodz < 22 zdrowych albo sila < 80, albo 0.75 doby po poprzedniej armii) - to nie rozpad.");
                }
            }
            catch (Exception e) { Stumble("Daily", e); }
            _dReasons.Clear(); _dDisp = 0; _dAgedN = 0; _dAgeSum = 0; for (int i = 0; i < _dWho.Length; i++) _dWho[i] = 0;
        }
    }
}
