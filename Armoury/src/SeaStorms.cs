using System;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// p6-sztormy (R3-05d, POMYSLY-35 pkt 11; Jeff 20.10: "28 - nie, 29 - nie. Pozostale tak", "Rob od razu fale 2") - ZIMOWE SZTORMY NA MORZU.
    /// Zrodlo: docs/audyt-2026-10-09/10-pomysly.md Z15 "Sezon zeglugi" (zima czestsze sztormy, wolniejsze statki; najpierw sprawdzic, co robi NavalDLC).
    ///
    /// ROZPOZNANIE NavalDLC (dekompilacja 1.4.8, NavalDLC.dll):
    ///  - Burze rodzi StormCampaignBehavior.TrySpawningNewStorm co godzine gry: dla KAZDEGO wezla pogody na pelnym morzu (TerrainType.OpenSea, siatka
    ///    DefaultWeatherNodeDimension^2, kolejnosc tasowana raz na kampanie) - szansa NavalDLCStormModel.GetHourlyStormSpawnChanceForPosition = 0.0025
    ///    (stala, bez pory roku), o ile burz jest mniej niz MaximumNumberOfStorms = 6 (SUFIT) i nowa jest >= 200 jedn. od srodka kazdej innej.
    ///    Przy setkach wezlow (0.0025 x N na godzine) wolne miejsce zapelnia sie w godziny - burz jest prawie zawsze 6: wiaze SUFIT, nie szansa.
    ///    Zycie burzy 10-21 dob (+8 h narodzin, +8 h wygasania); slaby wiatr (< 0.15) ja gasi (sila -0.01/h od 0.5), zderzenie dwoch gasi mniejsza.
    ///  - Pora roku dziala dzis tylko na RODZAJ burzy (GetSpawnedStormTypeForPosition: deszcz w punkcie <= 0.2 Storm, <= 0.6 ThunderStorm, wyzej Hurricane;
    ///    deszcz gry zalezy od pory nieba Westeros - WesterosClimate), nie na ich liczbe.
    ///  - W burzy (aktywna, odleglosc od srodka < EffectRadius 20/30/40 jedn. +-20% wedlug sily): wzrok -40% (NavalDLCMapVisibilityModel, wpis "Storm"),
    ///    co godzine uszkodzenia kadluba wedlug dzielnosci morskiej (StormCampaignBehavior.DamageNearbyParties; 0 HP = statek tonie), AI z uszkodzona
    ///    flota ucieka od srodka burzy (NavalDLCMobilePartyAIModel.GetBestInitiativeBehavior), wiatr silniejszy o 0.1-0.6 (GetWindForPosition).
    ///  - PREDKOSC: NavalDLCPartySpeedCalculationModel NIE ma kary za burze - przeciwnie, silniejszy wiatr burzy daje premie "Wind" (do +150% z wiatrem
    ///    w plecy, 0 pod wiatr). Zaden inny mod tego nie robi (GetWeatherEventInPosition w BK - tylko pasza w sniegu, RBL - martwy, SeasonsOff). Kara za
    ///    burze nie dubluje niczego.
    ///  - Bufor okludera mapy NavalDLC (CampaignMapAmbientOccluder): max(MaximumNumberOfStorms + 2, 16) kosci, w kazdej klatce po jednej na burze
    ///    i na burze fabuly (najwyzej 2). Burz wiecej niz 14 = zapis poza bufor - dlatego twarda granica sufitu 14 (CapCeiling).
    ///
    /// REGULA (gracz i AI, cale morze):
    ///  1. ZIMA (pora Westeros WesterosClimate.Now() - ta sama, co zima WinterBite, plony i jarmarki): szansa narodzin burzy na wezel x SeaStormWinterFactor
    ///     (domyslnie 2: 0.0025 -> 0.005) i SUFIT burz naraz x ten sam mnoznik (6 -> 12; granica 14). "x2 do sufitu": przy wiazacym suficie sama szansa nic
    ///     by nie dala, wiec zima podnosi oba. Po zimie sufit wraca do 6 - burze juz trwajace dozywaja swoje (nic ich nie gasi na sile). Sila, zasieg,
    ///     zycie, szkody i ucieczka AI - bez zmian (NavalDLC); wiecej burz = wiecej rejsow przez burze, wiecej szkod i objazdow.
    ///  2. STATEK W BURZY (cala rok; partia na morzu, nie w przejsciu lad-morze; ta sama strefa co kara wzroku NavalDLC i granica wiatru burzy: aktywna burza,
    ///     odleglosc < EffectRadius): -SeaStormShipSpeedPercent% (domyslnie 15) wyniku predkosci, wpis "Storm". Latka MarchPace.SpeedPostfix (najbardziej
    ///     zewnetrzny poziom lancucha - SpeedDepth), po wietrze NavalDLC: Add(-wynik x procent / (1 + suma wspolczynnikow)); sufit NavalDLC na morzu
    ///     (LimitMax 10) w dol o ten sam procent, gdy kara zginelaby pod nim (jak MapMud). Kara tylko spowalnia (najwyzej 50%) - nigdy nie zatrzymuje.
    ///     PAMIEC PREDKOSCI GRY: na morzu gra przelicza predkosc przy kazdej zmianie wiatru w punkcie (MobileParty.IsLastSpeedCacheInvalid: kierunek > 0.06 rad
    ///     albo kwadrat dlugosci > 0.0001), a wiatr burzy skacze o >= 0.1 na granicy EffectRadius - wejscie i wyjscie z burzy przelicza predkosc samo
    ///     (bez latki pamieci, inaczej niz bloto).
    ///  WYSPY (Pyke, Lys, Braavos): wozy jedzenia KS-H i hurt TradeCarts plyna "w dobach" (tabela dob, bez partii na morzu) - burze ich nie dotycza, dowoz na
    ///  wyspy plynie zima jak latem; partie na morzu (lordowie, karawany, piraci, gracz) tylko zwalniaja w burzy - szlak zostaje otwarty.
    ///
    /// KOSZT: postfiksy NavalDLC - mnozenie przez liczbe z pamieci (watek glowny, raz na wezel morza na godzine); predkosc (watki robocze gry, PartiesThink)
    /// - tylko partie na morzu: przeglad listy burz NavalDLC (najwyzej 14) przez skompilowane gettery, bez alokacji i bez blokad (NavalDLC czyta te sama liste
    /// z tych samych watkow - wiatr w swoim modelu predkosci). Pora roku i mnoznik zimy - raz na godzine w watku glownym (pamiec). Spis partii na morzu
    /// tylko w dobach pomiaru (LogGate.Today), linia "Sztormy (MS): dzien N" w dobie linii (LogGate.Lines). Potkniecia liczone, nigdy wylacznik calej funkcji.
    /// STAN: brak zapisu (burze zapisuje NavalDLC; regula wynika z pory roku); liczniki tylko w pamieci, od zera przy nowej kampanii / wczytaniu (Reset).
    /// Klucze NOWE: SeaStormWinter (TAK), SeaStormWinterFactor (2, przyciete 1..2.3), SeaStormSlowsShips (TAK), SeaStormShipSpeedPercent (15, przyciete 0..50).
    /// Wylaczony klucz = jak przed zmiana (postfiksy oddaja wynik NavalDLC, predkosc wychodzi od razu).
    /// </summary>
    internal static class SeaStorms
    {
        private const string P = "Sztormy (MS): ";
        private const int CapCeiling = 14;       // bufor kosci okludera NavalDLC: 16 = 14 burz + 2 burze fabuly
        private const float FactorMax = 2.3f;    // 6 x 2.3 = 13.8 -> 14
        private const int MaxPercent = 50;
        private const int AuditMax = 8;
        private const int SnapMax = 32;
        private const float RMax2 = 48.5f * 48.5f;   // EffectRadius NavalDLC najwyzej 40 x (1 + 0.2) = 48 - dalsze burze bez wolania getterow
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly TextObject _txtStorm = new TextObject("{=!}Storm");
        private static readonly string[] TypeName = { "Storm", "ThunderStorm", "Hurricane" };

        [ThreadStatic] private static bool _suppress;   // audyt: liczenie "bez kary burzy" (tylko watek glowny audytu)

        // NavalDLC przez refleksje (Armoury nie ma referencji do NavalDLC.dll) - gettery skompilowane raz na proces
        private static Func<object> _mgrInstance;              // NavalDLCManager.Instance
        private static Func<object, object> _mgrStorms;        // NavalDLCManager.StormManager
        private static Func<object, object> _spawned;          // StormManager.SpawnedStorms (MBReadOnlyList<Storm> : List<Storm>)
        private static Func<object, bool> _isActive;           // Storm.IsActive
        private static Func<object, Vec2> _pos;                // Storm.CurrentPosition
        private static Func<object, float> _radius;            // Storm.EffectRadius
        private static Func<object, int> _type;                // Storm.StormType
        private static Type _behType;                          // StormCampaignBehavior (liczba wezlow pelnego morza - tylko log)
        private static FieldInfo _nodesField;
        private static bool _chanceHooked, _capHooked, _gettersOk;
        private static string _hookNote = "nie wpinane";

        // stan z watku glownego (co godzine) czytany w postfiksach i w watkach roboczych
        private static volatile IList _list;
        private static float _mul = 1f;          // mnoznik zimy czynny teraz (1 = jak NavalDLC)
        private static bool _winter;
        private static int _openSeaNodes = -1;

        // liczniki watkow roboczych (przyblizone, bez blokad - jak MapMud): liczenia na morzu, w burzy z kara, sufit obnizony, probka czasu
        private static long _calls, _inStorm, _capLowered, _ticks, _sampled;
        private static int _parStumbles, _parStumblesAll, _stumbles, _stumblesAll;
        private static Exception _firstPar;
        // postfiksy NavalDLC (watek glowny): wywolania szansy (= wezly pelnego morza x godziny), szansa podniesiona, sufit podniesiony
        private static long _chanceCalls, _chanceRaised, _capCalls, _capRaised;
        private static int _lastCap = -1, _lastCapBase = -1;
        private static float _lastChance = -1f, _lastChanceBase = -1f;

        // doba: godziny, burze-godziny (wszystkie / aktywne), godziny przy suficie, nowe burze (wedlug rodzaju)
        private static int _dHours, _dStormH, _dActiveH, _dCapH, _dNew;
        private static readonly int[] _dNewType = new int[3];
        // spis w dobach pomiaru: partio-godziny na morzu / w burzy wedlug rodzaju (0 rody, 1 karawany, 2 wozy wsi, 3 bandy, 4 inne, 5 gracz)
        private static readonly int[] _hSea = new int[6], _hIn = new int[6];
        private static int _censusHours;
        private static double _spInSum, _spOutSum; private static int _spInN, _spOutN;
        private static double _hullSum; private static int _hullN, _hullLow;   // kadluby statkow partii w burzy: suma HP/MaxHP, liczba, ponizej 25%
        private static long _censusTicks;
        // od wczytania wedlug pory: [0] poza zima, [1] zima
        private static readonly long[] _sHours = new long[2], _sActiveH = new long[2], _sNew = new long[2], _sSea = new long[2], _sIn = new long[2], _sCensusH = new long[2];
        private static readonly double[] _sSpIn = new double[2], _sSpOut = new double[2];
        private static readonly long[] _sSpInN = new long[2], _sSpOutN = new long[2];

        // migawka burz z poprzedniej godziny (nowe burze = obiekty, ktorych w niej nie bylo)
        private static readonly object[] _snap = new object[SnapMax], _snapCur = new object[SnapMax];
        private static int _snapN;
        private static bool _snapInit;

        private static readonly System.Random _rng = new System.Random(2005);
        private static readonly System.Collections.Generic.List<MobileParty> _cand = new System.Collections.Generic.List<MobileParty>();
        private static AccessTools.FieldRef<MobileParty, float> _lastSpeedRef;
        private static bool _lastSpeedTried;

        // ------------------------------------------------------------------ klucze
        private static bool WinterOn(Settings s) { return s != null && s.SeaStormWinter; }
        private static bool SlowOn(Settings s) { return s != null && s.SeaStormSlowsShips; }

        private static float FactorOf(Settings s)
        {
            float f = s != null ? s.SeaStormWinterFactor : 1f;
            if (!(f > 1f)) return 1f;   // takze NaN
            return f > FactorMax ? FactorMax : f;
        }

        private static float PctOf(Settings s)
        {
            float p = s != null ? s.SeaStormShipSpeedPercent : 0f;
            if (!(p > 0f)) return 0f;
            return (p > MaxPercent ? MaxPercent : p) / 100f;
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
            if (_stumblesAll <= 3) Log.Error("SeaStorms." + where, e);
        }

        /// <summary>Konstruktor ArmouryBehavior (nowa kampania / przed wczytaniem): liczniki i migawka od zera, lista burz poprzedniej kampanii zapomniana.</summary>
        internal static void Reset()
        {
            _list = null; _mul = 1f; _winter = false; _openSeaNodes = -1;
            ZeroDay();
            for (int i = 0; i < 2; i++) { _sHours[i] = 0; _sActiveH[i] = 0; _sNew[i] = 0; _sSea[i] = 0; _sIn[i] = 0; _sCensusH[i] = 0; _sSpIn[i] = 0; _sSpOut[i] = 0; _sSpInN[i] = 0; _sSpOutN[i] = 0; }
            for (int i = 0; i < SnapMax; i++) _snap[i] = null;
            _snapN = 0; _snapInit = false;
            _parStumblesAll = 0; _stumblesAll = 0; _firstPar = null;
            _lastCap = -1; _lastCapBase = -1; _lastChance = -1f; _lastChanceBase = -1f;
            for (int i = 0; i < 2; i++) { _sCartSea[i] = 0; _sCartLate[i] = 0; _sCartDays[i] = 0; }
            lock (_seaPair) { _seaPair.Clear(); _pairCalls = 0; }   // pary osad poprzedniej kampanii (inna mapa / inne osady)
        }

        private static void ZeroDay()
        {
            _dHours = 0; _dStormH = 0; _dActiveH = 0; _dCapH = 0; _dNew = 0;
            for (int i = 0; i < 3; i++) _dNewType[i] = 0;
            for (int i = 0; i < 6; i++) { _hSea[i] = 0; _hIn[i] = 0; }
            _censusHours = 0; _spInSum = 0; _spOutSum = 0; _spInN = 0; _spOutN = 0; _censusTicks = 0; _hullSum = 0; _hullN = 0; _hullLow = 0;
            _calls = 0; _inStorm = 0; _capLowered = 0; _ticks = 0; _sampled = 0;
            _chanceCalls = 0; _chanceRaised = 0; _capCalls = 0; _capRaised = 0;
            _parStumbles = 0; _stumbles = 0;
            for (int i = 0; i < _dCartSea.Length; i++) { _dCartSea[i] = 0; _dCartLate[i] = 0; _dCartDays[i] = 0; }
        }

        // ------------------------------------------------------------------ latki (menu glowne, SubModuleMain - po MapRoads.ApplyAll)
        /// <summary>Postfiksy na NavalDLCStormModel (DeclaredMethod na klasie deklarujacej): szansa narodzin burzy i sufit burz. Wpinane zawsze, gdy jest NavalDLC
        /// (klucz czytany w pamieci godziny - wylaczony = mnoznik 1, wynik NavalDLC). Tu tez gettery listy burz i pol burzy.</summary>
        internal static void ApplyAll(Harmony h)
        {
            var notes = new StringBuilder();
            try
            {
                var tModel = AccessTools.TypeByName("NavalDLC.GameComponents.NavalDLCStormModel");
                if (tModel == null) { _hookNote = "NIE wpiete (brak NavalDLC - typ NavalDLCStormModel)"; Log.Info(P + "latki: " + _hookNote + "."); return; }
                var mChance = AccessTools.DeclaredMethod(tModel, "GetHourlyStormSpawnChanceForPosition");
                if (mChance != null)
                {
                    h.Patch(mChance, postfix: new HarmonyMethod(typeof(SeaStorms).GetMethod("ChancePostfix", BindingFlags.Public | BindingFlags.Static)));
                    _chanceHooked = Ours(mChance);
                }
                var mCap = AccessTools.DeclaredPropertyGetter(tModel, "MaximumNumberOfStorms");
                if (mCap != null)
                {
                    h.Patch(mCap, postfix: new HarmonyMethod(typeof(SeaStorms).GetMethod("CapPostfix", BindingFlags.Public | BindingFlags.Static)));
                    _capHooked = Ours(mCap);
                }
                notes.Append("szansa narodzin (GetHourlyStormSpawnChanceForPosition) ").Append(_chanceHooked ? "wpieta" : "NIE wpieta")
                     .Append(", sufit burz (MaximumNumberOfStorms) ").Append(_capHooked ? "wpiety" : "NIE wpiety");
            }
            catch (Exception e) { notes.Append("latki: wyjatek ").Append(e.GetType().Name); Log.Error("SeaStorms.ApplyAll", e); }
            try
            {
                var tMgr = AccessTools.TypeByName("NavalDLC.NavalDLCManager");
                var tSm = AccessTools.TypeByName("NavalDLC.Map.StormManager");
                var tStorm = AccessTools.TypeByName("NavalDLC.Map.Storm");
                _behType = AccessTools.TypeByName("NavalDLC.CampaignBehaviors.StormCampaignBehavior");
                if (_behType != null) _nodesField = AccessTools.Field(_behType, "_allOpenSeaWeatherNodePositions");
                // NavalDLCManager.Instance - publiczne pole statyczne (1.4.8); wlasciwosc na wypadek zmiany w innej wersji
                var fiInst = tMgr != null ? AccessTools.Field(tMgr, "Instance") : null;
                var piInst = tMgr != null && fiInst == null ? AccessTools.Property(tMgr, "Instance") : null;
                if (fiInst != null && fiInst.IsStatic)
                    _mgrInstance = Expression.Lambda<Func<object>>(Expression.Convert(Expression.Field(null, fiInst), typeof(object))).Compile();
                else if (piInst != null && piInst.GetGetMethod(true) != null && piInst.GetGetMethod(true).IsStatic)
                    _mgrInstance = Expression.Lambda<Func<object>>(Expression.Convert(Expression.Property(null, piInst), typeof(object))).Compile();
                _mgrStorms = Getter<object>(tMgr, "StormManager");
                _spawned = Getter<object>(tSm, "SpawnedStorms");
                _isActive = Getter<bool>(tStorm, "IsActive");
                _pos = Getter<Vec2>(tStorm, "CurrentPosition");
                _radius = Getter<float>(tStorm, "EffectRadius");
                _type = Getter<int>(tStorm, "StormType");
                _gettersOk = _mgrInstance != null && _mgrStorms != null && _spawned != null && _isActive != null && _pos != null && _radius != null;
                notes.Append(" | gettery burz NavalDLC ").Append(_gettersOk ? "gotowe" : "NIEKOMPLETNE (kara predkosci nieczynna)")
                     .Append(_type != null ? "" : " (bez rodzaju burzy)").Append(_nodesField != null ? "" : " (bez liczby wezlow morza)");
            }
            catch (Exception e) { _gettersOk = false; notes.Append(" | gettery burz: wyjatek ").Append(e.GetType().Name); Log.Error("SeaStorms.Getters", e); }
            _hookNote = notes.ToString();
            Log.Info(P + "latki: " + _hookNote + ".");
        }

        private static bool Ours(MethodBase m)
        {
            var pi = Harmony.GetPatchInfo(m);
            if (pi == null) return false;
            foreach (var x in pi.Postfixes) if (x.PatchMethod != null && x.PatchMethod.DeclaringType == typeof(SeaStorms)) return true;
            return false;
        }

        /// <summary>Getter pola albo wlasciwosci (takze niepublicznej) jako Func&lt;object, T&gt; - enum przez rzutowanie na int.</summary>
        private static Func<object, T> Getter<T>(Type t, string name)
        {
            if (t == null) return null;
            var o = Expression.Parameter(typeof(object), "o");
            Expression m;
            var pi = AccessTools.Property(t, name);
            if (pi != null && pi.GetGetMethod(true) != null) m = Expression.Property(Expression.Convert(o, t), pi);
            else
            {
                var fi = AccessTools.Field(t, name);
                if (fi == null) return null;
                m = Expression.Field(Expression.Convert(o, t), fi);
            }
            if (m.Type != typeof(T))
            {
                if (typeof(T) == typeof(object) && m.Type.IsValueType) return null;
                m = Expression.Convert(m, typeof(T));
            }
            return Expression.Lambda<Func<object, T>>(m, o).Compile();
        }

        /// <summary>Postfiks NavalDLCStormModel.GetHourlyStormSpawnChanceForPosition (watek glowny, raz na wezel pelnego morza na godzine): zima x mnoznik.</summary>
        public static void ChancePostfix(ref float __result)
        {
            _chanceCalls++;
            float m = _mul;
            if (m > 1f)
            {
                _lastChanceBase = __result;
                __result *= m;
                _lastChance = __result;
                _chanceRaised++;
            }
        }

        /// <summary>Postfiks getter NavalDLCStormModel.MaximumNumberOfStorms (spawn burz i okluder mapy): zima sufit x mnoznik, nigdy ponad CapCeiling (14)
        /// i nigdy w dol.</summary>
        public static void CapPostfix(ref int __result)
        {
            _capCalls++;
            float m = _mul;
            if (m > 1f && __result < CapCeiling)
            {
                int c = (int)Math.Round(__result * m);
                if (c > CapCeiling) c = CapCeiling;
                if (c > __result) { _lastCapBase = __result; __result = c; _lastCap = c; _capRaised++; }
            }
        }

        // ------------------------------------------------------------------ burza w punkcie (watki robocze i glowny)
        /// <summary>Punkt w aktywnej burzy NavalDLC (odleglosc od srodka &lt; EffectRadius) - ta sama strefa co kara wzroku i wiatr burzy. Bez alokacji.</summary>
        private static bool InStorm(IList list, float x, float y)
        {
            int n = list.Count;
            for (int i = 0; i < n; i++)
            {
                if (i >= list.Count) break;           // lista zmieniona w tej chwili (watek glowny) - koniec przegladu
                object o = list[i];
                if (o == null) continue;
                Vec2 c = _pos(o);
                float dx = c.x - x, dy = c.y - y;
                float d2 = dx * dx + dy * dy;
                if (d2 >= RMax2) continue;
                float r = _radius(o);
                if (d2 < r * r && _isActive(o)) return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ sciezka predkosci (watki robocze gry)
        /// <summary>MarchPace.SpeedPostfix (po MapMud, ten sam poziom lancucha): statek na morzu w aktywnej burzy -SeaStormShipSpeedPercent% (wpis "Storm").</summary>
        internal static void Speed(MobileParty mp, ref ExplainedNumber r)
        {
            if (_suppress) return;
            var s = Settings.Current;
            if (!SlowOn(s)) return;
            if (mp == null || !SpeedDepth.OutermostFinal) return;   // lancuch modeli - liczymy raz, na poziomie zewnetrznym
            if (!mp.IsCurrentlyAtSea || mp.IsTransitionInProgress) return;   // lad i przejscie lad-morze - nie sztorm morski
            var list = _list;
            if (list == null || !_gettersOk) return;
            long t0 = (++_calls & 63) == 1 ? Stopwatch.GetTimestamp() : 0L;
            try
            {
                var p = mp.Position;
                if (!InStorm(list, p.X, p.Y)) return;
                float pct = PctOf(s);
                if (pct <= 0f) return;
                float res = r.ResultNumber;
                if (res <= 0f) return;
                float f = 1f + r.SumOfFactors;
                float cut = res * pct;
                float target = res - cut;   // cel: wynik przed kara x (1 - procent)
                r.Add(f > 0.01f ? -cut / f : -cut, _txtStorm);
                // recenzja 20.10 (wazne): sufit NavalDLC na morzu (LimitMax 10) albo inny sam LimitMax trzymal wynik PRZED kara - wynik bez obciecia byl nad
                // sufitem, wiec Add zjada czesc kary (bez obciecia 11.0: 11.0 - 1.5 = 9.5 zamiast 8.5). Decyzja wzgledem celu PO Add: wynik nadal ponad
                // celem = sufit w dol do celu (nie nizej niz LimitMin - podloga gry wygrywa jak dotad)
                float lim = r.LimitMaxValue;
                if (lim > 0f && lim < float.MaxValue * 0.5f && res >= lim - 0.0005f && r.ResultNumber > target + 0.001f)
                {
                    float lo = r.LimitMinValue;
                    r.LimitMax(target > lo ? target : lo);
                    _capLowered++;
                }
                _inStorm++;
            }
            catch (Exception e) { ParStumble(e); }
            finally { if (t0 != 0) { _ticks += Stopwatch.GetTimestamp() - t0; _sampled++; } }
        }

        // ------------------------------------------------------------------ ladunki "w dobach" przez morze (watek glowny - takty doby systemow wozow)
        // Recenzja 20.10 (wazne): POMYSLY pkt 11 obiecuje "statki z towarem kursuja na Zelazne Wyspy rzadziej i trudniej je zaopatrzyc". Glowny dowoz wysp
        // to ladunki liczone w dobach (bez partii na morzu - burza ich nie widzi): wozy jedzenia KS-H (KsFoodCarts), materialy kowali (MaterialCarts), hurt
        // kupcow (TradeCarts), zaopatrzenie zamkow (GarrisonCarts), drewno dymarek (Bloomery). Zima (ta sama flaga co szansa burz) kazdy taki ladunek przez
        // morze czeka w porcie na okno pogody: + doba portu KS (KsCore.PortDays = 1) x (mnoznik zimy - 1) = 1 doba przy x2. Tylko opoznienie w chwili wysylki:
        // ilosc, cena i fracht bez zmian, przeplyw na dobe ten sam (woz na pare na dobe), wiec dluga zima przesuwa dostawy, a nie odcina wysp od zboza.
        // Morze: flaga systemu (KS-H "sea", materialy "naval") albo para bez drogi ladowej (MapDistanceModel ladem, ten sam prog co wozy - CartTownExit.BkLimit).
        internal const int CKs = 0, CMat = 1, CTrade = 2, CCastle = 3, CBloom = 4;
        private static readonly string[] CartName = { "jedzenie KS-H", "materialy", "hurt kupcow", "zamki", "dymarki" };
        private static readonly int[] _dCartSea = new int[5], _dCartLate = new int[5], _dCartDays = new int[5];
        private static readonly long[] _sCartSea = new long[2], _sCartLate = new long[2], _sCartDays = new long[2];   // od wczytania wedlug pory wysylki: [0] poza zima, [1] zima
        private static readonly System.Collections.Generic.Dictionary<long, bool> _seaPair = new System.Collections.Generic.Dictionary<long, bool>();   // para osad -> bez drogi ladowej
        private static int _pairCalls;

        private static bool CartsOn(Settings s) { return s != null && s.SeaStormWinterCarts && s.SeaStormWinter; }

        /// <summary>Doby czekania w porcie dla ladunku przez morze wyslanego teraz: zima i klucze wlaczone - round(PortDays x (mnoznik - 1)), inaczej 0.</summary>
        internal static int WinterPortDays()
        {
            var s = Settings.Current;
            if (!_winter || !CartsOn(s)) return 0;
            int d = (int)Math.Round(KsCore.PortDays * (FactorOf(s) - 1f), MidpointRounding.AwayFromZero);
            return d > 0 ? d : 0;
        }

        /// <summary>Wpiecie w chwili wysylki ladunku (sys = CKs..CBloom): sea - system wie, ze plynie morzem; inaczej para a-b bez drogi ladowej = morze.
        /// Zwraca doby do dodania do terminu dostawy (0 - lad, poza zima, klucz wylaczony). Nigdy wyjatek na zewnatrz.</summary>
        internal static int CartDays(int sys, bool sea, Settlement a, Settlement b)
        {
            try
            {
                if (!sea && a != null && b != null) sea = SeaPair(a, b);
                if (!sea) return 0;
                int extra = WinterPortDays();
                int k = _winter ? 1 : 0;
                if (sys < 0 || sys >= _dCartSea.Length) sys = CKs;
                _dCartSea[sys]++; _sCartSea[k]++;
                if (extra > 0) { _dCartLate[sys]++; _dCartDays[sys] += extra; _sCartLate[k]++; _sCartDays[k] += extra; }
                return extra;
            }
            catch (Exception e) { Stumble("CartDays", e); return 0; }
        }

        private static bool SeaPair(Settlement a, Settlement b)
        {
            uint ia = a.Id.InternalValue, ib = b.Id.InternalValue;
            long key = ia < ib ? ((long)ia << 32) | ib : ((long)ib << 32) | ia;
            bool sea;
            lock (_seaPair) { if (_seaPair.TryGetValue(key, out sea)) return sea; }
            float d = -1f;
            try
            {
                var c = Campaign.Current;
                var m = c != null ? c.Models.MapDistanceModel : null;
                if (m == null) return false;
                d = m.GetDistance(a, b, false, false, MobileParty.NavigationType.Default);
            }
            catch { d = -1f; }
            sea = !(d >= 0f && d < CartTownExit.BkLimit);   // jak TradeCarts.Road / GarrisonCarts.MarketFor: brak drogi ladowej = ladem i morzem
            lock (_seaPair) { _seaPair[key] = sea; _pairCalls++; }
            return sea;
        }

        private static void CartLine(Settings s, StringBuilder sb)
        {
            int sea = 0, late = 0, days = 0;
            for (int i = 0; i < _dCartSea.Length; i++) { sea += _dCartSea[i]; late += _dCartLate[i]; days += _dCartDays[i]; }
            sb.Append(" | ladunki w dobach przez morze: klucz SeaStormWinterCarts ").Append(s.SeaStormWinterCarts ? "tak" : "nie")
              .Append(" (zima +").Append(CartsOn(s) ? (int)Math.Round(KsCore.PortDays * (FactorOf(s) - 1f), MidpointRounding.AwayFromZero) : 0).Append(" dob w porcie)")
              .Append(", teraz +").Append(WinterPortDays())
              .Append("; w dobie morskich ").Append(sea).Append(", opoznionych ").Append(late).Append(" (+").Append(days).Append(" dob) - ");
            for (int i = 0; i < CartName.Length; i++)
                sb.Append(i > 0 ? ", " : "").Append(CartName[i]).Append(' ').Append(_dCartLate[i]).Append('/').Append(_dCartSea[i]);
            sb.Append(" (opoznionych/morskich); od wczytania: zima morskich ").Append(_sCartSea[1]).Append(", opoznionych ").Append(_sCartLate[1]).Append(" (+").Append(_sCartDays[1]).Append(" dob)")
              .Append(", poza zima morskich ").Append(_sCartSea[0]).Append(", opoznionych ").Append(_sCartLate[0])
              .Append("; par sprawdzonych ").Append(_pairCalls);
        }

        // ------------------------------------------------------------------ godzina i doba (watek glowny)
        /// <summary>Lista burz kampanii, pora roku i mnoznik zimy do pamieci (postfiksy i watki robocze czytaja tylko pamiec).</summary>
        private static void Refresh()
        {
            IList list = null;
            try
            {
                if (_gettersOk)
                {
                    var mgr = _mgrInstance();
                    var sm = mgr != null ? _mgrStorms(mgr) : null;
                    list = sm != null ? _spawned(sm) as IList : null;
                }
            }
            catch (Exception e) { Stumble("Refresh.List", e); }
            _list = list;
            bool w = false;
            try { w = WesterosClimate.Now() == CampaignTime.Seasons.Winter; } catch (Exception e) { Stumble("Refresh.Season", e); }
            _winter = w;
            var s = Settings.Current;
            _mul = WinterOn(s) && w ? FactorOf(s) : 1f;
        }

        /// <summary>Start sesji kampanii (OnSessionLaunched, po MapMud.SessionStart): pamiec, migawka burz (bez liczenia jako nowe), linia startu.</summary>
        internal static void SessionStart()
        {
            ZeroDay();
            _parStumblesAll = 0; _stumblesAll = 0; _firstPar = null;
            _snapN = 0; _snapInit = false;
            Refresh();
            Snap(false);
            var s = Settings.Current;
            string model = "-";
            try { var c = Campaign.Current; model = c != null && c.Models.PartySpeedCalculatingModel != null ? c.Models.PartySpeedCalculatingModel.GetType().FullName : "brak"; } catch (Exception e) { Stumble("SessionStart", e); }
            var list = _list;
            Log.Info(P + "start sesji - wiecej burz zima SeaStormWinter " + (WinterOn(s) ? "tak" : "nie") + " (x" + FactorOf(s).ToString("0.00", Inv)
                     + " szansy narodzin i sufitu burz, sufit najwyzej " + CapCeiling + "), statki wolniej w burzy SeaStormSlowsShips " + (SlowOn(s) ? "tak" : "nie")
                     + " (-" + (PctOf(s) * 100f).ToString("0.#", Inv) + "%, wpis Storm) | pora Westeros " + SafeSeason() + " (zima " + (_winter ? "tak" : "nie")
                     + ", mnoznik czynny x" + _mul.ToString("0.00", Inv) + ") | latki: " + _hookNote
                     + " | lista burz NavalDLC " + (list != null ? "jest (" + list.Count + " burz)" : "BRAK") + " | latka predkosci MarchPace w " + MapRoads.SpeedModels
                     + " modelach (model " + model + ") | kara " + Status(s) + ".");
        }

        private static string SafeSeason() { try { return WesterosClimate.Now().ToString(); } catch { return "?"; } }

        private static string Status(Settings s)
        {
            if (!SlowOn(s)) return "WYLACZONA (SeaStormSlowsShips)";
            if (!_gettersOk) return "NIECZYNNA (brak getterow burz NavalDLC)";
            if (MapRoads.SpeedModels <= 0) return "NIECZYNNA (latka predkosci MarchPace niewpieta przy starcie gry - potrzebny restart gry)";
            if (_list == null) return "NIECZYNNA (brak listy burz NavalDLC w tej kampanii)";
            return "CZYNNA";
        }

        /// <summary>Migawka listy burz; count = true - obiekty spoza poprzedniej migawki licza sie jako nowe burze (doba i pora).</summary>
        private static int Snap(bool count)
        {
            var list = _list;
            int fresh = 0;
            if (list == null) { _snapN = 0; return 0; }
            int n = Math.Min(list.Count, SnapMax);
            var cur = _snapCur;
            for (int i = 0; i < n; i++)
            {
                object o = list[i];
                cur[i] = o;
                if (!count || !_snapInit || o == null) continue;
                bool seen = false;
                for (int j = 0; j < _snapN; j++) if (ReferenceEquals(_snap[j], o)) { seen = true; break; }
                if (seen) continue;
                fresh++;
                int t = 0;
                try { t = _type != null ? _type(o) : -1; } catch (Exception e) { Stumble("Snap.Type", e); t = -1; }
                if (t >= 0 && t < 3) _dNewType[t]++;
            }
            for (int i = 0; i < SnapMax; i++) { _snap[i] = i < n ? cur[i] : null; cur[i] = null; }
            _snapN = n; _snapInit = true;
            return fresh;
        }

        /// <summary>Takt godziny (zawsze): pamiec pory i listy, burze tej godziny (wszystkie / aktywne / przy suficie / nowe); w dobach pomiaru (LogGate.Today)
        /// spis partii na morzu - ile w burzy, srednia predkosc z pamieci gry w burzy i poza nia.</summary>
        internal static void Hourly()
        {
            if (Campaign.Current == null) return;
            Refresh();
            int k = _winter ? 1 : 0;
            var list = _list;
            try
            {
                int all = 0, act = 0;
                if (list != null && _gettersOk)
                {
                    all = list.Count;
                    for (int i = 0; i < all; i++) { var o = list[i]; if (o != null && _isActive(o)) act++; }
                }
                int fresh = Snap(true);
                int cap = CurrentCap();
                _dHours++; _dStormH += all; _dActiveH += act; _dNew += fresh;
                if (cap > 0 && all >= cap) _dCapH++;
                _sHours[k]++; _sActiveH[k] += act; _sNew[k] += fresh;
            }
            catch (Exception e) { Stumble("Hourly.Storms", e); }
            if (!LogGate.Today || list == null || !_gettersOk) return;
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                if (!_lastSpeedTried)
                {
                    _lastSpeedTried = true;
                    try { _lastSpeedRef = AccessTools.FieldRefAccess<MobileParty, float>("_lastCalculatedSpeed"); } catch { _lastSpeedRef = null; }
                }
                var parties = MobileParty.All;
                var main = MobileParty.MainParty;
                for (int i = 0; i < parties.Count; i++)
                {
                    var mp = parties[i];
                    try
                    {
                        if (mp == null || !mp.IsActive || mp.AttachedTo != null || !mp.IsCurrentlyAtSea || mp.IsTransitionInProgress || mp.CurrentSettlement != null) continue;
                        int c = mp == main ? 5 : mp.IsLordParty ? 0 : mp.IsCaravan ? 1 : mp.IsVillager ? 2 : mp.IsBandit ? 3 : 4;
                        var p = mp.Position;
                        bool inS = InStorm(list, p.X, p.Y);
                        _hSea[c]++; _sSea[k]++;
                        if (inS)
                        {
                            _hIn[c]++; _sIn[k]++;
                            var ships = mp.Ships;
                            if (ships != null)
                                for (int j = 0; j < ships.Count; j++)
                                {
                                    var sh = ships[j];
                                    if (sh == null || !(sh.MaxHitPoints > 0f)) continue;
                                    float q = sh.HitPoints / sh.MaxHitPoints;
                                    _hullSum += q; _hullN++; if (q < 0.25f) _hullLow++;
                                }
                        }
                        if (mp.IsMoving && _lastSpeedRef != null)
                        {
                            float v = _lastSpeedRef(mp);
                            if (v > 0f && v < 100f)
                            {
                                if (inS) { _spInSum += v; _spInN++; _sSpIn[k] += v; _sSpInN[k]++; }
                                else { _spOutSum += v; _spOutN++; _sSpOut[k] += v; _sSpOutN[k]++; }
                            }
                        }
                    }
                    catch (Exception e) { Stumble("Hourly.Census", e); }
                }
                _censusHours++; _sCensusH[k]++;
            }
            catch (Exception e) { Stumble("Hourly", e); }
            finally { _censusTicks += Stopwatch.GetTimestamp() - t0; }
        }

        /// <summary>Sufit burz NavalDLC teraz (przez model - z nasza latka); -1 bez NavalDLC.</summary>
        private static int CurrentCap()
        {
            try
            {
                var mgr = _mgrInstance != null ? _mgrInstance() : null;
                if (mgr == null) return -1;
                var gm = AccessGameModels(mgr);
                return gm;
            }
            catch (Exception e) { Stumble("CurrentCap", e); return -1; }
        }

        private static Func<object, object> _gameModels, _stormModel;
        private static Func<object, int> _maxStorms;
        private static bool _capGettersTried;

        private static int AccessGameModels(object mgr)
        {
            if (!_capGettersTried)
            {
                _capGettersTried = true;
                try
                {
                    _gameModels = Getter<object>(mgr.GetType(), "GameModels");
                    var tGm = AccessTools.TypeByName("NavalDLC.GameModels");
                    _stormModel = Getter<object>(tGm, "MapStormModel");
                    var tMsm = AccessTools.TypeByName("NavalDLC.ComponentInterfaces.MapStormModel");
                    _maxStorms = Getter<int>(tMsm, "MaximumNumberOfStorms");   // wirtualny getter - wolany przez callvirt, z latka
                }
                catch (Exception e) { Stumble("CapGetters", e); }
            }
            if (_gameModels == null || _stormModel == null || _maxStorms == null) return -1;
            var gm = _gameModels(mgr);
            var sm = gm != null ? _stormModel(gm) : null;
            return sm != null ? _maxStorms(sm) : -1;
        }

        /// <summary>Wezly pelnego morza NavalDLC (StormCampaignBehavior._allOpenSeaWeatherNodePositions) - tylko log; czytane, dopoki sie nie uda.</summary>
        private static int OpenSeaNodes()
        {
            if (_openSeaNodes >= 0 || _behType == null || _nodesField == null || Campaign.Current == null) return _openSeaNodes;
            try
            {
                var gm = typeof(Campaign).GetMethod("GetCampaignBehavior", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                var beh = gm != null ? gm.MakeGenericMethod(_behType).Invoke(Campaign.Current, null) : null;
                var nodes = beh != null ? _nodesField.GetValue(beh) as ICollection : null;
                if (nodes != null) _openSeaNodes = nodes.Count;
            }
            catch (Exception e) { Stumble("OpenSeaNodes", e); }
            return _openSeaNodes;
        }

        private static string Pct(long a, long b) { return b > 0 ? (100.0 * a / b).ToString("0.0", Inv) : "-"; }
        private static string Avg(double sum, long n, string fmt) { return n > 0 ? (sum / n).ToString(fmt, Inv) : "-"; }

        /// <summary>Takt doby (po MapMud.Daily): w dobie linii (LogGate.Lines) linia "Sztormy (MS): dzien N" za dobe; potem zerowanie doby.</summary>
        internal static void Daily()
        {
            var s = Settings.Current;
            if (s == null) return;
            try
            {
                if (LogGate.Lines) Line(s, (int)CampaignTime.Now.ToDays - 1);
            }
            catch (Exception e) { Stumble("Daily", e); }
            ZeroDay();
        }

        private static void Line(Settings s, int day)
        {
            var ci = Inv;
            var sb = new StringBuilder(1600);
            int cap = CurrentCap();
            int nodes = OpenSeaNodes();
            var list = _list;
            int now = 0, nowAct = 0;
            try { if (list != null && _gettersOk) { now = list.Count; for (int i = 0; i < now; i++) { var o = list[i]; if (o != null && _isActive(o)) nowAct++; } } } catch (Exception e) { Stumble("Line.Now", e); }
            sb.Append(P).Append("dzien ").Append(day)
              .Append(" | klucze: wiecej burz zima SeaStormWinter ").Append(WinterOn(s) ? "tak" : "nie").Append(" (x").Append(FactorOf(s).ToString("0.00", ci)).Append(')')
              .Append(", statki wolniej w burzy SeaStormSlowsShips ").Append(SlowOn(s) ? "tak" : "nie").Append(" (-").Append((PctOf(s) * 100f).ToString("0.#", ci)).Append("%)")
              .Append(" | pora Westeros ").Append(SafeSeason()).Append(" (zima ").Append(_winter ? "tak" : "nie").Append(") - mnoznik czynny x").Append(_mul.ToString("0.00", ci))
              .Append(", sufit burz ").Append(cap).Append(" (NavalDLC 6, granica ").Append(CapCeiling).Append(')')
              .Append(", szansa narodzin na wezel/h ").Append(_mul > 1f ? "x" + _mul.ToString("0.00", ci) + (_lastChanceBase >= 0f ? " (NavalDLC " + _lastChanceBase.ToString("0.0000", ci) + " -> " + _lastChance.ToString("0.0000", ci) + ")" : "") : "jak NavalDLC")
              .Append(", wezly pelnego morza ").Append(nodes >= 0 ? nodes.ToString(ci) : "?")
              .Append(" (wywolan szansy w dobie ").Append(_chanceCalls).Append(", podniesionych ").Append(_chanceRaised).Append("; sufit czytany ").Append(_capCalls).Append(", podniesiony ").Append(_capRaised).Append(')');
            sb.Append(" | burze: teraz ").Append(now).Append(" (aktywnych ").Append(nowAct).Append(')')
              .Append(", srednio na godzine ").Append(Avg(_dStormH, _dHours, "0.00")).Append(" (aktywnych ").Append(Avg(_dActiveH, _dHours, "0.00")).Append(')')
              .Append(", godzin przy suficie ").Append(_dCapH).Append('/').Append(_dHours)
              .Append(", nowe ").Append(_dNew).Append(" (Storm ").Append(_dNewType[0]).Append(", ThunderStorm ").Append(_dNewType[1]).Append(", Hurricane ").Append(_dNewType[2]).Append(')');
            int tot = 0, totIn = 0;
            for (int i = 0; i < 5; i++) { tot += _hSea[i]; totIn += _hIn[i]; }
            sb.Append(" | partio-godziny AI na morzu (godzin spisu ").Append(_censusHours).Append("): ").Append(tot).Append(", w burzy ").Append(totIn).Append(" (").Append(Pct(totIn, tot)).Append("%)")
              .Append(" | w burzy / na morzu: rody ").Append(_hIn[0]).Append('/').Append(_hSea[0])
              .Append(", karawany ").Append(_hIn[1]).Append('/').Append(_hSea[1])
              .Append(", wozy wsi ").Append(_hIn[2]).Append('/').Append(_hSea[2])
              .Append(", bandy ").Append(_hIn[3]).Append('/').Append(_hSea[3])
              .Append(", inne ").Append(_hIn[4]).Append('/').Append(_hSea[4])
              .Append(" | gracz godzin na morzu ").Append(_hSea[5]).Append(", w burzy ").Append(_hIn[5])
              .Append(" | kadluby w burzy (statko-godziny) ").Append(_hullN).Append(", stan sr. ").Append(_hullN > 0 ? (100.0 * _hullSum / _hullN).ToString("0.0", ci) + "%" : "-")
              .Append(", ponizej 25% ").Append(_hullLow);
            double vin = _spInN > 0 ? _spInSum / _spInN : 0, vout = _spOutN > 0 ? _spOutSum / _spOutN : 0;
            sb.Append(" | predkosc na morzu w ruchu (pamiec gry): w burzy sr. ").Append(_spInN > 0 ? vin.ToString("0.00", ci) : "-").Append(" (n ").Append(_spInN).Append(')')
              .Append(", poza burza sr. ").Append(_spOutN > 0 ? vout.ToString("0.00", ci) : "-").Append(" (n ").Append(_spOutN).Append(')')
              .Append(", w burzy / poza ").Append(_spInN > 0 && _spOutN > 0 && vout > 0 ? (vin / vout).ToString("0.000", ci) : "-");
            for (int k = 1; k >= 0; k--)
            {
                sb.Append(k == 1 ? " | od wczytania: zima - " : "; poza zima - ")
                  .Append("godzin ").Append(_sHours[k]).Append(", burz aktywnych sr. ").Append(Avg(_sActiveH[k], _sHours[k], "0.00"))
                  .Append(", nowych ").Append(_sNew[k]).Append(" (na dobe ").Append(_sHours[k] > 0 ? (_sNew[k] * 24.0 / _sHours[k]).ToString("0.00", ci) : "-").Append(')')
                  .Append(", udzial burzy na morzu ").Append(Pct(_sIn[k], _sSea[k])).Append("% (partio-godzin ").Append(_sSea[k]).Append(", godzin spisu ").Append(_sCensusH[k]).Append(')')
                  .Append(", predkosc w burzy / poza ").Append(_sSpInN[k] > 0 && _sSpOutN[k] > 0 && _sSpOut[k] > 0 ? ((_sSpIn[k] / _sSpInN[k]) / (_sSpOut[k] / _sSpOutN[k])).ToString("0.000", ci) : "-");
            }
            sb.Append(" | liczenia predkosci na morzu (ok.) ").Append(_calls).Append(", w burzy z kara ").Append(_inStorm).Append(", sufit obnizony ").Append(_capLowered);
            Audit(s, sb);
            CartLine(s, sb);   // recenzja 20.10: ladunki w dobach przez morze (zima czekaja w porcie)
            double censusMs = _censusTicks * 1000.0 / Stopwatch.Frequency;
            double us = _sampled > 0 ? _ticks * 1e6 / Stopwatch.Frequency / _sampled : 0;
            sb.Append(" | koszt: spis ").Append(censusMs.ToString("0.000", ci)).Append(" ms/dobe, sciezka predkosci ").Append(us.ToString("0.000", ci))
              .Append(" us/wywolanie (probka ").Append(_sampled).Append(')')
              .Append(" | kara ").Append(Status(s))
              .Append(" | potkniecia dzis ").Append(_stumbles).Append(" (od wczytania ").Append(_stumblesAll).Append("), w predkosci dzis ").Append(_parStumbles)
              .Append(" (od wczytania ").Append(_parStumblesAll).Append(")")
              .Append(_firstPar != null ? " pierwsze: " + _firstPar.GetType().Name + " " + (_firstPar.Message ?? "") : "").Append('.');
            Log.Info(sb.ToString());
        }

        /// <summary>Audyt w takcie linii: do 8 losowych partii AI w ruchu na morzu w burzy + gracz - model predkosci wprost bez kary (r0) i z kara (r1):
        /// mnoznik r1/r0 (oczekiwany 1 - procent), wpis "Storm", kara zjedzona (r1 &gt; r0 x (1 - kara) x 1.005), na podlodze LimitMin (poza srednia),
        /// pamiec gry (_lastCalculatedSpeed) zgodna z r1 +-2%.</summary>
        private static void Audit(Settings s, StringBuilder sb)
        {
            var ci = Inv;
            var list = _list;
            if (Campaign.Current == null || list == null || !_gettersOk) { sb.Append(" | audyt statkow w burzy: brak listy burz | gracz: -"); return; }
            var model = Campaign.Current.Models.PartySpeedCalculatingModel;
            if (model == null) { sb.Append(" | audyt statkow w burzy: brak modelu predkosci | gracz: -"); return; }
            if (!_lastSpeedTried)
            {
                _lastSpeedTried = true;
                try { _lastSpeedRef = AccessTools.FieldRefAccess<MobileParty, float>("_lastCalculatedSpeed"); } catch { _lastSpeedRef = null; }
            }
            float exp = SlowOn(s) ? PctOf(s) : 0f;
            _cand.Clear();
            var all = MobileParty.All;
            var main = MobileParty.MainParty;
            for (int i = 0; i < all.Count; i++)
            {
                var mp = all[i];
                try
                {
                    if (mp == null || mp == main || !mp.IsActive || mp.AttachedTo != null || !mp.IsCurrentlyAtSea || mp.IsTransitionInProgress || !mp.IsMoving) continue;
                    var p = mp.Position;
                    if (InStorm(list, p.X, p.Y)) _cand.Add(mp);
                }
                catch (Exception e) { Stumble("Audit.Scan", e); }
            }
            int candN = _cand.Count;
            int n = 0, noLine = 0, eaten = 0, atFloor = 0, cacheOk = 0, cacheN = 0, nr = 0;
            double sumM = 0, mn = double.MaxValue, mx = double.MinValue;
            int kmax = Math.Min(AuditMax, _cand.Count);
            for (int i = 0; i < kmax; i++)
            {
                int j = i + _rng.Next(_cand.Count - i);
                var tmp = _cand[i]; _cand[i] = _cand[j]; _cand[j] = tmp;
                float r0, r1, lmin, cached;
                bool line;
                if (!Probe(model, _cand[i], out r0, out r1, out lmin, out line, out cached)) continue;
                n++;
                if (!line && exp > 0f) noLine++;
                if (cached > 0f) { cacheN++; if (Math.Abs(cached - r1) <= 0.02f * r1) cacheOk++; }
                if (r0 <= 0.0001f || r0 * (1f - exp) <= lmin + 0.001f) { atFloor++; continue; }
                double m = r1 / r0;
                sumM += m; nr++; if (m < mn) mn = m; if (m > mx) mx = m;
                if (r1 > r0 * (1f - exp) * 1.005f) eaten++;
            }
            _cand.Clear();
            sb.Append(" | audyt statkow w burzy: kandydatow ").Append(candN).Append("; probek ").Append(n)
              .Append(", mnoznik predkosci sr. ").Append(nr > 0 ? (sumM / nr).ToString("0.000", ci) : "-")
              .Append(" (oczekiwany ").Append((1f - exp).ToString("0.000", ci)).Append(", min ").Append(nr > 0 ? mn.ToString("0.000", ci) : "-")
              .Append(", maks ").Append(nr > 0 ? mx.ToString("0.000", ci) : "-").Append(')')
              .Append(", bez wpisu Storm ").Append(noLine).Append(", kara zjedzona ").Append(eaten)
              .Append(", pamiec gry zgodna ").Append(cacheOk).Append('/').Append(cacheN)
              .Append(", na podlodze LimitMin (poza srednia) ").Append(atFloor);
            try
            {
                if (main != null && main.IsActive && main.IsCurrentlyAtSea && !main.IsTransitionInProgress)
                {
                    var p = main.Position;
                    bool inS = InStorm(list, p.X, p.Y);
                    float r0, r1, lmin, cached;
                    bool line;
                    if (Probe(model, main, out r0, out r1, out lmin, out line, out cached))
                        sb.Append(" | gracz: na morzu, w burzy ").Append(inS ? "tak" : "nie").Append(", bez kary ").Append(r0.ToString("0.000", ci)).Append(", z kara ").Append(r1.ToString("0.000", ci))
                          .Append(", wpis Storm ").Append(line ? "tak" : "nie").Append(", pamiec gry ").Append(cached.ToString("0.000", ci));
                    else sb.Append(" | gracz: audyt nieudany");
                }
                else sb.Append(" | gracz: na ladzie / w przejsciu / nieczynny");
            }
            catch (Exception e) { Stumble("Audit.Player", e); sb.Append(" | gracz: blad audytu"); }
        }

        private static bool Probe(PartySpeedModel model, MobileParty mp, out float r0, out float r1, out float lmin, out bool line, out float cached)
        {
            r0 = r1 = cached = 0f; lmin = float.MinValue; line = false;
            try
            {
                _suppress = true;
                try { var e0 = model.CalculateFinalSpeed(mp, model.CalculateBaseSpeed(mp, false)); r0 = e0.ResultNumber; lmin = e0.LimitMinValue; }
                finally { _suppress = false; }
                var en = model.CalculateFinalSpeed(mp, model.CalculateBaseSpeed(mp, true));
                r1 = en.ResultNumber;
                string storm = _txtStorm.ToString();
                foreach (var l in en.GetLines()) if (l.Item1 == storm) { line = true; break; }
                if (_lastSpeedRef != null) cached = _lastSpeedRef(mp);
                return true;
            }
            catch (Exception e) { Stumble("Audit.Probe", e); return false; }
            finally { _suppress = false; }
        }
    }
}
