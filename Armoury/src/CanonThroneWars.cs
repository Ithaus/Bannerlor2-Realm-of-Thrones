using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// 312c - AMBICJA ZELAZNEGO TRONU (nowe wojny o Westeros). Jeff 01.11: "kazdy chce byc i zostac krolem i zdobyc zelazny tron ... wszyscy chca wladac westeros
    /// poza polnoca ktora chce by odzielnym krolestwem ... poludniowi wladcy ... beda zawsze dazyli jak znajda sily aby westeros bylo jednym krolestwem";
    /// odpowiedzi: "Cisza do Krwawych Godow", "Po Bitwie Bekartow". Projekt rozp312/wojny-projekt.md rozdz. B-E, krytyka wojny-krytyk.md (POPRAWIONE PODSUMOWANIE).
    /// Linie "Wojny o tron (312c): ...", progi tools/sprawdz_logi.py --grupa tron312c.
    ///
    /// JAK: postfiks Priority.High (przed CrashScribe PovertyPeace - Normal) na BKDiplomacyModel.GetScoreOfDeclaringWar(IFaction, IFaction, IFaction, out TextObject,
    ///     CasusBelli, bool) -> ExplainedNumber (metoda zadeklarowana w BKDiplomacyModel - AccessTools.GetDeclaredMethods, filtr jak PovertyPeace.cs:262-279; pulapka
    ///     15.10). Wpinany z CanonWarsBehavior.OnSessionLaunched raz na proces (nigdy SubModuleMain). Ta jedna funkcja zasila bramke barteru gry, poparcie wnioskodawcy,
    ///     glosy rodow (takze BKDeclareWarDecision/BKWarBarterable), podpowiedz i BK WillJoinWar. Czlon = liczba plaska dodana po ocenie BK (nie mnozy kar BK).
    /// WYJSCIA (bez licznika bramek): klucz NIE; __2 == __0 (wywolanie z GetScoreOfDeclaringPeace - wynik pokoju bez zmian, jak PovertyPeace.cs:381); wynik <= -50000
    ///     (zakaz BK / zgody gry); strona nie krolestwo; A == T; A juz w wojnie z T.
    /// CZLON (pamiec doby na pare A->T, kasowana przy WarDeclared/MakePeace i zmianie pana King's Landing - limity wojen i przerwa zmieniaja sie w srodku doby):
    ///     = IronThroneAmbitionScore x w x R x (1 - T_biedy); bramki (zero, liczone na pare w tej kolejnosci): prolog (dzien opowiesci &lt; 1), brak tronu (town_EW4),
    ///     gracz (A = krolestwo gracza - wladca, wasal albo najemnik: "Never pushes your own realm into a war"; T = krolestwo gracza przy kluczu IronThroneTargetsPlayer NIE), Essos/poza Westeros (A albo T poza W), bez celu (brak wagi), kanon (przed
    ///     CanonWarClock.OverNow - cisza do Krwawych Godow w calym Westeros, decyzja Jeffa 1), Polnoc-przed-Bekartami (para z battania albo z sojusznikiem Polnocy - projekt C.3 - przed ROT BastardsSiegeEvent = 2,
    ///     decyzja Jeffa 2), limit (A ma &gt;= IronThroneMaxWars wrogow-krolestw bez Nocnej Strazy i Wolnych Ludzi), zmeczenie (Diplomacy A w ktorejkolwiek wojnie
    ///     &gt;= IronThroneWearyAt), sila (R = 0), przerwa (wojna o tron gdziekolwiek &lt; IronThroneWarGapDays dni temu), bieda (T_biedy &gt;= 0.5).
    /// W (Westeros): vlandia, dragonstone, stormlands, empire_w, empire, sturgia, aserai, riverlands, battania; najezdzcy valyrian (Aegon) i empire_s (Daenerys) dopiero
    ///     po najezdzie (ROT AegonInvasionEvent / DanyInvasionEvent stan &gt;= 1 albo twierdza o kulturze Westeros - PopulationLaw.WesterosCultures). Nocna Straz, Wolni
    ///     Ludzie, Skagos - poza regula. Essos: zadnego czlonu.
    /// AMBITNI: W bez battania; riverlands tylko poza sojuszem z battania (unia 272). WAGA w: T trzyma King's Landing - 1.0 "Claim to the Iron Throne"; A trzyma KL,
    ///     T = battania - 1.0 "The North must bend the knee"; A trzyma KL, T inny - 0.8 "Rebels against the Iron Throne"; pretendent kontra Polnoc - 0.5 "The North
    ///     must bend the knee" (decyzja Jeffa: "posiadacz King's Landing i pretendenci z sila"); pretendent kontra inny - 0.5 "Rival for the Iron Throne";
    ///     A = battania, T trzyma twierdze o kulturze battania - 0.8 "Northern lands held by a foreign lord" (klucz IronThroneNorthReclaims). Polnoc nigdy o tron.
    /// SILA R: ratio = sila A / (sila T + 0.5 x sojusznicy T + 0.5 x inni wrogowie-krolestwa A) (sojusznicy jak BK bkdm.cs:975-978; bez Nocnej Strazy, Wolnych Ludzi,
    ///     Innych); R = clamp((ratio - Min) / (Full - Min), 0, 1).
    /// BIEDA T_biedy (krytyk POPRAWKI 6; jedna regula na zjawisko): ta sama miara co CrashScribe PovertyPeace (domyslne ustawienia CS: zapas skarbca, bez jednorazowych
    ///     odplywow) - Armoury nie zalezy od CrashScribe, wiec liczy ja sama: historia skarbca krolestwa z 7 dob (od wczytania), ubytek/d bez skokow jednej doby (port
    ///     DrainNoOneOff), zapas = skarbiec / max(ubytek, 1000); &lt;= 28 dob - 1, &gt;= 91 - 0, liniowo; przed pierwsza doba - skarbiec / 1000 (jak PovertyPeace.Poverty).
    ///     Czlon x (1 - T), 0 przy T &gt;= 0.5 (prog wniosku z biedy E1b). PovertyPeace (po nas) dalej odejmuje swoj czlon od calej oceny (z naszym czlonem) - bieda
    ///     dziala na czlon dwa razy (x(1-T) tu i kara E1 od calosci; decyzja Jeffa 3, recenzja). Historia skarbca przesuwana razem z CS (ShiftHistory z BookHoldings.E1Shift).
    /// KLASYFIKACJA (CampaignEvents.WarDeclared): "wojna o tron" = detail CausedByKingdomDecision i para (A,T) miala dzis czlon &gt; 0 (droga: BK uzasadnienie, gdy
    ///     BKDeclareWarDecision na stosie, inaczej decyzja krolestwa) - ustawia przerwe (zapis arm_t312). "Dolaczenie" = CausedByCallToWarAgreement albo AddAlly/CallToWar/
    ///     WillJoinWar na stosie (168, AllianceCampaignBehavior, BK War.AddAlly), strona w W - tylko licznik. Pokoj w wojnie o tron - licznik.
    /// FLAGA TESTU: Documents\Mount and Blade II Bannerlord\CrashScribe\armoury-tron312-porw.flag, tylko DLL autotestu (LogGate.Mode 1/2, wzor CanonPeaceLockTest):
    ///     dla CZLONU kanon i fabula Polnocy jak skonczone (Krwawe Gody i Bitwa Bekartow za nami); 264/274/m104 i reszta bez zmian. Bez flagi - zachowanie produkcyjne.
    /// Bledy: per wywolanie, liczone (Stumble, "ERROR in CanonThroneWars.*"), bez globalnego wylacznika; wynik BK zostaje.
    /// </summary>
    internal static class CanonThroneWars
    {
        internal const string Tag = "Wojny o tron (312c): ";
        internal const string FlagName = "armoury-tron312-porw.flag";
        private const string SaveKey = "arm_t312";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        internal const string NorthId = "battania", RiverId = "riverlands", AegonId = "valyrian", DanyId = "empire_s", ThroneTownId = "town_EW4";
        private static readonly HashSet<string> WestCore = new HashSet<string>(StringComparer.Ordinal)
        {
            "vlandia", "dragonstone", "stormlands", "empire_w", "empire", "sturgia", "aserai", "riverlands", "battania"
        };
        // poza liczeniem wojen i sily (ROT zabrania im wojen, armia umarlych nie gasi ambicji Polnocy)
        private static readonly HashSet<string> NotCounted = new HashSet<string>(StringComparer.Ordinal) { "nightswatch", "freefolk" };

        private const float BkForbidden = -49999f;
        private const float PoorGate = 0.5f;
        // miara biedy jak CrashScribe PovertyPeace (stale tam: RunwayEmpty, RunwayFull, DrainFloor, DrainDays, OneOffTimes)
        private const float RunwayEmpty = 28f, RunwayFull = 91f, DrainFloor = 1000f, OneOffTimes = 3f;
        private const int DrainDays = 7;

        internal const string GProlog = "prolog", GNoThrone = "brak tronu", GPlayer = "gracz", GEssos = "Essos/poza Westeros", GNoAim = "bez celu", GCanon = "kanon",
                              GNorth = "Polnoc-przed-Bekartami", GLimit = "limit", GWeary = "zmeczenie", GStrength = "sila", GGap = "przerwa", GPoor = "bieda";
        private static readonly string[] GateOrder = { GCanon, GNorth, GLimit, GStrength, GGap, GWeary, GPoor, GPlayer, GEssos, GProlog, GNoThrone, GNoAim };

        private static Settings S { get { return Settings.Current; } }
        private static bool Key { get { var s = S; return s != null && s.IronThroneAmbition; } }
        private static float Score { get { var s = S; float v = s != null ? s.IronThroneAmbitionScore : 4000f; return v < 0f ? 0f : v > 20000f ? 20000f : v; } }
        private static int MaxWars { get { var s = S; int v = s != null ? s.IronThroneMaxWars : 2; return v < 1 ? 1 : v > 10 ? 10 : v; } }
        private static float StrMin { get { var s = S; float v = s != null ? s.IronThroneStrengthMin : 1.0f; return v < 0.5f ? 0.5f : v > 3f ? 3f : v; } }
        private static float StrFull { get { var s = S; float v = s != null ? s.IronThroneStrengthFull : 1.5f; v = v < 0.6f ? 0.6f : v > 5f ? 5f : v; return v <= StrMin ? StrMin + 0.1f : v; } }
        private static int GapDays { get { var s = S; int v = s != null ? s.IronThroneWarGapDays : 14; return v < 0 ? 0 : v > 364 ? 364 : v; } }
        private static float WearyAt { get { var s = S; float v = s != null ? s.IronThroneWearyAt : 50f; return v < 1f ? 1f : v > 100f ? 100f : v; } }
        private static bool SpareKey { get { var s = S; return s == null || s.IronThroneSpareFiveKings; } }
        private static bool NorthWaitKey { get { var s = S; return s == null || s.IronThroneNorthAfterBastards; } }
        private static bool ReclaimKey { get { var s = S; return s == null || s.IronThroneNorthReclaims; } }
        private static bool TargetsPlayerKey { get { var s = S; return s == null || s.IronThroneTargetsPlayer; } }

        // ---------------------------------------------------------------- stan
        private static readonly object _lock = new object();

        private sealed class Mem
        {
            public float Term, W, R, Pov; public int Wars; public string Gate = "", Why = "", Label = ""; public TextObject Text;
        }

        private static readonly Dictionary<string, Mem> _mem = new Dictionary<string, Mem>(StringComparer.Ordinal);   // pamiec doby A>T (kasowana przy wojnie/pokoju)
        private static int _memDay = int.MinValue;
        private static readonly Dictionary<string, string> _posToday = new Dictionary<string, string>(StringComparer.Ordinal);   // A>T z czlonem > 0 dzis -> opis
        private static readonly Dictionary<string, string> _posWhy = new Dictionary<string, string>(StringComparer.Ordinal);     // A>T -> powod (tron / bunt / ...)
        private static readonly Dictionary<string, float> _posTerm = new Dictionary<string, float>(StringComparer.Ordinal);   // A>T -> czlon ostatniej oceny
        private static readonly Dictionary<string, float> _posMax = new Dictionary<string, float>(StringComparer.Ordinal);    // A>T -> najwyzszy czlon dzis (linia doby)
        private static readonly Dictionary<string, int> _gatesDay = new Dictionary<string, int>(StringComparer.Ordinal);
        private static int _dCalls, _dCallsPair, _dCallsTerm, _dFlip, _dPairs, _tCalls;
        private static double _dSumBefore, _dSumAfter, _tSumBefore, _tSumAfter; private static int _tCallsTerm;

        // dzien (odswiezane przy zmianie doby albo kasowaniu pamieci)
        private static bool _flag, _canonOver, _northOpen, _aegonIn, _danyIn;
        private static int _bsState = -9, _bEvState = -9;   // recenzja: stan ROT BastardsSiegeEvent i BastardsEvent do linii (decyzja Jeffa, gdy oblezenie nie ruszy)
        private static bool _orderLogged;                  // recenzja: kolejnosc postfiksow raz w pierwszej dobie (po wpieciu PovertyPeace)
        private static Kingdom _holder; private static bool _throneExists;
        private static int _flagDay = int.MinValue;

        // zapis (arm_t312): dzien ostatniej wojny o tron, trwajace wojny o tron (A>T -> dzien)
        private static int _lastWarDay = int.MinValue;
        private static readonly Dictionary<string, int> _throneWars = new Dictionary<string, int>(StringComparer.Ordinal);
        // od wczytania / dzis (bez zapisu)
        private static int _dWars, _tWars, _dPeace, _tPeace, _dJoin, _tJoin, _dOther, _tOther, _tFeudPair;
        private static readonly List<string> _dWarList = new List<string>(), _dJoinList = new List<string>();

        private static int _stumbles, _stumblesDay;

        // bieda skarbca (miara PovertyPeace)
        private sealed class PState { public readonly List<KeyValuePair<int, int>> Hist = new List<KeyValuePair<int, int>>(); public float T; public bool Has; }
        private static readonly Dictionary<string, PState> _pov = new Dictionary<string, PState>(StringComparer.Ordinal);

        internal static void Stumble(string where, Exception e)
        {
            lock (_lock) { _stumbles++; _stumblesDay++; }
            if (_stumbles <= 5) Log.Error("CanonThroneWars." + where + " (potkniecie " + _stumbles + ")", e);
        }

        /// <summary>Prologue.ResetCampaign (OnGameStart kazdej kampanii) i przed Unpack: stan poprzedniej kampanii w procesie zerowany.</summary>
        internal static void Reset()
        {
            lock (_lock)
            {
                _mem.Clear(); _memDay = int.MinValue; _posToday.Clear(); _posWhy.Clear(); _posTerm.Clear(); _posMax.Clear(); _gatesDay.Clear();
                _dCalls = _dCallsPair = _dCallsTerm = _dFlip = _dPairs = _tCalls = _tCallsTerm = 0;
                _dSumBefore = _dSumAfter = _tSumBefore = _tSumAfter = 0.0;
                _flagDay = int.MinValue; _holder = null; _throneExists = false;
                _lastWarDay = int.MinValue; _throneWars.Clear();
                _dWars = _tWars = _dPeace = _tPeace = _dJoin = _tJoin = _dOther = _tOther = _tFeudPair = 0;
                _dWarList.Clear(); _dJoinList.Clear();
                _stumblesDay = 0;
                _pov.Clear();
                _bsState = _bEvState = -9; _orderLogged = false;
            }
        }

        // ---------------------------------------------------------------- zapis
        internal static string Pack()
        {
            lock (_lock)
            {
                var l = new List<string>();
                foreach (var kv in _throneWars) l.Add(kv.Key + "@" + kv.Value.ToString(Inv));
                return "v=1;g=" + (_lastWarDay == int.MinValue ? "-" : _lastWarDay.ToString(Inv)) + ";w=" + string.Join(",", l.ToArray()) + ";";
            }
        }

        internal static void Unpack(string v)
        {
            lock (_lock)
            {
                _lastWarDay = int.MinValue; _throneWars.Clear();
                if (string.IsNullOrEmpty(v)) return;
                foreach (var part in v.Split(';'))
                {
                    int i = part.IndexOf('=');
                    if (i <= 0) continue;
                    string k = part.Substring(0, i), val = part.Substring(i + 1);
                    int d;
                    if (k == "g" && int.TryParse(val, NumberStyles.Integer, Inv, out d)) _lastWarDay = d;
                    else if (k == "w" && val.Length > 0)
                        foreach (var it in val.Split(','))
                        {
                            int at = it.IndexOf('@');
                            if (at <= 0 || it.IndexOf('>') <= 0) continue;
                            if (int.TryParse(it.Substring(at + 1), NumberStyles.Integer, Inv, out d)) _throneWars[it.Substring(0, at)] = d;
                        }
                }
            }
        }

        internal static void Sync(IDataStore ds)
        {
            string v = "";
            try { if (ds.IsSaving) v = Pack(); } catch (Exception e) { Stumble("Pack", e); v = ""; }
            SaveText.Sync(ds, SaveKey, ref v);
            if (ds.IsLoading) { try { Unpack(v); } catch (Exception e) { Stumble("Unpack", e); } }
        }

        // ---------------------------------------------------------------- latka
        private static bool _tried;
        private static string _wire = "nie wpieta";
        private static MethodInfo _mWar;

        /// <summary>CanonWarsBehavior.OnSessionLaunched: raz na proces postfiks Priority.High na BKDiplomacyModel.GetScoreOfDeclaringWar (6 arg.).</summary>
        internal static void EnsureHooks()
        {
            if (_tried) return;
            _tried = true;
            try
            {
                var tBk = AccessTools.TypeByName("BannerKings.Models.Vanilla.BKDiplomacyModel");
                if (tBk == null) { _wire = "BRAK (BK nieobecny - brak BKDiplomacyModel)"; return; }
                foreach (var m in AccessTools.GetDeclaredMethods(tBk))
                {
                    if (m.Name != "GetScoreOfDeclaringWar" || m.ReturnType != typeof(ExplainedNumber)) continue;
                    var ps = m.GetParameters();
                    if (ps.Length == 6 && ps[0].ParameterType == typeof(IFaction) && ps[1].ParameterType == typeof(IFaction) && ps[2].ParameterType == typeof(IFaction)) { _mWar = m; break; }
                }
                if (_mWar == null) { _wire = "BRAK (BKDiplomacyModel.GetScoreOfDeclaringWar 6 arg.)"; return; }
                var h = new Harmony("com.jeff.armoury.throne312c");
                var r = h.Patch(_mWar, postfix: new HarmonyMethod(typeof(CanonThroneWars), nameof(WarScorePostfix)) { priority = Priority.High });
                _wire = r != null ? "TAK" : "BRAK";
            }
            catch (Exception e) { Stumble("EnsureHooks", e); _wire = "BLAD (" + e.GetType().Name + ")"; }
        }

        /// <summary>Kolejnosc postfiksow celu: nasz (High 600) wzgledem PovertyPeace CrashScribe (szukany po typie metody latki).</summary>
        private static string OrderText()
        {
            if (_mWar == null) return "-";
            try
            {
                var info = Harmony.GetPatchInfo(_mWar);
                if (info == null || info.Postfixes == null) return "brak informacji Harmony";
                int ours = int.MinValue, pp = int.MinValue;
                var all = new List<string>();
                foreach (var p in info.Postfixes)
                {
                    string t = p.PatchMethod != null && p.PatchMethod.DeclaringType != null ? p.PatchMethod.DeclaringType.Name : "?";
                    all.Add(t + "/" + p.priority);
                    if (t == "CanonThroneWars") ours = p.priority;
                    else if (t == "PovertyPeace") pp = p.priority;
                }
                string rel = pp == int.MinValue ? "PovertyPeace brak" : ours > pp ? "przed PovertyPeace TAK" : "przed PovertyPeace NIE";
                return rel + " (postfiksy: " + string.Join(", ", all.ToArray()) + ")";
            }
            catch { return "?"; }
        }

        // ---------------------------------------------------------------- flaga testu
        private static string FlagPath()
        {
            var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return Path.Combine(Path.Combine(Path.Combine(docs, "Mount and Blade II Bannerlord"), "CrashScribe"), FlagName);
        }

        private static bool ReadFlag()
        {
            int m = LogGate.Mode;
            if (m != 1 && m != 2) return false;   // tylko DLL autotestu CrashScribe
            try { return File.Exists(FlagPath()); } catch { return false; }
        }

        // ---------------------------------------------------------------- stan swiata na dobe (pod _lock)
        private static int Today() { try { return (int)CampaignTime.Now.ToDays; } catch { return 0; } }

        private static void RefreshDay(int day)
        {
            if (day == _memDay) return;
            _memDay = day;
            _mem.Clear();
            RefreshWorld(day);
        }

        private static void RefreshWorld(int day)
        {
            if (_flagDay != day) { _flagDay = day; _flag = ReadFlag(); }
            bool over;
            try { over = CanonWarClock.OverNow(); } catch { over = false; }
            _canonOver = _flag || !SpareKey || over;
            int bs = SafeRot("BastardsSiegeEvent");
            _bsState = bs; _bEvState = SafeRot("BastardsEvent");   // tylko do linii: BastardsEvent rusza dopiero po koncu oblezenia (ROT MainCheck)
            _northOpen = _flag || (NorthWaitKey ? bs == 2 : _canonOver);
            Settlement kl = null;
            try { kl = Settlement.Find(ThroneTownId); } catch { kl = null; }
            _throneExists = kl != null;
            _holder = kl != null && kl.OwnerClan != null ? kl.OwnerClan.Kingdom : null;
            _aegonIn = Landed(AegonId, "AegonInvasionEvent");
            _danyIn = Landed(DanyId, "DanyInvasionEvent");
        }

        private static int SafeRot(string ev) { try { return Prologue.RotEventState(ev); } catch { return -9; } }

        private static Kingdom FindK(string id)
        {
            foreach (var k in Kingdom.All) if (k != null && k.StringId == id) return k;
            return null;
        }

        /// <summary>Najezdzca w Westeros: ROT najazd stan >= 1 albo twierdza o kulturze Westeros (kultura z kalibracji).</summary>
        private static bool Landed(string id, string rotEvent)
        {
            try
            {
                if (SafeRot(rotEvent) >= 1) return true;
                var k = FindK(id);
                if (k == null || k.IsEliminated) return false;
                foreach (var t in k.Fiefs)
                {
                    var s = t != null ? t.Settlement : null;
                    if (s != null && PopulationLaw.WesterosCultures.Contains(PeopleBook.CultureAtCalibration(s))) return true;
                }
            }
            catch (Exception e) { Stumble("Landed", e); }
            return false;
        }

        private static bool InW(Kingdom k)
        {
            if (k == null || k.IsEliminated) return false;
            string id = k.StringId;
            if (WestCore.Contains(id)) return true;
            if (id == AegonId) return _aegonIn;
            if (id == DanyId) return _danyIn;
            return false;
        }

        private static bool Counted(Kingdom k)
        {
            if (k == null || k.IsEliminated || NotCounted.Contains(k.StringId)) return false;
            try { if (k.Culture != null && k.Culture.StringId == "whitewalker") return false; } catch { }
            return true;
        }

        private static bool Ambitious(Kingdom a)
        {
            if (!InW(a) || a.StringId == NorthId) return false;
            if (a.StringId == RiverId)
            {
                var n = FindK(NorthId);
                if (n != null && !n.IsEliminated && a.IsAllyWith(n)) return false;   // unia 272 - Dorzecze przy Polnocy nie siega po tron
            }
            return true;
        }

        private static bool HoldsNorthLand(Kingdom t)
        {
            foreach (var town in t.Fiefs)
            {
                var s = town != null ? town.Settlement : null;
                if (s != null && PeopleBook.CultureAtCalibration(s) == NorthId) return true;
            }
            return false;
        }

        /// <summary>Recenzja (projekt C.3): krolestwo w sojuszu gry z Polnoca - wojna z nim wciagnelaby Polnoc wezwaniem przed Bitwa Bekartow.</summary>
        private static bool NorthAlly(Kingdom k)
        {
            if (k == null || k.StringId == NorthId) return false;
            var n = FindK(NorthId);
            try { return n != null && !n.IsEliminated && k.IsAllyWith(n); } catch { return false; }
        }

        private static bool PlayerRealm(Kingdom k)
        {
            try { return k != null && Clan.PlayerClan != null && Clan.PlayerClan.Kingdom == k; } catch { return false; }
        }

        // ---------------------------------------------------------------- Diplomacy: zmeczenie wojna
        private static bool _weTried;
        private static PropertyInfo _pWem;
        private static MethodInfo _mWe;

        private static bool WearyReady()
        {
            if (!_weTried)
            {
                _weTried = true;
                try
                {
                    var tw = AccessTools.TypeByName("Diplomacy.WarExhaustion.WarExhaustionManager");
                    _pWem = tw != null ? AccessTools.Property(tw, "Instance") : null;
                    _mWe = tw != null ? AccessTools.Method(tw, "GetWarExhaustion", new[] { typeof(Kingdom), typeof(Kingdom) }) : null;
                    if (_pWem == null || _mWe == null || _mWe.ReturnType != typeof(float)) { _pWem = null; _mWe = null; }
                }
                catch { _pWem = null; _mWe = null; }
            }
            return _mWe != null;
        }

        /// <summary>Najwyzsze zmeczenie Diplomacy A w trwajacych wojnach z krolestwami (-1 = brak odczytu).</summary>
        private static float MaxWeariness(Kingdom a)
        {
            if (!WearyReady()) return -1f;
            object wem;
            try { wem = _pWem.GetValue(null, null); } catch { wem = null; }
            if (wem == null) return -1f;
            float mx = 0f;
            foreach (var e in FactionHelper.GetEnemyKingdoms(a))
            {
                if (e == null || e == a || e.IsEliminated) continue;
                float v;
                try { v = (float)_mWe.Invoke(wem, new object[] { a, e }); }
                catch (Exception ex) { Stumble("MaxWeariness", ex); return -1f; }   // recenzja: jak brak odczytu; para trafia do pamieci doby (jedno potkniecie na pare/dobe)
                if (v > mx) mx = v;
            }
            return mx;
        }

        // ---------------------------------------------------------------- bieda skarbca (port PovertyPeace.Update / DrainNoOneOff, ustawienia domyslne CS)

        /// <summary>Bieda skarbca T (0..1) jak CrashScribe PovertyPeace: stan z doby, przed pierwsza - z samego skarbca. Pod _lock.</summary>
        private static float PovertyOf(Kingdom k)
        {
            PState s;
            if (_pov.TryGetValue(k.StringId, out s) && s.Has) return s.T;
            int w = Math.Max(0, k.KingdomBudgetWallet);
            return Clamp01((RunwayFull - w / DrainFloor) / (RunwayFull - RunwayEmpty));
        }

        private static float Clamp01(float v) { return v < 0f ? 0f : (v > 1f ? 1f : v); }

        private static void PovertyDaily(int absDay)
        {
            foreach (var k in Kingdom.All)
            {
                if (k == null || k.IsEliminated) continue;
                try
                {
                    PState s;
                    if (!_pov.TryGetValue(k.StringId, out s)) { s = new PState(); _pov[k.StringId] = s; }
                    int wp = Math.Max(0, k.KingdomBudgetWallet);
                    if (s.Hist.Count > 0 && s.Hist[s.Hist.Count - 1].Key == absDay) s.Hist.RemoveAt(s.Hist.Count - 1);
                    s.Hist.Add(new KeyValuePair<int, int>(absDay, wp));
                    while (s.Hist.Count > 1 && s.Hist[0].Key < absDay - DrainDays) s.Hist.RemoveAt(0);
                    int span = absDay - s.Hist[0].Key;
                    float drain = span > 0 ? (s.Hist[0].Value - wp) / (float)span : 0f;
                    drain = DrainNoOneOff(s.Hist, drain);
                    float d = drain > 0f ? drain : 0f;
                    float runway = wp / Math.Max(d, DrainFloor);
                    s.T = Clamp01((RunwayFull - runway) / (RunwayFull - RunwayEmpty));
                    s.Has = true;
                }
                catch (Exception e) { _pov.Remove(k.StringId); Stumble("PovertyDaily", e); }
            }
        }

        /// <summary>Recenzja: kopia CrashScribe PovertyPeace.ShiftHistory - przelew miedzy skarbcami (BookHoldings.E1Shift: zwrot skarbca L5b-1, okup Neda) przesuwa
        /// cala historie krolestwa o delta, zeby przelew nie liczyl sie jako ubytek/doplyw w mierze biedy. Zwraca liczbe dob (-1 bez stanu). Nie rzuca.</summary>
        internal static int ShiftHistory(Kingdom k, long delta)
        {
            try
            {
                if (k == null) return -1;
                lock (_lock)
                {
                    PState s;
                    if (!_pov.TryGetValue(k.StringId, out s) || s == null) return -1;
                    if (delta == 0) return s.Hist.Count;
                    for (int i = 0; i < s.Hist.Count; i++)
                    {
                        long v = (long)s.Hist[i].Value + delta;
                        s.Hist[i] = new KeyValuePair<int, int>(s.Hist[i].Key, (int)Math.Max(0L, Math.Min((long)int.MaxValue, v)));
                    }
                    return s.Hist.Count;
                }
            }
            catch (Exception e) { Stumble("ShiftHistory", e); return -1; }
        }

        /// <summary>Kopia CrashScribe PovertyPeace.DrainNoOneOff (f19-kanon-a 5): ubytek na dobe bez skokow jednej doby w dol, nigdy wiekszy niz wzor prosty.</summary>
        internal static float DrainNoOneOff(List<KeyValuePair<int, int>> hist, float drainOld)
        {
            int m = hist.Count - 1;
            if (m < 2) return drainOld;
            var z = new float[m];
            var absZ = new float[m];
            for (int i = 0; i < m; i++)
            {
                int days = Math.Max(1, hist[i + 1].Key - hist[i].Key);
                z[i] = (hist[i + 1].Value - hist[i].Value) / (float)days;
                absZ[i] = Math.Abs(z[i]);
            }
            long kept = 0; int keptDays = 0;
            var others = new List<float>(m);
            for (int i = 0; i < m; i++)
            {
                others.Clear();
                for (int j = 0; j < m; j++) if (j != i) others.Add(absZ[j]);
                others.Sort();
                int o = others.Count;
                float med = o % 2 == 1 ? others[o / 2] : 0.5f * (others[o / 2 - 1] + others[o / 2]);
                float near = Math.Max(med, DrainFloor);
                if (i > 0) near = Math.Max(near, absZ[i - 1]);
                if (i < m - 1) near = Math.Max(near, absZ[i + 1]);
                long delta = (long)hist[i + 1].Value - hist[i].Value;
                if (z[i] < 0f && absZ[i] > OneOffTimes * near) continue;
                kept += delta;
                keptDays += Math.Max(1, hist[i + 1].Key - hist[i].Key);
            }
            float drainNew = keptDays > 0 ? -kept / (float)keptDays : 0f;
            if (drainNew > drainOld) return drainOld;
            return drainNew;
        }

        // ---------------------------------------------------------------- czlon

        private static string PairKey(Kingdom a, Kingdom t) { return a.StringId + ">" + t.StringId; }

        private static void Gate(Mem m, string g) { m.Gate = g; int v; _gatesDay.TryGetValue(g, out v); _gatesDay[g] = v + 1; }

        /// <summary>Pamiec doby pary A>T (pod _lock): czlon albo bramka.</summary>
        private static Mem Compute(Kingdom a, Kingdom t, int day)
        {
            var m = new Mem();
            _dPairs++;
            if (Prologue.Active && Prologue.StoryDays() < 1.0) { Gate(m, GProlog); return m; }
            if (!_throneExists) { Gate(m, GNoThrone); return m; }
            if (PlayerRealm(a) || (!TargetsPlayerKey && PlayerRealm(t))) { Gate(m, GPlayer); return m; }
            if (!InW(a) || !InW(t)) { Gate(m, GEssos); return m; }
            // waga celu
            float w = 0f; string why = "", label = "";
            bool aKl = _holder != null && _holder == a, tKl = _holder != null && _holder == t;
            if (a.StringId == NorthId)
            {
                if (ReclaimKey && HoldsNorthLand(t)) { w = 0.8f; why = "ziemie Polnocy"; label = "Northern lands held by a foreign lord"; }
            }
            else if (Ambitious(a))
            {
                if (tKl) { w = 1.0f; why = "tron"; label = "Claim to the Iron Throne"; }
                else if (aKl && t.StringId == NorthId) { w = 1.0f; why = "Polnoc"; label = "The North must bend the knee"; }
                else if (aKl) { w = 0.8f; why = "bunt"; label = "Rebels against the Iron Throne"; }
                else if (t.StringId == NorthId) { w = 0.5f; why = "Polnoc-pretendent"; label = "The North must bend the knee"; }
                else { w = 0.5f; why = "rywal"; label = "Rival for the Iron Throne"; }
            }
            if (w <= 0f) { Gate(m, GNoAim); return m; }
            m.W = w; m.Why = why; m.Label = label;
            if (!_canonOver) { Gate(m, GCanon); return m; }
            if (!_northOpen && (a.StringId == NorthId || t.StringId == NorthId || NorthAlly(a) || NorthAlly(t))) { Gate(m, GNorth); return m; }
            // limit wojen A
            int wars = 0, mx = MaxWars;
            float otherEnemies = 0f;
            foreach (var e in FactionHelper.GetEnemyKingdoms(a))
            {
                if (e == null || e == a || !Counted(e)) continue;
                wars++;
                if (e != t) otherEnemies += e.CurrentTotalStrength;
            }
            m.Wars = wars;
            if (wars >= mx) { Gate(m, GLimit); return m; }
            float weary = MaxWeariness(a);
            if (weary >= WearyAt) { Gate(m, GWeary); return m; }
            // sila R
            float allies = 0f;
            foreach (var k in Kingdom.All)
            {
                if (k == null || k == t || k == a || !Counted(k)) continue;
                bool ally = false;
                try { ally = t.IsAllyWith(k); } catch { ally = false; }
                if (ally) allies += k.CurrentTotalStrength;
            }
            float den = t.CurrentTotalStrength + 0.5f * allies + 0.5f * otherEnemies;
            float ratio = den > 0f ? a.CurrentTotalStrength / den : 10f;
            float r = Clamp01((ratio - StrMin) / (StrFull - StrMin));
            m.R = r;
            if (r <= 0f) { Gate(m, GStrength); return m; }
            int gap = GapDays;
            if (_lastWarDay != int.MinValue && day - _lastWarDay < gap) { Gate(m, GGap); return m; }
            float pov = PovertyOf(a);
            m.Pov = pov;
            if (pov >= PoorGate) { Gate(m, GPoor); return m; }
            m.Term = Score * w * r * (1f - pov);
            if (m.Term > 0f) m.Text = new TextObject("{=!}" + label);
            return m;
        }

        /// <summary>Postfiks BKDiplomacyModel.GetScoreOfDeclaringWar(declarer, target, evaluator, out reason, casusBelli, explanations). Nie rzuca.</summary>
        public static void WarScorePostfix(IFaction __0, IFaction __1, IFaction __2, ref ExplainedNumber __result)
        {
            try
            {
                if (!Key) return;
                if (__2 != null && ReferenceEquals(__2, __0)) return;   // wywolanie z GetScoreOfDeclaringPeace BK - wynik pokoju bez zmian
                var a = __0 as Kingdom; var t = __1 as Kingdom;
                lock (_lock) { _dCalls++; _tCalls++; }
                if (a == null || t == null || a == t || a.IsEliminated || t.IsEliminated) return;
                float s = __result.ResultNumber;
                if (s <= BkForbidden) return;
                if (FactionManager.IsAtWarAgainstFaction(a, t)) return;
                Mem m;
                int day = Today();
                lock (_lock)
                {
                    RefreshDay(day);
                    _dCallsPair++;
                    string key = PairKey(a, t);
                    if (!_mem.TryGetValue(key, out m)) { m = Compute(a, t, day); _mem[key] = m; }
                    if (m.Term <= 0f || m.Text == null) { _posWhy.Remove(key); _posTerm.Remove(key); return; }   // recenzja: klasyfikacja tylko przy czlonie w chwili glosu
                    _dCallsTerm++; _tCallsTerm++;
                    _dSumBefore += s; _dSumAfter += s + m.Term; _tSumBefore += s; _tSumAfter += s + m.Term;
                    if (s < 0f && s + m.Term >= 0f) _dFlip++;
                    _posTerm[key] = m.Term; _posWhy[key] = m.Why;   // recenzja: stan z OSTATNIEJ oceny (klasyfikacja), linia doby - najwyzszy czlon dnia
                    float prev;
                    if (!_posMax.TryGetValue(key, out prev) || m.Term > prev)
                    {
                        _posMax[key] = m.Term;
                        _posToday[key] = key + " w=" + m.W.ToString("0.00", Inv) + " R=" + m.R.ToString("0.00", Inv) + " T=" + m.Pov.ToString("0.00", Inv)
                                         + " wojny=" + m.Wars + " czlon=" + m.Term.ToString("0", Inv) + " (" + m.Why + ")";
                    }
                }
                __result.Add(m.Term, m.Text);
            }
            catch (Exception e) { Stumble("WarScorePostfix", e); }
        }

        // ---------------------------------------------------------------- zdarzenia (CanonWarsBehavior.RegisterEvents)

        private static string KName(IFaction f) { try { return f != null && f.Name != null ? f.Name.ToString() : "?"; } catch { return "?"; } }

        private static bool StackHas(params string[] needles)
        {
            try
            {
                var st = new StackTrace(3, false);   // StackHas, OnWarLocked, OnWar
                for (int i = 0; i < st.FrameCount && i < 50; i++)
                {
                    var mm = st.GetFrame(i)?.GetMethod();
                    if (mm == null) continue;
                    string n = (mm.DeclaringType != null ? mm.DeclaringType.FullName + "." : "") + (mm.Name ?? "");   // DMD (metoda zalatana) - nazwa zawiera typ
                    foreach (var x in needles) if (n.IndexOf(x, StringComparison.Ordinal) >= 0) return true;
                }
            }
            catch { }
            return false;
        }

        /// <summary>CampaignEvents.WarDeclared: wojna o tron (decyzja krolestwa + czlon > 0 dzis) / dolaczenie / inna decyzja w Westeros. Kasuje pamiec doby.</summary>
        internal static void OnWar(IFaction f1, IFaction f2, DeclareWarAction.DeclareWarDetail detail)
        {
            try
            {
                var a = f1 as Kingdom; var t = f2 as Kingdom;
                if (a == null || t == null) return;
                int day = Today();
                lock (_lock)
                {
                    RefreshDay(day);
                    string key = PairKey(a, t);
                    bool inW = InW(a) || InW(t);
                    _mem.Clear();   // limity wojen i przerwa zmieniaja sie teraz
                    // recenzja: po klasyfikacji zdejmujemy tylko TE pare (wezwania sojusznikow AllianceCampaignBehavior/168/BK rozstrzygaja sie w zdarzeniu WarDeclared
                    // PRZED naszym sluchaczem - czyszczenie calego _posWhy zgubiloby zewnetrzna wojne o tron); inne pary bez czlonu zdejmuje postfiks przy nowej ocenie
                    try { OnWarLocked(a, t, detail, day, key, inW); }
                    finally { _posWhy.Remove(key); _posTerm.Remove(key); }
                }
            }
            catch (Exception e) { Stumble("OnWar", e); }
        }

        /// <summary>Klasyfikacja wypowiedzenia (pod _lock, z OnWar).</summary>
        private static void OnWarLocked(Kingdom a, Kingdom t, DeclareWarAction.DeclareWarDetail detail, int day, string key, bool inW)
        {
                    string why;
                    if (detail == DeclareWarAction.DeclareWarDetail.CausedByKingdomDecision && _posWhy.TryGetValue(key, out why))
                    {
                        float term; _posTerm.TryGetValue(key, out term);
                        string path = StackHas("BKDeclareWarDecision") ? "BK uzasadnienie (BKDeclareWarDecision)" : "decyzja krolestwa";
                        bool feud = Prologue.FeudKeyOf(a.StringId, t.StringId) != null;
                        if (feud) _tFeudPair++;
                        _lastWarDay = day;
                        _throneWars[key] = day;
                        _dWars++; _tWars++;
                        if (_dWarList.Count < 10) _dWarList.Add(key + " (" + why + ")");
                        Log.Info(Tag + "dzien " + day + " | WOJNA O TRON " + key + " (" + KName(a) + " -> " + KName(t) + "): powod " + why + ", droga " + path
                                 + ", czlon " + term.ToString("0", Inv) + ", dawna para zatargu " + (feud ? "TAK" : "NIE") + ", przerwa do dnia " + (day + GapDays)
                                 + ", kanon skonczony " + T(_canonOver) + ", Polnoc po Bekartach " + T(_northOpen) + ", flaga testu " + T(_flag) + ".");
                        return;
                    }
                    if (!inW) return;
                    if (detail == DeclareWarAction.DeclareWarDetail.CausedByCallToWarAgreement || StackHas("AddAlly", "CallToWar", "WillJoinWar"))
                    {
                        _dJoin++; _tJoin++;
                        if (_dJoinList.Count < 10) _dJoinList.Add(key);
                        Log.Info(Tag + "dzien " + day + " | DOLACZENIE " + key + " (" + KName(a) + " -> " + KName(t) + "): " + detail + ".");
                        return;
                    }
                    if (detail == DeclareWarAction.DeclareWarDetail.CausedByKingdomDecision) { _dOther++; _tOther++; }
        }

        /// <summary>CampaignEvents.MakePeace: pokoj w wojnie o tron (licznik). Kasuje pamiec doby.</summary>
        internal static void OnPeace(IFaction f1, IFaction f2, MakePeaceAction.MakePeaceDetail detail)
        {
            try
            {
                var a = f1 as Kingdom; var t = f2 as Kingdom;
                if (a == null || t == null) return;
                int day = Today();
                lock (_lock)
                {
                    _mem.Clear();
                    string k1 = PairKey(a, t), k2 = PairKey(t, a), key = null;
                    int since;
                    if (_throneWars.TryGetValue(k1, out since)) key = k1;
                    else if (_throneWars.TryGetValue(k2, out since)) key = k2;
                    if (key == null) return;
                    _throneWars.Remove(key);
                    _dPeace++; _tPeace++;
                    Log.Info(Tag + "dzien " + day + " | POKOJ w wojnie o tron " + key + " (" + KName(a) + " - " + KName(t) + "), wojna od dnia " + since + ", " + detail + ".");
                }
            }
            catch (Exception e) { Stumble("OnPeace", e); }
        }

        /// <summary>Recenzja: OnSettlementOwnerChanged - zmiana pana King's Landing odswieza posiadacza tronu i pamiec doby od razu (wojna/pokoj tego nie robia).</summary>
        internal static void OnOwnerChanged(Settlement st, bool openToClaim, Hero newOwner, Hero oldOwner, Hero capturer,
                                            ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
        {
            try
            {
                if (st == null || st.StringId != ThroneTownId) return;
                lock (_lock) { _memDay = int.MinValue; }   // _posWhy/_posTerm odswieza postfiks przy nastepnej ocenie
            }
            catch (Exception e) { Stumble("OnOwnerChanged", e); }
        }

        /// <summary>Recenzja: wojny o tron bez pokoju (krolestwo zniszczone - DestroyKingdomAction nie wola MakePeace - albo wojny juz nie ma) zdejmowane z zapisu. Pod _lock.</summary>
        private static void PruneThroneWars(int day)
        {
            if (_throneWars.Count == 0) return;
            var gone = new List<string>();
            var txt = new List<string>();
            foreach (var kv in _throneWars)
            {
                int gt = kv.Key.IndexOf('>');
                var a = gt > 0 ? FindK(kv.Key.Substring(0, gt)) : null;
                var t = gt > 0 ? FindK(kv.Key.Substring(gt + 1)) : null;
                bool dead = a == null || t == null || a.IsEliminated || t.IsEliminated;
                if (dead || !FactionManager.IsAtWarAgainstFaction(a, t))
                {
                    gone.Add(kv.Key);
                    txt.Add(kv.Key + "@" + kv.Value.ToString(Inv) + (dead ? " (krolestwo zniszczone)" : " (bez wojny)"));
                }
            }
            foreach (var g in gone) _throneWars.Remove(g);
            if (gone.Count > 0) Log.Info(Tag + "dzien " + day + " | SPRZATNIETE wojny o tron bez pokoju: " + string.Join(", ", txt.ToArray()) + ".");
        }

        // ---------------------------------------------------------------- doba i linie

        /// <summary>CanonWarsBehavior.OnDaily: bieda skarbca (historia doby), linia doby, zerowanie licznikow doby.</summary>
        internal static void Daily()
        {
            int day = Today();
            string line;
            lock (_lock)
            {
                try { PovertyDaily(day); } catch (Exception e) { Stumble("PovertyDaily", e); }
                try { PruneThroneWars(day); } catch (Exception e) { Stumble("PruneThroneWars", e); }
                if (!_orderLogged)
                {
                    _orderLogged = true;
                    try { Log.Info(Tag + "kolejnosc latek (pierwsza doba) | " + OrderText() + "."); } catch (Exception e) { Stumble("OrderText", e); }
                }
                try { RefreshWorld(day); } catch (Exception e) { Stumble("RefreshWorld", e); }
                line = DayLine(day);
                _mem.Clear(); _memDay = int.MinValue;
                _posToday.Clear(); _posWhy.Clear(); _posTerm.Clear(); _posMax.Clear(); _gatesDay.Clear();
                _dCalls = _dCallsPair = _dCallsTerm = _dFlip = _dPairs = 0;
                _dSumBefore = _dSumAfter = 0.0;
                _dWars = _dPeace = _dJoin = _dOther = 0; _dWarList.Clear(); _dJoinList.Clear();
                _stumblesDay = 0;
            }
            Log.Info(line);
        }

        private static string T(bool b) { return b ? "TAK" : "NIE"; }

        private static string WearyText() { return WearyReady() ? "odczyt Diplomacy" : "brak odczytu"; }

        private static string HolderText()
        {
            if (!_throneExists) return "brak (" + ThroneTownId + ")";
            return _holder != null ? KName(_holder) + " (" + _holder.StringId + ")" : "bez krolestwa";
        }

        private static string DayLine(int day)
        {
            var sb = new StringBuilder(1200);
            sb.Append(Tag).Append("doba | dzien ").Append(day)
              .Append(" | klucz ").Append(T(Key)).Append(", flaga testu ").Append(T(_flag)).Append(", kanon skonczony ").Append(T(_canonOver))
              .Append(", Polnoc po Bekartach ").Append(T(_northOpen)).Append(" (BastardsSiegeEvent ").Append(_bsState).Append(", BastardsEvent ").Append(_bEvState).Append(')')
              .Append(" | tron: ").Append(HolderText())
              .Append(" | limity: max wojen ").Append(MaxWars).Append(", sila ").Append(StrMin.ToString("0.00", Inv)).Append('-').Append(StrFull.ToString("0.00", Inv))
              .Append(", przerwa ").Append(GapDays).Append(" dni, zmeczenie ").Append(WearyAt.ToString("0", Inv)).Append(" (").Append(WearyText()).Append(')')
              .Append(" | wywolania postfiksu dzis ").Append(_dCalls).Append(", z ocena pary ").Append(_dCallsPair).Append(", z czlonem ").Append(_dCallsTerm)
              .Append(" (srednia przed ").Append(_dCallsTerm > 0 ? (_dSumBefore / _dCallsTerm).ToString("0", Inv) : "-")
              .Append(", po ").Append(_dCallsTerm > 0 ? (_dSumAfter / _dCallsTerm).ToString("0", Inv) : "-")
              .Append("), czlon zmienil znak ").Append(_dFlip)
              .Append(" | ocen par ").Append(_dPairs)
              .Append(" | czlon > 0: ").Append(_posToday.Count).Append(" par");
            if (_posToday.Count > 0)
            {
                var l = new List<string>(_posToday.Values);
                l.Sort(StringComparer.Ordinal);
                if (l.Count > 20) { int more = l.Count - 20; l = l.GetRange(0, 20); l.Add("i " + more + " innych"); }
                sb.Append(" [").Append(string.Join("; ", l.ToArray())).Append(']');
            }
            sb.Append(" | zero przez bramke: ");
            for (int i = 0; i < GateOrder.Length; i++)
            {
                int v; _gatesDay.TryGetValue(GateOrder[i], out v);
                if (i > 0) sb.Append(", ");
                sb.Append(GateOrder[i]).Append(' ').Append(v);
            }
            sb.Append(" | wojny o tron dzis ").Append(_dWars);
            if (_dWarList.Count > 0) sb.Append(" [").Append(string.Join(", ", _dWarList.ToArray())).Append(']');
            sb.Append(", od wczytania ").Append(_tWars).Append(", trwajace ").Append(_throneWars.Count)
              .Append(", pokoje w nich dzis ").Append(_dPeace).Append(", od wczytania ").Append(_tPeace)
              .Append(", w dawnej parze zatargu od wczytania ").Append(_tFeudPair)
              .Append(" | dolaczenia dzis ").Append(_dJoin);
            if (_dJoinList.Count > 0) sb.Append(" [").Append(string.Join(", ", _dJoinList.ToArray())).Append(']');
            sb.Append(", od wczytania ").Append(_tJoin)
              .Append(" | inne wypowiedzenia w Westeros (decyzja krolestwa, bez czlonu) dzis ").Append(_dOther).Append(", od wczytania ").Append(_tOther)
              .Append(" | przerwa do dnia ").Append(_lastWarDay == int.MinValue ? "-" : (_lastWarDay + GapDays).ToString(Inv))
              .Append(" | od wczytania: wywolan ").Append(_tCalls).Append(", z czlonem ").Append(_tCallsTerm)
              .Append(" (srednia przed ").Append(_tCallsTerm > 0 ? (_tSumBefore / _tCallsTerm).ToString("0", Inv) : "-")
              .Append(", po ").Append(_tCallsTerm > 0 ? (_tSumAfter / _tCallsTerm).ToString("0", Inv) : "-").Append(')')
              .Append(" | potkniecia dzis ").Append(_stumblesDay).Append(" (razem ").Append(_stumbles).Append(").");
            return sb.ToString();
        }

        /// <summary>CanonWarsBehavior.OnSessionLaunched (po EnsureHooks): latka, kolejnosc wzgledem PovertyPeace, klucze, flaga, stan z zapisu.</summary>
        internal static void StartLine()
        {
            string line;
            lock (_lock)
            {
                int day = Today();
                _flagDay = int.MinValue;
                try { RefreshWorld(day); } catch (Exception e) { Stumble("RefreshWorld", e); }
                var wars = new List<string>();
                foreach (var kv in _throneWars) wars.Add(kv.Key + "@" + kv.Value.ToString(Inv));
                var sb = new StringBuilder(900);
                sb.Append(Tag).Append("start sesji | latka GetScoreOfDeclaringWar ").Append(_wire).Append(" | kolejnosc: ").Append(OrderText())
                  .Append(" | klucze: Ambition for the Iron Throne ").Append(T(Key)).Append(", Score ").Append(Score.ToString("0", Inv))
                  .Append(", Max Wars ").Append(MaxWars).Append(", Strength ").Append(StrMin.ToString("0.00", Inv)).Append('-').Append(StrFull.ToString("0.00", Inv))
                  .Append(", War Gap Days ").Append(GapDays).Append(", Weary At ").Append(WearyAt.ToString("0", Inv))
                  .Append(", Spare Five Kings ").Append(T(SpareKey)).Append(", North After Bastards ").Append(T(NorthWaitKey))
                  .Append(", North Reclaims ").Append(T(ReclaimKey)).Append(", Targets Player ").Append(T(TargetsPlayerKey))
                  .Append(" | flaga testu ").Append(FlagName).Append(' ').Append(T(_flag)).Append(LogGate.Mode == 1 || LogGate.Mode == 2 ? "" : " (gra - flaga nieczytana)")
                  .Append(" | kanon skonczony ").Append(T(_canonOver)).Append(", Polnoc po Bekartach ").Append(T(_northOpen))
                  .Append(" (BastardsSiegeEvent ").Append(_bsState).Append(", BastardsEvent ").Append(_bEvState).Append(')')
                  .Append(", najazd Aegona ").Append(T(_aegonIn)).Append(", najazd Daenerys ").Append(T(_danyIn))
                  .Append(" | tron: ").Append(HolderText())
                  .Append(" | zmeczenie: ").Append(WearyText())
                  .Append(" | z zapisu: ostatnia wojna o tron ").Append(_lastWarDay == int.MinValue ? "-" : "dzien " + _lastWarDay.ToString(Inv))
                  .Append(", trwajace wojny o tron ").Append(_throneWars.Count).Append(wars.Count > 0 ? " [" + string.Join(", ", wars.ToArray()) + "]" : "")
                  .Append(" | potkniecia ").Append(_stumbles).Append('.');
                line = sb.ToString();
            }
            Log.Info(line);
        }
    }
}
