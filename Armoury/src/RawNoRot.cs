using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// PACZKA 174.3 (C) - KONIEC GNICIA SUROWCOW TRWALYCH W BK (audyt 09.10, 03 P1-D; docs/PROJEKT-174-PRODUKCJA-UZBROJENIA-2026-10-09.md rozdz. 3.4).
    /// BK BKSettlementBehavior.DeleteOverProduction (miasta i zamki, co dobe): stosy wyrobow gracza i sztandary - cale (jak gra), a kazdy stos towaru
    /// handlowego ponad 500 sztuk (bez jedzenia, zwierzat, zbroi i broni) traci 2% dziennie - w nicosc. Dzis drewno 9/d (A171), 179/d w roku TOWARY 3;
    /// po krokach lasu (x1.6) i kopaln (x1.5) wiecej surowca = wiecej w nicosc. Przy BkRawNoRot prefiks robi te sama petle co BK (ten sam warunek
    /// i wylacznik BK DeleteOverProduction), z inna stawka: ruda, metale (sztabki), narzedzia, skora i plotno 0%; drewno, len i welna 0.2% dziennie
    /// (sklad bez dachu - stawka 10 razy nizsza), reszta towarow 2% jak w BK. Wylaczone - oryginal BK.
    /// </summary>
    internal static class RawNoRot
    {
        internal static bool Hooked;
        private static PropertyInfo _bkInst, _bkFlag;
        private static long _cut, _spared;
        private static int _stumbles;

        internal static bool On { get { var s = Settings.Current; return s != null && s.BkRawNoRot && Hooked; } }

        internal static void Reset() { _cut = _spared = 0; _stumbles = 0; }

        private static bool BkDeletes()
        {
            try { var inst = _bkInst != null ? _bkInst.GetValue(null, null) : null; return inst != null && (bool)_bkFlag.GetValue(inst, null); }
            catch { return true; }
        }

        /// <summary>Stawka gnicia dziennie dla stosu ponad 500 (BK: 2%).</summary>
        private static float Rate(ItemObject it)
        {
            string id = it.StringId ?? "";
            if (id == "iron" || id == "tools" || id == "leather" || id == "linen" || id.StartsWith("ironIngot")) return 0f;
            if (id == "hardwood" || id == "flax" || id == "wool") return 0.002f;
            return 0.02f;
        }

        /// <summary>Prefiks BK DeleteOverProduction(Town): petla BK z nasza stawka. false = oryginal BK pominiety.</summary>
        public static bool Prefix(Town __0)
        {
            var s = Settings.Current;
            if (s == null || !s.BkRawNoRot || __0 == null) return true;
            try
            {
                var roster = __0.Owner != null ? __0.Owner.ItemRoster : null;
                if (roster == null) return true;
                bool bk = BkDeletes();
                for (int i = roster.Count - 1; i >= 0; i--)
                {
                    var el = roster.GetElementCopyAtIndex(i);
                    var item = el.EquipmentElement.Item;
                    int amount = el.Amount;
                    if (item == null) continue;
                    if (amount > 0 && (item.IsCraftedByPlayer || item.IsBannerItem)) { roster.AddToCounts(el.EquipmentElement, -amount); continue; }
                    if (bk && !item.IsFood && !item.IsAnimal && item.IsTradeGood && !item.HasArmorComponent && !item.HasWeaponComponent && amount > 500)
                    {
                        int bkCut = (int)(amount * 0.02f);
                        int cut = (int)(amount * Rate(item));
                        if (cut > 0) roster.AddToCounts(el.EquipmentElement, -cut);
                        _cut += cut; _spared += Math.Max(0, bkCut - cut);
                    }
                }
            }
            catch (Exception e) { _stumbles++; if (_stumbles == 1) Log.Error("RawNoRot.Prefix", e); }
            return false;
        }

        /// <summary>Linia dnia (tylko gdy cos sie dzialo).</summary>
        internal static void Daily()
        {
            if (!On && _cut == 0 && _spared == 0) return;
            Log.Info("BK gnicie surowcow (174.3): dzien " + ((int)CampaignTime.Now.ToDays - 1) + " - skasowano " + _cut + " szt. (drewno, len, welna 0.2% i inne towary 2% ze stosow > 500), oszczedzone wobec BK ok. "
                     + _spared + " szt. (ruda, metale, narzedzia, skora, plotno 0%); potkniecia " + _stumbles + ".");
            _cut = _spared = 0; _stumbles = 0;
        }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var t = QuartermasterLaw.FindType("BannerKings.Behaviours.BKSettlementBehavior");
                var m = t != null ? AccessTools.Method(t, "DeleteOverProduction", new[] { typeof(Town) }) : null;
                var st = QuartermasterLaw.FindType("BannerKings.Settings.BannerKingsSettings");
                _bkInst = st != null ? AccessTools.Property(st, "Instance") : null;
                _bkFlag = st != null ? AccessTools.Property(st, "DeleteOverProduction") : null;
                if (m != null && _bkInst != null && _bkFlag != null && _bkFlag.PropertyType == typeof(bool))
                {
                    h.Patch(m, prefix: new HarmonyMethod(typeof(RawNoRot), nameof(Prefix)));
                    Hooked = true;
                }
                Log.Info("RawNoRot (174.3): BK DeleteOverProduction - " + (Hooked ? "latka wpieta (ruda, metale, narzedzia, skora, plotno 0%; drewno, len, welna 0.2%)" : t == null ? "bez BK - nic do wpiecia" : "BRAK metody albo ustawienia BK - gnicie jak w BK") + ".");
            }
            catch (Exception e) { Log.Error("RawNoRot.ApplyAll", e); }
        }
    }
}
