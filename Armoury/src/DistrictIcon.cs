using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// inwest (noc 17/18.10) - IKONA OKREGU (projekt A9 / F17, etap 2): wies gry pokazuje na tabliczce glowny towar (VillageType.PrimaryProduction - zawsze jeden z rodzimych
    /// obrazkow gry) zajecia, ktore daje okregowi najwiecej pieniedzy (suma wartosci dnia wszystkich czesci okregu tego zajecia przy cenach miasta targowego - wies gry
    /// i wioski poboczne; pod KS z blokow KS, bez KS z zajec wiosek). Zmiana tylko, gdy nowe zajecie bije pokazane o District Icon Margin (10%). Liczone przy pierwszej
    /// dobie sesji i co 28 dob. Okreg stadniny zostaje przy koniu gry (tabory nie sprzedaja koni - wartosc koni poza rachunkiem).
    ///  - tabliczka: postfiks na prywatnej SettlementNameplateEventsVM.AddPrimaryProductionIcon (SandBox.ViewModelCollection - refleksja, bez referencji) ustawia
    ///    AdditionalParameters pozycji Production; VM zapamietane slabo (WeakReference) - przy zmianie ikony w sesji tylko AdditionalParameters (raz, nie co klatke);
    ///  - dymek osady (TooltipRefresherCollection i NavalTooltipRefresherCollection.RefreshSettlementTooltip): wartosc wiersza "Primary production" = towar okregu
    ///    i sklad ("cows (whole district: cattle 85%, grain 8%, fish 7%)");
    ///  - lista produkcji miasta (CampaignUIHelper.GetSettlementProductionTooltip): "cows (district)".
    /// VillageType.PrimaryProduction NIE jest latany (projekt). Klucz District Icon From Sum wylaczony - wszystko jak w grze. Wyjatki liczone, nic sie nie gasi.
    /// </summary>
    internal static class DistrictIcon
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        internal static bool On { get { var s = Settings.Current; return s != null && s.DistrictIconFromSum; } }

        private sealed class Info { public int Day = -1; public int Craft = -1; public ItemObject Item; public string IconId; public string Mix = ""; public string Counts = ""; }
        private static readonly Dictionary<string, Info> _info = new Dictionary<string, Info>(StringComparer.Ordinal);
        private static readonly List<WeakReference> _vms = new List<WeakReference>();
        private static int _lastDay = -1, _diff, _changed, _updated, _st, _stAll, _districts, _ks, _snowKs;
        private static double _ms;
        private static bool _wired; private static string _wiredText = "nie wpiete";
        private static Type _tVm, _tItem; private static FieldInfo _fSettlement; private static PropertyInfo _pEvents; private static FieldInfo _fEventType; private static PropertyInfo _pParam;

        internal static void Reset()
        {
            _info.Clear(); _vms.Clear(); _lastDay = -1; _diff = _changed = _updated = _st = _stAll = _districts = _ks = 0; _ms = 0;
        }

        private static void Stumble(string where, Exception e)
        {
            _st++; _stAll++;
            if (_stAll <= 3) Log.Error("DistrictIcon." + where, e);
        }

        // ============================================================ wpiecie (raz na proces)
        internal static void ApplyAll(Harmony h)
        {
            if (_wired) return;
            _wired = true;
            var parts = new List<string>();
            try
            {
                _tVm = AccessTools.TypeByName("SandBox.ViewModelCollection.Nameplate.SettlementNameplateEventsVM");
                var tItem = AccessTools.TypeByName("SandBox.ViewModelCollection.Nameplate.SettlementNameplateEventItemVM");
                _tItem = tItem;
                var m = _tVm != null ? AccessTools.DeclaredMethod(_tVm, "AddPrimaryProductionIcon") : null;
                _fSettlement = _tVm != null ? AccessTools.Field(_tVm, "_settlement") : null;
                _pEvents = _tVm != null ? AccessTools.Property(_tVm, "EventsList") : null;
                _fEventType = tItem != null ? AccessTools.Field(tItem, "EventType") : null;
                _pParam = tItem != null ? AccessTools.Property(tItem, "AdditionalParameters") : null;
                if (m != null && _fSettlement != null && _pEvents != null && _fEventType != null && _pParam != null)
                {
                    h.Patch(m, postfix: new HarmonyMethod(typeof(DistrictIcon), nameof(IconPostfix)));
                    parts.Add("tabliczka (AddPrimaryProductionIcon)");
                }
                else parts.Add("tabliczka NIE (brak metody albo pol SandBox)");
            }
            catch (Exception e) { parts.Add("tabliczka BLAD"); Log.Error("DistrictIcon.ApplyAll(tabliczka)", e); }
            foreach (var tn in new[] { "TaleWorlds.CampaignSystem.ViewModelCollection.TooltipRefresherCollection", "NavalDLC.View.NavalTooltipRefresherCollection" })
            {
                try
                {
                    var t = AccessTools.TypeByName(tn);
                    var mi = t != null ? AccessTools.Method(t, "RefreshSettlementTooltip") : null;
                    if (mi == null) continue;
                    h.Patch(mi, postfix: new HarmonyMethod(typeof(DistrictIcon), nameof(TipPostfix)) { priority = Priority.Low });
                    parts.Add("dymek " + t.Name);
                }
                catch (Exception e) { Log.Error("DistrictIcon.ApplyAll(" + tn + ")", e); }
            }
            try
            {
                var t = AccessTools.TypeByName("TaleWorlds.CampaignSystem.ViewModelCollection.CampaignUIHelper");
                var mi = t != null ? AccessTools.Method(t, "GetSettlementProductionTooltip") : null;
                if (mi != null) { h.Patch(mi, postfix: new HarmonyMethod(typeof(DistrictIcon), nameof(ProdListPostfix))); parts.Add("lista produkcji miasta"); }
            }
            catch (Exception e) { Log.Error("DistrictIcon.ApplyAll(lista)", e); }
            _wiredText = string.Join(", ", parts.ToArray());
            Log.Info(VillageCrafts.P + "ikona okregu - wpiete: " + _wiredText + " (VillageType.PrimaryProduction bez latki).");
        }

        // ============================================================ rachunek (pierwsza doba sesji i co 28 dob)
        /// <summary>ArmouryBehavior.OnDailyTick (po VillageCrafts.Daily): ikony okregow od nowa w pierwszej dobie sesji i co 28 dob; zmienione - tabliczki w sesji.</summary>
        internal static void Daily()
        {
            if (!On) return;
            int today = (int)CampaignTime.Now.ToDays;
            if (_lastDay >= 0 && today - _lastDay < 28) return;
            _lastDay = today;
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            _diff = _changed = _districts = _ks = _snowKs = 0;
            var s = Settings.Current;
            float margin = s != null ? Math.Max(0f, Math.Min(1f, s.DistrictIconMargin)) : 0.10f;
            var parts = new List<VillageCrafts.PartCraft>();
            foreach (var v in Village.All)
            {
                try
                {
                    if (v == null || v.Settlement == null || v.VillageType == null) continue;
                    bool ks;
                    if (!VillageCrafts.PartsOf(v, parts, out ks)) continue;
                    _districts++; if (ks) _ks++;
                    if (ks) foreach (var p in parts) if (!p.Main && p.Craft != VillageCrafts.CraftOfType(v.VillageType) && VillageCrafts.SnowCrop(p.Uid, p.Craft)) _snowKs++;
                    Info old; _info.TryGetValue(v.Settlement.StringId, out old);
                    var ni = Compute(v, parts, old, margin, today);
                    _info[v.Settlement.StringId] = ni;
                    var gameIcon = v.VillageType.PrimaryProduction;
                    if (ni.Item != null && !ReferenceEquals(ni.Item, gameIcon)) _diff++;
                    if (old != null && old.Craft >= 0 && old.Craft != ni.Craft) _changed++;
                }
                catch (Exception e) { Stumble("Daily(okreg)", e); }
            }
            _updated += UpdateVms();
            _ms = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            Log.Info(VillageCrafts.P + "ikony dzien " + today + " | okregow " + _districts + " (pod KS " + _ks + "), ikona inna niz typ wsi: " + _diff + " okregow (projekt: ok. 81 z 558, prog 40-130), zmienione od poprzedniego rachunku "
                     + _changed + ", tabliczki poprawione w sesji " + _updated + " | uprawy na sniegu pod KS (wioski) " + _snowKs + " | prog zmiany x" + (1 + margin).ToString("0.00", Inv) + " | wpiete: " + _wiredText + " | koszt " + _ms.ToString("0.0", Inv) + " ms | potkniecia " + _st + " (sesja " + _stAll + ").");
            _st = 0;
        }

        /// <summary>Do linii ZW: stan ikon (ostatni rachunek).</summary>
        internal static string DayText() { return On ? "okregow " + _districts + ", inna niz typ " + _diff + ", zmienione " + _changed : "WYLACZONA (District Icon From Sum)"; }

        private static Info Compute(Village v, List<VillageCrafts.PartCraft> parts, Info old, float margin, int today)
        {
            var val = new double[KsTab.NC];
            var cnt = new int[KsTab.NC];
            foreach (var p in parts)
            {
                for (int c = 0; c < KsTab.NC; c++) { double x = p.CraftVal[c]; if (x > 0) val[Fold(c)] += x; }
                if (p.Craft >= 0) cnt[Fold(p.Craft)]++;
            }
            var ni = new Info { Day = today };
            int D = VillageCrafts.CraftOfType(v.VillageType);
            bool horse = D >= KsTab.CHorse0 && D <= KsTab.CHorse5;
            int best = -1; double bv = 0;
            for (int c = 0; c < KsTab.NC; c++)
            {
                var m = VillageCrafts.Meta(c);
                if (m == null || m.Type == null || m.Icon == null || m.Horse || !(val[c] > bv)) continue;
                best = c; bv = val[c];
            }
            int keep = old != null ? old.Craft : (D >= 0 ? Fold(D) : -1);
            if (keep >= 0 && best >= 0 && best != keep && val[keep] > 0 && bv < (1 + margin) * val[keep]) best = keep;
            if (horse || best < 0) best = D >= 0 ? Fold(D) : -1;
            ni.Craft = best;
            var meta = VillageCrafts.Meta(best);
            ni.Item = horse || meta == null || meta.Icon == null ? v.VillageType.PrimaryProduction : meta.Icon;
            ni.IconId = IconId(ni.Item);
            // sklad wartosci (najwyzej 4 zajecia) i liczby wiosek
            double tot = 0; for (int c = 0; c < KsTab.NC; c++) tot += val[c];
            var order = new List<int>(); for (int c = 0; c < KsTab.NC; c++) if (val[c] > 0) order.Add(c);
            order.Sort((a, b) => val[b].CompareTo(val[a]));
            var sb = new StringBuilder();
            for (int i = 0; i < order.Count && i < 4; i++)
            {
                double pct = tot > 0 ? 100 * val[order[i]] / tot : 0;
                if (pct < 0.5 && i > 0) break;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(VillageCrafts.NameOf(KsTab.Crafts[order[i]])).Append(' ').Append(Math.Round(pct).ToString("0", Inv)).Append('%');
            }
            ni.Mix = sb.ToString();
            var sc = new StringBuilder();
            var corder = new List<int>(); for (int c = 0; c < KsTab.NC; c++) if (cnt[c] > 0) corder.Add(c);
            corder.Sort((a, b) => cnt[b].CompareTo(cnt[a]));
            foreach (int c in corder) { if (sc.Length > 0) sc.Append(", "); sc.Append(cnt[c]).Append(' ').Append(VillageCrafts.NameOf(KsTab.Crafts[c])); }
            ni.Counts = sc.ToString();
            return ni;
        }

        /// <summary>Maslo bydla liczy sie jak bydlo (jeden typ wsi gry).</summary>
        private static int Fold(int c) { return c == KsTab.CCattleB ? KsTab.CCattle : c; }

        private static string IconId(ItemObject it)
        {
            if (it == null) return "";
            string id = it.StringId ?? "";
            return id.Contains("camel") ? "camel" : ((id.Contains("horse") || id.Contains("mule")) ? "horse" : id);
        }

        private static Info InfoOf(Settlement st)
        {
            if (st == null || st.StringId == null) return null;
            Info i; return _info.TryGetValue(st.StringId, out i) ? i : null;
        }

        /// <summary>Sklad produkcji okregu dla opisu ("cattle 85%, grain 8%, fish 7%. Main good: cows") - z wycen czesci (ten sam rachunek co ikona).</summary>
        internal static string MixText(Village v, List<DistrictWorks.Quote> qs)
        {
            try
            {
                var parts = new List<VillageCrafts.PartCraft>();
                foreach (var q in qs) if (q.Pc != null) parts.Add(q.Pc);
                var s = Settings.Current;
                Info old = InfoOf(v.Settlement);
                var ni = Compute(v, parts, old, s != null ? s.DistrictIconMargin : 0.10f, (int)CampaignTime.Now.ToDays);
                var item = On && old != null && old.Item != null ? old.Item : v.VillageType.PrimaryProduction;
                return (ni.Mix.Length > 0 ? ni.Mix : "-") + ". Main good: " + (item != null && item.Name != null ? item.Name.ToString().ToLowerInvariant() : "?");
            }
            catch (Exception e) { Stumble("MixText", e); return "-"; }
        }

        // ============================================================ tabliczka
        public static void IconPostfix(object __instance)
        {
            try
            {
                if (__instance == null) return;
                lock (_vms) { _vms.Add(new WeakReference(__instance)); if (_vms.Count > 4000) _vms.RemoveAll(w => !w.IsAlive); }
                if (!On) return;
                Apply(__instance, false);
            }
            catch (Exception e) { Stumble("IconPostfix", e); }
        }

        /// <summary>Pozycja Production tabliczki = ikona okregu (bez zmiany - nic). Przy tworzeniu VM (postfiks) - sama wartosc; w sesji (rachunek co 28 dob) pozycja zdjeta
        /// i dodana od nowa w tym samym miejscu: widzet gry (SettlementNameplateEventVisualBrushWidget) ustala obrazek raz, przy pierwszym LateUpdate.</summary>
        private static bool Apply(object vm, bool live)
        {
            var st = _fSettlement.GetValue(vm) as Settlement;
            var info = InfoOf(st);
            if (st == null || !st.IsVillage || info == null || string.IsNullOrEmpty(info.IconId)) return false;
            var list = _pEvents.GetValue(vm) as IList;
            if (list == null) return false;
            foreach (var it in list)
            {
                if (it == null) continue;
                object et = _fEventType.GetValue(it);
                if (et == null || Convert.ToInt32(et) != 6) continue;          // SettlementEventType.Production = 6
                string cur = _pParam.GetValue(it, null) as string;
                if (cur == info.IconId) return false;
                if (!live || _tItem == null) { _pParam.SetValue(it, info.IconId, null); return true; }
                int at = list.IndexOf(it);
                object fresh = Activator.CreateInstance(_tItem, new object[] { info.IconId });
                if (at < 0 || fresh == null) return false;
                list.RemoveAt(at);
                list.Insert(at, fresh);
                return true;
            }
            return false;
        }

        private static int UpdateVms()
        {
            int n = 0;
            if (_fSettlement == null) return 0;
            lock (_vms)
            {
                _vms.RemoveAll(w => !w.IsAlive);
                foreach (var w in _vms)
                {
                    try { var vm = w.Target; if (vm != null && Apply(vm, true)) n++; }
                    catch (Exception e) { Stumble("UpdateVms", e); }
                }
            }
            return n;
        }

        // ============================================================ dymek osady i lista produkcji miasta
        public static void TipPostfix(PropertyBasedTooltipVM __0, object[] __1)
        {
            try
            {
                if (!On || __0 == null || __1 == null || __1.Length == 0) return;
                var st = __1[0] as Settlement;
                if (st == null || !st.IsVillage) return;
                var info = InfoOf(st);
                if (info == null || info.Item == null) return;
                var list = __0.TooltipPropertyList;
                if (list == null) return;
                string prim = null;
                try { prim = GameTexts.FindText("str_primary_production").ToString(); } catch { prim = null; }
                if (prim == null) return;
                for (int i = 0; i < list.Count; i++)
                {
                    var p = list[i];
                    if (p == null || p.DefinitionLabel != prim) continue;
                    string name = info.Item.Name != null ? info.Item.Name.ToString() : "?";
                    p.ValueLabel = name + (info.Mix.Length > 0 ? " (whole district: " + info.Mix + (info.Counts.Length > 0 ? "; villages: " + info.Counts : "") + ")" : "");
                    break;
                }
            }
            catch (Exception e) { Stumble("TipPostfix", e); }
        }

        public static void ProdListPostfix(Settlement settlement, List<TooltipProperty> __result)
        {
            try
            {
                if (!On || settlement == null || __result == null || !settlement.IsFortification) return;
                foreach (var v in settlement.BoundVillages)
                {
                    var info = InfoOf(v.Settlement);
                    if (info == null || info.Item == null || ReferenceEquals(info.Item, v.VillageType.PrimaryProduction)) continue;
                    string vn = v.Name != null ? v.Name.ToString() : null;
                    foreach (var p in __result)
                        if (p != null && p.DefinitionLabel == vn) { p.ValueLabel = (info.Item.Name != null ? info.Item.Name.ToString() : "?") + " (district)"; break; }
                }
            }
            catch (Exception e) { Stumble("ProdListPostfix", e); }
        }
    }
}
