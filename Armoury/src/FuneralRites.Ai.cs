using System;
using System.Collections.Generic;
using System.Threading;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>406 (PLAN-396 commit 406, Jeff 11.11 "rob 5") - POKOJ POGRZEBU. Klucz Funeral Truce. Siedziba pogrzebu w trybie PELNYM i jej wsie przez D..D+5:
    ///  - partie AI: ScoreTruce w SiegeRansom.ScorePost (postfiks Last GetTargetScoreForFaction, zaraz po pakcie 384, przed okupem) - Besieger i Raider -> 0,
    ///    dla KAZDEJ frakcji (cs_full.cs:220839 - oblezenie i najazd ida przez te sama ocene celu);
    ///  - armie BK (FindArmyObjective nie pyta o ocene celu, bk.cs:88454): WarFront334.BkFirstPostfix - cel w pokoju zawetowany (nastepny cel albo null);
    ///    to samo weto w WarFront334.VetoedAny (NextBkTarget i opcje 334e dopisywane PO ocenie celu);
    ///  - gracz: SiegeRansom.BesiegeCondPost - "Besiege the town/castle" nieczynne z dymkiem.
    /// NIGDY: tryb cichy, osada fabuly ROT (SiegeRansom.IsStory), dopoki jej wydarzenie oblezenia nie ma stanu 2; siedziba, ktora przestala byc rodu.
    /// TRWAJACEGO oblezenia / najazdu NIE zdejmujemy (osada z SiegeEvent albo wies nie w stanie Normal = bez weta).
    /// Watki: ocena celu bywa liczona rownolegle - czytana jest NIEZMIENNA migawka (_truce), podmieniana w calosci w watku glownym (TruceRebuild);
    /// liczniki przez Interlocked. Linia stanu pokoju (BRAK / POKOJ) pisana przy kazdej ZMIANIE stanu wiersza (406-p), po wczytaniu od nowa. Zero nowych latek.
    /// Znana dziura (406-p, swiadomie): poza kampania kanonu weto armii BK daje brak celu (null) zamiast nastepnego - lord, ktoremu najblizej do siedziby
    /// w zalobie, przez czas pogrzebu nie zwola armii BK (NextBkTarget i jego weta 334 sa pisane i testowane tylko dla kanonu).
    /// Najazd gracza na wies i gracz w cudzej armii - bez blokady (odlozone).</summary>
    internal static partial class FuneralRites
    {
        private sealed class TruceRow
        {
            public Row Src; public string Clan, ClanName, SeatName; public Settlement Seat; public int End;
        }

        private sealed class TruceMap { public readonly Dictionary<string, TruceRow> By = new Dictionary<string, TruceRow>(StringComparer.Ordinal); }

        private static volatile TruceMap _truce;   // null = zadnego pokoju (szybkie wyjscie)

        private static bool TruceKey { get { var s = Settings.Current; return s != null && s.Funerals && s.FuneralTruce; } }

        private static void TruceReset() { _truce = null; GatherReset(); }

        /// <summary>Wydarzenie oblezenia ROT osady fabuly (SiegeRansom.StoryIds); null = nie osada fabuly.</summary>
        private static string StoryEvent(string seatId)
        {
            switch (seatId)
            {
                case "town_EW4": case "ROT_castle61": return "BlackwaterSiegeEvent";
                case "town_B2": return "BastardsSiegeEvent";
                case "ROT_town32": return "HarrenhalSiegeEvent";
                case "ROT_castle60": case "town_B5": return "WallSiegeEvent";
                default: return null;
            }
        }

        /// <summary>Watek glowny: migawka pokoju od nowa (po smierci glowy, co dobe, po wczytaniu). Nie rzuca.</summary>
        private static void TruceRebuild()
        {
            GatherRebuild();   // 407: zjazd rodu - ta sama chwila (smierc glowy, doba, start sesji)
            try
            {
                if (_active.Count == 0) { _truce = null; return; }
                int today = Today();
                TruceMap map = null;
                foreach (var r in _active.Values)
                {
                    if (r.Mode != Full || today > r.End) continue;
                    try
                    {
                        var seat = Settlement.Find(r.Seat);
                        var cl = FindClan(r.Clan);
                        if (seat == null || cl == null || seat.OwnerClan != cl)
                        {
                            if (r.TState != 1) { r.TState = 1; Info("pokoj pogrzebu rodu " + r.Clan + ": BRAK - siedziba " + r.Seat + " nie jest juz rodu."); }
                            continue;
                        }
                        if (SiegeRansom.IsStory(seat))
                        {
                            string ev = StoryEvent(seat.StringId);
                            int state = ev != null ? Prologue.RotEventState(ev) : -1;
                            if (state != 2)
                            {
                                if (r.TState != 2) { r.TState = 2; Info("pokoj pogrzebu rodu " + r.Clan + ": BRAK - " + seat.StringId + " to osada fabuly ROT (" + (ev ?? "?") + " stan " + state + ", potrzeba 2)."); }
                                continue;
                            }
                        }
                        var t = new TruceRow { Src = r, Clan = r.Clan, ClanName = Nm(cl), SeatName = Nm(seat), Seat = seat, End = r.End };
                        if (map == null) map = new TruceMap();
                        map.By[seat.StringId] = t;
                        int villages = 0;
                        var bound = seat.BoundVillages;
                        if (bound != null)
                            foreach (var v in bound)
                            {
                                var vs = v != null ? v.Settlement : null;
                                if (vs == null || vs.StringId == null) continue;
                                map.By[vs.StringId] = t; villages++;
                            }
                        if (r.TState != 3)
                        {
                            r.TState = 3;
                            Info("POKOJ POGRZEBU rodu " + r.Clan + ": siedziba " + seat.StringId + " i " + villages + " wsi do dnia " + r.End + " wlacznie (klucz " + YN(TruceKey)
                                 + "; oblezenie i najazd AI -> ocena 0, armie BK - weto, gracz - opcja oblezenia nieczynna).");
                        }
                    }
                    catch (Exception e) { Stumble("TruceRebuild.row", e); }
                }
                _truce = map;
            }
            catch (Exception e) { _truce = null; Stumble("TruceRebuild", e); }
        }

        /// <summary>Dowolny watek, sam odczyt: osada (siedziba albo jej wies) w czynnym pokoju pogrzebu. Siedziba musi dalej nalezec do rodu.</summary>
        private static bool TruceAt(Settlement st, out TruceRow t)
        {
            t = null;
            var m = _truce;
            if (m == null || st == null || st.StringId == null || !TruceKey) return false;
            if (!m.By.TryGetValue(st.StringId, out t)) return false;
            if (Today() > t.End) return false;
            var oc = t.Seat.OwnerClan;
            return oc != null && oc.StringId == t.Clan;
        }

        /// <summary>Oblezenie albo najazd juz trwa - pokoj go nie zdejmuje.</summary>
        private static bool Ongoing(Settlement st)
        {
            if (st.IsVillage) return st.Village != null && st.Village.VillageState != Village.VillageStates.Normal;
            return st.SiegeEvent != null;
        }

        /// <summary>Z SiegeRansom.ScorePost (postfiks Last GetTargetScoreForFaction; watki AI): cel oblezenia / najazdu w pokoju pogrzebu -> 0. Nie rzuca.</summary>
        internal static void ScoreTruce(Settlement st, Army.ArmyTypes type, MobileParty party, ref float result)
        {
            if (_truce == null || result <= 0f) return;
            try
            {
                if (st == null || party == null || (type != Army.ArmyTypes.Besieger && type != Army.ArmyTypes.Raider)) return;
                TruceRow t;
                if (!TruceAt(st, out t) || Ongoing(st)) return;
                result = 0f;
                Interlocked.Increment(ref t.Src.TZero);
            }
            catch (Exception e) { Stumble("ScoreTruce", e); }
        }

        /// <summary>Weto celu bez licznika (WarFront334.VetoedAny: NextBkTarget i opcje 334e dopisywane po ocenie celu; watki AI). Nie rzuca.</summary>
        internal static bool TruceVeto(Settlement st)
        {
            if (_truce == null) return false;
            try { TruceRow t; return TruceAt(st, out t) && !Ongoing(st); }
            catch (Exception e) { Stumble("TruceVeto", e); return false; }
        }

        /// <summary>Weto celu armii BK (WarFront334.BkFirstPostfix) - z licznikiem. Nie rzuca.</summary>
        internal static bool BkVeto(Settlement st)
        {
            if (_truce == null) return false;
            try
            {
                TruceRow t;
                if (!TruceAt(st, out t) || Ongoing(st)) return false;
                Interlocked.Increment(ref t.Src.TBk);
                return true;
            }
            catch (Exception e) { Stumble("BkVeto", e); return false; }
        }

        /// <summary>Z SiegeRansom.BesiegeCondPost: opcja gry "Besiege the town/castle" w pokoju pogrzebu - nieczynna z dymkiem. true = zablokowano. Nie rzuca.</summary>
        internal static bool PlayerBesiege(MenuCallbackArgs args)
        {
            if (_truce == null || args == null) return false;
            try
            {
                var st = Settlement.CurrentSettlement;
                TruceRow t;
                if (st == null || !TruceAt(st, out t) || Ongoing(st)) return false;
                int today = Today();
                int left = Math.Max(1, t.End - today);
                args.IsEnabled = false;
                args.Tooltip = new TextObject("{=!}" + t.ClanName + " is burying its lord at " + t.SeatName + ". No banner besieges a house in mourning; the funeral peace lasts " + left
                                              + (left == 1 ? " more day." : " more days."));
                if (t.Src.TPlayerDay != today) { t.Src.TPlayerDay = today; t.Src.TPlayer++; }   // warunek menu biegnie co odswiezenie - liczymy doby
                return true;
            }
            catch (Exception e) { Stumble("PlayerBesiege", e); return false; }
        }

        /// <summary>Doba (po wygaszeniu wierszy): migawka od nowa i pomiar - siedziba oblegana mimo pokoju (np. armia BK, ktora dostala cel przed smiercia glowy).</summary>
        private static void TruceDaily()
        {
            TruceRebuild();
            var m = _truce;
            if (m == null) return;
            foreach (var kv in m.By)
            {
                var t = kv.Value;
                try
                {
                    if (kv.Key != t.Seat.StringId || t.Src.TBroken || !t.Seat.IsUnderSiege) continue;
                    t.Src.TBroken = true;
                    var camp = t.Seat.SiegeEvent != null ? t.Seat.SiegeEvent.BesiegerCamp : null;
                    var lp = camp != null ? camp.LeaderParty : null;
                    Info("POKOJ POGRZEBU ZLAMANY: " + t.Seat.StringId + " (rod " + t.Clan + ") oblegana przez " + (lp != null ? lp.StringId + " / " + (lp.MapFaction != null ? lp.MapFaction.StringId : "-") : "?")
                         + (lp != null && lp.IsMainParty ? " [gracz]" : "") + " - oblezenia nie zdejmujemy (pomiar).");
                }
                catch (Exception e) { Stumble("TruceDaily.row", e); }
            }
        }

        private static string TruceEndText(Row r)
        {
            return " | rozejm: wyzerowane cele " + Volatile.Read(ref r.TZero) + ", weta BK " + Volatile.Read(ref r.TBk) + ", gracz zablokowany " + r.TPlayer + (r.TBroken ? ", oblezona mimo pokoju TAK" : "");
        }

        // ================================================================ 407: ZJAZD RODU (AI)
        // PLAN-396 commit 407 (Jeff 11.11 "rob 5"). Jedyna latka pogrzebow: postfiks priorytet 130 na CampaignEventDispatcher.AiHourlyTick(MobileParty, PartyThinkParams)
        // (metoda zadeklarowana w tej klasie - DeclaredMethod; po RescueHunt 150 i pakcie 140, przed ArmsFirst 100, wetami 50/48/45 i LordEscort / SiegeWalls2 Last -
        // dozbrojenie, weta i ucieczka przed silniejszym dalej wygrywaja). Wpinana w kampanii, raz na proces (GatherSessionStart).
        // REGULA: partia lorda rodu zmarlego (pogrzeb PELNY, do dnia End wlacznie) dostaje GoToSettlement(siedziba) = najlepsza ocena tej godziny + 0.01, gdy WSZYSTKO:
        //  nie gracz, nie w armii / bitwie / oblezeniu / na morzu, nie gloduje i ma zapas jedzenia (407-p: miasto 2 doby, zamek do konca pogrzebu + 2), siedziba nie
        //  oblezona, nie przez Mur (gdy gra / ROT nie daly wpisu do siedziby), !DoNotChangeBehavior, lider nie pod straza fabuly, odleglosc <= Funeral Travel Range,
        //  zadna twierdza rodu ani krolestwa w zasiegu 120 od partii nie jest oblegana, krolestwo w wojnie -> zdrowych >= Funeral Min Escort,
        //  NAJLEPSZA opcja tej godziny jest pokojowa (PatrolAroundPoint albo GoToSettlement, bez zwolywania armii) - kazda inna na czele = nie jedzie.
        // DECYZJA (407-p, swiadomie): zbiorka wojenna W1b i werbunek W1a z CanonWarBalance (250) oraz patrol lowow RescueHunt (150) to dla nas zwykle
        // GoToSettlement / PatrolAroundPoint = "pokojowe" - lord rodu w zalobie w zasiegu 120 jedzie na pogrzeb zamiast na zbiorke (do 6 dob, litera planu).
        // Wyniku <= -1 (cudze weto celu) nie podnosimy. Partia stojaca w siedzibie dostaje te sama ocene do konca pogrzebu: gra przy GoToSettlement do osady, w ktorej
        // partia juz jest, nie wydaje nowego rozkazu (cs_full.cs:221119) - partia zostaje.
        // Mysl AI w 1.4.8 jest sekwencyjna (tickery TickPartialHourlyAi z doParallel: false, cs_full.cs:22049), mimo to postfiks czyta NIEZMIENNA migawke (_gather),
        // a pamiec linii logu jest pod zamkiem. Bez pogrzebu pelnego: jedno porownanie z null.

        private sealed class GatherRow
        {
            public Row Src; public string ClanId; public Clan Clan; public Settlement Seat; public int End;
            public HashSet<string> Guarded;   // lordowie rodu pod straza fabuly w chwili budowy migawki (doba)
        }

        private sealed class GatherMap { public readonly Dictionary<string, GatherRow> By = new Dictionary<string, GatherRow>(StringComparer.Ordinal); }

        private const string GatherHarmonyId = "armoury.funeralrites.gather407";
        private const float GatherThreatRange = 120f;   // oblegana twierdza rodu / krolestwa blizej niz tyle od partii = obrona wazniejsza
        private const int GatherLogCap = 4;              // linii "pominieta" na partie na pogrzeb
        private static volatile GatherMap _gather;       // null = zadnego zjazdu (szybkie wyjscie)
        private static bool _gTried, _gHooked;
        private static string _gHook = "nie probowano";
        private static readonly object _gLock = new object();
        private static readonly Dictionary<string, string> _gSaid = new Dictionary<string, string>(StringComparer.Ordinal);            // "rod|partia" -> ostatni powod / stan w logu
        private static readonly Dictionary<string, int> _gSaidN = new Dictionary<string, int>(StringComparer.Ordinal);                  // "rod|partia" -> ile linii "pominieta"
        private static readonly Dictionary<string, HashSet<string>> _gSeen = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);      // rod -> partie lordow rodu widziane w mysli AI
        private static readonly Dictionary<string, HashSet<string>> _gArrived = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);   // rod -> partie, ktore staly w siedzibie

        private static void GatherReset()
        {
            _gather = null;
            lock (_gLock) { _gSaid.Clear(); _gSaidN.Clear(); _gSeen.Clear(); _gArrived.Clear(); }
        }

        /// <summary>OnSessionLaunched (raz na proces): postfiks 130 na CampaignEventDispatcher.AiHourlyTick. Stan w linii startu pogrzebow. Nie rzuca.</summary>
        internal static void GatherSessionStart()
        {
            if (_gTried) return;
            _gTried = true;
            try
            {
                var m = AccessTools.DeclaredMethod(typeof(CampaignEventDispatcher), "AiHourlyTick", new[] { typeof(MobileParty), typeof(PartyThinkParams) });
                if (m == null) { _gHook = "BRAK (CampaignEventDispatcher.AiHourlyTick)"; return; }
                new Harmony(GatherHarmonyId).Patch(m, postfix: new HarmonyMethod(typeof(FuneralRites), nameof(GatherPostfix)) { priority = 130 });
                var info = Harmony.GetPatchInfo(m);
                if (info != null) foreach (var p in info.Postfixes) if (p != null && p.owner == GatherHarmonyId) _gHooked = true;
                _gHook = _gHooked ? "wpieta" : "BRAK (latka nie weszla)";
            }
            catch (Exception e) { _gHook = "BLAD (" + e.GetType().Name + ")"; Stumble("GatherSessionStart", e); }
        }

        /// <summary>Watek glowny (z TruceRebuild: po smierci glowy, co dobe, przy starcie sesji): migawka zjazdu od nowa. Nie rzuca.</summary>
        private static void GatherRebuild()
        {
            try
            {
                if (_active.Count == 0) { _gather = null; return; }
                int today = Today();
                GatherMap map = null;
                foreach (var r in _active.Values)
                {
                    if (r.Mode != Full || today > r.End) continue;
                    try
                    {
                        var seat = Settlement.Find(r.Seat);
                        var cl = FindClan(r.Clan);
                        if (seat == null || cl == null || cl.IsEliminated || cl == Clan.PlayerClan || seat.OwnerClan != cl) continue;
                        var g = new GatherRow { Src = r, ClanId = r.Clan, Clan = cl, Seat = seat, End = r.End, Guarded = new HashSet<string>(StringComparer.Ordinal) };
                        foreach (var h in cl.Heroes)
                            if (h != null && h.IsAlive && h.StringId != null && GuardedSafe(h)) g.Guarded.Add(h.StringId);
                        if (map == null) map = new GatherMap();
                        map.By[r.Clan] = g;
                    }
                    catch (Exception e) { Stumble("GatherRebuild.row", e); }
                }
                _gather = map;
            }
            catch (Exception e) { _gather = null; Stumble("GatherRebuild", e); }
        }

        private static bool RealmAtWar(IFaction f)
        {
            if (f == null || !f.IsKingdomFaction) return false;
            foreach (var k in Kingdom.All)
                if (k != null && k != f && !k.IsEliminated && f.IsAtWarWith(k)) return true;
            return false;
        }

        /// <summary>Oblegana twierdza rodu albo jego krolestwa w zasiegu 120 od partii (obrona wazniejsza niz pogrzeb). null = brak.</summary>
        private static Settlement ThreatNear(MobileParty party, Clan cl)
        {
            var pos = party.GetPosition2D;
            foreach (var t in cl.Fiefs)
            {
                var st = t != null ? t.Settlement : null;
                if (st != null && st.IsUnderSiege && pos.Distance(st.GetPosition2D) <= GatherThreatRange) return st;
            }
            var k = cl.Kingdom;
            if (k != null)
                foreach (var t in k.Fiefs)
                {
                    var st = t != null ? t.Settlement : null;
                    if (st != null && st.IsUnderSiege && pos.Distance(st.GetPosition2D) <= GatherThreatRange) return st;
                }
            return null;
        }

        /// <summary>Linia "pominieta" - raz na zmiane powodu, najwyzej GatherLogCap na partie na pogrzeb.</summary>
        private static void GatherSkip(GatherRow t, MobileParty party, string code, string detail)
        {
            if (!LogOn) return;
            string key = t.ClanId + "|" + party.StringId;
            lock (_gLock)
            {
                string last; int n;
                if (_gSaid.TryGetValue(key, out last) && last == code) return;
                _gSaidN.TryGetValue(key, out n);
                if (n >= GatherLogCap) return;
                _gSaid[key] = code; _gSaidN[key] = n + 1;
            }
            Info("zjazd " + t.ClanId + ": partia " + party.StringId + " pominieta (" + (detail ?? code) + ").");
        }

        /// <summary>true = pierwszy raz w tym stanie (linia do logu).</summary>
        private static bool GatherMark(GatherRow t, MobileParty party, string code)
        {
            string key = t.ClanId + "|" + party.StringId;
            lock (_gLock)
            {
                string last;
                if (_gSaid.TryGetValue(key, out last) && last == code) return false;
                _gSaid[key] = code;
                return true;
            }
        }

        private static void GatherNote(Dictionary<string, HashSet<string>> book, string clan, string partyId)
        {
            lock (_gLock)
            {
                HashSet<string> set;
                if (!book.TryGetValue(clan, out set)) book[clan] = set = new HashSet<string>(StringComparer.Ordinal);
                set.Add(partyId);
            }
        }

        /// <summary>Postfiks (priorytet 130) CampaignEventDispatcher.AiHourlyTick: zjazd rodu na pogrzeb glowy (regula w naglowku sekcji). Nie rzuca.</summary>
        public static void GatherPostfix(MobileParty party, PartyThinkParams partyThinkParams)
        {
            var g = _gather;
            if (g == null) return;
            try
            {
                if (party == null || partyThinkParams == null || !party.IsLordParty || party.IsMainParty || party.StringId == null) return;
                var lh = party.LeaderHero;
                var cl = lh != null ? lh.Clan : null;
                if (cl == null || cl.StringId == null) return;
                GatherRow t;
                if (!g.By.TryGetValue(cl.StringId, out t) || party.ActualClan != cl) return;
                var s = Settings.Current;
                if (s == null || !s.Funerals || Today() > t.End) return;
                float range = s.FuneralTravelRange;
                if (range <= 0f) return;
                var seat = t.Seat;
                GatherNote(_gSeen, t.ClanId, party.StringId);
                bool here = party.CurrentSettlement == seat;
                if (here)
                {
                    GatherNote(_gArrived, t.ClanId, party.StringId);
                    if (GatherMark(t, party, "dotarla")) Info("zjazd " + t.ClanId + ": partia " + party.StringId + " dotarla do " + seat.StringId + " (zostaje do dnia " + t.End + ", dopoki najlepsza opcja godziny jest pokojowa).");
                }
                var oc = seat.OwnerClan;
                if (oc != cl) { GatherSkip(t, party, "siedziba", "siedziba nie jest juz rodu"); return; }
                // 407-p: siedziba oblezona w trakcie pogrzebu - wprost (dotad posrednio przez ThreatNear w zasiegu 120; przy Funeral Travel Range > 120 partia jechala pod oboz)
                if (seat.IsUnderSiege && !here) { GatherSkip(t, party, "siedziba-oblezona", "siedziba " + seat.StringId + " oblezona"); return; }
                if (partyThinkParams.DoNotChangeBehavior) { GatherSkip(t, party, "dnc", "rozkaz nie do zmiany (DoNotChangeBehavior)"); return; }
                if (party.Army != null) { GatherSkip(t, party, "armia", "w armii"); return; }
                if (party.MapEvent != null) { GatherSkip(t, party, "bitwa", "w bitwie"); return; }
                if (party.BesiegedSettlement != null || party.SiegeEvent != null) { GatherSkip(t, party, "oblega", "oblega"); return; }
                if (party.CurrentSettlement != null && party.CurrentSettlement.IsUnderSiege) { GatherSkip(t, party, "oblezona", "stoi w oblezonej twierdzy"); return; }
                if (party.IsCurrentlyAtSea) { GatherSkip(t, party, "morze", "na morzu"); return; }
                if (party.Party.IsStarving) { GatherSkip(t, party, "glod", "gloduje"); return; }
                // 407-p: zapas jedzenia. Wizyta gry po zywnosc to tez GoToSettlement (bylaby przebita o 0.01), a gra kupuje jedzenie tylko w miescie i wsi
                // (cs_full.cs:202481) - w ZAMKU partia stalaby do dnia End i zaglodzila sie. Miasto: 2 doby zapasu; zamek: zapas do konca pogrzebu + 2 doby.
                int foodNeed = seat.IsTown ? 2 : Math.Max(0, t.End - Today()) + 2;
                int foodHave = party.GetNumDaysForFoodToLast();
                if (foodHave < foodNeed) { GatherSkip(t, party, "zywnosc", "zapas jedzenia " + foodHave + " dob < " + foodNeed + (seat.IsTown ? "" : " (zamek: do konca pogrzebu + 2)")); return; }
                // 407-p: straz fabuly takze na zywo (migawka doby mogla nie znac bohatera, ktory wszedl w stan "potrzebny" w srodku doby); mysl AI jest sekwencyjna
                if (lh.StringId != null && (t.Guarded.Contains(lh.StringId) || GuardedSafe(lh))) { GatherSkip(t, party, "straz", "lider " + lh.StringId + " pod straza fabuly"); return; }
                float dist = here ? 0f : party.GetPosition2D.Distance(seat.GetPosition2D);
                if (dist > range) { GatherSkip(t, party, "daleko", "odleglosc " + dist.ToString("F0", Inv) + " > " + range.ToString("F0", Inv)); return; }
                var threat = ThreatNear(party, cl);
                if (threat != null) { GatherSkip(t, party, "obrona", "oblegana twierdza " + threat.StringId + " w zasiegu " + GatherThreatRange.ToString("F0", Inv)); return; }
                int escort = s.FuneralMinEscort;
                if (escort > 0 && RealmAtWar(cl.MapFaction))
                {
                    int healthy = party.MemberRoster != null ? party.MemberRoster.TotalHealthyCount : 0;
                    if (healthy < escort) { GatherSkip(t, party, "eskorta", "wojna: zdrowych " + healthy + " < " + escort); return; }
                }
                var scores = partyThinkParams.AIBehaviorScores;
                if (scores == null || scores.Count == 0) { GatherSkip(t, party, "brak", "brak opcji tej godziny"); return; }
                int topI = -1, mineI = -1; float top = float.MinValue;
                for (int i = 0; i < scores.Count; i++)
                {
                    var d = scores[i].Item1; float v = scores[i].Item2;
                    if (v > top) { top = v; topI = i; }
                    if (mineI < 0 && d.AiBehavior == AiBehavior.GoToSettlement && ReferenceEquals(d.Party, seat) && !d.WillGatherArmy) mineI = i;
                }
                if (topI < 0 || !(top > -1f)) { GatherSkip(t, party, "weta", "wszystkie opcje zawetowane"); return; }
                var topD = scores[topI].Item1;
                bool peaceful = (topD.AiBehavior == AiBehavior.PatrolAroundPoint || topD.AiBehavior == AiBehavior.GoToSettlement) && !topD.WillGatherArmy;
                if (!peaceful) { GatherSkip(t, party, "wojenna:" + topD.AiBehavior, "najlepsza opcja wojenna: " + topD.AiBehavior + (topD.WillGatherArmy ? " (zwoluje armie)" : "") + " " + top.ToString("F2", Inv)); return; }
                float ours = top + 0.01f;
                if (mineI >= 0)
                {
                    float cur = scores[mineI].Item2;
                    if (!(cur > -1f)) { GatherSkip(t, party, "weto", "cel " + seat.StringId + " zawetowany (ocena " + cur.ToString("F2", Inv) + ")"); return; }
                    if (mineI != topI)
                    {
                        var d = scores[mineI].Item1;
                        partyThinkParams.SetBehaviorScore(in d, ours);
                    }
                }
                else
                {
                    // 407-p: gra / ROT nie daly wpisu do siedziby. ROT (AIThinkPatch.Patch1, rot_all.cs:17966) usuwa cele po drugiej stronie Muru - nie dopisujemy
                    // go z powrotem (ta sama bramka strony Muru co ranny wodz 408: ROTUtilities.IsBeyondTheWall przez ArmsFirst.Beyond)
                    if (!here)
                    {
                        var bw = ArmsFirst.Beyond();
                        bool wall = false;
                        if (bw != null) { try { wall = bw(party.GetPosition2D) != bw(seat.GetPosition2D); } catch { wall = false; } }
                        if (wall) { GatherSkip(t, party, "mur", "siedziba " + seat.StringId + " po drugiej stronie Muru"); return; }
                    }
                    MobileParty.NavigationType nav; float navDist; bool port;
                    AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty(party, seat, false, out nav, out navDist, out port);
                    if (nav == MobileParty.NavigationType.None)
                    {
                        if (!here) { GatherSkip(t, party, "droga", "brak drogi do " + seat.StringId); return; }
                        nav = MobileParty.NavigationType.Default; port = false;   // stoi w siedzibie - gra i tak nie wyda rozkazu (cs_full.cs:221119)
                    }
                    var a = new AIBehaviorData(seat, AiBehavior.GoToSettlement, nav, false, port, false);
                    var item = (a, ours);
                    partyThinkParams.AddBehaviorScore(in item);
                }
                Interlocked.Increment(ref t.Src.GOrders);
                if (!here && GatherMark(t, party, "wyrusza"))
                    Info("zjazd " + t.ClanId + ": partia " + party.StringId + " wyrusza do " + seat.StringId + " (odleglosc " + dist.ToString("F0", Inv) + ", ocena " + ours.ToString("F2", Inv) + " = najlepsza pokojowa " + topD.AiBehavior + " + 0.01).");
            }
            catch (Exception e) { Stumble("GatherPostfix", e); }
        }

        /// <summary>Koniec pogrzebu (watek glowny, linia KONIEC): "obecnych n/m" i sprzatanie pamieci zjazdu tego rodu.</summary>
        private static string GatherEnd(Row r)
        {
            try
            {
                int n = 0, m = 0;
                lock (_gLock)
                {
                    HashSet<string> set;
                    if (_gArrived.TryGetValue(r.Clan, out set)) n = set.Count;
                    if (_gSeen.TryGetValue(r.Clan, out set)) m = set.Count;
                    _gArrived.Remove(r.Clan); _gSeen.Remove(r.Clan);
                    string pre = r.Clan + "|";
                    List<string> drop = null;
                    foreach (var k in _gSaid.Keys) if (k.StartsWith(pre, StringComparison.Ordinal)) { if (drop == null) drop = new List<string>(); drop.Add(k); }
                    if (drop != null) foreach (var k in drop) { _gSaid.Remove(k); _gSaidN.Remove(k); }
                }
                if (r.Mode != Full) return "";
                return " | zjazd: obecnych " + n + "/" + m + " partii rodu, rozkazow " + Volatile.Read(ref r.GOrders);
            }
            catch (Exception e) { Stumble("GatherEnd", e); return ""; }
        }
    }
}
