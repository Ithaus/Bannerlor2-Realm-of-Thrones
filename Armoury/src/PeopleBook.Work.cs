using System;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// 435-L10 (migracja za praca, WorkMigration.cs): ludzie przeniesieni miedzy regionami (wies -> wies w promieniu, wies -> miasto targowe innego regionu niz jej
    /// warownia) - rownanie regionu (10.5) dostaje WorkOut regionu zrodla i WorkIn regionu celu (435-L10-p: osobno od MigOut/MigIn wedrowcow BK, ten sam znak w rownaniu). Ten sam region - nic (ruch wewnatrz DOM
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

        /// <summary>435-L10-p (recenzja): cofniecie licznika doby `men` ludzi przyczyny `cause`, gdy Move zaksiegowal ruch, a hearth sie nie ruszyl (wyjatek w Move -
        /// zwrot 0). Bez tego ksiega pokazywala wyjscie bez wejscia (MWorkOut - MWorkIn != 0) dla ludzi, ktorzy nigdzie nie poszli. Tylko licznik ludzi (hearth 0).</summary>
        internal static void WorkUnbook(double men, int cause)
        {
            if (!(men > 0.0) || double.IsInfinity(men) || cause < 0 || cause >= MKinds) return;
            try { lock (Gate) { _mMen[cause] -= men; } }
            catch (Exception e) { Stumble("WorkUnbook", e); }
        }

        internal static void NoteWorkRegion(Settlement from, Settlement to, double men)
        {
            if (from == null || to == null || !(men > Eps) || double.IsInfinity(men) || !OriginLive) return;
            try
            {
                string a = RegKey(OutlawLaw.RegionFor(from)), b = RegKey(OutlawLaw.RegionFor(to));
                if (a.Length == 0 || b.Length == 0 || a == b) return;
                Rg(a).WorkOut += men; Rg(b).WorkIn += men;     // 435-L10-p: osobne pola (dotad MigOut/MigIn razem z R19 - linia nie rozrozniala)
            }
            catch (Exception e) { OStumble("NoteWorkRegion", e); }
        }
    }
}
