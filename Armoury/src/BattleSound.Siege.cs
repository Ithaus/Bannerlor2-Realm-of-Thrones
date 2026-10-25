using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// noc 25.10 P10 SZTURM MUROW I BEBNY OBLEZENIA (Jeff 03:20-03:30: "dodaj odglosy tlumu i krzykow przy szturmie murow", "dodaj odglosy bebnow przy oblezeniu";
    /// klucze SiegeAssaultCrowd i SiegeDrums, czytane na zywo, pod glownym BattleSoundsEnabled). Tylko szturm (Mission.IsSiegeBattle - nie wypad, nie bitwa polowa).
    ///  - Obiekty: raz po rozstawieniu przeglad m.MissionObjects (drabiny, wieze, tarany, bramy z DestructionComponent, odcinki murow); pozycje tez raz
    ///    (brama i mur - ICastleKeyPosition.GetPosition, drabina / wieza / taran - ich cel na murach albo brama). Potem co 1 s tylko pola C# (stan drabiny,
    ///    HasArrivedAtTarget, RamState, IsDestroyed, IsDisabled) - bez GameEntity i bez pracy co klatke. Stan poczatkowy przy przegladzie = bez zdarzenia.
    ///    OnClearScene zapomina obiekty (nowa scena = nowy przeglad).
    ///  - Fazy: podejscie (od rozstawienia) -> szturm (pierwsza drabina w gore, wieza / taran u celu, rozbita brama, wylom, szyk atakujacych do 30 m od murow).
    ///    Walka na murach: otwarta droga (drabina na murze, wieza u murow, rozbita brama, wylom) i szyk wrecz atakujacych (od 5 ludzi) w zwarciu (wrog do 15 m)
    ///    do 40 m od murow - trzyma sie 20 s po ostatnim zwarciu.
    ///  - Zdarzenia (przejscia stanu, ocena co 1 s): drabina w gore - ryk atakujacych (sc_roar_&lt;styl&gt;) + "Up the ladders!" (sa ladders) i po 1,5 s
    ///    "Ladders! Push them off!" (sd ladders); wieza u murow - ryk + sa tower + sd walls; taran u bramy - sa ram + sd oil ("Pour the oil!"), po okrzyku
    ///    plonaca smola (sg_oil) przy bramie; rozbita brama albo atakujacy przy wylomie - ryk wylomu (sc_breach) + sa breach + sd breach. Okresowo: sd archers
    ///    ("Archers to the walls!") w podejsciu (najwyzej 3), sd walls (najwyzej 8) i sa assault (najwyzej 6) w szturmie, sd oil co 45-70 s, gdy taran bije.
    ///  - Glosy: mowca szyku tej strony najblizszego miejscu (do 150 m; SpeakerOf - kapitan / najwyzszy tier, mezczyzna, AI, nie gracz, nie Inny), region jak
    ///    rozkazy (OrderRegion; Dothrakowie i Nieskalani - essos; Nocna Straz - nightswatch, potem north); bramka glosu druzyny jak rozkazy (TState.OrderVoice),
    ///    odstep tego samego okrzyku na strone (SaGap / SdGap). Strona milczaca (Inni, bandyci) - cisza (bez glosu, ryku i bebnow).
    ///  - Tlo (GAmb): petla tlumu atakujacych (sc_assault_&lt;styl&gt;) i obroncow (sc_defend_&lt;styl&gt;) w goracym miejscu szturmu najblizszym sluchacza (od fazy
    ///    szturmu, gdy jest gorace miejsce); petla zalogi taranu (sg_ram) tylko, gdy taran bije (uderzenia gra gra - bez dubla). Styl tlumu: westeros, freefolk,
    ///    iron, essos (brak klipu stylu - westeros).
    ///  - Bebny oblezenia (GDrum, jedna linia bebnow atakujacych 30 m za ich srodkiem, od 20 ludzi): ds_&lt;styl&gt;_approach w podejsciu, ds_&lt;styl&gt;_assault w szturmie
    ///    (brak - klip drugiej fazy, potem beben marszu dr_&lt;styl&gt;); ten sam klip przez faze (rowny rytm); styl jak bebny marszu (DrumStyle; Dothrakowie bez bebna).
    ///    Walka na murach - nowy takt nie rusza (beben cichnie z koncem taktu); klucz NIE / koniec bitwy / brak atakujacych - Release od razu.
    ///    Przy SiegeDrums TAK bebny marszu (BattleDrumsMarch) w oblezeniu ustepuja (jedna zasada na jedno zjawisko - BattleSound.Drums.cs).
    ///  - Linia: segmenty " | szturm murow ..." i " | bebny oblezenia ..." w linii misji (tylko oblezenie).
    /// </summary>
    internal static partial class BattleSound
    {
        private const float SgEvalEvery = 1f, SgNearWall = 30f, SgFightNear = 15f, SgFightWall = 40f, SgFightHold = 20f, SgSpkRange = 150f, SgStale = 4f;
        private const float ShoutMin = 30f, ShoutMax = 280f, RoarMin = 25f, RoarMax = 400f, BreachMin = 30f, BreachMax = 500f, CrowdMin = 20f, CrowdMax = 300f;
        private const float RamMin = 15f, RamMax = 200f, OilMin = 15f, OilMax = 200f, SdMin = 25f, SdMax = 450f, SdBack = 30f, RamHitHold = 3f;
        private const int MinMenSdrum = 20, MinMenSgFight = 5, SgQMax = 16, SgFxMax = 8;

        // obiekty
        private const int OLadder = 0, OTower = 1, ORam = 2, OGate = 3, OBreach = 4, OTypes = 5;
        // okrzyki: atakujacy (sa) i obroncy (sd)
        private const int SaAssault = 0, SaLadders = 1, SaTower = 2, SaRam = 3, SaBreach = 4;
        private const int SdWalls = 0, SdLadders = 1, SdOil = 2, SdArchers = 3, SdBreach = 4, SgKinds = 5;
        private static readonly string[] SaName = { "assault", "ladders", "tower", "ram", "breach" };
        private static readonly string[] SdName = { "walls", "ladders", "oil", "archers", "breach" };
        private static readonly float[] SaGap = { 40f, 12f, 15f, 25f, 6f };
        private static readonly float[] SdGap = { 30f, 12f, 40f, 40f, 6f };

        private sealed class SgObj
        {
            internal MissionObject O;
            internal int Type;
            internal Vec3 Pos;        // cel na murach / brama (raz przy przegladzie)
            internal int Last;        // drabina 1 = w gore, wieza / taran 1 = u celu, brama 1 = rozbita, wylom 1 = atakujacy przy nim
            internal bool Hot;        // gorace miejsce szturmu (tlo tlumu)
        }

        private sealed class SgQ { internal float At; internal int Side, Kind; internal Vec3 Pos; }
        private sealed class SgFx { internal float At; internal string Code, Key, Tag; internal Vec3 Pos; internal float Min, Max; }

        private static readonly List<SgObj> _sgObjs = new List<SgObj>();
        private static readonly List<Vec2> _sgKeys = new List<Vec2>();      // bramy i odcinki murow (2D) - bliskosc murow
        private static readonly List<Vec2> _sgAttPts = new List<Vec2>();    // szyki atakujacych od 5 ludzi (ocena)
        private static readonly List<SgQ> _sgQ = new List<SgQ>();
        private static readonly List<SgFx> _sgFx = new List<SgFx>();
        private static readonly Stamp[] _sgKindStamp = new Stamp[2 * SgKinds];
        private static readonly int[] _sgN = new int[OTypes];
        private static readonly int[] _sgEv = new int[OTypes];
        private static readonly int[] _cSa = new int[SgKinds];
        private static readonly int[] _cSd = new int[SgKinds];
        private static Stamp _sgRoarStamp, _sgBreachStamp;
        private static Loop _sgCrowdA, _sgCrowdD, _sgRamLoop;

        private static bool _sgScanned, _sgFight, _sgOpen, _sgAttSilent, _sgDefSilent, _sgHotOk;
        private static int _sgPhase, _sgScanBad, _sgArchersN, _sgWallsN, _sgAssaultN, _sgFightCount, _sgMenA, _sgMenD;
        private static float _sgAcc, _sgApproachAt, _sgAssaultAt, _sgFightAt, _sgFightLast, _sgRamHitAt, _sgRamHitSec;
        private static float _sgNextArchers, _sgNextWalls, _sgNextAssault, _sgNextOil;
        private static Vec2 _sgCenA, _sgCenD;
        private static Vec3 _sgHot, _sgRamPos;
        private static string _sgAttCul, _sgDefCul, _sgAttStyle, _sgDefStyle;
        private static int _cSgGap, _cSgNoSpk, _cSgMute, _cSgNoClip, _cSgBusy, _cSgRej, _cSgStale, _cSgRoar, _cSgBreach, _cSgOil, _cSgLoopA, _cSgLoopD, _cSgLoopRam;

        // bebny oblezenia
        private static Snd _sdCur;
        private static Clip _sdClip;
        private static string _sdStyle, _sdClipPhase;
        private static bool _sdActive, _sdWant, _sdHush, _sdPosOk, _sdFromMarch;
        private static Vec3 _sdPos;
        private static int _cSdStart, _cSdBeat, _cSdBeatAppr, _cSdBeatAss, _cSdFromMarch, _cSdStop, _cSdHushN, _cSdNoClip;

        private static void ResetSiege()
        {
            _sgObjs.Clear(); _sgKeys.Clear(); _sgAttPts.Clear(); _sgQ.Clear(); _sgFx.Clear();
            for (int i = 0; i < _sgKindStamp.Length; i++) _sgKindStamp[i] = default(Stamp);
            for (int i = 0; i < OTypes; i++) { _sgN[i] = 0; _sgEv[i] = 0; }
            for (int i = 0; i < SgKinds; i++) { _cSa[i] = 0; _cSd[i] = 0; }
            _sgRoarStamp = default(Stamp); _sgBreachStamp = default(Stamp);
            _sgCrowdA = _sgCrowdD = _sgRamLoop = null;
            _sgScanned = _sgFight = _sgOpen = _sgAttSilent = _sgDefSilent = _sgHotOk = false;
            _sgPhase = _sgScanBad = _sgArchersN = _sgWallsN = _sgAssaultN = _sgFightCount = _sgMenA = _sgMenD = 0;
            _sgAcc = 0f; _sgApproachAt = _sgAssaultAt = _sgFightAt = -1f; _sgFightLast = _sgRamHitAt = -1000f; _sgRamHitSec = 0f;
            _sgNextArchers = _sgNextWalls = _sgNextAssault = _sgNextOil = -1f;
            _sgCenA = _sgCenD = Vec2.Zero; _sgHot = _sgRamPos = Vec3.Zero;
            _sgAttCul = _sgDefCul = _sgAttStyle = _sgDefStyle = null;
            _cSgGap = _cSgNoSpk = _cSgMute = _cSgNoClip = _cSgBusy = _cSgRej = _cSgStale = _cSgRoar = _cSgBreach = _cSgOil = _cSgLoopA = _cSgLoopD = _cSgLoopRam = 0;
            _sdCur = null; _sdClip = null; _sdStyle = _sdClipPhase = null;
            _sdActive = _sdWant = _sdHush = _sdPosOk = _sdFromMarch = false; _sdPos = Vec3.Zero;
            _cSdStart = _cSdBeat = _cSdBeatAppr = _cSdBeatAss = _cSdFromMarch = _cSdStop = _cSdHushN = _cSdNoClip = 0;
        }

        /// <summary>Bebny marszu (BattleSound.Drums.cs) ustepuja w oblezeniu, gdy gra beben oblezenia (klucz SiegeDrums).</summary>
        private static bool SiegeDrumsOwn(Settings c) { return _siege && c != null && c.SiegeDrums; }

        /// <summary>Takt 0,25 s (TickFeatures, po ogniu). Ocena obiektow co 1 s; kolejki okrzykow i efektow, petle i bebny w kazdym takcie.</summary>
        private static void Siege(Mission m, Settings c)
        {
            if (!_siege) return;
            _sgAcc += TickEvery;
            if (_sgAcc >= SgEvalEvery)
            {
                _sgAcc = 0f;
                SiegeEval(m, c);
            }
            if (!_sgScanned) return;
            bool ended = m.MissionEnded;
            bool crowd = c.SiegeAssaultCrowd;
            // kolejka okrzykow (opoznienie drugiej strony) i efektow (smola po okrzyku)
            for (int i = _sgQ.Count - 1; i >= 0; i--)
            {
                var q = _sgQ[i];
                if (_now < q.At) continue;
                _sgQ.RemoveAt(i);
                if (!crowd || ended || _now - q.At > SgStale) { _cSgStale++; continue; }
                TryShout(m, q.Side, q.Kind, q.Pos);
            }
            for (int i = _sgFx.Count - 1; i >= 0; i--)
            {
                var x = _sgFx[i];
                if (_now < x.At) continue;
                _sgFx.RemoveAt(i);
                if (!crowd || ended || _now - x.At > SgStale) { _cSgStale++; continue; }
                var cl = Pick(x.Code, x.Key);
                if (cl == null) { _cSgNoClip++; continue; }
                if (Play(m, cl, GAmb, x.Pos, x.Min, x.Max, true, null, null, x.Tag) != null) _cSgOil++;
                else _cSgRej++;
            }
            // petle tlumu i zalogi taranu
            bool assault = crowd && _sgPhase >= 2 && !ended && _sgHotOk;
            if (LoopTick(m, _sgCrowdA, assault && !_sgAttSilent && _sgMenA >= 10)) _cSgLoopA++;
            if (LoopTick(m, _sgCrowdD, assault && !_sgDefSilent && _sgMenD >= 5)) _cSgLoopD++;
            if (LoopTick(m, _sgRamLoop, crowd && !ended && !_sgAttSilent && _now - _sgRamHitAt < RamHitHold)) _cSgLoopRam++;
            SiegeDrumTick(m, c);
        }

        // ============================================================ przeglad obiektow (raz po rozstawieniu)

        private static void SiegeScan(Mission m)
        {
            _sgScanned = true;
            _sgObjs.Clear(); _sgKeys.Clear();
            for (int i = 0; i < OTypes; i++) _sgN[i] = 0;
            var all = m.MissionObjects;
            int n = all != null ? all.Count : 0;
            for (int i = 0; i < n; i++)
            {
                var o = all[i];
                if (o == null || o.IsDisabled) continue;
                try
                {
                    if (o is SiegeLadder l) AddObj(OLadder, l, TargetPos(l.TargetCastlePosition, l), LadderUp(l.State) ? 1 : 0);
                    else if (o is SiegeTower t) { if (!t.IsDestroyed) AddObj(OTower, t, TargetPos(t.TargetCastlePosition, t), t.HasArrivedAtTarget ? 1 : 0); }
                    else if (o is BatteringRam r) { if (!r.IsDestroyed) AddObj(ORam, r, TargetPos(r.TargetCastlePosition, r), r.HasArrivedAtTarget ? 1 : 0); }
                    else if (o is CastleGate g)
                    {
                        if (!g.IsDestructible) continue;
                        var p = g.GetPosition();
                        _sgKeys.Add(p.AsVec2);
                        AddObj(OGate, g, p, g.IsDestroyed ? 1 : 0);
                    }
                    else if (o is WallSegment w)
                    {
                        var p = w.GetPosition();
                        _sgKeys.Add(p.AsVec2);
                        if (w.IsBreachedWall) AddObj(OBreach, w, p, 0);
                    }
                }
                catch (Exception e) { _sgScanBad++; RoutHold.Stumble(RoutHold.SiteSound, "BattleSound.Szturm.Przeglad", e); }
            }
            BasicCharacterObject chA, chD;
            _sgAttCul = TeamCul(m.AttackerTeam, out chA);
            _sgDefCul = TeamCul(m.DefenderTeam, out chD);
            _sgAttSilent = Silent(chA, _sgAttCul);
            _sgDefSilent = Silent(chD, _sgDefCul);
            _sgAttStyle = CrowdStyle(_sgAttCul);
            _sgDefStyle = CrowdStyle(_sgDefCul);
            _sdStyle = _sgAttSilent ? null : DrumStyle(_sgAttCul);
            string ka = CrowdKey("assault", _sgAttStyle), kd = CrowdKey("defend", _sgDefStyle);
            if (ka != null) _sgCrowdA = new Loop { Code = "sc", Key = ka, Tag = "tlum szturmu", Is3d = true, Min = CrowdMin, Max = CrowdMax };
            if (kd != null) _sgCrowdD = new Loop { Code = "sc", Key = kd, Tag = "tlum obroncow", Is3d = true, Min = CrowdMin, Max = CrowdMax };
            if (Has("sg", "ram")) _sgRamLoop = new Loop { Code = "sg", Key = "ram", Tag = "zaloga taranu", Is3d = true, Min = RamMin, Max = RamMax };
        }

        private static void AddObj(int type, MissionObject o, Vec3 pos, int state)
        {
            _sgObjs.Add(new SgObj { O = o, Type = type, Pos = pos, Last = state, Hot = state == 1 });
            _sgN[type]++;
        }

        private static Vec3 TargetPos(MissionObject target, MissionObject self)
        {
            var k = target as ICastleKeyPosition;
            if (k != null) return k.GetPosition();
            return self.GameEntity.GlobalPosition;
        }

        /// <summary>Drabina poza ziemia: podnoszona, na murze albo spychana (spor o drabine to jedno zdarzenie - bez okrzyku przy kazdym przechyle);
        /// na ziemi tylko OnLand / FallToLand.</summary>
        private static bool LadderUp(SiegeLadder.LadderState s)
        {
            return s != SiegeLadder.LadderState.OnLand && s != SiegeLadder.LadderState.FallToLand && s != SiegeLadder.LadderState.NumberOfStates;
        }

        /// <summary>Styl tlumu: westeros (Westeros), freefolk (dzicy), iron (Zelazne Wyspy), essos (reszta); null - brak kultury.</summary>
        private static string CrowdStyle(string cul)
        {
            if (cul == null) return null;
            if (Wild(cul)) return "freefolk";
            if (cul == "sturgia") return "iron";
            return Westeros(cul) ? "westeros" : "essos";
        }

        private static string CrowdKey(string kind, string style)
        {
            if (style != null && Has("sc", kind + "_" + style)) return kind + "_" + style;
            return Has("sc", kind + "_westeros") ? kind + "_westeros" : null;
        }

        /// <summary>Region okrzykow szturmu: jak rozkazy, Dothrakowie i Nieskalani - essos; Nocna Straz - wlasne klipy, potem north.</summary>
        private static Clip SiegeClip(string code, string cul, string kind)
        {
            if (cul == "nightswatch") { var nw = Pick(code, "nightswatch_" + kind); if (nw != null) return nw; }
            string reg = OrderRegion(cul);
            if (reg == null) return null;
            if (reg == "dothraki" || reg == "ghiscari") reg = "essos";
            return Pick(code, reg + "_" + kind);
        }

        // ============================================================ ocena (co 1 s)

        private static void SiegeEval(Mission m, Settings c)
        {
            if (!m.IsDeploymentFinished) return;
            if (!_sgScanned) SiegeScan(m);
            bool ended = m.MissionEnded;
            if (_sgPhase == 0)
            {
                _sgPhase = 1;
                _sgApproachAt = _now;
                _sgNextArchers = _now + 8f;
            }
            SideScan(m);
            // obiekty: przejscia stanu -> zdarzenia
            _sgOpen = false;
            float best = float.MaxValue;
            _sgHotOk = false;
            for (int i = 0; i < _sgObjs.Count; i++)
            {
                var s = _sgObjs[i];
                int now = s.Last;
                bool open = false;
                try
                {
                    var o = s.O;
                    if (o == null || o.IsDisabled) { s.Hot = false; continue; }
                    switch (s.Type)
                    {
                        case OLadder:
                            var st = ((SiegeLadder)o).State;
                            now = LadderUp(st) ? 1 : 0;
                            open = st == SiegeLadder.LadderState.OnWall || st == SiegeLadder.LadderState.FallToWall;
                            break;
                        case OTower:
                            var t = (SiegeTower)o;
                            if (t.IsDestroyed) { s.Hot = false; continue; }
                            now = t.HasArrivedAtTarget ? 1 : 0;
                            open = now == 1;
                            break;
                        case ORam:
                            var r = (BatteringRam)o;
                            if (r.IsDestroyed) { s.Hot = false; continue; }
                            now = r.HasArrivedAtTarget ? 1 : 0;
                            if (now == 1 && r.State != BatteringRam.RamState.Stable)
                            {
                                if (_now - _sgRamHitAt < 2f * SgEvalEvery) _sgRamHitSec += SgEvalEvery;
                                _sgRamHitAt = _now;
                                _sgRamPos = s.Pos;
                            }
                            break;
                        case OGate:
                            now = ((CastleGate)o).IsDestroyed ? 1 : 0;
                            open = now == 1;
                            break;
                        case OBreach:
                            open = true;
                            if (now == 0 && AttackerNear(s.Pos, SgNearWall)) now = 1;
                            break;
                    }
                }
                catch (Exception e) { RoutHold.Stumble(RoutHold.SiteSound, "BattleSound.Szturm.Obiekt", e); s.Hot = false; continue; }
                if (s.Last == 0 && now == 1 && !ended) SiegeEvent(m, c, s);
                s.Last = now;
                s.Hot = now == 1;
                if (open) _sgOpen = true;
                if (s.Hot)
                {
                    float d = LisDist(s.Pos);
                    if (d < best) { best = d; _sgHot = s.Pos; _sgHotOk = true; }
                }
            }
            if (_sgPhase < 2 && !ended && AttackersNearWalls()) EnterAssault();
            // walka na murach (z przytrzymaniem)
            bool fight = _sgOpen && AttackersFighting(m);
            if (fight)
            {
                if (!_sgFight) { _sgFight = true; _sgFightCount++; if (_sgFightAt < 0f) _sgFightAt = _now; }
                _sgFightLast = _now;
            }
            else if (_sgFight && _now - _sgFightLast > SgFightHold) _sgFight = false;
            // pozycje petli tlumu przy goracym miejscu
            if (_sgHotOk)
            {
                MoveLoop(_sgCrowdA, Toward(_sgHot, _sgCenA, 12f, 0f));
                MoveLoop(_sgCrowdD, Toward(_sgHot, _sgCenD, 6f, 4f));
            }
            if (_sgRamLoop != null) MoveLoop(_sgRamLoop, _sgRamPos);
            // bebny oblezenia: miejsce za atakujacymi i warunek
            DrumPlace(m);
            _sdHush = _sgFight;
            _sdWant = c.SiegeDrums && !ended && _sgPhase >= 1 && !_sgFight && _sdStyle != null && _sgMenA >= MinMenSdrum && _sdPosOk;
            // okrzyki okresowe
            if (ended || !c.SiegeAssaultCrowd) return;
            if (_sgPhase == 1 && _sgArchersN < 3 && _now >= _sgNextArchers)
            {
                _sgArchersN++;
                _sgNextArchers = _now + 50f + (float)_rng.NextDouble() * 30f;
                Vec3 p;
                if (KeyNearest(_sgCenA, out p)) Shout(SgDefSide, SdArchers, p, 0f);
            }
            if (_sgPhase >= 2)
            {
                Vec3 p = _sgHot;
                bool ok = _sgHotOk || KeyNearest(_sgCenA, out p);
                if (ok && _sgWallsN < 8 && _now >= _sgNextWalls)
                {
                    _sgWallsN++;
                    _sgNextWalls = _now + 35f + (float)_rng.NextDouble() * 30f;
                    Shout(SgDefSide, SdWalls, p, 0f);
                }
                if (ok && _sgAssaultN < 6 && _now >= _sgNextAssault)
                {
                    _sgAssaultN++;
                    _sgNextAssault = _now + 45f + (float)_rng.NextDouble() * 30f;
                    Shout(SgAttSide, SaAssault, p, 0f);
                }
            }
            if (_now - _sgRamHitAt < RamHitHold && _sgNextOil >= 0f && _now >= _sgNextOil)
            {
                _sgNextOil = _now + 45f + (float)_rng.NextDouble() * 25f;
                Shout(SgDefSide, SdOil, _sgRamPos, 0f);
            }
        }

        private const int SgAttSide = 0, SgDefSide = 1;

        private static void EnterAssault()
        {
            if (_sgPhase >= 2) return;
            _sgPhase = 2;
            _sgAssaultAt = _now;
            _sgNextWalls = _now + 2f;
            _sgNextAssault = _now + 6f;
        }

        /// <summary>Zdarzenie obiektu (przejscie 0 -> 1). Licznik zawsze; dzwiek tylko przy SiegeAssaultCrowd.</summary>
        private static void SiegeEvent(Mission m, Settings c, SgObj s)
        {
            _sgEv[s.Type]++;
            EnterAssault();
            if (!c.SiegeAssaultCrowd) return;
            switch (s.Type)
            {
                case OLadder:
                    Roar(m, s.Pos);
                    Shout(SgAttSide, SaLadders, s.Pos, 0f);
                    Shout(SgDefSide, SdLadders, s.Pos, 1.5f);
                    break;
                case OTower:
                    Roar(m, s.Pos);
                    Shout(SgAttSide, SaTower, s.Pos, 0f);
                    Shout(SgDefSide, SdWalls, s.Pos, 1.5f);
                    break;
                case ORam:
                    _sgRamPos = s.Pos;
                    Shout(SgAttSide, SaRam, s.Pos, 0f);
                    Shout(SgDefSide, SdOil, s.Pos, 1.2f);
                    _sgNextOil = _now + 45f + (float)_rng.NextDouble() * 25f;
                    break;
                case OGate:
                case OBreach:
                    BreachRoar(m, s.Pos);
                    Shout(SgAttSide, SaBreach, s.Pos, 0.3f);
                    Shout(SgDefSide, SdBreach, s.Pos, 1.8f);
                    break;
            }
        }

        private static void Shout(int side, int kind, Vec3 pos, float delay)
        {
            if (_sgQ.Count >= SgQMax) { _cSgRej++; return; }
            _sgQ.Add(new SgQ { At = _now + delay, Side = side, Kind = kind, Pos = pos });
        }

        /// <summary>Okrzyk strony: mowca najblizszego szyku tej strony, klip regionu, bramka glosu druzyny jak rozkazy. true = zagral.</summary>
        private static bool TryShout(Mission m, int side, int kind, Vec3 pos)
        {
            int idx = side * SgKinds + kind;
            if (!_sgKindStamp[idx].Ready(side == SgAttSide ? SaGap[kind] : SdGap[kind])) { _cSgGap++; return false; }
            var f = NearestOfSide(m, side, pos, SgSpkRange);
            if (f == null) { _cSgNoSpk++; return false; }
            BasicCharacterObject ch;
            string cul = FormCul(f, out ch);
            if (Silent(ch, cul)) { _cSgMute++; return false; }
            var clip = SiegeClip(side == SgAttSide ? "sa" : "sd", cul, side == SgAttSide ? SaName[kind] : SdName[kind]);
            if (clip == null) { _cSgNoClip++; return false; }
            var spk = SpeakerOf(f);
            if (spk == null || IsSpeaking(spk)) { _cSgNoSpk++; return false; }
            var ts = TS(f.Team);
            if (!ts.OrderVoice.Ready(f.IsAIControlled ? OrderTeamGapAi : OrderTeamGapPlayer)) { _cSgBusy++; return false; }
            if (Play(m, clip, GVoice, HeadOf(spk), ShoutMin, ShoutMax, true, spk, null, side == SgAttSide ? "szturm" : "obrona") == null) { _cSgRej++; return false; }
            ts.OrderVoice.Hit();
            _sgKindStamp[idx].Hit();
            if (side == SgAttSide) _cSa[kind]++; else _cSd[kind]++;
            // po "Pour the oil!" - plonaca smola przy bramie (efekt, gdy klip jest)
            if (side == SgDefSide && kind == SdOil && Has("sg", "oil") && _sgFx.Count < SgFxMax)
                _sgFx.Add(new SgFx { At = _now + Math.Min(2.5f, clip.Dur + 0.3f), Code = "sg", Key = "oil", Tag = "smola", Pos = pos + new Vec3(0f, 0f, 2f), Min = OilMin, Max = OilMax });
            return true;
        }

        /// <summary>Ryk atakujacych przy machinie (raz na 6 s).</summary>
        private static void Roar(Mission m, Vec3 pos)
        {
            if (_sgAttSilent || !_sgRoarStamp.Ready(6f)) return;
            var cl = (_sgAttStyle != null ? Pick("sc", "roar_" + _sgAttStyle) : null) ?? Pick("sc", "roar_westeros");
            if (cl == null) { _cSgNoClip++; return; }
            if (Play(m, cl, GAmb, Toward(pos, _sgCenA, 12f, 0f), RoarMin, RoarMax, true, null, null, "ryk szturmu") == null) { _cSgRej++; return; }
            _sgRoarStamp.Hit();
            _cSgRoar++;
        }

        /// <summary>Wrzawa przy rozbitej bramie / wylomie (raz na 4 s).</summary>
        private static void BreachRoar(Mission m, Vec3 pos)
        {
            if (_sgAttSilent || !_sgBreachStamp.Ready(4f)) return;
            var cl = Pick("sc", "breach");
            if (cl == null) { _cSgNoClip++; return; }
            if (Play(m, cl, GAmb, pos, BreachMin, BreachMax, true, null, null, "wylom") == null) { _cSgRej++; return; }
            _sgBreachStamp.Hit();
            _cSgBreach++;
        }

        /// <summary>Liczba ludzi i srodek stron (z sojusznikami) oraz punkty szykow atakujacych od 5 ludzi.</summary>
        private static void SideScan(Mission m)
        {
            _sgAttPts.Clear();
            int na = 0, nd = 0;
            Vec2 sa = Vec2.Zero, sd = Vec2.Zero;
            var teams = m.Teams;
            for (int i = 0; teams != null && i < teams.Count; i++)
            {
                var t = teams[i];
                if (!MissionTeams.Valid(t)) continue;
                bool att = t.Side == BattleSideEnum.Attacker;
                if (!att && t.Side != BattleSideEnum.Defender) continue;
                var fl = t.FormationsIncludingSpecialAndEmpty;
                for (int j = 0; fl != null && j < fl.Count; j++)
                {
                    var f = fl[j];
                    if (f == null) continue;
                    int n = f.CountOfUnits;
                    if (n <= 0) continue;
                    var p = f.CachedAveragePosition;
                    if (att) { na += n; sa += p * n; if (n >= MinMenSgFight) _sgAttPts.Add(p); }
                    else { nd += n; sd += p * n; }
                }
            }
            _sgMenA = na; _sgMenD = nd;
            if (na > 0) _sgCenA = sa * (1f / na);
            if (nd > 0) _sgCenD = sd * (1f / nd);
        }

        private static bool NearKey(Vec2 p, float r)
        {
            float r2 = r * r;
            for (int i = 0; i < _sgKeys.Count; i++) if (_sgKeys[i].DistanceSquared(p) <= r2) return true;
            return false;
        }

        private static bool KeyNearest(Vec2 p, out Vec3 pos)
        {
            pos = Vec3.Zero;
            float best = float.MaxValue;
            bool ok = false;
            for (int i = 0; i < _sgObjs.Count; i++)
            {
                var s = _sgObjs[i];
                if (s.Type != OGate && s.Type != OBreach && s.Type != OLadder) continue;
                float d = s.Pos.AsVec2.DistanceSquared(p);
                if (d < best) { best = d; pos = s.Pos; ok = true; }
            }
            return ok;
        }

        private static bool AttackerNear(Vec3 pos, float r)
        {
            float r2 = r * r;
            var p = pos.AsVec2;
            for (int i = 0; i < _sgAttPts.Count; i++) if (_sgAttPts[i].DistanceSquared(p) <= r2) return true;
            return false;
        }

        private static bool AttackersNearWalls()
        {
            for (int i = 0; i < _sgAttPts.Count; i++) if (NearKey(_sgAttPts[i], SgNearWall)) return true;
            return false;
        }

        /// <summary>Szyk wrecz atakujacych (od 5 ludzi) w zwarciu (wrog do 15 m) do 40 m od murow / bramy.</summary>
        private static bool AttackersFighting(Mission m)
        {
            var teams = m.Teams;
            for (int i = 0; teams != null && i < teams.Count; i++)
            {
                var t = teams[i];
                if (!MissionTeams.Valid(t) || t.Side != BattleSideEnum.Attacker) continue;
                var fl = t.FormationsIncludingSpecialAndEmpty;
                for (int j = 0; fl != null && j < fl.Count; j++)
                {
                    var f = fl[j];
                    if (f == null || f.CountOfUnits < MinMenSgFight) continue;
                    if (EnemyDist(f) >= SgFightNear) continue;
                    try { var q = f.QuerySystem; if (q == null || q.IsRangedFormationReadOnly) continue; } catch { continue; }
                    if (NearKey(f.CachedAveragePosition, SgFightWall)) return true;
                }
            }
            return false;
        }

        private static Formation NearestOfSide(Mission m, int side, Vec3 pos, float range)
        {
            var want = side == SgAttSide ? BattleSideEnum.Attacker : BattleSideEnum.Defender;
            Formation best = null;
            float bd = range * range;
            var p = pos.AsVec2;
            var teams = m.Teams;
            for (int i = 0; teams != null && i < teams.Count; i++)
            {
                var t = teams[i];
                if (!MissionTeams.Valid(t) || t.Side != want) continue;
                var fl = t.FormationsIncludingSpecialAndEmpty;
                for (int j = 0; fl != null && j < fl.Count; j++)
                {
                    var f = fl[j];
                    if (f == null || f.CountOfUnits <= 0) continue;
                    float d = f.CachedAveragePosition.DistanceSquared(p);
                    if (d < bd) { bd = d; best = f; }
                }
            }
            return best;
        }

        /// <summary>Punkt przesuniety od miejsca w strone srodka strony o dist (2D) i w gore o up.</summary>
        private static Vec3 Toward(Vec3 from, Vec2 center, float dist, float up)
        {
            Vec2 d = center - from.AsVec2;
            float len = d.Length;
            if (len < 1f || center.x == 0f && center.y == 0f) return from + new Vec3(0f, 0f, up);
            d = d * (Math.Min(dist, len) / len);
            return new Vec3(from.x + d.x, from.y + d.y, from.z + up);
        }

        private static void MoveLoop(Loop l, Vec3 pos)
        {
            if (l == null) return;
            l.Pos = pos;
            if (l.Cur != null && _live.Contains(l.Cur)) { l.Cur.Pos = pos; try { l.Cur.Ev.SetPosition(pos); } catch { } }
        }

        // ============================================================ bebny oblezenia

        /// <summary>Linia bebnow 30 m za srodkiem atakujacych (od obroncow); wysokosc gruntu sceny.</summary>
        private static void DrumPlace(Mission m)
        {
            _sdPosOk = false;
            if (_sgMenA <= 0) return;
            Vec2 dir = _sgCenA - _sgCenD;
            float len = dir.Length;
            dir = len > 1f && _sgMenD > 0 ? dir * (1f / len) : new Vec2(1f, 0f);
            Vec2 p = _sgCenA + dir * SdBack;
            float z = _lis.z;
            try { var sc = m.Scene; if (sc != null) z = sc.GetGroundHeightAtPosition(new Vec3(p.x, p.y, 0f)); } catch { }
            _sdPos = new Vec3(p.x, p.y, z + 2f);
            _sdPosOk = true;
            if (_sdCur != null && _live.Contains(_sdCur)) { _sdCur.Pos = _sdPos; try { _sdCur.Ev.SetPosition(_sdPos); } catch { } }
        }

        private static void SiegeDrumTick(Mission m, Settings c)
        {
            if (_sdCur != null && !_live.Contains(_sdCur)) _sdCur = null;
            bool want = _sdWant && c.SiegeDrums && !m.MissionEnded;
            if (!want)
            {
                if (!_sdActive) return;
                // walka na murach: beben dobiega do konca taktu (cichnie), nowego taktu nie ma
                if (_sdHush && _sdCur != null && c.SiegeDrums && !m.MissionEnded) return;
                if (_sdCur != null) { Cut(_sdCur); _sdCur = null; }
                _sdActive = false;
                _cSdStop++;
                if (_sdHush) _cSdHushN++;
                return;
            }
            if (_sdCur != null)
            {
                float em = _now - _sdCur.T0;
                double er = Real - _sdCur.R0;
                if (em < _sdCur.C.Dur - 0.1f || er < 0.9 * _sdCur.C.Dur) return;   // takt trwa
            }
            string ph = _sgPhase >= 2 ? "assault" : "approach";
            if (_sdClipPhase != ph)
            {
                _sdClipPhase = ph;
                var cl = Pick("ds", _sdStyle + "_" + ph) ?? Pick("ds", _sdStyle + "_" + (ph == "assault" ? "approach" : "assault"));
                _sdFromMarch = false;
                if (cl == null) { cl = Pick("dr", _sdStyle); _sdFromMarch = cl != null; }
                if (cl == null) _cSdNoClip++;
                _sdClip = cl;
            }
            if (_sdClip == null) return;
            var s = Play(m, _sdClip, GDrum, _sdPos, SdMin, SdMax, true, null, null, "beben oblezenia");
            if (s == null) return;
            _sdCur = s;
            _cSdBeat++;
            if (_sgPhase >= 2) _cSdBeatAss++; else _cSdBeatAppr++;
            if (_sdFromMarch) _cSdFromMarch++;
            if (!_sdActive) { _sdActive = true; _cSdStart++; }
        }

        /// <summary>Koniec misji przed linia: aktywna linia bebnow liczy sie jako zatrzymana (start == stop).</summary>
        private static void SiegeFinish()
        {
            if (_sdActive) { _sdActive = false; _sdCur = null; _cSdStop++; }
        }

        /// <summary>OnClearScene (po Release wszystkich): zapomnij obiekty sceny - kolejny takt po rozstawieniu przeglada je od nowa.</summary>
        private static void SiegeSceneCleared()
        {
            _sgObjs.Clear(); _sgKeys.Clear(); _sgAttPts.Clear(); _sgQ.Clear(); _sgFx.Clear();
            _sgScanned = false; _sgHotOk = false; _sgOpen = false;
            if (_sgCrowdA != null) _sgCrowdA.Cur = null;
            if (_sgCrowdD != null) _sgCrowdD.Cur = null;
            if (_sgRamLoop != null) _sgRamLoop.Cur = null;
            SiegeFinish();
        }

        // ============================================================ linia misji

        private static string Sec(float at) { return at < 0f ? "-" : (at - _beganAt).ToString("0", Inv) + " s"; }

        private static void AppendSiege(StringBuilder sb, Settings c)
        {
            if (!_siege) return;
            sb.Append(" | szturm murow ").Append(c != null && c.SiegeAssaultCrowd ? "TAK" : "NIE");
            if (!_sgScanned && _sgPhase == 0) sb.Append(": obiekty nie przegladane (bez konca rozstawienia)");
            else
            {
                sb.Append(": drabin ").Append(_sgN[OLadder]).Append(" (w gore razy ").Append(_sgEv[OLadder]).Append("), wiez ").Append(_sgN[OTower])
                  .Append(" (u murow ").Append(_sgEv[OTower]).Append("), taranow ").Append(_sgN[ORam]).Append(" (u bramy ").Append(_sgEv[ORam])
                  .Append(", bil ").Append(_sgRamHitSec.ToString("0", Inv)).Append(" s), bram ").Append(_sgN[OGate]).Append(" (rozbite ").Append(_sgEv[OGate])
                  .Append("), wylomow ").Append(_sgN[OBreach]).Append(" (wejscie ").Append(_sgEv[OBreach]).Append(')');
                if (_sgScanBad > 0) sb.Append(", zle obiekty ").Append(_sgScanBad);
                sb.Append("; faza: podejscie od ").Append(Sec(_sgApproachAt)).Append(", szturm od ").Append(Sec(_sgAssaultAt)).Append(", walka na murach razy ")
                  .Append(_sgFightCount).Append(" (pierwsza od ").Append(Sec(_sgFightAt)).Append(')');
                int sa = 0, sd = 0;
                for (int i = 0; i < SgKinds; i++) { sa += _cSa[i]; sd += _cSd[i]; }
                sb.Append("; okrzyki atakujacych ").Append(sa).Append(" (");
                for (int i = 0; i < SgKinds; i++) sb.Append(i == 0 ? "" : ", ").Append(SaName[i]).Append(' ').Append(_cSa[i]);
                sb.Append("), obroncow ").Append(sd).Append(" (");
                for (int i = 0; i < SgKinds; i++) sb.Append(i == 0 ? "" : ", ").Append(SdName[i]).Append(' ').Append(_cSd[i]);
                sb.Append("); pominiete: odstep ").Append(_cSgGap).Append(", bez mowcy ").Append(_cSgNoSpk).Append(", strona milczaca ").Append(_cSgMute)
                  .Append(", brak klipu ").Append(_cSgNoClip).Append(", glos druzyny zajety ").Append(_cSgBusy).Append(", odrzucone ").Append(_cSgRej)
                  .Append(", przeterminowane ").Append(_cSgStale);
                sb.Append("; tlum: ryk ").Append(_cSgRoar).Append(", wylom ").Append(_cSgBreach).Append(", smola ").Append(_cSgOil).Append(", petle atak ").Append(_cSgLoopA)
                  .Append(" / obrona ").Append(_cSgLoopD).Append(", zaloga taranu ").Append(_cSgLoopRam).Append(" (styl atak ").Append(_sgAttStyle ?? "-")
                  .Append(_sgAttSilent ? " milczy" : "").Append(", obrona ").Append(_sgDefStyle ?? "-").Append(_sgDefSilent ? " milczy" : "").Append("; kultura atak ")
                  .Append(_sgAttCul ?? "?").Append(", obrona ").Append(_sgDefCul ?? "?").Append("); ROTBA okrzyki oblezenia ").Append(ForeignVoices.BaSiegeShouts ? "tak" : "nie");
            }
            sb.Append(" | bebny oblezenia ").Append(c != null && c.SiegeDrums ? "TAK" : "NIE").Append(": start ").Append(_cSdStart).Append(", takty ").Append(_cSdBeat)
              .Append(" (podejscie ").Append(_cSdBeatAppr).Append(", szturm ").Append(_cSdBeatAss).Append(", z bebna marszu ").Append(_cSdFromMarch).Append("), stop ")
              .Append(_cSdStop).Append(" (cisza przy walce na murach ").Append(_cSdHushN).Append("), brak klipu ").Append(_cSdNoClip).Append(" (styl ").Append(_sdStyle ?? "-")
              .Append(')').Append(c != null && c.SiegeDrums ? "; bebny marszu w oblezeniu ustepuja" : "");
        }
    }
}
