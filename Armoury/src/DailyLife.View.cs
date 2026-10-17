using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// ZC2 - WIDOK STANOW I DOSTATKU (projekt docs/PROJEKT-ZYCIE-CODZIENNE-2026-10-16.md rozdz. 12; Jeff 16.10: "pokazywac ile w danym regionie, miescie, wiosce,
    /// calym obszarze jest chlopstwa, szlachty, mieszczan, arystokracji"). Tylko odczyt liczb ostatniej doby pomiaru (DailyLife: stany z ksiegi stanow, Dostatek
    /// bez wielkich rodow - D0; dochod rodow to 1:1 brutto rodu, nieporownywalny), napisy po angielsku, bez stanu menu i bez GameMenu.SwitchToMenu (CLAUDE.md 7):
    ///  - dymek osady gry (miasto, zamek, wies): postfiks Low na TooltipRefresherCollection.RefreshSettlementTooltip i NavalDLC (ten rejestruje dymek od nowa) -
    ///    "Estates (book)" (chlopi / mieszczanie / szlachta / niewolni w %, wielkie rody - liczba) i "Livelihood" (krotnosc, slowo, dochod na glowe) pod
    ///    "Security" (warownia) albo pod "People of this land" / "Primary Production" (wies); zamek - region (zamek + jego wsie); po Alt - liczby ludzi i mieszczanie
    ///    wedlug stanow; osada ukryta dla gracza (IsSettlementInformationHidden) - bez linii;
    ///  - dymek wioski pobocznej (MapVillageTooltip i zapas w MapVillagesView): jedna linia - sklad okregu (wies gry) i jego Dostatek;
    ///  - "People of this land" (PeopleMenu): na gorze okna 2 linie - sklad stanow i Dostatek (recenzja 16.10: dluga sekcja wychodzila poza 480 px okna ROT);
    ///  - "Estates and livelihood" (wlasna pozycja w menu wsi, miasta i zamku, MBInformationManager.ShowMultiSelectionInquiry - lista przewijana): w opisie osada
    ///    ze strzalka 28 dob (pamiec Dostatku co 7 dob), wiersz na stan (ludzie, %, Dostatek; dymek - dochod na glowe, stol w kg), region, awans i spadek (ZC6),
    ///    wojna (ZC7), dobrobyt (ZC6), stol doby osady (z pola i domu / z targu), objasnienie skali i probki targu;
    ///  - "Look over the district" (DistrictMenu): w naglowku sklad i Dostatek okregu, w dymku wiersza wioski - jej ludzie wedlug stanow (dom wioski z ksiegi wiosek);
    ///  - "Realm ledger" (wlasna pozycja w menu miasta i zamku, ShowMultiSelectionInquiry): w opisie krolestwo wlasciciela osady i swiat, w liscie region
    ///    (warownia + wsie) na wiersz wedlug ludzi, sklad z liczbami w dymku.
    /// Napisy okien i dymkow bez '&lt;' i '&gt;' (RichTextParser: '&lt;' bez zamkniecia skleja reszte tekstu w jedno slowo).
    /// ZC7 (DailyLife.War.cs): w oknie Estates and livelihood wiersz blizny wojny (Dostatek przed i po najezdzie / zlupieniu, uchodzcy poza domem, zrujnowani bez zawodu);
    /// Dostatek wszedzie z uchodzcami okregu w mianowniku (Rec.AwayE - te same liczby co w logu).
    /// Klucz DailyLifeEstates (z DailyLifeMeasure) wylaczony - nic (pozycja Realm ledger ukryta, linie dymkow i sekcje nie powstaja). Przed pierwsza doba pomiaru
    /// w sesji - zdanie "not reckoned yet". Bledy liczone (potkniecia widoku w linii "Stany (ZC2)"), pierwszy w miejscu do logu; nic sie nie gasi.
    /// </summary>
    internal static partial class DailyLife
    {
        private static int _viewDay = int.MinValue;          // doba pomiaru, ktorej liczby czyta widok (sesja)
        private static int _vTip, _vTipCastle, _vSide, _vPeople, _vDistrict, _vRealm, _vEst, _vSt, _vStAll;
        private static readonly HashSet<string> _vErr = new HashSet<string>();
        private static bool _tipWired;                       // proces
        private static string _tipWhere = "nie wpiety";      // proces

        private static readonly string[] EstEn = { "slaves", "peasants", "labourers", "craftsmen", "merchants", "gentry", "great houses" };
        private static readonly string[] EstEnCap = { "Slaves", "Peasants", "Labourers", "Craftsmen", "Merchants", "Gentry", "Great houses" };
        private static readonly int[] RowOrder = { EPeasant, ELabour, ECraft, EMerchant, EGentry, ESlave, EHouse };
        private const string Legend = "Livelihood is how many times over the people can afford the bare survival basket of bread, pottage, salt and fuel at local prices: "
                                      + "below 0.8 starving, then destitute, from 1.2 modest, from 2 comfortable, from 4 well-off, from 10 rich.";
        private const string NotYet = "Not reckoned yet - the book reckons the estates at the next dawn.";

        internal static string TipWhere { get { return _tipWhere; } }

        private static bool ViewReady { get { return EstatesOn && _recs != null && _viewDay != int.MinValue; } }

        private static void ResetView()
        {
            _viewDay = int.MinValue;
            _vTip = _vTipCastle = _vSide = _vPeople = _vDistrict = _vRealm = _vEst = _vSt = _vStAll = 0;
            _vErr.Clear();
        }

        /// <summary>Compute (doba z kluczem ZC2, po LivelihoodDay): pamiec Dostatku co 7 dob i doba liczb widoku.</summary>
        private static void ViewDay(int day)
        {
            RingDay(day);
            _viewDay = day;
        }

        private static void ViewStumble(string where, Exception e)
        {
            _vSt++; _vStAll++;
            try { if (_vErr.Add(where)) Log.Error("DailyLife.View." + where, e); } catch { }
        }

        /// <summary>Fragment linii doby ZC2: uzycie widoku od ostatniej linii (liczniki od nowa), potkniecia widoku.</summary>
        private static string ViewText()
        {
            string s = "dymki osad " + _vTip + " (w tym zamki - region " + _vTipCastle + "), dymki wiosek pobocznych " + _vSide + ", People of this land " + _vPeople
                       + ", Look over the district " + _vDistrict + ", Realm ledger " + _vRealm + ", Estates and livelihood " + _vEst + " | potkniecia widoku " + _vSt + " (od wczytania " + _vStAll + ")";
            _vTip = _vTipCastle = _vSide = _vPeople = _vDistrict = _vRealm = _vEst = _vSt = 0;
            return s;
        }

        // ------------------------------------------------------------ zbiory (osada, region, krolestwo, swiat)
        private sealed class Agg
        {
            public readonly double[] N = new double[Estates];                                         // ludzie stanow (z wielkimi rodami)
            public readonly double[] NE = new double[Estates], YE = new double[Estates], YKE = new double[Estates];   // stany z liczbami dochodu
            public double Nv, Y, YK;                                                                  // bez wielkich rodow (D0)
            public double CurYK, PastYK;                                                              // strzalka: osady z wlasnym D i wpisem pamieci
            public int PastDays = -1, Places;
            public double Away;                                                                       // ZC7: uchodzcy okregow poza domem (w mianowniku D, nie w N)
        }

        private static void AddTo(Agg a, Rec r)
        {
            if (a == null || r == null) return;
            a.Places++;
            double k = r.K > 0 ? r.K : _medTownK;
            for (int e = 0; e < Estates; e++)
            {
                double n = r.N[e];
                if (!Fin(n)) continue;
                a.N[e] += n;
                if (!(n > 0) || !HasY(r, e) || !(k > 0)) continue;
                double y = r.YAvg[e];
                if (!Fin(y)) continue;
                double nd = n + r.AwayE[e];                  // ZC7: uchodzcy okregu w mianowniku bez dochodu (te same liczby co w logu)
                a.NE[e] += nd; a.YE[e] += y; a.YKE[e] += y / k;
                if (e != EHouse) { a.Nv += nd; a.Y += y; a.YK += y / k; }
            }
            if (r.Away > 0) a.Away += r.Away;
            double pd; int days;
            if (r.DSrc == 0 && r.NvH > 0 && Fin(r.YKH) && PastD0(r, out pd, out days))
            {
                a.CurYK += r.YKH; a.PastYK += pd * r.NvH;
                if (a.PastDays < 0 || days < a.PastDays) a.PastDays = days;
            }
        }

        /// <summary>Region osady: warownia (miasto, zamek) i jej wsie; wies bez regionu - ona sama.</summary>
        private static Agg RegionAgg(Rec r)
        {
            var reg = r.Region ?? r;
            var a = new Agg();
            AddTo(a, reg);
            if (reg.Villages != null) foreach (var v in reg.Villages) if (!ReferenceEquals(v, reg)) AddTo(a, v);
            return a;
        }

        /// <summary>Dostatek (D0) i dochod na glowe osady na dobe: wlasny, a gdy osada go nie ma (zamek, miasto bez liczb kiesy, wies bez ludzi) - regionu.</summary>
        private static void LivOf(Rec r, out double d, out double inc, out bool region)
        {
            region = false;
            if (r.DSrc == 0)
            {
                var a = new Agg(); AddTo(a, r);
                d = Div(a.YK, a.Nv); inc = Div(a.Y, a.Nv);
                if (Fin(d)) return;
            }
            var g = RegionAgg(r);
            d = Div(g.YK, g.Nv); inc = Div(g.Y, g.Nv); region = true;
            if (!Fin(d)) { d = r.D0; inc = double.NaN; }
        }

        private static double People(double[] n) { double t = 0; for (int e = 0; e < Estates; e++) if (Fin(n[e])) t += n[e]; return t; }

        // ------------------------------------------------------------ teksty (po angielsku)
        private static string Word(double d)
        {
            if (!Fin(d)) return "not reckoned";
            if (d < 0.8) return "Starving";
            if (d < 1.2) return "Destitute";
            if (d < 2.0) return "Modest";
            if (d < 4.0) return "Comfortable";
            if (d < 10.0) return "Well-off";
            return "Rich";
        }

        private static string LivText(double d) { return Fin(d) ? d.ToString(d < 10 ? "0.0" : "0", Inv) + "x (" + Word(d) + ")" : "not reckoned yet"; }

        private static string Num(double v) { return Fin(v) ? Math.Round(Math.Max(0.0, v)).ToString("N0", Inv) : "-"; }

        private static string PctOf(double part, double all)
        {
            if (!(all > 0) || !Fin(part)) return "-";
            double p = 100.0 * part / all;
            if (p > 0 && p < 0.01) return "under 0.01%";            // bez '<' - RichTextParser skleja reszte tekstu w jedno slowo
            return p.ToString(p >= 10 ? "0" : p >= 1 ? "0.#" : "0.##", Inv) + "%";
        }

        private static string IncShort(double yDay) { return Fin(yDay) ? Num(yDay * 364.0) + " denars a head a year" : "income not reckoned"; }

        private static string Trend(double cur, double past, int days)
        {
            if (!Fin(cur) || !Fin(past) || !(past > 0) || days <= 0) return null;
            double ch = cur / past - 1.0;
            if (Math.Abs(ch) < 0.03) return "steady over " + days + " days";
            return (ch > 0 ? "up " : "down ") + (100.0 * Math.Abs(ch)).ToString("0", Inv) + "% in " + days + " days";
        }

        private static string Trend(Agg a) { return a.PastDays > 0 ? Trend(a.CurYK, a.PastYK, a.PastDays) : null; }

        private static string Trend1(Rec r, double cur)
        {
            double pd; int days;
            return PastD0(r, out pd, out days) ? Trend(cur, pd, days) : null;
        }

        /// <summary>"peasants 95%, burghers 3.2%, gentry 1.5%" - chlopi, mieszczanie (biedota + rzemieslnicy + kupcy), szlachta, niewolni od najwiekszych;
        /// counts - z liczbami ludzi; houses - wielkie rody liczba (ponad ksiege, ulamek procenta).</summary>
        private static string GroupsText(double[] n, bool counts, bool houses)
        {
            double tot = People(n);
            if (!(tot > 0)) return null;
            var items = new List<KeyValuePair<double, string>>(4)
            {
                new KeyValuePair<double, string>(n[EPeasant], "peasants"),
                new KeyValuePair<double, string>(n[ELabour] + n[ECraft] + n[EMerchant], "burghers"),
                new KeyValuePair<double, string>(n[EGentry], "gentry"),
                new KeyValuePair<double, string>(n[ESlave], "slaves"),
            };
            items.Sort((x, y) => y.Key.CompareTo(x.Key));
            var parts = new List<string>(5);
            foreach (var it in items)
            {
                if (!(it.Key >= 0.5)) continue;
                parts.Add(counts ? it.Value + " " + Num(it.Key) + " (" + PctOf(it.Key, tot) + ")" : it.Value + " " + PctOf(it.Key, tot));
            }
            if (houses && n[EHouse] >= 0.5) parts.Add("great houses " + Num(n[EHouse]));
            return parts.Count > 0 ? string.Join(", ", parts.ToArray()) : null;
        }

        /// <summary>Dymek osady: wiersz na grupe stanow (chlopi, mieszczanie, szlachta, niewolni) od najwiekszej - sam %, po Alt "41,213 (95%)" i wszystkie grupy;
        /// bez Alt najwyzej 3 grupy od 0.1%.</summary>
        private static void GroupRows(List<TooltipProperty> rows, double[] n, string suffix, bool ext)
        {
            double tot = People(n);
            if (!(tot > 0)) return;
            var items = new List<KeyValuePair<double, string>>(4)
            {
                new KeyValuePair<double, string>(n[EPeasant], "Peasants"),
                new KeyValuePair<double, string>(n[ELabour] + n[ECraft] + n[EMerchant], "Burghers"),
                new KeyValuePair<double, string>(n[EGentry], "Gentry"),
                new KeyValuePair<double, string>(n[ESlave], "Slaves"),
            };
            items.Sort((x, y) => y.Key.CompareTo(x.Key));
            int shown = 0;
            foreach (var it in items)
            {
                if (!(it.Key >= 0.5)) continue;
                if (!ext && (shown >= 3 || it.Key < 0.001 * tot)) continue;
                rows.Add(new TooltipProperty(it.Value + suffix, ext ? Num(it.Key) + " (" + PctOf(it.Key, tot) + ")" : PctOf(it.Key, tot), 0));
                shown++;
            }
        }

        /// <summary>"41,213 peasants, 1,383 burghers, 648 gentry" (po Alt w dymku).</summary>
        private static string CountsText(double[] n)
        {
            var parts = new List<string>(5);
            double b = n[ELabour] + n[ECraft] + n[EMerchant];
            if (n[EPeasant] >= 0.5) parts.Add(Num(n[EPeasant]) + " peasants");
            if (b >= 0.5) parts.Add(Num(b) + " burghers");
            if (n[EGentry] >= 0.5) parts.Add(Num(n[EGentry]) + " gentry");
            if (n[ESlave] >= 0.5) parts.Add(Num(n[ESlave]) + " slaves");
            if (n[EHouse] >= 0.5) parts.Add(Num(n[EHouse]) + " of the great houses");
            return parts.Count > 0 ? string.Join(", ", parts.ToArray()) : null;
        }

        // ------------------------------------------------------------ dymek osady gry (latka UI - SubModuleMain, po VillageByPeople.ApplyTip)
        /// <summary>Raz na proces: postfiks Low dymka osady gry i NavalDLC (NavalDLC rejestruje dymek osady od nowa) - po linii "People of this land" GL1.</summary>
        internal static void ApplyTip(Harmony h)
        {
            if (_tipWired) return;
            _tipWired = true;
            _h = h;                                          // ZC6: postfiks dobrobytu wpinany przy starcie sesji (DailyLife.Prosperity.cs) tym samym Harmony
            var where = new List<string>();
            foreach (var tn in new[] { "TaleWorlds.CampaignSystem.ViewModelCollection.TooltipRefresherCollection", "NavalDLC.View.NavalTooltipRefresherCollection" })
            {
                try
                {
                    var t = AccessTools.TypeByName(tn);
                    var mi = t != null ? AccessTools.DeclaredMethod(t, "RefreshSettlementTooltip") : null;
                    if (mi == null) continue;
                    h.Patch(mi, postfix: new HarmonyMethod(typeof(DailyLife), nameof(TipPostfix)) { priority = Priority.Low });
                    where.Add(t.Name);
                }
                catch (Exception e) { Log.Error("DailyLife.ApplyTip(" + tn + ")", e); }
            }
            _tipWhere = where.Count > 0 ? string.Join(", ", where.ToArray()) : "NIGDZIE (brak metod dymka)";
            Log.Info(PE + "dymek osady - linie stanow i Dostatku wpiete w: " + _tipWhere + " (klucz Daily Life Estates czytany na zywo).");
        }

        /// <summary>Miejsce wstawienia: wies - za "People of this land" (GL1) albo "Primary Production"; warownia - za "Security" (brak = informacje ukryte).</summary>
        private static int TipAnchor(MBBindingList<TooltipProperty> list, Settlement st)
        {
            string a1 = null, a2 = null;
            try
            {
                if (st.IsVillage) { a1 = "People of this land"; a2 = GameTexts.FindText("str_primary_production").ToString(); }
                else a1 = GameTexts.FindText("str_security").ToString();
            }
            catch { }
            int at = -1;
            for (int i = 0; i < list.Count; i++)
            {
                var p = list[i];
                string d = p != null ? p.DefinitionLabel : null;
                if (d == null) continue;
                if ((a1 != null && d == a1) || (a2 != null && d == a2)) at = i;
            }
            return at;
        }

        public static void TipPostfix(PropertyBasedTooltipVM __0, object[] __1)
        {
            try
            {
                if (__0 == null || __1 == null || __1.Length == 0 || !ViewReady) return;
                var st = __1[0] as Settlement;
                if (st == null || !(st.IsTown || st.IsCastle || st.IsVillage)) return;
                Rec r;
                if (!_byS.TryGetValue(st, out r) || r == null) return;
                TextObject why;
                if (CampaignUIHelper.IsSettlementInformationHidden(st, out why)) return;
                var list = __0.TooltipPropertyList;
                if (list == null) return;
                int at = TipAnchor(list, st);
                if (at < 0) return;
                bool ext = __0.IsExtended;
                var rows = new List<TooltipProperty>(10);
                // krotkie wiersze (recenzja 16.10: wartosc w jednej linii bez MaxWidth poszerzala dymek ok. 2 x): stan na wiersz, sam %; po Alt - liczby, dochod, mieszczanie
                if (r.Kind == KCastle)
                {
                    var g = RegionAgg(r);
                    GroupRows(rows, g.N, " (region)", ext);
                    rows.Add(new TooltipProperty("Livelihood (region)", LivText(Div(g.YK, g.Nv)), 0));
                    if (ext)
                    {
                        double inc = Div(g.Y, g.Nv);
                        if (Fin(inc)) rows.Add(new TooltipProperty("Income per head", Num(inc * 364.0) + " denars a year", 0));
                        if (g.N[EHouse] >= 0.5) rows.Add(new TooltipProperty("Great houses", Num(g.N[EHouse]), 0));
                    }
                    _vTipCastle++;
                }
                else
                {
                    GroupRows(rows, r.N, " (book)", ext);
                    double d, inc; bool reg;
                    LivOf(r, out d, out inc, out reg);
                    rows.Add(new TooltipProperty(reg ? "Livelihood (region)" : "Livelihood", LivText(d), 0));
                    if (ext)
                    {
                        if (Fin(inc)) rows.Add(new TooltipProperty("Income per head", Num(inc * 364.0) + " denars a year", 0));
                        if (r.Kind == KTown)
                        {
                            double tot = People(r.N);
                            if (tot > 0 && r.N[ELabour] + r.N[ECraft] + r.N[EMerchant] >= 0.5)
                            {
                                rows.Add(new TooltipProperty("Labourers", PctOf(r.N[ELabour], tot), 0));
                                rows.Add(new TooltipProperty("Craftsmen", PctOf(r.N[ECraft], tot), 0));
                                rows.Add(new TooltipProperty("Merchants", PctOf(r.N[EMerchant], tot), 0));
                            }
                        }
                        if (r.N[EHouse] >= 0.5) rows.Add(new TooltipProperty("Great houses", Num(r.N[EHouse]), 0));
                    }
                }
                for (int i = 0; i < rows.Count; i++) list.Insert(at + 1 + i, rows[i]);
                _vTip++;
            }
            catch (Exception e) { ViewStumble("TipPostfix", e); }
        }

        // ------------------------------------------------------------ dymek wioski pobocznej (MapVillageTooltip, MapVillagesView)
        /// <summary>Jedna linia dymka wioski pobocznej: sklad stanow okregu (wies gry district) i jego Dostatek. null - klucz off, przed pomiarem, wies spoza spisu.</summary>
        internal static string SideTipLine(Settlement district)
        {
            try
            {
                if (district == null || !ViewReady) return null;
                Rec r;
                if (!_byS.TryGetValue(district, out r) || r == null) return null;
                string gl = GroupsText(r.N, false, false);
                if (gl == null) return null;
                double d, inc; bool reg;
                LivOf(r, out d, out inc, out reg);
                _vSide++;
                return "Estates: " + gl + " | Livelihood " + LivText(d);
            }
            catch (Exception e) { ViewStumble("SideTipLine", e); return null; }
        }

        // ------------------------------------------------------------ "Look over the district" (DistrictMenu)
        /// <summary>Linia naglowka okna okregu: sklad stanow okregu z liczbami i jego Dostatek. null - klucz off albo nie wies.</summary>
        internal static string DistrictLine(Settlement st)
        {
            try
            {
                if (st == null || !st.IsVillage || !EstatesOn) return null;
                _vDistrict++;
                Rec r;
                if (!ViewReady || !_byS.TryGetValue(st, out r) || r == null) return "Estates of the district (book): " + NotYet;
                string gl = GroupsText(r.N, true, true);
                if (gl == null) return null;
                double d, inc; bool reg;
                LivOf(r, out d, out inc, out reg);
                return "Estates of the district (book): " + gl + " - Livelihood " + LivText(d) + (Fin(inc) ? ", " + IncShort(inc) : "") + (reg ? " (the region's)" : "") + ".";
            }
            catch (Exception e) { ViewStumble("DistrictLine", e); return null; }
        }

        /// <summary>Dymek wiersza wioski pobocznej w oknie okregu: jej ludzie w domu (ksiega wiosek) wedlug stanow okregu. Wies gry (main) - null.</summary>
        internal static string DistrictRowCounts(Settlement st, string uid, bool main)
        {
            try
            {
                if (main || uid == null || st == null || !ViewReady) return null;
                Rec r;
                if (!_byS.TryGetValue(st, out r) || r == null) return null;
                VillageBook.VState vs;
                if (!VillageBook.StateOf(uid, out vs) || !(vs.Home > 0)) return null;
                double tot = r.N[ESlave] + r.N[EPeasant] + r.N[ELabour] + r.N[ECraft] + r.N[EMerchant] + r.N[EGentry];
                if (!(tot > 0)) return null;
                var n = new double[Estates];
                for (int e = 0; e < 6; e++) n[e] = vs.Home * r.N[e] / tot;
                string c = CountsText(n);
                return c != null ? "By estate (book): " + c : null;
            }
            catch (Exception e) { ViewStumble("DistrictRowCounts", e); return null; }
        }

        // ------------------------------------------------------------ "People of this land" (PeopleMenu) - krotko na gorze okna
        /// <summary>Jedna-dwie linie na GORZE okna People of this land: sklad stanow osady (zamek - regionu) i Dostatek; reszta w oknie "Estates and livelihood"
        /// (recenzja 16.10: dluga sekcja w oknie ShowInquiry wychodzila poza 480 px prefabu ROT, gdzie Gauntlet nie lamie juz wierszy). null - klucz off.</summary>
        internal static string MenuSummary(Settlement st)
        {
            if (st == null || !EstatesOn) return null;
            try
            {
                _vPeople++;
                Rec r;
                if (!ViewReady || !_byS.TryGetValue(st, out r) || r == null) return "Estates (book): " + NotYet;
                bool castle = r.Kind == KCastle;
                double[] n; double d;
                if (castle) { var g = RegionAgg(r); n = g.N; d = Div(g.YK, g.Nv); }
                else { double inc; bool reg; LivOf(r, out d, out inc, out reg); n = r.N; }
                string gl = GroupsText(n, false, false);
                return (castle ? "Estates of the region (book): " : "Estates (book): ") + (gl ?? "no people of the book") + " - Livelihood " + LivText(d)
                       + ".\nThe whole book of the estates: 'Estates and livelihood' in this menu.";
            }
            catch (Exception e) { ViewStumble("MenuSummary", e); return null; }
        }

        // ------------------------------------------------------------ "Estates and livelihood" (menu wsi, miasta i zamku) - lista przewijana
        /// <summary>Wiersz okna z lista (MultiSelection): krotki tytul (dwie linie w wierszu 65 px) i dymek ze szczegolami. Bez znakow '&lt;' i '&gt;' (RichText).</summary>
        private static InquiryElement Row(string title, string hint)
        {
            return new InquiryElement(title, title, null, true, hint);
        }

        /// <summary>Okno "Estates and livelihood": opis - osada w jednym zdaniu; wiersze - stany (ludzie, %, Dostatek; dymek: dochod, stol), region, awans i spadek,
        /// wojna, dobrobyt, stol doby, objasnienie liczb. Lista przewijana (MultiSelectionQueryPopup), wiec dlugosc nie wychodzi poza okno.</summary>
        private static void EstatesWindow(Settlement st)
        {
            string name = st.Name != null ? st.Name.ToString() : "this place";
            string title = "Estates and livelihood - " + name;
            Rec r;
            if (!ViewReady || !_byS.TryGetValue(st, out r) || r == null)
            {
                InformationManager.ShowInquiry(new InquiryData(title, NotYet, true, false, "Close", null, delegate { }, null), true);
                return;
            }
            var list = new List<InquiryElement>(16);
            var desc = new StringBuilder(400);
            if (r.Kind == KCastle)
            {
                var g = RegionAgg(r);
                double d = Div(g.YK, g.Nv), inc = Div(g.Y, g.Nv);
                string tr = Trend(g);
                desc.Append("The people of this castle live in its villages: ").Append(Num(People(g.N))).Append(" people of the book - Livelihood ").Append(LivText(d))
                    .Append(tr != null ? ", " + tr : "").Append(Fin(inc) ? "; income per head " + Num(inc * 364.0) + " denars a year" : "").Append('.');
                EstateElements(list, g, null);
            }
            else
            {
                var a = new Agg(); AddTo(a, r);
                double d, inc; bool reg;
                LivOf(r, out d, out inc, out reg);
                string tr = Trend1(r, d);
                desc.Append("Here: ").Append(Num(People(r.N))).Append(" people of the book - Livelihood ").Append(LivText(d)).Append(reg ? " (the region's)" : "")
                    .Append(tr != null ? ", " + tr : "").Append(Fin(inc) ? "; income per head " + Num(inc * 364.0) + " denars a year" : "")
                    .Append(r.YDays > 0 && r.YDays < WarmDays ? " (reckoned over " + r.YDays + (r.YDays == 1 ? " day" : " days") + " only - rough)" : "").Append('.');
                EstateElements(list, a, r);
                var reg0 = r.Region ?? r;
                if (r.Kind == KTown || !ReferenceEquals(reg0, r)) RegionElement(list, reg0, RegionAgg(r));
                string mt = MobilityText(r);                 // ZC6: kto awansowal i spadl w ostatnich 28 dobach
                if (mt != null)
                {
                    int c = mt.IndexOf(": ", StringComparison.Ordinal);
                    if (c > 0) list.Add(Row(mt.Substring(0, c), mt.Substring(c + 2).Replace(", ", ",\n")));
                    else list.Add(Row("Rose and fell between the estates", mt));
                }
                string wt = WarViewText(r);                  // ZC7: blizna wojny - Dostatek przed i po, uchodzcy, zrujnowani bez zawodu
                if (wt != null) list.Add(Row("War has left its mark here", (wt.StartsWith("War: ", StringComparison.Ordinal) ? wt.Substring(5) : wt).Replace(". ", ".\n")));
            }
            string ps = ProsperityShort(r);                  // ZC6: dobrobyt za Dostatkiem regionu
            if (ps != null) list.Add(Row("Prosperity of the stronghold: " + ps, ProsperityViewText(r)));
            if (r.Kind != KCastle) { string tt = TableText(r); if (tt != null) list.Add(Row("Daily table here today", tt)); }
            list.Add(Row("What the numbers mean", Legend + "\n" + ScaleText()));
            desc.Append("\nHover over a row for more.");
            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(title, desc.ToString(), list, false, 0, 1, "Close", null, delegate { }, null), false);
        }

        private static string ScaleText()
        {
            return "One sack on the market stands for about " + (_sigma > 0 ? _sigma : 37) + " sacks of the land's real trade; the book counts all the people of the land, not only the village or town on the map.";
        }

        /// <summary>Wiersze stanow: tytul - ludzie, %, Dostatek; dymek - Dostatek, dochod na glowe, stol w kg (osada), konto niewolnych ksiegi.</summary>
        private static void EstateElements(List<InquiryElement> list, Agg a, Rec own)
        {
            double tot = People(a.N);
            if (!(tot > 0)) return;
            double[][] b = own != null ? BasketOf(own.Cul) : null;
            foreach (int e in RowOrder)
            {
                double n = a.N[e];
                if (!(n >= 0.5)) continue;
                if (e == EHouse)
                {
                    list.Add(Row("Great houses: " + Num(n), "Kin and households of the lords who hold fiefs here (three for each living member of the house); they live off their lord's purse."));
                    continue;
                }
                var hint = new List<string>(4);
                string t = EstEnCap[e] + ": " + Num(n) + " (" + PctOf(n, tot) + ")";
                if (e == ESlave)
                {
                    hint.Add("Fed by their owners.");
                    if (own != null && own.Thralls >= 0.5) hint.Add(Num(own.Thralls) + " of them were taken in raids and wars.");
                }
                else
                {
                    double de = Div(a.YKE[e], a.NE[e]), ie = Div(a.YE[e], a.NE[e]);
                    t += " - " + LivText(de);
                    hint.Add("Livelihood " + LivText(de));
                    if (Fin(ie)) hint.Add(IncShort(ie));
                }
                if (b != null)
                {
                    double kg = 0, l = 0;
                    for (int g = 0; g < Groups; g++) { if (IsDrink(g)) l += b[e][g]; else kg += b[e][g]; }
                    hint.Add("Daily table: " + kg.ToString("0.00", Inv) + " kg of food and " + l.ToString("0.00", Inv) + " l of drink a head");
                }
                list.Add(Row(t, string.Join("\n", hint.ToArray())));
            }
        }

        /// <summary>Wiersz regionu (warownia i jej wsie): tytul - ludzie i Dostatek; dymek - sklad z liczbami, strzalka 28 dob, dochod na glowe.</summary>
        private static void RegionElement(List<InquiryElement> list, Rec reg, Agg g)
        {
            double tot = People(g.N);
            if (!(tot > 0)) return;
            double d = Div(g.YK, g.Nv), inc = Div(g.Y, g.Nv);
            string tr = Trend(g), gl = GroupsText(g.N, true, true);
            var hint = new List<string>(4);
            hint.Add(reg.Kind == KVillage ? "This district." : "The stronghold and its villages.");
            if (gl != null) hint.Add(gl);
            hint.Add("Livelihood " + LivText(d) + (tr != null ? ", " + tr : ""));
            if (Fin(inc)) hint.Add(IncShort(inc));
            list.Add(Row("The region of " + reg.Name + ": " + Num(tot) + " people - " + LivText(d), string.Join("\n", hint.ToArray())));
        }

        /// <summary>Stol doby osady (koszyki stanow x ludzie): jedzenie w t i napoje w l, z pola i domu / z targu (wies - z pola tylko najedzeni ze spichlerza).</summary>
        private static string TableText(Rec r)
        {
            var b = BasketOf(r.Cul);
            bool town = r.Kind == KTown;
            double kg = 0, kgM = 0, kgF = 0, l = 0;
            for (int e = 0; e < Estates; e++)
            {
                double n = r.N[e];
                if (!(n > 0)) continue;
                double fed = r.Kind == KVillage && e != EHouse ? r.Fed : 1.0;
                for (int g = 0; g < Groups; g++)
                {
                    double q = n * b[e][g];
                    if (!(q > 0)) continue;
                    if (IsDrink(g)) { l += q; continue; }
                    double m = q * Mk(e, town, g);
                    kg += q; kgM += m; kgF += (q - m) * fed;
                }
            }
            if (!(kg > 0)) return null;
            double t = kg / 1000.0;
            var sb = new StringBuilder(240);
            sb.Append(t >= 10 ? Num(t) : t.ToString("0.0", Inv)).Append(" t of food and ").Append(Num(l)).Append(" l of drink.\nOwn fields and homes ")
              .Append(PctOf(kg - kgM, kg)).Append(", market ").Append(PctOf(kgM, kg)).Append('.');
            double hungry = kg - kgM - kgF;
            if (hungry > 0.005 * kg) sb.Append("\nThe granary falls short of ").Append(PctOf(hungry, kg)).Append(" of it.");
            return sb.ToString();
        }

        // ------------------------------------------------------------ pozycje menu: "Estates and livelihood" (wies, miasto, zamek) i "Realm ledger" (miasto i zamek)
        /// <summary>OnSessionLaunched (po DistrictMenu.AddMenus): "Estates and livelihood" w menu wsi (takze spladrowanej), miasta i zamku za "People of this land"
        /// i "Realm ledger" w menu miasta i zamku za nim. Okna ShowMultiSelectionInquiry (lista przewijana), bez GameMenu.SwitchToMenu.</summary>
        internal static void AddMenus(CampaignGameStarter starter)
        {
            try
            {
                foreach (var menu in new[] { "town", "castle" })
                    starter.AddGameMenuOption(menu, "arm_realm_ledger_" + menu, "{=!}Realm ledger", RealmCondition, RealmConsequence, false, 7);
                foreach (var menu in new[] { "village", "town", "castle" })
                    starter.AddGameMenuOption(menu, "arm_estates_" + menu, "{=!}Estates and livelihood", RealmCondition, EstatesConsequence, false, 7);
                starter.AddGameMenuOption("village_looted", "arm_estates_village_looted", "{=!}Estates and livelihood", RealmCondition, EstatesConsequence, false, 2);
                Log.Info(PE + "pozycja \"Realm ledger\" w menu miasta i zamku i \"Estates and livelihood\" w menu wsi, miasta i zamku wpiete (okna ShowMultiSelectionInquiry - lista przewijana,"
                         + " bez GameMenu.SwitchToMenu; widoczne przy kluczu Daily Life Estates).");
            }
            catch (Exception e) { Log.Error("DailyLife.AddMenus", e); }
        }

        private static bool RealmCondition(MenuCallbackArgs args)
        {
            try
            {
                args.optionLeaveType = GameMenuOption.LeaveType.Manage;
                return EstatesOn && Settlement.CurrentSettlement != null;
            }
            catch { return false; }
        }

        private static void EstatesConsequence(MenuCallbackArgs args)
        {
            try
            {
                var st = Settlement.CurrentSettlement;
                if (st == null) return;
                _vEst++;
                EstatesWindow(st);
            }
            catch (Exception e) { ViewStumble("EstatesConsequence", e); }
        }

        private static void RealmConsequence(MenuCallbackArgs args)
        {
            try
            {
                var st = Settlement.CurrentSettlement;
                if (st == null) return;
                _vRealm++;
                string title, desc;
                var list = new List<InquiryElement>(64);
                RealmText(st, out title, out desc, list);
                if (list.Count == 0) { InformationManager.ShowInquiry(new InquiryData(title, desc, true, false, "Close", null, delegate { }, null), true); return; }
                MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(title, desc, list, false, 0, 1, "Close", null, delegate { }, null), false);
            }
            catch (Exception e) { ViewStumble("RealmConsequence", e); }
        }

        /// <summary>Okno Realm ledger: krolestwo wlasciciela osady (region osady liczy sie do krolestwa wlasciciela warowni) - w opisie krolestwo i swiat, w liscie
        /// region na wiersz (wedlug ludzi; dymek - sklad z liczbami, Dostatek, dochod) i objasnienie liczb.</summary>
        private static void RealmText(Settlement st, out string title, out string desc, List<InquiryElement> list)
        {
            title = "Realm ledger";
            Rec r;
            if (!ViewReady || !_byS.TryGetValue(st, out r) || r == null) { desc = NotYet; return; }
            string kid = r.KingdomId ?? "-";
            string kname = kid == "-" ? "the lands without a realm" : r.KingdomName;
            title = "Realm ledger - " + (kid == "-" ? "lands without a realm" : kname);
            var regs = new Dictionary<Rec, Agg>();
            var realm = new Agg();
            var world = new Agg();
            foreach (var x in _recs)
            {
                AddTo(world, x);
                if ((x.KingdomId ?? "-") != kid) continue;
                AddTo(realm, x);
                var reg = x.Region ?? x;
                Agg a;
                if (!regs.TryGetValue(reg, out a)) { a = new Agg(); regs[reg] = a; }
                AddTo(a, x);
            }
            var sb = new StringBuilder(800);
            sb.Append("The people of ").Append(kname).Append(" (book), as reckoned at the last dawn:\n");
            AggLine(sb, "The whole realm", realm);
            AggLine(sb, "The whole world", world);
            sb.Append(regs.Count).Append(regs.Count == 1 ? " region" : " regions").Append(" (the stronghold and its villages), most people first - hover over a row for its estates.");
            desc = sb.ToString();
            var lst = new List<KeyValuePair<Rec, Agg>>(regs);
            lst.Sort((x, y) => People(y.Value.N).CompareTo(People(x.Value.N)));
            foreach (var kv in lst)
            {
                var a = kv.Value;
                double tot = People(a.N), d = Div(a.YK, a.Nv), inc = Div(a.Y, a.Nv);
                string tr = Trend(a), gl = GroupsText(a.N, true, true);
                var hint = new List<string>(4);
                if (gl != null) hint.Add(gl);
                hint.Add("Livelihood " + LivText(d) + (tr != null ? ", " + tr : ""));
                if (Fin(inc)) hint.Add(IncShort(inc));
                list.Add(Row(kv.Key.Name + ": " + Num(tot) + " people - " + LivText(d), string.Join("\n", hint.ToArray())));
            }
            if (list.Count > 0) list.Add(Row("What the numbers mean", Legend + "\n" + ScaleText()));
        }

        /// <summary>Linia zbioru w opisie okna: ludzie, sklad w %, Dostatek ze strzalka, dochod na glowe.</summary>
        private static void AggLine(StringBuilder sb, string name, Agg a)
        {
            double tot = People(a.N);
            double d = Div(a.YK, a.Nv), inc = Div(a.Y, a.Nv);
            string tr = Trend(a), gl = GroupsText(a.N, false, true);
            sb.Append(name).Append(": ").Append(Num(tot)).Append(" people").Append(gl != null ? " - " + gl : "")
              .Append(" - ").Append(LivText(d)).Append(tr != null ? ", " + tr : "").Append(Fin(inc) ? "; " + IncShort(inc) : "").Append(".\n");
        }
    }
}
