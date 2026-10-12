using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// E5/A2 (paczka 162, PLAN 5.2) - DWOR PELNY (projekt etapu 5 v2, regula R8). Wylacznik CourtGetsLapsedArmsShare; dziala tylko z budzetem rodu
    /// (ClanBudgetEnabled) i dworem 162m (HouseholdMinimal). Bez latek cudzego kodu - przelew wlasny, ten sam co dwor 162m.
    ///
    /// Przydzial sprzetu rodu AI (166: GearShare x D x f, w wojnie + 0.2 (G - R) / WarChestDays) jest trzymany najwyzej AiGearDaysCap dni
    /// (ClanBudget.Daily: GearLeft = min(GearLeft + Gear, AiGearDaysCap x Gear)). Dotad nadwyzke ponad pulap tylko przycinano - zloto lezalo
    /// w kiesie glowy (bieg z31-A d120: przydzial dnia 516 615 wobec 35 155 wydanych przez panow, L:31679; kiesy glow rodow +585 227/d, L:31716).
    /// Teraz przydzial, ktorego pan nie wydal przez AiGearDaysCap dni, przechodzi na DWOR (OB :277 "pieniadz ma krazyc, nie lezec"): dochodzi do
    /// wplaty dworu w ClanBudget.Court - jeden przelew glowa -> kasa siedziby (miasto albo zamek), ta sama tarcza dworu przy A1 wylaczonym
    /// (SoldierPay.HoldCourt), "wlasne" D stalego (dwor - ClanIncomeBook.NoteOwnPaid; sprzet do WLASNEJ siedziby - zapas Own/OwnBack, nizej), ta sama
    /// pozycja ksiegi (MoneyLedger.MBudget).
    ///
    /// Regula (jedna dla dworu, kolejnosc ciec):
    ///  1. przeterminowany jest najwyzej jeden przydzial dnia (ten sprzed AiGearDaysCap dni); reszta nadwyzki ponad pulap (pulap maleje przy spadku D,
    ///     przy pokoju, przy biedzie) - przyciecie jak dotad (inaczej jednorazowy skok wplaty przy kazdym zmniejszeniu przydzialu);
    ///  2. najemnik (185) - bez dworu, nic; rod bez siedziby - nic; dluznik (168) - polowa (jak dwor: "dwor -50%");   [1-2: Lapse, w Daily]
    ///  3. w wojnie ustepuje zoldowi jak dwor (WarCourtYieldsToWages): z zoldu ponad pulap bez udzialu dworu najpierw ustepuje dwor (jak dotad),
    ///     potem sprzet - rod na kredycie wojennym (168) nie placi dworu pozyczonym zlotem;
    ///  4. tylko z kiesy rodziny ponad rezerwe rodu po dzisiejszym dworze: w wojnie R (rezerwa wojny 166), w pokoju ReserveDaysPeace x D (OB: "reszta
    ///     do zapasu, dopoki G < 60 x D") - ponizej rezerwy niewydany przydzial to oszczednosci (zostaje w kiesie); bez tego przelew wciagalby rody pod
    ///     rezerwa (58 z 318 w d120 biegu z31-A, budzet-rodow.csv) dalej w biede - mnoznik f pulapu zoldu do 0.8, pod 0.25 R do 0.5 (wojsko);
    ///  5. kiesa glowy po dworze zostaje z zoldem dnia (partie + zalogi + karawany), nie mniej niz podloga rodziny (FamilyPurseFloor): gra pobiera
    ///     zold z kiesy glowy w rozliczeniu rodu rozlozonym na cala dobe (CampaignPeriodicEventManager._dailyTickClanTicker.PeriodicTickSome - po
    ///     naszym bloku dobowym), a dwor 162m bierze tylko do podlogi 5 000 - przeterminowany sprzet nie moze zabrac zoldu (zalegly zold = dezercja).
    ///     Dwor placony pierwszy (podloga 162m bez zmian - wplata samego dworu jak dotad), sprzet z reszty.   [3-5: Take w Court; licznik Paid]
    /// Gracz - bez budzetu i bez dworu ([D] 08.10). Zloto nie powstaje i nie znika: przelew glowa -> kasa siedziby; zawor kasy oddaje je panu i koronie.
    ///
    /// Hak B1 (KL 1.3: dwor 80% kupcy / 20% lud): CourtFolkShare dziala przy czynnej kiesie ludu (E5/B1, TownFolk.Live) - inaczej FolkShare = 0
    /// i cala wplata dworu (z przeterminowanym sprzetem) idzie do kasy siedziby. B1 dzieli przelew w ClanBudget.Court (siedziba-zamek: kredyt dworu).
    ///
    /// Zapas "wlasnego" (recenzja A2, uwaga 1): sprzet wplacony do kasy WLASNEJ siedziby (siedziba nalezy do rodu-platnika) to pieniadze rodu - wracaja
    /// zaworem przez wiele dob (miasto: renta PopulationLaw ok. 7% nadwyzki dziennie; zamek: zawor 110, 2/3 pan, 1/3 korona). 169c uznaje za "wlasne"
    /// tylko ulamek zaworu jednej doby (wplata x zawor / kasa ponad prog, ClanIncomeBook.StableD.cs:336-349; _ownPaid czyszczone co dobe) - reszta
    /// wracalaby jako "ziemia" i D staly rodu rosl o zawrot jego wlasnego niewydanego przydzialu (sprzezenie 166: dwor, sprzet, pulap zoldu; 165 od
    /// StableLand). Dlatego czesc E5/A2 przelewu do wlasnej siedziby nie idzie do ClanIncomeBook.NoteOwnPaid, tylko do zapasu osady (Own); od nastepnej
    /// doby StableBegin pyta OwnBack: zapas x (zawor dnia / kasa ponad prog) wraca jako "wlasne", az zapas sie wyczerpie. Sam dwor 162m - bez zmian
    /// (NoteOwnPaid jak dotad; rozszerzenie na caly przelew dworu - decyzja Jeffa).
    /// Recenzja E5/A5: ten sam zapas bierze wplate gracza z wlasnego zlota do kasy WLASNEJ osady (BeeGifts.Pay -> OwnPlayer, akcja z menu poza blokiem
    /// dobowym) - wraca zaworem jako "wlasne", nie "ziemia" (inaczej StableLand gracza rosl o jego wlasne zloto: podstawa powinnosci 165, limit Banku 168).
    /// Taka wplata wchodzi do zapasu przy najblizszym ClearDay (RollOwn), a linia "D staly" tej doby podaje ja osobno ("wplaty gracza ... od wczorajszej
    /// linii") - "wplacone dzis" liczy tylko wplaty z bloku dobowego (ClearDay zeruje je przed linia), bez tego bilans zapasu z doby na dobe sie rozjezdza.
    /// Stan: liczniki doby (ClanBudget.ClearDay) i zapas "wlasnego" w pamieci sesji (jak pierscienie D stalego - bez zapisu); Reset (ClanBudget.Reset,
    /// konstruktor ArmouryBehavior - nowa kampania i kazde wczytanie) czysci wszystko, takze licznik potkniec i zapamietane bledy.
    /// </summary>
    internal static class CourtFull
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>Paczka czynna: klucz i dwor 162m (bez dworu nie ma komu oddac przydzialu). Budzet rodu sprawdza wolajacy (ClanBudget.Daily).</summary>
        internal static bool On { get { var s = Settings.Current; return s != null && s.ClanBudgetEnabled && s.CourtGetsLapsedArmsShare && s.HouseholdMinimal; } }

        /// <summary>Hak B1: czesc wplaty dworu dla kiesy ludu siedziby (KL 1.3: 80% kupcy / 20% lud). E5/B1: kiesa czynna (TownFolk.Live) - CourtFolkShare
        /// (ClanBudget.Court dzieli przelew dworu: TownFolk.Earn KCourt); kiesa wylaczona - 0 (caly dwor do kasy siedziby, jak dotad).</summary>
        internal static float FolkShare { get { var s = Settings.Current; return s != null && TownFolk.Live ? ValveSplit.Unit(s.CourtFolkShare) : 0f; } }

        // liczniki doby (linie "Budzet rodow (166)" i "Obieg")
        private static double _dGross, _dMerc, _dNoSeat, _dDebtor, _dReserve, _dYield, _dFloor;
        private static long _dPaid, _dPaidTown, _dPaidCastle;
        private static int _dGrossN, _dPaidN;
        private static int _stumbles;
        private static readonly HashSet<string> _err = new HashSet<string>();

        // recenzja A2 (uwaga 1): zapas "wlasnego" - sprzet wplacony do kasy WLASNEJ siedziby, ktory nie wrocil jeszcze zaworem (opis klasy, Own, OwnBack)
        private sealed class OwnStock { public Clan C; public double V; }
        private static readonly Dictionary<Settlement, OwnStock> _own = new Dictionary<Settlement, OwnStock>();        // zapas z dob poprzednich (zawor go widzi)
        private static readonly Dictionary<Settlement, OwnStock> _ownToday = new Dictionary<Settlement, OwnStock>();   // wplaty dnia - do zapasu od jutra
        private static double _dOwnBack, _dOwnCrown, _dOwnCap, _dOwnTrim, _dOwnLost;
        private static long _dOwnIn;
        private static long _lateIn, _dLateIn;   // recenzja E5/A5: wplaty gracza do kasy wlasnej osady od ostatniego ClearDay / weszly do zapasu przy dzisiejszym

        /// <summary>Sprzet niewydany -> dwor dzis (zl, wplacone do kas siedzib) - linia "Obieg".</summary>
        internal static long LastPaid { get { return _dPaid; } }

        /// <summary>Recenzja A2 (uwaga 2): ClanBudget.Reset (miedzy kampaniami i po wczytaniu) - licznik potkniec, zapamietane bledy, zapas "wlasnego", liczniki doby.</summary>
        internal static void Reset()
        {
            _stumbles = 0; _err.Clear(); _own.Clear(); _ownToday.Clear(); _lateIn = 0;
            ClearDay();
        }

        internal static void ClearDay()
        {
            _dGross = _dMerc = _dNoSeat = _dDebtor = _dReserve = _dYield = _dFloor = 0;
            _dPaid = _dPaidTown = _dPaidCastle = 0; _dGrossN = _dPaidN = 0;
            _dOwnBack = _dOwnCrown = _dOwnCap = _dOwnTrim = _dOwnLost = 0; _dOwnIn = 0;
            _dLateIn = _lateIn; _lateIn = 0;   // recenzja E5/A5: wplaty gracza od wczoraj wchodza do zapasu teraz (RollOwn nizej) - do linii "D staly" tej doby
            // recenzja A2 (uwaga 1): wczorajsze wplaty do wlasnych siedzib wchodza do zapasu - dzisiejszy zawor (PopulationLaw.Daily, CastlePurse.Daily)
            // biegl juz po nich, przed ClanBudget.Daily (ClearDay na jego poczatku); dzisiejsze wplaty zawor zobaczy jutro
            try { RollOwn(); } catch (Exception e) { Stumble("RollOwn", e); }
        }

        private static void RollOwn()
        {
            try
            {
                foreach (var kv in _ownToday)
                {
                    OwnStock o;
                    if (_own.TryGetValue(kv.Key, out o) && ReferenceEquals(o.C, kv.Value.C)) { o.V += kv.Value.V; continue; }
                    if (o != null) _dOwnLost += o.V;   // osada zmienila pana miedzy wplatami - stary zapas to juz nie pieniadze nowego pana
                    _own[kv.Key] = kv.Value;
                }
            }
            finally { _ownToday.Clear(); }   // po wyjatku w polowie - bez drugiego dodania tych samych wplat jutro
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_err.Add(where)) Log.Error("CourtFull." + where, e); } catch { }
        }

        /// <summary>ClanBudget.Daily, tuz przed przycieciem GearLeft do pulapu: ile przydzialu sprzetu przechodzi dzis na dwor (po cieciach 1-2;
        /// GearLeft jeszcze z wczoraj, Gear - przydzial dnia). Wynik zapisany w b.Lapsed; GearLeft nie zmienia sie (przyciecie robi wolajacy jak dotad).</summary>
        internal static double Lapse(ClanBudget.B b, Settings s, bool merc)
        {
            try
            {
                if (b == null) return 0;
                b.Lapsed = 0;
                if (s == null || !(b.Gear > 0)) return 0;
                double over = b.GearLeft + b.Gear - Math.Max(1f, s.AiGearDaysCap) * b.Gear;
                if (!(over > 0)) return 0;
                double x = Math.Min(over, b.Gear);   // 1: najwyzej jeden przydzial dnia
                _dGross += x; _dGrossN++;
                if (!On) return 0;   // wylaczone - przeterminowane tylko w linii (zostaje w kiesie jak dotad)
                if (merc) { _dMerc += x; return 0; }
                if (b.Seat == null || b.Seat.Town == null) { _dNoSeat += x; return 0; }
                if (b.Debtor) { _dDebtor += x * 0.5; x *= 0.5; }   // 2: dluznik - polowa, jak dwor
                b.Lapsed = x;
                return x;
            }
            catch (Exception e) { Stumble("Lapse", e); if (b != null) b.Lapsed = 0; return 0; }
        }

        /// <summary>ClanBudget.Court, po ustapieniu dworu zoldowi (court - sam dwor po ustapieniu): przeterminowany sprzet do wplaty dworu po cieciach 3-5
        /// (w wojnie ustepuje zoldowi z tego, czego dwor nie pokryl - wageNow i CapBase te same co w Court; tylko z kiesy rodziny ponad rezerwe rodu po
        /// dzisiejszym dworze; glowa zostaje z zoldem dnia). headGold - kiesa glowy przed wplata dworu. Zwraca kwote do dodania do wplaty; b.Lapsed = ta kwota.</summary>
        internal static double Take(ClanBudget.B b, Settings s, double court, int headGold)
        {
            try
            {
                if (b == null || !(b.Lapsed > 0)) return 0;
                double x = b.Lapsed;
                if (s != null && b.War && s.WarCourtYieldsToWages)   // 3: w wojnie ustepuje zoldowi po dworze
                {
                    double court0 = Math.Max(0, b.Household - b.Food);
                    double wageNow = b.Zero ? b.Nominal * (b.MenPar + b.MenGar) + b.WCar : b.WPar + b.WGar + b.WCar;
                    double over = Math.Max(0, wageNow - b.CapBase) - court0;   // zold ponad pulap razem z calym udzialem dworu
                    if (over > 0) { double used = Math.Min(x, over); x -= used; _dYield += used; }
                }
                if (s != null && x > 0)   // 4: tylko ponad rezerwe rodu (wojna R, pokoj ReserveDaysPeace x D), po dzisiejszym dworze
                {
                    double reserve = b.War ? b.R : Math.Max(0f, s.ReserveDaysPeace) * b.D;
                    double room = Math.Max(0, b.G - reserve - Math.Max(0, court));
                    if (x > room) { _dReserve += x - room; x = room; }
                }
                if (s != null && x > 0)   // 5: glowa zostaje po dworze z zoldem dnia (gra pobiera go z kiesy glowy w rozliczeniu rodu pozniej w dobie), nie mniej niz podloga rodziny
                {
                    double keep = Math.Max(Math.Max(0, s.FamilyPurseFloor), (double)(b.WPar + b.WGar + b.WCar));
                    double room = Math.Max(0, headGold - Math.Max(0, court) - keep);
                    if (x > room) { _dFloor += x - room; x = room; }
                }
                b.Lapsed = x;
                return x;
            }
            catch (Exception e) { Stumble("Take", e); if (b != null) b.Lapsed = 0; return 0; }
        }

        /// <summary>ClanBudget.Court, po podlodze kiesy glowy: x - cala wplata dworu dzis (moze byc 0), court - udzial samego dworu (po ustapieniu zoldowi).
        /// Dwor placony pierwszy, przeterminowany sprzet z reszty (podloga 162m). Tylko liczniki - zloto przelewa Court. Zwraca czesc sprzetu w wplacie x
        /// (do zapasu "wlasnego" - Own).</summary>
        internal static long Paid(ClanBudget.B b, Settlement seat, long x, double court)
        {
            try
            {
                if (b == null || !(b.Lapsed > 0)) return 0;
                double want = b.Lapsed;
                b.Lapsed = 0;
                long courtPaid = Math.Min(Math.Max(0, x), (long)Math.Max(0, court));   // dwor pierwszy - tyle, ile 162m wplacilby bez sprzetu
                long paid = Math.Min(Math.Max(0, x - courtPaid), (long)Math.Ceiling(want));
                if (want > paid) _dFloor += want - paid;
                if (paid <= 0) return 0;
                _dPaid += paid; _dPaidN++;
                if (seat != null && seat.IsTown) _dPaidTown += paid; else _dPaidCastle += paid;
                return paid;
            }
            catch (Exception e) { Stumble("Paid", e); return 0; }
        }

        /// <summary>Recenzja A2 (uwaga 1) - ClanBudget.Court, przy przelewie: czesc sprzetu lp wplacona do kasy WLASNEJ siedziby idzie do zapasu "wlasnego"
        /// (wraca jako "wlasne" przez OwnBack az do zwrotu) zamiast do ClanIncomeBook.NoteOwnPaid. Zwraca kwote wzieta do zapasu - wolajacy odejmuje ja od
        /// NoteOwnPaid (jedno liczenie). 0: siedziba nie jest rodu (zawor cudzej osady to renta jej pana - dochod z ziemi) albo D staly wylaczony.</summary>
        internal static int Own(Settlement seat, Clan c, long lp)
        {
            try
            {
                if (lp <= 0 || seat == null || c == null || !ReferenceEquals(seat.OwnerClan, c) || !ClanIncomeBook.StableDOn) return 0;
                OwnStock o;
                if (!_ownToday.TryGetValue(seat, out o) || !ReferenceEquals(o.C, c)) { o = new OwnStock { C = c }; _ownToday[seat] = o; }
                o.V += lp; _dOwnIn += lp;
                return (int)Math.Min(int.MaxValue, lp);
            }
            catch (Exception e) { Stumble("Own", e); return 0; }
        }

        /// <summary>Recenzja E5/A5 - BeeGifts.Pay, po przelewie: wplata gracza z wlasnego zlota do kasy WLASNEJ osady do zapasu "wlasnego" (jak sprzet E5/A2 -
        /// Own). Akcja z menu, poza blokiem dobowym: liczona osobno (linia "D staly" nastepnej doby), nie w "wplacone dzis". Zwraca kwote wzieta do zapasu
        /// (0: osada nie jest rodu platnika albo D staly wylaczony).</summary>
        internal static int OwnPlayer(Settlement st, Clan c, long amount)
        {
            int x = Own(st, c, amount);
            if (x > 0) { _dOwnIn -= x; _lateIn += x; }
            return x;
        }

        /// <summary>Recenzja A2 (uwaga 1) - ClanIncomeBook.StableBegin (169c), dla kazdego miasta i zamku, po "wlasnych" z wplat od ostatniej doby
        /// (NoteOwnPaid): ile zaworu tej doby to zwrot zapasu (pieniadze rodu, nie ziemia). pay - czesc pana z zaworu dnia (miasto PopulationLaw.RentOf,
        /// zamek CastlePurse.DuesOf), avail - kasa ponad prog przed zaworem, room - czesc zaworu dnia, ktora nie jest jeszcze "wlasne". Zapas schodzi o caly
        /// zawor na swoja czesc (zapas / kasa ponad prog - jak tarcza dworu SoldierPay.DecayCourt): czesc pana -> "wlasne", obok niej zamek - czesc korony
        /// (114), miasto - danina wojenna (KingdomTreasury.TownLevyToday); zapas przyciety do kasy ponad prog (osada wydala), znika przy zmianie pana.
        /// Bez zaworu tej doby (oblezenie, kasa pod progiem, pan martwy - RentOf/DuesOf 0) zapas czeka. Tylko liczniki - zlota nie rusza.</summary>
        internal static long OwnBack(Settlement st, long pay, long avail, long room)
        {
            try
            {
                OwnStock o;
                if (st == null || !_own.TryGetValue(st, out o)) return 0;
                if (!ReferenceEquals(st.OwnerClan, o.C)) { _dOwnLost += o.V; _own.Remove(st); return 0; }   // nowy pan - jego zawor to juz nie pieniadze platnika
                if (pay <= 0 || avail <= 0) return 0;
                double v = o.V;
                if (v > avail) { _dOwnTrim += v - avail; v = avail; }   // kasa ponad prog mniejsza niz zapas - osada wydala reszte (towar, budowy)
                double part = v / avail;
                double lord = pay * part;
                double other = (st.IsCastle ? CastleCrownOf(st, pay) : TownLevyOf(st)) * part;
                if (lord + other > v) { double k = v / (lord + other); lord *= k; other *= k; }
                long back = (long)Math.Min(Math.Max(0, room), lord);
                _dOwnBack += back; _dOwnCap += lord - back; _dOwnCrown += other;
                v -= lord + other;
                if (v < 1) _own.Remove(st); else o.V = v;
                return back;
            }
            catch (Exception e) { Stumble("OwnBack", e); return 0; }
        }

        // zamek: czesc korony obok czesci pana (CastlePurse.Daily 114: podzial ValveSplit przy CastleDuesSplitWithCrown i krolestwie rodu; korona = pan x (1 - u) / u)
        private static double CastleCrownOf(Settlement st, long lordPay)
        {
            var s = Settings.Current;
            var k = st.OwnerClan != null ? st.OwnerClan.Kingdom : null;
            if (s == null || !s.CastleDuesSplitWithCrown || k == null || k.IsEliminated) return 0;
            double u = CastlePurse.LordShare(s);
            return u > 0 && u < 1 ? lordPay * (1 - u) / u : 0;
        }

        // miasto: danina wojenna tej doby z kasy miasta (KingdomTreasury.Levies - przed ClanBudget.Daily); scalenie porcji A: + udzial korony z zaworu
        // kasy miasta E5/A1 (TownPurse.CrownOf - poza RentOf, ktory ma tylko czesc pana; jak czesc korony zamku 114)
        private static double TownLevyOf(Settlement st)
        {
            int x; double lv = KingdomTreasury.TownLevyToday.TryGetValue(st, out x) ? Math.Max(0, x) : 0;
            try { lv += TownPurse.CrownOf(st); } catch { }
            return lv;
        }

        /// <summary>Dopisek do linii "D staly (169c)" (po "zawor zamkow X (wlasne Y)" - te "wlasne" juz zawieraja zwrot zapasu): zapas "wlasnego" E5/A2.</summary>
        internal static string OwnNote()
        {
            if (!On && _own.Count == 0 && _ownToday.Count == 0 && _dOwnLost <= 0 && _dLateIn <= 0) return "";
            double v = 0; foreach (var o in _own.Values) v += o.V;
            return " [E5/A2 zapas wlasny (sprzet wplacony do kasy wlasnej siedziby - wraca zaworem jako wlasne, nie ziemia): wrocilo dzis " + R(_dOwnBack)
                   + ", do korony i daniny " + R(_dOwnCrown) + ", ponad zawor dnia " + R(_dOwnCap) + ", przyciete (osada wydala) " + R(_dOwnTrim)
                   + ", utracone (nowy pan) " + R(_dOwnLost) + ", zapas " + R(v) + " w " + _own.Count + " osadach, wplacone dzis " + _dOwnIn + " (do zapasu od jutra)"
                   + (_dLateIn > 0 ? "; wplaty gracza do kasy wlasnej osady (E5/A5) od wczorajszej linii " + _dLateIn + " (w zapasie od dzis)" : "") + "]";
        }

        /// <summary>Dopisek do linii "Budzet rodow (166)" (po "sprzet: przydzial dnia ...").</summary>
        internal static string Note(Settings s)
        {
            var sb = new StringBuilder(480);
            int days = s != null ? (int)Math.Max(1f, s.AiGearDaysCap) : 30;
            sb.Append(" | sprzet niewydany po ").Append(days).Append(" dniach -> dwor (E5/A2): ");
            if (s == null || !s.CourtGetsLapsedArmsShare || !s.HouseholdMinimal)
            {
                sb.Append("wylaczone").Append(s != null && s.CourtGetsLapsedArmsShare ? " (wymaga Household Minimal)" : "")
                  .Append(" - przeterminowane dzis ").Append(R(_dGross)).Append(" u ").Append(_dGrossN).Append(" rodow zostaje w kiesie glowy jak dotad");
            }
            else
            {
                sb.Append("przeterminowane dzis ").Append(R(_dGross)).Append(" u ").Append(_dGrossN).Append(" rodow, wplacone do kas siedzib ").Append(_dPaid)
                  .Append(" u ").Append(_dPaidN).Append(" rodow (miasta ").Append(_dPaidTown).Append(" / zamki ").Append(_dPaidCastle).Append(")")
                  .Append("; zostalo w kiesie glowy: ponizej rezerwy rodu (wojna R, pokoj ").Append(s.ReserveDaysPeace.ToString("0", Inv)).Append(" D) ").Append(R(_dReserve))
                  .Append(", ustapilo zoldowi ").Append(R(_dYield)).Append(", kiesa glowy (zold dnia, podloga) ").Append(R(_dFloor))
                  .Append(", dluznicy (polowa) ").Append(R(_dDebtor)).Append(", najemnicy bez dworu ").Append(R(_dMerc)).Append(", bez siedziby ").Append(R(_dNoSeat))
                  .Append("; udzial ludu (hak B1) ").Append((100.0 * FolkShare).ToString("0", Inv)).Append("% (Court Folk Share ")
                  .Append(s.CourtFolkShare.ToString("0.00", Inv)).Append(" - czynny przy kiesie ludu E5/B1)");
            }
            if (_stumbles > 0) sb.Append(", potkniecia E5/A2 ").Append(_stumbles);
            return sb.ToString();
        }

        /// <summary>Dopisek do linii "Obieg" (po "dwor (166/162m) do kas siedzib X (w tym miasta pod tarcza dworu Y"): zamki i sprzet niewydany.</summary>
        internal static string ObiegNote()
        {
            return ", zamki " + Math.Max(0, ClanBudget.LastCourt - ClanBudget.LastCourtTown) + "; w tym sprzet niewydany -> dwor (E5/A2) " + _dPaid
                   + ": miasta " + _dPaidTown + " / zamki " + _dPaidCastle;
        }

        private static string R(double v) { return Math.Round(v).ToString("0", Inv); }
    }
}
