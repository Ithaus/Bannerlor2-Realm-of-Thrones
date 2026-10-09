using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.BarterSystem;
using TaleWorlds.CampaignSystem.BarterSystem.Barterables;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// PACZKA 169c - POMIARY ETAPU 2 (projekt etapu 2, rozdz. "169c"; PLAN 2.1; audyt 00 E2, 04 N8, 05 A4, 09 P-1, KL pomiar A). Sam log i CSV:
    /// zadna latka nie zwraca false, nie zmienia wyniku ani parametrow, nie rusza zlota, towaru ani ludzi. Linie raz na dobe (po ksiedze rodow):
    ///  "Niewola lordow i okupy (169c)", "Zalogi: przyrost bez werbunku (169c)", "Kasy miast (169c)" (+ kasy-miast.csv), "Wydatki rycerzy (169c)",
    ///  "Dezercja AI wedlug przyczyny (169c)", "Ludnosc BK (169c)" (+ ludnosc-bk.csv), "Sluby AI (169c)", "Towar wedrowcow BK (169c)",
    ///  "Wzrost Innych (169c)", "Miara historyczna cz. 2 (169c)". Okna pomiaru (prefiks + finalizer) wpinane w kampanii (EnsureHooks), raz na
    /// proces; zdarzenia gry przez RegisterEvents. Kazde cialo w try; bledy liczone (licz potkniecia, nie gas funkcji - CLAUDE.md).
    /// Bez zapisu w grze - liczniki sesji (po wczytaniu od zera). Wylacznik: ClanIncomeBookEnabled + ClanIncomeBookStableD (+ LogEnabled).
    /// </summary>
    internal static class Measure169c
    {
        private static bool On { get { var s = Settings.Current; return s != null && s.LogEnabled && s.ClanIncomeBookEnabled && s.ClanIncomeBookStableD; } }
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static Harmony _harmony;
        private static readonly HashSet<MethodBase> _hooked = new HashSet<MethodBase>();
        private static readonly List<string> _wired = new List<string>(), _missing = new List<string>();
        private static bool _hooksTried;
        private static int _stumbles;
        private static readonly HashSet<string> _errSites = new HashSet<string>();
        private static int _sessDays;

        internal static void SetHarmony(Harmony h) { _harmony = h; }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_errSites.Add(where)) Log.Error("Measure169c." + where, e); } catch { }
        }

        internal static void Reset()
        {
            Array.Clear(_capToday, 0, _capToday.Length); Array.Clear(_relToday, 0, _relToday.Length); _capSess = 0; _sessDays = 0;
            _aiRansomN = 0; _aiRansomSum = 0; _aiRansomMax = 0; _aiRansomMaxTxt = null; _aiRansomSessN = 0; _aiRansomSessSum = 0;
            _gNotables = _gModel = _gFromParty = _gToParty = _gBkPatrol = 0; _garLast = -1; _milLast = -1;
            _regOutTown.Clear(); _regInTown.Clear();
            _gentrySupply.Clear(); _gentrySlaves.Clear(); _gentryLastGold.Clear();
            _wageDesert.Clear(); Array.Clear(_des, 0, _des.Length);
            _budget.Clear();
            _marN = _marPoorRich = 0; _marDowry = 0;
            _travLast.Clear(); _othersLast.Clear(); _othersBattle = 0;
            _twinsEnter.Clear(); _twins = null; _twinsLooked = false; _ratioStart = -1;
            _errSites.Clear(); _stumbles = 0;
            RansomFlows.Reset();
        }

        // ------------------------------------------------------------ wpiecie
        private static void Wire(string name, MethodBase m, string pre, string fin, string post = null)
        {
            try
            {
                if (m == null) { _missing.Add(name); return; }
                if (_hooked.Contains(m)) { _wired.Add(name); return; }
                _harmony.Patch(m, prefix: pre != null ? new HarmonyMethod(typeof(Measure169c), pre) { priority = Priority.First } : null,
                                  postfix: post != null ? new HarmonyMethod(typeof(Measure169c), post) { priority = Priority.Last } : null,
                                  finalizer: fin != null ? new HarmonyMethod(typeof(Measure169c), fin) : null);
                _hooked.Add(m); _wired.Add(name);
            }
            catch (Exception e) { _missing.Add(name + " (blad)"); Stumble("Wire(" + name + ")", e); }
        }

        /// <summary>Z ArmouryBehavior.OnSessionLaunched (kampania istnieje): okna pomiaru - raz na proces. Linia startowa w logu.</summary>
        internal static void EnsureHooks()
        {
            if (_harmony == null) return;
            if (!_hooksTried)
            {
                _hooksTried = true;
                _wired.Clear(); _missing.Clear();
                RansomFlows.Hook(_harmony);
                // przeciazenie z lista (jednolinijkowe z jednym Barterable JIT moze wkleic w ConsiderRansomPrisoner - latka by nie dzialala)
                Wire("okup AI-AI (barter)", AccessTools.Method(typeof(BarterManager), "ExecuteAiBarter", new[] { typeof(IFaction), typeof(IFaction), typeof(Hero), typeof(Hero), typeof(IEnumerable<Barterable>) }), nameof(BarterPre), null, nameof(BarterPost));
                Wire("zaloga: werbunek od notabli", AccessTools.Method(typeof(GarrisonRecruitmentCampaignBehavior), "TickAutoRecruitmentGarrisonChange", new[] { typeof(Town) }), nameof(TownPre), nameof(NotablesFin));
                Wire("zaloga: przyrost z modelu", AccessTools.Method(typeof(GarrisonRecruitmentCampaignBehavior), "TickGarrisonChangeForTown", new[] { typeof(Town) }), nameof(TownPre), nameof(ModelFin));
                Wire("zaloga: z partii", AccessTools.Method(typeof(GarrisonTroopsCampaignBehavior), "LeaveTroopsToGarrison", new[] { typeof(MobileParty), typeof(Settlement), typeof(int), typeof(bool) }), nameof(StPre), nameof(LeaveFin));
                Wire("zaloga: do partii", AccessTools.Method(typeof(GarrisonTroopsCampaignBehavior), "TakeTroopsFromGarrison", new[] { typeof(MobileParty), typeof(Settlement), typeof(int), typeof(bool) }), nameof(StPre), nameof(TakeFin));
                var bkp = AccessTools.TypeByName("BannerKings.Behaviours.BKPartyBehavior");
                Wire("zaloga: patrole BK", bkp != null ? AccessTools.Method(bkp, "AddPatrolBehavior") : null, nameof(StPre), nameof(PatrolFin));
                var sup = AccessTools.TypeByName("BannerKings.Behaviours.Estates.BKVillageSupplyAutoBehavior");
                Wire("rycerze: zaopatrzenie wsi BK", sup != null ? AccessTools.Method(sup, "RefillFromTownMarket") : null, nameof(PayerPre), nameof(SupplyFin));
                var sl = AccessTools.TypeByName("BannerKings.Behaviours.BKEstateAutoSlavePurchaseBehavior");
                Wire("rycerze: niewolnicy BK", sl != null ? AccessTools.Method(sl, "TryAutoBuyForEstate") : null, nameof(EstatePre), nameof(SlavesFin));
                Wire("dezercja: limit zoldu gry", AccessTools.Method(typeof(DefaultPartyDesertionModel), "GetTroopsToDesertDueToWageAndPartySize", new[] { typeof(MobileParty), typeof(TroopRoster) }), nameof(WagePre), nameof(WageFin));
                Wire("budzet konsumpcji miast", AccessTools.Method(typeof(ItemConsumptionBehavior), "MakeConsumption", new[] { typeof(Town), typeof(Dictionary<ItemCategory, float>), typeof(Dictionary<ItemCategory, int>) }), nameof(ConsPre), null);
                var rot = AccessTools.TypeByName("ROT.CampaignBehaviors.ROTOthersCampaignBehavior");
                Wire("Inni: nekromancja ROT", rot != null ? AccessTools.Method(rot, "OnMapEventEnded") : null, nameof(OthersPre), nameof(OthersFin));
            }
            // przeglad 169c: doba startu kampanii w logu - narzedzie progow liczy doby 120/364/728 i rok 1/2 od niej, takze w logu kontynuacji z zapisu
            string start = "-";
            try { var ctm = Campaign.Current != null ? Campaign.Current.Models.CampaignTimeModel : null; if (ctm != null) start = ((int)ctm.CampaignStartTime.ToDays).ToString(Inv); } catch { start = "-"; }
            Log.Info("Pomiary 169c: okna " + (_wired.Count > 0 ? string.Join(", ", _wired.ToArray()) : "-") + "; BRAK: " + (_missing.Count > 0 ? string.Join(", ", _missing.ToArray()) : "-")
                     + "; " + RansomFlows.Wired + ". Linie raz na dobe przy wlaczonych Clan Income Book + Stable D (teraz: " + (On ? "TAK" : "NIE") + ")"
                     + "; start kampanii dzien " + start + " (dzis " + ((int)CampaignTime.Now.ToDays - 1).ToString(Inv) + ").");
        }

        /// <summary>Z ArmouryBehavior.RegisterEvents - nasluchy gry (same liczniki).</summary>
        internal static void RegisterEvents(CampaignBehaviorBase owner)
        {
            CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(owner, OnPrisonerTaken);
            CampaignEvents.HeroPrisonerReleased.AddNonSerializedListener(owner, OnPrisonerReleased);
            CampaignEvents.OnTroopsDesertedEvent.AddNonSerializedListener(owner, OnTroopsDeserted);
            CampaignEvents.BeforeHeroesMarried.AddNonSerializedListener(owner, OnMarried);
            CampaignEvents.SettlementEntered.AddNonSerializedListener(owner, OnSettlementEntered);
        }

        // ============================================================ NIEWOLA LORDOW I OKUPY
        private const int RKing = 0, RHeir = 1, RHead = 2, RLord = 3;
        private static readonly string[] RName = { "krol", "dziecko krola", "glowa rodu", "lord" };
        private static readonly int[] _capToday = new int[4];
        private static readonly int[] _relToday = new int[8];
        private static long _capSess;
        private static int _aiRansomN, _aiRansomSessN; private static long _aiRansomSum, _aiRansomMax, _aiRansomSessSum; private static string _aiRansomMaxTxt;

        private static bool IsLordHero(Hero h) { return h != null && h.IsLord && h != Hero.MainHero && h.Clan != null && !h.Clan.IsBanditFaction; }

        private static int RankOf(Hero h)
        {
            var f = h.MapFaction;
            var king = f != null && f.IsKingdomFaction ? f.Leader : null;
            if (king != null && king == h) return RKing;
            if (king != null && !h.IsChild && (h.Father == king || h.Mother == king)) return RHeir;
            if (h.Clan != null && h.Clan.Leader == h) return RHead;
            return RLord;
        }

        private static void OnPrisonerTaken(PartyBase capturer, Hero h)
        {
            try { if (!On || !IsLordHero(h)) return; _capToday[RankOf(h)]++; _capSess++; }
            catch (Exception e) { Stumble("OnPrisonerTaken", e); }
        }

        private static void OnPrisonerReleased(Hero h, PartyBase party, IFaction f, EndCaptivityDetail d, bool show)
        {
            try { if (!On || !IsLordHero(h)) return; int i = (int)d; if (i >= 0 && i < _relToday.Length) _relToday[i]++; }
            catch (Exception e) { Stumble("OnPrisonerReleased", e); }
        }

        internal sealed class BarterState { public Hero Prisoner, Captor, Payer; public long CaptorGold, PayerGold; }
        private static FieldInfo _fPrisoner;

        public static void BarterPre(Hero __2, Hero __3, IEnumerable<Barterable> __4, out BarterState __state)
        {
            __state = null;
            try
            {
                if (!On || __4 == null) return;
                SetPrisonerFreeBarterable spf = null;
                foreach (var b in __4) { spf = b as SetPrisonerFreeBarterable; if (spf != null) break; }
                if (spf == null) return;
                if (_fPrisoner == null) _fPrisoner = AccessTools.Field(typeof(SetPrisonerFreeBarterable), "_prisonerCharacter");
                var p = _fPrisoner != null ? _fPrisoner.GetValue(spf) as Hero : null;
                if (p == null || !p.IsPrisoner) return;
                __state = new BarterState { Prisoner = p, Captor = __2, Payer = __3, CaptorGold = __2 != null ? __2.Gold : 0, PayerGold = __3 != null ? __3.Gold : 0 };
            }
            catch (Exception e) { Stumble("BarterPre", e); }
        }

        public static void BarterPost(BarterState __state)
        {
            try
            {
                if (__state == null || __state.Prisoner.IsPrisoner) return;   // barter nie doszedl - jeniec dalej w niewoli
                long paid = __state.Payer != null ? __state.PayerGold - __state.Payer.Gold : 0;
                long got = __state.Captor != null ? __state.Captor.Gold - __state.CaptorGold : 0;
                long amt = Math.Max(paid, got);
                if (amt < 0) amt = 0;
                _aiRansomN++; _aiRansomSum += amt; _aiRansomSessN++; _aiRansomSessSum += amt;
                if (amt >= _aiRansomMax)
                {
                    _aiRansomMax = amt;
                    _aiRansomMaxTxt = ClanName(__state.Payer) + " -> " + ClanName(__state.Captor) + " " + amt + " za " + __state.Prisoner.Name + " (" + RName[RankOf(__state.Prisoner)] + ")";
                }
            }
            catch (Exception e) { Stumble("BarterPost", e); }
        }

        private static string ClanName(Hero h) { try { return h != null && h.Clan != null ? h.Clan.Name.ToString() : "-"; } catch { return "?"; } }

        private static string CaptivityLine(int day, int aiClans)
        {
            var rank = new int[4]; int inDungeon = 0, inParty = 0, atPlayer = 0, over60 = 0, familyCan = 0, total = 0;
            var days = new List<double>(); double maxD = -1; string maxName = "-";
            foreach (var h in Hero.AllAliveHeroes)
            {
                if (h == null || !h.IsPrisoner || !IsLordHero(h)) continue;
                total++;
                rank[RankOf(h)]++;
                var pb = h.PartyBelongedToAsPrisoner;
                if (pb != null)
                {
                    if (pb == PartyBase.MainParty) atPlayer++;
                    else if (pb.IsSettlement) inDungeon++;
                    else inParty++;
                }
                double d = Math.Max(0, h.CaptivityStartTime.ElapsedDaysUntilNow);
                days.Add(d);
                if (d > 60) over60++;
                if (d > maxD) { maxD = d; maxName = h.Name + " (" + ClanName(h) + ")"; }
                var lead = h.Clan.Leader;
                if (lead != null && lead != h && lead.Gold > 20000) familyCan++;
            }
            days.Sort();
            double med = days.Count > 0 ? days[days.Count / 2] : 0;
            _sessDays++;
            double perClanYear = aiClans > 0 && _sessDays > 0 ? (double)_capSess / aiClans / _sessDays * 364.0 : 0;
            long dPlayer = ClanIncomeBook.InflowOf(Clan.PlayerClan);
            // przeglad 169c: D gracza = D staly (W-1, projekt rozdz. "169c"); bez pomiaru czesci (-1) - D169 z dopiskiem
            double pd = ClanIncomeBook.StableD(Clan.PlayerClan);
            string pdKind = "D staly";
            if (pd < 0) { pd = ClanIncomeBook.StableIncome(Clan.PlayerClan); pdKind = "D169 - brak D stalego"; }
            var ml = CirculationWindows.In;
            var sb = new StringBuilder(1200);
            sb.Append("Niewola lordow i okupy (169c): dzien ").Append(day)
              .Append(" | w niewoli teraz ").Append(total).Append(" (krol ").Append(rank[RKing]).Append(", dziecko krola ").Append(rank[RHeir]).Append(", glowa rodu ").Append(rank[RHead])
              .Append(", lord ").Append(rank[RLord]).Append("; w lochach ").Append(inDungeon).Append(", w partiach ").Append(inParty).Append(", u gracza ").Append(atPlayer)
              .Append("; glowa rodu jenca ma > 20 000: ").Append(familyCan).Append(')')
              .Append(" | dni w niewoli: mediana ").Append(med.ToString("0", Inv)).Append(", ponad 60 dni ").Append(over60).Append(", najdluzej ").Append(maxD >= 0 ? maxD.ToString("0", Inv) + " (" + maxName + ")" : "-")
              .Append(" | pojmania dzis ").Append(_capToday[0] + _capToday[1] + _capToday[2] + _capToday[3]).Append(" (krol ").Append(_capToday[RKing]).Append(", dziecko krola ").Append(_capToday[RHeir])
              .Append(", glowa ").Append(_capToday[RHead]).Append(", lord ").Append(_capToday[RLord]).Append("), od startu sesji ").Append(_capSess).Append(" w ").Append(_sessDays)
              .Append(" dobach = ").Append(perClanYear.ToString("0.00", Inv)).Append(" na rod AI w roku (rodow AI ").Append(aiClans).Append(')')
              .Append(" | uwolnieni dzis: okup ").Append(_relToday[(int)EndCaptivityDetail.Ransom]).Append(", pokoj ").Append(_relToday[(int)EndCaptivityDetail.ReleasedAfterPeace])
              .Append(", ucieczka ").Append(_relToday[(int)EndCaptivityDetail.ReleasedAfterEscape]).Append(", po bitwie ").Append(_relToday[(int)EndCaptivityDetail.ReleasedAfterBattle])
              .Append(", z wyboru ").Append(_relToday[(int)EndCaptivityDetail.ReleasedByChoice]).Append(", zmarli ").Append(_relToday[(int)EndCaptivityDetail.Death])
              .Append(" | okupy AI-AI (barter gry) dzis ").Append(_aiRansomN).Append(" na ").Append(_aiRansomSum).Append(" zl (najwiekszy: ").Append(_aiRansomMaxTxt ?? "-").Append("), od startu sesji ")
              .Append(_aiRansomSessN).Append(" na ").Append(_aiRansomSessSum).Append(" zl")
              .Append(" | okup gracza z menu niewoli dzis ").Append(RansomFlows.DayPlayerN).Append(" na ").Append(RansomFlows.DayPlayerPaid).Append(" zl (w nicosc ").Append(RansomFlows.DayPlayerToNothing)
              .Append("), od startu sesji ").Append(RansomFlows.SessPlayerPaid).Append(" zl = ").Append(pd > 0 ? (RansomFlows.SessPlayerPaid / pd).ToString("0.0", Inv) : "-").Append(" dni D gracza (")
              .Append(pdKind).Append(' ').Append(pd > 0 ? pd.ToString("0", Inv) : "-").Append(", wplyw doby ").Append(dPlayer).Append(')')
              .Append(" | kurier okupu dzis ").Append(RansomFlows.DayCourierN).Append(": gracz dostal ").Append(RansomFlows.DayCourierIn).Append(", gracz zaplacil ").Append(RansomFlows.DayCourierOut)
              .Append("; dosypka gry placacemu AI (z niczego) ").Append(RansomFlows.DayTopUp).Append(" w ").Append(RansomFlows.DayTopUpN).Append(" ofertach (sesja ").Append(RansomFlows.SessTopUp).Append(')')
              .Append(" | posrednik jencow z niczego (ksiega obiegu): jency sprzedani przez partie ").Append(CirculationWindows.On ? ml[CirculationWindows.KPrisonersParty].ToString(Inv) : "-")
              .Append(", jency do kas twierdz ").Append(CirculationWindows.On ? ml[CirculationWindows.KPrisonersFort].ToString(Inv) : "-")
              .Append(RansomFlows.Stumbles > 0 ? " | potkniecia okupow " + RansomFlows.Stumbles : "").Append('.');
            Array.Clear(_capToday, 0, _capToday.Length); Array.Clear(_relToday, 0, _relToday.Length);
            _aiRansomN = 0; _aiRansomSum = 0; _aiRansomMax = 0; _aiRansomMaxTxt = null;
            RansomFlows.ZeroDay();
            return sb.ToString();
        }

        // ============================================================ ZALOGI: PRZYROST BEZ WERBUNKU
        private static long _gNotables, _gModel, _gFromParty, _gToParty, _gBkPatrol, _garLast = -1, _milLast = -1;

        private static int GarMen(Town t)
        {
            var g = t != null ? t.GarrisonParty : null;
            return g != null && g.MemberRoster != null ? g.MemberRoster.TotalManCount : 0;
        }

        public static void TownPre(Town __0, out int __state) { __state = 0; try { __state = GarMen(__0); } catch { } }
        public static Exception NotablesFin(Exception __exception, Town __0, int __state) { try { if (On) _gNotables += GarMen(__0) - __state; } catch (Exception e) { Stumble("NotablesFin", e); } return __exception; }
        public static Exception ModelFin(Exception __exception, Town __0, int __state) { try { if (On) _gModel += GarMen(__0) - __state; } catch (Exception e) { Stumble("ModelFin", e); } return __exception; }

        public static void StPre(Settlement __1, out int __state) { __state = 0; try { __state = GarMen(__1 != null ? __1.Town : null); } catch { } }
        public static Exception LeaveFin(Exception __exception, Settlement __1, int __state) { try { if (On) _gFromParty += GarMen(__1 != null ? __1.Town : null) - __state; } catch (Exception e) { Stumble("LeaveFin", e); } return __exception; }
        public static Exception TakeFin(Exception __exception, Settlement __1, int __state) { try { if (On) _gToParty += __state - GarMen(__1 != null ? __1.Town : null); } catch (Exception e) { Stumble("TakeFin", e); } return __exception; }
        public static Exception PatrolFin(Exception __exception, Settlement __1, int __state) { try { if (On) _gBkPatrol += GarMen(__1 != null ? __1.Town : null) - __state; } catch (Exception e) { Stumble("PatrolFin", e); } return __exception; }

        private static string GarrisonLine(int day)
        {
            long gar = 0, garTowns = 0, mil = 0;
            foreach (var st in Settlement.All)
            {
                if (st == null) continue;
                if (st.Town != null) { int m = GarMen(st.Town); gar += m; if (st.IsTown) garTowns += m; }
                var mc = st.MilitiaPartyComponent;
                if (mc != null && mc.MobileParty != null && mc.MobileParty.MemberRoster != null) mil += mc.MobileParty.MemberRoster.TotalManCount;
            }
            bool first = _garLast < 0;
            long dGar = first ? 0 : gar - _garLast, dMil = first ? 0 : mil - _milLast;
            long named = _gNotables + _gModel + _gFromParty - _gToParty + _gBkPatrol;
            var sb = new StringBuilder(600);
            sb.Append("Zalogi: przyrost bez werbunku (169c): dzien ").Append(day)
              .Append(" | zalogi ").Append(gar).Append(" ludzi (w miastach ").Append(garTowns).Append(", w zamkach ").Append(gar - garTowns).Append(')')
              .Append(first ? " - pierwsza doba sesji, zmiany od nastepnej" : ", zmiana od wczoraj " + S(dGar))
              .Append(" | werbunek gry od notabli ").Append(S(_gNotables)).Append(" | przyrost z modelu zalogi gry (bez werbunku i bez zlota) ").Append(S(_gModel))
              .Append(" | z partii do zalogi ").Append(S(_gFromParty)).Append(", z zalogi do partii ").Append(S(-_gToParty)).Append(" | patrole BK wracajace do zalogi ").Append(S(_gBkPatrol))
              .Append(" | reszta (bitwy, oblezenia, zmiana wlasciciela, dezercja, jency, ROT, nasze moduly) ").Append(first ? "-" : S(dGar - named))
              .Append(" | milicja ").Append(mil).Append(first ? "" : " (" + S(dMil) + ")").Append('.');
            _garLast = gar; _milLast = mil;
            _gNotables = _gModel = _gFromParty = _gToParty = _gBkPatrol = 0;
            return sb.ToString();
        }

        private static string S(long v) { return v >= 0 ? "+" + v.ToString(Inv) : v.ToString(Inv); }

        // ============================================================ KASY MIAST
        private static readonly Dictionary<Town, long> _regOutTown = new Dictionary<Town, long>(), _regInTown = new Dictionary<Town, long>();

        /// <summary>MoneyLedger.RegulatorPostfix: dzienny wynik regulatora kasy jednej osady (dosypka + / kasowanie -).</summary>
        internal static void NoteRegulator(Town t, int result)
        {
            try
            {
                if (t == null || !On) return;
                var d = result >= 0 ? _regInTown : _regOutTown;
                long v; d.TryGetValue(t, out v); d[t] = v + Math.Abs((long)result);
            }
            catch (Exception e) { Stumble("NoteRegulator", e); }
        }

        private const string CsvTowns = "dzien;dzien_gry;miasto_id;miasto;rod;krolestwo;kasa;cel_regulatora;kasa_do_celu;skasowane_regulator;dosypka_regulator;pod_tarcza;zawor_do_pana;dobrobyt";

        private static string TownsLine(int day, int gameDay)
        {
            var csv = new StringBuilder(8192);
            long gold = 0, target = 0, cut = 0, add = 0, held = 0, valve = 0; int n = 0, over = 0, under = 0, half = 0;
            var ratios = new List<double>();
            foreach (var st in Settlement.All)
            {
                try
                {
                    if (st == null || !st.IsTown || st.Town == null) continue;
                    var t = st.Town;
                    long g = t.Gold, z = 10000 + (long)(12f * t.Prosperity);
                    long c0; _regOutTown.TryGetValue(t, out c0);
                    long a0; _regInTown.TryGetValue(t, out a0);
                    long h = SoldierPay.HeldOf(st);
                    long pay, avail; PopulationLaw.RentOf(st, out pay, out avail);
                    n++; gold += g; target += z; cut += c0; add += a0; held += h; valve += pay;
                    if (g > z) over++; else under++;
                    if (g < z / 2) half++;
                    double r = z > 0 ? (double)g / z : 0; ratios.Add(r);
                    csv.Append(day).Append(';').Append(gameDay).Append(';').Append(st.StringId).Append(';').Append(Clean(st.Name.ToString())).Append(';')
                       .Append(st.OwnerClan != null ? Clean(st.OwnerClan.Name.ToString()) : "").Append(';')
                       .Append(st.OwnerClan != null && st.OwnerClan.Kingdom != null ? Clean(st.OwnerClan.Kingdom.Name.ToString()) : "").Append(';')
                       .Append(g).Append(';').Append(z).Append(';').Append(r.ToString("0.00", Inv)).Append(';').Append(c0).Append(';').Append(a0).Append(';').Append(h).Append(';').Append(pay).Append(';')
                       .Append(t.Prosperity.ToString("0", Inv)).Append(Environment.NewLine);
                }
                catch (Exception e) { Stumble("TownsLine(miasto)", e); }
            }
            try { Log.Csv("kasy-miast.csv", CsvTowns, csv.ToString()); } catch (Exception e) { Stumble("TownsLine(csv)", e); }
            ratios.Sort();
            _regOutTown.Clear(); _regInTown.Clear();
            return "Kasy miast (169c): dzien " + day + " | miast " + n + ": kasy " + gold + " wobec celu regulatora gry (10 000 + 12 x dobrobyt) " + target
                   + " (kasa/cel: mediana " + (ratios.Count > 0 ? ratios[ratios.Count / 2].ToString("0.00", Inv) : "-") + ", ponad celem " + over + ", ponizej " + under + ", ponizej polowy " + half + ")"
                   + " | regulator dzis: skasowal " + cut + ", dosypal " + add + " | pod tarcza zoldu (zold i wydatki ludzi w drodze do pana) " + held
                   + " | zawor do pana dzis " + valve + " | dwor -, do korony - (162m, 111') | plik kasy-miast.csv (miasto po miescie).";
        }

        private static string Clean(string s) { return string.IsNullOrEmpty(s) ? "" : s.Replace(';', ',').Replace('\n', ' ').Replace('\r', ' '); }

        // ============================================================ WYDATKI RYCERZY (rody gentry BK)
        private static readonly Dictionary<Clan, long> _gentrySupply = new Dictionary<Clan, long>(), _gentrySlaves = new Dictionary<Clan, long>(), _gentryLastGold = new Dictionary<Clan, long>();

        private static bool IsGentry(Clan c) { return c != null && c.StringId != null && c.StringId.StartsWith("gentryClan_", StringComparison.Ordinal); }

        public static void PayerPre(Hero __0, out int __state) { __state = 0; try { __state = __0 != null ? __0.Gold : 0; } catch { } }
        public static Exception SupplyFin(Exception __exception, Hero __0, int __state)
        {
            try { if (On && __0 != null && __0.Clan != null) { long d = __state - __0.Gold; if (d > 0) { long v; _gentrySupply.TryGetValue(__0.Clan, out v); _gentrySupply[__0.Clan] = v + d; } } }
            catch (Exception e) { Stumble("SupplyFin", e); }
            return __exception;
        }

        private static Hero OwnerOf(object estate)
        {
            try { return estate != null ? Traverse.Create(estate).Property("Owner").GetValue() as Hero : null; } catch { return null; }
        }

        public static void EstatePre(object __0, out int __state) { __state = 0; try { var o = OwnerOf(__0); __state = o != null ? o.Gold : 0; } catch { } }
        public static Exception SlavesFin(Exception __exception, object __0, int __state)
        {
            try { if (On) { var o = OwnerOf(__0); if (o != null && o.Clan != null) { long d = __state - o.Gold; if (d > 0) { long v; _gentrySlaves.TryGetValue(o.Clan, out v); _gentrySlaves[o.Clan] = v + d; } } } }
            catch (Exception e) { Stumble("SlavesFin", e); }
            return __exception;
        }

        private static string GentryLine(int day)
        {
            int n = 0, noMen = 0, poor = 0; long gold = 0, dGold = 0, inflow = 0, wage = 0, supply = 0, slaves = 0, supplyAll = 0, slavesAll = 0; bool first = _gentryLastGold.Count == 0;
            foreach (var kv in _gentrySupply) { supplyAll += kv.Value; if (IsGentry(kv.Key)) supply += kv.Value; }
            foreach (var kv in _gentrySlaves) { slavesAll += kv.Value; if (IsGentry(kv.Key)) slaves += kv.Value; }
            var now = new Dictionary<Clan, long>();
            foreach (var c in Clan.All)
            {
                try
                {
                    if (c == null || c.IsEliminated || !IsGentry(c) || c.Leader == null) continue;
                    n++;
                    long g = ClanIncomeBook.FamilyGoldOf(c);
                    gold += g; now[c] = g;
                    long last; if (_gentryLastGold.TryGetValue(c, out last)) dGold += g - last;
                    inflow += ClanIncomeBook.InflowOf(c); wage += ClanIncomeBook.WageOf(c);
                    int men = 0; var wps = c.WarPartyComponents;
                    if (wps != null) for (int i = 0; i < wps.Count; i++) { var mp = wps[i] != null ? wps[i].MobileParty : null; if (mp != null && mp.MemberRoster != null) men += mp.MemberRoster.TotalRegulars; }
                    if (men == 0) noMen++;
                    if (c.Leader.Gold < 5000) poor++;
                }
                catch (Exception e) { Stumble("GentryLine(rod)", e); }
            }
            _gentryLastGold.Clear(); foreach (var kv in now) _gentryLastGold[kv.Key] = kv.Value;
            _gentrySupply.Clear(); _gentrySlaves.Clear();
            long rest = first ? 0 : inflow - wage - supply - slaves - dGold;   // wydatki bez nazwy = wplyw - zold - nazwane - zmiana kies (bilans)
            return "Wydatki rycerzy (169c): dzien " + day + " | rody gentry BK " + n + " (bez ludzi " + noMen + ", glowy < 5000: " + poor + "), kiesy rodzin " + gold
                   + (first ? " - pierwsza doba sesji" : " (" + S(dGold) + ")")
                   + " | wplyw doby (D169) " + inflow + ", zold " + wage + " | BK zaopatrzenie wsi majatku " + supply + " (wszyscy placacy " + supplyAll + "), BK niewolnicy majatku " + slaves
                   + " (wszyscy " + slavesAll + ") | inne wydatki (bilans: wplyw - zold - nazwane - zmiana kies) " + (first ? "-" : rest.ToString(Inv)) + ".";
        }

        // ============================================================ DEZERCJA AI WEDLUG PRZYCZYNY
        private static readonly Dictionary<MobileParty, int> _wageDesert = new Dictionary<MobileParty, int>();
        // [grupa, przyczyna]: grupa 0 lordowie AI, 1 zalogi AI, 2 gracz i jego rod, 3 karawany i inne; przyczyna 0 morale, 1 morale w glodzie, 2 limit zoldu/partii
        private static readonly long[] _des = new long[12];

        public static void WagePre(TroopRoster __1, out int __state) { __state = 0; try { __state = __1 != null ? __1.TotalManCount : 0; } catch { } }
        public static Exception WageFin(Exception __exception, MobileParty __0, TroopRoster __1, int __state)
        {
            try
            {
                if (On && __0 != null && __1 != null)
                {
                    int d = __1.TotalManCount - __state;
                    if (d > 0) { int v; _wageDesert.TryGetValue(__0, out v); _wageDesert[__0] = v + d; }
                }
            }
            catch (Exception e) { Stumble("WageFin", e); }
            return __exception;
        }

        private static void OnTroopsDeserted(MobileParty mp, TroopRoster roster)
        {
            try
            {
                if (!On || mp == null || roster == null) return;
                int total = roster.TotalManCount;
                int wage; _wageDesert.TryGetValue(mp, out wage); _wageDesert.Remove(mp);
                wage = Math.Max(0, Math.Min(wage, total));
                int morale = total - wage;
                int grp = mp.ActualClan == Clan.PlayerClan || mp.IsMainParty ? 2 : mp.IsGarrison ? 1 : mp.IsLordParty ? 0 : 3;
                bool hungry = mp.Party != null && mp.Party.IsStarving;
                _des[grp * 3 + (hungry ? 1 : 0)] += morale;
                _des[grp * 3 + 2] += wage;
            }
            catch (Exception e) { Stumble("OnTroopsDeserted", e); }
        }

        private static string DesertionLine(int day)
        {
            string[] g = { "partie lordow AI", "zalogi AI", "gracz i jego rod", "karawany i inne" };
            var sb = new StringBuilder(700);
            sb.Append("Dezercja AI wedlug przyczyny (169c): dzien ").Append(day);
            long aiTotal = 0, aiMoraleUnpaid = 0;
            for (int i = 0; i < 4; i++)
            {
                long m = _des[i * 3], h = _des[i * 3 + 1], w = _des[i * 3 + 2];
                long unpaid = i == 0 ? WarLedger.LastGoneAi : i == 2 ? WarLedger.LastGoneClan : 0;
                if (i < 2) { aiTotal += m + h + w + unpaid; aiMoraleUnpaid += m + h + unpaid; }
                sb.Append(" | ").Append(g[i]).Append(": morale ").Append(m).Append(", morale w glodzie ").Append(h).Append(", limit zoldu i wielkosci partii (gra) ").Append(w);
                if (i == 0 || i == 2) sb.Append(", zalegly zold (WarLedger) ").Append(unpaid);
            }
            // przeglad 169c: prog 183 (projekt rozdz. 1) liczy "z morale i z zaleglego zoldu razem" - bez limitu zoldu gry, ktory 166 wylacza
            sb.Append(" | AI razem ").Append(aiTotal).Append(" | AI morale i zalegly zold (baza progu 183: najwyzej bieg bazowy + 50%) ").Append(aiMoraleUnpaid)
              .Append(" | prawo dezercji dla AI (DesertionLaw): ")
              .Append(Settings.Current != null && Settings.Current.DesertionLawForAi ? "tak" : "nie - gra (morale ponizej 10)").Append('.');
            Array.Clear(_des, 0, _des.Length);
            if (_wageDesert.Count > 2000) _wageDesert.Clear();
            return sb.ToString();
        }

        // ============================================================ LUDNOSC BK (KL pomiar A) i MIARA HISTORYCZNA cz. 2
        private static readonly Dictionary<Town, long> _budget = new Dictionary<Town, long>();
        private static PropertyInfo _pCfg, _pPopMgr; private static MethodInfo _mGetPop, _mTypeCount, _mPopulated; private static Type _popType; private static PropertyInfo _pTotal, _pEcon, _pConsumed;
        private static bool _popDone; private static string _popNote = "-";
        private static long _lastBkPop, _lastConsumed; private static double _lastTablePop, _ratioStart = -1;

        public static void ConsPre(Town __0, Dictionary<ItemCategory, float> __1)
        {
            try
            {
                if (!On || __0 == null || __1 == null) return;
                double s = 0; foreach (var kv in __1) if (kv.Value > 0) s += kv.Value;
                long v; _budget.TryGetValue(__0, out v); _budget[__0] = v + (long)s;
            }
            catch (Exception e) { Stumble("ConsPre", e); }
        }

        private static void ResolvePop()
        {
            if (_popDone) return;
            _popDone = true;
            try
            {
                var cfgT = AccessTools.TypeByName("BannerKings.BannerKingsConfig");
                _pCfg = cfgT != null ? AccessTools.Property(cfgT, "Instance") : null;
                _pPopMgr = cfgT != null ? AccessTools.Property(cfgT, "PopulationManager") : null;
                var pmT = _pPopMgr != null ? _pPopMgr.PropertyType : null;
                _mGetPop = pmT != null ? AccessTools.Method(pmT, "GetPopData", new[] { typeof(Settlement) }) : null;
                // przeglad 169c: GetPopData dla osady bez danych wola InitializeSettlementPops (losowanie + zapis w BK) - najpierw czysty odczyt
                _mPopulated = pmT != null ? AccessTools.Method(pmT, "IsSettlementPopulated", new[] { typeof(Settlement) }) : null;
                var pdT = _mGetPop != null ? _mGetPop.ReturnType : null;
                _mTypeCount = pdT != null ? AccessTools.Method(pdT, "GetTypeCount") : null;
                _popType = _mTypeCount != null && _mTypeCount.GetParameters().Length == 1 ? _mTypeCount.GetParameters()[0].ParameterType : null;
                _pTotal = pdT != null ? AccessTools.Property(pdT, "TotalPop") : null;
                _pEcon = pdT != null ? AccessTools.Property(pdT, "EconomicData") : null;
                _pConsumed = _pEcon != null ? AccessTools.Property(_pEcon.PropertyType, "ConsumedValue") : null;
                _popNote = _mGetPop != null && _mTypeCount != null && _popType != null && _mPopulated != null ? "BK PopulationData" : "BRAK BK PopulationData";
            }
            catch (Exception e) { Stumble("ResolvePop", e); _popNote = "BRAK (blad)"; }
        }

        private const string CsvPop = "dzien;dzien_gry;osada_id;osada;rodzaj;krolestwo;kultura;szlachta;rzemieslnicy;chlopi;niewolni;dzierzawcy;razem_bk;ludnosc_tabeli;zuzycie_bk;budzet_konsumpcji;dobrobyt_lub_hearth";
        private static readonly string[] ClassName = { "szlachta", "rzemieslnicy", "chlopi", "niewolni", "-", "dzierzawcy" };

        private static string PopulationLine(int day, int gameDay)
        {
            ResolvePop();
            object pm = null;
            try { var cfg = _pCfg != null ? _pCfg.GetValue(null, null) : null; pm = cfg != null && _pPopMgr != null ? _pPopMgr.GetValue(cfg, null) : null; } catch { pm = null; }
            var csv = new StringBuilder(65536);
            var cls = new long[6]; long total = 0, consumed = 0, budget = 0; double table = 0; int n = 0, miss = 0;
            var byK = new Dictionary<string, long[]>();   // krolestwo -> [ludnosc BK, zuzycie, budzet, ludnosc tabeli]
            object[] types = null;
            if (_popType != null) { types = new object[6]; for (int i = 0; i < 6; i++) { try { types[i] = Enum.ToObject(_popType, i); } catch { types[i] = null; } } }
            foreach (var st in Settlement.All)
            {
                try
                {
                    if (st == null || !(st.IsTown || st.IsCastle || st.IsVillage)) continue;
                    double tp = 0; try { tp = PopulationLaw.PeopleOf(st); } catch { }
                    table += tp;
                    long[] c = new long[6]; long tot = 0, cons = 0;
                    object pd = null;
                    if (pm != null && _mGetPop != null && _mPopulated != null)
                    {
                        try { pd = (bool)_mPopulated.Invoke(pm, new object[] { st }) ? _mGetPop.Invoke(pm, new object[] { st }) : null; } catch { pd = null; }
                    }
                    if (pd == null) { miss++; }
                    else
                    {
                        if (types != null && _mTypeCount != null)
                            for (int i = 0; i < 6; i++) { if (i == 4 || types[i] == null) continue; try { c[i] = Convert.ToInt64(_mTypeCount.Invoke(pd, new[] { types[i] })); } catch { } }
                        try { tot = _pTotal != null ? Convert.ToInt64(_pTotal.GetValue(pd, null)) : c[0] + c[1] + c[2] + c[3] + c[5]; } catch { }
                        try { var ec = _pEcon != null ? _pEcon.GetValue(pd, null) : null; cons = ec != null && _pConsumed != null ? Convert.ToInt64(_pConsumed.GetValue(ec, null)) : 0; } catch { }
                    }
                    long bud = 0; if (st.Town != null) _budget.TryGetValue(st.Town, out bud);
                    n++; total += tot; consumed += cons; budget += bud;
                    for (int i = 0; i < 6; i++) cls[i] += c[i];
                    var k = st.OwnerClan != null && st.OwnerClan.Kingdom != null ? st.OwnerClan.Kingdom.Name.ToString() : "(bez krolestwa)";
                    long[] kv; if (!byK.TryGetValue(k, out kv)) { kv = new long[4]; byK[k] = kv; }
                    kv[0] += tot; kv[1] += cons; kv[2] += bud; kv[3] += (long)tp;
                    float pros = st.Town != null ? st.Town.Prosperity : (st.Village != null ? st.Village.Hearth : 0f);
                    csv.Append(day).Append(';').Append(gameDay).Append(';').Append(st.StringId).Append(';').Append(Clean(st.Name.ToString())).Append(';')
                       .Append(st.IsTown ? "miasto" : st.IsCastle ? "zamek" : "wies").Append(';').Append(Clean(k)).Append(';').Append(st.Culture != null ? st.Culture.StringId : "").Append(';')
                       .Append(c[0]).Append(';').Append(c[1]).Append(';').Append(c[2]).Append(';').Append(c[3]).Append(';').Append(c[5]).Append(';').Append(tot).Append(';').Append((long)tp).Append(';')
                       .Append(cons).Append(';').Append(bud).Append(';').Append(pros.ToString("0", Inv)).Append(Environment.NewLine);
                }
                catch (Exception e) { Stumble("PopulationLine(osada)", e); }
            }
            try { Log.Csv("ludnosc-bk.csv", CsvPop, csv.ToString()); } catch (Exception e) { Stumble("PopulationLine(csv)", e); }
            _budget.Clear();
            _lastBkPop = total; _lastConsumed = consumed; _lastTablePop = table;
            var ks = new List<KeyValuePair<string, long[]>>(byK);
            ks.Sort((a, b) => b.Value[0].CompareTo(a.Value[0]));
            var parts = new List<string>();
            for (int i = 0; i < ks.Count && i < 30; i++)
                parts.Add(ks[i].Key + " " + ks[i].Value[0] + " (tabela " + ks[i].Value[3] + ", zuzycie " + ks[i].Value[1] + ", budzet " + ks[i].Value[2] + ")");
            return "Ludnosc BK (169c): dzien " + day + " | osad " + n + " (bez danych BK " + miss + ", " + _popNote + ") | ludnosc BK " + total + ": " + ClassName[0] + " " + cls[0] + ", " + ClassName[1] + " " + cls[1]
                   + ", " + ClassName[2] + " " + cls[2] + ", " + ClassName[3] + " " + cls[3] + ", " + ClassName[5] + " " + cls[5] + "; ludnosc tabeli PopulationLaw " + table.ToString("0", Inv)
                   + " | zuzycie BK (ConsumedValue) razem " + consumed + ", budzet konsumpcji gry (suma popytu kategorii) razem " + budget
                   + " | wedlug krolestw: " + string.Join("; ", parts.ToArray()) + " | wartosc wyrobow warsztatow i karawany w miastach - (w ksiegach 147/148/WorkshopLaw i 174b) | plik ludnosc-bk.csv.";
        }

        // ============================================================ SLUBY AI
        private static int _marN, _marPoorRich; private static long _marDowry;

        private static void OnMarried(Hero a, Hero b, bool show)
        {
            try
            {
                if (!On || a == null || b == null || a.Clan == null || b.Clan == null) return;
                if (a.Clan == Clan.PlayerClan || b.Clan == Clan.PlayerClan) return;
                _marN++;
                long ga = ClanIncomeBook.FamilyGoldOf(a.Clan), gb = ClanIncomeBook.FamilyGoldOf(b.Clan);
                double da = Math.Max(0, ClanIncomeBook.StableIncome(a.Clan)), db = Math.Max(0, ClanIncomeBook.StableIncome(b.Clan));
                if ((ga < 20000 && gb > 60 * db) || (gb < 20000 && ga > 60 * da)) _marPoorRich++;
                var bride = a.IsFemale ? a : (b.IsFemale ? b : null);
                if (bride != null)
                {
                    long g = bride == a ? ga : gb; double d = bride == a ? da : db;
                    _marDowry += (long)Math.Max(0, Math.Min(0.25 * g, 90 * d));   // posag hipotetyczny (05 D2): min(25% G, 90 D) rodu panny
                }
            }
            catch (Exception e) { Stumble("OnMarried", e); }
        }

        private static string MarriageLine(int day)
        {
            string s = "Sluby AI (169c): dzien " + day + " | slubow AI-AI " + _marN + ", w tym biedny z bogatym (G < 20 000 i G > 60 D) " + _marPoorRich
                       + " | posag hipotetyczny razem " + _marDowry + " zl (min(25% G, 90 D) rodu panny; bez wyceny BK GetDowryValue).";
            _marN = _marPoorRich = 0; _marDowry = 0;
            return s;
        }

        // ============================================================ TOWAR WEDROWCOW BK
        private static readonly Dictionary<MobileParty, long> _travLast = new Dictionary<MobileParty, long>();

        private static bool IsTraveller(MobileParty mp)
        {
            var pc = mp != null ? mp.PartyComponent : null;
            return pc != null && pc.GetType().Name == "PopulationPartyComponent";
        }

        private static string TravellerLine(int day)
        {
            int n = 0, trade = 0, newN = 0, goneN = 0; long value = 0, mats = 0, newV = 0, goneV = 0;
            var now = new Dictionary<MobileParty, long>();
            foreach (var mp in MobileParty.All)
            {
                try
                {
                    if (mp == null || !mp.IsActive || !IsTraveller(mp)) continue;
                    n++;
                    try { if (Traverse.Create(mp.PartyComponent).Property("Trading").GetValue<bool>()) trade++; } catch { }
                    long v = 0;
                    var r = mp.ItemRoster;
                    if (r != null)
                        for (int i = 0; i < r.Count; i++)
                        {
                            var el = r.GetElementCopyAtIndex(i);
                            var it = el.EquipmentElement.Item;
                            if (it == null || el.Amount <= 0) continue;
                            long x = (long)it.Value * el.Amount;
                            v += x;
                            var id = it.StringId ?? "";
                            if (id.IndexOf("ingot", StringComparison.OrdinalIgnoreCase) >= 0 || id.IndexOf("charcoal", StringComparison.OrdinalIgnoreCase) >= 0) mats += x;
                        }
                    value += v; now[mp] = v;
                    if (!_travLast.ContainsKey(mp)) { newN++; newV += v; }
                }
                catch (Exception e) { Stumble("TravellerLine(partia)", e); }
            }
            bool first = _travLast.Count == 0 && _sessDays <= 1;
            foreach (var kv in _travLast) if (!now.ContainsKey(kv.Key)) { goneN++; goneV += kv.Value; }
            _travLast.Clear(); foreach (var kv in now) _travLast[kv.Key] = kv.Value;
            return "Towar wedrowcow BK (169c): dzien " + day + " | partii ludnosci BK " + n + " (handlarze " + trade + ") | towar w taborach " + value + " zl (wartosc gry; sztaby i wegiel " + mats + ")"
                   + (first ? " - pierwsza doba sesji" : " | nowe od wczoraj " + newN + " z towarem " + newV + " zl (towar nadany przy starcie partii - z niczego), zniknely " + goneN + " z towarem " + goneV + " zl (ostatni stan)") + ".";
        }

        // ============================================================ WZROST INNYCH
        private static readonly Dictionary<MobileParty, int> _othersLast = new Dictionary<MobileParty, int>();
        private static long _othersBattle;

        private static bool IsOthers(MobileParty mp)
        {
            try { return mp != null && ((mp.ActualClan != null && mp.ActualClan.StringId == "ROTclan_126") || Undead.Party(mp)); } catch { return false; }
        }

        private static int OthersMen(MapEvent me)
        {
            int s = 0;
            if (me == null) return 0;
            foreach (var p in me.InvolvedParties) { var mp = p != null ? p.MobileParty : null; if (mp != null && IsOthers(mp) && mp.MemberRoster != null) s += mp.MemberRoster.TotalManCount; }
            return s;
        }

        public static void OthersPre(MapEvent __0, out int __state) { __state = 0; try { if (On) __state = OthersMen(__0); } catch { } }
        public static Exception OthersFin(Exception __exception, MapEvent __0, int __state) { try { if (On) _othersBattle += OthersMen(__0) - __state; } catch (Exception e) { Stumble("OthersFin", e); } return __exception; }

        private static string OthersLine(int day)
        {
            int n = 0, newN = 0, goneN = 0; long men = 0, newMen = 0, goneMen = 0, grown = 0;
            var now = new Dictionary<MobileParty, int>();
            foreach (var mp in MobileParty.All)
            {
                if (mp == null || !mp.IsActive || !IsOthers(mp) || mp.MemberRoster == null) continue;
                int m = mp.MemberRoster.TotalManCount;
                n++; men += m; now[mp] = m;
                int last;
                if (_othersLast.TryGetValue(mp, out last)) grown += m - last; else { newN++; newMen += m; }
            }
            bool first = _othersLast.Count == 0 && _sessDays <= 1;
            foreach (var kv in _othersLast) if (!now.ContainsKey(kv.Key)) { goneN++; goneMen += kv.Value; }
            _othersLast.Clear(); foreach (var kv in now) _othersLast[kv.Key] = kv.Value;
            string s = "Wzrost Innych (169c): dzien " + day + " | partii Innych " + n + ", ludzi " + men
                       + (first ? " - pierwsza doba sesji" : " | zmiana w starych partiach " + S(grown) + " (w tym po bitwach - nekromancja ROT w oknie OnMapEventEnded " + S(_othersBattle)
                                                            + ", reszta: dosypki ROT, ochotnicy z mapy, straty " + S(grown - _othersBattle) + "), nowe partie " + newN + " z " + newMen
                                                            + " ludzi (narodziny band), zniknely " + goneN + " z " + goneMen + " ludzi")
                       + " | blokady dosypek z niczego - linia CrashScribe \"Inni\" (T2b).";
            _othersBattle = 0;
            return s;
        }

        // ============================================================ MIARA HISTORYCZNA cz. 2 (T6 a, b, e, f)
        private static readonly Dictionary<string, int> _twinsEnter = new Dictionary<string, int>();
        private static Settlement _twins; private static bool _twinsLooked;

        private static void OnSettlementEntered(MobileParty mp, Settlement st, Hero h)
        {
            try
            {
                if (!On || st == null || mp == null) return;
                if (!_twinsLooked)
                {
                    _twinsLooked = true;
                    foreach (var s in Settlement.All) { if (s != null && s.Name != null && s.Name.ToString().IndexOf("Twins", StringComparison.OrdinalIgnoreCase) >= 0) { _twins = s; break; } }
                }
                if (_twins == null || st != _twins) return;
                string k = mp.IsMainParty ? "gracz" : mp.IsCaravan ? "karawany" : mp.IsVillager ? "wiesniacy" : mp.IsLordParty ? "lordowie" : mp.IsBandit ? "bandy" : "inne";
                int v; _twinsEnter.TryGetValue(k, out v); _twinsEnter[k] = v + 1;
            }
            catch (Exception e) { Stumble("OnSettlementEntered", e); }
        }

        private static string HistoryLine(int day, List<Clan> ai)
        {
            double lords = 0;
            for (int i = 0; i < ai.Count; i++) { double d = ClanIncomeBook.StableD(ai[i]); if (d > 0) lords += d; }
            double perBk = _lastBkPop > 0 ? lords * 364 / _lastBkPop : 0, perTab = _lastTablePop > 0 ? lords * 364 / _lastTablePop : 0;
            double folkBk = _lastBkPop > 0 ? (double)_lastConsumed * 364 / _lastBkPop : 0;
            double ratio = _lastBkPop > 0 ? _lastTablePop / _lastBkPop : 0;
            if (_ratioStart < 0 && ratio > 0) _ratioStart = ratio;
            var tw = new List<string>(); int twAll = 0;
            foreach (var kv in _twinsEnter) { tw.Add(kv.Key + " " + kv.Value); twAll += kv.Value; }
            _twinsEnter.Clear();
            string model = "-";
            try { var m = Campaign.Current.Models.VillageProductionCalculatorModel; model = m != null ? m.GetType().FullName : "brak"; } catch { }
            return "Miara historyczna cz. 2 (169c): dzien " + day
                   + " | (a) dochod panow (D staly rodow AI) " + lords.ToString("0", Inv) + " zl/dobe = " + perBk.ToString("0.0", Inv) + " zl rocznie na glowe BK, " + perTab.ToString("0.0", Inv)
                   + " na glowe tabeli; lud (zuzycie BK) " + folkBk.ToString("0.0", Inv) + " zl rocznie na glowe BK; panowie / lud " + (folkBk > 0 ? (perBk / folkBk).ToString("0.00", Inv) : "-")
                   + " | (b) dryf tabeli: ludnosc tabeli / BK " + ratio.ToString("0.00", Inv) + " (na starcie sesji " + (_ratioStart > 0 ? _ratioStart.ToString("0.00", Inv) : "-") + ")"
                   + " | (e) Bliznaki: wejscia do osady " + (_twins != null ? twAll + (tw.Count > 0 ? " (" + string.Join(", ", tw.ToArray()) + ")" : "") : "- (osady nie znaleziono)")
                   + " | (f) model produkcji wsi " + model + ".";
        }

        // ============================================================ raz na dobe (po ksiedze rodow)
        internal static void Daily()
        {
            if (!On || Campaign.Current == null) { RansomFlows.ZeroDay(); return; }
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            int gameDay = (int)CampaignTime.Now.ToDays, day = gameDay - 1;
            var ai = new List<Clan>();
            try
            {
                foreach (var c in Clan.All)
                {
                    if (c == null || c.IsEliminated || c.IsBanditFaction || c.Leader == null || c == Clan.PlayerClan) continue;
                    if (c.StringId != null && c.StringId.StartsWith("bk_courtiers_", StringComparison.Ordinal)) continue;
                    if (ClanIncomeBook.IsUndeadClan(c)) continue;
                    ai.Add(c);
                }
            }
            catch (Exception e) { Stumble("Daily(rody)", e); }
            try { Log.Info(CaptivityLine(day, ai.Count)); } catch (Exception e) { Stumble("CaptivityLine", e); }
            try { Log.Info(GarrisonLine(day)); } catch (Exception e) { Stumble("GarrisonLine", e); }
            try { Log.Info(TownsLine(day, gameDay)); } catch (Exception e) { Stumble("TownsLine", e); }
            try { Log.Info(GentryLine(day)); } catch (Exception e) { Stumble("GentryLine", e); }
            try { Log.Info(DesertionLine(day)); } catch (Exception e) { Stumble("DesertionLine", e); }
            try { Log.Info(PopulationLine(day, gameDay)); } catch (Exception e) { Stumble("PopulationLine", e); }
            try { Log.Info(MarriageLine(day)); } catch (Exception e) { Stumble("MarriageLine", e); }
            try { Log.Info(TravellerLine(day)); } catch (Exception e) { Stumble("TravellerLine", e); }
            try { Log.Info(OthersLine(day)); } catch (Exception e) { Stumble("OthersLine", e); }
            try { Log.Info(HistoryLine(day, ai)); } catch (Exception e) { Stumble("HistoryLine", e); }
            if (_stumbles > 0) { try { Log.Info("Pomiary 169c: potkniecia " + _stumbles + " (pierwszy blad kazdego miejsca w logu)."); } catch { } }
            // przeglad 169c: koszt pomiaru (prog projektu: najwyzej +3% czasu doby; 13.1 s/dobe -> ok. 390 ms). Okna Harmony (prefiksy i finalizery
            // w ciagu doby) tu nie wchodza - ich koszt widac tylko w czasie doby calego biegu wobec sklad8
            try
            {
                double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                double f = 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                double sb = ClanIncomeBook.LastTicksStableBegin * f, sc = ClanIncomeBook.LastTicksStableClans * f, sl = ClanIncomeBook.LastTicksStableLine * f;
                Log.Info("Pomiary 169c: czas dzien " + day + " | linie pomiaru " + ms.ToString("0", Inv) + " ms | D staly w ksiedze rodow " + (sb + sc + sl).ToString("0", Inv)
                         + " ms (poczatek doby " + sb.ToString("0", Inv) + ", rozbicie rodow " + sc.ToString("0", Inv) + ", linia " + sl.ToString("0", Inv)
                         + ") | razem " + (ms + sb + sc + sl).ToString("0", Inv) + " ms na dobe (prog: <= 3% czasu doby, ok. 390 ms przy 13.1 s; dochod modelu z opisami - linia \"D staly\" i \"Obieg\").");
            }
            catch { }
        }
    }
}
