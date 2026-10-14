using System;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// O3 - GRACZ-OBLEGAJACY (klucz SiegeRansomPlayerBesieger; off = stan O2: bez opcji, szturm jak w grze). Projekt rozdz. 3, 6.3, P6.
    ///  - Opcja "Demand a ransom" w menu oczekiwania gry menu_siege_strategies, wstawiona przed "Leave" (CampaignEvents.OnAfterSessionLaunchedEvent - menu gry juz
    ///    jest; drugi raz w tej samej kampanii nie dopisuje). Widoczna dla strony oblegajacej; czynna na tym samym warunku co "Lead an assault"
    ///    (SiegeEventCampaignBehavior :430-457: gracz dowodzi oblezeniem, przygotowania gotowe) plus regula okupu dla obroncy AI: stosunek MUROW (bez czlonu czasu
    ///    gry - recenzja 14.10) > 1 albo pusta warownia, okup = 1/4 monety >= doba zoldu armii gracza (P3), bez wykluczen (stolica, fabula ROT, Inni, bunt) i bez
    ///    "w tym oblezeniu bez umow" (BesWhyNot - jedno miejsce dla opcji i okna); inaczej nieczynna z dymkiem
    ///    (powod). Bez bitwy przy osadzie i bez spotkania gracza w toku. Warunek liczony raz na godzine gry (gra odswieza warunki menu co klatke - regula tylko z pamieci).
    ///  - Konsekwencja opcji ustawia TYLKO flage; okno (InformationManager.ShowInquiry z pauza: ile, czesc jego ludzi i jego, korona, rozejm; "Accept" / "Refuse")
    ///    otwiera postfiks ticku tego menu (wspolny z O2), wywolanie okna tylko zapisuje wybor (umowa KBes), wykonanie w nastepnym ticku kampanii (O1 Execute:
    ///    te same zbiorniki, podzial na druzyny obozu wedlug ludzi, rozejm, RemoveAllSiegeParties). ZADNEGO SwitchToMenu: po zwinieciu obozu gra sama zdejmuje
    ///    menu (GetGenericStateMenu bez obozu -> game_menu_siege_strategies_on_tick -> ExitToLast, :262-270) - ta sama droga co po odejsciu z obozu AI.
    ///  - Rozejm gracza (P6, jedna regula z AI): opcja gry "Besiege the town/castle" (game_menu_town_town_besiege_on_condition) nieczynna z dymkiem dla osady,
    ///    od ktorej frakcja gracza wziela okup w ostatnich TruceDays dobach.
    /// Linie "Okup oblezenia (gracz)" (strona oblegajacy: klik, przyjmij, odmowa) i O1 (rodzaj gracz-oblegajacy). Wyjatek - licznik potkniec.
    /// </summary>
    internal static partial class SiegeRansom
    {
        internal static bool PlayerBesOn { get { var s = Settings.Current; return s != null && s.SiegeRansomPlayerBesieger; } }

        private const string OptId = "arm_siege_ransom_demand";
        private static bool _besAsk, _besOpen;
        private static SiegeEvent _besEv;
        private static int _besFails;
        private static int _dBesClick, _dBesShown, _dBesAcc, _dBesRef, _dBesFail, _dBesBlocked, _dBesAssaultBlocked;
        private static string _hookOption = "nie dodana", _hookBesiege = "nie zakladana", _hookAssaultOpt = "nie zakladana";

        // regula dla opcji - pamiec na godzine gry (gra odswieza warunki menu oczekiwania co klatke)
        private static SiegeEvent _cEv;
        private static double _cHour = -1;
        private static Judge _cJ;
        private static bool _cValid;                  // pamiec wazna takze dla null (blad Evaluate) - inaczej wyjatek co klatke (recenzja 14.10)

        private static bool BesAskPending { get { return _besAsk; } }

        private static Judge BesJudge(BesiegerCamp camp)
        {
            var ev = camp.SiegeEvent;
            double h = CampaignTime.Now.ToHours;
            if (_cValid && ReferenceEquals(_cEv, ev) && h - _cHour < 1.0 && h >= _cHour) return _cJ;
            _cJ = Evaluate(camp, StateOf(ev, true));
            _cEv = ev; _cHour = h; _cValid = true;
            return _cJ;
        }

        // ------------------------------------------------------------ opcja menu (OnAfterSessionLaunched)
        /// <summary>CampaignEvents.OnAfterSessionLaunchedEvent (ArmouryBehavior): opcja "Demand a ransom" przed "Leave" w menu_siege_strategies. Nie rzuca.</summary>
        internal static void AddMenus(CampaignGameStarter starter)
        {
            try
            {
                var mgr = Campaign.Current != null ? Campaign.Current.GameMenuManager : null;
                var menu = mgr != null ? mgr.GetGameMenu("menu_siege_strategies") : null;
                if (starter == null || menu == null) { _hookOption = "BRAK (menu_siege_strategies nie znalezione)"; return; }
                int idx = -1, i = 0;
                foreach (var o in menu.MenuOptions)
                {
                    if (o != null && o.IdString == OptId) { _hookOption = "dodana (juz byla)"; return; }
                    if (o != null && o.IdString == "menu_siege_strategies_leave" && idx < 0) idx = i;
                    i++;
                }
                starter.AddGameMenuOption("menu_siege_strategies", OptId, "{=!}Demand a ransom", OptCondition, OptConsequence, false, idx);
                _hookOption = "dodana (pozycja " + idx + ")";
            }
            catch (Exception e) { _hookOption = "BRAK (blad: " + e.Message + ")"; Stumble("AddMenus", e); }
        }

        private static bool OptCondition(MenuCallbackArgs args)
        {
            try
            {
                args.optionLeaveType = GameMenuOption.LeaveType.Ransom;
                if (!PlayerBesOn || !Live) return false;
                var main = MobileParty.MainParty;
                var camp = main != null ? main.BesiegerCamp : null;
                var ev = camp != null ? camp.SiegeEvent : null;
                if (ev == null || ev.BesiegedSettlement == null || ev.BesiegedSettlement.Town == null || PlayerSiege.PlayerSide != BattleSideEnum.Attacker) return false;
                var st = ev.BesiegedSettlement;
                string town = Nm(st);
                if (Campaign.Current.Models.EncounterModel.GetLeaderOfSiegeEvent(ev, BattleSideEnum.Attacker) != Hero.MainHero) return Off(args, "You are not in command of this siege.");
                if (!camp.IsPreparationComplete) return Off(args, "You need to wait for the siege equipment to be prepared.");
                if (DealOf(ev) != null) return Off(args, "The people of " + town + " are gathering the coin.");
                if (main.MapEvent != null || (st.Party != null && st.Party.MapEvent != null)) return Off(args, "You cannot treat with them while there is fighting.");
                if (PlayerEncounter.Current != null) return Off(args, "Your men are still caught up in the last engagement - you cannot send envoys yet.");
                var j = BesJudge(camp);
                string no = BesWhyNot(ev, j, town);
                if (no != null) return Off(args, no);
                args.Tooltip = new TextObject("{=!}" + "The people of " + town + " would pay about " + N(j.Ransom) + " denars to see your army gone.");
                return true;
            }
            catch (Exception e) { Stumble("OptCondition", e); return false; }
        }

        /// <summary>Powod (po angielsku), dla ktorego obronca nie zaplaci graczowi - ta sama regula co obronca AI (Judge.DefPays: wykluczenia, oboz bez zywych,
        /// stosunek murow bez czlonu czasu, okup >= doba zoldu) plus "w tym oblezeniu bez umow". null - zaplaci. Jedno miejsce dla opcji i okna (recenzja 14.10:
        /// dotad opcja byla czynna przy buncie, a klik nic nie dawal).</summary>
        private static string BesWhyNot(SiegeEvent ev, Judge j, string town)
        {
            if (j == null) return "No one in " + town + " will speak to you now.";
            var s = StateOf(ev, false);
            if (s != null && s.NoDeal != null) return "No one in " + town + " will treat with you again during this siege.";
            if (j.Capital) return town + " is the seat of a crown - they will fight for it, not pay.";
            if (j.Story) return "No ransom will be offered at " + town + ": this siege will be decided by the sword.";
            if (j.Undead) return "There is no bargaining with the dead.";
            if (j.Rebel) return town + " is in revolt - there is no one to pay you.";
            if (j.Excl != null) return "No ransom will be offered at " + town + ".";
            if (j.Men <= 0) return "There is no one in your camp to take the coin.";
            if (!j.Empty && j.RatioWalls <= 1.0) return "Their walls still hold - they will not pay. (Your strength against what the walls make of the defenders: " + F(j.RatioWalls, "0.00") + ".)";
            if (j.Ransom <= 0 || j.Ransom < Math.Max(1L, j.Wage))
                return town + " holds too little coin to buy you off: a quarter of it is " + N(j.Ransom) + " denars, less than a day's pay of your army (" + N(j.Wage) + ").";
            return null;
        }

        private static bool Off(MenuCallbackArgs args, string why)
        {
            args.IsEnabled = false;
            args.Tooltip = new TextObject("{=!}" + why);
            return true;
        }

        /// <summary>Konsekwencja opcji: TYLKO flaga (okno otwiera tick menu - CLAUDE.md 7). Nie rzuca.</summary>
        private static void OptConsequence(MenuCallbackArgs args)
        {
            try
            {
                var main = MobileParty.MainParty;
                var ev = main != null && main.BesiegerCamp != null ? main.BesiegerCamp.SiegeEvent : null;
                if (ev == null) return;
                _besAsk = true; _besEv = ev; _besFails = 0; _besOpen = false;
                _dBesClick++;
            }
            catch (Exception e) { Stumble("OptConsequence", e); }
        }

        // ------------------------------------------------------------ okno z ticku menu (wspolny postfiks z O2)
        private static void BesMenuTick()
        {
            if (!_besAsk) return;
            if (_besOpen)
            {
                if (InformationManager.IsAnyInquiryActive()) return;
                _besOpen = false;                                                   // zamkniete bez wyboru
                if (++_besFails >= MaxAskFails) { _besAsk = false; _dBesFail++; return; }
            }
            var main = MobileParty.MainParty;
            var camp = main != null ? main.BesiegerCamp : null;
            if (!PlayerBesOn || camp == null || camp.SiegeEvent != _besEv || DealOf(_besEv) != null) { _besAsk = false; return; }   // oblezenie inne albo skonczone
            if (InformationManager.IsAnyInquiryActive() || !CanShow(true)) return;
            _cValid = false;                                                        // regula swieza przy oknie
            var j = BesJudge(camp);
            string no = BesWhyNot(_besEv, j, Nm(_besEv.BesiegedSettlement));
            if (no != null) { _besAsk = false; Log.Player(no, true); return; }      // stan zmienil sie od kliku - powod zamiast ciszy
            ShowBesieger(camp, j);
        }

        private static void ShowBesieger(BesiegerCamp camp, Judge j)
        {
            var st = j.St;
            string town = Nm(st);
            // czesc druzyny gracza wedlug ludzi obozu (ten sam podzial co wykonanie - SharePool), przyblizenie przed zdjeciem
            long mine = 0, menPart = 0, leadPart = 0, crown = 0;
            try
            {
                int myMen = MobileParty.MainParty.MemberRoster != null ? MobileParty.MainParty.MemberRoster.TotalManCount : 0;
                if (j.Men > 0) mine = j.Ransom * myMen / j.Men;
                if (MenPurse.On) { leadPart = mine / 3; menPart = mine - leadPart; } else { leadPart = mine; menPart = 0; }
                crown = CitySack.CrownPart(Hero.MainHero, leadPart);
            }
            catch { }
            string body = "The people of " + town + " offer " + N(j.Ransom) + " denars " + (st.IsTown ? "from their merchants and townsfolk" : "from the castle's treasury and its folk")
                          + " to see your army gone.\n\n"
                          + (j.Parties > 1 ? "It is shared among all the parties of your camp by their men. " : "")
                          + (menPart > 0 ? "Your men would get " + N(menPart) + ", you " + N(leadPart) : "You would get " + N(leadPart))
                          + (crown > 0 ? " (the crown will claim " + N(crown) + " of it)" : "") + ".\n\n"
                          + "Take the ransom and lift the siege? You would not besiege " + town + " again for " + TruceDays + " days.";
            _besOpen = true;
            _dBesShown++;
            var ev = camp.SiegeEvent;
            Action accept = delegate
            {
                _besOpen = false; _besAsk = false;
                var s = StateOf(ev, true);
                if (s == null || DealOf(ev) != null || s.St == null || s.St.SiegeEvent != ev) return;
                var d = NewDeal(s, j, KBes, 1);
                _dBesAcc++;
                ChoiceLine(d, "przyjmij");
                Log.Player("The people of " + town + " are bringing out the coin. Let time run on, and your army will lift the siege once it is paid.");
            };
            Action refuse = delegate
            {
                _besOpen = false; _besAsk = false;
                _dBesRef++;
                var tmp = new Deal { Ev = ev, St = st, J = j, Kind = KBes, Day0 = Now };
                ChoiceLine(tmp, "odmowa");
            };
            InformationManager.ShowInquiry(new InquiryData("Ransom of " + town, body, true, true, "Accept", "Refuse", accept, refuse), true);
        }

        /// <summary>Messages (O1): okup wziety przez gracza-oblegajacego - wiadomosc z czescia jego druzyny (napis jak L2).</summary>
        private static void BesiegerDone(Deal d, Settlement st, long pool, long[] took, CitySack.Rec pr)
        {
            long men = pr != null ? pr.ToMen : 0, lead = pr != null ? pr.ToLead : 0;
            long crown = pr != null && pr.Lead != null ? CitySack.CrownPart(pr.Lead, lead) : 0;
            Log.Player("You took a ransom of " + N(pool) + " denars from " + Nm(st) + " and lifted the siege. " + CitySack.PartText(men, lead, crown)
                       + " You will not besiege " + Nm(st) + " again for " + TruceDays + " days.");
        }

        // ------------------------------------------------------------ rozejm gracza: opcja gry "Besiege the town/castle"
        /// <summary>Postfiks EncounterGameMenuBehavior.game_menu_town_town_besiege_on_condition: rozejm frakcji albo rodu gracza z ta osada - opcja nieczynna z dymkiem. Nie rzuca.</summary>
        public static void BesiegeCondPost(MenuCallbackArgs __0, ref bool __result)
        {
            if (!__result || _truce.Count == 0) return;
            try
            {
                if (!ActOn || __0 == null || Hero.MainHero == null) return;
                var st = Settlement.CurrentSettlement;
                var f = Hero.MainHero.MapFaction;
                if (st == null || !IsTruce(st, f, Clan.PlayerClan)) return;
                int left = Math.Max(1, TruceUntil(st, f, Clan.PlayerClan) - Today);
                __0.IsEnabled = false;
                __0.Tooltip = new TextObject("{=!}" + Nm(st) + " paid a ransom to your banner: you swore not to besiege it again for another " + left + (left == 1 ? " day." : " days."));
                _dBesBlocked++;
            }
            catch (Exception e) { Stumble("BesiegeCondPost", e); }
        }

        // ------------------------------------------------------------ szturm gracza po "Accept"
        /// <summary>Postfiks SiegeEventCampaignBehavior.game_menu_siege_strategies_lead_assault_on_condition i ..._order_assault_on_condition (recenzja 14.10): po
        /// przyjeciu okupu ("Accept", umowa w toku) "Lead an assault" i "Send troops" nieczynne z dymkiem - szturm miasta, ktore placi, zerwalby umowe (i tak
        /// przepada po szturmie - OnMapEventEnded). Tylko IsEnabled i dymek (zadnej zmiany menu). Zwykle: jedno porownanie. Nie rzuca.</summary>
        public static void AssaultOptPost(MenuCallbackArgs __0, ref bool __result)
        {
            if (!__result || _deals.Count == 0) return;
            try
            {
                var main = MobileParty.MainParty;
                var ev = main != null && main.BesiegerCamp != null ? main.BesiegerCamp.SiegeEvent : null;
                var d = ev != null ? DealOf(ev) : null;
                if (d == null || d.Kind != KBes || d.Choice != 1 || __0 == null) return;
                __0.IsEnabled = false;
                __0.Tooltip = new TextObject("{=!}You have taken the ransom of " + Nm(d.St) + ". Let time run on - your army will lift the siege once the coin is handed over.");
                _dBesAssaultBlocked++;
            }
            catch (Exception e) { Stumble("AssaultOptPost", e); }
        }

        // ------------------------------------------------------------ doba i wpiecia
        private static bool DayAnyBes { get { return _dBesClick + _dBesShown + _dBesAcc + _dBesRef + _dBesFail + _dBesBlocked + _dBesAssaultBlocked > 0; } }

        private static void DayPartBes(StringBuilder sb)
        {
            sb.Append(" | gracz-oblegajacy: opcja ").Append(_hookOption).Append(", klikniec ").Append(_dBesClick).Append(", okno ").Append(_dBesShown).Append(", przyjmij ").Append(_dBesAcc)
              .Append(", odmowa ").Append(_dBesRef).Append(", okno zamkniete bez wyboru ").Append(_dBesFail).Append("; rozejm gracza - oblezenie nieczynne ").Append(_dBesBlocked)
              .Append(" (latka ").Append(_hookBesiege).Append(')')
              .Append("; szturm po Accept nieczynny (klatki menu) ").Append(_dBesAssaultBlocked).Append(" (latka ").Append(_hookAssaultOpt).Append(')');
        }

        private static void ZeroDayBes() { _dBesClick = _dBesShown = _dBesAcc = _dBesRef = _dBesFail = _dBesBlocked = _dBesAssaultBlocked = 0; }

        private static void ResetBes() { _besAsk = _besOpen = false; _besEv = null; _besFails = 0; _cEv = null; _cJ = null; _cHour = -1; _cValid = false; }

        private static void EnsureHooksBes()
        {
            try
            {
                var m = AccessTools.Method(typeof(EncounterGameMenuBehavior), "game_menu_town_town_besiege_on_condition");
                if (m == null) _hookBesiege = "BRAK (game_menu_town_town_besiege_on_condition nie znaleziona)";
                else
                {
                    Seal31.Patch("okup oblezenia: rozejm gracza (EncounterGameMenuBehavior.game_menu_town_town_besiege_on_condition)", m, typeof(SiegeRansom), null, nameof(BesiegeCondPost), null,
                                 Priority.Normal, Priority.Last);
                    _hookBesiege = "wpieta";
                }
            }
            catch (Exception e) { _hookBesiege = "BRAK (blad: " + e.Message + ")"; Stumble("EnsureHooksBes", e); }
            try
            {
                var wired = 0;
                foreach (var name in new[] { "game_menu_siege_strategies_lead_assault_on_condition", "game_menu_siege_strategies_order_assault_on_condition" })
                {
                    var m = AccessTools.Method(typeof(SiegeEventCampaignBehavior), name);
                    if (m == null) continue;
                    Seal31.Patch("okup oblezenia: szturm po przyjeciu okupu (SiegeEventCampaignBehavior." + name + ")", m, typeof(SiegeRansom), null, nameof(AssaultOptPost), null,
                                 Priority.Normal, Priority.Last);
                    wired++;
                }
                _hookAssaultOpt = wired == 2 ? "wpieta" : wired == 1 ? "wpieta czesciowo (1 z 2)" : "BRAK (warunki szturmu gry nie znalezione)";
            }
            catch (Exception e) { _hookAssaultOpt = "BRAK (blad: " + e.Message + ")"; Stumble("EnsureHooksBes.Assault", e); }
        }
    }
}
