using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// f8-bandy (noc 18.10, decyzja Jeffa "7. b ale o ile spowolni to gre gdy damy 2200 band?"; STAN-PRAC ### 43 i ### 86, rozpoznanie roz/wyniki/rozp_bandyci.json).
    /// LIMIT BAND GRY WYZEJ, BANDY DALEJ TYLKO Z PRAWDZIWYCH LUDZI, Z POMIAREM KOSZTU.
    ///
    /// Gra (sprawdzone w dekompilacji 1.4.8): BanditSpawnCampaignBehavior.HourlyTickClan (tylko w nocy) -> SpawnBanditsAroundHideout(clan, 0.1):
    /// nowych band = round((GetInfestedHideoutCount(clan) x (NumberOfMaximumBanditPartiesAroundEachHideout + NumberOfMaximumBanditPartiesInEachHideout)
    /// - clan.WarPartyComponents.Count) x 0.1) - kazda przez SpawnBanditParty -> SelectBanditHideout (zajete kryjowki kultury klanu, waga 1/bandy^2).
    /// Czynny model to ROT.Models.ROTBanditDensityModel (ROT SubModule: AddModel na NavalDLC -> Default): wokol 6, w srodku 6, kryjowek na klan 20, prog
    /// zajecia 4 - stad "zajete kryjowki x 12". Getter "wokol" (NumberOfMaximumBanditPartiesAroundEachHideout) czyta w calej grze TYLKO ten cel narodzin
    /// (BanditSpawnCampaignBehavior._numberOfMaxBanditPartiesAroundEachHideout); "w srodku" czytaja tez AI odwiedzin kryjowek (gra, BK) i misja kryjowki -
    /// tego nie ruszamy. BK lata DefaultBanditDensityModel (+2 wokol) - pod ROT bez skutku, jak dotad.
    ///
    /// 1. BanditLimitPerHideout (domyslnie 24, MCM 12-48): postfiks gettera "wokol" czynnego modelu (DeclaredPropertyGetter na klasie deklarujacej - pulapka
    ///    CLAUDE.md 15.10), wynik = max(ROT, limit - w srodku). Dziala tylko przy czynnym prawie wyrzutkow (OutlawLaw - nowe bandy tylko z puli ludzi);
    ///    limit <= ROT (12) albo prawo wylaczone = wynik ROT bez zmian (stara droga co do liczby).
    /// 2. BanditBirthsWherePeople (tylko przy celu podniesionym): kazda proba narodzin bandy klanu z kryjowkami (ponizej i ponad stary cel ROT - obie strefy
    ///    liczone osobno w linii) rodzi sie tylko przy kryjowce klanu, ktorej region (z sasiadami - jak OutlawLaw.Draw) ma w puli wyrzutkow co najmniej
    ///    OutlawMinBand ludzi; wybor wazony jak w grze (GetSpawnChanceInSettlement, kandydaci jak SelectBanditHideout); gdy takiej kryjowki nie ma - proba
    ///    odmowiona. Bez tego: dzis ok. 70% prob narodzin konczy sie pusta banda tworzona i kasowana godzine pozniej (OutlawLaw._empty), a przy celu x2
    ///    prob jest wiecej - takze ponizej starego celu, gdy klan ma mniej band niz zajete kryjowki x 12 (pula za mala): dla klanu z 10 kryjowkami i 70 bandami
    ///    5 prob na godzine nocy przy 12 i 17 przy 24, wszystkie losowo, wiec ok. 3.4 x wiecej pustych. Bramka GateClan juz przepuszcza tylko klan, ktory ma
    ///    gdzies ludzi - wybor kryjowki z ludzmi konczy jej zamysl ("nie ma ludzi w okolicy = nie ma bandy"). Cel ROT (limit 12) - wybor gry jak dotad.
    ///    Prefiks SpawnBanditParty (Priority.Low - po bramce OutlawLaw.GateClan), prefiks SelectBanditHideout podaje wybrana kryjowke. Harmony 2.4.2 (test
    ///    18.10 w scratchpad f8/htest): prefiks zwracajacy false pomija dalsze prefiksy i oryginal, postfiks biegnie zawsze i widzi __runOriginal - nasz prefiks
    ///    widzi wiec tylko proby przepuszczone przez GateClan (odmowy bramki liczy linia "Wyrzutki", pole "odmowione"); latka gettera dziala przez wywolanie
    ///    wirtualne takze w metodzie skompilowanej przed latka (ten sam test).
    /// 3. BanditCostLog: linia doby "Bandy i limit (f8-bandy): dzien N" - bandy wobec celu (klan po klanie), narodziny (strefa, puste), koszt doby w ms:
    ///    rodzenie band (SpawnBanditsAroundHideout - z tworzeniem partii i sluchaczami MobilePartyCreated), mysl AI godzinowa (AiPartyThinkBehavior.PartyHourlyAiTick),
    ///    AI taktyczne (MobilePartyAi.TickInternal - szukanie celow wokol partii, koszt rosnie z gestoscia partii), tiki partii (CampaignEventDispatcher.HourlyTickParty
    ///    i DailyTickParty - wszyscy sluchacze gry i modow), bandy wobec innych partii; OutlawLaw.Daily (nasza praca doby na bandach, z profilu doby); doba realna
    ///    (zegar sciany miedzy liniami) i klatki (Campaign.CurrentTickCount). Stopery w probkach 1/4-1/8 (czas x wywolan/probek), liczba wywolan pelna.
    ///    Ruch po mapie: stoper calego Campaign.RealTick (raz na klatke; ruch partii w rownoleglych zadaniach gry, czekanie WaitAsyncTasks, oblezenia) - bez podzialu
    ///    na bandy, w linii udzial band liniowo wedlug liczby partii (szacunek). Obraz i interfejs - tylko w dobie realnej (porownanie biegu 12 i 24 tych samych dob).
    ///    Profil klatki baza61 (16.10, 8 dob, ok. 5200 partii, 181 klatek na dobe): RealTick 36.4 ms/klatke (36%), sluchacze AiHourlyTick razem 16.6 ms (AiMilitary 11.2 -
    ///    tylko lordowie), PartyHourlyAiTick 0.5 ms - stad szacunek przed testem: +1100 band = ok. +21% partii, ruch +1.0-2.0 s (stoja zalogi i milicje - ruchomych partii przybywa ok. 30%) i mysl AI +0.3 s na dobe gry (ok. +8-14% przy 15-16 s).
    /// Pieniadze i towar: bez zmian (latka zmienia tylko liczbe celu band); ludzie nowych band tylko z puli wyrzutkow (OutlawLaw.RosterPostfix / Draw) - nic z niczego.
    /// Latki zakladane W KAMPANII (SessionStart z OnSessionLaunched), raz na proces; wylaczony klucz = prefiks wraca od razu. Bez zapisu w grze (liczniki sesji).
    /// Wolane z watku gry (tikery gry z doParallel false, PartiesThink po kolei) - liczniki bez blokad (to log). Potkniecia liczone, nic nie gasnie globalnie.
    /// </summary>
    internal static class BanditLimit
    {
        private const string P = "Bandy i limit (f8-bandy): ";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static bool PeopleOn { get { var s = Settings.Current; return s != null && s.BanditBirthsWherePeople; } }

        // ------------------------------------------------------------ latki (raz na proces)
        private static Harmony _h;
        private static MethodInfo _aroundGetter;
        private static bool _aroundTried, _pickTried, _costTried;
        private static string _aroundWhy = "niezakladana", _pickWhy = "niezakladana", _modelName = "-";
        private static int _baseAround = -1, _inEach = -1;
        private static bool _spawnHooked, _selectHooked;
        private static readonly List<string> _costDone = new List<string>(), _costMiss = new List<string>();
        private static Func<BanditSpawnCampaignBehavior, Clan, int> _infestedOf;
        private static Func<BanditSpawnCampaignBehavior, Settlement, float> _chanceOf;
        private static AccessTools.FieldRef<MobilePartyAi, MobileParty> _aiParty;

        // ------------------------------------------------------------ stan kampanii i okresu (bez zapisu)
        private static object _campaign;
        private static bool _costLive;
        private static long _lastWall;
        private static int _lastFrames = -1;
        private static int _stumbles, _dStumbles;
        private static bool _errLogged;

        // narodziny (SpawnBanditParty)
        private static int _dTries, _dPreRefused, _dBelow, _dExtra, _dExtraPicked, _dExtraRefused, _dSpawned, _dEmptyBelow, _dEmptyExtra, _dNotFound;
        private static int _dBelowPicked, _dBelowRefused;
        private static long _dMenExtra, _dMenBelow;
        // stan jednej proby (SpawnPre -> PickPre -> SpawnPost); watek gry, bez zagniezdzen (SpawnBanditParty nie wola sie rekurencyjnie)
        private static Hideout _forced;
        private static Clan _tryClan;
        private static int _tryZone = -1, _tryCountBefore;

        // pamiec godziny: zajete kryjowki klanu i kandydaci (zajete kryjowki kultury klanu - jak SelectBanditHideout)
        private static double _cacheHour = -1;
        private static readonly Dictionary<Clan, int> _infestedHour = new Dictionary<Clan, int>();
        private static readonly Dictionary<Clan, List<Hideout>> _candHour = new Dictionary<Clan, List<Hideout>>();
        private static readonly HashSet<Clan> _noPeopleHour = new HashSet<Clan>();   // klan bez kryjowki z ludzmi w tej godzinie - dalsze proby bez szukania (jak GateClan)
        private static readonly List<KeyValuePair<Hideout, float>> _pickBuf = new List<KeyValuePair<Hideout, float>>();

        // stopery: rodzenie band (pelny), mierniki partii [miernik x 2]: 0 bandy, 1 inne
        private static long _dSpawnCalls, _dSpawnTicks;
        private static long _dRealCalls, _dRealTicks;   // Campaign.RealTick (raz na klatke): ruch partii (rownolegle zadania - czekanie WaitAsyncTasks), oblezenia, komponenty
        private const int MThink = 0, MTac = 1, MHour = 2, MDay = 3, MN = 4;
        private static readonly int[] Mask = { 3, 7, 7, 0 };   // probka: mysl 1/4, taktyczne 1/8, tik godziny 1/8, tik doby - pelny
        private static readonly long[] _mCalls = new long[MN * 2], _mSampled = new long[MN * 2], _mTicks = new long[MN * 2];

        private static double Ms(long ticks) { return ticks * 1000.0 / Stopwatch.Frequency; }
        private static string F1(double v) { return v.ToString("0.0", Inv); }
        private static string F2(double v) { return v.ToString("0.00", Inv); }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _dStumbles++;
            if (!_errLogged) { _errLogged = true; Log.Error("BanditLimit." + where, e); }
        }

        private static void ClearDay()
        {
            _dTries = _dPreRefused = _dBelow = _dExtra = _dExtraPicked = _dExtraRefused = _dSpawned = _dEmptyBelow = _dEmptyExtra = _dNotFound = 0;
            _dMenExtra = _dMenBelow = 0;
            _dBelowPicked = _dBelowRefused = 0;
            _dSpawnCalls = _dSpawnTicks = 0;
            _dRealCalls = _dRealTicks = 0;
            Array.Clear(_mCalls, 0, _mCalls.Length); Array.Clear(_mSampled, 0, _mSampled.Length); Array.Clear(_mTicks, 0, _mTicks.Length);
            _dStumbles = 0;
        }

        /// <summary>Nowa kampania albo wczytanie w tym samym procesie: liczniki, pamiec godziny i zegar od zera (latki zostaja w procesie).</summary>
        private static void EnsureCampaign()
        {
            var c = Campaign.Current;
            if (ReferenceEquals(c, _campaign)) return;
            _campaign = c;
            ClearDay();
            _stumbles = 0; _errLogged = false;
            _lastWall = 0; _lastFrames = -1;
            _cacheHour = -1; _infestedHour.Clear(); _candHour.Clear(); _noPeopleHour.Clear();
            _forced = null; _tryClan = null; _tryZone = -1;
        }

        // ------------------------------------------------------------ limit (getter "wokol")
        /// <summary>Limit band na zajeta kryjowke ponad ROT (wokol + w srodku) albo 0 = cel gry bez zmian (limit <= ROT, prawo wyrzutkow wylaczone, brak latki).</summary>
        internal static int RaisedTarget()
        {
            var s = Settings.Current;
            if (s == null || _baseAround < 0 || _inEach < 0 || !OutlawLaw.On) return 0;
            int t = Math.Min(48, s.BanditLimitPerHideout);
            return t > _baseAround + _inEach ? t : 0;
        }

        private static int BaseSum { get { return _baseAround >= 0 && _inEach >= 0 ? _baseAround + _inEach : 0; } }

        public static void AroundPostfix(ref int __result)
        {
            try
            {
                int t = RaisedTarget();
                if (t <= 0) return;
                int around = t - _inEach;
                if (around > __result) __result = around;
            }
            catch (Exception e) { Stumble("AroundPostfix", e); }
        }

        private static MethodInfo FindGetter(Type t, string prop)
        {
            for (var c = t; c != null && c != typeof(object); c = c.BaseType)
            {
                var m = AccessTools.DeclaredPropertyGetter(c, prop);
                if (m != null && !m.IsAbstract) return m;
            }
            return null;
        }

        private static void HookAround()
        {
            var model = Campaign.Current != null && Campaign.Current.Models != null ? Campaign.Current.Models.BanditDensityModel : null;
            if (model == null) { if (_aroundGetter == null) _aroundWhy = "brak modelu gestosci band w kampanii"; return; }
            _modelName = model.GetType().FullName;
            var g = FindGetter(model.GetType(), "NumberOfMaximumBanditPartiesAroundEachHideout");
            if (g == null) { _aroundWhy = "nie znaleziono gettera NumberOfMaximumBanditPartiesAroundEachHideout w " + _modelName; return; }
            if (g == _aroundGetter) return;                                  // ten sam getter co w poprzedniej kampanii procesu - latka jest
            if (_aroundTried && _aroundGetter != null) Log.Info(P + "inny model gestosci band w tej kampanii (" + _modelName + ") - latka takze na jego getterze.");
            _aroundTried = true;
            try
            {
                // wartosci ROT czytane PRZED latka (po latce getter oddaje juz podniesiony cel)
                _baseAround = model.NumberOfMaximumBanditPartiesAroundEachHideout;
                _inEach = model.NumberOfMaximumBanditPartiesInEachHideout;
                _h.Patch(g, postfix: new HarmonyMethod(typeof(BanditLimit), nameof(AroundPostfix)) { priority = Priority.Last });
                _aroundGetter = g;
                _aroundWhy = "zalozona na " + g.DeclaringType.FullName + ".get_NumberOfMaximumBanditPartiesAroundEachHideout";
            }
            catch (Exception e) { _aroundWhy = "blad: " + e.Message; Stumble("HookAround", e); }
        }

        // ------------------------------------------------------------ narodziny ponad stary cel tylko przy kryjowce z ludzmi
        private static bool IsHideoutClan(Clan c)
        {
            // jak BanditSpawnCampaignBehavior.IsBanditFaction (prywatna statyczna): klan band z kryjowkami, bez piratow morskich
            return c != null && c.IsBanditFaction && !c.HasNavalNavigationCapability && c.Culture != null && c.Culture.CanHaveSettlement;
        }

        private static void HourCache()
        {
            double h = Math.Floor(CampaignTime.Now.ToHours);
            if (h == _cacheHour) return;
            _cacheHour = h; _infestedHour.Clear(); _candHour.Clear(); _noPeopleHour.Clear();
        }

        private static int InfestedOf(BanditSpawnCampaignBehavior b, Clan clan)
        {
            HourCache();
            int n;
            if (_infestedHour.TryGetValue(clan, out n)) return n;
            n = 0;
            if (_infestedOf != null && b != null) n = _infestedOf(b, clan);
            else foreach (var h in Hideout.All) if (h != null && h.Settlement != null && h.Settlement.Culture == clan.Culture && h.IsInfested && h.MapFaction == clan) n++;
            _infestedHour[clan] = n;
            return n;
        }

        private static List<Hideout> CandidatesOf(Clan clan)
        {
            HourCache();
            List<Hideout> l;
            if (_candHour.TryGetValue(clan, out l)) return l;
            l = new List<Hideout>();
            foreach (var h in Hideout.All)
                if (h != null && h.Settlement != null && h.Settlement.Culture == clan.Culture && h.IsInfested) l.Add(h);
            _candHour[clan] = l;
            return l;
        }

        /// <summary>Kryjowka klanu z ludzmi w puli regionu (z sasiadami) - waga gry; null = zadna zajeta kryjowka kultury klanu nie ma ludzi.</summary>
        private static Hideout PickWithPeople(BanditSpawnCampaignBehavior b, Clan clan)
        {
            int min = Math.Max(1, Settings.Current.OutlawMinBand);
            _pickBuf.Clear();
            float sum = 0f;
            foreach (var h in CandidatesOf(clan))
            {
                if (h == null || h.Settlement == null || !h.IsInfested) continue;
                if (OutlawLaw.AvailFor(h.Settlement) < min) continue;
                float w = 1f;
                if (_chanceOf != null && b != null) { try { w = _chanceOf(b, h.Settlement); } catch { w = 1f; } }
                if (!(w > 0f) || float.IsInfinity(w)) w = 1e-6f;
                _pickBuf.Add(new KeyValuePair<Hideout, float>(h, w));
                sum += w;
            }
            if (_pickBuf.Count == 0) return null;
            float r = MBRandom.RandomFloat * sum;
            for (int i = 0; i < _pickBuf.Count; i++)
            {
                r -= _pickBuf[i].Value;
                if (r <= 0f) return _pickBuf[i].Key;
            }
            return _pickBuf[_pickBuf.Count - 1].Key;
        }

        /// <summary>Prefiks SpawnBanditParty (Priority.Low - po bramce OutlawLaw.GateClan). Cel podniesiony: kryjowka z ludzmi albo odmowa (obie strefy).</summary>
        public static bool SpawnPre(BanditSpawnCampaignBehavior __instance, Clan __0, bool __runOriginal)
        {
            _forced = null; _tryClan = null; _tryZone = -1;
            try
            {
                if (_campaign == null || !ReferenceEquals(_campaign, Campaign.Current)) EnsureCampaign();
                _dTries++;
                if (!__runOriginal) { _dPreRefused++; return false; }   // wczesniejszy prefiks (bramka OutlawLaw) juz odmowil
                var clan = __0;
                if (clan == null) return true;
                _tryClan = clan;
                _tryCountBefore = clan.WarPartyComponents != null ? clan.WarPartyComponents.Count : 0;
                int t = RaisedTarget();
                if (t <= 0) { _tryZone = 0; _dBelow++; return true; } // cel gry bez zmian - jak dotad (proba gry zawsze ponizej celu ROT)
                int infested = InfestedOf(__instance, clan);
                int zone = _tryCountBefore < infested * BaseSum ? 0 : 1;   // 0 - ponizej starego celu ROT, 1 - ponad (tylko pomiar strefy)
                _tryZone = zone;
                if (zone == 0) _dBelow++; else _dExtra++;
                if (!PeopleOn) return true;                            // klucz wylaczony - wybor gry (puste narodziny mozliwe)
                var pick = _noPeopleHour.Contains(clan) ? null : PickWithPeople(__instance, clan);   // HourCache wolany w InfestedOf
                if (pick == null) { _noPeopleHour.Add(clan); if (zone == 0) _dBelowRefused++; else _dExtraRefused++; _tryZone = -1; return false; }
                _forced = pick;
                if (zone == 0) _dBelowPicked++; else _dExtraPicked++;
                return true;
            }
            catch (Exception e) { Stumble("SpawnPre", e); _forced = null; return true; }
        }

        /// <summary>Prefiks SelectBanditHideout: kryjowka wybrana w SpawnPre (raz) zamiast losowania gry; bez wyboru - gra jak dotad.</summary>
        public static bool PickPre(ref Hideout __result)
        {
            var f = _forced;
            if (f == null) return true;
            _forced = null;
            __result = f;
            return false;
        }

        /// <summary>Postfiks SpawnBanditParty: banda powstala? pusta? (ostatni komponent wojenny klanu - gra dopisuje go na koniec listy).</summary>
        public static void SpawnPost(bool __runOriginal)
        {
            try
            {
                _forced = null;
                var clan = _tryClan;
                if (!__runOriginal || clan == null) return;
                var list = clan.WarPartyComponents;
                int n = list != null ? list.Count : 0;
                if (n <= _tryCountBefore) { _dNotFound++; return; }
                _dSpawned++;
                var mp = list[n - 1] != null ? list[n - 1].MobileParty : null;
                int men = mp != null && mp.MemberRoster != null ? mp.MemberRoster.TotalManCount : -1;
                if (men < 0) { _dNotFound++; return; }
                if (_tryZone == 1) { if (men == 0) _dEmptyExtra++; else _dMenExtra += men; }
                else { if (men == 0) _dEmptyBelow++; else _dMenBelow += men; }
            }
            catch (Exception e) { Stumble("SpawnPost", e); }
            finally { _tryClan = null; _tryZone = -1; }
        }

        public static Exception SpawnFin(Exception __exception) { _forced = null; return __exception; }

        private static void HookPick()
        {
            if (_pickTried) return;
            _pickTried = true;
            var t = typeof(BanditSpawnCampaignBehavior);
            var miss = new List<string>();
            try
            {
                var gi = AccessTools.DeclaredMethod(t, "GetInfestedHideoutCount", new[] { typeof(Clan) });
                if (gi != null) _infestedOf = AccessTools.MethodDelegate<Func<BanditSpawnCampaignBehavior, Clan, int>>(gi);
                else miss.Add("GetInfestedHideoutCount (liczone po Hideout.All jak gra)");
            }
            catch (Exception e) { miss.Add("GetInfestedHideoutCount(" + e.Message + ")"); }
            try
            {
                var gc = AccessTools.DeclaredMethod(t, "GetSpawnChanceInSettlement", new[] { typeof(Settlement) });
                if (gc != null) _chanceOf = AccessTools.MethodDelegate<Func<BanditSpawnCampaignBehavior, Settlement, float>>(gc);
                else miss.Add("GetSpawnChanceInSettlement (waga 1)");
            }
            catch (Exception e) { miss.Add("GetSpawnChanceInSettlement(" + e.Message + ")"); }
            try
            {
                var sel = AccessTools.DeclaredMethod(t, "SelectBanditHideout", new[] { typeof(Clan) });
                var spn = AccessTools.DeclaredMethod(t, "SpawnBanditParty", new[] { typeof(Clan) });
                if (sel == null || sel.ReturnType != typeof(Hideout)) miss.Add("SelectBanditHideout");
                if (spn == null) miss.Add("SpawnBanditParty");
                if (sel != null && spn != null && sel.ReturnType == typeof(Hideout))
                {
                    // najpierw wybor (prefiks bez skutku, dopoki SpawnPre nie poda kryjowki), potem proba
                    _h.Patch(sel, prefix: new HarmonyMethod(typeof(BanditLimit), nameof(PickPre)) { priority = Priority.First });
                    _selectHooked = true;
                    _h.Patch(spn, prefix: new HarmonyMethod(typeof(BanditLimit), nameof(SpawnPre)) { priority = Priority.Low },
                                  postfix: new HarmonyMethod(typeof(BanditLimit), nameof(SpawnPost)),
                                  finalizer: new HarmonyMethod(typeof(BanditLimit), nameof(SpawnFin)));
                    _spawnHooked = true;
                }
            }
            catch (Exception e) { miss.Add("SpawnBanditParty/SelectBanditHideout(" + e.Message + ")"); Stumble("HookPick", e); }
            _pickWhy = (_spawnHooked && _selectHooked ? "zalozone (SpawnBanditParty, SelectBanditHideout)" : "BRAK latek wyboru")
                       + (miss.Count > 0 ? "; brak: " + string.Join(", ", miss.ToArray()) : "");
        }

        // ------------------------------------------------------------ stopery (BanditCostLog)
        public static void SpawnAllPre(out long __state) { __state = _costLive ? Stopwatch.GetTimestamp() : 0L; }

        public static void SpawnAllPost(long __state)
        {
            if (__state == 0L) return;
            _dSpawnTicks += Stopwatch.GetTimestamp() - __state; _dSpawnCalls++;
        }

        public static void RealPre(out long __state) { __state = _costLive ? Stopwatch.GetTimestamp() : 0L; }

        public static void RealPost(long __state)
        {
            if (__state == 0L) return;
            _dRealTicks += Stopwatch.GetTimestamp() - __state; _dRealCalls++;
        }

        private static long MBegin(int m, MobileParty p)
        {
            if (!_costLive || p == null) return 0L;
            int k = m * 2 + (p.IsBandit ? 0 : 1);
            return (++_mCalls[k] & Mask[m]) == 0 ? Stopwatch.GetTimestamp() : 0L;
        }

        private static void MEnd(int m, MobileParty p, long t)
        {
            if (t == 0L || p == null) return;
            int k = m * 2 + (p.IsBandit ? 0 : 1);
            _mTicks[k] += Stopwatch.GetTimestamp() - t; _mSampled[k]++;
        }

        public static void ThinkPre(MobileParty __0, out long __state) { __state = MBegin(MThink, __0); }
        public static void ThinkPost(MobileParty __0, long __state) { MEnd(MThink, __0, __state); }
        public static void HourPre(MobileParty __0, out long __state) { __state = MBegin(MHour, __0); }
        public static void HourPost(MobileParty __0, long __state) { MEnd(MHour, __0, __state); }
        public static void DayPre(MobileParty __0, out long __state) { __state = MBegin(MDay, __0); }
        public static void DayPost(MobileParty __0, long __state) { MEnd(MDay, __0, __state); }

        public static void TacPre(MobilePartyAi __instance, out long __state)
        {
            __state = 0L;
            if (!_costLive || _aiParty == null || __instance == null) return;
            __state = MBegin(MTac, _aiParty(__instance));
        }

        public static void TacPost(MobilePartyAi __instance, long __state)
        {
            if (__state == 0L || _aiParty == null || __instance == null) return;
            MEnd(MTac, _aiParty(__instance), __state);
        }

        private static void HookTimed(MethodInfo m, string label, string pre, string post)
        {
            try
            {
                if (m == null) { _costMiss.Add(label + " (nie znaleziono)"); return; }
                _h.Patch(m, prefix: new HarmonyMethod(typeof(BanditLimit), pre) { priority = Priority.First },
                            postfix: new HarmonyMethod(typeof(BanditLimit), post) { priority = Priority.Last });
                _costDone.Add(label);
            }
            catch (Exception e) { _costMiss.Add(label + " (" + e.Message + ")"); Stumble("HookTimed(" + label + ")", e); }
        }

        private static void HookCost()
        {
            if (_costTried) return;
            _costTried = true;
            HookTimed(AccessTools.DeclaredMethod(typeof(BanditSpawnCampaignBehavior), "SpawnBanditsAroundHideout", new[] { typeof(Clan), typeof(float) }),
                      "rodzenie band", nameof(SpawnAllPre), nameof(SpawnAllPost));
            HookTimed(AccessTools.DeclaredMethod(typeof(AiPartyThinkBehavior), "PartyHourlyAiTick", new[] { typeof(MobileParty) }),
                      "mysl AI godzinowa", nameof(ThinkPre), nameof(ThinkPost));
            try { _aiParty = AccessTools.FieldRefAccess<MobilePartyAi, MobileParty>("_mobileParty"); }
            catch (Exception e) { _aiParty = null; _costMiss.Add("MobilePartyAi._mobileParty (" + e.Message + ")"); }
            if (_aiParty != null)
                HookTimed(AccessTools.DeclaredMethod(typeof(MobilePartyAi), "TickInternal", Type.EmptyTypes), "AI taktyczne", nameof(TacPre), nameof(TacPost));
            HookTimed(AccessTools.DeclaredMethod(typeof(CampaignEventDispatcher), "HourlyTickParty", new[] { typeof(MobileParty) }),
                      "tik godziny partii", nameof(HourPre), nameof(HourPost));
            HookTimed(AccessTools.DeclaredMethod(typeof(CampaignEventDispatcher), "DailyTickParty", new[] { typeof(MobileParty) }),
                      "tik doby partii", nameof(DayPre), nameof(DayPost));
            // ruch partii po mapie: w Campaign.RealTick (raz na klatke) - bez podzialu na bandy (rownolegle zadania gry); w linii szacunek liniowy wedlug liczby partii
            HookTimed(AccessTools.DeclaredMethod(typeof(Campaign), "RealTick", new[] { typeof(float) }), "ruch partii (Campaign.RealTick)", nameof(RealPre), nameof(RealPost));
        }

        // ------------------------------------------------------------ start sesji
        /// <summary>Z ArmouryBehavior.OnSessionLaunched (kampania istnieje - modele i zachowania gry sa): latki raz na proces, linia startu sesji.</summary>
        internal static void SessionStart()
        {
            try
            {
                EnsureCampaign();
                var s = Settings.Current;
                if (s == null || Campaign.Current == null) return;
                if (_h == null) _h = new Harmony("com.jeff.armoury.f8bandy");
                HookAround();
                if (s.BanditBirthsWherePeople || s.BanditCostLog) HookPick();
                if (s.BanditCostLog) HookCost();
                _costLive = s.BanditCostLog;
                var model = Campaign.Current.Models != null ? Campaign.Current.Models.BanditDensityModel : null;
                int cur = -1;
                try { if (model != null) cur = model.NumberOfMaximumBanditPartiesAroundEachHideout; } catch { }
                int t = RaisedTarget();
                Log.Info(P + "start sesji | klucze: Bandit Limit Per Hideout " + s.BanditLimitPerHideout + ", Bandit Births Where People " + (s.BanditBirthsWherePeople ? "TAK" : "NIE")
                         + ", Bandit Cost Log " + (s.BanditCostLog ? "TAK" : "NIE") + ", prawo wyrzutkow " + (OutlawLaw.On ? "TAK" : "NIE")
                         + " | model gestosci " + _modelName + ": ROT wokol " + _baseAround + " + w srodku " + _inEach + " = " + BaseSum + " na kryjowke; wokol teraz " + cur
                         + " -> cel " + (t > 0 ? t + " na zajeta kryjowke (PODNIESIONY)" : BaseSum + " na zajeta kryjowke (gry, bez zmian" + (OutlawLaw.On ? "" : " - prawo wyrzutkow wylaczone") + ")")
                         + " | latka celu: " + _aroundWhy + " | wybor kryjowki z ludzmi (przy celu podniesionym): " + _pickWhy
                         + " | stopery: " + (s.BanditCostLog ? (_costDone.Count > 0 ? string.Join(", ", _costDone.ToArray()) : "zadne") + (_costMiss.Count > 0 ? "; BRAK: " + string.Join(", ", _costMiss.ToArray()) : "")
                                                              : "wylaczone (Bandit Cost Log)")
                         + " | band teraz " + MobileParty.AllBanditParties.Count + ", partii swiata " + MobileParty.All.Count + ".");
            }
            catch (Exception e) { Stumble("SessionStart", e); }
        }

        // ------------------------------------------------------------ linia doby
        private static double EstMs(int m, int side)
        {
            int k = m * 2 + side;
            return _mSampled[k] > 0 ? Ms(_mTicks[k]) * ((double)_mCalls[k] / _mSampled[k]) : 0.0;
        }

        /// <summary>Z ArmouryBehavior.OnDailyTick (zaraz po OutlawLaw.Daily): linia "Bandy i limit (f8-bandy): dzien N" za okres od poprzedniej linii, zerowanie.</summary>
        internal static void Daily()
        {
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                EnsureCampaign();
                var s = Settings.Current;
                if (s == null) return;
                _costLive = s.BanditCostLog;
                if (!s.BanditCostLog) { _lastFrames = -1; return; }   // bez linii; zerowanie i zegar w finally
                var c = Campaign.Current;
                if (c == null) return;
                int day = (int)CampaignTime.Now.ToDays;
                bool firstLine = _lastWall == 0;
                double wallS = firstLine ? -1.0 : (t0 - _lastWall) / (double)Stopwatch.Frequency;
                int frames = c.CurrentTickCount;
                int dFrames = _lastFrames >= 0 ? frames - _lastFrames : -1;

                // bandy: klany z kryjowkami (cel), lupiezcy, inne (piraci morscy, dezerterzy, Inni...)
                int bands = 0, bHide = 0, bLoot = 0, bOther = 0; long men = 0;
                foreach (var p in MobileParty.AllBanditParties)
                {
                    if (p == null) continue;
                    bands++;
                    if (p.MemberRoster != null) men += p.MemberRoster.TotalManCount;
                    Clan pc = null;
                    try { pc = p.ActualClan; } catch { }
                    if (IsHideoutClan(pc)) bHide++;
                    else if (pc != null && pc.IsBanditFaction && pc.Culture != null && !pc.Culture.CanHaveSettlement && !pc.HasNavalNavigationCapability && pc.StringId != "deserters") bLoot++;
                    else bOther++;
                }
                int hideouts = 0, infestedAll = 0;
                foreach (var h in Hideout.All) { if (h == null) continue; hideouts++; if (h.IsInfested) infestedAll++; }
                var beh = c.GetCampaignBehavior<BanditSpawnCampaignBehavior>();
                int t = RaisedTarget();
                int per = t > 0 ? t : BaseSum;
                int sumInf = 0, sumTarget = 0, sumOld = 0, sumClanBands = 0, clans = 0, overOld = 0;
                var cl = new StringBuilder();
                foreach (var clan in Clan.BanditFactions)
                {
                    if (!IsHideoutClan(clan)) continue;
                    int inf = 0;
                    try { inf = _infestedOf != null && beh != null ? _infestedOf(beh, clan) : 0; } catch { inf = 0; }
                    if (_infestedOf == null || beh == null)
                        foreach (var h in Hideout.All) if (h != null && h.Settlement != null && h.Settlement.Culture == clan.Culture && h.IsInfested && h.MapFaction == clan) inf++;
                    int nb = clan.WarPartyComponents != null ? clan.WarPartyComponents.Count : 0;
                    clans++; sumInf += inf; sumTarget += inf * per; sumOld += inf * BaseSum; sumClanBands += nb;
                    overOld += Math.Max(0, nb - inf * BaseSum);
                    cl.Append(cl.Length > 0 ? ", " : "").Append(clan.StringId).Append(' ').Append(nb).Append('/').Append(inf * per).Append('/').Append(inf);
                }
                float pool = 0f;
                try { pool = OutlawLaw.PoolTotal(); } catch { }

                var model = c.Models != null ? c.Models.BanditDensityModel : null;
                int curAround = -1;
                try { if (model != null) curAround = model.NumberOfMaximumBanditPartiesAroundEachHideout; } catch { }

                double spawnMs = Ms(_dSpawnTicks);
                double thinkB = EstMs(MThink, 0), thinkO = EstMs(MThink, 1), tacB = EstMs(MTac, 0), tacO = EstMs(MTac, 1);
                double hourB = EstMs(MHour, 0), hourO = EstMs(MHour, 1), dayB = EstMs(MDay, 0), dayO = EstMs(MDay, 1);
                double outlawMs = DayProfile.LapMsNow("OutlawLaw.Daily");
                double bandMs = spawnMs + thinkB + tacB + hourB + dayB;
                double otherMs = thinkO + tacO + hourO + dayO;

                int parties = MobileParty.All.Count;
                var sb = new StringBuilder(1600);
                sb.Append(P).Append("dzien ").Append(day)
                  .Append(" | limit na kryjowke ").Append(s.BanditLimitPerHideout).Append(" (ROT ").Append(BaseSum).Append(" = ").Append(_baseAround).Append(" wokol + ").Append(_inEach)
                  .Append(" w srodku; wokol teraz ").Append(curAround).Append("), cel podniesiony ").Append(t > 0 ? "TAK" : (OutlawLaw.On ? "NIE (limit <= ROT albo brak latki)" : "NIE (prawo wyrzutkow wylaczone)"))
                  .Append(", narodziny tylko przy kryjowce z ludzmi ").Append(s.BanditBirthsWherePeople && _spawnHooked ? (t > 0 ? "TAK" : "TAK (bez skutku - cel ROT)") : "NIE")
                  .Append(" | band ").Append(bands).Append(" (klanow z kryjowkami ").Append(bHide).Append(", lupiezcow ").Append(bLoot).Append(", inne ").Append(bOther)
                  .Append("), ludzi ").Append(men).Append(", pula wyrzutkow ").Append((long)pool).Append(", partii swiata ").Append(parties)
                  .Append(" | kryjowki zajete ").Append(infestedAll).Append(" z ").Append(hideouts).Append(": klany z kryjowkami ").Append(clans).Append(" - zajete ").Append(sumInf)
                  .Append(", cel ").Append(sumTarget).Append(" (stary ").Append(sumOld).Append("), band klanow ").Append(sumClanBands).Append(", ponad stary cel ").Append(overOld)
                  .Append(", band/cel ").Append(sumTarget > 0 ? F2((double)sumClanBands / sumTarget) : "-")
                  .Append(" | klany (band/cel/zajete): ").Append(cl.Length > 0 ? cl.ToString() : "-")
                  .Append(" | narodziny dzis: prob po bramkach OutlawLaw ").Append(_dTries - _dPreRefused)
                  .Append(", ponizej starego celu ").Append(_dBelow).Append(" (kryjowka z ludzmi ").Append(_dBelowPicked).Append(", odmowione - brak ludzi przy kryjowkach klanu ")
                  .Append(_dBelowRefused).Append("), ponad stary cel ").Append(_dExtra).Append(" (kryjowka z ludzmi ").Append(_dExtraPicked)
                  .Append(", odmowione - brak ludzi przy kryjowkach klanu ").Append(_dExtraRefused).Append("), urodzone ").Append(_dSpawned)
                  .Append(" (puste: ponizej starego celu ").Append(_dEmptyBelow).Append(", ponad stary cel ").Append(_dEmptyExtra).Append("; ludzi: ponizej ").Append(_dMenBelow)
                  .Append(", ponad ").Append(_dMenExtra).Append("; bez sladu partii ").Append(_dNotFound).Append(')')
                  .Append(" | koszt doby (ms): rodzenie band ").Append(F1(spawnMs)).Append(" / ").Append(_dSpawnCalls).Append(" wywolan")
                  .Append("; mysl AI godzinowa: bandy ").Append(F1(thinkB)).Append(" / ").Append(_mCalls[MThink * 2]).Append(" (inne partie ").Append(F1(thinkO)).Append(" / ").Append(_mCalls[MThink * 2 + 1]).Append(')')
                  .Append("; AI taktyczne: bandy ").Append(F1(tacB)).Append(" / ").Append(_mCalls[MTac * 2]).Append(" (inne ").Append(F1(tacO)).Append(" / ").Append(_mCalls[MTac * 2 + 1]).Append(')')
                  .Append("; tik godziny partii: bandy ").Append(F1(hourB)).Append(" (inne ").Append(F1(hourO)).Append(')')
                  .Append("; tik doby partii: bandy ").Append(F1(dayB)).Append(" (inne ").Append(F1(dayO)).Append(')')
                  .Append("; OutlawLaw.Daily ").Append(outlawMs >= 0 ? F1(outlawMs) : "-")
                  .Append("; ruch partii (Campaign.RealTick, wszystkie partie) ").Append(F1(Ms(_dRealTicks))).Append(" / ").Append(_dRealCalls).Append(" klatek, udzial band wedlug liczby partii ~")
                  .Append(parties > 0 ? F1(Ms(_dRealTicks) * bands / parties) : "-")
                  .Append(" | razem bandy ").Append(F1(bandMs)).Append(" ms (inne partie ").Append(F1(otherMs)).Append(" ms), na bande ")
                  .Append(bands > 0 ? F1(bandMs * 1000.0 / bands) : "-").Append(" us, ")
                  .Append(wallS > 0 ? (100.0 * bandMs / (wallS * 1000.0)).ToString("0.00", Inv) + "% doby realnej" : "- doby realnej")
                  .Append(" | doba realnie ").Append(wallS > 0 ? F2(wallS) + " s" : "- (pierwsza linia sesji)").Append(", klatek ").Append(dFrames >= 0 ? dFrames.ToString(Inv) : "-")
                  .Append(", ").Append(wallS > 0 && dFrames > 0 ? F2(wallS * 1000.0 / dFrames) + " ms/klatke" : "- ms/klatke").Append(" (z pauzami gracza)")
                  .Append(" | pieniadze i towar: bez zmian (latka zmienia tylko cel liczby band); ludzie nowych band tylko z puli wyrzutkow (OutlawLaw)")
                  .Append(" | pomiar: probki mysl 1/4, taktyczne 1/8, tik godziny 1/8, tik doby, rodzenie i ruch pelne").Append(firstLine ? "; okres niepelny (od startu sesji)" : "")
                  .Append(" | potkniecia dzis ").Append(_dStumbles).Append(" (sesja ").Append(_stumbles).Append(") | czas linii ")
                  .Append(F2(Ms(Stopwatch.GetTimestamp() - t0))).Append(" ms.");
                Log.Info(sb.ToString());
                _lastFrames = frames;
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally
            {
                ClearDay();
                _lastWall = t0;
            }
        }
    }
}
