using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;

namespace Armoury
{
    /// <summary>
    /// R3-06d (noc 20.10, galaz p5-stada; POMYSLY-35 poz. 15, audyt 02 rozdz. 2.1 i E pkt 5, 10-pomysly F6) - UGOR I ZMECZONA ZIEMIA. Klucz TiredLandYield.
    /// KS liczy plon okregu liniowo od rak (rece bloku x P0 x przydatnosc ziemi), a ziemie zajeta bloku (KsPart.LandUse: dom, zboze wlasne, rece zajec x ha na pracujacego
    /// - ha zajec juz z ugorem trojpolowki) tylko pilnuje regula zmiany zajec. Gdy ludzi przybywa, ziemia zajeta rosnie ponad ziemie okregu i kazdy chlop dawal dotad tyle samo.
    /// Odtad (KS, gra - ComputeDistrict z czynnikami; Plan bez zmian): rho = ziemia zajeta okregu / ziemia okregu (suma blokow). Baza okregu b = max(1, rho przy pierwszym
    /// rachunku w grze) - w zapisie (start neutralny: okreg, ktory na starcie juz siedzial na calej ziemi, nie traci ani grama). Gdy rho > b, towary ziemi (zboze, owoce,
    /// warzywa, winogrona, oliwki, daktyle, przyprawy, len, bawelna, welna, nabial i zywiec) x (b / rho)^Elast - kazdy chlop ponad ziemie dorzuca mniej niz poprzedni
    /// (obsiewany ugor meczy ziemie; plon calego okregu rosnie dalej, ale wolniej niz ludzie: rece^0.4). Nie dotyczy: jaj i miodu (dom), ryb, soli, drewna, kopalin,
    /// futer, wielorybow, dziczyzny. Najmniej MinFactor. Wies poza KS (lancuch gry) - bez zmian (lancuch gry nie zna ziemi okregu; dzis KS obejmuje wszystkie wsie).
    /// Zapis: klucz arm_tiredland ("v1|id:baza;..."); brak klucza (stary zapis) - baza z pierwszego rachunku po wczytaniu. Tylko watek glowny (LiveDaily).
    /// </summary>
    internal static class TiredLand
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const double Elast = 0.6;            // czynnik na glowe (b / rho)^0.6 = plon okregu ~ rece^0.4 ponad ziemie
        private const double MinFactor = 0.5;        // najmniej polowa plonu na glowe
        private static readonly int[] LandItems =
        {
            KsDeliver.IGrain, KsDeliver.ICheese, KsDeliver.IButter, KsDeliver.IApple, KsDeliver.ICarrot, KsDeliver.IOrange, KsDeliver.IGrape, KsDeliver.IOlives,
            KsDeliver.IDate, KsDeliver.ISpice, KsDeliver.IWool, KsDeliver.IFlax, KsDeliver.ICotton, KsDeliver.ICow, KsDeliver.IHog, KsDeliver.ISheep
        };

        internal static bool KeyOn { get { var s = Settings.Current; return s != null && s.TiredLandYield; } }

        private sealed class Dist { public float Base; public float Rho; public float F = 1f; public int Day; }
        private static readonly Dictionary<string, Dist> _d = new Dictionary<string, Dist>(StringComparer.Ordinal);
        private static int _stumbles, _dStumbles, _fromSave, _newBase;

        internal static void Reset() { _d.Clear(); _stumbles = _dStumbles = _fromSave = _newBase = 0; }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _dStumbles++;
            if (_stumbles <= 3) { try { Log.Error("TiredLand." + where, e); } catch { } }
        }

        /// <summary>StartCalibration.ComputeDistrict (gra, z czynnikami; po Produce, zimie i rekach): towary ziemi okregu x (b / rho)^Elast, gdy rho > b.</summary>
        internal static void KsStep(string id, KsPart[] parts, double[] prod)
        {
            if (id == null || parts == null || prod == null || !KeyOn) return;
            try
            {
                double lu = 0, area = 0;
                for (int i = 0; i < parts.Length; i++)
                {
                    var p = parts[i];
                    if (p == null || p.K == null || p.C == null) continue;
                    if (p.LandUse > 0 && !double.IsNaN(p.LandUse) && !double.IsInfinity(p.LandUse)) lu += p.LandUse;
                    if (p.K.Area > 0) area += p.K.Area;
                }
                if (!(area > 0) || !(lu > 0)) return;
                double rho = lu / area;
                Dist d;
                if (!_d.TryGetValue(id, out d)) { d = new Dist { Base = (float)Math.Max(1.0, rho) }; _d[id] = d; _newBase++; }
                d.Rho = (float)rho; d.Day = (int)CampaignTime.Now.ToDays;
                double f = rho > d.Base ? Math.Max(MinFactor, Math.Pow(d.Base / rho, Elast)) : 1.0;
                d.F = (float)f;
                if (f >= 0.9999) return;
                foreach (int it in LandItems) prod[it] *= f;
            }
            catch (Exception e) { Stumble("KsStep", e); }
        }

        internal static void Daily()
        {
            int day = (int)CampaignTime.Now.ToDays - 1;
            if (!LogGate.Lines) { _dStumbles = 0; return; }
            int n = _d.Count, over = 0, cut = 0; double sumF = 0, minF = 1; string minId = null; double rhoMax = 0; string rhoId = null;
            var rhos = new List<float>(n);
            foreach (var kv in _d)
            {
                var d = kv.Value;
                rhos.Add(d.Rho);
                if (d.Rho > d.Base) over++;
                if (d.F < 0.9999f) { cut++; sumF += d.F; if (d.F < minF) { minF = d.F; minId = kv.Key; } }
                if (d.Rho > rhoMax) { rhoMax = d.Rho; rhoId = kv.Key; }
            }
            rhos.Sort();
            var sb = new StringBuilder(400);
            sb.Append("Zmeczona ziemia (R3-06d): dzien ").Append(day).Append(KeyOn ? "" : " | KLUCZ Tired Land Yield WYLACZONY (czynnik 1)")
              .Append(" | okregi KS z ziemia ").Append(n).Append(" (baza z zapisu ").Append(_fromSave).Append(", nowa baza w sesji ").Append(_newBase).Append(')')
              .Append(" | ziemia zajeta / ziemia okregu: mediana ").Append(n > 0 ? rhos[n / 2].ToString("0.00", Inv) : "-").Append(", najwiecej ").Append(rhoMax.ToString("0.00", Inv)).Append(" (").Append(rhoId ?? "-").Append(')')
              .Append(" | ponad baze ").Append(over).Append(", plon na glowe nizszy w ").Append(cut).Append(" okregach (srednio x").Append(cut > 0 ? (sumF / cut).ToString("0.000", Inv) : "1").Append(", najnizej x").Append(minF.ToString("0.000", Inv)).Append(' ').Append(minId ?? "-").Append(')')
              .Append(" | potkniecia dzis ").Append(_dStumbles).Append(" (sesja ").Append(_stumbles).Append(").");
            Log.Info(sb.ToString());
            _dStumbles = 0;
        }

        // ------------------------------------------------------------ zapis
        internal static string Export()
        {
            var sb = new StringBuilder(16 + 20 * _d.Count);
            sb.Append("v1|");
            foreach (var kv in _d)
            {
                if (kv.Key.IndexOf(':') >= 0 || kv.Key.IndexOf(';') >= 0) continue;
                sb.Append(kv.Key).Append(':').Append(kv.Value.Base.ToString("0.####", Inv)).Append(';');
            }
            return sb.ToString();
        }

        internal static void Import(string s)
        {
            Reset();
            if (string.IsNullOrEmpty(s) || !s.StartsWith("v1|", StringComparison.Ordinal)) return;
            foreach (var e in s.Substring(3).Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int k = e.LastIndexOf(':'); float b;
                if (k <= 0 || !float.TryParse(e.Substring(k + 1), NumberStyles.Float, Inv, out b) || !(b >= 1f) || b > 100f) continue;
                _d[e.Substring(0, k)] = new Dist { Base = b, Rho = b };
                _fromSave++;
            }
        }

        internal static void RegisterEvents(CampaignBehaviorBase owner)
        {
            CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(owner, _ => { try { Reset(); } catch (Exception e) { Stumble("Reset", e); } });
        }
    }
}
