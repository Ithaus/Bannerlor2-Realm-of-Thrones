using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace CrashScribe
{
    /// <summary>
    /// PACZKA 175.3 - SKLAD WOJSK (szablony partii i drzewa awansow), projekt rozdz. 2.
    /// Wartosci bezwzgledne (stos = N, min = max = N; 0 = usun; cele awansu = cala tablica) - idempotentne.
    /// Dziala przy wczytaniu (AfterRegisterSubModuleObjects), PRZED kazdym czytelnikiem stosow: partie lordow
    /// przy (re)spawnie, partie i zalogi startowe nowej gry, sredni zold gry, ROT pula/cel (budowana leniwie),
    /// szansa awansu ROT, Armoury HouseLevies/Stables, 171 RecruitKit. Wylaczniki dzialaja od nastepnego
    /// wczytania (ROT trzyma pule do konca sesji). Brak id -> linia logu i pominiecie tej jednej zmiany.
    /// </summary>
    internal static partial class Army175
    {
        internal static bool CompositionApplied;
        internal static bool DothrakiPoolActive;     // 2.1 wlaczone w tej sesji - postfiks puli ROT dopisuje pieszych
        private static bool _poolFirstLogged;

        // (szablon, wylacznik, domyslnie, zmiany "jednostka=N"; N = 0 usuwa stos, brak stosu = dodaj)
        private static readonly string[][] StackPlan =
        {
            // 2.1 Dothrakowie konni (decyzja 3)
            new[] { "kingdom_hero_party_khuzait_template", "Army175DothrakiRide", "1",
                "khuzait_footman=0", "khuzait_spearman=0", "khuzait_spear_infantry=0", "khuzait_darkhan=0", "khuzait_hunter=0", "khuzait_archer=0", "khuzait_marksman=0",
                "khuzait_tribal_warrior=8", "khuzait_raider=6", "khuzait_horseman=3", "khuzait_horse_archer=4",
                "khuzait_lancer=2", "khuzait_heavy_horse_archer=2", "khuzait_heavy_lancer=1" },
            // 2.2 Zelazne Wyspy - jazda tylko Harlaw (decyzja 6a); clan_harlaw_party_template BEZ zmian
            new[] { "kingdom_hero_party_sturgia_template", "Army175IronbornFoot", "1",
                "sturgian_hardened_brigand=0", "sturgian_horse_raider=0", "sturgian_berzerker=3", "sturgian_shock_troop=2", "sturgian_ulfhednar=1", "sturgian_hunter=6" },
            new[] { "clan_greyjoy_party_template", "Army175IronbornFoot", "1",
                "greyjoy_rider=0", "greyjoy_horseman=0", "greyjoy_houseguard=7", "greyjoy_fingerdancer=4" },
            // 2.3 Polnoc - wiecej piechoty, mniej lucznikow, troche wiecej jazdy (decyzja 1C)
            new[] { "kingdom_hero_party_battania_template", "Army175NorthFoot", "1",
                "battanian_trained_warrior=5", "battanian_picked_warrior=3", "battanian_skirmisher=6", "battanian_veteran_skirmisher=3", "battanian_horseman=3" },
            new[] { "clan_stark_party_template", "Army175NorthFoot", "1",
                "stark_bowman=5", "stark_archer=4", "stark_footman=5", "stark_soldier=5", "stark_cavalry=3" },
            new[] { "clan_bolton_party_template", "Army175NorthFoot", "1",
                "bolton_archer=5", "bolton_elite_archer=4", "bolton_scout=5", "bolton_veteran=5", "bolton_knight=3" },
            new[] { "clan_karstark_party_template", "Army175NorthFoot", "1",
                "karstark_archer=6", "karstark_elite_archer=4", "karstark_soldier=5", "karstark_ruffian=6", "karstark_shock_cavalry=3" },
            new[] { "clan_glover_party_template", "Army175NorthFoot", "1",
                "glover_archer=6", "glover_veteran_archer=4", "glover_footman=5", "glover_man_at_arms=6", "glover_horseman=3" },
            new[] { "clan_manderly_party_template", "Army175NorthFoot", "1",
                "manderly_archer=6", "manderly_veteran_archer=3", "whiteharbor_footman=5", "manderly_man_at_arms=6", "whiteharbor_elite_knight=2" },
            new[] { "clan_umber_party_template", "Army175NorthFoot", "1",
                "umber_archer=7", "umber_marksman=2", "umber_footman=5", "umber_man_at_arms=5", "umber_horseman=3" },
            new[] { "clan_mormont_party_template", "Army175NorthFoot", "1",
                "mormont_trapper=7", "mormont_footman=5", "mormont_horseman=4" },
            new[] { "clan_cerwyn_party_template", "Army175NorthFoot", "1",
                "cerwyn_archer=6", "cerwyn_veteran_archer=4", "cerwyn_soldier=5", "cerwyn_axeman=6", "cerwyn_horseman=3" },
            // 2.4 Volantis i Norvos - propozycja audytu 4.6 bez decyzji Jeffa (dom. WYL.); slonie zostaja
            new[] { "kingdom_hero_party_volantine_template", "Army175VolantisNorvos", "0",
                "volantine_bowman=4", "tigercloak_archer=2", "tigercloak_master_archer=1", "volantine_soldier=6", "tigercloak_warrior=4", "tigercloak_elite_warrior=2" },
            new[] { "kingdom_hero_party_norvos_template", "Army175VolantisNorvos", "0",
                "norvos_horseman=2", "norvos_cavalry=1", "norvos_axeman=5", "norvos_master_axeman=2" },
            // 2.5 Wolni Ludzie, Smocza Skala - propozycja audytu 4.12 bez decyzji Jeffa (dom. WYL.)
            new[] { "kingdom_hero_party_freefolk_template", "Army175MinorLore", "0", "freefolk_horseman=1" },
            new[] { "clan_rayder_party_template", "Army175MinorLore", "0", "freefolk_horseman=1" },
            new[] { "kingdom_hero_party_dragonstone_template", "Army175MinorLore", "0",
                "dragonstone_rider=1", "dragonstone_horseman=1", "dragonstone_man_at_arms=4", "dragonstone_brute=5" },
            new[] { "clan_velaryon_party_template", "Army175MinorLore", "0",
                "velaryon_scout=2", "velaryon_horseman=1", "velaryon_warrior=5" },
            new[] { "clan_celtigar_party_template", "Army175MinorLore", "0",
                "celtigar_horseman=2", "celtigar_knight=1", "celtigar_man_at_arms=7" },
        };

        // (szablon, wylacznik, domyslnie, zamiany "z>na" - ta sama liczba ludzi, ten sam tier)
        private static readonly string[][] SwapPlan =
        {
            // 2.2 patrole Zelaznych Wysp (patroli ROT nie wymienia) i nagroda wasalna sturgia
            new[] { "patrol_party_sturgia_template_level_1", "Army175IronbornFoot", "1", "sturgian_hardened_brigand>sturgian_berzerker" },
            new[] { "patrol_party_sturgia_template_level_2", "Army175IronbornFoot", "1", "sturgian_horse_raider>sturgian_shock_troop", "sturgian_hardened_brigand>sturgian_berzerker" },
            new[] { "patrol_party_sturgia_template_level_3", "Army175IronbornFoot", "1", "sturgian_horse_raider>sturgian_shock_troop", "sturgian_hardened_brigand>sturgian_berzerker" },
            new[] { "vassal_reward_troops_sturgia", "Army175IronbornFoot", "1", "sturgian_hardened_brigand>sturgian_berzerker" },
        };

        // (jednostka, wylacznik, domyslnie, nowe cele awansu...)
        private static readonly string[][] TreePlan =
        {
            new[] { "khuzait_nomad", "Army175DothrakiRide", "1", "khuzait_tribal_warrior" },
            new[] { "sturgian_brigand", "Army175IronbornFoot", "1", "sturgian_berzerker", "sturgian_spearman" },
            new[] { "greyjoy_soldier", "Army175IronbornFoot", "1", "greyjoy_houseguard", "greyjoy_archer" },
        };

        // piesi Dothrakowie dopisywani do puli ROT (2.1, OBOWIAZKOWE przy Army175DothrakiRide)
        private static readonly string[] DothrakiFoot =
        {
            "khuzait_footman", "khuzait_spearman", "khuzait_spear_infantry", "khuzait_darkhan", "khuzait_hunter", "khuzait_archer", "khuzait_marksman"
        };
        private static List<CharacterObject> _dothrakiFoot;
        private static CharacterObject _dothrakiRoot;

        private static bool PlanOn(string key, string def)
        {
            if (!Wanted("Army175Composition", 1f)) return false;
            return Wanted(key, def == "1" ? 1f : 0f);
        }

        /// <summary>Cel ROT (stosy t3+, formacja z default_group) P/S/J/KL w procentach i ludzie (suma max).</summary>
        private static string Shape(PartyTemplateObject tpl, out int men)
        {
            men = 0;
            int p = 0, s = 0, j = 0, kl = 0, n = 0;
            foreach (var st in tpl.Stacks)
            {
                if (st.Character == null) continue;
                men += st.MaxValue;
                int t = 0; try { t = st.Character.Tier; } catch { }
                if (t < 3) continue;
                bool r = st.Character.IsRanged, m = st.Character.IsMounted;
                if (m && r) kl += st.MaxValue; else if (m) j += st.MaxValue; else if (r) s += st.MaxValue; else p += st.MaxValue;
                n += st.MaxValue;
            }
            if (n == 0) return "0/0/0/0";
            return (int)Math.Round(100.0 * p / n) + "/" + (int)Math.Round(100.0 * s / n) + "/" + (int)Math.Round(100.0 * j / n) + "/" + (int)Math.Round(100.0 * kl / n);
        }

        internal static void Composition()
        {
            var lines = new List<string>();
            var skipped = new List<string>();
            var missing = new List<string>();
            int changedTpl = 0;
            var om = MBObjectManager.Instance;

            foreach (var row in StackPlan)
            {
                if (!PlanOn(row[1], row[2])) { skipped.Add(row[0] + " (" + row[1] + ")"); continue; }
                try
                {
                    var tpl = om.GetObject<PartyTemplateObject>(row[0]);
                    if (tpl == null || tpl.Stacks == null) { missing.Add("szablon " + row[0]); continue; }
                    int men0; string a = Shape(tpl, out men0);
                    for (int k = 3; k < row.Length; k++)
                    {
                        var parts = row[k].Split('=');
                        var ch = om.GetObject<CharacterObject>(parts[0]);
                        int n = int.Parse(parts[1]);
                        if (ch == null) { missing.Add(row[0] + ": " + parts[0]); continue; }
                        int idx = -1;
                        for (int i = 0; i < tpl.Stacks.Count; i++) if (tpl.Stacks[i].Character == ch) { idx = i; break; }
                        if (n <= 0) { if (idx >= 0) tpl.Stacks.RemoveAt(idx); }
                        else if (idx >= 0) tpl.Stacks[idx] = new PartyTemplateStack(ch, n, n);
                        else tpl.Stacks.Add(new PartyTemplateStack(ch, n, n));
                    }
                    int men1; string b = Shape(tpl, out men1);
                    lines.Add("Mends: sklad 175 - " + row[0] + ": cel P/S/J/KL " + a + " -> " + b + ", ludzi " + men0 + " -> " + men1);
                    changedTpl++;
                }
                catch (Exception e) { missing.Add(row[0] + " potkniecie " + e.GetType().Name); }
            }

            foreach (var row in SwapPlan)
            {
                if (!PlanOn(row[1], row[2])) { skipped.Add(row[0] + " (" + row[1] + ")"); continue; }
                try
                {
                    var tpl = om.GetObject<PartyTemplateObject>(row[0]);
                    if (tpl == null || tpl.Stacks == null) { missing.Add("szablon " + row[0]); continue; }
                    int men0; string a = Shape(tpl, out men0);
                    var done = new List<string>();
                    for (int k = 3; k < row.Length; k++)
                    {
                        var parts = row[k].Split('>');
                        var from = om.GetObject<CharacterObject>(parts[0]);
                        var to = om.GetObject<CharacterObject>(parts[1]);
                        if (from == null || to == null) { missing.Add(row[0] + ": " + row[k]); continue; }
                        int iFrom = -1, iTo = -1;
                        for (int i = 0; i < tpl.Stacks.Count; i++)
                        {
                            if (tpl.Stacks[i].Character == from) iFrom = i;
                            else if (tpl.Stacks[i].Character == to) iTo = i;
                        }
                        if (iFrom < 0) continue;   // juz zamienione (idempotentnie) albo brak w tej wersji danych
                        var sf = tpl.Stacks[iFrom];
                        if (iTo >= 0)
                        {
                            var stt = tpl.Stacks[iTo];
                            tpl.Stacks[iTo] = new PartyTemplateStack(to, stt.MinValue + sf.MinValue, stt.MaxValue + sf.MaxValue);
                            tpl.Stacks.RemoveAt(iFrom);
                        }
                        else tpl.Stacks[iFrom] = new PartyTemplateStack(to, sf.MinValue, sf.MaxValue);
                        done.Add(parts[0] + " " + sf.MaxValue + " -> " + parts[1]);
                    }
                    int men1; string b = Shape(tpl, out men1);
                    lines.Add("Mends: sklad 175 - " + row[0] + ": " + (done.Count > 0 ? string.Join(", ", done.ToArray()) : "bez zmian") + "; cel P/S/J/KL " + a + " -> " + b + ", ludzi " + men0 + " -> " + men1);
                    changedTpl++;
                }
                catch (Exception e) { missing.Add(row[0] + " potkniecie " + e.GetType().Name); }
            }

            var trees = new List<string>();
            var setTargets = AccessTools.PropertySetter(typeof(CharacterObject), "UpgradeTargets");
            foreach (var row in TreePlan)
            {
                if (!PlanOn(row[1], row[2])) { skipped.Add("drzewo " + row[0] + " (" + row[1] + ")"); continue; }
                try
                {
                    var co = om.GetObject<CharacterObject>(row[0]);
                    if (co == null) { missing.Add("drzewo " + row[0]); continue; }
                    if (setTargets == null) { missing.Add("drzewo " + row[0] + " (brak settera UpgradeTargets)"); continue; }
                    var nt = new List<CharacterObject>();
                    for (int k = 3; k < row.Length; k++)
                    {
                        var t = om.GetObject<CharacterObject>(row[k]);
                        if (t == null) { missing.Add("drzewo " + row[0] + ": " + row[k]); continue; }
                        nt.Add(t);
                    }
                    if (nt.Count == 0) continue;
                    var old = co.UpgradeTargets ?? new CharacterObject[0];
                    var olds = new List<string>(); foreach (var x in old) if (x != null) olds.Add(x.StringId);
                    var news = new List<string>(); foreach (var x in nt) news.Add(x.StringId);
                    setTargets.Invoke(co, new object[] { nt.ToArray() });
                    trees.Add(row[0] + " [" + string.Join(", ", olds.ToArray()) + "] -> [" + string.Join(", ", news.ToArray()) + "]");
                }
                catch (Exception e) { missing.Add("drzewo " + row[0] + " potkniecie " + e.GetType().Name); }
            }

            // 2.1: pula ROT - piesi Dothrakowie zostaja (dopisek w postfiksie Settings)
            DothrakiPoolActive = PlanOn("Army175DothrakiRide", "1");
            _dothrakiFoot = new List<CharacterObject>();
            _dothrakiRoot = om.GetObject<CharacterObject>("khuzait_nomad");
            if (DothrakiPoolActive)
                foreach (var id in DothrakiFoot)
                {
                    var c = om.GetObject<CharacterObject>(id);
                    if (c != null) _dothrakiFoot.Add(c); else missing.Add("piesi Dothrakow: " + id);
                }

            CompositionApplied = true;
            foreach (var l in lines) Scribe.Line(l + ".");
            if (trees.Count > 0) Scribe.Line("Mends: sklad 175 - drzewa: " + string.Join("; ", trees.ToArray()) + ".");
            if (DothrakiPoolActive)
            {
                int dummy; var kh = om.GetObject<PartyTemplateObject>("kingdom_hero_party_khuzait_template");
                Scribe.Line("Mends: sklad 175 - Dothrakowie: piesi w puli ROT (" + _dothrakiFoot.Count + "), cel " + (kh != null ? Shape(kh, out dummy) : "?")
                            + "; postfiks puli ROT " + (_rotSettingsPatched ? "wpiety" : "NIE wpiety - piesi Dothrakowie wypadna z puli") + ".");
            }
            Scribe.Line("Mends: sklad 175 - szablonow zmienionych " + changedTpl + ", drzew " + trees.Count
                        + (skipped.Count > 0 ? "; pominiete przez wylacznik: " + string.Join(", ", skipped.ToArray()) : "")
                        + (missing.Count > 0 ? "; BRAK w danych (pominiete): " + string.Join(", ", missing.ToArray()) : "") + ".");
        }

        // ---------------- postfiks ROT ROTTroopRecruiter.Settings (2.1) ----------------

        private static bool _rotSettingsPatched;
        private static System.Reflection.PropertyInfo _pPartyTemplate;

        /// <summary>Wpiecie postfiksu na ROT ROTTroopRecruiter.Settings(Hero, Settlement) (internal; refleksja po nazwie typu).</summary>
        internal static void Install(Harmony harmony)
        {
            try
            {
                var t = AccessTools.TypeByName("ROT.CampaignBehaviors.ROTTroopRecruiter");
                var m = t != null ? AccessTools.Method(t, "Settings", new[] { typeof(Hero), typeof(TaleWorlds.CampaignSystem.Settlements.Settlement) }) : null;
                if (m == null) { Scribe.Line("Mends: sklad 175 - ROTTroopRecruiter.Settings nieznaleziony - pula ROT bez dopisku pieszych Dothrakow."); return; }
                harmony.Patch(m, postfix: new HarmonyMethod(typeof(Army175), "RotPoolKeepFoot"));
                _rotSettingsPatched = true;
                Scribe.Line("Mends: sklad 175 - postfiks puli ROT (ROTTroopRecruiter.Settings) wpiety: piesi Dothrakowie zostaja w puli, gdy pula ma khuzait_nomad.");
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "Army175.Install", null); } catch { } }
        }

        /// <summary>Postfiks: warunek po ZAWARTOSCI wyniku (ROT pamieta pule osady od pierwszego wywolania i nie
        /// czysci jej przy zmianie wlasciciela) - pula z khuzait_nomad (korzen drzewa wsi Dothrakow) dostaje
        /// brakujacych z 7 pieszych. Idempotentnie (Contains). Composition (cel) bez zmian.</summary>
        public static void RotPoolKeepFoot(object __result)
        {
            try
            {
                if (!DothrakiPoolActive || __result == null || _dothrakiRoot == null || _dothrakiFoot == null || _dothrakiFoot.Count == 0) return;
                if (_pPartyTemplate == null) _pPartyTemplate = AccessTools.Property(__result.GetType(), "PartyTemplate");
                var list = _pPartyTemplate != null ? _pPartyTemplate.GetValue(__result, null) as List<CharacterObject> : null;
                if (list == null || !list.Contains(_dothrakiRoot)) return;
                int added = 0;
                for (int i = 0; i < _dothrakiFoot.Count; i++)
                    if (!list.Contains(_dothrakiFoot[i])) { list.Add(_dothrakiFoot[i]); added++; }
                if (added > 0)
                {
                    if (!_poolFirstLogged)
                    {
                        _poolFirstLogged = true;
                        Scribe.Line("Mends: sklad 175 - Dothrakowie: pierwszy dopisek pieszych do puli ROT (" + added + " jednostek) - starzy piesi nie zamienia sie na konnych bez koni.");
                    }
                }
            }
            catch { }
        }
    }
}
