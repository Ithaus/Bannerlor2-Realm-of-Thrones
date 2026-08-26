using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace RealisticCaptivity
{
    /// <summary>
    /// Uczciwa praca dla golca. W miescie i wsi mozna najac sie na dniowke (nedzna placa,
    /// strawa od gospodarza, troche Atletyki), a w miescie stanac na nocnej warcie za
    /// lepszy grosz - o ile umiesz trzymac bron. Placa skaluje sie z zamoznoscia osady,
    /// po zbyt wielu dniach w jednym miejscu spada o polowe. Bogatych i dowodcow
    /// nikt do roboty nie najmie.
    /// </summary>
    internal static class Work
    {
        // dane trzyma CaptivityBehavior (SyncData); tu tylko odwolania
        internal static Dictionary<string, int> Days;      // settlementId -> dni przepracowane (nasycenie)
        internal static Dictionary<string, int> LastDay;   // settlementId -> nr dnia kampanii ostatniej dniowki

        private static string _cameFrom = "town";
        private static float _hours;                       // narosle godziny w biezacej robocie
        private static bool _fedToday;

        private static Settlement Here { get { return Settlement.CurrentSettlement; } }
        private static int Today { get { return (int)CampaignTime.Now.ToDays; } }

        // ------------------------------------------------------------------ ROT enlistment guard
        // Zaciagniety zolnierz nie najmie sie do roboty w miescie - armia by odmaszerowala
        // bez niego i dwa systemy klocilyby sie o partie gracza. Refleksja, zeby nie
        // wiazac RC twardo z ROT (dziala tez bez ROT).

        private static bool _rotLookupDone;
        private static System.Reflection.FieldInfo _rotBehField;
        private static System.Reflection.FieldInfo _rotIsEnlistedField;

        private static bool RotEnlisted()
        {
            try
            {
                if (!_rotLookupDone)
                {
                    _rotLookupDone = true;
                    var sub = Type.GetType("ROT.SubModule, ROT");
                    if (sub != null)
                    {
                        _rotBehField = sub.GetField("EnlistmentBehavior",
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                        if (_rotBehField != null && _rotBehField.FieldType != null)
                            _rotIsEnlistedField = _rotBehField.FieldType.GetField("IsEnlisted",
                                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    }
                }
                if (_rotBehField == null || _rotIsEnlistedField == null) return false;
                var beh = _rotBehField.GetValue(null);
                if (beh == null) return false;
                var v = _rotIsEnlistedField.GetValue(beh);
                return v is bool && (bool)v;
            }
            catch { return false; }
        }

        // ------------------------------------------------------------------ pay

        private static int LabourPay(Settlement s)
        {
            var c = Settings.Current;
            float pay;
            if (s.IsVillage)
                pay = c.WorkPayVillageBase + s.Village.Hearth / Math.Max(1f, c.WorkPayVillageHearthDiv);
            else
                pay = c.WorkPayTownBase + (s.Town != null ? s.Town.Prosperity : 3000f) / Math.Max(1f, c.WorkPayTownProsperityDiv);
            if (Saturated(s)) pay *= 0.5f;
            return Math.Max(1, (int)pay);
        }

        private static int GuardPay(Settlement s)
        {
            var c = Settings.Current;
            float pay = c.GuardPayBase + (s.Town != null ? s.Town.Prosperity : 3000f) / Math.Max(1f, c.GuardPayProsperityDiv);
            return Math.Max(1, (int)pay);
        }

        private static bool Saturated(Settlement s)
        {
            Forget(s);
            int d;
            return Days != null && Days.TryGetValue(s.StringId, out d) && d >= Settings.Current.WorkSaturationDays;
        }

        /// <summary>Po kilku dniach przerwy osada "zapomina" i znow placi pelna stawke.</summary>
        private static void Forget(Settlement s)
        {
            try
            {
                if (Days == null || LastDay == null || s == null) return;
                int last;
                if (LastDay.TryGetValue(s.StringId, out last) &&
                    Today - last >= Settings.Current.WorkSaturationRestDays)
                {
                    Days.Remove(s.StringId);
                    LastDay.Remove(s.StringId);
                }
            }
            catch { }
        }

        private static int BestWeaponSkill()
        {
            var h = Hero.MainHero;
            int best = h.GetSkillValue(DefaultSkills.OneHanded);
            if (h.GetSkillValue(DefaultSkills.TwoHanded) > best) best = h.GetSkillValue(DefaultSkills.TwoHanded);
            if (h.GetSkillValue(DefaultSkills.Polearm) > best) best = h.GetSkillValue(DefaultSkills.Polearm);
            if (h.GetSkillValue(DefaultSkills.Bow) > best) best = h.GetSkillValue(DefaultSkills.Bow);
            if (h.GetSkillValue(DefaultSkills.Crossbow) > best) best = h.GetSkillValue(DefaultSkills.Crossbow);
            if (h.GetSkillValue(DefaultSkills.Throwing) > best) best = h.GetSkillValue(DefaultSkills.Throwing);
            return best;
        }

        private static SkillObject BestMeleeSkillObject()
        {
            var h = Hero.MainHero;
            SkillObject skill = DefaultSkills.OneHanded;
            int best = h.GetSkillValue(DefaultSkills.OneHanded);
            if (h.GetSkillValue(DefaultSkills.TwoHanded) > best) { best = h.GetSkillValue(DefaultSkills.TwoHanded); skill = DefaultSkills.TwoHanded; }
            if (h.GetSkillValue(DefaultSkills.Polearm) > best) { skill = DefaultSkills.Polearm; }
            return skill;
        }

        // ------------------------------------------------------------------ menus

        internal static void Add(CampaignGameStarter starter)
        {
            foreach (var root in new[] { "town", "village" })
            {
                var r = root;
                starter.AddGameMenuOption(root, "rc_work_labour_" + root,
                    "{=!}Hire yourself out for day labour",
                    LabourCondition,
                    delegate (MenuCallbackArgs a) { _cameFrom = r; StartWork("rc_work_labour"); }, false, 5);
            }

            starter.AddGameMenuOption("town", "rc_work_guard",
                "{=!}Stand the night watch for pay",
                GuardCondition,
                delegate (MenuCallbackArgs a) { _cameFrom = "town"; StartWork("rc_work_guard"); }, false, 6);

            starter.AddWaitGameMenu("rc_work_labour",
                "{=!}From first light to dusk you dig, carry and haul with the rest of the hands. " +
                "The pay is {RC_WORK_PAY} stags a day and a seat at the master's table. Earned so far: {RC_WORK_EARNED}.",
                WorkInit, WaitCondition, null, LabourTick,
                GameMenu.MenuAndOptionType.WaitMenuHideProgressAndHoursOption,
                GameMenu.MenuOverlayType.SettlementWithBoth);

            starter.AddWaitGameMenu("rc_work_guard",
                "{=!}Night after night you walk the walls and gates with a torch and a cudgel. " +
                "The merchants pay {RC_WORK_PAY} stags a watch. Earned so far: {RC_WORK_EARNED}.",
                WorkInit, WaitCondition, null, GuardTick,
                GameMenu.MenuAndOptionType.WaitMenuHideProgressAndHoursOption,
                GameMenu.MenuOverlayType.SettlementWithBoth);

            foreach (var id in new[] { "rc_work_labour", "rc_work_guard" })
            {
                starter.AddGameMenuOption(id, id + "_stop",
                    "{=!}Enough of this toil (stop working)",
                    delegate (MenuCallbackArgs a) { a.optionLeaveType = GameMenuOption.LeaveType.Leave; return true; },
                    delegate (MenuCallbackArgs a) { GameMenu.SwitchToMenu(_cameFrom); }, true, 9);
            }
        }

        private static bool LabourCondition(MenuCallbackArgs args)
        {
            try
            {
                var c = Settings.Current;
                if (!c.WorkEnabled || Here == null || RotEnlisted()) return false;
                args.optionLeaveType = GameMenuOption.LeaveType.Wait;
                if (Here.IsUnderRaid || Here.IsUnderSiege)
                { args.IsEnabled = false; args.Tooltip = new TextObject("{=!}No one is hiring while the place is under attack."); return true; }
                if (PartyBase.MainParty.NumberOfHealthyMembers > c.WorkMaxPartySize)
                { args.IsEnabled = false; args.Tooltip = new TextObject("{=!}You command too many men - no one hires a captain as a farmhand. (at most {MAX} in your party)").SetTextVariable("MAX", c.WorkMaxPartySize); return true; }
                if (c.WorkOnlyBelowGold > 0 && Hero.MainHero.Gold >= c.WorkOnlyBelowGold)
                { args.IsEnabled = false; args.Tooltip = new TextObject("{=!}You are far too wealthy to be seen digging ditches. (under {MAX} gold)").SetTextVariable("MAX", c.WorkOnlyBelowGold); return true; }
                args.Tooltip = new TextObject("{=!}About {PAY} stags a day, food included. Hard graft builds Athletics.")
                    .SetTextVariable("PAY", LabourPay(Here));
                return true;
            }
            catch (Exception e) { Log.Error("LabourCondition", e); return false; }
        }

        private static bool GuardCondition(MenuCallbackArgs args)
        {
            try
            {
                var c = Settings.Current;
                if (!c.WorkEnabled || !c.GuardWorkEnabled || Here == null || !Here.IsTown || RotEnlisted()) return false;
                args.optionLeaveType = GameMenuOption.LeaveType.DefendAction;
                if (Here.IsUnderSiege)
                { args.IsEnabled = false; args.Tooltip = new TextObject("{=!}The garrison mans the walls now - hired watchmen are sent home."); return true; }
                if (PartyBase.MainParty.NumberOfHealthyMembers > c.WorkMaxPartySize)
                { args.IsEnabled = false; args.Tooltip = new TextObject("{=!}You command too many men for hired watch work. (at most {MAX} in your party)").SetTextVariable("MAX", c.WorkMaxPartySize); return true; }
                if (c.GuardOnlyBelowGold > 0 && Hero.MainHero.Gold >= c.GuardOnlyBelowGold)
                { args.IsEnabled = false; args.Tooltip = new TextObject("{=!}No merchant believes a man of your wealth needs watch work. (under {MAX} gold)").SetTextVariable("MAX", c.GuardOnlyBelowGold); return true; }
                if (BestWeaponSkill() < c.GuardSkillRequired)
                { args.IsEnabled = false; args.Tooltip = new TextObject("{=!}They want someone who can handle a weapon. (best weapon skill {MIN} needed)").SetTextVariable("MIN", c.GuardSkillRequired); return true; }
                args.Tooltip = new TextObject("{=!}About {PAY} stags a night. Rowdy nights may earn a bonus - or a beating.")
                    .SetTextVariable("PAY", GuardPay(Here));
                return true;
            }
            catch (Exception e) { Log.Error("GuardCondition", e); return false; }
        }

        private static void StartWork(string menuId)
        {
            _hours = 0f;
            _earned = 0;
            _fedToday = false;
            GameMenu.SwitchToMenu(menuId);
        }

        private static bool WaitCondition(MenuCallbackArgs args)
        {
            return true;
        }

        private static void WorkInit(MenuCallbackArgs args)
        {
            try
            {
                if (Here != null && Here.SettlementComponent != null &&
                    !string.IsNullOrEmpty(Here.SettlementComponent.WaitMeshName))
                    args.MenuContext.SetBackgroundMeshName(Here.SettlementComponent.WaitMeshName);
            }
            catch { }
            RefreshText(args);
        }

        private static void RefreshText(MenuCallbackArgs args)
        {
            try
            {
                bool guard = args != null && args.MenuContext != null &&
                             args.MenuContext.GameMenu != null &&
                             args.MenuContext.GameMenu.StringId == "rc_work_guard";
                int pay = Here == null ? 0 : (guard ? GuardPay(Here) : LabourPay(Here));
                MBTextManager.SetTextVariable("RC_WORK_PAY", pay);
                MBTextManager.SetTextVariable("RC_WORK_EARNED", _earned);
            }
            catch { }
        }

        private static int _earned;

        // ------------------------------------------------------------------ ticks

        private static void LabourTick(MenuCallbackArgs args, CampaignTime dt)
        {
            try
            {
                if (!StillSafe(args)) return;
                _hours += (float)dt.ToHours;
                if (_hours < 24f) return;
                _hours -= 24f;

                var c = Settings.Current;
                bool halfPay = Saturated(Here);
                int pay = LabourPay(Here);
                GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, pay);
                _earned += pay;
                MarkWorked();
                Hero.MainHero.AddSkillXp(DefaultSkills.Athletics, c.WorkAthleticsXpPerDay);
                FeedFromMastersPot();
                Log.Player(halfPay
                    ? "A day's wage: " + pay + " stags. (little work left here - half pay)"
                    : "A day's wage: " + pay + " stags.");
                _fedToday = false;
                RefreshText(args);
            }
            catch (Exception e) { Log.Error("LabourTick", e); }
        }

        private static void GuardTick(MenuCallbackArgs args, CampaignTime dt)
        {
            try
            {
                if (!StillSafe(args)) return;
                _hours += (float)dt.ToHours;
                if (_hours < 24f) return;
                _hours -= 24f;

                var c = Settings.Current;
                var skillObj = BestMeleeSkillObject();
                Hero.MainHero.AddSkillXp(skillObj, c.GuardWeaponXpPerDay);

                bool brawl = MBRandom.RandomFloat < c.GuardBrawlChancePercent / 100f;
                if (brawl)
                {
                    int skill = Hero.MainHero.GetSkillValue(skillObj);
                    float winChance = Math.Min(0.9f, 0.30f + skill / 200f);
                    if (MBRandom.RandomFloat < winChance)
                    {
                        int pay = GuardPay(Here) + c.GuardBrawlBonus;
                        GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, pay);
                        _earned += pay;
                        Hero.MainHero.AddSkillXp(skillObj, 150f);
                        if (MBRandom.RandomFloat < 0.25f) ThankfulNotable();
                        Log.Player("Trouble at the gate - you cracked heads and kept the peace. " + pay + " stags, bonus included.");
                    }
                    else
                    {
                        int before = Hero.MainHero.HitPoints;
                        Hero.MainHero.HitPoints = Math.Max(5, before - c.GuardBrawlHealthLoss);
                        Log.Player("Toughs got the better of you in the dark. You wake bruised, the night unpaid.", true);
                    }
                }
                else
                {
                    int pay = GuardPay(Here);
                    GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, pay);
                    _earned += pay;
                    Log.Player("A quiet watch. " + pay + " stags for the night.");
                }
                RefreshText(args);
            }
            catch (Exception e) { Log.Error("GuardTick", e); }
        }

        private static bool StillSafe(MenuCallbackArgs args)
        {
            try
            {
                if (Here == null) return false;
                if (Here.IsUnderRaid || Here.IsUnderSiege)
                {
                    Log.Player("Alarm bells - all work stops.", true);
                    GameMenu.SwitchToMenu(_cameFrom);
                    return false;
                }
                return true;
            }
            catch { return false; }
        }

        private static void MarkWorked()
        {
            try
            {
                if (Days == null || LastDay == null || Here == null) return;
                int d;
                Days.TryGetValue(Here.StringId, out d);
                Days[Here.StringId] = d + 1;
                LastDay[Here.StringId] = Today;
            }
            catch { }
        }

        /// <summary>Dniowka jest z wiktem - gdy sakwy puste, gospodarz dokarmia.</summary>
        private static void FeedFromMastersPot()
        {
            try
            {
                if (_fedToday) return;
                if (MobileParty.MainParty.Food >= 2f) return;
                ItemObject grain = null;
                foreach (var id in new[] { "grain", "meat", "cheese", "butter", "fish" })
                {
                    grain = MBObjectManager.Instance.GetObject<ItemObject>(id);
                    if (grain != null) break;
                }
                if (grain == null) return;
                PartyBase.MainParty.ItemRoster.AddToCounts(grain, 1);
                _fedToday = true;
                Log.Player("The master feeds you from his own pot.");
            }
            catch { }
        }

        private static void ThankfulNotable()
        {
            try
            {
                if (Here == null || Here.Notables == null || Here.Notables.Count == 0) return;
                Hero pick = null;
                foreach (var n in Here.Notables)
                    if (n != null && n.IsAlive && n.IsGangLeader) { pick = n; break; }
                if (pick == null)
                {
                    int i = MBRandom.RandomInt(Here.Notables.Count);
                    pick = Here.Notables[i];
                }
                if (pick != null && pick.IsAlive)
                {
                    ChangeRelationAction.ApplyPlayerRelation(pick, 1);
                    Log.Player(pick.Name + " heard how you kept the peace. (+1 relation)");
                }
            }
            catch { }
        }
    }
}
