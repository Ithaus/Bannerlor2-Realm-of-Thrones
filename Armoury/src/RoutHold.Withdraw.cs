using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 143 (25.10, karta "141 Ranny ponizej 20% HP: odwrot z bronia", decyzja Jeffa D2): tryb "Wycofanie" RoutKeeper - liczniki i linia w WoundedWithdraw.cs.
    ///
    /// Dowod [K] (dekompilacje 1.4.8: scratchpad/panB/CommonAI.cs, scratchpad/ai_run/tw_Agent.cs, tw_Mission.cs, tw_Formation.cs; RBMAI scratchpad/surv/RBMAI.AgentAi.cs):
    ///  - RBM AgentAi.cs:651-657 wola co klatke CommonAIComponent.StopRetreating przy morale &gt; 0 i 10 s bez ciosu wrecz, a StopRetreating dziala TYLKO, gdy komponent
    ///    ma IsRetreating (CommonAI.cs StopRetreating: "if (IsRetreating)"). Natywny Agent.Retreat(pos) (SetRetreatMode) komponentu nie dotyka - RBM tego odwrotu nie gasi,
    ///    nie ma Mission.OnAgentPanicked (zrzut broni ArtemsLivelyAnimations, Debacle), OnAgentFleeing (zarazanie AgentMoraleInteractionLogic) ani EnforceShieldUsage(None).
    ///  - Natywny odwrot ustawia Agent.IsRunningAway (callback SetAgentAIPerformingRetreatBehavior), a Mission.CanAgentRout jest prawdziwe dla IsRunningAway - wiec
    ///    CommonAIComponent.OnTickParallel (co 0.5-0.6 s) wola StartFadingOut, gdy czlowiek jest blizej celu odwrotu (GetRetreatPos) niz 0.55 s x v^2 + 2 x promien kapsuly
    ///    (sprint 7 m/s: ok. 5.3 m, kon w galopie 12 m/s: ok. 8.9 m) albo przy granicy pola. Dlatego: postoj (Agent.StopRetreating + SetScriptedPosition) juz od
    ///    max(ArriveRadius = 8 m, pierwiastek(0.8 x v^2 + 9)) (sprawdzane co klatke) i cel co najmniej EdgeMargin = 15 m od granicy - bez tego ranny "uciekalby
    ///    z pola" za wlasna linia (licznik "uszedl z pola", oczekiwane 0).
    ///  - Przy morale ponizej 0.01 bez IsRetreating komponentu gra sama wola Panic (CommonAI.cs OnTickParallel) - trzymamy MoraleFloor 0.02 (jak RoutHold po zgaszeniu).
    ///  - Rozkaz odwrotu szyku (MovementOrder.RetreatAux -> CommonAIComponent.Retreat) i szarza RBM (AgentAi.cs:826-859) ustawiaja IsRetreating komponentu - to cudzy
    ///    odwrot: konczymy nasz (postoj zdjety) i zostawiamy grze. Panika gry (IsPanicked) - 142 przejmuje z kolejki (Break konczy nasz odwrot).
    ///  - BannerCompanies "polowa skladu" (InRanks) liczy tylko !IsPanicked - ranny w odwrocie NIE jest strata kompanii (zostaje w szeregu wobec rzutu); fale zalamania
    ///    i wstrzas pomijaja IsRunningAway (ranny w drodze), postoj - nie (zalamanie kompanii lamie tez rannego na postoju: Break konczy odwrot).
    /// Zasady:
    ///  - wejscie RoutKeeper.Withdraw (BrokenMen co 0.5 s, raz na czlowieka na bitwe; klucz zostaje po losie): czlowiek AI w waznej druzynie, nie w zbiorze zlamanych;
    ///    juz w panice gry - nic (142 przejmie; bez RoutedMenNeverRally - stara regula Break), cudzy odwrot (komponent albo natywny) - czekamy; cudzy odwrot
    ///    bez paniki W TRAKCIE naszego (szarza RBM, rozkaz odwrotu szyku) konczy nasz i zdejmuje klucz - po nim BrokenMen wycofa rannego od nowa;
    ///  - cel co RefreshEvery (4 s): tyl wlasnego szyku = CurrentPosition - Direction x (Depth/2 + 5 m), potem Depth/2 + 2 m, potem 3 m za najblizszym chorazym strony
    ///    (BannerBearerLogic) do 80 m; cel w granicach pola, 15 m od granicy, bez wroga (nie uciekajacego) w 10 m, z droga (Scene.GetPathDistanceBetweenPositions,
    ///    najwyzej PathPerFrame zapytan na klatke). Brak bezpiecznego miejsca - nic nie wysylamy, w szyku walczy dalej, proba za RefreshEvery;
    ///  - w drodze: Agent.Retreat(cel) natywnie - pierwszy raz, gdy cel przesunal sie o ponad 5 m albo natywny odwrot zgasl (ponowienie co RefreshEvery);
    ///    po WalkMax (30 s) w drodze - postoj tam, gdzie stoi;
    ///  - postoj: Agent.StopRetreating + SetScriptedPosition(cel) bez NoAttack (stoi za linia, broni sie, gdy ktos do niego dojdzie); co RefreshEvery cel na nowo
    ///    (szyk sie rusza - postoj idzie za nim), postoj przywrocony, gdy ktos zdjal GoToPosition (RBM StanceLogic przy zmianie szyku: DisableScriptedMovement);
    ///    "wrocil do walki" = na postoju znow w przedniej polowie wlasnego szyku (pomiar: czy postoj trzyma przy RBM Frontline / szarzy - kod natywny);
    ///  - szyk rozbity (bez szyku, sam, co najmniej polowa reszty szyku w PANICE wedlug przelacznika 139) - RoutHold.Break(a, Flee): jedna regula zlamanego;
    ///  - koniec bitwy (MissionEnded): postoj zdjety; idacy po stronie zwyciezcow - StopRetreating (jak gra spanikowanym zwyciezcom), przegrani ida dalej.
    /// Wszystko na glownym watku (OnMissionTick RoutKeeper, callbacki misji). Wyjatek na czlowieka liczony (RoutHold.SiteWithdraw), po MaxFails wypada (postoj zdjety).
    /// </summary>
    internal sealed partial class RoutKeeper
    {
        private sealed class Wd
        {
            public Agent A;
            public bool Holding;              // za linia: postoj (ScriptedPosition)
            public bool Arrived;              // postoj juz liczony (raz na czlowieka)
            public bool Done;                 // poza tickiem (los, inna droga, gracz, potkniecia) - klucz w _wd zostaje (raz na bitwe)
            public bool Fought;               // cios wrecz zadany po wejsciu
            public bool Returned;             // na postoju znow w przedniej polowie szyku - "wrocil do walki" (raz)
            public int Kind = -1;             // rodzaj celu (WoundedWithdraw.Kind*)
            public int Why;                   // szyk rozbity - powod dla Break (EndWithdrawForBreak)
            public int Fails;
            public float Since;
            public float NextEval;
            public float LastSend = -100f;    // ostatni natywny Agent.Retreat (-100 = jeszcze nie)
            public float FirstSend = -100f;   // pierwszy natywny Agent.Retreat (poczatek drogi)
            public Vec2 Target = Vec2.Invalid;
            public WorldPosition Wp = WorldPosition.Invalid;
        }

        private const int CheckOk = 0, CheckBad = 1, CheckLimit = 2, KindLimit = -2;

        private readonly Dictionary<Agent, Wd> _wd = new Dictionary<Agent, Wd>();
        private readonly List<Wd> _wList = new List<Wd>();
        private static readonly Predicate<Wd> WdDone = w => w.Done;
        // szyk rozbity: liczenie ludzi szyku raz na klatke na szyk (wielu rannych z jednego szyku)
        private struct FCount { public int All, Panic; }
        private readonly Dictionary<Formation, FCount> _fCount = new Dictionary<Formation, FCount>();
        private float _fStamp = -1f;
        private int _cntAll, _cntPanic;
        private Action<Agent> _cnt;
        private int _wPathLeft;
        private bool _wEnded;
        private BannerBearerLogic _bbl;
        private bool _bblInit;

        /// <summary>Bitwa polowa (zakres 142): IsFieldBattle bez kryjowki i morza, rodzaj "bitwa" - liczone raz na misje.</summary>
        internal bool FieldForWithdraw(Mission m)
        {
            return m != null && ReferenceEquals(m, Mission) && NoReturnHere(m);
        }

        /// <summary>Czlowiek jest teraz w odwrocie z bronia (w drodze albo na postoju). Tylko odczyt, glowny watek.</summary>
        internal bool Withdrawing(Agent a)
        {
            Wd w;
            return a != null && _wd.TryGetValue(a, out w) && !w.Done;
        }

        // ------------------------------------------------------------ wejscie (BrokenMen)

        internal void Withdraw(Agent a)
        {
            var m = Mission;
            if (m == null || a == null) return;
            if (_by.ContainsKey(a)) return;                               // juz zlamany - jego droga (RoutHold)
            if (_wd.ContainsKey(a)) return;                               // raz na czlowieka na bitwe
            if (!a.IsActive() || !a.IsHuman) return;
            if (m.MissionEnded) return;                                   // po bitwie gra sama prowadzi odwroty
            if (!a.IsAIControlled || a.IsMainAgent) return;
            if (Undead.Character(a.Character)) return;
            if (!MissionTeams.Valid(a.Team)) { MissionTeams.Skip(a, MissionTeams.Broken); return; }
            var cai = a.CommonAIComponent;
            if (cai == null) return;
            if (cai.IsPanicked)
            {
                // juz w panice gry: 142 wezmie go z kolejki (OnAgentPanicked); bez RoutedMenNeverRally - stara regula NB2 ("takze ten, kto juz ucieka panika gry")
                var c = Settings.Current;
                if (c != null && c.RoutedMenNeverRally) WoundedWithdraw.PanickedSkip.Add(a.Index);
                else Break(a, MissionTeams.Broken);
                return;
            }
            // cudzy odwrot - szarza RBM / rozkaz odwrotu szyku (komponent), natywny odwrot szyku; BrokenMen sprobuje za 0.5 s
            if (cai.IsRetreating) { WoundedWithdraw.WaitComp.Add(a.Index); return; }
            if (a.IsRunningAway) { WoundedWithdraw.WaitOrder.Add(a.Index); return; }

            float now = m.CurrentTime;
            var w = new Wd { A = a, Since = now, NextEval = now };
            _wd[a] = w;
            _wList.Add(w);
            MissionTeams.Pushed(MissionTeams.Broken, a);                  // linia NM: odwrot rannego wyslany - raz na czlowieka
            bool mine = false, wpn, sh;
            try { var t = a.Team; mine = t != null && (t.IsPlayerTeam || t.IsPlayerAlly); } catch { }
            BrokenFate.Arms(a, out wpn, out sh);
            WoundedWithdraw.Started(a, mine, wpn, sh);
        }

        /// <summary>Break (z bolu, kompanii, paniki gry albo rozbitego szyku) lamie rannego w odwrocie: postoj zdjety, powod do licznika.</summary>
        private void EndWithdrawForBreak(Agent a, int site)
        {
            Wd w;
            if (a == null || !_wd.TryGetValue(a, out w) || w.Done) return;
            int why;
            if (w.Why > 0) why = w.Why;                                   // szyk rozbity - z ticku odwrotu
            else if (site == MissionTeams.GamePanic) why = WoundedWithdraw.WhyGame;
            else if (site == MissionTeams.Flee) why = Pain(a) ? WoundedWithdraw.WhyPain : WoundedWithdraw.WhyCompany;
            else why = WoundedWithdraw.WhyOther;
            Release(w);
            w.Done = true;
            WoundedWithdraw.Ended(why);
        }

        /// <summary>Ponizej progu FieldCraft (AiFleeBelowPercent) - ta sama miara co FieldCraft (Health / HealthLimit).</summary>
        private static bool Pain(Agent a)
        {
            var c = Settings.Current;
            float max = a.HealthLimit;
            return c != null && max > 0f && a.Health / max < c.AiFleeBelowPercent / 100f;
        }

        /// <summary>Postoj zdjety (DisableScriptedMovement), gdy byl. Natywnego odwrotu w drodze nie ruszamy - nadpisze go cudzy odwrot albo handler paniki.</summary>
        private static void Release(Wd w)
        {
            if (!w.Holding) return;
            w.Holding = false;
            try { if (w.A != null && w.A.IsActive()) w.A.DisableScriptedMovement(); } catch { }
        }

        // ------------------------------------------------------------ callbacki misji

        private void WithdrawRemoved(Agent a, AgentState state)
        {
            try
            {
                Wd w;
                if (a == null || !_wd.TryGetValue(a, out w) || w.Done) return;
                w.Done = true;
                bool wpn, sh;
                BrokenFate.Arms(a, out wpn, out sh);                      // przed zrzutem broni z trupa (Mission.OnAgentRemoved)
                WoundedWithdraw.Fate(w.Arrived, state, wpn, sh);          // Arrived: byl za linia (postoj albo po koncu bitwy postoj zdjety)
            }
            catch (Exception e) { RoutHold.Stumble(RoutHold.SiteWithdraw, "RoutKeeper.WithdrawRemoved", e); }
        }

        private void WithdrawDeleted(Agent a)
        {
            try { Wd w; if (a != null && _wd.TryGetValue(a, out w)) w.Done = true; } catch { }
        }

        private void WithdrawToPlayer(Agent a)
        {
            try
            {
                Wd w;
                if (a == null || !_wd.TryGetValue(a, out w) || w.Done) return;
                Release(w);
                w.Done = true;
                WoundedWithdraw.Player++;
            }
            catch { }
        }

        private void ClearWithdraw()
        {
            try { _wd.Clear(); _wList.Clear(); _fCount.Clear(); _bbl = null; _bblInit = false; } catch { }
        }

        /// <summary>RoutKeeper.OnEndMission przed linia: kto wciaz w odwrocie (w drodze / na postoju) i jego bron.</summary>
        private void CloseWithdraw()
        {
            for (int i = 0; i < _wList.Count; i++)
            {
                var w = _wList[i];
                if (w.Done) continue;
                try
                {
                    var a = w.A;
                    if (a == null || !a.IsActive()) continue;
                    bool wpn, sh;
                    BrokenFate.Arms(a, out wpn, out sh);
                    WoundedWithdraw.AtEnd(w.Arrived, wpn, sh);
                }
                catch (Exception e) { RoutHold.Stumble(RoutHold.SiteWithdraw, "RoutKeeper.CloseWithdraw", e); }
            }
        }

        // ------------------------------------------------------------ tick

        private void TickWithdraw()
        {
            var m = Mission;
            if (m == null) return;
            try
            {
                if (m.MissionEnded)
                {
                    if (!_wEnded) { _wEnded = true; EndOfBattle(m); }
                    return;
                }
                var c = Settings.Current;
                if (c == null) return;
                float now = m.CurrentTime;
                _wPathLeft = WoundedWithdraw.PathPerFrame;
                bool done = false;
                for (int i = 0; i < _wList.Count; i++)
                {
                    var w = _wList[i];
                    if (w.Done) { done = true; continue; }
                    try { TickW(w, m, now); }
                    catch (Exception ex)
                    {
                        RoutHold.Stumble(RoutHold.SiteWithdraw, "RoutKeeper.TickWithdraw", ex);
                        if (++w.Fails >= MaxFails)
                        {
                            try { Release(w); } catch { }
                            w.Done = true;                                // jeden zly czlowiek wypada, reszta dziala
                            WoundedWithdraw.Dropped++;
                        }
                    }
                    if (w.Done) done = true;
                }
                if (done) _wList.RemoveAll(WdDone);
            }
            catch (Exception ex) { RoutHold.Stumble(RoutHold.SiteWithdraw, "RoutKeeper.TickWithdraw", ex); }
        }

        private void TickW(Wd w, Mission m, float now)
        {
            var a = w.A;
            if (a == null || !a.IsActive()) { w.Done = true; return; }   // los przyszedl albo przyjdzie z OnAgentRemoved
            if (!a.IsAIControlled) { Release(w); w.Done = true; WoundedWithdraw.Player++; return; }
            var cai = a.CommonAIComponent;
            if (cai == null) { Release(w); w.Done = true; return; }
            if (!MissionTeams.Valid(a.Team)) return;                     // widz pojedynku (Team.Invalid) - rece precz, wraca, gdy znow ma druzyne
            if (cai.IsPanicked || cai.IsRetreating)
            {
                // inna droga: panika gry (142 przejmie z kolejki - zwykle juz w Break), rozkaz odwrotu szyku albo szarza RBM (odwrot komponentu)
                Release(w);
                w.Done = true;
                if (cai.IsPanicked) WoundedWithdraw.Ended(WoundedWithdraw.WhyGame);
                else
                {
                    // cudzy odwrot bez paniki (szarza RBM - po ok. 10 s StopRetreating; rozkaz odwrotu szyku) - po nim ranny wracalby do walki: klucz zdjety,
                    // BrokenMen wycofa go od nowa, gdy cudzy odwrot sie skonczy (Withdraw czeka, dopoki komponent ma IsRetreating)
                    WoundedWithdraw.Ended(WoundedWithdraw.WhyComp);
                    _wd.Remove(a);
                }
                return;
            }
            // morale >= 0.02: pod 0.01 gra sama wolalaby Panic (komponent bez IsRetreating) - panike daje tylko bol, kompania albo rozbity szyk
            if (a.GetMorale() < WoundedWithdraw.MoraleFloor) { a.SetMorale(WoundedWithdraw.MoraleFloor); WoundedWithdraw.Pins++; }
            if (!w.Fought && a.LastMeleeAttackTime > w.Since) { w.Fought = true; WoundedWithdraw.FoughtPeople.Add(a.Index); }

            if (!w.Holding)
            {
                Vec2 pos = a.Position.AsVec2;
                // co klatke: postoj przed promieniem zanikania gry (natywny odwrot + CanAgentRout). Promien gry (do kwadratu) = 0.5-0.6 s x v^2 + 2 x promien
                // kapsuly - u konnego w galopie (12 m/s) ok. 8.9 m; nasz: max(8 m, pierwiastek(0.8 x v^2 + 9)) - zawsze wiekszy, sprawdzany co klatke
                float r2 = WoundedWithdraw.ArriveRadius * WoundedWithdraw.ArriveRadius;
                try
                {
                    float v2 = a.Velocity.AsVec2.LengthSquared;
                    if (v2 >= 0f && !float.IsInfinity(v2)) r2 = Math.Max(r2, 0.8f * v2 + 9f);
                }
                catch { }
                if (w.LastSend > -50f && w.Target.IsValid && pos.IsValid && pos.DistanceSquared(w.Target) <= r2) { Arrive(w, now, false); return; }
                if (w.FirstSend > -50f && now - w.FirstSend > WoundedWithdraw.WalkMax) { Arrive(w, now, true); return; }
            }
            if (now < w.NextEval) return;
            Evaluate(w, m, now);
        }

        /// <summary>Postoj za linia: natywny odwrot zgaszony (przed zanikaniem gry), ScriptedPosition na celu (albo tam, gdzie stoi - inPlace).</summary>
        private void Arrive(Wd w, float now, bool inPlace)
        {
            var a = w.A;
            WorldPosition wp = w.Wp;
            if (inPlace || !wp.IsValid)
            {
                wp = a.GetWorldPosition();
                w.Target = a.Position.AsVec2;
                w.Kind = WoundedWithdraw.KindInPlace;
            }
            try { if (a.IsRetreating()) a.StopRetreating(); } catch { }  // natywne SetRetreatMode(false) i IsRunningAway = false
            a.SetScriptedPosition(ref wp, false, Agent.AIScriptedFrameFlags.None);
            w.Wp = wp;
            w.Holding = true;
            if (!w.Arrived)
            {
                w.Arrived = true;
                WoundedWithdraw.Arrive(a.Index, w.Kind, w.FirstSend > -50f ? now - w.FirstSend : 0f);
            }
            w.NextEval = now + RoutHold.RefreshEvery;
        }

        private void Evaluate(Wd w, Mission m, float now)
        {
            var a = w.A;
            w.NextEval = now + RoutHold.RefreshEvery;
            // szyk rozbity -> panika (jedna regula zlamanego; Break konczy odwrot z tym powodem)
            int why = BrokenFormation(a, now);
            if (why > 0)
            {
                w.Why = why;
                if (!Break(a, MissionTeams.Flee)) { WoundedWithdraw.BreakRefused++; w.Why = 0; }
                return;
            }
            // na postoju: z powrotem w przedniej polowie wlasnego szyku (przed jego srodkiem wzdluz kierunku) - postoj nie utrzymal sie ("wrocil do walki")
            if (w.Holding && !w.Returned)
            {
                var f = a.Formation;
                Vec2 pos = a.Position.AsVec2;
                if (f != null && f.CountOfUnits > 1 && pos.IsValid)
                {
                    Vec2 cen = f.CurrentPosition, dir = f.Direction;
                    if (cen.IsValid && dir.IsValid && dir.LengthSquared > 0.25f && (pos - cen).DotProduct(dir.Normalized()) > 0f)
                    {
                        w.Returned = true;
                        WoundedWithdraw.ReturnedPeople.Add(a.Index);
                    }
                }
            }
            int kind;
            WorldPosition wp;
            Vec2 t = Pick(a, m, out kind, out wp);
            if (kind == KindLimit) { WoundedWithdraw.PathLimit++; w.NextEval = now + 0.5f; return; }   // budzet zapytan - proba za chwile
            bool lost = false;
            if (w.Holding)
            {
                try { lost = (a.GetScriptedFlags() & Agent.AIScriptedFrameFlags.GoToPosition) == 0; } catch { lost = false; }
            }
            if (!t.IsValid)
            {
                WoundedWithdraw.NoSpot++;
                // postoj zostaje na starym miejscu (przywrocony, gdy ktos go zdjal); w drodze - stary cel, ponowienie gdy natywny odwrot zgasl
                if (w.Holding)
                {
                    if (lost && w.Wp.IsValid) { var o = w.Wp; a.SetScriptedPosition(ref o, false, Agent.AIScriptedFrameFlags.None); WoundedWithdraw.Restored++; }
                }
                else if (w.LastSend > -50f && w.Wp.IsValid) Resend(w, now);
                return;                                                   // jeszcze nie wyslany - w szyku walczy dalej, proba za RefreshEvery
            }
            bool moved = !w.Target.IsValid || w.Target.DistanceSquared(t) > WoundedWithdraw.MoveMin * WoundedWithdraw.MoveMin;
            if (w.Holding)
            {
                if (moved || lost)
                {
                    w.Target = t;
                    w.Wp = wp;
                    w.Kind = kind;
                    a.SetScriptedPosition(ref wp, false, Agent.AIScriptedFrameFlags.None);
                    if (lost) WoundedWithdraw.Restored++; else WoundedWithdraw.Moves++;
                }
                return;
            }
            bool first = w.LastSend < -50f;
            if (first || moved)
            {
                w.Target = t;
                w.Wp = wp;
                w.Kind = kind;
            }
            Vec2 my = a.Position.AsVec2;
            if (my.IsValid && my.DistanceSquared(w.Target) <= WoundedWithdraw.ArriveRadius * WoundedWithdraw.ArriveRadius)
            {
                Arrive(w, now, false);                                    // juz za linia - postoj od razu, bez natywnego odwrotu
                return;
            }
            if (first || moved)
            {
                var send = w.Wp;
                a.Retreat(send);                                          // natywne SetRetreatMode - bez Panic, bez OnAgentFleeing, bez okrzyku
                if (first) { WoundedWithdraw.Sends++; w.FirstSend = now; } else WoundedWithdraw.Turns++;
                w.LastSend = now;
                return;
            }
            Resend(w, now);
        }

        /// <summary>W drodze: natywny odwrot zgasl - ponowienie na ten sam cel (najwyzej co RefreshEvery, bo wolane z oceny).</summary>
        private static void Resend(Wd w, float now)
        {
            var a = w.A;
            bool native = true;
            try { native = a.IsRetreating(); } catch { native = true; }
            if (native) return;
            var send = w.Wp;
            a.Retreat(send);
            WoundedWithdraw.Resends++;
            w.LastSend = now;
        }

        /// <summary>Szyk rozbity: 0 = trzyma sie; WhyNoFormation - bez szyku, WhyAlone - nikt poza nim, WhyHalf - co najmniej polowa reszty w PANICE (przelacznik 139).
        /// Liczenie ludzi szyku raz na klatke (cache), bez alokacji (delegat w polu).</summary>
        private int BrokenFormation(Agent a, float now)
        {
            var f = a.Formation;
            if (f == null) return WoundedWithdraw.WhyNoFormation;
            if (now != _fStamp) { _fCount.Clear(); _fStamp = now; }
            FCount fc;
            if (!_fCount.TryGetValue(f, out fc))
            {
                if (_cnt == null)
                    _cnt = x =>
                    {
                        if (x == null || !x.IsActive() || !x.IsHuman) return;
                        _cntAll++;
                        if (SurvivalMode.InPanic(x)) _cntPanic++;
                    };
                _cntAll = _cntPanic = 0;
                f.ApplyActionOnEachUnit(_cnt);
                fc = new FCount { All = _cntAll, Panic = _cntPanic };
                _fCount[f] = fc;
            }
            int others = fc.All - 1;                                      // on sam jest w szyku i nie jest w panice (ROZSADEK)
            if (others <= 0) return WoundedWithdraw.WhyAlone;
            if (fc.Panic >= WoundedWithdraw.BrokenShare * others) return WoundedWithdraw.WhyHalf;
            return 0;
        }

        /// <summary>Cel odwrotu: tyl szyku (Depth/2 + 5 m, potem Depth/2 + 2 m), potem 3 m za najblizszym chorazym strony do 80 m. Vec2.Invalid = brak bezpiecznego
        /// miejsca; kind = KindLimit - budzet zapytan o droge wyczerpany w tej klatce.</summary>
        private Vec2 Pick(Agent a, Mission m, out int kind, out WorldPosition wp)
        {
            kind = -1;
            wp = WorldPosition.Invalid;
            var f = a.Formation;
            if (f != null && f.CountOfUnits > 1)
            {
                Vec2 cen = f.CurrentPosition;
                Vec2 dir = f.Direction;
                if (cen.IsValid && dir.IsValid && dir.LengthSquared > 0.25f)
                {
                    dir = dir.Normalized();
                    float half = Math.Max(0f, f.Depth) * 0.5f;
                    for (int k = 0; k < 2; k++)
                    {
                        Vec2 p = cen - dir * (half + (k == 0 ? WoundedWithdraw.RearExtra : WoundedWithdraw.RearNear));
                        int r = Check(a, m, p, out wp);
                        if (r == CheckLimit) { kind = KindLimit; return Vec2.Invalid; }
                        if (r == CheckOk) { kind = WoundedWithdraw.KindRear; return p; }
                    }
                }
            }
            Agent b = NearestBanner(a, m);
            if (b != null)
            {
                Vec2 bp = b.Position.AsVec2;
                var bf = b.Formation;
                Vec2 bd = bf != null ? bf.Direction : Vec2.Invalid;
                Vec2 p = bd.IsValid && bd.LengthSquared > 0.25f ? bp - bd.Normalized() * WoundedWithdraw.BannerBack : bp;
                int r = Check(a, m, p, out wp);
                if (r == CheckLimit) { kind = KindLimit; return Vec2.Invalid; }
                if (r == CheckOk) { kind = WoundedWithdraw.KindBanner; return p; }
            }
            wp = WorldPosition.Invalid;
            return Vec2.Invalid;
        }

        /// <summary>Cel w granicach pola i 15 m od granicy, bez wroga (nie uciekajacego) w 10 m, z droga od czlowieka (zapytanie natywne z budzetu klatki).</summary>
        private int Check(Agent a, Mission m, Vec2 p, out WorldPosition wp)
        {
            wp = WorldPosition.Invalid;
            if (!p.IsValid || m.Scene == null) return CheckBad;
            if (!m.IsPositionInsideBoundaries(p)) return CheckBad;
            // dla punktu wewnatrz pola - najblizszy punkt granicy (tak samo liczy zanikanie przy granicy CommonAIComponent.OnTickParallel)
            Vec2 edge = m.GetClosestBoundaryPosition(p);
            if (edge.IsValid && edge.DistanceSquared(p) < WoundedWithdraw.EdgeMargin * WoundedWithdraw.EdgeMargin) return CheckBad;
            bool foe = false;
            try
            {
                m.GetNearbyEnemyAgents(p, WoundedWithdraw.SafeRadius, a.Team, _near);
                for (int i = 0; i < _near.Count; i++)
                {
                    var x = _near[i];
                    if (x != null && x.IsActive() && x.IsHuman && !x.IsRunningAway) { foe = true; break; }
                }
            }
            finally { _near.Clear(); }                                    // bez referencji do agentow miedzy ocenami
            if (foe) { WoundedWithdraw.FoeAtSpot++; return CheckBad; }
            if (_wPathLeft <= 0) return CheckLimit;
            _wPathLeft--;
            WoundedWithdraw.PathQueries++;
            WorldPosition from = a.GetWorldPosition();
            WorldPosition dest = new WorldPosition(m.Scene, UIntPtr.Zero, new Vec3(p.x, p.y, a.Position.z), false);
            float dist;
            if (!m.Scene.GetPathDistanceBetweenPositions(ref from, ref dest, 0f, out dist)) return CheckBad;
            wp = dest;
            return CheckOk;
        }

        /// <summary>Najblizszy chorazy strony (BannerBearerLogic.GetFormationBannerBearers - lista gry bez alokacji) w BannerRange; nie on sam, nie uciekajacy.</summary>
        private Agent NearestBanner(Agent a, Mission m)
        {
            if (!_bblInit)
            {
                _bblInit = true;
                try { _bbl = m.GetMissionBehavior<BannerBearerLogic>(); } catch { _bbl = null; }
                WoundedWithdraw.BannerLogic(_bbl != null);
            }
            if (_bbl == null) return null;
            var t = a.Team;
            if (!MissionTeams.Valid(t)) return null;
            Vec2 my = a.Position.AsVec2;
            Agent best = null;
            float bd = WoundedWithdraw.BannerRange * WoundedWithdraw.BannerRange;
            foreach (var f in t.FormationsIncludingEmpty)
            {
                if (f == null || f.CountOfUnits == 0) continue;
                List<Agent> bs = null;
                try { bs = _bbl.GetFormationBannerBearers(f); } catch { bs = null; }
                if (bs == null) continue;
                for (int i = 0; i < bs.Count; i++)
                {
                    var b = bs[i];
                    if (b == null || ReferenceEquals(b, a) || !b.IsActive() || b.Banner == null || b.IsRunningAway) continue;
                    float d = b.Position.AsVec2.DistanceSquared(my);
                    if (d < bd) { bd = d; best = b; }
                }
            }
            return best;
        }

        /// <summary>Koniec bitwy (pierwsza klatka z MissionEnded): stan "na koniec" do linii (w drodze / na postoju, bron), potem postoj zdjety; idacy po stronie
        /// zwyciezcow - StopRetreating (ta sama regula co gra dla spanikowanych zwyciezcow, CommonAI.cs OnTickParallel), przegrani ida dalej. Wszyscy wypadaja
        /// z ticku (Done) - los po koncu bitwy nie jest liczony (przegrany idacy moze zniknac przy celu - to juz nie "uszedl z pola" w bitwie).</summary>
        private void EndOfBattle(Mission m)
        {
            MissionResult res = null;
            try { res = m.MissionResult; } catch { res = null; }
            for (int i = 0; i < _wList.Count; i++)
            {
                var w = _wList[i];
                if (w.Done) continue;
                try
                {
                    var a = w.A;
                    if (a == null || !a.IsActive()) continue;
                    bool wpn, sh;
                    BrokenFate.Arms(a, out wpn, out sh);
                    WoundedWithdraw.AtEnd(w.Arrived, wpn, sh);
                    w.Done = true;
                    if (w.Holding) { Release(w); continue; }
                    if (res == null || w.LastSend < -50f) continue;
                    var t = a.Team;
                    bool mine = t != null && (t.IsPlayerTeam || t.IsPlayerAlly);
                    bool won = (res.PlayerVictory && mine) || (res.PlayerDefeated && !mine);
                    if (won && a.IsRetreating()) a.StopRetreating();
                }
                catch (Exception e) { RoutHold.Stumble(RoutHold.SiteWithdraw, "RoutKeeper.EndOfBattle", e); }
            }
        }
    }
}
