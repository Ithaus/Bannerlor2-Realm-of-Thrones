using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// 568 (Jeff 15.11, "ABCDE tak rob" - punkt B): LUP W KRYJOWCE. Dotad bandy odnosily do kryjowki tylko zloto (dola dzienna StashDay),
    /// a zrabowany towar zostawal w jukach i szedl do pasera; magazyn kryjowki (Settlement.ItemRoster) byl zawsze pusty, wiec ekran lupu
    /// po przeszukaniu (HideoutPurge.BuildLoot) nigdy sie nie pokazywal.
    ///
    /// DOLA TOWAROWA (Day, raz na dobe w OutlawLaw.Daily - po doli zlota, PRZED paserem): kazda banda z kryjowka (BanditPartyComponent.Hideout,
    /// ta sama regula co dola zlota) odnosi HideoutGoodsStashShare kazdego stosu ZRABOWANEGO TOWARU (ItemTypeEnum.Goods, bez zywnosci - zapas bandy
    /// i to, co sprzeda paserowi, bez stad i koni, bez broni i pancerzy - sprzet bandy i sprzet do awansow, bez przedmiotow zadan i NotMerchandise).
    /// Zaokraglenie losowe (MBRandom.RoundRandomized) - maly stos tez czasem trafia do kryjowki. Przelew juki -> magazyn kryjowki: sztuka schodzi
    /// z jukow i trafia do magazynu (nieudane dolozenie cofa zdjecie) - nic nie powstaje, nic nie ginie.
    /// Poza dola (jak przy zlocie): banda w bitwie, zajeta przez zadanie, nieumarli, kryjowka czekajaca na przeszukanie gracza (HideoutPurge.Holds).
    /// (Bez warunku Hideout.IsInfested - to tylko liczba band w srodku w tej chwili; dola zlota tez go nie ma.)
    /// SUFIT (HideoutGoodsStashCap, wartosc bazowa Item.Value): pelny magazyn nie przyjmuje wiecej - reszta idzie do pasera jak dotad. Bez sufitu towar
    /// z niezdobytych kryjowek schodzilby z rynku bez konca.
    ///
    /// ODBIOR: gracz - przy PRZESZUKANIU (HideoutPurge.DoSearch -> BuildLoot, ekran lupu jak po bitwie; odejscie bez przeszukania zostawia towar jak zloto);
    /// zwyciezca AI (HideoutPurge.AiCleared, ten sam odbiorca co skarbiec zlota): wodz / partia -> juki partii, milicja / patrol / nieumarli -> magazyn osady.
    /// Gra sama magazynu kryjowki nie lupi (DefaultBattleRewardModel.GetLootItemChancesForWinnerParties: osada pokonana - pusta lista).
    ///
    /// Klucz HideoutGoodsStash off = jak dotad (towar zostaje w jukach, magazyn czytany przy bitwie jak dawniej). Wyjatek per banda - licznik potkniec.
    /// Linia doby "Lup w kryjowkach (568): dzien N".
    /// </summary>
    internal static class HideoutGoods
    {
        private static int _bands, _bandsCapped, _units, _stumbles, _plUnits, _aiUnits, _plN, _aiN;
        private static long _value, _plValue, _aiValue;
        private static bool _errLogged;
        private static readonly Dictionary<Settlement, long> _stashVal = new Dictionary<Settlement, long>();

        internal static bool On { get { var s = Settings.Current; return s != null && s.HideoutGoodsStash; } }

        internal static void Reset()
        {
            _bands = _bandsCapped = _units = _stumbles = _plUnits = _aiUnits = _plN = _aiN = 0;
            _value = _plValue = _aiValue = 0;
            _errLogged = false;
            _stashVal.Clear();
        }

        /// <summary>Czy stos to zrabowany towar, ktory banda odnosi do kryjowki (nie zywnosc, nie zwierze, nie sprzet).</summary>
        private static bool Stashable(ItemRosterElement el)
        {
            var it = el.EquipmentElement.Item;
            if (it == null || el.Amount <= 0) return false;
            if (el.EquipmentElement.IsQuestItem || it.NotMerchandise || it.IsBannerItem) return false;
            if (it.IsFood) return false;
            return it.ItemType == ItemObject.ItemTypeEnum.Goods;
        }

        private static long ValueOf(ItemRoster r)
        {
            long v = 0;
            if (r == null) return 0;
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                if (el.Amount > 0 && el.EquipmentElement.Item != null) v += (long)el.Amount * Math.Max(1, el.EquipmentElement.Item.Value);
            }
            return v;
        }

        /// <summary>Dola towarowa doby - wolana z OutlawLaw.Daily po doli zlota, przed paserem.</summary>
        internal static void Day(Settings s)
        {
            if (s == null || !s.HideoutGoodsStash) return;
            float share = MBMath.ClampFloat(s.HideoutGoodsStashShare, 0f, 1f);
            if (share <= 0f) return;
            long cap = Math.Max(0, s.HideoutGoodsStashCap);
            _stashVal.Clear();
            List<MobileParty> list;
            try { list = MobileParty.AllBanditParties.ToList(); }
            catch (Exception e) { Stumble("Day(lista)", e); return; }
            foreach (var p in list)
            {
                try
                {
                    if (p == null || !p.IsActive || p.ItemRoster == null || p.ItemRoster.Count == 0) continue;
                    var home = OutlawLaw.HomeOf(p);
                    if (home == null || home.ItemRoster == null) continue;   // jak dola zlota: bez warunku IsInfested (to liczba band W SRODKU w tej chwili - kryjowka bandy wraca do gry z nia)
                    if (p.MapEvent != null || p.IsCurrentlyUsedByAQuest || Undead.Party(p) || HideoutPurge.Holds(home)) continue;
                    long have;
                    if (!_stashVal.TryGetValue(home, out have)) { have = ValueOf(home.ItemRoster); _stashVal[home] = have; }
                    var pack = p.ItemRoster;
                    int moved = 0; bool capped = false;
                    // od konca: oddany do zera stos znika, a na jego miejsce wskakuje ostatni (juz obejrzany)
                    for (int i = pack.Count - 1; i >= 0; i--)
                    {
                        if (i >= pack.Count) continue;
                        var el = pack.GetElementCopyAtIndex(i);
                        if (!Stashable(el)) continue;
                        int n = MBRandom.RoundRandomized(el.Amount * share);
                        if (n <= 0) continue;
                        if (n > el.Amount) n = el.Amount;
                        int v = Math.Max(1, el.EquipmentElement.Item.Value);
                        if (cap > 0)
                        {
                            long room = cap - have;
                            if (room < v) { capped = true; continue; }   // tanszy stos moze sie jeszcze zmiescic
                            if ((long)n * v > room) { n = (int)(room / v); capped = true; }
                        }
                        var what = el.EquipmentElement;
                        pack.AddToCounts(what, -n);
                        bool placed = false;
                        try { home.ItemRoster.AddToCounts(what, n); placed = true; }
                        finally { if (!placed) pack.AddToCounts(what, n); }   // nieudane dolozenie - towar wraca do jukow
                        have += (long)n * v; moved += n;
                        _units += n; _value += (long)n * v;
                    }
                    _stashVal[home] = have;
                    if (moved > 0) _bands++;
                    if (capped) _bandsCapped++;
                }
                catch (Exception e) { Stumble("Day", e); }
            }
        }

        /// <summary>Caly magazyn kryjowki do `dst` (zwyciezca AI). Zwraca liczbe sztuk; wartosc bazowa w `value`.</summary>
        internal static int TakeAll(Settlement hideout, ItemRoster dst, out long value)
        {
            value = 0;
            if (hideout == null || hideout.ItemRoster == null || dst == null) return 0;
            var src = hideout.ItemRoster;
            int units = 0;
            for (int i = src.Count - 1; i >= 0; i--)
            {
                if (i >= src.Count) continue;
                var el = src.GetElementCopyAtIndex(i);
                if (el.Amount <= 0 || el.EquipmentElement.Item == null) continue;
                src.AddToCounts(el.EquipmentElement, -el.Amount);
                bool placed = false;
                try { dst.AddToCounts(el.EquipmentElement, el.Amount); placed = true; }
                finally { if (!placed) src.AddToCounts(el.EquipmentElement, el.Amount); }
                units += el.Amount; value += (long)el.Amount * Math.Max(1, el.EquipmentElement.Item.Value);
            }
            if (units > 0) { _aiUnits += units; _aiValue += value; _aiN++; }
            return units;
        }

        /// <summary>Gracz zabral magazyn kryjowki przy przeszukaniu (HideoutPurge.BuildLoot) - tylko licznik.</summary>
        internal static void NotePlayer(ItemRoster loot)
        {
            if (loot == null || loot.Count == 0) return;
            for (int i = 0; i < loot.Count; i++) { var el = loot.GetElementCopyAtIndex(i); if (el.Amount > 0) _plUnits += el.Amount; }
            _plValue += ValueOf(loot); _plN++;
        }

        internal static long ValueOfRoster(ItemRoster r) { return ValueOf(r); }

        internal static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (!_errLogged) { _errLogged = true; Log.Error("HideoutGoods." + where, e); }
        }

        /// <summary>Linia doby: doplyw do magazynow kryjowek, odbior (gracz, AI), stan magazynow. Tylko log; zeruje liczniki doby.</summary>
        internal static void LogDay(Settings s)
        {
            try
            {
                if (s == null) return;
                int dens = 0, full = 0; long units = 0, val = 0;
                long cap = Math.Max(0, s.HideoutGoodsStashCap);
                foreach (var st in Settlement.All)
                {
                    if (st == null || !st.IsHideout || st.ItemRoster == null || st.ItemRoster.Count == 0) continue;
                    long v = ValueOf(st.ItemRoster);
                    if (v <= 0) continue;
                    dens++; val += v;
                    for (int i = 0; i < st.ItemRoster.Count; i++) { var el = st.ItemRoster.GetElementCopyAtIndex(i); if (el.Amount > 0) units += el.Amount; }
                    if (cap > 0 && v >= cap - 50) full++;
                }
                if (!s.HideoutGoodsStash && dens == 0 && _plN == 0 && _aiN == 0) return;
                var inv = CultureInfo.InvariantCulture;
                Log.Info("Lup w kryjowkach (568): dzien " + (int)CampaignTime.Now.ToDays
                         + (s.HideoutGoodsStash ? " | udzial " + MBMath.ClampFloat(s.HideoutGoodsStashShare, 0f, 1f).ToString("0.00", inv) + ", sufit " + (cap > 0 ? cap + " zl" : "brak") : " | KLUCZ WYLACZONY (dola towarowa nie biegnie)")
                         + " | do kryjowek: band " + _bands + ", sztuk " + _units + " wart " + _value + " zl, band przy suficie " + _bandsCapped
                         + " | odbior: gracz " + _plN + "x (" + _plUnits + " szt., " + _plValue + " zl), AI " + _aiN + "x (" + _aiUnits + " szt., " + _aiValue + " zl)"
                         + " | magazyny: kryjowek z towarem " + dens + " (pelnych " + full + "), sztuk " + units + ", wart " + val + " zl"
                         + " | potkniecia " + _stumbles + ".");
            }
            finally
            {
                _bands = _bandsCapped = _units = _plUnits = _aiUnits = _plN = _aiN = 0;
                _value = _plValue = _aiValue = 0;
            }
        }
    }
}
