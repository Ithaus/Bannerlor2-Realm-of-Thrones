using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// 338 grupa 4 - MURARZ (Mason miasta +3/6/9, zamku +2/4/6 punktow budowy na dobe; ConstructionPerDay).
    /// BKConstructionModel:431 czyta efekt, ale BuildFunding.PowerPostfix / PowerIntPostfix (Priority.Last) w osadach objetych rozliczeniem (Covered - kazdy pan AI
    /// i gracz przy PaidConstructionPlayer) ZASTEPUJA wynik oplacona praca - Mason martwy. Budowa w Armoury to oplacona praca, wiec Mason pracuje tylko przy
    /// oplaconej budowie i daje najwyzej swoje punkty z opisu: dopisek = min(punkty Masona, punkty pana + robota ludu) (recenzja 338 W1 - bylo k x praca,
    /// do 10x opisu).
    /// Punkty Masona - tylko budynki SettlementMason / CastleMason (nie AddEffectOfBuildings - punkty kopalni BK spoza opisu nie wracaja).
    /// Czyste (bez stanu) - wolaja to tez dymki. Dymek i postep doby licza ten sam dopisek (bez surowej mocy BK). EngPaidNeed odejmuje dopisek od potrzeby.
    /// Galaz nieobjeta (lenna gracza bez PaidConstructionPlayer) - nic, bo tam BK liczy Masona sam.
    /// </summary>
    // 473-a (Jeff 13.11, odpowiedz 2 na pytania o budynki: "Tak" - Mason jako tanszy punkt budowy zamiast darmowych punktow; klucz Buildings338MasonCheaper,
    // wylaczony = dopisek min(punkty Masona, praca) jak dotad co do bitu). Log 60 dob (12.11): dopisek ok. 314 pkt/d przy 518 pkt/d pana i 300 pkt/d ludu
    // - ok. 38% oplaconej mocy swiata bez platnika ("nic z niczego"). Teraz: cena punktu budowy w osadzie z Masonem x (1 - poziom x Buildings338MasonPriceCut),
    // domyslnie -10/-20/-30% (poziom 1/2/3, miasto i zamek tak samo) - u pana (BuildFunding.Daily, takze R3-11a OneNeed i 472 WallBudget - ta sama ppp),
    // u ludu (P2 FolkWorks) i w przyspieszeniu zlotem (Boost: te same pensy kupuja wiecej punktow, jak czynnik murow 472 w odwrotna strone). Punkty Masona = 0
    // (Points), wiec dopisek, EngPaidNeed i WallFolkCap licza bez Masona. Mury: czynnik murow x3/x2 i rabat Masona mnoza sie; dzienny limit murow bez zmian.
    // Lenno gracza bez PaidConstructionPlayer - bez zmian (BK liczy Masona sam).
    internal static class Buildings338Mason
    {
        private static TextObject _txt;
        // 473-a: liczniki doby (watek glowny: BuildFunding.Daily i FolkWorks przed Buildings338.Daily tej samej doby)
        private static readonly System.Collections.Generic.HashSet<Town> _cutTowns = new System.Collections.Generic.HashSet<Town>();
        private static double _cutPts, _cutExtra, _cutGold, _cutPaid; private static int _cutLord, _cutFolk;

        internal static void SessionStart() { CutReset(); }
        private static void CutReset() { _cutTowns.Clear(); _cutPts = _cutExtra = _cutGold = _cutPaid = 0; _cutLord = _cutFolk = 0; }

        /// <summary>473-a: rabat Masona na cene punktu budowy wlaczony (Buildings338 + Mason + MasonCheaper).</summary>
        internal static bool CheaperOn { get { var s = Settings.Current; return Buildings338.MasonOn && s != null && s.Buildings338MasonCheaper; } }

        /// <summary>473-a: czynnik ceny punktu budowy osady (1 - poziom Masona x rabat na poziom; 1 - klucz wylaczony, brak Masona albo blad).</summary>
        internal static float PriceFactor(Town t)
        {
            if (t == null || !CheaperOn) return 1f;
            try
            {
                int lvl = 0;
                foreach (var b in t.Buildings)
                    if (b != null && (b.BuildingType == DefaultBuildingTypes.SettlementMason || b.BuildingType == DefaultBuildingTypes.CastleMason) && b.CurrentLevel > lvl) lvl = b.CurrentLevel;
                if (lvl <= 0) return 1f;
                float step = Settings.Current.Buildings338MasonPriceCut;
                if (float.IsNaN(step)) return 1f;
                float d = Math.Min(3, lvl) * Math.Max(0f, Math.Min(0.3f, step));
                return d > 0f ? 1f - d : 1f;
            }
            catch (Exception e) { Buildings338.Stumble(Buildings338.GMason, "Mason.PriceFactor", e); return 1f; }
        }

        /// <summary>473-a: zaplata za punkty budowy osady z rabatem Masona (f &lt; 1): gold - zaplacone, pts - punkty kupione. Bez rabatu te same pensy kupilyby pts x f
        /// punktow; te same punkty kosztowalyby gold / f.</summary>
        internal static void NoteCut(Town t, float f, double gold, double pts, bool folk)
        {
            if (t == null || !(f < 1f) || !(f > 0f) || !(pts > 0)) return;
            _cutTowns.Add(t); _cutPts += pts; _cutExtra += pts * (1.0 - f); _cutPaid += gold; _cutGold += gold * (1.0 - f) / f;
            if (folk) _cutFolk++; else _cutLord++;
        }

        /// <summary>Punkty Masona w modelu gry (tylko typy Mason, perki modelu efektow wchodza same).</summary>
        private static float MasonPoints(Town t)
        {
            var e = new ExplainedNumber(0f);
            foreach (var b in t.Buildings)
                if (b != null && (b.BuildingType == DefaultBuildingTypes.SettlementMason || b.BuildingType == DefaultBuildingTypes.CastleMason))
                    b.AddEffectOfBuilding(BuildingEffectEnum.ConstructionPerDay, ref e);
            return e.ResultNumber;
        }

        /// <summary>Punkty Masona tej osady (0 - klucz wylaczony / brak Masona).</summary>
        internal static float Points(Town t)
        {
            if (t == null || !Buildings338.MasonOn || CheaperOn) return 0f;   // 473-a: przy rabacie Mason nie dopisuje darmowych punktow
            try { float m = MasonPoints(t); return m > 0f && !float.IsNaN(m) ? m : 0f; }
            catch (Exception e) { Buildings338.Stumble(Buildings338.GMason, "Mason.Points", e); return 0f; }
        }

        /// <summary>BuildFunding.PowerPostfix (galaz Covered): pozycja dymku "Mason" = min(punkty Masona, punkty pana + robota ludu + przyspieszenie zlotem (347e)).</summary>
        internal static void Add(Town t, float paid, ref ExplainedNumber r)
        {
            float x = Extra(t, paid);
            if (!(x > 0f)) return;
            if (_txt == null) _txt = new TextObject("{=!}Mason (works with the paid builders)");
            r.Add(x, _txt);
        }

        /// <summary>BuildFunding.PowerIntPostfix (galaz Covered) i dymek: ten sam dopisek.</summary>
        internal static float Extra(Town t, float paid)
        {
            if (!(paid > 0f)) return 0f;
            float m = Points(t);
            return m < paid ? m : paid;
        }

        internal static void Daily(int day)
        {
            if (CheaperOn) { CutLine(day); return; }   // 473-a
            CutReset();
            int n = 0, over = 0; double pts = 0, mSum = 0;
            if (Buildings338.MasonOn)
                foreach (var t in Town.AllFiefs)
                {
                    try
                    {
                        if (t.BuildingsInProgress == null || t.BuildingsInProgress.Count == 0) continue;
                        float m = MasonPoints(t);
                        if (!(m > 0f)) continue;
                        float a = BuildFunding.MasonDay(t);
                        if (!(a > 0f)) continue;
                        n++; pts += a; mSum += m; if (a > m + 0.01f) over++;
                    }
                    catch (Exception e) { Buildings338.Stumble(Buildings338.GMason, "Mason.Daily", e); }
                }
            Log.Info("Budynki (338) murarz: dzien " + day + " | osad " + n + " z Masonem w budowach oplaconych | punktow +" + pts.ToString("0.0", Buildings338.Inv) + "/d (punkty Masona "
                     + mSum.ToString("0.0", Buildings338.Inv) + ", dopisek ponad punkty " + over + ")" + Buildings338.Tail(Buildings338.GMason));
        }

        /// <summary>473-a: linia doby przy rabacie - osady z Masonem (poziomy), osady z oplacona budowa tej doby po tanszej cenie, punkty, punkty z rabatu,
        /// zloto zaoszczedzone (te same punkty po pelnej cenie minus zaplacone). log_grep: "Budynki (338) murarz: dzien".</summary>
        private static void CutLine(int day)
        {
            try
            {
                int m1 = 0, m2 = 0, m3 = 0, inQ = 0;
                foreach (var t in Town.AllFiefs)
                {
                    try
                    {
                        float f = PriceFactor(t);
                        if (!(f < 1f)) continue;
                        int lvl = (int)Math.Round((1f - f) / Math.Max(0.001f, Math.Min(0.3f, Settings.Current.Buildings338MasonPriceCut)));
                        if (lvl <= 1) m1++; else if (lvl == 2) m2++; else m3++;
                        if (t.BuildingsInProgress != null && t.BuildingsInProgress.Count > 0) inQ++;
                    }
                    catch (Exception e) { Buildings338.Stumble(Buildings338.GMason, "Mason.CutLine(osada)", e); }
                }
                var inv = Buildings338.Inv;
                Log.Info("Budynki (338) murarz: dzien " + day + " | 473-a tanszy punkt budowy (Mason Cheaper TAK, rabat " + (100f * Settings.Current.Buildings338MasonPriceCut).ToString("0", inv)
                         + "%/poziom) | osad z Masonem " + (m1 + m2 + m3) + " (poz. 1/2/3: " + m1 + "/" + m2 + "/" + m3 + "), z budowa w kolejce " + inQ
                         + " | oplacone tej doby po tanszej cenie: osad " + _cutTowns.Count + " (wplat pana " + _cutLord + ", ludu " + _cutFolk + "), zaplacono " + _cutPaid.ToString("0", inv)
                         + " zl za " + _cutPts.ToString("0.0", inv) + " pkt, w tym z rabatu " + _cutExtra.ToString("0.0", inv) + " pkt/d, zaoszczedzone zloto " + _cutGold.ToString("0", inv)
                         + " zl/d (te punkty po pelnej cenie) | darmowe punkty Masona 0" + Buildings338.Tail(Buildings338.GMason));
            }
            catch (Exception e) { Buildings338.Stumble(Buildings338.GMason, "Mason.CutLine", e); }
            finally { CutReset(); }
        }
    }
}
