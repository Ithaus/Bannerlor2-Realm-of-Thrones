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
    ///    gdy dol bylby szerszy niz gora, pozycje PELNE (bez ostrzezenia) pokazuja sam licznik, a gdy to nie wystarczy (recenzja 541) - takze pozycje z brakiem
    ///    (kolor ostrzezenia zostaje, pelny ulamek w podpowiedzi); Horses ostrzega tylko, gdy ktos idzie pieszo bez uprzezy albo przez za niska Jazde;
    ///  - ikony: wlasne warstwy pedzla MapBar.Right.Icons (dopisane do pedzla, ktory wygral - BK zostaje) ze sprajtami gry (kon, juczne, miecz, zbroja).
    /// 548 (Jeff 15.11: "nie rozumiem tej tabelki, mam 131 ludzi czemu 146 czerwone ... 82/43 czyli co ... wez to madrze zrob aby UX/UI byl czytelny"):
    /// jedna zasada dla calego dolnego rzedu - "mam / potrzeba", zawsze pelny ulamek, zwykly kolor gdy pelne, ostrzezenie gdy brakuje (Pack: gdy ponad limit):
    ///  - Horses = ludzie na koniach (pary + bohaterowie na wlasnych) / ludzie w partii (r.Men, ta sama liczba co gorny rzad); ostrzezenie, gdy ktos idzie pieszo;
    ///  - Pack bez zmian (juczne / limit Herding), wlasna podpowiedz;
    ///  - Weapons i Armour polaczone w jedna pozycje Drill (ikona miecza): ludzie z bronia i zbroja do musztry / wszyscy ludzie - min(w uzyciu bron, w uzyciu
    ///    zbroje) / komplet (Drill.Full, od 548 1 na czlowieka - klucz Drill Men Per Piece); "w uzyciu" = min(stan, komplet), wiec licznik nie przekracza
    ///    mianownika i nie skacze z nadwyzka ludzi (187/43 w logu 15.11). Cztery pelne ulamki nie mieszcza sie w szerokosci sprzed 537 (ok. 717 px wobec 690),
    ///    trzy - tak (ok. 611 px); bezpiecznik szerokosci zostaje tylko na wypadek ulamkow czterocyfrowych (linia doby: "skrocone do licznika" ma byc 0).
    ///  - podpowiedzi: pierwszy wiersz mowi zwyklym angielskim, co znacza liczby, dalej szczegoly.
    /// </summary>
    internal static class MarchHud
    {
        private const string Tag = "Pasek mapy - pary marszowe (537 E1 / 541): ";
        private const string IdHorses = "armoury_horses", IdHarness = "armoury_harness", IdPack = "armoury_pack", IdWeapons = "armoury_weapons", IdArmour = "armoury_armour";
        internal const int BottomMaxWidth = 70;   // 541: MarchHudUi (UIExtenderEx) - pole tekstu dolnego rzedu
        private const float TopMaxWidth = 52f, GameMaxWidth = 52f, MinText = 20f, ItemPad = 50f, RowPad = 75f, FontScale = 20f / 32f, Pad = 0.5f;   // MapBar.xml, Galahad.fnt, Text.cs
        internal static int BottomApplied;        // 541: ile razy UIExtenderEx nalozyl MaxWidth na prefab MapBar (getter MarchHudBottomTextWidth.Attributes)

        private sealed class Items { public MapInfoItemVM Horses, Pack, Drill; }   // 548: Drill (ItemId armoury_weapons) zamiast Weapons + Armour

        private static readonly ConditionalWeakTable<MapInfoVM, Items> _vms = new ConditionalWeakTable<MapInfoVM, Items>();
        private static Harmony _h;
        private static string _wired = "nie sprawdzane";
        private const int NItems = 3;   // 548: Horses, Pack, Drill
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
                         + " | 548: gorny rzad bez naszych pozycji (szerokosc jak przed 537); dolny, zawsze 'mam / potrzeba': Horses (na koniach / ludzie), Pack (juczne / limit), Drill (562: z pelnym zestawem do roli / ludzie, 1 sztuka rodzaju na "
                         + Drill.OnePer + " - zapas musztry "
                         + (Drill.StockOn ? "czynny" : "NIECZYNNY - pozycje ukryte") + ") | pole tekstu dolnego rzedu: " + MarchHudUi.State + (BottomApplied > 0 ? ", nalozone " + BottomApplied + "x" : " (nakladane przy pierwszym wczytaniu paska mapy - po starcie sesji; liczbe pokazuje linia doby)")
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
                   + ", ikony " + IconState() + ", przeliczen zestawu musztry " + Drill.HudKitCounts + " (562: bez planow K1 i bez podgladu bitwy)"
                   + (_stumbles > 0 ? ", potkniecia " + _stumbles : "")
                   + (_lastText != null ? ", ostatnio \"" + _lastText + "\"" : "");
        }

        // ---------------------------------------------------------------- teksty (te same w HUD i w linii doby)
        private static string Frac(int a, int b) { return a + "/" + b; }

        /// <summary>548: ludzie na koniach (pary jazdy i puli + bohaterowie na wlasnych koniach); na morzu 0.</summary>
        internal static int Mounted(MarchPairs.Rec r) { return r == null || r.AtSea ? 0 : r.Ridden + r.HeroMounted; }
        /// <summary>548: ludzie na koniach / ludzie w partii (r.Men - ta sama liczba co gorny rzad gry); na morzu "-".</summary>
        internal static string HorsesText(MarchPairs.Rec r) { return r == null || r.AtSea ? "-" : Frac(Mounted(r), r.Men); }
        internal static string HarnessText(MarchPairs.Rec r) { return r == null ? "-" : Frac(r.Ridden, r.AllHorses); }   // D10 a: mianownik = wszystkie konie (tylko linia doby; 548: pasek pokazuje ludzi na koniach / ludzi)
        internal static string PackText(MarchPairs.Rec r) { return r == null ? "-" : Frac(r.CPack, r.PackLimit); }

        // ---------------------------------------------------------------- 541: ikony (warstwy pedzla MapBar.Right.Icons)
        // id pozycji, sprajt gry, zapasowy sprajt gry, warstwa gry, gdy zaden sprajt nie istnieje
        private static readonly string[][] Icons =
        {
            new[] { IdHorses,  "General\\EquipmentIcons\\equipment_type_mount",      "SPGeneral\\MountFlagIcons\\speed_mount", "speed" },
            new[] { IdPack,    "SPGeneral\\MountFlagIcons\\weight_carrying_mount",   "General\\EquipmentIcons\\equipment_type_mount", "food" },
            new[] { IdWeapons, "General\\EquipmentIcons\\equipment_type_one_handed", "SPGeneral\\WeaponFlagIcons\\one_handed", "troops" },   // 548: pozycja Drill (bron i zbroja razem)
        };
        private static Brush _iconBrush;                  // pedzel, do ktorego dopisano warstwy (inny obiekt = BrushFactory przeladowana - dopisac od nowa)
        private static readonly bool[] _iconOk = new bool[NItems];
        private static readonly string[] _iconUsed = new string[NItems];

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
                    if (_vms.TryGetValue(vm, out it)) { Drop(vm.SecondaryInfoItems, it.Horses); Drop(vm.SecondaryInfoItems, it.Pack); Drop(vm.SecondaryInfoItems, it.Drill); }
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
                        Pack = new MapInfoItemVM(IdPack, PackTooltip),
                        Drill = new MapInfoItemVM(IdWeapons, KitTooltip)
                    };
                    _vms.Add(vm, it);
                    _bars++;
                }
                Visual(it.Horses, 0); Visual(it.Pack, 1); Visual(it.Drill, 2);
                // 541: gorny rzad - tylko pozycje gry (szerokosc paska sprzed 537); dawne pozycje Harness (541) i Armour (548) zdjete z dolnego
                PurgeOurs(vm.PrimaryInfoItems);
                PurgeId(vm.SecondaryInfoItems, IdHarness);
                PurgeId(vm.SecondaryInfoItems, IdArmour);
                Ensure(vm.SecondaryInfoItems, it.Horses);
                Ensure(vm.SecondaryInfoItems, it.Pack);

                var r = MarchPairs.Main(false);
                var k = Drill.HudKit();
                bool kit = k != null && k.Ok;
                if (kit) Ensure(vm.SecondaryInfoItems, it.Drill);
                else Drop(vm.SecondaryInfoItems, it.Drill);

                // pelne teksty, sam licznik, ostrzezenie
                // 548: kazda pozycja "mam / potrzeba" - pelny ulamek; ostrzezenie tylko przy braku (Pack: ponad limit)
                var full = new string[NItems]; var num = new string[NItems]; var warn = new bool[NItems]; var iv = new int[NItems];
                int mounted = Mounted(r);
                full[0] = HorsesText(r); num[0] = r != null && !r.AtSea ? mounted.ToString() : "-"; iv[0] = mounted;
                warn[0] = r != null && !r.AtSea && mounted < r.Men;   // 548: ktos idzie pieszo (bez konia, bez pasujacej uprzezy albo za niska Jazda) - kto i dlaczego w podpowiedzi
                full[1] = PackText(r); num[1] = r != null ? r.CPack.ToString() : "-"; iv[1] = r != null ? r.CPack : 0;
                warn[1] = r != null && r.CPack > r.PackLimit;
                bool men = kit && k.KitN > 0;   // 558: zolnierze + towarzysze, ktorzy cwicza
                int ready = men ? k.Ready : 0;   // 562: ludzie z pelnym zestawem do swojej roli
                full[2] = men ? Frac(ready, k.KitN) : "-"; num[2] = men ? ready.ToString() : "-"; iv[2] = ready; warn[2] = men && ready < k.KitN;

                // bezpiecznik szerokosci: dolny rzad nie szerszy niz gorny (gorny = szerokosc sprzed 537)
                float maxB = BottomApplied > 0 ? BottomMaxWidth : GameMaxWidth;
                int n = kit ? 3 : 2;
                var pick = new string[NItems];
                for (int i = 0; i < n; i++) pick[i] = TextPx(full[i]) <= maxB ? full[i] : num[i];   // nie miesci sie w polu - zawinalby sie do drugiej linii
                float top = RowPx(vm.PrimaryInfoItems, TopMaxWidth);
                float bottom = RowPx(vm.SecondaryInfoItems, maxB);
                for (int i = 0; i < n; i++) bottom += ItemPx(pick[i], maxB);
                int cut = 0;
                // 548: przy trzech pozycjach rzad miesci sie z zapasem (ok. 611 px wobec gory 690+) - ponizsze tylko na wypadek ulamkow czterocyfrowych.
                // runda 0: najpierw pozycje pelne (bez ostrzezenia); runda 1 (recenzja 541): gdy nadal za szeroko - takze z ostrzezeniem (kolor zostaje,
                // pelny ulamek w podpowiedzi) - szerokosc paska jak przed 537 ma pierwszenstwo
                for (int round = 0; round < 2; round++)
                    for (int i = n - 1; i >= 0 && bottom > top + 0.5f; i--)
                    {
                        if (pick[i] == num[i] || (round == 0 && warn[i])) continue;
                        bottom += ItemPx(num[i], maxB) - ItemPx(pick[i], maxB);
                        pick[i] = num[i]; cut++;
                    }

                Put(it.Horses, pick[0], iv[0], warn[0]);
                Put(it.Pack, pick[1], iv[1], warn[1]);
                if (kit) Put(it.Drill, pick[2], iv[2], warn[2]);
                string text = "Horses " + pick[0] + ", Pack " + pick[1] + (kit ? ", Drill " + pick[2] : "");
                if (text != _lastText)
                {
                    _lastText = text;
                    _shown++;
                    if (cut > 0) _shortened++;
                    if (r != null && (_shown <= 20 || _shown % 50 == 0))
                        try
                        {
                            Log.Info(Tag + "pasek (" + _shown + ") -> " + text + " | pelne: Horses " + full[0] + " (na koniach / ludzie), Pack " + full[1] + (kit ? ", Drill " + full[2] + " (z pelnym zestawem do roli / ludzie)" : ", zapas musztry nieczynny")
                                     + " | szerokosc (szacunek): gora " + top.ToString("0") + " px, dol " + bottom.ToString("0") + " px, MaxWidth dolu " + maxB + ", skrocone do licznika " + cut
                                     + " | jedzie: jazda " + r.CavPairs + ", pula " + r.Ride + " z " + r.PoolMen + ", bohaterowie " + r.HeroMounted + ", pieszo " + Math.Max(0, r.Men - mounted)
                                     + ", konie " + r.AllHorses + " (ladunek " + r.CMounts + ", zbrojownia " + r.AHorses + "), uprzeze " + r.AllHarness
                                     + ", juczne " + r.CPack + " / limit " + r.PackLimit + (r.AtSea ? ", na morzu" : "")
                                     + (men ? " | musztra 562: z kompletem " + k.Ready + "/" + k.KitN + " (bron " + k.ReadyW + ", zbroje " + k.ReadyA + "), zapas " + k.Stock + " szt., rodzaje " + Drill.KitLog(k) + " (1 na " + Drill.OnePer + ")" : "")
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

        // ---------------------------------------------------------------- podpowiedzi (po angielsku; z pamieci, bez liczenia)
        // 548: pierwszy wiersz kazdej podpowiedzi mowi zwyklym angielskim, co znacza liczby na pasku; dalej szczegoly
        private static List<TooltipProperty> Tooltip()
        {
            var l = new List<TooltipProperty>();
            try
            {
                l.Add(new TooltipProperty("Horses on the march", "", 0, false, TooltipProperty.TooltipPropertyFlags.Title));
                var r = MarchPairs.Last;
                if (r == null || !r.Ok) { l.Add(new TooltipProperty("Not counted yet", "", 0, false, TooltipProperty.TooltipPropertyFlags.None)); return l; }
                if (r.AtSea) { Row(l, "At sea", "nobody rides on board"); return l; }
                int mounted = Mounted(r), walk = Math.Max(0, r.Men - mounted);
                Row(l, "Men riding / men", mounted + " of " + r.Men + " men ride (heroes included, as Troops on the top bar)" + (walk > 0 ? " - " + walk + " walk (red)" : " - nobody walks"));
                if (walk > 0)
                {
                    // recenzja 548: bohater bez wlasnego konia jest w puli piechoty (MarchPairs AddMan KHero) - jesli idzie, juz siedzi w WalkNoHorse / NoHarness / Skill;
                    // HeroFoot liczy tez bohaterow jadacych na parach z puli, wiec nie dokladamy go drugi raz
                    int known = r.WalkNoHorse + r.WalkNoHarness + r.WalkSkill;
                    var why = new List<string>();
                    if (r.WalkNoHorse > 0) why.Add(r.WalkNoHorse + " no horse");
                    if (r.WalkNoHarness > 0) why.Add(r.WalkNoHarness + " no harness that fits a horse");
                    if (r.WalkSkill > 0) why.Add(r.WalkSkill + " Riding too low" + (r.SkillLowEn.Count > 0 ? " (" + string.Join(", ", r.SkillLowEn) + ")" : ""));
                    if (walk > known) why.Add((walk - known) + " other");
                    Row(l, "On foot", walk + ": " + string.Join(", ", why));
                }
                Row(l, "Cavalry", r.CavPairs + "/" + (r.CavMen + r.CavWounded) + (r.CavWounded > 0 ? " (" + r.CavWounded + " wounded go on the spare horses)" : ""));
                Row(l, "Footmen on spare horses", r.Ride + "/" + r.PoolMen);
                if (r.HeroMounted > 0) Row(l, "Heroes on their own horses", r.HeroMounted.ToString());
                if (r.SMounts + r.SHarness + r.SOther > 0) Row(l, "Footmen's mounts", r.SMounts + " horses, " + r.SHarness + " harness kept apart from the baggage (town or camp menu)");   // 537 E6
                int notRidden = Math.Max(0, r.AllHorses - r.Ridden);
                Row(l, "Riding horses you own", r.AllHorses + ": " + r.Ridden + " under your men, " + notRidden + " spare (" + r.FreeHerd + " free in the herd)");
                int unusedH = Math.Max(0, r.AllHarness - r.Ridden);
                Row(l, "Harness", r.Ridden + " worn, " + unusedH + " unused"
                    + (r.UnusedPairs > 0 && r.UnusedReqMin >= 0 ? " (" + r.UnusedPairs + " horse and harness pairs need Riding " + r.UnusedReqMin + " or more)" : ""));
                var s = Settings.Current;
                Row(l, " ", "A man rides only a horse with a harness that fits it and his Riding allows (footmen need no Riding for a pair that asks up to " + (s != null ? s.FootmenRidingWaiver : 0) + "). Counted from the baggage and the troops' armoury; "
                            + (MarchPairs.SpeedOn ? "party speed uses these numbers" : "party speed does not use these numbers (March Pairs Speed is off)")
                            + (MarchPairs.WeightOn ? "; harness on ridden horses does not weigh on the baggage." : "; cargo weight does not use them (Ridden Harness Weightless is off)."));   // 537 E2, E3
            }
            catch (Exception e) { Stumble("Tooltip", e); }
            return l;
        }

        /// <summary>548: podpowiedz Pack (dotad wspolna z Horses).</summary>
        private static List<TooltipProperty> PackTooltip()
        {
            var l = new List<TooltipProperty>();
            try
            {
                l.Add(new TooltipProperty("Pack animals", "", 0, false, TooltipProperty.TooltipPropertyFlags.Title));
                var r = MarchPairs.Last;
                if (r == null || !r.Ok) { l.Add(new TooltipProperty("Not counted yet", "", 0, false, TooltipProperty.TooltipPropertyFlags.None)); return l; }
                int over = r.CPack - r.PackLimit;
                Row(l, "Pack animals / allowed", r.CPack + " of " + r.PackLimit + " you may lead before the herding penalty" + (over > 0 ? " - " + over + " too many (red)" : ""));
                Row(l, "Allowed", r.Men + " men - " + r.CLive + " livestock - " + r.FreeHerd + " free horses = " + r.PackLimit);
                var s = Settings.Current;
                if (s != null) Row(l, "Baggage pace", (int)Math.Ceiling(r.Men * Math.Max(0f, s.MarchPackAllowance)) + " pack animals allowed while all men ride");
            }
            catch (Exception e) { Stumble("PackTooltip", e); }
            return l;
        }

        private static void Row(List<TooltipProperty> l, string k, string v) { l.Add(new TooltipProperty(k, v, 0, false, TooltipProperty.TooltipPropertyFlags.None)); }

        // ---------------------------------------------------------------- 541/548/562: podpowiedz zestawu musztry (po angielsku; z pamieci Drill.HudKit)
        private static string HalfLine(Drill.Kit k, int ready, int need, int lo, int hi)
        {
            string sh = k.Shorts(lo, hi, 4);
            return ready + "/" + need + (sh.Length > 0 ? " - short: " + sh : " - complete");
        }

        private static string RolesLine(Drill.Kit k)
        {
            var parts = new List<string>();
            int[] r = k.RoleMen;
            if (r[0] > 0) parts.Add("sword and shield " + r[0]);
            if (r[1] > 0) parts.Add("two-handed " + r[1]);
            if (r[2] > 0) parts.Add("cavalry " + r[2]);
            if (r[3] > 0) parts.Add("archers " + r[3]);
            if (r[4] > 0) parts.Add("crossbowmen " + r[4]);
            if (r[5] > 0) parts.Add("with javelins " + r[5]);
            return parts.Count > 0 ? string.Join(", ", parts) : "-";
        }

        private static List<TooltipProperty> KitTooltip()
        {
            var l = new List<TooltipProperty>();
            try
            {
                l.Add(new TooltipProperty("Drill kit", "", 0, false, TooltipProperty.TooltipPropertyFlags.Title));
                var k = Drill.HudKit();
                if (k == null || !k.Ok) { l.Add(new TooltipProperty("Drill stock is off", "", 0, false, TooltipProperty.TooltipPropertyFlags.None)); return l; }
                if (k.KitN <= 0) { Row(l, "Nobody to drill", "no soldiers or companions - the kit takes nothing"); return l; }
                int per = Drill.OnePer;
                // recenzja 548: mianownik inny niz Troops i Horses, wiec mowimy to wprost; 558: zolnierze + towarzysze, ktorzy cwicza (bez gracza)
                string who = k.Men + " soldiers" + (k.Heroes > 0 ? " and " + k.Heroes + (k.Heroes == 1 ? " companion" : " companions") : "") + (Drill.HudHeroesOn ? " - you lead the drill and are not counted" : " - heroes do not drill and are not counted");
                Row(l, "Full kit / men", k.Ready + " of " + k.KitN + " have a full drill kit for their role" + (k.Ready < k.KitN ? " (red)" : "") + " (" + who + ")");
                Row(l, "Weapons", HalfLine(k, k.ReadyW, k.KitN, Drill.K1H, Drill.KThrown));
                if (k.NeedR > 0) Row(l, "Ranged", k.ReadyR + "/" + k.NeedR + " archers, crossbowmen and javelin men" + (k.Shorts(Drill.KBow, Drill.KThrown, 4).Length > 0 ? " - short: " + k.Shorts(Drill.KBow, Drill.KThrown, 4) : ""));
                Row(l, "Armour", HalfLine(k, k.ReadyA, k.KitN, Drill.KBody, Drill.KCape));
                Row(l, "Roles", RolesLine(k));
                Row(l, "Kit per man", "Infantry: a one-handed weapon and a shield, or a two-handed weapon. Cavalry: a one-handed weapon, a shield and a spear. Archers: a bow and a quiver of arrows; "
                            + "crossbowmen: a crossbow and bolts; javelin men also a stack of javelins. Everyone: body armour, a helmet, leg armour (boots) and a cape. Gloves are not part of the drill kit."
                            + (per == 1 ? "" : " One piece of each kind serves " + per + " men."));
                Row(l, "Refill", "Your men take what their drill kit lacks from their spare kit in the troops' armoury - once a day and when the troops change - only pieces the next battle's hand-out leaves unused; "
                            + "those pieces leave the armoury. Taken today " + k.Taken + ", given back for battle " + k.BackBattle + ", back to the armoury (not needed) " + k.Back + ".");
                int sh = 0; for (int i = 0; i < Drill.NK; i++) sh += k.Short(i);
                Row(l, "From you", "Give arms and armour in the inventory (Discard) or leave trophies - your men keep what their kit lacks: " + sh + " pieces.");
                float bonus = (k.Bonus(true) + k.Bonus(false)) * 100f;
                float max = Drill.BonusPerSet * 100f * ((k.PerkW ? Drill.BonusPerkMult : 1f) + (k.PerkA ? Drill.BonusPerkMult : 1f));
                Row(l, "Drill bonus", "+" + bonus.ToString("0.#") + "% of +" + max.ToString("0.#") + "%" + (Drill.HudHeroesOn ? " drill XP of troops and companions" : " troop XP from drill") + " (each half grows with the share of men with a full weapons or armour kit; "
                       + "Giving Hands raises the weapons half" + (k.PerkW ? "" : " - not taken") + ", Paid in Promise the armour half" + (k.PerkA ? "" : " - not taken")
                       + ", from +" + (Drill.BonusPerSet * 100f).ToString("0.#") + "% to +" + (Drill.BonusPerSet * 100f * Drill.BonusPerkMult).ToString("0.#") + "%)");
                float rw = 0f, ra = 0f;
                for (int i = 0; i < Drill.NK; i++) { if (i <= Drill.KThrown) rw += k.WearPerFullDay(i); else ra += k.WearPerFullDay(i); }
                Row(l, "Wear", "a full day of drill (8 h) wears out about " + rw.ToString("0.00") + " weapons and " + ra.ToString("0.00") + " pieces of armour - the cheapest first. A piece lasts days of drill: "
                            + "weapons " + Drill.LifeDays(Drill.K1H).ToString("0") + ", shields " + Drill.LifeDays(Drill.KShield).ToString("0") + ", bows " + Drill.LifeDays(Drill.KBow).ToString("0")
                            + ", crossbows " + Drill.LifeDays(Drill.KXbow).ToString("0") + ", arrows and bolts " + Drill.LifeDays(Drill.KArrows).ToString("0") + ", javelins " + Drill.LifeDays(Drill.KThrown).ToString("0")
                            + ", armour " + Drill.LifeDays(Drill.KBody).ToString("0") + ", capes " + Drill.LifeDays(Drill.KCape).ToString("0") + ". No drill, no wear.");
                var worn = new List<string>();
                for (int i = 0; i < Drill.NK; i++) if (k.WornToday[i] > 0) worn.Add(k.WornToday[i] + " " + Drill.KindName(i));
                Row(l, "Worn out today", worn.Count > 0 ? string.Join(", ", worn) : "nothing");
                Row(l, " ", "Worn pieces become scrap the smiths of the next town buy - the coin goes to the men's purse. What the kit no longer needs (after deaths or promotions) goes back to the armoury and is sold with the men's spare kit.");
            }
            catch (Exception e) { Stumble("KitTooltip", e); }
            return l;
        }
    }
}
