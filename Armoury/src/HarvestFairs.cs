using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// R3-05a (p5-handel, noc 20/21.10; Jeff 20.10: "pozostale tak") - JARMARKI PO ZNIWACH (klucz HarvestFairs). Zrodlo: docs/POMYSLY-35-2026-10-20.md poz. 8,
    /// audyt 10-pomysly Z8 (szesc jarmarkow Szampanii po 2-6 tygodni, rozlozone w roku, wyrosly z jarmarkow rolnych i bydlecych).
    /// Regula:
    ///  - KRAINA = kultura miasta (Settlement.Culture; ROT: battania = Polnoc, vlandia = Zachod). Kazda kraina z miastem ma jeden jarmark w roku.
    ///  - KIEDY: po oknie zniw (FreeHands.HarvestTo, dzien 252 roku 364-dniowego; inny rok - proporcjonalnie) do konca roku, krainy po kolei od poludnia na polnoc
    ///    (srednia wspolrzedna Y jej miast; na polnocy zniwa koncza sie pozniej). Jarmark trwa Days (14) dni. Rok bez zniw (pora Westeros zima albo wiosna w dniu
    ///    otwarcia - FreeHands.HarvestWindow liczy zniwa tylko latem i jesienia) - jarmarku nie ma.
    ///  - GDZIE: najbogatsze (Prosperity) miasto krainy bez oblezenia w dniu otwarcia; wybor zapisany (arm_fairs), wiec po wczytaniu jarmark zostaje w tym samym miescie.
    ///  - POPYT (nie zloto z niczego): przybywa kupujacych, wiec w miescie jarmarku dzisiejszy szacunek popytu towarow handlowych i zwierzat jest o DemandBoost (40%)
    ///    wiekszy. Gra wygladza popyt rynku co dobe (ItemConsumptionBehavior.UpdateSupplyAndDemand: nowy = (1 - a) x stary + a x szacunek, a = 0.15 - mierzone raz
    ///    na sesje jak FoodSubstitute). Postfiks dopisuje b x a x szacunek, gdzie a x szacunek = nowy - (1 - a) x stary (stary zapamietany w prefiksie) - dokladnie
    ///    to, co dalby szacunek x (1 + b), bez pytania modelu drugi raz i bez skladania sie dopisku z dnia na dzien. Indeks ceny rosnie przez 2 tygodnie (ok. +36%
    ///    popytu na koniec, cena ok. +20% przy tej samej polce) i po jarmarku wraca sam. Placi kasa miasta (Town.Gold) - kupcy miasta skupuja drozej, a kupujacy w
    ///    miescie tez placa drozej; budzet konsumpcji mieszczan (GetDailyDemandForCategory) bez zmian. Wozy wsi (MarketCarts.Choose) i karawany (BK GetTradeScoreForTown,
    ///    CaravanGoods) widza wyzsza cene przez dane rynku - nic wiecej nie trzeba wpinac. Uzbrojenie poza regula (osobny rynek broni modu).
    ///  - OPLATA TARGOWA: od kazdej sprzedazy miastu jarmarku przez woz wsi i karawane FeeRate (1%) utargu z sakwy sprzedajacego do kasy miasta (Town.ChangeGold).
    ///    Wpieta w TownToll (CartEnd / TakeCaravan) - te same zwolnienia co myto (kontrakty 174, wyprzedaz przed rozwiazaniem, dostawy) i ta sama kontrola ksiegi:
    ///    woz - migawka okna przesunieta (MoneyLedger.CartTollInWindow), karawana - czesc myta wracajaca do kasy (TownToll.LastToTown); wplata nazwana jak myto do kasy
    ///    (Note169 N169Toll). Gdy klucz Town Market Toll wylaczony, oplata jarmarku dalej dziala.
    /// Wiesc: przy otwarciu krotki napis dla gracza. Stan: arm_fairs (SaveText.Sync; brak klucza = brak jarmarkow w toku). Linie: "Jarmarki po zniwach (R3-05a): start
    /// sesji" i "...: dzien N". Wyjatek per obiekt - licznik potkniec, bez wylacznika.
    /// </summary>
    internal static class HarvestFairs
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        internal const int Days = 14;
        internal const float DemandBoost = 0.40f;
        internal const double FeeRate = 0.01;

        private sealed class Fair { public string Realm; public string TownId; public Town Town; public int Start; public bool Skipped; public string Why; }
        private sealed class Land { public string Realm; public double Y; public int N; public int StartDoy; }

        private static readonly List<Fair> _fairs = new List<Fair>();                 // jarmarki (i pominiete) z ostatniego roku
        private static readonly Dictionary<Town, Fair> _active = new Dictionary<Town, Fair>();
        private static List<Land> _lands;                                            // kalendarz krain tej sesji
        private static int _landsYear = -1;

        private static Harmony _h; private static bool _hookTried, _hooked;          // raz na proces
        private static float _alpha;
        private static float[] _old; private static Town _snapTown;

        // liczniki od ostatniej linii
        private static int _cartN, _carN, _boostTowns, _boostCats, _opened, _closed, _skipped, _stumbles, _stumbleSess;
        private static long _cartGross, _cartFee, _carGross, _carFee;
        private static double _boostSum;
        private static readonly StringBuilder _openTxt = new StringBuilder(), _closeTxt = new StringBuilder();
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static bool _startDone;

        internal static bool On { get { var s = Settings.Current; return s != null && s.HarvestFairs; } }

        internal static void Reset()
        {
            _fairs.Clear(); _active.Clear(); _lands = null; _landsYear = -1; _alpha = 0f; _snapTown = null; _startDone = false; _stumbleSess = 0; _err.Clear();
            ZeroDay();
        }

        private static void ZeroDay()
        {
            _cartN = _carN = _boostTowns = _boostCats = _opened = _closed = _skipped = _stumbles = 0;
            _cartGross = _cartFee = _carGross = _carFee = 0; _boostSum = 0;
            _openTxt.Length = 0; _closeTxt.Length = 0;
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumbleSess++;
            try { if (_err.Add(where)) Log.Error("HarvestFairs." + where, e); } catch { }
        }

        private static int YearDays() { try { return Math.Max(28, CampaignTime.DaysInYear); } catch { return 364; } }

        // ------------------------------------------------------------ kalendarz krain
        private static List<Land> Lands(int y)
        {
            if (_lands != null && _landsYear == y) return _lands;
            var by = new Dictionary<string, Land>();
            foreach (var t in Town.AllTowns)
            {
                var st = t != null ? t.Settlement : null;
                if (st == null || !st.IsTown) continue;
                string r = RealmOfTown(st);
                Land l; if (!by.TryGetValue(r, out l)) { l = new Land { Realm = r }; by[r] = l; }
                l.Y += st.GetPosition2D.y; l.N++;
            }
            var list = new List<Land>(by.Values);
            foreach (var l in list) l.Y = l.N > 0 ? l.Y / l.N : 0;
            list.Sort((a, b) => a.Y != b.Y ? a.Y.CompareTo(b.Y) : string.CompareOrdinal(a.Realm, b.Realm));   // od poludnia na polnoc
            int from = (int)Math.Round((FreeHands.HarvestTo + 1) * (double)y / 364.0), last = Math.Max(from, y - Days - 1);
            for (int k = 0; k < list.Count; k++)
                list[k].StartDoy = from + (list.Count > 1 ? (int)Math.Round(k * (double)(last - from) / (list.Count - 1)) : 0);
            _lands = list; _landsYear = y;
            return list;
        }

        /// <summary>Kraina miasta = jego kultura (ROT: battania = Polnoc, vlandia = Zachod itd.). Bez FreeHands.RealmOf - wczesne wolanie zapisaloby w jego pamieci
        /// kraine sprzed kalibracji ludzi.</summary>
        private static string RealmOfTown(Settlement st)
        {
            try { return st.Culture != null && !string.IsNullOrEmpty(st.Culture.StringId) ? st.Culture.StringId : "?"; } catch { return "?"; }
        }

        private static Town Resolve(Fair f)
        {
            if (f.Town != null || f.Skipped || string.IsNullOrEmpty(f.TownId)) return f.Town;
            try { var st = Settlement.Find(f.TownId); f.Town = st != null ? st.Town : null; } catch { f.Town = null; }
            if (f.Town == null) { f.Skipped = true; f.Why = "brak miasta po wczytaniu"; }
            return f.Town;
        }

        private static string NameOf(Town t) { try { return t != null && t.Name != null ? t.Name.ToString() : "?"; } catch { return "?"; } }

        // ------------------------------------------------------------ sesja
        internal static void SessionStart()
        {
            EnsureHooks();
            try
            {
                int y = YearDays();
                var lands = Lands(y);
                RebuildActive((int)CampaignTime.Now.ToDays);   // jarmarki w toku z zapisu dzialaja od razu po wczytaniu (nie od pierwszej doby)
                _closed = 0; _closeTxt.Length = 0;
                var sb = new StringBuilder(1200);
                sb.Append("Jarmarki po zniwach (R3-05a): start sesji | klucz Harvest Fairs ").Append(On ? "tak" : "nie")
                  .Append(" | krain z miastem ").Append(lands.Count).Append(", jarmark ").Append(Days).Append(" dni w najbogatszym miescie krainy, po zniwach od poludnia na polnoc")
                  .Append(" | popyt towarow handlowych i zwierzat +").Append((100 * DemandBoost).ToString("0", Inv)).Append("% szacunku dnia, oplata targowa ").Append((100 * FeeRate).ToString("0.#", Inv))
                  .Append("% od sprzedazy wozow i karawan do kasy miasta | latka UpdateSupplyAndDemand ").Append(_hooked ? "wpieta" : "NIE")
                  .Append(" | w zapisie jarmarkow ").Append(_fairs.Count).Append(" | kalendarz (dzien roku): ");
                for (int i = 0; i < lands.Count; i++) { if (i > 0) sb.Append(", "); sb.Append(lands[i].Realm).Append('@').Append(lands[i].StartDoy); }
                Log.Info(sb.ToString());
                _startDone = true;
            }
            catch (Exception e) { Stumble("SessionStart", e); }
        }

        private static void EnsureHooks()
        {
            if (_hookTried) return;
            _hookTried = true;
            try
            {
                var m = AccessTools.DeclaredMethod(typeof(ItemConsumptionBehavior), "UpdateSupplyAndDemand");
                if (m == null) { Log.Info("Jarmarki po zniwach (R3-05a): BRAK metody ItemConsumptionBehavior.UpdateSupplyAndDemand - bez dopisku popytu (oplata dziala)."); return; }
                _h = _h ?? new Harmony("armoury.p5handel.fairs");
                _h.Patch(m, prefix: new HarmonyMethod(typeof(HarvestFairs), nameof(SupplyPrefix)), postfix: new HarmonyMethod(typeof(HarvestFairs), nameof(SupplyPostfix)));
                _hooked = true;
            }
            catch (Exception e) { Log.Error("HarvestFairs.EnsureHooks", e); }
        }

        // ------------------------------------------------------------ doba
        internal static void Daily()
        {
            var s = Settings.Current;
            if (s == null || Campaign.Current == null) return;
            if (!_startDone) SessionStart();
            int day = (int)CampaignTime.Now.ToDays, y = YearDays(), doy = day % y, year = day / y;
            try
            {
                // zamkniecie i porzadki (rekordy starsze niz rok - skreslone)
                for (int i = _fairs.Count - 1; i >= 0; i--)
                {
                    var f = _fairs[i];
                    if (day - f.Start >= y) { _fairs.RemoveAt(i); continue; }
                }
                RebuildActive(day);
                // otwarcie
                if (s.HarvestFairs)
                {
                    var lands = Lands(y);
                    foreach (var l in lands)
                    {
                        if (doy < l.StartDoy || doy >= l.StartDoy + Days) continue;
                        int start = year * y + l.StartDoy;
                        bool had = false;
                        foreach (var f in _fairs) if (f.Realm == l.Realm && f.Start == start) { had = true; break; }
                        if (had) continue;
                        Open(l.Realm, start, day);
                    }
                    RebuildActive(day);
                }
            }
            catch (Exception e) { Stumble("Daily", e); }
            Line(s, day, doy, year);
        }

        private static void RebuildActive(int day)
        {
            var was = new List<Town>(_active.Keys);
            _active.Clear();
            foreach (var f in _fairs)
            {
                if (f.Skipped || day < f.Start || day >= f.Start + Days) continue;
                var t = Resolve(f);
                if (t != null) _active[t] = f;
            }
            foreach (var t in was)
                if (!_active.ContainsKey(t)) { _closed++; if (_closeTxt.Length > 0) _closeTxt.Append(", "); _closeTxt.Append(NameOf(t)); }
        }

        private static void Open(string realm, int start, int day)
        {
            var f = new Fair { Realm = realm, Start = start };
            try
            {
                var season = WesterosClimate.Now();
                if (season != CampaignTime.Seasons.Summer && season != CampaignTime.Seasons.Autumn) { f.Skipped = true; f.Why = "rok bez zniw (" + season + ")"; }
                else
                {
                    Town best = null; float bp = float.MinValue;
                    foreach (var t in Town.AllTowns)
                    {
                        var st = t != null ? t.Settlement : null;
                        if (st == null || !st.IsTown || st.IsUnderSiege) continue;
                        if (RealmOfTown(st) != realm) continue;
                        if (t.Prosperity > bp) { bp = t.Prosperity; best = t; }
                    }
                    if (best == null) { f.Skipped = true; f.Why = "brak miasta bez oblezenia"; }
                    else { f.Town = best; f.TownId = best.Settlement.StringId; }
                }
            }
            catch (Exception e) { f.Skipped = true; f.Why = "blad"; Stumble("Open", e); }
            _fairs.Add(f);
            if (f.Skipped) { _skipped++; if (_openTxt.Length > 0) _openTxt.Append(", "); _openTxt.Append(realm).Append(" - pominiety: ").Append(f.Why); return; }
            _opened++;
            if (_openTxt.Length > 0) _openTxt.Append(", ");
            _openTxt.Append(NameOf(f.Town)).Append(" (").Append(realm).Append(", do dnia ").Append(start + Days - 1).Append(')');
            try
            {
                int left = start + Days - day;
                TaleWorlds.Library.InformationManager.DisplayMessage(new TaleWorlds.Library.InformationMessage(
                    "Harvest fair in " + NameOf(f.Town) + " for " + left + " days: more buyers, better prices."));
            }
            catch (Exception e) { Stumble("Open(wiesc)", e); }
        }

        /// <summary>Czy w miescie trwa jarmark (tylko odczyt; watek glowny).</summary>
        internal static bool IsFairTown(Town t) { return t != null && On && _active.ContainsKey(t); }

        // ------------------------------------------------------------ popyt (ItemConsumptionBehavior.UpdateSupplyAndDemand, watek glowny)
        public static void SupplyPrefix(Town __0)
        {
            _snapTown = null;
            try
            {
                if (__0 == null || !On || !_active.ContainsKey(__0)) return;
                var all = ItemCategories.All;
                if (_old == null || _old.Length < all.Count) _old = new float[all.Count];
                var md = __0.MarketData;
                for (int i = 0; i < all.Count; i++) _old[i] = md.GetCategoryData(all[i]).Demand;
                _snapTown = __0;
            }
            catch (Exception e) { _snapTown = null; Stumble("SupplyPrefix", e); }
        }

        public static void SupplyPostfix(Town __0)
        {
            if (_snapTown == null || !ReferenceEquals(_snapTown, __0)) return;
            _snapTown = null;
            try
            {
                float a = Alpha(__0);
                var all = ItemCategories.All;
                var md = __0.MarketData;
                int n = 0;
                for (int i = 0; i < all.Count && i < _old.Length; i++)
                {
                    var c = all[i];
                    if (c == null || !(c.IsTradeGood || c.IsAnimal)) continue;
                    float now = md.GetCategoryData(c).Demand;
                    float add = DemandBoost * (now - (1f - a) * _old[i]);      // b x (a x szacunek dnia)
                    if (!(add > 0f) || float.IsInfinity(add)) continue;
                    md.SetDemand(c, now + add);
                    n++; _boostSum += add;
                }
                if (n > 0) { _boostTowns++; _boostCats += n; }
            }
            catch (Exception e) { Stumble("SupplyPostfix", e); }
        }

        /// <summary>Waga dzisiejszego szacunku w wygladzaniu gry (GetSupplyDemandForCategory(..., dzis 1, stary 0)) - raz na sesje, jak FoodSubstitute.Alpha.</summary>
        private static float Alpha(Town t)
        {
            if (_alpha > 0f) return _alpha;
            float a = 0.15f;
            try
            {
                var all = ItemCategories.All;
                ItemCategory c = null;
                for (int i = 0; i < all.Count; i++) if (all[i] != null && all[i].IsTradeGood) { c = all[i]; break; }
                if (c != null)
                {
                    var r = Campaign.Current.Models.SettlementEconomyModel.GetSupplyDemandForCategory(t, c, 0f, 1f, 0f, 0f);
                    if (r.Item2 > 0.001f && r.Item2 <= 1f) a = r.Item2;
                }
            }
            catch { a = 0.15f; }
            _alpha = a;
            return a;
        }

        // ------------------------------------------------------------ oplata targowa (TownToll.CartEnd / TakeCaravan)
        /// <summary>Woz wsi sprzedal miastu za `net` (utarg netto sprzedazy). Zwraca zdjeta oplate (0 - nie jarmark). Nie rzuca.</summary>
        internal static int CartFee(Settlement st, MobileParty cart, int net) { return Fee(st, cart, net, true); }
        /// <summary>Karawana sprzedala miastu za `gross`. Zwraca zdjeta oplate (0 - nie jarmark). Nie rzuca.</summary>
        internal static int CaravanFee(Settlement st, MobileParty mp, int gross) { return Fee(st, mp, gross, false); }

        private static int Fee(Settlement st, MobileParty payer, int amount, bool cart)
        {
            try
            {
                if (!On || st == null || payer == null || amount <= 0 || !st.IsTown) return 0;
                var town = st.Town;
                if (town == null || !_active.ContainsKey(town)) return 0;
                int fee = (int)Math.Round(amount * FeeRate, MidpointRounding.AwayFromZero);
                fee = Math.Min(fee, Math.Max(0, payer.PartyTradeGold));
                if (fee <= 0) return 0;
                // przelew: najpierw zdjecie z sakwy, potem wplata; wyjatek przy wplacie oddaje zloto platnikowi
                int had = payer.PartyTradeGold;
                payer.PartyTradeGold = had - fee;
                if (had - payer.PartyTradeGold != fee) { payer.PartyTradeGold = had; return 0; }
                try { town.ChangeGold(fee); }
                catch (Exception e) { payer.PartyTradeGold = had; Stumble("Fee(kasa)", e); return 0; }
                try { MoneyLedger.Note169(MoneyLedger.N169Toll, st, fee); } catch { }
                if (cart) { _cartN++; _cartGross += amount; _cartFee += fee; }
                else { _carN++; _carGross += amount; _carFee += fee; }
                return fee;
            }
            catch (Exception e) { Stumble("Fee", e); return 0; }
        }

        // ------------------------------------------------------------ linia doby
        private static void Line(Settings s, int day, int doy, int year)
        {
            try
            {
                var sb = new StringBuilder(1200);
                sb.Append("Jarmarki po zniwach (R3-05a): dzien ").Append(day).Append(" | ").Append(s.HarvestFairs ? "CZYNNE" : "WYLACZONE (MCM)")
                  .Append(" | rok ").Append(year).Append(", dzien roku ").Append(doy).Append(", pora Westeros ").Append(SafeSeason())
                  .Append(" | trwa ").Append(_active.Count).Append(": ");
                int k = 0;
                foreach (var kv in _active)
                {
                    if (k++ > 0) sb.Append(", ");
                    sb.Append(NameOf(kv.Key)).Append(" (").Append(kv.Value.Realm).Append(", dzien ").Append(day - kv.Value.Start + 1).Append('/').Append(Days).Append(')');
                }
                sb.Append(" | otwarte ").Append(_opened).Append(_openTxt.Length > 0 ? " [" + _openTxt + "]" : "")
                  .Append(", zamkniete ").Append(_closed).Append(_closeTxt.Length > 0 ? " [" + _closeTxt + "]" : "")
                  .Append(", pominiete ").Append(_skipped)
                  .Append(" | oplata targowa: wozy sprzedazy ").Append(_cartN).Append(" (utarg ").Append(_cartGross).Append(", oplata ").Append(_cartFee)
                  .Append("), karawany ").Append(_carN).Append(" (utarg ").Append(_carGross).Append(", oplata ").Append(_carFee)
                  .Append("), do kas miast ").Append(_cartFee + _carFee)
                  .Append(" | popyt: dopis w miastach ").Append(_boostTowns).Append(", kategorii ").Append(_boostCats).Append(", suma dopisu ").Append(_boostSum.ToString("0.0", Inv))
                  .Append(", waga dnia ").Append(_alpha > 0f ? _alpha.ToString("0.000", Inv) : "-")
                  .Append(" | potkniecia ").Append(_stumbles).Append(" (sesja ").Append(_stumbleSess).Append(").");
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Line", e); }
            finally { ZeroDay(); }
        }

        private static string SafeSeason() { try { return WesterosClimate.Now().ToString(); } catch { return "?"; } }

        // ------------------------------------------------------------ zapis (arm_fairs): "hf1|kraina,miasto,start,pominiety;..."
        internal static string Export()
        {
            try
            {
                if (_fairs.Count == 0) return null;
                var sb = new StringBuilder("hf1|");
                foreach (var f in _fairs)
                    sb.Append(f.Realm.Replace(",", "").Replace(";", "")).Append(',').Append(f.Skipped || string.IsNullOrEmpty(f.TownId) ? "-" : f.TownId).Append(',')
                      .Append(f.Start.ToString(Inv)).Append(',').Append(f.Skipped ? '1' : '0').Append(';');
                return sb.ToString();
            }
            catch (Exception e) { Stumble("Export", e); return null; }
        }

        internal static void Import(string s)
        {
            _fairs.Clear(); _active.Clear();
            try
            {
                if (string.IsNullOrEmpty(s) || !s.StartsWith("hf1|", StringComparison.Ordinal)) return;
                foreach (var part in s.Substring(4).Split(';'))
                {
                    var c = part.Split(',');
                    int start;
                    if (c.Length < 4 || !int.TryParse(c[2], NumberStyles.Integer, Inv, out start)) continue;
                    var f = new Fair { Realm = c[0], TownId = c[1] == "-" ? null : c[1], Start = start, Skipped = c[3] == "1" || c[1] == "-" };
                    if (f.Skipped) f.Why = "z zapisu";
                    _fairs.Add(f);
                }
            }
            catch (Exception e) { Stumble("Import", e); }
        }
    }
}
