using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// Z16 - WYMOGI SPRZETU U BOHATEROW (Jeff 09.10, 02:50: "jak nie mam danej umiejetnosci, np. atletyki, nie moge zalozyc
    /// pancerza, ktory ma takie wymaganie"; zasada 29.08 "CALY ekwipunek"; SPROSTOWANIE 03:05). Gra pilnuje u bohatera tylko
    /// przedmiotow z umiejetnoscia w danych (Helpers.CharacterHelper.CanUseItem, 1.4.8 :566-587 sprawdza RelevantSkill + Difficulty):
    /// bron, tarcze, konie ORAZ amunicje - strzaly i belty maja RelevantSkill z klasy broni (TaleWorlds.Core
    /// WeaponComponentData.GetRelevantSkillFromWeaponClass: Arrow -> Bow, Bolt -> Crossbow; POPRAWKA Z16-5 po recenzji - wczesniej
    /// opis mowil, ze amunicja przechodzila). Przechodzil tylko PANCERZ (RelevantSkill = null), wiec gracz i towarzysze zakladali
    /// kazda zbroje. Etap 2 (ekran): postfiks na CanUseItem (3-arg - 2-arg wola 3-arg) dodaje brakujacy wiersz tej samej reguly
    /// co u zolnierzy: pancerz - Atletyka (ItemReq.MeetsHero, ladry przepuszczone - decyzja, patrz ItemReq.MeetsHero); strzaly i belty pilnuje sama gra.
    /// Ekran (SPInventoryVM.IsItemEquipmentPossible / CanCharacterUseItem) zrobi z tego sam czerwona karte i komunikat gry
    /// "You don't have enough {SKILL_NAME} skill to equip this item". Nigdy "nie" -> "tak". Opis przedmiotu (ItemMenuVM)
    /// dostaje linie "Requires: Athletics 175" w tym samym miejscu, co gra pisze wymog broni (po wadze). To, co bohater juz
    /// nosi, zostaje (decyzja 3) - blokada tylko przy zakladaniu; Reset/Cancel ekranu przywraca stan bez sprawdzania.
    /// Wylacznik: HeroGearRequirements (czytany na zywo).
    /// </summary>
    internal static class HeroGear
    {
        private static System.Reflection.FieldInfo _fTarget, _fCompared, _fCharacter;

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var m = AccessTools.Method(typeof(global::Helpers.CharacterHelper), "CanUseItem",
                    new[] { typeof(BasicCharacterObject), typeof(EquipmentElement), typeof(TextObject).MakeByRefType() });
                if (m != null) h.Patch(m, postfix: new HarmonyMethod(typeof(HeroGear), nameof(CanUsePostfix)));
                Log.Info("Z16: ekran ekwipunku " + (m != null
                    ? "pilnuje pancerza (Athletics) u bohaterow - ta sama regula co u zolnierzy; strzaly (Bow) i belty (Crossbow) pilnuje sama gra."
                    : "- CharacterHelper.CanUseItem(3) NIEZNALEZIONE, sito ekranu spi."));
            }
            catch (Exception e) { Log.Error("HeroGear.ApplyAll(CanUseItem)", e); }
            try
            {
                var t = AccessTools.Method(typeof(ItemMenuVM), "SetGeneralComponentTooltip");
                _fTarget = AccessTools.Field(typeof(ItemMenuVM), "_targetItem");
                _fCompared = AccessTools.Field(typeof(ItemMenuVM), "_comparedItem");
                _fCharacter = AccessTools.Field(typeof(ItemMenuVM), "_character");
                if (t != null && _fTarget != null && _fCharacter != null)
                    h.Patch(t, postfix: new HarmonyMethod(typeof(HeroGear), nameof(TooltipPostfix)));
                else Log.Info("Z16: ItemMenuVM.SetGeneralComponentTooltip / pola NIEZNALEZIONE - opis przedmiotu bez linii Requires dla pancerza.");
            }
            catch (Exception e) { Log.Error("HeroGear.ApplyAll(ItemMenuVM)", e); }
            ApplySpoils(h);
            ApplyGank(h);
        }

        /// <summary>Sztuka, ktorej wymog pilnuje tylko nasza regula (gra nie ma dla niej RelevantSkill): pancerz bez ladr.
        /// Bron, tarcze, konie, strzaly i belty (RelevantSkill Bow/Crossbow z klasy broni) pilnuje gra - tym samym wierszem
        /// i komunikatem; dla nich warunek RelevantSkill != null zwraca false (Z16-5: opis poprawiony, dzialanie bez zmian).</summary>
        internal static bool OursOnly(ItemObject it)
        {
            if (it == null || it.RelevantSkill != null) return false;
            if (it.ItemType == ItemObject.ItemTypeEnum.HorseHarness) return false;
            return ItemReq.SkillFor(it) != null;
        }

        /// <summary>Postfiks CharacterHelper.CanUseItem(BasicCharacterObject, EquipmentElement, out TextObject).</summary>
        public static void CanUsePostfix(BasicCharacterObject __0, EquipmentElement __1, ref TextObject __2, ref bool __result)
        {
            try
            {
                if (!__result) return;                               // nigdy "nie" -> "tak"
                var s = Settings.Current;
                if (s == null || !s.HeroGearRequirements) return;
                var it = __1.Item;
                if (!OursOnly(it)) return;
                var co = __0 as CharacterObject;
                if (co == null || ItemReq.MeetsHero(co, it)) return;
                __2 = WhyFor(ItemReq.SkillFor(it));
                __result = false;
            }
            catch { }
        }

        // Z16-5 (recenzja, wydajnosc): jeden gotowy powod na umiejetnosc - ekran wola CanUseItem dla kazdej karty przy kazdej
        // zmianie postaci, a wersja 2-arg powod i tak wyrzuca; SKILL_NAME to TextObject nazwy, wiec jezyk liczy sie przy ToString
        private static readonly Dictionary<SkillObject, TextObject> _why = new Dictionary<SkillObject, TextObject>();
        private static TextObject WhyFor(SkillObject sk)
        {
            lock (_why)
            {
                TextObject t;
                if (!_why.TryGetValue(sk, out t))
                {
                    t = new TextObject("{=rgqA29b8}You don't have enough {SKILL_NAME} skill to equip this item");
                    t.SetTextVariable("SKILL_NAME", sk.Name);
                    _why[sk] = t;
                }
                return t;
            }
        }

        private static string _requires;
        private static string RequiresText()
        {
            if (_requires == null)
            {
                try { _requires = new TextObject("{=154a34f8caccfc833238cc89d38861e8}Requires: ").ToString(); }
                catch { _requires = "Requires: "; }
            }
            return _requires;
        }

        /// <summary>Postfiks ItemMenuVM.SetGeneralComponentTooltip (wolane z RefreshItemTooltips - przy SetItem i przy
        /// zmianie uzycia broni): linia "Requires: Athletics 175" dla pancerza (strzalom i beltom "Requires: Bow N" /
        /// "Crossbow N" pisze sama gra - maja RelevantSkill),
        /// zaraz po wadze - tam, gdzie gra pisze wymog broni (AddSkillRequirement). Kolor jak w grze: zielony, gdy
        /// CanUseItem (juz z naszym postfiksem) mowi "moze", inaczej czerwony; porownywana sztuka - tak samo, bez napisu.</summary>
        public static void TooltipPostfix(ItemMenuVM __instance)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.HeroGearRequirements || __instance == null) return;
                var target = _fTarget.GetValue(__instance) as ItemVM;
                if (target == null) return;
                var ti = target.ItemRosterElement.EquipmentElement.Item;
                if (!OursOnly(ti)) return;
                ItemVM cmp = null;
                if (__instance.IsComparing && _fCompared != null) cmp = _fCompared.GetValue(__instance) as ItemVM;
                var ci = cmp != null ? cmp.ItemRosterElement.EquipmentElement.Item : null;
                bool cmpOurs = OursOnly(ci) && ci.Difficulty > 0;
                if (ti.Difficulty <= 0 && !cmpOurs) return;
                var ch = _fCharacter.GetValue(__instance) as BasicCharacterObject;
                AddRow(__instance.TargetItemProperties, target, ch, false);
                if (cmp != null) AddRow(__instance.ComparedItemProperties, cmp, ch, true);
            }
            catch (Exception e) { Log.Error("HeroGear.Tooltip", e); }
        }

        private static void AddRow(MBBindingList<ItemMenuTooltipPropertyVM> list, ItemVM vm, BasicCharacterObject ch, bool comparison)
        {
            if (list == null || vm == null) return;
            var el = vm.ItemRosterElement.EquipmentElement;
            var it = el.Item;
            string value = "";
            if (it != null && OursOnly(it) && it.Difficulty > 0) value = ItemReq.SkillFor(it).Name + " " + it.Difficulty;
            bool ok = ch == null || it == null || global::Helpers.CharacterHelper.CanUseItem(ch, el);
            list.Add(new ItemMenuTooltipPropertyVM(comparison ? "" : RequiresText(), value, 0,
                ok ? TaleWorlds.CampaignSystem.ViewModelCollection.UIColors.PositiveIndicator : TaleWorlds.CampaignSystem.ViewModelCollection.UIColors.NegativeIndicator, false, null, TooltipProperty.TooltipPropertyFlags.None));
        }

        // ---------------------------------------------------------------- Z16-3: Spoils "Auto-equip companions"
        // Spoils of War (RealisticLoot.Models.AutoEquipPlanner.TryUpgradeSlot, Spoils 1.8.4, dekompilacja :85-163) wybiera towarzyszowi
        // najlepsza sztuke z taboru gracza BEZ zadnej kontroli umiejetnosci (nawet broni) - plyta t6 z taboru wchodzila na
        // kazdego. PREFIKS podmienia tabor (argument 0) na kopie z samymi sztukami pasujacymi do slotu, ktore TEN towarzysz
        // udzwignie (ItemReq.MeetsHero); Spoils wybiera i zapisuje na kopii; POSTFIKS oddaje prawdziwemu taborowi to samo, co
        // Spoils zrobil na kopii (wybrana sztuka -1, zdjeta +1). Bez ukrytych sztuk w tym slocie - Spoils pracuje na prawdziwym
        // taborze jak dotad.
        // Podwojenie sztuki cywilnej (:154-157, opcja UpdateCivilianEquipment) to osobna sprawa (projekt Z16 uwaga 7.3).
        internal sealed class SpoilsSlotState
        {
            internal ItemRoster Real;
            internal Hero Who;
            internal EquipmentIndex Slot;
            internal EquipmentElement Old;
        }

        private static int _spoilsHidden, _spoilsUpgrades, _spoilsCalls;

        private static void ApplySpoils(Harmony h)
        {
            try
            {
                var t = AccessTools.TypeByName("RealisticLoot.Models.AutoEquipPlanner");
                if (t == null) { Log.Info("Z16: Spoils of War (AutoEquipPlanner) nieobecny - auto-ekwipunek towarzyszy bez zmian."); return; }
                var m = AccessTools.Method(t, "TryUpgradeSlot");
                var ex = AccessTools.Method(t, "Execute");
                var ps = m != null ? m.GetParameters() : null;
                if (m == null || ps.Length < 3 || ps[0].ParameterType != typeof(ItemRoster) || ps[1].ParameterType != typeof(Hero)
                    || ps[2].ParameterType != typeof(EquipmentIndex) || m.ReturnType != typeof(bool))
                {
                    Log.Info("Z16: Spoils AutoEquipPlanner.TryUpgradeSlot - inna sygnatura niz w Spoils 1.8.4, sito auto-ekwipunku spi.");
                    return;
                }
                h.Patch(m, prefix: new HarmonyMethod(typeof(HeroGear), nameof(SpoilsPrefix)), postfix: new HarmonyMethod(typeof(HeroGear), nameof(SpoilsPostfix)));
                if (ex != null) h.Patch(ex, prefix: new HarmonyMethod(typeof(HeroGear), nameof(SpoilsRunStart)), postfix: new HarmonyMethod(typeof(HeroGear), nameof(SpoilsRunEnd)));
                Log.Info("Z16: Spoils 'Auto-equip companions' - towarzysz dostaje z taboru tylko to, co udzwignie (ItemReq.MeetsHero).");
            }
            catch (Exception e) { Log.Error("HeroGear.ApplySpoils", e); }
        }

        public static void SpoilsRunStart() { _spoilsHidden = 0; _spoilsUpgrades = 0; _spoilsCalls = 0; }

        public static void SpoilsRunEnd()
        {
            try
            {
                if (_spoilsCalls > 0)
                    Log.Info("Z16: Spoils auto-equip - " + _spoilsUpgrades + " zmian sprzetu przez sito (sloty z ukrytymi sztukami: " + _spoilsCalls
                             + ", ukrytych sztuk ponad umiejetnosc lacznie " + _spoilsHidden + ").");
            }
            catch { }
        }

        public static void SpoilsPrefix(ref ItemRoster __0, Hero __1, EquipmentIndex __2, out SpoilsSlotState __state)
        {
            __state = null;
            try
            {
                var s = Settings.Current;
                if (s == null || !s.HeroGearRequirements || __0 == null || __1 == null || __1.CharacterObject == null || __1.BattleEquipment == null) return;
                var real = __0;
                var co = __1.CharacterObject;
                // Z16-5 (recenzja, wydajnosc): Spoils bierze pod uwage tylko sztuki pasujace do slotu (Equipment.IsItemFitsToSlot,
                // dekompilacja :126) - wiec liczymy i kopiujemy tylko je; kopia powstaje, gdy w TYM slocie jest cos do ukrycia
                int hidden = 0;
                for (int i = 0; i < real.Count; i++)
                {
                    var el = real.GetElementCopyAtIndex(i);
                    var it = el.EquipmentElement.Item;
                    if (el.Amount > 0 && it != null && Equipment.IsItemFitsToSlot(__2, it) && !ItemReq.MeetsHero(co, it)) hidden += el.Amount;
                }
                if (hidden == 0) return;
                var sieve = new ItemRoster();
                for (int i = 0; i < real.Count; i++)
                {
                    var el = real.GetElementCopyAtIndex(i);
                    var it = el.EquipmentElement.Item;
                    if (el.Amount > 0 && it != null && Equipment.IsItemFitsToSlot(__2, it) && ItemReq.MeetsHero(co, it))
                        sieve.AddToCounts(el.EquipmentElement, el.Amount);
                }
                __state = new SpoilsSlotState { Real = real, Who = __1, Slot = __2, Old = __1.BattleEquipment[__2] };
                __0 = sieve;
                _spoilsHidden += hidden;
                _spoilsCalls++;
            }
            catch (Exception e) { __state = null; Log.Error("HeroGear.SpoilsPrefix", e); }
        }

        public static void SpoilsPostfix(bool __result, SpoilsSlotState __state)
        {
            if (__state == null || !__result) return;
            try
            {
                var now = __state.Who.BattleEquipment[__state.Slot];
                if (!now.IsEmpty) __state.Real.AddToCounts(now, -1);
                if (!__state.Old.IsEmpty) __state.Real.AddToCounts(__state.Old, 1);
                _spoilsUpgrades++;
            }
            catch (Exception e) { Log.Error("HeroGear.SpoilsPostfix", e); }
        }

        // ---------------------------------------------------------------- Z16-4: ROT - bron zabrana jencowi
        // ROT (ROT.CampaignBehaviors.ROTGankBehavior.TakeWeapon :109-138): gracz zabiera pojmanemu bohaterowi legendarna bron,
        // a jeniec dostaje w ten slot losowa bron tego typu z GetRandomItem (:196-275, kultura jenca albo neutralna, potem
        // dowolna) - bez patrzenia na umiejetnosc. POSTFIKS (metoda wola sama siebie z anyCulture - postfiks idzie po obu):
        // gdy wylosowanej jeniec nie udzwignie (ItemReq.MeetsHero) - losujemy JAK ROT (ta sama waga 1/(max(100, cena) + 100),
        // czyli tansze czesciej, i losowy modyfikator z grupy przedmiotu), tylko wsrod broni tego typu, ktore udzwignie, z tych
        // samych zrodel co ROT (kultura jenca albo neutralna, potem dowolna; bez listy broni specjalnych ROT i skradzionych, bez
        // unikatow i legend). POPRAWKA Z16-5 (recenzja): dotad brana byla NAJLEPSZA dozwolona (tier, cena) - jeniec dostawal
        // systematycznie lepsza bron, niz dalby mu ROT. Gdy zadnej nie udzwignie - zostaje wylosowana (lorda nie rozbrajamy).
        private static System.Reflection.FieldInfo _fGankSpecial, _fGankStolen;

        private static void ApplyGank(Harmony h)
        {
            try
            {
                var t = AccessTools.TypeByName("ROT.CampaignBehaviors.ROTGankBehavior");
                if (t == null) { Log.Info("Z16: ROTGankBehavior nieobecny - zamiennik broni jenca bez zmian."); return; }
                var m = AccessTools.Method(t, "GetRandomItem");
                var ps = m != null ? m.GetParameters() : null;
                if (m == null || ps.Length < 2 || ps[0].ParameterType != typeof(CharacterObject)
                    || ps[1].ParameterType != typeof(ItemObject.ItemTypeEnum) || m.ReturnType != typeof(EquipmentElement))
                {
                    Log.Info("Z16: ROTGankBehavior.GetRandomItem - inna sygnatura niz w ROT 8.1.8, sito zamiennika broni jenca spi.");
                    return;
                }
                _fGankSpecial = AccessTools.Field(t, "_specialWeapons");
                _fGankStolen = AccessTools.Field(t, "_stolenItems");
                h.Patch(m, postfix: new HarmonyMethod(typeof(HeroGear), nameof(GankPostfix)));
                Log.Info("Z16: ROT - jeniec, ktoremu zabierzesz legendarna bron, dostaje zamiennik w granicy swojej umiejetnosci.");
            }
            catch (Exception e) { Log.Error("HeroGear.ApplyGank", e); }
        }

        public static void GankPostfix(object __instance, CharacterObject __0, ItemObject.ItemTypeEnum __1, ref EquipmentElement __result)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.HeroGearRequirements || __0 == null) return;
                var got = __result.Item;
                if (got == null || ItemReq.MeetsHero(__0, got)) return;
                var special = _fGankSpecial != null ? _fGankSpecial.GetValue(__instance) as List<string> : null;
                var stolen = _fGankStolen != null ? _fGankStolen.GetValue(__instance) as List<ItemObject> : null;
                ItemObject pick = RandomGankItem(__0, __1, special, stolen, false) ?? RandomGankItem(__0, __1, special, stolen, true);
                if (pick == null)
                {
                    Log.Info("Z16: ROT - jeniec " + __0.StringId + " nie udzwignie " + got.StringId + ", a lzejszej broni tego typu brak - zostaje przy niej.");
                    return;
                }
                ItemModifier mod = null;
                try
                {
                    var grp = pick.ItemComponent != null ? pick.ItemComponent.ItemModifierGroup : null;
                    if (grp != null) mod = grp.GetRandomItemModifierProductionScoreBased();   // jak ROT GetRandomItem
                }
                catch { mod = null; }
                Log.Info("Z16: ROT - jeniec " + __0.StringId + " nie udzwignie " + got.StringId + " - dostaje " + pick.StringId
                         + " (losowo jak ROT, w granicy umiejetnosci).");
                __result = new EquipmentElement(pick, mod);
            }
            catch (Exception e) { Log.Error("HeroGear.GankPostfix", e); }
        }

        /// <summary>Z16-5: losowanie jak ROT GetRandomItem (:196-275) - jedno przejscie, kazda pasujaca sztuka z waga
        /// 1/(max(100, Value) + 100) (losowanie "rezerwuarowe" jak w ROT) - tylko wsrod sztuk, ktore jeniec udzwignie.</summary>
        private static ItemObject RandomGankItem(CharacterObject ch, ItemObject.ItemTypeEnum type, List<string> special, List<ItemObject> stolen, bool anyCulture)
        {
            ItemObject pick = null;
            float sum = 0f;
            foreach (var it in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
            {
                if (it == null || it.ItemType != type || it.Culture == null || it.StringId == null) continue;
                if (!anyCulture && it.Culture != ch.Culture && it.Culture.StringId != "neutral_culture") continue;
                if (special != null && special.Contains(it.StringId)) continue;
                if (stolen != null && stolen.Contains(it)) continue;
                if (UniqueGear.Is(it) || LegendaryLaw.IsLegend(it)) continue;
                if (!ItemReq.MeetsHero(ch, it)) continue;
                float w = 1f / (Math.Max(100, it.Value) + 100f);
                if (MBRandom.RandomFloat * (sum + w) >= sum) pick = it;
                sum += w;
            }
            return pick;
        }

        /// <summary>Samotest przy wczytaniu (plan testu Z16 pkt 4): 500 losowych par (bohater, sztuka z wymogiem) -
        /// wynik CanUseItem po postfiksie musi sie zgadzac z ItemReq.MeetsHero (plus warunki gry niezalezne od
        /// umiejetnosci: plec, sztandar smoka, kon nie pod siodlo). Tylko log.</summary>
        internal static void SelfTest()
        {
            try
            {
                var heroes = new List<Hero>();
                foreach (var h in Hero.AllAliveHeroes)
                    if (h != null && h.CharacterObject != null && !h.IsChild && !h.IsNotable) heroes.Add(h);
                var items = new List<ItemObject>();
                foreach (var it in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
                    if (it != null && it.Difficulty > 0 && ItemReq.SkillFor(it) != null) items.Add(it);
                if (heroes.Count == 0 || items.Count == 0) { Log.Info("Z16 samotest: brak bohaterow albo sztuk z wymogiem - pominiety."); return; }
                bool on = Settings.Current != null && Settings.Current.HeroGearRequirements;
                var rnd = new Random(1609);
                int n = 500, agree = 0, ours = 0, refused = 0;
                var bad = new List<string>();
                for (int i = 0; i < n; i++)
                {
                    var h = heroes[rnd.Next(heroes.Count)];
                    var it = items[rnd.Next(items.Count)];
                    var co = h.CharacterObject;
                    TextObject why;
                    bool got = global::Helpers.CharacterHelper.CanUseItem(co, new EquipmentElement(it), out why);
                    bool rule = OursOnly(it) ? (!on || ItemReq.MeetsHero(co, it)) : ItemReq.MeetsHero(co, it);
                    bool other = GameOtherOk(co, it);
                    bool want = rule && other;
                    if (OursOnly(it)) ours++;
                    if (!got) refused++;
                    if (got == want) agree++;
                    else if (bad.Count < 5)
                        bad.Add(h.StringId + "/" + it.StringId + " (" + ItemReq.SkillFor(it).StringId + " " + co.GetSkillValue(ItemReq.SkillFor(it))
                                + " vs " + it.Difficulty + "): ekran " + (got ? "TAK" : "NIE") + ", regula " + (want ? "TAK" : "NIE"));
                }
                Log.Info("Z16 samotest: CanUseItem zgodny z ItemReq.MeetsHero w " + agree + "/" + n + " par (w tym pancerz - wiersz Z16 - "
                         + ours + ", odmowy " + refused + ", sito " + (on ? "wlaczone" : "WYLACZONE") + ")"
                         + (bad.Count > 0 ? " - NIEZGODNE: " + string.Join("; ", bad.ToArray()) : "") + ".");
            }
            catch (Exception e) { Log.Error("HeroGear.SelfTest", e); }
        }

        /// <summary>Warunki CanUseItem niezalezne od umiejetnosci (gra 1.4.8, CharacterHelper.cs:578-585).</summary>
        private static bool GameOtherOk(CharacterObject co, ItemObject it)
        {
            bool female = co.IsFemale;
            if (female && (it.ItemFlags & ItemFlags.NotUsableByFemale) != 0) return false;
            if (!female && (it.ItemFlags & ItemFlags.NotUsableByMale) != 0) return false;
            var id = it.StringId ?? "";
            if (id == "dragon_banner_center" || id == "dragon_banner_dragonhead" || id == "dragon_banner_handle") return false;
            if (it.HasHorseComponent && !it.HorseComponent.IsRideable) return false;
            return true;
        }
    }

    /// <summary>Z16: samotest ekranu przy wczytaniu (tylko log, bez zapisu).</summary>
    internal sealed class HeroGearBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, delegate (CampaignGameStarter s) { HeroGear.SelfTest(); });
        }

        public override void SyncData(IDataStore dataStore) { }
    }
}
