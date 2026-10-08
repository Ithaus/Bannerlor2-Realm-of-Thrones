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

namespace Armoury
{
    /// <summary>
    /// WYSPY: KARAWANY I LORDOWIE NIE STOJA (paczka 154; autotest 6 07.10 12:37, 40 dob, DLL c92f7e7 - pamiec drog naprawiona, "sciana bez
    /// wpisu" 0 przez 40 dob, ALE straznik BK GuardSettlementMove odrzuca codziennie 3-13 rozkazow karawan i 0-6 lordow: "brak drogi ladowej do
    /// celu" i "na morzu"; stoja karawany w Pyke 3-10 naraz (najdluzej 13.9 doby), Pebbleton 1-4, Lys, Mormont Keep, Lonely Light, Blacktyde,
    /// w 1.-2. dobie Kyth, Qarkash, Lhazosh (Essos) i miasta Westeros; lord w Pebbleton 15.6 doby i dalej).
    /// PRZYCZYNA (kod BK z gry, gra 1.4.8, NavalDLC, BKROTPatch; dane siatki mapy ROT): siatka drog ladowych ma 35 oddzielnych czesci - lad
    /// Westeros, Essos (Kyth, Qarkash, Lhazosh), Pyke z Lordsport i 16 wysp z jednym miastem (Pebbleton, Lonely Light, Blacktyde, Lys, Mormont
    /// Keep, Hull, Arbor, ...). Karawanami rzadzi BK (CaravansCampaignBehavior_HourlyTickParty_Skip); BKCaravansBehavior.GetTradeScoreForTown
    /// liczy odleglosc do miasta Default (1e8 bez drogi ladowej) i w drugim przebiegu wyboru (ThinkNextDestination: distanceCut false)
    /// bierze najlepsze miasto bez wzgledu na droge - karawana bez statkow dostaje rozkaz Default do miasta na innej wyspie, straznik BK go
    /// odrzuca, karawana stoi i probuje dalej (BKROTPatch: raz na dobe, w miescie z szansa 1/3). Gra sama takiego miasta nie wybiera
    /// (CaravansCampaignBehavior.GetTradeScoreForTown: AiHelper -> None -> wynik -1). Lordow trzymaja sztywne rozkazy Default BK: uczta
    /// (BKFeastBehavior.HourlyTickPartyImpl: DisableAi + Default), gentry (BKGentryBehavior.OnPartyDailyTick: DisableForHours(72) + Default
    /// do majatku, co dobe), zakup zywnosci (BKPartyBehavior.GoBuyFood) - z wyspy bez drogi ladowej odrzucone, a lord z AI wylaczonym przez
    /// BK nie wyjedzie z osady (MobileParty.CheckExitingSettlementParallel: Ai.IsDisabled = zostaje).
    /// LATKA (wylacznik IslandRoadsFix), jedna regula w jednym miejscu - postfiks Armoury na straznika BK (CartTownExit.GuardPostfix):
    ///  (c) rozkaz BK Default odrzucony, a partia ma statki (i lad) - ten sam cel droga ladem i morzem (NavigationType.All), gdy pamiec drog
    ///      zna te droge (GetDistance(partia, cel, All) ponizej progu BK);
    ///  (a) karawana bez statkow - zamiast celu bez drogi ladowej cel osiagalny ladem: wybor BK wedle zysku (ThinkNextDestination), a gdy go
    ///      nie ma - dom, potem najblizsze miasto ladem (ten sam zamiar co awaryjny cel BK "dom albo najblizsze miasto"); gdy zadnego miasta
    ///      ladem nie ma (wyspa z jednym miastem) - zostaje, jak karawana gry bez celu (gra nie wydaje wtedy rozkazu - czeka w miescie);
    ///      u zrodla: w ocenie miast BK (postfiks GetTradeScoreForTown) miasto bez drogi ladowej dla karawany bez statkow = -1, jak u gry;
    ///  (b) uczta i gentry BK: gdy rozkaz BK bez drogi ladowej nie moze byc wykonany (odrzucony, albo lord zamkniety w osadzie z wylaczonym
    ///      AI), AI lorda wraca - uczta bez niego albo pojedzie sam, gdy bedzie mogl.
    /// Nic nie powstaje i nic nie znika: tylko rozkazy ruchu i AI lorda. Linia dnia "Wyspy i drogi" (tylko log).
    /// </summary>
    internal static class IslandRoads
    {
        internal const int None = 0, ToAll = 1, Redirect = 2, Wait = 3, Kept = 4;

        private static Type _bkCar;
        private static MethodInfo _think, _getBeh;
        private static readonly MethodInfo _setMove = typeof(MobileParty).GetMethod("SetMoveGoToSettlement", new[] { typeof(Settlement), typeof(MobileParty.NavigationType), typeof(bool) });
        private static bool _wiredScore, _wiredFeast, _wiredGentry;
        internal static bool NavRef;                              // straznik BK ma parametr ref NavigationType - zmiana drogi mozliwa (CartTownExit.ApplyAll)
        private static int _mainThread = -1;                      // watek, w ktorym wpinamy latki (glowny watek gry)
        private static readonly object _lock = new object();
        private static readonly HashSet<string> _errSites = new HashSet<string>();

        // rozkaz owiniety (uczta / gentry BK) - ten sam watek, ten sam stos wywolan
        [ThreadStatic] private static MobileParty _call;
        [ThreadStatic] private static int _cBk, _cAll, _cKept;
        [ThreadStatic] private static MobileParty _redir;         // trwa nasz rozkaz zastepczy dla tej karawany
        [ThreadStatic] private static bool _redirOk;

        // ------------------------------------------------------------ liczniki doby (pod _lock)
        private static int _cut;
        private static readonly HashSet<MobileParty> _cutCar = new HashSet<MobileParty>();
        private static int _allLord, _allCar, _allOther;
        private static int _rProfit, _rHome, _rNear, _rFail;
        private static readonly Dictionary<string, int> _rAt = new Dictionary<string, int>();
        private static int _wait;
        private static readonly Dictionary<string, int> _waitAt = new Dictionary<string, int>();
        private static int _keptLord, _keptCar, _keptOther;
        private static int _aiFeast, _aiGentry;
        private static readonly List<string> _ex = new List<string>();   // przyklady doby: skad -> dokad => co
        private const int MaxEx = 6;
        private static int _stumbles, _stumblesAll;

        // ------------------------------------------------------------ stan (pamiec; czyszczony w Reset)
        private static readonly Dictionary<Settlement, bool> _lonely = new Dictionary<Settlement, bool>();          // miasto bez innego miasta w zasiegu ladu
        private static readonly Dictionary<MobileParty, Rec> _inTown = new Dictionary<MobileParty, Rec>();          // karawana bez statkow w miescie -> od kiedy (probka raz na dobe)
        internal const double IdleDays = 7.0;                                                                       // "stoi w miescie" = ta sama osada w probkach dobowych od tylu dob
        // (recenzja 07.10: 7, nie 3 - BK decyduje o karawanie w miescie raz na dobe (BKROTPatch) z szansa 1/3 na wyjazd, wiec zwykly postoj trwa
        // srednio 3 doby i ok. 30% karawan w miastach stoi 3+ doby; od 7 dob - ok. 6%, wiec Pyke i wyspy widac ponad zwykly postoj)
        private static readonly Dictionary<MobileParty, Rec> _pinned = new Dictionary<MobileParty, Rec>();          // lord w osadzie z AI wylaczonym przez BK (uczta / gentry), cel gdzie indziej
        private sealed class Rec { public Settlement At; public double First, Last; public bool Feast; }
        internal const double PinWindowDays = 2.0;

        internal static void Reset()
        {
            lock (_lock)
            {
                _lonely.Clear(); _inTown.Clear(); _pinned.Clear(); _errSites.Clear();
                _stumblesAll = 0;
                ClearDay();
            }
        }

        private static void ClearDay()
        {
            _cut = 0; _cutCar.Clear();
            _allLord = _allCar = _allOther = 0;
            _rProfit = _rHome = _rNear = _rFail = 0; _rAt.Clear();
            _wait = 0; _waitAt.Clear();
            _keptLord = _keptCar = _keptOther = 0;
            _aiFeast = _aiGentry = 0;
            _ex.Clear();
            _stumbles = 0;
        }

        private static void Stumble(string where, Exception e)
        {
            lock (_lock)
            {
                _stumbles++; _stumblesAll++;
                if (_errSites.Add(where)) Log.Error(where, e);
            }
        }

        private static string Name(Settlement s)
        {
            try { return s != null && s.Name != null ? s.Name.ToString() : "?"; } catch { return "?"; }
        }

        private static string Kind(MobileParty p)
        {
            try { return p.IsLordParty ? "lord" : p.IsCaravan ? "karawana" : "inna partia"; } catch { return "?"; }
        }

        private static void Bump(Dictionary<string, int> d, string k) { int n; d.TryGetValue(k, out n); d[k] = n + 1; }

        private static string Top(Dictionary<string, int> d, int k)
        {
            if (d.Count == 0) return "";
            return " [" + string.Join(", ", d.OrderByDescending(x => x.Value).Take(k).Select(x => x.Key + " " + x.Value)) + (d.Count > k ? ", ..." : "") + "]";
        }

        private static void Example(string s) { if (_ex.Count < MaxEx) _ex.Add(s); }

        private static bool On()
        {
            var s = Settings.Current;
            return s != null && s.IslandRoadsFix;
        }

        // ------------------------------------------------------------ odleglosc: ta sama, ktora liczy straznik BK
        private static bool Reach(MobileParty p, Settlement s, MobileParty.NavigationType nt)
        {
            float lr;
            float d = Campaign.Current.Models.MapDistanceModel.GetDistance(p, s, false, nt, out lr);
            return d >= 0f && d < CartTownExit.BkLimit;
        }

        private static bool Hostile(Settlement s, MobileParty p)
        {
            try { var f = s.MapFaction; var pf = p.MapFaction; return f != null && pf != null && f.IsAtWarWith(pf); }
            catch { return false; }
        }

        /// <summary>Miasto, do ktorego karawana moze jechac: miasto (nie zamek), nie to, w ktorym stoi, nie odrzucony cel, bez oblezenia, nie wrog,
        /// droga ladowa w pamieci drog (te same warunki co wybor celu BK i jego cel awaryjny).</summary>
        private static bool Fit(MobileParty p, Settlement s, Settlement to)
        {
            if (s == null || s == to || s == p.CurrentSettlement || !s.IsTown || s.IsUnderSiege || Hostile(s, p)) return false;
            return Reach(p, s, MobileParty.NavigationType.Default);
        }

        /// <summary>Wybor celu BK (BKCaravansBehavior.ThinkNextDestination) - wedle zysku BK; przy czynnej latce oceny BK pomija miasta bez drogi ladowej.</summary>
        private static Town BkThink(MobileParty p)
        {
            if (_think == null || _getBeh == null || Campaign.Current == null) return null;
            object beh;
            try { beh = _getBeh.Invoke(Campaign.Current, null); }
            catch (TargetInvocationException e) { throw e.InnerException ?? e; }
            if (beh == null) return null;
            try { return _think.Invoke(beh, new object[] { p }) as Town; }
            catch (TargetInvocationException e) { throw e.InnerException ?? e; }
        }

        /// <summary>Najblizsze miasto, do ktorego karawana dojedzie ladem (pamiec drog), wedle drogi - nie linii prostej.</summary>
        private static Settlement NearestByRoad(MobileParty p, Settlement to)
        {
            var m = Campaign.Current.Models.MapDistanceModel;
            Settlement best = null; float bd = float.MaxValue;
            foreach (var t in Town.AllTowns)
            {
                var s = t != null ? t.Settlement : null;
                if (s == null || s == to || s == p.CurrentSettlement || !s.IsTown || s.IsUnderSiege || Hostile(s, p)) continue;
                float lr;
                float d = m.GetDistance(p, s, false, MobileParty.NavigationType.Default, out lr);
                if (d >= 0f && d < CartTownExit.BkLimit && d < bd) { bd = d; best = s; }
            }
            return best;
        }

        // ------------------------------------------------------------ latka w strazniku BK (wolana z CartTownExit.GuardPostfix)
        /// <summary>BK pozwolil na rozkaz (najczestszy przypadek) - tylko znacznik dla owinietej uczty / gentry i dla naszego rozkazu zastepczego.</summary>
        internal static void NoteAccepted(MobileParty p)
        {
            if (p == null) return;
            if (_call != null && p == _call) _cBk++;
            if (_redir != null && p == _redir) _redirOk = true;
        }

        /// <summary>
        /// BK odrzucil rozkaz Default (brak drogi ladowej z miejsca partii do celu). Partia ze statkami - ten sam cel droga ladem i morzem
        /// (nav = All, result = true); karawana bez statkow - nasz rozkaz do miasta osiagalnego ladem (rozkaz BK zostaje odrzucony); inaczej bez
        /// zmian. Zwraca, co zrobiono (ToAll / Redirect / Wait / Kept; None = latka wylaczona albo nie dotyczy).
        /// </summary>
        internal static int OnRejected(MobileParty p, Settlement to, ref MobileParty.NavigationType nav, bool canNav, ref bool result)
        {
            if (p == null || to == null) return None;
            bool mine = _call != null && p == _call;
            int how = None;
            try
            {
                if (_redir != null || !On() || p.IsMainParty || p.IsVillager) how = None;   // nasz wlasny rozkaz zastepczy / wylaczone / gracz / woz wsi (CartTownExit)
                else
                {
                    // (c) jedna regula dla wszystkich partii: kto ma statki i lad, jedzie tam, gdzie kazal BK, ladem i morzem
                    if (canNav && nav == MobileParty.NavigationType.Default && p.HasLandNavigationCapability && p.HasNavalNavigationCapability && !p.IsInRaftState
                        && Reach(p, to, MobileParty.NavigationType.All))
                    {
                        nav = MobileParty.NavigationType.All;
                        result = true;
                        how = ToAll;
                        lock (_lock)
                        {
                            if (p.IsLordParty) _allLord++; else if (p.IsCaravan) _allCar++; else _allOther++;
                            Example(Kind(p) + " " + (p.CurrentSettlement != null ? Name(p.CurrentSettlement) : "w polu") + " -> " + Name(to) + (mine ? " (" + CallName() + ")" : "") + " => ladem i morzem");
                        }
                    }
                    // (a) karawana bez statkow: cel osiagalny ladem zamiast celu bez drogi ladowej (karawana z zadania - bez zmian: jej rozkazy
                    // wydaje tez zadanie, a ono czeka na przyjazd do swojego celu; recenzja 07.10)
                    else if (p.IsCaravan && p.HasLandNavigationCapability && !p.HasNavalNavigationCapability && !p.IsCurrentlyUsedByAQuest)
                        how = CaravanElsewhere(p, to);
                    else
                    {
                        how = Kept;
                        lock (_lock)
                        {
                            if (p.IsLordParty) _keptLord++; else if (p.IsCaravan) _keptCar++; else _keptOther++;
                            string why = p.HasNavalNavigationCapability ? "ladem i morzem tez brak drogi"
                                       : !p.HasLandNavigationCapability ? "partia morska bez statkow"
                                       : p.IsCaravan && p.IsCurrentlyUsedByAQuest ? "karawana z zadania"
                                       : p.IsCurrentlyAtSea ? "na morzu bez statkow" : "bez statkow";
                            Example(Kind(p) + " " + (p.CurrentSettlement != null ? Name(p.CurrentSettlement) : "w polu") + " -> " + Name(to) + (mine ? " (" + CallName() + ")" : "")
                                    + " => bez zmian (" + why + ")");
                        }
                    }
                }
            }
            catch (Exception e) { Stumble("IslandRoads.OnRejected", e); }
            if (mine) { if (result) _cAll++; else _cKept++; }
            return how;
        }

        [ThreadStatic] private static bool _callFeast;
        private static string CallName() { return _callFeast ? "uczta BK" : "gentry BK"; }

        /// <summary>(a) karawana bez statkow: wybor BK wedle zysku, potem dom, potem najblizsze miasto ladem; brak - czeka (jak karawana gry bez celu).</summary>
        private static int CaravanElsewhere(MobileParty p, Settlement to)
        {
            var at = p.CurrentSettlement;
            string here = at != null ? Name(at) : "w polu";
            Settlement alt = null; int kind = 0;
            Town t = null;
            if (System.Threading.Thread.CurrentThread.ManagedThreadId == _mainThread)              // stan BK (slowniki karawan) nie znosi innych watkow - poza glownym tylko dom i najblizsze miasto
            {
                try { t = BkThink(p); } catch (Exception e) { Stumble("IslandRoads.BkThink", e); }   // wywrotka w BK - zostaja dom i najblizsze miasto
            }
            if (t != null && Fit(p, t.Settlement, to)) { alt = t.Settlement; kind = 1; }
            if (alt == null)
            {
                var home = p.HomeSettlement;
                if (home != null && Fit(p, home, to)) { alt = home; kind = 2; }
            }
            if (alt == null) { alt = NearestByRoad(p, to); if (alt != null) kind = 3; }
            if (alt == null)
            {
                lock (_lock) { _wait++; Bump(_waitAt, here); Example("karawana " + here + " -> " + Name(to) + " => czeka (zadnego miasta ladem)"); }
                return Wait;
            }
            bool ok;
            _redir = p; _redirOk = false;
            try
            {
                // przez wejscie metody (MethodInfo.Invoke) - z prefiksem BK i jego straznikiem, jak kazdy rozkaz; wywolanie wprost JIT moze
                // zastapic kopia krotkiej metody gry bez latek (proba 07.10: metoda skompilowana przed latka BK omijala prefiks BK)
                if (_setMove != null)
                {
                    try { _setMove.Invoke(p, new object[] { alt, MobileParty.NavigationType.Default, false }); }
                    catch (TargetInvocationException e) { throw e.InnerException ?? e; }
                }
                else p.SetMoveGoToSettlement(alt, MobileParty.NavigationType.Default, false);
                ok = _redirOk;
            }
            finally { _redir = null; }
            lock (_lock)
            {
                if (!ok) { _rFail++; Example("karawana " + here + " -> " + Name(to) + " => " + Name(alt) + " NIE PRZESZLO"); return Kept; }
                if (kind == 1) _rProfit++; else if (kind == 2) _rHome++; else _rNear++;
                Bump(_rAt, here);
                Example("karawana " + here + " -> " + Name(to) + " => " + Name(alt) + (kind == 1 ? " (wybor BK wedle zysku)" : kind == 2 ? " (dom)" : " (najblizsze miasto ladem)"));
            }
            return Redirect;
        }

        // ------------------------------------------------------------ (a) u zrodla: ocena miasta BK
        /// <summary>Postfiks BKCaravansBehavior.GetTradeScoreForTown(caravan, town, ...): karawana bez statkow - miasto bez drogi ladowej (ta sama
        /// odleglosc, ktora liczy straznik BK) nie jest celem; -1 jak w ocenie gry (CaravansCampaignBehavior.GetTradeScoreForTown: AiHelper None).</summary>
        public static void ScorePostfix(MobileParty __0, Town __1, ref float __result)
        {
            if (!(__result > 0f) || __0 == null || __1 == null) return;
            try
            {
                if (!On() || !__0.HasLandNavigationCapability || __0.HasNavalNavigationCapability) return;   // ze statkami BK daje Naval - straznik jej nie dotyczy
                if (Reach(__0, __1.Settlement, MobileParty.NavigationType.Default)) return;
                __result = -1f;
                lock (_lock) { _cut++; _cutCar.Add(__0); }
            }
            catch (Exception e) { Stumble("IslandRoads.ScorePostfix", e); }
        }

        // ------------------------------------------------------------ (b) uczta i gentry BK
        public static void FeastPrefix(MobileParty __0, out bool __state) { __state = Begin(__0, true); }
        public static void FeastPostfix(MobileParty __0, bool __state) { End(__0, __state, true); }
        public static void GentryPrefix(MobileParty __0, out bool __state) { __state = Begin(__0, false); }
        public static void GentryPostfix(MobileParty __0, bool __state) { End(__0, __state, false); }

        private static bool Begin(MobileParty p, bool feast)
        {
            // zawsze od nowa: gdyby metoda BK rzucila wyjatkiem (postfiks wtedy nie biegnie), stary kontekst nie blokuje nastepnych wywolan
            _call = p; _callFeast = feast; _cBk = _cAll = _cKept = 0;
            return p != null;
        }

        private static void End(MobileParty p, bool mine, bool feast)
        {
            if (!mine || _call != p) return;
            try
            {
                if (p == null || p.Ai == null || (_cBk + _cAll + _cKept) == 0) return;   // BK nic nie rozkazal w tym wywolaniu
                var at = p.CurrentSettlement;
                bool shut = at != null && p.TargetSettlement != at;                      // w osadzie z wylaczonym AI partia nie wyjedzie
                if (On() && p.Ai.IsDisabled && _cBk == 0 && !p.IsInRaftState && !(p.IsCurrentlyAtSea && !p.HasNavalNavigationCapability)
                    && ((_cAll == 0 && _cKept > 0) || (shut && (_cAll + _cKept) > 0)))
                {
                    // rozkaz BK bez drogi ladowej nie zostanie wykonany: odrzucony, albo lord zamkniety w osadzie z AI wylaczonym przez BK
                    p.Ai.EnableAi();
                    lock (_lock) { if (feast) _aiFeast++; else _aiGentry++; }
                }
                // diagnoza (tylko log): lord w osadzie z AI wylaczonym przez BK i celem gdzie indziej - nie wyjedzie, dopoki BK nie odda AI
                // (tratwa i partia na morzu bez statkow - AI trzyma gra, nie BK)
                if (p.Ai.IsDisabled && shut && !p.IsInRaftState && !(p.IsCurrentlyAtSea && !p.HasNavalNavigationCapability))
                {
                    double now = CampaignTime.Now.ToDays;
                    lock (_lock)
                    {
                        Rec r;
                        if (!_pinned.TryGetValue(p, out r) || r.At != at) { r = new Rec { At = at, First = now, Feast = feast }; _pinned[p] = r; }
                        r.Last = now; r.Feast = feast;
                    }
                }
            }
            catch (Exception e) { Stumble("IslandRoads.End", e); }
            finally { _call = null; }
        }

        // ------------------------------------------------------------ linia dnia
        /// <summary>Kto ma karawane i skad ona jest (tylko log; recenzja 07.10): wlasciciel lord / kupiec / inny, dom tutaj / zamek / wies / inne miasto.
        /// Karawana lorda BK (BKLordPropertyBehavior) powstaje w osadzie, do ktorej lord wjechal - w zamku albo wsi z szablonem ladowym, takze na wyspie.</summary>
        private static string Who(MobileParty c, Settlement at)
        {
            string o = "inny";
            try { var h = c.Owner; o = h == null ? "brak" : h.IsLord ? "lord" : h.IsMerchant ? "kupiec" : "inny"; } catch { }
            string d = "?";
            try { var home = c.HomeSettlement; d = home == null ? "brak" : home == at ? "tutaj" : home.IsCastle ? "zamek" : home.IsVillage ? "wies" : home.IsTown ? "inne miasto" : "inne"; } catch { }
            return o + " - dom " + d;
        }

        private static bool Lonely(Settlement town)
        {
            bool v;
            if (_lonely.TryGetValue(town, out v)) return v;
            var m = Campaign.Current.Models.MapDistanceModel;
            v = true;
            foreach (var t in Town.AllTowns)
            {
                var s = t != null ? t.Settlement : null;
                if (s == null || s == town || !s.IsTown) continue;
                float d = m.GetDistance(town, s, false, false, MobileParty.NavigationType.Default);
                if (d >= 0f && d < CartTownExit.BkLimit) { v = false; break; }
            }
            _lonely[town] = v;
            return v;
        }

        /// <summary>Raz na dobe (z CartTownExit.Daily): linia "Wyspy i drogi:" - co latka zrobila z rozkazami BK bez drogi ladowej, kto dalej stoi.</summary>
        internal static void Daily()
        {
            var s = Settings.Current;
            if (s == null) return;
            var inv = CultureInfo.InvariantCulture;
            double now = CampaignTime.Now.ToDays;
            int day = (int)now - 1;
            lock (_lock)
            {
                // karawany bez statkow w miescie (probka raz na dobe): stojace od IdleDays+ dob w dowolnym miescie (czekaja na zysk - jak w grze) i te
                // w miescie, z ktorego zadne inne miasto nie jest osiagalne ladem (wyspa z jednym miastem) - tam czekaja zawsze
                int idleN = 0, lonelyN = 0; double idleMax = -1, lonelyMax = -1; string idleAt = "", lonelyAt = "";
                var idleTowns = new Dictionary<string, int>(); var lonelyTowns = new Dictionary<string, int>();
                var idleWho = new Dictionary<string, int>(); var lonelyWho = new Dictionary<string, int>();   // kto ma karawane i skad ona jest (recenzja 07.10)
                var seen = new HashSet<MobileParty>();
                try
                {
                    foreach (var c in MobileParty.AllCaravanParties)
                    {
                        try
                        {
                            if (c == null || !c.IsActive || !c.HasLandNavigationCapability || c.HasNavalNavigationCapability) continue;
                            var at = c.CurrentSettlement;
                            if (at == null || !at.IsTown) continue;
                            seen.Add(c);
                            Rec r;
                            if (!_inTown.TryGetValue(c, out r) || r.At != at) { r = new Rec { At = at, First = now }; _inTown[c] = r; }
                            r.Last = now;
                            double dur = now - r.First;
                            string where = Name(at) + (c.HomeSettlement != null && c.HomeSettlement != at ? " (dom " + Name(c.HomeSettlement) + ")" : "");
                            if (Lonely(at))
                            {
                                lonelyN++; Bump(lonelyTowns, Name(at)); Bump(lonelyWho, Who(c, at));
                                if (dur > lonelyMax) { lonelyMax = dur; lonelyAt = where; }
                            }
                            if (dur >= IdleDays)
                            {
                                idleN++; Bump(idleTowns, Name(at)); Bump(idleWho, Who(c, at));
                                if (dur > idleMax) { idleMax = dur; idleAt = where; }
                            }
                        }
                        catch (Exception e) { _stumbles++; _stumblesAll++; if (_errSites.Add("IslandRoads.Daily (karawana)")) Log.Error("IslandRoads.Daily (karawana)", e); }
                    }
                }
                catch (Exception e) { _stumbles++; _stumblesAll++; if (_errSites.Add("IslandRoads.Daily")) Log.Error("IslandRoads.Daily", e); }
                List<MobileParty> gone = null;
                foreach (var k in _inTown.Keys) if (!seen.Contains(k)) { if (gone == null) gone = new List<MobileParty>(); gone.Add(k); }
                if (gone != null) foreach (var k in gone) _inTown.Remove(k);
                // lordowie w osadzie z AI wylaczonym przez BK (uczta / gentry) i celem gdzie indziej
                int pinF = 0, pinG = 0; double pinMax = -1; string pinAt = "";
                var pinTowns = new Dictionary<string, int>();
                List<MobileParty> left = null;
                foreach (var kv in _pinned)
                {
                    var p = kv.Key; var r = kv.Value;
                    bool stays = false;
                    try { stays = p != null && p.IsActive && p.CurrentSettlement == r.At && p.Ai != null && p.Ai.IsDisabled && now - r.Last <= PinWindowDays; } catch { }
                    if (!stays) { if (left == null) left = new List<MobileParty>(); left.Add(p); continue; }
                    if (r.Feast) pinF++; else pinG++;
                    Bump(pinTowns, Name(r.At));
                    double dur = now - r.First;
                    if (dur > pinMax) { pinMax = dur; pinAt = (r.Feast ? "uczta" : "gentry") + " w " + Name(r.At); }
                }
                if (left != null) foreach (var p in left) _pinned.Remove(p);

                var sb = new StringBuilder();
                sb.Append("Wyspy i drogi (IslandRoadsFix ").Append(s.IslandRoadsFix ? "CZYNNE" : "WYLACZONE").Append("): dzien ").Append(day)
                  .Append(" - rozkazy BK bez drogi ladowej (straznik BK odrzucil): na te sama droge ladem i morzem (partia ma statki) lordow ").Append(_allLord)
                  .Append(", karawan ").Append(_allCar).Append(", innych ").Append(_allOther)
                  .Append("; karawany bez statkow - inny cel osiagalny ladem: wybor BK wedle zysku ").Append(_rProfit).Append(", dom ").Append(_rHome)
                  .Append(", najblizsze miasto ladem ").Append(_rNear).Append(Top(_rAt, 4))
                  .Append(", rozkaz nie przeszedl ").Append(_rFail)
                  .Append(", zadnego miasta ladem - czeka w miescie (jak w grze) ").Append(_wait).Append(Top(_waitAt, 4))
                  .Append("; bez zmian (bez statkow albo bez drogi ladem i morzem, partia morska bez statkow, karawana z zadania): lordow ").Append(_keptLord).Append(", karawan ").Append(_keptCar).Append(", innych ").Append(_keptOther)
                  .Append("; wybor celu karawan BK: miasto bez drogi ladowej odrzucone w ocenie ").Append(_cut).Append(" razy (").Append(_cutCar.Count).Append(" karawan)")
                  .Append("; AI lorda oddane po rozkazie BK, ktorego nie da sie wykonac: uczta ").Append(_aiFeast).Append(", gentry ").Append(_aiGentry)
                  .Append("; lordowie w osadzie z AI wylaczonym przez BK i celem gdzie indziej (ostatnie ").Append(PinWindowDays.ToString("0", inv)).Append(" doby): uczta ").Append(pinF)
                  .Append(", gentry ").Append(pinG).Append(Top(pinTowns, 4))
                  .Append(", najdluzej ").Append(pinMax >= 0 ? pinMax.ToString("0.0", inv) + " dob - " + pinAt : "-")
                  .Append("; karawany bez statkow w miescie bez innego miasta w zasiegu ladu (wyspa z jednym miastem) - czekaja: ").Append(lonelyN).Append(Top(lonelyTowns, 6))
                  .Append(lonelyWho.Count > 0 ? " (kto" + Top(lonelyWho, 4) + ")" : "")
                  .Append(", najdluzej ").Append(lonelyMax >= 0 ? lonelyMax.ToString("0.0", inv) + " dob - " + lonelyAt : "-")
                  .Append("; karawany bez statkow w tym samym miescie od ").Append(IdleDays.ToString("0", inv)).Append("+ dob (kazde miasto, probka dobowa): ").Append(idleN).Append(Top(idleTowns, 6))
                  .Append(idleWho.Count > 0 ? " (kto" + Top(idleWho, 4) + ")" : "")
                  .Append(", najdluzej ").Append(idleMax >= 0 ? idleMax.ToString("0.0", inv) + " dob - " + idleAt : "-")
                  .Append("; przyklady: ").Append(_ex.Count > 0 ? string.Join("; ", _ex) : "-")
                  .Append("; wpiete: ocena miast BK ").Append(_wiredScore ? "TAK" : "BRAK").Append(", wybor celu BK ").Append(_think != null ? "TAK" : "BRAK")
                  .Append(", uczta ").Append(_wiredFeast ? "TAK" : "BRAK").Append(", gentry ").Append(_wiredGentry ? "TAK" : "BRAK")
                  .Append(", droga w strazniku ").Append(NavRef ? "TAK" : "BRAK")
                  .Append("; potkniecia dzis ").Append(_stumbles).Append(" (od wczytania ").Append(_stumblesAll).Append(").");
                Log.Info(sb.ToString());
                ClearDay();
            }
        }

        // ------------------------------------------------------------ wpiecie
        internal static void ApplyAll(Harmony h)
        {
            var miss = new List<string>();
            _mainThread = System.Threading.Thread.CurrentThread.ManagedThreadId;
            try
            {
                _bkCar = AccessTools.TypeByName("BannerKings.Behaviours.BKCaravansBehavior");
                if (_bkCar != null)
                {
                    var score = AccessTools.Method(_bkCar, "GetTradeScoreForTown");
                    var ps = score != null ? score.GetParameters() : null;
                    if (score != null && score.ReturnType == typeof(float) && ps.Length >= 2 && ps[0].ParameterType == typeof(MobileParty) && ps[1].ParameterType == typeof(Town))
                    {
                        h.Patch(score, postfix: new HarmonyMethod(typeof(IslandRoads), nameof(ScorePostfix)));
                        _wiredScore = true;
                    }
                    else miss.Add("BKCaravansBehavior.GetTradeScoreForTown(MobileParty, Town, ..)");
                    var think = AccessTools.Method(_bkCar, "ThinkNextDestination", new[] { typeof(MobileParty) });
                    var gb = typeof(Campaign).GetMethod("GetCampaignBehavior", Type.EmptyTypes);
                    if (think != null && think.ReturnType == typeof(Town) && gb != null && gb.IsGenericMethodDefinition) { _think = think; _getBeh = gb.MakeGenericMethod(_bkCar); }
                    else miss.Add("BKCaravansBehavior.ThinkNextDestination(MobileParty)");
                }
                else miss.Add("BannerKings.Behaviours.BKCaravansBehavior");
                var tf = AccessTools.TypeByName("BannerKings.Behaviours.Feasts.BKFeastBehavior");
                var mf = tf != null ? AccessTools.Method(tf, "HourlyTickPartyImpl", new[] { typeof(MobileParty) }) : null;
                if (mf != null) { h.Patch(mf, prefix: new HarmonyMethod(typeof(IslandRoads), nameof(FeastPrefix)), postfix: new HarmonyMethod(typeof(IslandRoads), nameof(FeastPostfix))); _wiredFeast = true; }
                else miss.Add("BKFeastBehavior.HourlyTickPartyImpl(MobileParty)");
                var tg = AccessTools.TypeByName("BannerKings.Behaviours.BKGentryBehavior");
                var mg = tg != null ? AccessTools.Method(tg, "OnPartyDailyTick", new[] { typeof(MobileParty) }) : null;
                if (mg != null) { h.Patch(mg, prefix: new HarmonyMethod(typeof(IslandRoads), nameof(GentryPrefix)), postfix: new HarmonyMethod(typeof(IslandRoads), nameof(GentryPostfix))); _wiredGentry = true; }
                else miss.Add("BKGentryBehavior.OnPartyDailyTick(MobileParty)");
                var s = Settings.Current;
                Log.Info("IslandRoads: wpiete - rozkazy BK bez drogi ladowej (wyspy): partia ze statkami jedzie ladem i morzem, karawana bez statkow do miasta osiagalnego ladem (wybor BK wedle zysku, dom, najblizsze miasto), lord z uczty / gentry BK nie zostaje z wylaczonym AI ("
                         + (s != null && s.IslandRoadsFix ? "CZYNNE" : "wylaczone") + ")" + (miss.Count > 0 ? "; BRAK w kodzie BK: " + string.Join(", ", miss) + " - ta czesc nie dziala, reszta tak" : "") + ".");
            }
            catch (Exception e) { Log.Error("IslandRoads.ApplyAll", e); }
        }
    }
}
