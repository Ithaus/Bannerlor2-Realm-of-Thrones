using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// SW-DROGI (noc 17/18.10, Jeff 16.10 lista pkt 4 i 17.10: "a zajazdy, szybsze poruszanie po drogach?") - +15% PREDKOSCI NA DROGACH ROT.
    /// Drogi ROT to farba terenu (warstwa "road" w terrain.bin), nie encje - silnik ich nie podaje (mapa bez materialow fizycznych), wiec lamane
    /// policzyla sonda geografii (galaz w-toku/noc-geo 19e6f041: tools/geo/geo_drogi_rot.py + MapGeoProbe G3 w grze, STAN-PRAC 59), a mod czyta gotowy
    /// plik ModuleData\arm_map_roads_geo.tsv (generator tools/drogi/drogi_plik_modu.py; 311 lamanych, 10876 punktow, 10565 odcinkow, 10016 jedn.).
    ///
    /// PLIK -> SIATKA: raz na sesje kampanii (OnSessionLaunched, watek glowny) plik wczytany do siatki komorek 2 x 2 jedn. (CSR: poczatek komorki + lista
    /// odcinkow); odcinek wpisany do kazdej komorki, ktora siega jego kapsula (pol-szerokosc + najwiekszy margines 2 jedn.). Siatka niezmienna po budowie,
    /// publikowana jednym zapisem referencji - czytana bez blokad z watkow roboczych gry (predkosc partii liczy sie rownolegle, TWParallel w
    /// CampaignTickCacheDataStore). Pol-szerokosc drogi z farby: width / 2 lamanej (pas farby z wagi > 48), przycieta do 0.15..1.5 jedn.
    /// NA DRODZE = odleglosc do najblizszego odcinka <= pol-szerokosc + MapRoadMargin (domyslnie 0.4 jedn. - farba ma probke 0.39 jedn., szkielet
    /// Douglas-Peucker 0.2). Symulacja na trasach AI sondy (geo-trasy-punkty, 80 526 punktow): przy 0.4 na drodze 4.9% punktow tras (AI nie szuka drog).
    /// STRAZNIK MAPY: naglowek pliku ma rozmiar terrain.bin ROT-Map; inny rozmiar w grze = inna mapa = premia nieczynna z powodem w logu (bez pliku
    /// terrain.bin - bez straznika).
    ///
    /// PREMIA: MarchPace.SpeedPostfix (jedna latka na kazdy CalculateFinalSpeed, Priority.Last, najbardziej zewnetrzny poziom lancucha - SpeedDepth) po
    /// suficie kolumny wola SpeedRoad: partia ladowa na drodze dostaje +MapRoadSpeedPercent% WYNIKU jako wpis "Road" (Add / (1 + suma wspolczynnikow),
    /// jak karczmy) i sufit LimitMax (kolumna marszowa) w gore o ten sam procent - jak TownInns.SpeedBonus (STAN-PRAC 58: bez tego premia znikala pod
    /// sufitem). Wszystkie partie ladowe (gracz, lordowie, karawany, wozy wsi, bandy, patrole); partie na morzu i w przejsciu lad-morze bez zmian.
    /// Kara snu (NightRest) i premia karczmy (TownInns) biegna PO nas (procent od wyniku) - mnoza sie z premia drogi, sufit karczmy liczy sie od
    /// sufitu juz podniesionego o droge. Przy predkosci na podlodze gry (LimitMin) premia moze zginac - rzadki brzeg.
    ///
    /// PAMIEC PREDKOSCI GRY (sprawdzone w dekompilacji 1.4.8, MobileParty.CalculateSpeedForPartyUnified): gra przelicza predkosc koncowa tylko przy
    /// zmianie sciany siatki ruchu, nocy, wiatru albo bazy (IsLastSpeedCacheInvalid). Bez naszej latki premia liczylaby sie z punktu wejscia na sciane
    /// (sciany sa duze, droga ma ~0.5 jedn.) - na drodze wzdluz sciany zostawalby stary wynik. Dlatego: postfiks na MobileParty.IsLastSpeedCacheInvalid
    /// (DeclaredMethod na klasie deklarujacej) - gdy gra mowi "pamiec dobra", a stan drogi w biezacym punkcie rozni sie od stanu uzytego przy ostatnim
    /// liczeniu predkosci tej partii -> "pamiec niewazna" i gra liczy od nowa (raz na wejscie / zejscie z drogi).
    /// PAMIEC PARTII: tablica bez blokad (1 << 17 slotow long, indeks = Party.Index modulo): Party.Index (32 bity) | komorka siatki + 1 (24 bity) |
    /// stan (2 bity: 0 nieznany, 1 poza droga, 2 na drodze). Zapis w SpeedRoad (watek liczacy te partie), odczyt w postfiksie pamieci; jeden long =
    /// zapis atomowy na x64 (Volatile). Inna partia w tym samym slocie = stan nieznany = decyduje gra (jak bez latki). Ta sama komorka bez odcinkow =
    /// stan "poza droga" bez liczenia odleglosci (O(1)). Tablica czyszczona przy kazdym starcie sesji (indeksy partii sa z kampanii).
    /// STRAZNIK ZAPISU (recenzja 17/18.10, uwaga 1): stan drogi zapisywany do pamieci partii TYLKO w liczeniu, ktore gra zapisuje do _lastCalculatedSpeed
    /// (dekompilacja 1.4.8: jedyne zapisy to MobileParty.CalculateSpeedForPartyUnified i getter SpeedExplained) - prefiks + finalizer na obu ustawia
    /// [ThreadStatic] _writer = ta partia. Model wolany wprost (BkrotChains.SpeedOf co dobe, takze dla gracza; szacunki innych modow) nie zapisuje stanu -
    /// inaczej slot mowilby "na drodze", a pamiec gry trzymalaby predkosc sprzed wejscia na droge (postfiks pamieci nie widzialby roznicy). Bez obu latek
    /// straznika (blad wpinania) - zapis jak przed recenzja (wszystkie liczenia poza probka E7/T i audytem).
    /// LATKA PREDKOSCI: premia jedzie w latce MarchPace (wpinana przy starcie gry, gdy MarchPaceEnabled albo MapRoadSpeedBonus) - MarchPace.ApplyAll podaje
    /// liczbe wpietych modeli (SpeedModels). 0 = klucz wlaczony dopiero w trakcie gry przy obu kluczach wylaczonych na starcie: linie mowia NIECZYNNA (restart).
    ///
    /// POMIAR (tylko log): spis partio-godzin w dobach pomiaru (LogGate.Today - w grze co QuietLogEveryDays, w autotescie codziennie), audyt w takcie
    /// linii (do 8 partii AI na drodze + gracz: predkosc bez premii drogi i z premia z modelu wprost - bez zapisu do pamieci partii; premia rzeczywista,
    /// wpis "Road", sufit, pamiec gry), linia "Drogi (DR): dzien N". Bledy liczone (potkniecia), nigdy wylacznik calej funkcji. Nic nie idzie do zapisu gry.
    /// Ciche logi (recenzja, uwaga 2): liczniki predkosci i potkniec zerowane takze w dobie bez linii - linia mowi o tej samej dobie co spis godzinny.
    /// Podloga gry (recenzja, uwaga 3): partia na LimitMin (MinimumSpeed 1, np. dlug snu 3 = -90% w NightRest po nas) traci premie i w modelu, i u nas -
    /// audyt liczy ja osobno ("na podlodze LimitMin"), poza srednia premii i "zjedzona przez sufit".
    /// Klucze: MapRoadSpeedBonus (TAK), MapRoadSpeedPercent (15), MapRoadMargin (0.4; od n20-drogi tylko przy MapRoadWideField NIE). Wylaczony klucz = sciezka sprzed zmiany (SpeedRoad i postfiks
    /// pamieci wychodza od razu; plik nie jest wczytywany).
    ///
    /// n20-drogi (noc 19/20.10, Jeff: "rozszerz nieco pole +15% do predkosci na drodze glownej i daj +7.5% na drogach pobocznych ... bo teraz te drogi nic
    /// nie daja" / "czasami jak lekko sie zejdzie z drogi to juz nie lapie predkosc"):
    ///  - SZERSZE POLE: NOWE klucze MapRoadWideField (TAK) + MapRoadWideMargin (1.0 jedn.; 1 jedn. = ok. 4.75 km, Wayfinder.KmPerUnit) zamiast zmiany
    ///    domyslnej MapRoadMargin (Jeff ma 0.4 zapisane w MCM - zmiana domyslnej by nie dotarla; recenzja, uwaga 1). NIE = MapRoadMargin jak dotad.
    ///    Symulacja na trasach AI sondy (80 526 punktow, n20-drogi-tmp/sym_margin.py): na drodze 4.9% punktow przy 0.4, 9.2% przy 1.0. Ten sam margines
    ///    dla traktow i drog bocznych (MarginOf);
    ///  - DROGI BOCZNE: drogi do wiosek z planu MapSideRoads (te same lamane, ktore widac na mapie - MapSideRoadsView po skonczonym planie wola SidePublish)
    ///    dokladane do siatki jako odcinki co ok. 1 jedn. (os planu co 0.25 - blad cieciwy ok. 0.06), pol-szerokosc = szerokosc pasa planu / 2.
    ///    Siatka wspolna: w kazdej komorce najpierw odcinki traktow, potem bocznych (indeksy rosnace) - Kind() zwraca 2 trakt, 1 boczna, 0 nic; trakt
    ///    wygrywa. Partia na bocznej dostaje +MapSideRoadSpeedPercent% (domyslnie 7.5) jako wpis "Side road" i sufit kolumny w gore o ten sam procent.
    ///    Siatke wspolna buduje watek z puli (raz na plan), publikacja jedna referencja pod blokada (_pubLock - tylko watek glowny i watek budowy;
    ///    sciezka predkosci czyta bez blokad). Drogi boczne wylaczone na mapie / plan od nowa / koniec mapy -> SideClear (wraca siatka samych traktow).
    ///    Pamiec partii: stan 1 poza droga, 2 trakt, 3 boczna - zmiana rodzaju drogi = przeliczenie predkosci (postfiks pamieci).
    ///    Klucze nowe: MapSideRoadSpeedBonus (TAK), MapSideRoadSpeedPercent (7.5); oba wymagaja MapRoadSpeedBonus i widocznych drog bocznych (MapSideRoads).
    /// </summary>
    internal static class MapRoads
    {
        internal const string FileName = "arm_map_roads_geo.tsv";
        private const string P = "Drogi (DR): ";
        private const float CellSize = 2f;
        internal const float MaxMargin = 2f;           // MCM i kod przycinaja MapRoadMargin / MapRoadWideMargin do 0..2 (siatka budowana na ten zasieg)
        private const float HalfMin = 0.15f, HalfMax = 1.5f;
        private const int MaxPercent = 50;
        private const int SlotBits = 17;
        private const int SlotMask = (1 << SlotBits) - 1;
        private const int AuditMax = 8;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly TextObject _txtRoad = new TextObject("{=!}Road");
        private static readonly TextObject _txtSide = new TextObject("{=!}Side road");   // n20-drogi: wpis drogi bocznej w podpowiedzi predkosci
        private const float SideStep = 1f;            // n20-drogi: odcinek drogi bocznej w siatce (os planu co 0.25 jedn.)
        private const float SideHalfMin = 0.1f, SideHalfMax = 1f;
        private const int SideMaxPercent = 50;

        // ------------------------------------------------------------------ siatka (niezmienna po budowie)
        private sealed class Grid
        {
            public float X0, Y0, Inv;
            public int Nx, Ny;
            public int[] Start;       // Nx * Ny + 1
            public int[] Seg;         // odcinki komorek (w komorce rosnaco: najpierw trakty 0..MainSegs-1, potem drogi boczne)
            public float[] Ax, Ay, Dx, Dy, InvL2, Half;
            public int Lines, Points, Segments, CellsUsed, Entries, MaxPerCell;
            public double Length;
            public int Version;
            public string Md5 = "-";
            public long TerrainSize;
            // n20-drogi: odcinki o indeksie >= MainSegs to drogi boczne (siatka samych traktow: MainSegs = Segments, SideSegs = 0)
            public int MainSegs, SideSegs, SideRoads;
            public double SideLength;
            public float SideHalf;
            // m96-valyria: klasa odcinka traktu (MapRoadKinds: 0 trakt, 1 Valyria, 2 szlak karawan, 3 most Valyrii; null = bez rodzajow) i przedzial mostow
            public byte[] Cls;
            public int BridgeLo, BridgeHi, Bridges;

            /// <summary>n20-drogi: rodzaj drogi w punkcie - 2 trakt (wygrywa), 1 droga boczna (tylko przy side), 0 poza droga. W komorce trakty sa przed
            /// bocznymi, wiec pierwszy trafiony odcinek boczny znaczy, ze zaden trakt nie siega punktu.</summary>
            internal int Kind(int c, float x, float y, float margin, bool side)
            {
                int seg;
                return KindS(c, x, y, margin, side, out seg);
            }

            /// <summary>m96-valyria: Kind z trafionym odcinkiem (seg; -1 poza droga). Most Valyrii (nie farba) liczy sie tylko przy kluczu MapValyrianRoads -
            /// bez klucza pomijany (jak przed zmiana). W komorce odcinki V, mosty, trakty, szlaki (MapRoadKinds.Order) - pierwszy trafiony wygrywa.</summary>
            internal int KindS(int c, float x, float y, float margin, bool side, out int seg)
            {
                seg = -1;
                if (c < 0) return 0;
                int k1 = Start[c + 1], ms = MainSegs;
                for (int k = Start[c]; k < k1; k++)
                {
                    int s = Seg[k];
                    if (s >= ms && !side) return 0;   // dalej same drogi boczne
                    if (s >= BridgeLo && s < BridgeHi && !MapRoadKinds.ValOn(Settings.Current)) continue;
                    float px = x - Ax[s], py = y - Ay[s];
                    float t = (px * Dx[s] + py * Dy[s]) * InvL2[s];
                    if (t < 0f) t = 0f; else if (t > 1f) t = 1f;
                    float ex = px - t * Dx[s], ey = py - t * Dy[s];
                    float r = Half[s] + margin;
                    if (ex * ex + ey * ey <= r * r) { seg = s; return s < ms ? 2 : 1; }
                }
                return 0;
            }

            /// <summary>m96-valyria: klasa odcinka (MapRoadKinds.Trakt dla drog bocznych, bez rodzajow i seg -1).</summary>
            internal byte ClsOf(int seg)
            {
                var a = Cls;
                return a != null && seg >= 0 && seg < a.Length ? a[seg] : MapRoadKinds.Trakt;
            }

            internal int CellOf(float x, float y)
            {
                float fx = (x - X0) * Inv, fy = (y - Y0) * Inv;
                if (!(fx >= 0f) || !(fy >= 0f)) return -1;   // takze NaN
                int cx = (int)fx, cy = (int)fy;
                if (cx >= Nx || cy >= Ny) return -1;
                return cy * Nx + cx;
            }

            internal bool Empty(int c) { return c < 0 || Start[c] == Start[c + 1]; }

            /// <summary>Odleglosc od krawedzi pasa (odleglosc - pol-szerokosc) najblizszego odcinka komorki danego rodzaju (side: false trakty, true drogi
            /// boczne); float.MaxValue = brak odcinka w zasiegu.</summary>
            internal float EdgeDist(int c, float x, float y, bool side)
            {
                float best = float.MaxValue;
                if (c < 0) return best;
                int k1 = Start[c + 1];
                for (int k = Start[c]; k < k1; k++)
                {
                    int s = Seg[k];
                    if ((s >= MainSegs) != side) continue;
                    float px = x - Ax[s], py = y - Ay[s];
                    float t = (px * Dx[s] + py * Dy[s]) * InvL2[s];
                    if (t < 0f) t = 0f; else if (t > 1f) t = 1f;
                    float ex = px - t * Dx[s], ey = py - t * Dy[s];
                    float d = (float)Math.Sqrt(ex * ex + ey * ey) - Half[s];
                    if (d < best) best = d;
                }
                return best;
            }

            /// <summary>p6-pola (widok pol, recenzja): czy pas FARBY drogi (trakt albo droga boczna; pol-szerokosc odcinka + gap, BEZ marginesu premii
            /// predkosci) dotyka prostokata pola: srodek (x, y), os dluga (c, s) z pol-dlugoscia hb, os krotka (-s, c) z pol-szerokoscia ha. Dokladnie: odcinek
            /// przecina prostokat albo jest od niego blizej niz Half + gap. Komorka srodka wystarcza, gdy przekatna/2 + gap <= MaxMargin (wpis siega Half +
            /// MaxMargin), inaczej tez komorki rogow.</summary>
            internal bool RectHit(float x, float y, float c, float s, float ha, float hb, float gap)
            {
                int probes = (float)Math.Sqrt(ha * ha + hb * hb) + gap > MaxMargin ? 5 : 1;
                for (int q = 0; q < probes; q++)
                {
                    float qx = x, qy = y;
                    if (q > 0)
                    {
                        float sa = (q == 1 || q == 2) ? ha : -ha, sb = (q == 1 || q == 3) ? hb : -hb;
                        qx = x + c * sb - s * sa; qy = y + s * sb + c * sa;
                    }
                    int cell = CellOf(qx, qy);
                    if (Empty(cell)) continue;
                    int k1 = Start[cell + 1];
                    for (int k = Start[cell]; k < k1; k++)
                    {
                        int g = Seg[k];
                        if (g >= BridgeLo && g < BridgeHi) continue;   // m96-valyria: most Valyrii to nie farba - pola jak przed zmiana
                        float px = Ax[g] - x, py = Ay[g] - y;   // odcinek w ukladzie pola: u wzdluz osi dlugiej, v wzdluz krotkiej
                        float u0 = px * c + py * s, v0 = py * c - px * s;
                        float du = Dx[g] * c + Dy[g] * s, dv = Dy[g] * c - Dx[g] * s;
                        if (SegBoxHit(u0, v0, du, dv, hb, ha, Half[g] + gap)) return true;
                    }
                }
                return false;
            }

            /// <summary>Odcinek (u0, v0) + t (du, dv), t 0..1, wobec prostokata |u| <= bu, |v| <= bv: przecina (Liang-Barsky) albo odleglosc <= r. Rozlaczne figury
            /// wypukle: najblizej jest koniec odcinka do prostokata albo rog prostokata do odcinka.</summary>
            private static bool SegBoxHit(float u0, float v0, float du, float dv, float bu, float bv, float r)
            {
                float t0 = 0f, t1 = 1f;
                if (Clip(-du, u0 + bu, ref t0, ref t1) && Clip(du, bu - u0, ref t0, ref t1) && Clip(-dv, v0 + bv, ref t0, ref t1) && Clip(dv, bv - v0, ref t0, ref t1)) return true;
                float r2 = r * r;
                if (PtBox2(u0, v0, bu, bv) <= r2 || PtBox2(u0 + du, v0 + dv, bu, bv) <= r2) return true;
                float l2 = du * du + dv * dv;
                for (int q = 0; q < 4; q++)
                {
                    float wu = ((q & 1) == 0 ? bu : -bu) - u0, wv = ((q & 2) == 0 ? bv : -bv) - v0;
                    float t = l2 > 1e-12f ? (wu * du + wv * dv) / l2 : 0f;
                    if (t < 0f) t = 0f; else if (t > 1f) t = 1f;
                    float eu = wu - t * du, ev = wv - t * dv;
                    if (eu * eu + ev * ev <= r2) return true;
                }
                return false;
            }

            /// <summary>Liang-Barsky: warunek p * t <= q zaweza przedzial [t0, t1]; false = przedzial pusty.</summary>
            private static bool Clip(float p, float q, ref float t0, ref float t1)
            {
                if (p == 0f) return q >= 0f;
                float t = q / p;
                if (p < 0f) { if (t > t1) return false; if (t > t0) t0 = t; }
                else { if (t < t0) return false; if (t < t1) t1 = t; }
                return true;
            }

            private static float PtBox2(float u, float v, float bu, float bv)
            {
                float eu = Math.Abs(u) - bu, ev = Math.Abs(v) - bv;
                if (eu < 0f) eu = 0f;
                if (ev < 0f) ev = 0f;
                return eu * eu + ev * ev;
            }
        }

        private static volatile Grid _grid;            // siatka sciezki predkosci: same trakty (_main) albo trakty + drogi boczne
        internal static object GridNow { get { return _grid; } }   // p6-pola: widok pol poznaje zmiane siatki (pola nie na drodze - plan od nowa)
        /// <summary>p6-pola (recenzja): pas farby drogi na prostokacie pola - Grid.RectHit na siatce z chwili planu (GridNow); bez siatki false. Niezalezne od
        /// kluczy premii (MapRoadWideField / MapRoadWideMargin / MapSideRoadSpeedBonus) - drogi boczne sa w siatce zawsze, gdy plan widoku jest opublikowany.</summary>
        internal static bool PaintNear(object grid, float x, float y, float c, float s, float ha, float hb, float gap) { var g = grid as Grid; return g != null && g.RectHit(x, y, c, s, ha, hb, gap); }
        private static volatile Grid _main;            // n20-drogi: siatka samych traktow (Load)
        private static readonly object _pubLock = new object();   // n20-drogi: publikacja _grid / _main / stanu drog bocznych (watek glowny i watek budowy)
        private static SideRoadPlan.Result _sideRes;   // plan drog bocznych podany przez widok (pod _pubLock)
        private static float _sideHalf;
        private static int _sideVer;                    // numer zamowienia siatki wspolnej (pod _pubLock); stara budowa nie publikuje
        private static string _sideNote = "brak planu drog bocznych";
        private static volatile string _sidePending;    // linia logu z watku budowy - wypisuje watek glowny (FlushSideNote)
        /// <summary>n20-drogi: numer sesji (Reset) - widok drog bocznych podaje plan od nowa, gdy sie zmieni.</summary>
        internal static int Epoch;
        private static string _note = "nie wczytany";
        private static bool _cacheHooked;
        private static string _hookNote = "nie wpinana";
        private static readonly long[] _slots = new long[1 << SlotBits];

        // audyt: [ThreadStatic] - tylko watek glowny audytu; watki robocze gry widza false
        [ThreadStatic] private static bool _suppress;   // liczenie "bez premii drogi"
        [ThreadStatic] private static bool _noRecord;   // liczenie bez zapisu stanu do pamieci partii
        // straznik zapisu (recenzja, uwaga 1): partia, ktorej predkosc gra wlasnie liczy DO PAMIECI (CalculateSpeedForPartyUnified / getter SpeedExplained);
        // [ThreadStatic] - kazdy watek roboczy gry ma swoja; poprzednia wartosc wraca w finalizerze (__state), takze przy wyjatku
        [ThreadStatic] private static MobileParty _writer;
        private static int _writerHooks;               // 0..2 wpietych latek straznika; 2 = straznik czynny, inaczej zapis jak przed recenzja
        /// <summary>MarchPace.ApplyAll: liczba modeli predkosci z latka MarchPace.SpeedPostfix (niesie SpeedRoad); 0 = latka niewpieta przy starcie gry.</summary>
        internal static int SpeedModels;

        // liczniki z watkow roboczych (przyblizone - bez blokad, jak HorseState): przeliczenia wymuszone, wywolania postfiksu pamieci, probka czasu
        private static long _flips, _cacheCalls, _cacheTicks, _cacheSampled, _speedCalls, _speedOn, _speedSide, _recSkipped;
        private static int _parStumbles, _parStumblesAll;
        private static int _stumbles, _stumblesAll;
        private static Exception _firstPar;

        // spis partio-godzin (watek glowny): 0 rody, 1 karawany, 2 wozy wsi, 3 bandy, 4 inne, 5 gracz
        private static readonly int[] _hN = new int[6], _hOn = new int[6], _hSide = new int[6];
        private static int _hours;
        private static long _censusTicks;
        private static readonly Random _rng = new Random(1718);
        private static readonly List<MobileParty> _cand = new List<MobileParty>();
        private static readonly List<MobileParty> _candSide = new List<MobileParty>();
        private static AccessTools.FieldRef<MobileParty, float> _lastSpeedRef;
        private static bool _lastSpeedTried;

        internal static bool Ready { get { return _grid != null; } }

        /// <summary>Margines pola drogi (jedn. mapy poza krawedzia): n20-drogi - przy MapRoadWideField (domyslnie TAK, klucz NOWY) MapRoadWideMargin (1.0),
        /// inaczej dawny MapRoadMargin (0.4). Przyciete do 0..MaxMargin (siatka budowana na ten zasieg), NaN = 0. Sciezka predkosci - dwa odczyty pol.</summary>
        private static float MarginOf(Settings s)
        {
            float m = s.MapRoadWideField ? s.MapRoadWideMargin : s.MapRoadMargin;
            if (!(m > 0f)) return 0f;
            return m > MaxMargin ? MaxMargin : m;
        }

        /// <summary>n20-drogi: opis pola do linii logu (ktory klucz wyznacza margines).</summary>
        private static string FieldText(Settings s)
        {
            return s.MapRoadWideField
                ? "szerokie pole MapRoadWideField tak (MapRoadWideMargin " + s.MapRoadWideMargin.ToString("0.00", Inv) + "; waskie MapRoadMargin " + s.MapRoadMargin.ToString("0.00", Inv) + " nieuzywane)"
                : "szerokie pole MapRoadWideField NIE - waskie MapRoadMargin " + s.MapRoadMargin.ToString("0.00", Inv);
        }

        private static float PctOf(Settings s)
        {
            int p = s.MapRoadSpeedPercent;
            return (p < 0 ? 0 : p > MaxPercent ? MaxPercent : p) / 100f;
        }

        /// <summary>n20-drogi: premia drogi bocznej (MapSideRoadSpeedPercent przyciete do 0..50, NaN = 0).</summary>
        private static float SidePctOf(Settings s)
        {
            float p = s.MapSideRoadSpeedPercent;
            if (!(p > 0f)) return 0f;
            return (p > SideMaxPercent ? SideMaxPercent : p) / 100f;
        }

        /// <summary>n20-drogi: drogi boczne licza sie w premii - klucz MapSideRoadSpeedBonus, premia > 0 i siatka z drogami bocznymi.</summary>
        private static bool SideOn(Settings s, Grid g)
        {
            return g.SideSegs > 0 && s.MapSideRoadSpeedBonus && SidePctOf(s) > 0f;
        }

        /// <summary>Stan pamieci partii z rodzaju drogi: 1 poza droga, 2 trakt, 3 droga boczna.</summary>
        private static int StOf(int kind) { return kind == 2 ? 2 : kind == 1 ? 3 : 1; }

        /// <summary>m96-valyria: stan pamieci z rodzajem czynnym farby (eff z MapRoadKinds.Eff): 4 droga Valyrii, 5 szlak karawan, reszta jak StOf.</summary>
        private static int StOf(int kind, int eff) { return kind == 2 ? (eff == MapRoadKinds.Valyrian ? 4 : eff == MapRoadKinds.Caravan ? 5 : 2) : kind == 1 ? 3 : 1; }

        /// <summary>m96-valyria: rodzaj w punkcie z rodzajem farby - Kind, a dla farby (2) rodzaj czynny (MapRoadKinds.Eff). Most bez klucza - Kind go pomija.</summary>
        private static int KindEff(Grid g, int c, float x, float y, float margin, bool side, Settings s, out int eff)
        {
            int seg = -1;
            int kind = g.Empty(c) ? 0 : g.KindS(c, x, y, margin, side, out seg);
            eff = kind == 2 ? MapRoadKinds.Eff(s, g.ClsOf(seg)) : -1;
            return kind;
        }

        private static void ParStumble(Exception e)
        {
            Interlocked.Increment(ref _parStumbles);
            Interlocked.Increment(ref _parStumblesAll);
            if (_firstPar == null) _firstPar = e;
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumblesAll++;
            if (_stumblesAll <= 3) Log.Error("MapRoads." + where, e);
        }

        // ------------------------------------------------------------------ sciezka predkosci (watki robocze gry)
        /// <summary>MarchPace.SpeedPostfix (po suficie kolumny, ten sam poziom lancucha): partia ladowa na drodze +x% wyniku jako "Road" i sufit LimitMax
        /// w gore o ten sam procent (jak TownInns.SpeedBonus). Zapisuje stan drogi uzyty w tym liczeniu do pamieci partii (postfiks pamieci gry).</summary>
        internal static long _floorBonus;   // m105 G3: liczenia predkosci z premia od podlogi gry (watki robocze - Interlocked; tylko licznik, zeruje linia VR)

        internal static void SpeedRoad(MobileParty mp, ref ExplainedNumber r)
        {
            if (_suppress) return;
            var g = _grid;
            if (g == null) return;
            var s = Settings.Current;
            if (s == null || !s.MapRoadSpeedBonus) return;
            try
            {
                if (mp == null || !SpeedDepth.OutermostFinal) return;
                if (mp.IsCurrentlyAtSea || mp.IsTransitionInProgress) return;   // morze i przejscie lad-morze - prawa morza
                var p = mp.Position;
                float x = p.X, y = p.Y;
                int c = g.CellOf(x, y);
                // n20-drogi: rodzaj drogi (2 trakt, 1 droga boczna, 0 nic) - trakt wygrywa, premie sie nie sumuja; m96-valyria: eff - rodzaj farby
                int eff;
                int kind = KindEff(g, c, x, y, MarginOf(s), SideOn(s, g), s, out eff);
                // stan do pamieci partii tylko z liczenia, ktore gra zapisuje do _lastCalculatedSpeed (straznik zapisu); bez straznika - jak przed recenzja
                if (!_noRecord && !TerrainEase.ProbeActive && (_writerHooks < 2 || ReferenceEquals(_writer, mp))) Record(mp, c, StOf(kind, eff));
                else _recSkipped++;
                _speedCalls++;
                if (kind == 0) return;
                float pct;
                TextObject txt;
                if (kind == 2)
                {
                    _speedOn++;
                    // m96-valyria: droga Valyrii - co najmniej premia traktu; szlak karawan (glab Essos) - premia drogi bocznej
                    if (eff == MapRoadKinds.Valyrian) { MapRoadKinds.SpeedVal++; pct = MapRoadKinds.ValPct(s, PctOf(s)); txt = MapRoadKinds.TxtValyrian; }
                    else if (eff == MapRoadKinds.Caravan) { MapRoadKinds.SpeedCar++; pct = SidePctOf(s); txt = MapRoadKinds.TxtCaravan; }
                    else { pct = PctOf(s); txt = _txtRoad; }
                }
                else { _speedSide++; pct = SidePctOf(s); txt = _txtSide; }
                if (pct <= 0f) return;
                float res = r.ResultNumber;
                if (res <= 0f) return;
                float f = 1f + r.SumOfFactors;
                float add = res * pct;
                // m105 G3 (Map Road Bonus Above Floor): premia od predkosci PO podlodze gry - wynik bez podlogi (podstawa x czynniki) ponizej LimitMin dostawal res x pct
                // dodane do wyniku bez podlogi, a podloga ja zjadala (VR-3 24.98 / 24.26%). Dodatek do wyniku bez podlogi tak, zeby wynik z premia = max(wynik, podloga) x (1 + pct);
                // nigdy mniej niz dotad (res x pct). Partia ponad podloga - bez zmiany (u = res)
                if (s.MapRoadBonusAboveFloor)
                {
                    float u = r.BaseNumber * f;
                    float need = Math.Max(res, r.LimitMinValue) * (1f + pct) - u;
                    if (need > add + 1e-5f) { add = need; System.Threading.Interlocked.Increment(ref _floorBonus); }
                }
                r.Add(f > 0.01f ? add / f : add, txt);
                float lim = r.LimitMaxValue;
                if (lim > 0f && lim < float.MaxValue * 0.5f) r.LimitMax(lim * (1f + pct) + 0.001f);   // zapas 0.001 - bez linii "Maximum -0.00"
            }
            catch (Exception e) { ParStumble(e); }
        }

        /// <summary>p5-bloto (MapMud): rodzaj drogi w punkcie - 2 trakt, 1 droga boczna (przy MapSideRoadSpeedBonus), 0 poza droga, -1 brak siatki albo
        /// MapRoadSpeedBonus wylaczony. Ta sama siatka i ten sam margines co premia drogi; bez zapisu do pamieci partii (czyta watek roboczy i glowny).</summary>
        internal static int KindAt(float x, float y)
        {
            var g = _grid;
            if (g == null) return -1;
            var s = Settings.Current;
            if (s == null || !s.MapRoadSpeedBonus) return -1;
            int c = g.CellOf(x, y);
            return g.Empty(c) ? 0 : g.Kind(c, x, y, MarginOf(s), SideOn(s, g));
        }

        /// <summary>m96-valyria (AiRoadRoutes - koszt przejazdu): jak KindAt, ale farba z rodzajem czynnym - 2 trakt, 4 droga Valyrii, 5 szlak karawan; 1 boczna,
        /// 0 poza droga, -1 brak siatki / klucz. Bez zapisu do pamieci partii (watek roboczy).</summary>
        internal static int KindClassAt(float x, float y)
        {
            var g = _grid;
            if (g == null) return -1;
            var s = Settings.Current;
            if (s == null || !s.MapRoadSpeedBonus) return -1;
            int c = g.CellOf(x, y), eff;
            int kind = KindEff(g, c, x, y, MarginOf(s), SideOn(s, g), s, out eff);
            return kind == 2 ? (eff == MapRoadKinds.Valyrian ? 4 : eff == MapRoadKinds.Caravan ? 5 : 2) : kind;
        }

        /// <summary>m96-valyria (AiRoadRoutes): premie do kosztu - droga Valyrii (co najmniej trakt) i szlak karawan (jak droga boczna).</summary>
        internal static float ValPctNow(Settings s) { return MapRoadKinds.ValPct(s, PctOf(s)); }
        internal static float CarPctNow(Settings s) { return SidePctOf(s); }

        /// <summary>p5-bloto (MapMud): czy to liczenie gra zapisuje do pamieci predkosci (_lastCalculatedSpeed) - ten sam straznik zapisu co stan drogi.</summary>
        internal static bool Recording(MobileParty mp)
        {
            return !_noRecord && !TerrainEase.ProbeActive && (_writerHooks < 2 || ReferenceEquals(_writer, mp));
        }

        /// <summary>p6-szubienice (Gallows): plan drog bocznych podany przez widok mapy (null - brak planu, drogi boczne wylaczone albo nowa sesja). Tylko odczyt.</summary>
        internal static SideRoadPlan.Result SidePlan() { lock (_pubLock) return _sideRes; }

        /// <summary>Pamiec partii: Party.Index | komorka + 1 (tylko do logu zmian, porownanie idzie po stanie) | stan (1 poza droga, 2 trakt, 3 boczna).</summary>
        private static void Record(MobileParty mp, int cell, int st)
        {
            int idx = mp.Party.Index;
            long v = ((long)idx << 32) | ((long)((cell + 1) & 0xFFFFFF) << 3) | (long)(st & 7);   // m96-valyria: stan 3 bity (4 Valyria, 5 szlak)
            Volatile.Write(ref _slots[idx & SlotMask], v);
        }

        /// <summary>Postfiks MobileParty.IsLastSpeedCacheInvalid (prywatna, DeclaredMethod): gra uznala pamiec predkosci za dobra - sprawdzamy, czy stan
        /// drogi w biezacym punkcie jest ten sam co przy ostatnim liczeniu; inny = pamiec niewazna (gra liczy predkosc od nowa, SpeedRoad zapisze nowy stan).</summary>
        public static void CachePostfix(MobileParty __instance, ref bool __result)
        {
            if (__result) return;
            var g = _grid;
            if (g == null) return;
            var s = Settings.Current;
            if (s == null || !s.MapRoadSpeedBonus) return;
            long t0 = (++_cacheCalls & 63) == 1 ? Stopwatch.GetTimestamp() : 0L;
            try
            {
                var mp = __instance;
                if (mp == null || mp.IsCurrentlyAtSea || mp.IsTransitionInProgress) return;
                int idx = mp.Party.Index;
                long v = Volatile.Read(ref _slots[idx & SlotMask]);
                if ((int)(v >> 32) != idx) return;          // inna partia w slocie albo jeszcze nie liczona - decyduje gra
                int st = (int)(v & 7);
                if (st == 0) return;
                var p = mp.Position;
                float x = p.X, y = p.Y;
                int c = g.CellOf(x, y);
                // komorka bez drog - O(1); n20-drogi: porownanie rodzaju drogi (trakt / boczna / nic) z biezacej siatki - po podmianie siatki (drogi boczne
                // dolozone albo zdjete) inny rodzaj = przeliczenie, tak samo jak wejscie na droge; m96-valyria: takze trakt / Valyria / szlak
                int eff;
                int kind = KindEff(g, c, x, y, MarginOf(s), SideOn(s, g), s, out eff);
                if (StOf(kind, eff) != st) { __result = true; _flips++; }
            }
            catch (Exception e) { ParStumble(e); }
            finally { if (t0 != 0) { _cacheTicks += Stopwatch.GetTimestamp() - t0; _cacheSampled++; } }
        }

        /// <summary>Straznik zapisu - prefiks MobileParty.CalculateSpeedForPartyUnified (prywatna) i gettera SpeedExplained: gra liczy predkosc tej partii DO
        /// PAMIECI (_lastCalculatedSpeed); SpeedRoad zapisze stan drogi tylko wtedy. Poprzednia wartosc w __state (zagniezdzenie innej partii).</summary>
        public static void WriterPrefix(MobileParty __instance, out MobileParty __state)
        {
            __state = _writer;
            _writer = __instance;
        }

        /// <summary>Straznik zapisu - finalizer (biegnie takze przy wyjatku, wyjatek gry idzie dalej bez zmian): przywraca poprzednia partie watku.</summary>
        public static void WriterFinalizer(MobileParty __state)
        {
            _writer = __state;
        }

        private static bool HasOurs(MethodBase m, string patchName, bool finalizer)
        {
            var pi = Harmony.GetPatchInfo(m);
            if (pi == null) return false;
            foreach (var x in finalizer ? pi.Finalizers : pi.Prefixes)
                if (x.PatchMethod != null && x.PatchMethod.DeclaringType == typeof(MapRoads) && x.PatchMethod.Name == patchName) return true;
            return false;
        }

        /// <summary>Straznik zapisu: prefiks + finalizer na obu metodach zapisujacych pamiec predkosci gry. Obie albo zadna (jedna wpieta = straznik dziurawy -
        /// zdjeta tym samym MethodInfo; zapis stanu jak przed recenzja).</summary>
        private static void HookWriters(Harmony h)
        {
            _writerHooks = 0;
            var pre = typeof(MapRoads).GetMethod("WriterPrefix", BindingFlags.Public | BindingFlags.Static);
            var fin = typeof(MapRoads).GetMethod("WriterFinalizer", BindingFlags.Public | BindingFlags.Static);
            var targets = new MethodBase[]
            {
                AccessTools.DeclaredMethod(typeof(MobileParty), "CalculateSpeedForPartyUnified"),
                AccessTools.DeclaredPropertyGetter(typeof(MobileParty), "SpeedExplained")
            };
            var done = new List<MethodBase>(2);
            string why = "";
            foreach (var t in targets)
            {
                if (t == null) { why += " brak metody;"; continue; }
                try
                {
                    h.Patch(t, prefix: new HarmonyMethod(pre), finalizer: new HarmonyMethod(fin));
                    if (HasOurs(t, "WriterPrefix", false) && HasOurs(t, "WriterFinalizer", true)) done.Add(t);
                    else why += " " + t.Name + " bez latki po Patch;";
                }
                catch (Exception e) { why += " " + t.Name + " " + e.GetType().Name + ";"; Log.Error("MapRoads.HookWriters " + t.Name, e); }
            }
            if (done.Count == targets.Length) _writerHooks = done.Count;
            else
            {
                foreach (var t in done)
                {
                    try { h.Unpatch(t, pre); h.Unpatch(t, fin); } catch (Exception e) { Log.Error("MapRoads.HookWriters Unpatch " + t.Name, e); }
                }
            }
            Log.Info(P + "straznik zapisu stanu drogi (MobileParty.CalculateSpeedForPartyUnified, getter SpeedExplained) " + _writerHooks + "/2 "
                     + (_writerHooks == 2 ? "wpiety - stan drogi zapisywany tylko w liczeniu, ktore gra zapisuje do pamieci predkosci (model wolany wprost - bez zapisu)."
                                          : "NIEWPIETY (" + why.Trim() + ") - stan drogi zapisywany przy kazdym liczeniu (jak przed recenzja)."));
        }

        // ------------------------------------------------------------------ latka (menu glowne, SubModuleMain - po MarchPace.ApplyAll)
        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var c = Settings.Current;
                if (c == null || !c.MapRoadSpeedBonus)
                {
                    _hookNote = "NIE wpieta (MapRoadSpeedBonus wylaczony przy starcie gry)";
                    Log.Info(P + "premia drog wylaczona przy starcie gry (MapRoadSpeedBonus) - latka pamieci predkosci MobileParty.IsLastSpeedCacheInvalid niewpieta.");
                    return;
                }
                var m = AccessTools.DeclaredMethod(typeof(MobileParty), "IsLastSpeedCacheInvalid");
                if (m == null)
                {
                    _hookNote = "NIE wpieta (brak metody MobileParty.IsLastSpeedCacheInvalid)";
                    Log.Info(P + "BRAK metody MobileParty.IsLastSpeedCacheInvalid - premia drog liczy sie tylko przy zmianie sciany siatki (pamiec predkosci gry).");
                    return;
                }
                var post = new HarmonyMethod(typeof(MapRoads).GetMethod("CachePostfix", BindingFlags.Public | BindingFlags.Static));
                h.Patch(m, postfix: post);
                var pi = Harmony.GetPatchInfo(m);
                bool ok = false;
                if (pi != null) foreach (var x in pi.Postfixes) if (x.PatchMethod != null && x.PatchMethod.DeclaringType == typeof(MapRoads)) { ok = true; break; }
                _cacheHooked = ok;
                _hookNote = ok ? "wpieta" : "NIE wpieta (brak postfiksu po Patch)";
                Log.Info(P + "latka pamieci predkosci MobileParty.IsLastSpeedCacheInvalid " + (ok ? "wpieta" : "NIE wpieta (brak postfiksu po Patch)")
                         + " (premia drogi przeliczana przy wejsciu na droge i zejsciu z niej, nie tylko przy zmianie sciany siatki).");
                // straznik zapisu (recenzja, uwaga 1) potrzebny tylko postfiksowi pamieci - bez niego latki straznika bylyby samym kosztem
                if (ok) HookWriters(h);
                MapMud.ApplyCache(h, m);   // p5-bloto (R3-07c): postfiks pamieci stopnia pogody (ulewa <-> mokro, snieg <-> deszcz) na tej samej metodzie
            }
            catch (Exception e) { _hookNote = "NIE wpieta (" + e.GetType().Name + ")"; Log.Error("MapRoads.ApplyAll", e); }
        }

        // ------------------------------------------------------------------ plik i siatka (watek glowny)
        internal static void Reset()
        {
            lock (_pubLock)
            {
                _grid = null;
                _main = null;
                _sideRes = null;   // n20-drogi: plan drog bocznych z poprzedniej sesji nieaktualny; budowa w toku nie opublikuje (numer zamowienia)
                _sideVer++;
                Epoch++;
                _sideNote = "brak planu drog bocznych";
            }
            _sidePending = null;
            _note = "nie wczytany";
            Array.Clear(_slots, 0, _slots.Length);
            ZeroDay();
            _parStumbles = _parStumblesAll = 0; _firstPar = null;
            _stumbles = _stumblesAll = 0;
        }

        private static void ZeroDay()
        {
            for (int i = 0; i < _hN.Length; i++) { _hN[i] = 0; _hOn[i] = 0; _hSide[i] = 0; }
            _hours = 0; _censusTicks = 0;
            ZeroSpeed();
        }

        /// <summary>Liczniki sciezki predkosci i potkniec doby (bez spisu godzinnego - ten zbiera tylko w dobach pomiaru i zeruje sie po linii).</summary>
        private static void ZeroSpeed()
        {
            _flips = 0; _cacheCalls = 0; _cacheTicks = 0; _cacheSampled = 0; _speedCalls = 0; _speedOn = 0; _speedSide = 0; _recSkipped = 0;
            _parStumbles = 0; _stumbles = 0;
        }

        /// <summary>Stan premii do linii startu sesji i doby: CZYNNA / NIECZYNNA z powodem (bez siatki, bez latki predkosci MarchPace).</summary>
        private static string Status(Grid g)
        {
            if (g == null) return "NIECZYNNA";
            if (SpeedModels <= 0) return "NIECZYNNA (latka predkosci niewpieta przy starcie gry - klucz wlaczony w trakcie gry, potrzebny restart gry)";
            return _cacheHooked ? "CZYNNA" : "CZYNNA (bez latki pamieci - tylko przy zmianie sciany siatki)";
        }

        /// <summary>Start sesji kampanii (OnSessionLaunched, po McmSettings.Apply): pamiec partii od zera, plik wczytany do siatki (przy wlaczonym kluczu).</summary>
        internal static void SessionStart()
        {
            Reset();
            var s = Settings.Current;
            if (s == null || !s.MapRoadSpeedBonus)
            {
                Log.Info(P + "start sesji - premia drog WYLACZONA (MapRoadSpeedBonus) - plik " + FileName + " niewczytany, predkosc bez zmian.");
                return;
            }
            long t0 = Stopwatch.GetTimestamp();
            Load();
            double ms = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
            var g = _grid;
            Log.Info(P + "start sesji - klucz MapRoadSpeedBonus tak, premia " + s.MapRoadSpeedPercent.ToString(Inv) + "% (przyciete " + (PctOf(s) * 100f).ToString("0", Inv)
                     + "%), margines " + MarginOf(s).ToString("0.00", Inv) + " jedn. | " + _note
                     + (g != null ? " | siatka komorek " + CellSize.ToString("0", Inv) + " jedn.: " + g.Nx + " x " + g.Ny + ", z drogami " + g.CellsUsed + ", wpisow " + g.Entries
                                    + ", maks. odcinkow na komorke " + g.MaxPerCell : "")
                     + " | latka pamieci predkosci " + _hookNote + " | straznik zapisu stanu " + _writerHooks + "/2 | latka predkosci MarchPace w " + SpeedModels + " modelach"
                     + " | wczytanie " + ms.ToString("0.0", Inv) + " ms"
                     + " | premia " + Status(g) + ".");
            // n20-drogi: osobna linia (linia startu sesji bez zmian - czyta ja sprawdz_logi.py, grupa ndrogi)
            Log.Info(P + "drogi boczne - start sesji: klucz MapSideRoadSpeedBonus " + (s.MapSideRoadSpeedBonus ? "tak" : "nie") + ", premia " + (SidePctOf(s) * 100f).ToString("0.0", Inv)
                     + "%, margines " + MarginOf(s).ToString("0.00", Inv) + " jedn. (wspolny z traktami; " + FieldText(s) + ") - czekaja na plan drog bocznych z widoku mapy (klucz MapSideRoads "
                     + (s.MapSideRoads ? "tak" : "nie") + ").");
            MapRoadKinds.SessionLine(s, g != null);   // m96-valyria: osobna linia "Drogi Valyrii (VR): start sesji" (linie DR bez zmian)
        }

        private static void Load()
        {
            try
            {
                string path = MapVillageData.DataPathFor(FileName);
                if (path == null || !File.Exists(path)) { _note = "BRAK pliku ModuleData\\" + FileName + " - premia drog nieczynna"; return; }
                var xs = new List<float>(11000);
                var ys = new List<float>(11000);
                var ws = new List<float>(11000);
                var ids = new List<int>(11000);
                int bad = 0, version = 0;
                long tsize = 0;
                string md5 = "-";
                var cols = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (string raw in File.ReadAllText(path, Encoding.UTF8).Replace("\r", "").Split('\n'))
                {
                    if (raw.Length == 0) continue;
                    if (raw[0] == '#')
                    {
                        string h = raw.TrimStart('#').Trim();
                        if (h.StartsWith("version:", StringComparison.Ordinal)) int.TryParse(h.Substring(8).Trim(), NumberStyles.Integer, Inv, out version);
                        else if (h.StartsWith("terrain_size:", StringComparison.Ordinal)) long.TryParse(h.Substring(13).Trim(), NumberStyles.Integer, Inv, out tsize);
                        else if (h.StartsWith("terrain_md5:", StringComparison.Ordinal)) md5 = h.Substring(12).Trim();
                        else if (h.StartsWith("road\t", StringComparison.Ordinal))
                        {
                            string[] hc = h.Split('\t');
                            for (int i = 0; i < hc.Length; i++) cols[hc[i].Trim()] = i;
                        }
                        continue;
                    }
                    try
                    {
                        string[] c = raw.Split('\t');
                        int id = int.Parse(c[Col(cols, "road", 0)], Inv);
                        float x = float.Parse(c[Col(cols, "x", 2)], Inv), y = float.Parse(c[Col(cols, "y", 3)], Inv);
                        float w = float.Parse(c[Col(cols, "width", 4)], Inv);
                        if (float.IsNaN(x) || float.IsNaN(y) || float.IsInfinity(x) || float.IsInfinity(y)) { bad++; continue; }
                        ids.Add(id); xs.Add(x); ys.Add(y); ws.Add(float.IsNaN(w) ? 0f : w);
                    }
                    catch { bad++; }
                }
                if (version < 1) { _note = "plik " + FileName + " bez wersji w naglowku - premia drog nieczynna"; return; }
                // straznik mapy: rozmiar terrain.bin ROT-Map z naglowka
                string tnote;
                string tb = TerrainPath();
                if (tsize > 0 && tb != null && File.Exists(tb))
                {
                    long now = new FileInfo(tb).Length;
                    if (now != tsize)
                    {
                        _note = "plik " + FileName + " v" + version + " z INNEJ MAPY: terrain.bin ROT-Map ma " + now + " B, dane z " + tsize + " B (terrain_md5 " + md5
                                + ") - premia drog nieczynna (dane do odswiezenia: tools/geo/geo_drogi_rot.py + tools/drogi/drogi_plik_modu.py)";
                        return;
                    }
                    tnote = "terrain.bin ROT-Map zgodny (" + now + " B)";
                }
                else tnote = tsize > 0 ? "terrain.bin ROT-Map nie znaleziony - bez straznika mapy" : "naglowek bez terrain_size - bez straznika mapy";
                var g = Build(ids, xs, ys, ws, path);
                g.Version = version; g.Md5 = md5; g.TerrainSize = tsize;
                lock (_pubLock) { _main = g; _grid = g; }   // n20-drogi: siatka samych traktow; drogi boczne dokladane osobno (SidePublish)
                _note = "plik " + FileName + " v" + version + ": lamanych " + g.Lines + ", punktow " + g.Points + ", odcinkow " + g.Segments + ", dlugosc "
                        + g.Length.ToString("0.0", Inv) + " jedn., zle wiersze " + bad + "; " + tnote;
            }
            catch (Exception e) { lock (_pubLock) { _grid = null; _main = null; } _note = "blad wczytania " + FileName + " (" + e.GetType().Name + ") - premia drog nieczynna"; Stumble("Load", e); }
        }

        private static int Col(Dictionary<string, int> cols, string name, int def)
        {
            int i;
            return cols.TryGetValue(name, out i) ? i : def;
        }

        private static string TerrainPath()
        {
            try
            {
                string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);   // Modules\Armoury\bin\Win64_Shipping_Client
                string p = Path.GetFullPath(Path.Combine(dir, "..", "..", "..", "ROT-Map", "SceneObj", "Main_map", "terrain.bin"));
                if (File.Exists(p)) return p;
            }
            catch { }
            try { return Path.Combine(BasePath.Name, "Modules", "ROT-Map", "SceneObj", "Main_map", "terrain.bin"); }
            catch { return null; }
        }

        private static Grid Build(List<int> ids, List<float> xs, List<float> ys, List<float> ws, string path)
        {
            var g = new Grid();
            int n = xs.Count;
            g.Points = n;
            // odcinki: kolejne punkty tej samej lamanej (plik posortowany po road, seq)
            var sa = new List<int>(n);
            int lines = 0;
            for (int i = 0; i < n; i++)
            {
                if (i == 0 || ids[i] != ids[i - 1]) { lines++; continue; }
                sa.Add(i);
            }
            int m = sa.Count;
            g.Lines = lines; g.Segments = m;
            // m96-valyria: kolejnosc odcinkow V, mosty Valyrii, trakty, szlaki karawan (null = bez rodzajow - kolejnosc i odcinki jak przed zmiana)
            byte[] cls; float[] br;
            int[] ord = MapRoadKinds.Order(path, ids, xs, ys, ws, sa, HalfMin, HalfMax, out cls, out br);
            int mt = ord != null ? ord.Length : m;
            g.Ax = new float[mt]; g.Ay = new float[mt]; g.Dx = new float[mt]; g.Dy = new float[mt]; g.InvL2 = new float[mt]; g.Half = new float[mt];
            g.BridgeLo = g.BridgeHi = -1;
            double len = 0;
            for (int k = 0; k < mt; k++)
            {
                int o = ord != null ? ord[k] : k;
                float ax, ay, dx, dy, half;
                if (o >= 0)
                {
                    int i = sa[o];
                    ax = xs[i - 1]; ay = ys[i - 1]; dx = xs[i] - ax; dy = ys[i] - ay;
                    half = ws[i] * 0.5f;
                    if (half < HalfMin) half = HalfMin; else if (half > HalfMax) half = HalfMax;
                }
                else
                {
                    int b = (-o - 1) * 5;   // most Valyrii przez przerwe farby (MapRoadKinds.Order)
                    ax = br[b]; ay = br[b + 1]; dx = br[b + 2] - ax; dy = br[b + 3] - ay; half = br[b + 4];
                    if (g.BridgeLo < 0) g.BridgeLo = k;
                    g.BridgeHi = k + 1;
                    g.Bridges++;
                }
                float l2 = dx * dx + dy * dy;
                g.Ax[k] = ax; g.Ay[k] = ay; g.Dx[k] = dx; g.Dy[k] = dy; g.InvL2[k] = l2 > 1e-8f ? 1f / l2 : 0f; g.Half[k] = half;
                if (o >= 0) len += Math.Sqrt(l2);
            }
            g.Cls = cls;
            g.Length = len;
            g.MainSegs = mt;
            Index(g, mt);
            return g;
        }

        /// <summary>Siatka komorek CellSize x CellSize z odcinkow g.Ax..g.Half [0, m): odcinek wpisany do kazdej komorki, ktora siega prostokat jego kapsuly
        /// (pol-szerokosc + MaxMargin). Odcinki wpisywane rosnaco - w kazdej komorce najpierw trakty (0..MainSegs-1), potem drogi boczne (n20-drogi).</summary>
        private static void Index(Grid g, int m)
        {
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            for (int k = 0; k < m; k++)
            {
                float ax = g.Ax[k], ay = g.Ay[k], dx = g.Dx[k], dy = g.Dy[k];
                minX = Math.Min(minX, Math.Min(ax, ax + dx)); maxX = Math.Max(maxX, Math.Max(ax, ax + dx));
                minY = Math.Min(minY, Math.Min(ay, ay + dy)); maxY = Math.Max(maxY, Math.Max(ay, ay + dy));
            }
            float pad = HalfMax + MaxMargin + CellSize;
            if (m == 0) { minX = minY = 0f; maxX = maxY = 1f; }
            g.X0 = minX - pad; g.Y0 = minY - pad; g.Inv = 1f / CellSize;
            g.Nx = Math.Max(1, (int)Math.Ceiling((maxX + pad - g.X0) / CellSize));
            g.Ny = Math.Max(1, (int)Math.Ceiling((maxY + pad - g.Y0) / CellSize));
            if ((long)g.Nx * g.Ny >= 0xFFFFFFL) throw new InvalidOperationException("siatka drog za duza: " + g.Nx + " x " + g.Ny);   // komorka + 1 w 24 bitach pamieci partii
            int cells = g.Nx * g.Ny;
            var count = new int[cells + 1];
            for (int pass = 0; pass < 2; pass++)
            {
                int[] fill = pass == 1 ? new int[cells] : null;
                if (pass == 1)
                {
                    g.Start = new int[cells + 1];
                    for (int c = 0; c < cells; c++) g.Start[c + 1] = g.Start[c] + count[c];
                    g.Seg = new int[g.Start[cells]];
                }
                for (int k = 0; k < m; k++)
                {
                    float reach = g.Half[k] + MaxMargin;
                    float x0 = Math.Min(g.Ax[k], g.Ax[k] + g.Dx[k]) - reach, x1 = Math.Max(g.Ax[k], g.Ax[k] + g.Dx[k]) + reach;
                    float y0 = Math.Min(g.Ay[k], g.Ay[k] + g.Dy[k]) - reach, y1 = Math.Max(g.Ay[k], g.Ay[k] + g.Dy[k]) + reach;
                    int cx0 = Math.Max(0, (int)((x0 - g.X0) * g.Inv)), cx1 = Math.Min(g.Nx - 1, (int)((x1 - g.X0) * g.Inv));
                    int cy0 = Math.Max(0, (int)((y0 - g.Y0) * g.Inv)), cy1 = Math.Min(g.Ny - 1, (int)((y1 - g.Y0) * g.Inv));
                    // n20-drogi: komorka, ktorej srodek jest dalej od odcinka niz zasieg + pol przekatnej komorki, nie ma punktu w zasiegu - bez wpisu
                    // (rogi prostokata ukosnego odcinka; wynik zapytan bez zmian, krotsze listy komorek)
                    float rc = reach + CellSize * 0.7072f, rc2 = rc * rc;
                    for (int cy = cy0; cy <= cy1; cy++)
                        for (int cx = cx0; cx <= cx1; cx++)
                        {
                            float qx = g.X0 + (cx + 0.5f) * CellSize - g.Ax[k], qy = g.Y0 + (cy + 0.5f) * CellSize - g.Ay[k];
                            float t = (qx * g.Dx[k] + qy * g.Dy[k]) * g.InvL2[k];
                            if (t < 0f) t = 0f; else if (t > 1f) t = 1f;
                            float ox = qx - t * g.Dx[k], oy = qy - t * g.Dy[k];
                            if (ox * ox + oy * oy > rc2) continue;
                            int c = cy * g.Nx + cx;
                            if (pass == 0) count[c]++;
                            else g.Seg[g.Start[c] + fill[c]++] = k;
                        }
                }
            }
            int used = 0, maxc = 0;
            for (int c = 0; c < cells; c++) { int q = count[c]; if (q > 0) used++; if (q > maxc) maxc = q; }
            g.CellsUsed = used; g.Entries = g.Seg.Length; g.MaxPerCell = maxc;
        }

        // ------------------------------------------------------------------ n20-drogi: drogi boczne w premii (plan z widoku mapy)
        /// <summary>Widok drog bocznych (watek glowny, MapSideRoadsView po skonczonym planie): plan do premii predkosci. Siatke wspolna (trakty + drogi boczne)
        /// buduje watek z puli; publikuje ja tylko najnowsze zamowienie przy tej samej siatce traktow. Bez siatki traktow (premia drog wylaczona albo plik
        /// niewczytany) plan czeka - Daily po wczytaniu traktow zamawia budowe.</summary>
        internal static void SidePublish(SideRoadPlan.Result res, float width)
        {
            if (res == null) return;
            int ver;
            Grid main;
            float half = width * 0.5f;
            if (!(half >= SideHalfMin)) half = SideHalfMin; else if (half > SideHalfMax) half = SideHalfMax;
            lock (_pubLock)
            {
                _sideRes = res;
                _sideHalf = half;
                ver = ++_sideVer;
                main = _main;
                if (main == null) _sideNote = "plan jest (drog " + res.Roads.Count + "), czeka na siatke traktow";
            }
            if (main == null) return;
            QueueSide(ver, main, res, half);
        }

        /// <summary>Widok drog bocznych (watek glowny): drogi boczne zdjete z mapy (wylaczone w MCM, plan od nowa, koniec mapy) - z premii tez; wraca siatka
        /// samych traktow. Tylko gdy w premii jest wlasnie ten plan (stary widok nie zdejmuje planu nowego).</summary>
        internal static void SideClear(SideRoadPlan.Result res, string why)
        {
            if (res == null) return;
            bool was;
            lock (_pubLock)
            {
                if (!ReferenceEquals(_sideRes, res)) return;
                was = _grid != null && _grid.SideSegs > 0;
                _sideRes = null;
                _sideVer++;
                if (_main != null) _grid = _main;
                _sideNote = "zdjete (" + why + ")";
            }
            if (was) Log.Info(P + "drogi boczne zdjete z premii predkosci (" + why + ") - premia tylko na traktach ROT do nastepnego planu drog bocznych.");
        }

        /// <summary>Watek glowny (tick widoku drog bocznych i Daily): linia logu z watku budowy siatki wspolnej.</summary>
        internal static void FlushSideNote()
        {
            string n = _sidePending;
            if (n == null) return;
            _sidePending = null;
            Log.Info(n);
        }

        private static void QueueSide(int ver, Grid main, SideRoadPlan.Result res, float half)
        {
            try { ThreadPool.QueueUserWorkItem(_ => SideWork(ver, main, res, half)); }
            catch (Exception e) { Stumble("QueueSide", e); SideWork(ver, main, res, half); }
        }

        private static void SideWork(int ver, Grid main, SideRoadPlan.Result res, float half)
        {
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                var g = Combine(main, res, half);
                double ms = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
                bool done = false;
                lock (_pubLock)
                {
                    if (ver == _sideVer && ReferenceEquals(main, _main) && ReferenceEquals(res, _sideRes))
                    {
                        _grid = g;
                        done = true;
                        _sideNote = "w premii (drog " + g.SideRoads + ", odcinkow " + g.SideSegs + ")";
                    }
                }
                var s = Settings.Current;
                _sidePending = P + "drogi boczne w premii predkosci " + (done ? "TAK" : "NIE (nowsze zamowienie albo inna siatka traktow - zostaje biezaca)")
                               + " - drog " + g.SideRoads + ", odcinkow " + g.SideSegs + " (co ok. " + SideStep.ToString("0.0", Inv) + " jedn.), dlugosc "
                               + g.SideLength.ToString("0", Inv) + " jedn., pol-szerokosc " + g.SideHalf.ToString("0.00", Inv) + " + margines "
                               + (s != null ? MarginOf(s).ToString("0.00", Inv) : "-") + " jedn. | siatka wspolna: " + g.Nx + " x " + g.Ny + " komorek po "
                               + CellSize.ToString("0", Inv) + " jedn., z drogami " + g.CellsUsed + ", wpisow " + g.Entries + ", maks. odcinkow na komorke " + g.MaxPerCell
                               + " | budowa " + ms.ToString("0.0", Inv) + " ms (watek z puli) | premia drog bocznych " + (s != null ? (SidePctOf(s) * 100f).ToString("0.0", Inv) : "-")
                               + "% (klucz MapSideRoadSpeedBonus " + (s != null && s.MapSideRoadSpeedBonus ? "tak" : "nie") + "), trakty " + (s != null ? (PctOf(s) * 100f).ToString("0", Inv) : "-")
                               + "% (trakt wygrywa, premie sie nie sumuja).";
            }
            catch (Exception e)
            {
                lock (_pubLock) { if (ver == _sideVer) _sideNote = "budowa nieudana (" + e.GetType().Name + ")"; }
                _sidePending = P + "drogi boczne w premii predkosci NIE - budowa siatki wspolnej nieudana (" + e.GetType().Name + ": " + (e.Message ?? "") + ") - premia tylko na traktach.";
            }
        }

        /// <summary>Siatka wspolna: kopie odcinkow traktow (indeksy 0..M-1, bez zmian) + odcinki drog bocznych planu (os co 0.25 jedn. przerzedzona do
        /// punktow co >= SideStep, ostatni punkt zawsze). Czysta geometria - watek z puli; plan i siatka traktow tylko czytane.</summary>
        private static Grid Combine(Grid main, SideRoadPlan.Result res, float half)
        {
            int m = main.MainSegs;
            var sx = new List<float>(32000); var sy = new List<float>(32000); var ex = new List<float>(32000); var ey = new List<float>(32000);
            int roads = 0;
            double len = 0;
            foreach (var rd in res.Roads)
            {
                if (rd == null || rd.X == null || rd.Y == null) continue;
                int n = Math.Min(rd.X.Length, rd.Y.Length);
                if (n < 2) continue;
                float lx = rd.X[0], ly = rd.Y[0];
                int before = sx.Count;
                for (int i = 1; i < n; i++)
                {
                    float x = rd.X[i], y = rd.Y[i];
                    if (float.IsNaN(x) || float.IsNaN(y) || float.IsInfinity(x) || float.IsInfinity(y)) continue;
                    float dx = x - lx, dy = y - ly;
                    float d2 = dx * dx + dy * dy;
                    if (d2 < SideStep * SideStep && i < n - 1) continue;
                    if (d2 > 1e-8f) { sx.Add(lx); sy.Add(ly); ex.Add(x); ey.Add(y); len += Math.Sqrt(d2); }
                    lx = x; ly = y;
                }
                if (sx.Count > before) roads++;
            }
            int sn = sx.Count, all = m + sn;
            var g = new Grid
            {
                Lines = main.Lines, Points = main.Points, Segments = main.Segments, Length = main.Length, Version = main.Version, Md5 = main.Md5,
                TerrainSize = main.TerrainSize, MainSegs = m, SideSegs = sn, SideRoads = roads, SideLength = len, SideHalf = half,
                Cls = main.Cls, BridgeLo = main.BridgeLo, BridgeHi = main.BridgeHi, Bridges = main.Bridges   // m96-valyria: rodzaje traktow (indeksy 0..M-1 bez zmian)
            };
            g.Ax = new float[all]; g.Ay = new float[all]; g.Dx = new float[all]; g.Dy = new float[all]; g.InvL2 = new float[all]; g.Half = new float[all];
            Array.Copy(main.Ax, g.Ax, m); Array.Copy(main.Ay, g.Ay, m); Array.Copy(main.Dx, g.Dx, m); Array.Copy(main.Dy, g.Dy, m);
            Array.Copy(main.InvL2, g.InvL2, m); Array.Copy(main.Half, g.Half, m);
            for (int j = 0; j < sn; j++)
            {
                int k = m + j;
                float ax = sx[j], ay = sy[j], dx = ex[j] - ax, dy = ey[j] - ay;
                float l2 = dx * dx + dy * dy;
                g.Ax[k] = ax; g.Ay[k] = ay; g.Dx[k] = dx; g.Dy[k] = dy; g.InvL2[k] = l2 > 1e-8f ? 1f / l2 : 0f; g.Half[k] = half;
            }
            Index(g, all);
            return g;
        }

        private static string SideText(Grid g)
        {
            string note;
            lock (_pubLock) note = _sideNote;
            return g != null && g.SideSegs > 0 ? "w premii (drog " + g.SideRoads + ", odcinkow " + g.SideSegs + ")" : "NIE w premii - " + note;
        }

        // ------------------------------------------------------------------ pomiar (watek glowny)
        /// <summary>Spis godzinny w dobach pomiaru (LogGate.Today): partie ladowe w ruchu (bez doczepionych do armii) - ile z nich na drodze, wedlug rodzaju.</summary>
        internal static void Hourly()
        {
            var g = _grid;
            var s = Settings.Current;
            if (g == null || s == null || !s.MapRoadSpeedBonus || Campaign.Current == null || !LogGate.Today) return;
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                float margin = MarginOf(s);
                bool side = SideOn(s, g);
                var all = MobileParty.All;
                var main = MobileParty.MainParty;
                for (int i = 0; i < all.Count; i++)
                {
                    var mp = all[i];
                    try
                    {
                        if (mp == null || !mp.IsActive || mp.AttachedTo != null || mp.IsCurrentlyAtSea || mp.IsTransitionInProgress || !mp.IsMoving) continue;
                        int k = mp == main ? 5 : mp.IsLordParty ? 0 : mp.IsCaravan ? 1 : mp.IsVillager ? 2 : mp.IsBandit ? 3 : 4;
                        var p = mp.Position;
                        int c = g.CellOf(p.X, p.Y);
                        int eff;
                        int kind = KindEff(g, c, p.X, p.Y, margin, side, s, out eff);
                        _hN[k]++;
                        if (kind == 2) _hOn[k]++;
                        else if (kind == 1) _hSide[k]++;   // n20-drogi: na drodze bocznej (nie na trakcie)
                        MapRoadKinds.Census(eff);          // m96-valyria: spis drog Valyrii / szlakow karawan (linia "Drogi Valyrii (VR)")
                    }
                    catch (Exception e) { Stumble("Hourly", e); }
                }
                _hours++;
            }
            catch (Exception e) { Stumble("Hourly", e); }
            finally { _censusTicks += Stopwatch.GetTimestamp() - t0; }
        }

        private static string Pct(int a, int b) { return b > 0 ? (100.0 * a / b).ToString("0.0", Inv) : "-"; }

        /// <summary>Takt doby (po TerrainEase.Stage7Daily): w dobie linii (LogGate.Lines) audyt i linia "Drogi (DR): dzien N" za dobe spisu; potem zerowanie.</summary>
        internal static void Daily()
        {
            var s = Settings.Current;
            if (s == null) return;
            try
            {
                FlushSideNote();
                if (s.MapRoadSpeedBonus && _grid == null && _note == "nie wczytany")
                {
                    Load();   // klucz wlaczony w MCM w trakcie sesji - plik wczytany tu (watek glowny)
                    // n20-drogi: plan drog bocznych podany, gdy trakty nie byly wczytane - budowa siatki wspolnej teraz
                    SideRoadPlan.Result res; float half; int ver; Grid main;
                    lock (_pubLock) { res = _sideRes; half = _sideHalf; main = _main; ver = res != null && main != null ? ++_sideVer : 0; }
                    if (res != null && main != null) QueueSide(ver, main, res, half);
                }
                if (!s.MapRoadSpeedBonus) { ZeroDay(); return; }
                // ciche logi: doba bez linii - spis godzinny zostaje (zbiera tylko w dobie pomiaru, LogGate.Today), liczniki predkosci i potkniec od zera,
                // zeby linia w takcie S+1 mowila o tej samej dobie S co spis (recenzja, uwaga 2); potkniecia z dob bez linii zostaja w "od wczytania"
                if (!LogGate.Lines) { ZeroSpeed(); return; }
                Line(s, (int)CampaignTime.Now.ToDays - 1);
            }
            catch (Exception e) { Stumble("Daily", e); }
            ZeroDay();
        }

        private static void Line(Settings s, int day)
        {
            var g = _grid;
            var ci = Inv;
            var sb = new StringBuilder(900);
            var sd = new StringBuilder(700);   // n20-drogi: osobna linia drog bocznych (linia doby bez zmian - czyta ja sprawdz_logi.py, grupa ndrogi)
            float pct = PctOf(s), spct = SidePctOf(s);
            sb.Append(P).Append("dzien ").Append(day)
              .Append(" | klucz MapRoadSpeedBonus tak, premia ").Append((pct * 100f).ToString("0", ci)).Append("%, margines ").Append(MarginOf(s).ToString("0.00", ci))
              .Append(" | plik ").Append(g != null ? "v" + g.Version + ": lamanych " + g.Lines + ", punktow " + g.Points + ", odcinkow " + g.Segments : "NIECZYNNY (" + _note + ")");
            int tot = 0, totOn = 0, totSide = 0;
            for (int k = 0; k < 5; k++) { tot += _hN[k]; totOn += _hOn[k]; totSide += _hSide[k]; }
            sb.Append(" | partio-godziny w ruchu na ladzie (AI, godzin spisu ").Append(_hours).Append("): ").Append(tot).Append(", na drodze ").Append(totOn)
              .Append(" (").Append(Pct(totOn, tot)).Append("%)")
              .Append(" | rody ").Append(_hOn[0]).Append('/').Append(_hN[0]).Append(" (").Append(Pct(_hOn[0], _hN[0])).Append("%)")
              .Append(", karawany ").Append(_hOn[1]).Append('/').Append(_hN[1]).Append(" (").Append(Pct(_hOn[1], _hN[1])).Append("%)")
              .Append(", wozy wsi ").Append(_hOn[2]).Append('/').Append(_hN[2]).Append(" (").Append(Pct(_hOn[2], _hN[2])).Append("%)")
              .Append(", bandy ").Append(_hOn[3]).Append('/').Append(_hN[3]).Append(" (").Append(Pct(_hOn[3], _hN[3])).Append("%)")
              .Append(", inne ").Append(_hOn[4]).Append('/').Append(_hN[4]).Append(" (").Append(Pct(_hOn[4], _hN[4])).Append("%)")
              .Append(" | gracz godzin w ruchu ").Append(_hN[5]).Append(", na drodze ").Append(_hOn[5]);
            long sc = _speedCalls, so = _speedOn, ss = _speedSide;
            sb.Append(" | liczenia predkosci (ok.) ").Append(sc).Append(", z premia drogi ").Append(so).Append(" (").Append(sc > 0 ? (100.0 * so / sc).ToString("0.0", ci) : "-").Append("%)")
              .Append(", przeliczenia wymuszone zmiana drogi (ok.) ").Append(_flips).Append(", latka pamieci ").Append(_cacheHooked ? "wpieta" : "NIE")
              .Append(", straznik zapisu ").Append(_writerHooks).Append("/2, liczenia bez zapisu stanu (model wprost, ok.) ").Append(_recSkipped)
              .Append(", premia ").Append(Status(g));
            // n20-drogi: linia drog bocznych - ten sam spis godzinny i te same liczniki doby
            sd.Append(P).Append("drogi boczne, dzien ").Append(day)
              .Append(" | klucz MapSideRoadSpeedBonus ").Append(s.MapSideRoadSpeedBonus ? "tak" : "nie").Append(", premia ").Append((spct * 100f).ToString("0.0", ci))
              .Append("%, margines ").Append(MarginOf(s).ToString("0.00", ci)).Append(" | ").Append(SideText(g))
              .Append(" | partio-godziny AI w ruchu na ladzie ").Append(tot).Append(", na drodze bocznej ").Append(totSide).Append(" (").Append(Pct(totSide, tot)).Append("%)")
              .Append(" - rody ").Append(_hSide[0]).Append('/').Append(_hN[0]).Append(" (").Append(Pct(_hSide[0], _hN[0])).Append("%)")
              .Append(", karawany ").Append(_hSide[1]).Append('/').Append(_hN[1]).Append(" (").Append(Pct(_hSide[1], _hN[1])).Append("%)")
              .Append(", wozy wsi ").Append(_hSide[2]).Append('/').Append(_hN[2]).Append(" (").Append(Pct(_hSide[2], _hN[2])).Append("%)")
              .Append(", bandy ").Append(_hSide[3]).Append('/').Append(_hN[3]).Append(" (").Append(Pct(_hSide[3], _hN[3])).Append("%)")
              .Append(", inne ").Append(_hSide[4]).Append('/').Append(_hN[4]).Append(" (").Append(Pct(_hSide[4], _hN[4])).Append("%)")
              .Append(" | gracz godzin w ruchu ").Append(_hN[5]).Append(", na drodze bocznej ").Append(_hSide[5])
              .Append(" | liczenia predkosci (ok.) ").Append(sc).Append(", z premia drogi bocznej ").Append(ss).Append(" (").Append(sc > 0 ? (100.0 * ss / sc).ToString("0.0", ci) : "-").Append("%)");
            Audit(s, g, sb, sd, pct, spct);
            double censusMs = _censusTicks * 1000.0 / Stopwatch.Frequency;
            double cacheUs = _cacheSampled > 0 ? _cacheTicks * 1e6 / Stopwatch.Frequency / _cacheSampled : 0;
            sb.Append(" | koszt: spis ").Append(censusMs.ToString("0.000", ci)).Append(" ms/dobe, postfiks pamieci ").Append(cacheUs.ToString("0.000", ci))
              .Append(" us/wywolanie (probka ").Append(_cacheSampled).Append(", wywolan ok. ").Append(_cacheCalls).Append(')')
              .Append(" | potkniecia dzis ").Append(_stumbles).Append(" (od wczytania ").Append(_stumblesAll).Append("), w predkosci dzis ").Append(_parStumbles)
              .Append(" (od wczytania ").Append(_parStumblesAll).Append(")")
              .Append(_firstPar != null ? " pierwsze: " + _firstPar.GetType().Name + " " + (_firstPar.Message ?? "") : "").Append('.');
            sd.Append('.');
            Log.Info(sb.ToString());
            Log.Info(sd.ToString());
        }

        /// <summary>Audyt w takcie linii: do 8 losowych partii AI w ruchu na trakcie (sb) i (n20-drogi) do 8 na drodze bocznej (sd) + gracz na ladzie - model
        /// predkosci wprost (bez zapisu do pamieci partii): bez premii drogi (r0) i z premia (r1, z opisami): premia rzeczywista r1/r0 - 1, wpis "Road" /
        /// "Side road", wynik na suficie LimitMax, premia zjedzona przez sufit (r1 < r0 x (1 + premia) x 0.995), pamiec gry (_lastCalculatedSpeed) zgodna z r1 +-2%.
        /// Czesc linii traktow w tym samym ksztalcie co przed n20-drogi (gracz "na drodze" = na trakcie).</summary>
        private static void Audit(Settings s, Grid g, StringBuilder sb, StringBuilder sd, float pct, float spct)
        {
            var ci = Inv;
            if (g == null || Campaign.Current == null) { sb.Append(" | audyt: brak siatki | gracz: -"); sd.Append(" | audyt: brak siatki"); return; }
            var model = Campaign.Current.Models.PartySpeedCalculatingModel;
            if (model == null) { sb.Append(" | audyt: brak modelu predkosci | gracz: -"); sd.Append(" | audyt: brak modelu predkosci"); return; }
            if (!_lastSpeedTried)
            {
                _lastSpeedTried = true;
                try { _lastSpeedRef = AccessTools.FieldRefAccess<MobileParty, float>("_lastCalculatedSpeed"); } catch { _lastSpeedRef = null; }
            }
            float margin = MarginOf(s);
            bool side = SideOn(s, g);
            _cand.Clear();
            _candSide.Clear();
            var all = MobileParty.All;
            var main = MobileParty.MainParty;
            for (int i = 0; i < all.Count; i++)
            {
                var mp = all[i];
                try
                {
                    if (mp == null || mp == main || !mp.IsActive || mp.AttachedTo != null || mp.IsCurrentlyAtSea || mp.IsTransitionInProgress || !mp.IsMoving) continue;
                    var p = mp.Position;
                    int c = g.CellOf(p.X, p.Y);
                    int eff;
                    int kind = KindEff(g, c, p.X, p.Y, margin, side, s, out eff);
                    if (kind == 2) { if (eff == MapRoadKinds.Trakt) _cand.Add(mp); }   // m96-valyria: droga Valyrii / szlak - audyt w linii VR (inna premia)
                    else if (kind == 1) _candSide.Add(mp);
                }
                catch (Exception e) { Stumble("Audit.Scan", e); }
            }
            sb.Append(" | audyt AI na drodze: ");
            AuditList(model, _cand, pct, false, sb);
            sd.Append(" | audyt AI na drodze bocznej: ");
            AuditList(model, _candSide, spct, true, sd);
            _cand.Clear();
            _candSide.Clear();
            // gracz (na ladzie): zawsze, z flaga drogi
            try
            {
                if (main != null && main.IsActive && !main.IsCurrentlyAtSea && !main.IsTransitionInProgress)
                {
                    var p = main.Position;
                    int c = g.CellOf(p.X, p.Y);
                    int eff;   // m96-valyria (recenzja): "na drodze tak" tylko na zwyklym trakcie - droga Valyrii / szlak maja inna premie (NDR-6 porownuje z traktem)
                    int kind = KindEff(g, c, p.X, p.Y, margin, side, s, out eff);
                    bool other = kind == 2 && eff != MapRoadKinds.Trakt;
                    float ed = g.EdgeDist(c, p.X, p.Y, false), es = g.EdgeDist(c, p.X, p.Y, true);
                    float r0, r1, lim, lmin, cached;
                    bool line, lineS;
                    if (Probe(model, main, out r0, out r1, out lim, out lmin, out line, out lineS, out cached))
                    {
                        string limT = lim < float.MaxValue * 0.5f ? lim.ToString("0.000", ci) : "brak";
                        string floorT = r0 <= lmin + 0.001f ? "tak" : "nie";
                        sb.Append(" | gracz: na drodze ").Append(kind == 2 && !other ? "tak" : "nie").Append(" (od krawedzi farby ").Append(ed < 1e30f ? ed.ToString("0.00", ci) : ">2").Append(" jedn.)")
                          .Append(", bez premii ").Append(r0.ToString("0.000", ci)).Append(", z premia ").Append(r1.ToString("0.000", ci))
                          .Append(", LimitMax ").Append(limT)
                          .Append(", wpis Road ").Append(line ? "tak" : "nie").Append(", pamiec gry ").Append(cached.ToString("0.000", ci))
                          .Append(", na podlodze LimitMin ").Append(floorT)
                          .Append(other ? (eff == MapRoadKinds.Valyrian ? ", rodzaj: droga Valyrii (premia w linii VR)" : ", rodzaj: szlak karawan (premia w linii VR)") : "");
                        sd.Append(" | gracz: na drodze bocznej ").Append(kind == 1 ? "tak" : kind == 2 ? "nie (na trakcie)" : "nie").Append(" (od krawedzi drogi bocznej ")
                          .Append(es < 1e30f ? es.ToString("0.00", ci) : ">2").Append(" jedn.)")
                          .Append(", bez premii ").Append(r0.ToString("0.000", ci)).Append(", z premia ").Append(r1.ToString("0.000", ci))
                          .Append(", LimitMax ").Append(limT)
                          .Append(", wpis Side road ").Append(lineS ? "tak" : "nie").Append(", pamiec gry ").Append(cached.ToString("0.000", ci))
                          .Append(", na podlodze LimitMin ").Append(floorT);
                    }
                    else { sb.Append(" | gracz: audyt nieudany"); sd.Append(" | gracz: audyt nieudany"); }
                }
                else { sb.Append(" | gracz: na morzu / w przejsciu / nieczynny"); sd.Append(" | gracz: na morzu / w przejsciu / nieczynny"); }
            }
            catch (Exception e) { Stumble("Audit.Player", e); sb.Append(" | gracz: blad audytu"); sd.Append(" | gracz: blad audytu"); }
        }

        /// <summary>Audyt jednego rodzaju drogi (side: false trakt, true droga boczna): do AuditMax losowych kandydatow (lista mieszana w miejscu), premia
        /// rzeczywista sr./min/maks, brak wpisu, zjedzona przez sufit, na suficie, pamiec gry zgodna, na podlodze LimitMin (poza srednia - recenzja, uwaga 3).</summary>
        private static void AuditList(PartySpeedModel model, List<MobileParty> cand, float pct, bool side, StringBuilder sb)
        {
            var ci = Inv;
            int n = 0, nb = 0, noLine = 0, eaten = 0, atCap = 0, atFloor = 0, cacheOk = 0, cacheN = 0;
            double sum = 0, mn = double.MaxValue, mx = double.MinValue;
            int k = Math.Min(AuditMax, cand.Count);
            for (int i = 0; i < k; i++)
            {
                int j = i + _rng.Next(cand.Count - i);
                var tmp = cand[i]; cand[i] = cand[j]; cand[j] = tmp;
                float r0, r1, lim, lmin, cached;
                bool line, lineS;
                if (!Probe(model, cand[i], out r0, out r1, out lim, out lmin, out line, out lineS, out cached)) continue;
                n++;
                if (!(side ? lineS : line)) noLine++;
                if (cached > 0f) { cacheN++; if (Math.Abs(cached - r1) <= 0.02f * r1) cacheOk++; }
                // podloga gry (LimitMin, MinimumSpeed 1): wynik bez premii na podlodze = premia ginie w modelu gry (np. dlug snu 3 w NightRest po nas) -
                // osobny licznik, poza srednia premii i "zjedzona przez sufit" (recenzja, uwaga 3)
                if (r0 <= lmin + 0.001f) { atFloor++; continue; }
                nb++;
                double b = r0 > 0.0001f ? r1 / r0 - 1.0 : 0.0;
                sum += b; if (b < mn) mn = b; if (b > mx) mx = b;
                if (r1 < r0 * (1f + pct) * 0.995f) eaten++;
                if (lim < float.MaxValue * 0.5f && r1 >= lim - 0.0015f) atCap++;
            }
            sb.Append("kandydatow ").Append(cand.Count).Append("; probek ").Append(n)
              .Append(", premia rzeczywista sr. ").Append(nb > 0 ? (100.0 * sum / nb).ToString("0.00", ci) : "-").Append("% (min ").Append(nb > 0 ? (100.0 * mn).ToString("0.00", ci) : "-")
              .Append(", maks ").Append(nb > 0 ? (100.0 * mx).ToString("0.00", ci) : "-").Append(')')
              .Append(side ? ", bez wpisu Side road " : ", bez wpisu Road ").Append(noLine).Append(", zjedzona przez sufit ").Append(eaten).Append(", na suficie LimitMax ").Append(atCap)
              .Append(", pamiec gry zgodna ").Append(cacheOk).Append('/').Append(cacheN)
              .Append(", na podlodze LimitMin (poza srednia) ").Append(atFloor);
        }

        /// <summary>m96-valyria (audyt linii VR, watek glowny): predkosc partii bez premii drogi i z premia (model wprost, bez zapisu do pamieci partii) oraz czy
        /// jest wpis o tekscie txt. false = brak modelu / blad.</summary>
        internal static bool ProbeFor(MobileParty mp, string txt, out float r0, out float r1, out bool hasLine)
        {
            r0 = r1 = 0f; hasLine = false;
            var model = Campaign.Current != null ? Campaign.Current.Models.PartySpeedCalculatingModel : null;
            if (model == null || mp == null) return false;
            try
            {
                _noRecord = true;
                _suppress = true;
                try { r0 = model.CalculateFinalSpeed(mp, model.CalculateBaseSpeed(mp, false)).ResultNumber; }
                finally { _suppress = false; }
                var en = model.CalculateFinalSpeed(mp, model.CalculateBaseSpeed(mp, true));
                r1 = en.ResultNumber;
                foreach (var l in en.GetLines()) if (l.Item1 == txt) hasLine = true;
                return true;
            }
            catch (Exception e) { Stumble("Audit.ProbeFor", e); return false; }
            finally { _noRecord = false; _suppress = false; }
        }

        /// <summary>m103 P1 (audyt VR): jak ProbeFor wyzej, plus podloga modelu bez premii (LimitMinValue) i sufit z premia (LimitMaxValue) - powod premii
        /// ponizej 5% w linii VR (sam odczyt, bez zapisu do pamieci partii).</summary>
        internal static bool ProbeFor(MobileParty mp, string txt, out float r0, out float r1, out bool hasLine, out float lmin, out float lmax)
        {
            r0 = r1 = 0f; hasLine = false; lmin = float.MinValue; lmax = float.MaxValue;
            var model = Campaign.Current != null ? Campaign.Current.Models.PartySpeedCalculatingModel : null;
            if (model == null || mp == null) return false;
            try
            {
                _noRecord = true;
                _suppress = true;
                try { var e0 = model.CalculateFinalSpeed(mp, model.CalculateBaseSpeed(mp, false)); r0 = e0.ResultNumber; lmin = e0.LimitMinValue; }
                finally { _suppress = false; }
                var en = model.CalculateFinalSpeed(mp, model.CalculateBaseSpeed(mp, true));
                r1 = en.ResultNumber;
                lmax = en.LimitMaxValue;
                foreach (var l in en.GetLines()) if (l.Item1 == txt) hasLine = true;
                return true;
            }
            catch (Exception e) { Stumble("Audit.ProbeFor", e); return false; }
            finally { _noRecord = false; _suppress = false; }
        }

        private static bool Probe(PartySpeedModel model, MobileParty mp, out float r0, out float r1, out float lim, out float lmin, out bool line, out bool lineS, out float cached)
        {
            r0 = r1 = lim = cached = 0f; lmin = float.MinValue; line = false; lineS = false;
            try
            {
                _noRecord = true;
                _suppress = true;
                try { var e0 = model.CalculateFinalSpeed(mp, model.CalculateBaseSpeed(mp, false)); r0 = e0.ResultNumber; lmin = e0.LimitMinValue; }
                finally { _suppress = false; }
                var en = model.CalculateFinalSpeed(mp, model.CalculateBaseSpeed(mp, true));
                r1 = en.ResultNumber;
                lim = en.LimitMaxValue;
                string road = _txtRoad.ToString(), sideRoad = _txtSide.ToString();
                string val = MapRoadKinds.TxtValyrian.ToString(), car = MapRoadKinds.TxtCaravan.ToString();   // m96-valyria: wpis farby innego rodzaju
                foreach (var l in en.GetLines())
                {
                    if (l.Item1 == road || l.Item1 == val || l.Item1 == car) line = true;
                    else if (l.Item1 == sideRoad) lineS = true;
                }
                if (_lastSpeedRef != null) cached = _lastSpeedRef(mp);
                return true;
            }
            catch (Exception e) { Stumble("Audit.Probe", e); return false; }
            finally { _noRecord = false; _suppress = false; }
        }
    }
}
