using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// TEMPO SWIATA (Jeff 31.08: "nasz czas leci x2 wzgledem zwyklego, bylo x4
    /// - marsz i oblezenia trzeba uwzglednic"). Rok wydluzylismy (31.08 do 168 dni,
    /// dzis 364 dni - Calendar.cs, WeeksPerSeason 13, jeden dzien to jeden dzien),
    /// ale mapa zostala vanillowa: armia robila ~340 km na dzien gry (skala
    /// z lore: Mur->Sunspear = 1012 jedn. = ~4800 km, czyli ~4.75 km/jedn.),
    /// Winterfell->Krolewska Przystan w 6.5 dnia zamiast ksiazkowego miesiaca.
    /// Dwa suwaki, oba domyslnie 50%:
    ///  - WorldPacePercent: predkosc BAZOWA kazdej partii x50% - po tym
    ///    Winterfell->KP to ~13 dni (pomiar 31.08; przy 75% Jeffa ~8.7 dnia).
    ///    Przy roku 168 bylo to "miesiac lore jako ulamek kalendarza"; przy roku
    ///    364 ta zbieznosc nie zachodzi (E7/C: tekst MCM juz jej nie obiecuje) -
    ///    suwak to po prostu predkosc bazowa; teren, ladunek, kary snu
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

        /// <summary>564 (Jeff 15.11, punkt 1 "ok"): modele predkosci z latka FinalPostfix (0 = latka nie weszla - zawsze stary tryb, bo inaczej tempo
        /// zniknieloby z obu miejsc naraz).</summary>
        private static int _finalModels;

        /// <summary>564: tempo swiata jako mnoznik CALEJ predkosci (klucz WorldPaceWholeSpeed i latka FinalPostfix wpieta). false = jak przed 564:
        /// wspolczynnik w predkosci bazowej (BasePostfix) w sumie z innymi premiami, sufit kolumny MarchPace x tempo.</summary>
        internal static bool Whole(Settings s) { return s != null && s.WorldPaceWholeSpeed && _finalModels > 0; }

        /// <summary>564: mnoznik tempa swiata - ta sama regula co BasePostfix: 5..99 = procent, poza tym 1 (bez zmiany).</summary>
        internal static float Scale(Settings s)
        {
            int p = s != null ? s.WorldPacePercent : 100;
            return p >= 100 || p < 5 ? 1f : p / 100f;
        }

        /// <summary>Recenzja 564 (uwaga 2): druzyna skryptu (CustomPartyComponent z wlasnym BaseSpeed - questy sandbox, NavalDLC). Gra zastepuje jej cala
        /// predkosc (DefaultPartySpeedCalculatingModel.CalculateFinalSpeed), wiec FinalPostfix jej nie rusza; sufit kolumny dostaje wtedy tempo jak przed 564.</summary>
        internal static bool Skips(MobileParty mp)
        {
            if (mp == null || !mp.IsCustomParty) return false;
            var cc = mp.PartyComponent as CustomPartyComponent;
            return cc != null && Math.Abs(cc.BaseSpeed) > 1e-5f;
        }

        /// <summary>Recenzja 564 (uwaga 3): podloga gry (MinimumSpeed 1) na mapie - przy tempie na calej predkosci FinalPostfix mnozy ja x tempo.</summary>
        internal static float MinSpeed(Settings s) { return Whole(s) ? Scale(s) : 1f; }

        /// <summary>Recenzja 564 (uwaga 1): predkosc bazowa z pamieci gry (MobileParty.LastCalculatedBaseSpeed) w jednostkach mapy - dla NASZYCH szacunkow
        /// w logu. Przy kluczu WorldPaceWholeSpeed tempo nie siedzi juz w bazie (BasePostfix wychodzi), tylko w predkosci koncowej - tu x tempo.
        /// Gra (inicjatywa poscigu AI, punkt ucieczki) czyta pole wprost i tego mnoznika NIE dostaje - opisane w CHANGELOG (564, recenzja).</summary>
        internal static float BaseOnMap(MobileParty mp, Settings s)
        {
            float v = mp != null ? mp.LastCalculatedBaseSpeed : 0f;
            return Whole(s) && !Skips(mp) ? v * Scale(s) : v;
        }

        public static void BasePostfix(ref ExplainedNumber __result)
        {
            try
            {
                if (!SpeedDepth.OutermostBase) return;          // lancuch modeli: tylko raz
                var s = Settings.Current;
                if (Whole(s)) return;                           // 564: tempo idzie mnoznikiem calej predkosci (FinalPostfix), nie wspolczynnikiem bazy
                int p = s != null ? s.WorldPacePercent : 100;
                if (p >= 100 || p < 5) return;
                __result.AddFactor(p / 100f - 1f, _txtPace);
            }
            catch { }
        }

        /// <summary>
        /// 564 (Jeff 15.11, punkt 1 "ok"): tempo swiata jako PRAWDZIWY mnoznik calej predkosci koncowej. Dotad BasePostfix dokladal AddFactor(p - 1)
        /// do sumy wspolczynnikow (wynik = baza x (1 + F - 0.25)), wiec premie i kary szly od pelnej bazy, a sufit kolumny MarchPace mnozyl sie x tempo -
        /// dwie skale. Teraz: postfiks Priority.Last na kazdym zadeklarowanym CalculateFinalSpeed, wpiety PO MarchPace i NightRest (ten sam priorytet,
        /// pozniejsza rejestracja = pozniej), raz na lancuch (SpeedDepth.OutermostFinal): wynik bez granic U = B x (1 + F) -> k x U przez
        /// AddFactor((k - 1) x (1 + F)); granice LimitMin / LimitMax tez x k, wiec wynik = k x clamp(U, min, max). Wszystkie wczesniejsze wpisy
        /// (sufit kolumny, droga, bloto, woz, sen, nosze, podloga gry 1) licza sie w jednostkach "pelnego tempa", a tempo mnozy je na koncu raz.
        /// Wpis "World pace" w rozpisce gry = B_koncowe x wspolczynnik = (k - 1) x U - rzeczywisty ubytek predkosci (bez granic).
        /// Wszyscy rowno (gracz, AI, karawany, wsie, morze) - jak dotad; poza druzynami CustomPartyComponent z wlasnym BaseSpeed (gra zastepuje im
        /// cala predkosc, wiec stary wspolczynnik bazy do nich nie dochodzil - zostaja jak byly).
        /// </summary>
        public static void FinalPostfix(MobileParty __0, ref ExplainedNumber __result)
        {
            try
            {
                if (!SpeedDepth.OutermostFinal) return;         // lancuch modeli (ROT -> RB -> vanilla): tylko raz, na zewnatrz
                var s = Settings.Current;
                if (!Whole(s)) return;
                float k = Scale(s);
                if (k >= 1f) return;
                if (Skips(__0)) return;                         // wlasna predkosc druzyny skryptu - jak przed 564 (sufit kolumny x tempo w MarchPace)
                float f = 1f + __result.SumOfFactors;
                __result.AddFactor((k - 1f) * f, _txtPace);
                float lo = __result.LimitMinValue, hi = __result.LimitMaxValue;
                if (lo > 0f && lo < float.MaxValue * 0.5f) __result.LimitMin(lo * k);        // podloga gry (MinimumSpeed 1) -> 1 x tempo
                if (hi > 0f && hi < float.MaxValue * 0.5f) __result.LimitMax(hi * k);        // sufit kolumny / wozu / noszy -> x tempo (raz)
            }
            catch { }
        }

        private static readonly HashSet<string> _siegeLogged = new HashSet<string>();
        private static int _siegeDayLogged = -1;

        /// <summary>17.09 (Jeff: "buduje onager, nie postepuje ani o 1%"): raz na dzien
        /// kampanii, strona atakujaca oblezenia gracza - co gra NAPRAWDE buduje
        /// (ConstructionTick: dopoki przygotowania obozu nie sa gotowe, zadna machina
        /// nie rusza), postep, sila robocza wedle gry (sqrt zdrowych w obozie), partie
        /// w obozie, Tools w sakwach i mnoznik, ktory RealisticBannerlord czyta z MCM
        /// (papier 12:30 i 13:36: "gra 0.55%/h" przed i PO zmianie suwaka 0.4 -> 1.0 -
        /// cos sie nie zgadza, ta linia ma to rozstrzygnac).</summary>
        private static void SiegeDaily(SiegeEvent siegeEvent, ISiegeEventSide side)
        {
            try
            {
                if (side.BattleSide != BattleSideEnum.Attacker) return;
                int day = (int)CampaignTime.Now.ToDays;
                if (day == _siegeDayLogged) return;
                _siegeDayLogged = day;
                var sb = new System.Text.StringBuilder();
                sb.Append("WorldPace: oblezenie ").Append(siegeEvent.BesiegedSettlement != null ? siegeEvent.BesiegedSettlement.Name.ToString() : "?")
                  .Append(" dzien ").Append(day).Append(": ");
                var prep = side.SiegeEngines != null ? side.SiegeEngines.SiegePreparations : null;
                if (prep != null) sb.Append("przygotowania obozu ").Append((prep.Progress * 100f).ToString("0.0")).Append("%").Append(prep.IsConstructed ? " (gotowe)" : " (W BUDOWIE - machiny czekaja)").Append("; ");
                try
                {
                    var building = new List<string>();
                    foreach (var e in side.SiegeEngines.DeployedSiegeEngines)
                        if (e != null && e.SiegeEngine != null && !e.IsConstructed) building.Add(e.SiegeEngine.StringId + " " + (e.Progress * 100f).ToString("0.0") + "%");
                    sb.Append("machiny w budowie: ").Append(building.Count > 0 ? string.Join(", ", building.ToArray()) : "(zadna)").Append("; ");
                }
                catch { }
                try
                {
                    float mdp = Campaign.Current.Models.SiegeEventModel.GetAvailableManDayPower(side);
                    sb.Append("sila robocza wg gry ").Append(mdp.ToString("0.0")).Append(" (sqrt zdrowych) -> ~").Append((mdp * mdp).ToString("0")).Append(" zdrowych; ");
                }
                catch { }
                try
                {
                    var parts = new List<string>();
                    foreach (var p in siegeEvent.BesiegerCamp.GetInvolvedPartiesForEventType())
                        if (p != null) parts.Add(p.Name + " " + p.NumberOfHealthyMembers + "/" + p.NumberOfAllMembers);
                    sb.Append("partie w obozie: ").Append(string.Join(", ", parts.ToArray())).Append("; ");
                }
                catch { }
                try { sb.Append("Tools w sakwach gracza ").Append(MobileParty.MainParty.ItemRoster.GetItemNumber(DefaultItems.Tools)).Append("; "); } catch { }
                try
                {
                    var tRb = AccessTools.TypeByName("RealisticBannerlord.Settings.RealisticSettings");
                    var tGs = AccessTools.TypeByName("MCM.Abstractions.Base.Global.GlobalSettings`1");
                    if (tRb != null && tGs != null)
                    {
                        var inst = AccessTools.Property(tGs.MakeGenericType(tRb), "Instance").GetValue(null, null);
                        var pv = inst != null ? AccessTools.Property(tRb, "SiegeConstructionSpeedMultiplier") : null;
                        sb.Append("RB mnoznik budowy z MCM = ").Append(inst == null ? "(Instance null -> RB bierze 0.4)" : (pv != null ? Convert.ToSingle(pv.GetValue(inst, null)).ToString("0.00") : "?"));
                    }
                    else sb.Append("RB: ").Append(tRb == null ? "typ ustawien nieznaleziony" : "MCM GlobalSettings nieznaleziony");
                }
                catch (Exception e) { sb.Append("RB mnoznik: blad ").Append(e.GetType().Name); }
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Log.Error("WorldPace.SiegeDaily", e); }
        }

        public static void SiegePostfix(ref float __result, SiegeEngineType type, SiegeEvent siegeEvent, ISiegeEventSide side)
        {
            try
            {
                if (!SpeedDepth.OutermostSiege) return;         // lancuch modeli (RB -> Default): tylko raz
                var s = Settings.Current;
                int p = s != null ? s.SiegePacePercent : 100;
                float before = __result;
                if (p < 100 && p >= 5) __result *= p / 100f;
                // OB4 (Jeff 14.10: "realistyczne oblezenia takze dla mnie i czas budowy tez realistyczny"): strona OBLEGAJACA - AI i gracz - x SiegeBesiegerPrepPercent
                // / klasa twierdzy (miasto 1.5, stolica 2.5); bez Innych i obozow skryptu fabuly; raz na lancuch (tu, po OutermostSiege); TB2-b: trebusz bez klasy
                float ob4 = SiegeWalls.PrepFactor(siegeEvent, side, type);
                if (ob4 > 0f && ob4 < 1f) __result *= ob4;
                // slad w logu: tylko oblezenie gracza, raz na typ machiny
                try
                {
                    if (siegeEvent == null || type == null || side == null || !siegeEvent.IsPlayerSiegeEvent) return;
                    SiegeDaily(siegeEvent, side);
                    string key = type.StringId + "|" + side.BattleSide;
                    if (_siegeLogged.Contains(key)) return;
                    _siegeLogged.Add(key);
                    int men = 0;
                    try { men = MobileParty.MainParty != null ? MobileParty.MainParty.MemberRoster.TotalHealthyCount : 0; } catch { }
                    float hoursNow = __result > 0f ? 1f / __result : 0f;
                    Log.Info("WorldPace: budowa " + type.StringId + " (" + type.ManDayCost + " osobodni, strona " + side.BattleSide + ", zdrowych " + men
                             + "): gra (z innymi modami) " + (before * 100f).ToString("0.00") + "%/h -> po suwaku " + p + "% i OB4 x" + ob4.ToString("0.000") + " " + (__result * 100f).ToString("0.00")
                             + "%/h = ~" + hoursNow.ToString("0") + " h (" + (hoursNow / 24f).ToString("0.0") + " dnia) od zera.");
                }
                catch { }
            }
            catch { }
        }

        /// <summary>564: FinalPostfix na kazdym zadeklarowanym CalculateFinalSpeed, Priority.Last - wolane w SubModuleMain PO NightRest.ApplyAll
        /// (ten sam priorytet co MarchPace i NightRest, pozniejsza rejestracja = biegnie po nich). Wpinane zawsze; klucz WorldPaceWholeSpeed czytany na zywo.</summary>
        internal static void ApplyFinal(Harmony harmony)
        {
            try
            {
                var post = new HarmonyMethod(typeof(WorldPace).GetMethod("FinalPostfix", BindingFlags.Public | BindingFlags.Static)) { priority = Priority.Last };
                int done = 0;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type[] types;
                    try { types = asm.GetTypes(); }
                    catch (ReflectionTypeLoadException rtle) { types = rtle.Types; }
                    catch { continue; }
                    foreach (var t in types)
                    {
                        if (t == null || t.IsAbstract || !typeof(PartySpeedModel).IsAssignableFrom(t)) continue;
                        try
                        {
                            var m = t.GetMethod("CalculateFinalSpeed", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                            if (m == null || m.IsAbstract) continue;
                            harmony.Patch(m, postfix: post);
                            done++;
                        }
                        catch (Exception e) { Log.Error("WorldPace.ApplyFinal(" + t.Name + ")", e); }
                    }
                }
                _finalModels = done;
                var c = Settings.Current;
                Log.Info("WorldPace 564: tempo swiata " + (c != null && c.WorldPaceWholeSpeed ? "mnozy CALA predkosc" : "w bazie (klucz WorldPaceWholeSpeed wylaczony)")
                         + " - latka predkosci koncowej w " + done + " modelach" + (done == 0 ? " (BRAK - zostaje stary tryb)" : "") + ".");
            }
            catch (Exception e) { Log.Error("WorldPace.ApplyFinal", e); }
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
