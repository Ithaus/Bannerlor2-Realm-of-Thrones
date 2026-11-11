using System;
using System.Globalization;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 378 (Jeff 09.11 "i powinien byc tez czas odliczany ze jak nie zdobede w iles minut to tez wycofanie np 30 minut"). LIMIT CZASU SZTURMU - GRACZ ATAKUJE.
    /// Faza 2 zalamania 371 (gracz szturmuje) nie istnieje, wiec tu TYLKO komunikaty - bez przymusowego odwrotu (o odstapieniu decyduje gracz, TAB / odwrot gry):
    ///  - ok. 5 min przed limitem (gdy limit &gt; 5 min i jeszcze nie minal): "The assault is dragging on - your men tire. (5 minutes left before the time limit)";
    ///  - przy uplywie: "Time is up - the assault has stalled. You may fall back." + linia logu 'Szturm (378): limit czasu (gracz atakuje)'.
    /// Czas walki = Mission.CurrentTime od konca rozstawienia (jak SiegeBreak371) + czas poprzednich misji TEJ SAMEJ bitwy (Battle continues; pamiec statyczna
    /// jednej bitwy - inna bitwa zeruje). Gracz-obronca: limit robi SiegeBreak371 (odwrot AI). Wyjatek = potkniecie (log 3 pierwszych), bez globalnego wylacznika.
    /// </summary>
    internal sealed class SiegeClock378 : MissionLogic
    {
        private const float NoDeployStart = 10f, WarnBefore = 300f;
        private const int ErrMax = 3;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // czas walki poprzednich misji tej samej bitwy (gracz atakuje)
        private static MapEvent _lastMe;
        private static float _lastFought;

        private bool _known, _active, _started, _warned, _up, _ended, _by371;   // _by371 (387): szturm gracza lamie SiegeBreak371 - tu tylko ostrzezenie 5 min
        private float _startAt, _tPrior, _endAt = -1f;
        private int _aCas, _dCas, _stumbles, _errs;
        private MapEvent _me;

        public override MissionBehaviorType BehaviorType { get { return MissionBehaviorType.Other; } }

        private static string F(double v, string f) { return v.ToString(f, Inv); }

        private bool Known(Mission m)
        {
            if (_known) return _active;
            _known = true;
            try
            {
                if (m == null || !m.IsSiegeBattle || SiegeWalls.TimeLimitSeconds <= 0f) return _active = false;
                var pt = m.PlayerTeam;
                if (pt == null || pt.Side != BattleSideEnum.Attacker) return _active = false;   // gracz broni - SiegeBreak371
                _me = MapEvent.PlayerMapEvent;
                if (_me == null || !_me.IsSiegeAssault || _me.IsNavalMapEvent) return _active = false;
                _tPrior = ReferenceEquals(_me, _lastMe) ? Math.Max(0f, _lastFought) : 0f;
                _active = true;
            }
            catch (Exception e) { Stumble("Known", e); _active = false; }
            return _active;
        }

        public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)
        {
            if (!_active || _ended) return;
            try
            {
                if (affectedAgent == null || !affectedAgent.IsHuman) return;
                if (agentState != AgentState.Killed && agentState != AgentState.Unconscious) return;
                var t = affectedAgent.Team;
                if (t == null) return;
                if (t.Side == BattleSideEnum.Attacker) _aCas++;
                else if (t.Side == BattleSideEnum.Defender) _dCas++;
            }
            catch (Exception e) { Stumble("Removed", e); }
        }

        public override void OnMissionTick(float dt)
        {
            if (_ended) return;
            var m = Mission;
            if (m == null) return;
            try
            {
                if (!Known(m)) { _ended = true; return; }
                float now = m.CurrentTime;
                if (!_started)
                {
                    bool deployed = m.IsDeploymentFinished || (m.Mode != MissionMode.Deployment && now >= NoDeployStart);
                    if (!deployed) return;
                    _started = true;
                    _startAt = now;
                    _warned = _tPrior >= SiegeWalls.TimeLimitSeconds - WarnBefore;   // kolejna misja tej bitwy juz po progu ostrzezenia - bez powtorki
                    _up = SiegeWalls.TimeLimitSeconds > 0f && _tPrior >= SiegeWalls.TimeLimitSeconds;   // recenzja 378: limit minal w poprzedniej misji tej bitwy - "Time is up" raz na bitwe, nie co misje
                    // 387: SiegeBreak371 dodawany przed nami (SubModuleMain) - jego Known juz po pierwszym ticku; gdy lamie szturm gracza, odwrot z limitu robi on
                    try { var b = m.GetMissionBehavior<SiegeBreak371>(); _by371 = b != null && b.PlayerMode; } catch (Exception e) { Stumble("By371", e); _by371 = false; }
                    Log.Info("Szturm (378): start misji (gracz atakuje) | osada " + Settlement() + " | limit czasu " + F(SiegeWalls.TimeLimitSeconds / 60.0, "0") + " min, z poprzednich misji tej bitwy "
                             + F(_tPrior, "0") + " s - " + (_by371 ? "ostrzezenie 5 min tutaj, odwrot z limitu robi 371 (387: szturm gracza)." : "tylko komunikaty, bez przymusowego odwrotu."));
                    return;
                }
                if (m.MissionEnded) { if (_endAt < 0f) _endAt = now; return; }
                if (_up) return;
                float tl = SiegeWalls.TimeLimitSeconds, fought = _tPrior + (now - _startAt);
                if (tl <= 0f) return;                                             // klucz wylaczony w trakcie misji
                if (!_warned && tl > WarnBefore && fought >= tl - WarnBefore)
                {
                    _warned = true;
                    Show("The assault is dragging on - your men tire. (5 minutes left before the time limit)", 0xFFE0B040u);
                    Log.Info("Szturm (378): ostrzezenie (gracz atakuje) - 5 min do limitu | czas walki " + F(fought, "0") + " s / limit " + F(tl, "0") + " s | straty A " + _aCas + " / O " + _dCas + " (ta misja).");
                }
                if (fought >= tl)
                {
                    _up = true;
                    _warned = true;
                    if (_by371) { Log.Info("Szturm (378): limit czasu (gracz atakuje) - bez komunikatu, odwrot robi 371 (387) | czas walki " + F(fought, "0") + " s / limit " + F(tl, "0") + " s."); return; }
                    Show("Time is up - the assault has stalled. You may fall back.", 0xFFE07040u);
                    int dNow = 0, rem = 0;
                    try
                    {
                        foreach (var t in m.Teams)
                        {
                            if (t == null || t.Side != BattleSideEnum.Defender) continue;
                            foreach (var a in t.ActiveAgents) if (a != null && a.IsHuman) dNow++;
                        }
                        var sp = m.GetMissionBehavior<DefaultBattleMissionAgentSpawnLogic>();
                        if (sp != null) rem = Math.Max(0, sp.NumberOfRemainingDefenderTroops);
                    }
                    catch (Exception e) { Stumble("Defenders", e); }
                    Log.Info("Szturm (378): limit czasu (gracz atakuje) - tylko komunikat, bez odwrotu | osada " + Settlement() + " | czas walki " + F(fought, "0") + " s (ta misja "
                             + F(now - _startAt, "0") + " s, wczesniej " + F(_tPrior, "0") + " s) / limit " + F(tl, "0") + " s | straty A " + _aCas + " / O " + _dCas + " (ta misja), wymiana "
                             + F(_aCas / (double)Math.Max(1, _dCas), "0.00") + ":1 | obroncow zostalo " + (dNow + rem) + " (w polu " + dNow + ", w odwodzie " + rem + ").");
                }
            }
            catch (Exception e) { Stumble("Tick", e); }
        }

        private string Settlement()
        {
            try { var st = _me != null ? _me.MapEventSettlement : null; return st != null ? st.StringId : "-"; } catch { return "-"; }
        }

        private static void Show(string text, uint argb)
        {
            try { InformationManager.DisplayMessage(new InformationMessage(text, Color.FromUint(argb))); } catch { }
        }

        protected override void OnEndMission()
        {
            try
            {
                if (_active && _started && _me != null)
                {
                    float endAt = _endAt >= 0f ? _endAt : (Mission != null ? Mission.CurrentTime : _startAt);
                    _lastMe = _me;
                    _lastFought = _tPrior + Math.Max(0f, endAt - _startAt);
                    Log.Info("Szturm (378): koniec misji (gracz atakuje) | osada " + Settlement() + " | czas walki " + F(_lastFought, "0") + " s / limit " + F(SiegeWalls.TimeLimitSeconds, "0")
                             + " s | ostrzezenie " + (_warned ? "tak" : "nie") + ", limit minal " + (_up ? "tak" : "nie") + " | straty A " + _aCas + " / O " + _dCas + " (ta misja) | potkniecia " + _stumbles + ".");
                }
            }
            catch (Exception e) { Stumble("Koniec", e); }
            _ended = true;
            base.OnEndMission();
        }

        private void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_errs >= ErrMax) return;
            _errs++;
            try { Log.Info("Szturm (378): potkniecie " + where + " - " + e.GetType().Name + ": " + e.Message); } catch { }
        }
    }
}
