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
    /// i jencow. Piechota na luzakach jedzie z vanilla POLOWA premii
    /// kawalerii - "nie jezdza tak dobrze jak jezdzcy" (Jeff).
    /// Czapka tylko OBNIZA - nigdy nie przyspiesza ponizej naturalnej predkosci.
    /// Statki plyna po swojemu (morze wylaczone), wiesniacy i karawany
    /// chodza swoim rytmem.
    /// </summary>
    internal static class MarchPace
    {
        /// <summary>Zlicza kolumne: ludzi, piechote, luzaki, juczne+bydlo, jencow (z doczepionymi partiami armii).</summary>
        private static void CountColumn(MobileParty mp, out int men, out int foot, out int mounts, out int train, out int prisoners)
        {
            men = mp.MemberRoster != null ? mp.MemberRoster.TotalManCount : 0;
            foot = mp.Party != null ? mp.Party.NumberOfMenWithoutHorse : 0;
            var r = mp.ItemRoster;
            mounts = r != null ? r.NumberOfMounts : 0;
            train = r != null ? r.NumberOfPackAnimals + r.NumberOfLivestockAnimals : 0;
            prisoners = mp.PrisonRoster != null ? mp.PrisonRoster.TotalManCount : 0;
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
            }
        }

        /// <summary>
        /// Postfix (Priority.Last) na KAZDY CalculateFinalSpeed w grze.
        /// Pozycyjne __0, bo nadpisania w modach zmieniaja nazwy parametrow.
        /// </summary>
        public static void SpeedPostfix(MobileParty __0, ref ExplainedNumber __result)
        {
            try
            {
                var c = Settings.Current;
                if (c == null || !c.MarchPaceEnabled) return;
                var mp = __0;
                if (mp == null || mp.MemberRoster == null) return;
                if (mp.IsCurrentlyAtSea) return;                        // okrety plyna prawami morza
                if (mp.IsVillager || mp.IsCaravan || mp.IsGarrison || mp.IsMilitia) return;
                if (!c.MarchPaceAiToo && mp != MobileParty.MainParty) return;

                int men, foot, mounts, train, prisoners;
                CountColumn(mp, out men, out foot, out mounts, out train, out prisoners);
                if (men <= 0) return;

                // luzaki najpierw pod piechote, reszta pod jencow
                int ridingFoot = Math.Min(foot, mounts);
                int mountsLeft = mounts - ridingFoot;
                int walkers = (foot - ridingFoot) + Math.Max(0, prisoners - mountsLeft);

                float cap;
                TextObject why;
                if (walkers > 0)
                {
                    cap = c.MarchFootPace;                              // ktos idzie - wszyscy ida
                    why = new TextObject("{=!}Marching column: {N} afoot").SetTextVariable("N", walkers);
                }
                else
                {
                    // piechota na luzakach zostaje przy vanilla polowie premii
                    // kawalerii (+15% vs +30%) - piechur nie jezdzi jak jezdziec
                    int allowance = (int)Math.Ceiling(men * Math.Max(0f, c.MarchPackAllowance));
                    if (train > allowance)
                    {
                        cap = c.MarchTrainPace;
                        why = new TextObject("{=!}Marching column: baggage train");
                    }
                    else
                    {
                        cap = c.MarchRiderPace;
                        why = new TextObject("{=!}Marching column: all riders");
                    }
                }
                if (cap <= 0.5f) return;
                // czapka WIDOCZNA w rozpisce predkosci: gole LimitMax cielo tempo
                // bez zadnego sladu i gracz nie wiedzial, CZEMU wlecze sie 4.0
                // (Jeff 27.08: "cos jest nie tak z mechanika predkosci").
                // Ujemny wpis z nazwana przyczyna + LimitMax jako pas bezpieczenstwa.
                float current = __result.ResultNumber;
                if (current > cap) __result.Add(cap - current, why);
                __result.LimitMax(cap);
            }
            catch { }
        }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var c = Settings.Current;
                if (c == null || !c.MarchPaceEnabled) { Log.Info("MarchPace: wylaczony."); return; }

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
                Log.Info("MarchPace: kolumna marszowa wpieta w " + done + " modeli predkosci.");
            }
            catch (Exception e) { Log.Error("MarchPace.ApplyAll", e); }
        }
    }
}
