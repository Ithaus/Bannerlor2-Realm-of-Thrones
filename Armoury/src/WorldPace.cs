using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
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
    ///
    /// 17.09 (Jeff: "strasznie wolno trwa oblezenie, taran buduje sie kilka
    /// dni"): SiegePostfix byl podpiety pod KAZDY model z wlasnym
    /// GetConstructionProgressPerHour (log: "budowa oblezen 50% (2 modeli)"),
    /// a RealisticBannerlord.RealisticSiegeEventModel wola w srodku bazowy
    /// DefaultSiegeEventModel - nasze 50% wchodzilo wiec DWA razy (x0.25),
    /// do tego RB mnozy przez swoj suwak SiegeConstructionSpeedMultiplier
    /// (MCM Jeffa 0.4; oblegajacy bez Tools w sakwach jeszcze x0.75).
    /// Razem 0.5 x 0.4 x 0.75 x 0.5 = 7.5% tempa gry: taran (12 osobodni,
    /// 300 ludzi -> vanilla ~17 h) budowal sie ~9 dni, przygotowania obozu
    /// (48 osobodni) ~37 dni. Teraz: licznik SpeedDepth.OutermostSiege -
    /// suwak liczy sie raz, na najbardziej zewnetrznym poziomie lancucha.
    /// Do logu (raz na typ machiny i sesje, tylko oblezenie gracza): tempo
    /// z gry (juz po RB) i po naszym suwaku, w % postepu na godzine.
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

        private static readonly HashSet<string> _siegeLogged = new HashSet<string>();

        public static void SiegePostfix(ref float __result, SiegeEngineType type, SiegeEvent siegeEvent, ISiegeEventSide side)
        {
            try
            {
                if (!SpeedDepth.OutermostSiege) return;         // lancuch modeli (RB -> Default): tylko raz
                var s = Settings.Current;
                int p = s != null ? s.SiegePacePercent : 100;
                float before = __result;
                if (p < 100 && p >= 5) __result *= p / 100f;
                // slad w logu: tylko oblezenie gracza, raz na typ machiny
                try
                {
                    if (siegeEvent == null || type == null || side == null || !siegeEvent.IsPlayerSiegeEvent) return;
                    string key = type.StringId + "|" + side.BattleSide;
                    if (_siegeLogged.Contains(key)) return;
                    _siegeLogged.Add(key);
                    int men = 0;
                    try { men = MobileParty.MainParty != null ? MobileParty.MainParty.MemberRoster.TotalHealthyCount : 0; } catch { }
                    float hoursNow = __result > 0f ? 1f / __result : 0f;
                    Log.Info("WorldPace: budowa " + type.StringId + " (" + type.ManDayCost + " osobodni, strona " + side.BattleSide + ", zdrowych " + men
                             + "): gra (z innymi modami) " + (before * 100f).ToString("0.00") + "%/h -> po suwaku " + p + "% " + (__result * 100f).ToString("0.00")
                             + "%/h = ~" + hoursNow.ToString("0") + " h (" + (hoursNow / 24f).ToString("0.0") + " dnia) od zera.");
                }
                catch { }
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
