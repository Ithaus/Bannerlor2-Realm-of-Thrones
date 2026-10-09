using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// H3 - PRZEGRANI UCHODZA ZAMIAST GINAC (decyzja Jeffa 07.10, hamulec H3; projekt docs/paczki/n9-przegrani-uchodza.md).
    /// Dla gracza: w bitwie AI w polu gra dalej sama decyduje, kto wygral. Zmienia sie to, co dzieje sie z pokonanymi po bitwie:
    /// ginie 5-65% wedlug sytuacji (poscig konnicy, rzeka / bagno / wawoz obok, przewaga, zawodowcy na pospolite ruszenie, konni
    /// przegrani uciekaja na otwartym polu), do niewoli 30% weteranow (tier 4+) i 5% reszty, reszta ucieka i wraca tam, skad ja
    /// wzieto (OutlawLaw.OnMapEventEnded + SendHome). Zwyciezca traci zabitymi najwyzej 5% - reszta jego poleglych to ranni.
    ///
    /// Kod: prefiks na MapEvent.CalculateAndCommitMapEventResults (PRZED lupem z cial, pojmaniem i MapEventEnded - wszyscy pozniejsi
    /// czytelnicy widza jeden, poprawiony podzial) - dwie fazy: plan i sprawdzenie, dopiero potem same AddToCounts (zmiany zerowe
    /// pomijane). Postfiks na tej samej metodzie tylko mierzy (jency wzieci = j, przegrani bez szeregowych). Bitwy gracza, oblezenia,
    /// rabunki, morze, odwrot, poddanie i Inni - bez zmian (licznik). Bohaterowie - jak w grze. Zapisu nie ma (liczniki dnia).
    ///
    /// SendHome - jedna funkcja pochodzenia: tabor wsi / rybacy -> hearth swojej wsi (odwrotnosc VillagerCampaignBehavior:179);
    /// karawany, zalogi, milicje, patrole i straz karawan -> "z szablonu" (gra tworzy ich z niczego - licznik do E7 / 108);
    /// Soldier / Mercenary -> ludnosc BK najblizszej bitwie osady swojej frakcji i kultury; reszta (bandyci, chlopi) -> hearth regionu.
    /// </summary>
    internal static class LosersFlee
    {
        internal static bool On { get { var s = Settings.Current; return s != null && s.LosersFleeEnabled; } }

        // rodzaje partii
        internal const int KArmy = 0, KBand = 1, KVillager = 2, KCaravan = 3, KGarrison = 4, Kinds = 5;
        private static readonly string[] KName = { "wojsko", "bandy", "tabory wsi", "karawany", "zalogi/milicje/patrole" };
        // skad czlowiek wraca do domu: rozbici z bitwy, uwolnieni jency bez odbiorcy, powrot z puli wyrzutkow
        internal const int SrcRouted = 0, SrcFreed = 1, SrcPool = 2;
        // dokad trafil: ludnosc BK, hearth wsi, "z szablonu"
        internal const int CatBk = 0, CatVillage = 1, CatTemplate = 2;
        // zawod klucza puli wyrzutkow
        internal const int KeyCommon = 0, KeyBandit = 1, KeySoldier = 2, KeyGuard = 3;

        // wzor (HISTORIA 4.3 + czlon F z audytu 2.8 / E10 / R10): p = 0.15 + 0.35C + 0.15T + 0.15O + 0.20Q - 0.10F(1-T), granice 5-65%
        private const float PBase = 0.15f, WC = 0.35f, WT = 0.15f, WO = 0.15f, WQ = 0.20f, WF = 0.10f, PMin = 0.05f, PMax = 0.65f;
        // promien sprawdzania terenu: srodek + po 4 punkty na kole 1 i 2 jednostek mapy (jedna stala na promien - prog 10-30% bitew z T = 1)
        private const float TerrainR1 = 1f, TerrainR2 = 2f;
        // tabor wsi wraca do swojej wsi dokladnie tak, jak go zdjeto: VillagerCampaignBehavior.cs:179, 187 - Hearth -= (n + 1) / 2
        private const float VillagerHearthPerMan = 0.5f;
        private const int VeteranTier = 4;
        // kultury z niewolnictwem wedlug Martina (decyzja Jeffa 07.10 pkt 5 i 6a; bez Pentos i Lorath) - jedna lista, ktora ma przejac
        // rabunek osada po osadzie: Zatoka Niewolnicza, Volantis, Lys, Myr, Tyrosh, Qohor, Norvos, Valyria, Dothrakowie (ROT: khuzait),
        // Zelazni Ludzie (thralls; ROT: sturgia = Iron Islands)
        private static readonly HashSet<string> SlaveCultureIds = new HashSet<string>
            { "ghiscari", "volantine", "lyseni", "myrish", "tyroshi", "qohorik", "norvos", "valyrian", "khuzait", "sturgia" };

        internal static bool SlaveCulture(CultureObject c) { return c != null && c.StringId != null && SlaveCultureIds.Contains(c.StringId); }

        // ------------------------------------------------------------ latka
        private static AccessTools.FieldRef<MapEvent, bool> _appliedRef;
        private static AccessTools.FieldRef<MapEventSide, bool> _surrRef;
        private static bool _patched;

        // ------------------------------------------------------------ liczniki dnia
        private static int _battles, _skipPlayer, _skipNotField, _skipRetreat, _skipSurr, _skipUndead;
        private static readonly int[] _kMen = new int[Kinds], _kDead = new int[Kinds], _kCapt = new int[Kinds], _kRouted = new int[Kinds], _kDead0 = new int[Kinds];
        private static int _lMen, _lDead, _lDead0, _lCapt, _lRouted; private static double _lCapt0;
        private static int _wMen, _wDead, _wDead0, _wRevived;
        private static double _realLose, _realLose0, _realWin, _realWin0; private static int _realN;
        private static double _sumP, _sumC, _sumT, _sumO, _sumQ, _sumF; private static int _trapBattles;
        private static int _sumJ, _taken, _lostByGame, _losersLeft, _controlDiff, _checked;
        private static int _freed, _freedOutside;
        private static int _toBk, _asSerfs, _asNobles, _toVillageMen, _toCommon, _template, _templateGarr, _homeless, _vanished, _bkOddCalls, _bkOddDiff;
        private static float _toVillageHearth, _toCommonHearth, _templateFloat;
        private static int _recruitedPrisoners;
        private static int _stumbleCalc, _stumbleApply, _stumbleHome, _stumblePost;
        private static long _ticks;
        private static bool _errCalc, _errApply, _errHome, _errPost;
        private static long _lastWorldPop = -1;
        private static bool _ownersLogged;
        private static string _csvPath;

        private sealed class RegionRow { public Settlement St; public int Bk, VillageMen, Woods, Template, Freed; public float VillageHearth; }
        private static readonly Dictionary<string, RegionRow> _rows = new Dictionary<string, RegionRow>();

        // pamiec na sesje / dobe / bitwe
        private static readonly Dictionary<string, int> _keyKind = new Dictionary<string, int>();
        private static readonly Dictionary<string, CharacterObject> _keyTroop = new Dictionary<string, CharacterObject>();
        private static readonly Dictionary<string, Home> _battleHome = new Dictionary<string, Home>(), _poolHome = new Dictionary<string, Home>();
        private static int _poolHomeDay = -1;

        internal static void Reset()
        {
            ClearDay();
            _keyKind.Clear(); _keyTroop.Clear(); _battleHome.Clear(); _poolHome.Clear(); _poolHomeDay = -1;
            _homes = null; _homeOf = null; _homesDay = -1;
            _bkTried = false; _popMgr = null; _getPopData = null; _fromSoldiers = null; _updateType = null; _typeCount = null; _isRetinue = null; _totalPop = null; _serfs = null; _nobles = null;
            _lastWorldPop = -1; _ownersLogged = false; _csvPath = null; _cur = null;
            _errCalc = _errApply = _errHome = _errPost = false;
        }

        private static void ClearDay()
        {
            _battles = _skipPlayer = _skipNotField = _skipRetreat = _skipSurr = _skipUndead = 0;
            Array.Clear(_kMen, 0, Kinds); Array.Clear(_kDead, 0, Kinds); Array.Clear(_kCapt, 0, Kinds); Array.Clear(_kRouted, 0, Kinds); Array.Clear(_kDead0, 0, Kinds);
            _lMen = _lDead = _lDead0 = _lCapt = _lRouted = 0; _lCapt0 = 0;
            _wMen = _wDead = _wDead0 = _wRevived = 0;
            _realLose = _realLose0 = _realWin = _realWin0 = 0; _realN = 0;
            _sumP = _sumC = _sumT = _sumO = _sumQ = _sumF = 0; _trapBattles = 0;
            _sumJ = _taken = _lostByGame = _losersLeft = _controlDiff = _checked = 0;
            _freed = _freedOutside = 0;
            _toBk = _asSerfs = _asNobles = _toVillageMen = _toCommon = _template = _templateGarr = _homeless = _vanished = _bkOddCalls = _bkOddDiff = 0;
            _toVillageHearth = _toCommonHearth = _templateFloat = 0f;
            _recruitedPrisoners = 0;
            _stumbleCalc = _stumbleApply = _stumbleHome = _stumblePost = 0;
            _ticks = 0;
            _rows.Clear();
        }

        // ------------------------------------------------------------ rodzaj partii, zawod klucza puli
        internal static int KindOf(MobileParty mp)
        {
            if (mp == null) return KArmy;
            if (mp.IsVillager) return KVillager;            // rybacy NavalDLC tez (FishingPartyComponent : VillagerPartyComponent)
            if (OutlawLaw.IsOutlawParty(mp)) return KBand;
            if (mp.IsCaravan) return KCaravan;
            if (mp.IsGarrison || mp.IsMilitia || mp.IsPatrolParty) return KGarrison;
            return KArmy;                                    // partie rodow i inne partie z wodzem
        }

        internal static CharacterObject TroopOf(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            CharacterObject c;
            if (_keyTroop.TryGetValue(key, out c)) return c;
            try { c = MBObjectManager.Instance.GetObject<CharacterObject>(key); } catch { c = null; }
            _keyTroop[key] = c;
            return c;
        }

        internal static int KeyKind(string key)
        {
            if (string.IsNullOrEmpty(key) || key == "~") return KeyCommon;
            int k;
            if (_keyKind.TryGetValue(key, out k)) return k;
            k = KeyCommon;
            var ch = TroopOf(key);
            if (ch != null)
            {
                var o = ch.Occupation;
                if (o == Occupation.Soldier || o == Occupation.Mercenary) k = KeySoldier;
                else if (o == Occupation.Bandit) k = KeyBandit;
                else if (o == Occupation.CaravanGuard) k = KeyGuard;
            }
            _keyKind[key] = k;
            return k;
        }

        // ------------------------------------------------------------ ludnosc BK (refleksja - wzor Levy.Resolve / PopulationLaw)
        private static bool _bkTried;
        private static object _popMgr, _serfs, _nobles;
        private static MethodInfo _getPopData, _fromSoldiers, _updateType, _typeCount, _isRetinue;
        private static PropertyInfo _totalPop;

        private static bool BkResolve()
        {
            if (_getPopData != null && _popMgr != null && _totalPop != null) return true;
            if (_bkTried) return false;
            _bkTried = true;
            try
            {
                var cfgT = AccessTools.TypeByName("BannerKings.BannerKingsConfig");
                var inst = cfgT != null ? AccessTools.Property(cfgT, "Instance")?.GetValue(null, null) : null;
                _popMgr = inst != null ? AccessTools.Property(cfgT, "PopulationManager")?.GetValue(inst, null) : null;
                if (_popMgr == null) return false;
                _getPopData = AccessTools.Method(_popMgr.GetType(), "GetPopData", new[] { typeof(Settlement) });
                var pdT = _getPopData != null ? _getPopData.ReturnType : null;
                if (pdT == null) return false;
                _totalPop = AccessTools.Property(pdT, "TotalPop");
                _fromSoldiers = AccessTools.Method(pdT, "UpdatePopFromSoldiers", new[] { typeof(CharacterObject), typeof(int) });
                _updateType = AccessTools.Method(pdT, "UpdatePopType");
                _typeCount = AccessTools.Method(pdT, "GetTypeCount");
                var popT = _updateType != null ? _updateType.GetParameters()[0].ParameterType : null;
                if (popT != null && popT.IsEnum) { _serfs = Enum.Parse(popT, "Serfs"); _nobles = Enum.Parse(popT, "Nobles"); }
                var helpT = AccessTools.TypeByName("BannerKings.Utils.Helpers");
                _isRetinue = helpT != null ? AccessTools.Method(helpT, "IsRetinueTroop", new[] { typeof(CharacterObject) }) : null;
                return _totalPop != null;
            }
            catch { return false; }
        }

        private static int TotalPop(object pd) { return Convert.ToInt32(_totalPop.GetValue(pd, null)); }

        private static int TypeCount(object pd, object type)
        {
            try { return _typeCount != null && type != null ? Convert.ToInt32(_typeCount.Invoke(pd, new[] { type })) : 0; }
            catch { return 0; }
        }

        private static bool IsRetinue(CharacterObject c)
        {
            try { return _isRetinue != null && (bool)_isRetinue.Invoke(null, new object[] { c }); }
            catch { return false; }
        }

        // osady z danymi BK - lista raz na dobe (ta sama sluzy do sumy ludnosci BK swiata)
        private sealed class Home { public Settlement St; public object Pd; }
        private static List<Home> _homes;
        private static Dictionary<Settlement, Home> _homeOf;
        private static int _homesDay = -1;

        private static List<Home> Homes()
        {
            int day = (int)CampaignTime.Now.ToDays;
            if (_homes != null && _homesDay == day) return _homes;
            _homesDay = day;
            _homes = new List<Home>();
            _homeOf = new Dictionary<Settlement, Home>();
            if (!BkResolve()) return _homes;
            foreach (var st in Settlement.All)
            {
                try
                {
                    if (st == null || !(st.IsTown || st.IsCastle || st.IsVillage)) continue;
                    var pd = _getPopData.Invoke(_popMgr, new object[] { st });
                    if (pd == null) continue;
                    var h = new Home { St = st, Pd = pd };
                    _homes.Add(h);
                    _homeOf[st] = h;
                }
                catch { }
            }
            return _homes;
        }

        /// <summary>Osada BK zolnierza (2.4 pkt 4): najblizsza punktowi osada z danymi BK swojej frakcji i kultury; brak -> swojej frakcji;
        /// frakcja bez osad (albo nieznana) -> swojej kultury; na koniec warownia regionu punktu. Pamiec na (miejsce, frakcja, kultura):
        /// bitwa - jedno miejsce (pamiec czyszczona na bitwe), pula - klucz regionu.</summary>
        private static Home FindHome(Vec2 pos, IFaction fac, CultureObject cul, Dictionary<string, Home> cache, string where)
        {
            string key = where + "|" + (fac != null ? fac.StringId : "-") + "|" + (cul != null ? cul.StringId : "-");
            Home h;
            if (cache.TryGetValue(key, out h)) return h;
            Home b1 = null, b2 = null, b3 = null;
            float d1 = float.MaxValue, d2 = float.MaxValue, d3 = float.MaxValue;
            foreach (var x in Homes())
            {
                var st = x.St;
                bool f = fac != null && st.MapFaction == fac;
                bool c = cul != null && st.Culture == cul;
                if (!f && !c) continue;
                float d = pos.DistanceSquared(st.GetPosition2D);
                if (f && c && d < d1) { d1 = d; b1 = x; }
                if (f && d < d2) { d2 = d; b2 = x; }
                if (c && d < d3) { d3 = d; b3 = x; }
            }
            h = b1 ?? b2 ?? b3;
            if (h == null && _homeOf != null)
            {
                var node = OutlawLaw.RegionAt(pos);
                if (node != null) _homeOf.TryGetValue(node, out h);
            }
            cache[key] = h;
            return h;
        }

        /// <summary>Poczatek rozliczenia rozbitych jednej bitwy - pamiec osad domowych jest na bitwe (miejsce bitwy).</summary>
        internal static void BeginBattle() { _battleHome.Clear(); }

        // ------------------------------------------------------------ dom uciekiniera
        private static RegionRow Row(Settlement region)
        {
            string key = region != null ? region.StringId : "?";
            RegionRow r;
            if (!_rows.TryGetValue(key, out r)) { r = new RegionRow { St = region }; _rows[key] = r; }
            return r;
        }

        internal static void NoteWoods(Settlement region, int n) { if (n > 0) Row(region).Woods += n; }
        internal static void NoteHomeless(int n, bool toPool) { if (n <= 0) return; _homeless += n; if (!toPool) _vanished += n; }
        internal static void NoteTemplateFloat(float m) { if (m > 0f) _templateFloat += m; }

        /// <summary>
        /// Jedna funkcja pochodzenia (2.4, 3.4): rozbici z bitwy, uwolnieni jency bez odbiorcy, powrot z puli. Zwraca, ilu przyjeto;
        /// reszte wolajacy oddaje do puli ("bez domu"). cat - dokad trafili (BK, wies, z szablonu).
        /// </summary>
        internal static int SendHome(MobileParty party, CharacterObject troop, int n, Vec2 pos, IFaction fac, int src, out int cat)
        {
            cat = CatVillage;
            if (troop == null || troop.IsHero || n <= 0) return 0;
            try
            {
                // 1. tabor wsi i rybacy -> hearth wlasnej wsi (dokladna odwrotnosc zdjecia przy wysylaniu taboru); ludnosci BK nie dopisujemy
                if (party != null && party.IsVillager)
                {
                    Village v = null;
                    try { v = party.HomeSettlement != null ? party.HomeSettlement.Village : null; } catch { }
                    if (v != null)
                    {
                        float h = VillagerHearthPerMan * n;
                        v.Hearth += h;
                        _toVillageMen += n; _toVillageHearth += h;
                        var row = Row(OutlawLaw.RegionFor(v.Settlement)); row.VillageMen += n; row.VillageHearth += h;
                        return n;
                    }
                    return ToCommon(n, pos, true);           // wies nieznana - najbiedniejsza wies regionu
                }
                // 2. z szablonu: karawany, zalogi, milicje, patrole (gra tworzy ich bez ubytku ludnosci) i straz karawan poza karawana
                bool garr = party != null && party.IsGarrison;
                if ((party != null && (party.IsCaravan || garr || party.IsMilitia || party.IsPatrolParty)) || troop.Occupation == Occupation.CaravanGuard)
                {
                    cat = CatTemplate;
                    _template += n; if (garr) _templateGarr += n;
                    Row(OutlawLaw.RegionAt(pos)).Template += n;
                    return n;
                }
                // 3. zolnierz i najemnik -> ludnosc BK (stamtad BK go zabral przy werbunku)
                var occ = troop.Occupation;
                if (occ == Occupation.Soldier || occ == Occupation.Mercenary)
                {
                    cat = CatBk;
                    return ToBk(troop, n, pos, fac, src);
                }
                // 4. bandyci, chlopi i reszta -> hearth regionu (symetrycznie do TakeCommoners, skad bandyta w wiekszosci pochodzi)
                return ToCommon(n, pos, false);
            }
            catch (Exception e)
            {
                _stumbleHome++;
                if (!_errHome) { _errHome = true; Log.Error("LosersFlee.SendHome", e); }
                return 0;
            }
        }

        private static int ToCommon(int n, Vec2 pos, bool villager)
        {
            var region = OutlawLaw.HearthRegionAt(pos);
            if (region == null) return 0;
            OutlawLaw.ReturnHome(region, n);
            float h = n * Settings.Current.OutlawHearthPerMan;
            if (villager) { _toVillageMen += n; _toVillageHearth += h; } else { _toCommon += n; _toCommonHearth += h; }
            var row = Row(region); row.VillageMen += n; row.VillageHearth += h;
            return n;
        }

        private static int ToBk(CharacterObject troop, int n, Vec2 pos, IFaction fac, int src)
        {
            Dictionary<string, Home> cache = _battleHome;
            string where = "";
            if (src == SrcPool)
            {
                int day = (int)CampaignTime.Now.ToDays;
                if (_poolHomeDay != day) { _poolHome.Clear(); _poolHomeDay = day; }
                cache = _poolHome;
                fac = null;                          // pula: osada BK regionu, a gdy kultura sie nie zgadza - najblizsza osada kultury jednostki
                var reg = OutlawLaw.RegionAt(pos);
                where = reg != null ? reg.StringId : ((int)pos.x + ":" + (int)pos.y);
            }
            var home = FindHome(pos, fac, troop.Culture, cache, where);
            if (home == null) return 0;
            int before = TotalPop(home.Pd);
            bool retinue = IsRetinue(troop);
            int nob0 = retinue ? 0 : TypeCount(home.Pd, _nobles);
            bool threw = false, fallback = false;
            try { _fromSoldiers.Invoke(home.Pd, new object[] { troop, n }); }
            catch { threw = true; }
            // BK nie przyjal (KeyNotFound w Manpowers, brak klasy) - zastepczo klasa chlopow (bez manpower); tylko gdy ludnosc sie
            // nie ruszyla - wyjatek w srodku dopisywania (po zmianie klasy) nie moze dac drugiego dopisania tych samych ludzi
            if (threw && _updateType != null && _serfs != null && TotalPop(home.Pd) == before)
            {
                fallback = true;
                try { _updateType.Invoke(home.Pd, new object[] { _serfs, n, false }); } catch { }
            }
            int delta = TotalPop(home.Pd) - before;
            int got = Math.Max(0, Math.Min(n, delta));
            if (!fallback && delta != n) { _bkOddCalls++; _bkOddDiff += n - delta; }       // niezalezne sprawdzenie: przyrost TotalPop = n
            if (fallback) _asSerfs += got;
            else if (!retinue && _nobles != null)
            {
                int nob1 = TypeCount(home.Pd, _nobles);
                if (nob1 > nob0) _asNobles += Math.Min(got, nob1 - nob0);                 // kaprys BK: pusta lista klas -> Nobles (bez poprawiania)
            }
            _toBk += got;
            if (got > 0) Row(OutlawLaw.RegionFor(home.St)).Bk += got;
            return got;
        }

        // ------------------------------------------------------------ plan bitwy
        private sealed class TP { public CharacterObject T; public int D, R, M, W, Wb, Wbw, K, J, F; }
        private sealed class LP { public MapEventParty Mep; public MobileParty Mp; public int Kind; public float P; public readonly List<TP> Types = new List<TP>(); public int N, K, J, F, D0; public double J0; }
        private sealed class WP { public MapEventParty Mep; public readonly List<KeyValuePair<CharacterObject, int>> X = new List<KeyValuePair<CharacterObject, int>>(); public int N, D, Rev; }
        private sealed class FP { public TroopRoster Roster; public CharacterObject T; public int N; }
        private sealed class BP
        {
            public MapEvent Me; public MapEventSide Win, Lose;
            public readonly List<LP> Losers = new List<LP>();
            public readonly List<WP> Winners = new List<WP>();
            public readonly List<FP> Freed = new List<FP>();
            public readonly List<TroopRoster> Prisons = new List<TroopRoster>();
            public int PrisonBefore, SumJ, FreedN;
            public float C, T, O, Q, F, P; public string Center; public int TrapPts;
            public int LMen, WMen, LRegs, WRegs; public float LMount, WMount, LTier, WTier;
            public int LoseDeadAll0, LoseDeadAll, WinDeadAll0, WinDeadAll;
            public int WinN, WinD0, WinRev;        // zwyciezcy (szeregowi): ludzie, polegli wedlug gry, z poleglych ranni
            public int G_W, G_R, G_Stand;         // gra przed zmiana: ranni, rozbici, stali (szeregowi przegranych)
        }

        private static BP _cur;

        private static void Collect(Dictionary<CharacterObject, TP> map, TroopRoster roster, int which)
        {
            if (roster == null) return;
            for (int i = 0; i < roster.Count; i++)
            {
                var e = roster.GetElementCopyAtIndex(i);
                var c = e.Character;
                if (c == null || c.IsHero) continue;
                if (e.Number == 0 && e.WoundedNumber == 0) continue;
                TP tp;
                if (!map.TryGetValue(c, out tp)) { tp = new TP { T = c }; map[c] = tp; }
                if (which == 0) { tp.M += e.Number; tp.W += e.WoundedNumber; }
                else if (which == 1) tp.D += e.Number;
                else if (which == 2) tp.R += e.Number;
                else { tp.Wb += e.Number; tp.Wbw += e.WoundedNumber; }
            }
        }

        private static void AddComp(TroopRoster roster, ref int regs, ref int mounted, ref double tiers)
        {
            if (roster == null) return;
            for (int i = 0; i < roster.Count; i++)
            {
                var e = roster.GetElementCopyAtIndex(i);
                var c = e.Character;
                if (c == null || c.IsHero || e.Number <= 0) continue;
                regs += e.Number;
                if (c.IsMounted) mounted += e.Number;
                tiers += (double)c.Tier * e.Number;
            }
        }

        /// <summary>Sklad strony: ludzie = suma HealthyManCountAtStart; szeregowi = w partii (z rannymi) + polegli + rozbici.</summary>
        private static void Compose(MapEventSide side, out int men, out int regs, out float mount, out float tier)
        {
            men = 0; regs = 0; int mnt = 0; double ts = 0;
            for (int i = 0; i < side.Parties.Count; i++)
            {
                var mep = side.Parties[i];
                if (mep == null || mep.Party == null) continue;
                men += mep.HealthyManCountAtStart;
                AddComp(mep.Party.MemberRoster, ref regs, ref mnt, ref ts);
                AddComp(mep.DiedInBattle, ref regs, ref mnt, ref ts);
                AddComp(mep.RoutedInBattle, ref regs, ref mnt, ref ts);
            }
            mount = regs > 0 ? (float)mnt / regs : 0f;
            tier = regs > 0 ? (float)(ts / regs) : 0f;
        }

        private static bool IsTrap(TerrainType t)
        {
            switch (t)
            {
                case TerrainType.Fording: case TerrainType.Lake: case TerrainType.River: case TerrainType.Swamp: case TerrainType.Bridge:
                case TerrainType.Beach: case TerrainType.NonNavigableRiver: case TerrainType.UnderBridge: case TerrainType.Canyon: case TerrainType.Cliff:
                    return true;
            }
            return false;
        }

        private static readonly Vec2[] Ring = BuildRing();
        private static Vec2[] BuildRing()
        {
            var r = new Vec2[8];
            for (int i = 0; i < 8; i++)
            {
                float rad = i < 4 ? TerrainR1 : TerrainR2;
                double a = (i < 4 ? i * 90.0 : 45.0 + (i - 4) * 90.0) * Math.PI / 180.0;
                r[i] = new Vec2((float)(rad * Math.Cos(a)), (float)(rad * Math.Sin(a)));
            }
            return r;
        }

        /// <summary>T - pulapka terenu: rzeka, brod, jezioro, bagno, most, plaza, wawoz, urwisko pod bitwa albo tuz obok (9 punktow).</summary>
        private static float Terrain(MapEvent me, out string center, out int trapPts)
        {
            center = "?"; trapPts = 0;
            bool trap = false;
            try { var tc = me.EventTerrainType; center = tc.ToString(); if (IsTrap(tc)) trap = true; } catch { }
            var scene = Campaign.Current != null ? Campaign.Current.MapSceneWrapper : null;
            if (scene != null)
            {
                for (int i = 0; i < Ring.Length; i++)
                {
                    try
                    {
                        var p = me.Position + Ring[i];
                        if (IsTrap(scene.GetTerrainTypeAtPosition(in p))) trapPts++;
                    }
                    catch { }
                }
            }
            if (trapPts > 0) trap = true;
            return trap ? 1f : 0f;
        }

        private static float Clamp01(float v) { return v < 0f ? 0f : (v > 1f ? 1f : v); }

        private static bool AnyUndead(MapEvent me)
        {
            foreach (var side in new[] { me.AttackerSide, me.DefenderSide })
            {
                if (side == null) continue;
                for (int i = 0; i < side.Parties.Count; i++)
                {
                    var mep = side.Parties[i];
                    var mp = mep != null && mep.Party != null ? mep.Party.MobileParty : null;
                    if (mp != null && Undead.Party(mp)) return true;
                }
            }
            return false;
        }

        /// <summary>Uwolnieni jency bez odbiorcy w bitwach poza H3 - tylko licznik (U2).</summary>
        private static void CountFreedOutside(MapEvent me)
        {
            try
            {
                var lose = me.GetMapEventSide(me.DefeatedSide); var win = me.GetMapEventSide(me.WinningSide);
                if (lose == null || win == null) return;
                var model = Campaign.Current.Models.BattleRewardModel;
                for (int i = 0; i < lose.Parties.Count; i++)
                {
                    var pr = lose.Parties[i] != null && lose.Parties[i].Party != null ? lose.Parties[i].Party.PrisonRoster : null;
                    if (pr == null || pr.TotalRegulars <= 0) continue;
                    for (int k = 0; k < pr.Count; k++)
                    {
                        var e = pr.GetElementCopyAtIndex(k);
                        if (e.Character == null || e.Character.IsHero || e.Number <= 0) continue;
                        var ch = model.GetLootPrisonerChances(win.Parties, e);
                        if (ch == null || ch.Count == 0) _freedOutside += e.Number;
                    }
                }
            }
            catch { }
        }

        // ------------------------------------------------------------ prefiks / postfiks
        public static void ResultsPrefix(MapEvent __instance)
        {
            _cur = null;
            if (!On || __instance == null) return;
            var sw = Stopwatch.StartNew();
            try
            {
                var me = __instance;
                if (_appliedRef(me)) return;                                  // raz na bitwe
                var bs = me.BattleState;
                if (bs != BattleState.AttackerVictory && bs != BattleState.DefenderVictory) return;
                if (me.IsPlayerMapEvent) { _skipPlayer++; return; }           // bitwy gracza: walka decyduje (linia "pominieta (gracz)" przy MapEventEnded)
                if (!me.IsFieldBattle || me.IsNavalMapEvent) { _skipNotField++; CountFreedOutside(me); return; }
                if (me.RetreatingSide != BattleSideEnum.None) { _skipRetreat++; CountFreedOutside(me); return; }
                var lose = me.GetMapEventSide(me.DefeatedSide); var win = me.GetMapEventSide(me.WinningSide);
                if (lose == null || win == null) return;
                if (_surrRef(lose)) { _skipSurr++; CountFreedOutside(me); return; }
                if (AnyUndead(me)) { _skipUndead++; CountFreedOutside(me); return; }

                var plan = Plan(me, win, lose);           // faza 1: policz i sprawdz
                if (plan == null) return;
                Apply(plan);                              // faza 2: same AddToCounts na sprawdzonych liczbach
                // migawka lochow zwyciezcow dla postfiksu (zaloga i milicja tej samej osady maja wspolny loch - bez dubli)
                int pb = 0;
                foreach (var r in plan.Prisons) pb += r.TotalRegulars;
                plan.PrisonBefore = pb;
                _cur = plan;
            }
            catch (Exception e)
            {
                _stumbleCalc++;
                if (!_errCalc) { _errCalc = true; Log.Error("LosersFlee.Prefix", e); }
            }
            finally { _ticks += sw.ElapsedTicks; }
        }

        private static BP Plan(MapEvent me, MapEventSide win, MapEventSide lose)
        {
            var s = Settings.Current;
            var model = Campaign.Current.Models.BattleRewardModel;
            var bp = new BP { Me = me, Win = win, Lose = lose };

            // 1. opis stron, teren -> C, T, O, Q, F, p (kazdy mianownik zero -> skladnik 0)
            Compose(lose, out bp.LMen, out bp.LRegs, out bp.LMount, out bp.LTier);
            Compose(win, out bp.WMen, out bp.WRegs, out bp.WMount, out bp.WTier);
            bool both = bp.LRegs > 0 && bp.WRegs > 0;
            bp.C = both ? Math.Max(0f, bp.WMount - bp.LMount) : 0f;
            bp.F = both ? Math.Max(0f, bp.LMount - bp.WMount) : 0f;
            bp.O = bp.LMen > 0 && bp.WMen > 0 ? Clamp01(((float)bp.WMen / bp.LMen - 1.5f) / 1.5f) : 0f;
            bp.Q = both ? Clamp01((bp.WTier - bp.LTier) / 3f) : 0f;
            bp.T = Terrain(me, out bp.Center, out bp.TrapPts);
            float p = PBase + WC * bp.C + WT * bp.T + WO * bp.O + WQ * bp.Q - WF * bp.F * (1f - bp.T);
            bp.P = p < PMin ? PMin : (p > PMax ? PMax : p);
            if (float.IsNaN(bp.P)) bp.P = PBase;

            // 2. kto moze brac jencow (raz na bitwe), kultura zwyciezcy
            MBList<KeyValuePair<MapEventParty, float>> wch, hch;
            model.GetCaptureMemberChancesForWinnerParties(me, win.Parties, out wch, out hch);
            bool canCapture = wch != null && wch.Count > 0;
            bool slaveWinner = false;
            try { var wf = win.MapFaction; slaveWinner = wf != null && SlaveCulture(wf.Culture); } catch { }
            float vet = Clamp01(s.LoserCaptiveVeteranPercent / 100f), com = Clamp01(s.LoserCaptiveCommonPercent / 100f);
            float nonComb = Clamp01(s.NonCombatantDeathPercent / 100f);

            // 3. plan przegranych - blad w liczeniu albo w sprawdzeniu = cala partia po staremu (nic jeszcze nie zmieniono)
            for (int i = 0; i < lose.Parties.Count; i++)
            {
                var mep = lose.Parties[i];
                try
                {
                    bp.LoseDeadAll0 += mep != null && mep.DiedInBattle != null ? mep.DiedInBattle.TotalManCount : 0;
                    if (mep == null || mep.Party == null) continue;
                    var mp = mep.Party.MobileParty;
                    if (mp == null || !mp.IsActive) continue;
                    var lp = new LP { Mep = mep, Mp = mp, Kind = KindOf(mp) };
                    lp.P = lp.Kind == KVillager ? nonComb : bp.P;
                    var map = new Dictionary<CharacterObject, TP>();
                    Collect(map, mep.Party.MemberRoster, 0);
                    Collect(map, mep.DiedInBattle, 1);
                    Collect(map, mep.RoutedInBattle, 2);
                    Collect(map, mep.WoundedInBattle, 3);
                    foreach (var tp in map.Values)
                    {
                        int n = tp.D + tp.R + tp.M;
                        if (n <= 0) continue;
                        tp.K = Math.Max(0, Math.Min(n, MBRandom.RoundRandomized(lp.P * n)));
                        int sv = n - tp.K;
                        bool gameTakes = canCapture && model.CanTroopBeTakenPrisoner(tp.T);
                        bool take = gameTakes && (lp.Kind != KVillager || slaveWinner);
                        float c = tp.T.Tier >= VeteranTier ? vet : com;
                        tp.J = take ? Math.Max(0, Math.Min(sv, MBRandom.RoundRandomized(sv * c))) : 0;
                        tp.F = sv - tp.J;
                        if (tp.K < 0 || tp.J < 0 || tp.F < 0 || tp.K + tp.J + tp.F != n || tp.J > sv || tp.W < 0 || tp.W > tp.M || tp.Wbw > tp.Wb)
                            throw new InvalidOperationException("plan H3 niespojny: " + tp.T.StringId + " d" + tp.D + " r" + tp.R + " m" + tp.M + " w" + tp.W + " k" + tp.K + " j" + tp.J + " f" + tp.F);
                        lp.Types.Add(tp);
                        lp.N += n; lp.K += tp.K; lp.J += tp.J; lp.F += tp.F; lp.D0 += tp.D;
                        if (gameTakes) lp.J0 += tp.W + 0.25 * (tp.M - tp.W);
                        bp.G_W += tp.W; bp.G_R += tp.R; bp.G_Stand += tp.M - tp.W;
                    }
                    if (lp.Types.Count > 0) bp.Losers.Add(lp);
                }
                catch (Exception e)
                {
                    _stumbleCalc++;
                    if (!_errCalc) { _errCalc = true; Log.Error("LosersFlee.Plan (partia po staremu)", e); }
                }
            }

            // 3a. uwolnieni jency pokonanych, ktorych nikt nie wezmie (gra zdjelaby ich z lochu w nicosc) - oddajemy wedlug pochodzenia
            for (int i = 0; i < lose.Parties.Count; i++)
            {
                try
                {
                    var mep = lose.Parties[i];
                    var pr = mep != null && mep.Party != null ? mep.Party.PrisonRoster : null;
                    if (pr == null || pr.TotalRegulars <= 0) continue;
                    for (int k = 0; k < pr.Count; k++)
                    {
                        var e = pr.GetElementCopyAtIndex(k);
                        if (e.Character == null || e.Character.IsHero || e.Number <= 0) continue;
                        var ch = model.GetLootPrisonerChances(win.Parties, e);
                        if (ch != null && ch.Count > 0) continue;               // gra wcieli ich do zwyciezcy jako ludzi
                        bp.Freed.Add(new FP { Roster = pr, T = e.Character, N = e.Number });
                    }
                }
                catch (Exception e)
                {
                    _stumbleCalc++;
                    if (!_errCalc) { _errCalc = true; Log.Error("LosersFlee.Plan (uwolnieni)", e); }
                }
            }

            // 5. zwyciezcy: zabitych najwyzej WinnerDeathCapPercent, reszta poleglych to ranni (rozklad proporcjonalny, reszta do najwiekszych)
            float cap = Clamp01(s.WinnerDeathCapPercent / 100f);
            for (int i = 0; i < win.Parties.Count; i++)
            {
                var mep = win.Parties[i];
                try
                {
                    bp.WinDeadAll0 += mep != null && mep.DiedInBattle != null ? mep.DiedInBattle.TotalManCount : 0;
                    if (mep == null || mep.Party == null) continue;
                    try { var r = mep.RosterToReceiveLootPrisoners; if (r != null && !bp.Prisons.Contains(r)) bp.Prisons.Add(r); } catch { }
                    if (!mep.Party.IsActive || mep.Party.MemberRoster == null || mep.DiedInBattle == null) continue;
                    var wp = new WP { Mep = mep };
                    wp.D = mep.DiedInBattle.TotalRegulars;
                    wp.N = mep.Party.MemberRoster.TotalRegulars + wp.D + (mep.RoutedInBattle != null ? mep.RoutedInBattle.TotalRegulars : 0);
                    bp.WinN += wp.N; bp.WinD0 += wp.D;
                    int lim = Math.Max(0, MBRandom.RoundRandomized(cap * wp.N));
                    if (wp.D <= lim) continue;
                    int x = wp.D - lim;
                    var died = mep.DiedInBattle;
                    var types = new List<KeyValuePair<CharacterObject, int>>();
                    int dsum = 0;
                    for (int k = 0; k < died.Count; k++)
                    {
                        var e = died.GetElementCopyAtIndex(k);
                        if (e.Character == null || e.Character.IsHero || e.Number <= 0) continue;
                        types.Add(new KeyValuePair<CharacterObject, int>(e.Character, e.Number)); dsum += e.Number;
                    }
                    if (dsum != wp.D || dsum <= 0) throw new InvalidOperationException("polegli zwyciezcy niespojni: " + dsum + " / " + wp.D);
                    var xs = new int[types.Count];
                    int given = 0;
                    for (int k = 0; k < types.Count; k++) { xs[k] = (int)Math.Floor((double)x * types[k].Value / dsum); given += xs[k]; }
                    var order = new List<int>();
                    for (int k = 0; k < types.Count; k++) order.Add(k);
                    order.Sort((a, b) => (types[b].Value - xs[b]).CompareTo(types[a].Value - xs[a]));
                    for (int pass = 0; given < x && pass < 2; pass++)
                        foreach (var k in order) { if (given >= x) break; if (xs[k] < types[k].Value) { xs[k]++; given++; } }
                    int chk = 0;
                    for (int k = 0; k < types.Count; k++)
                    {
                        if (xs[k] < 0 || xs[k] > types[k].Value) throw new InvalidOperationException("rozklad rannych zwyciezcy niespojny");
                        chk += xs[k];
                        if (xs[k] > 0) wp.X.Add(new KeyValuePair<CharacterObject, int>(types[k].Key, xs[k]));
                    }
                    if (chk != x) throw new InvalidOperationException("rozklad rannych zwyciezcy: " + chk + " z " + x);
                    wp.Rev = x;
                    bp.Winners.Add(wp);
                }
                catch (Exception e)
                {
                    _stumbleCalc++;
                    if (!_errCalc) { _errCalc = true; Log.Error("LosersFlee.Plan (zwyciezca po staremu)", e); }
                }
            }
            return bp;
        }

        private static void Apply(BP bp)
        {
            var me = bp.Me;
            var pos = me.Position.ToVec2();
            // przegrani: w partii zostaje dokladnie j rannych (gra wezmie ich do niewoli), reszta - zabici albo rozbici
            foreach (var lp in bp.Losers)
            {
                try
                {
                    var mep = lp.Mep; var member = mep.Party.MemberRoster;
                    foreach (var tp in lp.Types)
                    {
                        int dd = tp.K - tp.D;
                        if (dd != 0) mep.DiedInBattle.AddToCounts(tp.T, dd);
                        int wn = tp.J - tp.Wb, ww = tp.J - tp.Wbw;
                        if (wn != 0 || ww != 0) mep.WoundedInBattle.AddToCounts(tp.T, wn, false, ww);
                        int rr = tp.F - tp.R;
                        if (rr != 0) mep.RoutedInBattle.AddToCounts(tp.T, rr);
                        int mn = tp.J - tp.M, mw = tp.J - tp.W;
                        if (mn != 0 || mw != 0) member.AddToCounts(tp.T, mn, false, mw);
                    }
                    _controlDiff += lp.K + lp.J + lp.F - lp.N;          // kontrola kodu (z definicji 0); prawdziwe sprawdzenie - postfiks
                    bp.SumJ += lp.J;
                    bp.LoseDeadAll += lp.K - lp.D0;
                    _kMen[lp.Kind] += lp.N; _kDead[lp.Kind] += lp.K; _kCapt[lp.Kind] += lp.J; _kRouted[lp.Kind] += lp.F; _kDead0[lp.Kind] += lp.D0;
                    _lMen += lp.N; _lDead += lp.K; _lDead0 += lp.D0; _lCapt += lp.J; _lRouted += lp.F; _lCapt0 += lp.J0;
                }
                catch (Exception e)
                {
                    _stumbleApply++;
                    if (!_errApply) { _errApply = true; Log.Error("LosersFlee.Apply (przegrany)", e); }
                }
            }
            bp.LoseDeadAll += bp.LoseDeadAll0;
            // zwyciezcy: polegli ponad limit wracaja do partii ranni
            bp.WinDeadAll = bp.WinDeadAll0;
            foreach (var wp in bp.Winners)
            {
                try
                {
                    var mep = wp.Mep;
                    foreach (var kv in wp.X)
                    {
                        mep.DiedInBattle.AddToCounts(kv.Key, -kv.Value);
                        mep.WoundedInBattle.AddToCounts(kv.Key, kv.Value, false, kv.Value);
                        mep.Party.MemberRoster.AddToCounts(kv.Key, kv.Value, false, kv.Value);
                        bp.WinRev += kv.Value; bp.WinDeadAll -= kv.Value;
                    }
                }
                catch (Exception e)
                {
                    _stumbleApply++;
                    if (!_errApply) { _errApply = true; Log.Error("LosersFlee.Apply (zwyciezca)", e); }
                }
            }
            // uwolnieni jency bez odbiorcy: z lochu sami i do domu wedlug pochodzenia (bez domu - do puli regionu bitwy)
            if (bp.Freed.Count > 0)
            {
                _battleHome.Clear();
                var region = OutlawLaw.PoolRegionAt(pos);
                bool law = OutlawLaw.On && region != null;
                foreach (var fp in bp.Freed)
                {
                    try
                    {
                        int n = Math.Min(fp.N, fp.Roster.GetTroopCount(fp.T));
                        if (n <= 0) continue;
                        fp.Roster.AddToCounts(fp.T, -n);
                        _freed += n; bp.FreedN += n;
                        Row(OutlawLaw.RegionAt(pos)).Freed += n;
                        int cat;
                        int got = SendHome(null, fp.T, n, pos, null, SrcFreed, out cat);
                        int left = n - got;
                        if (left > 0)
                        {
                            if (law) { OutlawLaw.PoolAdd(region, fp.T, left); NoteWoods(region, left); }
                            NoteHomeless(left, law);
                        }
                    }
                    catch (Exception e)
                    {
                        _stumbleApply++;
                        if (!_errApply) { _errApply = true; Log.Error("LosersFlee.Apply (uwolnieni)", e); }
                    }
                }
            }
            // liczniki bitwy
            _wMen += bp.WinN; _wDead0 += bp.WinD0; _wDead += bp.WinD0 - bp.WinRev; _wRevived += bp.WinRev;
            _battles++;
            _sumP += bp.P; _sumC += bp.C; _sumT += bp.T; _sumO += bp.O; _sumQ += bp.Q; _sumF += bp.F;
            if (bp.T > 0.5f) _trapBattles++;
            _sumJ += bp.SumJ;
            int minSide = Math.Max(1, Settings.Current.BattleRealMinSide);
            if (bp.LMen >= minSide && bp.WMen >= minSide && Math.Min(bp.LMen, bp.WMen) * 4 >= Math.Max(bp.LMen, bp.WMen))
            {
                _realN++;
                _realLose += 100.0 * bp.LoseDeadAll / bp.LMen; _realLose0 += 100.0 * bp.LoseDeadAll0 / bp.LMen;
                _realWin += 100.0 * bp.WinDeadAll / bp.WMen; _realWin0 += 100.0 * bp.WinDeadAll0 / bp.WMen;
            }
        }

        public static void ResultsPostfix(MapEvent __instance)
        {
            var bp = _cur;
            _cur = null;
            if (bp == null || bp.Me != __instance) return;
            var sw = Stopwatch.StartNew();
            try
            {
                // niezalezne sprawdzenie: przyrost szeregowych w lochach zwyciezcow = suma j (uwolnieni jency pokonanych ida do zwyciezcow
                // jako LUDZIE, wiec sie tu nie mieszaja); roznica = jency zgubieni przez gre (null w losowaniu)
                int after = 0;
                foreach (var r in bp.Prisons) after += r.TotalRegulars;
                int got = after - bp.PrisonBefore;
                _taken += got; _lostByGame += bp.SumJ - got; _checked++;
                // po pojmaniu w kazdej partii przegranej zero szeregowych
                for (int i = 0; i < bp.Lose.Parties.Count; i++)
                {
                    var mep = bp.Lose.Parties[i];
                    if (mep != null && mep.Party != null && mep.Party.MemberRoster != null && mep.Party.MemberRoster.TotalRegulars > 0) _losersLeft++;
                }
                BattleLine(bp, got);
            }
            catch (Exception e)
            {
                _stumblePost++;
                if (!_errPost) { _errPost = true; Log.Error("LosersFlee.Postfix", e); }
            }
            finally { _ticks += sw.ElapsedTicks; }
        }

        public static void RecruitPrisonersPostfix(MobileParty __0, int __2)
        {
            try { if (__0 != null && __2 > 0 && __0.IsLordParty && __0 != MobileParty.MainParty) _recruitedPrisoners += __2; } catch { }
        }

        // ------------------------------------------------------------ logi
        private static string F2(float v) { return v.ToString("0.00", CultureInfo.InvariantCulture); }
        private static string Pc(double x, double of) { return of > 0 ? (100.0 * x / of).ToString("0.#", CultureInfo.InvariantCulture) + "%" : "-"; }

        private static string Who(MapEventSide side)
        {
            try
            {
                var lp = side.LeaderParty;
                string leader = lp != null ? (lp.LeaderHero != null ? lp.LeaderHero.Name.ToString() : lp.Name.ToString()) : "?";
                string fac = side.MapFaction != null ? side.MapFaction.Name.ToString() : "?";
                return leader + " (" + fac + (side.Parties.Count > 1 ? ", partii " + side.Parties.Count : "") + ")";
            }
            catch { return "?"; }
        }

        private static string Where(MapEvent me)
        {
            try
            {
                if (me.MapEventSettlement != null) return " pod " + me.MapEventSettlement.Name;
                var r = OutlawLaw.RegionAt(me.Position.ToVec2());
                return r != null ? " w regionie " + r.Name : "";
            }
            catch { return ""; }
        }

        private static void BattleLine(BP bp, int got)
        {
            int min = Math.Max(0, Settings.Current.BattleChronicleMinMen);
            if (bp.LMen + bp.WMen < min) return;                      // male potyczki tylko liczone
            int n = 0, k = 0, j = 0, f = 0, d0 = 0;
            foreach (var lp in bp.Losers) { n += lp.N; k += lp.K; j += lp.J; f += lp.F; d0 += lp.D0; }
            int wn = bp.WinN, wd0 = bp.WinD0, rev = bp.WinRev, wdNow = wd0 - rev;
            var sb = new StringBuilder();
            sb.Append("Bitwa: H3 dzien ").Append((int)CampaignTime.Now.ToDays).Append(" - ").Append(bp.Me.EventType).Append(Where(bp.Me)).Append(": przegrany ")
              .Append(Who(bp.Lose)).Append(' ').Append(bp.LMen).Append(" ludzi (konni ").Append(Pc(bp.LMount, 1)).Append(", tier ").Append(bp.LTier.ToString("0.0", CultureInfo.InvariantCulture))
              .Append(") vs zwyciezca ").Append(Who(bp.Win)).Append(' ').Append(bp.WMen).Append(" ludzi (konni ").Append(Pc(bp.WMount, 1)).Append(", tier ")
              .Append(bp.WTier.ToString("0.0", CultureInfo.InvariantCulture)).Append(") | p = 0.15 + 0.35xC ").Append(F2(bp.C)).Append(" + 0.15xT ").Append(bp.T > 0.5f ? "1" : "0")
              .Append(" (teren: srodek ").Append(bp.Center).Append(", woda ").Append(bp.TrapPts).Append("/8) + 0.15xO ").Append(F2(bp.O)).Append(" + 0.20xQ ").Append(F2(bp.Q))
              .Append(" - 0.10xF ").Append(F2(bp.F)).Append(" = ").Append(Pc(bp.P, 1))
              .Append(" | gra: zabici ").Append(d0).Append(" (").Append(Pc(d0, n)).Append("), ranni ").Append(bp.G_W).Append(", rozbici ").Append(bp.G_R).Append(", stali ").Append(bp.G_Stand)
              .Append(" | H3: zabici ").Append(k).Append(" (").Append(Pc(k, n)).Append("), jency ").Append(j).Append(" (").Append(Pc(j, n)).Append("), rozbici ").Append(f).Append(" (").Append(Pc(f, n)).Append(')')
              .Append(" | zwyciezca: zabici gry ").Append(wd0).Append(" (").Append(Pc(wd0, wn)).Append(") -> ").Append(wdNow).Append(" (").Append(Pc(wdNow, wn)).Append("), +").Append(rev).Append(" rannych")
              .Append(" | jency wzieci ").Append(got).Append(" (zgubieni przez gre ").Append(bp.SumJ - got).Append("), uwolnieni bez odbiorcy ").Append(bp.FreedN).Append('.');
            Log.Info(sb.ToString());
        }

        /// <summary>Bitwa z graczem: IsPlayerMapEvent albo partia gracza wsrod stron (przy MapEventEnded partia gracza jest jeszcze w bitwie).</summary>
        internal static bool PlayerIn(MapEvent me)
        {
            try
            {
                if (me.IsPlayerMapEvent) return true;
                foreach (var side in new[] { me.AttackerSide, me.DefenderSide })
                {
                    if (side == null) continue;
                    for (int i = 0; i < side.Parties.Count; i++) if (side.Parties[i] != null && side.Parties[i].Party == PartyBase.MainParty) return true;
                }
            }
            catch { }
            return false;
        }

        /// <summary>Bitwa gracza (misja albo symulacja): H3 jej nie rusza - jedna linia z liczbami gry i podzialem rozbitych (3.2).</summary>
        internal static void PlayerLine(MapEvent me, int pool, int bk, int village, int template, int homeless, bool toPool)
        {
            try
            {
                var sb = new StringBuilder();
                sb.Append("Bitwa: H3 pominieta (gracz) dzien ").Append((int)CampaignTime.Now.ToDays).Append(" - ").Append(me.EventType).Append(Where(me)).Append(": ")
                  .Append(me.IsPlayerSimulation ? "symulacja" : "pole").Append(" | gra: ");
                bool first = true;
                foreach (var side in new[] { me.AttackerSide, me.DefenderSide })
                {
                    if (side == null) continue;
                    int dead = 0, wounded = 0, routed = 0;
                    for (int i = 0; i < side.Parties.Count; i++)
                    {
                        var p = side.Parties[i];
                        if (p == null) continue;
                        if (p.DiedInBattle != null) dead += p.DiedInBattle.TotalManCount;
                        if (p.WoundedInBattle != null) wounded += p.WoundedInBattle.TotalManCount;
                        if (p.RoutedInBattle != null) routed += p.RoutedInBattle.TotalManCount;
                    }
                    if (!first) sb.Append("; ");
                    first = false;
                    sb.Append(side == me.AttackerSide ? "atakujacy" : "obronca").Append(" zabici ").Append(dead).Append(", ranni ").Append(wounded).Append(", rozbici ").Append(routed);
                }
                sb.Append(" | rozbici: do puli ").Append(pool).Append(", do domu ").Append(bk + village).Append(" (BK ").Append(bk).Append(", wies ").Append(village)
                  .Append("), z szablonu ").Append(template).Append(", bez domu ").Append(homeless).Append(homeless > 0 ? (toPool ? " (do puli)" : " (znikneli - prawo wyrzutkow wylaczone)") : "").Append('.');
                Log.Info(sb.ToString());
            }
            catch { }
        }

        /// <summary>Suma ludnosci BK swiata (osady z danymi BK) - do testu A/B; -1 gdy BK nie ma.</summary>
        private static long WorldBkPop()
        {
            var hs = Homes();
            if (hs.Count == 0 || _totalPop == null) return -1;
            long sum = 0;
            foreach (var h in hs) { try { sum += TotalPop(h.Pd); } catch { } }
            return sum;
        }

        private static string Owners()
        {
            var parts = new List<string>();
            Action<string, MethodBase> add = (label, m) =>
            {
                try
                {
                    if (m == null) { parts.Add(label + ": brak metody"); return; }
                    var info = Harmony.GetPatchInfo(m);
                    var owners = new List<string>();
                    if (info != null) foreach (var o in info.Owners) if (!owners.Contains(o)) owners.Add(o);
                    parts.Add(label + ": " + (owners.Count > 0 ? string.Join(", ", owners.ToArray()) : "bez latek"));
                }
                catch (Exception e) { parts.Add(label + ": blad (" + e.Message + ")"); }
            };
            add("CalculateAndCommitMapEventResults", AccessTools.Method(typeof(MapEvent), "CalculateAndCommitMapEventResults"));
            add("CaptureDefeatedPartyMembers", AccessTools.Method(typeof(MapEvent), "CaptureDefeatedPartyMembers"));
            add("LootDefeatedPartyPrisoners", AccessTools.Method(typeof(MapEvent), "LootDefeatedPartyPrisoners"));
            add("DefaultPartyHealingModel.GetSurvivalChance", AccessTools.Method(typeof(DefaultPartyHealingModel), "GetSurvivalChance"));
            return string.Join("; ", parts.ToArray());
        }

        internal static void Daily()
        {
            var sw = Stopwatch.StartNew();
            try
            {
                if (Campaign.Current == null) return;
                if (!_ownersLogged)
                {
                    // druga lista wlascicieli po starcie kampanii - czesc modow lata dopiero przy starcie gry (oczekiwani: Armoury, ROT, RBM)
                    _ownersLogged = true;
                    Log.Info("LosersFlee (H3): wlasciciele latek po starcie kampanii - " + Owners() + ".");
                }
                int day = (int)CampaignTime.Now.ToDays;
                long world = -1;
                try { world = WorldBkPop(); } catch { }
                string worldTxt = world >= 0 ? world + (_lastWorldPop >= 0 ? " (zmiana " + (world - _lastWorldPop >= 0 ? "+" : "") + (world - _lastWorldPop) + ")" : "") : "brak BK";
                if (world >= 0) _lastWorldPop = world;
                var ci = CultureInfo.InvariantCulture;
                var sb = new StringBuilder();
                sb.Append("Przegrani (H3): dzien ").Append(day);
                if (!On)
                {
                    sb.Append(" - WYLACZONE (gra jak dzis) | ludnosc BK swiata ").Append(worldTxt).Append(" | wcieleni jency AI (partie rodow) ").Append(_recruitedPrisoners)
                      .Append(" | czas ").Append(((_ticks + sw.ElapsedTicks) * 1000.0 / Stopwatch.Frequency).ToString("0.0", ci)).Append(" ms.");
                    Log.Info(sb.ToString());
                    return;
                }
                sb.Append(" - bitew objetych ").Append(_battles).Append(" (pominiete: z graczem ").Append(_skipPlayer).Append(", nie w polu ").Append(_skipNotField)
                  .Append(" [oblezenie, rabunek, wypad, morze], odwrot ").Append(_skipRetreat).Append(", poddanie ").Append(_skipSurr).Append(", z Innymi ").Append(_skipUndead).Append(')');
                sb.Append(" | przegrani ").Append(_lMen).Append(" ludzi: zabici ").Append(_lDead).Append(" (").Append(Pc(_lDead, _lMen)).Append("; gra dalaby ").Append(_lDead0).Append(", ").Append(Pc(_lDead0, _lMen))
                  .Append("), jency ").Append(_lCapt).Append(" (").Append(Pc(_lCapt, _lMen)).Append("; gra ok. ").Append((int)Math.Round(_lCapt0)).Append("), rozbici ").Append(_lRouted).Append(" (").Append(Pc(_lRouted, _lMen)).Append(") -");
                for (int i = 0; i < Kinds; i++)
                {
                    if (_kMen[i] <= 0) continue;
                    sb.Append(' ').Append(KName[i]).Append(' ').Append(_kMen[i]).Append(": zabici ").Append(Pc(_kDead[i], _kMen[i])).Append(" (gra ").Append(Pc(_kDead0[i], _kMen[i]))
                      .Append("), jency ").Append(Pc(_kCapt[i], _kMen[i])).Append(", rozbici ").Append(Pc(_kRouted[i], _kMen[i])).Append(';');
                }
                sb.Append(" | zwyciezcy ").Append(_wMen).Append(" ludzi: zabici ").Append(_wDead).Append(" (").Append(Pc(_wDead, _wMen)).Append("; gra ").Append(Pc(_wDead0, _wMen))
                  .Append("), z poleglych ranni +").Append(_wRevived);
                sb.Append(" | prawdziwe bitwy (").Append(_realN).Append("; obie >= ").Append(Settings.Current.BattleRealMinSide).Append(", do 1:4; srednia na bitwe jak w \"Bitwy:\"): przegrani zabici ")
                  .Append(_realN > 0 ? (_realLose / _realN).ToString("0.#", ci) + "%" : "-").Append(" (gra ").Append(_realN > 0 ? (_realLose0 / _realN).ToString("0.#", ci) + "%" : "-")
                  .Append("), zwyciezcy ").Append(_realN > 0 ? (_realWin / _realN).ToString("0.#", ci) + "%" : "-").Append(" (gra ").Append(_realN > 0 ? (_realWin0 / _realN).ToString("0.#", ci) + "%" : "-")
                  .Append(") - historia 15-40% / 1-5%");
                if (_battles > 0)
                    sb.Append(" | srednie p ").Append(F2((float)(_sumP / _battles))).Append(": C ").Append(F2((float)(_sumC / _battles))).Append(", T ").Append(F2((float)(_sumT / _battles)))
                      .Append(", O ").Append(F2((float)(_sumO / _battles))).Append(", Q ").Append(F2((float)(_sumQ / _battles))).Append(", F ").Append(F2((float)(_sumF / _battles)))
                      .Append("; bitew z T = 1: ").Append(Pc(_trapBattles, _battles));
                sb.Append(" | sprawdzenia: jency wzieci ").Append(_taken).Append(" - j ").Append(_sumJ).Append(" = ").Append(_taken - _sumJ).Append(" (zgubieni przez gre ").Append(_lostByGame)
                  .Append(", bitew sprawdzonych ").Append(_checked).Append("), BK przyjal inaczej niz przyrost TotalPop: ").Append(_bkOddCalls).Append(" wywolan (roznica ").Append(_bkOddDiff)
                  .Append("), partie przegranych z szeregowymi po bitwie ").Append(_losersLeft).Append("; kontrola kodu k + j + f - n = ").Append(_controlDiff);
                sb.Append(" | uwolnieni jency bez odbiorcy ").Append(_freed).Append(" (w bitwach poza H3 tylko liczeni: ").Append(_freedOutside).Append(')');
                sb.Append(" | domy: do domu BK ").Append(_toBk).Append(" (w tym jako chlopi ").Append(_asSerfs).Append(", jako szlachta - kaprys BK ").Append(_asNobles)
                  .Append("), do wsi (tabory) ").Append(_toVillageMen).Append(" ludzi = ").Append(_toVillageHearth.ToString("0.0", ci)).Append(" hearth, do wsi (prosci i bandyci) ").Append(_toCommon)
                  .Append(" ludzi = ").Append(_toCommonHearth.ToString("0.0", ci)).Append(" hearth, z szablonu ").Append(_template).Append(" (garnizony ").Append(_templateGarr)
                  .Append(", z puli straz karawan ").Append(_templateFloat.ToString("0.0", ci)).Append("), bez domu ").Append(_homeless)
                  .Append(_vanished > 0 ? " (znikneli - prawo wyrzutkow wylaczone " + _vanished + ")" : " (do puli)");
                sb.Append(" | ludnosc BK swiata ").Append(worldTxt).Append(" | wcieleni jency AI (partie rodow) ").Append(_recruitedPrisoners);
                sb.Append(" | potkniecia: liczenie ").Append(_stumbleCalc).Append(", zastosowanie ").Append(_stumbleApply).Append(", dom ").Append(_stumbleHome).Append(", pomiar ").Append(_stumblePost);
                sb.Append(" | czas ").Append(((_ticks + sw.ElapsedTicks) * 1000.0 / Stopwatch.Frequency).ToString("0.0", ci)).Append(" ms.");
                Log.Info(sb.ToString());
                WriteCsv(day - 1);
            }
            catch (Exception e) { Log.Error("LosersFlee.Daily", e); }
            finally { ClearDay(); }
        }

        private static string Clean(string s) { return string.IsNullOrEmpty(s) ? "" : s.Replace(';', ',').Replace('\n', ' ').Replace('\r', ' '); }

        /// <summary>h3-domy.csv: wiersz na region na dobe (region jak w ludzie-regiony.csv) - porownanie z kolumna zwerbowani_dzis.</summary>
        private static void WriteCsv(int day)
        {
            if (_rows.Count == 0) return;
            var ci = CultureInfo.InvariantCulture;
            var csv = new StringBuilder();
            foreach (var r in _rows.Values)
            {
                if (r.Bk == 0 && r.VillageMen == 0 && r.Woods == 0 && r.Template == 0 && r.Freed == 0) continue;
                string id = "?", name = "";
                try { if (r.St != null) { id = r.St.StringId; name = r.St.Name != null ? r.St.Name.ToString() : ""; } } catch { }
                csv.Append(day).Append(';').Append(id).Append(';').Append(Clean(name)).Append(';').Append(r.Bk).Append(';').Append(r.VillageMen).Append(';')
                   .Append(r.VillageHearth.ToString("0.##", ci)).Append(';').Append(r.Woods).Append(';').Append(r.Template).Append(';').Append(r.Freed).Append(Environment.NewLine);
            }
            if (csv.Length == 0) return;
            string path = Log.Csv("h3-domy.csv", "dzien;region_id;region;do_domu_bk;do_wsi_ludzi;do_wsi_hearth;w_las;z_szablonu;uwolnieni", csv.ToString());
            if (path != null && _csvPath != path) { _csvPath = path; Log.Info("Przegrani (H3): plik domow uciekinierow (wiersz na region na dobe): " + path); }
        }

        // ------------------------------------------------------------ wpiecie
        internal static void ApplyAll(Harmony h)
        {
            string res, rec;
            try { _appliedRef = AccessTools.FieldRefAccess<MapEvent, bool>("_mapEventResultsApplied"); } catch { _appliedRef = null; }
            try { _surrRef = AccessTools.FieldRefAccess<MapEventSide, bool>("IsSurrendered"); } catch { _surrRef = null; }
            try
            {
                var m = AccessTools.Method(typeof(MapEvent), "CalculateAndCommitMapEventResults");
                var ps = m != null ? m.GetParameters() : null;
                if (m != null && !m.IsStatic && ps.Length == 0 && _appliedRef != null && _surrRef != null)
                {
                    h.Patch(m, prefix: new HarmonyMethod(typeof(LosersFlee), nameof(ResultsPrefix)) { priority = Priority.First },
                               postfix: new HarmonyMethod(typeof(LosersFlee), nameof(ResultsPostfix)) { priority = Priority.Last });
                    _patched = true;
                    res = "latka na wynik bitwy wpieta (prefiks + postfiks pomiaru)";
                }
                else res = "nie znaleziono metody - H3 spi" + (m == null ? " (CalculateAndCommitMapEventResults)" : "") + (_appliedRef == null ? " (_mapEventResultsApplied)" : "") + (_surrRef == null ? " (IsSurrendered)" : "");
            }
            catch (Exception e) { res = "latka nie weszla (" + e.Message + ") - H3 spi"; }
            try
            {
                var t = AccessTools.TypeByName("TaleWorlds.CampaignSystem.CampaignBehaviors.RecruitPrisonersCampaignBehavior");
                var m = t != null ? AccessTools.Method(t, "RecruitPrisonersAi") : null;
                var ps = m != null ? m.GetParameters() : null;
                if (m != null && ps.Length == 4 && ps[0].ParameterType == typeof(MobileParty) && ps[2].ParameterType == typeof(int))
                {
                    h.Patch(m, postfix: new HarmonyMethod(typeof(LosersFlee), nameof(RecruitPrisonersPostfix)));
                    rec = "licznik wcielonych jencow AI wpiety";
                }
                else rec = "licznik wcielonych jencow AI: brak metody";
            }
            catch (Exception e) { rec = "licznik wcielonych jencow AI: blad (" + e.Message + ")"; }
            Log.Info("LosersFlee (H3): " + res + "; " + rec + " | wlasciciele latek: " + Owners() + ".");
        }

        internal static bool Patched { get { return _patched; } }
    }
}
