using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// PACZKA f20-d3 - ZYSK BANKU DO BRAAVOS (decyzja Jeffa 18.10 "3. a": "zysk Banku ponad jego zapas trafia do wlascicieli w Braavos. To jak w ksiazkach:
    /// Bankiem rzadza rody braawoskie, ktore potem wydaja te pieniadze w Braavos na towary, sluzbe i budowy. Zloto wraca do obiegu."). Klucz IronBankDividends (TAK).
    ///  - ZYSK ZREALIZOWANY = odsetki i oplaty NAPRAWDE wplacone, nie kapital. Kazda pozyczka ma koszt - zloto, ktore wyszlo z kapitalu (IronBank.Lend / LendKw,
    ///    kredyt korony 186 - CrownBorrow.Lend -> NoteCrownLent). Splata najpierw gasi odsetki (czesc dlugu ponad koszt), dopiero potem koszt: po kazdej dobie
    ///    koszt pozyczki = min(koszt, dlug). Zysk doby = wplywy do kapitalu (zmiana kapitalu od wczoraj + pozyczone od wczoraj) - zwrot kosztu (spadek kosztu).
    ///    Dlug, ktory znika bez zaplaty (rod wymarly bez wsi - "dlug bez platnika" 168, stary bankrut wymarly, krolestwo zniszczone 186), zdejmuje koszt bez
    ///    wplywu - strata: zysk doby ujemny, pula maleje (straty pokrywa zysk, zanim cokolwiek wyjdzie do Braavos). Dlug wymarlego rodu przechodzi na dziedzicow
    ///    razem z kosztem (MoveCost w DebtLadder.Inherit). Dlug sprzed klucza (start na starym zapisie): koszt = caly dzisiejszy dlug (ostroznie - narosle odsetki
    ///    starych pozyczek nie licza sie jako zysk). Reszta skarbca upadlego krolestwa bez odbiorcy (FallenCrown) to NIE zysk (ani odsetki, ani oplata) -
    ///    kapital wlasny Banku (NoteCapitalNoProfit: migawka podniesiona o te kwote, wplyw doby jej nie widzi; recenzja f20-d3 - bez jednorazowego przelewu
    ///    1-3 mln do Braavos). Linia doby: "zmiana kapitalu X - kapital wlasny Y + pozyczone Z".
    ///  - ZWROT NADWYZKI KAPITALU WLASNEGO (recenzja calosci noc-ks4 - bez tego zloto ponad kapital startowy zostawalo w Banku na zawsze): kapital wlasny = kapital
    ///    (skarbiec) + koszt pozyczek (rody i korony) - pula; nadwyzka ponad IronBankCapital idzie do puli po 1/BankProfitToBraavosDays dziennie (tempo dawnej
    ///    reguly 168) i wychodzi do Braavos ta sama wyplata (ta sama rezerwa, ci sami odbiorcy). Zrodla nadwyzki: reszta skarbca upadlego krolestwa bez odbiorcy
    ///    (wyzej) i dlug sprzed klucza liczony przy starcie po dzisiejszym dlugu (narosle odsetki). Pula rosnie o zwrot, wiec kapital wlasny maleje o niego - nadwyzka
    ///    schodzi geometrycznie do IronBankCapital; strata (koszt bez wplywu) zmniejsza i skarbiec+koszt, i pule - kapitalu wlasnego nie rusza (pokrywa ja zysk i zwrot).
    ///  - REZERWA = max(BankFreeCapitalFloor, CrownLoanBankFloor) (ponizej nich Bank nie daje kredytu wojennego / nie pozycza koronom) + wszystko, co Bank
    ///    pozyczyl (rodom i koronom) w ostatnich IronBankReserveLendDays dobach - tyle trzyma na kredyt nastepnego okresu.
    ///  - WYPLATA co IronBankDividendDays (28) dob: min(pula, kapital - rezerwa). Kapital nigdy nie schodzi ponizej rezerwy; kapital wlasny Banku do IronBankCapital
    ///    nie jest wyplacany (ponad - tylko przez zwrot nadwyzki wyzej). Gdy dzis nie da sie wyplacic (pula <= 0, kapital przy rezerwie) - pula czeka do nastepnej wyplaty.
    ///  - ODBIORCY: IronBankDividendTownShare (0.5) do miasta Braavos (town_EN5) jak wydatek dworu klucznikow (KCourt: czesc kies ludu - sluzba, reszta kasa
    ///    kupcow pod tarcza dworu - te same drogi co dwor pana w ClanBudget.Court, bez towaru), reszta do glow rodow krolestwa Braavos ("bravos") z miastem
    ///    albo zamkiem (klucznicy; wagi: miasto 2, zamek 1; bez najemnikow, rodow bez lenna, dworzan BK). Bez takich rodow - calosc do miasta, bez miasta -
    ///    calosc do rodow, bez obu - nic (pula czeka). Reszta z zaokraglen - ostatni rod w kolejnosci StringId.
    ///  - Zastepuje stara regule "kapital ponad IronBankCapital -> kasa Braavos po 1/BankProfitToBraavosDays dziennie" (DebtLadder.Daily krok 3) - jedna
    ///    regula na zjawisko (dywidenda: zysk zrealizowany + zwrot nadwyzki kapitalu wlasnego); klucz wylaczony = stara regula jak dotad (stan f20-d3 czyszczony,
    ///    przy ponownym wlaczeniu start od zera). Wyplata doby - IronBank.LastDividend (linia Obieg i linia "Dlugi (168)" obok "zysk do kasy Braavos").
    /// Platnik -> odbiorca: kapital Banku -> glowy rodow Braavos, kiesy ludu i kasa kupcow miasta Braavos; kapital zmniejszany o to, co odbiorca naprawde dostal
    /// (Bank -x = odbiorcy +x co do zlotowki). Wyplata glowie (nie graczowi) - wplyw jednorazowy w ksiedze dochodu (ClanIncomeBook.NoteInflow KRansom, jak okup). Rozliczenie na poczatku IronBank.Daily (przed drabina 168 i kredytem dnia 166).
    /// Linie "Bank Zelazny - zysk (f20-d3): dzien N" (co dobe) i "Bank Zelazny - wyplata do Braavos (f20-d3): dzien N" (w dobie wyplaty); checker --grupa nbank.
    /// Zapis "arm_bankdiv" (brak klucza = start od zera w pierwszej dobie). Wyjatki liczone (potkniecia w linii), bez globalnego wylacznika (CLAUDE.md 7).
    /// </summary>
    internal static partial class IronBank
    {
        private const string BraavosKingdomId = "bravos";   // krolestwo Braavos w ROT (CrownGifts.FreeCityIds; budzet-rodow.csv fin93: Antaryon i Celeris - miasta, Prestayn, Fregar, Volentin - zamki)
        private static readonly CultureInfo DvInv = CultureInfo.InvariantCulture;

        private static bool _dvInit;                                                               // stan zainicjowany (koszt pozyczek, migawka kapitalu)
        private static double _dvPool;                                                             // zysk zrealizowany niewyplacony (ujemny - straty do pokrycia)
        private static double _dvSnap;                                                             // kapital przy ostatnim rozliczeniu (po wyplacie)
        private static double _dvWinLent;                                                          // pozyczone z kapitalu od ostatniego rozliczenia (rody + korony)
        private static double _dvOwnIn;                                                            // wplyw do kapitalu poza zyskiem od ostatniego rozliczenia (kapital wlasny - FallenCrown bez odbiorcy)
        private static int _dvLastPay = -1, _dvInitDay = -1;                                       // doba ostatniej wyplaty (albo startu), doba startu
        private static double _dvProfitAll, _dvPaidAll, _dvRelAll;                                 // od startu (linia): zysk, wyplacone, zwrot nadwyzki kapitalu wlasnego
        /// <summary>Recenzja calosci noc-ks4: wyplacone do Braavos w tej dobie (klucznicy + miasto; 0 bez wyplaty i przy kluczu wylaczonym) - linia Obieg i "Dlugi (168)".</summary>
        internal static long LastDividend;
        private static readonly Dictionary<string, double> _dvCost = new Dictionary<string, double>();    // rod (StringId) -> koszt pozyczek (kapital niezwrocony)
        private static readonly Dictionary<string, double> _dvCostK = new Dictionary<string, double>();   // krolestwo (StringId) -> koszt kredytu korony
        private static readonly List<long> _dvRing = new List<long>();                             // pozyczone w kolejnych dobach (najnowsza na koncu)
        private static int _dvStumbles;
        private static readonly HashSet<string> _dvErr = new HashSet<string>();
        private static string _dvImportNote = "brak klucza";

        internal static bool DividendsOn { get { var s = Settings.Current; return s != null && s.IronBankEnabled && s.IronBankDividends; } }

        private static void DvReset() { DvClear(); _dvStumbles = 0; _dvErr.Clear(); _dvImportNote = "brak klucza"; }

        private static void DvClear()
        {
            _dvInit = false; _dvPool = 0; _dvSnap = 0; _dvWinLent = 0; _dvOwnIn = 0; _dvLastPay = -1; _dvInitDay = -1; _dvProfitAll = 0; _dvPaidAll = 0; _dvRelAll = 0;
            _dvCost.Clear(); _dvCostK.Clear(); _dvRing.Clear();
        }

        private static void DvStumble(string where, Exception e)
        {
            _dvStumbles++;
            try { if (_dvErr.Add(where)) Log.Error("IronBank.Dividends." + where, e); } catch { }
        }

        // ------------------------------------------------------------ wpiecia (nie rzucaja)
        /// <summary>Pozyczka rodu wyszla z kapitalu (Lend / LendKw): koszt pozyczki rodu i licznik okna.</summary>
        private static void DvLent(string id, double amount)
        {
            try
            {
                if (!_dvInit || id == null || amount <= 0) return;
                double c; _dvCost.TryGetValue(id, out c); _dvCost[id] = c + amount;
                _dvWinLent += amount;
            }
            catch (Exception e) { DvStumble("DvLent", e); }
        }

        /// <summary>CrownBorrow.Lend: kredyt korony wyszedl z kapitalu (skarbiec juz dostal) - koszt kredytu korony i licznik okna.</summary>
        internal static void NoteCrownLent(string kingdomId, long amount)
        {
            try
            {
                if (!_dvInit || kingdomId == null || amount <= 0) return;
                double c; _dvCostK.TryGetValue(kingdomId, out c); _dvCostK[kingdomId] = c + amount;
                _dvWinLent += amount;
            }
            catch (Exception e) { DvStumble("NoteCrownLent", e); }
        }

        /// <summary>272 (CrownBorrow.MoveLoan - unia Dorzecza z Polnoca): koszt kredytu korony `fromId` przechodzi na korone `toId` razem z dlugiem - inaczej DvClamp
        /// wzialby znikniecie dlugu `fromId` za zwrot kosztu, a dlug `toId` ponad koszt za zysk. Bez startu f20-d3 - nic. Nie rzuca.</summary>
        internal static void MoveCrownCost(string fromId, string toId)
        {
            try
            {
                if (!_dvInit || fromId == null || toId == null || fromId == toId) return;
                double c;
                if (!_dvCostK.TryGetValue(fromId, out c)) return;
                _dvCostK.Remove(fromId);
                double t; _dvCostK.TryGetValue(toId, out t); _dvCostK[toId] = t + c;
            }
            catch (Exception e) { DvStumble("MoveCrownCost", e); }
        }

        /// <summary>Recenzja f20-d3: wplyw do kapitalu, ktory NIE jest zyskiem (ani odsetki, ani oplata) - reszta skarbca upadlego krolestwa bez odbiorcy
        /// (FallenCrown, zaraz po IronBank.CapitalAdd). Migawka kapitalu rosnie o te kwote - wplyw doby (zmiana kapitalu od migawki) jej nie widzi; zostaje w Banku
        /// jako kapital wlasny (nie jako zysk; recenzja calosci noc-ks4: nadwyzka kapitalu wlasnego ponad IronBankCapital wraca do puli po 1/BankProfitToBraavosDays
        /// dziennie - DvDay krok 2b). Bez startu f20-d3 - nic (start bierze migawke z dzisiejszego kapitalu).</summary>
        internal static void NoteCapitalNoProfit(double amount)
        {
            try
            {
                if (!_dvInit || !(amount > 0)) return;
                _dvSnap += amount; _dvOwnIn += amount;
            }
            catch (Exception e) { DvStumble("NoteCapitalNoProfit", e); }
        }

        /// <summary>DebtLadder.Inherit: czesc dlugu wymarlego rodu (part z tego, co z niego jeszcze zostalo - left) przechodzi na dziedzica - koszt w tej samej
        /// proporcji (ostatni dziedzic: part = left - caly koszt). Bez tego dlug dziedzica bylby "bez kosztu" (splata = zysk), a znikniecie dlugu wymarlego - strata.</summary>
        internal static void MoveCost(string fromId, Clan to, double part, double left)
        {
            try
            {
                if (!_dvInit || fromId == null || to == null || to.StringId == null || part <= 0 || left <= 0 || to.StringId == fromId) return;
                double c;
                if (!_dvCost.TryGetValue(fromId, out c) || c <= 0) return;
                double m = part >= left ? c : c * part / left;
                _dvCost[fromId] = c - m;
                double t; _dvCost.TryGetValue(to.StringId, out t); _dvCost[to.StringId] = t + m;
            }
            catch (Exception e) { DvStumble("MoveCost", e); }
        }

        // ------------------------------------------------------------ doba
        private static long DvReserve(Settings s, out long floor, out long lentN)
        {
            floor = Math.Max(Math.Max(0, s.BankFreeCapitalFloor), Math.Max(0, s.CrownLoanBankFloor));
            lentN = 0;
            for (int i = 0; i < _dvRing.Count; i++) lentN += _dvRing[i];
            return floor + lentN;
        }

        /// <summary>Koszt pozyczek po dobie: min(koszt, dlug) dla kazdej pozyczki (dlug ponizej 1 = 0). Zwraca zwrot kosztu (spadek). Zmiany zbiera do list -
        /// wolajacy zatwierdza je dopiero po calym rozliczeniu (wyjatek w polowie nie zostawia polowy stanu).</summary>
        private static double DvClamp(Dictionary<string, double> cost, Func<string, double> debtOf, List<KeyValuePair<string, double>> set, List<string> drop)
        {
            double back = 0;
            foreach (var kv in cost)
            {
                double p = Math.Max(0, debtOf(kv.Key));
                if (p < 1) p = 0;
                if (kv.Value > p) back += Math.Max(0, kv.Value) - p;
                else if (p > 0) continue;                                    // dlug ponad koszt - bez zmian
                if (p <= 0) drop.Add(kv.Key); else set.Add(new KeyValuePair<string, double>(kv.Key, p));   // dlugu nie ma - wpis znika (koszt 0 po przeniesieniu na dziedzicow tez)
            }
            return back;
        }

        /// <summary>Poczatek IronBank.Daily (przed drabina 168): rozliczenie zysku od ostatniej doby i - co IronBankDividendDays - wyplata do Braavos. Nie rzuca.</summary>
        private static void DvDay(int today)
        {
            LastDividend = 0;   // recenzja calosci noc-ks4: wyplata tej doby (DvPay) - zerowana co dobe, takze przy kluczu wylaczonym
            try
            {
                var s = Settings.Current;
                if (s == null) return;
                if (!s.IronBankDividends)
                {
                    if (_dvInit) { DvClear(); Log.Info("Bank Zelazny - zysk (f20-d3): dzien " + today + " | klucz Iron Bank Dividends wylaczony - stan wyczyszczony, zysk Banku wedlug starej reguly (Bank Profit To Braavos Days).");
                    }
                    return;
                }
                EnsureCapital();
                var crown = new Dictionary<string, double>();
                if (!CrownBorrow.PrincipalsInto(crown)) { _dvStumbles++; return; }   // bez dlugow koron rozliczenie wzieloby je za straty - jutro dwie doby naraz
                TownDebt.BankInto(crown);   // m96-dlugi: dlugi rad miast w Banku (id "td:<miasto>") - koszt pozyczek jak kredyt korony (NoteCrownLent)
                if (!_dvInit)
                {
                    DvClear();
                    double sc = 0, sk = 0;
                    foreach (var kv in _debts) if (kv.Value != null && kv.Value.Principal >= 1) { _dvCost[kv.Key] = kv.Value.Principal; sc += kv.Value.Principal; }
                    foreach (var kv in crown) if (kv.Value >= 1) { _dvCostK[kv.Key] = kv.Value; sk += kv.Value; }
                    _dvSnap = _capital; _dvLastPay = today; _dvInitDay = today; _dvInit = true;
                    Log.Info("Bank Zelazny - zysk (f20-d3): start dzien " + today + " (" + _dvImportNote + ") | koszt pozyczek = dzisiejszy dlug (narosle odsetki starych pozyczek nie beda zyskiem): rody "
                             + (long)sc + " (" + _dvCost.Count + "), korony " + (long)sk + " (" + _dvCostK.Count + ") | pula 0, kapital " + (long)_capital
                             + " | wyplata co " + Math.Max(1, s.IronBankDividendDays) + " dob, pierwsza dzien " + (today + Math.Max(1, s.IronBankDividendDays))
                             + ", do miasta Braavos " + (Unit01(s.IronBankDividendTownShare) * 100).ToString("0", DvInv) + "%, reszta klucznikom.");
                    return;
                }
                // 1. koszt pozyczek: splata najpierw gasi odsetki, potem koszt
                var setC = new List<KeyValuePair<string, double>>(); var dropC = new List<string>();
                var setK = new List<KeyValuePair<string, double>>(); var dropK = new List<string>();
                double backC = DvClamp(_dvCost, delegate (string id) { Debt d; return _debts.TryGetValue(id, out d) && d != null ? d.Principal : 0; }, setC, dropC);
                double backK = DvClamp(_dvCostK, delegate (string id) { double p; return crown.TryGetValue(id, out p) ? p : 0; }, setK, dropK);
                // 2. wplywy i zysk doby
                double lent = _dvWinLent;
                double own = _dvOwnIn;                                               // recenzja f20-d3: kapital wlasny (juz w migawce - poza wplywem doby)
                double cashIn = (_capital - _dvSnap) + lent;
                double profit = cashIn - (backC + backK);
                // zatwierdzenie
                foreach (var kv in setC) _dvCost[kv.Key] = kv.Value;
                foreach (var id in dropC) _dvCost.Remove(id);
                foreach (var kv in setK) _dvCostK[kv.Key] = kv.Value;
                foreach (var id in dropK) _dvCostK.Remove(id);
                _dvPool += profit; _dvProfitAll += profit;
                _dvRing.Add((long)Math.Round(lent));
                int keep = Math.Max(1, Math.Min(364, s.IronBankReserveLendDays));
                while (_dvRing.Count > keep) _dvRing.RemoveAt(0);
                _dvWinLent = 0; _dvOwnIn = 0; _dvSnap = _capital;
                // 2b. recenzja calosci noc-ks4: zwrot nadwyzki kapitalu wlasnego - kapital wlasny = skarbiec + koszt pozyczek (po dzisiejszym rozliczeniu) - pula; nadwyzka
                // ponad IronBankCapital do puli po 1/BankProfitToBraavosDays dziennie (tempo dawnej reguly 168), dalej ta sama wyplata, rezerwa i odbiorcy. Wyplata nizej
                // zmniejsza skarbiec i pule o to samo - kapitalu wlasnego nie zmienia, a kosztow nie rusza, wiec sumy kosztow z tego miejsca sa tez sumami linii.
                double sumC = 0, sumK = 0;
                foreach (var v in _dvCost.Values) sumC += v;
                foreach (var v in _dvCostK.Values) sumK += v;
                double eq = _capital + sumC + sumK - _dvPool;
                double eqBase = Math.Max(0, s.IronBankCapital);
                float relDays = Math.Max(1f, s.BankProfitToBraavosDays);
                double rel = eq > eqBase ? (eq - eqBase) / relDays : 0;
                _dvPool += rel; _dvRelAll += rel;
                // 3. wyplata co IronBankDividendDays
                int every = Math.Max(1, s.IronBankDividendDays);
                long floor, lentN;
                long reserve = DvReserve(s, out floor, out lentN);
                string payLine = null;
                if (today - _dvLastPay >= every)
                {
                    payLine = DvPay(today, reserve, s);
                    _dvLastPay = today;
                }
                // linia doby
                double debtC = 0, debtK = 0;
                foreach (var kv in _debts) if (kv.Value != null && kv.Value.Principal >= 1) debtC += kv.Value.Principal;
                foreach (var v in crown.Values) if (v >= 1) debtK += v;
                var sb = new StringBuilder(700);
                sb.Append("Bank Zelazny - zysk (f20-d3): dzien ").Append(today)
                  .Append(" | wplywy ").Append(L(cashIn)).Append(" (zmiana kapitalu ").Append(L(cashIn - lent + own)).Append(" - kapital wlasny ").Append(L(own))
                  .Append(" + pozyczone ").Append(L(lent)).Append(")")
                  .Append(", zwrot kosztu pozyczek ").Append(L(backC + backK)).Append(" (rody ").Append(L(backC)).Append(", korony ").Append(L(backK)).Append(")")
                  .Append(", zysk zrealizowany dzis ").Append(L(profit))
                  .Append(", zwrot nadwyzki kapitalu wlasnego ").Append(L(rel)).Append(" (kapital wlasny ").Append(L(eq)).Append(" - Iron Bank Capital ").Append(L(eqBase))
                  .Append(", 1/").Append(relDays.ToString("0.##", DvInv)).Append(" dziennie)")
                  .Append(" | pula ").Append(L(_dvPool))
                  .Append(" | kapital ").Append(L(_capital)).Append(", rezerwa ").Append(reserve).Append(" (prog ").Append(floor).Append(" + pozyczone w ").Append(_dvRing.Count).Append(" dobach ").Append(lentN)
                  .Append("), ponad rezerwe ").Append(L(_capital - reserve))
                  .Append(" | koszt pozyczek: rody ").Append(L(sumC)).Append(" (").Append(_dvCost.Count).Append("), korony ").Append(L(sumK)).Append(" (").Append(_dvCostK.Count).Append(")")
                  .Append("; dlug: rody ").Append(L(debtC)).Append(", korony ").Append(L(debtK))
                  .Append(" | od startu (dzien ").Append(_dvInitDay).Append("): zysk ").Append(L(_dvProfitAll)).Append(", zwrot nadwyzki ").Append(L(_dvRelAll)).Append(", wyplacone ").Append(L(_dvPaidAll))
                  .Append(" | wyplata co ").Append(every).Append(" dob, nastepna dzien ").Append(_dvLastPay + every)
                  .Append(" | potkniecia ").Append(_dvStumbles).Append('.');
                Log.Info(sb.ToString());
                if (payLine != null) Log.Info(payLine);
            }
            catch (Exception e) { DvStumble("Day", e); }
        }

        private static long L(double v) { return (long)Math.Round(v); }
        private static double Unit01(float v) { return v < 0f ? 0.0 : (v > 1f ? 1.0 : v); }

        /// <summary>Klucznicy Braavos: glowy rodow krolestwa "bravos" z miastem albo zamkiem (waga: miasto 2, zamek 1), kolejnosc StringId.</summary>
        private static List<KeyValuePair<Clan, int>> DvKeyholders(out string kname)
        {
            var list = new List<KeyValuePair<Clan, int>>();
            kname = "brak krolestwa";
            Kingdom k = null;
            foreach (var x in Kingdom.All) if (x != null && x.StringId == BraavosKingdomId) { k = x; break; }
            if (k == null) return list;
            kname = (k.Name != null ? k.Name.ToString() : k.StringId) + (k.IsEliminated ? " (zniszczone)" : "");
            if (k.IsEliminated) return list;
            foreach (var c in k.Clans)
            {
                if (c == null || c.IsEliminated || c.IsBanditFaction || c.IsMinorFaction || c.IsUnderMercenaryService || c.StringId == null) continue;
                var h = c.Leader;
                if (h == null || !h.IsAlive) continue;
                int w = 0;
                foreach (var f in c.Fiefs) { if (f == null) continue; if (f.IsTown) w += 2; else if (f.IsCastle) w += 1; }
                if (w > 0) list.Add(new KeyValuePair<Clan, int>(c, w));
            }
            list.Sort(delegate (KeyValuePair<Clan, int> a, KeyValuePair<Clan, int> b) { return string.CompareOrdinal(a.Key.StringId, b.Key.StringId); });
            return list;
        }

        /// <summary>Wyplata: min(pula, kapital - rezerwa) do klucznikow i miasta Braavos. Kapital i migawka zmniejszane o to, co odbiorca naprawde dostal
        /// (po kazdym przelewie - wyjatek w polowie nie psuje rachunku jutra). Zwraca linie wyplaty.</summary>
        private static string DvPay(int today, long reserve, Settings s)
        {
            double pool0 = _dvPool, cap0 = _capital, room = _capital - reserve;
            var sb = new StringBuilder(600);
            sb.Append("Bank Zelazny - wyplata do Braavos (f20-d3): dzien ").Append(today)
              .Append(" | pula przed ").Append(L(pool0)).Append(", ponad rezerwe ").Append(L(room)).Append(" (kapital ").Append(L(cap0)).Append(", rezerwa ").Append(reserve).Append(")");
            long x = (long)Math.Floor(Math.Min(pool0, room));
            if (x > int.MaxValue) x = int.MaxValue;
            string kname;
            var keys = DvKeyholders(out kname);
            Settlement br = null;
            try { br = Settlement.Find(BraavosId); } catch { br = null; }
            bool townOk = br != null && br.IsTown && br.Town != null;
            if (x < 1)
            {
                sb.Append(" -> do wyplaty 0 (").Append(pool0 < 1 ? "pula pusta albo straty do pokrycia" : "kapital przy rezerwie").Append(") | wyplacone 0")
                  .Append(" | pula po ").Append(L(_dvPool)).Append(", kapital po ").Append(L(_capital)).Append(" - pula czeka do nastepnej wyplaty.");
                return sb.ToString();
            }
            long xTown, xKeys;
            if (keys.Count == 0) { xTown = townOk ? x : 0; xKeys = 0; }
            else if (!townOk) { xTown = 0; xKeys = x; }
            else { xTown = (long)Math.Floor(x * Unit01(s.IronBankDividendTownShare)); xKeys = x - xTown; }
            sb.Append(" -> do wyplaty ").Append(x);
            if (xTown + xKeys <= 0)
            {
                sb.Append(" | brak odbiorcow (krolestwo ").Append(kname).Append(" bez rodow z lennem, miasto Braavos ").Append(townOk ? "jest" : "brak").Append(") | wyplacone 0 | pula po ")
                  .Append(L(_dvPool)).Append(" - czeka.");
                return sb.ToString();
            }
            // klucznicy
            long paidKeys = 0, given = 0, noted = 0; int wsum = 0;
            foreach (var kv in keys) wsum += kv.Value;
            var parts = new List<string>();
            for (int i = 0; i < keys.Count && xKeys > 0; i++)
            {
                try
                {
                    var c = keys[i].Key;
                    long a = i == keys.Count - 1 ? xKeys - given : xKeys * keys[i].Value / Math.Max(1, wsum);
                    given += a;
                    if (a <= 0) continue;
                    var h = c.Leader;
                    int before = h.Gold;
                    h.ChangeHeroGold((int)Math.Min(int.MaxValue, a));
                    long got = (long)h.Gold - before;
                    if (got <= 0) continue;
                    _capital -= got; _dvSnap -= got; _dvPool -= got; _dvPaidAll += got; paidKeys += got;
                    CirculationWindows.NoteHeroGold(h, got);   // glowa poza swiatem - tylko licznik (wyplacamy tylko zywym)
                    // recenzja f20-d3: jednorazowy wplyw od prawdziwego platnika - jak okup, skarbiec upadlego krolestwa (FallenCrown), zajecie (DebtLadder):
                    // ksiega dochodu widzi go w OnceToday (168: splata kredytu wojennego z jednorazowych, limit KW), D staly nie (QOnce poza D - Braavos nie jest najezdzca)
                    bool pl = h == Hero.MainHero;
                    if (!pl && ClanIncomeBook.On) { ClanIncomeBook.NoteInflow(h, (int)Math.Min(int.MaxValue, got), ClanIncomeBook.KRansom); noted += got; }
                    parts.Add((c.Name != null ? c.Name.ToString() : c.StringId) + " " + got + " (waga " + keys[i].Value + (pl ? ", gracz" : "") + ")");
                    if (pl)
                        Log.Player("The keyholders of the Iron Bank share out the Bank's profit: " + got.ToString("N0", DvInv) + " denars come to you from Braavos.");
                }
                catch (Exception e) { DvStumble("Pay(rod)", e); }
            }
            // miasto Braavos - jak dwor klucznikow: czesc kies ludu (sluzba), reszta kasa kupcow pod tarcza dworu
            long folk = 0, coffer = 0;
            if (xTown > 0)
            {
                try
                {
                    int ix = (int)Math.Min(int.MaxValue, xTown);
                    int f = TownFolk.Earn(br, (int)(ix * (double)CourtFull.FolkShare), TownFolk.KCourt);
                    if (f > 0) { folk = f; _capital -= f; _dvSnap -= f; _dvPool -= f; _dvPaidAll += f; }
                    int g0 = br.Town.Gold;
                    br.Town.ChangeGold(ix - f);
                    coffer = Math.Max(0, (long)br.Town.Gold - g0);
                    if (coffer > 0) { _capital -= coffer; _dvSnap -= coffer; _dvPool -= coffer; _dvPaidAll += coffer; }
                    try { TownFolk.Note(br, (int)Math.Min(int.MaxValue, folk + coffer), TownFolk.KCourt); } catch { }   // E5/0: wydatek dworu w osadzie (tylko licznik)
                    if (coffer > 0) SoldierPay.HoldCourt(br, (int)coffer);   // tarcza dworu: regulator gry nie kasuje tej wplaty (przy TownPurse - zbedna, nic nie robi)
                }
                catch (Exception e) { DvStumble("Pay(miasto)", e); }
            }
            long paid = paidKeys + folk + coffer;
            LastDividend += paid;   // recenzja calosci noc-ks4: linia Obieg i "Dlugi (168)"
            double dcap = cap0 - _capital;
            sb.Append(" | wyplacone ").Append(paid).Append(": klucznicy ").Append(paidKeys).Append(" (krolestwo ").Append(kname).Append(", rodow ").Append(parts.Count)
              .Append(parts.Count > 0 ? ": " + string.Join(", ", parts.ToArray()) : "").Append("; w ksiedze dochodu jako jednorazowe ").Append(noted).Append(")")
              .Append(", miasto Braavos ").Append(folk + coffer).Append(" (kiesy ludu ").Append(folk).Append(", kasa kupcow ").Append(coffer).Append(townOk ? "" : "; miasta brak").Append(")")
              .Append(" | kontrola: kapital ").Append(L(cap0)).Append(" -> ").Append(L(_capital)).Append(" (Bank -").Append(L(dcap)).Append("), odbiorcy +").Append(paid)
              .Append(", roznica ").Append(L(dcap) - paid)
              .Append(" | pula po ").Append(L(_dvPool)).Append(", kapital po ").Append(L(_capital)).Append(_capital >= reserve ? " >= " : " < ").Append("rezerwa ").Append(reserve).Append('.');
            return sb.ToString();
        }

        // ------------------------------------------------------------ zapis "arm_bankdiv"
        internal static string DvExport()
        {
            try
            {
                if (!_dvInit) return "";
                var sb = new StringBuilder(256 + 40 * (_dvCost.Count + _dvCostK.Count) + 12 * _dvRing.Count);
                sb.Append("1|").Append(_dvPool.ToString("R", DvInv)).Append(';').Append(_dvSnap.ToString("R", DvInv)).Append(';').Append(_dvWinLent.ToString("R", DvInv))
                  .Append(';').Append(_dvLastPay).Append(';').Append(_dvInitDay).Append(';').Append(_dvProfitAll.ToString("R", DvInv)).Append(';').Append(_dvPaidAll.ToString("R", DvInv))
                  .Append(';').Append(_dvOwnIn.ToString("R", DvInv))    // recenzja f20-d3: pole 8 (kapital wlasny od rozliczenia - tylko linia doby; starsza DLL go nie czyta)
                  .Append(';').Append(_dvRelAll.ToString("R", DvInv));  // recenzja calosci noc-ks4: pole 9 (zwrot nadwyzki od startu - tylko linia; starsza DLL go nie czyta)
                sb.Append("|r");
                for (int i = 0; i < _dvRing.Count; i++) sb.Append(i == 0 ? ":" : ",").Append(_dvRing[i]);
                sb.Append("|c");
                bool first = true;
                foreach (var kv in _dvCost) { sb.Append(first ? ":" : ";").Append(kv.Key).Append('=').Append(kv.Value.ToString("R", DvInv)); first = false; }
                sb.Append("|k");
                first = true;
                foreach (var kv in _dvCostK) { sb.Append(first ? ":" : ";").Append(kv.Key).Append('=').Append(kv.Value.ToString("R", DvInv)); first = false; }
                return sb.ToString();
            }
            catch (Exception e) { DvStumble("Export", e); return ""; }
        }

        internal static void DvImport(string data)
        {
            DvClear();
            if (string.IsNullOrEmpty(data)) { _dvImportNote = "brak klucza arm_bankdiv - start od zera"; return; }
            try
            {
                var sec = data.Split('|');
                if (sec.Length < 5 || sec[0] != "1") throw new FormatException("naglowek");
                var h = sec[1].Split(';');
                if (h.Length < 7) throw new FormatException("pola");
                _dvPool = double.Parse(h[0], NumberStyles.Float, DvInv);
                _dvSnap = double.Parse(h[1], NumberStyles.Float, DvInv);
                _dvWinLent = double.Parse(h[2], NumberStyles.Float, DvInv);
                _dvLastPay = int.Parse(h[3], DvInv);
                _dvInitDay = int.Parse(h[4], DvInv);
                _dvProfitAll = double.Parse(h[5], NumberStyles.Float, DvInv);
                _dvPaidAll = double.Parse(h[6], NumberStyles.Float, DvInv);
                if (h.Length >= 8) _dvOwnIn = double.Parse(h[7], NumberStyles.Float, DvInv);   // recenzja f20-d3: zapis sprzed poprawki - 7 pol, kapital wlasny 0
                if (h.Length >= 9) _dvRelAll = double.Parse(h[8], NumberStyles.Float, DvInv);  // recenzja calosci noc-ks4: zapis sprzed poprawki - 8 pol, zwrot od startu 0
                if (sec[2].Length > 2 && sec[2].StartsWith("r:"))
                    foreach (var v in sec[2].Substring(2).Split(',')) _dvRing.Add(long.Parse(v, DvInv));
                DvReadCosts(sec[3], "c", _dvCost);
                DvReadCosts(sec[4], "k", _dvCostK);
                _dvInit = true;
                _dvImportNote = "z zapisu arm_bankdiv: rody " + _dvCost.Count + ", korony " + _dvCostK.Count + ", pula " + L(_dvPool);
            }
            catch (Exception e)
            {
                DvClear();
                _dvImportNote = "klucz arm_bankdiv nieczytelny - start od zera";
                DvStumble("Import", e);
            }
        }

        private static void DvReadCosts(string sec, string tag, Dictionary<string, double> into)
        {
            if (sec == null || !sec.StartsWith(tag)) throw new FormatException("sekcja " + tag);
            if (sec.Length <= tag.Length + 1) return;
            foreach (var rec in sec.Substring(tag.Length + 1).Split(';'))
            {
                int eq = rec.LastIndexOf('=');
                if (eq <= 0) throw new FormatException("rekord " + tag);
                into[rec.Substring(0, eq)] = double.Parse(rec.Substring(eq + 1), NumberStyles.Float, DvInv);
            }
        }
    }
}
