using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace CrashScribe
{
    /// <summary>P3 audytu modow 14.10 (docs/audyt-modow-2026-10-14/RAPORT-AUDYT-MODOW.md rozdz. 3 i 5) - naprawy cudzych modow w CrashScribe,
    /// kazda pod wlasnym kluczem (Config), wylaczona = zachowanie b47fecc3. Pliki BK / BKROTPatch / ROT nietkniete.
    /// AM-29 (CultureTemplateRepair): ROT-Content wpisuje do list wzorcow kultur (lord_templates, notable_templates, rebellion_hero_templates)
    /// odwolania do postaci, ktorych nie ma (RGL 'Null object reference found': spc_brotherhood_2, spc_notable_reach_6, spc_yiti_headman_1,
    /// yiti_clansman ...). Gra dodaje odwolanie bez sprawdzenia (CultureObject.Deserialize), a MBObjectManager.UnregisterNonReadyObjects zostawia
    /// w liscie obiekt zastepczy (IsReady false, bez BodyPropertyRange). Rod towarzysza (CompanionRolesCampaignBehavior.SpawnNewHeroesForNewCompanionClan)
    /// losuje wzorzec lorda 2 razy - w Westerlands (vlandia, 1 z 12 zepsuty) ok. 16% szansy na pewny NullReference w
    /// DefaultHeroCreationModel.GetStaticBodyProperties w srodku zakladania rodu. Przy KAZDYM starcie sesji (listy nie sa w zapisie, gra buduje je
    /// z XML przy kazdym wczytaniu) z trzech list kazdej kultury wylatuja wpisy null / nie gotowe / bez BodyPropertyRange (nowa lista przez
    /// prywatny setter; lista, z ktorej nic by nie zostalo - bez zmian + linia). Siatka: prefiks na CreateNewHeroForNewCompanionClan podmienia
    /// zepsuty wzorzec na gotowy wzorzec lorda tej samej kultury (gdy filtr nie zadzialal).</summary>
    internal static class AuditP3
    {
        /// <summary>Start procesu (OnBeforeInitialModuleScreenSetAsRoot, po Mends.Install i BkTitles.Install). Nie rzuca.</summary>
        internal static void Install(Harmony harmony)
        {
            TemplatesInstall(harmony);
        }

        /// <summary>OnSessionLaunched (nowa gra i kazde wczytanie). Nie rzuca.</summary>
        internal static void OnSession()
        {
            TemplatesRepair();
        }

        // ------------------------------------------------------------------ AM-29: wzorce kultur
        private static readonly string[] TemplateLists = { "LordTemplates", "NotableTemplates", "RebelliousHeroTemplates" };
        private static int _netSwaps, _netMiss;
        private static bool _netHooked;

        private static bool Broken(CharacterObject c)
        {
            try { return c == null || !c.IsReady || c.BodyPropertyRange == null; }
            catch { return true; }
        }

        private static void TemplatesInstall(Harmony harmony)
        {
            try
            {
                if (!Config.CultureTemplateRepair)
                {
                    Scribe.Line("Mends: wzorce kultur (AM-29) wylaczone (CultureTemplateRepair=false) - listy wzorcow ROT jak dotad (zepsute wpisy zostaja).");
                    return;
                }
                var t = AccessTools.TypeByName("TaleWorlds.CampaignSystem.CampaignBehaviors.CompanionRolesCampaignBehavior");
                var m = t != null ? AccessTools.Method(t, "CreateNewHeroForNewCompanionClan") : null;
                var ps = m != null ? m.GetParameters() : null;
                if (m != null && ps.Length >= 2 && ps[0].ParameterType == typeof(CharacterObject) && ps[1].ParameterType == typeof(Settlement))
                {
                    harmony.Patch(m, prefix: new HarmonyMethod(typeof(AuditP3), nameof(CompanionTemplateNet)));
                    _netHooked = true;
                }
                Scribe.Line("Mends: wzorce kultur (AM-29) - siatka na rod towarzysza (CreateNewHeroForNewCompanionClan) " + (_netHooked ? "wpieta" : "NIE wpieta (brak metody albo inne parametry)")
                            + "; filtr list wzorcow przy kazdym starcie sesji.");
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "AuditP3.TemplatesInstall", null); } catch { } }
        }

        /// <summary>Filtr list wzorcow wszystkich kultur. Linia: ile usunietych, z jakich kultur i list; pozostalo zepsutych (cel 0).</summary>
        internal static void TemplatesRepair()
        {
            if (!Config.CultureTemplateRepair) return;
            try
            {
                var cultures = MBObjectManager.Instance != null ? MBObjectManager.Instance.GetObjectTypeList<CultureObject>() : null;
                if (cultures == null) { Scribe.Line("Mends: wzorce kultur (AM-29) - brak listy kultur (MBObjectManager)."); return; }
                int removed = 0, keptEmpty = 0, leftBroken = 0, stumbles = 0, checkedN = 0;
                var parts = new List<string>();
                foreach (var cu in cultures)
                {
                    if (cu == null) continue;
                    foreach (var name in TemplateLists)
                    {
                        try
                        {
                            var prop = AccessTools.Property(typeof(CultureObject), name);
                            var setter = AccessTools.PropertySetter(typeof(CultureObject), name);
                            var list = prop != null ? prop.GetValue(cu, null) as MBReadOnlyList<CharacterObject> : null;
                            if (list == null || setter == null) continue;
                            checkedN += list.Count;
                            var good = new MBList<CharacterObject>();
                            var bad = new List<string>();
                            foreach (var c in list)
                            {
                                if (Broken(c)) bad.Add(c != null ? c.StringId : "null");
                                else good.Add(c);
                            }
                            if (bad.Count == 0) continue;
                            string kind = name == "LordTemplates" ? "lord" : (name == "NotableTemplates" ? "notable" : "rebel");
                            if (good.Count == 0)
                            {
                                keptEmpty++; leftBroken += bad.Count;
                                parts.Add(cu.StringId + " " + kind + " " + list.Count + " - zostawiona (same zepsute: " + string.Join(", ", bad.ToArray()) + ")");
                                continue;
                            }
                            setter.Invoke(cu, new object[] { good });
                            removed += bad.Count;
                            parts.Add(cu.StringId + " " + kind + " " + list.Count + "->" + good.Count + " [" + string.Join(", ", bad.ToArray()) + "]");
                        }
                        catch (Exception e)
                        {
                            stumbles++;
                            if (stumbles == 1) try { Scribe.Report("CrashScribe", e, "AuditP3.TemplatesRepair(" + cu.StringId + "." + name + ")", null); } catch { }
                        }
                    }
                }
                Scribe.Line("Mends: wzorce kultur (AM-29) - start sesji: wzorcow sprawdzonych " + checkedN + ", usunietych " + removed
                            + (parts.Count > 0 ? ": " + string.Join(" | ", parts.ToArray()) : "")
                            + "; list zostawionych bez zmian (same zepsute) " + keptEmpty + ", pozostalo zepsutych " + leftBroken
                            + ", siatka rodu towarzysza " + (_netHooked ? "wpieta" : "NIE") + " (podmian w procesie " + _netSwaps + ", bez zastepcy " + _netMiss + ")"
                            + ", potkniec " + stumbles + ".");
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "AuditP3.TemplatesRepair", null); } catch { } }
        }

        /// <summary>Siatka AM-29: zepsuty wzorzec przy tworzeniu bohatera rodu towarzysza -> gotowy wzorzec lorda tej samej kultury
        /// (kultura, ktorej lista go zawiera; inaczej kultura osady; inaczej pierwsza kultura z gotowym wzorcem lorda).</summary>
        public static void CompanionTemplateNet(ref CharacterObject __0, Settlement __1)
        {
            try
            {
                if (!Config.CultureTemplateRepair || !Broken(__0)) return;
                string was = __0 != null ? __0.StringId : "null";
                CharacterObject pick = null;
                CultureObject home = null;
                var cultures = MBObjectManager.Instance.GetObjectTypeList<CultureObject>();
                if (__0 != null)
                    foreach (var cu in cultures)
                    {
                        var lt = cu != null ? cu.LordTemplates : null;
                        if (lt != null && lt.Contains(__0)) { home = cu; break; }
                    }
                if (home == null && __1 != null) home = __1.Culture;
                pick = ReadyLord(home);
                if (pick == null)
                    foreach (var cu in cultures) { pick = ReadyLord(cu); if (pick != null) break; }
                if (pick == null) { _netMiss++; Scribe.Line("Mends: wzorce kultur (AM-29) - siatka: zepsuty wzorzec " + was + " przy rodzie towarzysza, brak gotowego zastepcy (gra moze sie wywrocic)."); return; }
                __0 = pick;
                _netSwaps++;
                Scribe.Line("Mends: wzorce kultur (AM-29) - siatka: zepsuty wzorzec " + was + " przy rodzie towarzysza (" + (__1 != null ? __1.Name.ToString() : "?")
                            + ") -> " + pick.StringId + " (" + (home != null ? home.StringId : "inna kultura") + "), lacznie " + _netSwaps + ".");
            }
            catch { }
        }

        private static CharacterObject ReadyLord(CultureObject cu)
        {
            try
            {
                var lt = cu != null ? cu.LordTemplates : null;
                if (lt == null) return null;
                var ok = new List<CharacterObject>();
                foreach (var c in lt) if (!Broken(c)) ok.Add(c);
                return ok.Count > 0 ? ok[MBRandom.RandomInt(ok.Count)] : null;
            }
            catch { return null; }
        }
    }

    /// <summary>P3 audytu 14.10: zdarzenia kampanii (start sesji).</summary>
    internal sealed class AuditP3Behavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, delegate (CampaignGameStarter s) { AuditP3.OnSession(); });
        }

        public override void SyncData(IDataStore dataStore) { }
    }
}
