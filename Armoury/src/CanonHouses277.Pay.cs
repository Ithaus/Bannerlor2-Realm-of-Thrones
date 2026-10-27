using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;

namespace Armoury
{
    /// <summary>
    /// 277 - ZOLD OD PANA LENNEGO (klucz CanonHouses277LiegePay; Jeff "ksiazce": Tywin wezwal choragwie Zachodu i placil zlotem Lannisterow). Rody panow 277 nie maja
    /// lenna, wiec korona ich nie wspiera (W5 KingdomTreasury.NeedAid - tylko rody z wierszem rent), a ich wlasny pulap zoldu 166 to 15-495 zl na dobe.
    /// Regula - TA SAMA co W5 (liczby z istniejacego modelu, bez nowych stalych), tylko platnikiem jest pan lenny z pliku (Lannister dla Zachodu, Joffrey dla ziem
    /// koronnych), nie skarbiec:
    ///  - co dobe, krolestwo w wojnie (KingdomTreasury.AtWar), rod w budzecie 166 w wojnie (ClanBudget.PartyCapOwn - wczorajszy pulap wlasny druzyn, jak W5);
    ///  - druzyny rodu: limit L, ludzie M, zold W, zold na czlowieka P (W/M przy co najmniej 20 ludziach, inaczej srednia gry - jak W5); cel f = CrownWarAidFillTarget
    ///    (0.60), krok werbunku = CrownWarAidRecruitStep (0.10 limitu na dobe);
    ///  - GOTOWKA = zold ludzi, ktorzy SA (najwyzej do f x L), ponad pulap wlasny rodu: glowa rodu pana -> glowa rodu (GiveGoldAction w parze, nic z niczego);
    ///  - MIEJSCE = gotowka + krok werbunku (najwyzej do f x L) po P - podnosi pulap partii i rodu w ClanBudget.Daily tej doby jak W5 (b.Aid; nie wchodzi do D, nie
    ///    otwiera drugiej druzyny - SpawnPrefix liczy pulap wlasny), a o tyle samo OBNIZA pulap partii pana (ClanBudget: CommitOf) - pan oddaje miejsce w swoim zoldzie;
    ///  - granice pana: wolne miejsce w jego pulapie (wczorajszy pulap wlasny + wczorajsze oddane miejsce - dzisiejszy zold jego druzyn - oddane dzis innym rodom)
    ///    i kiesa glowy pana ponad rezerwe R 166 (ClanBudget.Reserve). Pan bez miejsca albo zlota - rod dostaje mniej (druzyna zostaje mala), liczniki w linii.
    /// ODCHYLENIA OD PROJEKTU (raport 277): (1) przelew NIE idzie do D rodu (ClanIncomeBook.NoteInflow KContract) - miejsce w pulapie daje b.Aid tej samej doby,
    /// a wpis do D liczylby ta sama zlotowke drugi raz (0.6 D w wojnie); (2) bez SoldierPay.AddKnightPaid - zold druzyn rodu placi sam rod (finanse gry) i ten zold jest
    /// juz w podstawie zwrotu 50% korony; dopisanie przelewu pana liczyloby go drugi raz. Pan w niewoli (Tywin u gracza) - przelew z jego kiesy dziala, licznik w linii.
    /// </summary>
    internal static partial class CanonHouses277
    {
        private sealed class Grant { public double Boost, Cash; }
        private static int _payDay = -1;
        private static readonly Dictionary<Clan, Grant> _grant = new Dictionary<Clan, Grant>();        // rod -> miejsce i gotowka tej doby
        private static readonly Dictionary<Clan, double> _commit = new Dictionary<Clan, double>();     // pan -> miejsce oddane dzis
        private static readonly Dictionary<Clan, double> _cutYday = new Dictionary<Clan, double>();    // pan -> miejsce zdjete z jego pulapu wczoraj (ClanBudget)
        private static readonly Dictionary<Clan, double> _cutToday = new Dictionary<Clan, double>();
        private static readonly Dictionary<string, long[]> _byLiege = new Dictionary<string, long[]>(StringComparer.Ordinal);   // nazwa pana -> [gotowka, miejsce]
        private static long _dPaid, _dGot, _dRoom, _dNeed;
        private static int _dNoRoom, _dNoGold, _dNoBudget, _dNoLiege, _dLiegePrisoner, _dPaidN;
        private static bool _dWar;

        private static void PayReset()
        {
            _payDay = -1; _grant.Clear(); _commit.Clear(); _cutYday.Clear(); _cutToday.Clear(); _byLiege.Clear();
            _dPaid = _dGot = _dRoom = _dNeed = 0; _dNoRoom = _dNoGold = _dNoBudget = _dNoLiege = _dLiegePrisoner = _dPaidN = 0; _dWar = false;
        }

        /// <summary>Nowa doba: przydzialy wczorajsze znikaja (ClanBudget.Daily wczoraj juz je zuzyl), zdjete wczoraj miejsce pana zostaje do rachunku wolnego miejsca.</summary>
        private static void PayRoll()
        {
            int today = (int)CampaignTime.Now.ToDays;
            if (_payDay == today) return;
            _payDay = today;
            _grant.Clear(); _commit.Clear(); _byLiege.Clear();
            _cutYday.Clear(); foreach (var kv in _cutToday) _cutYday[kv.Key] = kv.Value; _cutToday.Clear();
            _dPaid = _dGot = _dRoom = _dNeed = 0; _dNoRoom = _dNoGold = _dNoBudget = _dNoLiege = _dLiegePrisoner = _dPaidN = 0; _dWar = false;
        }

        // ---- odczyt dla ClanBudget.Daily (ta sama doba; inna doba - 0)
        internal static double BoostOf(Clan c) { Grant g; return c != null && _payDay == (int)CampaignTime.Now.ToDays && _grant.TryGetValue(c, out g) ? g.Boost : 0; }
        internal static double CashOf(Clan c) { Grant g; return c != null && _payDay == (int)CampaignTime.Now.ToDays && _grant.TryGetValue(c, out g) ? g.Cash : 0; }
        internal static double CommitOf(Clan c) { double v; return c != null && _payDay == (int)CampaignTime.Now.ToDays && _commit.TryGetValue(c, out v) ? v : 0; }
        /// <summary>ClanBudget.Daily: ile miejsca naprawde zdjeto z pulapu pana (jutro wraca do rachunku jego wolnego miejsca).</summary>
        internal static void NoteCut(Clan c, double cut) { if (c != null && cut > 0) _cutToday[c] = cut; }

        /// <summary>Czysty rachunek (test T2) - regula W5 przy poziomie f: gotowka = zold ludzi, ktorzy sa (do f x L) ponad pulap wlasny; miejsce = gotowka + krok
        /// werbunku (najwyzej do f x L) po stawce P.</summary>
        internal static void PayPlan(double L, double M, double W, double P, double pc0, double f, double step, out double cash, out double room)
        {
            cash = 0; room = 0;
            double cap = f * L;
            if (!(cap > 0)) return;
            double funded = M > cap && M > 0 ? W * cap / M : W;
            cash = Math.Max(0, funded - pc0);
            double recruit = Math.Max(0, Math.Min(step * L, cap - M)) * Math.Max(0, P);
            room = cash + recruit;
        }

        private static void Parties(Clan c, out int limit, out int men, out long wage)
        {
            limit = 0; men = 0; wage = 0;
            var wps = c.WarPartyComponents;
            if (wps == null) return;
            for (int i = 0; i < wps.Count; i++)
            {
                var mp = wps[i] != null ? wps[i].MobileParty : null;
                if (mp == null || !mp.IsActive || !mp.IsLordParty || mp.Party == null) continue;
                limit += Math.Max(0, mp.Party.PartySizeLimit);
                men += mp.MemberRoster != null ? mp.MemberRoster.TotalRegulars : 0;
                wage += Math.Max(0, mp.TotalWage) + GentryService.KnightWage(mp);
            }
        }

        private static void Pay(Kingdom k, Settings s, int today)
        {
            if (!s.CanonHouses277LiegePay || !KingdomTreasury.AtWar(k)) return;
            _dWar = true;
            double f = Math.Max(0, Math.Min(1, s.CrownWarAidFillTarget)), step = Math.Max(0, Math.Min(1, s.CrownWarAidRecruitStep));
            float avg = Math.Max(1f, Campaign.Current.AverageWage);
            var free = new Dictionary<Clan, double>(); var purse = new Dictionary<Clan, double>();
            var houses = new List<Clan>(_lords.Keys);
            houses.Sort((a, b) => string.CompareOrdinal(a.StringId, b.StringId));
            foreach (var c in houses)
            {
                try
                {
                    var r = _lords[c];
                    if (c.IsEliminated || c.Kingdom != k || c.Leader == null || !c.Leader.IsAlive) continue;
                    double pc0;
                    if (!ClanBudget.PartyCapOwn(c, out pc0)) { _dNoBudget++; continue; }
                    int L, M; long W;
                    Parties(c, out L, out M, out W);
                    if (L <= 0) continue;
                    double P = M >= 20 && W > 0 ? (double)W / M : avg * CrownGifts.GoldWageShare(c);
                    double cash, room;
                    PayPlan(L, M, W, P, pc0, f, step, out cash, out room);
                    if (!(room > 0)) continue;
                    _dNeed += (long)room;
                    Clan lc = null;
                    foreach (var x in Clan.All) if (x != null && x.StringId == r.Liege) { lc = x; break; }
                    var lh = lc != null ? lc.Leader : null;
                    if (lc == null || lc.IsEliminated || lc.Kingdom != k || lc == Clan.PlayerClan || lh == null || !lh.IsAlive) { _dNoLiege++; continue; }
                    double fr;
                    if (!free.TryGetValue(lc, out fr))
                    {
                        double lcap; int lL, lM; long lW;
                        if (!ClanBudget.PartyCapOwn(lc, out lcap)) { _dNoBudget++; continue; }
                        Parties(lc, out lL, out lM, out lW);
                        double back; _cutYday.TryGetValue(lc, out back);
                        fr = Math.Max(0, lcap + back - lW);
                        free[lc] = fr;
                    }
                    double pu;
                    if (!purse.TryGetValue(lc, out pu))
                    {
                        double g, rr;
                        pu = ClanBudget.Reserve(lc, out g, out rr) ? Math.Max(0, lh.Gold - rr) : 0;
                        purse[lc] = pu;
                    }
                    double give = Math.Min(room, fr);
                    if (give < room) _dNoRoom++;
                    double pay = Math.Min(Math.Min(cash, give), pu);
                    if (pay < Math.Min(cash, give)) _dNoGold++;
                    int x2 = (int)Math.Floor(pay);
                    int got = 0;
                    if (x2 > 0)
                    {
                        var h = c.Leader;
                        int before = h.Gold, lb = lh.Gold;
                        GiveGoldAction.ApplyBetweenCharacters(lh, h, x2, true);
                        got = Math.Max(0, h.Gold - before);
                        _dPaid += Math.Max(0, lb - lh.Gold); _dGot += got;
                        if (got > 0) _dPaidN++;
                        if (lh.IsPrisoner) _dLiegePrisoner++;
                    }
                    if (!(give > 0)) continue;
                    free[lc] = fr - give; purse[lc] = pu - got;
                    _grant[c] = new Grant { Boost = give, Cash = got };
                    double cm; _commit.TryGetValue(lc, out cm); _commit[lc] = cm + give;
                    _dRoom += (long)give;
                    string ln = Nm(lc);
                    long[] a; if (!_byLiege.TryGetValue(ln, out a)) _byLiege[ln] = a = new long[2];
                    a[0] += got; a[1] += (long)give;
                }
                catch (Exception e) { Stumble("Pay(rod)", e); }
            }
        }

        private static string PayText()
        {
            var s = Settings.Current;
            if (s == null || !s.CanonHouses277LiegePay) return "od pana lennego: WYLACZONE";
            if (!_dWar) return "od pana lennego: pokoj - bez zoldu";
            var sb = new StringBuilder(220);
            sb.Append("od pana lennego: zaplacone ").Append(_dPaid).Append(", otrzymane ").Append(_dGot).Append(" (rodow ").Append(_dPaidN).Append("), miejsce ").Append(_dRoom)
              .Append(" z potrzeby ").Append(_dNeed).Append(" (pan bez miejsca ").Append(_dNoRoom).Append(", pan bez zlota ").Append(_dNoGold).Append(", bez pana ").Append(_dNoLiege)
              .Append(", bez budzetu 166 ").Append(_dNoBudget).Append(", pan w niewoli ").Append(_dLiegePrisoner).Append(") [");
            bool first = true;
            foreach (var kv in _byLiege) { if (!first) sb.Append(", "); first = false; sb.Append(kv.Key).Append(' ').Append(kv.Value[0]).Append('/').Append(kv.Value[1]); }
            sb.Append(']');
            return sb.ToString();
        }
    }
}
