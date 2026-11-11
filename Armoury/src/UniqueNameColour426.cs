using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// 426b (Jeff 11.11: "bronie z valyrianskiej stali oznaczaj innym kolorem napis tej broni ... unikaty oznaczamy kolorem np zlotym, a reszta to biale,
    /// valyrianska stal kazda bron to tez unikat"). Jeden predykat - UniqueSpoils.Is (ten sam co zdobycz 426a i kronika unikatow); nazwy w danych bez zmian.
    ///  1. Wiersz listy ekwipunku / handlu / lupu gry / trofeow Spoils of War / zbrojowni DTE (jeden prefab SandBox InventoryItemTuple: TextWidget NameText
    ///     z Brush.FontColor #FFFFFFFF): postfiks Harmony na settery InventoryItemTupleWidget.ItemID i .NameTextWidget (typ odbiciem - DLL widzetow nie jest
    ///     w libs; wzor NameplateFont). ItemID to id przedmiotu sklejone z id modyfikatora (CampaignUIHelper.GetItemLockStringID). Kolor idzie na DefaultStyle
    ///     klonu pedzla napisu (BrushRenderer sledzi Style.Version - zmiana widoczna bez zmiany stanu). Biel przywracamy TYLKO widzetom, ktore sami
    ///     zlocilismy (ConditionalWeakTable) - wiersz, ktory nigdy nie pokazal unikatu, nie jest dotykany. ROT latka ten sam setter prefiksem (pedzel TLA
    ///     wiersza rzeczy kradzionych) - napisu nie rusza, brak kolizji.
    ///  2. Zlota linia "Unique" / "Unique - Valyrian steel" w panelu przedmiotu (ItemMenuVM.SetItem) i w podpowiedzi ogolnej (RefreshItemTooltip) -
    ///     dopisywana w istniejacych postfiksach SteelTooltip.cs.
    /// Klucz UniqueNamesGold czytany przy kazdym malowaniu (zmiana w MCM dziala od nastepnego odswiezenia listy). Potkniecia licznikiem.
    /// </summary>
    internal static class UniqueGold
    {
        internal static readonly Color Gold = Color.FromUint(0xFFFFD24Au);
        private static readonly Color White = Color.FromUint(0xFFFFFFFFu);
        private const string WidgetType = "TaleWorlds.MountAndBlade.GauntletUI.Widgets.Inventory.InventoryItemTupleWidget";

        private static bool _installed;
        private static PropertyInfo _pId, _pName;
        private static readonly ConditionalWeakTable<object, object> _gilded = new ConditionalWeakTable<object, object>();
        private static readonly object Mark = new object();
        private static readonly Dictionary<string, bool> _modded = new Dictionary<string, bool>();   // id z modyfikatorem -> unikat (pamiec sesji)
        private static int _gold, _white, _lines, _st;
        internal static string State = "nie instalowane";

        internal static bool On { get { var s = Settings.Current; return s != null && s.UniqueNamesGold; } }

        internal static void Reset() { _modded.Clear(); }

        internal static void Install(Harmony h)
        {
            if (_installed) return;
            _installed = true;
            var t = AccessTools.TypeByName(WidgetType);
            _pId = t != null ? AccessTools.DeclaredProperty(t, "ItemID") : null;
            _pName = t != null ? AccessTools.DeclaredProperty(t, "NameTextWidget") : null;
            MethodInfo sId = _pId != null ? _pId.GetSetMethod(true) : null;
            MethodInfo sName = _pName != null ? _pName.GetSetMethod(true) : null;
            if (sId == null || _pName == null || _pName.GetGetMethod(true) == null || _pId.GetGetMethod(true) == null)
            {
                State = "postfiks InventoryItemTupleWidget.ItemID BRAK (typ " + (t != null) + ", ItemID " + (sId != null) + ", NameTextWidget " + (_pName != null) + ") - nazwy biale jak w grze";
                Log.Info("Kolor unikatow (426): " + State + ", linia Unique w panelu i podpowiedzi " + (On ? "TAK" : "NIE") + ".");
                return;
            }
            var post = new HarmonyMethod(typeof(UniqueGold), nameof(Post));
            h.Patch(sId, postfix: post);
            bool nameToo = false;
            if (sName != null) { h.Patch(sName, postfix: new HarmonyMethod(typeof(UniqueGold), nameof(Post))); nameToo = true; }
            State = "postfiks InventoryItemTupleWidget.ItemID wpiety (typ TAK, NameTextWidget TAK, setter NameTextWidget " + (nameToo ? "wpiety" : "BRAK") + ")";
            Log.Info("Kolor unikatow (426): " + State + ", linia Unique w panelu i podpowiedzi " + (On ? "TAK" : "NIE") + " (klucz Unique Names Gold).");
        }

        private static void Post(object __instance)
        {
            try
            {
                if (__instance == null) return;
                var tw = _pName.GetValue(__instance, null) as TextWidget;
                if (tw == null) return;
                object seen;
                bool was = _gilded.TryGetValue(__instance, out seen);
                bool u = On && IsUniqueLock(_pId.GetValue(__instance, null) as string);
                if (u)
                {
                    tw.Brush.FontColor = Gold;
                    if (!was) _gilded.Add(__instance, Mark);
                    _gold++;
                    if (_gold == 1) Log.Info("Kolor unikatow (426): pierwszy wiersz na zloto (napis '" + tw.Text + "').");
                }
                else if (was)
                {
                    tw.Brush.FontColor = White;
                    _gilded.Remove(__instance);
                    _white++;
                }
            }
            catch (Exception e)
            {
                _st++;
                if (_st <= 3) Log.Error("UniqueGold.Post (potkniecie " + _st + ")", e);
            }
        }

        /// <summary>id wiersza (id przedmiotu + id modyfikatora) -> unikat. Bez kampanii - nie.</summary>
        private static bool IsUniqueLock(string id)
        {
            if (string.IsNullOrEmpty(id) || Campaign.Current == null) return false;
            var om = MBObjectManager.Instance;
            if (om == null) return false;
            var it = om.GetObject<ItemObject>(id);
            if (it != null) return UniqueSpoils.Is(it);          // bez modyfikatora: dwa odczyty slownika, bez pamieci (spis wlasnych kling Qohoru rosnie)
            bool v;
            if (_modded.TryGetValue(id, out v)) return v;
            v = false;
            foreach (var u in RotUniques.Ids.Concat(ValyrianBlades.AllIds))
            {
                if (u == null || id.Length <= u.Length || !id.StartsWith(u, StringComparison.Ordinal)) continue;
                if (om.GetObject<ItemModifier>(id.Substring(u.Length)) == null) continue;
                var iu = om.GetObject<ItemObject>(u);
                if (iu != null && UniqueSpoils.Is(iu)) { v = true; break; }
            }
            if (_modded.Count > 4000) _modded.Clear();
            _modded[id] = v;
            return v;
        }

        /// <summary>Tresc zlotej linii panelu / podpowiedzi albo null (klucz wylaczony, nie unikat, poza kampania).</summary>
        internal static string LineFor(ItemObject item)
        {
            try
            {
                if (item == null || !On || Campaign.Current == null || !UniqueSpoils.Is(item)) return null;
                _lines++;
                return ValyrianBlades.Is(item) ? "Unique - Valyrian steel" : "Unique";
            }
            catch { _st++; return null; }
        }

        private static int _told = -1;
        /// <summary>Linia doby - tylko gdy liczniki sesji sie zmienily od ostatniej linii.</summary>
        internal static string DayLine()
        {
            int sum = _gold + _white + _lines + _st;
            if (sum == _told || (sum == 0 && _told < 0)) return null;
            _told = sum;
            return "Kolor unikatow (426): " + State + " | od startu gry: wiersze na zloto " + _gold + ", z powrotem na bialo " + _white + ", linie Unique w panelu i podpowiedzi " + _lines
                   + ", potkniecia " + _st + " | klucz " + (On ? "TAK" : "NIE") + ".";
        }
    }
}
