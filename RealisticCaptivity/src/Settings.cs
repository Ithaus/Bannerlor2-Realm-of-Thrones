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
        // E5 decyzja 9 Jeffa (12.10): gracz pojmany przez kogokolwiek traci cale zloto i caly rynsztunek - do pojmujacego, nie w nicosc
        public bool CaptorTakesAllFromPlayer = true;       // whoever takes you - outlaws, a lord, a town's militia or garrison - takes every coin you carry and all your war gear: the gold goes to the lord's purse, to the band's purse, or to the coffers of the settlement whose men took you (overrides Loot Gold Percent and Strip Equipment); the baggage they seize goes to them too, never to nobody (off = Loot Gold Percent, and with no lord to take it the gold simply vanishes, as before)
        // Jeff 12.10 (raport nocny, pytanie 3): przy CaptorTakesAllFromPlayer pojmujacy zabiera takze CALE juki (towar, zywnosc, juczne) - nie LootPartyInventoryPercent
        // C4 (Jeff 14.10 "tak samo jak pokonany lord AI"): gracz po pojmaniu traci to samo, co pokonany lord AI - zloto tylko przez lup bitwy (5% kiesy,
        // najwyzej 10 000 - gra; Armoury A6 sciaga to z kiesy), rynsztunek zostaje na nim (zdobywca bierze tylko unikaty - Armoury UniqueSpoils, jak u AI),
        // tabor caly (jak rozbita druzyna lorda), zbrojownia DTE do zdobywcy (Armoury CaptiveArmory). Wylaczone - CaptorTakesAllFromPlayer i reszta jak dotad.
        public bool PlayerLosesLikeAiLord = true;          // when you are taken captive you lose what a beaten AI lord loses, no more and no less: your captors keep you in your own arms and clothes (a lord's renowned arms still go by the custom of war), your purse pays only the battle's toll as any lord's does (a twentieth, at most 10000), and your whole baggage train is theirs as a broken lord's company is; your companions keep their gear too; with Captor Takes Arms And Purse on, your war gear and your purse go to your captor as well (overrides Captor Takes All From Player, Strip Equipment, Strip Companions and Loot Gold Percent; off = as before)
        // n20-niewola (Jeff 19.10: "wypuscili mnie ze zlotem i pancerzem ... i bronia i koniem; zabrali wszystko inne"): przy C4 zdobywca zabiera tez
        // rynsztunek bojowy (12 slotow zestawu bojowego: bron, choragiew, zbroja, kon, uprzaz) do SWOICH jukow (osada - do zapasu) i cale zloto przy sobie
        // (Hero.Gold) do swojej kiesy / kasy osady; stroj cywilny i zestaw ukrycia zostaja; unikaty - tylko zwyczaj wojny Armoury (UniqueSpoils).
        // Zwrot tylko przy odbiciu sila (to, co jeszcze lezy w jukach tych, ktorzy trzymali gracza; z GearBackFromVictors takze to, co zwyciezcy wzieli
        // z tych jukow w bitwie, ktora uwolnila gracza). CaptivityBehavior.TakeWarGear / WarGearOnRelease.
        public bool CaptorTakesArmsAndPurse = true;        // with Player Loses Like Ai Lord on: whoever takes you captive also takes your war gear - weapons, banner, armour, horse and harness - into his own packs (the stores of the settlement, if its militia, garrison or dungeon took you) and every coin in your purse into his own purse (or the settlement's coffers). Your town clothes stay yours, but on the road and in battle you have only your smallclothes and no horse until you get new gear. What you do not carry stays yours: the strongbox and chest of your houses, your workshops and fiefs. Renowned unique arms still go only by the custom of war. Your gear is then his plunder like any other: he may sell it or wear it. If his company is beaten and you are set free by force, you take your gear back (see Gear Back From Victors); ransom, escape or peace leave it with him (off = you keep your arms and your purse, as before)
        // n20-niewola poprawka po recenzji: gra lupi juki pokonanej partii sztuka po sztuce z szansa ok. 0.5 (DefaultBattleRewardModel.GetLootItemChancesForWinnerParties
        // x0.5; milicja i zaloga nie lupia) PRZED uwolnieniem jencow - bez tej reguly przy odbiciu w bitwie wracala losowo ok. polowa rynsztunku, a odsiecz
        // rodu dawala dwa rozne wyniki (wirtualna 100%, prawdziwa bitwa ok. 50% - reszta w jukach wlasnej druzyny rodu). Jeff 13.09: "skoro pokonali bandytow
        // i mnie uwolnili, moj sprzet powinien byc odzyskany". Wlaczone: sztuki z ksiegi, ktore zwyciezcy wzieli z jukow zdobywcy w TEJ bitwie
        // (CampaignEvents.OnLootDistributedToPartyEvent), wracaja do gracza z ich jukow. Wylaczone: tylko to, co zostalo w jukach zdobywcy.
        public bool GearBackFromVictors = true;            // when your captors' company is beaten in battle and you are set free, you also take back the pieces of your war gear that the victors have just taken from your captors' packs, so all of your gear that your captors still had comes back to you, whoever freed you (off = only what the victors left in your captors' packs comes back, about half)
        public bool CaptorTakesAllBaggage = true;          // with Captor Takes All From Player on, your captor also takes your whole baggage train - goods, food and pack animals - not just Loot Party Inventory Percent; a lord or a band puts it in their own packs, a settlement whose men took you (militia, garrison or dungeon) in its own stores, like your gold (off = Loot Party Inventory Percent, as before)
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
        public bool PrisonerGeoSale = true;                // slavery is outlawed in Westeros: selling captives there pays a fence's cut, while Essosi slave markets pay in full
        public int WesterosFencePercent = 20;              // what a Westerosi fence pays for a captive, as percent of the full Essosi price
        public float PrisonerSaleFloorFactor = 1f;         // times the vanilla broker rate (a quarter of the man's recruitment cost)

        // --- Lord ransom ---
        public bool LordRansomByRank = true;               // a lord YOU hold is worth his station: kings, heads of houses and landed lords fetch ransoms fit for their rank (couriers, brokers, barter)
        public int LordRansomKing = 100000;                // base ransom for a king or queen held by you
        public int LordRansomClanLeader = 25000;           // base ransom for the head of a noble house held by you
        public int LordRansomLord = 8000;                  // base ransom for any other lord or lady of a noble house held by you
        public float LordRansomPerClanTier = 0.25f;        // each tier of the prisoner's house adds this share of the base
        public int LordRansomPerTown = 10000;              // a king or head of house is worth this much more for every town his house holds
        public int LordRansomPerCastle = 5000;             // a king or head of house is worth this much more for every castle his house holds

        // --- Bandit plunder ---
        public bool FenceGearWhenNoLord = true;            // bandits sell your gear at the nearest market
        // 3.9 drobne w nicosc (Drobne39.cs): lapowka i rynsztunek u bandytow maja platnika i odbiorce
        public bool EscapeBribeToCaptor = true;            // the bribe for your escape goes into the purse of the party holding you - the guard is its man; on parole you pay only when you really run (off = the coin vanishes, as before, and is paid before you choose to keep your word)
        public bool GearToCaptorPacks = true;              // the war gear stripped from you is your captors' plunder like any other: it goes into their packs, and with Armoury the bands' law decides its fate as for any band's loot (the fence buys only spare horses, body armour waits for promotions, the rest stays in the packs) - run them down to take it back; with your captors gone you take your gear back (off = the gear lands on the market for free, as before)


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
        public int HomePriceTown = 4000;                   // base price of a town house (not used while Historical Town Rates is on)
        public float HomePriceProsperityFactor = 0.5f;     // plus this per point of prosperity (not used while Historical Town Rates is on)
        public int HomePriceVillage = 1500;                // base price of a village house (not used while Historical Town Rates is on)
        public float HomePriceHearthFactor = 1.5f;         // plus this per hearth of the village (not used while Historical Town Rates is on)
        public float HomeSellFactor = 0.6f;                // a buyer gives this share of the price
        public int HomeTownPence = 2400;                   // with historical town rates: a modest town house in a middling town, pence (about 10 pounds c.1300), as dear as the town is prosperous
        public int HomeVillagePence = 480;                 // with historical town rates: a village cottage, pence (about 2 pounds), by the wage level of its market town
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
        // E8/RC (259, regula decyzji 9): splata dlugu okupu i wykup rynsztunku przy pojmujacym bez zywego bohatera - do jego rodu, osady albo bandy, nie w nicosc
        public bool DebtToCaptorHome = true;               // when no living lord stands behind your captor, the daily instalment on your ransom debt and the price of buying back your war gear go to whoever holds the claim: the head of a dead captor's house, the coffers of the settlement whose militia, garrison or dungeon held you, or the purse of the band that took you; with no one left to take it nothing is taken and the debt waits (off = that coin simply vanishes, as before)

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
        public float WorkPayVillageBase = 1f;              // village day wage before the hearth bonus (not used while Historical Town Rates is on)
        public float WorkPayVillageHearthDiv = 1000f;       // village hearths divided by this are added to the wage (not used while Historical Town Rates is on)
        public float WorkPayTownBase = 1.5f;               // town day wage before the prosperity bonus (not used while Historical Town Rates is on)
        public float WorkPayTownProsperityDiv = 5000f;     // town prosperity divided by this is added to the wage (not used while Historical Town Rates is on)
        public int WorkAthleticsXpPerDay = 25;             // athletics practice from a day of hard graft
        public int WorkSaturationDays = 10;                // after this many days in one place the wage halves
        public int WorkSaturationRestDays = 5;             // days away before the place forgets you and pays full again
        public bool GuardWorkEnabled = true;               // night-watch work for hire in towns
        public int GuardOnlyBelowGold = 10000;             // guard work offered only while your purse is under this (0 = always)
        public int GuardSkillRequired = 40;                // best weapon skill the merchants demand of a hired guard
        public float GuardPayBase = 2f;                    // night-watch wage before the prosperity bonus (not used while Historical Town Rates is on)
        public float GuardPayProsperityDiv = 4000f;        // town prosperity divided by this is added to the wage (not used while Historical Town Rates is on)
        public int GuardWeaponXpPerDay = 40;               // weapon practice from a night on the walls
        public int GuardBrawlChancePercent = 15;           // nightly chance of trouble at the gates
        public int GuardBrawlBonus = 50;                   // extra pay for cracking heads when trouble comes (not used while Historical Town Rates is on)
        public int GuardBrawlHealthLoss = 30;              // health lost when the toughs get the better of you
        public bool HistoricalTownRates = true;            // wages and house prices at historical rates, as dear as the town is prosperous (the town wage level of Armoury), and paid by the settlement's coffers - not out of thin air (off = the old formulas, gold from nobody and to nobody)
        public float LabourerDayWage = 1.5f;               // an unskilled labourer's day in a middling town, pence (England c.1300: a labourer about 1.5 d, a craftsman 3 d); a night on the watch pays the same
        public float VillageWageShare = 0.75f;             // a day's hire in a village pays this share of the wage in its market town
        public float GuardBrawlBonusDays = 3f;             // cracking heads when trouble comes pays this many days' wages on top
        // E8/RC (259, LAWA 7/16, 8, 10): dniowki i warta z nadwyzki miasta (regula R1 etapu 8), bez zaokraglania w gore do 1, pusta nadwyzka zatrzymuje prace
        public bool WorkRespectsReserve = true;            // with Historical Town Rates on, day labour and the night watch are paid only out of what a town can spare above its merchants' reserve (the town surplus of Armoury; a village pays from its whole purse): with nothing to spare no one hires, work under way stops and an unpaid day does not count towards the half-pay limit; a wage under one stag is no longer rounded up to a full stag every day (off = paid down to the last coin of the coffers and never less than 1 stag a day, as before)

        // --- In the lord's service ---
        public bool EnlistedHonestWounds = true;           // no miracle full-heal after enlisted battles - wounds mend with rest, as they should
        public int EnlistedPostBattleHealHp = 10;          // a field dressing after the fight - this many hit points, no more
        public int EnlistedDailyCareHp = 15;               // the army medicus can restore this much each day

        // --- Realm of Thrones without gold from nowhere (3.7) ---
        // 3.7 ROT: wycena lorda w lupie zaciagnietego gracza - min(nasz okup, cena gry) (RotEnlistLoot37.cs)
        public bool RotEnlistLootGamePrice = true;         // while you serve enlisted under a lord, a captured lord in your share of the loot is valued at the game's ransom price or at the ransom his house would pay you, whichever is lower - nobody pays that ransom here, the value only turns into goods for you (off = valued at the ransom his house would pay you, as before)

        // --- Payment messages ---
        // m96-wies (Jeff 20.10: "You paid w miescie 2 - powinno byc, za co"): PayNotes.cs - przy przelewach gracza z tego moda komunikat gry "You paid N" /
        // "You received N" wylaczony; zostaje jeden komunikat z powodem (dotychczasowy albo nowy: rata dlugu okupu, kupno i sprzedaz domu). Kwoty bez zmian.
        // Wylaczony = komunikaty gry jak dotad (od nastepnego wczytania - MCM RC). Nowy klucz (MCM Jeffa go nie ma - domyslnie wlaczony). Ten sam klucz w Armoury i GT.
        public bool PlayerPaymentReasons = true;           // when you pay or receive gold in captivity, for your ransom or your house, the game's bare "You paid N" / "You received N" is replaced by one message that says what for. Only the message changes - sums and payers stay the same. Off = the game's message as before

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
