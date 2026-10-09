using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace CrashScribe
{
    /// <summary>
    /// 175c ZASADA STALI INNYCH (decyzja Jeffa 09.10 ok. 07:45, 177-1b; projekt 175 rozdz. 13): "pelne obrazenia Bialym Wedrowcom
    /// i Nocnemu Krolowi tylko od stali valyrianskiej, smoczego szkla i smoczego ognia; bron t6 (stal zamkowa) np. 50%, reszta 15%
    /// (jak w ksiazkach; Inni silniejsi - zmierzyc ich pochod/kalendarz)". Zastepuje zasade 30.08 "tier 6 = stal valyrianska".
    ///
    /// KOGO CHRONI (bez zmian, Mends.WalkerBlood): Nocny Krol (szablon ROTuniqueleader_whitewalker), lordowie Innych z szablonow
    /// whitewalker2/3/4, lordowie wskrzeszeni przez ROT ("has risen again as a White Walker"), gracz-Inny, notable przemienieni
    /// (nie walcza). Zwykle wighty (kultura whitewalker, nie bohater) padaja od wszystkiego jak dotad.
    ///
    /// KLASA CIOSU (kolejnosc, pierwsza pasujaca wygrywa):
    ///  1. OGIEN SMOKA - pelne w obu trybach (przywraca decyzje 31.08): agent-smok (MonsterUsage "dragon"/"dragonfly") albo jezdziec
    ///     smoka bez broni w ciosie (ROT tworzy cios ognia jako new Blow(jezdziec.Index) z pustym WeaponRecord -
    ///     ROTDragonMissionBehavior.FireTickInternal/FireTickBody; dotad lapal sie na 15%: przy DragonDamageScaling 10 ogien zadawal
    ///     Wedrowcowi 1 punkt). W symulacji: bijacy z koniem-smokiem w slocie 10 (ROT i tak nadpisuje wynik PO nas).
    ///  2. STAL VALYRIANSKA albo SMOCZE SZKLO w broni (melee), w broni rzucanej albo w amunicji (przedmiot pocisku z prywatnego
    ///     Mission._missilesDictionary - gra czyta go zaraz po victim.RegisterBlow, Mission.RegisterBlow 1.4.8) - pelne.
    ///     Tylko przy wlaczonej zasadzie (OthersSteelRule); wylaczona = stara regula wedlug tieru.
    ///  3. TIER NARZEDZIA >= 6 (stal zamkowa; melee - przedmiot ze slotu, pocisk - luk/kusza/oszczep w rece, decyzja 02.09) -
    ///     OthersCastleSteelPercent (dom. 50) przy wlaczonej zasadzie, 100% przy wylaczonej (jak dotad).
    ///  4. RESZTA (bron ponizej t6) i GOLE RECE (piesc, kopyto, brak przedmiotu) - 15%, min 1 (jak dotad).
    /// Symulacja (autobitwa): bijacy-bohater - jego zestaw bojowy (sloty 0-3); zolnierz - bron z migawki 175 sprzed zamiany
    /// (Army175.PreWeapons) i tier z Army175.PreTierBest BEZ ZMIANY definicji (neutralnie wobec 175; "t6 bez amunicji i proc" to
    /// osobna decyzja). Uwaga: wedlug RBM t6 maja tez proce, bolt_a i woodland_longbow - w symulacji "t6" to nie zawsze stal.
    ///
    /// LISTY: stal valyrianska - 29 wzorow z rejestru 177 (kopia ponizej, "AKTUALIZUJ OBIE RAZEM"); gdy Armoury ma juz
    /// ValyrianBlades (177 scalone) - lista Armoury, w logu startu zgodnosc z kopia (wzor Army175.EssosCheck). Smocze szklo - id
    /// przedmiotu albo kawalka klingi z WeaponDesign (kopie kute przez gracza) zawiera "dragonglass" albo "obsidian" (te same slowa
    /// co Armoury WorkshopForbiddenIds); w ROT jest jeden taki przedmiot (dragonglass_axe, freefolk, t5) i nikt go nie nosi.
    ///
    /// USTAWIENIA (Armoury, Mends.ArmouryFloat): OthersSteelRule (dom. TAK), OthersCastleSteelPercent (15-100, dom. 50). Czytane przy
    /// starcie sesji, raz na dobe i przy pierwszym ciosie w Wedrowca w nowej misji - nigdy per cios (ArmouryFloat przeglada assembly).
    /// Bez Armoury: wlaczone, 50.
    ///
    /// POMIAR (tylko log): linia "Inni (175c): bitwa ..." po kazdej bitwie z Innymi (strona z partia Innych - jak NightKingGate - albo
    /// z ciosami w Wedrowcow) i linia dobowa "Inni (175c) dzien N". Liczniki: pole - kubelek misji przypiety do MapEvent gracza z chwili
    /// pierwszego ciosu; symulacja - per MapEvent z parametru SimulateHit (battle). Bez zapisu w grze (bez SyncData).
    /// Per cios: catch bez wylacznika globalnego (CLAUDE.md 7); potkniecia liczone w linii dobowej.
    /// </summary>
    internal static class OthersSteel
    {
        internal const int KVs = 0, KGlass = 1, KFire = 2, KT6 = 3, KRest = 4, KBare = 5, KN = 6;
        private const int RestPct = 15;

        internal static bool RuleOn = true;
        internal static int CastlePct = 50;

        // AKTUALIZUJ OBIE RAZEM: kopia Armoury ValyrianBlades.Named + Serial (paczka 177, galaz w-toku/177-stal-valyrianska,
        // Armoury/src/ValyrianBlades.cs) - 19 nazwanych + 10 seryjnych "Valyrian Steel Sword" (Qohor przekuwa na seryjne).
        private static readonly string[] VsIds =
        {
            "ice_sword", "longclaw_sword", "brightroar", "brightroar2", "blackfyre", "heartsbane", "nightfall", "whyt_sword", "assist_sword",
            "oathkeeper_sword", "ww2_sword", "darksister", "lady_forlorn2", "lady_forlorn", "lamentation", "red_rain", "vigilance_sword", "truth",
            "celtigar_axe",
            "koa_sword_tier_5", "val_steel_sword_2", "val_steel_sword_3", "val_steel_sword_4", "val_steel_sword_5", "val_steel_sword_6",
            "val_steel_sword_7", "val_steel_sword_8", "val_steel_sword_blue", "val_steel_sword_red"
        };
        private static HashSet<string> _vs = new HashSet<string>(VsIds, StringComparer.Ordinal);
        private static string _vsSource = "kopia CS (29)";
        private static readonly string[] GlassWords = { "dragonglass", "obsidian" };

        private static readonly object _gate = new object();
        private static readonly Dictionary<ItemObject, int> _itemCls = new Dictionary<ItemObject, int>(new RefEq<ItemObject>());       // 0 zwykla, 1 VS, 2 szklo
        private static readonly Dictionary<CharacterObject, int> _unitCls = new Dictionary<CharacterObject, int>(new RefEq<CharacterObject>()); // zolnierze (migawka)
        private static FieldInfo _fMissiles;
        private static bool _fMissilesTried;

        private sealed class RefEq<T> : IEqualityComparer<T> where T : class
        {
            public bool Equals(T a, T b) { return ReferenceEquals(a, b); }
            public int GetHashCode(T o) { return RuntimeHelpers.GetHashCode(o); }
        }

        private sealed class Tally
        {
            internal readonly int[] N = new int[KN];
            internal readonly double[] Pre = new double[KN], Post = new double[KN];
            internal int Hits { get { int s = 0; for (int i = 0; i < KN; i++) s += N[i]; return s; } }
            internal void Add(int k, double pre, double post) { N[k]++; Pre[k] += pre; Post[k] += post; }
            internal void AddAll(Tally o)
            {
                if (o == null) return;
                for (int i = 0; i < KN; i++) { N[i] += o.N[i]; Pre[i] += o.Pre[i]; Post[i] += o.Post[i]; }
            }
            internal void Clear() { for (int i = 0; i < KN; i++) { N[i] = 0; Pre[i] = 0; Post[i] = 0; } }
        }

        private sealed class Battle
        {
            internal readonly Tally Sim = new Tally();
            internal readonly HashSet<Hero> Walkers = new HashSet<Hero>(new RefEq<Hero>());
        }

        private static readonly Dictionary<MapEvent, Battle> _battles = new Dictionary<MapEvent, Battle>(new RefEq<MapEvent>());
        // pole: kubelek biezacej misji, przypiety do MapEvent gracza z chwili pierwszego ciosu w Wedrowca
        private static readonly Tally _field = new Tally();
        private static readonly HashSet<Hero> _fieldWalkers = new HashSet<Hero>(new RefEq<Hero>());
        private static WeakReference _fieldEvent, _lastMission;
        // doba
        private static readonly Tally _dayField = new Tally(), _daySim = new Tally();
        private static int _dayBattles, _dayPlayer, _dayOthersWins, _dayOthersLosses, _dayWalkers, _dayWounded, _dayDead, _dayCaptured,
                           _dayOthDead, _dayOthWounded, _dayFoeDead, _dayFoeWounded, _dayLoose, _stumbles;

        // ------------------------------------------------------------ ustawienia

        /// <summary>Odczyt suwakow z Armoury (raz: start sesji, doba, nowa misja). full = tez czysci pamiec klas jednostek
        /// (migawka 175 moze dojsc w dobie DailyCatchUp).</summary>
        internal static void Refresh(bool full)
        {
            try
            {
                RuleOn = Mends.ArmouryFloat("OthersSteelRule", 1f) >= 0.5f;
                int p = (int)Math.Round(Mends.ArmouryFloat("OthersCastleSteelPercent", 50f));
                CastlePct = p < 15 ? 15 : (p > 100 ? 100 : p);
                if (full) lock (_gate) _unitCls.Clear();
            }
            catch { }
        }

        /// <summary>Opis dzialania tieru 6 do linii 175 (migawka valyrianska, kontrola) - wolane raz przy wczytaniu, wiec suwaki
        /// czytane na swiezo (linia kontroli idzie przed OnSession).</summary>
        internal static string T6Text()
        {
            Refresh(false);
            return RuleOn ? "t6 = " + CastlePct + "% przy zasadzie stali Innych 175c, pelne tylko stal valyrianska / smocze szklo / ogien smoka"
                          : "t6 = pelne obrazenia (zasada stali Innych 175c wylaczona)";
        }

        private static string RuleText()
        {
            return RuleOn ? "zasada stali wl. (t6 " + CastlePct + "%)" : "zasada stali WYL. (stara: t6 pelne, VS i szklo wedlug tieru)";
        }

        // ------------------------------------------------------------ klasyfikacja

        private static bool IsDragonUsage(Monster m)
        {
            try
            {
                var mu = m != null ? (m.MonsterUsage ?? "") : "";
                return mu.IndexOf("dragon", StringComparison.OrdinalIgnoreCase) >= 0;   // "dragon" i "dragonfly" (ROT-Dragon monsters.xml)
            }
            catch { return false; }
        }

        /// <summary>Ogien smoka w polu: agent-smok albo jezdziec smoka z ciosem bez broni (tak ROT tworzy cios ognia).</summary>
        internal static bool IsDragonFire(Agent att, BlowWeaponRecord rec)
        {
            if (att == null) return false;
            if (!att.IsHuman) return IsDragonUsage(att.Monster);
            if (rec.HasWeapon()) return false;
            var mount = att.MountAgent;
            return mount != null && IsDragonUsage(mount.Monster);
        }

        private static bool HasGlassWord(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            for (int i = 0; i < GlassWords.Length; i++)
                if (id.IndexOf(GlassWords[i], StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        /// <summary>0 zwykla, 1 stal valyrianska, 2 smocze szklo (id przedmiotu albo kawalka klingi z WeaponDesign).</summary>
        internal static int ItemClass(ItemObject it)
        {
            if (it == null) return 0;
            int c;
            lock (_gate) { if (_itemCls.TryGetValue(it, out c)) return c; }
            c = 0;
            try
            {
                var id = it.StringId ?? "";
                if (_vs.Contains(id)) c = 1;
                else if (HasGlassWord(id)) c = 2;
                else
                {
                    var d = it.WeaponDesign;
                    if (d != null && d.UsedPieces != null)
                        foreach (var el in d.UsedPieces)
                            if (el != null && el.CraftingPiece != null && el.CraftingPiece.PieceType == CraftingPiece.PieceTypes.Blade
                                && HasGlassWord(el.CraftingPiece.StringId)) { c = 2; break; }
                }
            }
            catch { }
            lock (_gate) _itemCls[it] = c;
            return c;
        }

        /// <summary>Przedmiot pocisku (strzala, belt, bron rzucana) z prywatnego slownika misji; null - brak.</summary>
        internal static ItemObject MissileItem(Mission mission, int index)
        {
            try
            {
                if (mission == null || index < 0) return null;
                if (!_fMissilesTried) { _fMissilesTried = true; _fMissiles = AccessTools.Field(typeof(Mission), "_missilesDictionary"); }
                var dict = _fMissiles != null ? _fMissiles.GetValue(mission) as Dictionary<int, Mission.Missile> : null;
                Mission.Missile m;
                if (dict != null && dict.TryGetValue(index, out m) && m != null && !m.Weapon.IsEmpty) return m.Weapon.Item;
            }
            catch { }
            return null;
        }

        /// <summary>Klasa ciosu w polu (trafiony to juz Wedrowiec). tool = narzedzie do tieru (melee - bron, pocisk - luk/kusza
        /// w rece), struck = przedmiot, ktory trafil (melee - bron, pocisk - przedmiot pocisku; brak - narzedzie).</summary>
        internal static int FieldClass(Agent att, BlowWeaponRecord rec, Mission mission)
        {
            if (att == null) return KBare;
            if (IsDragonFire(att, rec)) return KFire;
            if (!rec.HasWeapon()) return KBare;
            ItemObject tool = null, struck = null;
            if (!rec.IsMissile)
            {
                int slot = rec.AffectorWeaponSlotOrMissileIndex;
                if (slot >= 0 && slot < 5)
                {
                    var mw = att.Equipment[(EquipmentIndex)slot];
                    if (!mw.IsEmpty) tool = mw.Item;
                }
                struck = tool;
            }
            else
            {
                var mw = att.WieldedWeapon;
                if (!mw.IsEmpty) tool = mw.Item;
                if (RuleOn) struck = MissileItem(mission, rec.AffectorWeaponSlotOrMissileIndex) ?? tool;
            }
            if (RuleOn)
            {
                int c = ItemClass(struck);
                if (c == 1) return KVs;
                if (c == 2) return KGlass;
            }
            if (tool == null) return KBare;
            return (int)tool.Tier + 1 >= 6 ? KT6 : KRest;          // PULAPKA: ItemTiers.Tier1 == 0
        }

        private static int BestClassOf(IEnumerable<ItemObject> items)
        {
            int best = 0;
            foreach (var it in items)
            {
                int c = ItemClass(it);
                if (c == 1) return 1;
                if (c == 2) best = 2;
            }
            return best;
        }

        private static IEnumerable<ItemObject> WeaponsOf(IEnumerable<Equipment> eqs)
        {
            foreach (var eq in eqs)
            {
                if (eq == null) continue;
                for (int s = 0; s <= 3; s++)
                {
                    var it = eq[(EquipmentIndex)s].Item;
                    if (it != null) yield return it;
                }
            }
        }

        /// <summary>VS / szklo u bijacego w symulacji: bohater - jego zestaw bojowy; zolnierz - migawka 175 (albo wzorzec na zywo).</summary>
        private static int UnitClass(CharacterObject c)
        {
            if (c.IsHero) return BestClassOf(WeaponsOf(c.BattleEquipments));
            int r;
            lock (_gate) { if (_unitCls.TryGetValue(c, out r)) return r; }
            var pre = Army175.PreWeapons(c);
            r = pre != null ? BestClassOf(pre) : BestClassOf(WeaponsOf(c.BattleEquipments));
            lock (_gate) _unitCls[c] = r;
            return r;
        }

        /// <summary>Klasa trafienia w symulacji (trafiony to juz Wedrowiec).</summary>
        internal static int SimClass(CharacterObject striker)
        {
            if (striker == null) return KBare;
            try
            {
                var h = striker.Equipment[EquipmentIndex.Horse].Item;     // jak ROTCombatSimulationModel: slot 10
                if (h != null && h.HorseComponent != null && IsDragonUsage(h.HorseComponent.Monster)) return KFire;
            }
            catch { }
            if (RuleOn)
            {
                int u = UnitClass(striker);
                if (u == 1) return KVs;
                if (u == 2) return KGlass;
            }
            int tier = Army175.PreTierBest(striker);
            if (tier >= 6) return KT6;
            return tier <= 0 ? KBare : KRest;
        }

        internal static int Apply(int k, int pre)
        {
            if (k == KFire || k == KVs || k == KGlass) return pre;
            int pct = k == KT6 ? (RuleOn ? CastlePct : 100) : RestPct;
            if (pct >= 100) return pre;
            int cut = pre * pct / 100;
            return cut < 1 ? 1 : cut;
        }

        internal static float ApplyF(int k, float pre)
        {
            if (k == KFire || k == KVs || k == KGlass) return pre;
            int pct = k == KT6 ? (RuleOn ? CastlePct : 100) : RestPct;
            if (pct >= 100) return pre;
            float cut = pre * pct / 100f;
            return cut < 1f ? 1f : cut;
        }

        // ------------------------------------------------------------ liczniki

        /// <summary>Pierwszy cios w Wedrowca w misji: ustawienia, przypiecie kubelka pola do MapEvent gracza.</summary>
        internal static void MissionCheck(Mission mission)
        {
            try
            {
                object last = _lastMission != null ? _lastMission.Target : null;
                if (ReferenceEquals(last, mission)) return;
                _lastMission = new WeakReference(mission);
                Refresh(false);
                MapEvent pe = null;
                try { if (Campaign.Current != null) pe = MapEvent.PlayerMapEvent; } catch { }
                lock (_gate)
                {
                    object prev = _fieldEvent != null ? _fieldEvent.Target : null;
                    if (pe == null || !ReferenceEquals(prev, pe)) FlushFieldLoose();   // ta sama bitwa mapy (powrot do walki) - kubelek zostaje
                    _fieldEvent = pe != null ? new WeakReference(pe) : null;
                }
            }
            catch { }
        }

        private static void FlushFieldLoose()
        {
            if (_field.Hits == 0) { _fieldWalkers.Clear(); return; }
            _dayLoose += _field.Hits;
            _dayField.AddAll(_field);
            _field.Clear();
            _fieldWalkers.Clear();
        }

        internal static void CountField(int k, int pre, int post, Agent victim)
        {
            try
            {
                lock (_gate)
                {
                    _field.Add(k, pre, post);
                    var co = victim != null ? victim.Character as CharacterObject : null;
                    if (co != null && co.IsHero && co.HeroObject != null) _fieldWalkers.Add(co.HeroObject);
                }
            }
            catch { _stumbles++; }
        }

        internal static void CountSim(MapEvent battle, int k, float pre, float post, CharacterObject struck)
        {
            try
            {
                lock (_gate)
                {
                    if (battle == null) { _daySim.Add(k, pre, post); _dayLoose++; return; }
                    Battle b;
                    if (!_battles.TryGetValue(battle, out b)) { b = new Battle(); _battles[battle] = b; }
                    b.Sim.Add(k, pre, post);
                    if (struck != null && struck.IsHero && struck.HeroObject != null) b.Walkers.Add(struck.HeroObject);
                }
            }
            catch { _stumbles++; }
        }

        private static void CollectWalkers(TroopRoster r, HashSet<Hero> into, ref int troops)
        {
            if (r == null) return;
            foreach (var e in r.GetTroopRoster())
            {
                var c = e.Character;
                if (c == null || !Mends.WalkerBlood(c)) continue;
                if (c.IsHero) { if (c.HeroObject != null) into.Add(c.HeroObject); }
                else troops += e.Number;
            }
        }

        /// <summary>MapEventStarted: Wedrowcy obecni od poczatku (zdrowy Wedrowiec wziety do niewoli znika z rosterow do konca bitwy).</summary>
        internal static void OnMapEventStarted(MapEvent m)
        {
            try
            {
                if (m == null) return;
                HashSet<Hero> found = null;
                for (int si = 0; si < 2; si++)
                {
                    var side = si == 0 ? m.AttackerSide : m.DefenderSide;
                    if (side == null) continue;
                    foreach (var mp in side.Parties)
                    {
                        if (mp == null || mp.Party == null || mp.Party.MemberRoster == null) continue;
                        foreach (var e in mp.Party.MemberRoster.GetTroopRoster())
                        {
                            var c = e.Character;
                            if (c == null || !c.IsHero || c.HeroObject == null || !Mends.WalkerBlood(c)) continue;
                            if (found == null) found = new HashSet<Hero>(new RefEq<Hero>());
                            found.Add(c.HeroObject);
                        }
                    }
                }
                if (found == null) return;                         // zwykla bitwa - bez wpisu
                lock (_gate)
                {
                    Battle b;
                    if (!_battles.TryGetValue(m, out b)) { b = new Battle(); _battles[m] = b; }
                    b.Walkers.UnionWith(found);
                }
            }
            catch { _stumbles++; }
        }

        private static bool IsOthersParty(PartyBase p)
        {
            try
            {
                if (p == null) return false;
                if (p.MobileParty != null) return NightKingGate.IsOthersParty(p.MobileParty);
                var s = p.Settlement;
                return s != null && NightKingCall.IsOthers(s.OwnerClan);
            }
            catch { return false; }
        }

        private sealed class SideSum
        {
            internal bool Others;
            internal int Start, Dead, Wounded;
            internal string Name = "?";
        }

        private static SideSum Sum(MapEventSide side)
        {
            var s = new SideSum();
            if (side == null) return s;
            try { if (side.LeaderParty != null) s.Name = side.LeaderParty.Name != null ? side.LeaderParty.Name.ToString() : "?"; } catch { }
            foreach (var mp in side.Parties)
            {
                if (mp == null) continue;
                try
                {
                    if (IsOthersParty(mp.Party)) s.Others = true;
                    s.Start += mp.HealthyManCountAtStart;
                    s.Dead += mp.DiedInBattle != null ? mp.DiedInBattle.TotalManCount : 0;
                    s.Wounded += mp.WoundedInBattle != null ? mp.WoundedInBattle.TotalManCount : 0;
                }
                catch { }
            }
            return s;
        }

        private static string Fmt(Tally t)
        {
            string t6 = RuleOn ? CastlePct + "%" : "100%, zasada wyl.";
            double pre = 0, post = 0;
            for (int i = 0; i < KN; i++) { pre += t.Pre[i]; post += t.Post[i]; }
            return "VS " + t.N[KVs] + ", szklo " + t.N[KGlass] + ", ogien " + t.N[KFire] + ", stal t6 " + t.N[KT6] + " (" + t6 + "), reszta "
                   + t.N[KRest] + " (15%), gole rece " + t.N[KBare] + " (15%); obrazenia " + pre.ToString("0") + "->" + post.ToString("0");
        }

        private static int Day()
        {
            try { return (int)(CampaignTime.Now - Campaign.Current.Models.CampaignTimeModel.CampaignStartTime).ToDays; } catch { return -1; }
        }

        /// <summary>MapEventEnded: linia bitwy z Innymi (strona z partia Innych albo ciosy w Wedrowcow).</summary>
        internal static void OnMapEventEnded(MapEvent m)
        {
            try
            {
                if (m == null) return;
                Battle b = null;
                Tally field = null;
                var walkers = new HashSet<Hero>(new RefEq<Hero>());
                lock (_gate)
                {
                    if (_battles.TryGetValue(m, out b)) _battles.Remove(m);
                    object fe = _fieldEvent != null ? _fieldEvent.Target : null;
                    if (ReferenceEquals(fe, m))
                    {
                        if (_field.Hits > 0) { field = new Tally(); field.AddAll(_field); }
                        walkers.UnionWith(_fieldWalkers);
                        _field.Clear(); _fieldWalkers.Clear(); _fieldEvent = null;
                    }
                }
                if (b != null) walkers.UnionWith(b.Walkers);
                var att = Sum(m.AttackerSide);
                var def = Sum(m.DefenderSide);
                int troops = 0, troopsDead = 0, troopsWounded = 0;
                var died = new HashSet<Hero>(new RefEq<Hero>());
                var wounded = new HashSet<Hero>(new RefEq<Hero>());
                foreach (var side in new[] { m.AttackerSide, m.DefenderSide })
                {
                    if (side == null) continue;
                    foreach (var mp in side.Parties)
                    {
                        if (mp == null || mp.Party == null) continue;
                        CollectWalkers(mp.Party.MemberRoster, walkers, ref troops);
                        int td = 0, tw = 0;
                        CollectWalkers(mp.DiedInBattle, died, ref td);
                        CollectWalkers(mp.WoundedInBattle, wounded, ref tw);
                        troops += td; troopsDead += td; troopsWounded += tw;
                    }
                }
                walkers.UnionWith(died); walkers.UnionWith(wounded);
                bool simHits = b != null && b.Sim.Hits > 0;
                if (!att.Others && !def.Others && field == null && !simHits && walkers.Count == 0 && troops == 0) return;

                int k = walkers.Count + troops, d = troopsDead, c = 0, w = troopsWounded;
                foreach (var h in walkers)
                {
                    try
                    {
                        if (died.Contains(h) || h.IsDead) d++;
                        else if (h.IsPrisoner) c++;
                        else if (wounded.Contains(h) || h.IsWounded) w++;
                    }
                    catch { }
                }
                // strona Innych: ta z partia Innych; obie albo zadna - atak/obrona bez nazwy "Inni"
                SideSum oth = null, foe = null;
                string othRole = "";
                if (att.Others && !def.Others) { oth = att; foe = def; othRole = "atak"; }
                else if (def.Others && !att.Others) { oth = def; foe = att; othRole = "obrona"; }
                var win = m.WinningSide;
                string result;
                if (win == BattleSideEnum.None) result = "bez rozstrzygniecia";
                else if (oth == null) result = "wygrala strona " + (win == BattleSideEnum.Attacker ? "atakujaca" : "broniaca");
                else result = ((win == BattleSideEnum.Attacker) == (othRole == "atak")) ? "wygrali Inni" : "przegrali Inni";

                string kind = field != null ? (simHits ? "pole + symulacja" : "pole")
                                            : (m.IsPlayerMapEvent ? (simHits ? "z graczem (symulacja)" : "z graczem") : "autobitwa");
                int day = Day();
                string hits;
                if (field == null && !simHits) hits = "ciosy w Wedrowcow: brak";
                else if (field != null && simHits) hits = "ciosy w Wedrowcow - pole: " + Fmt(field) + "; symulacja: " + Fmt(b.Sim);
                else hits = "ciosy w Wedrowcow: " + Fmt(field ?? b.Sim);
                string sides = oth != null
                    ? "Inni (" + othRole + ") '" + oth.Name + "' " + oth.Start + " ludzi: polegli " + oth.Dead + ", ranni " + oth.Wounded
                      + "; przeciwnik '" + foe.Name + "' " + foe.Start + ": polegli " + foe.Dead + ", ranni " + foe.Wounded
                    : "atak '" + att.Name + "' " + att.Start + ": polegli " + att.Dead + ", ranni " + att.Wounded
                      + "; obrona '" + def.Name + "' " + def.Start + ": polegli " + def.Dead + ", ranni " + def.Wounded;
                Scribe.Line("Inni (175c): bitwa dzien " + day + " " + kind + " '" + att.Name + "' vs '" + def.Name + "' | " + hits
                            + " | Wedrowcy: " + k + " w bitwie, ranni " + w + ", polegli " + d + ", w niewoli " + c
                            + " | straty: " + sides + " | wynik: " + result + "; " + RuleText() + ".");
                lock (_gate)
                {
                    _dayBattles++;
                    if (m.IsPlayerMapEvent) _dayPlayer++;
                    if (result == "wygrali Inni") _dayOthersWins++;
                    else if (result == "przegrali Inni") _dayOthersLosses++;
                    _dayWalkers += k; _dayWounded += w; _dayDead += d; _dayCaptured += c;
                    if (oth != null) { _dayOthDead += oth.Dead; _dayOthWounded += oth.Wounded; _dayFoeDead += foe.Dead; _dayFoeWounded += foe.Wounded; }
                    _dayField.AddAll(field);
                    if (b != null) _daySim.AddAll(b.Sim);
                }
            }
            catch (Exception e) { _stumbles++; try { Scribe.Report("CrashScribe", e, "OthersSteel.OnMapEventEnded", null); } catch { } }
        }

        /// <summary>Raz na dobe: ustawienia, sprzatanie zakonczonych bitew, linia dobowa (gdy cos sie dzialo).</summary>
        internal static void Daily()
        {
            try
            {
                Refresh(true);
                lock (_gate)
                {
                    var gone = new List<MapEvent>();
                    foreach (var kv in _battles)
                    {
                        bool fin = true;
                        try { fin = kv.Key == null || kv.Key.IsFinalized; } catch { }
                        if (fin) gone.Add(kv.Key);
                    }
                    foreach (var g in gone) { Battle b; if (g != null && _battles.TryGetValue(g, out b)) { _dayLoose += b.Sim.Hits; _daySim.AddAll(b.Sim); } _battles.Remove(g); }
                    var fe = _fieldEvent != null ? _fieldEvent.Target as MapEvent : null;
                    bool feAlive = false;
                    try { feAlive = fe != null && !fe.IsFinalized; } catch { }
                    if (!feAlive) { FlushFieldLoose(); _fieldEvent = null; }   // misja bez bitwy mapy (pojedynek ROT) albo bitwa juz bez nas
                }
                int dayHits = _dayField.Hits + _daySim.Hits;
                if (_dayBattles > 0 || dayHits > 0 || _stumbles > 0)
                {
                    Scribe.Line("Inni (175c) dzien " + Day() + ": bitew z Innymi " + _dayBattles + " (z graczem " + _dayPlayer + "), Inni wygrali "
                                + _dayOthersWins + ", przegrali " + _dayOthersLosses
                                + " | ciosy w Wedrowcow - pole: " + Fmt(_dayField) + "; symulacja: " + Fmt(_daySim)
                                + (_dayLoose > 0 ? " (z tego poza bitwami mapy " + _dayLoose + ")" : "")
                                + " | Wedrowcy w bitwach " + _dayWalkers + ", ranni " + _dayWounded + ", polegli " + _dayDead + ", w niewoli " + _dayCaptured
                                + " | straty Innych: polegli " + _dayOthDead + ", ranni " + _dayOthWounded + "; ich przeciwnikow: polegli " + _dayFoeDead
                                + ", ranni " + _dayFoeWounded + "; " + RuleText() + (_stumbles > 0 ? "; potkniecia " + _stumbles : "") + ".");
                }
                lock (_gate)
                {
                    _dayField.Clear(); _daySim.Clear();
                    _dayBattles = _dayPlayer = _dayOthersWins = _dayOthersLosses = _dayWalkers = _dayWounded = _dayDead = _dayCaptured = 0;
                    _dayOthDead = _dayOthWounded = _dayFoeDead = _dayFoeWounded = _dayLoose = _stumbles = 0;
                }
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "OthersSteel.Daily", null); } catch { } }
        }

        // ------------------------------------------------------------ start sesji

        /// <summary>Lista stali valyrianskiej: z Armoury ValyrianBlades (177 scalone) albo kopia CS; opis zgodnosci.</summary>
        private static string VsListCheck()
        {
            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (asm.GetName().Name != "Armoury") continue;
                    var t = asm.GetType("Armoury.ValyrianBlades");
                    if (t == null) break;
                    var p = AccessTools.Property(t, "AllIds");
                    var src = p != null ? p.GetValue(null, null) as IEnumerable<string> : null;
                    if (src == null) return "Armoury ValyrianBlades bez AllIds - kopia CS (" + VsIds.Length + ")";
                    var other = new HashSet<string>(src, StringComparer.Ordinal);
                    if (other.Count == 0) return "Armoury ValyrianBlades pusta - kopia CS (" + VsIds.Length + ")";
                    var mine = new HashSet<string>(VsIds, StringComparer.Ordinal);
                    _vs = other;
                    if (other.SetEquals(mine)) return "lista Armoury ValyrianBlades (" + other.Count + "), zgodna z kopia CS";
                    var onlyCs = new List<string>(); foreach (var x in mine) if (!other.Contains(x)) onlyCs.Add(x);
                    var onlyArm = new List<string>(); foreach (var x in other) if (!mine.Contains(x)) onlyArm.Add(x);
                    return "OSTRZEZENIE: lista Armoury ValyrianBlades (" + other.Count + ") ROZNA od kopii CS (tylko CS: " + string.Join(", ", onlyCs.ToArray())
                           + "; tylko Armoury: " + string.Join(", ", onlyArm.ToArray()) + ") - uzyta lista Armoury, poprawic kopie w OthersSteel";
                }
            }
            catch { }
            _vs = new HashSet<string>(VsIds, StringComparer.Ordinal);
            return "Armoury bez ValyrianBlades (177 nie scalone) - kopia CS (" + VsIds.Length + ")";
        }

        /// <summary>OnSessionLaunched: czysty stan, ustawienia, linia startu (listy, kogo dotyczy).</summary>
        internal static void OnSession()
        {
            try
            {
                lock (_gate)
                {
                    _battles.Clear(); _itemCls.Clear(); _unitCls.Clear();
                    _field.Clear(); _fieldWalkers.Clear(); _fieldEvent = null; _lastMission = null;
                    _dayField.Clear(); _daySim.Clear();
                    _dayBattles = _dayPlayer = _dayOthersWins = _dayOthersLosses = _dayWalkers = _dayWounded = _dayDead = _dayCaptured = 0;
                    _dayOthDead = _dayOthWounded = _dayFoeDead = _dayFoeWounded = _dayLoose = _stumbles = 0;
                }
                Refresh(true);
                _vsSource = VsListCheck();
                int vsFound = 0;
                foreach (var id in _vs) { try { if (MBObjectManager.Instance.GetObject<ItemObject>(id) != null) vsFound++; } catch { } }
                var glass = new List<string>();
                foreach (var it in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
                {
                    try { if (it != null && it.WeaponComponent != null && ItemClass(it) == 2) glass.Add(it.StringId); } catch { }
                }
                var who = new List<string>();
                int walkers = 0;
                foreach (var h in Hero.AllAliveHeroes)
                {
                    try
                    {
                        if (h == null || h.CharacterObject == null || !Mends.WalkerBlood(h.CharacterObject)) continue;
                        walkers++;
                        if (who.Count < 12) who.Add(h.Name + (h.IsPrisoner ? " (w niewoli)" : ""));
                    }
                    catch { }
                }
                Scribe.Line("Mends: zasada stali Innych (175c) - " + (RuleOn
                                ? "WLACZONA: pelne obrazenia tylko stal valyrianska (" + _vs.Count + " wzorow, w grze " + vsFound + "; " + _vsSource
                                  + "), smocze szklo (" + glass.Count + " przedmiotow: " + (glass.Count > 0 ? string.Join(", ", glass.ToArray()) : "brak") + ")"
                                  + " i ogien smoka (smok i jezdziec smoka bez broni); stal t6 " + CastlePct + "%, reszta 15% (min 1)"
                                : "WYLACZONA (OthersSteelRule): stara zasada - bron t6 pelne, reszta 15%; ogien smoka pelne (poprawka 175c dziala w obu trybach)")
                            + "; dotyczy " + walkers + " zywych Wedrowcow (Nocny Krol, lordowie Innych, wskrzeszeni): "
                            + (who.Count > 0 ? string.Join(", ", who.ToArray()) : "brak") + (walkers > who.Count ? ", ..." : "")
                            + " - wighty bez zmian. Pole: Agent.RegisterBlow; autobitwa: SimulateHit (zolnierze wedlug migawki 175, bohaterowie wedlug zestawu).");
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "OthersSteel.OnSession", null); } catch { } }
        }
    }
}
