using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// TEMPO SWIATA (Jeff 31.08: "nasz czas leci x2 wzgledem zwyklego, bylo x4
    /// - marsz i oblezenia trzeba uwzglednic"). Rok wydluzylismy do 168 dni,
    /// ale mapa zostala vanillowa: armia robila ~340 km na dzien gry (skala
    /// z lore: Mur->Sunspear = 1012 jedn. = ~4800 km, czyli ~4.75 km/jedn.),
    /// Winterfell->Krolewska Przystan w 6.5 dnia zamiast ksiazkowego miesiaca.
    /// Dwa suwaki, oba domyslnie 50%:
    ///  - WorldPacePercent: predkosc BAZOWA kazdej partii x50% - po tym
    ///    Winterfell->KP to ~13 dni ze 168-dniowego roku, czyli dokladnie
    ///    lore'owy miesiac jako ulamek kalendarza; teren, ladunek, kary snu
    ///    i sufit kolumny licza sie dalej od wolniejszej bazy;
    ///  - SiegePacePercent: budowa machin obleczniczych x50% - oblezenia
    ///    trwaja ~2x dluzej, wiec glodzenie twierdzy wraca do gry.
    /// Dotyczy WSZYSTKICH rowno (gracz, AI, karawany, wieśniacy) - swiat
    /// zwalnia jednym rytmem.
    /// </summary>
    internal static class WorldPace
    {
        private static readonly TextObject _txtPace = new TextObject("{=!}World pace");

        public static void BasePostfix(ref ExplainedNumber __result)
        {
            try
            {
                if (!SpeedDepth.OutermostBase) return;          // lancuch modeli: tylko raz
                var s = Settings.Current;
                int p = s != null ? s.WorldPacePercent : 100;
                if (p >= 100 || p < 5) return;
                __result.AddFactor(p / 100f - 1f, _txtPace);
            }
            catch { }
        }

        public static void SiegePostfix(ref float __result)
        {
            try
            {
                var s = Settings.Current;
                int p = s != null ? s.SiegePacePercent : 100;
                if (p >= 100 || p < 5) return;
                __result *= p / 100f;
            }
            catch { }
        }

        internal static void ApplyAll(Harmony harmony)
        {
            try
            {
                int spd = Patch(harmony, typeof(PartySpeedModel), "CalculateBaseSpeed", "BasePostfix");
                int sie = Patch(harmony, typeof(SiegeEventModel), "GetConstructionProgressPerHour", "SiegePostfix");
                Log.Info("WorldPace: mapa " + Settings.Current.WorldPacePercent + "% (" + spd
                         + " modeli), budowa oblezen " + Settings.Current.SiegePacePercent + "% (" + sie + " modeli).");
            }
            catch (Exception e) { Log.Error("WorldPace.ApplyAll", e); }
        }

        private static int Patch(Harmony harmony, Type baseType, string method, string postfixName)
        {
            int done = 0;
            var post = new HarmonyMethod(typeof(WorldPace).GetMethod(postfixName,
                BindingFlags.Public | BindingFlags.Static));
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException rtle) { types = rtle.Types; }
                catch { continue; }
                foreach (var t in types)
                {
                    if (t == null || t.IsAbstract || !baseType.IsAssignableFrom(t)) continue;
                    try
                    {
                        var m = t.GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic |
                                                    BindingFlags.Instance | BindingFlags.DeclaredOnly);
                        if (m == null || m.IsAbstract) continue;
                        harmony.Patch(m, postfix: post);
                        done++;
                    }
                    catch (Exception e) { Log.Error("WorldPace.Patch(" + t.Name + ")", e); }
                }
            }
            return done;
        }
    }
}
