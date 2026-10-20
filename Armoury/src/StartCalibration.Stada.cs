using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// R3-06c / R3-06d (noc 20.10, galaz p5-stada, poprawki po recenzji): wpiecia stada wsi (VillageHerds) i zmeczonej ziemi (TiredLand) w rachunek okregu KS
    /// (ComputeDistrict z czynnikami - tylko gra, watek glowny LiveDaily; Plan, T1 i linie Planu licza bez czynnikow) i w bramke krainy KS (ks-arm, f26).
    ///  - HerdKs: stado zaraz po Produce, PRZED dodatkiem prac okregu (stado kupione w pracach okregu to osobny majatek - DistrictWorks sam pedzi je przy rabunku
    ///    i tnie jednostki prac; dotad obrywalo drugi raz przez H stada wsi). Zapamietuje ubytek stada w kg (przed zima i rekami).
    ///  - LandKs: kopia plonu bez stada (plon + ubytek stada x czynnik zimy i rak f - mnozniki sa mnozne) i bez ziemi, potem TiredLand.KsStep.
    ///  - HerdLandOwn: kcal wlasnego stolu okregu z kopii (KsDeliver.Own na kopii) minus kcal wlasnego stolu po stadzie i ziemi = kcal zabrane przez stado i ziemie.
    ///  - HerdLandFormula (NoteGranary): bramka krainy porownuje racje KS ze wzorem gry, a wzor gry nie zna ani stad, ani ziemi. Wzor gry dostaje ten sam ubytek racji
    ///    (ludzie w domu x min(1.5, kcal / potrzeba) - ten sam wzor racji co LiveDaily i FloorDaily). Glod po rabunku stada albo z ludnosci ponad ziemie nie jest wiec
    ///    "glodem z winy KS" i nie oddaje krainy regulom gry (inaczej powrot wylaczalby KS - a z nim stada i ziemie - dokladnie tam, gdzie te zasady maja dzialac).
    ///    Ta sama poprawka trafia do luki strat A/C krainy (wzor gry - racje KS) i do sumy wzoru gry krainy.
    ///    Przyblizenie: koszyk ZC4 (HomeKcal - jedzenie robione w domu) liczony jest z wlasnym stolem PO stadzie i ziemi; kopia go nie przelicza.
    /// Tylko watek glowny (bez blokad). Stan czyszczony z VillageHerds.Reset (nowa kampania, wczytanie zapisu).
    /// </summary>
    internal static partial class StartCalibration
    {
        private static double[] _hlNo, _hlDh, _hlOwn;          // kopia plonu bez stada i ziemi; ubytek stada (kg, przed zima i rekami); wlasny stol kopii
        private static LiveDist _hlLd;                         // okreg biezacego rachunku (HerdKs -> LandKs -> HerdLandOwn)
        private static bool _hlHerd, _hlCut;                   // stado obnizylo plon (HerdKs); stado albo ziemia obnizyly plon (LandKs)
        private static readonly Dictionary<Village, float> _hlKcal = new Dictionary<Village, float>();   // kcal/d wlasnego stolu wsi zabrane przez stado i ziemie
        private static int _hlStum, _hlDStum, _hlGateV, _hlGateFlip;
        private static double _hlGateLost, _hlGateF;

        internal static void HerdLandReset()
        {
            _hlKcal.Clear(); _hlLd = null; _hlHerd = _hlCut = false;
            _hlStum = _hlDStum = _hlGateV = _hlGateFlip = 0; _hlGateLost = _hlGateF = 0;
        }

        private static void HlStumble(string where, Exception e)
        {
            _hlStum++; _hlDStum++;
            if (_hlStum <= 3) { try { Log.Error("StartCalibration." + where, e); } catch { } }
        }

        /// <summary>ComputeDistrict (z czynnikami, wies): stado wsi zaraz po Produce, przed pracami okregu; ubytek stada w kg do kopii plonu bez stada.</summary>
        private static void HerdKs(LiveDist ld, double[] prod, double[] kgT)
        {
            _hlLd = null; _hlHerd = _hlCut = false;
            if (ld == null || ld.V == null || prod == null || !VillageHerds.KeyOn) return;
            try
            {
                if (_hlDh == null || _hlDh.Length != prod.Length) _hlDh = new double[prod.Length];
                Array.Copy(prod, _hlDh, prod.Length);
                VillageHerds.KsStep(ld.V, prod, kgT);                  // wlasny try w srodku
                for (int i = 0; i < prod.Length; i++)
                {
                    double dl = _hlDh[i] - prod[i];
                    if (dl > 1e-12) { _hlDh[i] = dl; _hlHerd = true; } else _hlDh[i] = 0.0;
                }
                _hlLd = ld;
            }
            catch (Exception e) { HlStumble("HerdKs", e); _hlLd = null; _hlHerd = false; }
        }

        /// <summary>ComputeDistrict (z czynnikami, po zimie i rekach f): kopia plonu bez stada i ziemi, potem zmeczona ziemia (TiredLand).</summary>
        private static void LandKs(LiveDist ld, double[] prod, double f)
        {
            bool herd = _hlLd == ld && _hlHerd && _hlDh != null;
            bool copy = false;
            try
            {
                if (_hlNo == null || _hlNo.Length != prod.Length) _hlNo = new double[prod.Length];
                for (int i = 0; i < prod.Length; i++) _hlNo[i] = prod[i] + (herd ? _hlDh[i] * f : 0.0);
                copy = true;
            }
            catch (Exception e) { HlStumble("LandKs", e); }
            TiredLand.KsStep(ld.Id, ld.B, prod);                         // wlasny try w srodku
            _hlLd = copy ? ld : null; _hlHerd = _hlCut = false;
            if (!copy) return;
            for (int i = 0; i < prod.Length; i++) if (prod[i] < _hlNo[i] - 1e-9) { _hlCut = true; break; }
        }

        /// <summary>ComputeDistrict (z czynnikami, wies; zaraz po KsDeliver.Own): kcal wlasnego stolu zabrane przez stado i ziemie (kopia bez nich - Own kopii).</summary>
        private static void HerdLandOwn(LiveDist ld, double ownKcal, bool fix)
        {
            if (ld == null || ld.V == null) return;
            try
            {
                if (_hlLd != ld || !_hlCut || _hlNo == null) { _hlKcal.Remove(ld.V); return; }
                if (_hlOwn == null || _hlOwn.Length != _hlNo.Length) _hlOwn = new double[_hlNo.Length];
                double own0 = KsDeliver.Own(_hlNo, ld.OwnNeed, _hlOwn, fix);   // na kopii (Own dopisuje skory uboju do kopii - bez znaczenia)
                double dk = own0 - ownKcal;
                if (dk > 1e-6 && !double.IsNaN(dk) && !double.IsInfinity(dk)) _hlKcal[ld.V] = (float)dk; else _hlKcal.Remove(ld.V);
            }
            catch (Exception e) { HlStumble("HerdLandOwn", e); }
            finally { _hlLd = null; _hlHerd = _hlCut = false; }
        }

        /// <summary>NoteGranary (bramka krainy, stan zastosowany): wzor gry minus racje zabrane przez stado i ziemie (najmniej 0). Bez wpisu - wzor bez zmian.</summary>
        private static double HerdLandFormula(Village v, double formula, double given)
        {
            float dk;
            if (v == null || _hlKcal.Count == 0 || !_hlKcal.TryGetValue(v, out dk) || !(dk > 0f)) return formula;
            try
            {
                var st = v.Settlement; LiveDist ld;
                if (st == null || st.StringId == null || !_live.TryGetValue(st.StringId, out ld) || ld == null || ld.Res == null) return formula;
                double need = ld.Res.KcalNeed, ppl = ld.PeopleHome;
                if (!(need > 0) || !(ppl > 0) || !(ld.Rations >= 0)) return formula;
                double r = Math.Min(1.5, ld.Rations / ppl);
                double lost = ppl * (Math.Min(1.5, r + dk / need) - r);
                if (!(lost > 0) || double.IsNaN(lost) || double.IsInfinity(lost)) return formula;
                double f1 = Math.Max(0.0, formula - lost);
                _hlGateV++; _hlGateLost += Math.Min(lost, Math.Max(0.0, formula)); _hlGateF += Math.Max(0.0, formula);
                if (given < formula - 0.5 && !(given < f1 - 0.5)) _hlGateFlip++;     // racje KS ponizej wzoru gry tylko przez stado i ziemie
                return f1;
            }
            catch (Exception e) { HlStumble("HerdLandFormula", e); return formula; }
        }

        /// <summary>Dopisek linii "Stada (R3-06c)": wsie z kcal zabranymi przez stado i ziemie, pomiary bramki od ostatniej linii.</summary>
        internal static string HerdLandGateText()
        {
            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder(220);
            sb.Append("wsi z kcal wlasnego stolu zabranymi przez stado i ziemie ").Append(_hlKcal.Count)
              .Append(", wzor gry obnizony o ten sam ubytek we wsiach ").Append(_hlGateV)
              .Append(" (racje ").Append(_hlGateLost.ToString("0", inv)).Append(" z ").Append(_hlGateF.ToString("0", inv)).Append(" wzoru gry")
              .Append(_hlGateF > 0 ? ", " + (100 * _hlGateLost / _hlGateF).ToString("0.0", inv) + "%" : "").Append(')')
              .Append(", wsi nie liczonych jako racje KS ponizej wzoru gry (stado i ziemia, nie KS) ").Append(_hlGateFlip)
              .Append(", potkniecia ").Append(_hlDStum).Append(" (sesja ").Append(_hlStum).Append(')');
            return sb.ToString();
        }

        internal static void HerdLandGateDayClose() { _hlGateV = _hlGateFlip = _hlDStum = 0; _hlGateLost = _hlGateF = 0; }
    }
}
