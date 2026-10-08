using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// WOZY NIE UTYKAJA W MIASTACH (paczka 130; autotest 07.10 09:11, 40 dob: 39 z 53 zatkanych wsi ma woz w miescie od 8 do 40 dob -
    /// w 10 przykladach co 5 dob wylacznie Wickenden i Lord Hewett's Town; gra nie wystawia wsi nowego wozu, dopoki stary istnieje).
    /// PRZYCZYNA (kod gry, BK i dane mapy ROT): woz wsi po sprzedazy czeka w miescie na rozkaz gry "do domu"
    /// (VillagerCampaignBehavior.ThinkAboutSendingInsideVillagersToTheirHomeVillage, co godzine z szansa 20%) - rozkaz idzie przez
    /// MobileParty.SetMoveGoToSettlement(dom, Default). BannerKings ma na tej metodzie prefiks (AiDecisionTracePatches
    /// .SetMoveGoToSettlementPostfix -> GuardSettlementMove), ktory ODRZUCA rozkaz, gdy MapDistanceModel.GetDistance(partia, osada,
    /// Default) >= 50000. Ten dystans gra liczy od "najblizszego wejscia" sciany siatki drog, na ktorej partia stoi (tablica sciana ->
    /// osada w pamieci drog ROT-Map\ModuleData\DistanceCaches\settlements_distance_cache_Default.bin); gdy sciany nie ma w tablicy,
    /// zwraca 1e8 (DefaultMapDistanceModel i NavalDLCMapDistanceModel tak samo). Pamiec drog ROT (28.07) jest starsza niz siatka mapy:
    /// siatka ma 17864 scian, tablica konczy sie na scianie 17841 - 22 sciany dociete pozniej leza przy Griffin's Roost, Lord Hewett's
    /// Town, Pinkmaiden, Acorn Hall, kryjowce i Wickenden, a BRAMY Wickenden (sciana 17862), Lord Hewett's Town (17848) i zamku Acorn
    /// Hall (17858) stoja wlasnie na nich (jedyne 3 z 1065 osad). Partia w osadzie stoi na bramie, wiec KAZDY rozkaz "jedz do osady"
    /// z tych trzech osad BK odrzuca: woz wjechal (rozkaz wydany we wsi przechodzi), sprzedal i stoi na zawsze z celem = to miasto.
    /// LATKA (VillageCartLeaveTown, wylacznik): postfiks na BK GuardSettlementMove - gdy BK odrzucil rozkaz WOZU WSI stojacego w osadzie,
    /// pytamy o te sama droge tablice osada -> osada (MapDistanceModel.GetDistance(osada, cel, Default) - ta sama pamiec drog, z ktorej
    /// gra liczy droge z osady): jest droga ladowa (< 50000, prog BK) - rozkaz przechodzi; nie ma - zostaje odrzucony jak u BK. Lordow,
    /// karawan i wozow w polu nie ruszamy (tylko liczniki w logu). Ten sam rozkaz, ktory i tak wydaje gra - zloto i towar bez zmian.
    /// BEZPIECZNIK (VillageCartTownMaxDays, 0 = wylaczony): raz na dobe woz wsi, ktory stoi w (nieoblezonym, bez bitwy) miescie od
    /// N dob, dostaje rozkaz gry "do domu" (MoveVillagersToSettlementWithBestNavigationType - ten sam, ktory gra wydaje co godzine);
    /// przez straznika BK przechodzi ta sama regula latki. VillageCartLeaveTown = false gasi CALA paczke (latke i bezpiecznik) - gra
    /// jak przed paczka, zostaje tylko linia w logu (recenzja: wylacznik = d126 bit w bit). Niesprzedany towar jedzie z wozem do domu i wraca na
    /// targ z nastepnym kursem (MarketCarts.Choose wycenia caly ladunek wozu); nic nie powstaje i nic nie znika.
    /// PO RoadMemoryFix (pamiec drog uzupelniona u zrodla po wczytaniu mapy) BK nie odrzuca juz rozkazow z tych 3 osad - latka i bezpiecznik zostaja
    /// jako siatka bezpieczenstwa (oczekiwane: przepuszczone 0); linia dnia liczy dodatkowo ROZNE partie (lordowie, karawany) odrzucane przez BK,
    /// powod odrzucenia i najdluzsze stanie lorda / karawany w osadzie z odrzuceniem (tylko log).
    /// </summary>
    internal static class CartTownExit
    {
        /// <summary>Prog BK (GuardSettlementMove): dystans od tej wartosci = "brak drogi ladowej".</summary>
        internal const float BkLimit = 50000f;

        private static MethodInfo _guard, _move;
        private static bool _wired;
        private static bool _force;                               // trwa rozkaz bezpiecznika (tylko licznik "w tym rozkazy bezpiecznika"; przepuszcza go ta sama regula latki)
        private static bool _scanned;                             // raz po wczytaniu: lista osad z brama poza pamiecia drog
        private static readonly HashSet<string> _errSites = new HashSet<string>();
        private static readonly object _lock = new object();

        // ------------------------------------------------------------ stan (tylko pamiec; czyszczony w konstruktorze ArmouryBehavior)
        private static readonly Dictionary<MobileParty, int> _seen = new Dictionary<MobileParty, int>();   // woz wsi w miescie -> doba pierwszej obserwacji
        private static readonly HashSet<MobileParty> _sent = new HashSet<MobileParty>();                    // woz odeslany juz przez bezpiecznik (i nadal w miescie)
        private static int _stumbles, _stumblesAll;

        // ------------------------------------------------------------ liczniki doby (straznik BK)
        private static int _gV, _gVPass, _gVForce, _gVDead, _gVRoad, _gVOff, _gLord, _gCaravan, _gOther;
        private static readonly Dictionary<string, int> _passAt = new Dictionary<string, int>();
        private static readonly Dictionary<string, int> _otherAt = new Dictionary<string, int>();

        // inne partie (lordowie, karawany, inne): ROZNE partie doby, powod odrzucenia i stanie w osadzie z odrzuceniem - tylko log (autotest 5 07.10:
        // po latce wozow straznik BK odrzucal coraz wiecej rozkazow karawan i lordow w tych samych 3 osadach, osobno na wyspach - linia ma pokazac, ilu
        // ich jest i czy stoja na zawsze; po RoadMemoryFix w 3 osadach ma byc 0)
        private static readonly HashSet<MobileParty> _dLord = new HashSet<MobileParty>(), _dCaravan = new HashSet<MobileParty>(), _dOther = new HashSet<MobileParty>();
        private static int _rFace, _rSea, _rNoRoad, _rElse;
        private sealed class Stand { public Settlement At; public double First, Last; public bool Lord; public int How; }
        private static readonly Dictionary<MobileParty, Stand> _stand = new Dictionary<MobileParty, Stand>();   // lord / karawana w osadzie -> od kiedy BK odrzuca jej rozkazy (w tej osadzie)
        internal const double StandWindowDays = 2.0;                                                            // "stoi z odrzuceniem" = nadal w tej osadzie i odrzucona w ostatnich 2 dobach

        internal static void Reset()
        {
            _seen.Clear(); _sent.Clear(); _errSites.Clear(); _stand.Clear();
            _scanned = false; _force = false; _stumblesAll = 0;
            ClearDay();
            IslandRoads.Reset();                                                     // latka wysp: stan w pamieci (ten sam straznik BK, ta sama linia dnia)
        }

        private static void ClearDay()
        {
            _gV = _gVPass = _gVForce = _gVDead = _gVRoad = _gVOff = _gLord = _gCaravan = _gOther = 0;
            _passAt.Clear(); _otherAt.Clear();
            _dLord.Clear(); _dCaravan.Clear(); _dOther.Clear();
            _rFace = _rSea = _rNoRoad = _rElse = 0;
            _stumbles = 0;
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumblesAll++;
            if (_errSites.Add(where)) Log.Error(where, e);
        }

        private static string Name(Settlement s)
        {
            try { return s != null && s.Name != null ? s.Name.ToString() : "?"; } catch { return "?"; }
        }

        private static void Bump(Dictionary<string, int> d, string k) { int n; d.TryGetValue(k, out n); d[k] = n + 1; }

        private static string Top(Dictionary<string, int> d, int k)
        {
            if (d.Count == 0) return "";
            return " [" + string.Join(", ", d.OrderByDescending(x => x.Value).Take(k).Select(x => x.Key + " " + x.Value)) + (d.Count > k ? ", ..." : "") + "]";
        }

        // ------------------------------------------------------------ latka: straznik BK
        /// <summary>
        /// Postfiks na BannerKings.Patches.AiDecisionTracePatches.GuardSettlementMove(MobileParty, Settlement, ref NavigationType).
        /// Gdy BK odrzucil rozkaz (false) wozowi wsi stojacemu w osadzie, a pamiec drog osada -> cel ma droge ladowa - rozkaz przechodzi.
        /// Lordowie, karawany i inne partie: najpierw latka wysp (IslandRoads.OnRejected - partia ze statkami ladem i morzem, karawana bez
        /// statkow do miasta osiagalnego ladem), potem liczniki. Woz w polu i cel naprawde bez drogi - wynik BK bez zmian.
        /// </summary>
        public static void GuardPostfix(MobileParty __0, Settlement __1, ref MobileParty.NavigationType __2, ref bool __result)
        {
            if (__result) { IslandRoads.NoteAccepted(__0); return; }               // BK przepuscil - nic (ogromna wiekszosc wywolan; znacznik dla latki wysp)
            Rejected(__0, __1, ref __2, true, ref __result);
        }

        /// <summary>Ten sam postfiks, gdy straznik BK nie ma parametru ref NavigationType (inna wersja BK) - bez zmiany drogi.</summary>
        public static void GuardPostfixNoNav(MobileParty __0, Settlement __1, ref bool __result)
        {
            if (__result) { IslandRoads.NoteAccepted(__0); return; }
            var nav = MobileParty.NavigationType.Default;
            Rejected(__0, __1, ref nav, false, ref __result);
        }

        private static void Rejected(MobileParty __0, Settlement __1, ref MobileParty.NavigationType nav, bool canNav, ref bool __result)
        {
            // latka wysp (IslandRoads, wylacznik IslandRoadsFix) - przed liczeniem i poza zamkiem (moze wydac karawanie rozkaz zastepczy);
            // wozy wsi zostaja dla latki ponizej
            int how = IslandRoads.None;
            if (__0 != null && __1 != null && !__0.IsVillager) how = IslandRoads.OnRejected(__0, __1, ref nav, canNav, ref __result);
            lock (_lock)                                                            // rozkazy AI moga isc z kilku watkow - liczniki i slowniki pod zamkiem (tylko odrzucone przez BK)
            {
                try
                {
                    var party = __0;
                    var to = __1;
                    if (party == null || to == null) return;
                    var at = party.CurrentSettlement;
                    if (!party.IsVillager)
                    {
                        bool lord = party.IsLordParty, car = !lord && party.IsCaravan;
                        if (lord) { _gLord++; _dLord.Add(party); } else if (car) { _gCaravan++; _dCaravan.Add(party); } else { _gOther++; _dOther.Add(party); }
                        Bump(_otherAt, at != null ? Name(at) : "w polu");
                        try
                        {
                            // powod (tylko log): to samo, co liczy NavalDLCMapDistanceModel.GetDistance(partia, osada, Default) przed progiem BK
                            if (party.IsCurrentlyAtSea) _rSea++;                             // partia na morzu (takze w wsi z portem po przyplynieciu) - droga ladowa = 1e8
                            else
                            {
                                var m = Campaign.Current.Models.MapDistanceModel;
                                var ent = m.GetClosestEntranceToFace(party.CurrentNavigationFace, MobileParty.NavigationType.Default).Item1;
                                if (ent == null) _rFace++;                                   // sciana bez wpisu w pamieci drog (to, co naprawia RoadMemoryFix)
                                else
                                {
                                    float dd = m.GetDistance(ent, to, false, false, MobileParty.NavigationType.Default);
                                    if (!(dd >= 0f && dd < BkLimit)) _rNoRoad++; else _rElse++;   // brak drogi ladowej do celu (np. z wyspy na lad) / inne
                                }
                            }
                        }
                        catch (Exception e) { Stumble("CartTownExit.GuardPostfix (powod)", e); }
                        if (at != null && (lord || car))
                        {
                            double now = CampaignTime.Now.ToDays;
                            Stand st;
                            if (!_stand.TryGetValue(party, out st) || st.At != at) { st = new Stand { At = at, First = now, Lord = lord }; _stand[party] = st; }
                            st.Last = now;
                            st.How = how;                                                // co z tym rozkazem zrobila latka wysp (tylko log)
                        }
                        return;
                    }
                    _gV++;
                    if (at == null || party.IsCurrentlyAtSea) { _gVRoad++; return; }      // woz w drodze - jak u BK (obecny rozkaz zostaje)
                    var s = Settings.Current;
                    if (s == null || !s.VillageCartLeaveTown) { _gVOff++; return; }       // wylacznik calej paczki: wynik BK bez zmian (bezpiecznik tez stoi)
                    // ta sama droga, ktora gra liczy dla partii w osadzie (DistanceHelper.FindClosestDistanceFromSettlementToSettlementForMobileParty):
                    // pamiec drog osada -> osada, nie sciana, na ktorej stoi brama
                    float d = Campaign.Current.Models.MapDistanceModel.GetDistance(at, to, false, false, MobileParty.NavigationType.Default);
                    if (!(d >= 0f && d < BkLimit)) { _gVDead++; return; }                // z tej osady naprawde nie ma drogi ladowej (albo NaN) - jak u BK
                    __result = true;
                    _gVPass++;
                    if (_force) _gVForce++;
                    Bump(_passAt, Name(at));
                }
                catch (Exception e) { Stumble("CartTownExit.GuardPostfix", e); }   // blad = wynik BK bez zmian
            }
        }

        // ------------------------------------------------------------ bezpiecznik + linia dnia
        /// <summary>Dlaczego woz stal w miescie (stan przed rozkazem bezpiecznika).</summary>
        private enum Why { Stale, Hold, AiOff, TargetTown, WaitsHome, Other }

        private static Why Reason(MobileParty cart, Settlement at, Settlement home)
        {
            if (cart.Ai != null && cart.Ai.IsDisabled) return Why.AiOff;
            if (cart.DefaultBehavior == AiBehavior.Hold || cart.ShortTermBehavior == AiBehavior.Hold) return Why.Hold;
            if (cart.TargetSettlement == home) return Why.WaitsHome;                  // rozkaz do domu jest, a woz czeka (zagrozenie pod brama - gra chowa woz w miescie)
            if (cart.TargetSettlement == at)
            {
                try
                {
                    var entrance = Campaign.Current.Models.MapDistanceModel.GetClosestEntranceToFace(cart.CurrentNavigationFace, MobileParty.NavigationType.Default).Item1;
                    if (entrance == null) return Why.Stale;                           // cel = to miasto, a brama poza pamiecia drog - BK zjadal kazdy rozkaz gry
                }
                catch { }
                return Why.TargetTown;
            }
            return Why.Other;
        }

        /// <summary>Rozkaz gry "do domu" (ten sam, ktory gra wydaje co godzine wozom w miescie); przez straznika BK przechodzi regula latki (_force - tylko licznik).</summary>
        private static void SendHome(MobileParty cart, Settlement home)
        {
            _force = true;
            try
            {
                var vcb = Campaign.Current != null ? Campaign.Current.GetCampaignBehavior<VillagerCampaignBehavior>() : null;
                if (_move != null && vcb != null) _move.Invoke(vcb, new object[] { cart, home });
                else cart.SetMoveGoToSettlement(home, MobileParty.NavigationType.Default, false);
            }
            finally { _force = false; }
        }

        /// <summary>Raz po wczytaniu: osady, ktorych brama stoi na scianie spoza pamieci drog (Default) - BK nie wypusci z nich rozkazem "jedz".</summary>
        private static string StaleGates()
        {
            var names = new List<string>();
            int n = 0;
            var m = Campaign.Current.Models.MapDistanceModel;
            foreach (var st in Settlement.All)
            {
                if (st == null || st.IsHideout) continue;
                n++;
                try { if (m.GetClosestEntranceToFace(st.GatePosition.Face, MobileParty.NavigationType.Default).Item1 == null) names.Add(Name(st) + (st.IsTown ? "" : st.IsCastle ? " (zamek)" : st.IsVillage ? " (wies)" : "")); }
                catch (Exception e) { Stumble("CartTownExit.StaleGates", e); }
            }
            return (names.Count > 0 ? string.Join(", ", names) : "zadna") + " (" + names.Count + " z " + n + " osad)";
        }

        /// <summary>Raz na dobe: bezpiecznik (woz wsi w miescie od N dob - do domu) i linia "Wozy w miastach:".</summary>
        internal static void Daily()
        {
            var s = Settings.Current;
            if (s == null) return;
            var inv = CultureInfo.InvariantCulture;
            int today = (int)CampaignTime.Now.ToDays;
            int day = today - 1;
            if (!_scanned)
            {
                _scanned = true;
                string stale = "?";
                try { stale = StaleGates(); } catch (Exception e) { Stumble("CartTownExit.StaleGates", e); }
                Log.Info("Wozy w miastach: brama poza pamiecia drog ROT (sciana siatki bez wpisu w settlements_distance_cache_Default.bin - straznik BK odrzuca stamtad kazdy rozkaz \"jedz do osady\"): " + stale
                         + "; latka straznika BK " + (!_wired ? "BRAK (nie znaleziono GuardSettlementMove)" : (s.VillageCartLeaveTown ? "CZYNNA" : "WYLACZONA")) + ".");
            }
            bool on = s.VillageCartLeaveTown;                                         // wylacznik calej paczki: wylaczony = latka i bezpiecznik stoja (jak d126), zostaje sam log
            float maxDays = on ? s.VillageCartTownMaxDays : 0f;
            int inTown = 0, over = 0, sent = 0, resent = 0, failed = 0, homeDead = 0, held = 0;
            int rStale = 0, rHold = 0, rAiOff = 0, rTown = 0, rWait = 0, rOther = 0;
            int longest = -1; string longestAt = "";
            var townsNow = new Dictionary<string, int>();
            var present = new HashSet<MobileParty>();
            foreach (var v in Village.All)
            {
                try
                {
                    var comp = v != null ? v.VillagerPartyComponent : null;
                    var cart = comp != null ? comp.MobileParty : null;
                    if (cart == null || !cart.IsActive) continue;
                    var at = cart.CurrentSettlement;
                    if (at == null || !at.IsTown) continue;
                    inTown++;
                    present.Add(cart);
                    int first;
                    if (!_seen.TryGetValue(cart, out first)) { first = today; _seen[cart] = today; }   // po wczytaniu: od pierwszej obserwacji
                    int days = today - first;
                    if (days > longest) { longest = days; longestAt = Name(at); }
                    if (days >= 1) Bump(townsNow, Name(at));
                    if (maxDays <= 0f || days < maxDays) continue;
                    over++;
                    if (at.IsUnderSiege || (at.Party != null && at.Party.MapEvent != null) || cart.MapEvent != null) { held++; continue; }   // gra trzyma wozy w oblezonym miescie i w bitwie - jak dotad
                    var home = cart.HomeSettlement;
                    if (home == null) { failed++; continue; }
                    switch (Reason(cart, at, home))
                    {
                        case Why.Stale: rStale++; break;
                        case Why.Hold: rHold++; break;
                        case Why.AiOff: rAiOff++; break;
                        case Why.TargetTown: rTown++; break;
                        case Why.WaitsHome: rWait++; break;
                        default: rOther++; break;
                    }
                    float d = Campaign.Current.Models.MapDistanceModel.GetDistance(at, home, false, false, MobileParty.NavigationType.Default);
                    if (!(d >= 0f && d < BkLimit)) { homeDead++; continue; }
                    SendHome(cart, home);
                    if (cart.DefaultBehavior == AiBehavior.GoToSettlement && cart.TargetSettlement == home)
                    {
                        sent++;
                        if (!_sent.Add(cart)) resent++;
                    }
                    else failed++;
                }
                catch (Exception e) { Stumble("CartTownExit.Daily", e); }
            }
            // porzadki: wozy, ktore wyjechaly z miasta albo zniknely
            List<MobileParty> gone = null;
            foreach (var k in _seen.Keys) if (!present.Contains(k)) { if (gone == null) gone = new List<MobileParty>(); gone.Add(k); }
            if (gone != null) foreach (var k in gone) { _seen.Remove(k); _sent.Remove(k); }

            lock (_lock)
            {
            // lordowie i karawany stojacy w osadzie, ktorym BK odrzuca rozkazy (w ostatnich StandWindowDays dobach) - ile ich i najdluzej
            int standL = 0, standC = 0; double standMax = -1; string standAt = "", standKind = "";
            var standTowns = new Dictionary<string, int>();
            var standHow = new int[5];                                                // ostatni rozkaz stojacych po latce wysp: None, ToAll, Redirect, Wait, Kept
            double nowD = CampaignTime.Now.ToDays;
            List<MobileParty> left = null;
            foreach (var kv in _stand)
            {
                var p = kv.Key; var st = kv.Value;
                bool stays = false;
                try { stays = p != null && p.IsActive && p.CurrentSettlement == st.At && nowD - st.Last <= StandWindowDays; } catch { }
                if (!stays) { if (left == null) left = new List<MobileParty>(); left.Add(p); continue; }
                if (st.Lord) standL++; else standC++;
                if (st.How >= 0 && st.How < standHow.Length) standHow[st.How]++;
                Bump(standTowns, Name(st.At));
                double dur = nowD - st.First;
                if (dur > standMax) { standMax = dur; standAt = Name(st.At); standKind = st.Lord ? "lord" : "karawana"; }
            }
            if (left != null) foreach (var p in left) _stand.Remove(p);
            var sb = new StringBuilder();
            sb.Append("Wozy w miastach: dzien ").Append(day)
              .Append(" - straznik drog BK (GuardSettlementMove) odrzucil dzis rozkazow \"jedz do osady\": wozom wsi ").Append(_gV)
              .Append(" (z osady: przepuszczone ").Append(_gVPass).Append(Top(_passAt, 4))
              .Append(", w tym rozkazy bezpiecznika ").Append(_gVForce)
              .Append(", bez drogi ladowej z osady ").Append(_gVDead)
              .Append(", latka wylaczona ").Append(_gVOff)
              .Append("; w drodze ").Append(_gVRoad).Append(" - bez zmian)")
              .Append(", lordom ").Append(_gLord).Append(", karawanom ").Append(_gCaravan).Append(", innym ").Append(_gOther)
              .Append(" (bez zmian").Append(Top(_otherAt, 4)).Append(")")
              .Append("; rozne partie: lordow ").Append(_dLord.Count).Append(", karawan ").Append(_dCaravan.Count).Append(", innych ").Append(_dOther.Count)
              .Append(" (powod: sciana bez wpisu w pamieci drog ").Append(_rFace).Append(", na morzu ").Append(_rSea)
              .Append(", brak drogi ladowej do celu, np. z wyspy ").Append(_rNoRoad).Append(", inne ").Append(_rElse).Append(")")
              .Append("; stoja w osadzie z odrzuceniem (ostatnie ").Append(StandWindowDays.ToString("0", inv)).Append(" doby): lordow ").Append(standL).Append(", karawan ").Append(standC)
              .Append(Top(standTowns, 4))
              .Append(", najdluzej ").Append(standMax >= 0 ? standMax.ToString("0.0", inv) + " dob - " + standKind + " w " + standAt : "-")
              .Append(" (ostatni rozkaz po latce wysp: ladem i morzem ").Append(standHow[IslandRoads.ToAll]).Append(", inny cel ").Append(standHow[IslandRoads.Redirect])
              .Append(", czeka ").Append(standHow[IslandRoads.Wait]).Append(", bez zmian ").Append(standHow[IslandRoads.Kept]).Append(", latka nie dotyczy / wylaczona ").Append(standHow[IslandRoads.None]).Append(")")
              .Append("; wozy wsi w miastach teraz ").Append(inTown)
              .Append(" (od wczoraj lub dluzej ").Append(townsNow.Values.Sum()).Append(Top(townsNow, 4))
              .Append("; najdluzej ").Append(longest >= 0 ? longest + " dob - " + longestAt : "-").Append(")")
              .Append("; bezpiecznik (").Append(maxDays > 0f ? "od " + maxDays.ToString("0.#", inv) + " dob w miescie" : (on ? "WYLACZONY" : "WYLACZONY - latka wylaczona")).Append("): wozow ").Append(over)
              .Append(", odeslano do domu ").Append(sent).Append(" (w tym ponownie ").Append(resent).Append(")")
              .Append(", oblezenie / bitwa - czeka ").Append(held)
              .Append(", dom bez drogi ladowej ").Append(homeDead)
              .Append(", rozkaz nie przeszedl ").Append(failed)
              .Append("; staly, bo: brama poza pamiecia drog ROT ").Append(rStale)
              .Append(", cel to miasto ").Append(rTown)
              .Append(", Hold ").Append(rHold)
              .Append(", AI wylaczone ").Append(rAiOff)
              .Append(", cel dom - czeka (zagrozenie pod brama) ").Append(rWait)
              .Append(", inne ").Append(rOther)
              .Append("; latka straznika BK ").Append(!_wired ? "BRAK" : (s.VillageCartLeaveTown ? "CZYNNA" : "WYLACZONA"))
              .Append("; potkniecia dzis ").Append(_stumbles).Append(" (od wczytania ").Append(_stumblesAll).Append(").");
            Log.Info(sb.ToString());
            ClearDay();
            }
            try { IslandRoads.Daily(); } catch (Exception e) { Stumble("IslandRoads.Daily", e); }   // linia "Wyspy i drogi:" zaraz po "Wozy w miastach:"
        }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                _move = AccessTools.Method(typeof(VillagerCampaignBehavior), "MoveVillagersToSettlementWithBestNavigationType");
                var t = AccessTools.TypeByName("BannerKings.Patches.AiDecisionTracePatches");
                _guard = t != null ? AccessTools.Method(t, "GuardSettlementMove") : null;
                var ps = _guard != null ? _guard.GetParameters() : null;
                if (_guard == null || !_guard.IsStatic || _guard.ReturnType != typeof(bool) || ps.Length < 2 || ps[0].ParameterType != typeof(MobileParty) || ps[1].ParameterType != typeof(Settlement))
                {
                    _guard = null;
                    Log.Info("CartTownExit: BRAK BannerKings AiDecisionTracePatches.GuardSettlementMove(MobileParty, Settlement, ..) - straznika BK nie ma albo zmienil sie kod; latka niewpieta, bezpiecznik (VillageCartTownMaxDays, przy wlaczonym VillageCartLeaveTown) dziala"
                             + (_move != null ? "." : ", rozkaz prosty (brak metody gry)."));
                    return;
                }
                // trzeci parametr ref NavigationType (BK z gry: GuardSettlementMove(party, settlement, ref navigationType) - ta sama zmienna, z ktora
                // BK wola SetMoveGoToSettlement): latka wysp moze zmienic droge na ladem i morzem; inna sygnatura - postfiks bez zmiany drogi
                bool navRef = ps.Length >= 3 && ps[2].ParameterType == typeof(MobileParty.NavigationType).MakeByRefType();
                h.Patch(_guard, postfix: new HarmonyMethod(typeof(CartTownExit), navRef ? nameof(GuardPostfix) : nameof(GuardPostfixNoNav)));
                IslandRoads.NavRef = navRef;
                _wired = true;
                var s = Settings.Current;
                Log.Info("CartTownExit: latka wpieta - woz wsi stojacy w osadzie wyjezdza, gdy pamiec drog osada -> cel zna droge ladowa (straznik BK odrzucal rozkazy z bram poza pamiecia drog ROT: Wickenden, Lord Hewett's Town, Acorn Hall) ("
                         + (s != null && s.VillageCartLeaveTown ? "CZYNNA" : "wylaczona") + "); bezpiecznik: woz w miescie od "
                         + (s != null ? s.VillageCartTownMaxDays.ToString("0.#", CultureInfo.InvariantCulture) : "?") + " dob - do domu" + (_move != null ? " (rozkaz gry)." : " (rozkaz prosty - brak metody gry)."));
            }
            catch (Exception e) { Log.Error("CartTownExit.ApplyAll", e); }
        }
    }
}
