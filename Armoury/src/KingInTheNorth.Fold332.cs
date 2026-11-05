using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// PACZKA 332 - OGOLNE WSTAPIENIE KROLESTWA W INNE (szablon unii 272 - KingInTheNorth.Merge, bez zmian w samej unii 272). Uzywa tej samej maszynerii: latka armii
    /// (prefiks Clan.LeaveKingdomInternal na czas Moving), migawka rodow / armii / oblezen / jencow gracza, decyzje rady zrodla anulowane, wojny tylko zrodla - pokoj,
    /// sojusze zrodla zakonczone, pieniadze (Bank, skarbiec, portfele gry, pozyczki od miast) do celu z kontrola, rody w kolejnosci 272 (najemnicy AI, wasale, gracz,
    /// rod panujacy), kariera najemnika BK, armie zachowane, oblezenia znowu, stan modow wedlug id krolestwa, zrodlo zniszczone tylko bez rodow.
    /// Linie z prefiksem wolajacego (np. "Reach do korony (332): "). Wynik w FoldResult (liczby do kontroli i testu wolajacego). Stan 272 (_st, _clans, test 272) bez zmian.
    /// </summary>
    internal static partial class KingInTheNorth
    {
        internal sealed class FoldResult
        {
            public int Clans, Moved, Destroyed, Mercs, Fiefs0, Fiefs1, Villages0, Parties0, Parties1, Men0, Men1, Armies, ArmiesOk, CohOk, PrisTot0, PrisTot1, Peace;
            public long Control = long.MinValue;
            public bool PlayerOk = true, PrisOk = true, SrcGone, CareerOk = true, AllyGone = true;
            public string Role = "-", Career = "-", Seat = "", RulerId = "", RulerName = "";
            public readonly List<string> ClanIds = new List<string>(), FortIds = new List<string>(), MercIds = new List<string>(), PeaceWith = new List<string>();
        }

        /// <summary>Wojny krolestw zrodla, ktorych cel nie ma (bez siebie nawzajem) - 332.</summary>
        internal static List<Kingdom> SourceOnlyWars(Kingdom src, Kingdom dst)
        {
            var l = new List<Kingdom>();
            foreach (var f in src.FactionsAtWarWith.ToList())
            {
                var k = f as Kingdom;
                if (k != null && !k.IsEliminated && k != dst && !dst.IsAtWarWith(k)) l.Add(k);
            }
            return l;
        }

        /// <summary>332: wojna domowa Diplomacy w krolestwie (jak RiverCivilWar 272).</summary>
        internal static bool CivilWarIn(Kingdom k) { return k != null && RiverCivilWar(k); }

        /// <summary>332: czy teraz nie jest bezpiecznie (gracz w bitwie / misji, partia albo twierdza zrodla w MapEvent). null = bezpiecznie. Wojny tylko zrodla - nie blokuja
        /// (wstapienie konczy je pokojem).</summary>
        internal static string FoldUnsafeWhy(Kingdom src, string srcName)
        {
            var mp = MobileParty.MainParty;
            if (mp != null && (mp.MapEvent != null || mp.SiegeEvent != null || (TaleWorlds.CampaignSystem.Encounters.PlayerEncounter.Current != null && TaleWorlds.CampaignSystem.Encounters.PlayerEncounter.EncounterSettlement == null)))
                return "gracz w bitwie, oblezeniu albo spotkaniu";
            try { if (TaleWorlds.MountAndBlade.Mission.Current != null) return "gracz w misji"; } catch { }
            foreach (var c in src.Clans)
            {
                if (c == null || c.IsEliminated) continue;
                foreach (var w in c.WarPartyComponents)
                {
                    var p = w != null ? w.MobileParty : null;
                    if (p != null && p.IsActive && p.MapEvent != null) return "partia " + srcName + " w bitwie (" + Nm(p) + ")";
                }
                foreach (var t in c.Fiefs)
                    if (t != null && t.Settlement != null && t.Settlement.Party != null && t.Settlement.Party.MapEvent != null) return "twierdza " + srcName + " w bitwie (" + Nm(t.Settlement) + ")";
            }
            return null;
        }

        /// <summary>332: wstapienie krolestwa src w dst (jedna transakcja). tag - prefiks linii, srcName / dstName - nazwy w liniach (po polsku).</summary>
        internal static FoldResult Fold(Kingdom src, Kingdom dst, string tag, string srcName, string dstName)
        {
            Fields();
            var res = new FoldResult();
            var pc = Clan.PlayerClan;
            // ---- 0. migawka
            var snaps = new List<ClanSnap>();
            var ruler = src.RulingClan;
            res.RulerId = ruler != null ? ruler.StringId ?? "" : ""; res.RulerName = Nm(ruler);
            int totF = 0, totV = 0, totP = 0, totM = 0, totG = 0;
            foreach (var c in src.Clans.ToList())
            {
                if (c == null || c.IsEliminated) continue;
                var s = new ClanSnap { C = c, Merc = c.IsUnderMercenaryService, Player = c == pc, Ruler = c == ruler, Inf = c.Influence, Debt = c.DebtToKingdom, Mult = c.MercenaryAwardMultiplier, Stay = c.ShouldStayInKingdomUntil };
                s.Fiefs = c.Fiefs.Count; s.Villages = c.Villages.Count;
                s.Men = Men(c, out s.Parties, out s.Garrison);
                totF += s.Fiefs; totV += s.Villages; totP += s.Parties; totM += s.Men; totG += s.Garrison; if (s.Merc) res.Mercs++;
                snaps.Add(s);
            }
            float renewal = -1f;
            try { renewal = Campaign.Current.KingdomManager.PlayerMercenaryServiceNextRenewalDay; } catch { }
            int prisTot0; var pris0 = PlayerPrisoners(out prisTot0);
            var armies = new List<ArmySnap>();
            _keep.Clear();
            var seen = new HashSet<Army>();
            foreach (var s in snaps)
                foreach (var w in s.C.WarPartyComponents)
                {
                    var p = w != null ? w.MobileParty : null;
                    var a = p != null ? p.Army : null;
                    if (a == null || !seen.Add(a)) continue;
                    var x = new ArmySnap { A = a, Leader = a.LeaderParty, Cohesion = a.Cohesion, Type = a.ArmyType, Obj = a.AiBehaviorObject, Besieged = a.LeaderParty != null ? a.LeaderParty.BesiegedSettlement : null };
                    foreach (var m in a.Parties) { x.Members.Add(m); if (m.AttachedTo != null) x.Attached.Add(m); }
                    armies.Add(x); _keep.Add(a);
                }
            var sieges = new List<SiegeSnap>();
            foreach (var s in snaps)
                foreach (var w in s.C.WarPartyComponents)
                {
                    var p = w != null ? w.MobileParty : null;
                    if (p == null || !p.IsActive || p.IsMainParty || p.AttachedTo != null || p.MapEvent != null || p.BesiegerCamp == null || p.BesiegedSettlement == null) continue;
                    sieges.Add(new SiegeSnap { P = p, S = p.BesiegedSettlement, Nav = p.DesiredAiNavigationType });
                }
            foreach (var s in snaps) if (s.Player) res.Role = s.Merc ? "najemnik" : "wasal";
            var fortSet = new HashSet<string>(StringComparer.Ordinal);
            foreach (var s in snaps)
            {
                res.ClanIds.Add(s.C.StringId);
                if (s.Merc) res.MercIds.Add(s.C.StringId);
                foreach (var t in s.C.Fiefs) if (t != null && t.Settlement != null && fortSet.Add(t.Settlement.StringId)) res.FortIds.Add(t.Settlement.StringId);
            }
            res.Seat = ruler != null && ruler.HomeSettlement != null ? ruler.HomeSettlement.StringId ?? "" : "";
            res.Clans = snaps.Count; res.Fiefs0 = totF; res.Villages0 = totV; res.Parties0 = totP; res.Men0 = totM; res.PrisTot0 = prisTot0;
            res.Career = "kariera BK gracza nie dotyczy (" + res.Role + ")";
            Log.Info(tag + "WSTAPIENIE start | " + srcName + ": rodow " + snaps.Count + " (najemnikow " + res.Mercs + ", gracz " + res.Role + "), twierdz " + totF + ", wsi " + totV + ", partii " + totP
                     + ", ludzi " + totM + " (zalogi " + totG + "), armii " + armies.Count + ", oblezen " + sieges.Count + " | wladca " + res.RulerName + " (" + (res.RulerId.Length > 0 ? res.RulerId : "-") + ") | "
                     + dstName + " przed: rodow " + dst.Clans.Count + ", twierdz " + dst.Fiefs.Count + " | jency gracza " + prisTot0 + " (bohaterowie: " + (pris0.Count > 0 ? string.Join(",", pris0.ToArray()) : "-") + ").");
            int bk0 = MercContract.BkDownUnion;
            Moving = true;
            try
            {
                // ---- 1. decyzje rady zrodla
                try
                {
                    foreach (var d in src.UnresolvedDecisions.ToList())
                    {
                        bool inv = false;
                        try { inv = d.DetermineChooser().Leader.IsHumanPlayerCharacter || d.DetermineSupporters().Any(x => x.IsPlayer); } catch { }
                        src.RemoveDecision(d);
                        CampaignEventDispatcher.Instance.OnKingdomDecisionCancelled(d, inv);
                        Log.Info(tag + "decyzja rady " + srcName + " anulowana - " + d.GetType().Name + " (proponujacy " + Nm(d.ProposerClan) + ", wplywy proponujacego bez zwrotu - jak anulowanie gry).");
                    }
                }
                catch (Exception e) { Stumble("Fold.Decisions", e); }
                // ---- 2. wojny tylko zrodla - pokoj (cel ich nie prowadzi; wojny celu rody dostaja przy wstapieniu - AdjustFactionStancesForClanJoiningKingdom)
                try
                {
                    foreach (var x in SourceOnlyWars(src, dst))
                    {
                        MakePeaceAction.Apply(src, x);
                        bool ok = !src.IsAtWarWith(x);
                        if (ok) { res.Peace++; res.PeaceWith.Add(x.StringId); }
                        Log.Info(tag + "pokoj " + srcName + " z " + Nm(x) + " (" + x.StringId + ") - wojna tylko " + srcName + ", " + dstName + " jej nie prowadzi | pokoj " + (ok ? "TAK" : "NIE") + ".");
                    }
                }
                catch (Exception e) { Stumble("Fold.Wars", e); }
                // ---- 3. sojusze zrodla
                try
                {
                    var ab = Campaign.Current.GetCampaignBehavior<IAllianceCampaignBehavior>();
                    if (ab != null)
                        foreach (var ak in src.AlliedKingdoms.ToList())
                        {
                            if (ak == null) continue;
                            ab.EndAlliance(src, ak);
                            Log.Info(tag + "sojusz " + srcName + " z " + Nm(ak) + " (" + ak.StringId + ") zakonczony " + (ab.IsAllyWithKingdom(src, ak) ? "NIE" : "TAK") + (ak == dst ? " (teraz jedno krolestwo)" : "") + ".");
                        }
                }
                catch (Exception e) { Stumble("Fold.Alliances", e); }
                // ---- 4. pieniadze
                res.Control = FoldMoney(src, dst, tag, srcName, dstName);
                // ---- 5. rody: jak unia 272 (gracz po najemnikach i wasalach, przed rodem panujacym z lennami)
                var ps0 = snaps.FirstOrDefault(s => s.Player);
                var rs0 = snaps.FirstOrDefault(s => !s.Player && s.Ruler);
                bool playerLate = ps0 != null && rs0 != null && rs0.Fiefs > 0;
                if (ps0 != null) ps0.Note += playerLate ? " (kolejnosc: po najemnikach i wasalach, przed rodem panujacym - zadania od lordow zostaja)" : " (kolejnosc: pierwszy - rod panujacy bez lenn)";
                var order = new List<ClanSnap>();
                if (!playerLate) order.AddRange(snaps.Where(s => s.Player));
                order.AddRange(snaps.Where(s => !s.Player && s.Merc));
                order.AddRange(snaps.Where(s => !s.Player && !s.Merc && !s.Ruler));
                if (playerLate) order.AddRange(snaps.Where(s => s.Player));
                order.AddRange(snaps.Where(s => !s.Player && !s.Merc && s.Ruler));
                EnsureArmyPatch();
                foreach (var s in order) FoldMoveClan(s, src, dst, renewal, tag, srcName, dstName, res);
                foreach (var s in order) if (!s.Ok && !s.C.IsEliminated && s.C.Kingdom == src) FoldMoveClan(s, src, dst, renewal, tag, srcName, dstName, res);
                RestoreDetached();
                // ---- 6. armie i oblezenia
                FoldArmies(armies, dst, tag, dstName, res);
                foreach (var g in sieges)
                {
                    try
                    {
                        if (g.P == null || !g.P.IsActive || g.P.MapEvent != null || g.P.AttachedTo != null || g.S == null || g.P.BesiegerCamp == null) continue;
                        if (!g.S.MapFaction.IsAtWarWith(g.P.MapFaction)) continue;
                        g.P.SetMoveBesiegeSettlement(g.S, g.Nav != MobileParty.NavigationType.None ? g.Nav : g.P.NavigationCapability);
                        Log.Info(tag + "oblezenie " + Nm(g.S) + " - " + Nm(g.P) + " dalej oblega (rozkaz oblezenia znowu po postoju ChangeKingdomAction).");
                    }
                    catch (Exception e) { Stumble("Fold.Siege", e); }
                }
                foreach (var s in snaps)
                    foreach (var w in s.C.WarPartyComponents)
                    {
                        var p = w != null ? w.MobileParty : null;
                        if (p != null && p.IsActive && !p.IsMainParty && p.MapEvent == null) p.Ai.RethinkAtNextHourlyTick = true;
                    }
                // ---- 7. stan modow wedlug id krolestwa
                try
                {
                    int deals = MercContract.MoveDeals(src.StringId, dst.StringId);
                    int rents = CrownRents.MoveKingdom(src.StringId, dst.StringId);
                    int captives, rewards; Ransom178.MoveKingdom(src.StringId, dst.StringId, out captives, out rewards);
                    Log.Info(tag + "stan wedlug krolestwa -> " + dstName + " | umowy najemnikow 185: " + deals + " | sluzba rent 180: " + rents + " | wielcy jency 178: " + captives + ", nagrody 178: " + rewards + ".");
                }
                catch (Exception e) { Stumble("Fold.ReKey", e); }
                // ---- 8. koniec krolestwa (tylko bez rodow - DestroyKingdomAction zniszczylby pozostale)
                int left = src.Clans.Count(c => c != null && !c.IsEliminated);
                if (left == 0)
                {
                    try { src.RulingClan = null; DestroyKingdomAction.Apply(src); }
                    catch (Exception e) { Stumble("Fold.Destroy", e); }
                }
                else Log.Info(tag + srcName + " NIE zniszczone - zostalo rodow " + left + " (DestroyKingdomAction zniszczylby je) - sprawdz linie rodow wyzej.");
            }
            finally
            {
                Moving = false;
                RestoreDetached();
                _keep.Clear();
            }
            // ---- 9. kontrola
            foreach (var s in snaps)
            {
                if (s.C.IsEliminated) { res.Destroyed++; continue; }
                if (s.C.Kingdom == dst) res.Moved++;
                int pp, gg; res.Men1 += Men(s.C, out pp, out gg); res.Parties1 += pp; res.Fiefs1 += s.C.Fiefs.Count;
            }
            int prisTot1; var pris1 = PlayerPrisoners(out prisTot1);
            res.PrisTot1 = prisTot1;
            res.PrisOk = prisTot1 == prisTot0 && string.Join(",", pris0.ToArray()) == string.Join(",", pris1.ToArray());
            res.SrcGone = src.IsEliminated;
            try { var ab = Campaign.Current.GetCampaignBehavior<IAllianceCampaignBehavior>(); res.AllyGone = ab == null || !ab.IsAllyWithKingdom(dst, src); } catch { res.AllyGone = false; }
            var ps = snaps.FirstOrDefault(s => s.Player);
            res.PlayerOk = ps == null || (pc.Kingdom == dst && pc.IsUnderMercenaryService == ps.Merc && (!ps.Merc || pc.MercenaryAwardMultiplier == ps.Mult));
            Log.Info(tag + "WSTAPIENIE ZAKONCZONE | " + srcName + " zniszczone " + (src.IsEliminated ? "TAK" : "NIE") + " | rodow przeniesionych " + res.Moved + "/" + snaps.Count + ", zniszczonych " + res.Destroyed
                     + " | lenna " + res.Fiefs1 + "/" + totF + ", partie " + res.Parties1 + "/" + totP + ", ludzie " + res.Men1 + "/" + totM + " | " + dstName + " po: rodow " + dst.Clans.Count + ", twierdz " + dst.Fiefs.Count
                     + ", armii " + dst.Armies.Count + " | jency gracza przed " + prisTot0 + " / po " + prisTot1 + " " + (res.PrisOk ? "TAK" : "NIE") + " | gracz " + res.Role + " -> "
                     + (pc != null && pc.Kingdom != null ? (pc.IsUnderMercenaryService ? "najemnik " : "wasal ") + pc.Kingdom.StringId : "-")
                     + " | zadatki BK wstrzymane (ta sama sluzba) " + (MercContract.BkDownUnion - bk0) + " | potkniecia 272/332 " + _stumbles + ".");
            return res;
        }

        private static long FoldMoney(Kingdom src, Kingdom dst, string tag, string srcName, string dstName)
        {
            long taken = 0, added = 0;
            var sb = new StringBuilder(400);
            try
            {
                long w0 = src.KingdomBudgetWallet;
                long paid = w0 > 0 ? CrownBorrow.RepayOnFall(src, w0) : 0;
                if (paid > 0) src.KingdomBudgetWallet -= (int)paid;
                taken += paid; added += paid;
                int rest = src.KingdomBudgetWallet;
                long nv0 = dst.KingdomBudgetWallet;
                src.KingdomBudgetWallet -= rest;
                dst.KingdomBudgetWallet += rest;
                long got = (long)dst.KingdomBudgetWallet - nv0;
                CrownIncome.ShiftSnapshot(dst, got, 'k');   // skarb przejety, nie wplyw dnia (jak 272)
                taken += rest; added += got;
                double loan = CrownBorrow.MoveLoan(src, dst);
                sb.Append("skarbiec ").Append(srcName).Append(' ').Append(w0).Append(" -> splata Banku ").Append(paid).Append(", do skarbca ").Append(dstName).Append(' ').Append(rest)
                  .Append(" | dlug Banku przeniesiony ").Append((long)Math.Round(loan));
                int mw = src.MercenaryWallet;
                if (mw != 0 && _setMercWallet != null)
                {
                    int nm0 = dst.MercenaryWallet;
                    _setMercWallet.Invoke(src, new object[] { 0 });
                    _setMercWallet.Invoke(dst, new object[] { nm0 + mw });
                    taken += mw; added += (long)dst.MercenaryWallet - nm0;
                }
                int tw = src.TributeWallet;
                if (tw != 0)
                {
                    int nt0 = dst.TributeWallet;
                    src.TributeWallet = 0; dst.TributeWallet = nt0 + tw;
                    taken += tw; added += (long)dst.TributeWallet - nt0;
                }
                int cw = src.CallToWarWallet, ncw0 = dst.CallToWarWallet;
                string ctw;
                if (cw == 0) ctw = "0";
                else if (cw > 0 && ncw0 >= 0)
                {
                    int rc0 = cw;
                    src.CallToWarWallet = 0; dst.CallToWarWallet = ncw0 + cw;
                    Measure31.CtwOther(src, rc0); Measure31.CtwOther(dst, ncw0);
                    taken += cw; added += (long)dst.CallToWarWallet - ncw0;
                    ctw = cw + " przeniesiony";
                }
                else ctw = cw + " zostaje (pole netto: " + srcName + " " + cw + ", " + dstName + " " + ncw0 + " - jak przy kazdym upadku krolestwa)";
                sb.Append(" | portfele gry: najemnicy ").Append(mw).Append(_setMercWallet == null && mw != 0 ? " (BRAK settera - zostaje)" : "").Append(", danina ").Append(tw).Append(", wezwania ").Append(ctw);
                double kl = TownFolk.KlMoveKingdom(src.StringId, dst.StringId);
                sb.Append(" | pozyczki od miast (korona2) przeniesione ").Append((long)Math.Round(kl));
            }
            catch (Exception e) { Stumble("Fold.Money", e); sb.Append(" | BLAD ").Append(e.GetType().Name); }
            long diff = taken - added;
            Log.Info(tag + sb.ToString() + " | kontrola: zdjete " + taken + " - dopisane " + added + " = " + diff + ".");
            return diff;
        }

        private static void FoldMoveClan(ClanSnap s, Kingdom src, Kingdom dst, float renewal, string tag, string srcName, string dstName, FoldResult res)
        {
            var c = s.C;
            if (c == null || c.IsEliminated || c.Kingdom == dst) return;
            try
            {
                bool raw = SetInfluenceRaw(c, 0f);
                if (s.Merc) ChangeKingdomAction.ApplyByJoinFactionAsMercenary(c, dst, s.Stay, s.Mult, false);
                else ChangeKingdomAction.ApplyByJoinToKingdom(c, dst, s.Stay, false);
                if (raw) SetInfluenceRaw(c, s.Inf);
                else
                {
                    float d = s.Inf - c.Influence;
                    if (d != 0f) ChangeClanInfluenceAction.Apply(c, d);
                    s.Note += " (wplywy przez akcje gry - brak pola _influence)";
                }
                c.DebtToKingdom = s.Debt;
                if (s.Player && s.Merc && renewal > 0f) Campaign.Current.KingdomManager.PlayerMercenaryServiceNextRenewalDay = renewal;
                s.Ok = c.Kingdom == dst;
                if (s.Merc && s.Ok)
                {
                    bool bkOk;
                    string bk = BkCareerMove(c, src, dst, out bkOk);
                    s.Note += " | " + bk;
                    if (s.Player) { res.CareerOk = bkOk; res.Career = bk; }
                }
            }
            catch (Exception e) { Stumble("FoldMoveClan", e); s.Note += " (wyjatek " + e.GetType().Name + ")"; }
            int pp, gg; int men = Men(c, out pp, out gg);
            string kind = s.Player ? (s.Merc ? "gracz-najemnik" : "gracz-wasal") : s.Merc ? "najemnik" : s.Ruler ? "wladca " + srcName : "wasal";
            Log.Info(tag + "rod " + Nm(c) + " (" + c.StringId + ") " + kind + " -> " + dstName + " " + (s.Ok ? "TAK" : "NIE") + " | lenna " + c.Fiefs.Count + "/" + s.Fiefs + ", wsie " + c.Villages.Count + "/" + s.Villages
                     + ", partie " + pp + "/" + s.Parties + ", ludzie " + men + "/" + s.Men + ", zalogi " + gg + "/" + s.Garrison + " | wplywy " + s.Inf.ToString("0.#", Inv) + " -> " + c.Influence.ToString("0.#", Inv)
                     + " | dlug wobec krolestwa " + s.Debt + " -> " + c.DebtToKingdom + " | glowa " + Nm(c.Leader) + (s.Merc ? " | mnoznik " + s.Mult + " -> " + c.MercenaryAwardMultiplier : "") + s.Note + ".");
        }

        private static void FoldArmies(List<ArmySnap> list, Kingdom dst, string tag, string dstName, FoldResult res)
        {
            res.Armies = list.Count; res.ArmiesOk = 0; res.CohOk = 0;
            foreach (var x in list)
            {
                try
                {
                    string how; Army a = x.A;
                    bool alive = x.Leader != null && x.Leader.IsActive && x.Leader.LeaderHero != null && x.Leader.LeaderHero.IsAlive && !x.Leader.LeaderHero.IsPrisoner;
                    if (_armyPatched && a != null && a.LeaderParty == x.Leader && x.Leader != null && x.Leader.Army == a)
                    {
                        if (a.Kingdom != dst) a.Kingdom = dst;
                        how = "zachowana pod " + dstName;
                    }
                    else if (!alive) { Log.Info(tag + "armia " + Nm(x.Leader) + " NIE (wodz nieaktywny, martwy albo w niewoli) | partii " + x.Members.Count + "."); continue; }
                    else if (x.Leader.Army != null) { Log.Info(tag + "armia " + Nm(x.Leader) + " NIE (wodz juz w innej armii) | partii " + x.Members.Count + "."); continue; }
                    else
                    {
                        var lc = x.Leader.LeaderHero.Clan;
                        float inf = lc != null ? lc.Influence : 0f;
                        a = new Army(dst, x.Leader, x.Type);
                        if (x.Obj != null) a.AiBehaviorObject = x.Obj;
                        foreach (var m in x.Members)
                        {
                            if (m == null || m == x.Leader || !m.IsActive || m.Army != null || m.MapFaction != dst) continue;
                            m.Army = a;
                            if (x.Attached.Contains(m)) m.AttachedTo = x.Leader;
                        }
                        a.Cohesion = x.Cohesion;
                        if (lc != null && !SetInfluenceRaw(lc, inf)) { float d = inf - lc.Influence; if (d != 0f) ChangeClanInfluenceAction.Apply(lc, d); }
                        try { CampaignEventDispatcher.Instance.OnArmyCreated(a); } catch (Exception e) { Stumble("FoldArmies.OnArmyCreated", e); }
                        how = "odtworzona pod " + dstName;
                    }
                    int k = 0; foreach (var m in x.Members) if (m != null && m.Army == a) k++;
                    bool coh = Math.Abs(a.Cohesion - x.Cohesion) < 0.01f;
                    if (k == x.Members.Count) res.ArmiesOk++;
                    if (coh) res.CohOk++;
                    var bs = a.LeaderParty != null ? a.LeaderParty.BesiegedSettlement : null;
                    Log.Info(tag + "armia " + Nm(x.Leader) + " " + how + " | partii " + k + "/" + x.Members.Count + ", spojnosc " + x.Cohesion.ToString("0.#", Inv) + " -> " + a.Cohesion.ToString("0.#", Inv)
                             + " | oblezenie " + (x.Besieged != null ? Nm(x.Besieged) : "-") + " -> " + (bs != null ? Nm(bs) : "-") + " | krolestwo armii " + (a.Kingdom != null ? a.Kingdom.StringId : "-") + ".");
                }
                catch (Exception e) { Stumble("FoldArmies", e); }
            }
        }

        /// <summary>332: wodz (do linii testu).</summary>
        internal static string Name(object o) { return Nm(o); }

        /// <summary>332: settlement krolestwa (bez zmian - pomocnik testu).</summary>
        internal static bool HeldBy(Settlement s, string kingdomId)
        {
            try { return s != null && s.MapFaction != null && s.MapFaction.StringId == kingdomId; } catch { return false; }
        }
    }
}
