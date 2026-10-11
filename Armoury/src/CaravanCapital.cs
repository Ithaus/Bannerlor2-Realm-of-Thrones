using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using CW = Armoury.CirculationWindows;

namespace Armoury
{
    /// <summary>
    /// E3/7 (paczka 164a-6) - KAPITAL KARAWAN Z ZAPLATY I PARTIE ZNIKAJACE BEZ BITWY (projekt etapu 3, rozdz. 3; reguly R9, R10). Wylacznik CaravanCapitalReal.
    ///  1. KONWOJ DTE - KAPITAL 0: DTE CutTheirSupplyBehavior.SpawnReinforcementCaravan tworzy konwoj zaopatrzenia przez CreateCaravanParty (kapital
    ///     z niczego z CaravanModel.GetInitialTradeGold), a konwoj nie handluje (SetDoNotMakeNewDecisions). Kontekst (prefiks/finalizer) + postfiks
    ///     GetInitialTradeGold czynnego modelu (priorytet Low - PRZED odczytem okna ksiegi O17, Last) zwraca 0. Skutek uboczny (krytyka D6): rod dopelnia
    ///     kase partii do 5 000 w rozliczeniu (DefaultClanFinanceModel.AddPartyExpense) - wraca do glowy rodu przy usunieciu konwoju (pkt 4).
    ///  2. KUPNO KARAWANY PRZEZ LORDA BK (BKLordPropertyBehavior.OnSettlementEntered -> RunWeekly: lord.ChangeHeroGold(-cena) + CreateCaravanParty):
    ///     cena = spadek kiesy lorda miedzy wejsciem do metody BK a CreateCaravanParty; kasa karawany = min(kapital gry, cena), reszta ceny -> kasa
    ///     miasta zakupu (zwierzeta, wozy, najem). Ksiega: Z2 "kapital ... lordowie" i U6 "kupno karawan" zostaja jako pomiar okien, obok pozycje
    ///     "minus kapital z ceny kupna" (korekta zrodel) i "minus oddane: kupno karawan BK" (korekta ujsc) - netto 0.
    ///  3. GRACZ TWORZY KARAWANE (CaravanConversationsCampaignBehavior...form_a_caravan_accept_on_consequence: najpierw CreateCaravanParty, potem
    ///     gracz placi cene kasie miasta): kontekst - kapital 0, finalizer po zaplacie - przelew miasto -> karawana min(kapital gry, zaplacone)
    ///     (GiveGoldAction.ApplyForSettlementToParty - obcina do kasy miasta). Ta sama regula co BK.
    ///  4. PARTIE ZNIKAJACE BEZ BITWY (R10; krytyka W5): rozwiazane (MobilePartyDestroyed z niszczycielem null - nasz nasluch biegnie po 3.2 i 112,
    ///     przed ksiega obiegu), usuniete bez zniszczenia (OnPartyRemoved - DTE RemoveParty), przypadki "poza regula" 3.2 (BeatenPurse - zniszczone
    ///     przez kogos, kto ich nie pokonal): kiesa partii bez wodza -> glowa rodu wlasciciela (karawana notabla - sam notabl), brak - kasa
    ///     najblizszego miasta (GiveGoldAction - ksiega widzi przelew). Sakiewka ludzi partii usunietej (sierota) -> kasa najblizszego miasta;
    ///     rozwiazanej - jak dotad MenPurse.OnPartyDestroyed (najblizsze miasto). Tabory wsi - 112 (VillageTakings).
    ///  5. E5/A3 (164c): NOWA KARAWANA NOTABLA (bez kontekstu 1-3, po starcie gry, bez isInitialSpawn) - kapital z kiesy notabla ponad 4 500 i z nadwyzki
    ///     kasy miasta startu (NotableGold.CarOpen/CarCap/CarClose wolane z CreatePre, CapPostfix i CreateFin; wylacznik NotableCaravanCapitalReal).
    /// </summary>
    internal static class CaravanCapital
    {
        internal static bool On { get { var s = Settings.Current; return s != null && s.CaravanCapitalReal; } }
        private static bool Live { get { var c = Campaign.Current; return c != null && c.GameStarted; } }

        private const int KDte = 1, KBk = 2, KPlayer = 3;

        internal sealed class Ctx
        {
            public int Kind; public Hero Buyer; public Settlement Town; public int G0;
            public long Price = -1, K = -1, Cap = -1; public MobileParty Created; public bool Done;
        }

        private static readonly Ctx[] _stack = new Ctx[4];
        private static int _depth, _createDepth;
        private static bool _createSeen;

        private static long _dteCap, _bkPrice, _bkCap, _bkTown, _plK, _plPaid, _plMoved;
        private static int _dteN, _bkN, _plN, _noModel;
        private static readonly long[] _toHead = new long[3], _toTown = new long[3];
        private static readonly int[] _toHeadN = new int[3], _toTownN = new int[3];
        private static long _lost, _convoyBack, _orphanPurse;
        private static int _convoyBackN;
        private static readonly string[] RName = { "rozwiazane (niszczyciel null)", "usuniete bez zniszczenia (OnPartyRemoved)", "poza regula 3.2" };

        private static Ctx Top() { return _depth > 0 ? _stack[_depth - 1] : null; }

        private static bool Push(Ctx c)
        {
            if (_depth >= _stack.Length) return false;
            _stack[_depth++] = c; return true;
        }

        private static void Pop(Ctx c)
        {
            if (c == null || _depth == 0) return;
            if (ReferenceEquals(_stack[_depth - 1], c)) { _stack[--_depth] = null; return; }
            for (int i = _depth - 1; i >= 0; i--) if (ReferenceEquals(_stack[i], c)) { for (int j = i; j < _depth - 1; j++) _stack[j] = _stack[j + 1]; _stack[--_depth] = null; Seal31.Stumbles++; return; }
        }

        // ------------------------------------------------------------ konteksty
        public static void DtePre(out Ctx __state)
        {
            __state = null;
            try { if (On && Live) { var c = new Ctx { Kind = KDte }; if (Push(c)) __state = c; } }
            catch (Exception e) { __state = null; Seal31.Stumble("CaravanCapital.DtePre", e); }
        }

        public static void BkPre(MobileParty __0, Settlement __1, out Ctx __state)
        {
            __state = null;
            try
            {
                if (!On || !Live || __0 == null || !__0.IsLordParty || __0.LeaderHero == null || __0.LeaderHero == Hero.MainHero) return;
                var c = new Ctx { Kind = KBk, Buyer = __0.LeaderHero, Town = __1, G0 = __0.LeaderHero.Gold };
                if (Push(c)) __state = c;
            }
            catch (Exception e) { __state = null; Seal31.Stumble("CaravanCapital.BkPre", e); }
        }

        public static void PlayerPre(out Ctx __state)
        {
            __state = null;
            try
            {
                if (!On || !Live || Hero.MainHero == null) return;
                var c = new Ctx { Kind = KPlayer, Buyer = Hero.MainHero, Town = Settlement.CurrentSettlement, G0 = Hero.MainHero.Gold };
                if (Push(c)) __state = c;
            }
            catch (Exception e) { __state = null; Seal31.Stumble("CaravanCapital.PlayerPre", e); }
        }

        public static void CtxFin(Ctx __state)
        {
            if (__state == null) return;
            try
            {
                if (__state.Kind == KPlayer) PlayerSettle(__state);
            }
            catch (Exception e) { Seal31.Stumble("CaravanCapital.CtxFin", e); }
            finally { Pop(__state); }
        }

        /// <summary>Gracz zaplacil kasie miasta cene karawany - miasto oddaje karawanie kapital: min(kapital gry, zaplacone).</summary>
        private static void PlayerSettle(Ctx c)
        {
            if (c.Created == null || c.K < 0 || c.Town == null || c.Town.SettlementComponent == null) return;
            long paid = Math.Max(0L, (long)c.G0 - Hero.MainHero.Gold);
            long x = Math.Min(c.K, paid);
            _plN++; _plK += c.K; _plPaid += paid;
            if (x <= 0) return;
            int g0 = c.Created.PartyTradeGold;
            GiveGoldAction.ApplyForSettlementToParty(c.Town, c.Created.Party, (int)Math.Min(int.MaxValue, x), true);
            _plMoved += c.Created.PartyTradeGold - g0;
        }

        // ------------------------------------------------------------ CreateCaravanParty i kapital z modelu
        public struct CreateState { public bool PrevSeen; public bool On; public NotableGold.Car Nb; }   // Nb: E5/A3 (164c) kontekst karawany notabla

        public static void CreatePre(Hero __0, Settlement __1, bool __3, out CreateState __state)
        {
            __state = new CreateState { PrevSeen = _createSeen, On = true };
            _createDepth++; _createSeen = false;
            try
            {
                var c = Top();
                if (c != null && c.Kind == KBk && !c.Done && c.Price < 0 && ReferenceEquals(c.Buyer, __0)) c.Price = Math.Max(0L, (long)c.G0 - __0.Gold);
                if (c == null) __state.Nb = NotableGold.CarOpen(__0, __1, __3, _createDepth);   // E5/A3 (164c): karawana notabla bez kontekstu E3/7 - kapital z kiesy notabla i kasy miasta startu
            }
            catch (Exception e) { Seal31.Stumble("CaravanCapital.CreatePre", e); }
        }

        public static void CreateFin(Hero __0, MobileParty __result, CreateState __state)
        {
            if (!__state.On) return;
            _createDepth = Math.Max(0, _createDepth - 1); _createSeen = __state.PrevSeen;
            if (__state.Nb != null) NotableGold.CarClose(__state.Nb, __result);   // E5/A3 (164c): wlasny try
            try
            {
                var c = Top();
                if (c == null || c.Done) return;
                if (c.Kind == KPlayer && ReferenceEquals(c.Buyer, __0)) { c.Created = __result; c.Done = true; return; }
                if (c.Kind != KBk || !ReferenceEquals(c.Buyer, __0) || c.Price < 0) return;
                c.Done = true;
                if (c.Cap < 0) { _noModel++; return; }       // kapital nie przeszedl przez nasz postfiks modelu - jak dotad
                var town = c.Town;                                 // miasto zakupu (target BK)
                if ((town == null || town.Town == null) && __result != null) town = VillageTakings.NearestTown(__result);
                long toTown = c.Price - c.Cap;
                if (toTown > 0 && town != null && town.SettlementComponent != null)
                {
                    town.SettlementComponent.ChangeGold((int)Math.Min(int.MaxValue, toTown));
                    MoneyLedger.Note169(MoneyLedger.N169Other, town, (int)Math.Min(int.MaxValue, toTown));
                    _bkTown += toTown;
                }
                else if (toTown > 0) { _lost += toTown; toTown = 0; }
                Seal31.AddWorld(Seal31.KCapFromPrice, c.Cap);
                Seal31.AddWorld(Seal31.MLordBuyPaid, c.Cap + toTown);
                _bkN++; _bkPrice += c.Price; _bkCap += c.Cap;
            }
            catch (Exception e) { Seal31.Stumble("CaravanCapital.CreateFin", e); }
        }

        /// <summary>Postfiks GetInitialTradeGold czynnego CaravanModel (priorytet Low - przed oknem ksiegi O17): pierwszy wynik w CreateCaravanParty w kontekscie.</summary>
        public static void CapPostfix(ref int __result)
        {
            try
            {
                if (_createDepth == 0 || _createSeen) return;
                var c = Top();
                if (c == null) { if (NotableGold.CarCap(_createDepth, ref __result)) _createSeen = true; return; }   // E5/A3 (164c): karawana notabla (wlasny try)
                if (c.Done) return;
                _createSeen = true;
                if (c.Kind == KDte) { _dteN++; _dteCap += Math.Max(0, __result); __result = 0; }
                else if (c.Kind == KBk && c.Price >= 0) { c.K = Math.Max(0, __result); c.Cap = Math.Min(c.K, c.Price); __result = (int)c.Cap; }
                else if (c.Kind == KPlayer) { c.K = Math.Max(0, __result); __result = 0; }
            }
            catch (Exception e) { Seal31.Stumble("CaravanCapital.CapPostfix", e); }
        }

        // ------------------------------------------------------------ 4. partie znikajace bez bitwy
        /// <summary>Kiesa partii bez wodza znikajacej bez bitwy -> glowa rodu wlasciciela (karawana notabla - notabl), brak - kasa najblizszego miasta.
        /// kind: 0 rozwiazana (niszczyciel null), 1 usunieta bez zniszczenia, 2 poza regula 3.2.</summary>
        internal static void RoutePurse(MobileParty mp, int kind)
        {
            try
            {
                if (!On || !Live || mp == null || kind < 0 || kind > 2) return;
                if (mp.IsVillager || mp.IsMainParty || (mp.IsLordParty && mp.LeaderHero != null)) return;   // tabory - 112; kiesa wodza zostaje z wodzem
                int g = mp.PartyTradeGold;
                if (g <= 0) return;
                var owner = mp.Party != null ? mp.Party.Owner : null;
                Hero head = null;
                if (owner != null && owner.IsNotable) head = owner;
                else
                {
                    var clan = mp.ActualClan ?? (owner != null ? owner.Clan : null);
                    if (clan != null && !clan.IsBanditFaction) head = clan.Leader;
                }
                int moved;
                if (head != null && head.IsAlive && !CW.OutOfWorld(head))
                {
                    GiveGoldAction.ApplyForPartyToCharacter(mp.Party, head, g, true);
                    moved = g - mp.PartyTradeGold;
                    _toHead[kind] += moved; _toHeadN[kind]++;
                }
                else
                {
                    var t = VillageTakings.NearestTown(mp);
                    if (t == null || t.SettlementComponent == null) { _lost += g; return; }
                    GiveGoldAction.ApplyForPartyToSettlement(mp.Party, t, g, true);
                    moved = g - mp.PartyTradeGold;
                    _toTown[kind] += moved; _toTownN[kind]++;
                }
                if (Measure31.IsDteConvoy(mp)) { _convoyBack += moved; _convoyBackN++; }
            }
            catch (Exception e) { Seal31.Stumble("CaravanCapital.RoutePurse", e); }
        }

        /// <summary>OnPartyRemoved bez MobilePartyDestroyed: kiesa jak wyzej, sakiewka ludzi (sierota) -> kasa najblizszego miasta.</summary>
        internal static void OnRemoved(MobileParty mp)
        {
            if (!On || !Live || mp == null) return;
            RoutePurse(mp, 1);
            try
            {
                if (!MenPurse.On) return;
                int purse = MenPurse.Get(mp);
                if (purse <= 0) return;
                var t = VillageTakings.NearestTown(mp);
                if (t == null || t.Town == null) return;
                int x = MenPurse.Take(mp, purse);
                t.Town.ChangeGold(x);
                MoneyLedger.NotePurseGone(x, true);
                MoneyLedger.Note169(MoneyLedger.N169PurseGone, t, x);
                _orphanPurse += x;
            }
            catch (Exception e) { Seal31.Stumble("CaravanCapital.OnRemoved", e); }
        }

        // ------------------------------------------------------------ wpiecia
        internal static void ApplyAll()
        {
            const string bkLord = "BannerKings.Behaviours.BKLordPropertyBehavior";
            var bt = AccessTools.TypeByName(bkLord);
            Seal31.Patch("kapital: kupno karawany BK (BKLordPropertyBehavior.OnSettlementEntered)", bt != null ? AccessTools.Method(bt, "OnSettlementEntered") : null, typeof(CaravanCapital),
                         nameof(BkPre), null, nameof(CtxFin), Priority.Low, Priority.Normal, Priority.High);
            Seal31.Patch("kapital: karawana gracza (form_a_caravan_accept)", AccessTools.Method(typeof(CaravanConversationsCampaignBehavior), "conversation_magistrate_form_a_caravan_accept_on_consequence"),
                         typeof(CaravanCapital), nameof(PlayerPre), null, nameof(CtxFin), Priority.Low, Priority.Normal, Priority.High);
            Seal31.Patch("kapital: nowa karawana (CaravanPartyComponent.CreateCaravanParty)", AccessTools.Method(typeof(CaravanPartyComponent), "CreateCaravanParty"), typeof(CaravanCapital),
                         nameof(CreatePre), null, nameof(CreateFin), Priority.Low, Priority.Normal, Priority.High);
        }

        private static readonly Type[] CapArgs = { typeof(Hero), typeof(bool), typeof(bool) };

        internal static void EnsureHooks()
        {
            const string dte = "DynamicTroopEquipmentReupload.CutTheirSupplyBehavior";
            var dt = AccessTools.TypeByName(dte);
            Seal31.Patch("kapital: konwoj DTE (SpawnReinforcementCaravan)", dt != null ? AccessTools.Method(dt, "SpawnReinforcementCaravan") : null, typeof(CaravanCapital),
                         nameof(DtePre), null, nameof(CtxFin), Priority.Low, Priority.Normal, Priority.High);
            try
            {
                var cm = Campaign.Current.Models.CaravanModel;
                MethodInfo m = null;
                if (cm != null)
                {
                    var any = cm.GetType().GetMethod("GetInitialTradeGold", BindingFlags.Public | BindingFlags.Instance, null, CapArgs, null);
                    var decl = any != null ? any.DeclaringType : null;
                    m = decl != null ? decl.GetMethod("GetInitialTradeGold", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, null, CapArgs, null) : null;
                    if (m != null && m.IsAbstract) m = null;
                }
                Seal31.Patch("kapital: model karawan (" + (cm != null ? cm.GetType().Name : "brak") + ".GetInitialTradeGold, Low - przed oknem O17)", m, typeof(CaravanCapital),
                             null, nameof(CapPostfix), null, Priority.Normal, Priority.Low);
            }
            catch (Exception e) { Seal31.Missing.Add("kapital: model karawan (blad: " + e.Message + ")"); }
        }

        // ------------------------------------------------------------ linia i porzadki
        internal static string Part()
        {
            var sb = new System.Text.StringBuilder(600);
            if (!On) sb.Append("WYLACZONE (Caravan Capital Real) - ");
            sb.Append("konwoje DTE z kapitalem 0: ").Append(_dteN).Append(" (gra dalaby ").Append(_dteCap).Append("); kiesy konwojow DTE oddane glowom przy usunieciu ").Append(_convoyBack)
              .Append(" (").Append(_convoyBackN).Append(" - m.in. doplaty rodow do 5000)")
              .Append(" | kupno karawan BK: ").Append(_bkN).Append(", cena ").Append(_bkPrice).Append(", kapital z ceny ").Append(_bkCap).Append(", do kas miast ").Append(_bkTown)
              .Append(" | karawany gracza: ").Append(_plN).Append(", kapital gry ").Append(_plK).Append(", zaplacone miastu ").Append(_plPaid).Append(", miasto -> karawana ").Append(_plMoved)
              .Append(" | znikajace bez bitwy - kiesy: ");
            for (int k = 0; k < 3; k++)
            {
                if (k > 0) sb.Append("; ");
                sb.Append(RName[k]).Append(" -> glowom ").Append(_toHead[k]).Append(" (").Append(_toHeadN[k]).Append("), -> miastom ").Append(_toTown[k]).Append(" (").Append(_toTownN[k]).Append(")");
            }
            sb.Append("; sakiewki usunietych -> miastom ").Append(_orphanPurse).Append("; bez odbiorcy (prog 0) ").Append(_lost).Append(", kupno bez odczytu modelu ").Append(_noModel);
            return sb.ToString();
        }

        internal static void ClearDay()
        {
            _dteCap = _bkPrice = _bkCap = _bkTown = _plK = _plPaid = _plMoved = 0; _dteN = _bkN = _plN = _noModel = 0;
            Array.Clear(_toHead, 0, 3); Array.Clear(_toTown, 0, 3); Array.Clear(_toHeadN, 0, 3); Array.Clear(_toTownN, 0, 3);
            _lost = _convoyBack = _orphanPurse = 0; _convoyBackN = 0;
        }

        internal static void Reset() { ClearDay(); Array.Clear(_stack, 0, _stack.Length); _depth = 0; _createDepth = 0; _createSeen = false; }
    }
}
