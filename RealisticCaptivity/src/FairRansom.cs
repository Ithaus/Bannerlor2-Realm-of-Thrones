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
                if (_vanillaOnly) return;                                     // liczymy cene gry (do potracenia nadwyzki)
                // 178 (Armoury, projekt etapu 2 krok D): okup wedlug majatku - cena z Armoury (sprzedaje gracz: gotowka, ktora rod jenca odda; trzyma gracz: pelna
                // cena); rod jenca rozlicza Armoury (gotowka w nicosc - rownowazy zloto gry, reszta dlug w ksiedze 168) - tu bez potracen
                // tylko lordowie, ktorych trzyma albo sprzedaje gracz (jak RC) - okupy miedzy AI, lapowki w lochach itd. bez zmian (Armoury ma wlasny przeplyw AI-AI)
                if (h != null && h != Hero.MainHero && h.Clan != null && h.Clan != Clan.PlayerClan && !h.Clan.IsBanditFaction && h.IsLord && h.CompanionOf == null
                    && ((seller != null && seller == Hero.MainHero) || HeldByPlayer(h)))
                {
                    int arm = ArmouryBridge.LordPrice(h, seller, result);
                    if (arm >= 0) { result = arm; return; }
                }
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
                // AUDYT 04.10 (B4): posrednik w karczmie placi z niczego (SellPrisonersAction -> GiveGoldAction(null, ...)).
                // Nasza nadwyzka ponad cene gry to pieniadze RODU jenca: najwyzej tyle, ile ma glowa rodu,
                // a przy faktycznej sprzedazy (w srodku SellPrisonersAction) zdejmowane z jej kiesy.
                var payerHero = clan.Leader;
                long extra = Math.Max(0L, price - vanilla);
                if (sellerIsPlayer && payerHero != null) extra = Math.Min(extra, Math.Max(0, payerHero.Gold));   // glowa rodu w niewoli placi ze swojej kiesy
                else if (sellerIsPlayer) extra = 0;
                if (extra > 0) result = (int)Math.Min(int.MaxValue, vanilla + extra);
                if (sellerIsPlayer && _inSale && extra > 0 && payerHero != null)
                {
                    payerHero.ChangeHeroGold(-(int)extra);
                    Log.Info("Okup lorda (posrednik): rod " + clan.Name + " placi " + extra + " ponad cene gry " + vanilla + " za " + h.Name + ".");
                }

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

        // flaga: jestesmy w srodku faktycznej sprzedazy jencow (nie w podgladzie ekranu)
        [ThreadStatic] internal static bool _inSale;
        public static void SalePrefix() { _inSale = true; }
        public static Exception SaleFinalizer(Exception __exception) { _inSale = false; return __exception; }
        [ThreadStatic] internal static bool _vanillaOnly;

        /// <summary>Ekran druzyny (ApplyByPartyScreen, applyConsequences=false): zloto placi PartyScreenLogic wedle
        /// naszej wyceny, a tu cena nie jest liczona - wiec nadwyzke ponad cene gry zdejmujemy z kiesy rodu tutaj.
        /// Sciezka posrednika (applyConsequences=true) potraca juz w LordPrice - tu jej nie ruszamy (bez podwojnego pobrania).</summary>
        public static void SalePostfix(TaleWorlds.CampaignSystem.Party.PartyBase __0, TaleWorlds.CampaignSystem.Roster.TroopRoster __2, bool __3)
        {
            _inSale = false;
            try
            {
                if (__3 || __0 != TaleWorlds.CampaignSystem.Party.PartyBase.MainParty || __2 == null) return;
                if (ArmouryBridge.Active) return;   // 178: ekran druzyny rozlicza Armoury (Ransom178.SalePostfix)
                var model = Campaign.Current.Models.RansomValueCalculationModel;
                foreach (var el in __2.GetTroopRoster())
                {
                    var ch = el.Character;
                    if (ch == null || !ch.IsHero || ch.HeroObject == null || ch.HeroObject == Hero.MainHero) continue;
                    var h = ch.HeroObject;
                    var payer = h.Clan != null ? h.Clan.Leader : null;
                    if (payer == null) continue;
                    int vanilla, ours;
                    _vanillaOnly = true;
                    try { vanilla = model.PrisonerRansomValue(ch, Hero.MainHero); } finally { _vanillaOnly = false; }
                    ours = model.PrisonerRansomValue(ch, Hero.MainHero);
                    int extra = Math.Min(ours - vanilla, Math.Max(0, payer.Gold));
                    if (extra <= 0) continue;
                    payer.ChangeHeroGold(-extra);
                    Log.Info("Okup lorda (ekran druzyny): rod " + h.Clan.Name + " placi " + extra + " ponad cene gry " + vanilla + " za " + h.Name + ".");
                }
            }
            catch (Exception e) { Log.Error("FairRansom.SalePostfix", e); }
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
                try
                {
                    var sell = AccessTools.Method(typeof(TaleWorlds.CampaignSystem.Actions.SellPrisonersAction), "ApplyInternal");
                    if (sell != null) harmony.Patch(sell, prefix: new HarmonyMethod(typeof(FairRansomPatch), nameof(SalePrefix)), postfix: new HarmonyMethod(typeof(FairRansomPatch), nameof(SalePostfix)), finalizer: new HarmonyMethod(typeof(FairRansomPatch), nameof(SaleFinalizer)));
                }
                catch (Exception e) { Log.Error("FairRansom.SellHook", e); }
                Log.Info("FairRansom: cena szeregowego jenca ma podloge (broker rate) w " + done + " modelach.");
            }
            catch (Exception e) { Log.Error("FairRansom.ApplyAll", e); }
        }
    }
}
