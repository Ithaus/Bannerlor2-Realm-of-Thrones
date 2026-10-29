using System;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// 295 (Jeff 29.10 "napraw pozostale bledy"; ksiega ludzi E4/7: 4-6 ludzi na dobe "w nicosc ... inne" w 136a): ORSZAKI DWOROW BK z ludzi wsi.
    /// BannerKings: wies z budynkiem Dwor (Manor, poziom 1-3) ma orszak "Retinue from {wies}" (RetinueComponent, partia stoi we wsi, AI wylaczone). Co dobe
    /// BKSettlementBehavior.TickVillage wola RetinueComponent.DailyTick(poziom): ponizej celu poziom x 15 - AddMember(1) z szablonu kultury (czlowiek z niczego),
    /// powyzej - RemoveTroop(1) (czlowiek w nicosc); nowy orszak (CreateRetinue - po zniszczeniu albo przy pierwszym Dworze) rodzi sie z szablonu kultury
    /// (InitializeMobilePartyAtPosition, kilkadziesiat ludzi z niczego), potem co dobe -1 do celu. Uzgodnienie doby ksiegi (ReconcileRoster) nazywalo to tylko
    /// "z niczego - inne" i "w nicosc - inne" (diagnostyka P2-T4: "Retinue from Fozzo (inne, w osadzie) 1" co dobe).
    /// Zmiana (klucz BkRetinueFromVillage): prefiks/postfiks na DailyTick - zmiana skladu z tej doby (i ludzie nowego orszaku bez slownika ksiegi) ta sama droga co
    /// obroncy wioski pobocznej (m97, SideVillageVisit.Defenders): przyrost - DOM TEJ wsi (TakeLevyHere, MLevyMilitia, podloga 50 hearth), udzial do slownika
    /// (GiveNew); czego DOM nie da (podloga, kraina z hamulcem wolnych rak) - ten czlowiek zdjety z orszaku (nikt z niczego); ubytek - udzialy ze slownika do DOM
    /// (Take FMilitiaHome + ArriveAt MBackMilitia, jak milicja bez walki). Osada Strazy i Innych, partia w bitwie, ksiega bez poboru (LevyLive) - jak dotad.
    /// Cel orszaku (poziom x 15) i rytm +-1 na dobe - BK bez zmian. Potkniecia liczone per miejsce (Stumble), bez wylacznika.
    /// </summary>
    internal static partial class ArmsFromPeople
    {
        public sealed class RetState { public int Men0, Gap; }

        private static void WireRetinue(Harmony h, Func<string, HarmonyMethod> P)
        {
            System.Reflection.MethodBase m = null;
            try
            {
                var t = AccessTools.TypeByName("BannerKings.Components.RetinueComponent");
                m = t != null ? AccessTools.Method(t, "DailyTick", new[] { typeof(float) }) : null;
            }
            catch (Exception e) { m = null; Stumble("WireRetinue", e); }
            Wire(h, "orszaki dworow BK (RetinueComponent.DailyTick, 295)", m, P(nameof(RetinuePre)), P(nameof(RetinuePost)), null);
        }

        public static void RetinuePre(object __instance, out RetState __state)
        {
            __state = null;
            try
            {
                var s = S;
                if (s == null || !s.BkRetinueFromVillage || !PeopleBook.LevyLive) return;
                var pc = __instance as PartyComponent;
                var mp = pc != null ? pc.MobileParty : null;
                if (mp == null || mp.MemberRoster == null) return;
                var home = pc.HomeSettlement;
                if (home == null || !home.IsVillage || mp.MapEvent != null || WatchAt(home) || OthersAt(home)) { D.rtSkip++; return; }
                int men = Men(mp.MemberRoster);
                // ludzie bez slownika ksiegi (nowy orszak z szablonu kultury - CreateRetinue tuz przed ta doba) - pelni ludzie; ulamek zostaje uzgodnieniu doby
                int gap = (int)Math.Floor(men - PeopleBook.BookMen(mp.MemberRoster) + 1e-6);
                __state = new RetState { Men0 = men, Gap = Math.Max(0, gap) };
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
                var r = mp.MemberRoster;
                var day = D;
                day.rtCalls++;
                int net = __state.Gap + (Men(r) - __state.Men0);
                if (__state.Gap > 0) day.rtNew += __state.Gap;
                if (net > 0)
                {
                    day.rtUp += net;
                    int keep = 0;
                    if (!FreeHands.Braked(home))
                    {
                        Settlement region;
                        double got = PeopleBook.TakeLevyHere(home, net, PeopleBook.MLevyMilitia, out region);
                        day.rt += got;                                        // KONTROLA E4/8: ruch MLevyMilitia z DOM = m1 + ... + ob + rt
                        keep = Math.Min(net, Ceil(got));
                        if (got > Eps) PeopleBook.GiveNew(r, region, got, PeopleBook.FMilitiaLevy);
                        if (keep - got > Eps) { PeopleBook.GiveNone(r, keep - got, PeopleBook.FArmsFloor); day.rtFrac += keep - got; }
                    }
                    int strip = net - keep;
                    if (strip > 0)
                    {
                        int st = StripMost(r, strip);
                        day.rtStrip += st;
                        if (strip - st > 0) PeopleBook.GiveNone(r, strip - st, PeopleBook.FArmsFloor);
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
            catch (Exception e) { Stumble("RetinuePost", e); }
        }

        /// <summary>Linia doby "Orszaki dworow BK (295)" (zaraz po "Wojsko z ludzi (E4/8)").</summary>
        private static string RetinueLine(int day, Day d, Settings s)
        {
            var sb = new StringBuilder(400);
            sb.Append("Orszaki dworow BK (295): dzien ").Append(day).Append(" | klucz ").Append(s != null && s.BkRetinueFromVillage ? "TAK" : "NIE")
              .Append(", ksiega z poborem ").Append(PeopleBook.LevyLive ? "TAK" : "NIE")
              .Append(" | wywolan ").Append(d.rtCalls).Append(" (pominiete: bitwa, Straz, Inni, nie wies ").Append(d.rtSkip).Append(")")
              .Append(" | przyrost BK +").Append(d.rtUp).Append(" (w tym nowe orszaki z szablonu ").Append(d.rtNew).Append(") = z DOM wsi ").Append(F1(d.rt))
              .Append(" + ulamki bez DOM ").Append(F1(d.rtFrac)).Append(" + zdjeci (DOM na podlodze albo bez wolnych rak) ").Append(d.rtStrip)
              .Append(" | ubytek BK -").Append(d.rtHome).Append(" do domu (ta wies ").Append(F1(d.rtH1)).Append(", inne regiony ").Append(F1(d.rtH2))
              .Append(", bez pochodzenia ").Append(F1(d.rtH3)).Append(", na Mur ").Append(F1(d.rtH4)).Append(")")
              .Append(" | potkniecia RetinuePre ").Append(StumblesAt("RetinuePre")).Append(", RetinuePost ").Append(StumblesAt("RetinuePost")).Append('.');
            return sb.ToString();
        }

        private static int StumblesAt(string where) { int v; return _stumbleAt.TryGetValue(where, out v) ? v : 0; }
    }
}
