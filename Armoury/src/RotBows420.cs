using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// 420 (audyt RBM 11.11, B7 + wiersz "Luki lore ROT"): STRZALY DLA LUKOW SPOZA LISTY ROT + LUKI LORE Z PELNA ENERGIA. Sama korekta danych w pamieci,
    /// raz na start sesji (wzor: RotData418, HorseBarding419). Bez edycji XML, bez latek Harmony.
    ///
    /// CZESC 1 - PODMIANA GROTOW. Mechanizm jest ROT-u, nie RBM ani nasz: ROT.HarmonyPatches.ModCompatibility.RBM (rot_all.cs:17388-17468) przy wczytaniu XML
    /// (prefiksy na BasicCharacterObject.Deserialize i Equipment.Deserialize -> ParseEquipment) podmienia strzaly w zestawie wedlug luku, ale slownik BuildMap
    /// (rot_all.cs:17637) zna 14 lukow. Zestaw z innym lukiem zostaje ze strzala z XML - u ROT to default_arrows, pod RBM "Barbed Head 50 g" (Cut, 115):
    /// prog przebicia 2.6 x pelny pancerz zamiast 2 x polowa. Zliczenie XML 11.11 (scan420b/c.py): poza lista stoi highland_ranger_bow (90 lb) - 48 zestawow
    /// ROT-Content z default_arrows, wszystkie u lucznikow MILICJI miast (militia_archer t2, militia_veteran_archer t3) osmiu kultur: Dolina, Dorzecze,
    /// Smocza Skala, Krainy Burzy, Reach, Nocna Straz, Wolni Ludzie, upiory; do tego milicje i szlachta wanilii (highland_ranger_bow / noble_bow /
    /// noble_long_bow z default / barbed / range w SandBoxCore, SandBox, NavalDLC). Wlasne luki ROT (weirwood, dragonbone, goldenheart, ravens_teeth, qarth)
    /// tez sa poza lista, ale we wszystkich zestawach maja juz grot przebijajacy - regula je obejmuje, dzis nie ma czego podmieniac.
    ///
    /// REGULA. W zestawie (bojowym i cywilnym) wzorca jednostki albo zestawu z EquipmentRosters, w ktorym jest luk SPOZA listy ROT i nie ma luku z listy,
    /// strzala z grotem szerokim zamieniana jest na iglice TEJ SAMEJ WAGI: default_arrows 50 g -> bodkin_arrows_c 50 g, barbed_arrows 60 g ->
    /// heavy_steppe_arrows 60 g, range_arrows 40 g -> steppe_arrows 40 g. Ta sama waga = ten sam tier gry (RBM CalculateAmmoTier: obrazenia x waga -
    /// 0.93 / 0.81, 1.85 / 1.61, 0.01 / 0.01 - calkowity tier rowny, ulamek nizszy, wiec cena GRY z DefaultItemValueModel jest dla iglicy nizsza o
    /// ok. 13%) i ten sam koszt wyrobu u nas (ArmsPricing: grot bodkin i plaski to ta sama robota), wiec prawo tieru CrashScribe
    /// (Army175: amunicja nie ponad tier jednostki), koszyki strzelarzy (typ x tier) i zamowienia nie zmieniaja sie - zmienia sie tylko typ grotu.
    /// Para jest sprawdzana przy starcie (obie sztuki istnieja, zrodlo Cut, cel Pierce, cel nie wyzszego tieru, waga rowna) - inaczej para nieczynna.
    /// NIE ruszamy: lukow z listy ROT (takze tych, ktorym ROT swiadomie daje grot szeroki: steppe_bow 90 lb -> barbed, hunting_bow 60 lb -> range),
    /// kusz, strzal specjalnych (turniejowe, hartowane, olbrzymow, Ravens' Teeth), ekwipunku bohaterow (jest w zapisie; ubiera ich nasz dobor - RangedRank).
    /// Skutek z wzorow RBM (luk 90 lb, 20 m, tulow, ofiara bez premii wypoczetego): nagi 76 -> 66, skora 33: 6 -> 40, przeszywanica 61: 5 -> 16,
    /// kolczuga 88: 4 -> 5 (prog 88 J, strzala niesie 74), plyta bez zmian (2-5).
    ///
    /// KOLEJNOSC (420-p, sprostowanie). MbEvent dopisuje nasluch na czolo listy i wola od czola (cs_full.cs:22405-22424), wiec zachowanie zarejestrowane
    /// PRZED ArmouryBehavior bieglo PO nim - odwrotnie, niz zakladalo 420 (TownFletchers.SessionStart widzial zestawy sprzed podmiany). Teraz obie czesci
    /// wola wprost ArmouryBehavior: ApplyAmmo jako pierwsza rzecz w jego OnSessionLaunched (spisy liczone ze wzorcow przy starcie - CraftRealm.Wearers,
    /// TownFletchers, WorkshopLaw - widza zestawy po podmianie niezaleznie od kolejnosci rejestracji), ApplyLore na poczatku jego nasluchu
    /// OnAfterSessionLaunched z ArmsPricing.RefreshAfterFinalStats (dopisany ostatni = biegnie pierwszy z nasluchow ArmouryBehavior; OnSessionLaunched
    /// ROT-u jest wtedy dawno skonczony). Wlasnych nasluchow zachowanie nie ma. Wzorce nie sa w zapisie - przy kazdym wczytaniu powstaja z XML.
    /// Wzgledem CrashScribe (Army175.TierGear) kolejnosc zalezy od ladowania modow, ale TierGear buduje zestawy klonem BIEZACEGO zestawu
    /// (new Equipment(eq), Army175Gear.cs:981) - przebieg po nas kopiuje iglice, przebieg przed nami zostawia nam swoje klony do podmiany.
    /// Przebieg po EquipmentRosters obejmuje takze rostery-wzorce bohaterow (BasicCharacterObject.Deserialize rejestruje roster kazdej postaci) i rostery
    /// odpiete przez Army175 - tak samo jak podmiana ROT (Equipment.Deserialize); "bohaterowie pominieci" znaczy: ich ZYWY ekwipunek (kopia w zapisie).
    ///
    /// CZESC 2 - LUKI LORE. Pod RBM energia strzaly = energia kinetyczna x (obrazenia luku + obrazenia strzaly - 100) / 100: silnik sumuje
    /// GetModifiedThrustDamageForCurrentUsage luku z obrazeniami strzaly (Mission.OnAgentShootMissile, mb_full.cs:56932), RBM odejmuje 100
    /// (MagnitudeChanges.CalculateMissileMagnitude, rbm.cs:1445). Kazdy luk RBM ma 100. ROT daje swoim: XML weirwood / dragonbone 84, qarth 95, ravens_teeth 96,
    /// goldenheart 97, a ROTRBMCompatibility.ModifyBowsAndArrows (rot_all.cs:32169, OnSessionLaunched) wpisuje te same liczby jeszcze raz razem z naciagiem
    /// 170-210 lb. Skutek: Dragonbone 180 lb bije 16% slabiej niz zwykly luk 180 lb (137 J zamiast 163 z bodkinem 90 g z 20 m; woodland_longbow 180 lb: 142).
    /// Regula: tym pieciu lukom obrazenia = 100, w OnAfterSessionLaunched (po OnSessionLaunched ROT-u). Tier luku RBM liczy z samego naciagu
    /// (OverrideCalculateRangedWeaponTier), cene ROT wpisuje wprost, nasza wycena obrazen luku nie czyta - naciag, szybkosc naciagu, celnosc, cena, tier i
    /// wymog bez zmian. giant_bow ma juz 100 (ROT) - poza lista.
    ///
    /// Obiekty sa nowe w kazdej grze, wiec klucze dzialaja od nastepnego wczytania; gdyby te same obiekty przyszly drugi raz, obie czesci najpierw
    /// przywracaja stan sprzed zmiany - NIE = dokladnie dane ROT. Regula nie zna wlasciciela: gracz i AI jednakowo.
    /// </summary>
    internal sealed class RotBows420 : CampaignBehaviorBase
    {
        // ROT.HarmonyPatches.ModCompatibility.RBM.BuildMap (dekompilacja ROT 8.1.8, rot_all.cs:17637-17656) - luki, ktorym ROT sam dobiera strzaly
        private static readonly HashSet<string> RotMappedBows = new HashSet<string>(StringComparer.Ordinal)
        {
            "woodland_longbow", "lowland_yew_bow", "woodland_yew_bow", "steppe_war_bow", "tribal_bow", "steppe_heavy_bow", "lowland_longbow",
            "nordic_shortbow", "composite_steppe_bow", "composite_bow", "glen_ranger_bow", "steppe_bow", "mountain_hunting_bow", "hunting_bow",
        };

        // grot szeroki (Cut 115) -> iglica (Pierce 100) tej samej wagi (RBMCombat_arrow_visuals.xml: 50 / 60 / 40 g)
        private static readonly string[] HeadFrom = { "default_arrows", "barbed_arrows", "range_arrows" };
        private static readonly string[] HeadTo = { "bodkin_arrows_c", "heavy_steppe_arrows", "steppe_arrows" };

        // wlasne luki ROT z obrazeniami ponizej normy RBM (ROTassets.xml: 84 / 84 / 97 / 96 / 95)
        private static readonly string[] LoreBowIds = { "weirwood_bow", "dragonbone_bow", "goldenheart_longbow", "ravens_teeth_longbow", "qarth_longbow" };
        private const int RbmBowThrust = 100;             // obrazenia kazdego luku i kuszy RBM (RBMCombat_ranged.xml) - mnoznik energii 1.00 z iglica

        private struct Swap { public Equipment Eq; public int Slot; public EquipmentElement Old; }

        private sealed class RefCmp : IEqualityComparer<Equipment>
        {
            public bool Equals(Equipment a, Equipment b) { return ReferenceEquals(a, b); }
            public int GetHashCode(Equipment e) { return RuntimeHelpers.GetHashCode(e); }
        }

        // stan sesji (glowny watek). Lore: podmieniany w calosci, czyta go sciezka trafien
        private static List<Swap> _swaps = new List<Swap>();
        private static Dictionary<WeaponComponentData, int> _loreOrig = new Dictionary<WeaponComponentData, int>();
        private static HashSet<ItemObject> _lore = new HashSet<ItemObject>();
        private static bool _swapOn, _loreOn;
        private static int _setsChanged, _loreChanged;
        // 420-p: tylko na czas ApplyAmmo (glowny watek) - podmienione zestawy z rosterow bez zywej jednostki; zestawy z lukiem Z LISTY ROT i grotem szerokim
        private static int _setsRosterOnly;
        private static SortedDictionary<string, int> _listedBroad;

        // liczniki misji (glowny watek). Trafienia strzal w ludzi: 0 = grot przebijajacy, 1 = grot szeroki, 2 = strzala z luku lore (podzbior 0 / 1)
        private static readonly int[] HitN = new int[3];
        private static readonly long[] HitDmg = new long[3];
        private static readonly int[] HitMax = new int[3];
        private static int _archPierce, _archCut, _archOther, _archLore;

        public override void RegisterEvents()
        {
            // 420-p: bez wlasnych nasluchow - ApplyAmmo i ApplyLore wola ArmouryBehavior w ustalonym miejscu (patrz KOLEJNOSC w naglowku)
        }

        public override void SyncData(IDataStore dataStore) { }

        private static bool RotLinkLoaded()
        {
            try { return AccessTools.TypeByName("ROT.HarmonyPatches.ModCompatibility.RBM") != null; } catch { return false; }
        }

        /// <summary>420-p: ustawienie ROT "RBM Integration" (ROT.ROTSettings.RBMIntegration, rot_all.cs:6865; domyslnie w MCM false, u Jeffa true) - bez niego
        /// ROT nie podmienia strzal wcale (rot_all.cs:17391, 17474). 1 = wlaczone, 0 = wylaczone, -1 = nie da sie odczytac (wtedy jak w 420: dzialamy).</summary>
        private static int RotIntegration()
        {
            try
            {
                var t = AccessTools.TypeByName("ROT.ROTSettings");
                var p = t != null ? AccessTools.Property(t, "RBMIntegration") : null;
                if (p == null || p.PropertyType != typeof(bool)) return -1;
                return (bool)p.GetValue(null, null) ? 1 : 0;
            }
            catch { return -1; }
        }

        // ---------------- czesc 1: strzaly dla lukow spoza listy ROT ----------------

        internal static void ApplyAmmo()
        {
            var old = _swaps;
            var neu = new List<Swap>();
            _swapOn = false; _setsChanged = 0;
            _setsRosterOnly = 0; _listedBroad = new SortedDictionary<string, int>(StringComparer.Ordinal);
            try
            {
                // te same zestawy drugi raz w tym procesie: najpierw stan sprzed podmiany (NIE = dane ROT)
                int restored = 0;
                if (old != null)
                    for (int i = old.Count - 1; i >= 0; i--)
                    {
                        try { old[i].Eq[(EquipmentIndex)old[i].Slot] = old[i].Old; restored++; } catch { }
                    }
                var c = Settings.Current;
                if (c == null) { _swaps = neu; return; }
                int rotInt = RotIntegration();

                var from = new ItemObject[HeadFrom.Length]; var to = new ItemObject[HeadFrom.Length]; var ok = new bool[HeadFrom.Length];
                int pairs = 0; var sbBad = new StringBuilder();
                for (int i = 0; i < HeadFrom.Length; i++)
                {
                    string why = null;
                    try
                    {
                        from[i] = MBObjectManager.Instance.GetObject<ItemObject>(HeadFrom[i]);
                        to[i] = MBObjectManager.Instance.GetObject<ItemObject>(HeadTo[i]);
                        why = PairFault(from[i], to[i]);
                    }
                    catch (Exception e) { why = "blad " + e.GetType().Name; }
                    ok[i] = why == null;
                    if (ok[i]) pairs++;
                    else sbBad.Append(sbBad.Length > 0 ? "; " : "").Append(HeadFrom[i]).Append(" -> ").Append(HeadTo[i]).Append(": ").Append(why);
                }

                string off = !c.RotUnlistedBowBodkinsEnabled ? "klucz ROT Bodkins For Unlisted Bows NIE - strzaly jak wczytal ROT"
                           : !RangedRank.RbmLoaded ? "brak Realistic Battle Mod - poza RBM grot szeroki nie jest slabszy, bez zmian"
                           : !RotLinkLoaded() ? "brak lacznika ROT z RBM (ROT.HarmonyPatches.ModCompatibility.RBM) - nie ma listy do uzupelnienia, bez zmian"
                           : rotInt == 0 ? "ustawienie ROT RBM Integration NIE - ROT nie podmienia strzal zadnemu lukowi, nie ma listy do uzupelnienia, bez zmian"
                           : pairs == 0 ? "zadna para grotow nie przeszla kontroli (" + sbBad + ") - bez zmian" : null;
                _swapOn = off == null;

                var seen = new HashSet<Equipment>(new RefCmp());
                var troops = new HashSet<CharacterObject>();
                var byBow = new SortedDictionary<string, int>(StringComparer.Ordinal);
                var perPair = new int[HeadFrom.Length];
                var sbEx = new StringBuilder();
                int setsBow = 0, setsMixed = 0, setsBroad = 0, slotsBroad = 0, heroes = 0, stumbles = 0;

                foreach (var co in MBObjectManager.Instance.GetObjectTypeList<CharacterObject>())
                {
                    try
                    {
                        if (co == null) continue;
                        if (co.IsHero) { heroes++; continue; }      // ekwipunek bohatera to jego wlasna kopia w zapisie - nie wzorzec
                        foreach (var eq in co.BattleEquipments) OneSet(eq, co, from, to, ok, neu, seen, troops, byBow, perPair, sbEx, ref setsBow, ref setsMixed, ref setsBroad, ref slotsBroad);
                        foreach (var eq in co.CivilianEquipments) OneSet(eq, co, from, to, ok, neu, seen, troops, byBow, perPair, sbEx, ref setsBow, ref setsMixed, ref setsBroad, ref slotsBroad);
                    }
                    catch (Exception e) { if (stumbles++ == 0) Log.Error("RotBows420.ApplyAmmo(postac)", e); }
                }
                // zestawy z EquipmentRosters (szablony lordow i postaci - z nich ubiera sie nowy bohater); ROT podmienia w nich tak samo (Equipment.Deserialize)
                foreach (var ro in MBObjectManager.Instance.GetObjectTypeList<MBEquipmentRoster>())
                {
                    try
                    {
                        if (ro == null) continue;
                        foreach (var eq in ro.AllEquipments) OneSet(eq, null, from, to, ok, neu, seen, troops, byBow, perPair, sbEx, ref setsBow, ref setsMixed, ref setsBroad, ref slotsBroad);
                    }
                    catch (Exception e) { if (stumbles++ == 0) Log.Error("RotBows420.ApplyAmmo(zestaw)", e); }
                }
                _swaps = neu;

                var sbBows = new StringBuilder();
                foreach (var kv in byBow) sbBows.Append(sbBows.Length > 0 ? ", " : "").Append(kv.Key).Append(' ').Append(kv.Value);
                // 420-p: pomiar - zestawy z lukiem z listy ROT, w ktorych zostal grot szeroki (steppe_bow / hunting_bow: wybor ROT; inny luk = zestaw, ktorego
                // lacznik ROT nie przetworzyl, np. wzorzec bez isROT) - nie ruszane
                var sbListed = new StringBuilder(); int listedSets = 0;
                foreach (var kv in _listedBroad) { listedSets += kv.Value; sbListed.Append(sbListed.Length > 0 ? ", " : "").Append(kv.Key).Append(' ').Append(kv.Value); }
                string tail420p = " | ROT RBM Integration " + (rotInt == 1 ? "TAK" : rotInt == 0 ? "NIE" : "nieodczytane")
                                + " | zestawy z lukiem Z LISTY ROT i grotem szerokim (nie ruszane; poza steppe_bow / hunting_bow = nieprzetworzone przez lacznik ROT): "
                                + listedSets + (listedSets > 0 ? " (" + sbListed + ")" : "");
                _listedBroad = null;
                string found = "zestawy z lukiem spoza listy ROT (14 lukow BuildMap): " + setsBow + " (pominiete, bo jest w nich tez luk z listy: " + setsMixed
                             + "), z grotem szerokim " + setsBroad + " (sztuk strzal " + slotsBroad + ")";
                if (!_swapOn)
                    Log.Info("Luki ROT (420) strzaly: " + off + " | " + found + (restored > 0 ? " | przywrocone do danych ROT " + restored : "")
                             + " | luki tych zestawow: " + (sbBows.Length > 0 ? sbBows.ToString() : "-") + tail420p + " (tylko pomiar w linii konca misji).");
                else
                    Log.Info("Luki ROT (420) strzaly: grot szeroki -> iglica tej samej wagi i tieru | " + found + " | podmienione zestawy " + _setsChanged
                             + " (wzorcow jednostek " + troops.Count + "; z tego zestawy z rosterow bez zywej jednostki - wzorce bohaterow, rostery odpiete przez 175 - "
                             + _setsRosterOnly + "): " + HeadFrom[0] + " -> " + HeadTo[0] + " " + perPair[0] + ", " + HeadFrom[1] + " -> " + HeadTo[1] + " "
                             + perPair[1] + ", " + HeadFrom[2] + " -> " + HeadTo[2] + " " + perPair[2]
                             + (pairs < HeadFrom.Length ? " | pary nieczynne: " + sbBad : "")
                             + " | luki podmienionych zestawow: " + (sbBows.Length > 0 ? sbBows.ToString() : "-")
                             + " | np. " + (sbEx.Length > 0 ? sbEx.ToString() : "-")
                             + " | bohaterowie: zywy ekwipunek pominiety " + heroes + " (ich rostery-wzorce podmieniane jak u ROT)" + (stumbles > 0 ? " | potkniecia " + stumbles : "")
                             + tail420p
                             + " | luki z listy ROT (takze steppe_bow i hunting_bow z grotem szerokim), kusze i strzaly specjalne bez zmian.");
            }
            catch (Exception e) { _swaps = neu; Log.Error("RotBows420.ApplyAmmo", e); }
        }

        /// <summary>Kontrola pary przy starcie: null = para czynna, inaczej powod (do logu).</summary>
        private static string PairFault(ItemObject a, ItemObject b)
        {
            if (a == null) return "brak grotu szerokiego w grze";
            if (b == null) return "brak iglicy w grze";
            if (a.ItemType != ItemObject.ItemTypeEnum.Arrows || b.ItemType != ItemObject.ItemTypeEnum.Arrows || a.PrimaryWeapon == null || b.PrimaryWeapon == null) return "to nie strzaly";
            if (a.PrimaryWeapon.ThrustDamageType != DamageTypes.Cut) return "zrodlo nie jest grotem szerokim (Cut)";
            if (b.PrimaryWeapon.ThrustDamageType != DamageTypes.Pierce) return "cel nie jest iglica (Pierce)";
            if (Math.Abs(a.Weight - b.Weight) > 0.0015f) return "inna waga (" + a.Weight.ToString("0.000") + " / " + b.Weight.ToString("0.000") + " kg)";
            if (Recipes.Grade(b) > Recipes.Grade(a)) return "iglica wyzszego tieru (t" + Recipes.Grade(a) + " -> t" + Recipes.Grade(b) + ")";
            return null;
        }

        private static void OneSet(Equipment eq, CharacterObject co, ItemObject[] from, ItemObject[] to, bool[] ok, List<Swap> rec, HashSet<Equipment> seen,
            HashSet<CharacterObject> troops, SortedDictionary<string, int> byBow, int[] perPair, StringBuilder sbEx,
            ref int setsBow, ref int setsMixed, ref int setsBroad, ref int slotsBroad)
        {
            if (eq == null || ReferenceEquals(eq, MBEquipmentRoster.EmptyEquipment) || !seen.Add(eq)) return;
            ItemObject bow = null; bool mapped = false;
            for (int s = 0; s < 4; s++)
            {
                var it = eq[(EquipmentIndex)s].Item;
                if (it == null || it.ItemType != ItemObject.ItemTypeEnum.Bow) continue;
                if (RotMappedBows.Contains(it.StringId)) mapped = true; else bow = it;
            }
            if (bow == null)
            {
                // 420-p: sam pomiar - luk z listy ROT i grot szeroki w tym samym zestawie
                if (mapped && _listedBroad != null)
                {
                    string lb = null; bool br = false;
                    for (int s = 0; s < 4; s++)
                    {
                        var it = eq[(EquipmentIndex)s].Item;
                        if (it == null) continue;
                        if (it.ItemType == ItemObject.ItemTypeEnum.Bow) { if (lb == null) lb = it.StringId; }
                        else for (int i = 0; i < from.Length; i++) if (ReferenceEquals(it, from[i])) br = true;
                    }
                    if (br && lb != null) { int n0; _listedBroad.TryGetValue(lb, out n0); _listedBroad[lb] = n0 + 1; }
                }
                return;
            }
            setsBow++;
            if (mapped) { setsMixed++; return; }      // ROT dobral juz strzaly wedlug luku z listy (ParseEquipment) - jego wybor zostaje
            bool broad = false, changed = false;
            for (int s = 0; s < 4; s++)
            {
                var el = eq[(EquipmentIndex)s];
                var it = el.Item;
                if (it == null) continue;
                int p = -1;
                for (int i = 0; i < from.Length; i++) if (ReferenceEquals(it, from[i])) { p = i; break; }
                if (p < 0) continue;
                broad = true; slotsBroad++;
                if (!_swapOn || !ok[p]) continue;
                rec.Add(new Swap { Eq = eq, Slot = s, Old = el });
                eq[(EquipmentIndex)s] = new EquipmentElement(to[p], el.ItemModifier);
                perPair[p]++; changed = true;
            }
            if (broad)
            {
                setsBroad++;
                int n; byBow.TryGetValue(bow.StringId, out n); byBow[bow.StringId] = n + 1;
            }
            if (!changed) return;
            _setsChanged++;
            if (co == null) _setsRosterOnly++;
            if (co != null && troops.Add(co) && troops.Count <= 6)
                sbEx.Append(sbEx.Length > 0 ? ", " : "").Append(co.StringId).Append(" t").Append(co.Tier).Append(" [").Append(bow.StringId).Append(']');
        }

        // ---------------- czesc 2: luki lore z pelna energia ----------------

        internal static void ApplyLore()
        {
            var old = _loreOrig;
            var neu = new Dictionary<WeaponComponentData, int>();
            var set = new HashSet<ItemObject>();
            _loreOn = false; _loreChanged = 0;
            try
            {
                var fThrust = AccessTools.Field(typeof(WeaponComponentData), "<ThrustDamage>k__BackingField");
                // te same sztuki drugi raz w tym procesie: najpierw obrazenia sprzed zmiany (NIE = dane ROT)
                int restored = 0;
                if (fThrust != null && old != null)
                    foreach (var kv in old) { try { fThrust.SetValue(kv.Key, kv.Value); restored++; } catch { } }
                var c = Settings.Current;
                if (c == null) { _loreOrig = neu; _lore = set; return; }
                bool rbm = RangedRank.RbmLoaded;
                string off = !c.RotLoreBowsFullEnergyEnabled ? "klucz ROT Lore Bows At Full Strength NIE - obrazenia lukow jak ustawil ROT"
                           : !rbm ? "brak Realistic Battle Mod - poza RBM obrazenia luku to nie mnoznik energii, bez zmian"
                           : fThrust == null ? "brak pola WeaponComponentData.ThrustDamage (inna wersja gry) - bez zmian" : null;
                _loreOn = off == null;

                int missing = 0, odd = 0, atNorm = 0, below = 0;
                var sb = new StringBuilder();
                foreach (var id in LoreBowIds)
                {
                    try
                    {
                        var it = MBObjectManager.Instance.GetObject<ItemObject>(id);
                        if (it == null) { missing++; continue; }
                        if (it.ItemType != ItemObject.ItemTypeEnum.Bow || it.WeaponComponent == null || it.WeaponComponent.Weapons == null) { odd++; continue; }
                        set.Add(it);      // w zbiorze takze przy kluczu NIE - pomiar "przed" w linii konca misji
                        bool low = false;
                        foreach (var w in it.WeaponComponent.Weapons)
                        {
                            if (w == null || w.ThrustDamage >= RbmBowThrust) continue;
                            low = true;
                            int was = w.ThrustDamage;
                            if (_loreOn) { neu[w] = was; fThrust.SetValue(w, RbmBowThrust); }
                            sb.Append(sb.Length > 0 ? ", " : "").Append(id).Append(' ').Append(w.MissileSpeed).Append(" lb: ").Append(was)
                              .Append(_loreOn ? " -> " + RbmBowThrust : "").Append(" (energia x").Append((was / 100f).ToString("0.00")).Append(_loreOn ? " -> x1.00)" : ")");
                        }
                        if (!low) atNorm++;
                        else { below++; if (_loreOn) _loreChanged++; }
                    }
                    catch (Exception e) { odd++; Log.Error("RotBows420.ApplyLore " + id, e); }
                }
                _loreOrig = neu; _lore = set;

                // pomiar: inne luki ponizej normy (nie ruszane) - gdyby ROT albo inny mod dolozyl kolejne
                int others = 0; var sbO = new StringBuilder();
                if (rbm)
                {
                    try
                    {
                        foreach (var it in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
                        {
                            if (it == null || it.ItemType != ItemObject.ItemTypeEnum.Bow || set.Contains(it) || it.PrimaryWeapon == null) continue;
                            if (it.PrimaryWeapon.ThrustDamage >= RbmBowThrust) continue;
                            others++;
                            if (others <= 6) sbO.Append(sbO.Length > 0 ? ", " : "").Append(it.StringId).Append(' ').Append(it.PrimaryWeapon.ThrustDamage);
                        }
                    }
                    catch { }
                }
                Log.Info("Luki ROT (420) lore: " + (_loreOn ? "obrazenia luku = " + RbmBowThrust + " jak u kazdego luku RBM (mnoznik energii strzaly = (luk + strzala - 100) / 100)" : off)
                         + " | luki z listy ponizej normy: " + below + (_loreOn ? ", podniesione " + _loreChanged : "") + ", juz w normie " + atNorm + ", brak w grze " + missing
                         + ", pominiete " + odd + (restored > 0 ? ", przywrocone do danych ROT " + restored : "")
                         + " | " + (sb.Length > 0 ? sb.ToString() : "-")
                         + " | inne luki ponizej " + RbmBowThrust + " (nie ruszane): " + others + (others > 0 ? " (" + sbO + (others > 6 ? ", ..." : "") + ")" : "")
                         + " | naciag, szybkosc naciagu, celnosc, cena, tier i wymog bez zmian.");
            }
            catch (Exception e) { _loreOrig = neu; _lore = set; Log.Error("RotBows420.ApplyLore", e); }
        }

        // ---------------- liczniki misji ----------------

        private static void Reset()
        {
            for (int i = 0; i < 3; i++) { HitN[i] = 0; HitDmg[i] = 0; HitMax[i] = 0; }
            _archPierce = 0; _archCut = 0; _archOther = 0; _archLore = 0;
        }

        private static string Cell(int i)
        {
            return HitN[i] + " / " + (HitN[i] > 0 ? (HitDmg[i] / HitN[i]).ToString() : "-") + " / " + HitMax[i];
        }

        private static void Flush(Mission m)
        {
            if (_archPierce + _archCut + _archOther + HitN[0] + HitN[1] == 0) return;      // misja bez lucznikow i bez strzal - bez linii
            string kind = "?";
            try { kind = MissionTeams.Kind(m); } catch { }
            Log.Info("Luki (420): koniec misji (" + kind + ") | strzaly dla lukow spoza listy ROT " + (_swapOn ? "TAK (zestawow " + _setsChanged + ")" : "NIE")
                     + " | luki lore z pelna energia " + (_loreOn ? "TAK (lukow " + _loreChanged + ")" : "NIE")
                     + " | lucznicy w misji wedlug kolczanu: grot przebijajacy " + _archPierce + ", grot szeroki " + _archCut + ", inny / bez strzal " + _archOther
                     + "; z lukiem lore " + _archLore
                     + " | strzaly w ludzi, nie na tarcze (trafienia / srednio HP / najwiecej): grot przebijajacy - " + Cell(0) + "; grot szeroki - " + Cell(1)
                     + "; z lukow lore - " + Cell(2) + ".");
            Reset();
        }

        /// <summary>Liczniki misji: lucznicy wedlug grotu w kolczanie (z rynsztunku przy wejsciu), trafienia strzal w ludzi wedlug grotu i osobno z lukow lore;
        /// linia sumy na koncu. Dziala takze przy kluczach NIE (pomiar "przed"). Bez alokacji, bez try/catch w sciezce trafien.</summary>
        internal sealed class Logic : MissionLogic
        {
            public override MissionBehaviorType BehaviorType { get { return MissionBehaviorType.Other; } }

            public override void OnBehaviorInitialize()
            {
                base.OnBehaviorInitialize();
                Reset();
            }

            public override void OnAgentBuild(Agent agent, Banner banner)
            {
                if (agent == null || !agent.IsHuman) return;
                var eq = agent.SpawnEquipment;
                if (eq == null) return;
                bool bow = false, lore = false; int head = 0;      // 0 = brak strzal, 1 = przebijajacy, 2 = szeroki, 3 = inny
                var set = _lore;
                for (int s = 0; s < 4; s++)
                {
                    var it = eq[(EquipmentIndex)s].Item;
                    if (it == null) continue;
                    if (it.ItemType == ItemObject.ItemTypeEnum.Bow) { bow = true; if (set.Count > 0 && set.Contains(it)) lore = true; }
                    else if (it.ItemType == ItemObject.ItemTypeEnum.Arrows && head == 0)
                    {
                        var w = it.PrimaryWeapon;
                        head = w == null ? 3 : (w.ThrustDamageType == DamageTypes.Pierce ? 1 : (w.ThrustDamageType == DamageTypes.Cut ? 2 : 3));
                    }
                }
                if (!bow) return;
                if (head == 1) _archPierce++; else if (head == 2) _archCut++; else _archOther++;
                if (lore) _archLore++;
            }

            public override void OnScoreHit(Agent affectedAgent, Agent affectorAgent, WeaponComponentData attackerWeapon, bool isBlocked, bool isSiegeEngineHit,
                in Blow blow, in AttackCollisionData collisionData, float damagedHp, float hitDistance, float shotDifficulty)
            {
                if (affectedAgent == null || !affectedAgent.IsHuman || !blow.IsMissile || isBlocked || isSiegeEngineHit) return;
                if (attackerWeapon == null || attackerWeapon.WeaponClass != WeaponClass.Arrow) return;
                int k = blow.DamageType == DamageTypes.Pierce ? 0 : (blow.DamageType == DamageTypes.Cut ? 1 : -1);
                if (k < 0) return;
                int dmg = blow.InflictedDamage;
                HitN[k]++; HitDmg[k] += dmg;
                if (dmg > HitMax[k]) HitMax[k] = dmg;
                var set = _lore;
                if (set.Count == 0 || affectorAgent == null) return;
                var mw = affectorAgent.WieldedWeapon;      // luk w reku strzelca w chwili trafienia (strzelec, ktory zdazyl zmienic bron, wypada z rubryki)
                if (mw.IsEmpty || mw.Item == null || !set.Contains(mw.Item)) return;
                HitN[2]++; HitDmg[2] += dmg;
                if (dmg > HitMax[2]) HitMax[2] = dmg;
            }

            protected override void OnEndMission()
            {
                try { Flush(Mission); } catch (Exception e) { Log.Error("RotBows420.Flush", e); }
                base.OnEndMission();
            }
        }
    }
}
