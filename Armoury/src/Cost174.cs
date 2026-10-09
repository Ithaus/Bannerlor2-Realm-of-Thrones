using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;

namespace Armoury
{
    /// <summary>
    /// PACZKA 174b.5 F6 - LINIA "Koszt 171-174 (doba)" (docs/PROJEKT-174B-DOWOZ-2026-10-09.md rozdz. 3.5 i "Krytyka i odpowiedzi" uwaga 16). Twarde ms na
    /// dobe petli dodanych w 171-174b zamiast szacunkow: wzor CirculationWindows - Stopwatch.GetTimestamp() przy co 16. wywolaniu, czas mnozony przez
    /// (wywolan / probek), liczniki wywolan pelne. BEZ Thread.Suspend. Prog P13 (krytyka 16): suma "nasze 171-174b" <= 1% czasu doby z zegara (nowa
    /// kampania ok. 0.13 s/d, zapis Jeffa ok. 0.22 s/d - ponizej szumu biegu). Pozycje zagniezdzone (BuyLoop lorda, zakup zastepczy, rezerwa) maja
    /// osobne wiersze; do sumy ida tylko rozlaczne: BuySubstitutes, zamowienia zamkow, VolunteerKit.BuyCore, WorkshopLaw.CyclePrefix (z TryStart i
    /// Quickest), TownFletchers.Work, CaravanAmmo, MaterialOrders (godzina i zamowienia), GarrisonArmory. Ceny (SupplyDemand.PricePostfix) - osobno, poza
    /// suma: ta warstwa jest starsza niz 171 (wpis 04.10), F1 ja tylko przyspiesza. Wolane tylko z watku gry (liczniki bez blokad - to log).
    /// </summary>
    internal static class Cost174
    {
        internal const int SBuySub = 0, SCastleOrder = 1, SVolunteer = 2, SCycle = 3, SFletch = 4, SCaravanAmmo = 5, SMoHourly = 6, SMoOrder = 7, SGarrison = 8,
                           STryBuy = 9, SBuyLoop = 10, STryStart = 11, SReserve = 12, SPrice = 13, Slots = 14;
        private static readonly string[] Names =
        {
            "BuySubstitutes", "zamowienia zamkow (BuyLoop w miescie)", "VolunteerKit.BuyCore", "WorkshopLaw.CyclePrefix", "TownFletchers.Work", "CaravanAmmo", "MaterialOrders.Hourly",
            "MaterialOrders.Order", "GarrisonArmory", "AiGear.TryBuyCore (calosc)", "BuyLoop (wlasna polka)", "WorkshopLaw.TryStart", "rezerwa kramu", "ceny sprzetu (PricePostfix)"
        };
        private const int InSum = 9;   // pozycje 0..8 - rozlaczne, do sumy
        private static readonly long[] _calls = new long[Slots], _ticks = new long[Slots], _sampled = new long[Slots];
        private static long _priceArms, _lastTs;
        private static int _lastDay = -1;

        internal static void Reset() { Array.Clear(_calls, 0, Slots); Array.Clear(_ticks, 0, Slots); Array.Clear(_sampled, 0, Slots); _priceArms = 0; _lastTs = 0; _lastDay = -1; }

        /// <summary>Poczatek pomiaru: znacznik czasu przy co 16. wywolaniu, inaczej 0.</summary>
        internal static long Begin(int s) { return (++_calls[s] & 15) == 1 ? Stopwatch.GetTimestamp() : 0L; }
        internal static void End(int s, long t) { if (t != 0) { _ticks[s] += Stopwatch.GetTimestamp() - t; _sampled[s]++; } }
        internal static void NotePriceArms() { _priceArms++; }

        private static double Ms(int s)
        {
            if (_sampled[s] <= 0) return 0.0;
            return _ticks[s] * 1000.0 / Stopwatch.Frequency * ((double)_calls[s] / _sampled[s]);
        }

        /// <summary>Z poczatku ArmouryBehavior.OnDailyTick: linia za dobe od poprzedniego taktu doby (czas z zegara miedzy taktami).</summary>
        internal static void Daily()
        {
            long now = Stopwatch.GetTimestamp();
            int day = (int)CampaignTime.Now.ToDays;
            try
            {
                if (_lastTs != 0)
                {
                    double wall = (now - _lastTs) * 1000.0 / Stopwatch.Frequency;
                    double sum = 0; for (int s = 0; s < InSum; s++) sum += Ms(s);
                    var inv = CultureInfo.InvariantCulture;
                    var sb = new StringBuilder();
                    sb.Append("Koszt 171-174 (doba): dzien ").Append(_lastDay).Append(" - doba ").Append(wall.ToString("0", inv)).Append(" ms (zegar); nasze 171-174b ").Append(sum.ToString("0.0", inv))
                      .Append(" ms (").Append(wall > 0 ? (100.0 * sum / wall).ToString("0.00", inv) : "-").Append("% doby, prog 1%) [");
                    for (int s = 0; s < InSum; s++) { if (s > 0) sb.Append(", "); sb.Append(Names[s]).Append(' ').Append(Ms(s).ToString("0.0", inv)).Append(" ms/").Append(_calls[s]); }
                    sb.Append("]; poza suma (zagniezdzone albo starsze niz 171) [");
                    for (int s = InSum; s < Slots; s++) { if (s > InSum) sb.Append(", "); sb.Append(Names[s]).Append(' ').Append(Ms(s).ToString("0.0", inv)).Append(" ms/").Append(_calls[s]); }
                    sb.Append("; w tym ceny sprzetu w osadach ").Append(_priceArms).Append("]; ").Append(ShelfIndex.Text()).Append('.');
                    Log.Info(sb.ToString());
                }
            }
            catch (Exception e) { Log.Error("Cost174.Daily", e); }
            Array.Clear(_calls, 0, Slots); Array.Clear(_ticks, 0, Slots); Array.Clear(_sampled, 0, Slots); _priceArms = 0;
            ShelfIndex.ClearDay();
            _lastDay = day;
            _lastTs = Stopwatch.GetTimestamp();   // bez czasu tej linii
        }
    }
}
