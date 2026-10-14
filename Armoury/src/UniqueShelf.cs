using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// A17 (poz. 4, F1-3) - UNIKAT NIE JEST TOWAREM MIESZCZAN. Klucz UniquesNotFolkGoods (wylaczony = 79a8d89).
    ///
    /// Dowod: F1-3 w 3 z 5 ostatnich biegow 120 dob - cersei_red_dress znika z targu (W53 d85 Dragonstone, fin27 d80 i fin28 d88 King's Landing); suknia nie ma
    /// item_category w ROTassets.xml (:5611), tier RBM 0.48 < Tier2 -> kategoria "garment", a konsumpcja mieszczan BK (EconomyPatches.ItemConsumptionPatch -
    /// prefiks ItemConsumptionBehavior.MakeConsumption) bierze kazda sztuke kategorii, na ktora ma budzet; ochrona 174.0 (ArmsLeaks) zeruje budzet tylko
    /// uzbrojeniu. W dniu znikniecia fin27: "mieszczanie: ... odziez (garment) 339 szt." (rozpoznanie TRIAZ-2 e-F1-3, ROZPOZNANIE-A17 rozdz. 2.4).
    ///
    /// REGULA: na czas dziennej konsumpcji osady (ItemConsumptionBehavior.MakeConsumptionInTown - kasowanie gry DeleteOverproducedItems, popyt i podaz,
    /// konsumpcja gry i BK, zywnosc; BK wola ja tez dla zamkow przez refleksje) unikaty (UniqueSpoils.Is - ten sam predykat co kronika unikatow i F1-3) sa
    /// zdjete z polki (prefiks First) i odlozone po niej (finalizer Last - takze po wyjatku). Unikat schodzi z polki tylko kupnem (bohater, gracz) albo wozem
    /// kupca. Kategoria przedmiotu bez zmian (ceny i inne mody jej uzywaja). Zdjecie i odlozenie poza ramkami ksiegi towarow (prefiks przed jej ramka,
    /// finalizer po niej) - netto 0 w tym samym ticku osady; flaga Busy - diagnoza D3 (A17Diag) nie liczy tego jako ubytku z polki. Zapisu nie ma.
    /// </summary>
    internal static class UniqueShelf
    {
        internal static bool On { get { var s = Settings.Current; return s != null && s.UniquesNotFolkGoods; } }

        /// <summary>Trwa zdjecie albo odlozenie unikatow (A17Diag.D3 pomija te zmiany rosteru).</summary>
        internal static bool Busy;

        private static bool _wired;
        private static int _hidden, _restored, _towns, _stumbles, _stumblesAll;
        private static bool _err;
        private static readonly Dictionary<string, int> _ids = new Dictionary<string, int>();

        internal static void Reset() { _hidden = _restored = _towns = _stumbles = 0; _stumblesAll = 0; _err = false; _ids.Clear(); Busy = false; }

        internal static void ApplyAll(Harmony h)
        {
            if (_wired || h == null) return;
            var m = AccessTools.Method(typeof(ItemConsumptionBehavior), "MakeConsumptionInTown");
            if (m == null) { Log.Info("UniqueShelf (A17): BRAK ItemConsumptionBehavior.MakeConsumptionInTown - unikaty dalej konsumowane jak dotad."); return; }
            h.Patch(m, prefix: new HarmonyMethod(typeof(UniqueShelf), nameof(Pre)) { priority = Priority.First },
                       finalizer: new HarmonyMethod(typeof(UniqueShelf), nameof(Fin)) { priority = Priority.Last });
            _wired = true;
            Log.Info("UniqueShelf (A17): unikaty zdejmowane z polki na czas konsumpcji osady - latka wpieta (klucz Uniques Not Folk Goods czytany na zywo).");
        }

        public static void Pre(Town town, out List<ItemRosterElement> __state)
        {
            __state = null;
            if (!On || town == null) return;
            try
            {
                var r = town.Owner != null ? town.Owner.ItemRoster : null;
                if (r == null) return;
                List<ItemRosterElement> keep = null;
                for (int i = r.Count - 1; i >= 0; i--)
                {
                    var el = r.GetElementCopyAtIndex(i);
                    if (el.Amount <= 0) continue;
                    var it = el.EquipmentElement.Item;
                    if (it == null || !UniqueSpoils.Is(it)) continue;
                    if (keep == null) keep = new List<ItemRosterElement>();
                    keep.Add(el);
                }
                if (keep == null) return;
                Busy = true;
                try
                {
                    foreach (var el in keep)
                    {
                        r.AddToCounts(el.EquipmentElement, -el.Amount);
                        _hidden += el.Amount;
                        int v; _ids.TryGetValue(el.EquipmentElement.Item.StringId, out v); _ids[el.EquipmentElement.Item.StringId] = v + el.Amount;
                    }
                }
                finally { Busy = false; }
                __state = keep;
                _towns++;
            }
            catch (Exception e) { Stumble("Pre", e); }
        }

        public static Exception Fin(Exception __exception, Town town, List<ItemRosterElement> __state)
        {
            if (__state == null || town == null) return __exception;
            try
            {
                var r = town.Owner.ItemRoster;
                Busy = true;
                try { foreach (var el in __state) { r.AddToCounts(el.EquipmentElement, el.Amount); _restored += el.Amount; } }
                finally { Busy = false; }
            }
            catch (Exception e) { Stumble("Fin", e); }
            return __exception;
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumblesAll++;
            if (!_err) { _err = true; Log.Error("UniqueShelf." + where, e); }
        }

        /// <summary>Linia doby "Unikaty na polkach (A17)" (po kronice unikatow). Kontrola: odlozone = zdjete.</summary>
        internal static void Daily()
        {
            try
            {
                var sb = new StringBuilder(400);
                sb.Append("Unikaty na polkach (A17): dzien ").Append((int)CampaignTime.Now.ToDays).Append(" | ").Append(!_wired ? "BRAK LATKI" : On ? "CZYNNE" : "WYLACZONE (Uniques Not Folk Goods)")
                  .Append(" | zdjete na czas konsumpcji osad ").Append(_hidden).Append(" szt. w ").Append(_towns).Append(" wywolaniach, odlozone ").Append(_restored)
                  .Append(" (kontrola: zdjete - odlozone = ").Append(_hidden - _restored).Append(")");
                if (_ids.Count > 0)
                {
                    sb.Append(" | sztuki: ");
                    int k = 0;
                    foreach (var kv in _ids) { if (k++ >= 12) { sb.Append(", ..."); break; } sb.Append(k > 1 ? ", " : "").Append(kv.Key).Append(" x").Append(kv.Value); }
                }
                if (_stumbles > 0) sb.Append(" | potkniecia dzis ").Append(_stumbles).Append(" (razem ").Append(_stumblesAll).Append(')');
                Log.Info(sb.Append('.').ToString());
            }
            catch (Exception e) { Log.Error("UniqueShelf.Daily", e); }
            _hidden = _restored = _towns = _stumbles = 0; _ids.Clear();
        }
    }
}
