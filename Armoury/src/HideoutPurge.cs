using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// PRZETRZEBIONA KRYJOWKA (Jeff 27.08): "jak pokonam kryjowke, to nie od
    /// razu nagroda - musze ja PRZESZUKAC, jak po bitwie. W kryjowce bandytow
    /// powinno byc zrabowane zloto z okolicznych napasci. I renown powinien
    /// pojsc, i reputacja w poblizu - im blizej, tym wiecej. To big deal jest."
    /// Po zwycieskiej bitwie w kryjowce otwiera sie menu przeszukania:
    ///  - "Search the hideout": zloto = baza + stawka x liczba rozbitych band
    ///    (kazda banda zyla z rozboju - tyle zdazyla zlupic okolicy), +-25%;
    ///    renown dla bohatera; wdziecznosc okolicy - notable wiosek, miast
    ///    i zamkow w okregu dostaja relacje od RepMax przy samej kryjowce
    ///    malejaco do zera na skraju okregu;
    ///  - "Leave without searching": big deal zostawiony szczurom.
    /// Vanillowego lupu przedmiotowego nie ruszamy - to nagroda Z WIERZCHU;
    /// nasze zloto i slawa leza GLEBIEJ i trzeba sie schylic.
    /// </summary>
    internal sealed class HideoutPurge : CampaignBehaviorBase
    {
        private static bool _pending;
        private static int _pendingGold;
        private static int _pendingBands;
        private static string _pendingName = "";
        private static CampaignVec2 _pendingPos;
        private static bool _pendingHasPos;   // bez osady nie ma okregu wdziecznosci
        private static Settlement _pendingHideout;   // kryjowka do przeszukania - jej kasa schodzi dopiero przy przeszukaniu
        // odwet: bandom odnawiamy rozkaz pogoni, bo AI co chwile przemysliwa
        private static readonly System.Collections.Generic.List<MobileParty> _reprisalPack =
            new System.Collections.Generic.List<MobileParty>();
        private static CampaignTime _reprisalUntil = CampaignTime.Zero;
        // 570 E: klan bandytow kryjowki (z pokonanych w bitwie) - odwet i poscig tylko tego klanu albo tej kryjowki; kryjowka, dla ktorej odwet juz
        // ruszyl (jeden odwet, jedno scalenie, jeden komunikat - dotad Reprisal biegl przy bitwie i drugi raz przy starcie przeszukania); bandy
        // z bohaterem i eskorty bohatera BK - nie scalane i bez naszych rozkazow (patrz Reprisal: wzajemna eskorta z BK SetFollow)
        private static Clan _pendingClan;
        private static Settlement _reprisalFor;
        private static DateTime _lastReprisalRefresh = DateTime.MinValue;   // tylko klucz HideoutPursuitHours 0 (stara regula 3 s realne)
        // E7/C (227, R4): odnowienie pogoni na zegarze gry - co HideoutPursuitHours h gry przy kazdej predkosci gry
        // (dotad co 3 s realne: przy x8 co ok. 7 h gry, przy x1 co ok. 0.9 h); licznik odnowien do linii konca pogoni
        private static CampaignTime _lastReprisalTick = CampaignTime.Zero;
        private static CampaignTime _pursuitStart = CampaignTime.Zero;
        private static int _pursuitRenewals;
        // lup przedmiotowy - ekran jak po bitwie, otwierany na czystej mapie
        private static ItemRoster _lootRoster;
        // recenzja 568 (1): lista pokazana na ekranie lupu - trzymana do zamkniecia ekranu (InventoryLogic zmienia ja w miejscu), reszta na polke
        // najblizszej przyjaznej warowni jak lup z pola (SpoilsRemnantsHome, wzor SiegeSpoils380.LootClosed); _lootOpenMissing - takty bez ekranu na stosie
        private static ItemRoster _lootOpen;
        private static int _lootOpenIn, _lootOpenMissing;
        // E8/S (246, rozpoznanie C poz. 11): potkniecia OnTick Z RZEDU przy czekajacym przeszukaniu. Dotad jeden wyjatek gasil _pending
        // (przeszukanie przepadalo); teraz _pending zostaje, kolejna proba najwczesniej po PendingRetrySeconds, najwyzej PendingStrikesMax
        // prob - dopiero wtedy jak dotad (_pending gasnie; kasa zostaje w kryjowce - schodzi dopiero w DoSearch). Udane otwarcie menu zeruje licznik
        private static int _pendingStrikes;
        private static DateTime _pendingRetryAt = DateTime.MinValue;
        private const int PendingStrikesMax = 3;
        private const double PendingRetrySeconds = 1.0;

        /// <summary>E8/S (246, PLAN 8.11): nowa kampania albo wczytanie (konstruktor ArmouryBehavior przez StateHygiene.ResetAll - PRZED SyncData,
        /// ktory tu nic nie zapisuje). Dotad pola zyly do konca procesu: po wczytaniu innego zapisu w tej samej sesji menu przeszukania wracalo
        /// z kryjowka POPRZEDNIEJ kampanii (obiekt osady, ktorej juz nie ma), a odwet odnawial rozkaz partiom starej gry. Tylko pola statyczne -
        /// sluchacze i menu (AddMenus na OnSessionLaunched) zostaja. _reprisalPack bez SetInitiative: partie poprzedniej kampanii juz nie istnieja.
        /// _lootRoster tez: bez tego po zgaszeniu _pending ekran lupow otwieralby sie w nowej kampanii ze sztukami ze starej (sztuki z niczego).
        /// Zwraca, ile rzeczy zostalo w pamieci (do linii startu "Porzadek stanu (E8/S)").</summary>
        internal static int Reset()
        {
            int had = (_pending ? 1 : 0) + _reprisalPack.Count + (_lootRoster != null && _lootRoster.Count > 0 ? 1 : 0) + (_searchLeave ? 1 : 0) + (_lootOpen != null ? 1 : 0);
            _pending = false; _pendingGold = 0; _pendingBands = 0; _pendingName = ""; _pendingPos = default(CampaignVec2); _pendingHasPos = false; _pendingHideout = null;
            _reprisalPack.Clear();
            _reprisalFor = null; _pendingClan = null; _reprisalUntil = CampaignTime.Zero;   // 570 E
            _searchLeave = false;
            _lootRoster = null;
            _lootOpen = null; _lootOpenIn = 0; _lootOpenMissing = 0;
            _pendingStrikes = 0; _pendingRetryAt = DateTime.MinValue;
            return had;
        }

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, _ => OneShotVendetta());
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, AddMenus);
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, _ => LogClock());   // E7/C: linia startowa
            CampaignEvents.OnHideoutBattleCompletedEvent.AddNonSerializedListener(this, OnHideoutBattle);
            CampaignEvents.TickEvent.AddNonSerializedListener(this, OnTick);
            // 570 D: poscig za skarbem - godzina (sam test flagi, gdy nic nie trwa), bitwy gracza (migawka zlota, wynik z banda poscigu)
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, HideoutVendetta.Hourly);
            CampaignEvents.MapEventStarted.AddNonSerializedListener(this, HideoutVendetta.OnMapEventStarted);
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, HideoutVendetta.OnMapEventEnded);
        }

        public override void SyncData(IDataStore dataStore) { }

        /// <summary>570 D: banda prowadzona przez odwet (poscig za skarbem jej nie rusza - jeden zarzadca bandy).</summary>
        internal static bool InReprisal(MobileParty mp) { return mp != null && _reprisalPack.Contains(mp); }

        /// <summary>Czy lup tej kryjowki czeka na przeszukanie przez gracza (odczyt dla OutlawLaw: takiej kasy nikt inny nie rusza).</summary>
        internal static bool Holds(Settlement hideout) { return _pending && hideout != null && ReferenceEquals(hideout, _pendingHideout); }

        private void OnHideoutBattle(BattleSideEnum winnerSide, HideoutEventComponent component,
                                     HideoutEventComponent.HideoutBattleEndState endState)
        {
            try
            {
                var c = Settings.Current;
                // nskarbiec: bitwa w kryjowce bez gracza (gra daje wtedy endState Defeated - PlayerSide None) - wlasna droga, przed wylacznikiem przeszukania
                var ev0 = component != null ? component.MapEvent : null;
                if (c != null && ev0 != null && !ev0.IsPlayerMapEvent) { AiCleared(ev0); return; }
                if (c == null || !c.HideoutPurgeEnabled) return;
                if (endState != HideoutEventComponent.HideoutBattleEndState.Victory) return;
                var mapEvent = component != null ? component.MapEvent : null;
                if (mapEvent == null || !mapEvent.IsPlayerMapEvent) return;
                if (mapEvent.PlayerSide != winnerSide) return;

                int bands = 1;
                try
                {
                    var side = mapEvent.GetMapEventSide(winnerSide == BattleSideEnum.Attacker
                        ? BattleSideEnum.Defender : BattleSideEnum.Attacker);
                    if (side != null && side.Parties != null && side.Parties.Count > 0) bands = side.Parties.Count;
                }
                catch { }

                var sett = mapEvent.MapEventSettlement;
                _pendingHasPos = sett != null;
                if (sett != null) _pendingPos = sett.GatePosition;
                _pendingName = sett != null && sett.Name != null ? sett.Name.ToString() : "the hideout";
                _pendingBands = bands;
                _pendingClan = BanditClanOf(mapEvent, winnerSide, sett);   // 570 E: klan kryjowki - do odwetu i poscigu za skarbem
                _reprisalFor = null;   // 570 E: nowa bitwa w kryjowce - odwet od nowa (drugie wywolanie - start przeszukania - juz nie)
                // AUDYT 04.10 (C6): lup to PRAWDZIWA kasa i magazyn kryjowki, nie zloto i zelastwo z niczego.
                // 568: poprawka komentarza - gra NIE odklada towaru do kryjowki (BanditSpawnCampaignBehavior.OnSettlementEntered dopisywala
                // 25% wartosci jukow jako ZLOTO z niczego, towaru nie zdejmowala; dosypke blokuje kurek OutlawLaw.HideoutGoldPostfix).
                // Kasa kryjowki = dola zlota band (OutlawLaw.StashDay), magazyn = dola towarowa band (HideoutGoods.Day, 568) - bez 568 zawsze pusty.
                // audyt ponowny W5: zloto zdejmowane dopiero przy PRZESZUKANIU (DoSearch) - odejscie bez przeszukania
                // zostawia je w kryjowce, zamiast kasowac
                _pendingHideout = sett;
                _pendingGold = 0;
                try { if (sett != null && sett.SettlementComponent != null) _pendingGold = Math.Max(0, sett.SettlementComponent.Gold); } catch { }
                _pendingStrikes = 0; _pendingRetryAt = DateTime.MinValue;   // E8/S (246): nowe przeszukanie - licznik potkniec od zera
                _pending = true;
                if (!HideoutGoods.On) BuildLoot(sett);   // 568: z dola towarowa magazyn schodzi dopiero przy PRZESZUKANIU (DoSearch) - jak zloto; klucz off - jak dotad
                Log.Info("HideoutPurge: zwyciestwo w " + _pendingName + " (" + (sett != null ? sett.StringId : "-") + "), band " + bands + ", lup " + _pendingGold + " zlota czeka na przeszukanie.");
                // odwet rusza JUZ TERAZ - przy duzej partii przeszukanie trwa
                // ledwie pare godzin gry i bandy musza wyjsc w droge od razu
                Reprisal();
            }
            catch (Exception e) { Log.Error("HideoutPurge.OnHideoutBattle", e); }
        }

        /// <summary>
        /// nskarbiec (noc 15/16.10): kryjowka oczyszczona BEZ gracza - jedna regula z przeszukaniem gracza: kase kryjowki bierze ten, kto ja oczyscil,
        /// bez 1/9 korony. Odbiorca jak w 3.2 (BeatenPurse): zywy wodz zwyciezcow (do jego kiesy), inaczej ich partia z kiesa handlowa (takze zaloga - gra daje
        /// jej kiese, GarrisonPartyComponent), inaczej kasa ich osady - milicja, patrol (BeatenPurse.SettlementOf: miasto, zamek, wies), inaczej najblizsze
        /// otwarte miasto (nieumarli monety nie biora). Oczyszczona = zwyciezca to nie banda i w kryjowce nie zostala zadna banda z ludzmi (gra niszczy
        /// pokonane bandy dopiero po tym zdarzeniu - pusta banda sie nie liczy). Odbiorca wybrany PRZED zdjeciem zlota: bez odbiorcy kasa zostaje w kryjowce.
        /// Kasy czekajacej na przeszukanie przez gracza nie rusza. Wylacznik HideoutHoardToAiVictor (off = jak dotad: kasa zostaje i kapie do miasta).
        /// </summary>
        private static void AiCleared(MapEvent ev)
        {
            try
            {
                var sett = ev != null ? ev.MapEventSettlement : null;
                if (sett == null || !sett.IsHideout) return;
                PartyBase lead = null;
                MobileParty lmp = null;
                bool won = false;
                if (ev.HasWinner)
                {
                    lead = ev.GetLeaderParty(ev.WinningSide);
                    lmp = lead != null && lead.IsMobile ? lead.MobileParty : null;
                    if (lmp != null && lmp.IsMainParty) return;          // straz: bitwa gracza (np. "wyslij wojsko") nigdy nie idzie droga AI - kase bierze przeszukanie
                    won = lmp != null && !OutlawLaw.IsOutlawParty(lmp);
                }
                bool cleared = won;
                if (cleared && sett.Parties != null)
                    foreach (var p in sett.Parties)
                        if (p != null && p.IsActive && OutlawLaw.IsOutlawParty(p) && p.MemberRoster != null && p.MemberRoster.TotalManCount > 0) { cleared = false; break; }
                OutlawLaw.NoteAiHideBattle(cleared);
                var c = Settings.Current;
                if (!cleared || c == null || !c.HideoutHoardToAiVictor || Holds(sett)) return;
                // odbiorca przed zdjeciem zlota
                Hero hero = lmp.LeaderHero;
                bool dead = Undead.Party(lmp);
                int who;
                Settlement town = null;
                if (hero != null && hero.IsAlive && hero != Hero.MainHero && !dead) who = OutlawLaw.AiToLord;
                else if (hero == null && lmp.IsPartyTradeActive && !dead) who = OutlawLaw.AiToParty;
                else
                {
                    // recenzja: jak 3.2 (BeatenPurse.OnPartyDestroyed) - zwyciezca bez kiesy (milicja, patrol) daje kase swojej osadzie, inaczej najblizsze miasto
                    town = BeatenPurse.SettlementOf(lead) ?? OutlawLaw.NearestTownOf(sett.GetPosition2D);
                    if (town == null || town.SettlementComponent == null) return;      // nie ma dokad - kasa zostaje w kryjowce (nic nie ginie)
                    who = OutlawLaw.AiToTown;
                }
                string name = sett.Name != null ? sett.Name.ToString() : "Hideout";
                string by = lmp.Name != null ? lmp.Name.ToString() : "?";
                int g = OutlawLaw.HoardTakeAll(sett, OutlawLaw.HoardOutAi);
                string to;
                if (g <= 0) to = "nic do wziecia";
                else if (who == OutlawLaw.AiToLord) { hero.ChangeHeroGold(g); to = "wodz " + hero.Name; }
                else if (who == OutlawLaw.AiToParty) { lmp.PartyTradeGold += g; to = "partia " + by; }
                else { town.SettlementComponent.ChangeGold(g); MoneyLedger.Note169(MoneyLedger.N169Other, town, g); to = (town.IsTown ? "miasto " : "osada ") + town.Name; }   // kasa osady poza GiveGoldAction (tylko licznik)
                OutlawLaw.NoteAiTo(who, g);
                Log.Info("Skarbce kryjowek (nskarbiec): kryjowka " + name + " (" + sett.StringId + ") oczyszczona bez gracza przez " + by
                         + " - skarbiec " + g + " zl -> " + to + ".");
                // 568: magazyn kryjowki (towar z doli band) do tego samego odbiorcy: wodz / partia - juki partii, osada - jej magazyn
                if (HideoutGoods.On)
                {
                    try
                    {
                        var dst = who == OutlawLaw.AiToTown ? town.ItemRoster : lmp.ItemRoster;
                        long gv;
                        int gu = HideoutGoods.TakeAll(sett, dst, out gv);
                        if (gu > 0)
                            Log.Info("Lup w kryjowkach (568): kryjowka " + name + " (" + sett.StringId + ") - magazyn " + gu + " szt. (wart " + gv + " zl) -> "
                                     + (who == OutlawLaw.AiToTown ? (town.IsTown ? "miasto " : "osada ") + town.Name : "juki " + by) + ".");
                    }
                    catch (Exception e) { HideoutGoods.Stumble("AiCleared", e); }
                }
            }
            catch (Exception e) { OutlawLaw.HoardStumble("HideoutPurge.AiCleared", e); }
        }

        /// <summary>
        /// Lup przedmiotowy kryjowki: caly jej magazyn (Settlement.ItemRoster) - od 568 towar, ktory bandy odniosly
        /// z rabunkow (HideoutGoods.Day). Przenoszony do _lootRoster, pokazywany ekranem lupow jak po bitwie.
        /// (568: stary opis "zelastwo tier 1-3 po kilka sztuk na bande" byl nieprawdziwy - magazyn byl zawsze pusty.)
        /// </summary>
        private static void BuildLoot(Settlement sett)
        {
            try
            {
                // recenzja 568 (2): lista juz z towarem (klucz HideoutGoodsStash przelaczony OFF -> ON miedzy bitwa a przeszukaniem albo
                // niepokazany lup poprzedniej kryjowki) - dopisujemy do niej, nie nadpisujemy (dotad towar zdjety przy bitwie przepadal)
                if (_lootRoster == null) _lootRoster = new ItemRoster();
                var src = sett != null ? sett.ItemRoster : null;
                if (src == null || src.Count == 0) { Log.Info("HideoutPurge: magazyn kryjowki pusty - lup tylko z band (vanilla)."); return; }
                for (int i = src.Count - 1; i >= 0; i--)
                {
                    var el = src.GetElementCopyAtIndex(i);
                    if (el.Amount <= 0 || el.EquipmentElement.Item == null) continue;
                    _lootRoster.AddToCounts(el.EquipmentElement, el.Amount);
                    src.AddToCounts(el.EquipmentElement, -el.Amount);
                }
                Log.Info("HideoutPurge: lup przedmiotowy z magazynu kryjowki (" + _lootRoster.Count + " pozycji).");
            }
            catch (Exception e) { Log.Error("HideoutPurge.BuildLoot", e); if (!HideoutGoods.On) _lootRoster = null; }   // 568 rec.: z kluczem lista zostaje (zdjete juz sztuki nie przepadaja)
        }

        private void OnTick(float dt)
        {
            try
            {
                if (Campaign.Current == null) return;
                // rozkaz pogoni odnawiany co HideoutPursuitHours h gry (E7/C), dopoki odwet trwa
                if (_reprisalPack.Count > 0 && PursuitDue())
                {
                    _pursuitRenewals++;
                    DriveReprisal();
                }

                var st = Game.Current != null && Game.Current.GameStateManager != null
                    ? Game.Current.GameStateManager.ActiveState as TaleWorlds.CampaignSystem.GameState.MapState : null;
                bool cleanMap = st != null && !st.AtMenu
                    && PlayerEncounter.Current == null && MobileParty.MainParty.MapEvent == null;

                // recenzja 568 (1): ekran lupu kryjowki zamkniety (2 takty mapy bez InventoryState na stosie) - reszta listy na polke warowni
                if (_lootOpen != null)
                {
                    if (LootScreenOnStack()) _lootOpenMissing = 0;
                    else if (++_lootOpenMissing >= 2) LootClosed();
                    return;   // nowy ekran lupu / menu przeszukania dopiero po rozliczeniu poprzedniego
                }

                // ekran lupow jak po bitwie - po przeszukaniu, na czystej mapie
                if (!_pending && _lootRoster != null && _lootRoster.Count > 0 && cleanMap)
                {
                    var roster = _lootRoster; _lootRoster = null;
                    var dict = new System.Collections.Generic.Dictionary<PartyBase, ItemRoster>
                    { { PartyBase.MainParty, roster } };
                    if (!HideoutGoods.On) { Helpers.InventoryScreenHelper.OpenScreenAsLoot(dict); return; }   // klucz off - jak dotad
                    int pcs = Pieces(roster);
                    try
                    {
                        _lootOpen = roster; _lootOpenIn = pcs; _lootOpenMissing = 0;
                        Helpers.InventoryScreenHelper.OpenScreenAsLoot(dict);
                        Log.Info("Lup w kryjowkach (568): ekran lupu kryjowki - " + pcs + " szt. na liscie.");
                    }
                    catch (Exception e)
                    {
                        // recenzja 568 (1): ekran sie nie otworzyl - cala lista do sakw (dotad lista byla juz wyzerowana i lup przepadal)
                        _lootOpen = null;
                        HideoutGoods.Stumble("OpenLoot", e);
                        try
                        {
                            var bag = MobileParty.MainParty.ItemRoster;
                            for (int i = 0; i < roster.Count; i++) { var el = roster.GetElementCopyAtIndex(i); if (el.Amount > 0 && el.EquipmentElement.Item != null) bag.AddToCounts(el.EquipmentElement, el.Amount); }
                            roster.Clear();
                            Log.Info("Lup w kryjowkach (568): ekran lupu kryjowki sie nie otworzyl - " + pcs + " szt. do sakw gracza.");
                        }
                        catch (Exception e2) { HideoutGoods.Stumble("OpenLoot.bag", e2); }
                    }
                    return;
                }

                if (!_pending || !cleanMap) return;
                // E8/S (246): po potknieciu kolejna proba otwarcia menu dopiero po PendingRetrySeconds (bez tego 3 proby = 3 klatki)
                if (_pendingStrikes > 0 && DateTime.UtcNow < _pendingRetryAt) return;
                // menu dopiero na CZYSTEJ mapie - nigdy w srodku cudzego menu
                // ani rozliczania bitwy (lekcja z klawisza O w NightRest).
                // _pending NIE gasnie tutaj: gdy odwet przerwie przeszukanie
                // walka konczy sie na mapie i menu wraca samo - do lupu wraca
                // sie po bitwie (Jeff). Gasi je dopiero DoSearch albo Leave.
                GameMenu.ActivateGameMenu("arm_hideout_search");
                _pendingStrikes = 0;   // E8/S (246): menu otwarte - licznik potkniec z rzedu od zera
            }
            catch (Exception e)
            {
                Log.Error("HideoutPurge.OnTick", e);
                // E8/S (246; CLAUDE.md 7 "licz potkniecia, nie gas funkcji"): czekajace przeszukanie nie przepada po jednej wywrotce - _pending
                // zostaje do PendingStrikesMax potkniec z rzedu (kolejna proba po PendingRetrySeconds). Potem jak dotad: _pending gasnie, kasa zostaje
                // w kryjowce (schodzi dopiero w DoSearch), zelastwo z _lootRoster pokaze ekran lupow. Klucz StateHygieneEnabled off = od razu jak dotad
                if (_pending && StateHygiene.On)
                {
                    _pendingStrikes++;
                    if (_pendingStrikes < PendingStrikesMax)
                    {
                        _pendingRetryAt = DateTime.UtcNow.AddSeconds(PendingRetrySeconds);
                        StateHygiene.NoteHideout(false);
                        return;
                    }
                    StateHygiene.NoteHideout(true);
                    Log.Info("HideoutPurge (E8/S): " + _pendingStrikes + " potkniecia z rzedu przy otwieraniu przeszukania " + _pendingName + " - przeszukanie porzucone (kasa zostaje w kryjowce).");
                }
                _pending = false;
            }
        }

        private static int Pieces(ItemRoster r)
        {
            int n = 0;
            if (r == null) return 0;
            for (int i = 0; i < r.Count; i++) { int a = r.GetElementCopyAtIndex(i).Amount; if (a > 0) n += a; }
            return n;
        }

        private static bool LootScreenOnStack()
        {
            try
            {
                var gsm = Game.Current != null ? Game.Current.GameStateManager : null;
                if (gsm == null) return false;
                foreach (var g in gsm.GameStates) if (g is TaleWorlds.CampaignSystem.GameState.InventoryState) return true;
            }
            catch { }
            return false;
        }

        /// <summary>Recenzja 568 (1): ekran lupu kryjowki zdjety ze stosu - InventoryLogic zmienia liste w miejscu (Done; Cancel = stan z otwarcia).
        /// To, co gracz zostawil, idzie bez zaplaty na polke najblizszej przyjaznej warowni (HorseFate.AbandonedShelf, klucz SpoilsRemnantsHome) - jak trofea
        /// z pola (SpoilsSeal.RemnantPostfix) i okno lupu obrony (SiegeSpoils380.LootClosed). Klucz off albo brak warowni - przepada jak w grze.</summary>
        private static void LootClosed()
        {
            var r = _lootOpen;
            _lootOpen = null; _lootOpenMissing = 0;
            int left = Pieces(r), moved = 0, kept = 0;
            string where = "-";
            try
            {
                var s = Settings.Current;
                if (left > 0 && s != null && s.SpoilsRemnantsHome)
                {
                    var st = HorseFate.AbandonedShelf(MobileParty.MainParty);
                    if (st != null && st.ItemRoster != null)
                    {
                        where = st.Name != null ? st.Name.ToString() : st.StringId;
                        for (int i = r.Count - 1; i >= 0; i--)
                        {
                            var el = r.GetElementCopyAtIndex(i);
                            var it = el.EquipmentElement.Item;
                            if (el.Amount <= 0 || it == null) continue;
                            if (it.NotMerchandise || it.IsBannerItem || el.EquipmentElement.IsQuestItem || UniqueSpoils.Is(it)) { kept += el.Amount; continue; }
                            st.ItemRoster.AddToCounts(el.EquipmentElement, el.Amount);
                            r.AddToCounts(el.EquipmentElement, -el.Amount);
                            moved += el.Amount;
                        }
                        if (moved > 0) Log.Player("What you left of the hideout plunder - " + moved + " pieces - is carted off to the market of " + where + ".");
                    }
                }
                if (r != null) r.Clear();
            }
            catch (Exception e) { HideoutGoods.Stumble("LootClosed", e); }
            Log.Info("Lup w kryjowkach (568): ekran lupu kryjowki zamkniety - na liscie " + _lootOpenIn + " szt., zostawione " + left
                     + (moved > 0 ? " -> na polke " + where + " " + moved : "")
                     + (left - moved > 0 ? ", przepadlo (jak w grze) " + (left - moved) + (kept > 0 ? " (w tym unikaty / nie-towar " + kept + ")" : "") : "") + ".");
            _lootOpenIn = 0;
        }

        /// <summary>E7/C (227): czy pora odnowic rozkaz pogoni. Zegar gry - co HideoutPursuitHours h gry
        /// (ujemna roznica = wczytany wczesniejszy zapis: odnow od razu); klucz 0 = stara regula co 3 s realne.
        /// TickEvent gry biegnie tylko przy plynacym czasie (Campaign.Tick: _dt > 0), wiec pauza nic nie odnawia.</summary>
        private static bool PursuitDue()
        {
            var c = Settings.Current;
            float step = c != null ? c.HideoutPursuitHours : 0.5f;
            if (step <= 0f)
            {
                if ((DateTime.Now - _lastReprisalRefresh).TotalSeconds <= 3.0) return false;
                _lastReprisalRefresh = DateTime.Now;
                return true;
            }
            var now = CampaignTime.Now;
            double dh = (now - _lastReprisalTick).ToHours;
            if (dh >= 0 && dh < step) return false;
            _lastReprisalTick = now;
            return true;
        }

        /// <summary>E7/C: start zegara pogoni (nowa horda) - pierwsze odnowienie po HideoutPursuitHours h gry od rozkazu startowego.</summary>
        private static void StartPursuitClock()
        {
            _lastReprisalTick = CampaignTime.Now;
            _pursuitStart = CampaignTime.Now;
            _pursuitRenewals = 0;   // _lastReprisalRefresh (klucz 0) nietkniety - stara regula dokladnie jak dotad
        }

        /// <summary>E7/C: dopisek linii konca pogoni - odnowien i odnowien na dobe gry (miara T3 bloku etap7_c).</summary>
        private static string PursuitTally()
        {
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            double h = (CampaignTime.Now - _pursuitStart).ToHours;
            var c = Settings.Current;
            float step = c != null ? c.HideoutPursuitHours : 0.5f;
            return " (E7/C: odnowien " + _pursuitRenewals + " w " + h.ToString("0.0", ci) + " h gry = "
                   + (h > 0.01 ? (_pursuitRenewals * 24.0 / h).ToString("0.0", ci) : "-") + " na dobe gry; "
                   + (step > 0f ? "co " + step.ToString("0.##", ci) + " h gry" : "co 3 s realne - stara regula") + ")";
        }

        /// <summary>E7/C: linia startowa - czym mierzony jest odwet kryjowek.</summary>
        private static void LogClock()
        {
            try
            {
                var c = Settings.Current;
                float step = c != null ? c.HideoutPursuitHours : 0.5f;
                Log.Info(step > 0f
                    ? "Jedna skala czasu (E7/C): odwet kryjowek co " + step.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + " h gry (HideoutPursuitHours)."
                    : "Jedna skala czasu (E7/C): odwet kryjowek co 3 s realne - stara regula (HideoutPursuitHours 0).");
            }
            catch (Exception e) { Log.Error("HideoutPurge.LogClock", e); }
            try { Log.Info(OutlawLaw.HoardStartLine()); } catch (Exception e) { Log.Error("HideoutPurge.HoardStart", e); }   // nskarbiec: linia startu sesji
        }

        /// <summary>Tlo menu: grafika kultury gracza, zapasowo ogolna - zadnych czerwonych TEMP.</summary>
        private static void SetSceneBackground(MenuCallbackArgs args)
        {
            try
            {
                string mesh = null;
                try
                {
                    var f = Hero.MainHero != null ? Hero.MainHero.MapFaction : null;
                    if (f != null && f.Culture != null) mesh = f.Culture.EncounterBackgroundMesh;
                }
                catch { }
                if (string.IsNullOrEmpty(mesh)) mesh = "wait_fallback";
                args.MenuContext.SetBackgroundMeshName(mesh);
            }
            catch { try { args.MenuContext.SetBackgroundMeshName("wait_fallback"); } catch { } }
        }

        private static CampaignTime _searchDone = CampaignTime.Zero;
        private static float _searchTotal = 2f;

        /// <summary>
        /// JEDNORAZOWA VENDETTA (narzedzie naprawcze). Jesli w folderze modulu
        /// lezy plik "vendetta.now", przy wczytaniu save'a bandy w promieniu 30
        /// od gracza scalaja sie w jedna horde i dostaja rozkaz pogoni - bez
        /// progu 3:1. Plik jest kasowany po wykonaniu: odpala sie RAZ.
        /// Uzyte 27.08: odwet z kryjowki Jeffa przepadl na starym DLL-u.
        /// </summary>
        private static void OneShotVendetta()
        {
            try
            {
                string marker = System.IO.Path.Combine(
                    TaleWorlds.Engine.Utilities.GetBasePath() ?? "", "Modules", "Armoury", "vendetta.now");
                if (!System.IO.File.Exists(marker)) return;
                try { System.IO.File.Delete(marker); } catch { }

                var me = MobileParty.MainParty;
                if (me == null) return;
                // tylko okolica kryjowki (gracz stoi przy niej) - ten sam promien
                // co odwet, ZADNEGO zbierania band z calej mapy
                float vr = Math.Max(1f, Settings.Current != null ? Settings.Current.HideoutReprisalRadius : 20f);
                var pack = new System.Collections.Generic.List<MobileParty>();
                foreach (var mp in MobileParty.All)
                {
                    if (mp == null || !mp.IsBandit || !mp.IsActive) continue;
                    if (mp.CurrentSettlement != null || mp.MapEvent != null) continue;
                    if (mp.GetPosition2D.Distance(me.GetPosition2D) > vr) continue;
                    pack.Add(mp);
                }
                if (pack.Count == 0) { Log.Info("Vendetta: brak band w okolicy - nic do scalenia."); return; }

                bool kin = Settings.Current != null && Settings.Current.HideoutReprisalKinOnly;   // 570 E: jak odwet - boss i scalane tylko Mergeable, jednego klanu
                MobileParty boss = null;
                foreach (var mp in pack)
                    if ((!kin || Mergeable(mp)) && (boss == null || mp.MemberRoster.TotalManCount > boss.MemberRoster.TotalManCount)) boss = mp;
                if (boss == null) { Log.Info("Vendetta: w okolicy same bandy z bohaterem albo eskorty bohatera - nic do scalenia (570)."); return; }
                int merged = 0;
                foreach (var mp in pack)
                {
                    if (mp == boss) continue;
                    if (kin)
                    {
                        if (!Mergeable(mp) || mp.ActualClan != boss.ActualClan) continue;
                        try { MergeInto(boss, mp); merged++; } catch (Exception e) { Log.Error("HideoutPurge.OneShotVendetta.Merge", e); }
                        continue;
                    }
                    try
                    {
                        var mr = mp.MemberRoster;
                        for (int i = 0; i < mr.Count; i++)
                        {
                            var el = mr.GetElementCopyAtIndex(i);
                            if (el.Character != null && el.Number > 0)
                                boss.MemberRoster.AddToCounts(el.Character, el.Number, false, el.WoundedNumber);
                        }
                        var ir = mp.ItemRoster;
                        for (int i = 0; i < ir.Count; i++)
                        {
                            var el = ir.GetElementCopyAtIndex(i);
                            if (el.Amount > 0) boss.ItemRoster.AddToCounts(el.EquipmentElement, el.Amount);
                        }
                        try { boss.PartyTradeGold += mp.PartyTradeGold; mp.PartyTradeGold = 0; } catch { }   // zloto scalanej bandy do bossa (audyt C6)
                        mp.MemberRoster.Clear();
                        DestroyPartyAction.Apply(null, mp);
                        merged++;
                    }
                    catch { }
                }
                _reprisalPack.Clear();
                _reprisalPack.Add(boss);
                float hrs = Math.Max(1f, Settings.Current != null ? Settings.Current.HideoutReprisalHours : 48f);
                _reprisalUntil = CampaignTime.HoursFromNow(hrs);
                StartPursuitClock();
                DriveReprisal();
                Log.Player("The bands you wronged have found each other - one warband now, and it is coming for you.", true);
                Log.Info("Vendetta: scalono " + (merged + 1) + " band w horde " + boss.MemberRoster.TotalManCount + " ludzi - pogon " + hrs + " h.");
            }
            catch (Exception e) { Log.Error("HideoutPurge.OneShotVendetta", e); }
        }

        private void AddMenus(CampaignGameStarter starter)
        {
            try
            {
                starter.AddGameMenu("arm_hideout_search",
                    "{=!}The hideout lies quiet. Smoke drifts from the fires the bandits will not need again. Their den has not given up everything yet.",
                    delegate (MenuCallbackArgs a) { SetSceneBackground(a); });
                starter.AddGameMenuOption("arm_hideout_search", "arm_hideout_do_search",
                    "{=!}Search the hideout",
                    delegate (MenuCallbackArgs a) { a.optionLeaveType = GameMenuOption.LeaveType.Continue; return true; },
                    delegate (MenuCallbackArgs a) { GameMenu.SwitchToMenu("arm_hideout_search_wait"); }, false, 0);
                starter.AddGameMenuOption("arm_hideout_search", "arm_hideout_leave",
                    "{=!}Leave without searching",
                    delegate (MenuCallbackArgs a) { a.optionLeaveType = GameMenuOption.LeaveType.Leave; return true; },
                    delegate (MenuCallbackArgs a) { _pending = false; GameMenu.ExitToLast(); }, true, 1);

                // PASEK PRZESZUKANIA jak po bitwie (Jeff 27.08: "nie od razu
                // rzeczy - pasek ile to zajmie, wykorzystaj ten mechanizm").
                // Ten sam AddWaitGameMenu co sen w obozie; nagrody dopiero
                // po dojechaniu paska do konca.
                starter.AddWaitGameMenu("arm_hideout_search_wait",
                    "{=!}{ARM_SEARCH_TEXT}",   // m110-sam: tekst w SearchInit wedlug skladu druzyny
                    SearchInit, delegate (MenuCallbackArgs a) { return true; }, null, SearchTick,
                    GameMenu.MenuAndOptionType.WaitMenuShowProgressAndHoursOption);
                starter.AddGameMenuOption("arm_hideout_search_wait", "arm_hideout_search_stop",
                    "{=!}Call the search off",
                    delegate (MenuCallbackArgs a) { a.optionLeaveType = GameMenuOption.LeaveType.Leave; return true; },
                    // flaga -> SearchTick przelaczy z powrotem na arm_hideout_search
                    // (SwitchToMenu z ticka to wzorzec vanilla; z opcji wait-menu
                    // ZAKAZ - CTD, a ExitToLast niszczy MenuContext). Zegar
                    // ruszamy sami - ratunek z deadlocka po load w srodku menu
                    delegate (MenuCallbackArgs a)
                    {
                        _searchLeave = true;
                        try { Campaign.Current.TimeControlMode = CampaignTimeControlMode.StoppablePlay; a.MenuContext.GameMenu.StartWait(); } catch { }
                    }, true, 9);
            }
            catch (Exception e) { Log.Error("HideoutPurge.AddMenus", e); }
        }

        private static bool _searchLeave;   // opcja "Call the search off" podnosi, SearchTick przelacza

        private static void SearchInit(MenuCallbackArgs args)
        {
            try
            {
                _searchLeave = false;
                // m110-sam: "The men" / "You" / "You and your companions" - ustawione najpierw, zeby blad nizej nie zostawil pustego paska
                MBTextManager.SetTextVariable("ARM_SEARCH_TEXT", PartyWords.MenCap() + " turn the hideout over - bedrolls, false floors, the cold ashes of the fire pits.");
                SetSceneBackground(args);
                var c = Settings.Current;
                // czas od liczby rak (Jeff): sam grzebiesz caly dzien, kazdy
                // czlowiek zdejmuje pol godziny, ale ponizej minimum nie zejdzie
                int men = 1;
                try { men = Math.Max(1, MobileParty.MainParty.MemberRoster.TotalManCount); } catch { }
                float solo = Math.Max(2f, c != null ? c.HideoutSearchSoloHours : 24f);
                float perMan = Math.Max(0f, c != null ? c.HideoutSearchPerManHours : 0.5f);
                float min = Math.Max(0.5f, c != null ? c.HideoutSearchMinHours : 2f);
                _searchTotal = MathF.Max(min, solo - perMan * (men - 1));
                _searchDone = CampaignTime.HoursFromNow(_searchTotal);
                args.MenuContext.GameMenu.StartWait();
                Log.Info("HideoutPurge: przeszukanie " + _searchTotal.ToString("0.#") + " h (" + men + " ludzi).");

                // ODWET: pladrujesz ich skarbiec - okoliczne bandy ida odbic
                // kryjowke. Przy przewadze gracza >= 3:1 podchodza i uciekaja
                // (tylko meldunek), ponizej - SetMoveEngageParty na gracza
                // i mechanizm spotkan gry robi reszte (przerwie przeszukanie).
                Reprisal();
            }
            catch (Exception e) { Log.Error("HideoutPurge.SearchInit", e); }
        }

        private static void Reprisal()
        {
            try
            {
                var c = Settings.Current;
                if (c == null || !c.HideoutReprisalEnabled || !_pendingHasPos) return;
                bool kin = c.HideoutReprisalKinOnly;   // 570 E: klucz off = jak dotad (kazda banda w promieniu, scalenie wszystkich)
                // 570 E: odwet tej kryjowki juz ruszyl (albo stchorzyl) - drugie wywolanie (start przeszukania) bez drugiego scalenia i komunikatu
                if (kin && _reprisalFor != null && ReferenceEquals(_reprisalFor, _pendingHideout))
                {
                    Log.Info("HideoutPurge: odwet (570) dla " + _pendingName + " juz rozstrzygniety - bez drugiego scalenia i komunikatu.");
                    return;
                }
                float radius = Math.Max(1f, c.HideoutReprisalRadius);
                float fleeOdds = Math.Max(1f, c.HideoutReprisalFleeOdds);
                float mine = 0f;
                try { mine = MobileParty.MainParty.Party.EstimatedStrength; } catch { }

                var pack = new System.Collections.Generic.List<MobileParty>();
                float theirs = 0f;
                int foreign = 0;
                foreach (var mp in MobileParty.All)
                {
                    if (mp == null || !mp.IsBandit || !mp.IsActive) continue;
                    if (mp.CurrentSettlement != null) continue;          // siedza w innej kryjowce
                    if (mp.MapEvent != null) continue;
                    if (mp.GetPosition2D.Distance(_pendingPos.ToVec2()) > radius) continue;
                    if (kin)
                    {
                        // 570 E: tylko bandy klanu kryjowki albo z ta kryjowka jako domem; bez band w zadaniu i bez nieumarlych
                        if (!Kin(mp, _pendingClan, _pendingHideout)) { foreign++; continue; }
                        if (mp.IsCurrentlyUsedByAQuest || Undead.Party(mp)) continue;
                        if (mp.MemberRoster == null || mp.MemberRoster.TotalManCount <= 0) continue;
                        if (HideoutVendetta.Hunting(mp)) continue;   // juz w poscigu za skarbem (570 D) - jeden zarzadca bandy
                    }
                    pack.Add(mp);
                    try { theirs += mp.Party.EstimatedStrength; } catch { }
                }
                if (pack.Count == 0)
                {
                    if (kin && foreign > 0) Log.Info("HideoutPurge: odwet (570) - w promieniu tylko obce bandy (" + foreign + "), nikt nie idzie.");
                    return;
                }

                if (mine >= theirs * fleeOdds)
                {
                    if (kin) _reprisalFor = _pendingHideout;   // 570 E: jeden komunikat na kryjowke
                    Log.Player("Bandits circle the ridge to take their den back - one look at your line and they melt away.", true);
                    Log.Info("HideoutPurge: odwet stchorzyl (" + pack.Count + " band, sila " + (int)theirs + " vs " + (int)mine + ").");
                    return;
                }
                // POLACZENIE W JEDEN ODDZIAL (Jeff: "pojedynczo nie maja przewagi
                // i uciekaja"): pack scala sie w najwieksza bande - ludzie, jency
                // i dobytek przechodza, oproznione bandy znikaja ze swiata.
                // Jedna horda z suma sil idzie po swoje zloto.
                // 570 E: przy kluczu HideoutReprisalKinOnly scalane tylko bandy bez bohatera i nie bedace eskorta bohatera (Mergeable); boss tez
                // tylko z Mergeable. Bandy z bohaterem i eskorty bohatera zostaja sojusznikami w sile odwetu, ale BEZ naszych rozkazow: BK
                // (BKBanditBehavior.TickBandits, co godzine) kaze kazdej bandzie bez bohatera w promieniu 10 od bohatera-bandyty isc za nim
                // (SetFollow: eskorta + AI wylaczone 2 h) - rozkaz "bohater za bossem" dawalby wzajemna eskorte (boss za bohaterem, bohater
                // za bossem) i horda stalaby w miejscu. Bohater idzie wiec wlasnym rozumem (ochrona bohaterow), jego eskorta - za nim. Scalenie przez MergeInto (towar zdjety ze scalanej bandy,
                // sakiewka ludzi przeniesiona - dotad towar zostawal tez w znikajacej bandzie i nasluchy zniszczenia rozdawaly go drugi raz).
                MobileParty boss = null;
                foreach (var mp in pack)
                    if (mp.MapEvent == null && (!kin || Mergeable(mp)) && (boss == null || mp.MemberRoster.TotalManCount > boss.MemberRoster.TotalManCount))
                        boss = mp;
                if (kin && boss == null)
                {
                    _reprisalFor = _pendingHideout;
                    Log.Info("HideoutPurge: odwet (570) - w promieniu same bandy z bohaterem albo eskorty bohatera (" + pack.Count + ") - nikt ich nie scala ani nie prowadzi.");
                    return;
                }
                int merged = 0;
                int allies = 0;
                if (boss != null)
                {
                    foreach (var mp in pack)
                    {
                        if (mp == boss || mp.MapEvent != null) continue;
                        if (kin && !Mergeable(mp)) { allies++; continue; }
                        try
                        {
                            if (kin) { MergeInto(boss, mp); merged++; continue; }
                            var mr = mp.MemberRoster;
                            for (int i = 0; i < mr.Count; i++)
                            {
                                var el = mr.GetElementCopyAtIndex(i);
                                if (el.Character != null && el.Number > 0)
                                    boss.MemberRoster.AddToCounts(el.Character, el.Number, false, el.WoundedNumber);
                            }
                            var pr = mp.PrisonRoster;
                            for (int i = 0; i < pr.Count; i++)
                            {
                                var el = pr.GetElementCopyAtIndex(i);
                                if (el.Character != null && el.Number > 0)
                                    boss.PrisonRoster.AddToCounts(el.Character, el.Number, false, el.WoundedNumber);
                            }
                            var ir = mp.ItemRoster;
                            for (int i = 0; i < ir.Count; i++)
                            {
                                var el = ir.GetElementCopyAtIndex(i);
                                if (el.Amount > 0) boss.ItemRoster.AddToCounts(el.EquipmentElement, el.Amount);
                            }
                            try { boss.PartyTradeGold += mp.PartyTradeGold; mp.PartyTradeGold = 0; } catch { }   // zloto scalanej bandy do bossa (audyt C6)
                            mp.MemberRoster.Clear();
                            mp.PrisonRoster.Clear();   // jency juz u bossa - inaczej OutlawLaw policzy ich drugi raz
                            DestroyPartyAction.Apply(null, mp);
                            merged++;
                        }
                        catch (Exception e) { Log.Error("HideoutPurge.Merge", e); }
                    }
                }
                _reprisalPack.Clear();
                if (boss != null) _reprisalPack.Add(boss); else _reprisalPack.AddRange(pack);
                if (kin) _reprisalFor = _pendingHideout;
                _reprisalUntil = CampaignTime.HoursFromNow(Math.Max(1f, c.HideoutReprisalHours));
                StartPursuitClock();
                DriveReprisal();
                Log.Player("The scattered bands mass into one warband - they want their gold back and they will chase you for it!", true);
                Log.Info("HideoutPurge: odwet rusza JEDNA HORDA (" + (merged + 1) + " band scalono, "
                         + (boss != null ? boss.MemberRoster.TotalManCount : 0) + " ludzi, sila " + (int)theirs + " vs " + (int)mine + ")"
                         + (kin ? "; 570: klan " + (_pendingClan != null ? _pendingClan.StringId : "-") + ", sojusznikow bez scalenia i rozkazow (bohater / eskorta bohatera) " + allies + ", obcych band w promieniu pominietych " + foreign : "") + ".");
            }
            catch (Exception e) { Log.Error("HideoutPurge.Reprisal", e); }
        }

        // ================================================================ 570 E: kto nalezy do kryjowki i kogo wolno scalic

        /// <summary>570 E: klan bandytow kryjowki - z pokonanych w bitwie (pierwsza banda z klanem), zapasowo wlasciciel osady kryjowki.</summary>
        private static Clan BanditClanOf(MapEvent ev, BattleSideEnum winnerSide, Settlement sett)
        {
            try
            {
                var side = ev != null ? ev.GetMapEventSide(winnerSide == BattleSideEnum.Attacker ? BattleSideEnum.Defender : BattleSideEnum.Attacker) : null;
                if (side != null && side.Parties != null)
                    foreach (var p in side.Parties)
                    {
                        var mp = p != null && p.Party != null ? p.Party.MobileParty : null;
                        if (mp != null && mp.IsBandit && mp.ActualClan != null) return mp.ActualClan;
                    }
                if (sett != null && sett.OwnerClan != null && sett.OwnerClan.IsBanditFaction) return sett.OwnerClan;
            }
            catch { }
            return null;
        }

        /// <summary>570 E/D: banda klanu kryjowki albo z ta kryjowka jako domem (BanditPartyComponent.Hideout).</summary>
        internal static bool Kin(MobileParty mp, Clan clan, Settlement home)
        {
            if (mp == null) return false;
            try
            {
                if (clan != null && mp.ActualClan == clan) return true;
                var bc = mp.BanditPartyComponent;
                return home != null && bc != null && bc.Hideout != null && ReferenceEquals(bc.Hideout.Settlement, home);
            }
            catch { return false; }
        }

        /// <summary>570 E: eskorta bohatera (BK BKBanditBehavior.SetFollow: SetMoveEscortParty na partie bohatera-bandyty) - jej sklad nalezy do bohatera.</summary>
        internal static bool HeroEscort(MobileParty mp)
        {
            try
            {
                var t = mp != null ? mp.TargetParty : null;
                return mp != null && mp.DefaultBehavior == AiBehavior.EscortParty && t != null && t.IsActive && t.LeaderHero != null;
            }
            catch { return false; }
        }

        /// <summary>570 E: czy bande wolno scalic (wchlonac albo uczynic bossem): bez bohatera, nie eskorta bohatera, nie w zadaniu, nie w bitwie,
        /// nie w osadzie, nie w armii, nie nieumarli.</summary>
        internal static bool Mergeable(MobileParty mp)
        {
            try
            {
                return mp != null && mp.IsActive && !mp.IsMainParty && mp.LeaderHero == null && !HeroEscort(mp) && !mp.IsCurrentlyUsedByAQuest
                       && mp.MapEvent == null && mp.CurrentSettlement == null && mp.Army == null && !Undead.Party(mp)
                       && mp.MemberRoster != null && mp.MemberRoster.TotalManCount > 0
                       && !(Hero.MainHero != null && Hero.MainHero.IsPrisoner && Hero.MainHero.PartyBelongedToAsPrisoner == mp.Party);   // gracz w jej lochu
            }
            catch { return false; }
        }

        /// <summary>570 E: jedno scalenie bandy w bossa - ludzie, jency, towar, kiesa i sakiewka ludzi (MenPurse) przechodza; scalana banda pusta
        /// znika (DestroyPartyAction bez niszczyciela). Towar zdejmowany ze scalanej bandy (dotad zostawal i nasluchy zniszczenia - HorseFate,
        /// UniqueSpoils, A17Fix - rozdawaly go drugi raz: rzeczy z niczego). Bohater w skladzie - takiej bandy nie scalamy (Mergeable); jency-bohaterowie
        /// przechodza akcja gry TransferPrisonerAction (gracz w lochu - bandy nie scalamy). Zwraca liczbe przeniesionych ludzi. Wyjatek idzie do wolajacego.</summary>
        internal static int MergeInto(MobileParty boss, MobileParty mp)
        {
            int men = 0;
            var mr = mp.MemberRoster;
            var pr = mp.PrisonRoster;
            var moveM = new System.Collections.Generic.List<TroopRosterElement>();
            var moveP = new System.Collections.Generic.List<TroopRosterElement>();
            for (int i = 0; i < mr.Count; i++) { var el = mr.GetElementCopyAtIndex(i); if (el.Character != null && el.Number > 0 && !el.Character.IsHero) moveM.Add(el); }
            var heroesP = new System.Collections.Generic.List<CharacterObject>();
            for (int i = 0; i < pr.Count; i++)
            {
                var el = pr.GetElementCopyAtIndex(i);
                if (el.Character == null || el.Number <= 0) continue;
                if (!el.Character.IsHero) moveP.Add(el);
                else if (!el.Character.IsPlayerCharacter) heroesP.Add(el.Character);
            }
            foreach (var el in moveM)
            {
                boss.MemberRoster.AddToCounts(el.Character, el.Number, false, el.WoundedNumber);
                mr.AddToCounts(el.Character, -el.Number, false, -el.WoundedNumber);
                men += el.Number;
            }
            foreach (var el in moveP)
            {
                boss.PrisonRoster.AddToCounts(el.Character, el.Number, false, el.WoundedNumber);
                pr.AddToCounts(el.Character, -el.Number, false, -el.WoundedNumber);
            }
            // jency-bohaterowie akcja gry (TransferPrisonerAction) - zostaja w niewoli u bossa (dotad wpis kopiowany recznie, a stara partia ich wypuszczala)
            foreach (var h in heroesP) try { TransferPrisonerAction.Apply(h, mp.Party, boss.Party); } catch { }
            var ir = mp.ItemRoster;
            for (int i = ir.Count - 1; i >= 0; i--)
            {
                var el = ir.GetElementCopyAtIndex(i);
                if (el.Amount <= 0 || el.EquipmentElement.Item == null) continue;
                boss.ItemRoster.AddToCounts(el.EquipmentElement, el.Amount);
                ir.AddToCounts(el.EquipmentElement, -el.Amount);
            }
            try { if (mp.PartyTradeGold > 0) { boss.PartyTradeGold += mp.PartyTradeGold; mp.PartyTradeGold = 0; } } catch { }
            try { int purse = MenPurse.Get(mp); if (purse > 0) { int t = MenPurse.Take(mp, purse); if (t > 0) MenPurse.Add(boss, t); } } catch { }
            DestroyPartyAction.Apply(null, mp);
            return men;
        }

        /// <summary>570 D: poscig za skarbem przejmuje odwet - horda i sojusznicy dostaja normalna inicjatywe (od nastepnej godziny rozkazy poscigu).</summary>
        internal static int HandOverReprisal()
        {
            int n = _reprisalPack.Count;
            if (n == 0) return 0;
            foreach (var mp in _reprisalPack) try { if (mp != null && mp.IsActive) { mp.Ai.SetInitiative(1f, 1f, 1f); mp.Ai.RethinkAtNextHourlyTick = true; } } catch { }
            _reprisalPack.Clear();
            Log.Info("HideoutPurge: odwet przejety przez poscig za skarbem (570) - " + n + " partii." + PursuitTally());
            return n;
        }

        /// <summary>
        /// TWARDY ROZKAZ POGONI, odnawiany. SetInitiative to tylko sugestia -
        /// AI widzi przewage gracza i zawraca (test Jeffa: "wojska sie nie
        /// zebraly i mnie nie scigaly"). EngageParty z celem-graczem to rozkaz;
        /// poniewaz AiPartyThinkBehavior co chwile przemysliwa, ponawiamy go
        /// co pare sekund przez cala dobe odwetu - takze PO spladrowaniu,
        /// bo oni chca swojego zlota z powrotem.
        /// </summary>
        private static System.Reflection.MethodInfo _mSetAiBehavior;

        private static void DriveReprisal()
        {
            try
            {
                if (_reprisalPack.Count == 0) return;
                if (CampaignTime.Now >= _reprisalUntil)
                {
                    // nie zlapali - zapal gasnie, rozchodza sie do swoich spraw
                    foreach (var mp in _reprisalPack)
                        try { if (mp != null && mp.IsActive) mp.Ai.SetInitiative(1f, 1f, 1f); } catch { }
                    _reprisalPack.Clear();
                    Log.Info("HideoutPurge: pogon wygasla - horda daje za wygrana." + PursuitTally());
                    return;
                }
                if (_mSetAiBehavior == null)
                    _mSetAiBehavior = AccessTools.Method(typeof(MobilePartyAi), "SetAiBehavior");
                var me = MobileParty.MainParty;
                for (int i = _reprisalPack.Count - 1; i >= 0; i--)
                {
                    var mp = _reprisalPack[i];
                    if (mp == null || !mp.IsActive || mp.MemberRoster == null || mp.MemberRoster.TotalManCount <= 0)
                    { _reprisalPack.RemoveAt(i); continue; }
                    if (mp.MapEvent != null || mp.CurrentSettlement != null) continue;
                    try
                    {
                        mp.Ai.SetInitiative(2f, 0.05f, 48f);
                        if (_mSetAiBehavior != null)
                            _mSetAiBehavior.Invoke(mp.Ai, new object[] { AiBehavior.EngageParty, me.Party, me.Position });
                    }
                    catch { }
                }
                // E7/C: horda rozbita albo rozwiazana przed terminem - ta sama miara odnowien co przy wygasnieciu (raz na pogon)
                if (_reprisalPack.Count == 0)
                    Log.Info("HideoutPurge: pogon skonczona - hordy juz nie ma." + PursuitTally());
            }
            catch (Exception e) { Log.Error("HideoutPurge.DriveReprisal", e); }
        }

        private static void SearchTick(MenuCallbackArgs args, CampaignTime dt)
        {
            try
            {
                if (_searchLeave) { _searchLeave = false; GameMenu.SwitchToMenu("arm_hideout_search"); return; }
                float left = (float)(_searchDone - CampaignTime.Now).ToHours;
                if (left <= 0.01f)
                {
                    DoSearch();
                    GameMenu.ExitToLast();
                    return;
                }
                args.MenuContext.GameMenu.SetProgressOfWaitingInMenu(
                    MathF.Clamp(1f - left / _searchTotal, 0f, 1f));
            }
            catch (Exception e) { Log.Error("HideoutPurge.SearchTick", e); }
        }

        private static void DoSearch()
        {
            try
            {
                var c = Settings.Current;
                _pending = false;   // lup zebrany - koniec sprawy
                string hid = _pendingHideout != null ? _pendingHideout.StringId : "-";
                try
                {
                    var hs = _pendingHideout;
                    // nskarbiec: cala kasa kryjowki przez ksiege kas kryjowek (OutlawLaw.HoardTakeAll - kategoria "przeszukanie gracza", kontrola k i r)
                    _pendingGold = hs != null && hs.SettlementComponent != null ? OutlawLaw.HoardTakeAll(hs, OutlawLaw.HoardOutSearch) : 0;
                }
                catch { _pendingGold = 0; }
                // 568: magazyn kryjowki (towar z doli band) - zdejmowany przy przeszukaniu, ekran lupu pokaze OnTick na czystej mapie
                if (HideoutGoods.On)
                {
                    BuildLoot(_pendingHideout);
                    try
                    {
                        if (_lootRoster != null && _lootRoster.Count > 0)
                        {
                            HideoutGoods.NotePlayer(_lootRoster);
                            Log.Player("Stacked behind the bedrolls lie bales of plunder the bands had carried home - goods worth about "
                                       + HideoutGoods.ValueOfRoster(_lootRoster) + " denars.");
                        }
                    }
                    catch (Exception e) { HideoutGoods.Stumble("DoSearch", e); }
                }
                // 570 D: skarb zabrany (zloto + towar z magazynu) - bandy klanu kryjowki ruszaja w poscig (HideoutVendetta, klucz HideoutVendetta)
                try
                {
                    long goods = HideoutGoods.On && _lootRoster != null ? HideoutGoods.ValueOfRoster(_lootRoster) : 0;
                    HideoutVendetta.Start(_pendingHideout, _pendingClan, _pendingGold, goods);
                }
                catch (Exception e) { HideoutVendetta.Stumble("Start", e); }
                _pendingHideout = null;
                if (_pendingGold > 0) Hero.MainHero.ChangeHeroGold(_pendingGold);
                if (c.HideoutGoldMessages)
                {
                    // nskarbiec: komunikat zawsze - takze przy pustej kryjowce (dotad gracz nie wiedzial, czy cos bylo); zloty kolor, kopia w pliku
                    try
                    {
                        string t = _pendingGold > 0
                            ? "The hideout held " + _pendingGold + " denars - the hoard its bands had put by, buried under the bedrolls."
                            : "The hideout held no coin - its bands had spent or carried off everything they took.";
                        Log.Player(t);
                        Log.Info("Skarbce kryjowek (nskarbiec): przeszukanie " + _pendingName + " (" + hid + ") - skarbiec " + _pendingGold + " zl do gracza; komunikat: " + t);
                        OutlawLaw.NoteSearchMsg();
                    }
                    catch (Exception e) { OutlawLaw.HoardStumble("HideoutPurge.DoSearch(komunikat)", e); }
                }
                else if (_pendingGold > 0)
                {
                    Log.Player("Buried under the bedrolls you find the plunder of " + _pendingBands
                               + " raiding band" + (_pendingBands > 1 ? "s" : "") + ": " + _pendingGold + " gold.", true);
                }

                float renown = Math.Max(0f, c.HideoutRenown);
                if (renown > 0.01f) GainRenownAction.Apply(Hero.MainHero, renown);

                // wdziecznosc okolicy: im blizej kryjowki, tym wiecej
                int repMax = Math.Max(0, c.HideoutRepMax);
                float radius = Math.Max(1f, c.HideoutRepRadius);
                int touched = 0, best = 0;
                if (repMax > 0 && _pendingHasPos)
                {
                    foreach (var s in Settlement.All)
                    {
                        try
                        {
                            if (s == null || s.IsHideout) continue;
                            if (!s.IsVillage && !s.IsTown && !s.IsCastle) continue;
                            float d = s.GatePosition.Distance(_pendingPos);
                            if (d > radius) continue;
                            int rel = (int)MathF.Round(repMax * (1f - d / radius));
                            if (rel <= 0) continue;
                            bool any = false;
                            foreach (var notable in s.Notables)
                            {
                                if (notable == null || !notable.IsAlive) continue;
                                try
                                {
                                    ChangeRelationAction.ApplyPlayerRelation(notable, rel, false, false);
                                    any = true;
                                }
                                catch { }
                            }
                            if (any) { touched++; if (rel > best) best = rel; }
                        }
                        catch { }
                    }
                }
                if (touched > 0)
                    Log.Player("Word of the purge spreads: the folk of " + touched + " nearby settlement"
                               + (touched > 1 ? "s" : "") + " think better of you (up to +" + best + ").", true);
                Log.Info("HideoutPurge: przeszukano " + _pendingName + " - zloto " + _pendingGold
                         + ", renown " + renown.ToString("0.#") + ", osad z wdziecznoscia " + touched + ".");
            }
            catch (Exception e) { Log.Error("HideoutPurge.DoSearch", e); }
        }
    }
}
