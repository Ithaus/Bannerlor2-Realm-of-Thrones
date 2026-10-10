using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// BANK ZELAZNY Z BRAAVOS (Jeff 04.10: "lordowie moga pozyczac pieniadze, jesli potrzeba, tak jak
    /// bylo w sredniowieczu, potem splacaja albo z wygranej wojny, lupy wojenne"; liczby zatwierdzone
    /// "tak zgoda"). Gotowe mody (A Life of Ice and Fire, Simple Bank, Iron Vaults...) pozyczaja tylko
    /// graczowi - tu pozyczaja tez lordowie AI.
    ///
    /// LIMIT: dochod rodu z 60 dni (model finansow, dochody brutto) + 10 000 za miasto + 5 000 za zamek,
    /// x wiarygodnosc (1.0 rzetelni, 0.5 po spoznieniach, 0 po bankructwie); dluznicy-bankruci sprawiaja,
    /// ze ich WROGOWIE dostaja limit x1.5 (Bank pozycza przeciwnikom dluznika).
    /// KIEDY AI: zloto glowy rodu < 10 dni zoldu partii wojennych rodu (w wojnie: < 20 dni) -> pozycza
    /// do 1.5x brakujacej sumy, nie ponad limit. Tylko rody krolestw i krolowie.
    /// OPROCENTOWANIE roczne (rok wedle kalendarza gry - Calendar.DaysPerYear, dopasuje sie do zmiany
    /// kalendarza): krol 20%, rod z fiefami 30%, bez fiefow 45%, +10 pp za kazda kolejna pozyczke,
    /// +15 pp po wczesniejszym bankructwie.
    /// SPLATA: termin pol roku; codzienna rata = dlug / dni do terminu + narosle odsetki; gdy zloto
    /// > 3x zapasu na zold, 20% nadwyzki idzie na splate (lupy, okupy, zdobycze).
    /// NIEPLACENIE: niezaplacona rata = spoznienie (wiarygodnosc x0.8, oprocentowanie +2 pp); trzy pod
    /// rzad = BANKRUCTWO: kredyt odciety, wiarygodnosc 0, Bank sciaga 25% zlota przy kazdej okazji
    /// i pozycza wrogom; gracz traci jednorazowo renome.
    /// KAPITAL BANKU: start IronBankCapital (5 mln); pozyczki go zmniejszaja, splaty z odsetkami
    /// zwiekszaja - pusty bank nie pozycza; dlug rodu wymarlego to strata Banku.
    /// GRACZ: w menu miasta Braavos (town_EN5) opcja "Visit the Iron Bank": pozyczka i splata.
    /// </summary>
    internal static class IronBank
    {
        internal static void Reset() { _debts.Clear(); _capital = -1; _lastDay = -1; _logged = 0; _famRates = 0; _famNoLoan = 0; _famSum = 0; _famRateSum = 0; _famRatesViaLoan = 0; _famToday.Clear(); ZeroLast(); }

        // paczka 169 (tylko log): liczby doby dla linii "Obieg" - zerowane TYLKO w Reset i w MoneyLedger.ClearLast169() (D20)
        internal static int LastLent, LastPaidN, LastMissed, LastDefaults;
        internal static long LastLentSum, LastPaidSum;

        internal static void ZeroLast() { LastLent = LastPaidN = LastMissed = LastDefaults = 0; LastLentSum = LastPaidSum = 0; }

        /// <summary>Paczka 169: dlug rodu (odczyt O(1)); false, gdy brak albo ponizej 1.</summary>
        internal static bool TryGetDebt(Clan c, out double principal, out int missed, out bool defaulted, out int dueDay)
        {
            principal = 0; missed = 0; defaulted = false; dueDay = 0;
            var d = Of(c, false);
            if (d == null || d.Principal < 1) return false;
            principal = d.Principal; missed = d.Missed; defaulted = d.Defaulted; dueDay = d.DueDay;
            return true;
        }

        /// <summary>Paczka 169: wiarygodnosc rodu jak w Limit (bankrut 0, bez ksiegi 1) - do limitu na sucho; true, gdy rod ma wpis w Banku.</summary>
        internal static bool TryGetTrust(Clan c, out float trust)
        {
            var d = Of(c, false);
            trust = d != null ? (d.Defaulted ? 0f : d.Trust) : 1f;
            return d != null;
        }

        /// <summary>Paczka 169: krolestwa rodow-bankrutow (raz na dobe; rod przez slownik StringId -> Clan zbudowany przez wolajacego - bez Clan.FindFirst).</summary>
        internal static void DefaultedKingdoms(HashSet<Kingdom> into, Dictionary<string, Clan> byId)
        {
            into.Clear();
            foreach (var kv in _debts)
            {
                if (!kv.Value.Defaulted) continue;
                Clan c;
                if (byId != null && byId.TryGetValue(kv.Key, out c) && c != null && c.Kingdom != null) into.Add(c.Kingdom);
            }
        }
        internal sealed class Debt
        {
            public double Principal;      // dlug glowny + doliczone odsetki niesplacone
            public double Rate;           // roczne (0.30 = 30%)
            public int DueDay;            // dzien terminu
            public int Missed;            // spoznienia pod rzad
            public float Trust = 1f;      // wiarygodnosc
            public bool Defaulted;        // bankrut
            public int Loans;             // ile pozyczek wzial (do narzutu)
            public bool EverDefaulted;
        }

        private static readonly Dictionary<string, Debt> _debts = new Dictionary<string, Debt>();   // klucz: Clan.StringId
        private static double _capital = -1;
        private static int _logged, _lastDay = -1;
        private const string Menu = "arm_iron_bank";
        private const string BraavosId = "town_EN5";   // Braavos w ROT-Map settlements.xml (sprawdzone 04.10)

        internal static bool On { get { var s = Settings.Current; return s != null && s.IronBankEnabled; } }

        /// <summary>Kapital Banku teraz (0, gdy Bank jeszcze nie ruszyl) - odczyt dla ksiegi "Pieniadz swiata" (MoneyLedger).</summary>
        internal static long CapitalNow { get { return _capital > 0 ? (long)_capital : 0L; } }

        // dlugosc roku z ZYWEGO modelu czasu gry - dopasuje sie do kazdej zmiany kalendarza
        private static int DaysPerYear()
        {
            try { var m = Campaign.Current.Models.CampaignTimeModel; return Math.Max(28, m.DaysInWeek * m.WeeksInSeason * m.SeasonsInYear); }
            catch { return 84; }
        }

        private static Debt Of(Clan c, bool create)
        {
            if (c == null) return null;
            Debt d;
            if (_debts.TryGetValue(c.StringId, out d) || !create) return d;
            d = new Debt();
            _debts[c.StringId] = d;
            return d;
        }

        // ------------------------------------------------------------ limit i oprocentowanie
        private static int Wages(Clan c)
        {
            int w = 0;
            try { foreach (var p in c.WarPartyComponents) if (p != null && p.MobileParty != null && p.MobileParty.IsActive) w += p.MobileParty.TotalWage; } catch { }
            return w;
        }

        private static bool AtWar(Clan c)
        {
            try
            {
                var k = c.Kingdom;
                if (k == null) return false;
                foreach (var f in k.FactionsAtWarWith) if (f != null && f.IsKingdomFaction) return true;
            }
            catch { }
            return false;
        }

        // wzor jak IronBank.Limit / ClanIncomeBook (paczka 169 liczy ten sam limit na sucho bez wolania tej metody - zmiana wzoru tu = zmiana tam)
        internal static int Limit(Clan c)
        {
            try
            {
                var s = Settings.Current;
                var d = Of(c, false);
                float trust = d != null ? (d.Defaulted ? 0f : d.Trust) : 1f;
                if (trust <= 0f) return 0;
                float income = 0f;
                try { income = Math.Max(0f, Campaign.Current.Models.ClanFinanceModel.CalculateClanIncome(c, false, false, false).ResultNumber); } catch { }
                int towns = 0, castles = 0;
                foreach (var f in c.Fiefs) { if (f == null) continue; if (f.IsCastle) castles++; else if (f.IsTown) towns++; }
                float lim = income * Math.Max(0f, s.IronBankIncomeDays) + towns * s.IronBankPerTown + castles * s.IronBankPerCastle;
                // Bank pozycza wrogom bankrutow
                if (c.Kingdom != null)
                    foreach (var kv in _debts)
                    {
                        if (!kv.Value.Defaulted) continue;
                        var dc = Clan.FindFirst(x => x.StringId == kv.Key);
                        if (dc != null && dc.Kingdom != null && dc.Kingdom != c.Kingdom && c.Kingdom.IsAtWarWith(dc.Kingdom)) { lim *= 1.5f; break; }
                    }
                return (int)(lim * trust);
            }
            catch { return 0; }
        }

        private static double RateFor(Clan c, Debt d)
        {
            var s = Settings.Current;
            bool king = c.Kingdom != null && c.Kingdom.RulingClan == c;
            int fiefs = 0; try { fiefs = c.Fiefs.Count; } catch { }
            double r = king ? s.IronBankRateKing : (fiefs > 0 ? s.IronBankRateLanded : s.IronBankRateLandless);
            if (d != null)
            {
                if (d.Principal > 1) r += s.IronBankRatePerLoan;
                if (d.EverDefaulted) r += s.IronBankRateAfterDefault;
            }
            return r / 100.0;
        }

        private static void EnsureCapital()
        {
            if (_capital < 0) _capital = Math.Max(0, Settings.Current.IronBankCapital);
        }

        /// <summary>Wyplata pozyczki: zloto od Banku do glowy rodu, nowy termin, sredni procent wazony.</summary>
        internal static int Lend(Clan c, int amount, string why)
        {
            try
            {
                EnsureCapital();
                if (c == null || c.Leader == null || amount <= 0) return 0;
                var d = Of(c, true);
                if (d.Defaulted) return 0;
                int room = Limit(c) - (int)d.Principal;
                amount = Math.Min(amount, Math.Min(room, (int)_capital));
                if (amount <= 0) return 0;
                double rate = RateFor(c, d);
                // AUDYT 04.10 (B5): oplata za udzielenie (pozyczka i splata tego samego dnia nie jest juz darmowa)
                // i termin NIE przesuwa sie przy dobieraniu - wczesniej kazda nowa pozyczka odsuwala termin calego
                // dlugu (rolowanie w nieskonczonosc, AI tez).
                double fee = amount * Math.Max(0f, Settings.Current.IronBankLoanFeePercent) / 100.0;
                bool running = d.Principal > 1;
                int today0 = (int)CampaignTime.Now.ToDays;
                // audyt ponowny W6: tuz przed terminem Bank nie dobiera - inaczej rata skacze (np. 1/3 dlugu dziennie)
                // audyt pelny K3: takze W dniu terminu i PO nim - wczesniej wtedy termin calego dlugu przesuwal sie o pol roku
                if (running && d.DueDay - today0 < Math.Max(1, Settings.Current.IronBankMinDaysToLend)) return 0;
                d.Rate = running ? (d.Rate * d.Principal + rate * amount) / (d.Principal + amount) : rate;
                d.Principal += amount + fee;
                d.Loans++;
                int today = (int)CampaignTime.Now.ToDays;
                if (!running) d.DueDay = today + Math.Max(7, DaysPerYear() / 2);
                c.Leader.ChangeHeroGold(amount);
                _capital -= amount;
                CirculationWindows.NoteHeroGold(c.Leader, amount);   // paczka 169b: glowa poza swiatem - zloto wyszlo ze swiata (tylko licznik)
                Note("IronBank: " + c.Name + " pozycza " + amount + " (" + why + ") na " + (d.Rate * 100).ToString("0") + "% rocznie, dlug " + (int)d.Principal
                     + ", termin dzien " + d.DueDay + ", limit " + Limit(c) + ", kapital Banku " + (long)_capital + ".");
                return amount;
            }
            catch (Exception e) { Log.Error("IronBank.Lend", e); return 0; }
        }

        // ------------------------------------------------------------ T5 noc 08/09.10: rodzina pomaga glowie (tylko rody AI)
        // Raport 05 A1 (most do 166 - po 166 wylaczyc): zanim Bank uzna rate za niezaplacona albo udzieli nowej pozyczki,
        // dorosli zywi czlonkowie rodu (bez glowy) oddaja glowie brakujaca kwote z tego, co maja ponad prog
        // max(5 000; IronBankWageDays dni zoldu wlasnej partii). Przelew miedzy bohaterami (GiveGoldAction) - nic z niczego.
        private const int FamilyFloor = 5000;               // prog czlonka bez partii (FamilyPurseFloor z projektu 6.4)
        private static int _famRates, _famNoLoan;           // licznik doby: raty pokryte przez rodzine, pozyczki niepotrzebne
        private static long _famSum, _famRateSum;           // licznik doby: zloto od rodziny razem i w tym na raty
        private static int _famRatesViaLoan;                // licznik doby: raty oplacone dzieki przelewowi z kroku pozyczki (w _famRates)
        private static readonly Dictionary<string, int> _famToday = new Dictionary<string, int>();   // doba: rod -> zloto od rodziny w kroku pozyczki

        internal static bool FamilyOn { get { var s = Settings.Current; return s != null && s.IronBankFamilyPays; } }

        /// <summary>Dorosli czlonkowie rodu AI oddaja glowie do 'need' z nadwyzki ponad prog. allOrNothing: przelew tylko,
        /// gdy rodzina pokryje caly brak (rata - czesciowa pomoc i tak nie ratuje przed spoznieniem). Zwraca przelana kwote.</summary>
        private static int FamilyCover(Clan c, int need, bool allOrNothing)
        {
            if (need <= 0 || c == null || c == Clan.PlayerClan || !FamilyOn) return 0;
            if (ClanBudget.FamilyRuleFor(c)) return 0;   // 166: rod z budzetem - kiesa rodziny budzetu zastepuje T5 (jedna regula); inne rody jak dotad
            int got = 0, donors = 0, minLeft = int.MaxValue;
            try
            {
                var head = c.Leader;
                if (head == null || head == Hero.MainHero) return 0;
                int days = Math.Max(1, Settings.Current.IronBankWageDays);
                for (int pass = allOrNothing ? 0 : 1; pass < 2; pass++)
                {
                    int sum = 0;
                    foreach (var h in c.Heroes)
                    {
                        if (sum >= need) break;
                        if (h == null || h == head || h == Hero.MainHero || !h.IsAlive || h.IsChild) continue;
                        int floor = FamilyFloor;
                        try { var mp = h.PartyBelongedTo; if (mp != null && mp.LeaderHero == h) floor = Math.Max(floor, mp.TotalWage * days); } catch { }
                        int spare = h.Gold - floor;
                        if (spare <= 0) continue;
                        int give = Math.Min(spare, need - sum);
                        if (pass == 1)
                        {
                            int before = head.Gold;
                            TaleWorlds.CampaignSystem.Actions.GiveGoldAction.ApplyBetweenCharacters(h, head, give, true);
                            give = Math.Max(0, head.Gold - before);
                            got += give;   // na biezaco - wyjatek w polowie przelewow nie gubi juz przelanego zlota z licznikow
                            if (give > 0) { donors++; minLeft = Math.Min(minLeft, h.Gold - floor); }
                        }
                        sum += give;
                    }
                    if (pass == 0 && sum < need) return 0;   // rodzina nie pokryje calej raty - nic nie ruszamy
                }
            }
            catch (Exception e) { Log.Error("IronBank.FamilyCover", e); }
            if (got > 0)
            {
                _famSum += got;
                // poprawka po recenzji: slad na rod (limit IronBankLogPerDay) - kto, ile, ilu dawcow, czy dawcy zostali na progu (nadwyzka >= 0)
                Note("IronBank: rodzina " + c.Name + " oddaje glowie " + got + " (" + (allOrNothing ? "rata" : "przed pozyczka") + "), dawcow " + donors
                     + ", najnizsza nadwyzka dawcy ponad jego prog po przelewie " + (minLeft == int.MaxValue ? 0 : minLeft) + " (prog min. " + FamilyFloor + ").");
            }
            return got;
        }

        private static void Note(string line)
        {
            var s = Settings.Current;
            if (_logged < Math.Max(0, s.IronBankLogPerDay)) { _logged++; Log.Info(line); }
        }

        // ------------------------------------------------------------ dzien
        internal static void Daily()
        {
            try
            {
                if (!On || Campaign.Current == null) return;
                EnsureCapital();
                var s = Settings.Current;
                int today = (int)CampaignTime.Now.ToDays;
                if (_lastDay == today) return;
                _lastDay = today;
                _logged = 0;
                _famRates = 0; _famNoLoan = 0; _famSum = 0; _famRateSum = 0; _famRatesViaLoan = 0; _famToday.Clear();   // T5: liczniki doby
                int lent = 0, paid = 0, missed = 0, defaults = 0; long lentSum = 0, paidSum = 0;

                // 1. lordowie AI pozyczaja, gdy brakuje na zold (i sprzet w wojnie)
                foreach (var c in Clan.All)
                {
                    if (c == null || c == Clan.PlayerClan || c.IsEliminated || c.IsBanditFaction || c.IsMinorFaction || c.Kingdom == null || c.Leader == null) continue;
                    int need = Wages(c) * Math.Max(1, s.IronBankWageDays);
                    if (need <= 0) continue;
                    int target = AtWar(c) ? need * 2 : need;
                    int gold = c.Leader.Gold;
                    if (gold >= target) continue;
                    // T5: najpierw rodzina (do wysokosci target); bankrutowi nie - Bank i tak nie pozycza, a sciaga 25% kiesy glowy.
                    // Poprawka po recenzji: tez nie rodom zagrozonym (spoznienie w toku albo termin blizej niz IronBankMinDaysToLend -
                    // Bank wtedy nie dobiera) - czesciowa pomoc trafilaby do kiesy glowy, z ktorej Bank przy 3. spoznieniu zajmuje
                    // polowe; dla nich rodzina placi tylko przy racie, w trybie "calosc albo nic".
                    var dl = Of(c, false);
                    bool risky = dl != null && (dl.Defaulted || dl.Missed > 0
                                 || (dl.Principal > 1 && dl.DueDay - today < Math.Max(1, s.IronBankMinDaysToLend)));
                    if (!risky)
                    {
                        try
                        {
                            int fam = FamilyCover(c, target - gold, false);
                            gold = c.Leader.Gold;   // zawsze odswiez - takze po wyjatku w polowie przelewow
                            if (fam > 0)
                            {
                                int prev; _famToday.TryGetValue(c.StringId, out prev); _famToday[c.StringId] = prev + fam;
                                if (gold >= target) { _famNoLoan++; continue; }
                            }
                        }
                        catch (Exception e) { Log.Error("IronBank.FamilyBeforeLoan", e); }
                    }
                    int got = Lend(c, (int)((target - gold) * 1.5f), AtWar(c) ? "zold i sprzet na wojne" : "zold");
                    if (got > 0) { lent++; lentSum += got; }
                }

                // 2. splaty, odsetki, spoznienia, bankructwa
                var dead = new List<string>();
                foreach (var kv in _debts)
                {
                    var d = kv.Value;
                    if (d.Principal < 1) continue;
                    var c = Clan.FindFirst(x => x.StringId == kv.Key);
                    if (c == null || c.IsEliminated || c.Leader == null) { dead.Add(kv.Key); continue; }
                    double interest = d.Principal * d.Rate / DaysPerYear();
                    d.Principal += interest;
                    var hero = c.Leader;
                    int reserve = c == Clan.PlayerClan ? 0 : Wages(c) * Math.Max(1, s.IronBankWageDays);
                    int pay;
                    if (d.Defaulted)
                    {
                        pay = (int)Math.Min(d.Principal, hero.Gold * 0.25);       // Bank sciaga przy kazdej okazji
                    }
                    else
                    {
                        int daysLeft = Math.Max(1, d.DueDay - today);
                        pay = (int)Math.Ceiling(Math.Min(d.Principal, d.Principal / daysLeft));
                        // T5: zanim rata bedzie spoznieniem - rodzina rodu AI oddaje glowie brak (tylko gdy pokryje calosc)
                        if (hero.Gold < pay && c != Clan.PlayerClan)
                        {
                            try
                            {
                                int fam = FamilyCover(c, pay - hero.Gold, true);
                                if (fam > 0) { _famRateSum += fam; if (hero.Gold >= pay) _famRates++; }
                            }
                            catch (Exception e) { Log.Error("IronBank.FamilyBeforeMiss", e); }
                        }
                        else if (_famToday.Count > 0 && c != Clan.PlayerClan)
                        {
                            // poprawka po recenzji: rata oplacona tylko dzieki zlotu rodziny z kroku pozyczki (kiesa bez tego przelewu < rata)
                            int fromLoanStep;
                            if (_famToday.TryGetValue(kv.Key, out fromLoanStep) && hero.Gold - fromLoanStep < pay) { _famRates++; _famRatesViaLoan++; }
                        }
                        if (hero.Gold < pay)
                        {
                            d.Missed++; missed++;
                            d.Trust = Math.Max(0.5f, d.Trust * 0.8f);
                            d.Rate += 0.02;
                            if (d.Missed >= 3)
                            {
                                d.Defaulted = true; d.EverDefaulted = true; d.Trust = 0f; defaults++;
                                // zajecie od razu: polowa skarbca na poczet dlugu (wczesniej tylko 25% dziennie - do wydania przed sciagnieciem)
                                int seize = (int)Math.Min(d.Principal, hero.Gold * Math.Max(0f, Math.Min(1f, s.IronBankDefaultSeizeShare)));
                                if (seize > 0) { hero.ChangeHeroGold(-seize); d.Principal -= seize; _capital += seize; paidSum += seize; }
                                if (seize > 0) CirculationWindows.NoteHeroGold(hero, -seize);   // paczka 169b (tylko licznik)
                                Log.Info("IronBank: BANKRUCTWO " + c.Name + " - dlug " + (int)d.Principal + ", Bank odcina kredyt, sciaga zloto i pozycza wrogom.");
                                if (c == Clan.PlayerClan)
                                {
                                    try { c.AddRenown(-Math.Max(0f, s.IronBankPlayerDefaultRenown)); } catch { }
                                    InformationManager.DisplayMessage(new InformationMessage("The Iron Bank will have its due. Your credit is gone, your name is worth less, and the Bank now funds your enemies.", Colors.Red));
                                }
                            }
                            else if (c == Clan.PlayerClan)
                                InformationManager.DisplayMessage(new InformationMessage("Iron Bank: you missed an instalment of " + pay + " (" + d.Missed + "/3). The Bank remembers.", Colors.Yellow));
                            continue;
                        }
                        d.Missed = 0;
                        // nadwyzka (lupy, okupy) - 20% ponad 3x zapasu na zold
                        int excess = hero.Gold - pay - reserve * 3;
                        if (c != Clan.PlayerClan && excess > 0) pay += (int)Math.Min(d.Principal - pay, excess * 0.2);
                    }
                    if (pay <= 0) continue;
                    hero.ChangeHeroGold(-pay);
                    d.Principal -= pay;
                    _capital += pay;
                    CirculationWindows.NoteHeroGold(hero, -pay);   // paczka 169b (tylko licznik)
                    paid++; paidSum += pay;
                    if (d.Principal < 1)
                    {
                        d.Principal = 0; d.Defaulted = false; d.Missed = 0;
                        d.Trust = Math.Min(1f, d.Trust + 0.25f);
                        Note("IronBank: " + c.Name + " splacil dlug w calosci; wiarygodnosc " + d.Trust.ToString("0.00") + ".");
                    }
                }
                foreach (var k in dead)
                {
                    var d = _debts[k];
                    if (d.Principal > 1) Log.Info("IronBank: rod " + k + " wymarl - Bank traci " + (int)d.Principal + ".");
                    _debts.Remove(k);
                }

                long total = 0; int debtors = 0, bankrupt = 0;
                foreach (var kv in _debts) { if (kv.Value.Principal >= 1) { total += (long)kv.Value.Principal; debtors++; if (kv.Value.Defaulted) bankrupt++; } }
                LastLent = lent; LastPaidN = paid; LastMissed = missed; LastDefaults = defaults; LastLentSum = lentSum; LastPaidSum = paidSum;   // paczka 169 (tylko log)
                Log.Info("IronBank: dzien " + today + " - nowe pozyczki " + lent + " (" + lentSum + "), splaty " + paid + " (" + paidSum + "), spoznienia " + missed
                         + ", bankructwa dzis " + defaults + "; dluznikow " + debtors + " (bankrutow " + bankrupt + "), dlug razem " + total + ", kapital Banku " + (long)_capital
                         + (FamilyOn ? "; z kies rodziny: " + _famRates + " rat, " + _famSum + " zl (na raty " + _famRateSum + ", przed pozyczka " + (_famSum - _famRateSum)
                                       + "); pozyczek mniej o " + _famNoLoan + ", w tym rat z przelewu przed pozyczka " + _famRatesViaLoan
                                       : "; z kies rodziny: wylaczone")
                         + ".");
            }
            catch (Exception e) { Log.Error("IronBank.Daily", e); }
        }

        // ------------------------------------------------------------ menu gracza w Braavos
        internal static void AddMenus(CampaignGameStarter starter)
        {
            try
            {
                starter.AddGameMenuOption("town", "arm_iron_bank_enter", "{=!}Visit the Iron Bank",
                    delegate (MenuCallbackArgs a)
                    {
                        a.optionLeaveType = GameMenuOption.LeaveType.Trade;
                        var st = Settlement.CurrentSettlement;
                        return On && st != null && st.StringId == BraavosId;
                    },
                    delegate { GameMenu.SwitchToMenu(Menu); }, false, 4);
                starter.AddGameMenu(Menu, "{=!}{IRON_BANK_TEXT}", delegate (MenuCallbackArgs a) { MBTextManager.SetTextVariable("IRON_BANK_TEXT", BankText()); });
                starter.AddGameMenuOption(Menu, "arm_iron_bank_borrow", "{=!}Ask for a loan",
                    delegate (MenuCallbackArgs a) { a.optionLeaveType = GameMenuOption.LeaveType.Trade; return true; },
                    delegate { BorrowDialog(); }, false, 0);
                starter.AddGameMenuOption(Menu, "arm_iron_bank_repay", "{=!}Repay what you owe",
                    delegate (MenuCallbackArgs a) { a.optionLeaveType = GameMenuOption.LeaveType.Trade; var d = Of(Clan.PlayerClan, false); return d != null && d.Principal >= 1; },
                    delegate { RepayDialog(); }, false, 1);
                starter.AddGameMenuOption(Menu, "arm_iron_bank_leave", "{=!}Leave",
                    delegate (MenuCallbackArgs a) { a.optionLeaveType = GameMenuOption.LeaveType.Leave; return true; },
                    delegate { GameMenu.SwitchToMenu("town"); }, true, 2);
            }
            catch (Exception e) { Log.Error("IronBank.AddMenus", e); }
        }

        private static string BankText()
        {
            var c = Clan.PlayerClan;
            var d = Of(c, false);
            var sb = new StringBuilder("The keyholders of the Iron Bank receive you in a cold stone hall. ");
            int lim = Limit(c);
            if (d != null && d.Defaulted) sb.Append("Your name is written in the book of those who did not pay. There will be no more loans.");
            else
            {
                sb.Append("They will lend up to " + Math.Max(0, lim - (int)(d != null ? d.Principal : 0)) + " gold");
                sb.Append(" at " + (RateFor(c, d) * 100).ToString("0") + "% a year, to be repaid within half a year.");
            }
            if (d != null && d.Principal >= 1)
                sb.Append("\n\nYou owe " + (int)d.Principal + " gold at " + (d.Rate * 100).ToString("0") + "% a year, due by day " + d.DueDay
                          + (d.Missed > 0 ? " (missed instalments: " + d.Missed + "/3)" : "") + ". The Bank takes its instalment every day.");
            return sb.ToString();
        }

        private static void BorrowDialog()
        {
            var c = Clan.PlayerClan;
            var d = Of(c, false);
            int room = Math.Max(0, Limit(c) - (int)(d != null ? d.Principal : 0));
            if (room <= 0) { InformationManager.DisplayMessage(new InformationMessage("The Iron Bank will not lend you more.", Colors.Red)); return; }
            var opts = new List<InquiryElement>();
            foreach (var share in new[] { 0.25f, 0.5f, 1f })
            {
                int amt = (int)(room * share);
                if (amt > 0) opts.Add(new InquiryElement(amt, amt + " gold", null));
            }
            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                "The Iron Bank", "How much will you borrow? The Bank will have its due.", opts, true, 1, 1, "Borrow", "Leave",
                delegate (List<InquiryElement> sel)
                {
                    if (sel == null || sel.Count == 0) return;
                    int got = Lend(c, (int)sel[0].Identifier, "gracz");
                    if (got > 0) InformationManager.DisplayMessage(new InformationMessage("The Iron Bank lends you " + got + " gold.", Colors.Green));
                    GameMenu.SwitchToMenu(Menu);
                },
                delegate (List<InquiryElement> _) { }), true);
        }

        private static void RepayDialog()
        {
            var c = Clan.PlayerClan;
            var d = Of(c, false);
            if (d == null || d.Principal < 1) return;
            int owe = (int)Math.Ceiling(d.Principal);
            int can = Math.Min(owe, Hero.MainHero.Gold);
            if (can <= 0) { InformationManager.DisplayMessage(new InformationMessage("You have no gold to repay.", Colors.Red)); return; }
            var opts = new List<InquiryElement>();
            foreach (var share in new[] { 0.25f, 0.5f, 1f })
            {
                int amt = (int)(can * share);
                if (amt > 0) opts.Add(new InquiryElement(amt, amt + " gold" + (amt >= owe ? " (all)" : ""), null));
            }
            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                "The Iron Bank", "You owe " + owe + " gold. How much will you repay?", opts, true, 1, 1, "Repay", "Leave",
                delegate (List<InquiryElement> sel)
                {
                    if (sel == null || sel.Count == 0) return;
                    int pay = Math.Min((int)sel[0].Identifier, Hero.MainHero.Gold);
                    EnsureCapital();
                    Hero.MainHero.ChangeHeroGold(-pay);
                    d.Principal -= pay; _capital += pay;
                    if (d.Principal < 1) { d.Principal = 0; d.Missed = 0; d.Defaulted = false; d.Trust = Math.Min(1f, d.Trust + 0.25f); }
                    InformationManager.DisplayMessage(new InformationMessage("You repay " + pay + " gold to the Iron Bank.", Colors.Green));
                    Log.Info("IronBank: gracz splaca " + pay + ", dlug " + (int)d.Principal + ".");
                    GameMenu.SwitchToMenu(Menu);
                },
                delegate (List<InquiryElement> _) { }), true);
        }

        // ------------------------------------------------------------ zapis
        internal static string Export()
        {
            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.Append("cap=").Append(_capital.ToString("0", inv));
            foreach (var kv in _debts)
            {
                var d = kv.Value;
                sb.Append('|').Append(kv.Key).Append(';').Append(d.Principal.ToString("0.##", inv)).Append(';').Append(d.Rate.ToString("0.####", inv))
                  .Append(';').Append(d.DueDay).Append(';').Append(d.Missed).Append(';').Append(d.Trust.ToString("0.##", inv))
                  .Append(';').Append(d.Defaulted ? 1 : 0).Append(';').Append(d.Loans).Append(';').Append(d.EverDefaulted ? 1 : 0);
            }
            return sb.ToString();
        }

        internal static void Import(string data)
        {
            try
            {
                _debts.Clear(); _capital = -1;
                if (string.IsNullOrEmpty(data)) return;
                var inv = CultureInfo.InvariantCulture;
                foreach (var part in data.Split('|'))
                {
                    if (part.StartsWith("cap=")) { double c; if (double.TryParse(part.Substring(4), NumberStyles.Float, inv, out c)) _capital = c; continue; }
                    var f = part.Split(';');
                    if (f.Length < 9) continue;
                    var d = new Debt();
                    double.TryParse(f[1], NumberStyles.Float, inv, out d.Principal);
                    double.TryParse(f[2], NumberStyles.Float, inv, out d.Rate);
                    int.TryParse(f[3], out d.DueDay);
                    int.TryParse(f[4], out d.Missed);
                    float.TryParse(f[5], NumberStyles.Float, inv, out d.Trust);
                    d.Defaulted = f[6] == "1";
                    int.TryParse(f[7], out d.Loans);
                    d.EverDefaulted = f[8] == "1";
                    _debts[f[0]] = d;
                }
            }
            catch (Exception e) { Log.Error("IronBank.Import", e); }
        }
    }
}
