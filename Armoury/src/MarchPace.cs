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
        /// 3 tabor, 4 (311d2) garstka piechurow "na drugiego" za jezdzcami (n). E6/K0: wolany dwa razy w tym samym wywolaniu (z wolnymi konmi zbrojowni i bez nich - sufit na sucho do linii).
        /// 311d (HorseQualitySpeed): mulRider / mulFoot - jakosc koni (HorsePace.CapMul) mnozy sufit sytuacji 1 i 2 (1 = bez zmian); wybor najnizszego po mnozeniu.
        /// 311d2 (v2 = reguly 311d2 czynne; false - sufit "jak przed 311d2" do linii doby i probki bez 311d): FootmenOnSpareHorsesRidePace - piechur na luzaku jedzie
        /// tempem jazdy x mulFoot x (1 - kara); RideDoubleFewFootmen - piesi bez zadnego konia <= stosunek x jezdzcow (ludzie z koniem pod siodlem, bohaterowie
        /// tez) i zaden jeniec nie idzie (luzaki najpierw pod jencow) -> tempo jazdy x mulRider x (1 - kara) zamiast tempa pieszych; gdy WSZYSCY piechurzy mieszcza
        /// sie na drugiego, a to szybsze niz luzaki (luzaki gorsze od koni jezdzcow), kolumna wybiera jazde na drugiego - wiecej koni nigdy nie obniza sufitu (prog K0).
        /// Obie reguly tylko podnosza sufit. Mokro (HorsePace.WetOf, pytane tylko gdy regula dziala): zysk ponad stary sufit -30% jak premie 311d, kary bez ciecia.
        /// rideFoot / walkers / dbl - podzial ludzi do Explain (to samo).</summary>
        private static float Cap(Settings c, MobileParty mp, bool v2, int men, int foot, int mounts, int train, int prisoners, float mulRider, float mulFoot,
                                 out int kind, out int n, out int dbl, out int rideFoot, out int walkers)
        {
            // luzaki najpierw pod piechote, reszta pod jencow
            int ridingFoot = Math.Min(foot, mounts);
            int mountsLeft = mounts - ridingFoot;
            walkers = (foot - ridingFoot) + Math.Max(0, prisoners - mountsLeft);
            dbl = 0;
            float wet = -1f;   // mokro leniwie - tylko gdy regula 311d2 zmienia sufit
            // 311d2: garstka piechurow siada za jezdzcami; jency zostaja na luzakach / sznurze (luzaki najpierw pod jencow - wtedy na drugiego jada tylko piechurzy)
            int riders = men - foot;   // ludzie z koniem pod siodlem (foot = bez konia, z jezdzcami bez konia z kroku 3); juczne nie wioza
            float room = MBMath.ClampFloat(c.RideDoubleRatio, 0f, 1f) * riders;
            bool canDbl = v2 && c.RideDoubleFewFootmen && riders > 0 && prisoners <= mounts;
            if (walkers > 0)
            {
                if (canDbl)
                {
                    int rf = Math.Min(foot, mounts - prisoners);
                    int fw = foot - rf;
                    if (fw > 0 && fw <= room) { dbl = fw; ridingFoot = rf; walkers = 0; }
                }
                if (dbl == 0) { kind = 0; n = walkers; rideFoot = ridingFoot; return c.MarchFootPace; }   // ktos idzie - wszyscy ida
            }
            float cap = Mounted(c, mp, v2, men, train, mulRider, mulFoot, ridingFoot, dbl, ref wet, out kind, out n);
            if (canDbl && ridingFoot > 0 && foot <= room)
            {
                // 311d2: wszyscy piechurzy na drugiego (luzaki pod jencow albo wolne), gdy to szybsze niz piechurzy na luzakach
                int k2, n2;
                float capB = Mounted(c, mp, v2, men, train, mulRider, mulFoot, 0, foot, ref wet, out k2, out n2);
                if (capB > cap + 0.0001f) { cap = capB; kind = k2; n = n2; dbl = foot; ridingFoot = 0; }
            }
            rideFoot = ridingFoot;
            return cap;
        }

        /// <summary>Sufit kolumny, w ktorej nikt nie idzie: ridingFoot piechurow na luzakach, dbl na drugiego (311d2). Najnizszy z pasujacych sufitow.</summary>
        private static float Mounted(Settings c, MobileParty mp, bool v2, int men, int train, float mulRider, float mulFoot, int ridingFoot, int dbl, ref float wet,
                                     out int kind, out int n)
        {
            // NIKT NIE IDZIE PIESZO - ale to jeszcze nie znaczy "kawaleria".
            // Do 13.09 kazda kolumna bez piechurow dostawala PELNY sufit jazdy,
            // wiec banda lotrow, ktora zrabowala dosc koni, maszerowala tak
            // szybko jak prawdziwi jezdzcy (Jeff: "widze bandytow, ktorzy maja
            // piechote, a biegaja jak konnica"). Klasowy komentarz obiecywal
            // polowe premii kawalerii, ale kod nigdy tego nie robil.
            // Teraz wygrywa sufit NAJNIZSZY z pasujacych, z wlasna nazwa przyczyny:
            // piechur w siodle jedzie wolniej niz czlowiek, ktory sie w nim urodzil.
            float cap = c.MarchRiderPace * mulRider;
            kind = 1; n = 0;
            if (dbl > 0)
            {
                // 311d2: konie niosa podwojnie - tempo jazdy mniej kara; zysk ponad tempo pieszych w mokro -30%
                float pen = MBMath.ClampFloat(c.RideDoublePenalty, 0f, 0.5f);
                float d2 = cap * (1f - pen), old = c.MarchFootPace;
                if (d2 > old) { if (wet < 0f) wet = HorsePace.WetOf(mp); d2 = old + (d2 - old) * wet; }
                cap = Math.Max(d2, old); kind = 4; n = dbl;   // regula tylko podnosi: nigdy wolniej niz pieszo
            }
            if (ridingFoot > 0)
            {
                float footRider = c.MarchFootRiderPace * mulFoot;
                if (v2 && c.FootmenOnSpareHorsesRidePace)
                {
                    // 311d2: piechur na luzaku jedzie tempem jazdy (jakosc luzakow), mniej kara za siodlo; zysk ponad stary sufit w mokro -30%
                    float pen = MBMath.ClampFloat(c.FootmenRidePacePenalty, 0f, 0.5f);
                    float fr = c.MarchRiderPace * mulFoot * (1f - pen);
                    if (fr > footRider) { if (wet < 0f) wet = HorsePace.WetOf(mp); footRider += (fr - footRider) * wet; }   // regula tylko podnosi (kara wieksza niz stary sufit - stary sufit)
                }
                if (footRider < cap) { cap = footRider; kind = 2; n = ridingFoot; }
            }
            int allowance = (int)Math.Ceiling(men * Math.Max(0f, c.MarchPackAllowance));
            if (train > allowance && c.MarchTrainPace < cap) { cap = c.MarchTrainPace; kind = 3; n = 0; }
            if (dbl > 0 && cap < c.MarchFootPace) cap = c.MarchFootPace;   // 311d2: na drugiego nigdy wolniej niz pieszo (tylko przy dziwnych ustawieniach sufitow)
            return cap;
        }

        /// <summary>noc 25.10 P4 (przeglad armii): kolumna partii "na sucho" do okna "Review the army" - skad wpis predkosci "Marching column: N afoot".</summary>
        internal sealed class Column
        {
            public int Men, Foot, Mounts, Train, Prisoners, Armory;   // jak CountColumn (z doczepionymi partiami armii); Armory - wolne konie zbrojowni (HorseState)
            public int Horses;                                       // luzaki liczone pod piechote i jencow: Mounts (+ Armory przy SpareHorsesCarryFootmen)
            public int RidingFoot, Walkers, Allowance;               // piechurzy na luzakach, idacy pieszo (piechota + jency), juczne wolne od kary taboru
            public int Kind, N;                                      // jak Cap: 0 ktos idzie (N idacych), 1 sami jezdzcy, 2 piechota na luzakach (N), 3 tabor
            public float CapRaw, Scale, Cap;                         // sufit przed tempem swiata, mnoznik WorldPacePercent, sufit na mapie
            public bool K0, Alone, Applies;                          // klucz SpareHorsesCarryFootmen; sam bohater; sufit czynny (ColumnPostfix pomija sufit <= 0.25)
            public int Walk;                                         // 311d krok 3: jezdzcy bez konia w Foot (ida pieszo)
            public float MulRider = 1f, MulFoot = 1f;                // 311d krok 2: mnozniki sufitu sytuacji 1 i 2 (jakosc koni)
            public int Double;                                       // 311d2: piechurzy "na drugiego" za jezdzcami (RideDoubleFewFootmen); Kind 4, gdy ten sufit wiaze
            public int Riders;                                       // 311d2: ludzie z koniem pod siodlem (Men - Foot) - podstawa stosunku jazdy na drugiego
            public float CapOld;                                     // 311d2: sufit na mapie bez regul 311d2 (linia doby "przed / po")
            public bool RidePace;                                    // 311d2: piechurzy na luzakach jada tempem jazdy (FootmenOnSpareHorsesRidePace i RidingFoot > 0)
        }

        /// <summary>
        /// noc 25.10 P4: wyjasnienie sufitu kolumny - TEN SAM CountColumn i Cap co ColumnPostfix (luzaki z kluczem SpareHorsesCarryFootmen, tempo swiata
        /// WorldPacePercent), tylko odczyt: nic nie zapisuje i nie zmienia ColumnPostfix. null = klucz kolumny wylaczony albo partia poza regula
        /// (morze, wies, karawana, zaloga, milicja, AI przy MarchPaceAiToo off) albo pusta.
        /// </summary>
        internal static Column Explain(MobileParty mp)
        {
            var c = Settings.Current;
            if (c == null || !c.MarchPaceEnabled || mp == null || mp.MemberRoster == null) return null;
            if (mp.IsCurrentlyAtSea || mp.IsVillager || mp.IsCaravan || mp.IsGarrison || mp.IsMilitia) return null;
            if (!c.MarchPaceAiToo && mp != MobileParty.MainParty) return null;
            var r = new Column();
            CountColumn(mp, out r.Men, out r.Foot, out r.Mounts, out r.Train, out r.Prisoners, out r.Armory);
            if (r.Men <= 0) return null;
            r.Walk = HorsePace.WalkOf(mp); r.Foot += r.Walk;            // 311d krok 3: ten sam rekord co ColumnPostfix
            HorsePace.CapMul(mp, out r.MulRider, out r.MulFoot);      // 311d krok 2
            r.K0 = c.SpareHorsesCarryFootmen;
            r.Horses = r.K0 ? r.Mounts + r.Armory : r.Mounts;
            bool v2 = !HorsePace.Dry;   // 311d2: jak ColumnPostfix (probka "bez 311d" w linii doby - bez regul 311d2)
            r.CapRaw = Cap(c, mp, v2, r.Men, r.Foot, r.Horses, r.Train, r.Prisoners, r.MulRider, r.MulFoot, out r.Kind, out r.N, out r.Double, out r.RidingFoot, out r.Walkers);
            int pace = Math.Max(5, Math.Min(100, c.WorldPacePercent));
            r.Scale = pace / 100f;
            r.Cap = r.CapRaw * r.Scale;
            r.Applies = r.Cap > 0.25f;
            // podzial ludzi (piechurzy na luzakach, idacy, na drugiego) - z Cap, ten sam co sufit (311d2: przy jezdzie na drugiego luzaki najpierw pod jencow)
            r.Riders = r.Men - r.Foot;
            r.RidePace = v2 && c.FootmenOnSpareHorsesRidePace && r.RidingFoot > 0;
            int k9, n9, d9, rf9, w9;
            r.CapOld = (v2 && (c.FootmenOnSpareHorsesRidePace || c.RideDoubleFewFootmen)
                        ? Cap(c, mp, false, r.Men, r.Foot, r.Horses, r.Train, r.Prisoners, r.MulRider, r.MulFoot, out k9, out n9, out d9, out rf9, out w9)
                        : r.CapRaw) * r.Scale;
            r.Allowance = (int)Math.Ceiling(r.Men * Math.Max(0f, c.MarchPackAllowance));
            r.Alone = mp == MobileParty.MainParty && r.Men == 1 && r.Prisoners == 0;
            return r;
        }

        /// <summary>
        /// Postfix (Priority.Last) na KAZDY CalculateFinalSpeed w grze.
        /// Pozycyjne __0, bo nadpisania w modach zmieniaja nazwy parametrow.
        /// SW-DROGI (noc 17/18.10): po suficie kolumny premia drogi ROT (MapRoads.SpeedRoad - wszystkie partie ladowe, takze karawany i wozy wsi,
        /// ktore kolumna pomija; ten sam poziom lancucha - SpeedDepth). Klucz MapRoadSpeedBonus wylaczony = SpeedRoad wychodzi od razu (jak przed zmiana).
        /// </summary>
        public static void SpeedPostfix(MobileParty __0, ref ExplainedNumber __result)
        {
            HorsePace.Begin(__0);   // 311d: rekord partii i mokro raz na wywolanie (Apply, WalkOf, CapMul)
            try
            {
                HorsePace.Apply(__0, ref __result);   // 311d: konie a tempo (wlasne klucze i wlasny SpeedDepth) - wspolczynniki PRZED sufitem kolumny
                ColumnPostfix(__0, ref __result);
            }
            finally { HorsePace.End(); }
            MapRoads.SpeedRoad(__0, ref __result);
            MapMud.Speed(__0, ref __result);   // p5-bloto (R3-07c): poza droga w deszczu -MapMudSpeedPercent% (wpis "Mud"); klucz wylaczony = wychodzi od razu
            SeaStorms.Speed(__0, ref __result);   // p6-sztormy (R3-05d): statek na morzu w burzy -SeaStormShipSpeedPercent% (wpis "Storm"); klucz wylaczony = wychodzi od razu
            CampCart.Speed(__0, ref __result);    // m97-oboz (recenzja): woz gracza w zaprzegu -CartSpeedPenaltyPercent% PO suficie kolumny (wpis "Supply cart"); bez wozu = wychodzi od razu
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
                foot += HorsePace.WalkOf(mp);   // 311d krok 3 (RidersWithoutHorseWalk): jezdziec bez konia idzie pieszo
                float mulR, mulF;
                HorsePace.CapMul(mp, out mulR, out mulF);   // 311d krok 2 (HorseQualitySpeed): 1/1 przy wylaczonym kluczu

                // E6/K0 (SpareHorsesCarryFootmen): kazdy wolny kon zbrojowni DTE partii (stan koni partii z pamieci doby) niesie piechura
                // jak luzak w taborze; luzaki w taborze (NumberOfMounts) licza juz takze konie ze stanem i kulawe (licznik gry u zrodla - HorseState)
                bool k0 = c.SpareHorsesCarryFootmen;
                int kind, n, dbl, rfx, wx;
                bool v2 = !HorsePace.Dry;   // 311d2: reguly 311d2 (piechur na luzaku tempem jazdy, na drugiego) - poza probka "bez 311d" linii doby
                float cap = Cap(c, mp, v2, men, foot, k0 ? mounts + armory : mounts, train, prisoners, mulR, mulF, out kind, out n, out dbl, out rfx, out wx);
                // sufity kolumny zyja w jednostkach mapy - przy zwolnionym
                // swiecie (WorldPacePercent) skaluja sie tym samym mnoznikiem
                int pace = Math.Max(5, Math.Min(100, c.WorldPacePercent));
                float scale = pace / 100f;
                if (k0 && mp != MobileParty.MainParty)
                {
                    // sufit tempa AI bez K0 (bez wolnych koni zbrojowni) na sucho w tym samym wywolaniu - prog linii: z K0 nigdy nizszy
                    int k2, n2, d2, r2, w2;
                    float cap0 = armory > 0 ? Cap(c, mp, v2, men, foot, mounts, train, prisoners, mulR, mulF, out k2, out n2, out d2, out r2, out w2) : cap;
                    HorseState.NoteCap(cap0 * scale, cap * scale);
                }
                if (mulR != 1f || mulF != 1f)
                {
                    // 311d: sufit bez jakosci koni na sucho w tym samym wywolaniu (linia "Konie a tempo (HQ)")
                    int k3, n3, d3, r3, w3;
                    HorsePace.NoteCap(Cap(c, mp, v2, men, foot, k0 ? mounts + armory : mounts, train, prisoners, 1f, 1f, out k3, out n3, out d3, out r3, out w3) * scale, cap * scale);
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
                    // m110-sam: sam bohater gracza (bez nikogo, bez doczepionych partii i jencow) - wpis mowi o nim; przedrostek "Marching column" zostaje (TerrainEase StartsWith)
                    bool alone = mp == MobileParty.MainParty && men == 1 && prisoners == 0;
                    TextObject why = alone
                                   ? (kind == 0 ? new TextObject("{=!}Marching column: you walk")
                                    : kind == 2 ? new TextObject("{=!}Marching column: you ride a spare horse")
                                    : kind == 3 ? new TextObject("{=!}Marching column: your pack animals")
                                    : new TextObject("{=!}Marching column: riding pace"))
                                   : kind == 0 ? new TextObject("{=!}Marching column: {N} afoot").SetTextVariable("N", n)
                                   : kind == 2 ? new TextObject("{=!}Marching column: {N} footmen on spare horses").SetTextVariable("N", n)
                                   : kind == 3 ? new TextObject("{=!}Marching column: baggage train")
                                   : kind == 4 ? new TextObject("{=!}Marching column: riding double - {N} footmen behind riders (-{P}%)").SetTextVariable("N", n)
                                                 .SetTextVariable("P", (MBMath.ClampFloat(c.RideDoublePenalty, 0f, 0.5f) * 100f).ToString("0", System.Globalization.CultureInfo.InvariantCulture))
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
                if (c == null || (!c.MarchPaceEnabled && !c.MapRoadSpeedBonus && !c.SeaStormSlowsShips && !c.CampCartEnabled && !HorsePace.AnyOn(c))) { Log.Info("MarchPace: wylaczony."); return; }   // 311d: latka niesie tez konie a tempo (HorsePace)   // m97-oboz: latka niesie tez kare wozu   // p6-sztormy: latka niesie tez kare burzy na morzu

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
                                 : c.SeaStormSlowsShips ? "kary burzy na morzu (SeaStormSlowsShips)" : c.MapRoadSpeedBonus ? "premii drog (MapRoadSpeedBonus)" : "kary wozu gracza (CampCartEnabled)") + ".");
            }
            catch (Exception e) { Log.Error("MarchPace.ApplyAll", e); }
        }
    }
}
