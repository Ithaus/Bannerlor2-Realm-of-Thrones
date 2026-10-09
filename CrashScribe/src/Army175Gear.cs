using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace CrashScribe
{
    /// <summary>
    /// PACZKA 175 - ARMIE KROLESTW (Jeff 09.10, decyzje 1-8; projekt docs/PROJEKT-175-ARMIE-2026-10-09.md).
    /// Czesc 175.2 - SPRZET WEDLUG TIERU (decyzja 8b: "kto zrobil, ze tier 4 dostawal tarcze tieru 6
    /// i pod to umiejetnosc podnoszono, to jakas bzdura, trzeba wszedzie poprawic").
    ///
    /// Kazda jednostka zolnierska (nie bohater, nie szablon) ma w kazdym zestawie BOJOWYM, w slotach
    /// broni 0-3 i pancerza 5-9, przedmioty o tierze gry &lt;= tier jednostki. Przedmiot ponad tier
    /// zastepuje przedmiot tego samego rodzaju (typ + klasa broni, dlugosc, luk dlugi za dlugi,
    /// tarcza tej samej klasy, ta sama amunicja, ten sam slot pancerza), wybrany kaskada kultura/tier.
    /// To tylko WZORCE (co AI kupuje, czego DTE szuka na polce) - zaden przedmiot nie powstaje w swiecie.
    /// Wymogi przedmiotow, SkillSinew ("umiejetnosc do wlasnego sprzetu"), prawa tieru, konie i ladry
    /// (sloty 10-11), sztandar (4), zestawy cywilne - BEZ zmian.
    ///
    /// Kiedy: AfterRegisterSubModuleObjects (przed SyncData, przed zalogami nowej gry i przed kazdym
    /// OnSessionLaunched - Armoury TroopFit/ColdStart widza juz nowe wzorce). Zmieniana jednostka dostaje
    /// ZAWSZE nowy, nierejestrowany MBEquipmentRoster z klonami zmienionych zestawow - stary roster
    /// (dzielony z bohaterami przez FillFrom/InitializeEquipmentsOnLoad i z innymi postaciami przez
    /// EquipmentSet) zostaje nietkniety.
    /// </summary>
    internal static partial class Army175
    {
        // ---------------- wspolne ----------------

        internal static bool Wanted(string key, float def) { return Mends.ArmouryFloat(key, def) >= 0.5f; }

        private static readonly System.Reflection.FieldInfo FRoster = AccessTools.Field(typeof(BasicCharacterObject), "_equipmentRoster");
        private static readonly System.Reflection.FieldInfo FEquipments = AccessTools.Field(typeof(MBEquipmentRoster), "_equipments");
        private static readonly System.Reflection.MethodInfo MSetRosterCulture = AccessTools.PropertySetter(typeof(MBEquipmentRoster), "EquipmentCulture");
        private static readonly System.Reflection.MethodInfo MSetRosterCategories = AccessTools.PropertySetter(typeof(MBEquipmentRoster), "EquipmentCategories");

        // KOPIA listy Armoury MountLaw.Essos (pole prywatne w Armoury, CS nie ma referencji do Armoury).
        // Zrodlo: Armoury/src/MountLaw.cs:30 - przy zmianie tamtej listy poprawic tez te.
        private static readonly HashSet<string> Essos = new HashSet<string>(StringComparer.Ordinal) {
            "empire", "ghiscari", "ibbenese", "khuzait", "lyseni", "myrish", "nord", "norvos", "pentoshi", "qartheen",
            "qohorik", "sarnor", "summer", "tyroshi", "valyrian", "volantine", "yiti", "yiti_bandits", "darshi"
        };

        internal static bool TierGearApplied;
        private static Dictionary<ItemObject, int> _tierCache;
        private static Dictionary<string, List<ItemObject>> _pool;          // rodzaj (WKey) -> przedmioty puli
        private static Dictionary<ItemObject, HashSet<string>> _worn;       // przedmiot -> kultury zolnierzy, ktorzy go nosza
        private static Dictionary<string, Pick> _choice;                    // klucz (przedmiot, sufit, cechy jednostki) -> wybor
        private static List<KeyValuePair<ItemObject, int>> _replaced;       // (oryginal, tier jednostki) - do licznika q
        private static HashSet<string> _noRepl;                             // klucze bez zadnego zamiennika
        private static Dictionary<CharacterObject, ItemObject[]> _preWeapons;  // migawka valyrianska (bron sprzed zamiany)
        private static int _rej1, _rej2, _rej3, _rej4, _rej5;               // odrzuceni kandydaci wedlug powodu
        private static int _valyrianT6;                                     // jednostek z bronia t6 we wzorcu sprzed zamiany
        private static string[] _races;

        /// <summary>Stan sesji - wolane z SubModuleMain.OnGameStart (przed AfterRegister).</summary>
        internal static void Reset()
        {
            TierGearApplied = false;
            _tierCache = null; _pool = null; _worn = null; _choice = null; _replaced = null; _noRepl = null;
            _preWeapons = null; _valyrianT6 = 0;
            _rej1 = _rej2 = _rej3 = _rej4 = _rej5 = 0;
            NorthDone = false; DothrakiDone = false;
            CompositionApplied = false; DothrakiPoolActive = false; _poolFirstLogged = false;
        }

        /// <summary>AfterRegisterSubModuleObjects (CS): sklad, migawka valyrianska, sprzet wedlug tieru,
        /// lore w granicy tieru, Zlota Kompania. Kazdy krok w osobnym try.</summary>
        internal static void OnObjectsRegistered()
        {
            if (Campaign.Current == null) return;
            try { Composition(); } catch (Exception e) { try { Scribe.Report("CrashScribe", e, "Army175.Composition", null); } catch { } }
            try { ValyrianSnapshot(); } catch (Exception e) { try { Scribe.Report("CrashScribe", e, "Army175.ValyrianSnapshot", null); } catch { } }
            try { TierGear("przy wczytaniu, przed sesja"); } catch (Exception e) { try { Scribe.Report("CrashScribe", e, "Army175.TierGear", null); } catch { } }
            try { LoreArmor(); } catch (Exception e) { try { Scribe.Report("CrashScribe", e, "Army175.LoreArmor", null); } catch { } }
            try { GoldenBows(); } catch (Exception e) { try { Scribe.Report("CrashScribe", e, "Army175.GoldenBows", null); } catch { } }
        }

        /// <summary>DailyTick (zabezpieczenie jak SkillSinew): gdy przy wczytaniu zestawy byly puste.</summary>
        internal static void DailyCatchUp()
        {
            try
            {
                if (TierGearApplied || !Wanted("Army175TierGear", 1f)) return;
                TierGear("pierwsza doba (zapas)");
                LoreArmor();
                GoldenBows();
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "Army175.DailyCatchUp", null); } catch { } }
        }

        internal static bool IsSoldierOcc(Occupation o)
        {
            return o == Occupation.Soldier || o == Occupation.Mercenary || o == Occupation.Bandit
                || o == Occupation.CaravanGuard || o == Occupation.Gangster;
        }

        /// <summary>Tier jednostki jak w grze, t0 liczony jak t1 (sprzet.py: T = max(1, tier)).</summary>
        internal static int UnitTier(CharacterObject co)
        {
            int t = co.Tier;
            if (t < 1) t = 1;
            if (t > 6) t = 6;
            return t;
        }

        /// <summary>Tier przedmiotu liczony przez gre (RBM) - PULAPKA CLAUDE.md 7: Tier1 == 0.</summary>
        internal static int TierOf(ItemObject it)
        {
            if (it == null) return 0;
            if (_tierCache == null) _tierCache = new Dictionary<ItemObject, int>();
            int g;
            if (_tierCache.TryGetValue(it, out g)) return g;
            try { g = (int)it.Tier + 1; } catch { g = 0; }
            if (g < 0) g = 0;
            if (g > 6) g = 6;
            _tierCache[it] = g;
            return g;
        }

        /// <summary>Nazwa rasy (FaceGen); gdy lista ras niedostepna - zapas po id jednostki (giant/wight).</summary>
        private static string RaceName(CharacterObject co)
        {
            try { if (_races == null) _races = FaceGen.GetRaceNames() ?? new string[0]; }
            catch { _races = new string[0]; }
            try
            {
                if (co.Race > 0 && co.Race < _races.Length) return _races[co.Race] ?? "";
                if (co.Race > 0 && _races.Length == 0)
                {
                    var id = co.StringId ?? "";
                    if (id.Contains("giant")) return "giant";
                    if (id.Contains("wight")) return "wight";
                }
            }
            catch { }
            return "";
        }

        private static string CultOf(ItemObject it)
        {
            try { return it.Culture != null ? (it.Culture.StringId ?? "") : ""; } catch { return ""; }
        }

        private static string Side(string cult)
        {
            if (string.IsNullOrEmpty(cult) || cult == "neutral_culture") return "";
            return Essos.Contains(cult) ? "E" : "W";
        }

        private static bool IsWeaponType(ItemObject.ItemTypeEnum t)
        {
            return t == ItemObject.ItemTypeEnum.OneHandedWeapon || t == ItemObject.ItemTypeEnum.TwoHandedWeapon
                || t == ItemObject.ItemTypeEnum.Polearm || t == ItemObject.ItemTypeEnum.Bow || t == ItemObject.ItemTypeEnum.Crossbow
                || t == ItemObject.ItemTypeEnum.Sling || t == ItemObject.ItemTypeEnum.Thrown
                || t == ItemObject.ItemTypeEnum.Pistol || t == ItemObject.ItemTypeEnum.Musket;
        }

        private static bool IsAmmoType(ItemObject.ItemTypeEnum t)
        {
            return t == ItemObject.ItemTypeEnum.Arrows || t == ItemObject.ItemTypeEnum.Bolts
                || t == ItemObject.ItemTypeEnum.SlingStones || t == ItemObject.ItemTypeEnum.Bullets;
        }

        private static bool IsArmorType(ItemObject.ItemTypeEnum t)
        {
            return t == ItemObject.ItemTypeEnum.HeadArmor || t == ItemObject.ItemTypeEnum.BodyArmor
                || t == ItemObject.ItemTypeEnum.LegArmor || t == ItemObject.ItemTypeEnum.HandArmor
                || t == ItemObject.ItemTypeEnum.Cape || t == ItemObject.ItemTypeEnum.ChestArmor;
        }

        /// <summary>Kategoria do logu: 0 bron, 1 tarcza, 2 amunicja, 3 pancerz, -1 poza zakresem.</summary>
        private static int Cat(ItemObject it)
        {
            var t = it.ItemType;
            if (IsWeaponType(t)) return 0;
            if (t == ItemObject.ItemTypeEnum.Shield) return 1;
            if (IsAmmoType(t)) return 2;
            if (IsArmorType(t)) return 3;
            return -1;
        }

        /// <summary>Rodzaj przedmiotu do zamiany: bron i tarcza - typ + klasa broni; reszta - typ.</summary>
        private static string WKey(ItemObject it)
        {
            var t = it.ItemType;
            if (IsWeaponType(t) || t == ItemObject.ItemTypeEnum.Shield)
            {
                var wc = it.PrimaryWeapon != null ? it.PrimaryWeapon.WeaponClass : WeaponClass.Undefined;
                return (int)t + "/" + (int)wc;
            }
            return ((int)t).ToString();
        }

        private static bool IsBanditGear(ItemObject it)
        {
            var id = it.StringId ?? "";
            return id.StartsWith("bandit_", StringComparison.Ordinal) || id.StartsWith("tacky_bandit_", StringComparison.Ordinal)
                || id.StartsWith("looter", StringComparison.Ordinal);
        }

        private static bool HasUsage(ItemObject it, string part)
        {
            try
            {
                if (it == null || it.Weapons == null) return false;
                foreach (var w in it.Weapons)
                    if (w != null && w.WeaponDescriptionId != null && w.WeaponDescriptionId.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            catch { }
            return false;
        }

        /// <summary>Unikaty, klingi lore, sprzet umarlych, smoki - nigdy w puli zamiennikow.</summary>
        private static bool Forbidden(ItemObject it)
        {
            var id = it.StringId ?? "";
            return Mends.IsUniqueGear(it) || Mends.IsLoreBlade(it) || Mends.IsDeadGear(it)
                || id.StartsWith("dragon_", StringComparison.Ordinal) || Mends.IsDragonMount(it);
        }

        private sealed class Unit
        {
            public CharacterObject Co; public int T; public string Cult; public bool Mounted, Female, Bandit, ArmorOk;
            public string Traits { get { return (Mounted ? "M" : "-") + (Female ? "F" : "-") + (Bandit ? "B" : "-"); } }
        }

        private static Unit MakeUnit(CharacterObject co, bool armorOk)
        {
            return new Unit
            {
                Co = co, T = UnitTier(co), Cult = co.Culture != null ? (co.Culture.StringId ?? "") : "",
                Mounted = co.IsMounted, Female = co.IsFemale, Bandit = co.Occupation == Occupation.Bandit, ArmorOk = armorOk
            };
        }

        private sealed class Pick { public ItemObject Item; public int Kind; }   // Kind: 1 scisly, 2 luzny, 3 zapas innej klasy

        private struct Cand
        {
            public ItemObject Item; public int G; public bool Strict; public int Level; public bool Worn; public bool SameItemCult; public int Diff;
        }

        private static int CmpCand(Cand a, Cand b)
        {
            if (a.Strict != b.Strict) return a.Strict ? -1 : 1;
            if (a.Level != b.Level) return a.Level.CompareTo(b.Level);
            if (a.G != b.G) return b.G.CompareTo(a.G);
            if (a.Worn != b.Worn) return a.Worn ? -1 : 1;
            if (a.SameItemCult != b.SameItemCult) return a.SameItemCult ? -1 : 1;
            if (a.Diff != b.Diff) return a.Diff.CompareTo(b.Diff);
            return string.CompareOrdinal(a.Item.StringId, b.Item.StringId);
        }

        private static bool SameTroopCult(ItemObject c, string unitCult)
        {
            if (unitCult.Length > 0 && CultOf(c) == unitCult) return true;
            HashSet<string> w;
            return unitCult.Length > 0 && _worn != null && _worn.TryGetValue(c, out w) && w.Contains(unitCult);
        }

        /// <summary>Kaskada: 1 kultura jednostki tier T; 2 kultura jednostki T-1; 3 ta sama strona Waskiego
        /// Morza albo bez kultury, tier T; 4 dowolna, tier T; 5 kultura jednostki nizej; 6 reszta.</summary>
        private static int Level(ItemObject c, int g, int cap, Unit u, bool sameTroop)
        {
            if (sameTroop && g == cap) return 1;
            if (sameTroop && g == cap - 1) return 2;
            string sc = Side(CultOf(c));
            if ((sc == "" || sc == Side(u.Cult)) && g == cap) return 3;
            if (g == cap) return 4;
            if (sameTroop) return 5;
            return 6;
        }

        /// <summary>Twarde warunki (dane XML ich nie znaja): 0 ok, 1 siodlo, 2 couch/brace, 3 plec, 4 bandyta, 5 cywilne.</summary>
        private static int Reject(ItemObject orig, ItemObject c, Unit u, bool usage)
        {
            if (IsBanditGear(c) && !u.Bandit) return 4;
            if (c.HasArmorComponent && c.IsCivilian && (orig == null || !orig.IsCivilian)) return 5;
            var fl = c.ItemFlags;
            if (u.Female && (fl & ItemFlags.NotUsableByFemale) != 0) return 3;
            if (!u.Female && (fl & ItemFlags.NotUsableByMale) != 0) return 3;
            if (u.Mounted && orig != null && Mends.MountOk(orig) && !Mends.MountOk(c)) return 1;
            if (usage && orig != null)
            {
                if (HasUsage(orig, "couch") && !HasUsage(c, "couch")) return 2;
                if (HasUsage(orig, "bracing") && !HasUsage(c, "bracing")) return 2;
            }
            return 0;
        }

        private static void CountReject(int r)
        {
            if (r == 1) _rej1++; else if (r == 2) _rej2++; else if (r == 3) _rej3++; else if (r == 4) _rej4++; else if (r == 5) _rej5++;
        }

        /// <summary>Scisle: ta sama dlugosc (+-max(15 cm, 20%)), luk dlugi za dlugi, kusza lekka za lekka.</summary>
        private static bool Strict(ItemObject o, ItemObject c)
        {
            if (IsArmorType(o.ItemType)) return true;
            var po = o.PrimaryWeapon; var pc = c.PrimaryWeapon;
            int lo = po != null ? po.WeaponLength : 0, lc = pc != null ? pc.WeaponLength : 0;
            if (lo > 0 && lc > 0 && Math.Abs(lo - lc) > Math.Max(15f, 0.2f * lo)) return false;
            if (o.ItemType == ItemObject.ItemTypeEnum.Bow || o.ItemType == ItemObject.ItemTypeEnum.Crossbow)
            {
                string uo = po != null ? (po.ItemUsage ?? "") : "", uc = pc != null ? (pc.ItemUsage ?? "") : "";
                if (uo.Contains("long") != uc.Contains("long") || uo.Contains("light") != uc.Contains("light")) return false;
            }
            return true;
        }

        private static bool PeasantOk(ItemObject c, ItemObject orig, int cap)
        {
            var id = c.StringId ?? "";
            if (!id.StartsWith("peasant_", StringComparison.Ordinal)) return true;
            return cap <= 1 || (orig != null && (orig.StringId ?? "").StartsWith("peasant_", StringComparison.Ordinal));
        }

        private static bool SiegeClass(ItemObject it)
        {
            var wc = it.PrimaryWeapon != null ? it.PrimaryWeapon.WeaponClass : WeaponClass.Undefined;
            return wc == WeaponClass.Boulder || wc == WeaponClass.BallistaBoulder || wc == WeaponClass.BallistaStone;
        }

        private static Cand MakeCand(ItemObject c, int g, ItemObject orig, int cap, Unit u, bool strict)
        {
            bool st = SameTroopCult(c, u.Cult);
            string oc = orig != null ? CultOf(orig) : "";
            int diff = 0; try { diff = c.Difficulty; } catch { }
            return new Cand
            {
                Item = c, G = g, Strict = strict, Level = Level(c, g, cap, u, st),
                Worn = _worn != null && _worn.ContainsKey(c), SameItemCult = oc.Length > 0 && CultOf(c) == oc, Diff = diff
            };
        }

        /// <summary>Zamiennik dla przedmiotu ponad sufit - jeden wybor na klucz (przedmiot, sufit, kultura,
        /// konny, plec, bandyta). Bron bez kandydata tej klasy - zapas tej samej umiejetnosci (1.4).</summary>
        private static Pick Choose(ItemObject orig, int cap, Unit u)
        {
            string key = orig.StringId + "|" + cap + "|" + u.Cult + "|" + u.Traits;
            Pick p;
            if (_choice.TryGetValue(key, out p)) return p;
            p = new Pick();
            bool hasBest = false; Cand best = default(Cand);
            List<ItemObject> lst;
            if (_pool.TryGetValue(WKey(orig), out lst))
            {
                foreach (var c in lst)
                {
                    if (c == orig) continue;
                    int g = TierOf(c);
                    if (g > cap || !PeasantOk(c, orig, cap)) continue;
                    int r = Reject(orig, c, u, true);
                    if (r != 0) { CountReject(r); continue; }
                    var cd = MakeCand(c, g, orig, cap, u, Strict(orig, c));
                    if (!hasBest || CmpCand(cd, best) < 0) { best = cd; hasBest = true; }
                }
            }
            if (hasBest) { p.Item = best.Item; p.Kind = best.Strict ? 1 : 2; }
            else if (IsWeaponType(orig.ItemType))
            {
                // ZAPAS (1.4): ta sama umiejetnosc i ten sam typ (miecz rekruta -> topor/buzdygan 1H,
                // oszczep -> topor do rzucania albo kamien); proca -> kamien do rzucania (Thrown/Stone)
                bool sling = orig.ItemType == ItemObject.ItemTypeEnum.Sling;
                SkillObject sk = null; try { sk = orig.RelevantSkill; } catch { }
                foreach (var kv in _pool)
                {
                    foreach (var c in kv.Value)
                    {
                        if (c == orig || SiegeClass(c)) continue;
                        if (sling)
                        {
                            if (c.ItemType != ItemObject.ItemTypeEnum.Thrown || c.PrimaryWeapon == null || c.PrimaryWeapon.WeaponClass != WeaponClass.Stone) continue;
                        }
                        else
                        {
                            if (c.ItemType != orig.ItemType) continue;
                            SkillObject cs = null; try { cs = c.RelevantSkill; } catch { }
                            if (sk == null || cs != sk) continue;
                        }
                        int g = TierOf(c);
                        if (g > cap || !PeasantOk(c, orig, cap)) continue;
                        if (Reject(orig, c, u, false) != 0) continue;
                        var cd = MakeCand(c, g, orig, cap, u, false);
                        if (!hasBest || CmpCand(cd, best) < 0) { best = cd; hasBest = true; }
                    }
                }
                if (hasBest) { p.Item = best.Item; p.Kind = 3; }
            }
            _choice[key] = p;
            return p;
        }

        /// <summary>Pula, tiery i "kto co nosi" - raz na przebieg (drugi przebieg po rozsadku liczy od nowa).</summary>
        private static void PreparePass()
        {
            _tierCache = new Dictionary<ItemObject, int>();
            _choice = new Dictionary<string, Pick>(StringComparer.Ordinal);
            _worn = new Dictionary<ItemObject, HashSet<string>>();
            foreach (var co in MBObjectManager.Instance.GetObjectTypeList<CharacterObject>())
            {
                try
                {
                    if (co == null || co.IsHero || co.IsTemplate || !IsSoldierOcc(co.Occupation)) continue;
                    string cu = co.Culture != null ? (co.Culture.StringId ?? "") : "";
                    foreach (var eq in co.BattleEquipments)
                    {
                        if (eq == null) continue;
                        for (int s = 0; s < 12; s++)
                        {
                            var it = eq[s].Item;
                            if (it == null) continue;
                            HashSet<string> h;
                            if (!_worn.TryGetValue(it, out h)) _worn[it] = h = new HashSet<string>(StringComparer.Ordinal);
                            if (cu.Length > 0) h.Add(cu);
                        }
                    }
                }
                catch { }
            }
            _pool = new Dictionary<string, List<ItemObject>>(StringComparer.Ordinal);
            foreach (var it in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
            {
                try
                {
                    if (it == null || string.IsNullOrEmpty(it.StringId) || Cat(it) < 0) continue;
                    if (Forbidden(it)) continue;
                    if (it.NotMerchandise && !_worn.ContainsKey(it)) continue;
                    string k = WKey(it);
                    List<ItemObject> l;
                    if (!_pool.TryGetValue(k, out l)) _pool[k] = l = new List<ItemObject>();
                    l.Add(it);
                }
                catch { }
            }
            // kolejnosc listy nie wplywa na wynik (ranking konczy sie na StringId), ale sortujemy dla powtarzalnosci
            foreach (var l in _pool.Values) l.Sort((a, b) => string.CompareOrdinal(a.StringId, b.StringId));
        }

        /// <summary>Podmiana zestawow jednostki: perSet dostaje oryginalny zestaw bojowy i zwraca klon ze
        /// zmianami albo null. Gdy cokolwiek sie zmienia - NOWY roster (plytka kopia listy), stary nietkniety.</summary>
        private static bool Rewrite(CharacterObject co, Func<Equipment, Equipment> perSet, ref int setsSeen)
        {
            if (FRoster == null || FEquipments == null) return false;
            var ro = FRoster.GetValue(co) as MBEquipmentRoster;
            if (ro == null) return false;
            var list = FEquipments.GetValue(ro) as MBList<Equipment>;
            if (list == null) return false;
            MBList<Equipment> nl = null;
            for (int i = 0; i < list.Count; i++)
            {
                var eq = list[i];
                if (eq == null || !eq.IsBattle) continue;
                setsSeen++;
                var ne = perSet(eq);
                if (ne == null) continue;
                if (nl == null) nl = new MBList<Equipment>(list);
                nl[i] = ne;
            }
            if (nl == null) return false;
            InstallRoster(co, ro, nl);
            return true;
        }

        private static void InstallRoster(CharacterObject co, MBEquipmentRoster old, MBList<Equipment> nl)
        {
            var nr = new MBEquipmentRoster();
            try { nr.StringId = old.StringId; } catch { }
            FEquipments.SetValue(nr, nl);
            try
            {
                if (MSetRosterCulture != null) MSetRosterCulture.Invoke(nr, new object[] { old.EquipmentCulture });
                if (MSetRosterCategories != null) MSetRosterCategories.Invoke(nr, new object[] { old.EquipmentCategories });
            }
            catch { }
            try { nr.OrderEquipments(); } catch { }
            FRoster.SetValue(co, nr);
        }

        private static IEnumerable<Equipment> RawBattleSets(CharacterObject co)
        {
            var ro = FRoster != null ? FRoster.GetValue(co) as MBEquipmentRoster : null;
            var list = ro != null && FEquipments != null ? FEquipments.GetValue(ro) as MBList<Equipment> : null;
            if (list == null) yield break;
            foreach (var eq in list) if (eq != null && eq.IsBattle) yield return eq;
        }

        /// <summary>Czy slot i przedmiot sa w zakresie decyzji 8b: bron/tarcza/amunicja w 0-3, pancerz w 5-9.</summary>
        private static bool InScope(int s, ItemObject it, Unit u)
        {
            if (s == 4 || s > 9) return false;
            int c = Cat(it);
            if (s <= 3) return c >= 0 && c <= 2;
            return u.ArmorOk && c == 3;
        }

        // ---------------- [a2] migawka valyrianska ----------------

        /// <summary>Bron (sloty 0-3, bez tarcz) kazdej jednostki zolnierskiej SPRZED zamiany. ValyrianWardSim
        /// czyta ja zamiast wzorca na zywo, zeby 175 nie zmienialo walki z Innymi (1.7 [a2], 1.9).</summary>
        private static void ValyrianSnapshot()
        {
            _preWeapons = new Dictionary<CharacterObject, ItemObject[]>();
            int t6 = 0;
            foreach (var co in MBObjectManager.Instance.GetObjectTypeList<CharacterObject>())
            {
                try
                {
                    if (co == null || co.IsHero || co.IsTemplate || !IsSoldierOcc(co.Occupation)) continue;
                    var l = new List<ItemObject>();
                    foreach (var eq in co.BattleEquipments)
                    {
                        if (eq == null) continue;
                        for (int s = 0; s <= 3; s++)
                        {
                            var it = eq[s].Item;
                            if (it == null || it.WeaponComponent == null) continue;
                            var pw = it.WeaponComponent.PrimaryWeapon;
                            if (pw != null && pw.IsShield) continue;
                            if (!l.Contains(it)) l.Add(it);
                        }
                    }
                    _preWeapons[co] = l.ToArray();
                    if (Mends.BestWeaponTier(co) >= 6) t6++;
                }
                catch { }
            }
            _valyrianT6 = t6;
            Scribe.Line("Mends: sprzet wedlug tieru (175) - migawka valyrianska: " + t6 + " jednostek z bronia t6 we wzorcu sprzed zamiany (zasada Innych w autobitwie bez zmian).");
        }

        /// <summary>Najwyzszy tier broni we wzorcu SPRZED zamiany (tier liczony na zywo, jak dotad).
        /// Jednostki spoza migawki (bohaterowie, nowe postacie) - wzorzec na zywo jak dotad.</summary>
        internal static int PreTierBest(CharacterObject c)
        {
            try
            {
                ItemObject[] l;
                if (c != null && _preWeapons != null && _preWeapons.TryGetValue(c, out l))
                {
                    int best = 0;
                    for (int i = 0; i < l.Length; i++)
                    {
                        int t = (int)l[i].Tier + 1;
                        if (t > best) best = t;
                    }
                    return best;
                }
            }
            catch { }
            return Mends.BestWeaponTier(c);
        }

        // ---------------- [b] sprzet wedlug tieru ----------------

        internal static void TierGear(string when)
        {
            try
            {
                if (!Wanted("Army175TierGear", 1f))
                {
                    Scribe.Line("Mends: sprzet wedlug tieru (175) - wylaczone (Army175TierGear), wzorce ROT bez zmian.");
                    return;
                }
                PreparePass();
                _replaced = new List<KeyValuePair<ItemObject, int>>();
                _noRepl = new HashSet<string>(StringComparer.Ordinal);
                _rej1 = _rej2 = _rej3 = _rej4 = _rej5 = 0;
                int soldiers = 0, units = 0, sets = 0, templates = 0, giants = 0, wights = 0, heroes = 0, stumbles = 0;
                int sWeap = 0, sShield = 0, sAmmo = 0, sArmor = 0, strict = 0, loose = 0, fb = 0, fbSling = 0, fbJav = 0, fbSword = 0;
                int noRepl = 0, stones = 0, rosters = 0;
                var jumps = new List<KeyValuePair<int, string>>();
                var jumpSeen = new HashSet<string>(StringComparer.Ordinal);

                foreach (var co in MBObjectManager.Instance.GetObjectTypeList<CharacterObject>())
                {
                    if (co == null) continue;
                    try
                    {
                        if (co.IsHero) { heroes++; continue; }
                        if (!IsSoldierOcc(co.Occupation)) continue;
                        if (co.IsTemplate || (co.StringId ?? "").StartsWith("tournament_template", StringComparison.Ordinal)) { templates++; continue; }
                        soldiers++;
                        string race = RaceName(co);
                        if (race == "giant") { giants++; continue; }
                        bool armorOk = race != "wight";
                        if (!armorOk) wights++;
                        var u = MakeUnit(co, armorOk);
                        bool any = Rewrite(co, eq =>
                        {
                            Equipment clone = null; ItemObject stoneForSling = null;
                            for (int s = 0; s <= 9; s++)
                            {
                                var it = eq[s].Item;
                                if (it == null || !InScope(s, it, u)) continue;
                                int g = TierOf(it);
                                if (g <= u.T) continue;
                                var p = Choose(it, u.T, u);
                                if (p.Item == null)
                                {
                                    noRepl++;
                                    _noRepl.Add(it.StringId + "|" + u.T);
                                    continue;
                                }
                                if (clone == null) clone = new Equipment(eq);
                                clone[s] = new EquipmentElement(p.Item);
                                _replaced.Add(new KeyValuePair<ItemObject, int>(it, u.T));
                                int c = Cat(it);
                                if (c == 0) sWeap++; else if (c == 1) sShield++; else if (c == 2) sAmmo++; else sArmor++;
                                if (p.Kind == 1) strict++; else if (p.Kind == 2) loose++;
                                else
                                {
                                    fb++;
                                    if (it.ItemType == ItemObject.ItemTypeEnum.Sling) fbSling++;
                                    else if (it.PrimaryWeapon != null && it.PrimaryWeapon.WeaponClass == WeaponClass.Javelin) fbJav++;
                                    else if (it.ItemType == ItemObject.ItemTypeEnum.OneHandedWeapon) fbSword++;
                                }
                                if (it.ItemType == ItemObject.ItemTypeEnum.Sling && p.Item.ItemType == ItemObject.ItemTypeEnum.Thrown) stoneForSling = p.Item;
                                int gr = TierOf(p.Item);
                                string jk = it.StringId + ">" + p.Item.StringId;
                                if (jumpSeen.Add(jk))
                                    jumps.Add(new KeyValuePair<int, string>(g - gr, it.StringId + " t" + g + " -> " + p.Item.StringId + " t" + gr + " (" + co.StringId + " t" + u.T + ")"));
                            }
                            // bez procy kamienie procy sa martwe - drugi stos kamieni do rzucania (1.4)
                            if (clone != null && stoneForSling != null)
                            {
                                bool slingLeft = false;
                                for (int s = 0; s <= 3; s++) { var x = clone[s].Item; if (x != null && x.ItemType == ItemObject.ItemTypeEnum.Sling) slingLeft = true; }
                                if (!slingLeft)
                                    for (int s = 0; s <= 3; s++)
                                    {
                                        var x = clone[s].Item;
                                        if (x != null && x.ItemType == ItemObject.ItemTypeEnum.SlingStones) { clone[s] = new EquipmentElement(stoneForSling); stones++; }
                                    }
                            }
                            return clone;
                        }, ref sets);
                        if (any) { units++; rosters++; }
                    }
                    catch { stumbles++; }
                }
                TierGearApplied = sets > 0;
                if (sets == 0)
                {
                    Scribe.Line("Mends: sprzet wedlug tieru (175) - zestawy jednostek jeszcze puste (" + when + "), powtorze pozniej.");
                    return;
                }
                Scribe.Line("Mends: sprzet wedlug tieru (175, " + when + ") - jednostek " + units + " z " + soldiers + " zolnierskich, slotow "
                            + (sWeap + sShield + sAmmo + sArmor) + " (bron " + sWeap + ", tarcze " + sShield + ", amunicja " + sAmmo + ", pancerz " + sArmor
                            + "); zamiennik scisly " + strict + ", luzny " + loose + ", zapas innej klasy " + fb + " (proce " + fbSling + ", oszczepy " + fbJav
                            + ", miecze rekrutow " + fbSword + "); kamienie procy za kamienie do rzucania " + stones + "; bez zamiennika (zostaje) " + noRepl
                            + "; nowe rostery " + rosters + "; odrzuceni kandydaci: siodlo " + _rej1 + ", couch/brace " + _rej2 + ", plec " + _rej3
                            + ", bandyta " + _rej4 + ", cywilne " + _rej5 + "; kultur Essos " + Essos.Count + "; migawka valyrianska "
                            + _valyrianT6 + " (z bronia t6); pominiete: szablony " + templates + ", giganci " + giants
                            + ", umarli-pancerz " + wights + ", bohaterowie " + heroes + " (zmienionych 0); zestawow bojowych " + sets + "; potkniecia " + stumbles + ".");
                if (jumps.Count > 0)
                {
                    jumps.Sort((a, b) => b.Key != a.Key ? b.Key.CompareTo(a.Key) : string.CompareOrdinal(a.Value, b.Value));
                    var sb = new System.Text.StringBuilder("Mends: sprzet wedlug tieru (175) - najwieksze skoki: ");
                    int show = Math.Min(15, jumps.Count);
                    for (int i = 0; i < show; i++) { if (i > 0) sb.Append(", "); sb.Append(jumps[i].Value); }
                    Scribe.Line(sb.ToString() + ".");
                }
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "Army175.TierGear", null); } catch { } }
        }

        /// <summary>Liczy sloty ponad tier (tier na zywo) w zakresie decyzji 8b; pomija przedmioty bez zamiennika.</summary>
        private static int CountOver(out string sample)
        {
            sample = "";
            int over = 0;
            foreach (var co in MBObjectManager.Instance.GetObjectTypeList<CharacterObject>())
            {
                try
                {
                    if (co == null || co.IsHero || co.IsTemplate || !IsSoldierOcc(co.Occupation)) continue;
                    if ((co.StringId ?? "").StartsWith("tournament_template", StringComparison.Ordinal)) continue;
                    string race = RaceName(co);
                    if (race == "giant") continue;
                    var u = MakeUnit(co, race != "wight");
                    foreach (var eq in RawBattleSets(co))
                        for (int s = 0; s <= 9; s++)
                        {
                            var it = eq[s].Item;
                            if (it == null || !InScope(s, it, u) || TierOf(it) <= u.T) continue;
                            if (_noRepl != null && _noRepl.Contains(it.StringId + "|" + u.T)) continue;
                            over++;
                            if (sample.Length < 300) sample += (sample.Length > 0 ? ", " : "") + co.StringId + ":" + it.StringId + " t" + TierOf(it);
                        }
                }
                catch { }
            }
            return over;
        }

        /// <summary>OnSessionLaunched po ArmorSanity/AmmoSanity: tier po rozsadku moze tylko spasc, wiec ma byc
        /// 0 slotow ponad tier. Gdy nie - drugi (idempotentny) przebieg i OSTRZEZENIE.</summary>
        internal static void TierGearCheck()
        {
            try
            {
                if (!Wanted("Army175TierGear", 1f)) return;
                if (!TierGearApplied)
                {
                    Scribe.Line("Mends: sprzet wedlug tieru (175) - przy wczytaniu zestawy byly puste; przebieg teraz (po rozsadku).");
                    TierGear("start sesji (zapas)");
                    LoreArmor();
                    GoldenBows();
                    return;
                }
                _tierCache = new Dictionary<ItemObject, int>();   // tiery po rozsadku
                int q = 0;
                if (_replaced != null)
                    foreach (var kv in _replaced) if (TierOf(kv.Key) <= kv.Value) q++;
                string sample;
                int over = CountOver(out sample);
                if (over > 0)
                {
                    Scribe.Line("Mends: sprzet wedlug tieru (175) - OSTRZEZENIE: po rozsadku " + over + " slotow ponad tier (" + sample + ") - drugi przebieg.");
                    TierGear("drugi przebieg po rozsadku");
                    _tierCache = new Dictionary<ItemObject, int>();
                    over = CountOver(out sample);
                }
                Scribe.Line("Mends: sprzet wedlug tieru (175) - kontrola po rozsadku: slotow ponad tier " + over
                            + (over > 0 ? " (" + sample + ")" : "") + " (zamienionych na zapas, choc po rozsadku by sie miescily: " + q
                            + "; przedmiotow bez zamiennika, ktore zostaja: " + (_noRepl != null ? _noRepl.Count : 0) + ").");
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "Army175.TierGearCheck", null); } catch { } }
        }

        // ---------------- [c] lore w granicy tieru (1.6) ----------------

        // Swiadome wyjatki lore (projekt 1.6) - selekcja po id, nie po regule ogolnej.
        private static readonly string[] QarthBody = { "qartheen_hoplite" };
        private static readonly string[] DorneBody = { "aserai_mameluke_axeman", "aserai_mameluke_regular", "aserai_mameluke_guard" };
        private static readonly string[] QohorHeavy = { "qohorik_goat_warrior", "qohorik_elite_spearman", "qohorik_falxman", "qohorik_goat_devout" };

        private static int ArmorSum(ItemObject it)
        {
            try { var a = it.ArmorComponent; return a == null ? 0 : a.HeadArmor + a.BodyArmor + a.LegArmor + a.ArmArmor; } catch { return 0; }
        }

        /// <summary>Drugi przebieg z sufitem T-1 dla wybranych slotow (ta sama kaskada i reguly "scisle").</summary>
        private static int LighterPass(CharacterObject co, bool allArmor, ref int sets)
        {
            var u = MakeUnit(co, true);
            int cap = u.T - 1;
            if (cap < 1) return 0;
            int changed = 0;
            Rewrite(co, eq =>
            {
                Equipment clone = null;
                for (int s = 5; s <= 9; s++)
                {
                    var it = eq[s].Item;
                    if (it == null || Cat(it) != 3) continue;
                    if (!allArmor && it.ItemType != ItemObject.ItemTypeEnum.BodyArmor) continue;
                    if (TierOf(it) <= cap) continue;
                    var p = Choose(it, cap, u);
                    if (p.Item == null) continue;
                    if (clone == null) clone = new Equipment(eq);
                    clone[s] = new EquipmentElement(p.Item);
                    changed++;
                }
                return clone;
            }, ref sets);
            return changed;
        }

        /// <summary>Qohor (miasto platnerzy): najciezszy korpus W TIERZE jednostki - kaskada tylko poziomy
        /// z tierem T (1, 3, 4), w najlepszym niepustym poziomie najwiekszy pancerz; tylko gdy ciezszy niz dzis.</summary>
        private static int HeavierPass(CharacterObject co, ref int sets)
        {
            var u = MakeUnit(co, true);
            int T = u.T, changed = 0;
            List<ItemObject> lst;
            if (!_pool.TryGetValue(((int)ItemObject.ItemTypeEnum.BodyArmor).ToString(), out lst)) return 0;
            Rewrite(co, eq =>
            {
                var it = eq[6].Item;   // korpus
                if (it == null || it.ItemType != ItemObject.ItemTypeEnum.BodyArmor) return null;
                ItemObject best = null; int bestLevel = 99, bestSum = -1; float bestW = -1f;
                foreach (var c in lst)
                {
                    if (c == it || TierOf(c) != T) continue;
                    if (Reject(it, c, u, false) != 0) continue;
                    int lv = Level(c, T, T, u, SameTroopCult(c, u.Cult));
                    int sum = ArmorSum(c); float w = c.Weight;
                    bool better = lv < bestLevel
                        || (lv == bestLevel && (sum > bestSum || (sum == bestSum && (w > bestW || (w == bestW && best != null && string.CompareOrdinal(c.StringId, best.StringId) < 0)))));
                    if (best == null || better) { best = c; bestLevel = lv; bestSum = sum; bestW = w; }
                }
                if (best == null || ArmorSum(best) <= ArmorSum(it)) return null;
                var clone = new Equipment(eq);
                clone[6] = new EquipmentElement(best);
                changed++;
                return clone;
            }, ref sets);
            return changed;
        }

        internal static void LoreArmor()
        {
            try
            {
                bool main = Wanted("Army175LoreArmor", 1f), extra = Wanted("Army175LoreArmorExtra", 0f);
                if (!main && !extra) { Scribe.Line("Mends: sprzet wedlug tieru (175) - lore w granicy tieru wylaczone (Army175LoreArmor, Army175LoreArmorExtra)."); return; }
                if (_pool == null || _choice == null) PreparePass();
                int pentos = 0, qarth = 0, dorne = 0, qohor = 0, sets = 0, stumbles = 0;
                if (main)
                {
                    foreach (var co in MBObjectManager.Instance.GetObjectTypeList<CharacterObject>())
                    {
                        try
                        {
                            if (co == null || co.IsHero || co.IsTemplate || co.Occupation != Occupation.Soldier) continue;
                            if (co.Culture == null || co.Culture.StringId != "pentoshi") continue;
                            int t = UnitTier(co);
                            if (t < 2 || t > 4) continue;
                            pentos += LighterPass(co, true, ref sets);
                        }
                        catch { stumbles++; }
                    }
                    foreach (var id in QarthBody)
                    {
                        try { var co = MBObjectManager.Instance.GetObject<CharacterObject>(id); if (co != null) qarth += LighterPass(co, false, ref sets); else Scribe.Line("Mends: sprzet wedlug tieru (175) - lore: brak jednostki " + id + "."); }
                        catch { stumbles++; }
                    }
                }
                if (extra)
                {
                    foreach (var id in DorneBody)
                    {
                        try { var co = MBObjectManager.Instance.GetObject<CharacterObject>(id); if (co != null) dorne += LighterPass(co, false, ref sets); else Scribe.Line("Mends: sprzet wedlug tieru (175) - lore: brak jednostki " + id + "."); }
                        catch { stumbles++; }
                    }
                    foreach (var id in QohorHeavy)
                    {
                        try { var co = MBObjectManager.Instance.GetObject<CharacterObject>(id); if (co != null) qohor += HeavierPass(co, ref sets); else Scribe.Line("Mends: sprzet wedlug tieru (175) - lore: brak jednostki " + id + "."); }
                        catch { stumbles++; }
                    }
                }
                Scribe.Line("Mends: sprzet wedlug tieru (175) - lore w granicy tieru: Pentos " + (main ? pentos.ToString() : "wyl.") + ", Qarth " + (main ? qarth.ToString() : "wyl.")
                            + ", Dorne " + (extra ? dorne.ToString() : "wyl.") + ", Qohor " + (extra ? qohor.ToString() : "wyl.") + " slotow; potkniecia " + stumbles + ".");
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "Army175.LoreArmor", null); } catch { } }
        }

        // ---------------- [d] Zlota Kompania - 1/3 kusz, 1/3 lukow dwukrzywych, 1/3 cisowych (2.5, dom. WYL.) ----------------

        private static readonly string[] GoldenXbow = { "golden_crossbowman", "golden_veteran_crossbowman", "golden_master_crossbowman" };

        /// <summary>Najlepszy przedmiot danego typu w tierze &lt;= sufit (ta sama kaskada; bez oryginalu).</summary>
        private static ItemObject BestOfType(ItemObject.ItemTypeEnum type, Func<ItemObject, bool> ok, int cap, Unit u)
        {
            List<ItemObject> all = new List<ItemObject>();
            foreach (var kv in _pool) foreach (var c in kv.Value) if (c.ItemType == type) all.Add(c);
            bool has = false; Cand best = default(Cand);
            foreach (var c in all)
            {
                int g = TierOf(c);
                if (g > cap || !PeasantOk(c, null, cap) || !ok(c)) continue;
                if (Reject(null, c, u, false) != 0) continue;
                var cd = MakeCand(c, g, null, cap, u, true);
                if (!has || CmpCand(cd, best) < 0) { best = cd; has = true; }
            }
            return has ? best.Item : null;
        }

        private static bool IsLongBow(ItemObject c)
        {
            var u = c.PrimaryWeapon != null ? (c.PrimaryWeapon.ItemUsage ?? "") : "";
            return u.Contains("long");
        }

        internal static void GoldenBows()
        {
            try
            {
                if (!Wanted("Army175GoldenBows", 0f)) return;   // domyslnie WYL. (czeka na koszyki K1 i zgode na wyjatek od zasady 28.08)
                if (_pool == null || _choice == null) PreparePass();
                var done = new List<string>();
                foreach (var id in GoldenXbow)
                {
                    try
                    {
                        var co = MBObjectManager.Instance.GetObject<CharacterObject>(id);
                        if (co == null) { done.Add(id + " brak"); continue; }
                        var ro = FRoster.GetValue(co) as MBEquipmentRoster;
                        var list = ro != null ? FEquipments.GetValue(ro) as MBList<Equipment> : null;
                        if (list == null) { done.Add(id + " bez rostera"); continue; }
                        Equipment first = null; bool hasBow = false;
                        foreach (var eq in list)
                        {
                            if (eq == null || !eq.IsBattle) continue;
                            if (first == null) first = eq;
                            for (int s = 0; s <= 3; s++) { var x = eq[s].Item; if (x != null && x.ItemType == ItemObject.ItemTypeEnum.Bow) hasBow = true; }
                        }
                        if (first == null) { done.Add(id + " bez zestawu"); continue; }
                        if (hasBow) { done.Add(id + " juz z lukami"); continue; }   // idempotentnie
                        var u = MakeUnit(co, true);
                        var recurve = BestOfType(ItemObject.ItemTypeEnum.Bow, c => !IsLongBow(c), u.T, u);
                        var yew = BestOfType(ItemObject.ItemTypeEnum.Bow, IsLongBow, u.T, u);
                        var arrows = BestOfType(ItemObject.ItemTypeEnum.Arrows, c => true, u.T, u);
                        if (recurve == null || yew == null || arrows == null) { done.Add(id + " bez luku/strzal w tierze"); continue; }
                        Func<ItemObject, Equipment> make = bow =>
                        {
                            var e = new Equipment(first);
                            int ammo = 0, empty = -1;
                            for (int s = 0; s <= 3; s++)
                            {
                                var x = e[s].Item;
                                if (x == null) { if (empty < 0) empty = s; continue; }
                                if (x.ItemType == ItemObject.ItemTypeEnum.Crossbow) e[s] = new EquipmentElement(bow);
                                else if (x.ItemType == ItemObject.ItemTypeEnum.Bolts) { e[s] = new EquipmentElement(arrows); ammo++; }
                            }
                            if (ammo < 2 && empty >= 0) e[empty] = new EquipmentElement(arrows);   // "2 strzaly"
                            return e;
                        };
                        var nl = new MBList<Equipment>();
                        nl.Add(first); nl.Add(make(recurve)); nl.Add(make(yew));
                        foreach (var eq in list) if (eq != null && !eq.IsBattle) nl.Add(eq);
                        InstallRoster(co, ro, nl);
                        done.Add(id + " t" + u.T + ": " + recurve.StringId + " t" + TierOf(recurve) + ", " + yew.StringId + " t" + TierOf(yew) + ", " + arrows.StringId + " t" + TierOf(arrows));
                    }
                    catch (Exception e) { done.Add(id + " potkniecie " + e.GetType().Name); }
                }
                Scribe.Line("Mends: Zlota Kompania (175.G) - 3 zestawy (kusza / luk dwukrzywy / luk cisowy): " + string.Join("; ", done.ToArray()) + ".");
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "Army175.GoldenBows", null); } catch { } }
        }
    }
}
