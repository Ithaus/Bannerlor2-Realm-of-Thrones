using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// PROJEKT-LENNA (14.10, slowa Jeffa: "logika przyznawania podbitych zamkow - w kolko dostaje jedna, max dwie postacie ... Tormund mial chyba
    /// z 6 miast"; docs/lenna/ROZPOZNANIE-LENNA.md, docs/lenna/PROJEKT-LENNA.md): REGULA N - KOMU KROL DAJE ZDOBYTY ZAMEK / MIASTO.
    ///
    /// Przyczyna (rozpoznanie rozdz. 3.4): BKROTPatch.Models.BKROTDiplomacyModel.CalculateHeroFiefScore wola sam siebie (nie base), straznik
    /// rekursji zwraca 0 - kazdy kandydat "Owner of X" ma ocene 0, LINQ OrderByDescending jest stabilny, wiec gra (KingdomDecision.NarrowDownCandidates)
    /// bierze 3 pierwsze rody listy krolestwa (kolejnosc wpisow ROT). Glosuje sie w kolko nad ta sama trojka.
    ///
    /// Regula N (jedna dla AI i gracza; kazda decyzja SettlementClaimantDecision - po zdobyciu i po aneksji; liczona z biezacego stanu):
    ///  N1 ocena rodu C do twierdzy X: S = (100 + Z + U + B + J + K) x P / (P + W)
    ///     W = 2 x miasta + 1 x zamki rodu (bez X i bez twierdz czekajacych na nadanie - IsOwnerUnassigned), P = max(1, tier - 1);
    ///     Z = FairFiefConquerorBonus (50) - rod wodza armii, ktora zdobyla (Town.LastCapturedBy), tylko po zdobyciu (IsOwnerUnassigned), nie rod krola;
    ///     U = 25 - inny rod z druzyna lorda po stronie zdobywcy w zwycieskim szturmie (zapis z MapEventEnded do nadania, najwyzej 60 dob), nie rod krola, nie razem z Z;
    ///     B = 50 - rod bez twierdzy (W = 0); J = 100 - czlonek rodu jest posiadaczem de jure tytulu X w BK (deJure.Clan == C);
    ///     K = 25 - rod krola, gdy jego ciezar < 20% ciezaru twierdz krolestwa (bez X);
    ///     dwor BK ("bk_courtiers_") - S = 0; ROT x999 dla rodu krola przy Krolewskiej Przystani - postfiks ROT na CalculateMeritOfOutcome (bez zmian).
    ///  N2 pulap: W + w(X) > P + FairFiefCapExtra (w: miasto 2, zamek 1) -> baza 1 zamiast sumy czlonow (na liscie tylko, gdy nikt inny sie nie miesci).
    ///  N3 lista: 3 najwyzsze oceny (gra), remis - dodatek 0.00-0.99 ze skrotu id osady, id rodu i doby decyzji (tylko w ocenie listy).
    ///  N4 glos rodu V na kandydata C (AI, takze krol AI): D = 100 x M(C) / M_max + relacja(V, C) / 10 + 10 (to samo stronnictwo BK) + 15 (glos na siebie),
    ///     relacja przycieta do +-100 (z samym soba 100 - jak gra), M = ocena listy (InitialMerit, z ROT x999 i remisem), M_max = najwyzsza ocena
    ///     kandydatow tej decyzji (postfiks NarrowDownCandidates); dwor BK -100 000 (jak BK). Zastepuje glos gry i BK.
    ///  N5 decyzja "Owner of X" anulowana tylko z twardych powodow gry (krolestwo upadlo, wnioskodawca poza krolestwem, IsAllowed, twierdza stracona) -
    ///     bez "krol na liscie stoi z boku" (KingdomDecision.ShouldBeCancelled, reszta metody).
    ///  N6 gracz: wasal - lista i podpowiedz BK "Claim Strength" (UIHelper wola ten sam model) wedlug S; krol - wybiera z 3 (gra), Abstain = glos rady;
    ///     prawo pierwszenstwa i Grant Fief Diplomacy bez zmian.
    ///
    /// Latki (w kampanii, raz na proces - EnsureHooks z OnSessionLaunched; typy BK / BKROT i model BK znane dopiero tam):
    ///  H1 prefiks Priority.First na CalculateHeroFiefScore czynnego modelu BK (BannerKingsConfig.Instance.DiplomacyModel; BKROTDiplomacyModel)
    ///     i na BKDiplomacyModel - zwraca S (z opisami dla podpowiedzi); H2 postfiks Last CalculateMeritOfOutcome - remis; H2b postfiks Last
    ///     KingdomDecision.NarrowDownCandidates - M_max decyzji; H3 postfiks Last DetermineSupport - glos N4; H4 prefiks KingdomDecision.ShouldBeCancelled -
    ///     N5; H6 prefiks + postfiks KingdomElection.ApplyChosenOutcome - linia nadania (tylko log); H6b prefiks OnPlayerAbstainedAsRuler (tylko log).
    ///  Zdarzenia (RegisterEvents): MapEventEnded (H5 - uczestnicy szturmu), OnSettlementOwnerChanged (rodzaj ostatniej zmiany pana; nadana twierdza
    ///  zmienia pana w krolestwie w 60 dob - linia), KingdomDecisionConcluded / KingdomDecisionCancelled (liczniki doby).
    /// Wylacznik FairFiefGrants = false: H1-H4 przepuszczaja obce wyniki (stan 7bc5dd0b), reszta tylko liczy i pisze log. Bez zapisu w grze.
    /// Potkniecia liczone per wywolanie (obcy wynik zostaje), zadnego gaszenia funkcji (CLAUDE.md 7).
    /// </summary>
    internal static class FiefGrants
    {
        internal const int BaseClaim = 100, ParticipantBonus = 25, LandlessBonus = 50, DeJureBonus = 100, CrownBonus = 25;
        internal const float CrownShareMax = 0.20f;
        // pamiec szturmu: do nadania tej twierdzy (zmiana pana BySiege w dobie szturmu), najwyzej 60 dob - w krolestwie gracza decyzje "Owner of X"
        // ida po jednej, kazda czeka 48 h, wiec kolejka po wojnie bywa dluzsza niz 10 dob z projektu
        internal const int CaptureMemoryDays = 60, GrantWatchDays = 60;
        private const float CourtierVote = -100000f;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        internal static bool On { get { var s = Settings.Current; return s != null && s.FairFiefGrants; } }
        internal static int ConquerorBonus { get { var s = Settings.Current; int v = s != null ? s.FairFiefConquerorBonus : 50; return v < 0 ? 0 : (v > 200 ? 200 : v); } }
        internal static int CapExtra { get { var s = Settings.Current; int v = s != null ? s.FairFiefCapExtra : 2; return v < 0 ? 0 : (v > 10 ? 10 : v); } }

        // ------------------------------------------------------------ latki (zakladane w kampanii; zyja przez caly proces)
        private static Harmony _harmony;
        internal static void SetHarmony(Harmony h) { _harmony = h; }
        private static bool _hooksTried;
        private static bool _wScore, _wMerit, _wNarrow, _wVote, _wCancel, _wApply, _wAbstain;
        private static string _scoreWhere = "-", _modelType = "-";
        private static readonly List<string> _missing = new List<string>();
        private static MethodInfo _mMerit, _mVote, _mCancelInternal;

        // ------------------------------------------------------------ BK przez refleksje (typy - raz na proces; obiekty BK czytane za kazdym razem)
        private static bool _bkTried, _bkTitleOk, _bkDemOk;
        private static PropertyInfo _pCfg, _pTitleMgr, _pDeJure, _pFullName, _pStab, _pDipModel;
        private static MethodInfo _mGetTitle, _mCurDem, _mDemLim;
        private static Type _tDipBeh;
        private static MethodInfo _mGetKd, _mGetGroup;
        private static object _dipBeh; private static bool _dipTried;   // zachowanie kampanii BK - od nowa w kazdej kampanii (Reset)

        // ------------------------------------------------------------ pamiec kampanii (bez zapisu; Reset z konstruktora ArmouryBehavior)
        private sealed class Capture { public double Day; public Clan Clan; public Hero Hero; public readonly HashSet<Clan> Clans = new HashSet<Clan>(); }
        private sealed class Change { public double Day; public ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail Detail; }
        private sealed class Grant { public double Day; public Clan Clan; public Kingdom Kingdom; }
        private sealed class Pass { public float Max = float.NaN; public int N; }
        private sealed class Flag { }
        private static readonly Dictionary<Settlement, Capture> _captures = new Dictionary<Settlement, Capture>();
        private static readonly Dictionary<Settlement, Change> _lastChange = new Dictionary<Settlement, Change>();
        private static readonly Dictionary<Settlement, Grant> _granted = new Dictionary<Settlement, Grant>();
        private static ConditionalWeakTable<KingdomDecision, Pass> _pass = new ConditionalWeakTable<KingdomDecision, Pass>();
        private static ConditionalWeakTable<KingdomElection, Flag> _abstain = new ConditionalWeakTable<KingdomElection, Flag>();

        // liczniki: d* - od poprzedniej linii doby, s* - od startu sesji
        private static int _dConcluded, _sConcluded, _dLines, _sLines, _dCancelled, _sCancelled, _dOver, _dOverWithIn, _sOverWithIn;
        private static int _dMoved, _sMoved, _dCaptures, _dNoMax, _dStumbles, _sStumbles;
        private static Settlement _applying;   // twierdza nadawana wlasnie przez KingdomElection.ApplyChosenOutcome (H6 prefiks -> postfiks)

        internal static void Reset()
        {
            _captures.Clear(); _lastChange.Clear(); _granted.Clear(); _applying = null;
            _pass = new ConditionalWeakTable<KingdomDecision, Pass>();
            _abstain = new ConditionalWeakTable<KingdomElection, Flag>();
            _dipBeh = null; _dipTried = false;
            _dConcluded = _sConcluded = _dLines = _sLines = _dCancelled = _sCancelled = _dOver = _dOverWithIn = _sOverWithIn = 0;
            _dMoved = _sMoved = _dCaptures = _dNoMax = _dStumbles = _sStumbles = 0;
        }

        private static void Stumble(string where, Exception e)
        {
            _dStumbles++; _sStumbles++;
            if (_sStumbles <= 5 || _sStumbles % 100 == 0) Log.Error("FiefGrants." + where + " (potkniecie " + _sStumbles + ")", e);
        }

        private static double Now { get { return CampaignTime.Now.ToDays; } }
        private static string F(float v) { return v.ToString("0.##", Inv); }
        private static string F2(float v) { return v.ToString("0.00", Inv); }

        // ------------------------------------------------------------ BK
        private static void BkResolve()
        {
            if (_bkTried) return;
            _bkTried = true;
            try
            {
                var cfgT = AccessTools.TypeByName("BannerKings.BannerKingsConfig");
                if (cfgT == null) return;
                _pCfg = AccessTools.Property(cfgT, "Instance");
                _pDipModel = AccessTools.Property(cfgT, "DiplomacyModel");
                _pTitleMgr = AccessTools.Property(cfgT, "TitleManager");
                _mGetTitle = _pTitleMgr != null ? AccessTools.Method(_pTitleMgr.PropertyType, "GetTitle", new[] { typeof(Settlement) }) : null;
                _pDeJure = _mGetTitle != null ? AccessTools.Property(_mGetTitle.ReturnType, "deJure") : null;
                _pFullName = _mGetTitle != null ? AccessTools.Property(_mGetTitle.ReturnType, "FullName") : null;
                _bkTitleOk = _pCfg != null && _pTitleMgr != null && _mGetTitle != null && _pDeJure != null;
                _pStab = AccessTools.Property(cfgT, "StabilityModel");
                var stT = _pStab != null ? _pStab.PropertyType : null;
                _mCurDem = stT != null ? AccessTools.Method(stT, "CalculateCurrentDemesne", new[] { typeof(Clan), typeof(bool) }) : null;
                _mDemLim = stT != null ? AccessTools.Method(stT, "CalculateDemesneLimit", new[] { typeof(Hero), typeof(bool) }) : null;
                _bkDemOk = _pCfg != null && _mCurDem != null && _mDemLim != null;
                _tDipBeh = AccessTools.TypeByName("BannerKings.Behaviours.Diplomacy.BKDiplomacyBehavior");
                _mGetKd = _tDipBeh != null ? AccessTools.Method(_tDipBeh, "GetKingdomDiplomacy", new[] { typeof(Kingdom) }) : null;
                _mGetGroup = _mGetKd != null ? AccessTools.Method(_mGetKd.ReturnType, "GetHeroGroup", new[] { typeof(Hero) }) : null;
            }
            catch (Exception e) { Stumble("BkResolve", e); }
        }

        private static object Cfg() { return _pCfg != null ? _pCfg.GetValue(null, null) : null; }

        /// <summary>Tytul BK twierdzy: rod posiadacza de jure i nazwa tytulu (null - brak tytulu albo BK).</summary>
        private static Clan DeJureClan(Settlement x, out TextObject title)
        {
            title = null;
            if (!_bkTitleOk) return null;
            try
            {
                var cfg = Cfg();
                var tm = cfg != null ? _pTitleMgr.GetValue(cfg, null) : null;
                var t = tm != null ? _mGetTitle.Invoke(tm, new object[] { x }) : null;
                if (t == null) return null;
                var h = _pDeJure.GetValue(t, null) as Hero;
                title = _pFullName != null ? _pFullName.GetValue(t, null) as TextObject : null;
                return h != null ? h.Clan : null;
            }
            catch (Exception e) { Stumble("DeJureClan", e); return null; }
        }

        /// <summary>Domena BK rodu "zajeta/limit" (tylko do logu).</summary>
        private static string BkDemesne(Clan c)
        {
            if (!_bkDemOk || c == null || c.Leader == null) return "-";
            try
            {
                var cfg = Cfg();
                var st = cfg != null ? _pStab.GetValue(cfg, null) : null;
                if (st == null) return "-";
                var cur = (ExplainedNumber)_mCurDem.Invoke(st, new object[] { c, false });
                var lim = (ExplainedNumber)_mDemLim.Invoke(st, new object[] { c.Leader, false });
                return cur.ResultNumber.ToString("0.0", Inv) + "/" + lim.ResultNumber.ToString("0.0", Inv);
            }
            catch (Exception e) { Stumble("BkDemesne", e); return "-"; }
        }

        /// <summary>Stronnictwo BK bohatera (InterestGroup) albo null.</summary>
        private static object GroupOf(Kingdom k, Hero h)
        {
            if (_mGetKd == null || _mGetGroup == null || k == null || h == null) return null;
            if (!_dipTried)
            {
                _dipTried = true;
                try
                {
                    var get = typeof(Campaign).GetMethod("GetCampaignBehavior", Type.EmptyTypes);
                    _dipBeh = get != null && Campaign.Current != null ? get.MakeGenericMethod(_tDipBeh).Invoke(Campaign.Current, null) : null;
                }
                catch (Exception e) { Stumble("GroupOf(zachowanie BK)", e); }
            }
            if (_dipBeh == null) return null;
            var kd = _mGetKd.Invoke(_dipBeh, new object[] { k });
            return kd != null ? _mGetGroup.Invoke(kd, new object[] { h }) : null;
        }

        private static bool IsCourtier(Clan c) { return c != null && c.StringId != null && c.StringId.StartsWith("bk_courtiers_", StringComparison.Ordinal); }

        // ------------------------------------------------------------ N1 / N2 - ocena
        internal sealed class Claim
        {
            public Clan Clan; public int Tier, P, Cap, W, Towns, Castles, WX, Z, U, B, J, K;
            public bool King, Courtier, Over, AfterCapture;
            public float Raw, Factor, S, CrownShare = -1f;
            public TextObject Title;
        }

        /// <summary>Czy decyzja o X jest "po zdobyciu": twierdza bez pana (IsOwnerUnassigned) i ostatnia zmiana pana w tej sesji (jesli znana) to szturm -
        /// nie wymarcie ani odejscie rodu (tez otwieraja roszczenia, a LastCapturedBy jest wtedy stary).</summary>
        private static bool AfterCapture(Settlement x)
        {
            var t = x.Town;
            if (t == null || !t.IsOwnerUnassigned) return false;
            Change ch;
            return !_lastChange.TryGetValue(x, out ch) || ch.Detail == ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.BySiege;
        }

        /// <summary>Zapis szturmu, ktory dal obecne zdobycie X: ostatnia zmiana pana X to BySiege w dobie szturmu (+-1); nie starszy niz 60 dob.</summary>
        private static Capture CaptureOf(Settlement x)
        {
            Capture c;
            if (x == null || !_captures.TryGetValue(x, out c)) return null;
            if (Now - c.Day > CaptureMemoryDays) return null;
            Change ch;
            if (_lastChange.TryGetValue(x, out ch) && (ch.Detail != ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.BySiege || Math.Abs(ch.Day - c.Day) > 1.0)) return null;
            return c;
        }

        private static int Weight(Town t) { return t == null ? 0 : (t.IsTown ? 2 : (t.IsCastle ? 1 : 0)); }

        /// <summary>Udzial korony: ciezar rodu krola / ciezar twierdz krolestwa (bez X); 0, gdy krolestwo nie ma innych twierdz.</summary>
        private static float CrownShareOf(Kingdom k, int crownW, Town x)
        {
            int tot = 0;
            foreach (var f in k.Fiefs) { if (f != null && f != x) tot += Weight(f); }
            return tot > 0 ? crownW / (float)tot : 0f;
        }

        internal static Claim Eval(Settlement x, Clan c)
        {
            var cl = new Claim { Clan = c };
            var town = x.Town;
            cl.WX = x.IsTown ? 2 : 1;
            var k = x.MapFaction as Kingdom ?? c.Kingdom;
            cl.King = k != null && k.RulingClan == c;
            cl.Courtier = IsCourtier(c);
            cl.Tier = c.Tier;
            cl.P = Math.Max(1, c.Tier - 1);
            cl.Cap = cl.P + CapExtra;
            foreach (var f in c.Fiefs)
            {
                // recenzja 14.10: twierdza czekajaca na nadanie (IsOwnerUnassigned - po szturmie i odejsciu rodu trzyma ja krol, po wymarciu rodu rod dziedzic,
                // do decyzji "Owner of X"; naraz jedna decyzja na krolestwo, w krolestwie gracza kazda po 48 h) nie jest posiadaniem - inaczej krol po kazdej
                // wojnie "mial" wszystkie zdobycze naraz (pulap, czynnik, udzial < 20%)
                if (f == null || f == town || f.IsOwnerUnassigned) continue;
                if (f.IsTown) { cl.Towns++; cl.W += 2; }
                else if (f.IsCastle) { cl.Castles++; cl.W += 1; }
            }
            cl.Factor = cl.P / (float)(cl.P + cl.W);
            if (cl.Courtier) { cl.Raw = 0f; cl.S = 0f; return cl; }
            cl.AfterCapture = AfterCapture(x);
            if (cl.AfterCapture && !cl.King)
            {
                if (town.LastCapturedBy == c) cl.Z = ConquerorBonus;
                else { var cap = CaptureOf(x); if (cap != null && cap.Clans.Contains(c)) cl.U = ParticipantBonus; }
            }
            if (cl.W == 0) cl.B = LandlessBonus;
            TextObject title;
            if (DeJureClan(x, out title) == c) { cl.J = DeJureBonus; cl.Title = title; }
            if (cl.King && k != null)
            {
                cl.CrownShare = CrownShareOf(k, cl.W, town);
                if (cl.CrownShare < CrownShareMax) cl.K = CrownBonus;
            }
            cl.Over = cl.W + cl.WX > cl.Cap;
            cl.Raw = cl.Over ? 1f : BaseClaim + cl.Z + cl.U + cl.B + cl.J + cl.K;
            cl.S = cl.Raw * cl.Factor;
            return cl;
        }

        private static TextObject T(string s) { return new TextObject(s); }

        /// <summary>Ocena jako ExplainedNumber (wynik = baza x (1 + (czynnik - 1)) = S); opisy po angielsku tylko dla podpowiedzi BK.</summary>
        private static ExplainedNumber Explain(Settlement x, Claim cl, bool expl)
        {
            if (cl.Courtier) return new ExplainedNumber(0f, expl, null);
            var en = new ExplainedNumber(cl.Over ? 1f : BaseClaim, expl,
                expl ? (cl.Over ? T("Beyond the limit of {CAP} points - only if no other house can take it").SetTextVariable("CAP", cl.Cap) : T("Base claim")) : null);
            if (!cl.Over)
            {
                if (cl.Z > 0) en.Add(cl.Z, expl ? T("Led the army that took {FIEF}").SetTextVariable("FIEF", x.Name) : null);
                if (cl.U > 0) en.Add(cl.U, expl ? T("Fought in the assault on {FIEF}").SetTextVariable("FIEF", x.Name) : null);
                if (cl.B > 0) en.Add(cl.B, expl ? T("Holds no castle or town") : null);
                if (cl.J > 0) en.Add(cl.J, expl ? T("De jure holder of {TITLE}").SetTextVariable("TITLE", cl.Title ?? x.Name) : null);
                if (cl.K > 0) en.Add(cl.K, expl ? T("Royal demesne under a fifth of the realm") : null);
            }
            en.AddFactor(cl.Factor - 1f, expl ? T("Holdings {W} of {P} points (tier {T})").SetTextVariable("W", cl.W).SetTextVariable("P", cl.P).SetTextVariable("T", cl.Tier) : null);
            return en;
        }

        // ------------------------------------------------------------ H1: ocena (model BK / BKROT)
        public static bool ScorePrefix(Settlement __0, Hero __1, bool __2, ref ExplainedNumber __result)
        {
            if (!On) return true;
            try
            {
                if (__0 == null || __0.Town == null || __1 == null || __1.Clan == null) return true;
                __result = Explain(__0, Eval(__0, __1.Clan), __2);
                return false;
            }
            catch (Exception e) { Stumble("ScorePrefix", e); return true; }
        }

        // ------------------------------------------------------------ H2: remis (po BK i ROT x999)
        /// <summary>Dodatek remisu 0.00-0.99 ze skrotu FNV-1a (id osady, id rodu, doba decyzji) - staly w ramach decyzji. Przy ocenie < 10 (tylko rody
        /// ponad pulapem, S <= 1, i dwor BK) x 0.01: inaczej skrot przestawialby kolejnosc "najmniej obciazony pierwszy" (N2).</summary>
        internal static float Tie(Settlement x, Clan c, KingdomDecision d, float merit)
        {
            uint h = 2166136261u;
            string key = (x.StringId ?? "") + "|" + (c.StringId ?? "") + "|" + ((int)Math.Floor(d.TriggerTime.ToDays)).ToString(Inv);
            for (int i = 0; i < key.Length; i++) { h ^= key[i]; h *= 16777619u; }
            float t = (h % 100u) / 100f;
            return merit >= 10f ? t : t * 0.01f;
        }

        public static void MeritPostfix(SettlementClaimantDecision __instance, DecisionOutcome __0, ref float __result)
        {
            if (!On) return;
            try
            {
                var o = __0 as SettlementClaimantDecision.ClanAsDecisionOutcome;
                if (o == null || o.Clan == null || __instance.Settlement == null) return;
                __result += Tie(__instance.Settlement, o.Clan, __instance, __result);
            }
            catch (Exception e) { Stumble("MeritPostfix", e); }
        }

        // ------------------------------------------------------------ H2b: M_max decyzji (wszyscy kandydaci maja juz InitialMerit)
        public static void NarrowPostfix(KingdomDecision __instance, MBList<DecisionOutcome> __0)
        {
            if (!(__instance is SettlementClaimantDecision) || __0 == null) return;
            try
            {
                float max = float.NaN;
                foreach (var o in __0) { if (o != null && (float.IsNaN(max) || o.InitialMerit > max)) max = o.InitialMerit; }
                var p = _pass.GetValue(__instance, _ => new Pass());
                p.Max = max; p.N = __0.Count;
            }
            catch (Exception e) { Stumble("NarrowPostfix", e); }
        }

        // ------------------------------------------------------------ H3: glos rady (N4)
        internal static float Vote(SettlementClaimantDecision d, Clan v, SettlementClaimantDecision.ClanAsDecisionOutcome o)
        {
            float m = o.InitialMerit;
            Pass p;
            float max;
            if (_pass.TryGetValue(d, out p) && !float.IsNaN(p.Max)) max = Math.Max(p.Max, m);
            else { max = m; _dNoMax++; }
            float ratio = max > 0f ? Math.Max(0f, Math.Min(1f, m / max)) : 0f;
            float rel = v == o.Clan ? 100f : Math.Max(-100f, Math.Min(100f, v.Leader.GetRelation(o.Clan.Leader)));
            float r = 100f * ratio + rel / 10f;
            object g = null;
            try { g = GroupOf(d.Kingdom, v.Leader); } catch (Exception e) { Stumble("Vote(stronnictwo)", e); }
            if (g != null)
            {
                object g2 = v == o.Clan ? g : null;
                if (g2 == null) { try { g2 = GroupOf(d.Kingdom, o.Clan.Leader); } catch (Exception e) { Stumble("Vote(stronnictwo)", e); } }
                if (ReferenceEquals(g, g2)) r += 10f;
            }
            if (v == o.Clan) r += 15f;
            return r;
        }

        public static void SupportPostfix(SettlementClaimantDecision __instance, Clan __0, DecisionOutcome __1, ref float __result)
        {
            if (!On) return;
            try
            {
                var o = __1 as SettlementClaimantDecision.ClanAsDecisionOutcome;
                if (o == null || o.Clan == null || o.Clan.Leader == null || __0 == null || __0.Leader == null) return;
                if (IsCourtier(o.Clan)) { __result = CourtierVote; return; }
                __result = Vote(__instance, __0, o);
            }
            catch (Exception e) { Stumble("SupportPostfix", e); }
        }

        // ------------------------------------------------------------ H4: anulowanie tylko z twardych powodow (N5)
        public static bool CancelPrefix(KingdomDecision __instance, ref bool __result)
        {
            var d = __instance as SettlementClaimantDecision;
            if (d == null || !On) return true;
            try
            {
                var k = d.Kingdom;
                bool hard = k == null || k.IsEliminated || d.ProposerClan == null || d.ProposerClan.Kingdom != k || !d.IsAllowed();
                if (!hard && _mCancelInternal != null) hard = (bool)_mCancelInternal.Invoke(d, null);
                else if (!hard) hard = d.Settlement == null || d.Settlement.MapFaction != k;
                __result = hard;
                return false;
            }
            catch (Exception e) { Stumble("CancelPrefix", e); return true; }
        }

        // ------------------------------------------------------------ H5 i zdarzenia
        internal static void RegisterEvents(CampaignBehaviorBase owner)
        {
            CampaignEvents.MapEventEnded.AddNonSerializedListener(owner, OnMapEventEnded);
            CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(owner, OnOwnerChanged);
            CampaignEvents.KingdomDecisionConcluded.AddNonSerializedListener(owner, OnConcluded);
            CampaignEvents.KingdomDecisionCancelled.AddNonSerializedListener(owner, OnCancelled);
        }

        /// <summary>Zwycieski szturm: strona, ktora zdobywa (gra KingdomManager.SiegeCompleted przy isWin) - Siege: atakujacy przy AttackerVictory,
        /// SallyOut / BlockadeSallyOut: obroncy wypadu (oblegajacy) przy DefenderVictory. Zdarzenie gry idzie PRZED zdobyciem (MapEvent.FinalizeEvent).</summary>
        private static void OnMapEventEnded(MapEvent me)
        {
            try
            {
                if (me == null || me.MapEventSettlement == null || me.MapEventSettlement.Town == null) return;
                MapEventSide side = null;
                if (me.IsSiegeAssault && me.BattleState == BattleState.AttackerVictory) side = me.AttackerSide;
                else if ((me.IsSallyOut || me.IsBlockadeSallyOut) && me.BattleState == BattleState.DefenderVictory) side = me.DefenderSide;
                if (side == null) return;
                var rec = new Capture { Day = Now };
                var lp = side.LeaderParty;
                rec.Hero = lp != null ? lp.LeaderHero : null;
                rec.Clan = lp != null && lp.Owner != null ? lp.Owner.Clan : (rec.Hero != null ? rec.Hero.Clan : null);
                foreach (var mep in side.Parties)
                {
                    var p = mep != null ? mep.Party : null;
                    var h = p != null ? p.LeaderHero : null;
                    if (h == null || h.Clan == null) continue;
                    if (p.IsMobile && p.MobileParty != null && !p.MobileParty.IsLordParty) continue;
                    rec.Clans.Add(h.Clan);
                }
                _captures[me.MapEventSettlement] = rec;
                _dCaptures++;
            }
            catch (Exception e) { Stumble("OnMapEventEnded", e); }
        }

        private static void OnOwnerChanged(Settlement s, bool openToClaim, Hero newOwner, Hero oldOwner, Hero capturer, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
        {
            try
            {
                if (s == null || s.Town == null) return;
                double now = Now;
                _lastChange[s] = new Change { Day = now, Detail = detail };
                Grant g;
                if (!_granted.TryGetValue(s, out g)) return;
                // nowe nadanie z glosowania - postfiks H6 je zapisze; ByKingDecision spoza glosowania (BK: prawo petycji z kontraktu, ROT: Harrenhal
                // dla Roose'a) to zmiana pana nadanej twierdzy - liczona jak kazda inna (recenzja 14.10, LN-11)
                if (detail == ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.ByKingDecision && s == _applying) return;
                var nc = newOwner != null ? newOwner.Clan : null;
                var nk = nc != null ? nc.Kingdom : null;
                if (nc == null || now - g.Day > GrantWatchDays || nk != g.Kingdom) { _granted.Remove(s); return; }
                if (nc == g.Clan) return;
                _granted.Remove(s);
                _dMoved++; _sMoved++;
                Log.Info("Nadanie lenna (N) zmiana pana: dzien " + (int)now + " | " + s.Name + " [" + s.StringId + "] | krolestwo " + (nk != null ? nk.Name.ToString() : "-")
                         + " | z " + (g.Clan != null ? g.Clan.Name.ToString() : "-") + " na " + nc.Name + " (" + detail + "), " + (int)Math.Round(now - g.Day) + " dob po nadaniu");
            }
            catch (Exception e) { Stumble("OnOwnerChanged", e); }
        }

        private static void OnConcluded(KingdomDecision d, DecisionOutcome o, bool playerInvolved)
        {
            if (d is SettlementClaimantDecision) { _dConcluded++; _sConcluded++; }
        }

        private static void OnCancelled(KingdomDecision d, bool playerInvolved)
        {
            if (d is SettlementClaimantDecision) { _dCancelled++; _sCancelled++; }
        }

        // ------------------------------------------------------------ H6: linia nadania (tylko log)
        private sealed class GrantState
        {
            public Settlement X; public Clan Winner; public Kingdom K; public int Cap; public string Head, Tail;
        }

        public static void AbstainPrefix(KingdomElection __instance)
        {
            try { if (__instance != null) _abstain.GetValue(__instance, _ => new Flag()); } catch { }
        }

        private static int Points(DecisionOutcome o)
        {
            int n = 0;
            if (o != null && o.SupporterList != null) foreach (var s in o.SupporterList) { if (s != null) n += Math.Max(0, (int)s.SupportWeight - 1); }
            return n;
        }

        /// <summary>Wplyw, ktory rody zaplaca za glosy na kandydata (jak KingdomElection.HandleInfluenceCosts: koszt 20/60/100 wedlug sily glosu,
        /// glosujacy na przegranego placi polowe; bez wyjatku cechy Good Natured i rady jednego rodu) - recenzja 14.10: glos N4 ma ok. 100 dla
        /// 1. w ocenie u kazdego rodu, wiec rody moga placic za glosy wiecej niz dotad (tylko log).</summary>
        private static int Paid(KingdomDecision d, DecisionOutcome o, bool chosen)
        {
            int n = 0;
            if (o == null || o.SupporterList == null) return 0;
            foreach (var s in o.SupporterList)
            {
                if (s == null || s.Clan == null || s.Clan.Leader == null) continue;
                int c = d.GetInfluenceCost(o, s.Clan, s.SupportWeight);
                n += chosen ? c : c / 2;
            }
            return n;
        }

        private static string Holdings(Claim cl) { return cl.Towns + "m/" + cl.Castles + "z = " + cl.W + " pkt"; }

        private static string Parts(Claim cl)
        {
            if (cl.Courtier) return "dwor BK";
            if (cl.Over) return "PONAD PULAPEM: baza 1";
            var sb = new StringBuilder("100");
            if (cl.Z > 0) sb.Append(" + Z ").Append(cl.Z);
            if (cl.U > 0) sb.Append(" + U ").Append(cl.U);
            if (cl.B > 0) sb.Append(" + B ").Append(cl.B);
            if (cl.J > 0) sb.Append(" + J ").Append(cl.J);
            if (cl.K > 0) sb.Append(" + K ").Append(cl.K);
            return sb.ToString();
        }

        public static void ApplyPrefix(KingdomElection __instance, KingdomDecision ____decision, MBList<DecisionOutcome> ____possibleOutcomes, DecisionOutcome ____chosenOutcome,
                                       Clan ____chooser, bool ____ignorePlayerSupport, bool ____hasPlayerVoted, out object __state)
        {
            __state = null;
            var d = ____decision as SettlementClaimantDecision;
            if (d == null) return;
            _applying = d.Settlement;
            try
            {
                var x = d.Settlement;
                var win = ____chosenOutcome as SettlementClaimantDecision.ClanAsDecisionOutcome;
                if (x == null || x.Town == null || win == null || win.Clan == null) return;
                var k = d.Kingdom;
                var king = k != null ? k.RulingClan : null;
                int day = (int)Now;
                bool after = AfterCapture(x);
                var cap = after ? CaptureOf(x) : null;
                var conq = after ? x.Town.LastCapturedBy : null;
                // kandydaci (prefiks BK) - ilu, ilu w pulapie
                int nCand = 0, nIn = 0; bool conqCand = false, conqIn = false;
                try
                {
                    foreach (var co in d.DetermineInitialCandidates())
                    {
                        var cc = co as SettlementClaimantDecision.ClanAsDecisionOutcome;
                        if (cc == null || cc.Clan == null) continue;
                        nCand++;
                        var ce = Eval(x, cc.Clan);
                        if (!ce.Over && !ce.Courtier) nIn++;
                        if (cc.Clan == conq) { conqCand = true; conqIn = !ce.Over; }
                    }
                }
                catch (Exception e) { Stumble("ApplyPrefix(kandydaci)", e); }
                var outs = new List<SettlementClaimantDecision.ClanAsDecisionOutcome>();
                if (____possibleOutcomes != null) foreach (var o in ____possibleOutcomes) { var co = o as SettlementClaimantDecision.ClanAsDecisionOutcome; if (co != null && co.Clan != null) outs.Add(co); }
                outs.Sort((a, b) => b.InitialMerit.CompareTo(a.InitialMerit));
                var top = outs.Count > 0 ? outs[0] : null;
                var wcl = Eval(x, win.Clan);
                float kingShare = -1f;
                if (king != null) { var kc = Eval(x, king); kingShare = k != null ? CrownShareOf(k, kc.W, x.Town) : -1f; }
                string chooser;
                if (____chooser == Clan.PlayerClan) chooser = _abstain.TryGetValue(__instance, out _) ? "gracz-krol (Abstain - wybor rady)" : "gracz-krol";
                else if (k != null && k == Clan.PlayerClan.Kingdom)
                    chooser = ____hasPlayerVoted ? "rada (krol AI, gracz glosowal)" : (____ignorePlayerSupport ? "rada (krol AI, bez gracza po 48 h)" : "rada (krol AI, gracz nie glosowal)");
                else chooser = "rada (krol AI)";
                string kind = d.ClanToExclude != null ? "aneksja (bez rodu " + d.ClanToExclude.Name + ")"
                    : after ? "po zdobyciu" : (x.Town.IsOwnerUnassigned ? "po zmianie pana bez szturmu" : "inna");
                Change ch;
                string when = cap != null ? " (zdobyte dnia " + (int)cap.Day + ", " + (int)Math.Round(Now - cap.Day) + " dob temu)"
                    : (after ? " (dzien zdobycia nieznany - brak zapisu szturmu)" : (_lastChange.TryGetValue(x, out ch) ? " (zmiana " + ch.Detail + " dnia " + (int)ch.Day + ")" : ""));

                var sb = new StringBuilder(1024);
                sb.Append("Nadanie lenna (N): dzien ").Append(day).Append(" | ").Append(x.Name).Append(" [").Append(x.StringId).Append("] (")
                  .Append(x.IsTown ? "miasto, 2 pkt" : "zamek, 1 pkt").Append(") | regula N: ").Append(On ? "tak" : "nie")
                  .Append(" | krolestwo ").Append(k != null ? k.Name.ToString() : "-").Append(", krol ").Append(king != null ? king.Name.ToString() : "-")
                  .Append(" (domena ").Append(kingShare >= 0f ? ((int)Math.Round(kingShare * 100f)).ToString(Inv) + "%" : "-").Append(")")
                  .Append(" | decyzja: ").Append(kind).Append(when).Append(", wybral: ").Append(chooser);
                sb.Append(" | zdobyl ");
                if (conq != null) sb.Append(conq.Name).Append(" (").Append(cap != null && cap.Hero != null ? cap.Hero.Name.ToString() : "-").Append(")");
                else sb.Append("-");
                sb.Append(", w szturmie: ");
                if (!after) sb.Append("-");
                else if (cap == null) sb.Append("brak");
                else
                {
                    var others = cap.Clans.Where(c => c != conq).Select(c => c.Name.ToString()).ToList();
                    sb.Append(others.Count > 0 ? string.Join(", ", others) : "tylko zdobywca");
                }
                sb.Append(" | kandydatow ").Append(nCand).Append(", w pulapie ").Append(nIn).Append(" | ");
                for (int i = 0; i < outs.Count; i++)
                {
                    var o = outs[i];
                    var cl = Eval(x, o.Clan);
                    if (i > 0) sb.Append(" ; ");
                    sb.Append(i + 1).Append(". ").Append(o.Clan.Name).Append(' ').Append(F(o.InitialMerit)).Append(" (S ").Append(F(cl.S)).Append(")")
                      .Append(" [t").Append(cl.Tier).Append(' ').Append(Holdings(cl)).Append(", przydzial ").Append(cl.P).Append(", pulap ").Append(cl.Cap)
                      .Append("; ").Append(Parts(cl)).Append("; x").Append(F2(cl.Factor));
                    if (cl.S > 0f && On)
                    {
                        float r = (o.InitialMerit - (o.InitialMerit >= 11f ? Tie(x, o.Clan, d, 99f) : 0f)) / cl.S;
                        if (r > 1.5f) sb.Append("; ROT x").Append(((int)Math.Round(r)).ToString(Inv));
                    }
                    int paid = 0;
                    try { paid = Paid(d, o, o == win); } catch (Exception e) { Stumble("ApplyPrefix(wplyw)", e); }
                    sb.Append("; BK domena ").Append(BkDemesne(o.Clan)).Append("] glosy ").Append(Points(o)).Append(" (rodow ").Append(o.SupporterList != null ? o.SupporterList.Count : 0)
                      .Append(", wplyw ").Append(paid).Append(")");
                }
                bool first = top != null && top == win;
                float ratio = top != null && top.InitialMerit > 0f ? win.InitialMerit / top.InitialMerit : (first ? 1f : 0f);
                bool anyIn = nIn > 0;
                sb.Append(" | zwyciezca ").Append(win.Clan.Name).Append(" (1. w ocenie: ").Append(first ? "tak" : "nie").Append(", ocena ").Append(F2(ratio)).Append(" x 1.")
                  .Append(", ponad pulapem: ").Append(wcl.Over ? "tak" : "nie").Append(", w pulapie byl ktos: ").Append(anyIn ? "tak" : "nie")
                  .Append(", rod krola: ").Append(win.Clan == king ? "tak" : "nie").Append(", gracz: ").Append(win.Clan == Clan.PlayerClan ? "tak" : "nie").Append(") ->");

                var tail = new StringBuilder(256);
                tail.Append(" | zdobywca: ");
                if (conq == null) tail.Append("-");
                else tail.Append(conq.Name).Append(" (rod krola: ").Append(conq == king ? "tak" : "nie").Append(", kandydat: ").Append(conqCand ? "tak" : "nie")
                         .Append(", w pulapie: ").Append(conqCand ? (conqIn ? "tak" : "nie") : "-").Append(", dostal: ").Append(conq == win.Clan ? "tak" : "nie").Append(")");
                tail.Append(" | gracz: ");
                string pl = "-";
                if (____chooser == Clan.PlayerClan) pl = "krol, wybral " + win.Clan.Name;
                else
                {
                    foreach (var o in outs)
                        if (o.SupporterList != null)
                            foreach (var s in o.SupporterList)
                                if (s != null && s.Clan == Clan.PlayerClan) pl = "glos na " + o.Clan.Name + " (" + s.SupportWeight + ")";
                }
                tail.Append(pl);
                if (wcl.Over) { _dOver++; if (anyIn) { _dOverWithIn++; _sOverWithIn++; } }
                __state = new GrantState { X = x, Winner = win.Clan, K = k, Cap = wcl.Cap, Head = sb.ToString(), Tail = tail.ToString() };
            }
            catch (Exception e) { Stumble("ApplyPrefix", e); __state = null; }
        }

        public static void ApplyPostfix(object __state)
        {
            _applying = null;
            var st = __state as GrantState;
            if (st == null) return;
            try
            {
                int towns = 0, castles = 0;
                foreach (var f in st.Winner.Fiefs) { if (f == null) continue; if (f.IsTown) towns++; else if (f.IsCastle) castles++; }
                bool got = st.X.OwnerClan == st.Winner;
                if (got) _granted[st.X] = new Grant { Day = Now, Clan = st.Winner, Kingdom = st.K };
                Log.Info(st.Head + " " + towns + "m/" + castles + "z = " + (2 * towns + castles) + " pkt (pulap " + st.Cap + ")" + (got ? "" : " - NIE DOSTAL (pan po nadaniu: " + (st.X.OwnerClan != null ? st.X.OwnerClan.Name.ToString() : "-") + ")") + st.Tail);
                _dLines++; _sLines++;
            }
            catch (Exception e) { Stumble("ApplyPostfix", e); }
        }

        // ------------------------------------------------------------ zakladanie latek (ArmouryBehavior.OnSessionLaunched)
        private static void Hook(MethodBase m, string what, HarmonyMethod pre, HarmonyMethod post, ref bool wired)
        {
            if (m == null) { _missing.Add(what + " (brak metody)"); return; }
            try { _harmony.Patch(m, prefix: pre, postfix: post); wired = true; }
            catch (Exception e) { _missing.Add(what + " (blad: " + e.Message + ")"); Stumble("Patch(" + what + ")", e); }
        }

        private static HarmonyMethod H(string name, int priority) { return new HarmonyMethod(typeof(FiefGrants), name) { priority = priority }; }

        internal static void EnsureHooks()
        {
            if (!_hooksTried && _harmony != null && Campaign.Current != null)
            {
                _hooksTried = true;
                try { Wire(); } catch (Exception e) { _missing.Add("blad: " + e.Message); Stumble("EnsureHooks", e); }
            }
            StartLine();
        }

        private static void Wire()
        {
            BkResolve();
            var args = new[] { typeof(Settlement), typeof(Hero), typeof(bool) };
            // H1: model czynny (BKROTPatch podmienia model BK w OnGameStart - BKROTPatch.cs:187), a dla pewnosci takze BKROTDiplomacyModel i BKDiplomacyModel
            // wedlug nazwy (gdyby ktos podmienil model po starcie sesji); metoda wolana wirtualnie - odpowiada ta z typu czynnego
            object model = null;
            try { var cfg = Cfg(); model = cfg != null && _pDipModel != null ? _pDipModel.GetValue(cfg, null) : null; } catch (Exception e) { Stumble("Wire(model BK)", e); }
            var done = new List<string>();
            var targets = new List<MethodInfo>();
            if (model != null)
            {
                _modelType = model.GetType().FullName;
                var m = PatchTarget.OfActive(model, "CalculateHeroFiefScore", args, "nadania lenn: ocena");   // HT: klasa deklarujaca
                if (m != null) targets.Add(m);
                else _missing.Add("ocena: " + _modelType + ".CalculateHeroFiefScore (brak metody)");
            }
            else _missing.Add("ocena: model dyplomacji BK (brak BK albo BannerKingsConfig.Instance)");
            foreach (var tn in new[] { "BKROTPatch.Models.BKROTDiplomacyModel", "BannerKings.Models.Vanilla.BKDiplomacyModel" })
            {
                var t = AccessTools.TypeByName(tn);
                var dm = t != null ? AccessTools.DeclaredMethod(t, "CalculateHeroFiefScore", args) : null;
                if (dm != null && !targets.Any(t0 => t0.MethodHandle == dm.MethodHandle)) targets.Add(dm);
            }
            foreach (var m in targets)
            {
                bool w = false;
                Hook(m, "ocena (" + m.DeclaringType.FullName + ")", H(nameof(ScorePrefix), Priority.First), null, ref w);
                if (w) { _wScore = true; done.Add(m.DeclaringType.FullName); }
            }
            _scoreWhere = done.Count > 0 ? string.Join(" + ", done.ToArray()) : "-";
            // H2, H2b, H3, H4, H6, H6b
            _mMerit = AccessTools.DeclaredMethod(typeof(SettlementClaimantDecision), "CalculateMeritOfOutcome", new[] { typeof(DecisionOutcome) });
            Hook(_mMerit, "remis (CalculateMeritOfOutcome)", null, H(nameof(MeritPostfix), Priority.Last), ref _wMerit);
            Hook(AccessTools.DeclaredMethod(typeof(KingdomDecision), "NarrowDownCandidates"), "M_max (NarrowDownCandidates)", null, H(nameof(NarrowPostfix), Priority.Last), ref _wNarrow);
            _mVote = AccessTools.DeclaredMethod(typeof(SettlementClaimantDecision), "DetermineSupport", new[] { typeof(Clan), typeof(DecisionOutcome) });
            Hook(_mVote, "glos (DetermineSupport)", null, H(nameof(SupportPostfix), Priority.Last), ref _wVote);
            _mCancelInternal = AccessTools.Method(typeof(KingdomDecision), "ShouldBeCancelledInternal");
            Hook(AccessTools.DeclaredMethod(typeof(KingdomDecision), "ShouldBeCancelled"), "anulowanie (ShouldBeCancelled)", H(nameof(CancelPrefix), Priority.Normal), null, ref _wCancel);
            Hook(AccessTools.DeclaredMethod(typeof(KingdomElection), "ApplyChosenOutcome"), "linia nadania (ApplyChosenOutcome)",
                  H(nameof(ApplyPrefix), Priority.Normal), H(nameof(ApplyPostfix), Priority.Normal), ref _wApply);
            Hook(AccessTools.DeclaredMethod(typeof(KingdomElection), "OnPlayerAbstainedAsRuler"), "Abstain gracza-krola", H(nameof(AbstainPrefix), Priority.Normal), null, ref _wAbstain);
        }

        private static string TypeOf(Patch p) { return p != null && p.PatchMethod != null && p.PatchMethod.DeclaringType != null ? p.PatchMethod.DeclaringType.FullName : "?"; }

        /// <summary>Kto jeszcze ma latki na metodzie: postfiksy przed nami i po nas (kolejnosc Harmony: priorytet malejaco, potem kolejnosc zalozenia).</summary>
        private static void Around(MethodBase m, out string before, out string after)
        {
            before = after = "-";
            if (m == null || _harmony == null) return;
            var info = Harmony.GetPatchInfo(m);
            if (info == null) return;
            var ours = info.Postfixes.FirstOrDefault(p => p.owner == _harmony.Id && p.PatchMethod != null && p.PatchMethod.DeclaringType == typeof(FiefGrants));
            if (ours == null) return;
            var b = info.Postfixes.Where(p => p != ours && (p.priority > ours.priority || (p.priority == ours.priority && p.index < ours.index))).Select(p => p.owner).Distinct().ToList();
            var a = info.Postfixes.Where(p => p != ours && (p.priority < ours.priority || (p.priority == ours.priority && p.index > ours.index))).Select(p => p.owner + " (" + TypeOf(p) + ")").Distinct().ToList();
            if (b.Count > 0) before = string.Join(", ", b.ToArray());
            if (a.Count > 0) after = string.Join(", ", a.ToArray());
        }

        private static void StartLine()
        {
            try
            {
                string bkPre = "-", rotPost = "nie ma", voteBefore = "-", voteAfter = "-", meritBefore = "-", meritAfter = "-";
                try
                {
                    var mi = _mMerit != null ? Harmony.GetPatchInfo(_mMerit) : null;
                    if (mi != null)
                    {
                        bkPre = mi.Prefixes.Any(p => TypeOf(p) == "BannerKings.Patches.Government.FiefOwnerPatches") ? "jest" : "NIE MA";
                        rotPost = mi.Postfixes.Any(p => TypeOf(p) == "ROT.HarmonyPatches.Core.KingdomDecisionPatches") ? "jest" : "nie ma";
                    }
                    Around(_mMerit, out meritBefore, out meritAfter);
                    Around(_mVote, out voteBefore, out voteAfter);
                }
                catch (Exception e) { Stumble("StartLine(latki)", e); }
                var sb = new StringBuilder(768);
                sb.Append("Nadania lenn (N): ").Append(On ? "CZYNNE" : "WYLACZONE (FairFiefGrants false - jak 7bc5dd0b: ocena z modelu BK, glos BK, anulowanie gry; tylko log)")
                  .Append(" | ocena: ").Append(_wScore ? "prefiks na " + _scoreWhere + ".CalculateHeroFiefScore" : "BRAK").Append(" (model BK: ").Append(_modelType).Append(")")
                  .Append(" | lista: prefiks BK FiefOwnerPatches ").Append(bkPre).Append(" + postfiks ROT KP x999 (").Append(rotPost).Append(") + nasz remis ").Append(_wMerit ? "tak" : "BRAK")
                  .Append(" (po nas: ").Append(meritAfter).Append("), M_max: ").Append(_wNarrow ? "postfiks NarrowDownCandidates" : "BRAK")
                  .Append(" | glos: postfiks DetermineSupport ").Append(_wVote ? "ostatni: " + (voteAfter == "-" ? "tak" : "NIE") : "BRAK").Append(" (przed nami: ").Append(voteBefore).Append("; po nas: ").Append(voteAfter).Append(")")
                  .Append(" | anulowanie przez krola: ").Append(_wCancel ? (On ? "wylaczone (N5)" : "jak w grze (klucz wylaczony)") : "BRAK latki")
                  .Append(" | linia nadania: ").Append(_wApply ? "tak" : "BRAK").Append(", Abstain: ").Append(_wAbstain ? "tak" : "BRAK")
                  .Append(" | BK: tytuly ").Append(_bkTitleOk ? "tak" : "NIE").Append(", domena ").Append(_bkDemOk ? "tak" : "NIE").Append(", stronnictwa ").Append(_mGetGroup != null ? "tak" : "NIE")
                  .Append(" | P = tier - 1, pulap P + ").Append(CapExtra).Append(", Z ").Append(ConquerorBonus).Append(", U ").Append(ParticipantBonus).Append(", B ").Append(LandlessBonus)
                  .Append(", J ").Append(DeJureBonus).Append(", K ").Append(CrownBonus).Append(" (< ").Append((int)(CrownShareMax * 100)).Append("%)")
                  .Append(" | BRAK: ").Append(_missing.Count > 0 ? string.Join("; ", _missing.ToArray()) : "-");
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("StartLine", e); }
        }

        // ------------------------------------------------------------ linia doby (gdy cos sie dzialo albo co 28 dob)
        internal static void Daily()
        {
            try
            {
                double now = Now;
                int day = (int)now;
                foreach (var s in _captures.Where(p => now - p.Value.Day > CaptureMemoryDays).Select(p => p.Key).ToList()) _captures.Remove(s);
                foreach (var s in _granted.Where(p => now - p.Value.Day > GrantWatchDays).Select(p => p.Key).ToList()) _granted.Remove(s);
                foreach (var s in _lastChange.Where(p => now - p.Value.Day > GrantWatchDays).Select(p => p.Key).ToList()) _lastChange.Remove(s);
                bool any = _dConcluded > 0 || _dLines > 0 || _dCancelled > 0 || _dMoved > 0 || _dStumbles > 0 || _dOver > 0;
                if (any || day % 28 == 0)
                {
                    Log.Info("Nadania lenn (N) doba " + day + ": FairFiefGrants " + (On ? "tak" : "nie")
                             + " | rozstrzygniete dzis " + _dConcluded + " (od startu sesji " + _sConcluded + "), z linia " + _dLines + " (od startu " + _sLines + ")"
                             + " | anulowane \"Owner of X\" " + _dCancelled + " (od startu " + _sCancelled + ")"
                             + " | zwyciezca ponad pulapem " + _dOver + " (przy kims w pulapie " + _dOverWithIn + ", od startu " + _sOverWithIn + ")"
                             + " | zmiany pana nadanych w " + GrantWatchDays + " dob " + _dMoved + " (od startu " + _sMoved + ")"
                             + " | szturmy zapisane " + _dCaptures + ", w pamieci " + _captures.Count + ", nadane w obserwacji " + _granted.Count
                             + " | glos bez M_max " + _dNoMax + " | potkniecia " + _dStumbles + " (od startu " + _sStumbles + ")");
                }
                _dConcluded = _dLines = _dCancelled = _dOver = _dOverWithIn = _dMoved = _dCaptures = _dNoMax = _dStumbles = 0;
            }
            catch (Exception e) { Stumble("Daily", e); }
        }
    }
}
