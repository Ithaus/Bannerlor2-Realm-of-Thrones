using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// STATYSTYKI RZADZA SPRZETEM (Jeff 28.08): "zasada max 2 tiery wyzej jest
    /// bezsensowna - wojsko moze uzywac czego chce, jesli statystyki pozwalaja.
    /// Glowna bron = najwyzsza umiejetnosc, zapasowa = druga w kolejnosci.
    /// Lucznik: luk + 2 kolczany + bron z drugiego skilla (one-handed, nie
    /// wlocznia, jesli one-handed wyzsze)".
    ///
    /// Dwa ostrza:
    /// (1) prefix na DTE GetMaxAllowedTier - limit tier+2 zdjety (zawsze 6);
    /// (2) postfix na konstruktor DTE Assignment - sloty broni w REFERENCJI
    ///     (prywatnej kopii per zolnierz) przestawiamy wg skilli jednostki,
    ///     a wzorcem kazdej klasy jest najlepsza bron, ktorej WYMAGANIA
    ///     (item.Difficulty vs skill) jednostka spelnia. DTE dobiera potem
    ///     z magazynu "najblizsze wzorcowi" - czyli wlasnie to, co umieja niesc.
    /// Tarcza zostaje, jesli jednostka miala ja w szablonie i glowna bron jest
    /// jednoreczna. Legendy wykluczone ze wzorcow (LegendaryLaw).
    /// </summary>
    internal static class SkillsDecide
    {
        private static readonly Dictionary<string, ItemObject> Pattern = new Dictionary<string, ItemObject>();

        internal static void ApplyAll(Harmony harmony)
        {
            try
            {
                if (!Settings.Current.SkillsDecideEnabled) { Log.Info("SkillsDecide: wylaczone."); return; }
                Type asg = null, dist = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        if (asg == null) asg = asm.GetType("DynamicTroopEquipmentReupload.Assignment");
                        if (dist == null) dist = asm.GetType("DynamicTroopEquipmentReupload.PartyEquipmentDistributor");
                    }
                    catch { }
                }
                if (asg == null || dist == null) { Log.Info("SkillsDecide: DTE nieobecny - patch spi."); return; }

                var tierM = AccessTools.Method(dist, "GetMaxAllowedTier");
                if (tierM != null)
                    harmony.Patch(tierM, prefix: new HarmonyMethod(typeof(SkillsDecide), "NoTierCap"));

                var ctor = AccessTools.Constructor(asg, new[] { typeof(CharacterObject), typeof(Equipment), typeof(bool), typeof(bool) });
                if (ctor == null) { Log.Info("SkillsDecide: nie znalazlem ctora Assignment - bronie wg szablonu."); return; }
                harmony.Patch(ctor, postfix: new HarmonyMethod(typeof(SkillsDecide), "RearmBySkill"));
                Log.Info("SkillsDecide: limit tierow zdjety, bronie ida wg umiejetnosci jednostki.");
            }
            catch (Exception e) { Log.Error("SkillsDecide.ApplyAll", e); }
        }

        public static bool NoTierCap(ref int __result)
        {
            __result = 6;   // zadnego "domyslny tier + 2" - statystyki rzadza
            return false;
        }

        /// <summary>Postfix ctora Assignment: przestaw sloty broni prywatnej
        /// kopii referencji wg skilli jednostki.</summary>
        public static void RearmBySkill(object __instance)
        {
            try
            {
                if (!Settings.Current.SkillsDecideEnabled) return;
                var t = __instance.GetType();
                var ch = Traverse.Create(__instance).Property("Character").GetValue() as CharacterObject;
                var reference = Traverse.Create(__instance).Property("ReferenceEquipment").GetValue() as Equipment;
                if (ch == null || reference == null || ch.IsHero) return;

                // skille klas broni
                int sOne = ch.GetSkillValue(DefaultSkills.OneHanded);
                int sTwo = ch.GetSkillValue(DefaultSkills.TwoHanded);
                int sPole = ch.GetSkillValue(DefaultSkills.Polearm);
                int sBow = ch.GetSkillValue(DefaultSkills.Bow);
                int sXbow = ch.GetSkillValue(DefaultSkills.Crossbow);
                int sThr = ch.GetSkillValue(DefaultSkills.Throwing);

                // glowna = najwyzszy skill; zapas = najwyzszy z pozostalych,
                // przy czym po glownej strzeleckiej zapas MUSI byc reczny
                var ranked = new List<KeyValuePair<string, int>>
                {
                    new KeyValuePair<string, int>("one", sOne),
                    new KeyValuePair<string, int>("two", sTwo),
                    new KeyValuePair<string, int>("pole", sPole),
                    new KeyValuePair<string, int>("bow", sBow),
                    new KeyValuePair<string, int>("xbow", sXbow),
                    new KeyValuePair<string, int>("thr", sThr)
                };
                ranked.Sort((a, b) => b.Value.CompareTo(a.Value));
                string main = ranked[0].Key;
                string second = null;
                foreach (var r in ranked)
                {
                    if (r.Key == main) continue;
                    bool mainRanged = main == "bow" || main == "xbow";
                    bool candRanged = r.Key == "bow" || r.Key == "xbow" || r.Key == "thr";
                    if (mainRanged && candRanged) continue;   // luk + kusza to nie zapas
                    second = r.Key; break;
                }

                // tarcza z szablonu zostaje (charakter jednostki), jesli glowna nie dwureczna
                ItemObject shield = null;
                for (int i = 0; i < 4; i++)
                {
                    var it = reference[(EquipmentIndex)i].Item;
                    if (it != null && it.ItemType == ItemObject.ItemTypeEnum.Shield) { shield = it; break; }
                }

                var slots = new List<ItemObject>();
                AddWeaponFor(slots, main, SkillFor(ch, main));
                if (main == "bow") { AddPattern(slots, ItemObject.ItemTypeEnum.Arrows, 0); AddPattern(slots, ItemObject.ItemTypeEnum.Arrows, 0); }
                else if (main == "xbow") { AddPattern(slots, ItemObject.ItemTypeEnum.Bolts, 0); AddPattern(slots, ItemObject.ItemTypeEnum.Bolts, 0); }
                else if (main == "thr") AddWeaponFor(slots, "thr", sThr);   // drugi pek oszczepow
                if (shield != null && main != "two" && main != "pole" && slots.Count < 4) slots.Add(shield);
                if (second != null && slots.Count < 4)
                {
                    AddWeaponFor(slots, second, SkillFor(ch, second));
                    if (second == "bow" && slots.Count < 4) AddPattern(slots, ItemObject.ItemTypeEnum.Arrows, 0);
                    else if (second == "xbow" && slots.Count < 4) AddPattern(slots, ItemObject.ItemTypeEnum.Bolts, 0);
                }

                // KSIEGA MUSZTRY: reczne przypisania gracza maja pierwszenstwo
                // przed nasza logika skilli (bron sloty 0-3) - ale WYMAGANIA
                // przedmiotu sa swiete (Jeff: "nie moge dac luku ponad wymogi,
                // opcja wyszarzona"): pin ponad skill jednostki jest pomijany
                while (slots.Count < 4) slots.Add(null);
                for (int s = 0; s < 4; s++)
                {
                    var pin = MusterBook.PinFor(ch, s);
                    if (pin == null || !ItemReq.Meets(ch, pin)) continue;
                    slots[s] = pin;
                }
                // AUDYT 29.08: pin luku/kuszy nie moze zostac bez amunicji -
                // dokladamy wzorcowy kolczan/sajdak w wolny slot
                bool hasBow = false, hasXbow = false, hasArrows = false, hasBolts = false;
                foreach (var it0 in slots)
                {
                    if (it0 == null) continue;
                    if (it0.ItemType == ItemObject.ItemTypeEnum.Bow) hasBow = true;
                    else if (it0.ItemType == ItemObject.ItemTypeEnum.Crossbow) hasXbow = true;
                    else if (it0.ItemType == ItemObject.ItemTypeEnum.Arrows) hasArrows = true;
                    else if (it0.ItemType == ItemObject.ItemTypeEnum.Bolts) hasBolts = true;
                }
                if (hasBow && !hasArrows) FillFree(slots, ItemObject.ItemTypeEnum.Arrows);
                if (hasXbow && !hasBolts) FillFree(slots, ItemObject.ItemTypeEnum.Bolts);

                for (int i = 0; i < 4; i++)
                    reference[(EquipmentIndex)i] = i < slots.Count && slots[i] != null
                        ? new EquipmentElement(slots[i]) : new EquipmentElement(null);
                // PANCERZ CELUJE W GORE (Jeff 29.08: "czy AI nie zacznie rozbierac
                // lucznikow do bazowego wzorca?"): DTE co bitwe dobiera sprzet
                // NAJBLIZSZY wzorcowi - bazowy szablon t3 sciagalby zolnierzy
                // w dol mimo t6 w magazynie. Wzorzec pancerza to od teraz
                // NAJLEPSZA sztuka danego typu w grze: "najblizsze wzorcowi"
                // znaczy wtedy "najlepsze, co magazyn ma". Pin gracza wygrywa.
                int athletics = ch.GetSkillValue(DefaultSkills.Athletics);
                for (int s = 5; s <= 9; s++)
                {
                    var pin = MusterBook.PinFor(ch, s);
                    if (pin != null && ItemReq.Meets(ch, pin))
                    { reference[(EquipmentIndex)s] = new EquipmentElement(pin); continue; }
                    if (reference[(EquipmentIndex)s].Item == null) continue;   // szablon nie ubiera slotu - nie my
                    var top = TopArmor(SlotArmorType(s), athletics);
                    if (top != null) reference[(EquipmentIndex)s] = new EquipmentElement(top);
                }
                // KON I RZAD (Jeff: "konie i pancerze tez, CALY ekwipunek!"):
                // pin gracza (w ramach Jezdziectwa) wygrywa; bez pinu jezdziec
                // celuje w najlepszego konia, na jakiego pozwala mu Riding
                var pinHorse = MusterBook.PinFor(ch, 10);
                if (pinHorse != null && ItemReq.Meets(ch, pinHorse))
                    reference[(EquipmentIndex)10] = new EquipmentElement(pinHorse);
                else if (reference[(EquipmentIndex)10].Item != null)
                {
                    var topH = TopMount(ch.GetSkillValue(DefaultSkills.Riding));
                    if (topH != null) reference[(EquipmentIndex)10] = new EquipmentElement(topH);
                }
                var pinHar = MusterBook.PinFor(ch, 11);
                if (pinHar != null) reference[(EquipmentIndex)11] = new EquipmentElement(pinHar);
            }
            catch (Exception e) { Log.Error("SkillsDecide.RearmBySkill", e); }
        }

        private static int SkillFor(CharacterObject ch, string cls)
        {
            switch (cls)
            {
                case "one": return ch.GetSkillValue(DefaultSkills.OneHanded);
                case "two": return ch.GetSkillValue(DefaultSkills.TwoHanded);
                case "pole": return ch.GetSkillValue(DefaultSkills.Polearm);
                case "bow": return ch.GetSkillValue(DefaultSkills.Bow);
                case "xbow": return ch.GetSkillValue(DefaultSkills.Crossbow);
                default: return ch.GetSkillValue(DefaultSkills.Throwing);
            }
        }

        private static void AddWeaponFor(List<ItemObject> slots, string cls, int skill)
        {
            ItemObject.ItemTypeEnum type;
            switch (cls)
            {
                case "one": type = ItemObject.ItemTypeEnum.OneHandedWeapon; break;
                case "two": type = ItemObject.ItemTypeEnum.TwoHandedWeapon; break;
                case "pole": type = ItemObject.ItemTypeEnum.Polearm; break;
                case "bow": type = ItemObject.ItemTypeEnum.Bow; break;
                case "xbow": type = ItemObject.ItemTypeEnum.Crossbow; break;
                default: type = ItemObject.ItemTypeEnum.Thrown; break;
            }
            AddPattern(slots, type, skill);
        }

        /// <summary>Najlepsza bron danego typu W RAMACH skilla - do podmian
        /// przy spawnie (zolnierz nie wchodzi golym slotem).</summary>
        internal static ItemObject PatternFor(ItemObject.ItemTypeEnum type, int skill)
        {
            var tmp = new List<ItemObject>();
            AddPattern(tmp, type, skill);
            return tmp.Count > 0 ? tmp[0] : null;
        }

        private static void FillFree(List<ItemObject> slots, ItemObject.ItemTypeEnum ammo)
        {
            for (int i = 0; i < 4 && i < slots.Count; i++)
                if (slots[i] == null)
                {
                    var tmp = new List<ItemObject>();
                    AddPattern(tmp, ammo, 0);
                    if (tmp.Count > 0) slots[i] = tmp[0];
                    return;
                }
        }

        private static ItemObject.ItemTypeEnum SlotArmorType(int slot)
        {
            switch (slot)
            {
                case 5: return ItemObject.ItemTypeEnum.HeadArmor;
                case 6: return ItemObject.ItemTypeEnum.BodyArmor;
                case 7: return ItemObject.ItemTypeEnum.LegArmor;
                case 8: return ItemObject.ItemTypeEnum.HandArmor;
                default: return ItemObject.ItemTypeEnum.Cape;
            }
        }

        private static readonly Dictionary<string, ItemObject> TopArmorCache = new Dictionary<string, ItemObject>();
        private static readonly Dictionary<int, ItemObject> TopMountCache = new Dictionary<int, ItemObject>();

        /// <summary>Najlepsza sztuka pancerza danego typu, ktorej WYMOG ATLETYKI
        /// (ItemReq: difficulty) jednostka udzwignie - cel przydzialu
        /// ("closest" = najlepsze dozwolone na stanie). Kubelki co 25 pkt.</summary>
        internal static ItemObject TopArmor(ItemObject.ItemTypeEnum type, int athletics)
        {
            // PROG DOKLADNY, nie kubelek co 25 (audyt 31.08): stare
            // cap = bucket*25+24 oddawalo jednostce sztuke do 24 pkt PONAD jej
            // Atletyke - sam mechanizm pilnujacy zasady nadrzednej lamal ja.
            // Klucz cache tez musi byc dokladny, inaczej Atletyka 10 dostaje
            // z cache wpis wygrzany przez Atletyke 24.
            if (athletics < 0) athletics = 0;
            string key = type + "|" + athletics;
            ItemObject best;
            if (TopArmorCache.TryGetValue(key, out best)) return best;
            bool ok = true;
            try
            {
                foreach (var it in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
                {
                    if (it == null || it.ItemType != type || !it.HasArmorComponent) continue;
                    if (it.Difficulty > athletics) continue;                               // atletyka rzadzi
                    if (it.NotMerchandise) continue;                                       // unikaty imienne i itemy testowe (dummy_armor_*)
                    if (it.StringId != null && it.StringId.EndsWith("_crown")) continue;   // korony to regalia
                    if (best == null || it.Effectiveness > best.Effectiveness) best = it;
                }
            }
            catch (Exception e) { ok = false; Log.Error("SkillsDecide.TopArmor", e); }
            // cachujemy TAKZE null (uczciwe "nic nie ma"), ale WYLACZNIE po
            // udanym przebiegu - jeden wyjatek nie moze zgasic typu do konca
            // procesu (wzorzec _tentBroken z CLAUDE.md p.7)
            if (ok) TopArmorCache[key] = best;
            return best;
        }

        /// <summary>Najlepszy KON w ramach Jezdziectwa jednostki. Smoki i slonie
        /// poza wzorcem (pin gracza moze slonia, jesli Riding pozwala).</summary>
        internal static ItemObject TopMount(int riding)
        {
            if (riding < 0) riding = 0;
            ItemObject best;
            if (TopMountCache.TryGetValue(riding, out best)) return best;
            bool ok = true;
            try
            {
                foreach (var it in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
                {
                    if (it == null || it.ItemType != ItemObject.ItemTypeEnum.Horse) continue;
                    if (it.Difficulty > riding) continue;                    // prog dokladny, jak przy pancerzu
                    var id = it.StringId ?? "";
                    if (id.StartsWith("dragon_") || id == "elephant") continue;
                    if (best == null || it.Effectiveness > best.Effectiveness) best = it;
                }
            }
            catch (Exception e) { ok = false; Log.Error("SkillsDecide.TopMount", e); }
            if (ok) TopMountCache[riding] = best;
            return best;
        }

        /// <summary>Wzorzec klasy: najlepsza bron (Effectiveness), ktorej
        /// WYMAGANIA jednostka spelnia (Difficulty <= skill; kubelki co 25 pkt,
        /// zeby cache nie pecznial). Legendy poza wzorcami.</summary>
        private static void AddPattern(List<ItemObject> slots, ItemObject.ItemTypeEnum type, int skill)
        {
            if (slots.Count >= 4) return;
            if (skill < 0) skill = 0;
            string key = type + "|" + skill;          // prog dokladny, jak przy pancerzu
            ItemObject best;
            if (!Pattern.TryGetValue(key, out best))
            {
                bool ok = true;
                try
                {
                    var all = MBObjectManager.Instance.GetObjectTypeList<ItemObject>();
                    foreach (var it in all)
                    {
                        if (it == null || it.ItemType != type || !it.HasWeaponComponent) continue;
                        if (it.Difficulty > skill) continue;                     // statystyki rzadza
                        if (LegendaryLaw.IsLegend(it)) continue;                 // legendy nie sa wzorcem
                        if (it.NotMerchandise) continue;                         // unikaty imienne i itemy testowe
                        if (it.StringId != null && it.StringId.StartsWith("dragon_")) continue;
                        if (best == null || it.Effectiveness > best.Effectiveness) best = it;
                    }
                }
                catch (Exception e) { ok = false; Log.Error("SkillsDecide.AddPattern", e); }
                if (ok) Pattern[key] = best;
            }
            if (best != null) slots.Add(best);
        }
    }
}
