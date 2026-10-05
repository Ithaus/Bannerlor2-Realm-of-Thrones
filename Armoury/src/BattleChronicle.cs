using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// KRONIKA BITEW (Jeff 04.10: "a byly wojny, jakies straty?" -> "tak" na kronike). Po kazdej bitwie (MapEventEnded):
    /// kto z kim, gdzie, rodzaj, sily na starcie, zabici / ranni / rozbici kazdej strony i ich odsetek, zloto zdobyte i
    /// stracone, zwyciezca. Szczegoly do Logs/<sesja>/bitwy.log, raz dziennie podsumowanie w glownym logu: ile bitew,
    /// ilu zabitych, sredni odsetek strat zwyciezcow i przegranych - do porownania z historia (docs/HISTORIA-ZBROJE-STRATY.md:
    /// zwyciezca 1-5% zabitych, przegrany 15-40% zabitych i 5-20% w niewoli).
    /// Jency: gra rozlicza ich po zakonczeniu bitwy - tu tylko zabici, ranni i rozbici.
    /// </summary>
    internal static class BattleChronicle
    {
        private static int _battles, _dead, _wounded, _small, _routs;
        private static double _winLossPct, _loseLossPct; private static int _winN, _loseN;

        private struct Side { public string Who; public int Start, Dead, Wounded, Routed, Gold, Lost; }

        private static Side Sum(MapEventSide side)
        {
            var r = new Side { Who = "?" };
            if (side == null) return r;
            try
            {
                var lp = side.LeaderParty;
                string leader = lp != null ? (lp.LeaderHero != null ? lp.LeaderHero.Name.ToString() : lp.Name.ToString()) : "?";
                string fac = side.MapFaction != null ? side.MapFaction.Name.ToString() : "?";
                int n = 0;
                foreach (var p in side.Parties)
                {
                    if (p == null) continue;
                    n++;
                    r.Start += p.HealthyManCountAtStart;
                    if (p.DiedInBattle != null) r.Dead += p.DiedInBattle.TotalManCount;
                    if (p.WoundedInBattle != null) r.Wounded += p.WoundedInBattle.TotalManCount;
                    if (p.RoutedInBattle != null) r.Routed += p.RoutedInBattle.TotalManCount;
                    r.Gold += p.PlunderedGold; r.Lost += p.GoldLost;
                }
                r.Who = leader + " (" + fac + (n > 1 ? ", partii " + n : "") + ")";
            }
            catch { }
            return r;
        }

        private static string Pct(int x, int of) { return of > 0 ? (100.0 * x / of).ToString("0.#") + "%" : "-"; }

        internal static void OnMapEventEnded(MapEvent me)
        {
            try
            {
                if (me == null) return;
                var a = Sum(me.AttackerSide); var d = Sum(me.DefenderSide);
                bool player = me.IsPlayerMapEvent;
                if (a.Dead + a.Wounded + d.Dead + d.Wounded == 0 && !player) return;      // bez walki (ucieczka, poddanie)
                int min = Math.Max(0, Settings.Current.BattleChronicleMinMen);
                if (!player && a.Start + d.Start < min) { _small++; return; }
                string where = "";
                try { where = me.MapEventSettlement != null ? " pod " + me.MapEventSettlement.Name : ""; } catch { }
                var win = me.WinningSide;
                string result = win == BattleSideEnum.Attacker ? "wygrywa atakujacy" : win == BattleSideEnum.Defender ? "wygrywa obronca" : "bez rozstrzygniecia";
                Log.Info("Bitwa: dzien " + (int)CampaignTime.Now.ToDays + (player ? " [GRACZ]" : "") + " - " + me.EventType + where + ": "
                         + a.Who + " " + a.Start + " ludzi vs " + d.Who + " " + d.Start + " ludzi; " + result
                         + " | atakujacy: zabici " + a.Dead + " (" + Pct(a.Dead, a.Start) + "), ranni " + a.Wounded + ", rozbici " + a.Routed + (a.Gold > 0 ? ", zdobyl " + a.Gold + " zl" : "") + (a.Lost > 0 ? ", stracil " + a.Lost + " zl" : "")
                         + " | obronca: zabici " + d.Dead + " (" + Pct(d.Dead, d.Start) + "), ranni " + d.Wounded + ", rozbici " + d.Routed + (d.Gold > 0 ? ", zdobyl " + d.Gold + " zl" : "") + (d.Lost > 0 ? ", stracil " + d.Lost + " zl" : "") + ".");
                _battles++; _dead += a.Dead + d.Dead; _wounded += a.Wounded + d.Wounded;
                // wpis 66 (test 16:53): wiekszosc bitew to lord 300-670 ludzi na bande 6-15 - przegrany ginie caly, srednia 70-80%
                // nic nie mowila; do porownania z historia tylko prawdziwe bitwy: obie strony >= BattleRealMinSide, sily nie gorsze niz 1:4
                int minSide = Math.Max(1, Settings.Current.BattleRealMinSide);
                bool real = a.Start >= minSide && d.Start >= minSide && Math.Min(a.Start, d.Start) * 4 >= Math.Max(a.Start, d.Start);
                if (!real) _routs++;
                if (real && (win == BattleSideEnum.Attacker || win == BattleSideEnum.Defender))
                {
                    var w = win == BattleSideEnum.Attacker ? a : d; var l = win == BattleSideEnum.Attacker ? d : a;
                    if (w.Start > 0) { _winLossPct += 100.0 * w.Dead / w.Start; _winN++; }
                    if (l.Start > 0) { _loseLossPct += 100.0 * l.Dead / l.Start; _loseN++; }
                }
            }
            catch (Exception e) { Log.Error("BattleChronicle", e); }
        }

        internal static void Daily()
        {
            if (_battles + _small > 0)
                Log.Info("Bitwy: dzien " + (int)CampaignTime.Now.ToDays + " - " + _battles + " starc (w tym pogromow/nierownych " + _routs + ", malych potyczek pominietych " + _small + "), zabitych " + _dead + ", rannych " + _wounded
                         + "; w " + _winN + " prawdziwych bitwach zwyciezcy tracili zabitych srednio " + (_winN > 0 ? (_winLossPct / _winN).ToString("0.#") + "%" : "-")
                         + ", przegrani " + (_loseN > 0 ? (_loseLossPct / _loseN).ToString("0.#") + "%" : "-") + " (historia: 1-5% / 15-40%).");
            _battles = _dead = _wounded = _small = _routs = 0; _winLossPct = _loseLossPct = 0; _winN = _loseN = 0;
        }
    }
}
