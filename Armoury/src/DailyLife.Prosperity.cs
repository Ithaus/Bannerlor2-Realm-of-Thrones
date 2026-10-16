using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// ZC6 - POWIAZANIE Z DOBROBYTEM GRY (projekt docs/PROJEKT-ZYCIE-CODZIENNE-2026-10-16.md rozdz. 9 "Skutki Dostatku", rozdz. 15 ryzyko 10; klucz DailyLifeProsperityLink
    /// z DailyLifeEstates). Dobrobyt warowni (miasto, zamek) idzie powoli za Dostatkiem jej REGIONU (warownia + jej wsie, bez wielkich rodow - D0, srednia 28 dob):
    ///   P* = mediana dobrobytu krainy (ta sama kultura z kalibracji, ten sam rodzaj warowni - dzisiaj) x (D regionu / mediana D regionow krainy) ^ 0.7;
    ///   czlon doby = 0.01 x (P* - dobrobyt), najwyzej +-2 pkt na dobe.
    /// Kotwica w dzisiejszej medianie krainy: region na medianie D stoi przy medianie dobrobytu, bogatszy ciagnie w gore, biedniejszy w dol - srodek krainy zostaje
    /// (start neutralny dla AI, bez skoku). Czlon liczony raz na dobe w bloku ZC (po Dostatku doby) na jutro, dopisany POSTFIKSEM Priority.Last na metodzie
    /// CalculateProsperityChange czynnego modelu dobrobytu (PatchTarget.OfActive - klasa deklarujaca; ROT/BK/RBL/gra - jedna metoda, raz, po warstwie BK, wiec
    /// mnozniki BK go nie skaluja) jako linia "Livelihood of the region" - ta sama liczba w dziennym ticku miasta (Town.DailyTick: Prosperity += ProsperityChange) i w dymku.
    /// Rozgrzewka sredniej dochodu (polowa osad z mniej niz 7 dobami), klucz wylaczony, region bez D - bez czlonu. Bez zlota, towaru i ludzi.
    /// </summary>
    internal static partial class DailyLife
    {
        private sealed class PInfo { public double P, Star, DReg, DMed, MedP; public float Term; }
        private static Dictionary<Town, float> _pTerm = new Dictionary<Town, float>();
        private static Dictionary<Settlement, PInfo> _pInfo = new Dictionary<Settlement, PInfo>();
        private static Dictionary<string, double> _pMed0;                    // kraina|rodzaj -> mediana dobrobytu w pierwszej dobie sesji z czlonem
        private static readonly Dictionary<Town, double> _pCum = new Dictionary<Town, double>();   // suma czlonow od wczytania (wplyw na mediany krain)
        private static int _pN, _pCap, _pNoD, _pKr, _pStum; private static double _pSum, _pAbs, _pDev, _pOurs; private static string _pDevKey = "-", _pOursKey = "-", _pState = "jeszcze nie liczony";
        private static long _pCalls;
        private static TextObject _pTxt;
        // proces: wpiecie postfiksu (raz na metode)
        private static Harmony _h;
        private static readonly HashSet<MethodBase> _pPatched = new HashSet<MethodBase>();
        private static MethodBase _pActive;                                   // metoda czynnego modelu tej sesji - postfiks na innej (z dawnej kampanii) nic nie dopisuje
        private static string _pWhere = "nie wpiety";

        internal static bool ProsOn { get { var s = Settings.Current; return EstatesOn && s.DailyLifeProsperityLink; } }
        private static string ProsperityWhere { get { return _pWhere; } }

        private static void ResetProsperity()
        {
            _pTerm = new Dictionary<Town, float>(); _pInfo = new Dictionary<Settlement, PInfo>(); _pMed0 = null; _pCum.Clear();
            _pN = _pCap = _pNoD = _pKr = _pStum = 0; _pSum = _pAbs = _pDev = _pOurs = 0; _pDevKey = _pOursKey = "-"; _pState = "jeszcze nie liczony"; _pCalls = 0;
        }

        /// <summary>OnSessionLaunched (MobilitySessionStart): postfiks na CalculateProsperityChange czynnego modelu - raz na metode w procesie; sprawdzenie, ze wisi
        /// (Harmony.Patch moze wrocic bez latki - finalizer BKROTPatch polyka wyjatek, PatchTarget).</summary>
        private static void WireProsperity()
        {
            var c = Campaign.Current;
            var model = c != null && c.Models != null ? c.Models.SettlementProsperityModel : null;
            _pActive = null;
            if (model == null) { _pWhere = "BRAK modelu dobrobytu"; return; }
            var mi = PatchTarget.OfActive(model, "CalculateProsperityChange", new[] { typeof(Town), typeof(bool) }, "ZC6 dobrobyt (DailyLife.ProsperityPostfix)");
            if (mi == null) { _pWhere = "BRAK metody CalculateProsperityChange (model czynny " + model.GetType().FullName + ")"; return; }
            string where = mi.DeclaringType != null ? mi.DeclaringType.FullName : "?";
            if (_pPatched.Contains(mi)) { _pActive = mi; _pWhere = "postfiks Last w " + where + ".CalculateProsperityChange (model czynny " + model.GetType().FullName + "; wpiety wczesniej w tym procesie)"; return; }
            (_h ?? new Harmony("com.jeff.armoury")).Patch(mi, postfix: new HarmonyMethod(typeof(DailyLife), nameof(ProsperityPostfix)) { priority = Priority.Last });
            bool ok = false;
            var pi = Harmony.GetPatchInfo(mi);
            if (pi != null)
                foreach (var p in pi.Postfixes)
                    if (p.PatchMethod != null && p.PatchMethod.Name == nameof(ProsperityPostfix) && p.PatchMethod.DeclaringType == typeof(DailyLife)) { ok = true; break; }
            if (ok) { _pPatched.Add(mi); _pActive = mi; }
            _pWhere = (ok ? "postfiks Last w " : "NIE WPIETY (Harmony bez latki) w ") + where + ".CalculateProsperityChange (model czynny " + model.GetType().FullName + ")";
        }

        /// <summary>Postfiks Last na CalculateProsperityChange czynnego modelu: czlon "Livelihood of the region" z bloku doby (0 - bez linii). Nie rzuca.</summary>
        public static void ProsperityPostfix(Town __0, ref ExplainedNumber __result, MethodBase __originalMethod)
        {
            try
            {
                if (__0 == null || __originalMethod == null || !__originalMethod.Equals(_pActive)) return;   // raz: tylko metoda czynnego modelu tej sesji
                var m = _pTerm;
                float v;
                if (m == null || m.Count == 0 || !m.TryGetValue(__0, out v) || v == 0f || float.IsNaN(v) || float.IsInfinity(v) || !ProsOn) return;
                if (_pTxt == null) _pTxt = new TextObject("{=!}Livelihood of the region");
                __result.Add(v, _pTxt);
                _pCalls++;
            }
            catch (Exception e) { _pStum++; ViewStumble("ProsperityPostfix", e); }
        }

        /// <summary>D0 regionu warowni (warownia + wsie, bez wielkich rodow) tej doby albo NaN.</summary>
        private static double RegD0(Rec r)
        {
            double[] a;
            return r != null && _reg.TryGetValue(r, out a) && a[3] > 0 ? a[2] / a[3] : double.NaN;
        }

        /// <summary>Czlon dobrobytu kazdej warowni na jutro (po Dostatku doby). Kraina = kultura z kalibracji; mediany dobrobytu osobno dla miast i zamkow.</summary>
        private static void ProsperityDay(int day)
        {
            var term = new Dictionary<Town, float>();
            var info = new Dictionary<Settlement, PInfo>();
            _pN = _pCap = _pNoD = _pKr = 0; _pSum = _pAbs = _pDev = _pOurs = 0; _pDevKey = _pOursKey = "-";
            if (!ProsOn) { _pTerm = term; _pInfo = info; _pState = "wylaczony"; return; }
            if (_warm) { _pTerm = term; _pInfo = info; _pState = "rozgrzewka sredniej dochodu - bez czlonu"; return; }
            var byK = new Dictionary<string, List<Rec>>(StringComparer.Ordinal);
            foreach (var r in _recs)
            {
                if (r.Kind == KVillage || r.St == null || r.St.Town == null) continue;
                string k = r.Cul ?? "?";
                List<Rec> l;
                if (!byK.TryGetValue(k, out l)) { l = new List<Rec>(); byK[k] = l; }
                l.Add(r);
            }
            bool first = _pMed0 == null;
            if (first) _pMed0 = new Dictionary<string, double>(StringComparer.Ordinal);
            var dl = new List<double>(32); var pt = new List<double>(32); var pc = new List<double>(32); var qt = new List<double>(32); var qc = new List<double>(32);
            foreach (var kv in byK)
            {
                dl.Clear(); pt.Clear(); pc.Clear(); qt.Clear(); qc.Clear();
                foreach (var r in kv.Value)
                {
                    double pr = r.St.Town.Prosperity;
                    if (Fin(pr))
                    {
                        double cum; _pCum.TryGetValue(r.St.Town, out cum);
                        (r.Kind == KTown ? pt : pc).Add(Math.Max(0.0, pr));
                        (r.Kind == KTown ? qt : qc).Add(Math.Max(0.0, pr - cum));   // dobrobyt bez naszych czlonow od wczytania
                    }
                    double d = RegD0(r);
                    if (Fin(d) && d > 0) dl.Add(d);
                }
                double medT = pt.Count > 0 ? Median(pt) : double.NaN, medC = pc.Count > 0 ? Median(pc) : double.NaN;
                for (int kind = 0; kind < 2; kind++)
                {
                    string key = kv.Key + (kind == 0 ? "|miasta" : "|zamki");
                    double med = kind == 0 ? medT : medC; int cnt = kind == 0 ? pt.Count : pc.Count;
                    if (!Fin(med) || cnt < 3) continue;
                    double m0;
                    if (first) _pMed0[key] = med;
                    else if (_pMed0.TryGetValue(key, out m0) && m0 > 0)
                    {
                        double dev = med / m0 - 1.0;
                        if (Math.Abs(dev) > Math.Abs(_pDev)) { _pDev = dev; _pDevKey = key; }
                    }
                    double mq = Median(kind == 0 ? qt : qc);
                    if (Fin(mq) && mq > 0)
                    {
                        double ours = med / mq - 1.0;                               // wplyw naszych czlonow na mediane krainy (projekt: +-5%)
                        if (Math.Abs(ours) > Math.Abs(_pOurs)) { _pOurs = ours; _pOursKey = key; }
                    }
                }
                if (dl.Count == 0) { _pNoD += kv.Value.Count; continue; }
                _pKr++;
                double medD = Median(dl);
                foreach (var r in kv.Value)
                {
                    double d = RegD0(r), medP = r.Kind == KTown ? medT : medC;
                    if (!Fin(d) || !(d > 0) || !Fin(medP) || !(medD > 0)) { _pNoD++; continue; }
                    double pr = r.St.Town.Prosperity;
                    if (!Fin(pr)) { _pNoD++; continue; }
                    double star = medP * Math.Pow(d / medD, 0.7);
                    double v = 0.01 * (star - pr);
                    if (!Fin(v)) { _pNoD++; continue; }
                    if (v > 2.0) { v = 2.0; _pCap++; } else if (v < -2.0) { v = -2.0; _pCap++; }
                    term[r.St.Town] = (float)v;
                    { double c0; _pCum.TryGetValue(r.St.Town, out c0); _pCum[r.St.Town] = c0 + (float)v; }   // przyjety jutro w ticku miasta
                    info[r.St] = new PInfo { P = pr, Star = star, DReg = d, DMed = medD, MedP = medP, Term = (float)v };
                    _pN++; _pSum += v; _pAbs += Math.Abs(v);
                }
            }
            _pTerm = term; _pInfo = info; _pState = "liczony (dzien " + day + ")";
        }

        /// <summary>Fragment linii ZC6: czlon dobrobytu doby, mediany krain wobec pierwszej doby sesji, wywolania i potkniecia postfiksu od ostatniej linii (liczniki od nowa).</summary>
        private static string ProsperityText()
        {
            var sb = new StringBuilder(400);
            sb.Append("dobrobyt (").Append(_pState).Append("; ").Append(_pWhere.Replace('|', '/')).Append("): warowni z czlonem ").Append(_pN).Append(" w ").Append(_pKr)
              .Append(" krainach (bez D regionu ").Append(_pNoD).Append("), czlon srednio ").Append(F3(_pN > 0 ? _pSum / _pN : double.NaN)).Append("/d, srednio |czlon| ")
              .Append(F3(_pN > 0 ? _pAbs / _pN : double.NaN)).Append("/d, na suficie 2 pkt ").Append(_pCap)
              .Append("; mediany dobrobytu krain (>= 3 warownie): wplyw czlonow od wczytania najwiekszy ").Append(F2(100 * _pOurs)).Append("% (").Append(_pOursKey)
              .Append("), zmiana wobec pierwszej doby sesji (z gra) najwieksza ").Append(F2(100 * _pDev)).Append("% (").Append(_pDevKey)
              .Append("); wywolan postfiksu ").Append(_pCalls).Append(", potkniec postfiksu ").Append(_pStum);
            _pCalls = 0; _pStum = 0;
            return sb.ToString();
        }

        /// <summary>Zdanie widoku (People of this land): czlon dobrobytu regionu warowni (wies - jej warowni). null - klucz off albo bez czlonu.</summary>
        private static string ProsperityViewText(Rec r)
        {
            try
            {
                if (r == null || !ProsOn) return null;
                var reg = r.Kind == KVillage ? r.Region : r;
                PInfo pi;
                if (reg == null || reg.St == null || !_pInfo.TryGetValue(reg.St, out pi) || pi == null) return null;
                double rel = pi.DMed > 0 ? pi.DReg / pi.DMed : double.NaN;
                string mv = Math.Abs(pi.Term) < 0.05f ? "steady" : (pi.Term > 0 ? "+" : "") + pi.Term.ToString("0.0", Inv) + " a day";
                return "Prosperity of " + reg.Name + " follows its region's livelihood: " + mv + " toward " + Num(pi.Star) + " (now " + Num(pi.P) + "; the middle "
                       + (reg.Kind == KTown ? "town" : "castle") + " of this land has " + Num(pi.MedP) + ", and this region lives " + (Fin(rel) ? rel.ToString("0.0#", Inv) : "-")
                       + " times as well as the land's middle region).";
            }
            catch (Exception e) { ViewStumble("ProsperityViewText", e); return null; }
        }
    }
}
