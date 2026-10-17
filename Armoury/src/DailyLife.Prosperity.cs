using System;
using System.Collections.Generic;
using System.Globalization;
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
    /// z DailyLifeEstates, domyslnie WYLACZONY - wlaczenie po tescie 120 dob z progami NZC6-8/9). Dobrobyt warowni (miasto, zamek) idzie powoli za ZMIANA Dostatku jej
    /// REGIONU (warownia + jej wsie, bez wielkich rodow - D0, srednia 28 dob; ZC7 - z uchodzcami w mianowniku) od dnia wlaczenia:
    ///   KOTWICA zamrozona na warownie w dniu wlaczenia klucza (i w zapisie, arm_estates v4 pole "dobrobyt"): D0 regionu tej doby (po uchodzcach ZC7), suma czlonow C = 0;
    ///   P* = (dobrobyt - C) x (D regionu / D0 kotwicy) ^ 0.7 - dobrobyt "gry" (bez naszych czlonow) razy zmiana Dostatku regionu od kotwicy;
    ///   czlon doby = 0.01 x (P* - dobrobyt), najwyzej +-2 pkt na dobe; C += czlon.
    /// Start neutralny: w dniu wlaczenia czlon 0 w kazdej warowni (bez mediany krainy - recenzja 16.10: kotwica w dzisiejszej medianie krainy jednego miasta gonila
    /// sama siebie, a mediana D z miast i zamkow razem pchala miasta krainy w gore bez konca). Bez zmiany Dostatku czlon wraca nasz wklad do 0, wiec dobrobyt
    /// prowadzi gra; rownowaga: dobrobyt = dobrobyt gry x (D / D0) ^ 0.7 (stala czasu ok. 100 dob). Kotwica od nowa: przerwa > 7 dob bez czlonu (klucz wylaczony,
    /// pomiar stal) - C = 0; zmiana klucza ZC7 (uchodzcy w mianowniku albo nie) - tylko D0 od nowa (bez skoku pomiaru), C zostaje.
    /// Czlon liczony raz na dobe w bloku ZC (po Dostatku doby) z pieczatka doby (_pTermDay) i dopisany POSTFIKSEM Priority.Last na CalculateProsperityChange
    /// czynnego modelu dobrobytu (PatchTarget.OfActive - klasa deklarujaca; ROT/BK/RBL/gra - jedna metoda, raz, po warstwie BK, wiec mnozniki BK go nie skaluja)
    /// jako linia "Livelihood of the region" - ta sama liczba w dziennym ticku miasta (Town.DailyTick: Prosperity += ProsperityChange) i w dymku; czlon starszy niz
    /// wczorajszy nie dziala (doba przerwana wyjatkiem nie powtarza wczorajszego czlonu). Czlony doby w zapisie - po wczytaniu te same do pierwszej doby.
    /// Rozgrzewka sredniej dochodu, klucz wylaczony, region bez D - bez czlonu. Bez zlota, towaru i ludzi.
    /// </summary>
    internal static partial class DailyLife
    {
        private const double PRate = 0.01, PCapDay = 2.0, PExp = 0.7, PDriftGate = 0.05;
        private const int PGapDays = 7;
        private sealed class PInfo { public double P, Star, DReg, D0, Cum; public float Term; }
        private static Dictionary<Town, float> _pTerm = new Dictionary<Town, float>();
        private static int _pTermDay = int.MinValue;                          // doba kampanii (CampaignTime.Now) policzenia _pTerm
        private static Dictionary<string, float> _pTermSaved;                // czlony z zapisu (id warowni) - do pierwszej doby po wczytaniu
        private static int _pTermSavedDay = int.MinValue;
        private static int _pLastOn = -1;                                     // ostatnia doba pomiaru z czlonem (zapis): przerwa > 7 dob - kotwice od nowa
        private static Dictionary<Settlement, PInfo> _pInfo = new Dictionary<Settlement, PInfo>();
        private static readonly Dictionary<string, double[]> _pRun = new Dictionary<string, double[]>(StringComparer.Ordinal);   // kraina|rodzaj -> [znak, doba poczatku, |mediana| na poczatku]
        private static int _pN, _pCap, _pNoD, _pKr, _pStum, _pAnch, _pReAnch, _pDriftN, _pLands, _pRunMax;
        private static double _pSum, _pAbs, _pDrift, _pDriftExp, _pRunA0, _pRunA1;
        private static string _pDriftKey = "-", _pRunKey = "-", _pState = "jeszcze nie liczony";
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
            _pTerm = new Dictionary<Town, float>(); _pTermDay = int.MinValue; _pTermSaved = null; _pTermSavedDay = int.MinValue; _pLastOn = -1;
            _pInfo = new Dictionary<Settlement, PInfo>(); _pRun.Clear();
            _pN = _pCap = _pNoD = _pKr = _pStum = _pAnch = _pReAnch = _pDriftN = _pLands = _pRunMax = 0;
            _pSum = _pAbs = _pDrift = _pDriftExp = _pRunA0 = _pRunA1 = 0; _pDriftKey = _pRunKey = "-"; _pState = "jeszcze nie liczony"; _pCalls = 0;
        }

        /// <summary>Daily (poczatek doby ZC): czlony wczorajsze i z zapisu przestaja dzialac, zanim doba je policzy od nowa (doba przerwana - bez czlonu).</summary>
        private static void ClearProsperityTerms()
        {
            _pTerm = new Dictionary<Town, float>(); _pTermDay = int.MinValue; _pTermSaved = null; _pTermSavedDay = int.MinValue;
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

        /// <summary>Czlon jest z dzisiejszej albo wczorajszej doby kampanii (doba przerwana wyjatkiem i wczytanie z pustym dniem - bez czlonu).</summary>
        private static bool TermFresh(int stamp)
        {
            if (stamp == int.MinValue) return false;
            int now = (int)CampaignTime.Now.ToDays;
            return now >= stamp && now - stamp <= 1;
        }

        /// <summary>Postfiks Last na CalculateProsperityChange czynnego modelu: czlon "Livelihood of the region" z bloku doby (0 - bez linii). Nie rzuca.</summary>
        public static void ProsperityPostfix(Town __0, ref ExplainedNumber __result, MethodBase __originalMethod)
        {
            try
            {
                if (__0 == null || __originalMethod == null || !__originalMethod.Equals(_pActive) || !ProsOn) return;   // raz: tylko metoda czynnego modelu tej sesji
                float v = 0f;
                var m = _pTerm;
                if (m != null && m.Count > 0)
                {
                    if (!TermFresh(_pTermDay) || !m.TryGetValue(__0, out v)) return;
                }
                else
                {
                    var sv = _pTermSaved;                                                 // zapis wczytany przed pierwsza doba: czlony z zapisu
                    var st = __0.Settlement;
                    if (sv == null || st == null || st.StringId == null || !TermFresh(_pTermSavedDay) || !sv.TryGetValue(st.StringId, out v)) return;
                }
                if (v == 0f || float.IsNaN(v) || float.IsInfinity(v)) return;
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

        /// <summary>Czlon dobrobytu kazdej warowni na jutro (po Dostatku doby) z kotwicy warowni. Kraina = kultura z kalibracji x rodzaj warowni - tylko do linii
        /// (dryf krain, serie znaku mediany czlonu); wzor nie patrzy na inne warownie.</summary>
        private static void ProsperityDay(int day)
        {
            var term = new Dictionary<Town, float>();
            var info = new Dictionary<Settlement, PInfo>();
            int now = day + 1;                                                   // doba kampanii liczenia (Daily: day = Now - 1)
            _pN = _pCap = _pNoD = _pKr = _pAnch = _pReAnch = _pDriftN = _pLands = _pRunMax = 0;
            _pSum = _pAbs = _pDrift = _pDriftExp = _pRunA0 = _pRunA1 = 0; _pDriftKey = _pRunKey = "-";
            _pTermSaved = null;
            foreach (var r in _recs)                                              // czlon w zapisie tylko z tej doby (warownia bez D dzis - 0, nie wczorajszy)
            {
                EBook e0;
                if (r.Kind != KVillage && SafeId(r.Id) && _eb.TryGetValue(r.Id, out e0) && e0 != null) e0.PTerm = 0f;
            }
            if (!ProsOn) { _pTerm = term; _pTermDay = now; _pInfo = info; _pState = "wylaczony"; _pRun.Clear(); return; }
            if (_warm) { _pTerm = term; _pTermDay = now; _pInfo = info; _pState = "rozgrzewka sredniej dochodu - bez czlonu"; return; }
            // postfiks nie wisi na czynnym modelu: czlon nie trafilby do ticku miasta, a suma C rosla - bez czlonu i bez kotwic (przerwa > 7 dob - kotwice od nowa)
            if (_pActive == null) { _pTerm = term; _pTermDay = now; _pInfo = info; _pState = "postfiks NIE wpiety - bez czlonu"; return; }
            bool fresh = _pLastOn < 0 || day - _pLastOn > PGapDays;            // pierwsze wlaczenie albo przerwa: kotwice od nowa (C = 0)
            bool war = WarOn;
            var byK = new Dictionary<string, List<Rec>>(StringComparer.Ordinal);
            foreach (var r in _recs)
            {
                if (r.Kind == KVillage || r.St == null || r.St.Town == null || !SafeId(r.Id)) continue;
                string k = (r.Cul ?? "?") + (r.Kind == KTown ? "|miasta" : "|zamki");
                List<Rec> l;
                if (!byK.TryGetValue(k, out l)) { l = new List<Rec>(); byK[k] = l; }
                l.Add(r);
            }
            var tl = new List<double>(32);
            foreach (var kv in byK)
            {
                tl.Clear();
                double sC = 0, sPg = 0, sExp = 0;
                foreach (var r in kv.Value)
                {
                    double d = RegD0(r), pr = r.St.Town.Prosperity;
                    if (!Fin(d) || !(d > 0) || !Fin(pr)) { _pNoD++; continue; }
                    pr = Math.Max(0.0, pr);
                    EBook eb;
                    if (!_eb.TryGetValue(r.Id, out eb) || eb == null) { eb = new EBook(); _eb[r.Id] = eb; }
                    double d0r = Math.Round(d, 4);
                    if (fresh || !Fin(eb.PD0) || !(eb.PD0 > 0) || !(d0r > 0))
                    {
                        eb.PD0 = d0r > 0 ? d0r : d; eb.PCum = 0; eb.PWar = war; _pAnch++;
                    }
                    else if (eb.PWar != war) { eb.PD0 = d0r; eb.PWar = war; _pReAnch++; }   // ZC7 wlaczony / wylaczony: uchodzcy w mianowniku albo nie - bez skoku pomiaru
                    double pg = Math.Max(0.0, pr - eb.PCum);
                    double ratio = Math.Pow(d / eb.PD0, PExp);
                    double star = pg * ratio;
                    double v = PRate * (star - pr);
                    if (!Fin(v) || !Fin(star)) { _pNoD++; continue; }
                    if (v > PCapDay) { v = PCapDay; _pCap++; } else if (v < -PCapDay) { v = -PCapDay; _pCap++; }
                    v = Math.Round(v, 3);                                          // te same liczby co w zapisie
                    term[r.St.Town] = (float)v;
                    eb.PTerm = (float)v;
                    eb.PCum = Math.Round(eb.PCum + v, 3);                          // przyjety w ticku miasta (dzis albo jutro - jeden raz na dobe)
                    info[r.St] = new PInfo { P = pr, Star = star, DReg = d, D0 = eb.PD0, Cum = eb.PCum, Term = (float)v };
                    _pN++; _pSum += v; _pAbs += Math.Abs(v);
                    tl.Add(v);
                    sC += eb.PCum; sPg += pg; sExp += pg * (ratio - 1.0);
                }
                if (tl.Count == 0) continue;
                _pKr++; _pLands++;
                // dryf krainy: nasz wklad (suma C) wobec dobrobytu bez naszych czlonow; "z Dostatku" - rownowaga przy dzisiejszym Dostatku regionow
                if (sPg > 0)
                {
                    double dr = sC / sPg, ex = sExp / sPg;
                    if (Math.Abs(dr) > PDriftGate) _pDriftN++;
                    if (Math.Abs(dr) > Math.Abs(_pDrift)) { _pDrift = dr; _pDriftExp = ex; _pDriftKey = kv.Key; }
                }
                // seria znaku mediany czlonu krainy (pamiec sesji): brak zbieznosci = dlugo ten sam znak bez malejacej |mediany|
                double med = Median(tl);
                int sg = Math.Abs(med) < 0.05 ? 0 : Math.Sign(med);
                double[] run;
                if (!_pRun.TryGetValue(kv.Key, out run)) { run = new double[3]; _pRun[kv.Key] = run; }
                if (sg == 0 || (int)run[0] != sg) { run[0] = sg; run[1] = day; run[2] = Math.Abs(med); }
                if (sg != 0)
                {
                    int len = day - (int)run[1] + 1;
                    if (len > _pRunMax) { _pRunMax = len; _pRunKey = kv.Key; _pRunA0 = run[2]; _pRunA1 = Math.Abs(med); }
                }
            }
            _pTerm = term; _pTermDay = now; _pInfo = info; _pLastOn = day;
            _pState = "liczony (dzien " + day + (fresh ? ", kotwice od nowa" : "") + ")";
        }

        /// <summary>Fragment linii ZC6: czlon dobrobytu doby, kotwice, dryf krain, serie znaku, wywolania i potkniecia postfiksu od ostatniej linii (liczniki od nowa).</summary>
        private static string ProsperityText()
        {
            var sb = new StringBuilder(520);
            sb.Append("dobrobyt (").Append(_pState).Append("; ").Append(_pWhere.Replace('|', '/')).Append("): warowni z czlonem ").Append(_pN).Append(" w ").Append(_pKr)
              .Append(" krainach (bez D regionu ").Append(_pNoD).Append("), kotwice nowe dzis ").Append(_pAnch).Append(" (ponowne przy zmianie ZC7 ").Append(_pReAnch)
              .Append("), czlon srednio ").Append(F3(_pN > 0 ? _pSum / _pN : double.NaN)).Append("/d, srednio |czlon| ")
              .Append(F3(_pN > 0 ? _pAbs / _pN : double.NaN)).Append("/d, na suficie 2 pkt ").Append(_pCap).Append(" (").Append(F1(_pN > 0 ? 100.0 * _pCap / _pN : double.NaN)).Append("% warowni)")
              .Append("; dryf krain (suma czlonow od kotwicy / dobrobyt bez czlonow, wszystkie krainy i rodzaje): najwiekszy ").Append(F2(100 * _pDrift)).Append("% (")
              .Append(_pDriftKey.Replace('|', '/')).Append("; z Dostatku krainy ").Append(F2(100 * _pDriftExp)).Append("%), krain ponad 5%: ").Append(_pDriftN).Append(" z ").Append(_pLands)
              .Append("; seria mediany czlonu krainy tego samego znaku najdluzsza ").Append(_pRunMax).Append(" dob (").Append(_pRunKey.Replace('|', '/')).Append("; |mediana| na poczatku ")
              .Append(F3(_pRunA0)).Append(" -> dzis ").Append(F3(_pRunA1)).Append(')')
              .Append("; wywolan postfiksu ").Append(_pCalls).Append(", potkniec postfiksu ").Append(_pStum);
            _pCalls = 0; _pStum = 0;
            return sb.ToString();
        }

        // ------------------------------------------------------------ zapis (arm_estates v4: pole "dobrobyt" warowni i naglowek)
        /// <summary>Pole "dobrobyt" warowni: D0 kotwicy, suma czlonow C, czlon ostatniej doby, kotwica z ZC7 (1/0). Puste bez kotwicy.</summary>
        private static void AppendPros(StringBuilder sb, EBook b)
        {
            if (!Fin(b.PD0) || !(b.PD0 > 0)) return;
            sb.Append(b.PD0.ToString("0.####", Inv)).Append(',').Append(b.PCum.ToString("0.###", Inv)).Append(',')
              .Append(((double)b.PTerm).ToString("0.###", Inv)).Append(',').Append(b.PWar ? '1' : '0');
        }

        private static bool ParsePros(string s, EBook b, string id)
        {
            var q = s.Split(',');
            if (q.Length != 4) return false;
            double d0, c, t;
            if (!double.TryParse(q[0], NumberStyles.Float, Inv, out d0) || !(d0 > 0) || !Fin(d0) || !double.TryParse(q[1], NumberStyles.Float, Inv, out c) || !Fin(c)
                || !double.TryParse(q[2], NumberStyles.Float, Inv, out t) || !Fin(t) || Math.Abs(t) > PCapDay + 1e-6) return false;
            b.PD0 = d0; b.PCum = c; b.PTerm = (float)t; b.PWar = q[3] == "1";
            if (b.PTerm != 0f && id != null)
            {
                if (_pTermSaved == null) _pTermSaved = new Dictionary<string, float>(StringComparer.Ordinal);
                _pTermSaved[id] = b.PTerm;
            }
            return true;
        }

        /// <summary>Zdanie widoku (okno Estates and livelihood): czlon dobrobytu regionu warowni (wies - jej warowni). null - klucz off albo bez czlonu.</summary>
        private static string ProsperityViewText(Rec r)
        {
            try
            {
                if (r == null || !ProsOn) return null;
                var reg = r.Kind == KVillage ? r.Region : r;
                PInfo pi;
                if (reg == null || reg.St == null || !_pInfo.TryGetValue(reg.St, out pi) || pi == null) return null;
                double rel = pi.D0 > 0 ? pi.DReg / pi.D0 : double.NaN;
                string mv = Math.Abs(pi.Term) < 0.05f ? "steady" : (pi.Term > 0 ? "+" : "") + pi.Term.ToString("0.0", Inv) + " a day";
                return "Prosperity of " + reg.Name + " follows the livelihood of its region: " + mv + " toward " + Num(pi.Star) + " (now " + Num(pi.P) + "). The region lives "
                       + (Fin(rel) ? rel.ToString("0.0#", Inv) : "-") + " times as well as when its prosperity was first tied to it; a richer region pulls prosperity up, a poorer one down, by at most 2 a day.";
            }
            catch (Exception e) { ViewStumble("ProsperityViewText", e); return null; }
        }

        /// <summary>Krotka forma do tytulu wiersza okna: "Prosperity: +0.4 a day" albo "steady".</summary>
        private static string ProsperityShort(Rec r)
        {
            try
            {
                if (r == null || !ProsOn) return null;
                var reg = r.Kind == KVillage ? r.Region : r;
                PInfo pi;
                if (reg == null || reg.St == null || !_pInfo.TryGetValue(reg.St, out pi) || pi == null) return null;
                return Math.Abs(pi.Term) < 0.05f ? "steady" : (pi.Term > 0 ? "+" : "") + pi.Term.ToString("0.0", Inv) + " a day";
            }
            catch (Exception e) { ViewStumble("ProsperityShort", e); return null; }
        }
    }
}
