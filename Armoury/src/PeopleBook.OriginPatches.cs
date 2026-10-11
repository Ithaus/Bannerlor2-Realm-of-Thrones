using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// E4/7 (164b-1): latki ksiegi pochodzenia - punkt zdjecia rekruta (BK DeduceManpower w oknie werbunku AI i gracza, A5, karczma gracza) i okna przenosin
    /// (zaloga, ekran druzyny, nowa partia rodu, rozwiazanie partii, patrole BK, jency po bitwie, sprzedaz i przekazanie jencow, wcielenie jencow AI).
    /// Wszystkie: void/finalizer, bez zmiany wyniku i bez zapisu pola gry - licza tylko udzialy. Wpiete zawsze (U26), dzialaja przy OriginLive.
    /// </summary>
    internal static partial class PeopleBook
    {
        // ================================================================ okna werbunku (3.1, 3.2)
        private const int WinAi = 1, WinPlayer = 2;
        [ThreadStatic] private static MobileParty _winParty;
        [ThreadStatic] private static int _winKind;
        [ThreadStatic] private static bool _selfDeduce;
        public struct WinState { public MobileParty Party; public int Kind; }

        // RecruitmentCampaignBehavior.ApplyInternal(MobileParty side1Party, Settlement settlement, Hero individual, CharacterObject troop, int number, int bitCode, RecruitingDetail detail)
        public static void RecruitWinPre(MobileParty __0, out WinState __state)
        {
            __state = new WinState { Party = _winParty, Kind = _winKind };
            _winParty = __0; _winKind = WinAi;
        }

        public static Exception RecruitWinFin(Exception __exception, MobileParty __0, int __4, object __6, WinState __state)
        {
            try
            {
                if (__exception == null && OriginLive && __0 != null && __4 > 0 && __6 != null && __6.ToString() == "VolunteerFromMap")
                    GiveNone(__0.MemberRoster, __4, FNoneMap);       // ochotnik z mapy: BK nie wola DeduceManpower (osada null) - bez pochodzenia
            }
            catch (Exception e) { OStumble("RecruitWinFin", e); }
            _winParty = __state.Party; _winKind = __state.Kind;
            return __exception;
        }

        // RecruitmentVM.OnDone (gracz) - prefiks Priority.First, PRZED prefiksem BK (ktory wola DeduceManpower i zwraca false)
        public static void PlayerWinPre(out WinState __state)
        {
            __state = new WinState { Party = _winParty, Kind = _winKind };
            _winParty = MobileParty.MainParty; _winKind = WinPlayer;
        }

        public static Exception PlayerWinFin(Exception __exception, WinState __state)
        {
            _winParty = __state.Party; _winKind = __state.Kind;
            return __exception;
        }

        // ================================================================ BK (refleksja): PopulationData.Settlement, GetPopData, MilitaryData, DeduceManpower
        private static PropertyInfo _pdSettlement, _pdMilitary;
        private static MethodInfo _getPopData, _deduce;
        private static object _popMgr;
        private static bool _bkTried;

        private static bool BkResolve()
        {
            if (_getPopData != null && _popMgr != null) return true;
            if (_bkTried) return false;
            _bkTried = true;
            try
            {
                var cfgT = AccessTools.TypeByName("BannerKings.BannerKingsConfig");
                var inst = cfgT != null ? AccessTools.Property(cfgT, "Instance")?.GetValue(null, null) : null;
                _popMgr = inst != null ? (AccessTools.Field(cfgT, "PopulationManager")?.GetValue(inst) ?? AccessTools.Property(cfgT, "PopulationManager")?.GetValue(inst, null)) : null;
                if (_popMgr == null) return false;
                _getPopData = AccessTools.Method(_popMgr.GetType(), "GetPopData", new[] { typeof(Settlement) });
                return _getPopData != null;
            }
            catch (Exception e) { OStumble("BkResolve", e); return false; }
        }

        private static Settlement PopSettlement(object data)
        {
            if (data == null) return null;
            if (_pdSettlement == null) _pdSettlement = AccessTools.Property(data.GetType(), "Settlement");
            return _pdSettlement != null ? _pdSettlement.GetValue(data, null) as Settlement : null;
        }

        /// <summary>Postfiks BK MilitaryData.DeduceManpower(PopulationData, int, CharacterObject, Hero) - punkt zdjecia ochotnika (U7), wspolny dla AI i gracza.
        /// Nie zmienia BK (sklad i klasa zostaja - R0: BK steruje skladem, ksiega iloscia).</summary>
        public static void DeducePostfix(object __0, int __1, CharacterObject __2, Hero __3)
        {
            if (_selfDeduce || __1 <= 0 || !OriginLive) return;
            try
            {
                if (__2 != null && (__2.IsHero || IsWight(__2))) return;
                var st = PopSettlement(__0);
                MobileParty party = _winParty; bool player = _winKind == WinPlayer, noWin = false;
                if (party == null)
                {
                    var main = MobileParty.MainParty;
                    if (st != null && Settlement.CurrentSettlement == st && main != null && main.CurrentSettlement == st) { party = main; player = true; noWin = true; }
                }
                NoteDeduce(__1, party == null, noWin);
                if (party == null || st == null) return;
                var roster = party.MemberRoster;
                if (CrownGifts.IsWatchParty(party) || (st.Culture != null && st.Culture.StringId == "nightswatch")) { GiveNone(roster, __1, FNoneWatch); return; }
                if (!LevyLive) { GiveNone(roster, __1, FNoneOff); return; }
                int cause = player ? FLevyPlayer : st.IsVillage ? FLevyVillage : st.IsCastle ? FLevyCastle : (__3 == null ? FTavernAi : FLevyTown);
                // recenzja E4/7 R1-3: najemnik z karczmy (BK bez notabla) w krainie bez wolnych rak - obcy (gra odnawia najemnikow z niczego), nie z DOM tej krainy;
                // hamulec u notabla (Levy) dziala tylko na puste miejsce - bez tego karczma dalej brala ludzi krainy z hamulcem (decyzja Jeffa 11.10)
                if (cause == FTavernAi && FreeHands.Braked(st)) { GiveNone(roster, __1, FNoneBrake); return; }
                Settlement region;
                double got = TakeLevy(st, __1, MLevyVillage, out region);
                if (got > Eps) GiveNew(roster, region, got, cause);
                if (__1 - got > Eps) GiveNone(roster, __1 - got, FNoneFloor);
            }
            catch (Exception e) { OStumble("DeducePostfix", e); }
        }

        // ================================================================ A5: autowerbunek zalogi (3.4) - gra bierze sloty notabli bez zdarzenia i bez BK
        public sealed class A5State { public Town T; public List<KeyValuePair<Hero, CharacterObject[]>> Snap; }

        public static void A5Pre(Town __0, out A5State __state)
        {
            __state = null;
            if (!OriginLive || __0 == null || __0.Settlement == null) return;
            try
            {
                var snap = new List<KeyValuePair<Hero, CharacterObject[]>>();
                Action<Settlement> add = st =>
                {
                    if (st == null || st.Notables == null) return;
                    foreach (var h in st.Notables) if (h != null && h.VolunteerTypes != null) snap.Add(new KeyValuePair<Hero, CharacterObject[]>(h, (CharacterObject[])h.VolunteerTypes.Clone()));
                };
                add(__0.Settlement);
                if (__0.Villages != null) foreach (var v in __0.Villages) add(v != null ? v.Settlement : null);
                __state = new A5State { T = __0, Snap = snap };
            }
            catch (Exception e) { __state = null; OStumble("A5Pre", e); }
        }

        public static void A5Post(A5State __state)
        {
            if (__state == null || !OriginLive) return;
            try
            {
                var gar = __state.T.GarrisonParty;
                if (gar == null) return;
                bool watch = CrownGifts.IsWatchParty(gar);
                bool bk = Settings.Current != null && Settings.Current.GarrisonLevyFromBk;
                foreach (var kv in __state.Snap)
                {
                    var h = kv.Key; var before = kv.Value; var now = h.VolunteerTypes;
                    if (now == null) continue;
                    for (int i = 0; i < before.Length && i < now.Length; i++)
                    {
                        var troop = before[i];
                        if (troop == null || now[i] != null || IsWight(troop)) continue;
                        var st = h.CurrentSettlement ?? __state.T.Settlement;
                        if (bk) DeduceBk(st, troop, h);                      // klasa BK (P: A5 zdejmuje tez BK DeduceManpower) - bez naszego postfiksu (flaga)
                        if (watch) { GiveNone(gar.MemberRoster, 1, FNoneWatch); continue; }
                        if (!LevyLive) { GiveNone(gar.MemberRoster, 1, FNoneOff); continue; }
                        Settlement region;
                        double got = TakeLevy(st, 1, MLevyA5, out region);
                        if (got > Eps) GiveNew(gar.MemberRoster, region, got, FLevyA5);
                        if (1.0 - got > Eps) GiveNone(gar.MemberRoster, 1.0 - got, FNoneFloor);
                    }
                }
            }
            catch (Exception e) { OStumble("A5Post", e); }
        }

        private static int _a5Bk, _a5BkFail;

        /// <summary>A5 z BK: MilitaryData.DeduceManpower(popData, 1, troop, notabl) z flaga _selfDeduce (nasz postfiks nic nie robi - bez podwojnego zdjecia DOM).</summary>
        private static void DeduceBk(Settlement st, CharacterObject troop, Hero notable)
        {
            if (st == null || _deduce == null || !BkResolve()) return;
            try
            {
                var pd = _getPopData.Invoke(_popMgr, new object[] { st });
                if (pd == null) return;
                if (_pdMilitary == null) _pdMilitary = AccessTools.Property(pd.GetType(), "MilitaryData");
                var md = _pdMilitary != null ? _pdMilitary.GetValue(pd, null) : null;
                if (md == null) return;
                _selfDeduce = true;
                try { _deduce.Invoke(md, new object[] { pd, 1, troop, notable }); _a5Bk++; }
                finally { _selfDeduce = false; }
            }
            catch (Exception e) { _a5BkFail++; OStumble("DeduceBk", e); }
        }

        // ================================================================ karczma gracza (3.5) - wlasny dialog gry, bez BK
        public static void TavernPre(out int __state) { __state = OriginLive && MobileParty.MainParty != null ? Men(MobileParty.MainParty.MemberRoster) : -1; }

        public static void TavernPost(int __state)
        {
            if (__state < 0 || !OriginLive) return;
            try
            {
                var main = MobileParty.MainParty;
                int gain = Men(main.MemberRoster) - __state;
                if (gain <= 0) return;
                Settlement town = main.CurrentSettlement;
                try { if (town == null && PlayerEncounter.EncounterSettlement != null) town = PlayerEncounter.EncounterSettlement; } catch { }
                if (!LevyLive || town == null) { GiveNone(main.MemberRoster, gain, FNoneOff); return; }
                if (FreeHands.Braked(town)) { GiveNone(main.MemberRoster, gain, FNoneBrake); return; }      // R1-3: kraina z hamulcem - najemnik obcy
                Settlement region;
                double got = TakeLevy(town, gain, MLevyVillage, out region);
                if (got > Eps) GiveNew(main.MemberRoster, region, got, FTavernPlayer);
                if (gain - got > Eps) GiveNone(main.MemberRoster, gain - got, FNoneFloor);
            }
            catch (Exception e) { OStumble("TavernPost", e); }
        }

        // ================================================================ partia <-> zaloga AI (4.1)
        public struct GarState { public int Mp, Gar; public bool On; }

        public static void GarPre(MobileParty __0, Settlement __1, out GarState __state)
        {
            __state = default(GarState);
            if (!OriginLive || __0 == null || __1 == null) return;
            try
            {
                var g = __1.Town != null ? __1.Town.GarrisonParty : null;
                __state = new GarState { On = true, Mp = Men(__0.MemberRoster), Gar = g != null ? Men(g.MemberRoster) : 0 };
            }
            catch (Exception e) { OStumble("GarPre", e); }
        }

        public static void LeavePost(MobileParty __0, Settlement __1, GarState __state)
        {
            if (!__state.On) return;
            try { var g = __1.Town != null ? __1.Town.GarrisonParty : null; if (g != null) Transfer(__0.MemberRoster, __state.Mp, g.MemberRoster, __state.Gar, FToGarrison); }
            catch (Exception e) { OStumble("LeavePost", e); }
        }

        public static void TakePost(MobileParty __0, Settlement __1, GarState __state)
        {
            if (!__state.On) return;
            try { var g = __1.Town != null ? __1.Town.GarrisonParty : null; if (g != null) Transfer(g.MemberRoster, __state.Gar, __0.MemberRoster, __state.Mp, FFromGarrison); }
            catch (Exception e) { OStumble("TakePost", e); }
        }

        // ================================================================ ekran druzyny gracza (4.2)
        public sealed class ScreenState { public TroopRoster LM, LP, RM, RP; public int LM0, LP0, RM0, RP0; public PartyBase L, R; }
        private static FieldInfo _fScreenInit;
        private static int _screenNoInit;

        /// <summary>PartyScreenLogic._initialData - kopie rosterow z otwarcia ekranu (albo z ostatniego Done). Rostery ekranu sa ZYWE (komendy ekranu zmieniaja
        /// partie od razu; Cancel cofa z _initialData), wiec stan "przed" jest tylko tam (wzor GarrisonArmory.ScreenDonePrefix).</summary>
        private static PartyScreenData ScreenInit(PartyScreenLogic l)
        {
            try { return _fScreenInit != null && l != null ? _fScreenInit.GetValue(l) as PartyScreenData : null; } catch { return null; }
        }

        /// <summary>
        /// Recenzja E4/7 R2-1: stan "przed" prawej strony (druzyna gracza) i zywej lewej (partia, zaloga, loch osady) z _initialData - w prefiksie DoneLogic
        /// zywe rostery maja juz stan PO zmianach (delty wychodzily 0 i pochodzenie przenoszonych ginelo w uzgodnieniu). "Donate troops/prisoners" (lewa strona
        /// to pusta kopia): zaloga i loch osady dostaja ludzi dopiero w procedurze zatwierdzenia (wewnatrz DoneLogic) - ich stan z chwili prefiksu jest stanem "przed".
        /// Bez _initialData - bez okna (roznice bierze uzgodnienie, nazwane wedlug rodzaju).
        /// </summary>
        public static void ScreenPre(PartyScreenLogic __instance, out ScreenState __state)
        {
            __state = null;
            WinRecruited = 0; WinReleased = 0;
            if (!OriginLive || __instance == null) return;
            try
            {
                var main = MobileParty.MainParty;
                if (main == null || __instance.RightOwnerParty != PartyBase.MainParty) return;
                var init = ScreenInit(__instance);
                if (init == null || init.RightMemberRoster == null || init.RightPrisonerRoster == null) { _screenNoInit++; return; }
                var left = __instance.LeftOwnerParty;
                TroopRoster lm = null, lp = null;
                int lm0 = 0, lp0 = 0;
                if (left != null)
                {
                    lm = left.MemberRoster; lp = left.PrisonRoster;
                    lm0 = Men(init.LeftMemberRoster); lp0 = Men(init.LeftPrisonerRoster);
                }
                else
                {
                    // "Donate troops" - lewa strona pusta, zaloga osady (wzor GarrisonArmory.ScreenDonePrefix); zaloga i loch zywe, ale zmieniane dopiero w zatwierdzeniu
                    var gsm = TaleWorlds.Core.Game.Current != null ? TaleWorlds.Core.Game.Current.GameStateManager : null;
                    var ps = gsm != null ? gsm.ActiveState as TaleWorlds.CampaignSystem.GameState.PartyState : null;
                    var cur = Settlement.CurrentSettlement;
                    if (ps != null && ps.IsDonating && cur != null && cur.Town != null && cur.Town.GarrisonParty != null) { lm = cur.Town.GarrisonParty.MemberRoster; left = cur.Town.GarrisonParty.Party; }
                    if (cur != null && cur.Party != null) lp = cur.Party.PrisonRoster;
                    lm0 = Men(lm); lp0 = Men(lp);
                }
                __state = new ScreenState
                {
                    L = left, R = PartyBase.MainParty, LM = lm, LP = lp, RM = main.MemberRoster, RP = main.PrisonRoster,
                    LM0 = lm0, LP0 = lp0, RM0 = Men(init.RightMemberRoster), RP0 = Men(init.RightPrisonerRoster)
                };
            }
            catch (Exception e) { __state = null; OStumble("ScreenPre", e); }
        }

        public static void ScreenPost(bool __result, ScreenState __state)
        {
            int rec = WinRecruited, rel = WinReleased;
            WinRecruited = 0; WinReleased = 0;
            if (!__result || __state == null || !OriginLive) return;
            try
            {
                var w = __state;
                // delty po poprawce o wcielonych (loch -> sklad gracza, zdarzenie w DoneLogic) i wypuszczonych (PrisonerLaw.OnReleased)
                int dLM = Men(w.LM) - w.LM0, dRM = Men(w.RM) - w.RM0 - rec, dLP = Men(w.LP) - w.LP0, dRP = Men(w.RP) - w.RP0 + rec + rel;
                // R1-5: pobor wymuszony gracza - ekran lupu (lewa strona bez wlasciciela); wzieci z niego dostaja region wsi, reszta - niewzieci
                if (_press != null) { int used = (int)Math.Round(PressTake(w.RM, w.LM == null ? dRM : 0)); dRM -= used; }
                if (w.LM != null)
                {
                    if (dLM > 0 && dRM < 0) Give(w.LM, Take(w.RM, Math.Min(dLM, -dRM), FScreen), FScreen);
                    else if (dRM > 0 && dLM < 0) Give(w.RM, Take(w.LM, Math.Min(dRM, -dLM), FScreen), FScreen);
                }
                if (w.LP != null)
                {
                    if (dLP > 0 && dRP < 0) Give(w.LP, Take(w.RP, Math.Min(dLP, -dRP), FScreen), FScreen);
                    else if (dRP > 0 && dLP < 0) Give(w.RP, Take(w.LP, Math.Min(dRP, -dLP), FScreen), FScreen);
                }
                // uzgodnienie tylko tych rosterow (roznica - nazwana wedlug rodzaju)
                ReconcileOne(w.RM); ReconcileOne(w.RP); ReconcileOne(w.LM); ReconcileOne(w.LP);
            }
            catch (Exception e) { OStumble("ScreenPost", e); }
        }

        private static void ReconcileOne(TroopRoster r)
        {
            if (r == null) return;
            PartyBase owner; int kind;
            var o = OrgOf(r, false, out owner, out kind);
            if (owner == null || kind < 0) return;
            bool prison = ReferenceEquals(r, owner.PrisonRoster);
            ReconcileRoster(owner, r, prison ? _pris : _mem, kind, owner.MobileParty);
        }

        /// <summary>CampaignEvents.OnMainPartyPrisonerRecruitedEvent (DoneLogic): wcieleni jency gracza - udzial z lochu do skladu.</summary>
        internal static void OnMainPartyPrisonerRecruited(FlattenedTroopRoster roster)
        {
            if (!OriginLive || roster == null) return;
            try
            {
                int n = 0;
                foreach (var el in roster) { var t = el.Troop; if (t != null && !t.IsHero && !IsWight(t)) n++; }
                if (n <= 0) return;
                var main = MobileParty.MainParty;
                Give(main.MemberRoster, Take(main.PrisonRoster, n, FPrisRecruit), FPrisRecruit);
                WinRecruited += n;
            }
            catch (Exception e) { OStumble("OnMainPartyPrisonerRecruited", e); }
        }

        // nowa partia rodu (Clan -> Create new party): ludzie przechodza w funkcji zamkniecia ekranu, PO DoneLogic
        public static void NewPartyPre(out int __state) { __state = OriginLive && MobileParty.MainParty != null ? Men(MobileParty.MainParty.MemberRoster) : -1; }

        public static void NewPartyPost(TroopRoster __1, bool __6, int __state)
        {
            if (__6 || __state < 0 || __1 == null || !OriginLive) return;
            try
            {
                Hero hero = null;
                for (int i = 0; i < __1.Count; i++) { var ch = __1.GetCharacterAtIndex(i); if (ch != null && ch.IsHero && ch.HeroObject != null) hero = ch.HeroObject; }
                var np = hero != null ? hero.PartyBelongedTo : null;
                var main = MobileParty.MainParty;
                if (np == null || np == main || np.MemberRoster == null) return;
                int lost = __state - Men(main.MemberRoster);
                if (lost > 0) Give(np.MemberRoster, Take(main.MemberRoster, lost, FNewParty), FNewParty);
            }
            catch (Exception e) { OStumble("NewPartyPost", e); }
        }

        // ================================================================ rozwiazanie partii (4.3) - zdarzenie gry przed zniszczeniem partii
        public struct DisbandState { public bool On; public int M, P, G; public MobileParty Gar; }

        public static void DisbandPre(MobileParty __0, Settlement __1, out DisbandState __state)
        {
            __state = default(DisbandState);
            if (!OriginLive || __0 == null) return;
            try
            {
                if (KindOf(__0) < 0) return;           // tabor/lodz (R18 - PeopleBook.OnPartyDestroyed), konwoj BK, Inni - bez slownika skladu; bez drugiego powrotu
                var g = __1 != null && __1.Town != null ? __1.Town.GarrisonParty : null;
                __state = new DisbandState { On = true, M = Men(__0.MemberRoster), P = Men(__0.PrisonRoster), Gar = g, G = g != null ? Men(g.MemberRoster) : 0 };
            }
            catch (Exception e) { OStumble("DisbandPre", e); }
        }

        public static void DisbandPost(MobileParty __0, Settlement __1, DisbandState __state)
        {
            if (!__state.On || __1 == null || __0 == null) return;
            try
            {
                var g = __1.Town != null ? __1.Town.GarrisonParty : null;
                double moved = 0;
                if (g != null) moved = Transfer(__0.MemberRoster, __state.M, g.MemberRoster, __state.Gar == g ? __state.G : 0, FDisbandGarr);   // zaloga zalozona w metodzie - od 0
                // gra: do warowni swojej frakcji - sklad do zalogi (wyzej); inaczej sklad czyszczony (warownia obca) albo znika z partia (wies: milicja +0.5 z niczego - E4/8).
                // Rozwiazana partia -> DOM regionu pochodzenia (R6)
                double gone = __state.M - moved;                       // wszyscy, ktorzy nie przeszli do zalogi (wyczyszczeni teraz albo znikna przy zniszczeniu)
                if (gone > Eps)
                {
                    var org = Take(__0.MemberRoster, gone, FDisband);
                    HomeOrNote(org, gone, MBackDisband, -1);
                }
                // loch we wsi wyczyszczony (P2: "jency w nicosc - rozwiazane we wsi"); w warowni - okno sprzedazy (SellPrisonersAction)
                double pSum = OrgSum(__0.PrisonRoster);
                int pNow = Men(__0.PrisonRoster);
                if (pSum > pNow + Eps) Lost(Take(__0.PrisonRoster, pSum - pNow, FDisband));
            }
            catch (Exception e) { OStumble("DisbandPost", e); }
        }

        private static double OrgSum(TroopRoster r)
        {
            PartyBase owner; int kind;
            var o = OrgOf(r, false, out owner, out kind);
            return o != null ? o.Sum : 0.0;
        }

        // ================================================================ patrole BK (4.4)
        public static void PatrolOutPre(Settlement __0, out int __state)
        {
            __state = -1;
            try { if (OriginLive && __0 != null && __0.Town != null && __0.Town.GarrisonParty != null) __state = Men(__0.Town.GarrisonParty.MemberRoster); }
            catch (Exception e) { OStumble("PatrolOutPre", e); }
        }

        public static void PatrolOutPost(Settlement __0, MobileParty __result, int __state)
        {
            if (__state < 0 || __result == null || !OriginLive) return;
            try { Transfer(__0.Town.GarrisonParty.MemberRoster, __state, __result.MemberRoster, 0, FPatrolOut); }
            catch (Exception e) { OStumble("PatrolOutPost", e); }
        }

        /// <summary>BK BKPartyBehavior.AddPatrolBehavior(party, target, data) - prefiks Priority.Low (po PrisonerLaw.PatrolPrefix): te same warunki co BK; sklad patrolu
        /// do zalogi (przed metoda - patrol znika w niej przez DestroyPartyAction), jency, ktorych PrisonerLaw nie odeslal - niewolnicy BK.</summary>
        public static void PatrolInPre(MobileParty __0, Settlement __1)
        {
            if (!OriginLive || __0 == null || __1 == null) return;
            try
            {
                if (!IsBkPatrol(__0) || __1.Town == null || __1 != __0.HomeSettlement || __1.Town.GarrisonParty == null) return;
                int m = Men(__0.MemberRoster);
                if (m > 0) Give(__1.Town.GarrisonParty.MemberRoster, Take(__0.MemberRoster, m, FPatrolIn), FPatrolIn);
                int p = Men(__0.PrisonRoster);
                if (p > 0) EnslaveShares(Take(__0.PrisonRoster, p, FPatrolPris));
            }
            catch (Exception e) { OStumble("PatrolInPre", e); }
        }

        // ================================================================ jency po bitwie (4.5) - finalizer Priority.Last (po OthersRise RescueFin i Measure40 W10)
        public sealed class BattleWin { public List<TroopRoster> Lose = new List<TroopRoster>(), Gain = new List<TroopRoster>(); public List<int> L0 = new List<int>(), G0 = new List<int>(); }

        private static BattleWin OpenBattle(MBReadOnlyList<MapEventParty> winners, MBReadOnlyList<MapEventParty> losers, bool prisoners)
        {
            if (!OriginLive || winners == null || losers == null) return null;
            var w = new BattleWin();
            foreach (var mep in losers)
            {
                if (mep == null || mep.Party == null) continue;
                var r = prisoners ? mep.Party.PrisonRoster : mep.Party.MemberRoster;
                if (r == null || w.Lose.Contains(r)) continue;
                w.Lose.Add(r); w.L0.Add(Men(r));
            }
            foreach (var mep in winners)
            {
                if (mep == null || mep.Party == null) continue;
                TroopRoster r = null;
                try { r = prisoners ? mep.RosterToReceiveLootMembers : mep.RosterToReceiveLootPrisoners; } catch { r = null; }
                if (r == null || w.Gain.Contains(r)) continue;
                w.Gain.Add(r); w.G0.Add(Men(r));
            }
            return w.Lose.Count > 0 ? w : null;
        }

        /// <summary>Przenosiny okna bitwy: ubytek pokonanych -> przyrost odbiorcow zwyciezcow (proporcjonalnie); zwraca udzialy bez odbiorcy (zdjete).</summary>
        private static Shares CloseBattle(BattleWin w, int cause)
        {
            var s = new Shares();
            int lost = 0;
            for (int i = 0; i < w.Lose.Count; i++)
            {
                int d = w.L0[i] - Men(w.Lose[i]);
                if (d > 0) { s.AddAll(Take(w.Lose[i], d, cause)); lost += d; }
            }
            if (lost <= 0) return s;
            var gains = new int[w.Gain.Count]; int got = 0;
            for (int j = 0; j < w.Gain.Count; j++) { gains[j] = Math.Max(0, Men(w.Gain[j]) - w.G0[j]); got += gains[j]; }
            for (int j = 0; j < w.Gain.Count; j++)
            {
                if (gains[j] <= 0) continue;
                double part = got <= lost ? gains[j] : (double)gains[j] * lost / got;
                Give(w.Gain[j], s.Cut(part), cause);
            }
            return s;
        }

        public static void CapturePre(MBReadOnlyList<MapEventParty> __0, MBReadOnlyList<MapEventParty> __1, out BattleWin __state)
        {
            __state = null;
            try { __state = OpenBattle(__0, __1, false); } catch (Exception e) { __state = null; OStumble("CapturePre", e); }
        }

        public static Exception CaptureFin(Exception __exception, BattleWin __state)
        {
            if (__state == null) return __exception;
            try
            {
                var rest = CloseBattle(__state, FCaptured);
                if (rest.Total > Eps) { _fOut[FCaptureLost] += rest.Total; HomeOrNote(rest, rest.Total, MBackFreed, -1); }   // bez odbiorcy (brak szans) - rozbity do domu (R6)
            }
            catch (Exception e) { OStumble("CaptureFin", e); }
            return __exception;
        }

        public static void LootPre(MBReadOnlyList<MapEventParty> __0, MBReadOnlyList<MapEventParty> __1, out BattleWin __state)
        {
            __state = null;
            OthersRise.LastRescuedKilled = 0;
            try { __state = OpenBattle(__0, __1, true); } catch (Exception e) { __state = null; OStumble("LootPre", e); }
        }

        public static Exception LootFin(Exception __exception, BattleWin __state)
        {
            long killed = OthersRise.LastRescuedKilled;
            OthersRise.LastRescuedKilled = 0;
            if (__state == null) return __exception;
            try
            {
                var rest = CloseBattle(__state, FLooted);
                if (rest.Total > Eps)
                {
                    // zywi zdjeci przez Innych (OthersRise RescueFin, E4/2 (f)) - polegli regionu pochodzenia, jedno DieShares (bez PeopleBook.Die drugi raz)
                    double dead = Math.Min(rest.Total, killed);
                    if (dead > Eps) DieShares(rest.Cut(dead), FDieOthers);
                    if (rest.Total > Eps) { _fOut[FLootLost] += rest.Total; HomeOrNote(rest, rest.Total, MBackFreed, -1); }
                }
            }
            catch (Exception e) { OStumble("LootFin", e); }
            return __exception;
        }

        // ================================================================ sprzedaz jencow (4.8) - okno sprzedajacego PRZED prefiksami BK (SendOffPrisoners -> PrisonerLaw.SendOffPrefix)
        [ThreadStatic] internal static PartyBase SaleSeller;
        [ThreadStatic] private static double _saleTaken;
        public struct SaleState { public PartyBase Prev; public double PrevTaken; public int P0; public bool On; }

        public static void SalePre(PartyBase __0, out SaleState __state)
        {
            __state = new SaleState { Prev = SaleSeller, PrevTaken = _saleTaken };
            SaleSeller = __0; _saleTaken = 0;
            try { if (OriginLive && __0 != null) { __state.P0 = Men(__0.PrisonRoster); __state.On = true; } } catch (Exception e) { OStumble("SalePre", e); }
        }

        public static Exception SaleFin(Exception __exception, PartyBase __0, SaleState __state)
        {
            try
            {
                if (__state.On && __0 != null && OriginLive)
                {
                    double rest = __state.P0 - Men(__0.PrisonRoster) - _saleTaken;
                    if (rest > Eps) EnslaveShares(Take(__0.PrisonRoster, rest, FSold));   // BK (niewolnicy/dzierzawcy osady) albo sprzedaz bez I1
                }
            }
            catch (Exception e) { OStumble("SaleFin", e); }
            SaleSeller = __state.Prev; _saleTaken = __state.PrevTaken;
            return __exception;
        }

        /// <summary>PrisonerLaw.SendOffPrefix: udzialy jencow ze sprzedazy (loch sprzedajacego z okna); poza oknem - "".</summary>
        internal static Shares SaleTake(int n, int cause)
        {
            if (n <= 0) return new Shares();
            _saleTaken += n;
            var seller = SaleSeller;
            return Take(seller != null ? seller.PrisonRoster : null, n, cause);
        }

        // ================================================================ przekazanie jenca, wcielenie jencow AI
        public static void TransferPrisonerPre(CharacterObject __0, PartyBase __1, PartyBase __2)
        {
            if (!OriginLive || __0 == null || __0.IsHero || __1 == null || __2 == null || IsWight(__0)) return;
            try { Give(__2.PrisonRoster, Take(__1.PrisonRoster, 1, FTransfer), FTransfer); }
            catch (Exception e) { OStumble("TransferPrisonerPre", e); }
        }

        public static void RecruitPrisonersAiPost(MobileParty __0, CharacterObject __1, int __2)
        {
            if (!OriginLive || __0 == null || __2 <= 0 || __1 == null || IsWight(__1)) return;
            try { Give(__0.MemberRoster, Take(__0.PrisonRoster, __2, FPrisRecruit), FPrisRecruit); }
            catch (Exception e) { OStumble("RecruitPrisonersAiPost", e); }
        }

        // ================================================================ wpiecie (SubModuleMain, po OthersRise.ApplyAll; latki wpiete zawsze - U26)
        internal static void ApplyOrigin(Harmony h)
        {
            var done = new List<string>(); var miss = new List<string>(); var owners = new List<string>();
            Func<MethodInfo, string, HarmonyMethod, HarmonyMethod, HarmonyMethod, bool> wire = (m, label, pre, post, fin) =>
            {
                try
                {
                    if (m == null) { miss.Add(label); return false; }
                    h.Patch(m, prefix: pre, postfix: post, finalizer: fin);
                    done.Add(label);
                    try
                    {
                        var info = Harmony.GetPatchInfo(m);
                        if (info != null)
                        {
                            var o = info.Owners.Where(x => x != h.Id).Distinct().ToList();
                            if (o.Count > 0) owners.Add(m.DeclaringType.Name + "." + m.Name + ": " + string.Join("/", o.ToArray()));
                        }
                    }
                    catch { }
                    return true;
                }
                catch (Exception e) { miss.Add(label + " (" + e.GetType().Name + ")"); return false; }
            };
            Func<string, HarmonyMethod> P = n => new HarmonyMethod(typeof(PeopleBook), n);
            Func<string, int, HarmonyMethod> PP = (n, prio) => new HarmonyMethod(typeof(PeopleBook), n) { priority = prio };

            // punkt zdjecia BK
            try
            {
                var mdT = AccessTools.TypeByName("BannerKings.Managers.Populations.MilitaryData");
                MethodInfo ded = null;
                if (mdT != null)
                    foreach (var m in mdT.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    {
                        if (m.Name != "DeduceManpower") continue;
                        var ps = m.GetParameters();
                        if (ps.Length == 4 && ps[1].ParameterType == typeof(int) && ps[2].ParameterType == typeof(CharacterObject) && ps[3].ParameterType == typeof(Hero)) { ded = m; break; }
                    }
                _deduce = ded;
                wire(ded, "punkt zdjecia BK DeduceManpower", null, P(nameof(DeducePostfix)), null);
            }
            catch (Exception e) { miss.Add("BK DeduceManpower (" + e.GetType().Name + ")"); }
            wire(AccessTools.Method(typeof(RecruitmentCampaignBehavior), "ApplyInternal"), "okno werbunku AI", PP(nameof(RecruitWinPre), Priority.First), null, P(nameof(RecruitWinFin)));
            var vmT = AccessTools.TypeByName("TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu.Recruitment.RecruitmentVM");
            wire(vmT != null ? AccessTools.Method(vmT, "OnDone") : null, "okno werbunku gracza (RecruitmentVM.OnDone)", PP(nameof(PlayerWinPre), Priority.First), null, P(nameof(PlayerWinFin)));
            wire(AccessTools.Method(typeof(GarrisonRecruitmentCampaignBehavior), "TickAutoRecruitmentGarrisonChange", new[] { typeof(Town) }), "autowerbunek zalogi (A5)", P(nameof(A5Pre)), P(nameof(A5Post)), null);
            int tav = 0;
            if (wire(AccessTools.Method(typeof(RecruitmentCampaignBehavior), "BuyMercenaries"), "karczma gracza (rozmowa)", P(nameof(TavernPre)), P(nameof(TavernPost)), null)) tav++;
            if (wire(AccessTools.Method(typeof(RecruitmentCampaignBehavior), "buy_mercenaries_on_consequence"), "karczma gracza (menu)", P(nameof(TavernPre)), P(nameof(TavernPost)), null)) tav++;
            // przenosiny
            wire(AccessTools.Method(typeof(GarrisonTroopsCampaignBehavior), "LeaveTroopsToGarrison", new[] { typeof(MobileParty), typeof(Settlement), typeof(int), typeof(bool) }), "partia -> zaloga", P(nameof(GarPre)), P(nameof(LeavePost)), null);
            wire(AccessTools.Method(typeof(GarrisonTroopsCampaignBehavior), "TakeTroopsFromGarrison", new[] { typeof(MobileParty), typeof(Settlement), typeof(int), typeof(bool) }), "zaloga -> partia", P(nameof(GarPre)), P(nameof(TakePost)), null);
            try { _fScreenInit = AccessTools.Field(typeof(PartyScreenLogic), "_initialData"); } catch { _fScreenInit = null; }
            if (_fScreenInit == null) miss.Add("ekran druzyny: PartyScreenLogic._initialData (stan przed - bez okna, roznice w uzgodnieniu)");
            wire(AccessTools.Method(typeof(PartyScreenLogic), "DoneLogic", new[] { typeof(bool) }), "ekran druzyny", PP(nameof(ScreenPre), Priority.First), P(nameof(ScreenPost)), null);
            wire(AccessTools.Method(typeof(Helpers.PartyScreenHelper), "OpenScreenAsCreateClanPartyForHeroPartyScreenClosed"), "nowa partia rodu", P(nameof(NewPartyPre)), P(nameof(NewPartyPost)), null);
            wire(AccessTools.Method(typeof(DisbandPartyCampaignBehavior), "OnPartyDisbanded", new[] { typeof(MobileParty), typeof(Settlement) }), "rozwiazanie partii", P(nameof(DisbandPre)), P(nameof(DisbandPost)), null);
            var gpcT = AccessTools.TypeByName("BannerKings.Components.GarrisonPartyComponent");
            wire(gpcT != null ? AccessTools.Method(gpcT, "CreateParty", new[] { typeof(Settlement) }) : null, "patrole BK - wyjscie", P(nameof(PatrolOutPre)), P(nameof(PatrolOutPost)), null);
            var bkpT = AccessTools.TypeByName("BannerKings.Behaviours.BKPartyBehavior");
            wire(bkpT != null ? AccessTools.Method(bkpT, "AddPatrolBehavior") : null, "patrole BK - powrot", PP(nameof(PatrolInPre), Priority.Low), null, null);
            wire(AccessTools.Method(typeof(MapEvent), "CaptureDefeatedPartyMembers"), "jency po bitwie - wzieci", PP(nameof(CapturePre), Priority.First), null, PP(nameof(CaptureFin), Priority.Last));
            wire(AccessTools.Method(typeof(MapEvent), "LootDefeatedPartyPrisoners"), "jency po bitwie - odbici", PP(nameof(LootPre), Priority.First), null, PP(nameof(LootFin), Priority.Last));
            wire(AccessTools.Method(typeof(SellPrisonersAction), "ApplyForAllPrisoners", new[] { typeof(PartyBase), typeof(PartyBase) }), "sprzedaz jencow (wszyscy)", PP(nameof(SalePre), Priority.First), null, P(nameof(SaleFin)));
            wire(AccessTools.Method(typeof(SellPrisonersAction), "ApplyForSelectedPrisoners", new[] { typeof(PartyBase), typeof(PartyBase), typeof(TroopRoster) }), "sprzedaz jencow (wybrani)", PP(nameof(SalePre), Priority.First), null, P(nameof(SaleFin)));
            wire(AccessTools.Method(typeof(TransferPrisonerAction), "ApplyInternal"), "przekazanie jenca", P(nameof(TransferPrisonerPre)), null, null);
            wire(AccessTools.Method(typeof(RecruitPrisonersCampaignBehavior), "RecruitPrisonersAi"), "wcielenie jencow AI", null, P(nameof(RecruitPrisonersAiPost)), null);
            if (FreeHands.ApplyFood(h)) done.Add("zywnosc wsi (rece)"); else miss.Add("zywnosc wsi (rece)");
            var s = Settings.Current;
            Log.Info("Pochodzenie (E4/7): wpiete " + done.Count + ": " + string.Join(", ", done.ToArray()) + " (karczma gracza " + tav + "/2); BRAK: " + (miss.Count > 0 ? string.Join(", ", miss.ToArray()) : "-")
                     + "; wlasciciele latek celow: " + (owners.Count > 0 ? string.Join("; ", owners.ToArray()) : "-")
                     + "; ustawienia startowe przed MCM (stan gry - linia dzienna): pochodzenie " + (s != null && s.PeopleOriginEnabled ? "TAK" : "NIE")
                     + ", pobor z wsi " + (s != null && s.LevyFromVillages ? "TAK" : "NIE") + ", A5 z BK " + (s != null && s.GarrisonLevyFromBk ? "TAK" : "NIE")
                     + ", hamulec " + (s != null && s.FreeHandsBrake ? "TAK" : "NIE") + ".");
        }
    }
}
