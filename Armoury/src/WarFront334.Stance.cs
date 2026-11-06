using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace Armoury
{
    /// <summary>
    /// 334c - POSTAWA PARTII W WOJNIE KANONU (klucz CanonWarStance334c).
    /// DOWOD (test 334b, 81 dob): linia "Zelazni" od drugiej doby "z opcja rabunku 0 (opcji 0)" prawie codziennie (doba 109522 przed pierwszym tickiem BK: opcji 152);
    /// postfiks Z0 liczy kazda opcje RaidSettlement - gra jej w ogole nie podala. Korona: 13 okien 28 dob bez oblezenia, armia 1 doba na 40 dob wojny.
    /// PRZYCZYNA: BannerKings BKDiplomacyBehavior.TickKingdoms (co dobe, takze po wczytaniu) - dla kazdego krolestwa AI: 1 twierdza albo najsilniejszy wrog >= 1.5 x
    /// sily (Kingdom.CurrentTotalStrength) -> wszystkie partie wojenne Defensive; sila >= 1.5 x najsilniejszego wroga -> Aggressive. Gra (AiMilitaryBehavior
    /// .FindBestTargetAndItsValueForFaction) przy Defensive nie podaje opcji oblezenia ani rabunku (takze z zebraniem armii), przy Aggressive - opcji obrony; Aggressive
    /// x1.2 do oblezen i rabunkow, Defensive x1.2 do obrony. Wyspy (9.3 tys. wobec 35 tys. Polnocy) i korona (13 tys.) - Defensive; Polnoc - Aggressive: regula BK
    /// "jedno krolestwo przeciw najsilniejszemu wrogowi" nie widzi koalicji, a najezdzcy z Wysp z natury sa slabsi.
    /// ZMIANA (postfiks TickKingdoms + start sesji; tylko kampania kanonu; krolestwo gracza bez zmian - BK go tez nie rusza):
    ///  - strony wojny kanonu (CanonWarBalance.SideOf != 0): ta sama regula BK (prog 1.5, wyjatek 1 twierdzy), ale sila wlasna = suma CurrentTotalStrength krolestw tej
    ///    strony, wrog = max(suma krolestw drugiej strony w wojnie z tym krolestwem, najsilniejszy inny wrog). Przy 0.9-0.95 obie strony - Neutral.
    ///  - Zelazne Wyspy w wojnie z Polnoca: Neutral (postawa gry bez BK) - kanon: najezdzca rabuje i oblega wybrzeze mimo slabosci; obrone przed atakiem na Wyspy daje
    ///    i tak mysl gry (Neutral nie odcina zadnej opcji).
    ///  - inne krolestwa (Dorne, Dolina, Essos, Wolni Ludzie...) - wynik BK bez zmian.
    /// Linia "Balans wojny (334): postawa (334c)" raz na dobe. Nic nie tworzy - tylko MobileParty.SetPartyObjective (to samo pole, ktore ustawia BK).
    /// </summary>
    internal static partial class WarFront334
    {
        private sealed class StanceRec { public int Bk, Ours, Fiefs, Parties; public double Own, Enemy; public string Rule; }
        private static readonly Dictionary<string, StanceRec> _stance = new Dictionary<string, StanceRec>(StringComparer.Ordinal);
        private static int _dStanceSet, _dStanceRuns;
        private static string _wStance = "BRAK";
        private static bool _stanceHookTried;

        private static void ResetStance() { _stance.Clear(); ZeroStanceDay(); }
        private static void ZeroStanceDay() { _dStanceSet = 0; _dStanceRuns = 0; }

        /// <summary>Czysta regula BK (autotest): 0 Neutral, 1 Defensive, 2 Aggressive. fiefs - twierdze krolestwa; own / enemy - sila wlasna i wroga.</summary>
        internal static int StanceRule(int fiefs, double own, double enemy)
        {
            int r = 0;
            if (fiefs == 1 || enemy >= own * 1.5) r = 1;
            if (own >= enemy * 1.5) r = 2;
            return r;
        }

        private static string StanceName(int o) { return o == 1 ? "D" : o == 2 ? "A" : "N"; }

        private static void EnsureStanceHook()
        {
            if (_stanceHookTried) return;
            _stanceHookTried = true;
            try
            {
                var t = AccessTools.TypeByName("BannerKings.Behaviours.Diplomacy.BKDiplomacyBehavior");
                var m = t != null ? AccessTools.DeclaredMethod(t, "TickKingdoms", Type.EmptyTypes) : null;
                if (t == null) _wStance = "bez BK (brak BKDiplomacyBehavior)";
                else if (m == null) _wStance = "BRAK (TickKingdoms)";
                else
                {
                    new Harmony("com.jeff.armoury.stance334c").Patch(m, postfix: new HarmonyMethod(typeof(WarFront334), nameof(StancePostfix)) { priority = Priority.Last });
                    _wStance = "ok";
                }
            }
            catch (Exception e) { Stumble("EnsureStanceHook", e); _wStance = "BLAD (" + e.GetType().Name + ")"; }
        }

        /// <summary>Postfiks BKDiplomacyBehavior.TickKingdoms (priorytet Last). Nie rzuca.</summary>
        public static void StancePostfix()
        {
            try { StanceApply("BK"); } catch (Exception e) { Stumble("StancePostfix", e); }
        }

        private static float Cts(Kingdom k) { try { return Math.Max(0f, k.CurrentTotalStrength); } catch { return 0f; } }

        /// <summary>Postawa krolestw wojny kanonu i Wysp wedlug reguly 334c; ustawia tylko partie z inna postawa. Kampania nie kanonu / klucz NIE - nic.</summary>
        internal static void StanceApply(string why)
        {
            var c = Campaign.Current;
            if (c == null || !c.GameStarted || !StanceOn) return;
            _dStanceRuns++;
            IFaction player = null;
            try { player = Clan.PlayerClan != null ? Clan.PlayerClan.MapFaction : null; } catch { }
            var kings = new List<Kingdom>();
            foreach (var k in Kingdom.All) if (k != null && !k.IsEliminated) kings.Add(k);
            var north = FindKingdom(NorthId);
            foreach (var k in kings)
            {
                try
                {
                    if (k == player) continue;
                    int side = CanonWarBalance.SideOf(k);
                    bool iron = k.StringId == IronId && north != null && k.IsAtWarWith(north);
                    if (side == 0 && !iron) { _stance.Remove(k.StringId); continue; }
                    var r = new StanceRec();
                    int fiefs = k.Fiefs != null ? k.Fiefs.Count : 0;
                    r.Fiefs = fiefs;
                    // regula BK (do linii): krolestwo przeciw najsilniejszemu wrogowi
                    double own1 = Cts(k), maxEnemy = 0;
                    foreach (var ek in FactionHelper.GetEnemyKingdoms(k)) { if (ek == null) continue; double v = Cts(ek); if (v > maxEnemy) maxEnemy = v; }
                    r.Bk = StanceRule(fiefs, own1, maxEnemy);
                    if (iron)
                    {
                        r.Ours = 0; r.Own = own1; r.Enemy = maxEnemy; r.Rule = "najezdzca";
                    }
                    else
                    {
                        double own = 0, opp = 0, other = 0;
                        foreach (var o in kings) if (CanonWarBalance.SideOf(o) == side) own += Cts(o);
                        foreach (var ek in FactionHelper.GetEnemyKingdoms(k))
                        {
                            if (ek == null) continue;
                            int es = CanonWarBalance.SideOf(ek);
                            if (es != 0 && es != side) opp += Cts(ek);
                            else { double v = Cts(ek); if (v > other) other = v; }
                        }
                        double enemy = Math.Max(opp, other);
                        r.Own = own; r.Enemy = enemy; r.Rule = "koalicja";
                        r.Ours = StanceRule(fiefs, own, enemy);
                    }
                    var want = (MobileParty.PartyObjective)r.Ours;
                    if (k.WarPartyComponents != null)
                        foreach (var wp in k.WarPartyComponents)
                        {
                            var mp = wp != null ? wp.MobileParty : null;
                            if (mp == null) continue;
                            r.Parties++;
                            if (mp.Objective != want) { mp.SetPartyObjective(want); _dStanceSet++; }
                        }
                    _stance[k.StringId] = r;
                }
                catch (Exception e) { Stumble("StanceApply.kingdom", e); }
            }
        }

        /// <summary>Linia doby: postawa krolestw (BK -> 334c), sila wlasna / wroga, partie obronne Wysp i korony (stan na teraz).</summary>
        private static void StanceLine(int today)
        {
            var sb = new StringBuilder(800);
            sb.Append(Tag).Append("postawa (334c) | dzien ").Append(today).Append(" | klucz ").Append(Key01(StanceOn)).Append(", latka ").Append(_wStance)
              .Append(", przebiegow ").Append(_dStanceRuns).Append(", partii przestawionych ").Append(_dStanceSet);
            string[] ids = { NorthId, CrownId, ReachId, IronId, DragonstoneId };
            foreach (var id in ids)
            {
                var k = FindKingdom(id);
                if (k == null) continue;
                int n = 0, d = 0, a = 0;
                try
                {
                    if (k.WarPartyComponents != null)
                        foreach (var wp in k.WarPartyComponents)
                        {
                            var mp = wp != null ? wp.MobileParty : null;
                            if (mp == null) continue;
                            n++;
                            if (mp.Objective == MobileParty.PartyObjective.Defensive) d++; else if (mp.Objective == MobileParty.PartyObjective.Aggressive) a++;
                        }
                }
                catch { }
                StanceRec r;
                sb.Append(" | [").Append(id).Append("] ").Append(Nm(k)).Append(": ");
                if (_stance.TryGetValue(id, out r))
                    sb.Append("BK ").Append(StanceName(r.Bk)).Append(" -> ").Append(StanceName(r.Ours)).Append(" (").Append(r.Rule).Append(", sila ").Append(F0(r.Own)).Append(" / wrog ").Append(F0(r.Enemy))
                      .Append(", twierdz ").Append(r.Fiefs).Append(')');
                else sb.Append("BK bez zmian");
                sb.Append(", partii ").Append(n).Append(" obronnych ").Append(d).Append(" ofensywnych ").Append(a);
            }
            Log.Info(sb.Append('.').ToString());
        }
    }
}
