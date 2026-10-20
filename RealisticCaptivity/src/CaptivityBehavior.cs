using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
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
        // n20-niewola (CaptorTakesArmsAndPurse): rynsztunek bojowy gracza oddany zdobywcy - TYLKO KSIEGA (sztuki leza w jukach zdobywcy / zapasie osady,
        // nie tu): co zabrano ("slot|item|mod") i dokad ("p:<StringId partii>" / "s:<StringId osady>"). Sluzy do odebrania przy odbiciu sila i do linii
        // logu przy wyjsciu; czyszczona przy kazdym wyjsciu z niewoli. Zapis: jeden napis rc_n20_warGear "1#dokad#wpis;wpis" (najwyzej 12 wpisow) przez
        // SaveText.Sync; stary zapis bez klucza - pusta ksiega (nic do odebrania).
        private List<string> _warGear = new List<string>();
        private string _warGearAt = "";
        private int _n20Taken, _n20Items, _n20Back, _n20Left;   // liczniki sesji (nie w save) do linii logu
        private long _n20Gold;
        // n20-niewola poprawka po recenzji (GearBackFromVictors): SLAD LUPU - ktore sztuki z ksiegi gra dala zwyciezcom z jukow zdobywcy w bitwie, ktora zaraz
        // uwolni gracza. MapEvent.CalculateAndCommitMapEventResults: LootDefeatedPartyItems (juki pokonanych losowo do zwyciezcow, szansa ok. 0.5 na sztuke,
        // zdarzenie OnLootDistributedToParty) idzie PRZED LootDefeatedPartyPrisoners (tam ApplyByReleasedAfterBattle gracza) - obie w jednym wywolaniu, partie
        // sa jeszcze w bitwie. Slad tylko na czas tego rozliczenia (nie w save): wazny, gdy przy wyjsciu partia zdobywcy jest w TEJ SAMEJ bitwie (_lootTrailEvent).
        private sealed class LootTrail { public PartyBase To; public EquipmentElement El; public int N; }
        private readonly List<LootTrail> _lootTrail = new List<LootTrail>();
        private MapEvent _lootTrailEvent;
        private int _n20FromVictors, _trailFail;   // liczniki sesji (nie w save)

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
                SyncWarGear(dataStore);   // n20-niewola
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
            CampaignEvents.OnLootDistributedToPartyEvent.AddNonSerializedListener(this, OnLootDistributed);   // n20-niewola: slad lupu (GearBackFromVictors)
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
                // n20-niewola: stan reguly na start sesji (widac w kazdym biegu, takze bez pojmania)
                Log.Info("Niewola gracza (n20): CaptorTakesArmsAndPurse=" + Settings.Current.CaptorTakesArmsAndPurse + " (C4 PlayerLosesLikeAiLord=" + Settings.Current.PlayerLosesLikeAiLord
                         + "; czynne: " + (Settings.Current.CaptorTakesArmsAndPurse && Settings.Current.PlayerLosesLikeAiLord) + "), GearBackFromVictors=" + Settings.Current.GearBackFromVictors
                         + ", unikaty przez Armoury UniqueSpoils: " + ArmouryBridge.UniqueKnown
                         + ", ksiega rynsztunku u zdobywcy z zapisu: " + (_warGear != null ? _warGear.Count : 0) + " szt." + (_warGear != null && _warGear.Count > 0 ? " (" + _warGearAt + ")" : ""));
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
                    // C4: pojmany bohater AI zostaje w swoim rynsztunku - towarzysz gracza tez (PlayerLosesLikeAiLord)
                    if (Settings.Current.StripCompanions && !Settings.Current.PlayerLosesLikeAiLord && prisoner != null && prisoner.Clan == Clan.PlayerClan)
                        StripCompanionGear(prisoner);
                    return;
                }
                _onParole = false;
                _paroleBroken = false;
                _captorHeroId = (capturer != null && capturer.LeaderHero != null) ? capturer.LeaderHero.StringId : "";
                Log.Info("Gracz pojmany przez: " + (_captorHeroId == "" ? "(bez lorda)" : _captorHeroId));
                NoteCaptorHome(capturer);   // E8/RC

                _lowborn = !HasNobleStanding();
                // C4 (Jeff 14.10): jak pokonany lord AI - rynsztunek zostaje na graczu (unikaty bierze zdobywca w Armoury UniqueSpoils, jak u AI)
                // n20-niewola (Jeff 19.10, CaptorTakesArmsAndPurse): przy C4 zdobywca bierze tez rynsztunek bojowy do swoich jukow i cale zloto przy sobie (LootBaggage)
                if (Settings.Current.PlayerLosesLikeAiLord && Settings.Current.CaptorTakesArmsAndPurse) TakeWarGear(capturer);
                else if (Settings.Current.PlayerLosesLikeAiLord) Log.Info("Pojmanie gracza (C4, jak lord AI): rynsztunek zostaje na graczu (unikaty - zwyczaj wojny Armoury), zloto - tylko lup bitwy (5%, najwyzej 10 000), tabor caly do zdobywcy, zbrojownia DTE - Armoury.");
                else if (Settings.Current.StripEquipment || Settings.Current.CaptorTakesAllFromPlayer) StripGear();   // E5 decyzja 9: caly rynsztunek
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

        // ---------------------------------------------------------------- n20-niewola: rynsztunek bojowy do zdobywcy
        /// <summary>n20-niewola (Jeff 19.10, CaptorTakesArmsAndPurse, przy C4): zdobywca zabiera rynsztunek bojowy gracza - 12 slotow zestawu bojowego (bron,
        /// choragiew, zbroja, kon, uprzaz) - DO SWOICH JUKOW, nie w limbo jak stary StripGear: lord i banda - juki partii (ItemRoster), osada (milicja, zaloga,
        /// loch) - jej zapas (Settlement.ItemRoster); ten sam odbiorca co tabor w LootBaggage (CaptorHome). Dalej to jego lup jak kazdy inny: AI sprzedaje lup
        /// w miescie (zaplata miasta), prawo band Armoury (OutlawLaw), a gdy jego druzyna przegra bitwe - gra rozdaje juki zwyciezcom losowo, sztuka po sztuce
        /// z szansa ok. 0.5 (MapEvent.LootDefeatedPartyItems, PRZED uwolnieniem jencow - LootDefeatedPartyPrisoners; milicja i zaloga nie lupia) - zwrot
        /// graczowi w WarGearOnRelease (z GearBackFromVictors takze od zwyciezcow). Stroj cywilny i zestaw ukrycia zostaja - ale widac je TYLKO w scenach osad:
        /// ikona na mapie, ekran postaci i bitwa uzywaja zestawu bojowego (CharacterObject.Equipment = HeroObject.BattleEquipment), wiec tam bohater jest
        /// w bieliznie i pieszo (bez lachmanow z niczego - zamknieta ekonomia; w zestawie cywilnym zostaje tez bron cywilna, jesli ja nosil, ale do bitwy
        /// gra jej nie bierze). Unikaty (spis ROT, stal valyrianska) - tylko zwyczaj wojny Armoury (UniqueSpoils.Take: bierze je
        /// zdobywca-bohater, sluchacz Armoury biegnie PRZED RC - zarejestrowany pozniej); RC ich nie rusza. Sztuka, ktora nie weszla do jukow, zostaje
        /// na graczu (nic nie znika). Bez pojmujacego z jukami - nic nie zabrane. Ksiega _warGear - tylko do odebrania przy odbiciu sila.</summary>
        private void TakeWarGear(PartyBase capturer)
        {
            try
            {
                _warGear.Clear(); _warGearAt = ""; _lootTrail.Clear(); _lootTrailEvent = null;
                var eq = Hero.MainHero != null ? Hero.MainHero.BattleEquipment : null;
                if (eq == null || (Campaign.Current != null && ReferenceEquals(eq, Campaign.Current.DeadBattleEquipment))) return;
                string at, where;
                var dest = PlunderDest(capturer, out at, out where);
                if (dest == null)
                {
                    Log.Info("Rynsztunek gracza (n20, CaptorTakesArmsAndPurse): pojmujacy bez jukow i zapasu (" + (capturer != null ? capturer.Name.ToString() : "brak partii") + ") - rynsztunek zostaje na graczu.");
                    return;
                }
                int taken = 0, horse = 0, weapons = 0, armour = 0, uniq = 0, fail = 0;
                long worth = 0;
                for (int i = 0; i < (int)EquipmentIndex.NumEquipmentSetSlots; i++)
                {
                    var el = eq[i];
                    if (el.IsEmpty || el.Item == null) continue;
                    if (ArmouryBridge.IsUnique(el.Item)) { uniq++; continue; }   // unikat - zwyczaj wojny Armoury, nie juki zdobywcy
                    try { dest.AddToCounts(el, 1); }
                    catch (Exception e) { if (fail++ == 0) Log.Error("TakeWarGear.put", e); continue; }   // nie weszla do jukow - zostaje na graczu
                    eq[i] = EquipmentElement.Invalid;
                    _warGear.Add(i + "|" + el.Item.StringId + "|" + (el.ItemModifier != null ? el.ItemModifier.StringId : ""));
                    taken++; worth += el.ItemValue;
                    if (i == (int)EquipmentIndex.Horse || i == (int)EquipmentIndex.HorseHarness) horse++;
                    else if (i <= (int)EquipmentIndex.ExtraWeaponSlot) weapons++;
                    else armour++;
                }
                if (taken > 0) _warGearAt = at;
                _n20Taken++; _n20Items += taken;
                int leftOn = 0;   // co zostalo w zestawie bojowym (unikat, ktorego nikt nie wzial; sztuka, ktora nie weszla do jukow)
                for (int i = 0; i < (int)EquipmentIndex.NumEquipmentSetSlots; i++) if (!eq[i].IsEmpty) leftOn++;
                Log.Info("Rynsztunek gracza (n20, CaptorTakesArmsAndPurse): " + taken + " szt. (wartosc " + worth + "; bron i choragiew " + weapons + ", zbroja " + armour
                         + ", kon i uprzaz " + horse + ") -> " + where + " | unikaty zostawione zwyczajowi wojny Armoury " + uniq + (ArmouryBridge.UniqueKnown ? "" : " (brak Armoury UniqueSpoils)")
                         + (fail > 0 ? " | nie weszly do jukow, zostaja na graczu " + fail : "") + " | stroj cywilny i zestaw ukrycia zostaja (widac je tylko w osadach; na mapie, w ekranie postaci i w bitwie zestaw bojowy - zostaje w nim " + leftOn
                         + " szt., pusty korpus to bielizna)"
                         + " | w tej sesji pojman " + _n20Taken + ", sztuk " + _n20Items + ".");
                if (taken > 0)
                    Log.Player("Your captors strip you of your war gear - " + taken + " pieces" + (horse > 0 ? ", your horse among them" : "") + " - and put it in their "
                               + (at.StartsWith("s:") ? "stores" : "packs") + (leftOn == 0
                                   ? ". You keep your town clothes, but on the road and in battle you have only your smallclothes" + (horse > 0 ? " and go on foot" : "") + " until you get new gear."
                                   : ". You keep your town clothes and the " + leftOn + " pieces they left you" + (horse > 0 ? ", but you go on foot" : "") + "."), true);
            }
            catch (Exception e) { Log.Error("TakeWarGear", e); }
        }

        /// <summary>n20-niewola: dokad idzie lup z gracza - ten sam odbiorca co tabor (LootBaggage): osada pojmujacego bez bohatera (loch, milicja, zaloga) -
        /// jej zapas; inna partia w polu (lord, banda, karawana) - jej juki. at: "s:&lt;osada&gt;" / "p:&lt;partia&gt;"; null - nikt.</summary>
        private static ItemRoster PlunderDest(PartyBase capturer, out string at, out string where)
        {
            at = ""; where = "nikt";
            if (capturer == null) return null;
            Settlement home = CaptorHome(capturer);
            if (home != null && home.ItemRoster != null) { at = "s:" + home.StringId; where = "zapas osady " + home.Name; return home.ItemRoster; }
            var mp = capturer.IsMobile ? capturer.MobileParty : null;
            if (mp != null && mp != MobileParty.MainParty && capturer.ItemRoster != null) { at = "p:" + mp.StringId; where = "juki partii " + capturer.Name; return capturer.ItemRoster; }
            return null;
        }

        /// <summary>n20-niewola, wyjscie z niewoli: rynsztunek zostaje u zdobywcy (okup, ucieczka, pokoj, wyrzucenie przez bandytow, dlug). ODBICIE SILA
        /// (ReleasedAfterBattle albo odsiecz rodu Rescue.TryFightRescue) - gracz odbiera swoje sztuka po sztuce (ten sam przedmiot i stan): najpierw z jukow /
        /// zapasu tych, ktorzy go trzymali; przy ReleasedAfterBattle i GearBackFromVictors - brakujace sztuki takze z jukow zwyciezcow, ktorym gra dala je z jukow
        /// zdobywcy w TEJ bitwie (slad _lootTrail, OnLootDistributed). Tak obie drogi odsieczy (wirtualna TryFightRescue i prawdziwa bitwa AI z AI) i kazdy
        /// zwyciezca (lord, druzyna rodu, milicja) daja ten sam wynik: wraca wszystko, co zdobywca jeszcze mial. Czego juz nie ma (sprzedane, zalozone przed
        /// bitwa; przy wylaczonym kluczu - lup zwyciezcow), tego nie ma - nic nie powstaje. Juki innej partii niz ta, ktora trzymala gracza w chwili wyjscia
        /// (np. lord odjechal, gracz w lochu) - bez odbioru. Odebrana sztuka wraca do swojego slotu, a gdy slot zajety - do taboru gracza. Ksiega i slad
        /// czyszczone zawsze.</summary>
        private void WarGearOnRelease(PartyBase heldBy, EndCaptivityDetail detail, bool byForce)
        {
            int n = _warGear.Count, back = 0, gone = 0, toBag = 0, fromVictors = 0;
            string at = _warGearAt ?? "";
            bool keyVictors = Settings.Current.GearBackFromVictors;
            try
            {
                string where = "";
                ItemRoster from = byForce ? HolderIfSame(heldBy, out where) : null;
                // slad lupu tylko z tej samej bitwy, w ktorej padla partia zdobywcy (partie sa jeszcze w bitwie przy LootDefeatedPartyPrisoners)
                bool useTrail = from != null && keyVictors && detail == EndCaptivityDetail.ReleasedAfterBattle && _lootTrail.Count > 0
                                && _lootTrailEvent != null && heldBy != null && ReferenceEquals(heldBy.MapEvent, _lootTrailEvent);
                var slots = new StringBuilder();
                var victors = new List<string>();
                if (from != null)
                {
                    var eq = Hero.MainHero.BattleEquipment;
                    foreach (var entry in _warGear)
                    {
                        int slot; EquipmentElement el;
                        if (!ParseGear(entry, out slot, out el)) { gone++; continue; }
                        ItemRoster src = null;
                        int idx = from.FindIndexOfElement(el);
                        if (idx >= 0 && from.GetElementNumber(idx) > 0) src = from;
                        else if (useTrail)
                        {
                            foreach (var t in _lootTrail)
                            {
                                if (t.N <= 0 || t.To == null || t.To.ItemRoster == null || !t.El.IsEqualTo(el)) continue;
                                int j = t.To.ItemRoster.FindIndexOfElement(el);
                                if (j < 0 || t.To.ItemRoster.GetElementNumber(j) <= 0) continue;
                                src = t.To.ItemRoster;
                                t.N--;
                                fromVictors++;
                                string nm = t.To.Name != null ? t.To.Name.ToString() : "?";
                                if (!victors.Contains(nm)) victors.Add(nm);
                                break;
                            }
                        }
                        if (src == null) { gone++; continue; }
                        src.AddToCounts(el, -1);
                        if (slot >= 0 && slot < (int)EquipmentIndex.NumEquipmentSetSlots && eq[slot].IsEmpty) eq[slot] = el;
                        else { MobileParty.MainParty.ItemRoster.AddToCounts(el, 1); toBag++; }
                        back++;
                        if (slots.Length > 0) slots.Append(',');
                        slots.Append(slot >= 0 && slot < (int)EquipmentIndex.NumEquipmentSetSlots ? ((EquipmentIndex)slot).ToString() : slot.ToString());
                    }
                }
                int left = n - back - gone;
                _n20Back += back; _n20Left += left + gone; _n20FromVictors += fromVictors;
                Log.Info("Rynsztunek gracza (n20) przy wyjsciu z niewoli (" + detail + (byForce ? ", odbicie sila" : "") + "): zabrane " + n + " szt. -> " + (at != "" ? at : "?")
                         + (byForce ? (from != null ? "; odebrane " + back + " (z " + where + " " + (back - fromVictors) + ", od zwyciezcow z tej bitwy " + fromVictors
                                                      + (victors.Count > 0 ? " [" + string.Join("; ", victors) + "]" : "") + (toBag > 0 ? "; do taboru, slot zajety " + toBag : "") + ")"
                                                      + (back > 0 ? ", sloty " + slots : "") + ", juz nie ma " + gone
                                                      + " | GearBackFromVictors=" + keyVictors + (detail == EndCaptivityDetail.ReleasedAfterBattle ? ", slad lupu " + (useTrail ? "uzyty" : "brak") : "")
                                                    : "; juki innej partii niz ta, ktora trzymala gracza (" + (heldBy != null ? heldBy.Name.ToString() : "brak") + ") - bez odbioru")
                                   : "; zostaje u zdobywcy " + n)
                         + " | w tej sesji pojman " + _n20Taken + ", sztuk " + _n20Items + ", zlota " + _n20Gold + ", odebranych przy odbiciu " + _n20Back
                         + " (od zwyciezcow " + _n20FromVictors + "), zostalo u zdobywcow " + _n20Left + ".");
                if (detail == EndCaptivityDetail.Death) return;
                // przy wylaczonym kluczu brakujace sztuki po bitwie to zwykle lup zwyciezcow; przy wlaczonym - sprzedane / zalozone przed bitwa
                string goneWhy = (!keyVictors && detail == EndCaptivityDetail.ReleasedAfterBattle) ? "plunder of the victors, sold or worn" : "sold or worn before you were freed";
                if (back > 0 && gone == 0)
                    Log.Player("Your captors are broken, and your war gear comes back to you - all " + back + " pieces"
                               + (fromVictors > 0 ? ", some of them handed back by the men who freed you." : "."), false);
                else if (back > 0)
                    Log.Player("Your captors are broken, and you take back " + back + " pieces of your war gear"
                               + (fromVictors > 0 ? ", some of them handed back by the men who freed you" : "") + ". The other " + gone + " are gone - " + goneWhy + ".", false);
                else if (from != null)
                    Log.Player("Your war gear is no longer with those who held you - " + goneWhy + ".", true);
                else
                    Log.Player("Your war gear stays with your captors. Win it back, or buy new.", true);
            }
            catch (Exception e) { Log.Error("WarGearOnRelease", e); }
            finally { _warGear.Clear(); _warGearAt = ""; _lootTrail.Clear(); _lootTrailEvent = null; }
        }

        /// <summary>n20-niewola: wpis ksiegi "slot|item|mod" -> slot i sztuka (ten sam przedmiot i stan). false - zly wpis albo przedmiotu nie ma w grze.</summary>
        private static bool ParseGear(string entry, out int slot, out EquipmentElement el)
        {
            slot = -1; el = EquipmentElement.Invalid;
            var p = (entry ?? "").Split('|');
            if (p.Length < 2 || !int.TryParse(p[0], out slot)) return false;
            ItemObject item = null; ItemModifier mod = null;
            try
            {
                item = MBObjectManager.Instance.GetObject<ItemObject>(p[1]);
                if (item != null && p.Length > 2 && !string.IsNullOrEmpty(p[2])) mod = MBObjectManager.Instance.GetObject<ItemModifier>(p[2]);
            }
            catch { item = null; }
            if (item == null) return false;
            el = new EquipmentElement(item, mod);
            return true;
        }

        /// <summary>n20-niewola (GearBackFromVictors), CampaignEvents.OnLootDistributedToPartyEvent: gra dala zwyciezcy lup z jukow pokonanej partii
        /// (MapEvent.LootDefeatedPartyItems; zdarzenie z innym zwyciezca niz gracz pada tylko tam). Gdy pokonana partia to ta, ktora trzyma gracza i do ktorej
        /// jukow poszedl jego rynsztunek - zapamietaj, ile sztuk z ksiegi dostal ten zwyciezca (najwyzej tyle, ile jest w ksiedze). Zdarzenie pada przy kazdej
        /// bitwie swiata - pierwszy warunek (pusta ksiega) bez alokacji; idzie na watku glownym (rozliczenie bitew), nie z PartiesThink.</summary>
        private void OnLootDistributed(PartyBase winner, PartyBase defeated, ItemRoster loot)
        {
            if (_warGear == null || _warGear.Count == 0 || winner == null || defeated == null || loot == null || loot.Count == 0) return;
            try
            {
                if (!Settings.Current.GearBackFromVictors || Hero.MainHero == null || !Hero.MainHero.IsPrisoner) return;
                var at = _warGearAt ?? "";
                if (at.Length < 3 || at[0] != 'p' || !defeated.IsMobile || defeated.MobileParty == null || defeated.MobileParty.StringId != at.Substring(2)) return;
                if (Hero.MainHero.PartyBelongedToAsPrisoner != defeated && PlayerCaptivity.CaptorParty != defeated) return;   // ksiega wskazuje te partie, ale gracza trzyma juz kto inny
                var me = defeated.MapEvent;
                if (me == null) return;
                if (!ReferenceEquals(_lootTrailEvent, me)) { _lootTrail.Clear(); _lootTrailEvent = me; }
                int noted = 0;
                var seen = new List<EquipmentElement>();
                foreach (var entry in _warGear)
                {
                    int slot; EquipmentElement el;
                    if (!ParseGear(entry, out slot, out el)) continue;
                    bool dup = false;
                    foreach (var x in seen) if (x.IsEqualTo(el)) { dup = true; break; }
                    if (dup) continue;
                    seen.Add(el);
                    int inLedger = 0;
                    foreach (var e2 in _warGear) { int s2; EquipmentElement el2; if (ParseGear(e2, out s2, out el2) && el2.IsEqualTo(el)) inLedger++; }
                    int idx = loot.FindIndexOfElement(el);
                    int got = idx >= 0 ? loot.GetElementNumber(idx) : 0;
                    int k = Math.Min(got, inLedger);
                    if (k <= 0) continue;
                    _lootTrail.Add(new LootTrail { To = winner, El = el, N = k });
                    noted += k;
                }
                if (noted > 0)
                    Log.Info("Slad lupu (n20 GearBackFromVictors): " + winner.Name + " wzial z jukow " + defeated.Name + " " + noted + " szt. rynsztunku gracza - wroca przy uwolnieniu w tej bitwie.");
            }
            catch (Exception e) { if (_trailFail++ < 3) Log.Error("OnLootDistributed", e); }
        }

        /// <summary>Juki / zapas z ksiegi, ale tylko gdy to ta sama partia albo osada, ktora trzymala gracza w chwili wyjscia. Inaczej null.</summary>
        private ItemRoster HolderIfSame(PartyBase heldBy, out string where)
        {
            where = "";
            var at = _warGearAt ?? "";
            if (heldBy == null || at.Length < 3) return null;
            string id = at.Substring(2);
            if (at[0] == 'p')
            {
                var mp = heldBy.IsMobile ? heldBy.MobileParty : null;
                if (mp != null && mp.StringId == id && mp.ItemRoster != null) { where = "juki partii " + heldBy.Name; return mp.ItemRoster; }
                return null;
            }
            if (at[0] == 's')
            {
                Settlement st = CaptorHome(heldBy) ?? (heldBy.IsSettlement ? heldBy.Settlement : null);
                if (st != null && st.StringId == id && st.ItemRoster != null) { where = "zapas osady " + st.Name; return st.ItemRoster; }
            }
            return null;
        }

        /// <summary>n20-niewola: ksiega rynsztunku w zapisie - jeden napis "1#dokad#wpis;wpis" (najwyzej 12 wpisow, ponizej 2 KB) przez SaveText.Sync.
        /// Stary zapis bez klucza - pusta ksiega. Wlasny try - wyjatek tu nie przerywa reszty SyncData.</summary>
        private void SyncWarGear(IDataStore ds)
        {
            try
            {
                string s = null;
                if (ds.IsSaving)
                {
                    var sb = new StringBuilder("1#");
                    sb.Append(_warGearAt ?? "").Append('#');
                    int k = 0;
                    if (_warGear != null)
                        foreach (var e in _warGear)
                        {
                            if (k >= 16) break;
                            if (k > 0) sb.Append(';');
                            sb.Append(e);
                            k++;
                        }
                    s = sb.ToString();
                }
                SaveText.Sync(ds, "rc_n20_warGear", ref s);
                if (ds.IsLoading)
                {
                    _warGear = new List<string>();
                    _warGearAt = "";
                    var p = (s ?? "").Split('#');
                    if (p.Length >= 3 && p[0] == "1")
                    {
                        _warGearAt = p[1];
                        foreach (var e in p[2].Split(';')) if (!string.IsNullOrEmpty(e)) _warGear.Add(e);
                    }
                }
            }
            catch (Exception e) { Log.Error("SyncWarGear", e); }
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
                // C4 (Jeff 14.10): jak pokonany lord AI - tabor caly (rozbita druzyna lorda traci go zwyciezcom: gra pol, reszta Armoury R14), zloto RC nie bierze
                // (lup bitwy: GoldLost 5% kiesy, najwyzej 10 000 - gra, Armoury A6 sciaga z kiesy wodza, takze gracza w niewoli)
                bool likeAi = s.PlayerLosesLikeAiLord;
                bool all = s.CaptorTakesAllFromPlayer || likeAi;
                var party = MobileParty.MainParty;
                int itemsTaken = 0;
                // E5 decyzja 9: ladunek do pojmujacego - partia w polu albo osada (zapas osady); bez pojmujacego nic nie zabieramy
                ItemRoster dest = capturer != null && capturer.ItemRoster != null && (capturer.IsMobile || (all && capturer.IsSettlement)) ? capturer.ItemRoster : null;
                // Jeff 12.10 (pytanie 3): cale juki, odbiorca jak zlota - milicja i zaloga bez bohatera oddaja ladunek do zapasu swojej osady (jak zloto do jej kasy)
                bool allBag = (all && s.CaptorTakesAllBaggage) || likeAi;
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
                // n20-niewola (CaptorTakesArmsAndPurse): przy C4 cale zloto przy sobie (Hero.Gold) do pojmujacego - ta sama droga co E5 (GoldToCaptor: kiesa lorda,
                // kasa osady milicji / zalogi / lochu, kiesa bandy). Pojmanie idzie w CaptureDefeatedPartyMembers PRZED rozliczeniem lupu A6 - Armoury liczy to,
                // co zeszlo z kiesy od migawki, jako zaliczke (LeaderCap), wiec gracz nie placi drugi raz, a udzialy zwyciezcow z puli maleja o to samo.
                bool armsPurse = likeAi && s.CaptorTakesArmsAndPurse;
                if (armsPurse)
                {
                    int g = Hero.MainHero.Gold;
                    if (g > 0)
                    {
                        to = GoldToCaptor(capturer, g);
                        goldTaken = to != null ? g - Math.Max(0, Hero.MainHero.Gold) : 0;
                        to = to == null ? "brak pojmujacego - nic nie zabrano" : to + " (n20 CaptorTakesArmsAndPurse: cale zloto przy sobie; A6 - zaliczka lupu bitwy)";
                    }
                    else to = "kiesa pusta (n20 CaptorTakesArmsAndPurse)";
                    _n20Gold += goldTaken;
                }
                else if (likeAi) to = "nikt - jak lord AI: zloto tylko przez lup bitwy (5%, najwyzej 10 000; A6)";
                else if (all)
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
                         + (all ? (likeAi ? " C4 (PlayerLosesLikeAiLord): zloto -> " : " E5 (decyzja 9, CaptorTakesAllFromPlayer): cale zloto -> ") + to + "; ladunek " + pct + "%" + (allBag ? (likeAi ? " (caly tabor, jak lord AI)" : " (cale juki, CaptorTakesAllBaggage)") : "") + " -> "
                                + (dest != null ? (bagHome != null && dest == bagHome.ItemRoster ? "zapas osady " + bagHome.Name : (capturer.IsSettlement ? "zapas osady " : "juki partii ") + capturer.Name) : "nikt (nic nie zabrano)") : ""));
                if (armsPurse)
                    Log.Player((goldTaken > 0 ? "They emptied your purse - every one of your " + goldTaken + " gold is theirs now" : "They found your purse empty")
                               + (itemsTaken > 0 ? ", and your whole baggage train with it (" + itemsTaken + " items)." : "."), true);
                else if (likeAi)
                    Log.Player((itemsTaken > 0 ? "Your captors take your whole baggage train (" + itemsTaken + " items), as from any beaten lord. " : "")
                               + "You keep your arms and your purse - your purse pays only the battle's toll, as any lord's does.", true);
                else if (all && goldTaken > 0)
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
                if (_warGear != null && _warGear.Count > 0) WarGearOnRelease(party, detail, freedByForce);   // n20-niewola
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
            try { _onParole = false; Log.Info("Niewola zakonczona. Rynsztunek w rekach zdobywcy: " + HasStoredGear + (_warGear != null && _warGear.Count > 0 ? " (ksiega odkupu StripGear); n20 - w jukach zdobywcy " + _warGear.Count + " szt." : "")); }
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
