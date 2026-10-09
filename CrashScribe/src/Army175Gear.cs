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
    /// Poprawki po recenzji (09.10): decyzja o 175.2 raz, przy wczytaniu (TierGearWanted - wylacznik i STRAZ ZAPISU
    /// bez K1); ranking "scisly przed kaskada" z wymogiem SKUTECZNYM (jak po prawach); migawka valyrianska tylko dla
    /// jednostek z zestawami (i przed kazda zmiana); postacie spoza wczytania (BK CustomTroop) poza zakresem;
    /// Qohor ciezej w sesji po rozsadku; lore i Zlota Kompania tylko razem z 175.2.
    /// 175b (decyzje Jeffa 09.10 ok. 05:50): najprostszy oszczep gry ma tier 2 (SimpleJavelins, przed TierGear) - oszczepnicy
    /// t2-t3 dostaja oszczep zamiast toporka; Dorne lzej i Qohor ciezej (Army175LoreArmorExtra) domyslnie WLACZONE.
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
        private static HashSet<string> _noRepl;                             // klucze wyboru (ChoiceKey) bez zadnego zamiennika
        private static Dictionary<CharacterObject, ItemObject[]> _preWeapons;  // migawka valyrianska (bron sprzed zamiany)
        private static int _rej1, _rej2, _rej3, _rej4, _rej5;               // odrzuceni kandydaci wedlug powodu
        private static int _valyrianT6;                                     // jednostek z bronia t6 we wzorcu sprzed zamiany
        private static string[] _races;

        // poprawki po recenzji 09.10
        private static int _tgWanted = -1;              // decyzja o 175.2 z chwili wczytania: -1 jeszcze nie, 0 nie, 1 tak (zmiana w MCM w trakcie sesji nic nie robi)
        private static string _tgOffWhy = "";
        private static bool? _isSaved;                  // z AfterRegisterSubModuleObjects(isSavedCampaign)
        private static bool _gaveUp;                    // brak pol refleksji albo 3 doby bez zestawow - koniec prob w tej sesji
        private static int _catchUpTries;
        private static bool _snapshotOk;                // migawka valyrianska widziala zestawy bojowe
        private static HashSet<CharacterObject> _atLoad;   // postacie istniejace przy AfterRegister; pozniejsze (BK CustomTroop) poza 175.2
        private static HashSet<MBEquipmentRoster> _newRosters;
        private static int _rostersInstalled;
        private static bool _qohorDone;
        private static float _kgPerAth = 0.333f, _armStep = 0f, _wpnStep = 35f;   // parametry praw (wymog skuteczny jak po prawach)

        // W2 (09.10): tier broni przy wczytaniu (to, co widzi TierGear) - porownanie z tierem przy starcie sesji (to, co widza
        // WeaponTierLaw i SkillSinew); zamiany bez warunku couch/brace (suma przebiegow sesji)
        private static Dictionary<ItemObject, int> _regTier;
        private static int _w2Slots, _w2Mounted;

        /// <summary>Stan sesji - wolane z SubModuleMain.OnGameStart (przed AfterRegister).</summary>
        internal static void Reset()
        {
            TierGearApplied = false;
            _tierCache = null; _pool = null; _worn = null; _choice = null; _replaced = null; _noRepl = null;
            _preWeapons = null; _valyrianT6 = 0;
            _rej1 = _rej2 = _rej3 = _rej4 = _rej5 = 0;
            _tgWanted = -1; _tgOffWhy = ""; _isSaved = null; _gaveUp = false; _catchUpTries = 0; _snapshotOk = false;
            _atLoad = null; _newRosters = null; _rostersInstalled = 0; _qohorDone = false; _javT2 = false;
            _regTier = null; _w2Slots = 0; _w2Mounted = 0;
            NorthDone = false; DothrakiDone = false;
            CompositionApplied = false; DothrakiPoolActive = false; _poolFirstLogged = false;
            ResetPoolCounters();
            ResetMeasure();
        }

        /// <summary>AfterRegisterSubModuleObjects (CS): sklad, migawka valyrianska, sprzet wedlug tieru,
        /// lore w granicy tieru, Zlota Kompania. Kazdy krok w osobnym try.</summary>
        internal static void OnObjectsRegistered(bool isSavedCampaign)
        {
            if (Campaign.Current == null) return;
            _isSaved = isSavedCampaign;
            try
            {
                _atLoad = new HashSet<CharacterObject>();
                foreach (var co in MBObjectManager.Instance.GetObjectTypeList<CharacterObject>()) if (co != null) _atLoad.Add(co);
            }
            catch { _atLoad = null; }
            try { Composition(); } catch (Exception e) { try { Scribe.Report("CrashScribe", e, "Army175.Composition", null); } catch { } }
            try { ValyrianSnapshot(); } catch (Exception e) { try { Scribe.Report("CrashScribe", e, "Army175.ValyrianSnapshot", null); } catch { } }
            // 175b: tier oszczepu PRZED pula 175.2 (TierGear), prawami tieru i SkillSinew; migawka wyzej trzyma przedmioty,
            // nie tiery, a oszczepy nie sa t6 - zasada Innych (PreTierBest >= 6) bez zmian
            try { SimpleJavelins(); } catch (Exception e) { try { Scribe.Report("CrashScribe", e, "Army175.SimpleJavelins", null); } catch { } }
            try { SnapRegTiers(); } catch { _regTier = null; }   // W2: tiery broni, ktore widzi TierGear (po oszczepie t2)
            try { TierGear("przy wczytaniu, przed sesja"); } catch (Exception e) { try { Scribe.Report("CrashScribe", e, "Army175.TierGear", null); } catch { } }
            try { LoreArmor(); } catch (Exception e) { try { Scribe.Report("CrashScribe", e, "Army175.LoreArmor", null); } catch { } }
            try { GoldenBows(); } catch (Exception e) { try { Scribe.Report("CrashScribe", e, "Army175.GoldenBows", null); } catch { } }
        }

        private static bool IsSavedCampaign()
        {
            if (_isSaved.HasValue) return _isSaved.Value;
            try { return Campaign.Current != null && Campaign.Current.CampaignGameLoadingType == Campaign.GameLoadingType.SavedCampaign; }
            catch { return false; }
        }

        /// <summary>Czy Armoury ma K1 (dozbrajanie): typ Armoury.SwapMath albo pole Settings.MenUpgradeOneTierUp.</summary>
        internal static bool K1Present()
        {
            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (asm.GetName().Name != "Armoury") continue;
                    if (asm.GetType("Armoury.SwapMath") != null) return true;
                    var ts = asm.GetType("Armoury.Settings");
                    return ts != null && ts.GetField("MenUpgradeOneTierUp") != null;
                }
            }
            catch { }
            return false;
        }

        /// <summary>Decyzja o 175.2 raz na sesje, w pierwszej chwili (AfterRegister): wylacznik Army175TierGear
        /// i STRAZ ZAPISU (projekt 1.9, rozdz. 0 pkt 2): na zapisie gry bez K1 zolnierze straciliby podbite
        /// umiejetnosci przy zbrojowniach pelnych t5-t6 (SkillLawWard - slot PUSTY, AiGear nie dokupi t2-t4) -
        /// wtedy 175.2 pominiete (nowa kampania bez zmian). Pozniejsza zmiana w MCM nic nie robi do nastepnego wczytania.</summary>
        internal static bool TierGearWanted()
        {
            if (_tgWanted < 0)
            {
                bool on = Wanted("Army175TierGear", 1f);
                string why = on ? "" : "wylacznik Army175TierGear";
                if (on && IsSavedCampaign() && !K1Present())
                {
                    on = false;
                    why = "zapis gry bez K1";
                    Scribe.Line("Mends: sprzet wedlug tieru (175) - OSTRZEZENIE: zapis gry, a Armoury bez K1 (dozbrajanie: brak Armoury.SwapMath i MenUpgradeOneTierUp) - "
                                + "175.2 POMINIETE w tej sesji (wzorce ROT, umiejetnosci i NorthHardy bez zmian; sklad 175.3 i DothrakiRiders dzialaja). "
                                + "Powod: zolnierze na zapisie straciliby podbite umiejetnosci, a zbrojownie sa pelne t5-t6, ktorych nie udzwigna (projekt 1.9). "
                                + "Wgrac 175 razem z K1 albo po nim; nowa kampania bez tej strazy.");
                }
                _tgWanted = on ? 1 : 0;
                _tgOffWhy = why;
            }
            return _tgWanted == 1;
        }

        /// <summary>DailyTick (zabezpieczenie jak SkillSinew): gdy przy wczytaniu zestawy byly puste.
        /// Najwyzej 3 doby; decyzja o wlaczeniu z chwili wczytania (TierGearWanted).</summary>
        internal static void DailyCatchUp()
        {
            try
            {
                if (TierGearApplied || _gaveUp || !TierGearWanted()) return;
                _catchUpTries++;
                if (!_snapshotOk) ValyrianSnapshot();
                TierGear("doba " + _catchUpTries + " (zapas)");
                if (TierGearApplied)
                {
                    LoreArmor();
                    GoldenBows();
                    QohorHeavier();
                    _tierCache = new Dictionary<ItemObject, int>();
                    string s0; int o0 = CountOver(out s0);
                    ControlLine("doba " + _catchUpTries + " (zapas)", o0, s0, -1, -1);
                }
                else if (!_gaveUp && _catchUpTries >= 3)
                {
                    _gaveUp = true;
                    // 175b: oszczep t2 ustawiony przy wczytaniu zostaje (prawa tieru juz go wziely; cofanie w polowie sesji = drugi stan) - mowimy to wprost
                    Scribe.Line("Mends: sprzet wedlug tieru (175) - OSTRZEZENIE: 3 doby bez zestawow bojowych jednostek - koniec prob w tej sesji (wzorce ROT bez zmian, NorthHardy pominiete"
                                + (_javT2 ? "; najprostszy oszczep (175b, Pine Javelin) zostal t2 - jednostki z nim we wzorcu ROT maja wymog Rzutu 35, nie 105" : "") + ").");
                }
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

        // Kind: 1 scisly, 2 luzny, 3 zapas innej klasy; PairKey - para (przedmiot, sufit, kultura) jak w sprzet.py; Gap - sufit minus tier zamiennika;
        // NoUsage (W2) - zamiennik z drugiego przebiegu, bez warunku couch/brace (w tierze nie bylo kopii/piki z tym uzyciem)
        private sealed class Pick { public ItemObject Item; public int Kind; public string PairKey; public int Gap; public bool NoUsage; }

        private struct Cand
        {
            public ItemObject Item; public int G; public bool Strict; public int Level; public bool Worn; public bool SameItemCult; public int Diff;
        }

        /// <summary>Klucz wyboru: (przedmiot, sufit, kultura jednostki, konny, plec, bandyta) - ten sam w Choose,
        /// w liczniku "bez zamiennika" i w CountOver.</summary>
        private static string ChoiceKey(ItemObject orig, int cap, Unit u)
        {
            return orig.StringId + "|" + cap + "|" + u.Cult + "|" + u.Traits;
        }

        /// <summary>Ranking: SCISLY (ten sam rodzaj z dlugoscia/naciagiem - "ten sam rodzaj przedmiotu" z decyzji 8b)
        /// przed kaskada kultura/tier, potem poziom kaskady, wyzszy tier, noszony, kultura oryginalu, nizszy wymog
        /// SKUTECZNY (jak po prawach - EffDiff), StringId. Ta sama kolejnosc co sprzet.py (zrodlo liczb projektu).</summary>
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

        /// <summary>Twarde warunki (dane XML ich nie znaja): 0 ok, 1 siodlo, 2 couch/brace, 3 plec, 4 bandyta, 5 cywilne.
        /// W2: couch/brace (usage) Choose zdejmuje w drugim przebiegu, gdy w tierze nie ma kandydata z tym uzyciem.</summary>
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

        private static bool IsArmorLawType(ItemObject.ItemTypeEnum t)
        {
            return t == ItemObject.ItemTypeEnum.HeadArmor || t == ItemObject.ItemTypeEnum.BodyArmor || t == ItemObject.ItemTypeEnum.LegArmor
                || t == ItemObject.ItemTypeEnum.HandArmor || t == ItemObject.ItemTypeEnum.Cape;
        }

        /// <summary>Wymog SKUTECZNY, jak po prawach z OnSessionLaunched (WeightLaw, ArmorTierLaw, WeaponTierLaw - te same
        /// wzory i suwaki; tylko w gore). AfterRegister biegnie przed prawami, a XML ROT ma wymog 0 u 46% pancerzy -
        /// bez tego remis "nizszy wymog" rozstrzygalby StringId zamiast wagi. Tier przedmiotu sprzed rozsadku (jak cala zamiana).</summary>
        private static int EffDiff(ItemObject c)
        {
            int d = 0;
            try { d = c.Difficulty; } catch { }
            try
            {
                var ty = c.ItemType;
                int g = TierOf(c); if (g < 1) g = 1;
                if (IsArmorLawType(ty))
                {
                    int w = (int)Math.Round(c.Weight / _kgPerAth);
                    d = w;   // 09.10: wymog pancerza = waga (WeightLaw ustawia dokladnie, takze w dol)
                    if (_armStep > 0f && c.HasArmorComponent) { int t = (int)Math.Round((g - 1) * _armStep); if (t > d) d = t; }
                }
                else if (_wpnStep > 0f && c.HasWeaponComponent && ty != ItemObject.ItemTypeEnum.Banner
                         && ty != ItemObject.ItemTypeEnum.Horse && ty != ItemObject.ItemTypeEnum.HorseHarness)
                {
                    int t = (int)Math.Round((g - 1) * _wpnStep);
                    if (Mends.ExactTierWeapon(ty)) d = t;   // 09.10: bron biala, tarcze, rzucana - wymog dokladnie z tieru (jak WeaponTierLaw)
                    else if (t > d) d = t;
                }
            }
            catch { }
            return d;
        }

        /// <summary>Parametry praw jak w Mends.WeightLaw/ArmorTierLaw/WeaponTierLaw (te same klucze, granice i zapasy).</summary>
        private static void ReadLawParams()
        {
            float kg = Mends.ArmouryFloat("ArmourKgPerAthletics", 0.333f);
            if (kg < 0.05f || kg > 2f) kg = 0.333f;
            _kgPerAth = kg;
            float a = Mends.ArmouryFloat("ArmourTierAthletics", 0f);
            _armStep = a < 0.5f ? 0f : (a > 100f ? 100f : a);
            float w = Mends.ArmouryFloat("WeaponSkillPerTier", 35f);
            _wpnStep = w < 0.5f ? 0f : (w > 100f ? 100f : w);
        }

        private static Cand MakeCand(ItemObject c, int g, ItemObject orig, int cap, Unit u, bool strict)
        {
            bool st = SameTroopCult(c, u.Cult);
            string oc = orig != null ? CultOf(orig) : "";
            return new Cand
            {
                Item = c, G = g, Strict = strict, Level = Level(c, g, cap, u, st),
                Worn = _worn != null && _worn.ContainsKey(c), SameItemCult = oc.Length > 0 && CultOf(c) == oc, Diff = EffDiff(c)
            };
        }

        /// <summary>W2: przedmiot z uzyciem couch (kopia) albo bracing (pika) - twardy warunek 2 w Reject.</summary>
        private static bool UsageBound(ItemObject orig)
        {
            return orig != null && (HasUsage(orig, "couch") || HasUsage(orig, "bracing"));
        }

        /// <summary>Zamiennik dla przedmiotu ponad sufit - jeden wybor na klucz (przedmiot, sufit, kultura,
        /// konny, plec, bandyta). Bron bez kandydata tej klasy - zapas tej samej umiejetnosci (1.4).
        /// W2 (09.10, zasada Jeffa "kazda jednostka ma sprzet najwyzej swojego tieru"): gdy przedmiot z couch/bracing
        /// nie ma w tierze ZADNEGO kandydata z tym samym uzyciem (w grze z RBM kazda kopia ma tier 5-6), drugi przebieg
        /// bez warunku couch/brace (reszta twardych warunkow bez zmian) - dotad taki slot zostawal ponad tier
        /// ("bez zamiennika"), prawo tieru broni dawalo wymog 140-175, a SkillSinew pompowal zolnierzom t2-t4 umiejetnosc.</summary>
        private static Pick Choose(ItemObject orig, int cap, Unit u)
        {
            string key = ChoiceKey(orig, cap, u);
            Pick p;
            if (_choice.TryGetValue(key, out p)) return p;
            p = new Pick();
            p.PairKey = orig.StringId + "|" + cap + "|" + u.Cult;
            for (int pass = 0; pass < 2 && p.Item == null; pass++)
            {
                bool usage = pass == 0;
                if (!usage && !UsageBound(orig)) break;   // W2: drugi przebieg tylko dla couch/bracing
                bool hasBest = false; Cand best = default(Cand);
                List<ItemObject> lst;
                if (_pool.TryGetValue(WKey(orig), out lst))
                {
                    foreach (var c in lst)
                    {
                        if (c == orig) continue;
                        int g = TierOf(c);
                        if (g > cap || !PeasantOk(c, orig, cap)) continue;
                        int r = Reject(orig, c, u, usage);
                        if (r != 0) { if (usage) CountReject(r); continue; }   // odrzuceni liczeni raz (pierwszy przebieg)
                        var cd = MakeCand(c, g, orig, cap, u, Strict(orig, c));
                        if (!hasBest || CmpCand(cd, best) < 0) { best = cd; hasBest = true; }
                    }
                }
                if (hasBest) { p.Item = best.Item; p.Kind = best.Strict ? 1 : 2; p.NoUsage = !usage; }
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
                            // twarde warunki takze w zapasie (couch/brace jak w 1.3); W2: w drugim przebiegu bez couch/brace
                            int r = Reject(orig, c, u, usage);
                            if (r != 0) { if (usage) CountReject(r); continue; }
                            var cd = MakeCand(c, g, orig, cap, u, false);
                            if (!hasBest || CmpCand(cd, best) < 0) { best = cd; hasBest = true; }
                        }
                    }
                    if (hasBest) { p.Item = best.Item; p.Kind = 3; p.NoUsage = !usage; }
                }
            }
            if (p.Item != null) p.Gap = cap - TierOf(p.Item);
            _choice[key] = p;
            return p;
        }

        /// <summary>Pula, tiery i "kto co nosi" - raz na przebieg (drugi przebieg po rozsadku liczy od nowa).</summary>
        private static void PreparePass()
        {
            _tierCache = new Dictionary<ItemObject, int>();
            _choice = new Dictionary<string, Pick>(StringComparer.Ordinal);
            _worn = new Dictionary<ItemObject, HashSet<string>>();
            try { ReadLawParams(); } catch { }
            foreach (var co in MBObjectManager.Instance.GetObjectTypeList<CharacterObject>())
            {
                try
                {
                    if (co == null || co.IsHero || co.IsTemplate || !IsSoldierOcc(co.Occupation)) continue;
                    if (Late(co)) continue;
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
                    if (it.IsCraftedByPlayer) continue;   // wykute w grze (zapis) - wzorce nie moga zalezec od zapisu
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
            _rostersInstalled++;
            if (_newRosters == null) _newRosters = new HashSet<MBEquipmentRoster>();
            _newRosters.Add(nr);
        }

        /// <summary>Postac zarejestrowana PO AfterRegister (np. BK CustomTroop - oddzial gracza z kariery najemnika,
        /// rejestrowany w OnGameLoaded, sprzet projektuje gracz) - poza 175.2. Bez migawki (AfterRegister nie biegl) - nikt.</summary>
        private static bool Late(CharacterObject co)
        {
            return _atLoad != null && !_atLoad.Contains(co);
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

        /// <summary>Bron (sloty 0-3, bez tarcz) kazdej jednostki zolnierskiej SPRZED zamiany. Do 175c ValyrianWardSim
        /// czytal ja zamiast wzorca na zywo, zeby 175 nie zmienialo walki z Innymi (1.7 [a2], 1.9); od 175d (decyzja Jeffa 09.10
        /// ok. 08:40 pkt 1) autobitwa liczy wzorzec po zamianie, a migawka sluzy juz tylko porownaniu w logu ("dawniej t6").</summary>
        private static void ValyrianSnapshot()
        {
            if (_preWeapons == null) _preWeapons = new Dictionary<CharacterObject, ItemObject[]>();
            int t6 = 0, sets = 0, units = 0;
            foreach (var co in MBObjectManager.Instance.GetObjectTypeList<CharacterObject>())
            {
                try
                {
                    if (!SnapUnit(co, ref sets)) continue;
                    units++;
                    if (Mends.BestWeaponTier(co) >= 6) t6++;
                }
                catch { }
            }
            _snapshotOk = sets > 0;
            _valyrianT6 = t6;
            Scribe.Line("Mends: sprzet wedlug tieru (175) - migawka valyrianska: " + t6 + " jednostek z bronia t6 we wzorcu sprzed zamiany (z " + units
                        + " jednostek z zestawami bojowymi; od 175d autobitwa z Innymi liczy wzorzec PO zamianie, migawka tylko do porownania; "
                        + OthersSteel.T6Text(false) + ")"
                        + (sets == 0 ? " - zestawy jeszcze puste, migawka powtorzona przed zamiana" : "") + ".");
        }

        /// <summary>Wpis migawki dla jednej jednostki zolnierskiej - TYLKO gdy ma zestaw bojowy i jeszcze go nie ma
        /// (pusty wpis dawalby PreTierBest 0 i ciecie 15% kazdego ciosu w Innych; brak wpisu = wzorzec na zywo).
        /// TierGear wola to przed kazda zmiana, wiec kazda zmieniana jednostka ma bron sprzed zamiany.</summary>
        private static bool SnapUnit(CharacterObject co, ref int sets)
        {
            if (co == null || co.IsHero || co.IsTemplate || !IsSoldierOcc(co.Occupation)) return false;
            if (_preWeapons == null) _preWeapons = new Dictionary<CharacterObject, ItemObject[]>();
            if (_preWeapons.ContainsKey(co)) return false;
            var l = new List<ItemObject>();
            int n = 0;
            foreach (var eq in co.BattleEquipments)
            {
                if (eq == null) continue;
                n++;
                for (int s = 0; s <= 3; s++)
                {
                    var it = eq[s].Item;
                    if (it == null || it.WeaponComponent == null) continue;
                    var pw = it.WeaponComponent.PrimaryWeapon;
                    if (pw != null && pw.IsShield) continue;
                    if (!l.Contains(it)) l.Add(it);
                }
            }
            if (n == 0) return false;
            sets += n;
            _preWeapons[co] = l.ToArray();
            return true;
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

        /// <summary>Bron z migawki (sloty 0-3 bez tarcz) sprzed zamiany. 175d: OthersSteel liczy z niej juz tylko porownanie w logu
        /// ("dawniej t6 - wzorzec sprzed 175", linie T6Text) - klasa ciosu w autobitwie idzie z wzorca po zamianie (decyzja Jeffa 09.10
        /// ok. 08:40 pkt 1). null = brak wpisu (bohater, nowa postac).</summary>
        internal static ItemObject[] PreWeapons(CharacterObject c)
        {
            try
            {
                ItemObject[] l;
                if (c != null && _preWeapons != null && _preWeapons.TryGetValue(c, out l)) return l;
            }
            catch { }
            return null;
        }

        // ---------------- [b0] najprostszy oszczep tier 2 (175b) ----------------

        /// <summary>175b (decyzja Jeffa 09.10 ok. 05:50 pkt 3): "najprostsze oszczepy dostaja tier 2" - Dornijczycy t2-t3 i inni
        /// oszczepnicy t2-t3 maja oszczepy, nie toporki. Przelicznik tieru RBM (obrazenia x szybkosc) stawia KAZDY oszczep gry
        /// na t4-t5, wiec 175.2 nie mialo dla t2-t3 zadnego oszczepu i bralo zapas 1.4 (topor do rzucania, raz kamien).
        /// Wybor (rachunek SCR\a175b, CHANGELOG 175b): JEDEN oszczep - northern_javelin_1_t2 "Pine Javelin": najtanszy oszczep gry,
        /// wszystkie czesci kuzni tieru 2 (grot "Javelin Head" z kutego zelaza Iron2, drzewce sosnowe), id gry "_t2". Wystarcza, zeby
        /// wszystkie 24 rodzaje t2-t3 z oszczepem we wzorcu mialy oszczep (6 wlasny, 18 zamiennik), a zaden rodzaj nie traci
        /// umiejetnosci; lzejsze "darty" (western/eastern_javelin_1_t2) dawalyby 4-8 rodzajom t4 spadek Rzutu o 5-15.
        /// Zmienia sie TIER PRZEDMIOTU (TierfOverride), jego cena gry (DetermineValue) i kategoria towaru (ItemCategory z selektora
        /// gry wedlug nowego tieru: ranged_weapons_4 -> ranged_weapons_2), NIE umiejetnosci. Tak dziala atrybut XML tier_override
        /// na zwyklym Item (Deserialize: TierfOverride, potem DetermineValue, na koncu DetermineItemCategoryForItem z tieru);
        /// CraftedItem (a Pine Javelin nim jest) tego atrybutu nie czyta, a DetermineItemCategoryForItem ustawia kategorie tylko,
        /// gdy jest null - dlatego kategorie ustawiamy wprost (poprawka po recenzji 175b: bez tego warsztaty robily go w linii
        /// ranged_weapons_4, a indeks ceny i popyt kategorii liczyly go jak t4). Przy wczytaniu, przed TierGear (pula 175.2 widzi
        /// t2), przed prawami tieru (WeaponTierLaw da wymog 35 zamiast 105), przed SkillSinew i przed zbudowaniem list kategorii
        /// warsztatow gry (WorkshopsCampaignBehavior.FillItemsInAllCategories - nowa gra i OnGameLoaded). Tylko razem z 175.2
        /// (TierGearWanted - zapis bez K1 zostaje bez zmian - i pola refleksji jak w TierGear). Obiekty przedmiotow sa nowe w kazdej
        /// grze - wylacznik dziala od nastepnego wczytania. Kultura przedmiotu (sturgia = Zelazne Wyspy) bez zmian: WorkshopLaw robi go
        /// z pierwszenstwem w miastach tej kultury, a AiGear kupuje koszykiem typ x tier - CHANGELOG 175b "Kto dostaje".</summary>
        private static readonly string[] SimpleJavelinIds = { "northern_javelin_1_t2" };
        private const int SimpleJavelinTier = 2;
        private static readonly System.Reflection.MethodInfo MSetTierfOverride = AccessTools.PropertySetter(typeof(ItemObject), "TierfOverride");
        private static readonly System.Reflection.MethodInfo MDetermineValue = AccessTools.Method(typeof(ItemObject), "DetermineValue");
        private static readonly System.Reflection.MethodInfo MSetItemCategory = AccessTools.PropertySetter(typeof(ItemObject), "ItemCategory");
        private static bool _javT2;                     // 175b: w tej sesji co najmniej jeden oszczep dostal tier 2 (do linii "3 doby bez zestawow")

        internal static void SimpleJavelins()
        {
            try
            {
                if (!Wanted("Army175SimpleJavelins", 1f)) { Scribe.Line("Mends: najprostszy oszczep tier 2 (175b) - wylaczone (Army175SimpleJavelins), oszczepy z tierem gry."); return; }
                if (!TierGearWanted()) { Scribe.Line("Mends: najprostszy oszczep tier 2 (175b) - pominiete (" + _tgOffWhy + "; dziala tylko razem z 175.2)."); return; }
                // ten sam warunek co TierGear: bez pol rosterow 175.2 nie zadziala, a oszczep t2 bez niego obnizylby wymog Rzutu
                // 6 rodzajom t2-t3 z wlasnym Pine Javelin we wzorcu ROT (SkillSinew nie podnioslby Rzutu do 105, np. sea_hounds 105 -> 40,
                // sturgian_brigand 105 -> 70) - wzorce i umiejetnosci maja wtedy zostac bez zmian
                if (FRoster == null || FEquipments == null)
                {
                    Scribe.Line("Mends: najprostszy oszczep tier 2 (175b) - pominiete (brak pol refleksji rosterow - 175.2 nie zadziala w tej sesji), oszczepy z tierem gry.");
                    return;
                }
                if (MSetTierfOverride == null)
                {
                    Scribe.Line("Mends: najprostszy oszczep tier 2 (175b) - OSTRZEZENIE: brak ItemObject.TierfOverride (inna wersja gry) - oszczepy z tierem gry.");
                    return;
                }
                bool valueModel = false;
                try { valueModel = Game.Current != null && Game.Current.BasicModels != null && Game.Current.BasicModels.ItemValueModel != null; } catch { }
                ItemCategorySelector catSel = null;
                try { catSel = Game.Current != null && Game.Current.BasicModels != null ? Game.Current.BasicModels.ItemCategorySelector : null; } catch { }
                var parts = new List<string>();
                foreach (var id in SimpleJavelinIds)
                {
                    try
                    {
                        var it = MBObjectManager.Instance.GetObject<ItemObject>(id);
                        if (it == null) { parts.Add(id + " brak w grze"); continue; }
                        if (it.ItemType != ItemObject.ItemTypeEnum.Thrown || it.PrimaryWeapon == null || it.PrimaryWeapon.WeaponClass != WeaponClass.Javelin)
                        {
                            parts.Add(id + " nie jest oszczepem (" + it.ItemType + ") - pominiety");
                            continue;
                        }
                        int g0 = (int)it.Tier + 1, v0 = it.Value;
                        // Tierf = TierfOverride - 1 = 2 -> ItemTiers.Tier2 (PULAPKA CLAUDE.md 7: Tier1 == 0, wyswietlany tier = (int)Tier + 1)
                        MSetTierfOverride.Invoke(it, new object[] { SimpleJavelinTier + 1f });
                        _javT2 = true;
                        string val;
                        if (MDetermineValue == null || !valueModel) val = ", cena gry bez zmian (brak modelu wartosci)";
                        else
                        {
                            try { MDetermineValue.Invoke(it, null); val = ", cena gry " + v0 + " -> " + it.Value; }
                            catch (Exception e) { val = ", cena gry bez zmian (" + e.GetType().Name + ")"; }
                        }
                        // kategoria towaru za tierem (jak XML tier_override na zwyklym Item): selektor gry liczy ja z it.Tier (juz t2)
                        string cat;
                        string c0 = it.ItemCategory != null ? it.ItemCategory.StringId : "brak";
                        if (MSetItemCategory == null || catSel == null) cat = ", kategoria bez zmian " + c0 + " (brak selektora kategorii)";
                        else
                        {
                            try
                            {
                                var nc = catSel.GetItemCategoryForItem(it);
                                if (nc == null) cat = ", kategoria bez zmian " + c0 + " (selektor bez wyniku)";
                                else if (nc == it.ItemCategory) cat = ", kategoria " + c0 + " (bez zmian)";
                                else { MSetItemCategory.Invoke(it, new object[] { nc }); cat = ", kategoria " + c0 + " -> " + nc.StringId; }
                            }
                            catch (Exception e) { cat = ", kategoria bez zmian " + c0 + " (" + e.GetType().Name + ")"; }
                        }
                        parts.Add(id + " (" + it.Name + ") t" + g0 + " -> t" + ((int)it.Tier + 1) + val + cat);
                    }
                    catch (Exception e) { parts.Add(id + " potkniecie " + e.GetType().Name); }
                }
                _tierCache = null;   // TierGear liczy tiery od nowa (PreparePass) - tu na wszelki wypadek po zmianie tieru
                Scribe.Line("Mends: najprostszy oszczep tier 2 (175b) - " + string.Join("; ", parts.ToArray())
                            + " (wymog Rzutu po prawie tieru 35 zamiast 105; cena historyczna Armoury liczona przy starcie sesji z tieru 2; "
                            + "w linii 175.2 'zapas innej klasy ... oszczepy' ma byc 0; kultura przedmiotu bez zmian - warsztaty z pierwszenstwem kultury miasta, zakupy AI koszykiem typ x tier).");
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "Army175.SimpleJavelins", null); } catch { } }
        }

        // ---------------- [b] sprzet wedlug tieru ----------------

        internal static void TierGear(string when)
        {
            try
            {
                if (!TierGearWanted())
                {
                    Scribe.Line("Mends: sprzet wedlug tieru (175) - pominiete (" + _tgOffWhy + "), wzorce ROT bez zmian.");
                    return;
                }
                if (_gaveUp) return;
                if (FRoster == null || FEquipments == null)
                {
                    // inna wersja gry: bez pol rosterow nie ma czego zmieniac - jedna linia i koniec prob (bez kosztu co dobe)
                    _gaveUp = true;
                    Scribe.Line("Mends: sprzet wedlug tieru (175) - OSTRZEZENIE: brak pol refleksji (BasicCharacterObject._equipmentRoster / MBEquipmentRoster._equipments) - 175.2 nie dziala w tej sesji, wzorce ROT bez zmian.");
                    return;
                }
                PreparePass();
                _replaced = new List<KeyValuePair<ItemObject, int>>();
                _noRepl = new HashSet<string>(StringComparer.Ordinal);
                _rej1 = _rej2 = _rej3 = _rej4 = _rej5 = 0;
                int soldiers = 0, units = 0, sets = 0, templates = 0, giants = 0, wights = 0, heroes = 0, stumbles = 0, late = 0;
                int sWeap = 0, sShield = 0, sAmmo = 0, sArmor = 0, strict = 0, loose = 0, fb = 0, fbSling = 0, fbJav = 0, fbSword = 0;
                int noRepl = 0, stones = 0, stonesOver = 0, occ0 = 0, occ1 = 0, occ2 = 0;
                int noReplWeap = 0, relaxed = 0, relaxedMounted = 0;   // W2: bron bez zamiennika; zamiennik bez couch/brace (i u konnych)
                var relaxedEx = new List<string>();
                var relaxedSeen = new HashSet<string>(StringComparer.Ordinal);
                int rost0 = _rostersInstalled;
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
                        if (Late(co)) { late++; continue; }
                        soldiers++;
                        string race = RaceName(co);
                        if (race == "giant") { giants++; continue; }
                        bool armorOk = race != "wight";
                        if (!armorOk) wights++;
                        var u = MakeUnit(co, armorOk);
                        int snapSets = 0;
                        try { SnapUnit(co, ref snapSets); } catch { }   // bron sprzed zamiany do porownania w logu OthersSteel (gdy migawka jeszcze jej nie ma)
                        bool any = Rewrite(co, eq =>
                        {
                            Equipment clone = null;
                            // PROCA (1.4): proca ponad tier -> kamien do rzucania; gdy po zamianie w zestawie nie zostaje zadna proca,
                            // kamienie procy (kazdego tieru) ida na drugi stos kamieni do rzucania - JEDNA zamiana na slot, liczona raz
                            ItemObject stoneForSling = null;
                            bool slingStays = false;
                            for (int s = 0; s <= 3; s++)
                            {
                                var x = eq[s].Item;
                                if (x == null || x.ItemType != ItemObject.ItemTypeEnum.Sling) continue;
                                if (!InScope(s, x, u) || TierOf(x) <= u.T) { slingStays = true; continue; }
                                var ps = Choose(x, u.T, u);
                                if (ps.Item != null && ps.Item.ItemType == ItemObject.ItemTypeEnum.Thrown) stoneForSling = ps.Item;
                                else slingStays = true;
                            }
                            if (slingStays) stoneForSling = null;
                            for (int s = 0; s <= 9; s++)
                            {
                                var it = eq[s].Item;
                                if (it == null) continue;
                                if (stoneForSling != null && s <= 3 && it.ItemType == ItemObject.ItemTypeEnum.SlingStones)
                                {
                                    if (clone == null) clone = new Equipment(eq);
                                    clone[s] = new EquipmentElement(stoneForSling);
                                    stones++;
                                    if (InScope(s, it, u) && TierOf(it) > u.T)
                                    {
                                        stonesOver++; sAmmo++;
                                        _replaced.Add(new KeyValuePair<ItemObject, int>(it, u.T));
                                    }
                                    continue;
                                }
                                if (!InScope(s, it, u)) continue;
                                int g = TierOf(it);
                                if (g <= u.T) continue;
                                var p = Choose(it, u.T, u);
                                if (p.Item == null)
                                {
                                    noRepl++;
                                    if (Cat(it) == 0) noReplWeap++;
                                    _noRepl.Add(ChoiceKey(it, u.T, u));
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
                                if (p.NoUsage)
                                {
                                    relaxed++;
                                    if (u.Mounted) relaxedMounted++;
                                    if (relaxedEx.Count < 6 && relaxedSeen.Add(it.StringId + ">" + p.Item.StringId))
                                        relaxedEx.Add(it.StringId + " t" + g + " -> " + p.Item.StringId + " t" + TierOf(p.Item) + " (" + co.StringId + " t" + u.T + (u.Mounted ? ", konny" : "") + ")");
                                }
                                if (p.Gap <= 0) occ0++; else if (p.Gap == 1) occ1++; else occ2++;
                                int gr = TierOf(p.Item);
                                string jk = it.StringId + ">" + p.Item.StringId;
                                if (jumpSeen.Add(jk))
                                    jumps.Add(new KeyValuePair<int, string>(g - gr, it.StringId + " t" + g + " -> " + p.Item.StringId + " t" + gr + " (" + co.StringId + " t" + u.T + ")"));
                            }
                            return clone;
                        }, ref sets);
                        if (any) units++;
                    }
                    catch { stumbles++; }
                }
                TierGearApplied = sets > 0;
                if (sets == 0)
                {
                    Scribe.Line("Mends: sprzet wedlug tieru (175) - zestawy jednostek jeszcze puste (" + when + "), powtorze pozniej.");
                    return;
                }
                _w2Slots += relaxed; _w2Mounted += relaxedMounted;   // suma przebiegow sesji (drugi przebieg liczy tylko nowe sloty)
                // pary jak w sprzet.py: (przedmiot, tier jednostki, kultura); roznica tierow - gorszy wariant cech (konny/plec/bandyta)
                var pairGap = new Dictionary<string, int>(StringComparer.Ordinal);
                var pairNone = new HashSet<string>(StringComparer.Ordinal);
                foreach (var pk in _choice.Values)
                {
                    if (pk == null || pk.PairKey == null) continue;
                    if (pk.Item == null) { pairNone.Add(pk.PairKey); continue; }
                    int gg;
                    if (!pairGap.TryGetValue(pk.PairKey, out gg) || pk.Gap > gg) pairGap[pk.PairKey] = pk.Gap;
                }
                int p0 = 0, p1 = 0, p2 = 0;
                foreach (var gv in pairGap.Values) { if (gv <= 0) p0++; else if (gv == 1) p1++; else p2++; }
                pairNone.ExceptWith(pairGap.Keys);
                int rosters = _rostersInstalled - rost0;
                Scribe.Line("Mends: sprzet wedlug tieru (175, " + when + ") - jednostek " + units + " z " + soldiers + " zolnierskich, slotow "
                            + (sWeap + sShield + sAmmo + sArmor) + " (bron " + sWeap + ", tarcze " + sShield + ", amunicja " + sAmmo + ", pancerz " + sArmor
                            + "); zamiennik scisly " + strict + ", luzny " + loose + ", zapas innej klasy " + fb + " (proce " + fbSling + ", oszczepy " + fbJav
                            + ", miecze rekrutow " + fbSword + "); z tego bez warunku couch/brace (W2 - w tierze jednostki nie ma kopii/piki z tym uzyciem) " + relaxed
                            + " slotow (u konnych " + relaxedMounted + ")" + (relaxedEx.Count > 0 ? ", np. " + string.Join(", ", relaxedEx.ToArray()) : "")
                            + "; pary (przedmiot, tier, kultura) " + pairGap.Count + ": zamiennik w tierze jednostki " + p0
                            + ", T-1 " + p1 + ", nizej " + p2 + " (wystapienia " + occ0 + "/" + occ1 + "/" + occ2 + "), par bez zamiennika " + pairNone.Count
                            + "; kamienie procy za kamienie do rzucania " + stones + " (z tego ponad tier " + stonesOver + ", wliczone w amunicje)"
                            + "; bez zamiennika (zostaje) " + noRepl + " slotow (z tego bron " + noReplWeap + "), " + _noRepl.Count + " kluczy"
                            + "; nowe rostery " + rosters + "; odrzuceni kandydaci: siodlo " + _rej1 + ", couch/brace " + _rej2 + ", plec " + _rej3
                            + ", bandyta " + _rej4 + ", cywilne " + _rej5 + "; kultur Essos " + Essos.Count + " (porownanie z Armoury w kontroli); migawka valyrianska "
                            + _valyrianT6 + " (z bronia t6, przed rozsadkiem); pominiete: szablony " + templates + ", giganci " + giants
                            + ", umarli-pancerz " + wights + ", bohaterowie " + heroes + " (nietykani; kontrola rosterow w sesji), postacie spoza wczytania (np. BK CustomTroop) " + late
                            + "; zestawow bojowych " + sets + "; potkniecia " + stumbles + ".");
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

        /// <summary>Liczy sloty ponad tier (tier na zywo) w zakresie decyzji 8b; pomija klucze bez zamiennika
        /// (ten sam klucz co wybor - ChoiceKey) i postacie spoza wczytania.</summary>
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
                    if (Late(co)) continue;
                    string race = RaceName(co);
                    if (race == "giant") continue;
                    var u = MakeUnit(co, race != "wight");
                    foreach (var eq in RawBattleSets(co))
                        for (int s = 0; s <= 9; s++)
                        {
                            var it = eq[s].Item;
                            if (it == null || !InScope(s, it, u) || TierOf(it) <= u.T) continue;
                            if (_noRepl != null && _noRepl.Contains(ChoiceKey(it, u.T, u))) continue;
                            over++;
                            if (sample.Length < 300) sample += (sample.Length > 0 ? ", " : "") + co.StringId + ":" + it.StringId + " t" + TierOf(it);
                        }
                }
                catch { }
            }
            return over;
        }

        /// <summary>OnSessionLaunched po ArmorSanity/AmmoSanity: tier po rozsadku moze tylko spasc, wiec ma byc
        /// 0 slotow ponad tier. Gdy nie - drugi (idempotentny) przebieg i OSTRZEZENIE. Potem Qohor (najciezszy korpus
        /// liczony PO rozsadku) i linia kontroli. Wszystko przed WeightLaw/prawami tieru i SkillSinew.</summary>
        internal static void TierGearCheck()
        {
            try
            {
                if (!TierGearWanted() || _gaveUp) return;
                if (!TierGearApplied)
                {
                    Scribe.Line("Mends: sprzet wedlug tieru (175) - przy wczytaniu zestawy byly puste; przebieg teraz (po rozsadku).");
                    if (!_snapshotOk) ValyrianSnapshot();
                    TierGear("start sesji (zapas)");
                    if (!TierGearApplied) return;
                    LoreArmor();
                    GoldenBows();
                    QohorHeavier();
                    _tierCache = new Dictionary<ItemObject, int>();
                    string s0; int o0 = CountOver(out s0);
                    ControlLine("start sesji (zapas)", o0, s0, -1, -1);
                    return;
                }
                _tierCache = new Dictionary<ItemObject, int>();   // tiery po rozsadku
                int q = 0;
                if (_replaced != null)
                    foreach (var kv in _replaced) if (TierOf(kv.Key) <= kv.Value) q++;
                int noReplFirst = _noRepl != null ? _noRepl.Count : 0;
                string sample;
                int over = CountOver(out sample);
                if (over > 0)
                {
                    Scribe.Line("Mends: sprzet wedlug tieru (175) - OSTRZEZENIE: po rozsadku " + over + " slotow ponad tier (" + sample + ") - drugi przebieg.");
                    TierGear("drugi przebieg po rozsadku");
                    _tierCache = new Dictionary<ItemObject, int>();
                    over = CountOver(out sample);
                }
                QohorHeavier();
                ControlLine("po rozsadku", over, sample, q, noReplFirst);
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "Army175.TierGearCheck", null); } catch { } }
        }

        /// <summary>Linia kontroli (bramki 6.1 pkt 2 i 10): sloty ponad tier, q (z PIERWSZEGO przebiegu), klucze bez
        /// zamiennika, migawka valyrianska liczona po rozsadku (z amunicja, jak stara regula; od 175d tylko porownanie), bohaterowie z nowym rosterem,
        /// rostery, zgodnosc listy Essos z Armoury.</summary>
        private static void ControlLine(string when, int over, string sample, int q, int noReplFirst)
        {
            int heroesNew = 0, vPost = 0;
            var hex = new List<string>();
            foreach (var co in MBObjectManager.Instance.GetObjectTypeList<CharacterObject>())
            {
                try
                {
                    if (co == null) continue;
                    if (co.IsHero)
                    {
                        var r = FRoster != null ? FRoster.GetValue(co) as MBEquipmentRoster : null;
                        if (r != null && _newRosters != null && _newRosters.Contains(r)) { heroesNew++; if (hex.Count < 5) hex.Add(co.StringId); }
                        continue;
                    }
                    if (co.IsTemplate || !IsSoldierOcc(co.Occupation)) continue;
                    if (PreTierBest(co) >= 6) vPost++;
                }
                catch { }
            }
            Scribe.Line("Mends: sprzet wedlug tieru (175) - kontrola (" + when + "): slotow ponad tier " + over
                        + (over > 0 ? " (" + sample + ")" : "")
                        + "; zamienionych na zapas, choc po rozsadku by sie miescily (q, pierwszy przebieg): " + (q >= 0 ? q.ToString() : "n/d")
                        + "; kluczy bez zamiennika: " + (noReplFirst >= 0 ? "pierwszy przebieg " + noReplFirst + ", " : "") + "teraz " + (_noRepl != null ? _noRepl.Count : 0)
                        + "; migawka valyrianska po rozsadku: " + vPost + " jednostek z bronia t6 liczac z amunicja (wzorzec sprzed zamiany - od 175d tylko porownanie, autobitwa liczy wzorzec po zamianie w obu trybach zasady stali Innych; dzis ok. 223; " + OthersSteel.T6Text(true) + ")"
                        + "; bohaterowie z nowym rosterem " + heroesNew + (hex.Count > 0 ? " (np. " + string.Join(", ", hex.ToArray()) + ")" : "")
                        + " (tylko wzorzec po InitializeHeroBasicCharacterOnAfterLoad, nie ich ekwipunek); nowych rosterow w sesji " + _rostersInstalled
                        + "; lista Essos: " + EssosCheck() + ".");
            W2Line(when);
        }

        /// <summary>Tier przedmiotu na zywo (bez pamieci TierOf) - ten sam wzor co Mends.WeaponTierLaw: (int)Tier + 1.
        /// ItemObject.Tierf nie jest zapamietany: kazde czytanie liczy go od nowa modelem gry (ItemValueModel.CalculateTier,
        /// u nas z latkami RBM), chyba ze przedmiot ma TierfOverride.</summary>
        private static int LiveTier(ItemObject it)
        {
            int g;
            try { g = (int)it.Tier + 1; } catch { g = 0; }
            if (g < 0) g = 0;
            if (g > 6) g = 6;
            return g;
        }

        /// <summary>W2: tiery broni (zbior Mends.WeaponTierLaw: komponent broni bez sztandarow, koni i ladr) w chwili,
        /// w ktorej liczy je TierGear przy wczytaniu (po oszczepie t2 z 175b).</summary>
        private static void SnapRegTiers()
        {
            var d = new Dictionary<ItemObject, int>();
            foreach (var it in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
            {
                try
                {
                    if (it == null || !it.HasWeaponComponent) continue;
                    var ty = it.ItemType;
                    if (ty == ItemObject.ItemTypeEnum.Banner || ty == ItemObject.ItemTypeEnum.Horse || ty == ItemObject.ItemTypeEnum.HorseHarness) continue;
                    d[it] = LiveTier(it);
                }
                catch { }
            }
            _regTier = d;
        }

        /// <summary>W2 - kontrola BEZ WYJATKOW (linia kontroli wyzej pomija klucze "bez zamiennika", wiec przy kopiach
        /// pokazywala 0, choc cerwyn_soldier t3 mial kopie t5): (1) jednostki zolnierskie z bronia ponad tier (sloty 0-3,
        /// tier na zywo - ten, ktory za chwile wezmie prawo tieru broni i SkillSinew); ma byc 0; tarcze, amunicja i pancerz
        /// ponad tier bez zamiennika - osobno; (2) ile broni ma inny tier przy wczytaniu (TierGear) i teraz, osobno skladane
        /// (CraftedItem) - sprawdzenie, czy TierGear i prawo tieru broni widza ten sam tier.</summary>
        private static void W2Line(string when)
        {
            try
            {
                int cTot = 0, cDiff = 0, oTot = 0, oDiff = 0, aTot = 0, aDiff = 0;
                var ex = new List<string>();
                if (_regTier != null)
                {
                    foreach (var kv in _regTier)
                    {
                        var it = kv.Key;
                        if (it == null) continue;
                        bool crafted = false; try { crafted = it.IsCraftedWeapon; } catch { }
                        bool ammo = IsAmmoType(it.ItemType);
                        if (crafted) cTot++; else if (ammo) aTot++; else oTot++;
                        int now = LiveTier(it);
                        if (now == kv.Value) continue;
                        if (crafted) cDiff++; else if (ammo) aDiff++; else oDiff++;
                        if (ex.Count < 8) ex.Add(it.StringId + " t" + kv.Value + "->t" + now + (crafted ? " (skladana)" : ""));
                    }
                }
                int units = 0, slotsW = 0, sh = 0, am = 0, ar = 0;
                var sample = new System.Text.StringBuilder();
                foreach (var co in MBObjectManager.Instance.GetObjectTypeList<CharacterObject>())
                {
                    try
                    {
                        if (co == null || co.IsHero || co.IsTemplate || !IsSoldierOcc(co.Occupation)) continue;
                        if ((co.StringId ?? "").StartsWith("tournament_template", StringComparison.Ordinal)) continue;
                        if (Late(co)) continue;
                        string race = RaceName(co);
                        if (race == "giant") continue;
                        var u = MakeUnit(co, race != "wight");
                        bool over = false;
                        foreach (var eq in RawBattleSets(co))
                            for (int s = 0; s <= 9; s++)
                            {
                                var it = eq[s].Item;
                                if (it == null || !InScope(s, it, u)) continue;
                                int g = LiveTier(it);
                                if (g <= u.T) continue;
                                int c = Cat(it);
                                if (c == 0)
                                {
                                    slotsW++;
                                    if (!over && sample.Length < 400)
                                        sample.Append(sample.Length > 0 ? ", " : "").Append(co.StringId).Append(" t").Append(u.T).Append(": ").Append(it.StringId).Append(" t").Append(g);
                                    over = true;
                                }
                                else if (c == 1) sh++;
                                else if (c == 2) am++;
                                else ar++;
                            }
                        if (over) units++;
                    }
                    catch { }
                }
                Scribe.Line("Mends: sprzet wedlug tieru (175, W2) - kontrola bez wyjatkow (" + when + "): jednostek z bronia ponad tier " + units
                            + " (slotow " + slotsW + "; ma byc 0" + (units > 0 ? ": " + sample : "") + "); ponad tier bez zamiennika: tarcze " + sh
                            + ", amunicja " + am + ", pancerz " + ar + " slotow; tier broni przy wczytaniu (TierGear) a teraz (prawo tieru broni, SkillSinew): "
                            + (_regTier == null ? "brak migawki przy wczytaniu"
                               : "skladane inny " + cDiff + " z " + cTot + ", pozostala bron inny " + oDiff + " z " + oTot + ", amunicja inny " + aDiff + " z " + aTot
                                 + (ex.Count > 0 ? " (np. " + string.Join(", ", ex.ToArray()) + ")" : ""))
                            + "; zamiany bez warunku couch/brace w sesji " + _w2Slots + " slotow (u konnych " + _w2Mounted + ").");
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "Army175.W2Line", null); } catch { } }
        }

        /// <summary>Kopia listy Essos wobec Armoury MountLaw.Essos (pole prywatne, refleksja).</summary>
        private static string EssosCheck()
        {
            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (asm.GetName().Name != "Armoury") continue;
                    var t = asm.GetType("Armoury.MountLaw");
                    var f = t != null ? AccessTools.Field(t, "Essos") : null;
                    var src = f != null ? f.GetValue(null) as IEnumerable<string> : null;
                    if (src == null) return "brak pola Armoury MountLaw.Essos - nie porownano";
                    var other = new HashSet<string>(src, StringComparer.Ordinal);
                    if (other.SetEquals(Essos)) return "zgodna z Armoury MountLaw.Essos (" + Essos.Count + ")";
                    var onlyCs = new List<string>(); foreach (var x in Essos) if (!other.Contains(x)) onlyCs.Add(x);
                    var onlyArm = new List<string>(); foreach (var x in other) if (!Essos.Contains(x)) onlyArm.Add(x);
                    return "OSTRZEZENIE: ROZNA od Armoury MountLaw.Essos (tylko CS: " + string.Join(", ", onlyCs.ToArray())
                           + "; tylko Armoury: " + string.Join(", ", onlyArm.ToArray()) + ") - poprawic kopie w Army175Gear";
                }
            }
            catch { }
            return "brak Armoury - nie porownano";
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
        /// z tierem T (1, 3, 4), w najlepszym niepustym poziomie najwiekszy pancerz; tylko gdy ciezszy niz dzis.
        /// Wolane z QohorHeavier w sesji (po ArmorSanity) - ochrona i tiery juz po rozsadku.</summary>
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
                // 175b: Dorne lzej i Qohor ciezej domyslnie WLACZONE (Jeff 09.10 ok. 05:50: "wlacz" - propozycje audytu 4.9 i 4.11)
                bool main = Wanted("Army175LoreArmor", 1f), extra = Wanted("Army175LoreArmorExtra", 1f);
                if (!main && !extra) { Scribe.Line("Mends: sprzet wedlug tieru (175) - lore w granicy tieru wylaczone (Army175LoreArmor, Army175LoreArmorExtra)."); return; }
                // lore to DRUGI przebieg po zamianie 175.2 (1.6) - bez niej (wylacznik, zapis bez K1) Pentos/Qarth/Dorne bez zmian
                if (!TierGearWanted()) { Scribe.Line("Mends: sprzet wedlug tieru (175) - lore w granicy tieru pominiete (" + _tgOffWhy + "; dziala tylko razem z 175.2)."); return; }
                if (!TierGearApplied) return;   // zestawy jeszcze puste - przebieg zapasowy zawola ponownie
                if (_pool == null || _choice == null) PreparePass();
                int pentos = 0, qarth = 0, dorne = 0, sets = 0, stumbles = 0;
                if (main)
                {
                    foreach (var co in MBObjectManager.Instance.GetObjectTypeList<CharacterObject>())
                    {
                        try
                        {
                            if (co == null || co.IsHero || co.IsTemplate || co.Occupation != Occupation.Soldier) continue;
                            if (Late(co)) continue;
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
                }
                Scribe.Line("Mends: sprzet wedlug tieru (175) - lore w granicy tieru: Pentos " + (main ? pentos.ToString() : "wyl.") + ", Qarth " + (main ? qarth.ToString() : "wyl.")
                            + ", Dorne " + (extra ? dorne.ToString() : "wyl.") + ", Qohor " + (extra ? "w sesji, po rozsadku pancerza" : "wyl.") + " slotow; potkniecia " + stumbles + ".");
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "Army175.LoreArmor", null); } catch { } }
        }

        /// <summary>Qohor ciezej (Army175LoreArmorExtra, dom. TAK od 175b - Jeff 09.10 ok. 05:50) - w SESJI, po ArmorSanity (TierGearCheck, zapasy):
        /// "najciezszy korpus" i "ciezszy niz dzis" liczone na ochronie PO rozsadku i tierach na zywo; przy wczytaniu
        /// wygrywalyby sztuki ROT z ochrona oderwana od wagi, ktore ArmorSanity tnie. Przed prawami i SkillSinew.
        /// Koszt kolejnosci: Armoury TroopFit/ColdStart moga zobaczyc stary korpus tych 4 jednostek (tylko w gore / zbrojownie startowe).</summary>
        internal static void QohorHeavier()
        {
            try
            {
                if (_qohorDone) return;
                if (!Wanted("Army175LoreArmorExtra", 1f)) { _qohorDone = true; return; }
                if (!TierGearWanted() || !TierGearApplied) return;
                _qohorDone = true;
                if (_pool == null || _choice == null) PreparePass();
                _tierCache = new Dictionary<ItemObject, int>();   // tiery po rozsadku
                int qohor = 0, sets = 0, stumbles = 0;
                foreach (var id in QohorHeavy)
                {
                    try { var co = MBObjectManager.Instance.GetObject<CharacterObject>(id); if (co != null) qohor += HeavierPass(co, ref sets); else Scribe.Line("Mends: sprzet wedlug tieru (175) - lore: brak jednostki " + id + "."); }
                    catch { stumbles++; }
                }
                Scribe.Line("Mends: sprzet wedlug tieru (175) - lore w granicy tieru, Qohor po rozsadku pancerza: " + qohor + " slotow (najciezszy korpus w tierze wedlug ochrony po ArmorSanity); potkniecia " + stumbles + ".");
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "Army175.QohorHeavier", null); } catch { } }
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
                // zestawy lukow z kaskady 175.2 - tylko razem z nia
                if (!TierGearWanted()) { Scribe.Line("Mends: Zlota Kompania (175.G) - pominiete (" + _tgOffWhy + "; dziala tylko razem z 175.2)."); return; }
                if (!TierGearApplied) return;   // zestawy jeszcze puste - przebieg zapasowy zawola ponownie
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
