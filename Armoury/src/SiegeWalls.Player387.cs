using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// 387 (Jeff 10.11, punkt 2c listy niedokonczonych: "Gdy ty szturmujesz, twoj szturm sie nie zalamuje i limit czasu daje tylko komunikat" - "rob caly
    /// punkt 2"). SZTURM GRACZA-WODZA zalamuje sie wedlug tych samych regul co szturm AI (371 / 371b / 378), a potem obowiazuje ta sama przerwa.
    /// Dowod (dekompilacja 1.4.8): BattleEndLogic nie konczy misji odwrotem strony gracza, dopoki gracz zyje (mb_full 79541-79623);
    /// CampaignSiegeStateHandler.OnEndMission przesuwa oblezenie do sali lorda, gdy gracz-atakujacy konczy misje bez odwrotu i bez DefenderVictory
    /// (d371/CSSH.cs) - dlatego bezpiecznik 387 daje wynik DefenderVictory / PlayerDefeated (jak natywne CreateDefeated); po takiej misji
    /// PlayerEncounter.ContinueBattle ("Battle continues", cs_full 98873) zostawia gracza w menu "encounter", a gra ma tam gotowa droge
    /// "Continue siege preparations." (cs_full 185411: gracz wodzem strony, oblezenie, mury OnTheWalls -> PlayerEncounter.Finish bez zwyciezcy +
    /// menu_siege_strategies; oblezenie trwa). Sami niczego nie przelaczamy (zadnego SwitchToMenu) - tylko wylaczamy ponowny atak:
    ///  - rekord szturmu gracza trzymany OSOBNO (_plMe / _plAssault), nie w _assaults - inaczej ozylyby dla gracza OB3 (TicksPost), liczniki szturmow AI
    ///    i "szturm przerwany" oblezen 3 (OnMapEventEnded);
    ///  - przerwa (Repel bez FinishBattle: 1-5 dob, 7-14 gdy taran / wieza stracone) zapisywana OD RAZU przy koncu misji - trafia do arm_siege_walls,
    ///    zanim gracz kliknie cokolwiek w menu (wczytanie w menu "encounter" nie gubi blokady);
    ///  - postfiksy warunkow menu (tylko IsEnabled i dymek): menu_siege_strategies "Lead an assault" / "Send troops" oraz encounter "Attack!" /
    ///    "Send troops" (tylko szturm, gracz wodzem atakujacych, mury OnTheWalls - wtedy gra pokazuje "Continue siege preparations.").
    /// Wyjatek = potkniecie (Stumble), bez globalnego wylacznika. Rekord tylko w pamieci (jak 371: wczytanie miedzy misjami tej samej bitwy = straty od zera).
    /// </summary>
    internal static partial class SiegeWalls
    {
        /// <summary>387: szturm gracza-wodza lamie sie jak szturm AI (wymaga 371 w misji).</summary>
        internal static bool PlayerBreakOn { get { var s = Settings.Current; return s != null && s.SiegeAssaultBreak && s.SiegeAssaultBreakMission && s.SiegePlayerAssaultBreaks; } }

        private static MapEvent _plMe;
        private static Assault _plAssault;
        private static int _dPlSeen, _dPlNone, _dPlWarn, _dPlBreak, _dPlFuse, _dPlPause, _dPlMenuSiege, _dPlMenuEnc;
        private static string _hookPl = "nie zakladana";

        /// <summary>387 (SiegeBreak371, gracz atakuje i dowodzi): rekord szturmu biezacej bitwy gracza. null + powod, gdy poza regula. Nie rzuca.</summary>
        internal static Assault MissionAssaultPlayer(MapEvent me, out string why)
        {
            why = null;
            try
            {
                if (!Live || me == null) { why = "bez kampanii"; return null; }
                if (!me.IsSiegeAssault) { why = "nie szturm"; return null; }
                if (me.IsNavalMapEvent) { why = "morze"; return null; }
                var st = me.MapEventSettlement;
                if (st == null || !st.IsFortification) { why = "bez twierdzy"; return null; }
                var lpb = me.AttackerSide != null ? me.AttackerSide.LeaderParty : null;
                var lp = lpb != null && lpb.IsMobile ? lpb.MobileParty : null;
                if (lp == null || !lp.IsMainParty) { why = "gracz nie jest wodzem szturmu"; return null; }
                if (st.CurrentSiegeState == Settlement.SiegeState.InTheLordsHall) { why = "walka o sale lorda"; return null; }
                // ta sama bitwa (kolejna misja "Battle continues") - ten sam rekord (straty i czas z poprzednich misji); po zapisanej przerwie - nowy szturm
                if (ReferenceEquals(_plMe, me) && _plAssault != null && !_plAssault.Repelled) return _plAssault;
                var a = NewAssault(me, lp, st);
                _plMe = me; _plAssault = a;
                why = "rekord gracza zalozony w misji";
                return a;
            }
            catch (Exception e) { Stumble("MissionAssaultPlayer", e); why = "potkniecie"; return null; }
        }

        internal static void NotePlayerSeen() { _dPlSeen++; }
        internal static void NotePlayerNone() { _dPlNone++; }
        internal static void NotePlayerWarn() { _dPlWarn++; }

        /// <summary>387 (SiegeBreak371.OnEndMission): szturm gracza zalamal sie w misji - przerwa jak po OB2, od razu (bitwa na mapie jeszcze trwa:
        /// gracz stoi w menu "encounter"). Tylko dane (Pause.Until / Repels, a.Repelled); bitwy nie konczymy - robi to opcja gry.</summary>
        internal static void RegisterPlayerBreak(MapEvent me, Assault a, bool fuse)
        {
            try
            {
                _dPlBreak++; if (fuse) _dPlFuse++;
                if (me == null || a == null || a.Repelled || !BreakOn) return;
                var st = a.St;
                if (st == null || st.SiegeEvent == null) return;
                int left = 0, dmen = 0;
                try { left = me.AttackerSide.TroopCount; dmen = me.DefenderSide.TroopCount; } catch { }
                Repel(me, a, left, dmen, false, true);
                _dPlPause++;
            }
            catch (Exception e) { Stumble("RegisterPlayerBreak", e); }
        }

        /// <summary>Z OnMapEventEnded (przed wyjsciem bez rejestru): bitwa gracza skonczona - rekord szturmu gracza do kosza.</summary>
        private static void PlayerEventEnded(MapEvent me)
        {
            if (_plMe != null && (ReferenceEquals(_plMe, me) || _plMe.IsFinalized)) { _plMe = null; _plAssault = null; }
        }

        private static void Reset387() { _plMe = null; _plAssault = null; ZeroDay387(); }

        private static void ZeroDay387() { _dPlSeen = _dPlNone = _dPlWarn = _dPlBreak = _dPlFuse = _dPlPause = _dPlMenuSiege = _dPlMenuEnc = 0; }

        private static string Day387Text()
        {
            return "; 387 szturm gracza " + (PlayerBreakOn ? "tak" : "nie") + ": misji z graczem-wodzem " + _dPlSeen + ", poza regula " + _dPlNone + ", ostrzezen 75% " + _dPlWarn + ", zalaman "
                 + _dPlBreak + " (bezpiecznik " + _dPlFuse + "), przerw zapisanych " + _dPlPause + ", szturm nieczynny (klatki menu): oblezenie " + _dPlMenuSiege + ", encounter " + _dPlMenuEnc
                 + " (latki " + _hookPl + ")";
        }

        private static TextObject PauseTip(double left)
        {
            int d = (int)Math.Max(1.0, Math.Ceiling(left));
            return new TextObject("{=!}Your men will not storm the walls again so soon - they need " + d + (d == 1 ? " more day." : " more days."));
        }

        /// <summary>Postfiks SiegeEventCampaignBehavior.game_menu_siege_strategies_lead_assault_on_condition i ..._order_assault_on_condition: w przerwie po
        /// zalamanym szturmie "Lead an assault" i "Send troops" nieczynne z dymkiem. Opcje juz nieczynne (gracz nie dowodzi, ranny, przygotowania, okup)
        /// zostaja z dymkiem gry. Tylko IsEnabled i dymek. Zwykle: jedno porownanie. Nie rzuca.</summary>
        public static void PlayerSiegeOptPost(MenuCallbackArgs __0, ref bool __result)
        {
            if (!__result || (_pauses.Count == 0 && _pendingPauses.Count == 0)) return;
            try
            {
                if (__0 == null || !__0.IsEnabled || !PlayerBreakOn) return;
                var main = MobileParty.MainParty;
                var camp = main != null ? main.BesiegerCamp : null;
                var ev = camp != null ? camp.SiegeEvent : null;
                var st = ev != null ? ev.BesiegedSettlement : null;
                if (st == null) return;                                            // gracz nie wodz - gra sama wylacza opcje (wyjscie na !IsEnabled wyzej)
                if (st.CurrentSiegeState != Settlement.SiegeState.OnTheWalls) return;   // 387b: mury wziete (sala lorda) - przerwa nie blokuje dokonczenia
                if (SiegeWalls2.Surrendered(st)) return;                           // twierdza po kapitulacji z glodu - bez przerwy (jak Veto AI)
                double left = RepelPauseLeftDays(ev);
                if (left <= 0.0) return;
                __0.IsEnabled = false;
                __0.Tooltip = PauseTip(left);
                _dPlMenuSiege++;
            }
            catch (Exception e) { Stumble("PlayerSiegeOptPost", e); }
        }

        /// <summary>Postfiks EncounterGameMenuBehavior.game_menu_encounter_attack_on_condition i ..._order_attack_on_condition: po zalamanym szturmie
        /// gracza bitwa na mapie trwa ("Battle continues") i menu "encounter" znow daje "Attack!" - w przerwie nieczynne. Tylko szturm murow, gracz
        /// wodzem atakujacych i mury OnTheWalls: wtedy gra pokazuje "Continue siege preparations." (powrot do obozu, oblezenie trwa) i "Leave...".
        /// Kazda inna bitwa - bez zmian. Nie rzuca.</summary>
        public static void PlayerEncOptPost(MenuCallbackArgs __0, ref bool __result)
        {
            if (!__result || (_pauses.Count == 0 && _pendingPauses.Count == 0)) return;
            try
            {
                if (__0 == null || !__0.IsEnabled || !PlayerBreakOn) return;
                var me = MapEvent.PlayerMapEvent;
                if (me == null || !me.IsSiegeAssault || me.IsNavalMapEvent || me.PlayerSide != BattleSideEnum.Attacker) return;
                if (me.AttackerSide == null || me.AttackerSide.LeaderParty != PartyBase.MainParty) return;
                var st = me.MapEventSettlement;
                var ev = st != null ? st.SiegeEvent : null;
                if (ev == null || !st.IsFortification || st.CurrentSiegeState != Settlement.SiegeState.OnTheWalls) return;
                // 387b: "Continue siege preparations." gry wymaga jeszcze wojny (cs_full 185420) - bez niej nie zostawiamy menu z samymi nieczynnymi opcjami
                if (Hero.MainHero == null || !FactionManager.IsAtWarAgainstFaction(Hero.MainHero.MapFaction, st.MapFaction)) return;
                if (SiegeWalls2.Surrendered(st)) return;
                double left = RepelPauseLeftDays(ev);
                if (left <= 0.0) return;
                __0.IsEnabled = false;
                __0.Tooltip = PauseTip(left);
                _dPlMenuEnc++;
            }
            catch (Exception e) { Stumble("PlayerEncOptPost", e); }
        }

        private static bool Wired387(System.Reflection.MethodBase m, string post)
        {
            try
            {
                var info = m != null ? Harmony.GetPatchInfo(m) : null;
                if (info != null && info.Postfixes != null)
                    foreach (var p in info.Postfixes)
                        if (p != null && p.PatchMethod != null && p.PatchMethod.DeclaringType == typeof(SiegeWalls) && p.PatchMethod.Name == post) return true;
            }
            catch { }
            return false;
        }

        /// <summary>Z SiegeWalls.EnsureHooks (kampania, raz na proces). Cele zadeklarowane w swoich klasach - DeclaredMethod.</summary>
        private static void EnsureHooks387()
        {
            int wired = 0, found = 0;
            try
            {
                foreach (var name in new[] { "game_menu_siege_strategies_lead_assault_on_condition", "game_menu_siege_strategies_order_assault_on_condition" })
                {
                    try
                    {
                        var m = AccessTools.DeclaredMethod(typeof(SiegeEventCampaignBehavior), name);
                        if (m == null) continue;
                        found++;
                        Seal31.Patch("387: szturm gracza w przerwie po zalamaniu (SiegeEventCampaignBehavior." + name + ")", m, typeof(SiegeWalls), null, nameof(PlayerSiegeOptPost), null,
                                     Priority.Normal, Priority.Last);
                        if (Wired387(m, nameof(PlayerSiegeOptPost))) wired++;
                    }
                    catch (Exception e) { Stumble("EnsureHooks387." + name, e); }
                }
                foreach (var name in new[] { "game_menu_encounter_attack_on_condition", "game_menu_encounter_order_attack_on_condition" })
                {
                    try
                    {
                        var m = AccessTools.DeclaredMethod(typeof(EncounterGameMenuBehavior), name);
                        if (m == null) continue;
                        found++;
                        Seal31.Patch("387: atak w menu encounter w przerwie po zalamaniu (EncounterGameMenuBehavior." + name + ")", m, typeof(SiegeWalls), null, nameof(PlayerEncOptPost), null,
                                     Priority.Normal, Priority.Last);
                        if (Wired387(m, nameof(PlayerEncOptPost))) wired++;
                    }
                    catch (Exception e) { Stumble("EnsureHooks387." + name, e); }
                }
                _hookPl = wired == 4 ? "wpiete 4/4" : "NIEPELNE: wpiete " + wired + "/4 (znalezione " + found + "/4) - linia 'Szczelnosc (164a)'";
            }
            catch (Exception e) { _hookPl = "BRAK (blad: " + e.Message + ")"; Stumble("EnsureHooks387", e); }
        }

        private static string Start387Text()
        {
            return " | 387 szturm gracza-wodza: klucz " + (PlayerBreakOn ? "tak" : "nie") + ", latki menu " + _hookPl;
        }
    }
}
