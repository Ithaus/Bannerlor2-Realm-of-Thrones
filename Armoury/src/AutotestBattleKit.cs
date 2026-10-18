using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// f5-bitwa (noc 17/18.10) - ZESTAW BITWY AUTOTESTU. Klucz AutotestBattleKit; dziala TYLKO w biegu autotestu CrashScribe (CrashScribe.Autotest.Active,
    /// etap Battle). Recenzja: typ CrashScribe.Autotest bywa tez w zainstalowanym CrashScribe.dll - w grze Jeffa nieczynne trzyma flaga Active (ustawiana
    /// tylko przy pliku autotest.json), nie brak typu.
    ///
    /// DOWOD (STAN-PRAC 55 z 16.10: "mocna druzyna przegrywa z bandytami, panika 53-58" - nikt nie sprawdzil): we WSZYSTKICH 15 bitwach autotestu
    /// od 16.10 18:25 do 18.10 04:04 (logi-testow) linia "Bitwa gracza (sklad7b-p, start): Twoich ludzi 59 - bez broni 59, bez zbroi 59, jezdzcy pieszo 59"
    /// i "Underequipped (E8/P3) ... druzyna gracza ... 12-20: 59, srednia kara 20.0". Trafienia w czempionow maja "wchloniete=0" (bez zbroi).
    /// Druzyna testu (34 ludzi zapisu autotest-F-d240 + 25 x vlandian_champion dolozonych przez AutotestBattle.StepSpawn przez
    /// MemberRoster.AddToCounts) walczy GOLYMI PIESCIAMI: zbrojownia gracza w zapisie testu jest pusta, a od sklad7b (TroopsFightWithOwnKitOnly,
    /// decyzja Jeffa 09.10 "nic nie dostaje z kosmosu") druzyna gracza walczy tylko tym, co ma zbrojownia DTE (ArmyArmory.Armory). Do tego DTE
    /// (PartyEquipmentDistributor.MarkUnderEquippedAssignmentsIfEnabled) daje KAZDEMU kare morale 20 (2 za brakujacy tier, sufit 20) - start morale
    /// 15-45 zamiast 35-65 - a smierc i omdlenie sasiada w 4 m (AgentMoraleInteractionLogic, gra) zdejmuje reszte. To nie nasza panika: NB2 zlamal
    /// 14 rannych (BrokenMen 9, FieldCraft 5) z 51 paniki gry. W grze Jeffa ludzie maja sprzet (jego logi 15-17.10: "bez broni 0", kara 0.2-3.4).
    ///
    /// Gracz werbujacy ludzi dostaje ich komplet do zbrojowni (DTE RecruitmentPatch.AddStartingEquipmentToPlayerArmory - PlayerRecruit); test
    /// dokladal ludzi bez tej drogi. TERAZ (tylko autotest): przy inicjalizacji rozdzielaczy DTE (prefiks DynamicTroopMissionLogic.TryInitializeDistributors,
    /// PRZED migawka zbrojowni w PartyEquipmentDistributor.RunAsync) zbrojownia gracza dostaje brakujace sztuki wzorcow zdrowych ludzi druzyny
    /// (zestawy BattleEquipments po kolei na czlowieka - DTE wybiera wzorzec losowo, RandomBattleEquipment; bron, zbroja, kon i uprzaz - bez choragwi,
    /// legend i olbrzymow, jak GatherFallen; bez koni w bitwie morskiej) przez DTE ArmyArmory.AddItemToArmory (czarna lista DTE i prawo unikatow
    /// dzialaja - odmowy liczone). Plan "slaba" (AutotestBattle._kind = Weak - test przegranej i niewoli) bez zestawu. Po bitwie (MapEventEnded tej
    /// bitwy, PO GatherFallen; misja otwarta - pierwsza godzina albo wjazd do osady PRZED MenPurse.OnEntered) zdjete najwyzej tyle, ile dodano
    /// (najpierw egzemplarze bez stanu), ksiega gracza przycieta (ReconcileStock), linia z tozsamoscia "dodane = zdjete + ubytek".
    /// KOLEJNOSC SLUCHACZY (recenzja 18.10, dekompilacja MbEvent: AddNonSerializedListener wstawia na poczatek listy, Invoke idzie od poczatku - sluchacz
    /// dopisany POZNIEJ biegnie WCZESNIEJ): MapEventEnded zarejestrowany PRZED delegatem GatherFallen+WearKeep+AmmoRecovery (biegnie po nim - ksiega po
    /// GatherFallen przycieta, odzyskana amunicja zestawu zdjeta); SettlementEntered zarejestrowany PO MenPurse.OnEntered i PO Orders+Stables+SaddleLaw
    /// (biegnie przed nimi - nadwyzki zestawu nie ida na targ); HeroPrisonerTaken zarejestrowany PO CaptiveArmory.OnPrisonerTaken (biegnie przed nim).
    /// POJMANIE GRACZA (recenzja): gra bierze gracza do niewoli w MapEvent.CaptureDefeatedPartyMembers (CalculateAndCommitMapEventResults, stan
    /// ApplyResults spotkania) - PRZED FinalizeEvent i MapEventEnded; CaptiveArmory oddaje wtedy CALA zbrojownie zdobywcy. Zestaw schodzi wiec juz przy
    /// HeroPrisonerTaken gracza (nic z testu nie trafia do swiata), a ksiega jest przycinana drugi raz w MapEventEnded po GatherFallen.
    /// UBYTEK (recenzja): sztuki zniszczone w bitwie (polegle konie, wystrzelana amunicja). Gdy zbrojownia miala juz sztuki tego samego id (B > 0),
    /// strata id dzielona proporcjonalnie: na zestaw dodane / (bylo + dodane) straty, reszta na sztuki gracza (osobna liczba w linii).
    /// Nic nie trafia do zapisu (stan statyczny, czyszczony z kazda kampania); bitwa testu jest przed pierwsza doba biegu, wiec ksiega uzbrojenia
    /// (ArmsBook - stan wyjsciowy w pierwszej dobie) i ksiega koni nie widza zestawu.
    /// Wyjatek na bitwe liczony (potkniecia), funkcji nie gasimy (CLAUDE.md 7).
    /// </summary>
    internal static class AutotestBattleKit
    {
        private const string P = "Bitwa autotestu (f5-bitwa): ";
        private static FieldInfo _fInit;            // DTE DynamicTroopMissionLogic._areDistributorsInitialized
        private static MethodInfo _add;             // DTE ArmyArmory.AddItemToArmory(ItemObject, int)
        private static string _wired = "nie wpiete";
        // autotest CrashScribe (refleksja raz na proces; w grze Jeffa typu nie ma)
        private static bool _atLooked;
        private static FieldInfo _fActive, _fNow, _fKind;
        // biezaca bitwa testu
        private static MapEvent _event;             // bitwa, dla ktorej zestaw juz byl (albo odmowa planu "slaba")
        private static readonly Dictionary<string, int> _added = new Dictionary<string, int>();
        private static int _addedTotal, _stumbles;
        private static string _plan = "?";
        private static BattleSideEnum _side = BattleSideEnum.None;
        private static bool _pending;               // zestaw w zbrojowni, czeka na zdjecie
        // recenzja 18.10: stan id przed dolozeniem (proporcja strat), bitwa do drugiego przyciecia ksiegi po GatherFallen (zdjecie przy pojmaniu),
        // klucz misji ostatniej linii "pominiete" (raz na misje - DTE wola TryInitializeDistributors przy kazdym spawnie, poki nie zainicjuje)
        private static readonly Dictionary<string, int> _before = new Dictionary<string, int>();
        private static MapEvent _reconcileEvent;
        private static object _skipKey;

        // ------------------------------------------------------------ latka (start gry)

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var tLogic = AccessTools.TypeByName("DynamicTroopEquipmentReupload.DynamicTroopMissionLogic");
                var tArm = AccessTools.TypeByName("DynamicTroopEquipmentReupload.ArmyArmory");
                if (tLogic == null || tArm == null) { _wired = "BRAK (DTE nieobecne)"; }
                else
                {
                    _fInit = AccessTools.Field(tLogic, "_areDistributorsInitialized");
                    _add = AccessTools.Method(tArm, "AddItemToArmory", new[] { typeof(ItemObject), typeof(int) });
                    // metoda zadeklarowana w DynamicTroopMissionLogic (nie dziedziczona) - DeclaredMethod, ten sam MethodInfo co postfiks GarrisonKit
                    var m = AccessTools.DeclaredMethod(tLogic, "TryInitializeDistributors");
                    if (_fInit == null || _add == null || m == null) _wired = "BRAK (zmienione DTE)";
                    else
                    {
                        h.Patch(m, prefix: new HarmonyMethod(typeof(AutotestBattleKit), nameof(InitPrefix)));
                        _wired = "wpieta";
                    }
                }
            }
            catch (Exception e) { _wired = "BRAK (" + e.Message + ")"; Log.Error("AutotestBattleKit.ApplyAll", e); }
            var s = Settings.Current;
            Log.Info(P + "latka DTE TryInitializeDistributors (prefiks) " + _wired + " | klucz AutotestBattleKit " + (s != null && s.AutotestBattleKit ? "TAK" : "NIE")
                     + " | dziala tylko w DLL autotestu CrashScribe (etap bitew): brakujace sztuki wzorcow zdrowych ludzi do zbrojowni gracza przed bitwa"
                     + " (plan mocna i odwrot; slaba bez), po bitwie zdjete najwyzej tyle samo; w grze nieczynne.");
        }

        /// <summary>Nowa kampania (konstruktor ArmouryBehavior): stan poprzedniej kampanii nieaktualny.</summary>
        internal static void Reset()
        {
            _event = null; _added.Clear(); _addedTotal = 0; _plan = "?"; _side = BattleSideEnum.None; _pending = false;
            _before.Clear(); _reconcileEvent = null; _skipKey = null;
        }

        // ------------------------------------------------------------ autotest CrashScribe

        private static void LookAutotest()
        {
            if (_atLooked) return;
            _atLooked = true;
            try
            {
                foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (asm.GetName().Name != "CrashScribe") continue;
                    var bf = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
                    Type t = asm.GetType("CrashScribe.Autotest", false);
                    _fActive = t != null ? t.GetField("Active", bf) : null;
                    if (_fActive != null && _fActive.FieldType != typeof(bool)) _fActive = null;
                    _fNow = t != null ? t.GetField("Now", bf) : null;
                    Type b = asm.GetType("CrashScribe.AutotestBattle", false);
                    _fKind = b != null ? b.GetField("_kind", bf) : null;
                    break;
                }
            }
            catch { _fActive = null; _fNow = null; _fKind = null; }
        }

        /// <summary>Autotest CrashScribe w etapie bitew (Now nieznane = sam Active).</summary>
        private static bool AutotestBattleStage()
        {
            LookAutotest();
            if (_fActive == null) return false;
            try
            {
                if (!(bool)_fActive.GetValue(null)) return false;
                if (_fNow == null) return true;
                var now = _fNow.GetValue(null);
                return now == null || now.ToString() == "Battle";
            }
            catch { return false; }
        }

        /// <summary>Plan biezacej bitwy testu (AutotestBattle._kind): Strong / Retreat / Weak; "?" - nie wiadomo.</summary>
        private static string Plan()
        {
            try { var v = _fKind != null ? _fKind.GetValue(null) : null; return v != null ? v.ToString() : "?"; }
            catch { return "?"; }
        }

        private static string PlanName(string k)
        {
            return k == "Strong" ? "mocna" : k == "Retreat" ? "odwrot" : k == "Weak" ? "slaba" : "?";
        }

        // ------------------------------------------------------------ przed bitwa

        /// <summary>Prefiks DTE DynamicTroopMissionLogic.TryInitializeDistributors: przed migawka zbrojowni gracza (RunAsync) - zestaw testu.</summary>
        public static void InitPrefix(object __instance)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.AutotestBattleKit || __instance == null || _fInit == null) return;
                if (!AutotestBattleStage()) return;                            // w grze Jeffa: typ autotestu nieobecny - koniec od razu
                if ((bool)_fInit.GetValue(__instance)) return;                 // rozdzielacze juz sa - DTE wola to przy kazdym spawnie
                var me = MapEvent.PlayerMapEvent;
                var main = MobileParty.MainParty;
                var mb = __instance as MissionBehavior;
                var mission = mb != null ? mb.Mission : null;
                if (me == null || main == null || main.MemberRoster == null || mission == null)
                {
                    // recenzja: w etapie bitew autotestu cichy powrot dawal w checkerze "stary uklad testu" zamiast NIE - linia raz na misje
                    object key = (object)mission ?? "brak misji";
                    if (!Equals(key, _skipKey))
                    {
                        _skipKey = key;
                        Log.Info(P + "pominiete (etap bitew autotestu): " + (me == null ? "brak bitwy na mapie (PlayerMapEvent)" : mission == null ? "brak misji" : "brak druzyny gracza")
                                 + " - bez zestawu w tej misji.");
                    }
                    return;
                }
                if (ReferenceEquals(me, _event)) return;                       // raz na bitwe
                if (_pending) TakeBack("nowa bitwa przed zdjeciem poprzedniego zestawu", null);
                _event = me;
                string plan = Plan();
                if (plan == "Weak")
                {
                    Log.Info(P + "plan slaba - bez zestawu (test przegranej i niewoli: ludzie walcza tym, co ma zbrojownia, jak dotad).");
                    return;
                }
                Stock(main, mission, plan);
            }
            catch (Exception e) { Stumble("InitPrefix", e); }
        }

        private static void Stock(MobileParty main, Mission mission, string plan)
        {
            var armory = QuartermasterLaw.DteArmory();
            if (armory == null || _add == null) { Log.Info(P + "brak zbrojowni DTE gracza - bez zestawu."); return; }
            bool mounts = !mission.IsNavalBattle && !mission.IsNavalRaidBattle;   // jak DTE (_missionAllowsMountEquipment)
            var need = new Dictionary<string, int>();
            var items = new Dictionary<string, ItemObject>();
            int men = 0, troops = 0, horses = 0, harness = 0, skipped = 0;
            var roster = main.MemberRoster;
            for (int i = 0; i < roster.Count; i++)
            {
                var el = roster.GetElementCopyAtIndex(i);
                var ch = el.Character;
                if (ch == null || ch.IsHero) continue;
                int healthy = el.Number - el.WoundedNumber;
                if (healthy <= 0) continue;
                var sets = new List<Equipment>();
                foreach (var e in ch.BattleEquipments) if (e != null) sets.Add(e);
                if (sets.Count == 0 && ch.FirstBattleEquipment != null) sets.Add(ch.FirstBattleEquipment);
                if (sets.Count == 0) continue;
                troops++;
                men += healthy;
                for (int k = 0; k < healthy; k++)
                {
                    var eq = sets[k % sets.Count];
                    for (int slot = 0; slot < 12; slot++)
                    {
                        if (slot == (int)EquipmentIndex.ExtraWeaponSlot) continue;
                        if (!mounts && (slot == (int)EquipmentIndex.Horse || slot == (int)EquipmentIndex.HorseHarness)) continue;
                        var it = eq[(EquipmentIndex)slot].Item;
                        if (it == null || it.StringId == null) continue;
                        // unikat: prawo unikatow (UniqueLaw.IntakePlayer) podmienia go w drzwiach zbrojowni na zamiennik innego id - nie dalby sie zdjac po bitwie
                        if (it.ItemType == ItemObject.ItemTypeEnum.Banner || it.StringId.StartsWith("dragon_") || LegendaryLaw.IsLegend(it) || GiantGear.Is(it) || UniqueGear.Is(it)) { skipped++; continue; }
                        int v; need.TryGetValue(it.StringId, out v);
                        need[it.StringId] = v + 1;
                        items[it.StringId] = it;
                        if (slot == (int)EquipmentIndex.Horse) horses++;
                        else if (slot == (int)EquipmentIndex.HorseHarness) harness++;
                    }
                }
            }
            var have = CountById(armory);
            int needTotal = 0, covered = 0, added = 0, refused = 0;
            _added.Clear(); _addedTotal = 0; _before.Clear();
            foreach (var kv in need)
            {
                needTotal += kv.Value;
                int h; have.TryGetValue(kv.Key, out h);
                int cov = Math.Min(h, kv.Value);
                covered += cov;
                int want = kv.Value - cov;
                if (want <= 0) continue;
                int before = CountOne(armory, kv.Key);
                try { _add.Invoke(null, new object[] { items[kv.Key], want }); }
                catch (Exception e) { Stumble("AddItemToArmory", e); }
                int got = Math.Max(0, Math.Min(want, CountOne(armory, kv.Key) - before));
                added += got;
                refused += want - got;
                if (got > 0) { _added[kv.Key] = got; _addedTotal += got; _before[kv.Key] = before; }
            }
            _plan = plan;
            _side = main.Party != null ? main.Party.Side : BattleSideEnum.None;
            _pending = _addedTotal > 0;
            Log.Info(P + "zestaw na bitwe | plan " + PlanName(plan) + " | ludzi zdrowych " + men + " w " + troops + " oddzialach | potrzeba " + needTotal
                     + " szt. (konie " + horses + ", uprzaz " + harness + "), bylo w zbrojowni " + covered + ", dodane " + added + ", odmowy DTE " + refused
                     + " | tozsamosc: potrzeba = bylo + dodane + odmowy (" + needTotal + " = " + covered + " + " + added + " + " + refused + ")"
                     + " | konie w misji " + (mounts ? "TAK" : "NIE") + ", pominiete (choragwie, legendy, olbrzymy, unikaty) " + skipped
                     + " | po bitwie zdjete najwyzej " + _addedTotal + " (tylko autotest).");
        }

        // ------------------------------------------------------------ po bitwie

        /// <summary>MapEventEnded (sluchacz zarejestrowany PRZED delegatem GatherFallen - biegnie PO nim): bitwa testu skonczona - zestaw zdjety; misja
        /// jeszcze otwarta - pierwsza godzina. Zestaw zdjety wczesniej przy pojmaniu gracza - tu drugie przyciecie ksiegi (po GatherFallen).</summary>
        internal static void OnMapEventEnded(MapEvent me)
        {
            try
            {
                if (me != null && _reconcileEvent != null && ReferenceEquals(me, _reconcileEvent))
                {
                    _reconcileEvent = null;
                    try { ArmouryBehavior.ReconcileStock("autotest-bitwa po GatherFallen"); } catch (Exception e) { Stumble("ReconcileStock", e); }
                    Log.Info(P + "ksiega gracza przycieta drugi raz po GatherFallen (zestaw zdjety wczesniej, przy pojmaniu); ReconcileStock nie tnie przy pustej zbrojowni"
                             + " (po oddaniu jej zdobywcy) - wtedy przy nastepnym otwarciu zbrojowni albo w miescie.");
                    return;
                }
                if (!_pending || me == null || !ReferenceEquals(me, _event)) return;
                if (Mission.Current != null) { Log.Info(P + "koniec bitwy przy otwartej misji - zdjecie zestawu w pierwszej godzinie."); return; }
                TakeBack("koniec bitwy", me);
            }
            catch (Exception e) { Stumble("OnMapEventEnded", e); }
        }

        /// <summary>Godzina: zestaw czekajacy (koniec bitwy przy otwartej misji) zdjety, gdy misji juz nie ma.</summary>
        internal static void Hourly() { Due("pierwsza godzina po bitwie"); }

        /// <summary>Wjazd druzyny gracza do osady: zestaw czekajacy zdjety PRZED sprzedaza nadwyzek ludzi (MenPurse.OnEntered) - sluchacz zarejestrowany PO
        /// MenPurse i Orders+Stables+SaddleLaw, wiec biegnie przed nimi.</summary>
        internal static void OnMainEntered() { Due("wjazd do osady przed sprzedaza nadwyzek"); }

        /// <summary>HeroPrisonerTaken (sluchacz zarejestrowany PO CaptiveArmory - biegnie przed nim): pojmany gracz - zestaw zdjety, zanim CaptiveArmory
        /// odda zbrojownie zdobywcy. Gra bierze gracza do niewoli przy rozliczeniu bitwy (CaptureDefeatedPartyMembers), PRZED MapEventEnded.</summary>
        internal static void OnPrisonerTaken(PartyBase capturer, Hero prisoner)
        {
            try
            {
                if (!_pending || prisoner == null || prisoner != Hero.MainHero) return;
                var me = _event;
                bool open = Mission.Current != null;
                TakeBack("pojmanie gracza, przed oddaniem zbrojowni zdobywcy" + (open ? ", misja otwarta" : ""), me);
                _reconcileEvent = me;   // GatherFallen (MapEventEnded) ksieguje komplety poleglych i pojmanych pozniej - drugie przyciecie ksiegi
            }
            catch (Exception e) { Stumble("OnPrisonerTaken", e); }
        }

        private static void Due(string why)
        {
            try
            {
                if (!_pending || Mission.Current != null) return;
                var me = _event;
                if (me != null && MapEvent.PlayerMapEvent != null && ReferenceEquals(MapEvent.PlayerMapEvent, me)) return;   // bitwa trwa
                TakeBack(why, me);
            }
            catch (Exception e) { Stumble("Due", e); }
        }

        private static void TakeBack(string why, MapEvent me)
        {
            _pending = false;
            var armory = QuartermasterLaw.DteArmory();
            int removed = 0, lost = 0, ownLost = 0;
            if (armory != null)
            {
                // kopia elementow, potem zdejmowanie - zmiana rostera w petli przesuwa indeksy
                var els = new List<ItemRosterElement>();
                for (int i = 0; i < armory.Count; i++) { var el = armory.GetElementCopyAtIndex(i); if (el.Amount > 0 && el.EquipmentElement.Item != null) els.Add(el); }
                var nowById = CountById(armory);
                foreach (var kv in _added)
                {
                    // recenzja: strata id dzielona proporcjonalnie miedzy zestaw i sztuki gracza tego samego id (bylo B, dodane A, jest N):
                    // strata = B + A - N; na zestaw round(strata x A / (B + A)), reszta na gracza; zdjete = A - strata zestawu (najwyzej N)
                    int add = kv.Value, bef, now;
                    _before.TryGetValue(kv.Key, out bef);
                    nowById.TryGetValue(kv.Key, out now);
                    int expect = bef + add, take = add;
                    if (now < expect)
                    {
                        int loss = expect - now;
                        int kitLoss = Math.Max(0, Math.Min(add, (int)Math.Round((double)loss * add / Math.Max(1, expect))));
                        take = add - kitLoss;
                        ownLost += Math.Max(0, Math.Min(bef, loss - kitLoss));
                    }
                    take = Math.Max(0, Math.Min(take, now));
                    int left = take;
                    for (int pass = 0; pass < 2 && left > 0; pass++)
                        foreach (var el in els)
                        {
                            if (left <= 0) break;
                            var ee = el.EquipmentElement;
                            if (ee.Item.StringId != kv.Key || (pass == 0) != (ee.ItemModifier == null)) continue;   // najpierw egzemplarze bez stanu
                            int t = Math.Min(left, el.Amount);
                            if (t <= 0) continue;
                            armory.AddToCounts(ee, -t);
                            left -= t; removed += t;
                        }
                    lost += add - (take - left);
                }
            }
            else lost = _addedTotal;
            try { ArmouryBehavior.ReconcileStock("autotest-bitwa"); } catch (Exception e) { Stumble("ReconcileStock", e); }
            string result = "?";
            try
            {
                if (me != null && _side != BattleSideEnum.None)
                {
                    if (me.HasWinner && me.WinningSide == _side) result = "wygrana";
                    else if (me.HasWinner && me.DefeatedSide == _side) result = "przegrana";
                    else result = "bez rozstrzygniecia";
                }
            }
            catch { result = "?"; }
            var sb = new StringBuilder(P);
            sb.Append("zestaw zdjety po bitwie (").Append(why).Append(") | plan ").Append(PlanName(_plan)).Append(" | wynik ").Append(result)
              .Append(" | dodane ").Append(_addedTotal).Append(" = zdjete ").Append(removed).Append(" + ubytek ").Append(lost)
              .Append(" (zniszczone w bitwie: polegle konie, wystrzelana amunicja) | strata sztuk gracza tego samego id (proporcja) ").Append(ownLost)
              .Append(" | tozsamosc ").Append(_addedTotal == removed + lost ? "TAK" : "NIE")
              .Append(" | ksiega gracza przycieta (ReconcileStock) | potkniecia (sesja) ").Append(_stumbles).Append('.');
            Log.Info(sb.ToString());
            _added.Clear(); _addedTotal = 0; _before.Clear();
        }

        // ------------------------------------------------------------ pomocnicze

        private static Dictionary<string, int> CountById(ItemRoster r)
        {
            var d = new Dictionary<string, int>();
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                var it = el.EquipmentElement.Item;
                if (it == null || it.StringId == null || el.Amount <= 0) continue;
                int v; d.TryGetValue(it.StringId, out v);
                d[it.StringId] = v + el.Amount;
            }
            return d;
        }

        private static int CountOne(ItemRoster r, string id)
        {
            int n = 0;
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                var it = el.EquipmentElement.Item;
                if (it != null && it.StringId == id && el.Amount > 0) n += el.Amount;
            }
            return n;
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_stumbles <= 3) Log.Error("AutotestBattleKit." + where, e);
        }
    }
}
