using System;
using System.Globalization;
using System.IO;
using System.Xml;

namespace Armoury
{
    public class Settings
    {
        public static Settings Current = new Settings();

        // --- Forging armour ---
        public bool TidyBannerKingsArmourList = true;       // real categories and tier labels in the Banner Kings armour tab
        public bool ForgeArmourEnabled = false;             // off when Banner Kings' own armour tab in the smithy screen handles forging
        public bool CraftingEnabled = true;                // armour forging on or off
        public int SmithingSkillPerTier = 45;              // Smithing needed per tier - 40 means tier 5 plate wants 200
        public float IronPerWeightUnit = 1.4f;             // refined iron per pound of the finished piece
        // --- Cost by piece ---
        public float ClassCostBody = 1.15f;                // cuirass - the most waste when the plates are cut
        public float ClassCostLeg = 0.85f;                 // greaves and tassets - broad plates, much alloy
        public float ClassCostHead = 0.60f;                // helmet - little steel for the protection it gives
        public float ClassCostHand = 0.40f;                // gauntlets - little steel, a great deal of fiddling
        public float ClassCostCape = 0.65f;                // cloaks and shoulders - mostly cloth and leather
        public float ClassCostHorse = 1.25f;               // barding - a great deal of everything
        public float ClassCostShield = 0.55f;              // shield - timber, hide and a rim of iron
        public float ClassCostRanged = 0.45f;              // bows and bolts - wood, horn and sinew
        public float FiddlyStaminaBonus = 0.5f;            // how much more the fiddly pieces take out of you
        public float CharcoalPerIron = 0.6f;               // charcoal burned per unit of iron
        public int StaminaPerTier = 25;                    // crafting stamina burned per tier
        // --- Time at the anvil ---
        public bool ForgeWorksWithoutYou = true;           // the smith and his lads keep at your project while you ride - the finished piece waits at that forge for collection; off = the clock only runs while you stay in that settlement
        public bool ForgeTakesTime = true;                 // armour is not finished the moment you order it
        public float DaysPerTier = 2f;                     // days per tier - 2 means a tier 5 plate takes ten days
        public float TempoHastyTime = 0.5f;                // in haste: this share of the time
        public float TempoHastyRisk = 2f;                  // in haste: this many times the risk
        public float TempoCarefulTime = 1.5f;              // with care: this many times longer
        public float TempoCarefulRisk = 0.5f;              // with care: this share of the risk
        public float TempoCarefulQuality = 2f;             // with care: this many times the chance of quality
        // --- Experience ---
        public int XpPerDayPerTier = 100;                  // XP for one day of work, per tier of the piece
        public int XpFullCreditMargin = 100;               // you still learn at full pace this far above the recipe
        public float XpDiminishingRange = 250f;            // and only past that margin does the learning taper off
        public float XpFloorFactor = 0.25f;                // the floor, when the work is far beneath your hand
        public int XpCapPerTier = 1300;                    // ceiling per project = this times the tier
        public float XpShareWhileWorking = 0.4f;           // share paid out as you work, the rest on completion
        public bool WeaponCraftingTakesTime = true;        // native weapon smithing is not instant either
        public float WeaponDaysPerTier = 1.0f;             // weapons come faster than armour, but not at once
        public bool WeaponXpFromValueCapped = true;        // native weapon XP follows the sale price - cap it
        public int WeaponXpCapPerTier = 500;               // ceiling per weapon = this times the tier
        // --- Orders from the lords ---
        public bool ArmourOrdersEnabled = true;            // lords leave armour commissions with town smiths
        public float OrderOfferChance = 0.5f;              // chance a fresh offer waits when you ride in
        public float OrderTownCooldownDays = 3f;           // one town does not post offers more often than this
        public float OrderOfferLifeDays = 5f;              // an untaken offer is withdrawn after this long
        public float OrderDeadlineDays = 12f;              // days to deliver once you take the order
        public float OrderPayMultiplier = 1.35f;           // pay above market - the lord skips the middleman
        public int OrderRelationReward = 2;                // relation gained with the lord on delivery
        public int OrderMissRelationPenalty = 2;           // relation lost when the deadline passes
        public int MaxAcceptedOrders = 3;                  // how many orders your book holds at once
        public int OrderMinTier = 2;                       // lords do not commission rags
        public int OrderMaxTier = 5;                       // nor ask a town smith for the impossible
        public int OrderMaxItemValue = 12000;              // cap on the piece's worth
        // --- Forge fee ---
        public int ForgeFeeBase = 75;                      // what the smith charges you to use his forge
        public int ForgeFeePerTier = 60;                   // and this much more for every tier of the work
        public float BkForgeHourlyMultiplier = 0.5f;       // Banner Kings charges by the hour at the anvil - this scales that hourly rate
        public bool ForgeDayPassEnabled = true;            // the smith hires his forge BY THE DAY, paid up front - one hour or twenty-three, same coin
        public float ForgeDayHours = 8f;                   // the day's hire costs this many hours at the smith's rate (~200 gold in an average town)
        public bool ForgeWorkNoRest = true;                // hammering is not napping - smithing stamina does NOT recover during hours spent working the forge
        public bool EnforceStaminaCosts = true;            // every smelt, refine and forging pays its stamina price - if some mod loses the bill, we collect it ourselves
        public bool StaminaCostMessages = true;            // a quiet grey line after each action at the furnace: what it cost and what wind is left
        public float ForgeStaminaCampRate = 8f;            // crafting stamina regained per hour asleep or in camp
        public float ForgeStaminaMarchRate = 3f;           // ...and per hour on the march - a rider is not swinging a hammer
        public int RefineXpCap = 60;                       // refining one batch teaches at most this much Smithing
        public bool ArmouryProtectUsed = true;             // the quartermaster hands out only SURPLUS - gear your soldiers still use cannot leave the troop armoury
        public bool QuartermasterShouts = true;            // the quartermaster reports missing kit OUT LOUD after every battle and each morning - not only in the armoury screen
        public float CharcoalWeight = 0.5f;                // a lump of charcoal weighs this much (vanilla hauls 5 kg bricks; 0 = leave alone)                   // the day's hire costs this many hours at the smith's rate (~200 gold in an average town)
        public bool BkTrueMaterials = true;                // Banner Kings armour crafting uses the honest material rule below instead of its own token amounts
        public float ArmorPointsPerMaterial = 10f;         // one unit of material per this many points of total protection on the piece
        public float ArmorMaterialScale = 0.5f;            // the whole material bill times this - the old bills asked for more leather than markets ever stock
        public int SoftMaterialPerTier = 1;                // leather+linen on one piece cap out at this many per tier - the rest of the bill turns to iron (fittings, rivets)
        public float ArmorTierBonusPercent = 15f;          // each tier above the first adds this much more material
        // --- Mending it yourself ---
        public float SelfRepairMaterialFactor = 0.25f;     // metal needed against the full recipe
        public float SelfRepairStaminaFactor = 0.4f;       // stamina needed against a full piece
        public float FailureChanceAtZeroMargin = 0.35f;    // chance to ruin the piece at exactly the required skill
        public float MarginForNoFailure = 60f;             // skill points above the requirement that remove all risk
        public float MaterialLossOnFailure = 0.5f;         // share of metal lost when you botch it
        public int MaxItemsListed = 24;                    // how many pieces the forge menu lists at once
        public bool AllowRangedCrafting = true;            // bows, crossbows, arrows and bolts as well
        public int AmmoBatchStacks = 3;                    // one fletching job yields this many sheaves of arrows or cases of bolts
        public float RangedStaminaFactor = 0.35f;          // bows and crossbows cost tier x stamina-per-tier x this - bowyery is lighter work than plate, as at the weapon bench
        public float RangedHighTierCostFactor = 2f;        // bows and crossbows of tier 5-6 eat this many times the materials - masterworks are not massed out of a sack of sticks
        public float RangedFailureFactor = 0.5f;           // ruin-risk multiplier for bows, crossbows and ammunition - wood forgives more than a quench
        public float LegendaryValueFloor = 25000f;         // an unbuyable piece worth at least this much is a LEGEND - legendary bills and the one-of-a-kind rule apply
        public float LegendaryMaterialFactor = 4f;         // a legend's bill: every material count multiplied by this, plus the noblest steel on top
        public int LegendarySkillNeeded = 250;             // no legend leaves the forge below this Smithing
        public float SmeltingReturnShare = 0.45f;          // share of the metal that comes back when you break a piece down
        public float SmeltingSkillBonus = 0.002f;          // extra recovery per point of Smithing

        // --- Wear and tear ---
        public bool TroopWearEnabled = true;               // the men's kit wears with every battle: a share of pieces in use drops one condition step - mend it at the smith
        public float TroopWearPercent = 12f;               // this share of pieces IN USE takes one step of wear per battle
        public bool WearEnabled = true;                    // gear loses condition with use
        public bool ShowConditionPercent = true;           // damaged gear carries its state in the name - (100%) is mint, (1%) is a wreck
        public bool ConditionScalesStats = true;           // Jeff's rule: protection and edge follow condition - light wear costs little, heavy wear costs dearly
        public float ConditionPenaltyMax = 90f;            // an all-but-broken piece (1%) still keeps this much less - never the full hundred
        public float ConditionPenaltyExponent = 1.15f;     // the curve: above 1 = small wear is cheap, deep wear bites (99% state ~ -0.5%, 50% ~ -41%, 1% ~ -89%)
        public float WearPerBattle = 0f;                   // flat wear per battle ON TOP of real damage - 0 = gear suffers only when something actually hits it
        public float WearDamageFactor = 0.15f;             // wear per point of damage the STRUCK piece takes (armour wears where the blow lands)
        public float MissileArmorWearPercent = 10f;        // arrows punch tidy little holes, not rents - armour counts only this % of missile damage as wear (hp damage unchanged)
        public float HarnessWearFactor = 0.15f;            // saddle and barding count only this share of the horse's raw hits as wear - at the old half-share saddles kept dying under you
        public float DurabilityPerArmorPoint = 20f;        // Jeff's pool: every point of protection gives this much durability, times the tier - 61 armor at tier 3 = 61 x 20 x 3 = 3660 points, and damage taken subtracts one for one
        public float WearWeaponPerHit = 0.6f;              // wear on your weapon for every blow you land (bows wear per arrow that strikes home)
        public float WearShieldFactor = 0.3f;              // shields are built to take it - blocked damage wears them at this share
        public int BowUsesAtTier1 = 2500;                  // a tier-1 bow survives this many shots; each tier multiplies (tier 3 = x3, tier 6 = x6)
        public float BowSkillBonusPercentPerPoint = 1f;    // every point of Bow/Crossbow skill adds this percent more shots - a trained hand spares the weapon
        public float TierDurabilityFactor = 0.22f;         // each tier of the piece slows the wear by this much
        public int ThresholdWorn = 70;                     // below this the gear starts to show
        public int ThresholdDamaged = 45;                  // below this it is plainly damaged and worth less
        public int ThresholdRuined = 20;                   // below this it is barely worth carrying
        public float RepairCostFactor = 0.5f;              // share of item value for a full repair
        public bool BreakAtZeroCondition = false;          // at zero the piece finally breaks and is gone for good
        // --- Crown jewels ---
        public bool UniqueCrownsEnabled = true;            // crowns of kings and queens are unique regalia: sane armour, out of the shops, impossible to forge - the pieces already in play become the only ones
        public int UniqueCrownHeadArmor = 10;              // head armour of a crown - it is jewellery, not a helmet (ROT ships every crown at 75)

        // --- The lean quartermasters ---
        public int BkSupplyDaysCap = 4;                    // AI parties stock this many days of Banner Kings supplies instead of 10 - healthier logistics than living hand to mouth (0 = off; Jeff 31.08: "daj na 4")
        public int BkSupplyMaxPieces = 12;                 // hard ceiling: an AI party never stockpiles more than this many pieces of any one supply - repairs need a few hides, not a warehouse (0 = off)

        public bool AiStarvingBuysAnyPrice = true;         // a STARVING AI party buys the cheapest food it can afford at ANY price - hunger does not haggle (vanilla and Banner Kings refuse anything above 120 denars, so lords starve on a full market in wartime)

        // --- The crossing law ---
        public bool CrossingLawEnabled = true;             // fortified crossings (The Twins) bar their bridge to ENEMIES of the holder - allies and neutrals pass; take the castle, make peace, or go by sea
        public bool CrossingLawAi = true;                  // the bridge watch also turns back hostile AI lord parties (they get a fallback point and a 3h cooldown so their pathfinding never jams)
        public float CrossingRadius = 3f;                  // how far the bridge watch reaches around the crossing castle

        // --- The slow muster ---
        public int VolunteerRegenPercent = 25;             // notables refill their volunteer slots at this percent of the normal daily chance - losses should STING, for lords and player alike (100 = vanilla, 0 = off; Jeff 30.08: halved again, the towns still teemed with recruits)

        // --- The slow mending ---
        public int HealingRegenPercent = 50;               // wounded men and heroes heal on the map at this percent of the normal daily rate - medicine perks still count on top (100 = vanilla)
        public int AiHealingRegenPercent = 100;            // AI parties heal at this percent of the normal daily rate (100 = vanilla, above 100 = faster) - vanilla tempo keeps lords' wounded from piling up for weeks after a famine or a battle

        // --- The tidy muster ---
        public bool AutoSortParty = true;                  // the party roster keeps itself in order: cavalry, horse archers, infantry, archers - each arm by tier, best first (no more dragging rows by hand)
        public bool MusterBookEnabled = true;              // the muster book in town, village and forge menus: inspect any troop (experience, full kit) and ASSIGN which piece from the stores the whole company of that troop must wear

        // --- The finished piece ---
        public bool CraftResultPopup = true;               // forging armour, bows or ammo ends with a result window: every stat, with the quality bonus or the botch penalty spelled out - same rule as weapons
        public bool RichQualityModifiers = true;           // fine/masterwork/legendary touch MORE than one stat (RBM strips them to bare damage): melee gains speed, ranged gains missile speed, botched work loses both
        public bool TroopSelfMendEnabled = true;           // each day in a town the men pay the smith from their own wages to mend the worst pieces in the company stores
        public int TroopSelfMendPercentPerDay = 10;        // the men mend this PERCENT of all battle-worn pieces in the stores each day in town (at least 3 pieces) - a full refit takes about 100/percent days of rest; pay the smith yourself to skip the wait

        // --- Skills rule the gear ---
        public bool TroopSkillAutoFit = true;              // troops are audited on load: any skill below the demands of their OWN template gear (armour->Athletics, mount->Riding, weapons->their class) is raised to match - the elite keeps its heavy plate because it has earned the muscles
        public bool SkillsDecideEnabled = true;            // no more "default tier +2": troops use ANY gear their stats allow, main weapon follows their best skill, the backup their second best (an archer carries bow, two quivers and a sidearm of his second skill)

        // --- The menagerie ---
        public bool ElephantQuarantineEnabled = true;      // elephants and their barding sell only in settlements of their own culture - no war beasts wintering in Winterfell

        // --- The hideout purge ---
        public bool HideoutPurgeEnabled = true;            // a cleared hideout must be SEARCHED: the plundered gold, renown and the gratitude of the district wait behind one more step
        public int HideoutGoldBase = 150;                  // gold hidden in any den before counting its bands
        public int HideoutGoldPerBand = 120;               // each raiding band that lived there stashed about this much loot from the district
        public float HideoutRenown = 5f;                   // renown for purging a hideout - the realm hears of it
        public int HideoutRepMax = 5;                      // relation gained with notables right next to the den, fading to zero at the edge of the district
        public float HideoutRepRadius = 50f;               // the district: map-distance within which settlements care about the purge
        public float HideoutSearchSoloHours = 24f;         // searching the den alone takes this long - a lone man turns every bedroll himself
        public float HideoutSearchPerManHours = 0.5f;      // every soldier in the party cuts the search by this many hours
        public float HideoutSearchMinHours = 4f;           // the search never drops below this many hours - some stones only come up slowly
        public bool HideoutReprisalEnabled = true;         // nearby bands mass to take their den back while you dig - unless your line scares them off
        public float HideoutReprisalRadius = 40f;          // map-distance within which bandit parties join the reprisal
        public float HideoutReprisalFleeOdds = 3f;         // at this strength advantage (yours vs theirs) the reprisal loses its nerve and melts away
        public float HideoutReprisalHours = 48f;           // the horde hunts you this long - then gives up and drifts back to its old ways

        // --- Loot from the field ---
        public bool LootArrivesWorn = true;                // gear stripped from the fallen comes to you already used
        public float LootWearBase = 45f;                   // the condition of an average piece of loot
        public float LootWearSpread = 25f;                 // how widely that varies
        public bool CaptiveSpoilsEnabled = true;           // a man you take captive is stripped at the rope: his whole kit lands in your baggage, battle-worn
        public bool CaptiveSpoilsIncludeMounts = true;     // his horse and harness are seized too
        public bool CaptiveRagsPreview = true;             // the party screen shows your captives the way you actually keep them: stripped to rags, not in the full armour you already took (icons stay, only the 3D preview changes)
        // --- The law of the battlefield ---
        public bool BattlefieldLawEnabled = true;          // battles you fight yourself: the dead drop their real gear (DTE), Spoils of War steps aside
        public bool LootArrivesBattleWorn = true;          // loot-screen items from fought battles arrive battle-worn but keep their worth
        public bool SimBattleFullDrop = true;              // auto-resolved battles ignore the hidden per-tier drop multipliers
        public int PlayerLootSharePercent = 30;            // your cut of what the party strips from the dead; the rest stays in the army armoury (a lone wanderer takes all) - 30 = the historical captain's third (Jeff 30.08)
        public bool WreckSalvageEnabled = true;            // the piece smashed by the killing blow is not lost - it lands in the loot as a wreck to mend at the forge
        public int LootMinConditionPercent = 3;            // gear battered down to this percent of its worth or less is DESTROYED - it never reaches the loot screen (0 = off)
        public int LegendaryLootValueFloor = 100000;       // weapons worth this much clean (the named blades of the realm) never lie in the common loot sacks (0 = off)
        public int MinSellPercentOfValue = 5;              // merchants never pay less than this share of an item's clean value - scrap is still metal and leather (0 = off)
        public bool EnlistedSoldierNoLooting = true;       // serving in a lord's army: the quartermasters strip the field - one soldier does not pocket the army's loot and gold

        // --- Flesh and wind ---
        public bool FieldCraftEnabled = true;              // battlefield body rules: sprint fatigue, wounded penalties, bleeding, arrows that barely scratched fall off
        public bool SprintFatigueEnabled = true;           // running on foot drinks stamina points - an empty pool means a slow man
        public bool UseRbmStamina = true;                  // sprint drinks from RBM's own stamina pool (Athletics already grows it); off = our own pool with the same rules
        public float SprintDrainPerSecond = 4f;            // stamina points one second of flat-out sprint costs, before the armour is weighed
        public float SprintDrainPerKg = 1f;                // extra points per second for every kilogram of armour carried
        public float FatigueFreeArmorKg = 0f;              // armour up to this weight costs no extra stamina
        public bool BattleStaminaEnabled = true;           // heroes: Endurance reshapes RBM battle stamina and posture - pools double every few points, breath returns fast but winded men pant
        public float BattleEndDoubleEvery = 2.5f;          // pool doubles every this many Endurance points (END 2.5 = x1, END 5 = x2, END 10 = x8)
        public float BattleRegenAtEnd1 = 10f;              // stamina regained per second at Endurance 1 (Athletics adds its share on top)
        public float BattleRegenAtEnd10 = 200f;            // stamina regained per second at Endurance 10 - the curve between is exponential, not a straight line
        public float BattleWindedFloor = 0.25f;            // share of regen left with an empty bar - the emptier the lungs the slower they fill
        public float StaminaRegenPerSecond = 25f;          // without RBM: points regained each second of easing off (with RBM its own regen rules)
        public float TiredSpeedFactor = 0.78f;             // top speed of a man whose pool has run dry
        public bool WoundedPenaltiesEnabled = true;        // hurt men move and swing slower, the worse the wound the worse the arm
        public int WoundedBelowPercent = 50;               // penalties begin below this share of health
        public float WoundedMaxSlow = 0.3f;                // top movement penalty at death's door
        public int BleedBelowHp = 10;                      // bleeding starts at this many hit points or fewer
        public int AiFleeBelowPercent = 10;                // AI soldiers below this share of health break and run
        public float BleedPerSecond = 0.5f;                // health lost per second while bleeding out
        public bool AiFleeWhenNearDeath = true;            // AI soldiers below the bleeding threshold break and run for their lives
        public bool ArrowUnstickEnabled = true;            // arrows the armour stopped do not stay stuck in a man (shields keep theirs)
        public int ArrowStickMinDamage = 8;                // an arrow must deal at least this to lodge in flesh
        public bool WalkKeyEnabled = true;                 // hold Left Ctrl on foot to walk instead of run - the wind comes back as you stroll
        public float WalkSpeedShare = 0.42f;               // walking pace as a share of full speed
        public bool HorseDeathPermanent = true;            // your mount killed in a real battle is truly gone - the slot empties, only the harness comes off the corpse
        public bool ThrownWobbleEnabled = true;            // javelins are not sniper rifles - thrown weapons scatter properly, worse still from the saddle
        public float ThrownInaccuracyFactor = 2.5f;        // spread multiplier for every thrown weapon (vanilla javelins fly absurdly true)
        public float ThrownMountedInaccuracyFactor = 2f;   // extra spread on top when hurling from horseback - a moving horse is no throwing platform
        public bool ChargeTemperEnabled = true;            // RBM charge damage = horse mass x speed, so even a slow bump crushes men - temper it
        public float ChargeDamageFactor = 0.6f;            // multiplier on mounted charge damage
        public float ChargeFullSpeed = 7f;                 // full charge damage only at this speed (m/s) and above - slower rides pay proportionally less

        // --- The master's parry ---
        public bool AutoParryEnabled = true;               // hold block and your skill does the aiming: if your weapon skill beats the attacker's, the block turns to meet his blow
        public bool WoundedFleeEnabled = true;             // a man too hurt to fight turns and runs instead of standing there screaming
        public float WoundedFleePercent = 20f;             // health left (percent) below which an AI fighter breaks and withdraws
        public bool WoundedFleeHeroes = false;             // heroes and companions break too (off: captains hold their ground)
        public bool WoundedFleeEnemiesOnly = false;        // only the enemy breaks (off: every man on the field, both sides)
        public bool WoundedFleeWithoutPlayer = true;       // also in battles the player is not personally fighting
        public float AutoParryFullDiff = 25f;              // your lead IS your chance: each point of skill lead gives (100 / this) % odds per swing, certain at this many points (25 = 4% a point: 13 pts -> 52%, 1 pt -> 4%)
        public bool AutoParryTwoHandedOnly = true;         // the master's parry works only with two-handed blades (off = any melee weapon)
        public bool AutoParryMirrorSides = true;           // side swings block mirror-wise (his left = your right) - flip if blocks feel wrong-sided

        // --- A knight needs a horse ---
        public bool CavalryNeedsMounts = true;             // upgrading a man into a MOUNTED troop takes a mount from the party inventory - yours and the AI's alike, one horse per man, gone on upgrade
        public int WarHorseFromTier = 4;                   // from this tier the upgrade demands a proper WAR horse
        public int NobleHorseFromTier = 6;                 // from this tier nothing but a noble steed will do
        public bool AiBuysMounts = true;                   // lords restock their stables when they ride into a settlement - the AI keeps its cavalry instead of slowly losing it
        public int AiMountSpareBuffer = 2;                 // a few head over the count actually waiting to be raised to horse - for losses on the road
        public float AiMountPurseShare = 0.15f;            // he never spends more than this share of his purse on horses in one visit
        public int AiMountMaxPerVisit = 10;                // and never buys more than this many head at one stop - a stable is topped up, not a herd bought
        public float AiMountBuyCooldownDays = 4f;          // days before that same party restocks again: without a pause the AI resold the horses as ordinary goods and bought them back, pumping millions through the market
        public bool AiMountBreederFallback = true;         // market empty? he orders from the local breeder instead of riding away horseless
        public float AiMountBreederMarkup = 1.3f;          // the breeder charges this much over the plain worth for the trouble

        // --- The turning year ---
        public bool LongYearEnabled = true;                // stretch the year so the world stops racing: children grow, lords age and seasons turn at a pace a long campaign can live with
        public int WeeksPerSeason = 6;                     // weeks in a season (vanilla 3 = an 84-day year; 6 = a 168-day year with 42-day seasons). SET IT BEFORE STARTING A CAMPAIGN and leave it alone afterwards

        // --- The marching column ---
        public bool MarchPaceEnabled = true;               // a column moves at the pace of its slowest man: any soldier or prisoner on foot holds the whole party to walking speed
        public int WorldPacePercent = 50;                  // base map speed of EVERY party - 50% matches the doubled year: Winterfell to King's Landing takes a lore-true month of the 168-day calendar
        public int SiegePacePercent = 50;                  // siege engine construction speed - 50% makes sieges last twice as long, so starving a fortress out matters again
        public bool MarchPaceAiToo = true;                 // the same law binds lords, bandits and patrols (villagers and caravans keep their own pace either way)
        public float MarchFootPace = 4.0f;                 // map speed cap while anyone walks - footmen without a spare mount, or prisoners on the rope
        public float MarchTrainPace = 4.2f;                // map speed cap for an all-riding party that still drags a baggage train (pack animals, livestock)
        public float MarchRiderPace = 6.5f;                // map speed cap for a clean column of riders - every man horsed, no train
        public float MarchPackAllowance = 0.25f;           // this many pack animals PER MAN count as field supply, not a train (0.25 = a mule per four men rides free)

        // --- The glutted market ---
        public bool MarketGlutEnabled = true;              // a merchant needs only so many of one thing: each extra piece of a type you sell him fetches less
        public float MarketGlutStartPercent = 5f;          // the FLOOR for the first piece: pays at least this % of value - a better trade rate (say 8%) stands as is
        public float MarketGlutDropPP = 0.25f;             // each further piece of that type knocks this many percentage points off YOUR rate
        public float MarketGlutMinPercent = 1f;            // the rate never falls below this % of the item's value
        public float MarketGlutRecoverPerDay = 2f;         // the market digests this many pieces of each type a day - come back later and prices breathe again

        // --- A night's rest ---
        public bool NightRestEnabled = true;               // the men must sleep: quiet night hours in camp or under a roof, or the column grows weary
        public float SleepHoursNeeded = 6f;                // base hours of sleep per day; sleep debt adds interest on top (1 night owed: +3h, 2: +9h, 3: +15h) and only the full sum clears it
        public float DayRestFactor = 0.6f;                 // sleep by daylight counts at this rate (camp noise, heat, light) - night hours count in full
        public bool QuickCampKey = true;                   // press O on the campaign map to pitch the BannerKings camp on the spot
        public bool SleepAtSeaFree = true;                 // crews sleep in watches - sailing through the night builds no sleep debt
        public bool AiCampsAtNight = true;                 // the world sleeps too: lord parties and caravans halt for the night (22-4) unless chased or in action
        public bool AiBanditsCampToo = false;               // brigands sleep as well - hideout by day, their own fire in the field by night
        public int AiTentCap = 60;                         // at most this many AI camps get a tent icon on the map - the rest still sleep, just without the picture
        public float AiTentRadius = 100f;                   // tent icons appear only this close to your party - the world beyond still sleeps, just without the picture
        public int AiCampSkipPercent = 15;                 // this share of lord columns press on through any given night - not everyone pitches camp
        public bool BanditsRestByDay = true;               // every band has a nature: three in four are night hunters (lie low 10-16), one in four hunts by day and beds down at night (23-5)
        public float AiNightsAwakeInChase = 1f;            // days a chasing or fleeing party may push on without sleep before it drops anyway
        public float AiCampDangerRadius = 6f;              // a hostile party this close keeps them marching - pursuit knows no bedtime
        public bool CampTentIcon = true;                   // pitched camps show a tent on the map (yours and theirs)
        public bool CoursePlotterEnabled = true;           // clicking a destination reports the route: kilometres, hours in the saddle and days on the road, and flags a settlement target on the map
        public bool CampBattlePropsEnabled = false;        // EXPERIMENTAL: attacked while encamped, the field battle gets your camp dressed on it - tents, fire, torches around your line (unknown prefabs are skipped and logged)
        public bool HideoutAlarmEnabled = true;            // a fight in a hideout wakes the camp: bandits within earshot come running - no more men ignoring a brawl ten paces away
        public float HideoutAlarmScreamRadius = 40f;       // a wounded man's cry and the ring of steel carry this many meters - bandits inside come at you, further ones sleep on
        public float HideoutAlarmWitnessRadius = 12f;      // a CLEAN one-blow kill alarms only enemies this close to the body - no witnesses, no alarm
        public bool HideoutArmouryGear = true;             // your men storm hideouts in their ARMOURY kit, same as field battles (opens the gate DTE leaves shut in regular hideouts)
        public bool HideoutNoiseEnabled = true;            // running feet carry in a hideout: bandits within earshot come alarmed - WALK (Left Ctrl) to move quietly
        public float HideoutHearDay = 10f;                 // meters a RUNNING man is heard by day - the camp's own bustle masks a lot
        public float HideoutHearNight = 20f;               // meters a RUNNING man is heard at night - silence carries sound, so sneak at a walk
        public bool HideoutAlarmRelay = true;              // a woken man shouts too: the alarm leaps man to man while each next stands within scream range of the last - never across the whole camp at once
        public float HideoutNoisePerArmorKg = 0.2f;        // every kilogram of worn armour adds this many meters to a RUNNING man's noise - plate thunders, leather whispers
        public bool HideoutAlarmVoice = true;              // the alarm is HEARD: one man in every ring of the relay lets out a real battle yell (the game's own voice line)
        public bool SightCycleEnabled = true;              // eyes follow the sun: every party sees further by day and shorter after dark
        public float DaySightFactor = 1.15f;               // spotting range in daylight, times this
        public float NightSightFactor = 0.65f;             // spotting range at night - the cut is mild, for you HEAR more after dark: a marching column is loud

        // --- The worth of a lesson ---
        public bool CombatXpFixEnabled = true;             // RBM pays the same XP for an arena tap and a battlefield kill - restore the proportions
        public float ArenaXpPercent = 20f;                 // practice bouts teach this share of the battle rate (vanilla 6, RBM 100)
        public float TournamentXpPercent = 50f;            // tournament bouts teach this share of the battle rate (vanilla 33, RBM 100)
        public bool BattleXpScalesWithDamage = true;       // real battles: XP follows the damage dealt and a kill pays double - not a flat fee per swing

        // --- The men's gear ---
        public bool TroopMendEnabled = true;               // the town smith mends the worn gear on the troop armoury's racks - his apprentices work at a bulk rate
        public float TroopMendWreckShare = 0.10f;          // a WRECK (1%) costs this share of the piece's worth - lighter wear costs proportionally less (60% condition = 4% of worth)
        public float TroopMendBulkDiscountPP = 0.5f;       // every piece on the job knocks this many percent off the whole bill - the more racks, the better the rate
        public float TroopMendBulkDiscountMax = 30f;       // the bulk discount never grows past this percent
        public float TroopMendMaxHours = 24f;              // the whole job never takes longer than this - the smith puts every hand he has on it
        public bool TroopOrderEnabled = true;              // order missing kit for the men from the town smith - plain pieces of the tier you ask, straight onto the racks
        public float TroopOrderMarkup = 1.15f;             // the smith's fee: each procured piece costs its market worth times this

        // --- Time at the forge ---
        public float SmithRepairHoursPerPiece = 1.5f;      // hours the smith needs per worn piece of your harness
        public float SelfRepairHoursPerPiece = 2.5f;       // hours you need per piece working the anvil yourself
        public float MendLootHoursPerPiece = 0.6f;         // hours per battle-worn piece from the bags
        public float MendMaterialMaxShare = 0.20f;         // mending is NOT forging anew: even a wreck (1%) takes at most this share of the full recipe's materials
        public bool TakeApartEnabled = true;                // rozlozenie gotowej rzeczy na czesci, zeby zdjac z niej wzor
        public float TakeApartBaseChance = 0.6f;           // szansa odczytania wzoru przy DOKLADNIE wymaganej Smithing
        public float TakeApartSkillSpan = 300f;            // ile punktow Smithing daje pelny przeskok szansy
        public float TakeApartSalvage = 0f;                // ile materialu wraca (0 = nic, 1 = tyle co z tygla)
        public float SmeltHoursPerTier = 0.5f;             // crucible hours per tier of the broken-down piece

        // --- Companion at the bellows ---
        public bool CompanionHelperEnabled = true;         // a companion may work the bellows for you
        public float HelperStaminaRelief = 0.4f;           // the most they can spare your arms
        public float HelperTimeRelief = 0.3f;              // the most they can shorten the work
        public int HelperSkillForFullRelief = 150;         // the Smithing at which a helper gives their full worth

        public bool LogEnabled = true;                     // write a log file in the module folder

        public static void Load(string moduleDataDir)
        {
            var file = Path.Combine(moduleDataDir, "Armoury.settings.xml");
            try
            {
                if (!File.Exists(file)) { Save(file); return; }
                var doc = new XmlDocument();
                doc.Load(file);
                var root = doc.DocumentElement;
                if (root == null) return;
                var s = new Settings();
                foreach (XmlNode n in root.ChildNodes)
                {
                    if (n.NodeType != XmlNodeType.Element) continue;
                    var f = typeof(Settings).GetField(n.Name);
                    if (f == null) continue;
                    var v = n.InnerText.Trim();
                    if (f.FieldType == typeof(bool)) f.SetValue(s, v.Equals("true", StringComparison.OrdinalIgnoreCase) || v == "1");
                    else if (f.FieldType == typeof(int)) f.SetValue(s, int.Parse(v, CultureInfo.InvariantCulture));
                    else if (f.FieldType == typeof(float)) f.SetValue(s, float.Parse(v, CultureInfo.InvariantCulture));
                }
                Current = s;
            }
            catch (Exception e) { Log.Error("Settings.Load", e); }
        }

        private static void Save(string file)
        {
            try
            {
                var doc = new XmlDocument();
                var root = doc.CreateElement("Armoury");
                doc.AppendChild(root);
                foreach (var f in typeof(Settings).GetFields())
                {
                    if (f.IsStatic) continue;
                    var e = doc.CreateElement(f.Name);
                    var val = f.GetValue(Current);
                    e.InnerText = val is bool b ? (b ? "true" : "false") : Convert.ToString(val, CultureInfo.InvariantCulture);
                    root.AppendChild(e);
                }
                Directory.CreateDirectory(Path.GetDirectoryName(file));
                doc.Save(file);
            }
            catch (Exception e) { Log.Error("Settings.Save", e); }
        }
    }
}
