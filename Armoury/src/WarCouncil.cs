using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// NB2 czesc 2 (16.10, Jeff: "zachowanie AI wedlug dowodcy i tieru"): RADA WOJENNA - zestaw taktyk druzyny wedlug umiejetnosci Tactics dowodcy
    /// strony (klucz CommanderTactics). Rozpoznanie rozp_ai (16.10) krok 2, tor M.
    ///
    /// Dowod [K]: gra bez modow (MissionCombatantsLogic.cs:145-180) bierze max GetTacticsSkillAmount strony: ponizej 20 tylko Charge, od 20
    /// FullScaleAttack i obrona, od 50 FrontalCavalryCharge / DefensiveRing / HoldChokePoint / CoordinatedRetreat. RBM (RBMAI Tactics.cs:158-374,
    /// postfiks MissionCombatantsLogic.EarlyStart) czysci te liste (ClearTacticOptions) i daje staly zestaw wedlug kultury dowodcy - umiejetnosc
    /// Tactics nie ma pod RBM zadnego wplywu, a ok. 28 kultur ROT ma tylko FullScaleAttack i linie obronna; FrontalCavalryCharge nie ma nigdy.
    ///
    /// Zasady (AfterStart naszego zachowania - po EarlyStart wszystkich, w tym postfiksie RBM, przed ustawieniem AI w rozstawieniu, ktore gra robi
    /// w pierwszym ticku DeploymentMissionController; bez latki Harmony):
    ///  - tylko bitwa polowa (Mission.IsFieldBattle - morska, oblezenie, wypad, kryjowka maja swoje), druzyna z TeamAIGeneral i wazna (MissionTeams);
    ///  - druzyna GRACZA bez zmian (jej AI to rozkazy Jeffa / tryb sierzanta) - sojusznicy AI tak;
    ///  - tac = max GetTacticsSkillAmount strony (jak gra);
    ///  - tac &lt; 30: tlum bez manewru - lista od nowa: Charge + FullScaleAttack (bandyci, partie bez bohatera);
    ///  - 30..59: zestaw RBM bez zmian;
    ///  - od 60: + FrontalCavalryCharge (RBM ja obsluguje, waga x0.75), obronca + DefensiveRing i HoldChokePoint;
    ///  - od 150 i strona od 150 ludzi (poprawka 16.10, dawniej 120 / 60; MapEvent gracza; bitwa wlasna - bez limitu): okrazenie piechota RBM (RBMTacticAttackSplitInfantry /
    ///    RBMTacticDefendSplitInfantry, typy z RBMAI.dll szukane po nazwie, brak typu = pominiete);
    ///  - bez dubli typow; kazde dodanie we wlasnym try (potkniecie liczone, funkcja dalej dziala).
    /// Lista taktyk (TeamAIComponent._availableTactics) czytana tylko na glownym watku w AfterStart - gra iteruje ja w MakeDecision z ticku druzyn,
    /// ktory przy ticku asynchronicznym konczy sie przed OnPreMissionTick (Mission.cs:3532), wiec bez wyscigu.
    /// noc 25.10 P1 (M2 doktryny kultur, Doctrine.cs): na koncu One - Doctrine.ApplyList (mnozniki wag i dodatki listy wedlug kultury strony; ta sama kultura
    /// i ten sam Tactics co tutaj). Rada biegnie takze przy CommanderTactics NIE, gdy wlaczona doktryna (CultureDoctrines / ShootBeforeAttacking) - wtedy
    /// lista wedlug dowodcy bez zmian (jak przy kluczu NIE), tylko doktryna. Druzyny DynamicReinforcements (powstaja w trakcie bitwy) - LateTeam z Doctrine.Tick,
    /// z Tactics i kultura strony zapamietanymi w Council.
    /// </summary>
    internal static class WarCouncil
    {
        // okrazenie: poprawka po recenzji (16.10) - prog 150 Tactics i strona od 150 ludzi (dawniej 120 / 60): RBM daje te taktyke tylko
        // kulturom sturgia / nord, a przy 120 / 60 dzielilaby piechote w wiekszosci wiekszych bitew ROT z lordem
        internal const int HordeBelow = 30, CavalryFrom = 60, EnvelopFrom = 150, EnvelopMinMen = 150;

        // ---- liczniki misji (pod RoutHold.Gate; zerowane w RoutHold.Ensure)
        private static int _teams, _horde, _cavalry, _envelop, _same, _player, _notField, _noData, _added, _dupes, _noType;
        private static string _sampleA, _sampleD;

        private static FieldInfo _fCombatants, _fTactics;
        private static bool _fieldsDone;
        private static Type _tSplitAtt, _tSplitDef;
        private static bool _typesDone;
        // noc 25.10 P1: Tactics i kultura stron z Council (dla poznych druzyn DR); misja, dla ktorej sa wazne
        private static int _tacA, _tacD;
        private static string _culA, _culD;
        private static WeakReference _sideOf;

        internal static void ResetCounters()
        {
            _teams = _horde = _cavalry = _envelop = _same = _player = _notField = _noData = _added = _dupes = _noType = 0;
            _sampleA = _sampleD = null;
        }

        internal static int Activity { get { return _teams + _player + _notField + _noData; } }

        internal static void Append(StringBuilder sb, Settings c)
        {
            sb.Append(" | taktyka wg dowodcy ").Append(c != null && c.CommanderTactics ? "TAK" : "NIE").Append(": druzyn ").Append(_teams)
              .Append(" (tlum ").Append(_horde).Append(", konnica i obrona ").Append(_cavalry).Append(", okrazenie ").Append(_envelop)
              .Append(", bez zmian ").Append(_same).Append(", gracza pominieto ").Append(_player).Append(", nie pole bitwy ").Append(_notField)
              .Append(", bez danych dowodcy ").Append(_noData).Append("), taktyk dodanych ").Append(_added).Append(" (duble ").Append(_dupes)
              .Append(", brak typu RBM ").Append(_noType).Append("); probka atak ").Append(_sampleA ?? "-").Append(", obrona ").Append(_sampleD ?? "-");
        }

        private static void Fields()
        {
            if (_fieldsDone) return;
            _fieldsDone = true;
            try { _fCombatants = AccessTools.Field(typeof(MissionCombatantsLogic), "BattleCombatants"); } catch { _fCombatants = null; }
            try { _fTactics = AccessTools.Field(typeof(TeamAIComponent), "_availableTactics"); } catch { _fTactics = null; }
        }

        private static void Types()
        {
            if (_typesDone) return;
            _typesDone = true;
            try { _tSplitAtt = AccessTools.TypeByName("RBMTacticAttackSplitInfantry"); } catch { _tSplitAtt = null; }
            try { _tSplitDef = AccessTools.TypeByName("RBMTacticDefendSplitInfantry"); } catch { _tSplitDef = null; }
            if (_tSplitAtt != null && !typeof(TacticComponent).IsAssignableFrom(_tSplitAtt)) _tSplitAtt = null;
            if (_tSplitDef != null && !typeof(TacticComponent).IsAssignableFrom(_tSplitDef)) _tSplitDef = null;
        }

        /// <summary>AfterStart (BattleSense, glowny watek, raz na misje; wolajacy sprawdzil bitwe na mapie).</summary>
        internal static void Council(Mission m)
        {
            var c = Settings.Current;
            _sideOf = null;
            if (c == null || m == null || (!c.CommanderTactics && !DoctrineWanted(c))) return;
            if (!m.IsFieldBattle) { if (c.CommanderTactics) lock (RoutHold.Gate) { RoutHold.EnsureLocked(m); _notField++; } return; }
            Fields();
            Types();
            var logic = m.GetMissionBehavior<MissionCombatantsLogic>();
            IEnumerable<IBattleCombatant> all = null;
            try { if (logic != null && _fCombatants != null) all = _fCombatants.GetValue(logic) as IEnumerable<IBattleCombatant>; } catch { all = null; }
            if (all == null || _fTactics == null) { lock (RoutHold.Gate) { RoutHold.EnsureLocked(m); _noData++; } return; }

            int tacA = 0, tacD = 0;
            string culA = null, culD = null;
            foreach (var bc in all)
            {
                if (bc == null) continue;
                int t = 0;
                try { t = bc.GetTacticsSkillAmount(); } catch { t = 0; }
                string cu = null;
                try { cu = bc.BasicCulture != null ? bc.BasicCulture.StringId : null; } catch { cu = null; }
                if (bc.Side == BattleSideEnum.Attacker) { if (t >= tacA) { tacA = t; if (cu != null) culA = cu; } }
                else if (bc.Side == BattleSideEnum.Defender) { if (t >= tacD) { tacD = t; if (cu != null) culD = cu; } }
            }
            _tacA = tacA; _tacD = tacD; _culA = culA; _culD = culD;
            _sideOf = new WeakReference(m);

            var teams = m.Teams;
            int n = teams != null ? teams.Count : 0;
            for (int i = 0; i < n; i++)
            {
                Team team = null;
                try { team = teams[i]; } catch { team = null; }
                if (team == null) continue;
                try { One(m, team, team.Side == BattleSideEnum.Attacker ? tacA : tacD, team.Side == BattleSideEnum.Attacker ? culA : culD, false); }
                catch (Exception e) { RoutHold.Stumble(RoutHold.SiteTactics, "WarCouncil.Druzyna", e); }
            }
        }

        private static bool DoctrineWanted(Settings c) { return c != null && (c.CultureDoctrines || c.ShootBeforeAttacking); }

        /// <summary>noc 25.10 P1: druzyna, ktora pojawila sie po AfterStart (DynamicReinforcements - TeamAIGeneral z kopia taktyk) - ta sama rada i doktryna,
        /// Tactics i kultura jej strony z Council tej misji. Glowny watek (Doctrine.Tick). Brak danych Council (inna misja, klucze NIE) - nic.</summary>
        internal static void LateTeam(Mission m, Team team)
        {
            var w = _sideOf;
            if (m == null || team == null || w == null || !ReferenceEquals(w.Target, m) || _fTactics == null) return;
            bool att = team.Side == BattleSideEnum.Attacker;
            try { One(m, team, att ? _tacA : _tacD, att ? _culA : _culD, true); }
            catch (Exception e) { RoutHold.Stumble(RoutHold.SiteTactics, "WarCouncil.PoznaDruzyna", e); }
        }

        private static void One(Mission m, Team team, int tac, string culture, bool late)
        {
            if (!MissionTeams.Valid(team) || !team.HasTeamAi) return;
            if (team.Side != BattleSideEnum.Attacker && team.Side != BattleSideEnum.Defender) return;
            var ai = team.TeamAI;
            if (!(ai is TeamAIGeneral)) return;
            var cs = Settings.Current;
            bool council = cs != null && cs.CommanderTactics;
            if (team.IsPlayerTeam) { if (council) lock (RoutHold.Gate) { RoutHold.EnsureLocked(m); _player++; } return; }
            var list = _fTactics.GetValue(ai) as List<TacticComponent>;
            if (list == null) { if (council) lock (RoutHold.Gate) { RoutHold.EnsureLocked(m); _noData++; } return; }
            var have = new List<Type>(list.Count + 4);
            var old = new List<TacticComponent>(list.Count);
            for (int i = 0; i < list.Count; i++) { var t = list[i]; if (t != null) { have.Add(t.GetType()); old.Add(t); } }
            bool att = team.Side == BattleSideEnum.Attacker;
            var note = new StringBuilder(80);
            int added = 0, dupes = 0, noType = 0;
            int kind;   // 0 bez zmian, 1 tlum, 2 konnica i obrona, 3 okrazenie

            if (!council) { kind = -1; }
            else if (tac < HordeBelow)
            {
                kind = 1;
                bool wasCurrent = false;
                for (int i = 0; i < old.Count; i++) { try { if (ai.IsCurrentTactic(old[i])) wasCurrent = true; } catch { } }
                team.ClearTacticOptions();
                have.Clear();
                note.Append("tlum");
                if (Add(team, have, typeof(TacticCharge), note)) added++;
                if (Add(team, have, typeof(TacticFullScaleAttack), note)) added++;
                // CoordinatedRetreat zostaje (ta sama instancja): RBM w prefiksie jej GetTacticWeight rozbija cala strone ponizej 10% sily
                // (Tactics.cs:376-437) - waga i tak 0, wybrana nie bedzie
                if (added > 0)
                    for (int i = 0; i < old.Count; i++)
                        if (old[i] is TacticCoordinatedRetreat && !Has(have, typeof(TacticCoordinatedRetreat)))
                        {
                            try { team.AddTacticOption(old[i]); have.Add(typeof(TacticCoordinatedRetreat)); note.Append("+CoordinatedRetreat(RBM)"); }
                            catch (Exception e) { RoutHold.Stumble(RoutHold.SiteTactics, "WarCouncil.Odwrot", e); }
                        }
                if (added == 0)
                {
                    // zadna nowa nie weszla - pusta lista wywrocilaby MakeDecision gry (MaxBy po pustej liscie): stara lista wraca w calosci
                    for (int i = 0; i < old.Count; i++) { try { team.AddTacticOption(old[i]); } catch { } }
                    note.Append("+stara_lista");
                    wasCurrent = false;
                }
                // zdjeta taktyka byla juz biezaca (ludzie wystawieni przed AfterStart) - wybor od nowa z nowej listy
                if (wasCurrent) { try { ai.ResetTactic(false); } catch (Exception e) { RoutHold.Stumble(RoutHold.SiteTactics, "WarCouncil.ResetTactic", e); } }
            }
            else if (tac >= CavalryFrom)
            {
                kind = 2;
                int before = added;
                if (Has(have, typeof(TacticFrontalCavalryCharge))) dupes++; else if (Add(team, have, typeof(TacticFrontalCavalryCharge), note)) added++;
                if (!att)
                {
                    if (Has(have, typeof(TacticDefensiveRing))) dupes++; else if (Add(team, have, typeof(TacticDefensiveRing), note)) added++;
                    if (Has(have, typeof(TacticHoldChokePoint))) dupes++; else if (Add(team, have, typeof(TacticHoldChokePoint), note)) added++;
                }
                if (tac >= EnvelopFrom && SideMen(team) >= EnvelopMinMen)
                {
                    var ty = att ? _tSplitAtt : _tSplitDef;
                    if (ty == null) noType++;
                    else if (Has(have, ty)) dupes++;
                    else if (Add(team, have, ty, note)) { added++; kind = 3; }
                }
                if (added == before && note.Length == 0) note.Append("bez_zmian");
            }
            else { kind = 0; note.Append("bez_zmian"); }

            // noc 25.10 P1 (M2): doktryna kultury strony - mnozniki wag i dodatki listy (we wlasnym try - rada zostaje, gdy doktryna sie potknie)
            if (DoctrineWanted(cs))
            {
                try { Doctrine.ApplyList(m, team, tac, culture, have, note, late); }
                catch (Exception e) { RoutHold.Stumble(RoutHold.SiteDoctrine, "Doctrine.ApplyList", e); }
            }
            if (!council) return;

            string sample = "T" + tac + "/" + (culture ?? "?") + "/" + note.ToString() + (late ? "/DR" : "");
            lock (RoutHold.Gate)
            {
                RoutHold.EnsureLocked(m);
                _teams++;
                if (kind == 1) _horde++;
                else if (kind == 2) _cavalry++;
                else if (kind == 3) { _cavalry++; _envelop++; }
                else _same++;
                _added += added;
                _dupes += dupes;
                _noType += noType;
                if (att) { if (_sampleA == null) _sampleA = sample; }
                else if (_sampleD == null) _sampleD = sample;
            }
        }

        private static bool Has(List<Type> have, Type t)
        {
            for (int i = 0; i < have.Count; i++) if (have[i] == t) return true;
            return false;
        }

        /// <summary>noc 25.10 P1: dodanie taktyki dla doktryny (Doctrine.ApplyList) - ta sama droga co rada (bez dubli, wyjatek liczony).</summary>
        internal static bool TryAdd(Team team, List<Type> have, Type t, StringBuilder note)
        {
            if (team == null || have == null || note == null || Has(have, t)) return false;
            return Add(team, have, t, note);
        }

        /// <summary>Nowa taktyka typu t dla druzyny (publiczny konstruktor (Team) - jak gra i RBM); wyjatek liczony, reszta listy zostaje.</summary>
        private static bool Add(Team team, List<Type> have, Type t, StringBuilder note)
        {
            if (t == null || Has(have, t)) return false;
            try
            {
                var tc = Activator.CreateInstance(t, new object[] { team }) as TacticComponent;
                if (tc == null) return false;
                team.AddTacticOption(tc);
                have.Add(t);
                note.Append('+').Append(t.Name.StartsWith("Tactic") ? t.Name.Substring(6) : t.Name);
                return true;
            }
            catch (Exception e)
            {
                RoutHold.Stumble(RoutHold.SiteTactics, "WarCouncil.Dodaj " + t.Name, e);
                return false;
            }
        }

        /// <summary>Ludzi strony wedlug MapEvent gracza (TroopCount); bitwa wlasna albo brak danych = bez limitu.</summary>
        private static int SideMen(Team team)
        {
            try
            {
                if (Campaign.Current == null) return int.MaxValue;
                var me = TaleWorlds.CampaignSystem.MapEvents.MapEvent.PlayerMapEvent;
                if (me == null) return int.MaxValue;
                var side = me.GetMapEventSide(team.Side);
                return side != null ? side.TroopCount : int.MaxValue;
            }
            catch { return int.MaxValue; }
        }
    }
}
