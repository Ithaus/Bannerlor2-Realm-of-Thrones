using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using HarmonyLib;
using Helpers;
using SandBox.GameComponents;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 512 (perki, Jeff 14.11: "dodatkowe strzaly to glupota" - nowe efekty 16, 20, 23, 25a, 25b zaakceptowane (21 Pavise - zmiana 506); 33 Breeder urealnic; 44 BeastWhisperer
    /// tylko zwykle konie/juczne, rzadziej). AMUNICJA I ZWIERZETA - jeden plik, wpiecia w istniejacych plikach po jednej linii.
    ///
    /// A. DAWNE "+AMUNICJA" (DeepQuivers, Fletcher, WellPrepared, Resourceful, Saddlebags): gra dodaje je w SandboxAgentStatCalculateModel.InitializeMissionEquipment
    ///    (SetAmountOfSlot z addOverflowToMaxAmount - podnosi ModifiedMaxAmount). DEKOMPILACJA: ROT owija model statystyk agenta dwa razy
    ///    (ROTAgentStatCalculateModel, RoTElephantAgentStatCalculateModel - ROT/SubModule.cs:332/338) i ZADEN z nich nie nadpisuje InitializeMissionEquipment -
    ///    wywolanie z Agent.InitializeMissionEquipment (MissionGameModels.Current) konczy sie w pustej metodzie bazowej AgentStatCalculateModel; NavalDLC
    ///    (NavalAgentStatCalculateModel) przekazuje do BaseModel, czyli tez do ROT. Wniosek z kodu: dawny efekt w tej skladance najpewniej juz nie dziala.
    ///    Straz (klucz AmmoPerksRework): prefiks/postfiks na metodzie gry - jesli jednak pobiegnie, cofa premie perkow (ilosc i ModifiedMaxAmount do bazowych;
    ///    MilitaryPlanner przy szturmie zostaje, jak w grze). Linia doby liczy wywolania - 0 = potwierdzenie, ze metoda nie biegnie.
    /// B. ODZYSK DRUZYNY (perki wodza partii, rola PartyLeader jak dawny drugi efekt): po bitwie, na polu utrzymanym przez strone agenta, odzysk wlasnych pociskow
    ///    x (1 + AmmoPerkPartyPercent/100): DeepQuivers - strzaly, Fletcher - belty, WellPrepared - bron miotana. Bitwa gracza: AmmoRecovery.Postfix (agenci DTE);
    ///    bitwy AI bez misji: AiWear.AmmoUse (ten sam wzor "1 - odzysk"). Przegrany traci wszystko jak dotad (odzysk 0 zostaje 0).
    /// C. OSOBISTE: Fletcher - kusza bohatera (gracz, towarzysz) zuzywa sie o AmmoPerkOwnPercent mniej na strzal (FieldCraft.OnAgentShootMissile).
    ///    Saddlebags - udzwig partii wodza z tym perkiem +SaddlebagsCarryPercent (na ladzie; postfiks DefaultInventoryCapacityModel.CalculateInventoryCapacity).
    ///    Osobisty odzysk strzal (16), wlasnej broni miotanej (23) i "-30% zuzycia" belty (20) / bron miotana (25a): pociski BOHATERA nie schodza w ogole
    ///    (DTE pomija bohaterow - ArmyArmory.cs:255/421, DynamicTroopMissionLogic.cs:163/439; FieldCraft nie obija broni miotanej) - nie ma do czego
    ///    przypiac, pominiete (decyzja Jeffa).
    /// D. 33 BREEDER: prefiks na latce ROT (ROT.HarmonyPatches.Dragon.FoodConsumptionBehaviorPatch.Prefix) - zamiast niej: tylko na ladzie, partia nie glodna,
    ///    co najmniej para TEGO SAMEGO gatunku (kon, wielblad, owca, krowa, swinia...; muly bezplodne), bez smokow, slonii, mamutow, rydwanow i unikatow;
    ///    mlode = sztuka rodzicow; mlode jedza jak reszta stada (WinterRealm.AnimalNeed liczy kazde zwierze w taborze).
    /// E. 44 BEASTWHISPERER: prefiks na ROT.CampaignBehaviors.ROTDragonBehavior.DailyTickParty - zamiast niej: ten sam teren (step, rownina) i perk zwiadowcy,
    ///    szansa x BeastWhispererChanceFactor, tylko zwykle konie wierzchowe i juczne (bez rumakow bojowych, egzotyki, smokow, unikatow, rzeczy nie na handel).
    /// Bledy liczone (potkniecia), bez wylacznika. Stan: tylko liczniki doby. Linie: "Perki amunicji i zwierzat (512): start sesji" i "... dzien N".
    /// </summary>
    internal static class Perk512
    {
        internal const string P = "Perki amunicji i zwierzat (512): ";
        private const string Owner = "com.jeff.armoury.perk512";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static Harmony _h;
        private static bool _ammoWired, _capWired, _breedWired, _whispWired;
        private static string _ammoHook = "nie wpieta", _capHook = "nie wpieta", _breedHook = "nie wpieta", _whispHook = "nie wpieta";
        private static int _stumbles, _dStumbles;

        // liczniki doby
        private static int _dEquipCalls, _dEquipReverted, _dEquipShots;
        private static int _dRecover, _dRecoverAi, _dXbowShots;
        private static int _dBreedTry, _dBreedSea, _dBreedHungry, _dBreedNoPair, _dBreedBirths, _dBreedEvents;
        private static int _dWhispTry, _dWhispFound;
        private static readonly Dictionary<string, int> _dBreedBy = new Dictionary<string, int>();

        private static Settings S { get { return Settings.Current; } }
        private static bool AmmoOn { get { var s = S; return s != null && s.AmmoPerksRework; } }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _dStumbles++;
            if (_stumbles <= 5) Log.Error("Perk512." + where + " (potkniecie " + _stumbles + ")", e);
        }

        // ------------------------------------------------------------ A. straz: dawne "+amunicja" z perkow

        [ThreadStatic] private static short[] _preAmt;
        [ThreadStatic] private static short[] _preMax;
        private static AccessTools.FieldRef<MissionEquipment, MissionWeapon[]> _slots;

        public static void EquipPrefix(Agent __0)
        {
            try
            {
                if (__0 == null || !__0.IsHuman || !AmmoOn) return;
                var eq = __0.Equipment;
                if (eq == null) return;
                if (_preAmt == null) { _preAmt = new short[5]; _preMax = new short[5]; }
                for (int i = 0; i < 5; i++)
                {
                    var w = eq[(EquipmentIndex)i];
                    _preAmt[i] = w.IsEmpty ? (short)-1 : w.Amount;
                    _preMax[i] = w.IsEmpty ? (short)-1 : w.ModifiedMaxAmount;
                }
            }
            catch (Exception e) { if (_preAmt != null) for (int i = 0; i < 5; i++) _preAmt[i] = -1; Stumble("EquipPrefix", e); }
        }

        public static void EquipPostfix(Agent __0)
        {
            try
            {
                if (__0 == null || !__0.IsHuman || !AmmoOn || _preAmt == null) return;
                _dEquipCalls++;
                var eq = __0.Equipment;
                if (eq == null) return;
                MissionWeapon[] arr = _slots != null ? _slots(eq) : null;
                for (int i = 0; i < 5; i++)
                {
                    short a0 = _preAmt[i], m0 = _preMax[i];
                    _preAmt[i] = -1;
                    if (a0 < 0) continue;
                    var idx = (EquipmentIndex)i;
                    var w = eq[idx];
                    if (w.IsEmpty || w.Amount <= a0) continue;
                    var u = w.CurrentUsageItem;
                    if (u == null || !u.IsConsumable || u.RelevantSkill == null) continue;
                    var sk = u.RelevantSkill;
                    if (sk != DefaultSkills.Bow && sk != DefaultSkills.Crossbow && sk != DefaultSkills.Throwing) continue;
                    // to, co gra dalaby BEZ perkow amunicji: baza x MilitaryPlanner (szturm, strona atakujaca) - ten sam warunek co SandboxAgentStatCalculateModel
                    int want = a0;
                    try
                    {
                        var pb = __0.Origin != null ? __0.Origin.BattleCombatant as PartyBase : null;
                        var me = pb != null ? pb.MapEvent : null;
                        var mp = pb != null && pb.IsMobile ? pb.MobileParty : null;
                        if (mp != null && me != null && me.AttackerSide == pb.MapEventSide && me.EventType == MapEvent.BattleTypes.Siege)
                        {
                            var en = new ExplainedNumber(a0, false, null);
                            PerkHelper.AddPerkBonusForParty(DefaultPerks.Engineering.MilitaryPlanner, mp, true, ref en);
                            want = MathF.Round(en.ResultNumber);
                        }
                    }
                    catch (Exception e) { Stumble("EquipPostfix.Planner", e); }
                    if (w.Amount <= want) continue;
                    int cut = w.Amount - want;
                    eq.SetAmountOfSlot(idx, (short)want, false);
                    // ModifiedMaxAmount: wszystko ponad stan sprzed metody gry, czego nie usprawiedliwia MilitaryPlanner, wraca do bazowego
                    if (arr != null && i < arr.Length)
                    {
                        int maxWant = Math.Max(m0, m0 + (want - a0));
                        int extra = arr[i].ModifiedMaxAmount - maxWant;
                        if (extra > 0) arr[i].AddExtraModifiedMaxValue((short)(-extra));
                    }
                    _dEquipReverted++; _dEquipShots += cut;
                }
            }
            catch (Exception e) { Stumble("EquipPostfix", e); }
        }

        // ------------------------------------------------------------ B. odzysk druzyny (perk wodza partii)

        /// <summary>Perk wodza partii dla rodzaju pocisku: strzaly - DeepQuivers, belty - Fletcher, bron miotana - WellPrepared; null - inny.</summary>
        private static PerkObject PartyPerkFor(ItemObject it)
        {
            if (it == null) return null;
            switch (it.ItemType)
            {
                case ItemObject.ItemTypeEnum.Arrows: return DefaultPerks.Bow.DeepQuivers;
                case ItemObject.ItemTypeEnum.Bolts: return DefaultPerks.Crossbow.Fletcher;
                case ItemObject.ItemTypeEnum.Thrown: return DefaultPerks.Throwing.WellPrepared;
                default: return null;
            }
        }

        private static float PartyBonus(MobileParty mp, ItemObject it)
        {
            var s = S;
            if (mp == null || s == null || !s.AmmoPerksRework || s.AmmoPerkPartyPercent <= 0f) return 0f;
            var perk = PartyPerkFor(it);
            if (perk == null || !mp.HasPerk(perk, true)) return 0f;   // true = druga rola perka (PartyLeader) - jak dawny drugi efekt
            return s.AmmoPerkPartyPercent / 100f;
        }

        /// <summary>AmmoRecovery.Postfix (bitwa gracza, agent DTE): odzysk po perku wodza partii agenta. Odzysk 0 (pole stracone) zostaje 0.</summary>
        internal static float Recover(float recover, Agent agent, ItemObject it)
        {
            try
            {
                if (recover <= 0f || agent == null) return recover;
                var pb = agent.Origin != null ? agent.Origin.BattleCombatant as PartyBase : null;
                var mp = pb != null && pb.IsMobile ? pb.MobileParty : null;
                float b = PartyBonus(mp, it);
                if (b <= 0f) return recover;
                _dRecover++;
                return Math.Min(1f, recover * (1f + b));
            }
            catch (Exception e) { Stumble("Recover", e); return recover; }
        }

        /// <summary>AiWear.AmmoUse (bitwa AI bez misji): ulamek pociskow, ktory schodzi - f liczone jak tam (natezenie x (zwyciezca: 1 - odzysk)).</summary>
        internal static float AiLoss(float f, float intensity, bool won, float keep, MobileParty mp, ItemObject it)
        {
            try
            {
                if (!won || keep <= 0f) return f;
                float b = PartyBonus(mp, it);
                if (b <= 0f) return f;
                _dRecoverAi++;
                return Math.Min(1f, intensity * (1f - Math.Min(1f, keep * (1f + b))));
            }
            catch (Exception e) { Stumble("AiLoss", e); return f; }
        }

        // ------------------------------------------------------------ C. osobiste: kusza (Fletcher), udzwig (Saddlebags)

        /// <summary>FieldCraft.OnAgentShootMissile: mnoznik zuzycia kuszy na strzal - bohater-strzelec z perkiem Fletcher: 1 - AmmoPerkOwnPercent/100.</summary>
        internal static float CrossbowWear(Agent shooter, ItemObject weapon)
        {
            try
            {
                var s = S;
                if (s == null || !s.AmmoPerksRework || s.AmmoPerkOwnPercent <= 0f || shooter == null || weapon == null) return 1f;
                if (weapon.ItemType != ItemObject.ItemTypeEnum.Crossbow) return 1f;
                var co = shooter.Character as CharacterObject;
                var h = co != null ? co.HeroObject : null;
                if (h == null || !h.GetPerkValue(DefaultPerks.Crossbow.Fletcher)) return 1f;
                _dXbowShots++;
                return Math.Max(0f, 1f - s.AmmoPerkOwnPercent / 100f);
            }
            catch (Exception e) { Stumble("CrossbowWear", e); return 1f; }
        }

        private static TextObject _capTxt;

        /// <summary>Postfiks DefaultInventoryCapacityModel.CalculateInventoryCapacity: __0 partia, __1 na morzu. Wodz z perkiem Saddlebags - +SaddlebagsCarryPercent udzwigu (na ladzie).</summary>
        public static void CapPostfix(MobileParty __0, bool __1, bool __6, ref ExplainedNumber __result)
        {
            try
            {
                var s = S;
                if (__0 == null || __1 || s == null || !s.AmmoPerksRework || s.SaddlebagsCarryPercent <= 0f) return;
                var h = __0.LeaderHero;
                if (h == null || !h.GetPerkValue(DefaultPerks.Throwing.Saddlebags)) return;
                if (_capTxt == null) _capTxt = DefaultPerks.Throwing.Saddlebags.Name;
                float pct = s.SaddlebagsCarryPercent / 100f;
                if (__6 && __0.AttachedParties != null && __0.AttachedParties.Count > 0)
                {
                    // udzwig liczony z doczepionymi partiami (armia): premia tylko od wlasnej czesci (wagi jak w grze: ludzie i konie x20, juczne x100)
                    float own = Share(__0.Party), all = own;
                    foreach (var ap in __0.AttachedParties) if (ap != null) all += Share(ap.Party);
                    if (all <= 0f) return;
                    __result.Add(__result.ResultNumber * pct * (own / all), _capTxt);
                    return;
                }
                __result.AddFactor(pct, _capTxt);
            }
            catch (Exception e) { Stumble("CapPostfix", e); }
        }

        private static float Share(PartyBase p)
        {
            if (p == null) return 0f;
            return p.NumberOfHealthyMembers * 20f + p.NumberOfMounts * 20f + p.NumberOfPackAnimals * 100f;
        }

        // ------------------------------------------------------------ D. 33 Breeder (zamiast latki ROT)

        /// <summary>Gatunek do rozmnazania; null - nie rozmnaza sie (smok, slon, mamut, rydwan, mul, unikat).</summary>
        private static string Species(ItemObject it)
        {
            if (it == null || !it.HasHorseComponent) return null;
            var hc = it.HorseComponent;
            if (hc == null || ArmsPricing.IsUnique(it)) return null;
            var mon = hc.Monster;
            string usage = mon != null ? mon.MonsterUsage : null;
            if (usage == "dragon" || usage == "dragonfly") return null;
            var fam = MountLaw.FamilyOf(it);
            if (fam == MountLaw.Fam.Dragon || fam == MountLaw.Fam.Elephant || fam == MountLaw.Fam.Mammoth || fam == MountLaw.Fam.Chariot) return null;
            if (fam == MountLaw.Fam.Camel) return "camel";
            string mid = mon != null && mon.StringId != null ? mon.StringId : "";
            string id = it.StringId ?? "";
            if (id.IndexOf("mule", StringComparison.OrdinalIgnoreCase) >= 0 || mid.IndexOf("mule", StringComparison.OrdinalIgnoreCase) >= 0) return null;   // mul jest bezplodny
            if (id.IndexOf("zorse", StringComparison.OrdinalIgnoreCase) >= 0) return null;   // zorse (ROT-Content) - mieszaniec, bezplodny jak mul
            if (hc.IsLiveStock) return "ls:" + (it.ItemCategory != null ? it.ItemCategory.StringId : mid);
            // konie wedlug kategorii: zwykly ze zwyklym, juczny z jucznym, bojowy z bojowym, szlachetny ze szlachetnym (recenzja 512: para zwykly +
            // szlachetny nie rodzi szlachetnego z niczego)
            if (fam == MountLaw.Fam.Horse) return "horse:" + (hc.IsPackAnimal ? "pack" : (it.ItemCategory != null ? it.ItemCategory.StringId : "?"));
            return mid.Length > 0 ? "m:" + mid : null;
        }

        /// <summary>Prefiks na ROT FoodConsumptionBehaviorPatch.Prefix(MobileParty): __result false = gra nie biegnie (jak u ROT), return false = cialo ROT nie biegnie.</summary>
        public static bool BreedPrefix(MobileParty __0, ref bool __result)
        {
            var s = S;
            if (s == null || !s.BreederRealistic) return true;   // klucz wylaczony - latka ROT jak dotad
            __result = false;
            try { Breed(__0); }
            catch (Exception e) { Stumble("Breed", e); }
            return false;
        }

        private static void Breed(MobileParty party)
        {
            if (party == null || party.ItemRoster == null) return;
            if (!(MBRandom.RandomFloat < DefaultPerks.Riding.Breeder.PrimaryBonus) || !party.HasPerk(DefaultPerks.Riding.Breeder, false)) return;
            _dBreedTry++;
            if (party.IsCurrentlyAtSea) { _dBreedSea++; return; }
            if (party.Party != null && party.Party.IsStarving) { _dBreedHungry++; return; }   // stado nie dojada - nie rodzi
            var ir = party.ItemRoster;
            var bySp = new Dictionary<string, int>();
            for (int i = 0; i < ir.Count; i++)
            {
                var el = ir[i];
                if (el.Amount <= 0) continue;
                string sp = Species(el.EquipmentElement.Item);
                if (sp == null) continue;
                int n; bySp.TryGetValue(sp, out n); bySp[sp] = n + el.Amount;
            }
            int total = 0;
            foreach (var kv in bySp) if (kv.Value >= 2) total += kv.Value;
            if (total <= 0) { _dBreedNoPair++; return; }
            // gatunek losowany wagowo liczebnoscia (tylko gatunki z para)
            int r = MBRandom.RandomInt(total);
            string pick = null; int cnt = 0;
            foreach (var kv in bySp)
            {
                if (kv.Value < 2) continue;
                if (r < kv.Value) { pick = kv.Key; cnt = kv.Value; break; }
                r -= kv.Value;
            }
            if (pick == null) return;
            // mlode = sztuka rodzicow (losowana wagowo wsrod sztuk gatunku)
            int r2 = MBRandom.RandomInt(cnt);
            ItemObject child = null;
            for (int i = 0; i < ir.Count; i++)
            {
                var el = ir[i];
                if (el.Amount <= 0) continue;
                var it = el.EquipmentElement.Item;
                if (Species(it) != pick) continue;
                if (r2 < el.Amount) { child = it; break; }
                r2 -= el.Amount;
            }
            if (child == null) return;
            int births = MathF.Round(MathF.Max(1f, cnt / 50f));   // ROT/gra: liczebnosc/50, tu liczebnosc GATUNKU
            births = Math.Max(1, Math.Min(births, cnt / 2));       // najwyzej jedno mlode na pare
            ir.AddToCounts(child, births);
            _dBreedEvents++; _dBreedBirths += births;
            int b; _dBreedBy.TryGetValue(pick, out b); _dBreedBy[pick] = b + births;
            if (party.IsMainParty)
            {
                var t = new TextObject("{=vl9bawa7}{COUNT} {?(COUNT > 1)}{PLURAL(ANIMAL_NAME)} are{?}{ANIMAL_NAME} is{\\?} added to your party.");
                t.SetTextVariable("COUNT", births);
                t.SetTextVariable("ANIMAL_NAME", child.Name);
                InformationManager.DisplayMessage(new InformationMessage(t.ToString()));
            }
        }

        // ------------------------------------------------------------ E. 44 BeastWhisperer (zamiast ROTDragonBehavior.DailyTickParty)

        private static List<ItemObject> _plainMounts;

        /// <summary>Zwykly kon wierzchowy albo juczne: na handel, bez unikatow, bez rumakow bojowych/szlachetnych, egzotyki i smokow, bez zywca.</summary>
        private static bool PlainMount(ItemObject it)
        {
            if (it == null || !it.HasHorseComponent || it.NotMerchandise || ArmsPricing.IsUnique(it)) return false;
            var hc = it.HorseComponent;
            if (hc == null || hc.IsLiveStock) return false;
            var mon = hc.Monster;
            string usage = mon != null ? mon.MonsterUsage : null;
            if (usage == "dragon" || usage == "dragonfly") return false;
            var fam = MountLaw.FamilyOf(it);
            if (fam != MountLaw.Fam.Horse && fam != MountLaw.Fam.Other) return false;   // wielblad, rydwan, slon, mamut, smok
            if (hc.IsPackAnimal) return true;
            return it.ItemCategory == DefaultItemCategories.Horse;   // tylko zwykly kon wierzchowy (bez WarHorse / NobleHorse)
        }

        public static bool WhispPrefix(MobileParty __0)
        {
            var s = S;
            if (s == null || !s.BeastWhispererPlainMounts) return true;   // klucz wylaczony - ROT jak dotad
            try { Whisper(__0, s); }
            catch (Exception e) { Stumble("Whisper", e); }
            return false;
        }

        private static void Whisper(MobileParty party, Settings s)
        {
            if (party == null || party.ItemRoster == null) return;
            if (!(MBRandom.RandomFloat < DefaultPerks.Scouting.BeastWhisperer.PrimaryBonus * Math.Max(0f, s.BeastWhispererChanceFactor))) return;
            if (!party.HasPerk(DefaultPerks.Scouting.BeastWhisperer, false)) return;
            var tt = Campaign.Current.MapSceneWrapper.GetFaceTerrainType(party.CurrentNavigationFace);
            if (tt != TerrainType.Steppe && tt != TerrainType.Plain) return;
            _dWhispTry++;
            if (_plainMounts == null)
            {
                var l = new List<ItemObject>();
                foreach (var it in TaleWorlds.CampaignSystem.Extensions.Items.All) { try { if (PlainMount(it)) l.Add(it); } catch { } }
                _plainMounts = l;
            }
            if (_plainMounts.Count == 0) return;
            var pick = _plainMounts[MBRandom.RandomInt(_plainMounts.Count)];
            party.ItemRoster.AddToCounts(pick, 1);
            _dWhispFound++;
            if (party.IsMainParty)
            {
                var t = new TextObject("{=vl9bawa7}{COUNT} {?(COUNT > 1)}{PLURAL(ANIMAL_NAME)} are{?}{ANIMAL_NAME} is{\\?} added to your party.");
                t.SetTextVariable("COUNT", 1);
                t.SetTextVariable("ANIMAL_NAME", pick.Name);
                InformationManager.DisplayMessage(new InformationMessage(t.ToString()));
            }
        }

        // ------------------------------------------------------------ wpiecie (OnSessionLaunched, raz na proces) i linie logu

        internal static void EnsureHooks()
        {
            ResetDay();
            // obiekty przedmiotow i napisy z poprzedniej gry w tym samym procesie sa niewazne (recenzja 512) - pula buduje sie od nowa
            _plainMounts = null;
            _capTxt = null;
            try
            {
                if (_h == null) _h = new Harmony(Owner);
                if (!_ammoWired)
                {
                    var m = AccessTools.DeclaredMethod(typeof(SandboxAgentStatCalculateModel), "InitializeMissionEquipment", new[] { typeof(Agent) });
                    try { _slots = AccessTools.FieldRefAccess<MissionEquipment, MissionWeapon[]>("_weaponSlots"); } catch (Exception e) { _slots = null; Stumble("EnsureHooks._weaponSlots", e); }
                    if (m == null) _ammoHook = "BRAK SandboxAgentStatCalculateModel.InitializeMissionEquipment";
                    else
                    {
                        _h.Patch(m, prefix: new HarmonyMethod(typeof(Perk512), nameof(EquipPrefix)), postfix: new HarmonyMethod(typeof(Perk512), nameof(EquipPostfix)) { priority = Priority.Last });
                        _ammoWired = true;
                        _ammoHook = "wpieta" + (_slots != null ? "" : " (bez _weaponSlots - ModifiedMaxAmount zostaje)");
                    }
                }
                if (!_capWired)
                {
                    var cap = AccessTools.DeclaredMethod(typeof(DefaultInventoryCapacityModel), "CalculateInventoryCapacity");
                    if (cap == null) _capHook = "BRAK CalculateInventoryCapacity";
                    else { _h.Patch(cap, postfix: new HarmonyMethod(typeof(Perk512), nameof(CapPostfix))); _capWired = true; _capHook = "wpieta"; }
                }
                if (!_breedWired)
                {
                    var t = AccessTools.TypeByName("ROT.HarmonyPatches.Dragon.FoodConsumptionBehaviorPatch");
                    var m = t != null ? AccessTools.DeclaredMethod(t, "Prefix") : null;
                    if (m == null) _breedHook = "BRAK latki ROT FoodConsumptionBehaviorPatch.Prefix";
                    else { _h.Patch(m, prefix: new HarmonyMethod(typeof(Perk512), nameof(BreedPrefix)) { priority = Priority.First }); _breedWired = true; _breedHook = "wpieta"; }
                }
                if (!_whispWired)
                {
                    var t = AccessTools.TypeByName("ROT.CampaignBehaviors.ROTDragonBehavior");
                    var m = t != null ? AccessTools.DeclaredMethod(t, "DailyTickParty") : null;
                    if (m == null) _whispHook = "BRAK ROT ROTDragonBehavior.DailyTickParty";
                    else { _h.Patch(m, prefix: new HarmonyMethod(typeof(Perk512), nameof(WhispPrefix)) { priority = Priority.First }); _whispWired = true; _whispHook = "wpieta"; }
                }
            }
            catch (Exception e) { Stumble("EnsureHooks", e); }
            var s = S;
            Log.Info(P + "start sesji | AmmoPerksRework " + (s != null && s.AmmoPerksRework ? "TAK" : "NIE")
                     + " (odzysk druzyny +" + (s != null ? s.AmmoPerkPartyPercent.ToString("0.#", Inv) : "?") + "%, kusza Fletcher -" + (s != null ? s.AmmoPerkOwnPercent.ToString("0.#", Inv) : "?")
                     + "% zuzycia, Saddlebags udzwig +" + (s != null ? s.SaddlebagsCarryPercent.ToString("0.#", Inv) : "?") + "%)"
                     + " | straz dawnej amunicji (Sandbox InitializeMissionEquipment) " + _ammoHook
                     + " | udzwig " + _capHook
                     + " | Breeder " + (s != null && s.BreederRealistic ? "TAK" : "NIE") + " - " + _breedHook
                     + " | BeastWhisperer " + (s != null && s.BeastWhispererPlainMounts ? "TAK" : "NIE") + " x" + (s != null ? s.BeastWhispererChanceFactor.ToString("0.##", Inv) : "?") + " - " + _whispHook
                     + " | potkniecia " + _stumbles + ".");
        }

        private static void ResetDay()
        {
            _dEquipCalls = _dEquipReverted = _dEquipShots = 0;
            _dRecover = _dRecoverAi = _dXbowShots = 0;
            _dBreedTry = _dBreedSea = _dBreedHungry = _dBreedNoPair = _dBreedBirths = _dBreedEvents = 0;
            _dWhispTry = _dWhispFound = 0;
            _dBreedBy.Clear();
            _dStumbles = 0;
        }

        internal static void Daily(int day)
        {
            try
            {
                var by = new System.Text.StringBuilder();
                foreach (var kv in _dBreedBy) { if (by.Length > 0) by.Append(", "); by.Append(kv.Key).Append(' ').Append(kv.Value); }
                Log.Info(P + "dzien " + day
                         + " | straz amunicji: wywolan metody gry " + _dEquipCalls + " (0 = ROT jej nie wola - dawna premia martwa), cofniete sloty " + _dEquipReverted + " (" + _dEquipShots + " szt.)"
                         + " | odzysk z perkiem wodza: agenci bitwy gracza " + _dRecover + ", pozycje AI " + _dRecoverAi
                         + " | strzaly z kuszy z Fletcher " + _dXbowShots
                         + " | Breeder: proby " + _dBreedTry + ", morze " + _dBreedSea + ", glod " + _dBreedHungry + ", bez pary " + _dBreedNoPair
                         + ", porody " + _dBreedEvents + " (" + _dBreedBirths + " szt." + (by.Length > 0 ? ": " + by : "") + ")"
                         + " | BeastWhisperer: proby " + _dWhispTry + ", znalezione " + _dWhispFound + ", pula " + (_plainMounts != null ? _plainMounts.Count.ToString(Inv) : "-")
                         + " | potkniecia doby " + _dStumbles + " (sesja " + _stumbles + ").");
            }
            catch (Exception e) { Stumble("Daily", e); }
            ResetDay();
        }
    }
}
