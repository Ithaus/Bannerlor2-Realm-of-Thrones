using System;

namespace CrashScribe
{
    /// <summary>
    /// UW (zawieszenie 19.10 16:09): linia pomiaru raz na minute zegara - ile odsmiecan pamieci (GC) przeszlo od startu i od poprzedniej linii.
    /// Kazde GC zatrzymuje wszystkie watki gry; zawieszenie 16:09 zaczelo sie wlasnie od GC wolanego z watku AI partii. Tylko pomiar, bez zmian w grze.
    /// Wolane co klatke z OnApplicationTick: poza minuta - jedno porownanie Environment.TickCount, bez alokacji.
    /// </summary>
    internal static class GcPulse
    {
        private const int PeriodMs = 60000;
        private static int _next;            // Environment.TickCount nastepnej linii
        private static bool _armed;
        private static int _g0, _g1, _g2;    // stan z poprzedniej linii
        private static int _lines;

        internal static void Tick()
        {
            if (!Config.GcPulseLog) return;
            int now = Environment.TickCount;
            if (!_armed)
            {
                _armed = true;
                _next = now + PeriodMs;
                _g0 = GC.CollectionCount(0); _g1 = GC.CollectionCount(1); _g2 = GC.CollectionCount(2);
                return;
            }
            if (unchecked(now - _next) < 0) return;   // TickCount przekreca sie co 49 dni - roznica ze znakiem
            _next = now + PeriodMs;
            try
            {
                int g0 = GC.CollectionCount(0), g1 = GC.CollectionCount(1), g2 = GC.CollectionCount(2);
                long mb = GC.GetTotalMemory(false) >> 20;
                _lines++;
                Scribe.Line("GC (UW): minuta " + _lines + " | gen0 " + g0 + " (+" + (g0 - _g0) + "), gen1 " + g1 + " (+" + (g1 - _g1)
                            + "), gen2 " + g2 + " (+" + (g2 - _g2) + ") | pamiec zarzadzana " + mb + " MB");
                _g0 = g0; _g1 = g1; _g2 = g2;
            }
            catch { }
        }
    }
}
