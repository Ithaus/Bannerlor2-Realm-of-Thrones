using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// m119-kij - NAJGORSZA BRON Z DOMU (Jeff 25.10 ok. 01:00: "i jakas nagorsza bron dostja akijem moga walczyc sa kije?"; ok. 01:40: "wgraj wszystko razem
    /// z kijem"). Regula "tylko to, co ma" (sklad7b) zostawia czlowieka bez broni, gdy zbrojownia partii nie ma dla niego zadnej uzywalnej sztuki - takze podloga
    /// broni GearFit (FloorPostfix) nic nie znalazla; u Jeffa 24.10 w 1 z 21 bitew "wszystkie partie - bez broni po strazach 64, z polki 26, nic w polce 38" -
    /// 38 ludzi szlo na piesci (zestaw awaryjny DTE zablokowany przez GarrisonKit.EmergencyPrefix). Teraz czlowiek objety ta regula, ktory po wszystkich
    /// strazach nie ma czym walczyc (GearFit.Armed), a wzorzec jego jednostki ma bron w slotach 0-3, bierze z domu najgorsza bron biala (GarrisonKit.HomeWeapon,
    /// z OwnKitSpawn - po ubraniu cywilnym).
    /// To jego rzecz, POZA gospodarka zbrojowni: sztuka tymczasowa DTE (Assignment.MarkSlotAsTemporary) - spawn jej nie zdejmuje (CreateEquipmentForArmory-
    /// Consumption czysci sloty tymczasowe), zwrot po bitwie, lup z ciala, uciekajacy i lup morski jej nie oddaja (wszystkie odczyty agenta DTE ida przez
    /// Global.ProcessAgentEquipment z filtrem ProcessAgentEquipmentRespectingTemporarySlots); GetAgentArmors bierze tylko HasArmorComponent (bron nie idzie do
    /// wrakow); podniesienie przez innego agenta zmienia tylko bron w rekach, nie SpawnEquipment. Precedens: DTE sam daje tak zestaw awaryjny. Jedyny warunek:
    /// agent zarejestrowany przez DTE RegisterSpawnedAgentAssignment (bez wpisu DTE czyta agenta bez filtra) - licznik kontrolny w GarrisonKit.RegisterPostfix
    /// ("potwierdzone tymczasowe M z N"); bez tej latki nikt nie dostaje kija.
    ///
    /// PULA (raz na kampanie, leniwie; Reset z GarrisonKit.Reset): bron jednoreczna (OneHandedWeapon) Grade 1 (Recipes.Grade - pulapka Tier1 == 0), bez wymogu
    /// (Difficulty 0 - ItemReq.Meets przepuszcza zawsze), w handlu (!NotMerchandise, Value > 0), bez crafted_item_*, tylko biala (zadne uzycie miotane, pociskowe,
    /// amunicja ani tarcza), bez unikatow (RotUniques, straz CrashScribe BarredInBattle, UniqueGear), legend, sprzetu olbrzymow i umarlych - sita jak Civvies
    /// i podloga broni. ROT 8.1.8 wedlug danych (items-dump 24.10): 15 sztuk 5-10 d - peasant_hammer_1/2, peasant_sickle, peasant_hatchet, peasant_pickaxe (bez
    /// kultury), battania_mace_1/2 (Polnoc), empire_mace_1 (Braavos), vlandia_mace_1 (Westerlands), aserai_mace_1/2 (Dorne), khuzait_mace_1 (Dothrakowie),
    /// nord_hatchett_2/3 i naval_one_handed_axe (Lorath). Poza pula m.in. pochodnia, bat, drewniane miecze, cepy (poza handlem), bo_staff, widly i kosa (nie
    /// jednoreczne), Hoe (wymog 35).
    /// WYBOR przy wystawieniu (kolejnosc jak w Civvies): (a) sztuki wlasnej kultury, (b) innej kultury, ktora mu wolno (GearFit.CultureOk - ta sama strona
    /// Waskiego Morza, styl krewny, wlasny wzorzec; bron Dothrakow tylko u Dothrakow - DothBar), (c) bez kultury (albo neutral_culture); w pierwszym niepustym
    /// poziomie LOSOWO (MBRandom, rowno - jak ubrania: "zeby wszyscy nie wygladali tak samo") sposrod tych, ktorych czlowiek uzyje (jezdziec - MenUpgrade.MountOk,
    /// kobieta - bez NotUsableByFemale, ItemReq.Meets); zadna - bez broni (licznik linii bitwy).
    /// </summary>
    internal static class HomeArms
    {
        private static readonly ItemObject[] None = new ItemObject[0];
        private static ItemObject[] _all = None;     // pula, po cenie (Value, potem id)
        // poziom sztuki puli dla biezacego czlowieka (Choose) - bufor bez alokacji; tylko watek glowny (Mission.SpawnAgent, BattleNotice, StartLines)
        private static int[] _lv = new int[0];
        private static bool _built;
        private static int _human;                   // numer rasy "human" (FaceGen) - olbrzymy i umarli maja inne
        private static int _seen, _cutCrafted, _cutTier, _cutSkill, _cutTrade, _cutRanged, _cutBarred;

        internal static void Reset()
        {
            _all = None; _lv = new int[0]; _built = false; _human = 0;
            _seen = _cutCrafted = _cutTier = _cutSkill = _cutTrade = _cutRanged = _cutBarred = 0;
        }

        /// <summary>Tylko bron biala: kazde uzycie sztuki bez strzelania, rzucania, amunicji i tarczy, pierwsze - bron biala.</summary>
        private static bool MeleeOnly(ItemObject it)
        {
            var ws = it.Weapons;
            if (ws == null || ws.Count == 0) return false;
            foreach (var w in ws)
            {
                if (w == null) continue;
                if (w.IsRangedWeapon || w.IsConsumable || w.IsAmmo || w.IsShield) return false;
            }
            var p = it.PrimaryWeapon;
            return p != null && p.IsMeleeWeapon;
        }

        /// <summary>Pula - raz na kampanie: jeden przebieg po przedmiotach. Wywrotka: pula pusta (nikt nie dostaje kija), linia bledu.</summary>
        private static void Build()
        {
            if (_built) return;
            _built = true;
            try
            {
                try { _human = FaceGen.GetRaceOrDefault("human"); } catch { _human = 0; }
                int bad = 0;
                var l = new List<ItemObject>();
                foreach (var it in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
                {
                    try
                    {
                        if (it == null || it.StringId == null || it.ItemType != ItemObject.ItemTypeEnum.OneHandedWeapon) continue;
                        _seen++;
                        if (it.StringId.StartsWith("crafted_item", StringComparison.Ordinal)) { _cutCrafted++; continue; }
                        if (Recipes.Grade(it) != 1) { _cutTier++; continue; }                 // pulapka Tier1 == 0 - Grade
                        if (it.Difficulty > 0) { _cutSkill++; continue; }
                        if (it.NotMerchandise || it.Value <= 0) { _cutTrade++; continue; }
                        if (!MeleeOnly(it)) { _cutRanged++; continue; }
                        if (RotUniques.Ids.Contains(it.StringId) || QuartermasterLaw.BarredInBattle(it) || UniqueGear.Is(it) || GiantGear.Is(it) || UniqueLaw.IsDead(it)
                            || LegendaryLaw.IsLegend(it))
                        { _cutBarred++; continue; }
                        l.Add(it);
                    }
                    catch { bad++; }
                }
                l.Sort((a, b) =>
                {
                    int c = a.Value.CompareTo(b.Value);
                    return c != 0 ? c : string.CompareOrdinal(a.StringId, b.StringId);
                });
                _all = l.ToArray();
                _lv = new int[_all.Length];
                StartLines(bad);
            }
            catch (Exception e) { _all = None; _lv = new int[0]; Log.Error("HomeArms.Build", e); }
        }

        /// <summary>Czlowiek innej rasy niz "human" (olbrzymy, umarli ROT) - bez kija z domu.</summary>
        internal static bool NonHuman(CharacterObject co)
        {
            if (co == null) return false;
            Build();
            try { return co.Race != _human; } catch { return false; }
        }

        /// <summary>Kij z domu dla czlowieka (losowo w pierwszym niepustym poziomie). level: 1 wlasna kultura, 2 dozwolona inna, 3 bez kultury; 0 - nic.</summary>
        internal static ItemObject Pick(CharacterObject co, bool mounted, out int level) { return Choose(co, mounted, true, out level); }

        /// <summary>Czy dla tego czlowieka jest jakas sztuka (BattleNotice - komunikat przed spawnem, bez losowania).</summary>
        internal static bool Has(CharacterObject co, bool mounted) { int l; return Choose(co, mounted, false, out l) != null; }

        private static ItemObject Choose(CharacterObject co, bool mounted, bool random, out int level)
        {
            level = 0;
            if (co == null) return null;
            Build();
            var all = _all;
            if (all.Length == 0) return null;
            if (_lv.Length != all.Length) _lv = new int[all.Length];
            string tc = co.Culture != null ? co.Culture.StringId ?? "" : "";
            int n1 = 0, n2 = 0, n3 = 0;
            bool was = GearFit.Quiet;
            GearFit.Quiet = true;   // wybor, nie odmowa sztuki z polki - bez licznika odmow broni Dothrakow linii doby (jak DothrakiArms.NobodyMay)
            try
            {
                for (int i = 0; i < all.Length; i++)
                {
                    int l = Usable(co, all[i], mounted) ? LevelOf(co, tc, all[i]) : 0;
                    _lv[i] = l;
                    if (l == 1) n1++; else if (l == 2) n2++; else if (l == 3) n3++;
                }
            }
            finally { GearFit.Quiet = was; }
            int lvl = n1 > 0 ? 1 : n2 > 0 ? 2 : n3 > 0 ? 3 : 0;
            if (lvl == 0) return null;
            int m = lvl == 1 ? n1 : lvl == 2 ? n2 : n3;
            int j = random && m > 1 ? MBRandom.RandomInt(m) : 0;
            for (int i = 0; i < all.Length; i++)
                if (_lv[i] == lvl && j-- == 0) { level = lvl; return all[i]; }
            return null;
        }

        /// <summary>1 - kultura czlowieka, 2 - inna kultura, ktora mu wolno (GearFit.CultureOk z DothBar), 3 - bez kultury / neutral_culture; 0 - obca.</summary>
        private static int LevelOf(CharacterObject co, string tc, ItemObject it)
        {
            string ic = it.Culture != null ? it.Culture.StringId : null;
            if (string.IsNullOrEmpty(ic) || ic == "neutral_culture") return 3;
            if (ic == tc) return 1;
            return GearFit.CultureOk(co, it) ? 2 : 0;
        }

        private static bool Usable(CharacterObject co, ItemObject it, bool mounted)
        {
            if (co.IsFemale && (it.ItemFlags & ItemFlags.NotUsableByFemale) != 0) return false;
            if (mounted && !MenUpgrade.MountOk(it)) return false;
            return ItemReq.Meets(co, it);
        }

        /// <summary>Raz na kampanie (przy budowie puli): naglowek z pula i odrzuconymi, potem linia na kulture - co dostalby jej BasicTroop pieszo.</summary>
        private static void StartLines(int bad)
        {
            var sb = new StringBuilder(1200);
            sb.Append("Kij z domu (m119): raz na kampanie; pula = bron jednoreczna biala Grade 1, bez wymogu, w handlu, bez crafted_item, unikatow, legend, olbrzymow")
              .Append(" i umarlych: ").Append(_all.Length).Append(" szt. (po cenie:");
            for (int i = 0; i < _all.Length && i < 24; i++)
            {
                var it = _all[i];
                sb.Append(i > 0 ? ", " : " ").Append(it.StringId).Append(' ').Append(it.Value).Append("d ").Append(it.Culture != null ? it.Culture.StringId : "-");
            }
            if (_all.Length > 24) sb.Append(", ... (+").Append(_all.Length - 24).Append(')');
            sb.Append("); odrzucone z ").Append(_seen).Append(" jednorecznych: crafted_item ").Append(_cutCrafted).Append(", ponad Grade 1 ").Append(_cutTier)
              .Append(", z wymogiem ").Append(_cutSkill).Append(", poza handlem ").Append(_cutTrade).Append(", miotane/pociskowe/tarcze ").Append(_cutRanged)
              .Append(", unikaty/legendy/olbrzymy/umarli ").Append(_cutBarred).Append(bad > 0 ? ", bledy odczytu " + bad : "")
              .Append("; rasa czlowieka nr ").Append(_human).Append("; wybor: wlasna kultura -> dozwolona inna (GearFit.CultureOk) -> bez kultury, losowo w poziomie.");
            Log.Info(sb.ToString());
            if (_all.Length == 0) return;
            try
            {
                foreach (var cu in MBObjectManager.Instance.GetObjectTypeList<CultureObject>())
                {
                    try
                    {
                        if (cu == null || cu.StringId == null) continue;
                        var b = cu.BasicTroop;
                        if (b == null || b.IsHero || Undead.Character(b)) continue;
                        Log.Info("Kij z domu (m119) | " + cu.StringId + " (wedlug " + b.StringId + ", pieszo): " + Describe(b));
                    }
                    catch { }
                }
            }
            catch (Exception e) { Log.Error("HomeArms.StartLines", e); }
        }

        /// <summary>Liczby poziomow i sztuki poziomu, z ktorego ten czlowiek bralby (do logu startu).</summary>
        private static string Describe(CharacterObject co)
        {
            int lv;
            if (Choose(co, false, false, out lv) == null) return "BRAK sztuki - walczy bez broni";
            int[] n = new int[4];
            var ids = new List<string>();
            for (int i = 0; i < _all.Length; i++)
            {
                int l = _lv[i];
                if (l < 1 || l > 3) continue;
                n[l]++;
                if (l == lv && ids.Count < 6) ids.Add(_all[i].StringId);
            }
            string name = lv == 1 ? "wlasna kultura" : lv == 2 ? "dozwolona inna kultura" : "bez kultury";
            return name + " " + n[lv] + " (" + string.Join(", ", ids.ToArray()) + (n[lv] > ids.Count ? ", ..." : "") + ") | poziomy: wlasna " + n[1] + ", dozwolona inna " + n[2]
                   + ", bez kultury " + n[3];
        }
    }
}
