using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// m112-inwest (24.10) - MAJATKI I INWESTYCJE GRACZA W ZAKLADCE FIEFS. Jeff wplacil 19 854 zl ("full kit") w prace wioski przy White Harbor; po 9 dobach dostal 5 zl
    /// i nie widzial nic - ani komunikatu, ani wpisu w ekranie rodu. Jeff: "i ma byc to poakzane w thiefts zeby wedizal gdzie mam majtek i inwestycje".
    ///  - ZAKLADKA FIEFS (klucz InvestmentsInFiefs): pod lista zamkow (obok bloku wioski pobocznej - SideVillageFiefUi) dwa bloki tekstu:
    ///    "Village works (N)" - prace rodu gracza (DistrictWorks.PlayerWorks): wioska, okreg i miasto targowe, rodzaj, sprzet na miejscu (% i jednostki / osady stojace),
    ///    gotowosc (rampa KReady dob - "full in N days (day X)"), wplacone, wyplacone w 28 dobach i od poczatku, stany: wojna (bez wyplat), ogien (stracone, termin odbudowy),
    ///    bezczynne, wstrzymane (klucz, droga produkcji);
    ///    "Estates (N)" - majatki BK rodu gracza (BannerKingsConfig.Instance.PopulationManager.GetPopData(wies).EstateData.Estates, wlasciciel Estate.Owner z rodu gracza;
    ///    odbicie jak VillageHolders.OwnersOf): wies, ile majatkow i akrow, dochod dnia z Estate.LastIncome (przy kluczu BK Estate Income From Sales Only to srednia
    ///    28 dob udzialu z utargu - EstateIncome187), wyplacone od wczytania (udzial z podatku przy powrocie taboru - nasluch VillageTakings.OnGoldTraded).
    ///    Prawa czesc zakladki (rod bez miast i zamkow) - krotkie podsumowanie w NoFiefsText (SideVillageFief.NoFiefsPostfix).
    ///  - WYPLATY: prace - wedlug dob w arm_works (sekcja "|h:" przy naglowku w2 - starsza DLL ja pomija, DistrictWorks); majatki - tylko pamiec sesji (UI mowi "since load"), bez nowego klucza zapisu.
    ///  - KOMUNIKAT DNIA (OnDailyTick, raz na dobe): "Your village works paid you N denars yesterday (...)" i/lub "Your estates paid you N denars yesterday (...)" (minionej doby); przy 0 - nic.
    ///  - Linia logu "Majatki i prace w Fiefs (m112): dzien N" (gdy rod gracza ma prace albo majatki, cos wyplacono albo bylo potkniecie).
    /// Wszystko w try, potkniecia liczone (pierwsze z miejsca do logu), ekran sie nie wywraca.
    /// </summary>
    internal static class FiefInvestments
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const string P = "Majatki i prace w Fiefs (m112): ";

        internal static bool On { get { var s = Settings.Current; return s != null && s.InvestmentsInFiefs; } }

        // BK przez odbicie (raz na proces)
        private static bool _bkTried, _bkOk;
        private static PropertyInfo _pCfg, _pPopMgr, _pEstData, _pEstates;
        private static MethodInfo _mPopulated, _mGetPop;
        private static readonly Dictionary<Type, PropertyInfo[]> _estProps = new Dictionary<Type, PropertyInfo[]>();   // typ majatku -> Owner, LastIncome, Acreage, IncomeBlockedReason

        /// <summary>Majatek BK rodu gracza.</summary>
        internal sealed class Est { public Settlement Village; public Hero Owner; public int Last; public float Acres; public string Blocked; }   // Blocked - BK IncomeBlockedReason (dochod 0)
        private static List<Est> _mine = new List<Est>();
        private static int _scanHour = int.MinValue;
        private static WeakReference _vm;                                   // m112-inwest po recenzji: ekran Fiefs, dla ktorego skan majatkow jest swiezy

        // wyplaty majatkow rodu gracza (pamiec sesji): wies -> suma i doby
        private sealed class Paid { public double All; public readonly Dictionary<int, double> Days = new Dictionary<int, double>(); }
        private static readonly Dictionary<Settlement, Paid> _estPaid = new Dictionary<Settlement, Paid>();

        private static int _msgDay = int.MinValue;
        private static bool _startLogged;
        private static int _st, _stDay, _dUi, _dWorkMsg, _dEstMsg, _dEstNotes;
        private static readonly HashSet<string> _err = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>Konstruktor ArmouryBehavior (kazda kampania): pamiec od zera.</summary>
        internal static void Reset()
        {
            _mine = new List<Est>(); _scanHour = int.MinValue; _vm = null; _estPaid.Clear(); _msgDay = int.MinValue; _startLogged = false;
            _st = _stDay = _dUi = _dWorkMsg = _dEstMsg = _dEstNotes = 0;
            lock (_err) _err.Clear();
        }

        private static void Stumble(string where, Exception e)
        {
            _st++; _stDay++;
            bool first; lock (_err) first = _err.Add(where);
            if (first || _st % 200 == 0) Log.Error("FiefInvestments." + where + " (potkniecie " + _st + ")", e);
        }

        internal static void UiStumble(string where, Exception e) { Stumble("Ui." + where, e); }

        /// <summary>m112-inwest po recenzji: nowy ekran Fiefs (ClanFiefsVM) - skan majatkow od nowa raz na otwarcie (czas stoi w menu, wiec majatek kupiony
        /// w tej samej godzinie gry inaczej nie bylby widoczny); kolejne odswiezenia tego samego ekranu - pamiec godziny. Nie rzuca.</summary>
        internal static void Opened(object vm)
        {
            try { if (vm != null && (_vm == null || !ReferenceEquals(_vm.Target, vm))) { _vm = new WeakReference(vm); _scanHour = int.MinValue; } }
            catch { }
        }

        private static int Today() { return (int)CampaignTime.Now.ToDays; }
        private static string N0(double x) { return Math.Round(x).ToString("N0", Inv); }
        private static string Txt(TaleWorlds.Localization.TextObject t) { try { return t != null ? t.ToString() : "?"; } catch { return "?"; } }

        // ============================================================ majatki BK
        private static void ResolveBk()
        {
            if (_bkTried) return;
            _bkTried = true;
            try
            {
                var cfgT = AccessTools.TypeByName("BannerKings.BannerKingsConfig");
                if (cfgT == null) return;
                _pCfg = AccessTools.Property(cfgT, "Instance");
                _pPopMgr = AccessTools.Property(cfgT, "PopulationManager");
                var pmT = _pPopMgr != null ? _pPopMgr.PropertyType : null;
                _mPopulated = pmT != null ? AccessTools.Method(pmT, "IsSettlementPopulated", new[] { typeof(Settlement) }) : null;
                _mGetPop = pmT != null ? AccessTools.Method(pmT, "GetPopData", new[] { typeof(Settlement) }) : null;
                _pEstData = _mGetPop != null ? AccessTools.Property(_mGetPop.ReturnType, "EstateData") : null;
                _pEstates = _pEstData != null ? AccessTools.Property(_pEstData.PropertyType, "Estates") : null;
                _bkOk = _pCfg != null && _pPopMgr != null && _mPopulated != null && _mGetPop != null && _pEstData != null && _pEstates != null;
            }
            catch (Exception e) { _bkOk = false; Stumble("ResolveBk", e); }
        }

        private static PropertyInfo[] PropsOf(Type t)
        {
            PropertyInfo[] p;
            if (_estProps.TryGetValue(t, out p)) return p;
            p = new[] { AccessTools.Property(t, "Owner"), AccessTools.Property(t, "LastIncome"), AccessTools.Property(t, "Acreage"), AccessTools.Property(t, "IncomeBlockedReason") };
            _estProps[t] = p;
            return p;
        }

        /// <summary>Majatki BK rodu gracza (pamiec godziny; fresh - od nowa). Pusta lista - brak BK albo majatkow. Sam odczyt: GetPopData tylko dla wsi z danymi BK
        /// (IsSettlementPopulated - jak VillageHolders.OwnersOf; GetPopData osady bez danych inicjuje je w BK). Nie rzuca.</summary>
        internal static List<Est> Estates(bool fresh = false)
        {
            try
            {
                if (Campaign.Current == null) return _mine;
                int hour = (int)CampaignTime.Now.ToHours;
                if (!fresh && hour == _scanHour) return _mine;
                _scanHour = hour;
                ResolveBk();
                var list = new List<Est>();
                var pc = Clan.PlayerClan;
                if (!_bkOk || pc == null) { _mine = list; return list; }
                object cfg = _pCfg.GetValue(null, null);
                object pm = cfg != null ? _pPopMgr.GetValue(cfg, null) : null;
                if (pm == null) { _mine = list; return list; }
                var args = new object[1];
                foreach (var st in Settlement.All)
                {
                    if (st == null || !st.IsVillage) continue;
                    try
                    {
                        args[0] = st;
                        if (!(bool)_mPopulated.Invoke(pm, args)) continue;
                        object pd = _mGetPop.Invoke(pm, args);
                        object ed = pd != null ? _pEstData.GetValue(pd, null) : null;
                        var es = ed != null ? _pEstates.GetValue(ed, null) as IList : null;
                        if (es == null || es.Count == 0) continue;
                        for (int i = 0; i < es.Count; i++)
                        {
                            var e = es[i];
                            if (e == null) continue;
                            var pr = PropsOf(e.GetType());
                            var h = pr[0] != null ? pr[0].GetValue(e, null) as Hero : null;
                            if (h == null || !ReferenceEquals(h.Clan, pc)) continue;
                            var x = new Est { Village = st, Owner = h };
                            // m112-inwest po recenzji: BK Estate.Income = 0 przy IncomeBlockedReason (wojna z krolestwem wsi, majatek niezapisany na wlasciciela...);
                            // przy kluczu BK Estate Income From Sales Only - srednia 28 dob wpisana raz na dobe (LastIncome w ciagu doby rosnie o dzisiejsze wyplaty)
                            try { if (pr[3] != null) x.Blocked = pr[3].GetValue(e, null) as string; } catch { x.Blocked = null; }
                            int avg;
                            if (x.Blocked != null) x.Last = 0;
                            else if (EstateIncome187.On && EstateIncome187.AverageOf(e, out avg)) x.Last = avg;
                            else { try { if (pr[1] != null) x.Last = Convert.ToInt32(pr[1].GetValue(e, null), Inv); } catch { x.Last = 0; } }
                            try { if (pr[2] != null) x.Acres = Convert.ToSingle(pr[2].GetValue(e, null), Inv); } catch { x.Acres = 0; }
                            list.Add(x);
                        }
                    }
                    catch (Exception ex) { Stumble("Estates(wies)", ex); }
                }
                _mine = list;
                return list;
            }
            catch (Exception e) { Stumble("Estates", e); return _mine; }
        }

        /// <summary>VillageTakings.OnGoldTraded (okno powrotu taboru): wyplata "nic -> bohater" to udzial majatku BK w podatku z utargu. Bohater z rodu gracza - zapis
        /// wyplaty do wsi majatku (wies wejscia taboru - jej EstateData placi; tabor w domu - wies macierzysta). Nie rzuca.</summary>
        internal static void NoteEstatePaid(Hero h, Village home, Village here, int amount)
        {
            try
            {
                if (h == null || amount <= 0 || home == null || Clan.PlayerClan == null || !ReferenceEquals(h.Clan, Clan.PlayerClan)) return;
                // m112-inwest po recenzji: placi EstateData wsi WEJSCIA (BK AccumulateTradeTax na wsi, do ktorej tabor wszedl - jak licznik podatku w Whole112)
                Settlement at = here != null && here.Settlement != null ? here.Settlement : home.Settlement;
                if (at == null) return;
                Paid p;
                if (!_estPaid.TryGetValue(at, out p)) { p = new Paid(); _estPaid[at] = p; }
                int day = Today();
                p.All += amount;
                double c; p.Days.TryGetValue(day, out c); p.Days[day] = c + amount;
                if (p.Days.Count > 32) { var old = new List<int>(); foreach (var k in p.Days.Keys) if (k < day - 27) old.Add(k); foreach (var k in old) p.Days.Remove(k); }
                _dEstNotes++;
            }
            catch (Exception e) { Stumble("NoteEstatePaid", e); }
        }

        private static double EstPaidSince(Settlement v, int fromDay, out double all)
        {
            all = 0;
            Paid p;
            if (v == null || !_estPaid.TryGetValue(v, out p)) return 0;
            all = p.All;
            double s = 0; foreach (var kv in p.Days) if (kv.Key >= fromDay) s += kv.Value;
            return s;
        }

        /// <summary>Majatki rodu gracza wedlug wsi (kolejnosc pierwszego wystapienia).</summary>
        private sealed class EstVillage { public Settlement V; public int Count, Blocked; public double Acres, Last; public string Why; public readonly List<string> Others = new List<string>(); }
        private static List<EstVillage> ByVillage(List<Est> es)
        {
            var res = new List<EstVillage>();
            var idx = new Dictionary<Settlement, EstVillage>();
            foreach (var e in es)
            {
                EstVillage g;
                if (!idx.TryGetValue(e.Village, out g)) { g = new EstVillage { V = e.Village }; idx[e.Village] = g; res.Add(g); }
                g.Count++; g.Acres += Math.Max(0, e.Acres); g.Last += Math.Max(0, e.Last);
                if (e.Blocked != null) { g.Blocked++; if (g.Why == null) g.Why = e.Blocked; }
                if (!ReferenceEquals(e.Owner, Hero.MainHero)) { string n = Txt(e.Owner.Name); if (!g.Others.Contains(n)) g.Others.Add(n); }
            }
            return res;
        }

        // ============================================================ zakladka Fiefs
        /// <summary>Blok "Village works (N)" zakladki Fiefs. false - brak prac gracza albo klucz wylaczony. Watek glowny, nie rzuca.</summary>
        internal static bool WorksTab(out string header, out string body)
        {
            header = body = "";
            try
            {
                if (!On) return false;
                var ws = DistrictWorks.PlayerWorks();
                if (ws.Count == 0) return false;
                _dUi++;
                header = "Village works (" + ws.Count + ")";
                var sb = new StringBuilder(ws.Count * 320);
                foreach (var w in ws)
                {
                    if (sb.Length > 0) sb.Append("\n\n");
                    sb.Append(w.Main ? w.Village + " (main village" : w.Village + " (lands of " + w.District).Append(", market ").Append(w.Market).Append(") - ").Append(w.Kind);
                    double kit = w.Standing > 0.5 ? Math.Min(1.0, w.Units / w.Standing) : 0;
                    double ready = w.Units > 1e-6 ? Math.Min(1.0, w.Eff / w.Units) : 0;
                    if (w.Standing > 0.5)
                        sb.Append("\nKit in place: ").Append(Math.Round(100 * kit).ToString("0", Inv)).Append("% (").Append(w.Units.ToString("0.0", Inv)).Append(" of ")
                          .Append(Math.Round(w.Standing).ToString("0", Inv)).Append(" standing homesteads)");
                    else sb.Append("\nKit in place: ").Append(w.Units.ToString("0.0", Inv)).Append(" units");   // m112-inwest po recenzji: osad nie znaleziono w ksiedze wsi - bez "0%"
                    if (w.Units > 1e-6)
                    {
                        sb.Append(", ready ").Append(Math.Round(100 * ready).ToString("0", Inv)).Append('%');
                        if (w.FullDay > w.Today) sb.Append(" - full in ").Append(w.FullDay - w.Today).Append(w.FullDay - w.Today == 1 ? " day" : " days");   // test 24.10: bez surowego numeru doby kampanii (108820 nic graczowi nie mowi)
                    }
                    sb.Append("\nPut in ").Append(w.In.ToString("N0", Inv)).Append(" - paid to you ").Append(N0(w.Paid28)).Append(" in the last ").Append(w.PaidDaysN).Append(w.PaidDaysN == 1 ? " day, " : " days, ").Append(w.PaidAll.ToString("N0", Inv)).Append(" in all");
                    if (w.WarWith != null) sb.Append("\nPaused - at war with ").Append(w.WarWith).Append(": pays nothing until peace");
                    if (w.Paused != null) sb.Append('\n').Append(char.ToUpperInvariant(w.Paused[0])).Append(w.Paused.Substring(1));
                    if (w.Lost > 1e-3) sb.Append("\nLost to fire: ").Append(w.Lost.ToString("0.0", Inv)).Append(" units - the homesteads stand empty; you may rebuild when people come back");
                    if (w.Reb > 1e-3 && w.RebDay >= 0)
                        sb.Append("\nLost to fire - rebuild until day ").Append(w.RebDay).Append(" (").Append(Math.Max(0, w.RebDay - w.Today)).Append(" days left, about ").Append(N0(w.RebCost)).Append(')');
                    if (w.Idle > 1e-3) sb.Append("\nIdle: ").Append(w.Idle.ToString("0.0", Inv)).Append(" units wait in empty homesteads");
                }
                body = sb.ToString();
                return true;
            }
            catch (Exception e) { Stumble("WorksTab", e); header = body = ""; return false; }
        }

        /// <summary>Blok "Estates (N)" zakladki Fiefs (majatki BK rodu gracza). false - brak majatkow, BK albo klucz wylaczony. Watek glowny, nie rzuca.</summary>
        internal static bool EstatesTab(out string header, out string body)
        {
            header = body = "";
            try
            {
                if (!On) return false;
                var es = Estates();
                if (es.Count == 0) return false;
                _dUi++;
                header = "Estates (" + es.Count + ")";
                bool sales = EstateIncome187.On;
                bool tracked = VillageTakings.CartHookLive;   // m112-inwest po recenzji: wyplaty liczone tylko w oknie powrotu taboru (VillageTakingsWhole + latka wejscia BK)
                int from = Today() - 27;
                var sb = new StringBuilder(es.Count * 200);
                foreach (var g in ByVillage(es))
                {
                    if (sb.Length > 0) sb.Append("\n\n");
                    var bound = g.V.Village != null ? g.V.Village.Bound : null;
                    sb.Append(Txt(g.V.Name)).Append(bound != null ? " (near " + Txt(bound.Name) + ")" : "").Append(" - ").Append(g.Count == 1 ? "1 estate" : g.Count + " estates");
                    if (g.Acres > 0.5) sb.Append(", ").Append(N0(g.Acres)).Append(" acres");
                    if (g.Others.Count > 0) sb.Append(", held by ").Append(string.Join(", ", g.Others.ToArray()));
                    sb.Append("\nIncome: about ").Append(N0(g.Last)).Append(" a day").Append(sales ? " (its share of the village's trade tax, average of the last 28 days)" : " (Banner Kings estate income)");
                    if (g.Blocked > 0)   // m112-inwest po recenzji: BK Estate.Income = 0 (IncomeBlockedReason)
                        sb.Append("\nPaused").Append(g.Blocked < g.Count ? " (" + g.Blocked + " of " + g.Count + ")" : "").Append(" - ").Append(g.Why).Append(": pays nothing until that changes");
                    if (tracked)
                    {
                        double all, p28 = EstPaidSince(g.V, from, out all);
                        sb.Append("\nPaid to your clan since load: ").Append(N0(all)).Append(all > 0 ? " (" + N0(p28) + " in the last 28 days)" : "");
                        if (!sales) sb.Append(" (trade-tax share only)");
                    }
                    else sb.Append("\nPayouts are not tracked with the current settings (Village Takings Whole is off)");
                }
                body = sb.ToString();
                return true;
            }
            catch (Exception e) { Stumble("EstatesTab", e); header = body = ""; return false; }
        }

        /// <summary>Prawa czesc zakladki (rod bez miast i zamkow): krotkie podsumowanie prac i majatkow. null - nic (klucz wylaczony, brak prac i majatkow).</summary>
        internal static string NoFiefsText()
        {
            try
            {
                if (!On) return null;
                var sb = new StringBuilder(400);
                var ws = DistrictWorks.PlayerWorks();
                if (ws.Count > 0)
                {
                    double p28 = 0; long inAll = 0; int dn = 28; var names = new List<string>();
                    foreach (var w in ws) { p28 += w.Paid28; inAll += w.In; dn = Math.Min(dn, Math.Max(1, w.PaidDaysN)); if (!names.Contains(w.Village)) names.Add(w.Village); }
                    // test 24.10 (zdjecie): lewa strona mowila "w ostatnich 4 dobach" (historia od wczytania zapisu sprzed m112), prawa "28" - to samo okno po obu stronach
                    sb.Append("Your village works: ").Append(string.Join(", ", names.ToArray())).Append(" - ").Append(inAll.ToString("N0", Inv)).Append(" put in, ")
                      .Append(N0(p28)).Append(" paid to you in the last ").Append(dn).Append(dn == 1 ? " day." : " days.");
                }
                var es = Estates();
                if (es.Count > 0)
                {
                    double last = 0; var names = new List<string>();
                    foreach (var g in ByVillage(es)) { last += g.Last; names.Add(Txt(g.V.Name)); }
                    if (sb.Length > 0) sb.Append('\n');
                    sb.Append("Your estates: ").Append(string.Join(", ", names.ToArray())).Append(" - about ").Append(N0(last)).Append(" a day.");
                }
                if (sb.Length == 0) return null;
                sb.Append("\nSee the list on the left.");
                return sb.ToString();
            }
            catch (Exception e) { Stumble("NoFiefsText", e); return null; }
        }

        // ============================================================ doba
        /// <summary>ArmouryBehavior.OnDailyTick (po DistrictWorks.Daily): komunikat wyplat minionej doby (prace i majatki; przy 0 nic) i linia logu. Raz na dobe.</summary>
        internal static void Daily()
        {
            try
            {
                if (Campaign.Current == null) return;
                int today = Today();
                if (_msgDay == today) return;
                _msgDay = today;
                int day = today - 1;
                bool on = On;
                var per = new List<KeyValuePair<string, double>>();
                int works;
                double wPaid = DistrictWorks.PlayerPaidOn(day, per, out works);
                var ePer = new List<KeyValuePair<string, double>>();
                double ePaid = 0;
                foreach (var kv in _estPaid)
                {
                    double x; if (!kv.Value.Days.TryGetValue(day, out x) || !(x > 0)) continue;
                    ePaid += x; ePer.Add(new KeyValuePair<string, double>(Txt(kv.Key.Name), x));
                }
                var es = on ? Estates(true) : _mine;                 // m112-inwest po recenzji: przy wylaczonym kluczu bez pelnego przegladu wsi
                bool msgW = false, msgE = false;
                if (on && Math.Round(wPaid) >= 1) { Log.Player("Your village works paid you " + N0(wPaid) + " denars yesterday (" + List(per) + ")."); msgW = true; _dWorkMsg++; }
                if (on && Math.Round(ePaid) >= 1) { Log.Player("Your estates paid you " + N0(ePaid) + " denars yesterday (" + List(ePer) + ")."); msgE = true; _dEstMsg++; }
                bool start = !_startLogged;
                _startLogged = true;
                if (start || works > 0 || es.Count > 0 || wPaid > 0 || ePaid > 0 || _stDay > 0)
                {
                    double sesE = 0; foreach (var kv in _estPaid) sesE += kv.Value.All;
                    var sb = new StringBuilder(400);
                    sb.Append(P).Append("dzien ").Append(day).Append(start ? " (start sesji)" : "").Append(" | klucz InvestmentsInFiefs ").Append(on ? "WL" : "WYL")
                      .Append(" | prace gracza ").Append(works).Append(", wyplacone w dobie ").Append(wPaid.ToString("0.0", Inv)).Append(per.Count > 0 ? " (" + List(per) + ")" : "")
                      .Append(" | majatki BK gracza ").Append(es.Count).Append(_bkOk ? "" : " (BK nieczynne)").Append(", udzial z utargu w dobie ").Append(ePaid.ToString("0", Inv))
                      .Append(" (wyplat ").Append(_dEstNotes).Append("), od wczytania ").Append(sesE.ToString("0", Inv))
                      .Append(" | komunikaty: prace ").Append(msgW ? "TAK" : "nie").Append(", majatki ").Append(msgE ? "TAK" : "nie")
                      .Append(" | odczyty zakladki ").Append(_dUi).Append(", lista - ").Append(SideVillageFiefUi.State)
                      .Append(" | potkniecia doby ").Append(_stDay).Append(", sesji ").Append(_st).Append('.');
                    Log.Info(sb.ToString());
                }
                // stare doby majatkow (pamiec sesji, 28 dob)
                foreach (var kv in _estPaid)
                {
                    if (kv.Value.Days.Count <= 28) continue;
                    var old = new List<int>(); foreach (var k in kv.Value.Days.Keys) if (k < today - 27) old.Add(k);
                    foreach (var k in old) kv.Value.Days.Remove(k);
                }
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally { _stDay = 0; _dUi = 0; _dEstNotes = 0; }
        }

        private static string List(List<KeyValuePair<string, double>> per)
        {
            var parts = new List<string>(per.Count);
            foreach (var kv in per) if (Math.Round(kv.Value) >= 1) parts.Add(kv.Key + " " + N0(kv.Value));
            if (parts.Count == 0) foreach (var kv in per) parts.Add(kv.Key);   // same ulamki - nazwy bez kwot
            return string.Join(", ", parts.ToArray());
        }
    }
}
