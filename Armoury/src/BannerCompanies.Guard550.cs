using System;
using System.Collections.Generic;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 550b (15.11, Jeff w bitwie: "podszedlem do goscia z banerem ... reszta jego ludzi gapi sie jak idioci ... normalnie rzucili by sie na mnie od razu jak tylko
    /// podejde do baneru, nie pozwolili go przechwycic"). STRAZ SZTANDARU - skan bliskosci (klucz BannerGuardsAnswer, z FormationsAnswerHarassers 283).
    /// Przyczyna: wykrycie 283 przychodzi tylko po ciosie (MeleeEvent); podejscie do chorazego albo lezacego sztandaru nie budzilo nikogo.
    /// Co takt BannerCompaniesMission (0.5 s, glowny watek, po TickLead): wokol kazdego czynnego chorazego AI (_bearerOf) i kazdego lezacego sztandaru (158, _fallen)
    /// zapytanie GetNearbyEnemyAgents w GuardR (8 m); wrogowie = czynni, nieuciekajacy ludzie. Gdy jest ich 1..GuardMaxFoes (3) - kazdy do HorsePursuit.BannerNear
    /// (bramka samotnosci i frontu jak MeleeEvent, zagrozenie 283 z ofiara = ten szyk i miejscem = sztandar). Wiecej niz 3 = zwarcie linii, zwykla walka.
    /// Najwyzej GuardQPerTick (24) zapytan na takt z rotacja (nadmiar w liczniku). Kazdy punkt we wlasnym try/catch - licznik potkniec, bez wylacznika.
    /// Nie czyta pol MovementOrder (pamiec "movementorder-pole-poza-misja").
    /// </summary>
    internal sealed partial class BannerCompaniesMission
    {
        private struct GuardPt { public Vec2 P; public Formation F; public Agent Bearer; }

        private readonly List<GuardPt> _g550Pts = new List<GuardPt>();
        private readonly MBList<Agent> _g550Buf = new MBList<Agent>();
        private readonly List<Agent> _g550Foes = new List<Agent>();
        private int _g550Rot;

        private void Guard550(float now, Settings c, Mission m)
        {
            if (c == null || !c.FormationsAnswerHarassers || !c.BannerGuardsAnswer || m == null || !_field) return;
            _g550Pts.Clear();
            foreach (var kv in _bearerOf)
            {
                var b = kv.Key;
                try
                {
                    if (b == null || !b.IsActive() || !b.IsAIControlled || b.Banner == null) continue;
                    var f = b.Formation;
                    if (f == null || f.CountOfUnits <= 0 || f.Team == null) continue;
                    _g550Pts.Add(new GuardPt { P = b.Position.AsVec2, F = f, Bearer = b });
                }
                catch (Exception e) { HorsePursuit.G550Stum++; BannerCompanies.Stumble("BannerCompaniesMission.Guard550", e); }
            }
            for (int i = 0; i < _fallen.Count; i++)
            {
                var fl = _fallen[i];
                if (fl == null || fl.Closed || fl.F == null || fl.F.CountOfUnits <= 0 || fl.F.Team == null) continue;
                _g550Pts.Add(new GuardPt { P = fl.Pos.AsVec2, F = fl.F, Bearer = null });
            }
            int n = _g550Pts.Count;
            if (n == 0) return;
            int q = Math.Min(n, HorsePursuit.GuardQPerTick);
            if (n > q) HorsePursuit.G550Cut += n - q;
            int start = _g550Rot < n ? _g550Rot : 0;
            for (int k = 0; k < q; k++)
            {
                var pt = _g550Pts[(start + k) % n];
                try { GuardOne(pt, now, m); }
                catch (Exception e) { HorsePursuit.G550Stum++; BannerCompanies.Stumble("BannerCompaniesMission.Guard550", e); }
                finally { _g550Buf.Clear(); _g550Foes.Clear(); }
            }
            _g550Rot = n > q ? (start + q) % n : 0;
            _g550Pts.Clear();
        }

        private void GuardOne(GuardPt pt, float now, Mission m)
        {
            m.GetNearbyEnemyAgents(pt.P, HorsePursuit.GuardR, pt.F.Team, _g550Buf);
            for (int i = 0; i < _g550Buf.Count; i++)
            {
                var x = _g550Buf[i];
                if (x != null && x.IsActive() && x.IsHuman && !x.IsRunningAway) _g550Foes.Add(x);
            }
            _g550Buf.Clear();
            int nf = _g550Foes.Count;
            if (nf == 0) return;
            if (nf > HorsePursuit.GuardMaxFoes) { HorsePursuit.G550Many++; return; }   // zwarcie linii - zwykla walka
            for (int i = 0; i < nf; i++) HorsePursuit.BannerNear(_g550Foes[i], pt.F, pt.P, now, pt.Bearer, nf);
        }
    }
}
