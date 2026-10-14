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
    /// zepsuty wzorzec na gotowy wzorzec lorda tej samej kultury (gdy filtr nie zadzialal).
    /// AM-01 (StopBkrotProsperityReset): 3 prefiksy BKROT EarlyHeal zdjete (dobrobyt twierdzy &lt; 100 -> 2000 / 4000 z niczego), straz NaN / Inf
    /// na Town.set_Prosperity - opis przy ProsperityInstall.</summary>
    internal static class AuditP3
    {
        /// <summary>Start procesu (OnBeforeInitialModuleScreenSetAsRoot, po Mends.Install i BkTitles.Install). Nie rzuca.</summary>
        internal static void Install(Harmony harmony)
        {
            TemplatesInstall(harmony);
            ProsperityInstall(harmony);
        }

        /// <summary>OnSessionLaunched (nowa gra i kazde wczytanie). Nie rzuca.</summary>
        internal static void OnSession()
        {
            TemplatesRepair();
            ProsperitySession();
        }

        /// <summary>Koniec doby (DailyTick). Nie rzuca.</summary>
        internal static void OnDaily()
        {
            ProsperityDaily();
        }

        /// <summary>Typ po pelnej nazwie: najpierw w assembly BKROTPatch, potem wszedzie (AccessTools).</summary>
        private static Type FindType(string fullName, string asm)
        {
            Type t = null;
            try { t = Type.GetType(fullName + ", " + asm, false); } catch { }
            if (t == null) try { t = AccessTools.TypeByName(fullName); } catch { }
            return t;
        }

        /// <summary>Zdejmuje cudzy prefiks ze WSZYSTKICH metod, na ktorych wisi (Harmony.Unpatch po metodzie latki - jak Mends.Unhook
        /// lataczki stylow zycia BK; tu bez nazwy celu, wiec przeciazenia i nazwy parametrow nie maja znaczenia). Zwraca liczbe zdjec.</summary>
        private static int UnhookEverywhere(Harmony harmony, MethodInfo theirPrefix, List<MethodBase> from)
        {
            int n = 0;
            if (harmony == null || theirPrefix == null) return 0;
            foreach (var m in new List<MethodBase>(Harmony.GetAllPatchedMethods()))
            {
                try
                {
                    var info = Harmony.GetPatchInfo(m);
                    if (info == null || info.Prefixes == null) continue;
                    bool has = false;
                    foreach (var p in info.Prefixes) if (p != null && p.PatchMethod == theirPrefix) { has = true; break; }
                    if (!has) continue;
                    harmony.Unpatch(m, theirPrefix);
                    if (from != null) from.Add(m);
                    n++;
                }
                catch { }
            }
            return n;
        }

        /// <summary>Czy prefiks wisi na danej metodzie (Harmony.GetPatchInfo jednej metody - tanio, raz na dobe).</summary>
        private static bool PrefixedOn(MethodBase m, MethodInfo prefix)
        {
            try
            {
                if (m == null || prefix == null) return false;
                var info = Harmony.GetPatchInfo(m);
                if (info == null || info.Prefixes == null) return false;
                foreach (var p in info.Prefixes) if (p != null && p.PatchMethod == prefix) return true;
            }
            catch { }
            return false;
        }

        /// <summary>Metoda, na ktorej wisi dany prefiks (albo null) - przeglad wszystkich latanych metod, tylko przy starcie procesu.</summary>
        private static MethodBase PrefixedWhere(MethodInfo prefix)
        {
            if (prefix == null) return null;
            foreach (var m in Harmony.GetAllPatchedMethods())
            {
                try
                {
                    var info = Harmony.GetPatchInfo(m);
                    if (info == null || info.Prefixes == null) continue;
                    foreach (var p in info.Prefixes) if (p != null && p.PatchMethod == prefix) return m;
                }
                catch { }
            }
            return null;
        }

        // ------------------------------------------------------------------ AM-01: BKROT EarlyHeal (dobrobyt ponizej 100 -> 2000 / 4000)
        // BKROTPatch wiesza 3 prefiksy (Priority 800), ktore przy dobrobycie twierdzy < 100 (albo NaN / Inf) wpisuja 2000 (zamek) / 4000 (miasto):
        // BKROTProsperityHeal_Patch na getterze Town.Prosperity (wyzwala go KAZDY odczyt, takze nasze tabele), VanillaLoyaltyChange_PREFIX_Heal_Patch
        // i VanillaSecurityChange_PREFIX_Heal_Patch (DefaultSettlementLoyaltyModel.CalculateLoyaltyChange / DefaultSettlementSecurityModel.CalculateSecurityChange).
        // Gra przycina dobrobyt tylko ponizej 0 (Town.set_Prosperity), ani gra, ani BK, ani nasze mody nie dziela przez dobrobyt - wiec zamek, ktory
        // naprawde upada (glod, oblezenie, spustoszenie), dostawal +1900 z niczego (RGL: Willow Wood 97.42 -> 2000, Pinkmaiden 99.03 -> 2000).
        // Klucz StopBkrotProsperityReset: 3 prefiksy zdjete przy starcie procesu (po OnSubModuleLoad BKROT), w zamian straz na Town.set_Prosperity:
        // NaN / Inf -> stara wartosc zostaje (licznik). Leczenie lojalnosci i bezpieczenstwa BKROT na getterach (prog < 0, NaN) zostaje.
        // Start sesji: NaN / Inf w zapisie -> 0 (jednorazowo, linia). Koniec doby: czy prefiks BKROT nie wrocil (konsola BKROT BkrotPatchToggle).
        private static readonly string[] HealPatches =
        {
            "BKROTPatch.Patches.BKROTProsperityHeal_Patch",
            "BKROTPatch.Patches.VanillaLoyaltyChange_PREFIX_Heal_Patch",
            "BKROTPatch.Patches.VanillaSecurityChange_PREFIX_Heal_Patch"
        };
        private static readonly MethodInfo[] _healPrefix = new MethodInfo[3];
        private static readonly MethodBase[] _healTarget = new MethodBase[3];   // gdzie wisial (albo cel wedlug nazwy) - kontrola doby tylko tej metody
        private static bool _healOn, _guardHooked;
        private static int _nanBlocked, _nanBlockedDay, _healBackDays;

        private static void ProsperityInstall(Harmony harmony)
        {
            try
            {
                if (!Config.StopBkrotProsperityReset)
                {
                    Scribe.Line("Mends: dobrobyt (AM-01) wylaczone (StopBkrotProsperityReset=false) - BKROT EarlyHeal jak dotad (twierdza ponizej 100 dobrobytu dostaje 2000 / 4000).");
                    return;
                }
                _healOn = true;
                int found = 0, off = 0;
                var parts = new List<string>();
                for (int i = 0; i < HealPatches.Length; i++)
                {
                    string shortName = HealPatches[i].Substring(HealPatches[i].LastIndexOf('.') + 1);
                    var t = FindType(HealPatches[i], "BKROTPatch");
                    var pm = t != null ? AccessTools.Method(t, "Prefix") : null;
                    _healPrefix[i] = pm;
                    if (pm == null) { parts.Add(shortName + " brak"); continue; }
                    found++;
                    var from = new List<MethodBase>();
                    int removed = UnhookEverywhere(harmony, pm, from);
                    _healTarget[i] = from.Count > 0 ? from[0] : HealTargetByName(i);
                    var still = PrefixedWhere(pm);
                    if (still == null) off++;
                    parts.Add(shortName + (still != null ? " NIE ZDJETY (" + still.Name + ")" : (removed > 0 ? " zdjety" : " nie byl wpiety")));
                }
                var setter = AccessTools.PropertySetter(typeof(Town), "Prosperity");
                if (setter != null)
                {
                    harmony.Patch(setter, prefix: new HarmonyMethod(typeof(AuditP3), nameof(ProsperityNanGuard)));
                    _guardHooked = true;
                }
                Scribe.Line("Mends: dobrobyt (AM-01) - " + (found == 0 ? "BKROTPatch bez latek EarlyHeal (nic do zdjecia), " : "")
                            + "BKROT EarlyHeal zdjety " + off + "/" + HealPatches.Length + " (" + string.Join(", ", parts.ToArray()) + "); straz NaN/Inf na Town.set_Prosperity "
                            + (_guardHooked ? "wpieta" : "NIE wpieta") + " (NaN / Inf -> stara wartosc zostaje).");
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "AuditP3.ProsperityInstall", null); } catch { } }
        }

        /// <summary>Cel prefiksu BKROT wedlug nazwy (gdy przy starcie nie wisial): getter Town.Prosperity, CalculateLoyaltyChange, CalculateSecurityChange.</summary>
        private static MethodBase HealTargetByName(int i)
        {
            try
            {
                if (i == 0) return AccessTools.PropertyGetter(typeof(Town), "Prosperity");
                var t = i == 1 ? typeof(TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementLoyaltyModel)
                               : typeof(TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementSecurityModel);
                string name = i == 1 ? "CalculateLoyaltyChange" : "CalculateSecurityChange";
                foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                    if (m.Name == name) return m;
            }
            catch { }
            return null;
        }

        /// <summary>Straz AM-01 na Town.set_Prosperity: NaN / Inf nie wchodzi (zwraca false = stara wartosc zostaje).</summary>
        public static bool ProsperityNanGuard(float __0)
        {
            if (!float.IsNaN(__0) && !float.IsInfinity(__0)) return true;
            if (!_healOn) return true;
            _nanBlocked++; _nanBlockedDay++;
            return false;
        }

        /// <summary>Start sesji AM-01: NaN / Inf dobrobytu w zapisie -> 0 (bez BKROT nikt by go nie wyleczyl); twierdze ponizej 100 - liczba.</summary>
        private static void ProsperitySession()
        {
            if (!_healOn) return;
            try
            {
                int n = 0, nan = 0, low = 0;
                var lows = new List<string>();
                foreach (var st in Settlement.All)
                {
                    var t = st != null ? st.Town : null;
                    if (t == null) continue;
                    n++;
                    float p = t.Prosperity;
                    if (float.IsNaN(p) || float.IsInfinity(p)) { t.Prosperity = 0f; nan++; continue; }
                    if (p < 100f) { low++; if (lows.Count < 8) lows.Add(st.Name + " " + p.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)); }
                }
                Scribe.Line("Mends: dobrobyt (AM-01) - start sesji: twierdz " + n + ", NaN/Inf w zapisie -> 0: " + nan + ", ponizej 100: " + low
                            + (lows.Count > 0 ? " [" + string.Join(", ", lows.ToArray()) + "]" : "") + ".");
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "AuditP3.ProsperitySession", null); } catch { } }
        }

        /// <summary>Koniec doby AM-01: licznik NaN, kontrola Harmony.GetPatchInfo (czy prefiks BKROT nie wrocil), twierdze ponizej 100.</summary>
        private static void ProsperityDaily()
        {
            if (!_healOn) return;
            try
            {
                var back = new List<string>();
                for (int i = 0; i < _healPrefix.Length; i++)
                {
                    if (PrefixedOn(_healTarget[i], _healPrefix[i]))
                        back.Add(HealPatches[i].Substring(HealPatches[i].LastIndexOf('.') + 1) + " na " + _healTarget[i].Name);
                }
                if (back.Count > 0) _healBackDays++;
                int low = 0;
                foreach (var st in Settlement.All)
                {
                    var t = st != null ? st.Town : null;
                    if (t != null && t.Prosperity < 100f) low++;
                }
                Scribe.Line("Mends: dobrobyt (AM-01) doba: NaN/Inf zablokowanych " + _nanBlockedDay + " (lacznie " + _nanBlocked + "), prefiks EarlyHeal BKROT "
                            + (back.Count == 0 ? "brak" : "WROCIL: " + string.Join(", ", back.ToArray()) + " (konsola BKROT?)") + ", twierdz ponizej 100: " + low + ".");
                _nanBlockedDay = 0;
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "AuditP3.ProsperityDaily", null); } catch { } }
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
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, delegate { AuditP3.OnDaily(); });
        }

        public override void SyncData(IDataStore dataStore) { }
    }
}
