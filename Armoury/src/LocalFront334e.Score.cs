using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// 334e - WYBOR CELOW (projekt rozdz. 3.2, 3.4, 3.6).
    ///  - FANTOM (klucz LocalPhantomOff334e): transpiler DefaultTargetScoreCalculatingModel.GetTargetScoreForFaction (klasa deklarujaca) - oba odczyty
    ///    IFaction.CurrentTotalStrength (num10: sila CALEGO krolestwa celu z zalogami, rozdzielona na kazda osade) -> PhantomStrength = 0 dla krolestw AI. Kotwice
    ///    dokladnie x2, inaczej latka nic nie zmienia ("fantom BRAK" w linii startu). SCA wola baze - dziala tez pod SCA. Mianownik gry ma +0.1 (num15) - bez dzielenia przez 0.
    ///  - OCENA CELU (klucz LocalPower334e): postfiks na czynnym modelu (PatchTarget.OfActive - u Jeffa StrategicCampaignAI145.StrategicTargetScoreModel), priorytet
    ///    HigherThanNormal (przed rozejmem SiegeRansom.ScorePost - Last: rozejm 0 zostaje 0). Besieger: nowy cel q&lt;=0.8 x1.2, &lt;=1.0 x0.8, wiecej 0; biezacy cel marszu
    ///    (druzyny albo jej armii) do 1.25 x0.8 (histereza); juz oblegamy x1 (rozdz. 4). Raider: x1.2 / x0.8 / 0 ponad 1.25 (w trakcie rabunku x1 - rozdz. 4.3).
    ///    Defender wsi pod rabunkiem: przydzial obrony (rozdz. 5) albo x0.25, gdy nie zdazymy. LUSTRO OBRONY (recenzja 334e W6): druzyna, ktorej wlasna twierdza w dobie
    ///    drogi ma wroga z widocznym zamiarem oblezenia, nie dostaje x1.2 ani cofniecia SCA na cele ataku; Defender takiej twierdzy (jeszcze nie oblezonej - oblezone: OB5)
    ///    x1.2. Pod SCA cofamy mnozniki, ktore zastepuje q - TYLKO w oknie (q &lt;= 0.8): NearbyEnemyLordStrength x0.25, glebia x0.18 / x0.55 i wyczerpanie x0.55 (warunki
    ///    jak w dekompilacji StrategicTargetScoreModel - te same pomocniki SCA, delegaty z refleksji). ForcePeace / WantsPeace, jedzenie, osobowosc, pogoda - zostaja.
    ///  - ARMIE LUPIA (klucz ArmyRaid334e): gra nie daje armiom opcji Raider (AiMilitaryBehavior.FindBestTargetAndItsValueForFaction: Army != null - break). Wodz armii
    ///    AI bez oblezenia z wynikiem &gt; 0, ze spojnoscia ponad 2 x prog rozwiazania gry (rabunek zjada dobe), dostaje RaidSettlement najlepszej z 6 najblizszych wsi wroga
    ///    z q &lt;= 0.8 bez weta (teatr, 274, 326b), wynik = model gry (Raider, sila armii) x odleglosc jak GetDistanceScoreForRaiding. Bez armii w odpoczynku i nocnym obozie.
    /// </summary>
    internal static partial class LocalFront334e
    {
        private static Harmony _h;
        private static bool _hooksTried;
        private static int _phantomHits = -1;
        private static string _wPhantom = "nie wpieta", _wScore = "nie wpieta", _wSca = "BRAK", _wStick = "nie wpieta", _wModel = "-";
        private static readonly HashSet<MethodBase> _scorePatched = new HashSet<MethodBase>();
        [ThreadStatic] private static bool _inOwn;   // nasze wywolanie modelu (armie lupia, Zelazni) - ScorePost nie liczy q drugi raz

        /// <summary>Kotwice transpilera fantomu (test T6): 2 - wpiety.</summary>
        internal static int PhantomAnchors { get { return _phantomHits; } }

        // pomocniki SCA (delegaty)
        private static Func<Settlement, IFaction, float, bool> _scaFriendNear, _scaEnemyNear;
        private static Func<Settlement, IFaction, float> _scaDistFriend;
        private static Func<Settlement, IFaction, float, float> _scaNearLords;
        private static Func<Settlement, bool> _scaCooldown;
        private static Type _scaModelType;

        private struct SK : IEquatable<SK>
        {
            public readonly object A, B;
            public SK(object a, object b) { A = a; B = b; }
            public bool Equals(SK o) { return ReferenceEquals(A, o.A) && ReferenceEquals(B, o.B); }
            public override bool Equals(object o) { return o is SK && Equals((SK)o); }
            public override int GetHashCode() { unchecked { return ((A != null ? A.GetHashCode() : 0) * 397) ^ (B != null ? B.GetHashCode() : 0); } }
        }
        private sealed class ScaGeo { public int Day; public bool Friend, Enemy; public float Dist; }
        private static readonly Dictionary<SK, ScaGeo> _scaGeo = new Dictionary<SK, ScaGeo>();
        private static readonly Dictionary<SK, KeyValuePair<int, float>> _scaNear = new Dictionary<SK, KeyValuePair<int, float>>();
        private static readonly Dictionary<IFaction, KeyValuePair<int, bool>> _scaExh = new Dictionary<IFaction, KeyValuePair<int, bool>>();

        // lustro obrony (W6): wlasne twierdze z wrogiem idacym je oblegac (godzina)
        private static int _thrH = -1;
        private static readonly Dictionary<IFaction, List<Settlement>> _thr = new Dictionary<IFaction, List<Settlement>>();
        private static readonly Dictionary<MobileParty, KeyValuePair<int, bool>> _homeThr = new Dictionary<MobileParty, KeyValuePair<int, bool>>();

        // licznik doby: opcje > 0 po naszej ocenie (krolestwo -> [Raider, Besieger]), cofniecia SCA, oceny
        private static readonly Dictionary<string, int[]> _dOpts = new Dictionary<string, int[]>(StringComparer.Ordinal);
        private static int _dScoreN, _dZero, _dUp, _dDown, _dScaUndo, _dDefLow, _dDefFort, _dHomeCap, _dNoSnap, _dArmyRaidOffers, _dArmyRaidLog, _dArmyRaidVeto;
        private static readonly HashSet<string> _dArmyRaidSaid = new HashSet<string>(StringComparer.Ordinal);
        private static float _raidRange = -1f;

        private static void ResetScore()
        {
            _scaGeo.Clear(); _scaNear.Clear(); _scaExh.Clear(); _thr.Clear(); _thrH = -1; _homeThr.Clear(); _raidRange = -1f; ZeroScoreDay();
        }

        // 334f (L3, B334-7): cele pytane przez mysl AI krolestwa (q policzone) i w oknie (q <= 0.8) - ta sama miara, ktora dostala mysl; diagnoza Besieger
        private sealed class KAsk
        {
            public readonly HashSet<Settlement> AV = new HashSet<Settlement>(), AF = new HashSet<Settlement>(), WV = new HashSet<Settlement>(), WF = new HashSet<Settlement>();
            public int Asked, QZero, Blocked, Mirror; public double MinQ = -1; public string MinT = "-";   // model 0 - _dM0 (334g D1, bez zamka)
        }
        private static readonly Dictionary<string, KAsk> _dAsk = new Dictionary<string, KAsk>(StringComparer.Ordinal);
        private static KAsk KA(IFaction k)
        {
            KAsk r;
            string id = k != null ? k.StringId ?? "-" : "-";
            if (!_dAsk.TryGetValue(id, out r)) { r = new KAsk(); _dAsk[id] = r; }
            return r;
        }
        private static void Asked(KAsk ka, Settlement t, double q)
        {
            if (q < 0) return;
            bool vil = t.IsVillage;
            (vil ? ka.AV : ka.AF).Add(t);
            if (q <= OpenQ) (vil ? ka.WV : ka.WF).Add(t);
        }

        /// <summary>Besieger z wynikiem modelu 0 (licznik diagnozy krolestwa). Nie rzuca.</summary>
        private static void BesModel0(MobileParty p)
        {
            try
            {
                if (!PowerOn || WithPlayer(p)) return;
                var k = p.MapFaction as Kingdom;
                if (k == null || k.StringId == null) return;
                // 334g (D1): bez zamka na goracej sciezce (rownolegla mysl) - licznik atomowy
                var arr = _dM0.GetOrAdd(k.StringId, _ => new int[1]);
                System.Threading.Interlocked.Increment(ref arr[0]);
            }
            catch { }
        }
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, int[]> _dM0 = new System.Collections.Concurrent.ConcurrentDictionary<string, int[]>(StringComparer.Ordinal);
        private static int Model0Of(string kid) { int[] a; return kid != null && _dM0.TryGetValue(kid, out a) ? System.Threading.Volatile.Read(ref a[0]) : 0; }

        private static void ZeroScoreDay()
        {
            _dAsk.Clear(); _dM0.Clear();
            _dOpts.Clear(); _dScoreN = _dZero = _dUp = _dDown = _dScaUndo = _dDefLow = _dDefFort = _dHomeCap = _dNoSnap = _dArmyRaidOffers = _dArmyRaidLog = _dArmyRaidVeto = 0;
            _dArmyRaidSaid.Clear();
        }

        // ================================================================ latki (SessionStart)
        internal static void EnsureHooks()
        {
            if (_h == null) _h = new Harmony("com.jeff.armoury.local334e");
            if (!_hooksTried)
            {
                _hooksTried = true;
                // fantom: transpiler na klasie deklarujacej (raz na proces)
                try
                {
                    var m = AccessTools.DeclaredMethod(typeof(DefaultTargetScoreCalculatingModel), "GetTargetScoreForFaction",
                                                       new[] { typeof(Settlement), typeof(Army.ArmyTypes), typeof(MobileParty), typeof(float) });
                    if (m == null) _wPhantom = "BRAK (GetTargetScoreForFaction)";
                    else
                    {
                        _h.Patch(m, transpiler: new HarmonyMethod(typeof(LocalFront334e), nameof(PhantomTranspiler)));
                        _wPhantom = _phantomHits == 2 ? "TAK (2/2)" : "BRAK (kotwic " + _phantomHits + "/2)";
                    }
                }
                catch (Exception e) { _wPhantom = "BRAK (" + e.GetType().Name + ")"; Stumble("EnsureHooks.phantom", e); }
                // SCA: pomocniki do cofania mnoznikow
                try { WireSca(); } catch (Exception e) { _wSca = "BRAK (" + e.GetType().Name + ")"; Stumble("EnsureHooks.sca", e); }
                // BK SIEGE-STICK: prefiks na postfiksie BK (parametr po pozycji __0 - nazwa parametru BK bez znaczenia)
                try
                {
                    var t = AccessTools.TypeByName("BannerKings.Patches.ArmySiegePersistencePatches");
                    var m = t != null ? AccessTools.DeclaredMethod(t, "Postfix", new[] { typeof(MobileParty) }) : null;
                    if (t == null) _wStick = "bez BK (brak ArmySiegePersistencePatches)";
                    else if (m == null) _wStick = "BRAK (Postfix(MobileParty))";
                    else { _h.Patch(m, prefix: new HarmonyMethod(typeof(LocalFront334e), nameof(BkStickPre)) { priority = Priority.First }); _wStick = "TAK"; }
                }
                catch (Exception e) { _wStick = "BRAK (" + e.GetType().Name + ")"; Stumble("EnsureHooks.stick", e); }
            }
            // ocena celu: postfiks na czynnym modelu kazdej kampanii (inny model - nowa latka)
            try
            {
                var model = Campaign.Current != null && Campaign.Current.Models != null ? Campaign.Current.Models.TargetScoreCalculatingModel : null;
                if (model == null) { _wScore = "BRAK (brak modelu)"; return; }
                _wModel = model.GetType().FullName;
                var mm = PatchTarget.OfActive(model, "GetTargetScoreForFaction", new[] { typeof(Settlement), typeof(Army.ArmyTypes), typeof(MobileParty), typeof(float) }, "334e: ocena celu");
                if (mm == null) { _wScore = "BRAK (metoda)"; return; }
                if (_scorePatched.Add(mm))
                    _h.Patch(mm, postfix: new HarmonyMethod(typeof(LocalFront334e), nameof(ScorePost)) { priority = Priority.HigherThanNormal });
                _wScore = "TAK (" + mm.DeclaringType.Name + ")";
            }
            catch (Exception e) { _wScore = "BRAK (" + e.GetType().Name + ")"; Stumble("EnsureHooks.score", e); }
        }

        private static void WireSca()
        {
            var hlp = AccessTools.TypeByName("StrategicCampaignAI145.StrategicAiHelpers");
            var stt = AccessTools.TypeByName("StrategicCampaignAI145.StrategicAiState");
            _scaModelType = AccessTools.TypeByName("StrategicCampaignAI145.StrategicTargetScoreModel");
            if (hlp == null || stt == null || _scaModelType == null) { _wSca = "bez SCA"; return; }
            var a = AccessTools.DeclaredMethod(hlp, "HasFriendlyFortificationNear", new[] { typeof(Settlement), typeof(IFaction), typeof(float) });
            var b = AccessTools.DeclaredMethod(hlp, "HasEnemyFortificationNear", new[] { typeof(Settlement), typeof(IFaction), typeof(float) });
            var c = AccessTools.DeclaredMethod(hlp, "DistanceToNearestFriendlyFortification", new[] { typeof(Settlement), typeof(IFaction) });
            var d = AccessTools.DeclaredMethod(hlp, "NearbyEnemyLordStrength", new[] { typeof(Settlement), typeof(IFaction), typeof(float) });
            var e = AccessTools.DeclaredMethod(stt, "IsTargetOnCooldown", new[] { typeof(Settlement) });
            if (a == null || b == null || c == null || d == null || e == null) { _wSca = "BRAK (pomocniki SCA)"; return; }
            _scaFriendNear = (Func<Settlement, IFaction, float, bool>)Delegate.CreateDelegate(typeof(Func<Settlement, IFaction, float, bool>), a);
            _scaEnemyNear = (Func<Settlement, IFaction, float, bool>)Delegate.CreateDelegate(typeof(Func<Settlement, IFaction, float, bool>), b);
            _scaDistFriend = (Func<Settlement, IFaction, float>)Delegate.CreateDelegate(typeof(Func<Settlement, IFaction, float>), c);
            _scaNearLords = (Func<Settlement, IFaction, float, float>)Delegate.CreateDelegate(typeof(Func<Settlement, IFaction, float, float>), d);
            _scaCooldown = (Func<Settlement, bool>)Delegate.CreateDelegate(typeof(Func<Settlement, bool>), e);
            _wSca = "TAK";
        }

        // ================================================================ fantom
        public static IEnumerable<CodeInstruction> PhantomTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var list = new List<CodeInstruction>(instructions);
            try
            {
                var getter = AccessTools.PropertyGetter(typeof(IFaction), "CurrentTotalStrength");
                var repl = AccessTools.Method(typeof(LocalFront334e), nameof(PhantomStrength));
                int n = 0;
                foreach (var c in list) if ((c.opcode == OpCodes.Callvirt || c.opcode == OpCodes.Call) && Equals(c.operand, getter)) n++;
                _phantomHits = n;
                if (n != 2 || getter == null || repl == null) return list;
                foreach (var c in list) if ((c.opcode == OpCodes.Callvirt || c.opcode == OpCodes.Call) && Equals(c.operand, getter)) { c.opcode = OpCodes.Call; c.operand = repl; }
            }
            catch (Exception e) { _phantomHits = -2; Stumble("PhantomTranspiler", e); }
            return list;
        }

        /// <summary>Z transpilera: sila krolestwa celu w "fantomie" num10 gry - 0 dla krolestw AI (zalogi innych osad i armie daleko sie nie licza; sile przy celu
        /// liczy num6/num7 gry i nasz q), inaczej odczyt gry.</summary>
        public static float PhantomStrength(IFaction f)
        {
            if (f == null) return 0f;
            try
            {
                if (f.IsKingdomFaction && PhantomOn)
                {
                    var pc = Clan.PlayerClan;
                    if (pc == null || pc.MapFaction != f) return 0f;
                }
            }
            catch { }
            return f.CurrentTotalStrength;
        }

        private static bool WithPlayer(MobileParty p)
        {
            if (p == null) return false;
            if (p.IsMainParty) return true;
            var m = MobileParty.MainParty;
            return m != null && p.Army != null && m.Army == p.Army;
        }

        // ================================================================ lustro obrony (W6)
        /// <summary>Wlasne twierdze (jeszcze nie oblezone), na ktore idzie wrog z widocznym zamiarem oblezenia - godzina. Wolajacy trzyma _lk.</summary>
        private static void BuildThreat()
        {
            int h = NowH;
            if (_thrH == h) return;
            _thrH = h; _thr.Clear(); _homeThr.Clear();
            foreach (var mp in MobileParty.AllLordParties)
            {
                try
                {
                    if (mp == null || !mp.IsActive || (mp.Army != null && mp.Army.LeaderParty != mp)) continue;
                    Settlement t = null;
                    if (mp.DefaultBehavior == AiBehavior.BesiegeSettlement) t = mp.TargetSettlement;
                    else if (mp.Army != null && mp.DefaultBehavior != AiBehavior.DefendSettlement) t = mp.Army.AiBehaviorObject as Settlement;
                    if (t == null || !t.IsFortification || t.IsUnderSiege) continue;
                    var tf = t.MapFaction;
                    if (tf == null || mp.MapFaction == null || !FactionManager.IsAtWarAgainstFaction(mp.MapFaction, tf)) continue;
                    List<Settlement> l;
                    if (!_thr.TryGetValue(tf, out l)) { l = new List<Settlement>(); _thr[tf] = l; }
                    if (!l.Contains(t)) l.Add(t);
                }
                catch { }
            }
        }

        private static bool Threatened(IFaction f, Settlement t)
        {
            BuildThreat();
            List<Settlement> l;
            return f != null && _thr.TryGetValue(f, out l) && l.Contains(t);
        }

        /// <summary>Druzyna ma w dobie drogi wlasna twierdze, na ktora idzie wrog. Wolajacy trzyma _lk.</summary>
        private static bool HomeThreat(MobileParty p)
        {
            BuildThreat();
            KeyValuePair<int, bool> kv;
            if (_homeThr.TryGetValue(p, out kv) && kv.Key == _thrH) return kv.Value;
            bool r = false;
            List<Settlement> l;
            if (p.MapFaction != null && _thr.TryGetValue(p.MapFaction, out l))
            {
                var u = LiveUnit(p);
                if (u != null)
                    foreach (var t in l) { float d = DaysTo(u, t); if (d >= 0f && d <= 1f) { r = true; break; } }
            }
            _homeThr[p] = new KeyValuePair<int, bool>(_thrH, r);
            return r;
        }

        // ================================================================ ocena celu
        /// <summary>Postfiks czynnego modelu GetTargetScoreForFaction (HigherThanNormal, przed rozejmem). Nie rzuca.</summary>
        public static void ScorePost(Settlement __0, Army.ArmyTypes __1, MobileParty __2, float __3, ref float __result)
        {
            if (_inOwn || __0 == null || __2 == null) return;
            if (__result <= 0f) { if (__1 == Army.ArmyTypes.Besieger) BesModel0(__2); return; }
            if (__1 != Army.ArmyTypes.Besieger && __1 != Army.ArmyTypes.Raider && __1 != Army.ArmyTypes.Defender) return;
            if (!PowerOn) return;
            var p = __2;
            if (WithPlayer(p)) return;
            var k = p.MapFaction as Kingdom;
            if (k == null) return;
            var t = __0;
            float m = 1f, undo = 1f;
            try
            {
                long w0 = Ts();
                double q = -1;
                KAsk ka = null;
                lock (_lk)
                {
                    long t1 = Ts();
                    _dWaitT += t1 - w0;   // 334f (Z0): czekanie na zamek poza kosztem
                    if (!SnapFresh) { _dNoSnap++; return; }   // bez migawki z dzis - stara gra (nie q = 0)
                    _dScoreN++;
                    ka = KA(k);
                    if (__1 == Army.ArmyTypes.Besieger)
                    {
                        if (!t.IsFortification) return;
                        ka.Asked++;
                        if (Blocked(p, t)) { m = 0f; ka.Blocked++; }
                        else
                        {
                            var bs = p.BesiegedSettlement ?? (p.Army != null && p.Army.LeaderParty != null ? p.Army.LeaderParty.BesiegedSettlement : null);
                            if (bs != t)
                            {
                                bool cur = p.TargetSettlement == t && p.DefaultBehavior == AiBehavior.BesiegeSettlement;
                                if (!cur && p.Army != null) { try { cur = ReferenceEquals(p.Army.AiBehaviorObject, t); } catch { } }
                                float our = OurDays(p, t);
                                if (our >= 0f)
                                {
                                    float first; double ally;
                                    double z = ZFor(t, k, p, our + SiegeDays, out first, out ally);
                                    q = Ratio(z, __3 + ally);
                                    m = SiegeMult(q, cur);
                                    double qk;
                                    if (m > 0f && KBlocked(k, t, p, __3, our, out qk)) { m = 0f; q = qk; ka.Blocked++; }   // 334g: po odstapieniu krolestwa - miara odwrotu
                                    Asked(ka, t, q);
                                    if (m == 0f) { ka.QZero++; if (ka.MinQ < 0 || q < ka.MinQ) { ka.MinQ = q; ka.MinT = t.StringId; } }
                                }
                            }
                        }
                    }
                    else if (__1 == Army.ArmyTypes.Raider)
                    {
                        if (!t.IsVillage) return;
                        var me = p.MapEvent;
                        bool raiding = me != null && me.IsRaid && me.MapEventSettlement == t;
                        if (Blocked(p, t)) m = 0f;
                        else if (!raiding)
                        {
                            float our = OurDays(p, t);
                            if (our >= 0f)
                            {
                                int men; StrOf(p, out men);
                                float first; double ally;
                                double z = ZFor(t, k, p, our + RaidDays(t.SettlementHitPoints, men), out first, out ally);
                                q = Ratio(z, __3 + ally);
                                m = RaidMult(q);
                                Asked(ka, t, q);
                            }
                        }
                    }
                    else
                    {
                        if (t.IsFortification)
                        {
                            // lustro obrony: twierdza swojej strony, na ktora idzie wrog (jeszcze nie oblezona - oblezone prowadzi OB5 / 326c)
                            if (!t.IsUnderSiege && SameSide(k, t.MapFaction) && Threatened(t.MapFaction, t)) { m = 1.2f; _dDefFort++; }
                        }
                        else if (t.IsVillage)
                        {
                            var me = t.Party != null ? t.Party.MapEvent : null;
                            if (me == null || !me.IsRaid) return;
                            m = DefenceMult(p, t);
                            if (m < 1f) _dDefLow++;
                        }
                        else return;
                    }
                    if (__1 != Army.ArmyTypes.Defender && m > 1f && HomeThreat(p)) { m = 1f; _dHomeCap++; if (__1 == Army.ArmyTypes.Besieger) ka.Mirror++; }
                    if (__1 == Army.ArmyTypes.Besieger && m > 1f && q >= 0 && q <= OpenQ && _wSca == "TAK" && _scaModelType != null) undo = ScaUndo(t, p, __3);
                    if (m == 0f) _dZero++; else if (m > 1f) _dUp++; else if (m < 1f) _dDown++;
                    if (undo != 1f) _dScaUndo++;
                    if (__1 != Army.ArmyTypes.Defender && __result * m * undo > 0f)
                    {
                        int[] o;
                        if (!_dOpts.TryGetValue(k.StringId, out o)) { o = new int[2]; _dOpts[k.StringId] = o; }
                        o[__1 == Army.ArmyTypes.Raider ? 0 : 1]++;
                    }
                    _dQ++;
                    Cost(TMs(Ts() - t1));
                }
                __result *= m * undo;
            }
            catch (Exception e) { Stumble("ScorePost", e); }
        }

        /// <summary>Cofniecie mnoznikow SCA zastapionych przez q - tylko w oknie (q &lt;= 0.8; warunki 1:1 z dekompilacji StrategicTargetScoreModel, te same pomocniki SCA;
        /// pamiec: geometria i wyczerpanie na dobe, lordowie w 95 na godzine). Wolajacy trzyma _lk.</summary>
        private static float ScaUndo(Settlement t, MobileParty p, float ourStr)
        {
            try
            {
                var f = p.MapFaction;
                if (!(t.IsTown || t.IsCastle) || t.MapFaction == null || !FactionManager.IsAtWarAgainstFaction(f, t.MapFaction)) return 1f;
                if (_scaCooldown(t)) return 1f;   // SCA zwraca wtedy x0.25 bez reszty mnoznikow
                float r = 1f;
                var key = new SK(t, f);
                int h = NowH, d = Today;
                KeyValuePair<int, float> nk;
                float near;
                if (_scaNear.TryGetValue(key, out nk) && nk.Key == h) near = nk.Value;
                else { near = _scaNearLords(t, f, 95f); _scaNear[key] = new KeyValuePair<int, float>(h, near); }
                if (near > ourStr * 1.25f) r /= 0.25f;
                ScaGeo g;
                if (!_scaGeo.TryGetValue(key, out g) || g.Day != d)
                {
                    g = new ScaGeo { Day = d, Friend = _scaFriendNear(t, f, 55f), Enemy = _scaEnemyNear(t, f, 55f), Dist = _scaDistFriend(t, f) };
                    _scaGeo[key] = g;
                }
                if (!g.Friend && g.Enemy) r /= 0.18f;
                if (g.Dist > 85f) r /= 0.55f;
                KeyValuePair<int, bool> ex;
                if (!_scaExh.TryGetValue(f, out ex) || ex.Key != d) { ex = new KeyValuePair<int, bool>(d, ScaNoBandits320.ExhaustedOf(f) == true); _scaExh[f] = ex; }
                if (ex.Value) r /= 0.55f;
                return r;
            }
            catch (Exception e) { Stumble("ScaUndo", e); return 1f; }
        }

        // ================================================================ B8: BK cel armii z q > 1.0 = weto (WarFront334.VetoedAny)
        /// <summary>Cel oblezenia BK z q > 1.0: O = wodz + kandydaci zbiorki z modelu armii gry (ArmyManagementCalculationModel.CanLordCreateArmy - te same druzyny,
        /// ktore gra wezwie; zapas: wolne druzyny krolestwa w zasiegu wezwania modelu, bez odpoczynku i nocnego obozu). Nie rzuca.</summary>
        internal static bool BkBlocked(MobileParty leader, Kingdom k, Settlement st, out double q)
        {
            q = -1;
            if (leader == null || k == null || st == null || !PowerOn || WithPlayer(leader)) return false;
            try
            {
                double o = 0; string src = "model";
                var model = Campaign.Current.Models.ArmyManagementCalculationModel;
                MBList<MobileParty> cand = null;
                try { model.CanLordCreateArmy(leader, out cand); } catch { cand = null; }
                o = leader.Party != null ? leader.Party.EstimatedStrength : 0f;
                if (cand != null && cand.Count > 0)
                {
                    foreach (var mp in cand) if (mp != null && mp != leader && mp.Party != null && mp.IsActive) o += mp.Party.EstimatedStrength;
                }
                else
                {
                    src = "zasieg";
                    float r = model.MaximumDistanceToCallToArmy;
                    var lp = leader.GetPosition2D;
                    foreach (var wp in k.WarPartyComponents)
                    {
                        var mp = wp != null ? wp.MobileParty : null;
                        if (mp == null || mp == leader || !mp.IsActive || mp.Party == null || mp.IsMainParty) continue;
                        if (mp.Army != null || mp.BesiegerCamp != null || mp.MapEvent != null) continue;
                        if (mp.GetPosition2D.DistanceSquared(lp) > r * r) continue;
                        try { if (NightRest.FieldCampHolds(mp) || NightRest.IsCamping(mp)) continue; } catch { }
                        o += mp.Party.EstimatedStrength;
                    }
                }
                lock (_lk)
                {
                    if (!SnapFresh) return false;
                    long t1 = Ts();
                    float our = OurDays(leader, st);
                    if (our < 0f) return false;
                    float first; double ally;
                    double z = ZFor(st, k, leader, our + SiegeDays, out first, out ally);
                    q = Ratio(z, o + ally);
                    double qk = -1;
                    bool kb = q <= SiegeBanQ && KBlocked(k, st, leader, o, our, out qk);   // 334g: po odstapieniu krolestwa - miara odwrotu
                    if (kb) q = qk;
                    Cost(TMs(Ts() - t1));
                    if (q > SiegeBanQ) { _dBkVeto++; if (src != "model") _dBkVetoFallback++; return true; }
                }
            }
            catch (Exception e) { Stumble("BkBlocked", e); }
            return false;
        }
        private static int _dBkVeto, _dBkVetoFallback;

        // ================================================================ 3.6 armie lupia (z ThinkField)
        private static void ArmyRaid(MobileParty party, PartyThinkParams pt)
        {
            var army = party.Army;
            if (army == null || army.LeaderParty != party || party.MapEvent != null || party.BesiegerCamp != null) return;
            try { if (ArmyRestDay.Resting(army) || NightRest.FieldCampHolds(party) || NightRest.IsCamping(party)) return; } catch { }
            // rabunek zjada dobe spojnosci - armia, ktorej spojnosci starczy tylko na rozejscie, nie rabuje (prog rozwiazania z modelu armii gry)
            int disp = 10;
            try { disp = Campaign.Current.Models.ArmyManagementCalculationModel.CohesionThresholdForDispersion; } catch { }
            if (army.Cohesion < 2f * disp) return;
            var scores = pt.AIBehaviorScores;
            if (scores == null) return;
            float best = 0f;
            for (int i = 0; i < scores.Count; i++)
            {
                var e = scores[i];
                if (e.Item2 > best) best = e.Item2;
                if (e.Item2 > 0f && !e.Item1.WillGatherArmy && (e.Item1.AiBehavior == AiBehavior.BesiegeSettlement || e.Item1.AiBehavior == AiBehavior.RaidSettlement)) return;
            }
            var k = party.MapFaction as Kingdom;
            if (k == null) return;
            // jedzenie jak gra dla rabunku (srednia dni armii >= prog modelu)
            try
            {
                int days = party.GetNumDaysForFoodToLast(), n = 1;
                foreach (var ap in party.AttachedParties) { if (ap == null) continue; days += ap.GetNumDaysForFoodToLast(); n++; }
                if ((float)days / n < Campaign.Current.Models.MobilePartyAIModel.NeededFoodsInDaysThresholdForRaid) return;
            }
            catch { return; }
            if (_raidRange < 0f) _raidRange = 5f * Campaign.Current.GetAverageDistanceBetweenClosestTwoTownsWithNavigationType(MobileParty.NavigationType.Default);
            float range = _raidRange;
            var pos = party.GetPosition2D;
            var cand = new List<KeyValuePair<Settlement, float>>();
            foreach (var f in k.FactionsAtWarWith)
            {
                if (f == null || f.Leader == null || !f.IsMapFaction || ScaNoBandits320.Skip(f) != 0) continue;
                foreach (var st in f.Settlements)
                {
                    if (st == null || !st.IsVillage || st.Village == null || st.Village.VillageState != Village.VillageStates.Normal) continue;
                    if (st.Party != null && st.Party.MapEvent != null) continue;
                    float d2 = pos.DistanceSquared(st.GetPosition2D);
                    if (d2 <= range * range) cand.Add(new KeyValuePair<Settlement, float>(st, d2));
                }
            }
            if (cand.Count == 0) return;
            cand.Sort((a, b) => a.Value.CompareTo(b.Value));
            float strength = party.GetTotalLandStrengthWithFollowers(false);   // sila dla modelu gry (jego miara)
            int men; lock (_lk) StrOf(party, out men);
            // 334g: sila do q ta sama miara co wrog w ZFor (EstimatedStrength calej armii - czlonkowie w drodze tez: ZFor pomija ich jako ta sama grupe)
            float qStr = 0f;
            foreach (var ap in army.Parties) if (ap != null && ap.IsActive && ap.Party != null) qStr += PStr(ap.Party);
            var model = Campaign.Current.Models.TargetScoreCalculatingModel;
            Settlement bestV = null; float bestS = 0f; double bestQ = -1; MobileParty.NavigationType bestNav = MobileParty.NavigationType.None; bool bestPort = false;
            for (int i = 0, tried = 0; i < cand.Count && tried < 6; i++)
            {
                var v = cand[i].Key;
                string why;
                if (WarFront334.VetoedFor334e(k, v, out why)) { _dArmyRaidVeto++; continue; }   // teatr Polnocy / Wysp, 274, 326b - ThinkPost (Last) idzie po wetach
                tried++;
                double q;
                lock (_lk)
                {
                    if (!SnapFresh || Blocked(party, v)) continue;
                    float our = OurDays(party, v);
                    if (our < 0f) continue;
                    float first; double ally;
                    double z = ZFor(v, k, party, our + RaidDays(v.SettlementHitPoints, men), out first, out ally);
                    q = Ratio(z, qStr + ally);
                }
                if (q > OpenQ) continue;
                MobileParty.NavigationType nav; float dist; bool fromPort;
                long m0 = Ts();
                AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty(party, v, false, out nav, out dist, out fromPort);
                _modelT += Ts() - m0;
                if (nav == MobileParty.NavigationType.None || dist > range) continue;
                float ds = MBMath.Map(0.75f - dist / range, 0f, 1f, 0.1f, 1f);
                float s;
                long m1 = Ts();
                _inOwn = true;
                try { s = model.GetTargetScoreForFaction(v, Army.ArmyTypes.Raider, party, strength) * ds; }
                finally { _inOwn = false; _modelT += Ts() - m1; }
                if (s > bestS) { bestS = s; bestV = v; bestQ = q; bestNav = nav; bestPort = fromPort; }
            }
            if (bestV == null || !(bestS > 0f)) return;
            pt.AddBehaviorScore((new AIBehaviorData(bestV, AiBehavior.RaidSettlement, bestNav, false, bestPort, false), bestS));
            lock (_lk)
            {
                _dArmyRaidOffers++;
                if (bestS > best && _dArmyRaidSaid.Add(party.StringId + "|" + bestV.StringId) && _dArmyRaidLog < 30 && S != null && S.LogEnabled)
                {
                    _dArmyRaidLog++;
                    Log.Info(Tag + "armia rabuje | dzien " + Today + " | " + Nm(party) + " (" + Id(k) + ", armia " + army.Parties.Count + " druzyn, sila " + F0(qStr) + ", model " + F0(strength) + ") | " + bestV.StringId
                             + " (" + Nm(bestV) + ", " + Id(bestV.MapFaction) + ") | q " + F2(bestQ) + " | wynik " + bestS.ToString("0.000", Inv) + " (najlepszy inny " + best.ToString("0.000", Inv) + ").");
                }
            }
        }
    }
}
