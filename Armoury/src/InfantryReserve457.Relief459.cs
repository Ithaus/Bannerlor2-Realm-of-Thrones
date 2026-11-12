using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>Stan zmiany linii 459 jednej druzyny (pole St.R w InfantryReserve457).</summary>
    internal sealed class Rel459
    {
        public int N, Pulled, Refused, Short, Gone, WaitRest, WaitBar, WaitSmall, Left, LeftCharge;
        public float At = -1f, MaxShare = -1f, MaxShareAt = -1f, MinMean = 9f, WalkTo = -1f, PhAt;
        public Formation F, Line;
        public bool Dry;
        public float DryAt = -1f;
        public string DryTxt = "-";
        public readonly HashSet<Agent> Fresh = new HashSet<Agent>();
        public readonly StringBuilder Hist = new StringBuilder();
    }

    /// <summary>
    /// 459 (pakiet 8 krok 8.3 audytu AI bitwy, docs/audyty-2026-11-11/AUDYT-AI-BITWA.md rozdz. 3: "Pauza w walce / wymiana linii - tylko obustronnie albo po 8.1;
    /// jednostronne oderwanie = rzez w plecy"; Jeff 11.11 "rob wszystko po kolei tak jak radzisz"). Wariant "po 8.1": ODWOD 457 ZMIENIA ZMECZONA LINIE.
    /// Klucz LineRelief ("Line Relief"), dziala tylko przy InfantryReserve TAK; NIE = co do bitu jak 457 (warunek liczony na sucho, linia "bylby").
    ///
    /// Pauzy obustronnej nie robimy (wymagalaby zgody obu stron, czyli oderwania tez szykow wroga - to jednostronne oderwanie w druga strone).
    /// Linia sama sie NIE odrywa: nikt w zwarciu nie dostaje rozkazu odejscia. Kolejnosc jak w zmianie szeregow:
    ///  1. WYZWALACZ (ostatni warunek odwodu 457 - po odwrocie / ataku na odwod / pekajacej linii / szarzy na strzelcow, tylko gdy zaden nie zaszedl):
    ///     szyk piechoty AI 0-7 w zwarciu (>= 5 wrogow w 30 m) i NIE pekajacy (LocalPowerRatio 457 >= 0.8 - pekajacy bierze warunek 3), nie w czworoboku 446;
    ///     pasek staminy znany (FieldCraft.BarOf - st.Ratio 144; przy SprintFatigueEnabled NIE nieznany = brak zmiany) u >= 20 ludzi i >= polowy szyku;
    ///     udzial zmeczonych (pasek &lt; 0.30) >= 0.40 w dwoch kolejnych skanach (ten sam szyk, najbardziej zmeczony z szykow w zwarciu);
    ///     odwod stoi >= 30 s (od utworzenia albo od poprzedniej zmiany - odpoczynek), sredni pasek odwodu >= 0.60, zywych w odwodzie >= 12; najwyzej 3 zmiany.
    ///  2. SWIEZI DO LINII: caly szyk 8 do tego szyku (Agent.Formation, przeniesienie masowe jak Merge 457) - stoja za walczacymi.
    ///  3. ZMECZENI NA TYL - dopiero gdy swiezi doszli (srodek swiezych w polowie glebokosci szyku + 10 m od jego mediany) albo po 25 s: ludzie tego szyku z paskiem
    ///     &lt; 0.30, kryterium odwodu 457 (Fit: piesi AI bez luku / kuszy, nie bohaterowie, nie chorazowie, nie dowodcy kompanii, nie w panice / odwrocie rannego 143 /
    ///     zbiorce 359, wolni od kompanii) i BEZ WROGA W 5 M (nie w zwarciu - nikt nie odwraca sie plecami do przeciwnika), najdalej od wroga; tylu, ilu swiezych
    ///     doszlo, szyk zostaje >= 30, co najmniej 12 (inaczej swiezi zostaja w linii, a odwod sie konczy jak w 457). Wypisani z kompanii (Detach457 z keepShare
    ///     459-p: udzial strat kompanii nie rosnie od wypisania zywych; wypisanie dopiero, gdy ludzi dosc), szyk 8
    ///     bez AI, Move na punkt 25 m za tym szykiem (HoldPoint 457) - to NOWY ODWOD 457 (te same warunki wyjscia, ta sama zmiana nastepnym razem).
    ///     Przez 30 s marszu na tyl (albo do dojscia w 15 m od punktu) warunki wyjscia 2-4 czekaja (zmeczeni nie zawracaja w pol drogi); odwrot druzyny / brak
    ///     linii (warunek 1) - od razu jak w 457.
    /// Bez bonusow do liczb. Pasek wraca tak, jak zwykle wraca w spoczynku (FieldCraft / RBM) - nic nie dopisujemy.
    /// Linie: "Zmiana linii (459)" przy kazdej zmianie / "bylby" (przy NIE raz na druzyne), "Zmiana linii (459) koniec" po linii konca 457.
    /// Wyjatek per czlowiek / szyk = potkniecie 457 (Stumble), bez globalnego wylacznika. Glowny watek (OnMissionTick 457). Bez pol statycznych MovementOrder.
    /// </summary>
    internal sealed partial class InfantryReserve457
    {
        private const string P459 = "Zmiana linii (459): ";
        private const float TiredBar = 0.30f, TiredShare = 0.40f, FreshBar = 0.60f, RestMin = 30f, ArriveMax = 25f, ArriveSlack = 10f, ClearR = 5f, WalkMax = 30f, WalkNear = 15f;
        private const int KnownMin = 20, MaxRelief = 3, QueryMax = 200;

        private readonly List<KeyValuePair<Formation, float>> _contact = new List<KeyValuePair<Formation, float>>();

        internal static bool RelOn { get { var s = Settings.Current; return s != null && s.LineRelief; } }

        /// <summary>Zmeczeni po zmianie linii jeszcze ida na tyl (tylko przy TAK; przy NIE WalkTo = -1).</summary>
        private bool WalkBack(St s, float now)
        {
            var r = s.R;
            if (r.WalkTo < 0f) return false;
            if (now >= r.WalkTo) { r.WalkTo = -1f; return false; }
            Vec2 c = Centre(s);
            if (c.IsValid && s.Hold.IsValid && c.Distance(s.Hold.AsVec2) <= WalkNear) { r.WalkTo = -1f; return false; }
            return true;
        }

        /// <summary>Pasek ludzi szyku: znani, sredni, udzial zmeczonych.</summary>
        private void Tired(Formation f, out int known, out float mean, out float share)
        {
            known = 0; mean = 1f; share = 0f;
            _units.Clear();
            f.ApplyActionOnEachUnit(_collect);
            float sum = 0f;
            int tired = 0;
            for (int i = 0; i < _units.Count; i++)
            {
                var a = _units[i];
                try
                {
                    if (a == null || !a.IsActive() || !a.IsHuman || a.HasMount || a.IsRunningAway) continue;
                    float b;
                    if (!FieldCraft.BarOf(a, out b)) continue;
                    known++;
                    sum += b;
                    if (b < TiredBar) tired++;
                }
                catch (Exception e) { Stumble("Pasek", e); }
            }
            _units.Clear();
            if (known > 0) { mean = sum / known; share = (float)tired / known; }
        }

        /// <summary>Sredni pasek zywych ludzi odwodu w szyku 8 (tylko TAK); -1 = nieznany.</summary>
        private float ResBar(St s)
        {
            float sum = 0f;
            int n = 0;
            for (int i = 0; i < s.Res.Count; i++)
            {
                var a = s.Res[i];
                try
                {
                    if (a == null || !a.IsActive() || a.IsRunningAway || !ReferenceEquals(a.Formation, s.F8)) continue;
                    float b;
                    if (!FieldCraft.BarOf(a, out b)) continue;
                    sum += b;
                    n++;
                }
                catch (Exception e) { Stumble("PasekOdwodu", e); }
            }
            return n > 0 ? sum / n : -1f;
        }

        /// <summary>Wyzwalacz zmiany (Keep 457, gdy zaden warunek wyjscia nie zaszedl).</summary>
        private void Relief(Mission m, St s, float now, int alive)
        {
            var r = s.R;
            Formation best = null;
            float bs = -1f, bm = 1f;
            int bk = 0;
            for (int i = 0; i < _contact.Count; i++)
            {
                var f = _contact[i].Key;
                try
                {
                    if (_contact[i].Value < RatioOut || Doctrine.Q446Holds(f)) continue;
                    int known;
                    float mean, share;
                    Tired(f, out known, out mean, out share);
                    if (known < KnownMin || known * 2 < f.CountOfUnits) continue;
                    if (share > r.MaxShare) { r.MaxShare = share; r.MaxShareAt = now; }
                    if (mean < r.MinMean) r.MinMean = mean;
                    if (share > bs) { bs = share; bm = mean; bk = known; best = f; }
                }
                catch (Exception e) { Stumble("Zmeczenie", e); }
            }
            if (r.N >= MaxRelief || (r.Dry && !_relOn)) return;
            if (best == null || bs < TiredShare) { r.At = -1f; r.F = null; return; }
            if (r.At < 0f || !ReferenceEquals(r.F, best)) { r.At = now; r.F = best; return; }
            if (now - s.FormAt < RestMin) { r.WaitRest++; return; }
            if (alive < ReserveMin) { r.WaitSmall++; return; }
            float rb = _keyOn ? ResBar(s) : -1f;
            if (_keyOn && rb < FreshBar) { r.WaitBar++; return; }
            var t = s.T;
            string what = "zmeczona linia F" + (int)best.FormationIndex + " (" + best.CountOfUnits + " ludzi, zmeczonych " + (bs * 100f).ToString("0", Inv) + "% z " + bk
                          + " znanych, sredni pasek " + bm.ToString("0.00", Inv) + ")";
            if (!_relOn)
            {
                r.Dry = true;
                r.DryAt = now;
                r.DryTxt = what;
                Log.Info(P459 + "t=" + now.ToString("0", Inv) + "s | T" + t.TeamIndex + " | klucz NIE (" + (_keyOn ? "Line Relief" : "Infantry Reserve") + ") | bylby: " + what
                         + " -> odwod " + alive + (rb >= 0f ? " (pasek " + rb.ToString("0.00", Inv) + ")" : "") + " do linii, zmeczeni bez wroga w " + ClearR.ToString("0", Inv) + " m na tyl.");
                return;
            }
            // 2. swiezi do linii
            _units.Clear();
            try { s.F8.ApplyActionOnEachUnit(_collect); } catch (Exception e) { Stumble("ZbiorZmiana", e); }
            var men = new List<Agent>(_units.Count);
            for (int i = 0; i < _units.Count; i++) { var a = _units[i]; if (a != null && a.IsActive()) men.Add(a); }
            _units.Clear();
            if (men.Count < ReserveMin) { r.WaitSmall++; return; }
            r.Fresh.Clear();
            foreach (var a in men) r.Fresh.Add(a);
            r.Line = best;
            r.PhAt = now;
            r.At = -1f;
            r.F = null;
            s.Out = true;
            s.OutAt = now;
            s.OutWhy = what;
            s.OutN = men.Count;
            s.LostBefore = s.Size - alive;
            s.OutTo = "zmiana linii F" + (int)best.FormationIndex;
            s.OutDist = -1f;
            try { Vec2 c = Centre(s); s.OutDist = c.IsValid ? best.CachedMedianPosition.AsVec2.Distance(c) : -1f; } catch { s.OutDist = -1f; }
            s.Ph = PhRelief;
            s.PhAt = now;
            Transfer(t, men, best, new List<Formation> { s.F8 });
            Log.Info(P459 + "t=" + now.ToString("0", Inv) + "s | T" + t.TeamIndex + " | klucz TAK | zmiana " + (r.N + 1) + ": " + what + " | swiezych " + men.Count
                     + " (pasek " + rb.ToString("0.00", Inv) + ", w odwodzie " + (now - s.FormAt).ToString("0", Inv) + " s) do linii, odleglosc "
                     + (s.OutDist >= 0f ? s.OutDist.ToString("0", Inv) + " m" : "-") + ".");
        }

        /// <summary>Faza zmiany (Phase 457): czekanie na dojscie swiezych, potem zmeczeni bez wroga w 5 m do szyku 8 jako nowy odwod.</summary>
        private void ReliefStep(Mission m, St s, float now)
        {
            var r = s.R;
            var t = s.T;
            var f = r.Line;
            int fresh = 0;
            Vec2 fc = Vec2.Zero;
            foreach (var a in r.Fresh)
            {
                try
                {
                    if (a == null || !a.IsActive() || a.IsRunningAway || !ReferenceEquals(a.Formation, f)) continue;
                    fresh++;
                    fc += a.Position.AsVec2;
                }
                catch (Exception e) { Stumble("Swiezi", e); }
            }
            string end = null;
            if (f == null || f.CountOfUnits <= 0 || !AiForm(f, t)) end = "szyk zniknal / nie pod AI";
            else if (Retreat(f)) end = "odwrot szyku";
            else if (fresh == 0) end = "swiezi wygineli / uciekli / przelozeni poza ten szyk przed dojsciem";
            if (end == null)
            {
                bool arrived = now - r.PhAt >= ArriveMax;
                if (!arrived)
                {
                    float depth = 0f;
                    try { depth = f.Depth; } catch { depth = 0f; }
                    fc = fc * (1f / fresh);
                    arrived = fc.Distance(f.CachedMedianPosition.AsVec2) <= depth * 0.5f + ArriveSlack;
                }
                if (!arrived) return;
                Swap(m, s, now, fresh);
                return;
            }
            r.Gone++;
            Strand(s, f);   // 459-p: swiezi, ktorzy utkneli w szyku 8 (potkniecie przy przelozeniu) - do linii albo szarza
            s.Ph = PhDone;
            s.MergeAt = now;
            s.MergeN = fresh;
            s.MergeTo = "zmiana linii F" + (f != null ? (int)f.FormationIndex : -1) + " przerwana: " + end;
            r.Hist.Append(r.Hist.Length > 0 ? "; " : "").Append("t=").Append(now.ToString("0", Inv)).Append("s przerwana (").Append(end).Append(')');
            Log.Info(P459 + "t=" + now.ToString("0", Inv) + "s | T" + t.TeamIndex + " | " + s.MergeTo + " - swiezi zostaja w linii, odwodu nie ma.");
        }

        private void Swap(Mission m, St s, float now, int fresh)
        {
            var r = s.R;
            var t = s.T;
            var f = r.Line;
            Strand(s, f);   // 459-p: inaczej "szyk 8 niedostepny" i swiezi zostaja w szyku 8 bez AI do konca bitwy
            Vec2 foe;
            bool hasFoe = FoeCentre(m, t, out foe);
            _units.Clear();
            try { f.ApplyActionOnEachUnit(_collect); } catch (Exception e) { Stumble("ZbiorZmeczonych", e); }
            var cand = new List<KeyValuePair<float, Agent>>();
            int queries = 0, inFight = 0;
            for (int i = 0; i < _units.Count; i++)
            {
                var a = _units[i];
                try
                {
                    if (r.Fresh.Contains(a) || !Fit(a)) continue;
                    float b;
                    if (!FieldCraft.BarOf(a, out b) || b >= TiredBar) continue;
                    if (queries >= QueryMax) break;
                    queries++;
                    if (FoesNear(m, t, a.Position.AsVec2, ClearR) > 0) { inFight++; continue; }
                    cand.Add(new KeyValuePair<float, Agent>(hasFoe ? a.Position.AsVec2.DistanceSquared(foe) : 0f, a));
                }
                catch (Exception e) { Stumble("Zmeczony", e); }
            }
            _units.Clear();
            cand.Sort((x, y) => y.Key.CompareTo(x.Key));
            int k = Math.Min(Math.Min(fresh, cand.Count), f.CountOfUnits - KeepMin);
            WorldPosition hold = WorldPosition.Invalid;
            string no = null;
            if (k < ReserveMin) no = "zmeczonych wolnych za malo (" + cand.Count + " bez wroga w " + ClearR.ToString("0", Inv) + " m, " + inFight + " w zwarciu; szyk zostaje >= " + KeepMin + ")";
            else if (s.F8 == null || s.F8.CountOfUnits > 0 || ReferenceEquals(t.GeneralsFormation, s.F8) || s.F8.PlayerOwner != null) no = "szyk 8 niedostepny";
            else if (!HoldPoint(m, t, f, out hold)) no = "brak miejsca za linia (siatka / granice pola)";
            var moved = new List<Agent>(k > 0 ? k : 0);
            if (no == null)
            {
                // 459-p (recenzja 459): najpierw wybor bez skutkow (Free457), wypisanie z kompanii dopiero gdy ludzi dosc - nikt nie zostaje wypisany a nieprzelozony
                var pick = new List<Agent>(k);
                for (int i = 0; i < cand.Count && pick.Count < k; i++)
                {
                    if (!BannerCompanies.Free457(cand[i].Value)) { r.Refused++; s.Refused++; continue; }
                    pick.Add(cand[i].Value);
                }
                if (pick.Count < ReserveMin) no = "po odmowach kompanii za malo ludzi (" + pick.Count + ")";
                else
                {
                    for (int i = 0; i < pick.Count; i++)
                    {
                        // keepShare: Peak tak, by udzial strat kompanii nie skoczyl od wypisania zywych (inaczej rzut zlamania bez poleglych)
                        int d = BannerCompanies.Detach457(pick[i], true);
                        if (d < 0) { r.Refused++; s.Refused++; continue; }
                        if (d > 0) s.Detached++;
                        moved.Add(pick[i]);
                    }
                    if (moved.Count == 0) no = "po odmowach kompanii nikogo";   // tylko potkniecie Detach457 - wtedy i tak nikt nie wypisany
                }
            }
            if (no != null)
            {
                r.Short++;
                s.Ph = PhDone;
                s.MergeAt = now;
                s.MergeN = fresh;
                s.MergeTo = "zmiana linii F" + (int)f.FormationIndex + ": swiezi zostali, " + no;
                r.Hist.Append(r.Hist.Length > 0 ? "; " : "").Append("t=").Append(now.ToString("0", Inv)).Append("s bez nowego odwodu (").Append(no).Append(')');
                Log.Info(P459 + "t=" + now.ToString("0", Inv) + "s | T" + t.TeamIndex + " | " + s.MergeTo + " - odwodu nie ma.");
                return;
            }
            try { s.F8.SetControlledByAI(false); } catch (Exception e) { Stumble("AI8", e); }
            Transfer(t, moved, s.F8, new List<Formation> { f });
            try
            {
                s.F8.SetControlledByAI(false);
                s.F8.SetMovementOrder(MovementOrder.MovementOrderMove(hold));
                s.F8.SetFacingOrder(FacingOrder.FacingOrderLookAtEnemy);
            }
            catch (Exception e) { Stumble("Rozkaz8", e); }
            r.N++;
            r.Pulled += moved.Count;
            r.WalkTo = now + WalkMax;
            r.Fresh.Clear();
            float took = now - r.PhAt;
            r.Hist.Append(r.Hist.Length > 0 ? "; " : "").Append("t=").Append(now.ToString("0", Inv)).Append("s F").Append((int)f.FormationIndex).Append(" swiezych ").Append(fresh)
             .Append(" za zmeczonych ").Append(moved.Count).Append(" (dojscie ").Append(took.ToString("0", Inv)).Append(" s)");
            // nowy odwod 457 (te same warunki wyjscia)
            s.Res.Clear();
            s.Res.AddRange(moved);
            s.Size = moved.Count;
            s.FormAt = now;
            s.Anchor = f;
            s.Hold = hold;
            s.HoldAt = now;
            s.Src = "zmiana linii " + r.N + " z F" + (int)f.FormationIndex;
            s.Out = false;
            s.Ph = PhHold;
            s.OutAt = -1f; s.OutDist = -1f; s.OutN = 0; s.LostBefore = 0; s.OutWhy = "-"; s.OutTo = "-";
            s.LowAt = -1f; s.LowFirst = -1f; s.LowF = null; s.Weak = null; s.Arch = null; s.Cav = null; s.CalmAt = -1f;
            s.MergeAt = -1f; s.MergeN = 0; s.MergeTo = "-";
            Log.Info(P459 + "t=" + now.ToString("0", Inv) + "s | T" + t.TeamIndex + " | zmiana " + r.N + " F" + (int)f.FormationIndex + ": swiezi doszli po " + took.ToString("0", Inv)
                     + " s (" + fresh + "), zmeczonych na tyl " + moved.Count + " (kandydatow " + cand.Count + ", w zwarciu pominietych " + inFight + ", odmowy kompanii "
                     + r.Refused + ") - nowy odwod w szyku 8, " + HoldBack.ToString("0", Inv) + " m za F" + (int)f.FormationIndex + ".");
        }

        /// <summary>459-p (recenzja 459): swiezi, ktorzy po potknieciu Transfer zostali w szyku 8 (bez AI, ostatni rozkaz) - ponownie do szyku linii, a gdy
        /// nie ma linii albo przelozenie znow sie nie uda i w szyku 8 sa tylko oni - szarza szyku 8 (jak Merge 457 bez szyku 0-7). Cudzych ludzi szyku 8 nie rusza.</summary>
        private void Strand(St s, Formation f)
        {
            var r = s.R;
            var t = s.T;
            var f8 = s.F8;
            if (f8 == null || t == null || r.Fresh.Count == 0) return;
            try
            {
                if (f8.CountOfUnits <= 0 || ReferenceEquals(t.GeneralsFormation, f8) || f8.PlayerOwner != null) return;
            }
            catch (Exception e) { Stumble("Utkneli8", e); return; }
            var left = new List<Agent>();
            foreach (var a in r.Fresh)
            {
                try { if (a != null && a.IsActive() && ReferenceEquals(a.Formation, f8)) left.Add(a); }
                catch (Exception e) { Stumble("Utkneli", e); }
            }
            if (left.Count == 0) return;
            r.Left += left.Count;
            bool line = false;
            try { line = f != null && f.CountOfUnits > 0 && AiForm(f, t); } catch { line = false; }
            if (line) Transfer(t, left, f, new List<Formation> { f8 });
            try
            {
                int still = 0;
                for (int i = 0; i < left.Count; i++) if (left[i].IsActive() && ReferenceEquals(left[i].Formation, f8)) still++;
                if (still > 0 && f8.CountOfUnits <= still)
                {
                    object ch = ChargeOrder();
                    if (ch != null) { f8.SetMovementOrder((MovementOrder)ch); r.LeftCharge += still; }
                }
            }
            catch (Exception e) { Stumble("Utkneli8Szarza", e); }
        }

        /// <summary>Linia "Zmiana linii (459) koniec" (OnEndMission 457, po linii konca 457).</summary>
        private void EndRelief(float now)
        {
            var sb = new StringBuilder(512);
            sb.Append("Zmiana linii (459) koniec: t=").Append(now.ToString("0", Inv)).Append("s | klucz ").Append(_relOn ? "TAK" : "NIE")
              .Append(_keyOn ? "" : " (Infantry Reserve NIE)").Append(" | druzyn AI ").Append(_teams.Count);
            int all = 0, pulled = 0, dry = 0;
            foreach (var s in _teams)
            {
                var r = s.R;
                all += r.N;
                pulled += r.Pulled;
                if (r.Dry) dry++;
                sb.Append(" || T").Append(s.T != null ? s.T.TeamIndex : -1).Append(": ");
                if (!s.Formed) { sb.Append("bez odwodu"); continue; }
                sb.Append("zmian ").Append(r.N).Append(", zmeczonych na tyl ").Append(r.Pulled).Append(", bez nowego odwodu ").Append(r.Short).Append(", przerwane ").Append(r.Gone)
                  .Append(", odmowy kompanii ").Append(r.Refused)
                  .Append(", utkneli w szyku 8 ").Append(r.Left).Append(" (szarza ").Append(r.LeftCharge).Append(')')
                  .Append(", najwiekszy udzial zmeczonych w zwarciu ").Append(r.MaxShare >= 0f ? (r.MaxShare * 100f).ToString("0", Inv) + "% t=" + r.MaxShareAt.ToString("0", Inv) + "s" : "-")
                  .Append(", najnizszy sredni pasek ").Append(r.MinMean < 9f ? r.MinMean.ToString("0.00", Inv) : "-")
                  .Append(", czekanie: odpoczynek ").Append(r.WaitRest).Append(" / pasek odwodu ").Append(r.WaitBar).Append(" / maly odwod ").Append(r.WaitSmall);
                if (r.Dry) sb.Append(" | bylby t=").Append(r.DryAt.ToString("0", Inv)).Append("s: ").Append(r.DryTxt);
                if (r.Hist.Length > 0) sb.Append(" | ").Append(r.Hist);
            }
            sb.Append(" || razem: zmian ").Append(all).Append(", zmeczonych na tyl ").Append(pulled).Append(", bylby ").Append(dry)
              .Append(" | prog: pasek < ").Append(TiredBar.ToString("0.00", Inv)).Append(" u >= ").Append((TiredShare * 100f).ToString("0", Inv)).Append("% (>= ").Append(KnownMin)
              .Append(" znanych), odwod >= ").Append(RestMin.ToString("0", Inv)).Append(" s i pasek >= ").Append(FreshBar.ToString("0.00", Inv)).Append(", najwyzej ").Append(MaxRelief)
              .Append(" na druzyne, zmeczeni bez wroga w ").Append(ClearR.ToString("0", Inv)).Append(" m.");
            Log.Info(sb.ToString());
        }
    }
}
