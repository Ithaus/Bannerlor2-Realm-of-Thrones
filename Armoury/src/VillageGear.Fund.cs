using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// 435-L7 - LANCUCH PODAZY, krok 7: FUNDATORZY SPRZETU WSI I CZYNSZ OD SPRZETU (projekt PROJEKT-LANCUCH-po-krytyce 11.11, II.5 pkt 2-5, II.10, II.11, II.14, II.16,
    /// wiersz L7; krytyka B2, B3, B9, B13). Polecenie Jeffa: "mamy opcje inwestuje w dana wies, wtedy aby produkcja wzrosla, najpierw trzeba zainwestowac w dana wies
    /// i wtedy wiecej ludzi moze produkowac dany surowiec ... inwestycja + ludzie daje 100% efektu".
    /// JEDNA REGULA dla trzech fundatorow (pan wsi AI, kupcy miasta targowego, gracz):
    ///  - fundator kupuje TYLKO brakujace jednostki sprzetu czesci okregu (rece bez sprzetu, do K = H - bez "nadsprzetu"), za pelna cene jednostki P (dni rodzaju x
    ///    wartosc dnia czlowieka): material KM x P - PRAWDZIWE sztuki z polki miasta targowego (ta sama droga co roboty gminy), rzemieslnicy miasta najwyzej
    ///    DsWorksWagePerMaterial x material -> kiesy ludu, reszta (praca wsi) -> kiesa wsi. Brak materialu na polce - nie ma zakupu (zadnej drogi "same place").
    ///  - sprzet jest wlasnoscia gminy (wchodzi do K czesci jak sprzet z robot gminy); fundator ma WIERZYTELNOSC G = wplacona kwota i dostaje CZYNSZ: G x Village Gear
    ///    Rent / 364 na dobe (staly procent od wkladu, nie udzial w produkcji - krytyka B9), naliczany tylko gdy wies jest Normal i fundator nie wojuje z krolestwem wsi;
    ///    placony przy powrocie wozu do wsi macierzystej z czesci wsi tego utargu (kiesa wsi -> fundator), najwyzej polowa czesci wsi z tego wozu; nalezne ponad 28 dob
    ///    czynszu przepada (bez dlugu wsi).
    ///  - wierzytelnosc gasnie co dobe wedlug tabeli zuzycia rodzaju (G x (1 - delta)^(1/364) - takze przy wylaczonym zuzyciu sprzetu: wierzytelnosc nie jest wieczna)
    ///    i o strate ognia czesci. Bez sprzedazy wierzytelnosci. Rod wymarl - wierzytelnosc znika, sprzet zostaje gminie.
    /// PAN WSI (AI; klucz Village Gear Lords Invest): rod ma swoja dobe raz na 28 dob; progi istniejace (District Works Ai Reserve Days / Reserve Flat / Surplus Share /
    ///   World Pace - tempo swiata w oknie 28 dob, pamiec w zapisie); rod z dlugiem w Banku, z pozyczka korony na zold (431), w zaleglosci drabiny albo w potrzebie
    ///   (pasmo zwrotu zoldu ponizej 2R) nie funduje - pozyczka nie finansuje inwestycji; tylko wlasne wsie, czesci z m ponizej 0.95, najpierw najwyzszy niedobor N.
    ///   NIE przez DistrictWorks.Buy i NIE jako wiersz prac okregu (krytyka B2 i B3: prace okregu zamrazaja zmiane zajec i zamowienia 363).
    /// MIASTO TARGOWE (kupcy; klucz Village Gear Towns Invest): miasto ma swoja dobe raz na 28 dob; warunki: zero otwartych rachunkow wsi wobec miasta (KsFoodCredit -
    ///   "wies przed panem" 428), zero dlugu rady (TownDebt), kasa co najmniej 1.2 x zapas kupcow; kwota najwyzej 10% nadwyzki ponad zapas; tylko wsie handlujace z tym
    ///   miastem i tylko zajecia, ktorych towar jest wsadem warsztatu tego miasta i ktorego w tym miescie brakuje (tablica N pomiaru Supply Chain Log).
    /// GRACZ (klucz Village Gear Player Funds): pozycja "Fund gear for this village's hands" w menu wsi.
    /// KOLEJNOSC W DOBIE: ArmouryBehavior zaraz po TownFolk.DaWorksRun (po budowach pana i ludu i po zleceniach gminy tej doby - fundator nie zabiera im materialu),
    ///   przed migawka MBuild ksiegi pieniadza. Czynsz: VillageTakings.EnterPostfix po wyplacie prac okregu, przed danina DS.
    /// Zapis arm_lpf ("lpf1|okno tempa panow|wierzytelnosci"; zly wpis pominiety i policzony; brak klucza = stan pusty; starsza DLL ignoruje klucz).
    /// Stan pod Gate (wspolny ze sprzetem); wyjatki per rod / miasto / woz liczone (potkniecia), bez globalnego wylacznika.
    /// </summary>
    internal static partial class VillageGear
    {
        internal const string P7 = "LP czynsz (435-L7): ";
        private const string FundVer = "lpf1";
        internal const int RentKeepDays = 28;             // nalezny czynsz ponad tyle dob przepada
        private const int FundCycle = 28;                 // rod / miasto funduje raz na tyle dob (wlasna doba z nazwy)
        private const double FundMinGold = 200.0;         // najmniejszy zakup
        private const double FundMMax = 0.95;             // AI funduje czesci z m ponizej tej liczby
        private const double TownCofferTimes = 1.2;       // miasto funduje przy kasie >= tyle x zapas kupcow
        private const double TownSurplusShare = 0.10;     // ... najwyzej tyle nadwyzki ponad zapas
        private const double ClaimWearDefault = 0.06;     // gasniecie wierzytelnosci czesci bez wyceny zuzycia
        private const int FLord = 0, FTown = 1, FPlayer = 2, FKinds = 3;
        private static readonly string[] FName = { "panowie", "miasta", "gracz" };
        private const int WNoNeed = 1, WSmall = 2, WPay = 10;   // powody FundBuy: 10 + TownFolk.Gf*

        private sealed class Claim { public string Who, Dist, Part; public double G, Acc, PaidAll; public int Day; }
        private static readonly Dictionary<string, List<Claim>> _claims = new Dictionary<string, List<Claim>>(StringComparer.Ordinal);   // okreg -> wierzytelnosci
        private static volatile int _claimN;
        private static readonly double[] _ring = new double[FundCycle]; private static readonly int[] _ringDay = new int[FundCycle];   // tempo swiata panow (okno 28 dob)
        private static int _fundDay = int.MinValue, _fLoadBad;
        private static string _fLoadNote = "nowa kampania albo zapis bez arm_lpf (bez wierzytelnosci)";
        private static bool _startLine7, _menu7;
        private static Dictionary<string, Village> _vilById;
        private static ItemCategory[][] _catsOf;
        private static readonly List<string> _burnMsg = new List<string>();
        private static double _prevG = double.NaN; private static int _prevGDay = int.MinValue;

        // liczniki od ostatniej linii
        private static readonly double[] _fU = new double[FKinds];
        private static readonly long[] _fPaid = new long[FKinds], _fMat = new long[FKinds], _fWageFolk = new long[FKinds], _fWageCof = new long[FKinds], _fWork = new long[FKinds];
        private static readonly int[] _fN = new int[FKinds], _fPcs = new int[FKinds];
        private static readonly int[] _fWhy = new int[TownFolk.GfKinds];
        private static long _fGap; private static int _fPcsBad;
        private static double _dFundU;                    // jednostki od fundatorow od ostatniej linii sprzetu (kontrola sprzetu L3)
        private static double _gIn, _gDecay, _gBurn, _gDead, _gSmall, _gShift;   // _gShift (435-L4-p / L7-p): odstawione zmiana zajecia
        private static int _lClans, _lBank, _lCrown, _lBlocked, _lNeed, _lNoSurplus, _lVil, _lWarMk, _lBudget;
        // 435-L7-p: dluznik drabiny, rod bez miary budzetu, naleznosc zdjeta z wpisami, pamiec naleznego (kontrola), pierwsza doba sesji bez wyceny (rody / miasta dnia przeniesione na jutro)
        private static int _lDebtor, _lNoMeasure, _fundWait;
        private static double _accGone, _prevAcc = double.NaN;
        private static bool _carry, _carryNext;
        /// <summary>435-L7-p (recenzja ksiegi S2, klucz Village Gear Funder Pays Village Work): NIE (domyslnie) - fundator placi jak gmina: material i rzemieslnicy
        /// miasta (jedna cena jednostki, wierzytelnosc = to, co naprawde wyszlo z kiesy na sprzet); TAK - jak 435-L7: pelna cena jednostki, reszta jako "praca wsi"
        /// do kiesy wsi (i w wierzytelnosci).</summary>
        private static bool PaysWork { get { var s = Settings.Current; return s != null && s.VillageGearFunderPaysVillageWork; } }
        private static double _lWorld, _lPace;
        private static int _tLooked, _tSiege, _tKwit, _tDebt, _tCoffer, _tNoTable, _tNoShort, _tVil;
        private static double _rAcc, _rLapsed; private static long _rPaid, _rOut, _rIn, _rChest, _rHalf; private static int _rPaidN, _rHeld, _rWarCart;
        private static int _st7, _st7All; private static double _ms7;

        private static void FundReset()
        {
            _claims.Clear(); _claimN = 0; Array.Clear(_ring, 0, FundCycle); for (int i = 0; i < FundCycle; i++) _ringDay[i] = int.MinValue;
            _fundDay = int.MinValue; _fLoadBad = 0; _fLoadNote = "nowa kampania albo zapis bez arm_lpf (bez wierzytelnosci)";
            _startLine7 = false; _vilById = null; _catsOf = null; _burnMsg.Clear(); _prevG = double.NaN; _prevGDay = int.MinValue; _st7All = 0; _dFundU = 0; _prevAcc = double.NaN; _carry = false;
            FundZero();
        }

        private static void FundZero()
        {
            Array.Clear(_fU, 0, FKinds); Array.Clear(_fPaid, 0, FKinds); Array.Clear(_fMat, 0, FKinds); Array.Clear(_fWageFolk, 0, FKinds); Array.Clear(_fWageCof, 0, FKinds);
            Array.Clear(_fWork, 0, FKinds); Array.Clear(_fN, 0, FKinds); Array.Clear(_fPcs, 0, FKinds); Array.Clear(_fWhy, 0, _fWhy.Length);
            _fGap = 0; _fPcsBad = 0; _gIn = _gDecay = _gBurn = _gDead = _gSmall = _gShift = 0;
            _lClans = _lBank = _lCrown = _lBlocked = _lNeed = _lNoSurplus = _lVil = _lWarMk = _lBudget = 0;
            _lDebtor = _lNoMeasure = _fundWait = 0; _accGone = 0;
            _tLooked = _tSiege = _tKwit = _tDebt = _tCoffer = _tNoTable = _tNoShort = _tVil = 0;
            _rAcc = _rLapsed = 0; _rPaid = _rOut = _rIn = _rChest = _rHalf = 0; _rPaidN = _rHeld = _rWarCart = 0; _st7 = 0; _ms7 = 0;
        }

        private static void Stumble7(string where, Exception e)
        {
            bool first;
            lock (Gate) { _st7++; _st7All++; first = _err.Add("L7." + where); }
            if (first) { try { Log.Error("VillageGear.Fund." + where, e); } catch { } }
        }

        internal static bool LordsOn { get { var s = Settings.Current; return s != null && s.VillageGear && s.VillageGearLordsInvest; } }
        internal static bool TownsOn { get { var s = Settings.Current; return s != null && s.VillageGear && s.VillageGearTownsInvest; } }
        internal static bool PlayerOn { get { var s = Settings.Current; return s != null && s.VillageGear && s.VillageGearPlayerFunds; } }
        private static double RentRate(Settings s) { double r = s != null ? s.VillageGearRent : 0.20; return double.IsNaN(r) || r < 0 ? 0.0 : (r > 1.0 ? 1.0 : r); }

        /// <summary>Staly skrot nazwy (nie string.GetHashCode - ten bywa losowany na proces): doba rodu / miasta w cyklu 28 dob ta sama po wczytaniu.</summary>
        private static int SHash(string x)
        {
            unchecked { uint h = 2166136261; if (x != null) for (int i = 0; i < x.Length; i++) { h ^= x[i]; h *= 16777619; } return (int)(h & 0x7fffffff); }
        }
        private static bool MyDay(string id, int today) { return ((SHash(id) % FundCycle) + today) % FundCycle == 0 || (_carry && ((SHash(id) % FundCycle) + today - 1) % FundCycle == 0); }   // 435-L7-p: _carry - doba przeniesiona z pierwszej doby sesji

        private static Village VillageOf(string dist)
        {
            if (dist == null) return null;
            var m = _vilById;
            if (m == null)
            {
                m = new Dictionary<string, Village>(StringComparer.Ordinal);
                foreach (var v in Village.All) if (v != null && v.Settlement != null && v.Settlement.StringId != null) m[v.Settlement.StringId] = v;
                _vilById = m;
            }
            Village r; return m.TryGetValue(dist, out r) ? r : null;
        }

        /// <summary>Kategorie wszystkich towarow zajecia (tabela P0 KS) - miasto-fundator: czy towar zajecia jest brakujacym wsadem jego warsztatow.</summary>
        private static ItemCategory[] CatsOfCraft(int c)
        {
            if (c < 0 || c >= KsTab.Crafts.Length || c >= KsTab.P0.Length) return null;
            if (_catsOf == null) _catsOf = new ItemCategory[KsTab.Crafts.Length][];
            if (_catsOf[c] != null) return _catsOf[c];
            var l = new List<ItemCategory>();
            try
            {
                var row = KsTab.P0[c];
                if (row != null && MBObjectManager.Instance != null)
                    foreach (var kv in row)
                    {
                        if (kv.Key < 0 || kv.Key >= KsTab.Goods.Length) continue;
                        var it = MBObjectManager.Instance.GetObject<ItemObject>(KsTab.Goods[kv.Key]);
                        if (it != null && it.ItemCategory != null && !l.Contains(it.ItemCategory)) l.Add(it.ItemCategory);
                    }
            }
            catch { }
            _catsOf[c] = l.ToArray();
            return _catsOf[c];
        }

        private static bool AtWar(IFaction a, IFaction b)
        {
            try { return a != null && b != null && a != b && FactionManager.IsAtWarAgainstFaction(a, b); } catch { return false; }
        }

        /// <summary>Strona wierzytelnosci: rod (glowa rodu) albo miasto. 1 - jest; 0 - fundatora juz nie ma (rod wymarl, osada nie jest miastem); 2 - rod zyje, ale
        /// chwilowo bez zywej glowy (435-L7-p: wierzytelnosc ZOSTAJE, czynsz wstrzymany do nowej glowy rodu - stan przejsciowy nie kasuje wpisu na stale).</summary>
        private static int Funder(string who, out Hero leader, out Settlement town, out IFaction faction)
        {
            leader = null; town = null; faction = null;
            if (string.IsNullOrEmpty(who) || who.Length < 3) return 0;
            string id = who.Substring(2);
            if (who[0] == 'c')
            {
                var clan = DistrictWorks.ClanOf(id);
                if (clan == null || clan.IsEliminated) return 0;
                faction = clan.MapFaction;
                if (clan.Leader == null || !clan.Leader.IsAlive) return 2;
                leader = clan.Leader;
                return 1;
            }
            if (who[0] == 't')
            {
                var st = Settlement.Find(id);
                if (st == null || !st.IsTown || st.Town == null) return 0;
                town = st; faction = st.MapFaction;
                return 1;
            }
            return 0;
        }

        // ============================================================ plan zakupu okregu (brakujace jednostki)
        private sealed class Plan
        {
            public readonly List<Rec> Recs = new List<Rec>(); public readonly List<double> Units = new List<double>();
            public double Mat, Price, N = 1.0, Worst; public int Kind = -1;
        }

        /// <summary>Rece bez sprzetu czesci okregu z ostatniej doby (po zakupach gminy tej doby). `mMax` &lt; 1 - tylko czesci z m ponizej; `cats` - tylko zajecia z towarem
        /// z tego zbioru. Pod Gate. null - okreg bez wpisu.</summary>
        private static Plan PlanOf(Village v, double mMax, HashSet<ItemCategory> cats)
        {
            List<Rec> list;
            if (v == null || v.Settlement == null || v.Settlement.StringId == null || !_byDist.TryGetValue(v.Settlement.StringId, out list)) return null;
            var p = new Plan();
            foreach (var r in list)
            {
                if (r.K < 0 || !r.Path || !(r.Need > 1e-6) || !(r.Unit > 1e-6) || !(r.UnitMat > 1e-6) || r.Unit < r.UnitMat) continue;
                if (mMax < 1.0 && !(MAt(r.Craft, r.Ratio) < mMax)) continue;
                if (cats != null)
                {
                    var cc = CatsOfCraft(r.Craft); bool hit = false;
                    if (cc != null) for (int i = 0; i < cc.Length; i++) if (cats.Contains(cc[i])) { hit = true; break; }
                    if (!hit) continue;
                }
                p.Recs.Add(r); p.Units.Add(r.Need);
                p.Mat += r.Need * r.UnitMat; p.Price += r.Need * r.Unit;
                double n = NOfCraft(r.Craft); if (n > p.N) p.N = n;
                if (r.Need > p.Worst && r.Craft >= 0 && r.Craft < KsTab.Crafts.Length) { int k = DistrictWorks.KindOfCraft(KsTab.Crafts[r.Craft]); if (k >= 0) { p.Worst = r.Need; p.Kind = k; } }
            }
            return p;
        }

        private static double WageK()
        {
            var s = Settings.Current;
            double k = s != null ? (double)s.DsWorksWagePerMaterial : 0.5;
            return double.IsNaN(k) ? 0.5 : Math.Max(0.0, Math.Min(3.0, k));
        }

        /// <summary>Zakup fundatora w okregu `v` do kwoty `cap`: material z polki `mk`, rzemieslnicy, praca wsi; jednostki do sprzetu czesci, wklad do wierzytelnosci.
        /// Zwraca wklad (0 - nic; `why`: WNoNeed, WSmall albo WPay + powod TownFolk.Gf*). Watek glowny, nie rzuca.</summary>
        private static long FundBuy(int kind, string who, Hero payer, Village v, Settlement mk, double cap, double mMax, HashSet<ItemCategory> cats, bool outside,
                                    out double units, out TownFolk.GearPay pay, out int why)
        {
            units = 0; pay = null; why = WNoNeed;
            try
            {
                if (_main >= 0 && Environment.CurrentManagedThreadId != _main) { _dOff++; return 0; }
                Plan p;
                lock (Gate) p = PlanOf(v, mMax, cats);
                if (p == null || p.Recs.Count == 0 || !(p.Mat >= 1.0)) return 0;
                double k = WageK();
                // praca wsi na zlotowke materialu: reszta ceny jednostki po materiale i rzemieslnikach - tylko przy kluczu Village Gear Funder Pays Village Work
                // (435-L7-p: domyslnie fundator placi jak gmina - material + rzemieslnicy; moneta "pracy wsi" wracala do pana danina od skarbca, a czynsz liczyl sie i od niej)
                double wr = PaysWork ? Math.Max(0.0, p.Price / p.Mat - (1.0 + k)) : 0.0;
                double matBudget = Math.Min(p.Mat, cap / (1.0 + k + wr));
                if (!(matBudget >= 1.0)) { why = WSmall; return 0; }
                pay = TownFolk.GearFundPay(payer, v, mk, matBudget, k, wr, outside);
                int today = Today();
                lock (Gate)
                {
                    if (pay.Why >= 0 && pay.Why < _fWhy.Length) _fWhy[pay.Why]++;
                    // 435-L7-p (recenzje N7 / stab. 4): wyjatek PO zdjeciu sztuk z polki (GfErr z materialem) - jednostki i wierzytelnosc za to, co naprawde zaplacone
                    if ((pay.Why != TownFolk.GfOk && !(pay.Why == TownFolk.GfErr && pay.Mat > 0)) || pay.Mat <= 0) { why = WPay + pay.Why; return 0; }
                    double share = Math.Min(1.0, pay.Mat / p.Mat), price = share * p.Price;
                    string dist = v.Settlement.StringId;
                    List<Claim> cl;
                    if (!_claims.TryGetValue(dist, out cl)) { cl = new List<Claim>(); _claims[dist] = cl; }
                    for (int i = 0; i < p.Recs.Count; i++)
                    {
                        var r = p.Recs[i];
                        double dk = p.Units[i] * share;
                        if (!(dk > 0)) continue;
                        r.K += dk; r.Need = Math.Max(0.0, r.Need - dk); units += dk;
                        double g = price > 0 ? pay.Paid * (dk * r.Unit) / price : 0.0;
                        if (!(g > 0)) continue;
                        Claim c = null;
                        foreach (var x in cl) if (x.Who == who && x.Part == r.Part) { c = x; break; }
                        if (c == null) { c = new Claim { Who = who, Dist = dist, Part = r.Part, Day = today }; cl.Add(c); _claimN++; }
                        c.G += g; _gIn += g;
                    }
                    _dFundU += units;
                    _fU[kind] += units; _fPaid[kind] += pay.Paid; _fMat[kind] += pay.Mat; _fWageFolk[kind] += pay.WageFolk; _fWageCof[kind] += pay.WageCof; _fWork[kind] += pay.Work;
                    _fN[kind]++; _fPcs[kind] += pay.Pcs; _fGap += pay.Gap;
                    if (pay.Drop != pay.Pcs) _fPcsBad++;
                }
                why = 0;
                return pay.Paid;
            }
            catch (Exception e) { Stumble7("FundBuy", e); return 0; }
        }

        // ============================================================ doba (po zleceniach robot gminy)
        /// <summary>ArmouryBehavior.OnDailyTick zaraz po TownFolk.DaWorksRun: czynsz doby (naliczenie, przepadek, gasniecie wierzytelnosci), fundatorzy AI (panowie,
        /// miasta), komunikaty ognia dla gracza, linia "LP czynsz". Klucz Village Gear wylaczony - nic (stan zamrozony). Nie rzuca.</summary>
        internal static void FundDaily()
        {
            var s = Settings.Current;
            if (s == null || Campaign.Current == null) return;
            int today = Today();
            if (_fundDay == today) return;
            _fundDay = today;
            if (!s.VillageGear || !VillageBook.Active) return;
            if (_main >= 0 && Environment.CurrentManagedThreadId != _main) { _dOff++; return; }
            bool lords = s.VillageGearLordsInvest, towns = s.VillageGearTownsInvest;
            if (!lords && !towns && !s.VillageGearPlayerFunds && _claimN == 0) return;   // nic czynnego i zadnej wierzytelnosci - bez linii (jak przed 435-L7)
            long t0 = Stopwatch.GetTimestamp();
            try { ClaimsDay(s, today); } catch (Exception e) { Stumble7("ClaimsDay", e); }
            // 435-L7-p (stabilnosc 5): pierwsza doba sesji - potrzeby i ceny jednostek to pamiec doby (licza sie w BeforeProduction, PO tym kroku); rody i miasta
            // tej doby nie traca cyklu 28 dob: ich doba przechodzi na jutro
            bool ready; lock (Gate) ready = _valuedAll;
            if (!ready && (lords || towns)) { _fundWait = 1; _carryNext = true; }
            else
            {
                _carry = _carryNext; _carryNext = false;
                try
                {
                    if (lords) { try { FundLords(s, today); } catch (Exception e) { Stumble7("FundLords", e); } }
                    if (towns) { try { FundTowns(s, today); } catch (Exception e) { Stumble7("FundTowns", e); } }
                }
                finally { _carry = false; }
            }
            try
            {
                string[] msgs = null;
                lock (Gate) { if (_burnMsg.Count > 0) { msgs = _burnMsg.ToArray(); _burnMsg.Clear(); } }
                if (msgs != null) foreach (var m in msgs) Log.Player(m, true);
            }
            catch (Exception e) { Stumble7("BurnMsg", e); }
            _ms7 = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
            try { Line7(today, s); } catch (Exception e) { Stumble7("Line7", e); }
        }

        /// <summary>Czynsz doby: naliczenie (wies Normal, fundator nie wojuje z krolestwem wsi), przepadek naleznego ponad 28 dob czynszu, gasniecie wierzytelnosci wedlug
        /// tabeli zuzycia czesci; fundator, ktorego juz nie ma - wierzytelnosc znika (sprzet zostaje gminie).</summary>
        private static void ClaimsDay(Settings s, int today)
        {
            if (_claimN == 0) return;
            double rate = RentRate(s) / 364.0;
            var known = new Dictionary<string, IFaction>(StringComparer.Ordinal);
            var gone = new HashSet<string>(StringComparer.Ordinal);
            var wait = new HashSet<string>(StringComparer.Ordinal);   // 435-L7-p: rod chwilowo bez glowy - czynsz wstrzymany, wpis zostaje
            lock (Gate)
            {
                foreach (var kv in _claims)
                {
                    var cl = kv.Value;
                    if (cl == null || cl.Count == 0) continue;
                    try
                    {
                        Village v = VillageOf(kv.Key);
                        List<Rec> recs; _byDist.TryGetValue(kv.Key, out recs);
                        bool normal = v != null && v.VillageState == Village.VillageStates.Normal;
                        IFaction vf = v != null && v.Settlement != null ? v.Settlement.MapFaction : null;
                        for (int i = cl.Count - 1; i >= 0; i--)
                        {
                            var c = cl[i];
                            IFaction ff;
                            if (!known.TryGetValue(c.Who, out ff) && !gone.Contains(c.Who) && !wait.Contains(c.Who))
                            {
                                Hero h; Settlement t;
                                int fs = Funder(c.Who, out h, out t, out ff);
                                if (fs == 1) known[c.Who] = ff; else if (fs == 2) wait.Add(c.Who); else gone.Add(c.Who);
                            }
                            if (gone.Contains(c.Who) || v == null) { _gDead += c.G; _accGone += c.Acc; cl.RemoveAt(i); _claimN--; continue; }
                            double day = c.G * rate;
                            if (normal && !wait.Contains(c.Who) && !AtWar(ff, vf)) { c.Acc += day; _rAcc += day; } else _rHeld++;
                            double max = RentKeepDays * day;
                            if (c.Acc > max) { _rLapsed += c.Acc - max; c.Acc = max; }
                            Rec r = recs != null ? Find(recs, c.Part) : null;
                            double wear = r != null ? r.WearNG + r.WearGr : 0.0;
                            if (!(wear > 0)) wear = ClaimWearDefault;
                            double d = c.G * (1.0 - Math.Pow(1.0 - Math.Min(WearMaxYear, wear), 1.0 / 364.0));
                            if (d > 0 && !double.IsNaN(d)) { c.G -= d; _gDecay += d; }
                            if (c.G < 1.0 && c.Acc < 1.0) { _gSmall += c.G; _accGone += c.Acc; cl.RemoveAt(i); _claimN--; }
                        }
                    }
                    catch (Exception e) { _st7++; _st7All++; if (_err.Add("L7.ClaimsDay(okreg)")) { try { Log.Error("VillageGear.Fund.ClaimsDay(okreg)", e); } catch { } } }
                }
            }
        }

        /// <summary>BeforeProduction (pod Gate): ogien czesci zabral `fr` jej sprzetu - wierzytelnosci tej czesci maleja o te sama czesc; gracz dostaje wiesc.</summary>
        private static void FundBurn(Rec rec, double fr)
        {
            if (_claimN == 0 || rec == null || !(fr > 0)) return;
            List<Claim> cl;
            if (!_claims.TryGetValue(rec.Dist, out cl)) return;
            if (fr > 1.0) fr = 1.0;
            string pid = null;
            foreach (var c in cl)
            {
                if (c.Part != rec.Part || !(c.G > 0)) continue;
                double d = c.G * fr;
                c.G -= d; _gBurn += d;
                if (pid == null) { try { pid = Clan.PlayerClan != null ? "c:" + Clan.PlayerClan.StringId : ""; } catch { pid = ""; } }
                if (c.Who == pid && _burnMsg.Count < 8)
                {
                    string name = "a village";
                    try { var v = VillageOf(rec.Dist); if (v != null && v.Name != null) name = v.Name.ToString(); } catch { }
                    _burnMsg.Add("Raiders burned " + Math.Round(100.0 * fr).ToString("0", Inv) + "% of the gear you funded at " + name + ".");
                }
            }
        }

        /// <summary>EndCore (pod Gate): koniec zmiany zajecia odstawil `fr` sprzetu czesci (bezczynny albo odpisany). 435-L7-p: wierzytelnosci fundatorow tej czesci
        /// maleja o te sama czesc (czynsz "dopoki sprzet pracuje").</summary>
        private static void FundShift(Rec rec, double fr)
        {
            if (_claimN == 0 || rec == null || !(fr > 0)) return;
            List<Claim> cl;
            if (!_claims.TryGetValue(rec.Dist, out cl)) return;
            if (fr > 1.0) fr = 1.0;
            foreach (var c in cl)
            {
                if (c.Part != rec.Part || !(c.G > 0)) continue;
                double d = c.G * fr;
                c.G -= d; _gShift += d;
            }
        }

        // ------------------------------------------------------------ pan wsi (AI)
        private static double RingSum(int today)
        {
            double x = 0;
            for (int i = 0; i < FundCycle; i++) if (_ringDay[i] != int.MinValue && today - _ringDay[i] >= 0 && today - _ringDay[i] < FundCycle) x += _ring[i];
            return x;
        }

        private static void RingAdd(int today, double x)
        {
            int k = ((today % FundCycle) + FundCycle) % FundCycle;
            if (_ringDay[k] != today) { _ringDay[k] = today; _ring[k] = 0; }
            _ring[k] += x;
        }

        private static void FundLords(Settings s, int today)
        {
            if (!TownFolk.DaWorksOn) return;
            double surplusShare = Math.Max(0f, Math.Min(1f, s.DistrictWorksAiSurplusShare));
            int resDays = Math.Max(0, s.DistrictWorksAiReserveDays), resFlat = Math.Max(0, s.DistrictWorksAiReserveFlat);
            double pace = Math.Max(0f, Math.Min(0.1f, s.DistrictWorksAiWorldPace));
            double world = 0, ring;
            lock (Gate)
            {
                foreach (var l in _byDist.Values) foreach (var r in l) if (r.K >= 0 && r.Path && r.VPer > 0 && r.Hs > 0) world += r.VPer * r.Hs;
                ring = RingSum(today);
            }
            world *= 364.0;
            _lWorld = world;
            // 435-L7-p (stabilnosc 6): okno tempa (28 dob) = cykl rodow (28 dob), wiec caly pulap bralyby zawsze te same rody; dzienna czesc pulapu (2 / 28) daje kolej
            // rodom kazdej doby, a suma okna dalej nie przekracza pulapu
            double budget = Math.Min(pace * world - ring, 2.0 * pace * world / FundCycle), spent = 0;
            foreach (var clan in Clan.All)
            {
                try
                {
                    if (clan == null || clan.StringId == null || clan == Clan.PlayerClan || clan.IsEliminated || clan.Leader == null || !clan.Leader.IsAlive
                        || clan.Fiefs == null || clan.Fiefs.Count == 0) continue;
                    if (!MyDay(clan.StringId, today)) continue;
                    _lClans++;
                    if (budget - spent < FundMinGold) { _lBudget++; continue; }
                    var leader = clan.Leader;
                    double pr; int ms, dd; bool df;
                    if (IronBank.TryGetDebt(clan, out pr, out ms, out df, out dd)) { _lBank++; continue; }
                    if (DebtLadder.CrownLoanOf(clan) > 0) { _lCrown++; continue; }
                    if (DebtLadder.FolkLoanBlocked(clan)) { _lBlocked++; continue; }
                    // 435-L7-p (stabilnosc 1): kazdy dluznik drabiny (dlug zoldu, dlug wobec korony inny niz 431, pozyczka rady / ludu) - pozyczka nie finansuje inwestycji
                    if (DebtLadder.On && DebtLadder.IsDebtor(clan)) { _lDebtor++; continue; }
                    double f, G, R;
                    int band = KingdomTreasury.NeedOfClan(clan, out f, out G, out R);
                    if (band == 0 || band == 1) { _lNeed++; continue; }        // rod w potrzebie (kiesa ponizej 2R) dostaje doplate / pozyczke na zold - nie funduje
                    if (band == 3 && ClanBudget.On) { _lNoMeasure++; continue; }   // 435-L7-p: budzet 166 czynny, a rod bez miary (dluznik, zajecie, bez budzetu) - nie funduje
                    int wages = 0; try { var pp = leader.PartyBelongedTo; wages = pp != null ? Math.Max(0, pp.TotalWage) : 0; } catch { wages = 0; }
                    long surplus = (long)leader.Gold - ((long)resDays * wages + resFlat);
                    double want = surplusShare * surplus;
                    if (want < FundMinGold) { _lNoSurplus++; continue; }
                    // wlasne wsie z rekami bez sprzetu, najpierw najwyzszy niedobor N towaru zajecia
                    var cand = new List<KeyValuePair<double, Village>>();
                    foreach (var fief in clan.Fiefs)
                    {
                        if (fief == null || fief.Settlement == null || fief.Settlement.BoundVillages == null) continue;
                        foreach (var v in fief.Settlement.BoundVillages)
                        {
                            if (v == null || v.Settlement == null || v.VillageState != Village.VillageStates.Normal) continue;
                            Plan p; lock (Gate) p = PlanOf(v, FundMMax, null);
                            if (p == null || !(p.Mat >= 1.0)) continue;
                            cand.Add(new KeyValuePair<double, Village>(p.N, v));
                        }
                    }
                    if (cand.Count == 0) continue;
                    cand.Sort((a, b) => b.Key.CompareTo(a.Key));
                    string who = "c:" + clan.StringId;
                    foreach (var kv in cand)
                    {
                        double cap = Math.Min(want, budget - spent);
                        if (cap < FundMinGold) break;
                        var v = kv.Value;
                        var mk = DistrictWorks.MarketOf(v);
                        if (mk == null || mk.Town == null) continue;
                        if (AtWar(clan.MapFaction, mk.MapFaction)) { _lWarMk++; continue; }
                        _lVil++;
                        double u; TownFolk.GearPay pay; int why;
                        long paid = FundBuy(FLord, who, leader, v, mk, cap, FundMMax, null, false, out u, out pay, out why);
                        if (paid <= 0) continue;
                        spent += paid; want -= paid;
                        lock (Gate) RingAdd(today, paid);
                    }
                }
                catch (Exception e) { Stumble7("FundLords(rod)", e); }
            }
            _lPace = world > 0 ? (ring + spent) / world : 0;
        }

        // ------------------------------------------------------------ miasto targowe (kupcy)
        private static void FundTowns(Settings s, int today)
        {
            if (!TownFolk.DaWorksOn) return;
            var table = SupplySignal.Table;
            Dictionary<Settlement, List<Village>> byTown = null;
            foreach (var st in Settlement.All)
            {
                try
                {
                    if (st == null || !st.IsTown || st.Town == null || st.StringId == null) continue;
                    if (!MyDay(st.StringId, today)) continue;
                    _tLooked++;
                    if (st.IsUnderSiege) { _tSiege++; continue; }
                    if (KsFoodCredit.VillageOpenOf(st) > 0) { _tKwit++; continue; }           // wies przed panem (428): najpierw rachunki wsi
                    if (TownDebt.ArrearsLike(st) >= 1.0) { _tDebt++; continue; }
                    long reserve = TownPurse.Reserve(st.Town);
                    if (st.Town.Gold < TownCofferTimes * reserve) { _tCoffer++; continue; }
                    double budget = TownSurplusShare * Math.Max(0L, (long)st.Town.Gold - reserve);
                    if (budget < FundMinGold) { _tCoffer++; continue; }
                    if (table == null) { _tNoTable++; continue; }
                    // towary, ktore sa wsadem warsztatow tego miasta i ktorych w nim brakuje (N > 1)
                    HashSet<ItemCategory> shortIn = null;
                    var ws = st.Town.Workshops;
                    if (ws != null)
                        foreach (var w in ws)
                        {
                            if (w == null || w.WorkshopType == null || w.WorkshopType.Productions == null) continue;
                            foreach (var p in w.WorkshopType.Productions)
                            {
                                if (p.Inputs == null) continue;
                                foreach (var x in p.Inputs)
                                {
                                    if (x.Item1 == null || !(SupplySignal.TownN(table, st, x.Item1) > 1.0)) continue;
                                    if (shortIn == null) shortIn = new HashSet<ItemCategory>();
                                    shortIn.Add(x.Item1);
                                }
                            }
                        }
                    if (shortIn == null) { _tNoShort++; continue; }
                    if (byTown == null)
                    {
                        byTown = new Dictionary<Settlement, List<Village>>();
                        foreach (var v in Village.All)
                        {
                            if (v == null || v.Settlement == null) continue;
                            var tb = v.TradeBound;
                            if (tb == null || !tb.IsTown) continue;
                            List<Village> l; if (!byTown.TryGetValue(tb, out l)) { l = new List<Village>(); byTown[tb] = l; }
                            l.Add(v);
                        }
                    }
                    List<Village> vs;
                    if (!byTown.TryGetValue(st, out vs)) continue;
                    string who = "t:" + st.StringId;
                    double spent = 0;
                    foreach (var v in vs)
                    {
                        double cap = budget - spent;
                        if (cap < FundMinGold) break;
                        if (v.VillageState != Village.VillageStates.Normal || AtWar(st.MapFaction, v.Settlement.MapFaction)) continue;
                        Plan p; lock (Gate) p = PlanOf(v, FundMMax, shortIn);
                        if (p == null || !(p.Mat >= 1.0)) continue;
                        _tVil++;
                        double u; TownFolk.GearPay pay; int why;
                        long paid = FundBuy(FTown, who, null, v, st, cap, FundMMax, shortIn, false, out u, out pay, out why);
                        // budzet miasta liczy monete z kasy (place i praca wsi) - material to wlasny towar kupcow
                        if (paid > 0 && pay != null) spent += pay.WageFolk + pay.Work;
                    }
                }
                catch (Exception e) { Stumble7("FundTowns(miasto)", e); }
            }
        }

        // ============================================================ czynsz przy powrocie wozu
        /// <summary>VillageTakings.EnterPostfix (woz wrocil do wsi macierzystej; po wyplacie prac okregu, przed danina DS): `grown` - przyrost kiesy wsi w oknie (czesc wsi
        /// utargu po pracach okregu). Nalezny czynsz okregu: najwyzej polowa `grown`, najwyzej kiesa wsi, a przy uldze pana (Ds Lord Relief Below Chest) tylko z kiesy
        /// ponad skrzynie (inaczej czynsz placilby pan z ulgi). Kiesa wsi -> glowa rodu fundatora / kasa miasta fundatora. Zwraca zaplacone. Nie rzuca.</summary>
        internal static long RentAtCart(MobileParty cart, Village home, long grown)
        {
            if (_claimN == 0 || home == null || home.Settlement == null || grown <= 0 || !On) return 0;
            long paidAll = 0;
            try
            {
                if (_main >= 0 && Environment.CurrentManagedThreadId != _main) { _dOff++; return 0; }
                if (home.VillageState != Village.VillageStates.Normal) return 0;
                string id = home.Settlement.StringId;
                var by = new Dictionary<string, double>(StringComparer.Ordinal);
                double due = 0;
                lock (Gate)
                {
                    List<Claim> cl;
                    if (!_claims.TryGetValue(id, out cl) || cl.Count == 0) return 0;
                    foreach (var c in cl) { if (!(c.Acc > 0)) continue; double x; by.TryGetValue(c.Who, out x); by[c.Who] = x + c.Acc; due += c.Acc; }
                }
                if (!(due >= 1.0)) return 0;
                long cap = Math.Min((long)Math.Floor(due), Math.Max(0, home.Gold));
                long half = grown / 2;
                if (cap > half) { lock (Gate) _rHalf += cap - half; cap = half; }
                var s = Settings.Current;
                if (s != null && s.DsLordReliefBelowChest && DsVillage.On(home.Settlement))
                {
                    double chest = DsVillage.ReliefChest(home.Settlement);
                    if (chest >= 1.0)
                    {
                        long free = Math.Max(0L, (long)home.Gold - (long)Math.Ceiling(chest));
                        if (cap > free) { lock (Gate) _rChest += cap - free; cap = free; }
                    }
                }
                if (cap < 1) return 0;
                IFaction vf = home.Settlement.MapFaction;
                foreach (var kv in by)
                {
                    try
                    {
                        long want = (long)Math.Floor(cap * kv.Value / due);
                        want = Math.Min(want, Math.Max(0, home.Gold));
                        if (want < 1) continue;
                        Hero leader; Settlement town; IFaction ff;
                        if (Funder(kv.Key, out leader, out town, out ff) != 1) continue;              // fundatora nie ma (wierzytelnosc zdejmie doba) albo rod chwilowo bez glowy (czeka)
                        if (AtWar(ff, vf)) { lock (Gate) _rWarCart++; continue; }
                        int had = home.Gold; long got, inn;
                        if (leader != null)
                        {
                            int h0 = leader.Gold;
                            GiveGoldAction.ApplyForSettlementToCharacter(home.Settlement, leader, (int)want, true);   // kiesa wsi -> glowa rodu (zdarzenie gry, jak prace okregu)
                            got = Math.Max(0L, (long)had - home.Gold); inn = (long)leader.Gold - h0;
                            GoldForecast.NoteWorksCart(leader, got);                                 // ksiega kiesy gracza (wyplata poza taktem doby; tylko gracz)
                        }
                        else
                        {
                            home.ChangeGold(-(int)want);
                            got = Math.Max(0L, (long)had - home.Gold);
                            int c0 = town.Town.Gold;
                            if (got > 0) town.Town.ChangeGold((int)got);
                            inn = (long)town.Town.Gold - c0;
                            if (got > 0) MoneyLedger.Note169(MoneyLedger.N169Other, town, (int)got);   // kasa miasta-fundatora poza tickiem - nazwane ("inne")
                        }
                        if (got <= 0) continue;
                        MoneyLedger.CartWorksInWindow(cart, (int)got, 0);                              // okno powrotu wozu: kiesa wsi z utargu brutto (czynsz nie jest "zniklo")
                        MoneyLedger.Note169(MoneyLedger.N169Other, home.Settlement, -(int)got);        // kiesa wsi poza tickiem - nazwane ("inne")
                        StartCalibration.KsVilOut(home.Settlement, got);                               // 435-L7-p: staly wydatek wsi spoza koszyka (jak danina DS) - alfa netto KS
                        paidAll += got;
                        lock (Gate)
                        {
                            _rPaid += got; _rOut += got; _rIn += inn;
                            List<Claim> cl;
                            if (_claims.TryGetValue(id, out cl) && kv.Value > 0)
                                foreach (var c in cl)
                                {
                                    if (c.Who != kv.Key || !(c.Acc > 0)) continue;
                                    double x = got * c.Acc / kv.Value;
                                    c.PaidAll += x; c.Acc = Math.Max(0.0, c.Acc - x);
                                }
                        }
                    }
                    catch (Exception e) { Stumble7("RentAtCart(fundator)", e); }
                }
                if (paidAll > 0) lock (Gate) _rPaidN++;
            }
            catch (Exception e) { Stumble7("RentAtCart", e); }
            return paidAll;
        }

        // ============================================================ gracz (menu wsi)
        /// <summary>ArmouryBehavior.OnSessionLaunched (za DistrictWorks.AddMenus): pozycja w menu wsi. Przy kluczu Village Gear Player Funds NIE - ukryta (menu jak dotad).</summary>
        internal static void AddMenus(CampaignGameStarter starter)
        {
            try
            {
                starter.AddGameMenuOption("village", "arm_gear_fund", "{=!}Fund gear for this village's hands", FundCondition, FundConsequence, false, 9);
                _menu7 = true;
            }
            catch (Exception e) { _menu7 = false; Log.Error("VillageGear.AddMenus", e); }
        }

        private sealed class PQuote { public double Units; public long Gold, Mat, Rent; public string Kind, Town, Why; public Settlement Mk; }

        private static PQuote QuoteFor(Village v)
        {
            var q = new PQuote();
            var s = Settings.Current;
            if (v == null || v.Settlement == null || s == null) { q.Why = "Not now."; return q; }
            if (AtWar(Clan.PlayerClan != null ? Clan.PlayerClan.MapFaction : null, v.Settlement.MapFaction)) { q.Why = "You are at war with this village's realm."; return q; }
            if (v.VillageState != Village.VillageStates.Normal) { q.Why = "The village has been raided."; return q; }
            Plan p; lock (Gate) p = PlanOf(v, 1.0, null);
            // 435-L7-p: cena dla gracza ta sama co dla panow - przy kluczu Village Gear Funder Pays Village Work pelna cena jednostki, bez niego material + rzemieslnicy
            double full = p != null ? (PaysWork ? p.Price : Math.Min(p.Price, p.Mat * (1.0 + WageK()))) : 0.0;
            if (p == null || !(p.Mat >= 1.0) || !(full >= FundMinGold)) { q.Why = "Every hand here already has gear."; return q; }
            q.Mk = DistrictWorks.MarketOf(v);
            q.Town = q.Mk != null && q.Mk.Name != null ? q.Mk.Name.ToString() : "the market town";
            if (q.Mk == null || q.Mk.Town == null || !TownFolk.DaWorksOn) { q.Why = "No market town will sell the materials."; return q; }
            // 435-L7-p (recenzje N6 / stab. 7): jedna regula dla trzech fundatorow - bez zakupu u kupcow miasta, z ktorym gracz wojuje
            if (AtWar(Clan.PlayerClan != null ? Clan.PlayerClan.MapFaction : null, q.Mk.MapFaction)) { q.Why = "No market town will sell the materials - you are at war with " + q.Town + "."; return q; }
            double share = Math.Min(1.0, Math.Max(0, Hero.MainHero != null ? Hero.MainHero.Gold : 0) / full);
            double units = 0; foreach (var u in p.Units) units += u;
            q.Units = units * share; q.Gold = (long)Math.Floor(full * share); q.Mat = (long)Math.Floor(p.Mat * share);
            q.Rent = (long)Math.Floor(q.Gold * RentRate(s));
            q.Kind = p.Kind >= 0 ? DistrictWorks.KindName[p.Kind] : "gear";
            if (q.Gold < FundMinGold) q.Why = "You need at least " + FundMinGold.ToString("N0", Inv) + " denars.";
            return q;
        }

        private static string QuoteText(PQuote q)
        {
            return "Pay for " + Math.Max(1.0, Math.Round(q.Units)).ToString("N0", Inv) + " sets of " + q.Kind + ": " + q.Gold.ToString("N0", Inv) + "{GOLD_ICON} (" + q.Mat.ToString("N0", Inv)
                   + " for materials from " + q.Town + (PaysWork ? ", the rest to craftsmen and villagers" : ", the rest to its craftsmen") + "). The village pays you " + q.Rent.ToString("N0", Inv) + "{GOLD_ICON} a year while the gear lasts.";
        }

        private static bool FundCondition(MenuCallbackArgs args)
        {
            try
            {
                args.optionLeaveType = GameMenuOption.LeaveType.Trade;
                var st = Settlement.CurrentSettlement;
                if (st == null || !st.IsVillage || st.Village == null || !PlayerOn || !VillageBook.Active) return false;
                var q = QuoteFor(st.Village);
                if (q.Why != null) { args.IsEnabled = false; args.Tooltip = new TextObject("{=!}" + q.Why); return true; }
                args.Tooltip = new TextObject("{=!}" + QuoteText(q));
                return true;
            }
            catch { return false; }
        }

        private static void FundConsequence(MenuCallbackArgs args)
        {
            try
            {
                var st = Settlement.CurrentSettlement;
                if (st == null || st.Village == null || !PlayerOn) return;
                var q = QuoteFor(st.Village);
                if (q.Why != null) { Log.Player(q.Why, true); return; }
                var v = st.Village;
                string text = new TextObject("{=!}" + QuoteText(q)).ToString();
                InformationManager.ShowInquiry(new InquiryData("Fund gear for this village's hands", text, true, true, "Pay", "Back", delegate { PlayerFund(v); }, null), true);
            }
            catch (Exception e) { Stumble7("FundConsequence", e); }
        }

        private static void PlayerFund(Village v)
        {
            try
            {
                var me = Hero.MainHero;
                if (v == null || me == null || Clan.PlayerClan == null || !PlayerOn) return;
                var q = QuoteFor(v);
                if (q.Why != null) { Log.Player(q.Why, true); return; }
                double u; TownFolk.GearPay pay; int why;
                long paid = FundBuy(FPlayer, "c:" + Clan.PlayerClan.StringId, me, v, q.Mk, Math.Min((double)q.Gold, Math.Max(0, me.Gold)), 1.0, null, true, out u, out pay, out why);
                string name = v.Name != null ? v.Name.ToString() : "The village";
                if (paid <= 0)
                {
                    if (why == WPay + TownFolk.GfNoShelf || why == WPay + TownFolk.GfPriceCap) Log.Player(q.Town + " has no building materials on its shelves.", true);
                    else if (why == WPay + TownFolk.GfNoGold) Log.Player("You cannot pay for it.", true);
                    else Log.Player("Nothing could be bought for " + name + " now.", true);
                    return;
                }
                long rent = (long)Math.Floor(paid * RentRate(Settings.Current));
                Log.Player(name + " has new " + q.Kind + " (" + Math.Round(u).ToString("N0", Inv) + " sets for " + paid.ToString("N0", Inv) + " denars). Rent: " + rent.ToString("N0", Inv) + " denars a year.");
            }
            catch (Exception e) { Stumble7("PlayerFund", e); }
        }

        /// <summary>DistrictMenu (naglowek okna okregu): wierzytelnosc gracza w tym okregu; null - brak. Nie rzuca.</summary>
        internal static string FundHeadText(Settlement st)
        {
            try
            {
                if (_claimN == 0 || st == null || st.StringId == null || Clan.PlayerClan == null || !On) return null;
                string pid = "c:" + Clan.PlayerClan.StringId;
                double g = 0, acc = 0, paid = 0;
                lock (Gate)
                {
                    List<Claim> cl;
                    if (!_claims.TryGetValue(st.StringId, out cl)) return null;
                    foreach (var c in cl) if (c.Who == pid) { g += c.G; acc += c.Acc; paid += c.PaidAll; }
                }
                if (!(g >= 1.0) && !(acc >= 1.0)) return null;
                return "Gear you funded here: " + Math.Round(g).ToString("N0", Inv) + " denars still standing, rent " + Math.Round(g * RentRate(Settings.Current)).ToString("N0", Inv)
                       + " a year (" + Math.Round(paid).ToString("N0", Inv) + " paid so far, " + Math.Round(acc).ToString("N0", Inv) + " owed).";
            }
            catch (Exception e) { Stumble7("FundHeadText", e); return null; }
        }

        // ============================================================ linia doby
        private static void Line7(int today, Settings s)
        {
            double gAll = 0; int n = 0; var gk = new double[FKinds]; double accAll = 0;
            double overG = 0; int overN = 0;                                  // 435-L7-p: wierzytelnosci czesci ponad wartosc jej sprzetu (K x cena jednostki) - pomiar
            string pid = null; try { pid = Clan.PlayerClan != null ? "c:" + Clan.PlayerClan.StringId : null; } catch { }
            lock (Gate)
                foreach (var cl in _claims.Values)
                {
                    Dictionary<string, double> byPart = null;
                    foreach (var c in cl)
                    {
                        gAll += c.G; accAll += c.Acc; n++; gk[c.Who == pid ? FPlayer : (c.Who.Length > 0 && c.Who[0] == 't' ? FTown : FLord)] += c.G;
                        if (byPart == null) byPart = new Dictionary<string, double>(StringComparer.Ordinal);
                        double x; byPart.TryGetValue(c.Part ?? "", out x); byPart[c.Part ?? ""] = x + c.G;
                    }
                    if (byPart != null && cl.Count > 0)
                    {
                        List<Rec> recs; _byDist.TryGetValue(cl[0].Dist ?? "", out recs);
                        foreach (var kv in byPart)
                        {
                            Rec r = recs != null ? Find(recs, kv.Key) : null;
                            if (r == null || !(r.Unit > 1e-6) || r.K < 0) continue;
                            double val = r.K * r.Unit;
                            if (kv.Value > val + 1.0) { overG += kv.Value - val; overN++; }
                        }
                    }
                }
            bool log = s.LogEnabled;
            double rate = RentRate(s);
            if (log && !_startLine7)
            {
                _startLine7 = true;
                Log.Info(P7 + "start | klucze: Village Gear Lords Invest " + (s.VillageGearLordsInvest ? "TAK" : "NIE") + ", Village Gear Towns Invest " + (s.VillageGearTownsInvest ? "TAK" : "NIE")
                         + ", Village Gear Player Funds " + (s.VillageGearPlayerFunds ? "TAK" : "NIE") + " (pozycja menu wsi " + (_menu7 ? "wpieta" : "NIEWPIETA") + "), Village Gear Rent " + F2(rate)
                         + " | zapis: " + _fLoadNote + (_fLoadBad > 0 ? ", zlych wpisow " + _fLoadBad : "")
                         + " | regula: fundator kupuje tylko brakujace jednostki (do K = H) za pelna cene jednostki: material z polki miasta targowego (droga robot gminy), rzemieslnicy "
                         + "najwyzej Ds Works Wage Per Material x material -> lud, reszta (praca wsi) -> kiesa wsi; bez materialu - bez zakupu | wierzytelnosc = wklad; czynsz = wierzytelnosc x "
                         + F2(rate) + " / 364 na dobe (wies Normal, bez wojny), placony przy powrocie wozu z czesci wsi (najwyzej polowa czesci wsi z wozu; przy uldze pana tylko ponad skrzynie); nalezne ponad "
                         + RentKeepDays + " dob przepada; wierzytelnosc gasnie wedlug tabeli zuzycia rodzaju (takze przy zuzyciu sprzetu 0) i o ogien | panowie: wlasna doba rodu co " + FundCycle
                         + " dob, progi District Works Ai (Reserve Days / Reserve Flat / Surplus Share / World Pace), bez dlugu Banku, pozyczki korony, zaleglosci, dlugu drabiny, potrzeby i bez rodow bez miary budzetu; dzienna czesc tempa swiata 2 / " + FundCycle + "; czesci z m < " + F2(FundMMax)
                         + " | miasta: wlasna doba co " + FundCycle + " dob, zero rachunkow wsi, zero dlugu rady, kasa >= " + F2(TownCofferTimes) + " x zapas, najwyzej " + F2(TownSurplusShare)
                         + " nadwyzki, tylko wsad wlasnych warsztatow z N > 1 (wymaga Supply Chain Log: " + (SupplySignal.On ? "TAK" : "NIE - miasta nie funduja") + ") | droga prawdziwego materialu (Ds Works Real Material): "
                         + (TownFolk.DaWorksOn ? "czynna" : "NIECZYNNA - nikt nie funduje") + " | kolejnosc doby: po TownFolk.DaWorksRun, przed migawka MBuild.");
            }
            if (log && LogGate.Lines)
            {
                long buyOut = 0, buyIn = 0;
                for (int k = 0; k < FKinds; k++) { buyOut += _fPaid[k]; buyIn += _fMat[k] + _fWageFolk[k] + _fWageCof[k] + _fWork[k]; }
                var sb = new StringBuilder(1500);
                sb.Append(P7).Append("dzien ").Append(today).Append(" | wierzytelnosci ").Append(N0(gAll)).Append(" zl w ").Append(n).Append(" wpisach (panowie / miasta / gracz ")
                  .Append(N0(gk[FLord])).Append(" / ").Append(N0(gk[FTown])).Append(" / ").Append(N0(gk[FPlayer])).Append("), czynsz roczny przy ").Append(F2(rate)).Append(": ").Append(N0(gAll * rate))
                  .Append(" zl, nalezne niezaplacone ").Append(N0(accAll)).Append(" | kupione od poprzedniej linii:");
                for (int k = 0; k < FKinds; k++)
                    sb.Append(' ').Append(FName[k]).Append(' ').Append(F1(_fU[k])).Append(" jedn. w ").Append(_fN[k]).Append(" zakupach za ").Append(_fPaid[k]).Append(" zl (material ").Append(_fMat[k])
                      .Append(", rzemieslnicy do ludu ").Append(_fWageFolk[k]).Append(", z plac w kasie targu ").Append(_fWageCof[k]).Append(", praca wsi ").Append(_fWork[k]).Append("; sztuk ").Append(_fPcs[k]).Append(')').Append(k < FKinds - 1 ? ";" : "");
                sb.Append(" | zakupy bez skutku wedlug powodu: droga nieczynna ").Append(_fWhy[TownFolk.GfRoad]).Append(", targ bez kiesy ludu ").Append(_fWhy[TownFolk.GfNoFolk]).Append(", za maly ").Append(_fWhy[TownFolk.GfSmall])
                  .Append(", polka bez materialu ").Append(_fWhy[TownFolk.GfNoShelf]).Append(", material ponad sufit ceny ").Append(_fWhy[TownFolk.GfPriceCap]).Append(", platnik bez zlota ").Append(_fWhy[TownFolk.GfNoGold])
                  .Append(", wyjatek ").Append(_fWhy[TownFolk.GfErr])
                  .Append(" | panowie (").Append(s.VillageGearLordsInvest ? "TAK" : "NIE").Append("): rodow w swojej dobie ").Append(_lClans).Append(", odrzucone: dlug Banku ").Append(_lBank).Append(", pozyczka korony ").Append(_lCrown)
                  .Append(", zaleglosc drabiny ").Append(_lBlocked).Append(", dluznik drabiny ").Append(_lDebtor).Append(", w potrzebie ").Append(_lNeed).Append(", bez miary budzetu ").Append(_lNoMeasure).Append(", bez nadwyzki ").Append(_lNoSurplus).Append(", tempo swiata wyczerpane ").Append(_lBudget)
                  .Append("; wsi z proba zakupu ").Append(_lVil).Append(", targ wroga ").Append(_lWarMk).Append("; tempo swiata w oknie ").Append(FundCycle).Append(" dob ").Append((100.0 * _lPace).ToString("0.0000", Inv))
                  .Append("% rocznej produkcji wsi (").Append(N0(_lWorld)).Append(" zl/rok; pulap ").Append((100.0 * Math.Max(0f, Math.Min(0.1f, s.DistrictWorksAiWorldPace))).ToString("0.00", Inv)).Append("%)")
                  .Append(" | miasta (").Append(s.VillageGearTownsInvest ? "TAK" : "NIE").Append("): w swojej dobie ").Append(_tLooked).Append(", odrzucone: oblezenie ").Append(_tSiege).Append(", rachunki wsi ").Append(_tKwit)
                  .Append(", dlug rady ").Append(_tDebt).Append(", kasa ponizej progu ").Append(_tCoffer).Append(", bez tablicy N ").Append(_tNoTable).Append(", bez brakujacego wsadu ").Append(_tNoShort)
                  .Append("; wsi z proba zakupu ").Append(_tVil)
                  .Append(" | czynsz: naliczone ").Append(F1(_rAcc)).Append(", zaplacone ").Append(_rPaid).Append(" zl przy ").Append(_rPaidN).Append(" wozach, przepadlo (ponad ").Append(RentKeepDays).Append(" dob) ").Append(F1(_rLapsed))
                  .Append("; wstrzymane: wies nie Normal albo wojna ").Append(_rHeld).Append(" wpisow, wojna przy wozie ").Append(_rWarCart).Append(", skrzynia ").Append(_rChest).Append(" zl, polowa czesci wsi ").Append(_rHalf).Append(" zl")
                  .Append(" | wierzytelnosci: przybylo ").Append(N0(_gIn)).Append(", wygaslo zuzyciem ").Append(F1(_gDecay)).Append(", spalone ").Append(F1(_gBurn)).Append(", odstawione zmiana zajecia ").Append(F1(_gShift)).Append(", fundator zniknal ").Append(F1(_gDead))
                  .Append(", ponizej 1 zl ").Append(F2(_gSmall))
                  .Append(" | kontrola zlota zakupow: z kies platnikow - (kasy targow + lud + kiesy wsi) = ").Append(_fGap).Append("; wklad - (material + rzemieslnicy + praca wsi) = ").Append(buyOut - buyIn)
                  .Append("; sztuki zakupu inne niz zmiana polki w ").Append(_fPcsBad).Append(" zakupach | kontrola czynszu: z kies wsi - u fundatorow = ").Append(_rOut - _rIn);
                if (_prevGDay == today - 1 && !double.IsNaN(_prevG))
                    sb.Append(" | kontrola ksiegi: dzis - (wczoraj + przybylo - wygaslo - spalone - odstawione - znikle) = ").Append(F2(gAll - (_prevG + _gIn - _gDecay - _gBurn - _gShift - _gDead - _gSmall)));
                else sb.Append(" | kontrola ksiegi: brak wczorajszej sumy (pierwsza doba sesji)");
                sb.Append(" | wierzytelnosci ponad wartosc sprzetu czesci (K x cena jednostki): ").Append(N0(overG)).Append(" zl w ").Append(overN).Append(" czesciach")
                  .Append(" | cena fundatora: ").Append(PaysWork ? "pelna cena jednostki z praca wsi (Village Gear Funder Pays Village Work TAK)" : "jak gmina - material + rzemieslnicy (praca wsi 0)")
                  .Append(_fundWait > 0 ? " | pierwsza doba sesji bez wyceny czesci - rody i miasta tej doby funduja jutro" : "");
                if (!double.IsNaN(_prevAcc) && _prevGDay == today - 1)
                    sb.Append(" | kontrola naleznego czynszu: dzis - (wczoraj + naliczone - zaplacone - przepadle - zdjete z wpisami ").Append(F1(_accGone)).Append(") = ")
                      .Append(F2(accAll - (_prevAcc + _rAcc - _rPaid - _rLapsed - _accGone)));
                sb.Append(" | sprzet od fundatorow jest w linii L3 (kontrola sprzetu: + od fundatorow) | koszt ").Append(F2(_ms7)).Append(" ms | potkniecia ").Append(_st7).Append(" (sesja ").Append(_st7All).Append(").");
                Log.Info(sb.ToString());
            }
            _prevG = gAll; _prevGDay = today; _prevAcc = accAll;
            lock (Gate) FundZero();
        }

        // ============================================================ zapis (arm_lpf)
        /// <summary>Zapis: "lpf1|doba:zloto,doba:zloto,...|fundator~okreg~czesc~G~nalezne~doba~zaplacone;...". Bez wierzytelnosci i bez okna tempa - "" (nic do wczytania).</summary>
        internal static string FundExport()
        {
            try
            {
                lock (Gate)
                {
                    var rg = new StringBuilder();
                    for (int i = 0; i < FundCycle; i++)
                        if (_ringDay[i] != int.MinValue && _ring[i] > 0) { if (rg.Length > 0) rg.Append(','); rg.Append(_ringDay[i]).Append(':').Append(D(_ring[i])); }
                    if (_claimN == 0 && rg.Length == 0) return "";
                    var sb = new StringBuilder(_claimN * 64 + 64);
                    sb.Append(FundVer).Append('|').Append(rg).Append('|');
                    bool f = true;
                    foreach (var cl in _claims.Values)
                        foreach (var c in cl)
                        {
                            if (!IdOk(c.Who) || !IdOk(c.Dist) || !IdOk(c.Part) || c.Who.IndexOf(',') >= 0) continue;
                            if (!f) sb.Append(';'); f = false;
                            sb.Append(c.Who).Append('~').Append(c.Dist).Append('~').Append(c.Part).Append('~').Append(D(c.G)).Append('~').Append(D(c.Acc)).Append('~').Append(c.Day).Append('~').Append(D(c.PaidAll));
                        }
                    return sb.ToString();
                }
            }
            catch (Exception e) { Stumble7("FundExport", e); return ""; }
        }

        /// <summary>Wczytanie (po Reset z konstruktora): brak klucza albo pusty - stan pusty; zly wpis pominiety i policzony.</summary>
        internal static void FundImport(string data)
        {
            try
            {
                lock (Gate)
                {
                    _claims.Clear(); _claimN = 0; _fLoadBad = 0; Array.Clear(_ring, 0, FundCycle); for (int i = 0; i < FundCycle; i++) _ringDay[i] = int.MinValue;
                    if (string.IsNullOrEmpty(data)) { _fLoadNote = "zapis bez arm_lpf (bez wierzytelnosci)"; return; }
                    var p = data.Split('|');
                    if (p.Length < 3 || p[0] != FundVer) { _fLoadNote = "arm_lpf w nieznanej wersji '" + (p.Length > 0 ? p[0] : "") + "' - pominiety (bez wierzytelnosci)"; return; }
                    if (p[1].Length > 0)
                        foreach (var e in p[1].Split(','))
                        {
                            int c = e.IndexOf(':'); int d; double x;
                            if (c <= 0 || !int.TryParse(e.Substring(0, c), NumberStyles.Integer, Inv, out d) || !Num(e.Substring(c + 1), out x) || x < 0) { _fLoadBad++; continue; }
                            int k = ((d % FundCycle) + FundCycle) % FundCycle;
                            if (_ringDay[k] == int.MinValue || d > _ringDay[k]) { _ringDay[k] = d; _ring[k] = x; }
                        }
                    int n = 0;
                    if (p[2].Length > 0)
                        foreach (var e in p[2].Split(';'))
                        {
                            var a = e.Split('~');
                            double g, acc, paid; int day;
                            if (a.Length < 7 || !IdOk(a[0]) || a[0].Length < 3 || (a[0][0] != 'c' && a[0][0] != 't') || a[0][1] != ':' || !IdOk(a[1]) || !IdOk(a[2])
                                || !Num(a[3], out g) || !Num(a[4], out acc) || !int.TryParse(a[5], NumberStyles.Integer, Inv, out day) || !Num(a[6], out paid) || g < 0 || acc < 0)
                            { _fLoadBad++; continue; }
                            List<Claim> cl;
                            if (!_claims.TryGetValue(a[1], out cl)) { cl = new List<Claim>(); _claims[a[1]] = cl; }
                            bool dup = false; foreach (var x in cl) if (x.Who == a[0] && x.Part == a[2]) { dup = true; break; }
                            if (dup) { _fLoadBad++; continue; }
                            cl.Add(new Claim { Who = a[0], Dist = a[1], Part = a[2], G = g, Acc = acc, Day = day, PaidAll = Math.Max(0.0, paid) });
                            _claimN++; n++;
                        }
                    _fLoadNote = "arm_lpf wczytany: wierzytelnosci " + n;
                }
            }
            catch (Exception e) { Stumble7("FundImport", e); }
        }
    }
}
