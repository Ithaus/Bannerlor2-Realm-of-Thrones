using System;
using System.Collections.Generic;
using System.Globalization;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// E6/W1 (216, PLAN 6.3, projekt etapu 6 v2 rozdz. 3 "E6/W1"; K13 131) - RECEPTURY WARSZTATOW WEDLUG WARTOSCI.
    /// Dotad warsztaty towarowe gry braly 1 sztuke surowca na 2 wyroby (spworkshops.xml: tkalnia welny 1 welna -> 2 filc, tkalnia lnu 1 len -> 2 plotno,
    /// garbarnia 1 skory -> 2 skora): wyrob wart kilka razy wiecej niz wsad, masa z niczego (10 kg welny -> 20 kg sukna), a len, welna i skory surowe
    /// rosly na polkach (R6), bo warsztaty zjadaly ich ulamek.
    /// Regula (jedna, ta sama funkcja proporcji co rzemioslo miasta - paczka 148, TownCrafts): sztuk wsadu linii = 60% wartosci wyrobow linii
    /// (TownCrafts.MaterialShare) / wartosc sztuki wsadu, zaokraglone do calej sztuki, w granicach fizyki: nie mniej masy niz masa wyrobow
    /// (TownCrafts.Kg) i nie wiecej niz pozwala najmniejsza wydajnosc procesu (MinYield: sukno 0.6, plotno 0.3, skora 0.4 masy wsadu - K13 131);
    /// nigdy mniej niz w recepturze gry ("material >= 60% - bez zmian"). Przy cenach historycznych: filc 3 welny -> 2, plotno 6 lnu -> 2,
    /// skora 5 skor -> 2. Wartosci w nowej monecie - wymaga HistoricalPrices.Applied (jak TownCrafts).
    /// Zakres (PLAN 6.3, projekt R6): linie warsztatow notabli i gracza (typy nieukryte) z JEDNYM wsadem z welny, lnu albo skor surowych
    /// i samymi wyrobami-towarami (WorkshopTrade.IsTradeLine). Linie zbrojne (skory/len -> zbroja, uprzaz), odziez wojska z welny (garment - nie towar),
    /// ukryci rzemieslnicy BK (ArtisanInputs 147) i reszta receptur (zboze, winogrona, oliwki, glina, jedwab, drewno, ruda - W4 / K13 136-137) bez zmian.
    /// Krok x1.3 produkcji wsi NIE jest tutaj - robi go regulator W3 (projekt v2, K1-03).
    /// Jak: raz na sesje, w ArmouryBehavior.OnSessionLaunched zaraz po HistoricalPrices.Apply. Production to struct, ale lista wsadu (MBList -
    /// List) jest obiektem z XML wspolnym dla wszystkich kopii - podmiana liczby w liscie widza od razu gra (TickOneProductionCycle..., ConsumeInputFromTownMarket,
    /// GetInputDailyChange, wybor typu), BK (ConsumeInputFromTownMarket - zaplata za kazda sztuke) i Armoury (WorkshopTrade cena sprawiedliwa i kapital,
    /// CaravanBulk.ShopUse - popyt warsztatow dla karawan i ceny 115, GoodsLedger). Typy warsztatow nie ida do zapisu (laduja sie z XML przy kazdym
    /// starcie gry) - stary zapis wczytuje sie bez zmian, receptura od nowa w kazdej sesji. Oryginaly zapamietane; Reset i wylaczony klucz przywracaja.
    /// Wylacznik: WorkshopRecipesByValue (czytany przy starcie sesji; off = receptury gry).
    /// </summary>
    internal static class WorkshopRecipes
    {
        private const string Scope = "wool,flax,hides";   // PLAN 6.3 / projekt R6: wsad z welny, lnu i skor surowych (reszta receptur - W4)

        // najmniejsza wydajnosc procesu = masa wyrobu / masa wsadu (K13 131, raport B rozdz. 2: sukno 0.6-0.7 - przedza i folowanie traca tluszcz
        // i odpad; len -> plotno 0.3 - paz i pakuly; skory -> skora 0.4 - mizdra, wlos, woda): wiecej wsadu niz masa wyrobu / wydajnosc nie ma sensu
        private static readonly Dictionary<string, float> MinYield = new Dictionary<string, float> { { "wool", 0.6f }, { "flax", 0.3f }, { "hides", 0.4f } };

        // lista wsadu linii (obiekt z XML, porownanie po referencji) -> liczby sztuk gry sprzed podmiany
        private static readonly Dictionary<List<(ItemCategory, int)>, int[]> _orig = new Dictionary<List<(ItemCategory, int)>, int[]>();
        private static int _stumbles;

        internal static void Reset()
        {
            Restore();
            _stumbles = 0;
        }

        /// <summary>Przywraca receptury gry (wszystkie podmienione listy) i czysci pamiec oryginalow.</summary>
        private static void Restore()
        {
            foreach (var kv in _orig)
            {
                try
                {
                    var l = kv.Key; var o = kv.Value;
                    for (int i = 0; i < l.Count && i < o.Length; i++) l[i] = (l[i].Item1, o[i]);
                }
                catch (Exception e) { _stumbles++; if (_stumbles <= 1) Log.Error("WorkshopRecipes.Restore", e); }
            }
            _orig.Clear();
        }

        /// <summary>Z ArmouryBehavior.OnSessionLaunched zaraz po HistoricalPrices.Apply (wlasny try - nigdy nie rzuca). Linia startowa "Receptury (216):".</summary>
        internal static void Apply()
        {
            try
            {
                Restore();   // druga sesja tego samego procesu / zmiana klucza - zawsze od receptury gry
                var s = Settings.Current;
                if (s == null || !s.WorkshopRecipesByValue) { Log.Info("Receptury (216): wylaczone w MCM (Workshop Recipes By Value) - wsad warsztatow jak w grze."); return; }
                if (!HistoricalPrices.Applied) { Log.Info("Receptury (216): NIECZYNNE - brak cen historycznych (60% wartosci jest w nowej monecie) - wsad warsztatow jak w grze."); return; }
                if (WorkshopType.All == null) { Log.Info("Receptury (216): NIECZYNNE - brak typow warsztatow - wsad jak w grze."); return; }
                var scope = new HashSet<string>(Scope.Split(','));
                var parts = new List<string>();
                int changed = 0, kept = 0, skipped = 0, stumbles = 0;
                foreach (var wt in WorkshopType.All)
                {
                    if (wt == null || wt.IsHidden || wt.Productions == null) continue;
                    foreach (var p in wt.Productions)
                    {
                        try
                        {
                            // Production to struct - lista wsadu wspolna (List); jeden wsad z zakresu i same wyroby-towary
                            if (p.Inputs == null || p.Inputs.Count != 1 || !WorkshopTrade.IsTradeLine(p)) continue;
                            List<(ItemCategory, int)> list = p.Inputs;
                            var inCat = list[0].Item1;
                            if (inCat == null || !scope.Contains(inCat.StringId)) continue;
                            var inIt = TownCrafts.ItemOf(inCat);
                            float outVal = 0f, outKg = 0f; string outTxt = ""; bool ok = inIt != null && inIt.Value > 0;
                            if (ok)
                                foreach (var o in p.Outputs)
                                {
                                    var it = TownCrafts.ItemOf(o.Item1);
                                    if (it == null || it.Value <= 0 || o.Item2 <= 0) { ok = false; break; }
                                    outVal += (float)it.Value * o.Item2; outKg += TownCrafts.Kg(it) * o.Item2;
                                    outTxt += (outTxt.Length > 0 ? " + " : "") + o.Item2 + " " + it.StringId;
                                }
                            if (!ok) { skipped++; continue; }
                            int was = list[0].Item2;
                            float inKg = TownCrafts.Kg(inIt);
                            float byValue = TownCrafts.MaterialShare * outVal / inIt.Value;
                            float byMass = outKg / inKg;
                            float y; float cap = MinYield.TryGetValue(inCat.StringId, out y) && y > 0f ? outKg / (y * inKg) : float.MaxValue;
                            int lo = Math.Max(1, (int)Math.Ceiling(byMass - 0.001f));
                            int hi = cap < 1e6f ? (int)Math.Floor(cap + 0.001f) : int.MaxValue;
                            int n = (int)Math.Round(Math.Max(byValue, byMass), MidpointRounding.AwayFromZero);
                            string binds;
                            if (hi < lo) { n = was; binds = "fizyka sprzeczna (sufit wydajnosci ponizej masy wyrobu) - bez zmian"; }
                            else if (n > hi) { n = hi; binds = "sufit wydajnosci"; }
                            else if (n < lo) { n = lo; binds = "masa"; }
                            else binds = byValue >= byMass ? "wartosc" : "masa";
                            int now = Math.Max(was, n);   // nigdy mniej niz receptura gry (material >= 60% - bez zmian)
                            if (hi >= lo && n <= was) binds = "receptura gry";
                            if (now == was) kept++;
                            else
                            {
                                _orig[list] = new[] { was };
                                list[0] = (inCat, now);
                                changed++;
                            }
                            parts.Add(wt.StringId + ": " + inIt.StringId + " -> " + outTxt + ", sztuk wsadu dawniej " + was + ", teraz " + now
                                      + " (60% wartosci " + F2(byValue) + ", masa " + F2(byMass) + (cap < 1e6f ? ", sufit wydajnosci " + F2(cap) : "") + " - wiaze " + binds + ")");
                        }
                        catch (Exception e) { stumbles++; _stumbles++; if (_stumbles <= 1) Log.Error("WorkshopRecipes.Apply (linia " + (wt.StringId ?? "?") + ")", e); }
                    }
                }
                Log.Info("Receptury (216): " + (changed > 0 ? "WLACZONE" : "WLACZONE (nic do zmiany)") + " - wsad warsztatow towarowych z welny, lnu i skor (notable i gracz) wedlug wartosci: "
                         + F2(TownCrafts.MaterialShare) + " wartosci wyrobow / wartosc wsadu (ta sama proporcja co rzemioslo miasta 148), nie mniej masy niz wyrob, nie wiecej niz wydajnosc procesu "
                         + "(welna 0.6, len 0.3, skory 0.4), nigdy mniej niz w grze; krok x1.3 wsi - W3 [" + string.Join("; ", parts.ToArray()) + "]; zmienione " + changed
                         + ", bez zmian " + kept + ", pominiete " + skipped + " (brak przedmiotu wsadu albo wyrobu); potkniecia " + stumbles + ".");
            }
            catch (Exception e) { _stumbles++; Log.Error("WorkshopRecipes.Apply", e); }
        }

        private static string F2(float v) { return v.ToString("0.00", CultureInfo.InvariantCulture); }
    }
}
