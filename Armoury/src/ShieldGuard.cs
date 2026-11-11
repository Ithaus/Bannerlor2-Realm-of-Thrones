using System;
using HarmonyLib;
using TaleWorlds.Core;
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
    /// <summary>
    /// 414 (audyt RBM C2): oslona byla za szeroka - ciela do MissileShieldDamagePercent KAZDY pocisk, takze oszczep (RBM x25, zamysl "pilum"),
    /// topor rzucany (x10) i strzale olbrzyma. Skutki: oszczep robil tarczy 33 pkt zamiast 3375, a pocisk z flaga CanPenetrateShield nigdy nie
    /// przechodzil przez tarcze, bo silnik (Mission.MissileHitCallback) porownuje z progiem przebicia wlasnie te scieta liczbe.
    /// Regula (klucz ShieldGuardArrowsOnly TAK): strzala i belt (WeaponClass.Arrow / Bolt) -> MissileShieldDamagePercent jak dotad;
    /// pocisk z flaga CanPenetrateShield (strzala olbrzyma, balista) -> nietkniety; kazdy inny pocisk (oszczep, topor, noz, wlocznia rzucona,
    /// kamien) -> ThrownShieldDamagePercent (domyslnie 100 = wynik RBM / silnika bez zmiany). Klucz NIE = stan sprzed 414: jeden procent na wszystko.
    /// Ta sama klasyfikacja (Classify) idzie do zuzycia tarczy w FieldCraft.OnAgentHit. Regula nie zna wlasciciela pocisku - gracz i AI jednakowo.
    /// 414-p (recenzja): Classify patrzy tylko na flage w DANYCH broni. Flage CanPenetrateShield nadawana w locie (AgentApplyDamageModel.DecideMissileWeaponFlags:
    /// perk Throwing.Impale u bohatera, CrewOfSpears w bitwie morskiej NavalDLC) jest tu niewidoczna - taki oszczep to "bron miotana"; przy domyslnych 100%
    /// bez skutku, przy obnizonym Thrown Shield Damage Percent perk slabnie (prog przebicia porownuje scieta liczbe). Swiadomie bez sprawdzania perka w goracej
    /// sciezce. Tarcza na plecach: prefiks RBM na Mission.MissileHitCallback (rbm.cs:4865-4880) przepisuje takie trafienie na blok tarcza, wiec idzie przez
    /// ComputeBlowDamageOnShield i przez nasz postfiks jak kazdy blok (wyjatek RBM: pocisk przebijajacy / obszarowy i rzucajacy z Impale).
    /// Liczniki rosna takze poza kampania (bitwa wlasna - Logic rejestrowana tylko w kampanii); pierwsza misja kampanii je zeruje - tylko dokladnosc linii.
    /// </summary>
    internal static class ShieldGuard
    {
        internal const int KindArrow = 0, KindThrown = 1, KindPiercing = 2;

        // liczniki misji (glowny watek: ComputeBlowDamageOnShield wola tylko Mission.GetAttackCollisionResults)
        private static int _arrowN, _thrownN, _pierceN;
        private static long _arrowIn, _arrowOut, _thrownIn, _thrownOut, _pierceSum;

        /// <summary>414: rodzaj pocisku i ulamek obrazen tarczy. Bez alokacji; null (pocisk bez danych broni) = jak strzala, czyli po staremu.</summary>
        internal static float Classify(Settings c, WeaponComponentData w, out int kind)
        {
            kind = KindArrow;
            if (c.ShieldGuardArrowsOnly && w != null)
            {
                if ((w.WeaponFlags & WeaponFlags.CanPenetrateShield) != 0) { kind = KindPiercing; return 1f; }
                var wc = w.WeaponClass;
                if (wc != WeaponClass.Arrow && wc != WeaponClass.Bolt)
                {
                    kind = KindThrown;
                    return Math.Max(0f, Math.Min(100f, c.ThrownShieldDamagePercent)) / 100f;
                }
            }
            return Math.Max(0f, Math.Min(100f, c.MissileShieldDamagePercent)) / 100f;
        }

        /// <summary>Postfix: obrazenia pocisku na tarczy wedlug rodzaju pocisku (strzala / belt - MissileShieldDamagePercent).</summary>
        public static void SoftenMissile(ref AttackCollisionData attackCollisionData, WeaponComponentData attackerWeapon, ref int inflictedDamage)
        {
            try
            {
                var c = Settings.Current;
                if (c == null || !c.ShieldMissileGuardEnabled) return;
                if (inflictedDamage <= 0) return;
                if (!attackCollisionData.IsMissile) return;
                int kind;
                float pct = Classify(c, attackerWeapon, out kind);
                if (kind == KindPiercing) { _pierceN++; _pierceSum += inflictedDamage; return; }
                int cut = pct >= 1f ? inflictedDamage : (int)(inflictedDamage * pct);
                if (cut < 0) cut = 0;
                if (kind == KindThrown) { _thrownN++; _thrownIn += inflictedDamage; _thrownOut += cut; }
                else { _arrowN++; _arrowIn += inflictedDamage; _arrowOut += cut; }
                inflictedDamage = cut;
            }
            catch { }
        }

        private static void Reset()
        {
            _arrowN = 0; _thrownN = 0; _pierceN = 0;
            _arrowIn = 0; _arrowOut = 0; _thrownIn = 0; _thrownOut = 0; _pierceSum = 0;
        }

        private static void Flush(Mission m)
        {
            if (_arrowN == 0 && _thrownN == 0 && _pierceN == 0) return;      // misja bez pocisku w tarczy - bez linii
            var s = Settings.Current;
            string kind = "?";
            try { kind = MissionTeams.Kind(m); } catch { }
            Log.Info("Tarcze (414): koniec misji (" + kind + ") | tylko strzaly i belty " + (s != null && s.ShieldGuardArrowsOnly ? "TAK" : "NIE")
                     + " | strzaly i belty w tarcze " + _arrowN + " (obrazenia tarcz " + _arrowIn + " -> " + _arrowOut + ")"
                     + " | miotane w tarcze " + _thrownN + " (" + _thrownIn + " -> " + _thrownOut
                     + (_thrownN > 0 ? ", srednio " + (_thrownOut / _thrownN) + " na rzut" : "") + ")"
                     + " | przebijajace tarcze " + _pierceN + " (bez ulgi, " + _pierceSum + ").");
            Reset();
        }

        /// <summary>414: liczniki misji - zerowanie na starcie, linia sumy na koncu.</summary>
        internal sealed class Logic : MissionLogic
        {
            public override MissionBehaviorType BehaviorType { get { return MissionBehaviorType.Other; } }

            public override void OnBehaviorInitialize()
            {
                base.OnBehaviorInitialize();
                Reset();
            }

            protected override void OnEndMission()
            {
                try { Flush(Mission); } catch (Exception e) { Log.Error("ShieldGuard.Flush", e); }
                base.OnEndMission();
            }
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
                         + "% obrazen tarczy (RBM liczyl 150%, goly silnik 15%); bron biala bez zmian; 414: "
                         + (c.ShieldGuardArrowsOnly
                            ? "bron miotana " + c.ThrownShieldDamagePercent + "%, pociski przebijajace tarcze bez ulgi."
                            : "klucz Shield Guard Arrows Only NIE - kazdy pocisk jak strzala (stan sprzed 414)."));
            }
            catch (Exception e) { Log.Error("ShieldGuard.ApplyAll", e); }
        }
    }
}
