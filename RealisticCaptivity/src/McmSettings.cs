using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;

namespace RealisticCaptivity
{
    /// <summary>Ustawienia w MCM. Plik XML dziala dalej jako wartosci startowe.</summary>
    public class McmSettings : AttributeGlobalSettings<McmSettings>
    {
        public override string Id => "RealisticCaptivity";
        public override string DisplayName => "Realistic Captivity";
        public override string FolderName => "RealisticCaptivity";
        public override string FormatType => "json2";

        [SettingPropertyBool("Strip Equipment", HintText = "your captor takes your armour and weapons")]
        [SettingPropertyGroup("Gear and plunder")]
        public bool StripEquipment { get; set; } = true;

        [SettingPropertyBool("Keep Civilian Clothes", HintText = "you are left your plain clothes")]
        [SettingPropertyGroup("Gear and plunder")]
        public bool KeepCivilianClothes { get; set; } = true;

        [SettingPropertyBool("Leave In Rags", HintText = "even the clothes go - you are left in rags")]
        [SettingPropertyGroup("Gear and plunder")]
        public bool LeaveInRags { get; set; } = true;

        [SettingPropertyInteger("Loot Party Inventory Percent", 0, 300, "0", HintText = "percent of the baggage train they seize")]
        [SettingPropertyGroup("Gear and plunder")]
        public int LootPartyInventoryPercent { get; set; } = 75;

        [SettingPropertyInteger("Loot Gold Percent", 0, 240, "0", HintText = "percent of the gold on your person they take")]
        [SettingPropertyGroup("Gear and plunder")]
        public int LootGoldPercent { get; set; } = 60;

        [SettingPropertyBool("Buyback Enabled", HintText = "your gear can be bought back from whoever holds it")]
        [SettingPropertyGroup("Gear and plunder")]
        public bool BuybackEnabled { get; set; } = true;

        [SettingPropertyFloatingInteger("Buyback Price Multiplier", 0.00f, 6.40f, "0.00", HintText = "times the market value they ask for it")]
        [SettingPropertyGroup("Gear and plunder")]
        public float BuybackPriceMultiplier { get; set; } = 1.6f;

        [SettingPropertyInteger("Min Days Before Escape", 0, 48, "0", HintText = "days behind the door before escape is possible at all")]
        [SettingPropertyGroup("Escape")]
        public int MinDaysBeforeEscape { get; set; } = 12;

        [SettingPropertyFloatingInteger("Escape Chance Multiplier", 0.00f, 1.00f, "0.00", HintText = "escape odds against vanilla (1.0 = unchanged)")]
        [SettingPropertyGroup("Escape")]
        public float EscapeChanceMultiplier { get; set; } = 0.2f;

        [SettingPropertyInteger("Failed Escape Health Loss Percent", 0, 140, "0", HintText = "health lost when they catch you at it")]
        [SettingPropertyGroup("Escape")]
        public int FailedEscapeHealthLossPercent { get; set; } = 35;

        [SettingPropertyInteger("Failed Escape Relation Penalty", -24, 0, "0", HintText = "relation lost with your captor for trying")]
        [SettingPropertyGroup("Escape")]
        public int FailedEscapeRelationPenalty { get; set; } = -8;

        [SettingPropertyBool("Parole Enabled", HintText = "a captor may accept your word not to flee")]
        [SettingPropertyGroup("Word of honour")]
        public bool ParoleEnabled { get; set; } = true;

        [SettingPropertyBool("Parole Requires Status", HintText = "parole only for someone of standing - land, title or renown")]
        [SettingPropertyGroup("Word of honour")]
        public bool ParoleRequiresStatus { get; set; } = true;

        [SettingPropertyInteger("Parole Min Renown", 0, 600, "0", HintText = "renown at which you count as somebody")]
        [SettingPropertyGroup("Word of honour")]
        public int ParoleMinRenown { get; set; } = 150;

        [SettingPropertyFloatingInteger("Lowborn Ransom Delay", 0.00f, 8.00f, "0.00", HintText = "a nobody waits this many times longer for a ransom offer")]
        [SettingPropertyGroup("Word of honour")]
        public float LowbornRansomDelay { get; set; } = 2.0f;

        [SettingPropertyInteger("Parole Renown Loss", 0, 320, "0", HintText = "renown lost for breaking your word")]
        [SettingPropertyGroup("Word of honour")]
        public int ParoleRenownLoss { get; set; } = 80;

        [SettingPropertyInteger("Parole Relation Penalty", -120, 0, "0", HintText = "relation lost for breaking your word")]
        [SettingPropertyGroup("Word of honour")]
        public int ParoleRelationPenalty { get; set; } = -40;

        [SettingPropertyFloatingInteger("Parole Ransom Discount", 0.00f, 3.00f, "0.00", HintText = "ransom is cheaper once you have given your word")]
        [SettingPropertyGroup("Word of honour")]
        public float ParoleRansomDiscount { get; set; } = 0.75f;

        [SettingPropertyFloatingInteger("Ransom Multiplier", 0.00f, 20.00f, "0.00", HintText = "times the vanilla ransom sum")]
        [SettingPropertyGroup("Ransom")]
        public float RansomMultiplier { get; set; } = 5.0f;

        [SettingPropertyFloatingInteger("Ransom Renown Factor", 0.00f, 120.00f, "0.00", HintText = "extra gold demanded per point of your renown")]
        [SettingPropertyGroup("Ransom")]
        public float RansomRenownFactor { get; set; } = 30f;

        [SettingPropertyInteger("Min Days Before Ransom Offer", 0, 16, "0", HintText = "days before anyone bothers to name a price")]
        [SettingPropertyGroup("Ransom")]
        public int MinDaysBeforeRansomOffer { get; set; } = 4;

        [SettingPropertyBool("Prisoner Sale Floor", HintText = "BannerKings pays the local slave price for common prisoners - in a slave-glutted town that is zero; guarantee at least the old broker rate")]
        [SettingPropertyGroup("Selling prisoners")]
        public bool PrisonerSaleFloor { get; set; } = true;

        [SettingPropertyFloatingInteger("Prisoner Sale Floor Factor", 0.00f, 4.00f, "0.00", HintText = "times the vanilla broker rate (a quarter of the man's recruitment cost)")]
        [SettingPropertyGroup("Selling prisoners")]
        public float PrisonerSaleFloorFactor { get; set; } = 1f;

        [SettingPropertyBool("Fence Gear When No Lord", HintText = "bandits sell your gear at the nearest market")]
        [SettingPropertyGroup("Bandit plunder")]
        public bool FenceGearWhenNoLord { get; set; } = true;

        [SettingPropertyFloatingInteger("Fence Price Multiplier", 0.00f, 4.00f, "0.00", HintText = "what the fence charges (1.0 = market price)")]
        [SettingPropertyGroup("Bandit plunder")]
        public float FencePriceMultiplier { get; set; } = 1.0f;

        [SettingPropertyBool("Starvation Enabled", HintText = "poor food and cold cells wear a prisoner down")]
        [SettingPropertyGroup("Hunger in the cells")]
        public bool StarvationEnabled { get; set; } = true;

        [SettingPropertyInteger("Starvation Health Per Day", 0, 16, "0", HintText = "health lost per day in the cells")]
        [SettingPropertyGroup("Hunger in the cells")]
        public int StarvationHealthPerDay { get; set; } = 4;

        [SettingPropertyFloatingInteger("Starvation Lowborn Factor", 0.00f, 7.00f, "0.00", HintText = "a nobody is fed worse - times the damage")]
        [SettingPropertyGroup("Hunger in the cells")]
        public float StarvationLowbornFactor { get; set; } = 1.75f;

        [SettingPropertyFloatingInteger("Starvation Parole Factor", 0.00f, 1.60f, "0.00", HintText = "on parole the conditions are lighter")]
        [SettingPropertyGroup("Hunger in the cells")]
        public float StarvationParoleFactor { get; set; } = 0.4f;

        [SettingPropertyInteger("Atrophy Chance Percent Per Day", 0, 100, "0", HintText = "daily chance the wasting costs you a point of a physical skill")]
        [SettingPropertyGroup("Hunger in the cells")]
        public int AtrophyChancePercentPerDay { get; set; } = 25;

        [SettingPropertyInteger("Atrophy Points Per Hit", 0, 10, "0", HintText = "points lost at once when it happens")]
        [SettingPropertyGroup("Hunger in the cells")]
        public int AtrophyPointsPerHit { get; set; } = 1;

        [SettingPropertyInteger("Atrophy Min Skill Value", 0, 80, "0", HintText = "the wasting will not take a skill below this")]
        [SettingPropertyGroup("Hunger in the cells")]
        public int AtrophyMinSkillValue { get; set; } = 20;

        [SettingPropertyBool("Homes Enabled", HintText = "houses to own in towns and villages")]
        [SettingPropertyGroup("A roof of your own")]
        public bool HomesEnabled { get; set; } = true;

        [SettingPropertyBool("Family Home Free", HintText = "the hearth you were born under is yours from the start")]
        [SettingPropertyGroup("A roof of your own")]
        public bool FamilyHomeFree { get; set; } = true;

        [SettingPropertyInteger("Home Price Town", 0, 16000, "0", HintText = "base price of a town house")]
        [SettingPropertyGroup("A roof of your own")]
        public int HomePriceTown { get; set; } = 4000;

        [SettingPropertyFloatingInteger("Home Price Prosperity Factor", 0.00f, 2.00f, "0.00", HintText = "plus this per point of prosperity")]
        [SettingPropertyGroup("A roof of your own")]
        public float HomePriceProsperityFactor { get; set; } = 0.5f;

        [SettingPropertyInteger("Home Price Village", 0, 6000, "0", HintText = "base price of a village house")]
        [SettingPropertyGroup("A roof of your own")]
        public int HomePriceVillage { get; set; } = 1500;

        [SettingPropertyFloatingInteger("Home Price Hearth Factor", 0.00f, 6.00f, "0.00", HintText = "plus this per hearth of the village")]
        [SettingPropertyGroup("A roof of your own")]
        public float HomePriceHearthFactor { get; set; } = 1.5f;

        [SettingPropertyFloatingInteger("Home Sell Factor", 0.00f, 2.40f, "0.00", HintText = "a buyer gives this share of the price")]
        [SettingPropertyGroup("A roof of your own")]
        public float HomeSellFactor { get; set; } = 0.6f;

        [SettingPropertyBool("Bandit Dump Enabled", HintText = "brigands do not feed a worthless mouth for weeks")]
        [SettingPropertyGroup("Cast out by brigands")]
        public bool BanditDumpEnabled { get; set; } = true;

        [SettingPropertyInteger("Bandit Dump After Days", 0, 20, "0", HintText = "they give a ransom this many days to appear")]
        [SettingPropertyGroup("Cast out by brigands")]
        public int BanditDumpAfterDays { get; set; } = 5;

        [SettingPropertyInteger("Bandit Dump Worthless Gold", 0, 3200, "0", HintText = "under this much coin you are not worth holding")]
        [SettingPropertyGroup("Cast out by brigands")]
        public int BanditDumpWorthlessGold { get; set; } = 800;

        [SettingPropertyInteger("Bandit Dump Chance Percent Per Day", 0, 80, "0", HintText = "then, each day, they may simply cut you loose")]
        [SettingPropertyGroup("Cast out by brigands")]
        public int BanditDumpChancePercentPerDay { get; set; } = 20;

        [SettingPropertyFloatingInteger("Bandit Dump Dying Factor", 0.00f, 8.00f, "0.00", HintText = "a prisoner near death is dumped twice as eagerly")]
        [SettingPropertyGroup("Cast out by brigands")]
        public float BanditDumpDyingFactor { get; set; } = 2f;

        [SettingPropertyBool("Sell Prisoner Enabled", HintText = "a captor may sell you on to someone else")]
        [SettingPropertyGroup("Being sold on")]
        public bool SellPrisonerEnabled { get; set; } = true;

        [SettingPropertyInteger("Sell Chance Percent Per Day", 0, 32, "0", HintText = "daily chance of being sold on")]
        [SettingPropertyGroup("Being sold on")]
        public int SellChancePercentPerDay { get; set; } = 8;

        [SettingPropertyInteger("Sell Min Days", 0, 20, "0", HintText = "days before anyone thinks of selling you")]
        [SettingPropertyGroup("Being sold on")]
        public int SellMinDays { get; set; } = 5;

        [SettingPropertyBool("Strip Companions", HintText = "captured companions are stripped as well")]
        [SettingPropertyGroup("Companions")]
        public bool StripCompanions { get; set; } = true;

        [SettingPropertyBool("Ransom Debt Enabled", HintText = "you may pledge the ransom as a debt and walk out today")]
        [SettingPropertyGroup("Ransom debt")]
        public bool RansomDebtEnabled { get; set; } = true;

        [SettingPropertyInteger("Debt Offer After Days", 0, 80, "0", HintText = "days before the offer of credit is made")]
        [SettingPropertyGroup("Ransom debt")]
        public int DebtOfferAfterDays { get; set; } = 20;

        [SettingPropertyFloatingInteger("Debt Interest", 0.00f, 5.60f, "0.00", HintText = "times more than a ransom paid in coin")]
        [SettingPropertyGroup("Ransom debt")]
        public float DebtInterest { get; set; } = 1.4f;

        [SettingPropertyInteger("Debt Daily Instalment", 0, 1000, "0", HintText = "gold taken from you each day until it is cleared")]
        [SettingPropertyGroup("Ransom debt")]
        public int DebtDailyInstalment { get; set; } = 250;

        [SettingPropertyInteger("Debt Grace Days", 0, 240, "0", HintText = "days of missed payment before the disgrace lands")]
        [SettingPropertyGroup("Ransom debt")]
        public int DebtGraceDays { get; set; } = 60;

        [SettingPropertyInteger("Debt Default Renown Loss", 0, 200, "0", HintText = "renown lost for defaulting on the debt")]
        [SettingPropertyGroup("Ransom debt")]
        public int DebtDefaultRenownLoss { get; set; } = 50;

        [SettingPropertyBool("Escape Needs Help", HintText = "no one walks out of a cell alone - it takes help")]
        [SettingPropertyGroup("Escape needs help")]
        public bool EscapeNeedsHelp { get; set; } = true;

        [SettingPropertyInteger("Escape Bribe Gold", 0, 3200, "0", HintText = "what your clan pays the gaoler")]
        [SettingPropertyGroup("Escape needs help")]
        public int EscapeBribeGold { get; set; } = 800;

        [SettingPropertyBool("Rescue Enabled", HintText = "your people ride out to free you or to talk terms")]
        [SettingPropertyGroup("Rescue")]
        public bool RescueEnabled { get; set; } = true;

        [SettingPropertyFloatingInteger("Rescue Arrival Distance", 0.00f, 12.00f, "0.00", HintText = "how close they must get before they act")]
        [SettingPropertyGroup("Rescue")]
        public float RescueArrivalDistance { get; set; } = 3.0f;

        [SettingPropertyFloatingInteger("Rescue Strength Ratio", 0.00f, 4.40f, "0.00", HintText = "how many times stronger they must be to storm a bandit camp")]
        [SettingPropertyGroup("Rescue")]
        public float RescueStrengthRatio { get; set; } = 1.1f;

        [SettingPropertyFloatingInteger("Negotiation Max Discount", 0.00f, 2.40f, "0.00", HintText = "the best discount your envoy can talk a lord down to")]
        [SettingPropertyGroup("Rescue")]
        public float NegotiationMaxDiscount { get; set; } = 0.6f;

        [SettingPropertyInteger("Rescue Retry Days", 0, 16, "0", HintText = "days before they try again after being driven off")]
        [SettingPropertyGroup("Rescue")]
        public int RescueRetryDays { get; set; } = 4;

        [SettingPropertyBool("Humiliation Enabled", HintText = "captivity itself costs you standing")]
        [SettingPropertyGroup("Humiliation")]
        public bool HumiliationEnabled { get; set; } = true;

        [SettingPropertyInteger("Humiliation Renown Loss", 0, 60, "0", HintText = "renown lost for having been taken")]
        [SettingPropertyGroup("Humiliation")]
        public int HumiliationRenownLoss { get; set; } = 15;

        [SettingPropertyBool("Clean Break Enabled", HintText = "a fully mounted party that flees a battle cannot be run down by pursuers on foot")]
        [SettingPropertyGroup("A clean break")]
        public bool CleanBreakEnabled { get; set; } = true;

        [SettingPropertyInteger("Clean Break Hours", 0, 16, "0", HintText = "for this many hours that band cannot force another fight on you")]
        [SettingPropertyGroup("A clean break")]
        public int CleanBreakHours { get; set; } = 4;

        [SettingPropertyBool("Clean Break Needs Standing", HintText = "the clean break belongs to a man who ACTUALLY RODE OFF - he sounded the retreat or rode away from the encounter. Cut down on the field, he is taken like anyone else")]
        [SettingPropertyGroup("A clean break")]
        public bool CleanBreakNeedsStanding { get; set; } = true;

        [SettingPropertyBool("Mounted Encounter Flight", HintText = "whole party in the saddle may try to ride away from an encounter - rearguard only against the enemy horse")]
        [SettingPropertyGroup("A clean break")]
        public bool MountedEncounterFlight { get; set; } = true;

        [SettingPropertyBool("Work Enabled", HintText = "day labour and guard work for hire in settlements")]
        [SettingPropertyGroup("Honest work")]
        public bool WorkEnabled { get; set; } = true;

        [SettingPropertyInteger("Work Max Party Size", 0, 20, "0", HintText = "with more mouths than this behind you, no one hires you as a hand")]
        [SettingPropertyGroup("Honest work")]
        public int WorkMaxPartySize { get; set; } = 5;

        [SettingPropertyInteger("Work Only Below Gold", 0, 10000, "0", HintText = "day labour offered only while your purse is under this (0 = always)")]
        [SettingPropertyGroup("Honest work")]
        public int WorkOnlyBelowGold { get; set; } = 2500;

        [SettingPropertyFloatingInteger("Work Pay Village Base", 0.00f, 60.00f, "0.00", HintText = "village day wage before the hearth bonus")]
        [SettingPropertyGroup("Honest work")]
        public float WorkPayVillageBase { get; set; } = 15f;

        [SettingPropertyFloatingInteger("Work Pay Village Hearth Div", 0.00f, 160.00f, "0.00", HintText = "village hearths divided by this are added to the wage")]
        [SettingPropertyGroup("Honest work")]
        public float WorkPayVillageHearthDiv { get; set; } = 40f;

        [SettingPropertyFloatingInteger("Work Pay Town Base", 0.00f, 80.00f, "0.00", HintText = "town day wage before the prosperity bonus")]
        [SettingPropertyGroup("Honest work")]
        public float WorkPayTownBase { get; set; } = 20f;

        [SettingPropertyFloatingInteger("Work Pay Town Prosperity Div", 0.00f, 1200.00f, "0.00", HintText = "town prosperity divided by this is added to the wage")]
        [SettingPropertyGroup("Honest work")]
        public float WorkPayTownProsperityDiv { get; set; } = 300f;

        [SettingPropertyInteger("Work Athletics Xp Per Day", 0, 100, "0", HintText = "athletics practice from a day of hard graft")]
        [SettingPropertyGroup("Honest work")]
        public int WorkAthleticsXpPerDay { get; set; } = 25;

        [SettingPropertyInteger("Work Saturation Days", 0, 40, "0", HintText = "after this many days in one place the wage halves")]
        [SettingPropertyGroup("Honest work")]
        public int WorkSaturationDays { get; set; } = 10;

        [SettingPropertyInteger("Work Saturation Rest Days", 0, 20, "0", HintText = "days away before the place forgets you and pays full again")]
        [SettingPropertyGroup("Honest work")]
        public int WorkSaturationRestDays { get; set; } = 5;

        [SettingPropertyBool("Guard Work Enabled", HintText = "night-watch work for hire in towns")]
        [SettingPropertyGroup("Honest work")]
        public bool GuardWorkEnabled { get; set; } = true;

        [SettingPropertyInteger("Guard Only Below Gold", 0, 40000, "0", HintText = "guard work offered only while your purse is under this (0 = always)")]
        [SettingPropertyGroup("Honest work")]
        public int GuardOnlyBelowGold { get; set; } = 10000;

        [SettingPropertyInteger("Guard Skill Required", 0, 160, "0", HintText = "best weapon skill the merchants demand of a hired guard")]
        [SettingPropertyGroup("Honest work")]
        public int GuardSkillRequired { get; set; } = 40;

        [SettingPropertyFloatingInteger("Guard Pay Base", 0.00f, 180.00f, "0.00", HintText = "night-watch wage before the prosperity bonus")]
        [SettingPropertyGroup("Honest work")]
        public float GuardPayBase { get; set; } = 45f;

        [SettingPropertyFloatingInteger("Guard Pay Prosperity Div", 0.00f, 600.00f, "0.00", HintText = "town prosperity divided by this is added to the wage")]
        [SettingPropertyGroup("Honest work")]
        public float GuardPayProsperityDiv { get; set; } = 150f;

        [SettingPropertyInteger("Guard Weapon Xp Per Day", 0, 160, "0", HintText = "weapon practice from a night on the walls")]
        [SettingPropertyGroup("Honest work")]
        public int GuardWeaponXpPerDay { get; set; } = 40;

        [SettingPropertyInteger("Guard Brawl Chance Percent", 0, 60, "0", HintText = "nightly chance of trouble at the gates")]
        [SettingPropertyGroup("Honest work")]
        public int GuardBrawlChancePercent { get; set; } = 15;

        [SettingPropertyInteger("Guard Brawl Bonus", 0, 200, "0", HintText = "extra pay for cracking heads when trouble comes")]
        [SettingPropertyGroup("Honest work")]
        public int GuardBrawlBonus { get; set; } = 50;

        [SettingPropertyInteger("Guard Brawl Health Loss", 0, 120, "0", HintText = "health lost when the toughs get the better of you")]
        [SettingPropertyGroup("Honest work")]
        public int GuardBrawlHealthLoss { get; set; } = 30;

        [SettingPropertyBool("Enlisted Honest Wounds", HintText = "no miracle full-heal after enlisted battles - wounds mend with rest, as they should")]
        [SettingPropertyGroup("In the lord's service")]
        public bool EnlistedHonestWounds { get; set; } = true;

        [SettingPropertyInteger("Enlisted Post Battle Heal Hp", 0, 40, "0", HintText = "a field dressing after the fight - this many hit points, no more")]
        [SettingPropertyGroup("In the lord's service")]
        public int EnlistedPostBattleHealHp { get; set; } = 10;

        [SettingPropertyInteger("Enlisted Daily Care Hp", 0, 60, "0", HintText = "the army medicus can restore this much each day")]
        [SettingPropertyGroup("In the lord's service")]
        public int EnlistedDailyCareHp { get; set; } = 15;

        [SettingPropertyBool("Log Enabled", HintText = "write a log file in the module folder")]
        [SettingPropertyGroup("Other")]
        public bool LogEnabled { get; set; } = true;

        public void ApplyTo(Settings s)
        {
            s.StripEquipment = StripEquipment;
            s.KeepCivilianClothes = KeepCivilianClothes;
            s.LeaveInRags = LeaveInRags;
            s.LootPartyInventoryPercent = LootPartyInventoryPercent;
            s.LootGoldPercent = LootGoldPercent;
            s.BuybackEnabled = BuybackEnabled;
            s.BuybackPriceMultiplier = BuybackPriceMultiplier;
            s.MinDaysBeforeEscape = MinDaysBeforeEscape;
            s.EscapeChanceMultiplier = EscapeChanceMultiplier;
            s.FailedEscapeHealthLossPercent = FailedEscapeHealthLossPercent;
            s.FailedEscapeRelationPenalty = FailedEscapeRelationPenalty;
            s.ParoleEnabled = ParoleEnabled;
            s.ParoleRequiresStatus = ParoleRequiresStatus;
            s.ParoleMinRenown = ParoleMinRenown;
            s.LowbornRansomDelay = LowbornRansomDelay;
            s.ParoleRenownLoss = ParoleRenownLoss;
            s.ParoleRelationPenalty = ParoleRelationPenalty;
            s.ParoleRansomDiscount = ParoleRansomDiscount;
            s.RansomMultiplier = RansomMultiplier;
            s.RansomRenownFactor = RansomRenownFactor;
            s.MinDaysBeforeRansomOffer = MinDaysBeforeRansomOffer;
            s.PrisonerSaleFloor = PrisonerSaleFloor;
            s.PrisonerSaleFloorFactor = PrisonerSaleFloorFactor;
            s.FenceGearWhenNoLord = FenceGearWhenNoLord;
            s.FencePriceMultiplier = FencePriceMultiplier;
            s.StarvationEnabled = StarvationEnabled;
            s.StarvationHealthPerDay = StarvationHealthPerDay;
            s.StarvationLowbornFactor = StarvationLowbornFactor;
            s.StarvationParoleFactor = StarvationParoleFactor;
            s.AtrophyChancePercentPerDay = AtrophyChancePercentPerDay;
            s.AtrophyPointsPerHit = AtrophyPointsPerHit;
            s.AtrophyMinSkillValue = AtrophyMinSkillValue;
            s.HomesEnabled = HomesEnabled;
            s.FamilyHomeFree = FamilyHomeFree;
            s.HomePriceTown = HomePriceTown;
            s.HomePriceProsperityFactor = HomePriceProsperityFactor;
            s.HomePriceVillage = HomePriceVillage;
            s.HomePriceHearthFactor = HomePriceHearthFactor;
            s.HomeSellFactor = HomeSellFactor;
            s.BanditDumpEnabled = BanditDumpEnabled;
            s.BanditDumpAfterDays = BanditDumpAfterDays;
            s.BanditDumpWorthlessGold = BanditDumpWorthlessGold;
            s.BanditDumpChancePercentPerDay = BanditDumpChancePercentPerDay;
            s.BanditDumpDyingFactor = BanditDumpDyingFactor;
            s.SellPrisonerEnabled = SellPrisonerEnabled;
            s.SellChancePercentPerDay = SellChancePercentPerDay;
            s.SellMinDays = SellMinDays;
            s.StripCompanions = StripCompanions;
            s.RansomDebtEnabled = RansomDebtEnabled;
            s.DebtOfferAfterDays = DebtOfferAfterDays;
            s.DebtInterest = DebtInterest;
            s.DebtDailyInstalment = DebtDailyInstalment;
            s.DebtGraceDays = DebtGraceDays;
            s.DebtDefaultRenownLoss = DebtDefaultRenownLoss;
            s.EscapeNeedsHelp = EscapeNeedsHelp;
            s.EscapeBribeGold = EscapeBribeGold;
            s.RescueEnabled = RescueEnabled;
            s.RescueArrivalDistance = RescueArrivalDistance;
            s.RescueStrengthRatio = RescueStrengthRatio;
            s.NegotiationMaxDiscount = NegotiationMaxDiscount;
            s.RescueRetryDays = RescueRetryDays;
            s.HumiliationEnabled = HumiliationEnabled;
            s.HumiliationRenownLoss = HumiliationRenownLoss;
            s.CleanBreakEnabled = CleanBreakEnabled;
            s.CleanBreakHours = CleanBreakHours;
            s.CleanBreakNeedsStanding = CleanBreakNeedsStanding;
            s.MountedEncounterFlight = MountedEncounterFlight;
            s.WorkEnabled = WorkEnabled;
            s.WorkMaxPartySize = WorkMaxPartySize;
            s.WorkOnlyBelowGold = WorkOnlyBelowGold;
            s.WorkPayVillageBase = WorkPayVillageBase;
            s.WorkPayVillageHearthDiv = WorkPayVillageHearthDiv;
            s.WorkPayTownBase = WorkPayTownBase;
            s.WorkPayTownProsperityDiv = WorkPayTownProsperityDiv;
            s.WorkAthleticsXpPerDay = WorkAthleticsXpPerDay;
            s.WorkSaturationDays = WorkSaturationDays;
            s.WorkSaturationRestDays = WorkSaturationRestDays;
            s.GuardWorkEnabled = GuardWorkEnabled;
            s.GuardOnlyBelowGold = GuardOnlyBelowGold;
            s.GuardSkillRequired = GuardSkillRequired;
            s.GuardPayBase = GuardPayBase;
            s.GuardPayProsperityDiv = GuardPayProsperityDiv;
            s.GuardWeaponXpPerDay = GuardWeaponXpPerDay;
            s.GuardBrawlChancePercent = GuardBrawlChancePercent;
            s.GuardBrawlBonus = GuardBrawlBonus;
            s.GuardBrawlHealthLoss = GuardBrawlHealthLoss;
            s.EnlistedHonestWounds = EnlistedHonestWounds;
            s.EnlistedPostBattleHealHp = EnlistedPostBattleHealHp;
            s.EnlistedDailyCareHp = EnlistedDailyCareHp;
            s.LogEnabled = LogEnabled;
        }

        internal static void Apply()
        {
            try { var i = Instance; if (i != null) i.ApplyTo(Settings.Current); }
            catch (System.Exception e) { Log.Error("Mcm.Apply", e); }
        }
    }
}