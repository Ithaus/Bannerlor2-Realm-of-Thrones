using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// PAROWANIE MISTRZA, wedle slow Jeffa: kto trzyma blok bronia dwureczna
    /// i przewyzsza napastnika wprawa, temu klinga sama schodzi na wlasciwa
    /// strone. Przewaga >= AutoParryFullDiff punktow umiejetnosci = kazdy
    /// trzymany blok trafia sam. Przewaga mniejsza (np. 125 vs 112) = tylko
    /// AutoParryPartialChance % szansy NA KAZDY CIOS - reszte celujesz sam.
    /// Rowny lub lepszy przeciwnik = zadnej pomocy, czysta reka.
    /// Dziala TYLKO, gdy blok jest trzymany - nic nie blokuje za darmo.
    /// </summary>
    internal sealed class GuardMaster : MissionBehavior
    {
        public override MissionBehaviorType BehaviorType { get { return MissionBehaviorType.Other; } }

        private sealed class Mark
        {
            public bool InSwing;    // napastnik jest w trakcie tego zamachu
            public bool Auto;       // rzut kostka dla TEGO zamachu (przy malej przewadze)
        }

        private readonly Dictionary<Agent, Mark> _marks = new Dictionary<Agent, Mark>();
        private float _accum;   // przeglad pola co 0.05 s wystarczy - zamach trwa dziesiec razy dluzej
        private static int _guards;   // ile ciosow mistrz przejal w tej sesji (do logu)

        public override void OnAgentDeleted(Agent affectedAgent)
        {
            try { _marks.Remove(affectedAgent); } catch { }
        }

        public override void OnMissionTick(float dt)
        {
            try
            {
                var c = Settings.Current;
                if (c == null || !c.AutoParryEnabled) return;
                _accum += dt;
                if (_accum < 0.05f) return;
                _accum = 0f;
                var me = Mission.MainAgent;
                if (me == null || !me.IsActive() || !me.IsHuman) return;

                var flags = me.MovementFlags;
                if ((flags & Agent.MovementControlFlag.DefendMask) == 0) return;   // nie trzymasz bloku - nie pomagamy
                if ((flags & Agent.MovementControlFlag.AttackMask) != 0) return;   // wlasny zamach - nie mieszamy

                // czym parujesz: dwureczna (albo dowolna biala, gdy tak ustawiono)
                var myWeap = me.WieldedWeapon;
                if (myWeap.IsEmpty || myWeap.Item == null) return;
                var myType = myWeap.Item.ItemType;
                if (myType != ItemObject.ItemTypeEnum.TwoHandedWeapon)
                {
                    if (c.AutoParryTwoHandedOnly) return;
                    if (myType != ItemObject.ItemTypeEnum.OneHandedWeapon
                        && myType != ItemObject.ItemTypeEnum.Polearm) return;
                }

                int mySkill = SkillOf(me, myWeap.Item);

                // najblizszy wrog, ktory wlasnie sklada sie do ciosu na wyciagniecie broni
                Agent foe = null; float best = 30f;   // ~5.5 m - dalej i tak nie siegnie
                Vec2 my2 = me.Position.AsVec2;
                foreach (var a in Mission.Agents)
                {
                    if (a == null || a == me || !a.IsActive() || !a.IsHuman) continue;
                    if (a.Team == null || me.Team == null || !a.Team.IsEnemyOf(me.Team)) continue;
                    var act = a.GetCurrentActionType(1);
                    if (act != Agent.ActionCodeType.ReadyMelee && act != Agent.ActionCodeType.ReleaseMelee)
                    {
                        Mark m0;
                        if (_marks.TryGetValue(a, out m0)) m0.InSwing = false;   // zamach skonczony
                        continue;
                    }
                    float d2 = a.Position.AsVec2.DistanceSquared(my2);
                    if (d2 >= best) continue;
                    // musi bic W TWOJA strone, nie w sasiada
                    Vec2 look = a.LookDirection.AsVec2;
                    Vec2 toMe = my2 - a.Position.AsVec2;
                    if (toMe.LengthSquared > 0.01f)
                    {
                        look.Normalize(); toMe.Normalize();
                        if (Vec2.DotProduct(look, toMe) < 0.5f) continue;
                    }
                    best = d2; foe = a;
                }
                if (foe == null) return;

                var dir = foe.AttackDirection;
                if (dir != Agent.UsageDirection.AttackUp && dir != Agent.UsageDirection.AttackDown
                    && dir != Agent.UsageDirection.AttackLeft && dir != Agent.UsageDirection.AttackRight) return;

                // pojedynek wprawy: twoja bron kontra jego bron
                int hisSkill = 0;
                try
                {
                    var hw = foe.WieldedWeapon;
                    if (!hw.IsEmpty && hw.Item != null) hisSkill = SkillOf(foe, hw.Item);
                }
                catch { }
                int lead = mySkill - hisSkill;
                if (lead <= 0) return;                                    // rowny lub lepszy - zero pomocy

                Mark mark;
                if (!_marks.TryGetValue(foe, out mark)) { mark = new Mark(); _marks[foe] = mark; }
                if (!mark.InSwing)
                {
                    // NOWY zamach - dopiero teraz rzucamy kostka (raz na cios).
                    // Przewaga to szansa, wprost po slowach Jeffa: kazdy punkt
                    // przewagi = 100/AutoParryFullDiff procent (przy 25 -> 4%/pkt).
                    // 125 vs 112 = 13 pkt = 52%; 120 vs 119 = 1 pkt = 4%;
                    // 25+ pkt = 100%, pewny blok.
                    mark.InSwing = true;
                    float need = Math.Max(1f, c.AutoParryFullDiff);
                    float chance = MBMath.ClampFloat(lead * (100f / need), 0f, 100f);
                    mark.Auto = MBRandom.RandomFloat * 100f < chance;
                    // slad zycia w logu (raz na zamach, nie na tick) - "sprawdz,
                    // czy dziala" bez zgadywania: pierwszy i co 25. przejety cios
                    if (mark.Auto)
                    {
                        _guards++;
                        if (_guards == 1 || _guards % 25 == 0)
                            Log.Info("GuardMaster: mistrz przejal blok (" + _guards + ". raz; przewaga "
                                     + lead + " pkt, szansa " + (int)chance + "%).");
                    }
                }
                if (!mark.Auto) return;                                   // kostka przeciw - celuj sam

                Agent.MovementControlFlag block;
                switch (dir)
                {
                    case Agent.UsageDirection.AttackUp: block = Agent.MovementControlFlag.DefendUp; break;
                    case Agent.UsageDirection.AttackDown: block = Agent.MovementControlFlag.DefendDown; break;
                    case Agent.UsageDirection.AttackLeft:
                        block = c.AutoParryMirrorSides ? Agent.MovementControlFlag.DefendRight : Agent.MovementControlFlag.DefendLeft; break;
                    default:
                        block = c.AutoParryMirrorSides ? Agent.MovementControlFlag.DefendLeft : Agent.MovementControlFlag.DefendRight; break;
                }
                me.MovementFlags = (flags & ~Agent.MovementControlFlag.DefendMask) | block;
            }
            catch (Exception e) { Log.Error("GuardMaster.Tick", e); }
        }

        private static int SkillOf(Agent a, ItemObject weapon)
        {
            try
            {
                var co = a.Character as CharacterObject;
                if (co == null || weapon == null) return 0;
                var sk = weapon.RelevantSkill;
                if (sk == null) return 0;
                return co.GetSkillValue(sk);
            }
            catch { return 0; }
        }
    }
}
