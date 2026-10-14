using System;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// WW1 (wyspy-wolni, zelazna cena - nabrzeze; docs/wyspy-wolni/PROJEKT-WYSPY-WOLNI.md rozdz. 2.1) - STRONA LUDU: odczyt nadwyzki kiesy klasy W (robotnicy,
    /// rybacy, dokerzy; zamek - podzamcze) osady portowej TERAZ. Miara zapasu ta sama co danina P1 i pobor W7: kiesa ponad TownFolkReserveDays dni potrzeby
    /// (zamek - RdC) i ponad oferte nadwyzki na jutro (to, co lud juz postanowil wydac - jak LevyRoom W7). Zdjecie i zwrot - LevyTake / LevyBack (ksiega kies
    /// _ctlOut i LDayOut - kontrola zamknieta r = 0). Bez nowych pol zapisu. Nie rzuca.
    /// </summary>
    internal static partial class TownFolk
    {
        /// <summary>WW1: nadwyzka kiesy W osady ponad zapas dni i oferte na jutro (zl, w dol do monety); 0 - bez nadwyzki albo bez kotwicy potrzeby;
        /// -1 - kiesa nieczynna, osada bez kiesy, wies albo zamek przy wylaczonym zaworze zamkow. Tylko odczyt.</summary>
        internal static long WaterfrontRoom(Settlement st)
        {
            try
            {
                if (st == null || !Live) return -1;
                Folk f;
                if (!_f.TryGetValue(st, out f) || f == null || f.Kind < 0 || f.Kind > 1) return -1;
                if (f.Kind == 1 && !CastleLive) return -1;
                if (!(f.Need[0] > 0)) return 0;                                   // bez kotwicy potrzeby nie ma miary zapasu - nic nie bierzemy
                var s = Settings.Current;
                double rdW = Math.Max(1, Math.Min(60, s != null ? s.TownFolkReserveDays : 10));
                double rd = f.Kind == 1 ? RdC : rdW;
                double r = PurseOf(f, 0) - rd * f.Need[0] - Math.Max(0, f.Offer[0]);
                return r > 0 ? (long)Math.Floor(r) : 0;
            }
            catch (Exception e) { Stumble("WaterfrontRoom", e); return -1; }
        }
    }
}
