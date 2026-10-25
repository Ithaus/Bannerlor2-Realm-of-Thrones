using System;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.ComponentInterfaces;

namespace Armoury
{
    internal static partial class ArmyReview
    {
        internal static void BattleStumble(string where, Exception e) { Stumble("Battle." + where, e); }

        private static FieldInfo _bimCached, _bimValue;
        private static bool _bimLooked;

        /// <summary>Czy gra pokazala ekran Order of Battle: tylko ODCZYT pamieci modelu gry (BattleInitializationModel._canPlayerSideDeployWithOOB, gdy policzona) -
        /// bez wolania CanPlayerSideDeployWithOrderOfBattle, ktore przy pierwszym wywolaniu liczy i zapamietuje wynik (nie zmieniamy chwili, w ktorej gra to robi).</summary>
        internal static string OobShown()
        {
            try
            {
                if (!_bimLooked)
                {
                    _bimLooked = true;
                    _bimCached = AccessTools.Field(typeof(BattleInitializationModel), "_isCanPlayerSideDeployWithOOBCached");
                    _bimValue = AccessTools.Field(typeof(BattleInitializationModel), "_canPlayerSideDeployWithOOB");
                }
                var m = MissionGameModels.Current != null ? MissionGameModels.Current.BattleInitializationModel : null;
                if (m == null || _bimCached == null || _bimValue == null) return "nieznane";
                if (!(bool)_bimCached.GetValue(m)) return "nie liczone przez gre" + (BattleInitializationModel.BypassPlayerDeployment ? " (rozstawienie pominiete)" : "");
                return (bool)_bimValue.GetValue(m) ? "TAK" : "NIE" + (BattleInitializationModel.BypassPlayerDeployment ? " (rozstawienie pominiete)" : " (gracz nie dowodzi, < 20 ludzi albo wypad)");
            }
            catch (Exception e) { BattleStumble("OobShown", e); return "blad odczytu"; }
        }
    }

    /// <summary>
    /// noc 25.10 P4: linia kontrolna przegladu armii w bitwie polowej gracza - raz na bitwe, tylko odczyt. Przy starcie misji (OnBehaviorInitialize) kopia zapisu
    /// Order of Battle gry (8 wpisow: klasa, waga, kapitan) - zanim gra nadpisze go po rozstawieniu (SaveConfiguration); po rozstawieniu (OnDeploymentFinished,
    /// zapas: 10 s bitwy poza trybem rozstawienia) formacje 0..7 druzyny gracza: ludzi, rodzaje wedlug wzorca, z koniem, jezdzcy pieszo, kapitan - wobec kopii.
    /// Dodawane w SubModuleMain.OnMissionBehaviorInitialize przy ArmyReviewEnabled i kampanii; poza bitwa polowa gracza nic nie pisze.
    /// P12 (przypiecia typow, ArmyReview.Pin.cs): OnDeploymentFinished najpierw PinDeploy (przypieci przeniesieni przez ekran wracaja do swojej choragwi -
    /// to zachowanie dodane PO BannerCompaniesMission, wiec Mission.OnDeploymentFinished, ktore idzie od konca listy, wola je PRZED podzialem na kompanie
    /// i przed widokiem Order of Battle), linia bitwy ma segment "przypiecia"; OnRemoveBehavior - linia posilkow i zwolnienie planu misji.
    /// </summary>
    internal sealed class ArmyReviewCheck : MissionLogic
    {
        private const string P = "Przeglad armii (P4): ";
        private bool _done, _army;
        private float _t;
        private int _entries;
        private readonly int[] _cls = new int[ArmyReview.Slots];
        private readonly int[] _w = new int[ArmyReview.Slots];
        private readonly Hero[] _cap = new Hero[ArmyReview.Slots];

        public override void OnBehaviorInitialize()
        {
            base.OnBehaviorInitialize();
            try
            {
                var mp = MobileParty.MainParty;
                if (Campaign.Current == null || MapEvent.PlayerMapEvent == null || mp == null) { _done = true; return; }
                _army = mp.Army != null;
                var b = ArmyReview.Behavior();
                if (b == null) return;
                for (int i = 0; i < ArmyReview.Slots; i++)
                {
                    var d = b.GetFormationDataAtIndex(i, false, _army);
                    if (d == null) continue;
                    _entries++;
                    _cls[i] = (int)d.FormationClass;
                    _w[i] = d.PrimaryClassWeight;
                    _cap[i] = d.Captain;
                }
            }
            catch (Exception e) { ArmyReview.BattleStumble("Init", e); _done = true; }
        }

        public override void OnDeploymentFinished()
        {
            base.OnDeploymentFinished();
            if (_done) return;
            try { ArmyReview.PinDeploy(Mission); } catch (Exception e) { ArmyReview.BattleStumble("PinDeploy", e); }   // P12: przed linia i przed kompaniami P3
            try { Check("po rozstawieniu"); }
            catch (Exception e) { _done = true; ArmyReview.BattleStumble("Deployment", e); }
        }

        public override void OnRemoveBehavior()
        {
            try { ArmyReview.PinMissionEnd(Mission); } catch (Exception e) { ArmyReview.BattleStumble("PinEnd", e); }   // P12: posilki przypietych, plan zwolniony
            base.OnRemoveBehavior();
        }

        public override void OnMissionTick(float dt)
        {
            if (_done) return;
            try
            {
                var m = Mission;
                if (m == null || m.Mode == MissionMode.Deployment) return;
                _t += dt;
                if (_t < 10f) return;
                Check("bez konca rozstawienia (10 s bitwy)");
            }
            catch (Exception e) { _done = true; ArmyReview.BattleStumble("Tick", e); }
        }

        private void Check(string when)
        {
            _done = true;
            var m = Mission;
            if (m == null || !m.IsFieldBattle || HideoutAlarm.IsHideout(m)) return;   // tylko bitwa polowa (oblezen i morza zapis nie dotyczy)
            var team = m.PlayerTeam;
            if (team == null) return;
            int n = ArmyReview.Slots;
            var men = new int[n]; var mounted = new int[n]; var kind = new int[n, 4]; var pinned = new int[n];
            int onFoot = 0, outside = 0;
            foreach (var a in team.ActiveAgents)
            {
                if (a == null || !a.IsHuman) continue;
                var f = a.Formation;
                var ch = a.Character;
                if (f == null || ch == null || f.Index < 0 || f.Index >= n) { outside++; continue; }
                int i = f.Index;
                men[i]++;
                if (a.HasMount) mounted[i]++;
                int k = ch.IsMounted ? (ch.IsRanged ? 3 : 2) : (ch.IsRanged ? 1 : 0);
                if (ArmyReview.PinnedAgent(m, a)) pinned[i]++;   // P12: przypieci poza zgodnoscia klasy choragwi (stoja ponad jej udzialem)
                else kind[i, k]++;
                if (ch.IsMounted && !a.HasMount) onFoot++;
            }
            var sb = new StringBuilder(P);
            sb.Append("bitwa (").Append(when).Append(") - zapis ").Append(_army ? "armii " : "pola ")
              .Append(_entries == ArmyReview.Slots ? "8 wpisow" : _entries == 0 ? "brak (porzadek gry)" : _entries + " wpisow (spawn gry uzywa tylko 8)")
              .Append(" | gracz dowodzi ").Append(team.IsPlayerGeneral ? "TAK" : "NIE").Append(", ekran Order of Battle ").Append(ArmyReview.OobShown())
              .Append(" | formacje gracza:");
            bool classesOk = true, capsOk = true;
            int classChecks = 0, capChecks = 0;
            var bad = new StringBuilder();
            for (int i = 0; i < n; i++)
            {
                var f = i < team.FormationsIncludingEmpty.Count ? team.FormationsIncludingEmpty[i] : null;
                var capA = f != null ? f.Captain : null;
                if (men[i] == 0 && _cls[i] == 0 && capA == null) continue;
                int major = -1, best = -1;
                for (int k = 0; k < 4; k++) if (kind[i, k] > best) { best = kind[i, k]; major = k; }
                sb.Append(' ').Append(i).Append(' ').Append(men[i]).Append(" (p").Append(kind[i, 0]).Append(" l").Append(kind[i, 1]).Append(" k").Append(kind[i, 2])
                  .Append(" kl").Append(kind[i, 3]).Append(pinned[i] > 0 ? ", przypietych " + pinned[i] : "").Append(", z koniem ").Append(mounted[i]).Append(')');
                if (capA != null && capA.Character != null) sb.Append(" kap. ").Append(capA.Character.Name);
                string nm = ArmyReview.NameOf(i);
                if (nm != null) sb.Append(" '").Append(nm).Append('\'');
                if (_entries == ArmyReview.Slots)
                {
                    sb.Append(" [zapis ").Append(ArmyReview.KindName(_cls[i])).Append(" w").Append(_w[i]);
                    if (_cap[i] != null) sb.Append(" kap. ").Append(_cap[i].Name);
                    sb.Append(']');
                    if (men[i] - pinned[i] > 0)
                    {
                        classChecks++;
                        int want = major + 1;
                        bool ok = _cls[i] == want || (_cls[i] == 5 && (want == 1 || want == 2)) || (_cls[i] == 6 && (want == 3 || want == 4));
                        if (!ok) { classesOk = false; bad.Append(' ').Append(i).Append(':').Append(ArmyReview.KindName(want)).Append("!=").Append(ArmyReview.KindName(_cls[i])); }
                    }
                    // kapitan z zapisu sprawdzany tylko, gdy stoi w tej bitwie (ranny albo nieobecny - gra go pomija)
                    var want0 = _cap[i];
                    if (want0 != null && want0.IsAlive && InMission(m, want0))
                    {
                        capChecks++;
                        if (capA == null || capA.Character != want0.CharacterObject) { capsOk = false; bad.Append(' ').Append(i).Append(":kapitan"); }
                    }
                }
                sb.Append(';');
            }
            sb.Append(" poza formacjami 0-7 ").Append(outside).Append(" | jezdzcy pieszo ").Append(onFoot);
            if (_entries == ArmyReview.Slots)
                sb.Append(" | zgodnosc z zapisem: klasy ").Append(classChecks == 0 ? "-" : classesOk ? "TAK" : "NIE").Append(" (").Append(classChecks).Append(')')
                  .Append(", kapitanowie ").Append(capChecks == 0 ? "-" : capsOk ? "TAK" : "NIE").Append(" (").Append(capChecks).Append(')')
                  .Append(bad.Length > 0 ? " -" + bad : "");
            else sb.Append(" | zgodnosc z zapisem: - (brak pelnego zapisu)");
            try { sb.Append(ArmyReview.PinBattleSegment(m, team)); } catch (Exception e) { ArmyReview.BattleStumble("PinSegment", e); }   // P12
            sb.Append('.');
            Log.Info(sb.ToString());
        }

        private static bool InMission(Mission m, Hero h)
        {
            try
            {
                var co = h.CharacterObject;
                foreach (var a in m.Agents) if (a != null && a.IsHuman && a.Character == co) return true;
            }
            catch { }
            return false;
        }
    }
}
