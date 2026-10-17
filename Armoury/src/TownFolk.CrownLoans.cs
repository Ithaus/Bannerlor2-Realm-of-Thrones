using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// korona2 (Jeff 15.10 "korony pozyczaja od miast czemu nie" - 'Tak, dodaj'; zwrotu zoldu z korony do rodow NIE ruszamy) - POZYCZKI KORON OD MIAST I MIESZCZAN
    /// (klucz CrownTownLoans) i CZESC KORONY PODATKU OD NADWYZKI DO ZAPASU KORONY (klucz FolkGuardCrownToReserve, TownFolk.Guard.cs).
    ///  - DOWOD (test D, log 2026-10-15_01-54-07, doby 108867-109031 = 164 doby; linie "Korona: wplywy dnia (165)", "Straznik obiegu (obieg2)", "Korona: podatek
    ///    wojenny ludu (184)"): skarbce koron 54.5 -> 36.2 mln (24.8 -> 16.4% zlota swiata), srednio -112 tys./d = 1/360 zapasu 82.6 tys. (165: wydawane tego samego
    ///    dnia - zwrot zoldu, potem renty 180 biora cala reszte) + W5b ze skarbca 6.8 tys. + wydatki spoza 165 (ujemne "inne") 26.3 tys. - zostalo 4.4 tys.
    ///    Wplywy dnia 616 tys./d: nasze 405 tys. (w tym danina P1 86 tys., 184 5 tys.), inne 185 tys. (w tym czesc korony straznika 129.5 tys.). Wydane: zwrot
    ///    zoldu 328 tys., renty 180 317 tys., dary 23 tys., kontrakty 14 tys., W5 11 tys., reparacje 5 tys., rata 186 2 tys. Czesc korony podatku od nadwyzki
    ///    wplywa w dobie ludu (TownFolk.Daily przed CrownIncome.Begin), wiec 165 liczy ja jako "inne wplywy dnia" i tego samego dnia oddaje w rentach 180
    ///    ("inne": bez straznika 42 tys./d, test C 75 tys. przy czesci korony 28 tys., test D 185 tys. przy 129.5 tys.) - wieksza czesc korony nie zatrzymuje
    ///    spadku skarbcow, tylko podnosi lordow (36.9 -> 40.2%). Na koniec testu D 8 krolestw w wojnie mialo skarbiec ponizej zapasu wojny (luka 2.25 mln).
    ///  - (A) CZESC KORONY DO ZAPASU (TownFolk.Guard.cs, GuardCrownReserve): czesc korony podatku od nadwyzki przesuwa migawke 165 (CrownIncome.ShiftSnapshot) - jest
    ///    zapasem, nie wplywem dnia (tak zamierzal obieg2); korona oddaje ja jak caly zapas (1/360 na dobe). Rownowaga skarbcow przy czesci 35% i podstawie podatku
    ///    testu D (370 tys./d): 14.5 mln + 360 x (129.5 - 6.8 - 26.3 + 4.4 tys.) = ok. 51 mln = 23% zlota swiata; ponizej celu 22% czesc zmienna (972e6270) przyspiesza.
    ///  - (B) POZYCZKI KORON (CrownTownLoans, czynne z 165): krolestwo w wojnie (z innym krolestwem), ktorego skarbiec po dniu korony (po CrownIncome.End) jest
    ///    ponizej ZAPASU WOJNY (KingdomTreasury.WarReserveFor - ta sama liczba co 184 / W5b / L5b-1: wieksze z 500 000 i 60 dob sredniego naleznego zwrotu zoldu),
    ///    pozycza brak z MIAST SWOJEGO KROLESTWA: kasa miasta ponad zapas kupcow (TownPurse.Spare) i kiesy mieszczan W / R / P ponad poziom normalny
    ///    (FolkHoardNormalTimes x zapas dni klasy - ten sam co podatek od nadwyzki); z kazdego zrodla najwyzej polowa nadwyzki na dobe, najpierw najwieksze;
    ///    bez miast oblezonych i miast rodu gracza. LIMIT: CrownTownLoanLimitDays (90) x sredni nasz wplyw dnia 165 (84 doby, od 7. doby pomiaru) + 50 000
    ///    za miasto krolestwa. ZLOTO POZYCZONE TO ZAPAS TRZYMANY: migawka 165 przesunieta (nie wplyw jutra), poza 1/360 zapasu (CrownIncome.Begin), poza
    ///    nadwyzka W5b i poza skarbcem stolicy L5b-1 (jak dlug w Banku 186).
    ///  - CENA I SPLATA: CrownTownLoanRateYear (10%) rocznie, doliczane codziennie. Krok splat zaraz po racie Banku (186), przed darami 182: w wojnie odsetki,
    ///    w pokoju odsetki + 1/182 najwiekszego dlugu (najwyzej 30% do wydania dnia) - z wplywow dnia (CrownIncome.Spent; mniej na renty 180); zawsze (wojna
    ///    i pokoj) 1/30 na dobe wolnego zlota korony ponad reszte wplywow dnia, zapas wojny, dlug u miast i dlug w Banku. Wplata do wierzycieli wedlug stanu
    ///    czesci dlugu (kasa miasta albo kiesa klasy; kiesa nieczynna - kasa).
    ///  - NIESPLATA: w pokoju CrownTownLoanDefaultDays (90) dob z rzedu bez pelnej raty - reszta przepada, kazde miasto-wierzyciel traci 10 lojalnosci, korona
    ///    bez pozyczek od miast przez 182 doby. Krolestwo zniszczone - dlug przepada (licznik).
    ///  - Nic z niczego: pozyczka i splata to przelewy kasa miasta / kiesa ludu <-> skarbiec; umorzenie nie rusza zlota. Ksiega kies: _ctlOut / _ctlIn i
    ///    LDayOut / LDayIn (kiesa-ludu.csv); "Obieg" (korona): pozyczki i splaty. Zapis "arm_korona2". Linie "Pozyczki koron (korona2): start sesji" i "...: dzien N".
    ///  - Klucze wylaczone = 972e6270 co do monety (CrownTownDebt = 0; dlug stoi w zapisie, bez odsetek i rat).
    /// Wyjatek per krolestwo / zrodlo - licznik potkniec (CLAUDE.md 7), przelew oddany zrodlu.
    /// </summary>
    internal static partial class TownFolk
    {
        private sealed class KlPart { public string St = ""; public int Kind; public double Left; }   // Kind: 0 kasa miasta, 1 kiesa W, 2 kiesa R, 3 kiesa P
        private sealed class KlDebtK { public readonly List<KlPart> Parts = new List<KlPart>(); public int Missed; public double Lent, Repaid, Interest, Peak; public long DLent, DPaid; }
        private sealed class KlInc { public double Avg; public int N; }
        private sealed class KlSrc { public Town T; public Folk F; public int Kind; public double Ex; }

        private static readonly Dictionary<string, KlDebtK> _kl = new Dictionary<string, KlDebtK>();     // id krolestwa -> dlug u miast i mieszczan
        private static readonly Dictionary<string, int> _klBan = new Dictionary<string, int>();          // id krolestwa -> doba konca zakazu po niesplacie
        private static readonly Dictionary<string, KlInc> _klInc = new Dictionary<string, KlInc>();      // id krolestwa -> sredni nasz wplyw dnia 165 (84 doby)
        private static readonly CultureInfo KlInv = CultureInfo.InvariantCulture;
        private const int KlPerTown = 50000, KlBanDays = 182, KlRepayDays = 182, KlIncDays = 84, KlIncWarmup = 7, KlSurplusDays = 30;
        private const double KlSrcShare = 0.5, KlIncomeShare = 0.30;
        private const float KlDefaultLoyalty = 10f;
        private static bool _klStartDone;
        private static double _klDebtPrev = double.NaN;
        // ---- okno linii (od linii do linii: splaty po CrownIncome.Begin, pozyczki po CrownIncome.End)
        private static long _klLent, _klLentKasa, _klLentW, _klLentR, _klLentP, _klPaidInc, _klPaidStock, _klPaidKasa, _klPaidPurse, _klWritten, _klLost;
        private static double _klInt, _klWrittenD, _klLostD, _klDust, _klGap;
        private static int _klLentN, _klWar, _klCand, _klBlkBan, _klBlkWarm, _klBlkLimit, _klBlkNoSrc, _klMissedN, _klDefaultN, _klLoyN, _klLostN, _klAbove, _klStumbles, _klStumbleSess;
        /// <summary>"Obieg" (korona): pozyczki koron od miast tej doby (kasy miast i kiesy mieszczan -> skarbce) i splaty (skarbce -> kasy miast i kiesy mieszczan).</summary>
        internal static long LastCrownLoanLent, LastCrownLoanRepaid;

        private static bool CrownLoansOn { get { var s = Settings.Current; return s != null && s.CrownTownLoans && CrownIncome.On; } }
        private static double KlRate(Settings s) { return HdClamp(s.CrownTownLoanRateYear, 0, 0.5); }
        private static double KlLimitDays(Settings s) { return HdClamp(s.CrownTownLoanLimitDays, 0, 364); }
        private static int KlDefault(Settings s) { return Math.Max(7, Math.Min(364, s.CrownTownLoanDefaultDays)); }

        private static void ResetCrownLoans()
        {
            _kl.Clear(); _klBan.Clear(); _klInc.Clear();
            _klStartDone = false; _klDebtPrev = double.NaN; _klStumbleSess = 0;
            ResetAnt();   // DS F3 (L1): zaliczki mieszczan pod nadanie (TownFolk.CrownLoans.Ds.cs) - zapis w tym samym kluczu
            KlDayZero();
            LastCrownLoanLent = LastCrownLoanRepaid = 0;
        }

        private static void KlDayZero()
        {
            _klLent = _klLentKasa = _klLentW = _klLentR = _klLentP = _klPaidInc = _klPaidStock = _klPaidKasa = _klPaidPurse = _klWritten = _klLost = 0;
            _klInt = _klWrittenD = _klLostD = _klDust = _klGap = 0;
            _klLentN = _klWar = _klCand = _klBlkBan = _klBlkWarm = _klBlkLimit = _klBlkNoSrc = _klMissedN = _klDefaultN = _klLoyN = _klLostN = _klAbove = _klStumbles = 0;
            _klFromAnt = 0;   // DS F3 (L1): dlug przeniesiony z zaliczek po kwartale
            foreach (var kv in _kl) { kv.Value.DLent = 0; kv.Value.DPaid = 0; }
        }

        private static void KlStumble(string where, Exception e) { _klStumbles++; _klStumbleSess++; Stumble("CrownLoans." + where, e); }

        private static double KlDebt(KlDebtK x) { double t = 0; if (x != null) foreach (var p in x.Parts) t += Math.Max(0, p.Left); return t; }

        private static Kingdom KlKingdom(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var k in Kingdom.All) if (k != null && k.StringId == id) return k;
            return null;
        }

        /// <summary>Krolestwo w wojnie z innym (niezniszczonym) krolestwem - bandy i rody bez krolestwa sie nie licza.</summary>
        private static bool KlAtWar(Kingdom k)
        {
            try
            {
                var list = k != null ? k.FactionsAtWarWith : null;
                if (list != null) for (int i = 0; i < list.Count; i++) { var o = list[i]; if (o != null && o.IsKingdomFaction && !o.IsEliminated) return true; }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// korona2: dlug korony u miast i mieszczan (zaokraglony w gore) - zloto pozyczone nie jest zapasem korony: 165 (1/360 zapasu), W5b (nadwyzka ponad zapas
        /// wojny), L5b-1 (skarbiec stolicy). Klucz wylaczony - 0 (stan sprzed paczki co do monety; dlug stoi w zapisie). Nie rzuca.
        /// </summary>
        internal static long CrownTownDebt(Kingdom k)
        {
            try
            {
                if (k == null || k.StringId == null || (_kl.Count == 0 && _kla.Count == 0) || !CrownLoansOn) return 0;
                KlDebtK x, a;
                double t = (_kl.TryGetValue(k.StringId, out x) ? KlDebt(x) : 0) + (_kla.TryGetValue(k.StringId, out a) ? KlDebt(a) : 0);   // DS F3 (L1): zaliczki mieszczan to tez dlug u miast
                return t > 0 ? (long)Math.Ceiling(t - 1e-9) : 0;
            }
            catch { return 0; }
        }

        /// <summary>Poziom normalny kiesy klasy c (0 W, 1 R, 2 P) miasta - ten sam co podatek od nadwyzki i straznik obiegu. NaN - potrzeba nieznana.</summary>
        private static double KlNormal(Folk f, int c, Settings s)
        {
            double need = f.Need[c];
            if (!(need > 0)) return double.NaN;
            double rd = c == 0 ? Math.Max(1, Math.Min(60, s.TownFolkReserveDays)) : c == 1 ? RdR : RdP;
            return HoardTimes(s) * rd * need;
        }

        /// <summary>
        /// Zrodla pozyczki korony: miasta krolestwa (bez oblezonych i bez miast rodu gracza) - kasa ponad zapas kupcow i kiesy W / R / P ponad poziom normalny;
        /// z kazdego najwyzej KlSrcShare (polowa) nadwyzki na dobe. Zwraca sume; `towns` - liczba miast krolestwa (limit).
        /// </summary>
        private static double KlSources(Kingdom k, List<KlSrc> into, out int towns)
        {
            towns = 0;
            double sum = 0;
            var s = Settings.Current;
            var fiefs = k.Fiefs;
            if (fiefs == null) return 0;
            bool live = Live;
            for (int i = 0; i < fiefs.Count; i++)
            {
                var t = fiefs[i];
                var st = t != null ? t.Settlement : null;
                if (st == null || !st.IsTown) continue;
                towns++;
                if (st.IsUnderSiege || (Clan.PlayerClan != null && st.OwnerClan == Clan.PlayerClan)) continue;
                double ex = KlSrcShare * TownPurse.Spare(t);
                if (ex >= 1) { sum += ex; into.Add(new KlSrc { T = t, Kind = 0, Ex = ex }); }
                Folk f;
                if (!live || !_f.TryGetValue(st, out f) || f == null || f.Kind != 0) continue;
                for (int c = 0; c < 3; c++)
                {
                    double nl = KlNormal(f, c, s);
                    if (double.IsNaN(nl)) continue;
                    double e2 = KlSrcShare * (PurseOf(f, c) - nl);
                    if (e2 >= 1) { sum += e2; into.Add(new KlSrc { T = t, F = f, Kind = 1 + c, Ex = e2 }); }
                }
            }
            return sum;
        }

        /// <summary>Zdjecie ze zrodla (kasa miasta albo kiesa klasy; ksiega kies jak W7 LevyTake). Zwraca zdjete.</summary>
        private static long KlTake(KlSrc x, long amount)
        {
            try
            {
                if (amount <= 0) return 0;
                if (x.Kind == 0)
                {
                    int had = x.T.Gold;
                    int want = (int)Math.Min(amount, Math.Max(0, had));
                    if (want <= 0) return 0;
                    x.T.ChangeGold(-want);
                    return Math.Max(0, had - x.T.Gold);
                }
                int c = x.Kind - 1;
                long t = Math.Min(amount, Math.Max(0, PurseOf(x.F, c)));
                if (t <= 0) return 0;
                AddPurse(x.F, c, -t); _ctlOut += t; x.F.LDayOut += t;
                return t;
            }
            catch (Exception e) { KlStumble("Take", e); return 0; }
        }

        /// <summary>
        /// Wplata do wierzyciela: kiesa klasy (1-3; kiesa nieczynna albo miasto bez ludu - kasa miasta) albo kasa miasta (0). repay=false - zwrot zdjetego po wyjatku
        /// (ksiega kies z powrotem). Zwraca 1 - kiesa, 2 - kasa, 0 - nigdzie (wolajacy oddaje zloto skarbcowi - nic w nicosc).
        /// </summary>
        private static int KlPut(Settlement st, int kind, long amount, bool repay)
        {
            if (st == null || st.Town == null || amount <= 0) return 0;
            if (kind >= 1 && kind <= 3 && Live)
            {
                Folk f;
                if (_f.TryGetValue(st, out f) && f != null && f.Kind == 0)
                {
                    AddPurse(f, kind - 1, amount);
                    if (repay) { _ctlIn += amount; f.LDayIn += amount; _klPaidPurse += amount; }
                    else { _ctlOut -= amount; f.LDayOut -= amount; }
                    return 1;
                }
            }
            st.Town.ChangeGold((int)Math.Min(int.MaxValue, amount));
            if (repay) _klPaidKasa += amount;
            return 2;
        }

        /// <summary>Splata `amount` ze skarbca do wierzycieli wedlug stanu czesci dlugu (najwiekszy ulamek dostaje reszte); czego nie ma gdzie wplacic - wraca do skarbca.
        /// Zwraca wplacone wierzycielom.</summary>
        private static long KlRepay(Kingdom k, KlDebtK x, long amount)
        {
            if (amount <= 0 || x.Parts.Count == 0) return 0;
            double total = KlDebt(x);
            int had = k.KingdomBudgetWallet;
            int ip = (int)Math.Min(Math.Min(amount, (long)Math.Ceiling(total)), Math.Max(0, had));
            if (ip <= 0) return 0;
            k.KingdomBudgetWallet -= ip;   // najpierw skarbiec, potem wierzyciele: wierzyciele dostaja dokladnie to, co zeszlo
            long took = ip, left = took, placed = 0;
            var share = new long[x.Parts.Count];
            for (int i = 0; i < x.Parts.Count; i++)
            {
                double q = total > 0 ? Math.Max(0, x.Parts[i].Left) / total : 1.0 / x.Parts.Count;
                share[i] = Math.Min(left, (long)Math.Floor(took * q));
                left -= share[i];
            }
            for (int i = 0; left > 0 && i < x.Parts.Count; i++) { long a = Math.Min(left, Math.Max(0, (long)Math.Ceiling(x.Parts[i].Left) - share[i])); share[i] += a; left -= a; }
            if (left > 0) share[0] += left;
            for (int i = 0; i < x.Parts.Count; i++)
            {
                long a = share[i];
                if (a <= 0) continue;
                var p = x.Parts[i];
                int r = 0;
                try { r = KlPut(Settlement.Find(p.St), p.Kind, a, true); }
                catch (Exception e) { KlStumble("Repay(wplata)", e); r = 0; }
                if (r == 0) continue;
                p.Left -= a; x.Repaid += a; placed += a;
            }
            if (placed < took) k.KingdomBudgetWallet += (int)(took - placed);   // nic w nicosc: niewplacone wraca do skarbca, dlug tej czesci bez zmian
            return placed;
        }

        /// <summary>
        /// korona2 - KROK SPLAT (ArmouryBehavior: zaraz po CrownBorrow.Instalments, przed darami 182; dzien korony otwarty - CrownIncome.DayOf czynne): sredni nasz
        /// wplyw dnia, odsetki, rata z wplywow dnia (wojna - odsetki; pokoj - odsetki + 1/182 najwiekszego dlugu, najwyzej 30% do wydania), splata z wolnego zlota
        /// korony ponad zapas wojny i dlugi (1/30 na dobe), niesplata w pokoju, krolestwa zniszczone. Klucz wylaczony - nic (dlug zamrozony). Nie rzuca.
        /// </summary>
        internal static void CrownLoansPay()
        {
            var s = Settings.Current;
            if (s == null || Campaign.Current == null || !CrownLoansOn) return;
            try
            {
                int today = (int)CampaignTime.Now.ToDays;
                foreach (var k in Kingdom.All)
                {
                    try
                    {
                        if (k == null || k.IsEliminated || k.StringId == null) continue;
                        var d = CrownIncome.DayOf(k);
                        if (d == null) continue;
                        KlInc a;
                        if (!_klInc.TryGetValue(k.StringId, out a)) { a = new KlInc(); _klInc[k.StringId] = a; }
                        a.N = Math.Min(KlIncDays, a.N + 1);
                        a.Avg += (Math.Max(0, d.Own) - a.Avg) / a.N;
                    }
                    catch (Exception e) { KlStumble("Pay(srednia)", e); }
                }
                if (_klInc.Count > 0)
                {
                    var gone = new List<string>();
                    foreach (var kv in _klInc) { if (_kl.ContainsKey(kv.Key)) continue; var k = KlKingdom(kv.Key); if (k == null || k.IsEliminated) gone.Add(kv.Key); }
                    foreach (var id in gone) _klInc.Remove(id);
                }
                if (_klBan.Count > 0)
                {
                    var old = new List<string>();
                    foreach (var kv in _klBan) if (kv.Value <= today) old.Add(kv.Key);
                    foreach (var id in old) _klBan.Remove(id);
                }
                // DS F3 (L1): zaliczki mieszczan pod nadanie - odsetki i splata PIERWSZA z rat nadania wplaconych dzis (dziesiata + W4), przed zwyklym dlugiem korona2
                try { AntRepay(s); } catch (Exception e) { KlStumble("Pay(zaliczki)", e); }
                double rate = KlRate(s) / YearDays;
                int dflt = KlDefault(s);
                var drop = new List<string>();
                foreach (var kv in _kl)
                {
                    var x = kv.Value;
                    try
                    {
                        var k = KlKingdom(kv.Key);
                        double total = KlDebt(x);
                        if (k == null || k.IsEliminated)
                        {
                            // krolestwa nie ma - dlug przepada (zloto dawno w skarbcu i dalej w obiegu: nic nie znika i nic nie powstaje)
                            _klLost += (long)Math.Round(total); _klLostD += total; _klLostN++; drop.Add(kv.Key);
                            continue;
                        }
                        double it = 0;
                        foreach (var p in x.Parts) { double a = Math.Max(0, p.Left) * rate; p.Left += a; it += a; }
                        x.Interest += it; _klInt += it; total += it;
                        if (total > x.Peak) x.Peak = total;
                        bool war = KlAtWar(k);
                        long paid = 0;
                        var d = CrownIncome.DayOf(k);
                        if (d != null)
                        {
                            double due = war ? it : Math.Min(it + x.Peak / KlRepayDays, Math.Max(it, KlIncomeShare * d.Spend));
                            due = Math.Min(due, total);
                            long want = (long)Math.Ceiling(Math.Max(0, due) - 1e-9);
                            long pay = Math.Min(want, Math.Min(CrownIncome.LeftFor(k), (long)Math.Max(0, k.KingdomBudgetWallet)));
                            if (pay > 0)
                            {
                                long placed = KlRepay(k, x, pay);
                                if (placed > 0) { CrownIncome.Spent(k, placed); _klPaidInc += placed; paid += placed; }
                            }
                            if (!war) { if (paid + 1 < due) x.Missed++; else x.Missed = 0; }
                        }
                        // splata z wolnego zlota korony (wojna i pokoj): skarbiec ponad reszte wplywow dnia, zapas wojny, dlug u miast i dlug w Banku - 1/30 na dobe
                        total = KlDebt(x);
                        long res = KingdomTreasury.WarReserveFor(k);
                        if (res >= 0 && total >= 0.5)
                        {
                            double free = (double)k.KingdomBudgetWallet - CrownIncome.LeftFor(k) - res - total - CrownBorrow.DebtOf(k);
                            if (free >= KlSurplusDays)
                            {
                                long extra = (long)Math.Floor(Math.Min(total, free / KlSurplusDays));
                                long placed = extra > 0 ? KlRepay(k, x, extra) : 0;
                                if (placed > 0) { _klPaidStock += placed; paid += placed; }
                            }
                        }
                        x.DPaid += paid;
                        if (!war && x.Missed >= dflt)
                        {
                            double rest = KlDebt(x);
                            _klWritten += (long)Math.Round(rest); _klWrittenD += rest; _klDefaultN++;
                            var hit = new HashSet<Town>();
                            foreach (var p in x.Parts)
                            {
                                var st = Settlement.Find(p.St);
                                if (st == null || st.Town == null || !hit.Add(st.Town)) continue;
                                try { st.Town.Loyalty = Math.Max(0f, st.Town.Loyalty - KlDefaultLoyalty); _klLoyN++; } catch (Exception e) { KlStumble("Pay(lojalnosc)", e); }
                            }
                            foreach (var p in x.Parts) if (p.Left < 0) _klDust += p.Left;   // ujemny ulamek po zaokragleniu splaty - kontrola jak przy koncu czesci
                            _klBan[kv.Key] = today + KlBanDays;
                            drop.Add(kv.Key);
                            Log.Info("Pozyczki koron (korona2): " + (k.Name != null ? k.Name.ToString() : kv.Key) + " nie splacalo dlugu u miast " + dflt + " dob w pokoju - reszta "
                                     + N0(rest) + " zl przepada, miasta-wierzyciele -" + KlDefaultLoyalty.ToString("0", KlInv) + " lojalnosci, bez pozyczek od miast do doby " + (today + KlBanDays) + ".");
                            continue;
                        }
                        if (x.Missed > 0) _klMissedN++;
                        foreach (var p in x.Parts) if (p.Left < 0.5) _klDust += p.Left;   // ulamki ponizej pol zlotowki - koniec czesci (kontrola liczy je osobno)
                        x.Parts.RemoveAll(p => p.Left < 0.5);
                        if (x.Parts.Count == 0) drop.Add(kv.Key);
                    }
                    catch (Exception e) { KlStumble("Pay(krolestwo)", e); }
                }
                foreach (var id in drop) _kl.Remove(id);
            }
            catch (Exception e) { KlStumble("Pay", e); }
        }

        /// <summary>
        /// korona2 - KROK POZYCZEK (ArmouryBehavior: po CrownIncome.End i linii 186, przed linia "Skarbce:" i ksiega pieniadza MCrown): krolestwo w wojnie ze
        /// skarbcem ponizej zapasu wojny pozycza brak (najwyzej do limitu) z miast krolestwa, najpierw z najwiekszych nadwyzek; zloto do skarbca jako zapas
        /// (migawka 165 przesunieta - nie wplyw jutra). Potem linia "Pozyczki koron (korona2)" (takze przy samym kluczu FolkGuardCrownToReserve). Nie rzuca.
        /// </summary>
        internal static void CrownLoansLend()
        {
            var s = Settings.Current;
            if (s == null || Campaign.Current == null) return;
            bool loans = CrownLoansOn;
            if (!loans && !s.FolkGuardCrownToReserve) { _klDebtPrev = double.NaN; KlDayZero(); LastCrownLoanLent = LastCrownLoanRepaid = 0; return; }
            int today = (int)CampaignTime.Now.ToDays;
            if (!_klStartDone)
            {
                _klStartDone = true;
                double d0 = 0; foreach (var kv in _kl) d0 += KlDebt(kv.Value);
                if (s.LogEnabled)
                    Log.Info("Pozyczki koron (korona2): start sesji | klucze: pozyczki koron od miast (Crown Town Loans) " + (s.CrownTownLoans ? "tak" : "nie")
                             + " (165 Crown Current Income " + (CrownIncome.On ? "tak" : "nie") + "), czesc korony podatku od nadwyzki do zapasu (Folk Guard Crown To Reserve) "
                             + (s.FolkGuardCrownToReserve ? "tak" : "nie") + " (straznik obiegu " + (GuardOn ? "tak" : "nie") + ", cel udzialu koron "
                             + (100 * HdClamp(s.FolkGuardCrownTarget, 0, 0.5)).ToString("0.0", KlInv) + "%)"
                             + " | regula: krolestwo w wojnie ze skarbcem ponizej zapasu wojny (184 / W5b: max(500000, 60 dob naleznego zwrotu)) pozycza brak z miast krolestwa"
                             + " (kasa ponad zapas kupcow, kiesy W / R / P ponad poziom normalny; z kazdego najwyzej " + (100 * KlSrcShare).ToString("0", KlInv)
                             + "% nadwyzki na dobe, najpierw najwieksze; bez oblezonych i miast rodu gracza), limit " + KlLimitDays(s).ToString("0", KlInv)
                             + " dob sredniego naszego wplywu dnia + " + KlPerTown + " za miasto; " + (100 * KlRate(s)).ToString("0.#", KlInv) + "% rocznie; wojna - odsetki, pokoj -"
                             + " odsetki + 1/" + KlRepayDays + " najwiekszego dlugu (najwyzej " + (100 * KlIncomeShare).ToString("0", KlInv) + "% do wydania); zawsze 1/" + KlSurplusDays
                             + " wolnego zlota ponad zapas wojny i dlugi; niesplata " + KlDefault(s) + " dob w pokoju - umorzenie, -" + KlDefaultLoyalty.ToString("0", KlInv)
                             + " lojalnosci miast-wierzycieli, zakaz " + KlBanDays + " dob; pozyczone poza 1/360 zapasu (165), W5b i L5b-1"
                             + " | stan z zapisu: krolestw z dlugiem " + _kl.Count + ", dlug " + N0(d0) + ", zakazow " + _klBan.Count + " | klucze wylaczone = 972e6270.");
            }
            if (loans)
            {
                try
                {
                    double dlim = KlLimitDays(s);
                    foreach (var k in Kingdom.All)
                    {
                        try
                        {
                            if (k == null || k.IsEliminated || k.StringId == null || !KlAtWar(k)) continue;
                            _klWar++;
                            long res = KingdomTreasury.WarReserveFor(k);
                            if (res < 0) continue;
                            long wallet0 = k.KingdomBudgetWallet;
                            double gap = (double)res - wallet0;
                            if (gap < 1) continue;
                            _klCand++; _klGap += gap;
                            int until;
                            if (_klBan.TryGetValue(k.StringId, out until) && today < until) { _klBlkBan++; continue; }
                            KlInc a;
                            if (!_klInc.TryGetValue(k.StringId, out a) || a.N < KlIncWarmup) { _klBlkWarm++; continue; }
                            var src = new List<KlSrc>();
                            int towns;
                            double avail = KlSources(k, src, out towns);
                            KlDebtK x;
                            _kl.TryGetValue(k.StringId, out x);
                            double room = Math.Min(gap, dlim * a.Avg + (double)KlPerTown * towns - KlDebt(x));
                            if (room < 1) { _klBlkLimit++; continue; }
                            if (avail < 1) { _klBlkNoSrc++; continue; }
                            long left = (long)Math.Floor(Math.Min(Math.Min(room, avail), int.MaxValue / 2));
                            src.Sort((p, q) => q.Ex.CompareTo(p.Ex));
                            if (x == null) { x = new KlDebtK(); _kl[k.StringId] = x; }
                            long got = 0;
                            foreach (var y in src)
                            {
                                if (left <= 0) break;
                                long want = Math.Min(left, (long)Math.Floor(y.Ex));
                                if (want <= 0) continue;
                                long t = KlTake(y, want);
                                if (t <= 0) continue;
                                try { k.KingdomBudgetWallet += (int)t; }
                                catch (Exception e) { if (KlPut(y.T.Settlement, y.Kind, t, false) == 0) KlStumble("Lend(zwrot)", e); KlStumble("Lend(skarbiec)", e); continue; }
                                string sid = y.T.Settlement.StringId;
                                KlPart part = null;
                                foreach (var p in x.Parts) if (p.St == sid && p.Kind == y.Kind) { part = p; break; }
                                if (part == null) { part = new KlPart { St = sid, Kind = y.Kind }; x.Parts.Add(part); }
                                part.Left += t; x.Lent += t; got += t; left -= t;
                                if (y.Kind == 0) _klLentKasa += t; else if (y.Kind == 1) _klLentW += t; else if (y.Kind == 2) _klLentR += t; else _klLentP += t;
                            }
                            if (got > 0)
                            {
                                CrownIncome.ShiftSnapshot(k, got);   // zapas, nie wplyw dnia jutra: 165 nie odda go w rentach 180, a 1/360 i W5b go nie licza (CrownTownDebt)
                                _klLent += got; _klLentN++; x.DLent += got;
                                double nd = KlDebt(x);
                                if (nd > x.Peak) x.Peak = nd;
                                if ((long)k.KingdomBudgetWallet > res + 1) _klAbove++;   // kontrola: pozyczka nie podnosi skarbca ponad zapas wojny (oczekiwane 0)
                            }
                            else if (x.Parts.Count == 0) _kl.Remove(k.StringId);
                        }
                        catch (Exception e) { KlStumble("Lend(krolestwo)", e); }
                    }
                }
                catch (Exception e) { KlStumble("Lend", e); }
            }
            KlLine(today, s, loans);
        }

        /// <summary>Linia doby "Pozyczki koron (korona2): dzien N" (okno od poprzedniej linii), liczby "Obieg", zerowanie okna.</summary>
        private static void KlLine(int day, Settings s, bool loans)
        {
            try
            {
                double debt = 0, debtKasa = 0, limAll = 0, worst = 0; int parts = 0;
                var places = new HashSet<string>();
                var per = new List<KeyValuePair<double, string>>();
                foreach (var kv in _kl)
                {
                    var x = kv.Value;
                    double dk = KlDebt(x);
                    debt += dk; parts += x.Parts.Count;
                    foreach (var p in x.Parts) { places.Add(p.St); if (p.Kind == 0) debtKasa += Math.Max(0, p.Left); }
                    var k = KlKingdom(kv.Key);
                    double lim = double.NaN; long res = -1, w = 0;
                    KlInc a;
                    if (k != null && !k.IsEliminated)
                    {
                        int towns = 0;
                        try { var fs = k.Fiefs; if (fs != null) for (int i = 0; i < fs.Count; i++) if (fs[i] != null && fs[i].IsTown) towns++; } catch { }
                        if (_klInc.TryGetValue(kv.Key, out a)) { lim = KlLimitDays(s) * a.Avg + (double)KlPerTown * towns; limAll += lim; if (lim > 0) worst = Math.Max(worst, dk / lim); }
                        res = KingdomTreasury.WarReserveFor(k); w = k.KingdomBudgetWallet;
                    }
                    per.Add(new KeyValuePair<double, string>(dk, (k != null && k.Name != null ? k.Name.ToString() : kv.Key) + " " + N0(dk) + "/" + N0(lim)
                            + ", skarbiec " + w + "/" + res + (x.DLent > 0 ? ", +" + x.DLent : "") + (x.DPaid > 0 ? ", -" + x.DPaid : "") + (x.Missed > 0 ? ", bez pelnej raty " + x.Missed + " dob" : "")));
                }
                per.Sort((p, q) => q.Key.CompareTo(p.Key));
                var txt = new List<string>();
                for (int i = 0; i < per.Count && i < 12; i++) txt.Add(per[i].Value);
                if (per.Count > 12) txt.Add("... (" + (per.Count - 12) + " wiecej)");
                long paid = _klPaidInc + _klPaidStock;
                string ctl = "-";
                if (!double.IsNaN(_klDebtPrev))
                    ctl = Math.Round(debt - (_klDebtPrev + _klLent + _klFromAnt + _klInt - paid - _klWrittenD - _klLostD - _klDust)).ToString("0", KlInv);   // DS F3: + zaliczki przeniesione po kwartale
                _klDebtPrev = loans ? debt : double.NaN;
                LastCrownLoanLent = _klLent; LastCrownLoanRepaid = paid;
                double share = _gdShares && _gdWorld > 0 ? (double)_gdCrowns / _gdWorld : double.NaN;
                if (s.LogEnabled)
                {
                    var sb = new StringBuilder(1600);
                    sb.Append("Pozyczki koron (korona2): dzien ").Append(day)
                      .Append(" | pozyczki ").Append(loans ? "CZYNNE" : "wylaczone").Append(": zawarte ").Append(_klLent).Append(" zl u ").Append(_klLentN).Append(" krolestw (z kas miast ")
                      .Append(_klLentKasa).Append(", z kies mieszczan W ").Append(_klLentW).Append(" / R ").Append(_klLentR).Append(" / P ").Append(_klLentP).Append(')')
                      .Append("; krolestw w wojnie ").Append(_klWar).Append(", ze skarbcem ponizej zapasu wojny ").Append(_klCand).Append(" (luka razem ").Append(N0(_klGap)).Append(')')
                      .Append("; bez pozyczki: zakaz po niesplacie ").Append(_klBlkBan).Append(", pomiar wplywow < ").Append(KlIncWarmup).Append(" dob ").Append(_klBlkWarm)
                      .Append(", limit ").Append(_klBlkLimit).Append(", miasta bez nadwyzki ").Append(_klBlkNoSrc)
                      .Append(" | splaty: z wplywow dnia ").Append(_klPaidInc).Append(", z wolnego zlota ponad zapas wojny i dlugi ").Append(_klPaidStock).Append(", razem ").Append(paid)
                      .Append(" (do kas miast ").Append(_klPaidKasa).Append(", do kies mieszczan ").Append(_klPaidPurse).Append(")")
                      .Append("; odsetki narosle ").Append(N0(_klInt)).Append(" (").Append((100 * KlRate(s)).ToString("0.#", KlInv)).Append("% rocznie), krolestw z niepelna rata w pokoju ")
                      .Append(_klMissedN)
                      .Append(" | niesplacone (umorzone po ").Append(KlDefault(s)).Append(" dobach w pokoju) ").Append(_klWritten).Append(" zl u ").Append(_klDefaultN)
                      .Append(" krolestw (lojalnosc -").Append(KlDefaultLoyalty.ToString("0", KlInv)).Append(" w ").Append(_klLoyN).Append(" miastach), przepadlo z krolestwami zniszczonymi ")
                      .Append(_klLost).Append(" (").Append(_klLostN).Append(')')
                      .Append(" | dlug koron u miast ").Append(N0(debt)).Append(" (krolestw ").Append(_kl.Count).Append(", miast ").Append(places.Count).Append(", czesci ").Append(parts)
                      .Append("; kasy ").Append(N0(debtKasa)).Append(" / kiesy ").Append(N0(debt - debtKasa)).Append("), limit krolestw z dlugiem ").Append(N0(limAll))
                      .Append(", dlug / limit najwyzej ").Append(worst.ToString("0.000", KlInv)).Append(", zakazow ").Append(_klBan.Count)
                      .Append(_klFromAnt >= 0.5 ? " | z zaliczek mieszczan pod nadanie (DS, po kwartale) " + N0(_klFromAnt) : "")
                      .Append(" | kontrola: dlug - (wczoraj + zawarte + odsetki - splacone - umorzone - przepadle) ").Append(ctl)
                      .Append(", skarbiec po pozyczce ponad zapas wojny ").Append(_klAbove).Append(" (oczekiwane 0 / 0)")
                      .Append(" | czesc korony podatku od nadwyzki do zapasu (A, Folk Guard Crown To Reserve ").Append(s.FolkGuardCrownToReserve ? "tak" : "nie").Append("): dzis ")
                      .Append(_gdResT).Append(" z ").Append(_gdCrownT + _vhCrown).Append(" zl czesci korony (udzial koron wczoraj ").Append(double.IsNaN(share) ? "-" : (100 * share).ToString("0.0", KlInv) + "%")
                      .Append(" / cel ").Append((100 * HdClamp(s.FolkGuardCrownTarget, 0, 0.5)).ToString("0.0", KlInv)).Append("%)")
                      .Append(" | na krolestwo (dlug/limit, skarbiec/zapas wojny, +pozyczone, -splacone dzis): ").Append(txt.Count > 0 ? string.Join("; ", txt.ToArray()) : "-")
                      .Append(" | potkniecia ").Append(_klStumbles).Append(" (sesja ").Append(_klStumbleSess).Append(").");
                    Log.Info(sb.ToString());
                }
            }
            catch (Exception e) { KlStumble("Line", e); }
            KlDayZero();
        }

        // ============================================================ zapis arm_korona2
        /// <summary>Zapis: "k2v1;I,krolestwo,srednia,n;D,krolestwo,zaleglosc,pozyczone,splacone,odsetki,najwiekszy;P,osada,rodzaj,dlug;...;B,krolestwo,do_doby".</summary>
        internal static string ExportKorona2()
        {
            try
            {
                var sb = new StringBuilder(256);
                sb.Append("k2v1");
                foreach (var kv in _klInc) sb.Append(";I,").Append(kv.Key).Append(',').Append(kv.Value.Avg.ToString("R", KlInv)).Append(',').Append(kv.Value.N);
                foreach (var kv in _kl)
                {
                    var x = kv.Value;
                    if (x.Parts.Count == 0) continue;
                    sb.Append(";D,").Append(kv.Key).Append(',').Append(x.Missed).Append(',').Append(x.Lent.ToString("R", KlInv)).Append(',').Append(x.Repaid.ToString("R", KlInv))
                      .Append(',').Append(x.Interest.ToString("R", KlInv)).Append(',').Append(x.Peak.ToString("R", KlInv));
                    foreach (var p in x.Parts) sb.Append(";P,").Append(p.St).Append(',').Append(p.Kind).Append(',').Append(p.Left.ToString("R", KlInv));
                }
                foreach (var kv in _klBan) sb.Append(";B,").Append(kv.Key).Append(',').Append(kv.Value);
                ExportAnt(sb);   // DS F3 (L1): rekordy A / Q na koncu (starsza wersja moda je pomija)
                return sb.ToString();
            }
            catch (Exception e) { Log.Error("TownFolk.ExportKorona2", e); return ""; }
        }

        internal static void ImportKorona2(string data)
        {
            try
            {
                if (string.IsNullOrEmpty(data) || !data.StartsWith("k2v1", StringComparison.Ordinal)) return;
                KlDebtK cur = null, curA = null;
                foreach (var rec in data.Split(';'))
                {
                    var f = rec.Split(',');
                    if (f.Length == 0) continue;
                    if ((f[0] == "A" || f[0] == "Q") && f.Length >= 2) { curA = ImportAnt(f, curA); continue; }   // DS F3 (L1): zaliczki mieszczan
                    if (f[0] == "I" && f.Length >= 4)
                    {
                        var a = new KlInc();
                        double.TryParse(f[2], NumberStyles.Float, KlInv, out a.Avg);
                        int.TryParse(f[3], NumberStyles.Integer, KlInv, out a.N);
                        if (double.IsNaN(a.Avg) || double.IsInfinity(a.Avg) || a.Avg < 0) a.Avg = 0;
                        a.N = Math.Max(0, Math.Min(KlIncDays, a.N));
                        _klInc[f[1]] = a;
                    }
                    else if (f[0] == "D" && f.Length >= 7)
                    {
                        cur = new KlDebtK();
                        int.TryParse(f[2], NumberStyles.Integer, KlInv, out cur.Missed);
                        double.TryParse(f[3], NumberStyles.Float, KlInv, out cur.Lent);
                        double.TryParse(f[4], NumberStyles.Float, KlInv, out cur.Repaid);
                        double.TryParse(f[5], NumberStyles.Float, KlInv, out cur.Interest);
                        double.TryParse(f[6], NumberStyles.Float, KlInv, out cur.Peak);
                        _kl[f[1]] = cur;
                    }
                    else if (f[0] == "P" && f.Length >= 4 && cur != null)
                    {
                        int kind; double left;
                        if (int.TryParse(f[2], NumberStyles.Integer, KlInv, out kind) && double.TryParse(f[3], NumberStyles.Float, KlInv, out left)
                            && left > 0 && !double.IsInfinity(left) && kind >= 0 && kind <= 3)
                            cur.Parts.Add(new KlPart { St = f[1], Kind = kind, Left = left });
                    }
                    else if (f[0] == "B" && f.Length >= 3)
                    {
                        int until;
                        if (int.TryParse(f[2], NumberStyles.Integer, KlInv, out until)) _klBan[f[1]] = until;
                    }
                }
                var empty = new List<string>();
                foreach (var kv in _kl) if (kv.Value.Parts.Count == 0) empty.Add(kv.Key);
                foreach (var k in empty) _kl.Remove(k);
                var emptyA = new List<string>();
                foreach (var kv in _kla) if (kv.Value.Parts.Count == 0) emptyA.Add(kv.Key);
                foreach (var k in emptyA) _kla.Remove(k);
            }
            catch (Exception e) { Log.Error("TownFolk.ImportKorona2", e); }
        }
    }
}
