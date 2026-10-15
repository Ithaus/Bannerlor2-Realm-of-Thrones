using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.TournamentGames;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// E8/D2 (257, PLAN 8.18 b; projekt etapu 8 v2 rozdz. 2 R1 i rozdz. 3 "E8/D2"; krytyka K1, W3, W4, D1, D2) - NAGRODA TURNIEJU GRACZA, ZAKLADY,
    /// OPLATY I DROBNE ZRODLA GRACZA. Poza zakresem (v2): jency sprzedani miastu i nagroda turnieju AI - to juz RealPayer (E3/1, TownPaysRewards).
    ///
    /// Dzis (sprawdzone w IL zywych bibliotek 1.4.8: TaleWorlds.CampaignSystem, SandBox, NavalDLC, TournamentsXPanded v1.4.1, RBMTournament):
    /// gracz placi "w nicosc" (GiveGoldAction(gracz, nikt)) i dostaje "z niczego" (GiveGoldAction(nikt, gracz)) w kilkunastu miejscach poza zadaniami.
    /// Kazde miejsce: transpiler zamienia SAMO wywolanie GiveGoldAction w metodzie gry na nasz wrapper o tej samej sygnaturze (wzor E8/K, E8/D1 -
    /// "jeden wrapper", rozdz. 5.1); wrapper poza kierunkiem "gracz -> nikt" / "nikt -> gracz" albo przy wylaczonym kluczu wola gre bez zmian.
    ///   PlayerTourneyPrizeFromTown: przedmiot nagrody turnieju (TournamentManager.GivePrizeToWinner - takze sciezka RBMTournament, ktora dodaje sztuke
    ///     sama) schodzi z polki miasta-gospodarza (ta sama rzecz; gdy na polce jest w innym stanie - gracz dostaje te z polki); nie ma jej na polce -
    ///     zamiast przedmiotu cena z kasy miasta z nadwyzki (Seal31.Spare, jak u AI). Migawka sakw przed wydaniem: RealPayer.TourneyPre (ten sam
    ///     kontekst - bez drugiego prefiksu przed RealPayer i RBM), rozliczenie w postfiksie. Zloto Tournaments XPanded (nagroda 700 + 100 / lorda
    ///     i premie meczu) - z kasy miasta z nadwyzki.
    ///   PlayerBetsCounterparty: zaklad turniejowy - jeden kontrahent, kasa miasta: postawienie do kasy, przegrana zostaje w kasie (gra dopisywala ja
    ///     kasie drugi raz z niczego), wygrana z kasy (stawka gracza lezaca w kasie wraca zawsze, zysk z nadwyzki). Gra planszowa w karczmie: zaklad
    ///     jest tylko z gospodarzem gry (nie-bohater bez kiesy - SB BoardGameCampaignBehavior: z bohaterem zloto nie plynie), wiec kontrahent = kasa
    ///     osady (ta sama regula co zaklad turniejowy).
    ///   PlayerFeesHistorical: lapowka strazy (DefaultBribeCalculationModel.GetBribeInternal - takze poslaniec Diplomacy) po kursie 5 000 starej monety
    ///     = 30 dni placy straznika (60 / 36 / 6, nie mniej niz 3 dni), lapowka przy odbijaniu wieznia = straznicy lochu x 30 dni placy x czynnik
    ///     lotrostwa gry (setki zamiast tysiecy) - obie do sakiewki zalogi (MenPurse; przez kiese partii zalogi - zdarzenie gry bohater -> partia),
    ///     bez zalogi do kasy osady; golibroda 3, wiesci karczmarza 12, kolejka 0.3 na czlowieka, napitek 2 (bez zmian) - do kasy miasta i z niej do
    ///     kiesy ludu (TownFolk.EarnFromTown, uslugi).
    ///   PlayerSmallSourcesClosed: nagroda areny treningowej z kasy miasta (nadwyzka); zloto za wymuszone zapasy wsi (hearth x 4) z kiesy wsi do jej
    ///     wysokosci; NavalDLC "Coastal Guard Edict" - drugie rozliczenie zalogi portowej (zold drugi raz w nicosc, dopelnienie kiesy z niczego,
    ///     +15% dopelnienia rodowi z niczego) pominiete. Zwrot ceny warsztatu gracza zamyka juz WorkshopTrade (kupiec placi z wlasnej kiesy).
    /// Bez doplat: platnik bez pieniedzy = mniej i komunikat. Wyjatek w naszym kodzie = wywolanie gry + licznik potkniec, nic nie gasimy.
    /// Stan: liczniki doby i stawka zakladu w toku (pamiec; Reset z WorldMeasure.Reset). Nic w zapisie. Latki raz na proces, w kampanii.
    /// </summary>
    internal static class PlayerFees8
    {
        private const int GuardDay = 2;              // dniowka straznika (RealisticCaptivity GuardPayBase)
        private const int DaysWar = 30, DaysMin = 3; // 5 000 starej monety = 30 dni placy (60); najmniej 3 dni (6) - rozpoznanie B 5.5: 60 / 36 / 6
        private const float OldToNew = (DaysWar * GuardDay) / 5000f;
        private const int BarberOld = 100, BarberNew = 3, ClanInfoOld = 500, ClanInfoNew = 12;
        private const float TunPerMan = 0.3f;        // litr piwa na czlowieka
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static bool Key(Func<Settings, bool> k) { var s = Settings.Current; return s != null && k(s); }
        internal static bool PrizeOn { get { return Key(s => s.PlayerTourneyPrizeFromTown); } }
        internal static bool BetsOn { get { return Key(s => s.PlayerBetsCounterparty); } }
        internal static bool FeesOn { get { return Key(s => s.PlayerFeesHistorical); } }
        internal static bool SmallOn { get { return Key(s => s.PlayerSmallSourcesClosed); } }

        // ------------------------------------------------------------ latki

        private sealed class Spec
        {
            public string Label, TypeName, Method;
            public MethodBase M;
            public MethodInfo From, To; public bool Arg0;     // podmiana wywolania; Arg0: przed nim ldarg.0 (osada z argumentu metody gry)
            public MethodInfo From2, To2;                      // druga podmiana wywolania (get_tun_price)
            public int Const; public MethodInfo ConstTo;       // stala calkowita -> wywolanie
            public int WantCalls, WantConsts, Calls, Consts;
            public string Pre, Post;                           // zwykla latka zamiast transpilera
            public bool Optional;                              // cudzy mod: brak typu to nie brak latki
            public byte State;                                 // 1 wpieta, 2 brak metody, 3 blad, 4 mod nieobecny, 5 niepelna podmiana
            public string Err;
        }

        private static readonly List<Spec> _all = new List<Spec>();
        private static readonly Dictionary<IntPtr, Spec> _specs = new Dictionary<IntPtr, Spec>();
        private static Harmony _h;
        private static bool _tried;
        private static Func<int> _origTun;
        private static int _wired, _wanted, _calls, _callsWant, _consts, _constsWant;
        private static string _missText = "-", _absentText = "-";

        private static int _stumbles, _stumblesDay;
        private static readonly HashSet<string> _errSites = new HashSet<string>();

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumblesDay++;
            try { if (_errSites.Add(where)) Log.Error("PlayerFees8." + where, e); } catch { }
        }

        // ------------------------------------------------------------ liczniki doby

        private const int FBribe = 0, FBreak = 1, FBarber = 2, FClanInfo = 3, FTun = 4, FDrink = 5, Fees = 6;
        private static readonly string[] FName = { "lapowka strazy", "odbicie wieznia", "golibroda", "wiesci karczmarza", "kolejka dla druzyny", "napitek" };
        private static readonly int[] _fN = new int[Fees];
        private static readonly long[] _fSum = new long[Fees], _fGame = new long[Fees];
        private static long _toGuards, _toChest, _toTown, _toFolk, _feeVoid;

        private const int STxp = 0, SBetWon = 1, SBoardWon = 2, SArena = 3, SForce = 4, SPrize = 5, Sources = 6;
        private static readonly int[] _sN = new int[Sources], _sShortN = new int[Sources];
        private static readonly long[] _sDue = new long[Sources], _sPaid = new long[Sources], _sNothing = new long[Sources];

        private static int _bStakeN, _bLostN, _bBoardLostN;
        private static long _bStake, _bStakeVoid, _bLost, _bLostKept, _bLostNothing, _bBoardLost, _bBoardVoid, _bStale;
        private static int _pzN, _pzShelf, _pzShelfAlt, _pzNothingN, _pzNoItem;
        private static long _pzNothingVal;
        private static int _edN, _edPayN, _below;

        // stan w toku (pamiec)
        private static Settlement _stakeTown; private static long _stakeHeld;
        private static Dictionary<EquipmentElement, int> _pzSnap;
        private static int _lastBribeGame, _lastBribeNow, _lastBreakGame, _lastBreakNow, _lastTunGame, _lastTunNow;

        private static void ClearDay()
        {
            _stumblesDay = 0;
            Array.Clear(_fN, 0, Fees); Array.Clear(_fSum, 0, Fees); Array.Clear(_fGame, 0, Fees);
            _toGuards = _toChest = _toTown = _toFolk = _feeVoid = 0;
            Array.Clear(_sN, 0, Sources); Array.Clear(_sShortN, 0, Sources); Array.Clear(_sDue, 0, Sources); Array.Clear(_sPaid, 0, Sources); Array.Clear(_sNothing, 0, Sources);
            _bStakeN = _bLostN = _bBoardLostN = 0;
            _bStake = _bStakeVoid = _bLost = _bLostKept = _bLostNothing = _bBoardLost = _bBoardVoid = _bStale = 0;
            _pzN = _pzShelf = _pzShelfAlt = _pzNothingN = _pzNoItem = 0; _pzNothingVal = 0;
            _edN = _edPayN = _below = 0;
        }

        /// <summary>Nowa kampania i kazde wczytanie (WorldMeasure.Reset - konstruktor ArmouryBehavior).</summary>
        internal static void Reset()
        {
            _stumbles = 0; _errSites.Clear();
            _stakeTown = null; _stakeHeld = 0; _pzSnap = null;
            _lastBribeGame = _lastBribeNow = _lastBreakGame = _lastBreakNow = _lastTunGame = _lastTunNow = 0;
            ClearDay();
        }

        // ------------------------------------------------------------ wspolni pomocnicy

        private static bool OutDir(Hero giver, Hero recipient, int amount) { var me = Hero.MainHero; return me != null && amount > 0 && recipient == null && ReferenceEquals(giver, me); }
        private static bool InDir(Hero giver, Hero recipient, int amount) { var me = Hero.MainHero; return me != null && amount > 0 && giver == null && ReferenceEquals(recipient, me); }

        private static Settlement Here()
        {
            var st = Settlement.CurrentSettlement ?? (MobileParty.MainParty != null ? MobileParty.MainParty.CurrentSettlement : null);
            return st != null && st.SettlementComponent != null ? st : null;
        }

        private static Settlement SafeHere(string where)
        {
            try { return Here(); }
            catch (Exception e) { Stumble(where, e); return null; }
        }

        private static string NameOf(Settlement s) { return s != null && s.Name != null ? s.Name.ToString() : "The town"; }

        private static float SkillFactor()
        {
            var me = Hero.MainHero;
            int rog = me != null ? me.GetSkillValue(DefaultSkills.Roguery) : 0;
            return (1f - rog / 300f) * 0.65f + 0.35f;   // wzor gry (DefaultBribeCalculationModel.GetSkillFactor)
        }

        /// <summary>Ile osada moze wyplacic: miasto / zamek - nadwyzka (Seal31.Spare, R1), wies - kiesa; floor - stawka gracza lezaca w kasie (wraca zawsze).</summary>
        private static int CapOf(Settlement st, int due, long floor)
        {
            long gold = Math.Max(0, st.SettlementComponent.Gold);
            long cap = st.IsVillage ? gold : Math.Min(gold, Seal31.Spare(st));
            if (floor > 0) cap = Math.Max(cap, Math.Min(floor, gold));
            return (int)Math.Max(0L, Math.Min((long)due, cap));
        }

        private static void TellShort(int kind, Settlement st, int pay, int due)
        {
            try
            {
                string n = NameOf(st);
                string t;
                if (kind == SForce) t = "The villagers of " + n + " had only " + pay + " of the " + due + " denars you demanded.";
                else if (kind == SBetWon) t = n + " could pay only " + pay + " of the " + due + " denars you won on your bet.";
                else if (kind == SBoardWon) t = "The house in " + n + " could pay only " + pay + " of the " + due + " denars you won at the board.";
                else if (kind == SArena) t = n + " could pay only " + pay + " of the " + due + " denars for your bouts in the arena.";
                else t = n + " could pay only " + pay + " of the " + due + " denars of prize money.";
                Log.Player(t, true);
            }
            catch (Exception e) { Stumble("TellShort", e); }
        }

        /// <summary>nikt -> gracz: placi osada (miasto / zamek z nadwyzki, wies z kiesy); brak = mniej, komunikat. Poza kierunkiem albo przy wylaczonym kluczu - gra.</summary>
        private static void TownPays(int kind, Hero giver, Hero recipient, int amount, bool dn, bool on, Settlement st, long floor)
        {
            bool mine = false; int pay = -1; long before = 0;
            try
            {
                mine = InDir(giver, recipient, amount);
                if (mine)
                {
                    _sN[kind]++; _sDue[kind] += amount;
                    if (on && st != null && st.SettlementComponent != null) { pay = CapOf(st, amount, floor); before = st.SettlementComponent.Gold; }
                }
            }
            catch (Exception e) { Stumble("TownPays(" + kind + ")", e); pay = -1; }
            if (!mine) { GiveGoldAction.ApplyBetweenCharacters(giver, recipient, amount, dn); return; }
            if (pay < 0) { GiveGoldAction.ApplyBetweenCharacters(giver, recipient, amount, dn); _sNothing[kind] += amount; return; }   // jak gra (z niczego), licznik
            if (pay > 0) GiveGoldAction.ApplyForSettlementToCharacter(st, Hero.MainHero, pay, dn);
            _sPaid[kind] += pay;
            try
            {
                if (pay > 0 && floor <= 0 && st.IsTown && st.Town != null) { long r = Seal31.TownReserve(st.Town); if (before >= r && before - pay < r) _below++; }
                if (pay < amount) { _sShortN[kind]++; TellShort(kind, st, pay, amount); }
            }
            catch (Exception e) { Stumble("TownPays(po)", e); }
        }

        // ------------------------------------------------------------ oplaty gracza (PlayerFeesHistorical)

        private static long GameOf(int kind, int amount)
        {
            if (kind == FBribe) return amount == _lastBribeNow && _lastBribeGame > 0 ? _lastBribeGame : amount;
            if (kind == FBreak) return amount == _lastBreakNow && _lastBreakGame > 0 ? _lastBreakGame : amount;
            if (kind == FBarber) return BarberOld;
            if (kind == FClanInfo) return ClanInfoOld;
            if (kind == FTun) return amount == _lastTunNow && _lastTunGame > 0 ? _lastTunGame : amount;
            return amount;
        }

        /// <summary>gracz -> lud miasta: zdarzenie gry gracz -> kasa osady, potem kasa -> kiesa ludu (TownFolk.EarnFromTown, uslugi; przy wylaczonej kiesie
        /// ludu albo poza miastem zostaje w kasie osady).</summary>
        private static void Folk(int kind, Hero giver, Hero recipient, int amount, bool dn)
        {
            bool mine = false, on = false; Settlement st = null;
            try { mine = OutDir(giver, recipient, amount); if (mine) { on = FeesOn; st = Here(); } }
            catch (Exception e) { Stumble("Folk(stan)", e); mine = false; }
            if (!mine) { GiveGoldAction.ApplyBetweenCharacters(giver, recipient, amount, dn); return; }
            var me = Hero.MainHero;
            int had = me.Gold;
            if (!on || st == null)
            {
                GiveGoldAction.ApplyBetweenCharacters(giver, recipient, amount, dn);   // jak gra (w nicosc), licznik
                int lost = Math.Max(0, had - me.Gold);
                _fN[kind]++; _fSum[kind] += lost; _fGame[kind] += lost; _feeVoid += lost;
                return;
            }
            GiveGoldAction.ApplyForCharacterToSettlement(me, st, amount, dn);
            int paid = Math.Max(0, had - me.Gold), folk = 0;
            try
            {
                if (paid > 0 && st.IsTown && st.Town != null)
                {
                    int t0 = st.Town.Gold;
                    TownFolk.EarnFromTown(st, paid, TownFolk.KLife);
                    folk = Math.Max(0, Math.Min(paid, t0 - st.Town.Gold));
                }
                _fN[kind]++; _fSum[kind] += paid; _fGame[kind] += GameOf(kind, amount); _toFolk += folk; _toTown += paid - folk;
            }
            catch (Exception e) { Stumble("Folk(lud)", e); }
        }

        public static void FolkBarber(Hero giver, Hero recipient, int amount, bool dn) { Folk(FBarber, giver, recipient, amount, dn); }
        public static void FolkClanInfo(Hero giver, Hero recipient, int amount, bool dn) { Folk(FClanInfo, giver, recipient, amount, dn); }
        public static void FolkTun(Hero giver, Hero recipient, int amount, bool dn) { Folk(FTun, giver, recipient, amount, dn); }
        public static void FolkDrink(Hero giver, Hero recipient, int amount, bool dn) { Folk(FDrink, giver, recipient, amount, dn); }

        /// <summary>Stale oplat w metodach SandBox (transpiler stalych): golibroda 100 -> 3, wiesci karczmarza 500 -> 12; klucz wylaczony = kwota gry.</summary>
        public static int BarberFee() { return FeesOn ? BarberNew : BarberOld; }
        public static int ClanInfoFee() { return FeesOn ? ClanInfoNew : ClanInfoOld; }

        private static int GameTun()
        {
            var mp = MobileParty.MainParty;
            return (int)(50f + (mp != null && mp.MemberRoster != null ? mp.MemberRoster.TotalHealthyCount : 0) * 0.2f);   // SB TavernEmployeesCampaignBehavior.get_tun_price
        }

        /// <summary>Zamiast TavernEmployeesCampaignBehavior.get_tun_price() w trzech metodach, ktore ja wolaja: 0.3 na zdrowego czlowieka druzyny
        /// (najmniej 1, nigdy wiecej niz gra); klucz wylaczony = kwota gry.</summary>
        public static int TunPrice()
        {
            int game;
            try { game = _origTun != null ? _origTun() : GameTun(); }
            catch (Exception e) { Stumble("TunPrice(gra)", e); game = 50; }
            try
            {
                if (!FeesOn) return game;
                var mp = MobileParty.MainParty;
                int men = mp != null && mp.MemberRoster != null ? mp.MemberRoster.TotalHealthyCount : 0;
                int now = Math.Max(1, (int)Math.Round(men * TunPerMan));
                if (game > 0 && now > game) now = game;
                _lastTunGame = game; _lastTunNow = now;
                return now;
            }
            catch (Exception e) { Stumble("TunPrice", e); return game; }
        }

        /// <summary>gracz -> straz: do sakiewki zalogi osady (MenPurse) przez kiese partii zalogi (zdarzenie gry bohater -> partia; sakiewki wylaczone -
        /// zostaje w kiesie zalogi); osada bez zalogi - kasa osady.</summary>
        private static void Guards(int kind, Settlement st, Hero giver, Hero recipient, int amount, bool dn)
        {
            bool mine = false, on = false; MobileParty gar = null;
            try
            {
                mine = OutDir(giver, recipient, amount);
                if (mine)
                {
                    on = FeesOn;
                    if (st != null && st.SettlementComponent == null) st = null;
                    var g = st != null && st.Town != null ? st.Town.GarrisonParty : null;
                    if (g != null && g.IsActive && g.Party != null && g.MemberRoster != null && g.MemberRoster.TotalManCount > 0) gar = g;
                }
            }
            catch (Exception e) { Stumble("Guards(stan)", e); mine = false; }
            if (!mine) { GiveGoldAction.ApplyBetweenCharacters(giver, recipient, amount, dn); return; }
            var me = Hero.MainHero;
            int had = me.Gold;
            if (!on || st == null)
            {
                GiveGoldAction.ApplyBetweenCharacters(giver, recipient, amount, dn);   // jak gra (w nicosc), licznik
                int lost = Math.Max(0, had - me.Gold);
                _fN[kind]++; _fSum[kind] += lost; _fGame[kind] += lost; _feeVoid += lost;
                return;
            }
            if (gar != null) GiveGoldAction.ApplyForCharacterToParty(me, gar.Party, amount, dn);
            else GiveGoldAction.ApplyForCharacterToSettlement(me, st, amount, dn);
            int paid = Math.Max(0, had - me.Gold), purse = 0;
            try
            {
                if (gar != null && paid > 0 && MenPurse.On)
                {
                    int want = Math.Min(paid, Math.Max(0, gar.PartyTradeGold));
                    int p0 = MenPurse.Get(gar);
                    if (want > 0) MenPurse.Add(gar, want);
                    purse = Math.Max(0, Math.Min(want, MenPurse.Get(gar) - p0));
                    if (purse > 0) gar.PartyTradeGold -= purse;   // z kiesy zalogi schodzi tylko to, co naprawde weszlo do sakiewki
                }
                _fN[kind]++; _fSum[kind] += paid; _fGame[kind] += GameOf(kind, amount);
                if (gar != null) { _toGuards += purse; _toChest += paid - purse; } else _toTown += paid;
                Log.Info("Wydatki gracza (E8/D2): " + FName[kind] + " - " + NameOf(st) + ": zaplacone " + paid + " (gra chciala " + GameOf(kind, amount) + "), "
                         + (gar != null ? "sakiewka zalogi " + purse + ", kiesa zalogi " + (paid - purse) : "kasa osady (bez zalogi) " + paid) + ".");
            }
            catch (Exception e) { Stumble("Guards(sakiewka)", e); }
        }

        /// <summary>BribeGuardsAction.ApplyInternal(settlement, gold): osada z argumentu metody (poslaniec Diplomacy placi za osade adresata).</summary>
        public static void PayGuardsAt(Hero giver, Hero recipient, int amount, bool dn, Settlement settlement) { Guards(FBribe, settlement, giver, recipient, amount, dn); }

        /// <summary>PrisonBreakCampaignBehavior.OpenPrisonBreakMission: osada, w ktorej stoi gracz.</summary>
        public static void PayGuardsHere(Hero giver, Hero recipient, int amount, bool dn) { Guards(FBreak, SafeHere("PayGuardsHere"), giver, recipient, amount, dn); }

        /// <summary>Postfiks DefaultBribeCalculationModel.GetBribeInternal(settlement): wynik gry = max(kwota - juz zaplacone, 0) w starej monecie.
        /// Kwota sprzed odjecia x 60/5000 (30 dni placy straznika za 5 000), nie mniej niz 6 (3 dni), nigdy wiecej niz gra; od niej to, co juz zaplacone.
        /// Wynik gry 0 (nie trzeba albo zaplacone) zostaje 0.</summary>
        public static void BribePost(Settlement __0, ref int __result)
        {
            try
            {
                if (__result <= 0 || __0 == null || !FeesOn) return;
                int paid = Math.Max(0, __0.BribePaid);
                long old = (long)__result + paid;
                long now = Math.Max((long)DaysMin * GuardDay, (long)Math.Round(old * (double)OldToNew));
                if (now > old) now = old;
                int left = (int)Math.Max(0L, now - paid);
                _lastBribeGame = __result; _lastBribeNow = left;
                __result = left;
            }
            catch (Exception e) { Stumble("BribePost", e); }
        }

        /// <summary>Postfiks DefaultPrisonBreakModel.GetPrisonBreakStartCost(prisoner): straznicy lochu (GetNumberOfGuardsToSpawn - ci, ktorych gracz
        /// spotka) x 30 dni placy straznika x czynnik lotrostwa gry; nie mniej niz jedna taka lapowka, nigdy wiecej niz gra.</summary>
        public static void BreakPost(Hero __0, ref int __result)
        {
            try
            {
                if (__result <= 0 || __0 == null || !FeesOn || Campaign.Current == null) return;
                var st = __0.CurrentSettlement;
                if (st == null || st.Town == null) return;
                int guards = Math.Max(1, Campaign.Current.Models.PrisonBreakModel.GetNumberOfGuardsToSpawn(st));
                int one = DaysWar * GuardDay;
                int now = Math.Max(one, (int)Math.Round(guards * one * SkillFactor()));
                if (now >= __result) return;
                _lastBreakGame = __result; _lastBreakNow = now;
                __result = now;
            }
            catch (Exception e) { Stumble("BreakPost", e); }
        }

        // ------------------------------------------------------------ zaklady (PlayerBetsCounterparty)

        /// <summary>TournamentBehavior.PlaceABet: stawka gracza do kasy miasta (gra: w nicosc).</summary>
        public static void BetStake(Hero giver, Hero recipient, int amount, bool dn)
        {
            bool mine = false, on = false; Settlement st = null;
            try { mine = OutDir(giver, recipient, amount); if (mine) { on = BetsOn; st = Here(); } }
            catch (Exception e) { Stumble("BetStake(stan)", e); mine = false; }
            if (!mine) { GiveGoldAction.ApplyBetweenCharacters(giver, recipient, amount, dn); return; }
            var me = Hero.MainHero;
            int had = me.Gold;
            if (!on || st == null)
            {
                GiveGoldAction.ApplyBetweenCharacters(giver, recipient, amount, dn);   // jak gra
                int lost = Math.Max(0, had - me.Gold);
                _bStakeN++; _bStake += lost; _bStakeVoid += lost;
                return;
            }
            GiveGoldAction.ApplyForCharacterToSettlement(me, st, amount, dn);
            int paid = Math.Max(0, had - me.Gold);
            if (!ReferenceEquals(_stakeTown, st)) { _bStale += _stakeHeld; _stakeTown = st; _stakeHeld = 0; }
            _stakeHeld += paid; _bStakeN++; _bStake += paid;
        }

        /// <summary>TournamentBehavior.OnPlayerEliminated: gra dopisuje kasie miasta przegrana stawke z niczego - stawka juz w kasie lezy, wiec nic nie dopisujemy.</summary>
        public static void BetLost(Hero giver, Settlement settlement, int amount, bool dn)
        {
            int skip = 0;
            try
            {
                if (giver == null && settlement != null && amount > 0)
                {
                    if (BetsOn && ReferenceEquals(_stakeTown, settlement)) skip = (int)Math.Min((long)amount, Math.Max(0L, _stakeHeld));
                    _bLostN++; _bLost += amount; _bLostKept += skip; _bLostNothing += amount - skip;
                    _stakeHeld = 0; _stakeTown = null;
                }
            }
            catch (Exception e) { Stumble("BetLost", e); skip = 0; }
            if (skip == 0) GiveGoldAction.ApplyForCharacterToSettlement(giver, settlement, amount, dn);            // jak gra
            else if (amount - skip > 0) GiveGoldAction.ApplyForCharacterToSettlement(giver, settlement, amount - skip, dn);
        }

        /// <summary>TournamentBehavior.OnPlayerWinTournament: wygrana z kasy miasta - stawka gracza wraca zawsze, zysk z nadwyzki.</summary>
        public static void BetWon(Hero giver, Hero recipient, int amount, bool dn)
        {
            Settlement st = null; long held = 0; bool on = false;
            try { on = BetsOn; st = _stakeTown ?? Here(); held = ReferenceEquals(_stakeTown, st) ? _stakeHeld : 0; }
            catch (Exception e) { Stumble("BetWon(stan)", e); st = null; }
            _stakeHeld = 0; _stakeTown = null;
            TownPays(SBetWon, giver, recipient, amount, dn, on, st, held);
        }

        /// <summary>BoardGameCampaignBehavior.OnPlayerBoardGameOver (tylko gra z gospodarzem - nie-bohaterem): przegrana do kasy osady, wygrana z kasy osady.</summary>
        public static void BoardBet(Hero giver, Hero recipient, int amount, bool dn)
        {
            bool lost = false, on = false; Settlement st = null;
            try { lost = OutDir(giver, recipient, amount); on = BetsOn; st = Here(); }
            catch (Exception e) { Stumble("BoardBet(stan)", e); lost = false; st = null; on = false; }
            if (!lost) { TownPays(SBoardWon, giver, recipient, amount, dn, on, st, 0); return; }
            var me = Hero.MainHero;
            int had = me.Gold;
            if (!on || st == null)
            {
                GiveGoldAction.ApplyBetweenCharacters(giver, recipient, amount, dn);   // jak gra
                int gone = Math.Max(0, had - me.Gold);
                _bBoardLostN++; _bBoardLost += gone; _bBoardVoid += gone;
                return;
            }
            GiveGoldAction.ApplyForCharacterToSettlement(me, st, amount, dn);
            _bBoardLostN++; _bBoardLost += Math.Max(0, had - me.Gold);
        }

        // ------------------------------------------------------------ nagroda turnieju gracza i zloto TXP (PlayerTourneyPrizeFromTown)

        /// <summary>Zloto Tournaments XPanded (TXPManager.EndTournament - nagroda; TXPTournamentMissionController.ApplyMatchAchievements - premie meczu):
        /// z kasy miasta turnieju z nadwyzki.</summary>
        public static void TxpGold(Hero giver, Hero recipient, int amount, bool dn) { TownPays(STxp, giver, recipient, amount, dn, PrizeOn, SafeHere("TxpGold"), 0); }

        /// <summary>RealPayer.TourneyPre (prefiks High na TournamentManager.GivePrizeToWinner - przed RBMTournament): migawka sakw gracza, gdy zwyciezca
        /// jest w jego druzynie. Bez migawki postfiks nic nie robi.</summary>
        internal static void PrizeBefore(TournamentGame tournament, Hero winner)
        {
            _pzSnap = null;
            try
            {
                var mp = MobileParty.MainParty;
                if (tournament == null || winner == null || mp == null || mp.ItemRoster == null || !ReferenceEquals(winner.PartyBelongedTo, mp)) return;
                var r = mp.ItemRoster;
                var snap = new Dictionary<EquipmentElement, int>(r.Count + 4);
                for (int i = 0; i < r.Count; i++)
                {
                    var el = r.GetElementCopyAtIndex(i);
                    if (el.EquipmentElement.Item == null) continue;
                    int n; snap.TryGetValue(el.EquipmentElement, out n);
                    snap[el.EquipmentElement] = n + el.Amount;
                }
                _pzSnap = snap;
            }
            catch (Exception e) { _pzSnap = null; Stumble("PrizeBefore", e); }
        }

        private static int Count(ItemRoster r, EquipmentElement el)
        {
            int i = r.FindIndexOfElement(el);
            return i >= 0 ? r.GetElementNumber(i) : 0;
        }

        /// <summary>Postfiks TournamentManager.GivePrizeToWinner (biegnie takze, gdy metode gry zastapil prefiks RBMTournament): sztuki nagrody, ktore
        /// przybyly w sakwach gracza, rozliczone z polka miasta-gospodarza.</summary>
        public static void PrizeAfter(TournamentGame __0)
        {
            var snap = _pzSnap;
            _pzSnap = null;
            if (snap == null) return;
            try
            {
                var mp = MobileParty.MainParty;
                var town = __0 != null ? __0.Town : null;
                if (mp == null || mp.ItemRoster == null || town == null || town.Settlement == null) return;
                var r = mp.ItemRoster;
                var prize = __0.Prize;
                var gained = new List<KeyValuePair<EquipmentElement, int>>();
                for (int i = 0; i < r.Count; i++)
                {
                    var el = r.GetElementCopyAtIndex(i);
                    if (el.EquipmentElement.Item == null || (prize != null && !ReferenceEquals(el.EquipmentElement.Item, prize))) continue;
                    int before; snap.TryGetValue(el.EquipmentElement, out before);
                    if (el.Amount > before) gained.Add(new KeyValuePair<EquipmentElement, int>(el.EquipmentElement, el.Amount - before));
                }
                if (gained.Count == 0) { _pzNoItem++; return; }
                bool on = PrizeOn;
                foreach (var g in gained)
                    for (int k = 0; k < g.Value; k++) SettlePrize(town, r, g.Key, on);
            }
            catch (Exception e) { Stumble("PrizeAfter", e); }
        }

        private static void SettlePrize(Town town, ItemRoster mine, EquipmentElement el, bool on)
        {
            var st = town.Settlement;
            int worth = Math.Max(0, town.MarketData.GetPrice(el.Item));   // ta sama wycena co nagroda AI (TournamentManager.cs:190)
            _pzN++;
            if (!on) { _pzNothingN++; _pzNothingVal += worth; return; }
            var shelf = st.ItemRoster;
            string item = el.Item.Name != null ? el.Item.Name.ToString() : "the prize";
            if (shelf != null && Count(shelf, el) > 0)
            {
                shelf.AddToCounts(el, -1);
                _pzShelf++;
                Log.Info("Wydatki gracza (E8/D2): nagroda turnieju - " + el.Item.StringId + " zdjeta z polki " + NameOf(st) + " (wartosc " + worth + ").");
                Log.Player("Your prize, " + item + ", came out of the stores of " + NameOf(st) + ".");
                return;
            }
            if (shelf != null)
                for (int i = 0; i < shelf.Count; i++)
                {
                    var se = shelf.GetElementCopyAtIndex(i);
                    if (se.Amount <= 0 || !ReferenceEquals(se.EquipmentElement.Item, el.Item)) continue;
                    var alt = se.EquipmentElement;
                    shelf.AddToCounts(alt, -1);          // sztuka z polki w jej stanie zamiast sztuki z niczego
                    mine.AddToCounts(el, -1);
                    mine.AddToCounts(alt, 1);
                    _pzShelf++; _pzShelfAlt++;
                    Log.Info("Wydatki gracza (E8/D2): nagroda turnieju - " + el.Item.StringId + " zdjeta z polki " + NameOf(st) + " w stanie z polki (wartosc " + worth + ").");
                    Log.Player("Your prize, " + item + ", came out of the stores of " + NameOf(st) + " - as it lay there.");
                    return;
                }
            // Jeff 15.10 ("wygralem turniej i nie dostalem nagrody, dostalem tylko kase, nagroda byl pancerz"): nagrody nie ma na polce - miasto
            // zamawia ja u swoich rzemieslnikow: przedmiot zostaje u gracza, kasa miasta placi jego cene ludowi miasta (robota rzemieslnikow, KCraftPaid).
            // Pieniadz bez zrodla z niczego (kasa -> kiesa ludu); gdy kasy brak - rzemieslnicy dostaja, ile jest, przedmiot i tak (obiecany w szrankach).
            if (Settings.Current != null && Settings.Current.TourneyPrizeCommissioned)
            {
                int paidC = 0;
                try
                {
                    if (worth > 0 && st.IsTown && st.Town != null)
                    {
                        int t0 = st.Town.Gold;
                        TownFolk.EarnFromTown(st, worth, TownFolk.KCraftPaid);
                        paidC = Math.Max(0, t0 - st.Town.Gold);
                    }
                }
                catch (Exception e) { Stumble("PrizeCommission", e); }
                _pzShelf++;
                Log.Info("Wydatki gracza (E8/D2): nagroda turnieju - " + el.Item.StringId + " nie ma na polce " + NameOf(st) + ": zamowiona u rzemieslnikow miasta, przedmiot u gracza; kasa miasta zaplacila ludowi " + paidC + " z ceny " + worth + ".");
                Log.Player(NameOf(st) + " had no " + item + " in its stores - the town had its craftsmen make your prize.");
                return;
            }
            // nie ma jej na polce: zamiast przedmiotu cena z kasy miasta z nadwyzki (jak u AI)
            mine.AddToCounts(el, -1);
            int pay = CapOf(st, worth, 0);
            long before = st.SettlementComponent.Gold;
            _sN[SPrize]++; _sDue[SPrize] += worth;
            if (pay > 0) GiveGoldAction.ApplyForSettlementToCharacter(st, Hero.MainHero, pay, true);
            _sPaid[SPrize] += pay;
            if (pay > 0 && st.Town != null) { long rsv = Seal31.TownReserve(st.Town); if (before >= rsv && before - pay < rsv) _below++; }
            if (pay < worth) _sShortN[SPrize]++;
            Log.Info("Wydatki gracza (E8/D2): nagroda turnieju - " + el.Item.StringId + " nie ma na polce " + NameOf(st) + ": cena " + worth + ", zaplacone z kasy " + pay + ".");
            Log.Player(NameOf(st) + " had no " + item + " in its stores - the town paid you " + pay + " denars for the prize"
                       + (pay < worth ? " (of " + worth + " - its treasury could spare no more)." : "."), pay < worth);
        }

        // ------------------------------------------------------------ drobne zrodla gracza (PlayerSmallSourcesClosed)

        /// <summary>ArenaMasterCampaignBehavior.conversation_arena_master_post_fight_on_condition: nagroda areny treningowej (5 - 250) z kasy miasta z nadwyzki.</summary>
        public static void ArenaPrize(Hero giver, Hero recipient, int amount, bool dn) { TownPays(SArena, giver, recipient, amount, dn, SmallOn, SafeHere("ArenaPrize"), 0); }

        /// <summary>VillageHostileActionCampaignBehavior.village_force_supplies_ended_successfully_on_consequence: hearth x 4 z kiesy wsi do jej wysokosci
        /// (ta sama regula co rabunek wsi - SmallGold RaidLootFromVillage).</summary>
        public static void VillagePays(Hero giver, Hero recipient, int amount, bool dn) { TownPays(SForce, giver, recipient, amount, dn, SmallOn, SafeHere("VillagePays"), 0); }

        /// <summary>Prefiks NavalDLCClanFinanceModel.AddPartyExpense(party, clan, goldChange, applyWithdrawals) - wolana TYLKO z galezi "Coastal Guard Edict"
        /// (zaloga portowa rodu krolestwa z ta polityka), PO rozliczeniu zalogi przez model bazowy: drugi raz zdejmuje zold z kiesy zalogi (w nicosc),
        /// dopelnia kiese do progu z niczego (rod za dopelnienie nie placi - wynik nie trafia do salda) i dopisuje rodowi +15% dopelnienia. Pomijamy.</summary>
        public static bool EdictPre(bool __3, ref int __result)
        {
            try
            {
                if (!SmallOn) return true;
                _edN++; if (__3) _edPayN++;
                __result = 0;
                return false;
            }
            catch (Exception e) { Stumble("EdictPre", e); return true; }
        }

        // ------------------------------------------------------------ transpiler

        private static bool LdcI4(CodeInstruction ci, out int v)
        {
            v = 0;
            if (ci.opcode == OpCodes.Ldc_I4 && ci.operand is int) { v = (int)ci.operand; return true; }
            if (ci.opcode == OpCodes.Ldc_I4_S && ci.operand != null) { v = Convert.ToInt32(ci.operand, Inv); return true; }
            return false;
        }

        public static IEnumerable<CodeInstruction> Il(IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            var list = new List<CodeInstruction>(instructions);
            try
            {
                Spec sp;
                if (original == null || !_specs.TryGetValue(original.MethodHandle.Value, out sp)) return list;
                int calls = 0, consts = 0;
                for (int i = 0; i < list.Count; i++)
                {
                    var ci = list[i];
                    int v;
                    if (sp.ConstTo != null && LdcI4(ci, out v) && v == sp.Const) { ci.opcode = OpCodes.Call; ci.operand = sp.ConstTo; consts++; continue; }
                    if (ci.opcode != OpCodes.Call && ci.opcode != OpCodes.Callvirt) continue;
                    var mi = ci.operand as MethodInfo;
                    if (mi == null) continue;
                    if (sp.From != null && mi == sp.From)
                    {
                        if (sp.Arg0)
                        {
                            var ld = new CodeInstruction(OpCodes.Ldarg_0);
                            if (ci.labels.Count > 0) { ld.labels.AddRange(ci.labels); ci.labels.Clear(); }
                            list.Insert(i, ld); i++;
                        }
                        ci.opcode = OpCodes.Call; ci.operand = sp.To; calls++;
                    }
                    else if (sp.From2 != null && mi == sp.From2) { ci.opcode = OpCodes.Call; ci.operand = sp.To2; calls++; }
                }
                sp.Calls = calls; sp.Consts = consts;
            }
            catch (Exception e) { Stumble("Il", e); }
            return list;
        }

        // ------------------------------------------------------------ wpiecie (raz na proces, w kampanii)

        private static MethodInfo W(string name) { return AccessTools.Method(typeof(PlayerFees8), name); }

        private static Spec Add(string label, string typeName, string method, bool optional = false)
        {
            var sp = new Spec { Label = label, TypeName = typeName, Method = method, Optional = optional };
            _all.Add(sp);
            return sp;
        }

        private static Spec Call(string label, string typeName, string method, MethodInfo from, string to, int want, bool optional = false)
        {
            var sp = Add(label, typeName, method, optional);
            sp.From = from; sp.To = W(to); sp.WantCalls = want;
            return sp;
        }

        private static Spec Num(Spec sp, int value, string to, int want) { sp.Const = value; sp.ConstTo = W(to); sp.WantConsts = want; return sp; }

        /// <summary>Metoda gry po nazwie; przy kilku przeciazeniach (SB TavernEmployeesCampaignBehavior.FindCompanionWithType ma dwa - drugie tylko wola
        /// pierwsze) ta, ktorej IL wola podmieniana metode.</summary>
        private static MethodBase FindMethod(Type t, Spec sp)
        {
            var c = new List<MethodInfo>();
            foreach (var m in AccessTools.GetDeclaredMethods(t)) if (m.Name == sp.Method) c.Add(m);
            if (c.Count == 1) return c[0];
            if (c.Count == 0) return AccessTools.Method(t, sp.Method);
            foreach (var m in c)
            {
                try
                {
                    foreach (var kv in PatchProcessor.ReadMethodBody(m))
                    {
                        var mi = kv.Value as MethodInfo;
                        if (mi != null && ((sp.From != null && mi == sp.From) || (sp.From2 != null && mi == sp.From2))) return m;
                    }
                }
                catch (Exception e) { Stumble("FindMethod(" + sp.Method + ")", e); }
            }
            return null;
        }

        internal static void SessionStart() { EnsureHooks(); }

        internal static void EnsureHooks()
        {
            if (_tried) { Log.Info(StatusLine("przy starcie kampanii")); return; }
            _tried = true;
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                if (_h == null) _h = new Harmony("com.jeff.armoury.playerfees8");
                var abc = AccessTools.Method(typeof(GiveGoldAction), "ApplyBetweenCharacters", new[] { typeof(Hero), typeof(Hero), typeof(int), typeof(bool) });
                var afcs = AccessTools.Method(typeof(GiveGoldAction), "ApplyForCharacterToSettlement", new[] { typeof(Hero), typeof(Settlement), typeof(int), typeof(bool) });
                if (abc == null || afcs == null)
                {
                    _missText = "metody gry GiveGoldAction - nie znaleziono, nic nie wpiete";
                    Log.Info(StatusLine("przy starcie pierwszej kampanii"));
                    return;
                }
                const string SB = "SandBox.CampaignBehaviors.", TB = "SandBox.Tournaments.MissionLogics.TournamentBehavior", TAV = SB + "TavernEmployeesCampaignBehavior";
                const string BAR = SB + "BarberCampaignBehavior";
                // nagroda turnieju gracza i zloto Tournaments XPanded
                Add("nagroda turnieju gracza (TournamentManager.GivePrizeToWinner)", typeof(TournamentManager).FullName, "GivePrizeToWinner").Post = nameof(PrizeAfter);
                Call("zloto TXP - nagroda (TXPManager.EndTournament)", "TournamentsXPanded.TXPManager", "EndTournament", abc, nameof(TxpGold), 1, true);
                Call("zloto TXP - premie meczu (ApplyMatchAchievements)", "TournamentsXPanded.Behaviors.TXPTournamentMissionController", "ApplyMatchAchievements", abc, nameof(TxpGold), 1, true);
                // zaklady
                Call("zaklad - postawienie (PlaceABet)", TB, "PlaceABet", abc, nameof(BetStake), 1);
                Call("zaklad - przegrana (OnPlayerEliminated)", TB, "OnPlayerEliminated", afcs, nameof(BetLost), 1);
                Call("zaklad - wygrana (OnPlayerWinTournament)", TB, "OnPlayerWinTournament", abc, nameof(BetWon), 1);
                Call("gra planszowa (OnPlayerBoardGameOver)", SB + "BoardGameCampaignBehavior", "OnPlayerBoardGameOver", abc, nameof(BoardBet), 2);
                // oplaty
                Call("lapowka strazy - odbiorca (BribeGuardsAction.ApplyInternal)", typeof(BribeGuardsAction).FullName, "ApplyInternal", abc, nameof(PayGuardsAt), 1).Arg0 = true;
                Add("lapowka strazy - kwota (DefaultBribeCalculationModel.GetBribeInternal)", typeof(DefaultBribeCalculationModel).FullName, "GetBribeInternal").Post = nameof(BribePost);
                Call("odbicie wieznia - odbiorca (OpenPrisonBreakMission)", SB + "PrisonBreakCampaignBehavior", "OpenPrisonBreakMission", abc, nameof(PayGuardsHere), 1);
                Add("odbicie wieznia - kwota (DefaultPrisonBreakModel.GetPrisonBreakStartCost)", typeof(DefaultPrisonBreakModel).FullName, "GetPrisonBreakStartCost").Post = nameof(BreakPost);
                Num(Call("golibroda - zaplata (ChargeThePlayer)", BAR, "ChargeThePlayer", abc, nameof(FolkBarber), 1), BarberOld, nameof(BarberFee), 1);
                Num(Add("golibroda - warunek (DoesPlayerHaveEnoughGold)", BAR, "DoesPlayerHaveEnoughGold"), BarberOld, nameof(BarberFee), 1);
                Num(Add("golibroda - tekst (GivePlayerAHaircutCondition)", BAR, "GivePlayerAHaircutCondition"), BarberOld, nameof(BarberFee), 1);
                Num(Call("wiesci karczmarza - zaplata (player_accepts_clan_info_offer_on_consequence)", TAV, "player_accepts_clan_info_offer_on_consequence", abc, nameof(FolkClanInfo), 1), ClanInfoOld, nameof(ClanInfoFee), 1);
                Num(Add("wiesci karczmarza - warunek (player_accepts_clan_info_offer_clickable_condition)", TAV, "player_accepts_clan_info_offer_clickable_condition"), ClanInfoOld, nameof(ClanInfoFee), 2);
                Num(Add("wiesci karczmarza - tekst (tavernkeeper_offer_clan_info_on_condition)", TAV, "tavernkeeper_offer_clan_info_on_condition"), ClanInfoOld, nameof(ClanInfoFee), 1);
                MethodInfo tun = null;
                try
                {
                    var tt = AccessTools.TypeByName(TAV);
                    tun = tt != null ? AccessTools.Method(tt, "get_tun_price") : null;
                    if (tun != null && tun.IsStatic && tun.ReturnType == typeof(int) && tun.GetParameters().Length == 0) _origTun = (Func<int>)Delegate.CreateDelegate(typeof(Func<int>), tun);
                    else tun = null;
                }
                catch (Exception e) { Stumble("EnsureHooks(get_tun_price)", e); tun = null; _origTun = null; }
                var s1 = Call("kolejka - zaplata (can_buy_tun_on_consequence)", TAV, "can_buy_tun_on_consequence", abc, nameof(FolkTun), tun != null ? 2 : 1);
                s1.From2 = tun; s1.To2 = W(nameof(TunPrice));
                var s2 = Add("kolejka - warunek (can_buy_tun_on_clickable_condition)", TAV, "can_buy_tun_on_clickable_condition");
                s2.From2 = tun; s2.To2 = W(nameof(TunPrice)); s2.WantCalls = 1;
                var s3 = Add("kolejka - tekst (calculate_tun_cost_on_condition)", TAV, "calculate_tun_cost_on_condition");
                s3.From2 = tun; s3.To2 = W(nameof(TunPrice)); s3.WantCalls = 1;
                Call("napitek (FindCompanionWithType)", TAV, "FindCompanionWithType", abc, nameof(FolkDrink), 1);
                // drobne zrodla
                Call("arena treningowa (conversation_arena_master_post_fight_on_condition)", SB + "ArenaMasterCampaignBehavior", "conversation_arena_master_post_fight_on_condition", abc, nameof(ArenaPrize), 1);
                Call("wymuszone zapasy wsi (village_force_supplies_ended_successfully_on_consequence)", "TaleWorlds.CampaignSystem.CampaignBehaviors.VillageHostileActionCampaignBehavior",
                     "village_force_supplies_ended_successfully_on_consequence", abc, nameof(VillagePays), 1);
                Add("edykt strazy wybrzeza (NavalDLCClanFinanceModel.AddPartyExpense)", "NavalDLC.GameComponents.NavalDLCClanFinanceModel", "AddPartyExpense", true).Pre = nameof(EdictPre);

                var il = new HarmonyMethod(typeof(PlayerFees8), nameof(Il));
                foreach (var sp in _all)
                {
                    try
                    {
                        var t = AccessTools.TypeByName(sp.TypeName);
                        if (t == null) { sp.State = (byte)(sp.Optional ? 4 : 2); continue; }
                        sp.M = FindMethod(t, sp);
                        if (sp.M == null) { sp.State = 2; continue; }
                        if (sp.Pre != null || sp.Post != null)
                        {
                            _h.Patch(sp.M, prefix: sp.Pre != null ? new HarmonyMethod(typeof(PlayerFees8), sp.Pre) : null,
                                           postfix: sp.Post != null ? new HarmonyMethod(typeof(PlayerFees8), sp.Post) : null);
                            sp.State = 1;
                            continue;
                        }
                        if ((sp.From != null && sp.To == null) || (sp.From2 != null && sp.To2 == null) || (sp.WantConsts > 0 && sp.ConstTo == null)) { sp.State = 3; sp.Err = "brak wrappera"; continue; }
                        _specs[sp.M.MethodHandle.Value] = sp;
                        _h.Patch(sp.M, transpiler: il);
                        sp.State = (byte)(sp.Calls == sp.WantCalls && sp.Consts == sp.WantConsts ? 1 : 5);
                    }
                    catch (Exception e) { sp.State = 3; sp.Err = e.GetType().Name + ": " + e.Message; Stumble("EnsureHooks(" + sp.Method + ")", e); }
                }
                var miss = new List<string>(); var absent = new List<string>();
                _wired = _wanted = _calls = _callsWant = _consts = _constsWant = 0;
                foreach (var sp in _all)
                {
                    if (sp.State == 4) { absent.Add(sp.Label); continue; }
                    _wanted++; _callsWant += sp.WantCalls; _constsWant += sp.WantConsts; _calls += sp.Calls; _consts += sp.Consts;
                    if (sp.State == 1) _wired++;
                    else miss.Add(sp.Label + (sp.State == 2 ? " (nie znaleziono)" : sp.State == 5 ? " (podmian " + sp.Calls + "/" + sp.WantCalls + ", stalych " + sp.Consts + "/" + sp.WantConsts + ")" : " (blad: " + sp.Err + ")"));
                }
                _missText = miss.Count > 0 ? string.Join(", ", miss.ToArray()) : "-";
                _absentText = absent.Count > 0 ? string.Join(", ", absent.ToArray()) : "-";
            }
            catch (Exception e) { Stumble("EnsureHooks", e); _missText = "wyjatek przy wpinaniu (" + e.Message + ")"; }
            Log.Info(StatusLine("przy starcie pierwszej kampanii, " + ((Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency).ToString("0.0", Inv) + " ms"));
        }

        private static string KeysText()
        {
            return "klucze: Player Tourney Prize From Town " + (PrizeOn ? "tak" : "WYLACZONE") + ", Player Bets Counterparty " + (BetsOn ? "tak" : "WYLACZONE")
                   + ", Player Fees Historical " + (FeesOn ? "tak" : "WYLACZONE") + ", Player Small Sources Closed " + (SmallOn ? "tak" : "WYLACZONE");
        }

        private static string StatusLine(string when)
        {
            string models = "?";
            try
            {
                var m = Campaign.Current != null ? Campaign.Current.Models : null;
                if (m != null)
                {
                    var b = m.BribeCalculationModel; var p = m.PrisonBreakModel;
                    models = "lapowka " + (b != null ? b.GetType().Name : "?") + (b is DefaultBribeCalculationModel ? "" : " (INNY MODEL - latka kwoty moze nie dzialac)")
                             + ", odbicie wieznia " + (p != null ? p.GetType().Name : "?") + (p is DefaultPrisonBreakModel ? "" : " (INNY MODEL - latka kwoty moze nie dzialac)");
                }
            }
            catch (Exception e) { Stumble("StatusLine", e); }
            return "Wydatki gracza (E8/D2): wpiete " + _wired + "/" + _wanted + " latek (" + when + "), podmian wywolan " + _calls + "/" + _callsWant + ", stalych " + _consts + "/" + _constsWant
                   + "; BRAK: " + _missText + "; mody nieobecne: " + _absentText + "; modele: " + models
                   + "; zwrot ceny warsztatu gracza - kupiec z wlasnej kiesy (WorkshopTrade) " + (WorkshopTrade.On ? "tak" : "NIE")
                   + "; sakiewki zalog (MenPurse) " + (MenPurse.On ? "tak" : "nie - lapowki zostaja w kiesie zalogi") + "; kiesa ludu (TownFolk) " + (TownFolk.Live ? "tak" : "nie - oplaty zostaja w kasie miasta")
                   + "; " + KeysText() + "; potkniecia " + _stumbles + ".";
        }

        // ------------------------------------------------------------ takt doby

        /// <summary>Raz na dobe (ArmouryBehavior.OnDailyTick, po QuestPay8.Daily): linia "Wydatki gracza (E8/D2): dzien N". Czas kampanii w misji stoi,
        /// wiec stawka zakladu widziana tutaj to stawka z misji, ktora nie skonczyla sie wygrana ani przegrana - zostala w kasie miasta (licznik).</summary>
        internal static void Daily()
        {
            var s = Settings.Current;
            if (s == null || Campaign.Current == null) return;
            long t0 = Stopwatch.GetTimestamp();
            int day = (int)CampaignTime.Now.ToDays - 1;
            if (_stakeHeld != 0) { _bStale += _stakeHeld; _stakeHeld = 0; }
            _stakeTown = null;
            try { Log.Info(DayLine(day, (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency)); }
            catch (Exception e) { Stumble("Daily", e); }
            finally { ClearDay(); }
        }

        private static void Src(StringBuilder sb, int k)
        {
            sb.Append(_sN[k]).Append(", nalezne ").Append(_sDue[k]).Append(", zaplacone ").Append(_sPaid[k]).Append(", niedoplata ")
              .Append(Math.Max(0, _sDue[k] - _sPaid[k] - _sNothing[k])).Append(" w ").Append(_sShortN[k]).Append(", z niczego ").Append(_sNothing[k]);
        }

        /// <summary>Linia doby z licznikow (osobno - probe poza gra wola ja wprost, zeby narzedzie sprawdzalo prawdziwy format).</summary>
        private static string DayLine(int day, double costMs)
        {
            var sb = new StringBuilder(1600);
            int fn = 0; long fsum = 0, fgame = 0;
            for (int k = 0; k < Fees; k++) { fn += _fN[k]; fsum += _fSum[k]; fgame += _fGame[k]; }
            sb.Append("Wydatki gracza (E8/D2): dzien ").Append(day).Append(" | ").Append(KeysText())
              .Append(" | oplaty (gracz -> odbiorca): ").Append(fn).Append(", kwota ").Append(fsum).Append(" (gra chciala ").Append(fgame).Append("):");
            for (int k = 0; k < Fees; k++) sb.Append(k > 0 ? "; " : " ").Append(FName[k]).Append(' ').Append(_fN[k]).Append(": ").Append(_fSum[k]).Append(" / ").Append(_fGame[k]);
            sb.Append(" - do sakiewek zalog ").Append(_toGuards).Append(", do kies zalog ").Append(_toChest).Append(", do kas osad ").Append(_toTown)
              .Append(", do kiesy ludu ").Append(_toFolk).Append(", oplaty w nicosc ").Append(_feeVoid)
              .Append(" | zaklady: turniej - postawione ").Append(_bStakeN).Append(": ").Append(_bStake).Append(" (do kasy miasta ").Append(_bStake - _bStakeVoid)
              .Append(", stawki w nicosc ").Append(_bStakeVoid).Append("), przegrane ").Append(_bLostN).Append(": ").Append(_bLost).Append(" (zostalo w kasie ").Append(_bLostKept)
              .Append(", kasie z niczego ").Append(_bLostNothing).Append("), stawki z przerwanych turniejow w kasie ").Append(_bStale).Append(", wygrane turnieju ");
            Src(sb, SBetWon);
            sb.Append("; plansza - przegrane ").Append(_bBoardLostN).Append(": ").Append(_bBoardLost).Append(" (do kas osad ").Append(_bBoardLost - _bBoardVoid)
              .Append(", plansza w nicosc ").Append(_bBoardVoid).Append("), wygrane planszy ");
            Src(sb, SBoardWon);
            sb.Append(" | nagroda turnieju gracza: ").Append(_pzN).Append(" szt., z polki miasta ").Append(_pzShelf).Append(" (w tym w stanie z polki ").Append(_pzShelfAlt)
              .Append("), przedmiot z niczego ").Append(_pzNothingN).Append(" szt. o wartosci ").Append(_pzNothingVal).Append(", wygrane bez sztuki w sakwach ").Append(_pzNoItem)
              .Append(", cena zamiast przedmiotu ");
            Src(sb, SPrize);
            sb.Append(" | zloto TXP ");
            Src(sb, STxp);
            sb.Append(" | drobne zrodla: arena ");
            Src(sb, SArena);
            sb.Append("; zapasy wsi ");
            Src(sb, SForce);
            sb.Append("; edykt strazy wybrzeza - pominiete rozliczenia ").Append(_edN).Append(" (z wyplata ").Append(_edPayN).Append(')');
            long nothing = _bLostNothing, voidAll = _feeVoid + _bStakeVoid + _bBoardVoid;
            for (int k = 0; k < Sources; k++) nothing += _sNothing[k];
            sb.Append(" | kontrola (prog 0): zloto z niczego razem ").Append(nothing).Append(", zloto w nicosc razem ").Append(voidAll)
              .Append(", kasa miasta ponizej zapasu kupcow przez nasza wyplate ").Append(_below)
              .Append(" | latki wpiete ").Append(_wired).Append('/').Append(_wanted).Append(_missText != "-" ? " (BRAK: " + _missText + ")" : "")
              .Append(" | koszt ").Append(costMs.ToString("0.00", Inv)).Append(" ms/dobe")
              .Append(" | potkniecia dzis ").Append(_stumblesDay).Append(" (od wczytania ").Append(_stumbles).Append(").");
            return sb.ToString();
        }
    }
}
