using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;

namespace Armoury
{
    /// <summary>
    /// 474 P1/P2 (Jeff 13.11, projekt docs/PROJEKT-WSIE-POBOCZNE-KARCZMY-2026-11-13.md pkt 1 "Nazwy pod klawiszem" i "Inwestycje z ikona"): GLOWNY TOWAR wioski pobocznej
    /// i lista jej towarow. Sam odczyt - niczego nie liczy od nowa poza wycena czesci, ktora juz jest (VillageCrafts.PartsOf - ta sama, co dymek, menu i okno prac).
    ///  - glowne zajecie czesci: pod KS przy kluczach ikony okregu (IconCraft >= 0) - zajecie ziemi czesci (ta sama regula, co ikona okregu, DistrictIcon.Compute);
    ///    inaczej zajecie o najwiekszej wartosci dnia (CraftVal, bez koni - jak DistrictIcon); bez wartosci - zajecie czesci (Pc.Craft);
    ///  - glowny towar: z towarow glownego zajecia ten, ktory daje najwiecej pieniedzy (sztuki dnia x cena miasta targowego, VillageCrafts.Price); bez nich - ikona zajecia;
    ///  - reszta: pozostale towary czesci o wartosci > 0, od najdrozszego, najwyzej 3;
    ///  - ikona: styl pedzla gry "Settlement.Event.Type.Image" (SandBox GUI\Brushes\Nameplates.xml - te same obrazki, co tabliczki wsi gry) o nazwie towaru
    ///    (camel / horse jak DistrictIcon.IconId); brak stylu - ikona glownego zajecia (meta.Icon, zawsze towar gry); brak i tego - bez ikony.
    /// Pamiec dla napisow pod Altem (MapVillagesView): okreg liczony raz na dobe i dopiero na zadanie (najwyzej kilka okregow na cwierc sekundy). Nie rzuca.
    /// </summary>
    internal static class SideVillageGoods
    {
        internal const string Brush = "Settlement.Event.Type.Image";

        internal sealed class Info
        {
            public ItemObject Main;          // glowny towar (null - nieznany)
            public string IconId;            // nazwa stylu pedzla (null - bez ikony)
            public string Makes;             // "wheat (main), flax, hemp" (null - brak towarow)
        }

        private sealed class Dist { public int Day = -1; public readonly Dictionary<string, Info> ByUid = new Dictionary<string, Info>(StringComparer.Ordinal); }
        private static readonly Dictionary<string, Dist> _dist = new Dictionary<string, Dist>(StringComparer.Ordinal);
        private static Campaign _for;
        private static int _st, _computed;

        internal static int Computed { get { return _computed; } }
        internal static int Stumbles { get { return _st; } }

        private static void Stumble(string where, Exception e)
        {
            _st++;
            if (_st <= 3 || _st % 500 == 0) Log.Error("SideVillageGoods." + where + " (potkniecie " + _st + ")", e);
        }

        /// <summary>Napisy pod Altem: towar wioski z pamieci. compute = wolno policzyc okreg (stara doba albo brak) - false: tylko to, co juz jest.
        /// null - nie policzone (jeszcze) albo brak.</summary>
        internal static Info Cached(Settlement district, string uid, bool compute, out bool didCompute)
        {
            didCompute = false;
            try
            {
                if (district == null || district.Village == null || uid == null) return null;
                var c = Campaign.Current;
                if (!ReferenceEquals(c, _for)) { _dist.Clear(); _for = c; }
                int today = (int)CampaignTime.Now.ToDays;
                Dist d;
                if (!_dist.TryGetValue(district.StringId, out d) || d.Day != today)
                {
                    if (!compute) { Info old; return d != null && d.ByUid.TryGetValue(uid, out old) ? old : null; }   // stara doba - stara ikona do przeliczenia
                    didCompute = true;
                    d = ComputeDistrict(district.Village, today);
                    _dist[district.StringId] = d;
                }
                Info i; return d.ByUid.TryGetValue(uid, out i) ? i : null;
            }
            catch (Exception e) { Stumble("Cached", e); return null; }
        }

        private static Dist ComputeDistrict(Village v, int today)
        {
            var d = new Dist { Day = today };
            var parts = new List<VillageCrafts.PartCraft>();
            bool ks;
            if (!VillageCrafts.PartsOf(v, parts, out ks)) return d;
            Town town = TownOf(v);
            foreach (var p in parts)
            {
                if (p == null || p.Main || p.Uid == null) continue;
                try { d.ByUid[p.Uid] = Of(p, town); }
                catch (Exception e) { Stumble("Part " + p.Uid, e); }
            }
            _computed++;
            return d;
        }

        internal static Town TownOf(Village v)
        {
            if (v == null) return null;
            return v.TradeBound != null ? v.TradeBound.Town : (v.Bound != null ? v.Bound.Town : null);
        }

        /// <summary>Towary jednej czesci (wycena z PartsOf). Nie rzuca wyzej niz wolajacy (wolajacy lapie).</summary>
        internal static Info Of(VillageCrafts.PartCraft p, Town town)
        {
            var info = new Info();
            if (p == null) return info;
            int mc = MainCraft(p);
            var meta = VillageCrafts.Meta(mc);
            // wartosci towarow
            var items = new List<KeyValuePair<ItemObject, double>>(p.Pcs.Count);
            foreach (var kv in p.Pcs)
            {
                if (kv.Key == null || kv.Key.Name == null || !(kv.Value > 0)) continue;
                double val = kv.Value * VillageCrafts.Price(town, kv.Key);
                if (!(val > 0) || double.IsNaN(val) || double.IsInfinity(val)) continue;
                items.Add(new KeyValuePair<ItemObject, double>(kv.Key, val));
            }
            items.Sort((a, b) => b.Value != a.Value ? b.Value.CompareTo(a.Value) : string.CompareOrdinal(a.Key.StringId ?? "", b.Key.StringId ?? ""));
            ItemObject main = null;
            if (meta != null && meta.Goods != null)
                foreach (var kv in items) { if (Array.IndexOf(meta.Goods, kv.Key) >= 0) { main = kv.Key; break; } }
            if (main == null && meta != null) main = meta.Icon;
            if (main == null && items.Count > 0) main = items[0].Key;
            info.Main = main;
            // ikona: styl towaru, inaczej styl ikony zajecia
            string id = IconId(main);
            if (!HasStyle(id)) id = meta != null ? IconId(meta.Icon) : null;
            info.IconId = HasStyle(id) ? id : null;
            // linia
            if (main != null && main.Name != null)
            {
                var sb = new StringBuilder(80);
                sb.Append(Low(main)).Append(" (main)");
                int more = 0;
                foreach (var kv in items)
                {
                    if (ReferenceEquals(kv.Key, main)) continue;
                    if (more == 3) break;
                    sb.Append(", ").Append(Low(kv.Key));
                    more++;
                }
                info.Makes = sb.ToString();
            }
            return info;
        }

        /// <summary>Glowne zajecie czesci: pod KS przy kluczach ikony okregu - zajecie ziemi (IconCraft); inaczej najwieksza wartosc dnia (bez koni); zapas - zajecie czesci.</summary>
        internal static int MainCraft(VillageCrafts.PartCraft p)
        {
            if (p.Ks && p.IconCraft >= 0) return p.IconCraft;
            int best = -1; double bv = 0;
            for (int c = 0; c < p.CraftVal.Length; c++)
            {
                var m = VillageCrafts.Meta(c);
                if (m == null || m.Horse || !(p.CraftVal[c] > bv)) continue;
                best = c; bv = p.CraftVal[c];
            }
            return best >= 0 ? best : p.Craft;
        }

        private static int _noteMakes, _noteIcon;

        /// <summary>474 P2: slad w logu - pierwsze 3 linie "Makes" i pierwsze 3 ikony okna prac od startu gry (dowod, ze droga dziala; potem cisza).</summary>
        internal static void NoteMakes(string uid, string line)
        {
            if (_noteMakes >= 3) return;
            _noteMakes++;
            Log.Info("Wioski - linia Makes (474 P2, " + _noteMakes + "/3 od startu gry): " + uid + " | " + line);
        }

        internal static void NoteIcon(string name, string sprite)
        {
            if (_noteIcon >= 3) return;
            _noteIcon++;
            Log.Info("Wioski - ikona glownego towaru w oknie prac (474 P2, " + _noteIcon + "/3 od startu gry): " + name + " | " + (sprite ?? "BRAK obrazka - bez ikony"));
        }

        private static string Low(ItemObject it) { return it.Name.ToString().ToLowerInvariant(); }

        /// <summary>Nazwa stylu jak DistrictIcon.IconId (camel / horse / StringId).</summary>
        internal static string IconId(ItemObject it)
        {
            if (it == null) return null;
            string id = it.StringId ?? "";
            return id.Contains("camel") ? "camel" : ((id.Contains("horse") || id.Contains("mule")) ? "horse" : id);
        }

        private static readonly Dictionary<string, string> _sprite = new Dictionary<string, string>(StringComparer.Ordinal);   // styl -> nazwa obrazka ("" - brak)

        internal static bool HasStyle(string id) { return SpriteOf(id) != null; }

        /// <summary>Nazwa obrazka stylu pedzla (np. General\Icons\Production\wheat) do &lt;img&gt; w tekstach menu i okien; null - brak stylu albo obrazka.</summary>
        internal static string SpriteOf(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            string s;
            if (_sprite.TryGetValue(id, out s)) return s.Length > 0 ? s : null;
            s = "";
            try
            {
                var bf = UIResourceManager.BrushFactory;
                var br = bf != null ? bf.GetBrush(Brush) : null;
                var style = br != null ? br.GetStyle(id) : null;
                var layer = style != null ? style.GetLayer(0) : null;
                var sp = layer != null ? layer.Sprite : null;
                if (sp != null && !string.IsNullOrEmpty(sp.Name)) s = sp.Name;
            }
            catch (Exception e) { Stumble("SpriteOf " + id, e); }
            if (UIResourceManager.BrushFactory != null) _sprite[id] = s;   // bez fabryki pedzli (wczesnie) - nie zapamietuj braku
            return s.Length > 0 ? s : null;
        }
    }
}
