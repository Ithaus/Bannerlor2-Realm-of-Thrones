using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// ZJ (sw-zajazdy) - NOCLEG W ZAJEZDZIE PRZY SZLAKU: ten sam rachunek co karczma miasta (Plan - szeregowi z wolnej sakiewki ludzi za siebie i konia, towarzysze
    /// z kiesy i brak z sakiewki, gracz za pokoj, swojego konia i konie bez jezdzca), te same ceny i premie (QSpeed / QMorale / QSleep / QHeal), tylko:
    ///  - jakosci zajazdu z pliku (1-3, zwykle 1-2); premia cen dobrego domu - nie (zajazd nie ma targu); okna lupu miast - nie;
    ///  - pieniadze do WSI OKREGU zajazdu (zamknieta ekonomia: platnik sakiewka ludzi / kiesy bohaterow, odbiorca kiesa wsi + licznik podatku wsi): szeregowi
    ///    MenPurse.InnPayVillage (podatek pana z czynnego modelu SettlementTaxModel na TradeTaxAccumulated, reszta do kiesy - regula czesci wsi markietanow; wies rodu
    ///    gracza - calosc do kiesy), bohaterowie GiveGoldAction do wsi, potem ta sama czesc podatku z kiesy na licznik (przelew w obrebie wsi); od czesci w kiesie
    ///    udzial korony DS (DsVillage.WarShareSutlers - jak czesc wsi markietanow; wies spoza DS - 0);
    ///  - kontrola obiegu przy kazdym noclegu: sakiewki ludzi + kiesy gracza i towarzyszy + kiesa wsi + licznik podatku wsi przed i po, plus przelane do korony - roznica 0;
    ///  - sen w menu NightRest jak w obozie (partia poza osada - namiot przy zajezdzie), pobudka nie przed switem (wieczorem i w nocy), premia "wypoczeci" do zmierzchu
    ///    jak po karczmie (stan _rest z RoadUid; zapis arm_inns pole 16). Sen dalej niz 3 jedn. od zajazdu (partia odjechala) - stan zdjety bez premii (InnSleep).
    /// Stara sciezka karczmy miasta bez zmian (RoadUid == null).
    /// </summary>
    internal static partial class TownInns
    {
        private const string PR = "Zajazdy (ZJ): ";
        private const float RoadSleepR = 3.0f;   // partia najwyzej tyle od miejsca postoju zajazdu = sen "w zajezdzie"
        // liczniki doby noclegow w zajazdach (linia "Zajazdy (ZJ): dzien N" - RoadDayText)
        private static int _rdStays, _rdStumbles;
        private static readonly int[] _rdPeople = new int[4];
        private static long _rdMen, _rdComp, _rdCompPurse, _rdPlayer, _rdPurse, _rdTax, _rdCrown, _rdDiff;

        /// <summary>Recenzja: podatek pana od noclegu w zajezdzie tylko przy kluczu VillageTributeFromSutlers - ta sama regula co czesc wsi markietanow (Sutlers.One:
        /// wylaczony = calosc do kiesy wsi, udzial korony DS od calosci). Jedna regula na jedno zjawisko (dochod wsi z uslug dla wojska).</summary>
        internal static bool RoadTributeOn { get { var s = Settings.Current; return s != null && s.VillageTributeFromSutlers; } }

        internal static void RoadDayReset()
        {
            _rdStays = _rdStumbles = 0;
            Array.Clear(_rdPeople, 0, 4);
            _rdMen = _rdComp = _rdCompPurse = _rdPlayer = _rdPurse = _rdTax = _rdCrown = _rdDiff = 0;
        }

        /// <summary>Czesc linii doby zajazdow: noclegi, platnicy i odbiorcy, kontrola obiegu. Zeruje liczniki.</summary>
        internal static string RoadDayText()
        {
            long paid = _rdMen + _rdComp + _rdPlayer;
            string t = "noclegi " + _rdStays + ": osoby " + (_rdPeople[0] + _rdPeople[1] + _rdPeople[2] + _rdPeople[3]) + " (wspolna izba " + _rdPeople[1] + ", porzadna karczma " + _rdPeople[2]
                       + ", dobry dom " + _rdPeople[3] + ", stajnia " + _rdPeople[0] + ") | zaplacone " + paid + " (ludzie " + _rdMen + ", towarzysze " + _rdComp + " (z sakiewki ludzi "
                       + _rdCompPurse + "), gracz " + _rdPlayer + ") -> kiesy wsi " + _rdPurse + ", podatek panow wsi " + _rdTax + ", korona (DS) " + _rdCrown
                       + " (razem " + (_rdPurse + _rdTax + _rdCrown) + ")"
                       + " | kontrola obiegu (prog 0): roznica " + _rdDiff + ", zaplacone - odebrane " + (paid - _rdPurse - _rdTax - _rdCrown) + " | potkniecia noclegu " + _rdStumbles;
            RoadDayReset();
            return t;
        }

        private static void RoadStumble(string where, Exception e) { _rdStumbles++; Stumble(where, e); }

        /// <summary>Partia gracza przy zajezdzie stanu noclegu (poza osada, do RoadSleepR od miejsca postoju). Watek glowny.</summary>
        private static bool RoadNear(Rest r)
        {
            try
            {
                var mp = MobileParty.MainParty;
                return r != null && r.RoadUid != null && mp != null && mp.CurrentSettlement == null && RoadsideInns.PlayerDist(r.RoadUid) <= RoadSleepR;
            }
            catch { return false; }
        }

        /// <summary>m95-zajazd (RoadsideInnStay): uid zajazdu, w ktorym gracz spi teraz po oplaconym noclegu (faza 1, partia przy zajezdzie - RoadNear); inaczej null.</summary>
        internal static string RoadSleepUid()
        {
            var r = _rest;
            return r != null && r.Phase == 1 && r.RoadUid != null && RoadNear(r) ? r.RoadUid : null;
        }

        /// <summary>NightRest.SleepInit poza osada po oplaconym noclegu w zajezdzie: poczatek snu, pobudka nie przed switem (gdy swit w 12 h), tlo wsi, tekst.
        /// null = to nie sen w zajezdzie (tekst NightRest jak dotad).</summary>
        private static string RoadSleepInit(MenuCallbackArgs args, Rest r)
        {
            if (r == null || r.Phase != 1 || !RoadNear(r)) return null;
            long now = NowMin();
            if (r.StartMin <= 0)
            {
                r.StartMin = now;
                long dawn = NextAt(now, NightRest.PlayerDawn);
                r.WakeMin = dawn - now <= HoldMaxMin ? dawn : 0;
                Log.Info(PR + "sen w zajezdzie (" + QLog[Math.Max(0, r.Q)] + ") " + r.Town + " od " + When(now) + ", "
                         + (r.WakeMin > 0 ? "pobudka nie przed " + When(r.WakeMin) + " (swit)" : "bez czekania do switu (swit dalej niz 12 h) - sen do wyspania")
                         + ", sen x" + r.Sleep.ToString("0.00", Inv) + ", gojenie +" + (r.Heal * 100f).ToString("0", Inv) + "%.");
            }
            try
            {
                var inn = RoadsideInns.Find(r.RoadUid);
                string mesh = inn != null && inn.Village != null && inn.Village.SettlementComponent != null ? inn.Village.SettlementComponent.WaitMeshName : null;
                if (!string.IsNullOrEmpty(mesh)) args.MenuContext.SetBackgroundMeshName(mesh);
            }
            catch { }
            RoadsideInnStay.OnSleepInit(args, r.RoadUid);   // m95-zajazd: sen w zajezdzie - partia schowana (po wczytaniu w trakcie snu tez; wlasny try); dzwiek - RoadsideInnStay.InnSleepSound
            string bed = r.Q == 2 ? "The keeper of " + r.Town + " gives you his best rooms and the men sleep in proper beds"
                       : r.Q == 1 ? "The men drink their ale by the hearth of " + r.Town + " and sleep in beds"
                       : "The men bed down on the straw of the common room of " + r.Town + ", the horses in the yard";
            return bed + (r.WakeMin > 0 ? " - nobody stirs before dawn." : ".");
        }

        /// <summary>Menu zajazdu: zajazd tu i teraz przyjmuje goscia (te same warunki co karczma miasta, plus zajazd przy partii i wies nie rabowana).
        /// Recenzja calosci 17/18.10: klucz RoadsideInns wylaczony - bez noclegu (zapis wczytany w srodku menu zajazdu wracal z czynnymi opcjami; plotki juz tak mialy,
        /// zostaje "Ride on"); wies okregu we wojnie z gracza frakcja - bez noclegu (jak karczma wrogiego miasta, do ktorego gra nie wpuszcza; pieniadze nie ida do wroga).</summary>
        private static bool RoadAllowed(RoadsideInns.Inn inn, out string why)
        {
            why = null;
            var mp = MobileParty.MainParty;
            if (inn == null || inn.Village == null || mp == null || Hero.MainHero == null) { why = "There is no bed to be had here."; return false; }
            if (!RoadsideInns.On) { why = "Roadside inns are switched off (game options)."; return false; }
            if (!On) { why = "Lodgings need Town Inns and Night Rest (game options)."; return false; }
            if (!RoadsideInns.AtInn(inn)) { why = "You are not at the inn."; return false; }
            try
            {
                var vf = inn.Village.MapFaction; var pf = Hero.MainHero.MapFaction;
                if (vf != null && pf != null && FactionManager.IsAtWarAgainstFaction(vf, pf)) { why = "The keeper bars the door to the enemies of his lord."; return false; }
            }
            catch { }
            if (mp.MapEvent != null) { why = "Not in the middle of a fight."; return false; }
            if (Campaign.Current != null && Campaign.Current.IsMainHeroDisguised) { why = "In disguise you cannot bring your company to an inn."; return false; }
            if (NightRest.EnlistedNow) { why = "In service your lord decides where the men sleep."; return false; }
            if (mp.Army != null && mp.Army.LeaderParty != mp) { why = "You march in another lord's army - he decides where the host sleeps."; return false; }
            if (InSleepMenu()) { why = "The men are already asleep."; return false; }
            var v = inn.Village.Village;
            if (v != null && v.VillageState == Village.VillageStates.BeingRaided) { why = "Raiders are on these lands - the inn is barred and the keeper hides."; return false; }
            return true;
        }

        /// <summary>Opcja jakosci q w menu zajazdu: tylko jakosci zajazdu (q &lt; Tiers); rachunek i podpowiedz jak w karczmie miasta.</summary>
        internal static bool RoadQCondition(MenuCallbackArgs args, RoadsideInns.Inn inn, int q)
        {
            try
            {
                args.optionLeaveType = GameMenuOption.LeaveType.Wait;
                if (inn == null || q < 0 || q >= inn.Tiers || q > 2) return false;
                string why;
                if (!RoadAllowed(inn, out why))
                {
                    MBTextManager.SetTextVariable("ARM_ROADINN_Q" + q, ShortLabels.Pick(QName[q], QName[q] + " (" + QWhat[q] + ")"));   // n20-napisy
                    args.IsEnabled = false;
                    args.Tooltip = new TextObject("{=!}" + why + (ShortLabels.On ? "\n" + QName[q] + ": " + QWhat[q] + "." : ""));
                    return true;
                }
                var b = Plan(null, q, false, false);
                int men = b.Foot + b.Mounted;
                string split = b.Total + " d: you " + b.PlayerCost + (men > 0 ? ", the men " + b.MenCost : "") + (b.Comp.Count > 0 ? ", companions " + b.CompCostSum : "");
                // n20-napisy: na przycisku rodzaj noclegu i suma; co daje i kto ile placi - pierwsza linia podpowiedzi
                MBTextManager.SetTextVariable("ARM_ROADINN_Q" + q, ShortLabels.Pick(QName[q] + " - " + b.Total + " d", QName[q] + " (" + QWhat[q] + ") - " + split));
                var t = new StringBuilder();
                if (ShortLabels.On) t.Append(QName[q]).Append(" (").Append(QWhat[q]).Append(") - ").Append(split).Append(".\n");
                t.Append(PMan[q]).Append(" d a man, ").Append(PHorse[q]).Append(" d a horse, ").Append(RoomOf(q)).Append(" d ").Append(RoomWord(q)).Append(".");
                if (men > 0)
                {
                    t.Append("\nThe men (").Append(men).Append(", ").Append(b.Mounted).Append(" mounted): ");
                    var parts = new List<string>();
                    for (int l = 2; l >= 0; l--) { int n = b.FootAt[l] + b.MountAt[l]; if (n > 0) parts.Add(n + " in a " + QualityOf(l)); }
                    if (b.MenStable > 0) parts.Add(b.MenStable + " in the stable");
                    t.Append(string.Join(", ", parts.ToArray())).Append(b.MenNoPurse ? " - they keep no purses." : " - they pay " + b.MenCost + " of the " + b.MenFree + " d they can spare.");
                }
                if (b.Comp.Count > 0)
                {
                    t.Append("\nCompanions: ");
                    var parts = new List<string>();
                    for (int i = 0; i < b.Comp.Count; i++)
                        parts.Add(b.Comp[i].Name + " " + (b.CompQ[i] < 0 ? "the stable" : QualityOf(b.CompQ[i]) + " " + b.CompCost[i] + " d"));
                    t.Append(string.Join(", ", parts.ToArray())).Append(".");
                }
                t.Append("\nYou: ").Append(RoomWord(q) == "your bed" ? "bed " : "room ").Append(RoomOf(q));
                if (b.OwnHorse) t.Append(" + your horse ").Append(PHorse[q]);
                if (b.Spare + b.ArmorySpare > 0) t.Append(" + ").Append(b.Spare + b.ArmorySpare).Append(" horses that carry nobody x ").Append(PHorse[q]);
                t.Append(" = ").Append(b.PlayerCost).Append(" d (you have ").Append(Hero.MainHero.Gold).Append(").");
                float sp, mo, sl, he; Bonus(b, out sp, out mo, out sl, out he);
                bool atDawn; long dawn;
                long wake = PredictWake(NowMin(), sl, out atDawn, out dawn);
                t.Append(!atDawn ? "\nBy day you sleep until the men have slept (about " : wake <= dawn ? "\nYou sleep until dawn (" : "\nYou sleep past dawn, until the men have slept (about ")
                 .Append(Clock(wake)).Append(") and wake rested until ").Append(Clock(RestedUntil(wake))).Append(": +").Append(F1(sp)).Append("% speed, +").Append(F1(mo)).Append(" morale");
                if (sl > 1.001f) t.Append(", sleep x").Append(sl.ToString("0.00", Inv));
                if (he > 0.001f) t.Append(", healing tonight +").Append((he * 100f).ToString("0", Inv)).Append("%");
                t.Append(". The coin goes to the village of ").Append(inn.Village.Name).Append(RoadTax(inn.Village, 100) > 0 ? " (its lord takes his share)." : ".");
                if (NightRest.Debt > 0) t.Append(" The men still owe sleep - the bed's comfort shows once it is paid off.");
                if (!b.PlayerCan) { args.IsEnabled = false; t.Append("\nYou cannot pay your own part (").Append(b.PlayerCost).Append(" d)."); }
                args.Tooltip = new TextObject("{=!}" + t.ToString());
                return true;
            }
            catch (Exception e) { RoadStumble("RoadQCondition", e); return false; }
        }

        /// <summary>Suma posiadaczy dotknietych noclegiem w zajezdzie: sakiewki ludzi, kiesy gracza i towarzyszy, kiesa wsi i licznik podatku wsi.</summary>
        private static long RoadWorld(Settlement vs, List<Hero> comp)
        {
            var v = vs.Village;
            long w = MenPurse.TotalNow() + Hero.MainHero.Gold + (v != null ? (long)v.Gold + v.TradeTaxAccumulated : 0L);
            foreach (var h in comp) w += h.Gold;
            return w;
        }

        /// <summary>Podatek pana od utargu wsi wedlug czynnego modelu (SettlementTaxModel.CalculateVillageTaxFromIncome - ta sama regula co VillageTakings / markietani),
        /// w granicach 0..income. Wies rodu partii gracza - bez podatku (jak markietani, K3); klucz VillageTributeFromSutlers wylaczony - bez podatku (jak markietani,
        /// recenzja). Wyjatek modelu = 0 (calosc zostaje w kiesie).</summary>
        private static int RoadTax(Settlement vs, int income)
        {
            var v = vs != null ? vs.Village : null;
            if (v == null || income <= 0 || !RoadTributeOn) return 0;
            if (MobileParty.MainParty != null && ReferenceEquals(vs.OwnerClan, MobileParty.MainParty.ActualClan)) return 0;
            try
            {
                var m = Campaign.Current.Models.SettlementTaxModel;
                int t = m != null ? m.CalculateVillageTaxFromIncome(v, income) : 0;
                return t < 0 ? 0 : (t > income ? income : t);
            }
            catch { return 0; }
        }

        /// <summary>Bohater -> wies: przelew gry do kiesy wsi (GiveGoldAction), potem czesc podatku z kiesy na licznik podatku wsi (w obrebie wsi) i udzial korony DS od
        /// reszty (DsVillage.WarShareSutlers - jak markietani). Zwraca zaplacone; `tax` - na licznik podatku, `crown` - do korony. Recenzja: oba odplywy z kiesy wsi
        /// (na licznik podatku i do korony) poza tickiem dobowym nazwane w ksiedze przeplywow osad (MoneyLedger.Note169 "inne"); przelew gry ksiega widzi sama.</summary>
        private static int RoadHeroPays(Hero h, Settlement vs, int cost, bool quiet, out int tax, out int crown)
        {
            tax = 0; crown = 0;
            if (h == null || cost <= 0 || vs == null || vs.Village == null) return 0;
            int had = h.Gold;
            GiveGoldAction.ApplyForCharacterToSettlement(h, vs, cost, quiet);
            int paid = Math.Max(0, had - h.Gold);
            if (paid > 0)
            {
                var v = vs.Village;
                int t = Math.Min(RoadTax(vs, paid), Math.Max(0, v.Gold));
                if (t > 0) { v.ChangeGold(-t); v.TradeTaxAccumulated += t; tax = t; MoneyLedger.Note169(MoneyLedger.N169Other, vs, -t); }   // kiesa -> licznik podatku (tylko licznik ksiegi)
                if (paid - tax > 0) crown = DsVillage.WarShareSutlers(v, paid - tax, true);   // wlasny try - nie rzuca
                if (crown > 0) MoneyLedger.Note169(MoneyLedger.N169Other, vs, -crown);       // kiesa wsi -> skarbiec korony (tylko licznik)
            }
            return paid;
        }

        /// <summary>Zaplata i sen w zajezdzie (opcja zwyklego menu "arm_roadinn" - SwitchToMenu do snu wolno, jak w karczmie miasta).</summary>
        internal static void RoadCommit(RoadsideInns.Inn inn, int q)
        {
            try
            {
                var mp = MobileParty.MainParty;
                var me = Hero.MainHero;
                string why;
                if (inn == null || q < 0 || q >= inn.Tiers || q > 2 || !RoadAllowed(inn, out why)) { Log.Player("No bed is to be had here now.", true); return; }
                var vs = inn.Village;
                var b = Plan(null, q, true, false);
                if (!b.PlayerCan) { Log.Player("You cannot pay your own part of the lodging (" + b.PlayerCost + " d).", true); return; }
                long w0 = RoadWorld(vs, b.Comp);
                int purseTax = 0, purseCrown = 0, pursePaid = 0, purseNeed = b.MenCost + b.CompPurseSum;
                if (purseNeed > 0) pursePaid = MenPurse.InnPayVillage(mp, vs, purseNeed, out purseTax, out purseCrown);
                int menPaid = Math.Min(pursePaid, b.MenCost), compPursePaid = pursePaid - menPaid;
                int compGoldPaid = 0, compTax = 0, compCrown = 0;
                var compLog = new StringBuilder();
                for (int i = 0; i < b.Comp.Count; i++)
                {
                    int tx, cw, paid = RoadHeroPays(b.Comp[i], vs, b.CompGold[i], true, out tx, out cw);
                    compGoldPaid += paid; compTax += tx; compCrown += cw;
                    if (compLog.Length > 0) compLog.Append(", ");
                    compLog.Append(b.Comp[i].StringId).Append(' ').Append(b.CompQ[i] < 0 ? "stajnia" : QLog[b.CompQ[i]]).Append(' ').Append(paid + b.CompPurse[i]);
                }
                int compPaid = compGoldPaid + compPursePaid;
                int playerTax, playerCrown, playerPaid = RoadHeroPays(me, vs, b.PlayerCost, false, out playerTax, out playerCrown);
                int crownAll = purseCrown + compCrown + playerCrown;
                long diff = RoadWorld(vs, b.Comp) + crownAll - w0;   // korona DS poza suma posiadaczy noclegu - dodana z powrotem (przelew wies -> korona)
                int paidAll = menPaid + compPaid + playerPaid, taxAll = purseTax + compTax + playerTax, purseAll = paidAll - taxAll - crownAll;
                float sp, mo, sl, he; Bonus(b, out sp, out mo, out sl, out he);
                long now = NowMin();
                _rest = new Rest { Phase = 1, Q = q, PlayerQ = q, TownId = vs.StringId, TownName = inn.Name, RoadUid = inn.Uid, CommitMin = now,
                                   Speed = sp, Morale = mo, Sleep = sl, Heal = he, Price = false };
                _rdStays++; for (int l = 0; l < 4; l++) _rdPeople[l] += b.People[l];
                _rdMen += menPaid; _rdComp += compPaid; _rdCompPurse += compPursePaid; _rdPlayer += playerPaid; _rdPurse += purseAll; _rdTax += taxAll; _rdCrown += crownAll; _rdDiff += diff;
                Log.Info(PR + "nocleg w " + inn.Name + " (" + inn.Uid + ", okreg " + vs.Name + " " + vs.StringId + ") " + When(now) + " - wybor " + q + " (" + QLog[q] + "), jakosci zajazdu " + inn.Tiers
                         + " | szeregowi " + (b.Foot + b.Mounted) + " (pieszych " + b.Foot + ", konnych " + b.Mounted + "): wspolna izba " + (b.FootAt[0] + b.MountAt[0])
                         + ", porzadna karczma " + (b.FootAt[1] + b.MountAt[1]) + ", dobry dom " + (b.FootAt[2] + b.MountAt[2]) + ", stajnia " + b.MenStable
                         + "; sakiewka ludzi " + b.MenPurse + ", wolne " + b.MenFree + ", zaplacili " + menPaid + " z " + b.MenCost + (b.MenNoPurse ? " (sakiewki wylaczone)" : "")
                         + " | towarzysze " + b.Comp.Count + (compLog.Length > 0 ? ": " + compLog : "") + ", z kiesy " + compGoldPaid + ", z sakiewki ludzi " + compPursePaid
                         + " | gracz: pokoj " + RoomOf(q) + ", swoj kon " + (b.OwnHorse ? 1 : 0) + ", konie bez jezdzca " + (b.Spare + b.ArmorySpare) + " x " + PHorse[q]
                         + " = " + b.PlayerCost + ", zaplacil " + playerPaid
                         + " | razem " + paidAll + " (ludzie " + menPaid + ", towarzysze " + compPaid + ", gracz " + playerPaid + ") -> kiesa wsi " + purseAll + ", podatek pana wsi " + taxAll
                         + ", korona (DS) " + crownAll
                         + " | kontrola obiegu: roznica " + diff
                         + " | osoby " + b.Persons + ": stajnia " + b.People[0] + ", wspolna izba " + b.People[1] + ", porzadna karczma " + b.People[2] + ", dobry dom " + b.People[3]
                         + " | premia po snie: predkosc +" + F1(sp) + "%, morale +" + F1(mo) + ", sen x" + sl.ToString("0.00", Inv) + ", gojenie +" + (he * 100f).ToString("0", Inv) + "%.");
                string men = b.Foot + b.Mounted == 0 ? "" : " Your men paid " + menPaid + " from their purses" + (b.MenStable > 0 ? ", " + b.MenStable + " of them sleep in the stable." : ".");
                Log.Player("You take lodgings at a " + QualityOf(q) + " in " + inn.Name + " - " + paidAll + " denars in all, " + playerPaid + " of them yours." + men
                           + (compPaid > 0 ? " Your companions paid " + compPaid + "." : ""));
                NightRest.BeginInnSleep();
            }
            catch (Exception e) { RoadStumble("RoadCommit", e); }
        }
    }
}
