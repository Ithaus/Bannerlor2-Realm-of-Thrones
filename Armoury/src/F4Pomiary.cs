using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// f4-pomiary (noc 17/18.10, koncowy przeglad ekonomii, pakiet P6) - ZELAZO I DREWNO Z POLEK, DREWNO W WOZACH WSI (klucz Measure Iron Wood Flow). Tylko log,
    /// niczego nie zmienia. Dwa pytania przegladu:
    ///  (1) "Zelazo: produkcja KS (257/d) przewyzsza zuzycie KS (237/d), a zapas (polki miast) i tak spada - kto zdejmuje zelazo z polek poza ksiega KS?"
    ///      Linia "Surowiec z polek (f4-pomiary)" co 7 dob dla rudy (iron) i drewna (hardwood) z liczb ksiegi towarow (GoodsLedger - ta sama doba, kopia liczb):
    ///      zapas swiata na poczatku i koncu okna = produkcja - zuzycie + bez wyjasnienia (tozsamosc ksiegi); polki miast wobec ksiegi: zmiana polek - (produkcja -
    ///      zuzycie) = bez wyjasnienia - zmiana pozostalych posiadaczy (wsie, wozy wsi, wozy surowcow, karawany, lordowie ...) - czyli dokad towar odplywa z polek,
    ///      choc ksiega KS widzi nadwyzke; zuzycie wedlug ujscia (kto zdejmuje z polek) i produkcja wedlug zrodla w sumie okna, przewoz wozami i karawanami.
    ///      Recenzja (noc 17/18.10): "kontrola" w nawiasie to tylko arytmetyka linii (zapas swiata = suma posiadaczy, wiec wychodzi 0 z definicji; inna niz 0 tylko przy
    ///      rozjezdzie tablicy posiadaczy i ich nazw) - prawdziwa miara to "bez wyjasnienia" okna (ksiega towarow domyka sie co dobe: 0; checker NF4-8).
    ///  (2) "Drewno stoi w wozach (37 tys.), a 56 miast nie ma opalu - czemu nie dojezdza?" Linia "Drewno w wozach wsi (f4-pomiary)" co 7 dob: przebieg raz na dobe po
    ///      taborach wsi (MobileParty.AllVillagerParties): drewno w wozach wedlug stanu (w drodze do miasta / zamku / domu, stoi we wsi, w miescie), wiek ladunku (doby od
    ///      zaladunku, od wczytania), dostawy do miast w oknie (spadek drewna w wozie przypisany ostatnio odwiedzonej osadzie), miasta bez opalu (polka nie wyzej niz
    ///      zapas rzemiosla - ten sam prog co opal mieszczan: max(5, CaravanBulk.TargetFor)) z przyczyna: woz w drodze, dowieziono a rzemioslo zabralo, brak wsi z drewnem
    ///      w targu; drewno na polkach ponad zapas rzemiosla wedlug rynku.
    /// Stan tylko w pamieci (bez zapisu): po wczytaniu okno i wiek ladunku od nowa. Koszt: przebieg po taborach, miastach i wsiach raz na dobe (zegar w linii).
    /// Recenzja: doba ksiegi towarow pominieta (ksiega wylaczona i wlaczona, blad doby) - okno, "wczoraj" posiadaczy i pamiec wozow od nowa (inaczej zmiana zapasu z wielu
    /// dob weszlaby w "bez wyjasnienia" jednej doby, a spadek w wozie z wielu dob w dostawy jednej); licznik "przerwy ksiegi" w linii drewna.
    /// </summary>
    internal static class F4Pomiary
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        internal static bool IronWoodOn { get { var s = Settings.Current; return s != null && s.MeasureIronWoodFlow; } }
        private const int ShelfKeep = 5;                    // ten sam prog co TownFolk.Firewood (ShelfKeep) - opal tylko ponad max(5, zapas rzemiosla)

        private static object _camp;
        private static bool _wasOff;                        // klucz byl wylaczony - po wlaczeniu stan od zera (bez starego "wczoraj")
        private static ItemObject _iron, _wood;
        private static int _st, _stDay;
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static int _itemDay = -1, _endDay = -1, _gaps;   // recenzja: ostatnia doba ksiegi towarow widziana przez LedgerItem i LedgerDayEnd, przerwy w sesji

        // ------------------------------------------------------------ (1) przeplyw z ksiegi towarow
        private sealed class Flow
        {
            public string Id;
            public long[] HoldPrev, Hold0, Hold1; public string[] HName;
            public long StockPrev = -1, Stock0, Stock1, P, C, U; public int Days, First = -1;
            public readonly Dictionary<string, long> Src = new Dictionary<string, long>(StringComparer.Ordinal), Sink = new Dictionary<string, long>(StringComparer.Ordinal);
            public readonly Dictionary<string, long> SinkSub = new Dictionary<string, long>(StringComparer.Ordinal);
            public long TWagon, TCarBuy, TCarSell;
            public void Clear() { Days = 0; First = -1; P = C = U = 0; Src.Clear(); Sink.Clear(); SinkSub.Clear(); TWagon = TCarBuy = TCarSell = 0; Hold0 = Hold1 = null; }
        }
        private static readonly Flow[] _flow = { new Flow { Id = "iron" }, new Flow { Id = "hardwood" } };

        // ------------------------------------------------------------ (2) wozy wsi
        private sealed class Wag { public int Wood = -1, Iron = -1, WoodSince = -1; }
        private static Dictionary<MobileParty, Wag> _wag = new Dictionary<MobileParty, Wag>();
        private static readonly Dictionary<Town, long> _deliv = new Dictionary<Town, long>();
        private static readonly HashSet<Village> _woodVil = new HashSet<Village>();
        private static long _soldTown, _soldCastle, _soldVillage, _soldOther, _loaded, _ironSoldTown;
        private static int _winDays, _winFirst = -1;
        private static bool _init;
        private static long _ticks;

        private static void Stumble(string where, Exception e)
        {
            _st++; _stDay++;
            try { if (_err.Add(where)) Log.Error("F4Pomiary." + where, e); } catch { }
        }

        /// <summary>Nowa kampania albo wczytanie (inny obiekt Campaign) - stan pomiaru od zera (bez zapisu; bez wpisu w konstruktorze zachowania).</summary>
        private static void EnsureCampaign()
        {
            var c = Campaign.Current;
            if (ReferenceEquals(c, _camp) && !_wasOff) return;
            _camp = c; _wasOff = false;
            _iron = _wood = null;
            foreach (var f in _flow) { f.Clear(); f.HoldPrev = null; f.StockPrev = -1; f.HName = null; }
            _wag = new Dictionary<MobileParty, Wag>();
            ClearWin();
            _init = false; _st = 0; _err.Clear();
            _itemDay = _endDay = -1; _gaps = 0;
        }

        /// <summary>Recenzja: LedgerItem - pierwsza wizyta doby; doba pominieta (ksiega towarow wylaczona albo blad doby) = przeplywy od nowa (stan poczatkowy dzis).</summary>
        private static void ItemDay(int day)
        {
            if (day == _itemDay) return;
            if (_itemDay >= 0 && day != _itemDay + 1)
                foreach (var f in _flow) { f.Clear(); f.HoldPrev = null; f.StockPrev = -1; }
            _itemDay = day;
        }

        /// <summary>Recenzja: LedgerDayEnd - doba pominieta = pamiec wozow i okno od nowa (dzisiejszy przebieg to stan poczatkowy, jak po wczytaniu).</summary>
        private static void EndDay(int day)
        {
            if (_endDay >= 0 && day != _endDay + 1)
            {
                _wag = new Dictionary<MobileParty, Wag>();
                ClearWin();
                _init = false; _gaps++;
                foreach (var f in _flow) f.Clear();   // okno przeplywow od jutra razem z oknem wozow ("wczoraj" posiadaczy = dzisiejszy stan z LedgerItem; bez dzisiejszej doby)
            }
            _endDay = day;
        }

        private static void ClearWin()
        {
            _deliv.Clear(); _woodVil.Clear();
            _soldTown = _soldCastle = _soldVillage = _soldOther = _loaded = _ironSoldTown = 0;
            _winDays = 0; _winFirst = -1; _ticks = 0; _stDay = 0;
        }

        private static bool EnsureItems()
        {
            if (_iron != null && _wood != null) return true;
            try { var om = MBObjectManager.Instance; if (om != null) { _iron = om.GetObject<ItemObject>("iron"); _wood = om.GetObject<ItemObject>("hardwood"); } } catch { }
            return _iron != null && _wood != null;
        }

        /// <summary>GoodsLedger.Daily, petla towarow (po KsNote): kopia liczb ksiegi doby dla rudy i drewna. Inne towary - jedno porownanie.</summary>
        internal static void LedgerItem(ItemObject it, int day, long now, long p, long c, long[] hold, string[] hname, Dictionary<string, Dictionary<string, long>> src,
                                        Dictionary<string, Dictionary<string, long>> sink, long tWagon, long tCarBuy, long tCarSell)
        {
            if (it == null) return;
            if (!IronWoodOn) { _wasOff = true; return; }
            try
            {
                EnsureCampaign();
                if (!EnsureItems()) return;
                ItemDay(day);   // recenzja: doba ksiegi pominieta - przeplywy od nowa
                int k = ReferenceEquals(it, _iron) ? 0 : ReferenceEquals(it, _wood) ? 1 : -1;
                if (k < 0 || hold == null) return;
                var f = _flow[k];
                f.HName = hname;
                if (f.HoldPrev == null) { f.HoldPrev = (long[])hold.Clone(); f.StockPrev = now; return; }   // pierwsza doba sesji - stan poczatkowy okna
                if (f.Days == 0) { f.Hold0 = f.HoldPrev; f.Stock0 = f.StockPrev; f.First = day; }
                f.P += p; f.C += c; f.U += (now - f.StockPrev) - (p - c);
                if (src != null) foreach (var kv in src) { long s = 0; if (kv.Value != null) foreach (var x in kv.Value) s += x.Value; Add(f.Src, kv.Key, s); }
                if (sink != null)
                    foreach (var kv in sink)
                    {
                        long s = 0;
                        if (kv.Value != null) foreach (var x in kv.Value) { s += x.Value; if (!string.IsNullOrEmpty(x.Key)) Add(f.SinkSub, kv.Key + " (" + x.Key + ")", x.Value); }
                        Add(f.Sink, kv.Key, s);
                    }
                f.TWagon += tWagon; f.TCarBuy += tCarBuy; f.TCarSell += tCarSell;
                f.Hold1 = (long[])hold.Clone(); f.Stock1 = now;
                f.HoldPrev = f.Hold1; f.StockPrev = now;
                f.Days++;
            }
            catch (Exception e) { Stumble("LedgerItem", e); }
        }

        private static void Add(Dictionary<string, long> d, string k, long v) { long o; d.TryGetValue(k ?? "?", out o); d[k ?? "?"] = o + v; }

        /// <summary>GoodsLedger.Daily po petli towarow (doba day = zakonczona): przebieg po taborach, wsiach i miastach; co 7 dob linie pomiaru.</summary>
        internal static void LedgerDayEnd(int day)
        {
            if (!IronWoodOn) { _wasOff = true; return; }
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                EnsureCampaign();
                if (!EnsureItems()) return;
                EndDay(day);    // recenzja: doba ksiegi pominieta - pamiec wozow i okno od nowa
                bool first = !_init;
                _init = true;
                if (!first && _winDays == 0) _winFirst = day;
                // ---- tabory wsi: drewno i ruda w wozie, dostawy (spadek w wozie) i wiek ladunku
                var next = new Dictionary<MobileParty, Wag>(_wag.Count + 16);
                foreach (var mp in MobileParty.AllVillagerParties)
                {
                    if (mp == null || !mp.IsActive) continue;
                    try
                    {
                        var r = mp.ItemRoster;
                        int w = r != null ? r.GetItemNumber(_wood) : 0, fe = r != null ? r.GetItemNumber(_iron) : 0;
                        Wag rec; if (!_wag.TryGetValue(mp, out rec)) rec = new Wag();
                        if (!first && rec.Wood >= 0)
                        {
                            int dw = rec.Wood - w;
                            if (dw > 0)
                            {
                                var ls = mp.LastVisitedSettlement;
                                if (ls != null && ls.IsTown && ls.Town != null) { _soldTown += dw; long o; _deliv.TryGetValue(ls.Town, out o); _deliv[ls.Town] = o + dw; }
                                else if (ls != null && ls.IsCastle) _soldCastle += dw;
                                else if (ls != null && ls.IsVillage) _soldVillage += dw;
                                else _soldOther += dw;
                            }
                            else if (dw < 0) _loaded += -dw;
                            if (rec.Iron >= 0 && fe < rec.Iron) { var ls = mp.LastVisitedSettlement; if (ls != null && ls.IsTown) _ironSoldTown += rec.Iron - fe; }
                        }
                        if (w > 0) { if (rec.WoodSince < 0) rec.WoodSince = day; var hv = mp.HomeSettlement != null ? mp.HomeSettlement.Village : null; if (hv != null) _woodVil.Add(hv); }
                        else rec.WoodSince = -1;
                        rec.Wood = w; rec.Iron = fe;
                        next[mp] = rec;
                    }
                    catch (Exception e) { Stumble("LedgerDayEnd(tabor)", e); }
                }
                _wag = next;
                // ---- wsie z drewnem w magazynie (okno)
                foreach (var v in Village.All)
                {
                    try { if (v != null && v.Settlement != null && v.Settlement.ItemRoster != null && v.Settlement.ItemRoster.GetItemNumber(_wood) > 0) _woodVil.Add(v); }
                    catch (Exception e) { Stumble("LedgerDayEnd(wies)", e); }
                }
                if (!first) _winDays++;
                _ticks += Stopwatch.GetTimestamp() - t0;
                if (first || _winDays < 7) return;
                long tl = Stopwatch.GetTimestamp();
                for (int k = 0; k < _flow.Length; k++) { try { Log.Info(FlowLine(day, _flow[k])); } catch (Exception e) { Stumble("FlowLine", e); } }
                try { Log.Info(WoodLine(day, tl)); } catch (Exception e) { Stumble("WoodLine", e); }
            }
            catch (Exception e) { Stumble("LedgerDayEnd", e); }
            if (_winDays >= 7) { foreach (var f in _flow) f.Clear(); ClearWin(); }
        }

        private static string FlowLine(int day, Flow f)
        {
            var sb = new StringBuilder(1800);
            sb.Append("Surowiec z polek (f4-pomiary): dzien ").Append(day).Append(" - ").Append(f.Id).Append(" | okno ").Append(f.Days).Append(" dob od dnia ").Append(f.First);
            if (f.Days == 0 || f.Hold0 == null || f.Hold1 == null || f.HName == null) { sb.Append(" | brak liczb ksiegi towarow w oknie (ksiega wylaczona albo towar nieznany)."); return sb.ToString(); }
            long d = f.Stock1 - f.Stock0;
            sb.Append(" | zapas swiata ").Append(f.Stock0).Append(" -> ").Append(f.Stock1).Append(" (zmiana ").Append(S(d)).Append(" = produkcja ").Append(f.P).Append(" - zuzycie ").Append(f.C)
              .Append(" + bez wyjasnienia ").Append(S(f.U)).Append(')');
            int town = Array.IndexOf(f.HName, "miasta");
            long dt = town >= 0 && town < f.Hold1.Length ? f.Hold1[town] - f.Hold0[town] : 0;
            long others = 0;
            var oth = new StringBuilder(300);
            for (int h = 0; h < f.Hold1.Length && h < f.HName.Length; h++)
            {
                if (h == town) continue;
                long dh = f.Hold1[h] - (h < f.Hold0.Length ? f.Hold0[h] : 0);
                others += dh;
                if (dh != 0 || f.Hold1[h] != 0) oth.Append(oth.Length > 0 ? ", " : "").Append(f.HName[h]).Append(' ').Append(f.Hold1[h]).Append(" (").Append(S(dh)).Append(')');
            }
            long gap = dt - (f.P - f.C);
            sb.Append(" | polki miast ").Append(town >= 0 ? f.Hold0[town] : 0).Append(" -> ").Append(town >= 0 ? f.Hold1[town] : 0).Append(" (zmiana ").Append(S(dt)).Append(')')
              .Append("; ksiega (produkcja - zuzycie) ").Append(S(f.P - f.C)).Append("; polki - ksiega ").Append(S(gap)).Append(" = bez wyjasnienia ").Append(S(f.U))
              .Append(" - zmiana pozostalych posiadaczy ").Append(S(others)).Append(" (kontrola arytmetyki linii ").Append(S(gap - (f.U - others))).Append(')')   // recenzja: 0 z definicji (zapas = suma posiadaczy) - miara to "bez wyjasnienia"
              .Append(" | pozostali posiadacze (koniec okna, zmiana): ").Append(oth.Length > 0 ? oth.ToString() : "-");
            sb.Append(" | zuzycie wedlug ujscia (suma okna, kto zdejmuje): ").Append(Top(f.Sink, 12)).Append(" | najwieksze pozycje: ").Append(Top(f.SinkSub, 6))
              .Append(" | produkcja wedlug zrodla: ").Append(Top(f.Src, 8))
              .Append(" | przewoz: wozy do osad ").Append(f.TWagon).Append(", karawany kupily ").Append(f.TCarBuy).Append(", sprzedaly ").Append(f.TCarSell).Append('.');
            return sb.ToString();
        }

        private static string WoodLine(int day, long tl)
        {
            // ---- stan wozow dzis: drewno wedlug stanu i celu, wiek ladunku
            long inWag = 0, toTown = 0, toCastle = 0, toHome = 0, toOther = 0, noTarget = 0, atHome = 0, atTown = 0, atOther = 0, ironWag = 0, ironToTown = 0;
            int nWood = 0, nIron = 0, nToTown = 0, nAtHome = 0;
            var ages = new List<int>(512); var speeds = new List<float>(512);
            long old14 = 0;
            var inbound = new Dictionary<Town, long>();
            foreach (var kv in _wag)
            {
                var mp = kv.Key; var rec = kv.Value;
                try
                {
                    if (rec.Iron > 0) { nIron++; ironWag += rec.Iron; var ti = mp.TargetSettlement; if (mp.CurrentSettlement == null && ti != null && ti.IsTown) ironToTown += rec.Iron; }
                    if (rec.Wood <= 0) continue;
                    nWood++; inWag += rec.Wood;
                    int age = rec.WoodSince >= 0 ? day - rec.WoodSince : 0;
                    for (int i = 0; i < Math.Min(rec.Wood, 1000); i += 50) ages.Add(age);   // waga sztuk (co 50 szt. - rozklad wedlug drewna, nie wozow)
                    if (age > 14) old14 += rec.Wood;
                    var cur = mp.CurrentSettlement; var tg = mp.TargetSettlement;
                    if (cur != null)
                    {
                        if (ReferenceEquals(cur, mp.HomeSettlement)) { atHome += rec.Wood; nAtHome++; }
                        else if (cur.IsTown) atTown += rec.Wood;
                        else atOther += rec.Wood;
                    }
                    else if (tg == null) noTarget += rec.Wood;
                    else if (tg.IsTown && tg.Town != null) { toTown += rec.Wood; nToTown++; long o; inbound.TryGetValue(tg.Town, out o); inbound[tg.Town] = o + rec.Wood; speeds.Add(mp.LastCalculatedBaseSpeed); }   // pamiec gry (bez liczenia modelu predkosci - zero skutkow ubocznych)
                    else if (tg.IsCastle) toCastle += rec.Wood;
                    else if (ReferenceEquals(tg, mp.HomeSettlement)) toHome += rec.Wood;
                    else toOther += rec.Wood;
                }
                catch (Exception e) { Stumble("WoodLine(tabor)", e); }
            }
            ages.Sort(); speeds.Sort();
            // ---- miasta: polka wobec zapasu rzemiosla (prog opalu mieszczan), wsie z drewnem w targu
            var vilBy = new Dictionary<Town, int>();
            foreach (var v in _woodVil)
            {
                try
                {
                    var st = v != null ? (v.TradeBound ?? v.Bound) : null;
                    if (st != null && st.IsTown && st.Town != null) { int o; vilBy.TryGetValue(st.Town, out o); vilBy[st.Town] = o + 1; }
                }
                catch (Exception e) { Stumble("WoodLine(wies)", e); }
            }
            var defBy = new int[3]; var surBy = new long[3]; var townsBy = new int[3];
            int defNoSrc = 0, defInbound = 0, defDeliv = 0, defOther = 0;
            long defInboundWood = 0, defDelivWood = 0, delivAll = 0;
            var worst = new List<KeyValuePair<long, string>>();
            foreach (var t in Town.AllTowns)
            {
                if (t == null || !t.IsTown || t.Settlement == null) continue;
                try
                {
                    int m = StartCalibration.MarketOfSettlement(t.Settlement); if (m != 0 && m != 1) m = 2;
                    townsBy[m]++;
                    int shelf = t.Settlement.ItemRoster != null ? t.Settlement.ItemRoster.GetItemNumber(_wood) : 0;
                    int keep = Math.Max(ShelfKeep, CaravanBulk.TargetFor(t, _wood));
                    long inb; inbound.TryGetValue(t, out inb);
                    long dl; _deliv.TryGetValue(t, out dl); delivAll += dl;
                    int nv; vilBy.TryGetValue(t, out nv);
                    if (shelf > keep) { surBy[m] += shelf - keep; continue; }
                    defBy[m]++;
                    if (inb > 0) { defInbound++; defInboundWood += inb; }
                    else if (dl > 0) { defDeliv++; }
                    else if (nv == 0) defNoSrc++;
                    else defOther++;
                    defDelivWood += dl;
                    worst.Add(new KeyValuePair<long, string>(keep - shelf, (t.Name != null ? t.Name.ToString() : t.Settlement.StringId) + " " + shelf + "/" + keep + " (dowoz " + dl + ", w drodze " + inb + ", wsi z drewnem " + nv + ")"));
                }
                catch (Exception e) { Stumble("WoodLine(miasto)", e); }
            }
            worst.Sort((a, b) => b.Key.CompareTo(a.Key));
            var flow = _flow[1];
            int wi = flow.HName != null ? Array.IndexOf(flow.HName, "wozy") : -1, vi = flow.HName != null ? Array.IndexOf(flow.HName, "wsie") : -1;
            long ledWag = wi >= 0 && flow.Hold1 != null && wi < flow.Hold1.Length ? flow.Hold1[wi] : -1;
            long ledVil = vi >= 0 && flow.Hold1 != null && vi < flow.Hold1.Length ? flow.Hold1[vi] : -1;
            double perDay = _winDays > 0 ? (double)flow.TWagon / _winDays : 0;
            double ms = (_ticks + (Stopwatch.GetTimestamp() - tl)) * 1000.0 / Stopwatch.Frequency;
            var sb = new StringBuilder(2200);
            sb.Append("Drewno w wozach wsi (f4-pomiary): dzien ").Append(day).Append(" | okno ").Append(_winDays).Append(" dob od dnia ").Append(_winFirst)
              .Append(" | wozow z drewnem ").Append(nWood).Append(", drewna w wozach ").Append(inWag).Append(" (ksiega: wozy ").Append(ledWag).Append(", wsie ").Append(ledVil).Append(')')
              .Append(" | stan: w drodze do miasta ").Append(toTown).Append(" (").Append(nToTown).Append(" wozow, miast celu ").Append(inbound.Count).Append("), do zamku ").Append(toCastle)
              .Append(", do domu z drewnem (niesprzedane) ").Append(toHome).Append(", inny cel ").Append(toOther).Append(", bez celu ").Append(noTarget)
              .Append(", stoi we wsi domowej ").Append(atHome).Append(" (").Append(nAtHome).Append(" wozow), w miescie ").Append(atTown).Append(", w innej osadzie ").Append(atOther)
              .Append(" | wiek ladunku (doby od zaladunku, od wczytania; wedlug drewna) med ").Append(ages.Count > 0 ? ages[(ages.Count - 1) / 2].ToString(Inv) : "-")
              .Append(" q90 ").Append(ages.Count > 0 ? ages[(int)(0.9 * (ages.Count - 1))].ToString(Inv) : "-").Append(", ponad 14 dob ").Append(old14).Append(" szt.")
              .Append("; predkosc wozow w drodze do miasta med ").Append(speeds.Count > 0 ? speeds[(speeds.Count - 1) / 2].ToString("0.00", Inv) : "-")
              .Append(" | okno: zaladowane do wozow ").Append(_loaded).Append(", zeszlo z wozow w miastach ").Append(_soldTown).Append(" (miast ").Append(_deliv.Count).Append("), w zamkach ").Append(_soldCastle)
              .Append(", we wsiach ").Append(_soldVillage).Append(", inne ").Append(_soldOther).Append("; ksiega - wozy do osad ").Append(flow.TWagon).Append(" (").Append(perDay.ToString("0", Inv)).Append("/d)")
              .Append(" -> drewno w wozach = ").Append(perDay > 0 ? (inWag / perDay).ToString("0.0", Inv) : "-").Append(" dob dostaw");
            sb.Append(" | miasta bez opalu (polka <= max(").Append(ShelfKeep).Append(", zapas rzemiosla)): Westeros ").Append(defBy[0]).Append(" z ").Append(townsBy[0]).Append(", Essos ").Append(defBy[1]).Append(" z ").Append(townsBy[1])
              .Append(defBy[2] > 0 ? ", bez rynku " + defBy[2] + " z " + townsBy[2] : "")
              .Append("; przyczyna: woz z drewnem w drodze ").Append(defInbound).Append(" (").Append(defInboundWood).Append(" szt.), dowieziono w oknie, a polka dalej <= zapasu (rzemioslo, dymarki, budowy zabraly) ").Append(defDeliv)
              .Append(", bez wsi z drewnem w targu i bez dowozu ").Append(defNoSrc).Append(", wsie z drewnem sa, ale bez wozu i dowozu ").Append(defOther)
              .Append("; dowieziono do miast bez opalu ").Append(defDelivWood).Append(" z ").Append(delivAll).Append(" szt.")
              .Append(" | drewno na polkach ponad zapas rzemiosla: Westeros ").Append(surBy[0]).Append(", Essos ").Append(surBy[1]).Append(surBy[2] > 0 ? ", bez rynku " + surBy[2] : "")
              .Append(" | najwiekszy brak (polka/zapas): ");
            for (int i = 0; i < worst.Count && i < 5; i++) sb.Append(i > 0 ? "; " : "").Append(worst[i].Value);
            if (worst.Count == 0) sb.Append('-');
            sb.Append(" | ruda w wozach wsi ").Append(ironWag).Append(" (").Append(nIron).Append(" wozow, w drodze do miast ").Append(ironToTown).Append("), zeszlo z wozow w miastach w oknie ").Append(_ironSoldTown)
              .Append(" | czas ").Append(ms.ToString("0.00", Inv)).Append(" ms (").Append((ms / Math.Max(1, _winDays)).ToString("0.00", Inv)).Append(" ms/d), potkniecia ").Append(_stDay).Append(" (sesja ").Append(_st).Append(")")
              .Append(", przerwy ksiegi towarow w sesji (okno i pamiec wozow od nowa) ").Append(_gaps).Append('.');
            return sb.ToString();
        }

        private static string Top(Dictionary<string, long> d, int k)
        {
            if (d.Count == 0) return "-";
            var l = new List<KeyValuePair<string, long>>(d);
            l.Sort((a, b) => b.Value.CompareTo(a.Value));
            var sb = new StringBuilder(300);
            for (int i = 0; i < l.Count && i < k; i++) { if (l[i].Value == 0) break; sb.Append(i > 0 ? ", " : "").Append(l[i].Key).Append(' ').Append(l[i].Value); }
            return sb.Length > 0 ? sb.ToString() : "-";
        }

        private static string S(long n) { return (n >= 0 ? "+" : "") + n.ToString(Inv); }
    }
}
