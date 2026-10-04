using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;

namespace RealisticCaptivity
{
    /// <summary>
    /// UCZCIWY HANDLARZ OKUPOW. "Choose the prisoners to be ransomed" placi
    /// przez RansomValueCalculationModel, a BannerKings podmienia tam wycene
    /// szeregowych jencow na CENE NIEWOLNIKA w danym miescie (polityka
    /// "Enslavement" - domyslna). Miasto zalane niewolnikami placi ZERO,
    /// wiec Jeff oddawal jencow za darmo i zadne zloto nie przychodzilo.
    /// Postfix po wszystkich modelach: szeregowy jeniec nigdy nie schodzi
    /// ponizej starej stawki posrednika (cwierc kosztu rekrutacji).
    /// Bohaterowie (lordowie) - osobno, w LordPrice (okup wedle pozycji).
    /// </summary>
    internal static class FairRansomPatch
    {
        internal static void Postfix(CharacterObject prisoner, Hero sellerHero, ref int __result)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || prisoner == null) return;
                if (prisoner.IsHero) { LordPrice(prisoner.HeroObject, sellerHero, ref __result); return; }
                if (!s.PrisonerSaleFloor) return;

                int recruit = Campaign.Current.Models.PartyWageModel
                    .GetTroopRecruitmentCost(prisoner, null).RoundedResultNumber;
                int floor = (int)(recruit * 0.25f * Math.Max(0f, s.PrisonerSaleFloorFactor));
                if (floor < 1) floor = 1;
                if (__result < floor) __result = floor;

                // GEOGRAFIA HANDLU ZYWYM TOWAREM (Jeff 31.08: "robimy jencow").
                // W Westeros niewolnictwo jest ZAKAZANE - jenca kupi tylko paser
                // za ulamek ceny; w Essos (za Waskim Morzem) targi niewolnikow
                // placa pelna stawke. Granica po mapie: X > 770 = Essos
                // (Sunspear 689, Braavos 857 - miedzy nimi Waskie Morze).
                // Tylko sprzedaz GRACZA w osadzie; wyceny AI w tle nietkniete.
                if (s.PrisonerGeoSale && TaleWorlds.CampaignSystem.Settlements.Settlement.CurrentSettlement != null
                    && (sellerHero == null || sellerHero == Hero.MainHero))
                {
                    var st = TaleWorlds.CampaignSystem.Settlements.Settlement.CurrentSettlement;
                    bool essos = st.GetPosition2D.X > 770f;
                    if (!essos)
                    {
                        int cut = (int)(__result * Math.Max(0, Math.Min(100, s.WesterosFencePercent)) / 100f);
                        __result = Math.Max(1, cut);
                    }
                }
            }
            catch { }
        }

        // ===== OKUP WEDLE POZYCJI (Jeff 04.10: "okup za lordow ma byc uzalezniony
        // od pozycji, teraz to smieszne kwoty, 3k zlota - buty kosztuja wiecej").
        // Vanilla (DefaultRansomValueCalculationModel) liczy bohatera jako koszt
        // rekrutacji + (tier rodu + 2) * 200 (x2.5 glowa rodu, x6 krol) + sqrt(zlota) * 6,
        // razy ulamek wielkosci krolestwa - kilka tysiecy, gdy helm t6 kosztuje 24 tys.
        // Ta sama wycena placi: kurier rodu (SetPrisonerFreeBarterable, bez sellerHero),
        // handlarz okupow w karczmie (SellPrisonersAction, sellerHero = gracz), ekran
        // druzyny i barter. Podnosimy WYLACZNIE lordow, ktorych trzyma rod gracza albo
        // ktorych sprzedaje gracz - okupy miedzy rodami AI, lapowki przy odbijaniu
        // wiezniow (DefaultPrisonBreakModel) i okup samego gracza zostaja vanilla.
        // Nigdy nie schodzimy ponizej wyceny vanilli (max).
        private static readonly System.Collections.Generic.Dictionary<Hero, int> _loggedDay =
            new System.Collections.Generic.Dictionary<Hero, int>();

        internal static void LordPrice(Hero h, Hero seller, ref int result)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.LordRansomByRank || h == null || h == Hero.MainHero) return;
                var clan = h.Clan;
                if (clan == null || clan == Clan.PlayerClan || clan.IsBanditFaction) return;
                if (!h.IsLord || h.CompanionOf != null) return;           // towarzysze: wycena BK (x0.3) bez zmian
                bool sellerIsPlayer = seller != null && seller == Hero.MainHero;
                if (!sellerIsPlayer && !HeldByPlayer(h)) return;

                string rank;
                int baseV;
                var mf = h.MapFaction;
                if (mf != null && mf.IsKingdomFaction && mf.Leader == h) { rank = "krol"; baseV = s.LordRansomKing; }
                else if (clan.Leader == h) { rank = "glowa rodu"; baseV = s.LordRansomClanLeader; }
                else { rank = "lord"; baseV = s.LordRansomLord; }

                float tierF = 1f + Math.Max(0f, s.LordRansomPerClanTier) * Math.Max(0, clan.Tier);
                int towns = 0, castles = 0;
                if (rank != "lord")
                {
                    foreach (var f in clan.Fiefs)
                    {
                        if (f == null) continue;
                        if (f.IsCastle) castles++; else if (f.IsTown) towns++;
                    }
                }
                long price = (long)(Math.Max(0, baseV) * tierF)
                             + (long)towns * Math.Max(0, s.LordRansomPerTown)
                             + (long)castles * Math.Max(0, s.LordRansomPerCastle);
                if (price > int.MaxValue) price = int.MaxValue;
                int vanilla = result;
                if (price > result) result = (int)price;

                // log raz na dobe na jenca - wycene wola ekran druzyny przy kazdym ruchu
                int day = (int)CampaignTime.Now.ToDays;
                int last;
                if (!_loggedDay.TryGetValue(h, out last) || last != day)
                {
                    _loggedDay[h] = day;
                    var payer = clan.Leader;
                    Log.Info("Okup lorda: " + h.Name + " (" + rank + ", rod " + clan.Name + " tier " + clan.Tier
                             + ", miast " + towns + ", zamkow " + castles + ") vanilla " + vanilla + " -> " + result
                             + " | glowa rodu " + (payer != null ? payer.Name + " ma " + payer.Gold : "?") + " zlota"
                             + " | " + (sellerIsPlayer ? "sprzedaje gracz" : "trzyma rod gracza")
                             + " (kurier przychodzi tylko, gdy placacy ma okup + 1000).");
                }
            }
            catch (Exception e) { Log.Error("FairRansom.LordPrice", e); }
        }

        /// <summary>Czy jenca trzyma rod gracza: partia gracza, partia jego rodu albo osada rodu.</summary>
        private static bool HeldByPlayer(Hero h)
        {
            try
            {
                var pb = h.PartyBelongedToAsPrisoner;
                if (pb == null) return false;
                if (pb == TaleWorlds.CampaignSystem.Party.PartyBase.MainParty) return true;
                if (pb.IsMobile && pb.MobileParty != null && pb.MobileParty.ActualClan == Clan.PlayerClan) return true;
                if (pb.IsSettlement && pb.Settlement != null && pb.Settlement.OwnerClan == Clan.PlayerClan) return true;
            }
            catch { }
            return false;
        }

        internal static void ApplyAll(Harmony harmony)
        {
            try
            {
                var post = new HarmonyMethod(typeof(FairRansomPatch).GetMethod(
                    "Postfix", BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public))
                { priority = Priority.Last };

                int done = 0;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type[] types;
                    try { types = asm.GetTypes(); }
                    catch (ReflectionTypeLoadException rtle) { types = rtle.Types; }
                    catch { continue; }

                    foreach (var t in types)
                    {
                        if (t == null || t.IsAbstract || !typeof(RansomValueCalculationModel).IsAssignableFrom(t)) continue;
                        try
                        {
                            var m = t.GetMethod("PrisonerRansomValue", BindingFlags.Public | BindingFlags.NonPublic |
                                                                       BindingFlags.Instance | BindingFlags.DeclaredOnly);
                            if (m == null || m.IsAbstract) continue;
                            harmony.Patch(m, postfix: post);
                            done++;
                        }
                        catch (Exception e) { Log.Error("FairRansom.Patch(" + t.Name + ")", e); }
                    }
                }
                Log.Info("FairRansom: cena szeregowego jenca ma podloge (broker rate) w " + done + " modelach.");
            }
            catch (Exception e) { Log.Error("FairRansom.ApplyAll", e); }
        }
    }
}
