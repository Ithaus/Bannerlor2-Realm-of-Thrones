using System;
using System.Globalization;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// NOCLEG. Jeff: wojsko ma SPAC - 5 cichych godzin miedzy zmierzchem a switem
    /// (w obozie, pod dachem, na postoju). Mozna zarywac noce, ale dlug rosnie:
    /// pierwsza nieprzespana noc uchodzi na sucho, druga meczy, trzecia i dalsze
    /// lamia kolumne (predkosc do -30%, morale do -15). Dobra noc splaca dwie zle.
    /// Do tego szybki oboz: klawisz O na mapie stawia oboz BannerKings od reki,
    /// a w menu obozu jest "Bed down until dawn" - spisz do switu z paskiem.
    /// W sluzbie ROT (SLUZBA) dlugu nie liczymy - o marszach decyduje lord.
    /// </summary>
    internal static class NightRest
    {
        // dlug snu 0..5 i przespane godziny biezacej nocy
        internal static int Debt;
        private static float _restTonight;
        private static bool _credited;                  // dzisiejszy sen juz rozliczony (od reki, nie o swicie)
        private static Vec2 _lastPos;
        private static bool _hadPos;
        private static CampaignTime _sleepUntil = CampaignTime.Zero;
        private static string _sleepReturn = "camp";

        private static readonly int[] SpdPenalty = { 0, 0, 5, 15, 25, 30 };   // % za dlug 0..5
        private static readonly int[] MorPenalty = { 0, 0, 3, 8, 12, 15 };

        // ------------------------------------------------------------ rachunek nocy
        internal static void OnHourly()
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.NightRestEnabled) return;
                var mp = MobileParty.MainParty;
                if (mp == null || Hero.MainHero == null || !Hero.MainHero.IsAlive) return;
                // umarli nie spia: armia Innych nie zna dlugu snu
                if (Undead.Party(mp) || Undead.Character(Hero.MainHero.CharacterObject))
                { Debt = 0; _restTonight = 0f; return; }

                var pos = mp.GetPosition2D;
                bool moved = _hadPos && pos.Distance(_lastPos) > 0.35f;
                _lastPos = pos; _hadPos = true;
                if (moved) PlayerCamped = false;   // ruszyl sie = oboz zwinal (takze oboz BK)

                int h = CampaignTime.Now.GetHourOfDay;
                bool night = h >= 21 || h <= 5;
                bool resting = mp.CurrentSettlement != null
                               || !moved
                               || (s.SleepAtSeaFree && mp.IsCurrentlyAtSea)
                               || RotEnlisted();
                // spac mozna O KAZDEJ porze - noc liczy sie w calosci, dzien slabiej
                // (gwar obozu, upal, swiatlo); rachunek zamyka sie o swicie
                if (resting) _restTonight += night ? 1f : MBMath.ClampFloat(s.DayRestFactor, 0.1f, 1f);

                // ROZLICZENIE OD REKI (Jeff: "sen ma byc aktualizowany zaraz po
                // wypoczynku, nie czekac do przeliczenia") - gdy tylko godziny
                // snu sie uzbieraja, dlug schodzi natychmiast; swit juz tego
                // nie liczy drugi raz
                if (!_credited && _restTonight >= Math.Max(1f, s.SleepHoursNeeded))
                {
                    _credited = true;
                    if (Debt > 0)
                    {
                        Debt = Math.Max(0, Debt - 2);   // dobra noc splaca dwie zle
                        Msg(Debt == 0 ? "Well slept - the men are fresh again."
                                      : "Some sleep at last, but the men still owe the pillow (" + Debt + ").",
                            Colors.Green);
                    }
                }

                if (h == 6) SettleNight(s);
                AiNightCamp(s, h);
            }
            catch (Exception e) { Log.Error("NightRest.OnHourly", e); }
        }

        // ------------------------------------------------------------ swiat tez spi
        private static readonly System.Collections.Generic.List<MobileParty> _tented =
            new System.Collections.Generic.List<MobileParty>();

        /// <summary>
        /// ROZKAZ ZAPAMIETANY NA NOC. Stare obozowanie wolalo SetMoveModeHold(),
        /// ktore KASUJE rozkaz lorda - o swicie AI zaczynalo myslec od zera,
        /// a przy dwoch podobnie kuszacych celach chodzilo tam i z powrotem
        /// (Jeff w armii: "bezsensowne lazenie"). Teraz rozkaz zapisujemy przed
        /// snem i oddajemy go o switku - lord budzi sie z tym, po co wyszedl.
        /// </summary>
        private sealed class NightOrder
        {
            public TaleWorlds.CampaignSystem.Party.AiBehavior Behavior;
            public TaleWorlds.CampaignSystem.Settlements.Settlement Settlement;
            public MobileParty Party;
        }

        private static readonly System.Collections.Generic.Dictionary<MobileParty, NightOrder> _orders =
            new System.Collections.Generic.Dictionary<MobileParty, NightOrder>();

        private static void RememberOrder(MobileParty mp)
        {
            try
            {
                if (mp == null || _orders.ContainsKey(mp)) return;
                _orders[mp] = new NightOrder
                {
                    Behavior = mp.DefaultBehavior,
                    Settlement = mp.TargetSettlement,
                    Party = mp.TargetParty
                };
            }
            catch { }
        }

        private static void GiveOrderBack(MobileParty mp)
        {
            try
            {
                NightOrder o;
                if (mp == null || !_orders.TryGetValue(mp, out o)) return;
                _orders.Remove(mp);
                if (!mp.IsActive || mp.MapEvent != null || mp.CurrentSettlement != null) return;
                var nav = MobileParty.NavigationType.Default;
                var st = o.Settlement; var tp = o.Party;
                switch (o.Behavior)
                {
                    case TaleWorlds.CampaignSystem.Party.AiBehavior.GoToSettlement:
                        if (st != null) mp.SetMoveGoToSettlement(st, nav, false); break;
                    case TaleWorlds.CampaignSystem.Party.AiBehavior.BesiegeSettlement:
                        if (st != null) mp.SetMoveBesiegeSettlement(st, nav); break;
                    case TaleWorlds.CampaignSystem.Party.AiBehavior.RaidSettlement:
                        if (st != null) mp.SetMoveRaidSettlement(st, nav, false); break;
                    case TaleWorlds.CampaignSystem.Party.AiBehavior.DefendSettlement:
                        if (st != null) mp.SetMoveDefendSettlement(st, false, nav); break;
                    case TaleWorlds.CampaignSystem.Party.AiBehavior.PatrolAroundPoint:
                        if (st != null) mp.SetMovePatrolAroundSettlement(st, nav, false); break;
                    case TaleWorlds.CampaignSystem.Party.AiBehavior.EngageParty:
                        if (tp != null && tp.IsActive) mp.SetMoveEngageParty(tp, nav); break;
                    case TaleWorlds.CampaignSystem.Party.AiBehavior.EscortParty:
                        if (tp != null && tp.IsActive) mp.SetMoveEscortParty(tp, nav, false); break;
                    case TaleWorlds.CampaignSystem.Party.AiBehavior.GoAroundParty:
                        if (tp != null && tp.IsActive) mp.SetMoveGoAroundParty(tp, nav); break;
                    default: break;   // Hold/None i reszta - niech AI zdecyduje na swiezo
                }
            }
            catch { }
        }

        /// <summary>
        /// CALY SWIAT OBOZUJE. Nocna (22-4) godzina: partie lordow i karawany
        /// staja na nocleg (AI upione na godzine, wznawiane co tick nocy),
        /// z namiotem na mapie. NIE staja: scigani i uciekajacy (wrog w poblizu
        /// lub rozkaz ucieczki), armie w akcji (bitwa/oblezenie), zeglujacy,
        /// bandyci (nocni lowcy) i umarli - Inni nie znaja snu. O swicie
        /// namioty znikaja i AI budzi sie samo.
        /// </summary>
        private static void AiNightCamp(Settings s, int h)
        {
            try
            {
                bool night = h >= 22 || h <= 4;
                if (!s.AiCampsAtNight || !night)
                {
                    if (_tented.Count > 0)
                    {
                        foreach (var mp in _tented) Tent(mp, false);
                        _tented.Clear();
                    }
                    // SWIT: kazdy uspiony lord dostaje z powrotem swoj rozkaz
                    if (_orders.Count > 0)
                    {
                        var wake = new System.Collections.Generic.List<MobileParty>(_orders.Keys);
                        foreach (var mp in wake) GiveOrderBack(mp);
                        _orders.Clear();
                    }
                    return;
                }

                // zagrozenia: partie lordow i bandytow (pogon nie zna pory snu)
                var threats = new System.Collections.Generic.List<MobileParty>();
                foreach (var mp in MobileParty.All)
                {
                    if (mp == null || !mp.IsActive) continue;
                    if (mp.IsLordParty || mp.IsBandit) threats.Add(mp);
                }

                foreach (var mp in MobileParty.All)
                {
                    if (mp == null || !mp.IsActive || mp == MobileParty.MainParty) continue;
                    // bandyci obozuja TYLKO za przelacznikiem (Ai Bandits Camp Too,
                    // domyslnie OFF) - to wlaczenie ich hurtem polozylo gre 25.08,
                    // wiec wraca ostroznie i bez namiotow ponad limit
                    if (!mp.IsLordParty && !mp.IsCaravan && !(s.AiBanditsCampToo && mp.IsBandit)) continue;
                    if (mp.CurrentSettlement != null || mp.MapEvent != null || mp.BesiegerCamp != null) continue;
                    if (mp.IsCurrentlyAtSea) continue;
                    if (mp.Army != null && mp.Army.LeaderParty != mp) continue;   // eskorta idzie za wodzem
                    if (Undead.Party(mp)) continue;                               // Inni maszeruja noca
                    string stb = mp.ShortTermBehavior.ToString();
                    if (stb.StartsWith("Flee") || stb.StartsWith("Engage")) continue;   // ucieczka i pogon

                    bool danger = false;
                    var mf = mp.MapFaction;
                    foreach (var t in threats)
                    {
                        if (t == mp || t.MapFaction == mf) continue;
                        bool hostile = t.IsBandit || (mf != null && t.MapFaction != null && mf.IsAtWarWith(t.MapFaction));
                        if (!hostile) continue;
                        if (mp.GetPosition2D.Distance(t.GetPosition2D) <= s.AiCampDangerRadius) { danger = true; break; }
                    }
                    if (danger)
                    {
                        if (_tented.Contains(mp)) { Tent(mp, false); _tented.Remove(mp); }
                        GiveOrderBack(mp);          // alarm w nocy - rozkaz wraca od reki
                        continue;                   // wrog blisko - zwijaja sie i ida
                    }

                    RememberOrder(mp);              // po co wyszedl - zapisane przed snem
                    mp.Ai.DisableForHours(1);       // spia godzine; nocny tick odnowi
                    mp.SetMoveModeHold();
                    // limit ikon AiTentCap (reszta spi bez obrazka) - wlasnie brak
                    // tego limitu przy setkach partii konczyl sie CTD
                    if (s.CampTentIcon && _tented.Count < Math.Max(0, s.AiTentCap) && !_tented.Contains(mp))
                    { Tent(mp, true); _tented.Add(mp); }
                }
                if (s.CampTentIcon) ReassertTents();   // konie nie wracaja na namioty
            }
            catch (Exception e) { Log.Error("AiNightCamp", e); }
        }

        // ---------------------------------------------------- namiot na mapie
        // ZASADA z CLAUDE.md: licz potkniecia, nie gas funkcji. Stary _tentBroken
        // gasil namioty CALEMU swiatu po jednej wywrotce na jednej partii
        // (Jeff: "kiedys dzialalo, potem sie popsulo"). Teraz: 3 wywrotki
        // Z RZEDU wylaczaja, kazdy sukces zeruje licznik.
        private static int _tentStrikes;
        private const int TentStrikesMax = 3;

        /// <summary>Gracz stoi obozem TERAZ - dla bitwy w obozie (CampScene). Stan niezalezny od ikony.</summary>
        internal static bool PlayerCamped;

        internal static void Tent(MobileParty mp, bool on)
        {
            try
            {
                if (mp != null && mp == MobileParty.MainParty) PlayerCamped = on;
                var s = Settings.Current;
                if (_tentStrikes >= TentStrikesMax || mp == null || s == null || !s.CampTentIcon) return;
                var tMgr = QuartermasterLaw.FindType("SandBox.View.Map.Managers.MobilePartyVisualManager");
                var cur = tMgr != null ? tMgr.GetProperty("Current",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static) : null;
                object mgr = cur != null ? cur.GetValue(null, null) : null;
                if (mgr == null) return;
                var getVis = mgr.GetType().GetMethod("GetPartyVisual");
                object vis = getVis != null ? getVis.Invoke(mgr, new object[] { mp.Party }) : null;
                var pStrat = vis != null ? vis.GetType().GetProperty("StrategicEntity") : null;
                object strat = pStrat != null ? pStrat.GetValue(vis, null) : null;
                if (strat == null) return;
                var tEnt = strat.GetType();
                var removeAll = tEnt.GetMethod("RemoveAllChildren");
                if (on)
                {
                    // "map_icon_siege_camp_tent" to MULTIMESH, nie prefab - stad
                    // czerwone TEMP z Instantiate. Silnik ma na to GOTOWA metode,
                    // ktora stawia namiot Z CHORAGWIA klanu - wolamy ja wprost.
                    var mTent = vis.GetType().GetMethod("AddTentEntityForParty",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (mTent == null) { _tentStrikes = TentStrikesMax; Log.Info("Tent: brak AddTentEntityForParty - namioty wylaczone."); return; }
                    if (removeAll != null) removeAll.Invoke(strat, null);
                    mTent.Invoke(vis, new object[] { strat, mp.Party, false });
                    // figurki jezdzca i konia zyja OSOBNO od ikony - chowamy je,
                    // zeby kon nie stal na namiocie (tak samo robi oboz BK)
                    ShowAgentFigures(vis, false);
                }
                else
                {
                    ShowAgentFigures(vis, true);
                    // wlasciwy reset silnika: czysci ikone i kaze odbudowac figurke
                    var mClear = vis.GetType().GetMethod("ClearVisualMemory",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (mClear != null) mClear.Invoke(vis, null);
                    else
                    {
                        if (removeAll != null) removeAll.Invoke(strat, null);
                        try { mp.Party.SetVisualAsDirty(); } catch { }
                    }
                }
                _tentStrikes = 0;   // udalo sie - licznik potkniec od zera
            }
            catch (Exception e)
            {
                _tentStrikes++;
                Log.Error("Tent (potkniecie " + _tentStrikes + "/" + TentStrikesMax + ")", e);
                if (_tentStrikes >= TentStrikesMax) Log.Info("Tent: " + TentStrikesMax + " wywrotki z rzedu - namioty wylaczone do konca sesji.");
            }
        }

        /// <summary>Figurki czlowieka, konia i mulow karawany - widoczne albo nie.</summary>
        private static void ShowAgentFigures(object vis, bool visible)
        {
            try
            {
                string[] props = { "HumanAgentVisuals", "MountAgentVisuals", "CaravanMountAgentVisuals" };
                foreach (var name in props)
                {
                    try
                    {
                        var p = vis.GetType().GetProperty(name);
                        object av = p != null ? p.GetValue(vis, null) : null;
                        if (av == null) continue;
                        var mGet = av.GetType().GetMethod("GetEntity");
                        object ent = mGet != null ? mGet.Invoke(av, null) : null;
                        if (ent == null) continue;
                        var mVis = ent.GetType().GetMethod("SetVisibilityExcludeParents");
                        if (mVis != null) mVis.Invoke(ent, new object[] { visible });
                    }
                    catch { }
                }
            }
            catch { }
        }

        /// <summary>Silnik potrafi przywrocic figurki w nocy - obozujacym chowamy je co godzine.</summary>
        private static void ReassertTents()
        {
            try
            {
                var tMgr = QuartermasterLaw.FindType("SandBox.View.Map.Managers.MobilePartyVisualManager");
                var cur = tMgr != null ? tMgr.GetProperty("Current",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static) : null;
                object mgr = cur != null ? cur.GetValue(null, null) : null;
                if (mgr == null) return;
                var getVis = mgr.GetType().GetMethod("GetPartyVisual");


                foreach (var mp in _tented)
                {
                    try
                    {
                        if (mp == null || !mp.IsActive) continue;
                        object vis = getVis != null ? getVis.Invoke(mgr, new object[] { mp.Party }) : null;
                        if (vis != null) ShowAgentFigures(vis, false);
                    }
                    catch { }
                }
            }
            catch { }
        }

        private static void SettleNight(Settings s)
        {
            // splata poszla juz OD REKI (patrz OnHourly); swit tylko zamyka dobe
            // i dolicza dlug tym, ktorzy nocy nie przespali
            bool slept = _credited || _restTonight >= Math.Max(1f, s.SleepHoursNeeded);
            _restTonight = 0f;
            _credited = false;
            if (RotEnlisted()) { Debt = 0; return; }   // w sluzbie spisz, kiedy kaza

            if (slept) return;
            Debt = Math.Min(5, Debt + 1);
            if (Debt == 1) Msg("The men marched through the night. One sleepless night - they bear it.", Colors.Yellow);
            else if (Debt == 2) Msg("Second night without sleep - the column drags its feet (speed -" + SpdPenalty[2] + "%).", Colors.Yellow);
            else Msg(Debt + " sleepless nights - the men stumble half-dreaming (speed -" + SpdPenalty[Debt] + "%, morale -" + MorPenalty[Debt] + ").", Colors.Red);
        }

        private static void Msg(string t, Color c)
        {
            try { InformationManager.DisplayMessage(new InformationMessage(t, c)); } catch { }
        }

        // ------------------------------------------------------------ kary
        internal static void SpeedPostfix(MobileParty mobileParty, ref ExplainedNumber __result)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.NightRestEnabled || Debt < 2) return;
                if (mobileParty == null || mobileParty != MobileParty.MainParty) return;
                __result.AddFactor(-SpdPenalty[Math.Min(5, Debt)] / 100f, _txtSleepless);
            }
            catch { }
        }

        internal static void MoralePostfix(MobileParty mobileParty, ref ExplainedNumber __result)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.NightRestEnabled || Debt < 2) return;
                if (mobileParty == null || mobileParty != MobileParty.MainParty) return;
                __result.Add(-MorPenalty[Math.Min(5, Debt)], _txtSleepless);
            }
            catch { }
        }

        private static readonly TextObject _txtSleepless = new TextObject("{=!}Sleepless nights");

        // ------------------------------------------------------------ klawisz O
        private static bool _keyDown;
        private static bool _askOpen;

        internal static void OnTick(float dt)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.QuickCampKey || _askOpen) return;
                bool down = Input.IsKeyDown(InputKey.O);
                bool pressed = down && !_keyDown;
                _keyDown = down;
                if (!pressed) return;

                if (Campaign.Current == null || MobileParty.MainParty == null) return;
                var st = Game.Current != null && Game.Current.GameStateManager != null
                    ? Game.Current.GameStateManager.ActiveState as MapState : null;
                if (st == null || st.AtMenu) return;                        // tylko czysta mapa
                if (MobileParty.MainParty.CurrentSettlement != null) return;
                if (MobileParty.MainParty.IsCurrentlyAtSea) return;          // na pokladzie nie ma gdzie wbic palika
                if (PlayerEncounter.Current != null || MobileParty.MainParty.MapEvent != null) return;

                // NIE otwieramy menu prosto z ticku - to klablo gre (GameMenuVM
                // tykal w pol zbudowanego kontekstu). Pytajka jak w decyzji BK:
                // jej callback biegnie bezpieczna sciezka UI, ta sama co u nich.
                _askOpen = true;
                InformationManager.ShowInquiry(new InquiryData(
                    new TextObject("{=!}Make Camp").ToString(),
                    new TextObject("{=!}Pitch your tents here? Breaking camp will leave the party disorganized for a while.").ToString(),
                    true, true,
                    GameTexts.FindText("str_accept").ToString(),
                    GameTexts.FindText("str_cancel").ToString(),
                    delegate
                    {
                        _askOpen = false;
                        try
                        {
                            // BK Redux u Jeffa NIE rejestruje swojego obozu - wtedy
                            // staje NASZ oboz (to samo menu czekania, spanie w srodku)
                            if (!TryBkCamp()) OwnCamp();
                        }
                        catch (Exception e) { Log.Error("NightRest.CampAccept", e); }
                    },
                    delegate { _askOpen = false; }));
            }
            catch { }
        }

        /// <summary>Wlasny oboz Armoury - gdy oboz BannerKings nie istnieje w tej kampanii.</summary>
        private static void OwnCamp()
        {
            try
            {
                GameMenu.ActivateGameMenu("arm_camp_wait");
                Tent(MobileParty.MainParty, true);
                Msg("You pitch camp.", Colors.White);
            }
            catch (Exception e) { Log.Error("NightRest.OwnCamp", e); }
        }

        private static bool TryBkCamp()
        {
            try
            {
                var t = QuartermasterLaw.FindType("BannerKings.Behaviours.Camping.BKCampingBehavior");
                if (t == null) return false;
                var get = typeof(Campaign).GetMethod("GetCampaignBehavior").MakeGenericMethod(t);
                var beh = get.Invoke(Campaign.Current, null);
                var m = t.GetMethod("MakeCamp", BindingFlags.Public | BindingFlags.Instance);
                if (beh == null || m == null) return false;
                m.Invoke(beh, new object[] { MobileParty.MainParty });
                PlayerCamped = true;
                Msg("You pitch camp.", Colors.White);
                return true;
            }
            catch (Exception e) { Log.Error("NightRest.TryBkCamp", e); return false; }
        }

        // ------------------------------------------------------------ menu obozu
        internal static void AddMenus(CampaignGameStarter starter)
        {
            try
            {
                // zwykly postoj ("camp"): polozyc sie spac - o kazdej porze
                starter.AddGameMenuOption("camp", "arm_sleep_opt",
                    "{=!}Bed down and sleep", SleepOptionCondition,
                    delegate (MenuCallbackArgs a) { _sleepReturn = "camp"; GameMenu.SwitchToMenu("arm_sleep_wait"); }, false, 1);

                // WLASNY oboz (klawisz O, gdy obozu BK nie ma w kampanii).
                // ZWYKLE menu, nie "wait": oboz to twarda PAUZA - czas rusza
                // dopiero po wybraniu snu (arm_sleep_wait)
                starter.AddGameMenu("arm_camp_wait",
                    "{=!}You are encamped. The fires are lit, the men tend the horses and the wind worries the tents.",
                    delegate (MenuCallbackArgs a) { SetCampBackground(a); });
                starter.AddGameMenuOption("arm_camp_wait", "arm_camp_sleep",
                    "{=!}Bed down and sleep", SleepOptionCondition,
                    delegate (MenuCallbackArgs a) { _sleepReturn = "arm_camp_wait"; GameMenu.SwitchToMenu("arm_sleep_wait"); }, false, 1);
                starter.AddGameMenuOption("arm_camp_wait", "arm_camp_break",
                    "{=!}Break camp and move on",
                    delegate (MenuCallbackArgs a) { a.optionLeaveType = GameMenuOption.LeaveType.Leave; return true; },
                    delegate (MenuCallbackArgs a)
                    {
                        // zwijanie obozu to chwila krzatania, nie pol dnia:
                        // dezorganizacja rowno GODZINE (vanillowy model dawalby kilka)
                        try
                        {
                            MobileParty.MainParty.SetDisorganized(true);
                            var f = AccessTools.Field(typeof(MobileParty), "_disorganizedUntilTime");
                            if (f != null) f.SetValue(MobileParty.MainParty, CampaignTime.HoursFromNow(1f));
                        }
                        catch { }
                        Tent(MobileParty.MainParty, false);
                        Msg("The camp is struck - an hour to form the column, then we march.", Colors.Yellow);
                        GameMenu.ExitToLast();
                    }, true, 9);

                starter.AddWaitGameMenu("arm_sleep_wait",
                    "{=!}{ARM_SLEEP_TEXT}",
                    SleepInit, delegate (MenuCallbackArgs a) { return true; }, null, SleepTick,
                    GameMenu.MenuAndOptionType.WaitMenuShowProgressAndHoursOption);
                starter.AddGameMenuOption("arm_sleep_wait", "arm_sleep_stop",
                    "{=!}Rouse the men early",
                    delegate (MenuCallbackArgs a) { a.optionLeaveType = GameMenuOption.LeaveType.Leave; return true; },
                    delegate (MenuCallbackArgs a) { LeaveSleep(); }, true, 9);

                // oboz BannerKings pilnuje wlasnego menu co tick - tam wystarczy STAC
                starter.AddGameMenuOption("bk_camping_wait_menu", "arm_sleep_bk",
                    "{=!}Bed down for the night", SleepOptionCondition,
                    delegate (MenuCallbackArgs a)
                    {
                        Msg("The men bed down. Keep the camp standing through the night and they wake rested.", Colors.White);
                    }, false, 1);
                Log.Info("Nocleg: klawisz O + spanie w menu obozu dodane (dlug snu " + Debt + ").");
            }
            catch (Exception e) { Log.Error("NightRest.AddMenus", e); }
        }

        /// <summary>
        /// WYJSCIE ZE SNU. NIE wolno przelaczac menu (SwitchToMenu) z wnetrza opcji
        /// menu oczekiwania - VM menu jest w polowie klatki i konczy sie to
        /// NullReference w GameMenuVM.OnFrameTick, czyli CTD w obozie
        /// (Jeff: "jak klikam Rouse the men early wywala gre"). Wychodzimy tak,
        /// jak od zawsze dziala sasiedni, dzialajacy przycisk "Break camp".
        /// </summary>
        private static void LeaveSleep()
        {
            try
            {
                // pobudka zwija namiot - chyba ze WLASNY oboz dalej stoi
                // (wtedy namiot nalezy do obozu, zwinie go "Break camp")
                if (_sleepReturn != "arm_camp_wait")
                    Tent(MobileParty.MainParty, false);
                GameMenu.ExitToLast();
            }
            catch (Exception e) { Log.Error("NightRest.LeaveSleep", e); }
        }

        private static bool SleepOptionCondition(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Wait;
            var s = Settings.Current;
            return s != null && s.NightRestEnabled;
        }

        /// <summary>Tlo menu obozu: grafika kultury (jak u armii), zapasowo ogolna - koniec z czerwonym "temp".</summary>
        private static void SetCampBackground(MenuCallbackArgs args)
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

        private static void SleepInit(MenuCallbackArgs args)
        {
            try
            {
                SetCampBackground(args);
                // SEN = NAMIOT NA MAPIE (Jeff: "jak spimy, ikona konia/czlowieka
                // ma sie zmienic w namiot"). Stawiamy raz, przy wejsciu w sen -
                // nie co klatke (pulapka wizerunkow z CLAUDE.md)
                if (MobileParty.MainParty != null && MobileParty.MainParty.CurrentSettlement == null)
                    Tent(MobileParty.MainParty, true);
                var s = Settings.Current;
                float needed = s != null ? Math.Max(1f, s.SleepHoursNeeded) : 5f;
                _sleepUntil = CampaignTime.HoursFromNow(14f);   // bezpiecznik: nikt nie spi wiecznie
                int h = CampaignTime.Now.GetHourOfDay;
                bool night = h >= 21 || h <= 5;
                MBTextManager.SetTextVariable("ARM_SLEEP_TEXT",
                    _restTonight >= needed
                        ? "The men have already slept their fill today - but a little more never hurt."
                        : ("The men bed down and sleep" + (night ? "." : " - by daylight the rest comes slower.")));
                args.MenuContext.GameMenu.StartWait();
            }
            catch (Exception e) { Log.Error("NightRest.SleepInit", e); }
        }

        private static void SleepTick(MenuCallbackArgs args, CampaignTime dt)
        {
            try
            {
                var s = Settings.Current;
                float needed = s != null ? Math.Max(1f, s.SleepHoursNeeded) : 5f;
                if (_restTonight >= needed || (float)(_sleepUntil - CampaignTime.Now).ToHours <= 0.02f)
                {
                    Msg("The men wake rested and the camp stirs.", Colors.White);
                    LeaveSleep();
                    return;
                }
                args.MenuContext.GameMenu.SetProgressOfWaitingInMenu(Math.Min(1f, _restTonight / needed));
            }
            catch (Exception e) { Log.Error("NightRest.SleepTick", e); }
        }

        // ------------------------------------------------------------ sluzba ROT
        private static bool RotEnlisted()
        {
            try
            {
                var t = QuartermasterLaw.FindType("ROT.SubModule");
                var p = t != null ? t.GetProperty("EnlistmentBehavior", BindingFlags.Public | BindingFlags.Static) : null;
                object beh = p != null ? p.GetValue(null, null) : null;
                if (beh == null && t != null)
                {
                    var f = t.GetField("EnlistmentBehavior", BindingFlags.Public | BindingFlags.Static);
                    beh = f != null ? f.GetValue(null) : null;
                }
                if (beh == null) return false;
                var pe = beh.GetType().GetProperty("IsEnlisted", BindingFlags.Public | BindingFlags.Instance);
                return pe != null && pe.GetValue(beh, null) is bool b && b;
            }
            catch { return false; }
        }

        // ------------------------------------------------------------ save
        internal static string Export()
        {
            return Debt.ToString(CultureInfo.InvariantCulture) + ";" +
                   _restTonight.ToString(CultureInfo.InvariantCulture) + ";" +
                   (_credited ? "1" : "0");
        }

        internal static void Import(string data)
        {
            try
            {
                if (string.IsNullOrEmpty(data)) return;
                var parts = data.Split(';');
                if (parts.Length > 0) int.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out Debt);
                if (parts.Length > 1) float.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out _restTonight);
                _credited = parts.Length > 2 && parts[2] == "1";   // stary zapis (2 pola) = false
                Debt = Math.Max(0, Math.Min(5, Debt));
            }
            catch { }
        }

        // ------------------------------------------------------------ patche
        /// <summary>Tick ekranu mapy chodzi TAKZE na pauzie - klawisz O dziala w miejscu.</summary>
        internal static void MapFramePostfix()
        {
            try { OnTick(0f); } catch { }
        }

        internal static void ApplyAll(Harmony harmony)
        {
            try
            {
                int spd = PatchModels(harmony, typeof(PartySpeedModel), "CalculateFinalSpeed", "SpeedPostfix");
                int mor = PatchModels(harmony, typeof(PartyMoraleModel), "GetEffectivePartyMorale", "MoralePostfix");

                // CampaignEvents.TickEvent staje razem z pauza - a gracz w miejscu
                // to pauza wlasnie. Dopinamy sie do klatki ekranu mapy (chodzi zawsze).
                var tMap = QuartermasterLaw.FindType("SandBox.View.Map.MapScreen");
                var mTick = tMap != null ? AccessTools.Method(tMap, "OnFrameTick") : null;
                if (mTick != null)
                    harmony.Patch(mTick, postfix: new HarmonyMethod(typeof(NightRest).GetMethod(
                        "MapFramePostfix", BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public)));

                Log.Info("Nocleg: kary snu wpiete (predkosc w " + spd + ", morale w " + mor + " modelach), "
                         + "klawisz O na klatce mapy: " + (mTick != null) + ".");
            }
            catch (Exception e) { Log.Error("NightRest.ApplyAll", e); }
        }

        private static int PatchModels(Harmony harmony, Type baseType, string method, string postfixName)
        {
            var post = new HarmonyMethod(typeof(NightRest).GetMethod(
                postfixName, BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public))
            { priority = Priority.Last };
            int done = 0;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException rtle) { types = rtle.Types; }
                catch { continue; }
                foreach (var t in types)
                {
                    if (t == null || t.IsAbstract || !baseType.IsAssignableFrom(t)) continue;
                    try
                    {
                        var m = t.GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic |
                                                    BindingFlags.Instance | BindingFlags.DeclaredOnly);
                        if (m == null || m.IsAbstract) continue;
                        harmony.Patch(m, postfix: post);
                        done++;
                    }
                    catch { }
                }
            }
            return done;
        }
    }
}
