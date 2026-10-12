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
        private int _captiveGoldYesterday = -1;   // dziennik zlota jenca (diagnostyka, nie w save)
        private int _debtDaysUnpaid;
        private bool _debtOffered;
        private string _rescuerLeaderId = "";
        private int _rescueCooldown;
        // E8/RC (259, DebtToCaptorHome): pojmujacy bez bohatera - dokad ida splaty dlugu okupu i wykup rynsztunku (regula decyzji 9: kasa osady milicji /
        // zalogi / lochu albo kiesa bandy). Zapis: jedna sekcja z wersja rc_e8_captorHome; stary zapis bez niej = puste (brak odbiorcy - nic nie pobrane).
        private string _captorHomeId = "";        // StringId osady
        private string _captorBandId = "";        // StringId partii bez bohatera i bez osady (banda)
        private int _e8DebtHomeN, _e8DebtHomeGold, _e8DebtNoTaker;   // liczniki sesji (nie w save; nowa instancja = nowa kampania, R8)

        // ODBICIE SILA, ALE NIE PRZEZ SILNIK (13.09): wlasna odsiecz rodu konczy
        // niewole przez ApplyByEscape (Rescue.cs:76), a nie ReleasedAfterBattle -
        // sam detal by jej nie zlapal i rynsztunek poszedlby na targ mimo tego,
        // ze ludzie Jeffa wlasnie wybili straznikow. Flaga zyje jedna klatke,
        // od wywolania w Rescue do naszego OnHeroPrisonerReleased - do save'a
        // nie ma po co trafiac.
        private bool _rescuedByForce;

        public bool OnParole { get { return _onParole; } }
        public bool HasStoredGear { get { return _storedGear != null && _storedGear.Count > 0; } }

        /// <summary>Wlasna odsiecz wlasnie przecina wiezy - rynsztunek ma wrocic, nie isc na targ.</summary>
        internal void MarkRescuedByForce() { _rescuedByForce = true; }

        public CaptivityBehavior() { Instance = this; Work.ResetE8(); }   // E8/RC: liczniki pracy od zera przy kazdej kampanii (R8)

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
                // E8/RC: sekcja z wersja ("1|osada|partia"); stary zapis jej nie ma - pola zostaja puste, bez bledu
                string e8 = "1|" + (_captorHomeId ?? "") + "|" + (_captorBandId ?? "");
                dataStore.SyncData("rc_e8_captorHome", ref e8);
                if (dataStore.IsLoading)
                {
                    _captorHomeId = ""; _captorBandId = "";
                    var e8p = (e8 ?? "").Split('|');
                    if (e8p.Length >= 3 && e8p[0] == "1") { _captorHomeId = e8p[1]; _captorBandId = e8p[2]; }
                }
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
                if (Settings.Current.WorkRespectsReserve) TownWageLink.LinkSpare();   // E8/RC: linia PODPIETY/BRAK nadwyzki D4 raz na proces, juz przy starcie sesji
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
                NoteCaptorHome(capturer);   // E8/RC

                _lowborn = !HasNobleStanding();
                if (Settings.Current.StripEquipment || Settings.Current.CaptorTakesAllFromPlayer) StripGear();   // E5 decyzja 9: caly rynsztunek
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
                if (!PlayerCaptivity.IsCaptive) { _captiveGoldYesterday = -1; return; }

                // DZIENNIK ZLOTA JENCA (Jeff 01.09: "co jakis czas mam minus
                // 200 w niewoli"). RC w niewoli zlota NIE bierze (dowod: brak
                // takiego kodu) - ubytek nalicza vanilla/BK (zold klanu idzie
                // dalej). Log dzien po dniu pokaze kwote i pozwoli wskazac
                // platnika, zanim cokolwiek zaczniemy mrozic.
                try
                {
                    int g = Hero.MainHero.Gold;
                    if (_captiveGoldYesterday >= 0 && g != _captiveGoldYesterday)
                        Log.Info("NIEWOLA zloto: " + _captiveGoldYesterday + " -> " + g
                                 + " (zmiana " + (g - _captiveGoldYesterday) + ") - RC nic nie pobral.");
                    _captiveGoldYesterday = g;
                }
                catch { }

                // KTO CIE TERAZ TRZYMA (Jeff 13.09: "popraw przeniesienie do lochu").
                // _captorHeroId zapisywalismy RAZ, przy pojmaniu w polu. Gdy silnik
                // przekazywal gracza do miejskiego lochu (CheckCaptivityChange podmienia
                // PlayerCaptivity.CaptorParty na party osady), my nadal pamietalismy
                // lorda, ktory dawno odjechal - albo, po bandytach, nikogo. Skutki:
                // wlasciciel miasta stojacy przed toba nie mogl odsprzedac ci rynsztunku
                // (CanBuyBackFrom porownuje StringId), "Dlug honorowy" nie mial komu byc
                // zaproponowany (OfferDebtDeal wychodzi przy captor == null), a przy
                // wyjsciu z lochu warunek fence widzial pustke i wyrzucal sprzet na targ.
                try
                {
                    var cp = PlayerCaptivity.CaptorParty;
                    if (cp != null && cp.IsSettlement && cp.Settlement != null)
                    {
                        var holder = cp.Settlement.OwnerClan != null ? cp.Settlement.OwnerClan.Leader : null;
                        if (holder != null && holder.StringId != _captorHeroId)
                        {
                            _captorHeroId = holder.StringId;
                            Log.Info("Loch " + cp.Settlement.Name + ": trzyma cie teraz " + holder.Name + " - ksiega odkupu przepisana.");
                        }
                    }
                }
                catch { }

                CaptivityExtras.DailyStarvation(_lowborn, _onParole);

                if (CaptivityExtras.TryBanditDump((int)PlayerCaptivity.CaptivityStartTime.ElapsedDaysUntilNow)) return;

                var newCaptor = CaptivityExtras.TrySellPrisoner();
                if (newCaptor != null) _captorHeroId = newCaptor.StringId;
                var cpNow = PlayerCaptivity.CaptorParty;   // E8/RC: pojmujacy zmienia sie w niewoli (sprzedaz, przeniesienie do lochu) - pamiec odbiorcy idzie za nim
                if (cpNow != null) NoteCaptorHome(cpNow);

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

                // Z LOCHEM SIE ROZMAWIA, NIE SZTURMUJE GO (Jeff 13.09). Party osady
                // nie ma LeaderHero (to nie MobileParty), wiec po przeniesieniu gracza
                // do miejskiego lochu odsiecz zawsze szla sciezka SILOWA i porownywala
                // garstke ludzi rodu z CALYM garnizonem miasta - warunek nie do spelnienia,
                // wiec kazda proba konczyla sie tylko przestojem. A przeciez miasto ma
                // wlasciciela, z ktorym da sie targowac o okup.
                var captorLord = captor.LeaderHero;
                if (captorLord == null && captor.IsSettlement && captor.Settlement != null)
                {
                    captorLord = captor.Settlement.OwnerClan != null ? captor.Settlement.OwnerClan.Leader : null;
                    if (captorLord != null)
                        Log.Info("Odsiecz: loch w " + captor.Settlement.Name + " - rozmawiamy z " + captorLord.Name + " zamiast szturmowac garnizon.");
                }
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
                    if (s.DebtToCaptorHome && (captor == null || !captor.IsAlive))
                    {
                        // E8/RC (regula decyzji 9): pojmujacy bez zywego bohatera - rata do jego rodu / osady / bandy; bez odbiorcy nic nie pobrane (dlug czeka)
                        int paid;
                        string to = GoldToCaptorHome(captor, pay, out paid);
                        if (to == null)
                        {
                            _e8DebtNoTaker++;
                            if (_e8DebtNoTaker == 1 || _e8DebtNoTaker % 30 == 0)
                                Log.Info("Dlug okupu (E8/RC): rata " + pay + " - brak odbiorcy (pojmujacy bez bohatera, osady i partii), nic nie pobrane, dlug " + _ransomDebt
                                         + " czeka; dob bez odbiorcy w tej sesji " + _e8DebtNoTaker);
                            if (_e8DebtNoTaker == 1) Log.Player("There is no one left to take the instalment on your ransom debt - the coin stays in your purse for now.");
                            _debtDaysUnpaid = 0;   // recenzja E8/RC: gracz mial czym zaplacic - licznik hanby zeruje sie jak przy kazdej racie (jak przed paczka); bez tego dni bez zlota sumowalyby sie przez lata do DebtGraceDays
                            return;
                        }
                        _e8DebtHomeN++; _e8DebtHomeGold += paid;
                        Log.Info("Dlug okupu (E8/RC): rata " + paid + " -> " + to + " (pojmujacy bez zywego bohatera); dlug po racie " + Math.Max(0, _ransomDebt - paid)
                                 + " | w tej sesji rat " + _e8DebtHomeN + ", razem " + _e8DebtHomeGold + ", dob bez odbiorcy " + _e8DebtNoTaker);
                        pay = paid;
                    }
                    else GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, captor, pay);
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

        /// <summary>Zdobywca przetrzasa juki: zabiera czesc ladunku i zlota.
        /// E5 decyzja 9 Jeffa (12.10, CaptorTakesAllFromPlayer): pojmujacy - KAZDY (banda, lord, milicja, zaloga, osada) - bierze CALE zloto gracza, do
        /// siebie, nie w nicosc: lord (bohater partii) - jego kiesa; milicja, zaloga i osada (bez bohatera) - kasa osady; inna partia bez bohatera (banda,
        /// karawana) - kiesa partii (PartyTradeGold; rozbicie bandy oddaje ja zwyciezcom - Armoury BeatenPurse 3.2). Ladunek (LootPartyInventoryPercent) -
        /// do jukow partii albo do zapasu osady; bez pojmujacego nic nie znika. Armoury A6 (BattlePlunder.LeaderCap) liczy zabrane zloto jako zaliczke -
        /// gracz nie placi drugi raz z puli bitwy. Wylaczone - jak dotad (procent, zloto bez lorda w nicosc).
        /// Jeff 12.10 (pytanie 3, CaptorTakesAllBaggage): przy CaptorTakesAllFromPlayer ladunek idzie CALY (100%, nie LootPartyInventoryPercent), a odbiorca
        /// jest ten sam co zlota (CaptorHome): osada bez bohatera (milicja, zaloga, loch) - zapas osady (Settlement.ItemRoster), lord i inna partia - juki partii.</summary>
        private void LootBaggage(PartyBase capturer)
        {
            try
            {
                var s = Settings.Current;
                bool all = s.CaptorTakesAllFromPlayer;
                var party = MobileParty.MainParty;
                int itemsTaken = 0;
                // E5 decyzja 9: ladunek do pojmujacego - partia w polu albo osada (zapas osady); bez pojmujacego nic nie zabieramy
                ItemRoster dest = capturer != null && capturer.ItemRoster != null && (capturer.IsMobile || (all && capturer.IsSettlement)) ? capturer.ItemRoster : null;
                // Jeff 12.10 (pytanie 3): cale juki, odbiorca jak zlota - milicja i zaloga bez bohatera oddaja ladunek do zapasu swojej osady (jak zloto do jej kasy)
                bool allBag = all && s.CaptorTakesAllBaggage;
                Settlement bagHome = allBag ? CaptorHome(capturer) : null;
                if (bagHome != null && bagHome.ItemRoster != null) dest = bagHome.ItemRoster;
                int pct = allBag ? 100 : s.LootPartyInventoryPercent;

                if (party != null && party.ItemRoster != null && pct > 0 && (!all || dest != null))
                {
                    var roster = party.ItemRoster;
                    for (int i = roster.Count - 1; i >= 0; i--)
                    {
                        var el = roster[i];
                        if (el.EquipmentElement.Item == null) continue;
                        int amount = pct >= 100 ? el.Amount : el.Amount * pct / 100;
                        if (amount <= 0) continue;
                        roster.AddToCounts(el.EquipmentElement, -amount);
                        if (dest != null)
                            dest.AddToCounts(el.EquipmentElement, amount);
                        itemsTaken += amount;
                    }
                }

                int goldTaken = 0;
                string to = "";
                if (all)
                {
                    int g = Hero.MainHero.Gold;
                    if (g > 0)
                    {
                        to = GoldToCaptor(capturer, g);
                        goldTaken = to != null ? g - Math.Max(0, Hero.MainHero.Gold) : 0;
                        if (to == null) to = "brak pojmujacego - nic nie zabrano";
                    }
                }
                else if (s.LootGoldPercent > 0)
                {
                    goldTaken = Hero.MainHero.Gold * s.LootGoldPercent / 100;
                    if (goldTaken > 0)
                    {
                        var captorHero = (capturer != null) ? capturer.LeaderHero : null;
                        GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, captorHero, goldTaken);
                    }
                }

                Log.Info("Przetrzasnieto juki: " + itemsTaken + " przedmiotow, " + goldTaken + " zlota."
                         + (all ? " E5 (decyzja 9, CaptorTakesAllFromPlayer): cale zloto -> " + to + "; ladunek " + pct + "%" + (allBag ? " (cale juki, CaptorTakesAllBaggage)" : "") + " -> "
                                + (dest != null ? (bagHome != null && dest == bagHome.ItemRoster ? "zapas osady " + bagHome.Name : (capturer.IsSettlement ? "zapas osady " : "juki partii ") + capturer.Name) : "nikt (nic nie zabrano)") : ""));
                if (all && goldTaken > 0)
                    Log.Player("They emptied your purse - every one of your " + goldTaken + " gold is theirs now" + (itemsTaken > 0 ? (allBag ? ", and your whole baggage train with it (" + itemsTaken + " items)." : ", and " + itemsTaken + " items from your baggage with it.") : "."), true);
                else if (itemsTaken > 0 || goldTaken > 0)
                    Log.Player("They went through your baggage: " + itemsTaken + " items and " + goldTaken + " gold are gone.", true);
            }
            catch (Exception e) { Log.Error("LootBaggage", e); }
        }

        /// <summary>E5 decyzja 9: cale zloto gracza do pojmujacego (przelew gry - GiveGoldAction, zdarzenie widza ksiegi Armoury). Zwraca opis odbiorcy do logu;
        /// null - nie ma komu dac (nic nie zabrane).</summary>
        private static string GoldToCaptor(PartyBase capturer, int gold)
        {
            if (capturer == null || gold <= 0) return null;
            var mp = capturer.IsMobile ? capturer.MobileParty : null;
            var lord = capturer.LeaderHero;
            if (lord != null)
            {
                GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, lord, gold);
                return "kiesa " + lord.Name;
            }
            Settlement home = CaptorHome(capturer);
            if (home != null && home.SettlementComponent != null)
            {
                GiveGoldAction.ApplyForCharacterToSettlement(Hero.MainHero, home, gold);
                return "kasa osady " + home.Name + (mp != null ? " (" + (mp.IsMilitia ? "milicja" : "zaloga") + ")" : "");
            }
            if (mp != null)
            {
                GiveGoldAction.ApplyForCharacterToParty(Hero.MainHero, capturer, gold);
                return "kiesa partii " + capturer.Name;
            }
            return null;
        }

        /// <summary>Osada pojmujacego bez bohatera: sama osada (loch) albo osada jej milicji / zalogi. Lord albo inna partia (banda, karawana) - null.
        /// Jedna regula dla zlota (GoldToCaptor) i ladunku (LootBaggage, Jeff 12.10).</summary>
        private static Settlement CaptorHome(PartyBase capturer)
        {
            if (capturer == null || capturer.LeaderHero != null) return null;
            if (capturer.IsSettlement) return capturer.Settlement;
            var mp = capturer.IsMobile ? capturer.MobileParty : null;
            if (mp != null && (mp.IsMilitia || mp.IsGarrison)) return mp.CurrentSettlement ?? mp.HomeSettlement;
            return null;
        }

        /// <summary>E8/RC: pamiec odbiorcy dla pojmujacego BEZ bohatera - osada (loch, milicja, zaloga; ta sama CaptorHome co przy pojmaniu) albo banda
        /// (partia w polu). Przy pojmaniu i co dobe w niewoli; pojmujacy z bohaterem - pamiec pusta (odbiorca to on, po jego smierci glowa rodu).</summary>
        private void NoteCaptorHome(PartyBase captor)
        {
            try
            {
                string home = "", band = "";
                if (captor != null && captor.LeaderHero == null)
                {
                    var st = CaptorHome(captor);
                    if (st != null) home = st.StringId;
                    else if (captor.IsMobile && captor.MobileParty != null) band = captor.MobileParty.StringId;
                }
                if (home == (_captorHomeId ?? "") && band == (_captorBandId ?? "")) return;
                _captorHomeId = home; _captorBandId = band;
                Log.Info("Pojmujacy (E8/RC): odbiorca splat, gdy bohatera brak - " + (home != "" ? "kasa osady " + home : band != "" ? "kiesa partii " + band : "nikt (pojmujacy ma bohatera)"));
            }
            catch (Exception e) { Log.Error("NoteCaptorHome", e); }
        }

        /// <summary>E8/RC (259, DebtToCaptorHome; regula decyzji 9 jak GoldToCaptor przy pojmaniu): zloto gracza dla pojmujacego, za ktorym nie stoi zywy bohater -
        /// zmarly lord: glowa jego rodu (regula gry - zloto zmarlego dziedziczy glowa rodu, KillCharacterAction); milicja, zaloga, loch: kasa tej osady;
        /// banda: kiesa partii, poki istnieje. Przelew gry (GiveGoldAction - zdarzenie widza ksiegi Armoury). Zwraca opis odbiorcy; null - nie ma komu dac
        /// (nic nie pobrane). paid = ile naprawde zeszlo z kiesy gracza.</summary>
        private string GoldToCaptorHome(Hero gone, int gold, out int paid)
        {
            paid = 0;
            if (gold <= 0) return null;
            int before = Hero.MainHero.Gold;
            string to = null;
            Hero heir = gone != null && gone.Clan != null ? gone.Clan.Leader : null;
            Settlement home = !string.IsNullOrEmpty(_captorHomeId) ? Settlement.Find(_captorHomeId) : null;
            if (heir != null && heir.IsAlive && heir != Hero.MainHero)
            {
                GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, heir, gold);
                to = "kiesa " + heir.Name + " (glowa rodu po " + gone.Name + ")";
            }
            else if (home != null && home.SettlementComponent != null)
            {
                GiveGoldAction.ApplyForCharacterToSettlement(Hero.MainHero, home, gold);
                to = "kasa osady " + home.Name;
            }
            else if (!string.IsNullOrEmpty(_captorBandId))
            {
                MobileParty band = null;
                foreach (var mp in MobileParty.All)
                    if (mp != null && mp.IsActive && mp.StringId == _captorBandId) { band = mp; break; }
                if (band != null && band.LeaderHero != null && band.LeaderHero != Hero.MainHero)   // banda dostala wodza - jego kiesa (jak lord w GoldToCaptor)
                {
                    GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, band.LeaderHero, gold);
                    to = "kiesa " + band.LeaderHero.Name + " (wodz partii " + band.Name + ")";
                }
                else if (band != null && band.LeaderHero == null)
                {
                    GiveGoldAction.ApplyForCharacterToParty(Hero.MainHero, band.Party, gold);
                    to = "kiesa partii " + band.Name;
                }
            }
            if (to != null) paid = Math.Max(0, before - Hero.MainHero.Gold);
            return to;
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
                // ODBICIE ODDAJE RYNSZTUNEK (Jeff 13.09: "skoro pokonali bandytow
                // i mnie uwolnili, moj sprzet powinien byc odzyskany"). Dotad gracz
                // byl jedynym, ktory wychodzil z niewoli nago: towarzysze dostawali
                // swoje wyzej (RestoreCompanionGear) za darmo i bezwarunkowo, a jego
                // rynsztunek szedl na targ najblizszego miasta (FenceGear) - 13.09
                // 11 sztuk za 43196 wyladowalo w The Eyrie. ReleasedAfterBattle to
                // detal, ktorego silnik uzywa, gdy oddzial zdobywcy zostal rozbity:
                // obozowisko pada razem z lupem, wiec zabieramy swoje z powrotem.
                // ...a takze gdy wyciagneli cie WLASNI ludzie: Rescue.TryFightRescue
                // konczy niewole przez ApplyByEscape, wiec detal jest "ucieczka"
                // mimo ze to byla regularna bitwa o twoja skore - stad flaga.
                bool freedByForce = detail == EndCaptivityDetail.ReleasedAfterBattle || _rescuedByForce;
                _rescuedByForce = false;
                if (HasStoredGear && freedByForce)
                {
                    RestoreGear();
                    Log.Player("Your captors are broken, and your war gear is back on your shoulders.", true);
                }
                else if (HasStoredGear && Settings.Current.FenceGearWhenNoLord && (captor == null || !captor.IsAlive))
                    FenceGear(party);
            }
            catch (Exception e) { Log.Error("OnHeroPrisonerReleased", e); }
        }

        /// <summary>Bandyci nie handluja - sprzedaja lup paserowi. Rynsztunek laduje na targu najblizszego miasta.</summary>
        private void FenceGear(PartyBase captorParty)
        {
            try
            {
                // E5 decyzja 9 (CaptorTakesAllFromPlayer): trzymala gracza osada bez bohatera (milicja, zaloga, loch) - rynsztunek to jej lup i zostaje w jej
                // zapasie (Settlement.ItemRoster); dotad GearToPacks nie widzial partii w polu i oddawal go graczowi ("porzucony oboz")
                if (Settings.Current.CaptorTakesAllFromPlayer && captorParty != null && captorParty.IsSettlement && captorParty.ItemRoster != null)
                {
                    int kept = 0;
                    foreach (var entry in _storedGear)
                    {
                        var parts = entry.Split('|');
                        if (parts.Length < 2) continue;
                        var item = MBObjectManager.Instance.GetObject<ItemObject>(parts[1]);
                        if (item == null) continue;
                        ItemModifier mod = null;
                        if (parts.Length > 2 && !string.IsNullOrEmpty(parts[2]))
                            mod = MBObjectManager.Instance.GetObject<ItemModifier>(parts[2]);
                        captorParty.ItemRoster.AddToCounts(new EquipmentElement(item, mod), 1);
                        kept++;
                    }
                    _storedGear.Clear();
                    _gearBuybackPrice = 0;
                    Log.Info("Rynsztunek (E5 decyzja 9): " + kept + " szt. zostaje w zapasie osady " + captorParty.Name + ", ktora trzymala gracza - jej lup, nie wraca.");
                    Log.Player("Your war gear stays behind in " + captorParty.Name + " - it belongs to them now.", true);
                    return;
                }
                if (Drobne39.PacksOn)   // 3.9: rynsztunek to lup zdobywcow - do ich jukow, dalej jak kazdy lup bandy (jedna regula)
                {
                    var back = new List<string>();
                    if (!Drobne39.GearToPacks(_storedGear, captorParty, back))
                    {
                        RestoreGear();   // zdobywcow juz nie ma - porzucony oboz, rynsztunek wraca (jak przy odbiciu sila)
                        Log.Player("Your captors are gone. You find your war gear in their abandoned camp.");
                        return;
                    }
                    _storedGear.Clear();
                    _gearBuybackPrice = 0;
                    if (back.Count > 0) { _storedGear.AddRange(back); RestoreGear(); }   // sztuki, ktore nie weszly do jukow, wracaja do gracza
                    return;
                }
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
                if (Settings.Current.DebtToCaptorHome && (captor == null || !captor.IsAlive))
                {
                    // E8/RC: ta sama regula co splata dlugu. Dialog wykupu wymaga rozmowy z pojmujacym (CanBuyBackFrom), wiec to zabezpieczenie - 0 trafien w grze
                    int paid;
                    string to = GoldToCaptorHome(captor, BuybackPrice, out paid);
                    if (to == null) { Log.Info("Wykup rynsztunku (E8/RC): brak odbiorcy zaplaty (pojmujacy bez bohatera, osady i partii) - nic nie pobrane, rynsztunek zostaje w ksiedze."); return false; }
                    Log.Info("Wykup rynsztunku (E8/RC): " + paid + " -> " + to + " (pojmujacy bez zywego bohatera)");
                }
                else GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, captor, BuybackPrice);
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
