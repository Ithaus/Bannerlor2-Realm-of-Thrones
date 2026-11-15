using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection.Map.MapBar;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// 537 E1 (projekt docs/projekty/KONIE-UPRZEZE.md rozdz. 5; Jeff 15.11 "HUD: konie np. 23/31 uzywane przez ludzi, 21/31 uprzezy, ile koni jucznych 4/5 zanim
    /// beda kary za herding"): trzy pozycje na pasku mapy - TYLKO WYSWIETLANIE (predkosc, waga i udzwig bez zmian do E2/E3).
    ///  - "Horses" (gorny rzad, PrimaryInfoItems): konie pod ludzmi / wszystkie konie wierzchowe (ladunek + zbrojownia DTE);
    ///  - "Harness" (dolny rzad, SecondaryInfoItems): uprzeze na koniach pod ludzmi / wszystkie konie (D10 a - jak u Jeffa);
    ///  - "Pack" (dolny rzad): juczne / limit Herding z par (ludzie - bydlo - wolne konie w stadzie); ostrzezenie (kolor), gdy juczne ponad limit.
    /// Liczby TYLKO z rekordu MarchPairs.Main (ten sam co linia doby "Pary marszowe (gracz)"), nigdy z NumberOfMounts gry. Podpowiedz z MarchPairs.Last (bez liczenia).
    /// Technika (wzor BK MapInfoPietyPatches, dekompilacja MapInfoVM 1.4.8): postfiks MapInfoVM.RefreshValues (konstruktor, zmiana jezyka) i
    /// MapInfoVM.UpdatePlayerInfo (MapBarVM.OnRefresh co 0.2 s, 0.1 s przy szybkim czasie; NavalMapInfoVM wola base) - cele przez DeclaredMethod na MapInfoVM.
    /// Pozycje trzymane per obiekt MapInfoVM (ConditionalWeakTable) - nowy pasek (wczytanie, nowy ekran mapy) dostaje swoje, nic nie jest dodawane drugi raz
    /// (lekcja BkPietyBar: porownanie referencji + zdjecie obcej pozycji o naszym ItemId). Co wywolanie tylko porownania; rekord liczony w MarchPairs.Main
    /// najwyzej raz na 1 s po zmianie klucza (albo co 10 s). Ikony: istniejace warstwy pedzla MapBar.Right.Icons przez SetOverriddenVisualId
    /// (speed, troops, food - wlasnego pedzla nie robimy, BK podmienia caly pedzel). Klucz MarchPairsHud (wylaczenie zdejmuje pozycje przy nastepnym odswiezeniu).
    /// Bledy liczone (Stumble), bez wylacznika.
    /// </summary>
    internal static class MarchHud
    {
        private const string Tag = "Pasek mapy - pary marszowe (537 E1): ";
        private const string IdHorses = "armoury_horses", IdHarness = "armoury_harness", IdPack = "armoury_pack";
        private const int MaxChars = 5;   // pole tekstu paska: MaxWidth 52 przy FontSize 20 (MapBar.xml) - dluzszy ulamek pokazuje sam licznik (pelny w podpowiedzi)

        private sealed class Items { public MapInfoItemVM Horses, Harness, Pack; }

        private static readonly ConditionalWeakTable<MapInfoVM, Items> _vms = new ConditionalWeakTable<MapInfoVM, Items>();
        private static Harmony _h;
        private static string _wired = "nie sprawdzane";
        private static bool _ok;
        private static int _stumbles, _shown, _added, _dupRemoved, _bars;
        private static string _lastText;
        private static bool _live;   // po OnSessionLaunched tej kampanii (Reset w konstruktorze zachowania gasi) - pasek budowany przy wczytaniu nie liczy podgladu DTE przed startem sesji

        internal static bool On { get { var s = Settings.Current; return s != null && s.MarchPairsHud; } }

        private static void Stumble(string where, Exception e)
        {
            int n = ++_stumbles;
            try { if (n <= 3 || n % 50 == 0) Log.Error("MarchHud." + where + " (potkniecie " + n + ")", e); } catch { }
        }

        /// <summary>OnSessionLaunched: latki raz na proces, linia startu. Nie rzuca.</summary>
        internal static void SessionStart()
        {
            if (_h == null)
            {
                _h = new Harmony("com.jeff.armoury.marchhud537");
                try
                {
                    var mr = AccessTools.DeclaredMethod(typeof(MapInfoVM), "RefreshValues");
                    var mu = AccessTools.DeclaredMethod(typeof(MapInfoVM), "UpdatePlayerInfo");
                    if (mr == null || mu == null) _wired = "BRAK (MapInfoVM.RefreshValues " + (mr != null) + ", UpdatePlayerInfo " + (mu != null) + ") - pasek bez pozycji";
                    else
                    {
                        _h.Patch(mr, postfix: new HarmonyMethod(typeof(MarchHud), nameof(RefreshPost)) { priority = Priority.Low });
                        _h.Patch(mu, postfix: new HarmonyMethod(typeof(MarchHud), nameof(UpdatePost)) { priority = Priority.Low });
                        _ok = true;
                        _wired = "TAK (postfiksy MapInfoVM.RefreshValues i UpdatePlayerInfo)";
                    }
                }
                catch (Exception e) { _wired = "wyjatek " + e.GetType().Name + " - pasek bez pozycji"; Stumble("SessionStart", e); }
            }
            Reset();
            _live = true;
            try
            {
                Log.Info(Tag + "latki " + _wired + " | klucz March Pairs Hud " + (On ? "TAK" : "NIE")
                         + " (Horses w gornym rzedzie, Harness i Pack w dolnym; liczby z rekordu MarchPairs - tego samego co linia doby 'Pary marszowe (gracz)'; tylko wyswietlanie)"
                         + (_stumbles > 0 ? " | potkniecia " + _stumbles : "") + ".");
            }
            catch { }
        }

        /// <summary>Nowa kampania / wczytanie: liczniki linii logu (pozycje paska sa per obiekt MapInfoVM - stary pasek ginie z ekranem).</summary>
        internal static void Reset() { _shown = 0; _lastText = null; _live = false; }

        /// <summary>Stan do linii doby.</summary>
        internal static string State()
        {
            return "pasek mapy: latki " + (_ok ? "TAK" : "NIE") + ", klucz " + (On ? "TAK" : "NIE") + ", paskow " + _bars + ", dodan pozycji " + _added
                   + ", zdjetych obcych duplikatow " + _dupRemoved + ", zmian tekstu " + _shown + (_stumbles > 0 ? ", potkniecia " + _stumbles : "")
                   + (_lastText != null ? ", ostatnio \"" + _lastText + "\"" : "");
        }

        // ---------------------------------------------------------------- teksty (te same w HUD i w linii doby)
        private static string Frac(int a, int b)
        {
            string s = a + "/" + b;
            return s.Length <= MaxChars ? s : a.ToString();
        }

        internal static string HorsesText(MarchPairs.Rec r) { return r == null ? "-" : Frac(r.Ridden, r.AllHorses); }
        internal static string HarnessText(MarchPairs.Rec r) { return r == null ? "-" : Frac(r.Ridden, r.AllHorses); }   // D10 a: mianownik = wszystkie konie
        internal static string PackText(MarchPairs.Rec r) { return r == null ? "-" : Frac(r.CPack, r.PackLimit); }

        // ---------------------------------------------------------------- latki
        public static void RefreshPost(MapInfoVM __instance) { Sync(__instance, true); }

        public static void UpdatePost(MapInfoVM __instance, bool updateForced) { Sync(__instance, updateForced); }

        private static void Sync(MapInfoVM vm, bool forced)
        {
            try
            {
                if (vm == null || vm.PrimaryInfoItems == null || vm.SecondaryInfoItems == null) return;
                Items it;
                if (!On)
                {
                    if (_vms.TryGetValue(vm, out it)) { Drop(vm.PrimaryInfoItems, it.Horses); Drop(vm.SecondaryInfoItems, it.Harness); Drop(vm.SecondaryInfoItems, it.Pack); }
                    // 537 E2: predkosc czyta rekord MarchPairs z pamieci - odswiezany tu (watek glowny, najwyzej raz na 1 s po zmianie) takze bez pozycji paska
                    if (_live && MarchPairs.SpeedOn) MarchPairs.Main(false);
                    return;
                }
                if (!_live) return;   // przed startem sesji kampanii: pozycje dojda przy pierwszym odswiezeniu po starcie (0.2 s)
                if (!_vms.TryGetValue(vm, out it))
                {
                    it = new Items
                    {
                        Horses = new MapInfoItemVM(IdHorses, Tooltip),
                        Harness = new MapInfoItemVM(IdHarness, Tooltip),
                        Pack = new MapInfoItemVM(IdPack, Tooltip)
                    };
                    it.Horses.SetOverriddenVisualId("speed");
                    it.Harness.SetOverriddenVisualId("troops");
                    it.Pack.SetOverriddenVisualId("food");
                    _vms.Add(vm, it);
                    _bars++;
                }
                Ensure(vm.PrimaryInfoItems, it.Horses);
                Ensure(vm.SecondaryInfoItems, it.Harness);
                Ensure(vm.SecondaryInfoItems, it.Pack);
                var r = MarchPairs.Main(false);
                string h = HorsesText(r), u = HarnessText(r), p = PackText(r);
                Put(it.Horses, h, r != null ? r.Ridden : 0, false);
                Put(it.Harness, u, r != null ? r.Ridden : 0, false);
                Put(it.Pack, p, r != null ? r.CPack : 0, r != null && r.CPack > r.PackLimit);
                string text = "Horses " + h + ", Harness " + u + ", Pack " + p;
                if (text != _lastText)
                {
                    _lastText = text;
                    _shown++;
                    if (r != null && (_shown <= 20 || _shown % 50 == 0))
                        try
                        {
                            Log.Info(Tag + "pasek (" + _shown + ") -> " + text + " | jedzie: jazda " + r.CavPairs + ", pula " + r.Ride + " z " + r.PoolMen
                                     + ", konie " + r.AllHorses + " (ladunek " + r.CMounts + ", zbrojownia " + r.AHorses + "), uprzeze " + r.AllHarness
                                     + ", juczne " + r.CPack + " / limit " + r.PackLimit + (r.AtSea ? ", na morzu" : "") + " (rekord liczony " + r.Ms.ToString("0.0") + " ms).");
                        }
                        catch { }
                }
            }
            catch (Exception e) { Stumble("Sync", e); }
        }

        private static void Put(MapInfoItemVM x, string val, int n, bool warn)
        {
            if (x.Value != val) x.Value = val;
            if (x.IntValue != n) x.IntValue = n;
            if (x.HasWarning != warn) x.HasWarning = warn;
        }

        /// <summary>Nasza pozycja dokladnie raz na liscie: dodana, gdy jej nie ma; obca pozycja z naszym ItemId zdjeta.</summary>
        private static void Ensure(MBBindingList<MapInfoItemVM> list, MapInfoItemVM mine)
        {
            bool have = false;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var x = list[i];
                if (ReferenceEquals(x, mine)) { if (have) { list.RemoveAt(i); _dupRemoved++; } else have = true; continue; }
                if (x != null && string.Equals(x.ItemId, mine.ItemId, StringComparison.Ordinal)) { list.RemoveAt(i); _dupRemoved++; }
            }
            if (!have) { list.Add(mine); _added++; }
        }

        private static void Drop(MBBindingList<MapInfoItemVM> list, MapInfoItemVM mine)
        {
            for (int i = list.Count - 1; i >= 0; i--) if (ReferenceEquals(list[i], mine)) list.RemoveAt(i);
        }

        // ---------------------------------------------------------------- podpowiedz (po angielsku; z pamieci, bez liczenia)
        private static List<TooltipProperty> Tooltip()
        {
            var l = new List<TooltipProperty>();
            try
            {
                l.Add(new TooltipProperty("Horses and harness on the march", "", 0, false, TooltipProperty.TooltipPropertyFlags.Title));
                var r = MarchPairs.Last;
                if (r == null || !r.Ok) { l.Add(new TooltipProperty("Not counted yet", "", 0, false, TooltipProperty.TooltipPropertyFlags.None)); return l; }
                int mounted = r.AtSea ? 0 : r.Ridden + r.HeroMounted;
                Row(l, "Men mounted", mounted + "/" + r.Men);
                if (r.AtSea) Row(l, "At sea", "nobody rides on board");
                else
                {
                    Row(l, "On foot", (r.Men - mounted) + " (" + r.WalkNoHorse + " no horse, " + r.WalkNoHarness + " no harness, " + r.WalkSkill + " Riding too low)");
                    Row(l, "Cavalry", r.CavPairs + "/" + (r.CavMen + r.CavWounded) + (r.CavWounded > 0 ? " (" + r.CavWounded + " wounded go on the spare horses)" : ""));
                    Row(l, "Footmen on spare horses", r.Ride + "/" + r.PoolMen);
                    if (r.HeroMounted > 0) Row(l, "Heroes on their own horses", r.HeroMounted.ToString());
                }
                int notRidden = Math.Max(0, r.AllHorses - r.Ridden);
                Row(l, "Horses", r.Ridden + " ridden, " + notRidden + " not ridden (" + r.FreeHerd + " free in the herd)");
                int unusedH = Math.Max(0, r.AllHarness - r.Ridden);
                Row(l, "Harness", r.Ridden + " worn, " + unusedH + " unused"
                    + (r.UnusedPairs > 0 && r.UnusedReqMin >= 0 ? " (" + r.UnusedPairs + " horse and harness pairs need Riding " + r.UnusedReqMin + " or more)" : ""));
                Row(l, "Pack animals", r.CPack + "/" + r.PackLimit + " before the herding penalty (" + r.Men + " men - " + r.CLive + " livestock - " + r.FreeHerd + " free horses)");
                var s = Settings.Current;
                if (s != null) Row(l, "Baggage pace", (int)Math.Ceiling(r.Men * Math.Max(0f, s.MarchPackAllowance)) + " pack animals allowed while all men ride");
                if (r.SkillLowEn.Count > 0) Row(l, "Riding too low", string.Join(", ", r.SkillLowEn));
                Row(l, " ", "A man rides only a horse with a harness that fits it and his Riding allows. Counted from the baggage and the troops' armoury; "
                            + (MarchPairs.SpeedOn ? "party speed uses these numbers, cargo weight does not yet." : "party speed and weight do not use these numbers (March Pairs Speed is off)."));   // 537 E2
            }
            catch (Exception e) { Stumble("Tooltip", e); }
            return l;
        }

        private static void Row(List<TooltipProperty> l, string k, string v) { l.Add(new TooltipProperty(k, v, 0, false, TooltipProperty.TooltipPropertyFlags.None)); }
    }
}
