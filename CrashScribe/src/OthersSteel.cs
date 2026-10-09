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
    ///  3. TIER NARZEDZIA >= 6 (stal zamkowa) - OthersCastleSteelPercent (dom. 50) przy wlaczonej zasadzie, 100% przy wylaczonej.
    ///     Narzedzie: melee - przedmiot ze slotu ciosu; pocisk (wlaczona zasada, recenzja 175c) - bron rzucana (oszczep, topor, noz)
    ///     wedlug WLASNEGO przedmiotu pocisku, strzala/belt/kamien z procy wedlug wyrzutni tej amunicji (luk/kusza/proca - decyzja
    ///     02.09; najpierw ta w rece, gdy pasuje do amunicji, potem najlepsza pasujaca w slotach 0-3 - zmiana broni w locie nie
    ///     zmienia tieru), kamien/glaz/garnek (klasy Stone, Boulder, Ballista*) i beret balisty bez luku u obslugi - reszta.
    ///     Wylaczona zasada: pocisk wedlug broni w rece, jak dotad.
    ///  4. RESZTA (bron ponizej t6) i GOLE RECE (piesc, kopyto, brak przedmiotu) - 15%, min 1. Ciosy do 1 punktu i upadek
    ///     (IsFallDamage) Mends.ValyrianWard przepuszcza bez zmian, jak dotad (stad tez ladowanie smoka ROT, 1 punkt, nie wchodzi
    ///     w liczniki). Cios wlasny (odbicie wlasnego ciosu) - obrazenia jak dotad, osobny licznik dobowy.
    /// SYMULACJA (autobitwa), wlaczona zasada: bijacy-bohater - jego zestaw bojowy (sloty 0-3); zolnierz - bron z migawki 175 sprzed
    /// zamiany (Army175.PreWeapons); tier = najlepsza bron BEZ AMUNICJI i tarcz (luk/kusza/proca wedlug wlasnego tieru - jak pole;
    /// recenzja 175c: dotad liczyla tez amunicje, wiec np. kusznicy Nocnej Strazy t3-t5 byli "t6" przez bolt_a w autobitwie, a w polu
    /// nie). Army175.PreTierBest bez zmian (definicja 175, linie migawki) - liczy go tylko wylaczona zasada (stara regula).
    /// Rozjazd, ktory zostaje (do decyzji Jeffa): autobitwa bierze bron sprzed zamiany 175.2, pole - po; licznik "tylko we wzorcu
    /// sprzed 175" pokazuje jego skale.
    ///
    /// LISTY: stal valyrianska - 29 wzorow z rejestru 177 (kopia ponizej, "AKTUALIZUJ OBIE RAZEM"); gdy Armoury ma juz
    /// ValyrianBlades (177 scalone) - lista Armoury, w logu startu zgodnosc z kopia (wzor Army175.EssosCheck). Smocze szklo - id
    /// przedmiotu albo kawalka klingi z WeaponDesign (kopie kute przez gracza) zawiera "dragonglass" albo "obsidian" (te same slowa
    /// co Armoury WorkshopForbiddenIds); w ROT jest jeden taki przedmiot (dragonglass_axe, freefolk, t5) i nikt go nie nosi
    /// (linia startu liczy noszacych).
    ///
    /// USTAWIENIA (Armoury, Mends.ArmouryFloat): OthersSteelRule (dom. TAK), OthersCastleSteelPercent (15-100, dom. 50). Czytane przy
    /// starcie sesji, raz na dobe i przy pierwszym ciosie w Wedrowca w nowej misji - nigdy per cios (ArmouryFloat przeglada assembly).
    /// Bez Armoury: wlaczone, 50.
    ///
    /// POMIAR (tylko log): linia "Inni (175c): bitwa ..." po kazdej bitwie z Innymi (strona z partia Innych - jak NightKingGate - albo
    /// z ciosami w Wedrowcow) i linia dobowa "Inni (175c) dzien N". Liczniki per MapEvent: pole - kubelek bitwy mapy gracza z chwili
    /// pierwszego ciosu w misji (wycofanie i inna bitwa nie mieszaja kubelkow), symulacja - z parametru SimulateHit (battle), obrazenia
    /// symulacji po (int) jak w grze (MapEvent.SimulateSingleTroopHit). Do tego: czas bitwy, rundy z samym Wedrowcem, spadek HP
    /// Wedrowcow od startu (porownanie z suma "po" - cudza latka po nas, np. AIInfluence SimulateHitPatch, wyszlaby jako roznica),
    /// HP oszczedzone przez 50% stali t6. Bez zapisu w grze (bez SyncData).
    /// Per cios: catch bez wylacznika globalnego (CLAUDE.md 7); potkniecia liczone w linii dobowej.
    /// </summary>
    internal static class OthersSteel
    {
        internal const int KVs = 0, KGlass = 1, KFire = 2, KT6 = 3, KRest = 4, KBare = 5, KN = 6;
        // uwagi do klasy - tylko liczniki, obrazen nie zmieniaja
        internal const int NLauncher = 1, NOldOnly = 2, NAmmoOnly = 4, NSiege = 8;
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
        private static readonly Dictionary<CharacterObject, UnitInfo> _units = new Dictionary<CharacterObject, UnitInfo>(new RefEq<CharacterObject>()); // zolnierze (migawka)
        private static FieldInfo _fMissiles;
        private static bool _fMissilesTried;

        private sealed class RefEq<T> : IEqualityComparer<T> where T : class
        {
            public bool Equals(T a, T b) { return ReferenceEquals(a, b); }
            public int GetHashCode(T o) { return RuntimeHelpers.GetHashCode(o); }
        }

        /// <summary>Bijacy w symulacji: klasa VS/szkla, tier stali bez amunicji, stary tier (PreTierBest), skad t6.</summary>
        private sealed class UnitInfo
        {
            internal int Cls, Tier, OldTier;
            internal bool Launcher, OldOnly;
        }

        private sealed class Tally
        {
            internal readonly int[] N = new int[KN];
            internal readonly double[] Pre = new double[KN], Post = new double[KN];
            internal int Launcher, OldOnly, AmmoOnly, Siege;
            internal int Hits { get { int s = 0; for (int i = 0; i < KN; i++) s += N[i]; return s; } }
            internal void Add(int k, double pre, double post, int note)
            {
                N[k]++; Pre[k] += pre; Post[k] += post;
                if ((note & NLauncher) != 0) Launcher++;
                if ((note & NOldOnly) != 0) OldOnly++;
                if ((note & NAmmoOnly) != 0) AmmoOnly++;
                if ((note & NSiege) != 0) Siege++;
            }
            internal void AddAll(Tally o)
            {
                if (o == null) return;
                for (int i = 0; i < KN; i++) { N[i] += o.N[i]; Pre[i] += o.Pre[i]; Post[i] += o.Post[i]; }
                Launcher += o.Launcher; OldOnly += o.OldOnly; AmmoOnly += o.AmmoOnly; Siege += o.Siege;
            }
            internal void Clear()
            {
                for (int i = 0; i < KN; i++) { N[i] = 0; Pre[i] = 0; Post[i] = 0; }
                Launcher = OldOnly = AmmoOnly = Siege = 0;
            }
            internal double PostSum { get { double s = 0; for (int i = 0; i < KN; i++) s += Post[i]; return s; } }
        }

        private sealed class Battle
        {
            internal readonly Tally Sim = new Tally(), Field = new Tally();
            internal readonly HashSet<Hero> Walkers = new HashSet<Hero>(new RefEq<Hero>());
            internal readonly Dictionary<Hero, int> StartHp = new Dictionary<Hero, int>(new RefEq<Hero>());   // Wedrowcy obecni od startu
            internal int AloneHits, AloneRounds;
            internal double LastAloneHour = -1;
        }

        private static readonly Dictionary<MapEvent, Battle> _battles = new Dictionary<MapEvent, Battle>(new RefEq<MapEvent>());
        // pole: kubelek bitwy mapy gracza z chwili pierwszego ciosu w Wedrowca w biezacej misji (null = misja bez bitwy mapy)
        private static Battle _curField;
        private static WeakReference _lastMission;
        // doba
        private static readonly Tally _dayField = new Tally(), _daySim = new Tally();
        private static int _dayBattles, _dayPlayer, _dayOthersWins, _dayOthersLosses, _dayWalkers, _dayWounded, _dayDead, _dayCaptured,
                           _dayOthDead, _dayOthWounded, _dayFoeDead, _dayFoeWounded, _dayLoose, _stumbles, _daySelf, _dayOver24, _dayHpMaxN;
        private static double _dayMaxHours, _dayHpMaxSum;

        // ------------------------------------------------------------ ustawienia

        /// <summary>Odczyt suwakow z Armoury (raz: start sesji, doba, nowa misja). full = tez czysci pamiec jednostek
        /// (migawka 175 moze dojsc w dobie DailyCatchUp).</summary>
        internal static void Refresh(bool full)
        {
            try
            {
                RuleOn = Mends.ArmouryFloat("OthersSteelRule", 1f) >= 0.5f;
                int p = (int)Math.Round(Mends.ArmouryFloat("OthersCastleSteelPercent", 50f));
                CastlePct = p < 15 ? 15 : (p > 100 ? 100 : p);
                if (full) lock (_gate) _units.Clear();
            }
            catch { }
        }

        /// <summary>Opis dzialania tieru 6 do linii 175 (migawka valyrianska, kontrola) - wolane raz przy wczytaniu, wiec suwaki
        /// czytane na swiezo (linia kontroli idzie przed OnSession). Przy wlaczonej zasadzie liczy jednostki t6 wedlug definicji
        /// autobitwy 175c (migawka bez amunicji) - bez pamieci jednostek (przed zamiana 175.2 wzorzec na zywo jest jeszcze stary).</summary>
        internal static string T6Text()
        {
            Refresh(false);
            if (!RuleOn) return "t6 = pelne obrazenia (zasada stali Innych 175c wylaczona; autobitwa liczy t6 razem z amunicja, jak dotad)";
            int n = 0, nl = 0;
            try
            {
                foreach (var co in MBObjectManager.Instance.GetObjectTypeList<CharacterObject>())
                {
                    try
                    {
                        if (co == null || co.IsHero) continue;
                        var pre = Army175.PreWeapons(co);
                        if (pre == null) continue;
                        bool lo;
                        if (SteelTier(pre, out lo) >= 6) { n++; if (lo) nl++; }
                    }
                    catch { }
                }
            }
            catch { }
            return "t6 = " + CastlePct + "% przy zasadzie stali Innych 175c, pelne tylko stal valyrianska / smocze szklo / ogien smoka; autobitwa"
                   + " przy zasadzie liczy t6 BEZ amunicji (luk, kusza, proca wedlug wlasnego tieru): " + n + " jednostek, z tego tylko przez"
                   + " luk/kusze/proce " + nl;
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

        /// <summary>Amunicja (strzaly, belty, kamienie do procy, kule) - nie stal; tier strzalu daje wyrzutnia.</summary>
        private static bool IsAmmo(ItemObject it)
        {
            var t = it.ItemType;
            return t == ItemObject.ItemTypeEnum.Arrows || t == ItemObject.ItemTypeEnum.Bolts
                || t == ItemObject.ItemTypeEnum.SlingStones || t == ItemObject.ItemTypeEnum.Bullets;
        }

        /// <summary>Wyrzutnia (luk, kusza, proca, bron palna).</summary>
        private static bool IsLauncher(ItemObject it)
        {
            var t = it.ItemType;
            return t == ItemObject.ItemTypeEnum.Bow || t == ItemObject.ItemTypeEnum.Crossbow || t == ItemObject.ItemTypeEnum.Sling
                || t == ItemObject.ItemTypeEnum.Pistol || t == ItemObject.ItemTypeEnum.Musket;
        }

        private static bool IsShield(ItemObject it)
        {
            if (it.ItemType == ItemObject.ItemTypeEnum.Shield) return true;
            var pw = it.WeaponComponent != null ? it.WeaponComponent.PrimaryWeapon : null;
            return pw != null && pw.IsShield;
        }

        /// <summary>Kamien, glaz, garnek (reczny albo z machiny) i kamien balisty - nie stal, zawsze reszta.</summary>
        private static bool IsRock(ItemObject it)
        {
            var wc = it.WeaponComponent;
            if (wc == null || wc.Weapons == null) return false;
            foreach (var w in wc.Weapons)
            {
                if (w == null) continue;
                var c = w.WeaponClass;
                if (c == WeaponClass.Stone || c == WeaponClass.Boulder || c == WeaponClass.BallistaStone || c == WeaponClass.BallistaBoulder) return true;
            }
            return false;
        }

        /// <summary>Czy przedmiot strzela amunicja tej klasy (AmmoClass ktoregos uzycia).</summary>
        private static bool Shoots(ItemObject it, WeaponClass ammo)
        {
            var wc = it != null ? it.WeaponComponent : null;
            if (wc == null || wc.Weapons == null) return false;
            foreach (var w in wc.Weapons)
                if (w != null && w.AmmoClass == ammo) return true;
            return false;
        }

        /// <summary>Wyrzutnia amunicji: ta w rece, gdy pasuje; inaczej najlepsza pasujaca w slotach 0-3 (zmiana broni w locie);
        /// null - zadnej (beret balisty od obslugi bez luku).</summary>
        private static ItemObject LauncherFor(Agent att, ItemObject ammo, ItemObject held)
        {
            var apw = ammo.PrimaryWeapon;
            if (apw == null) return null;
            var ac = apw.WeaponClass;
            if (held != null && Shoots(held, ac)) return held;
            ItemObject best = null;
            for (int s = 0; s < 4; s++)
            {
                var mw = att.Equipment[(EquipmentIndex)s];
                if (mw.IsEmpty) continue;
                var it = mw.Item;
                if (it != null && Shoots(it, ac) && (best == null || it.Tier > best.Tier)) best = it;
            }
            return best;
        }

        /// <summary>Tier stali: najwyzsza bron bez tarcz i amunicji (wyrzutnia wedlug wlasnego tieru). launcherOnly = t6 tylko z wyrzutni.</summary>
        private static int SteelTier(IEnumerable<ItemObject> items, out bool launcherOnly)
        {
            int melee = 0, launch = 0;
            foreach (var it in items)
            {
                if (it == null || it.WeaponComponent == null || IsShield(it) || IsAmmo(it)) continue;
                int t = (int)it.Tier + 1;                              // PULAPKA: ItemTiers.Tier1 == 0
                if (IsLauncher(it)) { if (t > launch) launch = t; }
                else if (t > melee) melee = t;
            }
            launcherOnly = launch >= 6 && melee < 6;
            return melee > launch ? melee : launch;
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

        /// <summary>Klasa ciosu w polu (trafiony to juz Wedrowiec, cios nie jest jego wlasny). note - uwagi do licznikow.</summary>
        internal static int FieldClass(Agent att, BlowWeaponRecord rec, Mission mission, out int note)
        {
            note = 0;
            if (att == null) return KBare;
            if (IsDragonFire(att, rec)) return KFire;
            if (!rec.HasWeapon()) return KBare;
            ItemObject tool = null;
            if (!rec.IsMissile)
            {
                int slot = rec.AffectorWeaponSlotOrMissileIndex;
                if (slot >= 0 && slot < 5)
                {
                    var mw = att.Equipment[(EquipmentIndex)slot];
                    if (!mw.IsEmpty) tool = mw.Item;
                }
                if (RuleOn)
                {
                    int c = ItemClass(tool);
                    if (c == 1) return KVs;
                    if (c == 2) return KGlass;
                }
            }
            else
            {
                var hw = att.WieldedWeapon;
                ItemObject held = hw.IsEmpty ? null : hw.Item;
                if (!RuleOn) tool = held;                              // stara zasada (02.09): tier tego, co w rece
                else
                {
                    var mi = MissileItem(mission, rec.AffectorWeaponSlotOrMissileIndex);
                    if (mi == null) tool = held;                       // brak slownika pociskow: jak dotad, bez VS/szkla (nie zgadujemy po rece)
                    else
                    {
                        int c = ItemClass(mi);
                        if (c == 1) return KVs;
                        if (c == 2) return KGlass;
                        if (IsAmmo(mi))
                        {
                            tool = LauncherFor(att, mi, held);
                            if (tool == null) { note |= NSiege; return KRest; }      // beret balisty, obsluga bez luku
                        }
                        else if (IsRock(mi)) { note |= NSiege; return KRest; }      // kamien, glaz, garnek
                        else tool = mi;                                // bron rzucana: tier z niej samej, nie z reki po rzucie
                    }
                }
            }
            if (tool == null) return KBare;
            if ((int)tool.Tier + 1 >= 6)                               // PULAPKA: ItemTiers.Tier1 == 0
            {
                if (IsLauncher(tool)) note |= NLauncher;
                return KT6;
            }
            return KRest;
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

        /// <summary>Bijacy w symulacji: bohater - jego zestaw bojowy (liczony na zywo); zolnierz - migawka 175 (albo wzorzec na zywo),
        /// pamietany do konca doby. OldOnly - t6 jest tylko w migawce, we wzorcu po zamianie 175.2 juz nie.</summary>
        private static UnitInfo Info(CharacterObject c)
        {
            UnitInfo u;
            if (!c.IsHero) lock (_gate) { if (_units.TryGetValue(c, out u)) return u; }
            u = new UnitInfo();
            var pre = c.IsHero ? null : Army175.PreWeapons(c);
            var src = pre != null ? (IEnumerable<ItemObject>)pre : new List<ItemObject>(WeaponsOf(c.BattleEquipments));
            u.Cls = BestClassOf(src);
            bool lo;
            u.Tier = SteelTier(src, out lo);
            u.Launcher = lo;
            u.OldTier = Army175.PreTierBest(c);
            if (pre != null && u.Tier >= 6) { bool l2; u.OldOnly = SteelTier(WeaponsOf(c.BattleEquipments), out l2) < 6; }
            if (!c.IsHero) lock (_gate) _units[c] = u;
            return u;
        }

        /// <summary>Klasa trafienia w symulacji (trafiony to juz Wedrowiec). note - uwagi do licznikow.</summary>
        internal static int SimClass(CharacterObject striker, out int note)
        {
            note = 0;
            if (striker == null) return KBare;
            try
            {
                var h = striker.Equipment[EquipmentIndex.Horse].Item;     // jak ROTCombatSimulationModel: slot 10
                if (h != null && h.HorseComponent != null && IsDragonUsage(h.HorseComponent.Monster)) return KFire;
            }
            catch { }
            if (!RuleOn)
            {
                int t = Army175.PreTierBest(striker);                    // stara zasada: definicja 175 (z amunicja), jak dotad
                if (t >= 6) return KT6;
                return t <= 0 ? KBare : KRest;
            }
            var u = Info(striker);
            if (u.Cls == 1) return KVs;
            if (u.Cls == 2) return KGlass;
            if (u.Tier >= 6)
            {
                if (u.Launcher) note |= NLauncher;
                if (u.OldOnly) note |= NOldOnly;
                return KT6;
            }
            if (u.OldTier >= 6) note |= NAmmoOnly;                      // stara definicja dalaby t6 tylko przez amunicje
            return u.Tier <= 0 ? KBare : KRest;
        }

        internal static int Apply(int k, int pre)
        {
            if (k == KFire || k == KVs || k == KGlass) return pre;   // ciosy 0-1 nie dochodza tu (straz w ValyrianWard)
            int pct = k == KT6 ? (RuleOn ? CastlePct : 100) : RestPct;
            if (pct >= 100) return pre;
            int cut = pre * pct / 100;
            return cut < 1 ? 1 : cut;
        }

        internal static float ApplyF(int k, float pre)
        {
            if (k == KFire || k == KVs || k == KGlass) return pre;   // ciosy do 1 nie dochodza tu (straz w ValyrianWardSim)
            int pct = k == KT6 ? (RuleOn ? CastlePct : 100) : RestPct;
            if (pct >= 100) return pre;
            float cut = pre * pct / 100f;
            return cut < 1f ? 1f : cut;
        }

        // ------------------------------------------------------------ liczniki

        private static Battle BattleOf(MapEvent m)
        {
            Battle b;
            if (!_battles.TryGetValue(m, out b)) { b = new Battle(); _battles[m] = b; }
            return b;
        }

        /// <summary>Pierwszy cios w Wedrowca w misji: ustawienia, kubelek pola = bitwa mapy gracza z tej chwili (osobny na kazda bitwe).</summary>
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
                lock (_gate) _curField = pe != null ? BattleOf(pe) : null;
            }
            catch { }
        }

        internal static void CountField(int k, int pre, int post, Agent victim, int note)
        {
            try
            {
                lock (_gate)
                {
                    var b = _curField;
                    if (b == null) { _dayField.Add(k, pre, post, note); _dayLoose++; return; }   // misja bez bitwy mapy (pojedynek ROT)
                    b.Field.Add(k, pre, post, note);
                    var co = victim != null ? victim.Character as CharacterObject : null;
                    if (co != null && co.IsHero && co.HeroObject != null) b.Walkers.Add(co.HeroObject);
                }
            }
            catch { _stumbles++; }
        }

        /// <summary>Cios wlasny Wedrowca (wlasciciel = ofiara: odbicie wlasnego ciosu, Agent.CreateBlowFromBlowAsReflection) - obrazenia
        /// jak dotad (wedlug jego broni), ale poza licznikami bitwy. Upadek (IsFallDamage) i ladowanie smoka ROT (1 punkt) nie dochodza tu.</summary>
        internal static void CountSelf()
        {
            lock (_gate) _daySelf++;
        }

        /// <summary>Cios symulacji w Wedrowca. Obrazenia po (int), jak stosuje gra. Sam Wedrowiec: po stronie trafionego zostalo nie wiecej
        /// ludzi niz Wedrowcow tej bitwy (przyblizenie - Wedrowcy sa zwykle po jednej stronie, jeden na bande).</summary>
        internal static void CountSim(MapEvent battle, int k, float pre, float post, CharacterObject struck, PartyBase struckParty, int note)
        {
            try
            {
                int ipre = (int)pre, ipost = (int)post;                 // MapEvent.SimulateSingleTroopHit: (int)ResultNumber
                lock (_gate)
                {
                    if (battle == null) { _daySim.Add(k, ipre, ipost, note); _dayLoose++; return; }
                    var b = BattleOf(battle);
                    b.Sim.Add(k, ipre, ipost, note);
                    if (struck != null && struck.IsHero && struck.HeroObject != null) b.Walkers.Add(struck.HeroObject);
                    var side = struckParty != null ? struckParty.MapEventSide : null;
                    if (side != null && side.NumRemainingSimulationTroops <= b.Walkers.Count)
                    {
                        b.AloneHits++;
                        double hr = CampaignTime.Now.ToHours;
                        if (hr != b.LastAloneHour) { b.AloneRounds++; b.LastAloneHour = hr; }
                    }
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

        /// <summary>MapEventStarted: Wedrowcy obecni od poczatku i ich HP (zdrowy Wedrowiec wziety do niewoli znika z rosterow do konca bitwy).</summary>
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
                    var b = BattleOf(m);
                    b.Walkers.UnionWith(found);
                    foreach (var h in found) if (!b.StartHp.ContainsKey(h)) b.StartHp[h] = h.HitPoints;
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
            return "VS " + t.N[KVs] + ", szklo " + t.N[KGlass] + ", ogien " + t.N[KFire] + ", stal t6 " + t.N[KT6] + " (" + t6
                   + (t.Launcher > 0 ? "; z luku/kuszy/procy " + t.Launcher : "")
                   + (t.OldOnly > 0 ? "; t6 tylko we wzorcu sprzed 175 " + t.OldOnly : "")
                   + "), reszta " + t.N[KRest] + " (15%"
                   + (t.AmmoOnly > 0 ? "; dawniej t6 tylko przez amunicje " + t.AmmoOnly : "")
                   + (t.Siege > 0 ? "; kamienie i pociski machin " + t.Siege : "")
                   + "), gole rece " + t.N[KBare] + " (15%); obrazenia " + pre.ToString("0") + "->" + post.ToString("0");
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
                lock (_gate)
                {
                    if (_battles.TryGetValue(m, out b)) _battles.Remove(m);
                    if (b != null && ReferenceEquals(_curField, b)) _curField = null;   // dalsze ciosy tej misji - poza bitwami mapy
                }
                Tally field = b != null && b.Field.Hits > 0 ? b.Field : null;
                var walkers = new HashSet<Hero>(new RefEq<Hero>());
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
                double hpMaxSum = 0; int hpMaxN = 0;
                foreach (var h in walkers)
                {
                    try
                    {
                        if (died.Contains(h) || h.IsDead) d++;
                        else if (h.IsPrisoner) c++;
                        else if (wounded.Contains(h) || h.IsWounded) w++;
                        hpMaxSum += h.MaxHitPoints; hpMaxN++;
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
                // czas bitwy (MapEvent.BattleStartTime) i rundy z samym Wedrowcem (recenzja 175c: 15% po (int) = 1 punkt na cios)
                double hours = -1;
                try { hours = (CampaignTime.Now - m.BattleStartTime).ToHours; } catch { }
                string time = "czas " + (hours >= 0 ? hours.ToString("0.0") + " h" : "?")
                              + (b != null && b.AloneRounds > 0 ? ", rundy z samym Wedrowcem " + b.AloneRounds + " (ciosow " + b.AloneHits + ")" : "");
                // spadek HP Wedrowcow obecnych od startu wobec sumy "po" (cudza latka po nas = wyrazna roznica)
                string hp = "";
                if (b != null && b.StartHp.Count > 0)
                {
                    int drop = 0;
                    foreach (var kv in b.StartHp)
                    {
                        try { int now = kv.Key.IsDead ? 0 : kv.Key.HitPoints; if (kv.Value > now) drop += kv.Value - now; } catch { }
                    }
                    hp = " | HP Wedrowcow obecnych od startu: -" + drop + " (suma 'po': symulacja " + b.Sim.PostSum.ToString("0")
                         + (field != null ? ", pole " + field.PostSum.ToString("0") : "") + ")";
                }
                Scribe.Line("Inni (175c): bitwa dzien " + day + " " + kind + " '" + att.Name + "' vs '" + def.Name + "' | " + hits
                            + " | Wedrowcy: " + k + " w bitwie, ranni " + w + ", polegli " + d + ", w niewoli " + c + hp
                            + " | " + time + " | straty: " + sides + " | wynik: " + result + "; " + RuleText() + ".");
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
                    if (hours > _dayMaxHours) _dayMaxHours = hours;
                    if (hours > 24) _dayOver24++;
                    _dayHpMaxSum += hpMaxSum; _dayHpMaxN += hpMaxN;
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
                    foreach (var g in gone)
                    {
                        Battle b;
                        if (g != null && _battles.TryGetValue(g, out b))
                        {
                            _dayLoose += b.Sim.Hits + b.Field.Hits; _daySim.AddAll(b.Sim); _dayField.AddAll(b.Field);
                            if (ReferenceEquals(_curField, b)) _curField = null;
                        }
                        _battles.Remove(g);
                    }
                }
                int dayHits = _dayField.Hits + _daySim.Hits;
                if (_dayBattles > 0 || dayHits > 0 || _stumbles > 0 || _daySelf > 0)
                {
                    // HP, ktorego Wedrowcy nie dostali przez ciecie stali t6 (przy wylaczonej zasadzie t6 bije w pelni)
                    double saved = (_dayField.Pre[KT6] - _dayField.Post[KT6]) + (_daySim.Pre[KT6] - _daySim.Post[KT6]);
                    double avgHp = _dayHpMaxN > 0 ? _dayHpMaxSum / _dayHpMaxN : 0;
                    string savedTxt = RuleOn && CastlePct < 100
                        ? " | stal t6 " + CastlePct + "% oszczedzila Wedrowcom " + saved.ToString("0") + " HP"
                          + (avgHp > 0 ? " (ok. " + (saved / avgHp).ToString("0.0") + " zycia przy srednio " + avgHp.ToString("0") + " HP)" : "")
                        : "";
                    Scribe.Line("Inni (175c) dzien " + Day() + ": bitew z Innymi " + _dayBattles + " (z graczem " + _dayPlayer + "), Inni wygrali "
                                + _dayOthersWins + ", przegrali " + _dayOthersLosses
                                + (_dayBattles > 0 ? ", najdluzsza " + _dayMaxHours.ToString("0.0") + " h, ponad 24 h: " + _dayOver24 : "")
                                + " | ciosy w Wedrowcow - pole: " + Fmt(_dayField) + "; symulacja: " + Fmt(_daySim)
                                + (_dayLoose > 0 ? " (z tego poza bitwami mapy " + _dayLoose + ")" : "")
                                + (_daySelf > 0 ? "; ciosy wlasne Wedrowcow (odbicie wlasnego ciosu, poza licznikami) " + _daySelf : "")
                                + savedTxt
                                + " | Wedrowcy w bitwach " + _dayWalkers + ", ranni " + _dayWounded + ", polegli " + _dayDead + ", w niewoli " + _dayCaptured
                                + " | straty Innych: polegli " + _dayOthDead + ", ranni " + _dayOthWounded + "; ich przeciwnikow: polegli " + _dayFoeDead
                                + ", ranni " + _dayFoeWounded + "; " + RuleText() + (_stumbles > 0 ? "; potkniecia " + _stumbles : "") + ".");
                }
                lock (_gate) ClearDay();
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "OthersSteel.Daily", null); } catch { } }
        }

        private static void ClearDay()
        {
            _dayField.Clear(); _daySim.Clear();
            _dayBattles = _dayPlayer = _dayOthersWins = _dayOthersLosses = _dayWalkers = _dayWounded = _dayDead = _dayCaptured = 0;
            _dayOthDead = _dayOthWounded = _dayFoeDead = _dayFoeWounded = _dayLoose = _stumbles = _daySelf = _dayOver24 = _dayHpMaxN = 0;
            _dayMaxHours = 0; _dayHpMaxSum = 0;
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

        /// <summary>OnSessionLaunched: czysty stan, ustawienia, linia startu (listy, kto nosi szklo, kogo dotyczy - walczacy osobno).</summary>
        internal static void OnSession()
        {
            try
            {
                lock (_gate)
                {
                    _battles.Clear(); _itemCls.Clear(); _units.Clear();
                    _curField = null; _lastMission = null;
                    ClearDay();
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
                // kto nosi smocze szklo: rodzaje jednostek (wzorzec) i zywi bohaterowie (zestaw bojowy)
                int glassUnits = 0, glassHeroes = 0;
                if (glass.Count > 0)
                {
                    foreach (var co in MBObjectManager.Instance.GetObjectTypeList<CharacterObject>())
                    {
                        try { if (co != null && !co.IsHero && BestClassOf(WeaponsOf(co.BattleEquipments)) == 2) glassUnits++; } catch { }
                    }
                }
                var who = new List<string>();
                int fighters = 0, inParty = 0, idle = 0;
                foreach (var h in Hero.AllAliveHeroes)
                {
                    try
                    {
                        if (h == null) continue;
                        if (glass.Count > 0 && h.BattleEquipment != null && BestClassOf(WeaponsOf(new[] { h.BattleEquipment })) == 2) glassHeroes++;
                        if (h.CharacterObject == null || !Mends.WalkerBlood(h.CharacterObject)) continue;
                        if (h != Hero.MainHero && !h.IsLord) { idle++; continue; }      // notable, dzieci, inni - chronieni, ale nie walcza
                        fighters++;
                        if (h.PartyBelongedTo != null) inParty++;
                        if (who.Count < 12) who.Add(h.Name + (h.IsPrisoner ? " (w niewoli)" : (h.PartyBelongedTo == null ? " (bez partii)" : "")));
                    }
                    catch { }
                }
                string glassTxt = glass.Count + " przedmiotow: " + (glass.Count > 0 ? string.Join(", ", glass.ToArray()) : "brak")
                                  + "; nosi " + glassUnits + " rodzajow jednostek i " + glassHeroes + " bohaterow"
                                  + (glassUnits + glassHeroes == 0 ? " - w swiecie nikt nie ma smoczego szkla" : "");
                Scribe.Line("Mends: zasada stali Innych (175c) - " + (RuleOn
                                ? "WLACZONA: pelne obrazenia tylko stal valyrianska (" + _vs.Count + " wzorow, w grze " + vsFound + "; " + _vsSource
                                  + "), smocze szklo (" + glassTxt + ")"
                                  + " i ogien smoka (smok i jezdziec smoka bez broni); stal t6 " + CastlePct + "% (pocisk: wyrzutnia albo sama bron rzucana;"
                                  + " autobitwa: bron z migawki 175 bez amunicji), reszta 15% (min 1; 0 zostaje 0)"
                                : "WYLACZONA (OthersSteelRule): stara zasada - bron t6 pelne, reszta 15%; ogien smoka pelne (poprawka 175c dziala w obu trybach)")
                            + "; dotyczy " + fighters + " walczacych Wedrowcow (lordowie Innych, Nocny Krol, wskrzeszeni, gracz-Inny; w partiach teraz " + inParty + "): "
                            + (who.Count > 0 ? string.Join(", ", who.ToArray()) : "brak") + (fighters > who.Count ? ", ..." : "")
                            + "; chronionych, ale niewalczacych (notable, dzieci) " + idle
                            + " - wighty bez zmian. Pole: Agent.RegisterBlow; autobitwa: SimulateHit (zolnierze wedlug migawki 175, bohaterowie wedlug zestawu).");
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "OthersSteel.OnSession", null); } catch { } }
        }
    }
}
