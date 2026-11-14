using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.TournamentGames;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace GrandTourney
{
    /// <summary>
    /// 491 (Jeff 14.11): rany szranek przechodza miedzy walkami i po turnieju.
    ///  - Gra stawia kazdego bohatera do kazdej walki z HP sprzed turnieju (Agent.Character -> Hero.HitPoints), zolnierzy z pelnym; nic nie zapisuje
    ///    HP z powrotem po turnieju. Tu: koniec walki -> ulamek HP; nastepna walka zaczyna sie z krzywej odpoczynku (Jeff: 10%->50%, 50%->75%,
    ///    75%->90%, 100%->100%; powalony -> 40%) plus atletyka ((Atl-100)/300 * premia), w [25%, 100%], nigdy ponizej konca i nigdy ponad HP z wejscia.
    ///  - Po turnieju bohater zostaje z HP konca ostatniej walki (powalony -> 40% wg atletyki); HP tylko obnizamy, na mapie (AppTick), nigdy w misji.
    ///  - Turnieje AI (ResolveTournament, bez misji): drabinka 4 rund losowana osobno od drabinki wykupu - zwyciezca rund traci 12-30% na walke.
    ///  - Smierc (Jeff 14.11 "AI bohaterowie nie gina"): domyslnie WYLACZONA (TourneyDeathAiHeroes NIE, TourneyDeathPlayer NIE); sciezka zostaje:
    ///    powalony z HP przed ciosem <= TourneyDeathMaxHpPct ciezkim ciosem, szansa TourneyDeathChancePct, smierc dopiero na mapie przez
    ///    Armoury.HeroFate.TryKill (StoryGuard, wladcy, ostatni z rodu, sufit roku), bez Armoury: CanDie + ApplyByWounds.
    /// Stan tylko w pamieci (nic w zapisie). Wyjatek - licznik potkniec per miejsce, nic nie gasimy. Linie "Rany szranek (491):".
    /// </summary>
    internal static class TourneyWounds
    {
        private const string P = "Rany szranek (491): ";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const float SimWear = 0.30f;      // walka bez agentow (pominieta / turniej AI): zwyciezca traci do 30% (0.4-1.0 z tego)
        private const float DeathMinBlow = 30f;   // ciezki cios: surowe obrazenia ciosu powalajacego co najmniej tyle
        private const float MinStart = 0.25f;

        private sealed class Rec
        {
            public CharacterObject C;
            public float Entry = 1f;   // ulamek HP przy wejsciu do turnieju (sufit)
            public float Start = 1f;   // ulamek HP na poczatku biezacej walki
            public float End = 1f;     // ulamek HP na koncu ostatniej walki
            public bool Ko;            // powalony w biezacej walce
            public bool EverKo;        // ostatnia walka skonczona powaleniem
            public bool Spawned;       // w biezacej walce byl agent
            public int Fights;
        }

        private sealed class Pending { public Hero H; public float Ratio; public bool Ko; }
        private sealed class Death { public Hero H, Killer; public string Town; public CampaignTime Since; public int Tries; }

        private static readonly Dictionary<int, Rec> _recs = new Dictionary<int, Rec>();
        private static readonly Dictionary<int, float> _preHit = new Dictionary<int, float>();
        private static readonly List<Pending> _apply = new List<Pending>();
        private static readonly List<Death> _deaths = new List<Death>();
        private static Mission _mis;
        private static string _misTown;
        private static int _misMatches;
        private static string _pendingLog;

        // liczniki doby
        private static int _tourneysMis, _tourneysSim, _heroesHurt, _heroesSkipped, _koRolls, _killed, _refused, _deferredDrop, _setStart;
        private static int _stumbles, _stumblesDay;
        private static bool _patched;

        // Armoury.HeroFate.TryKill (odbiciem, raz)
        private static bool _fateResolved;
        private static MethodInfo _tryKill;

        internal static bool On
        {
            get { var s = Settings.Current; return s != null && s.Enabled && s.TourneyWoundsCarry; }
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumblesDay++;
            try { Log.Error("TourneyWounds." + where, e); } catch { }
        }

        /// <summary>Nowa kampania i kazde wczytanie (konstruktor TourneyWoundsBehavior).</summary>
        internal static void Reset()
        {
            _recs.Clear(); _preHit.Clear(); _apply.Clear(); _deaths.Clear();
            _mis = null; _misTown = null; _misMatches = 0; _pendingLog = null; _stumbles = 0;
            ClearDay();
        }

        private static void ClearDay()
        {
            _tourneysMis = _tourneysSim = _heroesHurt = _heroesSkipped = _koRolls = _killed = _refused = _deferredDrop = _setStart = 0;
            _stumblesDay = 0;
        }

        // ------------------------------------------------------------ krzywa odpoczynku (Jeff 14.11)

        private static float Athletics(CharacterObject c)
        {
            try
            {
                if (c == null) return 0f;
                var s = Settings.Current;
                float bonus = s != null ? s.TourneyRecoveryAthleticsBonus : 0.15f;
                return (c.GetSkillValue(DefaultSkills.Athletics) - 100f) / 300f * bonus;
            }
            catch (Exception e) { Stumble("Athletics", e); return 0f; }
        }

        private static float Curve(float end)
        {
            // punkty Jeffa: (0 -> 0.45), (0.10 -> 0.50), (0.50 -> 0.75), (0.75 -> 0.90), (1.00 -> 1.00)
            end = MathF.Clamp(end, 0f, 1f);
            if (end <= 0.10f) return 0.45f + (end / 0.10f) * 0.05f;
            if (end <= 0.50f) return 0.50f + (end - 0.10f) / 0.40f * 0.25f;
            if (end <= 0.75f) return 0.75f + (end - 0.50f) / 0.25f * 0.15f;
            return 0.90f + (end - 0.75f) / 0.25f * 0.10f;
        }

        private static float KoStartBase()
        {
            var s = Settings.Current;
            return MathF.Clamp((s != null ? s.TourneyKnockoutStartPct : 40) / 100f, 0.01f, 1f);
        }

        /// <summary>Ulamek HP na start kolejnej walki po walce zakonczonej "end" (albo powaleniem).</summary>
        private static float NextStart(Rec r, bool ko, float end)
        {
            float st = (ko ? KoStartBase() : Curve(end)) + Athletics(r.C);
            st = MathF.Clamp(st, MinStart, 1f);
            if (st > r.Entry) st = r.Entry;          // turniej nie leczy ponad HP z wejscia
            if (!ko && st < end) st = end;           // odpoczynek nie odbiera zdrowia
            return MathF.Clamp(st, 0.01f, 1f);
        }

        /// <summary>HP po turnieju: koniec ostatniej walki; powalony - 40% wg atletyki (bez odpoczynku ponad to).</summary>
        private static float FinalRatio(Rec r)
        {
            if (r.EverKo)
            {
                float k = MathF.Clamp(KoStartBase() + Athletics(r.C), MinStart, 1f);
                return MathF.Min(k, r.Entry);
            }
            return MathF.Clamp(MathF.Min(r.End, r.Entry), 0.01f, 1f);
        }

        // ------------------------------------------------------------ latki

        internal static void ApplyAll(Harmony h)
        {
            if (_patched || h == null) return;
            _patched = true;
            int n = 0;
            try
            {
                var t = AccessTools.TypeByName("SandBox.Tournaments.MissionLogics.TournamentFightMissionController");
                var start = t != null ? AccessTools.DeclaredMethod(t, "StartMatch") : null;
                if (start != null) { h.Patch(start, postfix: new HarmonyMethod(typeof(TourneyWounds), nameof(StartMatchPostfix))); n++; }
                var hit = t != null ? AccessTools.DeclaredMethod(t, "OnScoreHit") : null;
                if (hit != null) { h.Patch(hit, postfix: new HarmonyMethod(typeof(TourneyWounds), nameof(HitPostfix))); n++; }
                var rem = t != null ? AccessTools.DeclaredMethod(t, "OnAgentRemoved") : null;
                if (rem != null) { h.Patch(rem, postfix: new HarmonyMethod(typeof(TourneyWounds), nameof(RemovedPostfix))); n++; }
                var end = AccessTools.DeclaredMethod(typeof(TournamentMatch), "End");
                if (end != null) { h.Patch(end, postfix: new HarmonyMethod(typeof(TourneyWounds), nameof(EndPostfix))); n++; }
                Log.Info(P + "latki " + n + "/4 (TournamentFightMissionController.StartMatch / OnScoreHit / OnAgentRemoved, TournamentMatch.End)"
                         + (start == null ? " - BRAK StartMatch: kazda walka z pelnym HP jak dotad" : "") + ".");
            }
            catch (Exception e) { Stumble("ApplyAll", e); }
        }

        private static Rec RecFor(TournamentParticipant p)
        {
            if (p == null || p.Character == null) return null;
            int seed = p.Descriptor.UniqueSeed;
            Rec r;
            if (_recs.TryGetValue(seed, out r)) return r;
            r = new Rec { C = p.Character };
            var hero = p.Character.HeroObject;
            if (hero != null && hero.MaxHitPoints > 0)
                r.Entry = MathF.Clamp((float)hero.HitPoints / hero.MaxHitPoints, 0.01f, 1f);
            r.Start = r.End = r.Entry;
            _recs[seed] = r;
            return r;
        }

        private static void CheckMission()
        {
            var m = Mission.Current;
            if (m != null && m != _mis)
            {
                _recs.Clear(); _preHit.Clear(); _mis = m; _misMatches = 0; _misTown = null;
                try { var st = Settlement.CurrentSettlement; _misTown = st != null ? st.Name.ToString() : null; } catch { }
            }
        }

        private static Agent AgentOf(TournamentParticipant p)
        {
            var m = Mission.Current;
            if (m == null || p == null) return null;
            int seed = p.Descriptor.UniqueSeed;
            foreach (var a in m.Agents)
                if (a != null && a.IsHuman && a.Origin != null && a.Origin.UniqueSeed == seed) return a;
            return null;
        }

        /// <summary>Po rozstawieniu agentow walki (takze po grze: Health = Hero.HitPoints gracza): HP startu z krzywej odpoczynku.
        /// Pierwsza walka - nic nie zmieniamy, tylko zapis HP wejscia.</summary>
        public static void StartMatchPostfix(TournamentMatch match)
        {
            try
            {
                if (!On || match == null || Mission.Current == null) return;
                CheckMission();
                _misMatches++;
                foreach (var p in match.Participants)
                {
                    try
                    {
                        if (p == null || p.Character == null) continue;
                        int seed = p.Descriptor.UniqueSeed;
                        bool known = _recs.ContainsKey(seed);
                        var r = RecFor(p);
                        if (r == null) continue;
                        r.Ko = false; r.Spawned = false;
                        var a = AgentOf(p);
                        if (a == null) continue;
                        r.Spawned = true;
                        if (!known)
                        {
                            // pierwsza walka: agent ma HP z gry (bohater - Hero.HitPoints, zolnierz - pelne)
                            if (a.HealthLimit > 0f) r.Entry = r.Start = r.End = MathF.Clamp(a.Health / a.HealthLimit, 0.01f, 1f);
                            continue;
                        }
                        // r.Start policzony na koncu poprzedniej walki
                        float hp = Math.Max(1f, (float)Math.Round(r.Start * a.HealthLimit));
                        if (Math.Abs(hp - a.Health) >= 1f) { a.Health = hp; _setStart++; }
                    }
                    catch (Exception e) { Stumble("StartMatch.p", e); }
                }
            }
            catch (Exception e) { Stumble("StartMatch", e); }
        }

        /// <summary>HP tuz przed ciosem (dla progu smierci): Health po ciosie + odjete.</summary>
        public static void HitPostfix(Agent affectedAgent, float damagedHp)
        {
            try
            {
                if (!On || affectedAgent == null || !affectedAgent.IsHuman) return;
                if (!DeathOn()) return;
                _preHit[affectedAgent.Index] = affectedAgent.Health + damagedHp;
            }
            catch (Exception e) { Stumble("Hit", e); }
        }

        private static bool DeathOn()
        {
            var s = Settings.Current;
            return s != null && (s.TourneyDeathAiHeroes || s.TourneyDeathPlayer) && s.TourneyDeathChancePct > 0;
        }

        /// <summary>Powalenie w walce: znacznik KO; ewentualny rzut smierci (tylko zapis - zabijamy na mapie).</summary>
        public static void RemovedPostfix(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow killingBlow)
        {
            try
            {
                if (!On || affectedAgent == null || !affectedAgent.IsHuman || affectedAgent.Origin == null) return;
                if (agentState != AgentState.Unconscious && agentState != AgentState.Killed) return;
                Rec r;
                if (!_recs.TryGetValue(affectedAgent.Origin.UniqueSeed, out r)) return;
                r.Ko = true;
                if (!DeathOn()) return;
                var c = affectedAgent.Character as CharacterObject;
                var h = c != null ? c.HeroObject : null;
                if (!Eligible(h)) return;
                float limit = affectedAgent.HealthLimit > 0f ? affectedAgent.HealthLimit : 100f;
                float pre;
                if (!_preHit.TryGetValue(affectedAgent.Index, out pre)) return;   // bez ciosu (np. wykrwawienie) - bez rzutu
                float preR = pre / limit;
                float raw = killingBlow.InflictedDamage;
                var s = Settings.Current;
                if (preR > s.TourneyDeathMaxHpPct / 100f || raw < DeathMinBlow) return;
                _koRolls++;
                float roll = MBRandom.RandomFloat;
                bool dies = roll < s.TourneyDeathChancePct / 100f;
                Hero killer = null;
                try
                {
                    var a = affectorAgent != null && affectorAgent.IsMount && affectorAgent.RiderAgent != null ? affectorAgent.RiderAgent : affectorAgent;
                    var kc = a != null && a != affectedAgent ? a.Character as CharacterObject : null;
                    killer = kc != null ? kc.HeroObject : null;
                }
                catch { }
                Log.Info(P + "rzut smierci: " + h.Name + " HP przed ciosem " + (preR * 100f).ToString("0", Inv) + "%, cios " + raw.ToString("0", Inv)
                         + ", rzut " + roll.ToString("0.000", Inv) + " < " + (s.TourneyDeathChancePct / 100f).ToString("0.000", Inv) + " -> " + (dies ? "SMIERTELNA RANA" : "przezyl"));
                if (dies) _deaths.Add(new Death { H = h, Killer = killer, Town = _misTown, Since = CampaignTime.Now });
            }
            catch (Exception e) { Stumble("Removed", e); }
        }

        private static bool Eligible(Hero h)
        {
            if (h == null || !h.IsAlive || h.IsChild) return false;
            if (h.DeathMark != KillCharacterAction.KillCharacterActionDetail.None) return false;
            if (h.Occupation == Occupation.Special || h.IsNotable) return false;
            var s = Settings.Current;
            if (h == Hero.MainHero) return s.TourneyDeathPlayer;
            if (!s.TourneyDeathAiHeroes) return false;
            return h.IsLord || h.IsWanderer || (h.Clan != null && h.Clan == Clan.PlayerClan);
        }

        /// <summary>Koniec walki (przed OnMatchEnded - agenci jeszcze stoja): ulamek konca i start nastepnej walki.</summary>
        public static void EndPostfix(TournamentMatch __instance)
        {
            try
            {
                if (!On || __instance == null || Mission.Current == null) return;
                CheckMission();
                var winners = new List<TournamentParticipant>(__instance.Winners);
                bool skipping = TourneyRansom.Skipping;
                foreach (var p in __instance.Participants)
                {
                    try
                    {
                        var r = RecFor(p);
                        if (r == null) continue;
                        bool won = winners.Contains(p);
                        bool ko; float end;
                        var a = AgentOf(p);
                        if (r.Ko) { ko = true; end = 0f; }
                        else if (a != null && a.IsActive() && a.HealthLimit > 0f && (won || !skipping))
                        { ko = false; end = MathF.Clamp(a.Health / a.HealthLimit, 0.01f, 1f); }
                        else if (won)
                        { ko = false; end = MathF.Max(0.05f, r.Start - SimWear * (0.4f + 0.6f * MBRandom.RandomFloat)); }
                        else { ko = true; end = 0f; }   // odpadl w walce pominietej / dosymulowanej
                        r.End = end; r.EverKo = ko; r.Fights++;
                        r.Start = NextStart(r, ko, end);
                        r.Ko = false; r.Spawned = false;
                    }
                    catch (Exception e) { Stumble("End.p", e); }
                }
                _preHit.Clear();
            }
            catch (Exception e) { Stumble("End", e); }
        }

        // ------------------------------------------------------------ koniec turnieju

        /// <summary>CampaignEvents.TournamentFinished. W misji - nic (AppTick po misji przenosi HP z zapisu walk); bez misji - drabinka symulowana.</summary>
        internal static void OnFinished(CharacterObject winner, MBReadOnlyList<CharacterObject> participants, Town town)
        {
            try
            {
                if (!On) return;
                if (TourneyRansom.CallingOff) return;   // turniej odwolany - nikt nie walczyl
                if (Mission.Current != null)
                {
                    if (town != null && _misTown == null) _misTown = town.Name.ToString();
                    return;
                }
                if (participants == null) return;
                _tourneysSim++;
                var sb = new StringBuilder();
                int n = 0;
                foreach (var c in participants)
                {
                    try
                    {
                        if (c == null || !c.IsHero || c.HeroObject == null) continue;
                        var h = c.HeroObject;
                        var r = new Rec { C = c };
                        if (h.MaxHitPoints > 0) r.Entry = MathF.Clamp((float)h.HitPoints / h.MaxHitPoints, 0.01f, 1f);
                        r.Start = r.End = r.Entry;
                        // 16 -> 8 -> 4 -> 2 -> 1: odpada w rundzie 1 (8 z 15), 2 (4), 3 (2), 4 (1); zwyciezca wygrywa 4
                        int wins;
                        if (c == winner) wins = 4;
                        else
                        {
                            float x = MBRandom.RandomFloat * 15f;
                            wins = x < 8f ? 0 : x < 12f ? 1 : x < 14f ? 2 : 3;
                        }
                        for (int i = 0; i < wins; i++)
                        {
                            float end = MathF.Max(0.05f, r.Start - SimWear * (0.4f + 0.6f * MBRandom.RandomFloat));
                            r.End = end; r.EverKo = false;
                            r.Start = NextStart(r, false, end);
                        }
                        if (c != winner) { r.EverKo = true; r.End = 0f; }
                        float fin = FinalRatio(r);
                        _apply.Add(new Pending { H = h, Ratio = fin, Ko = r.EverKo });
                        if (n < 20) sb.Append(" ").Append(h.Name).Append(" ").Append((fin * 100f).ToString("0", Inv)).Append("%").Append(r.EverKo ? "(KO r" + (wins + 1) + ")" : "");
                        n++;
                    }
                    catch (Exception e) { Stumble("OnFinished.c", e); }
                }
                Log.Info(P + "turniej AI " + (town != null ? town.Name.ToString() : "?") + " (symulacja, bez gracza): bohaterow " + n + ", HP po turnieju:" + sb);
            }
            catch (Exception e) { Stumble("OnFinished", e); }
        }

        /// <summary>Co klatke (po TourneyRansom.AppTick): po misji turnieju - HP z zapisu walk; kolejka HP; smierci (domyslnie wylaczone).</summary>
        internal static void AppTick()
        {
            if (_mis == null && _apply.Count == 0 && _deaths.Count == 0) return;
            try
            {
                if (Campaign.Current == null || Game.Current == null || Mission.Current != null) return;
                if (!(Game.Current.GameStateManager.ActiveState is MapState)) return;
                var cm = Campaign.Current.ConversationManager;
                if (cm != null && cm.IsConversationInProgress) return;

                if (_mis != null) FlushMission();
                if (_apply.Count > 0) ApplyHp();
                if (_deaths.Count > 0 && !TourneyRansom.Busy) Deaths();
            }
            catch (Exception e) { _apply.Clear(); Stumble("AppTick", e); }
        }

        private static void FlushMission()
        {
            var sb = new StringBuilder();
            int heroes = 0, all = _recs.Count;
            foreach (var r in _recs.Values)
            {
                if (r.C == null || !r.C.IsHero || r.C.HeroObject == null || r.Fights == 0) continue;
                float fin = FinalRatio(r);
                _apply.Add(new Pending { H = r.C.HeroObject, Ratio = fin, Ko = r.EverKo });
                heroes++;
                sb.Append(" ").Append(r.C.HeroObject.Name).Append(" ").Append((fin * 100f).ToString("0", Inv)).Append("% (walk ").Append(r.Fights)
                  .Append(r.EverKo ? ", KO" : "").Append(", wejscie ").Append((r.Entry * 100f).ToString("0", Inv)).Append("%)");
            }
            if (_recs.Count > 0)
            {
                _tourneysMis++;
                _pendingLog = P + "turniej " + (_misTown ?? "?") + ": walk " + _misMatches + ", uczestnikow z HP " + all + ", bohaterow " + heroes
                              + ", start walki ustawiony " + _setStart + " razy, HP po turnieju:" + sb + " | smierci w kolejce " + _deaths.Count;
                Log.Info(_pendingLog);
            }
            _recs.Clear(); _preHit.Clear(); _mis = null; _misTown = null; _misMatches = 0;
        }

        private static void ApplyHp()
        {
            var list = new List<Pending>(_apply);
            _apply.Clear();
            foreach (var p in list)
            {
                try
                {
                    var h = p.H;
                    if (h == null || !h.IsAlive || h.MaxHitPoints <= 0) { _heroesSkipped++; continue; }
                    int hp = Math.Max(1, (int)Math.Round(p.Ratio * h.MaxHitPoints));
                    if (hp >= h.HitPoints) { _heroesSkipped++; continue; }   // tylko obnizamy
                    int was = h.HitPoints;
                    h.HitPoints = hp;
                    _heroesHurt++;
                    if (h == Hero.MainHero)
                    {
                        Log.Player("You leave the lists battered: " + h.HitPoints + "/" + h.MaxHitPoints + " health.", true);
                        Log.Info(P + "gracz: HP " + was + " -> " + h.HitPoints + "/" + h.MaxHitPoints + (p.Ko ? " (powalony)" : ""));
                    }
                }
                catch (Exception e) { Stumble("ApplyHp.h", e); }
            }
        }

        private static void Deaths()
        {
            var list = new List<Death>(_deaths);
            _deaths.Clear();
            foreach (var d in list)
            {
                try
                {
                    var h = d.H;
                    if (h == null || !h.IsAlive) continue;
                    string why; string res = Kill(h, d.Killer, out why);
                    if (res == "Killed")
                    {
                        _killed++;
                        Log.Player(h.Name + " took a mortal blow in the lists" + (d.Town != null ? " at " + d.Town : "") + " and died of it.", true);
                        Log.Info(P + "smierc: " + h.Name + " (" + (d.Town ?? "?") + ") - " + why);
                    }
                    else if (res == "Deferred")
                    {
                        d.Tries++;
                        if ((CampaignTime.Now - d.Since).ToDays > 3.0) { _deferredDrop++; Log.Info(P + "smierc " + h.Name + " porzucona po 3 dniach odroczen - " + why); }
                        else _deaths.Add(d);
                    }
                    else { _refused++; Log.Info(P + "smierc " + h.Name + " odmowiona - " + why); }
                }
                catch (Exception e) { Stumble("Deaths.d", e); }
            }
        }

        private static string Kill(Hero h, Hero killer, out string why)
        {
            why = null;
            if (!_fateResolved)
            {
                _fateResolved = true;
                try
                {
                    var t = AccessTools.TypeByName("Armoury.HeroFate");
                    _tryKill = t != null ? AccessTools.DeclaredMethod(t, "TryKill") : null;
                    if (_tryKill != null && _tryKill.GetParameters().Length != 6) _tryKill = null;
                    Log.Info(P + "Armoury.HeroFate.TryKill " + (_tryKill != null ? "znaleziony" : "BRAK - smierc przez CanDie + ApplyByWounds"));
                }
                catch (Exception e) { _tryKill = null; Stumble("ResolveFate", e); }
            }
            if (_tryKill != null)
            {
                var args = new object[] { h, KillCharacterAction.KillCharacterActionDetail.WoundedInBattle, "tourney", killer, false, null };
                var r = _tryKill.Invoke(null, args);
                why = args[5] as string;
                return r != null ? r.ToString() : "Refused";
            }
            var party = h.PartyBelongedTo;
            if (party != null && (party.MapEvent != null || party.SiegeEvent != null)) { why = "partia w bitwie / oblezeniu"; return "Deferred"; }
            if (!h.CanDie(KillCharacterAction.KillCharacterActionDetail.WoundedInBattle)) { why = "CanDie = nie"; return "Refused"; }
            KillCharacterAction.ApplyByWounds(h, true);
            why = "ApplyByWounds";
            return h.IsAlive ? "Refused" : "Killed";
        }

        // ------------------------------------------------------------ linia doby

        internal static void Daily()
        {
            try
            {
                var s = Settings.Current;
                if (_tourneysMis + _tourneysSim + _stumblesDay > 0 || _deaths.Count > 0)
                    Log.Info(P + "dzien " + ((int)CampaignTime.Now.ToDays).ToString(Inv) + " | klucz " + (On ? "tak" : "nie")
                             + ", smierc AI " + (s != null && s.TourneyDeathAiHeroes ? "tak" : "nie") + ", gracza " + (s != null && s.TourneyDeathPlayer ? "tak" : "nie")
                             + " | turnieje w misji " + _tourneysMis + ", symulowane " + _tourneysSim + " | bohaterom obnizono HP " + _heroesHurt + ", bez zmiany " + _heroesSkipped
                             + " | rzuty smierci " + _koRolls + ", zabici " + _killed + ", odmowy " + _refused + ", porzucone " + _deferredDrop + ", czeka " + _deaths.Count
                             + " | potkniecia " + _stumblesDay + " (sesja " + _stumbles + ")");
            }
            catch (Exception e) { Stumble("Daily", e); }
            ClearDay();
        }
    }

    /// <summary>491: sluchacze ran szranek (koniec turnieju, linia doby). Nic w zapisie.</summary>
    public class TourneyWoundsBehavior : CampaignBehaviorBase
    {
        public TourneyWoundsBehavior() { TourneyWounds.Reset(); }

        public override void RegisterEvents()
        {
            CampaignEvents.TournamentFinished.AddNonSerializedListener(this, OnFinished);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, TourneyWounds.Daily);
        }

        public override void SyncData(IDataStore dataStore) { }

        private void OnFinished(CharacterObject winner, MBReadOnlyList<CharacterObject> participants, Town town, ItemObject prize)
        {
            TourneyWounds.OnFinished(winner, participants, town);
        }
    }
}
