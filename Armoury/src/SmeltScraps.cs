using System;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// SKRZYNKA RESZTEK PRZETOPU (r4-przetop, Jeff 20.10 na pytanie "przetop broni z 1 sztaby oddaje cala sztabe - zostawic czy oddawac czesc?": "tylko czesc").
    /// DOWOD (kod): SmeltTruth.WeaponMetal: n = max(1, floor(metal czesci x udzial)), udzial <= 0.5 - bron z 1 sztaba (31 wzorow, np. simple_pike_t2) oddawala 100%,
    /// petla kucie/przetop kosztowala tylko wegiel; Recipes.SmeltYield (pancerz w zakladce Smelt): kazda czesc receptury max(1, floor(liczba x udzial)) - czesc
    /// z 1 sztaba wraca cala, a sufit SmeltTab.YieldFor tez ma max(1, ...).
    /// REGULA (klucz SmeltKeepsScraps): przetop daje DOKLADNIE udzial metalu (tysiaczne czesci sztaby, bez minimum 1 sztaby), w gatunkach jak dotad (bron - czesci,
    /// pancerz - receptura; gatunku nie obnizamy). Cale sztaby ida do sakw, ulamek kazdego gatunku do skrzynki resztek kuzni gracza (zapis gry arm_smeltscraps);
    /// gdy ulamek z tego przetopu i skrzynka dadza razem pelna sztabe, wychodzi ona w tym samym przetopie (czyli przy nastepnym przetopie tego gatunku).
    /// Simple Pike (1 zelazo kute, udzial 50%): 1. przetop 0 sztab (+0.5 do skrzynki), 2. przetop 1 sztaba (skrzynka pusta).
    /// PODGLAD = PRAWDA (dekompilacja 1.4.8): SmeltingItemVM (lista Smelt, takze w ekranie CRAFT BK - CraftingMixin.ExecuteMainActionBK wola ten sam
    /// SmeltingVM.TrySmeltingSelectedItems) i CraftingVM.UpdateCurrentMaterialCosts licza wynik z GetSmeltingOutputForItem - nasz postfiks dolicza skrzynke, wiec
    /// ekran pokazuje to, co wyjdzie. CraftingCampaignBehavior.DoSmelting wola GetSmeltingOutputForItem raz i dodaje wynik bez warunku - zapis skrzynki tylko
    /// w oknie prawdziwego przetopu (prefiks Priority.Last .. postfiks Priority.First; pancerz - nasz DoSmeltingPrefix), liczony ze zdjecia skrzynki z poczatku
    /// okna (kazde wywolanie w oknie daje to samo), a przetop zatrzymany przez inny prefiks cofa zapis. SmeltingVM.SmeltSelectedItems odswieza liste PO
    /// DoSmelting - podglad nastepnej sztuki widzi juz nowa skrzynke. Wynik 0 sztab: gra nie blokuje (RefreshEnableMainAction sprawdza tylko stamine, wsad
    /// - wegiel - i wybor), DoSmelting zdejmuje bron i wegiel jak dotad.
    /// Wylaczony klucz = jak dotad (minimum 1 sztaba); skrzynka zostaje w zapisie nietknieta.
    /// </summary>
    internal static class SmeltScraps
    {
        internal const int Unit = 1000;                        // tysiaczne czesci sztaby
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly string[] Names = { "ore", "crude", "wrought", "iron", "steel", "fine", "castle" };
        private static readonly int[] Zero = new int[7];
        private static int[] _box = new int[7];                 // skrzynka: indeksy 1..6 (surowka .. stal zamkowa), zawsze 0 .. Unit-1

        // okno prawdziwego przetopu
        private static ItemObject _cap; private static int[] _snap; private static bool _committed;
        private static int[] _milli, _out, _fromBox;           // ostatni wynik w oknie (dokladnie, wydane, sztaby z resztek)
        // ostatni prawdziwy przetop (komunikat, linia logu, SmeltTruth.OnWeaponSmelted)
        private static ItemObject _lastItem; private static int[] _lastOut;
        private static int _smelts, _lines, _errors;
        internal static bool Ignore;                            // samosprawdzenie przy starcie sesji: wynik bez skrzynki

        internal static bool On { get { var s = Settings.Current; return s != null && s.SmeltKeepsScraps; } }

        internal static void Reset()
        {
            _box = new int[7];
            _cap = null; _snap = null; _committed = false; _milli = _out = _fromBox = null;
            _lastItem = null; _lastOut = null; _smelts = _lines = 0; Ignore = false;
        }

        /// <summary>Start sesji: liczniki linii od zera, okno zamkniete (skrzynka z zapisu zostaje).</summary>
        internal static void SessionReset()
        {
            _cap = null; _snap = null; _committed = false; _lastItem = null; _lastOut = null; _smelts = _lines = 0; Ignore = false;
        }

        // ------------------------------------------------------------ zapis (arm_smeltscraps, format "1|s1|s2|s3|s4|s5|s6")
        internal static string Export()
        {
            var sb = new StringBuilder("1");
            for (int g = 1; g <= 6; g++) sb.Append('|').Append(_box[g].ToString(Inv));
            return sb.ToString();
        }

        /// <summary>Brak klucza (stary zapis), inny format albo zla liczba = skrzynka pusta.</summary>
        internal static void Import(string s)
        {
            var b = new int[7];
            try
            {
                if (!string.IsNullOrEmpty(s))
                {
                    var p = s.Split('|');
                    if (p.Length >= 7 && p[0] == "1")
                        for (int g = 1; g <= 6; g++)
                        {
                            int v;
                            if (int.TryParse(p[g], NumberStyles.Integer, Inv, out v) && v > 0 && v < Unit) b[g] = v;
                        }
                }
            }
            catch (Exception e) { Stumble("Import", e); b = new int[7]; }
            _box = b;
        }

        private static void Stumble(string where, Exception e)
        {
            _errors++;
            if (_errors <= 5 || _errors % 100 == 0) Log.Error("SmeltScraps." + where + " (potkniecie " + _errors + ")", e);
        }

        // ------------------------------------------------------------ dokladny udzial (tysiaczne czesci sztaby)
        /// <summary>Rozdziela n tysiacznych na gatunki wedlug wag (najwieksze reszty, remis - nizszy gatunek, jak SmeltTruth.WeaponMetal).</summary>
        private static int[] Spread(int[] w, long n)
        {
            var outp = new int[7];
            long total = 0;
            for (int g = 1; g <= 6; g++) if (w[g] > 0) total += w[g];
            if (total <= 0 || n <= 0) return outp;
            long given = 0;
            var rem = new long[7];
            for (int g = 1; g <= 6; g++)
            {
                if (w[g] <= 0) { rem[g] = -1; continue; }
                long num = w[g] * n;
                outp[g] = (int)(num / total);
                rem[g] = num % total;
                given += outp[g];
            }
            while (given < n)
            {
                int best = -1;
                for (int g = 1; g <= 6; g++) if (rem[g] >= 0 && (best < 0 || rem[g] > rem[best])) best = g;
                if (best < 0) break;
                outp[best]++; rem[best] = -1; given++;
            }
            return outp;
        }

        /// <summary>Bron: metal czesci x udzial (bez minimum 1), nie wiecej niz sufit klasy BK (cap &gt; 0) i niz metal czesci - w tysiacznych, gatunki czesci.</summary>
        internal static int[] WeaponMilli(int[] raw, int cap, float share)
        {
            long total = 0;
            for (int g = 1; g <= 6; g++) total += Math.Max(0, raw[g]);
            if (total <= 0) return new int[7];
            double n = total * (double)share;
            if (cap > 0 && n > cap) n = cap;
            if (n > total) n = total;
            return Spread(raw, (long)Math.Floor(n * Unit + 1e-6));
        }

        /// <summary>Pancerz/tarcza (zakladka Smelt): metal receptury (Recipes.For) x udzial (najwyzej 0.5), sufit jak SmeltTab.YieldFor, ale bez minimum 1 sztaby.</summary>
        internal static int[] ArmourMilli(ItemObject it)
        {
            try
            {
                var s = Settings.Current;
                if (it == null || s == null) return new int[7];
                var r = Recipes.For(it);
                var w = new int[7];
                long total = 0;
                if (r.Parts != null)
                    foreach (var p in r.Parts)
                    {
                        if (p.Item == null || p.Count <= 0) continue;
                        for (int g = 1; g <= 6; g++)
                            if (Recipes.MaterialItem((CraftingMaterials)g) == p.Item) { w[g] += p.Count; total += p.Count; break; }
                    }
                if (total <= 0) return new int[7];
                float share = SmeltTruth.Share();
                double n = total * (double)share;
                if (s.SmeltCapToCraftCost)
                {
                    // ten sam sufit co SmeltTab.YieldFor: kwit kucia (SmeltCapFromForgeBill) albo punkty ochrony - tylko bez "co najmniej 1"
                    long u = s.SmeltCapFromForgeBill ? (it.HasArmorComponent || s.BkShieldBillByRecipe ? total : 0) : Recipes.ArmourUnits(it);
                    if (u > 0 && n > u * (double)share) n = u * (double)share;
                }
                return Spread(w, (long)Math.Floor(n * Unit + 1e-6));
            }
            catch (Exception e) { Stumble("ArmourMilli", e); return new int[7]; }
        }

        // ------------------------------------------------------------ wynik i skrzynka
        /// <summary>Wynik metalu (indeksy 1..6 - przypisuje) z dokladnego udzialu i skrzynki; w oknie prawdziwego przetopu tej sztuki zapisuje nowa skrzynke.</summary>
        internal static void Apply(ItemObject item, int[] milli, int[] result)
        {
            try
            {
                if (milli == null || result == null || result.Length < 7) return;
                bool live = _cap != null && _cap == item && _snap != null && !Ignore;
                int[] box = live ? _snap : (Ignore ? Zero : _box);
                var nb = new int[7]; var fromBox = new int[7];
                for (int g = 1; g <= 6; g++)
                {
                    long t = (long)Math.Max(0, milli[g]) + box[g];
                    result[g] = (int)(t / Unit);
                    nb[g] = (int)(t % Unit);
                    fromBox[g] = result[g] - Math.Max(0, milli[g]) / Unit;
                }
                if (!live) return;
                _box = nb; _committed = true;
                _milli = (int[])milli.Clone(); _out = new int[7]; for (int g = 1; g <= 6; g++) _out[g] = result[g]; _fromBox = fromBox;
            }
            catch (Exception e) { Stumble("Apply", e); }
        }

        /// <summary>Okno prawdziwego przetopu tej sztuki (zdjecie skrzynki).</summary>
        internal static void Begin(ItemObject item)
        {
            _lastItem = null; _lastOut = null;
            if (item == null || !On) { _cap = null; _snap = null; _committed = false; return; }
            _cap = item; _snap = (int[])_box.Clone(); _committed = false; _milli = _out = _fromBox = null;
        }

        /// <summary>Koniec okna: przetop sie odbyl - zostaje nowa skrzynka (komunikat w Announce); nie odbyl sie - skrzynka wraca do zdjecia.</summary>
        internal static void End(bool smelted)
        {
            try
            {
                if (_cap == null) return;
                if (_committed)
                {
                    if (!smelted) { if (_snap != null) _box = _snap; }
                    else { _lastItem = _cap; _lastOut = _out; }
                }
            }
            catch (Exception e) { Stumble("End", e); }
            finally { if (!smelted || !_committed) { _milli = _out = _fromBox = null; } _cap = null; _snap = null; _committed = false; }
        }

        /// <summary>Wynik ostatniego prawdziwego przetopu tej sztuki (SmeltTruth.OnWeaponSmelted - linia logu ma pokazac to, co wyszlo, nie podglad nastepnej).</summary>
        internal static int[] LastOutFor(ItemObject item) { return item != null && item == _lastItem ? _lastOut : null; }

        /// <summary>Po prawdziwym przetopie: komunikat dla gracza (po angielsku) i linia logu "Przetop resztki (r4-przetop)".</summary>
        internal static void Announce(ItemObject item)
        {
            try
            {
                if (item == null || item != _lastItem || _milli == null || _out == null || _fromBox == null) return;
                var milli = _milli; var outp = _out; var fromBox = _fromBox;
                _milli = _out = _fromBox = null;
                _smelts++;
                var model = Campaign.Current != null && Campaign.Current.Models != null ? Campaign.Current.Models.SmithingModel : null;
                var added = new StringBuilder(); var whole = new StringBuilder();
                for (int g = 1; g <= 6; g++)
                {
                    int frac = Math.Max(0, milli[g]) % Unit;
                    if (frac > 0) { if (added.Length > 0) added.Append(", "); added.Append('+').Append(Num(frac)).Append(' ').Append(MatName(model, g)); }
                    if (fromBox[g] > 0) { if (whole.Length > 0) whole.Append(", "); whole.Append(fromBox[g].ToString(Inv)).Append(' ').Append(MatName(model, g)); }
                }
                if (added.Length > 0 || whole.Length > 0)
                {
                    var msg = new StringBuilder("Smelting leftovers: ");
                    msg.Append(added.Length > 0 ? added + " went to your scrap box" : "nothing went to your scrap box");
                    if (whole.Length > 0) msg.Append(" - the scraps made up a full bar: ").Append(whole);
                    msg.Append(". Scrap box now holds: ").Append(BoxText(model)).Append('.');
                    Log.Player(msg.ToString());
                }
                if (_lines < 40 || _smelts % 25 == 0)
                {
                    _lines++;
                    Log.Info("Przetop resztki (r4-przetop): " + item.StringId + " | dokladnie: " + MilliFmt(milli) + " | wydane: " + SmeltTruth.Metal(outp)
                             + " (z resztek: " + SmeltTruth.Metal(fromBox) + ") | skrzynka: " + MilliFmt(_box)
                             + " | przetopow z kluczem Smelt Keeps Scraps w sesji " + _smelts.ToString(Inv) + (_lines >= 40 ? " (dalej co 25.)" : ""));
                }
            }
            catch (Exception e) { Stumble("Announce", e); }
        }

        // ------------------------------------------------------------ latki DoSmelting (bron - oryginal gry; pancerz idzie SmeltTab.DoSmeltingPrefix)
        /// <summary>Prefiks Priority.Last: przetop broni naprawde biegnie (__runOriginal) - otwiera okno zapisu skrzynki.</summary>
        public static void SmeltPrefix(EquipmentElement equipmentElement, bool __runOriginal)
        {
            try
            {
                _lastItem = null; _lastOut = null;
                if (!__runOriginal) return;
                var it = equipmentElement.Item;
                if (it == null || it.WeaponDesign == null) return;
                Begin(it);
            }
            catch (Exception e) { Stumble("SmeltPrefix", e); }
        }

        /// <summary>Postfiks Priority.First: zamyka okno (przetop zatrzymany - skrzynka wraca), komunikat i linia logu.</summary>
        public static void SmeltPostfix(EquipmentElement equipmentElement, bool __runOriginal)
        {
            try
            {
                var it = equipmentElement.Item;
                if (_cap == null || it == null || _cap != it) { _cap = null; _snap = null; _committed = false; return; }
                End(__runOriginal);
                if (__runOriginal) Announce(it);
            }
            catch (Exception e) { Stumble("SmeltPostfix", e); }
        }

        // ------------------------------------------------------------ napisy
        private static string Num(int milli) { return (milli / (double)Unit).ToString("0.##", Inv); }

        private static string MatName(TaleWorlds.CampaignSystem.ComponentInterfaces.SmithingModel model, int g)
        {
            try
            {
                var it = model != null ? model.GetCraftingMaterialItem((CraftingMaterials)g) : Recipes.MaterialItem((CraftingMaterials)g);
                if (it != null && it.Name != null) return it.Name.ToString();
            }
            catch { }
            return Names[g];
        }

        private static string BoxText(TaleWorlds.CampaignSystem.ComponentInterfaces.SmithingModel model)
        {
            var sb = new StringBuilder();
            for (int g = 1; g <= 6; g++)
            {
                if (_box[g] <= 0) continue;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(Num(_box[g])).Append(' ').Append(MatName(model, g));
            }
            return sb.Length > 0 ? sb.ToString() : "nothing";
        }

        /// <summary>Tysiaczne slowami logu (wrought 0.500 ...).</summary>
        internal static string MilliFmt(int[] a)
        {
            if (a == null) return "-";
            var sb = new StringBuilder();
            for (int g = 1; g <= 6 && g < a.Length; g++)
            {
                if (a[g] <= 0) continue;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(Names[g]).Append(' ').Append((a[g] / (double)Unit).ToString("0.000", Inv));
            }
            return sb.Length > 0 ? sb.ToString() : "pusto";
        }

        internal static string BoxLog() { return MilliFmt(_box); }

        /// <summary>Dwa przetopy tej samej sztuki od pustej skrzynki (bez zapisu): ile sztab w 1. i 2. - linia startu sesji.</summary>
        internal static string TwoSmelts(int[] milli)
        {
            int a = 0, b = 0;
            for (int g = 1; g <= 6; g++)
            {
                int m = Math.Max(0, milli[g]);
                a += m / Unit;
                b += (m + m % Unit) / Unit;
            }
            return "dokladnie " + MilliFmt(milli) + " -> 1. przetop " + a.ToString(Inv) + " szt., 2. przetop " + b.ToString(Inv) + " szt.";
        }
    }
}
