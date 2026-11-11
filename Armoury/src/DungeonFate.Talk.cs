using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 409 (Jeff 11.11 "rob 3" - los jencow w lochu; PLAN-396 commit 409): DungeonFate 3 - WYROK GRACZA nad jencem-lordem.
    ///
    /// Linia gracza w hero_main_options: "I have decided what is to be done with you." Warunek: klucz Captive Fate; warunek gry z linii
    /// "I have decided to free you" (jeniec w druzynie rodu gracza albo w osadzie rodu gracza, nie tuz po bitwie); IsLord z rodem; nie dziecko;
    /// inna frakcja niz gracza; nie rod gracza / towarzysz; NIE wladca ani nastepca (ci maja 366 CaptiveTerms); nie zakladnik 385c; nie w kolejce.
    ///
    /// SLOWO RODU (klucz Captive Fate Parole): tylko GLOWA RODU, frakcja jenca w wojnie z graczem, rod nie w wasni, wymiana jencow 384 wlaczona
    /// (to ona pilnuje paktu). Zgoda deterministyczna: S = min(dni niewoli, 30) + (chory w lochu ? 20 : 0) + relacja / 5 + 5 x Honor
    /// - 15 x max(0, Valor); zgoda przy S >= 25; odmowa = przerwa 7 dob na tego jenca. Wykonanie: EndCaptivityAction.ApplyByReleasedByChoice +
    /// PrisonerTerms384.AddHousePact (wpis "c:"+rod, zasieg "*", Prisoner Exchange Pact Days) - pakt WZAJEMNY jak zadanie A 384: zlamanie przez
    /// gracza = kara 384b; zdrada rodu losowana tylko dla Honor &lt; 0 regula SiegeSurrender374.BetrayChance. Bez zlota.
    ///
    /// EGZEKUCJA (klucz Captive Fate Execution): HeroFate.TryKill(h, Executed, "sentence", MainHero, playerChoice: true) - jedno API smierci
    /// (straz fabuly, CanHeroDie, dziecko, odroczenie przy bitwie / spotkaniu / menu lochu). Bez sceny: sama smierc + komunikat; kary gry bez
    /// zmian (ExecutionRelationModel i Honor - nasluch gry na HeroKilled z zabojca = gracz). Rod ofiary na liste wasni (nigdy nie da slowa),
    /// jego wpisy paktu precz (PrisonerTerms384.DropHousePact).
    ///
    /// Skutki TYLKO do kolejki; wykonanie w TickEvent na mapie (czas plynie, bez rozmowy i misji), z ponownym sprawdzeniem. Nigdy SwitchToMenu.
    /// Odroczenie HeroFate (gracz w osadzie z jencem w druzynie, menu lochu, bitwa) - ponowna proba; po 3 dobach wyrok przepada.
    /// Zapis: wiersze F / P / Q w "arm_dungeon" v1 (DungeonFate.Export / Import). Zero latek. Potkniecia liczone - nic sie nie gasi.
    /// </summary>
    internal static partial class DungeonFate
    {
        private const int TalkPriority = 104;        // pod 366 CaptiveTerms (106) i ziemia LandSale (105), nad "I have decided to free you" (100)
        private const int ParoleRefusalDays = 7;     // przerwa po odmowie slowa
        private const double ParoleAgreeFrom = 25;   // prog zgody
        private const int SentenceKeepDays = 3;      // odraczany wyrok przepada po tylu dobach
        private const int RetryTicks = 30;           // odroczony wyrok: ponowna proba co tyle taktow mapy

        private sealed class Verdict { public string Hero = ""; public char Kind; public int Day; public bool Told; }

        private static readonly List<Verdict> _verdicts = new List<Verdict>();                                        // kolejka wyrokow
        private static readonly HashSet<string> _feud = new HashSet<string>(StringComparer.Ordinal);                  // rody w wasni z graczem (egzekucja ich lorda)
        private static readonly Dictionary<string, int> _paroleCool = new Dictionary<string, int>(StringComparer.Ordinal);   // jeniec -> doba konca przerwy po odmowie
        private static bool _vArmed;      // pierwszy takt po zapisaniu wyroku - wykonanie dopiero w nastepnym
        private static int _vWait;        // takty do ponownej proby odroczonego wyroku
        private static Hero _vTalk;       // rozmowca (tylko na czas rozmowy)
        private static bool _vAccepted;
        private static double _vScore;

        private static bool TalkEmpty { get { return _verdicts.Count == 0 && _feud.Count == 0 && _paroleCool.Count == 0; } }

        private static void TalkReset()
        {
            _verdicts.Clear(); _feud.Clear(); _paroleCool.Clear();
            _vArmed = false; _vWait = 0; _vTalk = null; _vAccepted = false; _vScore = 0;
        }

        /// <summary>Tylko odczyt: rod w wasni z graczem (gracz scial jego lorda wyrokiem).</summary>
        internal static bool Feud(Clan c) { return _feud.Count != 0 && c != null && c.StringId != null && _feud.Contains(c.StringId); }

        private static string His(Hero h, bool cap) { bool f = h != null && h.IsFemale; return cap ? (f ? "Her" : "His") : (f ? "her" : "his"); }
        private static string He(Hero h, bool cap) { bool f = h != null && h.IsFemale; return cap ? (f ? "She" : "He") : (f ? "she" : "he"); }

        // ---------------------------------------------------------------- rozmowa

        /// <summary>OnSessionLaunched (ArmouryBehavior, obok 366): linie rozmowy i linia startu. Nie rzuca.</summary>
        internal static void AddDialogs(CampaignGameStarter starter)
        {
            try
            {
                starter.AddPlayerLine("arm409_ask", "hero_main_options", "arm409_open", "{=!}I have decided what is to be done with you.", VAskCond, VAskCons, TalkPriority);
                starter.AddDialogLine("arm409_open", "arm409_open", "arm409_menu", "{=!}Then say it.", null, null, 200);
                starter.AddPlayerLine("arm409_parole", "arm409_menu", "arm409_parole_answer",
                    "{=!}Give me your house's word: no sword of yours against me or mine for {ARM409_DAYS} days, and I swear the same. Then go free.", ParoleCond, ParoleCons, 110, ParoleClick);
                starter.AddPlayerLine("arm409_exec", "arm409_menu", "arm409_exec_answer", "{=!}You will die for this. Bring the block.", ExecCond, ExecCons, 109, ExecClick);
                starter.AddPlayerLine("arm409_back", "arm409_menu", "lord_pretalk", "{=!}Never mind.", null, VClearCons, 100);
                starter.AddDialogLine("arm409_parole_yes", "arm409_parole_answer", "close_window", "{=!}You have my word, and my house's.", VYesCond, ParoleYesCons, 200);
                starter.AddDialogLine("arm409_parole_no", "arm409_parole_answer", "lord_pretalk", "{=!}I will not buy my freedom with such an oath.", VNoCond, ParoleNoCons, 199);
                starter.AddDialogLine("arm409_exec_yes", "arm409_exec_answer", "close_window", "{=!}Do it, then.", null, ExecAnswerCons, 200);
                var s = Settings.Current;
                Log.Info(Tag + " (409): rozmowa wyroku wpieta (hero_main_options, priorytet " + TalkPriority + ") | klucze: Captive Fate " + YN(s != null && s.CaptiveFate)
                         + ", slowo rodu " + YN(s != null && s.CaptiveFateParole) + " (pakt " + PrisonerTerms384.PactDays + " dob, wymiana jencow 384 " + YN(PrisonerTerms384.On) + ")"
                         + ", egzekucja " + YN(s != null && s.CaptiveFateExecution)
                         + " | z zapisu: kolejka wyrokow " + _verdicts.Count + ", rody w wasni " + _feud.Count + ", przerwy odmow " + _paroleCool.Count + " | latki 0.");
            }
            catch (Exception e) { Stumble("AddDialogs", e); }
        }

        /// <summary>Jeniec gracza - ten sam warunek co gry przy "I have decided to free you" (bez kontekstu tuz po bitwie).</summary>
        private static bool HeldByPlayer(Hero h, bool talk)
        {
            if (h == null || !h.IsPrisoner || Clan.PlayerClan == null) return false;
            if (talk)
            {
                var ctx = Campaign.Current != null ? Campaign.Current.CurrentConversationContext : ConversationContext.Default;
                if (ctx == ConversationContext.CapturedLord || ctx == ConversationContext.FreeOrCapturePrisonerHero) return false;
            }
            var p = h.PartyBelongedToAsPrisoner;
            if (p != null && p.Owner != null && p.Owner.Clan == Clan.PlayerClan) return true;
            return h.CurrentSettlement != null && h.CurrentSettlement.OwnerClan == Clan.PlayerClan;
        }

        /// <summary>Jeniec, nad ktorym gracz moze wydac wyrok (bez kontekstu rozmowy - takze przy wykonaniu). why: powod (PL) do logu.</summary>
        private static bool Sentenceable(Hero h, bool talk, out string why)
        {
            why = null;
            if (h == null || h == Hero.MainHero || !h.IsAlive || h.StringId == null) { why = "jeniec nie zyje albo nie istnieje"; return false; }
            if (!HeldByPlayer(h, talk)) { why = "jeniec juz nie u gracza"; return false; }
            if (!h.IsLord || h.Clan == null || h.Clan == Clan.PlayerClan || h.IsPlayerCompanion) { why = "nie lord obcego rodu"; return false; }
            if (h.IsChild) { why = "dziecko"; return false; }
            if (h.MapFaction == null || Hero.MainHero == null || h.MapFaction == Hero.MainHero.MapFaction) { why = "ta sama frakcja co gracz"; return false; }
            if (Ransom178.IsRulerOrHeir(h)) { why = "wladca / nastepca (rokowania 366)"; return false; }
            if (PrisonerTerms384.HostageGuardedNow(h)) { why = "zakladnik 385c"; return false; }
            return true;
        }

        private static bool VQueued(Hero h)
        {
            foreach (var v in _verdicts) if (v.Hero == h.StringId) return true;
            return false;
        }

        private static bool VAskCond()
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.CaptiveFate || (!s.CaptiveFateParole && !s.CaptiveFateExecution)) return false;
                var h = Hero.OneToOneConversationHero;
                string why;
                if (!Sentenceable(h, true, out why)) return false;
                return !VQueued(h);
            }
            catch (Exception e) { Stumble("VAskCond", e); return false; }
        }

        private static void VAskCons() { try { _vTalk = Hero.OneToOneConversationHero; _vAccepted = false; _vScore = 0; } catch (Exception e) { Stumble("VAskCons", e); } }
        private static void VClearCons() { _vTalk = null; }

        private static bool ParoleCond()
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.CaptiveFateParole || _vTalk == null) return false;
                MBTextManager.SetTextVariable("ARM409_DAYS", PrisonerTerms384.PactDays);
                return true;
            }
            catch (Exception e) { Stumble("ParoleCond", e); return false; }
        }

        /// <summary>Slowo rodu mozliwe? why (EN) - dymek nieczynnej linii; pl - powod do logu.</summary>
        private static bool ParoleOpen(Hero h, out string why, out string pl)
        {
            why = null; pl = null;
            if (h == null || h.Clan == null) { why = "Not now."; pl = "brak rodu"; return false; }
            if (h.IsChild) { why = He(h, true) + " is too young."; pl = "dziecko"; return false; }
            if (h.Clan.Leader != h) { why = "Only the head of a house can give " + His(h, false) + " house's word."; pl = "nie glowa rodu"; return false; }
            if (Feud(h.Clan)) { why = His(h, true) + " house has sworn never to deal with you."; pl = "rod w wasni"; return false; }
            if (!PrisonerTerms384.On) { why = "Oaths sworn over prisoners are switched off (Prisoner Exchange For Terms)."; pl = "wymiana jencow 384 wylaczona"; return false; }
            var mf = Hero.MainHero != null ? Hero.MainHero.MapFaction : null;
            if (mf == null || h.MapFaction == null || !FactionManager.IsAtWarAgainstFaction(h.MapFaction, mf))
            { why = "There is no war between you to swear peace over."; pl = "brak wojny z graczem"; return false; }
            return true;
        }

        private static bool ParoleClick(out TextObject why)
        {
            why = null;
            try
            {
                string en, pl;
                if (!ParoleOpen(_vTalk, out en, out pl)) { why = new TextObject("{=!}" + en); return false; }
                int until;
                if (_paroleCool.TryGetValue(_vTalk.StringId, out until) && until > Today())
                {
                    why = new TextObject("{=!}" + He(_vTalk, true) + " has refused this already. Ask again in " + (until - Today()).ToString(Inv) + " days.");
                    return false;
                }
                return true;
            }
            catch (Exception e) { Stumble("ParoleClick", e); why = new TextObject("{=!}Not now."); return false; }
        }

        /// <summary>Wynik zgody S (deterministyczny) i skladniki do logu.</summary>
        private static double ParoleScore(Hero h, out string parts)
        {
            double days = 0;
            try { days = (CampaignTime.Now - h.CaptivityStartTime).ToDays; } catch { days = 0; }
            if (double.IsNaN(days) || days < 0) days = 0;
            if (days > 30) days = 30;
            bool sick = Sick(h);
            double rel = 0;
            try { rel = h.GetRelationWithPlayer(); } catch { rel = 0; }
            int honor = SiegeSurrender374.Trait(h, DefaultTraits.Honor), valor = SiegeSurrender374.Trait(h, DefaultTraits.Valor);
            double sc = days + (sick ? 20 : 0) + rel / 5.0 + 5 * honor - 15 * Math.Max(0, valor);
            parts = "dni niewoli " + days.ToString("0", Inv) + (sick ? ", chory +20" : "") + ", relacja " + rel.ToString("0", Inv) + " -> " + (rel / 5.0).ToString("0.#", Inv)
                    + ", Honor " + honor + " -> " + (5 * honor) + ", Valor " + valor + " -> " + (-15 * Math.Max(0, valor));
            return sc;
        }

        private static void ParoleCons()
        {
            try
            {
                _vAccepted = false;
                var h = _vTalk;
                if (h == null) return;
                string parts;
                _vScore = ParoleScore(h, out parts);
                bool yes = _vScore >= ParoleAgreeFrom;
                Log.Info(Tag + ": WYROK parole " + Nm(h) + " [" + h.StringId + "] - " + (yes ? "zgoda" : "odmowa") + " (S=" + _vScore.ToString("0.#", Inv) + ", prog " + ParoleAgreeFrom.ToString("0", Inv)
                         + ": " + parts + ")" + (yes ? " | do kolejki, wykonanie w takcie mapy." : " | przerwa do doby " + (Today() + ParoleRefusalDays) + "."));
                // 409-p: stan na samym koncu (po logu) - wyjatek wyzej nie zostawi wyroku w kolejce przy odpowiedzi "odmowa"
                if (yes)
                {
                    _verdicts.Add(new Verdict { Hero = h.StringId, Kind = 'P', Day = Today() });
                    _vArmed = false; _vWait = 0;
                }
                else _paroleCool[h.StringId] = Today() + ParoleRefusalDays;
                _vAccepted = yes;
            }
            catch (Exception e) { Stumble("ParoleCons", e); _vAccepted = false; }
        }

        private static bool VYesCond() { return _vAccepted; }
        private static bool VNoCond() { return !_vAccepted; }

        private static void ParoleYesCons()
        {
            try { if (_vTalk != null) Log.Player(Nm(_vTalk) + " gives " + His(_vTalk, false) + " house's word. " + He(_vTalk, true) + " will go free as soon as you part."); } catch { }
            _vTalk = null;
        }

        private static void ParoleNoCons()
        {
            try { if (_vTalk != null) Log.Player(Nm(_vTalk) + " refuses to give " + His(_vTalk, false) + " house's word. You may ask again in " + ParoleRefusalDays.ToString(Inv) + " days.", true); } catch { }
            _vTalk = null;
        }

        private static bool ExecCond()
        {
            var s = Settings.Current;
            return s != null && s.CaptiveFateExecution && _vTalk != null;
        }

        private static bool ExecClick(out TextObject why)
        {
            why = null;
            try
            {
                var h = _vTalk;
                if (h == null) { why = new TextObject("{=!}Not now."); return false; }
                if (h.IsChild) { why = new TextObject("{=!}" + He(h, true) + " is too young."); return false; }
                string pl;
                if (!StoryGuard.MayLeaveStory(h, out pl)) { why = new TextObject("{=!}" + His(h, true) + " part in what is to come is not yet played."); return false; }
                // 409-p: glowa rodu bez dziedzica - gra usunelaby z nia caly rod (DestroyClanAction.ApplyByClanLeaderDeath -> ApplyByRemove). HeroFate.TryKill
                // odmawia wtedy, gdy w rodzie zyje postac fabuly (403-p) - linia ma byc nieczynna z dymkiem, a nie przepadac po rozmowie
                if (h.Clan != null && h.Clan.Leader == h && HeroFate.LastOfHouse(h) && StoryGuard.ClanHoldsStory(h.Clan, h, out pl))
                { why = new TextObject("{=!}" + His(h, true) + " house cannot end yet: one of its blood has a part still to play."); return false; }
                if (h.DeathMark != KillCharacterAction.KillCharacterActionDetail.None || !h.CanDie(KillCharacterAction.KillCharacterActionDetail.Executed))
                { why = new TextObject("{=!}" + He(h, true) + " cannot be put to death now."); return false; }
                return true;
            }
            catch (Exception e) { Stumble("ExecClick", e); why = new TextObject("{=!}Not now."); return false; }
        }

        private static void ExecCons()
        {
            try
            {
                var h = _vTalk;
                if (h == null) return;
                _verdicts.Add(new Verdict { Hero = h.StringId, Kind = 'E', Day = Today() });
                _vArmed = false; _vWait = 0;
                Log.Info(Tag + ": WYROK egzekucja " + Nm(h) + " [" + h.StringId + "] (rod " + (h.Clan != null && h.Clan.Name != null ? h.Clan.Name.ToString() : "?") + ") - do kolejki, wykonanie w takcie mapy.");
            }
            catch (Exception e) { Stumble("ExecCons", e); }
        }

        private static void ExecAnswerCons()
        {
            try { if (_vTalk != null) Log.Player(Nm(_vTalk) + " will be put to death as soon as you have left.", true); } catch { }
            _vTalk = null;
        }

        // ---------------------------------------------------------------- wykonanie (takt mapy)

        /// <summary>CampaignEvents.TickEvent: pusta kolejka = jedno porownanie. Jeden wyrok na takt. Nie rzuca.
        /// 409-p: gra wola TickEvent takze w dwoch pierwszych taktach po starcie przy stojacym czasie (Campaign.Tick: _dt > 0 || CurrentTickCount &lt; 3,
        /// cs_full.cs:10094) - wyrok z zapisu wykonalby sie w chwili wczytania. Dlatego tylko przy dt > 0 (czas plynie).</summary>
        internal static void TalkTick(float dt)
        {
            if (_verdicts.Count == 0) return;
            if (!(dt > 0f)) return;
            try
            {
                if (Campaign.Current == null || Mission.Current != null) return;
                var cm = Campaign.Current.ConversationManager;
                if (cm != null && (cm.IsConversationInProgress || cm.IsConversationFlowActive)) return;
                if (!_vArmed) { _vArmed = true; return; }
                if (_vWait > 0) { _vWait--; return; }
                var v = _verdicts[0];
                _verdicts.RemoveAt(0);   // najpierw z kolejki - wyjatek nie powtorzy wyroku
                if (!RunVerdict(v)) { _verdicts.Add(v); _vWait = RetryTicks; }   // odroczony: na koniec kolejki, ponowna proba za chwile
            }
            catch (Exception e) { Stumble("TalkTick", e); }
        }

        private static void VDrop(Verdict v, Hero h, string en, string pl)
        {
            Log.Info(Tag + ": WYROK " + (v.Kind == 'P' ? "parole" : "egzekucja") + " " + Nm(h) + " [" + v.Hero + "] - PRZEPADL (" + pl + ").");
            if (h != null && en != null) Log.Player(en, true);
        }

        /// <summary>Wykonanie jednego wyroku z ponownym sprawdzeniem. false = odroczony (zostaje w kolejce); true = wykonany albo przepadl.</summary>
        private static bool RunVerdict(Verdict v)
        {
            Hero h = null;
            try { h = Hero.Find(v.Hero); } catch { h = null; }
            try
            {
                var s = Settings.Current;
                string why;
                if (!Sentenceable(h, false, out why)) { VDrop(v, h, h != null && h.IsAlive ? "Your sentence on " + Nm(h) + " comes to nothing: " + He(h, false) + " is no longer your captive." : null, why); return true; }
                if (s == null || !s.CaptiveFate) { VDrop(v, h, null, "klucz Captive Fate NIE"); return true; }
                bool late = Today() - v.Day > SentenceKeepDays;
                // 409-p: gracz sam w niewoli nie wykonuje wyrokow - odroczenie, po SentenceKeepDays wyrok przepada
                if (Hero.MainHero != null && Hero.MainHero.IsPrisoner)
                {
                    if (late) { VDrop(v, h, "Your sentence on " + Nm(h) + " was never carried out: you are a captive yourself.", "gracz w niewoli dluzej niz " + SentenceKeepDays + " doby"); return true; }
                    if (!v.Told) { v.Told = true; Log.Info(Tag + ": WYROK " + (v.Kind == 'P' ? "parole" : "egzekucja") + " " + Nm(h) + " [" + v.Hero + "] - odroczone (gracz w niewoli), ponowna proba co " + RetryTicks + " taktow mapy (najdluzej " + SentenceKeepDays + " doby)."); }
                    return false;
                }
                if (v.Kind == 'P') return RunParole(v, h, s, late);
                if (v.Kind == 'E') return RunExecution(v, h, s, late);
                VDrop(v, h, null, "nieznany rodzaj wyroku");
                return true;
            }
            catch (Exception e) { Stumble("RunVerdict", e); return true; }   // blad = wyrok przepada (nigdy petla)
        }

        private static bool RunParole(Verdict v, Hero h, Settings s, bool late)
        {
            string en, pl;
            if (!s.CaptiveFateParole) { VDrop(v, h, null, "klucz Captive Fate Parole NIE"); return true; }
            if (!ParoleOpen(h, out en, out pl)) { VDrop(v, h, "The oath of " + Nm(h) + " comes to nothing: " + en, pl); return true; }
            var cap = h.PartyBelongedToAsPrisoner;
            if (cap != null && cap.MapEvent != null)
            {
                if (late) { VDrop(v, h, "The oath of " + Nm(h) + " comes to nothing amid the fighting.", "bitwa pojmujacego dluzej niz " + SentenceKeepDays + " doby"); return true; }
                if (!v.Told) { v.Told = true; Log.Info(Tag + ": WYROK parole " + Nm(h) + " [" + v.Hero + "] - odroczone (bitwa pojmujacego), ponowna proba co " + RetryTicks + " taktow mapy (najdluzej " + SentenceKeepDays + " doby)."); }
                return false;
            }
            var clan = h.Clan;
            int days = PrisonerTerms384.PactDays;
            double p = SiegeSurrender374.BetrayChance(h);   // 0 dla Honor >= 0
            bool betray = p > 0 && MBRandom.RandomFloat < p;
            EndCaptivityAction.ApplyByReleasedByChoice(h, Hero.MainHero);
            if (h.IsPrisoner) { VDrop(v, h, null, "gra nie uwolnila jenca - bez paktu"); return true; }
            int until = PrisonerTerms384.AddHousePact(clan, h, days, betray);
            Log.Info(Tag + ": WYROK parole " + Nm(h) + " [" + h.StringId + "] - WYKONANE: uwolniony (ReleasedByChoice), pakt rodu [c:" + clan.StringId + "] "
                     + (until >= 0 ? "do doby " + until + " (" + days + " dob)" : "NIE ZAPISANY (blad - patrz log bledow)")
                     + (betray ? " - ZDRADA wylosowana (p " + (p * 100).ToString("0", Inv) + "%): wpis tylko pilnowany" : p > 0 ? " - uczciwy mimo Honor < 0 (p zdrady " + (p * 100).ToString("0", Inv) + "%)" : "") + " | bez zlota.");
            Log.Player(Nm(h) + " has sworn peace for " + days.ToString(Inv) + " days and gone free.");
            return true;
        }

        private static bool RunExecution(Verdict v, Hero h, Settings s, bool late)
        {
            if (!s.CaptiveFateExecution) { VDrop(v, h, null, "klucz Captive Fate Execution NIE"); return true; }
            var clan = h.Clan;
            string name = Nm(h), clanName = clan != null && clan.Name != null ? clan.Name.ToString() : "?", clanId = clan != null ? clan.StringId : null;
            var cap = h.PartyBelongedToAsPrisoner;
            Settlement st = cap != null && cap.IsSettlement ? cap.Settlement : h.CurrentSettlement;
            string why;
            var fate = HeroFate.TryKill(h, KillCharacterAction.KillCharacterActionDetail.Executed, LordToll.Sentence, Hero.MainHero, true, out why);
            if (fate == HeroFate.Fate.Deferred)
            {
                if (late) { VDrop(v, h, "Your sentence on " + name + " was never carried out.", (why ?? "odroczone") + " dluzej niz " + SentenceKeepDays + " doby"); return true; }
                if (!v.Told) { v.Told = true; Log.Info(Tag + ": WYROK egzekucja " + name + " [" + v.Hero + "] - " + (why ?? "odroczone") + ", ponowna proba co " + RetryTicks + " taktow mapy (najdluzej " + SentenceKeepDays + " doby)."); }
                return false;
            }
            if (fate != HeroFate.Fate.Killed && h.IsAlive && clan != Clan.PlayerClan && h.Clan == Clan.PlayerClan)
            {
                // 409-p: gracz z rodu Bialych Wedrowcow - prefiks ROT na KillCharacterAction.ApplyByExecution (rot_all.cs:21215-21248) zamiast smierci
                // przemienia ofiare, uwalnia ja i przenosi do rodu i druzyny gracza. To nie zgon (LordToll nie liczy), ale rod ofiary stracil lorda z wyroku gracza.
                string oathW = SentenceOnHouse(clan, clanId, name);
                Log.Info(Tag + ": WYROK egzekucja " + name + " [" + v.Hero + "] - PRZEJETY PRZEZ ROT (gracz Bialy Wedrowiec): przemieniony i wcielony do rodu gracza, bez smierci | rod " + clanName + " [" + (clanId ?? "-") + "] w wasni (rodow w wasni " + _feud.Count + "), " + oathW + ".");
                Log.Player(name + " rises again to serve you.", true);
                return true;
            }
            if (fate != HeroFate.Fate.Killed)
            {
                bool story = why != null && why.StartsWith("straz fabuly", StringComparison.Ordinal);
                VDrop(v, h, story ? name + " is spared: " + His(h, false) + " part in what is to come is not yet played." : "Your sentence on " + name + " could not be carried out.", why ?? "odmowa");
                return true;
            }
            string oath = SentenceOnHouse(clan, clanId, name);
            Log.Info(Tag + ": WYROK egzekucja " + name + " [" + v.Hero + "] - WYKONANE (" + why + ") w " + (st != null ? st.StringId : "druzynie gracza") + " | rod " + clanName + " [" + (clanId ?? "-") + "] w wasni (rodow w wasni " + _feud.Count
                     + "), " + oath + " | kary gry (relacje, Honor) bez zmian | LordToll rok " + LordToll.Year + ": wyrok " + LordToll.Count(LordToll.Sentence) + ", wspolny " + LordToll.Total + "/" + LordToll.MaxAll + ".");
            Log.Player(name + " has been put to death " + (st != null ? "at " + Nm(st) : "in your camp") + ".", true);
            return true;
        }

        /// <summary>409-p: skutki wyroku smierci dla rodu ofiary - rod w wasni; czynna przysiega wzajemna z tym rodem (slowo rodu 409 / pakt 384 z kluczem
        /// rodu) jest ZLAMANA przez gracza (kara 384b: relacja z przysiegajacym -20, Honor -500 xp, komunikat), potem pozostale wpisy rodu precz. Opis do logu.</summary>
        private static string SentenceOnHouse(Clan clan, string clanId, string victim)
        {
            if (clanId == null) return "bez rodu";
            _feud.Add(clanId);
            bool broke = PrisonerTerms384.ExecutionBreaksPact(clan, "egzekucja " + victim);
            int dropped = PrisonerTerms384.DropHousePact(clan);
            return (broke ? "PRZYSIEGA WZAJEMNA ZLAMANA (kara 384b), " : "") + "wpisy paktu zdjete " + dropped;
        }

        // ---------------------------------------------------------------- doba / zapis

        private static void TalkDaily(int day)
        {
            if (_paroleCool.Count == 0) return;
            try
            {
                List<string> gone = null;
                foreach (var kv in _paroleCool) if (kv.Value <= day) { if (gone == null) gone = new List<string>(); gone.Add(kv.Key); }
                if (gone != null) foreach (var k in gone) _paroleCool.Remove(k);
            }
            catch (Exception e) { Stumble("TalkDaily", e); }
        }

        /// <summary>Wiersze "F|rod", "P|bohater|dobaKonca", "Q|bohater|rodzaj|doba" (DLL sprzed 409 liczy je jako bledne wiersze i pomija).</summary>
        private static void TalkExport(StringBuilder sb)
        {
            foreach (var c in _feud) if (Ok(c)) sb.Append('\n').Append("F|").Append(c);
            foreach (var kv in _paroleCool) if (Ok(kv.Key)) sb.Append('\n').Append("P|").Append(kv.Key).Append('|').Append(kv.Value.ToString(Inv));
            foreach (var v in _verdicts) if (Ok(v.Hero)) sb.Append('\n').Append("Q|").Append(v.Hero).Append('|').Append(v.Kind).Append('|').Append(v.Day.ToString(Inv));
        }

        /// <summary>true = wiersz 409 (wczytany albo pominiety jako zly); false = nie nasz. Rzuca przy zlej liczbie (Import liczy bledny wiersz).</summary>
        private static bool TalkImport(string[] p)
        {
            if (p[0] == "F") { if (p.Length >= 2 && p[1].Length > 0) _feud.Add(p[1]); return true; }
            if (p[0] == "P") { if (p.Length >= 3 && p[1].Length > 0) _paroleCool[p[1]] = int.Parse(p[2], Inv); return true; }
            if (p[0] == "Q")
            {
                if (p.Length >= 4 && p[1].Length > 0 && (p[2] == "P" || p[2] == "E")) _verdicts.Add(new Verdict { Hero = p[1], Kind = p[2][0], Day = int.Parse(p[3], Inv) });
                return true;
            }
            return false;
        }
    }
}
