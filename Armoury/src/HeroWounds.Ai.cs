using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>408 (Jeff 11.11 "rob 4" - rany i blizny bohaterow; PLAN-396 commit 408, raport-08 pkt 3.4) - RANNY WODZ WIEZIE SIE DO TWIERDZY (AI).
    /// Klucz Hero Wounds Lords Rest. Postfiks priorytet 60 na CampaignEventDispatcher.AiHourlyTick(MobileParty, PartyThinkParams): po ArmsFirst (VeryLow 100)
    /// i zjezdzie pogrzebu 407 (130), przed wetami 50 / 48 / 45 i przed Last (LordEscort, CrossingLaw, SiegeWalls2.ThinkPost, CanonWarWatch).
    ///
    /// Dotyczy: partia lorda AI (nie gracz, nie rod gracza), ktorej WODZ ma czynny wpis w ksiedze ran (Serious albo Grave).
    /// Pomija (zadnej zmiany ocen): armia, bitwa, oblezenie (oblega albo stoi w oblezonej osadzie), DoNotChangeBehavior, morze, glod, wodz czynnego
    /// oblezenia fabuly ROT (SiegePhantomLift.StoryLeaderOf), wodz potrzebny fabule (StoryGuard.Needed) na mniej niz 14 dni przed jego wydarzeniem.
    /// Dzialanie (wzor ArmsFirst.ThinkPostfix): opcje szukajace bitwy - oblezenie, szturm, rabunek, obrona cudzej osady, poscig / zaczepka partii, kazda opcja
    /// zwolujaca armie - dostaja -1; obrona osady wlasnego rodu zostaje, a gdy jest ZAGROZONA (ArmsFirst.IsOwnDefend), partia nie dostaje wyjazdu.
    /// Cel: partia stojaca w nieoblezonej twierdzy swojej frakcji zostaje w niej; inaczej najblizsza nieoblezona twierdza RODU, a gdy rod zadnej nie ma
    /// (albo zadna nie ma drogi ladowej) - najblizsza twierdza frakcji. Ocena GoToSettlement (droga ladowa) = max(ArmsFirst.FloorScore, 1.1 x najlepsza inna
    /// opcja po naszych -1); wyniku &lt;= -1 (cudza blokada) nie podnosimy, twierdza z taka blokada nie jest kandydatem. Cel trzymany 24 h (bez wahadla).
    /// Zadnych doplat: zold i jedzenie normalnie. Wodz armii na lozu - bez zmian. Bez stanu w zapisie; pamiec czyszczona w Reset i w dobie.</summary>
    internal static partial class HeroWounds
    {
        private const float AiEdge = 1.1f;          // ponad najlepsza inna opcje (PLAN-396: 1.1, nie 1.2 jak ArmsFirst)
        private const double AiNeedDays = 14.0;     // wodz potrzebny fabule: mniej niz tyle dni do wydarzenia = bez wyjazdu
        private const int AiRoadChecks = 3;         // ile najblizszych twierdz sprawdzamy droga ladowa (model odleglosci)
        private const double AiHoldHours = 24.0;    // tyle godzin trzymamy raz wybrana twierdze

        private sealed class AiPick { public Settlement Fort; public double H; public bool Own; }

        private static string _aiHook = "nie probowano";
        private static readonly Dictionary<MobileParty, AiPick> _aiPick = new Dictionary<MobileParty, AiPick>();
        private static readonly Dictionary<string, string> _aiSaid = new Dictionary<string, string>(StringComparer.Ordinal);   // partia -> cel z ostatniej linii logu
        private static readonly HashSet<string> _aiDay = new HashSet<string>(StringComparer.Ordinal);                           // partie z ocena wyjazdu tej doby
        private static readonly List<(AIBehaviorData, float)> _aiCopy = new List<(AIBehaviorData, float)>(32);
        private static readonly List<KeyValuePair<Settlement, float>> _aiCand = new List<KeyValuePair<Settlement, float>>(16);
        private static int _aiOrders, _aiInside, _aiOff, _aiNoFort, _aiOwnDef, _aiSea, _aiStarve, _aiStory, _aiNeed, _aiVeto, _aiSessOrders;

        private static void AiReset()
        {
            _aiPick.Clear(); _aiSaid.Clear(); _aiDay.Clear(); _aiCopy.Clear(); _aiCand.Clear();
            _aiOrders = _aiInside = _aiOff = _aiNoFort = _aiOwnDef = _aiSea = _aiStarve = _aiStory = _aiNeed = _aiVeto = _aiSessOrders = 0;
        }

        /// <summary>Z Wire (OnSessionLaunched, raz na proces): postfiks 60 na metode zadeklarowana w CampaignEventDispatcher. Stan w linii startu ran.</summary>
        private static void AiWire()
        {
            try
            {
                var m = AccessTools.DeclaredMethod(typeof(CampaignEventDispatcher), "AiHourlyTick", new[] { typeof(MobileParty), typeof(PartyThinkParams) });
                if (m == null) { _aiHook = "BRAK (nie ma metody)"; Log.Info(Tag + "BLAD latki 408: nie ma metody CampaignEventDispatcher.AiHourlyTick."); return; }
                _harmony.Patch(m, postfix: new HarmonyMethod(typeof(HeroWounds), nameof(AiRestPostfix)) { priority = 60 });
                bool ok = false;
                var info = Harmony.GetPatchInfo(m);
                if (info != null)
                    foreach (var p in info.Postfixes)
                        if (p != null && p.PatchMethod != null && p.PatchMethod.DeclaringType == typeof(HeroWounds) && p.PatchMethod.Name == nameof(AiRestPostfix)) ok = true;
                _aiHook = ok ? "wpieta" : "BRAK (latka nie weszla)";
            }
            catch (Exception e) { _aiHook = "BLAD (" + e.GetType().Name + ")"; Log.Error("HeroWounds.AiWire", e); }
        }

        /// <summary>Opcja szukajaca bitwy albo zwolujaca armie. Obrona osady wlasnego rodu zostaje (takze niezagrozonej - patrol przy swoich).</summary>
        private static bool AiIsBattle(in AIBehaviorData d, Clan clan)
        {
            if (d.WillGatherArmy) return true;
            switch (d.AiBehavior)
            {
                case AiBehavior.BesiegeSettlement:
                case AiBehavior.AssaultSettlement:
                case AiBehavior.RaidSettlement:
                case AiBehavior.GoAroundParty:
                case AiBehavior.EngageParty:
                    return true;
                case AiBehavior.DefendSettlement:
                    var st = d.Party as Settlement;
                    return st == null || clan == null || st.OwnerClan != clan;
                default:
                    return false;
            }
        }

        /// <summary>Najblizsza (linia prosta) nieoblezona twierdza z listy, do ktorej jest droga ladowa; sprawdzamy najwyzej AiRoadChecks najblizszych.</summary>
        private static Settlement AiNearest(MobileParty party, MBReadOnlyList<Town> fiefs, IFaction f, HashSet<Settlement> blocked, Func<Vec2, bool> bw, bool pBeyond, out float road)
        {
            road = 0f;
            if (fiefs == null || fiefs.Count == 0) return null;
            var c = Campaign.Current;
            var model = c != null && c.Models != null ? c.Models.MapDistanceModel : null;
            if (model == null) return null;
            var pos = party.GetPosition2D;
            _aiCand.Clear();
            for (int i = 0; i < fiefs.Count; i++)
            {
                var st = fiefs[i] != null ? fiefs[i].Settlement : null;
                if (st == null || !st.IsFortification || st.IsUnderSiege || st.MapFaction != f) continue;
                if (blocked != null && blocked.Contains(st)) continue;
                if (bw != null) { try { if (bw(st.GetPosition2D) != pBeyond) continue; } catch { } }
                _aiCand.Add(new KeyValuePair<Settlement, float>(st, pos.DistanceSquared(st.GetPosition2D)));
            }
            if (_aiCand.Count == 0) return null;
            _aiCand.Sort((x, y) => x.Value.CompareTo(y.Value));
            Settlement best = null;
            for (int i = 0; i < _aiCand.Count && i < AiRoadChecks; i++)
            {
                float lr;
                float d = model.GetDistance(party, _aiCand[i].Key, false, MobileParty.NavigationType.Default, out lr);
                if (CartGuard.NoLandRoad(d)) continue;
                best = _aiCand[i].Key; road = d;
                break;
            }
            _aiCand.Clear();
            return best;
        }

        /// <summary>Twierdza spoczynku: ta, w ktorej partia stoi (swojej frakcji, bez oblezenia); inaczej najblizsza rodu, potem frakcji. Null = brak.</summary>
        private static Settlement AiFort(MobileParty party, Clan clan, HashSet<Settlement> blocked, out bool own, out float road)
        {
            own = false; road = 0f;
            var f = party.MapFaction;
            if (f == null || clan == null) return null;
            var at = party.CurrentSettlement;
            if (at != null && at.IsFortification && !at.IsUnderSiege && at.MapFaction == f && (blocked == null || !blocked.Contains(at)))
            { own = at.OwnerClan == clan; return at; }
            double now = CampaignTime.Now.ToHours;
            AiPick pick;
            if (_aiPick.TryGetValue(party, out pick) && pick.Fort != null && now - pick.H < AiHoldHours && !pick.Fort.IsUnderSiege && pick.Fort.MapFaction == f
                && (blocked == null || !blocked.Contains(pick.Fort)))
            { own = pick.Own; road = -1f; return pick.Fort; }
            var bw = ArmsFirst.Beyond();
            bool pBeyond = false;
            if (bw != null) { try { pBeyond = bw(party.GetPosition2D); } catch { bw = null; } }
            var fort = AiNearest(party, clan.Fiefs, f, blocked, bw, pBeyond, out road);
            own = fort != null;
            if (fort == null)
            {
                var k = f as Kingdom;
                if (k != null) fort = AiNearest(party, k.Fiefs, f, blocked, bw, pBeyond, out road);
            }
            if (fort == null) { _aiPick.Remove(party); return null; }
            if (pick == null) _aiPick[party] = pick = new AiPick();
            pick.Fort = fort; pick.H = now; pick.Own = own;
            return fort;
        }

        /// <summary>Postfiks (priorytet 60) CampaignEventDispatcher.AiHourlyTick: partia lorda AI z wodzem na lozu - opcje bitwy -1, wyjazd do twierdzy
        /// (regula w opisie pliku). Pusta ksiega = jedno porownanie. Nie rzuca.</summary>
        public static void AiRestPostfix(MobileParty party, PartyThinkParams partyThinkParams)
        {
            if (_n == 0) return;
            try
            {
                if (party == null || partyThinkParams == null || !party.IsLordParty || party.IsMainParty) return;
                var lh = party.LeaderHero;
                Row r;
                if (lh == null || !_bed.TryGetValue(lh, out r)) return;
                var s = Settings.Current;
                if (s == null || !s.HeroWoundsEnabled || !s.HeroWoundsLordsRest) return;
                var clan = party.ActualClan;
                if (lh == Hero.MainHero || clan == null || clan == Clan.PlayerClan) return;
                if (party.Army != null || party.MapEvent != null || party.BesiegedSettlement != null || party.SiegeEvent != null) return;
                var at = party.CurrentSettlement;
                if (at != null && at.IsUnderSiege) return;
                if (partyThinkParams.DoNotChangeBehavior) return;
                if (party.IsCurrentlyAtSea) { _aiSea++; return; }
                if (party.Party.IsStarving) { _aiStarve++; return; }   // najpierw jedzenie - droga gry
                if (SiegePhantomLift.StoryLeaderOf(party) != null) { _aiStory++; return; }
                if (StoryGuard.Needed(lh))
                {
                    double need = StoryGuard.NextNeedDay(lh);
                    if (need >= 0 && need - CanonCompanions.StoryDay() < AiNeedDays) { _aiNeed++; return; }
                }
                var scores = partyThinkParams.AIBehaviorScores;
                if (scores == null) return;
                // odczyt PRZED naszymi -1: zagrozona osada wlasnego rodu i twierdze zablokowane przez latki przed nami (wynik <= -1, np. CrossingLaw)
                float ownDef = -1f;
                HashSet<Settlement> blocked = null;
                bool unready = ArmsFirst.Unready(party);   // p9 (postfiks 100, przed nami) dal juz -1 opcjom bitwy - to nie jest blokada twierdzy
                _aiCopy.Clear();
                for (int i = 0; i < scores.Count; i++)
                {
                    var e = scores[i];
                    _aiCopy.Add(e);
                    if (ArmsFirst.IsOwnDefend(in e.Item1, clan)) { if (e.Item2 > ownDef) ownDef = e.Item2; }
                    if (e.Item2 > -1f) continue;
                    var bs = e.Item1.Party as Settlement;
                    if (bs == null || !bs.IsFortification) continue;
                    if (unready && AiIsBattle(in e.Item1, clan)) continue;
                    if (blocked == null) blocked = new HashSet<Settlement>();
                    blocked.Add(bs);
                }
                // opcje bitwy -1 (po kopii - SetBehaviorScore zmienia elementy listy)
                for (int i = 0; i < _aiCopy.Count; i++)
                {
                    if (!(_aiCopy[i].Item2 > -1f)) continue;
                    var d = _aiCopy[i].Item1;
                    if (!AiIsBattle(in d, clan)) continue;
                    partyThinkParams.SetBehaviorScore(in d, -1f);
                    _aiOff++;
                }
                _aiCopy.Clear();
                if (ownDef >= 0f) { _aiOwnDef++; return; }   // obrona zagrozonej osady rodu - ta opcja zostaje, bez wyjazdu (jak ArmsFirst)
                bool own; float road;
                var fort = AiFort(party, clan, blocked, out own, out road);
                if (fort == null) { _aiNoFort++; return; }
                int mine = -1; float other = 0f;
                for (int i = 0; i < scores.Count; i++)
                {
                    var d = scores[i].Item1;
                    if (mine < 0 && d.AiBehavior == AiBehavior.GoToSettlement && !d.WillGatherArmy && d.NavigationType == MobileParty.NavigationType.Default && ReferenceEquals(d.Party, fort))
                    { mine = i; continue; }
                    if (scores[i].Item2 > other) other = scores[i].Item2;
                }
                float nv = Math.Max(ArmsFirst.FloorScore, AiEdge * other);
                if (mine >= 0)
                {
                    float cur = scores[mine].Item2;
                    if (!(cur > -1f)) { _aiVeto++; return; }   // cudza blokada - nie podnosimy
                    if (cur < nv) { var d = scores[mine].Item1; partyThinkParams.SetBehaviorScore(in d, nv); }
                    else nv = cur;
                }
                else
                {
                    var a = new AIBehaviorData(fort, AiBehavior.GoToSettlement, MobileParty.NavigationType.Default, false, false, false);
                    partyThinkParams.AddBehaviorScore((a, nv));
                }
                bool here = at == fort;
                _aiOrders++; _aiSessOrders++;
                if (here) _aiInside++;
                string pid = party.StringId ?? "?";
                _aiDay.Add(pid);
                if (s.HeroWoundsLog)
                {
                    string code = (here ? "w:" : "do:") + fort.StringId, last;
                    if (!_aiSaid.TryGetValue(pid, out last) || last != code)
                    {
                        _aiSaid[pid] = code;
                        Log.Info(Tag + "partia " + pid + " (wodz " + Nm(lh) + " " + r.Id + ", " + GradeName(r.Grade) + ", zostalo " + Math.Max(1, r.Left) + " dni loza) "
                                 + (here ? "zostaje w twierdzy " : "jedzie do twierdzy ") + fort.StringId + " (" + (own ? "rodu" : "frakcji")
                                 + (here ? "" : road >= 0f ? ", droga " + road.ToString("F0", Inv) : ", cel z ostatniej doby")
                                 + ") | ocena " + nv.ToString("F2", Inv) + " = max(" + ArmsFirst.FloorScore.ToString("0.0#", Inv) + ", " + AiEdge.ToString("0.0#", Inv)
                                 + " x najlepsza inna " + other.ToString("F2", Inv) + ").");
                    }
                }
            }
            catch (Exception e) { Stumble("AiRestPostfix", e); }
        }

        /// <summary>Ogon linii doby "Rany (HW)".</summary>
        private static string AiDayText()
        {
            int skips = _aiOff + _aiNoFort + _aiOwnDef + _aiSea + _aiStarve + _aiStory + _aiNeed + _aiVeto;
            if (_aiOrders == 0 && skips == 0) return " | partie do twierdzy 0";
            return " | partie do twierdzy " + _aiDay.Count + " (ocen wyjazdu " + _aiOrders + ", z tego stojac w murach " + _aiInside + "; opcje bitwy -1: " + _aiOff
                   + "; bez wyjazdu: brak twierdzy " + _aiNoFort + ", obrona osady rodu " + _aiOwnDef + ", cel zawetowany " + _aiVeto + "; pominiete: morze " + _aiSea
                   + ", glod " + _aiStarve + ", wodz oblezenia fabuly " + _aiStory + ", przed wydarzeniem fabuly " + _aiNeed + "; sesja ocen " + _aiSessOrders + ")";
        }

        /// <summary>Koniec doby ran: liczniki doby od zera, pamiec celu i logu tylko dla partii z wodzem nadal na lozu.</summary>
        private static void AiDayEnd()
        {
            try
            {
                _aiOrders = _aiInside = _aiOff = _aiNoFort = _aiOwnDef = _aiSea = _aiStarve = _aiStory = _aiNeed = _aiVeto = 0;
                _aiDay.Clear();
                if (_aiPick.Count == 0 && _aiSaid.Count == 0) return;
                if (_n == 0) { _aiPick.Clear(); _aiSaid.Clear(); return; }
                List<MobileParty> drop = null;
                foreach (var kv in _aiPick)
                {
                    var p = kv.Key;
                    var lh = p != null && p.IsActive ? p.LeaderHero : null;
                    if (lh == null || !_bed.ContainsKey(lh)) (drop ?? (drop = new List<MobileParty>())).Add(p);
                }
                if (drop != null) foreach (var p in drop) _aiPick.Remove(p);
                if (_aiSaid.Count > 0)
                {
                    HashSet<string> keep = null;
                    foreach (var h in _bed.Keys)
                    {
                        var p = h != null ? h.PartyBelongedTo : null;
                        if (p != null && p.LeaderHero == h && p.StringId != null) (keep ?? (keep = new HashSet<string>(StringComparer.Ordinal))).Add(p.StringId);
                    }
                    List<string> gone = null;
                    foreach (var k in _aiSaid.Keys) if (keep == null || !keep.Contains(k)) (gone ?? (gone = new List<string>())).Add(k);
                    if (gone != null) foreach (var k in gone) _aiSaid.Remove(k);
                }
            }
            catch (Exception e) { Stumble("AiDayEnd", e); }
        }
    }
}
