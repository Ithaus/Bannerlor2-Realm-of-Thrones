using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.DividableTasks;
using TMath = TaleWorlds.Library.MathF;

namespace Armoury
{
    /// <summary>
    /// 379 (Jeff 09.11 "jak celuja sie ... katapulta bo zawsze trafia w to samo miejsce" -> "tak zrob mniejcelne wieksza wariacja ale im wieksza
    /// inzynieria operatora tym cleniejsza"). CELNOSC MACHIN WEDLUG ENGINEERING + LOSOWY CEL. Dowod (dekompilacja 1.4.8, mb_full.cs):
    ///  - rozrzut gry: SetupProjectileToShoot -> GetBallisticErrorAppliedDirection(MaximumBallisticError): kat = Max x RandomFloat (Ballista 0.5 deg - takze
    ///    FireBallista i ShipBallista NavalDLC, Mangonel 1.5, Trebuchet 1.0 bazowa), predkosc stala; RBMCombat podmienia ShootProjectileAux (trebusz 7 odlamkow
    ///    1.5 deg +-5%, balista 1.0 deg) - dlatego rozrzut doklada sie w AddCustomMissile, wolanym ze ShootProjectile niezaleznie od tego, kto zrobil reszte;
    ///  - wybor czlowieka w szyku: ThreatSeeker.PrepareTargetFromTask - najblizszy machinie (x0.8-1.0), poprzedni x0.5 i trzymany, dopoki zyje
    ///    (= "zawsze w to samo miejsce"); wybor celu: FindMostDangerousThreat.Prepare - wartosci x0.9-1.1, biezacy cel x2 (trzymanie sie celu).
    /// ZMIANA (klucz SiegeEngineAccuracyBySkill; NIE = gra co do bitu):
    ///  (1) prefiks ShootProjectile losuje JEDEN wspolny blad strzalu (odchylenie w poziomie i w pionie, trojkatne -dod..+dod, oraz mnoznik predkosci
    ///      = rozrzut wzdluz toru), prefiks AddCustomMissile obraca kierunek kazdego pocisku tego strzalu (salwa odlamkow przesuwa sie razem, wlasny rozrzut
    ///      odlamkow zostaje). dod = max(0, cel(skill) - MaximumBallisticError instancji), cel liniowo od "przy 0" do "przy 300";
    ///      skill = max(Engineering operatora, 0.5 x Engineering inzyniera strony) - zwykli zolnierze ROT maja 0 (ROT-Troops.xml: brak Engineering),
    ///      inzynier ustawia i kalibruje machiny, ale nie celuje kazda sam; bohater przy machinie (gracz, towarzysz) liczy sie w pelni;
    ///  (2) postfiks PrepareTargetFromTask: gdy cel to szyk - losowy czlowiek szyku (do 6 prob, IsActive i CanShootAtAgent), inaczej zostaje wybor gry;
    ///  (3) postfiks Prepare: zdjete x2 biezacego celu, cele bez ForceTarget o wartosci >= udzial x najlepsza dostaja te sama wartosc (+-1% losowo) -
    ///      gra wybiera losowo sposrod podobnych. ForceTarget ("attackMe") bez zmian. Udzial 0 = (2) i (3) wylaczone.
    /// Wszystkie cele przez AccessTools.DeclaredMethod na klasie deklarujacej (niedziedziczone, niewirtualne). Wyjatek = potkniecie (3 pierwsze do logu),
    /// bez globalnego wylacznika. Koszt bez machin: zero (latki tylko na strzale i wyborze celu machiny, AddCustomMissile - jedno porownanie flagi).
    /// </summary>
    internal static class SiegeAim379
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const int ErrMax = 3, AgentTries = 6;
        private const float SideEngineerShare = 0.5f;

        private static bool _installed;
        private static string _installInfo = "NIE";
        private static AccessTools.FieldRef<RangedSiegeWeapon, Agent> _fShooter;
        private static AccessTools.FieldRef<RangedSiegeWeaponAi.ThreatSeeker, Agent> _fTargetAgent;
        private static AccessTools.FieldRef<FindMostDangerousThreat, Threat> _fCurThreat;
        private static MethodInfo _mBaseError;
        private static readonly Dictionary<Type, float> _baseError = new Dictionary<Type, float>();

        // blad biezacego strzalu (ustawia prefiks ShootProjectile, czysci finalizer)
        [ThreadStatic] private static bool _ctxOn;
        [ThreadStatic] private static float _ctxYaw, _ctxPitch, _ctxSpeed;

        // inzynierowie stron - liczone raz na misje
        private static Mission _engMission;
        private static readonly Dictionary<PartyBase, int> _partyEng = new Dictionary<PartyBase, int>();
        private static readonly int[] _sideEng = { -1, -1 };

        // ostatni cel kazdej machiny AI (zmiany celu)
        private sealed class Last { public Threat T; }
        private static readonly ConditionalWeakTable<RangedSiegeWeaponAi.ThreatSeeker, Last> _last = new ConditionalWeakTable<RangedSiegeWeaponAi.ThreatSeeker, Last>();

        // liczniki misji
        private static int _shots, _missiles, _agentPicks, _similarRolls, _targetChanges, _stumbles, _errs;
        private static double _sumSpread;
        private static readonly List<int> _opSkills = new List<int>(), _effSkills = new List<int>();

        private static bool On { get { var s = Settings.Current; return s != null && s.SiegeEngineAccuracyBySkill; } }
        private static bool TargetsOn { get { var s = Settings.Current; return s != null && s.SiegeEngineAccuracyBySkill && s.SiegeEngineSimilarTargetShare > 0f; } }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_errs >= ErrMax) return;
            _errs++;
            try { Log.Error("SiegeAim379." + where, e); } catch { }
        }

        internal static void Install(Harmony h)
        {
            if (_installed || h == null) return;
            _installed = true;
            int ok = 0;
            var miss = new List<string>();
            try { _fShooter = AccessTools.FieldRefAccess<RangedSiegeWeapon, Agent>("LastShooterAgent"); } catch (Exception e) { Stumble("Install.LastShooterAgent", e); }
            try { _fTargetAgent = AccessTools.FieldRefAccess<RangedSiegeWeaponAi.ThreatSeeker, Agent>("_targetAgent"); } catch (Exception e) { Stumble("Install._targetAgent", e); }
            try { _fCurThreat = AccessTools.FieldRefAccess<FindMostDangerousThreat, Threat>("_currentThreat"); } catch (Exception e) { Stumble("Install._currentThreat", e); }
            try { _mBaseError = AccessTools.PropertyGetter(typeof(RangedSiegeWeapon), "MaximumBallisticError"); } catch (Exception e) { Stumble("Install.MaximumBallisticError", e); }

            ok += One(h, AccessTools.DeclaredMethod(typeof(RangedSiegeWeapon), "ShootProjectile", Type.EmptyTypes), "RangedSiegeWeapon.ShootProjectile",
                      prefix: nameof(ShootPrefix), finalizer: nameof(ShootFinalizer), miss: miss);
            ok += One(h, AccessTools.DeclaredMethod(typeof(Mission), "AddCustomMissile"), "Mission.AddCustomMissile", prefix: nameof(MissilePrefix), miss: miss);
            if (_fTargetAgent != null)
                ok += One(h, AccessTools.DeclaredMethod(typeof(RangedSiegeWeaponAi.ThreatSeeker), "PrepareTargetFromTask"), "ThreatSeeker.PrepareTargetFromTask",
                          postfix: nameof(PickPostfix), miss: miss);
            else miss.Add("ThreatSeeker._targetAgent");
            if (_fCurThreat != null)
                ok += One(h, AccessTools.DeclaredMethod(typeof(FindMostDangerousThreat), "Prepare"), "FindMostDangerousThreat.Prepare",
                          postfix: nameof(PreparePostfix), miss: miss);
            else miss.Add("FindMostDangerousThreat._currentThreat");
            if (_fShooter == null) miss.Add("RangedSiegeWeapon.LastShooterAgent (strzelec = PilotAgent)");
            if (_mBaseError == null) miss.Add("MaximumBallisticError (baza 0)");
            _installInfo = ok + "/4" + (miss.Count > 0 ? " | brak: " + string.Join(", ", miss) : "");
            try { Log.Info("Celnosc machin (379): latki wpiete " + _installInfo + "."); } catch { }
        }

        private static int One(Harmony h, MethodBase target, string name, string prefix = null, string postfix = null, string finalizer = null, List<string> miss = null)
        {
            try
            {
                if (target == null) { miss?.Add(name); return 0; }
                h.Patch(target,
                        prefix: prefix != null ? new HarmonyMethod(typeof(SiegeAim379), prefix) : null,
                        postfix: postfix != null ? new HarmonyMethod(typeof(SiegeAim379), postfix) : null,
                        finalizer: finalizer != null ? new HarmonyMethod(typeof(SiegeAim379), finalizer) : null);
                return 1;
            }
            catch (Exception e) { Stumble("Install." + name, e); miss?.Add(name + " (" + e.GetType().Name + ")"); return 0; }
        }

        // ---------------- (1) rozrzut ----------------

        private static float Tri() { return MBRandom.RandomFloat + MBRandom.RandomFloat - 1f; }   // trojkatny -1..1, wiecej przy srodku

        private static void ShootPrefix(RangedSiegeWeapon __instance)
        {
            _ctxOn = false;
            if (!On || __instance == null) return;
            try
            {
                var s = Settings.Current;
                Agent a = _fShooter != null ? _fShooter(__instance) : null;
                if (a == null) a = __instance.PilotAgent;
                if (a == null && __instance.PlayerForceUse) a = Agent.Main;   // NavalDLC ShipBallista: gracz strzela "na sile" (strzelec = Agent.Main dopiero w ShootProjectileAux)
                int op = OperatorSkill(a);
                int sideEng = SideEngineer(a);
                int skill = Math.Max(op, (int)(SideEngineerShare * sideEng));
                float t = TMath.Clamp(skill / 300f, 0f, 1f);
                bool ballista = __instance is Ballista;
                float at0 = ballista ? s.SiegeEngineSpreadBallistaAt0 : s.SiegeEngineSpreadThrowersAt0;
                float at300 = ballista ? s.SiegeEngineSpreadBallistaAt300 : s.SiegeEngineSpreadThrowersAt300;
                float target = Math.Max(0f, at0 + (at300 - at0) * t);
                float extra = Math.Max(0f, target - BaseError(__instance));
                float vs = Math.Max(0f, s.SiegeEngineRangeSpreadAt0 + (s.SiegeEngineRangeSpreadAt300 - s.SiegeEngineRangeSpreadAt0) * t);
                _ctxYaw = (extra * Tri()) * (TMath.PI / 180f);
                _ctxPitch = (extra * Tri()) * (TMath.PI / 180f);
                _ctxSpeed = TMath.Clamp(1f + vs * Tri(), 0.5f, 1.5f);
                _ctxOn = extra > 0f || vs > 0f;
                _shots++;
                _sumSpread += target;
                _opSkills.Add(op);
                _effSkills.Add(skill);
            }
            catch (Exception e) { _ctxOn = false; Stumble("ShootPrefix", e); }
        }

        private static Exception ShootFinalizer(Exception __exception)
        {
            _ctxOn = false;
            return __exception;
        }

        private static void MissilePrefix(ref Vec3 direction, ref Mat3 orientation, ref float baseSpeed, ref float speed)
        {
            if (!_ctxOn) return;
            try
            {
                direction = Turn(direction, _ctxYaw, _ctxPitch);
                var o = orientation;
                o.f = Turn(o.f, _ctxYaw, _ctxPitch);
                o.u = Vec3.Up;
                o.Orthonormalize();
                orientation = o;
                baseSpeed *= _ctxSpeed;
                speed *= _ctxSpeed;
                _missiles++;
            }
            catch (Exception e) { Stumble("MissilePrefix", e); }
        }

        // obrot kierunku: w poziomie wokol pionu, potem w pionie wokol osi bocznej (jak GetBallisticErrorAppliedDirection gry)
        private static Vec3 Turn(Vec3 d, float yaw, float pitch)
        {
            if (Math.Abs(d.z) > 0.98f || d.LengthSquared < 1e-6f) return d;   // strzal pionowy - bez obrotu
            float len = d.Length;
            var m = new Mat3 { f = d, u = Vec3.Up };
            m.Orthonormalize();
            m.RotateAboutUp(yaw);
            m.RotateAboutSide(pitch);
            return m.f * len;
        }

        private static float BaseError(RangedSiegeWeapon w)
        {
            var t = w.GetType();
            if (_baseError.TryGetValue(t, out float v)) return v;
            v = 0f;
            try { if (_mBaseError != null) v = Convert.ToSingle(_mBaseError.Invoke(w, null), Inv); }
            catch (Exception e) { Stumble("BaseError", e); }
            _baseError[t] = v;
            return v;
        }

        private static int OperatorSkill(Agent a)
        {
            try { return a != null && a.Character != null ? Math.Max(0, a.Character.GetSkillValue(DefaultSkills.Engineering)) : 0; }
            catch (Exception e) { Stumble("OperatorSkill", e); return 0; }
        }

        // inzynier strony: wiekszy z (inzynier oddzialu operatora, inzynier strony oblezenia gry); 0 poza kampania
        private static int SideEngineer(Agent a)
        {
            if (a == null || Campaign.Current == null) return 0;
            var m = Mission.Current;
            if (!ReferenceEquals(m, _engMission)) { _engMission = m; _partyEng.Clear(); _sideEng[0] = _sideEng[1] = -1; }
            int best = 0;
            try
            {
                var p = a.Origin != null ? a.Origin.BattleCombatant as PartyBase : null;
                if (p != null)
                {
                    if (!_partyEng.TryGetValue(p, out int e)) { e = PartyEngineer(p); _partyEng[p] = e; }
                    best = e;
                }
            }
            catch (Exception e) { Stumble("SideEngineer.Party", e); }
            try
            {
                var team = a.Team;
                if (team != null && (team.Side == BattleSideEnum.Attacker || team.Side == BattleSideEnum.Defender))
                {
                    int i = team.Side == BattleSideEnum.Attacker ? 0 : 1;
                    if (_sideEng[i] < 0)
                    {
                        _sideEng[i] = 0;
                        SiegeEvent se = PlayerSiege.PlayerSiegeEvent;
                        if (se == null) { var me = MapEvent.PlayerMapEvent; se = me != null && me.MapEventSettlement != null ? me.MapEventSettlement.SiegeEvent : null; }
                        var model = Campaign.Current.Models != null ? Campaign.Current.Models.SiegeEventModel : null;
                        var mp = se != null && model != null ? model.GetEffectiveSiegePartyForSide(se, team.Side) : null;
                        if (mp != null) _sideEng[i] = PartyEngineer(mp.Party);
                    }
                    best = Math.Max(best, _sideEng[i]);
                }
            }
            catch (Exception e) { Stumble("SideEngineer.Side", e); }
            return best;
        }

        private static int PartyEngineer(PartyBase p)
        {
            if (p == null) return 0;
            Hero h = null;
            var mp = p.MobileParty;
            if (mp != null)
            {
                h = mp.EffectiveEngineer;
                if (h == null && (mp.IsGarrison || mp.IsMilitia))
                {
                    var st = mp.CurrentSettlement ?? mp.HomeSettlement;
                    h = st != null && st.Town != null ? st.Town.Governor : null;   // garnizon bez dowodcy - zarzadca
                }
            }
            else if (p.IsSettlement && p.Settlement != null && p.Settlement.Town != null) h = p.Settlement.Town.Governor;
            return h != null ? Math.Max(0, h.GetSkillValue(DefaultSkills.Engineering)) : 0;
        }

        // ---------------- (2) losowy czlowiek szyku ----------------

        private static void PickPostfix(RangedSiegeWeaponAi.ThreatSeeker __instance, Threat __result)
        {
            if (!TargetsOn || __instance == null) return;
            try
            {
                if (__result != null && __result.TargetableObject == null && __result.Formation != null)
                {
                    var f = __result.Formation;
                    var w = __instance.Weapon;
                    int n = f.CountOfUnits;
                    if (w != null && n > 1)
                    {
                        for (int i = 0; i < AgentTries; i++)
                        {
                            var ag = f.GetUnitWithIndex(MBRandom.RandomInt(n));
                            if (ag == null || !ag.IsActive() || !w.CanShootAtAgent(ag)) continue;
                            __result.Agent = ag;
                            _fTargetAgent(__instance) = ag;
                            _agentPicks++;
                            break;
                        }
                    }
                }
                // zmiana celu machiny (inna machina / inny szyk niz poprzednio)
                var last = _last.GetOrCreateValue(__instance);
                if (__result != null)
                {
                    if (last.T != null && !last.T.Equals(__result)) _targetChanges++;
                    last.T = __result;
                }
            }
            catch (Exception e) { Stumble("PickPostfix", e); }
        }

        // ---------------- (3) losowy wybor sposrod podobnych celow ----------------

        private static void PreparePostfix(FindMostDangerousThreat __instance, List<Threat> threats)
        {
            if (!TargetsOn || __instance == null || threats == null || threats.Count == 0) return;
            try
            {
                var cur = _fCurThreat(__instance);
                if (cur != null) cur.ThreatValue *= 0.5f;   // zdjete x2 gry dla biezacego celu
                float max = float.MinValue;
                for (int i = 0; i < threats.Count; i++)
                {
                    var t = threats[i];
                    if (t != null && !t.ForceTarget && t.ThreatValue > max) max = t.ThreatValue;
                }
                if (max <= 0f) return;
                float lim = max * TMath.Clamp(Settings.Current.SiegeEngineSimilarTargetShare, 0f, 1f);
                int n = 0;
                for (int i = 0; i < threats.Count; i++)
                {
                    var t = threats[i];
                    if (t != null && !t.ForceTarget && t.ThreatValue >= lim) n++;
                }
                if (n < 2) return;
                for (int i = 0; i < threats.Count; i++)
                {
                    var t = threats[i];
                    if (t != null && !t.ForceTarget && t.ThreatValue >= lim) t.ThreatValue = max * (1f + 0.01f * MBRandom.RandomFloat);
                }
                _similarRolls++;
            }
            catch (Exception e) { Stumble("PreparePostfix", e); }
        }

        // ---------------- misja: zerowanie i linia konca ----------------

        private static void Reset()
        {
            _shots = _missiles = _agentPicks = _similarRolls = _targetChanges = _stumbles = _errs = 0;
            _sumSpread = 0;
            _opSkills.Clear();
            _effSkills.Clear();
            _engMission = null;
            _partyEng.Clear();
            _sideEng[0] = _sideEng[1] = -1;
            _ctxOn = false;
        }

        private static string MinMedMax(List<int> l)
        {
            if (l.Count == 0) return "-";
            var c = new List<int>(l);
            c.Sort();
            return c[0] + "/" + c[c.Count / 2] + "/" + c[c.Count - 1];
        }

        internal sealed class Logic : MissionLogic
        {
            public override MissionBehaviorType BehaviorType { get { return MissionBehaviorType.Other; } }

            public override void OnBehaviorInitialize()
            {
                base.OnBehaviorInitialize();
                Reset();
            }

            protected override void OnEndMission()
            {
                try
                {
                    if (_shots > 0 || _agentPicks > 0 || _similarRolls > 0 || _stumbles > 0)
                    {
                        Log.Info("Celnosc machin (379): strzalow " + _shots + " (pociskow " + _missiles + "), sredni rozrzut "
                                 + (_shots > 0 ? (_sumSpread / _shots).ToString("0.00", Inv) : "-") + " deg, operatorzy Engineering min/med/max "
                                 + MinMedMax(_opSkills) + " (z inzynierem strony " + MinMedMax(_effSkills) + "), cele losowe szyku " + _agentPicks
                                 + ", losowania wsrod podobnych " + _similarRolls + ", zmiany celu " + _targetChanges + ", potkniecia " + _stumbles
                                 + " | latki " + _installInfo + ".");
                    }
                }
                catch (Exception e) { Stumble("OnEndMission", e); }
                Reset();
                base.OnEndMission();
            }
        }
    }
}
