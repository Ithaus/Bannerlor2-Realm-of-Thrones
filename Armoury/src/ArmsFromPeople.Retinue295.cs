using System;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// 295 (Jeff 29.10 "napraw pozostale bledy"; ksiega ludzi E4/7: 4-6 ludzi na dobe "w nicosc ... inne" w 136a): ORSZAKI BK z ludzi osady.
    /// Trzy drogi BK (dekompilacja BannerKings.dll), w ktorych ludzie rodza sie z niczego albo znikaja, a uzgodnienie doby ksiegi (ReconcileRoster) nazywalo je tylko
    /// "z niczego" / "w nicosc":
    /// 1) DWOR wsi (Manor, poziom 1-3): orszak "Retinue from {wies}" (RetinueComponent, partia stoi we wsi, AI wylaczone). Co dobe BKSettlementBehavior.TickVillage
    ///    wola RetinueComponent.CreateRetinue (nowy orszak z DefaultPartyTemplate kultury - w ROT kingdom_hero_party_* 53-56 ludzi, staly sklad: skoki "z niczego -
    ///    inne" +52/+53 w 136a) i DailyTick(poziom): ponizej celu poziom x 15 - AddMember(1) z szablonu, powyzej - RemoveTroop(1) (czlowiek w nicosc co dobe).
    /// 2) MAJATEK BK (Estate): orszak "Estate Retinue from {wies}" (EstateComponent). Estate.Tick (co dobe z EstateData.Update): brak orszaku -
    ///    EstateComponent.CreateRetinue (MilitiaPartyTemplate kultury, w ROT 2 ludzi), inaczej +1 czlowiek (AddToCounts) ponizej MaxManpower.
    ///    Estate.TakeRetinue (BKGentryBehavior.SummonGentry): wszyscy ludzie orszaku do nowej partii pana (AddToCounts + RemoveIf) - bez przeniesienia udzialow.
    /// 3) ZAMEK z budynkiem Castle Retinue: BKSettlementBehavior.TickCastle - +1 EliteBasicTroop kultury do zalogi na dobe ponizej 20 x poziom (AddToCounts, z niczego).
    /// Zmiana (klucz BkRetinueFromVillage): przyrost tych drog ta sama droga co obroncy wioski pobocznej (m97, SideVillageVisit.Defenders): DOM osady orszaku
    /// (wies - TakeLevyHere, zamek - TakeLevy z wsi zamku; MLevyMilitia, podloga 50 hearth), udzial do slownika (GiveNew); czego DOM nie da (podloga, kraina
    /// z hamulcem wolnych rak) - ten czlowiek zdjety z orszaku (nikt z niczego); ubytek dworu - udzialy ze slownika do DOM (Take FMilitiaHome + ArriveAt
    /// MBackMilitia, jak milicja bez walki); TakeRetinue - przenosiny udzialow orszak -> partia pana (PeopleBook.Transfer, FNewParty).
    /// Osada Strazy i Innych, partia w bitwie, ksiega bez poboru (LevyLive) - jak dotad. Cele BK (poziom x 15, MaxManpower, 20 x poziom) i rytm +-1 na dobe - bez zmian.
    /// Potkniecia liczone per miejsce (Stumble), bez wylacznika.
    /// </summary>
    internal static partial class ArmsFromPeople
    {
        public sealed class RetState { public int Men0, Gap; public MobileParty Mp0; }
        public sealed class MoveState { public TroopRoster From, To; public int From0, To0; }
        public sealed class CastleState { public TroopRoster R; public CharacterObject Elite; public int N0; }

        private static MethodInfo _estRet;      // Estate.Retinue (getter) - BK Estate jest w zestawie bez referencji

        private static void WireRetinue(Harmony h, Func<string, HarmonyMethod> P)
        {
            MethodBase m = null, et = null, ek = null, ct = null;
            try
            {
                var t = AccessTools.TypeByName("BannerKings.Components.RetinueComponent");
                m = t != null ? AccessTools.Method(t, "DailyTick", new[] { typeof(float) }) : null;
            }
            catch (Exception e) { m = null; Stumble("WireRetinue", e); }
            Wire(h, "orszaki dworow BK (RetinueComponent.DailyTick, 295)", m, P(nameof(RetinuePre)), P(nameof(RetinuePost)), null);
            try
            {
                var est = AccessTools.TypeByName("BannerKings.Managers.Populations.Estates.Estate");
                _estRet = est != null ? AccessTools.PropertyGetter(est, "Retinue") : null;
                if (_estRet != null && _estRet.ReturnType == typeof(MobileParty))
                {
                    var pd = AccessTools.TypeByName("BannerKings.Managers.Populations.PopulationData");
                    et = pd != null ? AccessTools.Method(est, "Tick", new[] { pd }) : null;
                    ek = AccessTools.Method(est, "TakeRetinue", new[] { typeof(MobileParty) });
                }
                else _estRet = null;
                var bs = AccessTools.TypeByName("BannerKings.Behaviours.BKSettlementBehavior");
                ct = bs != null ? AccessTools.Method(bs, "TickCastle", new[] { typeof(Settlement) }) : null;
            }
            catch (Exception e) { et = ek = ct = null; Stumble("WireRetinue", e); }
            Wire(h, "orszaki majatkow BK (Estate.Tick, 295)", et, P(nameof(EstatePre)), P(nameof(EstatePost)), null);
            Wire(h, "orszak majatku do partii pana (Estate.TakeRetinue, 295)", ek, P(nameof(EstateTakePre)), P(nameof(EstateTakePost)), null);
            Wire(h, "orszak zamku BK (BKSettlementBehavior.TickCastle, 295)", ct, P(nameof(CastlePre)), P(nameof(CastlePost)), null);
        }

        private static bool RetOn { get { var s = S; return s != null && s.BkRetinueFromVillage && PeopleBook.LevyLive; } }

        /// <summary>Ludzie bez slownika ksiegi w tym rosterze (nowy orszak z szablonu przed uzgodnieniem doby) - pelni ludzie; ulamek zostaje uzgodnieniu doby.</summary>
        private static int BookGap(TroopRoster r, int men) { return Math.Max(0, (int)Math.Floor(men - PeopleBook.BookMen(r) + 1e-6)); }

        // ---------------------------------------------------------------- 1) dwor wsi (RetinueComponent.DailyTick)
        public static void RetinuePre(object __instance, out RetState __state)
        {
            __state = null;
            try
            {
                if (!RetOn) return;
                var pc = __instance as PartyComponent;
                var mp = pc != null ? pc.MobileParty : null;
                if (mp == null || mp.MemberRoster == null) return;
                var home = pc.HomeSettlement;
                if (home == null || !home.IsVillage || mp.MapEvent != null || WatchAt(home) || OthersAt(home)) { D.rtSkip++; return; }
                int men = Men(mp.MemberRoster);
                __state = new RetState { Men0 = men, Gap = BookGap(mp.MemberRoster, men) };
            }
            catch (Exception e) { __state = null; Stumble("RetinuePre", e); }
        }

        public static void RetinuePost(object __instance, RetState __state)
        {
            if (__state == null) return;
            try
            {
                var pc = __instance as PartyComponent;
                var mp = pc != null ? pc.MobileParty : null;
                var home = pc != null ? pc.HomeSettlement : null;
                if (mp == null || mp.MemberRoster == null || home == null) return;
                var day = D;
                day.rtCalls++;
                if (__state.Gap > 0) day.rtNew += __state.Gap;
                Settle(mp.MemberRoster, home, __state.Gap + (Men(mp.MemberRoster) - __state.Men0), null);
            }
            catch (Exception e) { Stumble("RetinuePost", e); }
        }

        // ---------------------------------------------------------------- 2) majatek BK (Estate.Tick, Estate.TakeRetinue)
        public static void EstatePre(object __instance, out RetState __state)
        {
            __state = null;
            try
            {
                if (!RetOn || _estRet == null) return;
                var mp = _estRet.Invoke(__instance, null) as MobileParty;
                var st = new RetState { Mp0 = mp };
                if (mp != null && mp.IsActive && mp.MemberRoster != null) { st.Men0 = Men(mp.MemberRoster); st.Gap = BookGap(mp.MemberRoster, st.Men0); }
                __state = st;
            }
            catch (Exception e) { __state = null; Stumble("EstatePre", e); }
        }

        public static void EstatePost(object __instance, RetState __state)
        {
            if (__state == null) return;
            try
            {
                var mp = _estRet.Invoke(__instance, null) as MobileParty;
                if (mp == null || !mp.IsActive || mp.MemberRoster == null) return;       // orszak zniszczony (BK trzyma martwa partie) - BK dopisuje do martwego rosteru
                var pc = mp.PartyComponent;
                var home = pc != null ? pc.HomeSettlement : null;
                var day = D;
                if (home == null || mp.MapEvent != null || WatchAt(home) || OthersAt(home)) { day.rtSkip++; return; }
                var r = mp.MemberRoster;
                int net, fresh;
                if (!ReferenceEquals(mp, __state.Mp0)) { fresh = BookGap(r, Men(r)); net = fresh; }        // nowy orszak (CreateRetinue w tym Tick)
                else { fresh = __state.Gap; net = __state.Gap + (Men(r) - __state.Men0); }
                day.esCalls++;
                if (fresh > 0) { day.rtNew += fresh; day.esNew += fresh; }
                if (net > 0) day.esUp += net;
                Settle(r, home, net, null);
            }
            catch (Exception e) { Stumble("EstatePost", e); }
        }

        public static void EstateTakePre(object __instance, MobileParty __0, out MoveState __state)
        {
            __state = null;
            try
            {
                var s = S;
                if (s == null || !s.BkRetinueFromVillage || !PeopleBook.OriginLive || _estRet == null || __0 == null || __0.MemberRoster == null) return;
                var mp = _estRet.Invoke(__instance, null) as MobileParty;
                if (mp == null || !mp.IsActive || mp.MemberRoster == null) return;
                __state = new MoveState { From = mp.MemberRoster, To = __0.MemberRoster, From0 = Men(mp.MemberRoster), To0 = Men(__0.MemberRoster) };
            }
            catch (Exception e) { __state = null; Stumble("EstateTake", e); }
        }

        public static void EstateTakePost(MoveState __state)
        {
            if (__state == null) return;
            try
            {
                var day = D;
                day.mvN++;
                int lost = __state.From0 - Men(__state.From);
                double moved = PeopleBook.Transfer(__state.From, __state.From0, __state.To, __state.To0, PeopleBook.FNewParty);
                day.mvMen += moved;
                if (lost - moved > Eps) day.mvLeft += lost - moved;
            }
            catch (Exception e) { Stumble("EstateTake", e); }
        }

        // ---------------------------------------------------------------- 3) zamek z Castle Retinue (BKSettlementBehavior.TickCastle)
        private static int CountOf(TroopRoster r, CharacterObject c) { int i = r.FindIndexOfTroop(c); return i >= 0 ? r.GetElementNumber(i) : 0; }

        public static void CastlePre(Settlement __0, out CastleState __state)
        {
            __state = null;
            try
            {
                if (!RetOn || __0 == null || !__0.IsCastle || __0.Town == null) return;
                var g = __0.Town.GarrisonParty;
                var elite = __0.Culture != null ? __0.Culture.EliteBasicTroop : null;
                if (g == null || g.MemberRoster == null || elite == null) return;
                if (g.MapEvent != null || WatchAt(__0) || OthersAt(__0)) return;      // bez licznika "pominiete" - zamek bez Castle Retinue tez tu przechodzi
                __state = new CastleState { R = g.MemberRoster, Elite = elite, N0 = CountOf(g.MemberRoster, elite) };
            }
            catch (Exception e) { __state = null; Stumble("CastlePre", e); }
        }

        public static void CastlePost(Settlement __0, CastleState __state)
        {
            if (__state == null) return;
            try
            {
                // jedyny przyrost EliteBasicTroop w TickCastle to AddToCounts Castle Retinue (HandleGarrison tylko wyprowadza patrol z zalogi)
                int d = CountOf(__state.R, __state.Elite) - __state.N0;
                if (d <= 0) return;
                D.csUp += d;
                Settle(__state.R, __0, d, __state.Elite);
            }
            catch (Exception e) { Stumble("CastlePost", e); }
        }

        // ---------------------------------------------------------------- wspolny rachunek
        /// <summary>net > 0: ludzie z DOM osady home (czego DOM nie da - zdjeci; only - zdejmij tylko ten typ); net &lt; 0: udzialy do domu.</summary>
        private static void Settle(TroopRoster r, Settlement home, int net, CharacterObject only)
        {
            var day = D;
            if (net > 0)
            {
                day.rtUp += net;
                int keep = 0;
                if (!FreeHands.Braked(home))
                {
                    Settlement region;
                    double got = home.IsVillage ? PeopleBook.TakeLevyHere(home, net, PeopleBook.MLevyMilitia, out region)
                                                : PeopleBook.TakeLevy(home, net, PeopleBook.MLevyMilitia, out region);
                    day.rt += got;                                        // KONTROLA E4/8: ruch MLevyMilitia z DOM = m1 + ... + ob + rt
                    keep = Math.Min(net, Ceil(got));
                    if (got > Eps) PeopleBook.GiveNew(r, region, got, PeopleBook.FMilitiaLevy);
                    if (keep - got > Eps) { PeopleBook.GiveNone(r, keep - got, PeopleBook.FArmsFloor); day.rtFrac += keep - got; }
                }
                int strip = net - keep;
                if (strip > 0)
                {
                    int st = only != null ? StripType(r, only, strip) : StripMost(r, strip);
                    day.rtStrip += st;
                    // nie do zdjecia (sami bohaterowie / wighty, typ ranny) - zostaje w rosterze z udzialem "" nazwanym (FArmsFloor), liczony w linii 295
                    if (strip - st > 0) { PeopleBook.GiveNone(r, strip - st, PeopleBook.FArmsFloor); day.rtFrac += strip - st; }
                }
            }
            else if (net < 0)
            {
                int n = -net;
                day.rtHome += n;
                var sh = PeopleBook.Take(r, n, PeopleBook.FMilitiaHome);
                var a = PeopleBook.ArriveAt(home, sh, PeopleBook.AService, PeopleBook.MBackMilitia, PeopleBook.MBackMilitiaNone, false);
                day.rtH1 += a.Here; day.rtH2 += a.Other; day.rtH3 += a.None; day.rtH4 += a.Wall;
            }
        }

        /// <summary>Zdejmuje do n zdrowych jednego typu (orszak zamku: wlasnie dopisany EliteBasicTroop).</summary>
        private static int StripType(TroopRoster r, CharacterObject c, int n)
        {
            int i = r.FindIndexOfTroop(c);
            if (i < 0 || n <= 0) return 0;
            int t = Math.Min(n, r.GetElementNumber(i) - r.GetElementWoundedNumber(i));
            if (t <= 0) return 0;
            r.AddToCounts(c, -t);
            return t;
        }

        /// <summary>Linia doby "Orszaki dworow BK (295)" (zaraz po "Wojsko z ludzi (E4/8)").</summary>
        private static string RetinueLine(int day, Day d, Settings s)
        {
            var sb = new StringBuilder(700);
            sb.Append("Orszaki dworow BK (295): dzien ").Append(day).Append(" | klucz ").Append(s != null && s.BkRetinueFromVillage ? "TAK" : "NIE")
              .Append(", ksiega z poborem ").Append(PeopleBook.LevyLive ? "TAK" : "NIE")
              .Append(" | wywolan ").Append(d.rtCalls).Append(" (pominiete: bitwa, Straz, Inni, nie wies ").Append(d.rtSkip).Append(")")
              .Append(" | przyrost BK +").Append(d.rtUp).Append(" (w tym nowe orszaki z szablonu ").Append(d.rtNew).Append(") = z DOM ").Append(F1(d.rt))
              .Append(" + bez DOM (ulamki, nie do zdjecia) ").Append(F1(d.rtFrac)).Append(" + zdjeci (DOM na podlodze albo bez wolnych rak) ").Append(d.rtStrip)
              .Append(" | ubytek BK -").Append(d.rtHome).Append(" do domu (ta wies ").Append(F1(d.rtH1)).Append(", inne regiony ").Append(F1(d.rtH2))
              .Append(", bez pochodzenia ").Append(F1(d.rtH3)).Append(", na Mur ").Append(F1(d.rtH4)).Append(")")
              .Append(" | majatki BK (Estate): wywolan ").Append(d.esCalls).Append(", przyrost +").Append(d.esUp).Append(" (nowe orszaki ").Append(d.esNew).Append(")")
              .Append(", do partii pana ").Append(d.mvN).Append(" razy, ludzi ").Append(F1(d.mvMen)).Append(" (bez przeniesienia ").Append(F1(d.mvLeft)).Append(")")
              .Append(" | zamki BK (Castle Retinue): przyrost +").Append(d.csUp)
              .Append(" | potkniecia RetinuePre ").Append(StumblesAt("RetinuePre")).Append(", RetinuePost ").Append(StumblesAt("RetinuePost"))
              .Append(", EstatePre ").Append(StumblesAt("EstatePre")).Append(", EstatePost ").Append(StumblesAt("EstatePost"))
              .Append(", EstateTake ").Append(StumblesAt("EstateTake")).Append(", CastlePre ").Append(StumblesAt("CastlePre")).Append(", CastlePost ").Append(StumblesAt("CastlePost")).Append('.');
            return sb.ToString();
        }

        private static int StumblesAt(string where) { int v; return _stumbleAt.TryGetValue(where, out v) ? v : 0; }
    }
}
