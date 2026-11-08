using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 366 (Jeff 08.11: "jak schwytam wrogiego krola to czy moge w nim w niewoli rozmawiac ... wynegocjowac dodatkowe warunki"). ROKOWANIA Z WIELKIM JENCEM - ETAP 1.
    /// Klucz CaptiveTerms (wylaczony = rozmowa jak dotad, bez nowej linii).
    ///
    /// Gdzie: linia gracza w hero_main_options "Let us speak of the terms of your release." - rozmowca to jeniec gracza (partia gracza albo loch osady rodu gracza;
    /// ten sam warunek co gry "I have decided to free you", cs conversation_player_let_prisoner_go_on_condition) i krol albo nastepca tronu (Ransom178.IsRulerOrHeir -
    /// ta sama regula co wielcy jency 178), z innej frakcji niz gracz. Rozmowa z ekranu druzyny i w lochu twierdzy.
    /// Warunki etapu 1:
    ///  R "Your crown will pay a king's ransom, doubled." - okup x2 przeplywem Ransom178 (skarbiec korony jenca albo rod jenca -> skarbiec korony, na ktora jeniec
    ///    przeszedl przy pojmaniu, albo gracz; brak gotowki - dlug korony / dlug okupu, raty; pomoc wasali), nagroda gracza 1/10 takze od nadwyzki; uwolnienie ApplyByRansom.
    ///  T "Confirm my conquests, and you go free." - tytuly de iure BK osad, ktore gracz trzyma de facto (twierdze rodu gracza i wsie ich okregow), a ktorych posiadaczem
    ///    de iure jest krol albo czlonek jego rodu (tylko to krol moze oddac; tytuly jego wasali - nie, etap 2). Przeniesienie BK TitleManager.InheritTitle (de iure, de facto
    ///    gdy BK trzyma je na tym samym posiadaczu) - NIE GrantTitle: GrantTitle daje obdarowanemu relacje +5..+150 wedlug rangi, wplywy i slawe nadajacemu i wola
    ///    ChangeOwnerOfSettlementAction.ApplyByGift na osadzie, ktora gracz juz trzyma (zdarzenie zmiany wlasciciela bez zmiany). Uwolnienie ApplyByReleasedByChoice.
    ///    Jeniec przeszedl na korone pana gracza (178) - pan traci okup: wybor w rozmowie - gracz placi panu (kiesa gracza -> skarbiec pana) wartosc utraconego okupu,
    ///    albo pan sie gniewa (relacja -1 za kazda dobe wplywow korony pana, ktora przepadla; 3..30). Niewyplacona nagroda 1/10 gracza za tego jenca skreslona (korona
    ///    nie dostanie okupu, z ktorego ja placila) - wartosc do zaplaty panu = okup - skreslona nagroda (pan wychodzi dokladnie jak przy okupie).
    /// Zgoda: zgoda = wolnosc + strach + relacja + perswazja - koszt warunku - duma; > 0 przyjmuje (deterministycznie). Podpowiedz w rozmowie slownie (pasma wokol zera,
    /// pasmo "either way" obejmuje zero). Odmowa: 7 dob przerwy na ten warunek u tego jenca; zgoda < -20 (obraza) - relacja z jencem -2..-5.
    /// Wykonanie odroczone: skutek rozmowy tylko zapisuje umowe w kolejce; wykonanie w takcie mapy (TickEvent - czas plynie: rozmowa, ekran druzyny i menu bez czasu
    /// zamkniete), bez misji i bez rozmowy. Przed wykonaniem wszystko od nowa (jeniec dalej u gracza, tytuly, kiesa). Bez SwitchToMenu, bez LeaveEncounter
    /// (uwolnienie nie dzieje sie w rozmowie - w lochu gracz zostaje w twierdzy). Kurier okupu nie zadziala drugi raz: po uwolnieniu gra czysci oferte
    /// (RansomOfferCampaignBehavior.OnHeroPrisonerReleased), a Ransom178 wpis wielkiego jenca (OnPrisonerReleased).
    /// Log: "ROKOWANIA (366): ..." przy kazdej probie i przy wykonaniu; linia startu przy wpieciu rozmowy. Zapis "arm_terms366": przerwy odmow i kolejka umow.
    /// 367a: "Confirm my conquests" z roszczeniami wasali (CaptiveTerms.Claims.cs) - krol mowi, ile chca jego lordowie (zloto albo slub); gracz wybiera pakiet
    /// albo "Only your own seal" (T jak wyzej). Warunek 'V' w kolejce; zapis v2 (Import czyta v1).
    /// </summary>
    internal static partial class CaptiveTerms
    {
        internal const string Tag = "ROKOWANIA (366)";
        private const int AskPriority = 106;   // nad "I have decided to free you" (100) i ziemia LandSale (105)
        private const double CooldownDays = 7.0;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        internal static bool On { get { var s = Settings.Current; return s != null && s.CaptiveTerms; } }

        private enum Liege { None = 0, Pay = 1, Anger = 2 }

        private sealed class Deal { public string Hero = ""; public char Cond; public Liege Mode; public long Owed; public double Score; public string Claims = ""; }   // 367: Claims - roszczenia wasali (Cond 'V')

        private static readonly Dictionary<string, double> _cool = new Dictionary<string, double>();   // "jeniec:R" -> doba konca przerwy
        private static readonly List<Deal> _queue = new List<Deal>();
        private static bool _armed;   // pierwszy takt po zapisaniu umowy - wykonanie dopiero w nastepnym
        private static int _stumbles;
        private static readonly HashSet<string> _err = new HashSet<string>();

        // stan rozmowy (tylko w trakcie rozmowy)
        private static Hero _talk;
        private static char _cond;
        private static Score _sc;
        private static bool _needLiege, _accepted;
        private static long _owed, _ransom2;
        private static List<object> _titles = new List<object>();
        private static string _titleNames = "-";

        internal static void Reset()
        {
            _cool.Clear(); _queue.Clear(); _armed = false; _stumbles = 0; _err.Clear(); ClearTalk();
        }

        private static void ClearTalk()
        {
            _talk = null; _cond = ' '; _sc = null; _needLiege = false; _accepted = false; _owed = 0; _ransom2 = 0; _titles = new List<object>(); _titleNames = "-";
            ClearClaims();   // 367
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_err.Add(where)) Log.Error("CaptiveTerms." + where, e); } catch { }
        }

        private static string Name(Hero h) { try { return h != null && h.Name != null ? h.Name.ToString() : "-"; } catch { return "?"; } }
        private static string F(double v) { return v.ToString("0.#", Inv); }

        // ------------------------------------------------------------ rozmowa
        internal static void AddDialogs(CampaignGameStarter starter)
        {
            try
            {
                if (starter == null) return;
                starter.AddPlayerLine("arm366_ask", "hero_main_options", "arm366_open", "{=!}Let us speak of the terms of your release.", AskCond, AskCons, AskPriority);
                starter.AddDialogLine("arm366_open", "arm366_open", "arm366_menu", "{=!}Terms? Speak, then. I am listening.", null, null, 200);
                starter.AddPlayerLine("arm366_r", "arm366_menu", "arm366_hint", "{=!}Your crown will pay a king's ransom, doubled.", RCond, PickR, 110, RClick);
                starter.AddPlayerLine("arm366_t", "arm366_menu", "arm366_hint", "{=!}Confirm my conquests, and you go free.", T1Cond, PickT, 109, TClick);
                // 367a: ta sama linia, gdy sa roszczenia wasali - krol najpierw mowi, ile chca jego lordowie
                starter.AddPlayerLine("arm367_t", "arm366_menu", "arm367_claims", "{=!}Confirm my conquests, and you go free.", T2Cond, null, 109, TClick);
                starter.AddDialogLine("arm367_claims", "arm367_claims", "arm367_pick", "{=!}{ARM367_CLAIMS}", ClaimsLineCond, null, 200);
                starter.AddPlayerLine("arm367_gold", "arm367_pick", "arm366_hint", "{=!}I will pay them all in gold ({ARM367_TOTAL} denars).", GoldPkgCond, PickGold, 110, GoldPkgClick);
                starter.AddPlayerLine("arm367_wed", "arm367_pick", "arm366_hint", "{=!}{ARM367_WED}", WedPkgCond, PickWed, 109, WedPkgClick);
                starter.AddPlayerLine("arm367_own", "arm367_pick", "arm366_hint", "{=!}Only your own seal, then.", OwnSealCond, PickT, 108);
                starter.AddPlayerLine("arm367_back", "arm367_pick", "lord_pretalk", "{=!}Never mind.", null, ClearTalkCons, 100);
                starter.AddPlayerLine("arm366_menu_back", "arm366_menu", "lord_pretalk", "{=!}Never mind.", null, ClearTalkCons, 100);
                starter.AddDialogLine("arm366_hint", "arm366_hint", "arm366_confirm", "{=!}{ARM366_HINT}", HintCond, null, 200);
                starter.AddPlayerLine("arm366_go", "arm366_confirm", "arm366_answer", "{=!}Then swear to it, and it is done.", GoCond, GoCons, 110, GoClick);
                starter.AddPlayerLine("arm366_pay", "arm366_confirm", "arm366_answer", "{=!}I will pay my liege the ransom he is owed ({ARM366_OWED} denars).", LiegeCond, PayCons, 109, PayClick);
                starter.AddPlayerLine("arm366_anger", "arm366_confirm", "arm366_answer", "{=!}My liege will have to accept it.", LiegeCond, AngerCons, 108, GoClick);
                starter.AddPlayerLine("arm366_confirm_back", "arm366_confirm", "lord_pretalk", "{=!}Never mind.", null, ClearTalkCons, 100);
                starter.AddDialogLine("arm366_yes", "arm366_answer", "close_window", "{=!}{ARM366_ANSWER}", YesCond, YesCons, 200);
                starter.AddDialogLine("arm366_no", "arm366_answer", "lord_pretalk", "{=!}{ARM366_ANSWER}", NoCond, NoCons, 199);
                var s = Settings.Current;
                Log.Info(Tag + ": rozmowa wpieta (hero_main_options, priorytet " + AskPriority + ") | klucz " + (s != null && s.CaptiveTerms ? "TAK" : "NIE")
                         + " | BK tytuly " + (LandBk.Ready ? "TAK" : "NIE") + " | okupy 178 " + (Ransom178.FlowsOn ? "TAK" : "NIE")
                         + " | 367 roszczenia wasali " + (s != null && s.CaptiveTermsVassalTitles ? "TAK (lata " + F(s.CaptiveTitleClaimYears) + ")" : "NIE")
                         + " | kolejka umow " + _queue.Count + ", przerwy odmow " + _cool.Count + ".");
            }
            catch (Exception e) { Stumble("AddDialogs", e); }
        }

        /// <summary>Jeniec gracza - ten sam warunek co gry przy "I have decided to free you" (bez kontekstu tuz po bitwie).</summary>
        private static bool HeldByPlayer(Hero h)
        {
            if (h == null || !h.IsPrisoner || Clan.PlayerClan == null) return false;
            var ctx = Campaign.Current != null ? Campaign.Current.CurrentConversationContext : ConversationContext.Default;
            if (ctx == ConversationContext.CapturedLord || ctx == ConversationContext.FreeOrCapturePrisonerHero) return false;
            var p = h.PartyBelongedToAsPrisoner;
            if (p != null && p.Owner != null && p.Owner.Clan == Clan.PlayerClan) return true;
            return h.CurrentSettlement != null && h.CurrentSettlement.OwnerClan == Clan.PlayerClan;
        }

        private static bool Queued(Hero h)
        {
            foreach (var d in _queue) if (d.Hero == h.StringId) return true;
            return false;
        }

        private static bool AskCond()
        {
            try
            {
                if (!On) return false;
                var h = Hero.OneToOneConversationHero;
                if (h == null || h == Hero.MainHero || !h.IsAlive || !h.IsLord || h.StringId == null || !HeldByPlayer(h)) return false;
                if (h.MapFaction == null || h.MapFaction == Hero.MainHero.MapFaction || !Ransom178.IsRulerOrHeir(h)) return false;
                return !Queued(h);
            }
            catch (Exception e) { Stumble("AskCond", e); return false; }
        }

        private static void AskCons() { try { ClearTalk(); _talk = Hero.OneToOneConversationHero; } catch (Exception e) { Stumble("AskCons", e); } }
        private static void ClearTalkCons() { ClearTalk(); }

        private static double CoolLeft(Hero h, char c)
        {
            double end;
            if (h == null || !_cool.TryGetValue(h.StringId + ":" + c, out end)) return 0;
            double left = end - CampaignTime.Now.ToDays;
            return left > 0 ? left : 0;
        }

        private static bool RCond() { return _talk != null; }
        private static bool RClick(out TextObject why)
        {
            why = null;
            try
            {
                if (!Ransom178.FlowsOn) { why = new TextObject("{=!}Ransoms by wealth are switched off."); return false; }
                if (Ransom178.PriceFor(_talk) <= 0) { why = new TextObject("{=!}His crown has no income to pay a ransom with."); return false; }
                double cl = CoolLeft(_talk, 'R');
                if (cl > 0) { why = new TextObject("{=!}He has refused this already. Ask again in " + Math.Ceiling(cl).ToString(Inv) + " days."); return false; }
                return true;
            }
            catch (Exception e) { Stumble("RClick", e); why = new TextObject("{=!}Not now."); return false; }
        }

        private static bool TCond() { return _talk != null; }
        private static bool TClick(out TextObject why)
        {
            why = null;
            try
            {
                if (!LandBk.Ready) { why = new TextObject("{=!}There are no feudal titles to confirm (Banner Kings)."); return false; }
                string names;
                var l = TitlesFor(_talk, out names);
                if (l.Count == 0 && !HasClaims()) { why = new TextObject(ClaimsOn ? "{=!}Neither he nor any lord of his realm holds a title to anything you have taken." : "{=!}He holds no title to anything you have taken - those titles belong to his vassals."); return false; }
                var oc = OtherCrown(_talk);
                if (oc != null) { why = new TextObject("{=!}He is the captive of " + oc.Name + ", a crown you no longer serve. You cannot trade away its ransom."); return false; }
                double cl = CoolLeft(_talk, 'T');
                if (cl > 0) { why = new TextObject("{=!}He has refused this already. Ask again in " + Math.Ceiling(cl).ToString(Inv) + " days."); return false; }
                return true;
            }
            catch (Exception e) { Stumble("TClick", e); why = new TextObject("{=!}Not now."); return false; }
        }

        private static void PickR() { Pick('R'); }
        private static void PickT() { Pick('T'); }

        private static void Pick(char c)
        {
            try
            {
                var h = _talk;
                if (h == null) return;
                _cond = c;
                if (c != 'V') { _pkg = new List<Pick367>(); _pkgKind = ' '; _pkgGold = 0; }   // 367: "Only your own seal" po spisie - bez pakietu
                _titles = c == 'T' || c == 'V' ? TitlesFor(h, out _titleNames) : new List<object>();
                _ransom2 = c == 'R' ? 2 * Ransom178.PriceFor(h) : 0;
                _sc = Evaluate(h, c, _titles);
                _needLiege = false; _owed = 0;
                if (c == 'T' || c == 'V') LiegeOwed(h, out _needLiege, out _owed, out _);
            }
            catch (Exception e) { Stumble("Pick", e); _sc = null; }
        }

        private static bool HintCond()
        {
            try
            {
                if (_talk == null || _sc == null) { MBTextManager.SetTextVariable("ARM366_HINT", "I cannot think on it now."); return true; }
                string mood, word;
                Band(_sc.Total, out mood, out word);
                var sb = new StringBuilder(200);
                sb.Append(mood).Append(" (Chance: ").Append(word).Append('.');
                if (_cond == 'R') sb.Append(" Ransom: ").Append(_ransom2.ToString("N0", Inv)).Append(" denars, double the usual.");
                else if (_cond == 'V' && _titles.Count == 0) sb.Append(" His own seal covers nothing you hold.");
                else sb.Append(" His seal can confirm: ").Append(_titleNames).Append('.');
                if (_cond == 'V') sb.Append(PkgHint());   // 367
                if ((_cond == 'T' || _cond == 'V') && _needLiege) sb.Append(" He is your liege's captive - your liege loses his ransom.");
                sb.Append(')');
                MBTextManager.SetTextVariable("ARM366_HINT", sb.ToString());
                MBTextManager.SetTextVariable("ARM366_OWED", _owed.ToString("N0", Inv));
                return true;
            }
            catch (Exception e) { Stumble("HintCond", e); MBTextManager.SetTextVariable("ARM366_HINT", "..."); return true; }
        }

        /// <summary>Pasma podpowiedzi (przyblizone - gracz nie widzi liczby; pasmo "either way" obejmuje zero).</summary>
        private static void Band(double v, out string mood, out string word)
        {
            if (v >= 20) { mood = "He nods slowly. He seems ready to agree"; word = "very likely"; }
            else if (v >= 5) { mood = "He weighs your words carefully"; word = "likely"; }
            else if (v > -5) { mood = "He hesitates. It could go either way"; word = "uncertain"; }
            else if (v > -20) { mood = "He frowns at the offer"; word = "unlikely"; }
            else { mood = "He scoffs at the offer"; word = "very unlikely"; }
        }

        private static bool TLike { get { return _cond == 'T' || _cond == 'V'; } }
        private static bool GoCond() { return _sc != null && !(TLike && _needLiege); }
        private static bool LiegeCond() { return _sc != null && TLike && _needLiege; }
        private static bool PayClick(out TextObject why)
        {
            why = null;
            long need = _owed + (_cond == 'V' ? _pkgGold : 0);   // 367: pan i lordowie z jednej kiesy
            if (Hero.MainHero != null && Hero.MainHero.Gold >= need) return true;
            why = new TextObject("{=!}You do not have " + need.ToString("N0", Inv) + " denars.");
            return false;
        }
        /// <summary>367: zaplata lordom pakietu (bez zaplaty panu).</summary>
        private static bool GoClick(out TextObject why)
        {
            why = null;
            if (_cond != 'V' || _pkgGold <= 0 || (Hero.MainHero != null && Hero.MainHero.Gold >= _pkgGold)) return true;
            why = new TextObject("{=!}You do not have " + _pkgGold.ToString("N0", Inv) + " denars for his lords.");
            return false;
        }

        private static void GoCons() { Decide(Liege.None); }
        private static void PayCons() { Decide(Liege.Pay); }
        private static void AngerCons() { Decide(Liege.Anger); }

        private static void Decide(Liege mode)
        {
            try
            {
                var h = _talk;
                if (h == null || _sc == null) { _accepted = false; MBTextManager.SetTextVariable("ARM366_ANSWER", "Not now."); return; }
                _sc = Evaluate(h, _cond, _titles);   // od nowa - deterministycznie, ten sam wynik co w podpowiedzi
                _accepted = _sc.Total > 0;
                string ans;
                if (_accepted) ans = _cond == 'R' ? "Very well. My treasury will pay what you ask. You have my word."
                                   : _cond == 'V' ? "So be it. My seal, and my lords' quitclaims - what you hold, you hold by right. Now let me go."
                                   : "So be it. What you hold, you hold by right - I will put my seal to it. Now let me go.";
                else ans = _sc.Total < -20 ? "You insult me. I would sooner rot in this cell." : "No. I will not buy my freedom at that price.";
                MBTextManager.SetTextVariable("ARM366_ANSWER", ans);
                string effect;
                if (_accepted)
                {
                    _queue.Add(new Deal { Hero = h.StringId, Cond = _cond, Mode = TLike && _needLiege ? mode : Liege.None, Owed = _owed, Score = _sc.Total, Claims = _cond == 'V' ? PkgCode() : "" });
                    _armed = false;
                    effect = "umowa w kolejce (wykonanie po rozmowie, w takcie mapy)" + (TLike && _needLiege ? (mode == Liege.Pay ? "; gracz zaplaci panu " + _owed : "; pan straci okup " + _owed + " - gniew") : "");
                }
                else
                {
                    _cool[h.StringId + ":" + (_cond == 'V' ? 'T' : _cond)] = CampaignTime.Now.ToDays + CooldownDays;   // 367: odmowa krola przy V - ta sama przerwa co T (TClick)
                    int drop = 0;
                    if (_sc.Total < -20) { drop = (int)Math.Max(2, Math.Min(5, Math.Round(-_sc.Total / 10.0))); ChangeRelationAction.ApplyPlayerRelation(h, -drop); }
                    effect = "odmowa - przerwa " + CooldownDays + " dob na ten warunek" + (drop > 0 ? ", obraza: relacja z jencem -" + drop : "");
                }
                if (_cond == 'V') { int nc = CoolRefusers(h); if (nc > 0) effect += "; odmowy lordow - przerwa " + CooldownDays + " dob: " + nc; }   // 367
                Log.Info(Tag + ": " + (h.MapFaction != null && h.MapFaction.Leader == h ? "krol " : "nastepca tronu ") + Name(h) + " (" + h.MapFaction.Name + ") | warunek "
                         + (_cond == 'R' ? "R (okup x2, " + _ransom2 + " zl)" : _cond == 'V' ? "V (367: pieczec " + _titleNames + "; pakiet " + (_pkgKind == 'M' ? "slub" : "zloto") + " " + _pkgGold + " zl: " + PkgPl() + ")" : "T (tytuly: " + _titleNames + ")") + " | wynik " + F(_sc.Total) + (_accepted ? " - PRZYJMUJE" : " - ODMAWIA")
                         + " | skladniki: " + _sc.Parts + " | skutek: " + effect + ".");
            }
            catch (Exception e) { Stumble("Decide", e); _accepted = false; MBTextManager.SetTextVariable("ARM366_ANSWER", "Not now."); }
        }

        private static bool YesCond() { return _accepted; }
        private static bool NoCond() { return !_accepted; }
        private static void YesCons()
        {
            try { Log.Player(Name(_talk) + " agrees to your terms. It will be done as soon as you part."); } catch { }
            ClearTalk();
        }
        private static void NoCons()
        {
            try { Log.Player(Name(_talk) + " refuses your terms. You may ask again in " + CooldownDays.ToString(Inv) + " days.", true); } catch { }
            ClearTalk();
        }

        // ------------------------------------------------------------ ocena zgody
        private sealed class Score { public double Total; public string Parts = "-"; }

        private static int Trait(Hero h, TraitObject t) { try { return t != null ? h.GetTraitLevel(t) : 0; } catch { return 0; } }

        // 366 popr.: bez pamieci podrecznej - SkillObject z poprzedniej gry (wczytanie innego zapisu bez restartu) dawal 0; GetObject jest tani, ocena rzadka
        private static int Lordship(Hero h)
        {
            try
            {
                var sk = Game.Current != null && Game.Current.ObjectManager != null ? Game.Current.ObjectManager.GetObject<SkillObject>("Lordship") : null;   // BK BKSkills.Lordship
                return sk != null ? h.GetSkillValue(sk) : 0;
            }
            catch { return 0; }
        }

        /// <summary>zgoda = wolnosc + strach + relacja + perswazja - koszt warunku - duma (punkty; > 0 przyjmuje).</summary>
        private static Score Evaluate(Hero h, char cond, List<object> titles)
        {
            var sc = new Score();
            var me = Hero.MainHero;
            var k = h.MapFaction as Kingdom;
            long price = Ransom178.PriceFor(h);
            // wolnosc: 30 + pol punktu za dobe niewoli (do +30); dwor bogaty (skarbiec pokrywa zwykly okup) wykupi go i tak - mniej desperacji
            double days = 0;
            try { days = Math.Max(0, (CampaignTime.Now - h.CaptivityStartTime).ToDays); } catch { }
            bool rich = k != null && k.Leader == h && price > 0 && k.KingdomBudgetWallet >= price;
            double W = 30 + Math.Min(30, days * 0.5) - (rich ? 10 : 0);
            // strach: stosunek sil frakcji gracza do frakcji jenca (+-25), wojna z wieloma krolestwami +5
            double ratio = 1, Fr = 0; int wars = 0;
            try
            {
                float mine = me.MapFaction != null ? me.MapFaction.CurrentTotalStrength : 0f, his = h.MapFaction.CurrentTotalStrength;
                ratio = his > 1f ? mine / his : 2.0;
                if (me.MapFaction != null && FactionManager.IsAtWarAgainstFaction(me.MapFaction, h.MapFaction)) Fr = Math.Max(-25, Math.Min(25, 25 * (ratio - 1)));
                var list = h.MapFaction.FactionsAtWarWith;
                if (list != null) foreach (var o in list) if (o != null && o.IsKingdomFaction) wars++;
                if (wars >= 2) Fr += 5;
            }
            catch { }
            int rel = 0;
            try { rel = h.GetRelation(me); } catch { }
            double R = Math.Max(-100, Math.Min(100, rel)) * 0.25;
            int charm = 0, lord = Lordship(me);
            try { charm = me.GetSkillValue(DefaultSkills.Charm); } catch { }
            double P = charm / 10.0 + lord / 20.0;
            // koszt warunku
            double C;
            string cost;
            if (cond == 'R')
            {
                long extra = price;   // druga polowa okupu x2
                double wallet = k != null && k.Leader == h ? Math.Max(1, (double)k.KingdomBudgetWallet) : Math.Max(1, (double)ClanIncomeBook.FamilyGoldOf(h.Clan));
                C = 20 + 30 * Math.Min(1.5, extra / wallet);
                cost = "okup x2: dodatek " + extra + " / " + (k != null && k.Leader == h ? "skarbiec " : "kiesy rodu ") + Math.Round(wallet);
            }
            else
            {
                int towns = 0, castles = 0, villages = 0;
                foreach (var t in titles) { var st = LandBk.Fief(t); if (st == null) continue; if (st.IsTown) towns++; else if (st.IsCastle) castles++; else villages++; }
                C = 10 + 15 * towns + 10 * castles + 3 * villages;
                cost = "tytuly: miasta " + towns + ", zamki " + castles + ", wsie " + villages;
                if (cond == 'V') { string vp; C += VassalCost(out vp); cost += "; " + vp; }   // 367: prosba do wlasnych lordow
            }
            // duma: krol 10, mestwo +8 za stopien, wyrachowanie -5 za stopien, honor -3 za stopien (honorowy przyjmuje uczciwy uklad; nieuczciwy - mniej skrupulow, ale i
            // mniej wstydu: honor ujemny podnosi dume - nie ufa ukladom)
            int valor = Trait(h, DefaultTraits.Valor), calc = Trait(h, DefaultTraits.Calculating), honor = Trait(h, DefaultTraits.Honor);
            double D = 10 + 8 * Math.Max(0, valor) - 5 * Math.Max(0, calc) - 3 * honor;
            sc.Total = W + Fr + R + P - C - D;
            sc.Parts = "wolnosc " + F(W) + " (dni " + F(days) + (rich ? ", dwor bogaty -10" : "") + "), strach " + F(Fr) + " (sila " + ratio.ToString("0.00", Inv) + ", wojen " + wars + ")"
                     + ", relacja " + F(R) + " (" + rel + "), perswazja " + F(P) + " (Charm " + charm + ", Lordship " + lord + "), koszt " + F(C) + " (" + cost + ")"
                     + ", duma " + F(D) + " (Valor " + valor + ", Calculating " + calc + ", Honor " + honor + ")";
            return sc;
        }

        // ------------------------------------------------------------ tytuly (T) i pan gracza
        /// <summary>Tytuly BK osad, ktore gracz trzyma de facto (twierdze rodu gracza, wsie ich okregow), z posiadaczem de iure = jeniec albo czlonek jego rodu.</summary>
        private static List<object> TitlesFor(Hero king, out string names)
        {
            var l = new List<object>();
            var sb = new StringBuilder();
            names = "-";
            if (king == null || king.Clan == null || Clan.PlayerClan == null || !LandBk.Ready) return l;
            foreach (var st in Settlement.All)
            {
                try
                {
                    if (st == null) continue;
                    Clan holder = null;
                    if (st.IsTown || st.IsCastle) holder = st.OwnerClan;
                    else if (st.IsVillage && st.Village != null && st.Village.Bound != null) holder = st.Village.Bound.OwnerClan;
                    if (holder != Clan.PlayerClan) continue;
                    var t = LandBk.Title(st);
                    var dj = LandBk.DeJure(t);
                    if (dj == null || !dj.IsAlive || dj.Clan == Clan.PlayerClan) continue;
                    if (dj != king && dj.Clan != king.Clan) continue;
                    l.Add(t);
                    if (sb.Length > 0) sb.Append(", ");
                    sb.Append(st.Name);
                }
                catch (Exception e) { Stumble("TitlesFor", e); }
            }
            if (sb.Length > 0) names = sb.ToString();
            return l;
        }

        /// <summary>366 popr.: jeniec przeszedl przy pojmaniu na korone, ktorej gracz juz nie sluzy (zmiana krolestwa albo gracz bez krolestwa) - T zablokowane: ta korona
        /// stracilaby okup bez zaplaty i bez gniewu, a nagroda gracza od niej dalej by czekala (dziura w zamknietej ekonomii). R bez zmian (okup idzie do niej).</summary>
        private static Kingdom OtherCrown(Hero h)
        {
            var crown = Ransom178.CaptiveCrown(h);
            var mine = Clan.PlayerClan != null ? Clan.PlayerClan.Kingdom : null;
            return crown != null && crown != mine ? crown : null;
        }

        /// <summary>Czy jeniec jest zdobycza korony pana gracza (178) i ile pan traci przy uwolnieniu bez okupu: okup - niewyplacona nagroda gracza za niego.</summary>
        private static Kingdom LiegeOwed(Hero h, out bool need, out long owed, out long rewardCut)
        {
            need = false; owed = 0; rewardCut = 0;
            var crown = Ransom178.CaptiveCrown(h);
            var mine = Clan.PlayerClan != null ? Clan.PlayerClan.Kingdom : null;
            if (crown == null || mine == null || crown != mine || crown.Leader == null || crown.Leader == Hero.MainHero) return crown;
            long price = Ransom178.PriceFor(h);
            var s = Settings.Current;
            long est = (long)(Math.Max(0f, Math.Min(1f, s.CrownGreatCaptiveReward)) * price);
            rewardCut = Math.Min(est, Ransom178.PendingReward(Hero.MainHero, crown));
            owed = Math.Max(0, price - rewardCut);
            need = owed > 0;
            return crown;
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
                if (!_armed) { _armed = true; return; }
                var d = _queue[0];
                _queue.RemoveAt(0);   // najpierw z kolejki - wyjatek nie powtorzy umowy (i nie da okupu drugi raz)
                Run(d);
            }
            catch (Exception e) { Stumble("Tick", e); }
        }

        private static void Drop(Deal d, Hero h, string en, string pl)
        {
            Log.Info(Tag + ": umowa PRZEPADLA - " + Name(h) + " | warunek " + d.Cond + " | " + pl + ".");
            if (h != null) Log.Player("The terms agreed with " + Name(h) + " fell through: " + en, true);
        }

        private static void Run(Deal d)
        {
            Hero h = null;
            try { h = Hero.Find(d.Hero); } catch { h = null; }
            try
            {
                if (h == null || !h.IsAlive) { Drop(d, h, "he is no more.", "jeniec nie zyje albo nie istnieje"); return; }
                if (!h.IsPrisoner || !(HeldUnderPlayer(h))) { Drop(d, h, "he is no longer your captive.", "jeniec juz nie u gracza"); return; }
                if (d.Cond == 'R') RunR(d, h); else if (d.Cond == 'V') RunV(d, h); else RunT(d, h);
            }
            catch (Exception e) { Stumble("Run", e); }
        }

        /// <summary>Jak HeldByPlayer, bez kontekstu rozmowy (wykonanie po rozmowie).</summary>
        private static bool HeldUnderPlayer(Hero h)
        {
            var p = h.PartyBelongedToAsPrisoner;
            if (p != null && p.Owner != null && p.Owner.Clan == Clan.PlayerClan) return true;
            return h.CurrentSettlement != null && h.CurrentSettlement.OwnerClan == Clan.PlayerClan;
        }

        private static void RunR(Deal d, Hero h)
        {
            long price, extra; string to;
            var rc = Ransom178.CaptiveCrown(h);   // przed okupem - po uwolnieniu wpis wielkiego jenca znika
            bool crownIsMine = rc != null && rc.Leader == Hero.MainHero;
            var payer = h.Clan != null ? (h.Clan.Leader != h ? h.Clan.Leader : null) : null;
            if (!Ransom178.TermsRansom(h, 2.0, out price, out extra, out to)) { Drop(d, h, "his house could not settle the ransom.", "okup 178 bez umowy (przeplyw odmowil)"); return; }
            EndCaptivityAction.ApplyByRansom(h, payer ?? (h.Clan != null ? h.Clan.Leader : h));
            Log.Info(Tag + ": WYKONANE - " + Name(h) + " | R okup x2 " + price + " zl -> " + to + (extra > 0 ? ", nagroda gracza od nadwyzki " + extra + " (kolejka nagrod 178)" : "") + " | uwolniony (okup).");
            Log.Player(Name(h) + " is set free. His ransom, doubled (" + price.ToString("N0", Inv) + " denars), goes to " + (to.StartsWith("skarbiec") ? (crownIsMine ? "your treasury" : rc != null ? "the treasury of " + rc.Name : "your liege's treasury") : "you")
                       + (extra > 0 ? "; your reward grows by " + extra.ToString("N0", Inv) + " denars." : "."));
        }

        private static void RunT(Deal d, Hero h)
        {
            string names;
            var titles = TitlesFor(h, out names);
            if (titles.Count == 0) { Drop(d, h, "his seal no longer covers anything you hold.", "brak tytulow do przeniesienia"); return; }
            var oc = OtherCrown(h);
            if (oc != null) { Drop(d, h, "he is now the captive of " + oc.Name + ", a crown you no longer serve.", "jeniec korony " + oc.Name + ", ktorej gracz juz nie sluzy"); return; }
            bool need; long owed, cut;
            var crown = LiegeOwed(h, out need, out owed, out cut);
            var me = Hero.MainHero;
            // 366 popr. (L3): pan traci okup, a w rozmowie gracz nie widzial wyboru (stan zmienil sie miedzy rozmowa a wykonaniem) - bez kary bez wyboru
            if (need && d.Mode == Liege.None) { Drop(d, h, "your liege now has a claim to his ransom - speak to him again.", "pan traci okup, a w rozmowie nie bylo wyboru (stan zmienil sie po rozmowie)"); return; }
            // 366 popr. (R2-3): gracz placi kwote z umowy; zmiana wartosci okupu ponad 5% - umowa przepada (nowa rozmowa pokaze nowa kwote)
            if (need && d.Mode == Liege.Pay)
            {
                if (d.Owed > 0 && Math.Abs(owed - d.Owed) > 0.05 * d.Owed) { Drop(d, h, "the ransom your liege is owed has changed (" + owed.ToString("N0", Inv) + " denars now) - speak to him again.", "dlug wobec pana zmienil sie: umowa " + d.Owed + ", teraz " + owed); return; }
                if (d.Owed > 0) owed = d.Owed;
                if (me.Gold < owed) { Drop(d, h, "you no longer have " + owed.ToString("N0", Inv) + " denars for your liege.", "za malo zlota gracza dla pana (" + me.Gold + " < " + owed + ")"); return; }
            }
            // 366 popr. (R2-1): najpierw tytuly - nic nie przeszlo = umowa przepada bez zaplaty, bez gniewu, bez skreslenia nagrody i bez uwolnienia (jeniec zostaje)
            int moved = 0, failed = 0;
            foreach (var t in titles)
            {
                var from = LandBk.DeJure(t);
                if (from != null && LandBk.Inherit(from, me, t) && LandBk.DeJure(t) == me) moved++; else failed++;
            }
            if (moved == 0) { Drop(d, h, "his seal could not confirm your conquests. He stays your captive.", "BK nie przenioslo zadnego tytulu (" + failed + ") - bez zaplaty, bez uwolnienia"); return; }
            string liege = SettleLiege(d, crown, need, owed);
            long cancelled = crown != null && crown.Leader != me ? Ransom178.CancelReward(me, crown, cut) : 0;
            EndCaptivityAction.ApplyByReleasedByChoice(h, me);
            Log.Info(Tag + ": WYKONANE - " + Name(h) + " | T tytuly de iure -> gracz: " + moved + " (" + names + ")" + (failed > 0 ? ", nieudane " + failed : "")
                     + " | " + liege + (cancelled > 0 ? " | nagroda gracza skreslona " + cancelled : "") + " | uwolniony (z wyboru).");
            Log.Player(Name(h) + " goes free and puts his seal to your conquests: " + names + (failed > 0 ? " (" + failed + " could not be confirmed)" : "") + ".");
        }

        /// <summary>366 (wydzielone w 367): pan gracza traci okup - zaplata gracza (kiesa -> skarbiec pana) albo gniew. Zwraca opis do logu.</summary>
        private static string SettleLiege(Deal d, Kingdom crown, bool need, long owed)
        {
            var me = Hero.MainHero;
            string liege = "-";
            if (need && crown != null)
            {
                var lord = crown.Leader;
                if (d.Mode == Liege.Pay)
                {
                    int io = (int)Math.Min(int.MaxValue, owed);
                    me.ChangeHeroGold(-io);
                    CirculationWindows.NoteHeroGold(me, -io);
                    crown.KingdomBudgetWallet += io;
                    liege = "gracz zaplacil panu " + io + " (kiesa -> skarbiec " + crown.Name + ")";
                }
                else
                {
                    // gniew: -1 za kazda dobe sredniej wplywow korony pana, ktora przepada; okup krola = RansomKingDays (20) dob wplywow JEGO korony, wiec przy
                    // koronach rownych ~18, pulap 30 dopiero gdy korona jenca ~1.7 raza bogatsza od korony pana; nastepca (okup rodu) - zwykle mniej; 3..30
                    double day = Ransom178.CrownDayIncome(crown);
                    int drop = (int)Math.Max(3, Math.Min(30, Math.Round(day > 0 ? owed / day : 30)));
                    if (lord != null && lord.IsAlive)
                    {
                        ChangeRelationAction.ApplyPlayerRelation(lord, -drop);
                        liege = "pan " + Name(lord) + " traci okup " + owed + " (" + F(day > 0 ? owed / day : 0) + " dob wplywow korony) - relacja -" + drop;
                        Log.Player(Name(lord) + " is angered: you let his royal captive go without the ransom he was owed.", true);
                    }
                    else liege = "korona " + crown.Name + " traci okup " + owed + " - pan nie zyje, relacja bez zmian";
                }
            }
            return liege;
        }

        // ------------------------------------------------------------ zapis (SaveText, "arm_terms366")
        private static readonly char[] Bad = { '|', ';', ':' };

        /// <summary>367: "v2|jeniec:warunek[:posiadacz]:doba;...|jeniec:warunek:tryb:dlug:wynik:roszczenia;..." (roszczenia - PkgCode, bez | ; :). Import czyta tez v1.</summary>
        internal static string Export()
        {
            try
            {
                var sb = new StringBuilder(128);
                sb.Append("v2|");
                bool f = true;
                double now = Campaign.Current != null ? CampaignTime.Now.ToDays : 0;
                foreach (var kv in _cool)
                {
                    if (kv.Value <= now) continue;
                    var x = kv.Key.Split(':');
                    if ((x.Length != 2 && x.Length != 3) || x[0].IndexOfAny(Bad) >= 0) continue;
                    if (!f) sb.Append(';'); f = false;
                    sb.Append(kv.Key).Append(':').Append(kv.Value.ToString("0.###", Inv));
                }
                sb.Append('|'); f = true;
                foreach (var d in _queue)
                {
                    if (d.Hero.IndexOfAny(Bad) >= 0 || (d.Claims ?? "").IndexOfAny(Bad) >= 0) continue;
                    if (!f) sb.Append(';'); f = false;
                    sb.Append(d.Hero).Append(':').Append(d.Cond).Append(':').Append((int)d.Mode).Append(':').Append(d.Owed.ToString(Inv)).Append(':').Append(d.Score.ToString("0.#", Inv))
                      .Append(':').Append(d.Claims ?? "");
                }
                return sb.ToString();
            }
            catch (Exception e) { Stumble("Export", e); return ""; }
        }

        internal static void Import(string data)
        {
            _cool.Clear(); _queue.Clear(); _armed = false; ClearTalk();
            int n = 0, bad = 0;
            try
            {
                if (string.IsNullOrEmpty(data)) return;
                var f = data.Split('|');
                if (f.Length < 3 || (f[0] != "v1" && f[0] != "v2")) { Log.Info(Tag + ": zapis nieczytelny - od zera."); return; }
                bool v2 = f[0] == "v2";
                foreach (var p in f[1].Split(';'))
                {
                    if (p.Length == 0) continue;
                    var x = p.Split(':'); double v;
                    if (x.Length == 3 && x[1].Length == 1 && double.TryParse(x[2], NumberStyles.Float, Inv, out v)) { _cool[x[0] + ":" + x[1]] = v; n++; }
                    else if (v2 && x.Length == 4 && x[1] == "V" && x[2].Length > 0 && double.TryParse(x[3], NumberStyles.Float, Inv, out v)) { _cool[x[0] + ":V:" + x[2]] = v; n++; }
                    else bad++;
                }
                foreach (var p in f[2].Split(';'))
                {
                    if (p.Length == 0) continue;
                    var x = p.Split(':'); int m; long o; double sc;
                    bool shape = x.Length == 5 || (v2 && x.Length == 6);
                    if (shape && x[1].Length == 1 && (x[1][0] == 'R' || x[1][0] == 'T' || (v2 && x[1][0] == 'V')) && int.TryParse(x[2], out m) && long.TryParse(x[3], NumberStyles.Integer, Inv, out o)
                        && double.TryParse(x[4], NumberStyles.Float, Inv, out sc))
                    {
                        string cl = x.Length == 6 ? x[5] : "";
                        if (x[1][0] == 'V' && (cl.Length == 0 || !ValidCode(cl))) { bad++; continue; }
                        _queue.Add(new Deal { Hero = x[0], Cond = x[1][0], Mode = (Liege)Math.Max(0, Math.Min(2, m)), Owed = o, Score = sc, Claims = cl }); n++;
                    }
                    else bad++;
                }
                if (n > 0 || bad > 0) Log.Info(Tag + ": wczytano z zapisu (" + f[0] + ") - przerwy odmow " + _cool.Count + ", umowy w kolejce " + _queue.Count + (bad > 0 ? " (bledne " + bad + ")" : "") + ".");
            }
            catch (Exception e) { Stumble("Import", e); }
        }
    }
}
