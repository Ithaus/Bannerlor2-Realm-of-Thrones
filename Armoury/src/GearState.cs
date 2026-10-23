using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// m110-stan (Jeff 23.10 ok. 15:00, DECYZJA: "nie za duzo co 10, bo kazdy pancerz bedzie wtedy mial 10 typow, zrobmy 4: rozbity/wrak ponizej 10% hp
    /// durability; zmasakrowany powyzej 10%, ale ponizej 40%; uszkodzony powyzej 40%, ale do 80%; sprawny powyzej 80 do 100. I okreslajmy go nazwami, nie
    /// procentami"). CZTERY STANY SPRZETU wedlug pozostalej wytrzymalosci d (ulamek puli): SPRAWNY d > 80 (bez modyfikatora stanu; dobre jakosci tylko tu),
    /// USZKODZONY 40 &lt; d &lt;= 80, ZMASAKROWANY 10 &lt;= d &lt;= 40, WRAK d &lt; 10. Jedna cena i jedna kara na stan (srodek przedzialu: 60 / 25 / 5% -
    /// krzywa ConditionScaling daje -14 / -51 / -81%).
    /// Droga: 48 wlasnych modyfikatorow w Armoury/ModuleData/arm_item_modifiers.xml (16 rodzin x 3) BEZ modifier_group - przy starcie sesji, pod kluczem
    /// GearFourStates, w pamieci: (a) kazdy stary zly modyfikator 16 rodzin, Spoils rl_* i companion_weapon / companion_armor dostaje nazwe i cene swojego
    /// stanu (mapowanie wedlug ceny oryginalnej, rl_* wedlug id); (b) w 16 grupach stare zle modyfikatory zastapione przez Uszkodzony i Zmasakrowany (wraku
    /// nie ma w zadnej grupie - jak dotad nie losuje sie z etykiety), listy wag lupu i produkcji przebudowane, wpis "bez modyfikatora" zostaje. Zapis gry
    /// trzyma tylko StringId modyfikatora - alias i zamiana grup nie wchodza do zapisu; DLL 104 widzi arm_* jako zle modyfikatory po cenie (XML zostaje w grze).
    /// Kopie oryginalow per MBObjectManager (Prepare przed LootPrices.Apply - nowa sesja w tym samym zestawie obiektow najpierw wraca do oryginalu).
    /// Bledy liczone per grupa (grupa zostaje stara, stan dalej czytany z ceny oryginalu), brak ktoregos z 48 modyfikatorow wylacza calosc jawnie (linia NIE).
    /// </summary>
    internal static class GearState
    {
        internal const int Sound = 0, Damaged = 1, Mangled = 2, Wrecked = 3;
        internal static readonly string[] En = { "Sound", "Damaged", "Mangled", "Wrecked" };
        internal static readonly string[] Pl = { "sprawny", "uszkodzony", "zmasakrowany", "wrak" };
        /// <summary>Cena (i stan do krzywej kary) stanu - srodek przedzialu.</summary>
        internal static readonly float[] Value = { 1f, 0.60f, 0.25f, 0.05f };
        /// <summary>Wytrzymalosc reprezentatywna stanu w punktach procentowych (sztuka "przed" z modyfikatora, przesuniecie ksiegi gracza).</summary>
        internal static readonly float[] Mid = { 100f, 60f, 25f, 5f };
        /// <summary>Progi Jeffa (stale): sprawny d > 80, uszkodzony d > 40, zmasakrowany d >= 10, wrak ponizej.</summary>
        internal const float SoundAbove = 80f, DamagedAbove = 40f, WreckBelow = 10f;
        /// <summary>Gorna granica wytrzymalosci stanu w punktach procentowych (sprawny 100, uszkodzony 80, zmasakrowany 40, wrak 10) - prog zniszczenia
        /// sztuki bez wiedzy o dokladnej wytrzymalosci (BelowDestroy).</summary>
        internal static readonly float[] Upper = { 100f, 80f, 40f, 10f };
        internal static readonly string[] Families = { "plate", "chain", "leather", "cloth", "cloth_unarmoured", "sword", "polearm", "mace", "axe", "cheap_weapon",
                                                        "axe_throwing", "knife_throwing", "spear_dart_throwing", "shield", "bow", "crossbow" };
        private static readonly string[] Suffix = { null, "_damaged", "_mangled", "_wrecked" };
        private static readonly string[] NameText = { null, "{=arm_st_damaged}Damaged {ITEMNAME}", "{=arm_st_mangled}Mangled {ITEMNAME}", "{=arm_st_wrecked}Wrecked {ITEMNAME}" };
        private static readonly string[] RlIds = { "rl_looted", "rl_looted_medium", "rl_looted_heavy", "rl_looted_heavy_max" };
        private static readonly int[] RlState = { Damaged, Mangled, Mangled, Wrecked };
        private static readonly string[] CompanionIds = { "companion_weapon", "companion_armor" };
        private const string P = "Stany sprzetu (m110): ";

        /// <summary>Stany zastosowane w tej sesji (klucz GearFourStates przy wczytaniu i wszystkie 48 modyfikatorow z XML). Zmiana klucza - od nastepnego wczytania.</summary>
        internal static bool On { get; private set; }

        // ---- kopie oryginalow (per MBObjectManager - nowe wczytanie gry tworzy nowe obiekty)
        private sealed class ModOrig
        {
            public TextObject Name; public float Price;
            public int Damage, Speed, MissileSpeed, Armor; public short HitPoints;   // recenzja R2: statystyki z XML (wracaja tylko, gdy Stats)
            public bool Stats;
        }
        private sealed class GroupOrig { public List<ItemModifier> Mods; public List<(ItemModifier, float)> Loot, Prod; }
        private static object _mgr;
        private static readonly Dictionary<ItemModifier, ModOrig> _modOrig = new Dictionary<ItemModifier, ModOrig>();
        private static readonly Dictionary<ItemModifierGroup, GroupOrig> _groupOrig = new Dictionary<ItemModifierGroup, GroupOrig>();

        // ---- sesja
        private static readonly Dictionary<ItemModifier, int> _state = new Dictionary<ItemModifier, int>();
        private static readonly Dictionary<ItemModifier, int> _origin = new Dictionary<ItemModifier, int>();   // 1 arm_*, 2 rl_*, 3 stary zly (przemapowany)
        private static readonly Dictionary<ItemModifierGroup, ItemModifier[]> _canon = new Dictionary<ItemModifierGroup, ItemModifier[]>();
        private static readonly Dictionary<ItemModifierGroup, ItemModifier[]> _oldByState = new Dictionary<ItemModifierGroup, ItemModifier[]>();
        private static readonly HashSet<ItemModifierGroup> _hadWreck = new HashSet<ItemModifierGroup>();
        private static readonly ItemModifier[] _rl = new ItemModifier[4];
        private static int _stumbles;
        internal static int Stumbles { get { return _stumbles; } }

        private static FieldInfo _fMods, _fLoot, _fProd;
        private static MethodInfo _setName, _setPrice;
        private static FieldInfo _fName, _fPrice;
        // recenzja R2: setery statystyk (Damage, Speed, MissileSpeed, Armor, HitPoints) - stary zly modyfikator rodziny dostaje statystyki arm_* swojego stanu
        private static readonly string[] StatProps = { "Damage", "Speed", "MissileSpeed", "Armor", "HitPoints" };
        private static MethodInfo[] _setStat;

        // ------------------------------------------------------------ stany
        /// <summary>Stan wedlug pozostalej wytrzymalosci d (procent puli): 0 sprawny (d > 80), 1 uszkodzony (40 &lt; d &lt;= 80), 2 zmasakrowany (10 &lt;= d &lt;= 40), 3 wrak.</summary>
        internal static int OfCondition(float d)
        {
            if (d > SoundAbove) return Sound;
            if (d > DamagedAbove) return Damaged;
            if (d >= WreckBelow) return Mangled;
            return Wrecked;
        }

        /// <summary>Stan sztuki z modyfikatora: null i dobre - 0 (sprawny); arm_*, stare zle 16 rodzin, rl_*, companion - 1..3; inne zle (konie, towary BK) -1.
        /// Bez stanow w tej sesji: tylko 0 albo -1 (wolajacy sprawdzaja On).</summary>
        internal static int Of(ItemModifier m)
        {
            if (m == null) return Sound;
            int s;
            if (_state.TryGetValue(m, out s)) return s;
            // m110-stan krok 2: kopia modyfikatora w klonie sprzetu agenta (HeroHp - slownik po referencji, kopii tu nie ma) - stan oryginalu
            ItemModifier o;
            if (HeroHp.CopyOrig(m, out o)) return o == null ? Sound : Of(o);
            return m.PriceMultiplier >= 0.999f ? Sound : -1;
        }

        /// <summary>Stan modyfikatora wedlug id (ksiega zuzycia trzyma id); puste id - sprawny, nieznane - -1.</summary>
        internal static int OfId(string id)
        {
            if (string.IsNullOrEmpty(id)) return Sound;
            try
            {
                var m = MBObjectManager.Instance != null ? MBObjectManager.Instance.GetObject<ItemModifier>(id) : null;
                return m == null ? -1 : Of(m);
            }
            catch { return -1; }
        }

        /// <summary>Stan 1..3 sztuki w tej sesji (dla napisow) albo -1.</summary>
        internal static int Shown(ItemModifier m)
        {
            if (!On || m == null) return -1;
            int s = Of(m);
            return s >= Damaged ? s : -1;
        }

        /// <summary>Napis stanu dla gracza: slowo stanu (Damaged, Mangled, Wrecked) albo dotychczasowy procent.</summary>
        internal static string CondText(ItemModifier m, int pct)
        {
            int s = Shown(m);
            return s > 0 ? En[s] : pct + "%";
        }

        /// <summary>Nazwa stanu sztuki w komunikacie (QuartermasterLaw): slowo stanu albo nazwa modyfikatora jak dotad.</summary>
        internal static string ModName(ItemModifier m)
        {
            if (m == null) return "";
            int s = Shown(m);
            return s > 0 ? En[s] : (m.Name != null ? m.Name.ToString() : m.StringId);
        }

        private static ItemModifierGroup GroupOf(ItemObject it)
        {
            return it != null && it.ItemComponent != null ? it.ItemComponent.ItemModifierGroup : null;
        }

        /// <summary>Grupa ma stany w tej sesji (zamieniona albo stara z przemapowanymi stanami).</summary>
        internal static bool HandlesGroup(ItemModifierGroup g)
        {
            return On && g != null && (_canon.ContainsKey(g) || _oldByState.ContainsKey(g));
        }

        internal static bool Handles(ItemObject it) { return HandlesGroup(GroupOf(it)); }

        /// <summary>Modyfikator stanu s (1..3) w grupie: kanoniczny arm_*; grupa niezamieniona (blad) - stary zly modyfikator o tym stanie; null - brak.</summary>
        internal static ItemModifier ModForGroup(ItemModifierGroup g, int s)
        {
            if (!On || g == null || s <= Sound || s > Wrecked) return null;
            ItemModifier[] a;
            if (_canon.TryGetValue(g, out a) && a[s] != null) return a[s];
            if (_oldByState.TryGetValue(g, out a)) return a[s];
            return null;
        }

        internal static ItemModifier ModFor(ItemObject it, int s) { return ModForGroup(GroupOf(it), s); }

        /// <summary>Stan lupu: modyfikator Spoils (rl_looted - uszkodzony, rl_looted_heavy - zmasakrowany, rl_looted_heavy_max - wrak), bo Spoils rozpoznaje lup
        /// tylko po tych id (IsLootedModifier: kwatermistrz, przetop, paser, darowizny); bez Spoils - arm_*. Sprawny - null.</summary>
        internal static ItemModifier LootMod(ItemObject it, int s)
        {
            if (!On || s <= Sound) return null;
            if (s > Wrecked) s = Wrecked;
            return _rl[s] ?? ModFor(it, s);
        }

        /// <summary>Nastepny gorszy stan (zuzycie gracza, wojska, AI): o jeden stan az do wraku. false - przedmiot poza stanami (wolajacy jak dotad);
        /// true i next null - dno (wrak) albo brak modyfikatora nizszego stanu.</summary>
        internal static bool TryNext(ItemObject it, ItemModifier cur, out ItemModifier next)
        {
            next = null;
            var g = GroupOf(it);
            if (!HandlesGroup(g)) return false;
            int s = Of(cur);
            if (s < 0) return false;
            if (s >= Wrecked) return true;
            next = ModForGroup(g, s + 1);
            return true;
        }

        /// <summary>Pierwszy stopien zuzycia (uszkodzony). false - przedmiot poza stanami.</summary>
        internal static bool TryMild(ItemObject it, out ItemModifier mod)
        {
            mod = null;
            var g = GroupOf(it);
            if (!HandlesGroup(g)) return false;
            mod = ModForGroup(g, Damaged);
            return true;
        }

        /// <summary>Loteria stanu bez wiedzy o ciele (PickWornModifier, WearTheLoot) - dzisiejsze udzialy na rodzine: warunek > 55 uszkodzony, > 30 zmasakrowany,
        /// reszta wrak, jesli grupa miala go wczesniej (zbroje RBM), inaczej zmasakrowany (bron). false - przedmiot poza stanami.</summary>
        internal static bool TryLottery(ItemObject it, float cond, out ItemModifier mod)
        {
            mod = null;
            var g = GroupOf(it);
            if (!HandlesGroup(g)) return false;
            int s = cond > 55f ? Damaged : cond > 30f ? Mangled : (_hadWreck.Contains(g) ? Wrecked : Mangled);
            mod = ModForGroup(g, s);
            if (mod == null && s == Wrecked) mod = ModForGroup(g, Mangled);
            return true;
        }

        /// <summary>Sztuka poza grupa zbroi i broni, ktora dostaje stan (arm_*, stare zle, rl_*): 1 arm_*, 2 rl_*, 3 stary; 0 - inna.</summary>
        internal static int OriginOf(ItemModifier m)
        {
            int o;
            return m != null && _origin.TryGetValue(m, out o) ? o : 0;
        }

        /// <summary>Recenzja R7: sztuka ponizej progu zniszczenia min (LootMinConditionPercent) bez wiedzy o dokladnej wytrzymalosci - przy stanach caly
        /// przedzial stanu musi lezec na progu albo nizej (wrak dopiero od progu 10 - jak 104, gdzie wrak kosztowal 0.10; cena wraku 0.05 nie niszczy go juz
        /// przy progu 5-9); inne modyfikatory - cena x 100 &lt;= min jak dotad. Czesc z ciala z ksiega obrazen niszczy LootWear wedlug d.</summary>
        internal static bool BelowDestroy(ItemModifier m, int min)
        {
            if (m == null || min <= 0) return false;
            int s = Shown(m);
            if (s > 0) return Upper[s] <= min + 0.01f;
            return m.PriceMultiplier * 100f <= min + 0.01f;
        }

        // ------------------------------------------------------------ start sesji
        /// <summary>PRZED LootPrices.Apply: nowy MBObjectManager - kopie od zera; ten sam (nowa sesja w tym samym zestawie obiektow) - powrot do oryginalu,
        /// zeby LootPrices i Apply liczyly od stanu z XML.</summary>
        internal static void Prepare()
        {
            try
            {
                var mgr = MBObjectManager.Instance;
                if (mgr == null) return;
                if (!ReferenceEquals(mgr, _mgr)) { _mgr = mgr; _modOrig.Clear(); _groupOrig.Clear(); }
                else Restore();
                // rl_* przed LootPrices (ich cena oryginalna z XML Spoils)
                foreach (var id in RlIds) { var m = mgr.GetObject<ItemModifier>(id); if (m != null) Capture(m); }
            }
            catch (Exception e) { Stumble("GearState.Prepare", e); }
        }

        private static void Capture(ItemModifier m)
        {
            if (m == null || _modOrig.ContainsKey(m)) return;
            _modOrig[m] = new ModOrig { Name = m.Name, Price = m.PriceMultiplier, Damage = m.Damage, Speed = m.Speed, MissileSpeed = m.MissileSpeed, Armor = m.Armor, HitPoints = m.HitPoints };
        }

        /// <summary>Recenzja R2: statystyki modyfikatora (wszystkie 5 seterow albo nic). false - setery nie pasuja.</summary>
        private static bool SetStats(ItemModifier m, int damage, int speed, int missileSpeed, int armor, short hitPoints)
        {
            if (_setStat == null) return false;
            foreach (var s in _setStat) if (s == null) return false;
            _setStat[0].Invoke(m, new object[] { damage });
            _setStat[1].Invoke(m, new object[] { speed });
            _setStat[2].Invoke(m, new object[] { missileSpeed });
            _setStat[3].Invoke(m, new object[] { armor });
            _setStat[4].Invoke(m, new object[] { hitPoints });
            return true;
        }

        /// <summary>Recenzja R2: stary zly modyfikator rodziny (dull_sword, rusty_plate, chain_damage_95...) dostaje statystyki arm_* swojego stanu - jeden
        /// stan w nazwie = jedne statystyki (szybkosc, wytrzymalosc tarczy, pocisk; pancerz i obrazenia przy wylaczonym ConditionScalesStats). Oryginal w kopii
        /// (Restore). false - setery nie pasuja albo brak kopii.</summary>
        private static bool AlignStats(ItemModifier m, ItemModifier canon)
        {
            ModOrig o;
            if (m == null || canon == null || !_modOrig.TryGetValue(m, out o)) return false;
            if (!SetStats(m, canon.Damage, canon.Speed, canon.MissileSpeed, canon.Armor, canon.HitPoints)) return false;
            o.Stats = true;
            return true;
        }

        private static float OrigPrice(ItemModifier m)
        {
            ModOrig o;
            return m != null && _modOrig.TryGetValue(m, out o) ? o.Price : (m != null ? m.PriceMultiplier : 1f);
        }

        private static GroupOrig CaptureGroup(ItemModifierGroup g)
        {
            GroupOrig go;
            if (_groupOrig.TryGetValue(g, out go)) return go;
            var mods = _fMods.GetValue(g) as List<ItemModifier>;
            var loot = _fLoot.GetValue(g) as List<(ItemModifier, float)>;
            var prod = _fProd.GetValue(g) as List<(ItemModifier, float)>;
            if (mods == null || loot == null || prod == null) throw new InvalidOperationException("listy grupy " + g.StringId + " nie pasuja");
            go = new GroupOrig { Mods = new List<ItemModifier>(mods), Loot = new List<(ItemModifier, float)>(loot), Prod = new List<(ItemModifier, float)>(prod) };
            foreach (var m in go.Mods) Capture(m);
            _groupOrig[g] = go;
            return go;
        }

        private static void SetGroup(ItemModifierGroup g, List<ItemModifier> mods, List<(ItemModifier, float)> loot, List<(ItemModifier, float)> prod)
        {
            var lm = (List<ItemModifier>)_fMods.GetValue(g);
            var ll = (List<(ItemModifier, float)>)_fLoot.GetValue(g);
            var lp = (List<(ItemModifier, float)>)_fProd.GetValue(g);
            lm.Clear(); lm.AddRange(mods);
            ll.Clear(); ll.AddRange(loot);
            lp.Clear(); lp.AddRange(prod);
        }

        private static void SetNamePrice(ItemModifier m, TextObject name, float price)
        {
            if (_setName != null) _setName.Invoke(m, new object[] { name }); else if (_fName != null) _fName.SetValue(m, name);
            if (_setPrice != null) _setPrice.Invoke(m, new object[] { price }); else if (_fPrice != null) _fPrice.SetValue(m, price);
        }

        private static bool Reflect()
        {
            _fMods = AccessTools.Field(typeof(ItemModifierGroup), "_itemModifiers");
            _fLoot = AccessTools.Field(typeof(ItemModifierGroup), "_lootDropItemModifierScores");
            _fProd = AccessTools.Field(typeof(ItemModifierGroup), "_productionDropItemModifierScores");
            _setName = AccessTools.PropertySetter(typeof(ItemModifier), "Name");
            _setPrice = AccessTools.PropertySetter(typeof(ItemModifier), "PriceMultiplier");
            _fName = _setName == null ? AccessTools.Field(typeof(ItemModifier), "<Name>k__BackingField") : null;
            _fPrice = _setPrice == null ? AccessTools.Field(typeof(ItemModifier), "<PriceMultiplier>k__BackingField") : null;
            _setStat = new MethodInfo[StatProps.Length];
            for (int i = 0; i < StatProps.Length; i++) _setStat[i] = AccessTools.PropertySetter(typeof(ItemModifier), StatProps[i]);   // recenzja R2 (brak - statystyki bez zmian)
            return _fMods != null && _fLoot != null && _fProd != null && (_setName != null || _fName != null) && (_setPrice != null || _fPrice != null);
        }

        /// <summary>Powrot do oryginalu (tylko obiekty tego MBObjectManager - nowe wczytanie ma nowe obiekty z XML).</summary>
        private static void Restore()
        {
            if (_modOrig.Count == 0 && _groupOrig.Count == 0) return;
            if (!Reflect()) return;
            foreach (var kv in _modOrig)
                try
                {
                    var o = kv.Value;
                    SetNamePrice(kv.Key, o.Name, o.Price);
                    if (o.Stats && SetStats(kv.Key, o.Damage, o.Speed, o.MissileSpeed, o.Armor, o.HitPoints)) o.Stats = false;   // recenzja R2: tylko zmienione
                }
                catch (Exception e) { Stumble("GearState.Restore(mod)", e); }
            foreach (var kv in _groupOrig)
                try { SetGroup(kv.Key, kv.Value.Mods, kv.Value.Loot, kv.Value.Prod); } catch (Exception e) { Stumble("GearState.Restore(grupa)", e); }
        }

        /// <summary>Mapowanie starego zlego modyfikatora wedlug ceny oryginalnej: 0.5 i wiecej - uszkodzony (0.55-0.6), powyzej 0.15 - zmasakrowany (0.25-0.4),
        /// reszta - wrak (RBM *_damage_* 0.1: dzisiejsza regula "wraki na zlom" - zadna sztuka ze zlomu nie staje sie naprawialna).</summary>
        internal static int MapPrice(float p)
        {
            if (p >= 0.999f) return Sound;
            if (p >= 0.5f) return Damaged;
            if (p > 0.15f) return Mangled;
            return Wrecked;
        }

        /// <summary>Po LootPrices.Apply, przed McmSettings.Apply / ArmsPricing.Build / StartStock: stany w modyfikatorach i grupach (pod kluczem).</summary>
        internal static void Apply()
        {
            On = false;
            _state.Clear(); _origin.Clear(); _canon.Clear(); _oldByState.Clear(); _hadWreck.Clear();
            for (int i = 0; i < _rl.Length; i++) _rl[i] = null;
            _stumbles = 0;
            var sb = new StringBuilder(P + "start");
            try
            {
                var s = Settings.Current;
                var mgr = MBObjectManager.Instance;
                if (mgr == null) return;
                bool key = s != null && s.GearFourStates;
                var arm = new ItemModifier[Families.Length, 4];
                int xml = 0;
                var missing = new List<string>();
                for (int f = 0; f < Families.Length; f++)
                    for (int st = Damaged; st <= Wrecked; st++)
                    {
                        string id = "arm_" + Families[f] + Suffix[st];
                        arm[f, st] = mgr.GetObject<ItemModifier>(id);
                        if (arm[f, st] != null) xml++; else if (missing.Count < 4) missing.Add(id);
                    }
                sb.Append(" | klucz GearFourStates ").Append(key ? "TAK" : "NIE").Append(" | XML arm_* ").Append(xml).Append("/48");
                if (!key) { sb.Append(" | stany NIE - jak 104 (procent w nazwie, progi ThresholdWorn/Damaged/Ruined)."); Log.Info(sb.ToString()); return; }
                if (xml < 48)
                {
                    sb.Append(" | stany NIE - brak modyfikatorow z Armoury/ModuleData/arm_item_modifiers.xml (np. ").Append(string.Join(", ", missing.ToArray()))
                      .Append(") - jak 104.");
                    Log.Info(sb.ToString());
                    return;
                }
                if (!Reflect())
                {
                    sb.Append(" | stany NIE - pola ItemModifierGroup / ItemModifier nie pasuja (gra inna niz 1.4.8?) - jak 104.");
                    Log.Info(sb.ToString());
                    return;
                }
                // arm_* - stan zawsze (sztuki w zapisie, takze gdy grupy ich rodziny brak)
                for (int f = 0; f < Families.Length; f++)
                    for (int st = Damaged; st <= Wrecked; st++) { _state[arm[f, st]] = st; _origin[arm[f, st]] = 1; }

                int swapped = 0, nDam = 0, nMan = 0, nWr = 0, nStat = 0;
                var skipped = new List<string>();
                var failed = new List<string>();
                for (int f = 0; f < Families.Length; f++)
                {
                    var g = mgr.GetObject<ItemModifierGroup>(Families[f]);
                    if (g == null) { skipped.Add(Families[f]); continue; }
                    GroupOrig go = null;
                    try
                    {
                        go = CaptureGroup(g);
                        var byState = new ItemModifier[4];
                        var good = new HashSet<ItemModifier>();
                        var goodList = new List<ItemModifier>();
                        foreach (var m in go.Mods)
                        {
                            if (m == null) continue;
                            float p = OrigPrice(m);
                            int st = MapPrice(p);
                            if (st == Sound) { if (good.Add(m)) goodList.Add(m); continue; }
                            if (_state.ContainsKey(m)) continue;   // ten sam modyfikator w grupie dwa razy
                            _state[m] = st; _origin[m] = 3;
                            if (st == Damaged) nDam++; else if (st == Mangled) nMan++; else nWr++;
                            if (st == Wrecked) _hadWreck.Add(g);
                            if (byState[st] == null || p > OrigPrice(byState[st])) byState[st] = m;
                            SetNamePrice(m, new TextObject(NameText[st]), Value[st]);
                            if (AlignStats(m, arm[f, st])) nStat++;   // recenzja R2: statystyki arm_* tego stanu (rl_* i companion - wspolne dla rodzin, bez zmian)
                        }
                        _oldByState[g] = byState;
                        var a1 = arm[f, Damaged];
                        var a2 = arm[f, Mangled];
                        var mods = new List<ItemModifier>(goodList) { a1, a2 };
                        var loot = new List<(ItemModifier, float)>();
                        var prod = new List<(ItemModifier, float)>();
                        foreach (var e in go.Loot) if (e.Item1 != null && good.Contains(e.Item1)) loot.Add(e);
                        loot.Add((a1, a1.LootDropScore)); loot.Add((a2, a2.LootDropScore));
                        foreach (var e in go.Loot) if (e.Item1 == null) loot.Add(e);
                        foreach (var e in go.Prod) if (e.Item1 != null && good.Contains(e.Item1)) prod.Add(e);
                        prod.Add((a1, a1.ProductionDropScore)); prod.Add((a2, a2.ProductionDropScore));
                        foreach (var e in go.Prod) if (e.Item1 == null) prod.Add(e);
                        SetGroup(g, mods, loot, prod);
                        _canon[g] = new[] { null, a1, a2, arm[f, Wrecked] };
                        swapped++;
                    }
                    catch (Exception e)
                    {
                        failed.Add(Families[f]);
                        Stumble("GearState.Apply(" + Families[f] + ")", e);
                        try { if (go != null) SetGroup(g, go.Mods, go.Loot, go.Prod); } catch { }   // grupa zostaje stara; stany starych zlych zostaja (z ceny)
                        _canon.Remove(g);
                    }
                }
                // Spoils rl_* wedlug id (LootPrices ustawil im ceny 55/40/25/10 - tu stan)
                int rl = 0;
                for (int i = 0; i < RlIds.Length; i++)
                {
                    var m = mgr.GetObject<ItemModifier>(RlIds[i]);
                    if (m == null) continue;
                    Capture(m);
                    int st = RlState[i];
                    _state[m] = st; _origin[m] = 2;
                    SetNamePrice(m, new TextObject(NameText[st]), Value[st]);
                    if (i == 0) _rl[Damaged] = m; else if (i == 2) _rl[Mangled] = m; else if (i == 3) _rl[Wrecked] = m;
                    rl++;
                }
                // companion_weapon / companion_armor (0.4) - zmasakrowany
                int comp = 0;
                foreach (var id in CompanionIds)
                {
                    var m = mgr.GetObject<ItemModifier>(id);
                    if (m == null || _state.ContainsKey(m)) continue;
                    Capture(m);
                    int st = MapPrice(OrigPrice(m));
                    if (st == Sound) continue;
                    _state[m] = st; _origin[m] = 3;
                    SetNamePrice(m, new TextObject(NameText[st]), Value[st]);
                    comp++;
                }
                On = true;
                try { ArmourQuality.ClearLadders(); } catch (Exception e) { Stumble("GearState.ClearLadders", e); }
                try { QualityScarcity.ClearModCache(); } catch (Exception e) { Stumble("GearState.ClearModCache", e); }

                string check = SelfCheck();
                sb.Append(" | grupy zamienione ").Append(swapped).Append('/').Append(Families.Length)
                  .Append(", pominiete (brak grupy) ").Append(skipped.Count > 0 ? string.Join(" ", skipped.ToArray()) : "brak")
                  .Append(", bledy ").Append(failed.Count > 0 ? string.Join(" ", failed.ToArray()) : "brak")
                  .Append(" | stare zle -> stany: uszkodzony ").Append(nDam).Append(", zmasakrowany ").Append(nMan).Append(", wrak ").Append(nWr)
                  .Append(" (rl_* ").Append(rl).Append("/4, companion ").Append(comp).Append(')')
                  .Append(" | progi 80/40/10, wartosci 60/25/5")
                  .Append(" | kontrola: ").Append(check)
                  .Append(" | lup z obrazen ").Append(s.LootStateFromWounds && s.BattlefieldLawEnabled ? "TAK" : "NIE")
                  .Append(" (latki DTE ").Append(BattlefieldLaw.LootWiredCount).Append("/2, Spoils lucky ").Append(BattlefieldLaw.LuckyFound ? "TAK" : "NIE").Append(')')
                  .Append(" | potkniecia ").Append(_stumbles)
                  .Append(" | statystyki starych zlych jak arm_* ").Append(nStat).Append('/').Append(nDam + nMan + nWr).Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e)
            {
                Stumble("GearState.Apply", e);
                Log.Info(sb.Append(" | stany NIE - wyjatek (" + e.GetType().Name + ") - jak 104.").ToString());
                On = false;
                try { Restore(); } catch { }
                try { LootPrices.Apply(); } catch { }   // Restore wrocil ceny rl_* do XML Spoils - cena lupu = stan jak dotad
                _state.Clear(); _origin.Clear(); _canon.Clear(); _oldByState.Clear(); _hadWreck.Clear();
                return;
            }
        }

        /// <summary>Recenzja R3: linia "rynsztunek i sakwy gracza" PO WearGroups.Fix (ArmouryBehavior.OnSessionLaunched) - dopiero wtedy uprzaz, luki i kusze bez
        /// grupy w XML maja grupe i sa liczone (jak w linii "zapis"); dotad linia z Apply pomijala je i STAN-LC dawal falszywe NIE.</summary>
        internal static void SessionLine()
        {
            try { GearLine("rynsztunek i sakwy gracza"); } catch (Exception e) { Stumble("GearState.GearLine", e); }
        }

        /// <summary>Kontrola progow i arytmetyki (linia startu): przyklad Jeffa 480 z 2000 = 76% -> uszkodzony i granice przedzialow.</summary>
        private static string SelfCheck()
        {
            float d = 100f - 480f * 100f / 2000f;
            bool ok = OfCondition(d) == Damaged && OfCondition(80f) == Damaged && OfCondition(80.1f) == Sound && OfCondition(40f) == Mangled
                      && OfCondition(10f) == Mangled && OfCondition(9.9f) == Wrecked && MapPrice(0.6f) == Damaged && MapPrice(0.3f) == Mangled && MapPrice(0.1f) == Wrecked;
            return "480/2000 -> " + d.ToString("0", CultureInfo.InvariantCulture) + "% " + Pl[OfCondition(d)] + ", 80 " + Pl[OfCondition(80f)] + ", 80.1 " + Pl[OfCondition(80.1f)]
                   + ", 40 " + Pl[OfCondition(40f)] + ", 10 " + Pl[OfCondition(10f)] + ", 9.9 " + Pl[OfCondition(9.9f)] + " - " + (ok ? "OK" : "BLAD");
        }

        // ------------------------------------------------------------ linie gracza (zgodnosc starych zapisow)
        private sealed class Count
        {
            public readonly int[] St = new int[4];
            public int Old, Arm, Rl, ArmEq, ArmBag;
        }

        private static void Add(Count c, ItemObject it, ItemModifier m, int n, bool eq)
        {
            if (it == null || n <= 0 || !Handles(it)) return;
            int s = Of(m);
            if (s < 0) return;
            c.St[s] += n;
            int o = OriginOf(m);
            if (o == 1) { c.Arm += n; if (eq) c.ArmEq += n; else c.ArmBag += n; }
            else if (o == 2) c.Rl += n;
            else if (o == 3) c.Old += n;
        }

        private static Count CountPlayer()
        {
            var c = new Count();
            var h = Hero.MainHero;
            if (h != null)
            {
                foreach (var eqs in new[] { h.BattleEquipment, h.CivilianEquipment })
                {
                    if (eqs == null) continue;
                    for (int i = 0; i < 12; i++) { var el = eqs[i]; Add(c, el.Item, el.ItemModifier, 1, true); }
                }
            }
            var mp = MobileParty.MainParty;
            var r = mp != null ? mp.ItemRoster : null;
            if (r != null)
                for (int i = 0; i < r.Count; i++) { var el = r.GetElementCopyAtIndex(i); Add(c, el.EquipmentElement.Item, el.EquipmentElement.ItemModifier, el.Amount, false); }
            return c;
        }

        /// <summary>Linia "rynsztunek i sakwy gracza" (raz na sesje) - jak stary zapis widzi stany: przemapowane stare modyfikatory, arm_*, rl_*, ksiega zuzycia.</summary>
        private static void GearLine(string what)
        {
            if (!On || Hero.MainHero == null) return;
            var c = CountPlayer();
            int entries = 0, shifted = 0, armBook = 0;
            var beh = ArmouryBehavior.Instance;
            if (beh != null) beh.BookStats(out entries, out shifted, out armBook);
            Log.Info(P + what + " | sprawny " + c.St[0] + ", uszkodzony " + c.St[1] + ", zmasakrowany " + c.St[2] + ", wrak " + c.St[3]
                     + " | stare (przemapowane) " + c.Old + ", arm_* " + c.Arm + ", rl_* " + c.Rl + " | ksiega: wpisow " + entries + ", z przesunieciem " + shifted + ".");
        }

        /// <summary>Linia "zapis" (tylko pod flaga testu stanu): ile sztuk gracza z arm_* idzie do zapisu (zgodnosc po wczytaniu, takze na DLL 104).</summary>
        internal static void SaveLine()
        {
            try
            {
                if (!On || !TestWanted()) return;
                var c = CountPlayer();
                int entries = 0, shifted = 0, armBook = 0;
                var beh = ArmouryBehavior.Instance;
                if (beh != null) beh.BookStats(out entries, out shifted, out armBook);
                Log.Info(P + "zapis | gracz arm_* " + c.Arm + " (rynsztunek " + c.ArmEq + ", sakwy " + c.ArmBag + "), rl_* " + c.Rl + ", stare " + c.Old
                         + " | ksiega wpisow z arm_* " + armBook + ".");
            }
            catch (Exception e) { Stumble("GearState.SaveLine", e); }
        }

        internal const string AutoFlagName = "armoury-stan.flag";

        /// <summary>Hak autotestu stanow: plik CrashScribe/armoury-stan.flag i tryb autotestu (LogGate 1/2) - w grze Jeffa false.</summary>
        internal static bool TestWanted()
        {
            int m = LogGate.Mode;
            if (m != 1 && m != 2) return false;
            try
            {
                var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                return System.IO.File.Exists(System.IO.Path.Combine(System.IO.Path.Combine(System.IO.Path.Combine(docs, "Mount and Blade II Bannerlord"), "CrashScribe"), AutoFlagName));
            }
            catch { return false; }
        }

        internal static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_stumbles <= 3 || _stumbles % 50 == 0) Log.Error(where, e);
        }
    }
}
