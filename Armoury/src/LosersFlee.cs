using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
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
    /// pomijane). Postfiks na tej samej metodzie tylko mierzy (jency wzieci = j, przegrani bez szeregowych). Bitwy gracza w polu (walka
    /// reczna), oblezenia, rabunki, morze, odwrot, poddanie i Inni - bez zmian (licznik). Bohaterowie - jak w grze. Zapisu nie ma (liczniki dnia).
    /// F1 (decyzja Jeffa 09.10 "F"): bitwa gracza rozstrzygnieta symulacja (wyslij wojsko / autobitwa) - ten sam plan co bitwa AI
    /// (przegrani gracza albo wroga, zwyciezca z limitem zabitych), wylacznik LosersFleePlayerAuto; rozpoznanie - PlayerAuto; bitwa,
    /// w ktorej gracz walczyl w polu (misja), a potem dal "wyslij wojsko" - jak walka w polu (NoteMission, licznik "mieszane").
    ///
    /// SendHome - jedna funkcja pochodzenia: tabor wsi / rybacy -> hearth swojej wsi (odwrotnosc VillagerCampaignBehavior:179);
    /// karawany, zalogi, milicje, patrole i straz karawan -> "z szablonu" (gra tworzy ich z niczego - licznik do E7 / 108);
    /// Soldier / Mercenary -> ludnosc BK najblizszej bitwie osady swojej frakcji i kultury; reszta (bandyci, chlopi) -> hearth regionu.
    /// Poprawki po przegladzie kodu (rozdz. 15 projektu): wighty Innych (w ROT occupation="Soldier") nigdy do ludnosci BK - z niczego,
    /// do niczego; bandyta przy wylaczonym prawie wyrzutkow "z szablonu" (bandy rodza sie wtedy z szablonu gry, nie z hearth);
    /// prawdziwe sprawdzenie list po zmianie; liczniki domow wedlug zrodla; pomiar dziury werbunku (ludzie lordow AI z szablonu i z zalog).
    /// </summary>
    internal static class LosersFlee
    {
        internal static bool On { get { var s = Settings.Current; return s != null && s.LosersFleeEnabled; } }
        internal static bool PlayerAutoOn { get { var s = Settings.Current; return s != null && s.LosersFleePlayerAuto; } }

        // rodzaje partii
        internal const int KArmy = 0, KBand = 1, KVillager = 2, KCaravan = 3, KGarrison = 4, Kinds = 5;
        private static readonly string[] KName = { "wojsko", "bandy", "tabory wsi", "karawany", "zalogi/milicje/patrole" };
        // skad czlowiek wraca do domu: rozbici z bitwy, uwolnieni jency bez odbiorcy, powrot z puli wyrzutkow,
        // I1 prawo jenca (jency sprzedani albo wypuszczeni w krainie bez niewoli - PrisonerLaw; dom = osada kultury jenca, nie miejsce sprzedazy)
        internal const int SrcRouted = 0, SrcFreed = 1, SrcPool = 2, SrcLaw = 3, Srcs = 4;
        // dokad trafil: ludnosc BK, hearth wsi, "z szablonu"
        internal const int CatBk = 0, CatVillage = 1, CatTemplate = 2;
        // zawod klucza puli wyrzutkow (KeyUndead - wighty Innych: z niczego, wracaja do niczego, nigdy do ludnosci BK)
        internal const int KeyCommon = 0, KeyBandit = 1, KeySoldier = 2, KeyGuard = 3, KeyUndead = 4;
        // czas: wynik bitwy i doba (prefiks, postfiks, Daily), rozbici przy MapEventEnded (OutlawLaw.RoutedH3), powrot z puli (OutlawLaw.Daily)
        internal const int TickRouted = 0, TickPool = 1;

        // wzor (HISTORIA 4.3 + czlon F z audytu 2.8 / E10 / R10): p = 0.15 + 0.35C + 0.15T + 0.15O + 0.20Q - 0.10F(1-T), granice 5-65%
        private const float PBase = 0.15f, WC = 0.35f, WT = 0.15f, WO = 0.15f, WQ = 0.20f, WF = 0.10f, PMin = 0.05f, PMax = 0.65f;
        // promien sprawdzania terenu: srodek + po 4 punkty na kole 1 i 2 jednostek mapy (jedna stala na promien - prog 10-30% bitew z T = 1)
        private const float TerrainR1 = 1f, TerrainR2 = 2f;
        // tabor wsi wraca do swojej wsi dokladnie tak, jak go zdjeto: VillagerCampaignBehavior.cs:179, 187 - Hearth -= (n + 1) / 2
        private const float VillagerHearthPerMan = 0.5f;
        private const int VeteranTier = 4;
        // kultury z niewolnictwem wedlug Martina (decyzja Jeffa 07.10 pkt 5 i 6a; bez Pentos i Lorath) - jedna lista, ktora ma przejac
        // rabunek osada po osadzie: Zatoka Niewolnicza, Volantis, Lys, Myr, Tyrosh, Qohor, Norvos, Valyria, Dothrakowie (ROT: khuzait),
        // Zelazni Ludzie (thralls; ROT: sturgia = Iron Islands), Qarth (I1b: decyzja Jeffa 09.10 pkt 19; ADWD 16, 23 - Qarth zyje
        // z niewolnikow; ROT: qartheen = Qarth, Nowe Ghis, Qarkash, Miasto Kosci, krolestwo qarth)
        private static readonly HashSet<string> SlaveCultureIds = new HashSet<string>
            { "ghiscari", "volantine", "lyseni", "myrish", "tyroshi", "qohorik", "norvos", "valyrian", "khuzait", "sturgia", "qartheen" };

        internal static bool SlaveCulture(CultureObject c) { return c != null && c.StringId != null && SlaveCultureIds.Contains(c.StringId); }

        /// <summary>
        /// I1b: tekst linii startowej "Prawo jenca (I1b):" - warownie (miasta i zamki) kultur z listy wedlug kultury osady (prawo jenca I1)
        /// i frakcje, ktorych kultura jest na liscie - krolestwa oraz rody bez krolestwa (H3: zwyciezca z taka kultura frakcji bierze w niewole
        /// takze czesc taborow wsi). Kultura osady to kultura biezaca (BK zmienia ja po asymilacji i przy wczytaniu - w zapisie liczby moga
        /// sie roznic od nowej kampanii). Id z listy bez zadnej warowni = literowka albo zmiana kultury przez BK - wypisane zamiast "-".
        /// Tylko log, jeden przebieg po osadach, krolestwach i rodach.
        /// </summary>
        internal static string SlaveListText()
        {
            var byCul = new Dictionary<string, int>();
            foreach (var id in SlaveCultureIds) byCul[id] = 0;
            int sum = 0;
            foreach (var st in Settlement.All)
            {
                if (st == null || !(st.IsTown || st.IsCastle)) continue;
                var c = st.Culture;
                if (c == null || c.StringId == null || !byCul.ContainsKey(c.StringId)) continue;
                byCul[c.StringId]++; sum++;
            }
            var ids = new List<string>(byCul.Keys);
            ids.Sort(StringComparer.Ordinal);
            var none = new List<string>();
            var sb = new StringBuilder();
            for (int i = 0; i < ids.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(ids[i]).Append(' ').Append(byCul[ids[i]]);
                if (byCul[ids[i]] == 0) none.Add(ids[i]);
            }
            sb.Append(" (razem ").Append(sum).Append("); id bez warowni: ").Append(none.Count == 0 ? "-" : string.Join(", ", none.ToArray()));
            var ks = new List<string>();
            foreach (var k in Kingdom.All)
            {
                if (k == null || !SlaveCulture(k.Culture)) continue;
                ks.Add((k.Name != null ? k.Name.ToString() : "?") + " (" + k.StringId + "/" + k.Culture.StringId + (k.IsEliminated ? ", rozbite" : "") + ")");
            }
            sb.Append(" | krolestwa z niewola (H3, kultura krolestwa - zwyciezca bierze tez tabory wsi): ").Append(ks.Count == 0 ? "-" : string.Join(", ", ks.ToArray()));
            // H3 patrzy na kulture FRAKCJI zwyciezcy (win.MapFaction); rod bez krolestwa jest wlasna frakcja (ROT: np. bright_banners,
            // sons_of_the_harpy - ghiscari, is_minor_faction) - te tez biora w niewole tabory wsi
            var cs = new List<string>();
            foreach (var cl in Clan.All)
            {
                if (cl == null || cl.Kingdom != null || cl.IsEliminated || !SlaveCulture(cl.Culture)) continue;
                cs.Add((cl.Name != null ? cl.Name.ToString() : "?") + " (" + cl.StringId + "/" + cl.Culture.StringId + ")");
            }
            sb.Append(" | rody bez krolestwa z niewola (H3, kultura rodu - tez biora tabory wsi): ").Append(cs.Count == 0 ? "-" : string.Join(", ", cs.ToArray()));
            return sb.ToString();
        }

        // ------------------------------------------------------------ latka
        private static AccessTools.FieldRef<MapEvent, bool> _appliedRef;
        private static AccessTools.FieldRef<MapEventSide, bool> _surrRef;
        private static bool _patched;

        // ------------------------------------------------------------ liczniki dnia
        private static int _battles, _skipPlayer, _skipNotField, _skipRetreat, _skipSurr, _skipUndead;
        // F1: autobitwy gracza objete (wliczone tez w _battles i liczby przegranych / zwyciezcow), pominiete (nie w polu - takze szturm
        // wyslanym wojskiem, odwrot, Inni) i mieszane (najpierw walka w polu, potem wyslij wojsko - jak walka w polu, licznik "z graczem")
        private static int _autoCovered, _autoSkip, _autoMixed;
        private static MapEvent _lastAutoMe;          // ostatnia objeta autobitwa gracza - naglowek linii rozbitych przy MapEventEnded
        private static MapEvent _fieldMe;             // ostatnia bitwa gracza, w ktorej byla misja (walka w polu) - NoteMission
        private static readonly int[] _kMen = new int[Kinds], _kDead = new int[Kinds], _kCapt = new int[Kinds], _kRouted = new int[Kinds], _kDead0 = new int[Kinds];
        private static int _lMen, _lDead, _lDead0, _lCapt, _lRouted; private static double _lCapt0;
        private static int _wMen, _wDead, _wDead0, _wRevived;
        private static double _realLose, _realLose0, _realWin, _realWin0; private static int _realN;
        private static double _sumP, _sumC, _sumT, _sumO, _sumQ, _sumF; private static int _trapBattles;
        private static int _sumJChk, _taken, _lostByGame, _controlDiff, _checked, _unchecked;
        // prawdziwe sprawdzenie list (poprawka po przegladzie): stan list gry po zmianie wobec planu (k / j / f na typ) - w prefiksie
        // (zabici, ranni-jency, rozbici, w partii, ranni w partii) i po wyniku gry w postfiksie (zabici, rozbici - gra ich nie rusza)
        private static int _oddApplyTypes, _oddApplyMen, _oddPostTypes, _oddPostMen;
        private static int _freed, _freedOutside;
        private static int _toBk, _asSerfs, _asNobles, _toVillageMen, _toCommon, _template, _templateGarr, _templateBandit, _homeless, _vanished, _bkOddCalls, _bkOddDiff;
        private static float _toVillageHearth, _toCommonHearth, _templateFloat;
        // domy wedlug zrodla (SrcRouted / SrcFreed / SrcPool / SrcLaw): ludnosc BK, wies (tabory + prosci + bandyci), z szablonu
        private static readonly int[] _bkBy = new int[Srcs], _hearthBy = new int[Srcs], _tplBy = new int[Srcs];
        // Inni (wighty): rozbici pominieci w RoutedH3 i wighty, ktore doszly do SendHome inna droga - z niczego, do niczego
        private static int _undeadRouted, _undeadHome;
        // dziura werbunku (uwaga 1 przegladu): ludzie, ktorzy weszli do partii lordow AI bez werbunku u notabli - gorna granica tego,
        // co H3 moze dopisac do ludnosci BK ponad werbunek BK (nowa partia z szablonu klanu, przekazania z zalog)
        private static int _tplLordParties, _tplLordMen, _garrToLord, _lordToGarr;
        private static int _recruitedPrisoners;
        private static int _stumbleCalc, _stumbleApply, _stumbleHome, _stumblePost;
        private static long _ticks;
        private static readonly long[] _ticksH3 = new long[2];
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
        // I1: dom jenca wedlug kultury (pamiec na dobe, klucz = punkt + kultura) i warownie Muru w rekach Strazy (lista na dobe)
        private static readonly Dictionary<string, Home> _lawHome = new Dictionary<string, Home>();
        private static int _lawHomeDay = -1;
        private static List<Home> _wallHomes;
        private static int _wallDay = -1;
        private const string WatchId = "nightswatch";

        internal static void Reset()
        {
            ClearDay();
            _keyKind.Clear(); _keyTroop.Clear(); _battleHome.Clear(); _poolHome.Clear(); _poolHomeDay = -1;
            _lawHome.Clear(); _lawHomeDay = -1; _wallHomes = null; _wallDay = -1;
            _homes = null; _homeOf = null; _homesDay = -1;
            _bkTried = false; _popMgr = null; _getPopData = null; _fromSoldiers = null; _updateType = null; _typeCount = null; _isRetinue = null; _totalPop = null; _serfs = null; _nobles = null; _slavesT = null;
            _lastWorldPop = -1; _ownersLogged = false; _csvPath = null; _cur = null; _lastAutoMe = null; _fieldMe = null;
            _errCalc = _errApply = _errHome = _errPost = false;
        }

        private static void ClearDay()
        {
            _battles = _skipPlayer = _skipNotField = _skipRetreat = _skipSurr = _skipUndead = 0;
            _autoCovered = _autoSkip = _autoMixed = 0;
            Array.Clear(_kMen, 0, Kinds); Array.Clear(_kDead, 0, Kinds); Array.Clear(_kCapt, 0, Kinds); Array.Clear(_kRouted, 0, Kinds); Array.Clear(_kDead0, 0, Kinds);
            _lMen = _lDead = _lDead0 = _lCapt = _lRouted = 0; _lCapt0 = 0;
            _wMen = _wDead = _wDead0 = _wRevived = 0;
            _realLose = _realLose0 = _realWin = _realWin0 = 0; _realN = 0;
            _sumP = _sumC = _sumT = _sumO = _sumQ = _sumF = 0; _trapBattles = 0;
            _sumJChk = _taken = _lostByGame = _controlDiff = _checked = _unchecked = 0;
            _oddApplyTypes = _oddApplyMen = _oddPostTypes = _oddPostMen = 0;
            _freed = _freedOutside = 0;
            _toBk = _asSerfs = _asNobles = _toVillageMen = _toCommon = _template = _templateGarr = _templateBandit = _homeless = _vanished = _bkOddCalls = _bkOddDiff = 0;
            _toVillageHearth = _toCommonHearth = _templateFloat = 0f;
            Array.Clear(_bkBy, 0, Srcs); Array.Clear(_hearthBy, 0, Srcs); Array.Clear(_tplBy, 0, Srcs);
            _undeadRouted = _undeadHome = 0;
            _tplLordParties = _tplLordMen = _garrToLord = _lordToGarr = 0;
            _recruitedPrisoners = 0;
            _stumbleCalc = _stumbleApply = _stumbleHome = _stumblePost = 0;
            _ticks = 0; Array.Clear(_ticksH3, 0, 2);
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
            if (ch != null && Undead.Character(ch)) k = KeyUndead;        // wight ma w ROT occupation="Soldier" - nie moze isc do ludnosci BK
            else if (ch != null)
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
        private static object _popMgr, _serfs, _nobles, _slavesT;
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
                if (popT != null && popT.IsEnum) { _serfs = Enum.Parse(popT, "Serfs"); _nobles = Enum.Parse(popT, "Nobles"); _slavesT = Enum.Parse(popT, "Slaves"); }
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
        internal static void NoteUndead(int n) { if (n > 0) _undeadRouted += n; }
        /// <summary>Czas czesci H3 poza ta klasa (rozbici w OutlawLaw.RoutedH3, powrot z puli w OutlawLaw.Daily) - do linii dnia.</summary>
        internal static void AddTicks(int which, long t) { if (which >= 0 && which < _ticksH3.Length && t > 0) _ticksH3[which] += t; }

        /// <summary>
        /// Jedna funkcja pochodzenia (2.4, 3.4): rozbici z bitwy, uwolnieni jency bez odbiorcy, powrot z puli, I1 prawo jenca. Zwraca,
        /// ilu przyjeto; reszte wolajacy oddaje do puli ("bez domu"). cat - dokad trafili (BK, wies, z szablonu).
        /// </summary>
        internal static int SendHome(MobileParty party, CharacterObject troop, int n, Vec2 pos, IFaction fac, int src, out int cat)
        {
            cat = CatVillage;
            if (troop == null || troop.IsHero || n <= 0) return 0;
            if (src < 0 || src > SrcLaw) src = SrcRouted;
            try
            {
                // 0. wight Innych (ROT: occupation="Soldier", kultura whitewalker) - z niczego, wraca do niczego; nigdy ludnosc BK ani hearth
                // (RoutedH3 pomija ich wczesniej; to zabezpieczenie dla uwolnionych jencow i powrotu z puli)
                if (Undead.Character(troop))
                {
                    cat = CatTemplate;
                    _undeadHome += n;
                    return n;
                }
                // I1: jeniec sprzedany / wypuszczony daleko od domu wraca do SWOJEJ krainy - punkt = najblizsza miejscu osada z danymi BK
                // jego kultury (brak takiej osady, np. kultura bandycka - region miejsca); dalej te same reguly co dla rozbitych
                if (src == SrcLaw && troop.Culture != null) pos = LawHomePos(pos, troop.Culture);
                // 1. tabor wsi i rybacy -> hearth wlasnej wsi (dokladna odwrotnosc zdjecia przy wysylaniu taboru); ludnosci BK nie dopisujemy
                if (party != null && party.IsVillager)
                {
                    Village v = null;
                    try { v = party.HomeSettlement != null ? party.HomeSettlement.Village : null; } catch { }
                    if (v != null)
                    {
                        float h = VillagerHearthPerMan * n;
                        v.Hearth += h;
                        _toVillageMen += n; _toVillageHearth += h; _hearthBy[src] += n;
                        var row = Row(OutlawLaw.RegionFor(v.Settlement)); row.VillageMen += n; row.VillageHearth += h;
                        return n;
                    }
                    return ToCommon(n, pos, true, src);      // wies nieznana - najbiedniejsza wies regionu
                }
                // 2. z szablonu: karawany, zalogi, milicje, patrole (gra tworzy ich bez ubytku ludnosci) i straz karawan poza karawana;
                // 2a. bandyta przy WYLACZONYM prawie wyrzutkow - bandy rodza sie wtedy z szablonu gry (bramki OutlawLaw przepuszczaja),
                // a nie z hearth, wiec bandyta nie ma wsi, do ktorej wraca (przy wlaczonym prawie - pkt 4, hearth)
                bool garr = party != null && party.IsGarrison;
                bool banditTpl = !OutlawLaw.On && troop.Occupation == Occupation.Bandit;
                if ((party != null && (party.IsCaravan || garr || party.IsMilitia || party.IsPatrolParty)) || troop.Occupation == Occupation.CaravanGuard || banditTpl)
                {
                    cat = CatTemplate;
                    _template += n; _tplBy[src] += n;
                    if (garr) _templateGarr += n;
                    if (banditTpl) _templateBandit += n;
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
                return ToCommon(n, pos, false, src);
            }
            catch (Exception e)
            {
                _stumbleHome++;
                if (!_errHome) { _errHome = true; Log.Error("LosersFlee.SendHome", e); }
                return 0;
            }
        }

        private static int ToCommon(int n, Vec2 pos, bool villager, int src)
        {
            var region = OutlawLaw.HearthRegionAt(pos);
            if (region == null) return 0;
            OutlawLaw.ReturnHome(region, n);
            float h = n * Settings.Current.OutlawHearthPerMan;
            if (villager) { _toVillageMen += n; _toVillageHearth += h; } else { _toCommon += n; _toCommonHearth += h; }
            _hearthBy[src] += n;
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
            else if (src == SrcLaw)
            {
                cache = LawCache();                  // I1: pos to juz osada domowa (LawHomePos) - pamiec na punkt, nie na bitwe
                fac = null;
                where = PosKey(pos);
            }
            var home = FindHome(pos, fac, troop.Culture, cache, where);
            if (home == null) return 0;
            return AddBk(home, troop, n, src);
        }

        /// <summary>n ludzi typu troop do ludnosci BK osady home (werbunek BK na odwrot; zastepczo chlopi) - wspolne dla ToBk i ToWall.</summary>
        private static int AddBk(Home home, CharacterObject troop, int n, int src)
        {
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
            _toBk += got; _bkBy[src] += got;
            if (got > 0) Row(OutlawLaw.RegionFor(home.St)).Bk += got;
            return got;
        }

        // ------------------------------------------------------------ I1: prawo jenca (PrisonerLaw)
        private static string PosKey(Vec2 p) { return (int)p.x + ":" + (int)p.y; }

        private static Dictionary<string, Home> LawCache()
        {
            int day = (int)CampaignTime.Now.ToDays;
            if (_lawHomeDay != day) { _lawHome.Clear(); _lawHomeDay = day; }
            return _lawHome;
        }

        /// <summary>I1: punkt domu jenca - najblizsza miejscu (pos) osada z danymi BK jego kultury; brak - warownia regionu miejsca; nic - pos.</summary>
        private static Vec2 LawHomePos(Vec2 pos, CultureObject cul)
        {
            var h = FindHome(pos, null, cul, LawCache(), PosKey(pos));
            return h != null && h.St != null ? h.St.GetPosition2D : pos;
        }

        /// <summary>
        /// I1 "na Mur" (jak Yoren): n przestepcow do ludnosci BK najblizszej miejscu (pos) warowni, ktora Nocna Straz jeszcze trzyma
        /// (frakcja wlasciciela z kultura nightswatch - nie Inni, nie zdobywca). Stamtad Straz werbuje jak z kazdej
        /// osady BK. Zwraca, ilu przyjeto (0 = Muru nie ma albo BK nie przyjal - wolajacy odsyla ich do domu); wall - ktora warownia.
        /// </summary>
        internal static int ToWall(CharacterObject troop, int n, Vec2 pos, out Settlement wall)
        {
            wall = null;
            if (troop == null || troop.IsHero || n <= 0) return 0;
            try
            {
                int day = (int)CampaignTime.Now.ToDays;
                if (_wallHomes == null || _wallDay != day)
                {
                    _wallDay = day;
                    _wallHomes = new List<Home>();
                    foreach (var x in Homes())
                    {
                        var st = x.St;
                        if (!(st.IsTown || st.IsCastle)) continue;
                        var mf = st.MapFaction;                  // warownia w rekach Strazy (kultura krolestwa ROT "nightswatch") - kultura osady moze sie zmienic w BK
                        if (mf == null || mf.Culture == null || mf.Culture.StringId != WatchId) continue;
                        _wallHomes.Add(x);
                    }
                }
                Home best = null; float bd = float.MaxValue;
                foreach (var x in _wallHomes)
                {
                    float d = pos.DistanceSquared(x.St.GetPosition2D);
                    if (d < bd) { bd = d; best = x; }
                }
                if (best == null) return 0;
                wall = best.St;
                return AddBk(best, troop, n, SrcLaw);
            }
            catch (Exception e)
            {
                _stumbleHome++;
                if (!_errHome) { _errHome = true; Log.Error("LosersFlee.ToWall", e); }
                return 0;
            }
        }

        /// <summary>
        /// I1 (uwaga przegladu): n niewolnikow do ludnosci BK osady st - to samo, co BK SendOffPrisoners przy polityce Enslavement
        /// (PopulationData.UpdatePopType(Slaves, n)), ale kazdy czlowiek RAZ: BK liczy Helpers.GetRosterCount = Number + WoundedNumber,
        /// a Number juz zawiera rannych. Zwraca, ilu dopisano (0 - osada bez danych BK, BK tez nic); -1 - nie da sie (wolajacy oddaje
        /// sprawe BK, jak dotad).
        /// </summary>
        internal static int AddSlaves(Settlement st, int n)
        {
            if (st == null || n <= 0) return 0;                  // BK z liczba 0 dzieli przez zero (StateSlaves) - nic do dopisania
            if (!BkResolve() || _updateType == null || _slavesT == null) return -1;
            object pd;
            try { pd = _getPopData.Invoke(_popMgr, new object[] { st }); } catch { return -1; }
            if (pd == null) return 0;
            int before = TypeCount(pd, _slavesT);
            try { _updateType.Invoke(pd, new object[] { _slavesT, n, false }); return n; }
            catch
            {
                int d = TypeCount(pd, _slavesT) - before;       // wyjatek po dopisaniu (most BK do innej ekonomii) - BK nie moze dopisac drugi raz
                return d > 0 ? d : -1;
            }
        }

        // ------------------------------------------------------------ plan bitwy
        private sealed class TP { public CharacterObject T; public int D, R, M, W, Wb, Wbw, K, J, F; }
        private sealed class LP { public MapEventParty Mep; public MobileParty Mp; public int Kind; public float P; public readonly List<TP> Types = new List<TP>(); public int N, K, J, F, D0; public double J0; public bool Applied; }
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
            // plan objal kazda partie przegrana, ktora ma szeregowych w partii, i zastosowanie przeszlo bez bledu - tylko wtedy przyrost
            // lochow zwyciezcow = suma j (inaczej gra bierze jencow z partii spoza planu po staremu i "zgubieni" wychodza falszywie)
            public bool Covered = true;
            public bool Auto;                      // F1: autobitwa gracza (komunikat w grze, osobny naglowek linii)
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

        // ------------------------------------------------------------ F1: autobitwa gracza
        /// <summary>
        /// Bitwa gracza rozstrzygnieta symulacja (wyslij wojsko / autobitwa), a nie walka w polu (dekompilacja gry 1.4.8):
        /// - BattleSimulation (konstruktor, z PlayerEncounter.InitSimulation <- MenuHelper.EncounterOrderAttack) ustawia MapEvent.IsPlayerSimulation
        ///   = true i nic go nie zeruje (ROT tez tylko ustawia true) - sama flaga zostaje po ucieczce z symulacji i potem ataku w polu;
        /// - ucieczka z symulacji: BattleSimulation.IsPlayerRetreated; nowa symulacja tworzy nowy obiekt (flaga false);
        /// - walka w polu: MenuHelper -> PlayerEncounter.StartAttackMission ustawia CampaignBattleResult != null; zeruje go tylko ContinueBattle
        ///   (bitwa trwa dalej), wiec przy wyniku z pola jest != null, a przy wyniku z symulacji (DoWait: "BattleSimulation != null && wynik") null;
        /// - BattleSimulation znika dopiero w PlayerEncounter.Finish / LeaveBattle - po CalculateAndCommitMapEventResults (DoApplyMapEventResults).
        /// - bitwa mieszana (poprawka po przegladzie): gracz walczy w polu, wycofuje sie, bitwa trwa (ContinueBattle zeruje CampaignBattleResult),
        ///   potem "wyslij wojsko" - InitSimulation tworzy NOWA symulacje, a listy MapEventParty sumuja obie rundy. Taka bitwa = walka w polu
        ///   (H3 nie rusza): _fieldMe z NoteMission (kazda misja w czasie bitwy gracza; pomylka tylko w strone "jak w grze"); sprawdza to
        ///   ResultsPrefix (licznik "mieszane"), ta funkcja mowi tylko, czy wynik pochodzi z symulacji.
        /// </summary>
        internal static bool PlayerAuto(MapEvent me)
        {
            try
            {
                if (me == null || !me.IsPlayerMapEvent || !me.IsPlayerSimulation) return false;
                var pe = PlayerEncounter.Current;
                var sim = pe != null ? pe.BattleSimulation : null;
                return sim != null && sim.MapEvent == me && !sim.IsPlayerRetreated && PlayerEncounter.CampaignBattleResult == null;
            }
            catch { return false; }
        }

        /// <summary>Misja (walka w polu) w czasie bitwy gracza - z SubModuleMain.OnMissionBehaviorInitialize. Tanie: jedno pole.</summary>
        internal static void NoteMission()
        {
            try { var me = MapEvent.PlayerMapEvent; if (me != null) _fieldMe = me; } catch { }
        }

        /// <summary>Bitwa poza H3: AI - licznik powodu i uwolnieni jency (tylko licznik); autobitwa gracza - tylko licznik F1 (jak dotad bitwa gracza).</summary>
        private static void SkipOut(MapEvent me, bool auto, ref int counter)
        {
            if (auto) { _autoSkip++; return; }
            counter++;
            CountFreedOutside(me);
        }

        /// <summary>Komunikat w grze po autobitwie gracza (po angielsku): ilu pokonanych ucieklo, ilu zabitych, ilu wzietych - tylko partie
        /// zastosowane bez bledu (LP.Applied); gdy gracz wygral - ilu poleglych jego strony przezylo jako ranni (WinRev z zastosowania).</summary>
        private static string AutoMessage(BP bp)
        {
            int n = 0, k = 0, j = 0, f = 0;
            foreach (var lp in bp.Losers) { if (!lp.Applied) continue; n += lp.N; k += lp.K; j += lp.J; f += lp.F; }
            if (n <= 0) return null;
            bool mine = false;
            try { mine = bp.Me.DefeatedSide == bp.Me.PlayerSide; } catch { }
            string who = mine ? "of your side" : "of the defeated";
            string s = (f > 0 ? f + " " + who : "None " + who) + " fled the field (" + k + " slain, " + j + " taken)";
            if (!mine && bp.WinRev > 0) s += "; " + bp.WinRev + " of your side's fallen survived, wounded";
            return s + ".";
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
                // bitwy gracza: walka w polu decyduje (linia "pominieta (gracz)" przy MapEventEnded); F1 - rozstrzygnieta symulacja jak bitwa AI
                bool auto = false;
                if (me.IsPlayerMapEvent)
                {
                    bool sim = PlayerAutoOn && PlayerAuto(me);
                    // mieszana: najpierw walka w polu (misja), potem wyslij wojsko - jak walka w polu
                    if (sim && me == _fieldMe) { _skipPlayer++; _autoMixed++; return; }
                    // szturm / morze wyslanym wojskiem: DoWait przy IsSiegeAssault sam ustawia CampaignBattleResult - poza H3, licznik F1 "nie w polu"
                    if (!sim && PlayerAutoOn && me.IsPlayerSimulation && me != _fieldMe && (!me.IsFieldBattle || me.IsNavalMapEvent)) { _autoSkip++; return; }
                    if (!sim) { _skipPlayer++; return; }
                    auto = true;
                }
                if (!me.IsFieldBattle || me.IsNavalMapEvent) { SkipOut(me, auto, ref _skipNotField); return; }
                if (me.RetreatingSide != BattleSideEnum.None) { SkipOut(me, auto, ref _skipRetreat); return; }
                var lose = me.GetMapEventSide(me.DefeatedSide); var win = me.GetMapEventSide(me.WinningSide);
                if (lose == null || win == null) return;
                // poddanie: w autobitwie gracza to ksiegowosc gry PO walce - PlayerEncounter.DoWait po wyniku symulacji wola EnemySurrender
                // (gracz wygral, brak odwrotu) albo PlayerSurrender (gracz przegral, zero zdrowych); poddanie przed walka konczy bitwe bez symulacji
                if (!auto && _surrRef(lose)) { _skipSurr++; CountFreedOutside(me); return; }
                if (AnyUndead(me)) { SkipOut(me, auto, ref _skipUndead); return; }

                var plan = Plan(me, win, lose);           // faza 1: policz i sprawdz
                if (plan == null) return;
                plan.Auto = auto;
                Apply(plan);                              // faza 2: same AddToCounts na sprawdzonych liczbach
                if (auto) { _autoCovered++; _lastAutoMe = me; }
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
                    if (mp == null || !mp.IsActive)
                    {
                        // partia po staremu - gra wezmie jej rannych i 25% zdrowych; sprawdzenie jencow tej bitwy nic by nie znaczylo
                        if (mep.Party.MemberRoster != null && mep.Party.MemberRoster.TotalRegulars > 0) bp.Covered = false;
                        continue;
                    }
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
                    bp.Covered = false;
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
                    _controlDiff += lp.K + lp.J + lp.F - lp.N;          // kontrola kodu (z definicji 0)
                    // prawdziwe sprawdzenie (po przegladzie): odczyt list gry po zmianie, typ po typie - zabici = k, ranni-jency = j,
                    // rozbici = f, w partii dokladnie j (wszyscy ranni). Gra po pojmaniu zeruje kazda partie przegrana, wiec "szeregowi
                    // w partii po bitwie" niczego nie sprawdzali - to sprawdzenie zastapilo tamto
                    for (int t = 0; t < lp.Types.Count; t++)
                    {
                        var tp = lp.Types[t];
                        int idx = member.FindIndexOfTroop(tp.T);
                        int mNow = idx >= 0 ? member.GetElementNumber(idx) : 0, wNow = idx >= 0 ? member.GetElementWoundedNumber(idx) : 0;
                        int bad = Math.Abs(mep.DiedInBattle.GetTroopCount(tp.T) - tp.K) + Math.Abs(mep.WoundedInBattle.GetTroopCount(tp.T) - tp.J)
                                  + Math.Abs(mep.RoutedInBattle.GetTroopCount(tp.T) - tp.F) + Math.Abs(mNow - tp.J) + Math.Abs(wNow - tp.J);
                        if (bad != 0) { _oddApplyTypes++; _oddApplyMen += bad; }
                    }
                    bp.SumJ += lp.J;
                    bp.LoseDeadAll += lp.K - lp.D0;
                    _kMen[lp.Kind] += lp.N; _kDead[lp.Kind] += lp.K; _kCapt[lp.Kind] += lp.J; _kRouted[lp.Kind] += lp.F; _kDead0[lp.Kind] += lp.D0;
                    _lMen += lp.N; _lDead += lp.K; _lDead0 += lp.D0; _lCapt += lp.J; _lRouted += lp.F; _lCapt0 += lp.J0;
                    lp.Applied = true;                                     // komunikat w grze liczy tylko partie zastosowane bez bledu
                }
                catch (Exception e)
                {
                    _stumbleApply++;
                    bp.Covered = false;
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
                // jako LUDZIE, wiec sie tu nie mieszaja); roznica = jency zgubieni przez gre (null w losowaniu). Tylko bitwy, w ktorych plan
                // objal kazda partie przegrana z szeregowymi - inaczej gra bierze jencow z partii spoza planu i roznica wychodzi ujemna
                int after = 0;
                foreach (var r in bp.Prisons) after += r.TotalRegulars;
                int got = after - bp.PrisonBefore;
                if (bp.Covered) { _taken += got; _sumJChk += bp.SumJ; _lostByGame += bp.SumJ - got; _checked++; }
                else _unchecked++;
                // listy zabitych i rozbitych po wyniku gry dalej = plan (gra ich w tej metodzie nie zmienia - zmiana tu to cudza latka)
                foreach (var lp in bp.Losers)
                {
                    for (int t = 0; t < lp.Types.Count; t++)
                    {
                        var tp = lp.Types[t];
                        int bad = Math.Abs(lp.Mep.DiedInBattle.GetTroopCount(tp.T) - tp.K) + Math.Abs(lp.Mep.RoutedInBattle.GetTroopCount(tp.T) - tp.F);
                        if (bad != 0) { _oddPostTypes++; _oddPostMen += bad; }
                    }
                }
                string msg = null;
                if (bp.Auto)
                {
                    msg = AutoMessage(bp);
                    if (msg != null) InformationManager.DisplayMessage(new InformationMessage(msg, Colors.Yellow));
                }
                BattleLine(bp, got, msg);
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

        // ------------------------------------------------------------ dziura werbunku - tylko pomiar (uwaga 1 przegladu)
        // Gra daje nowej partii lorda AI ludzi z szablonu klanu (LordPartyComponent.InitializationArgs.InitializeLordPartyProperties ->
        // InitializeMobilePartyAroundPosition(DefaultPartyTemplate), LordPartyComponent.cs:38-39), a lordowie biora tez ludzi z zalog
        // (GarrisonTroopsCampaignBehavior.TakeTroopsFromGarrison, :583-610), ktore rosna z niczego. Tacy ludzie, rozbici w bitwie, ida
        // przez SendHome do ludnosci BK jak zwerbowani - te liczniki daja jawna gorna granice tej dziury. Nic nie zmieniaja.

        private static bool AiLordParty(MobileParty mp)
        {
            try { return mp != null && mp != MobileParty.MainParty && mp.IsLordParty && mp.ActualClan != Clan.PlayerClan; } catch { return false; }
        }

        public static void LordInitPrefix(MobileParty __0, out int __state)
        {
            __state = -1;
            try { if (__0 != null && __0.MemberRoster != null) __state = __0.MemberRoster.TotalRegulars; } catch { }
        }

        /// <summary>Postfiks Priority.Last - po SpoilsCompany (klan Spoils: szablon zdjety przy wlaczniku), wiec liczy tylko tych, co zostali.</summary>
        public static void LordInitPostfix(MobileParty __0, Hero __1, int __state)
        {
            try
            {
                if (__state < 0 || __0 == null || __1 == null || __0 == MobileParty.MainParty || __1.Clan == Clan.PlayerClan) return;
                int add = __0.MemberRoster.TotalRegulars - __state;
                if (add <= 0) return;
                _tplLordParties++; _tplLordMen += add;
            }
            catch { }
        }

        public static void GarrisonPrefix(MobileParty __0, out int __state)
        {
            __state = -1;
            try { if (AiLordParty(__0)) __state = __0.MemberRoster.TotalRegulars; } catch { }
        }

        public static void TakeFromGarrisonPostfix(MobileParty __0, int __state)
        {
            try { if (__state >= 0 && __0 != null) { int d = __0.MemberRoster.TotalRegulars - __state; if (d > 0) _garrToLord += d; } } catch { }
        }

        public static void LeaveToGarrisonPostfix(MobileParty __0, int __state)
        {
            try { if (__state >= 0 && __0 != null) { int d = __state - __0.MemberRoster.TotalRegulars; if (d > 0) _lordToGarr += d; } } catch { }
        }

        // ------------------------------------------------------------ logi
        private static string F2(float v) { return v.ToString("0.00", CultureInfo.InvariantCulture); }
        private static string Pc(double x, double of) { return of > 0 ? (100.0 * x / of).ToString("0.#", CultureInfo.InvariantCulture) + "%" : "-"; }
        private static string Ms(long ticks) { return (ticks * 1000.0 / Stopwatch.Frequency).ToString("0.0", CultureInfo.InvariantCulture); }

        /// <summary>Czas H3 w linii dnia - wszystkie czesci (poprawka po przegladzie: dawniej tylko prefiks, postfiks i doba).</summary>
        private static string TimeText(long dailyNow)
        {
            long a = _ticks + dailyNow, b = _ticksH3[TickRouted], c = _ticksH3[TickPool];
            return " | czas " + Ms(a + b + c) + " ms (wynik bitwy i doba " + Ms(a) + ", rozbici przy koncu bitwy " + Ms(b) + ", powrot z puli - doba wczesniej " + Ms(c) + ")";
        }

        /// <summary>Dziura werbunku (uwaga 1 przegladu): ludzie, ktorzy weszli do partii lordow AI spoza rodu gracza bez werbunku BK u notabli.
        /// To gorna granica tego, ile z "do domu BK" moze byc ludzmi z niczego: test - do domu BK <= werbunek u notabli + ta liczba.</summary>
        private static string HoleText()
        {
            return " | dziura werbunku (partie lordow AI, ludzie bez werbunku u notabli): nowe partie z szablonu klanu " + _tplLordMen + " ludzi (" + _tplLordParties
                   + " partii), z zalog do lordow " + _garrToLord + ", od lordow do zalog " + _lordToGarr + " (netto z zalog " + (_garrToLord - _lordToGarr) + ")";
        }

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

        private static void BattleLine(BP bp, int got, string msg)
        {
            int min = Math.Max(0, Settings.Current.BattleChronicleMinMen);
            if (!bp.Auto && bp.LMen + bp.WMen < min) return;          // male potyczki tylko liczone (autobitwa gracza - zawsze linia)
            int n = 0, k = 0, j = 0, f = 0, d0 = 0;
            foreach (var lp in bp.Losers) { n += lp.N; k += lp.K; j += lp.J; f += lp.F; d0 += lp.D0; }
            int wn = bp.WinN, wd0 = bp.WinD0, rev = bp.WinRev, wdNow = wd0 - rev;
            var sb = new StringBuilder();
            if (bp.Auto)
            {
                bool lost = false;
                try { lost = bp.Me.DefeatedSide == bp.Me.PlayerSide; } catch { }
                sb.Append("Bitwa: H3 autobitwa gracza (").Append(lost ? "gracz przegral" : "gracz wygral").Append(") dzien ");
            }
            else sb.Append("Bitwa: H3 dzien ");
            sb.Append((int)CampaignTime.Now.ToDays).Append(" - ").Append(bp.Me.EventType).Append(Where(bp.Me)).Append(": przegrany ")
              .Append(Who(bp.Lose)).Append(' ').Append(bp.LMen).Append(" ludzi (konni ").Append(Pc(bp.LMount, 1)).Append(", tier ").Append(bp.LTier.ToString("0.0", CultureInfo.InvariantCulture))
              .Append(") vs zwyciezca ").Append(Who(bp.Win)).Append(' ').Append(bp.WMen).Append(" ludzi (konni ").Append(Pc(bp.WMount, 1)).Append(", tier ")
              .Append(bp.WTier.ToString("0.0", CultureInfo.InvariantCulture)).Append(") | p = 0.15 + 0.35xC ").Append(F2(bp.C)).Append(" + 0.15xT ").Append(bp.T > 0.5f ? "1" : "0")
              .Append(" (teren: srodek ").Append(bp.Center).Append(", woda ").Append(bp.TrapPts).Append("/8) + 0.15xO ").Append(F2(bp.O)).Append(" + 0.20xQ ").Append(F2(bp.Q))
              .Append(" - 0.10xF ").Append(F2(bp.F * (1f - bp.T)))                 // wartosc czynna: F liczy sie tylko przy T = 0
              .Append(bp.T > 0.5f && bp.F > 0f ? " (F " + F2(bp.F) + " nie liczy sie przy T = 1)" : "").Append(" = ").Append(Pc(bp.P, 1))
              .Append(" | gra: zabici ").Append(d0).Append(" (").Append(Pc(d0, n)).Append("), ranni ").Append(bp.G_W).Append(", rozbici ").Append(bp.G_R).Append(", stali ").Append(bp.G_Stand)
              .Append(" | H3: zabici ").Append(k).Append(" (").Append(Pc(k, n)).Append("), jency ").Append(j).Append(" (").Append(Pc(j, n)).Append("), rozbici ").Append(f).Append(" (").Append(Pc(f, n)).Append(')')
              .Append(" | zwyciezca: zabici gry ").Append(wd0).Append(" (").Append(Pc(wd0, wn)).Append(") -> ").Append(wdNow).Append(" (").Append(Pc(wdNow, wn)).Append("), +").Append(rev).Append(" rannych")
              .Append(" | jency wzieci ").Append(got).Append(bp.Covered ? " (zgubieni przez gre " + (bp.SumJ - got) + ")" : " (bez sprawdzenia - partia przegrana poza planem)")
              .Append(", uwolnieni bez odbiorcy ").Append(bp.FreedN);
            if (bp.Auto) sb.Append(" | komunikat w grze: ").Append(msg ?? "brak (przegrani bez szeregowych albo blad zastosowania)");
            sb.Append('.');
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

        /// <summary>Bitwa gracza: jedna linia z liczbami po wyniku i podzialem rozbitych (3.2). Walka w polu - H3 jej nie rusza ("pominieta");
        /// autobitwa objeta przez F1 - liczby po H3 (plan w linii "Bitwa: H3 autobitwa gracza").</summary>
        internal static void PlayerLine(MapEvent me, int pool, int bk, int village, int template, int homeless, bool toPool, int undead)
        {
            try
            {
                bool auto = me != null && me == _lastAutoMe;
                if (auto) _lastAutoMe = null;
                var sb = new StringBuilder();
                sb.Append(auto ? "Bitwa: H3 autobitwa gracza - rozbici przy koncu bitwy, dzien " : "Bitwa: H3 pominieta (gracz) dzien ").Append((int)CampaignTime.Now.ToDays)
                  .Append(" - ").Append(me.EventType).Append(Where(me)).Append(": ")
                  .Append(auto ? "autobitwa (H3 objela)" : me == _fieldMe ? (me.IsPlayerSimulation ? "pole i symulacja (mieszana - jak pole)" : "pole")
                         : me.IsPlayerSimulation ? (me.IsFieldBattle && !me.IsNavalMapEvent ? "symulacja" : "symulacja nie w polu (poza H3)") : "pole").Append(" | gra: ");
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
                  .Append("), z szablonu ").Append(template).Append(", bez domu ").Append(homeless).Append(homeless > 0 ? (toPool ? " (do puli)" : " (znikneli - prawo wyrzutkow wylaczone)") : "")
                  .Append(undead > 0 ? ", Inni (wighty - z niczego, do niczego) " + undead : "").Append('.');
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
                      .Append(HoleText()).Append(TimeText(sw.ElapsedTicks)).Append('.');
                    Log.Info(sb.ToString());
                    return;
                }
                sb.Append(" - bitew objetych ").Append(_battles).Append(" (pominiete: z graczem ").Append(_skipPlayer).Append(", nie w polu ").Append(_skipNotField)
                  .Append(" [oblezenie, rabunek, wypad, morze], odwrot ").Append(_skipRetreat).Append(", poddanie ").Append(_skipSurr).Append(", z Innymi ").Append(_skipUndead).Append(')');
                sb.Append(" | autobitwy gracza (F1): ");
                if (PlayerAutoOn) sb.Append("objete ").Append(_autoCovered).Append(" (wliczone w objete i liczby nizej), pominiete ").Append(_autoSkip).Append(" (nie w polu - takze szturm wyslanym wojskiem, odwrot, Inni), mieszane ")
                                    .Append(_autoMixed).Append(" (najpierw walka w polu, potem wyslij wojsko - jak walka w polu, w \"z graczem\")");
                else sb.Append("WYLACZONE (jak walka w polu - w \"z graczem\")");
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
                sb.Append(" | sprawdzenia: jency wzieci ").Append(_taken).Append(" - j ").Append(_sumJChk).Append(" = ").Append(_taken - _sumJChk).Append(" (zgubieni przez gre ").Append(_lostByGame)
                  .Append(", bitew sprawdzonych ").Append(_checked).Append(", bez sprawdzenia - partia przegrana poza planem ").Append(_unchecked)
                  .Append("), listy gry po zmianie inne niz plan: ").Append(_oddApplyTypes).Append(" typow (").Append(_oddApplyMen).Append(" ludzi), po wyniku gry ")
                  .Append(_oddPostTypes).Append(" typow (").Append(_oddPostMen).Append(" ludzi), BK przyjal inaczej niz przyrost TotalPop: ").Append(_bkOddCalls).Append(" wywolan (roznica ").Append(_bkOddDiff)
                  .Append("); kontrola kodu k + j + f - n = ").Append(_controlDiff);
                sb.Append(" | uwolnieni jency bez odbiorcy ").Append(_freed).Append(" (w bitwach poza H3 tylko liczeni: ").Append(_freedOutside).Append(')');
                sb.Append(" | domy: do domu BK ").Append(_toBk).Append(" (w tym jako chlopi ").Append(_asSerfs).Append(", jako szlachta - kaprys BK ").Append(_asNobles)
                  .Append("), do wsi (tabory) ").Append(_toVillageMen).Append(" ludzi = ").Append(_toVillageHearth.ToString("0.0", ci)).Append(" hearth, do wsi (prosci i bandyci) ").Append(_toCommon)
                  .Append(" ludzi = ").Append(_toCommonHearth.ToString("0.0", ci)).Append(" hearth, z szablonu ").Append(_template).Append(" (garnizony ").Append(_templateGarr)
                  .Append(", bandyci przy wylaczonym prawie wyrzutkow ").Append(_templateBandit)
                  .Append(", z puli straz karawan ").Append(_templateFloat.ToString("0.0", ci)).Append("), bez domu ").Append(_homeless)
                  .Append(_vanished > 0 ? " (znikneli - prawo wyrzutkow wylaczone " + _vanished + ")" : " (do puli)");
                // ta sama suma wedlug zrodla: "rozbici z bitew" tej doby = dopisek H3 linii "Wyrzutki:" tej samej doby (ludnosc BK, do wsi,
                // z szablonu); "z puli" pochodzi z powrotu w OutlawLaw.Daily, ktory biegnie PO tej linii - to liczba z linii "Wyrzutki:" doby wczesniej
                sb.Append(" | domy wedlug zrodla: rozbici z bitew - BK ").Append(_bkBy[SrcRouted]).Append(", wies ").Append(_hearthBy[SrcRouted]).Append(", z szablonu ").Append(_tplBy[SrcRouted])
                  .Append("; uwolnieni jency - BK ").Append(_bkBy[SrcFreed]).Append(", wies ").Append(_hearthBy[SrcFreed]).Append(", z szablonu ").Append(_tplBy[SrcFreed])
                  .Append("; z puli (linia \"Wyrzutki:\" doby ").Append(day - 1).Append(") - BK ").Append(_bkBy[SrcPool])
                  .Append("; prawo jenca I1 (sprzedani i wypuszczeni, linia \"Prawo jenca (I1):\") - BK z Murem ").Append(_bkBy[SrcLaw]).Append(", wies ").Append(_hearthBy[SrcLaw])
                  .Append(", z szablonu ").Append(_tplBy[SrcLaw]);
                sb.Append(" | Inni (wighty - z niczego, do niczego, nigdy do BK ani wsi): rozbici ").Append(_undeadRouted).Append(", inna droga ").Append(_undeadHome);
                sb.Append(HoleText());
                sb.Append(" | ludnosc BK swiata ").Append(worldTxt).Append(" | wcieleni jency AI (partie rodow) ").Append(_recruitedPrisoners);
                sb.Append(" | potkniecia: liczenie ").Append(_stumbleCalc).Append(", zastosowanie ").Append(_stumbleApply).Append(", dom ").Append(_stumbleHome).Append(", pomiar ").Append(_stumblePost);
                sb.Append(TimeText(sw.ElapsedTicks)).Append('.');
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
            // dziura werbunku - tylko pomiar (postfiksy licza, nic nie zmieniaja); brak metody = licznik zostaje 0 i linia startowa to mowi
            var hole = new List<string>();
            try
            {
                var mi = AccessTools.Method(typeof(LordPartyComponent.InitializationArgs), "InitializeLordPartyProperties", new[] { typeof(MobileParty), typeof(Hero) });
                if (mi != null)
                {
                    h.Patch(mi, prefix: new HarmonyMethod(typeof(LosersFlee), nameof(LordInitPrefix)) { priority = Priority.First },
                                postfix: new HarmonyMethod(typeof(LosersFlee), nameof(LordInitPostfix)) { priority = Priority.Last });
                    hole.Add("szablon nowej partii lorda");
                }
                else hole.Add("szablon nowej partii lorda: BRAK METODY");
                var gt = AccessTools.TypeByName("TaleWorlds.CampaignSystem.CampaignBehaviors.GarrisonTroopsCampaignBehavior");
                var ga = new[] { typeof(MobileParty), typeof(Settlement), typeof(int), typeof(bool) };
                var take = gt != null ? AccessTools.Method(gt, "TakeTroopsFromGarrison", ga) : null;
                var leave = gt != null ? AccessTools.Method(gt, "LeaveTroopsToGarrison", ga) : null;
                if (take != null)
                {
                    h.Patch(take, prefix: new HarmonyMethod(typeof(LosersFlee), nameof(GarrisonPrefix)), postfix: new HarmonyMethod(typeof(LosersFlee), nameof(TakeFromGarrisonPostfix)));
                    hole.Add("z zalog do lordow");
                }
                else hole.Add("z zalog do lordow: BRAK METODY");
                if (leave != null)
                {
                    h.Patch(leave, prefix: new HarmonyMethod(typeof(LosersFlee), nameof(GarrisonPrefix)), postfix: new HarmonyMethod(typeof(LosersFlee), nameof(LeaveToGarrisonPostfix)));
                    hole.Add("od lordow do zalog");
                }
                else hole.Add("od lordow do zalog: BRAK METODY");
            }
            catch (Exception e) { hole.Add("blad (" + e.Message + ")"); }
            Log.Info("LosersFlee (H3): " + res + "; " + rec + "; pomiar dziury werbunku: " + string.Join(", ", hole.ToArray()) + " | wlasciciele latek: " + Owners() + ".");
        }

        internal static bool Patched { get { return _patched; } }
    }
}
