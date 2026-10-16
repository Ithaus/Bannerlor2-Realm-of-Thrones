using System;
using System.Collections.Generic;
using System.Globalization;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// KUSZE WEDLUG LUKU TEJ SAMEJ SILY (decyzja Jeffa 15.10 TAK: "cena kuszy 1.5-2 x luk o podobnej sile"; DECYZJA CLAUDE noc 15/16.10: x1.75 - XbowPriceVsBow).
    /// Dowod (items-dump.csv, log 2026-10-15_10-56-30): gra daje kuszom tier 0-2 (crossbow_f t2), lukom 0-6, wiec kusza liczyla dni kusznika i dniowke mistrza
    /// tieru 1-2 (ArmsPricing DaysXbow, HistoricalPrices.DayWageOf) - najmocniejsza kusza 330 funtow (328 J) kosztowala 27 d, dlugi luk t6 (236 J) 48 d.
    /// Sila strzalu jak w RBM (RBMCombat.Utilities.calculateMissileSpeed): energia naciagu E = 0.5 x funty x 4.448 x skok x sprawnosc (luk 0.635 / 0.9, dlugi luk
    /// 0.635 / 0.835, kusza 0.508 / 0.88); pole MissileSpeed na mapie to funty naciagu z XML RBM (RangedRework nadpisuje je tylko w misji i przywraca po niej) -
    /// dlatego Build liczy WYLACZNIE z HistoricalPrices.Apply (start sesji, mapa), nigdy w misji ani na zywo.
    /// Regula: cel T = XbowPriceVsBow x B(E), B - cena luku tej samej energii (luki na handel bez unikatow i bez HistExoticBows, cena z HistCost; krzywa = biegnace
    /// maksimum ceny po energii, liniowo miedzy lukami, ponizej najslabszego i powyzej najmocniejszego proporcjonalnie do energii). T nie wyzsze niz dzisiejszy
    /// koszt - bez zmian (kusze powtarzalne 45-80 funtow zostaja przy 22 d). Inaczej cena rosnie przez PRACE kusznika, nie przez mnoznik wartosci (jedna wycena R7):
    /// tier rownowazny = max(tier gry, tier biegnacego maksimum lukow o energii <= E; ponad wszystkie 6), dniowka mistrza tego tieru (DayWageOf), dni =
    /// max(dni receptury, (T / (1 + zysk) - material) / dniowka). Te same dni i dniowke czytaja wartosc (HistCost), warsztat miasta (WorkshopLaw.Needs / DayWage),
    /// naprawa (MendMaterial.LaborF) i zegar kuzni gracza (ForgeClock) - nikt nie bierze darmowej marzy, placa idzie do ludzi.
    /// Kuznia gracza (Recipes): wymog umiejetnosci i stamina mocnej kuszy wedlug tieru rownowaznego (XbowForgeEquivTier); metal i materialy - tier gry.
    /// Zakupy AI i ludzi gracza (AiGear.BuyLoop, MenPurse.BuyPlayerGaps): kusza wedlug skutecznosc^2 / cena (XbowBuyByPower) - wszystkie kusze poza jedna leza
    /// w jednym koszyku tieru gry, a skutecznosc / cena przy cenie rosnacej szybciej niz sila wybieralaby kusze 80-100 funtow. Bramka budzetu bez zmian.
    /// Bez RBM (pole missile_speed to predkosc w m/s, nie funty) albo bez cen historycznych - nic (linia startu mowi dlaczego). Stan sesji (slowniki po ItemObject)
    /// czyszczony w HistoricalPrices.Reset; nic w zapisie. Wyjatek przy jednym przedmiocie - potkniecie (licznik, pierwsze do Log.Error), reszta liczy dalej.
    /// </summary>
    internal static class XbowPrice
    {
        private sealed class Pt { public string Id; public float E; public int Price, Tier, RunPrice, RunTier; }

        private static readonly Dictionary<ItemObject, float> _days = new Dictionary<ItemObject, float>();   // dni kusznika (z jakoscia sztuki) po zmianie
        private static readonly Dictionary<ItemObject, int> _tier = new Dictionary<ItemObject, int>();       // tier rownowazny (dniowka i wymog kuzni)
        private static bool _active;                                                                        // przeliczenie tej sesji obowiazuje (Build)
        private static string _start = "Kusze wedlug luku (XbowLikeBow): start sesji | nie liczone (ceny historyczne nie przeliczone w tej sesji) | potkniecia 0.";
        private static int _stumbles;
        private static bool _errLogged;
        private static readonly object Gate = new object();
        // zakupy od poprzedniej linii doby (tylko liczniki)
        private static int _aiN, _menN, _dayStumbles;
        private static double _aiLbs, _menLbs;

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        internal static bool Active { get { return _active; } }

        internal static void Reset()
        {
            lock (Gate)
            {
                _days.Clear(); _tier.Clear(); _active = false; _stumbles = 0; _errLogged = false;
                _aiN = _menN = _dayStumbles = 0; _aiLbs = _menLbs = 0;
                _start = "Kusze wedlug luku (XbowLikeBow): start sesji | nie liczone (ceny historyczne nie przeliczone w tej sesji) | potkniecia 0.";
            }
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (!_errLogged) { _errLogged = true; Log.Error("XbowPrice." + where, e); }
        }

        /// <summary>Energia naciagu w dzulach wedlug wzoru RBM (funty z pola MissileSpeed, skok i sprawnosc wedlug ItemUsage). 0 = brak broni strzeleckiej.</summary>
        internal static float Energy(ItemObject it)
        {
            var w = it != null ? it.PrimaryWeapon : null;
            if (w == null) return 0f;
            float lbs = w.MissileSpeed;
            if (!(lbs > 0f)) return 0f;
            float stroke = 0.635f, eff = 0.9f;
            string u = w.ItemUsage ?? "";
            if (u == "long_bow") eff = 0.835f;
            else if (u == "crossbow" || u == "crossbow_fast") { stroke = 0.508f; eff = 0.88f; }
            return 0.5f * lbs * 4.448f * stroke * eff;
        }

        private static int Lbs(ItemObject it)
        {
            try { var w = it != null ? it.PrimaryWeapon : null; return w != null ? w.MissileSpeed : 0; } catch { return 0; }
        }

        private static HashSet<string> ExoticIds(string list)
        {
            // ten sam zapis co HistoricalPrices.Apply (HistExoticBows: id=wartosc, po przecinku)
            var res = new HashSet<string>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(list)) return res;
            foreach (var pair in list.Split(','))
            {
                var kv = pair.Split('=');
                if (kv.Length >= 1 && kv[0].Trim().Length > 0) res.Add(kv[0].Trim());
            }
            return res;
        }

        /// <summary>Cena luku tej samej energii (krzywa biegnacego maksimum ceny po energii).</summary>
        private static float BowPriceAt(List<Pt> pts, float e)
        {
            int n = pts.Count;
            if (e <= pts[0].E) return pts[0].RunPrice * e / Math.Max(0.01f, pts[0].E);
            if (e >= pts[n - 1].E) return pts[n - 1].RunPrice * e / Math.Max(0.01f, pts[n - 1].E);
            for (int i = 0; i + 1 < n; i++)
            {
                var a = pts[i]; var b = pts[i + 1];
                if (e < a.E || e > b.E) continue;
                if (b.E - a.E < 1e-4f) return a.RunPrice;
                return a.RunPrice + (b.RunPrice - a.RunPrice) * (e - a.E) / (b.E - a.E);
            }
            return pts[n - 1].RunPrice;
        }

        /// <summary>Tier rownowazny energii: tier biegnacego maksimum lukow o energii nie wiekszej; ponad wszystkie luki - 6, ponizej - 1.</summary>
        private static int TierAt(List<Pt> pts, float e)
        {
            int n = pts.Count;
            if (e > pts[n - 1].E) return 6;
            int t = 1;
            for (int i = 0; i < n; i++) if (pts[i].E <= e) t = pts[i].RunTier;
            return t;
        }

        /// <summary>Z HistoricalPrices.Apply, tuz PRZED wycena broni i zbroi (krok 2): tabela lukow, dni i tier rownowazny kusz, linia startu sesji.</summary>
        internal static void Build()
        {
            lock (Gate) { _days.Clear(); _tier.Clear(); _active = false; _stumbles = 0; _errLogged = false; }
            const string Head = "Kusze wedlug luku (XbowLikeBow): start sesji | ";
            try
            {
                var s = Settings.Current;
                if (s == null || !s.XbowLikeBow) { _start = Head + "WYLACZONE (MCM) - kusze wyceniane jak dotad (dni kusznika i dniowka tieru gry) | potkniecia 0."; return; }
                if (!HistoricalPrices.On) { _start = Head + "NIECZYNNE - ceny historyczne wylaczone, kusze jak dotad | potkniecia 0."; return; }
                bool rbm = false;
                try { rbm = AccessTools.TypeByName("RBMCombat.Utilities") != null; } catch { rbm = false; }
                if (!rbm) { _start = Head + "NIECZYNNE - bez RBM (pole missile_speed to predkosc w m/s, nie funty naciagu) - kusze jak dotad | potkniecia 0."; return; }

                // 1. tabela lukow na handel (bez unikatow i lukow z rzadkiego materialu): energia, cena (HistCost - ta sama, ktora dostana w kroku 2), tier gry
                var exotic = ExoticIds(s.HistExoticBows);
                var pts = new List<Pt>();
                foreach (var it in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
                {
                    try
                    {
                        if (it == null || it.ItemType != ItemObject.ItemTypeEnum.Bow || it.NotMerchandise || it.Value <= 0) continue;
                        if (it.StringId != null && exotic.Contains(it.StringId)) continue;
                        if (ArmsPricing.IsUnique(it)) continue;
                        float hc = HistoricalPrices.HistCost(it);
                        float e = Energy(it);
                        if (!(hc > 0f) || !(e > 0f)) continue;
                        pts.Add(new Pt { Id = it.StringId, E = e, Price = Math.Max(1, (int)Math.Round(hc)), Tier = Recipes.Grade(it) });
                    }
                    catch (Exception ex) { Stumble("Build(luk)", ex); }
                }
                if (pts.Count == 0) { _start = Head + "NIECZYNNE - brak lukow na handel z cena i sila (tabela pusta), kusze jak dotad | potkniecia " + _stumbles + "."; return; }
                pts.Sort((a, b) => a.E.CompareTo(b.E));
                int rp = 0, rt = 1, pMin = int.MaxValue, pMax = 0;
                foreach (var p in pts)
                {
                    rp = Math.Max(rp, p.Price); rt = Math.Max(rt, p.Tier);
                    p.RunPrice = rp; p.RunTier = rt;
                    pMin = Math.Min(pMin, p.Price); pMax = Math.Max(pMax, p.Price);
                }
                var top = pts[pts.Count - 1];

                // 2. kusze na handel: cel, tier rownowazny, dni (kolejnosc: koszt dzisiejszy liczony, zanim przedmiot trafi do slownika)
                float k = Math.Max(1f, s.XbowPriceVsBow);
                float profit = 1f + Math.Max(0f, s.HistProfitPercent) / 100f;
                _active = true;
                int merch = 0, noE = 0, uniq = 0, sameN = 0;
                var done = new List<KeyValuePair<float, string>>();   // (energia, opis)
                var same = new List<string>();
                ItemObject strongest = null; float strongE = -1f; int strongPrice = 0;
                foreach (var it in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
                {
                    try
                    {
                        if (it == null || it.ItemType != ItemObject.ItemTypeEnum.Crossbow || it.NotMerchandise || it.Value <= 0) continue;
                        var c = ArmsPricing.CostOf(it);
                        if (c == null) continue;
                        merch++;
                        float e = Energy(it);
                        if (!(e > 0f)) { noE++; continue; }
                        float c0 = HistoricalPrices.HistCost(it);       // dzisiejsza wycena (przedmiotu nie ma jeszcze w slowniku)
                        float b = BowPriceAt(pts, e);
                        float target = k * b;
                        if (target <= c0)
                        {
                            sameN++;
                            if (same.Count < 8) same.Add(it.StringId + " " + Math.Round(c0).ToString("0", Inv) + " (cel " + target.ToString("0", Inv) + ")");
                            else if (same.Count == 8) same.Add("...");
                            if (e > strongE) { strongE = e; strongest = it; strongPrice = (int)Math.Round(c0); }
                            continue;
                        }
                        int tg = Recipes.Grade(it);
                        int teq = Math.Max(tg, TierAt(pts, e));
                        float wage = HistoricalPrices.WageFor(teq);
                        float mat = HistoricalPrices.HistMaterial(it, c);
                        float days = Math.Max(c.Days, (target / profit - mat) / Math.Max(0.01f, wage));
                        lock (Gate) { _tier[it] = teq; _days[it] = days; }
                        float c1 = HistoricalPrices.HistCost(it);       // po zmianie (czyta slowniki) - ta wartosc dostanie przedmiot w kroku 2
                        bool u = ArmsPricing.IsUnique(it);
                        if (u) uniq++;
                        int newPrice = (int)Math.Round(c1);
                        if (e > strongE) { strongE = e; strongest = it; strongPrice = newPrice; }
                        done.Add(new KeyValuePair<float, string>(e, it.StringId + " " + Math.Round(c0).ToString("0", Inv) + "->" + newPrice
                                 + " (" + e.ToString("0", Inv) + " J, " + Lbs(it) + " lb, t" + tg + "->t" + teq + ", dni " + c.Days.ToString("0.0", Inv) + "->" + days.ToString("0.0", Inv)
                                 + ", luk tej sily " + b.ToString("0.0", Inv) + ", x" + (b > 0f ? (c1 / b).ToString("0.00", Inv) : "-") + (u ? ", unikat - w wartosci x prestiz" : "") + ")"));
                    }
                    catch (Exception ex)
                    {
                        lock (Gate) { _days.Remove(it); _tier.Remove(it); }   // ta kusza zostaje przy dawnej wycenie
                        Stumble("Build(kusza)", ex);
                    }
                }
                done.Sort((a, b) => b.Key.CompareTo(a.Key));
                var parts = new List<string>();
                foreach (var kv in done) parts.Add(kv.Value);
                string ratio = top.Price > 0 && strongest != null ? (strongPrice / (float)top.Price).ToString("0.00", Inv) : "-";
                _start = Head + "x" + k.ToString("0.00", Inv) + " ceny luku tej samej sily (energia naciagu RBM) | lukow w tabeli " + pts.Count
                         + " (E " + pts[0].E.ToString("0", Inv) + "-" + top.E.ToString("0", Inv) + " J, cena " + pMin + "-" + pMax + " d, najmocniejszy " + top.Id + " " + top.Price + " d)"
                         + " | kusz przeliczonych " + done.Count + " z " + merch + " na handel" + (uniq > 0 ? " (w tym unikatow " + uniq + ")" : "") + ": " + (parts.Count > 0 ? string.Join("; ", parts.ToArray()) : "-")
                         + " | bez zmian " + sameN + (same.Count > 0 ? " [" + string.Join(", ", same.ToArray()) + "]" : "")
                         + (noE > 0 ? " | bez sily (brak funtow) " + noE : "")
                         + " | najmocniejsza kusza " + (strongest != null ? strongest.StringId + " " + strongPrice : "-") + " d / najmocniejszy luk " + top.Price + " d = x" + ratio
                         + " | kuznia gracza: wymog wedlug tieru rownowaznego " + (s.XbowForgeEquivTier ? "tak" : "nie")
                         + " | zakupy kusz wedlug sily " + (s.XbowBuyByPower ? "tak" : "nie")
                         + " | potkniecia " + _stumbles + ".";
            }
            catch (Exception e)
            {
                lock (Gate) { _days.Clear(); _tier.Clear(); _active = false; }   // polowiczna tabela gorsza niz zadna: kusze jak dotad
                Stumble("Build", e);
                _start = Head + "NIECZYNNE - wyjatek przy liczeniu (ERROR w logu), kusze jak dotad | potkniecia " + _stumbles + ".";
            }
        }

        internal static string StartLine() { return _start; }

        /// <summary>Dni kusznika nad ta kusza po zmianie (z jakoscia sztuki) - HistoricalPrices.HistDays. false = bez zmian.</summary>
        internal static bool TryDays(ItemObject it, out float days)
        {
            days = 0f;
            if (!_active || it == null) return false;
            lock (Gate) return _days.TryGetValue(it, out days);
        }

        /// <summary>Tier rownowazny kuszy (dniowka mistrza - HistoricalPrices.DayWageOf); 0 = bez zmian.</summary>
        internal static int EquivTier(ItemObject it)
        {
            if (!_active || it == null || it.ItemType != ItemObject.ItemTypeEnum.Crossbow) return 0;
            int t;
            lock (Gate) return _tier.TryGetValue(it, out t) ? t : 0;
        }

        /// <summary>Kuznia gracza (Recipes): tier wymogu umiejetnosci i staminy kuszy; 0 = tier gry.</summary>
        internal static int ForgeTier(ItemObject it)
        {
            var s = Settings.Current;
            return s != null && s.XbowForgeEquivTier ? EquivTier(it) : 0;
        }

        /// <summary>Ocena sztuki przy zakupie z polki: kusza (przy przeliczeniu tej sesji i XbowBuyByPower) skutecznosc^2 / cena, reszta skutecznosc / cena.</summary>
        internal static float BuyScore(ItemObject it, float eff, int price)
        {
            if (price <= 0) return 0f;
            var s = Settings.Current;
            if (_active && it != null && it.ItemType == ItemObject.ItemTypeEnum.Crossbow && s != null && s.XbowBuyByPower) return eff * eff / price;
            return eff / price;
        }

        /// <summary>Licznik zakupow kusz (linia doby): ai - lordowie AI i zalogi (AiGear), inaczej ludzie gracza (MenPurse). Tylko licznik.</summary>
        internal static void NoteBuy(ItemObject it, int n, bool ai)
        {
            if (it == null || n <= 0 || it.ItemType != ItemObject.ItemTypeEnum.Crossbow) return;
            try
            {
                int lb = Lbs(it);
                lock (Gate)
                {
                    if (ai) { _aiN += n; _aiLbs += (double)lb * n; }
                    else { _menN += n; _menLbs += (double)lb * n; }
                }
            }
            catch { lock (Gate) _dayStumbles++; }
        }

        /// <summary>Z ArmouryBehavior.OnDailyTick: linia "Kusze wedlug luku (zakupy)" (tylko log).</summary>
        internal static void Daily()
        {
            var s = Settings.Current;
            if (s == null || !s.XbowLikeBow) return;
            int ai, men, st; double al, ml;
            lock (Gate) { ai = _aiN; men = _menN; al = _aiLbs; ml = _menLbs; st = _dayStumbles; _aiN = _menN = _dayStumbles = 0; _aiLbs = _menLbs = 0; }
            Log.Info("Kusze wedlug luku (zakupy): dzien " + (int)CampaignTime.Now.ToDays + " | " + (_active ? "CZYNNE (kusz przeliczonych " + _days.Count + ")" : "NIECZYNNE (linia startu)")
                     + " | kusz kupionych przez AI " + ai + " (srednio " + (ai > 0 ? (al / ai).ToString("0", Inv) : "-") + " funtow), ludzie gracza " + men
                     + " (srednio " + (men > 0 ? (ml / men).ToString("0", Inv) : "-") + " funtow) | ocena zakupu kuszy " + (_active && s.XbowBuyByPower ? "skutecznosc^2 / cena" : "skutecznosc / cena")
                     + " | potkniecia " + st + ".");
        }
    }
}
