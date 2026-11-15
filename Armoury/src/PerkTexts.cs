using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// 540 (Jeff 15.11: "czemu opisy perkow nie sa poprawione"): opisy polowek perkow, ktorych dzialanie zmienily wpisy 500-519 (i starsze: 289, 362,
    /// m109, 181, A17/KS), wedlug zweryfikowanej listy (docs/audyty-2026-11-14). Ten sam mechanizm co CampDrill/Drill/CastleSteel: PrimaryDescription /
    /// SecondaryDescription (podpowiedz) i opis calosci PropertyObject._description (okno wyboru perku) - opis calosci skladany od nowa z OBU biezacych
    /// polowek (STR1 + 2 nowe linie + STR2 jak PerkObject.Initialize), wiec polowki podmienione wczesniej przez CampDrill (Drill.SessionStart idzie
    /// przed nami w OnSessionLaunched) zostaja; Steel Maker 3 (CastleSteel nadpisywal caly opis jedna linia) dostaje z powrotem druga polowke.
    /// Polowki, ktore przepisuja CampDrill i Drill (druga polowka Walk It Off / A Good Days Rest / Saddlebags, Giving Hands, Paid in Promise) - NIE tu.
    /// Tekst null = klucz wylaczony, zostaje opis gry. Liczby z Settings.Current. Potkniecia liczone per perk (bez globalnego wylacznika).
    /// Klucz PerkTextsRewrite (domyslnie TAK) - NIE = nic nie podmieniamy.
    /// </summary>
    internal static class PerkTexts
    {
        private const string P = "Opisy perkow (540): ";

        private sealed class Rule
        {
            internal string Id; internal Func<PerkObject> Get; internal bool Secondary; internal Func<Settings, PerkObject, string> Text;
        }

        private static int _stumbles;

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_stumbles <= 10) Log.Error("PerkTexts." + where, e);
        }

        // ------------------------------------------------------------ liczby
        private static string N(float v) { return v.ToString("0.#", CultureInfo.InvariantCulture); }
        private static string Pc(float frac) { return N(frac * 100f); }   // 0.20 -> "20"

        /// <summary>{VALUE} jak w grze (StringHelpers.SetEffectIncrementTypeTextVariable): AddFactor x100, plus przed dodatnia.</summary>
        private static string V(PerkObject p, bool secondary)
        {
            float b = secondary ? p.SecondaryBonus : p.PrimaryBonus;
            var t = secondary ? p.SecondaryIncrementType : p.PrimaryIncrementType;
            float num = t == EffectIncrementType.AddFactor ? b * 100f : b;
            return (b > 0f ? "+" : "") + N(num);
        }

        /// <summary>Wartosc bez znaku ("5000", "30").</summary>
        private static string Raw(PerkObject p, bool secondary)
        {
            float b = secondary ? p.SecondaryBonus : p.PrimaryBonus;
            var t = secondary ? p.SecondaryIncrementType : p.PrimaryIncrementType;
            return N(Math.Abs(t == EffectIncrementType.AddFactor ? b * 100f : b));
        }

        private static PerkObject ById(string id)
        {
            try { return MBObjectManager.Instance != null ? MBObjectManager.Instance.GetObject<PerkObject>(id) : null; } catch { return null; }
        }

        private const string NoEffectSettings = "No effect with the current settings.";

        // ------------------------------------------------------------ lista (kolejnosc jak w CHANGELOG 540)
        private static Rule[] Rules()
        {
            return new[]
            {
                // 506: tarcze po RBM
                new Rule { Id = "OneHanded.ArrowCatcher", Get = () => DefaultPerks.OneHanded.ArrowCatcher, Secondary = false, Text = (s, p) =>
                    !_rbm ? null : !s.PerkShieldsAfterRbm ? "No effect in this game: Realistic Battle Mod gives shields no extra cover."
                    : "Your shield is " + Pc(s.ArrowCatcherShieldDurability) + "% tougher in combat and wears " + Pc(s.ArrowCatcherShieldWearCut) + "% slower." },
                new Rule { Id = "OneHanded.ArrowCatcher", Get = () => DefaultPerks.OneHanded.ArrowCatcher, Secondary = true, Text = (s, p) =>
                    !_rbm ? null : !s.PerkShieldsAfterRbm ? "No effect in this game: Realistic Battle Mod gives shields no extra cover."
                    : "Shields of the troops in your formation are " + Pc(s.ArrowCatcherCaptainDurability) + "% tougher in combat." },
                new Rule { Id = "Crossbow.Pavise", Get = () => DefaultPerks.Crossbow.Pavise, Secondary = false, Text = (s, p) =>
                    !_rbm || !s.PerkShieldsAfterRbm ? null
                    : "A shield on your back is " + Pc(s.PaviseBackShieldDurability) + "% tougher against missiles it stops and wears " + Pc(s.PaviseBackShieldWearCut) + "% slower."
                      + (s.PaviseBackShieldMelee ? " It is also " + Pc(s.PaviseBackShieldDurability) + "% tougher against melee blows that hit it." : "") },

                // 512: amunicja (dodatkowa amunicja martwa pod ROT - InitializeMissionEquipment nie wolany)
                new Rule { Id = "Bow.DeepQuivers", Get = () => DefaultPerks.Bow.DeepQuivers, Secondary = false, Text = (s, p) =>
                    "No effect: quivers get no extra arrows in this game." },
                new Rule { Id = "Bow.DeepQuivers", Get = () => DefaultPerks.Bow.DeepQuivers, Secondary = true, Text = (s, p) =>
                    s.AmmoPerksRework && s.AmmoRecoveryEnabled && s.AmmoPerkPartyPercent > 0f
                    ? "Your party gathers back " + N(s.AmmoPerkPartyPercent) + "% more of its shot arrows after a battle in which it holds the field."
                    : "No effect: troops get no extra arrows in this game." },
                new Rule { Id = "Crossbow.Fletcher", Get = () => DefaultPerks.Crossbow.Fletcher, Secondary = false, Text = (s, p) =>
                    s.AmmoPerksRework && s.WearEnabled && s.AmmoPerkOwnPercent > 0f
                    ? "Your crossbow wears " + N(s.AmmoPerkOwnPercent) + "% less with every shot."
                    : "No effect: quivers get no extra bolts in this game." },
                new Rule { Id = "Crossbow.Fletcher", Get = () => DefaultPerks.Crossbow.Fletcher, Secondary = true, Text = (s, p) =>
                    s.AmmoPerksRework && s.AmmoRecoveryEnabled && s.AmmoPerkPartyPercent > 0f
                    ? "Your party gathers back " + N(s.AmmoPerkPartyPercent) + "% more of its shot bolts after a battle in which it holds the field."
                    : "No effect: troops get no extra bolts in this game." },
                new Rule { Id = "Throwing.WellPrepared", Get = () => DefaultPerks.Throwing.WellPrepared, Secondary = false, Text = (s, p) =>
                    "No effect: you get no extra throwing weapons in this game." },
                new Rule { Id = "Throwing.WellPrepared", Get = () => DefaultPerks.Throwing.WellPrepared, Secondary = true, Text = (s, p) =>
                    s.AmmoPerksRework && s.AmmoRecoveryEnabled && s.AmmoPerkPartyPercent > 0f
                    ? "Your party gathers back " + N(s.AmmoPerkPartyPercent) + "% more of its thrown weapons after a battle in which it holds the field."
                    : "No effect: troops get no extra throwing weapons in this game." },
                new Rule { Id = "Throwing.Resourceful", Get = () => DefaultPerks.Throwing.Resourceful, Secondary = false, Text = (s, p) =>
                    "No effect: you get no extra throwing weapons in this game." },
                new Rule { Id = "Throwing.Saddlebags", Get = () => DefaultPerks.Throwing.Saddlebags, Secondary = false, Text = (s, p) =>
                    s.AmmoPerksRework && s.SaddlebagsCarryPercent > 0f
                    ? "+" + N(s.SaddlebagsCarryPercent) + "% carrying capacity to the party you lead, on land."
                    : "No effect: you get no extra throwing weapons in this game." },
                new Rule { Id = "Riding.Breeder", Get = () => DefaultPerks.Riding.Breeder, Secondary = false, Text = (s, p) =>
                    !s.BreederRealistic ? null
                    : V(p, false) + "% daily chance that a pair of animals of the same kind in your party breeds (only on land and while the party is fed; no mules, dragons, elephants, mammoths, chariots or unique mounts)." },

                // 513: leczenie
                new Rule { Id = "Athletics.WalkItOff", Get = () => DefaultPerks.Athletics.WalkItOff, Secondary = false, Text = (s, p) =>
                    !s.HealOnlyAtRest || s.WalkItOffLightWoundPercent <= s.MovingHealPercent ? null
                    : V(p, false) + "% hit point regeneration while traveling. On the march, lightly wounded heroes in your party (above 20 hit points) heal at "
                      + s.WalkItOffLightWoundPercent + "% of the resting rate instead of " + s.MovingHealPercent + "% (" + s.WalkItOffBothPercent + "% if your surgeon also has Walk It Off)." },
                new Rule { Id = "Athletics.AGoodDaysRest", Get = () => DefaultPerks.Athletics.AGoodDaysRest, Secondary = false, Text = (s, p) =>
                    !s.CampHealingPerks ? null : V(p, false) + "% hit point regeneration while waiting in settlements or resting in camp." },

                // 511 / MaterialLaw: kuznia
                new Rule { Id = "Crafting.IronMaker", Get = () => DefaultPerks.Crafting.IronMaker, Secondary = false, Text = (s, p) => IronMakerText(s) },
                new Rule { Id = "Crafting.CharcoalMaker", Get = () => DefaultPerks.Crafting.CharcoalMaker, Secondary = false, Text = (s, p) =>
                    !Refining(s) ? null : HistoricalPrices.On ? "+50% charcoal from every load of hardwood you burn."
                    : "You can burn one unit of hardwood into five units of charcoal instead of four." },
                new Rule { Id = "Crafting.SteelMaker", Get = () => DefaultPerks.Crafting.SteelMaker, Secondary = false, Text = (s, p) =>
                    !Refining(s) ? null : "You can refine five units of iron into four units of steel." },
                new Rule { Id = "Crafting.SteelMaker2", Get = () => DefaultPerks.Crafting.SteelMaker2, Secondary = false, Text = (s, p) =>
                    !Refining(s) ? null : "You can refine five units of steel into four units of fine steel." },
                new Rule { Id = "Crafting.SteelMaker3", Get = () => DefaultPerks.Crafting.SteelMaker3, Secondary = false, Text = (s, p) =>
                    !Refining(s) ? null : "You can refine five units of fine steel into four units of castle-forged steel." },
                new Rule { Id = "Crafting.PracticalSmith", Get = () => DefaultPerks.Crafting.PracticalSmith, Secondary = false, Text = (s, p) =>
                    !s.PerkSmithStamina ? null
                    : V(p, false) + "% stamina spent while smithing weapons, armour, bows, crossbows and ammunition, and " + N(s.PracticalSmithTimeCutPercent) + "% less time at the anvil." },
                new Rule { Id = "Crafting.ArtisanSmith", Get = () => DefaultPerks.Crafting.ArtisanSmith, Secondary = false, Text = (s, p) =>
                    !s.PerkArtisanSmithOwnWork ? null
                    : V(p, false) + "% trade penalty when selling smithing weapons and the armour, bows, crossbows and ammunition you forged yourself." },
                new Rule { Id = "Crafting.ExperiencedSmith", Get = () => DefaultPerks.Crafting.ExperiencedSmith, Secondary = false, Text = (s, p) =>
                    V(p, false) + "% greater chance of creating Fine weapons, armour, bows, crossbows and ammunition." },
                new Rule { Id = "Crafting.MasterSmith", Get = () => DefaultPerks.Crafting.MasterSmith, Secondary = false, Text = (s, p) =>
                    V(p, false) + "% greater chance of creating masterwork weapons, armour, bows, crossbows and ammunition." },
                new Rule { Id = "Crafting.LegendarySmith", Get = () => DefaultPerks.Crafting.LegendarySmith, Secondary = false, Text = (s, p) =>
                    V(p, false) + "% greater chance of creating Legendary weapons, armour, bows, crossbows and ammunition, chance increases by 1% for every 5 skill points above 275."
                    // 563d (Jeff 15.11): perk zostaje na 275 i odblokowuje przetop oraz kucie stali valyrianskiej (podpowiedz i okno wyboru perku); 563c: wedlug masy
                    + (!s.ValyrianIngots || !s.ValyrianNeedsLegendarySmith ? ""
                       : (s.ValyrianNoSmelt ? " Unlocks melting down Valyrian steel and forging it anew" : " Unlocks forging Valyrian steel anew") + ": a blade melts into as much steel as its blade weighs, and a new blade takes as much as it weighs (an ingot is "
                         + ValyrianBlades.KgText(ValyrianBlades.IngotG) + " kg; a Valyrian Steel Sword blade weighs " + ValyrianBlades.KgText(ValyrianBlades.MeasureG) + " kg).") },

                // 512-E
                new Rule { Id = "Scouting.BeastWhisperer", Get = () => DefaultPerks.Scouting.BeastWhisperer, Secondary = false, Text = (s, p) =>
                    !s.BeastWhispererPlainMounts ? null
                    : "+" + N(p.PrimaryBonus * 100f * Math.Max(0f, s.BeastWhispererChanceFactor)) + "% daily chance to find a common riding horse or pack animal when traveling through steppes and plains." },

                // 510 / 362 / m109: Roguery
                new Rule { Id = "Roguery.RansomBroker", Get = () => DefaultPerks.Roguery.RansomBroker, Secondary = false, Text = (s, p) =>
                    !s.PerkRansomByCaptor ? null
                    : V(p, false) + "% ransom for lords held captive in your party or in the dungeons of your settlements, paid by their family (half while your party is at sea)." },
                new Rule { Id = "Roguery.KnowHow", Get = () => DefaultPerks.Roguery.KnowHow, Secondary = false, Text = (s, p) =>
                    !s.RaidCacheByRoguery ? null
                    : "+5% more loot from defeated villagers and caravans; when you raid a village, you also force 5% more of its purse out of the villagers' hidden caches (at most "
                      + Pc(s.RaidCacheCap) + "% of the purse in all)." },
                new Rule { Id = "Roguery.Scarface", Get = () => DefaultPerks.Roguery.Scarface, Secondary = false, Text = (s, p) =>
                    !s.RaidCacheByRoguery && !s.CampInterrogation ? null
                    : "+30% chance for bandits, villagers and caravans to surrender."
                      + (s.RaidCacheByRoguery ? " Villagers you raid give up 25% more of their hidden coin." : "")
                      + (s.CampInterrogation ? " Outlaws you put to the question are 10% more likely to talk under torture." : "") },
                new Rule { Id = "Roguery.SaltTheEarth", Get = () => DefaultPerks.Roguery.SaltTheEarth, Secondary = false, Text = (s, p) =>
                    !s.RaidCacheByRoguery ? null
                    : "+20% more loot when villagers comply to your hostile actions, and 20% more hidden coin forced out of a village you raid." },

                // 509: budowa
                new Rule { Id = "Steward.ForcedLabor", Get = () => DefaultPerks.Steward.ForcedLabor, Secondary = true, Text = (s, p) =>
                    s.PrisonLabor
                    ? "Each working prisoner in the governed settlement's dungeon adds " + N(s.PrisonLaborPercentForcedLabor) + "% (instead of " + N(s.PrisonLaborPercentPerPrisoner)
                      + "%) to the day's construction while you are in the settlement."
                    : s.PaidConstruction && s.PaidConstructionPlayer ? NoEffectSettings
                    : "+1% construction speed in the governed settlement while you are there (only on works not paid by their owner)." },

                // 510 / 518: Trade
                new Rule { Id = "Trade.SpringOfGold", Get = () => DefaultPerks.Trade.SpringOfGold, Secondary = false, Text = (s, p) =>
                    !s.PerkSpringOfGoldFromMerchants ? null
                    : V(p, false) + "% of your denars as daily interest, up to 1000 denars, paid by the merchants of the towns where your clan owns workshops (only what they can spare; none from towns at war with you)." },
                new Rule { Id = "Trade.SpringOfGold", Get = () => DefaultPerks.Trade.SpringOfGold, Secondary = true, Text = (s, p) =>
                    "No effect in this game (Banner Kings bases the gold boost to construction on the town's craftsmen)." },
                new Rule { Id = "Trade.InsurancePlans", Get = () => DefaultPerks.Trade.InsurancePlans, Secondary = false, Text = (s, p) =>
                    !s.PerkTradeGiftsPaid ? null
                    : V(p, false) + " denars when an enemy destroys one of your caravans, paid by the merchants of its home town as far as they can spare (nothing for a disbanded caravan)." },
                new Rule { Id = "Trade.RapidDevelopment", Get = () => DefaultPerks.Trade.RapidDevelopment, Secondary = false, Text = (s, p) => RapidText(s, p) },
                new Rule { Id = "Trade.SelfMadeMan", Get = () => DefaultPerks.Trade.SelfMadeMan, Secondary = true, Text = (s, p) =>
                    s.PerkBuildSpeedOnPaidWork && s.EngineeringOnPaidWork
                    ? V(p, true) + "% build speed for marketplace and aqueduct projects in the governed settlement while you are there."
                    : s.PaidConstruction && s.PaidConstructionPlayer ? NoEffectSettings
                    : V(p, true) + "% build speed for marketplace and aqueduct projects in the governed settlement while you are there (only on works not paid by their owner)." },
                new Rule { Id = "Trade.Tollgates", Get = () => DefaultPerks.Trade.Tollgates, Secondary = true, Text = (s, p) =>
                    s.TownPaysRewards
                    ? "Caravans visiting the governed settlement pay a toll out of their own purse: " + Raw(p, true) + " denars (" + N(Math.Abs(p.SecondaryBonus) * 2f) + " for caravans travelling by land)."
                    : V(p, true) + " gold for each caravan visiting the governed settlement (" + N(Math.Abs(p.SecondaryBonus) * 2f) + " for caravans travelling by land)." },

                // 289 / BK: Steward
                new Rule { Id = "Steward.EfficientCampaigner", Get = () => DefaultPerks.Steward.EfficientCampaigner, Secondary = true, Text = (s, p) =>
                    !s.EfficientCampaignerAlways ? null : V(p, true) + "% troop wages in your party." },
                new Rule { Id = "Steward.Relocation", Get = () => DefaultPerks.Steward.Relocation, Secondary = true, Text = (s, p) =>
                    "No effect in this game (Banner Kings bases the gold boost to construction on the town's craftsmen)." },

                // Charm: miara perswazji (przesluchanie, plotki, targ)
                new Rule { Id = "Charm.MeaningfulFavors", Get = () => DefaultPerks.Charm.MeaningfulFavors, Secondary = false, Text = (s, p) =>
                    Uses(s) == null ? null : V(p, false) + "% chance for double persuasion success; +10 persuasion when you " + Uses(s) + "." },
                new Rule { Id = "Charm.MoralLeader", Get = () => DefaultPerks.Charm.MoralLeader, Secondary = false, Text = (s, p) =>
                    Uses(s) == null ? null : V(p, false) + " persuasion success required against characters of your own culture; +25 persuasion with your own people when you " + Uses(s) + "." },
                new Rule { Id = "Charm.NaturalLeader", Get = () => DefaultPerks.Charm.NaturalLeader, Secondary = false, Text = (s, p) =>
                    Uses(s) == null ? null : V(p, false) + " persuasion success required against characters of different cultures; +25 persuasion with strangers when you " + Uses(s) + "." },
                new Rule { Id = "Charm.ForgivableGrievances", Get = () => DefaultPerks.Charm.ForgivableGrievances, Secondary = false, Text = (s, p) =>
                    !s.CampInterrogation ? null
                    : V(p, false) + "% chance of avoiding critical failure on persuasion; prisoners you put to the question in camp are 20% less likely to die." },

                // 500-501 / 513 / 518 / CampFever / HeroWounds: Medicine
                new Rule { Id = "Medicine.PreventiveMedicine", Get = () => DefaultPerks.Medicine.PreventiveMedicine, Secondary = true, Text = (s, p) =>
                    !s.BattleDressingEnabled && !s.LoserSelfDressingCap && !s.SiegeSicknessEnabled ? null
                    : (s.BattleDressingEnabled
                        ? V(p, true) + "% of lost hit points restored after a won battle; as surgeon you give it to every hero in your party (never twice)."
                        : V(p, true) + "% recovery of lost hit points after each battle.")
                      + (s.LoserSelfDressingCap ? " After a lost battle your own healing perks restore at most " + s.LoserSelfDressingMaxHp + " hit points." : "")
                      + (s.SiegeSicknessEnabled ? " As surgeon: -15% camp fever in sieges." : "") },
                new Rule { Id = "Medicine.WalkItOff", Get = () => DefaultPerks.Medicine.WalkItOff, Secondary = false, Text = (s, p) =>
                    !s.HealOnlyAtRest || s.WalkItOffLightWoundPercent <= s.MovingHealPercent ? null
                    : V(p, false) + "% healing rate when moving on the campaign map; on the march lightly wounded heroes heal at " + s.WalkItOffLightWoundPercent
                      + "% of the full rate instead of " + s.MovingHealPercent + "% (" + s.WalkItOffBothPercent + "% together with Athletics Walk It Off on the party leader)." },
                new Rule { Id = "Medicine.Sledges", Get = () => DefaultPerks.Medicine.Sledges, Secondary = false, Text = (s, p) =>
                    !s.LitterEnabled ? null
                    : V(p, false) + "% party speed penalty from the wounded; while you are carried on a litter your party keeps " + s.LitterSpeedPercentSledges
                      + "% of its pace instead of " + s.LitterSpeedPercent + "%." },
                new Rule { Id = "Medicine.SiegeMedic", Get = () => DefaultPerks.Medicine.SiegeMedic, Secondary = true, Text = (s, p) =>
                    !s.SiegeSicknessEnabled ? null
                    : V(p, true) + "% chance to recover from lethal wounds during siege bombardment; half as many of your men sick with camp fever die during sieges." },
                new Rule { Id = "Medicine.Veterinarian", Get = () => DefaultPerks.Medicine.Veterinarian, Secondary = true, Text = (s, p) =>
                    !s.PerkVeterinarianHorses || !s.HorsesDieOnlyKilled ? null
                    : Raw(p, true) + "% chance that a horse of your party falling in battle (not at sea) is only wounded and stays with the party." },
                new Rule { Id = "Medicine.PristineStreets", Get = () => DefaultPerks.Medicine.PristineStreets, Secondary = false, Text = (s, p) =>
                    !s.SiegeSicknessEnabled ? null
                    : V(p, false) + " settlement prosperity every day in governed settlements; under siege its defenders catch 30% less camp fever and hunger worsens it by half instead of doubling it." },
                new Rule { Id = "Medicine.BushDoctor", Get = () => DefaultPerks.Medicine.BushDoctor, Secondary = true, Text = (s, p) =>
                    !s.BushDoctorInVillage && !s.CampHealingPerks ? null
                    : s.BushDoctorInVillage && s.CampHealingPerks ? V(p, true) + "% party healing rate while waiting in a village or camped beside one."
                    : s.BushDoctorInVillage ? V(p, true) + "% party healing rate while waiting in or beside a village."
                    : V(p, true) + "% party healing rate while waiting beside a village or camped near one." },
                new Rule { Id = "Medicine.CheatDeath", Get = () => DefaultPerks.Medicine.CheatDeath, Secondary = true, Text = (s, p) =>
                    !s.CheatDeathAtCrisis || !s.HeroWoundsEnabled ? null
                    : V(p, true) + "% chance to die when you fall unconscious in battle; as surgeon you also halve the chance of your party's heroes dying of a grave wound." },

                // 509 (59)
                new Rule { Id = "Engineering.ArchitecturalCommisions", Get = () => DefaultPerks.Engineering.ArchitecturalCommisions, Secondary = true, Text = (s, p) =>
                    !s.ArchitecturalCommissionsTownPays ? null
                    : V(p, true) + " gold per day for continuous projects in the governed settlement, paid out of its treasury (less when the treasury is short)." },

                // NavalDLC (po id w MBObjectManager; bez War Sails - brak perku, pominiety)
                new Rule { Id = "Boatswain.WellStocked", Get = () => ById("WellStocked"), Secondary = false, Text = (s, p) =>
                    s.NavalDeployAmmoFix ? null : !s.NavalAmmoPerksRecover || s.NavalAmmoPerkPercent <= 0f ? NoEffectSettings
                    : "+" + N(s.NavalAmmoPerkPercent) + "% to the share of spent arrows, bolts and throwing weapons your party gathers back after a naval battle you win." },
                new Rule { Id = "Boatswain.WellStocked", Get = () => ById("WellStocked"), Secondary = true, Text = (s, p) =>
                    s.NavalDeployAmmoFix ? null
                    : "No extra effect of its own: throwing weapons are included in the ammunition gathered back after a won naval battle." },
                new Rule { Id = "Boatswain.ShipwrightsInsight", Get = () => ById("ShipwrightsInsight"), Secondary = true, Text = (s, p) =>
                    s.NavalDeployAmmoFix ? null : !s.NavalAmmoPerksRecover || s.NavalAmmoPerkPercent <= 0f ? NoEffectSettings
                    : "+" + N(s.NavalAmmoPerkPercent) + "% to the share of spent arrows, bolts and throwing weapons your party gathers back after a naval battle you win (adds up with Well Stocked)." },
                new Rule { Id = "Boatswain.MerchantPrince", Get = () => ById("MerchantPrince"), Secondary = true, Text = (s, p) =>
                    !s.ShipTradeThroughPort ? null : "+500 denars for each ship bought or sold in the governed port, paid from the port's spare money." },
                new Rule { Id = "Boatswain.MasterShipwright", Get = () => ById("MasterShipwright"), Secondary = true, Text = (s, p) =>
                    !s.ShipTradeThroughPort ? null : "+30 denars from each ship repaired in the governed port, paid from the port's spare money." },
                new Rule { Id = "Boatswain.Salvage", Get = () => ById("Salvage"), Secondary = true, Text = (s, p) =>
                    !s.PerkNavalPortGoldPaid ? null : "+40 denars of port dues for each merchant convoy entering the governed port, paid by the convoy." },
                new Rule { Id = "Boatswain.ShipwrightsHand", Get = () => ById("ShipwrightsHand"), Secondary = true, Text = (s, p) =>
                    !s.PerkNavalPortGoldPaid ? null : !s.PerkShipwrightsHandVolunteer ? NoEffectSettings
                    : "+1 volunteer from the town's own notables to the garrison for each merchant convoy entering the governed port (your clan pays the recruitment fee)." },
                new Rule { Id = "Boatswain.Resilience", Get = () => ById("Resilience"), Secondary = false, Text = (s, p) =>
                    !s.BattleDressingEnabled ? "No effect in this game."
                    : "After a naval battle you win, the dressing of your wounds restores " + s.BattleDressingNavalResilience + "% more of the lost hit points." },
                new Rule { Id = "Shipmaster.NightRaider", Get = () => ById("NightRaider"), Secondary = true, Text = (s, p) =>
                    !s.StartCalibration || (!s.A17FishFromHands && !s.KsNoFishFromPerk) ? null
                    : "+5 fish per day to villages bound to the governed town only until the start calibration is applied; after that villages get fish only from their fishermen." },
            };
        }

        private static bool _rbm;

        private static bool Refining(Settings s) { return s.MaterialLawEnabled && s.RealRefiningEnabled; }

        /// <summary>Ta sama galaz co MaterialLaw.RefinePostfix: lupa z dymarki (ceny historyczne + Bloomery) / ruda historyczna / ruda zwykla.</summary>
        private static string IronMakerText(Settings s)
        {
            if (!Refining(s)) return null;
            if (HistoricalPrices.On)
            {
                if (Bloomery.IsBloom(Recipes.MaterialItem(CraftingMaterials.IronOre)))
                    return "Forging an iron bloom into crude iron bars loses " + N(Math.Max(0f, s.IronMakerBloomLossPercent)) + "% of its metal instead of " + N(Math.Max(0f, s.BloomForgeLossPercent)) + "%.";
                return "+25% crude iron from every unit of iron ore you smelt.";
            }
            return "You can smelt one unit of iron ore into four units of crude iron instead of three.";
        }

        /// <summary>D4 (518) + 510: zajecie warsztatow przy oblezeniu, ratunek kapitalu, 5000 od nowego pana.</summary>
        private static string RapidText(Settings s, PerkObject p)
        {
            string raw = Raw(p, false);
            if (!s.CapturedWorkshopsToTaker)
                return !s.PerkTradeGiftsPaid ? null
                    : V(p, false) + " denars for each workshop when its town is captured by an enemy, paid by the town's new lord as far as he can.";
            string other = s.PerkTradeGiftsPaid ? "its new lord pays you up to " + raw + " denars per workshop" : "you get " + raw + " denars per workshop";
            if (s.RapidDevelopmentSavesCapital)
                return "When an enemy takes by siege a town where you own a workshop, you carry off its working capital before it is seized; if the town passes to an enemy in any other way, " + other + ".";
            return "If a town where you own a workshop passes to an enemy in any other way than by siege, " + other + ".";
        }

        /// <summary>Czynnosci, w ktorych liczy sie miara perswazji (Persuasion.Measure); null = wszystkie klucze wylaczone.</summary>
        private static string Uses(Settings s)
        {
            var l = new List<string>();
            if (s.CampInterrogation) l.Add("question prisoners");
            if (s.GossipPerDrinkByPersuasion) l.Add("gather tavern gossip");
            if (s.PlayerHaggle || s.CaravanLeaderHaggle) l.Add("haggle over prices");
            if (l.Count == 0) return null;
            if (l.Count == 1) return l[0];
            return string.Join(", ", l.GetRange(0, l.Count - 1).ToArray()) + " or " + l[l.Count - 1];
        }

        // ------------------------------------------------------------ start sesji (OnSessionLaunched, po Drill.SessionStart i CastleSteel.Rename)
        internal static void SessionStart()
        {
            var s = Settings.Current;
            if (s == null) return;
            if (!s.PerkTextsRewrite) { Log.Info(P + "klucz Perk Texts Rewrite NIE - opisy gry bez zmian."); return; }
            _stumbles = 0;
            try { _rbm = Perk506.RbmShieldNow(); } catch (Exception e) { _rbm = false; Stumble("Rbm", e); }
            var pPrim = AccessTools.Property(typeof(PerkObject), "PrimaryDescription");
            var pSec = AccessTools.Property(typeof(PerkObject), "SecondaryDescription");
            var fDesc = AccessTools.Field(typeof(PropertyObject), "_description");
            Rule[] rules;
            try { rules = Rules(); } catch (Exception e) { Stumble("Rules", e); Log.Info(P + "lista perkow nie powstala - opisy gry bez zmian."); return; }
            int done = 0, off = 0, missing = 0;
            var ok = new StringBuilder(); var skipped = new StringBuilder(); var miss = new StringBuilder();
            foreach (var r in rules)
            {
                string tag = r.Id + (r.Secondary ? " (2)" : " (1)");
                try
                {
                    var perk = r.Get();
                    if (perk == null) { missing++; miss.Append(miss.Length > 0 ? ", " : "").Append(tag); continue; }
                    string text = r.Text(s, perk);
                    if (text == null) { off++; skipped.Append(skipped.Length > 0 ? ", " : "").Append(tag); continue; }
                    var prop = r.Secondary ? pSec : pPrim;
                    if (prop == null || fDesc == null) { missing++; miss.Append(miss.Length > 0 ? ", " : "").Append(tag + " brak pola"); continue; }
                    prop.SetValue(perk, new TextObject("{=!}" + text), null);
                    fDesc.SetValue(perk, Whole(perk));
                    done++;
                    ok.Append(ok.Length > 0 ? ", " : "").Append(tag);
                }
                catch (Exception e) { Stumble(tag, e); }
            }
            Log.Info(P + "podmienione " + done + " z " + rules.Length + ", pominiete (klucz wylaczony) " + off + ", potkniecia " + _stumbles
                     + (missing > 0 ? ", brak perku " + missing + " [" + miss + "]" : "") + " | RBM tarcze " + (_rbm ? "TAK" : "NIE")
                     + " | podmienione: [" + ok + "]" + (off > 0 ? " | opis gry (klucz wylaczony): [" + skipped + "]" : "") + ".");
        }

        /// <summary>Opis calosci jak PerkObject.Initialize: z druga polowka "STR1 + 2 nowe linie + STR2", bez niej kopia pierwszej.</summary>
        private static TextObject Whole(PerkObject perk)
        {
            var prim = perk.PrimaryDescription;
            var sec = perk.SecondaryDescription;
            TextObject d;
            if (sec != null && !string.IsNullOrEmpty(sec.ToString()))
            {
                d = new TextObject("{=!}{STR1}{newline}{newline}{STR2}");
                d.SetTextVariable("STR1", prim);
                d.SetTextVariable("STR2", sec);
            }
            else d = prim != null ? prim.CopyTextObject() : new TextObject("");
            d.SetTextVariable("newline", "\n");
            return d;
        }
    }
}
