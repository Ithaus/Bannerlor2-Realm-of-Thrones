using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// 334e - MYSL GODZINOWA. JEDNO wpiecie: SiegeWalls2.ThinkPost (postfiks Last CampaignEventDispatcher.AiHourlyTick - po wetach teatru (48), 274 (50), ArmsFirst p9
    /// i OB5; dlatego nasze NOWE opcje (armie lupia, Zelazni) same sprawdzaja weta: WarFront334.VetoedFor334e = teatr, 274, 326b). Obozy: ThinkPost -> Judge (przed F2
    /// i m116); poza obozem: ThinkPost -> ThinkField. Bez wlasnego postfiksu (jedno ocenianie oblezenia na godzine).
    ///  - OBLEZENIE W TOKU (klucz LocalSiegeJudge334e, rozdz. 4): wodz obozu (i samodzielna druzyna-wodz), nie gracz, nie szturm (F2), nie morze, nie skrypt, nie Inni.
    ///    Oboz = sila obozu + nasi idacy na te osade, ktorzy dojda przed przewaga wroga; odsiecz = wrogowie NADCHODZACY w horyzoncie 12.03 doby (zamiar OBRONY tej osady
    ///    albo w zasiegu wzroku obozu i blizej niz godzine temu; trzecia strona - nie), zaloga = zaloga + milicja + lordowie w osadzie (do ODSTAP najwyzej 0.8 x oboz).
    ///    Pozostaly czas oblezenia z postepu w grze: przygotowania (SiegePreparations.Progress) i tempo burzenia murow od konca przygotowan (model ostrzalu gry + mury
    ///    SiegeWalls2); bez danych - srednia gry 8.02 doby od startu. Macierz JudgeRule: KONTYNUUJ (q &lt;= 0.8 i jedzenie &gt;= 3 doby, albo przewaga odsieczy dojdzie
    ///    po zdobyciu) - wpis oblezenia = max(1.05 x najlepszy + 0.01, 0.5), chyba ze najlepszy to obrona wlasnej twierdzy (OB5), wpis zawetowany / rozejm (&lt;= 0),
    ///    wodz chroniony 335; SRODEK - gra; ODSTAP (przewaga dojdzie przed koncem, 2 kolejne godziny) - tylko gdy ucieczka zdazy (droga do wlasnej twierdzy dalej od
    ///    odsieczy krotsza niz przyjazd przewagi): oblezenie -1, GoToSettlement, flaga odwrotu 24 h (BK stick i m116 Hold jej nie trzymaja), blokada celu 1 doba.
    ///    Ucieczka nie zdazy - zostaje w obozie (bitwa przy obozie z wypadem zalogi lepsza niz poscig w polu).
    ///  - RABUNEK W TOKU (klucz LocalRaidBreakOff334e, rozdz. 4.3): rabujacy wodz - wrogowie nadchodzacy w pozostalym czasie rabunku silniejsi niz 1.25 x my, 2 godziny
    ///    - wpis rabunku tej wsi -1 (gra konczy rabunek, ucieczke prowadzi 223), blokada wsi 1 doba.
    ///  - ARMIE LUPIA (.Score.cs ArmyRaid) i ZELAZNI OBLEGAJA WYBRZEZE (ponizej) - tylko gdy mysl nie ma oblezenia z wynikiem &gt; 0.
    /// </summary>
    internal static partial class LocalFront334e
    {
        private sealed class CampRec
        {
            public int Hour = -1, Bad, State = -1, SaidDay = -1, StaySaid = -1; public double Q; public double PrepH = -1, Wall0 = 1;
            public readonly Dictionary<MobileParty, float> Dist = new Dictionary<MobileParty, float>();
        }
        private struct PS : IEquatable<PS>
        {
            public readonly MobileParty P; public readonly Settlement S;
            public PS(MobileParty p, Settlement s) { P = p; S = s; }
            public bool Equals(PS o) { return ReferenceEquals(P, o.P) && ReferenceEquals(S, o.S); }
            public override bool Equals(object o) { return o is PS && Equals((PS)o); }
            public override int GetHashCode() { unchecked { return ((P != null ? P.GetHashCode() : 0) * 397) ^ (S != null ? S.GetHashCode() : 0); } }
        }
        private static readonly Dictionary<SiegeEvent, CampRec> _camps = new Dictionary<SiegeEvent, CampRec>();
        private static readonly Dictionary<MobileParty, double> _retreat = new Dictionary<MobileParty, double>();   // flaga odwrotu do godziny
        private static readonly Dictionary<PS, double> _block = new Dictionary<PS, double>();                    // (druzyna, osada) -> doba konca
        private static readonly Dictionary<MapEvent, int[]> _raidBad = new Dictionary<MapEvent, int[]>();        // [godzina, kolejne zle]
        private static int _dJudge, _dKeep, _dMid, _dRetreat, _dRetreatCand, _dStay, _dKeepVeto, _dBreak, _dStickYield, _dFreshYield, _dIronOffers, _dIronVeto, _dSiegeLines;
        private static readonly int[] _dStates = new int[6];
        private static readonly List<string> _dRetreatNotes = new List<string>();
        private static int _sRetreat;

        private static void ResetSiege() { _camps.Clear(); _retreat.Clear(); _block.Clear(); _raidBad.Clear(); _sRetreat = 0; ZeroSiegeDay(); }
        private static void ZeroSiegeDay()
        {
            _dJudge = _dKeep = _dMid = _dRetreat = _dRetreatCand = _dStay = _dKeepVeto = _dBreak = _dStickYield = _dFreshYield = _dIronOffers = _dIronVeto = _dSiegeLines = 0;
            Array.Clear(_dStates, 0, _dStates.Length); _dRetreatNotes.Clear();
        }

        /// <summary>Flaga odwrotu (BK stick, m116 Hold): druzyna albo wodz jej armii odstepuje od oblezenia.</summary>
        internal static bool Retreating(MobileParty p)
        {
            if (p == null) return false;
            lock (_lk)
            {
                if (_retreat.Count == 0) return false;
                double until;
                double now = CampaignTime.Now.ToHours;
                if (_retreat.TryGetValue(p, out until) && until > now) return true;
                var a = p.Army;
                return a != null && a.LeaderParty != null && _retreat.TryGetValue(a.LeaderParty, out until) && until > now;
            }
        }

        /// <summary>Blokada celu po odwrocie / przerwanym rabunku (druzyna albo wodz armii). Wolajacy trzyma _lk.</summary>
        private static bool Blocked(MobileParty p, Settlement t)
        {
            if (_block.Count == 0 || p == null || t == null) return false;
            double until, now = CampaignTime.Now.ToDays;
            if (_block.TryGetValue(new PS(p, t), out until) && until > now) return true;
            var a = p.Army;
            return a != null && a.LeaderParty != null && _block.TryGetValue(new PS(a.LeaderParty, t), out until) && until > now;
        }

        /// <summary>Prefiks First BannerKings ArmySiegePersistencePatches.Postfix(MobileParty): odwrot 334e - BK nie trzyma armii pod oblezeniem. Nie rzuca.</summary>
        public static bool BkStickPre(MobileParty __0)
        {
            try
            {
                if (__0 == null || !StickYieldOn || !Retreating(__0)) return true;
                lock (_lk) _dStickYield++;
                return false;
            }
            catch { return true; }
        }

        /// <summary>SiegeWalls2.HoldFresh (m116): odwrot 334e nie jest trzymany jako swieze oblezenie.</summary>
        internal static bool FreshYields(MobileParty p)
        {
            if (!JudgeOn || !Retreating(p)) return false;
            lock (_lk) _dFreshYield++;
            return true;
        }

        // ================================================================ poza obozem (z SiegeWalls2.ThinkPost, Last)
        /// <summary>SiegeWalls2.ThinkPost dla druzyn poza obozem oblezenia: rabunek przerwany, armie lupia, Zelazni oblegaja wybrzeze. Nie rzuca.</summary>
        internal static void ThinkField(MobileParty party, PartyThinkParams partyThinkParams)
        {
            try
            {
                if (party == null || partyThinkParams == null || party.IsMainParty) return;
                if (!party.IsLordParty || !(party.MapFaction is Kingdom)) return;
                if (!PowerOn && !BreakOn) return;
                if (WithPlayer(party) || partyThinkParams.DoNotChangeBehavior) return;
                if (party.BesiegerCamp != null) return;
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var me = party.MapEvent;
                if (me != null) { if (BreakOn && me.IsRaid && me.AttackerSide != null && me.AttackerSide.LeaderParty == party.Party) RaidBreak(party, me, partyThinkParams); }
                else
                {
                    if (ArmyRaidOn && party.Army != null && party.Army.LeaderParty == party) ArmyRaid(party, partyThinkParams);
                    if (PowerOn && WarFront334.IronOn && party.MapFaction.StringId == WarFront334.IronId) IronSiege(party, partyThinkParams);
                }
                double ms = sw.Elapsed.TotalMilliseconds;
                if (ms > 0.05) lock (_lk) Cost(ms);
            }
            catch (Exception e) { Stumble("ThinkField", e); }
        }

        private static int Best(PartyThinkParams pt, out float best)
        {
            best = float.MinValue; int bi = -1;
            var s = pt.AIBehaviorScores;
            for (int i = 0; i < s.Count; i++) if (s[i].Item2 > best) { best = s[i].Item2; bi = i; }
            return bi;
        }

        private static float PStr(PartyBase pb) { try { return pb != null ? pb.EstimatedStrength : 0f; } catch { return 0f; } }

        /// <summary>Pozostaly czas oblezenia (doby) z postepu w grze. Wolajacy trzyma _lk.</summary>
        private static float TimeLeft(BesiegerCamp camp, SiegeEvent ev, Settlement st, CampRec r)
        {
            double el = ev.SiegeStartTime.ElapsedDaysUntilNow;
            float avg = Math.Max(1f, SiegeDays - (float)el);
            bool prepared = false;
            try { prepared = camp.IsPreparationComplete; } catch { }
            double wall = 1.0;
            try { wall = SiegeWalls.WallHpMin(st); } catch { }
            if (!prepared)
            {
                float p = 0f;
                try { var se = camp.SiegeEngines; if (se != null && se.SiegePreparations != null) p = se.SiegePreparations.Progress; } catch { }
                if (p > 0.05f && el > 0.04) return (float)Math.Min(HorizonDays, Math.Max(avg, el * (1.0 - p) / p + 1.0));   // reszta przygotowan + mury (co najmniej doba)
                return avg;
            }
            if (wall <= 0.0) return 0.5f;   // wyrwa - szturm najblizszy
            double nowH = CampaignTime.Now.ToHours;
            if (r.PrepH < 0) { r.PrepH = nowH; r.Wall0 = wall; }
            double wEl = (nowH - r.PrepH) / CampaignTime.HoursInDay, lost = r.Wall0 - wall;
            if (lost > 0.02 && wEl >= 0.25) return (float)Math.Max(0.5, Math.Min(HorizonDays, wEl * wall / lost));   // tempo burzenia murow (ostrzal gry)
            return avg;
        }

        /// <summary>Z SiegeWalls2.ThinkPost (Last): sad oblezenia w toku. Zwraca stan (JudgeRule) albo -1 (bez sadu); 4 - TYLKO gdy odwrot wykonany teraz
        /// (m116 Fresh wtedy nie trzyma). Nie rzuca.</summary>
        internal static int Judge(MobileParty party, BesiegerCamp camp, SiegeEvent ev, Settlement st, PartyThinkParams pt, bool skip)
        {
            try
            {
                if (skip || party == null || camp == null || ev == null || st == null || pt == null) return -1;
                if (!JudgeOn || party.IsMainParty || WithPlayer(party) || pt.DoNotChangeBehavior) return -1;
                if (!party.IsLordParty || !(party.MapFaction is Kingdom)) return -1;
                if (party.AttachedTo != null || (party.Army != null && party.Army.LeaderParty != party)) return -1;
                if (camp.LeaderParty != party) return -1;   // decyduje wodz obozu
                if (!st.IsFortification || st.Town == null) return -1;
                if (party.MapEvent != null || party.IsCurrentlyAtSea) return -1;
                try { if (party.Ai != null && party.Ai.DoNotMakeNewDecisions) return -1; } catch { }
                try { if (Undead.Party(party)) return -1; } catch { }
                var sw = System.Diagnostics.Stopwatch.StartNew();
                int res = JudgeCore(party, camp, ev, st, pt);
                lock (_lk) Cost(sw.Elapsed.TotalMilliseconds);
                return res;
            }
            catch (Exception e) { Stumble("Judge", e); return -1; }
        }

        private static int JudgeCore(MobileParty party, BesiegerCamp camp, SiegeEvent ev, Settlement st, PartyThinkParams pt)
        {
            var k = party.MapFaction;
            double camp0 = 0;
            foreach (var pb in camp.GetInvolvedPartiesForEventType(MapEvent.BattleTypes.Siege)) camp0 += PStr(pb);
            double gar = 0;
            foreach (var pb in st.GetInvolvedPartiesForEventType(MapEvent.BattleTypes.Siege)) gar += PStr(pb);
            float food = SiegeWalls2.FoodDays(party);
            int state, bad; double q, relief = 0, campS = camp0, allyIn = 0; float arrival = float.MaxValue, tLeft; int nGo = 0, nScout = 0;
            lock (_lk)
            {
                if (!SnapFresh) return -1;
                _dJudge++;
                CampRec r;
                if (!_camps.TryGetValue(ev, out r)) { r = new CampRec(); _camps[ev] = r; }
                tLeft = TimeLeft(camp, ev, st, r);
                var l = ListOf(st);
                var sc = ScoutOf(party);
                var rel = new List<KeyValuePair<float, float>>();
                var allies = new List<KeyValuePair<float, float>>();
                var seen = new HashSet<MobileParty>();
                var newDist = new Dictionary<MobileParty, float>();
                Action<MobileParty> consider = lead =>
                {
                    if (lead == null || !seen.Add(lead) || !lead.IsActive || lead == party) return;
                    if (lead.CurrentSettlement == st || lead.BesiegerCamp == camp) return;
                    var lf = lead.MapFaction;
                    if (lf == null) return;
                    if (!FactionManager.IsAtWarAgainstFaction(k, lf))
                    {
                        // nasi w drodze na te osade (dolacza do obozu)
                        if (!SameSide(k, lf) || !GoingTo(lead, st)) return;
                        var ua = LiveUnit(lead);
                        if (ua == null) return;
                        float da = DaysTo(ua, st);
                        if (da >= 0f && da <= HorizonDays) allies.Add(new KeyValuePair<float, float>(da, ua.Str));
                        return;
                    }
                    if (!Foe(k, lf, st)) return;   // trzecia strona - nie odsiecz
                    bool intent = Relieves(lead, st);
                    bool spotted = sc.Contains(lead);
                    if (!intent && !spotted) return;
                    var u = LiveUnit(lead);
                    if (u == null) return;
                    float dd = u.Pos.Distance(st.GetPosition2D);
                    newDist[lead] = dd;
                    if (!intent)
                    {
                        float prev;
                        if (!r.Dist.TryGetValue(lead, out prev) || !(dd < prev - 0.01f)) return;   // stoi albo sie oddala - nie wywoluje odwrotu
                    }
                    float d = DaysTo(u, st);
                    if (d < 0f || d > HorizonDays) return;
                    rel.Add(new KeyValuePair<float, float>(d, u.Str));
                    if (intent) nGo++; else nScout++;
                };
                for (int i = 0; i < l.E.Count; i++) consider(l.E[i].U.Lead);
                foreach (var m in sc) consider(m);
                r.Dist.Clear(); foreach (var kv in newDist) r.Dist[kv.Key] = kv.Value;
                rel.Sort((a, b) => a.Key.CompareTo(b.Key));
                foreach (var kv in rel) relief += kv.Value;
                // nasi w drodze: doliczeni, jesli dojda przed przewaga wroga (bez przewagi - przed koncem oblezenia)
                float a0 = Arrival(rel, GarCap(gar, camp0), camp0);
                float lim = a0 == float.MaxValue ? tLeft : a0;
                foreach (var kv in allies) if (kv.Key <= lim) allyIn += kv.Value;
                campS = camp0 + allyIn;
                arrival = Arrival(rel, GarCap(gar, campS), campS);
                state = JudgeRule(campS, relief, gar, arrival, tLeft, food, out q);
                int h = NowH;
                if (state == 4) { if (r.Hour == h - 1) r.Bad++; else if (r.Hour != h) r.Bad = 1; else if (r.Bad == 0) r.Bad = 1; }
                else r.Bad = 0;
                bad = r.Bad;
                r.Hour = h; r.Q = q;
                if (state >= 0 && state < _dStates.Length) _dStates[state]++;
                bool change = r.State != state;
                r.State = state;
                if ((change || r.SaidDay != Today) && S != null && S.LogEnabled && _dSiegeLines < 60)
                {
                    r.SaidDay = Today; _dSiegeLines++;
                    Log.Info(Tag + "oblezenie | dzien " + Today + " | " + st.StringId + " (" + Nm(st) + ") | " + Nm(party) + " (" + Id(k) + ") | oboz " + F0(campS) + (allyIn > 0 ? " (w drodze " + F0(allyIn) + ")" : "")
                             + " | odsiecz " + F0(relief) + " (idzie " + nGo + ", zwiad " + nScout + ", zaloga " + F0(gar) + ") | q_obl " + F2(q) + " | przyjazd " + (arrival == float.MaxValue ? "-" : F2(arrival))
                             + " | pozostalo " + F2(tLeft) + " | jedzenie " + (food >= 999f ? "-" : F2(food)) + " | stan " + StateName[state] + (state == 4 ? " (" + bad + "/2 h)" : "") + ".");
                }
            }
            var scores = pt.AIBehaviorScores;
            if (scores == null) return state;
            float best; int bi = Best(pt, out best);
            int si = -1;
            for (int i = 0; i < scores.Count; i++)
            {
                var e = scores[i];
                if (e.Item1.AiBehavior == AiBehavior.BesiegeSettlement && !e.Item1.WillGatherArmy && ReferenceEquals(e.Item1.Party, st) && (si < 0 || e.Item2 > scores[si].Item2)) si = i;
            }
            if (state == 1 || state == 3)
            {
                try { if (LordEscort.Guard335Busy(party)) return state; } catch { }   // wodz chroniony 335 - decyduje ochrona
                if (si >= 0 && scores[si].Item2 <= 0f) { lock (_lk) _dKeepVeto++; return state; }   // weto (teatr, 274, 326b, zakaz m116) albo rozejm - nie wskrzeszamy
                if (si < 0)
                {
                    string why;
                    var kk = k as Kingdom;
                    if (st.MapFaction == null || !FactionManager.IsAtWarAgainstFaction(k, st.MapFaction) || (kk != null && WarFront334.VetoedFor334e(kk, st, out why)))
                    { lock (_lk) _dKeepVeto++; return state; }
                }
                if (bi >= 0)
                {
                    var bd = scores[bi].Item1;
                    if (bd.AiBehavior == AiBehavior.DefendSettlement) { var ds = bd.Party as Settlement; if (ds != null && ds.IsFortification && ds.MapFaction == party.MapFaction) return state; }   // OB5 pierwsza
                    if (ReferenceEquals(bd.Party, st) && bd.AiBehavior == AiBehavior.BesiegeSettlement && best >= HoldFloor) { lock (_lk) _dKeep++; return state; }
                }
                float v = Math.Max((bi >= 0 ? best : 0f) * 1.05f + 0.01f, HoldFloor);
                if (si >= 0) { var d = scores[si].Item1; pt.SetBehaviorScore(in d, v); }
                else pt.AddBehaviorScore((new AIBehaviorData(st, AiBehavior.BesiegeSettlement, MobileParty.NavigationType.Default, false, false, false), v));
                lock (_lk) _dKeep++;
                return state;
            }
            if (state == 2) { lock (_lk) _dMid++; return 2; }
            if (state != 4) return state;
            lock (_lk) _dRetreatCand++;
            if (bad < 2) return 2;   // kandydat - 2 kolejne godziny
            return Retreat(party, camp, st, pt, best, q, relief, gar, campS, arrival) ? 4 : 2;
        }

        /// <summary>Odstapienie: tylko gdy ucieczka zdazy przed przewaga. true - wykonane.</summary>
        private static bool Retreat(MobileParty party, BesiegerCamp camp, Settlement st, PartyThinkParams pt, float best, double q, double relief, double gar, double campS, float arrival)
        {
            // dokad: wlasna nieoblegana twierdza dalej od odsieczy (najblizszy nadchodzacy wrog) niz oboz, do ktorej dojdziemy przed przyjazdem przewagi
            Vec2 cp = party.GetPosition2D;
            Vec2 foe = st.GetPosition2D; float foeD = float.MaxValue;
            var cands = new List<KeyValuePair<Settlement, float>>();
            lock (_lk)
            {
                CampRec r;
                if (_camps.TryGetValue(camp.SiegeEvent, out r))
                    foreach (var kv in r.Dist) { var u = LiveUnit(kv.Key); if (u != null && kv.Value < foeD) { foeD = kv.Value; foe = u.Pos; } }
            }
            float campFoe = cp.Distance(foe);
            foreach (var s in party.MapFaction.Settlements)
            {
                if (s == null || !s.IsFortification || s.IsUnderSiege) continue;
                var sp = s.GetPosition2D;
                if (sp.Distance(foe) <= campFoe) continue;
                cands.Add(new KeyValuePair<Settlement, float>(s, sp.DistanceSquared(cp)));
            }
            cands.Sort((a, b) => a.Value.CompareTo(b.Value));
            Settlement dest = null; float destDays = -1f;
            lock (_lk)
            {
                for (int i = 0; i < cands.Count && i < 6; i++)
                {
                    float our = OurDays(party, cands[i].Key);
                    if (our >= 0f && our < arrival) { dest = cands[i].Key; destDays = our; break; }
                }
            }
            MobileParty.NavigationType nav = MobileParty.NavigationType.None; float dist; bool fromPort = false;
            if (dest != null)
            {
                Helpers.AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty(party, dest, false, out nav, out dist, out fromPort);
                if (nav == MobileParty.NavigationType.None) dest = null;
            }
            // bez twierdzy, do ktorej zdazymy: odstepujemy tylko przy zapasie co najmniej pol doby (223 prowadzi ucieczke w pole); inaczej zostajemy w obozie
            bool go = dest != null || arrival >= 0.5f;
            if (!go)
            {
                lock (_lk)
                {
                    _dStay++;
                    CampRec r;
                    if (_camps.TryGetValue(camp.SiegeEvent, out r) && r.StaySaid != Today && S != null && S.LogEnabled)
                    {
                        r.StaySaid = Today;
                        Log.Info(Tag + "zostaje w obozie | dzien " + Today + " | " + st.StringId + " (" + Nm(st) + ") | " + Nm(party) + " (" + Id(party.MapFaction) + ") | oboz " + F0(campS) + " vs odsiecz "
                                 + F0(relief) + "+zaloga " + F0(gar) + " (q_obl " + F2(q) + ", przyjazd " + (arrival == float.MaxValue ? "-" : F2(arrival)) + ") | ucieczka nie zdazy - bitwa przy obozie.");
                    }
                }
                return false;
            }
            var scores = pt.AIBehaviorScores;
            for (int i = 0; i < scores.Count; i++)
            {
                var e = scores[i];
                if ((e.Item1.AiBehavior == AiBehavior.BesiegeSettlement || e.Item1.AiBehavior == AiBehavior.AssaultSettlement) && ReferenceEquals(e.Item1.Party, st) && e.Item2 > -1f)
                { var d = e.Item1; pt.SetBehaviorScore(in d, -1f); }
            }
            if (dest != null)
            {
                float v = Math.Max(best, 0.01f) * 1.05f;
                pt.AddBehaviorScore((new AIBehaviorData(dest, AiBehavior.GoToSettlement, nav, false, fromPort, false), v));
            }
            lock (_lk)
            {
                double now = CampaignTime.Now.ToHours;
                _retreat[party] = now + 24.0;
                _block[new PS(party, st)] = CampaignTime.Now.ToDays + 1.0;
                _dRetreat++;
                _sRetreat++;
                if (_dRetreatNotes.Count < 6) _dRetreatNotes.Add(Nm(st) + " q " + F2(q));
                if (S != null && S.LogEnabled)
                    Log.Info(Tag + "odstapienie | dzien " + Today + " | " + st.StringId + " (" + Nm(st) + ") | " + Nm(party) + " (" + Id(party.MapFaction) + ") | oboz " + F0(campS) + " vs odsiecz "
                             + F0(relief) + "+zaloga " + F0(gar) + " (q_obl " + F2(q) + ", przyjazd " + (arrival == float.MaxValue ? "-" : F2(arrival)) + ") | dokad " + (dest != null ? dest.StringId + " (" + Nm(dest) + ", " + F2(destDays) + " doby)" : "- (223)")
                             + " | ucieczka zdazy " + (dest != null ? "TAK" : "pole") + " | blokada do doby " + F2(CampaignTime.Now.ToDays + 1.0) + ".");
            }
            return true;
        }

        /// <summary>Rabunek w toku: przerwanie przed silniejszym, ktory zdazy (rozdz. 4.3).</summary>
        private static void RaidBreak(MobileParty party, MapEvent me, PartyThinkParams pt)
        {
            var v = me.MapEventSettlement;
            if (v == null || !v.IsVillage || party.IsCurrentlyAtSea) return;
            int men = 0; double ours = 0;
            foreach (var mep in me.AttackerSide.Parties) { if (mep == null || mep.Party == null) continue; ours += PStr(mep.Party); men += mep.Party.NumberOfHealthyMembers; }
            float left = RaidDays(v.SettlementHitPoints, men);
            double z; float first; double ally;
            int cnt;
            lock (_lk)
            {
                if (!SnapFresh) return;
                z = ZFor(v, party.MapFaction, party, left, out first, out ally);
                double qq = Ratio(z, ours + ally);
                bool bad = qq > AbandonQ;
                int[] r;
                if (!_raidBad.TryGetValue(me, out r)) { r = new int[] { -1, 0 }; _raidBad[me] = r; }
                int h = NowH;
                if (!bad) r[1] = 0;
                else if (r[0] == h - 1) r[1]++;
                else if (r[0] != h || r[1] == 0) r[1] = 1;
                r[0] = h; cnt = r[1];
            }
            double q = Ratio(z, ours + ally);
            if (cnt < 2) return;
            var scores = pt.AIBehaviorScores;
            for (int i = 0; i < scores.Count; i++)
            {
                var e = scores[i];
                if (e.Item1.AiBehavior == AiBehavior.RaidSettlement && ReferenceEquals(e.Item1.Party, v) && e.Item2 > -1f) { var d = e.Item1; pt.SetBehaviorScore(in d, -1f); }
            }
            lock (_lk)
            {
                _dBreak++;
                _block[new PS(party, v)] = CampaignTime.Now.ToDays + 1.0;
                if (S != null && S.LogEnabled && _dBreak <= 30)
                    Log.Info(Tag + "rabunek przerwany | dzien " + Today + " | " + v.StringId + " (" + Nm(v) + ") | " + Nm(party) + " (" + Id(party.MapFaction) + ") | nasi " + F0(ours) + " | wrog " + F0(z)
                             + " | q " + F2(q) + " | przyjazd " + (first == float.MaxValue ? "-" : F2(first)) + " | pozostalo " + F2(left) + ".");
            }
        }

        // ================================================================ Zelazni: oblezenia wybrzeza (dopisek testu 334c: 83 doby bez armii i bez opcji oblezenia)
        /// <summary>Gra daje opcje oblezenia tylko twierdzom sasiadujacym z wlasnymi (AiMilitaryBehavior.CalculateDistanceScoreForBesieging: udzial wlasnych sasiadow
        /// &lt; 0.1 -> 0); Wyspy nie maja sasiadow na ladzie Polnocy, wiec przez 83 doby nie mialy ANI JEDNEJ opcji oblezenia, a zbiorka armii idzie tylko za opcja
        /// oblezenia. Najezdzca z morza: wodz Wysp, ktory moze zebrac armie (gra dala czlonkow - CanLordCreateArmy, nie na morzu), albo wodz armii Wysp, bez zadnej
        /// opcji oblezenia > 0 - dostaje oblezenie najlepszej z 4 najblizszych twierdz krolestw w wojnie z q &lt;= 1.0 (sila przyszlej armii) bez weta (teatr Wysp,
        /// 274, 326b - jestesmy po wetach: ThinkPost Last), wynik = model gry (Besieger, ta sila) x odleglosc jak rabunek gry.</summary>
        private static void IronSiege(MobileParty party, PartyThinkParams pt)
        {
            if (party.MapEvent != null || party.BesiegerCamp != null || party.IsCurrentlyAtSea) return;
            bool lead = party.Army != null && party.Army.LeaderParty == party;
            var members = pt.PossibleArmyMembersUponArmyCreation;
            bool gather = !lead && party.Army == null && members != null && members.Count > 0;
            if (!lead && !gather) return;
            if (lead) { try { if (ArmyRestDay.Resting(party.Army) || NightRest.FieldCampHolds(party)) return; } catch { } }
            var scores = pt.AIBehaviorScores;
            for (int i = 0; i < scores.Count; i++) if (scores[i].Item1.AiBehavior == AiBehavior.BesiegeSettlement && scores[i].Item2 > 0f) return;
            var k = party.MapFaction as Kingdom;
            if (k == null) return;
            float strength = party.GetTotalLandStrengthWithFollowers(false);
            if (gather) foreach (var m in members) if (m != null && m.Party != null) strength += m.Party.GetCustomStrength(TaleWorlds.Core.BattleSideEnum.Attacker, MapEvent.PowerCalculationContext.Siege);
            float range = 5f * Campaign.Current.GetAverageDistanceBetweenClosestTwoTownsWithNavigationType(MobileParty.NavigationType.All);
            var pos = party.GetPosition2D;
            var cand = new List<KeyValuePair<Settlement, float>>();
            foreach (var f in k.FactionsAtWarWith)
            {
                if (f == null || !f.IsKingdomFaction || ScaNoBandits320.Skip(f) != 0) continue;
                foreach (var st in f.Settlements)
                {
                    if (st == null || !st.IsFortification || st.Town == null || st.IsUnderSiege || (st.Party != null && st.Party.MapEvent != null)) continue;
                    float d2 = pos.DistanceSquared(st.GetPosition2D);
                    if (d2 <= range * range) cand.Add(new KeyValuePair<Settlement, float>(st, d2));
                }
            }
            if (cand.Count == 0) return;
            cand.Sort((a, b) => a.Value.CompareTo(b.Value));
            var model = Campaign.Current.Models.TargetScoreCalculatingModel;
            Settlement bestT = null; float bestS = 0f; double bestQ = -1; MobileParty.NavigationType bestNav = MobileParty.NavigationType.None; bool bestPort = false;
            for (int i = 0, tried = 0; i < cand.Count && tried < 4; i++)
            {
                var t = cand[i].Key;
                string why;
                if (WarFront334.VetoedFor334e(k, t, out why)) { lock (_lk) _dIronVeto++; continue; }
                tried++;
                double q;
                lock (_lk)
                {
                    if (!SnapFresh || Blocked(party, t)) continue;
                    float our = OurDays(party, t);
                    if (our < 0f) continue;
                    float first; double ally;
                    double z = ZFor(t, k, party, our + SiegeDays, out first, out ally);
                    q = Ratio(z, strength + ally);
                }
                if (q > SiegeBanQ) continue;
                MobileParty.NavigationType nav; float dist; bool fromPort;
                Helpers.AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty(party, t, false, out nav, out dist, out fromPort);
                if (nav == MobileParty.NavigationType.None || dist > range) continue;
                float ds = MBMath.Map(0.75f - dist / range, 0f, 1f, 0.1f, 1f);
                float s;
                _inOwn = true;
                try { s = model.GetTargetScoreForFaction(t, Army.ArmyTypes.Besieger, party, strength) * ds; }
                finally { _inOwn = false; }
                if (s > bestS) { bestS = s; bestT = t; bestQ = q; bestNav = nav; bestPort = fromPort; }
            }
            if (bestT == null || !(bestS > 0f)) return;
            pt.AddBehaviorScore((new AIBehaviorData(bestT, AiBehavior.BesiegeSettlement, bestNav, gather, bestPort, false), bestS));
            lock (_lk)
            {
                _dIronOffers++;
                if (_dIronOffers <= 20 && S != null && S.LogEnabled)
                    Log.Info(Tag + "Zelazni oblegaja | dzien " + Today + " | " + Nm(party) + (gather ? " (zbiorka armii, czlonkow " + members.Count + ")" : " (wodz armii)") + " | " + bestT.StringId + " (" + Nm(bestT)
                             + ", " + Id(bestT.MapFaction) + ") | sila " + F0(strength) + " | q " + F2(bestQ) + " | wynik " + bestS.ToString("0.000", Inv) + ".");
            }
        }

        /// <summary>Doba (Daily, pod _lk): porzadek pamieci obozow, flag, blokad, rabunkow i przydzialow obrony.</summary>
        private static void SiegeDayClean()
        {
            double nowH = CampaignTime.Now.ToHours, nowD = CampaignTime.Now.ToDays;
            var deadC = new List<SiegeEvent>();
            foreach (var kv in _camps) if (kv.Key == null || kv.Key.BesiegedSettlement == null || kv.Key.BesiegedSettlement.SiegeEvent != kv.Key) deadC.Add(kv.Key);
            foreach (var e in deadC) _camps.Remove(e);
            var deadR = new List<MobileParty>();
            foreach (var kv in _retreat) if (kv.Value <= nowH || kv.Key == null || !kv.Key.IsActive) deadR.Add(kv.Key);
            foreach (var p in deadR) _retreat.Remove(p);
            var deadB = new List<PS>();
            foreach (var kv in _block) if (kv.Value <= nowD || kv.Key.P == null || !kv.Key.P.IsActive) deadB.Add(kv.Key);
            foreach (var s in deadB) _block.Remove(s);
            var deadM = new List<MapEvent>();
            foreach (var kv in _raidBad) if (kv.Key == null || kv.Key.IsFinalized || kv.Value[0] < NowH - 2) deadM.Add(kv.Key);
            foreach (var m in deadM) _raidBad.Remove(m);
            DefenceDayClean();
        }
    }
}
