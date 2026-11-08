using System;
using System.Collections.Generic;
using System.Text;

namespace Armoury
{
    /// <summary>
    /// 360c (Jeff 08.11: "A i czemu nie liczy sie kruszcow na wozach - powinno liczyc sie cale srebro w obiegu gospodarki") - RECE KOPALN SREBRA I ZLOTA
    /// ODDAJA NIESPRZEDANA NADWYZKE RYNKU.
    ///
    /// Przyczyna: swiat wydobywa ok. 1235 srebra/d, zuzywa 359/d (log 2026-11-07_23-29-50, doba 109634); nadwyzka ok. 22 tys. lezy na wozach wsi, a regula regali
    /// (RegaleCycle co 28 dob od D, takze 360a) patrzy tylko na cene miasta targowego - ta widzi polke (ok. 700), nie zapas rynku.
    ///
    /// Zmiana (klucz Mine Hands Shed Unsold Ore, KsRegaleWorldSurplus): w dobie cyklu regali spis srebra i rudy zlota rynku (OreStock360.Census - wsie, wozy wsi,
    /// zamki, polki miast, juki karawan, wozy surowcow) W, srednia 28 dob wydobycia P i zuzycia C rynku z ksiegi towarow (GoodsLedger.KsMarketTake - MkP, MkC)
    /// bez mennicy (CrownMint.MintByMarket; mennica bierze tylko nadwyzke - jak KsCons 311b, inaczej "wiecej wydobycia pod mennice"):
    ///   W > 60 x C i P > 0.9 x C - NADWYZKA: rece -10% (x0.9) niezaleznie od ceny;
    ///   30 x C &lt;= W - bez wzrostu (regula ceny moze tylko obnizyc);  W &lt; 30 x C - regula ceny jak dotad.
    /// Ponizej 7 probek ksiegi albo spis z wyjatkiem - bez zmian. Jeden kierunek na wpis (360a, 360c i regula ceny razem - najwyzej jeden krok x0.9 na cykl,
    /// nigdy 0.81); sufit polowy rak bez zmian. Dziala takze przy wylaczonym 360a. Okno probek tylko w sesji (bez nowego klucza zapisu). Klucz NIE - co do bitu jak dotad.
    /// Linia "Kopalnie (360c)" w dobie cyklu.
    /// </summary>
    internal static partial class StartCalibration
    {
        internal static bool RegSurOn { get { var s = Settings.Current; return s != null && s.KsRegaleWorldSurplus; } }

        private const int SurWin = 28, SurMinN = 7;
        private const double SurHi = 60.0, SurLo = 30.0, SurPk = 0.9;
        private const int Mt = OreStock360.Metals;

        // okno probek doby [pozycja, kruszec, rynek]
        private static readonly long[,,] _surP = new long[SurWin, Mt, 2], _surC = new long[SurWin, Mt, 2], _surMint = new long[SurWin, Mt, 2];
        private static int _surN, _surPos, _surKsDay = -1;
        private static readonly Dictionary<string, long[]> _surTmp = new Dictionary<string, long[]>(StringComparer.Ordinal);
        private static readonly long[,] _surMintTmp = new long[2, Mt];
        // cykl: spis, srednie i decyzja [kruszec, rynek] (0 bez danych, 1 nadwyzka - w dol, 2 pasmo - bez wzrostu, 3 niski zapas - regula ceny)
        private static readonly long[,,] _surW = new long[Mt, 2, OreStock360.HN];
        private static bool _surWOk, _surCycle;
        private static readonly int[,] _surDec = new int[Mt, 2];
        private static readonly double[,] _surPa = new double[Mt, 2], _surCa = new double[Mt, 2], _surMa = new double[Mt, 2];
        private static readonly int[,] _surNe = new int[Mt, 2], _surForced = new int[Mt, 2], _surHeld = new int[Mt, 2];
        private static readonly double[,] _surH0 = new double[Mt, 2], _surH1 = new double[Mt, 2];
        private static int _surStum;
        private static readonly string[] SurDecName = { "bez danych (bez zmian)", "NADWYZKA - w dol", "pasmo - bez wzrostu", "niski zapas - regula ceny" };

        /// <summary>Nowa kampania / wczytanie (StartCalibration.Reset): okno probek od zera.</summary>
        internal static void Sur360Reset()
        {
            Array.Clear(_surP, 0, _surP.Length); Array.Clear(_surC, 0, _surC.Length); Array.Clear(_surMint, 0, _surMint.Length);
            _surN = _surPos = 0; _surKsDay = -1; _surCycle = false; _surStum = 0;
        }

        private static int SurMetal(int g) { return g == KsTab.GSilver ? OreStock360.MSilver : (g == KsTab.GGold ? OreStock360.MGold : -1); }

        /// <summary>RuleDaily co dobe (przed wczesnym wyjsciem K4a): probka doby ksiegi towarow (wydobycie i zuzycie rynku) i mennicy tej samej doby. Tylko odczyt.</summary>
        private static void SurDaily(int today)
        {
            try
            {
                int d = GoodsLedger.KsMarketTake(_surTmp);
                if (d < 0 || d == _surKsDay) return;
                _surKsDay = d;
                // mennica bije w tym samym takcie doby przed zamknieciem ksiegi (ArmouryBehavior: CrownMint.Daily przed GoodsLedger.Daily) - jej sztuki sa w MkC tej doby
                bool mint = CrownMint.MintByMarket(_surMintTmp) == today;
                int pos = _surPos;
                for (int m = 0; m < Mt; m++)
                {
                    long[] a;
                    bool has = _surTmp.TryGetValue(OreStock360.MetalId[m], out a) && a != null && a.Length >= 5;
                    for (int mk = 0; mk < 2; mk++)
                    {
                        _surP[pos, m, mk] = has ? Math.Max(0, a[mk]) : 0;
                        _surC[pos, m, mk] = has ? Math.Max(0, a[3 + mk]) : 0;
                        _surMint[pos, m, mk] = mint ? Math.Max(0, _surMintTmp[mk, m]) : 0;
                    }
                }
                _surPos = (pos + 1) % SurWin;
                if (_surN < SurWin) _surN++;
            }
            catch (Exception e) { _surStum++; Stumble("SurDaily", e); }
        }

        /// <summary>RuleDaily, doba cyklu regali (klucz wlaczony): spis zapasu rynku, srednie okna, decyzja wedlug kruszcu i rynku, liczniki linii od zera.</summary>
        private static void SurBegin()
        {
            _surCycle = true;
            Array.Clear(_surNe, 0, _surNe.Length); Array.Clear(_surForced, 0, _surForced.Length); Array.Clear(_surHeld, 0, _surHeld.Length);
            Array.Clear(_surH0, 0, _surH0.Length); Array.Clear(_surH1, 0, _surH1.Length); Array.Clear(_surDec, 0, _surDec.Length);
            _surWOk = OreStock360.Census(_surW);
            for (int m = 0; m < Mt; m++)
                for (int mk = 0; mk < 2; mk++)
                {
                    double p = 0, c = 0, n = 0;
                    for (int i = 0; i < _surN; i++) { p += _surP[i, m, mk]; c += _surC[i, m, mk]; n += _surMint[i, m, mk]; }
                    double k = Math.Max(1, _surN);
                    _surPa[m, mk] = p / k; _surCa[m, mk] = c / k; _surMa[m, mk] = n / k;
                    if (!_surWOk || _surN < SurMinN) { _surDec[m, mk] = 0; continue; }
                    double cn = Math.Max(0.0, _surCa[m, mk] - _surMa[m, mk]);
                    long w = OreStock360.Sum(_surW, m, mk, 0);
                    if (w > SurHi * cn && _surPa[m, mk] > SurPk * cn) _surDec[m, mk] = 1;
                    else if (w < SurLo * cn) _surDec[m, mk] = 3;
                    else _surDec[m, mk] = 2;
                }
        }

        /// <summary>RegaleCycle (srebro i zloto, doba cyklu z kluczem): kierunek wpisu po decyzji rynku - jeden kierunek na wpis (jeden krok na cykl).</summary>
        private static int SurDir(LiveBlock b, int j, int g, int mk, int dir)
        {
            int m = SurMetal(g);
            if (m < 0 || mk < 0 || mk > 1) return dir;
            _surNe[m, mk]++;
            _surH0[m, mk] += b.Hands * b.RF[j];
            int dec = _surDec[m, mk];
            if (dec == 1) { if (dir >= 0) _surForced[m, mk]++; return -1; }
            if (dec == 2 && dir > 0) { _surHeld[m, mk]++; return 0; }
            return dir;
        }

        /// <summary>RegaleCycle: rece srebra i zlota po kroku i suficie (linia "Kopalnie (360c)").</summary>
        private static void SurNoteAfter(LiveBlock b, int mk)
        {
            if (mk < 0 || mk > 1) return;
            for (int j = 0; j < b.RC.Length && j < b.RF.Length; j++)
            {
                if (!RelGood(b.RC[j])) continue;
                int m = SurMetal(KsTab.RegGood(b.RC[j]));
                if (m >= 0) _surH1[m, mk] += b.Hands * b.RF[j];
            }
        }

        /// <summary>Linia "Kopalnie (360c)" w dobie cyklu: wedlug kruszcu i rynku zapas W z rozbiciem, srednie P, C, mennica, C bez mennicy, W / C, decyzja,
        /// wpisy, wymuszone w dol, wstrzymane wzrosty, rece przed -> po.</summary>
        private static void SurLine(int today)
        {
            _surCycle = false;
            var sb = new StringBuilder(2000);
            sb.Append("Kopalnie (360c): dzien ").Append(today).Append(" | cykl co ").Append(RegCycle).Append(" dob od D ").Append(DDay)
              .Append(" | prog: zapas rynku W > ").Append(SurHi.ToString("0", Inv)).Append(" x C i P > ").Append(SurPk.ToString("0.0", Inv)).Append(" x C - rece -10%; wzrost tylko przy W < ")
              .Append(SurLo.ToString("0", Inv)).Append(" x C (C - zuzycie rynku bez mennicy, srednia ").Append(_surN).Append(" dob z ").Append(SurWin).Append(", min. ").Append(SurMinN).Append(')')
              .Append(" | spis ").Append(_surWOk ? "OK" : "BLAD - bez zmian");
            for (int m = 0; m < Mt; m++)
                for (int mk = 0; mk < 2; mk++)
                {
                    long w = OreStock360.Sum(_surW, m, mk, 0);
                    double cn = Math.Max(0.0, _surCa[m, mk] - _surMa[m, mk]);
                    sb.Append(" | ").Append(OreStock360.MetalId[m]).Append(mk == 0 ? " Westeros" : " Essos").Append(": W ").Append(w).Append(" (");
                    for (int h = 0; h < OreStock360.HN; h++) sb.Append(h > 0 ? ", " : "").Append(OreStock360.HolderName[h]).Append(' ').Append(_surW[m, mk, h]);
                    sb.Append("), P ").Append(_surPa[m, mk].ToString("0.0", Inv)).Append("/d, C ").Append(_surCa[m, mk].ToString("0.0", Inv)).Append("/d (mennica ")
                      .Append(_surMa[m, mk].ToString("0.0", Inv)).Append("/d, bez mennicy ").Append(cn.ToString("0.0", Inv)).Append("/d), W/C ")
                      .Append(cn > 0 ? (w / cn).ToString("0.0", Inv) : "-").Append(", decyzja ").Append(SurDecName[_surDec[m, mk]])
                      .Append(" | wpisow ").Append(_surNe[m, mk]).Append(", wymuszone w dol ").Append(_surForced[m, mk]).Append(", wstrzymane wzrosty ").Append(_surHeld[m, mk])
                      .Append(", rece ").Append(_surH0[m, mk].ToString("0", Inv)).Append(" -> ").Append(_surH1[m, mk].ToString("0", Inv));
                }
            sb.Append(" | potkniecia ").Append(_surStum).Append(" (spis ").Append(OreStock360.Stumbles).Append(")").Append('.');
            Log.Info(sb.ToString());
        }
    }
}
