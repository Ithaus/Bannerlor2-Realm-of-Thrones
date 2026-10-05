using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// KSIEGA RUDY (wpis 94; Jeff 05.10: "czemu rudy jest za malo? da sie zwiekszyc?" -> "B": najpierw rachunek historyczny).
    /// Tylko log. Gra nigdzie nie podaje, ile rudy wykopaly wsie (docs/AUDYT-SUROWCE.md - "P1 diagnostyka"). Codziennie:
    /// wydobycie wsi (ladunki i tony, ile wsi kopalo), zuzycie warsztatow zbrojnych, zmiana zapasu na targach miast i reszta
    /// (konsumpcja miast/BK, kuznie narzedzi, kucie gracza) = wydobycie - warsztaty - przyrost zapasu.
    /// </summary>
    internal static class OreLedger
    {
        private static float _mined; private static int _villages; private static float _workshops;
        private static readonly HashSet<Village> _seen = new HashSet<Village>();
        private static int _day = -1, _lastStock = -1;

        internal static void Reset() { _mined = 0f; _villages = 0; _workshops = 0f; _seen.Clear(); _day = -1; _lastStock = -1; }

        private static void Roll() { int d = (int)CampaignTime.Now.ToDays; if (d != _day) { _seen.Clear(); _day = d; } }

        /// <summary>Wolane z MaterialLaw.ProdPostfix (wynik koncowy modelu): raz na wies na dobe.</summary>
        internal static void NoteVillage(Village v, ItemObject item, float amount)
        {
            try
            {
                if (v == null || item == null || item.StringId != "iron") return;
                Roll();
                if (!_seen.Add(v)) return;
                _mined += Math.Max(0f, amount); if (amount > 0f) _villages++;
            }
            catch { }
        }

        internal static void NoteWorkshop(int loads) { if (loads > 0) _workshops += loads; }

        internal static void Daily()
        {
            try
            {
                var ore = MBObjectManager.Instance.GetObject<ItemObject>("iron");
                if (ore == null) return;
                int stock = 0;
                foreach (var st in Settlement.All) if (st != null && st.IsTown && st.ItemRoster != null) stock += st.ItemRoster.GetItemNumber(ore);
                float kg = Math.Max(0.1f, ore.Weight);
                if (_lastStock >= 0)
                {
                    int delta = stock - _lastStock;
                    float rest = _mined - _workshops - delta;
                    Log.Info("Ruda: dzien " + ((int)CampaignTime.Now.ToDays - 1) + " - wsie wykopaly " + _mined.ToString("0.#") + " ladunkow (" + (_mined * kg / 1000f).ToString("0.0")
                             + " t, kopalo " + _villages + " wsi); warsztaty zbrojne zuzyly " + _workshops.ToString("0") + "; zapas na targach miast " + stock + " (" + (delta >= 0 ? "+" : "") + delta
                             + "); reszta (konsumpcja miast/BK, kuznie narzedzi, kucie, tabor) " + rest.ToString("0") + ". Ladunek = " + kg.ToString("0") + " kg.");
                }
                _lastStock = stock; _mined = 0f; _villages = 0; _workshops = 0f;
            }
            catch (Exception e) { Log.Error("OreLedger", e); }
        }
    }
}
