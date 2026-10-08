// WYGENEROWANE przez teksty_cs.py z VillageTexts.xml - teksty w grze po angielsku (zasada CLAUDE.md).
// Uzycie: VillageTexts.Make(VillageTexts.VilBurning, "VILLAGE", nazwa) -> TextObject.
using TaleWorlds.Localization;

namespace Armoury.Villages
{
    public static class VillageTexts
    {
        public const string VilBurning = "{=arm_vil_burning}{VILLAGE} is burning";
        public const string VilBurned = "{=arm_vil_burned}{VILLAGE} has been burned";
        public const string VilHalfempty = "{=arm_vil_halfempty}{VILLAGE} stands half empty";
        public const string VilRebuilding = "{=arm_vil_rebuilding}{VILLAGE} is being rebuilt";
        public const string VilRebuilt = "{=arm_vil_rebuilt}{VILLAGE} has been rebuilt";
        public const string VilAbandoned = "{=arm_vil_abandoned}{VILLAGE} lies abandoned";
        public const string VilTipDistrict = "{=arm_vil_tip_district}A village of the {DISTRICT} district";
        public const string VilTipPeople = "{=arm_vil_tip_people}{PEOPLE} souls in {SETTLEMENTS} settlements";
        public const string VilTipBurning = "{=arm_vil_tip_burning}{RAIDER} is putting it to the torch.";
        public const string VilTipBurned = "{=arm_vil_tip_burned}Burned {DAYS} days ago. {RETURNED} of {PEOPLE} souls have come back.";
        public const string VilTipRebuilt = "{=arm_vil_tip_rebuilt}Rebuilt after {YEARS} years.";
        public const string VilLogBurning = "{=arm_vil_log_burning}{RAIDER} is burning {VILLAGE} in the {DISTRICT} district.";
        public const string VilLogBurningOwn = "{=arm_vil_log_burning_own}Smoke over {VILLAGE}: {RAIDER} is burning your village in the {DISTRICT} district.";
        public const string VilLogBurned = "{=arm_vil_log_burned}{VILLAGE} in the {DISTRICT} district has been burned by {RAIDER}.";
        public const string VilLogBurnedPlayer = "{=arm_vil_log_burned_player}Your men have burned {VILLAGE}.";
        public const string VilLogReturning = "{=arm_vil_log_returning}People are returning to {VILLAGE} in the {DISTRICT} district.";
        public const string VilLogRebuilt = "{=arm_vil_log_rebuilt}{VILLAGE} in the {DISTRICT} district has been rebuilt. Its people have come home.";
        public const string VilLogAbandoned = "{=arm_vil_log_abandoned}The people of {VILLAGE} have left their homes.";
        public const string DistOption = "{=arm_dist_option}Look over the district";
        public const string DistTitle = "{=arm_dist_title}Villages of the {DISTRICT} district";
        public const string DistSummary = "{=arm_dist_summary}{STANDING} of {TOTAL} villages still stand: {BURNED} burned, {BURNING} burning. {PEOPLE} souls are at home and {REFUGEES} have fled. The granary holds food for about {DAYS} days.";
        public const string DistLineWhole = "{=arm_dist_line_whole}{VILLAGE}: {PEOPLE} souls, {SETTLEMENTS} settlements";
        public const string DistLineHalf = "{=arm_dist_line_half}{VILLAGE}: half burned, {PEOPLE} souls left";
        public const string DistLineBurning = "{=arm_dist_line_burning}{VILLAGE}: burning ({RAIDER})";
        public const string DistLineBurned = "{=arm_dist_line_burned}{VILLAGE}: burned {DAYS} days ago, {RETURNED} of {PEOPLE} souls back";
        public const string DistLineRebuilding = "{=arm_dist_line_rebuilding}{VILLAGE}: rebuilding, {PEOPLE} souls back";
        public const string DistNone = "{=arm_dist_none}No village of this district still stands.";
        public const string DistClose = "{=arm_dist_close}Leave";
        public const string RaidRide = "{=arm_raid_ride}Your men ride out to {VILLAGE}. Burning its next settlement ({PEOPLE} souls) will take about {DAYS} days.";
        public const string RaidWait = "{=arm_raid_wait}{VILLAGE} - settlement {K}, about {X} days left";
        public const string RaidStepTitle = "{=arm_raid_step_title}{VILLAGE} burns";
        public const string RaidStepBody = "{=arm_raid_step_body}Your men have searched and burned a settlement of {VILLAGE} ({PEOPLE} souls). Killed: {K}. Taken captive: {C}. Fled to the castle and the neighbours: {R}. Gone to the woods: {O}. Loot: {ITEMS}; {GOLD} denars from the villagers' purse. The granary lost {DAYS_FOOD} days of food; your men ate {FOOD} of it. {STANDING} of {N} settlements of {VILLAGE} still stand.";
        public const string RaidVillageDown = "{=arm_raid_village_down}{VILLAGE} has been burned. {LEFT} villages of the {DISTRICT} district still stand; the nearest is {NEXT}, about {KM} km away.";
        public const string RaidNext = "{=arm_raid_next}Raid the next settlement (about {DAYS} days)";
        public const string RaidEnd = "{=arm_raid_end}End the raid";
        public const string RaidSummary = "{=arm_raid_summary}{X} settlements burned in {D} days: {VILLAGES}.";

        /// <summary>TextObject z para (NAZWA, wartosc) - np. Make(VilBurning, "VILLAGE", "Brackenford").</summary>
        public static TextObject Make(string text, params object[] kv)
        {
            var t = new TextObject(text);
            for (int i = 0; i + 1 < kv.Length; i += 2)
            {
                var v = kv[i + 1];
                if (v is TextObject o) t.SetTextVariable((string)kv[i], o);
                else if (v is int n) t.SetTextVariable((string)kv[i], n);
                else t.SetTextVariable((string)kv[i], v == null ? "" : v.ToString());
            }
            return t;
        }
    }
}
