using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// m95-zajazd - POBYT W ZAJEZDZIE (Jeff 20.10: "Jak wchodze do zajazdu to powinienem znikac z mapy tak jak sie wchodzi do wioski i powinny byc odglosy karczmy,
    /// ze jestem w karczmie").
    /// Dzis (WGRANIE 94): zajazd to nie osada - menu "arm_roadinn" otwiera sie przy partii stojacej na mapie (RoadsideInns.Open: postoj + ActivateGameMenu),
    /// CurrentSettlement zostaje null, wiec gra rysuje partie gracza dalej (jezdziec, kon, napis z imieniem) obok budynku zajazdu, a przy snie - namiot; menu bez
    /// dzwieku tla, obrazek wsi okregu.
    /// Jak gra chowa partie w wiosce (dekompilacja 1.4.8): EnterSettlementAction ustawia CurrentSettlement, wizerunek (MobilePartyVisual.RefreshPartyIcon) przy
    /// CurrentSettlement != null stawia tylko choragiew przy osadzie, napis gracza zdejmuje PartyNameplatesVM.UpdateMobilePartyVisibility (CurrentSettlement != null).
    /// Wejscie do osady odpada (zajazd nie jest osada: menu wioski, pozycja przy wsi, AI). Zostaje droga mgly wojny gry: MobileParty.IsVisible = false - wizerunek
    /// gasnie sam (MobilePartyVisual.TickFadingState, jak partie poza zasiegiem wzroku; dzieci wizerunku - namiot - z nim), kroki koni milkna (MapScreen gra je
    /// tylko widocznym partiom), partie armii gracza ida za widocznoscia wodza (PartyBase.CalculateVisibilityAndInspected). AI widocznosci partii gracza nie czyta
    /// (TaleWorlds.CampaignSystem, SandBox, BK, ROT - sprawdzone grep; ROT sam chowa tak partie w sluzbie, wtedy bez nas).
    ///  U1 Ukrycie (klucz RoadsideInnHideParty): wejscie do menu zajazdu (MenuInit) i sen w zajezdzie (RoadSleepInit) - IsVisible false RAZ, przy zmianie stanu.
    ///     Gra liczy widocznosc partii gracza od nowa co tick, gdy czas biegnie (CampaignTickCacheDataStore.UpdateVisibilitiesAroundMainParty ->
    ///     PartyBase.UpdateVisibilityAndInspected): prefiks na tej metodzie (klasa deklarujaca PartyBase) przytrzymuje false tylko dla partii gracza i tylko w stanie
    ///     "w zajezdzie"; poza nim metoda gry bez zmian. Napis z imieniem gracza (PlayerNameplate) zdejmuje prefiks na PartyNameplatesVM.UpdateMobilePartyVisibility,
    ///     jak gra przy wejsciu do osady; po wyjsciu gra stawia go sama (ta sama metoda, partia znow widoczna).
    ///     Wyjscie: "Ride on" od razu (Leave), reszta - straznik 4 razy na sekunde z OnApplicationTick (dziala takze przy zatrzymanym czasie): stan "w zajezdzie" =
    ///     menu arm_roadinn przy zajezdzie albo sen oplacony w tym zajezdzie (arm_sleep_wait, TownInns.RoadSleepUid), partia aktywna, poza osada, bitwa, spotkaniem,
    ///     oblezeniem, morzem, niewola i sluzba ROT. Inaczej (pobudka, bitwa, napad, klucz wylaczony, menu zamkniete) - partia z powrotem: widocznosc liczy gra
    ///     (UpdateVisibilityAndInspected) dla partii gracza i jej armii. Wczytanie zapisu w zajezdzie: stan tylko w pamieci, straznik chowa partie od nowa (IsVisible
    ///     jest w zapisie gry, ale gra liczy je od nowa przy wczytaniu; stary DLL - partia wraca przy pierwszym ticku czasu).
    ///  U2 Dzwiek (klucz RoadsideInnTavernSound): to, co gra daje w dzielnicy karczmy miasta (PlayerTownVisitCampaignBehavior, menu "town_backstreet"): tlo
    ///     "event:/map/ambient/node/settlements/2d/tavern" i dzwiek wejscia "event:/ui/panels/panel_settlement_enter_tavern" (oba w Native sound_event_data.gen.xml).
    ///     MenuViewContext gra tlo, dopoki menu jest otwarte (takze przez sen - to samo menu), i wylacza je przy zamknieciu menu. Tlo obrazka: gra nie ma obrazka
    ///     karczmy (dzielnica karczmy pokazuje wait_mesh miasta) - zajazd zostaje przy wait_mesh wsi okregu, jak dotad.
    ///  Linie "Zajazdy (ZJ): pobyt - ..." - start sesji (klucze, latki), kazde ukrycie i powrot (z powodem), dzwiek, doba. Potkniecia liczone (pierwsze do Log.Error),
    ///  nic nie gasimy globalnie; latka napisu po 3 potknieciach z rzedu - napis zostaje jak dotad, ukrycie dziala dalej. Nic do zapisu gry.
    /// </summary>
    internal static class RoadsideInnStay
    {
        private const string P = "Zajazdy (ZJ): pobyt - ";
        internal const string AmbientTavern = "event:/map/ambient/node/settlements/2d/tavern";
        internal const string PanelTavern = "event:/ui/panels/panel_settlement_enter_tavern";
        private const string SleepMenu = "arm_sleep_wait";
        private const int PlateStrikesMax = 3;
        private const int ReassertMax = 20;   // na jeden pobyt: cos innego uparcie pokazuje partie - oddajemy (wizerunek nie mruga w nieskonczonosc)

        internal static bool HideOn { get { var s = Settings.Current; return s != null && s.RoadsideInns && s.RoadsideInnHideParty; } }
        internal static bool SoundOn { get { var s = Settings.Current; return s != null && s.RoadsideInns && s.RoadsideInnTavernSound; } }
        // stan pobytu (tylko pamiec)
        private static bool _hidden;
        private static string _hiddenUid, _hiddenName;
        private static int _stayReassert;
        private static long _lastCheck;
        // latki (raz na proces)
        private static Harmony _h;
        private static bool _visTried, _visPatched, _plateTried, _platePatched;
        private static int _plateLook;   // proby znalezienia typu napisu (zestaw SandBox.ViewModelCollection moze ladowac sie pozniej) - najwyzej 3
        private static string _visHook = "nie wpieta (klucz wylaczony przy starcie)", _plateHook = "nie wpieta (klucz wylaczony przy starcie)";
        private static PropertyInfo _plateProp;
        private static MethodInfo _plateClear;
        private static int _plateStrikes;
        // liczniki doby i sesji
        private static int _dHide, _dShow, _dHold, _dReassert, _dPlate, _dSound, _dStumbles, _stumbles;
        private static bool _errLogged;

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _dStumbles++;
            if (!_errLogged) { _errLogged = true; Log.Error("RoadsideInns.Stay." + where, e); }
        }

        /// <summary>Nowa kampania albo wczytanie (RoadsideInns.Reset): stan pobytu i liczniki od zera (latki zostaja - raz na proces).</summary>
        internal static void Reset()
        {
            _hidden = false; _hiddenUid = _hiddenName = null; _stayReassert = 0; _lastCheck = 0;
            _dHide = _dShow = _dHold = _dReassert = _dPlate = _dSound = _dStumbles = 0;
            _stumbles = 0; _errLogged = false; _plateStrikes = 0;
        }

        // ------------------------------------------------------------ latki (raz na proces)
        private static Harmony H() { return _h ?? (_h = new Harmony("com.jeff.armoury.m95zajazd")); }

        private static void EnsurePatches()
        {
            if (!_visTried)
            {
                _visTried = true;
                try
                {
                    var m = AccessTools.DeclaredMethod(typeof(PartyBase), "UpdateVisibilityAndInspected", new[] { typeof(CampaignVec2), typeof(float) });
                    if (m == null) _visHook = "BRAK (PartyBase.UpdateVisibilityAndInspected) - ukrycie wylaczone";
                    else
                    {
                        H().Patch(m, prefix: new HarmonyMethod(typeof(RoadsideInnStay), nameof(VisPrefix)));
                        _visPatched = HasOurs(m);   // CLAUDE.md 7: wyjatek Harmony bywa polykany - sprawdzamy, czy prefiks naprawde siedzi
                        _visHook = (_visPatched ? "wpieta " : "NIE weszla (brak prefiksu po Patch) - ukrycie wylaczone: ") + m.DeclaringType.FullName + "." + m.Name + (_visPatched ? " (prefiks)" : "");
                    }
                }
                catch (Exception e) { _visHook = "wyjatek " + e.GetType().Name + " - ukrycie wylaczone"; Stumble("Patch.Vis", e); }
            }
            if (!_plateTried)
            {
                try
                {
                    var t = AccessTools.TypeByName("SandBox.ViewModelCollection.Nameplate.PartyNameplatesVM");
                    if (t == null && ++_plateLook < 3) { _plateHook = "typ PartyNameplatesVM jeszcze nie zaladowany (proba " + _plateLook + "/3)"; return; }
                    _plateTried = true;
                    var m = t != null ? AccessTools.DeclaredMethod(t, "UpdateMobilePartyVisibility", new[] { typeof(MobileParty) }) : null;
                    _plateProp = t != null ? AccessTools.DeclaredProperty(t, "PlayerNameplate") : null;
                    _plateClear = _plateProp != null ? AccessTools.Method(_plateProp.PropertyType, "Clear", Type.EmptyTypes) : null;
                    if (m == null || _plateProp == null || !_plateProp.CanWrite || _plateClear == null)
                        _plateHook = "BRAK (" + (t == null ? "typ PartyNameplatesVM" : m == null ? "UpdateMobilePartyVisibility" : "PlayerNameplate / Clear") + ") - napis gracza zostaje";
                    else
                    {
                        H().Patch(m, prefix: new HarmonyMethod(typeof(RoadsideInnStay), nameof(PlatePrefix)));
                        _platePatched = HasOurs(m);
                        _plateHook = (_platePatched ? "wpieta " : "NIE weszla (brak prefiksu po Patch) - napis gracza zostaje: ") + m.DeclaringType.FullName + "." + m.Name + (_platePatched ? " (prefiks)" : "");
                    }
                }
                catch (Exception e) { _plateTried = true; _plateHook = "wyjatek " + e.GetType().Name + " - napis gracza zostaje"; Stumble("Patch.Plate", e); }
            }
        }

        private static bool HasOurs(MethodBase m)
        {
            var pi = Harmony.GetPatchInfo(m);
            if (pi == null) return false;
            foreach (var x in pi.Prefixes) if (x.PatchMethod != null && x.PatchMethod.DeclaringType == typeof(RoadsideInnStay)) return true;
            return false;
        }

        /// <summary>Prefiks PartyBase.UpdateVisibilityAndInspected: w zajezdzie partia gracza zostaje niewidoczna (metoda gry pominieta tylko dla niej); bitwa, osada,
        /// partia nieaktywna albo klucz wylaczony - metoda gry jak dotad (straznik zdejmie stan). Inne partie - zawsze metoda gry.</summary>
        private static bool VisPrefix(PartyBase __instance)
        {
            if (!_hidden) return true;
            try
            {
                var mp = MobileParty.MainParty;
                if (mp == null || !ReferenceEquals(__instance, mp.Party)) return true;
                if (!HideOn || mp.MapEvent != null || mp.CurrentSettlement != null || !mp.IsActive) return true;
                if (mp.IsVisible) mp.IsVisible = false;
                _dHold++;
                return false;
            }
            catch (Exception e) { Stumble("VisPrefix", e); return true; }
        }

        /// <summary>Prefiks PartyNameplatesVM.UpdateMobilePartyVisibility: schowana partia gracza w zajezdzie - napis z imieniem zdjety jak przy wejsciu do osady
        /// (Clear + PlayerNameplate = null, wzor gry). Po wyjsciu gra stawia napis sama (partia widoczna - CreateNameplateFor).</summary>
        private static bool PlatePrefix(object __instance, MobileParty mobileParty)
        {
            if (!_hidden || _plateStrikes >= PlateStrikesMax || mobileParty == null) return true;
            try
            {
                if (!ReferenceEquals(mobileParty, MobileParty.MainParty) || mobileParty.IsVisible) return true;
                object cur = _plateProp.GetValue(__instance, null);
                if (cur != null)
                {
                    _plateClear.Invoke(cur, null);
                    _plateProp.SetValue(__instance, null, null);
                    _dPlate++;
                }
                _plateStrikes = 0;
                return false;
            }
            catch (Exception e)
            {
                _plateStrikes++;
                Stumble("PlatePrefix", e);
                if (_plateStrikes >= PlateStrikesMax) Log.Info(P + "napis gracza: " + PlateStrikesMax + " potkniecia z rzedu - napis zostaje jak dotad (ukrycie dziala dalej).");
                return true;
            }
        }

        // ------------------------------------------------------------ stan "w zajezdzie"
        /// <summary>Partia gracza moze byc schowana przy tym zajezdzie: aktywna, poza osada, bitwa, spotkaniem, oblezeniem, morzem, niewola i sluzba ROT.
        /// atInnCheck - menu zajazdu (partia przy zajezdzie, RoadsideInns.AtInn); sen sprawdza odleglosc sam (TownInns.RoadSleepUid).</summary>
        private static bool CanHide(RoadsideInns.Inn inn, bool atInnCheck, out string why)
        {
            why = null;
            var mp = MobileParty.MainParty;
            if (inn == null) { why = "brak zajazdu przy partii"; return false; }
            if (mp == null || !mp.IsActive) { why = "partia gracza nieaktywna"; return false; }
            if (mp.MapEvent != null) { why = "bitwa"; return false; }
            if (PlayerEncounter.Current != null) { why = "spotkanie"; return false; }
            if (mp.CurrentSettlement != null) { why = "w osadzie"; return false; }
            if (mp.SiegeEvent != null || mp.BesiegerCamp != null) { why = "oblezenie"; return false; }
            if (mp.IsCurrentlyAtSea) { why = "morze"; return false; }
            if (TaleWorlds.CampaignSystem.PlayerCaptivity.IsCaptive) { why = "niewola"; return false; }
            if (atInnCheck && !RoadsideInns.AtInn(inn)) { why = "partia z dala od zajazdu"; return false; }
            if (NightRest.EnlistedNow) { why = "sluzba ROT"; return false; }
            return true;
        }

        private static string MenuId()
        {
            var c = Campaign.Current;
            var mc = c != null ? c.CurrentMenuContext : null;
            var gm = mc != null ? mc.GameMenu : null;
            return gm != null ? gm.StringId : null;
        }

        /// <summary>Gracz jest teraz w zajezdzie: menu zajazdu albo sen oplacony w zajezdzie, plus CanHide. why - powod, gdy nie.</summary>
        private static bool InnNow(out RoadsideInns.Inn inn, out string why)
        {
            inn = null;
            string id = MenuId();
            if (id == null) { why = "menu zamkniete"; return false; }
            if (id == RoadsideInns.Menu)
            {
                inn = RoadsideInns.CurrentOrNear();
                return CanHide(inn, true, out why);
            }
            if (id == SleepMenu)
            {
                string uid = TownInns.RoadSleepUid();
                inn = uid != null ? RoadsideInns.Find(uid) : null;
                if (inn == null) { why = "sen poza zajazdem"; return false; }
                return CanHide(inn, false, out why);
            }
            why = "menu " + id;
            return false;
        }

        // ------------------------------------------------------------ ukrycie i powrot (tylko przy zmianie stanu)
        /// <summary>Partie armii prowadzonej przez gracza: widocznosc od nowa (ida za wodzem - CalculateVisibilityAndInspected). Zwraca liczbe partii.</summary>
        private static int Followers(MobileParty mp)
        {
            int n = 0;
            var army = mp != null ? mp.Army : null;
            if (army == null || army.LeaderParty != mp) return 0;
            foreach (var p in army.LeaderParty.AttachedParties)
            {
                if (p == null || p.Party == null) continue;
                p.Party.UpdateVisibilityAndInspected(mp.Position);
                n++;
            }
            return n;
        }

        private static void Hide(RoadsideInns.Inn inn, string how)
        {
            EnsurePatches();
            if (!_visPatched) return;   // bez latki gra pokazalaby partie przy pierwszym ticku czasu - nie przepychamy sie (linia startu mowi, czemu)
            var mp = MobileParty.MainParty;
            bool was = mp.IsVisible;
            _hidden = true; _hiddenUid = inn.Uid; _hiddenName = inn.Name; _stayReassert = 0;
            mp.IsVisible = false;   // setter gry: OnVisibilityChanged - wizerunek gasnie, napis (prefiks), zdarzenie widocznosci; raz
            int army = Followers(mp);
            _dHide++;
            Log.Info(P + "ukrycie partii gracza w " + inn.Name + " (" + inn.Uid + ") - " + how + "; byla widoczna " + (was ? "tak" : "nie") + ", teraz " + (mp.IsVisible ? "WIDOCZNA" : "schowana")
                     + ", partie armii za wodzem " + army + ", napis gracza " + (_platePatched ? "zdejmowany" : "zostaje (" + _plateHook + ")") + ".");
        }

        private static void Show(string why)
        {
            var mp = MobileParty.MainParty;
            string name = _hiddenName ?? "?";
            _hidden = false; _hiddenUid = _hiddenName = null; _stayReassert = 0;
            if (mp != null && mp.Party != null)
            {
                mp.Party.UpdateVisibilityAndInspected(mp.Position);   // prefiks juz przepuszcza - widocznosc liczy gra
                Followers(mp);
            }
            _dShow++;
            Log.Info(P + "partia gracza znow na mapie (" + name + ") - powod: " + why + "; widoczna " + (mp != null && mp.IsVisible ? "tak" : "nie") + ".");
        }

        /// <summary>"Ride on" w menu zajazdu - partia z powrotem przed zamknieciem menu.</summary>
        internal static void Leave(string why)
        {
            try { if (_hidden) Show(why); }
            catch (Exception e) { _hidden = false; Stumble("Leave", e); }
        }

        /// <summary>RoadsideInns.MenuInit: dzwiek karczmy (raz na menu - tlo juz grane nie startuje od nowa) i ukrycie partii przy wejsciu (takze po wczytaniu w menu).</summary>
        internal static void OnMenuInit(MenuCallbackArgs args, RoadsideInns.Inn inn)
        {
            try
            {
                if (inn == null) return;
                if (SoundOn) SetTavernSound(args, inn, true);
                string why;
                if (HideOn && !_hidden && CanHide(inn, true, out why)) Hide(inn, "wejscie (menu zajazdu)");
            }
            catch (Exception e) { Stumble("OnMenuInit", e); }
        }

        /// <summary>TownInns.RoadSleepInit: sen w zajezdzie - tlo karczmy (gdy menu go nie gra, np. po wczytaniu w trakcie snu) i partia schowana.</summary>
        internal static void OnSleepInit(MenuCallbackArgs args, string uid)
        {
            try
            {
                var inn = RoadsideInns.Find(uid);
                if (inn == null) return;
                if (SoundOn) SetTavernSound(args, inn, false);
                string why;
                if (HideOn && !_hidden && CanHide(inn, false, out why)) Hide(inn, "sen w zajezdzie");
            }
            catch (Exception e) { Stumble("OnSleepInit", e); }
        }

        private static void SetTavernSound(MenuCallbackArgs args, RoadsideInns.Inn inn, bool door)
        {
            var mc = args != null ? args.MenuContext : null;
            if (mc == null || mc.CurrentAmbientSoundID == AmbientTavern) return;
            mc.SetAmbientSound(AmbientTavern);
            if (door) mc.SetPanelSound(PanelTavern);
            _dSound++;
            Log.Info(P + "dzwiek karczmy w " + inn.Name + " (" + inn.Uid + "): tlo " + AmbientTavern + (door ? ", wejscie " + PanelTavern : " (sen)") + ".");
        }

        // ------------------------------------------------------------ straznik (SubModuleMain.OnApplicationTick)
        /// <summary>4 razy na sekunde (zegar rzeczywisty), takze przy zatrzymanym czasie: ukrycie, gdy gracz w zajezdzie, a partia nie schowana (wczytanie, menu
        /// oczekiwania bez init); powrot, gdy pobyt sie skonczyl (pobudka, bitwa, spotkanie, klucz wylaczony, menu zamkniete). Klucz wylaczony i nic schowanego - nic.
        /// Tylko na mapie (misja, ekwipunek - stan bez zmian do powrotu).</summary>
        internal static void AppTick()
        {
            if (!_hidden && !HideOn) return;
            long t = Stopwatch.GetTimestamp();
            if (t - _lastCheck < Stopwatch.Frequency / 4) return;
            _lastCheck = t;
            try
            {
                if (Campaign.Current == null || MobileParty.MainParty == null) { _hidden = false; _hiddenUid = _hiddenName = null; return; }   // poza kampania - stan tylko w pamieci
                var gsm = Game.Current != null ? Game.Current.GameStateManager : null;
                if (gsm == null || !(gsm.ActiveState is MapState)) return;
                RoadsideInns.Inn inn; string why;
                bool want = InnNow(out inn, out why);
                if (!HideOn) { want = false; why = "klucz wylaczony"; }
                if (want && !_hidden) Hide(inn, "straznik (menu " + MenuId() + ")");
                else if (!want && _hidden) Show(why);
                else if (want && _hidden && MobileParty.MainParty.IsVisible)
                {
                    // cos poza metoda widocznosci gry pokazalo partie (np. kod innego moda) - schowac jeszcze raz, najwyzej ReassertMax razy na pobyt
                    if (++_stayReassert > ReassertMax) { Show("inny kod pokazuje partie (" + ReassertMax + " razy w tym pobycie)"); return; }
                    _dReassert++;
                    MobileParty.MainParty.IsVisible = false;
                }
            }
            catch (Exception e) { Stumble("AppTick", e); }
        }

        // ------------------------------------------------------------ linie startu i doby
        private static string Keys()
        {
            var s = Settings.Current;
            return "klucze: Roadside Inn Hide Party " + (s != null && s.RoadsideInnHideParty ? "tak" : "WYLACZONE") + ", Roadside Inn Tavern Sound "
                   + (s != null && s.RoadsideInnTavernSound ? "tak" : "WYLACZONE") + " (Roadside Inns " + (s != null && s.RoadsideInns ? "tak" : "WYLACZONE") + ")";
        }

        /// <summary>RoadsideInns.SessionStart: latki (przy kluczu ukrycia; wylaczony - przy pierwszym ukryciu) i linia startu - co wpiete, co nie.</summary>
        internal static void SessionStart()
        {
            try
            {
                if (HideOn) EnsurePatches();
                Log.Info(P + "start sesji | " + Keys() + " | latka widocznosci: " + _visHook + " | latka napisu gracza: " + _plateHook
                         + " | ukrycie: MobileParty.IsVisible na czas menu zajazdu i snu w nim (jak mgla wojny), powrot przy wyjsciu, pobudce, bitwie, spotkaniu, wylaczeniu klucza"
                         + " | dzwiek: tlo " + AmbientTavern + ", wejscie " + PanelTavern + " (jak dzielnica karczmy miasta); obrazek: wait_mesh wsi okregu (gra nie ma obrazka karczmy)"
                         + " | teraz schowana " + (_hidden ? "tak" : "nie") + " | potkniecia " + _stumbles + ".");
            }
            catch (Exception e) { Stumble("SessionStart", e); }
        }

        /// <summary>RoadsideInns.Daily: linia doby pobytu (przy kluczu albo gdy cos sie dzialo). Zeruje liczniki doby.</summary>
        internal static void Daily(int day)
        {
            try
            {
                bool any = _dHide + _dShow + _dHold + _dReassert + _dPlate + _dSound + _dStumbles > 0;
                if (HideOn || SoundOn || any)
                    Log.Info(P + "dzien " + day + " | " + Keys() + " | ukrycia " + _dHide + ", powroty " + _dShow + ", przytrzymania (tick gry) " + _dHold + ", ponowne ukrycia " + _dReassert
                             + ", napis gracza zdjety " + _dPlate + ", dzwiek karczmy " + _dSound + " | teraz schowana " + (_hidden ? "tak (" + _hiddenName + ", " + _hiddenUid + ")" : "nie")
                             + " | latki: widocznosc " + (_visPatched ? "tak" : "nie") + ", napis " + (_platePatched ? (_plateStrikes >= PlateStrikesMax ? "zatrzymana po potknieciach" : "tak") : "nie")
                             + " | potkniecia dzis " + _dStumbles + " (od wczytania " + _stumbles + ").");
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally { _dHide = _dShow = _dHold = _dReassert = _dPlate = _dSound = _dStumbles = 0; }
        }
    }
}
