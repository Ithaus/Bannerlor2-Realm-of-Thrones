using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// 367b (Jeff 09.11: "wypuszcze Cie i przysiegne wiernosc jesli zaakceptujesz moje zdobycze terytorialne"; "tak rob 367b"). ROKOWANIA Z WIELKIM JENCEM - WARUNEK F:
    /// "Go free, and I will bend the knee to you - if my lands stay mine." Klucz CaptiveTermsBendKnee (wylaczony = bez linii). Okup wielkich jencow bez zmian.
    ///
    /// GRA (cs:225456 ApplyInternal, ApplyByJoinToKingdomByDefection): lenna zostaja przy rodzie (ClanLeaveKingdom nie uzywa giveBackFiefs), DebtToKingdom = 0,
    /// wplywy 0, partie rodu wychodza z armii, AdjustFactionStances - pokoj rodu z kazdym, z kim nowa korona nie walczy (WarTruce H6, PeaceHold, CanonThroneWars,
    /// WorldClock7 - para krolestw, rod przechodzi bez skutkow). PrisonerReleaseCampaignBehavior.ClanChangedKingdom: jency rodu gracza z frakcji, ktore nie sa
    /// w wojnie z nowa korona - wolni za darmo; krewni gracza w rekach nowej korony i jej przyjaciol - wolni. WorkshopsCampaignBehavior: warsztaty gracza w miastach
    /// wrogow nowej korony (cala stara korona) - przechodza na notabli bez zaplaty. BK: defekcja nie nadaje parostwa (tylko JoinKingdom).
    /// Wszystko to podpowiedz pokazuje PRZED decyzja.
    ///
    /// LINIA widoczna: klucz + CaptiveTerms + BK, jeniec jest WLADCA (nastepca nie przyjmie wasala), gracz nie jest wladca. Nieklikalna (powod po angielsku): bez
    /// krolestwa, najemnik, korony nie w wojnie (takze rozejm WarTruce), korona spoza CanonThroneWars.InW (Nocna Straz, Wolni Ludzie, Essos, najezdzcy przed
    /// ladowaniem), stormlands przed koncem RenlyDeathEvent i dragonstone przed koncem StannisDeathEvent (ROT: gracz w tej koronie - ApplyByLeaveWithRebellion i
    /// zniszczenie korony), jeniec innej korony (OtherCrown, jak T), przerwa odmowy 'F', rod w polu (armia, bitwa, oblezenie), oblezona albo najezdzana osada rodu,
    /// gracz w murach cudzej osady (spotkanie z osada, ktora po przysiedze bylaby wroga).
    /// SPIS ROSZCZEN wspolny z 367a (T2), cena x0.75 i zgoda +10: "I swear it, and I pay your lords", "... seal it with marriage", "I swear it; my titles can wait"
    /// (ostrzezenie: dochod wsi idzie do posiadaczy - 354, w jednej frakcji), "Never mind"; bez roszczen od razu podpowiedz.
    /// ZGODA KROLA: Evaluate 366 z kosztem C = -(25 x miasta + 15 x zamki + 5 x wsie gracza) (krol zyskuje), + VassalCost pakietu jak V. Odmowa - 7 dob przerwy 'F'.
    /// WYKONANIE (kolejka 366, takt mapy, bez SwitchToMenu): sprawdzenia od nowa (krolestwo From, bramki, Recheck T2, kiesa) - zawiedzie = Drop, nic sie nie zmienia;
    /// potem DebtLadder.TakeOverCrownDebt (dlug wobec starej korony do ksiegi - gra by go wyzerowala), migawka, uwolnienie krola, defekcja (zostaje rok wedlug
    /// kalendarza Armoury), DOPIERO PO UDANEJ defekcji Ransom178.ForfeitPlayer (nagrody od starej korony i 1/9 doby skreslone), tytuly krola za darmo + RunClaim pakietu, relacje (dawny wladca
    /// -40, glowy rodow starej korony -(5 + 5 x Honor+)). Log "ROKOWANIA (367b)": decyzja, WYKONANE / PRZEPADLA / BLAD.
    /// Bez stanu poza kolejka i przerwami (Reset/Import 366).
    /// 367e (Jeff 09.11 "a jest opcja ze przechodze pod niego jako wasal?"): rod gracza BEZ krolestwa (nie najemnik) - ta sama linia, zgoda (Evaluate 'F') i pakiet; bramki bez
    /// wymogu wojny koron; Deal.From = FromNone; RunFNone: ChangeKingdomAction.ApplyByJoinToKingdom (AdjustFactionStances konczy wojne rodu z korona jenca, wojny korony
    /// staja sie wojnami rodu; wplywy zostaja - ClanLeaveKingdom tylko przy starej koronie; BK Lesser Peerage, gdy rod bez glosu); bez dlugu, nagrod i kar relacji.
    /// Uraza 367d przy F (oba warianty) = 0: krol zyskuje wasala z ziemiami, a lordowie oddaja tytuly dobrowolnie (zloto/slub) - CaptiveGrudge.Preview('F') = "", bez TermsBegin.
    /// NIE ZMIENIONE (decyzja Jeffa): CanonThroneWars :525 PlayerRealm - wasal korony daje jej bramke "gracz" (King's Landing sam nie szuka wojen o tron).
    /// </summary>
    internal static partial class CaptiveTerms
    {
        internal const string Tag367b = "ROKOWANIA (367b)";
        private const double FPriceMul = 0.75, FScoreBonus = 10;
        private const int FRulerDrop = 40;
        /// <summary>367e: Deal.From dla rodu gracza bez krolestwa (przysiega niezaleznego rodu - ApplyByJoinToKingdom zamiast defekcji).</summary>
        internal const string FromNone = "-";

        // stan rozmowy 367b (czyszczony w ClearTalk)
        private static bool _fTalk, _fWait;

        private static bool KneeOn { get { var s = Settings.Current; return On && s.CaptiveTermsBendKnee && LandBk.Ready; } }

        /// <summary>Linia F widoczna: jeniec-wladca, gracz nie wladca.</summary>
        private static bool FBase()
        {
            try
            {
                if (!KneeOn || _talk == null || Clan.PlayerClan == null) return false;
                var k = _talk.MapFaction as Kingdom;
                if (k == null || k.Leader != _talk) return false;
                var mine = Clan.PlayerClan.Kingdom;
                return mine == null || !Prologue.RulesRealm(mine);
            }
            catch (Exception e) { Stumble("FBase", e); return false; }
        }

        private static bool FCondClaims() { return FBase() && HasClaims(); }
        private static bool FCondBare() { return FBase() && !HasClaims(); }

        private static void FOpen() { _fTalk = true; _fWait = false; }   // spis od nowa z cena F (TalkClaims)

        private static void PickFBare() { _fTalk = true; _fWait = false; SetPkg(' '); Pick('F'); }
        private static void PickFWait() { _fTalk = true; _fWait = true; SetPkg(' '); Pick('F'); }
        private static void PickFGold() { _fTalk = true; _fWait = false; SetPkg('G'); Pick('F'); }
        private static void PickFWed() { _fTalk = true; _fWait = false; SetPkg('M'); Pick('F'); }

        private static void SetPkg(char kind)
        {
            try
            {
                _pkg = kind == ' ' ? new List<Pick367>() : Package(kind);
                _pkgKind = kind;
                _pkgGold = GoldOf(_pkg);
            }
            catch (Exception e) { Stumble("SetPkg", e); _pkg = new List<Pick367>(); _pkgKind = ' '; _pkgGold = 0; }
        }

        private static bool FClick(out TextObject why)
        {
            why = null;
            try
            {
                string en, pl;
                if (!FGate(_talk, false, out en, out pl)) { why = new TextObject("{=!}" + en); return false; }
                return true;
            }
            catch (Exception e) { Stumble("FClick", e); why = new TextObject("{=!}Not now."); return false; }
        }

        // ------------------------------------------------------------ bramki
        /// <summary>Wszystkie bramki F. run - przy wykonaniu (bez przerwy odmowy, z kontrola spotkania, niewoli i wladzy jenca). en - powod dla gracza, pl - do logu.</summary>
        private static bool FGate(Hero king, bool run, out string en, out string pl)
        {
            en = pl = null;
            var pc = Clan.PlayerClan;
            var me = Hero.MainHero;
            if (!KneeOn) { en = "Fealty cannot be sworn now."; pl = "klucz CaptiveTermsBendKnee/CaptiveTerms albo BK wylaczone"; return false; }
            var nk = king != null ? king.MapFaction as Kingdom : null;
            if (nk == null || nk.IsEliminated || nk.Leader != king) { en = "He no longer wears a crown."; pl = "jeniec nie jest juz wladca"; return false; }
            if (pc == null) { en = "Not now."; pl = "brak rodu gracza"; return false; }
            var old = pc.Kingdom;
            // 367e (Jeff 09.11 "a jest opcja ze przechodze pod niego jako wasal?"): rod BEZ krolestwa sklada przysiege i wstepuje do korony jenca (ApplyByJoinToKingdom) -
            // bez starej korony nie ma zmiany stron, wiec bez wymogu wojny koron; reszta bramek (fabula, pole, oblezenie, mury, przerwa) jak przy 367b
            if (old != null)
            {
                if (pc.IsUnderMercenaryService) { en = "You serve as a mercenary - you have no fealty to give."; pl = "gracz najemnikiem"; return false; }
                if (Prologue.RulesRealm(old)) { en = "You rule your own realm."; pl = "gracz wladca"; return false; }
                if (old == nk) { en = "You already serve his crown."; pl = "gracz juz w koronie jenca"; return false; }
                if (!FactionManager.IsAtWarAgainstFaction(old, nk)) { en = "Your crowns are not at war - there is no side to change."; pl = "korony nie w wojnie (" + old.StringId + " - " + nk.StringId + ")"; return false; }
            }
            else if (pc.IsUnderMercenaryService) { en = "You serve as a mercenary - you have no fealty to give."; pl = "gracz najemnikiem (bez krolestwa)"; return false; }
            if (!CanonThroneWars.InW(nk)) { en = "You cannot swear fealty to that realm."; pl = "korona poza Westeros (InW) " + nk.StringId; return false; }
            int rs;
            if (FateOpen(nk, out rs)) { en = "His crown's fate is not yet settled - an oath to it would not hold."; pl = "los korony " + nk.StringId + " otwarty (wydarzenie ROT stan " + rs + ")"; return false; }
            var oc = OtherCrown(king);
            if (oc != null) { en = "He is the captive of " + oc.Name + ", a crown you no longer serve. You cannot trade away its ransom."; pl = "jeniec korony " + oc.Name + ", ktorej gracz juz nie sluzy"; return false; }
            if (!run)
            {
                double cl = CoolLeft(king, 'F');
                if (cl > 0) { en = "He has refused this already. Ask again in " + Math.Ceiling(cl).ToString(Inv) + " days."; pl = "przerwa odmowy"; return false; }
            }
            string fw;
            if (InField(out fw)) { en = "Your house is still in the field - finish your battles and sieges, and leave the army first."; pl = "rod w polu: " + fw; return false; }
            string sw;
            if (HoldingBesieged(out sw)) { en = "One of your holdings is under siege."; pl = "osada rodu oblezona albo najezdzana: " + sw; return false; }
            Settlement inside;
            if (InForeignWalls(nk, out inside)) { en = "Leave " + inside.Name + " first - you cannot change your fealty within the walls of a realm you would betray."; pl = "gracz w osadzie " + inside.Name + " (" + (inside.MapFaction != null ? inside.MapFaction.StringId : "-") + ")"; return false; }
            if (run)
            {
                if (me == null || me.IsPrisoner) { en = "you are a captive yourself."; pl = "gracz w niewoli"; return false; }
                // recenzja 1: spotkanie z osada dozwolone jak w rozmowie (InForeignWalls wyzej - osada rodu, nowej korony albo trzeciej frakcji bez wojny z nia);
                // blokuje tylko bitwa i spotkanie bez osady (z partia) - inaczej umowa zawarta w miescie nowej korony zawsze by przepadala
                if (MapEvent.PlayerMapEvent != null || (PlayerEncounter.Current != null && PlayerEncounter.EncounterSettlement == null))
                { en = "you were not free to swear it - speak to him again."; pl = "gracz w spotkaniu bez osady albo w bitwie"; return false; }
            }
            return true;
        }

        /// <summary>ROT RenlyDeathEvent/StannisDeathEvent: gracz w stormlands/dragonstone - ROT robi ApplyByLeaveWithRebellionAgainstKingdom i niszczy korone. Przysiega
        /// tym koronom dopiero po zakonczeniu wydarzenia (stan 2); -1 (nie widac) tez blokuje.</summary>
        private static bool FateOpen(Kingdom nk, out int state)
        {
            state = 2;
            string ev = nk.StringId == "stormlands" ? "RenlyDeathEvent" : nk.StringId == "dragonstone" ? "StannisDeathEvent" : null;
            if (ev == null) return false;
            try { state = Prologue.RotEventState(ev); } catch { state = -1; }
            return state != 2;
        }

        private static bool InField(out string what)
        {
            what = "-";
            var mp = MobileParty.MainParty;
            if (mp != null)
            {
                if (mp.Army != null) { what = "partia gracza w armii"; return true; }
                if (mp.MapEvent != null || mp.SiegeEvent != null || mp.BesiegedSettlement != null) { what = "partia gracza w bitwie albo oblezeniu"; return true; }
            }
            var pc = Clan.PlayerClan;
            if (pc == null || pc.WarPartyComponents == null) return false;
            foreach (var w in pc.WarPartyComponents)
            {
                var p = w != null ? w.MobileParty : null;
                if (p == null) continue;
                if (p.MapEvent != null || p.SiegeEvent != null || p.BesiegedSettlement != null) { what = p.Name + " w bitwie albo oblezeniu"; return true; }
                if (p.Army != null && p.Army.LeaderParty == p) { what = p.Name + " prowadzi armie"; return true; }
            }
            return false;
        }

        private static bool HoldingBesieged(out string what)
        {
            what = "-";
            var pc = Clan.PlayerClan;
            if (pc == null) return false;
            foreach (var st in Settlement.All)
            {
                if (st == null || st.OwnerClan != pc) continue;
                if (st.IsUnderSiege || st.SiegeEvent != null || (st.IsVillage && st.IsUnderRaid) || (st.Party != null && st.Party.MapEvent != null)) { what = st.Name.ToString(); return true; }
            }
            return false;
        }

        /// <summary>Gracz w osadzie nie swojego rodu, ktora po przysiedze bylaby wroga (stara korona albo wrog nowej) - zmiana stron w cudzych murach.</summary>
        private static bool InForeignWalls(Kingdom nk, out Settlement st)
        {
            st = MobileParty.MainParty != null ? MobileParty.MainParty.CurrentSettlement : null;
            if (st == null || st.OwnerClan == Clan.PlayerClan || st.MapFaction == null || st.MapFaction == nk) return false;
            var mine = Clan.PlayerClan.Kingdom;   // 367e: bez krolestwa - tylko osada wroga nowej korony
            return (mine != null && st.MapFaction == mine) || st.MapFaction.IsAtWarWith(nk);
        }

        private static string GateState(Hero king)
        {
            try
            {
                var nk = king.MapFaction as Kingdom;
                int rs; bool fate = nk != null && FateOpen(nk, out rs);
                string fw, sw; Settlement ins;
                var oldK = Clan.PlayerClan != null ? Clan.PlayerClan.Kingdom : null;
                string war = oldK != null ? "wojna koron TAK" : "z: bez krolestwa, wojna rodu z korona " + (nk != null && FactionManager.IsAtWarAgainstFaction(Clan.PlayerClan, nk) ? "TAK" : "NIE");
                return war + ", InW " + (nk != null && CanonThroneWars.InW(nk) ? "TAK" : "NIE") + ", los korony " + (fate ? "OTWARTY" : "rozstrzygniety")
                       + ", okup korony " + (Ransom178.CaptiveCrown(king) != null ? Ransom178.CaptiveCrown(king).StringId : "brak") + ", w polu " + (InField(out fw) ? fw : "NIE")
                       + ", oblezenie " + (HoldingBesieged(out sw) ? sw : "NIE") + ", w murach " + (nk != null && InForeignWalls(nk, out ins) ? ins.Name.ToString() : "NIE")
                       + ", spotkanie " + (PlayerEncounter.Current != null ? (PlayerEncounter.EncounterSettlement != null ? PlayerEncounter.EncounterSettlement.Name.ToString() : "TAK") : "NIE");
            }
            catch (Exception e) { Stumble("GateState", e); return "?"; }
        }

        // ------------------------------------------------------------ podglad skutkow
        private static void PlayerFiefs(out int towns, out int castles, out int villages)
        {
            towns = castles = villages = 0;
            var pc = Clan.PlayerClan;
            if (pc == null) return;
            foreach (var st in Settlement.All)
            {
                try
                {
                    if (st == null) continue;
                    if (st.IsTown && st.OwnerClan == pc) towns++;
                    else if (st.IsCastle && st.OwnerClan == pc) castles++;
                    else if (st.IsVillage && st.Village != null && st.Village.Bound != null && st.Village.Bound.OwnerClan == pc) villages++;
                }
                catch (Exception e) { Stumble("PlayerFiefs", e); }
            }
        }

        /// <summary>Jency-bohaterowie rodu gracza (bez gracza) - te same miejsca co gra (PrisonerReleaseCampaignBehavior.ReleasePrisonersInternal/ReleasePrisonersForClan,
        /// recenzja 2): lochy osad rodu, karawany lordow i towarzyszy, partie wojenne, partie wiesniakow wsi rodu, garnizony miast i zamkow rodu.</summary>
        private static List<Hero> HeldHeroes()
        {
            var l = new List<Hero>();
            var pc = Clan.PlayerClan;
            if (pc == null) return l;
            var seen = new HashSet<Hero>();
            try
            {
                foreach (var st in Settlement.All)
                {
                    try
                    {
                        if (st == null || st.OwnerClan != pc) continue;
                        if (st.Party != null) AddHeld(st.Party.PrisonRoster, seen, l);
                        if (st.IsVillage && st.Village != null && st.Village.VillagerPartyComponent != null && st.Village.VillagerPartyComponent.MobileParty != null)
                            AddHeld(st.Village.VillagerPartyComponent.MobileParty.PrisonRoster, seen, l);
                        else if ((st.IsTown || st.IsCastle) && st.Town != null && st.Town.GarrisonParty != null) AddHeld(st.Town.GarrisonParty.PrisonRoster, seen, l);
                    }
                    catch (Exception e) { Stumble("HeldHeroes(osada)", e); }
                }
                foreach (var x in pc.Heroes)   // Clan.Heroes = lordowie + towarzysze (Clan.OnCompanionAdded -> OnHeroAdded)
                {
                    if (x == null || !x.IsAlive || x.OwnedCaravans == null) continue;
                    foreach (var cv in x.OwnedCaravans) if (cv != null && cv.MobileParty != null) AddHeld(cv.MobileParty.PrisonRoster, seen, l);
                }
                if (pc.WarPartyComponents != null)
                    foreach (var w in pc.WarPartyComponents)
                        if (w != null && w.MobileParty != null) AddHeld(w.MobileParty.PrisonRoster, seen, l);
            }
            catch (Exception e) { Stumble("HeldHeroes", e); }
            return l;
        }

        private static void AddHeld(TroopRoster r, HashSet<Hero> seen, List<Hero> l)
        {
            if (r == null) return;
            foreach (var e in r.GetTroopRoster())
                if (e.Number > 0 && e.Character != null && e.Character.IsHero && e.Character.HeroObject != null && e.Character.HeroObject != Hero.MainHero && seen.Add(e.Character.HeroObject)) l.Add(e.Character.HeroObject);
        }

        /// <summary>Gra uwolni (PrisonerReleaseCampaignBehavior): jeniec rodu gracza, ktorego frakcja nie bedzie w wojnie z nowa korona (bez krola - uwolniony wczesniej).</summary>
        private static bool GameFrees(Hero x, Hero king, Kingdom old, Kingdom nk)
        {
            if (x == null || x == king || !x.IsAlive || x.MapFaction == null) return false;
            if (x.MapFaction == old || x.MapFaction == nk) return x.MapFaction == nk;
            return !x.MapFaction.IsAtWarWith(nk);
        }

        /// <summary>Krewni gracza w niewoli frakcji, ktora po przysiedze nie bedzie w wojnie z graczem (nowa korona i jej przyjaciele).</summary>
        private static List<Hero> KinFreed(Kingdom old, Kingdom nk)
        {
            var l = new List<Hero>();
            try
            {
                foreach (var x in Clan.PlayerClan.Heroes)
                {
                    if (x == null || !x.IsAlive || !x.IsPrisoner || x == Hero.MainHero) continue;
                    var cap = x.PartyBelongedToAsPrisoner != null ? x.PartyBelongedToAsPrisoner.MapFaction : (x.CurrentSettlement != null ? x.CurrentSettlement.MapFaction : null);
                    if (cap == null || cap == old) continue;
                    if (cap == nk || !cap.IsAtWarWith(nk)) l.Add(x);
                }
            }
            catch (Exception e) { Stumble("KinFreed", e); }
            return l;
        }

        private static List<Workshop> LostWorkshops(Kingdom old, Kingdom nk)
        {
            var l = new List<Workshop>();
            try
            {
                foreach (var w in Hero.MainHero.OwnedWorkshops)
                {
                    var st = w != null ? w.Settlement : null;
                    if (st == null || st.OwnerClan == Clan.PlayerClan || st.MapFaction == null || st.MapFaction == nk) continue;
                    if (st.MapFaction == old || st.MapFaction.IsAtWarWith(nk)) l.Add(w);
                }
            }
            catch (Exception e) { Stumble("LostWorkshops", e); }
            return l;
        }

        private static void WarsAfter(Kingdom old, Kingdom nk, out List<Kingdom> wars, out List<Kingdom> peace)
        {
            wars = new List<Kingdom>(); peace = new List<Kingdom>();
            if (old != null) wars.Add(old);
            foreach (var k in Kingdom.All)
            {
                if (k == null || k.IsEliminated || k == nk || k == old) continue;
                bool w = k.IsAtWarWith(nk);
                if (w) wars.Add(k);
                else if (old != null ? k.IsAtWarWith(old) : (Clan.PlayerClan != null && FactionManager.IsAtWarAgainstFaction(k, Clan.PlayerClan))) peace.Add(k);   // 367e: bez krolestwa - wojny rodu
            }
        }

        private static string Names(List<Kingdom> l, int from)
        {
            var n = new List<string>();
            for (int i = from; i < l.Count; i++) n.Add(l[i].Name.ToString());
            return Join(n);
        }

        private static long RewardsOwed(Kingdom old)
        {
            long v = 0;
            try { foreach (var x in Clan.PlayerClan.Heroes) if (x != null) v += Ransom178.PendingReward(x, old); } catch (Exception e) { Stumble("RewardsOwed", e); }
            return v;
        }

        /// <summary>Podpowiedz F (dopisywana do pasma 366): co sie stanie po przysiedze.</summary>
        private static string FHint()
        {
            try
            {
                var king = _talk;
                var nk = king.MapFaction as Kingdom;
                var old = Clan.PlayerClan.Kingdom;
                if (nk == null) return " He no longer wears a crown.";
                if (old == null) return FHintNone(king, nk);   // 367e
                var sb = new StringBuilder(600);
                sb.Append(" Bending the knee to ").Append(Name(king)).Append(": your house leaves ").Append(old.Name).Append(" and is sworn to ").Append(nk.Name)
                  .Append(" for a year, keeping all your fiefs.");
                if (_titles.Count > 0) sb.Append(" His seal confirms ").Append(_titleNames).Append('.');
                List<Kingdom> wars, peace; WarsAfter(old, nk, out wars, out peace);
                sb.Append(" You will be at war with ").Append(old.Name);
                if (wars.Count > 1) sb.Append(", ").Append(Names(wars, 1));
                if (peace.Count > 0) sb.Append("; peace with ").Append(Names(peace, 0));
                sb.Append('.');
                var ruler = old.Leader;
                sb.Append(' ').Append(ruler != null ? Name(ruler) : "Your old liege").Append(" will never forgive it, and the lords of ").Append(old.Name).Append(" will think less of you.");
                long rew = RewardsOwed(old);
                bool ransom = Ransom178.CaptiveCrown(king) == old;
                if (ransom || rew > 0)
                {
                    sb.Append(' ').Append(old.Name).Append(ransom ? " loses his ransom" : " owes you nothing more");
                    if (rew > 0) sb.Append(ransom ? ", and the " : ": the ").Append(rew.ToString("N0", Inv)).Append(" denars it owes you as rewards are forfeit");
                    sb.Append('.');
                }
                int freed = 0; foreach (var x in HeldHeroes()) if (GameFrees(x, king, old, nk)) freed++;
                int kin = KinFreed(old, nk).Count;
                if (freed > 0 || kin > 0)
                {
                    sb.Append(' ').Append(nk.Name);
                    if (freed > 0) sb.Append(" sets free, without ransom, the ").Append(freed).Append(freed == 1 ? " captive" : " captives").Append(" you hold from realms at peace with it");
                    if (kin > 0) sb.Append(freed > 0 ? "; it frees " : " frees ").Append(kin).Append(kin == 1 ? " of your kin" : " of your kin");
                    sb.Append('.');
                }
                sb.Append(" Your influence (").Append(Math.Round(Clan.PlayerClan.Influence).ToString(Inv)).Append(") is lost.");
                int ws = LostWorkshops(old, nk).Count;
                if (ws > 0) sb.Append(" You lose your ").Append(ws).Append(ws == 1 ? " workshop" : " workshops").Append(" in towns of your new enemies.");
                return sb.ToString();
            }
            catch (Exception e) { Stumble("FHint", e); return " Bending the knee changes your fealty."; }
        }

        /// <summary>Recenzja 1: tytuly BK do ziem gracza w rekach lordow korony krola, ktore po przysiedze zostaja POZA pakietem (odmowa, przerwa, "titles can wait"
        /// albo CaptiveTermsVassalTitles wylaczone) - po F posiadacz jest w tej samej frakcji, wiec 354 oddaje mu podatek z wsi gracza. Pieczec krola (TitlesFor) nie wchodzi.</summary>
        private static void DuesLeft(out int titles, out int villages)
        {
            titles = villages = 0;
            try
            {
                if (_talk == null) return;
                var cl = ClaimsOn ? TalkClaims() : ClaimsFor(_talk, true);
                var inPkg = new HashSet<Hero>();
                foreach (var p in _pkg) if (p != null && p.C != null && p.C.Holder != null) inPkg.Add(p.C.Holder);
                foreach (var c in cl)
                {
                    if (c == null || c.Holder == null || inPkg.Contains(c.Holder)) continue;
                    titles += c.Titles.Count; villages += c.Villages;
                }
            }
            catch (Exception e) { Stumble("DuesLeft", e); }
        }

        private static string PkgLabel()
        {
            if (_pkg.Count > 0) return (_pkgKind == 'M' ? "slub" : "zloto") + " " + _pkgGold + " zl: " + PkgPl();
            return _fWait ? "bez (tytuly czekaja) 0 zl" : "bez (brak roszczen) 0 zl";
        }

        private static void LogFDecide(Hero h, string effect)
        {
            try
            {
                var old = Clan.PlayerClan != null ? Clan.PlayerClan.Kingdom : null;
                int t, c, v; PlayerFiefs(out t, out c, out v);
                int dt, dv; DuesLeft(out dt, out dv);
                Log.Info(Tag367b + ": krol " + Name(h) + " (" + (h.MapFaction != null ? h.MapFaction.StringId : "-") + ") | warunek F (" + (old != null ? old.StringId : "z: bez krolestwa") + " -> "
                         + (h.MapFaction != null ? h.MapFaction.StringId : "-") + "; wnosi miasta " + t + ", zamki " + c + ", wsie " + v + "; pieczec krola " + _titles.Count + " (" + _titleNames + "); pakiet "
                         + PkgLabel() + "; poza pakietem tytuly lordow " + dt + ", w tym wsie " + dv + " (354 - podatek do posiadaczy)) | wynik " + F(_sc.Total) + (_accepted ? " - PRZYJMUJE" : " - ODMAWIA") + " | skladniki: " + _sc.Parts + " | bramki: " + GateState(h)
                         + " | skutek: " + effect + ".");
            }
            catch (Exception e) { Stumble("LogFDecide", e); }
        }

        // ------------------------------------------------------------ wykonanie F
        private static void RunF(Deal d, Hero h)
        {
            var me = Hero.MainHero;
            var pc = Clan.PlayerClan;
            var old = pc != null ? pc.Kingdom : null;
            if (d.From == FromNone) { RunFNone(d, h); return; }   // 367e: przysiega rodu bez krolestwa
            if (old == null || old.StringId != d.From) { Drop(d, h, "you no longer serve the crown you meant to leave.", "gracz nie w krolestwie z rozmowy (" + d.From + ", teraz " + (old != null ? old.StringId : "brak") + ")"); return; }
            string en, pl;
            if (!FGate(h, true, out en, out pl)) { Drop(d, h, en.Length > 0 && char.IsUpper(en[0]) ? en.Substring(0, 1).ToLowerInvariant() + en.Substring(1) : en, pl); return; }
            var nk = (Kingdom)h.MapFaction;
            var pkg = new List<Pick367>();
            if (!string.IsNullOrEmpty(d.Claims))
            {
                if (!ClaimsOn) { Drop(d, h, "the claims of his lords can no longer be settled.", "klucz CaptiveTermsVassalTitles albo BK wylaczone"); return; }
                pkg = Recheck(d, h, out en, out pl);   // przed przejsciem: posiadacze jeszcze w koronie krola
                if (pkg == null) { Drop(d, h, en, pl); return; }
            }
            long gold = GoldOf(pkg);
            if (me.Gold < gold) { Drop(d, h, "you no longer have " + gold.ToString("N0", Inv) + " denars.", "za malo zlota gracza (" + me.Gold + " < " + gold + ")"); return; }

            // ---- od tego miejsca nieodwracalne
            long pg0 = me.Gold;
            int debt0 = pc.DebtToKingdom;
            try { DebtLadder.TakeOverCrownDebt(pc); } catch (Exception e) { Stumble("RunF(dlug)", e); }
            int debt1 = pc.DebtToKingdom;
            long rew = 0, thirds = 0;   // ForfeitPlayer dopiero po udanym przejsciu (recenzje 1 i 2: BLAD nie zabiera nagrod - zdrady nie bylo)
            long ransomLost = 0;
            try { if (Ransom178.CaptiveCrown(h) == old) ransomLost = Ransom178.PriceFor(h); } catch (Exception e) { Stumble("RunF(okup)", e); }
            // migawka
            var held = HeldHeroes();
            var kin = new List<Hero>();
            foreach (var x in pc.Heroes) if (x != null && x.IsAlive && x.IsPrisoner && x != me) kin.Add(x);
            int ws0 = me.OwnedWorkshops.Count;
            float inf0 = pc.Influence;
            var ruler = old.Leader;
            var heads = new List<Hero>();
            foreach (var c in old.Clans)
                if (c != null && c != pc && !c.IsEliminated && !c.IsUnderMercenaryService && c.Leader != null && c.Leader.IsAlive && c.Leader != ruler && c.Leader != me) heads.Add(c.Leader);
            var atWarBefore = new List<Kingdom>();
            foreach (var k in Kingdom.All) if (k != null && !k.IsEliminated && k.IsAtWarWith(old)) atWarBefore.Add(k);
            Log.Info(Tag367b + ": migawka przed przysiega - " + Name(h) + " | jency rodu gracza " + held.Count + " (" + ByFaction(held) + ") | krewni w niewoli " + kin.Count + " (" + Captors(kin) + ")"
                     + " | warsztaty " + ws0 + " (w miastach przyszlych wrogow " + LostWorkshops(old, nk).Count + ") | wplywy " + Math.Round(inf0) + " | dlug wobec korony " + debt0 + ".");

            EndCaptivityAction.ApplyByReleasedByChoice(h, me);
            int stay = YearDays();
            bool defExc = false;
            try { ChangeKingdomAction.ApplyByJoinToKingdomByDefection(pc, old, nk, CampaignTime.DaysFromNow(stay)); }
            catch (Exception e) { defExc = true; Stumble("RunF(defekcja)", e); }   // ApplyInternal ustawia krolestwo przed sluchaczami - wyjatek sluchacza nie cofa przejscia
            if (pc.Kingdom != nk)
            {
                Log.Info(Tag367b + ": BLAD - " + Name(h) + " uwolniony, ale przejscie " + old.StringId + " -> " + nk.StringId + " nie doszlo (krolestwo gracza teraz "
                         + (pc.Kingdom != null ? pc.Kingdom.StringId : "brak") + (defExc ? ", wyjatek" : "") + ") | dlug do ksiegi " + (debt0 - debt1) + " (wierzyciel " + old.StringId + "), nagrody i 1/9 nietkniete.");
                Log.Player(Name(h) + " goes free, but your oath to " + nk.Name + " could not be sworn.", true);
                return;
            }
            // zdrada dokonana: nagrody od starej korony i 1/9 przepadaja (w tym takcie nie ma rozliczenia o swicie - ThirdsSettle/CrownStep tylko w dobie)
            Ransom178.ForfeitPlayer(old, out rew, out thirds);
            // tytuly krola i jego rodu (za darmo), potem roszczenia pakietu (zloto przed slubami)
            string names; int moved, failed, pm, pf; List<string> lines, wed; List<object> titles;
            SealAndClaims(h, pkg, out titles, out names, out moved, out failed, out lines, out wed, out pm, out pf);
            // relacje
            int rd = 0; var hp = new List<string>();
            try { if (ruler != null && ruler.IsAlive && ruler != me) { ChangeRelationAction.ApplyPlayerRelation(ruler, -FRulerDrop, false, true); rd = FRulerDrop; } } catch (Exception e) { Stumble("RunF(wladca)", e); }
            foreach (var x in heads)
            {
                try
                {
                    int drop = 5 + 5 * Math.Max(0, Trait(x, DefaultTraits.Honor));
                    ChangeRelationAction.ApplyPlayerRelation(x, -drop, false, false);
                    hp.Add(Name(x) + " -" + drop);
                }
                catch (Exception e) { Stumble("RunF(glowy)", e); }
            }
            // skutki gry
            int freedN = 0; var freedBy = new List<Hero>();
            foreach (var x in held) if (x != h && !x.IsPrisoner) { freedN++; freedBy.Add(x); }
            int kinFreed = 0; foreach (var x in kin) if (!x.IsPrisoner) kinFreed++;
            int wsLost = ws0 - me.OwnedWorkshops.Count;
            var warsNow = new List<string>(); var peaceNow = new List<string>();
            foreach (var k in Kingdom.All) if (k != null && !k.IsEliminated && k != nk && k.IsAtWarWith(nk)) warsNow.Add(k.StringId);
            foreach (var k in atWarBefore) if (k != nk && !k.IsAtWarWith(nk)) peaceNow.Add(k.StringId);
            Log.Info(Tag367b + ": WYKONANE - " + Name(h) + " | F " + old.StringId + " -> " + nk.StringId + " (zostaje do d." + Math.Floor(pc.ShouldStayInKingdomUntil.ToDays).ToString(Inv) + ")" + (defExc ? " | UWAGA: wyjatek w defekcji (sluchacz) - skutki gry moga byc niepelne" : "")
                     + " | dlug do ksiegi " + (debt0 - debt1) + (debt1 > 0 ? " (zostal w grze " + debt1 + " - gra wyzerowala)" : "") + " | nagrody skreslone " + rew + ", 1/9 skreslona " + thirds
                     + " | okup " + old.StringId + " przepada " + ransomLost + " | jency uwolnieni przez gre " + freedN + " (frakcje " + ByFaction(freedBy) + "), rod gracza uwolniony " + kinFreed
                     + " | warsztaty utracone " + wsLost + " | wplywy " + Math.Round(inf0) + " -> " + Math.Round(pc.Influence) + " | wojny [" + string.Join(", ", warsNow.ToArray()) + "], pokoje [" + string.Join(", ", peaceNow.ToArray()) + "]"
                     + " | pieczec krola " + moved + "/" + titles.Count + " (" + names + ")" + (failed > 0 ? ", nieudane " + failed : "")
                     + " | T2: " + (pkg.Count > 0 ? string.Join(" || ", lines.ToArray()) + " (przeniesione " + pm + ", nieudane " + pf + ")" : (d.Claims.Length == 0 ? "bez pakietu" : "-"))
                     + " | relacje: wladca " + (rd > 0 ? "-" + rd : "0") + ", glowy rodow " + hp.Count + " x (" + string.Join(", ", hp.ToArray()) + ")"
                     + " | uraza 367d: brak (przysiega - krol zyskuje wasala) | kiesa " + pg0 + " -> " + me.Gold + ".");
            Log.Player("You bend the knee to " + Name(h) + " and set him free. Your house is sworn to " + nk.Name + " for a year and keeps all its fiefs"
                       + (moved + pm > 0 ? "; " + (moved + pm) + " titles to your lands are confirmed" : "") + "."
                       + (wed.Count > 0 ? " The marriage is sealed: " + Join(wed) + " joins your house." : "")
                       + " " + old.Name + " will not forget it.");
        }

        /// <summary>367b/367e: po udanej przysiedze - tytuly krola i jego rodu (pieczec, za darmo), potem roszczenia pakietu (zloto przed slubami). Wspolne dla RunF i RunFNone.</summary>
        private static void SealAndClaims(Hero h, List<Pick367> pkg, out List<object> titles, out string names, out int moved, out int failed, out List<string> lines, out List<string> wed, out int pm, out int pf)
        {
            var me = Hero.MainHero;
            titles = TitlesFor(h, out names);
            moved = 0; failed = 0;
            foreach (var t in titles)
            {
                try { var from = LandBk.DeJure(t); if (from != null && LandBk.Inherit(from, me, t) && LandBk.DeJure(t) == me) moved++; else failed++; }
                catch (Exception e) { Stumble("SealAndClaims(pieczec)", e); failed++; }
            }
            pkg.Sort((a, b) => (a.Mode == 'M' ? 1 : 0).CompareTo(b.Mode == 'M' ? 1 : 0));
            lines = new List<string>(); wed = new List<string>(); pm = 0; pf = 0;
            foreach (var p in pkg)
            {
                Done r;
                try { r = RunClaim(p); }
                catch (Exception e) { Stumble("RunF(posiadacz)", e); r = new Done { Pl = Name(p.C.Holder) + ": wyjatek", Failed = p.C.Titles.Count }; }
                lines.Add(r.Pl); pm += r.Moved; pf += r.Failed;
                if (r.Moved > 0 && p.Mode == 'M' && p.C.Groom.Spouse == p.C.Bride) wed.Add(Name(p.C.Bride));
            }
        }

        /// <summary>367e: podpowiedz F dla rodu gracza bez krolestwa - wstapienie do korony jenca (ApplyByJoinToKingdom): bez zdrady, bez starej korony, wplywy zostaja
        /// (gra zeruje je tylko przy wyjsciu z krolestwa - Clan.ClanLeaveKingdom). Wojna rodu z korona jenca konczy sie (AdjustFactionStancesForClanJoiningKingdom).</summary>
        private static string FHintNone(Hero king, Kingdom nk)
        {
            var pc = Clan.PlayerClan;
            var sb = new StringBuilder(600);
            sb.Append(" Bending the knee to ").Append(Name(king)).Append(": your house is sworn to ").Append(nk.Name).Append(" for a year, keeping all your fiefs.");
            if (_titles.Count > 0) sb.Append(" His seal confirms ").Append(_titleNames).Append('.');
            if (FactionManager.IsAtWarAgainstFaction(pc, nk)) sb.Append(" Your war with ").Append(nk.Name).Append(" ends.");
            List<Kingdom> wars, peace; WarsAfter(null, nk, out wars, out peace);
            if (wars.Count > 0) sb.Append(" His wars become yours: ").Append(Names(wars, 0)).Append('.');
            if (peace.Count > 0) sb.Append(" You make peace with ").Append(Names(peace, 0)).Append('.');
            int freed = 0; foreach (var x in HeldHeroes()) if (GameFrees(x, king, null, nk)) freed++;
            int kin = KinFreed(null, nk).Count;
            if (freed > 0 || kin > 0)
            {
                sb.Append(' ').Append(nk.Name);
                if (freed > 0) sb.Append(" sets free, without ransom, the ").Append(freed).Append(freed == 1 ? " captive" : " captives").Append(" you hold from realms at peace with it");
                if (kin > 0) sb.Append(freed > 0 ? "; it frees " : " frees ").Append(kin).Append(" of your kin");
                sb.Append('.');
            }
            int ws = LostWorkshops(null, nk).Count;
            if (ws > 0) sb.Append(" You lose your ").Append(ws).Append(ws == 1 ? " workshop" : " workshops").Append(" in towns of your new enemies.");
            return sb.ToString();
        }

        /// <summary>367e: wykonanie F dla rodu bez krolestwa (Deal.From == FromNone). Jak RunF, ale: ChangeKingdomAction.ApplyByJoinToKingdom (gra 1.4.8 cs:225587; BK daje
        /// wtedy Lesser Peerage, gdy rod nie ma prawa glosu - bk.cs:91955), bez starej korony: bez dlugu do ksiegi, bez nagrod, bez kary relacji dawnego wladcy i jego
        /// rodow. 1/9 sprzed przysiegi (gdyby zostala w _acc) skreslana - nie nalezy sie nowej koronie. Bez urazy 367d (krol zyskuje wasala).</summary>
        private static void RunFNone(Deal d, Hero h)
        {
            var me = Hero.MainHero;
            var pc = Clan.PlayerClan;
            if (pc == null || pc.Kingdom != null) { Drop(d, h, "your house has sworn to another crown since.", "gracz juz w krolestwie " + (pc != null && pc.Kingdom != null ? pc.Kingdom.StringId : "-") + " (umowa z: bez krolestwa)"); return; }
            string en, pl;
            if (!FGate(h, true, out en, out pl)) { Drop(d, h, en.Length > 0 && char.IsUpper(en[0]) ? en.Substring(0, 1).ToLowerInvariant() + en.Substring(1) : en, pl); return; }
            var nk = (Kingdom)h.MapFaction;
            var pkg = new List<Pick367>();
            if (!string.IsNullOrEmpty(d.Claims))
            {
                if (!ClaimsOn) { Drop(d, h, "the claims of his lords can no longer be settled.", "klucz CaptiveTermsVassalTitles albo BK wylaczone"); return; }
                pkg = Recheck(d, h, out en, out pl);
                if (pkg == null) { Drop(d, h, en, pl); return; }
            }
            long gold = GoldOf(pkg);
            if (me.Gold < gold) { Drop(d, h, "you no longer have " + gold.ToString("N0", Inv) + " denars.", "za malo zlota gracza (" + me.Gold + " < " + gold + ")"); return; }

            // ---- od tego miejsca nieodwracalne
            long pg0 = me.Gold;
            int debt0 = pc.DebtToKingdom;
            var held = HeldHeroes();
            var kin = new List<Hero>();
            foreach (var x in pc.Heroes) if (x != null && x.IsAlive && x.IsPrisoner && x != me) kin.Add(x);
            int ws0 = me.OwnedWorkshops.Count;
            float inf0 = pc.Influence;
            bool warNk = FactionManager.IsAtWarAgainstFaction(pc, nk);
            var atWarBefore = new List<Kingdom>();
            foreach (var k in Kingdom.All) if (k != null && !k.IsEliminated && FactionManager.IsAtWarAgainstFaction(k, pc)) atWarBefore.Add(k);
            Log.Info(Tag367b + ": migawka przed przysiega (z: bez krolestwa) - " + Name(h) + " | wojna rodu z " + nk.StringId + " " + (warNk ? "TAK" : "NIE") + " | jency rodu gracza " + held.Count + " (" + ByFaction(held) + ")"
                     + " | krewni w niewoli " + kin.Count + " (" + Captors(kin) + ") | warsztaty " + ws0 + " (w miastach przyszlych wrogow " + LostWorkshops(null, nk).Count + ") | wplywy " + Math.Round(inf0) + " | dlug wobec korony " + debt0 + ".");

            EndCaptivityAction.ApplyByReleasedByChoice(h, me);
            int stay = YearDays();
            bool joinExc = false;
            try { ChangeKingdomAction.ApplyByJoinToKingdom(pc, nk, CampaignTime.DaysFromNow(stay)); }
            catch (Exception e) { joinExc = true; Stumble("RunFNone(wstapienie)", e); }   // ApplyInternal ustawia krolestwo przed sluchaczami - wyjatek sluchacza nie cofa wstapienia
            if (pc.Kingdom != nk)
            {
                Log.Info(Tag367b + ": BLAD - " + Name(h) + " uwolniony, ale wstapienie (z: bez krolestwa) -> " + nk.StringId + " nie doszlo (krolestwo gracza teraz "
                         + (pc.Kingdom != null ? pc.Kingdom.StringId : "brak") + (joinExc ? ", wyjatek" : "") + ").");
                Log.Player(Name(h) + " goes free, but your oath to " + nk.Name + " could not be sworn.", true);
                return;
            }
            long rew, thirds;
            Ransom178.ForfeitPlayer(null, out rew, out thirds);   // old null: nagrody nietkniete, tylko 1/9 sprzed przysiegi
            string names; int moved, failed, pm, pf; List<string> lines, wed; List<object> titles;
            SealAndClaims(h, pkg, out titles, out names, out moved, out failed, out lines, out wed, out pm, out pf);
            int freedN = 0; var freedBy = new List<Hero>();
            foreach (var x in held) if (x != h && !x.IsPrisoner) { freedN++; freedBy.Add(x); }
            int kinFreed = 0; foreach (var x in kin) if (!x.IsPrisoner) kinFreed++;
            int wsLost = ws0 - me.OwnedWorkshops.Count;
            var warsNow = new List<string>(); var peaceNow = new List<string>();
            foreach (var k in Kingdom.All) if (k != null && !k.IsEliminated && k != nk && k.IsAtWarWith(nk)) warsNow.Add(k.StringId);
            foreach (var k in atWarBefore) if (k == nk || !k.IsAtWarWith(nk)) peaceNow.Add(k.StringId);
            Log.Info(Tag367b + ": WYKONANE - " + Name(h) + " | F z: bez krolestwa -> " + nk.StringId + " (zostaje do d." + Math.Floor(pc.ShouldStayInKingdomUntil.ToDays).ToString(Inv) + ")" + (joinExc ? " | UWAGA: wyjatek przy wstapieniu (sluchacz) - skutki gry moga byc niepelne" : "")
                     + " | wojna rodu z " + nk.StringId + " " + (warNk ? (FactionManager.IsAtWarAgainstFaction(pc, nk) ? "TRWA (!)" : "zakonczona") : "nie bylo") + " | dlug " + debt0 + " -> " + pc.DebtToKingdom + " | 1/9 skreslona " + thirds
                     + " | jency uwolnieni przez gre " + freedN + " (frakcje " + ByFaction(freedBy) + "), rod gracza uwolniony " + kinFreed
                     + " | warsztaty utracone " + wsLost + " | wplywy " + Math.Round(inf0) + " -> " + Math.Round(pc.Influence) + " | wojny [" + string.Join(", ", warsNow.ToArray()) + "], pokoje [" + string.Join(", ", peaceNow.ToArray()) + "]"
                     + " | pieczec krola " + moved + "/" + titles.Count + " (" + names + ")" + (failed > 0 ? ", nieudane " + failed : "")
                     + " | T2: " + (pkg.Count > 0 ? string.Join(" || ", lines.ToArray()) + " (przeniesione " + pm + ", nieudane " + pf + ")" : (string.IsNullOrEmpty(d.Claims) ? "bez pakietu" : "-"))
                     + " | uraza 367d: brak (przysiega - krol zyskuje wasala) | kiesa " + pg0 + " -> " + me.Gold + ".");
            Log.Player("You bend the knee to " + Name(h) + " and set him free. Your house is sworn to " + nk.Name + " for a year and keeps all its fiefs"
                       + (moved + pm > 0 ? "; " + (moved + pm) + " titles to your lands are confirmed" : "") + "."
                       + (wed.Count > 0 ? " The marriage is sealed: " + Join(wed) + " joins your house." : ""));
        }


        private static string ByFaction(List<Hero> l)
        {
            var by = new Dictionary<string, int>();
            foreach (var x in l) { string k = x != null && x.MapFaction != null ? x.MapFaction.StringId : "-"; int n; by.TryGetValue(k, out n); by[k] = n + 1; }
            var parts = new List<string>();
            foreach (var kv in by) parts.Add(kv.Key + " " + kv.Value);
            return parts.Count > 0 ? string.Join(", ", parts.ToArray()) : "-";
        }

        private static string Captors(List<Hero> l)
        {
            var parts = new List<string>();
            foreach (var x in l)
            {
                var cap = x.PartyBelongedToAsPrisoner != null ? x.PartyBelongedToAsPrisoner.MapFaction : (x.CurrentSettlement != null ? x.CurrentSettlement.MapFaction : null);
                parts.Add(Name(x) + " u " + (cap != null ? cap.StringId : "-"));
            }
            return parts.Count > 0 ? string.Join(", ", parts.ToArray()) : "-";
        }
    }
}
