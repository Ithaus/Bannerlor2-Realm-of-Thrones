using System;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// noc 25.10 P0 (krytyk K4): JEDEN detektor szarzy. PLIK INTEGRATORA - paczki go nie ruszaja.
    /// BattleCries (P2, wlasciciel BattleCries.cs) wola ChargeStarted(f) raz na przejscie szyku na Charge / ChargeToTarget - dla szykow AI ORAZ gracza
    /// (okrzyk tylko AI, jak dotad) i takze przy BattleCriesAndMoans NIE (wtedy bez okrzyku, ale z sygnalem): marsz (P1) zdejmuje wtedy cap, a dzwiek (P2)
    /// gra rog i rozkaz kapitana. Zadnego drugiego progu odleglosci ani drugiego detektora w MarchToContact / BattleSound.
    /// Kolejnosc w BattleSense.OnMissionTick: BattleCries -> MarchToContact -> Doctrine -> BattleSound, wiec obaj odbiorcy widza szarze w tym samym takcie.
    /// </summary>
    internal static partial class BattleCries
    {
        internal static void ChargeStarted(Formation f)
        {
            if (f == null) return;
            try { MarchToContact.OnChargeStarted(f); } catch (Exception e) { RoutHold.Stumble(RoutHold.SiteMarch, "MarchToContact.OnChargeStarted", e); }
            try { BattleSound.OnChargeStarted(f); } catch (Exception e) { RoutHold.Stumble(RoutHold.SiteSound, "BattleSound.OnChargeStarted", e); }
        }
    }
}
