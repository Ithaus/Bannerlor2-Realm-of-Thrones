using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// 334 - ROWNOWAGA WOJNY KORONA-POLNOC WEDLUG KANONU, Z ZELAZNYMI LUDZMI (Jeff 06.11 13:45 "znowu Polnoc leje poludnie", 13:50 "Zelazni ludzie w ogole atakuja Polnoc,
    /// pladruja ja, czy siedza na wyspie?"). Projekt docs/PROJEKT-334-BALANS-WOJNY.md (rozdz. A przyczyny P1-P8, B zmiany B1-B9, C macierz 16 przypadkow, D klucze,
    /// E linie, F progi sprawdz_logi --grupa balans334).
    /// WSPOLNY KORZEN P1-P3: miary liczyly sile "calego krolestwa" albo "calej koalicji", nie te, ktora stoi na froncie. Ten plik - B1 miara frontu F334 (wspolny
    /// pomocnik) i linie "Balans wojny (334): ". Reszta: WarFront334.Theatre.cs (B7 teatr, B8 BK nastepny cel), WarFront334.Iron.cs (B9 Zelazni Z0-Z4),
    /// WarFront334.Hire.cs (B5 najem kompanii), latki w istniejacych miejscach: ScaNoBandits320 (B2, B3), CanonWarBalance.Measure/BuildMap (B4 274),
    /// WarTruce.Surrender.Strength (B4 330), ClanBudget.AtWar i KingdomTreasury.AtWar (B6).
    /// F334(X -> Y) = suma PartyBase.EstimatedStrength (p9 juz w niej) druzyn lordow i armii frakcji X (z druzyna gracza, gdy nalezy do X), ktore dojda do najblizszej
    /// twierdzy krolestwa Y albo do wlasnej twierdzy oblezonej przez Y w 8.02 doby (CanonWarBalance.DefendRangeDays - gra GetDistanceScoreForDefending). Droga
    /// AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty - ten sam pomocnik co mysl AI (morze ze statkami tak, jak liczy je gra), predkosc
    /// srednia lorda gry wedlug typu drogi (jak zbiorka 274). Zalogi nie wchodza (nie wychodza w pole). Liczone raz na dobe (leniwie przy pierwszym pytaniu doby),
    /// druzyny armii dostaja wynik wodza. Tylko kampania kanonu (Prologue.CanonHere) - poza nia regul 334 nie ma (macierz C15).
    /// Klucze na zywo; wylaczony = stan 167. Potkniecia liczone per obiekt, bez globalnego wylacznika. Zapis "arm_f334" (WarFront334Behavior).
    /// </summary>
    internal static partial class WarFront334
    {
        internal const string Tag = "Balans wojny (334): ";
        internal const string NorthId = "battania", CrownId = "vlandia", IronId = "sturgia", ReachId = "empire_w", DragonstoneId = "dragonstone";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static Settings S { get { return Settings.Current; } }
        private static int Today { get { return (int)CampaignTime.Now.ToDays; } }

        internal static bool Canon { get { try { return Prologue.CanonHere; } catch { return false; } } }
        internal static bool FrontOn { get { var s = S; return s != null && s.CanonWarFront334 && Canon; } }
        internal static bool ScaFrontOn { get { var s = S; return s != null && s.ScaFrontThreat334 && Canon; } }
        internal static bool ScaLockOn { get { var s = S; return s != null && s.ScaFightsOnMainWarLocked334 && Canon; } }
        internal static bool HireOn { get { var s = S; return s != null && s.CanonWarBalanceHireCompanies && Canon; } }
        internal static bool TruceArmedOn { get { var s = S; return s != null && s.WarTruceArmed334 && Canon; } }
        internal static bool TheatreKey { get { var s = S; return s != null && s.NorthCanonTheatre334 && Canon; } }
        internal static bool BkNextOn { get { var s = S; return s != null && s.BkVetoNextTarget334 && Canon; } }
        internal static bool IronOn { get { var s = S; return s != null && s.IronbornCanonReave334 && Canon; } }
        internal static bool StanceOn { get { var s = S; return s != null && s.CanonWarStance334c && Canon; } }
        internal static bool ScaAllyOn { get { var s = S; return s != null && s.ScaCoalitionOwn334c && s.ScaFrontThreat334 && Canon; } }

        // ================================================================ stan F334 (doba, bez zapisu)
        // Forts: twierdze, na ktore REALNIE ida druzyny (cel w krolestwie, oblezenie / najazd, druzyna na jego ziemi) - sila zagrozenia na twierdze (homeGuard 274)
        private sealed class Pair { public double S; public int Parties, Men; public readonly Dictionary<Settlement, double> Forts = new Dictionary<Settlement, double>(); }
        private static readonly Dictionary<IFaction, Dictionary<Kingdom, Pair>> _front = new Dictionary<IFaction, Dictionary<Kingdom, Pair>>();
        private static readonly Dictionary<MobileParty, List<Kingdom>> _reach = new Dictionary<MobileParty, List<Kingdom>>();
        // po recenzji 334 (W1): druzyny, ktore dojda do WLASNEJ osady atakowanej przez wroga (oblezona twierdza, najezdzana wies) - obrona przed trzecim wrogiem
        private static readonly HashSet<MobileParty> _defend = new HashSet<MobileParty>();
        private static int _day = -1, _computeN, _calls, _partiesN, _ms;
        private static bool _broken;   // po recenzji 334 (W3 r2): liczenie doby rzucilo - miara nieczynna do konca doby (bez powtarzania przy kazdym pytaniu)
        private static int _stumbles, _dStumbles;
        private static readonly HashSet<string> _errSites = new HashSet<string>(StringComparer.Ordinal);
        // SCA (B2): ostatnia ocena na krolestwo - surowy wrog SCA i F334 (linia 3)
        private sealed class ScaRec { public float Raw, Front; public bool Used; public float OwnRaw = -1f, OwnCoal = -1f; public bool OwnField; }
        private static readonly Dictionary<string, ScaRec> _sca = new Dictionary<string, ScaRec>(StringComparer.Ordinal);
        private static int _dScaFront, _dScaRaw;   // oceny SCA z F334 / surowe (bez F334 - pierwsze godziny sesji)

        internal static void Reset()
        {
            _front.Clear(); _reach.Clear(); _defend.Clear(); _day = -1; _broken = false; _computeN = _calls = _partiesN = _ms = 0; _stumbles = _dStumbles = 0; _errSites.Clear();
            _sca.Clear(); _dScaFront = _dScaRaw = 0;
            ResetTheatre(); ResetIron(); ResetSaved(); ResetStance();
            WarFront334Test.Reset();
            LocalFront334e.Reset(); LocalFront334eTest.Reset();   // 334e: sila lokalna
        }

        internal static void Stumble(string where, Exception e)
        {
            _stumbles++; _dStumbles++;
            try { if (_errSites.Add(where)) Log.Error("WarFront334." + where + " (potkniecie " + _stumbles + ")", e); } catch { }
        }

        private static string Nm(IFaction f) { try { return f != null && f.Name != null ? f.Name.ToString() : "-"; } catch { return "?"; } }
        private static string Nm(Settlement st) { try { return st != null && st.Name != null ? st.Name.ToString() : "-"; } catch { return "?"; } }
        private static string Nm(MobileParty mp) { try { return mp != null && mp.Name != null ? mp.Name.ToString() : "-"; } catch { return "?"; } }
        private static string Nm(Hero h) { try { return h != null && h.Name != null ? h.Name.ToString() : "-"; } catch { return "?"; } }
        private static string Nm(Clan c) { try { return c != null && c.Name != null ? c.Name.ToString() : "-"; } catch { return "?"; } }
        private static string F0(double v) { return v.ToString("0", Inv); }
        private static string F2(double v) { return v.ToString("0.00", Inv); }

        internal static Kingdom FindKingdom(string id)
        {
            try { foreach (var k in Kingdom.All) if (k != null && !k.IsEliminated && k.StringId == id) return k; } catch { }
            return null;
        }

        // ================================================================ czyste funkcje (autotest)

        /// <summary>Doba drogi przy predkosci sredniej lorda wedlug typu drogi (jak zbiorka 274). -1 - brak drogi.</summary>
        internal static float Days(MobileParty.NavigationType nav, float dist, float land, float naval)
        {
            if (nav == MobileParty.NavigationType.None || dist < 0f || dist >= float.MaxValue * 0.5f) return -1f;
            float speed = nav == MobileParty.NavigationType.Naval ? naval : nav == MobileParty.NavigationType.All ? (land + naval) * 0.5f : land;
            if (!(speed > 0f)) return -1f;
            return dist / (speed * CampaignTime.HoursInDay);
        }

        /// <summary>B3 (autotest): czy wojny z zablokowanym pokojem daja co najmniej polowe zagrozenia frontu (lockedThreat / total >= 0.5). total 0 - null (stara regula).</summary>
        internal static bool? LockedShare(double lockedThreat, double total)
        {
            if (!(total > 0.0)) return null;
            return lockedThreat >= 0.5 * total;
        }

        // ================================================================ B1: liczenie F334

        /// <summary>F334 gotowe na dzis (liczy przy pierwszym pytaniu doby). false - kampania nie kanonu albo gra jeszcze nie ruszyla.</summary>
        internal static bool Ensure()
        {
            try
            {
                var c = Campaign.Current;
                if (c == null || !c.GameStarted || !Canon) return false;
                int d = Today;
                if (_day == d) return !_broken;
                _day = d; _broken = true;   // ustawione przed liczeniem: wyjatek nie powtarza liczenia w tej dobie
                Compute();
                _broken = false;
                return true;
            }
            catch (Exception e) { Stumble("Ensure", e); return false; }
        }

        private static void Compute()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            _front.Clear(); _reach.Clear(); _defend.Clear(); _lastTarget.Clear(); _calls = 0; _partiesN = 0;
            var c = Campaign.Current;
            float land = c.EstimatedAverageLordPartySpeed, naval = c.EstimatedAverageLordPartyNavalSpeed;
            // najdluzsza mozliwa droga skorygowana: odcinek morski mnozony przez gre najmniej x0.35 (CalculateShipDistanceAmplifier) - prosta dalsza niz to = poza zasiegiem.
            // Po recenzji 334 (W4 r2): druzyna bez statkow idzie tylko ladem (droga >= prosta) - jej filtr z predkosci ladowej, bez /0.35.
            float maxSpeed = Math.Max(land, naval);
            float maxReach = maxSpeed * CampaignTime.HoursInDay * CanonWarBalance.DefendRangeDays / 0.35f;
            float maxReach2 = maxReach * maxReach;
            float landReach = land * CampaignTime.HoursInDay * CanonWarBalance.DefendRangeDays;
            float landReach2 = landReach * landReach;
            // twierdze krolestw i osady atakowane (twierdza oblezona, wies najezdzana: wlasciciel -> napastnik)
            var forts = new Dictionary<Kingdom, List<Settlement>>();
            var besieged = new List<KeyValuePair<Settlement, IFaction>>();
            foreach (var t in Town.AllFiefs)
            {
                var st = t != null ? t.Settlement : null;
                if (st == null) continue;
                var k = st.MapFaction as Kingdom;
                if (k == null || k.IsEliminated) continue;
                List<Settlement> l;
                if (!forts.TryGetValue(k, out l)) { l = new List<Settlement>(); forts[k] = l; }
                l.Add(st);
                try
                {
                    var ev = st.SiegeEvent;
                    var lp = ev != null && ev.BesiegerCamp != null ? ev.BesiegerCamp.LeaderParty : null;
                    if (lp != null && lp.MapFaction != null) besieged.Add(new KeyValuePair<Settlement, IFaction>(st, lp.MapFaction));
                }
                catch { }
            }
            // po recenzji 334 (W1): wsie najezdzane - obrona wlasnej wsi to tez druzyna zajeta wojna (Zelazni na Kamiennym Brzegu)
            try
            {
                foreach (var v in Village.All)
                {
                    var vs = v != null ? v.Settlement : null;
                    if (vs == null || v.VillageState != Village.VillageStates.BeingRaided) continue;
                    var me = vs.Party != null ? vs.Party.MapEvent : null;
                    var lp = me != null && me.AttackerSide != null ? me.AttackerSide.LeaderParty : null;
                    var af = lp != null ? lp.MapFaction : (vs.LastAttackerParty != null ? vs.LastAttackerParty.MapFaction : null);
                    if (af != null) besieged.Add(new KeyValuePair<Settlement, IFaction>(vs, af));
                }
            }
            catch (Exception e) { Stumble("Compute.raids", e); }
            var parties = new List<MobileParty>(512);
            foreach (var mp in MobileParty.AllLordParties) if (mp != null && mp.IsActive && mp.Party != null) parties.Add(mp);
            var main = MobileParty.MainParty;
            if (main != null && main.IsActive && main.Party != null && !main.IsLordParty && main.MapFaction is Kingdom) parties.Add(main);
            // wodzowie (i druzyny bez armii) najpierw, potem czlonkowie armii z wynikiem wodza
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < parties.Count; i++)
                {
                    var mp = parties[i];
                    var army = mp.Army;
                    bool follower = army != null && army.LeaderParty != null && army.LeaderParty != mp;
                    if ((pass == 0) == follower) continue;
                    try
                    {
                        var F = mp.MapFaction;
                        if (F == null || ScaNoBandits320.Skip(F) != 0) continue;
                        List<Kingdom> got = null;
                        if (follower)
                        {
                            List<Kingdom> lead;
                            if (_reach.TryGetValue(army.LeaderParty, out lead)) { got = Filter(lead, F); if (_defend.Contains(army.LeaderParty)) _defend.Add(mp); }
                        }
                        bool ships = mp.Ships != null && mp.Ships.Count > 0;
                        if (got == null) got = ReachOf(mp, F, forts, besieged, land, naval, ships ? maxReach2 : landReach2);
                        _reach[mp] = got;
                        _partiesN++;
                        if (got.Count == 0) continue;
                        float s = mp.Party.EstimatedStrength;
                        int men = mp.Party.NumberOfAllMembers;
                        Dictionary<Kingdom, Pair> row;
                        if (!_front.TryGetValue(F, out row)) { row = new Dictionary<Kingdom, Pair>(); _front[F] = row; }
                        var lead2 = follower ? army.LeaderParty : mp;
                        for (int j = 0; j < got.Count; j++)
                        {
                            Pair p;
                            if (!row.TryGetValue(got[j], out p)) { p = new Pair(); row[got[j]] = p; }
                            p.S += s; p.Parties++; p.Men += men;
                            var tf = TargetOf(lead2, got[j]);
                            if (tf != null && tf.MapFaction == got[j] && RealThreat(lead2, got[j]))
                            {
                                double cur; p.Forts.TryGetValue(tf, out cur); p.Forts[tf] = cur + s;
                            }
                        }
                    }
                    catch (Exception e) { Stumble("Compute.party", e); }
                }
            }
            _computeN++;
            _ms = (int)sw.ElapsedMilliseconds;
        }

        // cel (twierdza) dojscia partii do krolestwa - dla homeGuard 274 (B4) i linii; tylko doba
        private static readonly Dictionary<MobileParty, Dictionary<Kingdom, Settlement>> _lastTarget = new Dictionary<MobileParty, Dictionary<Kingdom, Settlement>>();

        /// <summary>Po recenzji 334 (W2/W7): druzyna REALNIE idzie na krolestwo k - oblega / najezdza jego osade, jej cel (TargetSettlement) jest jego, albo stoi na jego
        /// ziemi (najblizsza twierdza nalezy do k). Partia stojaca na Pyke albo na morzu bez celu w k - nie (sama mozliwosc doplyniecia nie zatrzymuje rodow w domu).</summary>
        private static bool RealThreat(MobileParty mp, Kingdom k)
        {
            try
            {
                if (mp == null || k == null) return false;
                var bs = mp.BesiegedSettlement;
                if (bs != null && bs.MapFaction == k) return true;
                var ts = mp.TargetSettlement;
                if (ts != null && ts.MapFaction == k) return true;
                if (mp.IsCurrentlyAtSea) return false;
                var ns = NearestAny(mp.GetPosition2D);
                return ns != null && ns.MapFaction == k;
            }
            catch { return false; }
        }

        private static Settlement TargetOf(MobileParty mp, Kingdom k)
        {
            Dictionary<Kingdom, Settlement> d; Settlement st;
            return mp != null && _lastTarget.TryGetValue(mp, out d) && d.TryGetValue(k, out st) ? st : null;
        }

        private static List<Kingdom> Filter(List<Kingdom> lead, IFaction F)
        {
            var r = new List<Kingdom>(lead.Count);
            for (int i = 0; i < lead.Count; i++) { try { if (FactionManager.IsAtWarAgainstFaction(F, lead[i])) r.Add(lead[i]); } catch { } }
            return r;
        }

        private static List<Kingdom> ReachOf(MobileParty mp, IFaction F, Dictionary<Kingdom, List<Settlement>> forts, List<KeyValuePair<Settlement, IFaction>> besieged,
                                             float land, float naval, float maxReach2)
        {
            var got = new List<Kingdom>(2);
            var wars = F.FactionsAtWarWith;
            if (wars == null) return got;
            var pos = mp.GetPosition2D;
            foreach (var f in wars)
            {
                var E = f as Kingdom;
                if (E == null || E.IsEliminated || ScaNoBandits320.Skip(E) != 0) continue;
                // kandydaci: dwie najblizsze (prosta) twierdze E i wlasne twierdze F oblegane przez E
                Settlement a = null, b = null; float da = float.MaxValue, db = float.MaxValue;
                List<Settlement> l;
                if (forts.TryGetValue(E, out l))
                    for (int i = 0; i < l.Count; i++)
                    {
                        float d = pos.DistanceSquared(l[i].GetPosition2D);
                        if (d < da) { b = a; db = da; a = l[i]; da = d; } else if (d < db) { b = l[i]; db = d; }
                    }
                for (int i = 0; i < besieged.Count; i++)
                {
                    if (besieged[i].Value != E || besieged[i].Key.MapFaction != F) continue;
                    float d = pos.DistanceSquared(besieged[i].Key.GetPosition2D);
                    if (d < da) { b = a; db = da; a = besieged[i].Key; da = d; } else if (d < db) { b = besieged[i].Key; db = d; }
                }
                Settlement hit = null;
                if (a != null && da <= maxReach2 && InRange(mp, a, land, naval)) hit = a;
                else if (b != null && db <= maxReach2 && InRange(mp, b, land, naval)) hit = b;
                if (hit == null) continue;
                got.Add(E);
                if (hit.MapFaction == F) _defend.Add(mp);   // obrona wlasnej osady atakowanej przez E (W1)
                Dictionary<Kingdom, Settlement> td;
                if (!_lastTarget.TryGetValue(mp, out td)) { td = new Dictionary<Kingdom, Settlement>(); _lastTarget[mp] = td; }
                td[E] = hit;
            }
            return got;
        }

        private static bool InRange(MobileParty mp, Settlement st, float land, float naval)
        {
            _calls++;
            MobileParty.NavigationType nav; float dist; bool port;
            AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty(mp, st, false, out nav, out dist, out port);
            float d = Days(nav, dist, land, naval);
            return d >= 0f && d <= CanonWarBalance.DefendRangeDays;
        }

        // ================================================================ odczyty (B2-B4)

        /// <summary>F334(X -> Y): sila druzyn frakcji X, ktore dojda do Y w horyzoncie obrony. -1 - miara nieczynna (wolajacy bierze stara miare).</summary>
        internal static double Front(IFaction attacker, Kingdom defender)
        {
            if (attacker == null || defender == null || !Ensure()) return -1;
            Dictionary<Kingdom, Pair> row; Pair p;
            return _front.TryGetValue(attacker, out row) && row.TryGetValue(defender, out p) ? p.S : 0.0;
        }

        private static void FrontFull(IFaction attacker, Kingdom defender, out double s, out int parties, out int men)
        {
            s = 0; parties = 0; men = 0;
            Dictionary<Kingdom, Pair> row; Pair p;
            if (attacker != null && defender != null && _front.TryGetValue(attacker, out row) && row.TryGetValue(defender, out p)) { s = p.S; parties = p.Parties; men = p.Men; }
        }

        /// <summary>Partia dojdzie do krolestwa k (doba). Druzyna spoza liczenia (nowa po polnocy) - false.</summary>
        internal static bool Reaches(MobileParty mp, Kingdom k)
        {
            List<Kingdom> l;
            return mp != null && k != null && Ensure() && _reach.TryGetValue(mp, out l) && l.Contains(k);
        }

        /// <summary>274 (B4): partia dojdzie do ktoregokolwiek krolestwa strony `side` (CanonWarBalance.SideOf).</summary>
        internal static bool ReachesSide(MobileParty mp, int side)
        {
            List<Kingdom> l;
            if (mp == null || side == 0 || !Ensure() || !_reach.TryGetValue(mp, out l)) return false;
            for (int i = 0; i < l.Count; i++) if (CanonWarBalance.SideOf(l[i]) == side) return true;
            return false;
        }

        /// <summary>274 (W1 po recenzji 334): druzyna zajeta wojna - dojdzie do strony `side` ALBO broni wlasnej osady atakowanej przez dowolnego wroga (trzeci wrog:
        /// Zelazni na Kamiennym Brzegu). Obrona przed Wyspami nie jest "slaboscia frontu" - dzwignie 274 nie zjadaja kanonicznego hamulca Polnocy.</summary>
        internal static bool Engaged(MobileParty mp, int side)
        {
            if (mp == null || !Ensure()) return false;
            return ReachesSide(mp, side) || _defend.Contains(mp);
        }

        /// <summary>274 homeGuard (B4, po recenzji 334 W2/W7): twierdze krolestwa k, na ktore REALNIE ida druzyny frakcji f (RealThreat) i ktorych zaloga jest slabsza
        /// od tego zagrozenia (PartyBase.EstimatedStrength - ta sama miara). Sama mozliwosc doplyniecia z Pyke nikogo nie zatrzymuje.</summary>
        internal static void ThreatenedForts(IFaction f, Kingdom k, List<Settlement> into)
        {
            if (f == null || k == null || !Ensure()) return;
            Dictionary<Kingdom, Pair> row; Pair p;
            if (!_front.TryGetValue(f, out row) || !row.TryGetValue(k, out p)) return;
            foreach (var kv in p.Forts)
            {
                double gar = 0;
                try { var gp = kv.Key.Town != null ? kv.Key.Town.GarrisonParty : null; gar = gp != null && gp.Party != null ? gp.Party.EstimatedStrength : 0; } catch { }
                if (kv.Value > gar) into.Add(kv.Key);
            }
        }

        /// <summary>Frakcje z druzynami w zasiegu krolestwa k (do homeGuard i linii).</summary>
        internal static List<IFaction> AttackersOf(Kingdom k)
        {
            var r = new List<IFaction>();
            if (k == null || !Ensure()) return r;
            foreach (var kv in _front) if (kv.Value.ContainsKey(k)) r.Add(kv.Key);
            return r;
        }

        /// <summary>B2: zagrozenie krolestwa = suma F334 wrogow (Skip 0) do niego. -1 - miara nieczynna.</summary>
        internal static double Threat(Kingdom k)
        {
            if (k == null || !Ensure()) return -1;
            double e = 0;
            var wars = k.FactionsAtWarWith;
            if (wars == null) return 0;
            foreach (var f in wars)
            {
                if (f == null || ScaNoBandits320.Skip(f) != 0) continue;
                e += Math.Max(0.0, Front(f, k));
            }
            return e;
        }

        /// <summary>B2 (postfiks 320b CalculateFactionStatus): EnemyStrength z frontu. raw - wartosc SCA bez band (320b) do linii. Zwraca nowa wartosc albo -1 (bez zmiany).</summary>
        internal static float ScaEnemy(Kingdom k, float raw)
        {
            if (k == null) return -1f;
            ScaRec r;
            if (!_sca.TryGetValue(k.StringId ?? "", out r)) { r = new ScaRec(); _sca[k.StringId ?? ""] = r; }
            r.Raw = raw; r.Used = false;
            bool local = LocalFront334e.ScaLocalOn;   // 334e (ScaLocal334e): wrog w polu dla WSZYSTKICH krolestw AI
            if (!ScaFrontOn && !local) { r.Front = -1f; return -1f; }
            // po recenzji 334 (W3): B2 tylko dla krolestw wojny kanonu (strony 274) i Wysp - Dorne, Dolina, Essos bez zmian (wyczerpanie SCA jak dotad)
            if (!local && CanonWarBalance.SideOf(k) == 0 && k.StringId != IronId) { r.Front = -1f; return -1f; }
            double t = Threat(k);
            if (t < 0) { _dScaRaw++; r.Front = -1f; return -1f; }
            r.Front = (float)t; r.Used = true; _dScaFront++;
            return (float)t;
        }

        /// <summary>334c (postfiks 320b CalculateFactionStatus, po ScaEnemy): OwnedStrength krolestwa strony 274 = suma CurrentTotalStrength krolestw tej strony (ta sama
        /// miara, ktora SCA bierze dla jednego krolestwa). Swiadoma asymetria (recenzja 334c W4): swoi = cala sila koalicji (rezerwy koalicji moga przyjsc - SCA tez
        /// liczy swoich bez zasiegu), wrog = F334 (tylko ci, ktorzy dosiegna). Front(own, enemy) po stronie swoich zanizylby obrone stolicy daleko od frontu. raw - wartosc SCA (do linii). Zwraca nowa wartosc albo -1 (bez zmiany: klucz NIE, krolestwo poza 274).</summary>
        internal static float ScaOwn(Kingdom k, float raw)
        {
            if (k == null) return -1f;
            ScaRec r;
            if (!_sca.TryGetValue(k.StringId ?? "", out r)) { r = new ScaRec(); _sca[k.StringId ?? ""] = r; }
            r.OwnRaw = raw; r.OwnCoal = -1f; r.OwnField = false;
            // 334e (ScaLocal334e): swoi = sila w polu krolestwa i koalicji w zasiegu 8.02 doby od jego twierdz (lustro F334) - suma koalicji 334c nie dziala
            if (LocalFront334e.ScaLocalOn)
            {
                double of = LocalFront334e.OwnField(k);
                if (of >= 0) { r.OwnCoal = (float)of; r.OwnField = true; return (float)of; }
            }
            if (!ScaAllyOn) return -1f;
            int side = CanonWarBalance.SideOf(k);
            if (side == 0) return -1f;
            double sum = 0;
            foreach (var o in Kingdom.All)
            {
                if (o == null || o.IsEliminated || CanonWarBalance.SideOf(o) != side) continue;
                try { sum += Math.Max(0f, o.CurrentTotalStrength); } catch { }
            }
            if (!(sum > 0)) return -1f;
            r.OwnCoal = (float)sum;
            return (float)sum;
        }

        /// <summary>334e (linia krolestwa): ostatnie wartosci SCA - wrog surowy / z pola, swoi surowi / z pola (locUsed - swoi z pola 334e).</summary>
        internal static void ScaVals(Kingdom k, out float raw, out float front, out float ownRaw, out float ownField, out bool locUsed)
        {
            raw = front = ownRaw = ownField = -1f; locUsed = false;
            ScaRec r;
            if (k == null || !_sca.TryGetValue(k.StringId ?? "", out r)) return;
            raw = r.Raw; front = r.Used ? r.Front : -1f; ownRaw = r.OwnRaw; ownField = r.OwnCoal; locUsed = r.OwnField;
        }

        /// <summary>B3: pokoj z frakcja f zablokowany na stale w tej chwili - 264/330 (CanonPeaceLock.Blocks) albo wojna wymuszona przez ROT (Wyspy-Polnoc, zatargi).</summary>
        internal static bool PeaceImpossible(Kingdom k, IFaction f)
        {
            try { if (CanonPeaceLock.Blocks(k, f)) return true; } catch { }
            try { if (Prologue.RotWarForced(k, f)) return true; } catch { }
            return false;
        }

        /// <summary>B3 (320e): wojny bez mozliwego pokoju daja >= polowe zagrozenia F334. null - regula nieczynna albo brak zagrozenia (stara regula 320e).</summary>
        internal static bool? MainWarLocked(Kingdom k, out string why)
        {
            why = null;
            if (k == null || !ScaLockOn || !Ensure()) return null;
            double tot = 0, lk = 0; int n = 0, nl = 0;
            var wars = k.FactionsAtWarWith;
            if (wars == null) return null;
            foreach (var f in wars)
            {
                if (f == null || ScaNoBandits320.Skip(f) != 0) continue;
                double v = Math.Max(0.0, Front(f, k));
                tot += v;
                if (!f.IsKingdomFaction) continue;
                n++;
                if (PeaceImpossible(k, f)) { lk += v; nl++; }
            }
            // po recenzji 334 (W5): front glownej wojny chwilowo pusty (Polnoc wrocila do domu), a obok mala wojna z zagrozeniem - stara regula 320e, nie "false"
            if (nl > 0 && !(lk > 0.0)) return null;
            var r = LockedShare(lk, tot);
            if (r == null) return null;
            // bez nawiasow i srednikow - linia 320b (sprawdz_logi S320_K: powod [^()]*, krolestwa po '; '); zablokowany zaczyna sie od "pokoj zablokowany" (S320-10)
            why = (r.Value ? "pokoj zablokowany 334 - " : "334 ponizej polowy - ") + "zagrozenie wojen bez pokoju " + F0(lk) + " z " + F0(tot) + ", krolestwa " + nl + " z " + n;
            return r;
        }

        // ================================================================ linie doby (ArmouryBehavior po ReaverLaw.Daily)

        private static bool _startSaid;

        internal static void SessionStart()
        {
            _startSaid = false;
            try { NavGuard334.SessionStart(); } catch (Exception e) { Stumble("NavGuard334.SessionStart", e); }   // 334c: straznik pozycji - kazda kampania
            try { ScaNoBandits320.Wire(); } catch (Exception e) { Stumble("ScaWire", e); }   // 334c (B334-1): latki SCA przed linia startu (kolejnosc sluchaczy)
            try { EnsureHooks(); } catch (Exception e) { Stumble("EnsureHooks", e); }
            try { EnsureStanceHook(); } catch (Exception e) { Stumble("EnsureStanceHook", e); }
            try { LocalFront334e.SessionStart(); } catch (Exception e) { Stumble("334e.SessionStart", e); }   // 334e: latki i linia startu sily lokalnej
            // po recenzji 334c: bez StanceApply przy starcie sesji (GameStarted bywa false - nic by nie zrobil); postawe ustawia postfiks BK TickKingdoms (co dobe i po wczytaniu)
            try { StartLine(); } catch (Exception e) { Stumble("StartLine", e); }
        }

        private static string Key01(bool b) { return b ? "TAK" : "NIE"; }

        private static void StartLine()
        {
            var s = S;
            if (s == null) return;
            int inT = 0, outT = 0;
            try
            {
                foreach (var st in Settlement.All)
                {
                    if (st == null || !(st.IsFortification || st.IsVillage)) continue;
                    if (NorthTheatre(st, null)) inT++; else outT++;
                }
            }
            catch (Exception e) { Stumble("StartLine.theatre", e); }
            Log.Info(Tag + "start sesji | klucze: front " + Key01(s.CanonWarFront334) + ", SCA front " + Key01(s.ScaFrontThreat334) + ", SCA glowna wojna " + Key01(s.ScaFightsOnMainWarLocked334)
                     + ", najem kompanii " + Key01(s.CanonWarBalanceHireCompanies) + ", rozejm uzbrojony " + Key01(s.WarTruceArmed334) + ", teatr Polnocy " + Key01(s.NorthCanonTheatre334)
                     + ", BK nastepny cel " + Key01(s.BkVetoNextTarget334) + ", Zelazni " + Key01(s.IronbornCanonReave334)
                     + ", postawa 334c " + Key01(s.CanonWarStance334c) + ", SCA swoi koalicji 334c " + Key01(s.ScaCoalitionOwn334c) + ", straznik pozycji 334c " + Key01(s.PartyPositionGuard334c)
                     + " | zakres: " + (Canon ? "kampania kanonu" : "kampania klasyczna - nie dotyczy")
                     + " | latki: SCA status " + ScaNoBandits320.WiredStatus + ", SCA cel " + ScaNoBandits320.WiredGoal + ", BK " + _wBk + ", mysl AI " + _wThink
                     + ", postawa BK " + _wStance + ", straznik " + NavGuard334.WiredText
                     + " | teatr Polnocy: osad w teatrze " + inT + " / poza " + outT + " | Wyspy: " + RangeText()
                     + " | stan z zapisu: najmow " + _hiredN + " (ostatni d." + _lastHireDay + "), osad Polnocy na odbicie " + _northHeld.Count + ", przed rozejmem partii korony " + _preTruceParties
                     + WarFront334Test.StartText() + ".");
            _startSaid = true;
        }

        /// <summary>ArmouryBehavior.OnDailyTick (po ReaverLaw.Daily): linie 2-11. Nie rzuca.</summary>
        internal static void Daily()
        {
            int today = Today;
            try
            {
                var s = S;
                if (s == null || Campaign.Current == null) return;
                if (!Canon)
                {
                    if (s.LogEnabled) Log.Info(Tag + "dzien " + today + " | kampania klasyczna - reguly 334 nieczynne, pomiar Zelaznych tylko w linii.");
                    try { IronDaily(today, s.LogEnabled); } catch (Exception e) { Stumble("IronDaily", e); }
                    return;
                }
                if (!_startSaid) { try { StartLine(); } catch (Exception e) { Stumble("StartLine", e); } }
                Ensure();
                try { NorthHeldDaily(); } catch (Exception e) { Stumble("NorthHeldDaily", e); }
                try { TruceDaily(today, s.LogEnabled); } catch (Exception e) { Stumble("TruceDaily", e); }
                if (s.LogEnabled)
                {
                    try { FrontLine(today); } catch (Exception e) { Stumble("FrontLine", e); }
                    try { ScaLine(today); } catch (Exception e) { Stumble("ScaLine", e); }
                    try { TheatreLine(today); } catch (Exception e) { Stumble("TheatreLine", e); }
                    try { HireLine(today); } catch (Exception e) { Stumble("HireLine", e); }
                    try { CrownLine(today); } catch (Exception e) { Stumble("CrownLine", e); }
                    try { StanceLine(today); } catch (Exception e) { Stumble("StanceLine", e); }
                }
                try { IronDaily(today, s.LogEnabled); } catch (Exception e) { Stumble("IronDaily", e); }
                try { LocalFront334e.Daily(today); } catch (Exception e) { Stumble("334e.Daily", e); }   // 334e: linie sily lokalnej i zerowanie doby
                if (s.LogEnabled)
                {
                    try { ControlLine(today); } catch (Exception e) { Stumble("ControlLine", e); }
                }
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally { ZeroDay(); }
        }

        private static void ZeroDay()
        {
            _dScaFront = _dScaRaw = 0; _dStumbles = 0;
            ZeroTheatreDay(); ZeroIronDay(); ZeroHireDay(); ZeroStanceDay();
        }

        private static string Phase()
        {
            try
            {
                if (CanonWarClock.OverNow()) return "F2";
                // Czarny Nurt: 332 (Reach w koronie) - tylko etykieta linii; Reach zniknal z innego powodu - F0 z dopiskiem
                bool folded = false;
                try { folded = ReachJoinsCrown.Done; } catch { }
                if (folded) return "F1";
                return FindKingdom(ReachId) == null ? "F0(bez-Reach)" : "F0";
            }
            catch { return "?"; }
        }

        /// <summary>Linia 2: front Polnoc -> korona i korona -> Polnoc, rF (front) i koalicja (274), stan dzwigni 274, sila 330.</summary>
        private static void FrontLine(int today)
        {
            var n = FindKingdom(NorthId); var c = FindKingdom(CrownId);
            var sb = new StringBuilder(700);
            sb.Append(Tag).Append("dzien ").Append(today).Append(" | faza ").Append(Phase());
            if (n == null || c == null) { sb.Append(" | brak krolestwa Polnocy albo korony - linia bez frontu."); Log.Info(sb.ToString()); return; }
            bool war = false;
            try { war = n.IsAtWarWith(c); } catch { }
            if (!war)
            {
                // po recenzji 334: w rozejmie 330 F334 widzi tylko wojny - 0/0 dawalo bzdurne rF; linia bez frontu (B334-3 jej nie liczy)
                sb.Append(" | Polnoc i korona bez wojny (rozejm 330 albo pokoj) - front nie liczony | 274: ").Append(CanonWarBalance.LeverText).Append('.');
                Log.Info(sb.ToString());
                return;
            }
            // strona A (Polnoc z sojusznikami) -> strona B; strona B (korona z sojusznikami w zasiegu) -> strona A
            double fa = 0, fb = 0, fbAlly = 0; int pa = 0, ma = 0, pb = 0, mb = 0;
            foreach (var kv in _front)
            {
                int sideX = CanonWarBalance.SideOf(kv.Key);
                if (sideX == 0) continue;
                foreach (var kv2 in kv.Value)
                {
                    int sideY = CanonWarBalance.SideOf(kv2.Key);
                    if (sideY == 0 || sideY == sideX) continue;
                    if (sideX == 1) { fa += kv2.Value.S; pa += kv2.Value.Parties; ma += kv2.Value.Men; }
                    else { fb += kv2.Value.S; pb += kv2.Value.Parties; mb += kv2.Value.Men; if (kv.Key != c) fbAlly += kv2.Value.S; }
                }
            }
            // partia moze dojsc do dwoch krolestw strony - liczona raz (Engaged), jak 274: front drugiej strony albo obrona wlasnej osady przed trzecim wrogiem (W1)
            double sa = 0, sbb = 0, da = 0, db = 0;
            foreach (var kv in _reach)
            {
                var mp = kv.Key;
                int side = CanonWarBalance.SideOf(mp.MapFaction);
                if (side == 0 || mp.Party == null) continue;
                bool fr = ReachesSide(mp, side == 1 ? 2 : 1);
                if (!fr && !_defend.Contains(mp)) continue;
                if (side == 1) { sa += mp.Party.EstimatedStrength; if (!fr) da += mp.Party.EstimatedStrength; }
                else { sbb += mp.Party.EstimatedStrength; if (!fr) db += mp.Party.EstimatedStrength; }
            }
            double rF = sa > 0 ? sbb / sa : (sbb > 0 ? 99.0 : 1.0);
            double rC = CanonWarBalance.LastCoalitionRatio;
            sb.Append(" | front Polnoc->korona ").Append(F0(sa)).Append(" (partii ").Append(pa).Append(", ludzi ").Append(ma).Append(')')
              .Append(" / korona->Polnoc ").Append(F0(sbb)).Append(" (partii ").Append(pb).Append(", ludzi ").Append(mb).Append(", w tym sojusznik ").Append(F0(fbAlly)).Append(')')
              .Append(" | rF ").Append(rF.ToString("0.000", Inv)).Append(" (koalicja ").Append(rC >= 0 ? rC.ToString("0.000", Inv) : "-").Append(')')
              .Append(" | obrona przed trzecim wrogiem w sile: Polnoc ").Append(F0(da)).Append(", korona ").Append(F0(db))
              .Append(" | 274: ").Append(CanonWarBalance.LeverText)
              .Append(" | 330 sila ").Append(WarTruce.LastStrengthText)
              .Append(" | miara: partii ").Append(_partiesN).Append(", drog AiHelper ").Append(_calls).Append(", ").Append(_ms).Append(" ms, klucz ").Append(Key01(FrontOn)).Append('.');
            Log.Info(sb.ToString());
        }

        /// <summary>Linia 3: SCA na krolestwo (korona, Reach, Wyspy, Polnoc, Smocza Skala): wrog surowy -> F334, swoi, wyczerpane, cel, chce pokoju.</summary>
        private static void ScaLine(int today)
        {
            var sb = new StringBuilder(900);
            sb.Append(Tag).Append("dzien ").Append(today).Append(" | SCA (front ").Append(Key01(ScaFrontOn)).Append(", glowna wojna ").Append(Key01(ScaLockOn))
              .Append("; ocen z F334 ").Append(_dScaFront).Append(", bez F334 ").Append(_dScaRaw).Append(", swoi koalicji 334c ").Append(Key01(ScaAllyOn)).Append(')');
            string[] ids = { CrownId, ReachId, IronId, NorthId, DragonstoneId };
            foreach (var id in ids)
            {
                var k = FindKingdom(id);
                if (k == null) continue;
                ScaRec r;
                _sca.TryGetValue(id, out r);
                sb.Append(" | [").Append(id).Append("] ").Append(Nm(k)).Append(": wrog ").Append(r != null ? F0(r.Raw) : "?").Append("->").Append(r != null && r.Used ? F0(r.Front) : "-")
                  .Append(", swoi krolestwa ").Append(r != null && r.OwnRaw >= 0f ? F0(r.OwnRaw) : "?").Append("->koalicja ").Append(r != null && r.OwnCoal >= 0f ? F0(r.OwnCoal) : "-")
                  .Append(r != null && r.OwnField ? " (pole 334e)" : "")
              .Append(", ").Append(ScaNoBandits320.Brief(k));
            }
            Log.Info(sb.Append('.').ToString());
        }

        /// <summary>Linia 10: pomiar korony (bezwarunkowy) - rody z lennem: limit / partie / ludzie / pulap 166 / zold / D / gotowosc p9; armie: jedzenie i przyczyna glodu.</summary>
        private static void CrownLine(int today)
        {
            var c = FindKingdom(CrownId);
            if (c == null) return;
            var tier = Campaign.Current.Models.ClanTierModel;
            var sb = new StringBuilder(1500);
            sb.Append(Tag).Append("korona pomiar | dzien ").Append(today).Append(" | rody:");
            int clans = 0, lim = 0, parts = 0, men = 0, unready = 0; double cap = 0, wage = 0;
            var rows = new List<string>();
            foreach (var cl in c.Clans)
            {
                if (cl == null || cl.IsEliminated || cl.Fiefs == null || cl.Fiefs.Count == 0) continue;
                clans++;
                int l = 0, p = 0, m = 0, u = 0, w = 0;
                try { l = tier.GetPartyLimitForTier(cl, cl.Tier); } catch { }
                if (cl.WarPartyComponents != null)
                    foreach (var wp in cl.WarPartyComponents)
                    {
                        var mp = wp != null ? wp.MobileParty : null;
                        if (mp == null || !mp.IsActive) continue;
                        p++; m += mp.MemberRoster != null ? mp.MemberRoster.TotalManCount : 0; w += Math.Max(0, mp.TotalWage);
                        try { if (ArmsFirst.Unready(mp)) u++; } catch { }
                    }
                double cp; bool hasCap = false;
                try { hasCap = ClanBudget.TryCap(cl, out cp); } catch { cp = 0; }
                lim += l; parts += p; men += m; unready += u; wage += w; if (hasCap) cap += cp;
                if (rows.Count < 24) rows.Add(Nm(cl) + " " + p + "/" + l + " ludzi " + m + " pulap " + (hasCap ? F0(cp) : "-") + " zold " + w + " D " + F0(ClanBudget.DOf(cl)) + (u > 0 ? " p9 " + u : ""));
            }
            sb.Append(" razem rodow ").Append(clans).Append(", partii ").Append(parts).Append('/').Append(lim).Append(", ludzi ").Append(men).Append(", pulap 166 ").Append(F0(cap))
              .Append(", zold ").Append(F0(wage)).Append(", niegotowych p9 ").Append(unready).Append(" [").Append(string.Join("; ", rows.ToArray())).Append(']');
            sb.Append(" | armie:");
            int an = 0;
            try
            {
                foreach (var a in c.Armies)
                {
                    var lp = a != null ? a.LeaderParty : null;
                    if (lp == null) continue;
                    an++;
                    float fd = ReaverLaw.FoodDays(lp, 0);
                    string why = "-";
                    if (lp.Party != null && lp.Party.IsStarving)
                    {
                        var town = NearestTown(lp.GetPosition2D);
                        why = "glod: najblizsze miasto " + Nm(town) + " jedzenie na rynku " + (town != null && town.ItemRoster != null ? town.ItemRoster.TotalFood.ToString(Inv) : "-")
                              + ", zloto wodza " + (lp.LeaderHero != null ? lp.LeaderHero.Gold.ToString(Inv) : "-");
                    }
                    sb.Append(' ').Append(Nm(lp)).Append(" (partii ").Append(a.Parties.Count).Append(", jedzenie ").Append(fd >= 999f ? "-" : fd.ToString("0.0", Inv)).Append(" dni, ").Append(why).Append(");");
                }
            }
            catch (Exception e) { Stumble("CrownLine.armies", e); }
            if (an == 0) sb.Append(" brak");
            // B334-8: Tywin (lord_4_1) w polu - druzyna nie w osadzie i nie w lochu
            try
            {
                if (_tywin == null) foreach (var h in Hero.AllAliveHeroes) if (h != null && h.StringId == "lord_4_1") { _tywin = h; break; }
                var ty = _tywin;
                if (ty == null) sb.Append(" | Tywin: brak");
                else
                {
                    var tp = ty.PartyBelongedTo;
                    bool field = ty.IsAlive && !ty.IsPrisoner && tp != null && tp.CurrentSettlement == null;
                    sb.Append(" | Tywin: w polu ").Append(field ? "TAK" : "NIE").Append(" (").Append(!ty.IsAlive ? "nie zyje" : ty.IsPrisoner ? "w niewoli" : tp == null ? "bez druzyny"
                      : tp.CurrentSettlement != null ? "w " + Nm(tp.CurrentSettlement) : (tp.Army != null ? "armia " + (tp.Army.LeaderParty == tp ? "wodz" : "czlonek") : "druzyna")).Append(')');
                    // 334c (B334-8): "na wojnie" = zywy, wolny, z druzyna, w armii albo w zasiegu frontu Polnocy (F334: 8.02 doby drogi) - samo krazenie wokol Lannisportu nie
                    var nk = FindKingdom(NorthId);
                    bool war = ty.IsAlive && !ty.IsPrisoner && tp != null && (tp.Army != null || (nk != null && Reaches(tp, nk)));
                    sb.Append(", na wojnie ").Append(war ? "TAK" : "NIE");
                }
            }
            catch (Exception e) { Stumble("CrownLine.tywin", e); }
            Log.Info(sb.Append('.').ToString());
        }

        private static Settlement NearestTown(Vec2 pos)
        {
            Settlement best = null; float bd = float.MaxValue;
            foreach (var t in Town.AllTowns) { var st = t != null ? t.Settlement : null; if (st == null) continue; float d = pos.DistanceSquared(st.GetPosition2D); if (d < bd) { bd = d; best = st; } }
            return best;
        }

        /// <summary>Linia 11: kontrola - 334 nie tworzy zlota ani ludzi (przeplywy tylko istniejacymi drogami gry); potkniecia doby.</summary>
        private static void ControlLine(int today)
        {
            Log.Info(Tag + "kontrola | dzien " + today + " | zloto utworzone 0, ludzie utworzeni 0 (przeplywy: skarbiec -> kontrakt 185 kompanii " + _dHireN + " najmow, kiesa rodu -> naprawy statkow "
                     + _dRepairs + " (" + _dRepairGold + " zl) i jedzenie z rynku (gra przy wizycie w porcie, do portu " + _dToPort + ")) | potkniecia " + _dStumbles + " (sesja " + _stumbles + ").");
        }

        // ================================================================ zapis (arm_f334)
        private static int _hiredN = 0, _lastHireDay = -1, _preTruceParties = -1, _preTruceMen = -1, _preTruceDay = -1;
        private static long _hireGoldK;
        private static readonly HashSet<string> _northHeld = new HashSet<string>(StringComparer.Ordinal);
        private static Hero _tywin;

        private static void ResetSaved()
        {
            _hiredN = 0; _lastHireDay = -1; _hireGoldK = 0; _preTruceParties = _preTruceMen = _preTruceDay = -1; _northHeld.Clear(); _tywin = null;
        }

        /// <summary>v1|najmow|ostatni najem|suma K|partii korony przed rozejmem|ludzi|doba|osady Polnocy (id,..)</summary>
        internal static string Export()
        {
            var sb = new StringBuilder(256);
            sb.Append("v1|").Append(_hiredN).Append('|').Append(_lastHireDay).Append('|').Append(_hireGoldK).Append('|').Append(_preTruceParties).Append('|').Append(_preTruceMen)
              .Append('|').Append(_preTruceDay).Append('|');
            bool first = true;
            foreach (var id in _northHeld) { if (!first) sb.Append(','); first = false; sb.Append(id); }
            return sb.ToString();
        }

        private static int I(string[] p, int i, int def) { int v; return i < p.Length && int.TryParse(p[i], NumberStyles.Integer, Inv, out v) ? v : def; }

        internal static void Import(string v)
        {
            try
            {
                if (string.IsNullOrEmpty(v)) return;
                var p = v.Split('|');
                if (p.Length < 8 || p[0] != "v1") return;
                _hiredN = I(p, 1, 0); _lastHireDay = I(p, 2, -1);
                long g; _hireGoldK = long.TryParse(p[3], NumberStyles.Integer, Inv, out g) ? g : 0;
                _preTruceParties = I(p, 4, -1); _preTruceMen = I(p, 5, -1); _preTruceDay = I(p, 6, -1);
                _northHeld.Clear();
                if (p[7].Length > 0) foreach (var id in p[7].Split(',')) if (id.Length > 0) _northHeld.Add(id);
            }
            catch (Exception e) { Stumble("Import", e); }
        }

        // ================================================================ B6: rozejm uzbrojony

        /// <summary>B6: frakcja Polnocy albo korony w rozejmie 330 (rozejm trwa) - budzet 166 i W5 licza ja jako w wojnie.</summary>
        internal static bool ArmedTruce(IFaction f)
        {
            try
            {
                if (f == null || !TruceArmedOn || !WarTruce.KeyOn || !WarTruce.TruceHolds) return false;
                var id = f.StringId;
                return id == NorthId || id == CrownId;
            }
            catch { return false; }
        }

        /// <summary>Linia 7 (w rozejmie) i stan sprzed rozejmu (poza rozejmem - partie i ludzie korony z wczoraj).</summary>
        private static void TruceDaily(int today, bool log)
        {
            var c = FindKingdom(CrownId);
            if (c == null) return;
            int parts = 0, men = 0, warClans = 0, clans = 0;
            foreach (var cl in c.Clans)
            {
                if (cl == null || cl.IsEliminated) continue;
                if (cl.WarPartyComponents != null)
                    foreach (var wp in cl.WarPartyComponents)
                    {
                        var mp = wp != null ? wp.MobileParty : null;
                        if (mp == null || !mp.IsActive) continue;
                        parts++; men += mp.MemberRoster != null ? mp.MemberRoster.TotalManCount : 0;
                    }
                if (cl.Fiefs != null && cl.Fiefs.Count > 0) { clans++; try { if (ClanBudget.AtWarPublic(cl)) warClans++; } catch { } }
            }
            bool truce = false;
            try { truce = WarTruce.KeyOn && WarTruce.TruceHolds; } catch { }
            if (!truce) { _preTruceParties = parts; _preTruceMen = men; _preTruceDay = today; return; }
            if (!log) return;
            double share = _preTruceParties > 0 ? (double)parts / _preTruceParties : -1;
            Log.Info(Tag + "rozejm uzbrojony | dzien " + today + " | klucz " + Key01(TruceArmedOn) + " | rody korony z lennem w budzecie wojny " + warClans + " z " + clans
                     + " | partie korony dzis " + parts + " vs przed rozejmem " + _preTruceParties + " (d." + _preTruceDay + ", " + (share >= 0 ? (share * 100).ToString("0", Inv) + "%" : "-")
                     + "), ludzie " + men + " vs " + _preTruceMen + ".");
        }

        /// <summary>B7 (po recenzji 334): osady do odbicia - osady (twierdze i wsie) w rekach Polnocy o kulturze SPOZA teatru, ktore z danych ROT naleza do Polnocy
        /// (KingInTheNorth.OrigOwnerOf - pan poczatkowy z Polnocy). Tylko takie maja weto teatru i tylko dla nich wyjatek "odbicie" cos zmienia; zdobyczy w Crownlands,
        /// Reach, Stormlands i na Wyspach lista nie bierze (zwraca je 326b). Wpisy w teatrze albo spoza ziem Polnocy (stary zapis v1) - usuwane.</summary>
        private static void NorthHeldDaily()
        {
            var n = FindKingdom(NorthId);
            if (n == null) return;
            if (_northHeld.Count > 0)
            {
                var drop = new List<string>();
                foreach (var id in _northHeld) { var st = Settlement.Find(id); if (st == null || InNorthTheatre(CultureOf(st)) || !OrigNorth(st)) drop.Add(id); }
                foreach (var id in drop) _northHeld.Remove(id);
            }
            foreach (var st in n.Settlements)
            {
                if (st == null || st.StringId == null || !(st.IsFortification || st.IsVillage)) continue;
                if (InNorthTheatre(CultureOf(st)) || !OrigNorth(st)) continue;
                _northHeld.Add(st.StringId);
            }
        }

        private static bool OrigNorth(Settlement st)
        {
            try
            {
                var b = st.IsVillage && st.Village != null && st.Village.Bound != null ? st.Village.Bound : st;
                string ok;
                KingInTheNorth.OrigOwnerOf(b.StringId, out ok);
                return ok == NorthId;
            }
            catch { return false; }
        }
    }

    /// <summary>334: zapis arm_f334, zdarzenia (najazdy i oblezenia Wysp, wejscia do portu), start sesji.</summary>
    internal sealed class WarFront334Behavior : CampaignBehaviorBase
    {
        public WarFront334Behavior() { WarFront334.Reset(); }

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, _ => WarFront334.SessionStart());
            CampaignEvents.VillageLooted.AddNonSerializedListener(this, WarFront334.OnVillageLooted);
            CampaignEvents.OnSiegeEventStartedEvent.AddNonSerializedListener(this, WarFront334.OnSiegeStarted);
            CampaignEvents.OnSiegeEventStartedEvent.AddNonSerializedListener(this, LocalFront334e.OnSiegeStarted);   // 334e: q przy starcie oblezenia
            CampaignEvents.MapEventStarted.AddNonSerializedListener(this, LocalFront334e.OnMapEventStarted);         // 334e: q przy starcie rabunku
            CampaignEvents.SettlementEntered.AddNonSerializedListener(this, WarFront334.OnSettlementEntered);
            CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, WarFront334.OnOwnerChanged);
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, WarFront334Test.Hourly);
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, LocalFront334e.Hourly);   // 334e: migawka po wczytaniu (watek glowny), postawa nowych druzyn
            CampaignEvents.TickEvent.AddNonSerializedListener(this, NavGuard334.Tick);          // 334c: naprawa partii z kolejki straznika pogody (watek glowny; pusta kolejka - jedno porownanie)
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, NavGuard334.Daily);    // 334c: linia "Pozycje partii (334c)" (kazda kampania)
        }

        public override void SyncData(IDataStore dataStore)
        {
            try
            {
                string v = dataStore.IsSaving ? WarFront334.Export() : null;
                SaveText.Sync(dataStore, "arm_f334", ref v);
                if (dataStore.IsLoading) WarFront334.Import(v);
            }
            catch (Exception e) { Log.Error("SyncData.arm_f334", e); }
        }
    }
}
