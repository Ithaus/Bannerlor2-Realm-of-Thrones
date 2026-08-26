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
        public float LegendaryPerkJackpotBonus = 0.08f;    // added chance of a masterpiece from the smithing perks
        public float ShoddyChanceAtZeroMargin = 0.35f;     // a piece that survives the anvil can still come off SHODDY - this is the chance of that when your Smithing barely meets the recipe (lower stats, lower worth, like a botched blade)
        public float ShoddyMarginRange = 60f;              // that chance fades to nothing this far above the recipe's requirement, and grows when you work beneath it
        public float CharcoalPerIron = 0.6f;               // charcoal burned per unit of iron
        public int StaminaPerTier = 25;                    // crafting stamina burned per tier
        // --- Time at the anvil ---
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
        public float LegendaryValueFloor = 25000f;         // an unbuyable piece worth at least this much is a LEGEND - legendary bills and the one-of-a-kind rule apply
        public float LegendaryMaterialFactor = 4f;         // a legend's bill: every material count multiplied by this, plus the noblest steel on top
        public int LegendarySkillNeeded = 250;             // no legend leaves the forge below this Smithing
        public float SmeltingReturnShare = 0.45f;          // share of the metal that comes back when you break a piece down
        public float SmeltingSkillBonus = 0.002f;          // extra recovery per point of Smithing
        public float JackpotChance = 0.04f;                // chance of a masterpiece beyond what your skill would give

        // --- Wear and tear ---
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
        // --- Loot from the field ---
        public bool LootArrivesWorn = true;                // gear stripped from the fallen comes to you already used
        public float LootWearBase = 45f;                   // the condition of an average piece of loot
        public float LootWearSpread = 25f;                 // how widely that varies
        public bool CaptiveSpoilsEnabled = true;           // a man you take captive is stripped at the rope: his whole kit lands in your baggage, battle-worn
        public bool CaptiveSpoilsIncludeMounts = true;     // his horse and harness are seized too
        // --- The law of the battlefield ---
        public bool BattlefieldLawEnabled = true;          // battles you fight yourself: the dead drop their real gear (DTE), Spoils of War steps aside
        public bool LootArrivesBattleWorn = true;          // loot-screen items from fought battles arrive battle-worn but keep their worth
        public bool SimBattleFullDrop = true;              // auto-resolved battles ignore the hidden per-tier drop multipliers
        public int PlayerLootSharePercent = 40;            // your cut of what the party strips from the dead; the rest stays in the army armoury (a lone wanderer takes all)
        public bool WreckSalvageEnabled = true;            // the piece smashed by the killing blow is not lost - it lands in the loot as a wreck to mend at the forge
        public int MinSellPercentOfValue = 5;              // merchants never pay less than this share of an item's clean value - scrap is still metal and leather (0 = off)
        public bool EnlistedSoldierNoLooting = true;       // serving in a lord's army: the quartermasters strip the field - one soldier does not pocket the army's loot and gold

        // --- Flesh and wind ---
        public bool FieldCraftEnabled = true;              // battlefield body rules: sprint fatigue, wounded penalties, bleeding, arrows that barely scratched fall off
        public bool SprintFatigueEnabled = true;           // running on foot drinks stamina points - an empty pool means a slow man
        public bool UseRbmStamina = true;                  // sprint drinks from RBM's own stamina pool (Athletics already grows it); off = our own pool with the same rules
        public float SprintDrainPerSecond = 4f;            // stamina points one second of flat-out sprint costs, before the armour is weighed
        public float SprintDrainPerKg = 1f;                // extra points per second for every kilogram of armour carried
        public float FatigueFreeArmorKg = 0f;              // armour up to this weight costs no extra stamina
        public float StaminaEndurancePerPoint = 4f;        // heroes: each point of Endurance grows the stamina pool AND its regeneration by this percent
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
        public float SleepHoursNeeded = 5f;                // this many hours of sleep settle the day's account - whenever taken
        public float DayRestFactor = 0.6f;                 // sleep by daylight counts at this rate (camp noise, heat, light) - night hours count in full
        public bool QuickCampKey = true;                   // press O on the campaign map to pitch the BannerKings camp on the spot
        public bool SleepAtSeaFree = true;                 // crews sleep in watches - sailing through the night builds no sleep debt
        public bool AiCampsAtNight = true;                 // the world sleeps too: lord parties and caravans halt for the night (22-4) unless chased or in action
        public bool AiBanditsCampToo = false;               // brigands sleep as well - hideout by day, their own fire in the field by night
        public int AiTentCap = 40;                         // at most this many AI camps get a tent icon on the map - the rest still sleep, just without the picture
        public float AiNightsAwakeInChase = 1f;            // days a chasing or fleeing party may push on without sleep before it drops anyway
        public float AiCampDangerRadius = 6f;              // a hostile party this close keeps them marching - pursuit knows no bedtime
        public bool CampTentIcon = true;                   // pitched camps show a tent on the map (yours and theirs)

        // --- The worth of a lesson ---
        public bool CombatXpFixEnabled = true;             // RBM pays the same XP for an arena tap and a battlefield kill - restore the proportions
        public float ArenaXpPercent = 20f;                 // practice bouts teach this share of the battle rate (vanilla 6, RBM 100)
        public float TournamentXpPercent = 50f;            // tournament bouts teach this share of the battle rate (vanilla 33, RBM 100)
        public bool BattleXpScalesWithDamage = true;       // real battles: XP follows the damage dealt and a kill pays double - not a flat fee per swing

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
