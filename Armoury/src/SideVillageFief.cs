using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// n20-wioska (noc 19/20.10) - WIOSKA POBOCZNA GRACZA. Jeff: "czy mozemy naprawic to, ze zaczynam jako lord jakiejs wioski pobocznej! Nie wioski glownej tylko pobocznej,
    /// jest ona moja i daje mi jakis dochod ... moj herb przy niej ... moge wjechac i dostac tylko dla tej wioski opcje, jak nocleg, inwestuj, rozwijaj".
    ///  W1 START (klucz SideVillageFiefStart): start BK "Indebted Lord" (DefaultStartOptions.IndebtedLord) wola TitleManager.GiveLordshipOnKingdomJoin(krolestwo, rod gracza,
    ///     force: true) - JEDYNE wywolanie tej metody w BK (dekompilacja BannerKings.dll: grep). BK daje wtedy tytul Lordship najmniejszej wsi gry krola (deJure i deFacto;
    ///     sama wies gry zostaje przy zamku - ChangeOwnerOfSettlementAction tylko dla miast i zamkow). Prefiks: dla rodu gracza tytul pominiety (return false), gracz
    ///     dostaje WIOSKE POBOCZNA (wiersz ukladu wiosek arm_map_villages, nie przysiolek) w okregu wsi gry z TEGO krolestwa, najblizej partii gracza; wies gry zostaje
    ///     przy panu. Wioska stoi w ksiedze wiosek (stan stoi / uszkodzona). Brak kandydata (ksiega nieczynna) - proba co godzine, po 3 dobach tytul BK jak dotad.
    ///  W2 ZAPIS: arm_side_fief (SaveText): "sf1|uid|okreg|nazwa|doba|wyplacone|srednia". Brak klucza (stary zapis Jeffa) = brak wioski gracza.
    ///  W3 DOCHOD (klucz SideVillageFiefIncome) - r4-wioska (20.10, dotad przy wozie: u Jeffa i w autotescie 5 dob "do gracza 0"): CO DOBE (OnDailyTick, Pay) naleznosc
    ///     wioski = wartosc dnia jej produkcji (VillageCrafts.PartsOf) x stawka pana z utargu (czynny model podatku wsi), z licznika podatku pana wsi gry okregu
    ///     (TradeTaxAccumulated), reszta z kiesy pana - pan ma mniej dokladnie o tyle. Wojna z krolestwem okregu, gracz panem okregu, okreg rabowany, wioska spalona -
    ///     bez przelewu (powod w menu i w zakladce Fiefs). Podpowiedz zlota (CalculateClanGoldChange z opisami, bez pobrania) - linia z naleznoscia dnia (tylko widok).
    ///     Zakladka Fiefs ekranu rodu - SideVillageFiefUi (lista) i NoFiefsPostfix (tekst bez miast i zamkow).
    ///  W4 MAPA: VillageHolders.Of - gospodarzem tej wioski jest gracz (herb rodu gracza na proporczyku i w dymku, linia "This village is yours").
    ///  W5 MENU (klucz SideVillageFiefMenu): klik na wioske na mapie = jazda do niej (MapState.ProcessTravel, wzor zajazdow) i menu po dojechaniu; postoj przy niej - pytanie
    ///     raz na postoj; w menu wsi gry okregu "Visit X (your village)". Menu arm_side_fief (zwykle, nie oczekiwania): nocleg za darmo - pod dachem (recenzja: klucz
    ///     SideVillageFiefLodging, TownInns.HostSleep - stan noclegu jak wspolna izba karczmy: czekanie do switu i premia "wypoczeci"; bez klucza, karczm, w wojnie z krolestwem
    ///     okregu, przy rabunku albo spalonej wiosce - sen jak w obozie, podpowiedz mowi dlaczego), prace okregu tej wioski (DistrictWorks.OpenPartFor; opcja wylaczona z powodem,
    ///     gdy prac nie da sie kupic), przeglad, wyjazd.
    ///  Furtka autotestu: DLL autotestu CrashScribe (LogGate.Mode 1/2) + plik Documents\Mount and Blade II Bannerlord\CrashScribe\armoury-wioska-gracza.flag - w pierwszej
    ///     godzinie z czynna ksiega wiosek proba wola TE SAMA metode BK (odbiciem, force: true) dla krolestwa najblizszej wsi - prefiks, nadanie, herb, zapis, menu - linia "test".
    /// Linie logu: "Wioska gracza (n20): ..." (start sesji, nadanie, doba, test). Bledy liczone (potkniecia), nic nie gasimy globalnie.
    ///
    /// m113-zakup (24.10, Jeff: "kupno wioski za dlugi lordow ... wioske tak"; LandSale.cs) - WIELE WIOSEK GRACZA (F1):
    ///  - Own {Uid, Dist, Name, Day0, Paid, Avg, Price, From}: PIERWSZA wioska (pola sf1 - dom, skrzynia, spacer, nadanie startu) i lista DODATKOWYCH (kupione: zapis
    ///    arm_side_fief2 "sf2~uid|okreg|nazwa|doba0|wyplacone|srednia|cena|od_rodu~..."; starsza DLL go nie zna - dodatkowe wioski przepadna, pierwsza zostaje).
    ///    Uid / DistId / Name to dalej PIERWSZA wioska (SideVillageHome: dom, skrzynia, spacer). MenuUid / MenuDistId / MenuName - wioska OTWARTEGO menu (_cur):
    ///    ustawia ja klik na mapie, dojazd (Tick), pytanie przy postoju i opcja "Visit" we wsi gry (dwie i wiecej wiosek gracza w jednym okregu - okno wyboru).
    ///    Wszystkie opcje arm_side_fief i tekst menu - _cur; handel domu - _cur; dom, skrzynia i spacer - tylko przy pierwszej wiosce.
    ///  - DOCHOD kazdej wioski z osobna: DueFor(wies, uid) - czysta wartosc (wartosc dnia czesci x H okregu x stawka pana; bez bramek gracza, pamiec na uid i dobe,
    ///    jeden PartsOf na okreg - takze cena ziemi LandSale); Due(wioska) = bramki gracza + DueFor. Pay / Expect / zakladka Fiefs / podpowiedz zlota - po wszystkich
    ///    wioskach, z limitem na platnika (licznik podatku wsi i kiesa platnika liczone raz, nie na kazda wioske osobno).
    ///  - F2 (klucz SideVillagePayerIsTaxHolder): PLATNIK = posiadacz podatku okregu (BK GetActualOwner: tytul de iure wsi, gdy jego frakcja = frakcja wsi; inaczej
    ///    glowa rodu pana zamku) - najpierw licznik podatku wsi gry, potem jego kiesa. Wylaczony - glowa rodu pana zamku (jak dotad).
    ///  - F3: gracz (albo czlonek jego rodu) jest posiadaczem podatku okregu - wioska nie placi ("its whole tax is yours already").
    ///  - F4 (SideVillageIncomeSeizable): PaidAvg28 - srednia 28 dob wyplat wiosek gracza do ClanIncomeBook.VillageIncomeOf(rod gracza) (zajecie 168 bierze ja jak dochod wsi).
    ///  - F6 (SideVillageDuesInLordD): DrawnAvg28(rod) - srednia 28 dob tego, co platnik dal Z KIESY (nie z licznika) - ziemia D stalego platnika mniejsza o tyle.
    /// </summary>
    internal static class SideVillageFief
    {
        private const string P = "Wioska gracza (n20): ";
        internal const string Menu = "arm_side_fief";
        internal const string OptVillage = "arm_side_fief_visit";
        private const string SaveVer = "sf1", PendVer = "sfp", SaveVer2 = "sf2";   // sfp|krolestwo|doba - nadanie startu BK jeszcze czeka
        internal const string AutoFlagName = "armoury-wioska-gracza.flag";
        private const float EnterR = 1.6f, LeaveR = 3.5f, ArriveEps = 0.25f;
        private const int FallbackDays = 3;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // ------------------------------------------------------------ m113-zakup: wioska gracza
        internal sealed class Own
        {
            public string Uid, Dist, Name, From = "";   // From - id rodu sprzedawcy ("" - nadanie startu)
            public int Day0 = -1; public long Paid, Price; public double Avg;
            public bool Active = true;                  // false - uid spoza czynnego ukladu wiosek (zostaje w zapisie, bez dochodu i menu)
            // sesja
            public int ShareDay = -1; public double Share; public string ShareNote = "-";
            public int DueDay = -1, DueSigV, DueCode; public long Due; public string DueWhy = "", DueWhyPl = "-";
            public long DPaid; public float Px, Py; public bool PosSet;
            public TextObject TipTxt; public string TipName;
        }

        // ------------------------------------------------------------ stan kampanii (zapis arm_side_fief i arm_side_fief2)
        private static Own _p;                                  // pierwsza wioska (sf1)
        private static readonly List<Own> _extra = new List<Own>();   // kupione (sf2)
        private static readonly HashSet<string> _mine = new HashSet<string>(StringComparer.Ordinal);
        private static Own _cur;                                // wioska otwartego menu
        private static bool _layoutChecked;
        // oczekujace nadanie (start BK przed czynna ksiega) - w zapisie jako "sfp|krolestwo|doba" (Export/Import); po wczytaniu proba co godzine (TryPending),
        // po FallbackDays bez kandydata - tytul BK jak dotad
        private static string _pendKingdom; private static int _pendDay = -1; private static string _pendHow;

        // ------------------------------------------------------------ sesja
        private static Harmony _harmony;
        private static bool _bkTried, _bkWired, _finWired, _bypass, _startLogged, _testDone;
        private static MethodInfo _giveLordship;
        private static object _finTriedFor;
        private static Type _finDecl;
        private static int _st, _stDay;
        private static readonly HashSet<string> _err = new HashSet<string>(StringComparer.Ordinal);
        private sealed class HolderC { public VillageHolders.Holder H; public Clan Lord; public Banner B; }
        private static readonly Dictionary<string, HolderC> _holders = new Dictionary<string, HolderC>(StringComparer.Ordinal);
        private static string _pendingClick, _askedUid;
        private static long _lastCheck;
        // liczniki doby
        private static int _dCarts, _dWar, _dSelf, _dNoShare, _dIntercept, _dOpen, _dClick, _dAsk, _dRest, _dRoof, _dInvest, _dTip, _dPick;
        // recenzja (drobne): czy prace tej wioski da sie dzis kupic - wycena DistrictWorks.QuotesOf raz na godzine gry (warunek opcji menu, nie co klatke)
        private static int _invHour = -1; private static string _invUid; private static bool _invOk; private static string _invWhy;
        private static long _dPaid, _dTax;

        internal static void SetHarmony(Harmony h) { _harmony = h; }

        /// <summary>Konstruktor ArmouryBehavior (kazda kampania): stan od zera (zapis wczyta Import).</summary>
        internal static void Reset()
        {
            _p = null; _extra.Clear(); _mine.Clear(); _cur = null; _layoutChecked = false;
            _pendKingdom = null; _pendDay = -1; _pendHow = null;
            _startLogged = _testDone = false; _bypass = false;
            _st = _stDay = 0; lock (_err) _err.Clear();
            _holders.Clear();
            _pendingClick = _askedUid = null;
            _invHour = -1; _invUid = null; _invOk = false; _invWhy = null;
            _dueFor.Clear(); _drawn.Clear(); _drawnToday.Clear(); _paidRing = new R28();   // m113-zakup
            DayZero();
            SideVillageHome.Reset();   // r4-dom: dom, skrzynia, handel, spacer
        }

        private static void DayZero() { _dCarts = _dWar = _dSelf = _dNoShare = _dIntercept = _dOpen = _dClick = _dAsk = _dRest = _dRoof = _dInvest = _dTip = _dPick = 0; _dPaid = _dTax = 0; _dFromTax = _dFromLord = _dShort = 0; _stDay = 0; }

        private static void Stumble(string where, Exception e)
        {
            _st++; _stDay++;
            bool first; lock (_err) first = _err.Add(where);
            if (first || _st % 200 == 0) Log.Error("SideVillageFief." + where + " (potkniecie " + _st + ")", e);
        }

        private static int Today() { return (int)CampaignTime.Now.ToDays; }

        internal static bool HasFief { get { return _p != null && _p.Uid != null && _p.Dist != null; } }

        // r4-dom (SideVillageHome): odczyt PIERWSZEJ wioski gracza - uid, wies gry okregu, nazwa (dom, skrzynia, spacer)
        internal static string Uid { get { return _p != null ? _p.Uid : null; } }
        internal static string DistId { get { return _p != null ? _p.Dist : null; } }
        internal static string Name { get { return _p != null ? _p.Name : null; } }
        // m113-zakup: wioska otwartego menu (pierwsza, gdy menu nie wybralo innej)
        private static Own CurOwn { get { var c = _cur; return c != null && c.Active && (ReferenceEquals(c, _p) || _extra.Contains(c)) ? c : _p; } }
        internal static string MenuUid { get { var c = CurOwn; return c != null ? c.Uid : null; } }
        internal static string MenuDistId { get { var c = CurOwn; return c != null ? c.Dist : null; } }
        internal static string MenuName { get { var c = CurOwn; return c != null ? c.Name : null; } }
        internal static bool CurIsPrimary { get { return HasFief && ReferenceEquals(CurOwn, _p); } }
        internal static int OwnedCount { get { int n = HasFief ? 1 : 0; foreach (var o in _extra) if (o.Active) n++; return n; } }

        /// <summary>m113-zakup: wszystkie czynne wioski gracza (pierwsza + dodatkowe). Nowa lista - wolajacy moze ja zmieniac.</summary>
        internal static List<Own> Owned()
        {
            var l = new List<Own>(1 + _extra.Count);
            if (HasFief) l.Add(_p);
            foreach (var o in _extra) if (o != null && o.Active) l.Add(o);
            return l;
        }

        internal static Own Find(string uid)
        {
            if (uid == null) return null;
            if (HasFief && string.Equals(_p.Uid, uid, StringComparison.Ordinal)) return _p;
            foreach (var o in _extra) if (o != null && o.Active && string.Equals(o.Uid, uid, StringComparison.Ordinal)) return o;
            return null;
        }

        private static void RebuildMine()
        {
            _mine.Clear();
            foreach (var o in Owned()) if (o.Uid != null) _mine.Add(o.Uid);
            _holders.Clear();
        }

        /// <summary>Wioska gracza o tym uid (widok mapy, gospodarz, dymek) - kazda czynna wioska gracza. Sam odczyt.</summary>
        internal static bool IsMine(string uid) { return uid != null && _mine.Contains(uid); }

        /// <summary>Widok mapy: wioska gracza z menu (klik, kursor raczki).</summary>
        internal static bool Clickable(string uid) { var s = Settings.Current; return s != null && s.SideVillageFiefMenu && IsMine(uid); }

        /// <summary>m113-zakup (LandSale Z6): nowa wioska gracza - pierwsza (sf1), gdy gracz nie ma zadnej, inaczej dodatkowa (sf2). null - uid juz gracza albo brak danych.</summary>
        internal static Own AddOwned(string uid, string dist, string name, long price, string sellerClanId, int today)
        {
            if (string.IsNullOrEmpty(uid) || string.IsNullOrEmpty(dist) || IsMine(uid)) return null;
            var o = new Own { Uid = uid, Dist = dist, Name = string.IsNullOrEmpty(name) ? uid : name, Day0 = today, Price = Math.Max(0, price), From = sellerClanId ?? "" };
            bool first = !HasFief;
            if (first) _p = o; else _extra.Add(o);
            RebuildMine();
            PosFromBook();
            Log.Info(P + "nowa wioska gracza (m113 kupno) - " + o.Name + " (" + uid + "), okreg " + dist + ", cena " + o.Price + ", od rodu " + (o.From.Length > 0 ? o.From : "-")
                     + " - " + (first ? "jako PIERWSZA (sf1: dom, skrzynia)" : "jako dodatkowa (sf2), wiosek gracza " + OwnedCount) + ".");
            return o;
        }

        /// <summary>m113-zakup (Z7 odkup): wioska przestaje byc gracza. Pierwsza - tylko z pusta skrzynia domu (why); pierwsza dodatkowa staje sie wtedy pierwsza.
        /// false - nie gracza albo skrzynia nie pusta.</summary>
        internal static bool RemoveOwned(string uid, out string why)
        {
            why = null;
            var o = Find(uid);
            if (o == null) { why = "This village is not yours."; return false; }
            if (ReferenceEquals(o, _p))
            {
                if (!SideVillageHome.ChestEmpty()) { why = "Empty the chest of your house there first."; return false; }
                Own next = null;
                foreach (var x in _extra) if (x != null && x.Active) { next = x; break; }
                if (next != null) _extra.Remove(next);
                _p = next;
                Log.Info(P + "pierwsza wioska " + o.Name + " (" + o.Uid + ") odsprzedana" + (next != null ? " - pierwsza jest teraz " + next.Name + " (" + next.Uid + ")" : " - gracz nie ma juz wioski") + ".");
            }
            else { _extra.Remove(o); Log.Info(P + "dodatkowa wioska " + o.Name + " (" + o.Uid + ") odsprzedana; wiosek gracza " + OwnedCount + "."); }
            if (ReferenceEquals(_cur, o)) _cur = null;
            _invHour = -1;
            RebuildMine();
            return true;
        }

        // ============================================================ zapis
        internal static string Export()
        {
            if (!HasFief) return _pendKingdom != null ? PendVer + "|" + Clean(_pendKingdom) + "|" + _pendDay.ToString(Inv) : "";   // nadanie czeka (ksiega nieczynna) - przezyje zapis
            return SaveVer + "|" + Clean(_p.Uid) + "|" + Clean(_p.Dist) + "|" + Clean(_p.Name) + "|" + _p.Day0.ToString(Inv) + "|" + _p.Paid.ToString(Inv) + "|" + _p.Avg.ToString("0.###", Inv);
        }

        private static string Clean(string s) { return (s ?? "").Replace("|", "/").Replace("~", "-"); }

        internal static void Import(string data)
        {
            _p = null; _cur = null; _layoutChecked = false;
            if (string.IsNullOrEmpty(data)) { RebuildMine(); return; }
            try
            {
                var f = data.Split('|');
                if (f.Length >= 3 && f[0] == PendVer && f[1].Length > 0)
                {
                    _pendKingdom = f[1]; _pendHow = "start BK (Indebted Lord), po wczytaniu";
                    if (!int.TryParse(f[2], NumberStyles.Integer, Inv, out _pendDay)) _pendDay = Today();
                    return;
                }
                if (f.Length < 7 || f[0] != SaveVer || f[1].Length == 0 || f[2].Length == 0) { Log.Info(P + "zapis arm_side_fief nieznany (" + data.Length + " znakow) - pominiety, brak wioski gracza."); return; }
                var o = new Own { Uid = f[1], Dist = f[2], Name = f[3].Length > 0 ? f[3] : f[1] };
                int.TryParse(f[4], NumberStyles.Integer, Inv, out o.Day0);
                long.TryParse(f[5], NumberStyles.Integer, Inv, out o.Paid);
                double.TryParse(f[6], NumberStyles.Float, Inv, out o.Avg);
                if (double.IsNaN(o.Avg) || o.Avg < 0) o.Avg = 0;
                _p = o;
            }
            catch (Exception e) { _p = null; Stumble("Import", e); }
            finally { RebuildMine(); }
        }

        /// <summary>m113-zakup: zapis arm_side_fief2 - dodatkowe wioski gracza (takze nieczynne - wracaja do zapisu bez zmian). Pusto - brak dodatkowych.</summary>
        internal static string Export2()
        {
            if (_extra.Count == 0) return "";
            var sb = new StringBuilder(64 + 96 * _extra.Count);
            sb.Append(SaveVer2);
            foreach (var o in _extra)
            {
                if (o == null || o.Uid == null) continue;
                sb.Append('~').Append(Clean(o.Uid)).Append('|').Append(Clean(o.Dist)).Append('|').Append(Clean(o.Name)).Append('|').Append(o.Day0.ToString(Inv))
                  .Append('|').Append(o.Paid.ToString(Inv)).Append('|').Append(o.Avg.ToString("0.###", Inv)).Append('|').Append(o.Price.ToString(Inv)).Append('|').Append(Clean(o.From));
            }
            return sb.ToString();
        }

        /// <summary>m113-zakup: odczyt arm_side_fief2 (PO arm_side_fief). Wpis z mniej niz 6 polami, uid pierwszej wioski albo powtorzony - pominiety. Brak pierwszej
        /// wioski (sf1 pusty) przy dodatkowych - pierwsza dodatkowa staje sie pierwsza. Uid spoza ukladu wiosek - sprawdzany po wczytaniu ukladu (CheckLayout).</summary>
        internal static void Import2(string data)
        {
            _extra.Clear();
            if (string.IsNullOrEmpty(data)) { RebuildMine(); return; }
            int ok = 0, bad = 0;
            try
            {
                var e = data.Split('~');
                if (e.Length == 0 || e[0] != SaveVer2) { Log.Info(P + "zapis arm_side_fief2 nieznany (" + data.Length + " znakow) - pominiety."); return; }
                var seen = new HashSet<string>(StringComparer.Ordinal);
                if (HasFief) seen.Add(_p.Uid);
                for (int i = 1; i < e.Length; i++)
                {
                    var f = e[i].Split('|');
                    if (f.Length < 6 || f[0].Length == 0 || f[1].Length == 0 || !seen.Add(f[0])) { bad++; continue; }
                    var o = new Own { Uid = f[0], Dist = f[1], Name = f[2].Length > 0 ? f[2] : f[0] };
                    int.TryParse(f[3], NumberStyles.Integer, Inv, out o.Day0);
                    long.TryParse(f[4], NumberStyles.Integer, Inv, out o.Paid);
                    double.TryParse(f[5], NumberStyles.Float, Inv, out o.Avg);
                    if (double.IsNaN(o.Avg) || o.Avg < 0) o.Avg = 0;
                    if (f.Length > 6) long.TryParse(f[6], NumberStyles.Integer, Inv, out o.Price);
                    if (f.Length > 7) o.From = f[7];
                    _extra.Add(o); ok++;
                }
                if (!HasFief && _extra.Count > 0) { _p = _extra[0]; _extra.RemoveAt(0); Log.Info(P + "arm_side_fief2 bez pierwszej wioski - pierwsza jest " + _p.Name + " (" + _p.Uid + ")."); }
                Log.Info(P + "zapis arm_side_fief2: dodatkowe wioski " + ok + (bad > 0 ? ", pominiete wpisy " + bad : "") + ".");
            }
            catch (Exception ex) { Stumble("Import2", ex); }
            finally { RebuildMine(); }
        }

        /// <summary>m113-zakup: dodatkowa wioska o uid spoza czynnego ukladu wiosek (inny uklad, mod mapy) - nieczynna (bez dochodu i menu), zostaje w zapisie. Raz, gdy
        /// uklad jest wczytany.</summary>
        private static void CheckLayout()
        {
            if (_layoutChecked || _extra.Count == 0) { _layoutChecked = true; return; }
            var recs = VillageBook.LayoutRecords();
            if (recs == null) return;
            _layoutChecked = true;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var r in recs) if (r != null && !string.IsNullOrEmpty(r.Uid)) ids.Add(r.Uid);
            int off = 0;
            foreach (var o in _extra)
            {
                bool on = o.Uid != null && ids.Contains(o.Uid);
                if (!on && o.Active) { off++; Log.Info(P + "dodatkowa wioska " + o.Name + " (" + o.Uid + ") nie stoi w czynnym ukladzie wiosek - NIECZYNNA (bez dochodu i menu), zostaje w zapisie."); }
                o.Active = on;
            }
            if (off > 0) RebuildMine();
        }

        // ============================================================ latki (OnSessionLaunched - kampania istnieje)
        internal static void EnsureHooks()
        {
            var h = _harmony;
            if (h == null || Campaign.Current == null) return;
            if (!_bkTried)
            {
                _bkTried = true;
                try
                {
                    var t = AccessTools.TypeByName("BannerKings.Managers.TitleManager");
                    _giveLordship = t != null ? AccessTools.DeclaredMethod(t, "GiveLordshipOnKingdomJoin", new[] { typeof(Kingdom), typeof(Clan), typeof(bool) }) : null;
                    if (_giveLordship != null)
                    {
                        h.Patch(_giveLordship, prefix: new HarmonyMethod(typeof(SideVillageFief), nameof(GiveLordshipPrefix)) { priority = Priority.First });
                        _bkWired = true;
                    }
                }
                catch (Exception e) { _bkWired = false; Stumble("EnsureHooks(BK)", e); }
                // przeglad calosci 20.10: okno wyboru startu BK obiecuje przy "Indebted Lord" tytul Lordship ({=CiWCMaQV}), a przy kluczu jest wioska poboczna.
                // BK odtwarza opcje startu w swoim OnSessionLaunched (DefaultStartOptions.Initialize - kolejnosc wobec nas nieznana), wiec nie podmieniamy obiektu,
                // tylko wynik gettera StartOptionVM.ShortDescription (czyta go wiazanie okna przy pokazaniu, po OnSessionLaunched; metoda z wywolaniami, nie 1-bajtowy ret)
                try
                {
                    var vm = AccessTools.TypeByName("BannerKings.UI.CampaignStart.StartOptionVM");
                    var g = vm != null ? AccessTools.DeclaredPropertyGetter(vm, "ShortDescription") : null;
                    _vmOption = vm != null ? AccessTools.DeclaredPropertyGetter(vm, "Option") : null;
                    if (g != null && _vmOption != null) { h.Patch(g, postfix: new HarmonyMethod(typeof(SideVillageFief), nameof(StartTextPostfix))); _startTxtWired = true; }
                }
                catch (Exception e) { _startTxtWired = false; Stumble("EnsureHooks(opis startu BK)", e); }
                // r4-wioska: prawa czesc zakladki Fiefs ("Clan Does Not Own Any Fiefs") - NoFiefsText po RefreshValues ClanFiefsVM (metoda klasy bazowej - takze NavalClanFiefsVM,
                // ktory jej nie nadpisuje); bez UIExtenderEx, wiec dziala takze bez wstawki listy (SideVillageFiefUi)
                try
                {
                    var rv = AccessTools.DeclaredMethod(typeof(TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.Categories.ClanFiefsVM), "RefreshValues");
                    if (rv != null) { h.Patch(rv, postfix: new HarmonyMethod(typeof(SideVillageFief), nameof(NoFiefsPostfix))); _noFiefsWired = true; }
                }
                catch (Exception e) { _noFiefsWired = false; Stumble("EnsureHooks(Fiefs)", e); }
            }
            var fin = Campaign.Current.Models != null ? Campaign.Current.Models.ClanFinanceModel : null;
            if (fin != null && !ReferenceEquals(fin, _finTriedFor))
            {
                _finTriedFor = fin;
                try
                {
                    var args = new[] { typeof(Clan), typeof(bool), typeof(bool), typeof(bool) };
                    var any = fin.GetType().GetMethod("CalculateClanGoldChange", BindingFlags.Public | BindingFlags.Instance, null, args, null);
                    var decl = any != null ? any.DeclaringType : null;
                    var m = decl != null ? AccessTools.DeclaredMethod(decl, "CalculateClanGoldChange", args) : null;
                    if (m != null && !m.IsAbstract && decl != _finDecl)
                    {
                        h.Patch(m, postfix: new HarmonyMethod(typeof(SideVillageFief), nameof(GoldTipPostfix)) { priority = Priority.Last });
                        _finDecl = decl; _finWired = true;
                    }
                    else if (m != null && decl == _finDecl) _finWired = true;
                }
                catch (Exception e) { _finWired = false; Stumble("EnsureHooks(model finansow)", e); }
                // r4-wioska (prowadzacy): panel Finance ekranu rodu liczy "Total Income" z CalculateClanIncome (ClanManagementVM.RefreshDailyValues) - ta sama
                // naleznosc dnia tylko do widoku, zeby przychod i "Daily Change" zgadzaly sie z podpowiedzia zlota
                try
                {
                    var args = new[] { typeof(Clan), typeof(bool), typeof(bool), typeof(bool) };
                    var any = fin.GetType().GetMethod("CalculateClanIncome", BindingFlags.Public | BindingFlags.Instance, null, args, null);
                    var decl = any != null ? any.DeclaringType : null;
                    var m = decl != null ? AccessTools.DeclaredMethod(decl, "CalculateClanIncome", args) : null;
                    if (m != null && !m.IsAbstract && decl != _incDecl)
                    {
                        h.Patch(m, postfix: new HarmonyMethod(typeof(SideVillageFief), nameof(IncomeViewPostfix)) { priority = Priority.Last });
                        _incDecl = decl; _incWired = true;
                    }
                    else if (m != null && decl == _incDecl) _incWired = true;
                }
                catch (Exception e) { _incWired = false; Stumble("EnsureHooks(przychod rodu)", e); }
            }
        }

        private static Type _incDecl;
        private static bool _incWired;

        /// <summary>r4-wioska (prowadzacy): postfiks CalculateClanIncome czynnego modelu - rod gracza, BEZ pobrania (applyWithdrawals = false): naleznosc dnia wioski
        /// (ta sama co w podpowiedzi zlota). Tylko widok: pieniadze ida z Pay w OnDailyTick; dzienny przelew gry idzie przez CalculateClanGoldChange z pobraniem,
        /// nie przez ta metode, wiec nie ma podwojnej wyplaty. m113-zakup: linia na kazda wioske gracza (limit na platnika - ExpectEach).</summary>
        // m105-optym KtKnownPatches (KingdomTreasury.Optim105.cs): przy applyWithdrawals = false tylko odczyt, bez MBRandom; zmiana tresci = przeglad KtSkip
        public static void IncomeViewPostfix(object __instance, Clan __0, bool __1, bool __2, ref ExplainedNumber __result)
        {
            try
            {
                if (__2 || __0 == null || !HasFief || !ReferenceEquals(__0, Clan.PlayerClan)) return;
                var s = Settings.Current;
                if (s == null || !s.SideVillageFiefIncome || Campaign.Current == null || !ReferenceEquals(__instance, Campaign.Current.Models.ClanFinanceModel)) return;
                AddLines(ref __result);
            }
            catch (Exception e) { Stumble("IncomeViewPostfix", e); }
        }

        /// <summary>m113-zakup: linie wiosek gracza w wyniku modelu finansow (podpowiedz zlota, panel Finance) - kazda wioska osobno, obciete do tego, co platnik moze dac.</summary>
        private static int AddLines(ref ExplainedNumber r)
        {
            int n = 0;
            var l = new List<KeyValuePair<Own, long>>();
            ExpectEach(l);
            foreach (var kv in l)
            {
                var o = kv.Key; float x = kv.Value;
                if (x < 1f) continue;
                if (o.TipTxt == null || !ReferenceEquals(o.TipName, o.Name)) { o.TipName = o.Name; o.TipTxt = new TextObject("{=!}" + (o.Name ?? "?") + " (your side village: its share of the lord's tax)"); }
                r.Add(x, o.TipTxt);
                n++;
            }
            return n;
        }

        private static MethodInfo _vmOption;
        private static bool _startTxtWired, _noFiefsWired;

        /// <summary>r4-wioska: postfiks ClanFiefsVM.RefreshValues - przy wioskce gracza tekst prawej czesci zakladki Fiefs (widoczny tylko bez miast i zamkow) mowi o niej
        /// zamiast "Clan Does Not Own Any Fiefs". Klucz SideVillageFiefClanTab. Tylko napis - przy bledzie zostaje tekst gry.
        /// m112-inwest (Jeff: "ma byc to poakzane w thiefts zeby wedizal gdzie mam majtek i inwestycje"): do tego krotkie podsumowanie prac i majatkow gracza
        /// (FiefInvestments.NoFiefsText, klucz InvestmentsInFiefs) - takze bez wioski pobocznej.</summary>
        public static void NoFiefsPostfix(TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.Categories.ClanFiefsVM __instance)
        {
            try
            {
                var s = Settings.Current;
                if (__instance == null || s == null) return;
                FiefInvestments.Opened(__instance);                    // m112-inwest po recenzji: nowy ekran - skan majatkow od nowa (nie rzuca)
                string t = s.SideVillageFiefClanTab && HasFief ? NoFiefsText() : null;
                string inv = null;
                if (!__instance.IsAnyValidFiefSelected)                // m112-inwest po recenzji: tekst i tak ukryty przy miastach/zamkach - bez przegladu prac i majatkow
                    try { inv = FiefInvestments.NoFiefsText(); } catch (Exception e) { FiefInvestments.UiStumble("NoFiefsPostfix", e); }
                if (inv != null) t = (t ?? "Your clan holds no towns or castles.") + "\n\n" + inv;
                if (t != null) __instance.NoFiefsText = t;
            }
            catch (Exception e) { Stumble("NoFiefsPostfix", e); }
        }
        private const string StartText = "Start as a lord in a kingdom, holding one of its side villages - it flies your banner and pays you a share of the lord's tax. Reduced influence for 5 years. Gain Scholarship skill.";

        /// <summary>Postfiks StartOptionVM.ShortDescription (BK): opcja "start_lord" przy kluczu SideVillageFiefStart - opis wioski pobocznej zamiast tytulu Lordship.</summary>
        public static void StartTextPostfix(object __instance, ref string __result)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.SideVillageFiefStart || __instance == null || _vmOption == null) return;
                var so = _vmOption.Invoke(__instance, null) as TaleWorlds.ObjectSystem.MBObjectBase;
                if (so != null && so.StringId == "start_lord") __result = StartText;
            }
            catch { }   // tylko napis - przy bledzie zostaje tekst BK
        }

        // ============================================================ W1: start BK "Indebted Lord"
        /// <summary>Prefiks BK TitleManager.GiveLordshipOnKingdomJoin(krolestwo, rod, force): dla rodu gracza (przy kluczu) bez tytulu wsi gry - wioska poboczna.</summary>
        public static bool GiveLordshipPrefix(Kingdom __0, Clan __1, bool __2)
        {
            if (_bypass) return true;
            try
            {
                var s = Settings.Current;
                if (s == null || !s.SideVillageFiefStart || __0 == null || __1 == null || !ReferenceEquals(__1, Clan.PlayerClan)) return true;
                if (HasFief) { Log.Info(P + "BK GiveLordshipOnKingdomJoin dla gracza przy wiosce juz nadanej (" + _p.Name + ") - tytul BK pominiety."); return false; }
                _dIntercept++;
                _pendKingdom = __0.StringId; _pendDay = Today(); _pendHow = "start BK (Indebted Lord)";
                Log.Info(P + "BK GiveLordshipOnKingdomJoin(" + __0.StringId + ", gracz, force " + __2 + ") - tytul Lordship wsi gry POMINIETY (wies gry zostaje przy panu), gracz dostanie wioske poboczna krolestwa " + Txt(__0.Name) + ".");
                TryPending();
                // recenzja (drobne): wyjatek w nadaniu (TryPending lapie go i kasuje oczekiwanie) - bez wioski i bez czekania; wtedy tytul BK jak dotad, nie nic
                if (!HasFief && _pendKingdom == null)
                {
                    Log.Info(P + "nadanie wioski pobocznej nie powiodlo sie (blad, linia wyzej) - tytul BK jak dotad.");
                    return true;
                }
                return false;
            }
            catch (Exception e) { Stumble("GiveLordshipPrefix", e); return true; }   // blad - BK jak dotad
        }

        /// <summary>Oczekujace nadanie: kandydat z ksiegi wiosek; po FallbackDays bez kandydata - tytul BK jak dotad (to samo wywolanie z obejsciem prefiksu).</summary>
        private static void TryPending()
        {
            if (_pendKingdom == null) return;
            try
            {
                var k = Kingdom.All != null ? FindKingdom(_pendKingdom) : null;
                string why;
                bool at = LogGate.Mode == 1 || LogGate.Mode == 2;     // autotest CrashScribe: bez okna (nikt go nie kliknie) i bez wioski w wojnie z graczem
                if (k != null && Grant(k, _pendHow ?? "start", at, out why)) { _pendKingdom = null; return; }
                if (k == null) why = "krolestwo " + _pendKingdom + " nie istnieje";
                else why = "brak kandydata";
                if (Today() - _pendDay < FallbackDays) return;
                string kid = _pendKingdom;
                _pendKingdom = null;
                if (k != null && _giveLordship != null)
                {
                    Log.Info(P + "brak wioski pobocznej w krolestwie " + kid + " przez " + FallbackDays + " doby (" + why + ") - tytul BK jak dotad (GiveLordshipOnKingdomJoin z obejsciem).");
                    InvokeBk(k, true);
                }
                else Log.Info(P + "nadanie przepadlo (" + why + ").");
            }
            catch (Exception e) { _pendKingdom = null; Stumble("TryPending", e); }
        }

        private static Kingdom FindKingdom(string id)
        {
            foreach (var k in Kingdom.All) if (k != null && k.StringId == id) return k;
            return null;
        }

        private static bool InvokeBk(Kingdom k, bool bypass)
        {
            try
            {
                var cfgT = AccessTools.TypeByName("BannerKings.BannerKingsConfig");
                var inst = cfgT != null ? AccessTools.Property(cfgT, "Instance") : null;
                object cfg = inst != null ? inst.GetValue(null, null) : null;
                object tm = null;
                if (cfg != null)
                {
                    var f = AccessTools.Field(cfgT, "TitleManager");
                    if (f != null) tm = f.GetValue(cfg);
                    else { var p = AccessTools.Property(cfgT, "TitleManager"); if (p != null) tm = p.GetValue(cfg, null); }
                }
                if (tm == null || _giveLordship == null) return false;
                _bypass = bypass;                                  // obejscie prefiksu tylko dla zapasu "tytul BK jak dotad"; proba autotestu idzie przez prefiks
                _giveLordship.Invoke(tm, new object[] { k, Clan.PlayerClan, true });
                return true;
            }
            catch (Exception e) { Stumble("InvokeBk", e); return false; }
            finally { _bypass = false; }
        }

        /// <summary>Wybor i nadanie: wioska poboczna (nie przysiolek; w drugiej kolejce takze przysiolek i wioska uszkodzona) w okregu wsi gry, ktorej pan (Bound.OwnerClan)
        /// nie jest graczem i nalezy do krolestwa k - najblizej partii gracza. test: bez wojny z graczem.</summary>
        private static bool Grant(Kingdom k, string how, bool test, out string why)
        {
            why = "";
            if (!VillageBook.Active) { why = "ksiega wiosek nieczynna"; return false; }
            var recs = VillageBook.LayoutRecords();
            if (recs == null || recs.Count == 0) { why = "brak ukladu wiosek"; return false; }
            var mp = MobileParty.MainParty;
            if (mp == null) { why = "brak partii gracza"; return false; }
            Vec2 pos = mp.GetPosition2D;
            MapVillageRecord best = null; Settlement bestSt = null; float bestD = float.MaxValue;
            int seen = 0;
            // recenzja (drobne): w nowej kampanii n okregu liczy sie leniwie (EnsureN), a StateOf zwraca false dla okregu bez n - lista kandydatow przy "Finish" bywala
            // niepelna; VillageBook.PartsOf wola EnsureN (raz na okreg, wynik w slowniku tego wyboru)
            var parts = new Dictionary<string, List<VillageBook.PartView>>(StringComparer.Ordinal);
            for (int pass = 0; pass < 2 && best == null; pass++)
            {
                foreach (var r in recs)
                {
                    if (r == null || string.IsNullOrEmpty(r.Uid) || string.IsNullOrEmpty(r.VillageId)) continue;
                    if (pass == 0 && !string.IsNullOrEmpty(r.Hamlet)) continue;     // przysiolek na pustkowiu - dopiero w drugiej kolejce
                    if (IsMine(r.Uid)) continue;                                    // m113-zakup: juz gracza
                    var st = Settlement.Find(r.VillageId);
                    if (st == null || !st.IsVillage || st.Village == null) continue;
                    var bound = st.Village.Bound;
                    var lord = bound != null ? bound.OwnerClan : null;
                    if (lord == null || ReferenceEquals(lord, Clan.PlayerClan) || lord.IsEliminated) continue;
                    if (!ReferenceEquals(lord.Kingdom, k) && !ReferenceEquals(st.MapFaction, k)) continue;
                    if (test && Clan.PlayerClan.MapFaction != null && st.MapFaction != null && FactionManager.IsAtWarAgainstFaction(st.MapFaction, Clan.PlayerClan.MapFaction)) continue;
                    int state;
                    if (!PartState(st.Village, r.VillageId, r.Uid, parts, out state)) continue;
                    if (state != VillageBook.SStanding && (pass == 0 || state != VillageBook.SDamaged)) continue;
                    seen++;
                    float dx = r.X - pos.x, dy = r.Y - pos.y, d = dx * dx + dy * dy;
                    if (d < bestD) { bestD = d; best = r; bestSt = st; }
                }
            }
            if (best == null) { why = "brak wioski stojacej w okregach krolestwa " + k.StringId + " (okregow z wioskami sprawdzonych " + seen + ")"; return false; }
            var o = new Own { Uid = best.Uid, Dist = best.VillageId, Name = string.IsNullOrEmpty(best.Name) ? best.Uid : best.Name, Day0 = Today(), Px = best.X, Py = best.Y, PosSet = true };
            if (HasFief) _extra.Insert(0, _p);   // nie powinno sie zdarzyc (prefiks pomija nadanie przy wiosce) - stara pierwsza nie ginie
            _p = o;
            RebuildMine();
            var lordC = bestSt.Village.Bound != null ? bestSt.Village.Bound.OwnerClan : null;
            Log.Info(P + "NADANIE (" + how + ") - " + o.Name + " (" + o.Uid + "), okreg " + Txt(bestSt.Name) + " (" + o.Dist + "), pan " + (lordC != null ? Txt(lordC.Name) : "?")
                     + ", krolestwo " + k.StringId + ", " + Math.Sqrt(bestD).ToString("0.0", Inv) + " jedn. od partii gracza, kandydatow " + seen + "; wies gry zostaje przy panu.");
            if (!test)
            {
                string house = lordC != null ? MapVillagesView.HouseName(lordC) : "your liege";
                string body = "You hold " + o.Name + ", a side village in the lands of " + Txt(bestSt.Name) + ", sworn to " + house + ". " + Txt(bestSt.Name)
                              + " itself stays with its lord. Your village flies your banner, and every day its share of the lord's tax comes to you. "
                              + "Click it on the map to ride in: rest there, invest in its works or look it over.";
                Log.Player(o.Name + " is yours now - a side village of " + Txt(bestSt.Name) + ".");
                // okno jak zawiadomienie BK o nadaniu (BK pokazuje je w tym samym miejscu bez sprawdzania innych okien)
                try { InformationManager.ShowInquiry(new InquiryData("Your village", body, true, false, "Done", null, null, null), true); } catch (Exception e) { Stumble("Grant.Okno", e); }
            }
            return true;
        }

        /// <summary>Stan wioski pobocznej uid w okregu wsi gry v (VillageBook.PartsOf - z EnsureN; jeden odczyt na okreg w slowniku cache). false - ksiega jej nie zna.</summary>
        private static bool PartState(Village v, string dist, string uid, Dictionary<string, List<VillageBook.PartView>> cache, out int state)
        {
            state = -1;
            List<VillageBook.PartView> pv;
            if (!cache.TryGetValue(dist, out pv))
            {
                pv = new List<VillageBook.PartView>();
                if (!VillageBook.PartsOf(v, pv)) pv.Clear();
                cache[dist] = pv;
            }
            foreach (var p in pv) if (p != null && !p.Main && string.Equals(p.Uid, uid, StringComparison.Ordinal)) { state = p.State; return true; }
            return false;
        }

        private static string Txt(TextObject t) { try { return t != null ? t.ToString() : "?"; } catch { return "?"; } }

        // ============================================================ W3: wozy okregu (po r4-wioska tylko licznik - dochod idzie co dobe, Pay)
        /// <summary>VillageTakings.EnterPostfix (woz wrocil do wsi macierzystej; po 112, F4b i pracach okregu): r4-wioska - BEZ przelewu (dochod plynie co dobe z Pay,
        /// inaczej podwojnie), tylko licznik wozow okregu i podatku pana z ich okien do linii doby. Zwraca 0 (DsDues: udzial gracza przy wozie = 0).
        /// m113-zakup: okreg KAZDEJ wioski gracza.</summary>
        internal static long AtCart(MobileParty cart, Village home, long taxInWindow)
        {
            if (!HasFief || home == null || home.Settlement == null) return 0;
            try
            {
                string id = home.Settlement.StringId;
                bool any = false;
                foreach (var o in Owned()) if (string.Equals(id, o.Dist, StringComparison.Ordinal)) { any = true; break; }
                if (!any) return 0;
                _dCarts++;
                if (taxInWindow > 0) _dTax += taxInWindow;
            }
            catch (Exception e) { Stumble("AtCart", e); }
            return 0;
        }

        // ============================================================ m113-zakup: czysta naleznosc wioski (DueFor) - takze cena ziemi
        private sealed class DueC { public int Day, Code; public double Val, All, Share, Hand, Rate; public long Due; public string Note = "-", Why = "-"; }
        private static readonly Dictionary<string, DueC> _dueFor = new Dictionary<string, DueC>(StringComparer.Ordinal);
        internal static int DueForDistricts;   // ile okregow policzono (PartsOf) od ostatniego zerowania - koszt linii na sucho LandSale

        /// <summary>m113-zakup: NALEZNOSC DNIA wioski pobocznej uid okregu v BEZ bramek gracza (klucz, wojna, rabunek, wlasciciel): wartosc dnia jej produkcji
        /// (VillageCrafts.PartsOf; zapas - udzial ludzi w domu x wartosc okregu) x H okregu (DistrictWorks.HandedRatio) x stawka pana z utargu (LordRate). Pamiec na uid
        /// i dobe; jeden PartsOf liczy wszystkie wioski okregu. fresh - od nowa. whyPl - rachunek albo powod po polsku. Watek glowny, nie rzuca.</summary>
        internal static double DueFor(Village v, string uid, out string whyPl, bool fresh = false)
        {
            whyPl = "-";
            DueC c = DueCOf(v, uid, fresh);
            if (c == null) { whyPl = "wioska nieznana ksiedze okregu"; return 0; }
            whyPl = c.Why;
            return c.Due;
        }

        private static DueC DueCOf(Village v, string uid, bool fresh)
        {
            if (v == null || uid == null) return null;
            int today = Today();
            DueC c;
            if (!fresh && _dueFor.TryGetValue(uid, out c) && c.Day == today) return c;
            FillDistrict(v, today);
            return _dueFor.TryGetValue(uid, out c) && c.Day == today ? c : null;
        }

        private static void FillDistrict(Village v, int today)
        {
            try
            {
                DueForDistricts++;
                var parts = new List<VillageCrafts.PartCraft>();
                bool ks;
                if (!VillageCrafts.PartsOf(v, parts, out ks)) return;
                double all = 0, hAll = 0;
                foreach (var p in parts)
                {
                    double val = Math.Max(0, p.ValDay), home = p.Book != null ? Math.Max(0, p.Book.Home) : 0;
                    if (double.IsNaN(val) || double.IsInfinity(val)) val = 0;
                    all += val; hAll += home;
                }
                double hand = DistrictWorks.HandedRatio(v, all);
                if (double.IsNaN(hand) || double.IsInfinity(hand) || hand <= 0) hand = 0.868;
                double rate; bool rateOk = LordRate(v, out rate);
                foreach (var p in parts)
                {
                    if (p == null || p.Main || string.IsNullOrEmpty(p.Uid)) continue;
                    double val = Math.Max(0, p.ValDay), home = p.Book != null ? Math.Max(0, p.Book.Home) : 0;
                    if (double.IsNaN(val) || double.IsInfinity(val)) val = 0;
                    var c = new DueC { Day = today, All = all, Hand = hand, Rate = rate };
                    if (all > 0 && val > 0) { c.Share = Math.Min(1.0, val / all); c.Note = "wartosc dnia " + val.ToString("0", Inv) + " z " + all.ToString("0", Inv) + (ks ? " (KS)" : ""); }
                    else if (hAll > 0 && home > 0) { c.Share = Math.Min(1.0, home / hAll); val = c.Share * all; c.Note = "ludzie w domu " + home.ToString("0", Inv) + " z " + hAll.ToString("0", Inv); }
                    else c.Note = "wioska bez produkcji i ludzi w ksiedze";
                    c.Val = val;
                    double d = val * hand * rate;
                    string calc = "wartosc wioski " + val.ToString("0", Inv) + " x H " + hand.ToString("0.00", Inv) + " x stawka pana " + (100 * rate).ToString("0", Inv) + "%";
                    if (!rateOk) { c.Code = 3; c.Why = "stawka pana nieznana (model podatku wsi)"; }
                    else if (!(val > 0)) { c.Code = 4; c.Why = "brak wartosci produkcji (" + c.Note + ")"; }
                    else if (!(rate > 0)) { c.Code = 5; c.Why = "stawka pana 0"; }
                    else if (!(d >= 0.5) || double.IsInfinity(d)) { c.Code = 6; c.Why = "za mala wartosc dnia (" + calc + ")"; }
                    else { c.Due = (long)Math.Round(Math.Min(d, 1e6)); c.Why = calc; }
                    _dueFor[p.Uid] = c;
                }
            }
            catch (Exception e) { Stumble("FillDistrict", e); }
        }

        /// <summary>m113-zakup F2: kto placi udzial wioski okregu v - posiadacz podatku okregu (klucz SideVillagePayerIsTaxHolder: BK GetActualOwner - tytul de iure wsi, gdy jego
        /// frakcja = frakcja wsi, inaczej glowa rodu pana zamku); bez klucza albo bez BK - glowa rodu pana zamku (jak dotad). null - brak. Nie rzuca.</summary>
        internal static Hero PayerOf(Village v)
        {
            try
            {
                if (v == null || v.Settlement == null) return null;
                var lord = v.Bound != null ? v.Bound.OwnerClan : null;
                Hero fall = lord != null ? lord.Leader : null;
                var s = Settings.Current;
                if (s == null || !s.SideVillagePayerIsTaxHolder) return fall;
                return LandBk.ActualOwner(v) ?? fall;
            }
            catch (Exception e) { Stumble("PayerOf", e); return null; }
        }

        // ============================================================ r4-wioska: dochod co dobe
        private static long _dFromTax, _dFromLord, _dShort;

        /// <summary>r4-wioska (recenzja): podpis bramek naleznosci (klucz, pan okregu, platnik, wojna, rabunek wsi gry) - zmiana w ciagu doby (np. wojna wypowiedziana) liczy Due
        /// od nowa, zamiast pokazywac rano policzone +N. Tanie (bez ksiegi). Nie rzuca.</summary>
        private static int DueSig(Own o)
        {
            if (o == null) return 0;
            try
            {
                var s = Settings.Current;
                int k = s != null && s.SideVillageFiefIncome ? 1 : 0;
                var st = Settlement.Find(o.Dist);
                var v = st != null ? st.Village : null;
                if (v == null) return k | 2;
                var lord = v.Bound != null ? v.Bound.OwnerClan : null;
                if (lord == null || ReferenceEquals(lord, Clan.PlayerClan)) k |= 4;
                IFaction mf = Clan.PlayerClan != null ? Clan.PlayerClan.MapFaction : null, df = st.MapFaction;
                if (mf != null && df != null && !ReferenceEquals(mf, df) && FactionManager.IsAtWarAgainstFaction(mf, df)) k |= 8;
                if (v.VillageState == Village.VillageStates.BeingRaided || v.VillageState == Village.VillageStates.Looted) k |= 16;
                if (lord != null) k ^= System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(lord) << 5;
                var payer = PayerOf(v);
                if (payer != null) k ^= System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(payer) << 3;
                return k;
            }
            catch { return -1; }
        }

        /// <summary>r4-wioska (recenzja): stawka pana z utargu wsi okregu - czynny model podatku wsi (CalculateVillageTaxFromIncome, ta sama regula co BK, 112 i DS:
        /// 1 - DsVillage.VillageShareOfTakings), liczona tu we wlasnym try: wyjatek modelu = stawka NIEZNANA (false, dochod 0), a nie 100% (DsVillage przy wyjatku daje udzial wsi 0).</summary>
        private static bool LordRate(Village v, out double rate)
        {
            rate = 0;
            try
            {
                const int probe = 10000;
                var m = Campaign.Current != null ? Campaign.Current.Models.SettlementTaxModel : null;
                if (m == null) return false;
                double r = m.CalculateVillageTaxFromIncome(v, probe) / (double)probe;
                if (double.IsNaN(r) || double.IsInfinity(r)) return false;
                rate = r < 0 ? 0 : (r > 1 ? 1 : r);
                return true;
            }
            catch (Exception e) { rate = 0; Stumble("LordRate", e); return false; }
        }

        /// <summary>r4-wioska: naleznosc dnia wioski gracza - to, co z niej nalezy sie panu okregu wedlug naszej ekonomii (m113-zakup: DueFor - wartosc dnia x H x stawka pana).
        /// 0 tylko w uczciwych przypadkach: klucz wylaczony, gracz panem okregu, gracz (albo jego rod) posiadaczem podatku okregu (F3), wojna z krolestwem okregu, wies gry okregu
        /// rabowana / spladrowana (gra tez nie daje wtedy panu podatku), wioska gracza spalona, pan nie bierze podatku, wioska wytwarza dzis za malo, stawka nieznana - powod
        /// po angielsku w whyEn (Fiefs, menu), po polsku w o.DueWhyPl (log). Raz na dobe (fresh - od nowa; zmiana podpisu bramek DueSig - od nowa). Watek glowny.</summary>
        internal static long Due(Own o, out string whyEn, bool fresh = false)
        {
            whyEn = "";
            if (o == null) { whyEn = "This village is not yours."; return 0; }
            int today = Today(), sig = DueSig(o);
            if (!fresh && o.DueDay == today && o.DueSigV == sig) { whyEn = o.DueWhy; return o.Due; }
            o.DueDay = today; o.DueSigV = sig; o.Due = 0; o.DueCode = 0; o.DueWhy = ""; o.DueWhyPl = "-";
            try
            {
                var s = Settings.Current;
                if (s == null || !s.SideVillageFiefIncome) { o.DueWhy = "Its income is switched off in mod settings."; o.DueWhyPl = "klucz dochodu WYL"; }
                else
                {
                    var st = Settlement.Find(o.Dist);
                    var v = st != null ? st.Village : null;
                    var lord = v != null && v.Bound != null ? v.Bound.OwnerClan : null;
                    IFaction mf = Clan.PlayerClan != null ? Clan.PlayerClan.MapFaction : null, df = st != null ? st.MapFaction : null;
                    var payer = v != null ? PayerOf(v) : null;
                    VillageBook.VState vs;
                    if (v == null) { o.DueWhy = "Its district is not known."; o.DueWhyPl = "okreg nieznany"; }
                    else if (lord == null || ReferenceEquals(lord, Clan.PlayerClan)) { o.DueCode = 1; o.DueWhy = "You hold the district yourself - its whole tax is yours already."; o.DueWhyPl = "gracz panem okregu"; }
                    else if (payer != null && (ReferenceEquals(payer, Hero.MainHero) || ReferenceEquals(payer.Clan, Clan.PlayerClan)))
                    { o.DueCode = 1; o.DueWhy = "You hold the lordship of " + Txt(st.Name) + " - its whole tax is yours already."; o.DueWhyPl = "gracz posiadaczem podatku okregu (F3)"; }
                    else if (mf != null && df != null && !ReferenceEquals(mf, df) && FactionManager.IsAtWarAgainstFaction(mf, df)) { o.DueCode = 2; o.DueWhy = "Nothing while you are at war with its lord's realm."; o.DueWhyPl = "wojna z krolestwem okregu"; }
                    else if (v.VillageState == Village.VillageStates.BeingRaided || v.VillageState == Village.VillageStates.Looted) { o.DueWhy = "Nothing while " + Txt(st.Name) + " lies raided - the lord takes no tax from it either."; o.DueWhyPl = "wies gry okregu rabowana / spladrowana"; }
                    else if (VillageBook.StateOf(o.Uid, out vs) && (vs.State == VillageBook.SBurned || vs.State == VillageBook.SBurning || vs.State == VillageBook.SAbandoned)) { o.DueWhy = "Nothing while your village lies burned."; o.DueWhyPl = "wioska gracza spalona"; }
                    else
                    {
                        var c = DueCOf(v, o.Uid, fresh);
                        if (c == null) { o.DueCode = 3; o.DueWhy = "What your village makes is not known yet."; o.DueWhyPl = "wioska nieznana ksiedze okregu"; }
                        else
                        {
                            o.ShareDay = today; o.Share = c.Share; o.ShareNote = c.Note;
                            o.DueWhyPl = c.Why;
                            if (c.Code == 0) o.Due = c.Due;
                            else
                            {
                                o.DueCode = 3;
                                o.DueWhy = c.Code == 3 ? "Its lord's tax is not known today." : c.Code == 4 ? "What your village makes is not known yet."
                                         : c.Code == 5 ? "The lord takes no tax from this district." : "Your village makes too little today to owe the lord anything.";
                            }
                        }
                    }
                }
            }
            catch (Exception e) { o.Due = 0; o.DueWhy = "Not known today."; o.DueWhyPl = "blad"; Stumble("Due", e); }
            whyEn = o.DueWhy;
            return o.Due;
        }

        /// <summary>Stara droga (DistrictWorks, test): naleznosc PIERWSZEJ wioski.</summary>
        internal static long Due(out string whyEn, bool fresh = false) { return Due(_p, out whyEn, fresh); }

        // m113-zakup: pierscienie 28 dob (sesja) - wyplaty graczowi (F4) i to, co platnicy dali z kiesy (F6)
        private sealed class R28
        {
            public readonly long[] V = new long[28]; public int Head, Filled;
            public void Push(long v) { V[Head] = v; Head = (Head + 1) % 28; if (Filled < 28) Filled++; }
            public double Avg() { if (Filled <= 0) return 0; long s = 0; for (int i = 0; i < 28; i++) s += V[i]; return (double)s / Filled; }
        }
        private static R28 _paidRing = new R28();
        private static readonly Dictionary<Clan, R28> _drawn = new Dictionary<Clan, R28>();
        private static readonly Dictionary<Clan, long> _drawnToday = new Dictionary<Clan, long>();

        /// <summary>m113-zakup F4 (klucz SideVillageIncomeSeizable): srednia 28 dob (sesja) wyplat wszystkich wiosek gracza - czesc dochodu wsi rodu gracza. Nie rzuca.</summary>
        internal static double PaidAvg28()
        {
            var s = Settings.Current;
            if (s == null || !s.SideVillageIncomeSeizable) return 0;
            try { return _paidRing.Avg(); } catch { return 0; }
        }

        /// <summary>m113-zakup F6 (klucz SideVillageDuesInLordD): srednia 28 dob (sesja) tego, co rod c dal graczowi za jego wioski Z KIESY (nie z licznika podatku wsi). Nie rzuca.</summary>
        internal static double DrawnAvg28(Clan c)
        {
            var s = Settings.Current;
            if (c == null || s == null || !s.SideVillageDuesInLordD) return 0;
            try { R28 r; return _drawn.TryGetValue(c, out r) ? r.Avg() : 0; } catch { return 0; }
        }

        /// <summary>r4-wioska (OnDailyTick, SideVillageFief.Daily): wyplata naleznosci dnia graczowi - z licznika podatku wsi gry okregu (TradeTaxAccumulated: podatek
        /// z utargow wozow okregu, ktorego pan jeszcze nie zebral), a reszta z kiesy PLATNIKA (m113-zakup F2: posiadacz podatku okregu). Recenzja: z BK pan zbiera licznik wsi
        /// codziennie w 80-100% (EconomyPatches, AddSettlementIncome -> BKTaxModel.CalculateVillageTaxFromIncome i pobranie), wiec zwykle placi glownie KIESA.
        /// Zamknieta ekonomia: platnik ma mniej dokladnie o tyle, ile dostaje gracz; brak u platnika - "brak" (nic z niczego; widok - Expect).
        /// m113-zakup: wszystkie wioski gracza; licznik wsi i kiesa platnika liczone raz (dwie wioski jednego platnika nie dostaja dwa razy tej samej kiesy).
        /// Bez podwojnego liczenia: przy wozie nie ma juz przelewu (AtCart), czynsz i daniny DS zostaja przy panie.</summary>
        private static void Pay()
        {
            _drawnToday.Clear();
            if (!HasFief) return;
            var me = Hero.MainHero;
            var taxLeft = new Dictionary<Village, long>();
            var purseLeft = new Dictionary<Hero, long>();
            foreach (var o in Owned())
            {
                try
                {
                    o.DPaid = 0;
                    string why;
                    long due = Due(o, out why, true);
                    if (o.DueCode == 1) _dSelf++; else if (o.DueCode == 2) _dWar++; else if (o.DueCode == 3) _dNoShare++;   // liczniki linii doby - raz na wyplate
                    if (due <= 0) continue;
                    var st = Settlement.Find(o.Dist);
                    var v = st != null ? st.Village : null;
                    if (me == null || !me.IsAlive || v == null) continue;
                    var payer = PayerOf(v);
                    long t;
                    if (!taxLeft.TryGetValue(v, out t)) t = Math.Max(0, (long)v.TradeTaxAccumulated);
                    long fromTax = Math.Min(due, t);
                    taxLeft[v] = t - fromTax;
                    long fromLord = 0;
                    if (due - fromTax > 0 && payer != null && payer.IsAlive && !ReferenceEquals(payer, me))
                    {
                        long g;
                        if (!purseLeft.TryGetValue(payer, out g)) g = Math.Max(0, (long)payer.Gold);
                        fromLord = Math.Min(due - fromTax, g);
                        purseLeft[payer] = g - fromLord;
                    }
                    if (fromTax > 0) v.TradeTaxAccumulated -= (int)fromTax;          // licznik wsi -> gracz (jak wyplata prac okregu)
                    if (fromLord > 0) payer.ChangeHeroGold(-(int)fromLord);          // kiesa platnika -> gracz
                    long paid = fromTax + fromLord;
                    if (paid > 0) me.ChangeHeroGold((int)paid);
                    if (fromLord > 0 && payer.Clan != null) { long x; _drawnToday.TryGetValue(payer.Clan, out x); _drawnToday[payer.Clan] = x + fromLord; }
                    o.DPaid = paid; o.Paid += paid;
                    _dPaid += paid; _dFromTax += fromTax; _dFromLord += fromLord; _dShort += due - paid;
                }
                catch (Exception e) { Stumble("Pay", e); }
            }
        }

        /// <summary>m113-zakup: pierscienie F4 / F6 po wyplacie doby (rody, ktore dzis nic nie daly - 0). Nie rzuca.</summary>
        private static void PushRings()
        {
            try
            {
                long all = 0;
                foreach (var o in Owned()) all += o.DPaid;
                _paidRing.Push(all);
                foreach (var kv in _drawnToday) if (kv.Key != null && !_drawn.ContainsKey(kv.Key)) _drawn[kv.Key] = new R28();
                var drop = new List<Clan>();
                foreach (var kv in _drawn)
                {
                    long x; _drawnToday.TryGetValue(kv.Key, out x);
                    kv.Value.Push(x);
                    if (x == 0 && kv.Value.Avg() <= 0) drop.Add(kv.Key);
                }
                foreach (var c in drop) _drawn.Remove(c);
            }
            catch (Exception e) { Stumble("PushRings", e); }
        }

        /// <summary>m113-zakup: widok dochodu dnia kazdej wioski (podpowiedz zlota, Fiefs, menu) - naleznosc Due obcieta do tego, co zostaje u platnika po wioskach wyzej na liscie
        /// (licznik wsi + kiesa platnika liczone raz) - ekran nie obiecuje +N, gdy platnik nie ma z czego. Pieniadze plyna tylko z Pay. Nie rzuca.</summary>
        internal static void ExpectEach(List<KeyValuePair<Own, long>> into)
        {
            if (into == null) return;
            into.Clear();
            if (!HasFief) return;
            var taxLeft = new Dictionary<Village, long>();
            var purseLeft = new Dictionary<Hero, long>();
            foreach (var o in Owned())
            {
                long got = 0;
                try
                {
                    string why;
                    long due = Due(o, out why);
                    if (due > 0)
                    {
                        var st = Settlement.Find(o.Dist);
                        var v = st != null ? st.Village : null;
                        if (v != null)
                        {
                            long t;
                            if (!taxLeft.TryGetValue(v, out t)) t = Math.Max(0, (long)v.TradeTaxAccumulated);
                            long a = Math.Min(due, t); taxLeft[v] = t - a;
                            long b = 0;
                            var payer = PayerOf(v);
                            if (due - a > 0 && payer != null && payer.IsAlive && !ReferenceEquals(payer, Hero.MainHero))
                            {
                                long g;
                                if (!purseLeft.TryGetValue(payer, out g)) g = Math.Max(0, (long)payer.Gold);
                                b = Math.Min(due - a, g); purseLeft[payer] = g - b;
                            }
                            got = a + b;
                        }
                    }
                }
                catch (Exception e) { Stumble("ExpectEach", e); got = 0; }
                into.Add(new KeyValuePair<Own, long>(o, got));
            }
        }

        /// <summary>r4-wioska (recenzja): widok dochodu dnia jednej wioski - naleznosc Due obcieta do tego, co platnik moze dac teraz (ExpectEach), z powodem po angielsku,
        /// gdy obcieta.</summary>
        internal static long Expect(Own o, out string whyEn, out long due)
        {
            due = Due(o, out whyEn);
            if (due <= 0 || o == null) return 0;
            var l = new List<KeyValuePair<Own, long>>();
            ExpectEach(l);
            long have = due;
            foreach (var kv in l) if (ReferenceEquals(kv.Key, o)) { have = kv.Value; break; }
            if (have >= due) return due;
            whyEn = have > 0 ? "Its lord has only " + have.ToString("N0", Inv) + " to give today." : "Its lord has no money to pay it today.";
            return Math.Max(0, have);
        }

        /// <summary>r4-wioska: zakladka Fiefs ekranu rodu (SideVillageFiefUi) - naglowek, linie wiosek i dochod dnia. false - brak wioski gracza. Watek glowny, nie rzuca.
        /// m113-zakup: "Side villages (N)", wiersz na kazda wioske.</summary>
        internal static bool ClanTab(out string header, out string line, out string income)
        {
            header = line = income = "";
            try
            {
                if (!HasFief) return false;
                var own = Owned();
                var l = new List<KeyValuePair<Own, long>>();
                ExpectEach(l);
                header = "Side villages (" + own.Count.ToString(Inv) + ")";
                var sb = new StringBuilder();
                long sum = 0;
                foreach (var kv in l)
                {
                    var o = kv.Key;
                    var st = Settlement.Find(o.Dist);
                    var lord = st != null && st.Village != null && st.Village.Bound != null ? st.Village.Bound.OwnerClan : null;
                    if (sb.Length > 0) sb.Append('\n');
                    sb.Append(o.Name).Append(" - lands of ").Append(st != null ? Txt(st.Name) : o.Dist);
                    if (lord != null && !ReferenceEquals(lord, Clan.PlayerClan)) sb.Append(", sworn to ").Append(MapVillagesView.HouseName(lord));
                    string why; long due = Due(o, out why);
                    if (own.Count > 1) sb.Append(kv.Value > 0 ? ": about " + kv.Value.ToString("N0", Inv) + " a day" : ": nothing today (" + (why.Length > 0 && kv.Value <= 0 && due <= 0 ? why.TrimEnd('.') : "the lord has no more to give") + ")");
                    sum += kv.Value;
                }
                line = sb.ToString();
                if (own.Count == 1)
                {
                    string why; long due;
                    long d = Expect(own[0], out why, out due);
                    income = d <= 0 ? "Income: none today. " + why
                           : d < due ? "Income: about " + d.ToString("N0", Inv) + " today (its share of the lord's tax is " + due.ToString("N0", Inv) + ", but the lord has no more to give)"
                           : "Income: about " + d.ToString("N0", Inv) + " a day (its share of the lord's tax)";
                }
                else income = sum > 0 ? "Income: about " + sum.ToString("N0", Inv) + " a day in all (each village's share of its lord's tax)" : "Income: none today.";
                return true;
            }
            catch (Exception e) { Stumble("ClanTab", e); return false; }
        }

        /// <summary>r4-wioska: tekst prawej czesci zakladki Fiefs, gdy rod nie ma miast ani zamkow (zamiast "Clan Does Not Own Any Fiefs"). null - bez zmian.</summary>
        internal static string NoFiefsText()
        {
            string h, l, i;
            if (!ClanTab(out h, out l, out i)) return null;
            if (OwnedCount > 1) return "Your clan holds no towns or castles.\n\n" + h + ":\n" + l + "\n" + i.TrimEnd('.', ' ') + ".";
            return "Your clan holds no towns or castles.\n\nYour side village: " + l + ".\n" + i.TrimEnd('.', ' ') + ".";   // recenzja: powody koncza sie kropka - bez dwoch kropek
        }

        internal static void UiStumble(string where, Exception e) { Stumble("Ui." + where, e); }

        // ============================================================ W3: linia w podpowiedzi zlota
        /// <summary>Postfiks CalculateClanGoldChange czynnego modelu: rod gracza, z opisami i BEZ pobrania (podpowiedz zlota) - r4-wioska: naleznosc dnia (Due - wyplata
        /// w najblizszym OnDailyTick), nie srednia 7 dob (na starcie 0). Tylko widok: pieniadze ida z Pay, nie z modelu finansow (bez podwojnej wyplaty).
        /// m113-zakup: linia na kazda wioske gracza.</summary>
        public static void GoldTipPostfix(object __instance, Clan __0, bool __1, bool __2, ref ExplainedNumber __result)
        {
            try
            {
                if (!__1 || __2 || __0 == null || !HasFief || !ReferenceEquals(__0, Clan.PlayerClan)) return;
                var s = Settings.Current;
                if (s == null || !s.SideVillageFiefIncome || Campaign.Current == null || !ReferenceEquals(__instance, Campaign.Current.Models.ClanFinanceModel)) return;
                _dTip += AddLines(ref __result);   // recenzja: obciete do tego, co platnik ma teraz (bez obietnicy +N, gdy nie ma z czego)
            }
            catch (Exception e) { Stumble("GoldTipPostfix", e); }
        }

        // ============================================================ W4: gospodarz na mapie
        /// <summary>VillageHolders.Of: gracz gospodarzem swojej wioski (herb rodu gracza, pan okregu "sworn to"). null - nie wioska gracza albo brak herbu.</summary>
        internal static VillageHolders.Holder HolderOf(Settlement district, string uid)
        {
            if (!IsMine(uid) || district == null || district.Village == null) return null;
            var pc = Clan.PlayerClan;
            if (pc == null || pc.Banner == null || string.IsNullOrEmpty(pc.Banner.BannerCode)) return null;
            var bound = district.Village.Bound;
            var lord = bound != null ? bound.OwnerClan : null;
            HolderC c;
            if (_holders.TryGetValue(uid, out c) && c.H != null && ReferenceEquals(c.Lord, lord) && ReferenceEquals(c.B, pc.Banner) && ReferenceEquals(c.H.Hero, Hero.MainHero)) return c.H;
            var h = new VillageHolders.Holder { Rule = VillageHolders.RulePlayer, Hero = Hero.MainHero, Clan = pc, Banner = pc.Banner, Lord = lord };
            _holders[uid] = new HolderC { H = h, Lord = lord, B = pc.Banner };
            return h;
        }

        /// <summary>Linia dymku / okna okregu dla wioski gracza.</summary>
        internal static string TipLine(Clan lord)
        {
            if (lord == null || ReferenceEquals(lord, Clan.PlayerClan)) return "This village is yours.";
            return "This village is yours, sworn to " + MapVillagesView.HouseName(lord) + ".";
        }

        /// <summary>Herb gracza na proporczyku przy wylaczonym VillageBannerPerHolder (kod herbu rodu gracza albo null).</summary>
        internal static string BannerCodeOf(string uid)
        {
            if (!IsMine(uid)) return null;
            var pc = Clan.PlayerClan;
            return pc != null && pc.Banner != null && !string.IsNullOrEmpty(pc.Banner.BannerCode) ? pc.Banner.BannerCode : null;
        }

        // ============================================================ W5: wejscie z mapy
        /// <summary>MapVillagesView: klik na wioske gracza - jazda do niej (jak klik w teren), menu po dojechaniu (Tick). true = klik obsluzony.</summary>
        internal static bool OnClick(string uid, float x, float y)
        {
            try
            {
                if (!Clickable(uid)) return false;
                var mp = MobileParty.MainParty;
                if (mp == null || mp.MapEvent != null || mp.CurrentSettlement != null) return false;
                if (mp.Army != null && mp.Army.LeaderParty != mp) return false;
                var ms = Game.Current != null ? Game.Current.GameStateManager.ActiveState as TaleWorlds.CampaignSystem.GameState.MapState : null;
                if (ms == null) return false;
                var o = Find(uid);
                if (o == null) return false;
                o.Px = x; o.Py = y; o.PosSet = true;
                ms.ProcessTravel(new CampaignVec2(new Vec2(x, y), true));
                _pendingClick = uid; _askedUid = null;
                _dClick++;
                return true;
            }
            catch (Exception e) { Stumble("OnClick", e); return false; }
        }

        /// <summary>MapVillagesView: polozenie obrazka wioski gracza po przesunieciu (najechanie) - punkt postoju.</summary>
        internal static void NotePos(string uid, float x, float y) { var o = Find(uid); if (o != null) { o.Px = x; o.Py = y; o.PosSet = true; } }

        private static float DistTo(Own o, Vec2 pos)
        {
            if (o == null) return float.MaxValue;
            if (!o.PosSet) PosFromBook();
            return o.PosSet ? pos.Distance(new Vec2(o.Px, o.Py)) : float.MaxValue;
        }

        /// <summary>CampaignEvents.TickEvent: 4 razy na sekunde (zegar rzeczywisty) - partia gracza przy swojej wiosce: po kliku menu, po postoju pytanie (raz na postoj).
        /// Oczekujace nadanie z ksiega wiosek nieczynna - HourlyTick. Koszt: jedno porownanie odleglosci na wioske gracza.</summary>
        internal static void Tick()
        {
            if (!HasFief) return;
            long tq = Stopwatch.GetTimestamp();
            if (tq - _lastCheck < Stopwatch.Frequency / 4) return;
            _lastCheck = tq;
            try
            {
                var s = Settings.Current;
                if (s == null || !s.SideVillageFiefMenu) return;
                var c = Campaign.Current;
                if (c == null || c.CurrentMenuContext != null) return;
                if (TaleWorlds.CampaignSystem.Encounters.PlayerEncounter.Current != null) return;
                if (PopupBlocked()) return;
                var mp = MobileParty.MainParty;
                if (mp == null || Hero.MainHero == null || !Hero.MainHero.IsAlive) return;
                if (mp.CurrentSettlement != null || mp.MapEvent != null || mp.IsCurrentlyAtSea || TaleWorlds.CampaignSystem.PlayerCaptivity.IsCaptive
                    || mp.SiegeEvent != null || mp.BesiegerCamp != null) { _pendingClick = null; return; }
                if (!_layoutChecked) CheckLayout();
                var pos = mp.GetPosition2D;
                if (_askedUid != null) { var a = Find(_askedUid); if (a == null || DistTo(a, pos) > LeaveR) _askedUid = null; }
                if (_pendingClick != null)
                {
                    var t = Find(_pendingClick);
                    float dt = DistTo(t, pos);
                    if (t == null) { _pendingClick = null; return; }
                    if (dt <= EnterR) { _pendingClick = null; _askedUid = t.Uid; _cur = t; Open("klik na wioske"); return; }
                    if (mp.TargetPosition.ToVec2().Distance(new Vec2(t.Px, t.Py)) > EnterR + 0.5f) _pendingClick = null;   // gracz pojechal gdzie indziej
                    return;
                }
                Own near = null; float nd = float.MaxValue;
                foreach (var o in Owned()) { float d = DistTo(o, pos); if (d < nd) { nd = d; near = o; } }
                if (near == null || nd > EnterR || _askedUid == near.Uid) return;
                if (pos.Distance(mp.TargetPosition.ToVec2()) > ArriveEps) return;    // jeszcze jedzie (mija wioske)
                if (mp.Army != null && mp.Army.LeaderParty != mp) return;
                _askedUid = near.Uid;
                _dAsk++;
                var pick = near;
                InformationManager.ShowInquiry(new InquiryData(near.Name, "You draw rein by " + near.Name + ", your village. Ride in?", true, true, "Ride in", "Ride on",
                    delegate { try { if (!ConversationOn()) { _cur = pick; Open("pytanie przy postoju"); } } catch (Exception e) { Stumble("Ask.Yes", e); } }, null), true);
            }
            catch (Exception e) { Stumble("Tick", e); }
        }

        /// <summary>TownInns.Host: odleglosc partii gracza od obrazka jego PIERWSZEJ wioski (float.MaxValue - brak wioski albo polozenia). Watek glowny.</summary>
        internal static float PlayerDist() { return PlayerDist(Uid); }

        /// <summary>m113-zakup: odleglosc partii gracza od obrazka wioski gracza uid (float.MaxValue - nie gracza albo brak polozenia). Watek glowny.</summary>
        internal static float PlayerDist(string uid)
        {
            var mp = MobileParty.MainParty;
            var o = Find(uid);
            if (o == null || mp == null) return float.MaxValue;
            return DistTo(o, mp.GetPosition2D);
        }

        private static void PosFromBook()
        {
            var recs = VillageBook.LayoutRecords();
            if (recs == null) return;
            foreach (var r in recs)
            {
                if (r == null || !IsMine(r.Uid)) continue;
                var o = Find(r.Uid);
                if (o != null && !o.PosSet) { o.Px = r.X; o.Py = r.Y; o.PosSet = true; }
            }
        }

        private static bool PopupBlocked()
        {
            if (LogGate.Mode == 1 || LogGate.Mode == 2) return true;   // autotest CrashScribe - nikt nie kliknie okna (jak zajazdy)
            if (InformationManager.IsAnyInquiryActive()) return true;
            if (ConversationOn()) return true;
            var ms = Game.Current != null && Game.Current.GameStateManager != null ? Game.Current.GameStateManager.ActiveState as TaleWorlds.CampaignSystem.GameState.MapState : null;
            return ms == null;
        }

        private static bool ConversationOn()
        {
            var cm = Campaign.Current != null ? Campaign.Current.ConversationManager : null;
            return cm != null && (cm.IsConversationInProgress || cm.IsConversationFlowActive);
        }

        private static void Open(string how)
        {
            var c = Campaign.Current;
            if (c == null || c.CurrentMenuContext != null) return;
            if (TaleWorlds.CampaignSystem.Encounters.PlayerEncounter.Current != null || ConversationOn()) return;
            var mp = MobileParty.MainParty;
            if (mp == null || mp.CurrentSettlement != null || mp.MapEvent != null) return;
            var o = CurOwn;
            if (o == null) return;
            _dOpen++;
            try { mp.SetMoveModeHold(); } catch { }
            GameMenu.ActivateGameMenu(Menu);
            Log.Info(P + "wjazd do " + o.Name + " (" + o.Uid + ")" + (ReferenceEquals(o, _p) ? "" : " [dodatkowa]") + " - " + how + ", partia " + DistTo(o, mp.GetPosition2D).ToString("0.00", Inv) + " jedn. od wioski.");
        }

        // ============================================================ W5: menu
        internal static void AddMenus(CampaignGameStarter starter)
        {
            try
            {
                starter.AddGameMenuOption("village", OptVillage, "{=!}{ARM_SIDE_FIEF_VISIT}", VisitCondition, VisitConsequence, false, 9);
                starter.AddGameMenu(Menu, "{=!}{ARM_SIDE_FIEF_TEXT}", delegate (MenuCallbackArgs a)
                    {
                        try { MBTextManager.SetTextVariable("ARM_SIDE_FIEF_TEXT", MenuText()); } catch (Exception e) { Stumble("MenuInit", e); }
                        // recenzja (drobne): menu otwarte z mapy (nowy MenuContext) mialo tlo domyslne gry ("wait_guards_stop") - tlo wsi gry okregu jak sen w zajezdzie
                        try
                        {
                            var d = MenuDistId;
                            var st = d != null ? Settlement.Find(d) : null;
                            string mesh = st != null && st.SettlementComponent != null ? st.SettlementComponent.WaitMeshName : null;
                            a.MenuContext.SetBackgroundMeshName(string.IsNullOrEmpty(mesh) ? "wait_fallback" : mesh);
                        }
                        catch (Exception e) { Stumble("MenuInit.Tlo", e); }
                    }, GameMenu.MenuOverlayType.None);
                starter.AddGameMenuOption(Menu, "arm_side_fief_rest", "{=!}Rest the night here",
                    delegate (MenuCallbackArgs a)
                    {
                        a.optionLeaveType = GameMenuOption.LeaveType.Wait;
                        if (!HasFief) return false;
                        var mp = MobileParty.MainParty;
                        if (NightRest.EnlistedNow) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}Your lord decides when the men sleep."); return true; }
                        if (mp != null && mp.Army != null && mp.Army.LeaderParty != mp) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}You march in another lord's army - he decides where the host sleeps."); return true; }
                        string why;
                        // recenzja (WAZNE): podpowiedz mowi prawde - pod dachem (stan noclegu karczmy, TownInns.Host) albo jak w obozie z powodem
                        // m110-sam: bez zolnierzy - podpowiedz o graczu (sam) albo o nim i towarzyszach
                        if (RoofOk(out why))
                            a.Tooltip = new TextObject("{=!}Your villagers take " + PartyWords.Men() + " in for the night - no charge. "
                                                       + PartyWords.Pick("They sleep on straw under roofs, by night until dawn, and wake rested if they sleep their fill: ",
                                                                         "You sleep on straw under a roof, by night until dawn, and wake rested if you sleep your fill: ",
                                                                         "You sleep on straw under roofs, by night until dawn, and wake rested if you sleep your fill: ")
                                                       + TownInns.HostBonusText() + " (like the common room of an inn).");
                        else a.Tooltip = new TextObject("{=!}" + PartyWords.MenCap() + " camp by the village for the night, as anywhere on the map: " + why);
                        return true;
                    },
                    delegate (MenuCallbackArgs a)
                    {
                        try
                        {
                            _dRest++;
                            var o = CurOwn;
                            if (o == null) return;
                            string why;
                            bool roof = RoofOk(out why) && TownInns.HostSleep(o.Dist, o.Uid, o.Name);
                            if (roof) _dRoof++;
                            Log.Info(P + "nocleg w " + o.Name + (roof ? " - pod dachem (stan noclegu jak wspolna izba karczmy, bez oplaty)" : " - jak w obozie (" + why + ")") + ".");
                            NightRest.BeginInnSleep();
                        }
                        catch (Exception e) { Stumble("Rest", e); }
                    }, false, 0);
                starter.AddGameMenuOption(Menu, "arm_side_fief_invest", "{=!}Invest in its works",
                    delegate (MenuCallbackArgs a)
                    {
                        a.optionLeaveType = GameMenuOption.LeaveType.Trade;
                        if (!HasFief) return false;
                        string why;
                        // recenzja (drobne): opcja martwa, gdy prac tej wioski nie da sie kupic (konie, gift villages Strazy, spalona, zmiana zajecia) - wylaczona z powodem
                        if (!DistrictWorks.On) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}District works are switched off (mod settings)."); }
                        else if (!InvestOk(out why)) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}" + why); }
                        else a.Tooltip = new TextObject("{=!}Pay your villagers for boats, herds, ploughs or tools. Your village makes more of its own goods, and half of what the extra sells for is yours.");
                        return true;
                    },
                    delegate (MenuCallbackArgs a)
                    {
                        try { _dInvest++; _invHour = -1; var o = CurOwn; var st = o != null ? Settlement.Find(o.Dist) : null; if (st != null) DistrictWorks.OpenPartFor(st, o.Uid); }   // po oknie prac wycena od nowa
                        catch (Exception e) { Stumble("Invest", e); }
                    }, false, 1);
                starter.AddGameMenuOption(Menu, "arm_side_fief_look", "{=!}Look over your village",
                    delegate (MenuCallbackArgs a) { a.optionLeaveType = GameMenuOption.LeaveType.Manage; return HasFief; },
                    delegate (MenuCallbackArgs a) { try { ShowLook(); } catch (Exception e) { Stumble("Look", e); } }, false, 2);
                starter.AddGameMenuOption(Menu, "arm_side_fief_leave", "{=!}Leave",
                    delegate (MenuCallbackArgs a) { a.optionLeaveType = GameMenuOption.LeaveType.Leave; return true; },
                    delegate (MenuCallbackArgs a)
                    {
                        try
                        {
                            var here = MobileParty.MainParty != null ? MobileParty.MainParty.CurrentSettlement : null;
                            if (here != null && here.IsVillage) GameMenu.SwitchToMenu("village");   // wejscie z menu wsi gry - powrot do niego (zwykle menu)
                            else GameMenu.ExitToLast();
                        }
                        catch (Exception e) { Stumble("Leave", e); }
                    }, true, 9);
                SideVillageHome.AddMenus(starter);   // r4-dom: "Trade", "Your house" (lozko, skrzynia), "Take a walk"
            }
            catch (Exception e) { Stumble("AddMenus", e); }
        }

        /// <summary>m113-zakup: wioski gracza w okregu wsi gry distId (czynne).</summary>
        private static List<Own> OwnedIn(string distId)
        {
            var l = new List<Own>();
            if (distId == null) return l;
            foreach (var o in Owned()) if (string.Equals(o.Dist, distId, StringComparison.Ordinal)) l.Add(o);
            return l;
        }

        private static bool VisitCondition(MenuCallbackArgs a)
        {
            try
            {
                a.optionLeaveType = GameMenuOption.LeaveType.Submenu;
                var s = Settings.Current;
                if (s == null || !s.SideVillageFiefMenu || !HasFief) return false;
                var st = Settlement.CurrentSettlement;
                if (st == null) return false;
                var l = OwnedIn(st.StringId);
                if (l.Count == 0) return false;
                if (l.Count == 1)
                {
                    MBTextManager.SetTextVariable("ARM_SIDE_FIEF_VISIT", "Visit " + l[0].Name + " (your village)");
                    a.Tooltip = new TextObject("{=!}Ride over to your side village: rest there, invest in its works or look it over.");
                }
                else
                {
                    MBTextManager.SetTextVariable("ARM_SIDE_FIEF_VISIT", "Visit one of your villages here (" + l.Count.ToString(Inv) + ")");
                    a.Tooltip = new TextObject("{=!}Several side villages of these lands are yours - choose which one to ride over to.");
                }
                return true;
            }
            catch { return false; }
        }

        private static void VisitConsequence(MenuCallbackArgs a)
        {
            try
            {
                var st = Settlement.CurrentSettlement;
                var l = OwnedIn(st != null ? st.StringId : null);
                if (l.Count == 0) return;
                if (l.Count == 1) { _cur = l[0]; _dOpen++; Log.Info(P + "wjazd do " + l[0].Name + " z menu wsi gry."); GameMenu.SwitchToMenu(Menu); return; }
                // m113-zakup: dwie i wiecej wiosek gracza w tym okregu - okno wyboru (menu "village" to zwykle menu, nie oczekiwania)
                var opts = new List<InquiryElement>();
                foreach (var o in l) opts.Add(new InquiryElement(o.Uid, o.Name, null, true, o.Name + (ReferenceEquals(o, _p) ? " - your first village, with your house" : "")));
                _dPick++;
                MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData("Your villages", "Which of your villages in these lands do you ride to?", opts, true, 1, 1, "Ride", "Stay",
                    delegate (List<InquiryElement> sel)
                    {
                        try
                        {
                            if (sel == null || sel.Count == 0) return;
                            var o = Find(sel[0].Identifier as string);
                            if (o == null) return;
                            _cur = o; _dOpen++;
                            Log.Info(P + "wjazd do " + o.Name + " z menu wsi gry (wybor z " + l.Count + " wiosek gracza okregu).");
                            GameMenu.SwitchToMenu(Menu);
                        }
                        catch (Exception e) { Stumble("Visit.Wybor", e); }
                    },
                    delegate (List<InquiryElement> _) { }), true);
            }
            catch (Exception e) { Stumble("Visit", e); }
        }

        /// <summary>Recenzja (WAZNE): nocleg pod dachem we wlasnej wiosce - klucz SideVillageFiefLodging, karczmy czynne (TownInns.On), partia u wioski, bez wojny z krolestwem
        /// okregu, wies gry nie rabowana teraz, wioska nie spalona. why - powod po angielsku (podpowiedz "jak w obozie: ..."). Watek glowny (warunek menu). m113-zakup: wioska menu.</summary>
        private static bool RoofOk(out string why)
        {
            why = null;
            var s = Settings.Current;
            if (s == null || !s.SideVillageFiefLodging) { why = "lodging in your village is switched off (mod settings)."; return false; }
            if (!TownInns.On) { why = "lodgings need Town Inns and Night Rest (mod settings)."; return false; }
            var o = CurOwn;
            if (o == null) { why = "this village is not yours."; return false; }
            var st = Settlement.Find(o.Dist);
            if (st == null || st.Village == null) { why = "the village is not known."; return false; }
            if (!TownInns.HostNearNow(o.Dist, o.Uid)) { why = "you are not at the village."; return false; }
            IFaction mf = Clan.PlayerClan != null ? Clan.PlayerClan.MapFaction : null, df = st.MapFaction;
            if (mf != null && df != null && !ReferenceEquals(mf, df) && FactionManager.IsAtWarAgainstFaction(mf, df)) { why = "while you are at war with its lord's realm, your villagers dare not take " + PartyWords.Pick("your men", "you", "you and your companions") + " in."; return false; }   // m110-sam: bez zolnierzy - o graczu
            if (st.Village.VillageState == Village.VillageStates.BeingRaided) { why = "raiders are on these lands and your villagers have fled."; return false; }
            VillageBook.VState vs;
            if (VillageBook.StateOf(o.Uid, out vs) && (vs.State == VillageBook.SBurned || vs.State == VillageBook.SBurning || vs.State == VillageBook.SAbandoned))
            { why = "your village is burned - there are no roofs left to sleep under."; return false; }
            return true;
        }

        /// <summary>Recenzja (drobne): prace tej wioski da sie kupic (DistrictWorks.QuotesOf: Allowed i rodzaj prac) - wycena raz na godzine gry i po oknie prac. why - powod gry.</summary>
        private static bool InvestOk(out string why)
        {
            int h = (int)CampaignTime.Now.ToHours;
            var o = CurOwn;
            string uid = o != null ? o.Uid : null;
            if (h != _invHour || !string.Equals(_invUid, uid, StringComparison.Ordinal))
            {
                _invHour = h; _invUid = uid; _invOk = false; _invWhy = "No works here.";
                try
                {
                    var st = o != null ? Settlement.Find(o.Dist) : null;
                    var qs = new List<DistrictWorks.Quote>();
                    if (st == null || st.Village == null || !DistrictWorks.QuotesOf(st.Village, qs, Clan.PlayerClan)) _invWhy = "The villages of this district are not known yet.";
                    else
                    {
                        DistrictWorks.Quote q = null;
                        foreach (var x in qs) if (x != null && string.Equals(x.Uid, uid, StringComparison.Ordinal)) { q = x; break; }
                        if (q != null && q.Allowed && q.WKind >= 0) { _invOk = true; _invWhy = null; }
                        else if (q != null && !string.IsNullOrEmpty(q.Why)) _invWhy = q.Why;
                    }
                }
                catch (Exception e) { _invOk = false; _invWhy = "No works here."; Stumble("InvestOk", e); }
            }
            why = _invWhy;
            return _invOk;
        }

        /// <summary>Tekst menu: wioska, okreg, pan, ludzie, zajecie, udzial w produkcji, dochod. m113-zakup: wioska menu (_cur).</summary>
        internal static string MenuText()
        {
            var o = CurOwn;
            if (o == null) return "This village is not yours.";
            var st = Settlement.Find(o.Dist);
            var sb = new StringBuilder(600);
            string dname = st != null ? Txt(st.Name) : o.Dist;
            var lord = st != null && st.Village != null && st.Village.Bound != null ? st.Village.Bound.OwnerClan : null;
            sb.Append(o.Name).Append(" is your village in the lands of ").Append(dname);
            if (lord != null && !ReferenceEquals(lord, Clan.PlayerClan)) sb.Append(", sworn to ").Append(MapVillagesView.HouseName(lord));
            sb.Append(".\n");
            VillageBook.VState vs;
            if (VillageBook.StateOf(o.Uid, out vs))
                sb.Append(Math.Round(vs.Home + vs.Away).ToString("N0", Inv)).Append(" people in ").Append(Math.Max(1.0, Math.Round(vs.Settlements)).ToString("0", Inv)).Append(" homesteads")
                  .Append(vs.State == VillageBook.SStanding ? "." : vs.State == VillageBook.SBurned ? " - burned, people are slowly coming back." : " - raiders have emptied part of it.").Append('\n');
            if (st != null) foreach (string l in DistrictWorks.PartLines(st, o.Uid, false)) sb.Append(l).Append('\n');
            string why; long due;
            long got = Expect(o, out why, out due);   // r4-wioska: dochod dnia (ten sam co w podpowiedzi zlota i w zakladce Fiefs; recenzja - obciety do tego, co pan ma)
            if (got > 0) sb.Append("Your share of the lord's tax: about ").Append(got.ToString("N0", Inv)).Append(got < due ? " today (the lord has no more to give)" : " a day");
            else sb.Append("Your share of the lord's tax: none today (").Append(why.TrimEnd('.')).Append(")");
            sb.Append(", ").Append(o.Paid.ToString("N0", Inv)).Append(" in all since day ").Append(o.Day0).Append('.');
            if (o.Price > 0) sb.Append("\nYou bought it for ").Append(o.Price.ToString("N0", Inv)).Append('.');
            if (!ReferenceEquals(o, _p) && HasFief) sb.Append("\nYour house is in ").Append(_p.Name).Append(", your first village.");
            return sb.ToString();
        }

        private static void ShowLook()
        {
            var o = CurOwn;
            if (o == null) return;
            string body = MenuText();
            var st = Settlement.Find(o.Dist);
            if (st != null)
            {
                // tylko linie dymku, ktorych nie ma juz w tekscie menu ("Kit in place")
                var shown = new HashSet<string>(DistrictWorks.PartLines(st, o.Uid, false), StringComparer.Ordinal);
                var more = new StringBuilder();
                foreach (string l in DistrictWorks.PartLines(st, o.Uid, true)) if (!shown.Contains(l)) more.Append('\n').Append(l);
                if (st.Village != null) { string w; Due(o, out w); }
                more.Append("\nHow it pays: the lord who takes the tax of ").Append(Txt(st.Name)).Append(" takes it from what the district sells at market. The part that comes from your village - its share of what the district makes, now about ")
                    .Append((100 * o.Share).ToString("0", Inv)).Append("% - is paid to you every day, out of the district's tax (or that lord's purse). At war with the lord's realm, or while the district lies raided or your village burned, you get nothing.");
                body += "\n" + more.ToString();
            }
            // przeglad calosci 20.10: ramka okna (SingleQueryPopup ROT) nie przewija - ok. 19 linii (Rumours.PageLines z zapasem). Przy wojnie, spaleniu i odbudowie
            // naraz tekst ma ok. 22 linie - wtedy strony po akapitach (jak plotki): "More" i "Done"
            var pages = new List<string>();
            var cur = new StringBuilder();
            int n = 0;
            foreach (string para in body.Split('\n'))
            {
                int need = Math.Max(1, Rumours.EstLines(para));
                if (cur.Length > 0 && n + need > Rumours.PageLines) { pages.Add(cur.ToString().TrimEnd('\n')); cur.Length = 0; n = 0; }
                if (cur.Length == 0 && para.Length == 0) continue;   // pusta linia na poczatku strony - zbedna
                if (cur.Length > 0) cur.Append('\n');
                cur.Append(para); n += need;
            }
            if (cur.Length > 0 || pages.Count == 0) pages.Add(cur.ToString());
            LookPage(pages, 0, o.Name);
        }

        private static void LookPage(List<string> pages, int i, string name)
        {
            bool more = i + 1 < pages.Count;
            string title = (name ?? "Your village") + (pages.Count > 1 ? " (" + (i + 1).ToString(Inv) + "/" + pages.Count.ToString(Inv) + ")" : "");
            Action next = null;
            if (more) { int k = i + 1; next = () => { try { LookPage(pages, k, name); } catch (Exception e) { Stumble("LookPage", e); } }; }
            InformationManager.ShowInquiry(new InquiryData(title, pages[i], true, more, more ? "More" : "Done", more ? "Done" : null, next, null), false);
        }

        // ============================================================ godzina, doba, test
        /// <summary>HourlyTick: oczekujace nadanie (start BK przed czynna ksiega wiosek), furtka autotestu.</summary>
        internal static void Hourly()
        {
            try
            {
                if (_pendKingdom != null) TryPending();
                if (!_layoutChecked) CheckLayout();
                if (!_testDone && TestWanted()) Test();
                SideVillageHome.Hourly();   // r4-dom: samotest domu, handlu i spaceru (ta sama flaga)
            }
            catch (Exception e) { Stumble("Hourly", e); }
        }

        private static bool TestWanted()
        {
            int m = LogGate.Mode;
            if (m != 1 && m != 2) return false;
            if (!VillageBook.Active) return false;
            try
            {
                var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string p = System.IO.Path.Combine(System.IO.Path.Combine(System.IO.Path.Combine(docs, "Mount and Blade II Bannerlord"), "CrashScribe"), AutoFlagName);
                return System.IO.File.Exists(p);
            }
            catch { return false; }
        }

        /// <summary>Proba autotestu (raz na sesje): ta sama metoda BK co start (odbiciem), prefiks, nadanie, gospodarz i herb, zapis tam i z powrotem, tekst menu.</summary>
        private static void Test()
        {
            _testDone = true;
            var sb = new StringBuilder(800);
            int pass = 0, fail = 0;
            Action<string, bool, string> add = (n, ok, t) => { sb.Append(" | ").Append(n).Append(' ').Append(ok ? "TAK" : "NIE").Append(" (").Append(t).Append(')'); if (ok) pass++; else fail++; };
            try
            {
                if (HasFief) { Log.Info(P + "test (autotest, plik " + AutoFlagName + ") | wioska juz nadana (" + _p.Name + ") - bez nowego nadania; linie doby pokaza dochod."); return; }
                // krolestwo: gracza albo pana najblizszej wsi gry (bez wojny z graczem)
                Kingdom k = Clan.PlayerClan.Kingdom;
                if (k == null)
                {
                    var pos = MobileParty.MainParty.GetPosition2D; float bd = float.MaxValue;
                    foreach (var st in Settlement.All)
                    {
                        if (st == null || !st.IsVillage || st.Village == null || st.Village.Bound == null) continue;
                        var lc = st.Village.Bound.OwnerClan;
                        if (lc == null || lc.Kingdom == null || ReferenceEquals(lc, Clan.PlayerClan)) continue;
                        if (Clan.PlayerClan.MapFaction != null && FactionManager.IsAtWarAgainstFaction(lc.Kingdom, Clan.PlayerClan.MapFaction)) continue;
                        float d = st.GetPosition2D.Distance(pos);
                        if (d < bd) { bd = d; k = lc.Kingdom; }
                    }
                }
                if (k == null) { Log.Info(P + "test (autotest) | NIE - brak krolestwa bez wojny z graczem."); return; }
                int before = _dIntercept;
                bool called = _bkWired && InvokeBk(k, false);
                add("T1 latka BK GiveLordshipOnKingdomJoin", _bkWired, _bkWired ? "wpieta" : "BRAK");
                add("T2 prefiks przejal wywolanie BK", called && _dIntercept == before + 1, "wywolanie " + (called ? "tak" : "nie") + ", przejec " + (_dIntercept - before));
                if (!HasFief && _pendKingdom != null)
                {
                    string why; _pendKingdom = null;
                    if (!Grant(k, "autotest (bez wojny)", true, out why)) add("T3 nadanie", false, why);
                }
                add("T3 wioska nadana", HasFief, HasFief ? _p.Name + " (" + _p.Uid + "), okreg " + _p.Dist : "brak");
                if (HasFief)
                {
                    var st = Settlement.Find(_p.Dist);
                    var h = st != null ? VillageHolders.Of(st, _p.Uid) : null;
                    string code = VillageHolders.Code(st, _p.Uid);
                    add("T4 gospodarz = gracz, herb gracza", h != null && h.Rule == VillageHolders.RulePlayer && code == BannerCodeOf(_p.Uid), h != null ? "regula " + h.Rule + ", herb " + (code == BannerCodeOf(_p.Uid) ? "gracza" : "inny") : "brak");
                    add("T5 dymek", VillageHolders.Line(h) != null, VillageHolders.Line(h) ?? "brak");
                    string e1 = Export(); string u = _p.Uid, dd = _p.Dist;
                    Import(e1); string e2 = Export();
                    add("T6 zapis tam i z powrotem", e1 == e2 && _p != null && _p.Uid == u && _p.Dist == dd, e1);
                    string mt = MenuText();
                    add("T7 tekst menu", mt.Length > 40, mt.Replace('\n', ' ').Substring(0, Math.Min(160, mt.Length)));
                    add("T8 model finansow (linia podpowiedzi)", _finWired, _finWired ? "wpieta (" + (_finDecl != null ? _finDecl.Name : "?") + ")" : "BRAK");
                    string tw; long td = Due(_p, out tw, true);
                    double sh = _p.Share;
                    add("T9 udzial wioski w okregu", sh > 0 && sh < 1, (100 * sh).ToString("0.0", Inv) + "% (" + _p.ShareNote + ")");
                    // T11 (r4-wioska): naleznosc dnia znana od razu (bez wozu i bez wczorajszej wartosci) i tekst zakladki Fiefs
                    string th, tl, ti; bool tab = ClanTab(out th, out tl, out ti);
                    // recenzja: takze rejestracja wstawki i mixinu UIExtenderEx (przy kluczu WL) - sam napis nie dowodzi bloku w Fiefs; wyglad bloku tylko recznie w ekranie rodu
                    var ts = Settings.Current;
                    bool ui = (ts != null && !ts.SideVillageFiefClanTab) || (SideVillageFiefUi.State ?? "").StartsWith("wstawka", StringComparison.Ordinal);
                    add("T11 dochod dnia i zakladka Fiefs", td > 0 && tab && ui, "nalezne " + td + " (" + _p.DueWhyPl + (tw.Length > 0 ? ", " + tw : "") + "), Fiefs: " + (tab ? th + " | " + tl + " | " + ti : "BRAK")
                        + ", lista - " + SideVillageFiefUi.State);
                    // T10 (recenzja, WAZNE): nocleg pod dachem - stan noclegu karczmy z uid wioski i zapis arm_inns tam i z powrotem; stan noclegu gracza przywrocony
                    string i0 = TownInns.Export();
                    bool made = TownInns.HostSleep(_p.Dist, _p.Uid, _p.Name);
                    string i1 = TownInns.Export();
                    TownInns.Import(i1); string i2 = TownInns.Export();
                    TownInns.Import(i0);
                    bool f17 = i1.EndsWith(";" + _p.Uid, StringComparison.Ordinal);
                    add("T10 nocleg pod dachem (stan i zapis)", made ? i1 == i2 && f17 : !TownInns.On,
                        made ? (i1 == i2 ? "zapis tam i z powrotem" : "zapis ROZNY") + ", pole 17 " + (f17 ? "uid wioski" : "BRAK") + ", stan gracza przywrocony (" + (i0.Length > 0 ? "byl" : "brak") + ")"
                             : "bez stanu (karczmy " + (TownInns.On ? "WL" : "WYL") + ")");
                }
            }
            catch (Exception e) { add("wyjatek", false, e.GetType().Name + ": " + e.Message); Stumble("Test", e); }
            Log.Info(P + "test (autotest, plik " + AutoFlagName + ") | wynik TAK " + pass + ", NIE " + fail + sb.ToString() + ".");
        }

        /// <summary>ArmouryBehavior.OnDailyTick: srednia 7 dob udzialu (podpowiedz zlota) i linia doby (start sesji zawsze, potem tylko z wioska albo ruchem).</summary>
        internal static void Daily()
        {
            try
            {
                var s = Settings.Current;
                if (s == null) return;
                int day = Today() - 1;
                if (!_layoutChecked) CheckLayout();
                Pay();   // r4-wioska: dochod dnia (naleznosc wioski z licznika podatku wsi okregu, reszta z kiesy platnika) - przed srednia i linia doby
                PushRings();   // m113-zakup F4 / F6
                // srednia: przez pierwsze 7 dob od nadania srednia z dob, potem wykladnicza 7 dob (bez nowego stanu w zapisie - doba nadania jest w zapisie)
                foreach (var o in Owned()) { int n = Math.Max(1, Math.Min(7, Today() - o.Day0)); o.Avg += (o.DPaid - o.Avg) / n; if (o.Avg < 0) o.Avg = 0; }
                bool start = !_startLogged;
                _startLogged = true;
                if (!start && !HasFief && _dIntercept == 0 && _st == 0) return;
                var sb = new StringBuilder(500);
                sb.Append(P).Append("dzien ").Append(day).Append(start ? " (start sesji)" : "")
                  .Append(" | klucze: start ").Append(s.SideVillageFiefStart ? "WL" : "WYL").Append(", dochod ").Append(s.SideVillageFiefIncome ? "WL" : "WYL").Append(", menu ").Append(s.SideVillageFiefMenu ? "WL" : "WYL")
                  .Append(", nocleg pod dachem ").Append(s.SideVillageFiefLodging ? (TownInns.On ? "WL" : "WL (karczmy WYL)") : "WYL")
                  .Append(", platnik = posiadacz podatku ").Append(s.SideVillagePayerIsTaxHolder ? "WL" : "WYL")
                  .Append(" | latki: BK GiveLordshipOnKingdomJoin ").Append(_bkWired ? "wpieta" : "BRAK").Append(", podpowiedz zlota ").Append(_finWired ? "wpieta (" + (_finDecl != null ? _finDecl.Name : "?") + ")" : "BRAK")
                  .Append(", opis startu BK (StartOptionVM.ShortDescription) ").Append(_startTxtWired ? "wpiety" : "BRAK")
                  .Append(", zakladka Fiefs: tekst ").Append(_noFiefsWired ? "wpiety" : "BRAK").Append(", lista - ").Append(SideVillageFiefUi.State)
                  .Append(", klucz ").Append(s.SideVillageFiefClanTab ? "WL" : "WYL");
                if (HasFief)
                {
                    var o = _p;
                    sb.Append(" | wioska ").Append(o.Name).Append(" (").Append(o.Uid).Append(", okreg ").Append(o.Dist).Append(", od doby ").Append(o.Day0).Append(')')
                      .Append(" | wozy okregow ").Append(_dCarts).Append(", podatek pana w oknach ").Append(_dTax).Append(", udzial ").Append((100 * o.Share).ToString("0.0", Inv)).Append("% (").Append(o.ShareNote).Append(")")
                      .Append(" | dochod dnia (r4): nalezne ").Append(o.Due).Append(" (").Append(o.DueWhyPl).Append("), do gracza ").Append(o.DPaid).Append(" (razem ").Append(o.Paid).Append("), srednia 7 dob ").Append(o.Avg.ToString("0.0", Inv));
                    int extra = 0;
                    foreach (var x in _extra)
                    {
                        if (x == null) continue;
                        extra++;
                        sb.Append(extra == 1 ? " | dodatkowe (m113): " : "; ").Append(x.Name).Append(" (").Append(x.Uid).Append(x.Active ? "" : ", NIECZYNNA").Append(") nalezne ").Append(x.Due)
                          .Append(", do gracza ").Append(x.DPaid).Append(" (razem ").Append(x.Paid).Append(", cena ").Append(x.Price).Append(")");
                    }
                    sb.Append(" | razem wszystkie wioski: z licznika podatku wsi ").Append(_dFromTax).Append(", z kiesy platnikow ").Append(_dFromLord)
                      .Append(", brak u platnikow ").Append(_dShort).Append(", do gracza ").Append(_dPaid).Append(", srednia 28 dob ").Append(_paidRing.Avg().ToString("0.0", Inv))
                      .Append(", rodow placacych z kiesy (F6) ").Append(_drawn.Count)
                      .Append(", bez przelewu: wojna ").Append(_dWar).Append(", gracz panem ").Append(_dSelf).Append(", bez udzialu ").Append(_dNoShare);
                }
                else sb.Append(" | brak wioski gracza").Append(_pendKingdom != null ? " (nadanie czeka: krolestwo " + _pendKingdom + " od doby " + _pendDay + ")" : "");
                sb.Append(" | przejecia startu BK ").Append(_dIntercept).Append(", wjazdy ").Append(_dOpen).Append(" (klik ").Append(_dClick).Append(", pytania ").Append(_dAsk).Append(", wybory ").Append(_dPick).Append(")")
                  .Append(", noclegi ").Append(_dRest).Append(" (pod dachem ").Append(_dRoof).Append(")").Append(", okna prac ").Append(_dInvest).Append(", linie podpowiedzi ").Append(_dTip)
                  .Append(" | potkniecia doby ").Append(_stDay).Append(", sesji ").Append(_st).Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally
            {
                DayZero();
                SideVillageHome.Daily();   // r4-dom: linia doby domu, handlu i spaceru - w finally, bo "return" w try (bez wioski) pomijal ja (recenzja)
            }
        }
    }
}
