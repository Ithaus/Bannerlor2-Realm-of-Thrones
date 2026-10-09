using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;

namespace Armoury
{
    /// <summary>Ustawienia w MCM. Plik XML dziala dalej jako wartosci startowe.</summary>
    public class McmSettings : AttributeGlobalSettings<McmSettings>
    {
        public override string Id => "Armoury";
        public override string DisplayName => "The Armoury";
        public override string FolderName => "Armoury";
        public override string FormatType => "json2";

        [SettingPropertyBool("Tidy Banner Kings Armour List", HintText = "real categories and tier labels in the Banner Kings armour tab")]
        [SettingPropertyGroup("Forging armour")]
        public bool TidyBannerKingsArmourList { get; set; } = true;

        [SettingPropertyBool("Forge Armour Enabled", HintText = "off when Banner Kings' own armour tab in the smithy screen handles forging")]
        [SettingPropertyGroup("Forging armour")]
        public bool ForgeArmourEnabled { get; set; } = false;

        [SettingPropertyBool("Crafting Enabled", HintText = "armour forging on or off")]
        [SettingPropertyGroup("Forging armour")]
        public bool CraftingEnabled { get; set; } = true;

        [SettingPropertyInteger("Smithing Skill Per Tier", 0, 140, "0", HintText = "Smithing you need before you may attempt a piece, per tier above the first - 35 means tier 5 plate opens at 140 and tier 6 at 175 (cloth 20 and leather, bows and crossbows 10 lower); also the gate for mending your own gear by hand. It only opens the door - the dice are set by Smithing Difficulty Per Tier")]
        [SettingPropertyGroup("Forging armour")]
        public int SmithingSkillPerTier { get; set; } = 35;

        [SettingPropertyInteger("Smithing Difficulty Per Tier", 0, 180, "0", HintText = "how hard a piece really is for the dice, per tier above the first, whatever the door says: the quality it comes out with (rusty to legendary - armour, bows and the reforged weapons alike), the risk of cracking it, the XP it teaches and the odds of copying a pattern. 45 keeps the odds as they were, so a lower Smithing Skill Per Tier lets you start finer work sooner - at first with more spoiled pieces - and never hands out more legends")]
        [SettingPropertyGroup("Forging armour")]
        public int SmithingDifficultyPerTier { get; set; } = 45;

        [SettingPropertyBool("Armour Craft Like Weapons", HintText = "armour forged in the Banner Kings CRAFT tab follows the rule of bows and reforged weapons: you may start a piece once your smith reaches its threshold (Smithing Skill Per Tier - the tab shows it as Difficulty), and his Smithing against the piece's difficulty (Smithing Difficulty Per Tier) sets the risk of cracking it (the tab shows the real Botching Chance) and the quality it comes out with from its own armour group - rusty or ripped, dented or worn, plain, fine, lordly, legendary, with their protection bonus or loss (tier 1-3 at best fine, tier 4 at best lordly). Your own armour keeps its make under wear: wear never turns a rusty piece into a better one, and the smith's repair restores only the piece that was actually worn. Off: armour from the tab comes out plain as before, with the Banner Kings crack chance")]
        [SettingPropertyGroup("Forging armour")]
        public bool ArmourCraftLikeWeapons { get; set; } = true;

        [SettingPropertyFloatingInteger("Iron Per Weight Unit", 0.00f, 5.60f, "0.00", HintText = "refined iron per pound of the finished piece")]
        [SettingPropertyGroup("Forging armour")]
        public float IronPerWeightUnit { get; set; } = 1.4f;

        [SettingPropertyFloatingInteger("Class Cost Body", 0.00f, 4.60f, "0.00", HintText = "cuirass - the most waste when the plates are cut")]
        [SettingPropertyGroup("Cost by piece")]
        public float ClassCostBody { get; set; } = 1.15f;

        [SettingPropertyFloatingInteger("Class Cost Leg", 0.00f, 3.40f, "0.00", HintText = "greaves and tassets - broad plates, much alloy")]
        [SettingPropertyGroup("Cost by piece")]
        public float ClassCostLeg { get; set; } = 0.85f;

        [SettingPropertyFloatingInteger("Class Cost Head", 0.00f, 2.40f, "0.00", HintText = "helmet - little steel for the protection it gives")]
        [SettingPropertyGroup("Cost by piece")]
        public float ClassCostHead { get; set; } = 0.60f;

        [SettingPropertyFloatingInteger("Class Cost Hand", 0.00f, 1.60f, "0.00", HintText = "gauntlets - little steel, a great deal of fiddling")]
        [SettingPropertyGroup("Cost by piece")]
        public float ClassCostHand { get; set; } = 0.40f;

        [SettingPropertyFloatingInteger("Class Cost Cape", 0.00f, 2.60f, "0.00", HintText = "cloaks and shoulders - mostly cloth and leather")]
        [SettingPropertyGroup("Cost by piece")]
        public float ClassCostCape { get; set; } = 0.65f;

        [SettingPropertyFloatingInteger("Class Cost Horse", 0.00f, 5.00f, "0.00", HintText = "barding - a great deal of everything")]
        [SettingPropertyGroup("Cost by piece")]
        public float ClassCostHorse { get; set; } = 1.25f;

        [SettingPropertyFloatingInteger("Class Cost Shield", 0.00f, 2.20f, "0.00", HintText = "shield - timber, hide and a rim of iron")]
        [SettingPropertyGroup("Cost by piece")]
        public float ClassCostShield { get; set; } = 0.55f;

        [SettingPropertyFloatingInteger("Class Cost Ranged", 0.00f, 1.80f, "0.00", HintText = "bows and bolts - wood, horn and sinew")]
        [SettingPropertyGroup("Cost by piece")]
        public float ClassCostRanged { get; set; } = 0.45f;

        [SettingPropertyFloatingInteger("Fiddly Stamina Bonus", 0.00f, 2.00f, "0.00", HintText = "how much more the fiddly pieces take out of you")]
        [SettingPropertyGroup("Cost by piece")]
        public float FiddlyStaminaBonus { get; set; } = 0.5f;

        [SettingPropertyFloatingInteger("Charcoal Per Iron", 0.00f, 2.40f, "0.00", HintText = "charcoal burned per unit of iron")]
        [SettingPropertyGroup("Cost by piece")]
        public float CharcoalPerIron { get; set; } = 0.6f;

        [SettingPropertyInteger("Stamina Per Tier", 0, 100, "0", HintText = "crafting stamina burned per tier")]
        [SettingPropertyGroup("Cost by piece")]
        public int StaminaPerTier { get; set; } = 25;

        [SettingPropertyBool("Forge Works Without You", HintText = "the smith and his lads keep at your project while you ride - the finished piece waits at that forge for collection; off = the clock only runs while you stay in that settlement")]
        [SettingPropertyGroup("Time at the anvil")]
        public bool ForgeWorksWithoutYou { get; set; } = true;

        [SettingPropertyBool("Forge One Clock", HintText = "one forge clock: Craft puts the piece on the smith's bench for the hours the Banner Kings screen shows (stamina spent / 6); pieces come off one by one when done - no lump wait afterwards")]
        [SettingPropertyGroup("Time at the anvil")]
        public bool ForgeOneClock { get; set; } = true;

        [SettingPropertyBool("Forge Only While There", HintText = "your own work at the anvil goes on only while you stay in that settlement - ride away and the bench waits for you")]
        [SettingPropertyGroup("Time at the anvil")]
        public bool ForgeOnlyWhileThere { get; set; } = true;

        [SettingPropertyBool("Forge Takes Time", HintText = "armour is not finished the moment you order it")]
        [SettingPropertyGroup("Time at the anvil")]
        public bool ForgeTakesTime { get; set; } = true;

        [SettingPropertyFloatingInteger("Days Per Tier", 0.00f, 4.00f, "0.00", HintText = "days per tier - 1 means a tier 5 plate takes five days (Jeff 14.09: halved)")]
        [SettingPropertyGroup("Time at the anvil")]
        public float DaysPerTier { get; set; } = 1f;

        [SettingPropertyFloatingInteger("Tempo Hasty Time", 0.00f, 2.00f, "0.00", HintText = "in haste: this share of the time")]
        [SettingPropertyGroup("Time at the anvil")]
        public float TempoHastyTime { get; set; } = 0.5f;

        [SettingPropertyFloatingInteger("Tempo Hasty Risk", 0.00f, 8.00f, "0.00", HintText = "in haste: this many times the risk")]
        [SettingPropertyGroup("Time at the anvil")]
        public float TempoHastyRisk { get; set; } = 2f;

        [SettingPropertyFloatingInteger("Tempo Careful Time", 0.00f, 6.00f, "0.00", HintText = "with care: this many times longer")]
        [SettingPropertyGroup("Time at the anvil")]
        public float TempoCarefulTime { get; set; } = 1.5f;

        [SettingPropertyFloatingInteger("Tempo Careful Risk", 0.00f, 2.00f, "0.00", HintText = "with care: this share of the risk")]
        [SettingPropertyGroup("Time at the anvil")]
        public float TempoCarefulRisk { get; set; } = 0.5f;

        [SettingPropertyFloatingInteger("Tempo Careful Quality", 0.00f, 8.00f, "0.00", HintText = "with care: this many times the chance of quality")]
        [SettingPropertyGroup("Time at the anvil")]
        public float TempoCarefulQuality { get; set; } = 2f;

        [SettingPropertyInteger("Xp Per Day Per Tier", 0, 400, "0", HintText = "XP for one day of work, per tier of the piece")]
        [SettingPropertyGroup("Experience")]
        public int XpPerDayPerTier { get; set; } = 100;

        [SettingPropertyInteger("Xp Full Credit Margin", 0, 400, "0", HintText = "you still learn at full pace this far above the recipe")]
        [SettingPropertyGroup("Experience")]
        public int XpFullCreditMargin { get; set; } = 100;

        [SettingPropertyFloatingInteger("Xp Diminishing Range", 0.00f, 1000.00f, "0.00", HintText = "and only past that margin does the learning taper off")]
        [SettingPropertyGroup("Experience")]
        public float XpDiminishingRange { get; set; } = 250f;

        [SettingPropertyFloatingInteger("Xp Floor Factor", 0.00f, 1.00f, "0.00", HintText = "the floor, when the work is far beneath your hand")]
        [SettingPropertyGroup("Experience")]
        public float XpFloorFactor { get; set; } = 0.25f;

        [SettingPropertyInteger("Xp Cap Per Tier", 0, 5200, "0", HintText = "ceiling per project = this times the tier")]
        [SettingPropertyGroup("Experience")]
        public int XpCapPerTier { get; set; } = 1300;

        [SettingPropertyFloatingInteger("Xp Share While Working", 0.00f, 1.60f, "0.00", HintText = "share paid out as you work, the rest on completion")]
        [SettingPropertyGroup("Experience")]
        public float XpShareWhileWorking { get; set; } = 0.4f;

        [SettingPropertyBool("Weapon Crafting Takes Time", HintText = "native weapon smithing is not instant either")]
        [SettingPropertyGroup("Experience")]
        public bool WeaponCraftingTakesTime { get; set; } = true;

        [SettingPropertyFloatingInteger("Weapon Days Per Tier", 0.00f, 1.00f, "0.00", HintText = "days per tier of a weapon - 0.1 means a tier 4 sword takes under half a day at the anvil (Jeff 14.09: halved; 16.09: four swords took 169 hours, cut by 80 percent)")]
        [SettingPropertyGroup("Experience")]
        public float WeaponDaysPerTier { get; set; } = 0.1f;

        [SettingPropertyBool("Weapon Xp From Value Capped", HintText = "native weapon XP follows the sale price - cap it")]
        [SettingPropertyGroup("Experience")]
        public bool WeaponXpFromValueCapped { get; set; } = true;

        [SettingPropertyInteger("Weapon Xp Cap Per Tier", 0, 2000, "0", HintText = "ceiling per weapon = this times the tier")]
        [SettingPropertyGroup("Experience")]
        public int WeaponXpCapPerTier { get; set; } = 500;

        [SettingPropertyBool("Armour Orders Enabled", HintText = "lords leave armour commissions with town smiths")]
        [SettingPropertyGroup("Orders from the lords")]
        public bool ArmourOrdersEnabled { get; set; } = true;

        [SettingPropertyFloatingInteger("Order Offer Chance", 0.00f, 2.00f, "0.00", HintText = "chance a fresh offer waits when you ride in")]
        [SettingPropertyGroup("Orders from the lords")]
        public float OrderOfferChance { get; set; } = 0.5f;

        [SettingPropertyFloatingInteger("Order Town Cooldown Days", 0.00f, 12.00f, "0.00", HintText = "one town does not post offers more often than this")]
        [SettingPropertyGroup("Orders from the lords")]
        public float OrderTownCooldownDays { get; set; } = 3f;

        [SettingPropertyFloatingInteger("Order Offer Life Days", 0.00f, 20.00f, "0.00", HintText = "an untaken offer is withdrawn after this long")]
        [SettingPropertyGroup("Orders from the lords")]
        public float OrderOfferLifeDays { get; set; } = 5f;

        [SettingPropertyFloatingInteger("Order Deadline Days", 0.00f, 48.00f, "0.00", HintText = "days to deliver once you take the order")]
        [SettingPropertyGroup("Orders from the lords")]
        public float OrderDeadlineDays { get; set; } = 12f;

        [SettingPropertyFloatingInteger("Order Pay Multiplier", 0.00f, 5.40f, "0.00", HintText = "pay above market - the lord skips the middleman")]
        [SettingPropertyGroup("Orders from the lords")]
        public float OrderPayMultiplier { get; set; } = 1.35f;

        [SettingPropertyInteger("Order Relation Reward", 0, 10, "0", HintText = "relation gained with the lord on delivery")]
        [SettingPropertyGroup("Orders from the lords")]
        public int OrderRelationReward { get; set; } = 2;

        [SettingPropertyInteger("Order Miss Relation Penalty", 0, 10, "0", HintText = "relation lost when the deadline passes")]
        [SettingPropertyGroup("Orders from the lords")]
        public int OrderMissRelationPenalty { get; set; } = 2;

        [SettingPropertyInteger("Max Accepted Orders", 0, 12, "0", HintText = "how many orders your book holds at once")]
        [SettingPropertyGroup("Orders from the lords")]
        public int MaxAcceptedOrders { get; set; } = 3;

        [SettingPropertyInteger("Order Min Tier", 0, 10, "0", HintText = "lords do not commission rags")]
        [SettingPropertyGroup("Orders from the lords")]
        public int OrderMinTier { get; set; } = 2;

        [SettingPropertyInteger("Order Max Tier", 0, 20, "0", HintText = "nor ask a town smith for the impossible")]
        [SettingPropertyGroup("Orders from the lords")]
        public int OrderMaxTier { get; set; } = 5;

        [SettingPropertyInteger("Order Max Item Value", 0, 48000, "0", HintText = "cap on the piece's worth")]
        [SettingPropertyGroup("Orders from the lords")]
        public int OrderMaxItemValue { get; set; } = 12000;

        [SettingPropertyInteger("Forge Fee Base", 0, 12, "0", HintText = "what the smith charges you to use his forge (historical prices: about a day of a craftsman, 3 d)")]
        [SettingPropertyGroup("Forge fee")]
        public int ForgeFeeBase { get; set; } = 3;

        [SettingPropertyInteger("Forge Fee Per Tier", 0, 10, "0", HintText = "and this much more for every tier of the work (not used while Forge Hire Historical is on - then a forge day costs the same whatever you make)")]
        [SettingPropertyGroup("Forge fee")]
        public int ForgeFeePerTier { get; set; } = 2;

        [SettingPropertyFloatingInteger("Bk Forge Hourly Multiplier", 0.00f, 2.00f, "0.00", HintText = "Banner Kings charges by the hour at the anvil - this scales that hourly rate (not used while Forge Hire Historical is on - then the hour is the forge day split by Forge Day Hours)")]
        [SettingPropertyGroup("Forge fee")]
        public float BkForgeHourlyMultiplier { get; set; } = 0.5f;

        [SettingPropertyBool("Forge Day Pass Enabled", HintText = "the smith hires his forge BY THE DAY, paid up front - one hour or twenty-three, same coin")]
        [SettingPropertyGroup("Forge fee")]
        public bool ForgeDayPassEnabled { get; set; } = true;

        [SettingPropertyFloatingInteger("Forge Day Hours", 0.00f, 32.00f, "0.00", HintText = "with Forge Hire Historical on: the forge day (Forge Fee Base x the town's wage level) is split into this many hours for the Banner Kings hourly rate; off: the day's hire costs this many hours at the Banner Kings rate")]
        [SettingPropertyGroup("Forge fee")]
        public float ForgeDayHours { get; set; } = 8f;

        [SettingPropertyBool("Forge Hire Historical", HintText = "the smith's forge is hired by the day at historical prices, as dear as the town is prosperous - one price for a day of the forge whatever you forge on it: a craftsman's day (the forge fee base, 3 d in a middling town); a project pays it for every day of work (the first day when you set to work, the rest as the work goes on - with an empty purse the work waits), the Banner Kings day the same, its hourly rate that day split by the hours (off = the old fee by tier and the day at the Banner Kings hourly rate)")]
        [SettingPropertyGroup("Forge fee")]
        public bool ForgeHireHistorical { get; set; } = true;

        [SettingPropertyBool("Forge Work No Rest", HintText = "hammering is not napping - smithing stamina does NOT recover during hours spent working the forge")]
        [SettingPropertyGroup("Forge fee")]
        public bool ForgeWorkNoRest { get; set; } = true;

        [SettingPropertyBool("Enforce Stamina Costs", HintText = "every smelt, refine and forging pays its stamina price - if some mod loses the bill, we collect it ourselves")]
        [SettingPropertyGroup("Forge fee")]
        public bool EnforceStaminaCosts { get; set; } = true;

        [SettingPropertyBool("Stamina Cost Messages", HintText = "a quiet grey line after each action at the furnace: what it cost and what wind is left")]
        [SettingPropertyGroup("Forge fee")]
        public bool StaminaCostMessages { get; set; } = true;

        [SettingPropertyFloatingInteger("Forge Stamina Camp Rate", 0.00f, 32.00f, "0.00", HintText = "crafting stamina regained per hour asleep or in camp")]
        [SettingPropertyGroup("Forge fee")]
        public float ForgeStaminaCampRate { get; set; } = 8f;

        [SettingPropertyFloatingInteger("Forge Stamina March Rate", 0.00f, 12.00f, "0.00", HintText = "...and per hour on the march - a rider is not swinging a hammer")]
        [SettingPropertyGroup("Forge fee")]
        public float ForgeStaminaMarchRate { get; set; } = 3f;

        [SettingPropertyInteger("Refine Xp Cap", 0, 240, "0", HintText = "refining one batch teaches at most this much Smithing")]
        [SettingPropertyGroup("Forge fee")]
        public int RefineXpCap { get; set; } = 60;

        [SettingPropertyBool("Armoury Protect Used", HintText = "the quartermaster hands out only SURPLUS - gear your soldiers still use cannot leave the troop armoury")]
        [SettingPropertyGroup("Forge fee")]
        public bool ArmouryProtectUsed { get; set; } = true;

        [SettingPropertyBool("Quartermaster Shouts", HintText = "the quartermaster reports missing kit OUT LOUD after every battle and each morning - not only in the armoury screen")]
        [SettingPropertyGroup("Forge fee")]
        public bool QuartermasterShouts { get; set; } = true;

        [SettingPropertyBool("Quartermaster Purge Unusable", HintText = "on every armoury visit the war-chest keeps only what the men actually wear (skill law); every other piece - beyond their skill or beyond their need - moves to YOUR list in the stash so you can see and sell it")]
        [SettingPropertyGroup("Forge fee")]
        public bool QuartermasterPurgeUnusable { get; set; } = true;

        [SettingPropertyFloatingInteger("Charcoal Weight", 0.00f, 2.00f, "0.00", HintText = "a lump of charcoal weighs this much (vanilla hauls 5 kg bricks; 0 = leave alone)                   // the day's hire costs this many hours at the smith's rate (~200 gold in an average town)")]
        [SettingPropertyGroup("Forge fee")]
        public float CharcoalWeight { get; set; } = 0.5f;

        [SettingPropertyBool("Bk True Materials", HintText = "Banner Kings armour crafting uses the honest material rule below instead of its own token amounts")]
        [SettingPropertyGroup("Forge fee")]
        public bool BkTrueMaterials { get; set; } = true;

        [SettingPropertyFloatingInteger("Armor Points Per Material", 0.00f, 40.00f, "0.00", HintText = "one unit of material per this many points of total protection on the piece")]
        [SettingPropertyGroup("Forge fee")]
        public float ArmorPointsPerMaterial { get; set; } = 10f;

        [SettingPropertyFloatingInteger("Armor Material Scale", 0.00f, 2.00f, "0.00", HintText = "the whole material bill times this - the old bills asked for more leather than markets ever stock")]
        [SettingPropertyGroup("Forge fee")]
        public float ArmorMaterialScale { get; set; } = 0.5f;

        [SettingPropertyInteger("Soft Material Per Tier", 0, 10, "0", HintText = "leather+linen on one piece cap out at this many per tier - the rest of the bill turns to iron (fittings, rivets)")]
        [SettingPropertyGroup("Forge fee")]
        public int SoftMaterialPerTier { get; set; } = 1;

        [SettingPropertyFloatingInteger("Armor Tier Bonus Percent", 0.00f, 60.00f, "0.00", HintText = "each tier above the first adds this much more material")]
        [SettingPropertyGroup("Forge fee")]
        public float ArmorTierBonusPercent { get; set; } = 15f;

        [SettingPropertyFloatingInteger("Self Repair Material Factor", 0.00f, 1.00f, "0.00", HintText = "metal needed against the full recipe")]
        [SettingPropertyGroup("Mending it yourself")]
        public float SelfRepairMaterialFactor { get; set; } = 0.25f;

        [SettingPropertyFloatingInteger("Self Repair Stamina Factor", 0.00f, 1.60f, "0.00", HintText = "stamina needed against a full piece")]
        [SettingPropertyGroup("Mending it yourself")]
        public float SelfRepairStaminaFactor { get; set; } = 0.4f;

        [SettingPropertyFloatingInteger("Failure Chance At Zero Margin", 0.00f, 1.40f, "0.00", HintText = "chance to ruin the piece at exactly the required skill")]
        [SettingPropertyGroup("Mending it yourself")]
        public float FailureChanceAtZeroMargin { get; set; } = 0.35f;

        [SettingPropertyFloatingInteger("Margin For No Failure", 0.00f, 240.00f, "0.00", HintText = "skill points above the requirement that remove all risk")]
        [SettingPropertyGroup("Mending it yourself")]
        public float MarginForNoFailure { get; set; } = 60f;

        [SettingPropertyFloatingInteger("Material Loss On Failure", 0.00f, 2.00f, "0.00", HintText = "share of metal lost when you botch it")]
        [SettingPropertyGroup("Mending it yourself")]
        public float MaterialLossOnFailure { get; set; } = 0.5f;

        [SettingPropertyInteger("Max Items Listed", 0, 96, "0", HintText = "how many pieces the forge menu lists at once")]
        [SettingPropertyGroup("Mending it yourself")]
        public int MaxItemsListed { get; set; } = 24;

        [SettingPropertyBool("Allow Ranged Crafting", HintText = "bows, crossbows, arrows and bolts as well")]
        [SettingPropertyGroup("Mending it yourself")]
        public bool AllowRangedCrafting { get; set; } = true;

        [SettingPropertyInteger("Ammo Batch Stacks", 0, 12, "0", HintText = "one fletching job yields this many sheaves of arrows or cases of bolts")]
        [SettingPropertyGroup("Mending it yourself")]
        public int AmmoBatchStacks { get; set; } = 3;

        [SettingPropertyFloatingInteger("Ranged Stamina Factor", 0.00f, 1.40f, "0.00", HintText = "bows and crossbows cost tier x stamina-per-tier x this - bowyery is lighter work than plate, as at the weapon bench")]
        [SettingPropertyGroup("Mending it yourself")]
        public float RangedStaminaFactor { get; set; } = 0.35f;

        [SettingPropertyFloatingInteger("Ranged High Tier Cost Factor", 0.00f, 8.00f, "0.00", HintText = "bows and crossbows of tier 5-6 eat this many times the materials - masterworks are not massed out of a sack of sticks")]
        [SettingPropertyGroup("Mending it yourself")]
        public float RangedHighTierCostFactor { get; set; } = 2f;

        [SettingPropertyFloatingInteger("Ranged Failure Factor", 0.00f, 2.00f, "0.00", HintText = "ruin-risk multiplier for bows, crossbows and ammunition - wood forgives more than a quench")]
        [SettingPropertyGroup("Mending it yourself")]
        public float RangedFailureFactor { get; set; } = 0.5f;

        [SettingPropertyFloatingInteger("Legendary Value Floor", 0.00f, 100000.00f, "0.00", HintText = "an unbuyable piece worth at least this much is a LEGEND - legendary bills and the one-of-a-kind rule apply")]
        [SettingPropertyGroup("Mending it yourself")]
        public float LegendaryValueFloor { get; set; } = 25000f;

        [SettingPropertyFloatingInteger("Legendary Material Factor", 0.00f, 16.00f, "0.00", HintText = "a legend's bill: every material count multiplied by this, plus the noblest steel on top")]
        [SettingPropertyGroup("Mending it yourself")]
        public float LegendaryMaterialFactor { get; set; } = 4f;

        [SettingPropertyInteger("Legendary Skill Needed", 0, 1000, "0", HintText = "no legend leaves the forge below this Smithing")]
        [SettingPropertyGroup("Mending it yourself")]
        public int LegendarySkillNeeded { get; set; } = 250;

        [SettingPropertyFloatingInteger("Smelting Return Share", 0.00f, 1.80f, "0.00", HintText = "share of the metal that comes back when you break a piece down")]
        [SettingPropertyGroup("Mending it yourself")]
        public float SmeltingReturnShare { get; set; } = 0.45f;

        [SettingPropertyFloatingInteger("Smelting Skill Bonus", 0.00f, 1.00f, "0.00", HintText = "extra recovery per point of Smithing")]
        [SettingPropertyGroup("Mending it yourself")]
        public float SmeltingSkillBonus { get; set; } = 0.002f;

        [SettingPropertyBool("Troop Wear Enabled", HintText = "the men's kit wears with every battle: a share of pieces in use drops one condition step - mend it at the smith")]
        [SettingPropertyGroup("Wear and tear")]
        public bool TroopWearEnabled { get; set; } = true;

        [SettingPropertyFloatingInteger("Troop Wear Percent", 0.00f, 16.00f, "0.00", HintText = "this share of pieces IN USE takes one step of wear per battle")]
        [SettingPropertyGroup("Wear and tear")]
        public float TroopWearPercent { get; set; } = 4f;

        [SettingPropertyFloatingInteger("Troop Wear Per Hit", 0.00f, 1.00f, "0.00", HintText = "fought battle: chance that one hit on a soldier wears the piece it struck (helmet, body armour, gloves, greaves, cape) one step")]
        [SettingPropertyGroup("Wear and tear")]
        public float TroopWearPerHit { get; set; } = 0.05f;

        [SettingPropertyFloatingInteger("Troop Wear Per Block", 0.00f, 1.00f, "0.00", HintText = "fought battle: chance that a blocked blow wears a soldier's shield one step")]
        [SettingPropertyGroup("Wear and tear")]
        public float TroopWearPerBlock { get; set; } = 0.03f;

        [SettingPropertyFloatingInteger("Troop Wear Per Strike", 0.00f, 1.00f, "0.00", HintText = "fought battle: chance that a landed blow wears a soldier's weapon one step")]
        [SettingPropertyGroup("Wear and tear")]
        public float TroopWearPerStrike { get; set; } = 0.01f;

        [SettingPropertyFloatingInteger("Troop Wear Per Shot", 0.00f, 1.00f, "0.00", HintText = "fought battle: chance that one shot wears a soldier's bow or crossbow one step")]
        [SettingPropertyGroup("Wear and tear")]
        public float TroopWearPerShot { get; set; } = 0.002f;

        [SettingPropertyFloatingInteger("Troop Wear Base Casualty Share", 0.00f, 1.00f, "0.00", HintText = "auto-resolved battle: at this share of wounded men the stores wear by TroopWearPercent; lighter fights wear less, heavier more")]
        [SettingPropertyGroup("Wear and tear")]
        public float TroopWearBaseCasualtyShare { get; set; } = 0.10f;

        [SettingPropertyBool("Loot Price Follows Condition", HintText = "Spoils of War loot is priced by its condition (Plundered 55%, Damaged 40%, Battered 25%, Mangled 10% - a wreck)")]
        [SettingPropertyGroup("Wear and tear")]
        public bool LootPriceFollowsCondition { get; set; } = true;

        [SettingPropertyBool("Wear Enabled", HintText = "gear loses condition with use")]
        [SettingPropertyGroup("Wear and tear")]
        public bool WearEnabled { get; set; } = true;

        [SettingPropertyBool("Show Condition Percent", HintText = "damaged gear carries its state in the name - (100%) is mint, (1%) is a wreck")]
        [SettingPropertyGroup("Wear and tear")]
        public bool ShowConditionPercent { get; set; } = true;

        [SettingPropertyBool("Condition Scales Stats", HintText = "Jeff's rule: protection and edge follow condition - light wear costs little, heavy wear costs dearly")]
        [SettingPropertyGroup("Wear and tear")]
        public bool ConditionScalesStats { get; set; } = true;

        [SettingPropertyFloatingInteger("Condition Penalty Max", 0.00f, 360.00f, "0.00", HintText = "an all-but-broken piece (1%) still keeps this much less - never the full hundred")]
        [SettingPropertyGroup("Wear and tear")]
        public float ConditionPenaltyMax { get; set; } = 90f;

        [SettingPropertyFloatingInteger("Condition Penalty Exponent", 0.00f, 8.00f, "0.00", HintText = "the curve: above 1 = small wear is cheap, deep wear bites (99% state ~ -0.5%, 50% ~ -41%, 1% ~ -89%)")]
        [SettingPropertyGroup("Wear and tear")]
        public float ConditionPenaltyExponent { get; set; } = 2.0f;

        [SettingPropertyInteger("Armor Pool Min Points", 0, 160, "0", HintText = "gloves, greaves and capes wear like the rest of the harness: their wear pool counts at least this many armour points")]
        [SettingPropertyGroup("Wear and tear")]
        public int ArmorPoolMinPoints { get; set; } = 40;

        [SettingPropertyFloatingInteger("Wear Per Battle", 0.00f, 1.00f, "0.00", HintText = "flat wear per battle ON TOP of real damage - 0 = gear suffers only when something actually hits it")]
        [SettingPropertyGroup("Wear and tear")]
        public float WearPerBattle { get; set; } = 0f;

        [SettingPropertyFloatingInteger("Wear Damage Factor", 0.00f, 1.00f, "0.00", HintText = "wear per point of damage the STRUCK piece takes (armour wears where the blow lands)")]
        [SettingPropertyGroup("Wear and tear")]
        public float WearDamageFactor { get; set; } = 0.15f;

        [SettingPropertyFloatingInteger("Missile Armor Wear Percent", 0.00f, 40.00f, "0.00", HintText = "arrows punch tidy little holes, not rents - armour counts only this % of missile damage as wear (hp damage unchanged)")]
        [SettingPropertyGroup("Wear and tear")]
        public float MissileArmorWearPercent { get; set; } = 10f;

        [SettingPropertyFloatingInteger("Harness Wear Factor", 0.00f, 1.00f, "0.00", HintText = "saddle and barding count only this share of the horse's raw hits as wear - at the old half-share saddles kept dying under you")]
        [SettingPropertyGroup("Wear and tear")]
        public float HarnessWearFactor { get; set; } = 0.15f;

        [SettingPropertyFloatingInteger("Durability Per Armor Point", 0.00f, 53.32f, "0.00", HintText = "Jeff's pool: every point of protection gives this much durability, times the tier - 61 armor at tier 3 = 61 x 13.33 x 3 = 2439 points, and damage taken subtracts one for one. 13.33 wears your armour 1.5 times faster than the old 20")]
        [SettingPropertyGroup("Wear and tear")]
        public float DurabilityPerArmorPoint { get; set; } = 13.33f;

        [SettingPropertyFloatingInteger("Wear Weapon Per Hit", 0.00f, 1.00f, "0.00", HintText = "wear on your weapon for every blow you land (bows wear per arrow that strikes home)")]
        [SettingPropertyGroup("Wear and tear")]
        public float WearWeaponPerHit { get; set; } = 0.25f;

        [SettingPropertyFloatingInteger("Wear Shield Factor", 0.00f, 1.20f, "0.00", HintText = "shields are built to take it - blocked damage wears them at this share")]
        [SettingPropertyGroup("Wear and tear")]
        public float WearShieldFactor { get; set; } = 0.3f;

        [SettingPropertyBool("Shield Missile Guard Enabled", HintText = "a shield is not there to be shot to pieces - arrows and bolts barely mark it")]
        [SettingPropertyGroup("Wear and tear")]
        public bool ShieldMissileGuardEnabled { get; set; } = true;

        [SettingPropertyFloatingInteger("Missile Shield Damage Percent", 0.00f, 4.00f, "0.00", HintText = "percent of the arrow or bolt damage a shield actually takes, both in the fight and as wear (RBM counts 150%, the bare game 15%)")]
        [SettingPropertyGroup("Wear and tear")]
        public float MissileShieldDamagePercent { get; set; } = 1f;

        [SettingPropertyInteger("Bow Uses At Tier1", 0, 10000, "0", HintText = "a tier-1 bow survives this many shots; each tier multiplies (tier 3 = x3, tier 6 = x6)")]
        [SettingPropertyGroup("Wear and tear")]
        public int BowUsesAtTier1 { get; set; } = 2500;

        [SettingPropertyFloatingInteger("Bow Skill Bonus Percent Per Point", 0.00f, 4.00f, "0.00", HintText = "every point of Bow/Crossbow skill adds this percent more shots - a trained hand spares the weapon")]
        [SettingPropertyGroup("Wear and tear")]
        public float BowSkillBonusPercentPerPoint { get; set; } = 1f;

        [SettingPropertyFloatingInteger("Tier Durability Factor", 0.00f, 1.00f, "0.00", HintText = "each tier of the piece slows the wear by this much")]
        [SettingPropertyGroup("Wear and tear")]
        public float TierDurabilityFactor { get; set; } = 0.22f;

        [SettingPropertyInteger("Threshold Worn", 0, 280, "0", HintText = "below this the gear starts to show")]
        [SettingPropertyGroup("Wear and tear")]
        public int ThresholdWorn { get; set; } = 70;

        [SettingPropertyInteger("Threshold Damaged", 0, 180, "0", HintText = "below this it is plainly damaged and worth less")]
        [SettingPropertyGroup("Wear and tear")]
        public int ThresholdDamaged { get; set; } = 45;

        [SettingPropertyInteger("Threshold Ruined", 0, 80, "0", HintText = "below this it is barely worth carrying")]
        [SettingPropertyGroup("Wear and tear")]
        public int ThresholdRuined { get; set; } = 20;

        [SettingPropertyFloatingInteger("Repair Cost Factor", 0.00f, 2.00f, "0.00", HintText = "share of item value for a full repair (with Smith Mend From Market on, the town smiths charge by their day wages instead - this share is left only for pieces they have no recipe for)")]
        [SettingPropertyGroup("Wear and tear")]
        public float RepairCostFactor { get; set; } = 0.5f;

        [SettingPropertyBool("Break At Zero Condition", HintText = "at zero the piece finally breaks and is gone for good")]
        [SettingPropertyGroup("Wear and tear")]
        public bool BreakAtZeroCondition { get; set; } = false;

        [SettingPropertyBool("Unique Crowns Enabled", HintText = "crowns of kings and queens are unique regalia: sane armour, out of the shops, impossible to forge - the pieces already in play become the only ones")]
        [SettingPropertyGroup("Crown jewels")]
        public bool UniqueCrownsEnabled { get; set; } = true;

        [SettingPropertyInteger("Unique Crown Head Armor", 0, 40, "0", HintText = "head armour of a crown - it is jewellery, not a helmet (ROT ships every crown at 75)")]
        [SettingPropertyGroup("Crown jewels")]
        public int UniqueCrownHeadArmor { get; set; } = 10;

        [SettingPropertyInteger("Bk Supply Days Cap", 0, 16, "0", HintText = "AI parties stock this many days of Banner Kings supplies instead of 10 - healthier logistics than living hand to mouth (0 = off; Jeff 31.08: set to 4)")]
        [SettingPropertyGroup("The lean quartermasters")]
        public int BkSupplyDaysCap { get; set; } = 4;

        [SettingPropertyInteger("Bk Supply Max Pieces", 0, 48, "0", HintText = "hard ceiling: an AI party never stockpiles more than this many pieces of any one supply - repairs need a few hides, not a warehouse (0 = off)")]
        [SettingPropertyGroup("The lean quartermasters")]
        public int BkSupplyMaxPieces { get; set; } = 12;

        [SettingPropertyBool("Ai Starving Buys Any Price", HintText = "a STARVING AI party buys the cheapest food it can afford at ANY price - hunger does not haggle (vanilla and Banner Kings refuse anything above 120 denars, so lords starve on a full market in wartime)")]
        [SettingPropertyGroup("The lean quartermasters")]
        public bool AiStarvingBuysAnyPrice { get; set; } = true;

        [SettingPropertyInteger("Food Consumption Cut Percent", 0, 160, "0", HintText = "a party eats this much LESS per day, player and AI alike (0 = vanilla). The world marches slower now (world pace, long year), so the same road costs more days - rations must stretch to match")]
        [SettingPropertyGroup("The lean quartermasters")]
        public int FoodConsumptionCutPercent { get; set; } = 40;

        [SettingPropertyBool("Crossing Law Enabled", HintText = "fortified crossings (The Twins) bar their bridge to ENEMIES of the holder - allies and neutrals pass; take the castle, make peace, or go by sea")]
        [SettingPropertyGroup("The crossing law")]
        public bool CrossingLawEnabled { get; set; } = true;

        [SettingPropertyBool("Crossing Law Ai", HintText = "the bridge watch also turns back hostile AI lord parties (they get a fallback point and a 3h cooldown so their pathfinding never jams)")]
        [SettingPropertyGroup("The crossing law")]
        public bool CrossingLawAi { get; set; } = true;

        [SettingPropertyFloatingInteger("Crossing Radius", 0.00f, 12.00f, "0.00", HintText = "how far the bridge watch reaches around the crossing castle")]
        [SettingPropertyGroup("The crossing law")]
        public float CrossingRadius { get; set; } = 3f;

        [SettingPropertyInteger("Volunteer Regen Percent", 0, 400, "0", HintText = "notables refill their volunteer slots at this percent of the normal daily chance - losses should STING, for lords and player alike (100 = vanilla, 0 = off; Jeff 30.08: halved again, the towns still teemed with recruits)")]
        [SettingPropertyGroup("The slow muster")]
        public int VolunteerRegenPercent { get; set; } = 100;

        [SettingPropertyInteger("Healing Regen Percent", 0, 200, "0", HintText = "wounded men and heroes heal on the map at this percent of the normal daily rate - medicine perks still count on top (100 = vanilla)")]
        [SettingPropertyGroup("The slow mending")]
        public int HealingRegenPercent { get; set; } = 50;

        [SettingPropertyInteger("Ai Healing Regen Percent", 0, 400, "0", HintText = "AI parties heal at this percent of the normal daily rate (100 = vanilla, above 100 = faster) - vanilla tempo keeps lords' wounded from piling up for weeks after a famine or a battle")]
        [SettingPropertyGroup("The slow mending")]
        public int AiHealingRegenPercent { get; set; } = 100;

        [SettingPropertyInteger("Starvation Wound Percent", 0, 20, "0", HintText = "a STARVING party in the field loses this share of its regulars to wounds per day (vanilla 25 - a whole army wounded in four days; 0 = off, 25 = vanilla). Player and AI alike")]
        [SettingPropertyGroup("The slow mending")]
        public int StarvationWoundPercent { get; set; } = 5;

        [SettingPropertyBool("Auto Sort Party", HintText = "the party roster keeps itself in order: cavalry, horse archers, infantry, archers - each arm by tier, best first (no more dragging rows by hand)")]
        [SettingPropertyGroup("The tidy muster")]
        public bool AutoSortParty { get; set; } = true;

        [SettingPropertyBool("Muster Book Enabled", HintText = "the muster book in town, village and forge menus: inspect any troop (experience, full kit) and ASSIGN which piece from the stores the whole company of that troop must wear")]
        [SettingPropertyGroup("The tidy muster")]
        public bool MusterBookEnabled { get; set; } = true;

        [SettingPropertyBool("Craft Result Popup", HintText = "forging armour, bows or ammo ends with a result window: every stat, with the quality bonus or the botch penalty spelled out - same rule as weapons")]
        [SettingPropertyGroup("The finished piece")]
        public bool CraftResultPopup { get; set; } = true;

        [SettingPropertyBool("Rich Quality Modifiers", HintText = "fine/masterwork/legendary touch MORE than one stat (RBM strips them to bare damage): melee gains speed, ranged gains missile speed, botched work loses both")]
        [SettingPropertyGroup("The finished piece")]
        public bool RichQualityModifiers { get; set; } = true;

        [SettingPropertyBool("Troop Self Mend Enabled", HintText = "each day in a town the men pay the smith from their own wages to mend the worst pieces in the company stores")]
        [SettingPropertyGroup("The finished piece")]
        public bool TroopSelfMendEnabled { get; set; } = true;

        [SettingPropertyBool("Men Purse Enabled", HintText = "the men's share of the spoils is theirs: in a town they sell the spare kit to the merchants, mend their gear at the smiths, buy what they lack and spend the rest there; you buy from them what you take from the stores")]
        [SettingPropertyGroup("The finished piece")]
        public bool MenPurseEnabled { get; set; } = true;

        [SettingPropertyFloatingInteger("Surplus Keep Percent", 0.00f, 40.00f, "0.00", HintText = "spare kit the stores keep above what the men wear before the rest goes to the merchants")]
        [SettingPropertyGroup("The finished piece")]
        public float SurplusKeepPercent { get; set; } = 10f;

        [SettingPropertyFloatingInteger("Lord Loot Third Percent", 0.00f, 132.00f, "0.00", HintText = "an AI lord's cut (the captain's third) when his men sell their spare kit in a town")]
        [SettingPropertyGroup("The finished piece")]
        public float LordLootThirdPercent { get; set; } = 33f;

        [SettingPropertyBool("Ai Wear Enabled", HintText = "AI lords' kit wears too: battle wear on pieces in use, loot comes in battered, town smiths mend it day by day from the men's purse")]
        [SettingPropertyGroup("The finished piece")]
        public bool AiWearEnabled { get; set; } = true;

        [SettingPropertyBool("Mine Wages Stay In Town", HintText = "when a town buys a Banner Kings mine's ore, half the price is the lord's due and half the miners' wages - spent in that same town (before, that half vanished)")]
        [SettingPropertyGroup("The finished piece")]
        public bool MineWagesStayInTown { get; set; } = true;

        [SettingPropertyFloatingInteger("Work Hours Per Man Day", 0.00f, 64.00f, "0.00", HintText = "hours a town smith works in a day when there are orders - dawn to dusk with journeymen at the bench; the town's smiths share them between mending (your men and AI lords alike) and new work in the workshops")]
        [SettingPropertyGroup("The finished piece")]
        public float WorkHoursPerManDay { get; set; } = 16f;

        [SettingPropertyInteger("Troop Self Mend Percent Per Day", 0, 40, "0", HintText = "the men mend this PERCENT of all battle-worn pieces in the stores each day in town (at least 3 pieces) - a full refit takes about 100/percent days of rest; pay the smith yourself to skip the wait")]
        [SettingPropertyGroup("The finished piece")]
        public int TroopSelfMendPercentPerDay { get; set; } = 10;

        [SettingPropertyBool("Troop Skill Auto Fit", HintText = "troops are audited on load: any skill below the demands of their OWN template gear (armour->Athletics, mount->Riding, weapons->their class) is raised to match - the elite keeps its heavy plate because it has earned the muscles")]
        [SettingPropertyGroup("Skills rule the gear")]
        public bool TroopSkillAutoFit { get; set; } = true;

        [SettingPropertyBool("Skills Decide Enabled", HintText = "no more 'default tier +2': troops use ANY gear their stats allow, main weapon follows their best skill, the backup their second best (an archer carries bow, two quivers and a sidearm of his second skill)")]
        [SettingPropertyGroup("Skills rule the gear")]
        public bool SkillsDecideEnabled { get; set; } = true;

        [SettingPropertyInteger("Weapon Skill Per Tier", 0, 140, "0", HintText = "Weapon Tier Law: a weapon or shield needs at least (tier - 1) x this in its skill, whatever the data says - a tier 6 blade wants 175, so a One Handed 30 bandit never 'qualifies' for it; 0 turns the law off. Applied at session start")]
        [SettingPropertyGroup("Skills rule the gear")]
        public int WeaponSkillPerTier { get; set; } = 35;

        [SettingPropertyBool("Elephant Quarantine Enabled", HintText = "elephants and their barding sell only in settlements of their own culture - no war beasts wintering in Winterfell")]
        [SettingPropertyGroup("The menagerie")]
        public bool ElephantQuarantineEnabled { get; set; } = true;

        [SettingPropertyBool("Hideout Purge Enabled", HintText = "a cleared hideout must be SEARCHED: the plundered gold, renown and the gratitude of the district wait behind one more step")]
        [SettingPropertyGroup("The hideout purge")]
        public bool HideoutPurgeEnabled { get; set; } = true;

        [SettingPropertyInteger("Hideout Gold Base", 0, 600, "0", HintText = "gold a lived-in den keeps hidden before counting its bands - the hoard its bandits do not touch, found by whoever clears and searches it")]
        [SettingPropertyGroup("The hideout purge")]
        public int HideoutGoldBase { get; set; } = 150;

        [SettingPropertyInteger("Hideout Gold Per Band", 0, 480, "0", HintText = "each band that calls the den home adds this much to the hoard it keeps; whatever lies above the kept hoard is spent - on gear for its bands and on their living in town")]
        [SettingPropertyGroup("The hideout purge")]
        public int HideoutGoldPerBand { get; set; } = 120;

        [SettingPropertyFloatingInteger("Hideout Renown", 0.00f, 20.00f, "0.00", HintText = "renown for purging a hideout - the realm hears of it")]
        [SettingPropertyGroup("The hideout purge")]
        public float HideoutRenown { get; set; } = 5f;

        [SettingPropertyInteger("Hideout Rep Max", 0, 20, "0", HintText = "relation gained with notables right next to the den, fading to zero at the edge of the district")]
        [SettingPropertyGroup("The hideout purge")]
        public int HideoutRepMax { get; set; } = 5;

        [SettingPropertyFloatingInteger("Hideout Rep Radius", 0.00f, 200.00f, "0.00", HintText = "the district: map-distance within which settlements care about the purge")]
        [SettingPropertyGroup("The hideout purge")]
        public float HideoutRepRadius { get; set; } = 50f;

        [SettingPropertyFloatingInteger("Hideout Search Solo Hours", 0.00f, 96.00f, "0.00", HintText = "searching the den alone takes this long - a lone man turns every bedroll himself")]
        [SettingPropertyGroup("The hideout purge")]
        public float HideoutSearchSoloHours { get; set; } = 24f;

        [SettingPropertyFloatingInteger("Hideout Search Per Man Hours", 0.00f, 2.00f, "0.00", HintText = "every soldier in the party cuts the search by this many hours")]
        [SettingPropertyGroup("The hideout purge")]
        public float HideoutSearchPerManHours { get; set; } = 0.5f;

        [SettingPropertyFloatingInteger("Hideout Search Min Hours", 0.00f, 16.00f, "0.00", HintText = "the search never drops below this many hours - some stones only come up slowly")]
        [SettingPropertyGroup("The hideout purge")]
        public float HideoutSearchMinHours { get; set; } = 4f;

        [SettingPropertyBool("Hideout Reprisal Enabled", HintText = "nearby bands mass to take their den back while you dig - unless your line scares them off")]
        [SettingPropertyGroup("The hideout purge")]
        public bool HideoutReprisalEnabled { get; set; } = true;

        [SettingPropertyFloatingInteger("Hideout Reprisal Radius", 0.00f, 160.00f, "0.00", HintText = "map-distance within which bandit parties join the reprisal")]
        [SettingPropertyGroup("The hideout purge")]
        public float HideoutReprisalRadius { get; set; } = 40f;

        [SettingPropertyFloatingInteger("Hideout Reprisal Flee Odds", 0.00f, 12.00f, "0.00", HintText = "at this strength advantage (yours vs theirs) the reprisal loses its nerve and melts away")]
        [SettingPropertyGroup("The hideout purge")]
        public float HideoutReprisalFleeOdds { get; set; } = 3f;

        [SettingPropertyFloatingInteger("Hideout Reprisal Hours", 0.00f, 192.00f, "0.00", HintText = "the horde hunts you this long - then gives up and drifts back to its old ways")]
        [SettingPropertyGroup("The hideout purge")]
        public float HideoutReprisalHours { get; set; } = 48f;

        [SettingPropertyBool("Loot Arrives Worn", HintText = "gear stripped from the fallen comes to you already used")]
        [SettingPropertyGroup("Loot from the field")]
        public bool LootArrivesWorn { get; set; } = true;

        [SettingPropertyFloatingInteger("Loot Wear Base", 0.00f, 180.00f, "0.00", HintText = "the condition of an average piece of loot")]
        [SettingPropertyGroup("Loot from the field")]
        public float LootWearBase { get; set; } = 45f;

        [SettingPropertyFloatingInteger("Loot Wear Spread", 0.00f, 100.00f, "0.00", HintText = "how widely that varies")]
        [SettingPropertyGroup("Loot from the field")]
        public float LootWearSpread { get; set; } = 25f;

        [SettingPropertyBool("Captive Spoils Enabled", HintText = "a man you take captive is stripped at the rope: his whole kit lands in your baggage, battle-worn")]
        [SettingPropertyGroup("Loot from the field")]
        public bool CaptiveSpoilsEnabled { get; set; } = true;

        [SettingPropertyBool("Captive Spoils Include Mounts", HintText = "his horse and harness are seized too")]
        [SettingPropertyGroup("Loot from the field")]
        public bool CaptiveSpoilsIncludeMounts { get; set; } = true;

        [SettingPropertyBool("Captive Rags Preview", HintText = "the party screen shows your captives the way you actually keep them: stripped to rags, not in the full armour you already took (icons stay, only the 3D preview changes)")]
        [SettingPropertyGroup("Loot from the field")]
        public bool CaptiveRagsPreview { get; set; } = true;

        [SettingPropertyBool("Battlefield Law Enabled", HintText = "battles you fight yourself: the dead drop their real gear (DTE), Spoils of War steps aside")]
        [SettingPropertyGroup("The law of the battlefield")]
        public bool BattlefieldLawEnabled { get; set; } = true;

        [SettingPropertyBool("Loot Arrives Battle Worn", HintText = "loot-screen items from fought battles arrive battle-worn but keep their worth")]
        [SettingPropertyGroup("The law of the battlefield")]
        public bool LootArrivesBattleWorn { get; set; } = true;

        [SettingPropertyBool("Sim Battle Full Drop", HintText = "auto-resolved battles ignore the hidden per-tier drop multipliers")]
        [SettingPropertyGroup("The law of the battlefield")]
        public bool SimBattleFullDrop { get; set; } = true;

        [SettingPropertyInteger("Player Loot Share Percent", 0, 132, "0", HintText = "the captain's third: your cut of what YOUR MEN strip from the foes they felled (the rest is theirs, in the army armoury); foes felled by you or your companions are all yours; auto-resolved battles use this share for everything")]
        [SettingPropertyGroup("The law of the battlefield")]
        public int PlayerLootSharePercent { get; set; } = 33;

        [SettingPropertyBool("Wreck Salvage Enabled", HintText = "the piece smashed by the killing blow is not lost - it lands in the loot as a wreck to mend at the forge")]
        [SettingPropertyGroup("The law of the battlefield")]
        public bool WreckSalvageEnabled { get; set; } = true;

        [SettingPropertyInteger("Loot Min Condition Percent", 0, 12, "0", HintText = "gear battered down to this percent of its worth or less is DESTROYED - it never reaches the loot screen (0 = off)")]
        [SettingPropertyGroup("The law of the battlefield")]
        public int LootMinConditionPercent { get; set; } = 3;

        [SettingPropertyInteger("Legendary Loot Value Floor", 0, 400000, "0", HintText = "weapons worth this much clean (the named blades of the realm) never lie in the common loot sacks (0 = off)")]
        [SettingPropertyGroup("The law of the battlefield")]
        public int LegendaryLootValueFloor { get; set; } = 100000;

        [SettingPropertyBool("Spoils Clan Real Soldiers", HintText = "your Spoils of War mercenary company gets no soldiers out of thin air: a founded, reformed, newly hired or re-formed warband starts with its captain alone (no ready-made 50-odd men from the party template, no 20 initial troops, no volunteers from the map, and Banner Kings no longer swaps the captain out of his own warband for a soldier from nowhere) and grows only by real recruitment - notables' volunteers and tavern hirelings, paid from the company's own purse; what you pay Spoils of War to found, reform or hire a warband goes whole into the company's purse instead of partly vanishing (off = Spoils of War as before; the day's log line shows how many men either way)")]
        [SettingPropertyGroup("The law of the battlefield")]
        public bool SpoilsClanRealSoldiers { get; set; } = true;

        [SettingPropertyBool("Spoils No Auto Sale", HintText = "no automatic sale from the war stockpile: Spoils of War's quartermaster sold cheap stockpile gear every day - the goods vanished and the town treasury got coin that nobody paid; this stops it whatever the Spoils of War menu says (every Spoils preset turns auto-sell back on) - the gear stays in the stockpile until you take it (the day's log line shows how much was stopped)")]
        [SettingPropertyGroup("The law of the battlefield")]
        public bool SpoilsNoAutoSale { get; set; } = true;

        [SettingPropertyBool("Spoils No Free Gold", HintText = "no Spoils of War money out of thin air: no coins on the dead, no baggage-train chest, no lucky chest, ransom or extra gear while collecting (the beaten side's purse already pays the winner); the fence, the quartermaster's salvage and gear given to your mercenary company are paid from the town's coffers above its reserve, never above the market price, and the goods go onto the town's stalls - what the town cannot pay for stays with you; your company's share of its battle loot comes out of its own treasury; the war stockpile takes gear only clean or plundered (off = Spoils of War as before; the day's log line shows how much either way)")]
        [SettingPropertyGroup("The law of the battlefield")]
        public bool SpoilsNoFreeGold { get; set; } = true;

        [SettingPropertyBool("Spoils Quartermaster Repair", HintText = "the Spoils of War quartermaster's repairs are done by this town's smiths, like every other repair in Armoury: you pay into the town's coffers the smiths' work (with Smith Mend From Market on: its share of the days a master spent making the piece, at the town's day wage; off: a quarter of the worth it has lost), plus the materials they take from this market at its prices - iron (crude iron, scrap from wrecks or ore), wood, leather, linen or wool, more the worse the piece; with no such materials on the market the piece waits; wrecks (Mangled, or worn to a tenth of their worth or less - see Wrecks To Scrap) are not restored here - mend them at a forge with your own materials, or salvage them (off = Spoils of War as before: its price, the coin vanishes, no materials, wrecks restored; the day's log line shows how much either way)")]
        [SettingPropertyGroup("The law of the battlefield")]
        public bool SpoilsQuartermasterRepair { get; set; } = true;

        [SettingPropertyBool("Living Economy Sealed", HintText = "BetterEconomy (Living Economy) may not make gold vanish nor conjure goods, men or experience out of nothing: its actions that would pay your gold into nothing (contributions to a town or castle treasury, town and village investments, market access, armory, training camp, paid drill) stay in its menus but are closed and say why, and the Lord wealth realism switch in its ledger stays off; finished armories stop turning market iron into weapons from nothing, AI training camps stop handing out free experience, villages stop their second production from nothing, and the taking of lords' gold and the AI's 5000 market-access fee stop (the fee only once BetterEconomy's own settings shut village trade diversion - until then the fee is the only thing holding it back; off = BetterEconomy as before; the log shows what was stopped)")]
        [SettingPropertyGroup("The living economy")]
        public bool LivingEconomySealed { get; set; } = true;

        [SettingPropertyBool("Plague Spares Your Men", HintText = "sickness may weaken your men - it will not kill them: any troop death caused by a disease system is refused for YOUR party (the rest of the world still buries its dead)")]
        [SettingPropertyGroup("Plague shield")]
        public bool PlagueSparesYourMen { get; set; } = true;

        [SettingPropertyInteger("Plague Shield Log Every", 0, 80, "0", HintText = "after the first three saves, log every Nth - a long epidemic must not drown the log")]
        [SettingPropertyGroup("Plague shield")]
        public int PlagueShieldLogEvery { get; set; } = 20;

        [SettingPropertyBool("Desertion Law Enabled", HintText = "men desert only when party morale falls below THEIR tier's threshold (vanilla: below 10 for everyone); a fed and paid party at decent morale loses no one; pay and party-size desertion stay as in vanilla")]
        [SettingPropertyGroup("Desertion")]
        public bool DesertionLawEnabled { get; set; } = true;

        [SettingPropertyInteger("Desertion Morale Tier1", 0, 100, "0", HintText = "morale threshold for tier 1 troops - pressed men run first; every tier above lowers the threshold by Desertion Morale Step Per Tier; nobody deserts above this morale")]
        [SettingPropertyGroup("Desertion")]
        public int DesertionMoraleTier1 { get; set; } = 25;

        [SettingPropertyInteger("Desertion Morale Step Per Tier", 0, 12, "0", HintText = "with tier 1 at 25 and step 3: tier 2 deserts below 22, tier 3 below 19, tier 4 below 16, tier 5 below 13, tier 6 below 10 (0 = one threshold for all tiers)")]
        [SettingPropertyGroup("Desertion")]
        public int DesertionMoraleStepPerTier { get; set; } = 3;

        [SettingPropertyInteger("Desertion Morale Floor", 0, 40, "0", HintText = "no threshold drops below this - even the elite leave once morale is broken")]
        [SettingPropertyGroup("Desertion")]
        public int DesertionMoraleFloor { get; set; } = 10;

        [SettingPropertyFloatingInteger("Desertion Percent Per Morale Point", 0.00f, 4.00f, "0.00", HintText = "share of a stack (percent) that deserts per day for every morale point below its threshold")]
        [SettingPropertyGroup("Desertion")]
        public float DesertionPercentPerMoralePoint { get; set; } = 1.0f;

        [SettingPropertyInteger("Desertion Daily Cap Percent", 0, 100, "0", HintText = "at most this share of a stack deserts in one day")]
        [SettingPropertyGroup("Desertion")]
        public int DesertionDailyCapPercent { get; set; } = 25;

        [SettingPropertyBool("Desertion Law For Ai", HintText = "apply the tiered thresholds to AI lords as well (off: AI keeps vanilla desertion below morale 10)")]
        [SettingPropertyGroup("Desertion")]
        public bool DesertionLawForAi { get; set; } = false;

        [SettingPropertyBool("Unique Gear Law Enabled", HintText = "named heroes' gear (Ramsay, the Hound, the Mountain, Brienne, Renly...) belongs to its owner alone: copies in armouries, packs and on other heroes become same-tier gear of the wearer's own culture, and DTE swaps them on the way into any armoury")]
        [SettingPropertyGroup("The law of the battlefield")]
        public bool UniqueGearLawEnabled { get; set; } = true;

        [SettingPropertyInteger("Min Sell Percent Of Value", 0, 10, "0", HintText = "merchants never pay less than this share of what an item is worth in its present condition (with Sell Price By Condition off: of its clean worth) - scrap is still metal and leather. Only junk ever sinks this low: above the floor the price of worn gear follows its condition and the stall's supply and demand (0 = off)")]
        [SettingPropertyGroup("The law of the battlefield")]
        public int MinSellPercentOfValue { get; set; } = 2;

        [SettingPropertyBool("One Scrap Floor", HintText = "one floor for junk, after the stall's supply and demand: in towns and castles worn-out gear fetches its condition times the stall, never under Min Sell Percent Of Value - and never more than that stall itself asks for the very same piece, so a glutted stall pays little and nobody can buy junk cheap and sell it back at the floor. The glutted market's 5% start steps aside while supply and demand is on. Off = as before: a floor before the stall, the glutted market's 5% start, and the floor again after the stall")]
        [SettingPropertyGroup("The law of the battlefield")]
        public bool OneScrapFloor { get; set; } = true;

        [SettingPropertyBool("Bk Trade Penalty Once", HintText = "Banner Kings' trade penalty on arms, armour and saddles counts ONCE (x5, as Banner Kings means it): the price model of the Banner Kings - Realm of Thrones patch calls the game's own penalty, which Banner Kings has already multiplied by 5, and Banner Kings then multiplied the result by 5 again - x25 on every weapon and piece of armour you or the AI sell (x225 in a castle). Only that doubled patch is lifted; Banner Kings' castle, Gladiator and perk effects still apply once. Applied when a session loads; the log line 'Kara handlowa BK' shows the multiplier at every start (off = as before, x25)")]
        [SettingPropertyGroup("The law of the battlefield")]
        public bool BkTradePenaltyOnce { get; set; } = true;

        [SettingPropertyBool("Sell Price By Condition", HintText = "what a merchant pays for arms, armour and horses follows the piece's condition and quality: Min Sell Percent Of Value and the wholesale price between towns count from what the piece is worth as it is, not from a clean one; when the game's price drops to its 1-denar minimum the true fraction is kept before supply and demand multiplies it; and no town or castle stall pays more for a weapon, a piece of armour, a saddle or a quiver than Sell Cap Percent Of New Ask of what it asks for a new one of the same quality. A wreck fetches pennies, a clean or finer piece more, a legendary one many times more (off = as before: floor and wholesale from the clean worth, no cap)")]
        [SettingPropertyGroup("The law of the battlefield")]
        public bool SellPriceByCondition { get; set; } = true;

        [SettingPropertyInteger("Sell Cap Percent Of New Ask", 0, 40, "0", HintText = "the most a town or castle merchant pays for a weapon, a piece of armour, a saddle or ammunition, as % of what his own stall asks for a NEW one of the same kind and quality - and never more than he charges for its wreck, so buying worn gear or spoilt arrows, putting them right and selling them back in the same town never pays; carrying mended gear to a town that lacks it still pays. Horses and pack animals are not capped (0 = no cap; needs Sell Price By Condition)")]
        [SettingPropertyGroup("The law of the battlefield")]
        public int SellCapPercentOfNewAsk { get; set; } = 10;

        [SettingPropertyBool("Enlisted Soldier No Looting", HintText = "serving in a lord's army: the quartermasters strip the field - one soldier does not pocket the army's loot and gold")]
        [SettingPropertyGroup("The law of the battlefield")]
        public bool EnlistedSoldierNoLooting { get; set; } = true;

        [SettingPropertyBool("Field Craft Enabled", HintText = "battlefield body rules: sprint fatigue, wounded penalties, bleeding, arrows that barely scratched fall off")]
        [SettingPropertyGroup("Flesh and wind")]
        public bool FieldCraftEnabled { get; set; } = true;

        [SettingPropertyBool("Sprint Fatigue Enabled", HintText = "running on foot drinks stamina points - an empty pool means a slow man")]
        [SettingPropertyGroup("Flesh and wind")]
        public bool SprintFatigueEnabled { get; set; } = true;

        [SettingPropertyBool("Use Rbm Stamina", HintText = "sprint drinks from RBM's own stamina pool (Athletics already grows it); off = our own pool with the same rules")]
        [SettingPropertyGroup("Flesh and wind")]
        public bool UseRbmStamina { get; set; } = true;

        [SettingPropertyFloatingInteger("Sprint Drain Per Second", 0.00f, 16.00f, "0.00", HintText = "stamina points one second of flat-out sprint costs, before the armour is weighed")]
        [SettingPropertyGroup("Flesh and wind")]
        public float SprintDrainPerSecond { get; set; } = 4f;

        [SettingPropertyFloatingInteger("Sprint Drain Per Kg", 0.00f, 4.00f, "0.00", HintText = "extra points per second for every kilogram of armour carried")]
        [SettingPropertyGroup("Flesh and wind")]
        public float SprintDrainPerKg { get; set; } = 1f;

        [SettingPropertyFloatingInteger("Fatigue Free Armor Kg", 0.00f, 1.00f, "0.00", HintText = "armour up to this weight costs no extra stamina")]
        [SettingPropertyGroup("Flesh and wind")]
        public float FatigueFreeArmorKg { get; set; } = 0f;

        [SettingPropertyBool("Battle Stamina Enabled", HintText = "heroes: Endurance reshapes RBM battle stamina and posture - pools double every few points, breath returns fast but winded men pant")]
        [SettingPropertyGroup("Flesh and wind")]
        public bool BattleStaminaEnabled { get; set; } = true;

        [SettingPropertyFloatingInteger("Battle End Double Every", 0.00f, 10.00f, "0.00", HintText = "pool doubles every this many Endurance points (END 2.5 = x1, END 5 = x2, END 10 = x8)")]
        [SettingPropertyGroup("Flesh and wind")]
        public float BattleEndDoubleEvery { get; set; } = 2.5f;

        [SettingPropertyFloatingInteger("Battle Regen At End1", 0.00f, 20.00f, "0.00", HintText = "stamina regained per second at Endurance 1 (Athletics adds its share on top)")]
        [SettingPropertyGroup("Flesh and wind")]
        public float BattleRegenAtEnd1 { get; set; } = 5f;

        [SettingPropertyFloatingInteger("Battle Regen At End10", 0.00f, 400.00f, "0.00", HintText = "stamina regained per second at Endurance 10 - the curve between is exponential, not a straight line")]
        [SettingPropertyGroup("Flesh and wind")]
        public float BattleRegenAtEnd10 { get; set; } = 100f;

        [SettingPropertyFloatingInteger("Battle Winded Floor", 0.00f, 1.00f, "0.00", HintText = "share of regen left with an empty bar - the emptier the lungs the slower they fill")]
        [SettingPropertyGroup("Flesh and wind")]
        public float BattleWindedFloor { get; set; } = 0.25f;

        [SettingPropertyFloatingInteger("Stamina Regen Per Second", 0.00f, 100.00f, "0.00", HintText = "without RBM: points regained each second of easing off (with RBM its own regen rules)")]
        [SettingPropertyGroup("Flesh and wind")]
        public float StaminaRegenPerSecond { get; set; } = 25f;

        [SettingPropertyFloatingInteger("Tired Speed Factor", 0.00f, 3.12f, "0.00", HintText = "top speed of a man whose pool has run dry")]
        [SettingPropertyGroup("Flesh and wind")]
        public float TiredSpeedFactor { get; set; } = 0.78f;

        [SettingPropertyBool("Wounded Penalties Enabled", HintText = "hurt men move and swing slower, the worse the wound the worse the arm")]
        [SettingPropertyGroup("Flesh and wind")]
        public bool WoundedPenaltiesEnabled { get; set; } = true;

        [SettingPropertyInteger("Wounded Below Percent", 0, 200, "0", HintText = "penalties begin below this share of health")]
        [SettingPropertyGroup("Flesh and wind")]
        public int WoundedBelowPercent { get; set; } = 50;

        [SettingPropertyFloatingInteger("Wounded Max Slow", 0.00f, 1.20f, "0.00", HintText = "top movement penalty at death's door")]
        [SettingPropertyGroup("Flesh and wind")]
        public float WoundedMaxSlow { get; set; } = 0.3f;

        [SettingPropertyInteger("Bleed Below Hp", 0, 40, "0", HintText = "bleeding starts at this many hit points or fewer")]
        [SettingPropertyGroup("Flesh and wind")]
        public int BleedBelowHp { get; set; } = 10;

        [SettingPropertyInteger("Ai Flee Below Percent", 0, 40, "0", HintText = "AI soldiers below this share of health break and run")]
        [SettingPropertyGroup("Flesh and wind")]
        public int AiFleeBelowPercent { get; set; } = 10;

        [SettingPropertyFloatingInteger("Bleed Per Second", 0.00f, 2.00f, "0.00", HintText = "health lost per second while bleeding out")]
        [SettingPropertyGroup("Flesh and wind")]
        public float BleedPerSecond { get; set; } = 0.5f;

        [SettingPropertyBool("Ai Flee When Near Death", HintText = "AI soldiers below the bleeding threshold break and run for their lives")]
        [SettingPropertyGroup("Flesh and wind")]
        public bool AiFleeWhenNearDeath { get; set; } = true;

        [SettingPropertyBool("Arrow Unstick Enabled", HintText = "arrows the armour stopped do not stay stuck in a man (shields keep theirs)")]
        [SettingPropertyGroup("Flesh and wind")]
        public bool ArrowUnstickEnabled { get; set; } = true;

        [SettingPropertyInteger("Arrow Stick Min Damage", 0, 32, "0", HintText = "an arrow must deal at least this to lodge in flesh")]
        [SettingPropertyGroup("Flesh and wind")]
        public int ArrowStickMinDamage { get; set; } = 8;

        [SettingPropertyInteger("Javelin Stick Min Damage", 0, 320, "0", HintText = "a thrown javelin, axe or knife stays in a man only when it kills him or deals at least this much - anything less bounces off")]
        [SettingPropertyGroup("Flesh and wind")]
        public int JavelinStickMinDamage { get; set; } = 80;

        [SettingPropertyBool("Walk Key Enabled", HintText = "hold Left Ctrl on foot to walk instead of run - the wind comes back as you stroll")]
        [SettingPropertyGroup("Flesh and wind")]
        public bool WalkKeyEnabled { get; set; } = true;

        [SettingPropertyFloatingInteger("Walk Speed Share", 0.00f, 1.68f, "0.00", HintText = "walking pace as a share of full speed")]
        [SettingPropertyGroup("Flesh and wind")]
        public float WalkSpeedShare { get; set; } = 0.42f;

        [SettingPropertyBool("Horse Death Permanent", HintText = "your mount killed in a real battle is truly gone - the slot empties, only the harness comes off the corpse")]
        [SettingPropertyGroup("Flesh and wind")]
        public bool HorseDeathPermanent { get; set; } = true;

        [SettingPropertyBool("Thrown Wobble Enabled", HintText = "javelins are not sniper rifles - thrown weapons scatter properly, worse still from the saddle")]
        [SettingPropertyGroup("Flesh and wind")]
        public bool ThrownWobbleEnabled { get; set; } = true;

        [SettingPropertyFloatingInteger("Thrown Inaccuracy Factor", 0.00f, 10.00f, "0.00", HintText = "spread multiplier for every thrown weapon (vanilla javelins fly absurdly true)")]
        [SettingPropertyGroup("Flesh and wind")]
        public float ThrownInaccuracyFactor { get; set; } = 2.5f;

        [SettingPropertyFloatingInteger("Thrown Mounted Inaccuracy Factor", 0.00f, 8.00f, "0.00", HintText = "extra spread on top when hurling from horseback - a moving horse is no throwing platform")]
        [SettingPropertyGroup("Flesh and wind")]
        public float ThrownMountedInaccuracyFactor { get; set; } = 2f;

        [SettingPropertyBool("Charge Temper Enabled", HintText = "RBM charge damage = horse mass x speed, so even a slow bump crushes men - temper it")]
        [SettingPropertyGroup("Flesh and wind")]
        public bool ChargeTemperEnabled { get; set; } = true;

        [SettingPropertyFloatingInteger("Charge Damage Factor", 0.00f, 2.40f, "0.00", HintText = "multiplier on mounted charge damage")]
        [SettingPropertyGroup("Flesh and wind")]
        public float ChargeDamageFactor { get; set; } = 0.6f;

        [SettingPropertyFloatingInteger("Charge Full Speed", 0.00f, 28.00f, "0.00", HintText = "full charge damage only at this speed (m/s) and above - slower rides pay proportionally less")]
        [SettingPropertyGroup("Flesh and wind")]
        public float ChargeFullSpeed { get; set; } = 7f;

        [SettingPropertyBool("Auto Parry Enabled", HintText = "hold block and your skill does the aiming: if your weapon skill beats the attacker's, the block turns to meet his blow")]
        [SettingPropertyGroup("The master's parry")]
        public bool AutoParryEnabled { get; set; } = true;

        [SettingPropertyBool("Wounded Flee Enabled", HintText = "a man too hurt to fight turns and runs instead of standing there screaming")]
        [SettingPropertyGroup("The master's parry")]
        public bool WoundedFleeEnabled { get; set; } = true;

        [SettingPropertyFloatingInteger("Wounded Flee Percent", 0.00f, 80.00f, "0.00", HintText = "health left (percent) below which an AI fighter breaks and withdraws")]
        [SettingPropertyGroup("The master's parry")]
        public float WoundedFleePercent { get; set; } = 20f;

        [SettingPropertyBool("Wounded Flee Heroes", HintText = "heroes and companions break too (off: captains hold their ground)")]
        [SettingPropertyGroup("The master's parry")]
        public bool WoundedFleeHeroes { get; set; } = false;

        [SettingPropertyBool("Wounded Flee Enemies Only", HintText = "only the enemy breaks (off: every man on the field, both sides)")]
        [SettingPropertyGroup("The master's parry")]
        public bool WoundedFleeEnemiesOnly { get; set; } = false;

        [SettingPropertyBool("Wounded Flee Without Player", HintText = "also in battles the player is not personally fighting")]
        [SettingPropertyGroup("The master's parry")]
        public bool WoundedFleeWithoutPlayer { get; set; } = true;

        [SettingPropertyFloatingInteger("Auto Parry Full Diff", 0.00f, 100.00f, "0.00", HintText = "your lead IS your chance: each point of skill lead gives (100 / this) % odds per swing, certain at this many points (25 = 4% a point: 13 pts -> 52%, 1 pt -> 4%)")]
        [SettingPropertyGroup("The master's parry")]
        public float AutoParryFullDiff { get; set; } = 25f;

        [SettingPropertyBool("Auto Parry Two Handed Only", HintText = "the master's parry works only with two-handed blades (off = any melee weapon)")]
        [SettingPropertyGroup("The master's parry")]
        public bool AutoParryTwoHandedOnly { get; set; } = true;

        [SettingPropertyBool("Auto Parry Mirror Sides", HintText = "side swings block mirror-wise (his left = your right) - flip if blocks feel wrong-sided")]
        [SettingPropertyGroup("The master's parry")]
        public bool AutoParryMirrorSides { get; set; } = true;

        [SettingPropertyBool("Cavalry Needs Mounts", HintText = "upgrading a man into a MOUNTED troop takes a mount from the party inventory - yours and the AI's alike, one horse per man, gone on upgrade")]
        [SettingPropertyGroup("A knight needs a horse")]
        public bool CavalryNeedsMounts { get; set; } = true;

        [SettingPropertyInteger("War Horse From Tier", 0, 16, "0", HintText = "from this tier the upgrade demands a proper WAR horse")]
        [SettingPropertyGroup("A knight needs a horse")]
        public int WarHorseFromTier { get; set; } = 4;

        [SettingPropertyInteger("Noble Horse From Tier", 0, 24, "0", HintText = "from this tier nothing but a noble steed will do")]
        [SettingPropertyGroup("A knight needs a horse")]
        public int NobleHorseFromTier { get; set; } = 6;

        [SettingPropertyBool("Ai Buys Mounts", HintText = "lords restock their stables when they ride into a settlement - the AI keeps its cavalry instead of slowly losing it")]
        [SettingPropertyGroup("A knight needs a horse")]
        public bool AiBuysMounts { get; set; } = true;

        [SettingPropertyInteger("Ai Mount Spare Buffer", 0, 10, "0", HintText = "a few head over the count actually waiting to be raised to horse - for losses on the road")]
        [SettingPropertyGroup("A knight needs a horse")]
        public int AiMountSpareBuffer { get; set; } = 2;

        [SettingPropertyFloatingInteger("Ai Mount Purse Share", 0.00f, 1.00f, "0.00", HintText = "he never spends more than this share of his purse on horses in one visit")]
        [SettingPropertyGroup("A knight needs a horse")]
        public float AiMountPurseShare { get; set; } = 0.15f;

        [SettingPropertyInteger("Ai Mount Max Per Visit", 0, 40, "0", HintText = "and never buys more than this many head at one stop - a stable is topped up, not a herd bought")]
        [SettingPropertyGroup("A knight needs a horse")]
        public int AiMountMaxPerVisit { get; set; } = 10;

        [SettingPropertyFloatingInteger("Ai Mount Buy Cooldown Days", 0.00f, 16.00f, "0.00", HintText = "days before that same party restocks again: without a pause the AI resold the horses as ordinary goods and bought them back, pumping millions through the market")]
        [SettingPropertyGroup("A knight needs a horse")]
        public float AiMountBuyCooldownDays { get; set; } = 4f;

        [SettingPropertyBool("Ai Mount Breeder Fallback", HintText = "market empty? he orders from the local breeder instead of riding away horseless")]
        [SettingPropertyGroup("A knight needs a horse")]
        public bool AiMountBreederFallback { get; set; } = true;

        [SettingPropertyFloatingInteger("Ai Mount Breeder Markup", 0.00f, 5.20f, "0.00", HintText = "the breeder charges this much over the plain worth for the trouble")]
        [SettingPropertyGroup("A knight needs a horse")]
        public float AiMountBreederMarkup { get; set; } = 1.3f;

        [SettingPropertyBool("Horses At Market Price", HintText = "a horse costs what the local town market asks for it: a lord ordering from the village breeders pays them the town's market price for that horse instead of a flat markup over its worth, and - only with Recruits Own Horse off - a mounted recruit is charged his own horse at that market price instead of the game's flat 150 or 500 (with Historical Recruit Cost on) (off = as before)")]
        [SettingPropertyGroup("A knight needs a horse")]
        public bool HorsesAtMarketPrice { get; set; } = true;

        [SettingPropertyBool("Merc Horse From Shelf", HintText = "only with Recruits Own Horse off: a mounted hireling from a tavern costs a horse only if the town's market has one of the same breed on its shelf (the same horse, or one of the same kind and grade): that horse leaves the shelf with him and the town is paid for it what the hirer is charged for it (its market price, with the hirer's own discounts or surcharges). With no such horse he rides in on his own and costs only his days of pay; if more men are hired than horses stand on the shelf, what was charged for the missing horses goes back to the purse. Needs Historical Recruit Cost and Horses At Market Price (off = the market price of his horse is charged and paid to the town though no horse leaves the shelf)")]
        [SettingPropertyGroup("A knight needs a horse")]
        public bool MercHorseFromShelf { get; set; } = true;

        [SettingPropertyBool("Recruits Own Horse", HintText = "a mounted recruit - a hireling from a tavern or a volunteer from a notable, yours and the AI's - rides in on his own horse: taking him on costs his prest money (days of his pay) and nothing for the horse, and no horse leaves the town's shelf; a man who keeps a horse asks for more pay instead (Mounted Wage Premium). Needs Historical Recruit Cost (off = the horse is charged at recruitment as before: Horses At Market Price, Merc Horse From Shelf)")]
        [SettingPropertyGroup("A knight needs a horse")]
        public bool RecruitsOwnHorse { get; set; } = true;

        [SettingPropertyBool("Mounted Wage Premium", HintText = "a soldier who keeps his own horse wants more pay: every mounted man in a lord's party (yours too) and in a garrison draws the wage of his tier times Mounted Wage Factor - in Edward III's armies of 1346 a mounted archer had 6 pence a day, an archer on foot 3. The pay goes the usual way (to the men's purses, a garrison's to its town) and his prest money follows his pay; caravan guards are paid as before (off = a rider costs what a footman of his tier costs)")]
        [SettingPropertyGroup("A knight needs a horse")]
        public bool MountedWagePremium { get; set; } = true;

        [SettingPropertyFloatingInteger("Mounted Wage Factor", 0.00f, 6.00f, "0.00", HintText = "how much more a mounted soldier is paid than a footman of the same tier: 1.5 puts a tier 4 rider at 12 pence a day like a man-at-arms and a tier 6 knight at 26 like a knight's 24 (2.0 = the 1346 ratio of mounted to foot archer, 1.0 = no premium)")]
        [SettingPropertyGroup("A knight needs a horse")]
        public float MountedWageFactor { get; set; } = 1.5f;

        [SettingPropertyInteger("Ai Mount Market Share Percent", 0, 100, "0", HintText = "a lord may take at most this share of the horses on a town's shelf in one visit - the rest he orders from the breeder, so markets are not stripped bare (Jeff 15.09: no horses to buy anywhere)")]
        [SettingPropertyGroup("A knight needs a horse")]
        public int AiMountMarketSharePercent { get; set; } = 25;

        [SettingPropertyInteger("Ai Mount Shelf Floor", 0, 16, "0", HintText = "and never buys the last few: this many head always stay on the shelf for other buyers")]
        [SettingPropertyGroup("A knight needs a horse")]
        public int AiMountShelfFloor { get; set; } = 4;

        [SettingPropertyBool("Long Year Enabled", HintText = "stretch the year so the world stops racing: children grow, lords age and seasons turn at a pace a long campaign can live with")]
        [SettingPropertyGroup("The turning year")]
        public bool LongYearEnabled { get; set; } = true;

        [SettingPropertyInteger("Weeks Per Season", 0, 52, "0", HintText = "weeks in a season (vanilla 3 = an 84-day year; 13 = a 364-day year, one day is one day). SET IT BEFORE STARTING A CAMPAIGN and leave it alone afterwards")]
        [SettingPropertyGroup("The turning year")]
        public int WeeksPerSeason { get; set; } = 13;

        [SettingPropertyBool("March Pace Enabled", HintText = "a column moves at the pace of its slowest man: any soldier or prisoner on foot holds the whole party to walking speed")]
        [SettingPropertyGroup("The marching column")]
        public bool MarchPaceEnabled { get; set; } = true;

        [SettingPropertyInteger("World Pace Percent", 0, 200, "0", HintText = "base map speed of EVERY party - 50% matches the doubled year: Winterfell to King's Landing takes a lore-true month of the 168-day calendar")]
        [SettingPropertyGroup("The marching column")]
        public int WorldPacePercent { get; set; } = 50;

        [SettingPropertyBool("Terrain Ease Enabled", HintText = "replace the game's percentage terrain and night penalties with the flat map-speed penalties below (the vanilla share is shown undone in the tooltip, then ours applied)")]
        [SettingPropertyGroup("The marching column")]
        public bool TerrainEaseEnabled { get; set; } = true;

        [SettingPropertyFloatingInteger("Forest Speed Penalty", 0.00f, 1.00f, "0.00", HintText = "flat speed lost in forest (vanilla: 30% of your speed)")]
        [SettingPropertyGroup("The marching column")]
        public float ForestSpeedPenalty { get; set; } = 0.10f;

        [SettingPropertyFloatingInteger("Desert Speed Penalty", 0.00f, 1.00f, "0.00", HintText = "flat speed lost in desert and dunes (vanilla: 10%)")]
        [SettingPropertyGroup("The marching column")]
        public float DesertSpeedPenalty { get; set; } = 0.20f;

        [SettingPropertyFloatingInteger("Snow Speed Penalty", 0.00f, 1.00f, "0.00", HintText = "flat speed lost in snowfall or blizzard (vanilla: 10%)")]
        [SettingPropertyGroup("The marching column")]
        public float SnowSpeedPenalty { get; set; } = 0.20f;

        [SettingPropertyFloatingInteger("Swamp Speed Penalty", 0.00f, 1.20f, "0.00", HintText = "flat speed lost crossing swamp and marshland (vanilla: nothing at all)")]
        [SettingPropertyGroup("The marching column")]
        public float SwampSpeedPenalty { get; set; } = 0.30f;

        [SettingPropertyFloatingInteger("Ford Speed Penalty", 0.00f, 2.00f, "0.00", HintText = "flat speed lost fording rivers and crossing bridges (vanilla: 30%)")]
        [SettingPropertyGroup("The marching column")]
        public float FordSpeedPenalty { get; set; } = 0.5f;

        [SettingPropertyFloatingInteger("Night Speed Penalty", 0.00f, 2.00f, "0.00", HintText = "flat speed lost at night on land (vanilla: 25%)")]
        [SettingPropertyGroup("The marching column")]
        public float NightSpeedPenalty { get; set; } = 0.5f;

        [SettingPropertyBool("Ammo Tracer Enabled", HintText = "diagnostic: log every change in the troop armoury's stock of arrows, bolts and harnesses, with time and place")]
        [SettingPropertyGroup("The marching column")]
        public bool AmmoTracerEnabled { get; set; } = true;

        [SettingPropertyBool("Plague Watch Enabled", HintText = "a daily word when YOU are the one who is ill - the disease mod tells the player nothing outside a town hospital")]
        [SettingPropertyGroup("The marching column")]
        public bool PlagueWatchEnabled { get; set; } = true;

        [SettingPropertyBool("Influence Watch Enabled", HintText = "diagnostic: log the full daily breakdown of your clan influence - the game itself never shows it anywhere")]
        [SettingPropertyGroup("The marching column")]
        public bool InfluenceWatchEnabled { get; set; } = true;

        [SettingPropertyBool("Speed Audit Enabled", HintText = "once a day the full speed breakdown of your party is written to Armoury.log (before the marching-column cap and sleep debt)")]
        [SettingPropertyGroup("The marching column")]
        public bool SpeedAuditEnabled { get; set; } = true;

        [SettingPropertyBool("World Measure Log", HintText = "once a day Armoury.log measures the world: how many km a day the AI lords' hosts really march (median and 90th percentile, big hosts, army leaders and all-mounted parties, the world pace in force) and how many days of food every town and castle has left, the North apart - log only, changes nothing in the game")]
        [SettingPropertyGroup("The marching column")]
        public bool WorldMeasureLog { get; set; } = true;

        [SettingPropertyInteger("Siege Pace Percent", 0, 200, "0", HintText = "siege engine construction speed - 50% makes sieges last twice as long, so starving a fortress out matters again")]
        [SettingPropertyGroup("The marching column")]
        public int SiegePacePercent { get; set; } = 50;

        [SettingPropertyBool("Siege Sickness Enabled", HintText = "camp fever: long sieges breed dysentery - the sick go down as wounded, some die; medicine is the shield")]
        [SettingPropertyGroup("The marching column")]
        public bool SiegeSicknessEnabled { get; set; } = true;

        [SettingPropertyInteger("Siege Sickness Incubation Days", 0, 36, "0", HintText = "clean-camp grace period before the fever wakes")]
        [SettingPropertyGroup("The marching column")]
        public int SiegeSicknessIncubationDays { get; set; } = 9;

        [SettingPropertyFloatingInteger("Siege Sickness Base Percent", 0.00f, 2.40f, "0.00", HintText = "daily share of healthy men falling sick once the fever wakes (before ramp, crowding and medicine)")]
        [SettingPropertyGroup("The marching column")]
        public float SiegeSicknessBasePercent { get; set; } = 0.6f;

        [SettingPropertyInteger("Siege Sickness Ramp Percent", 0, 60, "0", HintText = "the daily rate grows by this much for every day past incubation - time is a weapon")]
        [SettingPropertyGroup("The marching column")]
        public int SiegeSicknessRampPercent { get; set; } = 15;

        [SettingPropertyInteger("Siege Sickness Defender Factor", 0, 160, "0", HintText = "defenders behind walls catch this share of the besiegers' rate; famine doubles it")]
        [SettingPropertyGroup("The marching column")]
        public int SiegeSicknessDefenderFactor { get; set; } = 40;

        [SettingPropertyInteger("Siege Sickness Death Share", 0, 40, "0", HintText = "share of the sick who die instead of joining the wounded (Siege Medic halves this)")]
        [SettingPropertyGroup("The marching column")]
        public int SiegeSicknessDeathShare { get; set; } = 10;

        [SettingPropertyInteger("Siege Sickness Medicine Max", 0, 200, "0", HintText = "ceiling of the surgeon's risk reduction (0.25% per Medicine point up to this cap)")]
        [SettingPropertyGroup("The marching column")]
        public int SiegeSicknessMedicineMax { get; set; } = 50;

        [SettingPropertyBool("Single Winter Source", HintText = "one source of winter: our Winter Bite alone cuts harvests and feeds armies more; Better Economy seasons no longer cut harvests, raise food prices or slow caravans on top of it")]
        [SettingPropertyGroup("The marching column")]
        public bool SingleWinterSource { get; set; } = true;

        [SettingPropertyBool("Winter Bite Enabled", HintText = "winter with teeth: armies eat more, villages yield less, town granaries drain faster - the north bites hardest")]
        [SettingPropertyGroup("The marching column")]
        public bool WinterBiteEnabled { get; set; } = true;

        [SettingPropertyInteger("Winter Party Food Bonus Percent", 0, 200, "0", HintText = "extra food a party consumes in winter (scaled by how far north it stands)")]
        [SettingPropertyGroup("The marching column")]
        public int WinterPartyFoodBonusPercent { get; set; } = 50;

        [SettingPropertyInteger("Winter Village Output Cut Percent", 0, 200, "0", HintText = "village production lost in winter - food prices rise on their own as supply dries up")]
        [SettingPropertyGroup("The marching column")]
        public int WinterVillageOutputCutPercent { get; set; } = 50;

        [SettingPropertyFloatingInteger("Winter Town Appetite Per1000", 0.00f, 2.00f, "0.00", HintText = "extra daily food-stock drain per 1000 town prosperity in winter")]
        [SettingPropertyGroup("The marching column")]
        public float WinterTownAppetitePer1000 { get; set; } = 0.5f;

        [SettingPropertyFloatingInteger("Autumn Stock Multiplier", 0.00f, 8.00f, "0.00", HintText = "in autumn the AI supply cap (BkSupplyDaysCap) is multiplied by this - stock up or starve")]
        [SettingPropertyGroup("The marching column")]
        public float AutumnStockMultiplier { get; set; } = 2f;

        [SettingPropertyInteger("North Gradient Percent", 0, 100, "0", HintText = "how much harder winter bites in the far north (and softer in Dorne)")]
        [SettingPropertyGroup("The marching column")]
        public int NorthGradientPercent { get; set; } = 25;

        [SettingPropertyBool("Scorched Earth Enabled", HintText = "war leaves scars: enemy armies forage villages on the march, and plundered villages heal slowly")]
        [SettingPropertyGroup("The marching column")]
        public bool ScorchedEarthEnabled { get; set; } = true;

        [SettingPropertyInteger("Forage Min Men", 0, 400, "0", HintText = "armies at least this large live off enemy land")]
        [SettingPropertyGroup("The marching column")]
        public int ForageMinMen { get; set; } = 100;

        [SettingPropertyFloatingInteger("Forage Radius", 0.00f, 12.00f, "0.00", HintText = "map range within which a passing army drains a hostile village")]
        [SettingPropertyGroup("The marching column")]
        public float ForageRadius { get; set; } = 3f;

        [SettingPropertyFloatingInteger("Forage Hearth Per Day", 0.00f, 3.20f, "0.00", HintText = "hearths a 500-man army drains per day (scales with army size)")]
        [SettingPropertyGroup("The marching column")]
        public float ForageHearthPerDay { get; set; } = 0.8f;

        [SettingPropertyInteger("Forage Floor", 0, 100, "0", HintText = "marching armies can never drain a village below this - true ruin takes a real raid")]
        [SettingPropertyGroup("The marching column")]
        public int ForageFloor { get; set; } = 25;

        [SettingPropertyInteger("Scar Threshold Hearth", 0, 600, "0", HintText = "below this many hearths a village counts as scarred and heals slowly")]
        [SettingPropertyGroup("The marching column")]
        public int ScarThresholdHearth { get; set; } = 150;

        [SettingPropertyInteger("Scar Regen Percent", 0, 100, "0", HintText = "share of normal hearth growth a scarred village keeps (vanilla springs back at +4/day)")]
        [SettingPropertyGroup("The marching column")]
        public int ScarRegenPercent { get; set; } = 25;

        [SettingPropertyInteger("Refugee Floor Hearth", 0, 160, "0", HintText = "below this the refugees trickle home (+0.5/day flat) - regions never die for good")]
        [SettingPropertyGroup("The marching column")]
        public int RefugeeFloorHearth { get; set; } = 40;

        [SettingPropertyBool("Wages Due Enabled", HintText = "unpaid wages: vanilla already cuts morale - we add desertion, the best-paid men first")]
        [SettingPropertyGroup("The marching column")]
        public bool WagesDueEnabled { get; set; } = true;

        [SettingPropertyInteger("Wages Grace Days", 0, 10, "0", HintText = "days of unpaid wages the men will stomach before walking")]
        [SettingPropertyGroup("The marching column")]
        public int WagesGraceDays { get; set; } = 2;

        [SettingPropertyFloatingInteger("Wages Desert Percent Per Day", 0.00f, 2.00f, "0.00", HintText = "share of the party deserting per day past grace, growing with every unpaid day (AI suffers half)")]
        [SettingPropertyGroup("The marching column")]
        public float WagesDesertPercentPerDay { get; set; } = 0.5f;

        [SettingPropertyInteger("Wages Desert Max Days", 0, 32, "0", HintText = "ceiling on that growth - without it a debt left unpaid for weeks bleeds a tenth of the army every single day")]
        [SettingPropertyGroup("The marching column")]
        public int WagesDesertMaxDays { get; set; } = 8;

        [SettingPropertyBool("War Ledger To Outlaws", HintText = "men who walk off over unpaid wages join the outlaw pool of the region (and the people ledger) instead of vanishing - needs Outlaw Law enabled, without it they still vanish")]
        [SettingPropertyGroup("The marching column")]
        public bool WarLedgerToOutlaws { get; set; } = true;

        [SettingPropertyBool("Sack Scar Enabled", HintText = "a settlement taken by siege loses prosperity and loyalty - conquest is a ruin you must rebuild")]
        [SettingPropertyGroup("The marching column")]
        public bool SackScarEnabled { get; set; } = true;

        [SettingPropertyInteger("Sack Prosperity Cut Percent", 0, 60, "0", HintText = "prosperity lost when a settlement falls to siege")]
        [SettingPropertyGroup("The marching column")]
        public int SackProsperityCutPercent { get; set; } = 15;

        [SettingPropertyInteger("Sack Loyalty Hit", 0, 60, "0", HintText = "loyalty lost when a settlement falls to siege")]
        [SettingPropertyGroup("The marching column")]
        public int SackLoyaltyHit { get; set; } = 15;

        [SettingPropertyBool("March Pace Ai Too", HintText = "the same law binds lords, bandits and patrols (villagers and caravans keep their own pace either way)")]
        [SettingPropertyGroup("The marching column")]
        public bool MarchPaceAiToo { get; set; } = true;

        [SettingPropertyFloatingInteger("March Foot Pace", 0.00f, 16.00f, "0.00", HintText = "map speed cap while anyone walks - footmen without a spare mount, or prisoners on the rope")]
        [SettingPropertyGroup("The marching column")]
        public float MarchFootPace { get; set; } = 4.0f;

        [SettingPropertyFloatingInteger("March Train Pace", 0.00f, 16.80f, "0.00", HintText = "map speed cap for an all-riding party that still drags a baggage train (pack animals, livestock)")]
        [SettingPropertyGroup("The marching column")]
        public float MarchTrainPace { get; set; } = 4.2f;

        [SettingPropertyFloatingInteger("March Foot Rider Pace", 0.00f, 20.00f, "0.00", HintText = "map speed cap when footmen ride spare horses - a man in the saddle is not a horseman born")]
        [SettingPropertyGroup("The marching column")]
        public float MarchFootRiderPace { get; set; } = 5.0f;

        [SettingPropertyFloatingInteger("March Rider Pace", 0.00f, 26.00f, "0.00", HintText = "map speed cap for a clean column of riders - every man horsed, no train")]
        [SettingPropertyGroup("The marching column")]
        public float MarchRiderPace { get; set; } = 6.5f;

        [SettingPropertyFloatingInteger("March Pack Allowance", 0.00f, 1.00f, "0.00", HintText = "this many pack animals PER MAN count as field supply, not a train (0.25 = a mule per four men rides free)")]
        [SettingPropertyGroup("The marching column")]
        public float MarchPackAllowance { get; set; } = 0.25f;

        [SettingPropertyBool("Material Law Enabled", HintText = "charcoal and iron bars get honest prices and honest smelting: a load of wood gives a few sacks of charcoal, a bloomery eats charcoal by the sackful, each finer grade of steel loses metal")]
        [SettingPropertyGroup("Smithing materials")]
        public bool MaterialLawEnabled { get; set; } = true;

        [SettingPropertyBool("Real Refining Enabled", HintText = "refining recipes follow the bloomery: 1 wood -> 4 charcoal, 1 ore + charcoal -> 3 crude iron, 5 bars + charcoal -> 4 bars of the next grade (a fifth of the metal lost) (perks still gate the steels)")]
        [SettingPropertyGroup("Smithing materials")]
        public bool RealRefiningEnabled { get; set; } = true;

        [SettingPropertyInteger("Bloomery Charcoal Per Ore", 0, 80, "0", HintText = "charcoal (0.5 kg each) a bloomery burns for one load of ore (10 kg) - about a kilo of charcoal per kilo of ore")]
        [SettingPropertyGroup("Smithing materials")]
        public int BloomeryCharcoalPerOre { get; set; } = 20;

        [SettingPropertyInteger("Charcoal Value", 0, 36, "0", HintText = "worth of one charcoal (0.5 kg): a quarter of a load of wood plus the burner's work")]
        [SettingPropertyGroup("Smithing materials")]
        public int CharcoalValue { get; set; } = 9;

        [SettingPropertyInteger("Crude Iron Value", 0, 348, "0", HintText = "worth of one bar of crude iron (0.5 kg), costed from ore and charcoal")]
        [SettingPropertyGroup("Smithing materials")]
        public int CrudeIronValue { get; set; } = 87;

        [SettingPropertyInteger("Wrought Iron Value", 0, 472, "0", HintText = "worth of one bar of wrought iron (0.5 kg)")]
        [SettingPropertyGroup("Smithing materials")]
        public int WroughtIronValue { get; set; } = 118;

        [SettingPropertyInteger("Iron Value", 0, 628, "0", HintText = "worth of one bar of iron (0.5 kg)")]
        [SettingPropertyGroup("Smithing materials")]
        public int IronValue { get; set; } = 157;

        [SettingPropertyInteger("Steel Value", 0, 840, "0", HintText = "worth of one bar of steel (0.5 kg)")]
        [SettingPropertyGroup("Smithing materials")]
        public int SteelValue { get; set; } = 210;

        [SettingPropertyInteger("Fine Steel Value", 0, 1124, "0", HintText = "worth of one bar of fine steel (0.5 kg)")]
        [SettingPropertyGroup("Smithing materials")]
        public int FineSteelValue { get; set; } = 281;

        [SettingPropertyInteger("Valyrian Steel Value", 0, 4000, "0", HintText = "worth of one bar of Valyrian steel (0.5 kg)")]
        [SettingPropertyGroup("Smithing materials")]
        public int ValyrianSteelValue { get; set; } = 1000;

        [SettingPropertyBool("Minerals Counted Once", HintText = "Banner Kings lists the mineral of a mining village twice and so credited it twice a day (iron ore, salt, clay, silver); this strikes the second entry and counts the first one twice instead - the village digs exactly as much as before, but mines, village storehouses and village carts now all reckon with the same true output")]
        [SettingPropertyGroup("Smithing materials")]
        public bool MineralsCountedOnce { get; set; } = true;

        [SettingPropertyFloatingInteger("Mine Output Multiplier", 0.00f, 12.00f, "0.00", HintText = "iron mines dig this many times the old output - the workshops of the realm were starving for ore")]
        [SettingPropertyGroup("Smithing materials")]
        public float MineOutputMultiplier { get; set; } = 3f;

        [SettingPropertyFloatingInteger("Lumber Output Multiplier", 0.00f, 12.00f, "0.00", HintText = "woodcutters fell this many times the old output - charcoal burners need wood by the cartload (a forge burns ~5 loads of wood per load of ore)")]
        [SettingPropertyGroup("Smithing materials")]
        public float LumberOutputMultiplier { get; set; } = 3f;

        [SettingPropertyBool("No Free Timber And Tools", HintText = "no timber or tools out of thin air: Realistic Bannerlord tops every settlement up each day from nothing - a town to 30 loads of timber and 15 tools, a castle to 15 and 8, a village or hideout to 10 and 4; this stops it, so timber and tools come only from woodcutters, smithies, village carts, caravans and the fence (off = Realistic Bannerlord's daily top-up as before; the day's log line shows either way how much it is or would be)")]
        [SettingPropertyGroup("Smithing materials")]
        public bool NoFreeTimberAndTools { get; set; } = true;

        [SettingPropertyFloatingInteger("Village Woodlot Loads", 0.00f, 10.00f, "0.00", HintText = "every village that is not a woodcutters' village fells this many loads (100 kg) of timber a day in its own woods and sends them to market with its carts - firewood and building timber came to a town from the woods of all the villages around it, not from a few woodcutters alone; the village storehouse holds five days of it like any other produce; it stands in for Realistic Bannerlord's free timber, so it works only while No Free Timber And Tools is on (0 = off)")]
        [SettingPropertyGroup("Smithing materials")]
        public float VillageWoodlotLoads { get; set; } = 2.5f;

        [SettingPropertyBool("Smelt Cap To Craft Cost", HintText = "melting a piece down never gives back more metal than a share of what forging it costs - no metal out of thin air")]
        [SettingPropertyGroup("Smithing materials")]
        public bool SmeltCapToCraftCost { get; set; } = true;

        [SettingPropertyBool("Start Stock In Loads", HintText = "new campaign only: the ore and timber the world starts with are counted by the load too - the game hands out the starting stock before the 100 kg load comes into force, so every storehouse, stall and pack train held ten times the intended weight and choked village storehouses stopped all work; done once, at the first launch of a new campaign (saves older than a day are left as they are)")]
        [SettingPropertyGroup("Smithing materials")]
        public bool StartStockInLoads { get; set; } = true;

        [SettingPropertyBool("Arms Cost Pricing Enabled", HintText = "every piece of arms and armour is priced from what it costs to make - its weight, its metal, its leather and cloth, the days at the anvil and how well it protects; unique pieces keep the price of their fame")]
        [SettingPropertyGroup("Arms pricing")]
        public bool ArmsCostPricingEnabled { get; set; } = true;

        [SettingPropertyFloatingInteger("Arms Price Band", 0.00f, 8.00f, "0.00", HintText = "how far the market price may stray from the cost of making it (1 = price is the cost, 2 = between half and double, higher = closer to the old prices)")]
        [SettingPropertyGroup("Arms pricing")]
        public float ArmsPriceBand { get; set; } = 2f;

        [SettingPropertyFloatingInteger("Smith Day Wage", 0.00f, 40.00f, "0.00", HintText = "what a day of a master smith and his helper costs, in the price of a piece")]
        [SettingPropertyGroup("Arms pricing")]
        public float SmithDayWage { get; set; } = 10f;

        [SettingPropertyFloatingInteger("Smith Profit Percent", 0.00f, 100.00f, "0.00", HintText = "the workshop's profit on top of materials and labour")]
        [SettingPropertyGroup("Arms pricing")]
        public float SmithProfitPercent { get; set; } = 25f;

        [SettingPropertyBool("Material Index Enabled", HintText = "the market prices by the cost of making it AGAIN: dear ore and wood make armour dear, even the pieces already on the stall")]
        [SettingPropertyGroup("Arms pricing")]
        public bool MaterialIndexEnabled { get; set; } = true;

        [SettingPropertyFloatingInteger("Material Index Inertia", 0.00f, 1.00f, "0.00", HintText = "share of the gap to today's material prices the market closes each day - prices drift, they do not jump")]
        [SettingPropertyGroup("Arms pricing")]
        public float MaterialIndexInertia { get; set; } = 0.25f;

        [SettingPropertyFloatingInteger("Material Ratio Min", 0.00f, 2.00f, "0.00", HintText = "cheapest a raw material (ore, wood, leather, linen) counts in one town against its usual price - below that it is shipped out (Jeff: 0.5)")]
        [SettingPropertyGroup("Arms pricing")]
        public float MaterialRatioMin { get; set; } = 0.5f;

        [SettingPropertyFloatingInteger("Material Ratio Max", 0.00f, 6.00f, "0.00", HintText = "dearest a raw material counts in one town against its usual price - above that a smith buys it in the next town (Jeff: 1.5)")]
        [SettingPropertyGroup("Arms pricing")]
        public float MaterialRatioMax { get; set; } = 1.5f;

        [SettingPropertyFloatingInteger("Material Index Min", 0.00f, 2.00f, "0.00", HintText = "floor of the material-cost factor on a finished piece")]
        [SettingPropertyGroup("Arms pricing")]
        public float MaterialIndexMin { get; set; } = 0.5f;

        [SettingPropertyFloatingInteger("Material Index Max", 0.00f, 6.00f, "0.00", HintText = "ceiling of the material-cost factor on a finished piece (before it was 5 - a leather garb sold for 20 000)")]
        [SettingPropertyGroup("Arms pricing")]
        public float MaterialIndexMax { get; set; } = 1.5f;

        [SettingPropertyBool("War Expectation Enabled", HintText = "when war is declared, traders on both sides expect armies to buy and raise their asking prices before the first lord arrives")]
        [SettingPropertyGroup("Arms pricing")]
        public bool WarExpectationEnabled { get; set; } = true;

        [SettingPropertyFloatingInteger("War Expectation Base", 0.00f, 2.00f, "0.00", HintText = "extra demand expected at the declaration of war, scaled by the enemy's strength against ours (0.5x to 2x)")]
        [SettingPropertyGroup("Arms pricing")]
        public float WarExpectationBase { get; set; } = 0.5f;

        [SettingPropertyFloatingInteger("War Expectation Days", 0.00f, 60.00f, "0.00", HintText = "days over which the traders' expectation fades unless real purchases bear it out")]
        [SettingPropertyGroup("Arms pricing")]
        public float WarExpectationDays { get; set; } = 15f;

        [SettingPropertyBool("Substitution Enabled", HintText = "when no piece of the better tier is on the stall, buyers settle for the tier below")]
        [SettingPropertyGroup("Arms pricing")]
        public bool SubstitutionEnabled { get; set; } = true;

        [SettingPropertyFloatingInteger("Substitution Share", 0.00f, 1.20f, "0.00", HintText = "share of the missing tier's demand that falls to the tier below")]
        [SettingPropertyGroup("Arms pricing")]
        public float SubstitutionShare { get; set; } = 0.3f;

        [SettingPropertyFloatingInteger("Trade Transport Percent Per100", 0.00f, 20.00f, "0.00", HintText = "hauling costs this % of a piece's worth per 100 leagues - traders carry goods only where the price difference pays for the road")]
        [SettingPropertyGroup("Arms pricing")]
        public float TradeTransportPercentPer100 { get; set; } = 5f;

        [SettingPropertyBool("Ai Buys Gear", HintText = "AI lords buy their soldiers' arms and armour on the market with their own gold - no more free gear from the quartermaster's hat, no more gold for sweeping the baggage into the armoury")]
        [SettingPropertyGroup("Army purchases")]
        public bool AiBuysGear { get; set; } = true;

        [SettingPropertyBool("Garrison Buys Gear", HintText = "a garrison buys the gear its men lack at the market of its own town, paid by the lord of the place - the coin goes to the town")]
        [SettingPropertyGroup("Army purchases")]
        public bool GarrisonBuysGear { get; set; } = true;

        [SettingPropertyBool("Garrison Buys Gear Player", HintText = "your own garrisons buy gear the same way, from your purse")]
        [SettingPropertyGroup("Army purchases")]
        public bool GarrisonBuysGearPlayer { get; set; } = false;

        [SettingPropertyBool("Unique Spoils From Player", HintText = "the custom of war binds you too: whoever takes you captive takes the renowned arms you wear")]
        [SettingPropertyGroup("Army purchases")]
        public bool UniqueSpoilsFromPlayer { get; set; } = true;

        [SettingPropertyInteger("Battle Real Min Side", 0, 200, "0", HintText = "a clash counts as a real battle for the chronicle's averages only if both sides had at least this many men and neither outnumbered the other more than 4 to 1")]
        [SettingPropertyGroup("Army purchases")]
        public int BattleRealMinSide { get; set; } = 50;

        [SettingPropertyInteger("Unique Max Wearers", 0, 12, "0", HintText = "an item worn by more than this many characters is ordinary attire, not a unique (kept out of the chronicle and the spoils of capture)")]
        [SettingPropertyGroup("Army purchases")]
        public int UniqueMaxWearers { get; set; } = 3;

        [SettingPropertyInteger("Battle Chronicle Min Men", 0, 120, "0", HintText = "battles where both sides together had fewer men than this are only counted, not written out in the battle chronicle (yours always are)")]
        [SettingPropertyGroup("Army purchases")]
        public int BattleChronicleMinMen { get; set; } = 30;

        [SettingPropertyBool("Build Diary Enabled", HintText = "write a daily diary of building works in every town and castle (progress, daily construction power, days left, stalled works) - log only")]
        [SettingPropertyGroup("Army purchases")]
        public bool BuildDiaryEnabled { get; set; } = true;

        [SettingPropertyBool("Finance Ledger Enabled", HintText = "write a daily ledger of every kingdom: treasury and its change, the king's purse and daily balance, clan purses, poor clans and clans losing money, troops - log only")]
        [SettingPropertyGroup("Army purchases")]
        public bool FinanceLedgerEnabled { get; set; } = true;

        [SettingPropertyInteger("Finance Ledger Poor", 0, 4000, "0", HintText = "a clan with less gold than this counts as poor in the ledger")]
        [SettingPropertyGroup("Army purchases")]
        public int FinanceLedgerPoor { get; set; } = 1000;

        [SettingPropertyInteger("Finance Ledger Poorest", 0, 20, "0", HintText = "how many of the poorest clans of each kingdom the ledger lists")]
        [SettingPropertyGroup("Army purchases")]
        public int FinanceLedgerPoorest { get; set; } = 5;

        [SettingPropertyBool("Goods Ledger Enabled", HintText = "write a daily ledger of every trade good and farm animal - how much villages, workshops and hidden craftsmen made, how much townsfolk, workshops, armies, builders and Banner Kings supplies used up, what carts and caravans carried, who holds the stock and what is left unexplained; each line balances yesterday's stock + made - used = today's stock - log only, changes nothing in the game")]
        [SettingPropertyGroup("Army purchases")]
        public bool GoodsLedgerEnabled { get; set; } = true;

        [SettingPropertyBool("Paid Construction", HintText = "buildings rise only as fast as their owner pays: wages and carting go to the town or castle purse, materials are bought off the market")]
        [SettingPropertyGroup("Army purchases")]
        public bool PaidConstruction { get; set; } = true;

        [SettingPropertyBool("Paid Construction Player", HintText = "your own fiefs too: their works take the same share of your daily income")]
        [SettingPropertyGroup("Army purchases")]
        public bool PaidConstructionPlayer { get; set; } = true;

        [SettingPropertyFloatingInteger("Build Income Share", 0.00f, 1.00f, "0.00", HintText = "share of a lord's daily income (fiefs and rents) spent each day on building works, split over his fiefs with works; in war only military works (walls, towers, barracks)")]
        [SettingPropertyGroup("Army purchases")]
        public float BuildIncomeShare { get; set; } = 0.10f;

        [SettingPropertyFloatingInteger("Build Material Share", 0.00f, 1.00f, "0.00", HintText = "of that money, this share buys materials (limestone, timber, clay, tools, marble) at the market; the rest pays masons, labourers and carters")]
        [SettingPropertyGroup("Army purchases")]
        public float BuildMaterialShare { get; set; } = 0.25f;

        [SettingPropertyFloatingInteger("Build Pence Per Point Military", 0.00f, 192.00f, "0.00", HintText = "pence per construction point of walls, towers and other military works")]
        [SettingPropertyGroup("Army purchases")]
        public float BuildPencePerPointMilitary { get; set; } = 48f;

        [SettingPropertyFloatingInteger("Build Pence Per Point Civil", 0.00f, 96.00f, "0.00", HintText = "pence per construction point of civil buildings")]
        [SettingPropertyGroup("Army purchases")]
        public float BuildPencePerPointCivil { get; set; } = 24f;

        [SettingPropertyBool("Build Wages By Town", HintText = "the masons and labourers are paid the day wage of the market town they come from: a penny of wages buys less work in a rich town and more in a poor one (materials at the market price either way; off = a penny of wages is a penny of work everywhere)")]
        [SettingPropertyGroup("Army purchases")]
        public bool BuildWagesByTown { get; set; } = true;

        [SettingPropertyBool("Ai Recruits Bring Kit", HintText = "a fresh recruit still arrives with his own kit (levies came armed); turn off and lords must buy for every new man")]
        [SettingPropertyGroup("Army purchases")]
        public bool AiRecruitsBringKit { get; set; } = true;

        [SettingPropertyBool("Kit From Notable", HintText = "an AI lord's new recruits bring only the kit their notable actually bought for them (tier 1 men bring their own belongings) - no full kit from thin air")]
        [SettingPropertyGroup("Army purchases")]
        public bool KitFromNotable { get; set; } = true;

        [SettingPropertyFloatingInteger("Ai Gear Budget Percent", 0.00f, 100.00f, "0.00", HintText = "share of a lord's gold (above the reserve) he is willing to spend on gear in one visit to a town")]
        [SettingPropertyGroup("Army purchases")]
        public float AiGearBudgetPercent { get; set; } = 25f;

        [SettingPropertyInteger("Ai Gear Gold Reserve", 0, 8000, "0", HintText = "gold a lord always keeps back - wages come first")]
        [SettingPropertyGroup("Army purchases")]
        public int AiGearGoldReserve { get; set; } = 2000;

        [SettingPropertyInteger("Ai Gear Max Pieces Per Visit", 0, 240, "0", HintText = "most pieces a lord buys in one visit")]
        [SettingPropertyGroup("Army purchases")]
        public int AiGearMaxPiecesPerVisit { get; set; } = 60;

        [SettingPropertyInteger("Ai Gear Log Per Day", 0, 60, "0", HintText = "how many lords' purchases are written to the log each day (the daily total is always written)")]
        [SettingPropertyGroup("Army purchases")]
        public int AiGearLogPerDay { get; set; } = 15;

        [SettingPropertyBool("Workshop Law Enabled", HintText = "town workshops that make arms and armour run as real businesses: they buy ore, wood, leather and linen on the market by the true weight of each piece, pay their workers, sell to the market - and make only what turns a profit")]
        [SettingPropertyGroup("Workshops")]
        public bool WorkshopLawEnabled { get; set; } = true;

        [SettingPropertyFloatingInteger("Workshop Workers Artisans", 0.00f, 24.00f, "0.00", HintText = "man-days of work the town's artisans put in each day")]
        [SettingPropertyGroup("Workshops")]
        public float WorkshopWorkersArtisans { get; set; } = 6f;

        [SettingPropertyFloatingInteger("Workshop Workers", 0.00f, 24.00f, "0.00", HintText = "man-days of work a notable's smithy or wood workshop puts in each day (a master with journeymen and apprentices)")]
        [SettingPropertyGroup("Workshops")]
        public float WorkshopWorkers { get; set; } = 6f;

        [SettingPropertyFloatingInteger("Workshop Prosperity Per Hand", 0.00f, 680.00f, "0.00", HintText = "the town's own craftsmen: one man-day of arms work each day for this much prosperity (a town of 4800 keeps about 28 hands busy) - history had 10-20x more (Paris 1292, Milan), raised step by step as ore allows")]
        [SettingPropertyGroup("Workshops")]
        public float WorkshopProsperityPerHand { get; set; } = 170f;

        [SettingPropertyFloatingInteger("Workshop Artisans Min", 0.00f, 24.00f, "0.00", HintText = "fewest man-days a day the craftsmen of even a poor town put in")]
        [SettingPropertyGroup("Workshops")]
        public float WorkshopArtisansMin { get; set; } = 6f;

        [SettingPropertyFloatingInteger("Workshop Artisans Max", 0.00f, 240.00f, "0.00", HintText = "most man-days a day the craftsmen of the richest town put in")]
        [SettingPropertyGroup("Workshops")]
        public float WorkshopArtisansMax { get; set; } = 60f;

        [SettingPropertyInteger("Artisan Tan Weave Per Cycle", 0, 20, "0", HintText = "the town's own tanners and weavers turn this many loads of raw hides into leather, or flax into linen, each working cycle - from the town market, nothing from thin air (one for one; not used while Town Crafts Enabled is on)")]
        [SettingPropertyGroup("Workshops")]
        public int ArtisanTanWeavePerCycle { get; set; } = 5;

        [SettingPropertyBool("Town Crafts Enabled", HintText = "the town's own fullers, weavers and tanners, in every town: wool into woollen cloth (felt), flax into linen, raw hides into leather, taken from their own town's stalls whenever the cloth or leather fetches enough to pay for its material, the work and a master's profit (Smith Profit Percent) at today's prices - one piece at a time, each priced anew, the best paying trade first. Material by the worth of the goods - 60% of the cloth's worth, never less than the cloth weighs: about 3 sacks of wool for 2 bolts of cloth, 3 of flax for 1 of linen, 12 hides for 5 leathers; the work is 20% of the worth at a craftsman's day wage (cloth 13 man-days, linen 7, leather 3), paid as dear as the town is prosperous (Workshop Wage By Tier, Town Wage Ref Prosperity). No coin changes hands - from stall to stall of the same town. What they work up counts in the town's daily use, so wool and flax are dear where they are worked and caravans bring them there. Replaces the one-for-one tanning and weaving (Artisan Tan Weave Per Cycle); needs Historical Prices (off = one for one as before)")]
        [SettingPropertyGroup("Workshops")]
        public bool TownCraftsEnabled { get; set; } = true;

        [SettingPropertyFloatingInteger("Town Craft Hands Per Arms Hand", 0.00f, 8.00f, "0.00", HintText = "hands of a town's cloth and leather trades for each hand of its arms craftsmen (Workshop Prosperity Per Hand), so they grow with the town's prosperity - weaving, fulling and tanning kept about twice as many people as the arms guilds (Paris 1292, Ghent 1356, estimate); 0 = town crafts off, the one-for-one tanning and weaving as before")]
        [SettingPropertyGroup("Workshops")]
        public float TownCraftHandsPerArmsHand { get; set; } = 2f;

        [SettingPropertyFloatingInteger("Workshop Sell Share", 0.00f, 3.60f, "0.00", HintText = "a craftsman sells his wares at the market price buyers pay, less this merchant's cut (0.9 = he keeps 90%); with the maker's profit of 25% built into worth, at a normal price he earns 1.125x his cost")]
        [SettingPropertyGroup("Workshops")]
        public float WorkshopSellShare { get; set; } = 0.9f;

        [SettingPropertyFloatingInteger("Guild Share Tailor", 0.00f, 1.20f, "0.00", HintText = "share of a town's craftsmen who are tailors and doublet-makers (Paris tax roll 1292)")]
        [SettingPropertyGroup("Workshops")]
        public float GuildShareTailor { get; set; } = 0.30f;

        [SettingPropertyFloatingInteger("Guild Share Armourer", 0.00f, 1.00f, "0.00", HintText = "share who are armourers and mail-makers")]
        [SettingPropertyGroup("Workshops")]
        public float GuildShareArmourer { get; set; } = 0.20f;

        [SettingPropertyFloatingInteger("Guild Share Weaponsmith", 0.00f, 1.00f, "0.00", HintText = "share who are weaponsmiths, cutlers and spear-makers")]
        [SettingPropertyGroup("Workshops")]
        public float GuildShareWeaponsmith { get; set; } = 0.20f;

        [SettingPropertyFloatingInteger("Guild Share Saddler", 0.00f, 1.00f, "0.00", HintText = "share who are saddlers and harness-makers")]
        [SettingPropertyGroup("Workshops")]
        public float GuildShareSaddler { get; set; } = 0.15f;

        [SettingPropertyFloatingInteger("Guild Share Bowyer", 0.00f, 1.00f, "0.00", HintText = "share who are bowyers and fletchers")]
        [SettingPropertyGroup("Workshops")]
        public float GuildShareBowyer { get; set; } = 0.10f;

        [SettingPropertyFloatingInteger("Guild Share Shieldwright", 0.00f, 1.00f, "0.00", HintText = "share who are shield-makers")]
        [SettingPropertyGroup("Workshops")]
        public float GuildShareShieldwright { get; set; } = 0.05f;

        [SettingPropertyFloatingInteger("Workshop Forge Wood Per Metal Kg", 0.00f, 50.00f, "0.00", HintText = "kilograms of wood (as charcoal) the forge burns for each kilogram of metal worked, on top of the bloomery")]
        [SettingPropertyGroup("Workshops")]
        public float WorkshopForgeWoodPerMetalKg { get; set; } = 12.5f;

        [SettingPropertyFloatingInteger("Workshop Wage Per Day", 0.00f, 12.00f, "0.00", HintText = "wages for one man-day at the forge, paid into the town (3 d - a craftsman's day in historical prices)")]
        [SettingPropertyGroup("Workshops")]
        public float WorkshopWagePerDay { get; set; } = 3f;

        [SettingPropertyBool("Workshop Wage By Tier", HintText = "an arms workshop pays every day of work at the master's day wage for the tier of the piece - the same wage that makes the piece's worth (3 d plain gear up to 10.5 d the finest harness) - and every workshop pays wages and keep as dear as its town is prosperous (off = 3 d a day for every piece, the same in every town)")]
        [SettingPropertyGroup("Workshops")]
        public bool WorkshopWageByTier { get; set; } = true;

        [SettingPropertyFloatingInteger("Workshop Min Profit Percent", 0.00f, 20.00f, "0.00", HintText = "a workshop makes a piece only if the market pays at least this much over materials and wages (a glutted stall - price below about 0.93 of worth - stops it)")]
        [SettingPropertyGroup("Workshops")]
        public float WorkshopMinProfitPercent { get; set; } = 5f;

        [SettingPropertyFloatingInteger("Workshop Crude Kg Per Ore", 0.00f, 6.00f, "0.00", HintText = "kilograms of crude iron a bloomery wins from one load of ore (10 kg); each finer grade costs a fifth more")]
        [SettingPropertyGroup("Workshops")]
        public float WorkshopCrudeKgPerOre { get; set; } = 1.5f;

        [SettingPropertyFloatingInteger("Workshop Wood Per Ore", 0.00f, 20.00f, "0.00", HintText = "loads of wood burnt to charcoal for each load of ore smelted")]
        [SettingPropertyGroup("Workshops")]
        public float WorkshopWoodPerOre { get; set; } = 5f;

        [SettingPropertyInteger("Workshop Candidates", 0, 160, "0", HintText = "how many different pieces a workshop weighs each morning when deciding what pays best to make")]
        [SettingPropertyGroup("Workshops")]
        public int WorkshopCandidates { get; set; } = 40;

        [SettingPropertyBool("Workshop Trade Enabled", HintText = "workshops keep their books in the new coin (needs Historical Prices): the daily keep and the wages go into the town purse instead of vanishing, a failed shop is refitted with its new owner's own coin instead of 10000 from nowhere, the coin in the till stays with the shop when it changes hands, and a workshop is bought and sold for what it earns (off = the game's rules in the old coin: 100 a day into thin air, price by town prosperity)")]
        [SettingPropertyGroup("Workshops")]
        public bool WorkshopTradeEnabled { get; set; } = true;

        [SettingPropertyFloatingInteger("Workshop Trade Upkeep Per Day", 0.00f, 16.00f, "0.00", HintText = "what every workshop pays the town each day whether it works or not - the master's own keep (a craftsman's 3 d) and the rent of the house (about 1 d a day, 30 shillings a year); the game took 100 a day")]
        [SettingPropertyGroup("Workshops")]
        public float WorkshopTradeUpkeepPerDay { get; set; } = 4f;

        [SettingPropertyBool("Workshop Trade Batch Wages", HintText = "journeymen are paid by the batch: a workshop makes a batch of trade goods (bread, beer, wine, leather, linen, pottery, oil, tools, felt, velvet, jewelry) only when the market pays for the materials and for that batch's wages, and the wages go into the town - Workshop Workers man-days a day at Workshop Wage Per Day, shared by its trade lines (off = the game's rule: a batch must clear 200 of the old coin per day of work, which no baker, brewer, tanner or potter can at today's prices, so they stand idle)")]
        [SettingPropertyGroup("Workshops")]
        public bool WorkshopTradeBatchWages { get; set; } = true;

        [SettingPropertyInteger("Workshop Trade Start Capital", 0, 8000, "0", HintText = "working coin of a workshop that makes only trade goods (bakery, brewery, tannery, weavery...): what it starts a new campaign with, what a new owner puts in from his own purse after a failure, and the level above which profit is paid out - a season of wages and a few batches of materials (the game: 10000 out of thin air). Never less than ten batches of its dearest material at the usual price, up to the game's 10000 (a velvet weavery buys raw silk at 1000 a bale, so it keeps 10000). Workshops that also make arms keep the game's 10000 - the materials of one mail barding cost thousands - and workshops in an old save keep their mark until they change trade")]
        [SettingPropertyGroup("Workshops")]
        public int WorkshopTradeStartCapital { get; set; } = 2000;

        [SettingPropertyInteger("Workshop Trade Low Capital", 0, 2000, "0", HintText = "when your own workshop's till holds no more than this, its keep and wages are paid from your purse instead (the game: 5000)")]
        [SettingPropertyGroup("Workshops")]
        public int WorkshopTradeLowCapital { get; set; } = 500;

        [SettingPropertyFloatingInteger("Workshop Trade Equipment Scale", 0.00f, 1.00f, "0.00", HintText = "worth of a workshop's tools and fittings in the new coin as a share of the old price list (bakery 6000 -> 1200, tannery 1000 -> 200): the floor under a workshop's price and the cost of changing what it makes - about what wages and tools fell by between the two coins")]
        [SettingPropertyGroup("Workshops")]
        public float WorkshopTradeEquipmentScale { get; set; } = 0.2f;

        [SettingPropertyFloatingInteger("Workshop Trade Price Years", 0.00f, 12.00f, "0.00", HintText = "a workshop sells for this many years of its profit after the workshop tax its buyer will really pay (a notable pays Banner Kings' tax; you and the lords pay none unless Banner Kings' own workshop model is the game's), never less than its tools and fittings, plus the coin in its till; town rents sold at about ten years' purchase and merchant ventures paid 15-30% a year, but here a workshop is seized without payment whenever its owner's realm goes to war with the town's - so a buyer wants his coin back in three years")]
        [SettingPropertyGroup("Workshops")]
        public float WorkshopTradePriceYears { get; set; } = 3f;

        [SettingPropertyInteger("Workshop Trade Profit Days", 0, 120, "0", HintText = "the profit a price is reckoned from is a running average over about this many days of the workshop's real takings (before that - what today's market prices promise); the shop also remembers its past year: a seller asks by the better of the two, a notable buying from you pays by the worse, so a shop that stood idle for a month is no bargain")]
        [SettingPropertyGroup("Workshops")]
        public int WorkshopTradeProfitDays { get; set; } = 30;

        [SettingPropertyFloatingInteger("Workshop Trade Resale Share", 0.00f, 3.20f, "0.00", HintText = "a notable buying your workshop pays this share of its worth to him (after his own tax) plus the coin in its till - out of his own purse, and no more than he has (0 to 1 - anything above 1 counts as 1)")]
        [SettingPropertyGroup("Workshops")]
        public float WorkshopTradeResaleShare { get; set; } = 0.8f;

        [SettingPropertyBool("Artisan Own Inputs", HintText = "every loaf from its own grain: Banner Kings lets the hidden craftsmen of a town make several pieces from one piece of material - with today's prices about 5 loaves or 9 jugs of beer from one sack of grain, 4 pots from one load of clay, three times the meat and hides from one beast - and the town pays them for every piece. On: that number stays as the work the craftsmen can do that day, but every extra batch takes its own grain, clay or beast from the town market at the town's price, paid from the craftsmen's till, and only when the batch is worth more than its material (the game's own rule for their first batch); where the market has none or it does not pay, the extra pieces are not made and the town gets back what it paid for them. A slaughter yields one beast's meat, hides and wool per batch. Quality, the lines without any material and the arms lines stay as they are (off = Banner Kings as before)")]
        [SettingPropertyGroup("Workshops")]
        public bool ArtisanOwnInputs { get; set; } = true;

        [SettingPropertyBool("Workshop Trade Fair Price", HintText = "the just price of the towns (assize of bread, guild prices): for each batch of trade goods the town pays a workshop at most what the batch cost - its materials at the town's price and its wages - plus the master's profit (Smith Profit Percent, the same margin as in the price of arms); whatever an empty stall would pay above that stays in the town purse. Bakers, weavers and silversmiths earn a craftsman's living instead of a lord's rent, so a workshop costs what such a living is worth. Your own workshops too; arms lines keep their own price (off = a workshop gets the town's full price for every piece)")]
        [SettingPropertyGroup("Workshops")]
        public bool WorkshopTradeFairPrice { get; set; } = true;

        [SettingPropertyBool("Castle Villages Sell In Town", HintText = "villagers of a village held from a castle cart their goods to the nearest town market of their realm instead of the lord's castle - the castle was the lord's storehouse and garrison, never a market (off = they keep hauling to the castle)")]
        [SettingPropertyGroup("The road to market")]
        public bool CastleVillagesSellInTown { get; set; } = true;

        [SettingPropertyFloatingInteger("Market Max Distance", 0.00f, 1000.00f, "0.00", HintText = "the farthest town market, by road, villagers will cart to - about four days on the road, the game's own trade range for a village (three spans between neighbouring towns). Beyond it a castle village keeps selling at its lord's castle (0 = no limit of ours, only the game's own trade range)")]
        [SettingPropertyGroup("The road to market")]
        public float MarketMaxDistance { get; set; } = 250f;

        [SettingPropertyFloatingInteger("Market Cart Factor", 0.00f, 8.00f, "0.00", HintText = "carts instead of pack loads: villagers hauling to a town market carry this many times their usual load - the longer road would otherwise choke the village storehouse (1 = off; above 2 changes little, they take at most three fifths of the store unless they set out on a long road; with Village Cart Whole Store on they take the whole store whatever it weighs)")]
        [SettingPropertyGroup("The road to market")]
        public float MarketCartFactor { get; set; } = 2f;

        [SettingPropertyBool("Market Cart All Villages", HintText = "every village's villagers haul by cart, not only those of villages held from a castle - town villages now take longer roads too, and they already choked their storehouses more often (off = carts for castle villages only)")]
        [SettingPropertyGroup("The road to market")]
        public bool MarketCartAllVillages { get; set; } = true;

        [SettingPropertyBool("Village Carts Best Market", HintText = "villagers take their load to the town within Market Max Distance that pays most for it per day of their journey - valued as that town's stalls would pay for it; their own town always counts, besieged and enemy towns never; a journey longer than the village storehouse can bear (it stops all work once full) earns less per day (off = the village's own town, as before)")]
        [SettingPropertyGroup("The road to market")]
        public bool VillageCartsBestMarket { get; set; } = true;

        [SettingPropertyBool("Village Cart Full Load Far", HintText = "villagers setting out on a road longer than their storehouse can bear take everything their cart can carry, so the village does not stand idle and its goods do not lie waiting (needs Village Carts Best Market)")]
        [SettingPropertyGroup("The road to market")]
        public bool VillageCartFullLoadFar { get; set; } = true;

        [SettingPropertyBool("Village Cart Whole Store", HintText = "villagers setting out for a town market take the whole village storehouse every time - the district hires as many carts as the load needs, so a heavy load (ore, timber) neither stays behind nor slows the train; a journey counts as free only while they are back before the storehouse holds the next load (five days of the village's output, waits in town and at home included) - every day beyond that earns less, and a full storehouse stops all village work, food too. Takes the place of Village Cart Full Load Far (needs Village Carts Best Market; off = the game's three fifths of each stack, a full cart only for a long road)")]
        [SettingPropertyGroup("The road to market")]
        public bool VillageCartWholeStore { get; set; } = true;

        [SettingPropertyBool("Village Cart Road News", HintText = "villagers know what other carts are already hauling to a town and count it as if it were on the stalls, so they do not all drive their loads to the same empty market (needs Village Carts Best Market)")]
        [SettingPropertyGroup("The road to market")]
        public bool VillageCartRoadNews { get; set; } = true;

        [SettingPropertyBool("Village Cart Fair Price", HintText = "a town pays for a cartload piece by piece - each piece at the price its stall gives once the pieces before it lie there - not the whole load at the price of the first piece; what was overpaid goes back from the villagers' purse to the town's. Keep it on while Village Carts Best Market is on - carts sent to empty markets would otherwise take the first piece's price for the whole load out of those towns' purses (off = the whole load at the first piece's price, as the game does)")]
        [SettingPropertyGroup("The road to market")]
        public bool VillageCartFairPrice { get; set; } = true;

        [SettingPropertyBool("Map Road Table Fix", HintText = "the map's road table is completed when a campaign loads: the Realm of Thrones map was reworked after its road table was made, and the new spots by Wickenden, Lord Hewett's Town, Acorn Hall, Griffin's Roost, Pinkmaiden and a hideout had no entry, so every party standing there - in those settlements or on the road beside them - looked cut off from the whole world: Banner Kings refused all its orders, caravans thought every town too far, and lords, caravans and villagers stood there for good. Each spot the road table never saw gets the nearest gate by road, worked out exactly as the game's own map tool does; spots the tool saw and left empty (no road to any settlement) stay as they are. Nothing goes into the save - the table is read anew at every load (off = the table as the map ships it)")]
        [SettingPropertyGroup("The road to market")]
        public bool MapRoadTableFix { get; set; } = true;

        [SettingPropertyBool("Village Cart Leave Town", HintText = "villagers who have sold their load in a town always set off again - Banner Kings refuses a 'go to' order whenever the map's road table has no entry for the spot a party stands on, and the gates of Wickenden, Lord Hewett's Town and Acorn Hall stand on such spots (the Realm of Thrones map was reworked after its road table was made), so carts that drove in there never left and their villages stopped all work; with this on, a village cart standing in a settlement may go wherever the game's own settlement road table knows a road (off = as Banner Kings decides, and Village Cart Town Max Days stands idle too)")]
        [SettingPropertyGroup("The road to market")]
        public bool VillageCartLeaveTown { get; set; } = true;

        [SettingPropertyFloatingInteger("Village Cart Town Max Days", 0.00f, 8.00f, "0.00", HintText = "a village cart still standing in a town this many days after it drove in is sent home with whatever it carries - nothing is made or lost; carts in a besieged town or one under attack wait as before. A sound cart leaves within hours (the game sends it home with one chance in five every hour), so two days catch only a cart that is truly stuck (needs Village Cart Leave Town; 0 = never)")]
        [SettingPropertyGroup("The road to market")]
        public float VillageCartTownMaxDays { get; set; } = 2f;

        [SettingPropertyBool("Island Roads Fix", HintText = "caravans and lords on islands are no longer left standing by a Banner Kings order to a place they cannot reach over land: a party with ships goes to the same place by land and sea; a caravan without ships picks the best town it can reach by land - by Banner Kings' own trade reckoning, else its home or the nearest town by road - and Banner Kings no longer picks towns across the water for such a caravan at all; if its island has no other town, it waits in town as the game's own caravans do when no trip pays; a lord whose Banner Kings feast or estate order cannot be carried out gets his own will back instead of standing frozen in a castle or town. Only orders change - nothing is made or lost (off = as Banner Kings decides)")]
        [SettingPropertyGroup("The road to market")]
        public bool IslandRoadsFix { get; set; } = true;

        [SettingPropertyBool("Village Clog Diagnostics", HintText = "log only, changes nothing in the game: once a day the Armoury log says why village storehouses stand full (one and a half times their size stops all village work) - village types, what lies in them, where each village's villagers are (at home, on the road and for how long, in town, none at all) and how many villages filled up or emptied that day; ten examples every five days (off = no such lines)")]
        [SettingPropertyGroup("The road to market")]
        public bool VillageClogDiagnostics { get; set; } = true;

        [SettingPropertyBool("Caravan Bulk Enabled", HintText = "caravans haul bulk raw goods (iron ore, timber, raw hides, leather, flax, linen, wool) by need: a town short of its own stock buys what it lacks from any passing caravan, and a caravan leaving a town buys only what that town holds to spare (off = Banner Kings' price-driven caravan trade alone)")]
        [SettingPropertyGroup("The road to market")]
        public bool CaravanBulkEnabled { get; set; } = true;

        [SettingPropertyInteger("Caravan Bulk Stock Days", 0, 40, "0", HintText = "a town wants this many days of its own craftsmen's and workshops' use of each bulk good on its stalls - and never less than one large piece of work takes (8 loads of ore, 40 of timber)")]
        [SettingPropertyGroup("The road to market")]
        public int CaravanBulkStockDays { get; set; } = 10;

        [SettingPropertyFloatingInteger("Caravan Bulk Surplus Factor", 0.00f, 8.00f, "0.00", HintText = "a town sells to caravans only what it holds above this many times the stock it wants for itself (2 = it keeps twenty days of use and sells the rest; never counted below 1)")]
        [SettingPropertyGroup("The road to market")]
        public float CaravanBulkSurplusFactor { get; set; } = 2f;

        [SettingPropertyFloatingInteger("Caravan Bulk Capacity Share", 0.00f, 2.00f, "0.00", HintText = "at most this share of a caravan's carrying capacity goes to bulk raw goods, and no single one of them takes more than half of that - the rest stays free for its usual trade (never above 0.8)")]
        [SettingPropertyGroup("The road to market")]
        public float CaravanBulkCapacityShare { get; set; } = 0.5f;

        [SettingPropertyFloatingInteger("Caravan Bulk Transit Cover", 0.00f, 24.00f, "0.00", HintText = "caravans keep buying a bulk good only while all of them together carry less than this many times what the towns of the world lack - above that they stop buying and unload it into any town up to its surplus mark (higher = fewer empty stalls, more cargo idling on the road; 0 = they never buy)")]
        [SettingPropertyGroup("The road to market")]
        public float CaravanBulkTransitCover { get; set; } = 6f;

        [SettingPropertyBool("Caravan Bulk Buy Before Route", HintText = "a caravan buys its bulk raw goods right after its usual purchases and before it picks the next town, so the fresh cargo already counts in that choice (off = it buys on its way out of the gate, when the destination is already set)")]
        [SettingPropertyGroup("The road to market")]
        public bool CaravanBulkBuyBeforeRoute { get; set; } = true;

        [SettingPropertyFloatingInteger("Caravan Bulk Fill Limit", 0.00f, 4.00f, "0.00", HintText = "bulk raw goods bought at a profit may fill a caravan's packs up to this share of its carrying capacity - Banner Kings stops its own buying at 0.8 and leaves the rest empty (0.8 = no more room than before; never below 0.8 or above 1; the player's own caravans always stay at 0.8)")]
        [SettingPropertyGroup("The road to market")]
        public float CaravanBulkFillLimit { get; set; } = 1f;

        [SettingPropertyBool("Levy Enabled", HintText = "volunteers come forward only where there are men to spare: hands the fields do not need, and men who want to leave a poor, burnt or warring land")]
        [SettingPropertyGroup("Iron bank")]
        public bool LevyEnabled { get; set; } = true;

        [SettingPropertyFloatingInteger("Recruit Base Willing", 0.00f, 1.00f, "0.00", HintText = "younger sons and restless lads: the small chance of a volunteer even in a land that needs every hand (against Banner Kings' daily chance)")]
        [SettingPropertyGroup("Iron bank")]
        public float RecruitBaseWilling { get; set; } = 0.05f;

        [SettingPropertyFloatingInteger("Recruit Excess Weight", 0.00f, 4.00f, "0.00", HintText = "how strongly hands without work on the land (Banner Kings workforce surplus) bring volunteers forward")]
        [SettingPropertyGroup("Iron bank")]
        public float RecruitExcessWeight { get; set; } = 1f;

        [SettingPropertyFloatingInteger("Recruit Misery Weight", 0.00f, 1.00f, "0.00", HintText = "how strongly poverty, war, burnt villages and hunger push men to leave and take service")]
        [SettingPropertyGroup("Iron bank")]
        public float RecruitMiseryWeight { get; set; } = 0.2f;

        [SettingPropertyFloatingInteger("Recruit Willing Max", 0.00f, 6.00f, "0.00", HintText = "ceiling on the willingness multiplier")]
        [SettingPropertyGroup("Iron bank")]
        public float RecruitWillingMax { get; set; } = 1.5f;

        [SettingPropertyBool("Recruit Gold To Seller", HintText = "the gold an AI lord pays for a recruit goes to the notable who raised him (tavern hirelings: to the town), as it already does for you - not into thin air")]
        [SettingPropertyGroup("Iron bank")]
        public bool RecruitGoldToSeller { get; set; } = true;

        [SettingPropertyBool("No Free Kit For New Parties", HintText = "a new AI warband no longer gets a full free kit for all its men (Dynamic Troop Equipment) after the campaign has begun - its lord buys gear at market")]
        [SettingPropertyGroup("Iron bank")]
        public bool NoFreeKitForNewParties { get; set; } = true;

        [SettingPropertyBool("Volunteer Kit Enabled", HintText = "a volunteer rises to a better troop only when his notable buys the missing gear for it on the town market (paid to the town, taken off the stall) - no gear from thin air")]
        [SettingPropertyGroup("Iron bank")]
        public bool VolunteerKitEnabled { get; set; } = true;

        [SettingPropertyBool("Volunteer Kit Key Only", HintText = "the volunteer rises once his notable buys the key pieces (body armour, main weapon - bow or crossbow plus one quiver for archers - and horse with harness for riders); helmet, shield, boots and the rest are bought if on the stall, otherwise they become orders for the workshops")]
        [SettingPropertyGroup("Iron bank")]
        public bool VolunteerKitKeyOnly { get; set; } = true;

        [SettingPropertyBool("Cold Start Enabled", HintText = "new campaign only: the world does not start empty - lords and garrisons have the kit their men already wear in their armouries, and town stalls hold the craftsmen's stock")]
        [SettingPropertyGroup("Iron bank")]
        public bool ColdStartEnabled { get; set; } = true;

        [SettingPropertyFloatingInteger("Cold Start Market Days", 0.00f, 56.00f, "0.00", HintText = "new campaign only: how many days of the town craftsmen's work lie on the stalls at the start (mostly common gear of the town's culture)")]
        [SettingPropertyGroup("Iron bank")]
        public float ColdStartMarketDays { get; set; } = 14f;

        [SettingPropertyBool("Historical Prices Enabled", HintText = "the whole world in historical prices (1 coin = 1 medieval penny): arms and armour priced from their real making cost, smithing materials at medieval prices - wages and incomes already sit at this scale")]
        [SettingPropertyGroup("Iron bank")]
        public bool HistoricalPricesEnabled { get; set; } = true;

        [SettingPropertyFloatingInteger("Hist Iron Ore Per Kg", 0.00f, 1.00f, "0.00", HintText = "iron ore, pence per kg (England c.1300, estimate)")]
        [SettingPropertyGroup("Iron bank")]
        public float HistIronOrePerKg { get; set; } = 0.075f;

        [SettingPropertyFloatingInteger("Hist Wood Per Kg", 0.00f, 1.00f, "0.00", HintText = "timber and firewood, pence per kg (Clark/Rogers)")]
        [SettingPropertyGroup("Iron bank")]
        public float HistWoodPerKg { get; set; } = 0.035f;

        [SettingPropertyFloatingInteger("Hist Charcoal Per Kg", 0.00f, 1.00f, "0.00", HintText = "charcoal, pence per kg (Clark/Rogers)")]
        [SettingPropertyGroup("Iron bank")]
        public float HistCharcoalPerKg { get; set; } = 0.07f;

        [SettingPropertyFloatingInteger("Hist Crude Iron Per Kg", 0.00f, 5.60f, "0.00", HintText = "crude bloom iron, pence per kg (Tudeley bloom 3s 4d)")]
        [SettingPropertyGroup("Iron bank")]
        public float HistCrudeIronPerKg { get; set; } = 1.4f;

        [SettingPropertyFloatingInteger("Hist Wrought Iron Per Kg", 0.00f, 10.00f, "0.00", HintText = "wrought bar iron, pence per kg (Clark/Rogers 1300-49: 1.15 d a pound)")]
        [SettingPropertyGroup("Iron bank")]
        public float HistWroughtIronPerKg { get; set; } = 2.5f;

        [SettingPropertyFloatingInteger("Hist Iron Per Kg", 0.00f, 14.00f, "0.00", HintText = "refined iron, pence per kg")]
        [SettingPropertyGroup("Iron bank")]
        public float HistIronPerKg { get; set; } = 3.5f;

        [SettingPropertyFloatingInteger("Hist Steel Per Kg", 0.00f, 24.00f, "0.00", HintText = "steel, pence per kg (estimate)")]
        [SettingPropertyGroup("Iron bank")]
        public float HistSteelPerKg { get; set; } = 6f;

        [SettingPropertyFloatingInteger("Hist Fine Steel Per Kg", 0.00f, 32.00f, "0.00", HintText = "fine steel, pence per kg (estimate)")]
        [SettingPropertyGroup("Iron bank")]
        public float HistFineSteelPerKg { get; set; } = 8f;

        [SettingPropertyFloatingInteger("Hist Valyrian Per Kg", 0.00f, 800.00f, "0.00", HintText = "Valyrian steel, pence per kg - a lost art, priced as a rare treasure")]
        [SettingPropertyGroup("Iron bank")]
        public float HistValyrianPerKg { get; set; } = 200f;

        [SettingPropertyFloatingInteger("Hist Leather Per Kg", 0.00f, 16.00f, "0.00", HintText = "tanned leather, pence per kg")]
        [SettingPropertyGroup("Iron bank")]
        public float HistLeatherPerKg { get; set; } = 4f;

        [SettingPropertyFloatingInteger("Hist Linen Per Kg", 0.00f, 40.00f, "0.00", HintText = "linen and canvas for padding, pence per kg")]
        [SettingPropertyGroup("Iron bank")]
        public float HistLinenPerKg { get; set; } = 10f;

        [SettingPropertyFloatingInteger("Hist Special Factor", 0.00f, 1.00f, "0.00", HintText = "horn, sinew and glue of bows, against the old game-scale bill")]
        [SettingPropertyGroup("Iron bank")]
        public float HistSpecialFactor { get; set; } = 0.1f;

        [SettingPropertyFloatingInteger("Hist Master Wage T1", 0.00f, 12.00f, "0.00", HintText = "a smith's day of work on plain gear, pence (a craftsman earned about 3 d a day)")]
        [SettingPropertyGroup("Iron bank")]
        public float HistMasterWageT1 { get; set; } = 3f;

        [SettingPropertyFloatingInteger("Hist Master Wage Per Tier", 0.00f, 6.00f, "0.00", HintText = "each tier above the first adds this to the master's day (a master armourer of fine harness about 10 d)")]
        [SettingPropertyGroup("Iron bank")]
        public float HistMasterWagePerTier { get; set; } = 1.5f;

        [SettingPropertyFloatingInteger("Hist Profit Percent", 0.00f, 100.00f, "0.00", HintText = "the maker's profit on top of material and labour")]
        [SettingPropertyGroup("Iron bank")]
        public float HistProfitPercent { get; set; } = 25f;

        [SettingPropertyFloatingInteger("Town Wage Ref Prosperity", 0.00f, 19200.00f, "0.00", HintText = "a town this prosperous pays its craftsmen exactly the historical day wage (the middle town of Westeros and Essos); a richer town pays more, up to half again (London paid about half again the provinces), a poorer less, down to half (0 = the same everywhere)")]
        [SettingPropertyGroup("Iron bank")]
        public float TownWageRefProsperity { get; set; } = 4800f;

        [SettingPropertyFloatingInteger("Hist Ammo Labor Multiplier", 0.00f, 32.00f, "0.00", HintText = "fletcher and arrowsmith work on a stack of arrows or bolts (a sheaf of 24 cost about 15 d)")]
        [SettingPropertyGroup("Iron bank")]
        public float HistAmmoLaborMultiplier { get; set; } = 8f;

        [SettingPropertyFloatingInteger("Hist Tournament Scale", 0.00f, 16.00f, "0.00", HintText = "the game seeks tournament prizes worth 1600-5000; with historical prices the range is divided by this (400-1250 pence: a fine sword, a good harness piece)")]
        [SettingPropertyGroup("Iron bank")]
        public float HistTournamentScale { get; set; } = 4f;

        [SettingPropertyFloatingInteger("Hist Unique Prestige", 0.00f, 16.00f, "0.00", HintText = "named pieces of the great houses cost this many times their making")]
        [SettingPropertyGroup("Iron bank")]
        public float HistUniquePrestige { get; set; } = 4f;

        [SettingPropertyFloatingInteger("Hist Bulk Unit Factor", 0.00f, 40.00f, "0.00", HintText = "ore and timber are sold by the load: one unit weighs this many times the game's 10 kg, so a unit costs whole pence (ore 100 kg = about 8 d, timber 100 kg = about 4 d) instead of a fraction of a penny rounded up to 1-2; village output, town use and every recipe are counted by weight, so nothing else changes")]
        [SettingPropertyGroup("Iron bank")]
        public float HistBulkUnitFactor { get; set; } = 10f;

        [SettingPropertyBool("Hist Trade Goods", HintText = "trade goods at historical prices too (grain, wine, tools, wool, silk, velvet, spices, ore of silver and gold...) - England c.1300 by the kilogram")]
        [SettingPropertyGroup("Iron bank")]
        public bool HistTradeGoods { get; set; } = true;

        [SettingPropertyBool("Hist Livestock Prices", HintText = "farm animals at historical prices too (England mid-14th century, by the head): ox 157, cow 113, hog 30, sheep 17, goose 4, chicken 1 penny, where the game asked 300, 200, 60, 80, 50 and 50. Horses, mules and camels keep their worth, which is already about right. Town demand for these animals is counted in the new coin like every other repriced good, so towns buy as many head as before (needs Historical Prices Enabled; off = the game's worth; a change takes effect when a game is next loaded)")]
        [SettingPropertyGroup("Iron bank")]
        public bool HistLivestockPrices { get; set; } = true;

        [SettingPropertyFloatingInteger("Hist Hides Per Kg", 0.00f, 4.00f, "0.00", HintText = "raw hides, pence per kg (an ox hide of 25-30 kg sold for 1-3 shillings)")]
        [SettingPropertyGroup("Iron bank")]
        public float HistHidesPerKg { get; set; } = 1f;

        [SettingPropertyFloatingInteger("Hist Flax Per Kg", 0.00f, 8.00f, "0.00", HintText = "raw flax and hemp, pence per kg (estimate)")]
        [SettingPropertyGroup("Iron bank")]
        public float HistFlaxPerKg { get; set; } = 2f;

        [SettingPropertyFloatingInteger("Hist Bow Labor Multiplier", 0.00f, 4.00f, "0.00", HintText = "a bowyer's days on a bow, against the smith's model (a war longbow cost 12-18 d)")]
        [SettingPropertyGroup("Iron bank")]
        public float HistBowLaborMultiplier { get; set; } = 1.0f;

        [SettingPropertyBool("Town Household Use", HintText = "townsfolk buy only their household share of raw goods - flax, wool, hides and ore went to weavers, tanners and smiths, not into homes (England c.1300)")]
        [SettingPropertyGroup("Iron bank")]
        public bool TownHouseholdUse { get; set; } = true;

        [SettingPropertyFloatingInteger("Town Use Flax", 0.00f, 1.00f, "0.00", HintText = "share of the townsfolk's old appetite for raw flax they keep (home spinning)")]
        [SettingPropertyGroup("Iron bank")]
        public float TownUseFlax { get; set; } = 0.02f;

        [SettingPropertyFloatingInteger("Town Use Wool", 0.00f, 1.00f, "0.00", HintText = "share kept for raw wool")]
        [SettingPropertyGroup("Iron bank")]
        public float TownUseWool { get; set; } = 0.02f;

        [SettingPropertyFloatingInteger("Town Use Hides", 0.00f, 1.00f, "0.00", HintText = "share kept for raw hides")]
        [SettingPropertyGroup("Iron bank")]
        public float TownUseHides { get; set; } = 0.02f;

        [SettingPropertyFloatingInteger("Town Use Iron", 0.00f, 1.00f, "0.00", HintText = "share kept for iron ore - households bought nails and tools, never ore")]
        [SettingPropertyGroup("Iron bank")]
        public float TownUseIron { get; set; } = 0f;

        [SettingPropertyFloatingInteger("Town Use Leather", 0.00f, 2.00f, "0.00", HintText = "share kept for leather (shoes, belts, straps)")]
        [SettingPropertyGroup("Iron bank")]
        public float TownUseLeather { get; set; } = 0.5f;

        [SettingPropertyFloatingInteger("Town Use Linen", 0.00f, 1.60f, "0.00", HintText = "share kept for linen cloth (shirts, bedding)")]
        [SettingPropertyGroup("Iron bank")]
        public float TownUseLinen { get; set; } = 0.4f;

        [SettingPropertyFloatingInteger("Town Use Hardwood", 0.00f, 4.00f, "0.00", HintText = "share kept for timber and firewood (hearths, bakers, brewers, building)")]
        [SettingPropertyGroup("Iron bank")]
        public float TownUseHardwood { get; set; } = 1f;

        [SettingPropertyBool("Hist Demand Scaling", HintText = "town demand for each repriced kind of goods is counted in the new coin too - otherwise townsfolk with the old purse buy up tens of times more cheap swords, ore and cloth and the stalls run dry")]
        [SettingPropertyGroup("Iron bank")]
        public bool HistDemandScaling { get; set; } = true;

        [SettingPropertyBool("Hist Demand From Definition", HintText = "the new-coin town demand of each kind of goods is reckoned from the worth its goods were defined with. A Realm of Thrones patch to Banner Kings divides the worth of every Banner Kings good by 100 (bread 20 becomes 0, honey 28 becomes 0, mead 120 becomes 1, fur 125 becomes 1), so bread, pies, fruit, honey, eggs, garum, papyrus and limestone kept their town demand in the old coin (bread up to 10 times its worth in big towns) and mead, fur, gold, ink and dyes got it many times too high. On: reckoned from the defined worth, like every other good; off = as before (a change takes effect when a game is next loaded)")]
        [SettingPropertyGroup("Iron bank")]
        public bool HistDemandFromDefinition { get; set; } = true;

        [SettingPropertyBool("Hist Mixed Category Shelf", HintText = "goods that share one town market but were repriced in opposite directions weigh on the stall by the share their makers gave them, not by their new worth. Gold ore became 8 times cheaper and the gold ingot nearly 5 times dearer than Banner Kings set them, so one ingot on a stall (worth 4750) counted like a hundred days of the whole town demand for gold: the first sold for about 2 in 5 parts of its worth, the next for less, and ore fetched 4.6 times its worth on a bare stall. On: an ingot weighs on the stall like 2.5 sacks of ore, as Banner Kings designed, so a bare stall pays about 1.2 times worth for an ingot and about 2 times for ore; the same rule evens apples, carrots and oranges, meat and whale meat, iron ingots beside ore. Prices are still paid in the new worth; kinds of goods with a single good do not change (needs Hist Demand Scaling; off = as before; a change takes effect when a game is next loaded)")]
        [SettingPropertyGroup("Iron bank")]
        public bool HistMixedCategoryShelf { get; set; } = true;

        [SettingPropertyBool("Price Formula In New Coin", HintText = "the game's price formula keeps a fixed 2 coins beside the worth of the goods on the stall; in the new coin that weighs like a quarter load of ore or a whole measure of salt, so a bare stall of a cheap good never grew dear (ore 1.5 times its worth at most, timber 0.8). On: every trade good the new coin made cheaper is priced as the unmodded game prices it - a bare stall up to 10 times worth, a glutted one down to a tenth; goods the new coin made dearer (fur, wool, raw silk, velvet) keep the fixed 2 coins, which weigh little beside them; and a new campaign opens with the towns' memory of supply and demand for every repriced trade good already in the new coin instead of drifting out of the old one for weeks (needs Hist Demand Scaling; off = prices as before)")]
        [SettingPropertyGroup("Iron bank")]
        public bool PriceFormulaInNewCoin { get; set; } = true;

        [SettingPropertyBool("Raw Price By Use", HintText = "a town prices the seven bulk raw goods (iron ore, timber, raw hides, leather, flax, linen, wool) by what it really uses each day: the share of the townsfolk's old appetite that households truly buy, plus what its craftsmen and workshops work up - the same daily use the caravans stock it by. A town with a smithy and no ore pays many times its worth, a town sitting on a hundred days of use pays a fraction, and wool is dear only where someone weaves it. Arms workshops then pay the market price for ore and timber too (needs Hist Demand Scaling; off = demand and workshop prices as before)")]
        [SettingPropertyGroup("Iron bank")]
        public bool RawPriceByUse { get; set; } = true;

        [SettingPropertyBool("Population Rent Enabled", HintText = "a fief pays by the people it stands for: each village and town on the map is a symbol of a whole land, and its lord's rents follow that land's population (the Reach richest, the Iron Islands poor)")]
        [SettingPropertyGroup("Iron bank")]
        public bool PopulationRentEnabled { get; set; } = true;

        [SettingPropertyFloatingInteger("Population Rent Per Head", 0.00f, 160.00f, "0.00", HintText = "rents and dues a lord draws from each subject a year, in coins (about 40 pence a head in the medieval estimate)")]
        [SettingPropertyGroup("Iron bank")]
        public float PopulationRentPerHead { get; set; } = 40f;

        [SettingPropertyFloatingInteger("Population Scale", 0.00f, 4.00f, "0.00", HintText = "scale on every land's population (Westeros ~30 million, Essos with its hinterlands ~30 million)")]
        [SettingPropertyGroup("Iron bank")]
        public float PopulationScale { get; set; } = 1f;

        [SettingPropertyFloatingInteger("Population Rent Max Share", 0.00f, 1.00f, "0.00", HintText = "a village pays its lord at most this share of its purse each day (0.5 emptied the villages in a few days)")]
        [SettingPropertyGroup("Iron bank")]
        public float PopulationRentMaxShare { get; set; } = 0.2f;

        [SettingPropertyFloatingInteger("Rent Tax Low", 0.00f, 2.80f, "0.00", HintText = "the fief's tax decree (Banner Kings) sets the rent: Low takes this share of the usual rent")]
        [SettingPropertyGroup("Iron bank")]
        public float RentTaxLow { get; set; } = 0.7f;

        [SettingPropertyFloatingInteger("Rent Tax High", 0.00f, 5.20f, "0.00", HintText = "High tax takes this much of the usual rent (and Banner Kings costs loyalty for it)")]
        [SettingPropertyGroup("Iron bank")]
        public float RentTaxHigh { get; set; } = 1.3f;

        [SettingPropertyFloatingInteger("Rent Tax Exemption", 0.00f, 1.00f, "0.00", HintText = "Exemption: no rent at all")]
        [SettingPropertyGroup("Iron bank")]
        public float RentTaxExemption { get; set; } = 0f;

        [SettingPropertyBool("Rent Replaces Town Tax", HintText = "one source of land income: Banner Kings' town population tax (paid from nothing) is replaced by rent from the town purse")]
        [SettingPropertyGroup("Iron bank")]
        public bool RentReplacesTownTax { get; set; } = true;

        [SettingPropertyFloatingInteger("Town Rent Share", 0.00f, 1.00f, "0.00", HintText = "share of the town purse above the merchants' floor its lord draws each day as rents, tolls and farms")]
        [SettingPropertyGroup("Iron bank")]
        public float TownRentShare { get; set; } = 0.07f;

        [SettingPropertyFloatingInteger("Town Rent Floor Gold", 0.00f, 80000.00f, "0.00", HintText = "a town keeps this much for its merchants - below 20 000 Banner Kings takes prosperity away")]
        [SettingPropertyGroup("Iron bank")]
        public float TownRentFloorGold { get; set; } = 20000f;

        [SettingPropertyBool("Workshop No Free Raw", HintText = "nothing from thin air: the hidden town artisans no longer make timber, ore, hides, meat, leather and linen without any input, and a workshop roll never yields charcoal or ingots (those only come from smelting) - raw goods come from the villages")]
        [SettingPropertyGroup("Iron bank")]
        public bool WorkshopNoFreeRaw { get; set; } = true;

        [SettingPropertyBool("Historical Recruit Cost", HintText = "a recruit costs his prest money - some days of his pay (mercenaries twice that) - instead of the game's flat table by level")]
        [SettingPropertyGroup("Iron bank")]
        public bool HistoricalRecruitCost { get; set; } = true;

        [SettingPropertyFloatingInteger("Recruit Cost Days", 0.00f, 40.00f, "0.00", HintText = "days of a troop's daily pay paid to take him on (historical prest/advance: a few to a dozen days of wages)")]
        [SettingPropertyGroup("Iron bank")]
        public float RecruitCostDays { get; set; } = 10f;

        [SettingPropertyBool("Ammo Recovery Enabled", HintText = "arrows and bolts are spent: of those shot, the side holding the field gathers some back, some are mended, the rest are lost; the beaten side loses all it shot")]
        [SettingPropertyGroup("Iron bank")]
        public bool AmmoRecoveryEnabled { get; set; } = true;

        [SettingPropertyFloatingInteger("Ammo Recover Percent", 0.00f, 180.00f, "0.00", HintText = "share of shot arrows the winner picks up whole (estimate from Poitiers, Towton and the 1343 Breton shipment)")]
        [SettingPropertyGroup("Iron bank")]
        public float AmmoRecoverPercent { get; set; } = 45f;

        [SettingPropertyFloatingInteger("Ammo Repair Percent", 0.00f, 80.00f, "0.00", HintText = "share of shot arrows mended by the army's fletchers (new fletching, heads) - the rest are broken or lost")]
        [SettingPropertyGroup("Iron bank")]
        public float AmmoRepairPercent { get; set; } = 20f;

        [SettingPropertyBool("Keep Wear Through Battle", HintText = "gear your men take into battle comes back in the state it went out (and worse after the fight) - worn mail no longer returns as new; damage piles up and must be repaired")]
        [SettingPropertyGroup("Iron bank")]
        public bool KeepWearThroughBattle { get; set; } = true;

        [SettingPropertyBool("Start Kit Enabled", HintText = "starting gear you cannot wear (heavy armour your Athletics cannot carry) is swapped once, after character creation, for the best piece of the same kind you can wear - never dearer than the original")]
        [SettingPropertyGroup("Iron bank")]
        public bool StartKitEnabled { get; set; } = true;

        [SettingPropertyInteger("Start Gold Adventurer", 0, 400, "0", HintText = "coins an Adventurer starts with in the Banner Kings start (Banner Kings gives 1000 - over a year of a labourer's pay); -1 leaves Banner Kings as it is")]
        [SettingPropertyGroup("Iron bank")]
        public int StartGoldAdventurer { get; set; } = 100;

        [SettingPropertyBool("Outlaw Law Enabled", HintText = "outlaws are real men: deserters, unpaid soldiers, men routed from battle, villagers driven off by raids, hunger and war; bands form only where such men exist and wear only what they brought, looted or bought")]
        [SettingPropertyGroup("Iron bank")]
        public bool OutlawLawEnabled { get; set; } = true;

        [SettingPropertyFloatingInteger("Outlaw Seed Per Hearth", 0.00f, 1.00f, "0.00", HintText = "outlaws already in the woods when the campaign begins, per hearth of each region's villages (taken from those villages)")]
        [SettingPropertyGroup("Iron bank")]
        public float OutlawSeedPerHearth { get; set; } = 0.03f;

        [SettingPropertyFloatingInteger("Outlaw Daily Per Thousand Hearth", 0.00f, 1.00f, "0.00", HintText = "men a region loses to the woods each day per 1000 hearths, times its misery (poverty, war, burnt villages, hunger, lawlessness)")]
        [SettingPropertyGroup("Iron bank")]
        public float OutlawDailyPerThousandHearth { get; set; } = 0.1f;

        [SettingPropertyFloatingInteger("Outlaw War Misery", 0.00f, 2.00f, "0.00", HintText = "misery added while the region's realm is at war - no order, easy plunder")]
        [SettingPropertyGroup("Iron bank")]
        public float OutlawWarMisery { get; set; } = 0.5f;

        [SettingPropertyFloatingInteger("Outlaw Looted Misery", 0.00f, 8.00f, "0.00", HintText = "misery added when all of a region's villages lie burnt (scaled by the share burnt)")]
        [SettingPropertyGroup("Iron bank")]
        public float OutlawLootedMisery { get; set; } = 2f;

        [SettingPropertyFloatingInteger("Outlaw Starving Misery", 0.00f, 6.00f, "0.00", HintText = "misery added while the region's town or castle is starving")]
        [SettingPropertyGroup("Iron bank")]
        public float OutlawStarvingMisery { get; set; } = 1.5f;

        [SettingPropertyInteger("Outlaw Prosperity Good", 0, 20000, "0", HintText = "prosperity at which a town counts as fully well-off (no misery from poverty)")]
        [SettingPropertyGroup("Iron bank")]
        public int OutlawProsperityGood { get; set; } = 5000;

        [SettingPropertyFloatingInteger("Outlaw Return Base Percent", 0.00f, 2.00f, "0.00", HintText = "percent of a region's outlaws who go home each day in any times")]
        [SettingPropertyGroup("Iron bank")]
        public float OutlawReturnBasePercent { get; set; } = 0.5f;

        [SettingPropertyFloatingInteger("Outlaw Return Peace Percent", 0.00f, 8.00f, "0.00", HintText = "extra percent going home each day in peace, scaled by prosperity")]
        [SettingPropertyGroup("Iron bank")]
        public float OutlawReturnPeacePercent { get; set; } = 2f;

        [SettingPropertyFloatingInteger("Outlaw Routed Share", 0.00f, 2.00f, "0.00", HintText = "share of men routed from a battle who take to the woods instead of going home")]
        [SettingPropertyGroup("Iron bank")]
        public float OutlawRoutedShare { get; set; } = 0.5f;

        [SettingPropertyFloatingInteger("Outlaw Raid Flee Percent", 0.00f, 12.00f, "0.00", HintText = "percent of a village's hearths that flee to the woods when it is burnt")]
        [SettingPropertyGroup("Iron bank")]
        public float OutlawRaidFleePercent { get; set; } = 3f;

        [SettingPropertyFloatingInteger("Outlaw Hearth Per Man", 0.00f, 2.00f, "0.00", HintText = "hearths a village loses for each man who becomes an outlaw (and regains when he returns)")]
        [SettingPropertyGroup("Iron bank")]
        public float OutlawHearthPerMan { get; set; } = 0.5f;

        [SettingPropertyInteger("Outlaw Min Band", 0, 24, "0", HintText = "fewest men needed nearby before a new band can form")]
        [SettingPropertyGroup("Iron bank")]
        public int OutlawMinBand { get; set; } = 6;

        [SettingPropertyInteger("Outlaw Neighbour Regions", 0, 16, "0", HintText = "a new band gathers men from its own region and this many nearest ones")]
        [SettingPropertyGroup("Iron bank")]
        public int OutlawNeighbourRegions { get; set; } = 4;

        [SettingPropertyFloatingInteger("Outlaw Band Size Scale", 0.00f, 4.00f, "0.00", HintText = "size of a new band against the game's usual size (still only as many as there are men)")]
        [SettingPropertyGroup("Iron bank")]
        public float OutlawBandSizeScale { get; set; } = 1f;

        [SettingPropertyInteger("Outlaw Daily Recruit", 0, 10, "0", HintText = "men a band can take in each day from the outlaws of the region it roams")]
        [SettingPropertyGroup("Iron bank")]
        public int OutlawDailyRecruit { get; set; } = 2;

        [SettingPropertyFloatingInteger("Outlaw Prisoner Join Percent", 0.00f, 40.00f, "0.00", HintText = "percent of a band's healthy prisoners who join it each day (green men sooner than veterans)")]
        [SettingPropertyGroup("Iron bank")]
        public float OutlawPrisonerJoinPercent { get; set; } = 10f;

        [SettingPropertyFloatingInteger("Outlaw Commoner Max Armor Kg", 0.00f, 32.00f, "0.00", HintText = "a commoner joins a band as its clan's lowest bandit only if that troop wears body armour this light; otherwise as a looter")]
        [SettingPropertyGroup("Iron bank")]
        public float OutlawCommonerMaxArmorKg { get; set; } = 8f;

        [SettingPropertyBool("Outlaw Gear Upgrades", HintText = "a bandit rises in rank only with gear for it: armour and horse from the band's loot, or bought from a fence in a town within reach")]
        [SettingPropertyGroup("Iron bank")]
        public bool OutlawGearUpgrades { get; set; } = true;

        [SettingPropertyFloatingInteger("Outlaw Fence Radius", 0.00f, 800.00f, "0.00", HintText = "reach of the fences: a band deals with every unbesieged town this near and takes the best bargain; a band with no town this near still reaches the nearest one, on the far-edge terms")]
        [SettingPropertyGroup("Iron bank")]
        public float OutlawFenceRadius { get; set; } = 200f;

        [SettingPropertyFloatingInteger("Outlaw Fence Markup", 0.00f, 6.00f, "0.00", HintText = "what the fence asks over an item's value when the band stands at the town's gates")]
        [SettingPropertyGroup("Iron bank")]
        public float OutlawFenceMarkup { get; set; } = 1.5f;

        [SettingPropertyFloatingInteger("Outlaw Fence Markup Far", 0.00f, 8.00f, "0.00", HintText = "what the fence asks over an item's value at the edge of his reach - the price rises evenly with the road he must ride (armour and horses are light for their worth, so less steeply than plunder loses value)")]
        [SettingPropertyGroup("Iron bank")]
        public float OutlawFenceMarkupFar { get; set; } = 2f;

        [SettingPropertyBool("Outlaw No Free Gold", HintText = "bands get no gold from nowhere: only what they plunder (no daily top-up, no purse at birth beyond a few coins a man)")]
        [SettingPropertyGroup("Iron bank")]
        public bool OutlawNoFreeGold { get; set; } = true;

        [SettingPropertyInteger("Outlaw Coins Per Man", 0, 10, "0", HintText = "coins each man of a new band is handed from what its home hideout holds above the kept hoard, as far as that goes (no surplus or no hideout - the band starts with none)")]
        [SettingPropertyGroup("Iron bank")]
        public int OutlawCoinsPerMan { get; set; } = 2;

        [SettingPropertyBool("Outlaw Fence Buys Loot", HintText = "bands sell their plunder to the fences of the towns within reach, each piece where it pays the band best: trade goods, livestock and food go back on the town's shelves, and the town pays from its purse above the merchants' floor")]
        [SettingPropertyGroup("Iron bank")]
        public bool OutlawFenceBuysLoot { get; set; } = true;

        [SettingPropertyFloatingInteger("Outlaw Fence Loot Share", 0.00f, 2.00f, "0.00", HintText = "share of the town's buying price the fence hands a band standing at the town's gates - the rest is the fence's cut and stays in the town's purse")]
        [SettingPropertyGroup("Iron bank")]
        public float OutlawFenceLootShare { get; set; } = 0.5f;

        [SettingPropertyFloatingInteger("Outlaw Fence Loot Share Far", 0.00f, 1.00f, "0.00", HintText = "share of the town's buying price the fence hands a band at the edge of his reach - it falls evenly with the road and the risk of carting stolen goods")]
        [SettingPropertyGroup("Iron bank")]
        public float OutlawFenceLootShareFar { get; set; } = 0.25f;

        [SettingPropertyBool("Outlaw Fence Buys Spare Animals", HintText = "bands also sell the animals they have no use for: pack beasts beyond what their remaining packs weigh, and riding horses beyond one for every man on foot")]
        [SettingPropertyGroup("Iron bank")]
        public bool OutlawFenceBuysSpareAnimals { get; set; } = true;

        [SettingPropertyBool("Outlaw No Free Food", HintText = "bands get no food from nowhere (they do not eat in this game - what they carry is plunder), and so the fence takes their food too; off = the game hands every new band food again and the fence leaves food alone")]
        [SettingPropertyGroup("Iron bank")]
        public bool OutlawNoFreeFood { get; set; } = true;

        [SettingPropertyBool("Outlaw No Hideout Gold", HintText = "a band coming home no longer gets a quarter of the worth of its packs as gold from nowhere, and neither does its hideout")]
        [SettingPropertyGroup("Iron bank")]
        public bool OutlawNoHideoutGold { get; set; } = true;

        [SettingPropertyFloatingInteger("Outlaw Hideout Stash Share", 0.00f, 1.00f, "0.00", HintText = "share of its own purse a band leaves in the hideout's hoard each time it comes home - real coin, found by whoever clears and searches the hideout (0 = nothing is put by)")]
        [SettingPropertyGroup("Iron bank")]
        public float OutlawHideoutStashShare { get; set; } = 0.25f;

        [SettingPropertyBool("Outlaw Hoard Circulates", HintText = "a hideout's hoard is spent, not only heaped: whatever lies above the kept hoard hands new bands their first coins, tops up its bands when they are short at the fence, and goes on their living in town")]
        [SettingPropertyGroup("Iron bank")]
        public bool OutlawHoardCirculates { get; set; } = true;

        [SettingPropertyFloatingInteger("Outlaw Life Spend Share", 0.00f, 1.00f, "0.00", HintText = "share of its purse a band spends each day on food, drink and company in the nearest open town - and the share of a hoard's surplus its hideout spends the same way (0 = nothing is spent)")]
        [SettingPropertyGroup("Iron bank")]
        public float OutlawLifeSpendShare { get; set; } = 0.1f;

        [SettingPropertyBool("Climate Enabled", HintText = "the seasons of Westeros: one season at a time lasting for years, its end proclaimed by a white raven from the Citadel; dates read 'Day N of Summer, 299 AC'")]
        [SettingPropertyGroup("Iron bank")]
        public bool ClimateEnabled { get; set; } = true;

        [SettingPropertyBool("Climate Drives Economy", HintText = "food, harvests, sickness, travel and the AI's winter caution (RealisticBannerlord, BetterEconomy, StrategicCampaignAI) follow the long seasons too - a winter of years is a hungry one")]
        [SettingPropertyGroup("Iron bank")]
        public bool ClimateDrivesEconomy { get; set; } = true;

        [SettingPropertyInteger("Climate Summer Days So Far", 0, 14560, "0", HintText = "how long the summer has already lasted when the campaign begins (the summer that lasted ten years)")]
        [SettingPropertyGroup("Iron bank")]
        public int ClimateSummerDaysSoFar { get; set; } = 3640;

        [SettingPropertyInteger("Climate Summer Days Left Min", 0, 480, "0", HintText = "fewest days the long summer still lasts after the campaign begins")]
        [SettingPropertyGroup("Iron bank")]
        public int ClimateSummerDaysLeftMin { get; set; } = 120;

        [SettingPropertyInteger("Climate Summer Days Left Max", 0, 1080, "0", HintText = "most days the long summer still lasts after the campaign begins")]
        [SettingPropertyGroup("Iron bank")]
        public int ClimateSummerDaysLeftMax { get; set; } = 270;

        [SettingPropertyInteger("Climate First Autumn Days Min", 0, 1200, "0", HintText = "the first autumn lasts at least this many days")]
        [SettingPropertyGroup("Iron bank")]
        public int ClimateFirstAutumnDaysMin { get; set; } = 300;

        [SettingPropertyInteger("Climate First Autumn Days Max", 0, 1800, "0", HintText = "the first autumn lasts at most this many days")]
        [SettingPropertyGroup("Iron bank")]
        public int ClimateFirstAutumnDaysMax { get; set; } = 450;

        [SettingPropertyFloatingInteger("Climate First Winter Years Min", 0.00f, 12.00f, "0.00", HintText = "the first winter lasts at least this many years")]
        [SettingPropertyGroup("Iron bank")]
        public float ClimateFirstWinterYearsMin { get; set; } = 3f;

        [SettingPropertyFloatingInteger("Climate First Winter Years Max", 0.00f, 20.00f, "0.00", HintText = "the first winter lasts at most this many years")]
        [SettingPropertyGroup("Iron bank")]
        public float ClimateFirstWinterYearsMax { get; set; } = 5f;

        [SettingPropertyFloatingInteger("Climate Spring Years Min", 0.00f, 4.00f, "0.00", HintText = "later springs last at least this many years")]
        [SettingPropertyGroup("Iron bank")]
        public float ClimateSpringYearsMin { get; set; } = 1f;

        [SettingPropertyFloatingInteger("Climate Spring Years Max", 0.00f, 12.00f, "0.00", HintText = "later springs last at most this many years")]
        [SettingPropertyGroup("Iron bank")]
        public float ClimateSpringYearsMax { get; set; } = 3f;

        [SettingPropertyFloatingInteger("Climate Summer Years Min", 0.00f, 8.00f, "0.00", HintText = "later summers last at least this many years")]
        [SettingPropertyGroup("Iron bank")]
        public float ClimateSummerYearsMin { get; set; } = 2f;

        [SettingPropertyFloatingInteger("Climate Summer Years Max", 0.00f, 32.00f, "0.00", HintText = "later summers last at most this many years")]
        [SettingPropertyGroup("Iron bank")]
        public float ClimateSummerYearsMax { get; set; } = 8f;

        [SettingPropertyFloatingInteger("Climate Autumn Years Min", 0.00f, 4.00f, "0.00", HintText = "later autumns last at least this many years")]
        [SettingPropertyGroup("Iron bank")]
        public float ClimateAutumnYearsMin { get; set; } = 1f;

        [SettingPropertyFloatingInteger("Climate Autumn Years Max", 0.00f, 8.00f, "0.00", HintText = "later autumns last at most this many years")]
        [SettingPropertyGroup("Iron bank")]
        public float ClimateAutumnYearsMax { get; set; } = 2f;

        [SettingPropertyFloatingInteger("Climate Winter Years Min", 0.00f, 8.00f, "0.00", HintText = "later winters last at least this many years")]
        [SettingPropertyGroup("Iron bank")]
        public float ClimateWinterYearsMin { get; set; } = 2f;

        [SettingPropertyFloatingInteger("Climate Winter Years Max", 0.00f, 24.00f, "0.00", HintText = "later winters last at most this many years")]
        [SettingPropertyGroup("Iron bank")]
        public float ClimateWinterYearsMax { get; set; } = 6f;

        [SettingPropertyBool("Crown Dues Enabled", HintText = "vassals owe the crown a share of their daily income (fief income and rents), paid into the kingdom treasury - small in peace, heavy in war as with medieval war taxes")]
        [SettingPropertyGroup("Iron bank")]
        public bool CrownDuesEnabled { get; set; } = true;

        [SettingPropertyFloatingInteger("Crown Dues Peace Percent", 0.00f, 8.00f, "0.00", HintText = "share of a vassal house's daily income owed to the crown in peace (aids and dues)")]
        [SettingPropertyGroup("Iron bank")]
        public float CrownDuesPeacePercent { get; set; } = 2f;

        [SettingPropertyFloatingInteger("Crown Dues War Percent", 0.00f, 12.00f, "0.00", HintText = "share owed while the realm is at war (aids, scutage in place of service) - the war tax itself is paid by the subjects (lay subsidy below)")]
        [SettingPropertyGroup("Iron bank")]
        public float CrownDuesWarPercent { get; set; } = 3f;

        [SettingPropertyBool("Lay Subsidy Enabled", HintText = "in war the realm's towns and villages pay a war subsidy (the fifteenth and tenth) from their purses into the kingdom treasury")]
        [SettingPropertyGroup("Iron bank")]
        public bool LaySubsidyEnabled { get; set; } = true;

        [SettingPropertyFloatingInteger("Lay Subsidy Town Share", 0.00f, 1.00f, "0.00", HintText = "share of a town's purse above the merchants' floor paid each day of war")]
        [SettingPropertyGroup("Iron bank")]
        public float LaySubsidyTownShare { get; set; } = 0.01f;

        [SettingPropertyFloatingInteger("Lay Subsidy Village Share", 0.00f, 1.00f, "0.00", HintText = "share of a village purse paid each day of war")]
        [SettingPropertyGroup("Iron bank")]
        public float LaySubsidyVillageShare { get; set; } = 0.015f;

        [SettingPropertyFloatingInteger("Lay Subsidy War Tax Multiplier", 0.00f, 8.00f, "0.00", HintText = "with the War Tax policy the subsidy is this many times heavier")]
        [SettingPropertyGroup("Iron bank")]
        public float LaySubsidyWarTaxMultiplier { get; set; } = 2f;

        [SettingPropertyBool("Crown Customs Enabled", HintText = "every crown takes customs on trade: a share of each town's toll counter goes to the kingdom treasury (the wool custom of 1275)")]
        [SettingPropertyGroup("Iron bank")]
        public bool CrownCustomsEnabled { get; set; } = true;

        [SettingPropertyFloatingInteger("Crown Customs Share", 0.00f, 1.00f, "0.00", HintText = "share of a town's daily toll counter taken as customs")]
        [SettingPropertyGroup("Iron bank")]
        public float CrownCustomsShare { get; set; } = 0.1f;

        [SettingPropertyFloatingInteger("Crown Customs Duty Multiplier", 0.00f, 8.00f, "0.00", HintText = "with the Crown Duty policy the customs are this many times heavier")]
        [SettingPropertyGroup("Iron bank")]
        public float CrownCustomsDutyMultiplier { get; set; } = 2f;

        [SettingPropertyBool("Policy Income Conserved", HintText = "Debasement and State Monopolies no longer make gold from nothing: the mint's profit comes out of the realm's town purses, monopoly dues out of workshop capital; foreign caravans no longer bring 500 from nothing under trade agreements; Banner Kings' village tax office is replaced by the rent")]
        [SettingPropertyGroup("Iron bank")]
        public bool PolicyIncomeConserved { get; set; } = true;

        [SettingPropertyFloatingInteger("Debasement Share", 0.00f, 1.00f, "0.00", HintText = "share of each town purse above the merchants' floor the crown takes each day while it debases the coin")]
        [SettingPropertyGroup("Iron bank")]
        public float DebasementShare { get; set; } = 0.005f;

        [SettingPropertyBool("No Rot Clan Bailout", HintText = "Realm of Thrones no longer hands every poor AI house Tier x 5000 gold a day from thin air - a house short of money borrows from the Iron Bank or goes bankrupt")]
        [SettingPropertyGroup("Iron bank")]
        public bool NoRotClanBailout { get; set; } = true;

        [SettingPropertyBool("No Free Kingdom Gold", HintText = "the kingdom treasury no longer refills from thin air (vanilla +1000 a day and random windfalls of 100-400 thousand); it lives on what the houses pay in - change needs a game restart")]
        [SettingPropertyGroup("Iron bank")]
        public bool NoFreeKingdomGold { get; set; } = true;

        [SettingPropertyInteger("Iron Bank Min Days To Lend", 0, 120, "0", HintText = "the Bank lends no more to a house whose running debt falls due within this many days - pay first")]
        [SettingPropertyGroup("Iron bank")]
        public int IronBankMinDaysToLend { get; set; } = 30;

        [SettingPropertyFloatingInteger("Iron Bank Loan Fee Percent", 0.00f, 8.00f, "0.00", HintText = "fee the Iron Bank adds to every loan, owed with the debt - no free same-day borrowing")]
        [SettingPropertyGroup("Iron bank")]
        public float IronBankLoanFeePercent { get; set; } = 2f;

        [SettingPropertyFloatingInteger("Iron Bank Default Seize Share", 0.00f, 2.00f, "0.00", HintText = "share of a defaulter's purse the Bank seizes at once when he defaults (then 25% a day as before)")]
        [SettingPropertyGroup("Iron bank")]
        public float IronBankDefaultSeizeShare { get; set; } = 0.5f;

        [SettingPropertyBool("Iron Bank Enabled", HintText = "the Iron Bank of Braavos lends to lords (AI and you): wages and war chests on credit, repaid daily with interest and from the spoils of war - and woe to those who do not pay")]
        [SettingPropertyGroup("Iron bank")]
        public bool IronBankEnabled { get; set; } = true;

        [SettingPropertyInteger("Iron Bank Capital", 0, 20000000, "0", HintText = "gold the Bank starts with; loans draw it down, repayments with interest fill it up - an empty Bank lends nothing")]
        [SettingPropertyGroup("Iron bank")]
        public int IronBankCapital { get; set; } = 5000000;

        [SettingPropertyFloatingInteger("Iron Bank Income Days", 0.00f, 240.00f, "0.00", HintText = "a house may borrow this many days of its income, plus its fiefs as surety")]
        [SettingPropertyGroup("Iron bank")]
        public float IronBankIncomeDays { get; set; } = 60f;

        [SettingPropertyInteger("Iron Bank Per Town", 0, 40000, "0", HintText = "surety the Bank counts for every town a house holds")]
        [SettingPropertyGroup("Iron bank")]
        public int IronBankPerTown { get; set; } = 10000;

        [SettingPropertyInteger("Iron Bank Per Castle", 0, 20000, "0", HintText = "surety the Bank counts for every castle a house holds")]
        [SettingPropertyGroup("Iron bank")]
        public int IronBankPerCastle { get; set; } = 5000;

        [SettingPropertyInteger("Iron Bank Wage Days", 0, 40, "0", HintText = "an AI lord borrows when his gold will not cover this many days of his armies' wages (twice that at war)")]
        [SettingPropertyGroup("Iron bank")]
        public int IronBankWageDays { get; set; } = 10;

        [SettingPropertyFloatingInteger("Iron Bank Rate King", 0.00f, 80.00f, "0.00", HintText = "yearly interest for a king (percent)")]
        [SettingPropertyGroup("Iron bank")]
        public float IronBankRateKing { get; set; } = 20f;

        [SettingPropertyFloatingInteger("Iron Bank Rate Landed", 0.00f, 120.00f, "0.00", HintText = "yearly interest for a house with fiefs (percent)")]
        [SettingPropertyGroup("Iron bank")]
        public float IronBankRateLanded { get; set; } = 30f;

        [SettingPropertyFloatingInteger("Iron Bank Rate Landless", 0.00f, 180.00f, "0.00", HintText = "yearly interest for a house without fiefs (percent)")]
        [SettingPropertyGroup("Iron bank")]
        public float IronBankRateLandless { get; set; } = 45f;

        [SettingPropertyFloatingInteger("Iron Bank Rate Per Loan", 0.00f, 40.00f, "0.00", HintText = "extra yearly interest when a debt is already running (percentage points)")]
        [SettingPropertyGroup("Iron bank")]
        public float IronBankRatePerLoan { get; set; } = 10f;

        [SettingPropertyFloatingInteger("Iron Bank Rate After Default", 0.00f, 60.00f, "0.00", HintText = "extra yearly interest for a house that has defaulted before (percentage points)")]
        [SettingPropertyGroup("Iron bank")]
        public float IronBankRateAfterDefault { get; set; } = 15f;

        [SettingPropertyFloatingInteger("Iron Bank Player Default Renown", 0.00f, 400.00f, "0.00", HintText = "renown you lose when the Bank writes your name among those who did not pay")]
        [SettingPropertyGroup("Iron bank")]
        public float IronBankPlayerDefaultRenown { get; set; } = 100f;

        [SettingPropertyInteger("Iron Bank Log Per Day", 0, 60, "0", HintText = "how many loans and repayments are written to the log each day (the daily total always is)")]
        [SettingPropertyGroup("Iron bank")]
        public int IronBankLogPerDay { get; set; } = 15;

        [SettingPropertyBool("Iron Bank Family Pays", HintText = "AI houses only: before the Iron Bank marks an instalment as missed or lends anew, the grown members of the house hand their head what is lacking - only what each holds above 5000 gold or Iron Bank Wage Days (default 10) of his own party's wages, whichever is more")]
        [SettingPropertyGroup("Iron bank")]
        public bool IronBankFamilyPays { get; set; } = true;

        [SettingPropertyBool("Soldier Pay To Purse", HintText = "the wages a party is actually paid no longer vanish: they go to the purse of its men, who spend them in the towns (mending, missing kit, food and drink) - your own men too; needs Men Purse Enabled")]
        [SettingPropertyGroup("The soldier's pay")]
        public bool SoldierPayToPurse { get; set; } = true;

        [SettingPropertyBool("Garrison Pay To Coffers", HintText = "the wages a garrison is actually paid go to the purse of the town or castle it guards - the soldiers spend them on the spot; your own garrisons too")]
        [SettingPropertyGroup("The soldier's pay")]
        public bool GarrisonPayToCoffers { get; set; } = true;

        [SettingPropertyBool("Crown Wage Refund Enabled", HintText = "a kingdom at war repays its houses a share of the wages they actually paid that day, out of the kingdom treasury and only while there is gold in it (shared out pro rata when it runs short); no refund in peace, to mercenaries or to houses without a kingdom")]
        [SettingPropertyGroup("The soldier's pay")]
        public bool CrownWageRefundEnabled { get; set; } = true;

        [SettingPropertyFloatingInteger("Crown Wage Refund Percent", 0.00f, 200.00f, "0.00", HintText = "share of the day's wages the treasury of a kingdom at war repays to each of its houses (0-100)")]
        [SettingPropertyGroup("The soldier's pay")]
        public float CrownWageRefundPercent { get; set; } = 50f;

        [SettingPropertyBool("Crown Wage Refund Garrisons", HintText = "the refund counts garrison wages too; off = wages of the parties in the field only")]
        [SettingPropertyGroup("The soldier's pay")]
        public bool CrownWageRefundGarrisons { get; set; } = true;

        [SettingPropertyBool("Town Wage Shield", HintText = "wages spent in a town (garrison pay, the men's spending on leaving) are kept out of reach of the game's daily town-purse regulator, which otherwise deletes a quarter of everything above its target each day: they stay until the lord's rents and the war subsidy draw them out, and what is still there after about two weeks the regulator may take; castles unchanged. Mind the side effect: with it a garrison in one's own town costs its lord next to nothing, for the pay comes back to him in rents")]
        [SettingPropertyGroup("The soldier's pay")]
        public bool TownWageShield { get; set; } = true;

        [SettingPropertyBool("Army Clothing Enabled", HintText = "every soldier on pay wears out his shoes, clothes and linen: men in a lord's party (yours too) buy leather, felt (woollen cloth) and linen in the towns they leave - piece by piece at the market price, out of their own purse, before they spend the rest on food and drink; a town garrison takes them from its own town's market without paying, for its pay already went into that town's purse; a castle garrison has the castle purse buy them in the town its villages trade with. What cannot be had waits (Army Clothing Max Wait Days), then is only noted in the log. Banner Kings party supplies no longer buy or use up wool, linen or flax for the troops, and their 'Textiles supplies' morale penalty is gone - one rule for clothing, rags carry no penalty (off = no wear, Banner Kings textiles and their morale penalty as before)")]
        [SettingPropertyGroup("The soldier's clothes")]
        public bool ArmyClothingEnabled { get; set; } = true;

        [SettingPropertyFloatingInteger("Army Clothing Field Leather Kg", 0.00f, 8.00f, "0.00", HintText = "kg of leather a soldier in a party wears out in a year: four pairs of shoes of about 0.4 kg (a pair lasted a season of marching) and his belts, straps and pouch; a crate of leather on the market is 10 kg")]
        [SettingPropertyGroup("The soldier's clothes")]
        public float ArmyClothingFieldLeatherKg { get; set; } = 2f;

        [SettingPropertyFloatingInteger("Army Clothing Field Cloth Kg", 0.00f, 12.00f, "0.00", HintText = "kg of woollen cloth (felt on the market) a soldier in a party wears out in a year: tunic, hood and hose - the yearly livery of a king's archer, two and a half to three yards of cloth - and his share of cloaks and blankets")]
        [SettingPropertyGroup("The soldier's clothes")]
        public float ArmyClothingFieldClothKg { get; set; } = 3f;

        [SettingPropertyFloatingInteger("Army Clothing Field Linen Kg", 0.00f, 10.00f, "0.00", HintText = "kg of linen a soldier in a party wears out in a year: two shirts and two pairs of braies (about 1.2 kg) and his share of tents, sacks and bags - a tent for six to ten men lasted a year or two")]
        [SettingPropertyGroup("The soldier's clothes")]
        public float ArmyClothingFieldLinenKg { get; set; } = 2.5f;

        [SettingPropertyFloatingInteger("Army Clothing Garrison Leather Kg", 0.00f, 4.80f, "0.00", HintText = "the same for a man of a garrison, who marches little: two pairs of shoes and his belts")]
        [SettingPropertyGroup("The soldier's clothes")]
        public float ArmyClothingGarrisonLeatherKg { get; set; } = 1.2f;

        [SettingPropertyFloatingInteger("Army Clothing Garrison Cloth Kg", 0.00f, 8.00f, "0.00", HintText = "woollen cloth a garrison man wears out in a year - tunic and hood last longer behind walls")]
        [SettingPropertyGroup("The soldier's clothes")]
        public float ArmyClothingGarrisonClothKg { get; set; } = 2f;

        [SettingPropertyFloatingInteger("Army Clothing Garrison Linen Kg", 0.00f, 4.00f, "0.00", HintText = "linen a garrison man wears out in a year - shirts only, he sleeps under a roof")]
        [SettingPropertyGroup("The soldier's clothes")]
        public float ArmyClothingGarrisonLinenKg { get; set; } = 1f;

        [SettingPropertyInteger("Army Clothing Max Wait Days", 0, 480, "0", HintText = "how many days of wear the men can put off when the town has no leather, cloth or linen or their purse is empty; beyond that their clothes simply go to rags (noted in the log only, no penalty in the game)")]
        [SettingPropertyGroup("The soldier's clothes")]
        public int ArmyClothingMaxWaitDays { get; set; } = 120;

        [SettingPropertyBool("Supply Demand Enabled", HintText = "arms, armour and horses obey supply and demand in towns and castles: a full stall sells cheap, an empty one dear - for buying AND selling, you and the AI alike (the item's own worth is untouched)")]
        [SettingPropertyGroup("Supply and demand")]
        public bool SupplyDemandEnabled { get; set; } = true;

        [SettingPropertyFloatingInteger("Supply Demand Base", 0.00f, 16.00f, "0.00", HintText = "pieces of one kind (type and tier) a town of reference prosperity wants on its stalls; fewer for higher tiers")]
        [SettingPropertyGroup("Supply and demand")]
        public float SupplyDemandBase { get; set; } = 4f;

        [SettingPropertyFloatingInteger("Supply Demand Ref Prosperity", 0.00f, 12000.00f, "0.00", HintText = "prosperity at which a town wants exactly the base amount (0.3x to 3x around it; castles half)")]
        [SettingPropertyGroup("Supply and demand")]
        public float SupplyDemandRefProsperity { get; set; } = 3000f;

        [SettingPropertyFloatingInteger("Supply Demand Elasticity", 0.00f, 2.00f, "0.00", HintText = "how hard the price reacts: (wanted + 1) / (on the stall + 1) raised to this power")]
        [SettingPropertyGroup("Supply and demand")]
        public float SupplyDemandElasticity { get; set; } = 0.5f;

        [SettingPropertyFloatingInteger("Supply Demand Min Factor", 0.00f, 1.00f, "0.00", HintText = "a glutted stall never pays less than this share of the normal price")]
        [SettingPropertyGroup("Supply and demand")]
        public float SupplyDemandMinFactor { get; set; } = 0.25f;

        [SettingPropertyBool("Retail From Worth", HintText = "the buying price of arms, armour and horses at a market is their worth (with condition) times supply and demand times the merchant's markup - no other mod's surcharges")]
        [SettingPropertyGroup("Supply and demand")]
        public bool RetailFromWorth { get; set; } = true;

        [SettingPropertyFloatingInteger("Retail Markup Percent", 0.00f, 40.00f, "0.00", HintText = "the merchant's markup on what a piece is worth")]
        [SettingPropertyGroup("Supply and demand")]
        public float RetailMarkupPercent { get; set; } = 10f;

        [SettingPropertyFloatingInteger("Supply Demand Max Factor", 0.00f, 16.00f, "0.00", HintText = "a starved stall never charges more than this many times the normal price (armour doubled and trebled in wartime shortages)")]
        [SettingPropertyGroup("Supply and demand")]
        public float SupplyDemandMaxFactor { get; set; } = 4f;

        [SettingPropertyFloatingInteger("Supply Demand Order Weight", 0.00f, 4.00f, "0.00", HintText = "each unmet request (a volunteer who found no armour, a lord who found nothing for his men) adds this much to the town's demand for that kind of piece")]
        [SettingPropertyGroup("Supply and demand")]
        public float SupplyDemandOrderWeight { get; set; } = 1f;

        [SettingPropertyFloatingInteger("Supply Demand Order Decay", 0.00f, 1.00f, "0.00", HintText = "share of open requests forgotten each day")]
        [SettingPropertyGroup("Supply and demand")]
        public float SupplyDemandOrderDecay { get; set; } = 0.15f;

        [SettingPropertyFloatingInteger("Supply Demand Order Cap", 0.00f, 240.00f, "0.00", HintText = "at most this many open requests per kind of piece in one town")]
        [SettingPropertyGroup("Supply and demand")]
        public float SupplyDemandOrderCap { get; set; } = 60f;

        [SettingPropertyInteger("Supply Demand Order Repeat Days", 0, 28, "0", HintText = "one buyer (a garrison, a lord, a notable) places an order for the same kind of gear in a town at most once in this many days - an order is a need, not a count of attempts")]
        [SettingPropertyGroup("Supply and demand")]
        public int SupplyDemandOrderRepeatDays { get; set; } = 7;

        [SettingPropertyFloatingInteger("Supply Demand Trade Percent", 0.00f, 60.00f, "0.00", HintText = "each day traders carry this % of a stall's surplus to the nearest town or castle that lacks it - nothing vanishes, the buyer pays")]
        [SettingPropertyGroup("Supply and demand")]
        public float SupplyDemandTradePercent { get; set; } = 15f;

        [SettingPropertyFloatingInteger("Supply Demand Trade Range", 0.00f, 1000.00f, "0.00", HintText = "how far (map distance) traders will haul arms to a market that lacks them")]
        [SettingPropertyGroup("Supply and demand")]
        public float SupplyDemandTradeRange { get; set; } = 250f;

        [SettingPropertyFloatingInteger("Supply Demand Trade Price Percent", 0.00f, 200.00f, "0.00", HintText = "wholesale price between towns, as % of worth times the glutted source's price factor")]
        [SettingPropertyGroup("Supply and demand")]
        public float SupplyDemandTradePricePercent { get; set; } = 50f;

        [SettingPropertyBool("Market Glut Enabled", HintText = "a merchant needs only so many of one thing: each extra piece of a type you sell him fetches less. With supply and demand on (and One Scrap Floor) it steps aside - the stall's own supply and demand prices the glut and Min Sell Percent Of Value is the only floor")]
        [SettingPropertyGroup("The glutted market")]
        public bool MarketGlutEnabled { get; set; } = true;

        [SettingPropertyFloatingInteger("Market Glut Start Percent", 0.00f, 20.00f, "0.00", HintText = "the FLOOR for the first piece: pays at least this % of value - a better trade rate (say 8%) stands as is")]
        [SettingPropertyGroup("The glutted market")]
        public float MarketGlutStartPercent { get; set; } = 5f;

        [SettingPropertyFloatingInteger("Market Glut Drop P P", 0.00f, 1.00f, "0.00", HintText = "each further piece of that type knocks this many percentage points off YOUR rate")]
        [SettingPropertyGroup("The glutted market")]
        public float MarketGlutDropPP { get; set; } = 0.25f;

        [SettingPropertyFloatingInteger("Market Glut Min Percent", 0.00f, 4.00f, "0.00", HintText = "the rate never falls below this % of the item's value")]
        [SettingPropertyGroup("The glutted market")]
        public float MarketGlutMinPercent { get; set; } = 1f;

        [SettingPropertyFloatingInteger("Market Glut Recover Per Day", 0.00f, 8.00f, "0.00", HintText = "the market digests this many pieces of each type a day - come back later and prices breathe again")]
        [SettingPropertyGroup("The glutted market")]
        public float MarketGlutRecoverPerDay { get; set; } = 2f;

        [SettingPropertyBool("Night Rest Enabled", HintText = "the men must sleep: quiet night hours in camp or under a roof, or the column grows weary")]
        [SettingPropertyGroup("A night's rest")]
        public bool NightRestEnabled { get; set; } = true;

        [SettingPropertyInteger("Camp Background", 0, 10, "0", HintText = "picture behind the camp menu: 0 = your culture's riders (old), 1 = an army camp before the walls, 2-10 = Realm of Thrones pictures of army life - try them and keep the one you like")]
        [SettingPropertyGroup("A night's rest")]
        public int CampBackground { get; set; } = 1;

        [SettingPropertyBool("Map Clock Enabled", HintText = "the hour of the day next to the date on the map bar ('Day 12 of Summer, 299 AC - 14:30')")]
        [SettingPropertyGroup("A night's rest")]
        public bool MapClockEnabled { get; set; } = true;

        [SettingPropertyFloatingInteger("Fast Forward Multiplier", 0.00f, 32.00f, "0.00", HintText = "how fast the fast-forward button runs (vanilla 4) - a 364-day year and slow marches need a quicker clock")]
        [SettingPropertyGroup("A night's rest")]
        public float FastForwardMultiplier { get; set; } = 8f;

        [SettingPropertyFloatingInteger("Sleep Hours Needed", 0.00f, 24.00f, "0.00", HintText = "base hours of sleep per day; sleep debt adds interest on top (1 night owed: +3h, 2: +9h, 3: +15h) and only the full sum clears it")]
        [SettingPropertyGroup("A night's rest")]
        public float SleepHoursNeeded { get; set; } = 6f;

        [SettingPropertyFloatingInteger("Day Rest Factor", 0.00f, 2.40f, "0.00", HintText = "sleep by daylight counts at this rate (camp noise, heat, light) - night hours count in full")]
        [SettingPropertyGroup("A night's rest")]
        public float DayRestFactor { get; set; } = 0.6f;

        [SettingPropertyBool("Quick Camp Key", HintText = "press O on the campaign map to pitch the BannerKings camp on the spot")]
        [SettingPropertyGroup("A night's rest")]
        public bool QuickCampKey { get; set; } = true;

        [SettingPropertyBool("Sleep At Sea Free", HintText = "crews sleep in watches - sailing through the night builds no sleep debt")]
        [SettingPropertyGroup("A night's rest")]
        public bool SleepAtSeaFree { get; set; } = true;

        [SettingPropertyBool("Ai Camps At Night", HintText = "the world sleeps too: lord parties and caravans halt for the night (camp hours) unless chased or in action")]
        [SettingPropertyGroup("A night's rest")]
        public bool AiCampsAtNight { get; set; } = true;

        [SettingPropertyInteger("Camp Start Hour", 0, 23, "0", HintText = "hour the world makes camp (lords, armies, caravans, day-hunting bands, your nightfall prompt); 0 and 6 = midnight to six; equal hours = no camp")]
        [SettingPropertyGroup("A night's rest")]
        public int CampStartHour { get; set; } = 0;

        [SettingPropertyInteger("Camp End Hour", 0, 23, "0", HintText = "hour the world breaks camp and marches on; 0 and 6 = midnight to six; equal hours = no camp (your own sleep then still settles at 6)")]
        [SettingPropertyGroup("A night's rest")]
        public int CampEndHour { get; set; } = 6;

        [SettingPropertyBool("Ai Bandits Camp Too", HintText = "brigands sleep as well - hideout by day, their own fire in the field by night")]
        [SettingPropertyGroup("A night's rest")]
        public bool AiBanditsCampToo { get; set; } = false;

        [SettingPropertyInteger("Ai Tent Cap", 0, 240, "0", HintText = "at most this many AI camps get a tent icon on the map - the rest still sleep, just without the picture")]
        [SettingPropertyGroup("A night's rest")]
        public int AiTentCap { get; set; } = 60;

        [SettingPropertyFloatingInteger("Ai Tent Radius", 0.00f, 400.00f, "0.00", HintText = "tent icons appear only this close to your party - the world beyond still sleeps, just without the picture")]
        [SettingPropertyGroup("A night's rest")]
        public float AiTentRadius { get; set; } = 100f;

        [SettingPropertyInteger("Ai Camp Skip Percent", 0, 60, "0", HintText = "this share of lord columns press on through any given night - not everyone pitches camp; army leaders are spared this roll while Army Leaders Always Camp is on")]
        [SettingPropertyGroup("A night's rest")]
        public int AiCampSkipPercent { get; set; } = 15;

        [SettingPropertyBool("Army Leaders Always Camp", HintText = "army leaders never skip the night camp - the whole host halts (only a nearby enemy, a chase or a siege keeps it marching); off = they roll the skip share like any lord")]
        [SettingPropertyGroup("A night's rest")]
        public bool ArmyLeadersAlwaysCamp { get; set; } = true;

        [SettingPropertyBool("Bandits Rest By Day", HintText = "every band has a nature: three in four are night hunters (lie low 10-16), one in four hunts by day and beds down at night (camp hours)")]
        [SettingPropertyGroup("A night's rest")]
        public bool BanditsRestByDay { get; set; } = true;

        [SettingPropertyFloatingInteger("Ai Nights Awake In Chase", 0.00f, 4.00f, "0.00", HintText = "days a chasing or fleeing party may push on without sleep before it drops anyway (not in force yet: chasing and fleeing parties never sleep)")]
        [SettingPropertyGroup("A night's rest")]
        public float AiNightsAwakeInChase { get; set; } = 1f;

        [SettingPropertyFloatingInteger("Ai Camp Danger Radius", 0.00f, 24.00f, "0.00", HintText = "a hostile party this close keeps them marching - pursuit knows no bedtime")]
        [SettingPropertyGroup("A night's rest")]
        public float AiCampDangerRadius { get; set; } = 6f;

        [SettingPropertyBool("Camp Tent Icon", HintText = "pitched camps show a tent on the map (yours and theirs)")]
        [SettingPropertyGroup("A night's rest")]
        public bool CampTentIcon { get; set; } = true;

        [SettingPropertyBool("Course Plotter Enabled", HintText = "clicking a destination reports the route: kilometres, hours in the saddle and days on the road, and flags a settlement target on the map")]
        [SettingPropertyGroup("A night's rest")]
        public bool CoursePlotterEnabled { get; set; } = true;

        [SettingPropertyBool("Nightfall Prompt Enabled", HintText = "when the camp hour strikes a marching column is asked to make camp; the popup lets you set always-camp or never-ask (choice lives in the save)")]
        [SettingPropertyGroup("A night's rest")]
        public bool NightfallPromptEnabled { get; set; } = true;

        [SettingPropertyBool("Anvil Shift Enabled", HintText = "waiting at the forge runs in day shifts: after AnvilShiftHours of work the smith beds down for his needed sleep (6h, more with sleep debt), then returns to the hammer")]
        [SettingPropertyGroup("A night's rest")]
        public bool AnvilShiftEnabled { get; set; } = true;

        [SettingPropertyFloatingInteger("Anvil Shift Hours", 0.00f, 72.00f, "0.00", HintText = "hours of work at the anvil before the smith must sleep")]
        [SettingPropertyGroup("A night's rest")]
        public float AnvilShiftHours { get; set; } = 18f;

        [SettingPropertyBool("Workshop Night Rest", HintText = "the apprentices sleep too: forge projects make no progress between 23:00 and 5:00 - a day of work is a day at the anvil")]
        [SettingPropertyGroup("A night's rest")]
        public bool WorkshopNightRest { get; set; } = true;

        [SettingPropertyFloatingInteger("Kg Per Athletics Point", 0.00f, 1.00f, "0.00", HintText = "Weight Law: kilograms of armour one Athletics point can carry (difficulty = weight / this); applied at session start")]
        [SettingPropertyGroup("A night's rest")]
        public float KgPerAthleticsPoint { get; set; } = 0.25f;

        [SettingPropertyInteger("Armor Athletics Per Tier", 0, 140, "0", HintText = "Armour Tier Law: any piece of armour (helmet, body, boots, gloves, cape) needs at least (tier - 1) x this Athletics, whatever its weight says - tier 4 boots want 105, so a low-Athletics bandit never 'qualifies' for them; 0 turns the law off. Applied at session start, after the Weight Law (the higher of the two wins)")]
        [SettingPropertyGroup("A night's rest")]
        public int ArmorAthleticsPerTier { get; set; } = 35;

        [SettingPropertyBool("Hit Scribe Enabled", HintText = "battle log: every missile hit written to Armoury.log (weapon, victim, body part, damage, armor absorbed) - capped per mission")]
        [SettingPropertyGroup("A night's rest")]
        public bool HitScribeEnabled { get; set; } = true;

        [SettingPropertyBool("Armor Sanity Enabled", HintText = "Armour Sense Law: outliers get levelled to the norm of their type and tier - no more 4 kg chaps outarmouring plate; the world's balance stays put")]
        [SettingPropertyGroup("A night's rest")]
        public bool ArmorSanityEnabled { get; set; } = true;

        [SettingPropertyFloatingInteger("Armor Outlier Percentile", 0.00f, 300.00f, "0.00", HintText = "Armour Sense Law: the norm is this percentile of total protection within (type, tier)")]
        [SettingPropertyGroup("A night's rest")]
        public float ArmorOutlierPercentile { get; set; } = 75f;

        [SettingPropertyFloatingInteger("Armor Outlier Tolerance", 0.00f, 5.20f, "0.00", HintText = "Armour Sense Law: pieces above norm x this get trimmed down to it")]
        [SettingPropertyGroup("A night's rest")]
        public float ArmorOutlierTolerance { get; set; } = 1.30f;

        [SettingPropertyBool("Camp Battle Props Enabled", HintText = "EXPERIMENTAL: attacked while encamped, the field battle gets your camp dressed on it - tents, fire, torches around your line (unknown prefabs are skipped and logged)")]
        [SettingPropertyGroup("A night's rest")]
        public bool CampBattlePropsEnabled { get; set; } = false;

        [SettingPropertyBool("Hideout Alarm Enabled", HintText = "a fight in a hideout wakes the camp: bandits within earshot come running - no more men ignoring a brawl ten paces away")]
        [SettingPropertyGroup("A night's rest")]
        public bool HideoutAlarmEnabled { get; set; } = true;

        [SettingPropertyFloatingInteger("Hideout Alarm Scream Radius", 0.00f, 160.00f, "0.00", HintText = "a wounded man's cry and the ring of steel carry this many meters - bandits inside come at you, further ones sleep on")]
        [SettingPropertyGroup("A night's rest")]
        public float HideoutAlarmScreamRadius { get; set; } = 40f;

        [SettingPropertyFloatingInteger("Hideout Alarm Witness Radius", 0.00f, 48.00f, "0.00", HintText = "a CLEAN one-blow kill alarms only enemies this close to the body - no witnesses, no alarm")]
        [SettingPropertyGroup("A night's rest")]
        public float HideoutAlarmWitnessRadius { get; set; } = 12f;

        [SettingPropertyBool("Hideout Armoury Gear", HintText = "your men storm hideouts in their ARMOURY kit, same as field battles (opens the gate DTE leaves shut in regular hideouts)")]
        [SettingPropertyGroup("A night's rest")]
        public bool HideoutArmouryGear { get; set; } = true;

        [SettingPropertyBool("Hideout Noise Enabled", HintText = "running feet carry in a hideout: bandits within earshot come alarmed - WALK (Left Ctrl) to move quietly")]
        [SettingPropertyGroup("A night's rest")]
        public bool HideoutNoiseEnabled { get; set; } = true;

        [SettingPropertyFloatingInteger("Hideout Hear Day", 0.00f, 40.00f, "0.00", HintText = "meters a RUNNING man is heard by day - the camp's own bustle masks a lot")]
        [SettingPropertyGroup("A night's rest")]
        public float HideoutHearDay { get; set; } = 10f;

        [SettingPropertyFloatingInteger("Hideout Hear Night", 0.00f, 80.00f, "0.00", HintText = "meters a RUNNING man is heard at night - silence carries sound, so sneak at a walk")]
        [SettingPropertyGroup("A night's rest")]
        public float HideoutHearNight { get; set; } = 20f;

        [SettingPropertyBool("Hideout Alarm Relay", HintText = "a woken man shouts too: the alarm leaps man to man while each next stands within scream range of the last - never across the whole camp at once")]
        [SettingPropertyGroup("A night's rest")]
        public bool HideoutAlarmRelay { get; set; } = true;

        [SettingPropertyFloatingInteger("Hideout Noise Per Armor Kg", 0.00f, 1.00f, "0.00", HintText = "every kilogram of worn armour adds this many meters to a RUNNING man's noise - plate thunders, leather whispers")]
        [SettingPropertyGroup("A night's rest")]
        public float HideoutNoisePerArmorKg { get; set; } = 0.2f;

        [SettingPropertyBool("Hideout Alarm Voice", HintText = "the alarm is HEARD: one man in every ring of the relay lets out a real battle yell (the game's own voice line)")]
        [SettingPropertyGroup("A night's rest")]
        public bool HideoutAlarmVoice { get; set; } = true;

        [SettingPropertyBool("Sight Cycle Enabled", HintText = "eyes follow the sun: every party sees further by day and shorter after dark")]
        [SettingPropertyGroup("A night's rest")]
        public bool SightCycleEnabled { get; set; } = true;

        [SettingPropertyFloatingInteger("Day Sight Factor", 0.00f, 4.60f, "0.00", HintText = "spotting range in daylight, times this")]
        [SettingPropertyGroup("A night's rest")]
        public float DaySightFactor { get; set; } = 1.15f;

        [SettingPropertyFloatingInteger("Night Sight Factor", 0.00f, 2.60f, "0.00", HintText = "spotting range at night - the cut is mild, for you HEAR more after dark: a marching column is loud")]
        [SettingPropertyGroup("A night's rest")]
        public float NightSightFactor { get; set; } = 0.65f;

        [SettingPropertyBool("Combat Xp Fix Enabled", HintText = "RBM pays the same XP for an arena tap and a battlefield kill - restore the proportions")]
        [SettingPropertyGroup("The worth of a lesson")]
        public bool CombatXpFixEnabled { get; set; } = true;

        [SettingPropertyFloatingInteger("Arena Xp Percent", 0.00f, 80.00f, "0.00", HintText = "practice bouts teach this share of the battle rate (vanilla 6, RBM 100)")]
        [SettingPropertyGroup("The worth of a lesson")]
        public float ArenaXpPercent { get; set; } = 20f;

        [SettingPropertyFloatingInteger("Tournament Xp Percent", 0.00f, 200.00f, "0.00", HintText = "tournament bouts teach this share of the battle rate (vanilla 33, RBM 100)")]
        [SettingPropertyGroup("The worth of a lesson")]
        public float TournamentXpPercent { get; set; } = 50f;

        [SettingPropertyBool("Battle Xp Scales With Damage", HintText = "real battles: XP follows the damage dealt and a kill pays double - not a flat fee per swing")]
        [SettingPropertyGroup("The worth of a lesson")]
        public bool BattleXpScalesWithDamage { get; set; } = true;

        [SettingPropertyBool("Troop Mend Enabled", HintText = "the town smith mends the worn gear on the troop armoury's racks - his apprentices work at a bulk rate")]
        [SettingPropertyGroup("The men's gear")]
        public bool TroopMendEnabled { get; set; } = true;

        [SettingPropertyFloatingInteger("Troop Mend Wreck Share", 0.00f, 1.00f, "0.00", HintText = "with Smith Mend From Market off: mending a piece on the men's racks costs this share of the worth it has lost (60% condition = 4% of worth); wrecks themselves are not mended while Wrecks To Scrap is on (off: a WRECK at 1% costs this share of its worth)")]
        [SettingPropertyGroup("The men's gear")]
        public float TroopMendWreckShare { get; set; } = 0.10f;

        [SettingPropertyFloatingInteger("Troop Mend Bulk Discount P P", 0.00f, 2.00f, "0.00", HintText = "every piece on the job knocks this many percent off the whole bill - the more racks, the better the rate")]
        [SettingPropertyGroup("The men's gear")]
        public float TroopMendBulkDiscountPP { get; set; } = 0.5f;

        [SettingPropertyFloatingInteger("Troop Mend Bulk Discount Max", 0.00f, 120.00f, "0.00", HintText = "the bulk discount never grows past this percent")]
        [SettingPropertyGroup("The men's gear")]
        public float TroopMendBulkDiscountMax { get; set; } = 30f;

        [SettingPropertyFloatingInteger("Troop Mend Max Hours", 0.00f, 96.00f, "0.00", HintText = "the whole job never takes longer than this - the smith puts every hand he has on it")]
        [SettingPropertyGroup("The men's gear")]
        public float TroopMendMaxHours { get; set; } = 24f;

        [SettingPropertyBool("Troop Order Enabled", HintText = "order missing kit for the men from the town smith - plain pieces of the tier you ask, straight onto the racks")]
        [SettingPropertyGroup("The men's gear")]
        public bool TroopOrderEnabled { get; set; } = true;

        [SettingPropertyFloatingInteger("Troop Order Markup", 0.00f, 4.60f, "0.00", HintText = "the smith's fee: each procured piece costs its market worth times this (not used while Troop Order From Shelf is on - then the shelf price plus the smith's legwork)")]
        [SettingPropertyGroup("The men's gear")]
        public float TroopOrderMarkup { get; set; } = 1.15f;

        [SettingPropertyBool("Troop Order From Shelf", HintText = "the smith procures the men's kit off THIS town's stalls: the cheapest sound piece of the type and tier you ask that is really on sale here, each at the stall's own buying price (the price your men's purse and the lords pay - every further piece dearer as the stall empties), plus his legwork: a fifth of a craftsman's day a piece (3 d a day, as dear as the town is prosperous), all paid into the town's coffers; what the stalls lack is passed to the town's workshops as an order and costs you nothing (off = as before: the cheapest such piece anywhere in the world at its worth times the markup, delivered only if this stall happens to hold it)")]
        [SettingPropertyGroup("The men's gear")]
        public bool TroopOrderFromShelf { get; set; } = true;

        [SettingPropertyFloatingInteger("Smith Repair Hours Per Piece", 0.00f, 6.00f, "0.00", HintText = "hours the smith needs per worn piece of your harness")]
        [SettingPropertyGroup("Time at the forge")]
        public float SmithRepairHoursPerPiece { get; set; } = 1.5f;

        [SettingPropertyFloatingInteger("Self Repair Hours Per Piece", 0.00f, 10.00f, "0.00", HintText = "hours you need per piece working the anvil yourself")]
        [SettingPropertyGroup("Time at the forge")]
        public float SelfRepairHoursPerPiece { get; set; } = 2.5f;

        [SettingPropertyFloatingInteger("Mend Loot Hours Per Piece", 0.00f, 2.40f, "0.00", HintText = "hours per battle-worn piece from the bags")]
        [SettingPropertyGroup("Time at the forge")]
        public float MendLootHoursPerPiece { get; set; } = 0.6f;

        [SettingPropertyFloatingInteger("Mend Material Max Share", 0.00f, 1.00f, "0.00", HintText = "mending is NOT forging anew: even a wreck (1%) takes at most this share of the full recipe's materials - and of the days of work that made the piece")]
        [SettingPropertyGroup("Time at the forge")]
        public float MendMaterialMaxShare { get; set; } = 0.20f;

        [SettingPropertyBool("Smith Mend From Market", HintText = "the town smiths' rule for every repair in a town (the mending bench, the quartermaster, your men, the lords' men): the work is paid like the making of the piece - its share of the days a master spent forging it, at the master's historical day wage, as dear as the town is prosperous; the mending bench (and, with Mend Material Men And Lords, your men's and the lords' repairs) also charges the materials they take from this market at its prices - iron (crude iron, scrap from wrecks or ore), wood, leather, linen or wool, more the worse the piece; with no such materials on the market the piece waits; wrecks (Mangled, or worn to a tenth of their worth or less - see Wrecks To Scrap) are not restored for coin - mend them yourself with your own materials, or melt them down (off = as before: the smith's price only, no materials, wrecks restored)")]
        [SettingPropertyGroup("Time at the forge")]
        public bool SmithMendFromMarket { get; set; } = true;

        [SettingPropertyBool("Wrecks To Scrap", HintText = "wrecks go to the scrap heap: no town smith restores a wreck for coin on any road - not the mending bench, not 'Pick a piece', not the men's racks (with Smith Mend From Market on or off), not your harness, not your men's or the lords' repairs. A wreck is Mangled loot or any piece worn to a tenth of its worth or less (on your back: worn to 10% or less); it stays a wreck - melt it down (the smiths also take wrecks off the market as scrap iron for their mending), or mend it at your own anvil with your own materials (off = as before: wrecks below a tenth are refused, a piece at exactly a tenth is restored, and with Smith Mend From Market off the men's racks restore wrecks for Troop Mend Wreck Share of their worth)")]
        [SettingPropertyGroup("Time at the forge")]
        public bool WrecksToScrap { get; set; } = true;

        [SettingPropertyBool("Mend Metal By Kind", HintText = "town smiths mending a piece take only the new iron that mending really needs, not a fixed share of a new piece: rivets and a few plates for plate (1% of its iron Plundered, 2% Damaged, 4% Battered), buckles and studs for leather and cloth (2/3/6%), almost none for blades (0/0.5/1.5%) and spear heads (0/0.5/2%), more for shield rims and bosses (4/8/12%) and crossbow locks (3/6/11%); other states (e.g. Dented, Rusty) fall between these points by how much of the piece is lost; the forge fuel follows the iron; mail keeps the old rule (badly cut mail needs many new rings); the work, the wood, leather and cloth are paid as before (off = iron and forge fuel at Mend Material Max Share times the damage, as before: 9/12/15% of the recipe Plundered/Damaged/Battered)")]
        [SettingPropertyGroup("Time at the forge")]
        public bool MendMetalByKind { get; set; } = true;

        [SettingPropertyBool("Mend Material Men And Lords", HintText = "with Smith Mend From Market on: the repairs your men pay for from their own purse each hour in a town, and the repairs the lords' men pay for, also take the materials from this market at its prices - the same rule and the same price as the mending bench (iron, wood, leather, linen or wool, more the worse the piece); a piece the market has no material for waits, the next goes ahead; off = your men's and the lords' repairs are the work only, as before")]
        [SettingPropertyGroup("Time at the forge")]
        public bool MendMaterialMenAndLords { get; set; } = true;

        [SettingPropertyBool("Take Apart Enabled", HintText = "rozlozenie gotowej rzeczy na czesci, zeby zdjac z niej wzor")]
        [SettingPropertyGroup("Time at the forge")]
        public bool TakeApartEnabled { get; set; } = true;

        [SettingPropertyFloatingInteger("Take Apart Base Chance", 0.00f, 2.40f, "0.00", HintText = "szansa odczytania wzoru przy DOKLADNIE wymaganej Smithing")]
        [SettingPropertyGroup("Time at the forge")]
        public float TakeApartBaseChance { get; set; } = 0.6f;

        [SettingPropertyFloatingInteger("Take Apart Skill Span", 0.00f, 1200.00f, "0.00", HintText = "ile punktow Smithing daje pelny przeskok szansy")]
        [SettingPropertyGroup("Time at the forge")]
        public float TakeApartSkillSpan { get; set; } = 300f;

        [SettingPropertyFloatingInteger("Take Apart Salvage", 0.00f, 1.00f, "0.00", HintText = "ile materialu wraca (0 = nic, 1 = tyle co z tygla)")]
        [SettingPropertyGroup("Time at the forge")]
        public float TakeApartSalvage { get; set; } = 0f;

        [SettingPropertyFloatingInteger("Smelt Hours Per Tier", 0.00f, 2.00f, "0.00", HintText = "crucible hours per tier of the broken-down piece")]
        [SettingPropertyGroup("Time at the forge")]
        public float SmeltHoursPerTier { get; set; } = 0.5f;

        [SettingPropertyBool("Companion Helper Enabled", HintText = "a companion may work the bellows for you")]
        [SettingPropertyGroup("Companion at the bellows")]
        public bool CompanionHelperEnabled { get; set; } = true;

        [SettingPropertyFloatingInteger("Helper Stamina Relief", 0.00f, 1.60f, "0.00", HintText = "the most they can spare your arms")]
        [SettingPropertyGroup("Companion at the bellows")]
        public float HelperStaminaRelief { get; set; } = 0.4f;

        [SettingPropertyFloatingInteger("Helper Time Relief", 0.00f, 1.20f, "0.00", HintText = "the most they can shorten the work")]
        [SettingPropertyGroup("Companion at the bellows")]
        public float HelperTimeRelief { get; set; } = 0.3f;

        [SettingPropertyInteger("Helper Skill For Full Relief", 0, 600, "0", HintText = "the Smithing at which a helper gives their full worth")]
        [SettingPropertyGroup("Companion at the bellows")]
        public int HelperSkillForFullRelief { get; set; } = 150;

        [SettingPropertyInteger("Forge Parts Free Below Tier", 0, 10, "0", HintText = "only crafting parts BELOW this tier are free at the forge from the start; everything above is unlocked by forging and smelting, lowest tier first (2 = only tier 1 free, 5 = named lore blades locked only, 7 = gate off). A safety floor always keeps one band of parts per required slot, so the Craft button never dies. Applied when a session loads")]
        [SettingPropertyGroup("The forge gate")]
        public int ForgePartsFreeBelowTier { get; set; } = 2;

        [SettingPropertyBool("Hideout Flag Enabled", HintText = "a freshly spotted hideout gets a flag on the map and a report: distance, direction, nearest settlement, who nests there")]
        [SettingPropertyGroup("Hideout spotted")]
        public bool HideoutFlagEnabled { get; set; } = true;

        [SettingPropertyInteger("Hideout Flag Days", 0, 12, "0", HintText = "how many days the flag stays on the map")]
        [SettingPropertyGroup("Hideout spotted")]
        public int HideoutFlagDays { get; set; } = 3;

        [SettingPropertyBool("House Levies Enabled", HintText = "in settlements owned by a house with its own troop line (Blackwood, Tully, Karstark, Lannister...) the NOBLE volunteers the game already offers are that house's men of the same tier - same rule and frequency as noble recruits anywhere, no extra dice")]
        [SettingPropertyGroup("House levies")]
        public bool HouseLeviesEnabled { get; set; } = true;

        [SettingPropertyBool("Northern Fare Enabled", HintText = "vineyards in the North and at the Wall (Farsfog, Tumbledown, Olden Oak, Queenscrown) become fishers, cattle and swine farms - grapes do not grow in snow; wine presses in Winterfell and Castle Black turn into breweries. Applied at session start")]
        [SettingPropertyGroup("Northern fare")]
        public bool NorthernFareEnabled { get; set; } = true;

        [SettingPropertyBool("Crop Climate Filter", HintText = "papyrus, a reed of hot marshes, grows only on grain farms of the hot south (Dorne, Ghis, Qarth, Volantis, Lys, Myr, Tyrosh, Valyria, the Summer Isles); Banner Kings gave it to every grain farm, even at the Wall - elsewhere the farm no longer yields it (grain output unchanged)")]
        [SettingPropertyGroup("Northern fare")]
        public bool CropClimateFilter { get; set; } = true;

        [SettingPropertyBool("Woodlot By Climate", HintText = "the village woodlot (Village Woodlot Loads) fells by climate: deserts 0.3, Dothraki steppe 0.5, Mediterranean lands 0.8, forest lands 1.2, the rest 1.0 - scaled so the world as a whole fells as much timber as before")]
        [SettingPropertyGroup("Northern fare")]
        public bool WoodlotByClimate { get; set; } = true;

        [SettingPropertyBool("Village Climate Fix", HintText = "12 villages whose main produce cannot grow where they stand get one that can (cotton at the Wall, in Braavos, in the Vale mountains and in Sarnor, a vineyard in Lorath, dates on Tarth), and 5 warm villages (Qarth, Volantis, Lys, Tyrosh) take up cotton so the world keeps some; grain farms beyond the Wall are left as they are. Applied at session start, nothing is written to the save (off = the map's own village types after the next load)")]
        [SettingPropertyGroup("Northern fare")]
        public bool VillageClimateFix { get; set; } = true;

        [SettingPropertyBool("Bandit Cheer Enabled", HintText = "villages near your victory over bandits thank you - relations with their notables improve")]
        [SettingPropertyGroup("Grateful villages")]
        public bool BanditCheerEnabled { get; set; } = true;

        [SettingPropertyInteger("Bandit Cheer Radius", 0, 200, "0", HintText = "how far from the battlefield a village still hears the good news (map distance)")]
        [SettingPropertyGroup("Grateful villages")]
        public int BanditCheerRadius { get; set; } = 50;

        [SettingPropertyInteger("Bandit Cheer Relation", 0, 10, "0", HintText = "relation gained with each notable of those villages")]
        [SettingPropertyGroup("Grateful villages")]
        public int BanditCheerRelation { get; set; } = 2;

        [SettingPropertyBool("Log Enabled", HintText = "write a log file in the module folder")]
        [SettingPropertyGroup("Grateful villages")]
        public bool LogEnabled { get; set; } = true;

        [SettingPropertyBool("Map Villages Enabled", HintText = "named villages on the campaign map between the game's own villages, castles and towns - each one a cluster of its district's settlements, standing where a village had reason to stand (bridge, ford, crossroads, road, river, coast); off = none drawn. Nothing is written to the save")]
        [SettingPropertyGroup("Map villages")]
        public bool MapVillagesEnabled { get; set; } = true;

        [SettingPropertyFloatingInteger("Map Villages Hide Above Camera Height", 0.00f, 640.00f, "0.00", HintText = "map villages vanish when the camera rises above this height - from afar only the game's villages, castles and towns remain")]
        [SettingPropertyGroup("Map villages")]
        public float MapVillagesHideAboveCameraHeight { get; set; } = 160f;

        [SettingPropertyBool("Map Village Names On Hover", HintText = "Map Village Names - hover only: point at a map village to see its name, its district, its settlements and people. Labels over burning villages come in a later update")]
        [SettingPropertyGroup("Map villages")]
        public bool MapVillageNamesOnHover { get; set; } = true;

        [SettingPropertyBool("Mill On Bank", HintText = "water mills on rivers stand on the bank, not in the stream: the whole mill village, mill and wheel included, is set half a step back from the river's edge. Mills on the sea coast are unchanged. Off = as before, the wheel a little out in the water")]
        [SettingPropertyGroup("Map villages")]
        public bool MillOnBank { get; set; } = true;

        [SettingPropertyBool("Village Tip Held By", HintText = "the map village tooltip names who holds the land, live from the game: 'A village of the Tumbledown lands, held by House Stark of Winterfell' ('your fief' for your own). Off = 'A village of the X district', as before")]
        [SettingPropertyGroup("Map villages")]
        public bool VillageTipHeldBy { get; set; } = true;

        public void ApplyTo(Settings s)
        {
            s.TidyBannerKingsArmourList = TidyBannerKingsArmourList;
            s.ForgeArmourEnabled = ForgeArmourEnabled;
            s.CraftingEnabled = CraftingEnabled;
            s.SmithingSkillPerTier = SmithingSkillPerTier;
            s.SmithingDifficultyPerTier = SmithingDifficultyPerTier;
            s.ArmourCraftLikeWeapons = ArmourCraftLikeWeapons;
            s.IronPerWeightUnit = IronPerWeightUnit;
            s.ClassCostBody = ClassCostBody;
            s.ClassCostLeg = ClassCostLeg;
            s.ClassCostHead = ClassCostHead;
            s.ClassCostHand = ClassCostHand;
            s.ClassCostCape = ClassCostCape;
            s.ClassCostHorse = ClassCostHorse;
            s.ClassCostShield = ClassCostShield;
            s.ClassCostRanged = ClassCostRanged;
            s.FiddlyStaminaBonus = FiddlyStaminaBonus;
            s.CharcoalPerIron = CharcoalPerIron;
            s.StaminaPerTier = StaminaPerTier;
            s.ForgeWorksWithoutYou = ForgeWorksWithoutYou;
            s.ForgeOneClock = ForgeOneClock;
            s.ForgeOnlyWhileThere = ForgeOnlyWhileThere;
            s.ForgeTakesTime = ForgeTakesTime;
            s.DaysPerTier = DaysPerTier;
            s.TempoHastyTime = TempoHastyTime;
            s.TempoHastyRisk = TempoHastyRisk;
            s.TempoCarefulTime = TempoCarefulTime;
            s.TempoCarefulRisk = TempoCarefulRisk;
            s.TempoCarefulQuality = TempoCarefulQuality;
            s.XpPerDayPerTier = XpPerDayPerTier;
            s.XpFullCreditMargin = XpFullCreditMargin;
            s.XpDiminishingRange = XpDiminishingRange;
            s.XpFloorFactor = XpFloorFactor;
            s.XpCapPerTier = XpCapPerTier;
            s.XpShareWhileWorking = XpShareWhileWorking;
            s.WeaponCraftingTakesTime = WeaponCraftingTakesTime;
            s.WeaponDaysPerTier = WeaponDaysPerTier;
            s.WeaponXpFromValueCapped = WeaponXpFromValueCapped;
            s.WeaponXpCapPerTier = WeaponXpCapPerTier;
            s.ArmourOrdersEnabled = ArmourOrdersEnabled;
            s.OrderOfferChance = OrderOfferChance;
            s.OrderTownCooldownDays = OrderTownCooldownDays;
            s.OrderOfferLifeDays = OrderOfferLifeDays;
            s.OrderDeadlineDays = OrderDeadlineDays;
            s.OrderPayMultiplier = OrderPayMultiplier;
            s.OrderRelationReward = OrderRelationReward;
            s.OrderMissRelationPenalty = OrderMissRelationPenalty;
            s.MaxAcceptedOrders = MaxAcceptedOrders;
            s.OrderMinTier = OrderMinTier;
            s.OrderMaxTier = OrderMaxTier;
            s.OrderMaxItemValue = OrderMaxItemValue;
            s.ForgeFeeBase = ForgeFeeBase;
            s.ForgeFeePerTier = ForgeFeePerTier;
            s.BkForgeHourlyMultiplier = BkForgeHourlyMultiplier;
            s.ForgeDayPassEnabled = ForgeDayPassEnabled;
            s.ForgeDayHours = ForgeDayHours;
            s.ForgeHireHistorical = ForgeHireHistorical;
            s.ForgeWorkNoRest = ForgeWorkNoRest;
            s.EnforceStaminaCosts = EnforceStaminaCosts;
            s.StaminaCostMessages = StaminaCostMessages;
            s.ForgeStaminaCampRate = ForgeStaminaCampRate;
            s.ForgeStaminaMarchRate = ForgeStaminaMarchRate;
            s.RefineXpCap = RefineXpCap;
            s.ArmouryProtectUsed = ArmouryProtectUsed;
            s.QuartermasterShouts = QuartermasterShouts;
            s.QuartermasterPurgeUnusable = QuartermasterPurgeUnusable;
            s.CharcoalWeight = CharcoalWeight;
            s.BkTrueMaterials = BkTrueMaterials;
            s.ArmorPointsPerMaterial = ArmorPointsPerMaterial;
            s.ArmorMaterialScale = ArmorMaterialScale;
            s.SoftMaterialPerTier = SoftMaterialPerTier;
            s.ArmorTierBonusPercent = ArmorTierBonusPercent;
            s.SelfRepairMaterialFactor = SelfRepairMaterialFactor;
            s.SelfRepairStaminaFactor = SelfRepairStaminaFactor;
            s.FailureChanceAtZeroMargin = FailureChanceAtZeroMargin;
            s.MarginForNoFailure = MarginForNoFailure;
            s.MaterialLossOnFailure = MaterialLossOnFailure;
            s.MaxItemsListed = MaxItemsListed;
            s.AllowRangedCrafting = AllowRangedCrafting;
            s.AmmoBatchStacks = AmmoBatchStacks;
            s.RangedStaminaFactor = RangedStaminaFactor;
            s.RangedHighTierCostFactor = RangedHighTierCostFactor;
            s.RangedFailureFactor = RangedFailureFactor;
            s.LegendaryValueFloor = LegendaryValueFloor;
            s.LegendaryMaterialFactor = LegendaryMaterialFactor;
            s.LegendarySkillNeeded = LegendarySkillNeeded;
            s.SmeltingReturnShare = SmeltingReturnShare;
            s.SmeltingSkillBonus = SmeltingSkillBonus;
            s.TroopWearEnabled = TroopWearEnabled;
            s.TroopWearPercent = TroopWearPercent;
            s.TroopWearPerHit = TroopWearPerHit;
            s.TroopWearPerBlock = TroopWearPerBlock;
            s.TroopWearPerStrike = TroopWearPerStrike;
            s.TroopWearPerShot = TroopWearPerShot;
            s.TroopWearBaseCasualtyShare = TroopWearBaseCasualtyShare;
            s.LootPriceFollowsCondition = LootPriceFollowsCondition;
            s.WearEnabled = WearEnabled;
            s.ShowConditionPercent = ShowConditionPercent;
            s.ConditionScalesStats = ConditionScalesStats;
            s.ConditionPenaltyMax = ConditionPenaltyMax;
            s.ConditionPenaltyExponent = ConditionPenaltyExponent;
            s.ArmorPoolMinPoints = ArmorPoolMinPoints;
            s.WearPerBattle = WearPerBattle;
            s.WearDamageFactor = WearDamageFactor;
            s.MissileArmorWearPercent = MissileArmorWearPercent;
            s.HarnessWearFactor = HarnessWearFactor;
            s.DurabilityPerArmorPoint = DurabilityPerArmorPoint;
            s.WearWeaponPerHit = WearWeaponPerHit;
            s.WearShieldFactor = WearShieldFactor;
            s.ShieldMissileGuardEnabled = ShieldMissileGuardEnabled;
            s.MissileShieldDamagePercent = MissileShieldDamagePercent;
            s.BowUsesAtTier1 = BowUsesAtTier1;
            s.BowSkillBonusPercentPerPoint = BowSkillBonusPercentPerPoint;
            s.TierDurabilityFactor = TierDurabilityFactor;
            s.ThresholdWorn = ThresholdWorn;
            s.ThresholdDamaged = ThresholdDamaged;
            s.ThresholdRuined = ThresholdRuined;
            s.RepairCostFactor = RepairCostFactor;
            s.BreakAtZeroCondition = BreakAtZeroCondition;
            s.UniqueCrownsEnabled = UniqueCrownsEnabled;
            s.UniqueCrownHeadArmor = UniqueCrownHeadArmor;
            s.BkSupplyDaysCap = BkSupplyDaysCap;
            s.BkSupplyMaxPieces = BkSupplyMaxPieces;
            s.AiStarvingBuysAnyPrice = AiStarvingBuysAnyPrice;
            s.FoodConsumptionCutPercent = FoodConsumptionCutPercent;
            s.CrossingLawEnabled = CrossingLawEnabled;
            s.CrossingLawAi = CrossingLawAi;
            s.CrossingRadius = CrossingRadius;
            s.VolunteerRegenPercent = VolunteerRegenPercent;
            s.HealingRegenPercent = HealingRegenPercent;
            s.AiHealingRegenPercent = AiHealingRegenPercent;
            s.StarvationWoundPercent = StarvationWoundPercent;
            s.AutoSortParty = AutoSortParty;
            s.MusterBookEnabled = MusterBookEnabled;
            s.CraftResultPopup = CraftResultPopup;
            s.RichQualityModifiers = RichQualityModifiers;
            s.TroopSelfMendEnabled = TroopSelfMendEnabled;
            s.MenPurseEnabled = MenPurseEnabled;
            s.SurplusKeepPercent = SurplusKeepPercent;
            s.LordLootThirdPercent = LordLootThirdPercent;
            s.AiWearEnabled = AiWearEnabled;
            s.MineWagesStayInTown = MineWagesStayInTown;
            s.WorkHoursPerManDay = WorkHoursPerManDay;
            s.TroopSelfMendPercentPerDay = TroopSelfMendPercentPerDay;
            s.TroopSkillAutoFit = TroopSkillAutoFit;
            s.SkillsDecideEnabled = SkillsDecideEnabled;
            s.WeaponSkillPerTier = WeaponSkillPerTier;
            s.ElephantQuarantineEnabled = ElephantQuarantineEnabled;
            s.HideoutPurgeEnabled = HideoutPurgeEnabled;
            s.HideoutGoldBase = HideoutGoldBase;
            s.HideoutGoldPerBand = HideoutGoldPerBand;
            s.HideoutRenown = HideoutRenown;
            s.HideoutRepMax = HideoutRepMax;
            s.HideoutRepRadius = HideoutRepRadius;
            s.HideoutSearchSoloHours = HideoutSearchSoloHours;
            s.HideoutSearchPerManHours = HideoutSearchPerManHours;
            s.HideoutSearchMinHours = HideoutSearchMinHours;
            s.HideoutReprisalEnabled = HideoutReprisalEnabled;
            s.HideoutReprisalRadius = HideoutReprisalRadius;
            s.HideoutReprisalFleeOdds = HideoutReprisalFleeOdds;
            s.HideoutReprisalHours = HideoutReprisalHours;
            s.LootArrivesWorn = LootArrivesWorn;
            s.LootWearBase = LootWearBase;
            s.LootWearSpread = LootWearSpread;
            s.CaptiveSpoilsEnabled = CaptiveSpoilsEnabled;
            s.CaptiveSpoilsIncludeMounts = CaptiveSpoilsIncludeMounts;
            s.CaptiveRagsPreview = CaptiveRagsPreview;
            s.BattlefieldLawEnabled = BattlefieldLawEnabled;
            s.LootArrivesBattleWorn = LootArrivesBattleWorn;
            s.SimBattleFullDrop = SimBattleFullDrop;
            s.PlayerLootSharePercent = PlayerLootSharePercent;
            s.WreckSalvageEnabled = WreckSalvageEnabled;
            s.LootMinConditionPercent = LootMinConditionPercent;
            s.LegendaryLootValueFloor = LegendaryLootValueFloor;
            s.SpoilsClanRealSoldiers = SpoilsClanRealSoldiers;
            s.SpoilsNoAutoSale = SpoilsNoAutoSale;
            s.SpoilsNoFreeGold = SpoilsNoFreeGold;
            s.SpoilsQuartermasterRepair = SpoilsQuartermasterRepair;
            s.LivingEconomySealed = LivingEconomySealed;
            s.PlagueSparesYourMen = PlagueSparesYourMen;
            s.PlagueShieldLogEvery = PlagueShieldLogEvery;
            s.DesertionLawEnabled = DesertionLawEnabled;
            s.DesertionMoraleTier1 = DesertionMoraleTier1;
            s.DesertionMoraleStepPerTier = DesertionMoraleStepPerTier;
            s.DesertionMoraleFloor = DesertionMoraleFloor;
            s.DesertionPercentPerMoralePoint = DesertionPercentPerMoralePoint;
            s.DesertionDailyCapPercent = DesertionDailyCapPercent;
            s.DesertionLawForAi = DesertionLawForAi;
            s.UniqueGearLawEnabled = UniqueGearLawEnabled;
            s.MinSellPercentOfValue = MinSellPercentOfValue;
            s.OneScrapFloor = OneScrapFloor;
            s.BkTradePenaltyOnce = BkTradePenaltyOnce;
            s.SellPriceByCondition = SellPriceByCondition;
            s.SellCapPercentOfNewAsk = SellCapPercentOfNewAsk;
            s.EnlistedSoldierNoLooting = EnlistedSoldierNoLooting;
            s.FieldCraftEnabled = FieldCraftEnabled;
            s.SprintFatigueEnabled = SprintFatigueEnabled;
            s.UseRbmStamina = UseRbmStamina;
            s.SprintDrainPerSecond = SprintDrainPerSecond;
            s.SprintDrainPerKg = SprintDrainPerKg;
            s.FatigueFreeArmorKg = FatigueFreeArmorKg;
            s.BattleStaminaEnabled = BattleStaminaEnabled;
            s.BattleEndDoubleEvery = BattleEndDoubleEvery;
            s.BattleRegenAtEnd1 = BattleRegenAtEnd1;
            s.BattleRegenAtEnd10 = BattleRegenAtEnd10;
            s.BattleWindedFloor = BattleWindedFloor;
            s.StaminaRegenPerSecond = StaminaRegenPerSecond;
            s.TiredSpeedFactor = TiredSpeedFactor;
            s.WoundedPenaltiesEnabled = WoundedPenaltiesEnabled;
            s.WoundedBelowPercent = WoundedBelowPercent;
            s.WoundedMaxSlow = WoundedMaxSlow;
            s.BleedBelowHp = BleedBelowHp;
            s.AiFleeBelowPercent = AiFleeBelowPercent;
            s.BleedPerSecond = BleedPerSecond;
            s.AiFleeWhenNearDeath = AiFleeWhenNearDeath;
            s.ArrowUnstickEnabled = ArrowUnstickEnabled;
            s.ArrowStickMinDamage = ArrowStickMinDamage;
            s.JavelinStickMinDamage = JavelinStickMinDamage;
            s.WalkKeyEnabled = WalkKeyEnabled;
            s.WalkSpeedShare = WalkSpeedShare;
            s.HorseDeathPermanent = HorseDeathPermanent;
            s.ThrownWobbleEnabled = ThrownWobbleEnabled;
            s.ThrownInaccuracyFactor = ThrownInaccuracyFactor;
            s.ThrownMountedInaccuracyFactor = ThrownMountedInaccuracyFactor;
            s.ChargeTemperEnabled = ChargeTemperEnabled;
            s.ChargeDamageFactor = ChargeDamageFactor;
            s.ChargeFullSpeed = ChargeFullSpeed;
            s.AutoParryEnabled = AutoParryEnabled;
            s.WoundedFleeEnabled = WoundedFleeEnabled;
            s.WoundedFleePercent = WoundedFleePercent;
            s.WoundedFleeHeroes = WoundedFleeHeroes;
            s.WoundedFleeEnemiesOnly = WoundedFleeEnemiesOnly;
            s.WoundedFleeWithoutPlayer = WoundedFleeWithoutPlayer;
            s.AutoParryFullDiff = AutoParryFullDiff;
            s.AutoParryTwoHandedOnly = AutoParryTwoHandedOnly;
            s.AutoParryMirrorSides = AutoParryMirrorSides;
            s.CavalryNeedsMounts = CavalryNeedsMounts;
            s.WarHorseFromTier = WarHorseFromTier;
            s.NobleHorseFromTier = NobleHorseFromTier;
            s.AiBuysMounts = AiBuysMounts;
            s.AiMountSpareBuffer = AiMountSpareBuffer;
            s.AiMountPurseShare = AiMountPurseShare;
            s.AiMountMaxPerVisit = AiMountMaxPerVisit;
            s.AiMountBuyCooldownDays = AiMountBuyCooldownDays;
            s.AiMountBreederFallback = AiMountBreederFallback;
            s.AiMountBreederMarkup = AiMountBreederMarkup;
            s.HorsesAtMarketPrice = HorsesAtMarketPrice;
            s.MercHorseFromShelf = MercHorseFromShelf;
            s.RecruitsOwnHorse = RecruitsOwnHorse;
            s.MountedWagePremium = MountedWagePremium;
            s.MountedWageFactor = MountedWageFactor;
            s.AiMountMarketSharePercent = AiMountMarketSharePercent;
            s.AiMountShelfFloor = AiMountShelfFloor;
            s.LongYearEnabled = LongYearEnabled;
            s.WeeksPerSeason = WeeksPerSeason;
            s.MarchPaceEnabled = MarchPaceEnabled;
            s.WorldPacePercent = WorldPacePercent;
            s.TerrainEaseEnabled = TerrainEaseEnabled;
            s.ForestSpeedPenalty = ForestSpeedPenalty;
            s.DesertSpeedPenalty = DesertSpeedPenalty;
            s.SnowSpeedPenalty = SnowSpeedPenalty;
            s.SwampSpeedPenalty = SwampSpeedPenalty;
            s.FordSpeedPenalty = FordSpeedPenalty;
            s.NightSpeedPenalty = NightSpeedPenalty;
            s.AmmoTracerEnabled = AmmoTracerEnabled;
            s.PlagueWatchEnabled = PlagueWatchEnabled;
            s.InfluenceWatchEnabled = InfluenceWatchEnabled;
            s.SpeedAuditEnabled = SpeedAuditEnabled;
            s.WorldMeasureLog = WorldMeasureLog;
            s.SiegePacePercent = SiegePacePercent;
            s.SiegeSicknessEnabled = SiegeSicknessEnabled;
            s.SiegeSicknessIncubationDays = SiegeSicknessIncubationDays;
            s.SiegeSicknessBasePercent = SiegeSicknessBasePercent;
            s.SiegeSicknessRampPercent = SiegeSicknessRampPercent;
            s.SiegeSicknessDefenderFactor = SiegeSicknessDefenderFactor;
            s.SiegeSicknessDeathShare = SiegeSicknessDeathShare;
            s.SiegeSicknessMedicineMax = SiegeSicknessMedicineMax;
            s.SingleWinterSource = SingleWinterSource;
            s.WinterBiteEnabled = WinterBiteEnabled;
            s.WinterPartyFoodBonusPercent = WinterPartyFoodBonusPercent;
            s.WinterVillageOutputCutPercent = WinterVillageOutputCutPercent;
            s.WinterTownAppetitePer1000 = WinterTownAppetitePer1000;
            s.AutumnStockMultiplier = AutumnStockMultiplier;
            s.NorthGradientPercent = NorthGradientPercent;
            s.ScorchedEarthEnabled = ScorchedEarthEnabled;
            s.ForageMinMen = ForageMinMen;
            s.ForageRadius = ForageRadius;
            s.ForageHearthPerDay = ForageHearthPerDay;
            s.ForageFloor = ForageFloor;
            s.ScarThresholdHearth = ScarThresholdHearth;
            s.ScarRegenPercent = ScarRegenPercent;
            s.RefugeeFloorHearth = RefugeeFloorHearth;
            s.WagesDueEnabled = WagesDueEnabled;
            s.WagesGraceDays = WagesGraceDays;
            s.WagesDesertPercentPerDay = WagesDesertPercentPerDay;
            s.WagesDesertMaxDays = WagesDesertMaxDays;
            s.WarLedgerToOutlaws = WarLedgerToOutlaws;
            s.SackScarEnabled = SackScarEnabled;
            s.SackProsperityCutPercent = SackProsperityCutPercent;
            s.SackLoyaltyHit = SackLoyaltyHit;
            s.MarchPaceAiToo = MarchPaceAiToo;
            s.MarchFootPace = MarchFootPace;
            s.MarchTrainPace = MarchTrainPace;
            s.MarchFootRiderPace = MarchFootRiderPace;
            s.MarchRiderPace = MarchRiderPace;
            s.MarchPackAllowance = MarchPackAllowance;
            s.MaterialLawEnabled = MaterialLawEnabled;
            s.RealRefiningEnabled = RealRefiningEnabled;
            s.BloomeryCharcoalPerOre = BloomeryCharcoalPerOre;
            s.CharcoalValue = CharcoalValue;
            s.CrudeIronValue = CrudeIronValue;
            s.WroughtIronValue = WroughtIronValue;
            s.IronValue = IronValue;
            s.SteelValue = SteelValue;
            s.FineSteelValue = FineSteelValue;
            s.ValyrianSteelValue = ValyrianSteelValue;
            s.MineralsCountedOnce = MineralsCountedOnce;
            s.MineOutputMultiplier = MineOutputMultiplier;
            s.LumberOutputMultiplier = LumberOutputMultiplier;
            s.NoFreeTimberAndTools = NoFreeTimberAndTools;
            s.VillageWoodlotLoads = VillageWoodlotLoads;
            s.SmeltCapToCraftCost = SmeltCapToCraftCost;
            s.StartStockInLoads = StartStockInLoads;
            s.ArmsCostPricingEnabled = ArmsCostPricingEnabled;
            s.ArmsPriceBand = ArmsPriceBand;
            s.SmithDayWage = SmithDayWage;
            s.SmithProfitPercent = SmithProfitPercent;
            s.MaterialIndexEnabled = MaterialIndexEnabled;
            s.MaterialIndexInertia = MaterialIndexInertia;
            s.MaterialRatioMin = MaterialRatioMin;
            s.MaterialRatioMax = MaterialRatioMax;
            s.MaterialIndexMin = MaterialIndexMin;
            s.MaterialIndexMax = MaterialIndexMax;
            s.WarExpectationEnabled = WarExpectationEnabled;
            s.WarExpectationBase = WarExpectationBase;
            s.WarExpectationDays = WarExpectationDays;
            s.SubstitutionEnabled = SubstitutionEnabled;
            s.SubstitutionShare = SubstitutionShare;
            s.TradeTransportPercentPer100 = TradeTransportPercentPer100;
            s.AiBuysGear = AiBuysGear;
            s.GarrisonBuysGear = GarrisonBuysGear;
            s.GarrisonBuysGearPlayer = GarrisonBuysGearPlayer;
            s.UniqueSpoilsFromPlayer = UniqueSpoilsFromPlayer;
            s.BattleRealMinSide = BattleRealMinSide;
            s.UniqueMaxWearers = UniqueMaxWearers;
            s.BattleChronicleMinMen = BattleChronicleMinMen;
            s.BuildDiaryEnabled = BuildDiaryEnabled;
            s.FinanceLedgerEnabled = FinanceLedgerEnabled;
            s.FinanceLedgerPoor = FinanceLedgerPoor;
            s.FinanceLedgerPoorest = FinanceLedgerPoorest;
            s.GoodsLedgerEnabled = GoodsLedgerEnabled;
            s.PaidConstruction = PaidConstruction;
            s.PaidConstructionPlayer = PaidConstructionPlayer;
            s.BuildIncomeShare = BuildIncomeShare;
            s.BuildMaterialShare = BuildMaterialShare;
            s.BuildPencePerPointMilitary = BuildPencePerPointMilitary;
            s.BuildPencePerPointCivil = BuildPencePerPointCivil;
            s.BuildWagesByTown = BuildWagesByTown;
            s.AiRecruitsBringKit = AiRecruitsBringKit;
            s.KitFromNotable = KitFromNotable;
            s.AiGearBudgetPercent = AiGearBudgetPercent;
            s.AiGearGoldReserve = AiGearGoldReserve;
            s.AiGearMaxPiecesPerVisit = AiGearMaxPiecesPerVisit;
            s.AiGearLogPerDay = AiGearLogPerDay;
            s.WorkshopLawEnabled = WorkshopLawEnabled;
            s.WorkshopWorkersArtisans = WorkshopWorkersArtisans;
            s.WorkshopWorkers = WorkshopWorkers;
            s.WorkshopProsperityPerHand = WorkshopProsperityPerHand;
            s.WorkshopArtisansMin = WorkshopArtisansMin;
            s.WorkshopArtisansMax = WorkshopArtisansMax;
            s.ArtisanTanWeavePerCycle = ArtisanTanWeavePerCycle;
            s.TownCraftsEnabled = TownCraftsEnabled;
            s.TownCraftHandsPerArmsHand = TownCraftHandsPerArmsHand;
            s.WorkshopSellShare = WorkshopSellShare;
            s.GuildShareTailor = GuildShareTailor;
            s.GuildShareArmourer = GuildShareArmourer;
            s.GuildShareWeaponsmith = GuildShareWeaponsmith;
            s.GuildShareSaddler = GuildShareSaddler;
            s.GuildShareBowyer = GuildShareBowyer;
            s.GuildShareShieldwright = GuildShareShieldwright;
            s.WorkshopForgeWoodPerMetalKg = WorkshopForgeWoodPerMetalKg;
            s.WorkshopWagePerDay = WorkshopWagePerDay;
            s.WorkshopWageByTier = WorkshopWageByTier;
            s.WorkshopMinProfitPercent = WorkshopMinProfitPercent;
            s.WorkshopCrudeKgPerOre = WorkshopCrudeKgPerOre;
            s.WorkshopWoodPerOre = WorkshopWoodPerOre;
            s.WorkshopCandidates = WorkshopCandidates;
            s.WorkshopTradeEnabled = WorkshopTradeEnabled;
            s.WorkshopTradeUpkeepPerDay = WorkshopTradeUpkeepPerDay;
            s.WorkshopTradeBatchWages = WorkshopTradeBatchWages;
            s.WorkshopTradeStartCapital = WorkshopTradeStartCapital;
            s.WorkshopTradeLowCapital = WorkshopTradeLowCapital;
            s.WorkshopTradeEquipmentScale = WorkshopTradeEquipmentScale;
            s.WorkshopTradePriceYears = WorkshopTradePriceYears;
            s.WorkshopTradeProfitDays = WorkshopTradeProfitDays;
            s.WorkshopTradeResaleShare = WorkshopTradeResaleShare;
            s.ArtisanOwnInputs = ArtisanOwnInputs;
            s.WorkshopTradeFairPrice = WorkshopTradeFairPrice;
            s.CastleVillagesSellInTown = CastleVillagesSellInTown;
            s.MarketMaxDistance = MarketMaxDistance;
            s.MarketCartFactor = MarketCartFactor;
            s.MarketCartAllVillages = MarketCartAllVillages;
            s.VillageCartsBestMarket = VillageCartsBestMarket;
            s.VillageCartFullLoadFar = VillageCartFullLoadFar;
            s.VillageCartWholeStore = VillageCartWholeStore;
            s.VillageCartRoadNews = VillageCartRoadNews;
            s.VillageCartFairPrice = VillageCartFairPrice;
            s.MapRoadTableFix = MapRoadTableFix;
            s.VillageCartLeaveTown = VillageCartLeaveTown;
            s.VillageCartTownMaxDays = VillageCartTownMaxDays;
            s.IslandRoadsFix = IslandRoadsFix;
            s.VillageClogDiagnostics = VillageClogDiagnostics;
            s.CaravanBulkEnabled = CaravanBulkEnabled;
            s.CaravanBulkStockDays = CaravanBulkStockDays;
            s.CaravanBulkSurplusFactor = CaravanBulkSurplusFactor;
            s.CaravanBulkCapacityShare = CaravanBulkCapacityShare;
            s.CaravanBulkTransitCover = CaravanBulkTransitCover;
            s.CaravanBulkBuyBeforeRoute = CaravanBulkBuyBeforeRoute;
            s.CaravanBulkFillLimit = CaravanBulkFillLimit;
            s.LevyEnabled = LevyEnabled;
            s.RecruitBaseWilling = RecruitBaseWilling;
            s.RecruitExcessWeight = RecruitExcessWeight;
            s.RecruitMiseryWeight = RecruitMiseryWeight;
            s.RecruitWillingMax = RecruitWillingMax;
            s.RecruitGoldToSeller = RecruitGoldToSeller;
            s.NoFreeKitForNewParties = NoFreeKitForNewParties;
            s.VolunteerKitEnabled = VolunteerKitEnabled;
            s.VolunteerKitKeyOnly = VolunteerKitKeyOnly;
            s.ColdStartEnabled = ColdStartEnabled;
            s.ColdStartMarketDays = ColdStartMarketDays;
            s.HistoricalPricesEnabled = HistoricalPricesEnabled;
            s.HistIronOrePerKg = HistIronOrePerKg;
            s.HistWoodPerKg = HistWoodPerKg;
            s.HistCharcoalPerKg = HistCharcoalPerKg;
            s.HistCrudeIronPerKg = HistCrudeIronPerKg;
            s.HistWroughtIronPerKg = HistWroughtIronPerKg;
            s.HistIronPerKg = HistIronPerKg;
            s.HistSteelPerKg = HistSteelPerKg;
            s.HistFineSteelPerKg = HistFineSteelPerKg;
            s.HistValyrianPerKg = HistValyrianPerKg;
            s.HistLeatherPerKg = HistLeatherPerKg;
            s.HistLinenPerKg = HistLinenPerKg;
            s.HistSpecialFactor = HistSpecialFactor;
            s.HistMasterWageT1 = HistMasterWageT1;
            s.HistMasterWagePerTier = HistMasterWagePerTier;
            s.HistProfitPercent = HistProfitPercent;
            s.TownWageRefProsperity = TownWageRefProsperity;
            s.HistAmmoLaborMultiplier = HistAmmoLaborMultiplier;
            s.HistTournamentScale = HistTournamentScale;
            s.HistUniquePrestige = HistUniquePrestige;
            s.HistBulkUnitFactor = HistBulkUnitFactor;
            s.HistTradeGoods = HistTradeGoods;
            s.HistLivestockPrices = HistLivestockPrices;
            s.HistHidesPerKg = HistHidesPerKg;
            s.HistFlaxPerKg = HistFlaxPerKg;
            s.HistBowLaborMultiplier = HistBowLaborMultiplier;
            s.TownHouseholdUse = TownHouseholdUse;
            s.TownUseFlax = TownUseFlax;
            s.TownUseWool = TownUseWool;
            s.TownUseHides = TownUseHides;
            s.TownUseIron = TownUseIron;
            s.TownUseLeather = TownUseLeather;
            s.TownUseLinen = TownUseLinen;
            s.TownUseHardwood = TownUseHardwood;
            s.HistDemandScaling = HistDemandScaling;
            s.HistDemandFromDefinition = HistDemandFromDefinition;
            s.HistMixedCategoryShelf = HistMixedCategoryShelf;
            s.PriceFormulaInNewCoin = PriceFormulaInNewCoin;
            s.RawPriceByUse = RawPriceByUse;
            s.PopulationRentEnabled = PopulationRentEnabled;
            s.PopulationRentPerHead = PopulationRentPerHead;
            s.PopulationScale = PopulationScale;
            s.PopulationRentMaxShare = PopulationRentMaxShare;
            s.RentTaxLow = RentTaxLow;
            s.RentTaxHigh = RentTaxHigh;
            s.RentTaxExemption = RentTaxExemption;
            s.RentReplacesTownTax = RentReplacesTownTax;
            s.TownRentShare = TownRentShare;
            s.TownRentFloorGold = TownRentFloorGold;
            s.WorkshopNoFreeRaw = WorkshopNoFreeRaw;
            s.HistoricalRecruitCost = HistoricalRecruitCost;
            s.RecruitCostDays = RecruitCostDays;
            s.AmmoRecoveryEnabled = AmmoRecoveryEnabled;
            s.AmmoRecoverPercent = AmmoRecoverPercent;
            s.AmmoRepairPercent = AmmoRepairPercent;
            s.KeepWearThroughBattle = KeepWearThroughBattle;
            s.StartKitEnabled = StartKitEnabled;
            s.StartGoldAdventurer = StartGoldAdventurer;
            s.OutlawLawEnabled = OutlawLawEnabled;
            s.OutlawSeedPerHearth = OutlawSeedPerHearth;
            s.OutlawDailyPerThousandHearth = OutlawDailyPerThousandHearth;
            s.OutlawWarMisery = OutlawWarMisery;
            s.OutlawLootedMisery = OutlawLootedMisery;
            s.OutlawStarvingMisery = OutlawStarvingMisery;
            s.OutlawProsperityGood = OutlawProsperityGood;
            s.OutlawReturnBasePercent = OutlawReturnBasePercent;
            s.OutlawReturnPeacePercent = OutlawReturnPeacePercent;
            s.OutlawRoutedShare = OutlawRoutedShare;
            s.OutlawRaidFleePercent = OutlawRaidFleePercent;
            s.OutlawHearthPerMan = OutlawHearthPerMan;
            s.OutlawMinBand = OutlawMinBand;
            s.OutlawNeighbourRegions = OutlawNeighbourRegions;
            s.OutlawBandSizeScale = OutlawBandSizeScale;
            s.OutlawDailyRecruit = OutlawDailyRecruit;
            s.OutlawPrisonerJoinPercent = OutlawPrisonerJoinPercent;
            s.OutlawCommonerMaxArmorKg = OutlawCommonerMaxArmorKg;
            s.OutlawGearUpgrades = OutlawGearUpgrades;
            s.OutlawFenceRadius = OutlawFenceRadius;
            s.OutlawFenceMarkup = OutlawFenceMarkup;
            s.OutlawFenceMarkupFar = OutlawFenceMarkupFar;
            s.OutlawNoFreeGold = OutlawNoFreeGold;
            s.OutlawCoinsPerMan = OutlawCoinsPerMan;
            s.OutlawFenceBuysLoot = OutlawFenceBuysLoot;
            s.OutlawFenceLootShare = OutlawFenceLootShare;
            s.OutlawFenceLootShareFar = OutlawFenceLootShareFar;
            s.OutlawFenceBuysSpareAnimals = OutlawFenceBuysSpareAnimals;
            s.OutlawNoFreeFood = OutlawNoFreeFood;
            s.OutlawNoHideoutGold = OutlawNoHideoutGold;
            s.OutlawHideoutStashShare = OutlawHideoutStashShare;
            s.OutlawHoardCirculates = OutlawHoardCirculates;
            s.OutlawLifeSpendShare = OutlawLifeSpendShare;
            s.ClimateEnabled = ClimateEnabled;
            s.ClimateDrivesEconomy = ClimateDrivesEconomy;
            s.ClimateSummerDaysSoFar = ClimateSummerDaysSoFar;
            s.ClimateSummerDaysLeftMin = ClimateSummerDaysLeftMin;
            s.ClimateSummerDaysLeftMax = ClimateSummerDaysLeftMax;
            s.ClimateFirstAutumnDaysMin = ClimateFirstAutumnDaysMin;
            s.ClimateFirstAutumnDaysMax = ClimateFirstAutumnDaysMax;
            s.ClimateFirstWinterYearsMin = ClimateFirstWinterYearsMin;
            s.ClimateFirstWinterYearsMax = ClimateFirstWinterYearsMax;
            s.ClimateSpringYearsMin = ClimateSpringYearsMin;
            s.ClimateSpringYearsMax = ClimateSpringYearsMax;
            s.ClimateSummerYearsMin = ClimateSummerYearsMin;
            s.ClimateSummerYearsMax = ClimateSummerYearsMax;
            s.ClimateAutumnYearsMin = ClimateAutumnYearsMin;
            s.ClimateAutumnYearsMax = ClimateAutumnYearsMax;
            s.ClimateWinterYearsMin = ClimateWinterYearsMin;
            s.ClimateWinterYearsMax = ClimateWinterYearsMax;
            s.CrownDuesEnabled = CrownDuesEnabled;
            s.CrownDuesPeacePercent = CrownDuesPeacePercent;
            s.CrownDuesWarPercent = CrownDuesWarPercent;
            s.LaySubsidyEnabled = LaySubsidyEnabled;
            s.LaySubsidyTownShare = LaySubsidyTownShare;
            s.LaySubsidyVillageShare = LaySubsidyVillageShare;
            s.LaySubsidyWarTaxMultiplier = LaySubsidyWarTaxMultiplier;
            s.CrownCustomsEnabled = CrownCustomsEnabled;
            s.CrownCustomsShare = CrownCustomsShare;
            s.CrownCustomsDutyMultiplier = CrownCustomsDutyMultiplier;
            s.PolicyIncomeConserved = PolicyIncomeConserved;
            s.DebasementShare = DebasementShare;
            s.NoRotClanBailout = NoRotClanBailout;
            s.NoFreeKingdomGold = NoFreeKingdomGold;
            s.IronBankMinDaysToLend = IronBankMinDaysToLend;
            s.IronBankLoanFeePercent = IronBankLoanFeePercent;
            s.IronBankDefaultSeizeShare = IronBankDefaultSeizeShare;
            s.IronBankEnabled = IronBankEnabled;
            s.IronBankCapital = IronBankCapital;
            s.IronBankIncomeDays = IronBankIncomeDays;
            s.IronBankPerTown = IronBankPerTown;
            s.IronBankPerCastle = IronBankPerCastle;
            s.IronBankWageDays = IronBankWageDays;
            s.IronBankRateKing = IronBankRateKing;
            s.IronBankRateLanded = IronBankRateLanded;
            s.IronBankRateLandless = IronBankRateLandless;
            s.IronBankRatePerLoan = IronBankRatePerLoan;
            s.IronBankRateAfterDefault = IronBankRateAfterDefault;
            s.IronBankPlayerDefaultRenown = IronBankPlayerDefaultRenown;
            s.IronBankLogPerDay = IronBankLogPerDay;
            s.IronBankFamilyPays = IronBankFamilyPays;
            s.SoldierPayToPurse = SoldierPayToPurse;
            s.GarrisonPayToCoffers = GarrisonPayToCoffers;
            s.CrownWageRefundEnabled = CrownWageRefundEnabled;
            s.CrownWageRefundPercent = CrownWageRefundPercent;
            s.CrownWageRefundGarrisons = CrownWageRefundGarrisons;
            s.TownWageShield = TownWageShield;
            s.ArmyClothingEnabled = ArmyClothingEnabled;
            s.ArmyClothingFieldLeatherKg = ArmyClothingFieldLeatherKg;
            s.ArmyClothingFieldClothKg = ArmyClothingFieldClothKg;
            s.ArmyClothingFieldLinenKg = ArmyClothingFieldLinenKg;
            s.ArmyClothingGarrisonLeatherKg = ArmyClothingGarrisonLeatherKg;
            s.ArmyClothingGarrisonClothKg = ArmyClothingGarrisonClothKg;
            s.ArmyClothingGarrisonLinenKg = ArmyClothingGarrisonLinenKg;
            s.ArmyClothingMaxWaitDays = ArmyClothingMaxWaitDays;
            s.SupplyDemandEnabled = SupplyDemandEnabled;
            s.SupplyDemandBase = SupplyDemandBase;
            s.SupplyDemandRefProsperity = SupplyDemandRefProsperity;
            s.SupplyDemandElasticity = SupplyDemandElasticity;
            s.SupplyDemandMinFactor = SupplyDemandMinFactor;
            s.RetailFromWorth = RetailFromWorth;
            s.RetailMarkupPercent = RetailMarkupPercent;
            s.SupplyDemandMaxFactor = SupplyDemandMaxFactor;
            s.SupplyDemandOrderWeight = SupplyDemandOrderWeight;
            s.SupplyDemandOrderDecay = SupplyDemandOrderDecay;
            s.SupplyDemandOrderCap = SupplyDemandOrderCap;
            s.SupplyDemandOrderRepeatDays = SupplyDemandOrderRepeatDays;
            s.SupplyDemandTradePercent = SupplyDemandTradePercent;
            s.SupplyDemandTradeRange = SupplyDemandTradeRange;
            s.SupplyDemandTradePricePercent = SupplyDemandTradePricePercent;
            s.MarketGlutEnabled = MarketGlutEnabled;
            s.MarketGlutStartPercent = MarketGlutStartPercent;
            s.MarketGlutDropPP = MarketGlutDropPP;
            s.MarketGlutMinPercent = MarketGlutMinPercent;
            s.MarketGlutRecoverPerDay = MarketGlutRecoverPerDay;
            s.NightRestEnabled = NightRestEnabled;
            s.CampBackground = CampBackground;
            s.MapClockEnabled = MapClockEnabled;
            s.FastForwardMultiplier = FastForwardMultiplier;
            s.SleepHoursNeeded = SleepHoursNeeded;
            s.DayRestFactor = DayRestFactor;
            s.QuickCampKey = QuickCampKey;
            s.SleepAtSeaFree = SleepAtSeaFree;
            s.AiCampsAtNight = AiCampsAtNight;
            s.CampStartHour = CampStartHour;
            s.CampEndHour = CampEndHour;
            s.AiBanditsCampToo = AiBanditsCampToo;
            s.AiTentCap = AiTentCap;
            s.AiTentRadius = AiTentRadius;
            s.AiCampSkipPercent = AiCampSkipPercent;
            s.ArmyLeadersAlwaysCamp = ArmyLeadersAlwaysCamp;
            s.BanditsRestByDay = BanditsRestByDay;
            s.AiNightsAwakeInChase = AiNightsAwakeInChase;
            s.AiCampDangerRadius = AiCampDangerRadius;
            s.CampTentIcon = CampTentIcon;
            s.CoursePlotterEnabled = CoursePlotterEnabled;
            s.NightfallPromptEnabled = NightfallPromptEnabled;
            s.AnvilShiftEnabled = AnvilShiftEnabled;
            s.AnvilShiftHours = AnvilShiftHours;
            s.WorkshopNightRest = WorkshopNightRest;
            s.KgPerAthleticsPoint = KgPerAthleticsPoint;
            s.ArmorAthleticsPerTier = ArmorAthleticsPerTier;
            s.HitScribeEnabled = HitScribeEnabled;
            s.ArmorSanityEnabled = ArmorSanityEnabled;
            s.ArmorOutlierPercentile = ArmorOutlierPercentile;
            s.ArmorOutlierTolerance = ArmorOutlierTolerance;
            s.CampBattlePropsEnabled = CampBattlePropsEnabled;
            s.HideoutAlarmEnabled = HideoutAlarmEnabled;
            s.HideoutAlarmScreamRadius = HideoutAlarmScreamRadius;
            s.HideoutAlarmWitnessRadius = HideoutAlarmWitnessRadius;
            s.HideoutArmouryGear = HideoutArmouryGear;
            s.HideoutNoiseEnabled = HideoutNoiseEnabled;
            s.HideoutHearDay = HideoutHearDay;
            s.HideoutHearNight = HideoutHearNight;
            s.HideoutAlarmRelay = HideoutAlarmRelay;
            s.HideoutNoisePerArmorKg = HideoutNoisePerArmorKg;
            s.HideoutAlarmVoice = HideoutAlarmVoice;
            s.SightCycleEnabled = SightCycleEnabled;
            s.DaySightFactor = DaySightFactor;
            s.NightSightFactor = NightSightFactor;
            s.CombatXpFixEnabled = CombatXpFixEnabled;
            s.ArenaXpPercent = ArenaXpPercent;
            s.TournamentXpPercent = TournamentXpPercent;
            s.BattleXpScalesWithDamage = BattleXpScalesWithDamage;
            s.TroopMendEnabled = TroopMendEnabled;
            s.TroopMendWreckShare = TroopMendWreckShare;
            s.TroopMendBulkDiscountPP = TroopMendBulkDiscountPP;
            s.TroopMendBulkDiscountMax = TroopMendBulkDiscountMax;
            s.TroopMendMaxHours = TroopMendMaxHours;
            s.TroopOrderEnabled = TroopOrderEnabled;
            s.TroopOrderMarkup = TroopOrderMarkup;
            s.TroopOrderFromShelf = TroopOrderFromShelf;
            s.SmithRepairHoursPerPiece = SmithRepairHoursPerPiece;
            s.SelfRepairHoursPerPiece = SelfRepairHoursPerPiece;
            s.MendLootHoursPerPiece = MendLootHoursPerPiece;
            s.MendMaterialMaxShare = MendMaterialMaxShare;
            s.SmithMendFromMarket = SmithMendFromMarket;
            s.WrecksToScrap = WrecksToScrap;
            s.MendMetalByKind = MendMetalByKind;
            s.MendMaterialMenAndLords = MendMaterialMenAndLords;
            s.TakeApartEnabled = TakeApartEnabled;
            s.TakeApartBaseChance = TakeApartBaseChance;
            s.TakeApartSkillSpan = TakeApartSkillSpan;
            s.TakeApartSalvage = TakeApartSalvage;
            s.SmeltHoursPerTier = SmeltHoursPerTier;
            s.CompanionHelperEnabled = CompanionHelperEnabled;
            s.HelperStaminaRelief = HelperStaminaRelief;
            s.HelperTimeRelief = HelperTimeRelief;
            s.HelperSkillForFullRelief = HelperSkillForFullRelief;
            s.ForgePartsFreeBelowTier = ForgePartsFreeBelowTier;
            s.HideoutFlagEnabled = HideoutFlagEnabled;
            s.HideoutFlagDays = HideoutFlagDays;
            s.HouseLeviesEnabled = HouseLeviesEnabled;
            s.NorthernFareEnabled = NorthernFareEnabled;
            s.CropClimateFilter = CropClimateFilter;
            s.WoodlotByClimate = WoodlotByClimate;
            s.VillageClimateFix = VillageClimateFix;
            s.BanditCheerEnabled = BanditCheerEnabled;
            s.BanditCheerRadius = BanditCheerRadius;
            s.BanditCheerRelation = BanditCheerRelation;
            s.LogEnabled = LogEnabled;
            s.MapVillagesEnabled = MapVillagesEnabled;
            s.MapVillagesHideAboveCameraHeight = MapVillagesHideAboveCameraHeight;
            s.MapVillageNamesOnHover = MapVillageNamesOnHover;
            s.MillOnBank = MillOnBank;
            s.VillageTipHeldBy = VillageTipHeldBy;
        }

        internal static void Apply()
        {
            try { var i = Instance; if (i != null) i.ApplyTo(Settings.Current); }
            catch (System.Exception e) { Log.Error("Mcm.Apply", e); }
        }
    }
}