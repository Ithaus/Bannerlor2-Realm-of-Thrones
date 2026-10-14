using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// GL-2 (P2 a) i GL-3 (P3) - GLOWY KIESY LUDU OD LUDZI (projekt docs/gospodarka-od-ludzi rozdz. 3.3; klucze MarketByPeople, VillageHeadsByPeople - PeopleMarket.cs).
    /// Glowy BK z kalibracji (HW/HR/HP/HS, w zapisie arm_townfolk) zostaja - zmienia sie tylko mnoznik:
    ///  - miasto: glowy = srednia swiata glow BK miast x m_t / mianownik, w proporcjach klas BK tego miasta (Scale = srednia x m_t / mianownik / glowy BK miasta);
    ///    dotad r = ludzie WLASNI osady dzis / przy kalibracji (BookScale) - miasto bez ludzi wlasnych mialo glowy BK bez zmian;
    ///  - wies: glowy (oplata stala, zakupy w miescie targowym, prog daniny) = srednia swiata glow BK wsi x skala DOMU wsi (bez uchodzcow - recenzja kodu D2:
    ///    kto uciekl, nie placi i nie kupuje, jak w rynku miasta) / mianownik;
    ///  - zamek (podzamcze): glowy = srednia glow podzamczy x skala podzamcza (ludzie w domu wsi zamku) / mianownik.
    /// Mianownik (recenzja obiegu D1) = srednia skala osad pod regula z pierwszej pamieci po kalibracji (PeopleScale.HeadNorm, w zapisie) - suma swiata glow
    /// w dniu kalibracji = glowy BK osad pod regula (wsie bez tego +1.9%: Dar poza regula, a w odniesieniu sredniej wsi).
    /// Srednie liczone raz na dobe z osad pod regula (Try* = true) - glowy BK sa zamrozone od kalibracji, wiec srednie stoja; suma swiata glow w dniu kalibracji
    /// skal = srednia x suma skal (= liczba osad, z dokladnoscia granic i wyjatku Strazy). Try* = false (Straz, wies bez przelicznika, brak pamieci skal) -
    /// dzisiejszy wzor. Tylko watek glowny (jak cala kiesa).
    /// </summary>
    internal static partial class TownFolk
    {
        private static int _hDay = int.MinValue;
        private static double _hT, _hV, _hC;                // srednie glow BK: miasta (W+R+P+niewolni), wsie (wolni + rzemieslnicy), podzamcza

        private static void HeadMeans()
        {
            int day = (int)CampaignTime.Now.ToDays;
            if (day == _hDay) return;
            _hDay = day;
            double sT = 0, sV = 0, sC = 0; int nT = 0, nV = 0, nC = 0;
            foreach (var f in _f.Values)
            {
                if (f == null || f.St == null || !f.Cal) continue;
                try
                {
                    float k;
                    if (f.Kind == 0)
                    {
                        double h = f.HW + f.HR + f.HP + f.HS;
                        if (h > 0 && f.St.Town != null && PeopleScale.TryMarket(f.St.Town, out k)) { sT += h; nT++; }
                    }
                    else if (f.Kind == 2)
                    {
                        double h = f.HW + f.HR;
                        if (h > 0 && f.St.Village != null && PeopleScale.TryVillage(f.St.Village, out k)) { sV += h; nV++; }
                    }
                    else if (f.Kind == 1)
                    {
                        if (f.HW > 0 && PeopleScale.TryCastleWard(f.St, out k)) { sC += f.HW; nC++; }
                    }
                }
                catch (Exception e) { Stumble("HeadMeans", e); }
            }
            _hT = nT > 0 ? sT / nT : 0; _hV = nV > 0 ? sV / nV : 0; _hC = nC > 0 ? sC / nC : 0;
        }

        /// <summary>GL-2 (a) / GL-3: mnoznik glow BK osady wedlug ludzi (miasto - rynek, wies - okreg). false - dzisiejszy wzor (BookScale).</summary>
        private static bool PeopleHeads(Folk f, out double r)
        {
            r = 1.0;
            if (f == null || f.St == null) return false;
            if (f.Kind == 0)
            {
                if (!PeopleMarket.MarketOn) return false;
                var t = f.St.Town;
                double h = f.HW + f.HR + f.HP + f.HS;
                float m;
                if (t == null || !(h > 0) || !PeopleScale.TryMarket(t, out m)) return false;
                HeadMeans();
                if (!(_hT > 0)) return false;
                r = _hT * m / PeopleScale.HeadNorm('t') / h;
                return !(double.IsNaN(r) || double.IsInfinity(r));
            }
            if (f.Kind == 2)
            {
                double v, h = f.HW + f.HR;
                if (!(h > 0) || !VillPeopleHeads(f, out v)) return false;
                r = v / h;
                return !(double.IsNaN(r) || double.IsInfinity(r));
            }
            return false;
        }

        /// <summary>GL-3 (P3): glowy wsi = srednia swiata glow BK wsi x skala domu wsi / mianownik. false - dzisiejszy wzor (klucz wylaczony, Straz, wies bez przelicznika).</summary>
        private static bool VillPeopleHeads(Folk f, out double heads)
        {
            heads = 0;
            if (f == null || f.Kind != 2 || !PeopleMarket.HeadsOn || f.St == null || f.St.Village == null) return false;
            float u;
            if (!PeopleScale.TryVillageHome(f.St.Village, out u)) return false;
            HeadMeans();
            if (!(_hV > 0)) return false;
            heads = _hV * u / PeopleScale.HeadNorm('w');
            return !(double.IsNaN(heads) || double.IsInfinity(heads));
        }

        /// <summary>GL-3 (P3): glowy podzamcza = srednia glow podzamczy x skala podzamcza / mianownik; inaczej glowy BK z kalibracji (f.HW) jak dotad.</summary>
        private static double CastleHeads(Folk f)
        {
            if (f == null) return 0;
            if (f.Kind != 1 || !PeopleMarket.HeadsOn || f.St == null) return f.HW;
            float w;
            if (!PeopleScale.TryCastleWard(f.St, out w)) return f.HW;
            HeadMeans();
            if (!(_hC > 0)) return f.HW;
            double h = _hC * w / PeopleScale.HeadNorm('z');
            return double.IsNaN(h) || double.IsInfinity(h) ? f.HW : h;
        }

        /// <summary>Linia "Rynek od ludzi (GL2)": glowy swiata dzisiejszym wzorem (BK x ludzie ksiegi / przy kalibracji; podzamcze BK) i wedlug ludzi.</summary>
        internal static void HeadsWorld(out double tOld, out double tNew, out double vOld, out double vNew, out double cOld, out double cNew)
        {
            tOld = tNew = vOld = vNew = cOld = cNew = 0;
            if (_pending != null) EnsureImported();
            foreach (var f in _f.Values)
            {
                if (f == null || !f.Cal) continue;
                try
                {
                    if (f.Kind == 0) { double h = f.HW + f.HR + f.HP + f.HS; tOld += h * BookScale(f); tNew += h * Scale(f); }
                    else if (f.Kind == 2) { vOld += Math.Max(0, f.HW + f.HR) * BookScale(f); vNew += VillHeads(f, true); }
                    else if (f.Kind == 1) { cOld += f.HW; cNew += CastleHeads(f); }
                }
                catch (Exception e) { Stumble("HeadsWorld", e); }
            }
        }

        /// <summary>Wiersz miasta do tabeli tygodnia "Rynek od ludzi wedlug krolestw (GL2)" - liczby ostatniej doby kiesy czynnej.</summary>
        internal sealed class MarketTown { public Settlement St; public double Spend, Need, Aff, Purse; }

        internal static List<MarketTown> MarketTowns()
        {
            var l = new List<MarketTown>();
            if (_pending != null) EnsureImported();
            foreach (var f in _f.Values)
            {
                if (f == null || f.Kind != 0 || !f.Cal || f.St == null || f.St.Town == null) continue;
                try
                {
                    double need = f.Need[0] + f.Need[1] + f.Need[2];
                    l.Add(new MarketTown { St = f.St, Spend = Math.Max(0, f.DWg), Need = need, Aff = double.IsNaN(f.DAff) ? 1 : f.DAff, Purse = f.KW + f.KR + f.KP });
                }
                catch (Exception e) { Stumble("MarketTowns", e); }
            }
            return l;
        }

        /// <summary>
        /// Zloto startowe wedlug ludzi (PeopleMarket.DoStart, tylko nowa kampania, po darach startowych R5): kiesy W/R/P miast pod regula przelane miedzy miastami
        /// wedlug koszyka klasy x m_t (ten sam wzor co dar startowy SeedTown, z glowami wedlug ludzi rynku: glowy BK klasy x m_t / glowy BK miasta), kazda klasa
        /// osobno, suma klasy co do zlotowki ta sama (kiesy ludu swiata bez zmian - kontrola zamknieta kies i ksiega pieniadza tego nie widza). Zwraca na miasto
        /// [kiesy przed, po]; f0/f1 - sumy klas przed / po.
        /// </summary>
        internal static Dictionary<Settlement, long[]> StartFolkShift(out long[] f0, out long[] f1, out long moved, out int towns)
        {
            f0 = new long[3]; f1 = new long[3]; moved = 0; towns = 0;
            var per = new Dictionary<Settlement, long[]>();
            if (_pending != null) EnsureImported();
            var fs = new List<Folk>(); var ms = new List<double>();
            foreach (var f in _f.Values)
            {
                if (f == null || f.Kind != 0 || f.NoBk || !(f.Cal || f.BkCal) || f.St == null || f.St.Town == null) continue;
                float m;
                double h = f.HW + f.HR + f.HP + f.HS;
                if (!(h > 0) || !PeopleScale.TryMarket(f.St.Town, out m)) continue;   // Straz i miasto bez skali - kiesy zostaja
                fs.Add(f); ms.Add(m / h);
            }
            towns = fs.Count;
            if (fs.Count == 0) return per;
            var before = new long[fs.Count];
            for (int i = 0; i < fs.Count; i++) before[i] = fs[i].KW + fs[i].KR + fs[i].KP;
            for (int c = 0; c < 3; c++)
            {
                var cur = new long[fs.Count]; var w = new double[fs.Count];
                for (int i = 0; i < fs.Count; i++)
                {
                    var f = fs[i];
                    double b;
                    if (c == 0) b = f.HW * BW;
                    else if (c == 1) b = f.HR * BR + (f.Slave ? f.HS * BS * OwnR : 0);
                    else b = f.HP * BP + (f.Slave ? f.HS * BS * OwnP : 0);
                    w[i] = Math.Max(0, b) * ms[i];
                    cur[i] = c == 0 ? f.KW : c == 1 ? f.KR : f.KP;
                }
                var t = PeopleMarket.Spread(cur, w);
                for (int i = 0; i < fs.Count; i++)
                {
                    var f = fs[i];
                    f0[c] += cur[i]; f1[c] += t[i];
                    if (t[i] > cur[i]) moved += t[i] - cur[i];
                    if (c == 0) f.KW = t[i]; else if (c == 1) f.KR = t[i]; else f.KP = t[i];
                }
            }
            for (int i = 0; i < fs.Count; i++) per[fs[i].St] = new[] { before[i], fs[i].KW + fs[i].KR + fs[i].KP };
            return per;
        }

        /// <summary>
        /// Zloto startowe wedlug ludzi, pula podzamczy (recenzja obiegu GL D4; PeopleMarket.StartGoldCastles, ta sama doba co pozostale pule, PO darze zamkow
        /// CastlePurse.TrimStartGift / SeedCastle): kiesy podzamczy (KW) pod regula przelane miedzy zamkami wedlug glow podzamcza (skala podzamcza - ten sam wzor
        /// co dar startowy SeedCastle: dni x glowy x koszyk), suma co do zlotowki ta sama. Zwraca na zamek [kiesa przed, po]; c0/c1 - sumy.
        /// </summary>
        internal static Dictionary<Settlement, long[]> StartCastleShift(out long c0, out long c1, out long moved, out int castles)
        {
            c0 = c1 = moved = 0; castles = 0;
            var per = new Dictionary<Settlement, long[]>();
            if (_pending != null) EnsureImported();
            var fs = new List<Folk>(); var ws = new List<double>();
            foreach (var f in _f.Values)
            {
                if (f == null || f.Kind != 1 || f.NoBk || !(f.Cal || f.BkCal) || f.St == null || !f.St.IsCastle) continue;
                float w;
                if (!PeopleScale.TryCastleWard(f.St, out w)) continue;   // Straz i zamek bez skali - kiesa zostaje
                fs.Add(f); ws.Add(Math.Max(0f, w));
            }
            castles = fs.Count;
            if (fs.Count == 0) return per;
            var cur = new long[fs.Count];
            for (int i = 0; i < fs.Count; i++) cur[i] = fs[i].KW;
            var t = PeopleMarket.Spread(cur, ws.ToArray());
            for (int i = 0; i < fs.Count; i++)
            {
                c0 += cur[i]; c1 += t[i];
                if (t[i] > cur[i]) moved += t[i] - cur[i];
                fs[i].KW = t[i];
                per[fs[i].St] = new[] { cur[i], t[i] };
            }
            return per;
        }
    }
}
