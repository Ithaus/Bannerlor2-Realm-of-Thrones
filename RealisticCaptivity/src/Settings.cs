using System;
using System.Globalization;
using System.IO;
using System.Xml;

namespace RealisticCaptivity
{
    public class Settings
    {
        public static Settings Current = new Settings();

        // --- Gear and plunder ---
        public bool StripEquipment = true;                 // your captor takes your armour and weapons
        public bool KeepCivilianClothes = true;            // you are left your plain clothes
        public bool LeaveInRags = true;                    // even the clothes go - you are left in rags
        public int LootPartyInventoryPercent = 75;         // percent of the baggage train they seize
        public int LootGoldPercent = 60;                   // percent of the gold on your person they take
        public bool BuybackEnabled = true;                 // your gear can be bought back from whoever holds it
        public float BuybackPriceMultiplier = 1.6f;        // times the market value they ask for it

        // --- Escape ---
        public int MinDaysBeforeEscape = 12;               // days behind the door before escape is possible at all
        public float EscapeChanceMultiplier = 0.2f;        // escape odds against vanilla (1.0 = unchanged)
        public int FailedEscapeHealthLossPercent = 35;     // health lost when they catch you at it
        public int FailedEscapeRelationPenalty = -8;       // relation lost with your captor for trying

        // --- Word of honour ---
        public bool ParoleEnabled = true;                  // a captor may accept your word not to flee
        public bool ParoleRequiresStatus = true;           // parole only for someone of standing - land, title or renown
        public int ParoleMinRenown = 150;                  // renown at which you count as somebody
        public float LowbornRansomDelay = 2.0f;            // a nobody waits this many times longer for a ransom offer
        public int ParoleRenownLoss = 80;                  // renown lost for breaking your word
        public int ParoleRelationPenalty = -40;            // relation lost for breaking your word
        public float ParoleRansomDiscount = 0.75f;         // ransom is cheaper once you have given your word

        // --- Ransom ---
        public float RansomMultiplier = 5.0f;              // times the vanilla ransom sum
        public float RansomRenownFactor = 30f;             // extra gold demanded per point of your renown
        public int MinDaysBeforeRansomOffer = 4;           // days before anyone bothers to name a price

        // --- Selling prisoners ---
        public bool PrisonerSaleFloor = true;              // BannerKings pays the local slave price for common prisoners - in a slave-glutted town that is zero; guarantee at least the old broker rate
        public float PrisonerSaleFloorFactor = 1f;         // times the vanilla broker rate (a quarter of the man's recruitment cost)

        // --- Bandit plunder ---
        public bool FenceGearWhenNoLord = true;            // bandits sell your gear at the nearest market
        public float FencePriceMultiplier = 1.0f;          // what the fence charges (1.0 = market price)


        // --- Hunger in the cells ---
        public bool StarvationEnabled = true;              // poor food and cold cells wear a prisoner down
        public int StarvationHealthPerDay = 4;             // health lost per day in the cells
        public float StarvationLowbornFactor = 1.75f;      // a nobody is fed worse - times the damage
        public float StarvationParoleFactor = 0.4f;        // on parole the conditions are lighter
        public int AtrophyChancePercentPerDay = 25;        // daily chance the wasting costs you a point of a physical skill
        public int AtrophyPointsPerHit = 1;                // points lost at once when it happens
        public int AtrophyMinSkillValue = 20;              // the wasting will not take a skill below this

        // --- A roof of your own ---
        public bool HomesEnabled = true;                   // houses to own in towns and villages
        public bool FamilyHomeFree = true;                 // the hearth you were born under is yours from the start
        public int HomePriceTown = 4000;                   // base price of a town house
        public float HomePriceProsperityFactor = 0.5f;     // plus this per point of prosperity
        public int HomePriceVillage = 1500;                // base price of a village house
        public float HomePriceHearthFactor = 1.5f;         // plus this per hearth of the village
        public float HomeSellFactor = 0.6f;                // a buyer gives this share of the price
        // --- Cast out by brigands ---
        public bool BanditDumpEnabled = true;              // brigands do not feed a worthless mouth for weeks
        public int BanditDumpAfterDays = 5;                // they give a ransom this many days to appear
        public int BanditDumpWorthlessGold = 800;          // under this much coin you are not worth holding
        public int BanditDumpChancePercentPerDay = 20;     // then, each day, they may simply cut you loose
        public float BanditDumpDyingFactor = 2f;           // a prisoner near death is dumped twice as eagerly
        // --- Being sold on ---
        public bool SellPrisonerEnabled = true;            // a captor may sell you on to someone else
        public int SellChancePercentPerDay = 8;            // daily chance of being sold on
        public int SellMinDays = 5;                        // days before anyone thinks of selling you

        // --- Companions ---
        public bool StripCompanions = true;                // captured companions are stripped as well

        // --- Ransom debt ---
        public bool RansomDebtEnabled = true;              // you may pledge the ransom as a debt and walk out today
        public int DebtOfferAfterDays = 20;                // days before the offer of credit is made
        public float DebtInterest = 1.4f;                  // times more than a ransom paid in coin
        public int DebtDailyInstalment = 250;              // gold taken from you each day until it is cleared
        public int DebtGraceDays = 60;                     // days of missed payment before the disgrace lands
        public int DebtDefaultRenownLoss = 50;             // renown lost for defaulting on the debt

        // --- Escape needs help ---
        public bool EscapeNeedsHelp = true;                // no one walks out of a cell alone - it takes help
        public int EscapeBribeGold = 800;                  // what your clan pays the gaoler


        // --- Rescue ---
        public bool RescueEnabled = true;                  // your people ride out to free you or to talk terms
        public float RescueArrivalDistance = 3.0f;         // how close they must get before they act
        public float RescueStrengthRatio = 1.1f;           // how many times stronger they must be to storm a bandit camp
        public float NegotiationMaxDiscount = 0.6f;        // the best discount your envoy can talk a lord down to
        public int RescueRetryDays = 4;                    // days before they try again after being driven off

        // --- Humiliation ---
        public bool HumiliationEnabled = true;             // captivity itself costs you standing
        public int HumiliationRenownLoss = 15;             // renown lost for having been taken

        // --- A clean break ---
        public bool CleanBreakEnabled = true;              // a fully mounted party that flees a battle cannot be run down by pursuers on foot
        public int CleanBreakHours = 4;                    // for this many hours that band cannot force another fight on you
        public bool CleanBreakNeedsStanding = true;        // the clean break belongs to a man who ACTUALLY RODE OFF - he sounded the retreat or rode away from the encounter. Cut down on the field, he is taken like anyone else
        public bool MountedEncounterFlight = true;         // whole party in the saddle may try to ride away from an encounter - rearguard only against the enemy horse

        // --- Honest work ---
        public bool WorkEnabled = true;                    // day labour and guard work for hire in settlements
        public int WorkMaxPartySize = 5;                   // with more mouths than this behind you, no one hires you as a hand
        public int WorkOnlyBelowGold = 2500;               // day labour offered only while your purse is under this (0 = always)
        public float WorkPayVillageBase = 15f;             // village day wage before the hearth bonus
        public float WorkPayVillageHearthDiv = 40f;        // village hearths divided by this are added to the wage
        public float WorkPayTownBase = 20f;                // town day wage before the prosperity bonus
        public float WorkPayTownProsperityDiv = 300f;      // town prosperity divided by this is added to the wage
        public int WorkAthleticsXpPerDay = 25;             // athletics practice from a day of hard graft
        public int WorkSaturationDays = 10;                // after this many days in one place the wage halves
        public int WorkSaturationRestDays = 5;             // days away before the place forgets you and pays full again
        public bool GuardWorkEnabled = true;               // night-watch work for hire in towns
        public int GuardOnlyBelowGold = 10000;             // guard work offered only while your purse is under this (0 = always)
        public int GuardSkillRequired = 40;                // best weapon skill the merchants demand of a hired guard
        public float GuardPayBase = 45f;                   // night-watch wage before the prosperity bonus
        public float GuardPayProsperityDiv = 150f;         // town prosperity divided by this is added to the wage
        public int GuardWeaponXpPerDay = 40;               // weapon practice from a night on the walls
        public int GuardBrawlChancePercent = 15;           // nightly chance of trouble at the gates
        public int GuardBrawlBonus = 50;                   // extra pay for cracking heads when trouble comes
        public int GuardBrawlHealthLoss = 30;              // health lost when the toughs get the better of you

        // --- In the lord's service ---
        public bool EnlistedHonestWounds = true;           // no miracle full-heal after enlisted battles - wounds mend with rest, as they should
        public int EnlistedPostBattleHealHp = 10;          // a field dressing after the fight - this many hit points, no more
        public int EnlistedDailyCareHp = 15;               // the army medicus can restore this much each day

        // --- Other ---
        public bool LogEnabled = true;                     // write a log file in the module folder

        public static void Load(string moduleDataDir)
        {
            var file = Path.Combine(moduleDataDir, "RealisticCaptivity.settings.xml");
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
                var root = doc.CreateElement("RealisticCaptivity");
                doc.AppendChild(root);
                foreach (var f in typeof(Settings).GetFields())
                {
                    if (f.IsStatic) continue;
                    var e = doc.CreateElement(f.Name);
                    var val = f.GetValue(Current);
                    e.InnerText = val is bool b
                        ? (b ? "true" : "false")
                        : Convert.ToString(val, CultureInfo.InvariantCulture);
                    root.AppendChild(e);
                }
                Directory.CreateDirectory(Path.GetDirectoryName(file));
                doc.Save(file);
            }
            catch (Exception e) { Log.Error("Settings.Save", e); }
        }
    }
}
