using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// P1 (OBIEG KIESY LUDU, docs/kiesa-ludu/PROJEKT-OBIEG-KIESY-LUDU-2026-10-13.md rozdz. 3.1 szczebel 4, 3.2, 5, 6, 7; wpiecia za PROJEKT-POZYCZKI rozdz. 2 R1
    /// i rozdz. 10 z dwiema zmianami sedziego: stawka 2.5% i jedna stawka dla wszystkich klas; decyzja Jeffa 13.10 "1. tak") - DANINA WOJENNA Z OSZCZEDNOSCI LUDU.
    /// Wylacznik FolkWarAid (wylaczony = pobor 0, stan sprzed paczki co do monety), stawka FolkWarAidShare (0.025).
    ///  - KTO i OD CZEGO: kazda klasa mieszczan (W / R / P) i podzamcze - od kiesy PONAD zapas dni (W TownFolkReserveDays 10 / R 21 / P 30 / podzamcze 10 dni
    ///    potrzeby - te same stale co oferta nadwyzki; liczy wolajacy: TownFolk.PayAid w kroku 5 doby ludu, po robotach gminy, przed oferta nadwyzki na jutro).
    ///    Zapasu nikt nie rusza: pobor najwyzej cala nadwyzka w dol do pelnej zlotowki; biedne miasto (bez nadwyzki) nie placi nic. Wies - bez zmian (Levies 2.5%).
    ///  - ILE: stawka dnia = FolkWarAidShare x s / WarTaxMax, s = biezaca stawka potrzeby korony z podatku 184 (WarTaxK.S, najwyzej pulap krolestwa: 3%, z polityka
    ///    War Tax 6% - wtedy danina x 2). s = 0 (korona bez potrzeby, pokoj) - danina 0. Jedna stawka dla wszystkich klas (rozroznienie klas jest w zapasie dni).
    ///    W5b regula 1 (klucz WarAidFollowsLordsShortfall, KingdomTreasury.WarTax.cs): w miejsce s wchodzi wieksza z s i stawki z braku wojska u panow (WtAidS) -
    ///    krolestwo, ktorego rody nie maja z czego oplacic druzyn, placi danine takze wtedy, gdy rachunki samej korony potrzeby nie pokazuja. Podatek od
    ///    zarobku tej stawki nie uzywa. Klucz wylaczony = samo s.
    ///  - BRAMKI dokladnie jak FolkWarTax: WtOn (WarTaxFromFolk, kiesa ludu czynna, 165), wlasciciel osady ma krolestwo (rod bez krolestwa, buntownicy bez korony -
    ///    nic), krolestwo w wojnie (WtAtWar), osada nieoblezona (BesiegedPay.Holds, rodzaj KFolkAid - liczy, ile by zaplacila). Krolestwo gracza na tych samych
    ///    zasadach: danina do skarbca krolestwa, nie do kiesy gracza.
    ///  - PLATNIK -> ODBIORCA: kiesa klasy ludu osady (wolajacy zdejmuje zwrocone CALE zloto) -> Kingdom.KingdomBudgetWallet (to samo zloto, nic z niczego i nic
    ///    w nicosc). Ulamek ponizej 1 zl czeka na osade i klase (niezaplacona reszta, nie pieniadz; pamiec sesji - bez pola zapisu). Wplyw dnia 165: osobne okno
    ///    _waWindow czytane w CrownIncome.Begin (KDay.WarAid, w "nasze") i zamykane razem z oknem 184 w CrownIncome.End - korona wydaje to zloto tej samej doby
    ///    w zwyklej kolejnosci (rata 186, dary, reparacje, kontrakty, wezwania, zwrot zoldu, renty 180).
    ///  - Linie: "Danina z oszczednosci (P1): dzien N" (dzien gry jak 184; po rentach 180, przed CrownIncome.End), dopisek w "Korona: wplywy dnia (165)",
    ///    w "Obieg" (korona) i w linii doby "Kiesa ludu:" (wydatek kies - kontrola zamknieta kies bez zmiany definicji).
    ///  - Gracz: komunikat, gdy lud jego osady zaczyna placic - raz na wojne (stan w pamieci sesji: zeruje go pokoj krolestwa gracza, zmiana krolestwa albo
    ///    utrata osady, NIE s = 0 w trwajacej wojnie; pierwsza doba po wczytaniu tylko zapamietuje - WaPlayerSweep), z czescia dzisiejsza i najwyzsza;
    ///    krol-gracz co 7 dob suma daniny do skarbca. Bez spamu: najwyzej jeden komunikat kazdego rodzaju na dobe.
    ///  - Odczyt dla okna mieszkancow (FolkWarAidRate) nie zapisuje pamieci stanu wojny doby (WaAtWarPeek) - pobor 184 i danina licza wojne w takcie doby.
    /// Wyjatek per pobor / per linia - licznik potkniec (pierwszy do pliku), bez globalnych wylacznikow (CLAUDE.md 7). Zapis gry: nic nowego.
    /// </summary>
    internal static partial class KingdomTreasury
    {
        /// <summary>Powod, dla ktorego klasa z nadwyzka dzis (nie) placi daniny - liczniki linii i zdanie "Savings" w oknie mieszkancow.</summary>
        internal const int WaPaid = 0, WaOff = 1, WaNoRealm = 2, WaNoNeed = 3, WaSiege = 4, WaWait = 5;
        private const int WaWhyN = 6;

        private sealed class WarAidK { public long Taken, Held; public readonly long[] Cls = new long[WtClasses]; public int Places; public double Lords; }   // Lords - W5b: czesc daniny ze stawki ponad s korony (brak wojska u panow)

        private static readonly Dictionary<Kingdom, long> _waWindow = new Dictionary<Kingdom, long>();              // pobrane od ostatniego CrownIncome.End (wplyw dnia 165)
        private static readonly Dictionary<Settlement, double[]> _waCarry = new Dictionary<Settlement, double[]>();   // ulamek ponizej 1 zl na osade i klase (niezaplacona reszta)
        private static readonly Dictionary<Kingdom, WarAidK> _waK = new Dictionary<Kingdom, WarAidK>();              // doba: na krolestwo
        private static readonly HashSet<Settlement> _waPlaces = new HashSet<Settlement>();                           // doba: osady, ktore zaplacily
        private static readonly long[] _waCls = new long[WtClasses];
        private static readonly int[] _waWhy = new int[WaWhyN];
        private static long _waHeld; private static int _waCalls, _waHeldN, _waTowns, _waCastles, _waStumbles;
        private static double _waLords;   // W5b regula 1: danina doby ze stawki ponad s korony (brak wojska u panow) - licznik linii
        private static readonly HashSet<string> _waErr = new HashSet<string>();
        // gracz (pamiec sesji): osady rodu gracza, o ktorych komunikat poczatku poboru juz byl W TEJ WOJNIE (wartosc - krolestwo, ktoremu placa; wpis znika
        // przy pokoju, bez krolestwa, po zmianie krolestwa i po utracie osady - WaPlayerSweep); nazwy do komunikatu tej doby; suma tygodnia krola-gracza
        private static readonly Dictionary<Settlement, Kingdom> _waPlayer = new Dictionary<Settlement, Kingdom>();
        private static readonly List<Settlement> _waGone = new List<Settlement>();
        private static readonly List<string> _waTell = new List<string>();
        private static double _waTellRate, _waTellTop;
        private static int _waDawns, _waKingDays;
        private static long _waKingSum;
        /// <summary>"Obieg" (korona): danina z oszczednosci ludu pobrana w tej dobie (kiesy ludu -> skarbce).</summary>
        internal static long LastFolkWarAid;

        private static void WarAidReset()
        {
            _waWindow.Clear(); _waCarry.Clear(); _waK.Clear(); _waPlaces.Clear();
            Array.Clear(_waCls, 0, WtClasses); Array.Clear(_waWhy, 0, WaWhyN);
            _waHeld = 0; _waCalls = _waHeldN = _waTowns = _waCastles = 0; _waStumbles = 0; _waErr.Clear(); _waLords = 0;
            _waPlayer.Clear(); _waGone.Clear(); _waTell.Clear(); _waTellRate = 0; _waTellTop = 0; _waDawns = 0; _waKingDays = 0; _waKingSum = 0;
            LastFolkWarAid = 0;
        }

        private static void WaClearDay()
        {
            _waK.Clear(); _waPlaces.Clear();
            Array.Clear(_waCls, 0, WtClasses); Array.Clear(_waWhy, 0, WaWhyN);
            _waHeld = 0; _waCalls = _waHeldN = _waTowns = _waCastles = 0; _waLords = 0;
        }

        private static void WaStumble(string where, Exception e)
        {
            _waStumbles++;
            try { if (_waErr.Add(where)) Log.Error("KingdomTreasury.WarAid." + where, e); } catch { }
        }

        /// <summary>Mianownik stawki: pulap podatku 184 bez polityki (WarTaxMax, 3%); gdy ktos ustawi go na 0 - 3% z projektu ("s / 3%").</summary>
        private static double WaBaseCap(Settings s)
        {
            double c = WtClamp(s.WarTaxMax, 0, 0.2);
            return c > 0 ? c : 0.03;
        }

        /// <summary>
        /// Stan wojny krolestwa dla sciezek ODCZYTU spoza taktu doby (okno mieszkancow) - BEZ zapisu pamieci doby _wtWar (recenzja P1): takt doby nie jest
        /// zrownany z granica licznika dob, wiec okno otwarte po zmianie licznika, a przed taktem, zapisaloby przez WtAtWar stan wojny, z ktorego w takcie
        /// korzysta pobor 184 i danina (pokoj zawarty w miedzyczasie = doba poboru w pokoju). Tu: pamiec tylko czytana (ta sama doba i wpis jest), inaczej
        /// stan liczony na swiezo i NIE zapamietywany. W takcie doby wynik ten sam co WtAtWar.
        /// </summary>
        private static bool WaAtWarPeek(Kingdom k)
        {
            bool w;
            if (_wtWarDay == (int)CampaignTime.Now.ToDays && _wtWar.TryGetValue(k, out w)) return w;
            return AtWar(k);
        }

        /// <summary>Stawka dnia daniny osady (ulamek nadwyzki ponad zapas) BEZ bramki oblezenia; 0 i powod, gdy nie placi. k - krolestwo wlasciciela osady.
        /// peek - sciezka odczytu (okno, pomiary): stan wojny bez zapisu pamieci doby (WaAtWarPeek); pobor i linia doby - WtAtWar jak podatek 184.</summary>
        private static double WaRate(Settlement st, Settings s, bool peek, out Kingdom k, out int why)
        {
            k = null; why = WaOff;
            if (s == null || !s.FolkWarAid || !WtOn) return 0;
            k = st.OwnerClan != null ? st.OwnerClan.Kingdom : null;
            if (k == null || k.IsEliminated) { why = WaNoRealm; return 0; }
            why = WaNoNeed;
            var w = WtOf(k, false);
            double sv = WtAidS(w);   // W5b regula 1: wieksza z s korony i stawki z braku wojska u panow (klucz wylaczony albo bez W5 = samo s, jak przed paczka)
            if (w == null || sv <= 0 || !(peek ? WaAtWarPeek(k) : WtAtWar(k))) return 0;
            double rate = WtClamp(s.FolkWarAidShare, 0, 0.2) * Math.Min(sv, WtCap(k, s)) / WaBaseCap(s);
            if (!(rate > 0)) return 0;
            why = WaPaid;
            return rate > 1 ? 1 : rate;
        }

        /// <summary>Najwyzsza stawka dnia daniny w tym krolestwie przy dzisiejszych prawach (s na pulapie krolestwa: 3%, z polityka War Tax 6%) - do komunikatu gracza.</summary>
        private static double WaTopRate(Kingdom k, Settings s)
        {
            double rate = WtClamp(s.FolkWarAidShare, 0, 0.2) * WtCap(k, s) / WaBaseCap(s);
            return rate > 1 ? 1 : rate;
        }

        /// <summary>
        /// Stawka dnia daniny tej osady dzis (ulamek nadwyzki ponad zapas) - TYLKO ODCZYT (bez licznikow, bez zlota i bez zapisu pamieci stanu wojny doby -
        /// WaAtWarPeek): okno mieszkancow i pomiary.
        /// 0 i powod: wylaczone / kiesa nieczynna / bez 165 (WaOff), rod bez krolestwa (WaNoRealm), pokoj albo s = 0 (WaNoNeed), osada oblezona (WaSiege).
        /// </summary>
        internal static double FolkWarAidRate(Settlement st, out int why)
        {
            why = WaOff;
            try
            {
                if (st == null) return 0;
                Kingdom k;
                double rate = WaRate(st, Settings.Current, true, out k, out why);
                if (rate > 0 && st.IsUnderSiege && BesiegedPay.On) { why = WaSiege; return 0; }
                return rate;
            }
            catch { why = WaOff; return 0; }
        }

        /// <summary>
        /// P1 POBOR z oszczednosci ludu (wola TownFolk.PayAid raz na dobe na klase: miasto WtW / WtR / WtP, zamek WtCastle). excess - kiesa klasy ponad zapas dni
        /// (policzona przez wolajacego ta sama stala co oferta nadwyzki), avail - stan kiesy klasy. Zwraca CALE zloto, ktore klasa oddaje koronie - wolajacy zdejmuje
        /// dokladnie tyle z kiesy tej klasy; skarbiec krolestwa wlasciciela osady dostaje to samo (przelew). Najwyzej nadwyzka w dol do zlotowki (zapas zostaje caly)
        /// i najwyzej stan kiesy. Ulamek ponizej 1 zl czeka (osada, klasa). 0: wylaczone, kiesa nieczynna, bez 165, rod bez krolestwa, pokoj, s = 0, osada
        /// oblezona (R22 - BesiegedPay.Holds liczy, ile by zaplacila). Nie rzuca (wlasny try, licznik potkniec); skarbiec dopisywany na samym koncu - wyjatek
        /// wczesniej = nic nie przelane.
        /// </summary>
        internal static int FolkWarAid(Settlement st, int cls, double excess, long avail)
        {
            try
            {
                if (st == null || cls < 0 || cls >= WtClasses || !(excess >= 1) || double.IsInfinity(excess)) return 0;
                var s = Settings.Current;
                Kingdom k; int why;
                double rate = WaRate(st, s, false, out k, out why);
                bool mine = st.OwnerClan != null && st.OwnerClan == Clan.PlayerClan;
                // stan komunikatu gracza NIE jest tu zerowany: s = 0 w trwajacej wojnie to nie nowa wojna (recenzja P1) - zeruje pokoj (WaPlayerSweep, raz na dobe)
                if (!(rate > 0)) { _waWhy[why]++; return 0; }
                _waCalls++;
                double owed = excess * rate;
                double[] cr;
                if (!_waCarry.TryGetValue(st, out cr)) { cr = new double[WtClasses]; _waCarry[st] = cr; }
                double due = owed + cr[cls];
                long pay = (long)Math.Floor(due);
                long room = Math.Min((long)Math.Floor(excess), Math.Max(0, avail));   // zapas dni zostaje caly; najwyzej stan kiesy
                if (pay > room) { pay = room; due = pay; }
                if (pay > int.MaxValue) { pay = int.MaxValue; due = pay; }
                // R22: oblezona osada nie placi (zostaje w kiesie ludu); niezaplacona reszta czeka jak byla
                if (BesiegedPay.Holds(st, BesiegedPay.KFolkAid, pay, true))
                {
                    _waHeld += pay; _waHeldN++; _waWhy[WaSiege]++;
                    WarAidK hk;
                    if (!_waK.TryGetValue(k, out hk)) { hk = new WarAidK(); _waK[k] = hk; }
                    hk.Held += pay;
                    return 0;
                }
                if (pay <= 0) { cr[cls] = due; _waWhy[WaWait]++; return 0; }
                int ip = (int)pay;
                cr[cls] = due - ip;
                WarAidK a;
                if (!_waK.TryGetValue(k, out a)) { a = new WarAidK(); _waK[k] = a; }
                a.Taken += ip; a.Cls[cls] += ip;
                // W5b regula 1 (sam licznik linii): czesc poboru, ktora wziela sie ze stawki ponad s korony - danina x (stawka - s) / stawka
                var wk = WtOf(k, false);
                if (wk != null && wk.SN > wk.S && LnOn)
                {
                    double cap = WtCap(k, s), sa = Math.Min(wk.SN, cap), s0 = Math.Min(wk.S, cap);
                    if (sa > s0) { double part = ip * (sa - s0) / sa; a.Lords += part; _waLords += part; }
                }
                if (_waPlaces.Add(st)) { a.Places++; if (st.IsTown) _waTowns++; else _waCastles++; }
                _waCls[cls] += ip; _waWhy[WaPaid]++;
                long x; _waWindow.TryGetValue(k, out x); _waWindow[k] = x + ip;   // wplyw dnia 165 (CrownIncome.Begin)
                if (mine)
                {
                    Kingdom told; bool known = _waPlayer.TryGetValue(st, out told) && told == k;
                    _waPlayer[st] = k;
                    // raz na wojne: pierwszy pobor w osadzie, o ktorej w tej wojnie komunikatu nie bylo; pierwsza doba sesji tylko zapamietuje (po wczytaniu
                    // nie wiadomo, czy komunikat juz byl - WaPlayerSweep dopisuje tej doby takze osady bez nadwyzki i oblezone)
                    if (!known && _waDawns > 0)
                    {
                        string nm = st.Name != null ? st.Name.ToString() : st.StringId;
                        if (!_waTell.Contains(nm)) _waTell.Add(nm);
                        if (rate > _waTellRate) _waTellRate = rate;
                        double top = WaTopRate(k, s);
                        if (top > _waTellTop) _waTellTop = top;
                    }
                }
                if (k.RulingClan != null && k.RulingClan == Clan.PlayerClan) _waKingSum += ip;
                k.KingdomBudgetWallet += ip;                       // odbiorca: skarbiec krolestwa (platnik - kiesa klasy u wolajacego, to samo zloto) - OSTATNI krok
                return ip;
            }
            catch (Exception e) { WaStumble("FolkWarAid", e); return 0; }
        }

        /// <summary>CrownIncome.Begin: danina z oszczednosci ludu pobrana od ostatniego CrownIncome.End do tego krolestwa ("nasze" wplywy dnia).</summary>
        internal static long FolkWarAidWindow(Kingdom k)
        {
            long x;
            return k != null && _waWindow.TryGetValue(k, out x) ? x : 0;
        }

        private static string WaNames(List<string> names)
        {
            int n = names.Count;
            if (n == 0) return "";
            if (n == 1) return names[0];
            if (n == 2) return names[0] + " and " + names[1];
            if (n == 3) return names[0] + ", " + names[1] + " and " + names[2];
            return names[0] + ", " + names[1] + ", " + names[2] + " and " + (n - 3) + " more";
        }

        /// <summary>
        /// Stan komunikatu "raz na wojne" osad rodu gracza - raz na dobe (z WarAidDay: po poborze tej doby, przed komunikatem). Krolestwo gracza w pokoju albo
        /// brak krolestwa: wpisy znikaja (nastepna wojna = nowy komunikat); s = 0 w TRWAJACEJ wojnie niczego nie zeruje (recenzja P1 - komunikat wracal przy
        /// kazdym przejsciu s przez zero). Wpisy osad, ktore odeszly od rodu, i wpisy innego krolestwa - usuwane. Pierwsza doba sesji: osady rodu, od ktorych
        /// korona dzis bierze (sama stawka - takze bez nadwyzki, z czekajacym ulamkiem i oblezone), uznane za powiadomione - komunikat nie wraca po wczytaniu.
        /// </summary>
        private static void WaPlayerSweep(Settings s)
        {
            var pc = Clan.PlayerClan;
            var pk = pc != null ? pc.Kingdom : null;
            if (pk == null || pk.IsEliminated || !WtAtWar(pk)) { _waPlayer.Clear(); return; }
            _waGone.Clear();
            foreach (var kv in _waPlayer) if (kv.Value != pk || kv.Key.OwnerClan != pc) _waGone.Add(kv.Key);
            foreach (var st in _waGone) _waPlayer.Remove(st);
            _waGone.Clear();
            if (_waDawns > 0) return;
            foreach (var st in pc.Settlements)
            {
                if (st == null || !(st.IsTown || st.IsCastle) || _waPlayer.ContainsKey(st)) continue;
                Kingdom k; int why;
                if (WaRate(st, s, false, out k, out why) > 0 && k == pk) _waPlayer[st] = pk;
            }
        }

        /// <summary>Komunikaty gracza (napisy po angielsku): lud jego osad zaczal placic danine (raz na wojne); krol-gracz - suma tygodnia. Najwyzej jeden kazdego rodzaju na dobe.</summary>
        private static void WarAidTellPlayer(int today, Settings s)
        {
            try { WaPlayerSweep(s); } catch (Exception e) { WaStumble("WaPlayerSweep", e); }
            if (_waTell.Count > 0)
            {
                string names = WaNames(_waTell);
                // stawka rosnie z potrzeba korony (s od 0.5% do pulapu) - komunikat jest jeden na wojne, wiec podaje czesc dzisiejsza i najwyzsza (recenzja P1:
                // pierwszy pobor nowej kampanii wypada przy s = 0.5% = jedna czesc na 240, a po kilku tygodniach lud placi jedna na 40)
                int part = _waTellRate > 0 ? (int)Math.Max(1, Math.Round(1.0 / _waTellRate)) : 0;
                int top = _waTellTop > 0 ? (int)Math.Max(1, Math.Round(1.0 / _waTellTop)) : part;
                if (part > 0)
                {
                    string share = top < part
                        ? "one part in " + part + " now, never more than one in " + top + " under the realm's present laws"
                        : "one part in " + part + ", the most the realm's present laws allow";
                    Log.Player("War aid: while the realm is at war, the people of " + names + " give the crown a share of the coin they hold above their reserve each day."
                               + " The share follows the crown's need: " + share + ". Their bread money is not touched.");
                    if (s.LogEnabled) Log.Info("Danina z oszczednosci (P1): komunikat gracza (poczatek poboru w jego osadach) - " + names + ", jedna czesc na " + part
                                               + " dziennie, najwyzej jedna na " + top + ".");
                }
                _waTell.Clear(); _waTellRate = 0; _waTellTop = 0;
            }
            var pk = Clan.PlayerClan != null ? Clan.PlayerClan.Kingdom : null;
            bool king = pk != null && !pk.IsEliminated && pk.RulingClan == Clan.PlayerClan;
            if (!king) { _waKingSum = 0; _waKingDays = 0; return; }
            _waKingDays++;
            if (today % 7 != 0) return;
            if (_waKingSum > 0)
            {
                string span = _waKingDays >= 7 ? "this week" : (_waKingDays == 1 ? "today" : "in the last " + _waKingDays + " days");
                Log.Player("War aid from the towns and castles of the realm brought the treasury " + _waKingSum.ToString("N0", WtInv) + " coins " + span + ".");
                if (s.LogEnabled) Log.Info("Danina z oszczednosci (P1): komunikat krola-gracza - do skarbca " + _waKingSum + " zl w " + _waKingDays + " dobach.");
            }
            _waKingSum = 0; _waKingDays = 0;
        }

        // ------------------------------------------------------------ doba: linia "Danina z oszczednosci (P1)" - z WarTaxDaily (po rentach 180, przed CrownIncome.End)
        private static void WarAidDay()
        {
            long taken = _waCls[WtW] + _waCls[WtR] + _waCls[WtP] + _waCls[WtCastle];
            LastFolkWarAid = taken;
            var s = Settings.Current;
            if (s == null || Campaign.Current == null) return;
            int today = (int)CampaignTime.Now.ToDays;
            try { WarAidTellPlayer(today, s); } catch (Exception e) { WaStumble("WarAidTellPlayer", e); }
            _waDawns++;
            if (!s.LogEnabled) { _waStumbles = 0; return; }
            double share = WtClamp(s.FolkWarAidShare, 0, 0.2), baseCap = WaBaseCap(s);
            double capHi = Math.Max(WtClamp(s.WarTaxMax, 0, 0.2), WtClamp(s.WarTaxMaxWithPolicy, 0, 0.2));
            var ov0 = new double[WtClasses]; var ov1 = new double[WtClasses];
            bool folkDay = TownFolk.AidDayOf(today - 1, ov0, ov1);
            // skarbce: krolestwa z danina tej doby i wszystkie (165 czynne do End)
            long kMeas = 0, kRel = 0, kAid = 0, kTax = 0, kGiven = 0, kDue = 0, kRent = 0, kLeft = 0, kLoan = 0, kSpend = 0, aGiven = 0, aDue = 0, aRent = 0;
            int kN = 0;
            var parts = new List<KeyValuePair<long, string>>();
            foreach (var k in Kingdom.All)
            {
                try
                {
                    if (k == null || k.IsEliminated || k.StringId == null) continue;
                    var kd = CrownIncome.DayOf(k);
                    if (kd != null) { aGiven += kd.RefundGiven; aDue += kd.RefundDue; aRent += kd.Rent; }
                    WarAidK a; _waK.TryGetValue(k, out a);
                    var w = WtOf(k, false);
                    double sv = w != null ? Math.Min(WtAidS(w), WtCap(k, s)) : 0;   // W5b regula 1: stawka, od ktorej liczy sie danina (wieksza z s korony i stawki z braku wojska u panow)
                    bool war = WtAtWar(k);
                    // recenzja P1: wiersz ma KAZDE krolestwo z nadwyzka ludu - takze w pokoju i z s = 0 (skrzynie, ktorych danina nie rusza: "korona bez
                    // potrzeby nie bierze"); lokata W miast osobno - narzedzie dzieli z niej lokate swiata na czesc z poborem i bez (wiersz P1-1b)
                    double ovW, ovK = TownFolk.AidExcessOf(k, today - 1, out ovW);
                    if (a == null && !(ovK >= 1)) continue;
                    long at = a != null ? a.Taken : 0;
                    if (at > 0)
                    {
                        kN++;
                        if (kd != null)
                        {
                            kMeas += kd.Measured; kRel += kd.Release; kAid += kd.WarAid; kTax += kd.WarTax; kGiven += kd.RefundGiven; kDue += kd.RefundDue;
                            kRent += kd.Rent; kLeft += Math.Max(0, kd.Left); kLoan += kd.LoanIn; kSpend += kd.Spend;
                        }
                    }
                    var sb1 = new StringBuilder(160);
                    sb1.Append(k.Name != null ? k.Name.ToString() : k.StringId).Append(": s ").Append((100 * sv).ToString("0.00", WtInv)).Append('%').Append(war ? "" : " (pokoj)")
                       .Append(", stawka dnia ").Append((100 * (war && s.FolkWarAid ? share * sv / baseCap : 0)).ToString("0.000", WtInv)).Append("%, danina ").Append(at);
                    if (a != null) sb1.Append(" (W ").Append(a.Cls[WtW]).Append(" / R ").Append(a.Cls[WtR]).Append(" / P ").Append(a.Cls[WtP]).Append(" / podz. ").Append(a.Cls[WtCastle])
                                      .Append("; osad ").Append(a.Places).Append(a.Held > 0 ? "; wstrzymane w oblezeniu " + a.Held : "").Append(')');
                    sb1.Append(", nadwyzka ludu po ").Append(Math.Round(ovK).ToString("0", WtInv)).Append(" (W miast ").Append(Math.Round(ovW).ToString("0", WtInv)).Append(')');
                    if (kd != null) sb1.Append(", zwrot ").Append(kd.RefundGiven).Append('/').Append(kd.RefundDue).Append(", renty ").Append(kd.Rent).Append(", kredyt186 ").Append(kd.LoanIn);
                    else sb1.Append(", 165 bez dnia");
                    // W5b regula 1 (na koncu wiersza): s korony z podatku 184, gdy danina liczy sie od stawki wyzszej, i czesc dzisiejszej daniny z tej roznicy
                    if (w != null && w.SN > w.S && LnOn)
                        sb1.Append(", z braku wojska u panow (W5b) ").Append(Math.Round(a != null ? a.Lords : 0).ToString("0", WtInv)).Append(" (s korony ")
                           .Append((100 * Math.Min(w.S, WtCap(k, s))).ToString("0.00", WtInv)).Append("%)");
                    parts.Add(new KeyValuePair<long, string>(at, sb1.ToString()));
                }
                catch (Exception e) { WaStumble("WarAidDay(krolestwo)", e); }
            }
            parts.Sort((x, y) => y.Key.CompareTo(x.Key));
            var txt = new List<string>(); foreach (var p in parts) txt.Add(p.Value);
            long other = Math.Max(0, kSpend - kGiven - kRent - kLeft + kLoan);   // rata 186, dary, reparacje, kontrakty, wezwania (zwrot z kredytu 186 nie schodzi z wplywow dnia)
            var sb = new StringBuilder(2048);
            sb.Append("Danina z oszczednosci (P1): dzien ").Append(today)
              .Append(" | wylacznik Folk War Aid ").Append(s.FolkWarAid ? "tak" : "nie (pobor 0 - jak przed paczka)")
              .Append(", podatek 184 (War Tax From Folk) ").Append(s.WarTaxFromFolk ? "tak" : "nie (s = 0 - danina 0)")
              .Append(", kiesa ludu (B1) ").Append(WtFolkLive ? "czynna" : "nieczynna (danina 0)")
              .Append(", 165 Crown Current Income ").Append(CrownIncome.On ? "tak" : "nie (danina 0)")
              .Append(" | stawka ").Append((100 * share).ToString("0.00", WtInv)).Append("%/d kiesy ponad zapas dni x s / ").Append((100 * baseCap).ToString("0.0", WtInv))
              .Append("% (s z podatku 184").Append(LnOn ? " albo wyzsza stawka z braku wojska u panow - W5b" : "").Append("; z polityka War Tax najwyzej x").Append((capHi / baseCap).ToString("0.00", WtInv)).Append("); zapas dni potrzeby: W ")
              .Append(s.TownFolkReserveDays).Append(" / R 21 / P 30 / podzamcze 10; jedna stawka dla klas; s i stawka dnia w wierszach krolestw - po kroku tygodnia (pobor tej doby szedl stawka sprzed kroku)")
              .Append("; wiersz ma kazde krolestwo z nadwyzka ludu - takze pokoj i s = 0, gdzie danina skrzyn nie rusza (W miast = lokata wyrobnikow)")
              .Append(" | pobrano ").Append(taken).Append(" zl z ").Append(_waPlaces.Count).Append(" osad (miasta ").Append(_waTowns).Append(", zamki ").Append(_waCastles)
              .Append("): W ").Append(_waCls[WtW]).Append(", R ").Append(_waCls[WtR]).Append(", P ").Append(_waCls[WtP]).Append(", podzamcza ").Append(_waCls[WtCastle])
              .Append("; wolan poboru ").Append(_waCalls)
              .Append(" | klaso-osady z nadwyzka bez poboru: wylaczone ").Append(_waWhy[WaOff]).Append(", bez krolestwa ").Append(_waWhy[WaNoRealm])
              .Append(", pokoj albo s = 0 ").Append(_waWhy[WaNoNeed]).Append(", oblezenie ").Append(_waWhy[WaSiege]).Append(" (wstrzymane ").Append(_waHeld).Append(" zl, ").Append(_waHeldN)
              .Append("), ulamek ponizej 1 zl czeka ").Append(_waWhy[WaWait]).Append("; zaplacily ").Append(_waWhy[WaPaid])
              .Append(" | nadwyzka ponad zapas przed / po: ");
            if (!folkDay) sb.Append("- (doba kiesy ludu nie biegla)");
            else
            {
                double b0 = 0, b1 = 0;
                string[] nm = { "W ", "R ", "P ", "podzamcza " };
                for (int c = 0; c < WtClasses; c++)
                {
                    if (c > 0) sb.Append(", ");
                    sb.Append(nm[c]).Append(Math.Round(ov0[c]).ToString("0", WtInv)).Append(" / ").Append(Math.Round(ov1[c]).ToString("0", WtInv));
                    b0 += ov0[c]; b1 += ov1[c];
                }
                sb.Append(", razem ").Append(Math.Round(b0).ToString("0", WtInv)).Append(" / ").Append(Math.Round(b1).ToString("0", WtInv));
            }
            sb.Append(" | do skarbcow ").Append(taken).Append(" (wplyw dnia 165 tej doby; krolestw ").Append(kN).Append(')')
              .Append(", w tym ze stawki ponad s korony - brak wojska u panow (W5b, War Aid Follows Lords Shortfall ").Append(s.WarAidFollowsLordsShortfall ? (LnOn ? "tak" : "tak, nieczynny") : "nie")
              .Append(") ").Append(Math.Round(_waLords).ToString("0", WtInv))
              .Append(" | skarbce krolestw z danina: wplywy dnia ").Append(kMeas).Append(" + 1/360 zapasu ").Append(kRel).Append(" (w tym danina ").Append(kAid)
              .Append(", podatek 184 ").Append(kTax).Append("); wydane tej doby: zwrot zoldu ").Append(kGiven).Append(" z naleznego ").Append(kDue)
              .Append(", renty (180) ").Append(kRent).Append(", pozostale (rata 186, dary, reparacje, kontrakty, wezwania, doplata wedlug potrzeby W5) ").Append(other)
              .Append(", zostalo z wplywow ").Append(kLeft).Append("; kredyt Banku (186) na zwrot ").Append(kLoan)
              .Append(" | wszystkie krolestwa: zwrot zoldu ").Append(aGiven).Append(" z naleznego ").Append(aDue).Append(", renty (180) ").Append(aRent)
              .Append(" | na krolestwo: ").Append(txt.Count > 0 ? string.Join(" ; ", txt.ToArray()) : "-")
              .Append(" | czas poboru ").Append(TownFolk.AidMs(today - 1)).Append(" ms | potkniecia ").Append(_waStumbles).Append('.');
            _waStumbles = 0;
            Log.Info(sb.ToString());
        }
    }
}
