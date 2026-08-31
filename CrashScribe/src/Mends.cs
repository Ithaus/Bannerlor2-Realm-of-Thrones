using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace CrashScribe
{
    /// <summary>
    /// Mostek na crash RealisticBannerlord: jego szpiegowski dialog
    /// (lord_spy_fail_consequence) wola PlayerEncounter.RestartPlayerEncounter
    /// z TRZEMA argumentami - w tej wersji gry metoda ma CZTERY, wiec sam JIT
    /// tej metody konczy sie MissingMethodException i gra pada. Oryginalu nie
    /// da sie zalatac wprost (Harmony tez musialby go skompilowac), wiec
    /// przechwytujemy zdanie dialogowe o id "lord_spy_reaction_fail" i odgrywamy
    /// jego skutki sami, juz z poprawna sygnatura.
    /// </summary>
    internal static class Mends
    {
        /// <summary>Prefix usypiajacy odswiezanie martwego widgetu DTE (A6).</summary>
        internal static bool SkipDteMapWidget() { return false; }

        /// <summary>
        /// UMARLI NIE ZNAJA STRACHU (Jeff 30.08). ROT nie rusza morale
        /// bitewnego, wiec wighty panikowaly i uciekaly z pola jak chlopi
        /// (angry_wight to level 11 - fala zalamian przy stratach).
        /// Postfix na CanPanicDueToMorale: agent kultury whitewalker nigdy
        /// nie dostaje prawa do paniki. Silnik (CommonAIComponent.OnTickParallel)
        /// przy odmowie trzyma morale na 0.01 i jednostka bije sie do konca -
        /// ta sama sciezka, ktorej vanilla uzywa dla perka Loyalty and Honor.
        /// Parametr przez __0, bo implementacje moga rozne nazywac agenta.
        /// </summary>
        public static void DeadDontPanic(TaleWorlds.MountAndBlade.Agent __0, ref bool __result)
        {
            if (!__result) return;                       // ktos juz odmowil paniki - nie ruszamy
            var a = __0;
            if (a == null || !a.IsHuman) return;
            var ch = a.Character;
            if (ch != null && ch.Culture != null && ch.Culture.StringId == "whitewalker")
                __result = false;
        }

        /// <summary>Bialy Wedrowiec albo Nocny Krol - NIE zwykly wight.
        /// Wedrowcy to jednostki whitewalker2/3/4 i szablon ROTuniqueleader_whitewalker;
        /// NK w bitwie to bohater kultury whitewalker (tak samo bohater-Other gracza).</summary>
        private static bool WalkerBlood(BasicCharacterObject c)
        {
            try
            {
                if (c == null) return false;
                var id = c.StringId ?? "";
                if (id.StartsWith("whitewalker") || id.Contains("uniqueleader_whitewalker")) return true;
                if (!c.IsHero) return false;             // zwykly wight (xxx_wight) odpada
                return c.Culture != null && c.Culture.StringId == "whitewalker";
            }
            catch { return false; }
        }

        /// <summary>
        /// VALYRIANSKA ZASADA T6 (Jeff 30.08): "Biali Wedrowcy i Nocny Krol maja
        /// odpornosc na bron, ktora nie jest valyrianska stala - u nas bron T6.
        /// Kazda bron ponizej T6 - mocno ograniczone obrazenia." Prefix na
        /// Agent.RegisterBlow - jedyne gardlo, przez ktore przechodzi kazdy cios
        /// (melee, pocisk, taran; takze dodatkowe rejestracje RBM). Tier broni:
        /// dla melee przedmiot ze slotu atakujacego (WeaponRecord niesie slot,
        /// nie item), dla pocisku bron miotajaca w rece strzelca (luk/kusza/
        /// oszczep - strzaly nie maja tierow z valyrianskiej stali, liczy sie
        /// narzedzie). Piesc, kopyto konia i upadek NIE przebijaja lodu: tier 0.
        /// Bron T6+ bije normalnie; reszta zadaje 15% (min 1). PULAPKA TIEROW:
        /// ItemTiers.Tier1 == 0, wiec wyswietlany tier = (int)Tier + 1.
        /// </summary>
        public static void ValyrianWard(TaleWorlds.MountAndBlade.Agent __instance,
                                        ref TaleWorlds.MountAndBlade.Blow blow)
        {
            try
            {
                if (blow.InflictedDamage <= 1 || blow.IsFallDamage) return;
                var v = __instance;
                if (v == null || !v.IsHuman || !WalkerBlood(v.Character)) return;

                int tier = 0;                                    // gole rece / kopyto = zadna stal
                var rec = blow.WeaponRecord;
                var mission = TaleWorlds.MountAndBlade.Mission.Current;
                var att = mission != null ? mission.FindAgentWithIndex(blow.OwnerId) : null;
                // SMOCZY OGIEN pali Innych pelnia takze w polu (Jeff 31.08) -
                // cios od agenta-smoka nie podlega cieciu T6
                if (att != null && !att.IsHuman)
                {
                    try
                    {
                        var mu = att.Monster != null ? (att.Monster.MonsterUsage ?? "") : "";
                        if (mu.IndexOf("dragon", StringComparison.OrdinalIgnoreCase) >= 0) return;
                    }
                    catch { }
                }
                if (rec.HasWeapon() && att != null)
                {
                    ItemObject it = null;
                    if (!rec.IsMissile)
                    {
                        int slot = rec.AffectorWeaponSlotOrMissileIndex;
                        if (slot >= 0 && slot < 5)
                        {
                            var mw = att.Equipment[(EquipmentIndex)slot];
                            if (!mw.IsEmpty) it = mw.Item;
                        }
                    }
                    else
                    {
                        var mw = att.WieldedWeapon;
                        if (!mw.IsEmpty) it = mw.Item;
                    }
                    if (it != null) tier = (int)it.Tier + 1;
                }
                if (tier >= 6) return;                           // rownowaznik valyrianskiej stali

                int cut = blow.InflictedDamage * 15 / 100;
                if (cut < 1) cut = 1;
                blow.InflictedDamage = cut;
            }
            catch { }                                            // per-cios: zadnego raportowania
        }

        /// <summary>
        /// WIELBLADY NIE DLA POLNOCY (Jeff 30.08: "nie moge patrzec jak kawaleria
        /// polnocy jezdzi na wielbladach"). DTE zbiera do przydzialu WSZYSTKIE
        /// konie z taboru partii - takze wielblady/rydwany/slonie z lupow po
        /// poludniowcach - i sadza na nie jezdzcow bez pytania o kulture.
        /// Postfix na PartyEquipmentDistributor.GenerateHorseAndHarnessList:
        /// wycinamy z listy przydzialu egzotyki (camel/chariot/elephant w id),
        /// ktorych kultura (aserai=Dorne, volantine=Essos) nie zgadza sie
        /// z kultura partii. Zwierze NIE znika - zostaje w taborze jako towar
        /// na sprzedaz (karawany moga handlowac); po prostu nikt nie wsiada.
        /// </summary>
        public static void CamelCulling(object __instance)
        {
            try
            {
                var tr = Traverse.Create(__instance);
                var party = tr.Field("_party").GetValue() as MobileParty;
                var list = tr.Field("_horseAndHarnesses").GetValue() as System.Collections.IList;
                if (party == null || list == null || list.Count == 0) return;
                string pc = PartyCultureId(party);
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    var horse = Traverse.Create(list[i]).Property("Horse").GetValue();
                    var item = horse != null ? Traverse.Create(horse).Property("Item").GetValue() as ItemObject : null;
                    if (item == null) continue;
                    var id = (item.StringId ?? "").ToLowerInvariant();
                    if (!id.Contains("camel") && !id.Contains("chariot") && !id.Contains("elephant")) continue;
                    var ic = item.Culture != null ? item.Culture.StringId : null;
                    if (ic == null || ic == pc) continue;   // wlasna kultura (albo bezpanski) - wolno
                    list.RemoveAt(i);
                }
            }
            catch { }
        }

        // ===== UNIKATY SA UNIKATOWE (Jeff 30.08: "nie moze byc 25 pancerzy
        // Brienny; maja je tylko te postacie i nikt inny") =====
        // Sprzet imiennych bohaterow lore, wyznaczony analiza danych ROT
        // (docs: itemy noszone wylacznie przez lordow, nigdy przez jednostki).
        // Prefiksy id - "jaime_leather" i mundury domow (Baratheon/Valyrian...)
        // CELOWO poza lista: nosza je cale oddzialy, to uniformy, nie unikaty.
        private static readonly string[] UniquePrefixes = {
            "aemon_", "baelish_", "blackfyre_", "brienne_", "cersei_", "dany_",
            "euron_crown", "hound_", "houndskull", "joffrey_crown", "robb_crown",
            "baratheon_crown", "renly_crown", "renly_armor", "renly_shoulders",
            "melisandre_", "nightking_", "ramsay_", "rhaegar_", "stannis_",
            "tyrion_", "varys_", "bull_helmet"
        };

        internal static bool IsUniqueGear(ItemObject it)
        {
            if (it == null) return false;
            var id = it.StringId ?? "";
            for (int i = 0; i < UniquePrefixes.Length; i++)
                if (id.StartsWith(UniquePrefixes[i], StringComparison.Ordinal)) return true;
            return false;
        }

        // ===== KAZDY UNIKAT MA DOM (Jeff 31.08: "ubierz postacie ktore
        // istnieja; nie zyja - spadkobiercom; nie ma ich - jedna sztuka lezy
        // w miescie historycznie poprawnym") =====
        // Wpis: item|wlasciciel|spadkobierca|miasto|slot(H/B/L/G/C)|bat/civ
        // Kaskada: zywy wlasciciel -> zywy spadkobierca -> polka wskazanego
        // miasta (1 szt.). Puste imie = od razu polka (wariant zapasowy albo
        // wlasciciel dawno martwy bez wolnego dziedzica - np. zestaw Rhaegara
        // to trofea znad Tridentu w Krolewskiej Przystani, bo Aegon nosi juz
        // Blackfyre'a; Aemon Smoczy Rycerz lezy w Dragonstone - CELOWO bez
        // imienia, zeby nie ubrac 100-letniego maestera Aemona z Muru).
        // Tryb "bag" (Jeff 31.08): sztuka wjezdza do TABORU partii wlasciciela
        // jako zapas - nightking_armor (wariant bez kolcow) wozi sam Nocny Krol
        // i wypada z lupow dopiero po rozbiciu jego partii.
        private static readonly string[] NamesakeGear = {
            "nightking_armor|Night King||||bag",
            "ramsay_armor|Ramsay||Barrowton|B|bat",
            "ramsay_helmet|Ramsay||Barrowton|H|bat",
            "ramsay_gloves|Ramsay||Barrowton|G|bat",
            "ramsay_boots|Ramsay||Barrowton|L|bat",
            "ramsay_shoulders2|Ramsay||Barrowton|C|bat",
            "ramsay_shoulders|||Barrowton||",
            "cersei_armor|Cersei||King's Landing|B|bat",
            "cersei_crown|Cersei||King's Landing|H|civ",
            "cersei_dress|Cersei||King's Landing|B|civ",
            "cersei_red_dress|||King's Landing||",
            "stannis_armor|Stannis||Dragonstone|B|bat",
            "stannis_cape|Stannis||Dragonstone|C|bat",
            "baratheon_crown|Robert Baratheon|Stannis|Dragonstone|H|civ",
            "dany_dress|Daenerys||Pentos|B|bat",
            "dany_sash|Daenerys||Pentos|C|bat",
            "dany_boots|Daenerys||Pentos|L|bat",
            "dany_hair|Daenerys||Pentos|H|bat",
            "joffrey_crown|Joffrey|Tommen|King's Landing|H|civ",
            "renly_armor|Renly||Storm's End|B|bat",
            "renly_shoulders_cloak|Renly||Storm's End|C|bat",
            "renly_crown|Renly||Storm's End|H|civ",
            "euron_crown|Euron||Pyke|H|civ",
            "rhaegar_plate|||King's Landing||",
            "rhaegar_helmet|||King's Landing||",
            "rhaegar_gauntlets|||King's Landing||",
            "rhaegar_boots|||King's Landing||",
            "rhaegar_pauldrons|||King's Landing||",
            "aemon_armor|||Dragonstone||",
            "aemon_helmet|||Dragonstone||",
            "aemon_gauntlets|||Dragonstone||",
            "aemon_boots|||Dragonstone||",
            "aemon_pauldrons|||Dragonstone||",
            "brienne_armor|||Storm's End||",
            "houndskull|||Lannisport||"
        };

        /// <summary>Id relikwii kierowanych na polki miast - czystka targow
        /// (UniqueWares) MUSI je omijac, inaczej zjadalaby je co sesje.</summary>
        internal static readonly System.Collections.Generic.HashSet<string> RelicIds = BuildRelicIds();
        private static System.Collections.Generic.HashSet<string> BuildRelicIds()
        {
            var h = new System.Collections.Generic.HashSet<string>();
            foreach (var row in NamesakeGear) h.Add(row.Split('|')[0]);
            return h;
        }

        /// <summary>Jednorazowe akcje (per kampania) - ubrania i polozenia; save w MendsBehavior.</summary>
        internal static System.Collections.Generic.List<string> UniqueHomesDone =
            new System.Collections.Generic.List<string>();

        internal static void DressTheNamesakes()
        {
            try
            {
                int dressed = 0, placed = 0, skipped = 0;
                foreach (var row in NamesakeGear)
                {
                    var p = row.Split('|');
                    string id = p[0], owner = p[1], heir = p[2], townName = p[3], slotS = p[4], mode = p[5];
                    if (UniqueHomesDone.Contains(id)) continue;
                    var item = TaleWorlds.ObjectSystem.MBObjectManager.Instance.GetObject<ItemObject>(id);
                    if (item == null) { skipped++; continue; }

                    if (mode == "bag")
                    {
                        // zapas w taborze partii wlasciciela; wlasciciel bez
                        // partii (np. NK przed inwazja) = czekamy do nastepnej sesji
                        var carrier = FindAliveHero(owner);
                        var pr = carrier != null && carrier.PartyBelongedTo != null
                            ? carrier.PartyBelongedTo.ItemRoster : null;
                        if (pr != null)
                        {
                            pr.AddToCounts(item, 1);
                            UniqueHomesDone.Add(id);
                            placed++;
                            Scribe.Line("Mends: " + item.Name + " (" + id + ") wjezdza do taboru " + carrier.Name + " jako zapas.");
                        }
                        else { skipped++; Scribe.Line("Mends: " + owner + " bez partii - " + id + " czeka na wlasciciela."); }
                        continue;
                    }

                    Hero wearer = FindAliveHero(owner);
                    if (wearer == null) wearer = FindAliveHero(heir);
                    if (wearer != null && slotS.Length > 0)
                    {
                        int slot = SlotOf(slotS);
                        var eq = mode == "civ" ? wearer.CivilianEquipment : wearer.BattleEquipment;
                        if (slot >= 0 && eq != null)
                        {
                            eq[(EquipmentIndex)slot] = new EquipmentElement(item);
                            UniqueHomesDone.Add(id);
                            dressed++;
                            Scribe.Line("Mends: " + wearer.Name + " zaklada " + item.Name + " (" + id + ").");
                            continue;
                        }
                    }
                    // nikt zywy nie nosi - jedna sztuka na polke wskazanego miasta
                    var town = FindTown(townName);
                    if (town != null && town.Owner != null && town.Owner.ItemRoster != null)
                    {
                        town.Owner.ItemRoster.AddToCounts(item, 1);
                        UniqueHomesDone.Add(id);
                        placed++;
                        Scribe.Line("Mends: " + item.Name + " (" + id + ") lezy na targu w " + town.Name + " - 1 sztuka.");
                    }
                    else { skipped++; Scribe.Line("Mends: brak miasta '" + townName + "' dla " + id + " - relikwia czeka."); }
                }
                Scribe.Line("Mends: kazdy unikat ma dom - ubrano " + dressed + ", polozono na targach "
                            + placed + (skipped > 0 ? ", pominieto " + skipped : "") + ".");
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "Mends.DressTheNamesakes", null); } catch { } }
        }

        private static Hero FindAliveHero(string namePart)
        {
            try
            {
                if (string.IsNullOrEmpty(namePart)) return null;
                foreach (var h in Hero.AllAliveHeroes)
                {
                    if (h == null || h.IsDead) continue;
                    var n = h.Name != null ? h.Name.ToString() : "";
                    if (n.IndexOf(namePart, StringComparison.OrdinalIgnoreCase) >= 0) return h;
                }
            }
            catch { }
            return null;
        }

        private static Town FindTown(string namePart)
        {
            try
            {
                if (string.IsNullOrEmpty(namePart)) return null;
                foreach (var s in Settlement.All)
                {
                    if (s == null || s.Town == null || s.Town.IsCastle) continue;
                    var n = s.Name != null ? s.Name.ToString() : "";
                    if (n.IndexOf(namePart, StringComparison.OrdinalIgnoreCase) >= 0) return s.Town;
                }
            }
            catch { }
            return null;
        }

        private static int SlotOf(string s)
        {
            switch (s)
            {
                case "H": return 5;   // Head
                case "B": return 6;   // Body
                case "L": return 7;   // Leg
                case "G": return 8;   // Gloves
                case "C": return 9;   // Cape
                default: return -1;
            }
        }

        /// <summary>Przy starcie sesji: unikaty schodza z handlu (NotMerchandise,
        /// wiec targi przestaja je LOSOWAC) i znikaja z polek istniejacych miast.
        /// Ekwipunek bohaterow, stash i sakwy gracza - nietykane: zdobyty
        /// egzemplarz pozostaje jedyny na swiecie.</summary>
        internal static void UniqueWares()
        {
            try
            {
                int flagged = 0, purged = 0;
                var setter = AccessTools.PropertySetter(typeof(ItemObject), "NotMerchandise");
                foreach (var it in TaleWorlds.ObjectSystem.MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
                {
                    if (it == null || !IsUniqueGear(it) || it.NotMerchandise) continue;
                    if (setter != null) { setter.Invoke(it, new object[] { true }); flagged++; }
                }
                foreach (var st in Settlement.All)
                {
                    var t = st.Town;
                    var ro = (t != null && t.Owner != null) ? t.Owner.ItemRoster : null;
                    if (ro == null) continue;
                    for (int i = ro.Count - 1; i >= 0; i--)
                    {
                        var it = ro.GetItemAtIndex(i);
                        if (it == null || !IsUniqueGear(it)) continue;
                        if (RelicIds.Contains(it.StringId)) continue;   // relikwie polozone celowo - nie zjadac
                        int n = ro.GetElementNumber(i);
                        purged += n;
                        ro.AddToCounts(ro.GetElementCopyAtIndex(i).EquipmentElement, -n);
                    }
                }
                Scribe.Line("Mends: unikaty imienne - " + flagged + " itemow zeszlo z handlu, "
                            + purged + " kopii zdjetych z targow miast.");
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "Mends.UniqueWares", null); } catch { } }
        }

        /// <summary>
        /// BRAMA KUCIA LORE (Jeff 30.08: "troszke bez sensu, ze moge kuc miecz
        /// Nocnego Krola od poczatku"). ROT rozdaje za darmo (is_default) 140
        /// czesci tieru 5 - w tym klingi imienne: Needle, Lightbringer, Widow's
        /// Wail, Dark Sister, Tempest... Odbieramy im darmowosc: czesci T5+
        /// trzeba ODBLOKOWAC kuciem, a vanilla otwiera czesci od NAJNIZSZEGO
        /// dostepnego tieru - wiec klingi lore wypadna na samym koncu progresji,
        /// dokladnie jak chcial Jeff ("kuj od T1, w koncu sie udostepnia").
        /// Czesci T4 i nizsze zostaja darmowe jak w ROT.
        /// </summary>
        internal static void LoreForgeGate()
        {
            try
            {
                var setter = AccessTools.PropertySetter(typeof(CraftingPiece), "IsGivenByDefault");
                if (setter == null) { Scribe.Line("Mends: CraftingPiece.IsGivenByDefault bez settera - brama kucia spi."); return; }
                int gated = 0;
                foreach (var cp in TaleWorlds.ObjectSystem.MBObjectManager.Instance.GetObjectTypeList<CraftingPiece>())
                {
                    if (cp == null || !cp.IsGivenByDefault || cp.PieceTier < 5) continue;
                    setter.Invoke(cp, new object[] { false });
                    gated++;
                }
                Scribe.Line("Mends: brama kucia - " + gated + " czesci T5+ (w tym klingi imienne) przestalo byc darmowych; odblokowuja sie kuciem od dolu.");
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "Mends.LoreForgeGate", null); } catch { } }
        }

        /// <summary>Prefix na DTE DoAssignAsync: unikaty imienne nie wchodza do
        /// puli przydzialu - zaden szeregowy nie dostanie pancerza Brienny,
        /// chocby lezal w taborze. Bohaterom DTE i tak sprzetu nie rusza.
        /// WYJATEK (Jeff 30.08): wzor NAUCZONY w kuzni (przetopiony unikat,
        /// Armoury.UniqueLore) jest odblokowany - gracz go kuje, wiec wykute
        /// egzemplarze wolno nosic wojsku. DTE nadal rozdaje tylko FIZYCZNE
        /// sztuki z taboru - zadna kopia nie bierze sie z powietrza.</summary>
        public static void UniqueWard(object __instance)
        {
            try
            {
                var dic = Traverse.Create(__instance).Field("_equipmentToAssign").GetValue() as System.Collections.IDictionary;
                if (dic == null || dic.Count == 0) return;
                var drop = new System.Collections.Generic.List<object>();
                foreach (System.Collections.DictionaryEntry kv in dic)
                {
                    var item = Traverse.Create(kv.Key).Property("Item").GetValue() as ItemObject;
                    if (item == null) continue;
                    // sprzet umarlych NIGDY dla zywych (Jeff: "nic martwego nie
                    // moze byc uzywane przez ludzi") - bez wyjatku nauki
                    if (IsDeadGear(item)) { drop.Add(kv.Key); continue; }
                    // smoki nie dla szeregowych - NIGDY (Jeff: "smoki ma tylko Daenerys")
                    if (IsDragonMount(item)) { drop.Add(kv.Key); continue; }
                    if (IsUniqueGear(item) && !LearnedUnique(item.StringId)) drop.Add(kv.Key);
                }
                for (int i = 0; i < drop.Count; i++) dic.Remove(drop[i]);
            }
            catch { }
        }

        /// <summary>
        /// SWIETA ZASADA SKILLI TAKZE W DTE (Jeff 31.08: "NAPRAW"). DTE ubiera
        /// szeregowych z limitem tierowym, ale BEZ ogladania Difficulty vs
        /// umiejetnosc jednostki - regula nadrzedna repo byla lamanina obok
        /// kwatermistrza. Postfix na DoAssignAsync: po przydziale kazdy slot
        /// z przedmiotem ponad umiejetnosc (vanillowe kryterium: Difficulty >
        /// GetSkillValue(RelevantSkill)) schodzi z grzbietu - jednostka idzie
        /// bez tej sztuki, a sztuka NIE przepada (pula DTE to odbicie taboru,
        /// nie magazyn - tabor zostaje nietkniety).
        /// </summary>
        public static void SkillLawWard(object __instance)
        {
            try
            {
                var list = Traverse.Create(__instance).Property("Assignments").GetValue() as System.Collections.IEnumerable;
                if (list == null) list = Traverse.Create(__instance).Field("Assignments").GetValue() as System.Collections.IEnumerable;
                if (list == null) return;
                int stripped = 0;
                foreach (var a in list)
                {
                    var tr = Traverse.Create(a);
                    var co = tr.Property("Character").GetValue() as CharacterObject;
                    var eq = tr.Property("Equipment").GetValue() as Equipment;
                    if (eq == null) eq = tr.Field("Equipment").GetValue() as Equipment;
                    if (co == null || eq == null || co.IsHero) continue;
                    for (int s = 0; s <= 11; s++)
                    {
                        ItemObject it;
                        try { it = eq[(EquipmentIndex)s].Item; } catch { continue; }
                        if (it == null || it.Difficulty <= 0 || it.RelevantSkill == null) continue;
                        if (co.GetSkillValue(it.RelevantSkill) >= it.Difficulty) continue;
                        try { tr.Method("SetEquipment", (EquipmentIndex)s, default(EquipmentElement)).GetValue(); stripped++; }
                        catch { try { eq[(EquipmentIndex)s] = default(EquipmentElement); stripped++; } catch { } }
                    }
                }
                if (stripped > 0)
                    Scribe.Line("Mends: swieta zasada skilli w DTE - zdjeto " + stripped + " sztuk ponad umiejetnosci jednostek.");
            }
            catch { }
        }

        // ===== SMOKI TYLKO DLA DAENERYS (Jeff 31.08: "smoki ma TYLKO Daenerys!
        // plus gracz, jesli wykona questa; wszystkie inne wywal - nie ma smokow
        // i nie da sie ich zdobyc!") =====
        // Zrodla smokow w ROT: Daenerys (kod ROT + odzysk po niewoli), quest
        // Valyrian Thief (gracz dostaje dragon_red - ZOSTAJE), dialog "Your
        // dragon will fight for me now" (zabieranie smoka pokonanym - BLOKUJEMY),
        // lupy po bitwie -> DTE sadza kawalerzyste na smoku (BLOKUJEMY+CZYSCIMY).

        internal static bool IsDragonMount(ItemObject it)
        {
            try
            {
                if (it == null) return false;
                var id = it.StringId ?? "";
                if (id.StartsWith("dragon_", StringComparison.Ordinal) && it.ItemType == ItemObject.ItemTypeEnum.Horse) return true;
                var mu = it.HorseComponent != null && it.HorseComponent.Monster != null
                    ? (it.HorseComponent.Monster.MonsterUsage ?? "") : "";
                return mu == "dragon" || mu == "dragonfly";
            }
            catch { return false; }
        }

        /// <summary>Czystka smokow: bohaterom spoza klanu gracza i sponad Daenerys
        /// smok schodzi z siodla; rostery partii AI i targi miast czyszczone;
        /// itemy smocze poza handel. Wolane na sesji i co dzien (bitwy w tle
        /// przenosza smoki jako lup).</summary>
        internal static void DragonPurge(bool shout)
        {
            try
            {
                var dany = FindAliveHero("Daenerys");
                int unsaddled = 0, swept = 0;
                foreach (var h in Hero.AllAliveHeroes)
                {
                    if (h == null || h == dany || h.Clan == Clan.PlayerClan) continue;
                    var eq = h.BattleEquipment;
                    if (eq == null) continue;
                    var it = eq[(EquipmentIndex)10].Item;      // ArmorItemEndSlot = kon
                    if (it == null || !IsDragonMount(it)) continue;
                    eq[(EquipmentIndex)10] = default(EquipmentElement);
                    unsaddled++;
                    Scribe.Line("Mends: smok " + it.StringId + " zdjety spod " + h.Name + " - smoki ma tylko Daenerys.");
                }
                foreach (var mp in MobileParty.All)
                {
                    if (mp == null || mp == MobileParty.MainParty || mp.ItemRoster == null) continue;
                    var ro = mp.ItemRoster;
                    for (int i = ro.Count - 1; i >= 0; i--)
                    {
                        var it = ro.GetItemAtIndex(i);
                        if (it == null || !IsDragonMount(it)) continue;
                        int n = ro.GetElementNumber(i);
                        swept += n;
                        ro.AddToCounts(ro.GetElementCopyAtIndex(i).EquipmentElement, -n);
                    }
                }
                foreach (var st in Settlement.All)
                {
                    var t = st.Town;
                    var ro = (t != null && t.Owner != null) ? t.Owner.ItemRoster : null;
                    if (ro == null) continue;
                    for (int i = ro.Count - 1; i >= 0; i--)
                    {
                        var it = ro.GetItemAtIndex(i);
                        if (it == null || !IsDragonMount(it)) continue;
                        int n = ro.GetElementNumber(i);
                        swept += n;
                        ro.AddToCounts(ro.GetElementCopyAtIndex(i).EquipmentElement, -n);
                    }
                }
                var setter = AccessTools.PropertySetter(typeof(ItemObject), "NotMerchandise");
                if (setter != null)
                    foreach (var it in TaleWorlds.ObjectSystem.MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
                        if (IsDragonMount(it) && !it.NotMerchandise) setter.Invoke(it, new object[] { true });
                if (shout || unsaddled + swept > 0)
                    Scribe.Line("Mends: smoki tylko dla Daenerys - zdjeto z siodel " + unsaddled
                                + ", wymieciono z obiegu " + swept + " (quest gracza nietkniety).");
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "Mends.DragonPurge", null); } catch { } }
        }

        /// <summary>Blokada dialogu zabierania smoka pokonanym ("Your dragon
        /// will fight for me now") - jedyna droga gracza do smoka to quest.</summary>
        public static bool NoDragonGank(ref bool __result) { __result = false; return false; }

        /// <summary>Sprzet umarlych: lodowe bronie Innych i martwe wierzchowce.
        /// Zywi tego nie tkna - lod topnieje, martwe ciało sie rozpada.</summary>
        internal static bool IsDeadGear(ItemObject it)
        {
            try
            {
                if (it == null) return false;
                var cu = it.Culture != null ? (it.Culture.StringId ?? "") : "";
                if (cu == "wights" || cu == "whitewalker") return true;
                var id = it.StringId ?? "";
                return id.StartsWith("ice_", StringComparison.Ordinal)
                    || id.StartsWith("wight_", StringComparison.Ordinal)
                    || id == "nightking_blade" || id == "white_walker_saddle";
            }
            catch { return false; }
        }

        /// <summary>Po bitwie z udzialem gracza: lupy z lodu i martwego ciala
        /// ROZPUSZCZAJA SIE (Jeff: "niszczone jest zawsze"). Partia gracza-Othera
        /// zachowuje swoje - umarli moga nosic umarle.</summary>
        public static void MeltDeadLoot(TaleWorlds.CampaignSystem.MapEvents.MapEvent mapEvent)
        {
            try
            {
                var main = MobileParty.MainParty;
                if (main == null || mapEvent == null) return;
                bool involved = main.MapEvent == mapEvent
                    || (Hero.MainHero != null && mapEvent.InvolvedParties != null
                        && System.Linq.Enumerable.Contains(mapEvent.InvolvedParties, main.Party));
                if (!involved) return;
                var cu = Hero.MainHero != null && Hero.MainHero.Culture != null ? Hero.MainHero.Culture.StringId : "";
                if (cu == "whitewalker" || cu == "wights") return;
                var ro = main.ItemRoster;
                if (ro == null) return;
                int melted = 0;
                for (int i = ro.Count - 1; i >= 0; i--)
                {
                    var it = ro.GetItemAtIndex(i);
                    if (it == null || !IsDeadGear(it)) continue;
                    int n = ro.GetElementNumber(i);
                    melted += n;
                    ro.AddToCounts(ro.GetElementCopyAtIndex(i).EquipmentElement, -n);
                }
                if (melted > 0)
                    InformationManager.DisplayMessage(new InformationMessage(
                        "The ice of the Others melts and dead flesh crumbles - " + melted + " trophies turn to nothing in your hands.",
                        Colors.Cyan));
            }
            catch { }
        }

        private static System.Reflection.FieldInfo _fUniqueLore;
        private static bool _uniqueLoreResolved;

        /// <summary>Czy gracz opanowal wzor unikatu (Armoury.ArmouryBehavior.UniqueLore
        /// przez reflection - CrashScribe nie referencuje Armoury; brak Armoury = nic
        /// nie nauczono).</summary>
        private static bool LearnedUnique(string itemId)
        {
            try
            {
                if (!_uniqueLoreResolved)
                {
                    _uniqueLoreResolved = true;
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        try
                        {
                            if (asm.GetName().Name != "Armoury") continue;
                            var t = asm.GetType("Armoury.ArmouryBehavior");
                            _fUniqueLore = t != null ? AccessTools.Field(t, "UniqueLore") : null;
                            break;
                        }
                        catch { }
                    }
                }
                var list = _fUniqueLore != null
                    ? _fUniqueLore.GetValue(null) as System.Collections.Generic.List<string> : null;
                return list != null && list.Contains(itemId);
            }
            catch { return false; }
        }

        private static string PartyCultureId(MobileParty p)
        {
            try
            {
                if (p.LeaderHero != null && p.LeaderHero.Culture != null) return p.LeaderHero.Culture.StringId;
                var f = p.MapFaction;
                if (f != null && f.Culture != null) return f.Culture.StringId;
            }
            catch { }
            return "";
        }

        /// <summary>Najwyzszy tier broni z wzorcow bojowych jednostki (tarcze
        /// nie sa stala bojowa i nie licza sie). PULAPKA: ItemTiers.Tier1 == 0.</summary>
        private static int BestWeaponTier(CharacterObject c)
        {
            try
            {
                if (c == null) return 0;
                int best = 0;
                foreach (var eq in c.BattleEquipments)
                {
                    if (eq == null) continue;
                    for (int s = 0; s <= 3; s++)                 // sloty broni Weapon0..Weapon3
                    {
                        var it = eq[(EquipmentIndex)s].Item;
                        if (it == null || it.WeaponComponent == null) continue;
                        var pw = it.WeaponComponent.PrimaryWeapon;
                        if (pw != null && pw.IsShield) continue;
                        int t = (int)it.Tier + 1;
                        if (t > best) best = t;
                    }
                }
                return best;
            }
            catch { return 0; }
        }

        /// <summary>
        /// VALYRIANSKA ZASADA T6 W AUTOKALKULACJI (Jeff 30.08: "AI walczy z AI
        /// na zasadzie autokalkulacji - trzeba te przewage zaznaczyc"). Postfix
        /// (Priority.Last, po faktorach BannerKings) TYLKO na bazowym
        /// DefaultCombatSimulationModel.SimulateHit - ROT-owy model deleguje
        /// do bazowego, wiec latanie obu cieloby podwojnie (15% x 15%).
        /// Cios jednostki bez broni T6+ w Wedrowca/Nocnego Krola tnie sie do
        /// 15% jak w polu. Smoki celowo NIE sa ciete: ROT nadpisuje ich wynik
        /// PO nas (DragonDamageScaling) - smoczy ogien pali Innych, jak w lore.
        /// </summary>
        public static void ValyrianWardSim(CharacterObject __0, CharacterObject __1, ref ExplainedNumber __result)
        {
            try
            {
                if (__result.ResultNumber <= 1f) return;
                if (!WalkerBlood(__1)) return;                   // __1 = trafiany
                if (BestWeaponTier(__0) >= 6) return;            // __0 = bijacy
                float cut = __result.ResultNumber * 0.15f;
                if (cut < 1f) cut = 1f;
                __result = new ExplainedNumber(cut);
            }
            catch { }
        }

        /// <summary>
        /// WLASNY MUNDUR KAZDY UMIE NOSIC (Jeff 30.08, po spisie
        /// docs/ROT-rozjazdy-skilli.md: 548 rozjazdow, 169 jednostek).
        /// ROT wpisuje jednostkom pancerze z difficulty, ale skille "na
        /// odczepnego" (milicje maja Atletyke 0 przy zbroi 120!) - vanilla
        /// tego nie sprawdza, nasza zasada nadrzedna tak, wiec egzekutor
        /// degradowal wlasne mundury (giganci bez skory, Zlote Plaszcze
        /// bez plaszczy). Podbijamy Atletyke jednostki do najwyzszego
        /// difficulty pancerza z JEJ WLASNEGO wzorca - ale z SUFITEM WEDLE
        /// TIERU (Jeff: "tier 2 nie moze miec Atletyki na poziom tieru 6"):
        /// sufit = 20 + 30 * tier (t1=50, t2=80, t3=110, t4=140, t5=170,
        /// t6=200). Rekrut z wpisana zbroja 120 nadal jej NIE uniesie -
        /// degradacja zostaje, bo to juz wina danych ROT, nie skilla.
        /// Zasada nadrzedna dalej gryzie przy przydziale CUDZEGO sprzetu.
        /// (Zastepuje wczesniejszy mend GiantSinew - giganci, level 26 =
        /// wysoki tier, dostaja z tej reguly dokladnie swoje 150.)
        /// </summary>
        /// <summary>
        /// PRAWO WAGI (Jeff 31.08, korekta: "daj 0.2 kg na punkt" - kazdy punkt
        /// Atletyki niesie 0.2 kg pancerza: 100 to 20 kg, 200 to 40 kg).
        /// Audyt: 46% z 1841 pancerzy mialo difficulty 0, w tym 105 CIEZKICH
        /// plyt >=8 kg bez zadnych wymagan (Volantene Heavy 31 kg za darmo).
        /// Odtad wymaganie = max(stare, waga x 5) - podnosimy tylko W GORE,
        /// wiec celowe blokady person (zbroja NK 250, suknie Daenerys 200)
        /// zostaja. Wagi sa wspolne dla XML i gry (RBM przelicza OCHRONE,
        /// wagi nie rusza). Biega PRZED SkillSinew, zeby mundury jednostek
        /// dostaly atletyke pod NOWE wymagania.
        /// </summary>
        internal static void WeightLaw()
        {
            try
            {
                var setter = AccessTools.PropertySetter(typeof(ItemObject), "Difficulty");
                if (setter == null) { Scribe.Line("Mends: ItemObject.Difficulty bez settera - prawo wagi spi."); return; }
                int raised = 0;
                foreach (var it in TaleWorlds.ObjectSystem.MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
                {
                    if (it == null) continue;
                    var ty = it.ItemType;
                    if (ty != ItemObject.ItemTypeEnum.HeadArmor && ty != ItemObject.ItemTypeEnum.BodyArmor
                        && ty != ItemObject.ItemTypeEnum.LegArmor && ty != ItemObject.ItemTypeEnum.HandArmor
                        && ty != ItemObject.ItemTypeEnum.Cape) continue;
                    int want = (int)Math.Round(it.Weight * 5f);
                    if (want <= it.Difficulty) continue;
                    setter.Invoke(it, new object[] { want });
                    raised++;
                }
                Scribe.Line("Mends: prawo wagi - Atletyka niesie 0.25 kg/pkt; wymagania podniesione " + raised + " pancerzom (max(stare, waga x 5) = 0.2 kg/pkt).");
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "Mends.WeightLaw", null); } catch { } }
        }

        internal static bool SinewApplied;

        internal static void SkillSinew()
        {
            try
            {
                int fixedN = 0, capped = 0, seen = 0;
                foreach (var co in TaleWorlds.ObjectSystem.MBObjectManager.Instance
                             .GetObjectTypeList<CharacterObject>())
                {
                    if (co == null || co.IsHero) continue;
                    int maxDiff = 0;
                    try
                    {
                        foreach (var eq in co.BattleEquipments)
                        {
                            if (eq == null) continue;
                            for (int s = 5; s <= 9; s++)     // Head/Body/Leg/Gloves/Cape
                            {
                                var it = eq[(EquipmentIndex)s].Item;
                                if (it != null && it.Difficulty > maxDiff) maxDiff = it.Difficulty;
                            }
                        }
                    }
                    catch { }
                    if (maxDiff <= 0) continue;
                    seen++;
                    int ath = co.GetSkillValue(DefaultSkills.Athletics);
                    if (ath >= maxDiff) continue;
                    int cap = 20 + 30 * Math.Max(0, co.Tier);
                    int target = Math.Min(maxDiff, cap);
                    if (target <= ath) { capped++; continue; }   // sufit tieru nizej niz mundur - zostaje degradacja
                    var skills = co.GetDefaultCharacterSkills();
                    var owner = skills != null ? skills.Skills : null;
                    var mSet = owner != null ? AccessTools.Method(owner.GetType(), "SetPropertyValue") : null;
                    if (mSet == null)
                    {
                        Scribe.Line("Mends: SkillSinew - brak Skills/SetPropertyValue na " + co.StringId + ", mend spi.");
                        return;
                    }
                    mSet.Invoke(owner, new object[] { DefaultSkills.Athletics, target });
                    fixedN++;
                    if (maxDiff - ath >= 50)
                        Scribe.Line("Mends: " + co.StringId + " (tier " + co.Tier + ") - Atletyka "
                                    + ath + " -> " + target + " (mundur difficulty " + maxDiff
                                    + (target < maxDiff ? ", sufit tieru " + cap : "") + ").");
                    if (target < maxDiff) capped++;
                }
                // NOWA GRA (31.08): na swiezej kampanii SessionLaunched biegnie
                // ZANIM ekwipunki jednostek sie zmaterializuja - wtedy seen == 0
                // i mend powtarza sie z pierwszym tickiem dnia (save'y mialy 169)
                SinewApplied = seen > 0;
                if (seen == 0)
                    Scribe.Line("Mends: SkillSinew - ekwipunki jeszcze niezaladowane (nowa gra), powtorze z pierwszym dniem.");
                else
                    Scribe.Line("Mends: SkillSinew - Atletyka podbita " + fixedN
                            + " jednostkom do poziomu wlasnego munduru (sufit 20+30*tier); "
                            + capped + " przypadkow zostalo ponizej munduru (sufit tieru).");
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "Mends.SkillSinew", null); } catch { } }
        }

        internal static void Install(Harmony harmony)
        {
            try
            {
                // ===== A6: MARTWY WIDGET DTE NA PASKU MAPY =====
                // MapArmoryReadinessMixin (wskaznik gotowosci zbrojowni przy
                // zegarze mapy) pada na 1.4.8 w inicjalizacji swojej bazy
                // (AmbiguousMatch w UIExtenderEx) i KAZDE odswiezenie paska
                // rzuca od nowa - 604 wyjatki w sesji 29.08, a wskaznika
                // i tak nie widac. Usypiamy samo odswiezanie; przycisk
                // otwierania zbrojowni ma osobna droge i zostaje.
                var tMix = QuietType("MapArmoryReadinessMixin");
                var mRef = tMix != null ? AccessTools.Method(tMix, "OnRefresh") : null;
                if (mRef != null)
                {
                    harmony.Patch(mRef, prefix: new HarmonyMethod(typeof(Mends), "SkipDteMapWidget"));
                    Scribe.Line("Mends: martwy wskaznik gotowosci DTE na pasku mapy uspiony (bylo 604 wyjatki/sesje).");
                }
                else Scribe.Line("Mends: MapArmoryReadinessMixin nieobecny - nic do usypiania.");
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "Mends.Install(dteWidget)", null); } catch { } }

            try
            {
                // ===== UMARLI NIE ZNAJA STRACHU =====
                // Panika wylaczona kulturze whitewalker (patrz DeadDontPanic).
                // Patchujemy KAZDA zaladowana implementacje BattleMoraleModel
                // (Sandbox, CustomBattle, ewentualne modowe podmiany), zeby
                // mend przezyl kazda konfiguracje modeli misji.
                int deadPatched = 0;
                var tBase = typeof(TaleWorlds.MountAndBlade.ComponentInterfaces.BattleMoraleModel);
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type[] types;
                    try { types = asm.GetTypes(); } catch { continue; }
                    foreach (var t in types)
                    {
                        try
                        {
                            if (t == null || t.IsAbstract || !tBase.IsAssignableFrom(t)) continue;
                            var mPanic = t.GetMethod("CanPanicDueToMorale",
                                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
                                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly);
                            if (mPanic == null) continue;    // nie deklaruje wlasnej - dziedziczy juz zalatana
                            harmony.Patch(mPanic, postfix: new HarmonyMethod(typeof(Mends), "DeadDontPanic"));
                            deadPatched++;
                        }
                        catch { }
                    }
                }
                Scribe.Line("Mends: umarli nie znaja strachu - panika wylaczona kulturze whitewalker ("
                            + deadPatched + " implementacji modelu morale).");
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "Mends.Install(deadPanic)", null); } catch { } }

            try
            {
                // ===== SMOKI TYLKO DLA DAENERYS: dialog ganku zablokowany =====
                Type tGank = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        if (asm.GetName().Name != "ROT") continue;
                        tGank = asm.GetType("ROT.CampaignBehaviors.ROTGankBehavior");
                        break;
                    }
                    catch { }
                }
                var mCan = tGank != null ? AccessTools.Method(tGank, "CanTakeDragon") : null;
                if (mCan != null)
                {
                    harmony.Patch(mCan, prefix: new HarmonyMethod(typeof(Mends), "NoDragonGank"));
                    Scribe.Line("Mends: zabieranie smoka pokonanym zablokowane - do smoka prowadzi tylko quest.");
                }
                else Scribe.Line("Mends: ROTGankBehavior.CanTakeDragon nieznaleziony - gank bez blokady.");
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "Mends.Install(dragonGank)", null); } catch { } }

            try
            {
                // ===== VALYRIANSKA ZASADA T6 =====
                // Bron ponizej T6 ledwie drasnie Wedrowca i Nocnego Krola
                // (patrz ValyrianWard). Zwykle wighty padaja od wszystkiego.
                var mBlow = AccessTools.Method(typeof(TaleWorlds.MountAndBlade.Agent), "RegisterBlow");
                if (mBlow != null)
                {
                    harmony.Patch(mBlow, prefix: new HarmonyMethod(typeof(Mends), "ValyrianWard") { priority = Priority.High });
                    Scribe.Line("Mends: valyrianska zasada T6 - bron ponizej tieru 6 zadaje Bialym Wedrowcom i Nocnemu Krolowi 15% obrazen.");
                }
                else Scribe.Line("Mends: Agent.RegisterBlow nieznaleziony - valyrianska zasada spi.");
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "Mends.Install(valyrian)", null); } catch { } }

            try
            {
                // ===== VALYRIANSKA ZASADA T6 W AUTOKALKULACJI =====
                // Bitwy AI vs AI (i "Send Troops") ida przez symulacje - bez
                // tego Wedrowcy padali tam od zwyklej stali (patrz ValyrianWardSim).
                var tDef = typeof(TaleWorlds.CampaignSystem.GameComponents.DefaultCombatSimulationModel);
                System.Reflection.MethodInfo mSim = null;
                foreach (var m in tDef.GetMethods())
                {
                    if (m.Name != "SimulateHit") continue;
                    var ps = m.GetParameters();
                    if (ps.Length > 0 && ps[0].ParameterType == typeof(CharacterObject)) { mSim = m; break; }
                }
                if (mSim != null)
                {
                    harmony.Patch(mSim, postfix: new HarmonyMethod(typeof(Mends), "ValyrianWardSim") { priority = Priority.Last });
                    Scribe.Line("Mends: valyrianska zasada T6 dziala tez w autokalkulacji bitew (symulacja tnie do 15% jak pole).");
                }
                else Scribe.Line("Mends: DefaultCombatSimulationModel.SimulateHit nieznaleziony - symulacja bez zasady T6.");
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "Mends.Install(valyrianSim)", null); } catch { } }

            try
            {
                // ===== WIELBLADY NIE DLA POLNOCY =====
                // DTE sadza jezdzcow na kazdym zlupionym zwierzeciu; egzotyki
                // obcej kultury schodza z listy przydzialu (patrz CamelCulling).
                Type tDist = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        if (!asm.GetName().Name.StartsWith("DynamicTroop")) continue;
                        tDist = asm.GetType("DynamicTroopEquipmentReupload.PartyEquipmentDistributor");
                        if (tDist != null) break;
                    }
                    catch { }
                }
                var mGen = tDist != null ? AccessTools.Method(tDist, "GenerateHorseAndHarnessList") : null;
                if (mGen != null)
                {
                    harmony.Patch(mGen, postfix: new HarmonyMethod(typeof(Mends), "CamelCulling"));
                    Scribe.Line("Mends: egzotyczne wierzchowce (wielblady, rydwany, slonie) tylko dla wlasnej kultury - DTE nie sadza na nich obcych.");
                }
                else Scribe.Line("Mends: DTE PartyEquipmentDistributor nieznaleziony - egzotyki bez straznika.");

                // ===== UNIKATY NIE DLA SZEREGOWYCH =====
                var mAssign = tDist != null ? AccessTools.Method(tDist, "DoAssignAsync") : null;
                if (mAssign != null)
                {
                    harmony.Patch(mAssign, prefix: new HarmonyMethod(typeof(Mends), "UniqueWard"),
                                  postfix: new HarmonyMethod(typeof(Mends), "SkillLawWard"));
                    Scribe.Line("Mends: sprzet imiennych bohaterow poza pula przydzialu DTE - piechota nie zalozy pancerza Brienny.");
                    Scribe.Line("Mends: swieta zasada skilli obowiazuje w DTE - Difficulty ponad umiejetnosc schodzi z grzbietu.");
                }
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "Mends.Install(camels)", null); } catch { } }

            try
            {
                // Zaciezny w bitwie przy oblezeniu (wycieczka obroncow, "continue_siege
                // _after_attack"): gracz potrafi nie miec formalnej strony w MapEvent
                // (PlayerSide = None) i vanilla GetPlayerBattleContributionRate wali
                // IndexOutOfRange - gra pada na ekranie wynikow. Wklad = 0 i gramy dalej.
                var contrib = AccessTools.Method(typeof(TaleWorlds.CampaignSystem.MapEvents.MapEvent), "GetPlayerBattleContributionRate");
                if (contrib != null)
                {
                    harmony.Patch(contrib, finalizer: new HarmonyMethod(typeof(Mends), "SafeContribution"));
                    Scribe.Line("Mends: contribution-rate crash (zaciezny przy oblezeniu) zmostkowany.");
                }
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "Mends.Install(contrib)", null); } catch { } }

            try
            {
                // ===== SEDNO PUSTYCH EKRANOW BANNER KINGS =====
                // BKROTPatch podmienia CALA inicjalizacje stylow zycia BK wlasnym
                // prefixem (zwraca false, wiec oryginal sie nie wykonuje). Ten prefix
                // wywraca sie w polowie na NullReference - a wtedy:
                //   DefaultLifestyles.Initialize konczy sie w polowie
                //   -> BannerKingsConfig.Initialize() nie dochodzi do konca
                //   -> menedzery BK (dwor, relacje, rekruci) zostaja niekompletne
                //   -> tysiace NullReference przy kazdym odwolaniu (u Jeffa 6000
                //      w kilkanascie minut) i PUSTE EKRANY tam, gdzie BK doklada
                //      swoje rozszerzenia - miedzy innymi ekran REKRUTACJI
                //      (BannerKings.UI.Extensions.VolunteerRecruitmentMixin).
                // Finalizer: wywrotka lataczki -> oddajemy robote ORYGINALNEJ
                // metodzie BannerKings (__result = true). Style zycia beda bez
                // ROT-owych opisow, ale CALA reszta moda wreszcie wstanie.
                var tLif = Type.GetType("BKROTPatch.Patches.DefaultLifestylesInitializePatch, BKROTPatch")
                           ?? QuietType("DefaultLifestylesInitializePatch");
                var mLif = tLif != null ? AccessTools.Method(tLif, "Prefix") : null;
                if (mLif != null)
                {
                    // Najpierw po prostu ZDEJMUJEMY ich lataczke z metody BK - wtedy
                    // oryginal leci czysto, bez zadnego wyjatku i bez polowicznego stanu.
                    bool off = Unhook(harmony, mLif, "BannerKings.Managers.Education.Lifestyles.DefaultLifestyles", "Initialize");
                    if (off)
                        Scribe.Line("Mends: lataczka BKROTPatch zdjeta ze stylow zycia BK - inicjalizacje robi oryginal (Banner Kings wstanie caly).");
                    else
                    {
                        harmony.Patch(mLif, finalizer: new HarmonyMethod(typeof(Mends), "SafeLifestyles"));
                        Scribe.Line("Mends: inicjalizacja stylow zycia BK zabezpieczona (BKROTPatch nie zabije juz calego Banner Kings).");
                    }
                }
                else Scribe.Line("Mends: BKROTPatch/DefaultLifestylesInitializePatch nieobecny - nic do zabezpieczenia.");
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "Mends.Install(lifestyles)", null); } catch { } }

            try
            {
                // ---------------------------------------------------------------
                // BKROTPatch / ReligionGenerateClergymanPatch - CICHY ZABOJCA OSAD.
                // Ich prefix "sprawdza" kulture osady wolajac
                //     NameGenerator.GetNameListForCulture(...)  przez  Invoke(NULL, ...)
                // a ta metoda NIE JEST STATYCZNA. Kazde wywolanie konczy sie
                // TargetException ("Non-static method requires a target"), ich wlasny
                // catch zwraca false - i ORYGINALNY BannerKings.Religion.GenerateClergyman
                // NIGDY NIE LECI. Skutek lawinowy:
                //   zadna osada nie dostaje duchownego
                //   -> ReligionData.Update wywraca sie na nullu
                //   -> PopulationData.Update NIGDY nie dochodzi do konca
                //   -> dane osady BK (ludnosc, milicja, OCHOTNICY DO REKRUTACJI) stoja martwe.
                // W sesji Jeffa z 25.08: 11 487 wyjatkow i pusty ekran rekrutacji.
                // Zdejmujemy TYLKO ten jeden prefix; ich Finalizer zostaje (lapie wywrotki
                // samego generatora BK), reszta BKROTPatch nietknieta.
                // ---------------------------------------------------------------
                var tCle = Type.GetType("BKROTPatch.Patches.ReligionGenerateClergymanPatch, BKROTPatch")
                           ?? QuietType("ReligionGenerateClergymanPatch");
                var mCle = tCle != null ? AccessTools.Method(tCle, "Prefix") : null;
                if (mCle != null)
                {
                    bool off = Unhook(harmony, mCle, "BannerKings.Managers.Institutions.Religions.Religion", "GenerateClergyman");
                    Scribe.Line(off
                        ? "Mends: BKROTPatch nie dusi juz duchownych BK - dane osad (ludnosc, milicja, ochotnicy) licza sie do konca."
                        : "Mends: nie udalo sie zdjac lataczki na duchownych - probuje objazdem.");
                    if (!off)
                        harmony.Patch(mCle, prefix: new HarmonyMethod(typeof(Mends), "LetClergyBe"));
                }
                else Scribe.Line("Mends: BKROTPatch/ReligionGenerateClergymanPatch nieobecny - nic do odblokowania.");
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "Mends.Install(clergy)", null); } catch { } }

            try
            {
                // ---------------------------------------------------------------
                // OCHOTNICY SA MIEJSCOWI. Banner Kings dobiera rekrutow wedle kultury
                // NOTABLA, nie osady:
                //     GetPossibleSpawns(sellerHero.Culture, popType, settlement)
                // a duchownych BK tworzy z presetu wiary:
                //     HeroCreator.CreateSpecialHero(preset, ...)   <- kultura Z PRESETU
                // i dopiero potem wsadza ich do osady. Czyli kaplan zrodzony jako
                // wolny lud siada w polnocnej wsi i wystawia synow Thenna.
                // Jeff (sluszznie): "nie moze byc free folk w wiosce Polnocy".
                // Chlopi z Acorn Water sa Polnocnikami niezaleznie od tego, kto im
                // odprawia obrzedy - wiec pytamy o kulture OSADY, nie kaplana.
                // ---------------------------------------------------------------
                if (Config.LocalRecruits)
                {
                    var tSpawns = AccessTools.TypeByName("BannerKings.Managers.Recruits.DefaultRecruitSpawns");
                    System.Reflection.MethodInfo mSpawns = null;
                    if (tSpawns != null)
                        foreach (var mi in tSpawns.GetMethods(System.Reflection.BindingFlags.Public
                                 | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly))
                        {
                            if (mi.Name != "GetPossibleSpawns") continue;
                            var ps = mi.GetParameters();
                            if (ps.Length == 2 && ps[1].ParameterType == typeof(Settlement)) { mSpawns = mi; break; }
                        }
                    if (mSpawns != null)
                    {
                        harmony.Patch(mSpawns, prefix: new HarmonyMethod(typeof(Mends), "LocalRecruitsPrefix"));
                        Scribe.Line("Mends: ochotnicy ida wedle kultury OSADY, nie notabla (koniec wolnego ludu w wioskach Polnocy).");
                    }
                    else Scribe.Line("Mends: nie znalazlem BK GetPossibleSpawns - ochotnicy zostaja po staremu.");
                }
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "Mends.Install(recruits)", null); } catch { } }

            try
            {
                // BannerKings BKRelationsModel.CalculateModifiers wali NullReference
                // DZIESIEC RAZY NA SEKUNDE (22 tys. powtorek w pol godziny u Jeffa -
                // stad freez): jakis bohater bez kultury/klanu wywraca liczenie
                // relacji. Finalizer: wywrotka -> pusta lista modyfikatorow i gramy.
                var tRel = Type.GetType("BannerKings.Models.BKModels.BKRelationsModel, BannerKings");
                var mRel = tRel != null ? AccessTools.Method(tRel, "CalculateModifiers") : null;
                if (mRel != null)
                {
                    // slad 28.08 nazwal null po imieniu: notabl w osadzie BEZ TYTULU
                    // feudalnego (Essos, np. Tolarra) -> GetTitle(osada) daje null,
                    // a BK robi title.DeFacto = NullReference. 13 wyjatkow NA SEKUNDE
                    // (12,9 tys. w 17 minut) - kazdy lapany, ale samo rzucanie MULI gre.
                    // Brama PRZED metoda wycina znany przypadek bez wyjatku;
                    // finalizer zostaje na wszystko inne.
                    try
                    {
                        var tCfg = Type.GetType("BannerKings.BannerKingsConfig, BannerKings") ?? QuietType("BannerKingsConfig");
                        _bkCfgInstanceGet = tCfg != null ? AccessTools.PropertyGetter(tCfg, "Instance") : null;
                        _bkCfgTitleMgrGet = tCfg != null ? AccessTools.PropertyGetter(tCfg, "TitleManager") : null;
                        var tTm = _bkCfgTitleMgrGet != null ? _bkCfgTitleMgrGet.ReturnType : null;
                        _bkGetTitle = tTm != null ? AccessTools.Method(tTm, "GetTitle", new[] { typeof(Settlement) }) : null;
                    }
                    catch { _bkGetTitle = null; }
                    harmony.Patch(mRel,
                        prefix: new HarmonyMethod(typeof(Mends), "EssosTitleGate"),
                        finalizer: new HarmonyMethod(typeof(Mends), "SafeRelations"));
                    Scribe.Line("Mends: BK CalculateModifiers zabezpieczone (brama Essos przed metoda + finalizer)."
                                + (_bkGetTitle == null ? " UWAGA: GetTitle nieznaleziony - brama spi, zostal sam finalizer." : ""));
                }
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "Mends.Install(relations)", null); } catch { } }

            try
            {
                // ZRODLO lawiny, nie objaw: HeroRelations.UpdateRelations (DailyTickHero)
                // wola GetHeroesToUpdate, a ta dla notabla BEZ OSADY (albo z osada
                // bez wlasciciela) robi Hero.CurrentSettlement.OwnerClan.Heroes
                // i pada NullReference (HeroRelations.cs:115; sesja 26.08: 2500+
                // powtorek, wspolnie z SafeRelations 12 tys. zlapan i freez gry).
                // Notabl bez osady nie ma zadnej listy do aktualizacji - pomijamy
                // go w calosci, ZANIM cokolwiek wybuchnie.
                var tHr = Type.GetType("BannerKings.Behaviours.Relations.HeroRelations, BannerKings")
                          ?? QuietType("HeroRelations");
                var mUpd = tHr != null ? AccessTools.Method(tHr, "UpdateRelations") : null;
                if (mUpd != null)
                {
                    _hrHeroGet = AccessTools.PropertyGetter(tHr, "Hero");
                    harmony.Patch(mUpd, prefix: new HarmonyMethod(typeof(Mends), "RelationsUpdateGate"));
                    Scribe.Line("Mends: relacje BK - notable bez osady pomijani (zrodlo lawiny NullReference).");
                }
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "Mends.Install(relations)", null); } catch { } }

            try
            {
                // PULAPKA NA PIERWOTNY WYJATEK CRASHA. Trzy crashe przy wejsciu
                // w bitwe (28.08) i w logu ZA KAZDYM RAZEM tylko wnetrze reportera
                // BLSE - pierwotny wyjatek byl powtorka i dedup go polknal.
                // Prefix na BLSE HandleException: pelny wyjatek + stos, bez dedupu,
                // do wlasnego pliku crash-original-*.log ZANIM reporter cokolwiek zrobi.
                var tIcept = AccessTools.TypeByName("Bannerlord.BLSE.Features.ExceptionInterceptor.ExceptionInterceptorFeature");
                var mHandle = tIcept != null ? AccessTools.Method(tIcept, "HandleException") : null;
                if (mHandle != null)
                {
                    harmony.Patch(mHandle, prefix: new HarmonyMethod(typeof(Mends), "CrashCallerTattle"));
                    Scribe.Line("Mends: pulapka na pierwotny wyjatek crasha zalozona (przed reporterem BLSE).");
                }
                else Scribe.Line("Mends: UWAGA nie znalazlem BLSE HandleException - pulapki crasha brak.");
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "Mends.Install(crashtrap)", null); } catch { } }

            try
            {
                // KULTURA BEZ IMION: bezpiecznik na NameGenerator.GetNameListForCulture
                // (crash 28.08 przy tworzeniu duchownego BK; szczegoly przy
                // FeedNamelessCultures i NameListSafety)
                var mNames = AccessTools.Method(typeof(TaleWorlds.CampaignSystem.NameGenerator), "GetNameListForCulture");
                if (mNames != null)
                {
                    harmony.Patch(mNames, prefix: new HarmonyMethod(typeof(Mends), "NameListSafety"));
                    Scribe.Line("Mends: NameGenerator z bezpiecznikiem na kultury bez list imion.");
                }
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "Mends.Install(namelist)", null); } catch { } }

            try
            {
                // MARTWY DIALOG DUCHOWNEGO (Jeff 27.08: "click to continue i nic
                // sie nie dzieje"). Powitanie BK i WSZYSTKIE opcje preachera maja
                // warunek ReligionsManager.IsPreacher - a preacher bez wpisu
                // w menedzerze (ROT-owe osady czesto nie maja religii, wiec
                // CleanClergymen BK go nie rejestruje) wypada z wlasnego dialogu:
                // gra pokazuje cudze powitanie, po ktorym nie ma ZADNEJ opcji.
                // Tuz przed warunkiem powitania dopisujemy go do religii.
                var tRelB = Type.GetType("BannerKings.Behaviours.BKReligionsBehavior, BannerKings")
                            ?? QuietType("BKReligionsBehavior");
                var mGreet = tRelB != null ? AccessTools.Method(tRelB, "OnConditionClergymanGreeting") : null;
                if (mGreet != null)
                {
                    harmony.Patch(mGreet, prefix: new HarmonyMethod(typeof(Mends), "RegisterStrayPreacher"));
                    Scribe.Line("Mends: preacher bez rejestru religii dostaje wpis przy rozmowie (martwy dialog).");
                }
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "Mends.Install(preacher)", null); } catch { } }

            try
            {
                // Westeros zna tylko DLUGA zime i dlugie lato - kalendarzyk por roku
                // RealisticBannerlord (co 21 dni wiosna/jesien, zamiecie, kary do marszu,
                // zimowy glod) nie ma tu sensu, a MCM RB i tak nie zapisuje ustawien.
                // Jeff kazal wylaczyc na twardo: getter SeasonsEnabled klamie "false"
                // i caly system Seasons (predkosc, pogoda, jedzenie, morale) spi.
                var tRbSet = Type.GetType("RealisticBannerlord.Settings.RealisticSettings, RealisticBannerlord");
                var gSeasons = tRbSet != null ? AccessTools.PropertyGetter(tRbSet, "SeasonsEnabled") : null;
                if (gSeasons != null)
                {
                    harmony.Patch(gSeasons, postfix: new HarmonyMethod(typeof(Mends), "SeasonsOff"));
                    Scribe.Line("Mends: pory roku RB wylaczone na twardo (Westeros ma zime i lato, nie kalendarzyk).");
                }
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "Mends.Install(seasons)", null); } catch { } }

            try
            {
                // KUZNIA (crash Jeffa 2026-08-24 11:36, Two Handed Mace + Long Steel
                // Mace Handle): mody laduja elementy broni PODWOJNIE (ten sam StringId,
                // INNA instancja obiektu). GenerateCraftedItem porownuje REFERENCJE:
                // klikniety uchwyt "nie nalezy do szablonu" -> item = null -> pierwsze
                // odswiezenie UI (WeaponDesignVM.RefreshStats i spolka) wali
                // NullReference i gra pada. Naprawa: gdy item wyszedl null, podmieniamy
                // obce instancje na blizniaki z szablonu (po StringId) i skladamy znowu.
                var mSetItem = AccessTools.Method(typeof(TaleWorlds.Core.Crafting), "SetItemObject");
                if (mSetItem != null)
                {
                    harmony.Patch(mSetItem, postfix: new HarmonyMethod(typeof(Mends), "SafeSmithy"));
                    Scribe.Line("Mends: kuznia zabezpieczona (podwojnie zaladowany element nie wywraca juz projektowania).");
                }
                // pas i szelki: gdyby item MIMO TO byl null, odswiezenia UI maja
                // nie dotykac pustki zamiast rzucac NullReference w silniku Gauntleta
                var tVm = Type.GetType("TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting.WeaponDesign.WeaponDesignVM, TaleWorlds.CampaignSystem.ViewModelCollection");
                if (tVm != null)
                {
                    string[] guarded = { "DoesCurrentItemHaveSecondaryUsage", "RefreshAlternativeUsageList", "RefreshStats", "AddClassFlagsToPiece" };
                    for (int gi = 0; gi < guarded.Length; gi++)
                    {
                        var mg = AccessTools.Method(tVm, guarded[gi]);
                        if (mg != null) harmony.Patch(mg, prefix: new HarmonyMethod(typeof(Mends), "VmNeedsItem"));
                    }
                }
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "Mends.Install(smithy)", null); } catch { } }

            try
            {
                if (Type.GetType("RealisticBannerlord.Systems.Espionage.LordSpyBehavior, RealisticBannerlord") == null)
                    return;   // moda nie ma - nie ma czego mostkowac
                var run = AccessTools.Method(typeof(ConversationSentence), "RunConsequence");
                if (run == null) { Scribe.Line("Mends: ConversationSentence.RunConsequence not found."); return; }
                harmony.Patch(run, prefix: new HarmonyMethod(typeof(Mends), "SafeSpyFail"));
                Scribe.Line("Mends: RealisticBannerlord spy-arrest crash bridged (RestartPlayerEncounter 3->4 args).");

                // formula skutecznosci przekupstwa: baza 0, honor wyklucza, licza sie
                // wady charakteru, przyjazn, Roguery i sila klanu - nie plaski rzut 20%
                var spyType = Type.GetType("RealisticBannerlord.Systems.Espionage.LordSpyBehavior, RealisticBannerlord");
                var cond = spyType != null ? AccessTools.Method(spyType, "lord_spy_success_condition") : null;
                if (cond != null)
                {
                    harmony.Patch(cond, prefix: new HarmonyMethod(typeof(Mends), "RealSpyChance"));
                    Scribe.Line("Mends: spy bribe odds rebuilt (base 0%, honour is incorruptible).");
                }
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "Mends.Install", null); } catch { } }
        }

        /// <summary>
        /// Finalizer na MapEvent.GetPlayerBattleContributionRate: gdy gracz nie ma
        /// strony w bitwie (sluzba, wycieczka przy oblezeniu), wklad = 0 zamiast crashu.
        /// </summary>
        private static int _relSaves;

        /// <summary>
        /// BK liczy modyfikatory relacji dla bohatera bez kultury/klanu i pada.
        /// Wywrotka -> pusta lista (relacja bez modyfikatorow) zamiast lawiny
        /// wyjatkow mrozacej gre. Log tylko co setny raz, zeby sie nie zapchac.
        /// </summary>
        private static bool _lifestylesSaved;

        /// <summary>
        /// Lataczka BKROTPatch na style zycia sie wywrocila - niech oryginal
        /// Banner Kings dokonczy, zamiast zostawiac pol moda w gruzach.
        /// </summary>
        public static Exception SafeLifestyles(Exception __exception, ref bool __result)
        {
            try
            {
                if (__exception == null) return null;
                __result = true;                       // TAK, wykonaj oryginalny Initialize
                if (!_lifestylesSaved)
                {
                    _lifestylesSaved = true;
                    Scribe.Report("BKROTPatch przerwal inicjalizacje Banner Kings - oddaje robote oryginalowi",
                                  __exception, "DefaultLifestylesInitializePatch.Prefix", null);
                }
                Scribe.Line("Mends: BKROTPatch wywrocil sie na stylach zycia (" + __exception.GetType().Name
                            + ") - inicjalizacje konczy oryginal BannerKings.");
                return null;                           // wyjatek polkniety
            }
            catch { return null; }
        }

        /// <summary>
        /// KULTURA BEZ IMION ZABIJA GRE. Kultura bez wpisow male_names/female_names
        /// w XML ma MaleNameList/FemaleNameList = null, a NameGenerator robi na tym
        /// IsEmpty() -> ArgumentNullException przy tworzeniu KAZDEGO bohatera tej
        /// kultury. Dowod 28.08 11:11: BK generowal duchownego (Religion.
        /// GenerateClergymanHero -> CreateSpecialHero -> GetNameListForCulture),
        /// FirstChance lecial seriami, a o 11:15 gra padla na ApplicationTick przy
        /// wejsciu Jeffa w bitwe. Raz na sesje pozyczamy listy od najbogatszej
        /// kultury - imie bedzie obce, ale gra zyje (kulture duchownego i tak
        /// zaraz prostuje nasza latka "kaplan jest stad").
        /// </summary>
        /// <summary>
        /// Pelny zapis wyjatku, ktory za chwile ubije gre - bez dedupu, do wlasnego
        /// pliku, zanim reporter BLSE zacznie miec (jego wlasne bledy zaslanialy
        /// pierwotny slad trzy razy z rzedu 28.08).
        /// </summary>
        public static void CrashCallerTattle(Exception exception)
        {
            try
            {
                if (exception == null) return;
                var sb = new System.Text.StringBuilder();
                sb.AppendLine();
                sb.AppendLine("#####################################################################");
                sb.AppendLine("# PIERWOTNY WYJATEK CRASHA   " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                sb.AppendLine("# (przechwycony przed reporterem BLSE, bez dedupu)");
                sb.AppendLine("#####################################################################");
                var e = exception; int depth = 0;
                while (e != null && depth++ < 5)
                {
                    sb.AppendLine("TYPE    : " + e.GetType().FullName);
                    sb.AppendLine("MESSAGE : " + e.Message);
                    sb.AppendLine(e.StackTrace ?? "(brak stosu)");
                    e = e.InnerException;
                    if (e != null) sb.AppendLine("--- INNER ---");
                }
                try
                {
                    System.IO.File.WriteAllText(System.IO.Path.Combine(Scribe.ReportDir,
                        "crash-original-" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".log"),
                        sb.ToString(), System.Text.Encoding.UTF8);
                }
                catch { }
                Scribe.TryRaw(sb.ToString(), 3000);
            }
            catch { }
        }

        private static TaleWorlds.Library.MBReadOnlyList<TaleWorlds.Localization.TextObject> _spareMale, _spareFemale;
        private static int _nameSaves;

        /// <summary>
        /// BEZPIECZNIK NA SAMEJ METODZIE IMION. FeedNamelessCultures lata dane
        /// przy starcie sesji, ale gdyby jakas kultura przemknela (dograna pozniej,
        /// spoza object managera), NameGenerator dalej robilby IsEmpty() na null.
        /// Prefix: kultura zdrowa -> normalna droga; dziura -> oddajemy zapasowa
        /// liste najbogatszej kultury i pomijamy oryginal. Zero wyjatkow.
        /// </summary>
        public static bool NameListSafety(CultureObject npcCulture, bool isFemale,
            ref TaleWorlds.Library.MBReadOnlyList<TaleWorlds.Localization.TextObject> __result)
        {
            try
            {
                if (npcCulture != null)
                {
                    var list = isFemale ? npcCulture.FemaleNameList : npcCulture.MaleNameList;
                    if (list != null && list.Count > 0) return true;   // zdrowa - niech gra wybiera sama
                }
                if (_spareMale == null || _spareFemale == null)
                {
                    var mgr = TaleWorlds.ObjectSystem.MBObjectManager.Instance;
                    var all = mgr != null ? mgr.GetObjectTypeList<CultureObject>() : null;
                    if (all == null) return true;
                    int bM = 0, bF = 0;
                    foreach (var c in all)
                    {
                        if (c == null) continue;
                        if (c.MaleNameList != null && c.MaleNameList.Count > bM) { bM = c.MaleNameList.Count; _spareMale = c.MaleNameList; }
                        if (c.FemaleNameList != null && c.FemaleNameList.Count > bF) { bF = c.FemaleNameList.Count; _spareFemale = c.FemaleNameList; }
                    }
                }
                var spare = isFemale ? _spareFemale : _spareMale;
                if (spare == null || spare.Count == 0) return true;   // nie mamy czym ratowac
                __result = spare;
                _nameSaves++;
                if (_nameSaves == 1 || _nameSaves % 50 == 0)
                    try { Scribe.Line("Mends: kultura bez imion (" + (npcCulture != null ? npcCulture.StringId : "null")
                        + ", " + (isFemale ? "zenskie" : "meskie") + ") dostala liste zapasowa - lacznie " + _nameSaves + "."); } catch { }
                return false;
            }
            catch { return true; }
        }

        internal static void FeedNamelessCultures()
        {
            try
            {
                var mgr = TaleWorlds.ObjectSystem.MBObjectManager.Instance;
                var all = mgr != null ? mgr.GetObjectTypeList<CultureObject>() : null;
                if (all == null) return;
                var fMale = AccessTools.Field(typeof(CultureObject), "_maleNameList");
                var fFem = AccessTools.Field(typeof(CultureObject), "_femaleNameList");
                var fClan = AccessTools.Field(typeof(CultureObject), "_clanNameList");
                if (fMale == null || fFem == null || fClan == null) return;

                // dawca OSOBNO dla kazdej listy - pierwsza wersja brala jednego
                // dawce po sumie (wygral... looters) i gdy ten nie mial listy
                // ZENSKIEJ, nakarmione kultury dalej mialy tam null - crash
                // wracal przy duchownej-kobiecie (Jeff 28.08 14:08, druga bitwa)
                CultureObject dM = null, dF = null, dC = null; int bM = 0, bF = 0, bC = 0;
                foreach (var c in all)
                {
                    if (c == null) continue;
                    if (c.MaleNameList != null && c.MaleNameList.Count > bM) { bM = c.MaleNameList.Count; dM = c; }
                    if (c.FemaleNameList != null && c.FemaleNameList.Count > bF) { bF = c.FemaleNameList.Count; dF = c; }
                    if (c.ClanNameList != null && c.ClanNameList.Count > bC) { bC = c.ClanNameList.Count; dC = c; }
                }
                if (dM == null && dF == null && dC == null) return;

                int fed = 0, holes = 0;
                foreach (var c in all)
                {
                    if (c == null) continue;
                    var got = "";
                    if ((c.MaleNameList == null || c.MaleNameList.Count == 0) && dM != null && c != dM)
                    { fMale.SetValue(c, fMale.GetValue(dM)); got += " meskie<-" + dM.StringId; }
                    if ((c.FemaleNameList == null || c.FemaleNameList.Count == 0) && dF != null && c != dF)
                    { fFem.SetValue(c, fFem.GetValue(dF)); got += " zenskie<-" + dF.StringId; }
                    if ((c.ClanNameList == null || c.ClanNameList.Count == 0) && dC != null && c != dC)
                    { fClan.SetValue(c, fClan.GetValue(dC)); got += " klanowe<-" + dC.StringId; }
                    if (got.Length > 0)
                    {
                        fed++;
                        Scribe.Line("Mends: kultura " + c.StringId + " bez imion:" + got + ".");
                    }
                    // samokontrola: czy po karmieniu COKOLWIEK zostalo dziurawe
                    if (c.MaleNameList == null || c.MaleNameList.Count == 0
                        || c.FemaleNameList == null || c.FemaleNameList.Count == 0)
                    { holes++; Scribe.Line("Mends: UWAGA kultura " + c.StringId + " DALEJ bez imion (m="
                        + (c.MaleNameList == null ? "null" : c.MaleNameList.Count.ToString())
                        + " z=" + (c.FemaleNameList == null ? "null" : c.FemaleNameList.Count.ToString()) + ")."); }
                }
                if (fed > 0)
                    Scribe.Line("Mends: " + fed + " kultur bez imion nakarmione, dziurawych zostalo " + holes + ".");
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "Mends.FeedNamelessCultures", null); } catch { } }
        }

        private static System.Reflection.MethodInfo _hrHeroGet;
        private static System.Reflection.MethodInfo _bkCfgInstanceGet, _bkCfgTitleMgrGet, _bkGetTitle;
        private static Type _tListRelMod;
        private static int _essosGated;

        /// <summary>
        /// Notabl w osadzie bez tytulu feudalnego (pol Essos w ROT) to w BK
        /// GWARANTOWANY NullReference (title.DeFacto na null). Oddajemy pusta
        /// liste modyfikatorow - DOKLADNIE to, co po wywrotce oddawal finalizer -
        /// tylko bez rzucania wyjatku, ktory przy 13/s mulil gre.
        /// </summary>
        public static bool EssosTitleGate(object heroRelations, Hero target, ref object __result)
        {
            try
            {
                if (_bkGetTitle == null || _hrHeroGet == null) return true;
                var hero = heroRelations != null ? _hrHeroGet.Invoke(heroRelations, null) as Hero : null;
                if (hero == null || target == null) return true;
                if (!hero.IsNotable || !target.IsLord) return true;   // pada tylko galaz notabl->lord
                var st = hero.CurrentSettlement;
                if (st != null)
                {
                    var cfg = _bkCfgInstanceGet != null ? _bkCfgInstanceGet.Invoke(null, null) : null;
                    var tm = cfg != null && _bkCfgTitleMgrGet != null ? _bkCfgTitleMgrGet.Invoke(cfg, null) : null;
                    var title = tm != null ? _bkGetTitle.Invoke(tm, new object[] { st }) : null;
                    if (title != null) return true;   // osada ma tytul - normalna droga BK
                }
                if (_tListRelMod == null)
                {
                    var t = Type.GetType("BannerKings.Managers.Skills.RelationsModifier, BannerKings")
                            ?? QuietType("RelationsModifier");
                    if (t == null) return true;
                    _tListRelMod = typeof(System.Collections.Generic.List<>).MakeGenericType(t);
                }
                __result = Activator.CreateInstance(_tListRelMod);
                _essosGated++;
                if (_essosGated == 1 || _essosGated % 2000 == 0)
                    try { Scribe.Line("Mends: relacje notabl-lord bez tytulu osady (Essos) - odciete " + _essosGated + " razy, zero wyjatkow."); } catch { }
                return false;
            }
            catch { return true; }
        }
        private static int _relSkipped;
        private static bool _relStackDumped;

        /// <summary>
        /// Brama na UpdateRelations. Niebezpieczna galaz GetHeroesToUpdate
        /// (else-if Hero.IsNotable) odpala sie tylko dla notabla bez klanu
        /// i wtedy CurrentSettlement/OwnerClan bez null-checka klada watek.
        /// Takiego bohatera pomijamy; cala reszta liczy sie normalnie.
        /// </summary>
        /// <summary>Wizytowka bohatera do diagnozy nulli: co ma, czego mu brak.</summary>
        private static string Describe(Hero h)
        {
            try
            {
                if (h == null) return "(null)";
                return h.Name + " [" + (h.IsLord ? "lord" : h.IsNotable ? "notabl" : "inny")
                       + ", kultura=" + (h.Culture != null ? h.Culture.StringId : "NULL")
                       + ", klan=" + (h.Clan != null ? h.Clan.StringId : "NULL")
                       + ", osada=" + (h.CurrentSettlement != null ? h.CurrentSettlement.StringId : "NULL") + "]";
            }
            catch { return "(blad opisu)"; }
        }

        private static int _strayPreachersFixed;

        /// <summary>
        /// Preacher-duch: notabl z zawodem kaplana, ktorego menedzer religii BK
        /// nie zna. Dopisujemy go do religii (wlasnej, a gdy brak - idealnej dla
        /// jego kultury) TUZ PRZED warunkiem powitania - dialog wstaje w tej
        /// samej rozmowie. Wszystko refleksja, zero twardej zaleznosci od BK.
        /// </summary>
        public static void RegisterStrayPreacher()
        {
            try
            {
                var hero = Hero.OneToOneConversationHero;
                if (hero == null || !hero.IsPreacher) return;
                var cfgT = AccessTools.TypeByName("BannerKings.BannerKingsConfig");
                var cfgP = cfgT != null ? AccessTools.Property(cfgT, "Instance") : null;
                object cfg = cfgP != null ? cfgP.GetValue(null, null) : null;
                object mgr = cfg != null ? Traverse.Create(cfg).Property("ReligionsManager").GetValue() : null;
                if (mgr == null) return;
                var tr = Traverse.Create(mgr);
                if (tr.Method("IsPreacher", hero).GetValue<bool>()) return;   // zna go - nic do roboty

                object rel = tr.Method("GetHeroReligion", hero).GetValue();
                if (rel == null)
                {
                    try { rel = tr.Method("GetIdealReligion", hero.Culture).GetValue(); } catch { }
                }
                var sett = hero.CurrentSettlement;
                if (rel == null || sett == null)
                {
                    Scribe.Line("Mends: preacher " + hero.Name + " bez religii/osady - dialogu nie ozywie.");
                    return;
                }
                Traverse.Create(rel).Method("AddClergyman", sett, hero).GetValue();
                _strayPreachersFixed++;
                Scribe.Line("Mends: preacher " + hero.Name + " (" + sett.Name + ") dopisany do religii - dialog ozyl (lacznie " + _strayPreachersFixed + ").");
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "RegisterStrayPreacher", null); } catch { } }
        }

        public static bool RelationsUpdateGate(object __instance)
        {
            try
            {
                if (_hrHeroGet == null || __instance == null) return true;
                var hero = _hrHeroGet.Invoke(__instance, null) as Hero;
                if (hero == null) return false;
                if (hero.IsNotable && hero.Clan == null
                    && (hero.CurrentSettlement == null || hero.CurrentSettlement.OwnerClan == null))
                {
                    if (_relSkipped++ % 500 == 0)
                        try { Scribe.Line("Mends: relacje BK - pominieto notabla bez osady/wlasciciela (" + hero.Name + "), lacznie " + _relSkipped + "."); } catch { }
                    return false;
                }
            }
            catch { }
            return true;
        }

        public static Exception SafeRelations(Exception __exception, ref object __result, object heroRelations, Hero target)
        {
            if (__exception == null) return null;
            if (__exception is NullReferenceException || __exception is ArgumentNullException
                || __exception is System.Collections.Generic.KeyNotFoundException)
            {
                // pierwszy zlapany dostaje PELNY raport ze stosem - w sesji 26.08
                // licznik doszedl do 12 tys., a w logu nie bylo ANI JEDNEGO sladu,
                // KTORA linia CalculateModifiers naprawde pada
                if (!_relStackDumped)
                {
                    _relStackDumped = true;
                    // slad 27.08 pokazal tylko ramke Patch1 (inline) - dokladamy
                    // KONTEKST bohaterow, zeby nazwac null po imieniu
                    string ctx = null;
                    try
                    {
                        Hero hero = null;
                        try { hero = _hrHeroGet != null && heroRelations != null ? _hrHeroGet.Invoke(heroRelations, null) as Hero : null; } catch { }
                        ctx = "hero=" + Describe(hero) + " | target=" + Describe(target);
                    }
                    catch { }
                    try { Scribe.Report("CrashScribe", __exception, "BKRelationsModel.CalculateModifiers - pelny slad lawiny (jednorazowo)", ctx); } catch { }
                }
                try
                {
                    var t = Type.GetType("BannerKings.Managers.Skills.RelationsModifier, BannerKings")
                            ?? QuietType("RelationsModifier");
                    var listT = typeof(System.Collections.Generic.List<>).MakeGenericType(t);
                    __result = Activator.CreateInstance(listT);
                }
                catch { }
                if (_relSaves++ % 100 == 0)
                    try { Scribe.Line("Mends: BK relations uratowane (NullReference), lacznie " + _relSaves + " razy."); } catch { }
                return null;
            }
            return __exception;
        }

        /// <summary>
        /// Zdejmuje JEDNA konkretna lataczke BKROTPatch z metody BannerKings, nie ruszajac
        /// niczego innego w ich modzie. Zwraca true, jesli metoda BK jest juz wolna.
        /// </summary>
        private static bool Unhook(Harmony harmony, System.Reflection.MethodInfo theirPatch,
                                   string bkTypeFullName, string bkMethodName)
        {
            try
            {
                if (harmony == null || theirPatch == null) return false;
                var bkType = AccessTools.TypeByName(bkTypeFullName);
                var target = bkType != null ? AccessTools.Method(bkType, bkMethodName) : null;
                if (target == null) return false;
                harmony.Unpatch(target, theirPatch);
                return true;
            }
            catch { return false; }
        }

        private static int _clergyFreed;
        private static int _localSwaps;

        /// <summary>
        /// JEDNORAZOWE SPRZATANIE PO MNIE. Zanim ochotnicy odswieza sie sami
        /// (jeden slot na dobe), w zapisie siedza juz obce oddzialy wpisane
        /// notablom - miedzy innymi wolny lud u polnocnych kaplanow, ktorych
        /// dopiero moja latka na BKROTPatch pozwolila stworzyc. Zerujemy tylko
        /// te sloty, ktorych kultura nie zgadza sie z osada; BK dobierze
        /// miejscowych przy najblizszym odswiezeniu.
        /// </summary>
        internal static void LocalLevies() { LocalLevies(null); }

        /// <summary>
        /// KAPLAN JEST STAD. Banner Kings tworzy duchownego z PRESETU WIARY:
        ///     HeroCreator.CreateSpecialHero(preset, ...)
        /// czyli jego kultura jest kultura presetu, nie osady - a wiare Starych
        /// Bogow ROT spina z wolnym ludem. Skutek widac czarno na bialym w moim
        /// wlasnym spisie notabli:
        ///     White Ranch (battania) -> [Greenseer Jarl / freefolk / Preacher]
        ///     Last River  (battania) -> [Greenseer Hali / freefolk / Preacher]
        /// a poniewaz OCHOTNIKOW dobiera sie wedle kultury NOTABLA, polnocne wsie
        /// wystawialy synow Thenna. Prostujemy u zrodla: kaplan dostaje kulture
        /// SWOJEJ osady, a obce oddzialy juz mu wpisane leca do kosza, zeby BK
        /// dobralo miejscowych. Wiara i tytul zostaja - zmienia sie tylko to,
        /// kogo ten czlowiek potrafi wystawic pod bron.
        /// </summary>
        internal static void LocalLevies(Settlement only)
        {
            try
            {
                if (!Config.LocalRecruits) return;
                int slots = 0, people = 0, cultures = 0;
                var all = only != null
                    ? (System.Collections.Generic.IEnumerable<Settlement>)new Settlement[] { only }
                    : Settlement.All;
                foreach (var st in all)
                {
                    if (st == null || st.Culture == null || st.IsHideout) continue;
                    var list = st.Notables;
                    if (list == null) continue;
                    foreach (var h in list)
                    {
                        if (h == null) continue;

                        // 1. obcy kaplan (albo inny notabl) dostaje kulture osady
                        if (h.Culture != st.Culture)
                        {
                            var was = h.Culture != null ? h.Culture.StringId : "?";
                            h.Culture = st.Culture;
                            cultures++;
                            try
                            {
                                Scribe.Line("Mends: " + h.Name + " w " + st.Name + " byl "
                                            + was + ", jest " + st.Culture.StringId
                                            + " (" + (h.CharacterObject != null ? h.CharacterObject.Occupation.ToString() : "?") + ").");
                            }
                            catch { }
                        }

                        // 2. obce oddzialy juz mu wpisane - do kosza, BK dobierze miejscowych
                        var vt = h.VolunteerTypes;
                        if (vt == null) continue;
                        bool touched = false;
                        for (int i = 0; i < vt.Length; i++)
                        {
                            var ch = vt[i];
                            if (ch == null || ch.Culture == null || ch.Culture == st.Culture) continue;
                            vt[i] = null;
                            slots++; touched = true;
                        }
                        if (touched) people++;
                    }
                }
                if (only == null || slots > 0 || cultures > 0)
                    Scribe.Line("Mends: werbunek miejscowy - poprawionych notabli " + cultures
                                + ", wyrzuconych obcych ochotnikow " + slots + " u " + people + " ludzi.");
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "Mends.LocalLevies", null); } catch { } }
        }

        /// <summary>
        /// Kultura osady bije kulture notabla przy doborze ochotnikow.
        /// </summary>
        public static void LocalRecruitsPrefix(ref CultureObject __0, Settlement __1)
        {
            try
            {
                if (__1 == null || __1.Culture == null) return;
                if (__0 == __1.Culture) return;
                __0 = __1.Culture;
                if (_localSwaps == 0)
                    try { Scribe.Line("Mends: pierwszy podmieniony werbunek - ochotnicy sa juz miejscowi."); } catch { }
                _localSwaps++;
            }
            catch { }
        }



        /// <summary>
        /// Objazd, gdyby zdjecie lataczki sie nie udalo: wchodzimy PRZED ich prefix,
        /// zostawiamy ich sensowne zabezpieczenia na null, a poza tym kazemy im
        /// zwrocic true - czyli "niech duchownego zrobi oryginal BannerKings".
        /// </summary>
        public static bool LetClergyBe(TaleWorlds.CampaignSystem.Settlements.Settlement __0, ref bool __result)
        {
            try
            {
                if (__0 == null || __0.Culture == null) return true;
                __result = true;
                if (_clergyFreed == 0)
                    try { Scribe.Line("Mends: duchowni BK odblokowani objazdem - dane osad znowu licza sie do konca."); } catch { }
                _clergyFreed++;
                return false;
            }
            catch { return true; }
        }

        /// <summary>Typ RelationsModifier bywa w roznych przestrzeniach BK - szukamy po nazwie.</summary>
        private static Type QuietType(string shortName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (asm.GetName().Name != "BannerKings") continue;
                    foreach (var t in asm.GetTypes())
                        if (t != null && t.Name == shortName) return t;
                }
                catch { }
            }
            return typeof(object);
        }

        /// <summary>Pory roku RB: zawsze "wylaczone" - w ROT zima trwa pokolenie, nie 21 dni.</summary>
        public static void SeasonsOff(ref bool __result) { __result = false; }

        // ---- kuznia: ratunek projektu z obcym (podwojnie zaladowanym) elementem ----
        private static bool _smithyFixing;
        private static int _smithySaves;

        public static void SafeSmithy(TaleWorlds.Core.Crafting __instance)
        {
            try
            {
                if (_smithyFixing || __instance == null) return;
                var fItem = AccessTools.Field(typeof(TaleWorlds.Core.Crafting), "_craftedItemObject");
                if (fItem == null || fItem.GetValue(__instance) != null) return;   // item jest - nic do ratowania

                var design = __instance.CurrentWeaponDesign;
                if (design == null || design.Template == null || design.UsedPieces == null) return;

                var used = design.UsedPieces;
                var els = new TaleWorlds.Core.WeaponDesignElement[used.Length];
                bool changed = false;
                for (int i = 0; i < used.Length; i++)
                {
                    els[i] = used[i];
                    var el = used[i];
                    if (el == null || !el.IsValid || el.CraftingPiece == null) continue;

                    bool inTemplate = false;
                    TaleWorlds.Core.CraftingPiece twin = null;
                    foreach (var p in design.Template.Pieces)
                    {
                        if (ReferenceEquals(p, el.CraftingPiece)) { inTemplate = true; break; }
                        if (twin == null && p != null && p.PieceType == el.CraftingPiece.PieceType
                            && p.StringId == el.CraftingPiece.StringId) twin = p;
                    }
                    if (inTemplate) continue;
                    if (twin == null)
                    {
                        // brak blizniaka po StringId - bierzemy pierwszy element tego samego typu
                        foreach (var p in design.Template.Pieces)
                            if (p != null && p.PieceType == el.CraftingPiece.PieceType) { twin = p; break; }
                    }
                    if (twin == null) continue;
                    els[i] = TaleWorlds.Core.WeaponDesignElement.CreateUsablePiece(twin, el.ScalePercentage);
                    changed = true;
                }
                if (!changed) return;

                var fresh = new TaleWorlds.Core.WeaponDesign(design.Template, design.WeaponName, els, null);
                var pDesign = AccessTools.Property(typeof(TaleWorlds.Core.Crafting), "CurrentWeaponDesign");
                var setDesign = pDesign != null ? pDesign.GetSetMethod(true) : null;
                if (setDesign != null) setDesign.Invoke(__instance, new object[] { fresh });
                else
                {
                    var bf = AccessTools.Field(typeof(TaleWorlds.Core.Crafting), "<CurrentWeaponDesign>k__BackingField");
                    if (bf == null) return;
                    bf.SetValue(__instance, fresh);
                }

                _smithyFixing = true;
                try
                {
                    var mSet = AccessTools.Method(typeof(TaleWorlds.Core.Crafting), "SetItemObject");
                    if (mSet != null) mSet.Invoke(__instance, new object[] { null, null });
                }
                finally { _smithyFixing = false; }

                _smithySaves++;
                if (_smithySaves == 1 || _smithySaves % 50 == 0)
                    Scribe.Line("Mends: kuznia uratowana (x" + _smithySaves + ") - obcy element podmieniony na blizniaka z szablonu.");
            }
            catch (Exception e) { _smithyFixing = false; try { Scribe.Report("CrashScribe", e, "SafeSmithy", null); } catch { } }
        }

        /// <summary>
        /// Odswiezenia UI projektanta broni nie maja czego liczyc, gdy zlozony
        /// item wyszedl null - pomijamy je, zamiast pozwolic silnikowi rzucic
        /// NullReference (crash). Zwrot false = nie wykonuj oryginalu.
        /// </summary>
        private static System.Reflection.FieldInfo _fVmCrafting;

        public static bool VmNeedsItem(object __instance)
        {
            try
            {
                if (__instance == null) return true;
                if (_fVmCrafting == null) _fVmCrafting = AccessTools.Field(__instance.GetType(), "_crafting");
                var cr = _fVmCrafting != null ? _fVmCrafting.GetValue(__instance) as TaleWorlds.Core.Crafting : null;
                if (cr == null) return true;
                return cr.GetCurrentCraftedItemObject() != null;
            }
            catch { return true; }
        }

        public static Exception SafeContribution(Exception __exception, ref float __result)
        {
            if (__exception == null) return null;
            if (__exception is IndexOutOfRangeException || __exception is NullReferenceException
                || __exception is ArgumentOutOfRangeException)
            {
                __result = 0f;
                try { Scribe.Line("Mends: contribution-rate uratowane (PlayerSide bez strony) -> wklad 0."); } catch { }
                return null;
            }
            return __exception;
        }

        /// <summary>
        /// Szansa, ze lord za 250 000 zostanie szpiegiem. Zamiast plaskich 20%:
        /// baza 0. Honorowy lord (Honor >= 1, np. Jon Snow) NIE sprzedaje sie nigdy.
        /// Dodaja: niski honor (+12/poziom), wyrachowanie (+5/poziom), przyjazn
        /// (+0.3/pkt relacji), Twoje Roguery (+0.1/pkt). Odejmuja: wrogosc
        /// (-0.5/pkt ujemnej relacji), poteha klanu (-4/tier). Sufit 35%.
        /// </summary>
        public static bool RealSpyChance(ref bool __result)
        {
            try
            {
                __result = false;
                if (Hero.MainHero.Gold < 250000) return false;
                var lord = Hero.OneToOneConversationHero;
                if (lord == null) return false;

                int honor = lord.GetTraitLevel(TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultTraits.Honor);
                if (honor >= 1) return false;   // czlowiek honoru - nie ma ceny

                float chance = 0f;
                chance += Hero.MainHero.GetSkillValue(TaleWorlds.Core.DefaultSkills.Roguery) * 0.1f;
                float rel = lord.GetRelationWithPlayer();
                chance += rel > 0f ? rel * 0.3f : rel * 0.5f;
                if (honor < 0) chance += -honor * 12f;
                int calc = lord.GetTraitLevel(TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultTraits.Calculating);
                if (calc > 0) chance += calc * 5f;
                if (lord.Clan != null) chance -= lord.Clan.Tier * 4f;
                if (chance < 0f) chance = 0f;
                if (chance > 35f) chance = 35f;

                __result = MBRandom.RandomFloat * 100f <= chance;
                return false;   // oryginalny rzut 20% pomijamy
            }
            catch (Exception e)
            {
                try { Scribe.Report("CrashScribe", e, "Mends.RealSpyChance", null); } catch { }
                __result = false;
                return false;
            }
        }

        public static bool SafeSpyFail(ConversationSentence __instance)
        {
            try
            {
                if (__instance == null || !"lord_spy_reaction_fail".Equals(__instance.Id)) return true;
                // odgrywamy tresc oryginalu (relacja, wojna, aresztowanie), bez feralnego wywolania
                var lord = Hero.OneToOneConversationHero;
                if (lord == null) return false;
                ChangeRelationAction.ApplyPlayerRelation(lord, -100);
                var playerFaction = Hero.MainHero.MapFaction;
                var lordFaction = lord.MapFaction;
                if (playerFaction != null && lordFaction != null && playerFaction != lordFaction &&
                    !playerFaction.IsAtWarWith(lordFaction))
                    DeclareWarAction.ApplyByDefault(playerFaction, lordFaction);
                InformationManager.DisplayMessage(new InformationMessage(
                    lord.Name + " was deeply offended! War has been declared.", Colors.Red));
                if (lord.PartyBelongedTo != null && MobileParty.MainParty != null)
                {
                    PlayerEncounter.RestartPlayerEncounter(MobileParty.MainParty.Party,
                        lord.PartyBelongedTo.Party, true, false);
                    PlayerEncounter.StartBattle();
                }
                return false;
            }
            catch (Exception e)
            {
                try { Scribe.Report("CrashScribe", e, "Mends.SafeSpyFail", null); } catch { }
                return false;   // lepiej pominac skutek niz polozyc gre
            }
        }
    }

    /// <summary>Mendy na DANYCH gry (nie na kodzie) musza czekac, az swiat
    /// wstanie - obiekty jednostek istnieja dopiero po starcie sesji.</summary>
    internal sealed class MendsBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this,
                delegate (CampaignGameStarter s)
                { Mends.WeightLaw(); Mends.SkillSinew(); Mends.UniqueWares(); Mends.LoreForgeGate(); Mends.DressTheNamesakes(); });
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this,
                delegate (TaleWorlds.CampaignSystem.MapEvents.MapEvent m) { Mends.MeltDeadLoot(m); });
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this,
                delegate (CampaignGameStarter s) { Mends.DragonPurge(true); });
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this,
                delegate { Mends.DragonPurge(false); if (!Mends.SinewApplied) Mends.SkillSinew(); });
        }

        public override void SyncData(IDataStore dataStore)
        {
            try { dataStore.SyncData("cs_uniq_homes", ref Mends.UniqueHomesDone); } catch { }
        }
    }
}
