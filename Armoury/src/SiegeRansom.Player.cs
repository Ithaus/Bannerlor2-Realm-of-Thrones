using System;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// O2 - GRACZ-OBRONCA (klucz SiegeRansomPlayerDefender; off = regula AI i wiadomosc, jak w O1). Projekt rozdz. 3, 6.3, P11.
    ///  - Kto decyduje: gracz w oblezonej osadzie albo rod gracza jest jej panem. Propozycja przychodzi w chwili decyzji AI o szturmie (cykl O0), tylko gdy regula
    ///    daje okup (obronca moze zaplacic, AI-oblegajacy przyjmuje, bez wykluczen) - umowa z wyborem "czeka", postfiks decyzji trzyma szturm (false).
    ///  - Okno: InformationManager.ShowInquiry z pauza - "Pay" / "Refuse". Otwiera je (1) postfiks ticku menu oczekiwania gry menu_siege_strategies (gracz w osadzie
    ///    - gra daje mu to menu) albo (2) tick kampanii przy czynnym MapState (gracz na mapie, w armii, w menu innej osady), bez bitwy, rozmowy i niewoli gracza.
    ///    Wzor E7/R2 DistrictRaid i NightRest: flaga -> okno z ticku -> wywolanie okna ustawia TYLKO wybor -> wykonanie w nastepnym ticku kampanii (O1, Step/Execute).
    ///    ZADNEGO SwitchToMenu (CLAUDE.md 7): gracza w osadzie przelacza sama gra (FinalizeSiegeEvent -> PlayerSiege.OnSiegeEventFinalized -> "siege_attacker_left").
    ///    Okno zamkniete bez wyboru (cudzy HideInquiry) - otwierane znowu, najwyzej 3 razy; potem regula AI.
    ///  - "Pay" - okup wedlug reguli (te same zbiorniki i podzial co O1); "Refuse" - szturm przy nastepnym losowaniu gry; w tym oblezeniu okupu juz nie ma.
    ///  - Bez odpowiedzi przez 24 h (okna nie da sie pokazac: misja, bitwa, rozmowa, niewola) albo autotest (CrashScribe.Autotest.Active - nikt nie odpowie
    ///    na okno z pauza) - regula AI-obroncy (placi) i wiadomosc graczowi (P11).
    /// Linia na zdarzenie "Okup oblezenia (gracz)": strona, wybor, okup, po ilu godzinach. Wyjatek - licznik potkniec (nic sie nie gasi).
    /// </summary>
    internal static partial class SiegeRansom
    {
        internal static bool PlayerDefOn { get { var s = Settings.Current; return s != null && s.SiegeRansomPlayerDefender; } }

        private const int MaxAskFails = 3;
        private static Deal _askDeal;                 // okno gracza-obroncy otwarte dla tej umowy
        private static bool _askOpen;
        private static int _dAsk, _dShown, _dPayChosen, _dRefused, _dLate, _dAskFail;
        private static string _hookMenuTick = "nie zakladana";

        /// <summary>Decide (O1): gracz-obronca. null - regula AI. Odmowa w tym oblezeniu - szturm.</summary>
        private static string DecidePlayer(Siege s, Judge j)
        {
            var old = DealOf(s.Ev);
            if (old != null && old.Choice == 2) return "szturm (gracz-obronca odmowil w tym oblezeniu)";
            if (j.Player != "w osadzie" && j.Player != "pan osady") return null;
            if (!PlayerDefOn) return null;                                          // regula AI i wiadomosc (jak O1)
            var d = NewDeal(s, j, KDefPaid, 0);
            d.Deadline = Now + 1f;
            _dAsk++;
            return "pytanie gracza-obroncy (" + j.Player + ", do 24 h)";
        }

        /// <summary>Tick (O1): umowa czeka na gracza - termin 24 h albo okno z ticku kampanii (MapState).</summary>
        private static void StepAsk(Deal d)
        {
            if (d.Kind == KBes) return;                                             // O3: okno oblegajacego otwiera tylko menu oblezenia
            if (!PlayerDefOn) { Late(d, "klucz Siege Ransom Player Defender wylaczony"); return; }
            if (Now > d.Deadline) { Late(d, "24 h bez odpowiedzi (okna nie dalo sie pokazac)"); return; }
            TryAsk(d, false);
        }

        private static void Late(Deal d, string why)
        {
            if (d.Choice != 0) return;
            d.Kind = KDefLate; d.Choice = 1;
            _dLate++;
            if (ReferenceEquals(_askDeal, d)) { _askDeal = null; _askOpen = false; }
            ChoiceLine(d, "bez odpowiedzi - regula AI (" + why + ")");
        }

        private static void TryAsk(Deal d, bool fromMenu)
        {
            if (_askOpen)
            {
                if (InformationManager.IsAnyInquiryActive()) return;               // okno stoi (czasu i tak nie ma - pauza)
                _askOpen = false;                                                   // zamkniete bez wyboru (cudzy HideInquiry)
                var a = _askDeal; _askDeal = null;
                if (a != null && a.Choice == 0 && ++a.AskFails >= MaxAskFails) { _dAskFail++; Late(a, "okno zamkniete bez wyboru " + MaxAskFails + " razy"); return; }
            }
            if (d.Choice != 0 || InformationManager.IsAnyInquiryActive() || !CanShow(fromMenu)) return;
            ShowDefender(d);
        }

        private static bool CanShow(bool fromMenu)
        {
            if (AutotestActive()) return false;
            var main = MobileParty.MainParty;
            var me = Hero.MainHero;
            if (main == null || me == null || !me.IsAlive || me.IsPrisoner || main.MapEvent != null) return false;
            var ms = Game.Current != null && Game.Current.GameStateManager != null ? Game.Current.GameStateManager.ActiveState as MapState : null;
            if (ms == null) return false;
            var cm = Campaign.Current != null ? Campaign.Current.ConversationManager : null;
            if (cm != null && (cm.IsConversationInProgress || cm.IsConversationFlowActive)) return false;
            if (!fromMenu && PlayerEncounter.Current != null && main.CurrentSettlement == null) return false;   // spotkanie na mapie w toku
            return true;
        }

        private static void ShowDefender(Deal d)
        {
            var st = d.St; var j = d.J;
            string town = Nm(st);
            bool castle = !st.IsTown;
            string lead = j.Leader != null && j.Leader.LeaderHero != null ? j.Leader.LeaderHero.Name.ToString() : "The enemy";
            long folk = j.K.FolkSum;
            string title = "Ransom for " + town;
            string body = "The besiegers of " + town + " demand " + N(j.Ransom) + " denars from " + (castle ? "its treasury and castle folk" : "its merchants and townsfolk") + " to lift the siege.\n\n"
                          + (castle ? "The castle's treasury holds " + N(j.K.Kasa) + " denars and its folk " + N(folk) + "."
                                    : "The merchants hold " + N(j.K.Kasa) + " denars and the townsfolk " + N(folk) + ".")
                          + " " + lead + " has " + N(j.Men) + " men before the walls, and the walls will not hold for long.\n\n"
                          + "If they are paid, they leave and will not besiege " + town + " again for " + TruceDays + " days. If you refuse, they will storm the walls.";
            if (!d.Shown) _dShown++;
            d.Shown = true;
            _askDeal = d; _askOpen = true;
            Action pay = delegate
            {
                _askOpen = false; _askDeal = null;
                if (d.Choice != 0) return;
                d.Choice = 1; d.Kind = KDefPaid; _dPayChosen++;
                ChoiceLine(d, "zaplac");
            };
            Action refuse = delegate
            {
                _askOpen = false; _askDeal = null;
                if (d.Choice != 0) return;
                d.Choice = 2; _dRefused++;
                ChoiceLine(d, "odmowa");
                Log.Player("You refused the ransom. The besiegers of " + town + " will storm the walls.", true);
            };
            InformationManager.ShowInquiry(new InquiryData(title, body, true, true, "Pay", "Refuse", pay, refuse), true);
        }

        private static void ChoiceLine(Deal d, string choice)
        {
            try
            {
                if (!MeasureOn) return;
                Log.Info("Okup oblezenia (gracz): dzien " + Today + " | osada " + (d.St != null ? d.St.StringId : "?") + " | strona " + (d.Kind == KBes ? "oblegajacy" : "obronca")
                         + " | wybor " + choice + " | okup " + (d.J != null ? d.J.Ransom : 0) + " | po " + F((Now - d.Day0) * 24.0, "0.0") + " h | okno pokazane " + (d.Shown ? "tak" : "nie")
                         + ", zamkniete bez wyboru " + d.AskFails + ".");
            }
            catch { }
        }

        // ------------------------------------------------------------ tick menu oczekiwania gry (menu_siege_strategies)
        /// <summary>Postfiks SiegeEventCampaignBehavior.game_menu_siege_strategies_on_tick (G :259-278 - gra wola go co klatke czynnego menu oczekiwania oblezenia,
        /// po swoim ewentualnym przelaczeniu menu). Zwykla klatka: jedno porownanie. Tylko otwiera okno (gracz-obronca; O3 - gracz-oblegajacy). Nie rzuca.</summary>
        public static void MenuTickPost()
        {
            if (_deals.Count == 0 && !BesAskPending) return;
            try
            {
                for (int i = 0; i < _deals.Count; i++)
                {
                    var d = _deals[i];
                    if (d.Choice == 0 && d.Kind != KBes) { TryAsk(d, true); return; }
                }
                BesMenuTick();                                                      // O3
            }
            catch (Exception e) { _askOpen = false; _askDeal = null; Stumble("MenuTickPost", e); }
        }

        // ------------------------------------------------------------ autotest (CrashScribe.Autotest.Active - pole tylko w DLL autotestu)
        private static int _autotest = -1;

        private static bool AutotestActive()
        {
            if (_autotest >= 0) return _autotest == 1;
            _autotest = 0;
            try
            {
                foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (asm.GetName().Name != "CrashScribe") continue;
                    Type t = asm.GetType("CrashScribe.Autotest", false);
                    FieldInfo f = t != null ? t.GetField("Active", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public) : null;
                    if (f != null && f.FieldType == typeof(bool) && (bool)f.GetValue(null)) _autotest = 1;
                    break;
                }
            }
            catch { _autotest = 0; }
            return _autotest == 1;
        }

        // ------------------------------------------------------------ doba i wpiecia
        private static bool DayAnyPlayer { get { return _dAsk + _dShown + _dPayChosen + _dRefused + _dLate + _dAskFail > 0 || DayAnyBes; } }

        private static void DayPartPlayer(StringBuilder sb)
        {
            sb.Append(" | gracz-obronca: pytan ").Append(_dAsk).Append(", okno pokazane ").Append(_dShown).Append(", zaplac ").Append(_dPayChosen).Append(", odmowa ").Append(_dRefused)
              .Append(", bez odpowiedzi (regula AI) ").Append(_dLate).Append(" (okno zamkniete bez wyboru ").Append(_dAskFail).Append(')')
              .Append(" | latka ticku menu oblezenia: ").Append(_hookMenuTick);
            DayPartBes(sb);
        }

        private static void ZeroDayPlayer() { _dAsk = _dShown = _dPayChosen = _dRefused = _dLate = _dAskFail = 0; ZeroDayBes(); }

        private static void ResetPlayer() { _askDeal = null; _askOpen = false; ResetBes(); }

        private static void EnsureHooksPlayer()
        {
            try
            {
                var m = AccessTools.Method(typeof(SiegeEventCampaignBehavior), "game_menu_siege_strategies_on_tick");
                if (m == null) _hookMenuTick = "BRAK (game_menu_siege_strategies_on_tick nie znaleziona - okno tylko z ticku kampanii)";
                else
                {
                    Seal31.Patch("okup oblezenia: okno gracza (SiegeEventCampaignBehavior.game_menu_siege_strategies_on_tick)", m, typeof(SiegeRansom), null, nameof(MenuTickPost), null);
                    _hookMenuTick = "wpieta";
                }
            }
            catch (Exception e) { _hookMenuTick = "BRAK (blad: " + e.Message + ")"; Stumble("EnsureHooksPlayer", e); }
            EnsureHooksBes();
        }
    }
}
