using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 484 (Jeff 13.11: "wygladam jak jez ... moge miec co najwyzej 1-2 strzaly, max 3; kon tylko ta, ktora go zabila"):
    /// wbite pociski tylko od prawdziwych ran, najwyzej StuckMissilesMaxHuman w czlowieku, w koniu tylko pocisk smiertelny.
    ///  (1) prefiks + finalizer Mission.MissileHitCallback: [ThreadStatic] schowek (indeks pocisku, ofiara, HP przed, czy zywa, attachGlobalFrame) - same odczyty.
    ///  (2) prefiks Mission.HandleMissileCollisionReaction z Priority.First (PRZED zastepujacym prefiksem RBM): Stick w cialo (nie w tarcze),
    ///      ktory nie spelnia zasady -> BounceBack z ramka GLOBALNA (wzor gry Mission.cs:5937-5938) i zerowa predkoscia - pocisk spada
    ///      w miejscu trafienia i lezy do podniesienia (jak zwykle odbicie); AmmoBreaksOnBounceBack -> BecomeInvisible (jak gra).
    /// Obrazenia, glos bolu, krew, OnAgentHit / OnMissileHit, wynik MissileHitCallback - bez zmian (Stick i BounceBack oba != PassThrough).
    /// Juz wbitych pociskow nie ruszamy (bez DeleteAttachedWeapon). Klucz NIE = prefiks wychodzi od razu (gra / RBM co do bitu).
    /// Bramka _active otwierana przez Logic (misja kampanii, klucz TAK), zamykana na koncu misji; GameNetwork.IsSessionActive = nic.
    /// </summary>
    internal static class StuckMissiles484
    {
        private static bool _preOk, _hmcrOk;
        private static volatile bool _active;

        // ------------------------------------------------------------ schowek trafienia (watek trafienia)
        [ThreadStatic] private static int _idx;           // -1 = brak (0 to poprawny indeks - znacznik osobno)
        [ThreadStatic] private static bool _has;
        [ThreadStatic] private static Agent _victim;
        [ThreadStatic] private static float _hpBefore;
        [ThreadStatic] private static bool _wasActive;
        [ThreadStatic] private static MatrixFrame _globalFrame;

        // ------------------------------------------------------------ liczniki misji (Interlocked)
        private static int _kept, _weak, _overCap, _horse, _broken, _stum;
        private static string _stumWhere;

        /// <summary>Bramka misji otwarta (wpieta, kampania) i klucz TAK - FieldCraft (JEZ ZE STRZAL) nie odpina wtedy strzal w ticku.</summary>
        internal static bool Owns
        {
            get
            {
                try { return _active && Settings.Current != null && Settings.Current.StuckMissilesLimit; }
                catch { return false; }
            }
        }

        internal static void ApplyAll(Harmony h)
        {
            if (h == null) { Log.Info("Wbite pociski (484): NIE wpiete - brak instancji Harmony."); return; }
            MethodInfo mhc = null, hmcr = null;
            var pre = Own(nameof(HitPre));
            var fin = Own(nameof(HitFin));
            var dec = Own(nameof(ReactionPre));
            try
            {
                mhc = AccessTools.DeclaredMethod(typeof(Mission), "MissileHitCallback");
                if (mhc != null && pre != null && fin != null)
                {
                    h.Patch(mhc, prefix: new HarmonyMethod(pre), finalizer: new HarmonyMethod(fin));
                    _preOk = Has(mhc, h.Id, pre, 'p') && Has(mhc, h.Id, fin, 'f');
                }
            }
            catch (Exception e) { _preOk = false; Log.Error("StuckMissiles484.ApplyAll (MissileHitCallback)", e); }
            string order = "-";
            try
            {
                hmcr = AccessTools.DeclaredMethod(typeof(Mission), "HandleMissileCollisionReaction");
                if (_preOk && hmcr != null && dec != null)
                {
                    var hm = new HarmonyMethod(dec) { priority = Priority.First };
                    h.Patch(hmcr, prefix: hm);
                    _hmcrOk = Has(hmcr, h.Id, dec, 'p');
                    order = PrefixOrder(hmcr, h.Id);
                }
            }
            catch (Exception e) { _hmcrOk = false; Log.Error("StuckMissiles484.ApplyAll (HandleMissileCollisionReaction)", e); }
            if (!_hmcrOk && _preOk)
            {
                // bez decyzji schowek zbedny - zdejmujemy tym samym MethodInfo
                try { h.Unpatch(mhc, pre); h.Unpatch(mhc, fin); } catch (Exception e) { Log.Error("StuckMissiles484.ApplyAll (zdjecie)", e); }
                _preOk = false;
            }
            Log.Info("Wbite pociski (484): " + (_preOk && _hmcrOk ? "wpiete" : "NIE wpiete")
                     + " - schowek MissileHitCallback " + (_preOk ? "1/1" : (mhc == null ? "NIE (brak celu)" : "NIE"))
                     + ", decyzja HandleMissileCollisionReaction " + (_hmcrOk ? "1/1" : (hmcr == null ? "NIE (brak celu)" : "NIE"))
                     + " | kolejnosc prefiksow: " + order + ".");
        }

        private static MethodInfo Own(string name)
        {
            return typeof(StuckMissiles484).GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        }

        private static bool Has(MethodBase target, string owner, MethodInfo patch, char kind)
        {
            var pi = Harmony.GetPatchInfo(target);
            if (pi == null) return false;
            IEnumerable<Patch> list = kind == 'p' ? pi.Prefixes : pi.Finalizers;
            if (list == null) return false;
            foreach (var p in list) if (p != null && p.owner == owner && p.PatchMethod == patch) return true;
            return false;
        }

        /// <summary>Prefiksy celu w kolejnosci Harmony (priorytet malejaco, potem indeks) - nasz ma byc przed RBM.</summary>
        private static string PrefixOrder(MethodBase target, string ownId)
        {
            try
            {
                var pi = Harmony.GetPatchInfo(target);
                if (pi == null || pi.Prefixes == null) return "-";
                var l = new List<Patch>(pi.Prefixes);
                l.Sort((a, b) => a.priority != b.priority ? b.priority.CompareTo(a.priority) : a.index.CompareTo(b.index));
                var sb = new StringBuilder();
                bool ownSeen = false, otherBefore = false;
                foreach (var p in l)
                {
                    if (sb.Length > 0) sb.Append(" > ");
                    sb.Append(p.owner == ownId ? "Armoury" : p.owner).Append('(').Append(p.priority).Append(')');
                    if (p.owner == ownId) ownSeen = true; else if (!ownSeen) otherBefore = true;
                }
                if (otherBefore) sb.Append(" UWAGA: cudzy prefiks przed naszym");
                return sb.ToString();
            }
            catch (Exception e) { return "blad odczytu (" + e.GetType().Name + ")"; }
        }

        private static void Stumble(string where, Exception e)
        {
            int n = Interlocked.Increment(ref _stum);
            if (n == 1)
            {
                _stumWhere = where + ": " + e.GetType().Name + " " + e.Message;
                try { Log.Error("StuckMissiles484." + where, e); } catch { }
            }
        }

        // ------------------------------------------------------------ latki

        /// <summary>Prefiks MissileHitCallback: schowek przed trafieniem (same odczyty: Health, State).</summary>
        public static void HitPre(ref AttackCollisionData collisionData, MatrixFrame attachGlobalFrame, Agent victim)
        {
            _has = false;
            if (!_active) return;
            try
            {
                _idx = collisionData.AffectorWeaponSlotOrMissileIndex;
                _victim = victim;
                _hpBefore = victim != null ? victim.Health : 0f;
                _wasActive = victim != null && victim.State == AgentState.Active;
                _globalFrame = attachGlobalFrame;
                _has = true;
            }
            catch (Exception e) { _has = false; Stumble("HitPre", e); }
        }

        /// <summary>Finalizer MissileHitCallback: schowek zdjety (takze po wyjatku).</summary>
        public static void HitFin()
        {
            _has = false;
            _victim = null;
        }

        /// <summary>Prefiks HandleMissileCollisionReaction (Priority.First, przed RBM): Stick w cialo bez rany / ponad limit / kon bez smierci -> BounceBack.</summary>
        public static void ReactionPre(Dictionary<int, Mission.Missile> ____missilesDictionary, int missileIndex,
            ref Mission.MissileCollisionReaction collisionReaction, ref MatrixFrame attachLocalFrame, ref bool isAttachedFrameLocal,
            Agent attachedAgent, bool attachedToShield, ref MissionObject attachedMissionObject,
            ref Vec3 bounceBackVelocity, ref Vec3 bounceBackAngularVelocity)
        {
            if (!_active || !_has) return;
            if (collisionReaction != Mission.MissileCollisionReaction.Stick || attachedAgent == null || attachedToShield) return;
            if (missileIndex != _idx || !ReferenceEquals(attachedAgent, _victim)) return;
            try
            {
                var c = Settings.Current;
                if (c == null || !c.StuckMissilesLimit || GameNetwork.IsSessionActive) return;
                Mission.Missile mi;
                if (____missilesDictionary == null || !____missilesDictionary.TryGetValue(missileIndex, out mi) || mi == null) return;
                var u = mi.Weapon.CurrentUsageItem;
                if (u == null) return;

                bool fatal = _wasActive && (attachedAgent.State != AgentState.Active || attachedAgent.Health <= 0f);
                float dmg = _hpBefore - attachedAgent.Health;
                int why;   // 0 zostaje, 1 slaba rana, 2 ponad limit, 3 kon bez smierci
                if (attachedAgent.IsMount)
                {
                    if (!c.StuckMissilesHorseOnlyFatal) return;   // klucz NIE - kon jak dotad (gra / RBM)
                    why = fatal ? 0 : 3;
                }
                else
                {
                    var wc = u.WeaponClass;
                    bool thrown = wc == WeaponClass.Javelin || wc == WeaponClass.ThrowingAxe || wc == WeaponClass.ThrowingKnife;
                    int min = thrown ? c.JavelinStickMinDamage : c.StuckMissilesMinDamage;
                    int n = attachedAgent.GetAttachedWeaponsCount();   // przed wbiciem tego pocisku (tylko kosci, bez tarczy)
                    if (n >= Math.Max(0, c.StuckMissilesMaxHuman)) why = 2;
                    else if (fatal || dmg >= min) why = 0;
                    else why = 1;
                }
                if (why == 0) { Interlocked.Increment(ref _kept); return; }

                // ramka GLOBALNA jak galaz "else" gry (Mission.cs:5937-5938) - ramka Stick w cialo jest lokalna wzgledem kosci
                var start = u.GetMissileStartingFrame();
                MatrixFrame g = _globalFrame.TransformToParent(start.TransformToParent(u.StickingFrame)).TransformToParent(start);
                g.origin.z = Math.Max(g.origin.z, -100f);

                bool breaks = (u.WeaponFlags & WeaponFlags.AmmoBreakOnBounceBackMask) == WeaponFlags.AmmoBreaksOnBounceBack;
                attachLocalFrame = g;
                isAttachedFrameLocal = false;
                attachedMissionObject = null;
                bounceBackVelocity = Vec3.Zero;
                bounceBackAngularVelocity = Vec3.Zero;
                collisionReaction = breaks ? Mission.MissileCollisionReaction.BecomeInvisible : Mission.MissileCollisionReaction.BounceBack;
                if (breaks) Interlocked.Increment(ref _broken);
                if (why == 1) Interlocked.Increment(ref _weak);
                else if (why == 2) Interlocked.Increment(ref _overCap);
                else Interlocked.Increment(ref _horse);
            }
            catch (Exception e) { Stumble("ReactionPre", e); }   // argumenty nietkniete do chwili przypisania - wbicie jak gra / RBM
        }

        // ------------------------------------------------------------ misja

        private static void Begin()
        {
            _kept = _weak = _overCap = _horse = _broken = _stum = 0;
            _stumWhere = null;
            _active = _preOk && _hmcrOk && !GameNetwork.IsSessionActive;
        }

        private static void End(string how)
        {
            if (!_active) return;
            _active = false;
            int total = _kept + _weak + _overCap + _horse;
            if (total == 0 && _stum == 0) return;
            var sb = new StringBuilder(220);
            sb.Append("Wbite pociski (484): wbite ").Append(_kept)
              .Append(", odbite slabe ").Append(_weak)
              .Append(", ponad limit ").Append(_overCap)
              .Append(", kon niesmiertelne ").Append(_horse)
              .Append(", potkniecia ").Append(_stum);
            if (_stum > 0 && _stumWhere != null) sb.Append(" (pierwsze: ").Append(_stumWhere).Append(')');
            sb.Append(" | z odbitych peklo (AmmoBreaksOnBounceBack, jak gra) ").Append(_broken);
            sb.Append(" | koniec: ").Append(how).Append('.');
            Log.Info(sb.ToString());
        }

        /// <summary>Bramka misji (SubModuleMain.OnMissionBehaviorInitialize przy kluczu i kampanii) + linia licznikow na koncu misji.</summary>
        internal sealed class Logic : MissionBehavior
        {
            public override MissionBehaviorType BehaviorType { get { return MissionBehaviorType.Other; } }

            public override void EarlyStart()
            {
                try { Begin(); } catch (Exception e) { _active = false; Stumble("Begin", e); }
                base.EarlyStart();
            }

            protected override void OnEndMission()
            {
                try { End("koniec misji"); } catch (Exception e) { _active = false; Log.Error("StuckMissiles484.OnEndMission", e); }
                base.OnEndMission();
            }

            public override void OnRemoveBehavior()
            {
                try { End("zdjete zachowanie"); } catch (Exception e) { _active = false; Log.Error("StuckMissiles484.OnRemoveBehavior", e); }
                base.OnRemoveBehavior();
            }
        }
    }
}
