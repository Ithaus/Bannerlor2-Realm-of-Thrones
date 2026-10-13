using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// E7/B (231, PLAN 7.12, R16; projekt etapu 7 v2 rozdz. 3 "E7/B") - LICZNIK PRZEJSC PRZEZ BLIZNAKI. Sam log: bez latki, bez stanu w zapisie, nikt nic nie placi.
    /// Klucz TwinsCrossingCount. Myto "gdyby bylo" (2 zl od czlowieka, 1 zl od zwierzecia, rody krolestwa pana bez myta - stawki z ES:215) tylko do porownania;
    /// myto naprawde to osobna paczka po liczniku i TAK Jeffa (projekt rozdz. 6 pyt. 4).
    /// 1. Os rzeki RAZ na sesje (pierwsza godzina gry po wczytaniu): 64 punkty na okregu r = 4 wokol punktu osady ROT_town3 (to NIE most - lezy 3.5 jedn. od osi, po stronie bramy), typ sciany siatki pod punktem
    ///    (MapSceneWrapper.GetFaceIndex z IsOnLand = true i FaceGroupIndex = TerrainType - to samo, co GetFaceTerrainType, bez asercji gry dla pustej sciany;
    ///    wzor MapVillagesView.Geo.Group). Woda = brak sciany albo Fording, Mountain (w ROT pasy rzek - docs W2), Lake, Water, River, CoastalSea, OpenSea,
    ///    NonNavigableRiver, LandRestriction, SeaRestriction, UnderBridge; reszta (z mostem Bridge) - lad. Os = prosta przez srodki DWOCH lukow wody na okregu;
    ///    inna liczba lukow = licznik wylaczony z napisem "BRAK OSI" (bez wywrotki; wzorce okregow r 3-6 w linii startowej - do poprawki promienia z dowodu).
    ///    MOST = punkt na osi w polowie miedzy srodkami lukow (F8, test zbiorczy 5-8: 4 pozycje przejsc z biegu 120 dob leza 0.0-0.1 jedn. od niego i 4.8 od bramy).
    /// 2. Raz na godzine gry (HourlyTickEvent): lokator mapy wokol MOSTU (MobileParty.StartFindingLocatablesAroundPosition, jak NightMarch.AlarmThreat) - tylko
    ///    partie w promieniu 8 od mostu, bez petli po MobileParty.All (F8: przedtem strefa wokol bramy - za rzeka zostawalo 2.8 jedn. strefy za pasem osi, po stronie
    ///    bramy ok. 12, wiec szybsza partia mogla nie dostac probki za rzeka). Strona = znak odleglosci od osi (iloczyn wektorowy (pozycja - os) x kierunek rzeki);
    ///    pas 0.5 jedn. przy osi (most, wieza) strony nie zmienia. Partia w osadzie stoi na bramie (setter MobileParty.CurrentSettlement; 4.8 jedn. od mostu - w strefie) - strona bramy, jak kazda
    ///    pozycja (recenzja: wizyta z drugiego brzegu = zawsze dwa przejscia, nie 0 albo 2 wedlug trafienia probki w dojscie do bramy); partia na morzu - pominieta.
    /// 3. Pamiec: slownik partia -> ostatnia pewna strona i pozycja (tylko partie w strefie i czekajace w Bliznakach; wpis znika po wyjsciu albo smierci partii).
    ///    Przejscie = zmiana strony. Strefa ma po 7.5 jedn. z kazdej strony pasa osi: partia do ok. 7.5 jedn./h nie przeskoczy jej miedzy probkami. Jedn./h = Speed / 1.2
    ///    (gra: MobileParty.ComputeNextMoveDistance - Speed * dt, Campaign - 4320 s gry na jednostke dt; jak NightMarch.UnitsPerHour), czyli granica to Speed ok. 9;
    ///    srednie Speed karawan i wozow wsi to 3.0-3.7. Szybsza partia moze przeskoczyc strefe - licznik zaniza, nie zawyza.
    ///    Liczniki kontrolne doby (F8) - zaden nie zmienia liczby przejsc ani myta, oba maja byc 0:
    ///    "zmiana strony poza strefa" = partia wypadla z pamieci (wyszla ze strefy), zyje, jest do 16 jedn. od mostu i stoi po innej stronie osi niz zapamietana
    ///    - przejscie zgubione (partia szybsza niz strefa albo os nie pasuje do rzeki);
    ///    "przejsc poza mostem" = punkt przejscia (przeciecie odcinka miedzy probkami z osia) dalej niz 2 jedn. od mostu - mozliwe falszywe przejscie
    ///    (os to prosta zmierzona do ok. 5 jedn. w bok od mostu, rzeka dalej moze skrecac; punkt lezy na CIECIWIE miedzy probkami, nie na torze partii).
    /// 4. Wejscia do osady (Measure169c.OnSettlementEntered, linia "(e) Bliznaki") zostaja obok (porownanie). CrossingLaw - bez zmian (etap 8).
    /// Linia doby "Przeprawa Bliznaki (E7/B)" po ksiedze pieniadza; w pierwszych 3 dobach sesji z licznikiem - pozycje przejsc (punkt na osi miedzy probkami).
    /// </summary>
    internal static class TwinsCrossing
    {
        private const string TwinsId = "ROT_town3";
        private const float ZoneR = 8f;        // strefa wokol mostu (F8; przedtem wokol bramy)
        private const float RingR = 4f;        // okrag wokol punktu osady do osi rzeki (projekt E7/B)
        private const int RingN = 64;
        private const float DeadBand = 0.5f;   // pas przy osi (most, wieza) - strona sie nie zmienia
        private const float OffBridgeR = 2f;   // F8: punkt przejscia dalej od mostu = licznik "przejsc poza mostem" (sam licznik - przejscie zostaje policzone)
        private const float LostR = 16f;       // F8: do tej odleglosci od mostu sprawdzamy strone partii, ktora wypadla z pamieci ("zmiana strony poza strefa")
        private const int TollMan = 2, TollAnimal = 1;   // ES:215 - tylko do porownania
        private const int PosDays = 3, PosMax = 30;      // pozycje przejsc: pierwsze 3 doby sesji z licznikiem, najwyzej 30 na dobe

        private struct Seen { public sbyte Side; public Vec2 Pos; public int Stamp; }

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly Dictionary<MobileParty, Seen> _seen = new Dictionary<MobileParty, Seen>();
        private static readonly List<MobileParty> _drop = new List<MobileParty>();
        private static readonly List<string> _pos = new List<string>();
        private static readonly HashSet<string> _errSites = new HashSet<string>();

        // os rzeki (sesja): 0 = jeszcze nie liczona, 1 = jest, -1 = BRAK OSI, -2 = brak osady
        private static int _state;
        private static string _why;
        private static Settlement _twins;
        private static Vec2 _gate, _bridge, _p0, _dir;   // _bridge (F8): most = punkt na osi w polowie miedzy srodkami lukow wody; srodek strefy
        private static bool _ns, _posFirst;   // _ns: strony polnoc/poludnie (inaczej wschod/zachod); _posFirst: strona dodatnia = polnoc albo wschod
        private static int _stamp, _inZone, _activeLines, _stumbles, _stumblesDay, _samples;
        private static long _ticks;

        // liczniki doby
        private static int _n, _toPos, _toNeg, _car, _vil, _clanOwn, _clanForeign, _band, _other, _player;
        private static long _carGold, _carMen, _carAnimals, _tollCar, _tollVil, _tollClan, _tollBand, _tollOther, _tollPlayer;
        private static int _offBridge, _lostSide;   // F8: liczniki kontrolne doby (nie zmieniaja liczby przejsc ani myta)
        private static float _farMax;               // F8: najdalszy dzis punkt przejscia od mostu

        internal static bool On { get { var s = Settings.Current; return s != null && s.TwinsCrossingCount; } }

        internal static void Reset()
        {
            _seen.Clear(); _drop.Clear();
            _state = 0; _why = null; _twins = null;
            _stamp = _inZone = _activeLines = _stumbles = 0;
            _errSites.Clear();
            DayReset();
        }

        private static void DayReset()
        {
            _n = _toPos = _toNeg = _car = _vil = _clanOwn = _clanForeign = _band = _other = _player = 0;
            _carGold = _carMen = _carAnimals = _tollCar = _tollVil = _tollClan = _tollBand = _tollOther = _tollPlayer = 0;
            _pos.Clear();
            _stumblesDay = _samples = 0;
            _offBridge = _lostSide = 0; _farMax = 0f;
            _ticks = 0;
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumblesDay++;
            if (_errSites.Add(where)) Log.Error("TwinsCrossing." + where, e);
        }

        // ------------------------------------------------------------ os rzeki (raz na sesje)

        /// <summary>Woda dla osi: brak sciany siatki albo typ wody / bariery (TerrainType: 6, 7, 8, 10, 11, 18, 19, 22-25); lad z mostem Bridge 17 - reszta.</summary>
        private static bool IsWater(int g)
        {
            return g < 0 || g == 6 || g == 7 || g == 8 || g == 10 || g == 11 || g == 18 || g == 19 || g == 22 || g == 23 || g == 24 || g == 25;
        }

        /// <summary>Okrag RingN punktow o promieniu r wokol c: woda w punkcie i wzorzec ('.' lad, 'm' Mountain, 'w' inna woda, 'x' brak sciany).</summary>
        private static bool[] Ring(TaleWorlds.CampaignSystem.Map.IMapScene ms, Vec2 c, float r, out string pattern)
        {
            var w = new bool[RingN];
            var sb = new StringBuilder(RingN);
            for (int i = 0; i < RingN; i++)
            {
                double a = 2.0 * Math.PI * i / RingN;
                var cv = new CampaignVec2(new Vec2(c.x + r * (float)Math.Cos(a), c.y + r * (float)Math.Sin(a)), true);
                PathFaceRecord f = ms.GetFaceIndex(in cv);
                int g = f.IsValid() ? f.FaceGroupIndex : -1;
                w[i] = IsWater(g);
                sb.Append(g < 0 ? 'x' : g == 7 ? 'm' : w[i] ? 'w' : '.');
            }
            pattern = sb.ToString();
            return w;
        }

        /// <summary>Luki wody na okregu (start, dlugosc); caly okrag woda albo caly lad - zero lukow.</summary>
        private static List<KeyValuePair<int, int>> Arcs(bool[] w)
        {
            var arcs = new List<KeyValuePair<int, int>>();
            int n = w.Length, first = -1;
            for (int i = 0; i < n; i++) if (!w[i]) { first = i; break; }
            if (first < 0) return arcs;
            int runStart = -1, runLen = 0;
            for (int k = 1; k <= n; k++)
            {
                int i = (first + k) % n;
                if (w[i]) { if (runLen == 0) runStart = i; runLen++; }
                else if (runLen > 0) { arcs.Add(new KeyValuePair<int, int>(runStart, runLen)); runLen = 0; }
            }
            return arcs;
        }

        private static Vec2 ArcCenter(Vec2 c, KeyValuePair<int, int> arc)
        {
            double a = 2.0 * Math.PI * (arc.Key + (arc.Value - 1) / 2.0) / RingN;
            return new Vec2(c.x + RingR * (float)Math.Cos(a), c.y + RingR * (float)Math.Sin(a));
        }

        /// <summary>Odleglosc ze znakiem od osi rzeki (dodatnia po stronie normalnej (dir.y, -dir.x)).</summary>
        private static float SD(Vec2 x) { return (x.x - _p0.x) * _dir.y - (x.y - _p0.y) * _dir.x; }

        private static string P(Vec2 v) { return v.x.ToString("0.00", Inv) + "/" + v.y.ToString("0.00", Inv); }

        private static void Compute()
        {
            _state = -1;
            _twins = Settlement.Find(TwinsId);
            if (_twins == null)
            {
                _state = -2;
                Log.Info("Przeprawa Bliznaki (E7/B): os rzeki | osady " + TwinsId + " nie ma - licznik wylaczony.");
                return;
            }
            var ms = Campaign.Current.MapSceneWrapper;
            Vec2 town = _twins.GetPosition2D;   // punkt osady = srodek okregow; to NIE most (F8: most = _bridge nizej)
            _gate = _twins.GatePosition.ToVec2();
            string p4, p3, p5, p6;
            bool[] w = Ring(ms, town, RingR, out p4);
            Ring(ms, town, 3f, out p3); Ring(ms, town, 5f, out p5); Ring(ms, town, 6f, out p6);
            int water = 0;
            for (int i = 0; i < w.Length; i++) if (w[i]) water++;
            var arcs = Arcs(w);
            string head = "Przeprawa Bliznaki (E7/B): os rzeki | osada " + TwinsId + " " + _twins.Name + ", punkt osady " + P(town) + ", brama " + P(_gate)
                          + " | okrag r " + RingR.ToString("0", Inv) + ": " + RingN + " punktow, woda " + water + ", luki " + arcs.Count;
            string pats = " | wzorce okregow (punkt 0 na wschodzie, przeciwnie do zegara; '.' lad, 'm' Mountain, 'w' inna woda, 'x' brak sciany): r3 " + p3 + ", r4 " + p4
                          + ", r5 " + p5 + ", r6 " + p6;
            if (arcs.Count != 2)
            {
                _why = "luki wody na okregu r 4: " + arcs.Count + " (potrzeba 2)";
                Log.Info(head + " | BRAK OSI - licznik wylaczony" + pats + ".");
                return;
            }
            Vec2 a1 = ArcCenter(town, arcs[0]), a2 = ArcCenter(town, arcs[1]);
            Vec2 d = a2 - a1;
            float len = d.Length;
            if (len < 0.5f)
            {
                _why = "srodki lukow za blisko (" + len.ToString("0.00", Inv) + " jedn.)";
                Log.Info(head + " | BRAK OSI - " + _why + " - licznik wylaczony" + pats + ".");
                return;
            }
            _p0 = a1;
            _dir = new Vec2(d.x / len, d.y / len);
            float nx = _dir.y, ny = -_dir.x;   // normalna = strona dodatnia
            _ns = Math.Abs(ny) >= Math.Abs(nx);
            _posFirst = _ns ? ny > 0 : nx > 0;
            _bridge = new Vec2((a1.x + a2.x) * 0.5f, (a1.y + a2.y) * 0.5f);   // F8: most = punkt na osi w polowie miedzy srodkami lukow wody - srodek strefy probek
            _state = 1;
            float gd = SD(_gate), td = SD(town);
            double ang = Math.Atan2(_dir.y, _dir.x) * 180.0 / Math.PI;
            Log.Info(head + " (srodki " + P(a1) + ", " + P(a2) + ") | os: kierunek " + ang.ToString("0", Inv) + " st., strony " + (_ns ? "polnoc / poludnie" : "wschod / zachod")
                     + "; most na osi " + P(_bridge) + " (w polowie miedzy srodkami lukow), punkt osady " + td.ToString("0.00", Inv) + " jedn. od osi, brama " + gd.ToString("0.00", Inv)
                     + " (" + SideName(gd) + "), " + _gate.Distance(_bridge).ToString("0.00", Inv) + " jedn. od mostu"
                     + " | strefa r " + ZoneR.ToString("0", Inv) + " od mostu, pas przy osi " + DeadBand.ToString("0.0", Inv) + " | klucz TwinsCrossingCount (teraz " + (On ? "TAK" : "NIE") + ")"
                     + pats + ".");
        }

        private static string SideName(float d)
        {
            if (d > -DeadBand && d < DeadBand) return "przy osi";
            bool pos = d > 0;
            bool first = pos == _posFirst;
            return _ns ? (first ? "polnoc" : "poludnie") : (first ? "wschod" : "zachod");
        }

        // ------------------------------------------------------------ probka godzinna

        /// <summary>Raz na godzine gry: partie w strefie 8 jedn. od mostu (lokator mapy), zmiana strony rzeki = przejscie.</summary>
        internal static void Hourly()
        {
            if (!On || Campaign.Current == null) { if (_seen.Count > 0) _seen.Clear(); return; }
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                if (_state == 0)
                {
                    try { Compute(); }
                    catch (Exception ce) { _state = -1; _why = "wyjatek przy liczeniu osi " + ce.GetType().Name; Stumble("Compute", ce); }
                }
                if (_state != 1) return;
                _stamp++; _samples++;
                int inZone = 0;
                var data = MobileParty.StartFindingLocatablesAroundPosition(_bridge, ZoneR);   // F8: srodek strefy = most (przedtem brama)
                for (var mp = MobileParty.FindNextLocatable(ref data); mp != null; mp = MobileParty.FindNextLocatable(ref data))
                {
                    try
                    {
                        if (!mp.IsActive || mp.IsCurrentlyAtSea) continue;
                        Vec2 pos = mp.GetPosition2D;
                        if (pos.Distance(_bridge) > ZoneR) continue;
                        inZone++;
                        Seen e;
                        bool had = _seen.TryGetValue(mp, out e);
                        e.Stamp = _stamp;
                        // partia w osadzie: pozycja = brama osady (gra), strona liczona jak kazda inna pozycja
                        float d = SD(pos);
                        sbyte side = d > DeadBand ? (sbyte)1 : (d < -DeadBand ? (sbyte)-1 : (sbyte)0);
                        if (side != 0)
                        {
                            if (had && e.Side != 0 && e.Side != side) Cross(mp, e.Pos, pos, side);
                            e.Side = side; e.Pos = pos;
                        }
                        else if (!had) { e.Side = 0; e.Pos = pos; }
                        _seen[mp] = e;
                    }
                    catch (Exception ex) { Stumble("Hourly(partia)", ex); }
                }
                _inZone = inZone;
                // wpisy partii, ktore wyszly ze strefy albo zginely (zostaja tylko czekajace w Bliznakach)
                _drop.Clear();
                foreach (var kv in _seen)
                {
                    if (kv.Value.Stamp == _stamp) continue;
                    var p = kv.Key;
                    if (p == null || !p.IsActive || p.CurrentSettlement != _twins)
                    {
                        _drop.Add(p);
                        // F8 (dowod "ile ginelo"): partia zyje i wypadla z pamieci (wyszla ze strefy, nie stoi w Bliznakach), a TERAZ jest po innej stronie osi niz zapamietana
                        // = przeszla bez probki po drugiej stronie. Sam licznik doby - NIE doliczane do przejsc ani do myta. Na morzu - pominieta, jak w probce.
                        try
                        {
                            if (p != null && p.IsActive && kv.Value.Side != 0 && !p.IsCurrentlyAtSea)
                            {
                                Vec2 q = p.GetPosition2D;
                                if (q.Distance(_bridge) <= LostR)
                                {
                                    float qd = SD(q);
                                    if ((qd > DeadBand && kv.Value.Side < 0) || (qd < -DeadBand && kv.Value.Side > 0)) _lostSide++;
                                }
                            }
                        }
                        catch (Exception ex) { Stumble("Hourly(wyjscie)", ex); }
                    }
                }
                for (int i = 0; i < _drop.Count; i++) _seen.Remove(_drop[i]);
                _drop.Clear();
            }
            catch (Exception e) { Stumble("Hourly", e); }
            finally { _ticks += Stopwatch.GetTimestamp() - t0; }
        }

        /// <summary>Przejscie partii na strone side: rodzaj partii, ludzie (z jencami), zwierzeta (konie pod jezdzcami, luzaki, juczne, bydlo), myto gdyby bylo.</summary>
        private static void Cross(MobileParty mp, Vec2 prev, Vec2 cur, sbyte side)
        {
            _n++;
            if (side > 0) _toPos++; else _toNeg++;
            // punkt przejscia = przeciecie odcinka miedzy probkami z osia; F8: liczony dla KAZDEGO przejscia (straznik falszywych przejsc), nie tylko w dobach z pozycjami
            float dp = SD(prev), dc = SD(cur);
            float t = Math.Abs(dp - dc) > 1e-4f ? dp / (dp - dc) : 0.5f;
            var x = new Vec2(prev.x + (cur.x - prev.x) * t, prev.y + (cur.y - prev.y) * t);
            float bd = x.Distance(_bridge);
            if (bd > _farMax) _farMax = bd;
            if (bd > OffBridgeR) _offBridge++;   // sam licznik w linii doby - przejscie i myto licza sie jak kazde inne
            int men = mp.MemberRoster.TotalManCount + mp.PrisonRoster.TotalManCount;
            var ir = mp.ItemRoster;
            int animals = mp.Party.NumberOfMenWithHorse + ir.NumberOfMounts + ir.NumberOfPackAnimals + ir.NumberOfLivestockAnimals;
            long toll = (long)TollMan * men + (long)TollAnimal * animals;
            if (mp.IsMainParty) { _player++; if (!OwnRealm(mp.ActualClan)) _tollPlayer += toll; }   // gracz w krolestwie pana - bez myta, jak inne rody tego krolestwa
            else if (mp.IsCaravan) { _car++; _carGold += mp.PartyTradeGold; _carMen += men; _carAnimals += animals; _tollCar += toll; }
            else if (mp.IsVillager) { _vil++; _tollVil += toll; }
            else if (mp.IsLordParty) { if (OwnRealm(mp.ActualClan)) _clanOwn++; else { _clanForeign++; _tollClan += toll; } }
            else if (mp.IsBandit) { _band++; _tollBand += toll; }
            else { _other++; _tollOther += toll; }
            if (_activeLines < PosDays && _pos.Count < PosMax)
                _pos.Add(P(x) + " (" + bd.ToString("0.0", Inv) + ")");   // F8: odleglosc od mostu (przedtem od bramy)
        }

        /// <summary>Rod krolestwa pana Bliznakow (pan bez krolestwa - tylko jego rod).</summary>
        private static bool OwnRealm(Clan c)
        {
            var oc = _twins != null ? _twins.OwnerClan : null;
            if (c == null || oc == null) return false;
            return oc.Kingdom != null ? c.Kingdom == oc.Kingdom : c == oc;
        }

        // ------------------------------------------------------------ linia doby

        /// <summary>Linia "Przeprawa Bliznaki (E7/B)" i zerowanie licznikow doby (po ksiedze pieniadza).</summary>
        internal static void Daily()
        {
            var s = Settings.Current;
            if (s == null) return;
            int day = (int)CampaignTime.Now.ToDays;
            string head = "Przeprawa Bliznaki (E7/B): dzien " + day;
            try
            {
                if (!s.TwinsCrossingCount) { Log.Info(head + " | WYLACZONE (Twins Crossing Count)."); return; }
                if (_state == -2) { Log.Info(head + " | osady " + TwinsId + " nie ma - licznik wylaczony."); return; }
                if (_state == -1) { Log.Info(head + " | BRAK OSI (" + _why + ") - licznik wylaczony | potkniecia dzis " + _stumblesDay + " (razem " + _stumbles + ")."); return; }
                if (_state == 0) { Log.Info(head + " | os rzeki jeszcze nie liczona (brak godziny gry z licznikiem w tej sesji)."); return; }
                int first = _posFirst ? _toPos : _toNeg, second = _posFirst ? _toNeg : _toPos;
                long toll = _tollCar + _tollVil + _tollClan + _tollBand + _tollOther + _tollPlayer;
                var oc = _twins.OwnerClan;
                string lord = oc == null ? "-" : oc.Name + " (" + (oc.Kingdom != null ? oc.Kingdom.Name.ToString() : "bez krolestwa") + ")";
                double ms = _ticks * 1000.0 / Stopwatch.Frequency;
                var sb = new StringBuilder(640);
                sb.Append(head).Append(" | przejsc ").Append(_n)
                  .Append(_ns ? " (na polnoc " : " (na wschod ").Append(first).Append(_ns ? ", na poludnie " : ", na zachod ").Append(second).Append(")")
                  .Append(" - karawany ").Append(_car).Append(" (kiesy ").Append(_carGold.ToString(Inv)).Append(" zl, ludzi ").Append(_carMen).Append(", zwierzat ").Append(_carAnimals).Append(")")
                  .Append(", wozy wsi ").Append(_vil)
                  .Append(", partie rodow ").Append(_clanOwn + _clanForeign).Append(" (krolestwo pana ").Append(_clanOwn).Append(" / obce ").Append(_clanForeign).Append(")")
                  .Append(", bandy ").Append(_band).Append(", inne ").Append(_other)
                  .Append(", gracz ").Append(_player > 0 ? "TAK (" + _player + ")" : "NIE")
                  .Append(" | myto gdyby bylo ").Append(toll.ToString(Inv)).Append(" zl (2 zl od czlowieka z jencami, 1 zl od zwierzecia - konie pod jezdzcami, luzaki, juczne, bydlo;")
                  .Append(" rody krolestwa pana bez myta - stawki z ES:215, tylko do porownania; karawany ").Append(_tollCar.ToString(Inv))
                  .Append(", wozy ").Append(_tollVil.ToString(Inv)).Append(", obce rody ").Append(_tollClan.ToString(Inv)).Append(", bandy ").Append(_tollBand.ToString(Inv))
                  .Append(", inne ").Append(_tollOther.ToString(Inv)).Append(", gracz ").Append(_tollPlayer.ToString(Inv)).Append(")")
                  .Append(" | pan Bliznakow ").Append(lord)
                  .Append(" | strefa r ").Append(ZoneR.ToString("0", Inv)).Append(" od mostu: partii ").Append(_inZone).Append(", w pamieci ").Append(_seen.Count)
                  // F8: liczniki kontrolne - w stalej czesci linii, zaraz za strefa; pozycje przejsc (3 pierwsze doby) zostaja ostatnim polem, bo narzedzie czyta je do kropki konca linii
                  .Append(" | kontrola: przejsc poza mostem ").Append(_offBridge).Append(" (punkt przejscia ponad ").Append(OffBridgeR.ToString("0", Inv))
                  .Append(" jedn. od mostu; najdalej dzis ").Append(_n > 0 ? _farMax.ToString("0.0", Inv) : "-").Append(")")
                  .Append(", zmiana strony poza strefa ").Append(_lostSide).Append(" (partia wyszla ze strefy i stoi na drugim brzegu, do ").Append(LostR.ToString("0", Inv))
                  .Append(" jedn. od mostu - nie doliczone do przejsc)")
                  .Append(" | koszt probek ").Append(ms.ToString("0.000", Inv)).Append(" ms/dobe (").Append(_samples).Append(" probek)")
                  .Append(" | potkniecia dzis ").Append(_stumblesDay).Append(" (razem ").Append(_stumbles).Append(")");
                if (_activeLines < PosDays)
                    sb.Append(" | pozycje przejsc (x/y, odl. od mostu): ").Append(_pos.Count > 0 ? string.Join(", ", _pos.ToArray()) : "brak");
                sb.Append('.');
                Log.Info(sb.ToString());
                _activeLines++;
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally { DayReset(); }
        }
    }
}
