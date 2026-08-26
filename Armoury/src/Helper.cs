using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>Czeladnik przy miechu: najlepszy kowal z twojej druzyny pomaga przy kowadle.</summary>
    internal static class Helper
    {
        internal static Hero Find()
        {
            try
            {
                if (!Settings.Current.CompanionHelperEnabled) return null;
                var party = MobileParty.MainParty;
                if (party == null) return null;
                Hero best = null;
                int bestSkill = 0;
                foreach (var m in party.MemberRoster.GetTroopRoster())
                {
                    var h = m.Character != null ? m.Character.HeroObject : null;
                    if (h == null || h == Hero.MainHero || !h.IsAlive || h.IsWounded) continue;
                    int sk = h.GetSkillValue(DefaultSkills.Crafting);
                    if (sk > bestSkill) { bestSkill = sk; best = h; }
                }
                return bestSkill >= 20 ? best : null;
            }
            catch (Exception e) { Log.Error("Helper.Find", e); return null; }
        }

        /// <summary>0..1 - jak bardzo pomocnik odciaza.</summary>
        internal static float Relief(Hero helper)
        {
            if (helper == null) return 0f;
            var s = Settings.Current;
            int sk = helper.GetSkillValue(DefaultSkills.Crafting);
            return MathF.Min(1f, (float)sk / MathF.Max(1, s.HelperSkillForFullRelief));
        }

        internal static void GiveXp(Hero helper, float xp)
        {
            try { if (helper != null) helper.HeroDeveloper.AddSkillXp(DefaultSkills.Crafting, xp); }
            catch (Exception e) { Log.Error("Helper.GiveXp", e); }
        }
    }
}
