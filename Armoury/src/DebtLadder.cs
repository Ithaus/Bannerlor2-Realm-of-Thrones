using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// PACZKA 168 - DLUG, KREDYT WOJENNY I ZAJECIE ZAMIAST BANKRUCTWA (projekt etapu 2, krok D; PLAN 2.12; [D] 05.10 lord nigdy nie traci lenna,
    /// 07.10 wariant A - wierzyciel bierze dochod wsi i kiese ponad 38 000, 08.10 dlug wymarlego rodu na nowego pana wsi, 10:45 w wojnie na kredyt splacany
    /// z lupow z limitem zdolnosci splaty). Wylacznik DebtLadderEnabled, czynny tylko z IronBankEnabled i ClanBudgetEnabled (KW siedzi w pulapie 166).
    ///
    ///  - KSIEGA RODU: dlug w Banku (IronBank.Debt - kapital, odsetki, wiarygodnosc) + roszczenia innych wierzycieli: dlug zoldu (ludzie partii, z DebtToKingdom),
    ///    dlug wobec korony (stary zapis; zaliczka gry - splata w nicosc jak OBIEG-1), okupy (178). Szczebel rodu: D1 kredyt, D2 zaleglosc, D3 zajecie, D4 wyprzedaz.
    ///  - KREDYT WOJENNY (KW) - jedyna pozyczka AI: miejsce w pulapie 166 (ClanBudget wola WarCreditRoom/LendWar) - wojna, G < R, bez zaleglosci i zajecia,
    ///    D pokrywa dwor i powinnosci, Bank ma wolny kapital ponad prog; najwyzej 0.40 D dziennie, do limitu (15 x ziemia + lenna + 30 dni lupow, x wiarygodnosc).
    ///  - SPLATA: w wojnie 50% jednorazowych wplywow rodu (lup, okupy, sakwy, trzecia, statki) najpierw na Bank - poza limitem rat; po pokoju 100% jednorazowych
    ///    i rata z D. Raty z D: Bank najwyzej 10% D, wszystkie raty razem najwyzej 15% D (Bank, potem dlug zoldu, dlug wobec korony, okupy wedlug wieku).
    ///  - ZALEGLOSC (D2): placi, ile ma ponad 3 dni zoldu; wiarygodnosc x0.8, +2 pp, KW wstrzymany. Trzecia z rzedu -> ZAJECIE (D3): wierzyciele biora codziennie
    ///    dochod wsi rodu (srednia 28 dob z dzisiejszych wsi) i kiese glowy ponad 38 000; odsetki zamrozone; D budzetu bez dochodu wsi (poczet sam maleje).
    ///    WYPRZEDAZ (D4) co 7 dni tylko przy 14 dobach zerowego zajecia albo prognozie > 728 dni: nadwyzki zbrojowni (AI) na targ, karawany i warsztaty do
    ///    najbogatszego notabla; lenno zostaje. Po splacie - KW dopiero po 182 dobach.
    ///  - BEZ UMORZENIA: dlug wymarlego rodu na nowych panow jego wsi (takze gracza); bez wsi - "dlug bez platnika" w ksiedze.
    ///  - KAPITAL: Bank pozycza z kapitalu (posiadacz w "Pieniadz swiata"); zysk ponad IronBankCapital - do kasy Braavos po 1/180 dziennie (pod tarcza dworu).
    ///  - PORTFELE GRY (GameWalletsNoAdvance): niezaplacony udzial rodu w portfelu najemnikow/trybutu/wezwania zostaje dlugiem krolestwa (portfel), nie
    ///    "zaplacony" z niczego i dopisany rodowi - zamyka zrodlo zaliczki gry (OBIEG-1).
    /// Gracz (PlayerSameLadder): ta sama ksiega i drabina; pozycza recznie w Braavos (w wojnie - KW); bez automatycznej splaty z jednorazowych (u gracza ksiega
    /// liczy tam handel) - raty z D w wojnie i w pokoju; nigdy jego ekwipunek. Platnik -> odbiorca: kapital Banku -> glowa (KW); glowa / rodzina / zajety dochod /
    /// wyprzedaz -> Bank i wierzyciele; dlug zoldu -> sakiewki ludzi (zapasowy odbiorca: inna partia rodu, kasa najblizszego miasta).
    /// </summary>
    internal static class DebtLadder
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        internal const int KMen = 1, KCrownAdv = 2, KCrownOld = 3, KRansom = 4, KGreat = 5;
        private static readonly string[] KindName = { "-", "dlug zoldu", "zaliczka gry", "dlug wobec korony", "okup", "okup wielkiego jenca" };

        internal sealed class Claim { public int Kind; public string Creditor = "", Crown = ""; public long Left, Total; public int Day; }

        internal sealed class L
        {
            public bool Seized; public int SeizeDay = -1, ZeroDays, SaleDay = -1, FreeDay = -1, Missed;
            public long Kw; public double SeizeEma;
            public readonly List<Claim> Claims = new List<Claim>();
            public List<string> Villages = new List<string>();
        }

        private sealed class Loot { public double Ema; public int N; }
        private sealed class Orphan { public string Id = "", Name = ""; public long Bank, Claims; public int Day; }

        private static readonly Dictionary<string, L> _l = new Dictionary<string, L>();
        private static readonly Dictionary<string, Loot> _loot = new Dictionary<string, Loot>();
        private static readonly List<Orphan> _orphans = new List<Orphan>();
        private static readonly Dictionary<Clan, long> _onceToday = new Dictionary<Clan, long>();
        private static Dictionary<string, MobileParty> _partyById;
        private static int _partyDay = -1;
        private static int _stumbles, _importN = -1, _importBad;
        private static readonly HashSet<string> _err = new HashSet<string>();

        // liczniki doby (linia "Dlugi (168)" i "Obieg")
        internal static long LastLootRepaid, LastInstalments, LastSeized, LastSold, LastMenPaid, LastCrownPaid, LastAdvNothing, LastReopened, LastProfit, LastRansomPaid;
        private static long _dLootRepaid, _dBankInst, _dClaimInst, _dArrearPaid, _dSeizedVill, _dSeizedPurse, _dSoldArm, _dSoldCar, _dSoldWs, _dMenPaid, _dMenSpare,
                            _dCrownOld, _dAdvNothing, _dRansomPaid, _dGreatPaid, _dSpare, _dTakenOld, _dTakenAdv, _dWageDebtNew, _dAdvNew, _dInherited, _dOrphanNew, _dProfit,
                            _dReopened, _dInterest;
        private static int _dMissed, _dNewArrears, _dNewSeize, _dSeizeEnded, _dSales, _dSoldCarN, _dSoldWsN, _dOver15, _dFromDefault, _dWageDebtParties, _dNoRecipient,
                           _dInheritN, _dOrphanN, _dReopenedN, _dPaidN;
        // z budzetu 166 (po Banku): dlaczego KW nie dostal miejsca
        private static int _dKwBlockArrear, _dKwBlockSeize, _dKwBlockD, _dKwBlockBank, _dKwAtLimit;

        internal static void Reset()
        {
            _l.Clear(); _loot.Clear(); _orphans.Clear(); _onceToday.Clear(); _partyById = null; _partyDay = -1;
            _stumbles = 0; _err.Clear(); _importN = -1; _importBad = 0;
            ZeroDay(); ZeroBudgetDay(); ZeroLast();
        }

        internal static void ZeroLast()
        {
            LastLootRepaid = LastInstalments = LastSeized = LastSold = LastMenPaid = LastCrownPaid = LastAdvNothing = LastReopened = LastProfit = LastRansomPaid = 0;
        }

        private static void ZeroDay()
        {
            _dLootRepaid = _dBankInst = _dClaimInst = _dArrearPaid = _dSeizedVill = _dSeizedPurse = _dSoldArm = _dSoldCar = _dSoldWs = _dMenPaid = _dMenSpare = 0;
            _dCrownOld = _dAdvNothing = _dRansomPaid = _dGreatPaid = _dSpare = _dInherited = _dOrphanNew = _dProfit = _dInterest = 0;
            _dMissed = _dNewArrears = _dNewSeize = _dSeizeEnded = _dSales = _dSoldCarN = _dSoldWsN = _dOver15 = _dFromDefault = _dNoRecipient = 0;
            _dInheritN = _dOrphanN = _dPaidN = 0;
        }

        // liczniki zbierane miedzy dobami (rozliczenia rodow gry i budzet) - zerowane po linii
        private static void ZeroBudgetDay()
        {
            _dKwBlockArrear = _dKwBlockSeize = _dKwBlockD = _dKwBlockBank = _dKwAtLimit = 0;
            _dTakenOld = _dTakenAdv = _dWageDebtNew = _dAdvNew = _dReopened = 0; _dWageDebtParties = _dReopenedN = 0;
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_err.Add(where)) Log.Error("DebtLadder." + where, e); } catch { }
        }

        // ------------------------------------------------------------ wylaczniki
        internal static bool On { get { var s = Settings.Current; return s != null && s.DebtLadderEnabled && s.IronBankEnabled && s.ClanBudgetEnabled; } }
        internal static bool PlayerOnLadder { get { var s = Settings.Current; return On && s.PlayerSameLadder; } }
        private static bool InLadder(Clan c) { var s = Settings.Current; return c != null && (c != Clan.PlayerClan || (s != null && s.PlayerSameLadder)); }
        internal static bool TakesCrownDebtFor(Clan c) { var s = Settings.Current; return On && s.WageDebtToMen && InLadder(c); }
        private static bool WalletsOn { get { var s = Settings.Current; return On && s.GameWalletsNoAdvance; } }

        private static L Of(Clan c, bool create) { return c != null ? OfId(c.StringId, create) : null; }

        private static L OfId(string id, bool create)
        {
            if (id == null) return null;
            L l;
            if (_l.TryGetValue(id, out l) || !create) return l;
            l = new L(); _l[id] = l;
            return l;
        }

        private static long ClaimsLeft(L l) { long t = 0; if (l != null) foreach (var c in l.Claims) t += Math.Max(0, c.Left); return t; }

        private static bool HasCreditClaims(L l)
        {
            if (l == null) return false;
            foreach (var c in l.Claims) if (c.Left > 0 && c.Kind != KRansom && c.Kind != KGreat) return true;
            return false;
        }

        // ------------------------------------------------------------ odczyty dla budzetu 166, Banku i linii
        /// <summary>Rod w zajeciu (D3/D4) - budzet liczy D bez dochodu wsi.</summary>
        internal static bool IsSeized(Clan c) { var l = Of(c, false); return l != null && l.Seized && InLadder(c); }

        /// <summary>Dluznik (166 "Dluznik"): dlug w Banku, dlug zoldu albo wobec korony, zaleglosc albo zajecie (sam okup - nie).</summary>
        internal static bool IsDebtor(Clan c)
        {
            if (c == null || !InLadder(c)) return false;
            var d = IronBank.DebtOf(c, false);
            if (d != null && d.Principal >= 1) return true;
            var l = Of(c, false);
            return l != null && (l.Seized || l.Missed > 0 || HasCreditClaims(l));
        }

        /// <summary>Szczebel rodu do linii i CSV: "-", "kredyt", "zaleglosc", "zajecie", "wyprzedaz".</summary>
        internal static string StageOf(Clan c)
        {
            var l = Of(c, false);
            var d = IronBank.DebtOf(c, false);
            bool any = (d != null && d.Principal >= 1) || ClaimsLeft(l) > 0;
            if (l != null && l.Seized) return SaleRegime(l, d) ? "wyprzedaz" : "zajecie";
            if (l != null && l.Missed > 0) return "zaleglosc";
            return any ? "kredyt" : "-";
        }

        private static bool SaleRegime(L l, IronBank.Debt d)
        {
            var s = Settings.Current;
            if (l == null || !l.Seized) return false;
            if (l.ZeroDays >= Math.Max(1, s.SaleAfterZeroSeizeDays)) return true;
            if (l.SeizeEma <= 0) return false;
            double owed = (d != null && d.Principal >= 1 ? d.Principal : 0) + ClaimsLeft(l);
            return owed / l.SeizeEma > Math.Max(1, s.SaleForecastDays);
        }

        /// <summary>D rodu do rat: D budzetu 166 (wczoraj), inaczej D staly, inaczej D169; nie mniej niz 0.</summary>
        internal static double DebtD(Clan c)
        {
            double d = ClanBudget.DLast(c);
            if (d < 0) d = ClanIncomeBook.StableD(c);
            if (d < 0) d = ClanIncomeBook.StableIncome(c);
            return Math.Max(0, d);
        }

        private static double LootAvg(Clan c) { Loot x; return c != null && c.StringId != null && _loot.TryGetValue(c.StringId, out x) ? Math.Max(0, x.Ema) : 0; }

        /// <summary>Limit Banku przy drabinie (IronBank.Limit): 15 x ziemia D stalego + 10 000 za miasto + 5 000 za zamek + 30 x srednie jednorazowe od prawdziwego
        /// platnika z 84 dob, x wiarygodnosc (bankrut 0).</summary>
        internal static int Limit(Clan c)
        {
            try
            {
                var s = Settings.Current;
                if (c == null) return 0;
                var d = IronBank.DebtOf(c, false);
                float trust = d != null ? (d.Defaulted ? 0f : d.Trust) : 1f;
                if (trust <= 0f) return 0;
                double land = ClanIncomeBook.StableLand(c);
                if (land < 0) land = Math.Max(0, ClanIncomeBook.StableIncome(c));   // bez D stalego - D169 (jak linia "Dlugi (na sucho)")
                int towns = 0, castles = 0;
                var fiefs = c.Fiefs;
                if (fiefs != null) for (int i = 0; i < fiefs.Count; i++) { var f = fiefs[i]; if (f == null) continue; if (f.IsCastle) castles++; else if (f.IsTown) towns++; }
                double lim = Math.Max(0f, s.DebtLimitLandDays) * land + towns * Math.Max(0, s.IronBankPerTown) + castles * Math.Max(0, s.IronBankPerCastle)
                             + Math.Max(0f, s.WarCreditLootDays) * LootAvg(c);
                return (int)Math.Min(int.MaxValue, lim * trust);
            }
            catch { return 0; }
        }

        private static bool AtWar(Clan c)
        {
            try
            {
                var f = c.MapFaction;
                if (f == null) return false;
                var list = f.FactionsAtWarWith;
                if (list != null) for (int i = 0; i < list.Count; i++) { var o = list[i]; if (o != null && o.IsKingdomFaction) return true; }
            }
            catch { }
            return false;
        }

        // ------------------------------------------------------------ kredyt wojenny (wola budzet 166, po Banku)
        /// <summary>Miejsce KW w pulapie rodu dzis (0 - rod nie moze brac kredytu wojennego). Liczniki powodow - linia "Dlugi (168)".</summary>
        internal static double WarCreditRoom(Clan c, double D, double G, double R, bool war, double household)
        {
            var s = Settings.Current;
            if (!On || s == null || !s.WarCredit || !war || c == null || c == Clan.PlayerClan) return 0;
            if (G >= R || D <= 0) return 0;
            int today = (int)CampaignTime.Now.ToDays;
            var l = Of(c, false);
            if (l != null && (l.Seized || l.Missed > 0)) { _dKwBlockArrear++; return 0; }
            if (l != null && l.FreeDay >= 0 && today - l.FreeDay < Math.Max(0, s.CreditAfterSeizureDays)) { _dKwBlockSeize++; return 0; }
            var d = IronBank.DebtOf(c, false);
            if (d != null && (d.Defaulted || d.Trust <= 0f)) { _dKwBlockArrear++; return 0; }
            // rod, ktorego D nie pokrywa kosztow stalych (dwor + powinnosci 3% w wojnie) - bez kredytu
            if (D <= Math.Max(0, household) + 0.03 * D) { _dKwBlockD++; return 0; }
            double free = IronBank.FreeCapital - Math.Max(0, s.BankFreeCapitalFloor);
            if (free <= 0) { _dKwBlockBank++; return 0; }
            double room = Limit(c) - (d != null ? d.Principal : 0);
            if (room < 1) { _dKwAtLimit++; return 0; }
            return Math.Max(0, Math.Min(Math.Min(Math.Max(0f, Math.Min(1f, s.WarCreditMaxShareD)) * D, room), free));
        }

        /// <summary>Wyplata KW dzis: kapital Banku -> kiesa glowy (IronBank.LendKw); czesc KW w ksiedze rodu (splata z lupow najpierw na KW).</summary>
        internal static int LendWar(Clan c, int amount)
        {
            int got = IronBank.LendKw(c, amount);
            if (got > 0) { var l = Of(c, true); l.Kw += got; }
            return got;
        }

        /// <summary>Pozyczka reczna gracza w Braavos (drabina): w wojnie - kredyt wojenny.</summary>
        internal static void NotePlayerLoan(Clan c, int got)
        {
            try { if (got > 0 && AtWar(c)) { var l = Of(c, true); l.Kw += got; } } catch (Exception e) { Stumble("NotePlayerLoan", e); }
        }

        /// <summary>Splata reczna w Braavos: najpierw gasi czesc KW.</summary>
        internal static void OnBankRepaid(Clan c, int pay)
        {
            try { var l = Of(c, false); if (l != null && pay > 0) l.Kw = Math.Max(0, l.Kw - pay); } catch { }
        }

        /// <summary>Gracz w Braavos: czy Bank pozyczy (drabina); why - powod po angielsku.</summary>
        internal static bool CanBorrow(Clan c, out string why)
        {
            why = "";
            var s = Settings.Current;
            var l = Of(c, false);
            var d = IronBank.DebtOf(c, false);
            int today = (int)CampaignTime.Now.ToDays;
            if (l != null && l.Seized) { why = "Your creditors hold the income of your villages until your debts are paid. The Bank will not lend to you now. "; return false; }
            if (l != null && l.Missed > 0) { why = "You are behind on your instalments. The Bank lends nothing more until you have paid what is due. "; return false; }
            if (l != null && l.FreeDay >= 0 && today - l.FreeDay < Math.Max(0, s.CreditAfterSeizureDays))
            { why = "Your lands were in the hands of your creditors not long ago. The Bank will lend to you again after day " + (l.FreeDay + s.CreditAfterSeizureDays) + ". "; return false; }
            if (d != null && d.Defaulted) { why = "Your name is written in the book of those who did not pay. "; return false; }
            if (IronBank.FreeCapital < Math.Max(0, s.BankFreeCapitalFloor)) { why = "The Bank's vaults run low and it lends to no one now. "; return false; }
            return true;
        }

        /// <summary>Opis dlugow gracza w menu Banku (po angielsku).</summary>
        internal static string PlayerStatus()
        {
            var s = Settings.Current;
            var c = Clan.PlayerClan;
            var d = IronBank.DebtOf(c, false);
            var l = Of(c, false);
            double D = DebtD(c);
            var sb = new StringBuilder("\n\n");
            sb.Append("Your steady income is about " + Math.Round(D).ToString("0", Inv) + " a day. The Bank takes at most " + (Math.Max(0f, s.IronBankMaxInstalmentShare) * 100).ToString("0", Inv)
                      + "% of it a day, all your creditors together at most " + (Math.Max(0f, s.AllInstalmentsMaxShare) * 100).ToString("0", Inv) + "%.");
            if (d != null && d.Principal >= 1)
                sb.Append(" You owe the Bank " + (long)d.Principal + " gold at " + (d.Rate * 100).ToString("0", Inv) + "% a year" + (l != null && l.Kw > 0 ? " (" + Math.Min(l.Kw, (long)d.Principal) + " of it war credit)" : "") + ".");
            long other = ClaimsLeft(l);
            if (other > 0) sb.Append(" Other debts: " + other + " gold.");
            if (l != null && l.Seized) sb.Append(" Your creditors take the income of your villages and every coin above " + s.SeizeFloorGold + " in your purse until all is paid.");
            else if (l != null && l.Missed > 0) sb.Append(" Missed instalments in a row: " + l.Missed + "/3 - at the third your creditors seize your villages' income.");
            return sb.ToString();
        }

        // ------------------------------------------------------------ dlug wobec korony z gry -> ksiega (SoldierPay)
        private static void AddClaim(L l, int kind, string creditor, string crown, long amount, int day)
        {
            if (l == null || amount <= 0) return;
            creditor = creditor ?? ""; crown = crown ?? "";
            if (kind == KMen || kind == KCrownAdv || kind == KCrownOld)
                foreach (var c in l.Claims)
                    if (c.Kind == kind && c.Creditor == creditor && c.Left > 0) { c.Left += amount; c.Total += amount; return; }
            l.Claims.Add(new Claim { Kind = kind, Creditor = creditor, Crown = crown, Left = amount, Total = amount, Day = day });
        }

        /// <summary>178: nowy dlug okupu (albo okupu wielkiego jenca) w ksiedze rodu-dluznika - kolejka za starszymi (bez blokowania uwolnienia).</summary>
        internal static void AddRansomDebt(Clan debtor, bool great, string creditor, string thirdsCrown, long amount)
        {
            try { if (debtor != null && amount > 0) AddClaim(Of(debtor, true), great ? KGreat : KRansom, creditor, thirdsCrown, amount, (int)CampaignTime.Now.ToDays); }
            catch (Exception e) { Stumble("AddRansomDebt", e); }
        }

        /// <summary>178: suma dlugow okupow rodu (pulap 364 D).</summary>
        internal static long RansomDebtOf(Clan c)
        {
            var l = Of(c, false); long t = 0;
            if (l != null) foreach (var x in l.Claims) if ((x.Kind == KRansom || x.Kind == KGreat) && x.Left > 0) t += x.Left;
            return t;
        }

        /// <summary>SoldierPay.ClanTickPrefix: caly DebtToKingdom rodu do ksiegi (zaliczka gry z CrownIncome - splata w nicosc; reszta - do skarbca krolestwa).</summary>
        internal static void TakeOverCrownDebt(Clan c)
        {
            if (!TakesCrownDebtFor(c)) return;
            int debt = c.DebtToKingdom;
            if (debt <= 0) return;
            long adv = CrownIncome.TakeAdvanceAll(c, debt);
            var l = Of(c, true);
            int today = (int)CampaignTime.Now.ToDays;
            if (adv > 0) AddClaim(l, KCrownAdv, "", "", adv, today);
            if (debt - adv > 0) AddClaim(l, KCrownOld, c.Kingdom != null ? c.Kingdom.StringId : "", "", debt - adv, today);
            c.DebtToKingdom = 0;
            _dTakenOld += debt - adv; _dTakenAdv += adv;
        }

        /// <summary>SoldierPay.Settle: nowy DebtToKingdom z tego rozliczenia - czesc rownowazona obcietym zoldem to dlug wobec ludzi partii (wedlug obciec),
        /// reszta to zaliczka gry (przy GameWalletsNoAdvance - zwykle 0). DebtToKingdom rodu = 0.</summary>
        internal static void TakeTickDebt(Clan c, List<MobileParty> who, List<long> cuts, long cutByDebt)
        {
            if (!TakesCrownDebtFor(c)) return;
            long cur = Math.Max(0, c.DebtToKingdom);
            if (cur <= 0) return;
            var l = Of(c, true);
            int today = (int)CampaignTime.Now.ToDays;
            long wage = Math.Min(Math.Max(0, cutByDebt), cur);
            long tot = 0;
            if (who != null && cuts != null) for (int i = 0; i < cuts.Count; i++) tot += Math.Max(0, cuts[i]);
            if (wage > 0 && tot > 0)
            {
                long given = 0; int last = -1;
                for (int i = 0; i < who.Count; i++) if (cuts[i] > 0 && who[i] != null && who[i].StringId != null) last = i;
                for (int i = 0; i <= last; i++)
                {
                    if (cuts[i] <= 0 || who[i] == null || who[i].StringId == null) continue;
                    long x = i == last ? wage - given : wage * cuts[i] / tot;
                    if (x <= 0) continue;
                    AddClaim(l, KMen, who[i].StringId, "", x, today);
                    given += x; _dWageDebtParties++;
                }
                wage = given;
            }
            else wage = 0;
            _dWageDebtNew += wage;
            long adv = cur - wage;
            if (adv > 0) { AddClaim(l, KCrownAdv, "", "", adv, today); _dAdvNew += adv; }
            c.DebtToKingdom = 0;
        }

        // ------------------------------------------------------------ raz na dobe (krok Banku: po SoldierPay, przed budzetem 166)
        internal static void Daily(int today, ref int paidN, ref long paidSum, ref int missedN)
        {
            ZeroDay();
            _onceToday.Clear();
            if (!On || Campaign.Current == null) { ZeroLast(); return; }
            var s = Settings.Current;
            var byId = new Dictionary<string, Clan>();
            foreach (var c in Clan.All) if (c != null && c.StringId != null) byId[c.StringId] = c;
            string player = Clan.PlayerClan != null ? Clan.PlayerClan.StringId : null;
            // 0. bankruci ze starego zapisu - zajecie zamiast bankructwa (raz)
            foreach (var id in IronBank.DebtIds())
            {
                try
                {
                    var d = IronBank.DebtById(id);
                    if (d == null || !d.Defaulted || d.Principal < 1 || (id == player && !s.PlayerSameLadder)) continue;
                    d.Defaulted = false;
                    var l = OfId(id, true);
                    if (!l.Seized) { l.Seized = true; l.SeizeDay = today; l.SaleDay = today; l.ZeroDays = 0; _dFromDefault++; }
                }
                catch (Exception e) { Stumble("Daily(bankrut)", e); }
            }
            // 1. jednorazowe doby: srednia 84 dob od prawdziwego platnika (limit) i wszystkie (splata KW) - kazdy rod z wpisem ksiegi
            foreach (var c in byId.Values)
            {
                try
                {
                    if (c.IsEliminated || c.IsBanditFaction) continue;
                    long all, real;
                    if (!ClanIncomeBook.OnceToday(c, out all, out real)) continue;
                    _onceToday[c] = all;
                    Loot x;
                    if (!_loot.TryGetValue(c.StringId, out x)) { x = new Loot(); _loot[c.StringId] = x; }
                    x.N = Math.Min(84, x.N + 1);
                    x.Ema += (real - x.Ema) / x.N;   // pierwsze 84 doby - srednia zwykla, potem srednia kroczaca 84 dob
                }
                catch (Exception e) { Stumble("Daily(lup)", e); }
            }
            // 2. dluznicy: zywi wedlug drabiny, wymarli - dlug na nowych panow wsi albo "bez platnika"
            var ids = new HashSet<string>();
            foreach (var id in IronBank.DebtIds()) { var d = IronBank.DebtById(id); if (d != null && d.Principal >= 1) ids.Add(id); }
            foreach (var kv in _l) if (kv.Value.Seized || ClaimsLeft(kv.Value) > 0 || kv.Value.Missed > 0) ids.Add(kv.Key);
            foreach (var id in ids)
            {
                try
                {
                    if (id == player && !s.PlayerSameLadder) continue;
                    Clan c;
                    if (!byId.TryGetValue(id, out c) || c.IsEliminated) { Inherit(id, c, today); continue; }
                    if (c.Leader == null || !c.Leader.IsAlive) continue;   // glowa zmarla, gra jeszcze nie wyznaczyla nastepcy - jutro (rod zyje, dlug zostaje)
                    DayClan(c, today, s, ref paidN, ref paidSum, ref missedN);
                }
                catch (Exception e) { Stumble("Daily(rod)", e); }
            }
            // 3. zysk Banku ponad kapital startowy -> kasa Braavos (1/180 dziennie, pod tarcza dworu - regulator gry jej nie skasuje)
            try
            {
                double cap = IronBank.FreeCapital, base0 = Math.Max(0, s.IronBankCapital);
                if (cap > base0)
                {
                    long x = (long)((cap - base0) / Math.Max(1f, s.BankProfitToBraavosDays));
                    var br = Settlement.Find("town_EN5");
                    if (x > 0 && br != null && br.Town != null)
                    {
                        int ix = (int)Math.Min(int.MaxValue, x);
                        IronBank.CapitalAdd(-ix);
                        br.Town.ChangeGold(ix);
                        SoldierPay.HoldCourt(br, ix);
                        _dProfit += ix;
                    }
                }
            }
            catch (Exception e) { Stumble("Daily(zysk)", e); }
            // 4. porzadki: puste wpisy (bez dlugu, zaleglosci i zajecia; karencja KW po zajeciu minela)
            var drop = new List<string>();
            foreach (var kv in _l)
            {
                var l = kv.Value;
                l.Claims.RemoveAll(x => x.Left <= 0);
                if (l.Seized || l.Missed > 0 || l.Claims.Count > 0) continue;
                var d = IronBank.DebtById(kv.Key);
                if (d != null && d.Principal >= 1) continue;
                l.Kw = 0;
                if (l.FreeDay >= 0 && today - l.FreeDay < Math.Max(0, s.CreditAfterSeizureDays)) continue;
                drop.Add(kv.Key);
            }
            foreach (var k in drop) _l.Remove(k);
            LastLootRepaid = _dLootRepaid; LastInstalments = _dBankInst + _dClaimInst + _dArrearPaid; LastSeized = _dSeizedVill + _dSeizedPurse;
            LastSold = _dSoldArm + _dSoldCar + _dSoldWs; LastMenPaid = _dMenPaid + _dMenSpare; LastCrownPaid = _dCrownOld; LastAdvNothing = _dAdvNothing; LastProfit = _dProfit;
            LastRansomPaid = _dRansomPaid + _dGreatPaid;
        }

        private static void DayClan(Clan c, int today, Settings s, ref int paidN, ref long paidSum, ref int missedN)
        {
            var d = IronBank.DebtOf(c, false);
            bool bank = d != null && d.Principal >= 1;
            var l = Of(c, false);
            if (!bank && (l == null || (ClaimsLeft(l) <= 0 && !l.Seized))) { if (l != null) l.Missed = 0; return; }
            if (l == null) l = Of(c, true);
            // wsie rodu (na wypadek wymarcia - dlug przechodzi na nowych panow tych wsi)
            l.Villages.Clear();
            var sts = c.Settlements;
            if (sts != null) for (int i = 0; i < sts.Count; i++) { var st = sts[i]; if (st != null && st.IsVillage && st.StringId != null) l.Villages.Add(st.StringId); }
            var head = c.Leader;
            bool player = c == Clan.PlayerClan;
            if (l.Seized) { Seize(c, l, d, head, today, s, ref paidN, ref paidSum); return; }
            // D1/D2: odsetki Banku
            if (bank)
            {
                double i = d.Principal * d.Rate / Math.Max(28, IronBank.DaysPerYearNow());
                d.Principal += i; _dInterest += (long)i;
                l.Kw = Math.Min(l.Kw, (long)d.Principal);
            }
            bool war = AtWar(c);
            // splata z jednorazowych (AI): w wojnie 50%, po pokoju 100% - najpierw na KW, poza limitem rat z D
            if (bank && !player)
            {
                long once; _onceToday.TryGetValue(c, out once);
                double share = war ? Math.Max(0f, Math.Min(1f, s.WarCreditLootRepayShare)) : 1.0;
                long want = (long)Math.Min(d.Principal, once * share);
                if (want > 0)
                {
                    long wage3 = 3L * Math.Max(0, ClanIncomeBook.WageOf(c));
                    long got = Collect(c, want, Math.Max((long)Math.Max(0, s.FamilyPurseFloor), wage3), Math.Max(0, s.FamilyPurseFloor));
                    if (got > 0) { PayBank(c, l, d, got); _dLootRepaid += got; paidSum += got; paidN++; _dPaidN++; }
                }
                bank = d.Principal >= 1;
            }
            // raty z D: Bank najwyzej 10% D (w wojnie AI bez raty z D za KW - splaca lup), wszystkie razem najwyzej 15% D
            double D = DebtD(c);
            double cap15 = Math.Max(0f, Math.Min(1f, s.AllInstalmentsMaxShare)) * D, capBank = Math.Max(0f, Math.Min(1f, s.IronBankMaxInstalmentShare)) * D;
            long bankDue = 0;
            if (bank)
            {
                long kw = Math.Min(l.Kw, (long)d.Principal), rest = Math.Max(0, (long)d.Principal - kw);
                int daysLeft = Math.Max(1, d.DueDay - today);
                long dueRest = rest > 0 ? (long)Math.Ceiling((double)rest / daysLeft) : 0;
                long dueKw = war && !player ? 0 : kw;
                bankDue = (long)Math.Max(0, Math.Min(Math.Min((double)(dueRest + dueKw), Math.Min(capBank, cap15)), d.Principal));
            }
            long room = (long)Math.Max(0, cap15 - bankDue);
            var dues = new List<KeyValuePair<Claim, long>>();
            long claimsDue = 0;
            foreach (var cl in Ordered(l))
            {
                if (room <= 0) break;
                long x = Math.Min(cl.Left, room);
                if (x <= 0) continue;
                dues.Add(new KeyValuePair<Claim, long>(cl, x)); room -= x; claimsDue += x;
            }
            long total = bankDue + claimsDue;
            if (total <= 0) return;   // nic do zaplaty z D (D bliskie 0) - bez zaleglosci
            if (total > cap15 + 1) _dOver15++;
            if (!player && head.Gold < total) { try { IronBank.FamilyCoverAll(c, (int)Math.Min(int.MaxValue, total - head.Gold)); } catch (Exception e) { Stumble("FamilyCover", e); } }
            if (head.Gold >= total)
            {
                head.ChangeHeroGold(-(int)total);
                CirculationWindows.NoteHeroGold(head, -total);
                if (bankDue > 0) { PayBank(c, l, d, bankDue); _dBankInst += bankDue; }
                foreach (var kv in dues) { Deliver(c, kv.Key, kv.Value); _dClaimInst += kv.Value; }
                paidN++; paidSum += total; _dPaidN++;
                l.Missed = 0; if (d != null) d.Missed = 0;
                return;
            }
            // D2 zaleglosc: placi, ile ma ponad 3 dni zoldu (Bank pierwszy), wiarygodnosc x0.8, +2 pp, KW wstrzymany
            long reserve3 = 3L * Math.Max(0, ClanIncomeBook.WageOf(c));
            long can = Math.Min(total, Math.Max(0, (long)head.Gold - reserve3));
            if (can > 0)
            {
                head.ChangeHeroGold(-(int)can);
                CirculationWindows.NoteHeroGold(head, -can);
                long left = can;
                if (bankDue > 0) { long x = Math.Min(left, bankDue); PayBank(c, l, d, x); left -= x; }
                foreach (var kv in dues) { if (left <= 0) break; long x = Math.Min(left, kv.Value); Deliver(c, kv.Key, x); left -= x; }
                if (left > 0) { head.ChangeHeroGold((int)left); CirculationWindows.NoteHeroGold(head, left); can -= left; }
                _dArrearPaid += can; paidSum += can;
            }
            l.Missed++; missedN++; _dMissed++;
            if (l.Missed == 1) _dNewArrears++;
            if (d != null && d.Principal >= 1) { d.Missed = l.Missed; d.Trust = Math.Max(0.5f, d.Trust * 0.8f); d.Rate += 0.02; }
            if (l.Missed >= 3)
            {
                l.Seized = true; l.SeizeDay = today; l.SaleDay = today; l.ZeroDays = 0; l.SeizeEma = 0;
                _dNewSeize++;
                long vill = ClanIncomeBook.VillageIncomeOf(c);
                Log.Info("Dlugi (168): " + c.Name + " - ZAJECIE dochodu (D3) po 3 zaleglosciach z rzedu; dlug w Banku " + (d != null ? (long)d.Principal : 0) + ", inne " + ClaimsLeft(l)
                         + "; wierzyciele biora dochod wsi (ok. " + vill + " zl/dobe) i kiese glowy ponad " + s.SeizeFloorGold + "; odsetki zamrozone; lenna zostaja.");
                if (player) Log.Player("Three instalments missed: your creditors now take the income of your villages and every coin above " + s.SeizeFloorGold
                                       + " in your purse until your debts are paid. Your fiefs stay yours.", true);
            }
            else if (player) Log.Player("You missed an instalment of " + total + " gold (" + l.Missed + "/3). At the third your creditors seize your villages' income.", true);
        }

        /// <summary>Roszczenia w kolejnosci splaty: dlug zoldu, dlug wobec korony, zaliczka gry, okupy wedlug wieku.</summary>
        private static IEnumerable<Claim> Ordered(L l)
        {
            return l.Claims.Where(x => x.Left > 0).OrderBy(x => x.Kind == KMen ? 0 : x.Kind == KCrownOld ? 1 : x.Kind == KCrownAdv ? 2 : 3).ThenBy(x => x.Day);
        }

        // ------------------------------------------------------------ zajecie (D3) i wyprzedaz (D4)
        private static void Seize(Clan c, L l, IronBank.Debt d, Hero head, int today, Settings s, ref int paidN, ref long paidSum)
        {
            long owed = (d != null && d.Principal >= 1 ? (long)d.Principal : 0) + ClaimsLeft(l);
            if (owed <= 0) { EndSeizure(c, l, d, today); return; }
            long vill = ClanIncomeBook.VillageIncomeOf(c);
            long fromVill = Math.Min(Math.Max(0, head.Gold), vill);
            long purse = Math.Max(0, (long)head.Gold - fromVill - Math.Max(0, s.SeizeFloorGold));
            long take = Math.Min(owed, fromVill + purse);
            if (take > 0)
            {
                head.ChangeHeroGold(-(int)Math.Min(int.MaxValue, take));
                CirculationWindows.NoteHeroGold(head, -take);
                long left = Distribute(c, l, d, take);
                if (left > 0) { head.ChangeHeroGold((int)left); CirculationWindows.NoteHeroGold(head, left); take -= left; }
                long v = Math.Min(fromVill, take);
                _dSeizedVill += v; _dSeizedPurse += take - v; paidN++; paidSum += take; _dPaidN++;
            }
            l.SeizeEma = l.SeizeEma <= 0 ? take : l.SeizeEma + (take - l.SeizeEma) / 28.0;
            l.ZeroDays = take > 0 ? 0 : l.ZeroDays + 1;
            owed = (d != null && d.Principal >= 1 ? (long)d.Principal : 0) + ClaimsLeft(l);
            if (owed <= 0) { EndSeizure(c, l, d, today); return; }
            // D4: co 7 dni, tylko przy 14 dobach zerowego zajecia albo prognozie > 728 dni
            if (today - l.SaleDay >= 7 && SaleRegime(l, d))
            {
                l.SaleDay = today;
                long got = 0;
                try { got = Sale(c, l, d, head, owed); } catch (Exception e) { Stumble("Sale", e); }
                _dSales++;
                owed = (d != null && d.Principal >= 1 ? (long)d.Principal : 0) + ClaimsLeft(l);
                if (got > 0) { paidSum += got; }
                if (owed <= 0) EndSeizure(c, l, d, today);
            }
        }

        private static void EndSeizure(Clan c, L l, IronBank.Debt d, int today)
        {
            l.Seized = false; l.FreeDay = today; l.Missed = 0; l.ZeroDays = 0; l.SeizeEma = 0; l.Kw = 0;
            if (d != null) { d.Missed = 0; if (d.Principal < 1) d.Principal = 0; }
            _dSeizeEnded++;
            Log.Info("Dlugi (168): " + c.Name + " - zajecie zakonczone, dlugi splacone; kredyt wojenny dopiero po " + Settings.Current.CreditAfterSeizureDays + " dobach.");
            if (c == Clan.PlayerClan) Log.Player("Your debts are paid. The income of your villages is yours again.");
        }

        /// <summary>D4: nadwyzki zbrojowni partii AI w miescie na targ (zloto przez kiese glowy), karawany bez bohatera i warsztaty do najbogatszego notabla
        /// (placi z kiesy, ile ma) - do kwoty dlugu; przychod do wierzycieli. Lenno zostaje; nigdy ekwipunek bohaterow.</summary>
        private static long Sale(Clan c, L l, IronBank.Debt d, Hero head, long owed)
        {
            long got = 0, arm = 0, car = 0, ws = 0; int nCar = 0, nWs = 0;
            bool player = c == Clan.PlayerClan;
            // 1. nadwyzki zbrojowni (AI) - partie lorda rodu w miescie
            if (!player && c.WarPartyComponents != null)
                foreach (var wpc in c.WarPartyComponents.ToList())
                {
                    if (got >= owed) break;
                    var mp = wpc != null ? wpc.MobileParty : null;
                    if (mp == null || !mp.IsActive || !mp.IsLordParty || mp.MapEvent != null || mp.CurrentSettlement == null || !mp.CurrentSettlement.IsTown) continue;
                    int g0 = head.Gold, gold;
                    try { GarrisonArmory.SellSurplus(mp, GarrisonArmory.NeedByType(mp.MemberRoster), mp.CurrentSettlement, head, 0f, "D4", out gold); }
                    catch (Exception e) { Stumble("Sale(zbrojownia)", e); gold = 0; }
                    long inc = Math.Max(0, head.Gold - g0);
                    long take = Math.Min(inc, owed - got);
                    if (take > 0) { head.ChangeHeroGold(-(int)take); CirculationWindows.NoteHeroGold(head, -take); arm += take; got += take; }
                }
            // 2. karawany (bez bohatera na czele) - najbogatszemu notablowi miasta karawany
            foreach (var h in c.Heroes.ToList())
            {
                if (got >= owed || h == null || !h.IsAlive) continue;
                foreach (var cp in h.OwnedCaravans.ToList())
                {
                    if (got >= owed) break;
                    try
                    {
                        var mp = cp != null ? cp.MobileParty : null;
                        if (mp == null || !mp.IsActive || mp.LeaderHero != null || mp.MapEvent != null || mp.CaravanPartyComponent == null) continue;
                        var home = mp.HomeSettlement != null && mp.HomeSettlement.IsTown ? mp.HomeSettlement : NearestTown(mp.GetPosition2D);
                        var buyer = RichestNotable(home, h);
                        if (buyer == null) continue;
                        long value = (long)mp.PartyTradeGold + GoodsValue(mp.ItemRoster);
                        if (buyer.Gold < mp.PartyTradeGold) continue;   // notabl nie ma nawet na gotowke karawany - bez sprzedazy z niczego
                        long price = Math.Min(value, (long)buyer.Gold);
                        buyer.ChangeHeroGold(-(int)price);
                        CirculationWindows.NoteHeroGold(buyer, -price);
                        CaravanPartyComponent.TransferCaravanOwnership(mp, buyer, buyer.CurrentSettlement != null && buyer.CurrentSettlement.IsTown ? buyer.CurrentSettlement : home);
                        car += price; got += price; nCar++;
                    }
                    catch (Exception e) { Stumble("Sale(karawana)", e); }
                }
            }
            // 3. warsztaty - najbogatszemu notablowi miasta warsztatu (cena gry dla notabla, najwyzej jego kiesa)
            foreach (var h in c.Heroes.ToList())
            {
                if (got >= owed || h == null || !h.IsAlive) continue;
                foreach (var w in h.OwnedWorkshops.ToList())
                {
                    if (got >= owed) break;
                    try
                    {
                        if (w == null || w.Settlement == null) continue;
                        var buyer = RichestNotable(w.Settlement, h);
                        if (buyer == null) continue;
                        long price = Math.Min((long)Campaign.Current.Models.WorkshopModel.GetCostForNotable(w), (long)buyer.Gold);
                        if (price <= 0) continue;
                        buyer.ChangeHeroGold(-(int)price);
                        CirculationWindows.NoteHeroGold(buyer, -price);
                        ChangeOwnerOfWorkshopAction.ApplyByDeath(w, buyer);
                        ws += price; got += price; nWs++;
                    }
                    catch (Exception e) { Stumble("Sale(warsztat)", e); }
                }
            }
            if (got > 0)
            {
                long left = Distribute(c, l, d, got);
                if (left > 0) { head.ChangeHeroGold((int)left); CirculationWindows.NoteHeroGold(head, left); }   // nadwyzka ponad dlug wraca do rodu
            }
            _dSoldArm += arm; _dSoldCar += car; _dSoldWs += ws; _dSoldCarN += nCar; _dSoldWsN += nWs;
            Log.Info("Dlugi (168): " + c.Name + " - WYPRZEDAZ (D4): nadwyzki zbrojowni " + arm + " zl, karawany " + nCar + " za " + car + " zl, warsztaty " + nWs + " za " + ws
                     + " zl -> wierzyciele; zajecie dalo 0 przez " + l.ZeroDays + " dob, srednio " + Math.Round(l.SeizeEma) + " zl/dobe; dlug " + owed + "; lenna zostaja.");
            if (player && got > 0) Log.Player("Your creditors sold your caravans and workshops for " + got + " gold to settle your debts. Your fiefs and your own gear stay yours.", true);
            return got;
        }

        private static long GoodsValue(TaleWorlds.CampaignSystem.Roster.ItemRoster r)
        {
            long v = 0;
            if (r == null) return 0;
            for (int i = 0; i < r.Count; i++) { var el = r.GetElementCopyAtIndex(i); if (el.EquipmentElement.Item != null && el.Amount > 0) v += (long)el.EquipmentElement.ItemValue * el.Amount; }
            return v;
        }

        private static Hero RichestNotable(Settlement st, Hero not)
        {
            if (st == null) return null;
            Hero best = null;
            foreach (var n in st.Notables) if (n != null && n.IsAlive && n != not && n != Hero.MainHero && n.Gold > 0 && (best == null || n.Gold > best.Gold)) best = n;
            return best;
        }

        private static Settlement NearestTown(Vec2 pos)
        {
            Settlement best = null; float bd = float.MaxValue;
            foreach (var t in Settlement.All)
            {
                if (t == null || !t.IsTown || t.Town == null) continue;
                float dd = pos.IsValid ? pos.DistanceSquared(t.GetPosition2D) : 0f;
                if (dd < bd) { bd = dd; best = t; }
            }
            return best;
        }

        private static Vec2 PosOf(Clan c)
        {
            try
            {
                var h = c != null ? c.Leader : null;
                if (h != null && h.PartyBelongedTo != null) return h.PartyBelongedTo.GetPosition2D;
                if (h != null && h.CurrentSettlement != null) return h.CurrentSettlement.GetPosition2D;
                var home = c != null ? c.HomeSettlement : null;
                if (home != null) return home.GetPosition2D;
            }
            catch { }
            return Vec2.Invalid;
        }

        // ------------------------------------------------------------ wierzyciele
        /// <summary>Zloto zdjete juz z dluznika: Bank pierwszy, potem roszczenia w kolejnosci. Zwraca reszte (ponad dlug).</summary>
        private static long Distribute(Clan c, L l, IronBank.Debt d, long amount)
        {
            if (amount <= 0) return 0;
            if (d != null && d.Principal >= 1)
            {
                long x = Math.Min(amount, (long)Math.Ceiling(d.Principal));
                PayBank(c, l, d, x); amount -= x;
            }
            foreach (var cl in Ordered(l).ToList())
            {
                if (amount <= 0) break;
                long x = Math.Min(amount, cl.Left);
                Deliver(c, cl, x); amount -= x;
            }
            return amount;
        }

        private static void PayBank(Clan c, L l, IronBank.Debt d, long x)
        {
            if (d == null || x <= 0) return;
            d.Principal -= x;
            IronBank.CapitalAdd(x);
            if (l != null) l.Kw = Math.Max(0, l.Kw - x);   // splata najpierw gasi kredyt wojenny
            if (d.Principal < 1)
            {
                d.Principal = 0; d.Missed = 0; d.Trust = Math.Min(1f, d.Trust + 0.25f);
                if (l != null) l.Kw = 0;
                IronBank.NoteLadderLine("IronBank: " + c.Name + " splacil dlug w Banku w calosci; wiarygodnosc " + d.Trust.ToString("0.00", Inv) + ".");
            }
        }

        private static MobileParty PartyById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            int today = (int)CampaignTime.Now.ToDays;
            if (_partyById == null || _partyDay != today)
            {
                _partyById = new Dictionary<string, MobileParty>();
                foreach (var mp in MobileParty.All) if (mp != null && mp.StringId != null) _partyById[mp.StringId] = mp;
                _partyDay = today;
            }
            MobileParty p; return _partyById.TryGetValue(id, out p) ? p : null;
        }

        private static Kingdom KingdomById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var k in Kingdom.All) if (k != null && k.StringId == id) return k;
            return null;
        }

        /// <summary>Rata albo zajete zloto (juz zdjete z dluznika) do wierzyciela roszczenia; zapasowy odbiorca 2.0b; nigdy w nicosc poza zaliczka gry (OBIEG-1).</summary>
        private static void Deliver(Clan debtor, Claim cl, long x)
        {
            if (x <= 0) return;
            cl.Left -= x;
            int ix = (int)Math.Min(int.MaxValue, x);
            switch (cl.Kind)
            {
                case KMen:
                {
                    var mp = PartyById(cl.Creditor);
                    if (mp != null && mp.IsActive)
                    {
                        if (mp.IsGarrison)
                        {
                            var st = mp.CurrentSettlement ?? mp.HomeSettlement;
                            if (st != null && st.Town != null) { st.Town.ChangeGold(ix); _dMenPaid += x; return; }
                        }
                        else if (mp.IsLordParty) { MenPurse.Add(mp, ix); _dMenPaid += x; return; }
                    }
                    // zapasowy odbiorca: inna partia lorda rodu (ludzie rodu), potem kasa najblizszego miasta (tam wydaliby zold)
                    if (debtor != null && debtor.WarPartyComponents != null)
                        foreach (var w in debtor.WarPartyComponents)
                        {
                            var p = w != null ? w.MobileParty : null;
                            if (p != null && p.IsActive && p.IsLordParty) { MenPurse.Add(p, ix); _dMenSpare += x; return; }
                        }
                    SpareTown(debtor, ix);
                    _dMenSpare += x;
                    return;
                }
                case KCrownAdv:
                    _dAdvNothing += x;   // recenzja C1 (OBIEG-1): zaliczka gry - portfel uznany z niczego; jej splata wraca w nicosc (rownowazy)
                    return;
                case KCrownOld:
                {
                    var k = KingdomById(cl.Creditor) ?? (debtor != null ? debtor.Kingdom : null);
                    if (k != null && !k.IsEliminated) { k.KingdomBudgetWallet += ix; _dCrownOld += x; return; }
                    SpareTown(debtor, ix); _dSpare += x;
                    return;
                }
                case KRansom:
                {
                    Hero h = null;
                    try { h = string.IsNullOrEmpty(cl.Creditor) ? null : Hero.Find(cl.Creditor); } catch { h = null; }
                    if (h == null || !h.IsAlive)
                    {
                        var lead = h != null && h.Clan != null ? h.Clan.Leader : null;   // zapasowy odbiorca: glowa rodu porywacza
                        h = lead != null && lead.IsAlive ? lead : null;
                    }
                    if (h != null)
                    {
                        h.ChangeHeroGold(ix);
                        CirculationWindows.NoteHeroGold(h, x);
                        if (h != Hero.MainHero) ClanIncomeBook.NoteInflow(h, ix, ClanIncomeBook.KRansom);
                        Ransom178.NoteRansomIn(h, x);   // 178: korona porywacza 1/9 z kazdej raty (rozliczenie raz na dobe)
                        _dRansomPaid += x;
                        return;
                    }
                    SpareTown(debtor, ix); _dSpare += x;
                    return;
                }
                case KGreat:
                {
                    var k = KingdomById(cl.Creditor);
                    if (k != null && !k.IsEliminated) { k.KingdomBudgetWallet += ix; _dGreatPaid += x; return; }
                    SpareTown(debtor, ix); _dSpare += x;
                    return;
                }
            }
            SpareTown(debtor, ix); _dSpare += x;
        }

        private static void SpareTown(Clan debtor, int x)
        {
            var t = NearestTown(PosOf(debtor));
            if (t != null && t.Town != null) { t.Town.ChangeGold(x); SoldierPay.HoldCourt(t, x); return; }
            // bez zadnego miasta na mapie - zloto wraca dluznikowi (nie w nicosc), licznik "bez odbiorcy" (prog 0)
            if (debtor != null && debtor.Leader != null) debtor.Leader.ChangeHeroGold(x);
            _dNoRecipient++;
        }

        /// <summary>Od rodziny: glowa ponad floorHead, potem dorosli czlonkowie ponad floorMember (bez gracza). Zwraca zebrane (zdjete z kies).</summary>
        private static long Collect(Clan c, long want, long floorHead, long floorMember)
        {
            long got = 0;
            var head = c.Leader;
            if (head != null && want > 0)
            {
                long x = Math.Min(want, Math.Max(0, (long)head.Gold - floorHead));
                if (x > 0) { head.ChangeHeroGold(-(int)x); CirculationWindows.NoteHeroGold(head, -x); got += x; }
            }
            foreach (var h in c.Heroes)
            {
                if (got >= want) break;
                if (h == null || h == head || h == Hero.MainHero || !h.IsAlive || h.IsChild || h.Clan != c) continue;
                long x = Math.Min(want - got, Math.Max(0, (long)h.Gold - floorMember));
                if (x > 0) { h.ChangeHeroGold(-(int)x); CirculationWindows.NoteHeroGold(h, -x); got += x; }
            }
            return got;
        }

        // ------------------------------------------------------------ dlug wymarlego rodu
        private static void Inherit(string id, Clan dead, int today)
        {
            var d = IronBank.DebtById(id);
            long bank = d != null && d.Principal >= 1 ? (long)d.Principal : 0;
            var l = OfId(id, false);
            long cl = ClaimsLeft(l);
            string name = dead != null && dead.Name != null ? dead.Name.ToString() : id;
            if (bank + cl <= 0) { IronBank.RemoveDebt(id); _l.Remove(id); return; }
            var heirs = new Dictionary<Clan, int>();
            if (l != null)
                foreach (var vid in l.Villages)
                {
                    var st = Settlement.Find(vid);
                    var oc = st != null ? st.OwnerClan : null;
                    if (oc == null || oc.IsEliminated || oc.IsBanditFaction || oc.StringId == id || oc.Leader == null || !oc.Leader.IsAlive) continue;
                    int n; heirs.TryGetValue(oc, out n); heirs[oc] = n + 1;
                }
            if (heirs.Count == 0)
            {
                _orphans.Add(new Orphan { Id = id, Name = name, Bank = bank, Claims = cl, Day = today });
                _dOrphanNew += bank + cl; _dOrphanN++;
                Log.Info("Dlugi (168): rod " + name + " wymarl bez wsi - dlug w Banku " + bank + " i inne " + cl + " zostaja w ksiedze jako dlug bez platnika (bez umorzenia).");
            }
            else
            {
                int all = heirs.Values.Sum();
                var list = heirs.ToList();
                long bGiven = 0;
                var parts = new List<string>();
                for (int i = 0; i < list.Count; i++)
                {
                    var heir = list[i].Key;
                    bool last = i == list.Count - 1;
                    long b = last ? bank - bGiven : bank * list[i].Value / all;
                    bGiven += b;
                    if (b > 0)
                    {
                        var hd = IronBank.DebtOf(heir, true);
                        double r = IronBank.RateNow(heir);
                        hd.Rate = hd.Principal > 1 ? (hd.Rate * hd.Principal + r * b) / (hd.Principal + b) : r;
                        if (hd.Principal < 1) hd.DueDay = today + Math.Max(7, IronBank.DaysPerYearNow() / 2);
                        hd.Principal += b;
                    }
                    var hl = Of(heir, true);
                    long cGiven = 0;
                    if (l != null)
                        foreach (var x in l.Claims)
                        {
                            if (x.Left <= 0) continue;
                            long part = last ? x.Left : x.Left * list[i].Value / all;
                            if (!last) x.Left -= part;
                            if (last) x.Left = 0;
                            if (part > 0) { hl.Claims.Add(new Claim { Kind = x.Kind, Creditor = x.Creditor, Crown = x.Crown, Left = part, Total = part, Day = x.Day }); cGiven += part; }
                        }
                    parts.Add(heir.Name + " " + (b + cGiven));
                    if (heir == Clan.PlayerClan) Log.Player("The debts of the extinct " + name + " pass to you with their villages: " + (b + cGiven) + " gold.", true);
                }
                _dInherited += bank + cl; _dInheritN++;
                Log.Info("Dlugi (168): rod " + name + " wymarl - dlug " + (bank + cl) + " (Bank " + bank + ", inne " + cl + ") przechodzi na nowych panow jego wsi: " + string.Join(", ", parts.ToArray()) + ".");
            }
            IronBank.RemoveDebt(id); _l.Remove(id);
        }

        // ------------------------------------------------------------ linia "Dlugi (168)" (po budzecie 166 - KW dnia)
        internal static void Report()
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.LogEnabled || Campaign.Current == null) return;
                if (!On) { ZeroBudgetDay(); return; }
                int today = (int)CampaignTime.Now.ToDays;
                int nCredit = 0, nKw = 0, nArrear = 0, nSeize = 0, nSale = 0, nRansomOnly = 0; long bankAll = 0, kwAll = 0, menAll = 0, crownAll = 0, ransomAll = 0;
                var seen = new HashSet<string>();
                foreach (var id in IronBank.DebtIds()) seen.Add(id);
                foreach (var id in _l.Keys) seen.Add(id);
                foreach (var id in seen)
                {
                    var d = IronBank.DebtById(id);
                    L l; _l.TryGetValue(id, out l);
                    long b = d != null && d.Principal >= 1 ? (long)d.Principal : 0;
                    long men = 0, crown = 0, rans = 0;
                    if (l != null) foreach (var x in l.Claims) { if (x.Left <= 0) continue; if (x.Kind == KMen) men += x.Left; else if (x.Kind == KRansom || x.Kind == KGreat) rans += x.Left; else crown += x.Left; }
                    bankAll += b; menAll += men; crownAll += crown; ransomAll += rans;
                    if (l != null) kwAll += Math.Min(l.Kw, b);
                    if (l != null && l.Seized) { if (SaleRegime(l, d)) nSale++; else nSeize++; continue; }
                    if (l != null && l.Missed > 0) { nArrear++; continue; }
                    if (b + men + crown > 0) { nCredit++; if (l != null && l.Kw > 0) nKw++; }
                    else if (rans > 0) nRansomOnly++;
                }
                long orph = 0; foreach (var o in _orphans) orph += o.Bank + o.Claims;
                var sb = new StringBuilder(1800);
                sb.Append("Dlugi (168): dzien ").Append(today)
                  .Append(" | dluznicy: kredyt ").Append(nCredit).Append(" (w tym kredyt wojenny ").Append(nKw).Append("), zaleglosc ").Append(nArrear).Append(", zajecie ").Append(nSeize)
                  .Append(", wyprzedaz ").Append(nSale).Append(", sam dlug okupu ").Append(nRansomOnly)
                  .Append("; nowe dzis: zaleglosci ").Append(_dNewArrears).Append(", zajecia ").Append(_dNewSeize).Append(" (z bankructw starego zapisu ").Append(_dFromDefault).Append(")")
                  .Append(", wyjscia z zajecia ").Append(_dSeizeEnded)
                  .Append(" | dlug: Bank ").Append(bankAll).Append(" (kredyt wojenny ").Append(kwAll).Append("), dlug zoldu ").Append(menAll).Append(", wobec korony ").Append(crownAll)
                  .Append(", okupy ").Append(ransomAll)
                  .Append(" | KW (166, dzis): miejsce w pulapie u ").Append(ClanBudgetKwN()).Append(", wyplacone ").Append(ClanBudget.LastKwLent).Append(" zl u ").Append(ClanBudget.LastKwLentN)
                  .Append(" rodow; bez KW: na limicie ").Append(_dKwAtLimit).Append(", zaleglosc/zajecie ").Append(_dKwBlockArrear).Append(", po zajeciu ").Append(_dKwBlockSeize)
                  .Append(", D nie pokrywa kosztow stalych ").Append(_dKwBlockD).Append(", Bank ponizej progu wolnego kapitalu ").Append(_dKwBlockBank)
                  .Append(" | splaty (Bank przed budzetem): z jednorazowych ").Append(_dLootRepaid).Append(" (wojna ").Append((Math.Max(0f, Math.Min(1f, s.WarCreditLootRepayShare)) * 100).ToString("0", Inv))
                  .Append("%, pokoj 100%), raty z D: Bank ").Append(_dBankInst).Append(", pozostali ").Append(_dClaimInst).Append(", w zaleglosci (ponad 3 dni zoldu) ").Append(_dArrearPaid)
                  .Append("; odsetki narosle ").Append(_dInterest).Append("; rod-raty ").Append(_dPaidN).Append(", zaleglosci dzis ").Append(_dMissed)
                  .Append("; suma rat ponad ").Append((Math.Max(0f, s.AllInstalmentsMaxShare) * 100).ToString("0", Inv)).Append("% D: ").Append(_dOver15).Append(" rodow (kontrola - 0)")
                  .Append(" | zajecie: dochod wsi ").Append(_dSeizedVill).Append(", kiesy ponad ").Append(s.SeizeFloorGold).Append(' ').Append(_dSeizedPurse)
                  .Append("; wyprzedaz ").Append(_dSales).Append(": zbrojownie ").Append(_dSoldArm).Append(", karawany ").Append(_dSoldCarN).Append(" za ").Append(_dSoldCar)
                  .Append(", warsztaty ").Append(_dSoldWsN).Append(" za ").Append(_dSoldWs)
                  .Append(" | do wierzycieli: Bank (kapital), ludzie partii ").Append(_dMenPaid).Append(" (zapasowy odbiorca ").Append(_dMenSpare).Append("), skarbce (stary dlug) ").Append(_dCrownOld)
                  .Append(", zaliczka gry w nicosc (OBIEG-1) ").Append(_dAdvNothing).Append(", okupy ").Append(_dRansomPaid).Append(", okupy wielkich jencow do skarbcow ").Append(_dGreatPaid)
                  .Append(", zapasowe kasy miast ").Append(_dSpare).Append(", bez odbiorcy ").Append(_dNoRecipient).Append(" (prog 0)")
                  .Append(" | z gry do ksiegi (od wczoraj): dlug wobec korony ").Append(_dTakenOld).Append(" + zaliczka gry ").Append(_dTakenAdv).Append(", dlug zoldu ").Append(_dWageDebtNew)
                  .Append(" (partii ").Append(_dWageDebtParties).Append("), nowa zaliczka gry ").Append(_dAdvNew)
                  .Append("; portfele gry ponownie otwarte (udzial niezaplacony zostaje dlugiem krolestwa) ").Append(_dReopened).Append(" zl w ").Append(_dReopenedN)
                  .Append(s.GameWalletsNoAdvance ? "" : " (wylaczone)")
                  .Append(" | wymarli: dlug na nowych panow wsi ").Append(_dInherited).Append(" (").Append(_dInheritN).Append("), bez platnika nowy ").Append(_dOrphanNew)
                  .Append("; dlug bez platnika razem ").Append(orph).Append(" u ").Append(_orphans.Count).Append(" rodow")
                  .Append(" | Bank: kapital ").Append((long)IronBank.FreeCapital).Append(", prog wolnego kapitalu ").Append(s.BankFreeCapitalFloor).Append(", zysk do kasy Braavos ").Append(_dProfit)
                  .Append(" | latki: ").Append(_wired.Count > 0 ? string.Join(", ", _wired.ToArray()) : "-").Append("; BRAK: ").Append(_missing.Count > 0 ? string.Join(", ", _missing.ToArray()) : "-")
                  .Append(_stumbles > 0 ? " | potkniecia " + _stumbles : "").Append('.');
                if (_importN >= 0) { sb.Append(" Wczytano z zapisu: rodow ").Append(_importN).Append(" (bledne ").Append(_importBad).Append(")."); _importN = -1; }
                Log.Info(sb.ToString());
                LastReopened = _dReopened;
            }
            catch (Exception e) { Stumble("Report", e); }
            finally { ZeroBudgetDay(); }
        }

        private static string ClanBudgetKwN() { return ClanBudget.LastKwRoomN + " rodow (" + ClanBudget.LastKwRoom + " zl)"; }

        // ------------------------------------------------------------ latki gry: portfele bez zaliczki (OBIEG-1) i B5 (karawany, warsztaty BK)
        private static Harmony _harmony;
        private static bool _hooksTried;
        private static readonly List<string> _wired = new List<string>(), _missing = new List<string>();
        internal static void SetHarmony(Harmony h) { _harmony = h; }

        internal static void EnsureHooks()
        {
            if (_hooksTried || _harmony == null || Campaign.Current == null) return;
            _hooksTried = true;
            var t = typeof(DefaultClanFinanceModel);
            var args = new[] { typeof(Clan), typeof(ExplainedNumber).MakeByRefType(), typeof(bool) };
            Wire("portfel najemnikow (AddExpensesForHiredMercenaries)", AccessTools.Method(t, "AddExpensesForHiredMercenaries", args), nameof(MercPre), nameof(MercPost));
            Wire("portfel trybutu (AddExpensesForTributes)", AccessTools.Method(t, "AddExpensesForTributes", args), nameof(TribPre), nameof(TribPost));
            Wire("portfel wezwania do wojny (AddExpensesForCallToWarAgreements)", AccessTools.Method(t, "AddExpensesForCallToWarAgreements", args), nameof(CallPre), nameof(CallPost));
            var bk = AccessTools.TypeByName("BannerKings.Behaviours.BKLordPropertyBehavior");
            Wire("B5 karawana BK (ShouldHaveCaravan)", bk != null ? AccessTools.Method(bk, "ShouldHaveCaravan", new[] { typeof(Hero), typeof(int) }) : null, nameof(PropPre), null);
            Wire("B5 warsztat BK (ShouldHaveWorkshop)", bk != null ? AccessTools.Method(bk, "ShouldHaveWorkshop", new[] { typeof(Hero), typeof(int) }) : null, nameof(PropPre), null);
            Log.Info("Dlugi (168): latki - " + (_wired.Count > 0 ? string.Join(", ", _wired.ToArray()) : "-") + "; BRAK: " + (_missing.Count > 0 ? string.Join(", ", _missing.ToArray()) : "-") + ".");
        }

        private static void Wire(string label, MethodBase m, string pre, string post)
        {
            try
            {
                if (m == null) { _missing.Add(label); return; }
                _harmony.Patch(m, prefix: pre != null ? new HarmonyMethod(typeof(DebtLadder), pre) : null,
                                  postfix: post != null ? new HarmonyMethod(typeof(DebtLadder), post) { priority = Priority.Last } : null);
                _wired.Add(label);
            }
            catch (Exception e) { _missing.Add(label + " (blad: " + e.Message + ")"); }
        }

        private static long _w0Merc, _w0Trib, _w0Call;
        private static readonly bool[] _armed = new bool[3];   // prefiks naprawde biegl dla tego wywolania (inny prefiks mogl pominac oryginal i nasz prefiks)
        private static MethodInfo _setMerc;

        public static void MercPre(Clan __0, bool __2, out long __state) { __state = Pre(__0, __2, 0); }
        public static void TribPre(Clan __0, bool __2, out long __state) { __state = Pre(__0, __2, 1); }
        public static void CallPre(Clan __0, bool __2, out long __state) { __state = Pre(__0, __2, 2); }
        public static void MercPost(Clan __0, long __state) { Reopen(__0, __state, 0); }
        public static void TribPost(Clan __0, long __state) { Reopen(__0, __state, 1); }
        public static void CallPost(Clan __0, long __state) { Reopen(__0, __state, 2); }

        private static int Wallet(Kingdom k, int kind) { return kind == 0 ? k.MercenaryWallet : kind == 1 ? k.TributeWallet : k.CallToWarWallet; }

        private static long Pre(Clan c, bool apply, int kind)
        {
            try
            {
                _armed[kind] = false;
                if (!apply || c == null || c.Kingdom == null || !WalletsOn) return long.MinValue;
                if (kind == 2 && CrownCallToWar.On) return long.MinValue;   // wezwanie placi korona - rody nie placa, nie ma czego otwierac
                long w = Wallet(c.Kingdom, kind);
                _armed[kind] = true;
                if (kind == 0) _w0Merc = w; else if (kind == 1) _w0Trib = w; else _w0Call = w;
                return c.DebtToKingdom;
            }
            catch { return long.MinValue; }
        }

        /// <summary>GameWalletsNoAdvance: gra uznala portfel krolestwa w calosci, a niezaplacony udzial dopisala rodowi do DebtToKingdom (zaliczka z niczego) -
        /// ta czesc wraca do portfela (zostaje dlugiem krolestwa, dzielonym jutro na rody), dlug rodu maleje o nia. Brak zoldu ukryty w dlugu zostaje (SoldierPay).</summary>
        private static void Reopen(Clan c, long debt0, int kind)
        {
            try
            {
                if (debt0 == long.MinValue || !_armed[kind] || c == null || c.Kingdom == null) return;
                _armed[kind] = false;
                var k = c.Kingdom;
                long added = (long)c.DebtToKingdom - debt0;
                if (added <= 0) return;
                long w0 = kind == 0 ? _w0Merc : kind == 1 ? _w0Trib : _w0Call;
                long credited = Wallet(k, kind) - w0;
                if (credited <= 0) return;
                int unpaid = (int)Math.Min(added, credited);
                if (kind == 0)
                {
                    // MercenaryWallet ma setter internal - przez refleksje (raz na proces)
                    if (_setMerc == null) _setMerc = AccessTools.PropertySetter(typeof(Kingdom), "MercenaryWallet");
                    if (_setMerc == null) return;
                    _setMerc.Invoke(k, new object[] { k.MercenaryWallet - unpaid });
                }
                else if (kind == 1) k.TributeWallet -= unpaid; else k.CallToWarWallet -= unpaid;
                c.DebtToKingdom -= unpaid;
                _dReopened += unpaid; _dReopenedN++;
            }
            catch (Exception e) { Stumble("Reopen", e); }
        }

        /// <summary>B5: rod AI z budzetem kupuje karawane albo warsztat BK tylko przy G >= 60 D i bez dlugu.</summary>
        public static bool PropPre(Hero __0, ref bool __result)
        {
            try
            {
                if (!On || __0 == null || __0.Clan == null || __0.Clan == Clan.PlayerClan) return true;
                if (!ClanBudget.PropertyBlocked(__0.Clan)) return true;
                __result = false;
                return false;
            }
            catch { return true; }
        }

        // ------------------------------------------------------------ zapis (SaveText, "arm_debt168")
        private static readonly char[] Bad = { '|', ';', ',', '~', '^', '+', ':' };
        private static string Safe(string s) { if (string.IsNullOrEmpty(s)) return ""; var sb = new StringBuilder(s.Length); foreach (var ch in s) sb.Append(Array.IndexOf(Bad, ch) >= 0 ? ' ' : ch); return sb.ToString(); }

        /// <summary>"v1|id,seized,seizeDay,zeroDays,saleDay,freeDay,missed,kw,seizeEma,wies+wies,kind^wierzyciel^korona^reszta^calosc^doba~...;...|id:ema:n;...|id^nazwa^bank^inne^doba;..."</summary>
        internal static string Export()
        {
            try
            {
                var sb = new StringBuilder(64 + _l.Count * 120 + _loot.Count * 24);
                sb.Append("v1|");
                bool first = true;
                foreach (var kv in _l)
                {
                    var l = kv.Value;
                    if (kv.Key == null || kv.Key.IndexOfAny(Bad) >= 0) continue;
                    if (!first) sb.Append(';'); first = false;
                    sb.Append(kv.Key).Append(',').Append(l.Seized ? 1 : 0).Append(',').Append(l.SeizeDay.ToString(Inv)).Append(',').Append(l.ZeroDays.ToString(Inv)).Append(',')
                      .Append(l.SaleDay.ToString(Inv)).Append(',').Append(l.FreeDay.ToString(Inv)).Append(',').Append(l.Missed.ToString(Inv)).Append(',').Append(l.Kw.ToString(Inv)).Append(',')
                      .Append(Math.Round(l.SeizeEma).ToString(Inv)).Append(',');
                    bool fv = true;
                    foreach (var v in l.Villages) { if (v == null || v.IndexOfAny(Bad) >= 0) continue; if (!fv) sb.Append('+'); fv = false; sb.Append(v); }
                    sb.Append(',');
                    bool fc = true;
                    foreach (var c in l.Claims)
                    {
                        if (c.Left <= 0 || (c.Creditor ?? "").IndexOfAny(Bad) >= 0 || (c.Crown ?? "").IndexOfAny(Bad) >= 0) continue;
                        if (!fc) sb.Append('~'); fc = false;
                        sb.Append(c.Kind.ToString(Inv)).Append('^').Append(c.Creditor).Append('^').Append(c.Crown).Append('^').Append(c.Left.ToString(Inv)).Append('^').Append(c.Total.ToString(Inv)).Append('^').Append(c.Day.ToString(Inv));
                    }
                }
                sb.Append('|');
                first = true;
                foreach (var kv in _loot)
                {
                    if (kv.Key == null || kv.Key.IndexOfAny(Bad) >= 0 || (kv.Value.Ema <= 0 && kv.Value.N <= 0)) continue;
                    if (!first) sb.Append(';'); first = false;
                    sb.Append(kv.Key).Append(':').Append(Math.Round(kv.Value.Ema, 1).ToString(Inv)).Append(':').Append(kv.Value.N.ToString(Inv));
                }
                sb.Append('|');
                first = true;
                foreach (var o in _orphans)
                {
                    if (!first) sb.Append(';'); first = false;
                    sb.Append(Safe(o.Id)).Append('^').Append(Safe(o.Name)).Append('^').Append(o.Bank.ToString(Inv)).Append('^').Append(o.Claims.ToString(Inv)).Append('^').Append(o.Day.ToString(Inv));
                }
                return sb.ToString();
            }
            catch (Exception e) { Stumble("Export", e); return ""; }
        }

        internal static void Import(string data)
        {
            _l.Clear(); _loot.Clear(); _orphans.Clear(); _importN = 0; _importBad = 0;
            try
            {
                if (string.IsNullOrEmpty(data)) return;
                var f = data.Split('|');
                if (f.Length < 4 || f[0] != "v1") { _importBad++; return; }
                if (f[1].Length > 0)
                    foreach (var rec in f[1].Split(';'))
                    {
                        try
                        {
                            var x = rec.Split(',');
                            if (x.Length < 11 || x[0].Length == 0) { _importBad++; continue; }
                            var l = new L();
                            l.Seized = x[1] == "1";
                            l.SeizeDay = int.Parse(x[2], NumberStyles.Integer, Inv); l.ZeroDays = int.Parse(x[3], NumberStyles.Integer, Inv);
                            l.SaleDay = int.Parse(x[4], NumberStyles.Integer, Inv); l.FreeDay = int.Parse(x[5], NumberStyles.Integer, Inv);
                            l.Missed = int.Parse(x[6], NumberStyles.Integer, Inv); l.Kw = long.Parse(x[7], NumberStyles.Integer, Inv);
                            l.SeizeEma = double.Parse(x[8], NumberStyles.Float, Inv);
                            if (x[9].Length > 0) l.Villages = x[9].Split('+').Where(v => v.Length > 0).ToList();
                            if (x[10].Length > 0)
                                foreach (var cs in x[10].Split('~'))
                                {
                                    var y = cs.Split('^');
                                    if (y.Length < 6) { _importBad++; continue; }
                                    var c = new Claim { Kind = int.Parse(y[0], NumberStyles.Integer, Inv), Creditor = y[1], Crown = y[2], Left = long.Parse(y[3], NumberStyles.Integer, Inv),
                                                        Total = long.Parse(y[4], NumberStyles.Integer, Inv), Day = int.Parse(y[5], NumberStyles.Integer, Inv) };
                                    if (c.Left > 0 && c.Kind >= KMen && c.Kind <= KGreat) l.Claims.Add(c); else _importBad++;
                                }
                            _l[x[0]] = l; _importN++;
                        }
                        catch { _importBad++; }
                    }
                if (f[2].Length > 0)
                    foreach (var rec in f[2].Split(';'))
                    {
                        var x = rec.Split(':'); double ema; int n;
                        if (x.Length == 3 && x[0].Length > 0 && double.TryParse(x[1], NumberStyles.Float, Inv, out ema) && int.TryParse(x[2], NumberStyles.Integer, Inv, out n))
                            _loot[x[0]] = new Loot { Ema = ema, N = Math.Max(0, Math.Min(84, n)) };
                        else _importBad++;
                    }
                if (f[3].Length > 0)
                    foreach (var rec in f[3].Split(';'))
                    {
                        var y = rec.Split('^'); long b, cl; int day;
                        if (y.Length == 5 && long.TryParse(y[2], NumberStyles.Integer, Inv, out b) && long.TryParse(y[3], NumberStyles.Integer, Inv, out cl) && int.TryParse(y[4], NumberStyles.Integer, Inv, out day))
                            _orphans.Add(new Orphan { Id = y[0], Name = y[1], Bank = b, Claims = cl, Day = day });
                        else _importBad++;
                    }
            }
            catch (Exception e) { Stumble("Import", e); }
        }
    }
}
