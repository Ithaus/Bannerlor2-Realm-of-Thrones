using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// R3-05f (Jeff 20.10 "pozostale tak"; audyt 10-pomysly Z7) - JESIENNY UBOJ I SOLENIE PRZED ZIMA. Klucz MCM Herd Cull Enabled (domyslnie TAK).
    ///
    /// Przyczyna -> skutek: na zime brak paszy -> w ostatnich 28 dniach jesieni (pora Westeros albo kalendarza gry, gdy klimat bez stanu) wsie hodowlane
    /// ubijaja czesc stada -> wiecej miesa i skor na targu (woz wsi wiezie je jak kazdy towar magazynu) -> do solenia miesa wies kupuje sol w swoim miescie
    /// targowym -> sol znika z polek jesienia i drozeje (cena gry z podazy) -> zima na targ idzie mniej zwierzat, bo czesc stada juz ubito.
    ///
    /// STADO MA STAN (sprawdzenie audytu: gra nie trzyma liczby zwierzat wsi - BK tez nie; wies ma tylko przyrost dzienny, ktory TickGoodProduction
    /// dopisuje do magazynu i oglasza OnItemProduced). Dlatego uboj to ZALICZKA na przyszly przyrost: kazde zwierze ubite ponad zwykly przyrost to
    /// dlug stada tej wsi (krowa / swinia / owca osobno, w zapisie). Po oknie uboju (zima i dalej, az do splaty) wies oddaje z dziennego przyrostu
    /// polowe, dopoki dlug nie spadnie do 0 - tych zwierzat po prostu nie ma. Suma zwierzat w roku ta sama: nic z niczego.
    ///  - uboj w oknie: dodatkowo 0.75 dziennego przyrostu tej wsi (21 dni przyrostu na okno), ulamki przechodza na nastepna dobe;
    ///  - mieso, skora, welna z zwierzecia - linia ukrytych rzemieslnikow BK (workshops.xml "artisans"): krowa 6 miesa + 2 skory, owca 1 + 1 skora + 1 welna,
    ///    swinia 2 + 1 skora - ten sam przelicznik co uboj w miastach (SlaughterHides);
    ///  - sol: 0.1 kg soli na kg miesa (solenie na sucho); wies kupuje cale worki w miescie targowym (Village.TradeBound, inaczej Bound) po cenie targu,
    ///    najpierw zloto (kiesa wsi -> kasa miasta), potem towar z polki; polka zostawia 5 workow (jak koszyk wsi). Bez soli albo bez zlota zwierzeta zyja
    ///    (nie ma uboju bez solenia - i nie ma dlugu stada); resztka soli z worka czeka w beczce wsi na nastepna dobe.
    /// Wies spladrowana: uboj i splata ida za jej przyrostem - gdy Devastation pozwala jej produkowac (na czas ticku stan Normal, mniej rak), jak w zwyklej;
    /// bez tego gra nic nie dopisuje, wiec i nic sie nie dzieje. Wies opuszczona - nic.
    /// REGULATOR WSI (HarvestValve E6/W3, recenzja): zmiany magazynu wsi w jej ticku produkcji zglaszamy regulatorowi (HarvestValve.OffTick) - skory i welna
    /// z uboju to inne zrodlo (jak warsztaty, nie produkcja wsi pod mnoznikiem), zwierzeta splaty to produkcja gry, ktora zniknela nie przez zuzycie swiata
    /// (jak gnicie) - regulator widzi produkcje i zuzycie zwierzat jak bez uboju i nie goni sezonowego braku zima.
    /// SCALANIE z p5-stada (VillageHerds R3-06c, stan stada H po rabunkach): dwa rozne zjawiska - H to majatek stada (rabunek i odrost), dlug uboju to
    /// zaliczka na przychowek; uboj i splata licza sie od przyrostu, ktory gra wlasnie dopisala (juz x H), wiec sie nie dubluja; dlug w sztukach - wies
    /// zlupiona splaca dluzej (mniej zwierzat), dalej bez sztuki z niczego. Kroki w MaterialLaw i StartCalibration.Live dotycza roznych towarow (plony / stado).
    /// Linia logu "Uboj jesienny (R3-05f):" w kazdej dobie okna albo splaty, inaczej co 28 dob. Wyjatki per obiekt (potkniecia), bez wylacznikow.
    /// Watek: OnItemProduced i Daily - watek glowny (dzienny tick osad); bez odczytow z watkow AI.
    /// </summary>
    internal static class HerdCull
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const int WindowDays = 28;
        private const float CullShare = 0.75f, RepayShare = 0.5f;
        private const double SaltPerKgMeat = 0.1;
        private const int ShelfKeep = 5;
        private static readonly string[] HerdIds = { "cow", "hog", "sheep" };
        private static readonly int[] MeatPer = { 6, 2, 1 }, HidesPer = { 2, 1, 1 }, WoolPer = { 0, 0, 1 };

        private sealed class Herd { public readonly float[] Debt = new float[3], CullAcc = new float[3], RepAcc = new float[3]; public double Salt; }
        private static readonly Dictionary<string, Herd> _herds = new Dictionary<string, Herd>(StringComparer.Ordinal);
        private static Campaign _camp;
        private static ItemObject _meat, _hides, _wool, _salt;
        private static Campaign _itemsCamp;                  // kampania, dla ktorej szukano przedmiotow (gra tworzy przedmioty od nowa przy kazdym wczytaniu)
        private static bool _window, _repay;
        private static int _lastLog = -1;
        private static bool _firstLine, _lastWindow;

        // liczniki doby (zerowane w Daily po linii)
        private static readonly int[] _culled = new int[3], _repaid = new int[3];
        private static int _meatN, _hidesN, _woolN, _saltN, _saltGold, _noSalt, _cullVills;
        private static readonly HashSet<string> _vToday = new HashSet<string>(StringComparer.Ordinal);
        private static int _stumbles;
        private static readonly HashSet<string> _errOnce = new HashSet<string>();

        internal static bool KeyOn { get { var s = Settings.Current; return s != null && s.HerdCullEnabled; } }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_errOnce.Add(where)) Log.Error("HerdCull." + where, e); } catch { }
        }

        /// <summary>Konstruktor ArmouryBehavior (nowa kampania, przed wczytaniem): stan i przedmioty od zera.</summary>
        internal static void Reset()
        {
            _camp = null; _itemsCamp = null; _meat = _hides = _wool = _salt = null;
            ResetIfNewCampaign();
        }

        private static void ResetIfNewCampaign()
        {
            var cur = Campaign.Current;
            if (_camp != null && ReferenceEquals(cur, _camp)) return;
            _camp = cur; _herds.Clear(); _window = false; _repay = false; _lastLog = -1; _firstLine = false; _lastWindow = false;
            _stumbles = 0; _errOnce.Clear(); ZeroDay();
        }

        private static void ZeroDay()
        {
            for (int i = 0; i < 3; i++) { _culled[i] = 0; _repaid[i] = 0; }
            _meatN = _hidesN = _woolN = _saltN = _saltGold = _noSalt = _cullVills = 0; _vToday.Clear();
        }

        /// <summary>Przedmioty biezacej kampanii (recenzja: po drugim wczytaniu w tym samym uruchomieniu stare przedmioty nie pasowaly do polek - sol "znikala").
        /// Szukane raz na kampanie, gdy menedzer obiektow jest gotowy (bez niego - nastepna proba).</summary>
        private static bool Items()
        {
            var cur = Campaign.Current;
            if (cur != null && !ReferenceEquals(cur, _itemsCamp))
            {
                _meat = _hides = _wool = _salt = null;
                try
                {
                    var om = MBObjectManager.Instance;
                    if (om != null)
                    {
                        _meat = om.GetObject<ItemObject>("meat"); _hides = om.GetObject<ItemObject>("hides"); _wool = om.GetObject<ItemObject>("wool"); _salt = om.GetObject<ItemObject>("salt");
                        _itemsCamp = cur;
                    }
                }
                catch (Exception e) { Stumble("Items", e); }
            }
            return _meat != null && _hides != null && _salt != null;
        }

        private static int HerdIdx(ItemObject it)
        {
            var id = it != null ? it.StringId : null;
            if (id == null) return -1;
            for (int i = 0; i < HerdIds.Length; i++) if (id == HerdIds[i]) return i;
            return -1;
        }

        /// <summary>Okno uboju: ostatnie 28 dni jesieni (pora gospodarki - WesterosClimate; dlugosc z kalendarza Westeros, bez stanu klimatu - pora gry).</summary>
        private static bool WindowNow(out int left)
        {
            left = -1;
            if (WesterosClimate.Now() != CampaignTime.Seasons.Autumn) return false;
            int season, day, len;
            if (WesterosClimate.SeasonInfo(out season, out day, out len) && len > 0 && season == (int)CampaignTime.Seasons.Autumn)
                left = Math.Max(0, len - day);
            else
            {
                var now = CampaignTime.Now;
                left = Math.Max(0, CampaignTime.DaysInSeason - 1 - now.GetDayOfSeason);
            }
            return left < WindowDays;
        }

        // ------------------------------------------------------------ doba
        /// <summary>Z ArmouryBehavior.OnDailyTickBodyInner (po HarvestYear.Daily): okno uboju i splaty tej doby, linia logu za dobe miniona. Nie rzuca.</summary>
        internal static void Daily()
        {
            try
            {
                ResetIfNewCampaign();
                int today = (int)CampaignTime.Now.ToDays;
                if (!KeyOn)
                {
                    if (!_firstLine) { _firstLine = true; Log.Info("Uboj jesienny (R3-05f): WYLACZONY (Herd Cull Enabled = off) - stada wsi jak dotad; dlug stad z zapisu czeka (" + DebtText() + ")."); }
                    _window = false; _repay = false; ZeroDay();
                    return;
                }
                int left; bool w = WindowNow(out left);
                bool any = false; foreach (var h in _herds.Values) if (h.Debt[0] > 0 || h.Debt[1] > 0 || h.Debt[2] > 0) { any = true; break; }
                bool busy = _culled[0] + _culled[1] + _culled[2] + _repaid[0] + _repaid[1] + _repaid[2] + _noSalt > 0;
                if (busy || w != _lastWindow || !_firstLine || today - _lastLog >= 28)
                {
                    _lastLog = today; _firstLine = true;
                    Log.Info(Line(today, w, left));
                }
                ZeroDay();
                _window = w; _lastWindow = w;
                _repay = !w && any;
            }
            catch (Exception e) { Stumble("Daily", e); }
        }

        private static string DebtText()
        {
            double[] d = new double[3]; int n = 0;
            foreach (var h in _herds.Values) { bool a = false; for (int i = 0; i < 3; i++) if (h.Debt[i] > 0) { d[i] += h.Debt[i]; a = true; } if (a) n++; }
            return "krowy " + d[0].ToString("0", Inv) + ", swinie " + d[1].ToString("0", Inv) + ", owce " + d[2].ToString("0", Inv) + " w " + n + " wsiach";
        }

        private static string Line(int today, bool w, int left)
        {
            var sb = new StringBuilder(400);
            sb.Append("Uboj jesienny (R3-05f): dzien ").Append(today).Append(" | okno uboju ").Append(w ? "TAK" : "NIE")
              .Append(" (pora ").Append(WesterosClimate.Now()).Append(left >= 0 ? ", do konca jesieni " + left + " dni" : "").Append("; okno = ostatnie ").Append(WindowDays).Append(" dni jesieni)")
              .Append(" | doba miniona: ubite ponad przyrost ").Append(_culled[0] + _culled[1] + _culled[2]).Append(" (krowy ").Append(_culled[0]).Append(", swinie ").Append(_culled[1]).Append(", owce ").Append(_culled[2])
              .Append(") w ").Append(_cullVills).Append(" wsiach -> mieso ").Append(_meatN).Append(", skory ").Append(_hidesN).Append(", welna ").Append(_woolN)
              .Append(" do magazynow wsi; sol kupiona ").Append(_saltN).Append(" worki za ").Append(_saltGold).Append(" zl (kiesy wsi -> kasy miast), bez soli lub zlota zwierzat zostawionych ").Append(_noSalt)
              .Append(" | splata stada (po oknie, polowa przyrostu): mniej na targ ").Append(_repaid[0] + _repaid[1] + _repaid[2]).Append(" (krowy ").Append(_repaid[0]).Append(", swinie ").Append(_repaid[1]).Append(", owce ").Append(_repaid[2]).Append(')')
              .Append(" | dlug stad: ").Append(DebtText()).Append(" | potkniecia ").Append(_stumbles).Append('.');
            return sb.ToString();
        }

        // ------------------------------------------------------------ przyrost wsi (OnItemProduced)
        /// <summary>Nasluch OnItemProducedEvent (ArmouryBehavior.RegisterEvents): przyrost zwierzat wsi tej doby - w oknie uboj ponad przyrost (z sola),
        /// po oknie splata dlugu stada (zwierzeta zdjete z magazynu, ktory gra wlasnie dopisala). Watek glowny. Nie rzuca.</summary>
        internal static void OnItemProduced(ItemObject item, Settlement st, int n)
        {
            if (n <= 0 || st == null || !st.IsVillage) return;
            int idx = HerdIdx(item);
            if (idx < 0) return;
            try
            {
                if (!KeyOn || (!_window && !_repay)) return;
                ResetIfNewCampaign();
                var v = st.Village;
                if (v == null || v.VillageState != Village.VillageStates.Normal) return;
                string id = st.StringId; if (id == null) return;
                Herd h;
                if (_window)
                {
                    if (!Items()) return;
                    if (!_herds.TryGetValue(id, out h)) _herds[id] = h = new Herd();
                    h.CullAcc[idx] += CullShare * n;
                    int k = (int)Math.Floor(h.CullAcc[idx]);
                    if (k <= 0) return;
                    h.CullAcc[idx] -= k;
                    double meatKg = MeatPer[idx] * Math.Max(0.1f, _meat.Weight > 0.01f ? _meat.Weight : 10f);
                    double saltPc = meatKg * SaltPerKgMeat / Math.Max(0.1f, _salt.Weight > 0.01f ? _salt.Weight : 10f);   // worki soli na zwierze
                    double need = k * saltPc;
                    if (h.Salt + 1e-9 < need) h.Salt += BuySalt(v, (int)Math.Ceiling(need - h.Salt - 1e-9));
                    int ok = Math.Min(k, saltPc > 0 ? (int)Math.Floor((h.Salt + 1e-9) / saltPc) : k);
                    if (ok < k) _noSalt += k - ok;
                    if (ok <= 0) return;
                    h.Salt = Math.Max(0.0, h.Salt - ok * saltPc);
                    var roster = st.ItemRoster;
                    // HarvestValve.OffTick: towar uboju to nie produkcja wsi pod regulatorem (inne zrodlo) - poza pomiarem przyrostu magazynu w tym ticku
                    roster.AddToCounts(_meat, ok * MeatPer[idx]); _meatN += ok * MeatPer[idx]; HarvestValve.OffTick(v, _meat, ok * MeatPer[idx], 0);
                    roster.AddToCounts(_hides, ok * HidesPer[idx]); _hidesN += ok * HidesPer[idx]; HarvestValve.OffTick(v, _hides, ok * HidesPer[idx], 0);
                    if (WoolPer[idx] > 0 && _wool != null) { roster.AddToCounts(_wool, ok * WoolPer[idx]); _woolN += ok * WoolPer[idx]; HarvestValve.OffTick(v, _wool, ok * WoolPer[idx], 0); }
                    h.Debt[idx] += ok; _culled[idx] += ok;
                    if (_vToday.Add(id)) _cullVills++;
                }
                else if (_herds.TryGetValue(id, out h) && h.Debt[idx] > 0f)
                {
                    h.RepAcc[idx] += RepayShare * n;
                    int k = Math.Min(n, Math.Min((int)Math.Floor(h.RepAcc[idx]), (int)Math.Ceiling(h.Debt[idx] - 1e-6f)));
                    if (k <= 0) return;
                    h.RepAcc[idx] -= k;
                    var roster = st.ItemRoster;
                    int have = roster.GetItemNumber(item);
                    k = Math.Min(k, have);
                    if (k <= 0) return;
                    roster.AddToCounts(item, -k);
                    HarvestValve.OffTick(v, item, 0, k);   // splata: produkcja gry byla, zniknela nie przez zuzycie swiata - regulator jak bez uboju
                    h.Debt[idx] = Math.Max(0f, h.Debt[idx] - k); _repaid[idx] += k;
                    if (h.Debt[idx] <= 0f) h.RepAcc[idx] = 0f;
                }
            }
            catch (Exception e) { Stumble("OnItemProduced", e); }
        }

        /// <summary>Zakup soli w miescie targowym wsi: najwyzej `want` workow ponad 5 na polce, cena targu; najpierw zloto (wies -> kasa miasta), potem towar. Zwraca worki.</summary>
        private static int BuySalt(Village v, int want)
        {
            if (want <= 0) return 0;
            try
            {
                var mk = v.TradeBound ?? v.Bound;
                var town = mk != null ? mk.Town : null;
                if (town == null) return 0;
                var roster = mk.ItemRoster;
                var e = new EquipmentElement(_salt);
                int ix = roster.FindIndexOfElement(e);
                if (ix < 0) return 0;
                int have = roster.GetElementNumber(ix) - ShelfKeep;
                if (have <= 0) return 0;
                int price = town.MarketData.GetPrice(e);
                if (price <= 0) return 0;
                int n = Math.Min(want, have);
                int had = v.Gold;
                if (n * price > had) n = had / price;
                if (n <= 0) return 0;
                v.ChangeGold(-n * price);
                int got = had - v.Gold;
                if (got <= 0) return 0;
                town.ChangeGold(got);
                roster.AddToCounts(e, -n);
                _saltN += n; _saltGold += got;
                return n;
            }
            catch (Exception ex) { Stumble("BuySalt", ex); return 0; }
        }

        // ------------------------------------------------------------ zapis
        /// <summary>Zapis: "2|okno|wies:krowy,swinie,owce;..." - okno uboju w chwili zapisu (1/0; recenzja: wczytanie w oknie nie zaczyna od splaty)
        /// i tylko wsie z dlugiem stada. Pusty - brak dlugu i poza oknem. Odczyt zna tez "1|wiersze" (pierwsza wersja galezi, bez okna).</summary>
        internal static string Export()
        {
            try
            {
                ResetIfNewCampaign();
                var sb = new StringBuilder(256);
                foreach (var kv in _herds)
                {
                    var d = kv.Value.Debt;
                    if (!(d[0] > 0 || d[1] > 0 || d[2] > 0)) continue;
                    if (sb.Length > 0) sb.Append(';');
                    sb.Append(kv.Key).Append(':').Append(d[0].ToString("0.##", Inv)).Append(',').Append(d[1].ToString("0.##", Inv)).Append(',').Append(d[2].ToString("0.##", Inv));
                }
                if (sb.Length == 0 && !_window) return "";
                return "2|" + (_window ? "1" : "0") + "|" + sb.ToString();
            }
            catch (Exception e) { Stumble("Export", e); return ""; }
        }

        internal static void Import(string data)
        {
            try
            {
                _camp = Campaign.Current; _herds.Clear(); _firstLine = false; _lastLog = -1; _window = false; _repay = false; _lastWindow = false;
                if (string.IsNullOrEmpty(data)) return;
                string rows; bool w = false;
                if (data.StartsWith("2|", StringComparison.Ordinal))
                {
                    int bar = data.IndexOf('|', 2); if (bar < 0) return;
                    w = data.Substring(2, bar - 2) == "1";
                    rows = data.Substring(bar + 1);
                }
                else if (data.StartsWith("1|", StringComparison.Ordinal)) rows = data.Substring(2);
                else return;
                foreach (var row in rows.Split(';'))
                {
                    try
                    {
                        if (row.Length == 0) continue;
                        int c = row.IndexOf(':'); if (c <= 0) continue;
                        var p = row.Substring(c + 1).Split(','); if (p.Length < 3) continue;
                        var h = new Herd();
                        for (int i = 0; i < 3; i++) { float x; if (float.TryParse(p[i], NumberStyles.Float, Inv, out x) && x > 0 && !float.IsInfinity(x)) h.Debt[i] = x; }
                        _herds[row.Substring(0, c)] = h;
                    }
                    catch (Exception e) { Stumble("Import(wiersz)", e); }
                }
                bool any = false; foreach (var h in _herds.Values) if (h.Debt[0] > 0 || h.Debt[1] > 0 || h.Debt[2] > 0) { any = true; break; }
                // stan doby z chwili zapisu (zegar ten sam): w oknie - uboj, poza oknem z dlugiem - splata, od razu po wczytaniu (przyrost doby moze biec przed
                // Daily); dalej rozstrzyga pierwsza Daily (w SyncData bez odczytow czasu i klimatu)
                bool on = KeyOn;
                _window = w && on; _lastWindow = _window;
                _repay = !_window && any && on;
            }
            catch (Exception e) { _herds.Clear(); Stumble("Import", e); }
        }
    }
}
