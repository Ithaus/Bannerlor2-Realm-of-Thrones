using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;
using CW = Armoury.CirculationWindows;

namespace Armoury
{
    /// <summary>
    /// E3/0 (paczka 164a-0) - POMIAR, gra bez zmian (projekt etapu 3, rozdz. 3 "FALA 1"). Cel: nazwac obie czesci reszty ksiegi pieniadza (R)
    /// - prog etapu: srednia 28 dob |R| ponizej 10 000 zl/d - i zmierzyc to, czego potrzebuja nastepne fale (statki, Inni). Tylko liczniki i pozycje
    /// ksiegi; zadna latka nie zwraca false, nie zmienia wyniku ani zlota.
    ///  1. PORTFELE WEZWAN DO WOJNY (RB3, -3.6..-5.2 tys./d): skarbiec wzywajacego placi do portfela wezwanego (CrownCallToWar.Pay), a ksiega liczyla
    ///     tylko KingdomBudgetWallet - wplata znikala w naszym ticku, a wraca w rozliczeniu rodow wezwanego jako "rozliczenia rodow na plus". Teraz
    ///     dodatnie portfele wezwan sa posiadaczem zlota (MoneyLedger.ReadHolders, linia "Pieniadz swiata": "portfele wezwan do wojny (168)").
    ///     Wyplata portfela rodom - przelew. Pole jest NETTO (krolestwo naraz wzywajace i wezwane), wiec: czesc wplaty do portfela, ktory zostaje
    ///     ponizej zera - ujscie "wplaty do ujemnego portfela wezwanego" (gra wyplaca rodom tylko z dodatniego, DefaultClanFinanceModel:630);
    ///     splata dlugu portfela sprzed paczki (skarbiec -> portfel ujemny) - ujscie "dlug portfela sprzed paczki" (licznik _dLegacyPaid);
    ///     kazda inna zmiana dodatniej czesci pola bez platnika (start porozumienia: gra odejmuje cene wzywajacemu, a gdy CrownPaysCallToWar
    ///     wylaczone - dopisuje wezwanemu z niczego; koniec porozumienia - skreslona reszta wraca na pole; upadle krolestwo) - NETTO "portfele wezwan
    ///     - zmiana pola netto bez platnika". Pomiar: suma max(0, portfel) przed i po (AllianceCampaignBehavior.Start/EndCallToWarAgreement
    ///     - prefiks First + finalizer, tylko zewnetrzne wywolanie) i w CrownCallToWar (Pay, dlug sprzed paczki, skreslenia).
    ///  2. PARTIE USUWANE BEZ ZNISZCZENIA (trop "poza tickiem", -11.8 tys./d): DTE kasuje konwoje przez MobileParty.RemoveParty (refleksja), ktore
    ///     wysyla tylko OnPartyRemoved (IsActive=false juz przed zdarzeniem) - nasluch ksiegi (MobilePartyDestroyed) ich nie widzial. Nowa pozycja
    ///     ujsc U8r "kiesy partii usunietych bez zniszczenia" (konwoje DTE / inne karawany / inne); partie, ktore widzial MobilePartyDestroyed,
    ///     oznaczane w pierwszej linii naszego nasluchu tego zdarzenia (zbior czyszczony co dobe). W U8 (partie znikajace) podzial "w tym rozwiazane
    ///     (niszczyciel null)" - informacja z CirculationWindows.OnPartyDestroyed.
    ///  3. STATKI: zwrot NavalDLC za statki (RecoverGoldFromRemainingShipsAfterDistribution) przy rozwiazaniu / przy zniszczeniu na ladzie /
    ///     DRUGI zwrot tej samej partii w dobie (rozwiazanie wysyla OnPartyDisbanded i potem MobilePartyDestroyed, oba wolaja zwrot, a zwrot statkow
    ///     nie zabiera - krytyka W6); zloto za statki zatopione w bitwach morskich (PlunderedGold zwyciezcow w LootDefeatedPartyShips) - w "bitwy".
    ///  4. POWTORNE LUPIENIE CIAL: wywolania LootCasualtyCharacter na jedno wywolanie LootDefeatedPartyCasualties wobec liczby cial pokonanych
    ///     (zabici + ranni) - skad 371 043 wywolan w dobie 420 biegu 457.
    ///  5. INNI (nieumarli, krytyka W12): kiesy ich rodow i wplywy wedlug zrodla (rozliczenie rodu - model z zoldem; bitwy; lup z cial; inne
    ///     zdarzenia z niczego; od innych posiadaczy; nasz tick dobowy - zwrot korony, Bank) - warunek E3/8d (zold Innych).
    /// </summary>
    internal static class Measure31
    {
        private static bool Live { get { var c = Campaign.Current; return c != null && c.GameStarted; } }

        // ------------------------------------------------------------ 1. portfele wezwan
        private static int _ctwDepth;
        private static long _ctwStartNet, _ctwEndNet, _ctwPayBelow, _ctwPayAbove, _ctwLegacy, _ctwOther, _ctwGone, _ctwPaid;
        private static int _ctwStartN, _ctwEndN;

        internal static long CtwSum()
        {
            long s = 0;
            foreach (var k in Kingdom.All) if (k != null && !k.IsEliminated && k.CallToWarWallet > 0) s += k.CallToWarWallet;
            return s;
        }

        private static long Pos(int w) { return w > 0 ? w : 0; }

        public struct CtwState { public bool On; public long V; public bool Player; }

        public static void StartPre(bool __4, out CtwState __state)
        {
            __state = default(CtwState);
            try
            {
                if (_ctwDepth++ > 0 || !Live) return;
                __state.On = true; __state.Player = __4;
                __state.V = CtwSum() + (__4 && Hero.MainHero != null ? Hero.MainHero.Gold : 0);
            }
            catch (Exception e) { __state.On = false; Seal31.Stumble("Measure31.StartPre", e); }
        }

        public static void StartFin(CtwState __state)
        {
            _ctwDepth = Math.Max(0, _ctwDepth - 1);
            if (!__state.On) return;
            try
            {
                long d = CtwSum() + (__state.Player && Hero.MainHero != null ? Hero.MainHero.Gold : 0) - __state.V;
                Seal31.AddWorld(Seal31.NCtwNet, d); _ctwStartNet += d; _ctwStartN++;
            }
            catch (Exception e) { Seal31.Stumble("Measure31.StartFin", e); }
        }

        public static void EndPre(out CtwState __state)
        {
            __state = default(CtwState);
            try
            {
                if (_ctwDepth++ > 0 || !Live) return;
                __state.On = true; __state.V = CtwSum();
            }
            catch (Exception e) { __state.On = false; Seal31.Stumble("Measure31.EndPre", e); }
        }

        public static void EndFin(CtwState __state)
        {
            _ctwDepth = Math.Max(0, _ctwDepth - 1);
            if (!__state.On) return;
            try
            {
                long d = CtwSum() - __state.V;
                Seal31.AddWorld(Seal31.NCtwNet, d); _ctwEndNet += d; _ctwEndN++;
            }
            catch (Exception e) { Seal31.Stumble("Measure31.EndFin", e); }
        }

        /// <summary>CrownCallToWar.Pay po przelewie: skarbiec -> portfel wezwanego (i splata dlugu korony w portfelu wzywajacego).</summary>
        internal static void CtwPay(Kingdom calling, int wc0, Kingdom called, int wd0, int ix)
        {
            try
            {
                if (ix <= 0 || calling == null || called == null || _ctwDepth > 0) return;
                long below = ix - (Pos(called.CallToWarWallet) - Pos(wd0));
                long above = Pos(calling.CallToWarWallet) - Pos(wc0);
                Seal31.AddWorld(Seal31.UCtwBelow, below); _ctwPayBelow += below;
                Seal31.AddWorld(Seal31.NCtwNet, above); _ctwPayAbove += above;
                _ctwPaid += ix;
            }
            catch (Exception e) { Seal31.Stumble("Measure31.CtwPay", e); }
        }

        /// <summary>CrownCallToWar.Daily - splata dlugu portfela sprzed paczki: skarbiec -> portfel ujemny wzywajacego (rody wezwanego dostaly to od gry z niczego).</summary>
        internal static void CtwLegacy(Kingdom calling, int w0, long pay)
        {
            try
            {
                if (pay <= 0 || calling == null || _ctwDepth > 0) return;
                long above = Pos(calling.CallToWarWallet) - Pos(w0);
                Seal31.AddWorld(Seal31.UCtwLegacy, pay); _ctwLegacy += pay;
                Seal31.AddWorld(Seal31.NCtwNet, above); _ctwPayAbove += above;
            }
            catch (Exception e) { Seal31.Stumble("Measure31.CtwLegacy", e); }
        }

        /// <summary>CrownCallToWar - zmiana pola bez platnika (reszta ceny wraca do gry przy wylaczeniu, skreslenie przy upadku krolestwa).</summary>
        internal static void CtwOther(Kingdom k, int w0)
        {
            try
            {
                if (k == null || k.IsEliminated || _ctwDepth > 0) return;
                long d = Pos(k.CallToWarWallet) - Pos(w0);
                Seal31.AddWorld(Seal31.NCtwNet, d); _ctwOther += d;
            }
            catch (Exception e) { Seal31.Stumble("Measure31.CtwOther", e); }
        }

        /// <summary>Krolestwo upada: jego dodatni portfel wezwan wypada z posiadaczy (jak skarbiec w CirculationWindows.OnKingdomDestroyed).</summary>
        internal static void OnKingdomDestroyed(Kingdom k)
        {
            try
            {
                if (k == null) return;
                long w = Pos(k.CallToWarWallet);
                if (w == 0) return;
                Seal31.AddWorld(Seal31.NCtwNet, -w); _ctwGone += w;
            }
            catch (Exception e) { Seal31.Stumble("Measure31.OnKingdomDestroyed", e); }
        }

        // ------------------------------------------------------------ 2. partie usuwane bez zniszczenia (U8r)
        private static readonly HashSet<MobileParty> _destroyed = new HashSet<MobileParty>();
        private static long _remConvoy, _remCaravan, _remOther;
        private static int _remConvoyN, _remCaravanN, _remOtherN, _remSeen;

        /// <summary>Nasz nasluch MobilePartyDestroyed (pierwsza linia): partia, ktora zniknie przez DestroyPartyAction - jej OnPartyRemoved nie liczymy.</summary>
        internal static void MarkDestroyed(MobileParty mp) { if (mp != null) _destroyed.Add(mp); }

        internal static bool SeenDestroyed(MobileParty mp) { return mp != null && _destroyed.Contains(mp); }

        /// <summary>Konwoj zaopatrzenia DTE: karawana, ktora nie podejmuje decyzji (DTE: SetDoNotMakeNewDecisions(true)).</summary>
        internal static bool IsDteConvoy(MobileParty mp)
        {
            try { return mp != null && mp.IsCaravan && mp.Ai != null && mp.Ai.DoNotMakeNewDecisions; } catch { return false; }
        }

        internal static void OnPartyRemoved(PartyBase party)
        {
            var mp = party != null && party.IsMobile ? party.MobileParty : null;
            if (mp == null) return;
            if (_destroyed.Contains(mp)) { _remSeen++; return; }   // widziane przez MobilePartyDestroyed - liczy U8
            CaravanCapital.OnRemoved(mp);                             // E3/7: kiesa do glowy rodu, sakiewka do miasta - U8r liczy reszte
            if (mp.IsLordParty && mp.LeaderHero != null) return;      // kiesa wodza zostaje z wodzem
            int g = mp.PartyTradeGold;
            if (g <= 0) return;
            Seal31.AddWorld(Seal31.UPartyRemoved, g);
            if (IsDteConvoy(mp)) { _remConvoy += g; _remConvoyN++; }
            else if (mp.IsCaravan) { _remCaravan += g; _remCaravanN++; }
            else { _remOther += g; _remOtherN++; }
        }

        internal static string RemovedText()
        {
            return "konwoje DTE " + _remConvoy + " w " + _remConvoyN + ", inne karawany " + _remCaravan + " w " + _remCaravanN + ", inne " + _remOther + " w " + _remOtherN;
        }

        /// <summary>CirculationWindows.OnPartyDestroyed (U8): kiesa, ktora zniknela z partia - podzial "w tym rozwiazane (niszczyciel null)".</summary>
        internal static void NoteGone(MobileParty mp, PartyBase destroyer, int g)
        {
            try { if (destroyer == null && g > 0) Seal31.AddWorld(Seal31.IU8Disb, g); } catch (Exception e) { Seal31.Stumble("Measure31.NoteGone", e); }
        }

        // ------------------------------------------------------------ 3. statki
        private static int _shipCtx;                               // 1 = OnPartyDisbanded NavalDLC, 2 = OnMobilePartyDestroyed NavalDLC
        private static readonly Dictionary<MobileParty, int> _shipPaidParty = new Dictionary<MobileParty, int>();
        private static long _shipDisb, _shipDestr, _shipOut, _shipSecond;
        private static int _shipDisbN, _shipDestrN, _shipOutN, _shipSecondN;
        internal static long DayShipBattleGold;
        private static int _shipBattleN;

        public static void NavDisbPre(out int __state) { __state = _shipCtx; _shipCtx = 1; }
        public static void NavDestrPre(out int __state) { __state = _shipCtx; _shipCtx = 2; }
        public static void NavCtxFin(int __state) { _shipCtx = __state; }

        public struct ShipState { public bool On; public Hero H; public int G0; }

        public static void RecoverPre(MobileParty __0, out ShipState __state)
        {
            __state = default(ShipState);
            try
            {
                if (__0 == null || !Live) return;
                var c = __0.ActualClan;
                var h = c != null ? c.Leader : null;
                if (h == null) return;
                __state.On = true; __state.H = h; __state.G0 = h.Gold;
            }
            catch (Exception e) { Seal31.Stumble("Measure31.RecoverPre", e); }
        }

        public static void RecoverFin(MobileParty __0, ShipState __state)
        {
            if (!__state.On) return;
            try
            {
                long d = (long)__state.H.Gold - __state.G0;
                if (d <= 0) return;
                int n; _shipPaidParty.TryGetValue(__0, out n);
                _shipPaidParty[__0] = n + 1;
                if (n > 0) { _shipSecond += d; _shipSecondN++; }
                if (_shipCtx == 1) { _shipDisb += d; _shipDisbN++; }
                else if (_shipCtx == 2) { _shipDestr += d; _shipDestrN++; }
                else { _shipOut += d; _shipOutN++; }
            }
            catch (Exception e) { Seal31.Stumble("Measure31.RecoverFin", e); }
        }

        public static void ShipsLootPre(MBReadOnlyList<MapEventParty> __0, out long __state)
        {
            __state = long.MinValue;
            try
            {
                if (__0 == null || !Live) return;
                long s = 0; foreach (var w in __0) if (w != null) s += w.PlunderedGold;
                __state = s;
            }
            catch (Exception e) { __state = long.MinValue; Seal31.Stumble("Measure31.ShipsLootPre", e); }
        }

        public static void ShipsLootFin(MBReadOnlyList<MapEventParty> __0, long __state)
        {
            if (__state == long.MinValue || __0 == null) return;
            try
            {
                long s = 0; foreach (var w in __0) if (w != null) s += w.PlunderedGold;
                long d = s - __state;
                if (d != 0) { DayShipBattleGold += d; _shipBattleN++; }
            }
            catch (Exception e) { Seal31.Stumble("Measure31.ShipsLootFin", e); }
        }

        // ------------------------------------------------------------ 4. powtorne lupienie cial
        private static long _casCalls, _casBodies, _casRunCalls, _casMaxCalls;
        private static int _casRuns, _casOver, _casRepeat, _casOutside;
        private static double _casMaxMult;
        private static int _casDepth;
        private static readonly HashSet<MapEvent> _casSeen = new HashSet<MapEvent>();

        public struct CasState { public bool On; public long Calls0; public long Bodies; }

        public static void CasRunPre(MapEvent __instance, MBReadOnlyList<MapEventParty> __1, out CasState __state)
        {
            __state = default(CasState);
            try
            {
                _casDepth++;
                if (!Live || __instance == null || __1 == null) return;
                long b = 0;
                foreach (var d in __1) if (d != null) b += (d.DiedInBattle != null ? d.DiedInBattle.TotalManCount : 0) + (d.WoundedInBattle != null ? d.WoundedInBattle.TotalManCount : 0);
                __state.On = true; __state.Calls0 = _casCalls; __state.Bodies = b;
                if (!_casSeen.Add(__instance)) _casRepeat++;
            }
            catch (Exception e) { __state.On = false; Seal31.Stumble("Measure31.CasRunPre", e); }
        }

        public static void CasRunFin(CasState __state)
        {
            _casDepth = Math.Max(0, _casDepth - 1);
            if (!__state.On) return;
            try
            {
                long calls = _casCalls - __state.Calls0;
                _casRuns++; _casBodies += __state.Bodies; _casRunCalls += calls;
                if (calls > _casMaxCalls) _casMaxCalls = calls;
                if (calls > 3 * Math.Max(1L, __state.Bodies)) _casOver++;
                double mult = (double)calls / Math.Max(1L, __state.Bodies);
                if (mult > _casMaxMult) _casMaxMult = mult;
            }
            catch (Exception e) { Seal31.Stumble("Measure31.CasRunFin", e); }
        }

        public struct LootState { public bool On; public MobileParty Mp; public int G0; }

        /// <summary>MapEvent.LootCasualtyCharacter (prefiks void, wysoki priorytet): licznik wywolan; zwyciezca nieumarly - migawka jego kiesy (Inni).</summary>
        public static void CasCallPre(MapEventParty __1, out LootState __state)
        {
            __state = default(LootState);
            try
            {
                _casCalls++;
                if (_casDepth == 0) _casOutside++;
                var p = __1 != null ? __1.Party : null;
                var mp = p != null ? p.MobileParty : null;
                if (mp == null || p.LeaderHero == null || p.LeaderHero == Hero.MainHero || !Undead.Party(mp)) return;
                __state.On = true; __state.Mp = mp; __state.G0 = mp.PartyTradeGold;
            }
            catch (Exception e) { __state.On = false; Seal31.Stumble("Measure31.CasCallPre", e); }
        }

        public static void CasCallFin(LootState __state)
        {
            if (!__state.On) return;
            try { _udLoot += (long)__state.Mp.PartyTradeGold - __state.G0; } catch (Exception e) { Seal31.Stumble("Measure31.CasCallFin", e); }
        }

        // ------------------------------------------------------------ 5. Inni (nieumarli)
        private static readonly HashSet<Clan> _udClans = new HashSet<Clan>();
        private static bool _udClansOk;
        private static long _udStart = long.MinValue, _udBlock0 = long.MinValue;
        private static long _udModel, _udBattle, _udNothing, _udFrom, _udOut, _udLoot;
        private static int _udModelN;

        private static void UdRefresh()
        {
            _udClans.Clear();
            foreach (var c in Clan.All) if (c != null && !c.IsEliminated && ClanIncomeBook.IsUndeadClan(c)) _udClans.Add(c);
            _udClansOk = true;
        }

        private static bool UdClan(Clan c) { if (!_udClansOk) UdRefresh(); return c != null && _udClans.Contains(c); }

        private static long UdGoldOf(Clan c)
        {
            long s = 0;
            foreach (var h in c.Heroes) if (h != null && !CW.OutOfWorld(h)) s += h.Gold;
            foreach (var w in c.WarPartyComponents)
            {
                var mp = w != null ? w.MobileParty : null;
                if (mp != null && mp.IsActive && mp.LeaderHero == null) s += mp.PartyTradeGold;
            }
            return s;
        }

        private static long UdGold()
        {
            if (!_udClansOk) UdRefresh();
            long s = 0;
            foreach (var c in _udClans) if (c != null && !c.IsEliminated) s += UdGoldOf(c);
            return s;
        }

        public static void UdTickPre(Clan __0, out long __state)
        {
            __state = long.MinValue;
            try { if (Live && __0 != null && UdClan(__0)) __state = UdGoldOf(__0); }
            catch (Exception e) { __state = long.MinValue; Seal31.Stumble("Measure31.UdTickPre", e); }
        }

        public static void UdTickPost(Clan __0, long __state)
        {
            if (__state == long.MinValue || __0 == null) return;
            try { _udModel += UdGoldOf(__0) - __state; _udModelN++; } catch (Exception e) { Seal31.Stumble("Measure31.UdTickPost", e); }
        }

        private static Clan UdSide(Hero h, PartyBase p)
        {
            if (h != null) return h.Clan;
            var mp = p != null && p.IsMobile ? p.MobileParty : null;
            if (mp == null) return null;
            if (mp.IsLordParty && mp.LeaderHero != null) return mp.LeaderHero.Clan;
            return mp.ActualClan;
        }

        /// <summary>Zdarzenie zlota gry (nasz nasluch): wplywy i wyplywy rodow nieumarlych poza rozliczeniem rodu i poza naszym tickiem.</summary>
        internal static void OnGold(Hero gh, PartyBase gp, Hero rh, PartyBase rp, int a)
        {
            if (a == 0 || !Live || MoneyLedger.InClanTick || MoneyLedger.InBlock) return;
            if (a < 0) { var th = gh; gh = rh; rh = th; var tp = gp; gp = rp; rp = tp; a = -a; }
            bool gNone = gh == null && gp == null;
            var rc = UdSide(rh, rp);
            var gc = gNone ? null : UdSide(gh, gp);
            bool rU = rc != null && UdClan(rc), gU = gc != null && UdClan(gc);
            if (rU == gU) return;                                     // przelew wewnatrz Innych albo bez Innych
            if (gU) { _udOut += a; return; }
            if (gNone) { if (CW.CurrentKind == CW.KBattle) _udBattle += a; else _udNothing += a; }
            else _udFrom += a;
        }

        internal static void BlockOpen()
        {
            if (!Live) return;
            _udBlock0 = UdGold();
            if (_udStart == long.MinValue) _udStart = _udBlock0;
        }

        // ------------------------------------------------------------ wpiecia
        internal static void ApplyAll()
        {
            const string navDist = "NavalDLC.CampaignBehaviors.NavalShipDistributionCampaignBehavior";
            var nd = AccessTools.TypeByName(navDist);
            Seal31.Patch("statki: zwrot NavalDLC (RecoverGold...)", nd != null ? AccessTools.Method(nd, "RecoverGoldFromRemainingShipsAfterDistribution") : null, typeof(Measure31), nameof(RecoverPre), null, nameof(RecoverFin), Priority.High);
            Seal31.Patch("statki: kontekst rozwiazania (OnPartyDisbanded)", nd != null ? AccessTools.Method(nd, "OnPartyDisbanded") : null, typeof(Measure31), nameof(NavDisbPre), null, nameof(NavCtxFin), Priority.First);
            Seal31.Patch("statki: kontekst zniszczenia (OnMobilePartyDestroyed)", nd != null ? AccessTools.Method(nd, "OnMobilePartyDestroyed") : null, typeof(Measure31), nameof(NavDestrPre), null, nameof(NavCtxFin), Priority.First);
            Seal31.Patch("statki zatopione w bitwach (LootDefeatedPartyShips)", AccessTools.Method(typeof(MapEvent), "LootDefeatedPartyShips"), typeof(Measure31), nameof(ShipsLootPre), null, nameof(ShipsLootFin), Priority.First);
            Seal31.Patch("lupienie cial - bitwa (LootDefeatedPartyCasualties)", AccessTools.Method(typeof(MapEvent), "LootDefeatedPartyCasualties"), typeof(Measure31), nameof(CasRunPre), null, nameof(CasRunFin), Priority.First);
            Seal31.Patch("lupienie cial - cialo (LootCasualtyCharacter)", AccessTools.Method(typeof(MapEvent), "LootCasualtyCharacter"), typeof(Measure31), nameof(CasCallPre), null, nameof(CasCallFin), Priority.High);
            Seal31.Patch("Inni - rozliczenie rodu (DailyTickClan)", AccessTools.Method(typeof(ClanVariablesCampaignBehavior), "DailyTickClan"), typeof(Measure31), nameof(UdTickPre), nameof(UdTickPost), null, Priority.First, Priority.Last);
        }

        internal static void EnsureHooks()
        {
            var ab = typeof(AllianceCampaignBehavior);
            Seal31.Patch("portfele wezwan - start porozumienia", AccessTools.Method(ab, "StartCallToWarAgreement", new[] { typeof(Kingdom), typeof(Kingdom), typeof(Kingdom), typeof(int), typeof(bool) }),
                         typeof(Measure31), nameof(StartPre), null, nameof(StartFin), Priority.First);
            Seal31.Patch("portfele wezwan - koniec porozumienia", AccessTools.Method(ab, "EndCallToWarAgreement", new[] { typeof(Kingdom), typeof(Kingdom), typeof(Kingdom) }),
                         typeof(Measure31), nameof(EndPre), null, nameof(EndFin), Priority.First);
        }

        // ------------------------------------------------------------ linia i porzadki
        internal static string Part()
        {
            var sb = new StringBuilder(1400);
            sb.Append("portfele wezwan (168): posiadacz ").Append(CtwSafe()).Append(" zl; skarbce zaplacily do portfeli ").Append(_ctwPaid)
              .Append(", z tego do ujemnych (ujscie) ").Append(_ctwPayBelow).Append(", dlug sprzed paczki (ujscie) ").Append(_ctwLegacy)
              .Append("; pole netto bez platnika: start porozumien ").Append(Sg(_ctwStartNet)).Append(" (").Append(_ctwStartN).Append("), koniec ").Append(Sg(_ctwEndNet)).Append(" (").Append(_ctwEndN)
              .Append("), splata dlugu w portfelu dodatnim ").Append(Sg(_ctwPayAbove)).Append(", skreslenia i wylaczenie ").Append(Sg(_ctwOther)).Append(", upadle krolestwa -").Append(_ctwGone)
              .Append(" | partie usuniete bez zniszczenia (U8r): ").Append(RemovedText()).Append("; widziane przez MobilePartyDestroyed ").Append(_remSeen)
              .Append(" | statki NavalDLC - zwrot za statki: przy rozwiazaniu ").Append(_shipDisb).Append(" (").Append(_shipDisbN).Append("), przy zniszczeniu na ladzie ").Append(_shipDestr)
              .Append(" (").Append(_shipDestrN).Append("), poza oknami ").Append(_shipOut).Append(" (").Append(_shipOutN).Append("); w tym DRUGI zwrot tej samej partii w dobie ").Append(_shipSecond)
              .Append(" (").Append(_shipSecondN).Append(") - kwota podwojna; statki zatopione w bitwach morskich (PlunderedGold) ").Append(DayShipBattleGold).Append(" (").Append(_shipBattleN).Append(" bitew)")
              .Append(" | lupienie cial: wywolan ").Append(_casCalls).Append(" w ").Append(_casRuns).Append(" lupach bitew (cial ").Append(_casBodies).Append("), bitew z wywolaniami > 3 x ciala ")
              .Append(_casOver).Append(", najwieksza krotnosc ").Append(_casMaxMult.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)).Append(" (najwiecej wywolan w jednym lupie ").Append(_casMaxCalls)
              .Append("), powtorny lup tej samej bitwy ").Append(_casRepeat).Append(", wywolania poza lupem bitwy ").Append(_casOutside);
            sb.Append(" | Inni: ").Append(UndeadPart());
            return sb.ToString();
        }

        private static long CtwSafe() { try { return CtwSum(); } catch { return -1; } }

        private static string Sg(long v) { return (v >= 0 ? "+" : "") + v; }

        private static string UndeadPart()
        {
            try
            {
                long now = UdGold();
                long block = _udBlock0 != long.MinValue ? now - _udBlock0 : 0;
                long start = _udStart != long.MinValue ? _udStart : now;
                long change = now - start;
                long rest = change - _udModel - _udBattle - _udNothing - _udFrom + _udOut - _udLoot - block;
                return "rodow " + _udClans.Count + ", kiesy " + now + " (zmiana " + Sg(change) + ") = rozliczenie rodu (model, z zoldem) " + Sg(_udModel) + " (" + _udModelN + " rozliczen)"
                       + " + bitwy (zdarzenia z niczego) " + _udBattle + " + lup z cial " + _udLoot + " + inne zdarzenia z niczego " + _udNothing + " + od innych posiadaczy " + _udFrom
                       + " - oddali innym " + _udOut + " + nasz tick dobowy (zwrot korony, Bank, budzet) " + Sg(block) + " + reszta " + Sg(rest);
            }
            catch (Exception e) { Seal31.Stumble("Measure31.UndeadPart", e); return "blad odczytu"; }
        }

        internal static void ClearDay()
        {
            _ctwStartNet = _ctwEndNet = _ctwPayBelow = _ctwPayAbove = _ctwLegacy = _ctwOther = _ctwGone = _ctwPaid = 0; _ctwStartN = _ctwEndN = 0;
            _destroyed.Clear(); _remConvoy = _remCaravan = _remOther = 0; _remConvoyN = _remCaravanN = _remOtherN = _remSeen = 0;
            _shipPaidParty.Clear(); _shipDisb = _shipDestr = _shipOut = _shipSecond = 0; _shipDisbN = _shipDestrN = _shipOutN = _shipSecondN = 0;
            DayShipBattleGold = 0; _shipBattleN = 0;
            _casCalls = _casBodies = _casRunCalls = _casMaxCalls = 0; _casRuns = _casOver = _casRepeat = _casOutside = 0; _casMaxMult = 0; _casSeen.Clear();
            try { if (Live) { _udClansOk = false; _udStart = UdGold(); } } catch { _udStart = long.MinValue; }
            _udBlock0 = long.MinValue; _udModel = _udBattle = _udNothing = _udFrom = _udOut = _udLoot = 0; _udModelN = 0;
        }

        internal static void Reset()
        {
            ClearDay();
            _ctwDepth = 0; _shipCtx = 0; _casDepth = 0; _udClansOk = false; _udClans.Clear(); _udStart = long.MinValue;
        }
    }
}
