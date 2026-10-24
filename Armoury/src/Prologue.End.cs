using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// m101-prolog E2b - KONIEC PROLOGU (projekt pkt 2: dzien 186 - smierc Roberta, Joffrey krolem, Ned w lochu; dzien 188 = dzien 0 ROT, egzekucja Neda).
    /// (1) Doba >= KingFallsDay prologu (Daily): najpierw pakty prologu gasna (EndPacts - takze, gdy krol jest w bitwie), potem krol umiera "od ran z polowania na dzika": AddDeathMark(WoundedInBattle)
    ///     + KillCharacterAction.ApplyByDeathMarkForced (wymuszona - nasza ochrona CanHeroDie i cudze weta nie blokuja). Krol w bitwie albo oblezeniu (jego druzyna albo
    ///     strona, ktora trzyma go w niewoli) - czekamy do nastepnej doby (proba co dobe bez limitu dnia, recenzja R2). Sukcesja (recenzja R5): PRZED smiercia krola
    ///     Joffrey glowa rodu clan_nord_1 (ChangeClanLeaderAction.ApplyWithSelectedNewLeader - jak ROT JoffreyEvent), wiec krol ginie jako zwykly czlonek rodu: gra nie
    ///     losuje dziedzica i nie dodaje glosowania o tron (KingSelectionKingdomDecision - premia ROT x999 mnozy tylko ocene wystawiana przez rod panujacy, nie daje mu
    ///     tronu); Joffrey martwy albo poza rodem - sukcesje robi gra. Nazwa rodu wraca do oryginalu z XML ("Baratheon, Joffrey").
    /// (2) Ned jencem korony w Krolewskiej Przystani (town_EW4): druzyna rozwiazana (jego armia - DisbandArmyAction), TakePrisonerAction do lochu osady; Ned w bitwie -
    ///     nastepna doba; jeniec kogo innego - najpierw uwolniony (EndCaptivityAction.ApplyByEscape). Pilnowane co dobe do egzekucji (pokoj gry wypuszcza jencow przy
    ///     zawarciu pokoju - wtedy Ned wraca do lochu nastepnej doby).
    /// (3) NedEvent ROT (dzien 2 osi opowiesci, x4.33) zaklada martwego Neda - prefiks NedEvent.StartEvent, PRZED ROTEvents.OnNedDeath (wojny Robba ROT widza juz
    ///     Robba): najpierw Robb glowa Starkow (ChangeClanLeaderAction - recenzja R5, bez glosowania o tron Polnocy), wiec Robb wladca Polnocy; potem zywy Ned ->
    ///     KillCharacterAction.ApplyByExecution(Ned, Joffrey, pokaz, isForced). Ned w walce - tylko znak smierci, gra zabija go po walce (NedMarkFollowUp pilnuje
    ///     co dobe). Latka wpinana leniwie z reszta latek prologu.
    /// Klasyczny start: nic (latki nie ma, Active = false).
    /// </summary>
    internal static partial class Prologue
    {
        private const string KingsLandingId = "town_EW4";
        private static bool _kingFallen, _nedDone, _endLogged, _nedMarked;   // _nedMarked: egzekucja Neda w walce - znak smierci, umrze po walce (recenzja R3/R5)
        private static int _kingWaits, _nedArrests;
        private static string _pactsEndText = "";   // wynik EndPacts z dnia 186 (do linii KONCA PROLOGU, gdy krol umrze pozniej)

        private static void ResetEnd() { _kingFallen = false; _nedDone = false; _endLogged = false; _kingWaits = 0; _nedArrests = 0; _pactsEndText = ""; _nedMarked = false; _nr = ""; }

        private static string PackEnd()
        {
            return "kf=" + (_kingFallen ? "1" : "0") + ";nd=" + (_nedDone ? "1" : "0") + ";nm=" + (_nedMarked ? "1" : "0") + ";"
                   + (_nr.Length > 0 ? "nr=" + Clean(_nr) + ";" : "");   // m116-fabula: zwrot okupu Neda (raz)
        }

        private static void UnpackEnd(Dictionary<string, string> map)
        {
            ResetEnd();
            string v;
            _kingFallen = map.TryGetValue("kf", out v) && v == "1";
            _nedDone = map.TryGetValue("nd", out v) && v == "1";
            _nedMarked = map.TryGetValue("nm", out v) && v == "1";
            _nr = map.TryGetValue("nr", out v) ? Unesc(v) : "";
            _endLogged = _kingFallen;
        }

        // ---------------------------------------------------------------- m116-fabula: Ned bez okupu do egzekucji, zwrot okupu w zapisie Jeffa
        // Jeff 24.10 (raport z gry): "[20:06:26] Okupy (178): AI-AI - Eddard Stark ... wolny za 292902 zl (placi skarbiec The North) -> skarbiec House Baratheon of King's
        // Landing", potem KeepNedInDungeon wsadzil go z powrotem do lochu bez zwrotu zlota. Straz niewoli E5 (Prologue.World) obejmowala tylko wiersze p01/p10.
        private const string NedRefundGameId = "KkUpHftX7ZIq";   // kampania Jeffa (IronmanKkUpHftX7ZIq.sav) - jedyna, w ktorej okup Neda zostal zaplacony
        private const long NedRefundAmount = 292902;               // log 24.10 20:06:26 (gotowka 292902, dlug 0)
        private static string _nr = "";                            // zapis arm_prologue "nr": "" = jeszcze nie sprawdzone, "done:<przeniesione>", "none:<powod>"

        private static bool NedKey { get { var s = S; return s != null && s.PrologueNedNoRansom; } }

        /// <summary>m116: Ned (ROTlord_512) jencem po koncu prologu, przed egzekucja (NedEvent), w czasie ochrony - ani okupu, ani wyjscia (poza smiercia, "po bitwie",
        /// naszym aresztem i graczem jako sprawca - CaptivityPrefix).</summary>
        internal static bool NedHeld(Hero h)
        {
            return Active && h != null && h.StringId == NedId && NedKey && h.IsAlive && h.IsPrisoner && _kingFallen && _kingsAlive && !_nedDone && NedProtected();
        }

        /// <summary>m116: jednorazowy zwrot okupu Neda w kampanii Jeffa - skarbiec korony (vlandia, dostala okup) -> skarbiec Polnocy (battania, zaplacila). Wzor
        /// BookHoldings.RefundTreasury: najpierw platnik, potem odbiorca; migawka 165 i historia E1 przesuniete u obu stron. Raz (znacznik nr, takze "none"). Nie rzuca.</summary>
        private static void TryNedRefund()
        {
            try
            {
                if (_nr.Length > 0) return;
                var s = S;
                if (s == null || !s.PrologueNedRansomRefund) return;
                string gid = "";
                try { gid = Campaign.Current != null ? Campaign.Current.UniqueGameId ?? "" : ""; } catch { gid = ""; }
                if (gid != NedRefundGameId) { _nr = "none:inna-kampania"; return; }
                var ned = H(NedId);
                if (!_kingFallen || !(_nedDone || (ned != null && !ned.IsAlive))) return;   // czeka do egzekucji
                var from = K(Crown); var to = K(NorthId);
                if (from == null || to == null || from.IsEliminated || to.IsEliminated)
                {
                    _nr = "none:brak-krolestw";
                    Log.Info(Tag + "zwrot okupu Neda: nie dotyczy (korona " + (from != null && !from.IsEliminated ? "jest" : "BRAK") + ", Polnoc " + (to != null && !to.IsEliminated ? "jest" : "BRAK") + ").");
                    return;
                }
                long w0 = from.KingdomBudgetWallet, v0 = to.KingdomBudgetWallet;
                long take = Math.Max(0, Math.Min(NedRefundAmount, w0));
                take = Math.Min(take, Math.Max(0, (long)int.MaxValue - Math.Max(0, v0)));   // skarbiec odbiorcy to int
                long moved = 0, got = 0;
                string e1 = "";
                if (take > 0)
                {
                    from.KingdomBudgetWallet -= (int)take;   // najpierw platnik, potem odbiorca: odbiorca dostaje dokladnie to, co zeszlo
                    moved = w0 - from.KingdomBudgetWallet;
                    to.KingdomBudgetWallet += (int)moved;
                    got = (long)to.KingdomBudgetWallet - v0;
                    CrownIncome.ShiftSnapshot(to, got);      // 165: zwrot skarbu, nie wplyw dnia
                    CrownIncome.ShiftSnapshot(from, -moved); // 165: ubytek skarbu, nie "ujemny wplyw"
                    e1 = " | historia skarbca E1 (CrashScribe): " + BookHoldings.E1Text(BookHoldings.E1Shift(to, got), to.Name.ToString())
                         + ", " + BookHoldings.E1Text(BookHoldings.E1Shift(from, -moved), from.Name.ToString());
                }
                _nr = take > 0 ? "done:" + moved.ToString(Inv) : "none:pusty-skarbiec";
                Log.Info(Tag + "zwrot okupu Neda (m116, raz) - z " + from.Name + " (" + from.StringId + ") " + w0 + " -> " + from.KingdomBudgetWallet + " do " + to.Name + " ("
                         + to.StringId + ") " + v0 + " -> " + to.KingdomBudgetWallet + " | nalezne " + NedRefundAmount + ", zdjete " + moved + " - dopisane " + got + " = " + (moved - got)
                         + (moved < NedRefundAmount ? " (skarbiec korony nie starczyl - oddane tyle, ile bylo)" : "") + ", migawka 165 przesunieta u obu stron" + e1 + " | znacznik nr=" + _nr + ".");
                if (moved > 0)
                    Log.Player("The crown returns the " + moved.ToString("N0", Inv) + " denars the North paid for Lord Eddard's freedom, a freedom King Joffrey never granted.");
            }
            catch (Exception e) { Stumble("TryNedRefund", e); }
        }

        private static string ApplyEnd(Harmony h)
        {
            var t = AccessTools.TypeByName("ROT.Events.NedEvent");
            var m = t != null ? AccessTools.DeclaredMethod(t, "StartEvent") : null;
            if (m == null) return "BRAK (ROT.Events.NedEvent.StartEvent)";
            return h.Patch(m, prefix: new HarmonyMethod(typeof(Prologue), nameof(NedEventPrefix))) != null ? "TAK" : "BRAK";
        }

        private static bool InFight(MobileParty p) { return p != null && (p.MapEvent != null || p.SiegeEvent != null); }

        private static bool Busy(Hero h)
        {
            if (h == null) return false;
            if (InFight(h.PartyBelongedTo)) return true;
            var pp = h.PartyBelongedToAsPrisoner;
            return pp != null && pp.IsMobile && InFight(pp.MobileParty);
        }

        /// <summary>Doba prologu: koniec prologu w dniu KingFallsDay (raz), potem Ned w lochu do egzekucji.</summary>
        private static string DailyEnd()
        {
            if (!Active) return "-";
            double day = PrologueDay();
            if (!_kingFallen && day >= KingFallsDay) TryKingFalls("doba " + ((int)day).ToString(Inv));
            if (_kingFallen && _kingsAlive && !_nedDone && NedProtected()) KeepNedInDungeon();
            if (_nedMarked) NedMarkFollowUp();
            if (_nr.Length == 0) TryNedRefund();   // m116-fabula: raz, po egzekucji Neda, tylko kampania Jeffa
            return _kingFallen ? "koniec prologu TAK" + (_nedDone ? ", Ned stracony" : "") : "koniec prologu za " + Math.Max(0.0, KingFallsDay - day).ToString("0.0", Inv) + " dni";
        }

        /// <summary>Smierc krola, Joffrey krolem, Ned w lochu. Krol w bitwie - false (nastepna doba).</summary>
        private static bool TryKingFalls(string why)
        {
            var robert = H(RobertId);
            // recenzja R2: pakty gasna w dniu 186 takze wtedy, gdy krol jest w bitwie (sojusz z prologu nie dotrwa do wojen ROT po NedEvent)
            if (!_pactsEnded) _pactsEndText = EndPacts();
            if (_kingsAlive && robert != null && robert.IsAlive && Busy(robert))
            {
                _kingWaits++;
                Log.Info(Tag + "koniec prologu (" + why + "): krol Robert w bitwie albo oblezeniu - smierc krola nastepnej doby (czeka " + _kingWaits
                         + ", pakty: " + (string.IsNullOrEmpty(_pactsEndText) ? "zakonczone" : _pactsEndText) + ").");
                return false;
            }
            string pacts = string.IsNullOrEmpty(_pactsEndText) ? "juz zakonczone" : _pactsEndText;
            string died = "-";
            var crownClan = C(CrownClanId); var joff = H(JoffreyId);
            string heir = "-";
            if (_kingsAlive && robert != null && robert.IsAlive)
            {
                // recenzja R5: Joffrey glowa rodu PRZED smiercia krola (jak ROT JoffreyEvent: ChangeClanLeaderAction, potem smierc). Krol, ktory nie jest glowa rodu,
                // umiera bez losowania dziedzica i bez glosowania o tron (KillCharacterAction: KingSelectionKingdomDecision tylko przy smierci glowy rodu panujacego) -
                // korona od razu u Joffreya, niezaleznie od BK i StoryThrone. Joffrey martwy albo poza rodem - sukcesje robi gra (log "gra: ...").
                try
                {
                    if (crownClan != null && joff != null && joff.IsAlive && joff.Clan == crownClan && crownClan.Leader != joff)
                    {
                        heir = "Joffrey glowa rodu przed smiercia krola (byl " + Who(crownClan.Leader) + ")";
                        ChangeClanLeaderAction.ApplyWithSelectedNewLeader(crownClan, joff);
                    }
                }
                catch (Exception e) { Stumble("TryKingFalls.Heir", e); }
                try
                {
                    robert.AddDeathMark(null, KillCharacterAction.KillCharacterActionDetail.WoundedInBattle);
                    KillCharacterAction.ApplyByDeathMarkForced(robert, true);
                }
                catch (Exception e) { Stumble("TryKingFalls.Kill", e); }
                died = robert.IsAlive ? "NIE (dalej zyje)" : "TAK (" + robert.DeathMark + ")";
                if (robert.IsAlive) { Log.Info(Tag + "koniec prologu (" + why + "): smierc krola NIE weszla - proba nastepnej doby."); return false; }
            }
            else died = robert == null ? "brak bohatera" : (robert.IsAlive ? "nie ozywiony" : "juz martwy");
            _kingFallen = true;
            // Joffrey krolem (zapasowo: krol juz martwy przed nami albo zmiana glowy rodu wyzej nie weszla)
            try
            {
                if (crownClan != null && joff != null && joff.IsAlive && joff.Clan == crownClan && crownClan.Leader != joff)
                {
                    heir = "gra wybrala " + Who(crownClan.Leader) + " - ustawiony Joffrey";
                    ChangeClanLeaderAction.ApplyWithSelectedNewLeader(crownClan, joff);
                }
                else if (heir == "-") heir = "gra: " + Who(crownClan != null ? crownClan.Leader : null);
                if (crownClan != null && !string.IsNullOrEmpty(_crownClanName))
                {
                    var nm = new TextObject(_crownClanName);
                    crownClan.ChangeClanName(nm, nm);
                }
            }
            catch (Exception e) { Stumble("TryKingFalls.Joffrey", e); }
            var crown = K(Crown);
            bool joffKing = crown != null && joff != null && crown.Leader == joff;
            // Ned do lochu
            // krol umiera po NedEvent (byl w bitwie) - Ned juz stracony, bez drugiego aresztu
            string ned = !_kingsAlive ? "nie ozywiony" : _nedDone ? "juz stracony (NedEvent)" : ArrestNed();
            _endLogged = true;
            Log.Info(Tag + "KONIEC PROLOGU (" + why + ", dzien " + PrologueDay().ToString("0.0", Inv) + ") | Robert martwy " + died
                     + " | korona: wladca " + Who(crown != null ? crown.Leader : null) + " - Joffrey krolem " + (joffKing ? "TAK" : "NIE") + " (" + heir + ")"
                     + " | Ned: " + ned + " | pakty: " + pacts + " | potkniecia " + _stumbles + ".");
            if (_kingsAlive)
                Log.Player("King Robert Baratheon is dead of the wounds a boar gave him on the hunt. Joffrey Baratheon sits the Iron Throne"
                           + (_nedDone ? "." : ", and Lord Eddard Stark, Hand of the King, has been seized for treason and thrown into the black cells of King's Landing."));
            return true;
        }

        private static Settlement KingsLanding() { return Settlement.Find(KingsLandingId); }

        /// <summary>m116-fabula (Jeff 24.10, raport: kruk o smierci Roberta bez tekstu - smierc WoundedInBattle bez zabojcy daje pusty tekst gry): tekst wiesci o smierci
        /// postaci prologu (Ravens.Fallback). null - brak wlasnego tekstu.</summary>
        internal static string DeathNews(Hero v)
        {
            if (!Active || v == null) return null;
            if (v.StringId == RobertId) return "King Robert Baratheon is dead of the wounds a boar gave him on the hunt. Joffrey Baratheon sits the Iron Throne.";
            return null;
        }

        private static bool InDungeon(Hero ned, Settlement kl)
        {
            return ned != null && kl != null && ned.IsPrisoner && ned.PartyBelongedToAsPrisoner == kl.Party;
        }

        /// <summary>Ned jencem w lochu Krolewskiej Przystani. Zwraca opis do logu.</summary>
        private static string ArrestNed()
        {
            var ned = H(NedId);
            var kl = KingsLanding();
            if (ned == null || !ned.IsAlive) return "nie zyje";
            if (kl == null || kl.Party == null) return "brak osady " + KingsLandingId;
            if (InDungeon(ned, kl)) return "jeniec w " + KingsLandingId + " TAK";
            if (Busy(ned)) return "w bitwie - loch nastepnej doby";
            try
            {
                if (ned.IsPrisoner) EndCaptivityAction.ApplyByEscape(ned, null, false);
                if (ned.IsPrisoner) return "jeniec " + (ned.PartyBelongedToAsPrisoner != null ? ned.PartyBelongedToAsPrisoner.Id : "-") + " - nie wyszedl z niewoli, loch nastepnej doby";
                if (ned.GovernorOf != null) ChangeGovernorAction.RemoveGovernorOf(ned);   // jeniec nie rzadzi miastem (KillCharacterAction robi to samo przy smierci)
                var party = ned.PartyBelongedTo;
                if (party != null && party != MobileParty.MainParty)
                {
                    if (party.Army != null)
                    {
                        if (party.Army.LeaderParty == party) DisbandArmyAction.ApplyByUnknownReason(party.Army);
                        else party.Army = null;
                    }
                }
                else if (party == null && ned.CurrentSettlement != null) LeaveSettlementAction.ApplyForCharacterOnly(ned);
                TakePrisonerAction.Apply(kl.Party, ned);
                if (party != null && party != MobileParty.MainParty && party.IsActive && party.LeaderHero == null) DisbandPartyAction.StartDisband(party);
                _nedArrests++;
            }
            catch (Exception e) { Stumble("ArrestNed", e); }
            return InDungeon(ned, kl) ? "jeniec w " + KingsLandingId + " TAK (wlasciciel " + (kl.MapFaction != null ? kl.MapFaction.StringId : "-") + ")" : "jeniec NIE";
        }

        private static void KeepNedInDungeon()
        {
            var ned = H(NedId); var kl = KingsLanding();
            if (ned == null || !ned.IsAlive || InDungeon(ned, kl)) return;
            string r = ArrestNed();
            Log.Info(Tag + "Ned poza lochem przed egzekucja - z powrotem: " + r + " (aresztowan " + _nedArrests + ").");
        }

        /// <summary>Prefiks NedEvent.StartEvent (ROT): w kampanii z prologiem Robb glowa Starkow (i przez to wladca Polnocy), potem zywy Ned zostaje stracony.</summary>
        public static void NedEventPrefix()
        {
            try
            {
                if (!Active || _nedDone) return;
                if (!_kingsAlive) { _nedDone = true; return; }
                if (!_kingFallen) TryKingFalls("NedEvent przed koncem prologu");
                var ned = H(NedId);
                var stark = C(StarkClanId); var robb = H(RobbId);
                string fix = "-";
                // recenzja R5: Robb glowa rodu PRZED egzekucja (jak ROT JoffreyEvent) - Ned, ktory nie jest glowa rodu panujacego, ginie bez losowania dziedzica
                // i bez glosowania o tron Polnocy; wladca Polnocy od razu Robb (takze gdy egzekucja w bitwie stawia tylko znak smierci)
                try
                {
                    if (stark != null && robb != null && robb.IsAlive && robb.Clan == stark && stark.Leader != robb)
                    {
                        fix = "Robb glowa rodu przed egzekucja (byl " + Who(stark.Leader) + ")";
                        ChangeClanLeaderAction.ApplyWithSelectedNewLeader(stark, robb);
                    }
                    else fix = "bez zmiany";
                }
                catch (Exception e) { Stumble("NedEventPrefix.Robb", e); fix = "BLAD"; }
                string killed;
                if (ned != null && ned.IsAlive)
                {
                    var joff = H(JoffreyId);
                    try { KillCharacterAction.ApplyByExecution(ned, joff != null && joff.IsAlive ? joff : null, true, true); }
                    catch (Exception e) { Stumble("NedEventPrefix.Kill", e); }
                    _nedMarked = ned.IsAlive && ned.DeathMark != KillCharacterAction.KillCharacterActionDetail.None;
                    killed = ned.IsAlive ? (_nedMarked ? "znak smierci (w bitwie - umrze po niej)" : "NIE") : "TAK";
                }
                else killed = ned == null ? "brak bohatera" : "juz martwy";
                _nedDone = true;
                NedLine(killed, fix);
            }
            catch (Exception e) { Stumble("NedEventPrefix", e); }
        }

        /// <summary>Ned w walce przy NedEvent (znak smierci): gra zabija go po bitwie (ApplyByDeathMark); gdy po walce dalej zyje (weto innego moda) - smierc wymuszona
        /// ze znaku. Gdy nie zyje - druga linia egzekucji (PR-8 czyta ostatnia). Co dobe (DailyEnd).</summary>
        private static void NedMarkFollowUp()
        {
            var ned = H(NedId);
            if (ned != null && ned.IsAlive && !Busy(ned))
            {
                try { KillCharacterAction.ApplyByDeathMarkForced(ned, true); }
                catch (Exception e) { Stumble("NedMarkFollowUp", e); }
            }
            if (ned != null && ned.IsAlive) return;
            _nedMarked = false;
            NedLine("TAK (po walce - znak smierci z NedEvent)", "bez zmiany");
        }

        private static void NedLine(string killed, string fix)
        {
            var stark = C(StarkClanId); var robb = H(RobbId); var north = K(NorthId);
            Log.Info(Tag + "egzekucja Neda (NedEvent, dzien opowiesci " + (PrologueDay() - PrologueDays).ToString("0.0", Inv) + ") | Ned martwy " + killed
                     + " | rod Stark: glowa " + Who(stark != null ? stark.Leader : null) + " (" + fix + ")"
                     + " | Polnoc: wladca " + Who(north != null ? north.Leader : null) + " - Robb " + (north != null && robb != null && north.Leader == robb ? "TAK" : "NIE")
                     + (_kingFallen ? "" : " | krol Robert jeszcze zyje (w bitwie albo oblezeniu) - smierc krola po walce, proba co dobe") + ".");
        }
    }
}
