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
    /// (1) Doba >= KingFallsDay prologu (Daily): najpierw pakty prologu gasna (EndPacts), potem krol umiera "od ran z polowania na dzika": AddDeathMark(WoundedInBattle)
    ///     + KillCharacterAction.ApplyByDeathMarkForced (wymuszona - nasza ochrona CanHeroDie i cudze weta nie blokuja). Krol w bitwie albo oblezeniu (jego druzyna albo
    ///     strona, ktora trzyma go w niewoli) - czekamy do nastepnej doby. Gra sama robi sukcesje: przywodca rodu clan_nord_1 z dziedzicow (ChangeClanLeaderAction),
    ///     glosowanie o tron z premia ROT x999 dla rodu panujacego; gdy przywodca nie jest Joffreyem, a Joffrey zyje w tym rodzie - ustawiamy Joffreya
    ///     (ChangeClanLeaderAction.ApplyWithSelectedNewLeader). Nazwa rodu wraca do oryginalu z XML ("Baratheon, Joffrey").
    /// (2) Ned jencem korony w Krolewskiej Przystani (town_EW4): druzyna rozwiazana (jego armia - DisbandArmyAction), TakePrisonerAction do lochu osady; Ned w bitwie -
    ///     nastepna doba; jeniec kogo innego - najpierw uwolniony (EndCaptivityAction.ApplyByEscape). Pilnowane co dobe do egzekucji (pokoj gry wypuszcza jencow przy
    ///     zawarciu pokoju - wtedy Ned wraca do lochu nastepnej doby).
    /// (3) NedEvent ROT (dzien 2 osi opowiesci, x4.33) zaklada martwego Neda - prefiks NedEvent.StartEvent: Ned zyje -> KillCharacterAction.ApplyByExecution(Ned, Joffrey,
    ///     pokaz, isForced) PRZED ROTEvents.OnNedDeath (wojny Robba ROT widza juz Robba), potem Robb glowa Starkow (gdy gra wybrala innego dziedzica - ustawiamy Robba),
    ///     wiec Robb wladca Polnocy. Latka wpinana leniwie z reszta latek prologu.
    /// Klasyczny start: nic (latki nie ma, Active = false).
    /// </summary>
    internal static partial class Prologue
    {
        private const string KingsLandingId = "town_EW4";
        private static bool _kingFallen, _nedDone, _endLogged;
        private static int _kingWaits, _nedArrests;

        private static void ResetEnd() { _kingFallen = false; _nedDone = false; _endLogged = false; _kingWaits = 0; _nedArrests = 0; }

        private static string PackEnd() { return "kf=" + (_kingFallen ? "1" : "0") + ";nd=" + (_nedDone ? "1" : "0") + ";"; }

        private static void UnpackEnd(Dictionary<string, string> map)
        {
            ResetEnd();
            string v;
            _kingFallen = map.TryGetValue("kf", out v) && v == "1";
            _nedDone = map.TryGetValue("nd", out v) && v == "1";
            _endLogged = _kingFallen;
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
            return _kingFallen ? "koniec prologu TAK" + (_nedDone ? ", Ned stracony" : "") : "koniec prologu za " + Math.Max(0.0, KingFallsDay - day).ToString("0.0", Inv) + " dni";
        }

        /// <summary>Smierc krola, Joffrey krolem, Ned w lochu. Krol w bitwie - false (nastepna doba).</summary>
        private static bool TryKingFalls(string why)
        {
            var robert = H(RobertId);
            if (_kingsAlive && robert != null && robert.IsAlive && Busy(robert))
            {
                _kingWaits++;
                Log.Info(Tag + "koniec prologu (" + why + "): krol Robert w bitwie albo oblezeniu - smierc krola nastepnej doby (czeka " + _kingWaits + ").");
                return false;
            }
            string pacts = EndPacts();
            string died = "-";
            if (_kingsAlive && robert != null && robert.IsAlive)
            {
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
            // Joffrey krolem
            var crownClan = C(CrownClanId); var joff = H(JoffreyId);
            string heir = "-";
            try
            {
                if (crownClan != null && joff != null && joff.IsAlive && joff.Clan == crownClan && crownClan.Leader != joff)
                {
                    heir = "gra wybrala " + Who(crownClan.Leader) + " - ustawiony Joffrey";
                    ChangeClanLeaderAction.ApplyWithSelectedNewLeader(crownClan, joff);
                }
                else heir = "gra: " + Who(crownClan != null ? crownClan.Leader : null);
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
            string ned = _kingsAlive ? ArrestNed() : "nie ozywiony";
            _endLogged = true;
            Log.Info(Tag + "KONIEC PROLOGU (" + why + ", dzien " + PrologueDay().ToString("0.0", Inv) + ") | Robert martwy " + died
                     + " | korona: wladca " + Who(crown != null ? crown.Leader : null) + " - Joffrey krolem " + (joffKing ? "TAK" : "NIE") + " (" + heir + ")"
                     + " | Ned: " + ned + " | pakty: " + pacts + " | potkniecia " + _stumbles + ".");
            if (_kingsAlive)
                Log.Player("King Robert Baratheon is dead of the wounds a boar gave him on the hunt. Joffrey Baratheon sits the Iron Throne, and Lord Eddard Stark, "
                           + "Hand of the King, has been seized for treason and thrown into the black cells of King's Landing.");
            return true;
        }

        private static Settlement KingsLanding() { return Settlement.Find(KingsLandingId); }

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

        /// <summary>Prefiks NedEvent.StartEvent (ROT): w kampanii z prologiem zywy Ned zostaje stracony, Robb glowa Starkow i wladca Polnocy.</summary>
        public static void NedEventPrefix()
        {
            try
            {
                if (!Active || _nedDone) return;
                if (!_kingsAlive) { _nedDone = true; return; }
                if (!_kingFallen) TryKingFalls("NedEvent przed koncem prologu");
                var ned = H(NedId);
                string killed;
                if (ned != null && ned.IsAlive)
                {
                    var joff = H(JoffreyId);
                    try { KillCharacterAction.ApplyByExecution(ned, joff != null && joff.IsAlive ? joff : null, true, true); }
                    catch (Exception e) { Stumble("NedEventPrefix.Kill", e); }
                    killed = ned.IsAlive ? (ned.DeathMark != KillCharacterAction.KillCharacterActionDetail.None ? "znak smierci (w bitwie - umrze po niej)" : "NIE") : "TAK";
                }
                else killed = ned == null ? "brak bohatera" : "juz martwy";
                _nedDone = true;
                var stark = C(StarkClanId); var robb = H(RobbId);
                string fix = "-";
                try
                {
                    if (stark != null && robb != null && robb.IsAlive && robb.Clan == stark && stark.Leader != robb && (ned == null || !ned.IsAlive))
                    {
                        fix = "gra wybrala " + Who(stark.Leader) + " - ustawiony Robb";
                        ChangeClanLeaderAction.ApplyWithSelectedNewLeader(stark, robb);
                    }
                    else fix = "gra: " + Who(stark != null ? stark.Leader : null);
                }
                catch (Exception e) { Stumble("NedEventPrefix.Robb", e); }
                var north = K(NorthId);
                Log.Info(Tag + "egzekucja Neda (NedEvent, dzien opowiesci " + (PrologueDay() - PrologueDays).ToString("0.0", Inv) + ") | Ned martwy " + killed
                         + " | rod Stark: glowa " + Who(stark != null ? stark.Leader : null) + " (" + fix + ")"
                         + " | Polnoc: wladca " + Who(north != null ? north.Leader : null) + " - Robb " + (north != null && robb != null && north.Leader == robb ? "TAK" : "NIE") + ".");
            }
            catch (Exception e) { Stumble("NedEventPrefix", e); }
        }
    }
}
