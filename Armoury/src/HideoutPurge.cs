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
        // odwet: bandom odnawiamy rozkaz pogoni, bo AI co chwile przemysliwa
        private static readonly System.Collections.Generic.List<MobileParty> _reprisalPack =
            new System.Collections.Generic.List<MobileParty>();
        private static CampaignTime _reprisalUntil = CampaignTime.Zero;
        private static DateTime _lastReprisalRefresh = DateTime.MinValue;
        // lup przedmiotowy - ekran jak po bitwie, otwierany na czystej mapie
        private static ItemRoster _lootRoster;

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, _ => OneShotVendetta());
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, AddMenus);
            CampaignEvents.OnHideoutBattleCompletedEvent.AddNonSerializedListener(this, OnHideoutBattle);
            CampaignEvents.TickEvent.AddNonSerializedListener(this, OnTick);
        }

        public override void SyncData(IDataStore dataStore) { }

        private void OnHideoutBattle(BattleSideEnum winnerSide, HideoutEventComponent component,
                                     HideoutEventComponent.HideoutBattleEndState endState)
        {
            try
            {
                var c = Settings.Current;
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
                float spread = 0.75f + MBRandom.RandomFloat * 0.5f;   // +-25%
                _pendingGold = (int)MathF.Max(0f,
                    (Math.Max(0, c.HideoutGoldBase) + Math.Max(0, c.HideoutGoldPerBand) * bands) * spread);
                _pending = true;
                BuildLoot(bands);
                Log.Info("HideoutPurge: zwyciestwo w " + _pendingName + ", band " + bands + ", lup " + _pendingGold + " zlota czeka na przeszukanie.");
                // odwet rusza JUZ TERAZ - przy duzej partii przeszukanie trwa
                // ledwie pare godzin gry i bandy musza wyjsc w droge od razu
                Reprisal();
            }
            catch (Exception e) { Log.Error("HideoutPurge.OnHideoutBattle", e); }
        }

        /// <summary>
        /// Lup przedmiotowy kryjowki: proste zelastwo i lachy bandyckiej hordy -
        /// tier 1-3, rzeczy z kramow, po kilka sztuk na bande. Do tego czasem
        /// beczka czegos mocniejszego. Pokazywany ekranem lupow jak po bitwie.
        /// </summary>
        private static void BuildLoot(int bands)
        {
            try
            {
                var pool = new System.Collections.Generic.List<ItemObject>();
                foreach (var it in TaleWorlds.ObjectSystem.MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
                {
                    if (it == null || it.NotMerchandise) continue;
                    if (!it.HasWeaponComponent && !it.HasArmorComponent) continue;
                    int g = Recipes.Grade(it);
                    if (g < 1 || g > 3) continue;
                    if (it.Value < 20 || it.Value > 800) continue;
                    var id = (it.StringId ?? "").ToLowerInvariant();
                    if (id.Contains("practice") || id.Contains("tournament") || id.Contains("siege")
                        || id.Contains("ballista") || id.Contains("dummy") || id.Contains("test")) continue;
                    pool.Add(it);
                }
                _lootRoster = new ItemRoster();
                if (pool.Count == 0) return;
                int pieces = 3 + bands + MBRandom.RandomInt(bands + 1);
                for (int i = 0; i < pieces; i++)
                    _lootRoster.AddToCounts(pool[MBRandom.RandomInt(pool.Count)], 1);
                var beer = TaleWorlds.ObjectSystem.MBObjectManager.Instance.GetObject<ItemObject>("beer");
                if (beer != null) _lootRoster.AddToCounts(beer, 1 + MBRandom.RandomInt(2));
                Log.Info("HideoutPurge: lup przedmiotowy przygotowany (" + _lootRoster.Count + " pozycji).");
            }
            catch (Exception e) { Log.Error("HideoutPurge.BuildLoot", e); _lootRoster = null; }
        }

        private void OnTick(float dt)
        {
            try
            {
                if (Campaign.Current == null) return;
                // rozkaz pogoni odnawiany co ~3 s realne, dopoki odwet trwa
                if (_reprisalPack.Count > 0 && (DateTime.Now - _lastReprisalRefresh).TotalSeconds > 3.0)
                {
                    _lastReprisalRefresh = DateTime.Now;
                    DriveReprisal();
                }

                var st = Game.Current != null && Game.Current.GameStateManager != null
                    ? Game.Current.GameStateManager.ActiveState as TaleWorlds.CampaignSystem.GameState.MapState : null;
                bool cleanMap = st != null && !st.AtMenu
                    && PlayerEncounter.Current == null && MobileParty.MainParty.MapEvent == null;

                // ekran lupow jak po bitwie - po przeszukaniu, na czystej mapie
                if (!_pending && _lootRoster != null && _lootRoster.Count > 0 && cleanMap)
                {
                    var roster = _lootRoster; _lootRoster = null;
                    var dict = new System.Collections.Generic.Dictionary<PartyBase, ItemRoster>
                    { { PartyBase.MainParty, roster } };
                    Helpers.InventoryScreenHelper.OpenScreenAsLoot(dict);
                    return;
                }

                if (!_pending || !cleanMap) return;
                // menu dopiero na CZYSTEJ mapie - nigdy w srodku cudzego menu
                // ani rozliczania bitwy (lekcja z klawisza O w NightRest).
                // _pending NIE gasnie tutaj: gdy odwet przerwie przeszukanie
                // walka konczy sie na mapie i menu wraca samo - do lupu wraca
                // sie po bitwie (Jeff). Gasi je dopiero DoSearch albo Leave.
                GameMenu.ActivateGameMenu("arm_hideout_search");
            }
            catch (Exception e) { Log.Error("HideoutPurge.OnTick", e); _pending = false; }
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

                MobileParty boss = null;
                foreach (var mp in pack)
                    if (boss == null || mp.MemberRoster.TotalManCount > boss.MemberRoster.TotalManCount) boss = mp;
                int merged = 0;
                foreach (var mp in pack)
                {
                    if (mp == boss) continue;
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
                    "{=!}The men turn the hideout over - bedrolls, false floors, the cold ashes of the fire pits.",
                    SearchInit, delegate (MenuCallbackArgs a) { return true; }, null, SearchTick,
                    GameMenu.MenuAndOptionType.WaitMenuShowProgressAndHoursOption);
                starter.AddGameMenuOption("arm_hideout_search_wait", "arm_hideout_search_stop",
                    "{=!}Call the search off",
                    delegate (MenuCallbackArgs a) { a.optionLeaveType = GameMenuOption.LeaveType.Leave; return true; },
                    delegate (MenuCallbackArgs a) { GameMenu.SwitchToMenu("arm_hideout_search"); }, true, 9);
            }
            catch (Exception e) { Log.Error("HideoutPurge.AddMenus", e); }
        }

        private static void SearchInit(MenuCallbackArgs args)
        {
            try
            {
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
                float radius = Math.Max(1f, c.HideoutReprisalRadius);
                float fleeOdds = Math.Max(1f, c.HideoutReprisalFleeOdds);
                float mine = 0f;
                try { mine = MobileParty.MainParty.Party.EstimatedStrength; } catch { }

                var pack = new System.Collections.Generic.List<MobileParty>();
                float theirs = 0f;
                foreach (var mp in MobileParty.All)
                {
                    if (mp == null || !mp.IsBandit || !mp.IsActive) continue;
                    if (mp.CurrentSettlement != null) continue;          // siedza w innej kryjowce
                    if (mp.MapEvent != null) continue;
                    if (mp.GetPosition2D.Distance(_pendingPos.ToVec2()) > radius) continue;
                    pack.Add(mp);
                    try { theirs += mp.Party.EstimatedStrength; } catch { }
                }
                if (pack.Count == 0) return;

                if (mine >= theirs * fleeOdds)
                {
                    Log.Player("Bandits circle the ridge to take their den back - one look at your line and they melt away.", true);
                    Log.Info("HideoutPurge: odwet stchorzyl (" + pack.Count + " band, sila " + (int)theirs + " vs " + (int)mine + ").");
                    return;
                }
                // POLACZENIE W JEDEN ODDZIAL (Jeff: "pojedynczo nie maja przewagi
                // i uciekaja"): pack scala sie w najwieksza bande - ludzie, jency
                // i dobytek przechodza, oproznione bandy znikaja ze swiata.
                // Jedna horda z suma sil idzie po swoje zloto.
                MobileParty boss = null;
                foreach (var mp in pack)
                    if (mp.MapEvent == null && (boss == null || mp.MemberRoster.TotalManCount > boss.MemberRoster.TotalManCount))
                        boss = mp;
                int merged = 0;
                if (boss != null)
                {
                    foreach (var mp in pack)
                    {
                        if (mp == boss || mp.MapEvent != null) continue;
                        try
                        {
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
                            mp.MemberRoster.Clear();
                            DestroyPartyAction.Apply(null, mp);
                            merged++;
                        }
                        catch (Exception e) { Log.Error("HideoutPurge.Merge", e); }
                    }
                }
                _reprisalPack.Clear();
                if (boss != null) _reprisalPack.Add(boss); else _reprisalPack.AddRange(pack);
                _reprisalUntil = CampaignTime.HoursFromNow(Math.Max(1f, c.HideoutReprisalHours));
                DriveReprisal();
                Log.Player("The scattered bands mass into one warband - they want their gold back and they will chase you for it!", true);
                Log.Info("HideoutPurge: odwet rusza JEDNA HORDA (" + (merged + 1) + " band scalono, "
                         + (boss != null ? boss.MemberRoster.TotalManCount : 0) + " ludzi, sila " + (int)theirs + " vs " + (int)mine + ").");
            }
            catch (Exception e) { Log.Error("HideoutPurge.Reprisal", e); }
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
                    Log.Info("HideoutPurge: pogon wygasla - horda daje za wygrana.");
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
            }
            catch (Exception e) { Log.Error("HideoutPurge.DriveReprisal", e); }
        }

        private static void SearchTick(MenuCallbackArgs args, CampaignTime dt)
        {
            try
            {
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
                if (_pendingGold > 0)
                {
                    Hero.MainHero.ChangeHeroGold(_pendingGold);
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
