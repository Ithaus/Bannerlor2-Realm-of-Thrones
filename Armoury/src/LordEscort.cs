using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// 223 ESKORTA LORDOW (Jeff 26.10 ok. 22:15: "jakim sposobem Tywin szedl z garstka ludzi i biedota w armii, kurwa znani lordowie a juz glowy rodu nie moga kurwa
    /// jezdzic i byc doslownie i czekac az ktos ich schwyta"). Klucze LordsAvoidDanger, LordEscortFromGarrison, LordsJoinNearArmy (MCM "Lords Avoid Danger" i dalej).
    ///
    /// Kogo: partie lordow AI rodow AI w krolestwie w wojnie (nie gracz i jego rod, nie Straz, nie umarli, nie partie prologu Prologue.Scripted, nie najemnicy),
    /// poza armia, bitwa, oblezeniem i morzem. Partie niegotowe p9 (ArmsFirst.Unready) i w wyjezdzie po pierwsza bron 159c (ArmsFirst.CapTripActive) prowadzi p9.
    /// Rangi: R1 wladca albo nastepca (Ransom178 - ta sama regula co wielcy jency), R2 glowa rodu z lennem, R3 reszta.
    /// Miary (z istniejacych modeli, bez nowych stalych):
    ///  - wypelnienie = zdrowi / normalny rozmiar partii (PartyBaseHelper.FindPartySizeNormalLimit x PartySizeLimit - z limitem zoldu rodu 166);
    ///  - prog = ArmyManagementCalculationModel.AIMobilePartySizeRatioToCallToArmy (gra 0.6 - ponizej gra i BK nie wolaja partii do armii);
    ///  - sila S = Party.EstimatedStrength (z mnoznikiem uzbrojenia p9), zagrozenie T = najsilniejsza wroga grupa (partia lorda, cala armia od wodza, banda, gracz)
    ///    w zasiegu CivilWorksFrontRange (60, "doba drogi"); zalogi, milicja, wiesniacy, karawany, partie w bitwie i w osadach pominiete.
    /// NARAZONA: R1/R2 z wypelnieniem ponizej progu albo dowolna ranga z S &lt; T. Co robi (mysl AI co 6 h, postfiks Priority.Last po p9, OB5, WatchLaw, CrossingLaw):
    ///  1. armia wlasnego krolestwa w zasiegu (wodz poza bitwa, nie armia gracza, jedzenie >= MinimumNeededFoodInDaysToCallToArmy, wplyw wodza na koszt wezwania; wladca
    ///     nie idzie do cudzej armii - regula gry) - dolaczenie w nastepnej godzinie (mp.Army = armia, jak gra przy wezwaniu; warunki od nowa);
    ///  2. przy zagrozeniu (T > 0): silniejsza przyjazna partia lorda (S >= T, sama nie narazona) w zasiegu - EscortParty gry;
    ///  3. bezpieczna twierdza: najpierw miasto/zamek wlasnego rodu, potem najblizsza przyjazna - bez oblezenia, bez wrogiego lorda w zasiegu, po tej samej stronie Muru,
    ///     droga ladowa najwyzej AiArmsTripMaxDays dob - GoToSettlement z ocena p9 (max(0.5, 1.2 x najlepsza inna) + 0.01; wyniku &lt;= -1 nie podnosimy);
    ///  4. w twierdzy wlasnego rodu: eskorta z zalogi - prawdziwi ludzie przez prywatna metode gry GarrisonTroopsCampaignBehavior.TakeTroopsFromGarrison (171 sprzet z
    ///     ludzmi, ksiega ludzi, 169c), do progu wypelnienia, nie ponad PartySizeLimit, zaloga najmniej 75 (zamek) / 125 (miasto) - te same liczby co gra; bez eskorty
    ///     przy oblezeniu i wrogim lordzie w zasiegu twierdzy; nie przy przekroczonym limicie zoldu partii (jak gra);
    ///  5. postoj do odbudowy (wypelnienie >= progu i S >= T), najwyzej AiArmsWaitMaxDays dob - potem zwolniony na AiArmsTripCooldownDays dob (regula Jeffa 12.10:
    ///     nie bezczynnosc);
    ///  6. w stanie narazenia opcje bitwy (oblezenie, szturm, rabunek, obrona cudzej osady, poscig za partia nie-banda) -1; obrona osady wlasnego rodu zostaje; obrona
    ///     osady krolestwa zostaje, gdy jest najlepsza (OB5); R1/R2 nie zaczepia partii poza bandami (postfiks ShouldConsiderAttacking, jak p9), poza obrona wlasnej osady.
    /// Stan (od kiedy narazona, zwolniona do) w zapisie (arm_esc223). Linie "Eskorta lordow (223): ...".
    /// </summary>
    internal static partial class LordEscort
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const int FloorTown = 125, FloorCastle = 75;   // GarrisonTroopsCampaignBehavior.MinGarrisonNumberForTown / ForCastle (gra 1.4.8)
        private const int MaxExamples = 10;
        private const int MaxFortChecks = 4;                    // najwyzej tyle skanow bezpieczenstwa twierdz na mysl (najblizsze najpierw)

        private sealed class St
        {
            internal MobileParty P; internal int Rank; internal double SinceH = -1, FreeUntilH = -1, ArriveH = -1; internal Settlement Fort; internal string Act = "";
        }
        private static readonly Dictionary<MBGUID, St> _st = new Dictionary<MBGUID, St>();
        private struct Saved { internal double Since, Free; }
        private static readonly Dictionary<string, Saved> _saved = new Dictionary<string, Saved>(StringComparer.Ordinal);
        private static int _savedLoaded;

        private sealed class Thr { internal double H; internal float T; internal MobileParty Who; }
        private static readonly Dictionary<MBGUID, Thr> _thr = new Dictionary<MBGUID, Thr>();
        private static readonly Dictionary<Settlement, Thr> _fortThr = new Dictionary<Settlement, Thr>();

        // R1/R2 narazone - zakaz zaczepiania (kopia przy zmianie; watki AI tylko czytaja opublikowany zbior)
        private static volatile HashSet<MBGUID> _noAtk = new HashSet<MBGUID>();
        private static readonly Dictionary<MobileParty, Army> _joinQ = new Dictionary<MobileParty, Army>();
        private static readonly Dictionary<MobileParty, Settlement> _escQ = new Dictionary<MobileParty, Settlement>();

        // latki
        private static Harmony _h;
        private static readonly HashSet<MethodBase> _patched = new HashSet<MethodBase>();
        private static string _hThink = "nie wpieta", _hAttack = "nie wpieta", _hTake = "nie wpieta";
        private static MethodInfo _take;
        private static bool _events;

        // liczniki doby
        private static readonly object Gate = new object();
        private static int _stumbles, _vAtkCalls;
        private static readonly HashSet<MBGUID> _vAtk = new HashSet<MBGUID>();
        private static readonly HashSet<MBGUID> _dLords = new HashSet<MBGUID>(), _dWeak = new HashSet<MBGUID>(), _dGreat = new HashSet<MBGUID>(), _dLow = new HashSet<MBGUID>(),
                                                _dWeaker = new HashSet<MBGUID>(), _dToFort = new HashSet<MBGUID>(), _dStay = new HashSet<MBGUID>(), _dFollow = new HashSet<MBGUID>(),
                                                _dNoFort = new HashSet<MBGUID>(), _dOwnDef = new HashSet<MBGUID>(), _dKin = new HashSet<MBGUID>();
        private static readonly HashSet<Settlement> _dEscForts = new HashSet<Settlement>();
        private static int _dArrived, _dJoined, _dJoinDrop, _dEsc, _dEscMen, _dEscBelow, _dEscHeld, _dEscNoRoom, _dRebuilt, _dFreed, _dStarve, _dExamples;

        private static void Stumble(string where, Exception e)
        {
            int n = Interlocked.Increment(ref _stumbles);
            try { if (n <= 5 || n % 100 == 0) Log.Error("LordEscort." + where + " (potkniecie " + n + ")", e); } catch { }
        }

        internal static bool On { get { var s = Settings.Current; return s != null && s.LordsAvoidDanger; } }

        internal static void Reset()
        {
            _st.Clear(); _saved.Clear(); _savedLoaded = 0; _thr.Clear(); _fortThr.Clear(); _noAtk = new HashSet<MBGUID>(); _joinQ.Clear(); _escQ.Clear(); _seen.Clear();
            ClearDay(); _stumbles = 0;
        }

        private static void ClearDay()
        {
            lock (Gate) { _vAtkCalls = 0; _vAtk.Clear(); }
            _dLords.Clear(); _dWeak.Clear(); _dGreat.Clear(); _dLow.Clear(); _dWeaker.Clear(); _dToFort.Clear(); _dStay.Clear(); _dFollow.Clear(); _dNoFort.Clear(); _dOwnDef.Clear(); _dKin.Clear();
            _dEscForts.Clear();
            _dArrived = _dJoined = _dJoinDrop = _dEsc = _dEscMen = _dEscBelow = _dEscHeld = _dEscNoRoom = _dRebuilt = _dFreed = _dStarve = _dExamples = 0;
            ClearDayProbe();
        }

        // ------------------------------------------------------------ miary
        private static float Range { get { var s = Settings.Current; return s != null ? MBMath.ClampFloat(s.CivilWorksFrontRange, 10f, 300f) : 60f; } }

        /// <summary>Prog wypelnienia z modelu armii gry (AIMobilePartySizeRatioToCallToArmy, gra 0.6; BK IsAvailableForArmies - ta sama liczba).</summary>
        internal static float Ratio()
        {
            try
            {
                var m = Campaign.Current != null && Campaign.Current.Models != null ? Campaign.Current.Models.ArmyManagementCalculationModel : null;
                return m != null ? MBMath.ClampFloat(m.AIMobilePartySizeRatioToCallToArmy, 0.1f, 1f) : 0.6f;
            }
            catch { return 0.6f; }
        }

        /// <summary>Normalny rozmiar partii: FindPartySizeNormalLimit (limit zoldu partii, 166) x PartySizeLimit; najmniej 1.</summary>
        private static float Normal(MobileParty mp)
        {
            int lim = mp.Party != null ? mp.Party.PartySizeLimit : 0;
            if (lim <= 0) return 1f;
            return Math.Max(1f, PartyBaseHelper.FindPartySizeNormalLimit(mp) * lim);
        }

        private static float Fill(MobileParty mp) { return mp.MemberRoster != null ? mp.MemberRoster.TotalHealthyCount / Normal(mp) : 0f; }

        private static float Str(MobileParty p)
        {
            try
            {
                if (p.Army != null && p.Army.LeaderParty == p) return p.Army.EstimatedStrength;
                return p.Party != null ? p.Party.EstimatedStrength : 0f;
            }
            catch { return 0f; }
        }

        /// <summary>Ranga: 1 wladca albo nastepca (Ransom178), 2 glowa rodu z lennem, 3 reszta.</summary>
        private static int RankOf(Hero h, Clan clan)
        {
            if (h == null) return 3;
            if (Ransom178.IsRulerOrHeir(h)) return 1;
            if (clan != null && clan.Leader == h && clan.Fiefs != null && clan.Fiefs.Count > 0) return 2;
            return 3;
        }

        private static string RankName(int r) { return r == 1 ? "wladca/nastepca" : (r == 2 ? "glowa rodu" : "lord"); }

        private static bool AtWar(Kingdom k)
        {
            foreach (var f in k.FactionsAtWarWith) if (f != null && !f.IsBanditFaction && !f.IsEliminated) return true;
            return false;
        }

        /// <summary>Partia w regule (bez stanu chwili: armia, bitwa, oblezenie - sprawdza mysl).</summary>
        private static bool Covered(MobileParty mp)
        {
            if (mp == null || !mp.IsActive || !mp.IsLordParty || mp.IsMainParty || mp.IsDisbanding) return false;
            var h = mp.LeaderHero;
            if (h == null || !h.IsAlive) return false;
            var clan = mp.ActualClan;
            if (clan == null || clan == Clan.PlayerClan || clan.IsUnderMercenaryService || clan.IsBanditFaction) return false;
            var k = mp.MapFaction as Kingdom;
            if (k == null || k.IsEliminated || !AtWar(k)) return false;
            if (CrownGifts.IsWatchParty(mp) || Undead.Party(mp) || Prologue.Scripted(mp)) return false;
            return true;
        }

        private static bool Hostile(IFaction f, MobileParty t)
        {
            if (t.IsBandit) return true;
            var ft = t.MapFaction;
            return f != null && ft != null && ft != f && ft.IsAtWarWith(f);
        }

        /// <summary>Najsilniejsza wroga grupa w zasiegu punktu: partia lorda (armia liczona raz, od wodza), banda (gdy lordsOnly false), gracz. Watek glowny.</summary>
        private static float ThreatAt(Vec2 pos, IFaction f, bool atSea, MobileParty self, bool lordsOnly, out MobileParty who)
        {
            who = null;
            float best = 0f, r = Range;
            HashSet<Army> armies = null;
            var data = MobileParty.StartFindingLocatablesAroundPosition(pos, r);
            for (var t = MobileParty.FindNextLocatable(ref data); t != null; t = MobileParty.FindNextLocatable(ref data))
            {
                try
                {
                    if (t == self || !t.IsActive || t.IsCurrentlyAtSea != atSea) continue;
                    if (t.CurrentSettlement != null || t.MapEvent != null) continue;
                    if (t.IsGarrison || t.IsMilitia || t.IsVillager || t.IsCaravan) continue;
                    if (!(t.IsLordParty || t.IsMainParty || (!lordsOnly && t.IsBandit))) continue;
                    if (!Hostile(f, t)) continue;
                    if (pos.Distance(t.GetPosition2D) > r) continue;
                    float st;
                    var a = t.Army;
                    if (a != null)
                    {
                        if (armies == null) armies = new HashSet<Army>();
                        if (!armies.Add(a)) continue;
                        st = a.EstimatedStrength;
                        if (a.LeaderParty != null) { if (st > best) { best = st; who = a.LeaderParty; } continue; }
                    }
                    else st = t.Party != null ? t.Party.EstimatedStrength : 0f;
                    if (st > best) { best = st; who = t; }
                }
                catch (Exception e) { Stumble("ThreatAt", e); }
            }
            return best;
        }

        /// <summary>Zagrozenie partii (pamiec na godzine).</summary>
        private static float ThreatOf(MobileParty mp, double now, out MobileParty who)
        {
            Thr c;
            if (_thr.TryGetValue(mp.Id, out c) && c.H == now) { who = c.Who; return c.T; }
            float t = ThreatAt(mp.GetPosition2D, mp.MapFaction, mp.IsCurrentlyAtSea, mp, false, out who);
            _thr[mp.Id] = new Thr { H = now, T = t, Who = who };
            return t;
        }

        /// <summary>Twierdza niebezpieczna: oblezona albo wrogi lord / armia / gracz w zasiegu (bandy nie biora twierdz). Pamiec na godzine.</summary>
        private static bool FortUnsafe(Settlement st, IFaction f, double now)
        {
            if (st.IsUnderSiege || st.SiegeEvent != null) return true;
            Thr c;
            if (_fortThr.TryGetValue(st, out c) && c.H == now) return c.T > 0f;
            MobileParty who;
            float t = ThreatAt(st.GetPosition2D, f, false, null, true, out who);
            _fortThr[st] = new Thr { H = now, T = t, Who = who };
            return t > 0f;
        }

        private static string Nm(MobileParty mp) { try { return mp != null && mp.Name != null ? mp.Name.ToString() : "?"; } catch { return "?"; } }
        private static string Hn(Hero h) { try { return h != null && h.Name != null ? h.Name.ToString() : "?"; } catch { return "?"; } }
        private static string Sn(Settlement s) { try { return s != null && s.Name != null ? s.Name.ToString() : "-"; } catch { return "?"; } }
        private static string F2(double v) { return v.ToString("0.00", Inv); }
        private static string F0(double v) { return v.ToString("0", Inv); }
        private static string F1(double v) { return v.ToString("0.0", Inv); }

        private static St StateOf(MobileParty mp) { St s; return mp != null && _st.TryGetValue(mp.Id, out s) ? s : null; }

        /// <summary>Partia narazona teraz (stan od kiedy, nie zwolniona). Watek glowny.</summary>
        internal static bool Exposed(MobileParty mp) { var s = StateOf(mp); return s != null && s.SinceH >= 0; }

        private static void SetNoAtk(MobileParty mp, bool on)
        {
            var cur = _noAtk;
            if (cur.Contains(mp.Id) == on) return;
            var nu = new HashSet<MBGUID>(cur);
            if (on) nu.Add(mp.Id); else nu.Remove(mp.Id);
            _noAtk = nu;
        }

        private static void Drop(MobileParty mp)
        {
            if (mp == null) return;
            _st.Remove(mp.Id);
            SetNoAtk(mp, false);
            _joinQ.Remove(mp);
            _escQ.Remove(mp);
        }

        // ------------------------------------------------------------ atak
        /// <summary>Postfiks ShouldConsiderAttacking (watki AI): narazona R1/R2 nie zaczepia partii poza bandami; wyjatek - obrona osady wlasnego rodu. Nie rzuca.</summary>
        public static void AttackPostfix(MobileParty __0, MobileParty __1, ref bool __result)
        {
            if (!__result || __0 == null || __1 == null) return;
            try
            {
                var set = _noAtk;
                if (set.Count == 0 || !set.Contains(__0.Id)) return;
                if (__1.IsBandit) return;
                if (ArmsFirst.DefendsOwn(__0, __1)) return;
                __result = false;
                lock (Gate) { _vAtkCalls++; _vAtk.Add(__0.Id); }
            }
            catch { }
        }

        // ------------------------------------------------------------ mysl
        private static bool IsBattle(in AIBehaviorData d, Clan clan, bool keepKin, MobileParty party)
        {
            switch (d.AiBehavior)
            {
                case AiBehavior.BesiegeSettlement:
                case AiBehavior.AssaultSettlement:
                case AiBehavior.RaidSettlement:
                    return true;
                case AiBehavior.DefendSettlement:
                    var st = d.Party as Settlement;
                    if (st != null && clan != null && st.OwnerClan == clan) return false;
                    if (keepKin && ArmsFirst.IsKinDefend(in d, party)) return false;
                    return true;
                case AiBehavior.GoAroundParty:
                case AiBehavior.EngageParty:
                    var t = d.Party as MobileParty;
                    return t == null || !t.IsBandit;
                default:
                    return false;
            }
        }

        /// <summary>Postfiks Priority.Last CampaignEventDispatcher.AiHourlyTick (po p9 VeryLow, OB5/WatchLaw Low, CrossingLaw i ROT Normal). Nie rzuca.</summary>
        public static void ThinkPostfix(MobileParty party, PartyThinkParams partyThinkParams)
        {
            if (party == null || partyThinkParams == null || !party.IsLordParty || party.IsMainParty) return;
            try
            {
                var s = Settings.Current;
                if (s == null || !s.LordsAvoidDanger) return;
                double now = Math.Floor(CampaignTime.Now.ToHours);
                Snap(party, now);   // obraz glowy rodu do linii pojmania (sonda)
                if (!Covered(party)) { if (_st.Count > 0) Drop(party); return; }
                if (party.Army != null) { if (_st.Count > 0) Drop(party); return; }   // w armii bezpieczny - koniec stanu
                if (party.MapEvent != null || party.BesiegedSettlement != null || party.IsCurrentlyAtSea) return;
                if (party.CurrentSettlement != null && party.CurrentSettlement.SiegeEvent != null) return;
                if (ArmsFirst.Unready(party) || ArmsFirst.CapTripActive(party)) { SetNoAtk(party, false); return; }   // p9/159c najpierw
                if (partyThinkParams.DoNotChangeBehavior) return;
                if (party.Party.IsStarving) { _dStarve++; return; }   // najpierw jedzenie - droga gry
                var scores = partyThinkParams.AIBehaviorScores;
                if (scores == null) return;
                _dLords.Add(party.Id);
                var h = party.LeaderHero;
                var clan = party.ActualClan;
                int rank = RankOf(h, clan);
                float ratio = Ratio(), fill = Fill(party), S = Str(party);
                MobileParty who;
                float T = ThreatOf(party, now, out who);
                bool low = rank <= 2 && fill < ratio, weaker = T > 0f && S < T;
                var st = StateOf(party);
                if (st != null && st.FreeUntilH > now) { SetNoAtk(party, false); return; }   // zwolniony - walczy tym, co ma
                if (!low && !weaker)
                {
                    if (st != null) { if (st.SinceH >= 0) _dRebuilt++; Drop(party); }
                    return;
                }
                if (st == null)
                {
                    st = new St { P = party };
                    Saved sv;
                    if (party.StringId != null && _saved.TryGetValue(party.StringId, out sv))
                    {
                        _saved.Remove(party.StringId);
                        if (sv.Free > now) { st.FreeUntilH = sv.Free; _st[party.Id] = st; return; }
                        if (sv.Since >= 0 && sv.Since <= now) st.SinceH = sv.Since;
                    }
                    if (st.SinceH < 0) st.SinceH = now;
                    _st[party.Id] = st;
                }
                else if (st.SinceH < 0) { st.SinceH = now; st.FreeUntilH = -1; }
                st.Rank = rank;
                int wait = ArmsFirst.WaitDays(s);
                if (now - st.SinceH > wait * 24.0)
                {
                    // regula Jeffa 12.10 (nie bezczynnosc): po AiArmsWaitMaxDays zwolniony na AiArmsTripCooldownDays - walczy tym, co ma
                    _dFreed++;
                    Example(party, h, rank, fill, ratio, S, T, who, "zwolniony po " + wait + " dobach - walczy tym, co ma (" + ArmsFirst.CoolDays(s) + " dob bez stanu)");
                    st.SinceH = -1; st.FreeUntilH = now + Math.Max(1, ArmsFirst.CoolDays(s)) * 24.0; st.Fort = null; st.ArriveH = -1; st.Act = "";
                    SetNoAtk(party, false); _joinQ.Remove(party); _escQ.Remove(party);
                    return;
                }
                _dWeak.Add(party.Id);
                if (rank <= 2)
                {
                    _dGreat.Add(party.Id);
                    if (low) _dLow.Add(party.Id);
                    if (weaker) _dWeaker.Add(party.Id);
                }
                SetNoAtk(party, rank <= 2);

                float other = 0f, own = -1f, kin = -1f;
                HashSet<Settlement> blocked = null;
                for (int i = 0; i < scores.Count; i++)
                {
                    var e = scores[i];
                    if (e.Item2 <= -1f)
                    {
                        var bs = e.Item1.Party as Settlement;
                        if (bs != null) { if (blocked == null) blocked = new HashSet<Settlement>(); blocked.Add(bs); }
                        continue;
                    }
                    if (ArmsFirst.IsOwnDefend(in e.Item1, clan)) { if (e.Item2 > own) own = e.Item2; }
                    else if (ArmsFirst.IsKinDefend(in e.Item1, party)) { if (e.Item2 > kin) kin = e.Item2; }
                    else if (e.Item2 > other) other = e.Item2;
                }
                bool keepKin = own < 0f && kin >= 0f && kin >= other;
                string act = null;
                if (own >= 0f) { _dOwnDef.Add(party.Id); act = "broni osady wlasnego rodu"; }
                else if (keepKin) { _dKin.Add(party.Id); act = "odsiecz osady krolestwa (OB5)"; }
                else
                {
                    Army army = s.LordsJoinNearArmy ? NearArmy(party, h) : null;
                    MobileParty esc = army == null && T > 0f ? Escort(party, T, S) : null;
                    if (army != null)
                    {
                        _joinQ[party] = army;
                        act = "do armii " + Nm(army.LeaderParty);
                    }
                    else if (esc != null)
                    {
                        AimEscort(partyThinkParams, scores, esc, other);
                        _dFollow.Add(party.Id);
                        act = "za silniejszym " + Nm(esc);   // bez sily w napisie - inaczej nowy przyklad co mysl
                    }
                    else
                    {
                        var fort = Fort(party, now, s, blocked);
                        if (fort != null)
                        {
                            Aim(partyThinkParams, scores, fort, other);
                            bool inside = party.CurrentSettlement == fort;
                            if (st.Fort != fort) { st.Fort = fort; st.ArriveH = -1; }
                            if (inside)
                            {
                                if (st.ArriveH < 0) { st.ArriveH = now; _dArrived++; }
                                _dStay.Add(party.Id);
                                act = "postoj w " + Sn(fort);
                            }
                            else { _dToFort.Add(party.Id); act = "do twierdzy " + Sn(fort) + (fort.OwnerClan == clan ? " (wlasna)" : " (krolestwa)"); }
                        }
                        else { _dNoFort.Add(party.Id); act = "brak bezpiecznej twierdzy w zasiegu"; }
                    }
                }
                // opcje bitwy -1 (kopia listy - SetBehaviorScore zmienia elementy); obrona wlasnej osady zostaje, odsiecz krolestwa - gdy najlepsza
                var copy = new List<(AIBehaviorData, float)>(scores.Count);
                for (int i = 0; i < scores.Count; i++) copy.Add(scores[i]);
                foreach (var it in copy)
                {
                    if (!(it.Item2 > -1f)) continue;
                    var d = it.Item1;
                    if (!IsBattle(in d, clan, keepKin, party)) continue;
                    partyThinkParams.SetBehaviorScore(in d, -1f);
                }
                if (act != null && act != st.Act)
                {
                    st.Act = act;
                    Example(party, h, rank, fill, ratio, S, T, who, act);
                }
            }
            catch (Exception e) { Stumble("ThinkPostfix", e); }
        }

        private static void Example(MobileParty party, Hero h, int rank, float fill, float ratio, float S, float T, MobileParty who, string act)
        {
            if (_dExamples >= MaxExamples) return;
            _dExamples++;
            try
            {
                var r = party.MemberRoster;
                Log.Info("Eskorta lordow (223): " + Hn(h) + " (" + RankName(rank) + ") ludzi " + (r != null ? r.TotalManCount : 0) + ", zdrowych " + (r != null ? r.TotalHealthyCount : 0)
                         + ", wypelnienie " + F2(fill) + "/" + F2(ratio) + ", sila " + F0(S) + " / zagrozenie " + F0(T)
                         + (who != null ? " (" + Nm(who) + ", " + (who.Army != null ? "armia" : (who.MemberRoster != null ? who.MemberRoster.TotalManCount + " ludzi" : "?")) + ")" : "")
                         + " -> " + act + ".");
            }
            catch (Exception e) { Stumble("Example", e); }
        }

        /// <summary>Ocena GoToSettlement do twierdzy - wzor p9 (ArmsFirst.Aim): max(FloorScore, Edge x najlepsza inna) + 0.01, droga ladowa; wyniku &lt;= -1 nie podnosimy.</summary>
        private static void Aim(PartyThinkParams p, MBReadOnlyList<(AIBehaviorData, float)> scores, Settlement fort, float other)
        {
            float nv = Math.Max(ArmsFirst.FloorScore, ArmsFirst.Edge * other) + 0.01f;
            for (int i = 0; i < scores.Count; i++)
            {
                var d = scores[i].Item1;
                if (d.AiBehavior != AiBehavior.GoToSettlement || d.WillGatherArmy || d.NavigationType != MobileParty.NavigationType.Default || !ReferenceEquals(d.Party, fort)) continue;
                if (scores[i].Item2 > -1f && scores[i].Item2 < nv) p.SetBehaviorScore(in d, nv);
                return;
            }
            p.AddBehaviorScore((new AIBehaviorData(fort, AiBehavior.GoToSettlement, MobileParty.NavigationType.Default, false, false, false), nv));
        }

        /// <summary>Ocena EscortParty za silniejsza przyjazna partia - ta sama co wyjazd do twierdzy.</summary>
        private static void AimEscort(PartyThinkParams p, MBReadOnlyList<(AIBehaviorData, float)> scores, MobileParty esc, float other)
        {
            float nv = Math.Max(ArmsFirst.FloorScore, ArmsFirst.Edge * other) + 0.01f;
            for (int i = 0; i < scores.Count; i++)
            {
                var d = scores[i].Item1;
                if (d.AiBehavior != AiBehavior.EscortParty || !ReferenceEquals(d.Party, esc)) continue;
                if (scores[i].Item2 > -1f && scores[i].Item2 < nv) p.SetBehaviorScore(in d, nv);
                return;
            }
            p.AddBehaviorScore((new AIBehaviorData(esc, AiBehavior.EscortParty, MobileParty.NavigationType.Default, false, false, false), nv));
        }

        /// <summary>Armia wlasnego krolestwa w zasiegu (najblizsza): wodz czynny, poza bitwa i morzem, nie gracz; jedzenie partii > MinimumNeededFoodInDaysToCallToArmy;
        /// wplyw rodu wodza >= koszt wezwania (CalculatePartyInfluenceCost - gra pobiera go przy dolaczeniu). Wladca krolestwa nie idzie do cudzej armii (regula gry).</summary>
        private static Army NearArmy(MobileParty party, Hero h)
        {
            var k = party.MapFaction as Kingdom;
            if (k == null || h == null || k.Leader == h || k.Armies == null || k.Armies.Count == 0) return null;
            var model = Campaign.Current.Models.ArmyManagementCalculationModel;
            if (model == null || party.GetNumDaysForFoodToLast() <= model.MinimumNeededFoodInDaysToCallToArmy) return null;
            var pos = party.GetPosition2D;
            float r = Range, bd = float.MaxValue;
            Army best = null;
            foreach (var a in k.Armies)
            {
                var lp = a != null ? a.LeaderParty : null;
                if (!ArmyOk(a, lp, party, model)) continue;
                float d = pos.Distance(lp.GetPosition2D);
                if (d > r || d >= bd) continue;
                best = a; bd = d;
            }
            return best;
        }

        private static bool ArmyOk(Army a, MobileParty lp, MobileParty party, TaleWorlds.CampaignSystem.ComponentInterfaces.ArmyManagementCalculationModel model)
        {
            if (a == null || lp == null || !lp.IsActive || lp.IsMainParty || lp.MapEvent != null || lp.IsCurrentlyAtSea || lp.LeaderHero == null) return false;
            if (a.Kingdom != party.MapFaction) return false;
            if (MobileParty.MainParty != null && MobileParty.MainParty.Army == a) return false;   // nie armia gracza
            var lc = lp.LeaderHero.Clan;
            if (lc == null) return false;
            int cost = model.CalculatePartyInfluenceCost(lp, party);
            return lc.Influence >= cost;
        }

        /// <summary>Silniejsza przyjazna partia lorda w zasiegu (najblizsza): sila >= zagrozenia i > wlasnej, poza armia, bitwa, osada, oblezeniem; sama nie narazona; nie gracz.</summary>
        private static MobileParty Escort(MobileParty party, float T, float S)
        {
            var pos = party.GetPosition2D;
            var f = party.MapFaction;
            float r = Range, bd = float.MaxValue;
            MobileParty best = null;
            var data = MobileParty.StartFindingLocatablesAroundPosition(pos, r);
            for (var t = MobileParty.FindNextLocatable(ref data); t != null; t = MobileParty.FindNextLocatable(ref data))
            {
                try
                {
                    if (t == party || !t.IsActive || !t.IsLordParty || t.IsMainParty || t.MapFaction != f || t.LeaderHero == null) continue;
                    if (t.Army != null || t.MapEvent != null || t.CurrentSettlement != null || t.BesiegedSettlement != null || t.IsCurrentlyAtSea != party.IsCurrentlyAtSea) continue;
                    if (Exposed(t) || ArmsFirst.Unready(t)) continue;
                    float d = pos.Distance(t.GetPosition2D);
                    if (d > r || d >= bd) continue;
                    float st = Str(t);
                    if (st < T || st <= S) continue;
                    best = t; bd = d;
                }
                catch (Exception e) { Stumble("Escort", e); }
            }
            return best;
        }

        /// <summary>Bezpieczna twierdza: najblizsza droga ladowa wlasnego rodu, potem najblizsza przyjazna (to samo krolestwo) w zasiegu AiArmsTripMaxDays dob marszu
        /// (wzor ArmsFirst.Nearest), bez oblezenia, bez wrogiego lorda w zasiegu, po tej samej stronie Muru, nie zablokowana cudza latka (wynik &lt;= -1).</summary>
        private static Settlement Fort(MobileParty party, double now, Settings s, HashSet<Settlement> blocked)
        {
            var c = Campaign.Current;
            var model = c != null && c.Models != null ? c.Models.MapDistanceModel : null;
            if (model == null) return null;
            float range = ArmsFirst.TripDays(s) * 24f * Math.Max(0.5f, c.EstimatedAverageLordPartySpeed);
            var at = party.CurrentSettlement;
            var pos = party.GetPosition2D;
            var f = party.MapFaction;
            var clan = party.ActualClan;
            // w przyjaznej twierdzy (bez oblezenia), a wrog w zasiegu - zostaje w murach (nie wyjezdza do innej twierdzy obok wroga)
            if (at != null && at.IsFortification && at.MapFaction == f && !at.IsUnderSiege && (blocked == null || !blocked.Contains(at)) && FortUnsafe(at, f, now)) return at;
            var bw = ArmsFirst.Beyond();
            bool pBeyond = false;
            if (bw != null) { try { pBeyond = bw(pos); } catch { bw = null; } }
            var cand = new List<(Settlement st, float d, bool own)>();
            foreach (var tw in Town.AllFiefs)
            {
                var st = tw != null ? tw.Settlement : null;
                if (st == null || st.MapFaction != f || st.IsUnderSiege) continue;
                if (blocked != null && blocked.Contains(st)) continue;
                if (pos.Distance(st.GetPosition2D) > range) continue;
                float d;
                if (at == st) d = 0f;
                else
                {
                    float lr;
                    d = at != null ? model.GetDistance(at, st, false, false, MobileParty.NavigationType.Default)
                                   : model.GetDistance(party, st, false, MobileParty.NavigationType.Default, out lr);
                    if (CartGuard.NoLandRoad(d)) continue;
                }
                if (d > range) continue;
                if (bw != null) { try { if (bw(st.GetPosition2D) != pBeyond) continue; } catch { } }
                cand.Add((st, d, st.OwnerClan == clan));
            }
            if (cand.Count == 0) return null;
            cand.Sort((x, y) => x.own != y.own ? (x.own ? -1 : 1) : x.d.CompareTo(y.d));
            int checks = 0;
            foreach (var x in cand)
            {
                if (checks++ >= MaxFortChecks) break;
                if (!FortUnsafe(x.st, f, now)) return x.st;
            }
            return null;
        }

        // ------------------------------------------------------------ godzina: dolaczenie do armii, eskorta po wejsciu do twierdzy
        internal static void Hourly()
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.LordsAvoidDanger)
                {
                    if (_st.Count > 0 || _noAtk.Count > 0 || _joinQ.Count > 0 || _escQ.Count > 0) { _st.Clear(); _noAtk = new HashSet<MBGUID>(); _joinQ.Clear(); _escQ.Clear(); }
                    return;
                }
                if (_joinQ.Count > 0)
                {
                    var q = _joinQ.ToList();
                    _joinQ.Clear();
                    var model = Campaign.Current.Models.ArmyManagementCalculationModel;
                    foreach (var kv in q)
                    {
                        var p = kv.Key; var a = kv.Value;
                        try
                        {
                            bool ok = s.LordsJoinNearArmy && p != null && p.IsActive && p.Army == null && p.MapEvent == null && Exposed(p) && !ArmsFirst.Unready(p)
                                      && (p.CurrentSettlement == null || p.CurrentSettlement.SiegeEvent == null) && !p.IsCurrentlyAtSea && a != null && a.LeaderParty != null
                                      && a.LeaderParty.Army == a && ArmyOk(a, a.LeaderParty, p, model)
                                      && p.GetPosition2D.Distance(a.LeaderParty.GetPosition2D) <= Range;
                            if (!ok) { _dJoinDrop++; continue; }
                            p.Army = a;   // jak gra przy wezwaniu (Army.Gather: item.Army = this) - koszt wplywu wodza i RethinkAtNextHourlyTick w OnAddPartyInternal
                            _dJoined++;
                            Log.Info("Eskorta lordow (223): do armii - " + Hn(p.LeaderHero) + " dolaczyl do armii " + Nm(a.LeaderParty) + " (" + a.Parties.Count + " partii, sila "
                                     + F0(a.EstimatedStrength) + ").");
                            Drop(p);
                        }
                        catch (Exception e) { Stumble("Hourly.Join", e); }
                    }
                }
                if (_escQ.Count > 0)
                {
                    var q = _escQ.ToList();
                    _escQ.Clear();
                    foreach (var kv in q)
                    {
                        try { if (kv.Key != null && kv.Key.IsActive && kv.Key.CurrentSettlement == kv.Value) TakeEscort(kv.Key, kv.Value, "wejscie", s); }
                        catch (Exception e) { Stumble("Hourly.Escort", e); }
                    }
                }
                // partie nieczynne albo w armii - koniec stanu; wygasle zwolnienia
                if (_st.Count > 0)
                {
                    double now = Math.Floor(CampaignTime.Now.ToHours);
                    List<MBGUID> rm = null;
                    foreach (var kv in _st)
                    {
                        var p = kv.Value.P;
                        if (p == null || !p.IsActive || p.Army != null || (kv.Value.SinceH < 0 && kv.Value.FreeUntilH <= now)) { if (rm == null) rm = new List<MBGUID>(); rm.Add(kv.Key); }
                    }
                    if (rm != null) foreach (var k in rm) { var p = _st[k].P; _st.Remove(k); if (p != null) SetNoAtk(p, false); }
                }
                if (_thr.Count > 2000) _thr.Clear();
                if (_fortThr.Count > 500) _fortThr.Clear();
            }
            catch (Exception e) { Stumble("Hourly", e); }
        }

        /// <summary>SettlementEntered: narazony lord w twierdzy wlasnego rodu - eskorta w nastepnej godzinie (po wymianie z zaloga gry przy wejsciu).</summary>
        internal static void OnEntered(MobileParty mp, Settlement st, Hero hero)
        {
            try
            {
                if (mp == null || st == null || !On || !st.IsFortification || !Exposed(mp)) return;
                if (st.OwnerClan != mp.ActualClan) return;
                _escQ[mp] = st;
            }
            catch (Exception e) { Stumble("OnEntered", e); }
        }

        /// <summary>
        /// Eskorta z zalogi: narazony lord w twierdzy WLASNEGO rodu bierze ludzi zalogi do progu wypelnienia - metoda gry TakeTroopsFromGarrison (prawdziwi ludzie,
        /// 171 sprzet z ludzmi, ksiega ludzi, 169c). Nie ponad PartySizeLimit; zaloga najmniej 75/125; bez eskorty przy oblezeniu, wrogim lordzie w zasiegu twierdzy
        /// i przy przekroczonym limicie zoldu partii (jak gra). Watek glowny.
        /// </summary>
        private static void TakeEscort(MobileParty p, Settlement st, string why, Settings s)
        {
            if (!s.LordEscortFromGarrison || _take == null || !Exposed(p) || p.Army != null || p.MapEvent != null) return;
            if (st == null || !st.IsFortification || st.Town == null || st.OwnerClan != p.ActualClan || p.CurrentSettlement != st) return;
            var g = st.Town.GarrisonParty;
            if (g == null || g.MemberRoster == null || g.Party == null) return;
            double now = Math.Floor(CampaignTime.Now.ToHours);
            if (FortUnsafe(st, p.MapFaction, now)) { _dEscHeld++; return; }
            if (p.IsWageLimitExceeded()) { _dEscNoRoom++; return; }
            float ratio = Ratio(), normal = Normal(p);
            int healthy = p.MemberRoster.TotalHealthyCount;
            int want = (int)Math.Ceiling(ratio * normal) - healthy;
            int room = p.Party.PartySizeLimit - p.Party.NumberOfAllMembers;
            int floor = st.IsTown ? FloorTown : FloorCastle;
            int gBefore = g.MemberRoster.TotalManCount, avail = g.Party.NumberOfRegularMembers - floor;
            if (want <= 0) return;
            if (room <= 0) { _dEscNoRoom++; return; }
            // limit zoldu partii (166 SetWagePaymentLimit): nie wiecej ludzi, niz zmiesci sie w nim po sredniej placy gry (ta sama miara co FindPartySizeNormalLimit) -
            // inaczej 166 zwalnialby ich najpierw z tej partii (nadwyzka ponad wlasny limit)
            int wageRoom = (int)Math.Min(int.MaxValue, Math.Max(0L, (long)p.PaymentLimit - p.TotalWage) / Math.Max(1f, Campaign.Current.AverageWage));
            if (wageRoom <= 0) { _dEscNoRoom++; return; }
            int n = Math.Min(Math.Min(want, wageRoom), Math.Min(room, avail));
            if (n <= 0) return;   // zaloga na podlodze - lord czeka w murach (odbudowa werbunkiem gry albo zwolnienie po AiArmsWaitMaxDays)
            var beh = Campaign.Current.GetCampaignBehavior<GarrisonTroopsCampaignBehavior>();
            if (beh == null) return;
            int pBefore = p.MemberRoster.TotalManCount;
            float fill0 = healthy / normal;
            _take.Invoke(beh, new object[] { p, st, n, false });
            var g2 = st.Town.GarrisonParty;
            int gAfter = g2 != null && g2.MemberRoster != null ? g2.MemberRoster.TotalManCount : 0;
            int moved = gBefore - gAfter, got = p.MemberRoster.TotalManCount - pBefore;
            if (moved <= 0 && got <= 0) return;
            _dEsc++; _dEscMen += got; _dEscForts.Add(st);
            int regAfter = g2 != null && g2.Party != null ? g2.Party.NumberOfRegularMembers : 0;
            if (regAfter < floor) _dEscBelow++;
            try { ArmedStrength.MarkDirty(p); if (g2 != null) ArmedStrength.MarkDirty(g2); } catch { }
            Log.Info("Eskorta lordow (223): eskorta z zalogi (" + why + "): " + Hn(p.LeaderHero) + " w " + Sn(st) + " wzial " + got + " (zaloga " + gBefore + " -> " + gAfter
                     + ", podloga " + floor + ", zgodnosc " + (moved == got ? "TAK" : "NIE") + "), wypelnienie " + F2(fill0) + " -> " + F2(Fill(p)) + "/" + F2(ratio) + ".");
        }

        // ------------------------------------------------------------ doba
        internal static void Daily()
        {
            var s = Settings.Current;
            if (s == null) return;
            try
            {
                if (!s.LordsAvoidDanger) { ClearDay(); return; }
                // doba postoju w twierdzy wlasnego rodu - eskorta z zalogi (zaloga mogla urosnac werbunkiem gry)
                if (s.LordEscortFromGarrison && _st.Count > 0)
                {
                    foreach (var st in _st.Values.ToList())
                    {
                        try { if (st.SinceH >= 0 && st.P != null && st.P.IsActive && st.P.CurrentSettlement != null) TakeEscort(st.P, st.P.CurrentSettlement, "doba", s); }
                        catch (Exception e) { Stumble("Daily.Escort", e); }
                    }
                }
                double now = Math.Floor(CampaignTime.Now.ToHours), longest = 0;
                string longName = "-";
                int exposedNow = 0, freeNow = 0;
                foreach (var st in _st.Values)
                {
                    if (st.SinceH >= 0)
                    {
                        exposedNow++;
                        double dd = (now - st.SinceH) / 24.0;
                        if (dd > longest) { longest = dd; longName = Hn(st.P != null ? st.P.LeaderHero : null); }
                    }
                    else if (st.FreeUntilH > now) freeNow++;
                }
                int a, aCalls;
                lock (Gate) { a = _vAtk.Count; aCalls = _vAtkCalls; }
                var sb = new StringBuilder(900);
                sb.Append("Eskorta lordow (223): dzien ").Append((int)CampaignTime.Now.ToDays)
                  .Append(" | partii lordow AI ").Append(_dLords.Count).Append(", slabe ").Append(_dWeak.Count)
                  .Append(" (wladcy i glowy rodow ").Append(_dGreat.Count).Append(": ponizej ").Append(F2(Ratio())).Append(' ').Append(_dLow.Count)
                  .Append(", slabsze od zagrozenia ").Append(_dWeaker.Count).Append(')')
                  .Append(" | dzialania: do twierdzy ").Append(_dToFort.Count).Append(" (dojechalo ").Append(_dArrived).Append("), postoj ").Append(_dStay.Count)
                  .Append(", do armii ").Append(_dJoined).Append(" (odpadlo ").Append(_dJoinDrop).Append("), za silniejszym ").Append(_dFollow.Count)
                  .Append(", eskorta z zalog ").Append(_dEsc).Append(" razy (ludzi ").Append(_dEscMen).Append(", twierdz ").Append(_dEscForts.Count)
                  .Append(", zaloga ponizej podlogi ").Append(_dEscBelow).Append(", wstrzymana - wrog przy twierdzy ").Append(_dEscHeld).Append(", bez miejsca lub zoldu ").Append(_dEscNoRoom).Append(')')
                  .Append(", odbudowani ").Append(_dRebuilt).Append(", zwolnieni po ").Append(ArmsFirst.WaitDays(s)).Append(" dobach ").Append(_dFreed)
                  .Append(", bez twierdzy w zasiegu ").Append(_dNoFort.Count).Append(", obrona wlasnej osady ").Append(_dOwnDef.Count).Append(", odsiecz krolestwa ").Append(_dKin.Count)
                  .Append(", glod ").Append(_dStarve)
                  .Append(" | atak wstrzymany ").Append(a).Append(" partii (").Append(aCalls).Append(" razy)")
                  .Append(" | ").Append(ProbeText())
                  .Append(" | teraz w stanie ").Append(exposedNow).Append(", zwolnionych ").Append(freeNow)
                  .Append(" | najdluzej w stanie ").Append(F1(longest)).Append(" dob (").Append(longName).Append(')')
                  .Append(" | potkniecia ").Append(_stumbles).Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Daily", e); }
            ClearDay();
        }

        // ------------------------------------------------------------ zapis
        /// <summary>"1|StringId,odH,zwolnionyDoH;..." - partie w stanie albo zwolnione, plus wpisy z zapisu jeszcze nie przeliczone.</summary>
        internal static string Export()
        {
            var sb = new StringBuilder("1|");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            double now = Math.Floor(CampaignTime.Now.ToHours);
            bool first = true;
            foreach (var st in _st.Values)
            {
                var p = st.P;
                if (p == null || !p.IsActive) continue;
                string id = p.StringId;
                if (string.IsNullOrEmpty(id) || id.IndexOfAny(Bad) >= 0 || !seen.Add(id)) continue;
                if (st.SinceH < 0 && st.FreeUntilH <= now) continue;
                Append(sb, ref first, id, st.SinceH, st.FreeUntilH);
            }
            foreach (var kv in _saved) if (seen.Add(kv.Key)) Append(sb, ref first, kv.Key, kv.Value.Since, kv.Value.Free);
            return sb.ToString();
        }

        private static readonly char[] Bad = { ',', ';', '|' };

        private static void Append(StringBuilder sb, ref bool first, string id, double since, double free)
        {
            if (!first) sb.Append(';');
            first = false;
            sb.Append(id).Append(',').Append(since.ToString("0.##", Inv)).Append(',').Append(free.ToString("0.##", Inv));
        }

        /// <summary>Wczytanie (po Reset z konstruktora zachowania): brak klucza albo zly format - pusto (stan od nowa).</summary>
        internal static void Import(string v)
        {
            _saved.Clear(); _savedLoaded = 0;
            if (string.IsNullOrEmpty(v) || !v.StartsWith("1|", StringComparison.Ordinal)) return;
            foreach (var row in v.Substring(2).Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var c = row.Split(',');
                double since, free;
                if (c.Length < 3 || c[0].Length == 0 || !double.TryParse(c[1], NumberStyles.Float, Inv, out since) || !double.TryParse(c[2], NumberStyles.Float, Inv, out free)) continue;
                _saved[c[0]] = new Saved { Since = since, Free = free };
                _savedLoaded++;
            }
        }

        // ------------------------------------------------------------ start sesji
        internal static void RegisterEvents(CampaignBehaviorBase owner)
        {
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(owner, DayProfile.Hr("LordEscort.Hourly", Hourly));
            CampaignEvents.SettlementEntered.AddNonSerializedListener(owner, DayProfile.Settle("LordEscort.OnEntered", OnEntered));
            CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(owner, OnPrisonerTaken);
            CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(owner, (mp, pb) => { try { Drop(mp); } catch { } });
            _events = true;
        }

        private static string Patch(MethodInfo m, string what, string post, int prio)
        {
            if (m == null) return "BRAK (" + what + ")";
            if (_patched.Contains(m)) return "TAK (juz wczesniej w tym procesie)";
            _h.Patch(m, postfix: new HarmonyMethod(typeof(LordEscort), post) { priority = prio });
            _patched.Add(m);
            return "TAK";
        }

        /// <summary>Postfiksy celu z priorytetem Priority.Last poza nasza latka (kolejnosc miedzy nimi - wedlug wpiecia): wlasciciel/metoda.</summary>
        private static string LastOwners(MethodBase m)
        {
            try
            {
                var info = m != null ? Harmony.GetPatchInfo(m) : null;
                if (info == null) return "-";
                var o = info.Postfixes.Where(x => x.priority <= Priority.Last && x.owner != _h.Id).Select(x => x.owner + "/" + (x.PatchMethod != null ? x.PatchMethod.DeclaringType.Name : "?")).Distinct().ToList();
                return o.Count > 0 ? string.Join(", ", o.ToArray()) : "brak";
            }
            catch { return "?"; }
        }

        /// <summary>OnSessionLaunched (po McmSettings.Apply i po ArmedStrength.SessionStart - p9 wpiety): latki raz na proces, linia startu.</summary>
        internal static void SessionStart()
        {
            if (_h == null) _h = new Harmony("com.jeff.armoury.lordescort223");
            try
            {
                var m = AccessTools.Method(typeof(CampaignEventDispatcher), "AiHourlyTick", new[] { typeof(MobileParty), typeof(PartyThinkParams) });
                _hThink = Patch(m, "CampaignEventDispatcher.AiHourlyTick", nameof(ThinkPostfix), Priority.Last);
                if (m != null) _hThink += " (inne postfiksy Last: " + LastOwners(m) + ")";
            }
            catch (Exception e) { _hThink = "wyjatek " + e.GetType().Name; Stumble("SessionStart.Think", e); }
            try
            {
                var m = AccessTools.DeclaredMethod(typeof(DefaultMobilePartyAIModel), "ShouldConsiderAttacking", new[] { typeof(MobileParty), typeof(MobileParty) });
                _hAttack = Patch(m, "DefaultMobilePartyAIModel.ShouldConsiderAttacking", nameof(AttackPostfix), Priority.Last);
            }
            catch (Exception e) { _hAttack = "wyjatek " + e.GetType().Name; Stumble("SessionStart.Attack", e); }
            try
            {
                _take = AccessTools.Method(typeof(GarrisonTroopsCampaignBehavior), "TakeTroopsFromGarrison", new[] { typeof(MobileParty), typeof(Settlement), typeof(int), typeof(bool) });
                var beh = Campaign.Current != null ? Campaign.Current.GetCampaignBehavior<GarrisonTroopsCampaignBehavior>() : null;
                _hTake = _take == null ? "BRAK (GarrisonTroopsCampaignBehavior.TakeTroopsFromGarrison)" : (beh == null ? "BRAK (zachowanie zalog gry nieobecne)" : "TAK");
            }
            catch (Exception e) { _take = null; _hTake = "wyjatek " + e.GetType().Name; Stumble("SessionStart.Take", e); }
            try
            {
                var s = Settings.Current;
                if (s == null) return;
                Log.Info("Eskorta lordow (223): start sesji | klucze: Lords Avoid Danger " + (s.LordsAvoidDanger ? "TAK" : "NIE") + ", Lord Escort From Garrison "
                         + (s.LordEscortFromGarrison ? "TAK" : "NIE") + ", Lords Join Near Army " + (s.LordsJoinNearArmy ? "TAK" : "NIE")
                         + " | prog wypelnienia " + F2(Ratio()) + " (gra AIMobilePartySizeRatioToCallToArmy, BK IsAvailableForArmies) | zasieg " + F0(Range) + " (CivilWorksFrontRange)"
                         + " | twierdza najwyzej " + ArmsFirst.TripDays(s) + " dob drogi | postoj najwyzej " + ArmsFirst.WaitDays(s) + " dob, potem zwolniony na " + ArmsFirst.CoolDays(s) + " dob"
                         + " | podloga zalogi " + FloorCastle + "/" + FloorTown
                         + " | latki: mysl AI " + _hThink + ", atak " + _hAttack + ", wejscie do osady " + (_events ? "TAK" : "NIE") + ", pojmanie " + (_events ? "TAK" : "NIE")
                         + ", zaloga -> partia (metoda gry) " + _hTake
                         + " | stan z zapisu (arm_esc223): " + _savedLoaded + " partii.");
            }
            catch (Exception e) { Stumble("SessionStart.Line", e); }
        }
    }
}
