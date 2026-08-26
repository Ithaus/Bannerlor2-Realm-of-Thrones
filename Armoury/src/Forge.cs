using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>Wlasciwe kucie: twoje zelazo, twoja wytrzymalosc, twoja umiejetnosc.</summary>
    internal static class Forge
    {
        internal static int Stamina() { return Stamina(null); }

        internal static int Stamina(Hero who)
        {
            try
            {
                var h = who ?? Hero.MainHero;
                var b = Campaign.Current.GetCampaignBehavior<CraftingCampaignBehavior>();
                return b != null ? b.GetHeroCraftingStamina(h) : 0;
            }
            catch (Exception e) { Log.Error("Stamina", e); return 0; }
        }

        internal static void SpendStamina(int amount) { SpendStamina(amount, null); }

        internal static void SpendStamina(int amount, Hero who)
        {
            try
            {
                var h = who ?? Hero.MainHero;
                var b = Campaign.Current.GetCampaignBehavior<CraftingCampaignBehavior>();
                if (b == null) return;
                int cur = b.GetHeroCraftingStamina(h);
                b.SetHeroCraftingStamina(h, MathF.Max(0, cur - amount));
            }
            catch (Exception e) { Log.Error("SpendStamina", e); }
        }

        /// <summary>Szansa na spartaczenie roboty. Im wieksza przewaga umiejetnosci, tym mniejsza.</summary>
        internal static float FailureChance(Recipes.Recipe r) { return FailureChance(r, 1, null); }

        internal static float FailureChance(Recipes.Recipe r, int tempo) { return FailureChance(r, tempo, null); }

        /// <summary>
        /// Ryzyko liczy sie z reki TEGO, KTO KUJE. Wczesniej zawsze brano
        /// umiejetnosc gracza - towarzysz przy kowadle w zakladce CRAFT placil
        /// wlasna stamina i XP, ale udawalo mu sie tak, jakby to kul Jeff.
        /// </summary>
        internal static float FailureChance(Recipes.Recipe r, int tempo, Hero who)
        {
            var s = Settings.Current;
            int skill = (who ?? Hero.MainHero).GetSkillValue(DefaultSkills.Crafting);
            float margin = skill - r.SkillNeeded;
            float risk = Project.RiskFactor(tempo);
            if (margin >= s.MarginForNoFailure) return 0f;
            float chance = margin <= 0f
                ? s.FailureChanceAtZeroMargin * risk
                : s.FailureChanceAtZeroMargin * risk * (1f - margin / s.MarginForNoFailure);
            // luczarnia wybacza wiecej niz hartowanie plachy - Jeff: "luki zawsze 50%, bez sensu"
            if (r.Ranged) chance *= MathF.Max(0f, s.RangedFailureFactor);
            return MathF.Min(0.9f, chance);
        }

        /// <summary>Jakosc wyrobu - modyfikator z wlasnej grupy przedmiotu.</summary>
        internal static ItemModifier RollQuality(ItemObject item, Recipes.Recipe r) { return RollQuality(item, r, 1, null); }

        internal static ItemModifier RollQuality(ItemObject item, Recipes.Recipe r, int tempo) { return RollQuality(item, r, tempo, null); }

        /// <summary>Jakosc wyrobu wedle reki kowala, ktory przy nim stal.</summary>
        internal static ItemModifier RollQuality(ItemObject item, Recipes.Recipe r, int tempo, Hero who)
        {
            try
            {
                var s = Settings.Current;
                var smith = who ?? Hero.MainHero;
                var group = item.ItemComponent != null ? item.ItemComponent.ItemModifierGroup : null;
                if (group == null) return null;

                int skill = smith.GetSkillValue(DefaultSkills.Crafting);
                float margin = skill - r.SkillNeeded;

                // ROBOTA PONIZEJ NORMY (Jeff: "mozesz zepsuc cos i wtedy cechy sa
                // nizsze"). Wyrob, ktory nie pekl na kowadle, wcale nie musi byc
                // udany - przy cienkiej wprawie schodzi z niego kruche, zle
                // hartowane zelastwo o gorszych cechach. Tak samo jak przy broni
                // w wanilii i przy pancerzach u Banner Kings.
                var bad = new List<ItemModifier>();
                foreach (var m in group.ItemModifiers)
                    if (m != null && m.PriceMultiplier < 1f && m.PriceMultiplier > 0f) bad.Add(m);
                if (bad.Count > 0)
                {
                    float slop = MathF.Max(1f, s.ShoddyMarginRange);
                    float shoddy = MathF.Max(0f, s.ShoddyChanceAtZeroMargin) * (1f - MathF.Max(0f, margin) / slop);
                    if (margin < 0f) shoddy = MathF.Min(0.9f, shoddy * (1f + (-margin) / slop));
                    if (shoddy > 0f && MBRandom.RandomFloat < shoddy)
                    {
                        bad.Sort((a, b) => b.PriceMultiplier.CompareTo(a.PriceMultiplier));  // najlagodniejszy pierwszy
                        int worst = margin < -slop ? bad.Count - 1 : 0;                      // dno tylko przy calkowitej niewprawie
                        return bad[MathF.Min(bad.Count - 1, worst)];
                    }
                }

                var good = new List<ItemModifier>();
                foreach (var m in group.ItemModifiers)
                    if (m != null && m.PriceMultiplier > 1f) good.Add(m);
                if (good.Count == 0) return null;
                good.Sort((a, b) => a.PriceMultiplier.CompareTo(b.PriceMultiplier));   // od najslabszego

                // 100 punktow ponad wymog = realna szansa na mistrzowski wyrob
                float chance = MathF.Min(0.85f, (margin / 200f) * Project.QualityFactor(tempo));
                float jackpot = s.JackpotChance;
                if (smith.GetPerkValue(DefaultPerks.Crafting.LegendarySmith)) jackpot += s.LegendaryPerkJackpotBonus;
                if (MBRandom.RandomFloat < jackpot) return good[good.Count - 1];   // rzecz mistrzowska
                if (MBRandom.RandomFloat > chance) return null;                 // zwykly wyrob

                int idx = 0;
                if (margin > 150f && MBRandom.RandomFloat < 0.35f) idx = good.Count - 1;
                else if (margin > 80f && MBRandom.RandomFloat < 0.5f) idx = MathF.Min(good.Count - 1, 1);
                return good[idx];
            }
            catch (Exception e) { Log.Error("RollQuality", e); return null; }
        }

        /// <summary>Przetapianie pancerza na metal - gra tego nie potrafi, liczymy po swojemu.</summary>
        internal static void Smelt(ItemObject item)
        {
            try
            {
                var s = Settings.Current;
                var r = Recipes.For(item);
                int skill = Hero.MainHero.GetSkillValue(DefaultSkills.Crafting);
                if (Stamina() < r.Stamina / 2)
                { Log.Player("You are too spent to break that down.", true); return; }

                SpendStamina(r.Stamina / 2);
                float share = MathF.Min(0.9f, s.SmeltingReturnShare + skill * s.SmeltingSkillBonus);
                var yield_ = Recipes.SmeltYield(r, share);

                var roster = MobileParty.MainParty.ItemRoster;
                roster.AddToCounts(item, -1);
                var sb = new System.Text.StringBuilder();
                foreach (var p in yield_)
                {
                    roster.AddToCounts(p.Item, p.Count);
                    if (sb.Length > 0) sb.Append(", ");
                    sb.Append(p.Count + "x " + p.Item.Name);
                }
                Hero.MainHero.HeroDeveloper.AddSkillXp(DefaultSkills.Crafting, ProjectXp(r, false) * 0.5f);

                Log.Player("You broke down " + item.Name + " for " + (sb.Length > 0 ? sb.ToString() : "scrap") + ".");
                Log.Info("Przetop: " + item.StringId + " -> " + sb + " (" + (int)(share * 100) + "%)");
            }
            catch (Exception e) { Log.Error("Smelt", e); }
        }

        /// <summary>Rozpoczecie roboty. Material i wytrzymalosc ida od razu, wyrob dopiero po czasie.</summary>
        internal static bool Begin(ItemObject item, int tempo, out float days)
        {
            days = 0f;
            try
            {
                var s = Settings.Current;
                var r = Recipes.For(item);
                int skill = Hero.MainHero.GetSkillValue(DefaultSkills.Crafting);

                string legendWhy;
                if (!LegendAllowed(item, out legendWhy))
                { Log.Player(legendWhy, true); return false; }
                if (!RangedLore.KnownOf(item))
                { Log.Player("You have not worked out this pattern yet - the craft itself will teach you.", true); return false; }
                if (skill < r.SkillNeeded)
                { Log.Player("That is beyond your hand. You need " + r.SkillNeeded + " Smithing.", true); return false; }
                if (!Recipes.HasMaterials(r))
                { Log.Player("You lack the materials.", true); return false; }
                float reliefPre = Helper.Relief(Helper.Find());
                int staminaNeed = MathF.Max(1, (int)(r.Stamina * (1f - reliefPre * s.HelperStaminaRelief)));
                if (Stamina() < staminaNeed)
                { Log.Player("You are too spent to start such work. Rest first.", true); return false; }

                int fee = ForgeFee(r);
                if (Hero.MainHero.Gold < fee)
                { Log.Player("The smith wants " + fee + " gold for the use of his forge.", true); return false; }
                if (fee > 0) GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, fee);

                var helper = Helper.Find();
                float relief = Helper.Relief(helper);

                Recipes.TakeMaterials(r);
                SpendStamina(MathF.Max(1, (int)(r.Stamina * (1f - relief * s.HelperStaminaRelief))));
                days = s.ForgeTakesTime ? MathF.Max(0.5f, r.Tier * s.DaysPerTier * Project.TimeFactor(tempo)) : 0f;
                days *= (1f - relief * s.HelperTimeRelief);
                if (helper != null)
                {
                    Helper.GiveXp(helper, item.Value * 0.08f);
                    Log.Player(helper.Name + " works the bellows for you - the labour goes faster.");
                    Log.Info("Pomocnik: " + helper.Name + " ulga=" + relief.ToString("0.00"));
                }
                Log.Info("Rozpoczeto: " + item.StringId + " tempo=" + tempo + " dni=" + days + " oplata=" + fee);
                return true;
            }
            catch (Exception e) { Log.Error("Begin", e); return false; }
        }

        /// <summary>Ile dni robota zajmuje w podstawowym tempie - baza do naliczania XP.</summary>
        internal static float BaseDays(Recipes.Recipe r)
        {
            var s = Settings.Current;
            if (!s.ForgeTakesTime) return 1f;
            return MathF.Max(1f, r.Tier * s.DaysPerTier);
        }

        /// <summary>
        /// XP za caly projekt. Placimy za czas przy kowadle i za trudnosc, nie za cene wyrobu -
        /// inaczej kucie drogich rzeczy bylo by drukarka poziomow. Gra sama tepi nauke ponad
        /// limitem uczenia (atrybut + skupienie), wiec tu tylko lekko scinamy robote ponizej twojej reki.
        /// </summary>
        internal static int ProjectXp(Recipes.Recipe r) { return ProjectXp(r, true); }

        internal static int ProjectXp(Recipes.Recipe r, bool timed)
        {
            try
            {
                var s = Settings.Current;
                int skill = Hero.MainHero.GetSkillValue(DefaultSkills.Crafting);
                float margin = MathF.Max(0f, (float)(skill - r.SkillNeeded - s.XpFullCreditMargin));
                float learning = MathF.Max(s.XpFloorFactor, 1f - margin / MathF.Max(1f, s.XpDiminishingRange));
                float days = timed ? BaseDays(r) : 1f;
                int xp = (int)(days * r.Tier * s.XpPerDayPerTier * learning);
                int cap = MathF.Max(50, s.XpCapPerTier * r.Tier);
                if (!timed) cap = MathF.Max(50, (int)(cap / MathF.Max(1f, BaseDays(r))));
                return Math.Min(cap, Math.Max(20, xp));
            }
            catch (Exception e) { Log.Error("ProjectXp", e); return 50; }
        }

        internal static int ForgeFee(Recipes.Recipe r)
        {
            var s = Settings.Current;
            if (s.ForgeDayPassEnabled && DayPass.ActiveHere()) return 0;   // dniowka oplacona - kuznia i tak Twoja
            return s.ForgeFeeBase + s.ForgeFeePerTier * r.Tier;
        }

        /// <summary>Zakonczenie roboty - tu zapada wynik.</summary>
        internal static void Finish(ItemObject item, int tempo)
        {
            try
            {
                var r = Recipes.For(item);
                int skill = Hero.MainHero.GetSkillValue(DefaultSkills.Crafting);

                if (MBRandom.RandomFloat < FailureChance(r, tempo))
                {
                    Hero.MainHero.HeroDeveloper.AddSkillXp(DefaultSkills.Crafting, ProjectXp(r) * 0.3f);
                    Log.Player("After all that work, the piece cracked at the quench. Nothing to show for it.", true);
                    Log.Info("Kucie nieudane po czasie: " + item.StringId);
                    return;
                }

                var quality = RollQuality(item, r, tempo);
                // amunicje robi sie SERIAMI: jedna robota = kilka wiazek strzal/beltow
                int made = (item.ItemType == ItemObject.ItemTypeEnum.Arrows || item.ItemType == ItemObject.ItemTypeEnum.Bolts)
                    ? MathF.Max(1, Settings.Current.AmmoBatchStacks) : 1;
                MobileParty.MainParty.ItemRoster.AddToCounts(new EquipmentElement(item, quality), made);
                if (Recipes.IsLegendary(item) && !ArmouryBehavior.Legends.Contains(item.StringId))
                    ArmouryBehavior.Legends.Add(item.StringId);
                RangedLore.OnCrafted(item);
                float xpMulF = (item.ItemType == ItemObject.ItemTypeEnum.Arrows
                             || item.ItemType == ItemObject.ItemTypeEnum.Bolts) ? 0.05f : 1f;
                Hero.MainHero.HeroDeveloper.AddSkillXp(DefaultSkills.Crafting,
                    ProjectXp(r) * (1f - Settings.Current.XpShareWhileWorking) * xpMulF);

                string qname = quality != null ? quality.Name + " " : "";
                Log.Player("Your work is done: " + (made > 1 ? made + " x " : "") + qname + item.Name + ".");
                Log.Info("Ukonczono: " + item.StringId + " jakosc=" + (quality != null ? quality.StringId : "zwykla"));
            }
            catch (Exception e) { Log.Error("Finish", e); }
        }

        /// <summary>Naprawa wlasnoreczna - material i wytrzymalosc zamiast zlota.
        /// Gdy sie nie da, w reason laduje DOKLADNY powod (skill/sila/materialy).</summary>
        internal static bool SelfRepair(ItemObject item, float missingCondition) { string _; return SelfRepair(item, missingCondition, out _); }

        internal static bool SelfRepair(ItemObject item, float missingCondition, out string reason)
        {
            reason = null;
            try
            {
                var s = Settings.Current;
                var r = Recipes.For(item);
                int skill = Hero.MainHero.GetSkillValue(DefaultSkills.Crafting);
                if (skill < r.SkillNeeded)
                { reason = "Smithing " + r.SkillNeeded + " (you have " + skill + ")"; return false; }

                int stam = MathF.Max(3, (int)(r.Stamina * s.SelfRepairStaminaFactor * missingCondition));
                if (Stamina() < stam)
                { reason = stam + " stamina (you have " + (int)Stamina() + ")"; return false; }

                var missing = Recipes.MissingParts(r, s.SelfRepairMaterialFactor * missingCondition);
                if (missing != null && missing.Count > 0)
                { reason = "materials: " + string.Join(", ", missing); return false; }

                Recipes.TakePartial(r, s.SelfRepairMaterialFactor * missingCondition);
                SpendStamina(stam);
                Hero.MainHero.HeroDeveloper.AddSkillXp(DefaultSkills.Crafting, ProjectXp(r, false) * 0.6f);
                return true;
            }
            catch (Exception e) { Log.Error("SelfRepair", e); reason = "forge mishap (see log)"; return false; }
        }

        /// <summary>Legenda moze istniec TYLKO JEDNA: raz wykuta albo juz posiadana - nigdy wiecej.</summary>
        internal static bool LegendAllowed(ItemObject item, out string why)
        {
            why = null;
            try
            {
                if (!Recipes.IsLegendary(item)) return true;
                if (ArmouryBehavior.Legends.Contains(item.StringId))
                { why = "You have forged this legend once already - there can be only one."; return false; }
                var roster = MobileParty.MainParty.ItemRoster;
                for (int i = 0; i < roster.Count; i++)
                    if (roster[i].EquipmentElement.Item == item && roster[i].Amount > 0)
                    { why = "The legend already rides in your baggage - there can be only one."; return false; }
                foreach (var h in Clan.PlayerClan.Heroes)
                {
                    var eq = h.BattleEquipment;
                    if (eq == null) continue;
                    for (int sl = 0; sl < 12; sl++)
                        if (eq[(EquipmentIndex)sl].Item == item)
                        { why = h.Name + " already bears this legend - there can be only one."; return false; }
                }
                return true;
            }
            catch { return true; }
        }

        internal static void Smith(ItemObject item) { Smith(item, null); }

        internal static void Smith(ItemObject item, Hero who)
        {
            try
            {
                var s = Settings.Current;
                var hero = who ?? Hero.MainHero;
                var r = Recipes.For(item);
                int skill = hero.GetSkillValue(DefaultSkills.Crafting);
                // wiazka strzal to nauka na 5 minut, nie dzielo: XP / 20
                float xpMul = (item.ItemType == ItemObject.ItemTypeEnum.Arrows
                            || item.ItemType == ItemObject.ItemTypeEnum.Bolts) ? 0.05f : 1f;

                string legendWhy;
                if (!LegendAllowed(item, out legendWhy))
                { Log.Player(legendWhy, true); return; }
                if (!RangedLore.KnownOf(item))
                { Log.Player("You have not worked out this pattern yet - the craft itself will teach you.", true); return; }
                if (skill < r.SkillNeeded)
                { Log.Player("That is beyond your hand. You need " + r.SkillNeeded + " Smithing.", true); return; }
                if (!Recipes.HasMaterials(r))
                { Log.Player("You lack the metal for it.", true); return; }
                if (Stamina(hero) < r.Stamina)
                { Log.Player("You are too spent to work the forge. Rest first.", true); return; }

                SpendStamina(r.Stamina, hero);

                if (MBRandom.RandomFloat < FailureChance(r, 1, hero))
                {
                    Recipes.TakePartial(r, s.MaterialLossOnFailure);
                    hero.HeroDeveloper.AddSkillXp(DefaultSkills.Crafting, ProjectXp(r, false) * 0.3f * xpMul);
                    Log.Player("The piece cracked on the anvil. Half your metal is slag.", true);
                    Log.Info("Kucie nieudane: " + item.StringId + " (skill " + skill + "/" + r.SkillNeeded + ")");
                    return;
                }

                Recipes.TakeMaterials(r);
                var quality = RollQuality(item, r, 1, hero);
                // amunicje robi sie SERIAMI: jedna robota = kilka wiazek strzal/beltow
                int made = (item.ItemType == ItemObject.ItemTypeEnum.Arrows || item.ItemType == ItemObject.ItemTypeEnum.Bolts)
                    ? MathF.Max(1, Settings.Current.AmmoBatchStacks) : 1;
                MobileParty.MainParty.ItemRoster.AddToCounts(new EquipmentElement(item, quality), made);
                if (Recipes.IsLegendary(item) && !ArmouryBehavior.Legends.Contains(item.StringId))
                    ArmouryBehavior.Legends.Add(item.StringId);
                RangedLore.OnCrafted(item);
                hero.HeroDeveloper.AddSkillXp(DefaultSkills.Crafting,
                    ProjectXp(r) * (1f - Settings.Current.XpShareWhileWorking) * xpMul);

                string qname = quality != null ? quality.Name + " " : "";
                Log.Player("You forged " + (made > 1 ? made + " x " : "") + qname + item.Name + ".");
                Log.Info("Wykuto: " + item.StringId + " jakosc=" + (quality != null ? quality.StringId : "zwykla")
                         + " skill=" + skill + "/" + r.SkillNeeded);
            }
            catch (Exception e) { Log.Error("Smith", e); }
        }
    }
}
