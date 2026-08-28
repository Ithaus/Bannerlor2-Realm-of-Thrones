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

        [SettingPropertyInteger("Smithing Skill Per Tier", 0, 180, "0", HintText = "Smithing needed per tier - 40 means tier 5 plate wants 200")]
        [SettingPropertyGroup("Forging armour")]
        public int SmithingSkillPerTier { get; set; } = 45;

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

        [SettingPropertyBool("Forge Takes Time", HintText = "armour is not finished the moment you order it")]
        [SettingPropertyGroup("Time at the anvil")]
        public bool ForgeTakesTime { get; set; } = true;

        [SettingPropertyFloatingInteger("Days Per Tier", 0.00f, 8.00f, "0.00", HintText = "days per tier - 2 means a tier 5 plate takes ten days")]
        [SettingPropertyGroup("Time at the anvil")]
        public float DaysPerTier { get; set; } = 2f;

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

        [SettingPropertyFloatingInteger("Weapon Days Per Tier", 0.00f, 4.00f, "0.00", HintText = "weapons come faster than armour, but not at once")]
        [SettingPropertyGroup("Experience")]
        public float WeaponDaysPerTier { get; set; } = 1.0f;

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

        [SettingPropertyInteger("Forge Fee Base", 0, 300, "0", HintText = "what the smith charges you to use his forge")]
        [SettingPropertyGroup("Forge fee")]
        public int ForgeFeeBase { get; set; } = 75;

        [SettingPropertyInteger("Forge Fee Per Tier", 0, 240, "0", HintText = "and this much more for every tier of the work")]
        [SettingPropertyGroup("Forge fee")]
        public int ForgeFeePerTier { get; set; } = 60;

        [SettingPropertyFloatingInteger("Bk Forge Hourly Multiplier", 0.00f, 2.00f, "0.00", HintText = "Banner Kings charges by the hour at the anvil - this scales that hourly rate")]
        [SettingPropertyGroup("Forge fee")]
        public float BkForgeHourlyMultiplier { get; set; } = 0.5f;

        [SettingPropertyBool("Forge Day Pass Enabled", HintText = "the smith hires his forge BY THE DAY, paid up front - one hour or twenty-three, same coin")]
        [SettingPropertyGroup("Forge fee")]
        public bool ForgeDayPassEnabled { get; set; } = true;

        [SettingPropertyFloatingInteger("Forge Day Hours", 0.00f, 32.00f, "0.00", HintText = "the day's hire costs this many hours at the smith's rate (~200 gold in an average town)")]
        [SettingPropertyGroup("Forge fee")]
        public float ForgeDayHours { get; set; } = 8f;

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

        [SettingPropertyFloatingInteger("Troop Wear Percent", 0.00f, 48.00f, "0.00", HintText = "this share of pieces IN USE takes one step of wear per battle")]
        [SettingPropertyGroup("Wear and tear")]
        public float TroopWearPercent { get; set; } = 12f;

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

        [SettingPropertyFloatingInteger("Condition Penalty Exponent", 0.00f, 4.60f, "0.00", HintText = "the curve: above 1 = small wear is cheap, deep wear bites (99% state ~ -0.5%, 50% ~ -41%, 1% ~ -89%)")]
        [SettingPropertyGroup("Wear and tear")]
        public float ConditionPenaltyExponent { get; set; } = 1.15f;

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

        [SettingPropertyFloatingInteger("Durability Per Armor Point", 0.00f, 80.00f, "0.00", HintText = "Jeff's pool: every point of protection gives this much durability, times the tier - 61 armor at tier 3 = 61 x 20 x 3 = 3660 points, and damage taken subtracts one for one")]
        [SettingPropertyGroup("Wear and tear")]
        public float DurabilityPerArmorPoint { get; set; } = 20f;

        [SettingPropertyFloatingInteger("Wear Weapon Per Hit", 0.00f, 2.40f, "0.00", HintText = "wear on your weapon for every blow you land (bows wear per arrow that strikes home)")]
        [SettingPropertyGroup("Wear and tear")]
        public float WearWeaponPerHit { get; set; } = 0.6f;

        [SettingPropertyFloatingInteger("Wear Shield Factor", 0.00f, 1.20f, "0.00", HintText = "shields are built to take it - blocked damage wears them at this share")]
        [SettingPropertyGroup("Wear and tear")]
        public float WearShieldFactor { get; set; } = 0.3f;

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

        [SettingPropertyFloatingInteger("Repair Cost Factor", 0.00f, 2.00f, "0.00", HintText = "share of item value for a full repair")]
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

        [SettingPropertyBool("Ale Supply Enabled", HintText = "town markets restock beer and wine daily up to a floor - Banner Kings parties drink the realm dry and the morale fine stays with no cask to buy")]
        [SettingPropertyGroup("The town brewery")]
        public bool AleSupplyEnabled { get; set; } = true;

        [SettingPropertyInteger("Ale Supply Beer Floor", 0, 60, "0", HintText = "beer casks a town market is topped up to")]
        [SettingPropertyGroup("The town brewery")]
        public int AleSupplyBeerFloor { get; set; } = 15;

        [SettingPropertyInteger("Ale Supply Wine Floor", 0, 32, "0", HintText = "wine amphorae a town market is topped up to (mead, where it exists, gets half this)")]
        [SettingPropertyGroup("The town brewery")]
        public int AleSupplyWineFloor { get; set; } = 8;

        [SettingPropertyInteger("Ale Supply Per Day", 0, 40, "0", HintText = "at most this many casks brewed per town per day - a trickle, not a flood")]
        [SettingPropertyGroup("The town brewery")]
        public int AleSupplyPerDay { get; set; } = 10;

        [SettingPropertyBool("Hideout Purge Enabled", HintText = "a cleared hideout must be SEARCHED: the plundered gold, renown and the gratitude of the district wait behind one more step")]
        [SettingPropertyGroup("The hideout purge")]
        public bool HideoutPurgeEnabled { get; set; } = true;

        [SettingPropertyInteger("Hideout Gold Base", 0, 600, "0", HintText = "gold hidden in any den before counting its bands")]
        [SettingPropertyGroup("The hideout purge")]
        public int HideoutGoldBase { get; set; } = 150;

        [SettingPropertyInteger("Hideout Gold Per Band", 0, 480, "0", HintText = "each raiding band that lived there stashed about this much loot from the district")]
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

        [SettingPropertyFloatingInteger("Hideout Search Min Hours", 0.00f, 8.00f, "0.00", HintText = "the search never drops below this many hours - some stones only come up slowly")]
        [SettingPropertyGroup("The hideout purge")]
        public float HideoutSearchMinHours { get; set; } = 2f;

        [SettingPropertyBool("Hideout Reprisal Enabled", HintText = "nearby bands mass to take their den back while you dig - unless your line scares them off")]
        [SettingPropertyGroup("The hideout purge")]
        public bool HideoutReprisalEnabled { get; set; } = true;

        [SettingPropertyFloatingInteger("Hideout Reprisal Radius", 0.00f, 80.00f, "0.00", HintText = "map-distance within which bandit parties join the reprisal")]
        [SettingPropertyGroup("The hideout purge")]
        public float HideoutReprisalRadius { get; set; } = 20f;

        [SettingPropertyFloatingInteger("Hideout Reprisal Flee Odds", 0.00f, 12.00f, "0.00", HintText = "at this strength advantage (yours vs theirs) the reprisal loses its nerve and melts away")]
        [SettingPropertyGroup("The hideout purge")]
        public float HideoutReprisalFleeOdds { get; set; } = 3f;

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

        [SettingPropertyBool("Battlefield Law Enabled", HintText = "battles you fight yourself: the dead drop their real gear (DTE), Spoils of War steps aside")]
        [SettingPropertyGroup("The law of the battlefield")]
        public bool BattlefieldLawEnabled { get; set; } = true;

        [SettingPropertyBool("Loot Arrives Battle Worn", HintText = "loot-screen items from fought battles arrive battle-worn but keep their worth")]
        [SettingPropertyGroup("The law of the battlefield")]
        public bool LootArrivesBattleWorn { get; set; } = true;

        [SettingPropertyBool("Sim Battle Full Drop", HintText = "auto-resolved battles ignore the hidden per-tier drop multipliers")]
        [SettingPropertyGroup("The law of the battlefield")]
        public bool SimBattleFullDrop { get; set; } = true;

        [SettingPropertyInteger("Player Loot Share Percent", 0, 160, "0", HintText = "your cut of what the party strips from the dead; the rest stays in the army armoury (a lone wanderer takes all)")]
        [SettingPropertyGroup("The law of the battlefield")]
        public int PlayerLootSharePercent { get; set; } = 40;

        [SettingPropertyBool("Wreck Salvage Enabled", HintText = "the piece smashed by the killing blow is not lost - it lands in the loot as a wreck to mend at the forge")]
        [SettingPropertyGroup("The law of the battlefield")]
        public bool WreckSalvageEnabled { get; set; } = true;

        [SettingPropertyInteger("Min Sell Percent Of Value", 0, 20, "0", HintText = "merchants never pay less than this share of an item's clean value - scrap is still metal and leather (0 = off)")]
        [SettingPropertyGroup("The law of the battlefield")]
        public int MinSellPercentOfValue { get; set; } = 5;

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

        [SettingPropertyFloatingInteger("Battle Regen At End1", 0.00f, 40.00f, "0.00", HintText = "stamina regained per second at Endurance 1 (Athletics adds its share on top)")]
        [SettingPropertyGroup("Flesh and wind")]
        public float BattleRegenAtEnd1 { get; set; } = 10f;

        [SettingPropertyFloatingInteger("Battle Regen At End10", 0.00f, 800.00f, "0.00", HintText = "stamina regained per second at Endurance 10 - the curve between is exponential, not a straight line")]
        [SettingPropertyGroup("Flesh and wind")]
        public float BattleRegenAtEnd10 { get; set; } = 200f;

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

        [SettingPropertyBool("Long Year Enabled", HintText = "stretch the year so the world stops racing: children grow, lords age and seasons turn at a pace a long campaign can live with")]
        [SettingPropertyGroup("The turning year")]
        public bool LongYearEnabled { get; set; } = true;

        [SettingPropertyInteger("Weeks Per Season", 0, 24, "0", HintText = "weeks in a season (vanilla 3 = an 84-day year; 6 = a 168-day year with 42-day seasons). SET IT BEFORE STARTING A CAMPAIGN and leave it alone afterwards")]
        [SettingPropertyGroup("The turning year")]
        public int WeeksPerSeason { get; set; } = 6;

        [SettingPropertyBool("March Pace Enabled", HintText = "a column moves at the pace of its slowest man: any soldier or prisoner on foot holds the whole party to walking speed")]
        [SettingPropertyGroup("The marching column")]
        public bool MarchPaceEnabled { get; set; } = true;

        [SettingPropertyBool("March Pace Ai Too", HintText = "the same law binds lords, bandits and patrols (villagers and caravans keep their own pace either way)")]
        [SettingPropertyGroup("The marching column")]
        public bool MarchPaceAiToo { get; set; } = true;

        [SettingPropertyFloatingInteger("March Foot Pace", 0.00f, 16.00f, "0.00", HintText = "map speed cap while anyone walks - footmen without a spare mount, or prisoners on the rope")]
        [SettingPropertyGroup("The marching column")]
        public float MarchFootPace { get; set; } = 4.0f;

        [SettingPropertyFloatingInteger("March Train Pace", 0.00f, 16.80f, "0.00", HintText = "map speed cap for an all-riding party that still drags a baggage train (pack animals, livestock)")]
        [SettingPropertyGroup("The marching column")]
        public float MarchTrainPace { get; set; } = 4.2f;

        [SettingPropertyFloatingInteger("March Rider Pace", 0.00f, 26.00f, "0.00", HintText = "map speed cap for a clean column of riders - every man horsed, no train")]
        [SettingPropertyGroup("The marching column")]
        public float MarchRiderPace { get; set; } = 6.5f;

        [SettingPropertyFloatingInteger("March Pack Allowance", 0.00f, 1.00f, "0.00", HintText = "this many pack animals PER MAN count as field supply, not a train (0.25 = a mule per four men rides free)")]
        [SettingPropertyGroup("The marching column")]
        public float MarchPackAllowance { get; set; } = 0.25f;

        [SettingPropertyBool("Market Glut Enabled", HintText = "a merchant needs only so many of one thing: each extra piece of a type you sell him fetches less")]
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

        [SettingPropertyFloatingInteger("Sleep Hours Needed", 0.00f, 20.00f, "0.00", HintText = "this many hours of sleep settle the day's account - whenever taken")]
        [SettingPropertyGroup("A night's rest")]
        public float SleepHoursNeeded { get; set; } = 5f;

        [SettingPropertyFloatingInteger("Day Rest Factor", 0.00f, 2.40f, "0.00", HintText = "sleep by daylight counts at this rate (camp noise, heat, light) - night hours count in full")]
        [SettingPropertyGroup("A night's rest")]
        public float DayRestFactor { get; set; } = 0.6f;

        [SettingPropertyBool("Quick Camp Key", HintText = "press O on the campaign map to pitch the BannerKings camp on the spot")]
        [SettingPropertyGroup("A night's rest")]
        public bool QuickCampKey { get; set; } = true;

        [SettingPropertyBool("Sleep At Sea Free", HintText = "crews sleep in watches - sailing through the night builds no sleep debt")]
        [SettingPropertyGroup("A night's rest")]
        public bool SleepAtSeaFree { get; set; } = true;

        [SettingPropertyBool("Ai Camps At Night", HintText = "the world sleeps too: lord parties and caravans halt for the night (22-4) unless chased or in action")]
        [SettingPropertyGroup("A night's rest")]
        public bool AiCampsAtNight { get; set; } = true;

        [SettingPropertyBool("Ai Bandits Camp Too", HintText = "brigands sleep as well - hideout by day, their own fire in the field by night")]
        [SettingPropertyGroup("A night's rest")]
        public bool AiBanditsCampToo { get; set; } = false;

        [SettingPropertyInteger("Ai Tent Cap", 0, 160, "0", HintText = "at most this many AI camps get a tent icon on the map - the rest still sleep, just without the picture")]
        [SettingPropertyGroup("A night's rest")]
        public int AiTentCap { get; set; } = 40;

        [SettingPropertyFloatingInteger("Ai Tent Radius", 0.00f, 140.00f, "0.00", HintText = "tent icons appear only this close to your party - the world beyond still sleeps, just without the picture")]
        [SettingPropertyGroup("A night's rest")]
        public float AiTentRadius { get; set; } = 35f;

        [SettingPropertyInteger("Ai Camp Skip Percent", 0, 60, "0", HintText = "this share of lord columns press on through any given night - not everyone pitches camp")]
        [SettingPropertyGroup("A night's rest")]
        public int AiCampSkipPercent { get; set; } = 15;

        [SettingPropertyBool("Bandits Rest By Day", HintText = "every band has a nature: three in four are night hunters (lie low 10-16), one in four hunts by day and beds down at night (23-5)")]
        [SettingPropertyGroup("A night's rest")]
        public bool BanditsRestByDay { get; set; } = true;

        [SettingPropertyFloatingInteger("Ai Nights Awake In Chase", 0.00f, 4.00f, "0.00", HintText = "days a chasing or fleeing party may push on without sleep before it drops anyway")]
        [SettingPropertyGroup("A night's rest")]
        public float AiNightsAwakeInChase { get; set; } = 1f;

        [SettingPropertyFloatingInteger("Ai Camp Danger Radius", 0.00f, 24.00f, "0.00", HintText = "a hostile party this close keeps them marching - pursuit knows no bedtime")]
        [SettingPropertyGroup("A night's rest")]
        public float AiCampDangerRadius { get; set; } = 6f;

        [SettingPropertyBool("Camp Tent Icon", HintText = "pitched camps show a tent on the map (yours and theirs)")]
        [SettingPropertyGroup("A night's rest")]
        public bool CampTentIcon { get; set; } = true;

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

        [SettingPropertyFloatingInteger("Troop Mend Wreck Share", 0.00f, 1.00f, "0.00", HintText = "a WRECK (1%) costs this share of the piece's worth - lighter wear costs proportionally less (60% condition = 4% of worth)")]
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

        [SettingPropertyFloatingInteger("Troop Order Markup", 0.00f, 4.60f, "0.00", HintText = "the smith's fee: each procured piece costs its market worth times this")]
        [SettingPropertyGroup("The men's gear")]
        public float TroopOrderMarkup { get; set; } = 1.15f;

        [SettingPropertyFloatingInteger("Smith Repair Hours Per Piece", 0.00f, 6.00f, "0.00", HintText = "hours the smith needs per worn piece of your harness")]
        [SettingPropertyGroup("Time at the forge")]
        public float SmithRepairHoursPerPiece { get; set; } = 1.5f;

        [SettingPropertyFloatingInteger("Self Repair Hours Per Piece", 0.00f, 10.00f, "0.00", HintText = "hours you need per piece working the anvil yourself")]
        [SettingPropertyGroup("Time at the forge")]
        public float SelfRepairHoursPerPiece { get; set; } = 2.5f;

        [SettingPropertyFloatingInteger("Mend Loot Hours Per Piece", 0.00f, 2.40f, "0.00", HintText = "hours per battle-worn piece from the bags")]
        [SettingPropertyGroup("Time at the forge")]
        public float MendLootHoursPerPiece { get; set; } = 0.6f;

        [SettingPropertyFloatingInteger("Mend Material Max Share", 0.00f, 1.00f, "0.00", HintText = "mending is NOT forging anew: even a wreck (1%) takes at most this share of the full recipe's materials")]
        [SettingPropertyGroup("Time at the forge")]
        public float MendMaterialMaxShare { get; set; } = 0.20f;

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

        [SettingPropertyBool("Log Enabled", HintText = "write a log file in the module folder")]
        [SettingPropertyGroup("Companion at the bellows")]
        public bool LogEnabled { get; set; } = true;

        public void ApplyTo(Settings s)
        {
            s.TidyBannerKingsArmourList = TidyBannerKingsArmourList;
            s.ForgeArmourEnabled = ForgeArmourEnabled;
            s.CraftingEnabled = CraftingEnabled;
            s.SmithingSkillPerTier = SmithingSkillPerTier;
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
            s.ForgeWorkNoRest = ForgeWorkNoRest;
            s.EnforceStaminaCosts = EnforceStaminaCosts;
            s.StaminaCostMessages = StaminaCostMessages;
            s.ForgeStaminaCampRate = ForgeStaminaCampRate;
            s.ForgeStaminaMarchRate = ForgeStaminaMarchRate;
            s.RefineXpCap = RefineXpCap;
            s.ArmouryProtectUsed = ArmouryProtectUsed;
            s.QuartermasterShouts = QuartermasterShouts;
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
            s.WearEnabled = WearEnabled;
            s.ShowConditionPercent = ShowConditionPercent;
            s.ConditionScalesStats = ConditionScalesStats;
            s.ConditionPenaltyMax = ConditionPenaltyMax;
            s.ConditionPenaltyExponent = ConditionPenaltyExponent;
            s.WearPerBattle = WearPerBattle;
            s.WearDamageFactor = WearDamageFactor;
            s.MissileArmorWearPercent = MissileArmorWearPercent;
            s.HarnessWearFactor = HarnessWearFactor;
            s.DurabilityPerArmorPoint = DurabilityPerArmorPoint;
            s.WearWeaponPerHit = WearWeaponPerHit;
            s.WearShieldFactor = WearShieldFactor;
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
            s.AleSupplyEnabled = AleSupplyEnabled;
            s.AleSupplyBeerFloor = AleSupplyBeerFloor;
            s.AleSupplyWineFloor = AleSupplyWineFloor;
            s.AleSupplyPerDay = AleSupplyPerDay;
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
            s.LootArrivesWorn = LootArrivesWorn;
            s.LootWearBase = LootWearBase;
            s.LootWearSpread = LootWearSpread;
            s.CaptiveSpoilsEnabled = CaptiveSpoilsEnabled;
            s.CaptiveSpoilsIncludeMounts = CaptiveSpoilsIncludeMounts;
            s.BattlefieldLawEnabled = BattlefieldLawEnabled;
            s.LootArrivesBattleWorn = LootArrivesBattleWorn;
            s.SimBattleFullDrop = SimBattleFullDrop;
            s.PlayerLootSharePercent = PlayerLootSharePercent;
            s.WreckSalvageEnabled = WreckSalvageEnabled;
            s.MinSellPercentOfValue = MinSellPercentOfValue;
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
            s.LongYearEnabled = LongYearEnabled;
            s.WeeksPerSeason = WeeksPerSeason;
            s.MarchPaceEnabled = MarchPaceEnabled;
            s.MarchPaceAiToo = MarchPaceAiToo;
            s.MarchFootPace = MarchFootPace;
            s.MarchTrainPace = MarchTrainPace;
            s.MarchRiderPace = MarchRiderPace;
            s.MarchPackAllowance = MarchPackAllowance;
            s.MarketGlutEnabled = MarketGlutEnabled;
            s.MarketGlutStartPercent = MarketGlutStartPercent;
            s.MarketGlutDropPP = MarketGlutDropPP;
            s.MarketGlutMinPercent = MarketGlutMinPercent;
            s.MarketGlutRecoverPerDay = MarketGlutRecoverPerDay;
            s.NightRestEnabled = NightRestEnabled;
            s.SleepHoursNeeded = SleepHoursNeeded;
            s.DayRestFactor = DayRestFactor;
            s.QuickCampKey = QuickCampKey;
            s.SleepAtSeaFree = SleepAtSeaFree;
            s.AiCampsAtNight = AiCampsAtNight;
            s.AiBanditsCampToo = AiBanditsCampToo;
            s.AiTentCap = AiTentCap;
            s.AiTentRadius = AiTentRadius;
            s.AiCampSkipPercent = AiCampSkipPercent;
            s.BanditsRestByDay = BanditsRestByDay;
            s.AiNightsAwakeInChase = AiNightsAwakeInChase;
            s.AiCampDangerRadius = AiCampDangerRadius;
            s.CampTentIcon = CampTentIcon;
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
            s.SmithRepairHoursPerPiece = SmithRepairHoursPerPiece;
            s.SelfRepairHoursPerPiece = SelfRepairHoursPerPiece;
            s.MendLootHoursPerPiece = MendLootHoursPerPiece;
            s.MendMaterialMaxShare = MendMaterialMaxShare;
            s.TakeApartEnabled = TakeApartEnabled;
            s.TakeApartBaseChance = TakeApartBaseChance;
            s.TakeApartSkillSpan = TakeApartSkillSpan;
            s.TakeApartSalvage = TakeApartSalvage;
            s.SmeltHoursPerTier = SmeltHoursPerTier;
            s.CompanionHelperEnabled = CompanionHelperEnabled;
            s.HelperStaminaRelief = HelperStaminaRelief;
            s.HelperTimeRelief = HelperTimeRelief;
            s.HelperSkillForFullRelief = HelperSkillForFullRelief;
            s.LogEnabled = LogEnabled;
        }

        internal static void Apply()
        {
            try { var i = Instance; if (i != null) i.ApplyTo(Settings.Current); }
            catch (System.Exception e) { Log.Error("Mcm.Apply", e); }
        }
    }
}