using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// MIARA BALANSU KROLESTW - paczka 175.0 (warunek Jeffa 09.10: "zeby nie zepsuc rownowagi gry, zeby nagle jedna armia nie bila
    /// wszystkich"; projekt rozdz. 4.4, progi 6.2 w tools/p175_balans.py). Tylko log, nic w grze nie zmienia (wylacznik Army175Measure).
    /// H3 loguje bitwe polowa z nazwa krolestwa, ludzmi, % konnych, tierem i terenem, ale bez rodzaju strony, mocy z gry, kontekstu
    /// symulacji (snieg z pogody), oblezen i sum wedlug krolestwa - jej linii NIE ruszamy (parsuja ja skrypty audytu). Dopisujemy:
    ///  - "Bitwa: B175" (do bitwy.log) dla kazdej bitwy krolestwa z krolestwem i kazdej bitwy z partia Polnocy (takze z bandami):
    ///    typ, kontekst symulacji, region, strony (krolestwo, rodzaj: lord/armia/wies/karawana/zaloga/banda, partie, ludzie, moc z gry
    ///    = suma MilitaryPowerModel.GetTroopPower po skladzie sprzed bitwy, % konnych, % konnych lucznikow, % piechoty Polnocy t3+,
    ///    straty), stosunek sil, zwyciezca, przewaga Polnocy (ciosy z mnoznikiem NorthHomeEdge), osada. Moc NIE zawiera przewagi Polnocy
    ///    (ta siedzi w ciosie) - dzieki temu "wygrane ponad oczekiwane" mierza jej skutek. Sklad sprzed bitwy = zdrowi w partii + ranni,
    ///    polegli i rozbici w bitwie - migawka w prefiksie CalculateAndCommitMapEventResults (Priority 801, PRZED H3, ktora przesuwa
    ///    ludzi, i przed jencami); bitwa bez wyniku - ten sam rachunek przy MapEventEnded;
    ///  - "Balans krolestw (175)" raz na dobe + balans-krolestw.csv: bitwy lordow (pole, lord/armia na lord/armia dwoch krolestw)
    ///    wygrane / wyrownane (stosunek mocy &lt; 1.5), WPO (wygrane ponad oczekiwane: p = 1/(1+exp(-k ln(Ms/Mo))), k = 12.6 z bazy
    ///    kopiaT9-120), szturmy zdobyte / odparte / stracone, twierdze (stan, start sesji, zdobyte i stracone oblezeniem), sredni tier
    ///    zalog, ludzie w partiach lordow (udzial swiata) i zalogach, bitwy z przewaga Polnocy i straty w nich, wsie i karawany
    ///    rozbite przez Dothrakow; suma 7 Wolnych Miast (D6); "dominacja" - sygnal (nie hamulec). Liczniki narastajaco od startu sesji
    ///    (autotest to jedna sesja), stan = stan doby. Bez SyncData.
    /// </summary>
    internal static class KingdomBalance
    {
        internal const double K = 12.6;   // nachylenie p(wygranej) od ln(stosunku mocy) - baza kopiaT9-120 (projekt 4.4, siatka do 99.9)
        private static readonly string[] FreeCities = { "pentos", "myr", "lys", "tyrosh", "norvos", "qohor", "lorath" };
        private const string Header = "dzien;krolestwo;bitwy_lordow;wygrane;wyrownane;wyrownane_wygrane;wpo;szturmy_zdobyte;szturmy_odparte;twierdze;twierdze_start;"
            + "twierdze_zdobyte;twierdze_stracone;tier_zalog;ludzie_partie;ludzie_zalogi;bitwy_z_przewaga_polnocy;straty_wlasne_przewaga;straty_wroga_przewaga;"
            + "wsie_karawany_rozbite_przez_dothrakow;szturmy_stracone;ludzie_partie_pct_swiata;bitwy_b175;tier_partii";

        private sealed class KRow
        {
            public int Lord, Won, Close, CloseWon, StormWon, StormHeld, StormLost, FortTaken, FortLost, EdgeBattles, Raided, B175;
            public double Wpo; public long EdgeOwn, EdgeFoe;
            public int FortStart = -1;
            // stan doby
            public int Forts; public long PartyMen, GarrMen, GarrRegs, PartyRegs; public double GarrTiers, PartyTiers;
        }

        private sealed class SideSnap { public int Parties, Men, Mounted, HorseArchers, North, Loss; public double Power; }
        private sealed class Snap { public SideSnap A, D; }

        private static readonly Dictionary<string, KRow> _rows = new Dictionary<string, KRow>();
        private static readonly ConditionalWeakTable<MapEvent, Snap> _snaps = new ConditionalWeakTable<MapEvent, Snap>();
        private static int _lines, _linesDay, _lordDay, _edgeDay, _stumbles;
        private static bool _patched;

        internal static bool On { get { return HorseCensus.On; } }

        internal static void Reset() { _rows.Clear(); _lines = _linesDay = _lordDay = _edgeDay = _stumbles = 0; }

        private static KRow RowOf(string key)
        {
            KRow r;
            if (!_rows.TryGetValue(key, out r)) { r = new KRow(); _rows[key] = r; }
            return r;
        }
        private static KRow RowOf(IFaction f) { return RowOf(HorseCensus.KeyOf(f)); }

        // ------------------------------------------------------------ sklad stron
        private static void AddTroops(SideSnap ss, TroopRoster r, bool healthyOnly, MilitaryPowerModelProxy pm)
        {
            if (r == null) return;
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                var c = el.Character;
                if (c == null) continue;
                int n = healthyOnly ? el.Number - el.WoundedNumber : el.Number;
                if (n <= 0) continue;
                ss.Men += n;
                ss.Power += pm.Power(c) * n;
                if (c.IsHero) continue;
                if (c.IsMounted) { ss.Mounted += n; if (c.IsRanged) ss.HorseArchers += n; }
                if (NorthHomeEdge.InSet(c)) ss.North += n;
            }
        }

        /// <summary>Moc jednostki z modelu gry w kontekscie tej bitwy (bez przewagi Polnocy, ktora siedzi w ciosie).</summary>
        private sealed class MilitaryPowerModelProxy
        {
            private readonly TaleWorlds.CampaignSystem.ComponentInterfaces.MilitaryPowerModel _m;
            private readonly BattleSideEnum _side; private readonly MapEvent.PowerCalculationContext _ctx; private readonly float _lead;
            private readonly Dictionary<CharacterObject, float> _cache = new Dictionary<CharacterObject, float>();
            public MilitaryPowerModelProxy(MapEvent me, MapEventSide side)
            {
                _m = Campaign.Current.Models.MilitaryPowerModel; _side = side.MissionSide; _ctx = me.SimulationContext;
                var lh = side.LeaderParty != null ? side.LeaderParty.LeaderHero : null;
                _lead = 0f;
                try { if (lh != null) _lead = _m.GetPowerModifierOfHero(lh); } catch { }
            }
            public float Power(CharacterObject c)
            {
                float v;
                if (_cache.TryGetValue(c, out v)) return v;
                try { v = _m.GetTroopPower(c, _side, _ctx, _lead); } catch { v = 0f; }
                _cache[c] = v;
                return v;
            }
        }

        private static SideSnap SideOf(MapEvent me, MapEventSide side)
        {
            var ss = new SideSnap();
            if (side == null) return ss;
            var pm = new MilitaryPowerModelProxy(me, side);
            for (int i = 0; i < side.Parties.Count; i++)
            {
                var mep = side.Parties[i];
                if (mep == null || mep.Party == null) continue;
                ss.Parties++;
                // zdrowi na starcie = zdrowi teraz + ranni, polegli i rozbici w tej bitwie
                AddTroops(ss, mep.Party.MemberRoster, true, pm);
                AddTroops(ss, mep.WoundedInBattle, false, pm);
                AddTroops(ss, mep.DiedInBattle, false, pm);
                AddTroops(ss, mep.RoutedInBattle, false, pm);
                // straty walki (polegli + ranni) z chwili migawki - przed H3, ktora czesc poleglych zamienia na rozbitych i jencow
                if (mep.DiedInBattle != null) ss.Loss += mep.DiedInBattle.TotalManCount;
                if (mep.WoundedInBattle != null) ss.Loss += mep.WoundedInBattle.TotalManCount;
            }
            return ss;
        }

        private static Snap Compute(MapEvent me) { return new Snap { A = SideOf(me, me.AttackerSide), D = SideOf(me, me.DefenderSide) }; }

        /// <summary>Prefiks CalculateAndCommitMapEventResults (Priority 801 - przed H3 i przed jencami): migawka skladu stron.</summary>
        public static void ResultsPrefix(MapEvent __instance)
        {
            try
            {
                if (!On || __instance == null) return;
                Snap s;
                if (_snaps.TryGetValue(__instance, out s)) return;
                if (!Interesting(__instance)) return;
                _snaps.Add(__instance, Compute(__instance));
            }
            catch { _stumbles++; }
        }

        // ------------------------------------------------------------ strony
        private static bool IsNorthParty(PartyBase p)
        {
            if (NorthHomeEdge.NorthClan(p)) return true;
            var k = p != null ? p.MapFaction as Kingdom : null;
            return k != null && k.Culture != null && k.Culture.StringId == "battania";
        }

        private static bool AnyNorth(MapEventSide side)
        {
            if (side == null) return false;
            for (int i = 0; i < side.Parties.Count; i++) { var mep = side.Parties[i]; if (mep != null && IsNorthParty(mep.Party)) return true; }
            return false;
        }

        private static bool Interesting(MapEvent me)
        {
            var fa = me.AttackerSide != null ? me.AttackerSide.MapFaction : null;
            var fd = me.DefenderSide != null ? me.DefenderSide.MapFaction : null;
            return (fa is Kingdom && fd is Kingdom) || AnyNorth(me.AttackerSide) || AnyNorth(me.DefenderSide);
        }

        private static string Kind(MapEventSide side)
        {
            try
            {
                var lp = side != null ? side.LeaderParty : null;
                if (lp == null) return "inna";
                var mp = lp.MobileParty;
                if (mp != null)
                {
                    if (mp.IsLordParty) return mp.Army != null && side.Parties.Count > 1 ? "armia" : "lord";
                    if (mp.IsCaravan) return "karawana";
                    if (mp.IsVillager) return "wies";
                    if (mp.IsGarrison || mp.IsMilitia) return "zaloga";
                    if (mp.IsBandit) return "banda";
                    return "inna";
                }
                if (lp.Settlement != null) return lp.Settlement.IsVillage ? "wies" : "zaloga";
            }
            catch { }
            return "inna";
        }

        private static string Pc(int x, int of) { return of > 0 ? (100.0 * x / of).ToString("0", CultureInfo.InvariantCulture) + "%" : "0%"; }

        private static void SideText(StringBuilder sb, string tag, IFaction f, string kind, SideSnap s, int loss)
        {
            var inv = CultureInfo.InvariantCulture;
            var k = f as Kingdom;
            sb.Append(" | ").Append(tag).Append(": krol=").Append(k != null ? k.StringId : "-(" + (f != null ? f.StringId : "?") + ")")
              .Append(" rodzaj=").Append(kind).Append(" partii ").Append(s.Parties).Append(" ludzi ").Append(s.Men)
              .Append(" moc ").Append(s.Power.ToString("0.0", inv)).Append(" konni ").Append(Pc(s.Mounted, s.Men)).Append(" KL ").Append(Pc(s.HorseArchers, s.Men))
              .Append(" PolnocT3+ ").Append(Pc(s.North, s.Men)).Append(" straty ").Append(loss);
        }

        /// <summary>MapEventEnded (z BattleChronicle - przed jej wyjsciem przy malych bitwach).</summary>
        internal static void OnBattle(MapEvent me)
        {
            if (!On || me == null) return;
            try
            {
                var sa = me.AttackerSide; var sd = me.DefenderSide;
                if (sa == null || sd == null || !Interesting(me)) return;
                var win = me.WinningSide;
                Snap snap;
                if (_snaps.TryGetValue(me, out snap)) _snaps.Remove(me); else snap = Compute(me);
                int la = snap.A.Loss, ld = snap.D.Loss;
                if (la + ld == 0 && win != BattleSideEnum.Attacker && win != BattleSideEnum.Defender) return;   // bez walki i bez wyniku
                var fa = sa.MapFaction; var fd = sd.MapFaction;
                string ka = Kind(sa), kd = Kind(sd);
                bool kk = fa is Kingdom && fd is Kingdom;
                double pa = snap.A.Power, pd = snap.D.Power;
                double ratio = pa > 0 && pd > 0 ? Math.Max(pa, pd) / Math.Min(pa, pd) : 0;
                bool close = ratio > 0 && ratio < 1.5;
                bool lordFight = me.IsFieldBattle && !me.IsNavalMapEvent && kk && fa != fd && (ka == "lord" || ka == "armia") && (kd == "lord" || kd == "armia")
                                 && (win == BattleSideEnum.Attacker || win == BattleSideEnum.Defender) && pa > 0 && pd > 0;
                long hA, hD;
                bool edge = NorthHomeEdge.Read(me, out hA, out hD);

                // --- liczniki krolestw (narastajaco od startu sesji)
                RowOf(fa).B175++;
                if (HorseCensus.KeyOf(fd) != HorseCensus.KeyOf(fa)) RowOf(fd).B175++;
                if (lordFight)
                {
                    var ra = RowOf(fa); var rd = RowOf(fd);
                    double pA = 1.0 / (1.0 + Math.Exp(-K * Math.Log(pa / pd)));
                    bool aWon = win == BattleSideEnum.Attacker;
                    ra.Lord++; rd.Lord++;
                    if (aWon) ra.Won++; else rd.Won++;
                    ra.Wpo += (aWon ? 1.0 : 0.0) - pA;
                    rd.Wpo += (aWon ? 0.0 : 1.0) - (1.0 - pA);
                    if (close) { ra.Close++; rd.Close++; if (aWon) ra.CloseWon++; else rd.CloseWon++; }
                    _lordDay++;
                }
                if (me.IsSiegeAssault && kk && fa != fd)
                {
                    if (win == BattleSideEnum.Attacker) { RowOf(fa).StormWon++; RowOf(fd).StormLost++; }
                    else if (win == BattleSideEnum.Defender) RowOf(fd).StormHeld++;
                }
                if (edge)
                {
                    // strona z przewaga = ta, na ktorej korzysc szly ciosy z mnoznikiem (jednostki Polnocy sa po jednej stronie)
                    bool aSide = hA >= hD;
                    var rr = RowOf(aSide ? fa : fd);
                    rr.EdgeBattles++; rr.EdgeOwn += aSide ? la : ld; rr.EdgeFoe += aSide ? ld : la;
                    _edgeDay++;
                }
                // wsie i karawany rozbite przez Dothrakow (D6 - zalew Wolnych Miast)
                var kfa = fa as Kingdom;
                if (kfa != null && kfa.StringId == "khuzait" && win == BattleSideEnum.Attacker && (kd == "wies" || kd == "karawana") && fd != fa)
                    RowOf(fd).Raided++;

                // --- linia bitwy
                var inv = CultureInfo.InvariantCulture;
                var sb = new StringBuilder(400);
                sb.Append("Bitwa: B175 dzien ").Append((int)CampaignTime.Now.ToDays).Append(me.IsPlayerMapEvent ? " [GRACZ]" : "")
                  .Append(" | ").Append(me.EventType).Append(me.IsNavalMapEvent ? " morze" : "")
                  .Append(" | kontekst ").Append(me.SimulationContext);
                string region = "? (?)";
                try
                {
                    var r = me.MapEventSettlement != null ? me.MapEventSettlement : OutlawLaw.RegionAt(me.Position.ToVec2());
                    if (r != null && r.IsVillage && r.Village != null && r.Village.Bound != null) r = r.Village.Bound;
                    if (r != null) region = r.StringId + " (" + (r.Culture != null ? r.Culture.StringId : "?") + ")";
                }
                catch { }
                sb.Append(" | region ").Append(region);
                SideText(sb, "A", fa, ka, snap.A, la);
                SideText(sb, "O", fd, kd, snap.D, ld);
                sb.Append(" | stosunek sil ").Append(ratio.ToString("0.00", inv)).Append(close ? " (wyrownana)" : ratio > 0 ? " (nierowna)" : " (brak mocy)")
                  .Append(" | wygrywa ").Append(win == BattleSideEnum.Attacker ? "A" : win == BattleSideEnum.Defender ? "O" : "brak")
                  .Append(" | lordowie ").Append(lordFight ? "tak" : "nie")
                  .Append(" | przewaga Polnocy: ").Append(edge ? (hA >= hD ? "A" : "O") + " tak (ciosow " + (hA + hD) + ")" : "nie");
                if (me.MapEventSettlement != null)
                    sb.Append(" | osada ").Append(me.MapEventSettlement.StringId)
                      .Append(me.IsSiegeAssault ? " (zdobyta " + (win == BattleSideEnum.Attacker ? "tak" : "nie") + ")" : "");
                sb.Append('.');
                Log.Info(sb.ToString());
                _lines++; _linesDay++;
            }
            catch (Exception e) { _stumbles++; if (_stumbles <= 3) Log.Error("KingdomBalance.OnBattle", e); }
        }

        // ------------------------------------------------------------ twierdze
        internal static void OnOwnerChanged(Settlement st, bool openToClaim, Hero newOwner, Hero oldOwner, Hero capturer,
                                            ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
        {
            if (!On || st == null) return;
            try
            {
                if (detail != ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.BySiege) return;
                if (!st.IsTown && !st.IsCastle) return;
                IFaction nk = newOwner != null && newOwner.Clan != null ? (IFaction)newOwner.Clan.Kingdom ?? newOwner.Clan : st.MapFaction;
                IFaction ok = oldOwner != null && oldOwner.Clan != null ? (IFaction)oldOwner.Clan.Kingdom ?? oldOwner.Clan : null;
                RowOf(nk).FortTaken++;
                if (ok != null) RowOf(ok).FortLost++;
            }
            catch { _stumbles++; }
        }

        private static void CountForts(bool start)
        {
            foreach (var r in _rows.Values) { r.Forts = 0; r.GarrMen = 0; r.GarrRegs = 0; r.GarrTiers = 0; r.PartyMen = 0; r.PartyRegs = 0; r.PartyTiers = 0; }
            var towns = new List<Town>();
            try { towns.AddRange(Town.AllTowns); } catch { }
            try { towns.AddRange(Town.AllCastles); } catch { }
            foreach (var t in towns)
            {
                var st = t != null ? t.Settlement : null;
                if (st == null) continue;
                var r = RowOf(st.MapFaction);
                r.Forts++;
                var g = t.GarrisonParty;
                if (g == null || g.MemberRoster == null) continue;
                var ro = g.MemberRoster;
                for (int i = 0; i < ro.Count; i++)
                {
                    var el = ro.GetElementCopyAtIndex(i);
                    if (el.Character == null || el.Number <= 0) continue;
                    r.GarrMen += el.Number;
                    if (el.Character.IsHero) continue;
                    r.GarrRegs += el.Number; r.GarrTiers += (double)el.Character.Tier * el.Number;
                }
            }
            if (start) foreach (var r in _rows.Values) if (r.FortStart < 0) r.FortStart = r.Forts;
        }

        internal static void SessionStart()
        {
            if (!On) return;
            try
            {
                try { foreach (var k in Kingdom.All) if (k != null && !k.IsEliminated) RowOf(k); } catch { }
                CountForts(true);
                int n = 0; foreach (var r in _rows.Values) if (r.FortStart > 0) n++;
                // mapa id -> nazwa dla tools/p175_balans.py (budzet-rodow.csv i linie H3 pisza nazwy krolestw, B175 - id)
                var names = new List<string>();
                try { foreach (var k in Kingdom.All) if (k != null && k.StringId != null) names.Add(k.StringId + "=" + (k.Name != null ? k.Name.ToString().Replace(";", ",").Replace("|", "/") : "?")); } catch { }
                Log.Info("KingdomBalance (175.0): krolestwa id=nazwa: " + string.Join("; ", names.ToArray()));
                Log.Info("KingdomBalance (175.0): miara balansu krolestw czynna - " + n + " krolestw z twierdzami na starcie sesji, migawka skladu stron "
                         + (_patched ? "wpieta (przed H3)" : "BRAK - sklad liczony przy MapEventEnded") + ", k = 12.6; zbior Polnocy " + NorthHomeEdge.SetCount + ".");
            }
            catch (Exception e) { Log.Error("KingdomBalance.SessionStart", e); }
        }

        internal static void Daily()
        {
            if (!On) { _linesDay = _lordDay = _edgeDay = 0; return; }
            var inv = CultureInfo.InvariantCulture;
            int day = (int)CampaignTime.Now.ToDays;
            try
            {
                try { foreach (var k in Kingdom.All) if (k != null && !k.IsEliminated) RowOf(k); } catch { }
                CountForts(false);
                long world = 0;
                foreach (var mp in MobileParty.All)
                {
                    if (mp == null || !mp.IsActive || mp.IsMainParty || !mp.IsLordParty || mp.MemberRoster == null) continue;
                    long men = mp.MemberRoster.TotalManCount;
                    var pr = RowOf(mp.MapFaction);
                    pr.PartyMen += men; world += men;
                    var ro = mp.MemberRoster;
                    for (int i = 0; i < ro.Count; i++)
                    {
                        var el = ro.GetElementCopyAtIndex(i);
                        if (el.Character == null || el.Character.IsHero || el.Number <= 0) continue;
                        pr.PartyRegs += el.Number; pr.PartyTiers += (double)el.Character.Tier * el.Number;   // sredni tier partii lordow (D4)
                    }
                }
                foreach (var r in _rows.Values) if (r.FortStart < 0) r.FortStart = r.Forts;   // krolestwo powstale w trakcie sesji

                var keys = new List<string>(_rows.Keys);
                keys.Sort((a, b) => _rows[b].PartyMen.CompareTo(_rows[a].PartyMen));
                // szczegoly wedlug krolestw - do pliku tematycznego balans.log (Log.TopicOf); w glownym logu krotka linia nizej
                var sb = new StringBuilder(4000);
                sb.Append("Balans krolestw (175) wedlug krolestw: dzien ").Append(day).Append(" (narastajaco od startu sesji)");
                string dom = null;
                int lordAll = 0, closeAll = 0, stormAll = 0, fortAll = 0;
                long fcMen = 0; int fcRaided = 0;
                foreach (var key in keys)
                {
                    var r = _rows[key];
                    if (key == HorseCensus.NoKingdom && r.Lord == 0 && r.B175 == 0) continue;
                    double tier = r.GarrRegs > 0 ? r.GarrTiers / r.GarrRegs : 0;
                    sb.Append(" | ").Append(key).Append(": bitwy lordow ").Append(r.Won).Append('/').Append(r.Lord - r.Won)
                      .Append(" (wyrownane ").Append(r.CloseWon).Append('/').Append(r.Close - r.CloseWon).Append("), WPO ").Append(r.Wpo.ToString("+0.0;-0.0;0.0", inv))
                      .Append(", szturmy ").Append(r.StormWon).Append('/').Append(r.StormHeld).Append('/').Append(r.StormLost)
                      .Append(", twierdze ").Append(r.Forts).Append(" (start ").Append(r.FortStart).Append(", +").Append(r.FortTaken).Append(" -").Append(r.FortLost).Append(')')
                      .Append(", ludzie w partiach ").Append(r.PartyMen).Append(" (").Append(world > 0 ? (100.0 * r.PartyMen / world).ToString("0.0", inv) : "0").Append("%)")
                      .Append(", zalogi ").Append(r.GarrMen).Append(" (tier ").Append(tier.ToString("0.0", inv)).Append(')');
                    if (r.EdgeBattles > 0) sb.Append(", przewaga Polnocy w ").Append(r.EdgeBattles).Append(" bitwach (straty ").Append(r.EdgeOwn).Append('/').Append(r.EdgeFoe).Append(')');
                    if (r.Raided > 0) sb.Append(", rozbite przez Dothrakow ").Append(r.Raided);
                    if (Array.IndexOf(FreeCities, key) >= 0) { fcMen += r.PartyMen; fcRaided += r.Raided; }
                    lordAll += r.Lord; closeAll += r.Close; stormAll += r.StormWon; fortAll += r.FortTaken;
                    if (key != HorseCensus.NoKingdom)
                    {
                        // sygnal (nie hamulec): wygrane > 85% z co najmniej 15 bitew lordow albo twierdze netto >= +3 (prog B4 z projektu 6.2 liczony wobec biegu bazowego)
                        string why = null;
                        if (r.Lord >= 15 && r.Won > 0.85 * r.Lord) why = "wygrane " + (100.0 * r.Won / r.Lord).ToString("0", inv) + "% z " + r.Lord + " bitew lordow";
                        else if (r.FortTaken - r.FortLost >= 3) why = "twierdze netto +" + (r.FortTaken - r.FortLost);
                        if (why != null) dom = (dom == null ? "" : dom + "; ") + key + " (" + why + ")";
                    }
                }
                sb.Append('.');
                Log.Info(sb.ToString());

                // krotka linia dnia (glowny log): dzis + narastajaco, Polnoc i Dothrakowie, Wolne Miasta, sygnal dominacji
                Func<string, string> Brief = key =>
                {
                    KRow r;
                    if (!_rows.TryGetValue(key, out r)) return key + " -";
                    return key + " bitwy lordow " + r.Won + "/" + (r.Lord - r.Won) + " (wyrownane " + r.CloseWon + "/" + (r.Close - r.CloseWon) + "), WPO "
                           + r.Wpo.ToString("+0.0;-0.0;0.0", inv) + ", twierdze " + r.Forts + " (start " + r.FortStart + "), ludzie w partiach " + r.PartyMen
                           + (world > 0 ? " (" + (100.0 * r.PartyMen / world).ToString("0.0", inv) + "%)" : "");
                };
                long edgeOwn = 0, edgeFoe = 0; int edgeB = 0;
                foreach (var r in _rows.Values) { edgeB += r.EdgeBattles; edgeOwn += r.EdgeOwn; edgeFoe += r.EdgeFoe; }
                Log.Info("Balans krolestw (175): dzien " + day + " | dzis: linii B175 " + _linesDay + ", bitew lordow " + _lordDay + ", z przewaga Polnocy " + _edgeDay
                         + " | narastajaco: bitew lordow " + (lordAll / 2) + " (wyrownanych " + (closeAll / 2) + "), szturmow zdobytych " + stormAll + ", twierdz zdobytych oblezeniem " + fortAll
                         + " | " + Brief("battania") + " | " + Brief("khuzait")
                         + " | przewaga Polnocy (suwak " + NorthHomeEdge.Percent + "%): bitwy " + edgeB + ", straty " + edgeOwn + "/" + edgeFoe + ", ciosy " + NorthHomeEdge.SessionHits()
                         + " | Wolne Miasta (7): ludzie w partiach " + fcMen + ", wsie i karawany rozbite przez Dothrakow " + fcRaided
                         + " | dominacja: " + (dom ?? "brak") + " | szczegoly: balans.log, balans-krolestw.csv | potkniecia " + (_stumbles + NorthHomeEdge.Stumbles) + ".");

                var rows = new StringBuilder();
                var ks = new List<string>(_rows.Keys); ks.Sort(StringComparer.Ordinal);
                foreach (var key in ks)
                {
                    var r = _rows[key];
                    double tier = r.GarrRegs > 0 ? r.GarrTiers / r.GarrRegs : 0;
                    rows.Append(day).Append(';').Append(key).Append(';').Append(r.Lord).Append(';').Append(r.Won).Append(';').Append(r.Close).Append(';').Append(r.CloseWon)
                        .Append(';').Append(r.Wpo.ToString("0.000", inv)).Append(';').Append(r.StormWon).Append(';').Append(r.StormHeld).Append(';').Append(r.Forts)
                        .Append(';').Append(r.FortStart).Append(';').Append(r.FortTaken).Append(';').Append(r.FortLost).Append(';').Append(tier.ToString("0.00", inv))
                        .Append(';').Append(r.PartyMen).Append(';').Append(r.GarrMen).Append(';').Append(r.EdgeBattles).Append(';').Append(r.EdgeOwn).Append(';').Append(r.EdgeFoe)
                        .Append(';').Append(r.Raided).Append(';').Append(r.StormLost).Append(';').Append(world > 0 ? (100.0 * r.PartyMen / world).ToString("0.00", inv) : "0")
                        .Append(';').Append(r.B175).Append(';').Append((r.PartyRegs > 0 ? r.PartyTiers / r.PartyRegs : 0).ToString("0.00", inv)).Append(Environment.NewLine);
                }
                Log.Csv("balans-krolestw.csv", Header, rows.ToString());
            }
            catch (Exception e) { Log.Error("KingdomBalance.Daily", e); }
            _linesDay = _lordDay = _edgeDay = 0;
        }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var m = AccessTools.Method(typeof(MapEvent), "CalculateAndCommitMapEventResults");
                if (m != null && !m.IsStatic && m.GetParameters().Length == 0)
                {
                    // 801 = przed H3 (LosersFlee.ResultsPrefix, Priority.First = 800), ktora przesuwa ludzi miedzy rosterami
                    h.Patch(m, prefix: new HarmonyMethod(typeof(KingdomBalance), nameof(ResultsPrefix)) { priority = Priority.First + 1 });
                    _patched = true;
                }
                Log.Info("KingdomBalance (175.0): migawka skladu stron " + (_patched ? "wpieta (CalculateAndCommitMapEventResults, przed H3)" : "BRAK metody - sklad przy MapEventEnded") + ".");
            }
            catch (Exception e) { Log.Error("KingdomBalance.ApplyAll", e); }
        }
    }
}
