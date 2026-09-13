using System;
using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// TARCZA NIE JEST OD STRZAL (Jeff 13.09: "strzaly maja nie rozwalac tarczy,
    /// albo uszkodzenia strzal i beltow tarczy ustaw na 1%").
    /// WINOWAJCA - RBM: podmienia silnikowe liczenie obrazen tarczy (prefix na
    /// MissionCombatMechanicsHelper.ComputeBlowDamageOnShield, ktory zwraca false)
    /// i mnozy trafienie strzala x1.5, beltem x1.5 - a goly silnik liczy strzale
    /// x0.15. Dziesieciokrotnie wiecej w tym samym miejscu, stad tarcze sypiace sie
    /// pod ostrzalem, podczas gdy pchniecie wloczni (x0.09) ledwie je rysuje.
    /// W konfiguracji RBM nie ma na to ZADNEGO pokretla - mnozniki sa wpisane
    /// na sztywno w RBMCombat.dll (sprawdzone: jedyny klucz z "shield" w nazwie
    /// to PassiveShoulderShields, o czym innym).
    /// Latka siedzi na METODZIE SILNIKA, nie na RBM: postfixy Harmony biegna takze
    /// wtedy, gdy czyjs prefix pominal cialo metody, wiec lapiemy i wersje RBM,
    /// i vanillowa, i kazda inna. Tniemy WYLACZNIE trafienia pociskiem - topory
    /// i maczugi rozlupuja tarcze dalej, tak jak RBM zamierzal.
    /// BEZPIECZENSTWO (sprawdzone w dekompilacji, MCMH linia 208-209): wywolujacy
    /// zaraz po nas robi "AbsorbedByArmor = InflictedDamage", wiec obie liczby
    /// maleja RAZEM i zablokowany cios dalej NIE dosiega czlowieka - nie jest tak,
    /// ze scieta liczba przecieka strzalem w gracza.
    /// </summary>
    internal static class ShieldGuard
    {
        private static int _softened;

        /// <summary>Postfix: obrazenia pocisku na tarczy schodza do MissileShieldDamagePercent.</summary>
        public static void SoftenMissile(ref AttackCollisionData attackCollisionData, ref int inflictedDamage)
        {
            try
            {
                var c = Settings.Current;
                if (c == null || !c.ShieldMissileGuardEnabled) return;
                if (inflictedDamage <= 0) return;
                if (!attackCollisionData.IsMissile) return;
                float pct = Math.Max(0f, Math.Min(100f, c.MissileShieldDamagePercent)) / 100f;
                int cut = (int)(inflictedDamage * pct);
                inflictedDamage = cut < 0 ? 0 : cut;
                _softened++;
            }
            catch { }
        }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var c = Settings.Current;
                if (c == null || !c.ShieldMissileGuardEnabled)
                { Log.Info("Tarcze: bez ulgi na strzaly (ShieldGuard wylaczony)."); return; }

                var m = AccessTools.Method(typeof(MissionCombatMechanicsHelper), "ComputeBlowDamageOnShield");
                if (m == null)
                { Log.Info("Tarcze: ComputeBlowDamageOnShield nieznaleziony - strzaly lupia tarcze po staremu."); return; }

                // Priority.Last: chcemy ciac liczbe, ktora zostala po WSZYSTKICH
                // innych postfixach, zeby nikt nam jej potem nie podniosl
                h.Patch(m, postfix: new HarmonyMethod(typeof(ShieldGuard), "SoftenMissile") { priority = Priority.Last });
                Log.Info("Tarcze: strzaly i belty robia " + c.MissileShieldDamagePercent
                         + "% obrazen tarczy (RBM liczyl 150%, goly silnik 15%); bron biala bez zmian.");
            }
            catch (Exception e) { Log.Error("ShieldGuard.ApplyAll", e); }
        }
    }
}
