using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// E3-Z1 (noc-ekonomia3 16.10, czesc 2; klucz MenUpgradeOrders) - ZAMOWIENIA ZOLNIERZY I PODZIAL LICZNIKA "BRAK LEPSZEJ NA POLCE".
    /// Dowod [L] (test noc-ekonomia2 21-25-46 d109195, "Dozbrajanie"): pominiete koszyki "brak lepszej na polce" 26 604 na dobe przy 217 zakupach lordow i 330 zalog; tylko 3 z 97
    /// miast nie ma na polce zbroi korpusu t3+, wiec licznik miesza dwie rzeczy: zolnierz na suficie swojego tieru (nic lepszego mu nie wolno) i zolnierz pod sufitem,
    /// dla ktorego polka nie ma towaru. Tylko ta druga to brak, na ktory warsztaty moga odpowiedziec.
    /// Regula: koszyk dozbrajania bez lepszej sztuki (MenUpgrade.Pick why 0 albo 5) liczy sie osobno "na suficie" (sufit jednostki SwapMath.CeilingTier <= tier sztuki)
    /// i "pod sufitem, bez towaru". Pod sufitem (bez strzal i beltow - maja AmmoOrders) skladamy zamowienie tieru + 1 (konwencja tieru 1-6, AiGear.TierOf) w MIESCIE
    /// tej wizyty (shop1 albo shop2 - zamek pomijamy), najwyzej 5 sztuk, najwyzej 1 zamowienie Z1 na partie na dobe, ten sam rodzaj (partia x miasto x typ x tier)
    /// najwyzej raz na SupplyDemandOrderRepeatDays dob - WLASNY slownik dedupu (uwaga recenzji: 26.6 tys. koszykow/d przepelnialoby slownik _onceSeen wpisu 81 (sufit
    /// 20 000, potem Clear) i kasowalo zabezpieczenie petli drozenia innym kupcom); stare wpisy czyszczone raz na dobe (tylko przeterminowane). Licznik czyszczen
    /// _onceSeen (SupplyDemand.OnceClears) w linii - cel 0.
    /// Kto placi, kto dostaje: samo zamowienie nie przelewa zlota (popyt koszyka SupplyDemand.NoteUnmet - warsztaty zbrojne planuja wedlug niego). Zakup potem jak dzis:
    /// sakiewka -> kasa miasta. Nic w zapisie poza istniejacymi zamowieniami SupplyDemand (arm_unmet). Wyjatek - potkniecie (licznik).
    /// 268 (Jeff 27.10 "zamowienia zbroi bez limitu 1 dziennie"; audyt: "partia zamowila juz dzis 11132, bez miasta w wizycie 5975" d109002): przy AiArmourOrderFull
    /// zbroja na tulow i helm BEZ limitu 1 zamowienia na partie na dobe (bez sprawdzenia i bez stempla _partyDay; inne typy - jak dotad); zaloga zamku poza dniem
    /// zamowienia (shop2 = null) - zamowienie w miescie zamku (GarrisonCarts.MarketFor, to samo miasto co wozy). Dedup partia x miasto x typ x tier+1 przez
    /// SupplyDemandOrderRepeatDays i n = min(5, slotN) bez zmian - to zyczenia lepszej sztuki, nie braki (caly brak zamawia ArmourOrders przy pustej polce).
    /// Note zwraca kod (autotest 268 T4); wolajacy go nie czyta.
    /// </summary>
    internal static class MenUpgradeOrders
    {
        private const int MaxPieces = 5;
        private static readonly Dictionary<string, int> _seen = new Dictionary<string, int>();
        private static readonly Dictionary<MobileParty, int> _partyDay = new Dictionary<MobileParty, int>();
        private static int _dAtCeil, _dUnder, _dOrders, _dPieces, _dPartyCap, _dRepeat, _dNoTown, _dAmmo, _dStumbles, _sessStumbles, _clears0 = -1;
        private static int _dArmOrders, _dArmPieces, _dArmCastle;   // 268: zamowienia zbroi i helmow bez limitu doby, z tego w miescie zamku
        internal const int ROrdered = 0, RAtCeil = 1, ROff = 2, RAmmo = 3, RNoTown = 4, RPartyCap = 5, RRepeat = 6, RErr = -1;   // 268: wynik Note (autotest T4)
        private static bool _err;

        internal static bool On { get { var s = Settings.Current; return s != null && s.MenUpgradeOrders; } }

        internal static void Reset()
        {
            _seen.Clear(); _partyDay.Clear(); _err = false; _sessStumbles = 0; _clears0 = -1;
            _dAtCeil = _dUnder = _dOrders = _dPieces = _dPartyCap = _dRepeat = _dNoTown = _dAmmo = _dStumbles = 0;
            _dArmOrders = _dArmPieces = _dArmCastle = 0;
        }

        /// <summary>268 (autotest T4): zapomnij stempel doby i dedup partii - proba zaczyna od czystego stanu tej partii. Watek glowny.</summary>
        internal static void TestForget(MobileParty mp)
        {
            if (mp == null) return;
            _partyDay.Remove(mp);
            string pre = (mp.StringId ?? mp.GetHashCode().ToString()) + "|";
            var rm = new List<string>();
            foreach (var k in _seen.Keys) if (k.StartsWith(pre, StringComparison.Ordinal)) rm.Add(k);
            foreach (var k in rm) _seen.Remove(k);
        }

        /// <summary>MenUpgrade.Run: pierwszy kandydat slotu bez lepszej sztuki (why 0 albo 5). Licznik podzialu zawsze; zamowienie przy kluczu i tylko pod sufitem tieru.</summary>
        internal static int Note(MobileParty mp, Settlement shop1, Settlement shop2, ItemObject.ItemTypeEnum type, int tier, int ceil, int slotN)
        {
            try
            {
                if (ceil <= tier) { _dAtCeil++; return RAtCeil; }
                _dUnder++;
                if (!On || mp == null) return ROff;
                if (type == ItemObject.ItemTypeEnum.Arrows || type == ItemObject.ItemTypeEnum.Bolts) { _dAmmo++; return RAmmo; }
                bool arm = ArmourOrders.On && ArmourOrders.IsArmour(type);   // 268: zbroja na tulow i helm - bez limitu doby
                var mk = shop1 != null && shop1.IsTown ? shop1 : (shop2 != null && shop2.IsTown ? shop2 : null);
                bool viaCastle = false;
                if (mk == null && arm && GarrisonCarts.On)
                {
                    // 268: zaloga zamku poza dniem zamowienia - miasto zamku (to samo, z ktorego jada wozy; zamowienie to popyt, bez zlota i bez towaru)
                    var castle = shop1 != null && shop1.IsCastle ? shop1 : (shop2 != null && shop2.IsCastle ? shop2 : null);
                    if (castle != null) { float d; string why; mk = GarrisonCarts.MarketFor(castle, out d, out why, false); viaCastle = mk != null; }
                }
                if (mk == null) { _dNoTown++; return RNoTown; }
                int day = (int)CampaignTime.Now.ToDays, last;
                if (!arm && _partyDay.TryGetValue(mp, out last) && last == day) { _dPartyCap++; return RPartyCap; }
                int t1 = Math.Max(1, Math.Min(6, tier + 1));
                string key = (mp.StringId ?? mp.GetHashCode().ToString()) + "|" + (mk.StringId ?? "?") + "|" + (int)type + "|" + t1;
                int repeat = Math.Max(1, Settings.Current.SupplyDemandOrderRepeatDays);
                if (_seen.TryGetValue(key, out last) && day - last < repeat) { _dRepeat++; return RRepeat; }
                _seen[key] = day;
                if (!arm) _partyDay[mp] = day;   // 268: zbroja i helm nie zabieraja partii zamowienia innego typu tego dnia
                int n = Math.Max(1, Math.Min(MaxPieces, slotN));
                SupplyDemand.NoteUnmet(mk, type, t1, n);
                _dOrders++; _dPieces += n;
                if (arm) { _dArmOrders++; _dArmPieces += n; if (viaCastle) _dArmCastle++; }
                return ROrdered;
            }
            catch (Exception e) { _dStumbles++; _sessStumbles++; if (!_err) { _err = true; Log.Error("MenUpgradeOrders.Note", e); } return RErr; }
        }

        /// <summary>Dopisek linii "Dozbrajanie: dzien" (zaraz po "brak lepszej na polce N"): podzial licznika i zamowienia Z1; zeruje liczniki doby i czysci przeterminowane wpisy.</summary>
        internal static string DayText()
        {
            string t;
            try
            {
                int clears = SupplyDemand.OnceClears;
                int dc = _clears0 < 0 ? 0 : clears - _clears0;
                _clears0 = clears;
                t = " (E3-Z1: na suficie tieru " + _dAtCeil + ", pod sufitem bez towaru " + _dUnder + "; zamowienia tier+1 (Men Upgrade Orders " + (On ? "tak" : "nie") + ") " + _dOrders + " za " + _dPieces + " szt."
                    + ", pominiete: partia zamowila juz dzis " + _dPartyCap + ", ten sam rodzaj w " + Math.Max(1, Settings.Current.SupplyDemandOrderRepeatDays) + " dobach " + _dRepeat + ", bez miasta w wizycie " + _dNoTown + ", amunicja " + _dAmmo
                    + "; zbroja i helm bez limitu doby (268, AI Armour Order Full " + (ArmourOrders.On ? "tak" : "nie") + "): zamowien " + _dArmOrders + " za " + _dArmPieces + " szt., w miescie zamku " + _dArmCastle
                    + "; czyszczenia slownika zamowien wpisu 81 dzis " + dc + " (cel 0); potkniecia " + _dStumbles + " (sesja " + _sessStumbles + "))";
                int day = (int)CampaignTime.Now.ToDays, repeat = Math.Max(1, Settings.Current.SupplyDemandOrderRepeatDays);
                var old = new List<string>();
                foreach (var kv in _seen) if (day - kv.Value >= repeat) old.Add(kv.Key);
                foreach (var k in old) _seen.Remove(k);
                var gone = new List<MobileParty>();
                foreach (var kv in _partyDay) if (kv.Value != day || !kv.Key.IsActive) gone.Add(kv.Key);
                foreach (var k in gone) _partyDay.Remove(k);
            }
            catch (Exception e) { t = " (E3-Z1: blad linii)"; Log.Error("MenUpgradeOrders.DayText", e); }
            _dAtCeil = _dUnder = _dOrders = _dPieces = _dPartyCap = _dRepeat = _dNoTown = _dAmmo = _dStumbles = 0;
            _dArmOrders = _dArmPieces = _dArmCastle = 0;
            return t;
        }
    }
}
