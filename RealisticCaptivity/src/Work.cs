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
        private static bool _stopWork;                     // opcja "Enough of this toil" podnosi, tick przelacza

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

        /// <summary>Dniowka robotnika w tej osadzie (pensy, z ulamkiem): stawka historyczna x poziom plac miasta (TownWageLink - ten sam co
        /// w Armoury); wies - czesc stawki miasta targowego. Jeff 07.10: koszty w miescie z dobrobytu i stawek historycznych.</summary>
        private static float DayWage(Settlement s)
        {
            var c = Settings.Current;
            float w = Math.Max(0f, c.LabourerDayWage) * TownWageLink.Index(s);
            return s.IsVillage ? w * Math.Max(0f, c.VillageWageShare) : w;
        }

        /// <summary>Kasa osady (miasto albo wies; 0 - brak).</summary>
        internal static int CofferOf(Settlement s)
        {
            var sc = s != null ? s.SettlementComponent : null;
            return sc != null ? Math.Max(0, sc.Gold) : 0;
        }

        /// <summary>Czy osada ma z czego zaplacic pelna dniowke (przy stawkach historycznych placi kasa osady).</summary>
        private static bool CanPay(Settlement s)
        {
            return !Settings.Current.HistoricalTownRates || Payable(s) >= Math.Max(1, (int)Math.Ceiling(DayWage(s)));
        }

        // ------------------------------------------------------------------ E8/RC (259): praca a nadwyzka miasta

        private static int _e8Pays, _e8Due, _e8Paid, _e8Stops, _e8NoHire;      // liczniki sesji do linii "Praca (E8/RC)"
        private static string _noHireKey;                                       // osada|doba - odmowa najmu w logu raz na osade i dobe

        /// <summary>Stan sesji paczki E8/RC - zerowany przy kazdej kampanii (konstruktor CaptivityBehavior, regula R8).</summary>
        internal static void ResetE8() { _e8Pays = _e8Due = _e8Paid = _e8Stops = _e8NoHire = 0; _noHireKey = null; }

        /// <summary>E8/RC (WorkRespectsReserve; LAWA 7/16, 8, 10): przy stawkach historycznych placa z kasy osady patrzy na NADWYZKE miasta (regula R1 etapu 8 -
        /// TownWageLink.SpareD4), dniowka ponizej pensa nie jest zaokraglana w gore do 1, a brak nadwyzki zatrzymuje prace w toku.</summary>
        private static bool ReserveOn { get { var c = Settings.Current; return c.HistoricalTownRates && c.WorkRespectsReserve; } }

        /// <summary>Ile osada moze wydac na place: przy WorkRespectsReserve nadwyzka (miasto - D4 z Armoury, wies - cala kasa), inaczej cala kasa (jak dotad).</summary>
        private static int Payable(Settlement s) { return ReserveOn ? TownWageLink.SpareD4(s) : CofferOf(s); }

        /// <summary>Linia wyplaty: nalezne, zaplacone, nadwyzka przed wyplata (zaplacone nigdy ponad nadwyzke - prog RC-1) i liczniki sesji.</summary>
        private static void NotePay(string what, Settlement s, int due, int paid, int spareBefore)
        {
            _e8Pays++; _e8Due += due; _e8Paid += paid;
            Log.Info("Praca (E8/RC): " + what + " w " + s.Name + " | nalezne " + due + ", zaplacone " + paid + ", nadwyzka osady przed wyplata " + spareBefore + ", kasa po " + CofferOf(s)
                     + (s.Town == null ? " (wies - cala kasa)" : TownWageLink.SpareLinked ? " (nadwyzka D4 Armoury)" : " (rezerwa TownRentFloorGold - bez Seal31.Spare)")
                     + " | w tej sesji: wyplat " + _e8Pays + ", nalezne " + _e8Due + ", zaplacone " + _e8Paid + ", przerwan pracy " + _e8Stops + ", odmow najmu " + _e8NoHire
                     + ", potkniec odczytu nadwyzki " + TownWageLink.SpareStumbles);
        }

        /// <summary>Osada nie najmuje, bo nie ma nadwyzki na dniowke - linia raz na osade i dobe (warunek opcji menu liczy sie przy kazdym odswiezeniu).</summary>
        private static void NoteNoHire(Settlement s)
        {
            try
            {
                if (!ReserveOn || s == null) return;
                string key = s.StringId + "|" + Today;
                if (key == _noHireKey) return;
                _noHireKey = key; _e8NoHire++;
                Log.Info("Praca (E8/RC): " + s.Name + " nie najmuje - nadwyzka " + TownWageLink.SpareD4(s) + " (kasa " + CofferOf(s) + ") ponizej dniowki "
                         + DayWage(s).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "; odmow najmu w tej sesji " + _e8NoHire);
            }
            catch { }
        }

        /// <summary>E8/RC (LAWA 10): osada nie ma nadwyzki na kolejna wyplate - praca w toku staje. Przelaczenie z TIKU menu oczekiwania (jak StillSafe i
        /// _stopWork - wzorzec gry); z OPCJI menu oczekiwania SwitchToMenu jest zakazane (CTD).</summary>
        private static void StopForPurse(string what)
        {
            _e8Stops++;
            Log.Info("Praca (E8/RC): " + what + " w " + Here.Name + " przerwana - osada nie ma nadwyzki na kolejna wyplate (nadwyzka " + Payable(Here) + ", kasa " + CofferOf(Here)
                     + "); przerwan pracy w tej sesji " + _e8Stops);
            Log.Player("There is no coin to spare for hired hands in " + Here.Name + " - the work ends here.", true);
            GameMenu.SwitchToMenu(_cameFrom);
        }

        /// <summary>Komunikat dniowki: pelna, czesciowa albo zadna (pusta kasa osady).</summary>
        private static string WageNote(int paid, int due, string what)
        {
            if (paid <= 0) return "No one here has the coin to pay you - " + what + " goes unpaid.";
            if (paid < due) return "Only " + paid + " of " + due + " stags - the purse here is thin.";
            return null;
        }

        /// <summary>Wyplata od osady (kasa miasta albo wsi) - najwyzej tyle, ile osada ma; 0 = nie ma czym zaplacic.</summary>
        private static int PayFromSettlement(Settlement s, int pay)
        {
            var sc = s != null ? s.SettlementComponent : null;
            if (sc == null) return 0;
            int give = Math.Min(pay, ReserveOn ? TownWageLink.SpareD4(s) : Math.Max(0, sc.Gold));   // E8/RC: tylko z nadwyzki (R1)
            if (give > 0) GiveGoldAction.ApplyForSettlementToCharacter(s, Hero.MainHero, give, true);
            return give;
        }

        private static int LabourPay(Settlement s)
        {
            var c = Settings.Current;
            if (c.HistoricalTownRates)
                return Math.Max(c.WorkRespectsReserve ? 0 : 1, MBRandom.RoundRandomized(DayWage(s) * (Saturated(s) ? 0.5f : 1f)));   // w calych pensach, srednio co do ulamka; E8/RC (LAWA 8): bez podlogi 1 (przeplacala dniowki ponizej pensa i kasowala polowe placy po nasyceniu)
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
            if (c.HistoricalTownRates) return Math.Max(c.WorkRespectsReserve ? 0 : 1, MBRandom.RoundRandomized(DayWage(s)));   // noc na murach - dniowka robotnika; E8/RC (LAWA 8): bez podlogi 1
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
                    // flaga -> tick przelaczy na _cameFrom (SwitchToMenu z ticka
                    // to wzorzec vanilla, tak robi juz StillSafe). Z opcji
                    // wait-menu SwitchToMenu ZAKAZANE (CTD), a ExitToLast
                    // niszczy MenuContext: we wsi zostawia gracza BEZ menu
                    // z wiszacym PlayerEncounter, w miescie laduje w town_outside.
                    // Zegar ruszamy sami - ratunek z deadlocka po load w menu
                    delegate (MenuCallbackArgs a)
                    {
                        _stopWork = true;
                        try { Campaign.Current.TimeControlMode = CampaignTimeControlMode.StoppablePlay; a.MenuContext.GameMenu.StartWait(); } catch { }
                    }, true, 9);
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
                if (!CanPay(Here))
                { NoteNoHire(Here); args.IsEnabled = false; args.Tooltip = new TextObject(ReserveOn ? "{=!}No one here has the coin to hire a hand - " + Here.Name + " has nothing to spare." : "{=!}No one here has the coin to hire a hand - the purse of " + Here.Name + " is empty."); return true; }
                args.Tooltip = new TextObject("{=!}About {PAY} stags a day, food included. Hard graft builds Athletics.")
                    .SetTextVariable("PAY", c.HistoricalTownRates ? (ReserveOn ? (DayWage(Here) * (Saturated(Here) ? 0.5f : 1f)).ToString("0.##") : Math.Max(1f, DayWage(Here) * (Saturated(Here) ? 0.5f : 1f)).ToString("0.#")) : LabourPay(Here).ToString());   // poprawka po audycie TOWARY 3: ta sama polowa co LabourPay w miejscu "nasyconym"; E8/RC: bez podlogi 1 jak wyplata
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
                if (!CanPay(Here))
                { NoteNoHire(Here); args.IsEnabled = false; args.Tooltip = new TextObject(ReserveOn ? "{=!}The merchants of " + Here.Name + " have no coin to spare for a watchman." : "{=!}The merchants of " + Here.Name + " have no coin left to hire a watchman."); return true; }
                args.Tooltip = new TextObject("{=!}About {PAY} stags a night. Rowdy nights may earn a bonus - or a beating.")
                    .SetTextVariable("PAY", c.HistoricalTownRates ? DayWage(Here).ToString("0.#") : GuardPay(Here).ToString());
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
                _stopWork = false;
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
                if (ReserveOn && Here != null)   // E8/RC: dniowka oczekiwana (bez podlogi 1 rzut groszy moze dac 0 - menu nie pokazuje losu)
                    MBTextManager.SetTextVariable("RC_WORK_PAY", (DayWage(Here) * (!guard && Saturated(Here) ? 0.5f : 1f)).ToString("0.##"));
                else
                {
                    int pay = Here == null ? 0 : (guard ? GuardPay(Here) : LabourPay(Here));
                    MBTextManager.SetTextVariable("RC_WORK_PAY", pay);
                }
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
                if (_stopWork) { _stopWork = false; GameMenu.SwitchToMenu(_cameFrom); return; }
                if (!StillSafe(args)) return;
                _hours += (float)dt.ToHours;
                if (_hours < 24f) return;
                _hours -= 24f;

                var c = Settings.Current;
                bool halfPay = Saturated(Here);
                bool rsv = ReserveOn;                                                   // E8/RC
                int spare = rsv ? TownWageLink.SpareD4(Here) : 0;                       // nadwyzka przed wyplata (do linii)
                int pay = LabourPay(Here), due = pay;
                if (c.HistoricalTownRates) pay = PayFromSettlement(Here, pay);          // place daje kasa osady, nie nikt
                else GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, pay);
                _earned += pay;
                bool unpaid = rsv && due > 0 && pay <= 0;                               // E8/RC (LAWA 10): osada nie miala z czego - dzien niezaplacony
                string thin = rsv && due <= 0 ? null : WageNote(pay, due, "the day's work");   // due 0 = grosze ponizej pensa (los), nie pusta kasa
                if (thin != null) { Log.Player(thin, true); Log.Info("Praca dniowkowa w " + Here.Name + ": kasa osady zaplacila " + pay + " z " + due); }
                if (rsv) NotePay("dniowka", Here, due, pay, spare);
                if (!unpaid) MarkWorked();                                              // E8/RC (LAWA 10): niezaplacony dzien nie liczy sie do nasycenia
                Hero.MainHero.AddSkillXp(DefaultSkills.Athletics, c.WorkAthleticsXpPerDay);
                FeedFromMastersPot();
                if (rsv && due <= 0) Log.Player("A lean day - the wage here is under a stag, and today's share of it came to nothing.");
                else if (!unpaid)
                    Log.Player(halfPay
                        ? "A day's wage: " + pay + " stags. (little work left here - half pay)"
                        : "A day's wage: " + pay + " stags.");
                _fedToday = false;
                if (rsv && (unpaid || !CanPay(Here))) { StopForPurse("dniowka"); return; }   // E8/RC (LAWA 10): bez nadwyzki na kolejny dzien praca staje
                RefreshText(args);
            }
            catch (Exception e) { Log.Error("LabourTick", e); }
        }

        private static void GuardTick(MenuCallbackArgs args, CampaignTime dt)
        {
            try
            {
                if (_stopWork) { _stopWork = false; GameMenu.SwitchToMenu(_cameFrom); return; }
                if (!StillSafe(args)) return;
                _hours += (float)dt.ToHours;
                if (_hours < 24f) return;
                _hours -= 24f;

                var c = Settings.Current;
                var skillObj = BestMeleeSkillObject();
                Hero.MainHero.AddSkillXp(skillObj, c.GuardWeaponXpPerDay);
                bool rsv = ReserveOn, unpaidNight = false;                              // E8/RC

                bool brawl = MBRandom.RandomFloat < c.GuardBrawlChancePercent / 100f;
                if (brawl)
                {
                    int skill = Hero.MainHero.GetSkillValue(skillObj);
                    float winChance = Math.Min(0.9f, 0.30f + skill / 200f);
                    if (MBRandom.RandomFloat < winChance)
                    {
                        int pay = GuardPay(Here) + (c.HistoricalTownRates ? MBRandom.RoundRandomized(DayWage(Here) * Math.Max(0f, c.GuardBrawlBonusDays)) : c.GuardBrawlBonus);
                        int dueB = pay;
                        int spareB = rsv ? TownWageLink.SpareD4(Here) : 0;
                        if (c.HistoricalTownRates) pay = PayFromSettlement(Here, pay);
                        else GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, pay);
                        string thinB = rsv && dueB <= 0 ? null : WageNote(pay, dueB, "the night's watch");
                        if (thinB != null) Log.Player(thinB, true);
                        if (rsv) { NotePay("warta z premia", Here, dueB, pay, spareB); unpaidNight = dueB > 0 && pay <= 0; }
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
                    int pay = GuardPay(Here), dueG = pay;
                    int spareG = rsv ? TownWageLink.SpareD4(Here) : 0;
                    if (c.HistoricalTownRates) pay = PayFromSettlement(Here, pay);
                    else GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, pay);
                    string thinG = rsv && dueG <= 0 ? null : WageNote(pay, dueG, "the night's watch");
                    if (thinG != null) Log.Player(thinG, true);
                    if (rsv) { NotePay("warta", Here, dueG, pay, spareG); unpaidNight = dueG > 0 && pay <= 0; }
                    _earned += pay;
                    Log.Player(rsv && dueG <= 0 ? "A quiet watch - the night's share of a thin wage came to nothing." : "A quiet watch. " + pay + " stags for the night.");
                }
                if (rsv && (unpaidNight || !CanPay(Here))) { StopForPurse("warta"); return; }   // E8/RC (LAWA 10): bez nadwyzki na kolejna noc warta staje
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
