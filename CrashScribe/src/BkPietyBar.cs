using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace CrashScribe
{
    /// <summary>
    /// POBOZNOSC DWA RAZY NA PASKU MAPY (Jeff 15.10, zrzut: dwie ikony "Piety 50"). Przyczyna [K BK]: BannerKings.Patches.MapInfoPietyPatches
    /// .RefreshValuesPatch (postfiks MapInfoVM.RefreshValues) przy KAZDYM odswiezeniu tworzy nowy MapInfoItemVM("piety") i dodaje go, gdy lista
    /// nie zawiera TEGO obiektu (porownanie referencji) - starej pozycji nie zdejmuje; UpdatePlayerInfo odswieza tylko ostatnia (_pietyItem).
    /// Naprawa (klucz BkPietyBarOnce): postfiks MapInfoVM.RefreshValues z priorytetem Last (po BK) - z pozycji "piety" zostaje ta z pola BK
    /// _pietyItem (bez pola - ostatnia), reszta zdjeta. Odbicia (CrashScribe nie odwoluje sie do ViewModelCollection).
    /// </summary>
    internal static class BkPietyBar
    {
        private const string HarmonyId = "com.jeff.crashscribe";
        private static bool _installed;
        private static string _state = "nie wpiete";
        private static FieldInfo _bkItem, _itemId;
        private static PropertyInfo _items;
        private static int _removed, _stumbles;

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_stumbles <= 3 || _stumbles % 100 == 0) { try { Scribe.Report("CrashScribe", e, "BkPietyBar." + where + " (potkniecie " + _stumbles + ")", null); } catch { } }
        }

        internal static void OnSession()
        {
            try { Install(); } catch (Exception e) { _state = "wyjatek " + e.GetType().Name; Stumble("Install", e); }
            try { Scribe.Line("Poboznosc na pasku mapy (BK): " + _state + " | klucz BkPietyBarOnce " + (Config.BkPietyBarOnce ? "TAK" : "NIE") + " | zdjetych podwojnych pozycji od startu procesu " + _removed + "."); }
            catch (Exception e) { Stumble("StartLine", e); }
        }

        private static void Install()
        {
            if (_installed) return;
            _installed = true;
            if (!Config.BkPietyBarOnce) { _state = "WYLACZONE (klucz)"; return; }
            var tVm = AccessTools.TypeByName("TaleWorlds.CampaignSystem.ViewModelCollection.Map.MapBar.MapInfoVM");
            var tItem = AccessTools.TypeByName("TaleWorlds.CampaignSystem.ViewModelCollection.Map.MapBar.MapInfoItemVM");
            var tBk = AccessTools.TypeByName("BannerKings.Patches.MapInfoPietyPatches");
            var m = tVm != null ? AccessTools.Method(tVm, "RefreshValues") : null;
            _items = tVm != null ? AccessTools.Property(tVm, "PrimaryInfoItems") : null;
            _itemId = tItem != null ? AccessTools.Field(tItem, "ItemId") : null;
            _bkItem = tBk != null ? AccessTools.Field(tBk, "_pietyItem") : null;
            if (tBk == null) { _state = "bez BK MapInfoPietyPatches - nic do robienia"; return; }
            if (m == null || _items == null || _itemId == null) { _state = "BRAK (MapInfoVM.RefreshValues / PrimaryInfoItems / ItemId)"; return; }
            new Harmony(HarmonyId).Patch(m, postfix: new HarmonyMethod(typeof(BkPietyBar), nameof(RefreshPost)) { priority = Priority.Last });
            _state = "wpiete (MapInfoVM.RefreshValues, po BK)";
        }

        public static void RefreshPost(object __instance)
        {
            try
            {
                if (!Config.BkPietyBarOnce || __instance == null) return;
                var list = _items.GetValue(__instance, null) as IList;
                if (list == null || list.Count < 2) return;
                object keep = _bkItem != null ? _bkItem.GetValue(null) : null;
                bool keepIn = keep != null && list.Contains(keep);
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    var it = list[i];
                    if (it == null || !string.Equals(_itemId.GetValue(it) as string, "piety", StringComparison.Ordinal)) continue;
                    if (!keepIn) { keep = it; keepIn = true; continue; }   // bez pozycji BK na liscie - zostaje ostatnia
                    if (!ReferenceEquals(it, keep)) { list.RemoveAt(i); _removed++; }
                }
            }
            catch (Exception e) { Stumble("RefreshPost", e); }
        }
    }

    /// <summary>15.10: jedna poboznosc na pasku mapy - wpiecie przy starcie sesji. Bez zapisu.</summary>
    internal sealed class BkPietyBarBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, delegate (CampaignGameStarter s) { BkPietyBar.OnSession(); });
        }

        public override void SyncData(IDataStore dataStore) { }
    }
}
