using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// 360a (Jeff 08.11 "rob obie"; sledztwo Ashemark Mines) - RECE PRZY ZLOZACH BEZ CELU POLKI ZA CENA WZGLEDNA.
    ///
    /// Przyczyna: RegaleCycle (co 28 dob od D) mierzyl BEZWZGLEDNY indeks ceny towaru w miescie targowym (> 1.2 rece x1.1, &lt; 0.8 x0.9). Pod KS mediana indeksu
    /// wszystkich 33 towarow miast to ok. 3.2 (W i E) - srebro (3.0-3.5) i zloto zawsze "drogie", wiec rece rosly 10% co cykl, procent skladany: 33 cykle od D,
    /// 1.1^33 ~ 23 - przy srebrze 21674 rak wobec planu ok. 0.8 tys. (log 2026-11-07_23-29-50, doba 109626). Nadwyzka stoi na wozach wsi (21990 srebra), polki
    /// miast jej nie widza.
    ///
    /// Zmiana (klucz Mine Hands Follow Relative Price): dla srebra, zlota, gliny, wapienia, marmuru i przypraw (zloza bez celu polki) prog bierze indeks towaru
    /// podzielony przez mediane indeksu wszystkich towarow tego samego miasta targowego (ta sama tablica px). Progi i kroki bez zmian, sufit bez zmian.
    /// Zelazo (f5-ruda), sol i daktyle (f20-d4) - wlasne reguly; drewno - bez zmian (indeks wzgledny ok. 0.3 cialby rece dymarek i bud - osobna decyzja).
    /// Klucz NIE - RegaleCycle jak dotad co do bitu. RF jest w zapisie KS (arm_ks) - bez nowego klucza zapisu; mediana liczona w kazdym cyklu od nowa.
    /// Linia "Kopalnie (360a)" tylko w dobie cyklu (RF zmienia sie wylacznie wtedy).
    /// </summary>
    internal static partial class StartCalibration
    {
        internal static bool RegRelOn { get { var s = Settings.Current; return s != null && s.KsRegaleRelativePrice; } }

        private static readonly int[] RelCrafts = { KsTab.CSilver, KsTab.CGoldM, KsTab.CClay, KsTab.CLime, KsTab.CQuarry, KsTab.CSpice };

        private static bool RelGood(int craft)
        {
            for (int i = 0; i < RelCrafts.Length; i++) if (RelCrafts[i] == craft) return true;
            return false;
        }

        // pamiec cyklu: mediana indeksu miasta (klucz - tablica px miasta), liczniki linii wedlug towaru KS
        private static readonly Dictionary<double[], double> _relMed = new Dictionary<double[], double>();
        private static readonly List<double> _relTmp = new List<double>(ItemCount);
        private static readonly int[] _relN = new int[KsTab.NG], _relUp = new int[KsTab.NG], _relDown = new int[KsTab.NG];
        private static readonly double[] _relH0 = new double[KsTab.NG], _relH1 = new double[KsTab.NG];
        private static readonly List<double>[] _relR = new List<double>[KsTab.NG];
        private static int _relStum;

        /// <summary>RuleDaily, doba cyklu regali: pamiec mediany i liczniki od zera.</summary>
        private static void RelBegin()
        {
            _relMed.Clear();
            for (int g = 0; g < KsTab.NG; g++) { _relN[g] = _relUp[g] = _relDown[g] = 0; _relH0[g] = _relH1[g] = 0; if (_relR[g] != null) _relR[g].Clear(); }
        }

        /// <summary>Mediana indeksu wszystkich towarow miasta (raz na miasto na cykl).</summary>
        private static double RelMed(double[] px)
        {
            double md;
            if (_relMed.TryGetValue(px, out md)) return md;
            _relTmp.Clear();
            for (int i = 0; i < px.Length && i < ItemCount; i++) _relTmp.Add(px[i]);
            md = _relTmp.Count > 0 ? Med(_relTmp) : 0.0;
            _relMed[px] = md;
            return md;
        }

        private static void RelNoteBefore(int g, LiveBlock b, int j, double r, int dir)
        {
            if (g < 0 || g >= KsTab.NG) return;
            _relN[g]++;
            if (dir > 0) _relUp[g]++; else if (dir < 0) _relDown[g]++;
            _relH0[g] += b.Hands * b.RF[j];
            if (_relR[g] == null) _relR[g] = new List<double>();
            _relR[g].Add(r);
        }

        private static void RelNoteAfter(LiveBlock b)
        {
            for (int j = 0; j < b.RC.Length && j < b.RF.Length; j++)
            {
                if (!RelGood(b.RC[j])) continue;
                int g = KsTab.RegGood(b.RC[j]);
                if (g >= 0 && g < KsTab.NG) _relH1[g] += b.Hands * b.RF[j];
            }
        }

        /// <summary>Linia "Kopalnie (360a)" w dobie cyklu: wedlug towaru wpisow, mediana miast W/E (mediana indeksu wszystkich towarow miasta), mediana indeksu wzglednego
        /// towaru, w gore / w dol / bez zmian, rece przy zlozu przed -> po kroku i suficie.</summary>
        private static void RelLine(int today)
        {
            var mw = new List<double>(); var me = new List<double>();
            foreach (var kv in _px)
            {
                if (kv.Value == null || kv.Key == null) continue;
                double md = RelMed(kv.Value);
                int m = TownMarket(kv.Key);
                if (m == 0) mw.Add(md); else if (m == 1) me.Add(md);
            }
            var sb = new StringBuilder(1200);
            sb.Append("Kopalnie (360a): dzien ").Append(today).Append(" | cykl co ").Append(RegCycle).Append(" dob od D ").Append(DDay)
              .Append(" | prog: indeks towaru / mediana indeksu wszystkich towarow miasta targowego > 1.2 - rece +10%, < 0.8 - rece -10% (zelazo, sol, daktyle, drewno - bez zmian)")
              .Append(" | mediana miast W/E ").Append(mw.Count > 0 ? Med(mw).ToString("0.00", Inv) : "-").Append('/').Append(me.Count > 0 ? Med(me).ToString("0.00", Inv) : "-");
            foreach (int c in RelCrafts)
            {
                int g = KsTab.RegGood(c);
                if (g < 0 || g >= KsTab.NG) continue;
                int n = _relN[g];
                sb.Append(" | ").Append(KsTab.Goods[g]).Append(": wpisow ").Append(n);
                if (n == 0) continue;
                sb.Append(", indeks wzgledny mediana ").Append(_relR[g] != null && _relR[g].Count > 0 ? Med(_relR[g]).ToString("0.00", Inv) : "-")
                  .Append(", w gore ").Append(_relUp[g]).Append(" / w dol ").Append(_relDown[g]).Append(" / bez zmian ").Append(n - _relUp[g] - _relDown[g])
                  .Append(", rece ").Append(_relH0[g].ToString("0", Inv)).Append(" -> ").Append(_relH1[g].ToString("0", Inv));
            }
            sb.Append(" | potkniecia ").Append(_relStum).Append('.');
            Log.Info(sb.ToString());
        }
    }
}
