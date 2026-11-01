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
                // PRZEGLAD 175 (decyzja Jeffa 8b: "kazda jednostka ... bron, tarcze, amunicje i pancerz najwyzej swojego tieru"): wzorzec
                // bitwy DTE (referencja) to "najlepsza sztuka, ktorej wymog jednostka spelnia" - bez sufitu tieru jednostka z umiejetnoscia
                // >= progu tieru wyzej (35 x tier: milicje, Polnoc t3 po +25, lucznicy z Lukiem 175 ponizej t6) dostawala w bitwie z graczem
                // bron, strzaly i pancerz tieru wyzej. Sufit = tier jednostki (t0 jak t1), tylko gdy CrashScribe zamienil przy tym wczytaniu
                // wzorce wedlug tieru (CsMarks.TierGearApplied - bez CS 175 jak dotad). Konie (slot 10) poza decyzja 8b; reczne rozkazy gracza
                // (MusterBook) bez zmian. Zasada "umiejetnosc do wlasnego sprzetu" i wymogi przedmiotow zostaja.
                int capT = CsMarks.TierGearApplied ? Math.Max(1, ch.Tier) : 0;   // 0 = bez sufitu

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
                // 175.G (Army175GoldenBows, dom. NIE - czeka na K1 i na zgode Jeffa na wyjatek od zasady 28.08): gdy zestawy jednostki
                // maja ROZNE klasy dystansowe (CS GoldenBows: Zlota Kompania - kusza / luk dwukrzywy / luk cisowy), glowna bron
                // dystansowa = klasa z WYLOSOWANEGO zestawu (referencja DTE), nie najwyzsza umiejetnosc - inaczej wszyscy dostaliby
                // kusze (Kusza 110 > Luk 105). Zapas (bron reczna) bez zmian - po glownej strzeleckiej i tak nie moze byc dystansowy.
                // przeglad 175: tylko trzy jednostki kusznikow Zlotej Kompanii, ktorym CS GoldenBows daje trzy zestawy (Army175Gear.GoldenXbow)
                if ((main == "bow" || main == "xbow") && Settings.Current.Army175GoldenBows && Array.IndexOf(GoldenXbow, ch.StringId) >= 0)
                {
                    string drawn = RangedClassOf(reference);
                    if (drawn != null && drawn != main && MixedRanged(ch)) main = drawn;
                }

                // tarcza z szablonu zostaje (charakter jednostki), jesli glowna nie dwureczna
                ItemObject shield = null;
                for (int i = 0; i < 4; i++)
                {
                    var it = reference[(EquipmentIndex)i].Item;
                    if (it != null && it.ItemType == ItemObject.ItemTypeEnum.Shield) { shield = it; break; }
                }

                var slots = new List<ItemObject>();
                AddWeaponFor(slots, main, SkillFor(ch, main), capT, ch.Culture);
                // AMUNICJA WEDLE SKILLA (Jeff 02.09: "strzal t6 bandyci nie moga
                // miec"): Prawo Tieru daje strzalom/beltom wymog Bow/Crossbow,
                // wiec wzorzec amunicji dobieramy po skillu strzelca, nie po zerze
                // (zero = "najlepsza w grze", stad t6 u kazdego lucznika)
                if (main == "bow") { AddPattern(slots, ItemObject.ItemTypeEnum.Arrows, SkillFor(ch, "bow"), capT, ch.Culture); AddPattern(slots, ItemObject.ItemTypeEnum.Arrows, SkillFor(ch, "bow"), capT, ch.Culture); }
                else if (main == "xbow") { AddPattern(slots, ItemObject.ItemTypeEnum.Bolts, SkillFor(ch, "xbow"), capT, ch.Culture); AddPattern(slots, ItemObject.ItemTypeEnum.Bolts, SkillFor(ch, "xbow"), capT, ch.Culture); }
                else if (main == "thr") AddWeaponFor(slots, "thr", sThr, capT, ch.Culture);   // drugi pek oszczepow
                if (shield != null && main != "two" && main != "pole" && slots.Count < 4) slots.Add(shield);
                if (second != null && slots.Count < 4)
                {
                    AddWeaponFor(slots, second, SkillFor(ch, second), capT, ch.Culture);
                    if (second == "bow" && slots.Count < 4) AddPattern(slots, ItemObject.ItemTypeEnum.Arrows, SkillFor(ch, "bow"), capT, ch.Culture);
                    else if (second == "xbow" && slots.Count < 4) AddPattern(slots, ItemObject.ItemTypeEnum.Bolts, SkillFor(ch, "xbow"), capT, ch.Culture);
                }

                // KSIEGA MUSZTRY: reczne przypisania gracza maja pierwszenstwo
                // przed nasza logika skilli (bron sloty 0-3) - ale WYMAGANIA
                // przedmiotu sa swiete (Jeff: "nie moge dac luku ponad wymogi,
                // opcja wyszarzona"): pin ponad skill jednostki jest pomijany
                while (slots.Count < 4) slots.Add(null);
                var pinned = new bool[4];
                bool anyPin = false;
                for (int s = 0; s < 4; s++)
                {
                    var pin = MusterBook.PinFor(ch, s);
                    if (pin == null || !ItemReq.Meets(ch, pin)) continue;
                    slots[s] = pin; pinned[s] = true; anyPin = true;
                }
                // ROZKAZ ZASTEPUJE LUK WZORCA, NIE DOKLADA (Jeff 16.09: "QM: 52 empty battle
                // bow"). Ksiega musztry pokazuje sloty z szablonu XML (river_ranger:
                // Weapon 2 = lowland longbow), a wzorzec tutaj klade luk w slocie 0,
                // kolczany w 1-2, miecz w 3. Rozkaz "Weapon 2 = weirwood_bow" trafial wiec
                // OBOK luku wzorca, na miejsce kolczanu: [luk, weirwood, strzaly, miecz] -
                // dwa luki, jeden kolczan. Straz skilli (CrashScribe, 12:14-12:30) liczyla
                // drugi luk jako pusty slot ("PUSTE Bow: 11x Tully Longbowman, 28x Riverlands
                // Ranger, 16x Mallister Elite Archer" = wszyscy ludzie trzech oddzialow
                // z rozkazem), a DTE wydawal dwa luki jednemu. Rozkaz na luk/kusze czysci
                // inny, nie-rozkazany luk/kusze; wolne sloty dopelniamy amunicja do dwoch
                // kolczanow, potem bronia boczna wzorca. Bez rozkazu - jak dawniej.
                if (anyPin)
                {
                    for (int s = 0; s < 4; s++)
                    {
                        if (!pinned[s] || slots[s] == null) continue;
                        var tp = slots[s].ItemType;
                        if (tp != ItemObject.ItemTypeEnum.Bow && tp != ItemObject.ItemTypeEnum.Crossbow) continue;
                        for (int q = 0; q < 4; q++)
                        {
                            if (q == s || pinned[q] || slots[q] == null) continue;
                            var tq = slots[q].ItemType;
                            if (tq == ItemObject.ItemTypeEnum.Bow || tq == ItemObject.ItemTypeEnum.Crossbow) slots[q] = null;
                        }
                    }
                }
                // AUDYT 29.08: pin luku/kuszy nie moze zostac bez amunicji - dokladamy
                // wzorcowy kolczan/sajdak w wolny slot (16.09: przy rozkazie do DWOCH,
                // tyle daje wzorzec; bez rozkazu jak dawniej - tylko gdy nie ma zadnego)
                bool hasBow = false, hasXbow = false; int nArrows = 0, nBolts = 0;
                foreach (var it0 in slots)
                {
                    if (it0 == null) continue;
                    if (it0.ItemType == ItemObject.ItemTypeEnum.Bow) hasBow = true;
                    else if (it0.ItemType == ItemObject.ItemTypeEnum.Crossbow) hasXbow = true;
                    else if (it0.ItemType == ItemObject.ItemTypeEnum.Arrows) nArrows++;
                    else if (it0.ItemType == ItemObject.ItemTypeEnum.Bolts) nBolts++;
                }
                int wantAmmo = anyPin ? 2 : 1;
                for (int k = nArrows; hasBow && k < wantAmmo; k++) FillFree(slots, ItemObject.ItemTypeEnum.Arrows, SkillFor(ch, "bow"), capT, ch.Culture);
                for (int k = nBolts; hasXbow && k < wantAmmo; k++) FillFree(slots, ItemObject.ItemTypeEnum.Bolts, SkillFor(ch, "xbow"), capT, ch.Culture);
                // wolny slot po rozkazie: bron boczna wzorca (miecz lucznika), gdy zadnej nie ma
                if (anyPin && second != null && slots.Contains(null))
                {
                    bool melee = false;
                    foreach (var it0 in slots)
                        if (it0 != null && (it0.ItemType == ItemObject.ItemTypeEnum.OneHandedWeapon
                                            || it0.ItemType == ItemObject.ItemTypeEnum.TwoHandedWeapon
                                            || it0.ItemType == ItemObject.ItemTypeEnum.Polearm)) melee = true;
                    if (!melee)
                    {
                        var tmp = new List<ItemObject>();
                        AddWeaponFor(tmp, second, SkillFor(ch, second), capT, ch.Culture);
                        int free = slots.IndexOf(null);
                        if (tmp.Count > 0 && tmp[0] != null && free >= 0) slots[free] = tmp[0];
                    }
                }

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
                    var top = TopArmor(SlotArmorType(s), athletics, ch.Culture, capT);
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

        /// <summary>175.G: kusznicy Zlotej Kompanii - te same id co CS Army175Gear.GoldenXbow (punkt styku z drzewem a175cs).</summary>
        private static readonly string[] GoldenXbow = { "golden_crossbowman", "golden_veteran_crossbowman", "golden_master_crossbowman" };

        /// <summary>175.G: klasa dystansowa zestawu (sloty broni 0-3): "bow", "xbow" albo null.</summary>
        private static string RangedClassOf(Equipment eq)
        {
            if (eq == null) return null;
            for (int i = 0; i < 4; i++)
            {
                var it = eq[(EquipmentIndex)i].Item;
                if (it == null) continue;
                if (it.ItemType == ItemObject.ItemTypeEnum.Bow) return "bow";
                if (it.ItemType == ItemObject.ItemTypeEnum.Crossbow) return "xbow";
            }
            return null;
        }

        /// <summary>175.G: czy zestawy bojowe jednostki maja rozne klasy dystansowe (luk w jednym, kusza w drugim). Bez pamieci
        /// podrecznej: wolane tylko przy wlaczonym Army175GoldenBows, gdy wylosowany zestaw nie zgadza sie z najwyzsza umiejetnoscia,
        /// a zestawy zmienia latka CS przy wczytaniu.</summary>
        private static bool MixedRanged(CharacterObject ch)
        {
            string first = null;
            try
            {
                foreach (var eq in ch.BattleEquipments)
                {
                    var c = RangedClassOf(eq);
                    if (c == null) continue;
                    if (first == null) first = c; else if (c != first) return true;
                }
            }
            catch { }
            return false;
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

        private static void AddWeaponFor(List<ItemObject> slots, string cls, int skill, int maxTier = 0, CultureObject cult = null)
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
            AddPattern(slots, type, skill, maxTier, cult);
        }

        /// <summary>Najlepsza bron danego typu W RAMACH skilla - do podmian
        /// przy spawnie (zolnierz nie wchodzi golym slotem).</summary>
        internal static ItemObject PatternFor(ItemObject.ItemTypeEnum type, int skill)
        {
            var tmp = new List<ItemObject>();
            AddPattern(tmp, type, skill);
            return tmp.Count > 0 ? tmp[0] : null;
        }

        /// <summary>313a: to samo dla konkretnego zolnierza (tez bohatera) - przy Skills Decide Culture Pattern stary wzorzec, chyba ze to bron Dothrakow dla
        /// kogos, komu jej nie wolno (SkillsDecide.DothBarred) - wtedy bron tego typu wedlug jego kultury; klucz wylaczony albo bez zolnierza - jak PatternFor(type,
        /// skill). maxTier - sufit tieru jak w RearmBySkill (0 = bez; recenzja 313a: straz przy spawnie). Bez licznika przydzialow.</summary>
        internal static ItemObject PatternFor(ItemObject.ItemTypeEnum type, int skill, CharacterObject who, int maxTier = 0)
        {
            return BestPattern(type, skill, maxTier, who != null ? who.Culture : null, false);
        }

        /// <summary>313a (recenzja): sufit tieru wzorca dla zolnierza - ten sam co w RearmBySkill (capT).</summary>
        internal static int TierCap(CharacterObject ch)
        {
            return ch != null && CsMarks.TierGearApplied ? Math.Max(1, ch.Tier) : 0;
        }

        private static void FillFree(List<ItemObject> slots, ItemObject.ItemTypeEnum ammo, int skill, int maxTier = 0, CultureObject cult = null)
        {
            for (int i = 0; i < 4 && i < slots.Count; i++)
                if (slots[i] == null)
                {
                    var tmp = new List<ItemObject>();
                    AddPattern(tmp, ammo, skill, maxTier, cult);
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


        private static readonly Dictionary<int, ItemObject> TopMountCache = new Dictionary<int, ItemObject>();

        /// <summary>Najlepsza sztuka pancerza danego typu, ktorej WYMOG ATLETYKI
        /// (ItemReq: difficulty) jednostka udzwignie - cel przydzialu
        /// ("closest" = najlepsze dozwolone na stanie). Kubelki co 25 pkt.</summary>
        internal static ItemObject TopArmor(ItemObject.ItemTypeEnum type, int athletics, TaleWorlds.CampaignSystem.CultureObject culture = null, int maxTier = 0)
        {
            // PROG DOKLADNY, nie kubelek co 25 (audyt 31.08): stare
            // cap = bucket*25+24 oddawalo jednostce sztuke do 24 pkt PONAD jej
            // Atletyke - sam mechanizm pilnujacy zasady nadrzednej lamal ja.
            // Klucz cache tez musi byc dokladny, inaczej Atletyka 10 dostaje
            // z cache wpis wygrzany przez Atletyke 24.
            if (athletics < 0) athletics = 0;
            var cult = culture;
            string key = type + "|" + athletics + "|" + (cult != null ? cult.StringId : "-") + "|" + maxTier;   // przeglad 175: sufit tieru w kluczu
            // ROZNORODNOSC W RAMACH TIERU (Jeff 01.09: "wszyscy biegaja 1:1,
            // glupio wyglada... losowo rozne ubrania, ten sam poziom pancerza").
            // Jeden najlepszy item klonowal wyglad calych hord - teraz cache
            // trzyma CALE pasmo najwyzszego osiagalnego tieru, a kazde wywolanie
            // (= kazdy zolnierz przy spawnie) losuje z niego sztuke.
            // FILTR KULTURY (Jeff 01.09: "nie beda biegac w ciuchach Dorn...
            // jakim cudem bandyci w Riverrun w dothrackich pancerzach!"):
            // pula = itemy kultury JEDNOSTKI + bezkulturowe wspolne. Gdy pula
            // kulturowa pusta, dopiero wtedy caly swiat (lepsze obce niz golizna).
            List<ItemObject> band;
            if (!TopArmorBandCache.TryGetValue(key, out band))
            {
                bool ok = true;
                var pool = new List<ItemObject>();
                var poolAny = new List<ItemObject>();
                try
                {
                    foreach (var it in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
                    {
                        if (it == null || it.ItemType != type || !it.HasArmorComponent) continue;
                        if (it.Difficulty > athletics) continue;                               // atletyka rzadzi
                        if (maxTier > 0 && Recipes.Grade(it) > maxTier) continue;              // przeglad 175: nie ponad tier jednostki (8b)
                        if (it.NotMerchandise) continue;                                       // unikaty imienne i itemy testowe (dummy_armor_*)
                        if (GiantGear.Is(it)) continue;                                        // pancerz olbrzymow nie jest wzorcem (Jeff 14.09)
                        if (it.StringId != null && it.StringId.EndsWith("_crown")) continue;   // korony to regalia
                        poolAny.Add(it);
                        if (cult == null || it.Culture == null || it.Culture == cult) pool.Add(it);
                    }
                    if (pool.Count == 0) pool = poolAny;
                }
                catch (Exception e) { ok = false; Log.Error("SkillsDecide.TopArmor", e); }
                band = null;
                if (pool.Count > 0)
                {
                    int topTier = int.MinValue;
                    foreach (var it in pool) if ((int)it.Tier > topTier) topTier = (int)it.Tier;
                    band = new List<ItemObject>();
                    foreach (var it in pool) if ((int)it.Tier == topTier) band.Add(it);
                }
                // cachujemy TAKZE null (uczciwe "nic nie ma"), ale WYLACZNIE po
                // udanym przebiegu - jeden wyjatek nie moze zgasic typu do konca
                // procesu (wzorzec _tentBroken z CLAUDE.md p.7)
                if (ok) TopArmorBandCache[key] = band;
            }
            if (band == null || band.Count == 0) return null;
            return band[TaleWorlds.Core.MBRandom.RandomInt(band.Count)];
        }

        private static readonly Dictionary<string, List<ItemObject>> TopArmorBandCache =
            new Dictionary<string, List<ItemObject>>();

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
                    // TYLKO ZWYKLY KON (Jeff 14.09, crash 11:17:58): zamiennik
                    // wchodzi pod ISTNIEJACA uprzaz jednostki - wielblad,
                    // mamut, rydwan czy slon pod konska uprzezia to natywny
                    // AccessViolation w AddMountMesh. Do tego zadnych trupow,
                    // jednorozcow i unikatow na szeregowym.
                    if (!Stables.IsPlainMount(it)) continue;
                    var mon = it.HorseComponent != null ? it.HorseComponent.Monster : null;
                    if (mon == null || mon.StringId == null || !mon.StringId.StartsWith("horse")) continue;
                    if (it.NotMerchandise) continue;
                    if (id.StartsWith("dragon_") || id == "elephant" || id.StartsWith("wight_")
                        || id.StartsWith("unicorn") || id.StartsWith("zorse")) continue;
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
        private static void AddPattern(List<ItemObject> slots, ItemObject.ItemTypeEnum type, int skill, int maxTier = 0, CultureObject cult = null)
        {
            if (slots.Count >= 4) return;
            var best = BestPattern(type, skill, maxTier, cult, true);
            if (best != null) slots.Add(best);
        }

        // ================================================================ 313a (Jeff 01.11: "te arakhi wywal ... pozamieniaj im to ... daj dobre miecze buzdygany")
        // ZRODLO ARAKHOW: wzorzec klasy (bez kultury) to najlepsza bron typu w CALYM swiecie w granicy umiejetnosci i tieru - dla OneHandedWeapon przy
        // umiejetnosci od 70 i tierze 3 jest nim arakh (wymog 70, mocny, tani). RearmBySkill wklada wzorzec do referencji DTE Assignment, a DTE dla partii, ktorych
        // nie obsluguje zbrojownia (bandy, karawany, wiesniacy, milicje, patrole - SpawnAgentPatch.IsPartyValidForProcessing false, RandomizeNonHeroLedAiPartiesArmor
        // TAK u Jeffa) robi new Assignment(...) + FillEmptySlots, czyli bron = wzorzec wprost (log 01.11 15:21:50: clegane_footman i mallister_footman bandy looters
        // z arakhem i tarcza wzorca - "tarcza z szablonu zostaje").
        // Recenzja 313a: zmiana TYLKO tam, gdzie stary wzorzec jest bronia Dothrakow dla zolnierza, ktoremu jej nie wolno (DothBarred) - wtedy najlepsza bron tego
        // samego typu bez broni Dothrakow, wedlug kultury zolnierza: (0) jego kultura albo bez kultury, (1) ta sama strona Waskiego Morza, (2) reszta; w poziomie
        // najlepsza jak dotad (RangedRank.Key). Kazdy inny wzorzec (luki, kusze, strzaly, drzewce, wzorce Dothrakow, wzorce bez broni Dothrakow) - dokladnie stary
        // wybor i stary klucz pamieci. Klucz wylaczony = jak przed 313a.
        private static readonly object PatternLock = new object();

        internal static bool CultOn { get { var s = Settings.Current; return s != null && s.SkillsDecideCulturePattern; } }

        /// <summary>313a: czy zolnierz tej kultury NIE dostaje broni Dothrakow ze wzorca: nie-Dothrak; przy Gear Fit Dothraki Only kazdy (tez Essos - Wolne Miasta,
        /// Ghis - jak regula sklepow i bitwy), przy wylaczonym tylko Westeros (opis klucza: "off = anyone from the Essos side may carry an arakh").</summary>
        internal static bool DothBarred(string tc)
        {
            if (string.IsNullOrEmpty(tc) || GearFit.IsDoth(tc)) return false;
            return GearFit.DothOn || !GearFit.IsEssos(tc);
        }

        /// <summary>Bron Dothrakow wedlug spisu (DothOnly); spis niegotowy (przed startem gry DothOnly daje false dla wszystkiego) - kazda bron kultury
        /// Dothrakow (recenzja 313a p. 7: inaczej Assignment z OnGameLoaded DTE dostalby arakh w referencji).</summary>
        internal static bool DothArm(ItemObject it, bool kinReady)
        {
            if (it == null) return false;
            if (kinReady) return GearFit.DothOnly(it);
            return it.Culture != null && GearFit.IsDoth(it.Culture.StringId);
        }

        /// <summary>313a: wzorce wedlug kultury od nowa (DothrakiArms.Reset; GearFit.SessionStart i Census - nowy spis broni Dothrakow). Stare klucze zostaja.</summary>
        internal static void ResetCulturePatterns()
        {
            lock (PatternLock)
            {
                var drop = new List<string>();
                foreach (var k in Pattern.Keys) if (k.IndexOf("|c=", StringComparison.Ordinal) >= 0) drop.Add(k);
                foreach (var k in drop) Pattern.Remove(k);
            }
        }

        private static int CultLevel(string tc, ItemObject it)
        {
            string ic = it.Culture != null ? it.Culture.StringId : null;
            if (string.IsNullOrEmpty(ic) || ic == "neutral_culture" || string.IsNullOrEmpty(tc) || ic == tc) return 0;
            return GearFit.IsEssos(ic) == GearFit.IsEssos(tc) ? 1 : 2;
        }

        /// <summary>Czy sztuka moze byc wzorcem (filtr jak dotad: umiejetnosc, sufit tieru, bez ognistej amunicji, legend, maczug olbrzymow, unikatow, smokow).</summary>
        private static bool PatternCandidate(ItemObject it, ItemObject.ItemTypeEnum type, int skill, int maxTier)
        {
            if (it == null || it.ItemType != type || !it.HasWeaponComponent) return false;
            if (it.Difficulty > skill) return false;                     // statystyki rzadza
            if (maxTier > 0 && Recipes.Grade(it) > maxTier) return false; // przeglad 175: nie ponad tier jednostki (8b)
            // OGNISTA AMUNICJA NIE JEST WZORCEM (Jeff 02.09: "lucznicy
            // biegaja z plonacymi strzalami, daj im normalne strzaly
            // ich tieru") - burning/flaming to specjal okretowy, nie
            // kolczan piechoty; Effectiveness i tak ja faworyzuje
            if ((type == ItemObject.ItemTypeEnum.Arrows || type == ItemObject.ItemTypeEnum.Bolts)
                && it.StringId != null
                && (it.StringId.IndexOf("burning", StringComparison.OrdinalIgnoreCase) >= 0
                    || it.StringId.IndexOf("flaming", StringComparison.OrdinalIgnoreCase) >= 0)) return false;
            if (LegendaryLaw.IsLegend(it)) return false;                 // legendy nie sa wzorcem
            if (GiantGear.Is(it)) return false;                          // maczugi olbrzymow tez nie (Jeff 14.09)
            if (it.NotMerchandise) return false;                         // unikaty imienne i itemy testowe
            if (it.StringId != null && it.StringId.StartsWith("dragon_")) return false;
            return true;
        }

        /// <summary>Wzorzec klasy (pamiec na klucz). Najpierw stary wybor (stary klucz, bez zmian). Gdy klucz 313a czynny, zolnierz ma kulture z zakazem broni
        /// Dothrakow (DothBarred), a stary wzorzec jest bronia Dothrakow - zamiennik tego samego typu wedlug kultury (opis wyzej), klucz "|c=kultura". count - licznik
        /// przydzialow z poprawionym wzorcem (RearmBySkill) do linii "Arakhy (313a)". Spis broni Dothrakow niegotowy - zamiennik bez zapisu w pamieci.
        /// Pamiec pod zamkiem (ctor Assignment bywa na watku DTE).</summary>
        internal static ItemObject BestPattern(ItemObject.ItemTypeEnum type, int skill, int maxTier, CultureObject cult, bool count)
        {
            if (skill < 0) skill = 0;
            string key = type + "|" + skill + "|" + maxTier;   // prog dokladny, jak przy pancerzu; przeglad 175: sufit tieru (0 = bez)
            ItemObject old;
            bool hit;
            lock (PatternLock) hit = Pattern.TryGetValue(key, out old);
            if (!hit)
            {
                bool ok = true;
                old = null;
                try
                {
                    var all = MBObjectManager.Instance.GetObjectTypeList<ItemObject>();
                    foreach (var it in all)
                    {
                        if (!PatternCandidate(it, type, skill, maxTier)) continue;
                        // 16.09: luki/kusze wg RangedRank (pod RBM naciag), reszta jak dawniej
                        if (old == null || RangedRank.Key(it) > RangedRank.Key(old)) old = it;
                    }
                }
                catch (Exception e) { ok = false; Log.Error("SkillsDecide.AddPattern", e); }
                if (ok) lock (PatternLock) Pattern[key] = old;
            }
            if (old == null || cult == null || !CultOn) return old;
            string tc = cult.StringId;
            if (!DothBarred(tc)) return old;                     // Dothrakowie (i Essos przy wylaczonym Gear Fit Dothraki Only) - bez zmian
            bool kinReady = GearFit.KinReady;
            if (!DothArm(old, kinReady)) return old;             // stary wzorzec nie jest bronia Dothrakow - bez zmian
            string ckey = key + "|c=" + tc;
            ItemObject best;
            lock (PatternLock) hit = Pattern.TryGetValue(ckey, out best);
            if (!hit)
            {
                bool ok = true;
                var lv = new ItemObject[3];
                try
                {
                    var all = MBObjectManager.Instance.GetObjectTypeList<ItemObject>();
                    foreach (var it in all)
                    {
                        if (!PatternCandidate(it, type, skill, maxTier)) continue;
                        if (DothArm(it, kinReady)) continue;     // 313a: bron Dothrakow nigdy dla takiego zolnierza
                        int l = CultLevel(tc, it);
                        if (lv[l] == null || RangedRank.Key(it) > RangedRank.Key(lv[l])) lv[l] = it;
                    }
                }
                catch (Exception e) { ok = false; Log.Error("SkillsDecide.AddPattern", e); }
                best = null;
                int level = 3;
                for (int l = 0; l < 3 && best == null; l++) if (lv[l] != null) { best = lv[l]; level = l; }
                if (ok && kinReady)
                {
                    bool fresh;
                    lock (PatternLock) { fresh = !Pattern.ContainsKey(ckey); Pattern[ckey] = best; }
                    if (fresh) DothrakiArms.NotePatternKey(level, true, true);
                }
            }
            // brak innej broni tego typu w granicy umiejetnosci - pusty slot zamiast broni Dothrakow (DTE FillEmptySlots zostawia go pustym)
            if (count) DothrakiArms.NotePatternFixed();
            return best;
        }
    }
}
