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
        /// <summary>wpis 90 (audyt): zdejmij JEDNA sztuke danego przedmiotu w dowolnym stanie. AddToCounts(ItemObject, -1) celuje
        /// tylko w sztuke bez modyfikatora - przy zuzytej nic nie zdejmowal (gra robi assert i return), a sztuka zostawala.</summary>
        internal static bool RemoveOne(TaleWorlds.CampaignSystem.Roster.ItemRoster r, TaleWorlds.Core.ItemObject it)
        {
            if (r == null || it == null) return false;
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                if (el.EquipmentElement.Item == it && el.Amount > 0) { r.AddToCounts(el.EquipmentElement, -1); return true; }
            }
            return false;
        }
        internal static Hero Find()
        {
            try
            {
                if (!Settings.Current.CompanionHelperEnabled) return null;
                var party = MobileParty.MainParty;
                if (party == null) return null;
                // WYBOR JEFFA (menu kuzni): konkretna postac uczy sie fachu
                // przy miechu, nawet ze skillem 0; "none" = pracuje sam;
                // ""/"auto" = najlepszy kowal z druzyny (min. 20, jak dotad)
                var pick = ArmouryBehavior.SmithHelperId;
                if (pick == "none") return null;
                bool manual = !string.IsNullOrEmpty(pick) && pick != "auto";
                Hero best = null;
                int bestSkill = 0;
                foreach (var m in party.MemberRoster.GetTroopRoster())
                {
                    var h = m.Character != null ? m.Character.HeroObject : null;
                    if (h == null || h == Hero.MainHero || !h.IsAlive || h.IsWounded) continue;
                    if (manual && h.StringId == pick) return h;   // wskazany palcem - bez progu
                    int sk = h.GetSkillValue(DefaultSkills.Crafting);
                    if (sk > bestSkill) { bestSkill = sk; best = h; }
                }
                // wskazanego nie ma w druzynie (ranny/odszedl) - auto robi za zapas
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
