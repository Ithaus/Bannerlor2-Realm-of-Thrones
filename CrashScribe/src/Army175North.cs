using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace CrashScribe
{
    /// <summary>
    /// PACZKA 175.4a - POLNOC TWARDSZA (decyzja 1C) i JEZDZCY DOTHRAKOW (audyt 4.1, decyzja 3).
    /// Wspolna maszyna: zbior liczony REGULA (nie lista), po SkillSinew (ten tylko podnosi, wiec bonus przed nim
    /// zostalby wchloniety), wartosc docelowa = max(obecna, wymog wlasnego sprzetu) + bonus, znacznik "raz" na
    /// OBIEKCIE UMIEJETNOSCI (skill_template i FillFrom dziela MBCharacterSkills - klon jednostki nie dostanie
    /// bonusu drugi raz). Obiekty sa nowe w kazdej grze - bez kumulacji miedzy wczytaniami.
    /// Dziala tylko w bitwach z graczem (autobitwa liczy moc z tieru); +10% w autobitwie to Armoury NorthHomeEdge.
    /// </summary>
    internal static partial class Army175
    {
        internal static bool NorthDone, DothrakiDone;
        private static readonly ConditionalWeakTable<MBCharacterSkills, object> _northMark = new ConditionalWeakTable<MBCharacterSkills, object>();
        private static readonly ConditionalWeakTable<MBCharacterSkills, object> _dothMark = new ConditionalWeakTable<MBCharacterSkills, object>();
        private static readonly object Mark = new object();

        // 9 szablonow Polnocy z 2.3 (krolestwo + 8 rodow)
        private static readonly string[] NorthTemplates =
        {
            "kingdom_hero_party_battania_template", "clan_stark_party_template", "clan_bolton_party_template", "clan_karstark_party_template",
            "clan_glover_party_template", "clan_manderly_party_template", "clan_umber_party_template", "clan_mormont_party_template", "clan_cerwyn_party_template"
        };

        private static void Tree(CharacterObject root, HashSet<CharacterObject> into)
        {
            if (root == null) return;
            var st = new Stack<CharacterObject>();
            st.Push(root);
            while (st.Count > 0)
            {
                var c = st.Pop();
                if (c == null || !into.Add(c)) continue;
                var ups = c.UpgradeTargets;
                if (ups != null) foreach (var u in ups) if (u != null) st.Push(u);
            }
        }

        private static void TemplateTrees(PartyTemplateObject tpl, HashSet<CharacterObject> into)
        {
            if (tpl == null || tpl.Stacks == null) return;
            foreach (var s in tpl.Stacks) Tree(s.Character, into);
        }

        private static HashSet<CharacterObject> MilitiaTrees(CultureObject cu)
        {
            var m = new HashSet<CharacterObject>();
            if (cu == null) return m;
            Tree(cu.MeleeMilitiaTroop, m); Tree(cu.RangedMilitiaTroop, m); Tree(cu.MeleeEliteMilitiaTroop, m); Tree(cu.RangedEliteMilitiaTroop, m);
            return m;
        }

        private static bool IsInfantryGroup(CharacterObject co)
        {
            return co.DefaultFormationClass == FormationClass.Infantry;
        }

        /// <summary>Zbior 3.2: kultura battania, default_group Infantry, tier 3-6, w drzewach wsi
        /// (BasicTroop), szlachty (EliteBasicTroop) i 9 szablonow Polnocy; bez milicji i bohaterow, tylko Occupation Soldier
        /// (jak WorldInfantryWithoutNorth). Ma byc 43. Armoury NorthHomeEdge.BuildSet liczy ten zbior osobno (korzenie na
        /// sztywno, bez wykluczenia milicji) - przy scaleniu ujednolicic albo czytac NorthSet() refleksja.</summary>
        internal static List<CharacterObject> NorthSet()
        {
            var om = MBObjectManager.Instance;
            var cu = om.GetObject<CultureObject>("battania");
            var all = new HashSet<CharacterObject>();
            if (cu != null) { Tree(cu.BasicTroop, all); Tree(cu.EliteBasicTroop, all); }
            foreach (var id in NorthTemplates) TemplateTrees(om.GetObject<PartyTemplateObject>(id), all);
            var mil = MilitiaTrees(cu);
            var res = new List<CharacterObject>();
            foreach (var co in all)
            {
                if (co == null || co.IsHero || mil.Contains(co)) continue;
                if (co.Occupation != Occupation.Soldier) continue;   // bez najemnikow i strazy karawan (jak WorldInfantryWithoutNorth)
                if (co.Culture == null || co.Culture.StringId != "battania") continue;
                if (!IsInfantryGroup(co)) continue;
                int t = co.Tier;
                if (t < 3 || t > 6) continue;
                res.Add(co);
            }
            res.Sort((a, b) => a.Tier != b.Tier ? a.Tier.CompareTo(b.Tier) : string.CompareOrdinal(a.StringId, b.StringId));
            return res;
        }

        /// <summary>Linia konna wsi Dothrakow t2-t5 (2.1, +30 Jazdy): kultura khuzait, drzewo BasicTroop
        /// i szablonu kultury (bez milicji), default_group Cavalry/HorseArcher, zolnierz. Ma byc 7.</summary>
        internal static List<CharacterObject> DothrakiRiderSet()
        {
            var om = MBObjectManager.Instance;
            var cu = om.GetObject<CultureObject>("khuzait");
            var all = new HashSet<CharacterObject>();
            if (cu != null) { Tree(cu.BasicTroop, all); TemplateTrees(cu.DefaultPartyTemplate, all); }
            var mil = MilitiaTrees(cu);
            var res = new List<CharacterObject>();
            foreach (var co in all)
            {
                if (co == null || co.IsHero || mil.Contains(co) || co.Occupation != Occupation.Soldier) continue;
                if (co.Culture == null || co.Culture.StringId != "khuzait") continue;
                if (!co.IsMounted) continue;
                int t = co.Tier;
                if (t < 2 || t > 5) continue;
                res.Add(co);
            }
            res.Sort((a, b) => a.Tier != b.Tier ? a.Tier.CompareTo(b.Tier) : string.CompareOrdinal(a.StringId, b.StringId));
            return res;
        }

        /// <summary>Wymog wlasnego sprzetu dla umiejetnosci (jak SkillSinew: sloty 0-11 bez sztandaru).</summary>
        private static int OwnNeed(CharacterObject co, SkillObject sk)
        {
            int need = 0;
            try
            {
                foreach (var eq in co.BattleEquipments)
                {
                    if (eq == null) continue;
                    for (int s = 0; s <= 11; s++)
                    {
                        if (s == 4) continue;
                        var it = eq[s].Item;
                        if (it == null || it.Difficulty <= 0) continue;
                        if (Mends.ReqSkill(it) == sk && it.Difficulty > need) need = it.Difficulty;
                    }
                }
            }
            catch { }
            return need;
        }

        /// <summary>Bron glowna jak Armoury SkillsDecide: najwyzsza z 1H/2H/Drzewce; remis - klasa noszona
        /// najczesciej, potem 1H > Drzewce > 2H.</summary>
        private static SkillObject MainMelee(CharacterObject co)
        {
            var order = new[] { DefaultSkills.OneHanded, DefaultSkills.Polearm, DefaultSkills.TwoHanded };
            var carried = new int[3];
            try
            {
                foreach (var eq in co.BattleEquipments)
                {
                    if (eq == null) continue;
                    for (int s = 0; s <= 3; s++)
                    {
                        var it = eq[s].Item;
                        if (it == null || it.PrimaryWeapon == null || it.PrimaryWeapon.IsShield) continue;
                        var rs = it.RelevantSkill;
                        for (int k = 0; k < 3; k++) if (rs == order[k]) carried[k]++;
                    }
                }
            }
            catch { }
            int best = 0;
            for (int k = 1; k < 3; k++)
            {
                int vk = co.GetSkillValue(order[k]), vb = co.GetSkillValue(order[best]);
                if (vk > vb || (vk == vb && carried[k] > carried[best])) best = k;
            }
            return order[best];
        }

        private static int MaxMelee(CharacterObject co)
        {
            return Math.Max(co.GetSkillValue(DefaultSkills.OneHanded), Math.Max(co.GetSkillValue(DefaultSkills.TwoHanded), co.GetSkillValue(DefaultSkills.Polearm)));
        }

        private static bool SetSkill(CharacterObject co, SkillObject sk, int value)
        {
            var skills = co.GetDefaultCharacterSkills();
            var owner = skills != null ? skills.Skills : null;
            var mSet = owner != null ? AccessTools.Method(owner.GetType(), "SetPropertyValue") : null;
            if (mSet == null) return false;
            mSet.Invoke(owner, new object[] { sk, value });
            return true;
        }

        /// <summary>Piechota t3+ swiata BEZ Polnocy (do kontroli poprawnosci): kultury nie-bandyckie poza battania,
        /// zolnierz, default_group Infantry, tier &gt;= 3, zrodlo glowne wies/szlachta/rod albo szablon kultury
        /// (bez milicji) - ta sama definicja co skrypt kryt175b/sila_odp.py (ok. 275 jednostek).</summary>
        private static List<CharacterObject> WorldInfantryWithoutNorth()
        {
            var om = MBObjectManager.Instance;
            var ais = new HashSet<CharacterObject>();
            var tplSet = new HashSet<CharacterObject>();
            var mil = new HashSet<CharacterObject>();
            foreach (var cu in om.GetObjectTypeList<CultureObject>())
            {
                if (cu == null || cu.IsBandit) continue;
                Tree(cu.BasicTroop, ais); Tree(cu.EliteBasicTroop, ais);
                TemplateTrees(cu.DefaultPartyTemplate, tplSet);
                foreach (var m in MilitiaTrees(cu)) mil.Add(m);
            }
            try
            {
                foreach (var cl in Clan.All)
                {
                    if (cl == null || cl.IsMinorFaction || cl.IsBanditFaction) continue;
                    TemplateTrees(cl.DefaultPartyTemplate, ais);
                }
            }
            catch { }
            foreach (var c in tplSet) if (!mil.Contains(c)) ais.Add(c);
            var res = new List<CharacterObject>();
            foreach (var co in ais)
            {
                if (co == null || co.IsHero || co.Occupation != Occupation.Soldier) continue;
                if (co.Culture == null || co.Culture.IsBandit || co.Culture.StringId == "battania") continue;
                if (!IsInfantryGroup(co) || co.Tier < 3) continue;
                res.Add(co);
            }
            return res;
        }

        /// <summary>Sufit "bez przeskoku tieru": wymog tieru T+1 (prawo tieru, T x krok) minus 1, nigdy ponizej podstawy.
        /// K1b (MenUpgradeOneTierUp, Jeff 09.10) pozwala kupic o stopien wyzej, gdy zolnierz udzwignie - po 175 udzwignelaby
        /// tylko Polnoc t3 (80 + 25 = 105 = wymog t4), wiec "twardsi" stalby sie przywilejem tieru. Krok 0 (prawo wyl.) albo t6 - bez sufitu.</summary>
        private static int TierCap(int value, int floor, int tier, float step)
        {
            if (step <= 0f || tier >= 6) return value;
            int lim = (int)Math.Round(tier * step) - 1;
            return Math.Max(floor, Math.Min(value, lim));
        }

        /// <summary>3.2: +bonus (suwak NorthHardySkillBonus, dom. 25, 0 = wyl.) do broni glownej i Atletyki
        /// piechoty Polnocy t3-t6. "+25 ponad dzisiejsze" = ponad wartosci PO zamianie sprzetu (rozdz. 1) -
        /// interpretacja do potwierdzenia przez Jeffa przy "wgraj" (projekt rozdz. 0 pkt 4). Tylko gdy 175.2 zadzialalo
        /// (inaczej +25 szloby na umiejetnosci napompowane sprzetem ponad tier). Cel z sufitem TierCap (bez przeskoku tieru).</summary>
        internal static void NorthHardy()
        {
            if (NorthDone || !Mends.SinewApplied) return;
            try
            {
                int bonus = (int)Math.Round(Mends.ArmouryFloat("NorthHardySkillBonus", 25f));
                if (bonus < 0) bonus = 0;
                if (bonus > 50) bonus = 50;
                NorthDone = true;
                if (bonus == 0) { Scribe.Line("Mends: NorthHardy (175) - wylaczone (NorthHardySkillBonus 0)."); return; }
                if (!TierGearApplied)
                {
                    string why = !TierGearWanted() ? _tgOffWhy : (_gaveUp ? "zamiana nie mogla zadzialac" : "zamiana nie zadzialala");
                    Scribe.Line("Mends: NorthHardy (175) - pominiete: sprzet wedlug tieru (175.2) nie dziala w tej sesji (" + why + ") - +" + bonus
                                + " liczyloby sie od umiejetnosci napompowanych sprzetem ponad tier (przewaga ok. +46%/+52% zamiast +28%, projekt 3.2).");
                    return;
                }
                float wStep = Mends.ArmouryFloat("WeaponSkillPerTier", 35f), aStep = Mends.ArmouryFloat("ArmorAthleticsPerTier", 35f);
                wStep = wStep < 0.5f ? 0f : (wStep > 100f ? 100f : wStep);
                aStep = aStep < 0.5f ? 0f : (aStep > 100f ? 100f : aStep);
                var set = NorthSet();
                if (set.Count == 0) { Scribe.Line("Mends: NorthHardy (175) - OSTRZEZENIE: pusty zbior piechoty Polnocy (brak kultury battania albo drzew)."); return; }
                double preM = 0, preA = 0;
                foreach (var co in set) { preM += MaxMelee(co); preA += co.GetSkillValue(DefaultSkills.Athletics); }
                preM /= set.Count; preA /= set.Count;
                int done = 0, shared = 0, stumbles = 0, capped = 0;
                double planM = 0, planA = 0;
                var planBySkills = new Dictionary<MBCharacterSkills, int[]>();
                var ex = new List<string>();
                foreach (var co in set)
                {
                    try
                    {
                        var skills = co.GetDefaultCharacterSkills();
                        if (skills == null) { stumbles++; continue; }
                        object o;
                        int[] pl;
                        if (_northMark.TryGetValue(skills, out o))
                        {
                            shared++;
                            if (planBySkills.TryGetValue(skills, out pl)) { planM += pl[0]; planA += pl[1]; }
                            continue;
                        }
                        var main = MainMelee(co);
                        int T = UnitTier(co);
                        int curM = co.GetSkillValue(main), curA = co.GetSkillValue(DefaultSkills.Athletics);
                        int baseM = Math.Max(curM, OwnNeed(co, main)), baseA = Math.Max(curA, OwnNeed(co, DefaultSkills.Athletics));
                        int tgtM = TierCap(baseM + bonus, baseM, T, wStep);
                        int tgtA = TierCap(baseA + bonus, baseA, T, aStep);
                        if (tgtM < baseM + bonus || tgtA < baseA + bonus) capped++;
                        if (!SetSkill(co, main, tgtM) || !SetSkill(co, DefaultSkills.Athletics, tgtA)) { stumbles++; continue; }
                        _northMark.Add(skills, Mark);
                        planBySkills[skills] = new[] { tgtM - curM, tgtA - curA };
                        planM += tgtM - curM; planA += tgtA - curA;
                        done++;
                        if (ex.Count < 6) ex.Add(co.StringId + " t" + T + " " + main.StringId + " " + curM + "->" + tgtM + " Atl " + curA + "->" + tgtA);
                    }
                    catch { stumbles++; }
                }
                planM /= set.Count; planA /= set.Count;
                double postM = 0, postA = 0;
                foreach (var co in set) { postM += MaxMelee(co); postA += co.GetSkillValue(DefaultSkills.Athletics); }
                postM /= set.Count; postA /= set.Count;
                var w = WorldInfantryWithoutNorth();
                double wM = 0, wA = 0;
                foreach (var co in w) { wM += MaxMelee(co); wA += co.GetSkillValue(DefaultSkills.Athletics); }
                if (w.Count > 0) { wM /= w.Count; wA /= w.Count; }
                double advM = wM > 0 ? 100.0 * (postM / wM - 1.0) : 0, advA = wA > 0 ? 100.0 * (postA / wA - 1.0) : 0;
                Scribe.Line("Mends: NorthHardy (175) - " + done + " jednostkom +" + bonus + " (bron glowna, Atletyka) z " + set.Count
                            + ", z tego " + capped + " przycietych do sufitu bez przeskoku tieru (wymog t(T+1) minus 1; przy 35/tier: t3 104, t4 139, t5 174)"
                            + "; piechota t3+ Polnocy bron/Atl " + preM.ToString("0") + "/" + preA.ToString("0") + " -> " + postM.ToString("0") + "/" + postA.ToString("0")
                            + " wobec swiata bez Polnocy " + wM.ToString("0") + "/" + wA.ToString("0") + " (" + w.Count + " jednostek) - przewaga "
                            + advM.ToString("+0.0;-0.0") + "%/" + advA.ToString("+0.0;-0.0") + "%; wspolne umiejetnosci pominiete " + shared + ", potkniecia " + stumbles
                            + "; np. " + string.Join(", ", ex.ToArray()) + ".");
                // KONTROLA POPRAWNOSCI (nie hamulec): liczebnosc zbioru, przyrost sredniej == zaplanowany (bonus po sufitach),
                // przewaga w oknie oczekiwanym dla suwaka (rachunek projektu: 0 -> +7.5%, 15 -> +20%, 25 -> +28%, 50 -> +48%; +-3 pkt;
                // sufit tieru przy 25 (rachunek na sprzet.json): 11 z 43 jednostek przycietych - highborn_warrior 90 -> 104 zamiast 115,
                // 8 footmanow t3 i 2 pikinierow t5 o 1; srednia broni -0.5, Atletyki -0.2 pkt, przewaga ok. -0.4 pkt - w oknie)
                double expect = 7.5 + 0.82 * bonus;
                var why2 = new List<string>();
                if (set.Count != 43) why2.Add("zbior " + set.Count + " zamiast 43");
                if (Math.Abs((postM - preM) - planM) > 0.5 || Math.Abs((postA - preA) - planA) > 0.5)
                    why2.Add("przyrost sredniej " + (postM - preM).ToString("0.0") + "/" + (postA - preA).ToString("0.0") + " zamiast zaplanowanego "
                             + planM.ToString("0.0") + "/" + planA.ToString("0.0"));
                if (Math.Abs(advM - expect) > 3 || Math.Abs(advA - expect) > 3)
                    why2.Add("przewaga poza oknem " + (expect - 3).ToString("0") + "-" + (expect + 3).ToString("0") + "%");
                if (why2.Count > 0)
                    Scribe.Line("Mends: NorthHardy (175) - OSTRZEZENIE (kontrola poprawnosci): " + string.Join("; ", why2.ToArray())
                                + " - sprawdz podwojny bonus, zbior albo klase broni.");
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "Army175.NorthHardy", null); } catch { } }
        }

        /// <summary>2.1 (audyt 4.1): +bonus Jazdy (suwak DothrakiRidingBonus, dom. 30, 0 = wyl.) linii konnej
        /// wsi Dothrakow t2-t5. 175 nie zmienia koni, wiec "ponad dzisiejsze" = ponad wartosc po SkillSinew.</summary>
        internal static void DothrakiRiders()
        {
            if (DothrakiDone || !Mends.SinewApplied) return;
            try
            {
                int bonus = (int)Math.Round(Mends.ArmouryFloat("DothrakiRidingBonus", 30f));
                if (bonus < 0) bonus = 0;
                if (bonus > 50) bonus = 50;
                DothrakiDone = true;
                if (bonus == 0) { Scribe.Line("Mends: DothrakiRiders (175) - wylaczone (DothrakiRidingBonus 0)."); return; }
                var set = DothrakiRiderSet();
                int done = 0, shared = 0, stumbles = 0;
                var ex = new List<string>();
                foreach (var co in set)
                {
                    try
                    {
                        var skills = co.GetDefaultCharacterSkills();
                        if (skills == null) { stumbles++; continue; }
                        object o;
                        if (_dothMark.TryGetValue(skills, out o)) { shared++; continue; }
                        int cur = co.GetSkillValue(DefaultSkills.Riding);
                        int tgt = Math.Max(cur, OwnNeed(co, DefaultSkills.Riding)) + bonus;
                        if (!SetSkill(co, DefaultSkills.Riding, tgt)) { stumbles++; continue; }
                        _dothMark.Add(skills, Mark);
                        done++;
                        ex.Add(co.StringId + " t" + co.Tier + " " + cur + "->" + tgt);
                    }
                    catch { stumbles++; }
                }
                Scribe.Line("Mends: DothrakiRiders (175) - " + done + " jednostkom z " + set.Count + " +" + bonus + " Jazdy (linia konna wsi t2-t5): "
                            + string.Join(", ", ex.ToArray()) + "; wspolne umiejetnosci pominiete " + shared + ", potkniecia " + stumbles
                            + (set.Count != 7 ? " - OSTRZEZENIE: zbior " + set.Count + " zamiast 7" : "") + ".");
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "Army175.DothrakiRiders", null); } catch { } }
        }
    }
}
