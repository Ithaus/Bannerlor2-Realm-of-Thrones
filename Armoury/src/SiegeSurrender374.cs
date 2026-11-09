using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 374 (Jeff 09.11: "jak obrona jest zbyt ciezka mozna robic parley i prosic o honorowe poddanie miasta ale warunek ze nie dostajemy sie do niewoli i nas
    /// atakujacy puszcza ale zabiera miasto ... moze tez nas oklamac i uwiezic"; "wybieram rozmowe z lordem ktory oblega i tam masz start parley").
    /// PODDANIE TWIERDZY NA WARUNKACH ("honours of war"). Klucz SiegeHonourableSurrender.
    ///
    /// Skad rozmowa (dekompilacja 1.4.8): gracz-obronca w menu oczekiwania "menu_siege_strategies" -> "Request a parley" -> menu "request_meeting_with_besiegers"
    /// (EncounterGameMenuBehavior) -> CampaignMission.OpenConversationMission z wodzem obozu (LeaderParty albo wodz jego armii). Gra ma tam tylko barter
    /// "What would it take for you to let me go my way?" (SafePassageBarterable - wolne przejscie za zloto, twierdza zostaje); warunek
    /// conversation_player_can_ask_for_honors_of_war_on_condition jest martwy (nikt go nie wola) - 374 wypelnia te luke.
    /// Linia gracza w hero_main_options "I would yield the keep, on terms." (wprost, bez lord_politics_request - tam cooldown barteru gry).
    /// Zgoda: Evaluate (wynik > 0, deterministycznie; podpowiedz slowna, bez liczb). Odmowa: 3 doby przerwy (st|wodz, w obrebie tego oblezenia).
    /// Zgoda w rozmowie: tylko SetNextMenu("arm374_yield") - ta sama droga co gra przy SafePassageBarterable (menu_siege_safe_passage_accepted); zadnego
    /// SwitchToMenu z menu oczekiwania (CTD). Menu arm374_yield (zwykle menu) - jedna opcja: wodz i jego oboz nie atakuja gracza 48 h (SetDoNotAttackMainParty),
    /// potem dokladnie jak gry "Leave" z oblezenia (menu_siege_leave_on_consequence): FinalizePlayerSiege, PlayerEncounter.LeaveSettlement, Finish; umowa do kolejki.
    /// Wykonanie w takcie mapy (bez misji, rozmowy, spotkania, gracz poza osada, jedna klatka zwloki):
    ///  - partie lordow obroncow w osadzie wychodza (LeaveSettlementAction, IgnoreForHours 12);
    ///  - slowo dotrzymane: bohaterowie bez druzyny do najblizszej wlasnej twierdzy, zaloga - twierdza rodu gracza: do druzyny gracza (ponad limit),
    ///    cudza: do partii i zalog starego pana (BookHoldings.YieldOut, ksiega ludzi i zbrojownia ida z ludzmi); potem KingdomManager.SiegeCompleted
    ///    (ta sama droga co zdobycie: koniec oblezenia, ApplyBySiege, glosowanie o lenno, wplyw zdobywcy) - bez bitwy, wiec bez lupu i rzezi;
    ///    relacje: wodz +3, pan twierdzy (cudzy rod) -15, wladca gracza -5; wplyw -10 jak w grze (twierdza frakcji gracza); BEZ roszczenia 369 (cesja).
    ///  - zdrada (losowana przy zgodzie, tylko Honor < 0): szeregowi gracza i zalogi do lochu wodza, towarzysze i gracz TakePrisonerAction (gracz na koncu -
    ///    PlayerCaptivity sam otwiera prisoner_wait), jency-bohaterowie gracza wolni; potem SiegeCompleted (z roszczeniem 369 - umowa zlamana);
    ///    relacja zdrajcy z graczem -30, glowy rodow z Honor > 0 wobec zdrajcy -5 x Honor; zdrajca zapamietany (ostrzezenie w podpowiedzi).
    /// Straty szturmow: wlasny rekord z MapEventEnded (szturm, zwyciezca != oblegajacy) + SiegeWalls.RepelsOf (odparcia sprzed 374).
    /// Zapis "arm_k374": rekordy oblezen, przerwy odmow, klamcy, oferta czekajaca w menu, kolejka. Potkniecia liczone, nic sie nie gasi.
    /// </summary>
    internal static class SiegeSurrender374
    {
        internal const string Tag = "Poddanie (374)";
        private const string MenuId = "arm374_yield";
        private const int AskPriority = 105;
        private const double CooldownDays = 3.0;
        private const int NoAttackHours = 48;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        internal static bool On { get { var s = Settings.Current; return s != null && s.SiegeHonourableSurrender; } }

        private sealed class Rec { public double Start; public int Held; public int Lost; }
        private sealed class Cool { public double Start; public double End; }
        private sealed class Deal { public string St = ""; public string Lord = ""; public double Start; public bool Betray; public double Score; public double P; public double Day; }

        private static readonly Dictionary<string, Rec> _recs = new Dictionary<string, Rec>();      // osada -> szturmy tego oblezenia
        private static readonly Dictionary<string, Cool> _cool = new Dictionary<string, Cool>();    // "osada|wodz" -> przerwa po odmowie
        private static readonly Dictionary<string, int> _liars = new Dictionary<string, int>();     // wodz -> doba zdrady
        private static readonly List<Deal> _queue = new List<Deal>();
        private static Deal _offer;          // zgoda w rozmowie, czeka na opcje menu arm374_yield
        private static bool _armed;
        private static int _stumbles;
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static volatile bool _ceding;   // okno SiegeCompleted przy dotrzymanym slowie (PlayerClaims369 - bez roszczenia)
        private static string _cededSt;

        // stan rozmowy
        private static Hero _talk;
        private static Settlement _st;
        private static Score _sc;
        private static bool _accepted;

        internal static void Reset()
        {
            _recs.Clear(); _cool.Clear(); _liars.Clear(); _queue.Clear(); _offer = null; _armed = false; _stumbles = 0; _err.Clear();
            _ceding = false; _cededSt = null; ClearTalk();
        }

        private static void ClearTalk() { _talk = null; _st = null; _sc = null; _accepted = false; }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_err.Add(where)) Log.Error("SiegeSurrender374." + where, e); } catch { }
        }

        private static string Name(Hero h) { try { return h != null && h.Name != null ? h.Name.ToString() : "-"; } catch { return "?"; } }
        private static string Name(Settlement s) { try { return s != null && s.Name != null ? s.Name.ToString() : "-"; } catch { return "?"; } }
        private static string Name(MobileParty mp) { try { return mp != null && mp.Name != null ? mp.Name.ToString() : "-"; } catch { return "?"; } }
        private static string F(double v) { return v.ToString("0.#", Inv); }
        private static double Today { get { return CampaignTime.Now.ToDays; } }

        /// <summary>PlayerClaims369: czy ta zmiana pana to cesja pod przysiega (bez roszczenia). Tylko w trakcie SiegeCompleted przy dotrzymanym slowie.</summary>
        internal static bool Ceded(Settlement st) { return _ceding && st != null && st.StringId == _cededSt; }

        // ------------------------------------------------------------ rozmowa
        internal static void AddDialogs(CampaignGameStarter starter)
        {
            try
            {
                if (starter == null) return;
                starter.AddPlayerLine("arm374_ask", "hero_main_options", "arm374_open", "{=!}I would yield the keep, on terms.", AskCond, AskCons, AskPriority, AskClick);
                starter.AddDialogLine("arm374_open", "arm374_open", "arm374_menu", "{=!}Terms? You are in no place to ask for much. Speak.", null, null, 200);
                starter.AddPlayerLine("arm374_terms", "arm374_menu", "arm374_hint", "{=!}My men and I march out with our arms and banners, unharmed and free. The keep is yours.", null, Pick, 110);
                starter.AddPlayerLine("arm374_back", "arm374_menu", "lord_pretalk", "{=!}Never mind.", null, ClearTalkCons, 100);
                starter.AddDialogLine("arm374_hint", "arm374_hint", "arm374_confirm", "{=!}{ARM374_HINT}", HintCond, null, 200);
                starter.AddPlayerLine("arm374_go", "arm374_confirm", "arm374_answer", "{=!}Then swear it before gods and men, and the gates open.", GoCond, Decide, 110);
                starter.AddPlayerLine("arm374_back2", "arm374_confirm", "lord_pretalk", "{=!}Never mind.", null, ClearTalkCons, 100);
                starter.AddDialogLine("arm374_yes", "arm374_answer", "close_window", "{=!}You held well. Keep your steel and go - you have my word.", YesCond, YesCons, 200);
                starter.AddDialogLine("arm374_no", "arm374_answer", "lord_pretalk", "{=!}{ARM374_NO}", NoCond, NoCons, 199);
                var s = Settings.Current;
                Log.Info(Tag + ": rozmowa wpieta (hero_main_options, priorytet " + AskPriority + ") | klucz " + (s != null && s.SiegeHonourableSurrender ? "TAK" : "NIE")
                         + " | oblezenia " + _recs.Count + ", przerwy " + _cool.Count + ", klamcy " + _liars.Count + ", kolejka " + _queue.Count + (_offer != null ? ", oferta w menu" : "") + ".");
            }
            catch (Exception e) { Stumble("AddDialogs", e); }
        }

        internal static void AddMenus(CampaignGameStarter starter)
        {
            try
            {
                if (starter == null) return;
                starter.AddGameMenu(MenuId, "{=!}{ARM374_MENU}", MenuInit, GameMenu.MenuOverlayType.None);
                starter.AddGameMenuOption(MenuId, "arm374_ride", "{=!}Ride out under the terms.", RideCond, RideCons, true);
            }
            catch (Exception e) { Stumble("AddMenus", e); }
        }

        /// <summary>Wodz obozu: wodz armii LeaderParty albo LeaderParty (jak gra w request_meeting_with_besiegers) i GetLeaderOfSiegeEvent.</summary>
        private static bool IsCampLeader(SiegeEvent ev, Hero h)
        {
            var camp = ev != null ? ev.BesiegerCamp : null;
            if (camp == null || h == null) return false;
            var lp = camp.LeaderParty;
            if (lp != null)
            {
                var top = lp.Army != null && lp.Army.LeaderParty != null ? lp.Army.LeaderParty : lp;
                if (top.LeaderHero == h || lp.LeaderHero == h) return true;
            }
            try { return Campaign.Current.Models.EncounterModel.GetLeaderOfSiegeEvent(ev, BattleSideEnum.Attacker) == h; } catch { return false; }
        }

        private static bool Excluded(Hero h)
        {
            if (h.Clan == null || h.MapFaction == null) return true;
            if (h.MapFaction.IsBanditFaction || h.Clan.IsBanditFaction) return true;
            if (OthersRise.IsWhiteWalkerClan(h.Clan)) return true;
            try { if (h.PartyBelongedTo != null && Undead.Party(h.PartyBelongedTo)) return true; } catch { }
            return false;
        }

        private static string CoolKey(Settlement st, Hero h) { return st.StringId + "|" + h.StringId; }

        private static double CoolLeft(Settlement st, Hero h)
        {
            Cool c;
            if (st == null || h == null || st.SiegeEvent == null || !_cool.TryGetValue(CoolKey(st, h), out c)) return 0;
            if (Math.Abs(c.Start - st.SiegeEvent.SiegeStartTime.ToHours) > 0.05) return 0;   // inne oblezenie - przerwa nie obowiazuje
            double left = c.End - Today;
            return left > 0 ? left : 0;
        }

        private static bool AskCond()
        {
            try
            {
                if (!On || _offer != null) return false;
                var h = Hero.OneToOneConversationHero;
                var me = Hero.MainHero;
                var mp = MobileParty.MainParty;
                if (h == null || me == null || mp == null || h == me || !h.IsAlive || !h.IsLord || h.IsPrisoner || h.StringId == null) return false;
                var st = mp.CurrentSettlement;
                if (st == null || !st.IsFortification || st.Town == null || st.SiegeEvent == null || mp.BesiegerCamp != null) return false;
                if (Settlement.CurrentSettlement != st) return false;
                var ev = st.SiegeEvent;
                if (!IsCampLeader(ev, h) || Excluded(h)) return false;
                if (me.MapFaction == null || !FactionManager.IsAtWarAgainstFaction(h.MapFaction, me.MapFaction)) return false;
                if ((st.Party != null && st.Party.MapEvent != null) || mp.MapEvent != null) return false;
                if (mp.Army != null && mp.Army.LeaderParty != mp) return false;
                Hero def = null;
                try { def = Campaign.Current.Models.EncounterModel.GetLeaderOfSiegeEvent(ev, BattleSideEnum.Defender); } catch { }
                if (def != me && st.OwnerClan != Clan.PlayerClan) return false;
                foreach (var d in _queue) if (d.St == st.StringId) return false;
                return true;
            }
            catch (Exception e) { Stumble("AskCond", e); return false; }
        }

        private static bool AskClick(out TextObject why)
        {
            why = null;
            try
            {
                var h = Hero.OneToOneConversationHero;
                var st = MobileParty.MainParty != null ? MobileParty.MainParty.CurrentSettlement : null;
                double cl = CoolLeft(st, h);
                if (cl > 0) { why = new TextObject("{=!}He has refused you already. You may ask again in " + Math.Ceiling(cl).ToString(Inv) + " days."); return false; }
                return true;
            }
            catch (Exception e) { Stumble("AskClick", e); return true; }
        }

        private static void AskCons()
        {
            try { ClearTalk(); _talk = Hero.OneToOneConversationHero; _st = MobileParty.MainParty.CurrentSettlement; }
            catch (Exception e) { Stumble("AskCons", e); }
        }

        private static void ClearTalkCons() { ClearTalk(); }

        private static void Pick()
        {
            try { _sc = _talk != null && _st != null && _st.SiegeEvent != null ? Evaluate(_talk, _st) : null; }
            catch (Exception e) { Stumble("Pick", e); _sc = null; }
        }

        private static bool IsLiar(Hero h) { return h != null && h.StringId != null && _liars.ContainsKey(h.StringId); }

        private static bool HintCond()
        {
            try
            {
                if (_talk == null || _sc == null) { MBTextManager.SetTextVariable("ARM374_HINT", "I cannot think on it now."); return true; }
                var sb = new StringBuilder(200);
                double v = _sc.Total;
                sb.Append("He weighs it (");
                sb.Append(v >= 5 ? "likely to accept" : v > -5 ? "it could go either way" : "unlikely");
                sb.Append(").");
                int honor = Trait(_talk, DefaultTraits.Honor);
                if (IsLiar(_talk) || honor < 0) sb.Append(" Beware: men say he has broken faith before.");
                else if (honor > 0) sb.Append(" His word: men say he keeps it.");
                MBTextManager.SetTextVariable("ARM374_HINT", sb.ToString());
                return true;
            }
            catch (Exception e) { Stumble("HintCond", e); MBTextManager.SetTextVariable("ARM374_HINT", "..."); return true; }
        }

        private static bool GoCond() { return _sc != null; }

        private static void Decide()
        {
            try
            {
                var h = _talk; var st = _st;
                if (h == null || st == null || st.SiegeEvent == null) { _accepted = false; MBTextManager.SetTextVariable("ARM374_NO", "Not now."); return; }
                _sc = Evaluate(h, st);   // od nowa - ten sam wynik co w podpowiedzi
                _accepted = _sc.Total > 0;
                if (_accepted) return;   // zdrada i oferta w YesCons (tylko przy zgodzie)
                string no = _sc.Why == 'S' ? "Why bargain for walls I can simply take?"
                          : _sc.Why == 'C' ? "No. I will have you in chains and your banner in the mud."
                          : "Not yet. Hold a few days more, and we shall speak again.";
                MBTextManager.SetTextVariable("ARM374_NO", no);
                _cool[CoolKey(st, h)] = new Cool { Start = st.SiegeEvent.SiegeStartTime.ToHours, End = Today + CooldownDays };
                LogDecision(h, st, _sc, "ODMOWA (przerwa " + F(CooldownDays) + " d)", 0);
            }
            catch (Exception e) { Stumble("Decide", e); _accepted = false; MBTextManager.SetTextVariable("ARM374_NO", "Not now."); }
        }

        private static bool YesCond() { return _accepted; }
        private static bool NoCond() { return !_accepted; }

        private static void YesCons()
        {
            try
            {
                var h = _talk; var st = _st;
                if (h != null && st != null && st.SiegeEvent != null && _sc != null)
                {
                    double p = BetrayChance(h);
                    bool betray = p > 0 && MBRandom.RandomFloat < p;
                    _offer = new Deal { St = st.StringId, Lord = h.StringId, Start = st.SiegeEvent.SiegeStartTime.ToHours, Betray = betray, Score = _sc.Total, P = p, Day = Today };
                    // ta sama droga co gra po udanym barterze wolnego przejscia (SafePassageBarterable.Apply): nastepne menu po rozmowie, bez SwitchToMenu
                    Campaign.Current.GameMenuManager.SetNextMenu(MenuId);
                    LogDecision(h, st, _sc, "ZGODA | zdrada " + F(p * 100) + "%" + (betray ? " -> ZLAMIE SLOWO" : " -> dotrzyma"), p);
                }
            }
            catch (Exception e) { Stumble("YesCons", e); _offer = null; }
            ClearTalk();
        }

        private static void NoCons()
        {
            try { Log.Player(Name(_talk) + " refuses your terms. You may ask again in " + CooldownDays.ToString(Inv) + " days.", true); } catch { }
            ClearTalk();
        }

        // ------------------------------------------------------------ menu po rozmowie (zwykle menu, nie oczekiwania)
        private static void MenuInit(MenuCallbackArgs args)
        {
            try
            {
                var st = _offer != null ? Settlement.Find(_offer.St) : MobileParty.MainParty.CurrentSettlement;
                var lord = _offer != null ? Hero.Find(_offer.Lord) : null;
                MBTextManager.SetTextVariable("ARM374_MENU", "The gates of " + Name(st) + " open. " + (lord != null ? Name(lord) : "The besiegers' commander") + " waits beyond them with his terms.");
                string mesh = st != null && st.SettlementComponent != null ? st.SettlementComponent.WaitMeshName : null;
                args.MenuContext.SetBackgroundMeshName(string.IsNullOrEmpty(mesh) ? "wait_fallback" : mesh);
            }
            catch (Exception e) { Stumble("MenuInit", e); }
        }

        private static bool RideCond(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Leave;
            return true;
        }

        private static void RideCons(MenuCallbackArgs args)
        {
            var o = _offer;
            _offer = null;
            try
            {
                var st = o != null ? Settlement.Find(o.St) : null;
                var ev = st != null ? st.SiegeEvent : null;
                // oboz nie atakuje gracza - PRZED wyjsciem za brame (gra: SetDoNotAttackMainParty(32) dla stron spotkania)
                int n = 0;
                if (ev != null && ev.BesiegerCamp != null)
                    foreach (var pb in ev.BesiegerCamp.GetInvolvedPartiesForEventType())
                    {
                        var mp = pb != null ? pb.MobileParty : null;
                        if (mp == null || mp.Ai == null) continue;
                        mp.Ai.SetDoNotAttackMainParty(NoAttackHours); n++;
                    }
                if (o != null) { _queue.Add(o); _armed = false; }
                Log.Info(Tag + ": dzien " + F(Today) + " | " + Name(st) + " - gracz wychodzi za brame pod warunkami | oboz nie atakuje " + n + " partii przez " + NoAttackHours + " h"
                         + (o == null ? " | BRAK umowy (wczytanie bez oferty?) - samo wyjscie" : " | umowa w kolejce (wykonanie w takcie mapy)") + ".");
            }
            catch (Exception e) { Stumble("RideCons", e); }
            // dokladnie jak gry menu_siege_leave_on_consequence
            try
            {
                var main = MobileParty.MainParty;
                if (main.BesiegerCamp == null && PlayerEncounter.Current != null && main.CurrentSettlement != null)
                {
                    if (main.Army != null) main.Army = null;
                    PlayerSiege.FinalizePlayerSiege();
                    PlayerEncounter.LeaveSettlement();
                    PlayerEncounter.Finish();
                }
                else GameMenu.ExitToLast();
            }
            catch (Exception e) { Stumble("RideCons.Leave", e); }
        }

        // ------------------------------------------------------------ ocena zgody
        private sealed class Score { public double Total; public string Parts = "-"; public char Why = 'T'; public int Held, Lost; public double Days, Camp, Walls, Food; }

        private static int Trait(Hero h, TraitObject t) { try { return t != null ? h.GetTraitLevel(t) : 0; } catch { return 0; } }

        private static float Power(PartyBase p)
        {
            try { return p != null ? Campaign.Current.Models.MilitaryPowerModel.GetPowerOfParty(p, BattleSideEnum.Attacker, MapEvent.PowerCalculationContext.PlainBattle) : 0f; }
            catch { return 0f; }
        }

        internal static void Strength(Settlement st, out double camp, out double walls)
        {
            camp = 0; walls = 0;
            var ev = st.SiegeEvent;
            if (ev != null && ev.BesiegerCamp != null)
                foreach (var pb in ev.BesiegerCamp.GetInvolvedPartiesForEventType()) camp += Power(pb);
            foreach (var mp in st.Parties) if (mp != null && (mp.IsGarrison || mp.IsLordParty || mp.IsMainParty || mp.IsMilitia)) walls += Power(mp.Party);
        }

        private static void Assaults(Settlement st, out int held, out int lost)
        {
            held = 0; lost = 0;
            var ev = st.SiegeEvent;
            if (ev == null) return;
            Rec r;
            if (_recs.TryGetValue(st.StringId, out r) && Math.Abs(r.Start - ev.SiegeStartTime.ToHours) <= 0.05) { held = r.Held; lost = r.Lost; }
            int rep = 0;
            try { rep = SiegeWalls.RepelsOf(ev); } catch { }
            held = Math.Max(held, rep);   // odparcia sprzed 374 (bez strat)
        }

        private static Score Evaluate(Hero h, Settlement st)
        {
            var sc = new Score();
            var me = Hero.MainHero;
            var ev = st.SiegeEvent;
            int held, lost; Assaults(st, out held, out lost);
            double camp, walls; Strength(st, out camp, out walls);
            int menIn = 0;
            foreach (var mp in st.Parties) if (mp != null && mp.MemberRoster != null && (mp.IsGarrison || mp.IsLordParty || mp.IsMainParty || mp.IsMilitia)) menIn += mp.MemberRoster.TotalManCount;
            double days = ev != null ? Math.Max(0, ev.SiegeStartTime.ElapsedDaysUntilNow) : 0;
            double food = 999;
            try { float ch = st.Town.FoodChange; if (ch < 0) food = st.Town.FoodStocks / -ch; } catch { }
            double R = walls > 1 ? camp / walls : 99;

            double baseV = -5;
            double A = Math.Min(36, 12.0 * held);
            double B = 15.0 * Math.Min(1.0, lost / Math.Max(1.0, 2.0 * menIn));
            double T = Math.Min(15, Math.Max(0, days - 5));
            double S = Math.Max(-25, Math.Min(15, -8.0 * Math.Log(Math.Max(0.01, R / 4.0), 2)));
            double Fd = food < 7 ? -2.0 * (7 - food) : food > 30 ? 5 : 0;
            int rel = 0; try { rel = h.GetRelation(me); } catch { }
            double Rl = Math.Max(-15, Math.Min(15, 0.3 * rel));
            int honor = Trait(h, DefaultTraits.Honor), calc = Trait(h, DefaultTraits.Calculating), mercy = Trait(h, DefaultTraits.Mercy), valor = Trait(h, DefaultTraits.Valor);
            double Hn = 6.0 * honor, Cc = 5.0 * calc, Mc = 4.0 * mercy, Vl = -3.0 * valor;
            double Lr = honor < 0 ? 5.0 * -honor : 0;   // latwo przysiega - nie zamierza dotrzymac
            int charm = 0; try { charm = me.GetSkillValue(DefaultSkills.Charm); } catch { }
            double Ch = Math.Max(-5, Math.Min(15, (charm - 50) / 10.0));
            int tier = 0; try { tier = Clan.PlayerClan != null ? Clan.PlayerClan.Tier : 0; } catch { }
            double Pr = -2.0 * tier;
            double Tw = st.IsTown ? -5 : 0;
            sc.Total = baseV + A + B + T + S + Fd + Rl + Hn + Cc + Mc + Vl + Lr + Ch + Pr + Tw;
            // glowny powod odmowy: przewaga sil / okrutny albo dumny / reszta
            if (S <= -15) sc.Why = 'S'; else if (mercy < 0 || valor > 0) sc.Why = 'C'; else sc.Why = 'T';
            sc.Held = held; sc.Lost = lost; sc.Days = days; sc.Camp = camp; sc.Walls = walls; sc.Food = food;
            sc.Parts = "podstawa " + F(baseV) + ", szturmy " + F(A) + " (" + held + "), krew " + F(B) + " (" + lost + "/" + (2 * menIn) + "), czas " + F(T) + " (" + F(days) + " d)"
                     + ", sily " + F(S) + " (R " + R.ToString("0.0", Inv) + "), jedzenie " + F(Fd) + " (" + (food >= 999 ? "bez ubytku" : F(food) + " d") + "), relacja " + F(Rl) + " (" + rel + ")"
                     + ", Honor " + F(Hn) + ", Calc " + F(Cc) + ", Mercy " + F(Mc) + ", Valor " + F(Vl) + (Lr > 0 ? ", klamca " + F(Lr) : "")
                     + ", Charm " + F(Ch) + " (" + charm + "), jeniec " + F(Pr) + " (tier " + tier + ")" + (Tw != 0 ? ", miasto " + F(Tw) : "");
            return sc;
        }

        private static double BetrayChance(Hero h)
        {
            int honor = Trait(h, DefaultTraits.Honor);
            if (honor >= 0) return 0;
            int rel = 0; try { rel = h.GetRelation(Hero.MainHero); } catch { }
            double p = 0.15 * -honor + (Trait(h, DefaultTraits.Calculating) > 0 ? 0.10 : 0) + (rel < -20 ? 0.05 : 0);
            return Math.Min(0.6, p);
        }

        private static void LogDecision(Hero h, Settlement st, Score sc, string verdict, double p)
        {
            try
            {
                int rel = 0; try { rel = h.GetRelation(Hero.MainHero); } catch { }
                Log.Info(Tag + ": dzien " + F(Today) + " | " + Name(st) + " [" + st.StringId + "] | wodz " + Name(h) + " (Honor " + Trait(h, DefaultTraits.Honor) + ", Calc " + Trait(h, DefaultTraits.Calculating)
                         + ", Mercy " + Trait(h, DefaultTraits.Mercy) + ", Valor " + Trait(h, DefaultTraits.Valor) + ", rel " + rel + (IsLiar(h) ? ", znany klamca" : "") + ") | szturmy odparte " + sc.Held + ", straty " + sc.Lost
                         + ", dni " + F(sc.Days) + ", sily " + Math.Round(sc.Camp) + "/" + Math.Round(sc.Walls) + ", jedzenie " + (sc.Food >= 999 ? "-" : F(sc.Food) + " d") + " | wynik " + sc.Total.ToString("+0.0;-0.0", Inv)
                         + " [" + sc.Parts + "] -> " + verdict + ".");
            }
            catch (Exception e) { Stumble("LogDecision", e); }
        }

        // ------------------------------------------------------------ szturmy (MapEventEnded)
        internal static void OnMapEventEnded(MapEvent me)
        {
            try
            {
                if (me == null || !me.IsSiegeAssault) return;
                var st = me.MapEventSettlement;
                if (st == null || st.SiegeEvent == null || st.StringId == null) return;
                if (me.WinningSide == BattleSideEnum.Attacker) return;
                double start = st.SiegeEvent.SiegeStartTime.ToHours;
                Rec r;
                if (!_recs.TryGetValue(st.StringId, out r) || Math.Abs(r.Start - start) > 0.05) { r = new Rec { Start = start }; _recs[st.StringId] = r; }
                r.Held++;
                try { r.Lost += me.AttackerSide.TroopCasualties; } catch { }
            }
            catch (Exception e) { Stumble("OnMapEventEnded", e); }
        }

        // ------------------------------------------------------------ wykonanie (takt mapy)
        internal static void Tick()
        {
            if (_queue.Count == 0) return;
            try
            {
                if (Campaign.Current == null || Mission.Current != null) return;
                var cm = Campaign.Current.ConversationManager;
                if (cm != null && (cm.IsConversationInProgress || cm.IsConversationFlowActive)) return;
                var mp = MobileParty.MainParty;
                if (PlayerEncounter.Current != null || mp == null || mp.CurrentSettlement != null || mp.MapEvent != null || Hero.MainHero.IsPrisoner) return;
                var d = _queue[0];
                var st0 = Settlement.Find(d.St);
                if (st0 != null && st0.Party != null && st0.Party.MapEvent != null && Today - d.Day < 2.0) return;   // szturm w toku - czekamy (najwyzej 2 doby)
                if (!_armed) { _armed = true; return; }
                _queue.RemoveAt(0);   // najpierw z kolejki - wyjatek nie powtorzy umowy
                _armed = false;
                Run(d);
            }
            catch (Exception e) { Stumble("Tick", e); }
        }

        private static void Drop(Deal d, Settlement st, string en, string pl)
        {
            Log.Info(Tag + ": dzien " + F(Today) + " | " + (st != null ? Name(st) : d.St) + " - umowa PRZEPADLA: " + pl + ".");
            if (!string.IsNullOrEmpty(en)) Log.Player(en, true);
        }

        private static MobileParty Capturer(SiegeEvent ev, Hero lord)
        {
            var camp = ev.BesiegerCamp;
            if (camp == null) return null;
            var lp = lord != null && lord.IsAlive && !lord.IsPrisoner ? lord.PartyBelongedTo : null;
            if (lp != null && lp.IsActive && lp.LeaderHero == lord && camp.HasInvolvedPartyForEventType(lp.Party)) return lp;
            var l = camp.LeaderParty;
            return l != null && l.IsActive && l.LeaderHero != null ? l : null;
        }

        private static void Run(Deal d)
        {
            Settlement st = null;
            try
            {
                st = Settlement.Find(d.St);
                var lord = Hero.Find(d.Lord);
                var ev = st != null ? st.SiegeEvent : null;
                if (Today - d.Day > 3.0) { Drop(d, st, "The terms agreed at " + Name(st) + " have lapsed.", "umowa przedawniona (ponad 3 doby od zgody - gracz w niewoli albo w spotkaniu)"); return; }
                if (st == null || ev == null || Math.Abs(ev.SiegeStartTime.ToHours - d.Start) > 0.05) { Drop(d, st, "The siege of " + Name(st) + " ended before the keys changed hands.", "oblezenie skonczylo sie przed przekazaniem"); return; }
                var cap = Capturer(ev, lord);
                if (cap == null) { Drop(d, st, null, "brak wodza obozu"); return; }
                if (!FactionManager.IsAtWarAgainstFaction(cap.MapFaction, st.MapFaction)) { Drop(d, st, null, "pokoj miedzy stronami - osada zostaje"); return; }
                if (st.Party != null && st.Party.MapEvent != null) { Drop(d, st, null, "szturm w toku dluzej niz 2 doby"); return; }
                bool betray = d.Betray && lord != null && cap.LeaderHero == lord && cap.MapEvent == null;
                if (betray) RunBetray(d, st, lord, cap); else RunKept(d, st, lord, cap);
            }
            catch (Exception e) { Stumble("Run", e); try { _ceding = false; _cededSt = null; } catch { } }
        }

        /// <summary>Partie lordow strony obroncow w osadzie wychodza przed zmiana pana (inaczej zostalyby w osadzie wroga).</summary>
        private static int LeadOutLords(Settlement st, MobileParty cap)
        {
            int n = 0;
            foreach (var mp in new List<MobileParty>(st.Parties))
            {
                try
                {
                    if (mp == null || mp.IsMainParty || mp.IsGarrison || mp.IsMilitia || !mp.IsLordParty || mp.MapEvent != null) continue;
                    if (mp.MapFaction == null || !mp.MapFaction.IsAtWarWith(cap.MapFaction)) continue;
                    LeaveSettlementAction.ApplyForParty(mp);
                    mp.IgnoreForHours(12f);
                    n++;
                }
                catch (Exception e) { Stumble("LeadOutLords", e); }
            }
            return n;
        }

        private static void RunKept(Deal d, Settlement st, Hero lord, MobileParty cap)
        {
            var me = Hero.MainHero;
            var oldClan = st.OwnerClan;
            var oldOwner = oldClan != null ? oldClan.Leader : null;
            bool mine = oldClan == Clan.PlayerClan;
            bool myFaction = st.MapFaction == me.MapFaction;
            int lords = LeadOutLords(st, cap);
            string out1 = BookHoldings.YieldOut(st, cap.MapFaction, mine ? MobileParty.MainParty : null, "374-" + st.StringId);
            _cededSt = st.StringId; _ceding = true;
            try { Campaign.Current.KingdomManager.SiegeCompleted(st, cap, true, MapEvent.BattleTypes.Siege); }
            finally { _ceding = false; _cededSt = null; }
            var rel = new List<string>();
            try
            {
                if (lord != null && lord.IsAlive) { ChangeRelationAction.ApplyPlayerRelation(lord, 3); rel.Add(Name(lord) + " +3"); }
                if (!mine && oldOwner != null && oldOwner.IsAlive && oldOwner != me) { ChangeRelationAction.ApplyPlayerRelation(oldOwner, -15); rel.Add(Name(oldOwner) + " -15"); }
                var k = Clan.PlayerClan != null ? Clan.PlayerClan.Kingdom : null;
                var ruler = k != null ? k.Leader : null;
                if (ruler != null && ruler.IsAlive && ruler != me && ruler != oldOwner) { ChangeRelationAction.ApplyPlayerRelation(ruler, -5); rel.Add(Name(ruler) + " -5"); }
                if (myFaction && Clan.PlayerClan != null && Clan.PlayerClan.Kingdom != null) { GainKingdomInfluenceAction.ApplyForSiegeSafePassageBarter(MobileParty.MainParty, -10f); rel.Add("wplyw -10"); }
            }
            catch (Exception e) { Stumble("RunKept.Relacje", e); }
            Log.Info(Tag + ": dzien " + F(Today) + " | " + Name(st) + " [" + st.StringId + "] WYKONANE: slowo dotrzymane | wodz " + Name(lord) + ", zdobywca " + Name(cap) + " | partie lordow wyprowadzone " + lords
                     + " | " + out1 + " | wlasciciel " + (oldClan != null ? oldClan.Name.ToString() : "-") + " -> " + (st.OwnerClan != null ? st.OwnerClan.Name.ToString() : "-") + " (SiegeCompleted)"
                     + " | relacje: " + (rel.Count > 0 ? string.Join(", ", rel.ToArray()) : "-") + " | roszczenie: brak (cesja).");
            Log.Player("You yield " + Name(st) + " to " + Name(lord) + ". Your men march out under arms, and he keeps his word.");
        }

        private static void RunBetray(Deal d, Settlement st, Hero lord, MobileParty cap)
        {
            var me = Hero.MainHero;
            var main = MobileParty.MainParty;
            var pris = cap.Party.PrisonRoster;
            int lords = LeadOutLords(st, cap);
            // szeregowi gracza do lochu wodza (rozbrojeni - jak jency)
            int men = 0, garr = 0, comp = 0, freed = 0;
            men = ToPrison(main.MemberRoster, pris);
            // zaloga wyszla z graczem - tez do lochu
            var g = st.Town != null ? st.Town.GarrisonParty : null;
            if (g != null && g.MemberRoster != null) garr = ToPrison(g.MemberRoster, pris);
            // jency-bohaterowie gracza wolni (gracz traci wszystko)
            try
            {
                var pr = main.PrisonRoster;
                for (int i = pr.Count - 1; i >= 0; i--)
                {
                    var c = pr.GetCharacterAtIndex(i);
                    if (c == null || !c.IsHero || c.HeroObject == null) continue;
                    try { EndCaptivityAction.ApplyByReleasedAfterBattle(c.HeroObject); freed++; } catch (Exception e) { Stumble("RunBetray.Free", e); }
                }
            }
            catch (Exception e) { Stumble("RunBetray.Prisoners", e); }
            // towarzysze, potem gracz (PlayerCaptivity: rozwiazanie armii, prisoner_wait w nastepnej klatce - kod gry)
            var heroes = new List<Hero>();
            try
            {
                var r = main.MemberRoster;
                for (int i = 0; i < r.Count; i++) { var c = r.GetCharacterAtIndex(i); if (c != null && c.IsHero && c.HeroObject != null && c.HeroObject != me) heroes.Add(c.HeroObject); }
            }
            catch (Exception e) { Stumble("RunBetray.Heroes", e); }
            foreach (var h in heroes)
            {
                try { TakePrisonerAction.Apply(cap.Party, h); comp++; } catch (Exception e) { Stumble("RunBetray.Companion", e); }
            }
            bool taken = false;
            try { TakePrisonerAction.Apply(cap.Party, me); taken = true; } catch (Exception e) { Stumble("RunBetray.Player", e); }
            // twierdza - zwykla droga zdobycia (umowa zlamana: roszczenie 369 jak przy zdobyciu)
            var oldClan = st.OwnerClan;
            Campaign.Current.KingdomManager.SiegeCompleted(st, cap, true, MapEvent.BattleTypes.Siege);
            // kara: zdrajca wobec gracza i honorowych glow rodow
            int punished = 0;
            try
            {
                ChangeRelationAction.ApplyPlayerRelation(lord, -30);
                foreach (var c in Clan.All)
                {
                    if (c == null || c.IsEliminated || c == lord.Clan || c == Clan.PlayerClan || c.IsBanditFaction) continue;
                    var l = c.Leader;
                    if (l == null || !l.IsAlive || l == lord) continue;
                    int hon = Trait(l, DefaultTraits.Honor);
                    if (hon <= 0) continue;
                    ChangeRelationAction.ApplyRelationChangeBetweenHeroes(l, lord, -5 * hon, false);
                    punished++;
                }
            }
            catch (Exception e) { Stumble("RunBetray.Kara", e); }
            _liars[lord.StringId] = (int)Today;
            Log.Info(Tag + ": dzien " + F(Today) + " | " + Name(st) + " [" + st.StringId + "] ZDRADA: wodz " + Name(lord) + " lamie slowo | gracz " + (taken ? "W NIEWOLI" : "NIE pojmany (blad)") + " + " + comp + " towarzyszy do niewoli "
                     + Name(cap) + ", jency szeregowi " + (men + garr) + " (druzyna " + men + ", zaloga " + garr + "), jency-bohaterowie gracza wolni " + freed + " | partie lordow wyprowadzone " + lords
                     + " | wlasciciel " + (oldClan != null ? oldClan.Name.ToString() : "-") + " -> " + (st.OwnerClan != null ? st.OwnerClan.Name.ToString() : "-") + " (SiegeCompleted, roszczenie jak przy zdobyciu)"
                     + " | kara relacji: " + punished + " glow rodow | klamca zapisany.");
            Log.Player(Name(lord) + " breaks his word! His men seize you at the gates of " + Name(st) + ".", true);
        }

        /// <summary>Szeregowi z rosteru do lochu (ranni zostaja ranni); ksiega ludzi FCaptured. Zwraca liczbe.</summary>
        private static int ToPrison(TaleWorlds.CampaignSystem.Roster.TroopRoster from, TaleWorlds.CampaignSystem.Roster.TroopRoster to)
        {
            int total = 0;
            try
            {
                var moved = new List<KeyValuePair<CharacterObject, int[]>>();
                for (int i = 0; i < from.Count; i++)
                {
                    var c = from.GetCharacterAtIndex(i);
                    if (c == null || c.IsHero) continue;
                    int n = from.GetElementNumber(i), w = from.GetElementWoundedNumber(i);
                    if (n > 0) moved.Add(new KeyValuePair<CharacterObject, int[]>(c, new[] { n, w }));
                }
                foreach (var kv in moved)
                {
                    from.AddToCounts(kv.Key, -kv.Value[0], false, -kv.Value[1]);
                    to.AddToCounts(kv.Key, kv.Value[0], false, kv.Value[1]);
                    total += kv.Value[0];
                }
                if (total > 0)
                {
                    try { PeopleBook.Give(to, PeopleBook.Take(from, total, PeopleBook.FCaptured), PeopleBook.FCaptured); } catch (Exception e) { Stumble("ToPrison.PeopleBook", e); }
                }
            }
            catch (Exception e) { Stumble("ToPrison", e); }
            return total;
        }

        // ------------------------------------------------------------ zapis ("arm_k374")
        private static readonly char[] Bad = { '|', ';', ':' };
        private static string D(double v) { return v.ToString("R", Inv); }
        private static bool P(string s, out double v) { return double.TryParse(s, NumberStyles.Float, Inv, out v); }

        /// <summary>"v1|R:st:start:held:lost;...|C:st:wodz:start:koniec;...|L:wodz:doba;...|Q:st:wodz:start:zdrada:wynik:p:doba;...|O:..." (O - oferta w menu).</summary>
        internal static string Export()
        {
            try
            {
                var sb = new StringBuilder(256);
                sb.Append("v1|");
                bool f = true;
                foreach (var kv in _recs)
                {
                    var st = Settlement.Find(kv.Key);
                    if (st == null || st.SiegeEvent == null || Math.Abs(st.SiegeEvent.SiegeStartTime.ToHours - kv.Value.Start) > 0.05 || kv.Key.IndexOfAny(Bad) >= 0) continue;
                    if (!f) sb.Append(';'); f = false;
                    sb.Append(kv.Key).Append(':').Append(D(kv.Value.Start)).Append(':').Append(kv.Value.Held).Append(':').Append(kv.Value.Lost);
                }
                sb.Append('|'); f = true;
                double now = Today;
                foreach (var kv in _cool)
                {
                    if (kv.Value.End <= now) continue;
                    var x = kv.Key.Split('|');
                    if (x.Length != 2 || x[0].IndexOfAny(Bad) >= 0 || x[1].IndexOfAny(Bad) >= 0) continue;
                    if (!f) sb.Append(';'); f = false;
                    sb.Append(x[0]).Append(':').Append(x[1]).Append(':').Append(D(kv.Value.Start)).Append(':').Append(D(kv.Value.End));
                }
                sb.Append('|'); f = true;
                foreach (var kv in _liars)
                {
                    if (kv.Key.IndexOfAny(Bad) >= 0) continue;
                    if (!f) sb.Append(';'); f = false;
                    sb.Append(kv.Key).Append(':').Append(kv.Value.ToString(Inv));
                }
                sb.Append('|'); f = true;
                var all = new List<Deal>(_queue);
                if (_offer != null) all.Add(_offer);
                for (int i = 0; i < all.Count; i++)
                {
                    var d = all[i];
                    if (d.St.IndexOfAny(Bad) >= 0 || d.Lord.IndexOfAny(Bad) >= 0) continue;
                    if (!f) sb.Append(';'); f = false;
                    sb.Append(ReferenceEquals(d, _offer) ? 'O' : 'Q').Append(':').Append(d.St).Append(':').Append(d.Lord).Append(':').Append(D(d.Start)).Append(':').Append(d.Betray ? '1' : '0')
                      .Append(':').Append(D(d.Score)).Append(':').Append(D(d.P)).Append(':').Append(D(d.Day));
                }
                return sb.ToString();
            }
            catch (Exception e) { Stumble("Export", e); return ""; }
        }

        internal static void Import(string data)
        {
            Reset();
            int bad = 0;
            try
            {
                if (string.IsNullOrEmpty(data)) return;
                var f = data.Split('|');
                if (f.Length < 5 || f[0] != "v1") { Log.Info(Tag + ": zapis nieczytelny - od zera."); return; }
                foreach (var p in f[1].Split(';'))
                {
                    if (p.Length == 0) continue;
                    var x = p.Split(':'); double s; int h, l;
                    if (x.Length == 4 && P(x[1], out s) && int.TryParse(x[2], out h) && int.TryParse(x[3], out l)) _recs[x[0]] = new Rec { Start = s, Held = h, Lost = l }; else bad++;
                }
                foreach (var p in f[2].Split(';'))
                {
                    if (p.Length == 0) continue;
                    var x = p.Split(':'); double s, e;
                    if (x.Length == 4 && P(x[2], out s) && P(x[3], out e)) _cool[x[0] + "|" + x[1]] = new Cool { Start = s, End = e }; else bad++;
                }
                foreach (var p in f[3].Split(';'))
                {
                    if (p.Length == 0) continue;
                    var x = p.Split(':'); int dd;
                    if (x.Length == 2 && int.TryParse(x[1], out dd)) _liars[x[0]] = dd; else bad++;
                }
                foreach (var p in f[4].Split(';'))
                {
                    if (p.Length == 0) continue;
                    var x = p.Split(':'); double s, sc, pp, dy;
                    if (x.Length == 8 && (x[0] == "Q" || x[0] == "O") && P(x[3], out s) && P(x[5], out sc) && P(x[6], out pp) && P(x[7], out dy))
                    {
                        var d = new Deal { St = x[1], Lord = x[2], Start = s, Betray = x[4] == "1", Score = sc, P = pp, Day = dy };
                        if (x[0] == "O") _offer = d; else _queue.Add(d);
                    }
                    else bad++;
                }
                if (_recs.Count + _cool.Count + _liars.Count + _queue.Count > 0 || _offer != null || bad > 0)
                    Log.Info(Tag + ": wczytano z zapisu - oblezenia " + _recs.Count + ", przerwy " + _cool.Count + ", klamcy " + _liars.Count + ", kolejka " + _queue.Count + (_offer != null ? ", oferta w menu" : "") + (bad > 0 ? " (bledne " + bad + ")" : "") + ".");
            }
            catch (Exception e) { Stumble("Import", e); }
        }
    }
}
