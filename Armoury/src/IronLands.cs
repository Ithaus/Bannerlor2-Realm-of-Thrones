using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// P3/P2a (TRIAZ-3 bieg roczny 13.10, O2-1 "Poprawka ostateczna" P2 pomiar a) - ZELAZO WEDLUG LADOW. TYLKO LOG - niczego nie zmienia w grze.
    /// Podwazenie O2-1 pkt 3: w Essos zelaza naprawde brakuje (8 kopaln z 26, nadwyzka zrodel 110 lup wobec 4 530 w Westeros), ale log tego nie mierzyl -
    /// podzial liczono recznie ze stalej x mapy. Decyzja P3 (dluzszy rejs albo regulator wsi osobno na lad) potrzebuje produkcji, zuzycia, zapasu i liczby
    /// miast bez zelaza osobno dla obu ladow.
    /// Podzial LADEM, nie stala x: skladowe drogi ladowej miast (pamiec drog gry); dwie najwieksze to lady glowne - zachodni = Westeros, drugi = Essos;
    /// miasta poza nimi (wyspy), zamki i wsie - do ladu najblizszego miasta w linii prostej (wies - lad osady, do ktorej nalezy). Gdy mapa nie rozdzieli sie
    /// droga ladowa na dwa duze lady - zapasowo stala x = 800 (jak w rozpoznaniu), z dopiskiem w linii. Liczone raz na kampanie.
    /// Linia doby "Zelazo wedlug ladow (P3/P2a)" zaraz po linii "Zelazo (lupy) (214)" (OreLedger.Line): lupy dopisane wsiom, zuzycie zmierzone (warsztaty
    /// zbrojne, strzelarze, rzemioslo miasta - MaterialOrders.NoteUse od poprzedniej doby; wsad linii towarowych gry - OnItemConsumed), zapas miast i zamkow,
    /// nadwyzka zrodel-miast ponad prog kontraktow, miasta bez zelaza.
    /// </summary>
    internal static class IronLands
    {
        private static readonly string[] Name = { "Westeros", "Essos" };
        private static Dictionary<Settlement, int> _of;
        private static List<Town> _towns;
        private static string _how = "-";
        private static readonly int[] _townsIn = new int[2];
        private static readonly int[] _prod = new int[2], _lines = new int[2];
        private static int _stumbles;
        private static bool _errLogged;

        internal static void Reset() { _of = null; _towns = null; _how = "-"; _stumbles = 0; _errLogged = false; ClearDay(); }
        private static void ClearDay() { Array.Clear(_prod, 0, 2); Array.Clear(_lines, 0, 2); }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (!_errLogged) { _errLogged = true; Log.Error("IronLands." + where, e); }
        }

        private static bool Build()
        {
            if (_of != null) return true;
            if (Campaign.Current == null) return false;
            var towns = new List<Town>();
            foreach (var t in Town.AllTowns) if (t != null && t.IsTown && t.Settlement != null) towns.Add(t);
            if (towns.Count == 0) return false;
            var of = new Dictionary<Settlement, int>();
            var dm = Campaign.Current.Models.MapDistanceModel;
            var comp = new List<List<Town>>();
            foreach (var t in towns)
            {
                int found = -1;
                for (int k = 0; k < comp.Count && found < 0; k++)
                {
                    float d;
                    try { d = dm.GetDistance(comp[k][0].Settlement, t.Settlement, false, false, MobileParty.NavigationType.Default); } catch { d = -1f; }
                    if (d > 0f && d < CartTownExit.BkLimit) found = k;
                }
                if (found < 0) comp.Add(new List<Town> { t }); else comp[found].Add(t);
            }
            int a = -1, b = -1;
            for (int k = 0; k < comp.Count; k++)
            {
                if (a < 0 || comp[k].Count > comp[a].Count) { b = a; a = k; }
                else if (b < 0 || comp[k].Count > comp[b].Count) b = k;
            }
            if (a >= 0 && b >= 0 && comp[a].Count >= 20 && comp[b].Count >= 8)
            {
                double xa = 0, xb = 0;
                foreach (var t in comp[a]) xa += t.Settlement.GetPosition2D.x;
                foreach (var t in comp[b]) xb += t.Settlement.GetPosition2D.x;
                int west = xa / comp[a].Count <= xb / comp[b].Count ? a : b, east = west == a ? b : a;
                foreach (var t in comp[west]) of[t.Settlement] = 0;
                foreach (var t in comp[east]) of[t.Settlement] = 1;
                var main = new List<Town>(comp[west]); main.AddRange(comp[east]);
                int isl = 0;
                foreach (var t in towns)
                {
                    if (of.ContainsKey(t.Settlement)) continue;
                    of[t.Settlement] = NearestLand(t.Settlement, main, of);
                    isl++;
                }
                _how = "skladowe drogi ladowej (" + comp.Count + " skladowych; lady glowne " + comp[west].Count + " i " + comp[east].Count + " miast, miasta poza nimi " + isl + " - do blizszego ladu)";
            }
            else
            {
                foreach (var t in towns) of[t.Settlement] = t.Settlement.GetPosition2D.x < 800f ? 0 : 1;
                _how = "stala x = 800 (droga ladowa nie rozdzielila mapy na dwa duze lady: skladowych " + comp.Count + ")";
            }
            _townsIn[0] = _townsIn[1] = 0;
            foreach (var t in towns) _townsIn[of[t.Settlement]]++;
            _towns = towns; _of = of;
            return true;
        }

        private static int NearestLand(Settlement st, List<Town> among, Dictionary<Settlement, int> of)
        {
            var p = st.GetPosition2D;
            float best = float.MaxValue; int land = 0;
            foreach (var t in among)
            {
                float d = p.DistanceSquared(t.Settlement.GetPosition2D);
                int l;
                if (d < best && of.TryGetValue(t.Settlement, out l)) { best = d; land = l; }
            }
            return land;
        }

        /// <summary>Lad osady: 0 Westeros, 1 Essos; -1 = nie da sie ustalic (przed startem kampanii).</summary>
        internal static int Of(Settlement st)
        {
            if (st == null || !Build()) return -1;
            int l;
            if (_of.TryGetValue(st, out l)) return l;
            var bound = st.IsVillage && st.Village != null ? st.Village.Bound : null;
            if (bound != null && bound != st && _of.TryGetValue(bound, out l)) { _of[st] = l; return l; }
            l = NearestLand(bound ?? st, _towns, _of);
            _of[st] = l;
            return l;
        }

        /// <summary>OreLedger.OnProduced: lupy dopisane wsi (kopalni).</summary>
        internal static void NoteProduced(Settlement st, int n)
        {
            try { int l = Of(st); if (l >= 0 && n > 0) _prod[l] += n; }
            catch (Exception e) { Stumble("NoteProduced", e); }
        }

        /// <summary>OreLedger.OnConsumed: wsad linii towarowej warsztatu (narzedzia) w osadzie.</summary>
        internal static void NoteLine(Settlement st, int n)
        {
            try { int l = Of(st); if (l >= 0 && n > 0) _lines[l] += n; }
            catch (Exception e) { Stumble("NoteLine", e); }
        }

        /// <summary>Linia doby (OreLedger.Line dla zelaza, po linii 214); zeruje liczniki doby.</summary>
        internal static void Daily(ItemObject iron, int day)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.LogEnabled || iron == null || !Build()) { ClearDay(); return; }
                var inv = CultureInfo.InvariantCulture;
                var use = new float[2]; var towns = new int[2]; var castles = new int[2]; var empty = new int[2]; var surplus = new long[2]; var srcN = new int[2];
                MaterialOrders.ForEachOreUseToday((t, v) => { int l = t != null ? Of(t.Settlement) : -1; if (l >= 0) use[l] += v; });
                foreach (var t in Town.AllFiefs)
                {
                    var st = t != null ? t.Settlement : null;
                    if (st == null || st.ItemRoster == null) continue;
                    int l = Of(st);
                    if (l < 0) continue;
                    int n = st.ItemRoster.GetItemNumber(iron);
                    if (!t.IsTown) { castles[l] += n; continue; }
                    towns[l] += n;
                    if (n <= 0) { empty[l]++; continue; }
                    int keep = MaterialOrders.SourceKeepFor(t, iron);
                    if (keep != int.MaxValue && n > keep) { surplus[l] += n - keep; srcN[l]++; }
                }
                var sb = new StringBuilder();
                sb.Append("Zelazo wedlug ladow (P3/P2a): dzien ").Append(day).Append(" - podzial: ").Append(_how).Append(" |");
                for (int l = 0; l < 2; l++)
                {
                    sb.Append(' ').Append(Name[l]).Append(" (miast ").Append(_townsIn[l]).Append("): lupy z kopaln ").Append(_prod[l])
                      .Append(", zuzycie zmierzone ").Append((use[l] + _lines[l]).ToString("0", inv)).Append(" (warsztaty zbrojne, strzelarze i rzemioslo ")
                      .Append(use[l].ToString("0", inv)).Append(", linie towarowe ").Append(_lines[l]).Append("), zapas miast ").Append(towns[l]).Append(", zamkow ").Append(castles[l])
                      .Append(", nadwyzka zrodel-miast ").Append(surplus[l]).Append(" (zrodel ").Append(srcN[l]).Append("), miast bez zelaza ").Append(empty[l]).Append(" z ").Append(_townsIn[l])
                      .Append(l == 0 ? " |" : "");
                }
                sb.Append(" | potkniecia ").Append(_stumbles).Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Daily", e); }
            ClearDay();
        }
    }
}
