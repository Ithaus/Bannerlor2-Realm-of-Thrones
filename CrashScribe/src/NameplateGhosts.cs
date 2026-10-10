using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace CrashScribe
{
    /// <summary>
    /// IKONY-DUCHY NAD OSADAMI (Jeff 10.10: zrzut z biegu 2-letniego - nad Orlim Gniazdem ok. 200 ikon partii, a w osadzie bylo 5-11 partii).
    /// Gra (SandBox SettlementNameplatePartyMarkersVM) buduje liste ikon raz (PopulatePartyList), potem tylko dopisuje przy SettlementEntered
    /// i skresla przy OnSettlementLeft / MapEventEnded - nie slucha zniszczenia partii, wiec partia rozwiazana albo zniszczona W osadzie zostaje
    /// ikona na zawsze (do przeladowania ekranu mapy). Latka: (1) MobilePartyDestroyed - ikona tej partii znika od razu; (2) raz na dobe lista
    /// budowana od nowa ze stanu faktycznego (PopulatePartyList gry). Tylko wyglad - zadnego wplywu na rozgrywke.
    /// </summary>
    internal static class NameplateGhosts
    {
        private static MethodInfo _populate;
        private static PropertyInfo _list;
        internal static int Removed, Refreshed;

        internal static void Install(Harmony harmony)
        {
            try
            {
                var t = AccessTools.TypeByName("SandBox.ViewModelCollection.Nameplate.SettlementNameplatePartyMarkersVM");
                if (t == null) { Scribe.Line("Mends: ikony partii nad osadami - typ gry nieznaleziony, bez latki."); return; }
                _populate = AccessTools.Method(t, "PopulatePartyList");
                _list = AccessTools.Property(t, "PartiesInSettlement");
                var reg = AccessTools.Method(t, "RegisterEvents");
                var unl = AccessTools.Method(t, "UnloadEvents");
                if (reg != null) harmony.Patch(reg, postfix: new HarmonyMethod(typeof(NameplateGhosts), nameof(RegisterPostfix)));
                if (unl != null) harmony.Patch(unl, postfix: new HarmonyMethod(typeof(NameplateGhosts), nameof(UnloadPostfix)));
                Scribe.Line("Mends: ikony partii nad osadami - " + (reg != null && unl != null && _populate != null && _list != null ? "latka wpieta" : "latka NIEPELNA")
                            + " (zniszczona partia znika z listy od razu, lista odswiezana raz na dobe - koniec ikon-duchow).");
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "NameplateGhosts.Install", null); } catch { } }
        }

        public static void RegisterPostfix(object __instance)
        {
            try
            {
                var vm = __instance;
                CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(vm, (Action<MobileParty, PartyBase>)((mp, pb) => Remove(vm, mp)));
                CampaignEvents.DailyTickEvent.AddNonSerializedListener(vm, (Action)(() => Refresh(vm)));
            }
            catch { }
        }

        public static void UnloadPostfix(object __instance)
        {
            try
            {
                ((IMbEventBase)CampaignEvents.MobilePartyDestroyed).ClearListeners(__instance);
                ((IMbEventBase)CampaignEvents.DailyTickEvent).ClearListeners(__instance);
            }
            catch { }
        }

        private static void Remove(object vm, MobileParty mp)
        {
            try
            {
                var list = _list != null ? _list.GetValue(vm, null) as IList : null;
                if (list == null || mp == null) return;
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    var it = list[i];
                    if (it == null) continue;
                    var pp = AccessTools.Property(it.GetType(), "Party");
                    if (pp != null && ReferenceEquals(pp.GetValue(it, null), mp)) { list.RemoveAt(i); Removed++; }
                }
            }
            catch { }
        }

        private static void Refresh(object vm)
        {
            try { if (_populate != null) { _populate.Invoke(vm, null); Refreshed++; } }
            catch { }
        }
    }
}
