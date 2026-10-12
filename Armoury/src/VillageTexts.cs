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
        // T7 (noc 08/09.10, raport 08 W-2): linia pana na zywo z gry - dopisane recznie (VillageTexts.xml / teksty_cs.py nie ma w repo)
        public const string VilTipHeld = "{=arm_vil_tip_held}A village of {LANDS}, held by {HOUSE} of {SEAT}";
        public const string VilTipHeldOwn = "{=arm_vil_tip_held_own}A village of {LANDS}, your fief";
        public const string VilLands = "{=arm_vil_lands}the {NAME} lands";
        public const string VilLandsOf = "{=arm_vil_lands_of}the lands of {NAME}";
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
        // E4/11 (193, plan rozdz. 6): sekcja okregu w "People of this land" i stan wioski w dymku z ksiegi wiosek - dopisane recznie (VillageTexts.xml / teksty_cs.py nie ma w repo)
        public const string DistHead = "{=arm_dist_head}The {DISTRICT} district: {SETTLEMENTS} settlements in {VILLAGES} villages";
        public const string DistSummaryNoGranary = "{=arm_dist_summary_nogranary}{STANDING} of {TOTAL} villages still stand: {BURNED} burned, {BURNING} burning. {PEOPLE} souls are at home and {REFUGEES} have fled.";
        public const string DistAbandonedNote = "{=arm_dist_abandoned_note}{ABANDONED} lie abandoned after the famine.";
        public const string DistHarvest = "{=arm_dist_harvest}The district's harvest stands at {PCT}% of a full one.";
        public const string DistLinePart = "{=arm_dist_line_part}{VILLAGE} {PLACE}: {BURNED} of {SETTLEMENTS} settlements are ash, {PEOPLE} souls at home, {FLED} fled";
        public const string DistLineEmptied = "{=arm_dist_line_emptied}{VILLAGE} {PLACE}: {BURNED} of {SETTLEMENTS} settlements stand empty, {PEOPLE} souls at home, {FLED} fled";
        public const string DistLineAbandoned = "{=arm_dist_line_abandoned}{VILLAGE} {PLACE}: abandoned in the famine, {PEOPLE} souls left";
        public const string DistLineMain = "{=arm_dist_line_main}{VILLAGE}, the district village: {PEOPLE} souls at home in {SETTLEMENTS} settlements";
        public const string DistShare = "{=arm_dist_share}({SHARE}% of the harvest)";
        public const string DistMore = "{=arm_dist_more}...and {N} more villages, all standing.";
        public const string DistNotYet = "{=arm_dist_not_yet}The villages of this district have not been reckoned yet.";
        public const string DistRegion = "{=arm_dist_region}Villages of the {REGION} lands";
        public const string DistRegionLine = "{=arm_dist_region_line}{DISTRICT}: {STANDING} of {TOTAL} villages stand, {PEOPLE} souls at home, {REFUGEES} fled";
        public const string VilTipRebuiltDays = "{=arm_vil_tip_rebuilt_days}Rebuilt {DAYS} days after the burning.";
        public const string VilTipEmptied = "{=arm_vil_tip_emptied}{EMPTY} of its {SETTLEMENTS} settlements stand empty; {FLED} souls have fled.";
        public const string PlaceBridge = "{=arm_place_bridge}by the bridge";
        public const string PlaceFord = "{=arm_place_ford}at the ford";
        public const string PlaceCrossroad = "{=arm_place_crossroad}at the crossroads";
        public const string PlaceRoad = "{=arm_place_road}on the road";
        public const string PlaceRiver = "{=arm_place_river}by the river";
        public const string PlaceLake = "{=arm_place_lake}by the lake";
        public const string PlaceCoast = "{=arm_place_coast}on the coast";
        public const string PlaceField = "{=arm_place_field}among the fields";
        public const string RaidRide = "{=arm_raid_ride}Your men ride out to {VILLAGE}. Burning its next settlement ({PEOPLE} souls) will take about {DAYS} days.";
        public const string RaidWait = "{=arm_raid_wait}{VILLAGE} - settlement {K}, about {X} days left";
        public const string RaidStepTitle = "{=arm_raid_step_title}{VILLAGE} burns";
        public const string RaidStepBody = "{=arm_raid_step_body}Your men have searched and burned a settlement of {VILLAGE} ({PEOPLE} souls). Killed: {K}. Taken captive: {C}. Fled to the castle and the neighbours: {R}. Gone to the woods: {O}. Loot: {ITEMS}; {GOLD} denars from the villagers' purse. The granary lost {DAYS_FOOD} days of food; your men ate {FOOD} of it. {STANDING} of {N} settlements of {VILLAGE} still stand.";
        public const string RaidVillageDown = "{=arm_raid_village_down}{VILLAGE} has been burned. {LEFT} villages of the {DISTRICT} district still stand; the nearest is {NEXT}, about {KM} km away.";
        public const string RaidNext = "{=arm_raid_next}Raid the next settlement (about {DAYS} days)";
        public const string RaidEnd = "{=arm_raid_end}End the raid";
        public const string RaidSummary = "{=arm_raid_summary}{X} settlements burned in {D} days: {VILLAGES}.";
        // E7/W1 (232, R10/R14): splondrowana bez spalenia w ksiedze i pamiec wojny (dymek W2) - dopisane recznie (VillageTexts.xml / teksty_cs.py nie ma w repo)
        public const string VilLogPlundered = "{=arm_vil_log_plundered}{RAIDER} has plundered {VILLAGE} in the {DISTRICT} district: {BURNED} of {N} settlements burned.";
        public const string VilLogPlunderedPlayer = "{=arm_vil_log_plundered_player}Your men have plundered {VILLAGE}: {BURNED} of {N} settlements burned.";
        public const string VilMemBurned = "{=arm_vil_mem_burned}Burned by {RAIDER} in the {SEASON} of {YEAR} AC; about {FLED} souls fled and {DEAD} were killed.";
        public const string VilMemPlundered = "{=arm_vil_mem_plundered}Plundered by {RAIDER} in the {SEASON} of {YEAR} AC; about {FLED} souls fled and {DEAD} were killed.";
        public const string VilMemGone = "{=arm_vil_mem_gone}a raider long gone";
        // E7/W2 (233, R11/R12): rycerz wioski (tytul wsi z BK albo dane lore, knight_form) i wiara z BK w dymku - dopisane recznie (VillageTexts.xml / teksty_cs.py nie ma w repo)
        public const string VilTipKept = "{=arm_vil_tip_kept}Kept for them by {KNIGHT}";
        public const string VilTipKeptOwn = "{=arm_vil_tip_kept_own}Kept for you by {KNIGHT}";
        public const string VilTipTribute = "{=arm_vil_tip_tribute}{KNIGHT} takes its tribute";
        public const string VilTipTributeOwn = "{=arm_vil_tip_tribute_own}{KNIGHT} takes its tribute for you";
        public const string VilTipHolderOf = "{=arm_vil_tip_holder_of}{HERO} of {HOUSE}";
        public const string VilTipFaith = "{=arm_vil_tip_faith}Faith: {FAITH} ({SHARE}%)";
        // E7/W3 (234, R13): okno okregu "Look over the district" - dopisane recznie (VillageTexts.xml / teksty_cs.py nie ma w repo)
        public const string DistHearMore = "{=arm_dist_hear_more}Hear more";
        public const string DistBack = "{=arm_dist_back}Back to the villages";
        public const string DistPick = "{=arm_dist_pick}Pick a village to hear more about it.";
        public const string DistRowDamaged = "{=arm_dist_row_damaged}{VILLAGE}, some homes empty";
        public const string VilArms = "{=arm_vil_arms}Arms of {HOUSE}: {ARMS}.";

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
