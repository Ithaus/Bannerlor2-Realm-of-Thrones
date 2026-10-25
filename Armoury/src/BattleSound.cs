using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// noc 25.10 P2 "dzwiek" (projekt dzwiek-glosy.json; klucze BattleSoundsEnabled - glowny, BattleHornsCulture, BattleDrumsMarch, BattleCaptainOrders,
    /// BattleHouseSpeeches, BattleChargeMottos, BattleWoundedAfter, BattleRavensAfter, BattleWindAndRain, BattleHorseSounds, BattleClashOfArms, SiegeFireSounds,
    /// BattleSoundsLoudness): bebny, rogi kultur, rozkazy kapitanow, przemowy rodow, motta przy szarzy, jeki po bitwie, kruki, tlo pola, konie, szczek broni,
    /// ogien oblezenia. v1 BEZ project.mbproj i module_sounds.xml - pliki ModuleData/Lektor/Bitwa/<kategoria>/*.ogg przez SoundEvent.CreateEventFromExternalFile
    /// (wzor Lektor.cs / EventMusic.Bitwa.cs). SZKIELET P0 - tresc wypelnia paczka P2 (wlasciciel BattleSound*.cs).
    ///
    /// KONTRAKT (sygnatury ustalone w P0; wszystko z BattleSense, kazde wolanie we wlasnym try z RoutHold.SiteSound; wolane tylko w bitwie, nie w pojedynku):
    ///  - ApplyMissionPatches(h)    - z NightHooks.FirstBattle (raz na proces); w v1 bez prefiksu rogow gry (krytyk K8) - zwraca np. "bez latek".
    ///  - Begin(m)                  - AfterStart (po WarCouncil.Council).
    ///  - Tick(m, dt)               - OnMissionTick, PO BattleCries i MarchToContact / Doctrine (K4) - tu jedyne CreateEvent / Play / Release.
    ///  - OnDeploymentFinished(m)   - przemowy (+3 s).
    ///  - OnShoot / OnMissileHit / OnPanicked / OnFleeing / OnRemoved - bardzo czeste i moga przyjsc z ticku asynchronicznego: tylko O(1) i kolejka pod lockiem.
    ///  - OnClearScene(m) i End(m)  - Release WSZYSTKICH zdarzen przed zniszczeniem sceny (OnClearScene) i na koncu misji (OnEndMission / OnRemoveBehavior).
    ///  - Zdarzenia innych paczek (dyspozytory w plikach integratora): OnChargeStarted (BattleCries.ChargeStarted), OnStandardFell / OnCommanderFell /
    ///    OnCompanyBroke (BannerCompanies.*), OnArchersFellBack / OnFlankRide (Doctrine.*) - glowny watek, tylko zapis do kolejki / stanu, dzwiek w Tick.
    /// Gracz (MainAgent) nigdy nie mowi; kobiety nie mowia naszymi meskimi glosami. Linia "Dzwiek bitwy" raz na misje (wlasna, nie w NB2).
    /// </summary>
    internal static partial class BattleSound
    {
        internal static string ApplyMissionPatches(Harmony h) { return "szkielet P0 (bez latek)"; }

        internal static void Begin(Mission m) { }

        internal static void Tick(Mission m, float dt) { }

        internal static void OnDeploymentFinished(Mission m) { }

        internal static void OnShoot(Mission m, Agent shooter, EquipmentIndex weaponIndex) { }

        internal static void OnMissileHit(Mission m, Agent attacker, Agent victim, bool isCanceled) { }

        internal static void OnPanicked(Mission m, Agent a) { }

        internal static void OnFleeing(Mission m, Agent a) { }

        internal static void OnRemoved(Mission m, Agent affected, Agent affector, AgentState state) { }

        internal static void OnClearScene(Mission m) { }

        internal static void End(Mission m) { }

        // ---- zdarzenia innych paczek (glowny watek)

        internal static void OnChargeStarted(Formation f) { }

        internal static void OnStandardFell(Formation f, int company, Vec3 pos) { }

        internal static void OnCommanderFell(Formation f, int company, Vec3 pos) { }

        internal static void OnCompanyBroke(Formation f, int company, Vec3 pos) { }

        internal static void OnArchersFellBack(Formation f) { }

        internal static void OnFlankRide(Formation f) { }
    }
}
