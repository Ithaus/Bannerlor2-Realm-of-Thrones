using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// r4-mur (decyzja Jeffa 17.10, STAN-PRAC ### 72; PRZEKAZANIE-NOC 5A krok 4) - DANINA ZBOZOWA MURU: lordowie Polnocy (krolestwo "battania" = The North) oddaja
    /// Nocnej Strazy (krolestwo "nightswatch": Castle Black, Eastwatch, Shadow Tower, Nightfort, The Wall i ich wsie) czesc zboza W NATURZE - zamkniety przeplyw fizyczny:
    ///  - CO: racje spichlerzy okregow (Famine, 1 racja = jedzenie 1 czlowieka na dobe; przy KS zastosowanym spichlerz napelnia praca domowa okregu, kupione i zapas -
    ///    KsGranaryNoFloor). Towar rynku (polki, KS-H) nietkniety - to danina z zapasow lordow, nie handel.
    ///  - ILE: tyle, ile Strazy brakuje dla jej ludzi - brak doby kazdej wsi Strazy (ludzie w domu - plon doby z migawki Famine) + uzupelnienie spichlerza do
    ///    WallTributeTargetDays dni (przez WallTributeRefillDays dob, minus to, co juz jedzie); nigdy ponad sufit spichlerza (nadwyzka ponad sufit przepadlaby
    ///    w Famine jako "towar na wozy" - zboze daniny nie moze zniknac).
    ///  - SKAD: wsie okregow Polnocy (bez rodu gracza - jego spichlerzy nie ruszamy bez jego decyzji; bez wojny z Straza) - tylko z tego, co lezy ponad
    ///    WallTributeGiverReserveDays dni wlasnej potrzeby, z wsi bez glodu (u = 0), najwyzej WallTributeGiverSharePercent% tej nadwyzki dziennie x czesc
    ///    jedzenia, ktora okreg sam rodzi (kraina, ktora sie nie wyzywi, oddaje mniej). Na okreg Strazy najwyzej 4 okregi dawcow (waga: oferta / (1 + doby / 10)).
    ///  - DROGA: wozy jada doby drogi ladowej okregu dawcy -> okregu Strazy (MapDistanceModel, jak KS-H; brak drogi - prosta x 1.25) / tempo wozu wsi
    ///    (MarketCarts.PerDay); w drodze psuje sie WallTributeSpoilPercentPerDay% dziennie (liczone przy przyjezdzie). Lista wozow w drodze w zapisie (arm_mur_danina).
    ///  - PRZYJAZD: do spichlerzy wsi okregu Strazy wedlug miejsca do 98% sufitu; czego nie zmiesci - czeka u bram (dobe, potem znow probuje); okreg juz nie Strazy
    ///    (zdobyty) - woz zawraca do dawcy i oddaje zboze do spichlerzy jego wsi (tez najwyzej do 98% sufitu - zboze ponad sufit przepadloby w Famine.Daily krok 4
    ///    jako "nadwyzka ponad sufit"). Woz, ktory przez MaxWaitDays dob z rzedu nie rozladowal niczego, traci reszte jako zepsute u bram (nazwane ujscie
    ///    "zepsute" - recenzja r4-mur: pelne spichlerze dawcy nie przyjmuja zwrotu, a woz psulby sie bez konca).
    ///  - ZLOTO: zadne (danina, nikt nie placi). KS: tylko wsie z KS zastosowanym (StartCalibration.AppliedFor) - dawcy i biorcy.
    /// KONTROLA (linia "Danina Muru (r4-mur)"): w drodze wczoraj + wyslane - zepsute - dostarczone - oddane dawcom = w drodze dzis; od poczatku kampanii (zapis):
    /// wyslane = dostarczone + zepsute + oddane dawcom + w drodze. Pokrycie Strazy: z pola (plon / ludzie) i z pola z dzisiejsza danina.
    /// Wylaczone (klucz albo KS) - nowych wozow nie ma, wozy w drodze dojezdzaja; bez wozow w drodze nic nie biegnie (stara sciezka bit w bit).
    /// Potkniecia liczone na woz / okreg (bez globalnych wylacznikow, CLAUDE.md 7).
    /// </summary>
    internal static class WallTribute
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const int MaxGivers = 4;                   // najwyzej tyle okregow Polnocy na jeden okreg Strazy dziennie
        private const int MaxEntries = 4000;               // straznik rozmiaru zapisu (wozy w drodze)
        private const int MaxImportChars = 1000000;        // straznik rozmiaru przy wczytaniu
        private const double MinShip = 1.0;                // racje - mniej nie jedzie
        private const double RationsPerGrain = 20.0;       // skala ksiegi (DistrictRaid.RationsPerGrain): 1 sztuka jedzenia = 20 racji - tylko do linii
        private const double CapFill = 0.98;               // spichlerz Strazy (i dawcy przy zwrocie) napelniany najwyzej do 98% sufitu (zapas na wahania plonu)
        private const int MaxWaitDays = 30;                // woz u bram bez zadnego rozladunku tyle dob z rzedu - reszta przepada jako zepsute u bram

        private sealed class Ship { public string Src, Dst; public double R; public int Day, Due; public bool Back; public int W0 = -1; }   // W0 - pierwsza doba czekania u bram bez rozladunku (-1 - nie czeka)
        private static readonly List<Ship> _ships = new List<Ship>();
        // od poczatku kampanii (w zapisie): wyslane, dostarczone Strazy, zepsute w drodze, oddane z powrotem dawcom
        private static double _cOut, _cIn, _cSpoil, _cBack;
        private static readonly Dictionary<string, float> _days = new Dictionary<string, float>(StringComparer.Ordinal);   // "src|dst" -> doby drogi (sesja)
        private static int _stumbles;
        private static readonly HashSet<string> _errSites = new HashSet<string>();
        private static bool _offLogged;

        internal static bool KeyOn { get { var s = Settings.Current; return s != null && s.WallGrainTribute; } }

        /// <summary>Konstruktor ArmouryBehavior (nowa kampania / wczytanie): stan jednej kampanii nie przecieka do drugiej.</summary>
        internal static void Reset()
        {
            _ships.Clear(); _cOut = _cIn = _cSpoil = _cBack = 0.0; _days.Clear(); _stumbles = 0; _errSites.Clear(); _offLogged = false;
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_errSites.Add(where)) { try { Log.Error("WallTribute." + where, e); } catch { } }
        }

        private static bool BadId(string id) { return string.IsNullOrEmpty(id) || id.IndexOf('|') >= 0 || id.IndexOf(',') >= 0 || id.IndexOf(';') >= 0; }
        private static double Fin(double x) { return double.IsNaN(x) || double.IsInfinity(x) || x < 0.0 ? 0.0 : x; }

        // ------------------------------------------------------------ zapis (arm_mur_danina)
        // "1|wyslane;dostarczone;zepsute;oddane|src,dst,racje,doba,termin,powrot,czeka od;..." (pusty napis - nic do zapisania; "czeka od" opcjonalne - brak = -1)
        internal static string Export()
        {
            try
            {
                if (_ships.Count == 0 && _cOut <= 0.0) return "";
                var sb = new StringBuilder(64 + _ships.Count * 48);
                sb.Append("1|").Append(Fin(_cOut).ToString("R", Inv)).Append(';').Append(Fin(_cIn).ToString("R", Inv)).Append(';')
                  .Append(Fin(_cSpoil).ToString("R", Inv)).Append(';').Append(Fin(_cBack).ToString("R", Inv)).Append('|');
                bool first = true;
                int n = 0;
                foreach (var sh in _ships)
                {
                    if (BadId(sh.Src) || BadId(sh.Dst) || !(sh.R > 0.0) || double.IsInfinity(sh.R)) continue;
                    if (++n > MaxEntries) break;
                    if (!first) sb.Append(';');
                    sb.Append(sh.Src).Append(',').Append(sh.Dst).Append(',').Append(sh.R.ToString("R", Inv)).Append(',').Append(sh.Day.ToString(Inv)).Append(',')
                      .Append(sh.Due.ToString(Inv)).Append(',').Append(sh.Back ? '1' : '0').Append(',').Append(sh.W0.ToString(Inv));
                    first = false;
                }
                return sb.ToString();
            }
            catch (Exception e) { Stumble("Export", e); return ""; }
        }

        internal static void Import(string s)
        {
            _ships.Clear(); _cOut = _cIn = _cSpoil = _cBack = 0.0;
            if (string.IsNullOrEmpty(s)) return;                                   // stary zapis bez klucza - pusto
            if (s.Length > MaxImportChars) { try { Log.Info("Danina Muru (r4-mur): zapis ponad " + MaxImportChars + " znakow - pominiety (start od zera)."); } catch { } return; }
            try
            {
                var p = s.Split('|');
                if (p.Length < 2 || p[0] != "1") return;                          // inny format - start od zera
                var c = p[1].Split(';');
                _cOut = Num(c, 0); _cIn = Num(c, 1); _cSpoil = Num(c, 2); _cBack = Num(c, 3);
                if (p.Length > 2 && p[2].Length > 0)
                    foreach (var item in p[2].Split(';'))
                    {
                        try
                        {
                            var a = item.Split(',');
                            if (a.Length < 6 || BadId(a[0]) || BadId(a[1])) continue;
                            double r = Num(a, 2);
                            int day, due;
                            if (!(r > 0.0) || !int.TryParse(a[3], NumberStyles.Integer, Inv, out day) || !int.TryParse(a[4], NumberStyles.Integer, Inv, out due)) continue;
                            if (_ships.Count >= MaxEntries) break;
                            int w0;
                            if (a.Length < 7 || !int.TryParse(a[6], NumberStyles.Integer, Inv, out w0) || w0 < 0) w0 = -1;
                            _ships.Add(new Ship { Src = a[0], Dst = a[1], R = r, Day = day, Due = due, Back = a[5] == "1", W0 = w0 });
                        }
                        catch (Exception e) { Stumble("Import(woz)", e); }
                    }
            }
            catch (Exception e) { Stumble("Import", e); _ships.Clear(); }
        }

        private static double Num(string[] a, int i)
        {
            double x;
            if (a == null || i >= a.Length || !double.TryParse(a[i], NumberStyles.Float, Inv, out x)) return 0.0;
            return Fin(x);
        }

        // ------------------------------------------------------------ geografia
        private static bool IsWatch(Settlement n) { var f = n != null ? n.MapFaction : null; return f != null && f.StringId == CrownGifts.WatchId; }
        private static bool IsNorth(Settlement n) { var f = n != null ? n.MapFaction : null; return f != null && f.StringId == CrownGifts.NorthId; }

        private static Settlement Node(string id)
        {
            if (id == null) return null;
            foreach (var n in OutlawLaw.RegionNodes()) if (n != null && n.StringId == id) return n;
            return null;
        }

        /// <summary>Doby drogi wozu: droga ladowa gry (pamiec drog, jak KS-H) / tempo wozu wsi; brak drogi ladowej - prosta x 1.25. Pamiec na sesje.</summary>
        private static float DaysOf(Settlement a, Settlement b, Settings s)
        {
            string k = a.StringId + "|" + b.StringId;
            float d;
            if (_days.TryGetValue(k, out d)) return d;
            double dist = -1.0;
            try
            {
                var m = Campaign.Current.Models.MapDistanceModel;
                float x = m.GetDistance(a, b, false, false, MobileParty.NavigationType.Default);
                if (x >= 0f && x < CartTownExit.BkLimit) dist = x;
            }
            catch (Exception e) { Stumble("DaysOf", e); }
            if (!(dist >= 0.0)) dist = a.GetPosition2D.Distance(b.GetPosition2D) * 1.25;
            double per = Math.Max(1.0, MarketCarts.PerDay(s));
            d = (float)Math.Max(1.0, Math.Ceiling(dist / per));
            _days[k] = d;
            return d;
        }

        // ------------------------------------------------------------ doba
        private sealed class VilNeed { public Village V; public double Home, Harvest, Food, Cap, Req, Offer; }
        private sealed class RegNeed { public Settlement N; public readonly List<VilNeed> Vs = new List<VilNeed>(); public double Home, Harvest, Food, Gap, Refill, Cut, Need, Offer, Surplus, Cov; }

        /// <summary>ArmouryBehavior.OnDailyTick, zaraz po Famine.Daily (migawka spichlerzy tej doby): przyjazdy, potrzeba Strazy, oferta Polnocy, wysylka, linia.</summary>
        internal static void Daily()
        {
            long t0 = Stopwatch.GetTimestamp();
            var s = Settings.Current;
            if (s == null || Campaign.Current == null) return;
            int today = (int)CampaignTime.Now.ToDays, day = today - 1;
            bool on = s.WallGrainTribute && StartCalibration.Applied && Famine.On;
            if (!on && _ships.Count == 0)
            {
                if (!_offLogged && s.LogEnabled)
                {
                    _offLogged = true;
                    string why = !s.WallGrainTribute ? "klucz Wall Grain Tribute wylaczony" : (!StartCalibration.Applied ? "kalibracja startu (KS) nie zastosowana" : "spichlerze (Famine) wylaczone");
                    try { Log.Info("Danina Muru (r4-mur): dzien " + day + " | WYLACZONA (" + why + ") - bez wozow w drodze nic nie biegnie; od poczatku: wyslane " + F0(_cOut) + " = dostarczone " + F0(_cIn) + " + zepsute " + F0(_cSpoil) + " + oddane dawcom " + F0(_cBack) + " racji."); } catch { }
                }
                return;
            }
            _offLogged = false;
            double transitY = 0.0; foreach (var sh in _ships) transitY += sh.R;
            double spoiledD = 0.0, delivD = 0.0, backDelivD = 0.0, backStartD = 0.0, waitD = 0.0, shippedD = 0.0, unmet = 0.0, rotD = 0.0;
            int lots = 0, arrivals = 0, waitN = 0, backN = 0, rotN = 0;
            double p = Math.Max(0.0, Math.Min(0.5, s.WallTributeSpoilPercentPerDay / 100.0));

            // 1. przyjazdy (takze przy wylaczonym kluczu - wozy w drodze dojezdzaja)
            var nodeById = new Dictionary<string, Settlement>(StringComparer.Ordinal);
            foreach (var n in OutlawLaw.RegionNodes()) if (n != null && n.StringId != null) nodeById[n.StringId] = n;
            for (int i = 0; i < _ships.Count; i++)
            {
                var sh = _ships[i];
                if (sh.Due > today) continue;
                try
                {
                    arrivals++;
                    int dd = Math.Max(1, sh.Due - sh.Day);
                    double spoil = sh.R * (1.0 - Math.Pow(1.0 - p, dd));
                    if (spoil > sh.R) spoil = sh.R;
                    sh.R -= spoil; spoiledD += spoil; _cSpoil += spoil;
                    Settlement dst, src;
                    nodeById.TryGetValue(sh.Dst, out dst); nodeById.TryGetValue(sh.Src, out src);
                    if (!sh.Back)
                    {
                        if (dst != null && IsWatch(dst))
                        {
                            double put = Deliver(dst, sh.R);
                            sh.R -= put; delivD += put; _cIn += put;
                            if (sh.R > 1e-6) Wait(sh, put, today, ref waitD, ref waitN, ref rotD, ref rotN, ref spoiledD);   // spichlerze pelne - reszta czeka u bram do jutra
                        }
                        else
                        {
                            // okreg zdobyty / nie Strazy - woz zawraca do dawcy (zboze nie trafia do obcych)
                            float back = src != null && dst != null ? DaysOf(src, dst, s) : 1f;
                            backStartD += sh.R; backN++;
                            sh.Back = true; sh.Day = today; sh.Due = today + (int)back; sh.W0 = -1;
                        }
                    }
                    else
                    {
                        double put = src != null ? Deliver(src, sh.R) : 0.0;
                        sh.R -= put; backDelivD += put; _cBack += put;
                        if (sh.R > 1e-6) Wait(sh, put, today, ref waitD, ref waitN, ref rotD, ref rotN, ref spoiledD);
                    }
                }
                catch (Exception e) { Stumble("Daily(przyjazd)", e); }
            }
            // woz pusty (reszta ponizej milionowej racji - blad zaokraglenia) - zdjety z listy; reszta wozow z towarem zostaje
            _ships.RemoveAll(x => !(x.R > 1e-6));

            // 2. potrzeba Strazy (migawka Famine tej doby, wsie z KS zastosowanym)
            var watch = new List<RegNeed>();
            var givers = new List<RegNeed>();
            IFaction watchF = null;
            var transitTo = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (var sh in _ships) if (!sh.Back) { double v0; transitTo.TryGetValue(sh.Dst, out v0); transitTo[sh.Dst] = v0 + sh.R; }
            double target = Math.Max(0, s.WallTributeTargetDays), refillDays = Math.Max(1, s.WallTributeRefillDays), reserve = Math.Max(0, s.WallTributeGiverReserveDays);
            double share = Math.Max(0.0, Math.Min(100.0, s.WallTributeGiverSharePercent)) / 100.0;
            foreach (var n in nodeById.Values)
            {
                try
                {
                    if (n.BoundVillages == null || n.BoundVillages.Count == 0) continue;
                    bool w = IsWatch(n), g = !w && IsNorth(n) && n.OwnerClan != Clan.PlayerClan;
                    if (!w && !g) continue;
                    if (w && watchF == null) watchF = n.MapFaction;
                    var r = new RegNeed { N = n };
                    foreach (var v in n.BoundVillages)
                    {
                        try
                        {
                            double home, harv, food, cap; float u; int d;
                            if (!StartCalibration.AppliedFor(v) || !Famine.DayOf(v, out home, out harv, out food, out cap, out u, out d) || d != today || !(home > 0.0)) continue;
                            var x = new VilNeed { V = v, Home = home, Harvest = harv, Food = food, Cap = cap };
                            if (g && u > 0.0001f) { r.Home += home; r.Harvest += harv; r.Food += food; continue; }   // glodna wies nie daje
                            r.Vs.Add(x); r.Home += home; r.Harvest += harv; r.Food += food;
                        }
                        catch (Exception e) { Stumble("Daily(wies)", e); }
                    }
                    if (r.Home <= 0.0) continue;
                    if (w) watch.Add(r); else givers.Add(r);
                }
                catch (Exception e) { Stumble("Daily(okreg)", e); }
            }
            double wHome = 0, wHarv = 0, wFood = 0, wGap = 0, wRefill = 0, wCut = 0, wNeed = 0;
            foreach (var r in watch)
            {
                double tr; transitTo.TryGetValue(r.N.StringId, out tr);
                foreach (var x in r.Vs)
                {
                    double trShare = r.Home > 0 ? tr * x.Home / r.Home : 0.0;
                    double gap = Math.Max(0.0, x.Home - x.Harvest);
                    double refill = Math.Max(0.0, target * x.Home - x.Food - trShare) / refillDays;
                    double room = x.Cap > 0.0 ? Math.Max(0.0, CapFill * x.Cap - x.Food - trShare) : gap + refill;
                    double req = Math.Min(gap + refill, room);
                    x.Req = req;
                    r.Gap += gap; r.Refill += refill; r.Cut += gap + refill - req; r.Need += req;
                }
                wHome += r.Home; wHarv += r.Harvest; wFood += r.Food; wGap += r.Gap; wRefill += r.Refill; wCut += r.Cut; wNeed += r.Need;
            }

            // 3. oferta Polnocy (nadwyzka ponad rezerwe x udzial x czesc jedzenia, ktora okreg sam rodzi)
            double gSurplus = 0, gOffer = 0; int gWith = 0;
            if (on)
            {
                foreach (var r in givers)
                {
                    try
                    {
                        if (watchF != null && r.N.MapFaction != null && r.N.MapFaction.IsAtWarWith(watchF)) continue;
                        r.Cov = r.Home > 0 ? Math.Min(1.0, r.Harvest / r.Home) : 0.0;
                        foreach (var x in r.Vs)
                        {
                            double sur = x.Food - reserve * x.Home;
                            if (!(sur > 0.0)) continue;
                            double cov = Math.Min(1.0, x.Harvest / x.Home);
                            x.Offer = share * sur * cov;
                            r.Surplus += sur; r.Offer += x.Offer;
                        }
                        if (r.Offer > 0.0) { gWith++; gSurplus += r.Surplus; gOffer += r.Offer; }
                    }
                    catch (Exception e) { Stumble("Daily(dawca)", e); }
                }
            }

            // 4. podzial: na okreg Strazy najwyzej MaxGivers dawcow (waga = oferta / (1 + doby / 10)); dawca najwyzej swoja oferta
            var asks = new List<KeyValuePair<RegNeed, KeyValuePair<RegNeed, double>>>();
            var asked = new Dictionary<RegNeed, double>();
            if (on && gOffer > 0.0)
            {
                foreach (var w in watch)
                {
                    if (!(w.Need >= MinShip)) continue;
                    var cand = new List<KeyValuePair<RegNeed, double>>();
                    foreach (var g in givers)
                    {
                        if (!(g.Offer > 0.0)) continue;
                        float dd = DaysOf(g.N, w.N, s);
                        cand.Add(new KeyValuePair<RegNeed, double>(g, g.Offer / (1.0 + dd / 10.0)));
                    }
                    var top = cand.OrderByDescending(c => c.Value).Take(MaxGivers).ToList();
                    double sw = 0; foreach (var c in top) sw += c.Value;
                    if (!(sw > 0.0)) continue;
                    foreach (var c in top)
                    {
                        double want = w.Need * c.Value / sw;
                        asks.Add(new KeyValuePair<RegNeed, KeyValuePair<RegNeed, double>>(w, new KeyValuePair<RegNeed, double>(c.Key, want)));
                        double a0; asked.TryGetValue(c.Key, out a0); asked[c.Key] = a0 + want;
                    }
                }
            }
            // 5. wysylka: zboze schodzi ze spichlerzy wsi dawcy wedlug ich oferty
            double wantAll = 0.0;
            foreach (var a in asks)
            {
                var w = a.Key; var g = a.Value.Key; double want = a.Value.Value;
                wantAll += want;
                try
                {
                    double askedG; asked.TryGetValue(g, out askedG);
                    double amount = askedG > g.Offer && askedG > 0.0 ? want * g.Offer / askedG : want;
                    if (!(amount >= MinShip)) { unmet += want; continue; }
                    int due = today + (int)DaysOf(g.N, w.N, s);              // przed zdjeciem ze spichlerzy (nic nie rzuca miedzy zdjeciem a wozem)
                    double taken = 0.0;
                    foreach (var x in g.Vs)
                    {
                        if (!(x.Offer > 0.0)) continue;
                        taken += Famine.TributeTake(x.V, amount * x.Offer / g.Offer);
                    }
                    unmet += Math.Max(0.0, want - taken);
                    if (!(taken > 0.0)) continue;
                    Ship merge = null;
                    foreach (var sh in _ships) if (!sh.Back && sh.Due == due && sh.Src == g.N.StringId && sh.Dst == w.N.StringId) { merge = sh; break; }
                    if (merge != null) merge.R += taken;
                    else if (_ships.Count < MaxEntries) _ships.Add(new Ship { Src = g.N.StringId, Dst = w.N.StringId, R = taken, Day = today, Due = due });
                    else
                    {
                        // straznik rozmiaru: dopisz do najblizszego wozu tej pary (ten sam cel, termin najblizszy)
                        Ship best = null;
                        foreach (var sh in _ships) if (!sh.Back && sh.Src == g.N.StringId && sh.Dst == w.N.StringId && (best == null || Math.Abs(sh.Due - due) < Math.Abs(best.Due - due))) best = sh;
                        if (best != null) best.R += taken; else _ships.Add(new Ship { Src = g.N.StringId, Dst = w.N.StringId, R = taken, Day = today, Due = due });
                    }
                    shippedD += taken; _cOut += taken; lots++;
                }
                catch (Exception e) { Stumble("Daily(wysylka)", e); }
            }
            if (on && gOffer <= 0.0) unmet += wNeed;
            else if (on && wantAll < wNeed) unmet += wNeed - wantAll;   // okregi Strazy bez zadnego dawcy

            double transit = 0.0; foreach (var sh in _ships) transit += sh.R;
            if (!s.LogEnabled) return;
            try
            {
                double ctl = transitY + shippedD - spoiledD - delivD - backDelivD - transit;
                double ctl2 = _cOut - _cIn - _cSpoil - _cBack - transit;
                double cov0 = wHome > 0 ? wHarv / wHome : 0.0, cov1 = wHome > 0 ? (wHarv + delivD) / wHome : 0.0;
                int wV = 0; foreach (var r in watch) wV += r.Vs.Count;
                var sb = new StringBuilder(1400);
                sb.Append("Danina Muru (r4-mur): dzien ").Append(day).Append(on ? " | CZYNNA" : " | WYLACZONA (wozy w drodze dojezdzaja)");
                sb.Append(" | Straz: okregi ").Append(watch.Count).Append(", wsi ").Append(wV).Append(", ludzie ").Append(F0(wHome)).Append(", plon ").Append(F0(wHarv))
                  .Append(" racji/d, brak ").Append(F0(wGap)).Append("/d, spichlerze ").Append(F0(wFood)).Append(" racji = ").Append(F(wHome > 0 ? wFood / wHome : 0, "0.0")).Append(" dni");
                sb.Append(" | zadanie ").Append(F0(wNeed)).Append(" racji (brak ").Append(F0(wGap)).Append(" + uzupelnienie ").Append(F0(wRefill)).Append(" - bez miejsca w spichlerzu ").Append(F0(wCut)).Append(')');
                sb.Append(" | Polnoc: okregi ").Append(givers.Count).Append(" (z nadwyzka ").Append(gWith).Append("), nadwyzka ponad rezerwe ").Append(F0(gSurplus)).Append(" racji, oferta ").Append(F0(gOffer)).Append("/d");
                sb.Append(" | wyslane ").Append(F0(shippedD)).Append(" racji (").Append(lots).Append(" wozow, ok. ").Append(F0(shippedD / RationsPerGrain)).Append(" szt. zboza), niedobor ").Append(F0(unmet));
                sb.Append(" | przyjazdy ").Append(arrivals).Append(": dostarczone ").Append(F0(delivD)).Append(", zepsute ").Append(F(spoiledD, "0.0"))
                  .Append(" (w tym przepadle u bram po ").Append(MaxWaitDays).Append(" dobach bez miejsca ").Append(F(rotD, "0.0")).Append(" w ").Append(rotN).Append(" wozach)")
                  .Append(", zawrocone do dawcow ").Append(F0(backStartD)).Append(" (").Append(backN)
                  .Append("), oddane dawcom ").Append(F0(backDelivD)).Append(", czeka u bram ").Append(F0(waitD)).Append(" (").Append(waitN).Append(')');
                sb.Append(" | w drodze ").Append(F0(transit)).Append(" (wczoraj ").Append(F0(transitY)).Append(", wozow ").Append(_ships.Count).Append(')');
                sb.Append(" | kontrola drogi ").Append(F(ctl, "0.###"));
                sb.Append(" | od poczatku: wyslane ").Append(F0(_cOut)).Append(" = dostarczone ").Append(F0(_cIn)).Append(" + zepsute ").Append(F0(_cSpoil)).Append(" + oddane dawcom ").Append(F0(_cBack))
                  .Append(" + w drodze ").Append(F0(transit)).Append(" (reszta ").Append(F(ctl2, "0.###")).Append(')');
                sb.Append(" | pokrycie Strazy: z pola ").Append(F(cov0, "0.000")).Append(", z pola i daniny ").Append(F(cov1, "0.000"));
                sb.Append(" | potkniecia ").Append(_stumbles);
                sb.Append(" | czas ").Append(((Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency).ToString("0.00", Inv)).Append(" ms.");
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Daily(linia)", e); }
        }

        /// <summary>Woz z reszta zboza u bram: czeka do jutra; MaxWaitDays dob z rzedu bez rozladunku - reszta przepada jako zepsute u bram (ujscie "zepsute",
        /// ksiega od poczatku bez zmian: wyslane = dostarczone + zepsute + oddane dawcom + w drodze). Rozladunek czesci (put > 0) liczy czekanie od nowa.</summary>
        private static void Wait(Ship sh, double put, int today, ref double waitD, ref int waitN, ref double rotD, ref int rotN, ref double spoiledD)
        {
            if (put > 1e-6 || sh.W0 < 0) sh.W0 = today;
            if (today - sh.W0 >= MaxWaitDays)
            {
                double rot = sh.R;
                sh.R = 0.0; spoiledD += rot; _cSpoil += rot; rotD += rot; rotN++;
                return;
            }
            waitD += sh.R; waitN++; sh.Day = today; sh.Due = today + 1;
        }

        /// <summary>Zboze do spichlerzy wsi okregu: wedlug miejsca do 98% sufitu (biorca i dawca przy zwrocie - ponad sufit Famine.Daily krok 4 zboze by przepadlo
        /// jako "nadwyzka ponad sufit"), wsie bez spichlerza pominiete. Zwraca, ile wlozono (reszta czeka u bram).</summary>
        private static double Deliver(Settlement region, double r)
        {
            if (region == null || !(r > 0.0) || region.BoundVillages == null) return 0.0;
            var room = new List<KeyValuePair<Village, double>>();
            double all = 0.0;
            foreach (var v in region.BoundVillages)
            {
                try
                {
                    double home, harv, food, cap; float u; int d;
                    if (!Famine.DayOf(v, out home, out harv, out food, out cap, out u, out d)) continue;
                    double rm = cap > 0.0 ? Math.Max(0.0, CapFill * cap - food) : r;
                    if (!(rm > 0.0)) continue;
                    room.Add(new KeyValuePair<Village, double>(v, rm)); all += rm;
                }
                catch (Exception e) { Stumble("Deliver(wies)", e); }
            }
            if (!(all > 0.0)) return 0.0;
            double put = 0.0, want = Math.Min(r, all);
            foreach (var kv in room)
            {
                double give = Math.Min(kv.Value, want * kv.Value / all);
                if (give > 0.0 && Famine.TributeGive(kv.Key, give)) put += give;
            }
            return Math.Min(put, r);
        }

        private static string F0(double x) { return Fin(x).ToString("0", Inv); }
        private static string F(double x, string f) { return (double.IsNaN(x) || double.IsInfinity(x) ? 0.0 : x).ToString(f, Inv); }
    }
}
