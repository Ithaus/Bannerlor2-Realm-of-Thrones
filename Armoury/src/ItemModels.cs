using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// PRZEDMIOTY BEZ MODELU (m110-modele, Jeff 23.10: "dlaczego rekawice nie maja obrazka, zobacz, czy nie ma wiecej przedmiotow bez obrazka"; klucz
    /// MissingModelFix). Obrazek i wyglad przedmiotu gra bierze z modelu ItemObject.MultiMeshName - gdy takiego modelu nie ma w silniku, przedmiot jest bez
    /// obrazka i niewidoczny. Skan autotestu (InventoryLookTest, 23.10): 3607 przedmiotow, modelu brak przy 6 w handlu - literowka ROT (andal_noble_women_shoes2,
    /// w silniku andal_noble_women__shoes2) i towary BK bez swoich modeli (limestone, goldore, marblestone, goldingot, pouchofgems); ROT ma tez nazwy z
    /// wielkimi literami (Karstark_Gambeson przy karstark_gambeson w silniku). Raz na sesje (przy starcie kampanii, po rejestracji modeli): model nieznany
    /// silnikowi -> ta sama nazwa malymi literami, nazwa bez podwojnych podkreslen, albo podobny model z gry (tabela Fallback); reszta bez zmian. Linia
    /// "Modele przedmiotow (m110): ...". Przedmioty "none" (domyslne glowy, nakladki DismembermentPlus) - celowo bez modelu, poza zakresem.
    /// Rekawice (i podglad postaci) bez obrazka 23.10 - to nie brak modelu, tylko indeksy akcji (HeroHp.Install, m110-animacje).
    /// </summary>
    internal static class ItemModels
    {
        private const string P = "Modele przedmiotow (m110): ";
        private static readonly Dictionary<string, string> Fallback = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "limestone", "merchandise_stones" },
            { "marblestone", "merchandise_stones" },
            { "goldore", "silver_ore" },
            { "goldingot", "merchandise_gold_b" },
            { "pouchofgems", "merchandise_gold" },
        };
        private static MethodInfo _setMesh;

        internal static void Apply()
        {
            var s = Settings.Current;
            if (s == null || !s.MissingModelFix) { Log.Info(P + "klucz Missing Model Fix NIE - bez zmian."); return; }
            try
            {
                var list = new List<TaleWorlds.Engine.MetaMesh>();
                TaleWorlds.Engine.MetaMesh.GetAllMultiMeshes(ref list);
                var exact = new HashSet<string>(StringComparer.Ordinal);
                var lower = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var m in list)
                {
                    if (m == null) continue;
                    string n = m.GetName();
                    if (string.IsNullOrEmpty(n)) continue;
                    exact.Add(n);
                    if (!lower.ContainsKey(n)) lower[n] = n;
                }
                if (exact.Count < 1000) { Log.Info(P + "modeli w silniku tylko " + exact.Count + " - lista niepelna, bez zmian."); return; }
                if (_setMesh == null) _setMesh = AccessTools.PropertySetter(typeof(ItemObject), "MultiMeshName");
                if (_setMesh == null) { Log.Info(P + "brak settera ItemObject.MultiMeshName - bez zmian."); return; }
                int items = 0, missing = 0, caseFix = 0, typoFix = 0, swapped = 0, left = 0;
                var shown = new List<string>();
                foreach (var it in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
                {
                    if (it == null) continue;
                    string mesh = it.MultiMeshName;
                    if (string.IsNullOrEmpty(mesh) || mesh == "none") continue;   // bron z czesci (wykuta) i celowo puste
                    items++;
                    if (exact.Contains(mesh)) continue;
                    missing++;
                    string to = null, how = null;
                    string hit;
                    if (lower.TryGetValue(mesh, out hit)) { to = hit; how = "wielkosc liter"; caseFix++; }
                    else
                    {
                        string flat = Flat(mesh);
                        foreach (var kv in lower) if (Flat(kv.Key) == flat) { to = kv.Value; break; }
                        if (to != null) { how = "literowka"; typoFix++; }
                        else if (Fallback.TryGetValue(mesh, out hit) && exact.Contains(hit)) { to = hit; how = "podobny model"; swapped++; }
                    }
                    if (to == null) { left++; if (shown.Count < 12) shown.Add(it.StringId + " (" + mesh + ") - bez zamiennika"); continue; }
                    _setMesh.Invoke(it, new object[] { to });
                    if (shown.Count < 12) shown.Add(it.StringId + " " + mesh + " -> " + to + " (" + how + ")");
                }
                Log.Info(P + "start sesji | modeli w silniku " + exact.Count + " | przedmiotow z modelem " + items + ", model nieznany silnikowi " + missing + ": wielkosc liter "
                         + caseFix + ", literowka " + typoFix + ", podobny model " + swapped + ", bez zamiennika " + left + " | " + (shown.Count > 0 ? string.Join("; ", shown.ToArray()) : "brak") + ".");
            }
            catch (Exception e) { Log.Error("ItemModels.Apply", e); }
        }

        /// <summary>Nazwa bez wielkich liter i z pojedynczymi podkresleniami (andal_noble_women__shoes2 = andal_noble_women_shoes2).</summary>
        private static string Flat(string n)
        {
            n = n.ToLowerInvariant();
            while (n.Contains("__")) n = n.Replace("__", "_");
            return n;
        }
    }
}
