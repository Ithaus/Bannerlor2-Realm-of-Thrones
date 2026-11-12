using System;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// 435-L10 (migracja za praca, WorkMigration.cs): ludzie przeniesieni miedzy regionami (wies -> wies w promieniu, wies -> miasto targowe innego regionu niz jej
    /// warownia) - rownanie regionu (10.5) dostaje MigOut regionu zrodla i MigIn regionu celu, jak wedrowcy BK u celu (ArriveAt). Ten sam region - nic (ruch wewnatrz DOM
    /// regionu). Tylko po siewie kont z pochodzeniem (OriginLive) - przed siewem rownania regionow jeszcze nie ma. Watek glowny, nie rzuca.
    /// </summary>
    internal static partial class PeopleBook
    {
        /// <summary>Linia "LP migracja (435-L10)": ludzie przyczyn MWorkOut / MWorkIn w liczniku doby ksiegi (od ostatniej linii "Ludzie (ksiega)"). Sam odczyt.</summary>
        internal static void WorkMen(out double outMen, out double inMen)
        {
            outMen = inMen = 0;
            try { lock (Gate) { outMen = _mMen[MWorkOut]; inMen = _mMen[MWorkIn]; } }
            catch (Exception e) { Stumble("WorkMen", e); }
        }

        internal static void NoteWorkRegion(Settlement from, Settlement to, double men)
        {
            if (from == null || to == null || !(men > Eps) || double.IsInfinity(men) || !OriginLive) return;
            try
            {
                string a = RegKey(OutlawLaw.RegionFor(from)), b = RegKey(OutlawLaw.RegionFor(to));
                if (a.Length == 0 || b.Length == 0 || a == b) return;
                Rg(a).MigOut += men; Rg(b).MigIn += men;
            }
            catch (Exception e) { OStumble("NoteWorkRegion", e); }
        }
    }
}
