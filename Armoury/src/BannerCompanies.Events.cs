using System;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// noc 25.10 P0 (krytyk K6): zdarzenia choragwi dla dzwieku (okrzyki "Their banner is down!", "They break!", rog zbiorki). PLIK INTEGRATORA - paczki go nie ruszaja.
    /// P3 (BannerCompaniesMission, glowny watek) wola je raz na zdarzenie: company = numer kompanii w formacji (0..5), pos = miejsce zdarzenia
    /// (sztandar / dowodca - gdzie padl, zlamanie - srodek kompanii). Odbiorca BattleSound (P2), we wlasnym try, potkniecie liczone.
    /// </summary>
    internal static partial class BannerCompanies
    {
        /// <summary>Padl sztandar kompanii (chorazy zabity, omdlaly albo upuscil sztandar).</summary>
        internal static void StandardFell(Formation f, int company, Vec3 pos)
        {
            if (f == null) return;
            try { BattleSound.OnStandardFell(f, company, pos); } catch (Exception e) { RoutHold.Stumble(RoutHold.SiteSound, "BattleSound.OnStandardFell", e); }
        }

        /// <summary>Padl dowodca kompanii (zabity albo omdlaly).</summary>
        internal static void CommanderFell(Formation f, int company, Vec3 pos)
        {
            if (f == null) return;
            try { BattleSound.OnCommanderFell(f, company, pos); } catch (Exception e) { RoutHold.Stumble(RoutHold.SiteSound, "BattleSound.OnCommanderFell", e); }
        }

        /// <summary>Kompania sie zalamala (rzut na zalamanie wypadl).</summary>
        internal static void CompanyBroke(Formation f, int company, Vec3 pos)
        {
            if (f == null) return;
            try { BattleSound.OnCompanyBroke(f, company, pos); } catch (Exception e) { RoutHold.Stumble(RoutHold.SiteSound, "BattleSound.OnCompanyBroke", e); }
        }
    }
}
