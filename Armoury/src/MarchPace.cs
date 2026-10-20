using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// KOLUMNA MARSZOWA, wedle slow Jeffa: "szybkosc party to szybkosc
    /// najwolniejszej jednostki, chyba ze piechota jedzie konno i nie ciagnie
    /// taboru". Zaden model (vanilla, BK, RBM, RealisticBannerlord...) nie
    /// wyzej niz pozwala fizyka:
    ///  - ktokolwiek idzie PIESZO (piechur bez luzaka, jeniec na sznurze)
    ///    -> cala kolumna czlapie tempem piechura (MarchFootPace),
    ///  - wszyscy konno, ale z taborem (juczne, bydlo ponad przydzial)
    ///    -> tempo taboru (MarchTrainPace),
    ///  - czysta kolumna jezdzcow -> tempo jazdy (MarchRiderPace).
    /// Luzaki (konie wierzchowe w sakwach) NIE sa taborem - niosa piechote
    /// i jencow. Piechota na luzakach ma WLASNY, posredni sufit
    /// (MarchFootRiderPace) - "chyba ze piechota jest na koniach" (Jeff 13.09):
    /// jedzie szybciej niz idzie, ale nigdy tak szybko jak prawdziwa jazda.
    /// Czapka tylko OBNIZA - nigdy nie przyspiesza ponizej naturalnej predkosci.
    /// Statki plyna po swojemu (morze wylaczone), wiesniacy i karawany
    /// chodza swoim rytmem.
    /// </summary>
    internal static class MarchPace
    {
        /// <summary>Zlicza kolumne: ludzi, piechote, luzaki, juczne+bydlo, jencow (z doczepionymi partiami armii); armory - wolne konie zbrojowni DTE
        /// ze stanu koni partii (E6/K0, pamiec doby - tylko odczyt, bez Stables.Balance w tej sciezce).</summary>
        private static void CountColumn(MobileParty mp, out int men, out int foot, out int mounts, out int train, out int prisoners, out int armory)
        {
            men = mp.MemberRoster != null ? mp.MemberRoster.TotalManCount : 0;
            foot = mp.Party != null ? mp.Party.NumberOfMenWithoutHorse : 0;
            var r = mp.ItemRoster;
            mounts = r != null ? r.NumberOfMounts : 0;
            train = r != null ? r.NumberOfPackAnimals + r.NumberOfLivestockAnimals : 0;
            prisoners = mp.PrisonRoster != null ? mp.PrisonRoster.TotalManCount : 0;
            armory = HorseState.FreeOf(mp);
            var attached = mp.AttachedParties;
            if (attached == null) return;
            for (int i = 0; i < attached.Count; i++)
            {
                var ap = attached[i];
                if (ap == null) continue;
                men += ap.MemberRoster != null ? ap.MemberRoster.TotalManCount : 0;
                foot += ap.Party != null ? ap.Party.NumberOfMenWithoutHorse : 0;
                var ar = ap.ItemRoster;
                if (ar != null) { mounts += ar.NumberOfMounts; train += ar.NumberOfPackAnimals + ar.NumberOfLivestockAnimals; }
                prisoners += ap.PrisonRoster != null ? ap.PrisonRoster.TotalManCount : 0;
                armory += HorseState.FreeOf(ap);
            }
        }

        /// <summary>Sufit kolumny w jednostkach mapy PRZED tempem swiata. kind: 0 ktos idzie (n - idacych), 1 sami jezdzcy, 2 piechota na luzakach (n),
        /// 3 tabor. E6/K0: wolany dwa razy w tym samym wywolaniu (z wolnymi konmi zbrojowni i bez nich - sufit na sucho do linii).</summary>
        private static float Cap(Settings c, int men, int foot, int mounts, int train, int prisoners, out int kind, out int n)
        {
            // luzaki najpierw pod piechote, reszta pod jencow
            int ridingFoot = Math.Min(foot, mounts);
            int mountsLeft = mounts - ridingFoot;
            int walkers = (foot - ridingFoot) + Math.Max(0, prisoners - mountsLeft);
            if (walkers > 0) { kind = 0; n = walkers; return c.MarchFootPace; }   // ktos idzie - wszyscy ida
            // NIKT NIE IDZIE PIESZO - ale to jeszcze nie znaczy "kawaleria".
            // Do 13.09 kazda kolumna bez piechurow dostawala PELNY sufit jazdy,
            // wiec banda lotrow, ktora zrabowala dosc koni, maszerowala tak
            // szybko jak prawdziwi jezdzcy (Jeff: "widze bandytow, ktorzy maja
            // piechote, a biegaja jak konnica"). Klasowy komentarz obiecywal
            // polowe premii kawalerii, ale kod nigdy tego nie robil.
            // Teraz wygrywa sufit NAJNIZSZY z pasujacych, z wlasna nazwa przyczyny:
            // piechur w siodle jedzie wolniej niz czlowiek, ktory sie w nim urodzil.
            float cap = c.MarchRiderPace;
            kind = 1; n = 0;
            if (ridingFoot > 0 && c.MarchFootRiderPace < cap) { cap = c.MarchFootRiderPace; kind = 2; n = ridingFoot; }
            int allowance = (int)Math.Ceiling(men * Math.Max(0f, c.MarchPackAllowance));
            if (train > allowance && c.MarchTrainPace < cap) { cap = c.MarchTrainPace; kind = 3; n = 0; }
            return cap;
        }

        /// <summary>
        /// Postfix (Priority.Last) na KAZDY CalculateFinalSpeed w grze.
        /// Pozycyjne __0, bo nadpisania w modach zmieniaja nazwy parametrow.
        /// SW-DROGI (noc 17/18.10): po suficie kolumny premia drogi ROT (MapRoads.SpeedRoad - wszystkie partie ladowe, takze karawany i wozy wsi,
        /// ktore kolumna pomija; ten sam poziom lancucha - SpeedDepth). Klucz MapRoadSpeedBonus wylaczony = SpeedRoad wychodzi od razu (jak przed zmiana).
        /// </summary>
        public static void SpeedPostfix(MobileParty __0, ref ExplainedNumber __result)
        {
            ColumnPostfix(__0, ref __result);
            MapRoads.SpeedRoad(__0, ref __result);
            MapMud.Speed(__0, ref __result);   // p5-bloto (R3-07c): poza droga w deszczu -MapMudSpeedPercent% (wpis "Mud"); klucz wylaczony = wychodzi od razu
            SeaStorms.Speed(__0, ref __result);   // p6-sztormy (R3-05d): statek na morzu w burzy -SeaStormShipSpeedPercent% (wpis "Storm"); klucz wylaczony = wychodzi od razu
        }

        /// <summary>Kolumna marszowa (tresc dawnego SpeedPostfix bez zmian).</summary>
        private static void ColumnPostfix(MobileParty __0, ref ExplainedNumber __result)
        {
            long tk = 0;
            try
            {
                var c = Settings.Current;
                if (c == null || !c.MarchPaceEnabled) return;
                var mp = __0;
                if (mp == null || mp.MemberRoster == null) return;
                if (mp.IsCurrentlyAtSea) return;                        // okrety plyna prawami morza
                if (mp.IsVillager || mp.IsCaravan || mp.IsGarrison || mp.IsMilitia) return;
                if (!c.MarchPaceAiToo && mp != MobileParty.MainParty) return;
                // LANCUCH MODELI - liczymy RAZ, na najbardziej zewnetrznym poziomie
                // (13.09: TerrainEase mial ten bezpiecznik od poczatku, my nie, wiec
                // czapka zakladala sie na kazdym poziomie lancucha)
                if (!SpeedDepth.OutermostFinal) return;
                tk = HorseState.SpeedBegin();   // E6/K0: koszt SpeedPostfix (znacznik czasu co 16. wywolanie)

                int men, foot, mounts, train, prisoners, armory;
                CountColumn(mp, out men, out foot, out mounts, out train, out prisoners, out armory);
                if (men <= 0) return;

                // E6/K0 (SpareHorsesCarryFootmen): kazdy wolny kon zbrojowni DTE partii (stan koni partii z pamieci doby) niesie piechura
                // jak luzak w taborze; luzaki w taborze (NumberOfMounts) licza juz takze konie ze stanem i kulawe (licznik gry u zrodla - HorseState)
                bool k0 = c.SpareHorsesCarryFootmen;
                int kind, n;
                float cap = Cap(c, men, foot, k0 ? mounts + armory : mounts, train, prisoners, out kind, out n);
                // sufity kolumny zyja w jednostkach mapy - przy zwolnionym
                // swiecie (WorldPacePercent) skaluja sie tym samym mnoznikiem
                int pace = Math.Max(5, Math.Min(100, c.WorldPacePercent));
                float scale = pace / 100f;
                if (k0 && mp != MobileParty.MainParty)
                {
                    // sufit tempa AI bez K0 (bez wolnych koni zbrojowni) na sucho w tym samym wywolaniu - prog linii: z K0 nigdy nizszy
                    int k2, n2;
                    float cap0 = armory > 0 ? Cap(c, men, foot, mounts, train, prisoners, out k2, out n2) : cap;
                    HorseState.NoteCap(cap0 * scale, cap * scale);
                }
                cap *= scale;
                if (cap <= 0.25f) return;
                // czapka WIDOCZNA w rozpisce predkosci: gole LimitMax cielo tempo
                // bez zadnego sladu i gracz nie wiedzial, CZEMU wlecze sie 4.0
                // (Jeff 27.08: "cos jest nie tak z mechanika predkosci").
                // Ujemny wpis z nazwana przyczyna + LimitMax jako pas bezpieczenstwa.
                float current = __result.ResultNumber;
                // Add() doklada do BAZY, ktora jest potem mnozona przez (1 + suma
                // wspolczynnikow) - wiec zeby zdjac X jednostek MAPY, trzeba wpisac
                // X podzielone przez ten mnoznik. Bez tego wpis w rozpisce klamal
                // (pokazywal wiecej, niz faktycznie schodzilo), a LimitMax i tak
                // docinal reszte po cichu. 13.09.
                if (current > cap)
                {
                    TextObject why = kind == 0 ? new TextObject("{=!}Marching column: {N} afoot").SetTextVariable("N", n)
                                   : kind == 2 ? new TextObject("{=!}Marching column: {N} footmen on spare horses").SetTextVariable("N", n)
                                   : kind == 3 ? new TextObject("{=!}Marching column: baggage train")
                                   : new TextObject("{=!}Marching column: all riders");
                    float f = 1f + __result.SumOfFactors;
                    __result.Add(f > 0.01f ? (cap - current) / f : (cap - current), why);
                }
                __result.LimitMax(cap);
            }
            catch { }
            finally { HorseState.SpeedEnd(tk); }
        }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var c = Settings.Current;
                // SW-DROGI: ta sama latka niesie premie drogi (MapRoads.SpeedRoad) - wpinana takze przy wylaczonej kolumnie, gdy MapRoadSpeedBonus wlaczony
                // (kolumna sprawdza wtedy swoj klucz w ColumnPostfix i wychodzi); oba klucze wylaczone = jak przed zmiana
                if (c == null || (!c.MarchPaceEnabled && !c.MapRoadSpeedBonus && !c.SeaStormSlowsShips)) { Log.Info("MarchPace: wylaczony."); return; }   // p6-sztormy: latka niesie tez kare burzy na morzu

                var post = new HarmonyMethod(typeof(MarchPace).GetMethod("SpeedPostfix",
                    BindingFlags.Public | BindingFlags.Static)) { priority = Priority.Last };
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
                            var m = t.GetMethod("CalculateFinalSpeed", BindingFlags.Public | BindingFlags.NonPublic |
                                                                       BindingFlags.Instance | BindingFlags.DeclaredOnly);
                            if (m == null || m.IsAbstract) continue;
                            h.Patch(m, postfix: post);
                            done++;
                        }
                        catch { }
                    }
                }
                MapRoads.SpeedModels = done;   // SW-DROGI (recenzja, uwaga 4): linie "Drogi (DR)" mowia NIECZYNNA, gdy ta latka nie weszla przy starcie gry
                if (c.MarchPaceEnabled) Log.Info("MarchPace: kolumna marszowa wpieta w " + done + " modeli predkosci.");
                else Log.Info("MarchPace: kolumna wylaczona - latka wpieta w " + done + " modeli predkosci tylko dla "   // p6-sztormy (recenzja): ktore klucze niosa latke
                              + (c.MapRoadSpeedBonus && c.SeaStormSlowsShips ? "premii drog (MapRoadSpeedBonus) i kary burzy na morzu (SeaStormSlowsShips)"
                                 : c.SeaStormSlowsShips ? "kary burzy na morzu (SeaStormSlowsShips)" : "premii drog (MapRoadSpeedBonus)") + ".");
            }
            catch (Exception e) { Log.Error("MarchPace.ApplyAll", e); }
        }
    }
}
