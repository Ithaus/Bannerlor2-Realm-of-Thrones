using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// GL-4 / P4 (projekt docs/gospodarka-od-ludzi/PROJEKT-GOSPODARKA-OD-LUDZI-2026-10-13.md rozdz. 3.3 wiersze "tempo werbunku" i "przyrost milicji",
    /// "Udzial zbrojnych ludu", 3.5, 8 P4; decyzja Jeffa pyt. 2 "lup i milicja bez wyjatku") - POBOR I MILICJA OD LUDZI.
    ///
    /// Czynnik osady c = min(3, max(0.1, s x a / N)): s = skala ludzi z pamieci doby GL-0 (wies u_v - PeopleScale.TryVillage, miasto m_t - TryMarket,
    /// zamek skala podzamcza - TryCastleWard), a = udzial zbrojnych ludu krainy = prog wolnych rak krainy / FreeHandsShare (FreeHands.ArmedShare: zwykla
    /// kraina 1, Zelazne Wyspy 2 przy progu 40%, Wolni Ludzie 5 - tnie gorna granica 3, Dothrakowie 3 przy progu 60% - DECYZJA pyt. 2),
    /// N = norma rodzaju osady (wsie / miasta / zamki osobno), dobrana co dobe tak, ze srednia wazona czynnika = 1.000 (bisekcja; granice 0.1-3 licza sie
    /// w sredniej). Wagi: pobor - liczba notabli osady (losowania pustych miejsc idzie od notabli); milicja - przyrost modelu gry z :104-120 (2 + dobrobyt /
    /// 1000 w warowni, 0.5 + hearth / 400 we wsi). Suma swiata ochotnikow i milicji zostaje wiec przy dzisiejszej (projekt: prog 0.9-1.1), zmienia sie podzial.
    ///  - LevyByPeople: Levy.ProbabilityPostfix (szansa PUSTEGO miejsca u notabla - BK GetDailyVolunteerProductionProbability, po hamulcu wolnych rak
    ///    i po checi do sluzby) x c, wynik najwyzej 1. Stojacy ochotnik (awans) bez zmian.
    ///  - MilitiaByPeople: (1) postfiks DefaultSettlementMilitiaModel.CalculateMilitiaChangeInternal - skladnik przyrostu gry (podstawa 2 / 0.5 + dobrobyt
    ///    / 1000 albo hearth / 400) x c: dopisane (c - 1) x przyrost; ubytek 2.5% stanu bez zmian. BK, ROT i NavalDLC wolaja model gry pod soba (BK: base,
    ///    ROT: _previousModel, NavalDLC: BaseModel), wiec mnoznik BK "Militia Growth Multiplier" i czynnik polityki NavalDLC dzialaja i na dopisek;
    ///    (2) postfiks BKMilitiaModel.GetMilitiaLimit x c - DECYZJA (pelne uprawnienia): limit BK (1% ludnosci BK + 20 / 100 / 200) jest plaski na osade,
    ///    a milicja swiata stoi dzis przy nim (bieg 43, doba 108900: milicja regionow 1.11 x suma limitow BK, mediana regionu 1.04); bez skali limitu
    ///    czlon "Over supported limit" zjadalby przyrost ludnych osad, a puste i tak by tracily - suma swiata spadlaby, zamiast przejsc za ludzmi.
    /// Poza regula (c = 1, poza norma): osady krainy Nocnej Strazy (ludzie Strazy ida przez WatchLaw z calego Westeros), osada bez skali GL-0 (usluga
    /// nie gotowa, wies bez przelicznika, potkniecie). Kraina z hamulcem wolnych rak dalej nie dostaje ani ochotnika, ani nowej milicji (Levy, ArmsFromPeople).
    /// Obieg: zloto nie rusza; ludzie ida z DOM do milicji i do ochotnikow ta sama droga co dzis (ArmsFromPeople, PeopleBook.TakeLevy) - wiecej w ludnych,
    /// mniej w pustych krainach. Odczyt z postfiksow (watek glowny i ewentualne watki AI): jedna referencja pamieci doby, slownik niezmienny po publikacji.
    /// Klucze wylaczone: postfiksy nic nie dopisuja (stan 97dd474); linia doby raz na sesje "WYLACZONE".
    /// </summary>
    internal static class PeopleLevy
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        internal const float FactorMin = 0.1f, FactorMax = 3f;
        private static readonly TextObject Text = new TextObject("{=!}People of this land");
        private const string WatchRealm = "nightswatch";

        internal static bool LevyOn { get { var s = Settings.Current; return s != null && s.LevyByPeople; } }
        internal static bool MilitiaOn { get { var s = Settings.Current; return s != null && s.MilitiaByPeople; } }
        /// <summary>FreeHands.FOf: Dothrakowie lud zbrojny (prog wolnych rak 60%, udzial zbrojnych 3) - przy ktoryms z kluczy P4 (DECYZJA pyt. 2).</summary>
        internal static bool DothrakiArmed { get { var s = Settings.Current; return s != null && (s.LevyByPeople || s.MilitiaByPeople); } }

        private sealed class Cell { public float L = 1f, M = 1f; public char Kind; public string Kingdom; }
        private sealed class Memory { public int Day; public Dictionary<Settlement, Cell> Map; }
        private static volatile Memory _mem;

        // ------------------------------------------------------------ liczniki (postfiksy: watek glowny gry; Interlocked na wszelki wypadek)
        private static int _lvN, _lvCut, _milN, _limN, _readStumbles, _stumbles;
        private static double _lvSum, _milAdd, _milSub;
        private static readonly object _kLock = new object();
        private static readonly Dictionary<string, double> _expect = new Dictionary<string, double>(StringComparer.Ordinal);   // krolestwo -> suma szans pustych miejsc (oczekiwani ochotnicy)
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static bool _offLogged, _weekDone;
        private static string _wired = "nie wpiete", _report = "";

        internal static void Reset()
        {
            _mem = null; _lvN = _lvCut = _milN = _limN = _readStumbles = _stumbles = 0; _lvSum = _milAdd = _milSub = 0.0;
            lock (_kLock) _expect.Clear();
            _err.Clear(); _offLogged = false; _weekDone = false; _report = "";
        }

        private static int Today() { return (int)CampaignTime.Now.ToDays; }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;                                                            // liczymy, niczego nie gasimy
            try { if (_err.Add(where)) { if (e != null) Log.Error("PeopleLevy." + where, e); else Log.Info("PeopleLevy: potkniecie - " + where + "."); } } catch { }
        }

        private static void ReadStumble(Exception e)
        {
            if (Interlocked.Increment(ref _readStumbles) == 1) { try { Log.Error("PeopleLevy.Odczyt", e); } catch { } }
        }

        // ============================================================ odczyt (postfiksy)

        private static bool TryCell(Settlement s, out Cell c)
        {
            c = null;
            var m = _mem;
            return m != null && s != null && m.Map.TryGetValue(s, out c) && c != null;
        }

        /// <summary>Levy.ProbabilityPostfix (puste miejsce u notabla, po hamulcu i checi): szansa x czynnik poboru osady, wynik najwyzej 1. Klucz wylaczony,
        /// osada poza regula - bez zmian. Nie rzuca.</summary>
        internal static void LevyStep(Settlement s, ref float p)
        {
            if (!LevyOn) return;                                    // klucz wylaczony: nic (stan 97dd474)
            try
            {
                Cell c;
                float f = 1f;
                if (TryCell(s, out c)) f = c.L;
                if (f != 1f)
                {
                    float q = p * f;
                    if (q > 1f) { q = 1f; Interlocked.Increment(ref _lvCut); }
                    if (!(q >= 0f)) q = p;                          // NaN - zostaje szansa gry
                    p = q;
                }
                Interlocked.Increment(ref _lvN);
                lock (_kLock)
                {
                    _lvSum += f;
                    string k = c != null ? c.Kingdom : KingdomOf(s);
                    double e; _expect.TryGetValue(k, out e); _expect[k] = e + p;
                }
            }
            catch (Exception e) { ReadStumble(e); }
        }

        /// <summary>Postfiks DefaultSettlementMilitiaModel.CalculateMilitiaChangeInternal (i CalculateMilitiaChange, gdy brak metody wewnetrznej): przyrost
        /// gry (:104-120) x czynnik milicji - dopisek (c - 1) x przyrost; ubytek 2.5% stanu i reszta skladnikow bez zmian.</summary>
        public static void MilitiaPostfix(Settlement settlement, ref ExplainedNumber __result)
        {
            if (!MilitiaOn || settlement == null) return;
            try
            {
                if (settlement.IsVillage && (settlement.Village == null || settlement.Village.VillageState != Village.VillageStates.Normal)) return;   // gra: 0 (bez przyrostu)
                Cell c;
                if (!TryCell(settlement, out c) || c.M == 1f) return;
                float g = Growth(settlement);
                if (!(g > 0f)) return;
                float add = (c.M - 1f) * g;
                if (float.IsNaN(add) || float.IsInfinity(add)) return;
                __result.Add(add, Text);
                Interlocked.Increment(ref _milN);
                lock (_kLock) { if (add > 0f) _milAdd += add; else _milSub -= add; }
            }
            catch (Exception e) { ReadStumble(e); }
        }

        /// <summary>Postfiks BKMilitiaModel.GetMilitiaLimit: limit milicji BK x czynnik milicji (te same ludzie, ten sam czynnik).</summary>
        public static void LimitPostfix(Settlement settlement, ref ExplainedNumber __result)
        {
            if (!MilitiaOn || settlement == null) return;
            try
            {
                Cell c;
                if (!TryCell(settlement, out c) || c.M == 1f) return;
                __result.AddFactor(c.M - 1f, Text);
                Interlocked.Increment(ref _limN);
            }
            catch (Exception e) { ReadStumble(e); }
        }

        /// <summary>Skladnik przyrostu milicji gry (DefaultSettlementMilitiaModel :104-120): warownia 2 + dobrobyt / 1000, wies 0.5 + hearth / 400.</summary>
        private static float Growth(Settlement s)
        {
            if (s.IsFortification && s.Town != null) return 2f + s.Town.Prosperity / 1000f;
            if (s.IsVillage && s.Village != null) return 0.5f + s.Village.Hearth / 400f;
            return 0f;
        }

        private static string KingdomOf(Settlement s)
        {
            try { var f = s != null ? s.MapFaction : null; return f != null && f.Name != null ? f.Name.ToString() : "-"; } catch { return "-"; }
        }

        // ============================================================ doba i start sesji (watek glowny)

        /// <summary>Z OnSessionLaunched po PeopleScale.SessionStart: pamiec czynnikow od pierwszej godziny sesji (po wczytaniu nie czeka na dobe).</summary>
        internal static void SessionStart() { Build(false); }

        /// <summary>Z OnDailyTickBody po PeopleScale.Daily (pamiec skal tej doby): czynniki na jutro, linia doby, co 7 dob tabela krolestw.</summary>
        internal static void Daily()
        {
            Build(true);
        }

        private sealed class Row { public Settlement S; public char Kind; public float X, WL, WM; public string Kingdom; }

        /// <summary>Norma rodzaju: N takie, ze srednia wazona min(3, max(0.1, x / N)) = 1 (bisekcja w skali log; funkcja nierosnaca w N).</summary>
        private static double Norm(List<Row> rows, bool levy)
        {
            double sw = 0;
            foreach (var r in rows) sw += levy ? r.WL : r.WM;
            if (!(sw > 0)) return 1.0;
            double lo = Math.Log(1e-4), hi = Math.Log(1e4);
            for (int i = 0; i < 80; i++)
            {
                double mid = 0.5 * (lo + hi), n = Math.Exp(mid), acc = 0;
                foreach (var r in rows) acc += (levy ? r.WL : r.WM) * Clamp(r.X / n);
                if (acc / sw > 1.0) lo = mid; else hi = mid;
            }
            return Math.Exp(0.5 * (lo + hi));
        }

        private static float Clamp(double v) { return (float)Math.Max(FactorMin, Math.Min(FactorMax, v)); }

        private static void Build(bool daily)
        {
            bool on = LevyOn || MilitiaOn;
            if (!on)
            {
                if (_mem != null) _mem = null;
                if (daily) Flush(false);
                return;
            }
            try
            {
                if (!PeopleScale.Ready)
                {
                    _mem = null;
                    if (daily) Flush(true);
                    return;
                }
                var rows = new List<Row>(900);
                int watch = 0, noScale = 0;
                foreach (var s in Settlement.All)
                {
                    if (s == null) continue;
                    try
                    {
                        char kind = s.IsVillage ? 'w' : (s.IsTown ? 'm' : (s.IsCastle ? 'z' : ' '));
                        if (kind == ' ') continue;
                        var region = s.IsVillage ? (s.Village != null ? s.Village.Bound : null) : s;
                        string realm = region != null ? FreeHands.RealmOf(region) : PeopleBook.CultureAtCalibration(s);
                        if (realm == WatchRealm) { watch++; continue; }           // Straz poza regula (WatchLaw)
                        float sc;
                        bool ok = kind == 'w' ? PeopleScale.TryVillage(s.Village, out sc) : (kind == 'm' ? PeopleScale.TryMarket(s.Town, out sc) : PeopleScale.TryCastleWard(s, out sc));
                        if (!ok) { noScale++; continue; }
                        double a = FreeHands.ArmedShare(realm);
                        double x = sc * a;
                        if (double.IsNaN(x) || double.IsInfinity(x) || x < 0) { Stumble("Skala(" + s.StringId + ")", null); continue; }
                        int nb = 0;
                        try { var nl = s.Notables; nb = nl != null ? nl.Count : 0; } catch { nb = 0; }
                        rows.Add(new Row { S = s, Kind = kind, X = (float)x, WL = nb, WM = Math.Max(0f, Growth(s)), Kingdom = KingdomOf(s) });
                    }
                    catch (Exception e) { Stumble("Spis(osada)", e); }
                }
                var map = new Dictionary<Settlement, Cell>(rows.Count);
                var parts = new StringBuilder();
                int lo = 0, hi = 0;
                foreach (char k in new[] { 'w', 'm', 'z' })
                {
                    var kr = rows.FindAll(r => r.Kind == k);
                    if (kr.Count == 0) continue;
                    double nL = Norm(kr, true), nM = Norm(kr, false);
                    double sl = 0, swl = 0, sm = 0, swm = 0;
                    var fl = new List<float>(kr.Count);
                    foreach (var r in kr)
                    {
                        float cl = LevyOn ? Clamp(r.X / nL) : 1f, cm = MilitiaOn ? Clamp(r.X / nM) : 1f;
                        map[r.S] = new Cell { L = cl, M = cm, Kind = k, Kingdom = r.Kingdom };
                        sl += r.WL * cl; swl += r.WL; sm += r.WM * cm; swm += r.WM;
                        float cx = Clamp(r.X / nL);
                        if (cx <= FactorMin + 1e-6f) lo++; else if (cx >= FactorMax - 1e-6f) hi++;
                        fl.Add(cx);
                    }
                    fl.Sort();
                    parts.Append(k == 'w' ? "wsie " : (k == 'm' ? "miasta " : "zamki ")).Append(kr.Count)
                         .Append(" (norma poboru ").Append(nL.ToString("0.000", Inv)).Append(", milicji ").Append(nM.ToString("0.000", Inv))
                         .Append("; srednia wazona czynnika: pobor ").Append((swl > 0 ? sl / swl : 1).ToString("0.000", Inv)).Append(", milicja ").Append((swm > 0 ? sm / swm : 1).ToString("0.000", Inv))
                         .Append("; czynnik poboru min/med/max ").Append(fl[0].ToString("0.00", Inv)).Append("/").Append(fl[fl.Count / 2].ToString("0.00", Inv)).Append("/").Append(fl[fl.Count - 1].ToString("0.00", Inv))
                         .Append(") ");
                }
                _mem = new Memory { Day = Today(), Map = map };                       // jedna referencja
                _report = parts.ToString().Trim() + " | na granicach 0.1 / 3: " + lo + " / " + hi + " | Straz poza regula " + watch + " osad, bez skali GL0 " + noScale;
                if (daily) Flush(true);
            }
            catch (Exception e) { Stumble("Build", e); if (daily) Flush(true); }      // pamiec wczorajsza zostaje
        }

        private static void Flush(bool on)
        {
            int day = Today();
            if (!on)
            {
                if (!_offLogged) { _offLogged = true; Log.Info("Pobor i milicja od ludzi (GL4): dzien " + day + " - WYLACZONE (Levy By People = off, Militia By People = off) - szansa ochotnika, przyrost i limit milicji jak dotad."); }
                return;
            }
            _offLogged = false;
            try
            {
                double milW = 0, milT = 0, milC = 0;
                var kMil = new Dictionary<string, double>(StringComparer.Ordinal);
                var kN = new Dictionary<string, int>(StringComparer.Ordinal);
                var kL = new Dictionary<string, double>(StringComparer.Ordinal);
                var kM = new Dictionary<string, double>(StringComparer.Ordinal);
                var mem = _mem;
                foreach (var s in Settlement.All)
                {
                    if (s == null || !(s.IsVillage || s.IsTown || s.IsCastle)) continue;
                    try
                    {
                        float m = s.Militia;
                        if (s.IsVillage) milW += m; else if (s.IsTown) milT += m; else milC += m;
                        string k = KingdomOf(s);
                        double v; kMil.TryGetValue(k, out v); kMil[k] = v + m;
                        Cell c;
                        if (mem != null && mem.Map.TryGetValue(s, out c))
                        {
                            int n; kN.TryGetValue(k, out n); kN[k] = n + 1;
                            kL.TryGetValue(k, out v); kL[k] = v + c.L;
                            kM.TryGetValue(k, out v); kM[k] = v + c.M;
                        }
                    }
                    catch (Exception e) { Stumble("Flush(osada)", e); }
                }
                double milAdd, milSub, lvSum; Dictionary<string, double> exp;
                lock (_kLock) { milAdd = _milAdd; milSub = _milSub; lvSum = _lvSum; exp = new Dictionary<string, double>(_expect, StringComparer.Ordinal); _expect.Clear(); _milAdd = _milSub = _lvSum = 0; }
                int lvN = Interlocked.Exchange(ref _lvN, 0), lvCut = Interlocked.Exchange(ref _lvCut, 0), milN = Interlocked.Exchange(ref _milN, 0), limN = Interlocked.Exchange(ref _limN, 0);
                int rs = Interlocked.Exchange(ref _readStumbles, 0);
                double expAll = 0; foreach (var v in exp.Values) expAll += v;
                Log.Info("Pobor i milicja od ludzi (GL4): dzien " + day + " - Levy By People " + (LevyOn ? "tak" : "nie") + ", Militia By People " + (MilitiaOn ? "tak" : "nie")
                         + ", pamiec skal dnia " + PeopleScale.MemoryDay + " | " + (mem != null ? _report : "czekam na usluge skal ludzi (GL0) - czynniki 1")
                         + " | pobor: losowan pustych miejsc " + lvN + " (czynnik srednio x" + (lvN > 0 ? (lvSum / lvN).ToString("0.000", Inv) : "-") + ", szansa przycieta do 1: " + lvCut
                         + "), ochotnicy oczekiwani " + expAll.ToString("0.0", Inv) + "/d"
                         + " | milicja swiata " + (milW + milT + milC).ToString("0", Inv) + " (wsie " + milW.ToString("0", Inv) + ", miasta " + milT.ToString("0", Inv) + ", zamki " + milC.ToString("0", Inv)
                         + "), przyrost modelu x czynnik: wywolan " + milN + ", dopisane +" + milAdd.ToString("0.0", Inv) + " / -" + milSub.ToString("0.0", Inv) + ", limit BK x czynnik: wywolan " + limN
                         + " | wpiete: " + _wired + " | potkniecia " + (_stumbles + rs) + ".");
                _stumbles = 0;
                bool week = day % 7 == 0 || !_weekDone;
                if (week && mem != null)
                {
                    _weekDone = true;
                    var keys = new List<string>(kMil.Keys);
                    keys.Sort((a, b) => kMil[b].CompareTo(kMil[a]));
                    var sb = new StringBuilder("Pobor i milicja od ludzi wedlug krolestw (GL4): dzien " + day + " - ");
                    bool first = true;
                    foreach (var k in keys)
                    {
                        int n; kN.TryGetValue(k, out n);
                        double l, m, e; kL.TryGetValue(k, out l); kM.TryGetValue(k, out m); exp.TryGetValue(k, out e);
                        if (!first) sb.Append(" | ");
                        first = false;
                        sb.Append(k).Append(": osad ").Append(n).Append(", czynnik poboru sr. x").Append(n > 0 ? (l / n).ToString("0.00", Inv) : "-")
                          .Append(", milicji sr. x").Append(n > 0 ? (m / n).ToString("0.00", Inv) : "-").Append(", milicja ").Append(kMil[k].ToString("0", Inv))
                          .Append(", ochotnicy oczekiwani ").Append(e.ToString("0.0", Inv)).Append("/d");
                    }
                    Log.Info(sb.Append(".").ToString());
                }
            }
            catch (Exception e) { Stumble("Flush", e); }
        }

        // ============================================================ latki (raz na proces)

        internal static void ApplyAll(Harmony h)
        {
            var got = new List<string>();
            try
            {
                var gm = typeof(TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementMilitiaModel);
                var mi = AccessTools.Method(gm, "CalculateMilitiaChangeInternal", new[] { typeof(Settlement), typeof(bool) });
                if (mi != null) { h.Patch(mi, postfix: new HarmonyMethod(typeof(PeopleLevy), nameof(MilitiaPostfix))); got.Add("przyrost milicji gry (CalculateMilitiaChangeInternal)"); }
                else
                {
                    var mp = AccessTools.Method(gm, "CalculateMilitiaChange", new[] { typeof(Settlement), typeof(bool) });
                    if (mp != null) { h.Patch(mp, postfix: new HarmonyMethod(typeof(PeopleLevy), nameof(MilitiaPostfix))); got.Add("przyrost milicji gry (CalculateMilitiaChange)"); }
                    else got.Add("BRAK modelu milicji gry");
                }
            }
            catch (Exception e) { got.Add("BLAD modelu milicji gry"); Log.Error("PeopleLevy.ApplyAll(milicja)", e); }
            try
            {
                var bk = AccessTools.TypeByName("BannerKings.Models.Vanilla.BKMilitiaModel");
                var ml = bk != null ? AccessTools.Method(bk, "GetMilitiaLimit") : null;
                if (ml != null) { h.Patch(ml, postfix: new HarmonyMethod(typeof(PeopleLevy), nameof(LimitPostfix))); got.Add("limit milicji BK (GetMilitiaLimit)"); }
                else got.Add("limit milicji BK: brak (bez BannerKings)");
            }
            catch (Exception e) { got.Add("BLAD limitu milicji BK"); Log.Error("PeopleLevy.ApplyAll(limit BK)", e); }
            got.Add("szansa ochotnika (Levy.ProbabilityPostfix)");
            _wired = string.Join(", ", got.ToArray());
            Log.Info("Pobor i milicja od ludzi (GL4): latki - " + _wired + ".");
        }
    }
}
