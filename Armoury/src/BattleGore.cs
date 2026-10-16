using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// NB2 czesc 2 (16.10, Jeff: "wiecej krwi"): KREW TAM, GDZIE CIOS PRZEBIL PANCERZ (klucz BloodThroughArmour). Rozpoznanie rozp_mody (16.10) krok 5.
    ///
    /// Dowod [K]: RBMCombat (RealisticWeaponCollision.cs:111-140, postfiks Mission.DecideAgentHitParticles) daje "pot" (psys_game_sweat_sword_enter)
    /// zamiast bryzgu krwi przy cieciu lub obuchu w kolczuge albo plyty (material czesci ciala 3/4, DamageType 0/2) i przy kazdym trafieniu za 20 lub
    /// mniej - takze wtedy, gdy cios przeszedl przez pancerz i zadal duze obrazenia. Przy ArmorMultiplier 2 i opancerzonych wojskach ROT ciezkie rany
    /// nie krwawia. Gra ma natywne Agent.CreateBloodBurstAtLimb(kosc, skala) - tego samego uzywa DismembermentPlus (w OnRegisterBlow).
    ///
    /// Zasady (bez Harmony, bez latania DecideAgentHitParticles - RBM zostaje):
    ///  - tylko bitwa na mapie / kryjowka / bitwa wlasna (BattleSense.InBattle), tylko przy RBMCombat (rbmCombatEnabled - bez niego gra daje krew sama);
    ///  - cios wrecz bronia z krwia (nie pocisk, nie kopniak / uderzenie tarcza, nie tarcza na plecach, nie szarza konia, nie upadek), ofiara czlowiek;
    ///  - krew, gdy RBM dal pot, a cios przeszedl: pancerz kolczy / plytowy, ciecie albo obuch i obrazenia >= 30; albo cios smiertelny, ktoremu RBM dal
    ///    pot (pancerz albo obrazenia <= 20);
    ///  - OnAgentHit biegnie PRZED Die (Agent.HandleBlow) - ofiara jeszcze aktywna, wskaznik silnika wazny; kosc trafienia (CollisionBoneIndex - ta
    ///    sama, ktorej gra uzywa dla ofiary w GetProtectorArmorMaterialOfBone) sprawdzona: >= 0 i mniejsza niz liczba kosci szkieletu ofiary;
    ///  - skala 0.5..1.0 wedlug obrazen (smiertelny co najmniej 0.8); limit: 1 na czlowieka na 0.5 s, 20 na sekunde w misji (nadmiar liczony);
    ///  - wyjatek liczony (RoutHold.Stumble, miejsce "krew") - wywolanie zwrotne silnika nie moze go wypuscic.
    /// </summary>
    internal static class BattleGore
    {
        internal const int MinDamage = 30;
        internal const float PerManEvery = 0.5f;
        internal const int PerSecond = 20;

        // stan misji (pod RoutHold.Gate - OnAgentHit moze przyjsc z ticku asynchronicznego)
        private static readonly Dictionary<int, float> _last = new Dictionary<int, float>();
        private static WeakReference _m;
        private static int _sec = int.MinValue, _secCount;
        private static bool _rbmCombat;

        // ---- liczniki misji (pod RoutHold.Gate; zerowane w RoutHold.Ensure)
        private static int _bursts, _armour, _lethal, _limited, _badBone;

        internal static void ResetCounters() { _bursts = _armour = _lethal = _limited = _badBone = 0; }

        internal static int Activity { get { return _bursts + _limited + _badBone; } }

        internal static void Append(StringBuilder sb, Settings c)
        {
            sb.Append(" | krew przez pancerz ").Append(c != null && c.BloodThroughArmour ? "TAK" : "NIE").Append(" (RBMCombat ").Append(_rbmCombat ? "TAK" : "NIE")
              .Append("): bryzgi ").Append(_bursts).Append(" (przez pancerz ").Append(_armour).Append(", smiertelne ").Append(_lethal)
              .Append("), odrzucone limitem ").Append(_limited).Append(", zla kosc ").Append(_badBone);
        }

        /// <summary>AfterStart (BattleSense, glowny watek): RBMCombat czynny w tej misji (typ latki i RBMConfig.rbmCombatEnabled).</summary>
        internal static void Begin(Mission m)
        {
            bool on = false;
            try
            {
                if (QuartermasterLaw.FindType("RBMCombat.RealisticWeaponCollision") != null)
                {
                    var t = QuartermasterLaw.FindType("RBMConfig.RBMConfig");
                    var f = t != null ? t.GetField("rbmCombatEnabled", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static) : null;
                    object v = f != null ? f.GetValue(null) : null;
                    on = v is bool ? (bool)v : true;
                }
            }
            catch { on = false; }
            lock (RoutHold.Gate)
            {
                RoutHold.EnsureLocked(m);
                _rbmCombat = on;
                _last.Clear();
                _sec = int.MinValue;
                _secCount = 0;
                _m = new WeakReference(m);
            }
        }

        internal static void Clear()
        {
            lock (RoutHold.Gate) { _last.Clear(); _m = null; _sec = int.MinValue; _secCount = 0; }
        }

        /// <summary>Material pancerza czesci ciala - to samo przypisanie co RBM (RealisticWeaponCollision.GetMateirialTypeofHitBodyPart) i gra.</summary>
        private static ArmorComponent.ArmorMaterialTypes MaterialOf(Agent victim, BoneBodyPartType part)
        {
            EquipmentIndex idx;
            switch (part)
            {
                case BoneBodyPartType.Head:
                case BoneBodyPartType.Neck: idx = EquipmentIndex.Head; break;
                case BoneBodyPartType.Chest:
                case BoneBodyPartType.Abdomen:
                case BoneBodyPartType.ShoulderLeft:
                case BoneBodyPartType.ShoulderRight: idx = EquipmentIndex.Body; break;
                case BoneBodyPartType.ArmLeft:
                case BoneBodyPartType.ArmRight: idx = EquipmentIndex.Gloves; break;
                case BoneBodyPartType.Legs: idx = EquipmentIndex.Leg; break;
                default: return ArmorComponent.ArmorMaterialTypes.None;
            }
            var eq = victim.SpawnEquipment;
            if (eq == null) return ArmorComponent.ArmorMaterialTypes.None;
            var item = eq[idx].Item;
            if (item == null || item.ArmorComponent == null) return ArmorComponent.ArmorMaterialTypes.None;
            return item.ArmorComponent.MaterialType;
        }

        /// <summary>OnAgentHit (BattleSense) - wywolanie zwrotne silnika; wolajacy lapie wyjatki.</summary>
        internal static void OnHit(Mission m, Agent victim, Agent attacker, in Blow blow, in AttackCollisionData col)
        {
            var c = Settings.Current;
            if (c == null || !c.BloodThroughArmour || m == null || victim == null || attacker == null || attacker == victim) return;
            if (!_rbmCombat || !BattleSense.InBattle(m)) return;
            if (col.IsMissile || col.IsAlternativeAttack || col.AttackBlockedWithShield || col.CollidedWithShieldOnBack || col.IsFallDamage || col.IsHorseCharge) return;
            if (!victim.IsHuman || !victim.IsActive()) return;
            var rec = blow.WeaponRecord;
            if (!rec.HasWeapon() || (rec.WeaponFlags & WeaponFlags.NoBlood) != 0) return;
            int dmg = blow.InflictedDamage;
            if (dmg <= 0) return;
            bool lethal = victim.Health < 1f;                             // HandleBlow ustawil zdrowie przed OnAgentHit, Die jeszcze nie bylo
            var mat = MaterialOf(victim, col.VictimHitBodyPart);
            int dt = col.DamageType;
            bool armourSweat = (mat == ArmorComponent.ArmorMaterialTypes.Chainmail || mat == ArmorComponent.ArmorMaterialTypes.Plate)
                               && (dt == (int)DamageTypes.Cut || dt == (int)DamageTypes.Blunt);
            bool through = armourSweat && dmg >= MinDamage;
            bool deathSweat = lethal && (armourSweat || dmg <= 20);
            if (!through && !deathSweat) return;
            sbyte bone = col.CollisionBoneIndex;
            float now = m.CurrentTime;
            lock (RoutHold.Gate)
            {
                RoutHold.EnsureLocked(m);
                if (bone < 0) { _badBone++; return; }
                if (_m == null || !ReferenceEquals(_m.Target, m)) { _last.Clear(); _sec = int.MinValue; _secCount = 0; _m = new WeakReference(m); }
                float last;
                if (_last.TryGetValue(victim.Index, out last) && now - last < PerManEvery) { _limited++; return; }
                int s = (int)Math.Floor(now);
                if (s != _sec) { _sec = s; _secCount = 0; }
                if (_secCount >= PerSecond) { _limited++; return; }
                _secCount++;
                _last[victim.Index] = now;
            }
            var vis = victim.AgentVisuals;
            Skeleton sk = vis != null ? vis.GetSkeleton() : null;
            if (sk == null || bone >= sk.GetBoneCount()) { lock (RoutHold.Gate) { _badBone++; } return; }
            float scale = 0.5f + 0.5f * Math.Max(0f, Math.Min(1f, (dmg - MinDamage) / 50f));
            if (lethal && scale < 0.8f) scale = 0.8f;
            victim.CreateBloodBurstAtLimb(bone, scale);
            lock (RoutHold.Gate)
            {
                _bursts++;
                if (through) _armour++;
                if (lethal) _lethal++;
            }
        }
    }
}
