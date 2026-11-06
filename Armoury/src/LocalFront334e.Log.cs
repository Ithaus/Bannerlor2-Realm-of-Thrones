using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;

namespace Armoury
{
    /// <summary>334e - linie "Sila lokalna (334e): " (projekt rozdz. 8): start, krolestwo, migawka, oblezenie / rabunek start, koszt; zdarzenia startu oblezen i rabunkow.</summary>
    internal static partial class LocalFront334e
    {
        private sealed class KDay { public int Raids, RaidsOpen, RaidsArmy, Sieges, SiegesOpen, SiegesBan; }
        private static readonly Dictionary<string, KDay> _kd = new Dictionary<string, KDay>(StringComparer.Ordinal);
        private static int _dStartLines, _dSiegePingPong, _dRaidRep, _dSiegeStory, _dSiegeNoAi;
        private static readonly Dictionary<SK, double> _raidSeen = new Dictionary<SK, double>();   // 334f: (strona, wies) -> godzina ostatniego startu rabunku

        private static void ResetLog() { _kd.Clear(); _dStartLines = 0; _dSiegePingPong = 0; _dRaidRep = 0; _dSiegeStory = 0; _dSiegeNoAi = 0; _raidSeen.Clear(); }

        private static string K01(bool b) { return b ? "TAK" : "NIE"; }

        /// <summary>WarFront334.SessionStart: latki i linia startu. Nie rzuca.</summary>
        internal static void SessionStart()
        {
            try { EnsureHooks(); } catch (Exception e) { Stumble("SessionStart.hooks", e); }
            try
            {
                var s = S;
                if (s == null || !s.LogEnabled) return;
                Log.Info(Tag + "start | klucze: sila " + K01(s.LocalPower334e) + ", fantom " + K01(s.LocalPhantomOff334e) + ", postawa " + K01(s.LocalStance334e) + ", SCA " + K01(s.ScaLocal334e)
                         + ", 274 " + K01(s.LocalRestraint334e) + ", armie lupia " + K01(s.ArmyRaid334e) + ", oblezenie " + K01(s.LocalSiegeJudge334e) + ", BK stick " + K01(s.BkSiegeStickYields334e)
                         + ", rabunek przerwany " + K01(s.LocalRaidBreakOff334e) + ", obrona " + K01(s.LocalDefence334e)
                         + " | zakres: " + (WarFront334.Canon ? "kampania kanonu" : "kampania klasyczna - nie dotyczy")
                         + " | fantom " + _wPhantom + " | cel OfActive " + _wModel + " " + _wScore + " | SCA cofanie " + _wSca + " | BK stick " + _wStick
                         + " | postawa BK " + WarFront334.StanceHookState
                         + " | kolejka AiHourlyTick: SiegeWalls2.ThinkPost (Last) " + SiegeWalls2.HookThinkState + " - oblezenie przed F2 i m116, rabunek / armie / Zelazni poza obozem (weta sprawdzane)"
                         + " | czas rabunku: wzor DefaultRaidModel, model gry " + RaidModelName() + ".");
            }
            catch (Exception e) { Stumble("SessionStart.line", e); }
            try { LocalFront334eTest.Run(); } catch (Exception e) { Stumble("Test", e); }
        }

        private static string RaidModelName()
        {
            try { var m = Campaign.Current.Models.RaidModel; return m != null ? m.GetType().Name : "-"; } catch { return "?"; }
        }

        private static KDay KD(IFaction f)
        {
            string id = Id(f);
            KDay r;
            if (!_kd.TryGetValue(id, out r)) { r = new KDay(); _kd[id] = r; }
            return r;
        }

        /// <summary>OnSiegeEventStarted (WarFront334Behavior): oblezenie AI - q przy starcie, cel zablokowany (powrot - ping-pong). Nie rzuca.</summary>
        internal static void OnSiegeStarted(SiegeEvent ev)
        {
            try
            {
                if (!PowerOn || ev == null || ev.BesiegerCamp == null || ev.BesiegedSettlement == null) return;
                var lp = ev.BesiegerCamp.LeaderParty;
                var st = ev.BesiegedSettlement;
                if (lp == null || WithPlayer(lp) || !(lp.MapFaction is Kingdom)) return;
                double q; bool blocked;
                double o = 0;
                foreach (var pb in ev.BesiegerCamp.GetInvolvedPartiesForEventType(MapEvent.BattleTypes.Siege)) if (pb != null) o += pb.EstimatedStrength;
                // 334f (L7): oblezenie z fabuly ROT (skrypt - np. Blackwater teleportuje Stannisa) - poza regula sily lokalnej, nie blokujemy i nie liczymy
                // 334g (D6): fabula = tylko skrypt ROT rozpoznany przez SiegePhantomLift; DoNotMakeNewDecisions (inne mody, ochrona 335) - osobno, liczy sie w L7
                bool story = false, noAi = false;
                try { story = SiegePhantomLift.StoryOf(ev.BesiegerCamp) != null; } catch { }
                if (!story) { try { noAi = lp.Ai != null && lp.Ai.DoNotMakeNewDecisions; } catch { } }
                lock (_lk)
                {
                    if (!SnapFresh) return;
                    blocked = Blocked(lp, st);
                    if (blocked) _dSiegePingPong++;
                    double kUntil;
                    bool afterRet = (_kblock.TryGetValue(new SK(lp.MapFaction, st), out kUntil) && kUntil > CampaignTime.Now.ToDays) || _krel.ContainsKey(new SK(lp.MapFaction, st));
                    if (noAi) _dSiegeNoAi++;
                    float first; double ally;
                    double z = ZFor(st, lp.MapFaction, lp, SiegeDays, out first, out ally);
                    q = Ratio(z, o + ally);
                    var r = KD(lp.MapFaction);
                    if (story) _dSiegeStory++;
                    else { r.Sieges++; if (q <= OpenQ) r.SiegesOpen++; if (q > SiegeBanQ) r.SiegesBan++; }
                    if (S != null && S.LogEnabled && _dStartLines < 200)
                    {
                        _dStartLines++;
                        Log.Info(Tag + "oblezenie start | dzien " + Today + " | " + Id(lp.MapFaction) + " (" + Nm(lp) + ", armia " + K01(lp.Army != null) + ") -> " + st.StringId + " (" + Nm(st) + ", "
                                 + Id(st.MapFaction) + ") | oboz " + F0(o) + " | wrog w polu " + F0(z) + " | q " + F2(q) + (blocked ? " | POWROT W BLOKADZIE" : "")
                                 + (story ? " | fabula ROT (skrypt) - poza regula" : "") + (noAi ? " | bez decyzji AI (skrypt/inne)" : "")
                                 + (afterRet ? " | po odstapieniu krolestwa" : "") + ".");
                    }
                }
            }
            catch (Exception e) { Stumble("OnSiegeStarted", e); }
        }

        /// <summary>MapEventStarted (WarFront334Behavior): rabunek AI - q przy starcie, armia. Nie rzuca.</summary>
        internal static void OnMapEventStarted(MapEvent me, PartyBase attacker, PartyBase defender)
        {
            try
            {
                if (me == null || !me.IsRaid || !PowerOn) return;
                var v = me.MapEventSettlement;
                var lpb = me.AttackerSide != null ? me.AttackerSide.LeaderParty : null;
                var lp = lpb != null ? lpb.MobileParty : null;
                if (v == null || lp == null || WithPlayer(lp) || !(lp.MapFaction is Kingdom)) return;
                double o = 0; int men = 0;
                foreach (var mep in me.AttackerSide.Parties) { var pb = mep != null ? mep.Party : null; if (pb != null) { o += pb.EstimatedStrength; men += pb.NumberOfAllMembers; } }
                lock (_lk)
                {
                    if (!SnapFresh) return;
                    // 334f (9): ten sam rabunek wznawiany (nocny oboz, powrot po bitwie) - jeden start na (strona, wies) w 24 h
                    var rk = new SK(lp.MapFaction, v);
                    double nowH = CampaignTime.Now.ToHours, last;
                    bool rep = _raidSeen.TryGetValue(rk, out last) && nowH - last < 24.0;
                    if (rep) { _dRaidRep++; return; }
                    _raidSeen[rk] = nowH;   // 334g (D5): powtorka nie przesuwa okna 24 h - wznawiany co noc rabunek to nowy start po dobie
                    float first; double ally;
                    double z = ZFor(v, lp.MapFaction, lp, RaidDays(v.SettlementHitPoints, men), out first, out ally);
                    double q = Ratio(z, o + ally);
                    var r = KD(lp.MapFaction);
                    r.Raids++; if (q <= OpenQ) r.RaidsOpen++; if (lp.Army != null) r.RaidsArmy++;
                    if (S != null && S.LogEnabled && _dStartLines < 200)
                    {
                        _dStartLines++;
                        Log.Info(Tag + "rabunek start | dzien " + Today + " | " + Id(lp.MapFaction) + " (" + Nm(lp) + ", armia " + K01(lp.Army != null) + ") -> " + v.StringId + " (" + Nm(v) + ", "
                                 + Id(v.MapFaction) + ") | nasi " + F0(o) + " | wrog w polu " + F0(z) + " | q " + F2(q) + ".");
                    }
                }
            }
            catch (Exception e) { Stumble("OnMapEventStarted", e); }
        }

        /// <summary>WarFront334.Daily (kampania kanonu): linie migawka, krolestwo (kazde krolestwo AI w wojnie), koszt - za mijajaca dobe; zerowanie doby, porzadek pamieci,
        /// potem nowa migawka (watek glowny). Nie rzuca.</summary>
        internal static void Daily(int today)
        {
            try
            {
                var s = S;
                if (s != null && s.LogEnabled && (s.LocalPower334e || s.LocalStance334e || s.ScaLocal334e || s.LocalSiegeJudge334e))
                {
                    lock (_lk)
                    {
                        try { SnapLine(today); } catch (Exception e) { Stumble("SnapLine", e); }
                        long d0 = Ts();
                        try { KingdomLines(today); } catch (Exception e) { Stumble("KingdomLines", e); }
                        _dDailyT += Ts() - d0;
                        try { CostLine(today); } catch (Exception e) { Stumble("CostLine", e); }
                    }
                }
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally
            {
                lock (_lk)
                {
                    try { SiegeDayClean(); } catch (Exception e) { Stumble("SiegeDayClean", e); }
                    ZeroScoreDay(); ZeroSiegeDay(); ZeroDefenceDay(); _kd.Clear(); _dStartLines = 0; _dBkVeto = 0; _dBkVetoFallback = 0; _dSiegePingPong = 0; _dStanceSweep = 0;
                    ZeroCost(); _dStum = 0; _dRaidRep = 0; _dSiegeStory = 0; _dSiegeNoAi = 0;
                    var oldR = new List<SK>();
                    double nh = CampaignTime.Now.ToHours;
                    foreach (var kv in _raidSeen) if (nh - kv.Value > 48.0) oldR.Add(kv.Key);
                    foreach (var key in oldR) _raidSeen.Remove(key);
                    _scaGeo.Clear(); _scaNear.Clear(); _scaExh.Clear(); _homeThr.Clear(); _thr.Clear(); _thrH = -1;
                }
                try
                {
                    var s = S;
                    var c = Campaign.Current;
                    if (s != null && c != null && c.GameStarted && Live && (s.LocalPower334e || s.LocalSiegeJudge334e || s.LocalRaidBreakOff334e || s.ScaLocal334e)) BuildSnap();
                }
                catch (Exception e) { Stumble("Daily.BuildSnap", e); }
            }
        }

        private static void SnapLine(int today)
        {
            Log.Info(Tag + "migawka | dzien " + today + " | jednostek " + _snap.Units.Count + " | zalog " + _snap.Garrisons + " | zwiad na zywo " + _dLiveUsed + " | celow w pamieci " + _tl.Count
                     + " | " + _snapMs.ToString("0.0", Inv) + " ms | doba migawki " + _snap.Day + ".");
        }

        private static void CostLine(int today)
        {
            double sum = _dMsSum + _hMs; int hrs = _dHours + (_hHour >= 0 ? 1 : 0);
            double max = Math.Max(_dMsMax, _hMs);
            double allSum = _dMsAllSum + _hMs + _hMsGc, allMax = Math.Max(_dMsAllMax, _hMs + _hMsGc);
            var ps = new StringBuilder(160);
            for (int i = 0; i < PathName.Length; i++) ps.Append(i == 0 ? "" : ", ").Append(PathName[i]).Append(' ').Append(_dPath[i].ToString("0.0", Inv)).Append(" (").Append(_dPathN[i]).Append(')');
            Log.Info(Tag + "koszt | dzien " + today + " | drog " + _dDist + " | zapytan " + _dQ + " (ocen " + _dScoreN + ", bez migawki " + _dNoSnap + ") | ms suma " + sum.ToString("0.0", Inv) + " / godzin " + hrs
                     + " = srednio " + (hrs > 0 ? (sum / hrs).ToString("0.00", Inv) : "0") + " / maks godziny " + max.ToString("0.0", Inv)
                     + " | potkniecia " + _dStum + " (sesja " + _sStum + ")"
                     + " | oceny: zero " + _dZero + ", w gore " + _dUp + ", w dol " + _dDown + ", cofniecia SCA " + _dScaUndo + ", obrona nizej " + _dDefLow + ", obrona twierdzy " + _dDefFort
                     + ", dom zagrozony " + _dHomeCap + ", BK weto " + _dBkVeto + " (zapas " + _dBkVetoFallback + ")"
                     + " | oblezenia: sad " + _dJudge + " (KONTYNUUJ " + _dStates[1] + ", SRODEK " + _dStates[2] + ", NIE-ZDAZY " + _dStates[3] + ", ODSTAP " + _dStates[4] + ", GLOD " + _dStates[5] + ")"
                     + ", trzymane " + _dKeep + ", bez wskrzeszania " + _dKeepVeto + ", kandydaci odwrotu " + _dRetreatCand + ", odstapienia " + _dRetreat
                     + (_dRetreatNotes.Count > 0 ? " [" + string.Join("; ", _dRetreatNotes.ToArray()) + "]" : "") + ", zostaly w obozie " + _dStay
                     + ", BK stick przepuscil " + _dStickYield + ", m116 przepuscil " + _dFreshYield
                     + " | rabunki przerwane " + _dBreak + " | armie rabunek opcji " + _dArmyRaidOffers + " (weta " + _dArmyRaidVeto + ") | Zelazni oblezenia opcji " + _dIronOffers + " (weta " + _dIronVeto + ")" + IronXLine()
                     + " | obrona: przydzialy " + _dDefAssign + ", bez obrony " + _dDefNone + ", zmiany przydzialu " + _dDefSwitch
                     + " | postawa przegladem " + _dStanceSweep
                     + " | czasy ms (334f): czekanie " + TMs(_dWaitT).ToString("0.0", Inv) + ", listy " + TMs(_dListT).ToString("0.0", Inv) + ", sumy " + TMs(_dAggT).ToString("0.0", Inv)
                     + ", zwiad " + TMs(_dScoutT).ToString("0.0", Inv) + ", model gry (poza) " + TMs(_dModelT).ToString("0.0", Inv) + ", linie doby (poza) " + TMs(_dDailyT).ToString("0.0", Inv)
                     + " | razem z modelem gry srednio " + (hrs > 0 ? ((sum + TMs(_dModelT)) / hrs).ToString("0.00", Inv) : "0") + " ms/h"
                     + " | blokada krolestwa po odstapieniu: weta " + _dKBlock + ", aktywnych " + _kblock.Count
                     + " | pamiec drog trafien " + _dRoadHit + " | sumy (cel, krolestwo) budow " + _dAggN + ", trafien " + _dAggHit
                     + " | kontrola ZFor " + (_dChk >> 7) + ", rozbieznosci " + _dDiv + (_dDivNote != null ? " [" + _dDivNote + "]" : "")
                     + " | rabunek start powtorki " + _dRaidRep + ", oblezenia fabuly ROT " + _dSiegeStory + ", bez decyzji AI " + _dSiegeNoAi
                     + " | ping-pong: powrot w blokadzie " + _dSiegePingPong + ", przydzial " + _dDefSwitch
                     + " | z GC (340): srednio " + (hrs > 0 ? (allSum / hrs).ToString("0.00", Inv) : "0") + " / maks godziny " + allMax.ToString("0.0", Inv) + ", okien z GC " + _dGcWin + " (" + _dGcMs.ToString("0.0", Inv) + " ms)"
                     + " | sciezki ms (340): " + ps.ToString() + " | wpisy celu (340) budow " + _dThN
                     + " | odsiecz w zasiegu po odstapieniu (340): weta " + _dKRelHold + ", zwolnione " + _dKRelFree + ", pamieci " + _krel.Count
                     + " | zbiorka nasza miara (340): ocen " + _dGather + ", w oknie/srodku " + _dGatherOk + ", nasza/gry " + (_dGather > 0 ? (_dGatherRatio / _dGather).ToString("0.00", Inv) : "-") + ".");
        }

        /// <summary>Linie krolestw. 334f (L3): cele pytane i okno z tej samej miary, ktora dostala mysl AI (ScorePost: q policzone / q &lt;= 0.8, krolestwo
        /// decydujace) - bez ZFor o polnocy. 334f (6): krolestwo gracza tez (tylko log - postawy i decyzji gracza nie sterujemy). Diagnoza Besieger (B334-7).</summary>
        private static void KingdomLines(int today)
        {
            IFaction player = null;
            try { player = Clan.PlayerClan != null ? Clan.PlayerClan.MapFaction : null; } catch { }
            foreach (var k in Kingdom.All)
            {
                if (k == null || k.IsEliminated) continue;
                bool isPlayer = k == player;
                var enemies = new List<IFaction>();
                foreach (var f in k.FactionsAtWarWith) if (f != null && f.IsKingdomFaction && ScaNoBandits320.Skip(f) == 0 && !f.IsEliminated) enemies.Add(f);
                if (enemies.Count == 0) continue;
                int n = 0, men = 0; double str = 0, foe = 0;
                foreach (var u in _snap.Units)
                {
                    if (u.F == k) { n++; men += u.Men; str += u.Str; }
                    else if (FactionManager.IsAtWarAgainstFaction(k, u.F)) foe += u.Str;
                }
                KAsk ka; _dAsk.TryGetValue(k.StringId, out ka);
                int nv = ka != null ? ka.AV.Count : 0, wv = ka != null ? ka.WV.Count : 0, nf = ka != null ? ka.AF.Count : 0, wf = ka != null ? ka.WF.Count : 0;
                int[] o; _dOpts.TryGetValue(k.StringId, out o);
                int def = 0, agg = 0, parts = 0, marches = 0, armies = 0;
                foreach (var wp in k.WarPartyComponents)
                {
                    var mp = wp != null ? wp.MobileParty : null;
                    if (mp == null || !mp.IsActive) continue;
                    parts++;
                    if (mp.Objective == MobileParty.PartyObjective.Defensive) def++; else if (mp.Objective == MobileParty.PartyObjective.Aggressive) agg++;
                    try
                    {
                        if ((mp.DefaultBehavior == AiBehavior.BesiegeSettlement || mp.DefaultBehavior == AiBehavior.RaidSettlement) && mp.TargetSettlement != null
                            && mp.TargetSettlement.MapFaction != null && FactionManager.IsAtWarAgainstFaction(k, mp.TargetSettlement.MapFaction)) marches++;
                    }
                    catch { }
                }
                foreach (var a in k.Armies) if (a != null && a.LeaderParty != null && a.LeaderParty.CurrentSettlement == null) armies++;
                KDay kd; _kd.TryGetValue(k.StringId, out kd); if (kd == null) kd = new KDay();
                float raw, front, ownRaw, ownF; bool locUsed;
                WarFront334.ScaVals(k, out raw, out front, out ownRaw, out ownF, out locUsed);
                bool? exh = ScaNoBandits320.ExhaustedOf(k);
                bool small = k.Fiefs == null || k.Fiefs.Count <= 1;
                string stance = def == 0 && agg == 0 ? "Neutral" : small ? "BK (twierdz <= 1)" : "inna";
                var sb = new StringBuilder(700);
                sb.Append(Tag).Append("krolestwo | dzien ").Append(today).Append(" | ").Append(k.StringId).Append(" (").Append(Nm(k)).Append(')')
                  .Append(" | pole: ").Append(n).Append(" jednostek, ").Append(men).Append(" ludzi, ").Append(F0(str)).Append(" sily (zalogi 0)")
                  .Append(" | wrog w polu ").Append(F0(foe))
                  .Append(" | cele (pytane): wsie ").Append(nv).Append(" (okno ").Append(wv).Append("), twierdze ").Append(nf).Append(" (okno ").Append(wf).Append(')')
                  .Append(" | opcje Raider/Besieger >0: ").Append(o != null ? o[0] : 0).Append('/').Append(o != null ? o[1] : 0)
                  .Append(" | postawa ").Append(stance).Append(": obronnych ").Append(def).Append(", ofensywnych ").Append(agg).Append(" z ").Append(parts)
                  .Append(" (BK chcial ").Append(WarFront334.StanceBkOf(k.StringId)).Append(')')
                  .Append(" | SCA wyczerpane ").Append(exh == null ? "-" : K01(exh.Value)).Append(", wrog/swoi ")
                  .Append(front >= 0 ? F0(front) : "-").Append('/').Append(locUsed ? F0(ownF) + " (pole)" : ownRaw >= 0 ? F0(ownRaw) : "-")
                  .Append(" | start: rabunki ").Append(kd.Raids).Append(" (puste ").Append(kd.RaidsOpen).Append(", armia ").Append(kd.RaidsArmy).Append(')')
                  .Append(", oblezenia ").Append(kd.Sieges).Append(" (puste ").Append(kd.SiegesOpen).Append(", q>1.0: ").Append(kd.SiegesBan).Append(')')
                  .Append(" | marsze na cel ").Append(marches).Append(" | armie w polu ").Append(armies)
                  .Append(" | Besieger: ocen ").Append(ka != null ? ka.Asked : 0).Append(", model 0 ").Append(Model0Of(k.StringId)).Append(", q>prog ").Append(ka != null ? ka.QZero : 0)
                  .Append(" (min q ").Append(ka != null && ka.MinQ >= 0 ? F2(ka.MinQ) : "-").Append(" @").Append(ka != null ? ka.MinT : "-").Append("), blokada ").Append(ka != null ? ka.Blocked : 0)
                  .Append(", lustro ").Append(ka != null ? ka.Mirror : 0)
                  .Append(isPlayer ? " | gracz (tylko log)" : "").Append('.');
                Log.Info(sb.ToString());
            }
        }
    }
}
