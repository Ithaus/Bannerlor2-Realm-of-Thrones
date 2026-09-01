using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// DZIENNIK TRAFIEN POCISKOW (Jeff 01.09: "musze wywalic kilka strzal
    /// w glowe zeby zabic bandyte - sprawdz logi bitwy"). Gra nie loguje
    /// obrazen, wiec spor "strzaly za slabe czy hełm za mocny" nie mial
    /// danych. Kazde trafienie pociskiem zapisuje: czym, kogo, w co,
    /// ile pancerza mial cel i ile HP realnie zeszlo. Limit wpisow na
    /// misje, zeby nie zalac logu.
    /// </summary>
    internal sealed class HitScribe : MissionBehavior
    {
        public override MissionBehaviorType BehaviorType { get { return MissionBehaviorType.Other; } }

        private int _written;
        private const int MaxLines = 150;

        public override void OnScoreHit(Agent affectedAgent, Agent affectorAgent,
            WeaponComponentData attackerWeapon, bool isBlocked, bool isSiegeEngineHit,
            in Blow blow, in AttackCollisionData collisionData, float damagedHp,
            float hitDistance, float shotDifficulty)
        {
            try
            {
                if (_written >= MaxLines) return;
                if (attackerWeapon == null || !attackerWeapon.IsRangedWeapon && !attackerWeapon.IsAmmo) return;
                if (affectedAgent == null || !affectedAgent.IsHuman) return;

                string arrow = attackerWeapon.WeaponClass.ToString();
                string victim = "?";
                try
                {
                    var ch = affectedAgent.Character;
                    if (ch != null) victim = ch.StringId ?? ch.Name.ToString();
                }
                catch { }
                float armor = 0f;
                try { armor = collisionData.AbsorbedByArmor; } catch { }
                string part = "?";
                try { part = collisionData.VictimHitBodyPart.ToString(); } catch { }

                _written++;
                Log.Info("HIT " + arrow + " -> " + victim + " [" + part + "]"
                         + " dmg=" + ((int)damagedHp) + " wchloniete=" + ((int)armor)
                         + " HPpo=" + ((int)affectedAgent.Health) + "/" + ((int)affectedAgent.HealthLimit)
                         + (isBlocked ? " BLOK" : "") + " dyst=" + ((int)hitDistance));
                if (_written == MaxLines)
                    Log.Info("HitScribe: limit " + MaxLines + " wpisow tej misji osiagniety - reszta pominieta.");
            }
            catch { }
        }
    }
}
