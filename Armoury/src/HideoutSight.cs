using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// m100-kryjowki (Jeff 21.10 ok. 03:00): "im wiecej masz scouting, tym lepiej widzisz kryjowki bandytow, ale ponizej 10 scoutingu masz szanse 0%,
    /// od 11 do 200 rosna do 100% ... inaczej mozesz ja zdobyc w karczmie ... jesli plotki powiedza, ze tu i tu jest obozowisko bandytow, to wtedy jak na nie
    /// trafisz, wyswietli sie normalnie" oraz "Kryjowki powinny nie pokazywac sie na wioskach pobocznych, tylko tam, gdzie jest wolne miejsce, z dala od miast
    /// i wsi, oraz jak jedna kryjowka zostanie rozbita, to kolejna powinna pojawic sie w innym miejscu".
    ///
    /// 1) WYPATRZENIE (klucz HideoutScoutingChance). Gra wypatruje kryjowke gracza w dwoch miejscach (dekompilacja 1.4.8):
    ///    a) HideoutCampaignBehavior.HourlyTickSettlement - co godzine, kryjowka zajeta i niewypatrzona w zasiegu GetHideoutSpottingDistance (wzrok x 1.2,
    ///       perk Rumour Network), rzut gry 1 - d2/r2;
    ///    b) BanditSpawnCampaignBehavior.OnSettlementEntered - banda wchodzi do kryjowki, a gracz widzi te bande (IsVisible).
    ///    Przed kazda z nich bramka: przy pierwszym podejsciu w zasieg JEDEN rzut na kryjowke - szansa z Zwiadu zwiadowcy partii (EffectiveScout):
    ///    Zwiad &lt;= Min (10) -> 0%, Min..Full (200) liniowo, &gt;= Full -> 100%. Wynik zostaje na caly pobyt w zasiegu (bez migania i bez rzutu co godzine);
    ///    wyjscie dalej niz 1.5 x zasieg zapomina wynik - nastepne podejscie to nowy rzut. Rzut udany = gra wypatruje jak dotad (swoj rzut odleglosci).
    ///    Kryjowka znana z plotki (karczma, zajazd, kufel m98 - Rumours.Show) - bez naszego rzutu, jak w grze. Juz wypatrzone (zapis) zostaja. AI bez zmian
    ///    (gra wypatruje tylko dla gracza). Zadania gry (NearbyBanditBase itp.) i wydarzenia ustawiaja IsSpotted same - to wiedza z zadania, nie wzrok.
    ///    Droga b) bez pomijania metody: CheckForSpawningBanditBoss (herszt tylko w wypatrzonej) idzie PRZED testem wypatrzenia, wiec IsSpotted=true
    ///    na czas testu ustawiamy dopiero w postfiksie CheckForSpawningBanditBoss, a finalizer OnSettlementEntered zdejmuje je z powrotem.
    /// 2) MIEJSCE (klucz HideoutAwayFromSettlements). Nowa kryjowke gra wybiera z puli stalych punktow (266 na mapie ROT) w
    ///    BanditSpawnCampaignBehavior.SelectANonInfestedHideoutOfSameCultureByWeight. Postfiks: wybor gry zostaje, jesli punkt lezy co najmniej
    ///    HideoutMinDistSettlement od miasta / zamku / wsi gry i HideoutMinDistSideVillage od wioski pobocznej (warstwa VillageBook) - inaczej losujemy
    ///    z punktow tej samej kultury, ktore spelniaja warunek (waga: odleglosc od najblizszej zajetej kryjowki, jak w grze - rozrzut po mapie).
    /// 3) PO ROZBICIU (klucz HideoutRelocateAfterClear). Rozbita kryjowka (bitwa wygrana przez napastnika albo oproznienie po walce) trafia do pamieci
    ///    (zapis) na HideoutClearedMemoryDays dni; nowa kryjowka nie powstaje w promieniu HideoutClearedRadius od ostatnio rozbitych. Punktow NIE
    ///    przesuwamy: obrazek kryjowki to obiekt sceny mapy (nie idzie za Settlement.Position), pozycja jest w zapisie, a siatka nawigacji nie daje pewnosci
    ///    ladu - wersja bezpieczna to wybor innego punktu z puli. Brak punktu spelniajacego wszystko -> najpierw luzujemy pamiec rozbitych (najdalszy od
    ///    nich), potem odleglosc (najdalszy od osad). Recenzja 21.10: nigdy blizej niz pol promienia od rozbitej (w tym ten sam punkt) - wtedy bez nowej kryjowki
    ///    (FillANewHideoutWithBandits ma 'if (hideout != null)', gra probuje znowu nastepnej doby).
    /// 4) BANDY AI (recenzja 21.10, klucz HideoutGuideBanditAi): bandy same zasiedlaja wolne punkty swojej kultury (AiVisitSettlementBehavior i kopia BK
    ///    BKAIVisitSettlementBehavior.CalculateVisitHideoutScoresForBanditParty - 4 bandy w srodku = IsInfested ROT) z pominieciem wyboru 2/3. Postfiks zeruje wynik
    ///    "idz do kryjowki" dla wolnych punktow, ktore nie spelniaja 2) albo leza w promieniu rozbitych - migawka liczona co godzine na watku glownym, watki AI
    ///    tylko czytaja (bez Log, bez slownikow; licznik przez Interlocked). Pomiar: co godzine nowe zajete kryjowki spoza wyboru -> linia "aktywacja poza wyborem".
    ///    Herszt BK (BKBanditBehavior.CreateBanditHero -> InfestHieout, pierwszy punkt kultury) - tylko linia "aktywacja BK" (podmiana punktu rozdzielilaby herszta
    ///    i bandy - ryzyko w CHANGELOG).
    /// Stan (zapis arm_hideout_sight przez SaveText.Sync): znane z plotki, rozbite (id, x, y, doba), wyniki rzutow na pobyt. Watek glowny (zdarzenia kampanii).
    /// </summary>
    internal sealed class HideoutSight : CampaignBehaviorBase
    {
        private const string P = "Kryjowki (m100): ";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static HideoutSight _inst;

        // --- stan kampanii (instancja = nowa kampania / wczytanie czysci wszystko)
        private readonly HashSet<string> _known = new HashSet<string>();                 // kryjowki znane z plotki
        private readonly Dictionary<string, int> _roll = new Dictionary<string, int>();  // wynik rzutu na pobyt: 1 tak, 0 nie, 2 znana z plotki
        private readonly List<Cleared> _cleared = new List<Cleared>();

        private sealed class Cleared { public string Id; public float X, Y; public double Day; }

        // --- pamiec przeliczen (bez zapisu)
        private readonly Dictionary<Settlement, float> _dSet = new Dictionary<Settlement, float>();
        private readonly Dictionary<Settlement, float> _dSide = new Dictionary<Settlement, float>();
        private readonly Dictionary<Settlement, string> _nearName = new Dictionary<Settlement, string>();
        private object _sideRef;          // lista wiosek pobocznych, z ktorej liczono (VillageBook.LayoutRecords)
        private bool _started;
        private int _stumbles;
        private readonly HashSet<string> _stumbleSites = new HashSet<string>();
        // liczniki doby
        private int _dRolls, _dPass, _dBlockHour, _dBlockEnter, _dRumour, _dKnownPass, _dPicks, _dKept, _dSwapped, _dLoosened, _dNoFit, _dSpotted, _dCleared;
        private int _dNone, _dOutside, _dOutsideBad, _dBk, _dForgot;
        // recenzja 21.10: nowe zajete kryjowki spoza wyboru (bandy AI) - zbior zajetych z poprzedniej godziny i kryjowki, ktore przeszly przez Pick / BK w tej godzinie
        private HashSet<string> _prevInf;
        private readonly HashSet<string> _viaPick = new HashSet<string>();

        // migawka dla watkow AI: wolne punkty zakazane (watek glowny podmienia cala referencje co godzine; watki AI tylko czytaja)
        private static volatile HashSet<Settlement> _aiBlock;
        private static int _aiCut, _aiErr;
        private static string _aiNote = "bandy AI 0/0";

        private static bool _wired;
        private static string _wireNote = "";
        private static readonly HashSet<Settlement> _blockEnter = new HashSet<Settlement>();
        private static readonly HashSet<Settlement> _tempSpotted = new HashSet<Settlement>();

        public HideoutSight() { _inst = this; }

        private static Settings S { get { return Settings.Current; } }
        private static bool ChanceOn { get { var s = S; return s != null && s.HideoutScoutingChance; } }
        private static bool RumourOn { get { var s = S; return s != null && s.HideoutScoutingChance && s.HideoutRumourReveals; } }
        private static bool AwayOn { get { var s = S; return s != null && s.HideoutAwayFromSettlements; } }
        private static bool RelocOn { get { var s = S; return s != null && s.HideoutRelocateAfterClear; } }
        private static bool AiOn { get { var s = S; return s != null && s.HideoutGuideBanditAi && (s.HideoutAwayFromSettlements || s.HideoutRelocateAfterClear); } }
        private static double Today { get { return CampaignTime.Now.ToDays; } }

        public override void RegisterEvents()
        {
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourly);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDaily);
            CampaignEvents.OnHideoutSpottedEvent.AddNonSerializedListener(this, OnSpotted);
            CampaignEvents.OnHideoutDeactivatedEvent.AddNonSerializedListener(this, OnDeactivated);
            CampaignEvents.OnHideoutBattleCompletedEvent.AddNonSerializedListener(this, OnBattle);
        }

        // ------------------------------------------------------------ zapis
        public override void SyncData(IDataStore dataStore)
        {
            string v = "";
            try { if (dataStore.IsSaving) v = Pack(); } catch (Exception e) { Stumble("Pack", e); v = ""; }
            SaveText.Sync(dataStore, "arm_hideout_sight", ref v);
            if (dataStore.IsLoading) { try { Unpack(v); } catch (Exception e) { Stumble("Unpack", e); } }
        }

        // format: 1|k:id,id|c:id;x;y;doba,...|r:id=0,id=1
        private string Pack()
        {
            var sb = new StringBuilder("1|k:");
            sb.Append(string.Join(",", new List<string>(_known).ToArray()));
            sb.Append("|c:");
            for (int i = 0; i < _cleared.Count; i++)
            {
                var c = _cleared[i];
                if (i > 0) sb.Append(',');
                sb.Append(c.Id).Append(';').Append(c.X.ToString("0.##", Inv)).Append(';').Append(c.Y.ToString("0.##", Inv)).Append(';').Append(c.Day.ToString("0.##", Inv));
            }
            sb.Append("|r:");
            bool first = true;
            foreach (var kv in _roll) { if (!first) sb.Append(','); first = false; sb.Append(kv.Key).Append('=').Append(kv.Value.ToString(Inv)); }
            return sb.ToString();
        }

        private void Unpack(string v)
        {
            _known.Clear(); _roll.Clear(); _cleared.Clear();
            if (string.IsNullOrEmpty(v) || !v.StartsWith("1|")) return;
            foreach (var part in v.Split('|'))
            {
                if (part.StartsWith("k:"))
                {
                    foreach (var id in part.Substring(2).Split(',')) if (id.Length > 0) _known.Add(id);
                }
                else if (part.StartsWith("c:"))
                {
                    foreach (var e in part.Substring(2).Split(','))
                    {
                        var f = e.Split(';');
                        float x, y; double d;
                        if (f.Length == 4 && f[0].Length > 0 && float.TryParse(f[1], NumberStyles.Float, Inv, out x) && float.TryParse(f[2], NumberStyles.Float, Inv, out y)
                            && double.TryParse(f[3], NumberStyles.Float, Inv, out d))
                            _cleared.Add(new Cleared { Id = f[0], X = x, Y = y, Day = d });
                    }
                }
                else if (part.StartsWith("r:"))
                {
                    foreach (var e in part.Substring(2).Split(','))
                    {
                        int eq = e.IndexOf('='); int r;
                        if (eq > 0 && int.TryParse(e.Substring(eq + 1), NumberStyles.Integer, Inv, out r)) _roll[e.Substring(0, eq)] = r;
                    }
                }
            }
        }

        // ------------------------------------------------------------ potkniecia (liczymy, nie gasimy)
        private void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_stumbleSites.Add(where)) Log.Error("HideoutSight." + where, e);
        }

        // ------------------------------------------------------------ szansa ze Zwiadu
        internal static float Chance(int scouting)
        {
            var s = S;
            int lo = s != null ? Math.Max(0, s.HideoutScoutingMin) : 10;
            int hi = s != null ? Math.Max(lo + 1, s.HideoutScoutingFull) : 200;
            if (scouting <= lo) return 0f;
            if (scouting >= hi) return 1f;
            return (scouting - lo) / (float)(hi - lo);
        }

        private static int Scouting(out string who)
        {
            who = "?";
            var mp = MobileParty.MainParty;
            Hero h = mp != null ? mp.EffectiveScout : null;
            if (h == null) h = Hero.MainHero;
            if (h == null) return 0;
            who = h.Name != null ? h.Name.ToString() : h.StringId;
            return h.GetSkillValue(DefaultSkills.Scouting);
        }

        /// <summary>Bramka wypatrzenia dla kryjowki w zasiegu: true = gra wypatruje jak dotad. Jeden rzut na pobyt, wynik w _roll.</summary>
        private bool Allow(Settlement st, string path, float dist, float range)
        {
            string id = st.StringId ?? "";
            int r;
            if (_roll.TryGetValue(id, out r))
            {
                if (r == 0) { if (path == "wzrok") _dBlockHour++; else _dBlockEnter++; return false; }
                if (r == 2) _dKnownPass++;
                return true;
            }
            string who; int sc = Scouting(out who);
            if (RumourOn && _known.Contains(id))
            {
                _roll[id] = 2; _dKnownPass++;
                Log.Info(P + "rzut " + id + " | Zwiad " + sc + " (" + who + "), szansa -, wynik TAK, znana z plotki TAK - wypatrzenie jak w grze | droga " + path
                         + ", odl. " + dist.ToString("0.0", Inv) + " (zasieg " + range.ToString("0.0", Inv) + ")");
                return true;
            }
            float p = Chance(sc);
            bool ok = p >= 1f || (p > 0f && MBRandom.RandomFloat < p);
            _roll[id] = ok ? 1 : 0;
            _dRolls++; if (ok) _dPass++; else if (path == "wzrok") _dBlockHour++; else _dBlockEnter++;
            Log.Info(P + "rzut " + id + " | Zwiad " + sc + " (" + who + "), szansa " + (p * 100f).ToString("0.0", Inv) + "%, wynik " + (ok ? "TAK" : "NIE")
                     + ", znana z plotki NIE | droga " + path + ", odl. " + dist.ToString("0.0", Inv) + " (zasieg " + range.ToString("0.0", Inv) + ")");
            return ok;
        }

        private static float Range()
        {
            try { return Campaign.Current.Models.MapVisibilityModel.GetHideoutSpottingDistance(); } catch { return 0f; }
        }

        // ------------------------------------------------------------ latki
        /// <summary>Prefiks HideoutCampaignBehavior.HourlyTickSettlement: te same warunki co gra (zajeta, niewypatrzona, w zasiegu, ktos w srodku), potem nasz rzut.</summary>
        public static bool HourPre(Settlement __0)
        {
            var me = _inst;
            if (me == null || !ChanceOn) return true;
            try
            {
                var st = __0;
                if (st == null || !st.IsHideout || st.Hideout == null || !st.Hideout.IsInfested || st.Hideout.IsSpotted) return true;
                var mp = MobileParty.MainParty;
                if (mp == null) return true;
                float range = Range();
                if (range <= 0f) return true;
                float d2 = mp.Position.DistanceSquared(st.Position);
                if (d2 >= range * range)
                {
                    // poza zasiegiem gra i tak nic nie robi; dalej niz 1.5 x zasieg - pobyt skonczony, wynik zapomniany
                    if (d2 > 2.25f * range * range) me._roll.Remove(st.StringId ?? "");
                    return true;
                }
                if (st.Parties.Count <= 0) return true;
                return me.Allow(st, "wzrok", (float)Math.Sqrt(d2), range);
            }
            catch (Exception e) { me.Stumble("HourPre", e); return true; }
        }

        /// <summary>Prefiks BanditSpawnCampaignBehavior.OnSettlementEntered: banda wchodzi do kryjowki na oczach gracza. Rzut nieudany -> kryjowka na liste
        /// blokady; IsSpotted=true na czas testu gry ustawia dopiero postfiks CheckForSpawningBanditBoss (herszt sprawdzany wczesniej - nie moze zobaczyc falszywej flagi).</summary>
        public static void EnterPre(MobileParty __0, Settlement __1, ref Settlement __state)
        {
            __state = null;
            var me = _inst;
            if (me == null || !ChanceOn) return;
            try
            {
                var mp = __0; var st = __1;
                if (Campaign.Current == null || !Campaign.Current.GameStarted || mp == null || !mp.IsBandit || st == null || !st.IsHideout || st.Hideout == null) return;
                if (st.Hideout.IsSpotted || !st.Hideout.IsInfested || !mp.IsVisible) return;
                var main = MobileParty.MainParty;
                float range = Range();
                float dist = main != null ? (float)Math.Sqrt(main.Position.DistanceSquared(st.Position)) : 0f;
                if (me.Allow(st, "banda wchodzi", dist, range)) return;
                _blockEnter.Add(st);
                __state = st;
            }
            catch (Exception e) { me.Stumble("EnterPre", e); }
        }

        public static void BossPost(Settlement __0)
        {
            try
            {
                var st = __0;
                if (st == null || _blockEnter.Count == 0 || !_blockEnter.Contains(st) || st.Hideout == null || st.Hideout.IsSpotted) return;
                st.Hideout.IsSpotted = true;
                _tempSpotted.Add(st);
            }
            catch (Exception e) { if (_inst != null) _inst.Stumble("BossPost", e); }
        }

        public static void EnterFin(Settlement __state)
        {
            if (__state == null) return;
            try
            {
                _blockEnter.Remove(__state);
                if (_tempSpotted.Remove(__state) && __state.Hideout != null) __state.Hideout.IsSpotted = false;
            }
            catch (Exception e) { if (_inst != null) _inst.Stumble("EnterFin", e); }
        }

        /// <summary>Postfiks BanditSpawnCampaignBehavior.SelectANonInfestedHideoutOfSameCultureByWeight: miejsce nowej kryjowki.</summary>
        public static void PickPost(Clan __0, ref Hideout __result)
        {
            var me = _inst;
            if (me == null || (!AwayOn && !RelocOn) || __result == null) return;
            try
            {
                __result = me.Pick(__0, __result);   // null = brak miejsca poza rozbitymi - gra nie zasiedla nic (FillANewHideoutWithBandits: if (hideout != null))
                if (__result != null && __result.Settlement != null) me._viaPick.Add(__result.Settlement.StringId ?? "");
            }
            catch (Exception e) { me.Stumble("PickPost", e); }
        }

        /// <summary>Postfiks CalculateVisitHideoutScoresForBanditParty (gra i BK; WATKI AI): wynik "idz do kryjowki" = 0 dla wolnych punktow z migawki _aiBlock.
        /// Tylko odczyt migawki i zmiana wyniku tej partii (PartyThinkParams jest jej wlasne); bez Log, bez alokacji, licznik przez Interlocked.</summary>
        public static void AiPost(PartyThinkParams __2)
        {
            var blk = _aiBlock;
            if (blk == null || __2 == null) return;
            try
            {
                var list = __2.AIBehaviorScores;
                for (int i = 0; i < list.Count; i++)
                {
                    var t = list[i];
                    if (t.Item2 <= 0f || t.Item1.AiBehavior != AiBehavior.GoToSettlement) continue;
                    var st = t.Item1.Party as Settlement;
                    if (st == null || !blk.Contains(st)) continue;
                    var d = t.Item1;
                    __2.SetBehaviorScore(in d, 0f);
                    Interlocked.Increment(ref _aiCut);
                }
            }
            catch { Interlocked.Increment(ref _aiErr); }
        }

        /// <summary>Postfiks BKBanditBehavior.InfestHieout (herszt BK, watek glowny): kryjowka zajeta poza wyborem - tylko linia (punkt bez zmian, patrz CHANGELOG).</summary>
        public static void BkInfestPost(Hideout __0)
        {
            var me = _inst;
            if (me == null || __0 == null || __0.Settlement == null) return;
            try
            {
                var st = __0.Settlement;
                if (!__0.IsInfested) return;   // OutlawLaw.GateBkInfest moglo pominac metode
                string id = st.StringId ?? "";
                if (!me._viaPick.Add(id)) return;
                me._dBk++;
                me.LogOutside(st, "aktywacja BK", "herszt BK zasiedla pierwszy punkt kultury (BKBanditBehavior.CreateBanditHero)");
            }
            catch (Exception e) { me.Stumble("BkInfestPost", e); }
        }

        internal static void ApplyAll(Harmony h)
        {
            var miss = new List<string>(); int ok = 0;
            try
            {
                var m = AccessTools.DeclaredMethod(typeof(HideoutCampaignBehavior), "HourlyTickSettlement", new[] { typeof(Settlement) });
                if (m != null) { h.Patch(m, prefix: new HarmonyMethod(typeof(HideoutSight), nameof(HourPre))); ok++; } else miss.Add("HourlyTickSettlement");
            }
            catch (Exception e) { miss.Add("HourlyTickSettlement(" + e.Message + ")"); }
            try
            {
                var t = typeof(BanditSpawnCampaignBehavior);
                var ose = AccessTools.DeclaredMethod(t, "OnSettlementEntered", new[] { typeof(MobileParty), typeof(Settlement), typeof(Hero) });
                var boss = AccessTools.DeclaredMethod(t, "CheckForSpawningBanditBoss", new[] { typeof(Settlement), typeof(MobileParty) });
                if (ose != null && boss != null)
                {
                    h.Patch(boss, postfix: new HarmonyMethod(typeof(HideoutSight), nameof(BossPost)));
                    h.Patch(ose, prefix: new HarmonyMethod(typeof(HideoutSight), nameof(EnterPre)), finalizer: new HarmonyMethod(typeof(HideoutSight), nameof(EnterFin)));
                    ok += 2;
                }
                else miss.Add("OnSettlementEntered/CheckForSpawningBanditBoss");
            }
            catch (Exception e) { miss.Add("OnSettlementEntered(" + e.Message + ")"); }
            try
            {
                var m = AccessTools.DeclaredMethod(typeof(BanditSpawnCampaignBehavior), "SelectANonInfestedHideoutOfSameCultureByWeight", new[] { typeof(Clan) });
                if (m != null && m.ReturnType == typeof(Hideout)) { h.Patch(m, postfix: new HarmonyMethod(typeof(HideoutSight), nameof(PickPost))); ok++; } else miss.Add("SelectANonInfestedHideoutOfSameCultureByWeight");
            }
            catch (Exception e) { miss.Add("SelectANonInfested(" + e.Message + ")"); }
            // recenzja 21.10: bandy AI (gra i BK) i herszt BK - typy szukane po nazwie (BK moze nie byc); liczone osobno od 4 latek rdzenia
            int aiOk = 0, aiN = 0; var aiMiss = new List<string>();
            foreach (var tn in new[] { "TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors.AiVisitSettlementBehavior", "BannerKings.Behaviours.BKAIVisitSettlementBehavior" })
            {
                try
                {
                    var t = AccessTools.TypeByName(tn);
                    if (t == null) continue;
                    aiN++;
                    var m = AccessTools.DeclaredMethod(t, "CalculateVisitHideoutScoresForBanditParty");
                    var ps = m != null ? m.GetParameters() : null;
                    if (ps != null && ps.Length == 3 && ps[2].ParameterType == typeof(PartyThinkParams)) { h.Patch(m, postfix: new HarmonyMethod(typeof(HideoutSight), nameof(AiPost))); aiOk++; }
                    else aiMiss.Add(t.Name);
                }
                catch (Exception e) { aiMiss.Add(tn + "(" + e.Message + ")"); }
            }
            string bk = "BK herszt -";
            try
            {
                var t = AccessTools.TypeByName("BannerKings.Behaviours.BKBanditBehavior");
                if (t != null)
                {
                    var m = AccessTools.DeclaredMethod(t, "InfestHieout");
                    var ps = m != null ? m.GetParameters() : null;
                    if (ps != null && ps.Length >= 1 && ps[0].ParameterType == typeof(Hideout)) { h.Patch(m, postfix: new HarmonyMethod(typeof(HideoutSight), nameof(BkInfestPost))); bk = "BK herszt 1/1"; }
                    else bk = "BK herszt 0/1";
                }
            }
            catch (Exception e) { bk = "BK herszt 0/1 (" + e.Message + ")"; }
            _aiNote = "bandy AI " + aiOk + "/" + aiN + (aiMiss.Count > 0 ? " (BRAK: " + string.Join(", ", aiMiss.ToArray()) + ")" : "") + ", " + bk;
            _wired = miss.Count == 0;
            _wireNote = "latki wpiete " + ok + "/4" + (miss.Count > 0 ? " (BRAK: " + string.Join(", ", miss.ToArray()) + ")" : "") + " | " + _aiNote;
            Log.Info(P + _wireNote + ".");
        }

        // ------------------------------------------------------------ plotki (Rumours.Show / Hideouts)
        /// <summary>Plotka o kryjowce uslyszana (okno plotek pokazane graczowi). Od teraz wypatrzenie tej kryjowki jak w grze.</summary>
        internal static void Heard(string hideoutId, string where)
        {
            var me = _inst;
            if (me == null || string.IsNullOrEmpty(hideoutId) || !RumourOn) return;
            try
            {
                var st = Settlement.Find(hideoutId);
                bool added = me._known.Add(hideoutId);
                if (me._roll.ContainsKey(hideoutId) && me._roll[hideoutId] == 0) me._roll.Remove(hideoutId);   // nowa wiedza - nastepny przeglad bez starego "nie"
                me._dRumour++;
                Log.Info(P + "plotka o kryjowce " + hideoutId + " | uslyszana w " + (where ?? "?") + ", " + (added ? "nowa" : "juz znana")
                         + ", zajeta " + (st != null && st.Hideout != null && st.Hideout.IsInfested ? "TAK" : "NIE")
                         + ", wypatrzona " + (st != null && st.Hideout != null && st.Hideout.IsSpotted ? "TAK" : "NIE")
                         + (st != null ? ", najblizsza osada " + me.NearInfo(st) : "") + " | znanych z plotki " + me._known.Count);
            }
            catch (Exception e) { me.Stumble("Heard", e); }
        }

        /// <summary>Czy plotka moze mowic o tej kryjowce: przy kluczu tylko zajeta i niewypatrzona.</summary>
        internal static bool RumourFits(Hideout h)
        {
            if (!RumourOn) return true;
            return h != null && h.IsInfested && !h.IsSpotted;
        }

        // ------------------------------------------------------------ odleglosci od osad
        private void EnsureDist()
        {
            object side = null;
            try { side = VillageBook.EnsureLayoutRecords(); } catch { }   // recenzja 21.10: nowa kampania wybiera startowe kryjowki przed VillageBook.Resolve
            if (_dSet.Count > 0 && ReferenceEquals(side, _sideRef)) return;
            _sideRef = side;
            _dSet.Clear(); _dSide.Clear(); _nearName.Clear();
            var real = new List<Settlement>();
            foreach (var s in Settlement.All) if (s != null && (s.IsTown || s.IsCastle || s.IsVillage)) real.Add(s);
            var recs = side as List<MapVillageRecord>;
            foreach (var h in Hideout.All)
            {
                var st = h != null ? h.Settlement : null;
                if (st == null) continue;
                Vec2 p = st.GetPosition2D;
                float bd = float.MaxValue; Settlement bs = null;
                foreach (var s in real) { float d = s.GetPosition2D.Distance(p); if (d < bd) { bd = d; bs = s; } }
                float sd = float.MaxValue; string sn = null;
                if (recs != null) foreach (var r in recs) { float d = new Vec2(r.X, r.Y).Distance(p); if (d < sd) { sd = d; sn = r.Name; } }
                _dSet[st] = bd; _dSide[st] = sd;
                _nearName[st] = (bs != null ? bs.Name + " " + bd.ToString("0.0", Inv) : "-") + (sn != null ? ", wioska " + sn + " " + sd.ToString("0.0", Inv) : ", wioski poboczne -");
            }
        }

        private string NearInfo(Settlement st) { EnsureDist(); string n; return _nearName.TryGetValue(st, out n) ? n : "?"; }

        private bool Away(Settlement st)
        {
            var s = S;
            float a, b;
            if (!_dSet.TryGetValue(st, out a)) return true;
            if (!_dSide.TryGetValue(st, out b)) b = float.MaxValue;
            return a >= s.HideoutMinDistSettlement && b >= s.HideoutMinDistSideVillage;
        }

        /// <summary>Jak bardzo punkt spelnia odleglosc (min z ilorazow; &gt;= 1 spelnia).</summary>
        private float AwayScore(Settlement st)
        {
            var s = S;
            float a, b;
            if (!_dSet.TryGetValue(st, out a)) a = 999f;
            if (!_dSide.TryGetValue(st, out b)) b = 999f;
            return Math.Min(a / Math.Max(0.1f, s.HideoutMinDistSettlement), b / Math.Max(0.1f, s.HideoutMinDistSideVillage));
        }

        private void PruneCleared()
        {
            var s = S;
            double keep = s != null ? Math.Max(0, s.HideoutClearedMemoryDays) : 60;
            double now = Today;
            _cleared.RemoveAll(c => now - c.Day > keep);
        }

        /// <summary>Odleglosc punktu od najblizszej ostatnio rozbitej kryjowki (pamiec) - float.MaxValue, gdy pamiec pusta.</summary>
        private float NearCleared(Settlement st, out string which)
        {
            which = null; float bd = float.MaxValue;
            Vec2 p = st.GetPosition2D;
            foreach (var c in _cleared)
            {
                float d = new Vec2(c.X, c.Y).Distance(p);
                if (d < bd) { bd = d; which = c.Id; }
            }
            return bd;
        }

        private Hideout Pick(Clan faction, Hideout game)
        {
            var s = S;
            EnsureDist();
            PruneCleared();
            _dPicks++;
            float rad = Math.Max(0f, s.HideoutClearedRadius);
            float hard = rad * 0.5f;   // recenzja 21.10: blizej niz pol promienia od rozbitej (w tym ten sam punkt) nigdy, takze przy luzowaniu pamieci
            string cw;
            bool gAway = !AwayOn || Away(game.Settlement);
            bool gFresh = !RelocOn || NearCleared(game.Settlement, out cw) >= rad;
            if (gAway && gFresh)
            {
                _dKept++;
                LogPick(game, game, "wybor gry zostaje", 0);
                return game;
            }
            var culture = game.Settlement.Culture;
            // kandydaci jak w grze: niezajete tej samej kultury
            var cand = new List<Hideout>();
            foreach (var h in Hideout.All)
                if (h != null && h.Settlement != null && !h.IsInfested && h.Settlement.Culture == culture) cand.Add(h);
            var tierA = new List<Hideout>(); var tierB = new List<Hideout>(); var rest = new List<Hideout>();
            int hardOut = 0;
            foreach (var h in cand)
            {
                float dcl = RelocOn ? NearCleared(h.Settlement, out cw) : float.MaxValue;
                if (dcl < hard) { hardOut++; continue; }
                bool away = !AwayOn || Away(h.Settlement);
                bool fresh = dcl >= rad;
                if (away && fresh) tierA.Add(h);
                else if (away) tierB.Add(h);
                rest.Add(h);
            }
            Hideout pick = null; string how; int tier;
            if (tierA.Count > 0) { pick = Weighted(tierA); how = "inne miejsce (wolne, z dala od osad i rozbitych)"; tier = 1; _dSwapped++; }
            else if (tierB.Count > 0)
            {
                // wszystkie wolne punkty w promieniu rozbitych - najdalszy od nich
                float bd = -1f;
                foreach (var h in tierB) { float d = NearCleared(h.Settlement, out cw); if (d > bd) { bd = d; pick = h; } }
                how = "luzuje pamiec rozbitych - najdalszy od nich"; tier = 2; _dLoosened++;
            }
            else
            {
                float bs = -1f;
                foreach (var h in rest)
                {
                    float sc = AwayScore(h.Settlement);
                    if (RelocOn) { float d = NearCleared(h.Settlement, out cw); if (d < rad) sc *= 0.5f; }
                    if (sc > bs) { bs = sc; pick = h; }
                }
                how = "brak wolnego punktu - najdalszy od osad"; tier = 3; if (pick != null) _dNoFit++;
            }
            if (pick == null && hardOut > 0)
            {
                // jedyne wolne punkty kultury leza przy rozbitej (np. kingswood 3/3 zajete, wolna tylko rozbita) - bez nowej kryjowki, gra sprobuje nastepnej doby
                _dNone++;
                float dg = NearCleared(game.Settlement, out cw);
                Log.Info(P + "aktywacja brak (" + (culture != null ? culture.StringId : "?") + ") | wybor gry " + game.Settlement.StringId + " przy rozbitej " + (cw ?? "-") + " "
                         + dg.ToString("0.0", Inv) + ", innego wolnego punktu brak (wolnych " + cand.Count + ", przy rozbitych " + hardOut + ") - bez nowej kryjowki, gra sprobuje znowu"
                         + " | stopien 4 | promien " + rad.ToString("0.#", Inv) + " (twardy " + hard.ToString("0.#", Inv) + "), rozbitych w pamieci " + _cleared.Count);
                return null;
            }
            if (pick == null) { pick = game; how = "brak kandydatow - wybor gry"; tier = 3; }
            LogPick(game, pick, how, tier);
            return pick;
        }

        private Hideout Weighted(List<Hideout> list)
        {
            // waga jak w grze: im dalej od najblizszej zajetej kryjowki, tym chetniej (rozrzut po mapie)
            var w = new float[list.Count]; float sum = 0f;
            for (int i = 0; i < list.Count; i++)
            {
                Vec2 p = list[i].Settlement.GetPosition2D; float bd = 400f;
                foreach (var o in Hideout.All)
                    if (o != null && o.IsInfested && o.Settlement != null) { float d = o.Settlement.GetPosition2D.Distance(p); if (d < bd) bd = d; }
                w[i] = 1f + bd; sum += w[i];
            }
            float r = MBRandom.RandomFloat * sum;
            for (int i = 0; i < list.Count; i++) { r -= w[i]; if (r <= 0f) return list[i]; }
            return list[list.Count - 1];
        }

        private void LogPick(Hideout game, Hideout pick, string how, int tier)
        {
            var st = pick.Settlement;
            float a, b; string cw;
            _dSet.TryGetValue(st, out a); if (!_dSide.TryGetValue(st, out b)) b = -1f;
            float dc = NearCleared(st, out cw);
            var s = S;
            Log.Info(P + "aktywacja " + st.StringId + " (" + (st.Culture != null ? st.Culture.StringId : "?") + ") | " + how + (pick != game ? " zamiast " + game.Settlement.StringId : "")
                     + " | stopien " + tier + " | najblizsza osada " + a.ToString("0.0", Inv) + " (prog " + s.HideoutMinDistSettlement.ToString("0.#", Inv) + "), wioska poboczna "
                     + (b >= 0f && b < 1e6f ? b.ToString("0.0", Inv) : "- brak warstwy") + " (prog " + s.HideoutMinDistSideVillage.ToString("0.#", Inv) + "), od rozbitej "
                     + (cw != null ? cw + " " + dc.ToString("0.0", Inv) : "-") + " (promien " + s.HideoutClearedRadius.ToString("0.#", Inv) + ", rozbitych w pamieci " + _cleared.Count + ")"
                     + " | " + NearInfo(st));
        }

        // ------------------------------------------------------------ zdarzenia
        private void OnSpotted(PartyBase party, PartyBase hideout)
        {
            try
            {
                var st = hideout != null ? hideout.Settlement : null;
                if (st == null || !ChanceOn) return;
                _dSpotted++;
                int r; string why = _roll.TryGetValue(st.StringId ?? "", out r) ? (r == 2 ? "plotka" : r == 1 ? "rzut" : "rzut NIE (?)") : "inne (zadanie, gra)";
                string who; int sc = Scouting(out who);
                Log.Info(P + "wypatrzona " + st.StringId + " | Zwiad " + sc + " (" + who + "), szansa " + (Chance(sc) * 100f).ToString("0.0", Inv) + "%, droga " + why
                         + ", znana z plotki " + (_known.Contains(st.StringId ?? "") ? "TAK" : "NIE"));
            }
            catch (Exception e) { Stumble("OnSpotted", e); }
        }

        private void OnBattle(BattleSideEnum winner, HideoutEventComponent comp, HideoutEventComponent.HideoutBattleEndState end)
        {
            try
            {
                var ev = comp != null ? comp.MapEvent : null;
                var st = ev != null ? ev.MapEventSettlement : null;
                if (st == null || !st.IsHideout) return;
                bool attackerWon = winner == BattleSideEnum.Attacker || end == HideoutEventComponent.HideoutBattleEndState.Victory;
                if (attackerWon) MarkCleared(st, "bitwa");
            }
            catch (Exception e) { Stumble("OnBattle", e); }
        }

        private void OnDeactivated(Settlement st)
        {
            try
            {
                if (st == null) return;
                string id = st.StringId ?? "";
                // recenzja 21.10: chwilowo pusta kryjowka (bandy wyszly) stoi w tym samym miejscu - wiedza z plotki zostaje ("juz wiesz, gdzie szukac");
                // znika dopiero przy rozbiciu (MarkCleared). Wynik rzutu na pobyt - od nowa.
                _roll.Remove(id);
                if (st.LastThreatTime.ElapsedDaysUntilNow <= 2f) MarkCleared(st, "oproznienie po walce");
            }
            catch (Exception e) { Stumble("OnDeactivated", e); }
        }

        private void MarkCleared(Settlement st, string how)
        {
            string id = st.StringId ?? "";
            double now = Today;
            foreach (var c in _cleared) if (c.Id == id && now - c.Day < 1.0) return;
            Vec2 p = st.GetPosition2D;
            _cleared.Add(new Cleared { Id = id, X = p.X, Y = p.Y, Day = now });
            _known.Remove(id); _roll.Remove(id);
            _dCleared++;
            PruneCleared();
            Log.Info(P + "rozbita " + id + " (" + how + ") | pozycja " + p.X.ToString("0.0", Inv) + ", " + p.Y.ToString("0.0", Inv) + " | rozbitych w pamieci " + _cleared.Count
                     + " (" + S.HideoutClearedMemoryDays + " dni, promien " + S.HideoutClearedRadius.ToString("0.#", Inv) + ")");
        }

        private void OnHourly()
        {
            if (!_started)
            {
                _started = true;
                try { StartLine(); } catch (Exception e) { Stumble("StartLine", e); }
            }
            try { HourScan(); } catch (Exception e) { Stumble("HourScan", e); }
        }

        /// <summary>Co godzine (watek glowny): zapominanie wynikow rzutu po odejsciu, migawka zakazanych punktow dla band AI, nowe zajete kryjowki spoza wyboru.</summary>
        private void HourScan()
        {
            var s = S;
            if (s == null) return;
            // 1) recenzja 21.10: wynik rzutu "na pobyt" zapominany po odejsciu dalej niz 1.5 x zasieg - takze gdy kryjowka chwilowo nie jest zajeta
            if (_roll.Count > 0)
            {
                var mp = MobileParty.MainParty; float range = Range();
                if (mp != null && range > 0f)
                {
                    List<string> drop = null;
                    foreach (var kv in _roll)
                    {
                        var st = Settlement.Find(kv.Key);
                        if (st == null || mp.Position.DistanceSquared(st.Position) > 2.25f * range * range) { if (drop == null) drop = new List<string>(); drop.Add(kv.Key); }
                    }
                    if (drop != null) foreach (var id in drop) { _roll.Remove(id); _dForgot++; }
                }
            }
            // 2) i 3) zajete teraz; wolne zakazane dla band AI
            EnsureDist();
            PruneCleared();
            float rad = Math.Max(0f, s.HideoutClearedRadius);
            bool ai = AiOn;
            var inf = new HashSet<string>();
            HashSet<Settlement> block = ai ? new HashSet<Settlement>() : null;
            string cw;
            foreach (var h in Hideout.All)
            {
                var st = h != null ? h.Settlement : null;
                if (st == null) continue;
                if (h.IsInfested) { inf.Add(st.StringId ?? ""); continue; }
                if (!ai) continue;
                bool away = !AwayOn || Away(st);
                bool fresh = !RelocOn || NearCleared(st, out cw) >= rad;
                if (!away || !fresh) block.Add(st);
            }
            _aiBlock = block != null && block.Count > 0 ? block : null;
            if (_prevInf != null)
                foreach (var id in inf)
                    if (!_prevInf.Contains(id) && !_viaPick.Contains(id))
                    {
                        var st = Settlement.Find(id);
                        if (st != null) { _dOutside++; LogOutside(st, "aktywacja poza wyborem", "bandy AI zajely wolny punkt"); }
                    }
            _prevInf = inf;
            _viaPick.Clear();
        }

        /// <summary>Linia kryjowki zajetej poza wyborem Pick (bandy AI, herszt BK): odleglosci i czy zgodna z zasada 2/3.</summary>
        private void LogOutside(Settlement st, string head, string how)
        {
            var s = S;
            EnsureDist();
            float a, b; string cw;
            if (!_dSet.TryGetValue(st, out a)) a = -1f;
            if (!_dSide.TryGetValue(st, out b)) b = -1f;
            float dc = NearCleared(st, out cw);
            bool ok = (!AwayOn || Away(st)) && (!RelocOn || dc >= Math.Max(0f, s.HideoutClearedRadius));
            if (!ok) _dOutsideBad++;
            int bands = 0;
            try { foreach (var mp in st.Parties) if (mp != null && mp.IsBandit) bands++; } catch { }
            Log.Info(P + head + " " + st.StringId + " (" + (st.Culture != null ? st.Culture.StringId : "?") + ") | " + how + ", band w srodku " + bands
                     + " | zgodna z zasada " + (ok ? "TAK" : "NIE") + " | najblizsza osada " + a.ToString("0.0", Inv) + " (prog " + s.HideoutMinDistSettlement.ToString("0.#", Inv)
                     + "), wioska poboczna " + (b >= 0f && b < 1e6f ? b.ToString("0.0", Inv) : "- brak warstwy") + " (prog " + s.HideoutMinDistSideVillage.ToString("0.#", Inv)
                     + "), od rozbitej " + (cw != null ? cw + " " + dc.ToString("0.0", Inv) : "-") + " (promien " + s.HideoutClearedRadius.ToString("0.#", Inv) + ")"
                     + " | bandy AI: klucz " + (AiOn ? "TAK" : "NIE") + " | " + NearInfo(st));
        }

        private void StartLine()
        {
            var s = S;
            if (s == null) return;
            EnsureDist();
            int pool = 0, far = 0, nearSet = 0, nearSide = 0, active = 0, activeNear = 0, spotted = 0;
            foreach (var h in Hideout.All)
            {
                if (h == null || h.Settlement == null) continue;
                pool++;
                float a, b;
                _dSet.TryGetValue(h.Settlement, out a);
                if (!_dSide.TryGetValue(h.Settlement, out b)) b = float.MaxValue;
                bool ok = Away(h.Settlement);
                if (ok) far++;
                if (a < s.HideoutMinDistSettlement) nearSet++;
                if (b < s.HideoutMinDistSideVillage) nearSide++;
                if (h.IsInfested) { active++; if (!ok) activeNear++; }
                if (h.IsSpotted) spotted++;
            }
            string who; int sc = Scouting(out who);
            bool side = _sideRef != null;
            Log.Info(P + "start sesji | klucze: Hideout Scouting Chance " + (s.HideoutScoutingChance ? "TAK" : "NIE") + ", Hideout Rumour Reveals " + (s.HideoutRumourReveals ? "TAK" : "NIE")
                     + ", Hideout Away From Settlements " + (s.HideoutAwayFromSettlements ? "TAK" : "NIE") + ", Hideout Relocate After Clear " + (s.HideoutRelocateAfterClear ? "TAK" : "NIE")
                     + ", Hideout Guide Bandit Ai " + (s.HideoutGuideBanditAi ? "TAK" : "NIE")
                     + " | krzywa: Zwiad <= " + s.HideoutScoutingMin + " -> 0%, " + s.HideoutScoutingMin + ".." + s.HideoutScoutingFull + " liniowo, >= " + s.HideoutScoutingFull + " -> 100% (5: "
                     + Pct(5) + ", 30: " + Pct(30) + ", 100: " + Pct(100) + ", 200: " + Pct(200) + ") | Zwiad teraz " + sc + " (" + who + ") = " + Pct(sc)
                     + " | pula " + pool + ", z dala od osad " + far + ", wykluczonych przez odleglosc " + (pool - far) + " (osada < " + s.HideoutMinDistSettlement.ToString("0.#", Inv)
                     + ": " + nearSet + ", wioska poboczna < " + s.HideoutMinDistSideVillage.ToString("0.#", Inv) + ": " + nearSide + (side ? "" : " - warstwa wiosek niewczytana") + ")"
                     + " | zajetych " + active + " (z zapisu blizej niz prog " + activeNear + "), wypatrzonych " + spotted + " | pamiec: znanych z plotki " + _known.Count + ", rozbitych "
                     + _cleared.Count + " (" + s.HideoutClearedMemoryDays + " dni, promien " + s.HideoutClearedRadius.ToString("0.#", Inv) + "), wynikow rzutu " + _roll.Count
                     + " | " + (_wireNote.Length > 0 ? _wireNote : "latki NIEWPIETE"));
        }

        private static string Pct(int sc) { return (Chance(sc) * 100f).ToString("0.0", Inv) + "%"; }

        private void OnDaily()
        {
            try
            {
                if (!_started) { _started = true; StartLine(); }
                PruneCleared();
                Log.Info(P + "dzien " + ((int)Today).ToString(Inv) + " | rzuty " + _dRolls + " (udane " + _dPass + "), blokady wzrok " + _dBlockHour + ", banda wchodzi " + _dBlockEnter
                         + ", przepustki z plotki " + _dKnownPass + ", plotki o kryjowkach " + _dRumour + ", wypatrzone " + _dSpotted + " | aktywacje " + _dPicks + " (gry zostaje " + _dKept
                         + ", inne miejsce " + _dSwapped + ", luzowana pamiec " + _dLoosened + ", bez wolnego " + _dNoFit + ", brak miejsca " + _dNone + "), rozbite " + _dCleared
                         + " | poza wyborem " + _dOutside + " (niezgodne " + _dOutsideBad + "), BK " + _dBk + ", bandy AI odciete " + Interlocked.Exchange(ref _aiCut, 0)
                         + " (bledy " + Interlocked.Exchange(ref _aiErr, 0) + ", zakazanych punktow " + (_aiBlock != null ? _aiBlock.Count : 0) + "), zapomniane rzuty " + _dForgot
                         + " | pamiec: znanych " + _known.Count + ", rozbitych " + _cleared.Count + ", wynikow " + _roll.Count + " | potkniecia " + _stumbles);
                _dRolls = _dPass = _dBlockHour = _dBlockEnter = _dRumour = _dKnownPass = _dPicks = _dKept = _dSwapped = _dLoosened = _dNoFit = _dSpotted = _dCleared = 0;
                _dNone = _dOutside = _dOutsideBad = _dBk = _dForgot = 0;
            }
            catch (Exception e) { Stumble("OnDaily", e); }
        }
    }
}
