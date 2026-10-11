using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
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
    /// (SoldierPay.HoldCourt), ten sam licznik "wlasne" D stalego (ClanIncomeBook.NoteOwnPaid), ta sama pozycja ksiegi (MoneyLedger.MBudget).
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
    /// Hak B1 (KL 1.3: dwor 80% kupcy / 20% lud): CourtFolkShare dziala dopiero z czynna kiesa ludu (klucz TownFolkLive powstaje w B1) - do tego
    /// czasu FolkShare = 0 i cala wplata dworu (z przeterminowanym sprzetem) idzie do kasy siedziby. B1 dzieli przelew w ClanBudget.Court.
    /// Stan: tylko liczniki doby (zerowane w ClanBudget.ClearDay - takze w Reset miedzy kampaniami); nic do zapisu.
    /// </summary>
    internal static class CourtFull
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>Paczka czynna: klucz i dwor 162m (bez dworu nie ma komu oddac przydzialu). Budzet rodu sprawdza wolajacy (ClanBudget.Daily).</summary>
        internal static bool On { get { var s = Settings.Current; return s != null && s.ClanBudgetEnabled && s.CourtGetsLapsedArmsShare && s.HouseholdMinimal; } }

        /// <summary>Hak B1: czesc wplaty dworu dla kiesy ludu siedziby. Do B1 zawsze 0 - kiesy ludu nie ma; B1 zamienia na
        /// "kiesa czynna (TownFolkLive i TownFolkMode >= 1) ? ValveSplit.Unit(CourtFolkShare) : 0" i dzieli przelew dworu.</summary>
        internal static float FolkShare { get { return 0f; } }

        // liczniki doby (linie "Budzet rodow (166)" i "Obieg")
        private static double _dGross, _dMerc, _dNoSeat, _dDebtor, _dReserve, _dYield, _dFloor;
        private static long _dPaid, _dPaidTown, _dPaidCastle;
        private static int _dGrossN, _dPaidN;
        private static int _stumbles;
        private static readonly HashSet<string> _err = new HashSet<string>();

        /// <summary>Sprzet niewydany -> dwor dzis (zl, wplacone do kas siedzib) - linia "Obieg".</summary>
        internal static long LastPaid { get { return _dPaid; } }

        internal static void ClearDay()
        {
            _dGross = _dMerc = _dNoSeat = _dDebtor = _dReserve = _dYield = _dFloor = 0;
            _dPaid = _dPaidTown = _dPaidCastle = 0; _dGrossN = _dPaidN = 0;
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
        /// Dwor placony pierwszy, przeterminowany sprzet z reszty (podloga 162m). Tylko liczniki - zloto przelewa Court.</summary>
        internal static void Paid(ClanBudget.B b, Settlement seat, long x, double court)
        {
            try
            {
                if (b == null || !(b.Lapsed > 0)) return;
                double want = b.Lapsed;
                b.Lapsed = 0;
                long courtPaid = Math.Min(Math.Max(0, x), (long)Math.Max(0, court));   // dwor pierwszy - tyle, ile 162m wplacilby bez sprzetu
                long paid = Math.Min(Math.Max(0, x - courtPaid), (long)Math.Ceiling(want));
                if (want > paid) _dFloor += want - paid;
                if (paid <= 0) return;
                _dPaid += paid; _dPaidN++;
                if (seat != null && seat.IsTown) _dPaidTown += paid; else _dPaidCastle += paid;
            }
            catch (Exception e) { Stumble("Paid", e); }
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
                  .Append(s.CourtFolkShare.ToString("0.00", Inv)).Append(" - czynny dopiero z kiesa ludu)");
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
