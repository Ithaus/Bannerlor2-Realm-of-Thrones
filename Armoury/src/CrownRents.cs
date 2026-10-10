using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// PACZKA 180 - RENTY KORONY WEDLUG LENN (projekt etapu 2, krok C2; PLAN 2.3; D-4, Q3a, [D] 03:45 nr 10, K10, K41). Wylacznik CrownRents, czynny z 165
    /// (renty to ostatni wydatek z wplywow dnia korony).
    ///  - Co zostalo z wplywow dnia (165: wplywy + 1/360 zapasu) po darach (182), ratach reparacji, kontraktach (185) i zwrocie zoldu, korona rozdaje TEGO SAMEGO
    ///    dnia rodom krolestwa wedlug STALYCH wag lenn (Z8 - nigdy wedlug zalogi): miasto CrownRentWeightTown (3), zamek CrownRentWeightCastle (1), wies
    ///    CrownRentWeightVillage (0.25). Rod krola tez (skarbiec to nie kiesa krola), gracz na tych samych warunkach (2.0b). Bez najemnikow (185), dworzan BK i Innych.
    ///  - WARUNEK SLUZBY (M4, K10), oba naraz:
    ///    1. zaloga: kazda twierdza rodu ma co najmniej CrownRentGarrisonShare (50%) STALEJ NORMY - sredniej zalogi twierdz tego rodzaju (miasto / zamek) w krolestwie
    ///       z ostatnich 28 dob (CrownRentGarrisonNormKingdom; wylaczone - norma swiata); nie wlasnej sredniej rodu. Twierdza w oblezeniu i twierdza w rekach rodu
    ///       krocej niz 28 dob (swiezo zdobyta albo nadana) - pomijane (zaloga dopiero sie zbiera; odstepstwo w C1-POSTEP).
    ///    2. sluzba: w ostatnich 60 dobach wojny krolestwa (w wojnie - biezaca, w pokoju - ostatnia) partia glowy albo czlonka rodu byla co najmniej
    ///       CrownRentServiceDays (20) dob w armii krolestwa, przy oblezeniu (oblegajac albo broniac), w bitwie z wrogiem albo na ziemi wroga (najblizsza osada
    ///       nalezy do krolestwa, z ktorym jest wojna); niewola u wroga liczy sie jako sluzba. Karawana i partia we wlasnych osadach - nie. Rod, ktory widzial
    ///       mniej niz 60 dob wojny tej korony (nowa kampania, nowy wasal, stary zapis), potrzebuje tej samej czesci dob, ktore widzial (20/60); bez zadnej doby
    ///       wojny - warunek sluzby nie obowiazuje (nie bylo wojny, w ktorej mozna sluzyc).
    ///    Udzial rodu bez warunku zostaje w skarbcu (zapas); reszta z zaokraglen tez.
    ///  - D: renta w czesci "korona" D stalego (ClanIncomeBook.KRent) - budzet 166 widzi ja od nastepnej doby; poza "jednorazowymi" (nie liczona dwa razy).
    /// Kolejnosc (2.0b): po zwrocie zoldu (KingdomTreasury.WageRefund), przed CrownIncome.End; kazde krolestwo i kazdy rod we wlasnym try.
    /// Platnik -> odbiorca: skarbiec (reszta wplywow dnia) -> glowa rodu (GiveGold w parze: skarbiec - x, glowa + x). Zapis: SaveText "arm_rent180"
    /// (sluzba rodow, normy zalog, doby przejecia twierdz).
    /// </summary>
    internal static class CrownRents
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const int Window = 60;          // doby wojny krolestwa, z ktorych liczy sie sluzba (projekt: "z ostatnich 60 dob")
        private const int NormDays = 28;        // stala norma zalogi: srednia z 28 dob
        private const int GraceDays = 28;       // twierdza w rekach rodu krocej - pomijana w warunku zalogi
        private const long MaskAll = (1L << Window) - 1;

        internal static bool On { get { var s = Settings.Current; return s != null && s.CrownRents && CrownIncome.On; } }

        // sluzba rodu: maska ostatnich 60 dob wojny jego krolestwa (bit 0 = ostatnia doba wojny), ile dob wojny widzial (najwyzej 60)
        private sealed class Svc { public string Kingdom; public long Mask; public int Seen; }
        private static readonly Dictionary<string, Svc> _svc = new Dictionary<string, Svc>();        // id rodu -> sluzba

        // stala norma zalogi: srednia zalogi twierdz rodzaju w krolestwie (klucz "idKrolestwa/T" albo "/C"; swiat "*/T", "*/C") z 28 dob
        private sealed class Norm
        {
            public readonly double[] V = new double[NormDays]; public int Head, Filled;
            public void Push(double v) { V[Head] = v; Head = (Head + 1) % NormDays; if (Filled < NormDays) Filled++; }
            public double Avg() { if (Filled <= 0) return -1; double s = 0; for (int i = 0; i < NormDays; i++) s += V[i]; return s / Filled; }
        }
        private static readonly Dictionary<string, Norm> _norm = new Dictionary<string, Norm>();

        // doba przejecia twierdzy przez obecnego wlasciciela (tylko ostatnie GraceDays dob; brak wpisu = dawno)
        private static readonly Dictionary<string, int> _since = new Dictionary<string, int>();     // id osady -> doba zmiany wlasciciela
        private static readonly Dictionary<Settlement, Clan> _owner = new Dictionary<Settlement, Clan>();

        // wynik doby na rod (CSV ksiegi 169 i linia)
        internal struct Row { public long Paid, Held; public int Served, Need; public bool GarOk, SvcOk, Counted; }
        private static readonly Dictionary<Clan, Row> _rows = new Dictionary<Clan, Row>();

        // liczniki doby (linia, "Korona", "Obieg")
        internal static long LastPool, LastPaid, LastHeld, LastHeldFail, LastRound;
        internal static int LastClans, LastOk, LastFailGar, LastFailSvc, LastFailBoth, LastKingdoms, LastNoPool;
        private static int _stumbles, _importN = -1, _importBad;
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static string _playerNote;     // ostatni powod wstrzymania renty gracza (komunikat tylko przy zmianie)
        private static long _playerWeekSum; private static int _playerWeekStart = -1;   // renta gracza zbiorczo co 7 dob

        internal static void Reset()
        {
            ClearState(); _rows.Clear();
            ZeroLast(); _stumbles = 0; _err.Clear(); _importN = -1; _importBad = 0; _playerNote = null; _playerWeekSum = 0; _playerWeekStart = -1;
        }

        /// <summary>Stan sluzby, norm i przejec twierdz (wylaczone 180 = stan sprzed paczki: nic nie liczymy i nic nie zapisujemy).</summary>
        private static void ClearState()
        {
            _svc.Clear(); _norm.Clear(); _since.Clear(); _owner.Clear();
            _pts = new Settlement[0]; _px = new float[0]; _py = new float[0];
        }

        internal static void ZeroLast()
        {
            LastPool = LastPaid = LastHeld = LastHeldFail = LastRound = 0;
            LastClans = LastOk = LastFailGar = LastFailSvc = LastFailBoth = LastKingdoms = LastNoPool = 0;
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_err.Add(where)) Log.Error("CrownRents." + where, e); } catch { }
        }

        private static bool Eligible(Clan c)
        {
            return c != null && !c.IsEliminated && !c.IsBanditFaction && c.Kingdom != null && c.Leader != null && c.Leader.IsAlive && !c.IsUnderMercenaryService
                   && c.StringId != null && !c.StringId.StartsWith("bk_courtiers_", StringComparison.Ordinal) && !ClanIncomeBook.IsUndeadClan(c);
        }

        /// <summary>Waga lenn rodu: miasto 3, zamek 1, wies 0.25 (stale - Z8; wsie wedlug wlasnosci gry, jak dar 182).</summary>
        private static double Weight(Clan c, Settings s)
        {
            double w = 0;
            var sts = c.Settlements;
            if (sts == null) return 0;
            for (int i = 0; i < sts.Count; i++)
            {
                var st = sts[i];
                if (st == null) continue;
                if (st.IsTown) w += Math.Max(0f, s.CrownRentWeightTown);
                else if (st.IsCastle) w += Math.Max(0f, s.CrownRentWeightCastle);
                else if (st.IsVillage) w += Math.Max(0f, s.CrownRentWeightVillage);
            }
            return w;
        }

        private static int GarMen(Town f)
        {
            var gp = f != null ? f.GarrisonParty : null;
            return gp != null && gp.MemberRoster != null ? gp.MemberRoster.TotalRegulars : 0;
        }

        private static Norm NormOf(string key)
        {
            Norm n;
            if (!_norm.TryGetValue(key, out n)) { n = new Norm(); _norm[key] = n; }
            return n;
        }

        private static bool Enemy(Kingdom k, IFaction f)
        {
            return f != null && f != k && f.IsKingdomFaction && k.IsAtWarWith(f);
        }

        // ------------------------------------------------------------ pozycje osad (najblizsza osada partii) - raz na dobe
        private static Settlement[] _pts = new Settlement[0];
        private static float[] _px = new float[0], _py = new float[0];

        private static void BuildPoints()
        {
            var list = new List<Settlement>();
            foreach (var st in Settlement.All) if (st != null && (st.IsTown || st.IsCastle || st.IsVillage)) list.Add(st);
            _pts = list.ToArray(); _px = new float[_pts.Length]; _py = new float[_pts.Length];
            for (int i = 0; i < _pts.Length; i++) { var p = _pts[i].GetPosition2D; _px[i] = p.x; _py[i] = p.y; }
        }

        private static Settlement Nearest(MobileParty mp)
        {
            if (mp.CurrentSettlement != null) return mp.CurrentSettlement;
            var p = mp.GetPosition2D;
            float best = float.MaxValue; int bi = -1;
            for (int i = 0; i < _pts.Length; i++)
            {
                float dx = _px[i] - p.x, dy = _py[i] - p.y, d = dx * dx + dy * dy;
                if (d < best) { best = d; bi = i; }
            }
            return bi >= 0 ? _pts[bi] : null;
        }

        // sluzba partii dzis: 1 armia krolestwa, 2 oblezenie (oblega albo broni oblezonej osady), 3 bitwa z wrogiem, 4 ziemia wroga; 0 - nie
        private static int Serving(MobileParty mp, Kingdom k)
        {
            try { return ServingBody(mp, k); }
            catch (Exception e) { Stumble("Serving", e); return 0; }   // recenzja C2: blad przy jednej partii nie zabiera rodowi doby wojny (maska przesuwa sie dalej)
        }

        private static int ServingBody(MobileParty mp, Kingdom k)
        {
            if (mp == null || !mp.IsActive || !(mp.IsLordParty || mp.IsMainParty)) return 0;
            if (mp.Army != null && mp.Army.Kingdom == k) return 1;
            if (mp.BesiegerCamp != null) return 2;
            if (mp.CurrentSettlement != null && mp.CurrentSettlement.IsUnderSiege) return 2;
            if (mp.MapEvent != null)
            {
                var side = mp.Party != null ? mp.Party.MapEventSide : null;
                var other = side != null ? side.OtherSide : null;
                if (other != null && Enemy(k, other.MapFaction)) return 3;
            }
            var st = Nearest(mp);
            if (st != null && Enemy(k, st.MapFaction)) return 4;
            return 0;
        }

        private static int Popcount(long v) { int n = 0; while (v != 0) { v &= v - 1; n++; } return n; }

        // ------------------------------------------------------------ raz na dobe (po zwrocie 165, przed CrownIncome.End)
        internal static void Daily()
        {
            ZeroLast(); _rows.Clear();
            var s = Settings.Current;
            if (s == null || Campaign.Current == null) return;
            if (!On) { if (_svc.Count > 0 || _norm.Count > 0 || _since.Count > 0 || _owner.Count > 0) ClearState(); return; }   // recenzja C2: wylacznik = stan sprzed paczki (bez starej sluzby po wlaczeniu)
            try
            {
                int today = (int)CampaignTime.Now.ToDays;
                int svcDays = Math.Max(0, Math.Min(Window, s.CrownRentServiceDays));
                float garShare = Math.Max(0f, Math.Min(1f, s.CrownRentGarrisonShare));
                // 1. normy zalog (krolestwo i swiat, wedlug rodzaju twierdzy) i doby przejecia twierdz
                double wT = 0, wC = 0; int nT = 0, nC = 0, sieged = 0;
                foreach (var k in Kingdom.All)
                {
                    try
                    {
                        if (k == null || k.IsEliminated || k.StringId == null) continue;
                        double sT = 0, sC = 0; int cT = 0, cC = 0;
                        var fiefs = k.Fiefs;
                        if (fiefs != null)
                            for (int i = 0; i < fiefs.Count; i++)
                            {
                                var f = fiefs[i];
                                if (f == null || f.Settlement == null || f.Settlement.IsUnderSiege) continue;   // oblezona - zaloga w walce, poza norma
                                int g = GarMen(f);
                                if (f.IsTown) { sT += g; cT++; } else if (f.IsCastle) { sC += g; cC++; }
                            }
                        if (cT > 0) NormOf(k.StringId + "/T").Push(sT / cT);
                        if (cC > 0) NormOf(k.StringId + "/C").Push(sC / cC);
                        wT += sT; nT += cT; wC += sC; nC += cC;
                    }
                    catch (Exception e) { Stumble("Daily(norma)", e); }
                }
                if (nT > 0) NormOf("*/T").Push(wT / nT);
                if (nC > 0) NormOf("*/C").Push(wC / nC);
                foreach (var st in Settlement.All)
                {
                    if (st == null || !(st.IsTown || st.IsCastle)) continue;
                    Clan o, now = st.OwnerClan;
                    if (!_owner.TryGetValue(st, out o)) { _owner[st] = now; continue; }        // pierwsze spojrzenie (start, wczytanie) - wlasciciel "od dawna" (albo doba z zapisu)
                    if (!ReferenceEquals(o, now)) { _owner[st] = now; if (st.StringId != null) _since[st.StringId] = today; }
                }
                var oldSince = new List<string>();
                foreach (var kv in _since) if (today - kv.Value >= GraceDays) oldSince.Add(kv.Key);
                foreach (var id in oldSince) _since.Remove(id);
                // 2. sluzba rodow w krolestwach w wojnie (doba wojny: maska przesuwa sie o 1)
                BuildPoints();
                var warK = new Dictionary<Kingdom, bool>();
                int warClans = 0, srvArmy = 0, srvSiege = 0, srvBattle = 0, srvLand = 0, srvCaptive = 0;
                var seen = new HashSet<string>();
                foreach (var c in Clan.All)
                {
                    try
                    {
                        if (!Eligible(c)) continue;
                        var k = c.Kingdom;
                        seen.Add(c.StringId);
                        Svc v;
                        if (!_svc.TryGetValue(c.StringId, out v) || v.Kingdom != k.StringId) { v = new Svc { Kingdom = k.StringId }; _svc[c.StringId] = v; }   // nowy wasal tej korony - od zera
                        bool war;
                        if (!warK.TryGetValue(k, out war)) { war = KingdomTreasury.AtWar(k); warK[k] = war; }
                        if (!war) continue;
                        warClans++;
                        int how = 0;
                        var wps = c.WarPartyComponents;
                        if (wps != null)
                            for (int i = 0; i < wps.Count && how == 0; i++) how = Serving(wps[i] != null ? wps[i].MobileParty : null, k);
                        if (how == 0)
                        {
                            var lords = c.AliveLords;
                            if (lords != null)
                                for (int i = 0; i < lords.Count && how == 0; i++)
                                {
                                    var h = lords[i];
                                    if (h == null || h.IsChild) continue;
                                    if (h.IsPrisoner) { var cap = h.PartyBelongedToAsPrisoner; if (cap != null && Enemy(k, cap.MapFaction)) how = 5; continue; }   // niewola na wojnie
                                    var mp = h.PartyBelongedTo;
                                    if (mp != null && mp.ActualClan != c) how = Serving(mp, k);   // czlonek rodu w partii innego rodu (np. w druzynie krewnego)
                                }
                        }
                        v.Mask = ((v.Mask << 1) | (how > 0 ? 1L : 0L)) & MaskAll;
                        if (v.Seen < Window) v.Seen++;
                        if (how == 1) srvArmy++; else if (how == 2) srvSiege++; else if (how == 3) srvBattle++; else if (how == 4) srvLand++; else if (how == 5) srvCaptive++;
                    }
                    catch (Exception e) { Stumble("Daily(sluzba)", e); }
                }
                var gone = new List<string>();
                foreach (var kv in _svc) if (!seen.Contains(kv.Key)) gone.Add(kv.Key);
                foreach (var id in gone) _svc.Remove(id);
                // 3. renty: reszta wplywow dnia -> glowy rodow wedlug wag; udzial rodu bez warunku zostaje w skarbcu
                var perShare = new List<double>(); var det = new List<KeyValuePair<long, string>>();
                long playerPaid = 0; string playerWhy = null; double playerW = 0; int playerServed = 0, playerNeed = 0; bool playerRow = false;
                foreach (var k in Kingdom.All)
                {
                    try
                    {
                        if (k == null || k.IsEliminated || k.StringId == null) continue;
                        var day = CrownIncome.DayOf(k);
                        if (day == null) continue;
                        long pool = CrownIncome.LeftFor(k);
                        var list = new List<Clan>(); var wts = new List<double>(); double sum = 0;
                        foreach (var c in k.Clans)
                        {
                            if (!Eligible(c)) continue;
                            double w = Weight(c, s);
                            if (w <= 0) continue;
                            list.Add(c); wts.Add(w); sum += w;
                        }
                        if (list.Count == 0 || sum <= 0) continue;               // nie ma komu dac - reszta zostaje w skarbcu (jak przed 180)
                        // recenzja C2: warunki liczone u wszystkich rodow z lennem, takze gdy krolestwo nie ma dzis reszty po zwrocie (zwykle w wojnie) - miara
                        // "bez warunku" nie zalezy od tego, czy korona ma z czego placic, a gracz widzi powod wstrzymania renty takze w takie doby
                        bool pay = pool > 0;
                        if (pay) LastKingdoms++; else LastNoPool += list.Count;
                        string normKey = s.CrownRentGarrisonNormKingdom ? k.StringId : "*";
                        double normT = -1, normC = -1; Norm nn;
                        if (_norm.TryGetValue(normKey + "/T", out nn)) normT = nn.Avg();
                        if (_norm.TryGetValue(normKey + "/C", out nn)) normC = nn.Avg();
                        long paidK = 0, heldK = 0;
                        for (int i = 0; i < list.Count; i++)
                        {
                            var c = list[i];
                            long share = pay ? (long)(pool * wts[i] / sum) : 0;
                            var row = new Row { Counted = true };
                            try
                            {
                                // warunek zalogi: kazda twierdza (poza oblezona i swiezo przejeta) >= 50% normy rodzaju
                                row.GarOk = true;
                                var fiefs = c.Fiefs;
                                if (fiefs != null)
                                    for (int j = 0; j < fiefs.Count; j++)
                                    {
                                        var f = fiefs[j];
                                        if (f == null || f.Settlement == null) continue;
                                        if (f.Settlement.IsUnderSiege) { sieged++; continue; }
                                        int since; if (f.Settlement.StringId != null && _since.TryGetValue(f.Settlement.StringId, out since) && today - since < GraceDays) continue;
                                        double norm = f.IsTown ? normT : f.IsCastle ? normC : -1;
                                        if (norm > 0 && GarMen(f) < garShare * norm) row.GarOk = false;
                                    }
                                // warunek sluzby: >= 20 z ostatnich 60 dob wojny; rod, ktory widzial mniej dob wojny - ta sama czesc dob, ktore widzial
                                Svc v;
                                if (_svc.TryGetValue(c.StringId, out v) && v.Kingdom == k.StringId && v.Seen > 0)
                                {
                                    row.Served = Popcount(v.Mask);
                                    row.Need = Math.Min(svcDays, (int)Math.Ceiling(svcDays * (double)v.Seen / Window - 1e-9));
                                }
                                row.SvcOk = row.Served >= row.Need;
                            }
                            catch (Exception e) { row.GarOk = false; row.SvcOk = false; Stumble("Daily(warunek)", e); }   // blad przy rodzie - udzial zostaje w skarbcu
                            int x = (int)Math.Min(int.MaxValue, Math.Max(0L, share));
                            bool ok = row.GarOk && row.SvcOk, paid = false;
                            if (ok && x > 0)
                            {
                                try
                                {
                                    var h = c.Leader;
                                    k.KingdomBudgetWallet -= x;                    // najpierw skarbiec, potem glowa - dostaje dokladnie tyle, ile zeszlo
                                    try { h.ChangeHeroGold(x); } catch { k.KingdomBudgetWallet += x; throw; }   // wyjatek - zloto wraca do skarbca (nic w nicosc)
                                    paid = true;
                                    row.Paid = x; paidK += x;
                                    if (c == Clan.PlayerClan) playerPaid += x;
                                    CirculationWindows.NoteHeroGold(h, x);         // paczka 169b: glowa poza swiatem - zloto wyszlo ze swiata (tylko licznik)
                                    ClanIncomeBook.NoteInflow(h, x, ClanIncomeBook.KRent);   // D rodu: czesc "korona" (renta)
                                }
                                catch (Exception e) { Stumble("Daily(wyplata)", e); }
                            }
                            if (!paid) { row.Held = x; heldK += x; }           // udzial rodu bez warunku (albo nieudana wyplata) zostaje w skarbcu
                            if (!row.GarOk && !row.SvcOk) LastFailBoth++; else if (!row.GarOk) LastFailGar++; else if (!row.SvcOk) LastFailSvc++;
                            if (ok) LastOk++;
                            LastClans++;
                            _rows[c] = row;
                            if (c == Clan.PlayerClan)
                            {
                                playerW = wts[i]; playerServed = row.Served; playerNeed = row.Need; playerRow = true;
                                playerWhy = !row.GarOk ? "garrison" : !row.SvcOk ? "service" : null;
                            }
                        }
                        if (!pay) continue;
                        CrownIncome.Spent(k, paidK);                           // renty schodza z reszty wplywow dnia; reszta (wstrzymane, zaokraglenia) zostaje w skarbcu
                        long round = pool - paidK - heldK;
                        day.Rent += paidK; day.RentHeld += pool - paidK;
                        LastPool += pool; LastPaid += paidK; LastHeldFail += heldK; LastRound += round; LastHeld += pool - paidK;
                        perShare.Add(pool / sum);
                        det.Add(new KeyValuePair<long, string>(pool, (k.Name != null ? k.Name.ToString() : k.StringId) + " " + paidK + "/" + pool + " (udzial " + (pool / sum).ToString("0", Inv) + ")"));
                    }
                    catch (Exception e) { Stumble("Daily(krolestwo)", e); }
                }
                // gracz: renta albo powod wstrzymania (komunikat tylko przy zmianie powodu)
                try
                {
                    // renta gracza zbiorczo raz na 7 dob, nie codziennie (Jeff: za duzo komunikatow); suma tygodnia tylko w pamieci - po wczytaniu liczy od nowa
                    _playerWeekSum += playerPaid;
                    int dayNow = (int)CampaignTime.Now.ToDays;
                    if (_playerWeekStart < 0 || dayNow < _playerWeekStart) _playerWeekStart = dayNow;
                    if (dayNow - _playerWeekStart >= 7)
                    {
                        if (_playerWeekSum > 0) Log.Player("The crown paid your house " + _playerWeekSum + " denars in rents for its fiefs this past week.");
                        _playerWeekSum = 0; _playerWeekStart = dayNow;
                    }
                    if (playerWhy != null && playerWhy != _playerNote)
                    {
                        Log.Player(playerWhy == "garrison"
                            ? "The crown withholds your house's rent: one of your strongholds is garrisoned below " + (garShare * 100f).ToString("0", Inv) + "% of the realm's usual garrison for its kind."
                            : "The crown withholds your house's rent: your lords served " + playerServed + " of the " + playerNeed + " war days the crown asks (in an army of the realm, at a siege, in battle with the enemy or on enemy land).", true);
                    }
                    if (playerRow) _playerNote = playerWhy;   // recenzja C2: doba bez wpisu gracza nie kasuje powodu (bez powtarzania komunikatu)
                }
                catch (Exception e) { Stumble("Daily(gracz)", e); }
                if (s.LogEnabled)
                {
                    perShare.Sort();
                    det.Sort((a, b) => b.Key.CompareTo(a.Key));
                    var txt = new List<string>(); for (int i = 0; i < det.Count && i < 12; i++) txt.Add(det[i].Value);
                    double nT28 = 0, nC28 = 0; int kT = 0, kC = 0;
                    foreach (var kv in _norm)
                    {
                        if (kv.Key.StartsWith("*/", StringComparison.Ordinal)) continue;
                        double a = kv.Value.Avg(); if (a < 0) continue;
                        if (kv.Key.EndsWith("/T", StringComparison.Ordinal)) { nT28 += a; kT++; } else { nC28 += a; kC++; }
                    }
                    Svc ps; Row pr = default(Row); bool pRow = Clan.PlayerClan != null && _rows.TryGetValue(Clan.PlayerClan, out pr);
                    string pSvc = Clan.PlayerClan != null && Clan.PlayerClan.StringId != null && _svc.TryGetValue(Clan.PlayerClan.StringId, out ps) ? Popcount(ps.Mask) + " z " + ps.Seen + " dob wojny" : "-";
                    var sb = new StringBuilder(1536);
                    sb.Append("Renty korony (180): dzien ").Append(today)
                      .Append(" | krolestwa z renta ").Append(LastKingdoms).Append(", reszta wplywow dnia po zwrocie ").Append(LastPool)
                      .Append(" = renty ").Append(LastPaid).Append(" + wstrzymane w skarbcach ").Append(LastHeld)
                      .Append(" (udzialy rodow bez warunku ").Append(LastHeldFail).Append(", zaokraglenia ").Append(LastRound).Append(')')
                      .Append(" | rody z lennem ").Append(LastClans).Append(" (w krolestwach bez reszty po zwrocie - renta 0: ").Append(LastNoPool).Append("): z warunkiem ").Append(LastOk).Append(", bez warunku ").Append(LastClans - LastOk)
                      .Append(LastClans > 0 ? " (" + (100.0 * (LastClans - LastOk) / LastClans).ToString("0.0", Inv) + "%)" : "")
                      .Append(" - zaloga ").Append(LastFailGar).Append(", sluzba ").Append(LastFailSvc).Append(", oba ").Append(LastFailBoth)
                      .Append(" | renta na udzial (zl/dobe): mediana krolestw ").Append(perShare.Count > 0 ? perShare[perShare.Count / 2].ToString("0", Inv) : "-")
                      .Append(", najwyzsza ").Append(perShare.Count > 0 ? perShare[perShare.Count - 1].ToString("0", Inv) : "-")
                      .Append(" | wagi: miasto ").Append(s.CrownRentWeightTown.ToString("0.##", Inv)).Append(", zamek ").Append(s.CrownRentWeightCastle.ToString("0.##", Inv))
                      .Append(", wies ").Append(s.CrownRentWeightVillage.ToString("0.##", Inv))
                      .Append("; warunek: zaloga >= ").Append((garShare * 100f).ToString("0", Inv)).Append("% normy ").Append(s.CrownRentGarrisonNormKingdom ? "krolestwa" : "swiata")
                      .Append(" (28 dob), sluzba >= ").Append(svcDays).Append(" z ostatnich ").Append(Window).Append(" dob wojny (rod, ktory widzial mniej dob wojny - ta sama czesc)")
                      .Append(" | sluzba dzis: rody w wojnie ").Append(warClans).Append(", sluzylo ").Append(srvArmy + srvSiege + srvBattle + srvLand + srvCaptive)
                      .Append(" (armia ").Append(srvArmy).Append(", oblezenie ").Append(srvSiege).Append(", bitwa ").Append(srvBattle).Append(", ziemia wroga ").Append(srvLand)
                      .Append(", niewola ").Append(srvCaptive).Append(')')
                      .Append(" | normy zalog (srednio na krolestwo, 28 dob): miasto ").Append(kT > 0 ? (nT28 / kT).ToString("0", Inv) : "-")
                      .Append(", zamek ").Append(kC > 0 ? (nC28 / kC).ToString("0", Inv) : "-")
                      .Append("; twierdze pominiete w warunku zalogi: w oblezeniu ").Append(sieged).Append("; twierdze swiata w rekach obecnego pana krocej niz ").Append(GraceDays).Append(" dob (pomijane) ").Append(_since.Count)
                      .Append(" | gracz: ").Append(pRow ? ("renta " + pr.Paid + " (udzialy " + playerW.ToString("0.##", Inv) + "; zaloga " + (pr.GarOk ? "TAK" : "NIE") + ", sluzba " + pr.Served + "/" + pr.Need + " " + (pr.SvcOk ? "TAK" : "NIE") + ")") : "bez lenna w krolestwie (albo najemnik)")
                      .Append(", sluzba ").Append(pSvc)
                      .Append(" | na krolestwo (renty/reszta): ").Append(txt.Count > 0 ? string.Join(", ", txt.ToArray()) : "-")
                      .Append(_stumbles > 0 ? " | potkniecia " + _stumbles : "")
                      .Append(_importN >= 0 ? " | wczytano: sluzba " + _importN + " rodow (bledne " + _importBad + ")" : "").Append('.');
                    Log.Info(sb.ToString());
                    _importN = -1;
                }
            }
            catch (Exception e) { Stumble("Daily", e); }
        }

        /// <summary>Ksiega 169 (CSV budzet-rodow): renta dnia i warunek rodu - ";renta;warunek" (puste - rod bez lenna, najemnik, 165/180 wylaczone).</summary>
        internal static string CsvCols(Clan c)
        {
            try
            {
                Row r;
                if (c == null || !_rows.TryGetValue(c, out r) || !r.Counted) return ";;";
                string st = r.GarOk && r.SvcOk ? "tak" : !r.GarOk && !r.SvcOk ? "zaloga+sluzba" : !r.GarOk ? "zaloga" : "sluzba";
                return ";" + r.Paid.ToString(Inv) + ";" + st + " " + r.Served.ToString(Inv) + "/" + r.Need.ToString(Inv);
            }
            catch { return ";;"; }
        }

        // ------------------------------------------------------------ zapis (SaveText, "arm_rent180")
        /// <summary>"v1|idRodu:idKrolestwa:maska(hex):doby;...|klucz normy>srednia>doby;...|idOsady=doba przejecia;..."</summary>
        internal static string Export()
        {
            try
            {
                var sb = new StringBuilder(64 + _svc.Count * 40 + _norm.Count * 24 + _since.Count * 24);
                sb.Append("v1|");
                bool first = true;
                foreach (var kv in _svc)
                {
                    var v = kv.Value;
                    if (kv.Key == null || kv.Key.IndexOfAny(Bad) >= 0 || v.Kingdom == null || v.Kingdom.IndexOfAny(Bad) >= 0 || v.Seen <= 0) continue;   // bez dob wojny - nic do zapisania
                    if (!first) sb.Append(';'); first = false;
                    sb.Append(kv.Key).Append(':').Append(v.Kingdom).Append(':').Append(v.Mask.ToString("x", Inv)).Append(':').Append(v.Seen.ToString(Inv));
                }
                sb.Append('|');
                first = true;
                foreach (var kv in _norm)
                {
                    double a = kv.Value.Avg();
                    if (a < 0 || kv.Key.IndexOfAny(Bad) >= 0) continue;
                    if (!first) sb.Append(';'); first = false;
                    sb.Append(kv.Key).Append('>').Append(a.ToString("0.#", Inv)).Append('>').Append(kv.Value.Filled.ToString(Inv));
                }
                sb.Append('|');
                first = true;
                foreach (var kv in _since)
                {
                    if (kv.Key == null || kv.Key.IndexOfAny(Bad) >= 0) continue;
                    if (!first) sb.Append(';'); first = false;
                    sb.Append(kv.Key).Append('=').Append(kv.Value.ToString(Inv));
                }
                return sb.ToString();
            }
            catch (Exception e) { Stumble("Export", e); return ""; }
        }
        private static readonly char[] Bad = { '|', ';', ':', '>', '=' };

        internal static void Import(string data)
        {
            _svc.Clear(); _norm.Clear(); _since.Clear(); _owner.Clear(); _importN = 0; _importBad = 0;
            try
            {
                if (string.IsNullOrEmpty(data)) return;   // stary zapis (bez klucza) - sluzba od zera (rod bez dob wojny - warunek sluzby nie obowiazuje)
                var f = data.Split('|');
                if (f.Length < 4 || f[0] != "v1") { _importBad++; return; }
                if (f[1].Length > 0)
                    foreach (var p in f[1].Split(';'))
                    {
                        var x = p.Split(':'); long mask; int seen;
                        if (x.Length == 4 && x[0].Length > 0 && x[1].Length > 0 && long.TryParse(x[2], NumberStyles.HexNumber, Inv, out mask) && int.TryParse(x[3], NumberStyles.Integer, Inv, out seen) && seen > 0)
                        { _svc[x[0]] = new Svc { Kingdom = x[1], Mask = mask & MaskAll, Seen = Math.Min(Window, seen) }; _importN++; }
                        else _importBad++;
                    }
                if (f[2].Length > 0)
                    foreach (var p in f[2].Split(';'))
                    {
                        var x = p.Split('>'); double a; int n;
                        if (x.Length == 3 && x[0].Length > 0 && double.TryParse(x[1], NumberStyles.Float, Inv, out a) && int.TryParse(x[2], NumberStyles.Integer, Inv, out n) && n > 0)
                        {
                            var nr = NormOf(x[0]);
                            for (int i = 0; i < Math.Min(NormDays, n); i++) nr.Push(a);   // srednia z zapisu na tyle dob, ile ich bylo
                        }
                        else _importBad++;
                    }
                if (f[3].Length > 0)
                    foreach (var p in f[3].Split(';'))
                    {
                        var x = p.Split('='); int d;
                        if (x.Length == 2 && x[0].Length > 0 && int.TryParse(x[1], NumberStyles.Integer, Inv, out d)) _since[x[0]] = d;
                        else _importBad++;
                    }
            }
            catch (Exception e) { Stumble("Import", e); }
        }
    }
}
