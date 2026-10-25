using System;
using System.Text;
using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// noc 25.10 P0 (baza nocy, krytyk K3): JEDNO miejsce zakladania latek paczek bitwy nocy 25.10.
    ///  - Latki misji (P1 MarchToContact i Doctrine: AdjustSpeedLimit, CacheMovementSpeedOfUnits, GetTacticWeight, Attack; P2 BattleSound: rog gry, jesli kiedys)
    ///    zakladamy przy PIERWSZYM AfterStart BattleSense w bitwie (BattleSense.AfterStart), nie w SubModuleMain: TacticComponent ma konstruktor statyczny
    ///    z SoundEvent.GetEventIdFromString (TacticComponent.cs:71-75) - kompilacja latki przez Harmony przy starcie gry moglaby go uruchomic za wczesnie
    ///    (ta sama pulapka co DefaultClanFinanceModel, CLAUDE.md 7).
    ///  - Latki kampanii (P3 BannerKit: SandboxBattleBannerBearersModel, DefaultFormationArrangementModel, prywatna metoda DTE) - raz na proces
    ///    w ArmouryBehavior.OnSessionLaunched (wzor WorkshopTrade.ApplyInCampaign).
    ///  - FriendlyFire (P6) NIE tutaj: transpilery na Mission.*HitCallback musza wejsc przed JIT wolajacych - SubModuleMain obok ChargeTemperPatch.
    /// Kazda paczka zwraca krotki opis swoich latek (np. "marsz 2/2"), sama liczy latki, ktore nie weszly (AccessTools.DeclaredMethod na klasie deklarujacej,
    /// Harmony.GetPatchInfo - pulapka 15.10) i sama je zdejmuje tym samym MethodInfo. Wyjatek paczki liczony (RoutHold.Stumble jej miejsca), reszta paczek
    /// wpinana dalej - bez globalnego wylacznika. Jedna linia logu na proces.
    /// </summary>
    internal static class NightHooks
    {
        private static Harmony _h;
        private static bool _missionDone, _campaignDone;

        /// <summary>140: instancja Harmony dla latek zakladanych w bitwie poza FirstBattle (ShieldsUp.EnsurePatched - raz na proces przy pierwszej
        /// bitwie z kluczem, K9: klucz wylaczony w pierwszej bitwie nie blokuje latek do konca sesji).</summary>
        internal static Harmony H { get { return _h; } }

        /// <summary>SubModuleMain.OnBeforeInitialModuleScreenSetAsRoot: tylko zapamietanie instancji Harmony (nic nie lata).</summary>
        internal static void SetHarmony(Harmony h)
        {
            if (h != null && _h == null) _h = h;
        }

        /// <summary>BattleSense.AfterStart pierwszej bitwy (glowny watek, raz na proces): latki misji P1 i P2.</summary>
        internal static void FirstBattle(Mission m)
        {
            if (_missionDone) return;
            _missionDone = true;
            if (_h == null) { Log.Info("Latki nocy (K3): brak instancji Harmony - latki misji marszu, doktryny i dzwieku NIE wpiete."); return; }
            var sb = new StringBuilder(400);
            sb.Append("Latki nocy (K3): misja - pierwsza bitwa (").Append(MissionTeams.Kind(m)).Append(") | ");
            Part(sb, "marsz", RoutHold.SiteMarch, () => MarchToContact.ApplyMissionPatches(_h));
            sb.Append(" | ");
            Part(sb, "doktryna", RoutHold.SiteDoctrine, () => Doctrine.ApplyMissionPatches(_h));
            sb.Append(" | ");
            Part(sb, "dzwiek", RoutHold.SiteSound, () => BattleSound.ApplyMissionPatches(_h));
            sb.Append('.');
            Log.Info(sb.ToString());
        }

        /// <summary>ArmouryBehavior.OnSessionLaunched (raz na proces - latki Harmony przezywaja zmiane kampanii): latki kampanii P3.</summary>
        internal static void InCampaign()
        {
            if (_campaignDone) return;
            _campaignDone = true;
            if (_h == null) { Log.Info("Latki nocy (K3): brak instancji Harmony - latki choragwi NIE wpiete."); return; }
            var sb = new StringBuilder(300);
            sb.Append("Latki nocy (K3): kampania | ");
            Part(sb, "choragwie", RoutHold.SiteBanner, () => BannerKit.ApplyInCampaign(_h));
            sb.Append(" | ");
            Part(sb, "przypiecia typow (P12)", RoutHold.SiteBanner, () => ArmyReview.ApplyPinHooks(_h));   // spawn i posilki czynnego BattleSpawnModel, ekran Order of Battle
            sb.Append('.');
            Log.Info(sb.ToString());
        }

        private static void Part(StringBuilder sb, string name, int site, Func<string> apply)
        {
            try
            {
                string r = apply();
                sb.Append(name).Append(": ").Append(string.IsNullOrEmpty(r) ? "-" : r);
            }
            catch (Exception e)
            {
                sb.Append(name).Append(": BLAD (").Append(e.GetType().Name).Append(')');
                RoutHold.Stumble(site, "NightHooks." + name, e);
            }
        }
    }
}
