using System;
using System.Collections.Generic;
using System.Globalization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 385d (Jeff 11.11 "tak, rob wszystko 2-4", pkt 2 "poslaniec do wladcy"): POSLANIEC DO WROGIEGO WLADCY - bezpieczny rdzen v1. Klucz PrisonerTermsEnvoy.
    /// Zasada: zadnego drugiego interfejsu oferty - poslaniec dociera, a gracz "mowi przez niego" tym samym drzewem 384 w rozmowie NA MAPIE (bez misji,
    /// CampaignMapConversation.OpenConversation - ta sama droga co NoticeBoard.TalkCons) z postacia wladcy. Trzeci tryb Talk() obok Parley / Field.
    /// WYSLANIE (opcje ZWYKLYCH menu, okno wyboru 1 z wrogich wladcow z czasem jazdy): "request_meeting_with_besiegers" (gracz w oblezonej twierdzy - dziala
    ///   takze, gdy 384c ukrylo wodza bez bohatera), "town_keep" i "castle" w warowni rodu gracza. Jeden poslaniec naraz; bez oplaty (zamknieta ekonomia: nie ma
    ///   odbiorcy zlota - kosztem jest czas). Czas: odleglosc prosta do wladcy / 2.5 na godzine (miara RescueHunt.RiderPerHour), 2..240 h.
    /// DOTARCIE (takt kampanii): wladca martwy / jeniec / pokoj -> poslaniec wraca; wladca w bitwie -> czeka do 24 h. Potem stan "czeka na posluchanie" 48 h -
    ///   komunikat i opcja "Hear your envoy" w zwyklych menu (town, castle, village, town_keep, request_meeting_with_besiegers). Rozmowa otwierana TYLKO z opcji
    ///   zwyklego menu (nigdy z okna ani z taktu: CampaignEvents.TickEvent stoi przy pauzie, a OpenMapConversation rzutuje ActiveState na MapState bez kontroli).
    /// ROZMOWA: _envTalk (flaga na czas tej jednej rozmowy; zdejmowana w ConversationEnded i zapasowo w takcie) + wlasna linia "start" o priorytecie 320 ->
    ///   arm384_want (Begin w konsekwencji). Zadania: A (pakt - wladca = klucz frakcji), M (moi ludzie i zolnierze - wladca ma zasieg calej frakcji), T (rozejm
    ///   krolestw gracza-wladcy), L / A z odstapieniem tylko z oblezonej twierdzy rodu gracza, z ktorej poslaniec wyjechal (to samo oblezenie, oblega frakcja
    ///   wladcy; Run nie wymaga, by przysiegajacy stal w obozie). P (swobodne przejscie) - NIE (wymaga menu przy bramie). Wszystkie wyjscia do "lord_pretalk"
    ///   maja w tym trybie blizniaki do "close_window" (rozmowa bez spotkania - vanillowe opcje wrogiego lorda nie dla niej).
    /// WYKONANIE: jak kazda umowa 384 w kolejce; Deal.NotBefore = powrot poslanca (ten sam czas jazdy), termin 3 dob liczony od powrotu.
    /// Jedna audiencja na poslanca: po rozmowie (takze "Never mind") poslaniec wraca. Zapis "arm_env385d". ODLOZONE: opcja w oknie krolestwa, poslaniec do
    ///   zwyklych lordow, "Ask for the ruler" w scenie obozu, okno "teraz / pozniej" przy dotarciu, przechwycenie poslanca.
    /// </summary>
    internal static partial class PrisonerTerms384
    {
        private const string EnvoyCool = "@e";
        private const double EnvoyWaitH = 48.0, EnvoyBattleH = 24.0, EnvoyMinH = 2.0, EnvoyMaxH = 240.0, EnvoyPerHour = 2.5;
        private static bool KEnvoy { get { var s = Settings.Current; return s != null && s.PrisonerExchangeForTerms && s.PrisonerTermsEnvoy; } }

        private static string _envHero = "", _envSt = "";
        private static double _envSiegeStart, _envArrive, _envUntil, _envHours;
        private static char _envState = '\0';   // R - w drodze, W - stoi przed wladca i czeka na posluchanie
        private static bool _envTalk;           // rozmowa przez poslanca w toku (nie w zapisie)
        private static int _dEnvSent, _dEnvHeard, _dEnvBack;

        private static void EnvoyReset()
        {
            _envHero = ""; _envSt = ""; _envSiegeStart = 0; _envArrive = 0; _envUntil = 0; _envHours = 0; _envState = '\0'; _envTalk = false; _dEnvSent = 0; _dEnvHeard = 0; _dEnvBack = 0;
        }

        private static void EnvoyClear() { _envHero = ""; _envSt = ""; _envSiegeStart = 0; _envArrive = 0; _envUntil = 0; _envHours = 0; _envState = '\0'; }

        private static double NowH { get { return CampaignTime.Now.ToHours; } }

        /// <summary>Wladca, do ktorego wolno slac / z ktorym wolno mowic przez poslanca: zyje, dorosly, nie jeniec, wojna z frakcja gracza, nie wykluczony (374).</summary>
        private static bool EnvoyValid(Hero r)
        {
            try
            {
                var me = Hero.MainHero;
                if (r == null || me == null || r == me || !r.IsAlive || r.IsChild || r.IsPrisoner || r.StringId == null || r.MapFaction == null || me.MapFaction == null) return false;
                if (r.MapFaction.Leader != r || SiegeSurrender374.Excluded(r)) return false;
                return FactionManager.IsAtWarAgainstFaction(r.MapFaction, me.MapFaction);
            }
            catch { return false; }
        }

        private static bool EnvoyWhere(Hero r, out Vec2 p)
        {
            p = default(Vec2);
            var mp = r.PartyBelongedTo;
            if (mp != null && mp.IsActive) { p = mp.GetPosition2D; return true; }
            var s = r.CurrentSettlement;
            if (s != null) { p = s.GetPosition2D; return true; }
            return false;
        }

        private static double EnvoyHours(Hero r)
        {
            Vec2 p;
            var main = MobileParty.MainParty;
            if (main == null || !EnvoyWhere(r, out p)) return -1;
            double d = p.Distance(main.GetPosition2D);
            return Math.Max(EnvoyMinH, Math.Min(EnvoyMaxH, d / EnvoyPerHour));
        }

        private static string RideText(double h) { return h < 24 ? Math.Max(1, (int)Math.Round(h)) + " hours' ride" : (h / 24.0).ToString("0.#", Inv) + " days' ride"; }

        // ------------------------------------------------------------ rozmowa
        /// <summary>Linia "start" poslanca (priorytet 320 - nad 310 NoticeBoard.Watch i 300 gry; warunek to jedno porownanie flagi dla kazdej innej rozmowy)
        /// i blizniaki wyjsc: w trybie poslanca "close_window" zamiast "lord_pretalk".</summary>
        private static void EnvoyDialogs(CampaignGameStarter starter)
        {
            try
            {
                starter.AddDialogLine("arm385d_start", "start", "arm384_want", "{=!}{ARM385D_START}", EnvoyStartCond, Begin, 320);
                starter.AddPlayerLine("arm385d_w_back", "arm384_want", "close_window", "{=!}Nothing, after all. My envoy takes his leave.", () => _envTalk, ClearTalk, 100);
                starter.AddPlayerLine("arm385d_o_back", "arm384_offer", "close_window", "{=!}Never mind. My envoy takes his leave.", () => _envTalk, ClearTalk, 100);
                starter.AddDialogLine("arm385d_no", "arm384_answer", "close_window", "{=!}{ARM384_NO}", () => _envTalk && _verdict != 'Y' && _verdict != 'K', NoCons, 198);
                starter.AddPlayerLine("arm385d_c_no", "arm384_counter", "close_window", "{=!}No. That is my last word.", () => _envTalk, LastWord, 100);
            }
            catch (Exception e) { Stumble("EnvoyDialogs", e); }
        }

        private static bool EnvoyStartCond()
        {
            if (!_envTalk) return false;
            try
            {
                var h = Hero.OneToOneConversationHero;
                if (h == null || h.StringId == null || h.StringId != _envHero || !On) return false;
                string hint = "";
                int honor = Trait(h, DefaultTraits.Honor);
                if (SiegeSurrender374.IsLiar(h) || honor < 0) hint = " (Men say his word is worth little.)";
                else if (honor > 0) hint = " (Men say he keeps his word.)";
                MBTextManager.SetTextVariable("ARM385D_START", "Your envoy has leave to speak. What does your lord want of me?" + hint);
                return true;
            }
            catch (Exception e) { Stumble("EnvoyStartCond", e); return false; }
        }

        /// <summary>Trzeci tryb Talk(): rozmowa przez poslanca. st - oblezona twierdza, z ktorej wyjechal (to samo oblezenie, oblega frakcja wladcy, twierdza
        /// rodu gracza albo gracz w niej stoi) - wtedy umowa osady (L / A z odstapieniem); inaczej field = true (umowa bez osady).</summary>
        private static bool EnvoyTalk(out Hero h, out Settlement st, out bool field)
        {
            h = null; st = null; field = false;
            if (!_envTalk) return false;
            var x = Hero.OneToOneConversationHero;
            if (x == null || x.StringId == null || x.StringId != _envHero || !EnvoyValid(x)) return false;
            var mp = MobileParty.MainParty;
            if (mp == null || mp.MapEvent != null) return false;
            h = x;
            var s = _envSt.Length > 0 ? Settlement.Find(_envSt) : null;
            var ev = s != null ? s.SiegeEvent : null;
            var lp = ev != null && ev.BesiegerCamp != null ? ev.BesiegerCamp.LeaderParty : null;
            if (ev != null && lp != null && s.IsFortification && s.Town != null && Math.Abs(ev.SiegeStartTime.ToHours - _envSiegeStart) < 0.05 && lp.MapFaction == h.MapFaction
                && (s.OwnerClan == Clan.PlayerClan || mp.CurrentSettlement == s) && (s.Party == null || s.Party.MapEvent == null) && mp.BesiegerCamp == null
                && !Pending(s) && !SiegeSurrender374.Pending(s))   // druga umowa o te sama twierdze (384 / 374) w toku - tylko umowa bez osady
                st = s;
            else field = true;
            return true;
        }

        private static void EnvoyRefused(Hero h)
        {
            if (!_envTalk || h == null || h.StringId == null) return;
            _cool[EnvoyCool + "|" + h.StringId] = new Cool { Start = 0, End = Today + CooldownDays };
        }

        /// <summary>Doba, od ktorej wolno wykonac umowe zawarta przez poslanca (powrot - ten sam czas jazdy).</summary>
        private static double EnvoyBackDay() { return Today + Math.Max(EnvoyMinH, _envHours) / 24.0; }

        /// <summary>Nasluch CampaignEvents.ConversationEnded: koniec rozmowy przez poslanca - flaga zdjeta, poslaniec wraca (jedna audiencja).</summary>
        internal static void OnConversationEnded(IEnumerable<CharacterObject> characters)
        {
            if (!_envTalk) return;
            try
            {
                _envTalk = false;
                var r = Hero.Find(_envHero);
                Log.Info(Tag + ": dzien " + F(Today) + " | poslaniec: rozmowa z " + Name(r) + " skonczona - poslaniec wraca (" + F(_envHours) + " h jazdy)" + (_queue.Exists(q => q.Envoy && q.Lord == _envHero) ? " z umowa (wykonanie po powrocie)" : " bez umowy") + ".");
                EnvoyClear();
                ClearTalk();
            }
            catch (Exception e) { _envTalk = false; Stumble("EnvoyEnded", e); }
        }

        // ------------------------------------------------------------ menu: wyslanie i posluchanie
        private static void EnvoyMenus(CampaignGameStarter starter)
        {
            // kazda opcja osobno - brak jednego menu nie zabiera pozostalych
            int n = 0;
            try { starter.AddGameMenuOption("request_meeting_with_besiegers", "arm385d_send_siege", "{=!}Send an envoy to a hostile ruler", EnvoySendSiegeCond, EnvoySendCons, false, 1); n++; } catch (Exception e) { Stumble("EnvoyMenus.siege", e); }
            try { starter.AddGameMenuOption("town_keep", "arm385d_send_keep", "{=!}Send an envoy to a hostile ruler", EnvoySendHomeCond, EnvoySendCons, false, 1); n++; } catch (Exception e) { Stumble("EnvoyMenus.keep", e); }
            try { starter.AddGameMenuOption("castle", "arm385d_send_castle", "{=!}Send an envoy to a hostile ruler", EnvoySendHomeCond, EnvoySendCons, false, 1); n++; } catch (Exception e) { Stumble("EnvoyMenus.castle", e); }
            foreach (var m in new[] { "request_meeting_with_besiegers", "town", "town_keep", "castle", "village" })
            {
                try { starter.AddGameMenuOption(m, "arm385d_hear_" + m, "{=!}{ARM385D_HEAR}", EnvoyHearCond, EnvoyHearCons, false, 1); n++; } catch (Exception e) { Stumble("EnvoyMenus.hear." + m, e); }
            }
            try { Log.Info(Tag + ": poslaniec (385d) - opcje menu wpiete " + n + " z 8 (wyslanie: request_meeting_with_besiegers, town_keep, castle; posluchanie: + town, village), klucz " + (KEnvoy ? "TAK" : "NIE") + (_envState != ' ' ? " | poslaniec w zapisie: stan " + _envState : "") + "."); } catch { }
        }

        private static bool EnvoySendBase(MenuCallbackArgs args)
        {
            if (!KEnvoy || !On || _envState != '\0' || _envTalk) return false;
            var me = Hero.MainHero; var mp = MobileParty.MainParty;
            if (me == null || mp == null || me.IsPrisoner || me.MapFaction == null || mp.MapEvent != null || mp.BesiegerCamp != null) return false;
            args.optionLeaveType = GameMenuOption.LeaveType.Mission;
            return true;
        }

        private static bool EnvoySendSiegeCond(MenuCallbackArgs args)
        {
            try
            {
                var st = Settlement.CurrentSettlement;
                if (st == null || !st.IsFortification || st.SiegeEvent == null) return false;
                if (!EnvoySendBase(args)) return false;
                return EnvoyRulers().Count > 0;
            }
            catch (Exception e) { Stumble("EnvoySendSiegeCond", e); return false; }
        }

        private static bool EnvoySendHomeCond(MenuCallbackArgs args)
        {
            try
            {
                var st = Settlement.CurrentSettlement;
                if (st == null || !st.IsFortification || st.SiegeEvent != null || st.OwnerClan != Clan.PlayerClan) return false;
                if (!EnvoySendBase(args)) return false;
                return EnvoyRulers().Count > 0;
            }
            catch (Exception e) { Stumble("EnvoySendHomeCond", e); return false; }
        }

        /// <summary>Wladcy krolestw w wojnie z frakcja gracza, do ktorych da sie dojechac (znane miejsce pobytu).</summary>
        private static List<Hero> EnvoyRulers()
        {
            var list = new List<Hero>();
            var mf = Hero.MainHero != null ? Hero.MainHero.MapFaction : null;
            if (mf == null) return list;
            foreach (var k in Kingdom.All)
            {
                if (k == null || k.IsEliminated || k == mf) continue;
                var r = k.Leader;
                Vec2 p;
                if (EnvoyValid(r) && EnvoyWhere(r, out p) && !list.Contains(r)) list.Add(r);
            }
            return list;
        }

        private static void EnvoySendCons(MenuCallbackArgs args)
        {
            try
            {
                if (_envState != ' ') { Log.Player("Your envoy is already on the road."); return; }   // menu nieodswiezone po wyslaniu (bez MenuContext.Refresh - przebudowa menu osady z okna nieprobowana)
                var st = Settlement.CurrentSettlement;
                var siegeSt = st != null && st.SiegeEvent != null ? st : null;
                var siegeF = siegeSt != null && siegeSt.SiegeEvent.BesiegerCamp != null && siegeSt.SiegeEvent.BesiegerCamp.LeaderParty != null ? siegeSt.SiegeEvent.BesiegerCamp.LeaderParty.MapFaction : null;
                var rulers = EnvoyRulers();
                rulers.Sort((a, b) => (b.MapFaction == siegeF ? 1 : 0).CompareTo(a.MapFaction == siegeF ? 1 : 0));   // wladca oblegajacych pierwszy
                var rows = new List<InquiryElement>();
                foreach (var r in rulers)
                {
                    double h = EnvoyHours(r);
                    if (h < 0) continue;
                    string no = null;
                    Cool c;
                    if (_cool.TryGetValue(EnvoyCool + "|" + r.StringId, out c) && c.End > Today) no = "He has turned your terms down already. You may try again in " + Math.Max(1, (int)Math.Ceiling(c.End - Today)) + " day(s).";
                    else if (_queue.Exists(q => q.Lord == r.StringId)) no = "Terms agreed with him are still to be carried out.";
                    bool lift = siegeSt != null && r.MapFaction == siegeF;
                    rows.Add(new InquiryElement(r.StringId, Name(r) + " of " + Realm(r.MapFaction) + " - " + RideText(h), null, no == null,
                        no ?? ("Your envoy rides to " + Name(r) + " (" + RideText(h) + "). When he stands before him you speak through him: a pact of peace for your lands and men, the release of your people and soldiers his realm holds"
                               + (lift ? ", or the lifting of the siege of " + Name(siegeSt) : "") + (Ruler(Hero.MainHero) ? ", or a truce between your realms" : "") + ". What is agreed is carried out when the envoy is back (the same ride).")));
                }
                if (rows.Count == 0) { Log.Player("No hostile ruler can be reached by an envoy now."); return; }
                MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData("Send an envoy",
                    "Choose the ruler your envoy rides to. One envoy at a time; it costs nothing but time. When he arrives you will be told, and you have two days to hear him from any town, castle or village menu (or from 'Request a parley' while besieged). Free passage out of a besieged keep cannot be bargained through an envoy.",
                    rows, true, 1, 1, "Send", "Back",
                    delegate (List<InquiryElement> picked)
                    {
                        try
                        {
                            string id = picked != null && picked.Count > 0 ? picked[0].Identifier as string : null;
                            var r = id != null ? Hero.Find(id) : null;
                            if (r == null || _envState != '\0' || !EnvoyValid(r)) return;
                            double h = EnvoyHours(r);
                            if (h < 0) return;
                            var s2 = Settlement.CurrentSettlement;
                            bool siege = s2 != null && s2.SiegeEvent != null && s2.StringId != null;
                            _envHero = r.StringId; _envSt = siege ? s2.StringId : ""; _envSiegeStart = siege ? s2.SiegeEvent.SiegeStartTime.ToHours : 0;
                            _envHours = h; _envArrive = NowH + h; _envUntil = 0; _envState = 'R'; _dEnvSent++;
                            Log.Info(Tag + ": dzien " + F(Today) + " | poslaniec wyslany do " + Name(r) + " (" + Realm(r.MapFaction) + ") z " + (s2 != null ? Name(s2) : "-") + (siege ? " (oblezona)" : "") + " | jazda " + F(h) + " h, dotrze w dobie " + F(_envArrive / 24.0) + ".");
                            Log.Player("Your envoy rides for " + Name(r) + " (" + RideText(h) + ").");
                        }
                        catch (Exception e) { Stumble("EnvoySend.Pick", e); }
                    }, null), true);
            }
            catch (Exception e) { Stumble("EnvoySendCons", e); }
        }

        private static bool EnvoyHearCond(MenuCallbackArgs args)
        {
            if (_envState != 'W') return false;
            try
            {
                if (!KEnvoy || !On || _envTalk) return false;
                var r = Hero.Find(_envHero);
                if (r == null) return false;
                MBTextManager.SetTextVariable("ARM385D_HEAR", "Hear your envoy - he stands before " + Name(r));
                args.optionLeaveType = GameMenuOption.LeaveType.Conversation;
                var me = Hero.MainHero; var mp = MobileParty.MainParty;
                if (me == null || mp == null || me.IsPrisoner || mp.MapEvent != null || mp.BesiegerCamp != null || _offer != null)
                {
                    args.IsEnabled = false; args.Tooltip = new TextObject("{=!}Not now.");
                }
                return true;
            }
            catch (Exception e) { Stumble("EnvoyHearCond", e); return false; }
        }

        private static void EnvoyHearCons(MenuCallbackArgs args)
        {
            try
            {
                if (_envState != 'W') { return; }   // menu nieodswiezone po rozmowie - poslaniec juz wrocil
                var r = Hero.Find(_envHero);
                if (!EnvoyValid(r)) { EnvoyReturn(r, "the ruler can no longer receive him", "wladca nie moze go przyjac"); return; }
                // OpenMapConversation rzutuje ActiveState na MapState bez kontroli (NRE poza mapa) - bramka obowiazkowa
                var gsm = GameStateManager.Current;
                var cm = Campaign.Current != null ? Campaign.Current.ConversationManager : null;
                if (gsm == null || !(gsm.ActiveState is MapState) || Mission.Current != null || cm == null || cm.IsConversationInProgress || cm.IsConversationFlowActive) return;
                _envTalk = true; _dEnvHeard++;
                Log.Info(Tag + ": dzien " + F(Today) + " | poslaniec: rozmowa z " + Name(r) + " otwarta z menu (rozmowa na mapie, bez spotkania)" + (_envSt.Length > 0 ? " | wyslany z oblezonej " + _envSt : "") + ".");
                // jak gra (GameMenuOverlay.ExecuteTroopAction, QuickConversation) i NoticeBoard.TalkCons
                CampaignMapConversation.OpenConversation(new ConversationCharacterData(CharacterObject.PlayerCharacter, null, false, false, false, true),
                                                         new ConversationCharacterData(r.CharacterObject, null, false, false, false, true));
            }
            catch (Exception e) { _envTalk = false; Stumble("EnvoyHearCons", e); }
        }

        private static void EnvoyReturn(Hero r, string en, string pl)
        {
            Log.Info(Tag + ": dzien " + F(Today) + " | poslaniec do " + Name(r) + " wraca bez rozmowy (" + pl + ").");
            Log.Player("Your envoy to " + Name(r) + " returns: " + en + ".");
            _dEnvBack++;
            EnvoyClear();
        }

        // ------------------------------------------------------------ takt i doba
        private static void EnvoyTick()
        {
            if (_envState == '\0' && !_envTalk) return;
            try
            {
                var cm = Campaign.Current != null ? Campaign.Current.ConversationManager : null;
                if (_envTalk)
                {
                    // zapas dla ConversationEnded: czas plynie, a rozmowy nie ma - flaga nie moze zostac (blokowalaby zwykle rozmowy 384)
                    if (cm == null || (!cm.IsConversationInProgress && !cm.IsConversationFlowActive)) { _envTalk = false; Log.Info(Tag + ": poslaniec - flaga rozmowy zdjeta w takcie (bez zdarzenia konca rozmowy)."); EnvoyClear(); }
                    return;
                }
                var r = Hero.Find(_envHero);
                if (_envState == 'R')
                {
                    if (NowH < _envArrive) return;
                    if (!EnvoyValid(r)) { EnvoyReturn(r, r != null && r.IsAlive && r.IsPrisoner ? "the ruler is a captive" : r != null && r.IsAlive ? "there is no war to speak of any more" : "the ruler is dead", "wladca martwy / jeniec / pokoj / juz nie wladca"); return; }
                    var rp = r.PartyBelongedTo;
                    if (rp != null && rp.MapEvent != null && NowH < _envArrive + EnvoyBattleH) return;   // wladca w bitwie - poslaniec czeka
                    _envState = 'W'; _envUntil = NowH + EnvoyWaitH;
                    Log.Info(Tag + ": dzien " + F(Today) + " | poslaniec DOTARL do " + Name(r) + " - czeka na posluchanie do doby " + F(_envUntil / 24.0) + " (opcja 'Hear your envoy' w menu osady / parley).");
                    Log.Player("Your envoy stands before " + Name(r) + ". Hear him within two days from any town, castle or village menu" + (MobileParty.MainParty != null && MobileParty.MainParty.CurrentSettlement != null && MobileParty.MainParty.CurrentSettlement.SiegeEvent != null ? " - or from 'Request a parley'." : "."), true);
                    return;
                }
                if (_envState == 'W')
                {
                    if (!EnvoyValid(r)) { EnvoyReturn(r, "the ruler can no longer receive him", "wladca nie moze go przyjac"); return; }
                    if (NowH > _envUntil) EnvoyReturn(r, "he waited two days and was not heard", "gracz nie posluchal w 48 h");
                }
            }
            catch (Exception e) { Stumble("EnvoyTick", e); }
        }

        private static void EnvoyDaily()
        {
            if (_envState != '\0' || _dEnvSent + _dEnvHeard + _dEnvBack > 0)
            {
                var r = _envHero.Length > 0 ? Hero.Find(_envHero) : null;
                Log.Info(Tag + ": doba " + (int)Today + " | poslaniec (klucz " + (KEnvoy ? "TAK" : "NIE") + "): " + (_envState == 'R' ? "w drodze do " + Name(r) + ", dotrze za " + F(Math.Max(0, _envArrive - NowH)) + " h"
                         : _envState == 'W' ? "stoi przed " + Name(r) + ", czeka jeszcze " + F(Math.Max(0, _envUntil - NowH)) + " h" : "brak")
                         + " | dzis: wyslani " + _dEnvSent + ", rozmowy " + _dEnvHeard + ", powroty bez rozmowy " + _dEnvBack + ".");
            }
            _dEnvSent = 0; _dEnvHeard = 0; _dEnvBack = 0;
        }

        // ------------------------------------------------------------ zapis ("arm_env385d"): v1|wladca|twierdza|start oblezenia|stan|dotarcie|termin|godziny
        internal static string EnvoyExport()
        {
            try
            {
                if (_envState == '\0' || !Ok(_envHero) || !Ok(_envSt)) return "";
                return "v1|" + _envHero + "|" + _envSt + "|" + D(_envSiegeStart) + "|" + _envState + "|" + D(_envArrive) + "|" + D(_envUntil) + "|" + D(_envHours);
            }
            catch (Exception e) { Stumble("EnvoyExport", e); return ""; }
        }

        /// <summary>Po Import() glownego zapisu (ten zeruje stan poslanca w Reset).</summary>
        internal static void EnvoyImport(string data)
        {
            try
            {
                EnvoyClear(); _envTalk = false;
                if (string.IsNullOrEmpty(data)) return;
                var f = data.Split('|');
                double ss, ar, un, hh;
                if (f.Length != 8 || f[0] != "v1" || f[1].Length == 0 || f[4].Length != 1 || (f[4][0] != 'R' && f[4][0] != 'W')
                    || !double.TryParse(f[3], NumberStyles.Float, Inv, out ss) || !double.TryParse(f[5], NumberStyles.Float, Inv, out ar)
                    || !double.TryParse(f[6], NumberStyles.Float, Inv, out un) || !double.TryParse(f[7], NumberStyles.Float, Inv, out hh))
                { Log.Info(Tag + ": zapis poslanca nieczytelny - bez poslanca."); return; }
                _envHero = f[1]; _envSt = f[2]; _envSiegeStart = ss; _envState = f[4][0]; _envArrive = ar; _envUntil = un; _envHours = hh;
                Log.Info(Tag + ": wczytano poslanca - do " + _envHero + ", stan " + _envState + ", dotarcie h " + F(_envArrive) + ", termin h " + F(_envUntil) + ".");
            }
            catch (Exception e) { Stumble("EnvoyImport", e); }
        }
    }
}
