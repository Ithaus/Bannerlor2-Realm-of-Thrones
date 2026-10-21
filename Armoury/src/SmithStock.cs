using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// OTWARTE LADUNKI LNU I SKORY W KUZNI GRACZA (m96-receptury, klucz RecipesByMass; Jeff 20.10: "len jest drogi ... tak jak w drewnie - wazy 100 kg, ale
    /// starcza na dluzej"). Sztuka lnu i skory wazy w grze 10 kg; przepis z masy (Recipes.Mass.cs) zuzywa kg (cieciwa 30 g, podszycie kolczugi 2 kg). Kuznia
    /// otwiera nowa sztuke z sakw dopiero, gdy otwarta sie skonczy; reszta zostaje tu (kg, zapis arm_smith_stock w ArmouryBehavior.SyncData przez SaveText.Sync,
    /// format "1|id:kg|id:kg"). Metal ma swoj otwarty ladunek w skrzynce resztek (SmeltScraps), drewno - SmithWood. Zamknieta ekonomia: kg stad to len tej
    /// samej, zdjetej z sakw sztuki - nic z niczego.
    /// </summary>
    internal static class SmithStock
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly Dictionary<string, float> _open = new Dictionary<string, float>();
        private const float MaxKg = 1000f;   // sanity: wiecej niz kilka sztuk otwartych naraz nie powstaje (Units = tyle, ile brakuje)
        private static int _errors;

        internal static void Reset() { _open.Clear(); }

        internal static float UnitKg(ItemObject it)
        {
            try { return it != null && it.Weight > 0.01f ? it.Weight : 10f; }
            catch { return 10f; }
        }

        internal static float OpenKg(ItemObject it)
        {
            float v;
            return it != null && it.StringId != null && _open.TryGetValue(it.StringId, out v) ? v : 0f;
        }

        /// <summary>Ile sztuk z sakw otworzyc na kg (0, gdy otwarty ladunek starcza).</summary>
        internal static int UnitsFor(ItemObject it, float kg)
        {
            if (it == null || kg <= 0f) return 0;
            float lack = kg - OpenKg(it);
            return lack <= 0.0001f ? 0 : (int)Math.Ceiling(lack / UnitKg(it) - 1e-6);
        }

        /// <summary>Kucie zuzylo kg, a z sakw zdjeto units sztuk.</summary>
        internal static void Use(ItemObject it, float kg, int units)
        {
            try
            {
                if (it == null || it.StringId == null) return;
                float v = OpenKg(it) + Math.Max(0, units) * UnitKg(it) - Math.Max(0f, kg);
                if (v < 0f) v = 0f;          // brak w sakwach mimo sprawdzenia - nie tworzymy lnu z niczego
                if (v > MaxKg) v = MaxKg;
                _open[it.StringId] = v;
            }
            catch (Exception e) { _errors++; if (_errors <= 5) Log.Error("SmithStock.Use (potkniecie " + _errors + ")", e); }
        }

        /// <summary>Pekniecie pancerza w CRAFT BK (po recenzji m96-receptury): kg wracaja do otwartego ladunku, pelne sztuki ponad niego - do sakw (zwraca ile).</summary>
        internal static int Return(ItemObject it, float kg)
        {
            try
            {
                if (it == null || it.StringId == null || kg <= 0f) return 0;
                float u = UnitKg(it);
                float v = OpenKg(it) + kg;
                int whole = (int)Math.Floor(v / u);
                v -= whole * u;
                if (v < 0f) v = 0f;
                if (v > MaxKg) v = MaxKg;
                _open[it.StringId] = v;
                return Math.Max(0, whole);
            }
            catch (Exception e) { _errors++; if (_errors <= 5) Log.Error("SmithStock.Return (potkniecie " + _errors + ")", e); return 0; }
        }

        internal static string Text(bool player)
        {
            var sb = new StringBuilder();
            foreach (var kv in _open)
            {
                if (kv.Value <= 0.0005f) continue;
                if (sb.Length > 0) sb.Append(", ");
                string name = kv.Key;
                if (player)
                {
                    try { var it = TaleWorlds.ObjectSystem.MBObjectManager.Instance.GetObject<ItemObject>(kv.Key); if (it != null && it.Name != null) name = it.Name.ToString(); } catch { }
                }
                sb.Append(name).Append(' ').Append(kv.Value.ToString(kv.Value < 0.1f ? "0.###" : "0.##", Inv)).Append(" kg");
            }
            return sb.ToString();
        }

        internal static string Export()
        {
            var sb = new StringBuilder("1");
            foreach (var kv in _open) if (kv.Value > 0.0005f) sb.Append('|').Append(kv.Key).Append(':').Append(kv.Value.ToString("0.####", Inv));
            return sb.ToString();
        }

        /// <summary>Brak klucza (stary zapis), inny format albo zla liczba = nic otwartego.</summary>
        internal static void Import(string s)
        {
            _open.Clear();
            try
            {
                if (string.IsNullOrEmpty(s)) return;
                var p = s.Split('|');
                if (p.Length < 1 || p[0] != "1") return;
                for (int i = 1; i < p.Length; i++)
                {
                    int c = p[i].LastIndexOf(':');
                    if (c <= 0) continue;
                    float v;
                    if (float.TryParse(p[i].Substring(c + 1), NumberStyles.Float, Inv, out v) && v > 0f && !float.IsNaN(v) && v <= MaxKg) _open[p[i].Substring(0, c)] = v;
                }
            }
            catch (Exception e) { _errors++; _open.Clear(); if (_errors <= 5) Log.Error("SmithStock.Import (potkniecie " + _errors + ")", e); }
        }
    }
}
