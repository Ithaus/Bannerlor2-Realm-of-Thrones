using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 157 (26.10, Jeff 25.10 ok. 22:45: "strzelalem do kawalerzysty mial tarcze na plecach ale glupek po pierwsze nie zaatakowal mnie tylko stal jak debil
    /// i dostawal strzaly a po drugie nie wzial tarczy do reki"; plan 23:55 bez sprzeciwu). Sledztwo 22:45 (investigate:konnik + verify:konnik):
    /// 140 pomijal kazdego konnego (Skip: WMounted), a jezdzcy trzymali drzewce z uzyciem #0 TwoHandedPolearm SPOZA wzorca (log nie mowil, jakie);
    /// stanie w miejscu - najpewniej oslona skrzydla RBM (OverrideBehaviorProtectFlank: szarza z HoldingFlank tylko na konnice wroga) - bez dowodu w logu.
    ///
    /// P1 (klucz CavalryUnderFireLog, sam odczyt): co 1 s szyki konnicy AI (QuerySystem.IsCavalryFormation, nie szyk gracza - HorsePursuit.PlayerForm)
    /// pod ostrzalem (UnderRangedAttackRatio &gt; 0.1 - ten sam prog co RBM ProtectFlank - albo jezdziec szyku trafiony pociskiem w ostatnich 5 s):
    /// sekundy wedlug aktywnego zachowania (BehaviorProtectFlank ze stanem z pola _protectFlankState - FieldInfo raz na proces; BehaviorCharge;
    /// RBMBehaviorCavalryCharge ze stanem _chargeState; BehaviorMountedSkirmish; inne po nazwie typu), rozkazu ruchu, naszego capu predkosci
    /// (MarchToContact.CapKind: marsz 128 / odpoczynek koni 136 / szarza marszem 128) i odleglosci do najblizszego czlowieka wroga
    /// (QuerySystem.ClosestEnemyAgent); trafienia jezdzcow pociskiem bez szarzy szyku (z kolejki 140, glowny watek). Linia "konnica AI pod ostrzalem (157)"
    /// raz na bitwe polowa (ShieldsUp.End). Do linii HIT (VictimState) - id przedmiotu w rece.
    /// P2 (klucz RidersShieldsUpUnderFire, przy AiShieldsUpUnderFire): liczniki konnych do linii 140 i RiderCharge (szarza szyku - FormCharging).
    /// Wyjatki: RoutHold.Stumble miejsce "tarcze" (ShieldsUp.*) i wlasny licznik linii - bez wylacznika.
    /// </summary>
    internal static partial class ShieldsUp
    {
        // ------------------------------------------------------------ stale 157
        internal const float RiderNoSwapNear = 4f;   // P2: jezdziec nie zmienia broni przy wrogu blizej (pieszy 8 m - D2)
        private const float CfEvery = 1f, FireRatio = 0.1f, HitRecent = 5f;
        private const int CfMaxForms = 24, CfMaxSamples = 4, CfMaxShow = 6, CfMaxOther = 3;
        // zachowania (P1)
        private const int BPfHold = 0, BPfCharge = 1, BPfBack = 2, BPfUnknown = 3, BCharge = 4, BRbm = 5, BSkirm = 6, BOther = 7, BNone = 8, NB = 9;
        private static readonly string[] BName = { "oslona skrzydla trzyma", "oslona skrzydla szarzuje", "oslona skrzydla wraca", "oslona skrzydla stan ?",
                                                   "szarza (gra)", "szarza konnicy RBM", "harce konne", "inne", "brak zachowania" };
        private static readonly string[] BShort = { "PF trzyma", "PF szarzuje", "PF wraca", "PF ?", "szarza gry", "szarza RBM", "harce", "inne", "brak" };
        // rozkazy (P1)
        private const int OMove = 0, OCharge = 1, OChargeTo = 2, OStop = 3, OAdvance = 4, OFallBack = 5, OFollow = 6, OOtherOrder = 7, NO = 8;
        private static readonly string[] OName = { "Move", "Charge", "ChargeToTarget", "Stop", "Advance", "FallBack", "Follow", "inne" };
        private static readonly string[] CapName = { "brak", "marsz 128", "odpoczynek koni 136", "szarza marszem 128" };
        private static readonly string[] RbmName = { "nieustalony", "szarzuje", "przejazd", "przegrupowanie" };

        // ------------------------------------------------------------ P2 liczniki konnych (glowny watek)
        private static readonly HashSet<int> RiderMen = new HashSet<int>(), RiderShieldMen = new HashSet<int>(), RiderTwoMen = new HashSet<int>(),
                                             RiderNoOne = new HashSet<int>(), RiderInCharge = new HashSet<int>(), RiderNear = new HashSet<int>(),
                                             RiderFavorMen = new HashSet<int>();
        private static int _rCmdShield, _rCmdTwo, _rChecks, _rChecksOk, _rReverts, _rStubborn, _rRetries, _rChargeRelease;

        // ------------------------------------------------------------ P1 stan (glowny watek)
        private static bool _cavLog, _cavField, _counterKey, _huntKey;   // 204: _huntKey - kolejka do HorsePursuit.OnFire (bitwa polowa)
        private static float _cfAcc;
        private sealed class CfSt
        {
            public string Name;
            public int Sec, OnlyHit, Hits;
            public readonly int[] B = new int[NB], O = new int[NO], C = new int[4], D = new int[5];
            public float DSum = 0f, DMin = float.MaxValue, LastHit = -100f;
            public int DN;
        }
        private static readonly Dictionary<Formation, CfSt> CavForms = new Dictionary<Formation, CfSt>();
        private static readonly List<CfSt> CavOrder = new List<CfSt>();
        private static readonly int[] CfB = new int[NB], CfO = new int[NO], CfC = new int[4], CfD = new int[5], CfRbm = new int[5];
        private static int _cfSec, _cfOnlyHit, _cfDN, _cfHitNoCharge, _cfHitCharge, _cfHorseNoCharge, _cfStum, _cfFormsOver;
        private static float _cfDSum, _cfDMin = float.MaxValue;
        private static readonly HashSet<int> CfRidersNoCharge = new HashSet<int>();
        private static readonly List<string> CfSamples = new List<string>();
        private static readonly List<string> CfOther = new List<string>();
        // odczyt prywatnych pol stanu (raz na proces)
        private static bool _fPfTried, _fRbmTried, _fPfOk, _fRbmOk, _rbmSeen;
        private static FieldInfo _fPf, _fRbm;
        private static Type _tRbm;

        private static void CavStumble(string where, Exception e)
        {
            _cfStum++;
            RoutHold.Stumble(RoutHold.SiteShields, where, e);
        }

        /// <summary>Begin (po Clear): klucz pomiaru (P1); trafienia jezdzcow do kontry P3 w kazdej bitwie polowej (klucz RidersRunDownShooters czyta
        /// HorsePursuit na zywo - przy NIE ocena na sucho); oba tylko w bitwie polowej (bez morza).</summary>
        private static void CavBegin(Mission m, Settings c)
        {
            _cavField = false;
            try { _cavField = m.IsFieldBattle && _kind == "bitwa" && !m.IsNavalBattle && !m.IsNavalRaidBattle; } catch { _cavField = false; }
            _cavLog = _cavField && (c == null || c.CavalryUnderFireLog);
            _counterKey = _cavField;
            _huntKey = _cavField;   // 204: wykrycie konnego strzelca w kazdej bitwie polowej (klucz RidersRunDownShooters czyta HorsePursuit - przy NIE same wykrycia)
        }

        /// <summary>Clear: liczniki konnych (P2) i pomiar konnicy (P1) od zera; klucze zgaszone (Begin ustawia je po Clear).</summary>
        private static void RiderClear()
        {
            RiderMen.Clear(); RiderShieldMen.Clear(); RiderTwoMen.Clear(); RiderNoOne.Clear(); RiderInCharge.Clear(); RiderNear.Clear(); RiderFavorMen.Clear();
            _rCmdShield = _rCmdTwo = _rChecks = _rChecksOk = _rReverts = _rStubborn = _rRetries = _rChargeRelease = 0;
            _cavLog = false; _cavField = false; _counterKey = false; _huntKey = false;
            _cfAcc = 0f;
            CavForms.Clear(); CavOrder.Clear(); CfSamples.Clear(); CfOther.Clear(); CfRidersNoCharge.Clear();
            Array.Clear(CfB, 0, CfB.Length); Array.Clear(CfO, 0, CfO.Length); Array.Clear(CfC, 0, CfC.Length); Array.Clear(CfD, 0, CfD.Length);
            Array.Clear(CfRbm, 0, CfRbm.Length);
            _cfSec = _cfOnlyHit = _cfDN = _cfHitNoCharge = _cfHitCharge = _cfHorseNoCharge = _cfStum = _cfFormsOver = 0;
            _cfDSum = 0f; _cfDMin = float.MaxValue;
            _rbmSeen = false;
        }

        // ------------------------------------------------------------ P2: szarza jezdzca

        /// <summary>P2 (glowny watek): jezdziec w szarzy szyku - dwureczna zostaje, preferencja jednorecznej zwolniona. Kontra P3 osobno (One: bron i
        /// preferencja zamrozone). Recenzja 157: szarza = FormCharging (bez progu 25 m - zmiana broni ok. 2 s przed uderzeniem i w kazdym cyklu RBM).</summary>
        internal static bool RiderCharge(Agent a)
        {
            return FormCharging(a.Formation);
        }

        /// <summary>Recenzja 157 (glowny watek): szyk w szarzy - rozkaz Charge / ChargeToTarget albo (szyk AI) zachowanie szarzy: RBMBehaviorCavalryCharge
        /// w kazdym stanie (przejazd i przegrupowanie ida rozkazem Move - RBMAI CalculateCurrentOrder: ChargingPast / Reforming = MovementOrderMove),
        /// BehaviorCharge, oslona skrzydla szarzuje / wraca (Returning tez Move - BehaviorProtectFlank 1.4.8). Jedna miara dla P1 (trafienia bez szarzy),
        /// P2 (RiderCharge) i P3 (wejscie i koniec kontry). Szyk pod rozkazami gracza - tylko rozkaz.</summary>
        internal static bool FormCharging(Formation f)
        {
            if (f == null) return false;
            var oe = f.GetReadonlyMovementOrderReference().OrderEnum;
            if (oe == MovementOrder.MovementOrderEnum.Charge || oe == MovementOrder.MovementOrderEnum.ChargeToTarget) return true;
            if (!f.IsAIControlled) return false;
            int b = Behave(f, false);
            return b == BPfCharge || b == BPfBack || b == BCharge || b == BRbm;
        }

        /// <summary>Segment " | konni (157) ..." linii 140 (wolane z Write).</summary>
        private static void AppendRiders(StringBuilder sb)
        {
            sb.Append(" | konni (157, regula konnych ").Append(_riders ? "TAK" : "NIE").Append(", prog zmiany broni ").Append(RiderNoSwapNear.ToString("0", Inv))
              .Append(" m, bez uniesienia EnforceShieldUsage): oznaczonych ").Append(RiderMen.Count)
              .Append(", tylko tarcza ").Append(_rCmdShield).Append(" (ludzi ").Append(RiderShieldMen.Count).Append(')')
              .Append(", dwureczna->jednoreczna ").Append(_rCmdTwo).Append(" (ludzi ").Append(RiderTwoMen.Count).Append(')')
              .Append(", bez jednorecznej ").Append(RiderNoOne.Count)
              .Append(", w szarzy szyku lub kontrze ").Append(RiderInCharge.Count)
              .Append(", wrog <").Append(RiderNoSwapNear.ToString("0", Inv)).Append(" m ").Append(RiderNear.Count)
              .Append("; z tarcza w rece po 1.5 s ").Append(_rChecksOk).Append('/').Append(_rChecks).Append(", powtorki ").Append(_rRetries)
              .Append(", powroty ").Append(_rReverts).Append(", uparci ").Append(_rStubborn)
              .Append("; preferencja jednorecznej ludzi ").Append(RiderFavorMen.Count).Append(" (zwolniona przez szarze ").Append(_rChargeRelease).Append(')')
              .Append("; pominieci: smoki ").Append(SkipMen[WDragon].Count).Append(", w poscigu 148 ").Append(SkipMen[WChase].Count);
        }

        // ------------------------------------------------------------ P1: trafienie jezdzca (z Hit, glowny watek)

        /// <summary>Jezdziec AI z szyku konnicy AI trafiony pociskiem (on albo kon): licznik bez szarzy / w szarzy, probka, znacznik trafienia szyku;
        /// wejscie kontry P3 (HorsePursuit.OnRiderShot).</summary>
        private static void RiderHit(Mission m, Agent at, Agent v)
        {
            Agent rider = v.IsMount ? v.RiderAgent : (v.IsHuman && v.MountAgent != null ? v : null);
            if (rider == null || !rider.IsHuman) return;   // martwy od tego pocisku tez liczony (P1), o ile ma jeszcze szyk; kontra sama sprawdza IsActive
            if (at == null || at.Team == null || rider.Team == null || !rider.Team.IsEnemyOf(at.Team)) return;   // bez strzelca albo swoj w swojego
            if (rider.IsMainAgent || !rider.IsAIControlled) return;
            var f = rider.Formation;
            if (f == null) return;
            var q = f.QuerySystem;
            if (q == null || !q.IsCavalryFormation || HorsePursuit.PlayerForm(f)) return;
            bool horse = v.IsMount;
            var oe = f.GetReadonlyMovementOrderReference().OrderEnum;
            bool charging = FormCharging(f);   // recenzja 157: takze przejazd / przegrupowanie RBM i powrot oslony skrzydla (rozkaz Move)
            if (_cavLog)
            {
                var st = GetCf(f);
                if (st != null) { st.LastHit = _now; st.Hits++; }
                if (charging) _cfHitCharge++;
                else
                {
                    _cfHitNoCharge++;
                    CfRidersNoCharge.Add(rider.Index);
                    if (horse) _cfHorseNoCharge++;
                    if (CfSamples.Count < CfMaxSamples)
                    {
                        string who = "?", by = "?";
                        try { who = rider.Character != null ? (rider.Character.StringId ?? "?") : "?"; } catch { }
                        try { by = at.IsMainAgent ? "gracz" : (at.Character != null ? (at.Character.StringId ?? "?") : "?"); } catch { }
                        float d = rider.Position.AsVec2.Distance(at.Position.AsVec2);
                        CfSamples.Add(who + " (" + BShort[Behave(f, false)] + ", " + OName[OrderIdx(oe)] + ", od " + by + (at.HasMount ? " konno" : "") + " z "
                                      + d.ToString("0", Inv) + " m" + (horse ? ", w konia" : "") + ")");
                    }
                }
            }
            if (_counterKey) HorsePursuit.OnRiderShot(m, rider, at, horse, charging);   // 204: juz tylko licznik "przejete przez 204" (wykrycie - OnFire)
        }

        private static CfSt GetCf(Formation f)
        {
            CfSt st;
            if (CavForms.TryGetValue(f, out st)) return st;
            if (CavForms.Count >= CfMaxForms) { _cfFormsOver++; return null; }
            st = new CfSt();
            try
            {
                var t = f.Team;
                var pt = Mission.Current != null ? Mission.Current.PlayerTeam : null;
                string side = t == null ? "?" : (t.Side == BattleSideEnum.Attacker ? "atak" : (t.Side == BattleSideEnum.Defender ? "obrona" : "?"));
                string whose = t != null && pt != null ? (t.Side == pt.Side ? "strona gracza" : "wrog") : "?";
                var u = f.Captain ?? f.GetFirstUnit();
                string troop = u != null && u.Character != null ? (u.Character.StringId ?? "?") : "?";
                st.Name = side + ", " + whose + ", " + f.FormationIndex + ", " + f.CountOfUnits + " ludzi, " + troop;
            }
            catch { st.Name = "?"; }
            CavForms[f] = st;
            CavOrder.Add(st);
            return st;
        }

        private static int OrderIdx(MovementOrder.MovementOrderEnum oe)
        {
            switch (oe)
            {
                case MovementOrder.MovementOrderEnum.Move: return OMove;
                case MovementOrder.MovementOrderEnum.Charge: return OCharge;
                case MovementOrder.MovementOrderEnum.ChargeToTarget: return OChargeTo;
                case MovementOrder.MovementOrderEnum.Stop: return OStop;
                case MovementOrder.MovementOrderEnum.Advance: return OAdvance;
                case MovementOrder.MovementOrderEnum.FallBack: return OFallBack;
                case MovementOrder.MovementOrderEnum.Follow:
                case MovementOrder.MovementOrderEnum.FollowEntity: return OFollow;
                default: return OOtherOrder;
            }
        }

        /// <summary>Aktywne zachowanie szyku (indeks B*); count = do licznikow (stan szarzy RBM, nazwy innych zachowan). Pola prywatne przez FieldInfo raz
        /// na proces (BehaviorProtectFlank._protectFlankState: HoldingFlank 0 / Charging 1 / Returning 2; RBMBehaviorCavalryCharge._chargeState:
        /// Undetermined 0 / Charging 1 / ChargingPast 2 / Reforming 3 - dekompilacja 1.4.8 i RBMAI).</summary>
        private static int Behave(Formation f, bool count)
        {
            var ai = f.AI;
            var b = ai != null ? ai.ActiveBehavior : null;
            if (b == null) return BNone;
            if (b is BehaviorProtectFlank)
            {
                if (!_fPfTried)
                {
                    _fPfTried = true;
                    try { _fPf = AccessTools.Field(typeof(BehaviorProtectFlank), "_protectFlankState"); } catch { _fPf = null; }
                    _fPfOk = _fPf != null;
                }
                if (_fPf == null) return BPfUnknown;
                int s;
                try { var o = _fPf.GetValue(b); s = o != null ? Convert.ToInt32(o) : -1; } catch { s = -1; }
                return s == 0 ? BPfHold : (s == 1 ? BPfCharge : (s == 2 ? BPfBack : BPfUnknown));
            }
            if (b is BehaviorCharge) return BCharge;
            if (b is BehaviorMountedSkirmish) return BSkirm;
            var t = b.GetType();
            if (t.Name == "RBMBehaviorCavalryCharge")
            {
                if (count)
                {
                    _rbmSeen = true;
                    if (!_fRbmTried || !ReferenceEquals(t, _tRbm))
                    {
                        _fRbmTried = true;
                        _tRbm = t;
                        try { _fRbm = AccessTools.Field(t, "_chargeState"); } catch { _fRbm = null; }
                        _fRbmOk = _fRbm != null;
                    }
                    int s = -1;
                    if (_fRbm != null) { try { var o = _fRbm.GetValue(b); s = o != null ? Convert.ToInt32(o) : -1; } catch { s = -1; } }
                    CfRbm[s >= 0 && s <= 3 ? s : 4]++;
                }
                return BRbm;
            }
            if (count && CfOther.Count < CfMaxOther && !CfOther.Contains(t.Name)) CfOther.Add(t.Name);
            return BOther;
        }

        // ------------------------------------------------------------ P1: pass szykow konnicy (co 1 s)

        private static void CavTick(Mission m, float dt)
        {
            if (!_cavField) return;
            _cfAcc += dt;
            if (_cfAcc < CfEvery) return;
            _cfAcc = 0f;
            var teams = m.Teams;
            int nt = teams != null ? teams.Count : 0;
            for (int i = 0; i < nt; i++)
            {
                Team t;
                try { t = teams[i]; } catch { continue; }
                if (!MissionTeams.Valid(t)) continue;
                if (t.Side != BattleSideEnum.Attacker && t.Side != BattleSideEnum.Defender) continue;
                var fl = t.FormationsIncludingSpecialAndEmpty;
                int nf = fl != null ? fl.Count : 0;
                for (int j = 0; j < nf; j++)
                {
                    var f = fl[j];
                    if (f == null || f.CountOfUnits <= 0) continue;
                    try { CavForm(f); } catch (Exception e) { CavStumble("ShieldsUp.KonnicaSzyk", e); }
                }
            }
        }

        private static void CavForm(Formation f)
        {
            var q = f.QuerySystem;
            if (q == null || !q.IsCavalryFormation || HorsePursuit.PlayerForm(f)) return;
            CfSt st;
            CavForms.TryGetValue(f, out st);
            bool hit = st != null && _now - st.LastHit < HitRecent;
            float ratio = q.UnderRangedAttackRatio;
            if (ratio <= FireRatio && !hit) return;
            if (st == null) { st = GetCf(f); if (st == null) return; }
            st.Sec++; _cfSec++;
            if (ratio <= FireRatio) { st.OnlyHit++; _cfOnlyHit++; }
            int b = Behave(f, true);
            st.B[b]++; CfB[b]++;
            int o = OrderIdx(f.GetReadonlyMovementOrderReference().OrderEnum);
            st.O[o]++; CfO[o]++;
            int c = MarchToContact.CapKind(f);
            if (c < 0 || c > 3) c = 0;
            st.C[c]++; CfC[c]++;
            Agent ce = null;
            try { ce = q.ClosestEnemyAgent; } catch { ce = null; }
            if (ce != null && ce.IsMount && ce.RiderAgent != null) ce = ce.RiderAgent;
            if (ce == null || !ce.IsActive()) { st.D[4]++; CfD[4]++; return; }
            float d = f.CachedAveragePosition.Distance(ce.Position.AsVec2);
            int k = d < 15f ? 0 : (d < 40f ? 1 : (d < 80f ? 2 : 3));
            st.D[k]++; CfD[k]++;
            st.DSum += d; st.DN++; if (d < st.DMin) st.DMin = d;
            _cfDSum += d; _cfDN++; if (d < _cfDMin) _cfDMin = d;
        }

        // ------------------------------------------------------------ P1: linia bitwy

        private static string Fm(float v) { return v == float.MaxValue ? "-" : v.ToString("0", Inv); }

        private static void AppendCounts(StringBuilder sb, int[] a, string[] names, string sep)
        {
            bool any = false;
            for (int i = 0; i < a.Length && i < names.Length; i++)
            {
                if (a[i] <= 0) continue;
                if (any) sb.Append(sep);
                sb.Append(names[i]).Append(' ').Append(a[i]);
                any = true;
            }
            if (!any) sb.Append('-');
        }

        private static void CavWrite()
        {
            if (!_cavField) return;
            var sb = new StringBuilder(2000);
            sb.Append("konnica AI pod ostrzalem (157): misja ").Append(_kind).Append(", klucz TAK (sam pomiar, bitwa bez zmian; szyk konnicy AI pod ostrzalem = UnderRangedAttackRatio > ")
              .Append(FireRatio.ToString("0.0", Inv)).Append(" (prog RBM oslony skrzydla) albo jezdziec szyku trafiony pociskiem w ").Append(HitRecent.ToString("0", Inv))
              .Append(" s; probka co ").Append(CfEvery.ToString("0", Inv)).Append(" s; szyki pod rozkazami gracza poza)")
              .Append(" | szykow ").Append(CavOrder.Count).Append(" (ponad limit ").Append(_cfFormsOver).Append("), szyko-sekund pod ostrzalem ").Append(_cfSec)
              .Append(" (w tym tylko z naszego trafienia ").Append(_cfOnlyHit).Append(')')
              .Append(" | zachowanie: ");
            AppendCounts(sb, CfB, BName, ", ");
            sb.Append(" (szarza RBM - stan: ");
            AppendCounts(sb, CfRbm, new[] { RbmName[0], RbmName[1], RbmName[2], RbmName[3], "?" }, ", ");
            sb.Append("; inne zachowania: ").Append(CfOther.Count > 0 ? string.Join(", ", CfOther) : "-").Append(')')
              .Append(" | rozkaz: ");
            AppendCounts(sb, CfO, OName, ", ");
            sb.Append(" | nasz cap predkosci szyku: ");
            AppendCounts(sb, CfC, CapName, ", ");
            sb.Append(" | najblizszy czlowiek wroga od szyku: sr. ").Append(_cfDN > 0 ? (_cfDSum / _cfDN).ToString("0", Inv) : "-").Append(" m, min ").Append(Fm(_cfDMin))
              .Append(" m (<15 m ").Append(CfD[0]).Append(", 15-40 m ").Append(CfD[1]).Append(", 40-80 m ").Append(CfD[2]).Append(", 80+ m ").Append(CfD[3])
              .Append(", brak ").Append(CfD[4]).Append(')')
              .Append(" | trafienia jezdzcow AI pociskiem bez szarzy szyku (szarza = rozkaz Charge / ChargeToTarget albo szarza RBM w kazdym stanie, szarza gry, oslona skrzydla szarzuje / wraca) ").Append(_cfHitNoCharge).Append(" (jezdzcow ").Append(CfRidersNoCharge.Count)
              .Append(", w konia ").Append(_cfHorseNoCharge).Append("), w szarzy szyku ").Append(_cfHitCharge)
              .Append(" | szyki:");
            int shown = 0;
            for (int i = 0; i < CavOrder.Count && shown < CfMaxShow; i++)
            {
                var st = CavOrder[i];
                if (st.Sec <= 0 && st.Hits <= 0) continue;
                shown++;
                sb.Append(shown > 1 ? "; [" : " [").Append(st.Name).Append(": ").Append(st.Sec).Append(" s (tylko trafienie ").Append(st.OnlyHit).Append("), trafien ")
                  .Append(st.Hits).Append(" - ");
                AppendCounts(sb, st.B, BShort, " / ");
                sb.Append("; ");
                AppendCounts(sb, st.O, OName, " / ");
                sb.Append("; cap ");
                AppendCounts(sb, st.C, CapName, " / ");
                sb.Append("; wrog sr. ").Append(st.DN > 0 ? (st.DSum / st.DN).ToString("0", Inv) : "-").Append(" m, min ").Append(Fm(st.DMin)).Append(" m]");
            }
            if (shown == 0) sb.Append(" -");
            else if (CavOrder.Count > shown) sb.Append(" (+").Append(CavOrder.Count - shown).Append(')');
            sb.Append(" | probka trafien bez szarzy: ").Append(CfSamples.Count > 0 ? string.Join("; ", CfSamples) : "-")
              .Append(" | odczyt stanu: _protectFlankState ").Append(!_fPfTried ? "nie czytany" : (_fPfOk ? "TAK" : "NIE"))
              .Append(", _chargeState RBM ").Append(!_rbmSeen ? "nie widziany" : (_fRbmOk ? "TAK" : "NIE"))
              .Append(" | potkniecia konnica ").Append(_cfStum).Append(" (miejsce tarcze).");
            Log.Info(sb.ToString());
        }
    }
}
