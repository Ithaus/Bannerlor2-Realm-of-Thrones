using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace CrashScribe
{
    /// <summary>P1 audytu modow 14.10 (docs/audyt-modow-2026-10-14, AM-26 / AM-27 / AM-04) - tytuly BannerKings na mapie ROT.
    /// AM-26 (klucz BkTitlesUseRotFile): BK czyta plik tytulow spod BannerKingsConfig.TitlesGeneratorPath w TitleGenerator.InitializeTitles
    /// (nowa gra; BannerKingsConfig.InitManagers) i w TitleGenerator.RetrofitOrphanedTitles (KAZDE wczytanie - BKManagerBehavior.OnGameLoaded).
    /// BKROTPatch ustawia sciezke ROT tylko w OnNewGameCreated (BKROTBehavior; OnGameLoaded pusty, UpdateTitles martwe), wiec wczytanie w nowym
    /// procesie gry bierze domyslny kalradyjski titles.xml BK: retrofit zaklada 30 ksiestw Calradii i odpina od ksiestw ROT ich hrabstwa
    /// (savescan3: zapisy Jeffa 30/33 ksiestw kalradyjskich). Prefiksy Priority.First na obu metodach ustawiaja sciezke ROT tuz przed odczytem -
    /// zawsze (nowa gra, wczytanie, nowy proces). Regula wyboru jak BKROT: mapa z ROT_town31 -> titles_6_3, inaczej titles_6_2; bez BKROTPatch
    /// nic (mapa Calradii, plik BK jest dobry).
    /// AM-27 (klucz BkTitlesRot818, dziala tylko z BkTitlesUseRotFile): titles_6_3.xml jest z ROT 6.x - 25 twierdz bez tytulu, Lorath / Ibben /
    /// Qarth bez tytulu krolewskiego, baronie-widma, stare id bohaterow. Nasz plik (tools/bk_titles_rot818.py -> res/bk_titles_rot818.xml, zasob
    /// DLL) zapisywany przy starcie procesu do Documents\Mount and Blade II Bannerlord\CrashScribe (folder gry blokuje nowe pliki danych)
    /// i podawany BK zamiast titles_6_3. Przed kazdym uzyciem tresc pliku porownana z zasobem (dysk C u Jeffa potrafi wyzerowac plik) -
    /// inna = zapis od nowa, nieudany = titles_6_3 BKROT.
    /// AM-04: NRE w BKCapitalBehavior.GetIdealCapital (miasto rodu panujacego bez tytulu, :110-111) dotyczylo tylko Driftwood Hall (Skagos) -
    /// w naszym pliku kazde miasto ma tytul, wiec bez latki; kontrola - linia sesji "twierdz bez tytulu 0" i bk-firstchance.log.
    /// Linie: start procesu (wpiecie, plik), kazde ustawienie sciezki, wynik retrofitu, pierwsza godzina sesji (twierdze, wsie, krolestwa bez
    /// tytulu, ksiestwa spoza pliku). Pliki BK / BKROTPatch nietkniete; klucze wylaczone = zachowanie b47fecc3.</summary>
    internal static class BkTitles
    {
        internal const string FileName = "bk_titles_rot818.xml";
        internal const string Resource = "CrashScribe.bk_titles_rot818.xml";   // tez StoryThrone (ustroj krolestw z pliku)

        private static PropertyInfo _pInst, _pPath, _pTm, _pAll, _pType, _pShort;
        private static MethodInfo _mGetTitle, _mGetSov;
        private static bool _hooked;
        private static byte[] _res;
        private static string _ourFile;
        private static int _rewrites, _rewritesAtStart;   // zapisy pliku od nowa (tresc inna niz zasob); po starcie procesu = dysk zmienil plik
        private static int _retroN = -1;
        private static string _retroPath;
        private static bool _checked;
        private static string _namesPath;
        private static HashSet<string> _names;
        // AUD-P1-5 (klucz BkTitlesVillageFix): czesci BK do dorobki tytulu wsi w biegnacej kampanii - TitleGenerator.CreateLordship i TitleManager.ExecuteAddTitle
        // (internal, tylko wywolania - bez latki Harmony), FeudalTitle: Contract, Vassals, deJure, Sovereign, SetSovereign; brak ktorejs = tylko nazwy wsi w linii
        private static MethodInfo _mCreateLordship, _mAddTitle, _mSetSov;
        private static PropertyInfo _pContract, _pVassals, _pDeJure, _pSov;
        private static string _fixMissing = "czesci BK nieodczytane (Install)";

        /// <summary>Start procesu (OnBeforeInitialModuleScreenSetAsRoot, po zaladowaniu BK i BKROTPatch). Nie rzuca.</summary>
        internal static void Install(Harmony harmony)
        {
            try
            {
                if (!Config.BkTitlesUseRotFile)
                {
                    Scribe.Line("Mends: tytuly BK (AM-26) wylaczone (BkTitlesUseRotFile=false) - sciezka tytulow jak BK / BKROTPatch (wczytanie w nowym procesie gry bierze kalradyjski titles.xml BK); plik ROT 8.1.8 (AM-27) nieuzywany.");
                    return;
                }
                var tGen = AccessTools.TypeByName("BannerKings.Managers.Helpers.TitleGenerator");
                var tCfg = AccessTools.TypeByName("BannerKings.BannerKingsConfig");
                bool bkrot = AccessTools.TypeByName("BKROTPatch.Behaviors.BKROTBehavior") != null;
                if (tGen == null || tCfg == null)
                {
                    Scribe.Line("Mends: tytuly BK (AM-26) - BannerKings nieobecny (TitleGenerator / BannerKingsConfig) - nic do zrobienia.");
                    return;
                }
                if (!bkrot)
                {
                    Scribe.Line("Mends: tytuly BK (AM-26) - BKROTPatch nieobecny - sciezka tytulow BK bez zmian (mapa Calradii).");
                    return;
                }
                var mInit = AccessTools.Method(tGen, "InitializeTitles", Type.EmptyTypes);
                var mRetro = AccessTools.Method(tGen, "RetrofitOrphanedTitles", Type.EmptyTypes);
                _pInst = AccessTools.Property(tCfg, "Instance");
                _pPath = AccessTools.Property(tCfg, "TitlesGeneratorPath");
                _pTm = AccessTools.Property(tCfg, "TitleManager");
                var tTm = _pTm != null ? _pTm.PropertyType : null;
                _mGetTitle = tTm != null ? AccessTools.Method(tTm, "GetTitle", new[] { typeof(Settlement) }) : null;
                _mGetSov = tTm != null ? AccessTools.Method(tTm, "GetSovereignTitle", new[] { typeof(Kingdom) }) : null;
                _pAll = tTm != null ? AccessTools.Property(tTm, "AllTitles") : null;
                var tFt = _mGetTitle != null ? _mGetTitle.ReturnType : null;
                _pType = tFt != null ? AccessTools.Property(tFt, "TitleType") : null;
                _pShort = tFt != null ? AccessTools.Property(tFt, "shortName") : null;
                // AUD-P1-5: dorobka tytulu wsi tak, jak BK zaklada go przy starcie gry (TitleGenerator.CreateLandedTitle: CreateLordship + ExecuteAddTitle + wasal twierdzy;
                // GenerateKingdom: SetSovereign); refleksja raz, tu - przed kontrola latek sciezki (linia sesji biegnie takze bez nich)
                _pContract = tFt != null ? AccessTools.Property(tFt, "Contract") : null;
                _pVassals = tFt != null ? AccessTools.Property(tFt, "Vassals") : null;
                _pDeJure = tFt != null ? AccessTools.Property(tFt, "deJure") : null;
                _pSov = tFt != null ? AccessTools.Property(tFt, "Sovereign") : null;
                _mSetSov = tFt != null ? AccessTools.Method(tFt, "SetSovereign", new[] { tFt }) : null;
                _mCreateLordship = _pContract != null ? AccessTools.Method(tGen, "CreateLordship", new[] { typeof(Settlement), typeof(Hero), _pContract.PropertyType }) : null;
                _mAddTitle = tTm != null && tFt != null ? AccessTools.Method(tTm, "ExecuteAddTitle", new[] { tFt }) : null;
                var fixMiss = new List<string>();
                if (_mCreateLordship == null || _mCreateLordship.ReturnType != tFt) fixMiss.Add("TitleGenerator.CreateLordship");
                if (_mAddTitle == null) fixMiss.Add("TitleManager.ExecuteAddTitle");
                if (_pContract == null || _pVassals == null || _pDeJure == null || _pDeJure.PropertyType != typeof(Hero) || _pSov == null || _mSetSov == null)
                    fixMiss.Add("FeudalTitle.Contract / Vassals / deJure / Sovereign / SetSovereign");
                _fixMissing = fixMiss.Count > 0 ? string.Join(", ", fixMiss.ToArray()) : null;
                var miss = new List<string>();
                if (mInit == null) miss.Add("TitleGenerator.InitializeTitles");
                if (mRetro == null || mRetro.ReturnType != typeof(int)) miss.Add("TitleGenerator.RetrofitOrphanedTitles");
                if (_pInst == null || _pPath == null || _pPath.PropertyType != typeof(string) || !_pPath.CanWrite) miss.Add("BannerKingsConfig.Instance / TitlesGeneratorPath");
                if (miss.Count > 0)
                {
                    Scribe.Line("Mends: tytuly BK (AM-26) - OSTRZEZENIE: brak " + string.Join(", ", miss.ToArray()) + " (inna wersja BK) - latki nie wpiete, sciezka jak BK / BKROTPatch.");
                    return;
                }
                string fileNote = "nieuzywany (BkTitlesRot818=false) - titles_6_3 BKROT";
                if (Config.BkTitlesRot818)
                {
                    _res = ReadResource();
                    if (_res == null) fileNote = "BRAK zasobu " + Resource + " w DLL - titles_6_3 BKROT";
                    else fileNote = EnsureOurFile(out string note) ? "plik " + _ourFile + " (" + note + ")" : "NIEUDANY zapis (" + note + ") - titles_6_3 BKROT";
                    _rewritesAtStart = _rewrites;
                }
                harmony.Patch(mInit, prefix: new HarmonyMethod(typeof(BkTitles), nameof(InitPrefix)) { priority = Priority.First });
                harmony.Patch(mRetro, prefix: new HarmonyMethod(typeof(BkTitles), nameof(RetroPrefix)) { priority = Priority.First },
                              postfix: new HarmonyMethod(typeof(BkTitles), nameof(RetroPostfix)));
                _hooked = Ours(mInit, harmony.Id) && Ours(mRetro, harmony.Id);
                Scribe.Line("Mends: tytuly BK (AM-26) - latki sciezki wpiete " + (_hooked ? "TAK" : "NIE (brak w Harmony.GetPatchInfo)")
                            + " (prefiksy TitleGenerator.InitializeTitles i RetrofitOrphanedTitles: sciezka ROT przed kazdym odczytem - nowa gra i kazde wczytanie);"
                            + " plik ROT 8.1.8 (AM-27, BkTitlesRot818=" + (Config.BkTitlesRot818 ? "true" : "false") + "): " + fileNote + ".");
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "BkTitles.Install", null); } catch { } }
        }

        private static bool Ours(MethodBase m, string id)
        {
            try
            {
                var info = Harmony.GetPatchInfo(m);
                if (info == null) return false;
                foreach (var p in info.Prefixes) if (p.owner == id) return true;
            }
            catch { }
            return false;
        }

        private static byte[] ReadResource()
        {
            try
            {
                using (var s = typeof(BkTitles).Assembly.GetManifestResourceStream(Resource))
                {
                    if (s == null) return null;
                    using (var ms = new MemoryStream())
                    {
                        s.CopyTo(ms);
                        return ms.ToArray();
                    }
                }
            }
            catch { return null; }
        }

        private static bool Same(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        /// <summary>Plik ROT 8.1.8 na dysku = zasob DLL (zapis, gdy brak albo inna tresc; odczyt kontrolny). Nie rzuca.</summary>
        private static bool EnsureOurFile(out string note)
        {
            note = "";
            try
            {
                if (_res == null) { note = "brak zasobu"; return false; }
                var dir = Scribe.ReportDir;
                if (string.IsNullOrEmpty(dir))
                    dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Mount and Blade II Bannerlord", "CrashScribe");
                Directory.CreateDirectory(dir);
                var path = Path.Combine(dir, FileName);
                bool same = File.Exists(path) && Same(File.ReadAllBytes(path), _res);
                if (!same)
                {
                    File.WriteAllBytes(path, _res);
                    _rewrites++;
                    if (!Same(File.ReadAllBytes(path), _res)) { note = "odczyt po zapisie inny niz zasob"; _ourFile = null; return false; }
                }
                _ourFile = path;
                note = (same ? "bez zmian" : "zapisany") + ", " + _res.Length + " B";
                return true;
            }
            catch (Exception e) { note = e.GetType().Name + ": " + e.Message; _ourFile = null; return false; }
        }

        /// <summary>Sciezka, ktora BK ma czytac: nasz plik ROT 8.1.8 albo plik BKROT wedlug jego reguly (ROT_town31 -> 6_3, inaczej 6_2).</summary>
        private static string WantedPath(out string kind)
        {
            bool v63 = false;
            try
            {
                foreach (var s in Settlement.All)
                    if (s != null && s.StringId == "ROT_town31") { v63 = true; break; }
            }
            catch { }
            if (Config.BkTitlesRot818 && v63 && _res != null && EnsureOurFile(out _)) { kind = "ROT 8.1.8 (CrashScribe)"; return _ourFile; }
            var f = v63 ? "titles_6_3.xml" : "titles_6_2.xml";
            var p = BasePath.Name + "Modules/BKROTPatch/ModuleData/" + f;
            kind = "BKROT " + f;
            return File.Exists(p) ? p : null;
        }

        private static string Kind(string path)
        {
            if (string.IsNullOrEmpty(path)) return "brak";
            if (path.EndsWith(FileName, StringComparison.OrdinalIgnoreCase)) return "ROT 8.1.8 (CrashScribe)";
            if (path.IndexOf("BKROTPatch", StringComparison.OrdinalIgnoreCase) >= 0) return "BKROT " + Path.GetFileName(path);
            if (path.IndexOf("BannerKings", StringComparison.OrdinalIgnoreCase) >= 0) return "KALRADYJSKI BK " + Path.GetFileName(path);
            return "inny " + Path.GetFileName(path);
        }

        public static void InitPrefix() { Apply("InitializeTitles (nowa gra)"); }

        public static void RetroPrefix() { Apply("RetrofitOrphanedTitles (wczytanie)"); }

        public static void RetroPostfix(int __result)
        {
            try
            {
                _retroN = __result;
                var cfg = _pInst.GetValue(null, null);
                _retroPath = cfg != null ? _pPath.GetValue(cfg, null) as string : null;
                Scribe.Line("Mends: tytuly BK (AM-26) - retrofit BK przy wczytaniu: przepietych powiazan " + __result + " (plik " + Kind(_retroPath)
                            + "); kampania zaczeta z tym samym plikiem daje 0, stary zapis z ksiestwami Calradii - wiecej.");
            }
            catch { }
        }

        private static void Apply(string where)
        {
            try
            {
                if (!Config.BkTitlesUseRotFile || _pInst == null || _pPath == null) return;
                var cfg = _pInst.GetValue(null, null);
                if (cfg == null) return;
                var old = _pPath.GetValue(cfg, null) as string;
                var want = WantedPath(out string kind);
                if (want == null)
                {
                    Scribe.Line("Mends: tytuly BK (AM-26) - " + where + ": brak pliku ROT (" + kind + ") - sciezka bez zmian: " + Kind(old) + " " + old + ".");
                    return;
                }
                bool change = !string.Equals(old, want, StringComparison.Ordinal);
                if (change) _pPath.SetValue(cfg, want, null);
                Scribe.Line("Mends: tytuly BK (AM-26) - " + where + ": plik " + kind + " " + want
                            + (change ? " (bylo: " + Kind(old) + ")" : " (bez zmian)") + (_rewrites > _rewritesAtStart ? "; plik inny niz zasob - zapisany od nowa " + (_rewrites - _rewritesAtStart) + " razy po starcie procesu" : "") + ".");
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "BkTitles.Apply", null); } catch { } }
        }

        /// <summary>Nowa sesja (konstruktor zachowania w OnGameStart): linia kontrolna jeszcze raz, wynik retrofitu od nowa.</summary>
        internal static void ResetSession() { _checked = false; _retroN = -1; _retroPath = null; }

        /// <summary>Nazwy ksiestw z pliku (tak jak BK RetrofitDuchy: new TextObject(name).ToString()); null = brak pliku.</summary>
        private static HashSet<string> DuchyNames(string path)
        {
            if (path == _namesPath && _names != null) return _names;
            _namesPath = path;
            _names = null;
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
                var doc = new XmlDocument();
                doc.LoadXml(File.ReadAllText(path));
                var set = new HashSet<string>(StringComparer.Ordinal);
                foreach (XmlNode n in doc.GetElementsByTagName("duchy"))
                {
                    var a = n.Attributes != null ? n.Attributes["name"] : null;
                    if (a != null) set.Add(new TextObject(a.Value).ToString());
                }
                _names = set;
            }
            catch { _names = null; }
            return _names;
        }

        private static string List(List<string> xs)
        {
            if (xs.Count == 0) return "";
            var take = xs.Count > 30 ? xs.GetRange(0, 30) : xs;
            return " [" + string.Join("; ", take.ToArray()) + (xs.Count > 30 ? "; ... +" + (xs.Count - 30) : "") + "]";
        }

        /// <summary>Pierwsza godzina sesji (po InitializeTitles nowej gry i po retroficie wczytania, niezaleznie od kolejnosci sluchaczy
        /// OnSessionLaunched): twierdze, wsie przy twierdzach z tytulem i krolestwa bez tytulu BK, ksiestwa spoza pliku. Tylko odczyt. Nie rzuca.</summary>
        internal static void SessionCheck()
        {
            if (_checked) return;
            _checked = true;
            try
            {
                if (!Config.BkTitlesUseRotFile || _pInst == null || _pTm == null || _mGetTitle == null || _mGetSov == null) return;
                var cfg = _pInst.GetValue(null, null);
                var tm = cfg != null ? _pTm.GetValue(cfg, null) : null;
                if (tm == null) { Scribe.Line("Mends: tytuly BK (AM-26/AM-27) - pierwsza godzina sesji: BK bez TitleManager - nic do sprawdzenia."); return; }
                var path = _pPath.GetValue(cfg, null) as string;
                int forts = 0, vil = 0, vilNo = 0, ks = 0, stumbles = 0;
                var noT = new List<string>();
                var noK = new List<string>();
                var vilMiss = new List<Settlement>();   // AUD-P1-5: wsie bez tytulu przy twierdzach z tytulem (nazwy i dorobka po linii sesji)
                var titled = new HashSet<Settlement>();
                foreach (var s in Settlement.All)
                {
                    if (s == null || !(s.IsTown || s.IsCastle)) continue;
                    forts++;
                    object t = null;
                    try { t = _mGetTitle.Invoke(tm, new object[] { s }); } catch { stumbles++; }
                    if (t == null) noT.Add(s.Name + " (" + s.StringId + ")");
                    else titled.Add(s);
                }
                foreach (var s in Settlement.All)
                {
                    if (s == null || !s.IsVillage || s.Village == null || s.Village.Bound == null || !titled.Contains(s.Village.Bound)) continue;
                    vil++;
                    object t = null;
                    try { t = _mGetTitle.Invoke(tm, new object[] { s }); } catch { stumbles++; }
                    if (t == null) { vilNo++; vilMiss.Add(s); }
                }
                foreach (var k in Kingdom.All)
                {
                    if (k == null || k.IsEliminated) continue;
                    ks++;
                    object t = null;
                    try { t = _mGetSov.Invoke(tm, new object[] { k }); } catch { stumbles++; }
                    if (t == null) noK.Add(k.Name + " (" + k.StringId + ")");
                }
                string duchies = "ksiestw ? (brak AllTitles / pliku)";
                var names = DuchyNames(path);
                if (names != null && _pAll != null && _pType != null && _pShort != null)
                {
                    int nd = 0;
                    var foreign = new List<string>();
                    var all = _pAll.GetValue(tm, null) as System.Collections.IEnumerable;
                    if (all != null)
                        foreach (var t in all)
                        {
                            if (t == null) continue;
                            try
                            {
                                if (Convert.ToString(_pType.GetValue(t, null)) != "Dukedom") continue;
                                nd++;
                                var n = Convert.ToString(_pShort.GetValue(t, null));
                                if (!names.Contains(n)) foreign.Add(n);
                            }
                            catch { stumbles++; }
                        }
                    duchies = "ksiestw " + nd + ", spoza pliku " + foreign.Count + List(foreign);
                }
                Scribe.Line("Mends: tytuly BK (AM-26/AM-27) - pierwsza godzina sesji: plik " + Kind(path) + " " + path
                            + " | twierdz bez tytulu " + noT.Count + " z " + forts + List(noT)
                            + " | wsi bez tytulu przy twierdzach z tytulem " + vilNo + " z " + vil
                            + " | krolestw bez tytulu krolewskiego " + noK.Count + " z " + ks + List(noK)
                            + " | " + duchies
                            + " | retrofit przy wczytaniu: " + (_retroN < 0 ? "brak (nowa gra)" : _retroN + " (plik " + Kind(_retroPath) + ")")
                            + " | potkniec " + stumbles + ".");
                FixVillages(tm, vilMiss);   // AUD-P1-5: liczby linii wyzej to stan PRZED dorobka (regex AU_SESS narzedzia bez zmian)
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "BkTitles.SessionCheck", null); } catch { } }
        }

        private static string Nm(Settlement s) { try { return s == null ? "-" : s.Name + " (" + s.StringId + ")"; } catch { return "?"; } }
        private static string Nm(Hero h) { try { return h == null || h.Name == null ? "-" : h.Name.ToString(); } catch { return "?"; } }

        /// <summary>AUD-P1-5 (klucz BkTitlesVillageFix; pierwsza godzina sesji, po linii sesji): wies przy twierdzy z tytulem BK bez wlasnego tytulu dostaje go
        /// tak, jak BK daje go kazdej wsi przy starcie gry (TitleGenerator.CreateLandedTitle :123-129, GenerateKingdom :311-314): lordship z CreateLordship
        /// (de iure = zywy posiadacz de iure tytulu twierdzy, a gdy go nie ma - wlasciciel twierdzy, jak CreateLandedTitle :115-118; de facto = wlasciciel
        /// twierdzy; kontrakt twierdzy), dodany przez ExecuteAddTitle (sam odswieza pamiec BK), dopisany do wasali twierdzy i z suwerenem twierdzy. BK nie
        /// dorabia tytulow wsi w biegnacej kampanii (CreateLordship wolany tylko w CreateLandedTitle; RetrofitOrphanedTitles rusza ksiestwa, hrabstwa i baronie).
        /// ExecuteAddTitle po cichu pomija tytul "rowny" istniejacemu (FeudalTitle.Equals) - dlatego kontrola GetTitle po dodaniu ("nie dodane", bez wasala).
        /// Idempotentna: wies z tytulem pominieta. Klucz false (albo brak czesci BK) = tylko nazwy. Linia zawsze, takze gdy przed 0. Wyjatek na jednej wsi -
        /// potkniecie, reszta dalej. Nowe tytuly trafiaja do zapisu BK na stale (wylaczenie klucza ich nie usuwa).</summary>
        private static void FixVillages(object tm, List<Settlement> miss)
        {
            int made = 0, notAdded = 0, noFort = 0, stumbles = 0, left = 0;
            var items = new List<string>();
            bool key = Config.BkTitlesVillageFix;
            bool can = key && _fixMissing == null && _mCreateLordship != null && _mAddTitle != null;
            try
            {
                foreach (var s in miss)
                {
                    var bound = s != null && s.Village != null ? s.Village.Bound : null;
                    string nm = Nm(s) + " przy " + Nm(bound);
                    try
                    {
                        object bt = bound != null ? _mGetTitle.Invoke(tm, new object[] { bound }) : null;
                        if (bt == null) { noFort++; items.Add(nm + ": bez tytulu twierdzy"); continue; }
                        if (!can) { items.Add(nm + ": tylko nazwa"); continue; }
                        if (_mGetTitle.Invoke(tm, new object[] { s }) != null) { items.Add(nm + ": ma juz tytul"); continue; }   // idempotentnie
                        if (bound.Owner == null) { items.Add(nm + ": twierdza bez wlasciciela - pominieta"); continue; }
                        var dj = _pDeJure.GetValue(bt, null) as Hero;
                        if (dj == null || !dj.IsAlive) dj = bound.Owner;
                        var lt = _mCreateLordship.Invoke(null, new object[] { s, dj, _pContract.GetValue(bt, null) });
                        if (lt == null) { notAdded++; items.Add(nm + ": NIE dodane (CreateLordship zwrocil null)"); continue; }
                        _mAddTitle.Invoke(tm, new object[] { lt });
                        var got = _mGetTitle.Invoke(tm, new object[] { s });
                        if (!ReferenceEquals(got, lt))
                        {
                            notAdded++;
                            items.Add(nm + ": NIE dodane (BK " + (got == null ? "uznal tytul za rowny istniejacemu" : "ma inny tytul tej wsi") + ")");
                            continue;
                        }
                        var vl = _pVassals.GetValue(bt, null) as System.Collections.IList;
                        if (vl != null && !vl.Contains(lt)) vl.Add(lt);
                        var sov = _pSov.GetValue(bt, null);
                        if (sov != null) _mSetSov.Invoke(lt, new object[] { sov });
                        made++;
                        items.Add(nm + ": dorobione, de iure " + Nm(dj) + (vl == null ? ", twierdza bez listy wasali" : "") + (sov == null ? ", bez suwerena (jak twierdza)" : ""));
                    }
                    catch (Exception e)
                    {
                        stumbles++;
                        var ie = e is TargetInvocationException && e.InnerException != null ? e.InnerException : e;
                        items.Add(nm + ": potkniecie " + ie.GetType().Name);
                    }
                }
                foreach (var s in miss)
                {
                    object t = null;
                    try { t = _mGetTitle.Invoke(tm, new object[] { s }); } catch { stumbles++; }
                    if (t == null) left++;
                }
            }
            catch (Exception e) { stumbles++; try { Scribe.Report("CrashScribe", e, "BkTitles.FixVillages", null); } catch { } }
            string note = !key ? "; dorobka wylaczona - tylko nazwy"
                        : _fixMissing != null ? "; OSTRZEZENIE: brak " + _fixMissing + " (inna wersja BK) - dorobka pominieta, tylko nazwy" : "";
            Scribe.Line("Mends: tytuly BK (AUD-P1-5) - wsie bez tytulu przy twierdzach z tytulem: przed " + miss.Count + ", dorobione " + made + ", nie dodane " + notAdded
                        + ", bez tytulu twierdzy " + noFort + ", zostalo " + left + " (BkTitlesVillageFix=" + (key ? "true" : "false") + ")" + note + List(items)
                        + " | potkniec " + stumbles + ".");
        }
    }

    /// <summary>P1: linia kontrolna tytulow BK raz na sesje (pierwsza godzina gry).</summary>
    internal sealed class BkTitlesBehavior : CampaignBehaviorBase
    {
        public BkTitlesBehavior() { BkTitles.ResetSession(); }

        public override void RegisterEvents()
        {
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, delegate { BkTitles.SessionCheck(); });
        }

        public override void SyncData(IDataStore dataStore) { }
    }
}
