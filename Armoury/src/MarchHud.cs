using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection.Map.MapBar;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI;
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
    /// 541 (Jeff 15.11: pasek "nachodzi na przycisk done", "dodaj ikony miecza i zbroi", "132/ na 132", "ikona zboza srednio pasuje"):
    ///  - gorny rzad wraca do pozycji gry (Horses zdjete) - szerokosc paska jak przed 537 (CoverChildren: szerszy z dwoch rzedow, gorny z 8 pozycjami gry);
    ///  - dolny rzad: Horses, Pack, Weapons, Armour (Harness polaczone z Horses - liczby byly te same; uprzeze w podpowiedzi i w kolorze ostrzezenia);
    ///  - Weapons / Armour = zapas musztry (Drill.HudKit: od gracza + nadwyzka ludzi / komplet = 1 na 3 ludzi), ostrzezenie ponizej kompletu;
    ///  - pelne ulamki: pole tekstu dolnego rzedu MaxWidth 70 (MarchHudUi, UIExtenderEx) + bezpiecznik szerokosci: szacunek obu rzedow z czcionki Galahad;
    ///    gdy dol bylby szerszy niz gora, pozycje PELNE (bez ostrzezenia) pokazuja sam licznik - pozycje z brakiem zawsze pelny ulamek;
    ///  - ikony: wlasne warstwy pedzla MapBar.Right.Icons (dopisane do pedzla, ktory wygral - BK zostaje) ze sprajtami gry (kon, juczne, miecz, zbroja).
    /// </summary>
    internal static class MarchHud
    {
        private const string Tag = "Pasek mapy - pary marszowe (537 E1 / 541): ";
        private const string IdHorses = "armoury_horses", IdHarness = "armoury_harness", IdPack = "armoury_pack", IdWeapons = "armoury_weapons", IdArmour = "armoury_armour";
        internal const int BottomMaxWidth = 70;   // 541: MarchHudUi (UIExtenderEx) - pole tekstu dolnego rzedu
        private const float TopMaxWidth = 52f, GameMaxWidth = 52f, MinText = 20f, ItemPad = 50f, RowPad = 75f, FontScale = 20f / 32f, Pad = 0.5f;   // MapBar.xml, Galahad.fnt, Text.cs
        internal static int BottomApplied;        // 541: ile razy UIExtenderEx nalozyl MaxWidth na prefab MapBar (getter MarchHudBottomTextWidth.Attributes)

        private sealed class Items { public MapInfoItemVM Horses, Pack, Weapons, Armour; }

        private static readonly ConditionalWeakTable<MapInfoVM, Items> _vms = new ConditionalWeakTable<MapInfoVM, Items>();
        private static Harmony _h;
        private static string _wired = "nie sprawdzane";
        private static bool _ok;
        private static int _stumbles, _shown, _added, _dupRemoved, _bars, _shortened, _iconStumbles;
        private static string _lastText;
        private static bool _live;   // po OnSessionLaunched tej kampanii (Reset w konstruktorze zachowania gasi) - pasek budowany przy wczytaniu nie liczy podgladu DTE przed startem sesji

        internal static bool On { get { var s = Settings.Current; return s != null && s.MarchPairsHud; } }

        private static void Stumble(string where, Exception e)
        {
            int n = ++_stumbles;
            try { if (n <= 3 || n % 50 == 0) Log.Error("MarchHud." + where + " (potkniecie " + n + ")", e); } catch { }
        }

        /// <summary>OnSessionLaunched: latki raz na proces, ikony, linia startu. Nie rzuca.</summary>
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
            try { EnsureIcons(); } catch (Exception e) { Stumble("SessionStart.Icons", e); }
            _live = true;
            try
            {
                Log.Info(Tag + "latki " + _wired + " | klucz March Pairs Hud " + (On ? "TAK" : "NIE")
                         + " | 541: gorny rzad bez naszych pozycji (szerokosc jak przed 537); dolny: Horses (z uprzezami), Pack, Weapons, Armour (zapas musztry "
                         + (Drill.StockOn ? "czynny" : "NIECZYNNY - pozycje ukryte") + ") | pole tekstu dolnego rzedu: " + MarchHudUi.State + ", nalozone " + BottomApplied + "x"
                         + " | ikony: " + IconState()
                         + (_stumbles > 0 ? " | potkniecia " + _stumbles : "") + ".");
            }
            catch { }
        }

        /// <summary>Nowa kampania / wczytanie: liczniki linii logu (pozycje paska sa per obiekt MapInfoVM - stary pasek ginie z ekranem).</summary>
        internal static void Reset() { _shown = 0; _lastText = null; _live = false; _shortened = 0; }

        /// <summary>Stan do linii doby.</summary>
        internal static string State()
        {
            return "pasek mapy: latki " + (_ok ? "TAK" : "NIE") + ", klucz " + (On ? "TAK" : "NIE") + ", paskow " + _bars + ", dodan pozycji " + _added
                   + ", zdjetych obcych duplikatow " + _dupRemoved + ", zmian tekstu " + _shown + ", w tym ze skrotem do licznika (bezpiecznik szerokosci) " + _shortened
                   + ", MaxWidth dolnego rzedu " + (BottomApplied > 0 ? BottomMaxWidth + " (nalozone " + BottomApplied + "x)" : "52 (wstawka nienalozona)")
                   + ", ikony " + IconState() + ", przeliczen zapasu musztry " + Drill.HudKitCounts
                   + (_stumbles > 0 ? ", potkniecia " + _stumbles : "")
                   + (_lastText != null ? ", ostatnio \"" + _lastText + "\"" : "");
        }

        // ---------------------------------------------------------------- teksty (te same w HUD i w linii doby)
        private static string Frac(int a, int b) { return a + "/" + b; }

        internal static string HorsesText(MarchPairs.Rec r) { return r == null ? "-" : Frac(r.Ridden, r.AllHorses); }
        internal static string HarnessText(MarchPairs.Rec r) { return r == null ? "-" : Frac(r.Ridden, r.AllHorses); }   // D10 a: mianownik = wszystkie konie (tylko linia doby - na pasku polaczone z Horses, 541)
        internal static string PackText(MarchPairs.Rec r) { return r == null ? "-" : Frac(r.CPack, r.PackLimit); }

        // ---------------------------------------------------------------- 541: ikony (warstwy pedzla MapBar.Right.Icons)
        // id pozycji, sprajt gry, zapasowy sprajt gry, warstwa gry, gdy zaden sprajt nie istnieje
        private static readonly string[][] Icons =
        {
            new[] { IdHorses,  "General\\EquipmentIcons\\equipment_type_mount",      "SPGeneral\\MountFlagIcons\\speed_mount", "speed" },
            new[] { IdPack,    "SPGeneral\\MountFlagIcons\\weight_carrying_mount",   "General\\EquipmentIcons\\equipment_type_mount", "food" },
            new[] { IdWeapons, "General\\EquipmentIcons\\equipment_type_one_handed", "SPGeneral\\WeaponFlagIcons\\one_handed", "troops" },
            new[] { IdArmour,  "General\\EquipmentIcons\\equipment_type_body_armor",  "General\\EquipmentIcons\\equipment_type_head_armor", "hit_points" },
        };
        private static Brush _iconBrush;                  // pedzel, do ktorego dopisano warstwy (inny obiekt = BrushFactory przeladowana - dopisac od nowa)
        private static readonly bool[] _iconOk = new bool[4];
        private static readonly string[] _iconUsed = new string[4];

        private static string IconState()
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < Icons.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(Icons[i][0].Substring(8)).Append(' ').Append(_iconOk[i] ? "sprajt " + (_iconUsed[i] ?? "?") : "zastepcza warstwa gry " + Icons[i][3]);
            }
            if (_iconStumbles > 0) sb.Append(", potkniecia ikon ").Append(_iconStumbles);
            return sb.ToString();
        }

        /// <summary>Dopisuje nasze warstwy do pedzla MapBar.Right.Icons (tego, ktory wygral - piety BK zostaje; wlasnego pedzla nie robimy). Tanio: porownanie
        /// referencji pedzla. Kazda ikona we wlasnym try (licznik potkniec, bez wylacznika) - brak sprajtu = zastepcza warstwa gry (SetOverriddenVisualId).</summary>
        private static void EnsureIcons()
        {
            var bf = UIResourceManager.BrushFactory;
            var b = bf != null ? bf.GetBrush("MapBar.Right.Icons") : null;
            if (b == null) { _iconBrush = null; return; }
            if (ReferenceEquals(b, _iconBrush)) return;
            _iconBrush = b;
            var data = UIResourceManager.SpriteData;
            BrushLayer tpl = b.GetLayer("food");
            if (tpl == null) foreach (var l in b.Layers) if (l != null && l.Sprite != null) { tpl = l; break; }
            for (int i = 0; i < Icons.Length; i++)
            {
                try
                {
                    var have = b.GetLayer(Icons[i][0]);
                    if (have != null && have.Sprite != null) { _iconOk[i] = true; _iconUsed[i] = have.Sprite.Name; continue; }
                    _iconOk[i] = false;
                    if (data == null || tpl == null || have != null) continue;
                    var spr = data.GetSprite(Icons[i][1]);
                    if (spr == null) spr = data.GetSprite(Icons[i][2]);
                    if (spr == null) continue;
                    var nl = new BrushLayer();
                    nl.FillFrom(tpl);
                    nl.Name = Icons[i][0];
                    nl.Sprite = spr;
                    nl.ImageFitType = ImageFit.ImageFitTypes.Contain;   // sprajty ekwipunku 36x35 w polu 40x40 - bez rozciagania
                    b.AddLayer(nl);
                    _iconOk[i] = true; _iconUsed[i] = spr.Name;
                }
                catch (Exception e)
                {
                    _iconOk[i] = false;
                    int n = ++_iconStumbles;
                    try { if (n <= 3 || n % 50 == 0) Log.Error("MarchHud.EnsureIcons " + Icons[i][0] + " (potkniecie " + n + ")", e); } catch { }
                }
            }
        }

        private static void Visual(MapInfoItemVM x, int i)
        {
            string want = _iconOk[i] ? x.ItemId : Icons[i][3];
            if (x.VisualId != want) x.SetOverriddenVisualId(want);
        }

        // ---------------------------------------------------------------- 541: szacunek szerokosci (Text.GetPreferredSize: (XAdvance + 0.5) x FontSize / Font.Size)
        // XAdvance Galahad.fnt (size 32) dla znakow 32..126; inne znaki - 16
        private static readonly byte[] Adv =
        {
            8, 6, 7, 18, 14, 17, 23, 5, 9, 9, 12, 13, 5, 10, 5, 10, 16, 9, 14, 14, 16, 14, 15, 13, 16, 15, 5, 5, 13, 13, 13, 12,
            21, 21, 16, 22, 22, 15, 15, 23, 21, 7, 8, 17, 15, 27, 22, 24, 15, 24, 16, 15, 18, 21, 19, 27, 18, 16, 18, 9, 10, 9, 13, 16,
            5, 13, 15, 14, 16, 14, 10, 14, 14, 6, 7, 13, 6, 22, 15, 15, 16, 16, 9, 10, 13, 15, 13, 22, 13, 13, 14, 9, 5, 9, 13
        };

        private static float TextPx(string t)
        {
            if (string.IsNullOrEmpty(t)) return 0f;
            float w = 0f;
            for (int i = 0; i < t.Length; i++) { int c = t[i]; w += (c >= 32 && c <= 126 ? Adv[c - 32] : 16) + Pad; }
            return w * FontScale;
        }

        private static float ItemPx(string t, float maxW) { return ItemPad + Math.Max(MinText, Math.Min(maxW, TextPx(t))); }

        /// <summary>Szerokosc rzedu bez pozycji Armoury (prefiks "armoury_").</summary>
        private static float RowPx(MBBindingList<MapInfoItemVM> list, float maxW)
        {
            float w = RowPad;
            for (int i = 0; i < list.Count; i++)
            {
                var x = list[i];
                if (x == null || IsOurs(x)) continue;
                w += ItemPx(x.Value, maxW);
            }
            return w;
        }

        private static bool IsOurs(MapInfoItemVM x) { return x.ItemId != null && x.ItemId.StartsWith("armoury_", StringComparison.Ordinal); }

        // ---------------------------------------------------------------- latki
        public static void RefreshPost(MapInfoVM __instance) { Sync(__instance, true); }

        public static void UpdatePost(MapInfoVM __instance, bool updateForced) { Sync(__instance, updateForced); }

        private static void Sync(MapInfoVM vm, bool forced)
        {
            try
            {
                if (vm == null || vm.PrimaryInfoItems == null || vm.SecondaryInfoItems == null) return;
                if (_live) FootStable.Pump();   // 537 E6: stajnia piechoty - przenoszenie tylko tu (stan mapy, najwyzej raz na 1 s, tylko przy powodzie)
                if (_live) StashWeight.Refresh();   // 537 E4: waga zbrojowni (lista na ludziach z pamieci, najwyzej raz na 1 s) - takze bez pozycji paska
                Items it;
                if (!On)
                {
                    if (_vms.TryGetValue(vm, out it)) { Drop(vm.SecondaryInfoItems, it.Horses); Drop(vm.SecondaryInfoItems, it.Pack); Drop(vm.SecondaryInfoItems, it.Weapons); Drop(vm.SecondaryInfoItems, it.Armour); }
                    // 537 E2: predkosc czyta rekord MarchPairs z pamieci - odswiezany tu (watek glowny, najwyzej raz na 1 s po zmianie) takze bez pozycji paska
                    if (_live && MarchPairs.Needed) MarchPairs.Main(false);   // 537 E3: takze waga i udzwig (RiddenHarnessWeightless)
                    return;
                }
                if (!_live) return;   // przed startem sesji kampanii: pozycje dojda przy pierwszym odswiezeniu po starcie (0.2 s)
                try { EnsureIcons(); } catch (Exception e) { Stumble("Sync.Icons", e); }
                if (!_vms.TryGetValue(vm, out it))
                {
                    it = new Items
                    {
                        Horses = new MapInfoItemVM(IdHorses, Tooltip),
                        Pack = new MapInfoItemVM(IdPack, Tooltip),
                        Weapons = new MapInfoItemVM(IdWeapons, WeaponsTooltip),
                        Armour = new MapInfoItemVM(IdArmour, ArmourTooltip)
                    };
                    _vms.Add(vm, it);
                    _bars++;
                }
                Visual(it.Horses, 0); Visual(it.Pack, 1); Visual(it.Weapons, 2); Visual(it.Armour, 3);
                // 541: gorny rzad - tylko pozycje gry (szerokosc paska sprzed 537); dawna pozycja Harness zdjeta z dolnego
                PurgeOurs(vm.PrimaryInfoItems);
                PurgeId(vm.SecondaryInfoItems, IdHarness);
                Ensure(vm.SecondaryInfoItems, it.Horses);
                Ensure(vm.SecondaryInfoItems, it.Pack);

                var r = MarchPairs.Main(false);
                var k = Drill.HudKit();
                bool kit = k != null && k.Ok;
                if (kit) { Ensure(vm.SecondaryInfoItems, it.Weapons); Ensure(vm.SecondaryInfoItems, it.Armour); }
                else { Drop(vm.SecondaryInfoItems, it.Weapons); Drop(vm.SecondaryInfoItems, it.Armour); }

                // pelne teksty, sam licznik, ostrzezenie
                var full = new string[4]; var num = new string[4]; var warn = new bool[4]; var iv = new int[4];
                full[0] = HorsesText(r); num[0] = r != null ? r.Ridden.ToString() : "-"; iv[0] = r != null ? r.Ridden : 0;
                warn[0] = r != null && !r.AtSea && (r.AllHarness < r.AllHorses || r.UnusedPairs > 0);   // konie bez uprzezy albo pary za trudne dla Riding
                full[1] = PackText(r); num[1] = r != null ? r.CPack.ToString() : "-"; iv[1] = r != null ? r.CPack : 0;
                warn[1] = r != null && r.CPack > r.PackLimit;
                bool men = kit && k.Men > 0;
                full[2] = men ? Frac(k.HaveW, k.Full) : "-"; num[2] = men ? k.HaveW.ToString() : "-"; iv[2] = men ? k.HaveW : 0; warn[2] = men && k.HaveW < k.Full;
                full[3] = men ? Frac(k.HaveA, k.Full) : "-"; num[3] = men ? k.HaveA.ToString() : "-"; iv[3] = men ? k.HaveA : 0; warn[3] = men && k.HaveA < k.Full;

                // bezpiecznik szerokosci: dolny rzad nie szerszy niz gorny (gorny = szerokosc sprzed 537)
                float maxB = BottomApplied > 0 ? BottomMaxWidth : GameMaxWidth;
                int n = kit ? 4 : 2;
                var pick = new string[4];
                for (int i = 0; i < n; i++) pick[i] = TextPx(full[i]) <= maxB ? full[i] : num[i];   // nie miesci sie w polu - zawinalby sie do drugiej linii
                float top = RowPx(vm.PrimaryInfoItems, TopMaxWidth);
                float bottom = RowPx(vm.SecondaryInfoItems, maxB);
                for (int i = 0; i < n; i++) bottom += ItemPx(pick[i], maxB);
                int cut = 0;
                for (int i = n - 1; i >= 0 && bottom > top + 0.5f; i--)
                {
                    if (warn[i] || pick[i] == num[i]) continue;   // brak (ostrzezenie) zawsze pelnym ulamkiem
                    bottom += ItemPx(num[i], maxB) - ItemPx(pick[i], maxB);
                    pick[i] = num[i]; cut++;
                }

                Put(it.Horses, pick[0], iv[0], warn[0]);
                Put(it.Pack, pick[1], iv[1], warn[1]);
                if (kit) { Put(it.Weapons, pick[2], iv[2], warn[2]); Put(it.Armour, pick[3], iv[3], warn[3]); }
                string text = "Horses " + pick[0] + ", Pack " + pick[1] + (kit ? ", Weapons " + pick[2] + ", Armour " + pick[3] : "");
                if (text != _lastText)
                {
                    _lastText = text;
                    _shown++;
                    if (cut > 0) _shortened++;
                    if (r != null && (_shown <= 20 || _shown % 50 == 0))
                        try
                        {
                            Log.Info(Tag + "pasek (" + _shown + ") -> " + text + " | pelne: Horses " + full[0] + ", Pack " + full[1] + (kit ? ", Weapons " + full[2] + ", Armour " + full[3] : ", zapas musztry nieczynny")
                                     + " | szerokosc (szacunek): gora " + top.ToString("0") + " px, dol " + bottom.ToString("0") + " px, MaxWidth dolu " + maxB + ", skrocone do licznika " + cut
                                     + " | jedzie: jazda " + r.CavPairs + ", pula " + r.Ride + " z " + r.PoolMen
                                     + ", konie " + r.AllHorses + " (ladunek " + r.CMounts + ", zbrojownia " + r.AHorses + "), uprzeze " + r.AllHarness
                                     + ", juczne " + r.CPack + " / limit " + r.PackLimit + (r.AtSea ? ", na morzu" : "")
                                     + (men ? " | musztra: bron " + k.HaveW + " (od Ciebie " + k.GivenW + ", nadwyzka ludzi " + k.SurW + "), zbroje " + k.HaveA + " (" + k.GivenA + " + " + k.SurA + "), komplet " + k.Full : "")
                                     + " (rekord liczony " + r.Ms.ToString("0.0") + " ms).");
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

        /// <summary>541: gorny rzad bez pozycji Armoury (szerokosc paska sprzed 537).</summary>
        private static void PurgeOurs(MBBindingList<MapInfoItemVM> list)
        {
            for (int i = list.Count - 1; i >= 0; i--) { var x = list[i]; if (x != null && IsOurs(x)) { list.RemoveAt(i); _dupRemoved++; } }
        }

        private static void PurgeId(MBBindingList<MapInfoItemVM> list, string id)
        {
            for (int i = list.Count - 1; i >= 0; i--) { var x = list[i]; if (x != null && string.Equals(x.ItemId, id, StringComparison.Ordinal)) { list.RemoveAt(i); _dupRemoved++; } }
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
                if (r.SMounts + r.SHarness + r.SOther > 0) Row(l, "Footmen's mounts", r.SMounts + " horses, " + r.SHarness + " harness kept apart from the baggage (town or camp menu)");   // 537 E6
                int notRidden = Math.Max(0, r.AllHorses - r.Ridden);
                Row(l, "Horses", r.Ridden + " ridden, " + notRidden + " not ridden (" + r.FreeHerd + " free in the herd)");
                int unusedH = Math.Max(0, r.AllHarness - r.Ridden);
                Row(l, "Harness", r.Ridden + " worn, " + unusedH + " unused"
                    + (r.UnusedPairs > 0 && r.UnusedReqMin >= 0 ? " (" + r.UnusedPairs + " horse and harness pairs need Riding " + r.UnusedReqMin + " or more)" : ""));
                Row(l, "Pack animals", r.CPack + "/" + r.PackLimit + " before the herding penalty (" + r.Men + " men - " + r.CLive + " livestock - " + r.FreeHerd + " free horses)");
                var s = Settings.Current;
                if (s != null) Row(l, "Baggage pace", (int)Math.Ceiling(r.Men * Math.Max(0f, s.MarchPackAllowance)) + " pack animals allowed while all men ride");
                if (r.SkillLowEn.Count > 0) Row(l, "Riding too low", string.Join(", ", r.SkillLowEn));
                Row(l, " ", "A man rides only a horse with a harness that fits it and his Riding allows (footmen need no Riding for a pair that asks up to " + (s != null ? s.FootmenRidingWaiver : 0) + "). Counted from the baggage and the troops' armoury; "
                            + (MarchPairs.SpeedOn ? "party speed uses these numbers" : "party speed does not use these numbers (March Pairs Speed is off)")
                            + (MarchPairs.WeightOn ? "; harness on ridden horses does not weigh on the baggage." : "; cargo weight does not use them (Ridden Harness Weightless is off)."));   // 537 E2, E3
            }
            catch (Exception e) { Stumble("Tooltip", e); }
            return l;
        }

        private static void Row(List<TooltipProperty> l, string k, string v) { l.Add(new TooltipProperty(k, v, 0, false, TooltipProperty.TooltipPropertyFlags.None)); }

        // ---------------------------------------------------------------- 541: podpowiedz zapasu musztry (po angielsku; z pamieci Drill.HudKit)
        private static List<TooltipProperty> WeaponsTooltip() { return KitTooltip(true); }
        private static List<TooltipProperty> ArmourTooltip() { return KitTooltip(false); }

        private static List<TooltipProperty> KitTooltip(bool w)
        {
            var l = new List<TooltipProperty>();
            try
            {
                string what = w ? "weapons" : "pieces of armour";
                l.Add(new TooltipProperty(w ? "Drill kit - weapons" : "Drill kit - armour", "", 0, false, TooltipProperty.TooltipPropertyFlags.Title));
                var k = Drill.HudKit();
                if (k == null || !k.Ok) { l.Add(new TooltipProperty("Drill stock is off", "", 0, false, TooltipProperty.TooltipPropertyFlags.None)); return l; }
                if (k.Men <= 0) { Row(l, "No soldiers", "nobody drills, the stock takes nothing"); return l; }
                int have = w ? k.HaveW : k.HaveA, given = w ? k.GivenW : k.GivenA, sur = w ? k.SurW : k.SurA, room = w ? k.RoomW : k.RoomA;
                int inUse = Math.Min(have, k.Full);
                bool perk = w ? k.PerkW : k.PerkA;
                Row(l, "Have / full set", have + "/" + k.Full + " (one per " + Drill.OnePer + " men; " + k.Men + " men)");
                Row(l, "In use for drill", inUse + "/" + k.Full + (have > k.Full ? " - " + (have - k.Full) + " spare to replace worn pieces" : have < k.Full ? " - " + (k.Full - have) + " short" : ""));
                Row(l, "From you", given + "  |  men's spare kit in the armoury: " + sur);
                Row(l, "They take from you", room + " more " + what + " (up to " + Drill.IntakeSetsN + " full sets)");
                float bonus = k.Bonus(w) * 100f, max = Drill.BonusPerSet * 100f * (perk ? Drill.BonusPerkMult : 1f);
                Row(l, "Drill bonus", "+" + bonus.ToString("0.#") + "% of +" + max.ToString("0.#") + "% troop XP from drill (grows with the share of the full set; "
                       + (w ? "Giving Hands" : "Paid in Promise") + (perk ? " raises it" : " would raise it") + " to +" + (Drill.BonusPerSet * 100f * Drill.BonusPerkMult).ToString("0.#") + "%)");
                float rate = k.WearPerFullDay(w);
                Row(l, "Wear", "a full day of drill (8 h) wears out about " + rate.ToString("0.00") + " " + what + " - the cheapest first, your gifts first, then the men's spare kit. No drill, no wear.");
                float acc = w ? k.AccW : k.AccA;
                if (rate > 0f) Row(l, "Next one worn out", "after about " + Math.Max(0f, (1f - acc) / rate).ToString("0.#") + " full days of drill");
                Row(l, "Worn out today", (w ? k.WornW : k.WornA).ToString());
                Row(l, " ", "Give " + what + " to your men in the inventory (Discard) or leave trophies after a battle - they keep them for drill. "
                            + "Their spare kit is sold to the merchants of the next town; new recruits take what they need from it. "
                            + "Worn pieces become scrap the smiths of the next town buy - the coin goes to the men's purse. "
                            + "Every recruit raises the full set, so the same stock covers less.");
            }
            catch (Exception e) { Stumble("KitTooltip", e); }
            return l;
        }
    }
}
