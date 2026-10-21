using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;

namespace Armoury
{
    /// <summary>
    /// m101-prolog E1a - START OPOWIESCI dla ROT. W kampanii z prologiem model czasu (LongYearTimeModel.CampaignStartTime) podaje start KALENDARZA (ROT 299 minus 188 dni),
    /// a cala os ROT ma liczyc od startu OPOWIESCI (data ROT 299, jak w klasycznym starcie). JEDNA latka: transpiler na kazdej metodzie ROT.dll, ktora czyta
    /// CampaignTimeModel.CampaignStartTime (rozpoznanie: 38 miejsc w 28 typach - wydarzenia ROT.Events.*, wojny fabularne ROTStorylineWar/ROTStorylineWars, Inni
    /// ROTOthersCampaignBehavior, zaciag ROTEnlistmentBehavior, AIThinkPatch). Kazde "callvirt CampaignTimeModel::get_CampaignStartTime" zamieniamy na
    /// "call Prologue.StoryStartOf(CampaignTimeModel)" - ten sam stos (model -> CampaignTime), etykiety i bloki bez zmian. StoryStartOf zwraca StoryStartTime
    /// modelu Armoury (start opowiesci); dla innego modelu - CampaignStartTime jak dotad. W klasycznej kampanii oba sa rowne co do tika.
    /// Wpinane LENIWIE: raz na proces, przy pierwszej kampanii z prologiem (nowej - Prologue.OnGameStart, przed zegarem; wczytanej - SyncData, przed pierwszym tickiem).
    /// Klasyczna gra w procesie bez prologu nie dostaje ani jednej latki. Szukanie: wszystkie typy ROT.dll (z zagniezdzonymi - lambdy, iteratory), metody i konstruktory
    /// z cialem; wstepny filtr po bajtach IL (kod call/callvirt + token odwolania do gettera w module ROT), potwierdzenie przez PatchProcessor.ReadMethodBody.
    /// Metody/typy otwarte generycznie - pomijane i liczone (nie da sie ich latac). Linia logu: wpiete N/M metod, podmienionych miejsc X.
    /// Fabula i Inni w CrashScribe licza od tego samego startu (CrashScribe StoryClock czyta StoryStartTime modelu odbiciem).
    /// </summary>
    internal static partial class Prologue
    {
        // odczyty startu, ktore mierza WIEK KAMPANII, nie os opowiesci - zostaja na kalendarzu (bez latki):
        // ROTEnlistmentBehavior (rozmowa zaciagu: "You must wait at least 5 days before enlisting" - w prologu gracz moze sie zaciagnac jak w klasycznym starcie)
        private static readonly string[] CampaignAgeTypes = { "ROT.CampaignBehaviors.ROTEnlistmentBehavior" };

        private static bool IsCampaignAgeType(Type t)
        {
            for (var x = t; x != null; x = x.DeclaringType)
                foreach (var n in CampaignAgeTypes) if (x.FullName == n) return true;
            return false;
        }

        private static bool _storyTried;
        private static string _storyState = "nie wpiete (brak kampanii z prologiem w tym procesie)";
        private static int _storySwapped;

        /// <summary>Podmiana odczytu w ROT: start opowiesci (StoryStartTime modelu Armoury), inny model - jego CampaignStartTime (null - wyjatek jak w oryginale).</summary>
        public static CampaignTime StoryStartOf(CampaignTimeModel model)
        {
            var l = model as LongYearTimeModel;
            return l != null ? l.StoryStartTime : model.CampaignStartTime;
        }

        private static bool IsStartGetter(MethodBase mb)
        {
            var mi = mb as MethodInfo;
            return mi != null && mi.Name == "get_CampaignStartTime" && mi.DeclaringType != null && typeof(CampaignTimeModel).IsAssignableFrom(mi.DeclaringType)
                   && mi.GetParameters().Length == 0 && mi.ReturnType == typeof(CampaignTime);
        }

        public static IEnumerable<CodeInstruction> StoryTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var repl = AccessTools.Method(typeof(Prologue), nameof(StoryStartOf));
            foreach (var ci in instructions)
            {
                if ((ci.opcode == OpCodes.Callvirt || ci.opcode == OpCodes.Call) && ci.operand is MethodBase && IsStartGetter((MethodBase)ci.operand))
                {
                    ci.opcode = OpCodes.Call;
                    ci.operand = repl;
                    _storySwapped++;
                }
                yield return ci;
            }
        }

        /// <summary>Raz na proces, przy pierwszej kampanii z prologiem: transpiler na wszystkich czytelnikach CampaignStartTime w ROT.dll i w ROTBattleAtmosphere
        /// (LoreGates.CampaignDay - atmosfera bitew wedlug dnia opowiesci: bramki "d25", "d120"... z ujemnym dniem zamkniete; bez latki po prologu zostalaby
        /// przesunieta o 188 dni na cala kampanie).</summary>
        internal static void InstallStory(string why)
        {
            if (_storyTried) return;
            _storyTried = true;
            var t0 = DateTime.UtcNow;
            try
            {
                var tBase = AccessTools.TypeByName("ROT.Events.EventBase");
                if (tBase == null) { _storyState = "ROT nieobecny - nic do wpiecia"; Log.Info(Tag + "latki opowiesci ROT: " + _storyState + "."); return; }
                var h = new Harmony("com.jeff.armoury.prologue");
                var tr = new HarmonyMethod(typeof(Prologue), nameof(StoryTranspiler));
                _storyState = ScanPatch(tBase.Assembly, h, tr);
                var tLore = AccessTools.TypeByName("ROTBattleAtmosphere.LoreGates");
                _storyState += tLore != null ? " | ROTBattleAtmosphere: " + ScanPatch(tLore.Assembly, h, tr) : " | ROTBattleAtmosphere nieobecny";
            }
            catch (Exception e) { Stumble("InstallStory", e); _storyState = "BLAD wpinania (" + e.GetType().Name + ")"; }
            Log.Info(Tag + "latki opowiesci ROT (" + why + "): " + _storyState + " w " + (int)(DateTime.UtcNow - t0).TotalMilliseconds + " ms.");
        }

        /// <summary>Jeden zestaw: szukanie czytelnikow gettera (filtr bajtow IL + ReadMethodBody) i transpiler. Opis do linii logu.</summary>
        private static string ScanPatch(Assembly asm, Harmony h, HarmonyMethod tr)
        {
            int found = 0, sites = 0, patched = 0, skipped = 0, failed = 0, age = 0, swapped0 = _storySwapped;
            var getter = AccessTools.PropertyGetter(typeof(CampaignTimeModel), "CampaignStartTime");
            Type[] types;
            try { types = asm.GetTypes(); }
            catch (ReflectionTypeLoadException e) { types = Array.FindAll(e.Types ?? new Type[0], x => x != null); }
            // tokeny odwolan do gettera w module zestawu (MemberRef albo MethodDef) - filtr wstepny po bajtach IL; pamiec tokenow na jeden zestaw
            var tokCache = new Dictionary<int, bool>();
            const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            foreach (var t in types)
            {
                if (t == null) continue;
                var members = new List<MethodBase>();
                try { members.AddRange(t.GetMethods(all)); } catch { }
                try { members.AddRange(t.GetConstructors(all)); } catch { }
                foreach (var m in members)
                {
                    try
                    {
                        if (m.IsAbstract) continue;
                        MethodBody body;
                        try { body = m.GetMethodBody(); } catch { body = null; }
                        if (body == null) continue;
                        var il = body.GetILAsByteArray();
                        if (il == null || !HasGetterCall(m.Module, il, getter, tokCache)) continue;
                        // potwierdzenie i liczba miejsc
                        int n = 0;
                        foreach (var kv in PatchProcessor.ReadMethodBody(m))
                            if ((kv.Key == OpCodes.Callvirt || kv.Key == OpCodes.Call) && kv.Value is MethodBase && IsStartGetter((MethodBase)kv.Value)) n++;
                        if (n == 0) continue;
                        if (IsCampaignAgeType(t)) { age += n; continue; }
                        found++; sites += n;
                        if (t.ContainsGenericParameters || m.ContainsGenericParameters) { skipped++; continue; }
                        if (h.Patch(m, transpiler: tr) != null) patched++; else failed++;
                    }
                    catch (Exception e) { failed++; Stumble("InstallStory " + t.FullName + "." + m.Name, e); }
                }
            }
            return "wpiete " + patched + "/" + found + " metod (miejsc odczytu " + sites + ", podmienionych " + (_storySwapped - swapped0)
                   + (skipped > 0 ? ", generycznych pominietych " + skipped : "") + ", wiek kampanii (zaciag) bez latki " + age + (failed > 0 ? ", NIEUDANYCH " + failed : "") + ")";
        }

        /// <summary>Czy w bajtach IL jest call/callvirt (0x28/0x6F) z tokenem, ktory rozwiazuje sie na getter CampaignStartTime (lub jego nadpisanie). Wynik tokenu w pamieci.</summary>
        private static bool HasGetterCall(Module mod, byte[] il, MethodInfo getter, Dictionary<int, bool> cache)
        {
            for (int i = 0; i + 4 < il.Length; i++)
            {
                byte op = il[i];
                if (op != 0x28 && op != 0x6F) continue;
                int tok = il[i + 1] | (il[i + 2] << 8) | (il[i + 3] << 16) | (il[i + 4] << 24);
                int table = (tok >> 24) & 0xFF;
                if (table != 0x0A && table != 0x06) continue;   // MemberRef / MethodDef
                bool hit;
                int key = tok;   // jeden modul (ROT.dll)
                if (!cache.TryGetValue(key, out hit))
                {
                    hit = false;
                    try { var r = mod.ResolveMethod(tok); hit = r != null && IsStartGetter(r); } catch { hit = false; }
                    cache[key] = hit;
                }
                if (hit) return true;
            }
            return false;
        }

        internal static string StoryState { get { return _storyState; } }
    }
}
