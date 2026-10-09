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
    ///     Narzedzie: melee - przedmiot ze slotu ciosu; pocisk (wlaczona zasada) - 175d (decyzja Jeffa 09.10 ok. 08:40 pkt 2: "tier 6
    ///     strzaly rania normalnie, czy belty, ale nie kamienie"): strzala, belt (i kula) wedlug tieru WLASNEJ AMUNICJI (luk, kusza
    ///     sie nie licza), bron rzucana (oszczep, topor, noz) wedlug wlasnego przedmiotu jak dotad; kamien do procy, kamien rzucany,
    ///     glaz, garnek (typ SlingStones, klasy Stone, Boulder, Ballista*) i KAZDY pocisk machiny (Mission.Missile.MissionObjectToIgnore
    ///     != null - tak strzela RangedSiegeWeapon gry, RBM i NavalDLC; beret balisty ma w RBM t6) - zawsze reszta.
    ///     Wylaczona zasada: pocisk wedlug broni w rece, jak dotad.
    ///  4. RESZTA (bron ponizej t6) i GOLE RECE (piesc, kopyto, brak przedmiotu) - 15%, min 1. Ciosy do 1 punktu i upadek
    ///     (IsFallDamage) Mends.ValyrianWard przepuszcza bez zmian, jak dotad (stad tez ladowanie smoka ROT, 1 punkt, nie wchodzi
    ///     w liczniki). Cios wlasny (odbicie wlasnego ciosu) - obrazenia jak dotad, osobny licznik dobowy.
    /// SYMULACJA (autobitwa): 175d (decyzja Jeffa 09.10 ok. 08:40 pkt 1: "autobitwa liczy bron PO naprawie 175, jedna zasada z polem")
    /// - bijacy-bohater - jego zestaw bojowy (sloty 0-3), zolnierz - WZORZEC NA ZYWO (po zamianie 175.2, tak jak wychodzi w pole), w obu
    /// trybach zasady. Wlaczona zasada: tier = najlepsza bron wrecz albo rzucana (bez kamieni) i najlepsze strzaly/belty, do ktorych
    /// jednostka ma pasujaca wyrzutnie (pkt 2: "najlepsza amunicja jednostki"); wyrzutnie, tarcze i kamienie do procy sie nie licza.
    /// Wylaczona zasada: stara regula na tym samym wzorcu - Mends.BestWeaponTier (najwyzszy tier w slotach 0-3 z amunicja i wyrzutnia,
    /// jak przed 175). Migawka 175 sprzed zamiany (Army175.PreWeapons) - juz tylko do porownania w logu ("dawniej t6").
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
    /// KOMUNIKAT W GRZE (175d, decyzja pkt 4): pierwszy cios gracza albo jego ludzi (druzyna gracza) w Wedrowca wroga bronia, ktora
    /// tnie (t6 ponizej 100% albo reszta), raz na misje - po angielsku, plus linia w logu. Tylko przy wlaczonej zasadzie.
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
        // uwagi do klasy - tylko liczniki, obrazen nie zmieniaja (175d): NAmmo - t6 dzieki strzalom/beltom, NPreT6 - reszta, choc
        // migawka sprzed 175 dalaby t6 (skutek decyzji pkt 1), NStone - kamien, glaz, garnek albo pocisk machiny
        internal const int NAmmo = 1, NPreT6 = 2, NStone = 8;
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
        private static readonly Dictionary<CharacterObject, UnitInfo> _units = new Dictionary<CharacterObject, UnitInfo>(new RefEq<CharacterObject>()); // zolnierze (175d: wzorzec na zywo, doba)
        private static FieldInfo _fMissiles;
        private static bool _fMissilesTried;

        private sealed class RefEq<T> : IEqualityComparer<T> where T : class
        {
            public bool Equals(T a, T b) { return ReferenceEquals(a, b); }
            public int GetHashCode(T o) { return RuntimeHelpers.GetHashCode(o); }
        }

        /// <summary>Bijacy w symulacji (175d - wzorzec na zywo): klasa VS/szkla, tier stali (wlaczona zasada), tier starej regule
        /// (wylaczona), skad t6, tier tej samej definicji na migawce sprzed 175 (-1 brak migawki), czy ma jakakolwiek bron.</summary>
        private sealed class UnitInfo
        {
            internal int Cls, Tier, OldTier, PreTier = -1;
            internal bool AmmoOnly, Armed;
        }

        private sealed class Tally
        {
            internal readonly int[] N = new int[KN];
            internal readonly double[] Pre = new double[KN], Post = new double[KN];
            internal int Ammo, PreT6, Stone;
            internal int Hits { get { int s = 0; for (int i = 0; i < KN; i++) s += N[i]; return s; } }
            internal void Add(int k, double pre, double post, int note)
            {
                N[k]++; Pre[k] += pre; Post[k] += post;
                if ((note & NAmmo) != 0) Ammo++;
                if ((note & NPreT6) != 0) PreT6++;
                if ((note & NStone) != 0) Stone++;
            }
            internal void AddAll(Tally o)
            {
                if (o == null) return;
                for (int i = 0; i < KN; i++) { N[i] += o.N[i]; Pre[i] += o.Pre[i]; Post[i] += o.Post[i]; }
                Ammo += o.Ammo; PreT6 += o.PreT6; Stone += o.Stone;
            }
            internal void Clear()
            {
                for (int i = 0; i < KN; i++) { N[i] = 0; Pre[i] = 0; Post[i] = 0; }
                Ammo = PreT6 = Stone = 0;
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
        private static bool _warned;          // 175d: komunikat dla gracza juz pokazany w tej misji
        // doba
        private static readonly Tally _dayField = new Tally(), _daySim = new Tally();
        private static int _dayBattles, _dayPlayer, _dayOthersWins, _dayOthersLosses, _dayWalkers, _dayWounded, _dayDead, _dayCaptured,
                           _dayOthDead, _dayOthWounded, _dayFoeDead, _dayFoeWounded, _dayLoose, _stumbles, _daySelf, _dayOver24, _dayHpMaxN;
        private static double _dayMaxHours, _dayHpMaxSum;

        // ------------------------------------------------------------ ustawienia

        /// <summary>Odczyt suwakow z Armoury (raz: start sesji, doba, nowa misja). full = tez czysci pamiec jednostek
        /// (zamiana 175.2 moze dojsc w dobie DailyCatchUp - wzorce sie zmieniaja).</summary>
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
        /// czytane na swiezo (linia kontroli idzie przed OnSession). Liczy jednostki zolnierskie z migawka wedlug definicji autobitwy
        /// 175d: wzorzec NA ZYWO (przed zamiana 175.2 jeszcze stary - linia migawki; po zamianie - linia kontroli) i dla porownania
        /// ta sama definicja na migawce sprzed zamiany. Bez pamieci jednostek.</summary>
        internal static string T6Text()
        {
            Refresh(false);
            int n = 0, na = 0, np = 0, units = 0;
            try
            {
                foreach (var co in MBObjectManager.Instance.GetObjectTypeList<CharacterObject>())
                {
                    try
                    {
                        if (co == null || co.IsHero) continue;
                        var pre = Army175.PreWeapons(co);
                        if (pre == null) continue;
                        units++;
                        if (!RuleOn)
                        {
                            if (Mends.BestWeaponTier(co) >= 6) n++;
                            if (Army175.PreTierBest(co) >= 6) np++;
                            continue;
                        }
                        bool ao, arm;
                        if (SteelTier(new List<ItemObject>(WeaponsOf(co.BattleEquipments)), out ao, out arm) >= 6) { n++; if (ao) na++; }
                        if (SteelTier(pre, out ao, out arm) >= 6) np++;
                    }
                    catch { }
                }
            }
            catch { }
            if (!RuleOn)
                return "t6 = pelne obrazenia (zasada stali Innych 175c wylaczona - stara regula: autobitwa liczy t6 z wzorca po zamianie 175 razem z"
                       + " amunicja i wyrzutnia, 175d): " + n + " z " + units + " jednostek (na migawce sprzed zamiany " + np + ")";
            return "t6 = " + CastlePct + "% przy zasadzie stali Innych 175c, pelne tylko stal valyrianska / smocze szklo / ogien smoka; autobitwa"
                   + " (175d) liczy wzorzec po zamianie 175 - bron wrecz i rzucana wedlug siebie, strzaly i belty wedlug wlasnego tieru (z pasujaca"
                   + " wyrzutnia), luk/kusza/proca i kamienie sie nie licza: " + n + " z " + units + " jednostek t6, z tego tylko przez strzaly/belty "
                   + na + " (ta sama definicja na migawce sprzed zamiany: " + np + ")";
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

        /// <summary>175d: amunicja liczona wedlug WLASNEGO tieru (strzaly, belty, kule) - decyzja pkt 2.</summary>
        private static bool IsTieredAmmo(ItemObject it)
        {
            var t = it.ItemType;
            return t == ItemObject.ItemTypeEnum.Arrows || t == ItemObject.ItemTypeEnum.Bolts || t == ItemObject.ItemTypeEnum.Bullets;
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

        /// <summary>Kamien do procy, kamien rzucany, glaz, garnek (reczny albo z machiny) i kamien balisty - nie stal, zawsze reszta.</summary>
        private static bool IsRock(ItemObject it)
        {
            if (it.ItemType == ItemObject.ItemTypeEnum.SlingStones) return true;
            var wc = it.WeaponComponent;
            if (wc == null || wc.Weapons == null) return false;
            foreach (var w in wc.Weapons)
            {
                if (w == null) continue;
                var c = w.WeaponClass;
                if (c == WeaponClass.Stone || c == WeaponClass.Boulder || c == WeaponClass.BallistaStone || c == WeaponClass.BallistaBoulder
                    || c == WeaponClass.SlingStone) return true;
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

        /// <summary>175d, zapas na brak slownika pociskow: najlepsza amunicja w slotach 0-3, ktora strzela wyrzutnia z reki; null - brak.</summary>
        private static ItemObject AmmoFor(Agent att, ItemObject launcher)
        {
            ItemObject best = null;
            for (int s = 0; s < 4; s++)
            {
                var mw = att.Equipment[(EquipmentIndex)s];
                if (mw.IsEmpty) continue;
                var it = mw.Item;
                var pw = it != null ? it.PrimaryWeapon : null;
                if (pw != null && IsTieredAmmo(it) && Shoots(launcher, pw.WeaponClass) && (best == null || it.Tier > best.Tier)) best = it;
            }
            return best;
        }

        /// <summary>Tier stali 175d (autobitwa przy wlaczonej zasadzie): bron wrecz i rzucana wedlug wlasnego tieru (bez kamieni),
        /// strzaly/belty/kule wedlug wlasnego tieru, gdy w tej samej liscie jest wyrzutnia, ktora je strzela (decyzja pkt 2: "najlepsza
        /// amunicja jednostki"); wyrzutnie, tarcze, kamienie do procy i rzucane - nie. ammoOnly = t6 tylko dzieki strzalom/beltom;
        /// armed = jest jakakolwiek bron (lucznik z amunicja ponizej t6 to reszta, nie gole rece).</summary>
        private static int SteelTier(IList<ItemObject> items, out bool ammoOnly, out bool armed)
        {
            int melee = 0, ammo = 0;
            armed = false;
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                if (it == null || it.WeaponComponent == null || IsShield(it)) continue;
                armed = true;
                if (IsLauncher(it) || IsRock(it)) continue;
                int t = (int)it.Tier + 1;                              // PULAPKA: ItemTiers.Tier1 == 0
                if (IsTieredAmmo(it))
                {
                    if (t <= ammo) continue;
                    var pw = it.PrimaryWeapon;
                    if (pw == null) continue;
                    for (int j = 0; j < items.Count; j++)
                    {
                        var l = items[j];
                        if (l != null && IsLauncher(l) && Shoots(l, pw.WeaponClass)) { ammo = t; break; }
                    }
                }
                else if (t > melee) melee = t;
            }
            ammoOnly = ammo >= 6 && melee < 6;
            return melee > ammo ? melee : ammo;
        }

        /// <summary>Pocisk (strzala, belt, bron rzucana, pocisk machiny) z prywatnego slownika misji; null - brak.</summary>
        internal static Mission.Missile MissileOf(Mission mission, int index)
        {
            try
            {
                if (mission == null || index < 0) return null;
                if (!_fMissilesTried) { _fMissilesTried = true; _fMissiles = AccessTools.Field(typeof(Mission), "_missilesDictionary"); }
                var dict = _fMissiles != null ? _fMissiles.GetValue(mission) as Dictionary<int, Mission.Missile> : null;
                Mission.Missile m;
                if (dict != null && dict.TryGetValue(index, out m) && m != null && !m.Weapon.IsEmpty) return m;
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
            bool ammo = false;
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
                    var ms = MissileOf(mission, rec.AffectorWeaponSlotOrMissileIndex);
                    var mi = ms != null ? ms.Weapon.Item : null;
                    if (mi == null)
                    {
                        // brak slownika pociskow: bez VS/szkla (nie zgadujemy po rece); z wyrzutnia w rece - jej najlepsza amunicja w slotach
                        tool = held;
                        if (tool != null && IsLauncher(tool))
                        {
                            tool = AmmoFor(att, tool);
                            if (tool == null) return KRest;
                            ammo = true;
                        }
                    }
                    else
                    {
                        int c = ItemClass(mi);
                        if (c == 1) return KVs;
                        if (c == 2) return KGlass;
                        // 175d: pocisk machiny (balista, katapulta, trebusz, bron okretu - gra, RBM i NavalDLC strzelaja przez
                        // AddCustomMissile z machina jako MissionObjectToIgnore; reka nigdy) - reszta, takze beret balisty (RBM t6)
                        if (ms.MissionObjectToIgnore != null) { note |= NStone; return KRest; }
                        if (IsRock(mi)) { note |= NStone; return KRest; }            // kamien do procy, kamien, glaz, garnek
                        tool = mi;                                     // 175d: strzala/belt wedlug siebie; bron rzucana wedlug siebie jak dotad
                        ammo = IsTieredAmmo(mi);
                    }
                }
            }
            if (tool == null) return KBare;
            if ((int)tool.Tier + 1 >= 6)                               // PULAPKA: ItemTiers.Tier1 == 0
            {
                if (ammo) note |= NAmmo;
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

        /// <summary>Bijacy w symulacji: bohater - jego zestaw bojowy (liczony na zywo); zolnierz - 175d: wzorzec NA ZYWO (po zamianie
        /// 175.2, jak w polu - decyzja pkt 1), pamietany do konca doby (Daily czysci po DailyCatchUp). PreTier - ta sama definicja
        /// na migawce sprzed zamiany (tylko licznik "dawniej t6").</summary>
        private static UnitInfo Info(CharacterObject c)
        {
            UnitInfo u;
            if (!c.IsHero) lock (_gate) { if (_units.TryGetValue(c, out u)) return u; }
            u = new UnitInfo();
            var live = new List<ItemObject>(WeaponsOf(c.BattleEquipments));
            u.Cls = BestClassOf(live);
            bool ao, arm;
            u.Tier = SteelTier(live, out ao, out arm);
            u.AmmoOnly = ao; u.Armed = arm;
            u.OldTier = Mends.BestWeaponTier(c);                      // stara regula (wylaczona zasada): z amunicja i wyrzutnia, jak przed 175
            var pre = c.IsHero ? null : Army175.PreWeapons(c);
            if (pre != null) u.PreTier = SteelTier(pre, out ao, out arm);
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
            var u = Info(striker);
            if (!RuleOn)
            {
                // stara zasada (z amunicja i wyrzutnia) - 175d: na wzorcu po zamianie 175, jak pole (decyzja pkt 1, oba tryby)
                if (u.OldTier >= 6) return KT6;
                return u.OldTier <= 0 ? KBare : KRest;
            }
            if (u.Cls == 1) return KVs;
            if (u.Cls == 2) return KGlass;
            if (u.Tier >= 6)
            {
                if (u.AmmoOnly) note |= NAmmo;
                return KT6;
            }
            if (u.PreTier >= 6) note |= NPreT6;                          // migawka sprzed zamiany 175 dalaby t6 (skutek decyzji pkt 1)
            return u.Armed ? KRest : KBare;
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
                _warned = false;
                Refresh(false);
                MapEvent pe = null;
                try { if (Campaign.Current != null) pe = MapEvent.PlayerMapEvent; } catch { }
                lock (_gate) _curField = pe != null ? BattleOf(pe) : null;
            }
            catch { }
        }

        /// <summary>175d (decyzja Jeffa 09.10 ok. 08:40 pkt 4): pierwszy w tej misji cios gracza albo jego ludzi (druzyna gracza) we wrogiego
        /// Wedrowca bronia, ktora tnie obrazenia (t6 ponizej 100% albo reszta - VS, szklo i ogien bija w pelni, gole rece to nie bron)
        /// - komunikat po angielsku na ekranie i linia w logu. Raz na misje (MissionCheck zeruje), tylko przy wlaczonej zasadzie.</summary>
        internal static void Warn(Agent att, Agent victim, int k)
        {
            try
            {
                if (_warned || !RuleOn) return;
                if (k != KRest && !(k == KT6 && CastlePct < 100)) return;
                if (att == null || victim == null || ReferenceEquals(att, victim)) return;
                var at = att.Team;
                var vt = victim.Team;
                if (at == null || vt == null || !at.IsPlayerTeam || !at.IsEnemyOf(vt)) return;
                _warned = true;
                string text = k == KT6
                    ? "Castle-forged steel barely bites the Others - only Valyrian steel cuts them true."
                    : "Common weapons barely scratch the Others - only Valyrian steel cuts them true.";
                TaleWorlds.Library.InformationManager.DisplayMessage(new TaleWorlds.Library.InformationMessage(text, TaleWorlds.Library.Color.FromUint(0xFFA8D8F0)));
                string who = "?";
                try { who = victim.Name ?? "?"; } catch { }
                Scribe.Line("Inni (175d): komunikat dla gracza - pierwszy cios " + (att.IsMainAgent ? "gracza" : "jego ludzi") + " w '" + who + "' klasa "
                            + (k == KT6 ? "stal t6 (" + CastlePct + "%)" : "reszta (15%)") + ": \"" + text + "\"");
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
                   + (t.Ammo > 0 ? "; strzaly/belty t6 " + t.Ammo : "")
                   + "), reszta " + t.N[KRest] + " (15%"
                   + (t.PreT6 > 0 ? "; dawniej t6 - wzorzec sprzed 175 " + t.PreT6 : "")
                   + (t.Stone > 0 ? "; kamienie i pociski machin " + t.Stone : "")
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
                                  + " i ogien smoka (smok i jezdziec smoka bez broni); stal t6 " + CastlePct + "% (175d - pocisk: strzala i belt wedlug"
                                  + " wlasnego tieru, bron rzucana wedlug siebie, kamienie i pociski machin zawsze 15%; autobitwa: wzorzec po zamianie 175,"
                                  + " najlepsza bron albo strzaly/belty z pasujaca wyrzutnia), reszta 15% (min 1; 0 zostaje 0); komunikat dla gracza przy"
                                  + " pierwszym cieciu w misji"
                                : "WYLACZONA (OthersSteelRule): stara zasada - bron t6 pelne, reszta 15% (autobitwa: wzorzec po zamianie 175 z amunicja,"
                                  + " 175d); ogien smoka pelne (poprawka 175c dziala w obu trybach)")
                            + "; dotyczy " + fighters + " walczacych Wedrowcow (lordowie Innych, Nocny Krol, wskrzeszeni, gracz-Inny; w partiach teraz " + inParty + "): "
                            + (who.Count > 0 ? string.Join(", ", who.ToArray()) : "brak") + (fighters > who.Count ? ", ..." : "")
                            + "; chronionych, ale niewalczacych (notable, dzieci) " + idle
                            + " - wighty bez zmian. Pole: Agent.RegisterBlow; autobitwa: SimulateHit (zolnierze wedlug wzorca po zamianie 175,"
                            + " bohaterowie wedlug zestawu).");
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "OthersSteel.OnSession", null); } catch { } }
        }
    }
}
