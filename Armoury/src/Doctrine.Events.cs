using System;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// noc 25.10 P0 (krytyk K6): zdarzenia doktryny dla dzwieku. PLIK INTEGRATORA - paczki go nie ruszaja.
    /// P1 (Doctrine.Tick, glowny watek) wola je w chwili manewru; odbiorca BattleSound (P2) - kazdy we wlasnym try, potkniecie liczone.
    /// </summary>
    internal static partial class Doctrine
    {
        /// <summary>S4: szyk lucznikow AI cofa sie za piechote (raz na wycofanie, nie co tick).</summary>
        internal static void ArchersFellBack(Formation f)
        {
            if (f == null) return;
            try { BattleSound.OnArchersFellBack(f); } catch (Exception e) { RoutHold.Stumble(RoutHold.SiteSound, "BattleSound.OnArchersFellBack", e); }
        }

        /// <summary>S3: konnica AI rusza na skrzydlo (raz na manewr).</summary>
        internal static void FlankRide(Formation f)
        {
            if (f == null) return;
            try { BattleSound.OnFlankRide(f); } catch (Exception e) { RoutHold.Stumble(RoutHold.SiteSound, "BattleSound.OnFlankRide", e); }
        }
    }
}
