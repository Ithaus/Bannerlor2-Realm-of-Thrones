using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// ODSIECZ DR - JEDNO OKIENKO RELACJI (n20-relacje, poprawka po recenzji; Jeff 19.10: "nie wyskakiwalo mi 30 powiadomien ze kazdy lord i ile ...
    /// zbiorczo ... tak mi to nic nie mowi to po co to").
    /// DOWOD: rgl Jeffa 19.10 (rgl_log_4376 / rgl_log_26116): po kazdej wygranej z banda, w chwili wyniku misji, 2-8 okienek "Your relation is increased
    /// by 2 to N with Mallister / Tully,Blackfish / Piper ..." (lordowie z odsieczy), a ok. 25 s pozniej okienka notabli z BanditCheer.
    /// ZRODLO (dekompilacja DynamicReinforcements v2.1.6): BattleStateBehavior.OnMissionResultReady -> Consequences.RewardHelpers(Candidates, playerVictory):
    /// dla kazdego lorda, ktory przyszedl z odsiecza (Accepted, TroopsSpawned > 0, strona gracza) ApplyPlayerRelation(leader, wygrana ? 2 : 1, true, true)
    /// - showQuickNotification = true, czyli jedno okienko gry na lorda (SandBox DefaultNotificationsCampaignBehavior.OnRelationChanged, tekst "_with_clan").
    /// ZMIANA (klucz ReinforcementRelationSummary, domyslnie wlaczony): transpiler podmienia W TEJ JEDNEJ METODZIE wywolanie ApplyPlayerRelation na Apply
    /// ponizej - ta sama relacja tym samym wywolaniem gry, tylko bez okienka; prefiks otwiera paczke, finalizer pokazuje JEDNO okienko zbiorcze
    /// i pisze linie logu. Warunki DR (kto dostaje, ile, RelationEffects, try/catch per lord, linia DRLog) zostaja nietkniete. Wylaczony klucz albo
    /// brak DR = okienko gry przy kazdym lordzie, jak dotad. Kara DR za zignorowane wezwanie (PenalizeRefusal, -1) bez zmian - pojedyncze okienko.
    /// Po co relacja z lordem (zdanie w okienku): DR ReinforcementCalculator.ComputeScore dolicza relacje lorda z dowodca strony (x0.2, do +20 pkt)
    /// - lord, ktory Cie lubi, chetniej przychodzi z odsiecza.
    /// Watek: wynik misji idzie na watku glownym; stan paczki to kilka pol statycznych i jedna mala lista na bitwe.
    /// </summary>
    internal static class ReinforcementThanks
    {
        private const string TypeName = "DynamicReinforcements.Reinforcement.Consequences";
        private const string Why = " Friendlier lords are more willing to join your battles.";

        private static int _swapped;            // podmienione wywolania w RewardHelpers (oczekiwane 1)
        private static bool _wired;

        // stan jednej paczki = jedno wywolanie RewardHelpers (watek glowny misji)
        private static bool _open, _victory;
        private static int _calls, _passed, _flat, _stumbles, _sum, _lo, _hi;
        // jeden wpis na lorda: DR potrafi nagrodzic tego samego lorda 2x w jednej bitwie (dwie partie / kandydaci - rgl Jeffa 13:45:49
        // "by 2 to 9 with Tully,Blackfish", "by 2 to 11 with Tully,Blackfish") - w okienku raz, z suma zysku
        private sealed class Gain { public Hero Who; public string Name; public int Delta, After; }
        private static readonly List<Gain> _gains = new List<Gain>();

        internal static void ApplyAll(Harmony h)
        {
            string miss = null;
            try
            {
                var t = AccessTools.TypeByName(TypeName);
                var m = t != null ? AccessTools.DeclaredMethod(t, "RewardHelpers") : null;
                if (t == null) miss = "brak modu DynamicReinforcements (" + TypeName + ")";
                else if (m == null) miss = "brak metody Consequences.RewardHelpers";
                else
                {
                    var res = h.Patch(m, prefix: new HarmonyMethod(typeof(ReinforcementThanks), nameof(Prefix)),
                            transpiler: new HarmonyMethod(typeof(ReinforcementThanks), nameof(Transpiler)),
                            finalizer: new HarmonyMethod(typeof(ReinforcementThanks), nameof(Finalizer)));
                    _wired = res != null && _swapped > 0;
                    // null = wyjatek Harmony polkniety przez finalizer BKROTPatch (PatchTarget.cs) - slad w Modules/BKROTPatch/BKROTLog.txt
                    if (res == null) miss = "Harmony.Patch zwrocil null (blad latki polkniety - BKROTLog) - okienka DR bez zmian";
                    else if (!_wired) miss = "w RewardHelpers nie ma wywolania ChangeRelationAction.ApplyPlayerRelation (inna wersja DR) - okienka DR bez zmian";
                }
            }
            catch (Exception e) { Log.Error("ReinforcementThanks.ApplyAll", e); miss = "wyjatek przy latce"; }
            var s = Settings.Current;
            Log.Info("ReinforcementThanks: start - jedno okienko relacji za odsiecz DR [" + (s != null && s.ReinforcementRelationSummary ? "wl" : "wyl")
                     + "], latka wpieta " + (_wired ? 1 : 0) + "/1 (DR Consequences.RewardHelpers, podmienione wywolania ApplyPlayerRelation: " + _swapped + ")"
                     + (miss != null ? " | " + miss : "") + ".");
        }

        /// <summary>DR Consequences.RewardHelpers: call ChangeRelationAction.ApplyPlayerRelation(Hero, int, bool, bool) -> Apply (ten sam stos).</summary>
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var helper = AccessTools.DeclaredMethod(typeof(ReinforcementThanks), nameof(Apply));
            var list = new List<CodeInstruction>(instructions);
            int n = 0;
            foreach (var ci in list)
            {
                var m = ci.operand as MethodInfo;
                if ((ci.opcode == OpCodes.Call || ci.opcode == OpCodes.Callvirt) && m != null && m.DeclaringType == typeof(ChangeRelationAction)
                    && m.Name == "ApplyPlayerRelation" && m.GetParameters().Length == 4 && helper != null)
                {
                    ci.opcode = OpCodes.Call;
                    ci.operand = helper;
                    n++;
                }
            }
            _swapped = n;
            return list;
        }

        public static void Prefix(bool playerVictory)
        {
            _open = true; _victory = playerVictory;
            _calls = _passed = _flat = _stumbles = _sum = _hi = 0;
            _lo = 0;
            _gains.Clear();
        }

        /// <summary>Zamiast ApplyPlayerRelation w RewardHelpers: przy wlaczonym kluczu ta sama relacja bez okienka i pomiar zysku; inaczej wywolanie 1:1.</summary>
        public static void Apply(Hero gainedRelationWith, int relation, bool affectRelatives, bool showQuickNotification)
        {
            var s = Settings.Current;
            if (!_open || s == null || !s.ReinforcementRelationSummary)
            {
                ChangeRelationAction.ApplyPlayerRelation(gainedRelationWith, relation, affectRelatives, showQuickNotification);
                if (_open) _passed++;
                return;
            }
            var me = Hero.MainHero;
            int before = 0;
            bool okBefore = false;
            try { before = BanditCheer.BaseRelation(me, gainedRelationWith); okBefore = true; }
            catch (Exception e) { Stumble(gainedRelationWith, e); }
            // wyjatek samej gry leci dalej do try/catch DR per lord - jak dotad
            ChangeRelationAction.ApplyPlayerRelation(gainedRelationWith, relation, affectRelatives, false);
            _calls++;
            if (!okBefore) return;
            try
            {
                int after = BanditCheer.BaseRelation(me, gainedRelationWith);
                int delta = after - before;
                if (delta > 0)
                {
                    _sum += delta;
                    Gain g = null;
                    for (int i = 0; i < _gains.Count; i++) if (ReferenceEquals(_gains[i].Who, gainedRelationWith)) { g = _gains[i]; break; }
                    if (g == null) _gains.Add(new Gain { Who = gainedRelationWith, Name = SafeName(gainedRelationWith), Delta = delta, After = after });
                    else { g.Delta += delta; g.After = after; }
                }
                else _flat++;
            }
            catch (Exception e) { Stumble(gainedRelationWith, e); }
        }

        public static void Finalizer()
        {
            if (!_open) return;
            _open = false;
            // po paczce nie trzymamy bohaterow do nastepnej bitwy (lista czyszczona tez w prefiksie)
            try { Emit(); } finally { _gains.Clear(); }
        }

        private static void Emit()
        {
            try
            {
                if (_passed > 0)
                    Log.Info("ReinforcementThanks (zbiorczo): wylaczone (Reinforcement Relation Summary) - DR pokazal " + _passed + " osobnych okienek relacji.");
                if (_calls <= 0) return;
                if (_gains.Count <= 0)
                {
                    Log.Info("ReinforcementThanks (zbiorczo): bez komunikatu - zaden z " + _calls + " lordow odsieczy nie zyskal (bez zmiany " + _flat
                             + ", potkniecia " + _stumbles + ").");
                    return;
                }
                _lo = int.MaxValue; _hi = 0;
                for (int i = 0; i < _gains.Count; i++)
                {
                    if (_gains[i].Delta < _lo) _lo = _gains[i].Delta;
                    if (_gains[i].Delta > _hi) _hi = _gains[i].Delta;
                }
                string text = BuildText();
                try
                {
                    MBInformationManager.AddQuickInformation(new TextObject("{=!}" + text), BanditCheer.SummaryExtraMs, null, null, "event:/ui/notification/relation");
                }
                catch (Exception e) { Log.Error("ReinforcementThanks.Finalizer(komunikat)", e); }
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < _gains.Count && i < 12; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append(_gains[i].Name).Append(" +").Append(_gains[i].Delta).Append(" -> ").Append(_gains[i].After);
                }
                if (_gains.Count > 12) sb.Append(" i ").Append(_gains.Count - 12).Append(" wiecej");
                Log.Info("ReinforcementThanks (zbiorczo): 1 komunikat zamiast " + _calls + " | lordow odsieczy z zyskiem " + _gains.Count + " (bez zmiany " + _flat
                         + ", potkniecia " + _stumbles + "), relacja +" + _lo + (_hi != _lo ? "..+" + _hi : "") + " na osobe, suma +" + _sum
                         + ", wygrana " + (_victory ? "tak" : "nie") + " | lordowie: " + sb + " | \"" + text + "\"");
            }
            catch (Exception e) { Log.Error("ReinforcementThanks.Finalizer", e); }
        }

        // "Lords who came to your aid think better of you: relation +2 with Jason Mallister, Patrek Mallister and 2 more. Friendlier lords are more willing to join your battles."
        private static string BuildText()
        {
            string gain = _lo == _hi ? "+" + _lo : "+" + _lo + " to +" + _hi;
            int c = _gains.Count;
            if (c == 1)
                return _gains[0].Name + " came to your aid and thinks better of you: relation " + gain + "." + Why;
            string names;
            if (c == 2) names = _gains[0].Name + " and " + _gains[1].Name;
            else if (c == 3) names = _gains[0].Name + ", " + _gains[1].Name + " and " + _gains[2].Name;
            else names = _gains[0].Name + ", " + _gains[1].Name + " and " + (c - 2) + " more";
            return "Lords who came to your aid think better of you: relation " + gain + " with " + names + "." + Why;
        }

        private static void Stumble(Hero h, Exception e)
        {
            // potkniecie przy jednym lordzie: licznik w linii logu, pierwszy blad paczki w logu bledow; reszta lordow dalej
            if (_stumbles++ == 0) Log.Error("ReinforcementThanks.Apply(lord " + SafeName(h) + ")", e);
        }

        private static string SafeName(Hero h)
        {
            try { return h != null && h.Name != null ? h.Name.ToString() : "?"; } catch { return "?"; }
        }
    }
}
