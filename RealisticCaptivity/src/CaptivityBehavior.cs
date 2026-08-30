using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace RealisticCaptivity
{
    public class CaptivityBehavior : CampaignBehaviorBase
    {
        public static CaptivityBehavior Instance;

        // stan zapisywany w save
        private List<string> _storedGear = new List<string>();  // "slot|itemId|modifierId"
        private string _captorHeroId = "";
        private bool _onParole;
        private bool _paroleBroken;
        private int _gearBuybackPrice;
        private bool _lowborn;
        private List<string> _companionGear = new List<string>();   // "heroId|slot|item|mod"
        private int _ransomDebt;
        private int _debtDaysUnpaid;
        private bool _debtOffered;
        private string _rescuerLeaderId = "";
        private int _rescueCooldown;

        public bool OnParole { get { return _onParole; } }
        public bool HasStoredGear { get { return _storedGear != null && _storedGear.Count > 0; } }

        public CaptivityBehavior() { Instance = this; }

        public override void SyncData(IDataStore dataStore)
        {
            try
            {
                dataStore.SyncData("rc_storedGear", ref _storedGear);
                dataStore.SyncData("rc_captorHeroId", ref _captorHeroId);
                dataStore.SyncData("rc_onParole", ref _onParole);
                dataStore.SyncData("rc_paroleBroken", ref _paroleBroken);
                dataStore.SyncData("rc_gearBuybackPrice", ref _gearBuybackPrice);
                dataStore.SyncData("rc_lowborn", ref _lowborn);
                dataStore.SyncData("rc_companionGear", ref _companionGear);
                dataStore.SyncData("rc_ransomDebt", ref _ransomDebt);
                dataStore.SyncData("rc_debtDaysUnpaid", ref _debtDaysUnpaid);
                dataStore.SyncData("rc_debtOffered", ref _debtOffered);
                dataStore.SyncData("rc_rescuerLeaderId", ref _rescuerLeaderId);
                dataStore.SyncData("rc_rescueCooldown", ref _rescueCooldown);
                dataStore.SyncData("rc_homeStash", ref _homeStash);
                dataStore.SyncData("rc_homeVault", ref _homeVault);
                dataStore.SyncData("rc_familyHomeGranted", ref _familyHomeGranted);
                dataStore.SyncData("rc_workDays", ref _workDays);
                dataStore.SyncData("rc_workLast", ref _workLast);
                _familyHomeSold = _familyHomeSold || Homes.FamilyHomeSold;
                dataStore.SyncData("rc_familyHomeSold", ref _familyHomeSold);
                Homes.FamilyHomeSold = _familyHomeSold;
                if (_homeStash == null) _homeStash = new Dictionary<string, ItemRoster>();
                if (_homeVault == null) _homeVault = new Dictionary<string, int>();
                if (_workDays == null) _workDays = new Dictionary<string, int>();
                if (_workLast == null) _workLast = new Dictionary<string, int>();
                Homes.Stash = _homeStash;
                Homes.Vault = _homeVault;
                Work.Days = _workDays;
                Work.LastDay = _workLast;
                if (_companionGear == null) _companionGear = new List<string>();
                if (_storedGear == null) _storedGear = new List<string>();
            }
            catch (Exception e) { Log.Error("SyncData", e); }
        }

        public override void RegisterEvents()
        {
            CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(this, OnHeroPrisonerTaken);
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.HeroPrisonerReleased.AddNonSerializedListener(this, OnHeroPrisonerReleased);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, CleanBreak.OnMapEventEnded);
        }

        private Dictionary<string, ItemRoster> _homeStash = new Dictionary<string, ItemRoster>();
        private Dictionary<string, int> _homeVault = new Dictionary<string, int>();
        private bool _familyHomeGranted;
        private Dictionary<string, int> _workDays = new Dictionary<string, int>();
        private Dictionary<string, int> _workLast = new Dictionary<string, int>();
        private bool _familyHomeSold;

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            try
            {
                Homes.Stash = _homeStash;
                Homes.Vault = _homeVault;
                Homes.FamilyHomeSold = _familyHomeSold;
                Work.Days = _workDays;
                Work.LastDay = _workLast;
                Homes.Add(starter);
                Work.Add(starter);
                Homes.GrantFamilyHome(ref _familyHomeGranted);
                Homes.AnnounceOwned();
                Dialogs.Add(starter);
                Log.Info("Sesja wystartowala, dialogi, domy i praca dodane.");
            }
            catch (Exception e) { Log.Error("OnSessionLaunched", e); }
        }

        // ---------------------------------------------------------------- pojmanie
        private void OnHeroPrisonerTaken(PartyBase capturer, Hero prisoner)
        {
            try
            {
                if (prisoner != Hero.MainHero)
                {
                    if (Settings.Current.StripCompanions && prisoner != null && prisoner.Clan == Clan.PlayerClan)
                        StripCompanionGear(prisoner);
                    return;
                }
                _onParole = false;
                _paroleBroken = false;
                _captorHeroId = (capturer != null && capturer.LeaderHero != null) ? capturer.LeaderHero.StringId : "";
                Log.Info("Gracz pojmany przez: " + (_captorHeroId == "" ? "(bez lorda)" : _captorHeroId));

                _lowborn = !HasNobleStanding();
                if (Settings.Current.StripEquipment) StripGear();
                LootBaggage(capturer);
                StartRescueMission();

                var captorLord = (capturer != null) ? capturer.LeaderHero : null;
                if (captorLord == null)
                {
                    Log.Info("Pojmany przez bande bez lorda - brak parolu, rynsztunek pojdzie na targ.");
                    Log.Player("You have fallen into the hands of brigands. No one here talks of ransom or honour.", true);
                }
                else if (!Settings.Current.ParoleEnabled) { }
                else if (_lowborn && Settings.Current.ParoleRequiresStatus)
                {
                    Log.Info("Gracz bez statusu - parol nie przysluguje.");
                    Log.Player("No one asks a nameless sellsword for his word of honour. You are thrown into the pit with the rest.", true);
                }
                else AskForParole(captorLord);
            }
            catch (Exception e) { Log.Error("OnHeroPrisonerTaken", e); }
        }

        private void StripGear()
        {
            try
            {
                if (HasStoredGear) { Log.Info("Rynsztunek juz zabrany wczesniej - pomijam."); return; }
                var eq = Hero.MainHero.BattleEquipment;
                int value = 0, taken = 0;
                for (int i = 0; i < 12; i++)
                {
                    var el = eq[i];
                    if (el.Item == null) continue;
                    _storedGear.Add(i + "|" + el.Item.StringId + "|" + (el.ItemModifier != null ? el.ItemModifier.StringId : ""));
                    value += el.Item.Value;
                    taken++;
                    eq[i] = new EquipmentElement(null);
                }
                if (Settings.Current.LeaveInRags || !Settings.Current.KeepCivilianClothes)
                {
                    var civ = Hero.MainHero.CivilianEquipment;
                    for (int i = 0; i < 12; i++)
                    {
                        var el = civ[i];
                        if (el.Item == null) continue;
                        _storedGear.Add((100 + i) + "|" + el.Item.StringId + "|" + (el.ItemModifier != null ? el.ItemModifier.StringId : ""));
                        value += el.Item.Value;
                        taken++;
                        civ[i] = new EquipmentElement(null);
                    }
                    if (Settings.Current.LeaveInRags) GiveRags();
                }
                _gearBuybackPrice = (int)(value * Settings.Current.BuybackPriceMultiplier);
                Log.Info("Zabrano " + taken + " przedmiotow, wartosc " + value + ", odkup za " + _gearBuybackPrice);
                if (taken > 0)
                    Log.Player("Your captors stripped you of your war gear. " + taken + " items taken.", true);
            }
            catch (Exception e) { Log.Error("StripGear", e); }
        }

        // ---------------------------------------------------------------- towarzysze
        private void StripCompanionGear(Hero companion)
        {
            try
            {
                var eq = companion.BattleEquipment;
                int taken = 0, value = 0;
                for (int i = 0; i < 12; i++)
                {
                    var el = eq[i];
                    if (el.Item == null) continue;
                    _companionGear.Add(companion.StringId + "|" + i + "|" + el.Item.StringId + "|" +
                                       (el.ItemModifier != null ? el.ItemModifier.StringId : ""));
                    value += el.Item.Value;
                    taken++;
                    eq[i] = new EquipmentElement(null);
                }
                if (taken > 0)
                {
                    _gearBuybackPrice += (int)(value * Settings.Current.BuybackPriceMultiplier);
                    Log.Info("Towarzysz " + companion.Name + " stracil " + taken + " przedmiotow.");
                    Log.Player(companion.Name + " was stripped of gear as well.", true);
                }
            }
            catch (Exception e) { Log.Error("StripCompanionGear", e); }
        }

        private void RestoreCompanionGear()
        {
            try
            {
                foreach (var entry in _companionGear)
                {
                    var p = entry.Split('|');
                    if (p.Length < 3) continue;
                    var hero = Hero.FindFirst(h => h.StringId == p[0]);
                    if (hero == null || !hero.IsAlive) continue;
                    int slot;
                    if (!int.TryParse(p[1], out slot)) continue;
                    var item = MBObjectManager.Instance.GetObject<ItemObject>(p[2]);
                    if (item == null) continue;
                    ItemModifier mod = null;
                    if (p.Length > 3 && !string.IsNullOrEmpty(p[3]))
                        mod = MBObjectManager.Instance.GetObject<ItemModifier>(p[3]);
                    hero.BattleEquipment[slot] = new EquipmentElement(item, mod);
                }
                _companionGear.Clear();
            }
            catch (Exception e) { Log.Error("RestoreCompanionGear", e); }
        }

        // ---------------------------------------------------------------- codzienny tick
        private void OnDailyTick()
        {
            try
            {
                PayDebt();
                if (!PlayerCaptivity.IsCaptive) return;

                CaptivityExtras.DailyStarvation(_lowborn, _onParole);

                if (CaptivityExtras.TryBanditDump((int)PlayerCaptivity.CaptivityStartTime.ElapsedDaysUntilNow)) return;

                var newCaptor = CaptivityExtras.TrySellPrisoner();
                if (newCaptor != null) _captorHeroId = newCaptor.StringId;

                RunRescueMission();
                OfferDebtDeal();
            }
            catch (Exception e) { Log.Error("OnDailyTick", e); }
        }

        // ---------------------------------------------------------------- odsiecz
        private void StartRescueMission()
        {
            try
            {
                _rescuerLeaderId = "";
                _rescueCooldown = 0;
                if (!Settings.Current.RescueEnabled) return;
                var party = Rescue.FindRescueParty();
                if (party == null)
                {
                    Log.Info("Brak druzyny rodu zdolnej do odsieczy.");
                    return;
                }
                _rescuerLeaderId = party.LeaderHero.StringId;
                Log.Info("Odsiecz wyrusza: " + party.LeaderHero.Name + " (" + party.Party.NumberOfHealthyMembers + " ludzi)");
                Log.Player(party.LeaderHero.Name + " gathers " + party.Party.NumberOfHealthyMembers +
                           " men and rides to your aid.");
            }
            catch (Exception e) { Log.Error("StartRescueMission", e); }
        }

        private void RunRescueMission()
        {
            try
            {
                if (!Settings.Current.RescueEnabled || string.IsNullOrEmpty(_rescuerLeaderId)) return;
                if (_rescueCooldown > 0) { _rescueCooldown--; return; }

                var leader = Hero.FindFirst(h => h.StringId == _rescuerLeaderId);
                if (leader == null || !leader.IsAlive || leader.IsPrisoner || leader.PartyBelongedTo == null)
                {
                    Log.Info("Odsiecz przepadla - dowodca niedostepny. Szukam nowej.");
                    StartRescueMission();
                    return;
                }
                var rescuer = leader.PartyBelongedTo;
                var captor = PlayerCaptivity.CaptorParty;
                if (captor == null) return;

                if (!Rescue.DriveTowardCaptor(rescuer, captor)) return;   // jeszcze w drodze

                var captorLord = captor.LeaderHero;
                if (captorLord == null)
                {
                    if (!Rescue.TryFightRescue(rescuer, captor))
                        _rescueCooldown = Settings.Current.RescueRetryDays;
                }
                else
                {
                    if (!Rescue.TryNegotiate(rescuer, captorLord))
                        _rescueCooldown = Settings.Current.RescueRetryDays;
                }
            }
            catch (Exception e) { Log.Error("RunRescueMission", e); }
        }

        // ---------------------------------------------------------------- dlug wykupu
        private void OfferDebtDeal()
        {
            try
            {
                var s = Settings.Current;
                if (!s.RansomDebtEnabled || _debtOffered || _ransomDebt > 0) return;
                if (PlayerCaptivity.CaptiveTimeInDays < s.DebtOfferAfterDays) return;
                if (_lowborn) return;                       // nikt nie da kredytu nikomu
                var captor = GetCaptorHero();
                if (captor == null) return;

                int amount = (int)(Campaign.Current.PlayerCaptivity.CurrentRansomAmount * s.DebtInterest);
                if (amount < 1) amount = 1000;
                _debtOffered = true;

                InformationManager.ShowInquiry(new InquiryData(
                    "A Debt of Honour",
                    captor.Name + " grows tired of feeding you. He offers to release you now against a written promise of "
                    + amount + " gold, to be paid in instalments.\n\nDefault on it and your name is worth nothing.",
                    true, true, "I accept the debt", "I would rather rot",
                    () =>
                    {
                        try
                        {
                            _ransomDebt = amount;
                            _debtDaysUnpaid = 0;
                            Log.Info("Gracz przyjal dlug " + amount);
                            Log.Player("You signed the promise. You owe " + amount + " gold.", true);
                            EndCaptivityAction.ApplyByRansom(Hero.MainHero, captor);
                        }
                        catch (Exception e) { Log.Error("DebtAccept", e); }
                    },
                    () => { Log.Player("You refuse. The cell door closes again."); }), true);
            }
            catch (Exception e) { Log.Error("OfferDebtDeal", e); }
        }

        /// <summary>Wysylannik reczy za reszte: wychodzisz teraz, splacasz ratami.</summary>
        internal void PledgeDebt(int amount, Hero creditor)
        {
            try
            {
                if (amount <= 0) return;
                _ransomDebt += amount;
                _debtDaysUnpaid = 0;
                _debtOffered = true;
                if (creditor != null) _captorHeroId = creditor.StringId;
                Log.Info("Zareczono dlug: " + amount + " wobec " + (creditor != null ? creditor.Name.ToString() : "?"));
            }
            catch (Exception e) { Log.Error("PledgeDebt", e); }
        }

        private void PayDebt()
        {
            try
            {
                var s = Settings.Current;
                if (_ransomDebt <= 0) return;
                int pay = Math.Min(s.DebtDailyInstalment, Math.Min(_ransomDebt, Hero.MainHero.Gold));
                if (PlayerCaptivity.IsCaptive) return;   // w lochu nie splacasz - nikt cie do kasy nie puszcza
                if (pay > 0)
                {
                    var captor = GetCaptorHero();
                    GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, captor, pay);
                    _ransomDebt -= pay;
                    _debtDaysUnpaid = 0;
                    if (_ransomDebt <= 0)
                    {
                        _ransomDebt = 0;
                        Log.Player("Your ransom debt is paid in full.");
                        Log.Info("Dlug splacony.");
                    }
                }
                else
                {
                    _debtDaysUnpaid++;
                    if (_debtDaysUnpaid == s.DebtGraceDays)
                    {
                        var clan = Clan.PlayerClan;
                        if (clan != null) clan.Renown = Math.Max(0f, clan.Renown - s.DebtDefaultRenownLoss);
                        var captor = GetCaptorHero();
                        if (captor != null) ChangeRelationAction.ApplyPlayerRelation(captor, -25);
                        Log.Player("You have defaulted on your ransom debt. Your word is worth nothing now. (-"
                                   + s.DebtDefaultRenownLoss + " renown)", true);
                        Log.Info("Dlug niesplacony - hanba.");
                    }
                }
            }
            catch (Exception e) { Log.Error("PayDebt", e); }
        }

        /// <summary>Znajduje najtansze okrycie w grze i zaklada je jako lachmany.</summary>
        private void GiveRags()
        {
            try
            {
                ItemObject rags = null;
                foreach (var item in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
                {
                    if (item == null || item.ItemType != ItemObject.ItemTypeEnum.BodyArmor) continue;
                    if (item.Value <= 0) continue;
                    var id = item.StringId.ToLower();
                    bool looksLikeRags = id.Contains("rag") || id.Contains("burlap") || id.Contains("sack")
                                         || id.Contains("peasant") || id.Contains("beggar");
                    if (rags == null || (looksLikeRags && item.Value < 60) || item.Value < rags.Value)
                        if (looksLikeRags || rags == null || item.Value < rags.Value) rags = item;
                }
                if (rags == null) { Log.Info("Nie znaleziono lachmanow - gracz zostaje w bieliznie."); return; }
                Hero.MainHero.CivilianEquipment[EquipmentIndex.Body] = new EquipmentElement(rags);
                Hero.MainHero.BattleEquipment[EquipmentIndex.Body] = new EquipmentElement(rags);
                Log.Info("Zalozono lachmany: " + rags.StringId + " (wartosc " + rags.Value + ")");
            }
            catch (Exception e) { Log.Error("GiveRags", e); }
        }

        /// <summary>Zdobywca przetrzasa juki: zabiera czesc ladunku i zlota.</summary>
        private void LootBaggage(PartyBase capturer)
        {
            try
            {
                var s = Settings.Current;
                var party = MobileParty.MainParty;
                int itemsTaken = 0;

                if (party != null && party.ItemRoster != null && s.LootPartyInventoryPercent > 0)
                {
                    var roster = party.ItemRoster;
                    for (int i = roster.Count - 1; i >= 0; i--)
                    {
                        var el = roster[i];
                        if (el.EquipmentElement.Item == null) continue;
                        int amount = el.Amount * s.LootPartyInventoryPercent / 100;
                        if (amount <= 0) continue;
                        roster.AddToCounts(el.EquipmentElement, -amount);
                        if (capturer != null && capturer.IsMobile && capturer.ItemRoster != null)
                            capturer.ItemRoster.AddToCounts(el.EquipmentElement, amount);
                        itemsTaken += amount;
                    }
                }

                int goldTaken = 0;
                if (s.LootGoldPercent > 0)
                {
                    goldTaken = Hero.MainHero.Gold * s.LootGoldPercent / 100;
                    if (goldTaken > 0)
                    {
                        var captorHero = (capturer != null) ? capturer.LeaderHero : null;
                        GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, captorHero, goldTaken);
                    }
                }

                Log.Info("Przetrzasnieto juki: " + itemsTaken + " przedmiotow, " + goldTaken + " zlota.");
                if (itemsTaken > 0 || goldTaken > 0)
                    Log.Player("They went through your baggage: " + itemsTaken + " items and " + goldTaken + " gold are gone.", true);
            }
            catch (Exception e) { Log.Error("LootBaggage", e); }
        }

        // ---------------------------------------------------------------- parol
        private void AskForParole(Hero captor)
        {
            try
            {
                var txt = "Your captor, " + captor.Name +
                          ", demands your word of honour that you will not attempt to escape.\n\n" +
                          "Give your word: lighter confinement and a cheaper ransom (-" +
                          (int)((1f - Settings.Current.ParoleRansomDiscount) * 100) + "%). Break it and you are disgraced: -" +
                          Settings.Current.ParoleRenownLoss + " renown and " + Settings.Current.ParoleRelationPenalty +
                          " relation with your captor's whole house.";
                InformationManager.ShowInquiry(new InquiryData(
                    "Word of Honour", txt, true, true,
                    "I give my word", "I refuse",
                    () => { _onParole = true; Log.Info("Gracz dal slowo honoru."); Log.Player("You gave your word. Escaping now would cost you your good name."); },
                    () => { _onParole = false; Log.Info("Gracz odmowil parolu."); Log.Player("You refused. They throw you into the pit without ceremony.", true); }), true);
            }
            catch (Exception e) { Log.Error("AskForParole", e); }
        }

        internal void BreakParole()
        {
            try
            {
                if (!_onParole) return;
                _onParole = false;
                _paroleBroken = true;
                var clan = Hero.MainHero.Clan;
                if (clan != null) clan.Renown = Math.Max(0f, clan.Renown - Settings.Current.ParoleRenownLoss);
                var captor = GetCaptorHero();
                if (captor != null)
                {
                    ChangeRelationAction.ApplyPlayerRelation(captor, Settings.Current.ParoleRelationPenalty);
                    if (captor.Clan != null)
                        foreach (var h in captor.Clan.Heroes)
                            if (h != captor && h.IsAlive)
                                ChangeRelationAction.ApplyPlayerRelation(h, Settings.Current.ParoleRelationPenalty / 2);
                }
                Log.Info("Parol zlamany.");
                Log.Player("You broke your word of honour. Word of this will travel far.", true);
            }
            catch (Exception e) { Log.Error("BreakParole", e); }
        }

        internal void OnFailedEscape()
        {
            try
            {
                var s = Settings.Current;
                int loss = (int)(Hero.MainHero.MaxHitPoints * (s.FailedEscapeHealthLossPercent / 100f));
                Hero.MainHero.HitPoints = Math.Max(1, Hero.MainHero.HitPoints - loss);
                var captor = GetCaptorHero();
                if (captor != null) ChangeRelationAction.ApplyPlayerRelation(captor, s.FailedEscapeRelationPenalty);
                Log.Info("Nieudana ucieczka, -" + loss + " HP.");
                Log.Player("Your escape attempt failed. The guards beat you half senseless (-" + loss + " health).", true);
            }
            catch (Exception e) { Log.Error("OnFailedEscape", e); }
        }

        /// <summary>Czy gracz nalezy do stanu, ktoremu przysluguje rycerski parol i okup.</summary>
        internal bool HasNobleStanding()
        {
            try
            {
                var clan = Hero.MainHero.Clan;
                if (clan == null) return false;
                if (clan.Settlements != null && clan.Settlements.Count > 0) return true;   // masz ziemie
                if (clan.Kingdom != null) return true;                                    // jestes wasalem
                if (clan.Renown >= Settings.Current.ParoleMinRenown) return true;          // jestes slawny
                return false;
            }
            catch (Exception e) { Log.Error("HasNobleStanding", e); return false; }
        }

        internal bool IsLowborn { get { return _lowborn; } }
        internal int RansomDebt { get { return _ransomDebt; } }

        private void OnHeroPrisonerReleased(Hero prisoner, PartyBase party, IFaction faction, EndCaptivityDetail detail, bool showNotification = true)
        {
            try
            {
                if (prisoner != Hero.MainHero)
                {
                    // rozbieranie jest TYLKO przy pojmaniu (z niewoli wychodzi
                    // sie nago - sprzet trzyma porywacz, ksiega odkupu pamieta).
                    // Wczesniej kopiuj-wklej z OnHeroPrisonerTaken rozbieral
                    // towarzysza DRUGI raz przy uwolnieniu: sprzet przywrocony
                    // mu po wyjsciu gracza wracal do ksiegi-limbo i sztucznie
                    // podbijal cene odkupu.
                    return;
                }
                OnCaptivityEnded();
                CaptivityExtras.ApplyHumiliation(detail);
                RestoreCompanionGear();
                var captor = GetCaptorHero();
                if (HasStoredGear && Settings.Current.FenceGearWhenNoLord && (captor == null || !captor.IsAlive))
                    FenceGear();
            }
            catch (Exception e) { Log.Error("OnHeroPrisonerReleased", e); }
        }

        /// <summary>Bandyci nie handluja - sprzedaja lup paserowi. Rynsztunek laduje na targu najblizszego miasta.</summary>
        private void FenceGear()
        {
            try
            {
                Settlement best = null;
                float bestDist = float.MaxValue;
                var myPos = MobileParty.MainParty.GetPosition2D;
                foreach (var st in Settlement.All)
                {
                    if (st == null || !st.IsTown) continue;
                    float d = st.GetPosition2D.Distance(myPos);
                    if (d < bestDist) { bestDist = d; best = st; }
                }
                if (best == null) { Log.Info("Brak miasta - rynsztunek przepadl."); _storedGear.Clear(); return; }

                int count = 0;
                foreach (var entry in _storedGear)
                {
                    var parts = entry.Split('|');
                    if (parts.Length < 2) continue;
                    var item = MBObjectManager.Instance.GetObject<ItemObject>(parts[1]);
                    if (item == null) continue;
                    ItemModifier mod = null;
                    if (parts.Length > 2 && !string.IsNullOrEmpty(parts[2]))
                        mod = MBObjectManager.Instance.GetObject<ItemModifier>(parts[2]);
                    best.ItemRoster.AddToCounts(new EquipmentElement(item, mod), 1);
                    count++;
                }
                _storedGear.Clear();
                _gearBuybackPrice = 0;
                Log.Info("Rynsztunek (" + count + " szt.) wystawiony na targu: " + best.Name);
                Log.Player("Your war gear has surfaced on the market in " + best.Name + ". Buy it back there - if no one beats you to it.", true);
            }
            catch (Exception e) { Log.Error("FenceGear", e); }
        }

        internal void OnCaptivityEnded()
        {
            try { _onParole = false; Log.Info("Niewola zakonczona. Rynsztunek w rekach zdobywcy: " + HasStoredGear); }
            catch (Exception e) { Log.Error("OnCaptivityEnded", e); }
        }

        // ---------------------------------------------------------------- odkup rynsztunku
        internal Hero GetCaptorHero()
        {
            try
            {
                if (string.IsNullOrEmpty(_captorHeroId)) return null;
                return Hero.FindFirst(h => h.StringId == _captorHeroId);
            }
            catch { return null; }
        }

        internal bool CanBuyBackFrom(Hero hero)
        {
            return Settings.Current.BuybackEnabled && HasStoredGear && hero != null
                   && !Hero.MainHero.IsPrisoner && hero.StringId == _captorHeroId;
        }

        internal int BuybackPrice { get { return Math.Max(1, _gearBuybackPrice); } }

        internal bool BuyBackGear()
        {
            try
            {
                if (Hero.MainHero.Gold < BuybackPrice) return false;
                var captor = GetCaptorHero();
                GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, captor, BuybackPrice);
                RestoreGear();
                Log.Player("You bought back your war gear for " + BuybackPrice + " gold.");
                return true;
            }
            catch (Exception e) { Log.Error("BuyBackGear", e); return false; }
        }

        private void RestoreGear()
        {
            try
            {
                var battle = Hero.MainHero.BattleEquipment;
                var civ = Hero.MainHero.CivilianEquipment;
                foreach (var entry in _storedGear)
                {
                    var parts = entry.Split('|');
                    if (parts.Length < 2) continue;
                    int slot;
                    if (!int.TryParse(parts[0], out slot)) continue;
                    var item = MBObjectManager.Instance.GetObject<ItemObject>(parts[1]);
                    if (item == null) continue;
                    ItemModifier mod = null;
                    if (parts.Length > 2 && !string.IsNullOrEmpty(parts[2]))
                        mod = MBObjectManager.Instance.GetObject<ItemModifier>(parts[2]);
                    if (slot >= 100) civ[slot - 100] = new EquipmentElement(item, mod);
                    else battle[slot] = new EquipmentElement(item, mod);
                }
                _storedGear.Clear();
                _gearBuybackPrice = 0;
                Log.Info("Rynsztunek przywrocony.");
            }
            catch (Exception e) { Log.Error("RestoreGear", e); }
        }
    }
}
