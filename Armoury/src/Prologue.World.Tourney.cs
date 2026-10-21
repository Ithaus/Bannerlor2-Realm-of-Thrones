using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// m104-e5 pkt 2 - TURNIEJ REKI (klucz PrologueHandTourney; projekt pkt 2: dzien 125, Krolewska Przystan, Gora, Rycerz Kwiatow, Ogar, gracz moze startowac).
    /// Dzien 117: uczestnicy kanonu jada do Krolewskiej Przystani - Ser Gregor Clegane (lord_4_21), Ser Loras Tyrell (lord_1_44), Ser Jaime Lannister (ROTwanderer1)
    /// swoimi druzynami (partia sceny: rozkaz "do miasta", bez nowych decyzji; partia w armii - nie ruszamy jej, linia), Ogar (ROTwanderer3, dworzanin bez druzyny) -
    /// przeniesiony do miasta w dniu obwieszczenia (gra nie wyprowadza lordow z miasta z turniejem - HeroSpawnCampaignBehavior.CanHeroMoveToAnotherSettlement). Dzien 121: obwieszczenie
    /// przez GrandTourney (CanonApi.ProclaimRoyal odbiciem: turniej Grand bez reguly osmiu i bez spadku do lokalnego, zjazd GatherDays = 4 dni z zamknietymi listami,
    /// uczestnicy kanonu w drabince przed innymi lordami), bez GrandTourney - turniej gry (TournamentModel.CreateTournament). Dzien 125 listy otwarte (gracz moze
    /// startowac), dzien 127 - jesli nikt go nie rozegral - rozstrzygniecie gry (TournamentManager.ResolveTournament; nagroda jak w kazdym turnieju - nic z niczego).
    /// Dzien 128 albo rozstrzygniecie - uczestnicy wracaja do swojego AI. Oblezenie miasta - turniej odwolany (gra), linia.
    /// </summary>
    internal static partial class Prologue
    {
        private const string HScene = "turniej";
        private const string GregorId = "lord_4_21", LorasId = "lord_1_44";
        private const int TourneySummonFrom = 117, TourneyProclaim = 121, TourneyResolveBy = 127, TourneyReleaseAt = 128;

        // stan (zapis): -1 pominiety/odwolany, 0 przed, 1 wezwania, 2 obwieszczony, 3 rozstrzygniety
        private static int _thPhase;
        private static double _thDay = -1;
        private static string _thWinner = "", _thWhy = "", _thHow = "";
        private static readonly HashSet<string> _thMoved = new HashSet<string>(StringComparer.Ordinal);   // przeniesieni do miasta (bez druzyny) - raz
        private static bool _thHeroesSent;    // sesja: uczestnicy kanonu podani GrandTourney (po wczytaniu - ponownie)
        private static bool _gtTried;
        private static MethodInfo _gtProclaim, _gtSetHeroes;

        private static void ResetTourney()
        {
            _thPhase = 0; _thDay = -1; _thWinner = _thWhy = _thHow = ""; _thMoved.Clear(); _thHeroesSent = false;
        }

        private static string PackTourney()
        {
            return "wth=" + _thPhase.ToString(Inv) + "," + D(_thDay) + ";wtw=" + Clean(_thWinner) + ";wtx=" + Clean(_thWhy) + ";wth2=" + Clean(_thHow) + ";wtm=" + Ids(_thMoved) + ";";
        }

        private static void UnpackTourney(Dictionary<string, string> map)
        {
            ResetTourney();
            string v;
            if (map.TryGetValue("wth", out v)) { var a = v.Split(','); _thPhase = Int(a, 0, 0); _thDay = Dbl(a, 1, -1); }
            if (map.TryGetValue("wtw", out v)) _thWinner = Unesc(v);
            if (map.TryGetValue("wtx", out v)) _thWhy = Unesc(v);
            if (map.TryGetValue("wth2", out v)) _thHow = Unesc(v);
            foreach (var id in IdList(map, "wtm")) _thMoved.Add(id);
        }

        private static List<Hero> TourneyHeroes()
        {
            var l = new List<Hero>();
            foreach (var h in new[] { H(GregorId), H(LorasId), Tpl(SandorTpl), Tpl(JaimeTpl) }) if (h != null) l.Add(h);
            return l;
        }

        private static string[] TourneyIds() { return TourneyHeroes().Select(h => h.StringId).ToArray(); }

        private static bool GtReady()
        {
            if (!_gtTried)
            {
                _gtTried = true;
                try
                {
                    var t = AccessTools.TypeByName("GrandTourney.CanonApi");
                    if (t != null)
                    {
                        _gtProclaim = t.GetMethod("ProclaimRoyal", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string), typeof(string[]) }, null);
                        _gtSetHeroes = t.GetMethod("SetHeroes", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string), typeof(string[]) }, null);
                    }
                }
                catch (Exception e) { WStumble("GtReady", e); }
            }
            return _gtProclaim != null && _gtSetHeroes != null;
        }

        private static void TourneyStep(double day, bool daily)
        {
            var s = S;
            if (s == null || !s.PrologueHandTourney) return;
            if (_thPhase < 0 || _thPhase >= 3) return;
            var kl = KingsLanding();
            if (_thPhase == 0)
            {
                if (day < TourneySummonFrom) return;
                if (kl == null || kl.Town == null) { TourneySkip("brak miasta " + KingsLandingId, day); return; }
                if (_wLegacy && day > TourneyProclaim + 1) { TourneySkip("zapis sprzed E5 - czas obwieszczenia minal", day); return; }
                _thPhase = 1;
                Log.Info(WTag + "Turniej Reki: uczestnicy kanonu jada do Krolewskiej Przystani (dzien prologu " + D(day) + ") | " + TourneyHeroesText(kl) + ".");
            }
            if (kl == null || kl.Town == null) return;
            if (_thPhase == 2 && !_thHeroesSent && GtReady())
            {
                _thHeroesSent = true;
                try { _gtSetHeroes.Invoke(null, new object[] { kl.StringId, TourneyIds() }); } catch (Exception e) { WStumble("GtSetHeroes", e); }
            }
            if (_thPhase == 1 && day >= TourneyProclaim) TourneyProclaimNow(kl, day);
            if (_thPhase == 2 && day >= TourneyResolveBy) TourneyResolve(kl, day);
            if (_thPhase >= 3 || _thPhase < 0 || day >= TourneyReleaseAt) return;
            foreach (var h in TourneyHeroes())
            {
                var p = Led(h);
                if (p != null)
                {
                    if (p.Army != null && !Scripted(p)) continue;   // w armii (wojna) - nie odciagamy (jak herold GT)
                    Control(HScene, p, KGo, kl, null, false);
                    continue;
                }
                // bez druzyny - dopiero po obwieszczeniu (turniej w miescie: gra nie wyprowadza wtedy lordow z miasta - HeroSpawnCampaignBehavior.CanHeroMoveToAnotherSettlement)
                if (_thPhase < 2 || _thMoved.Contains(h.StringId) || h.CurrentSettlement == kl) continue;
                try
                {
                    if (!h.IsAlive || h.IsPrisoner || h.PartyBelongedTo != null || h.IsChild || !h.IsActive || h.GovernorOf != null || h.Clan == Clan.PlayerClan) continue;
                    TeleportHeroAction.ApplyImmediateTeleportToSettlement(h, kl);
                    _thMoved.Add(h.StringId);
                    Log.Info(WTag + "Turniej Reki: " + WhoS(h) + " (bez druzyny) " + (h.CurrentSettlement == kl ? "przyjezdza do " + kl.StringId : "NIE przyjechal: " + WhereS(h))
                             + " (dzien prologu " + D(day) + ").");
                }
                catch (Exception e) { WStumble("TourneyMove(" + h.StringId + ")", e); }
            }
        }

        private static void TourneySkip(string why, double day)
        {
            _thPhase = -1; _thWhy = why;
            Log.Info(WTag + "Turniej Reki POMINIETY albo ODWOLANY - " + why + " (dzien prologu " + D(day) + ").");
        }

        private static string TourneyHeroesText(Settlement kl)
        {
            var parts = new List<string>();
            foreach (var h in TourneyHeroes())
            {
                var p = Led(h);
                parts.Add(WhoS(h) + " " + (p != null ? (p.Army != null && !Scripted(p) ? "(w armii - zostaje)" : "(" + Pos(p, kl) + ")") : "(" + WhereS(h) + ")"));
            }
            return parts.Count > 0 ? string.Join(", ", parts.ToArray()) : "brak uczestnikow kanonu";
        }

        private static void TourneyProclaimNow(Settlement kl, double day)
        {
            string r;
            if (GtReady())
            {
                try { r = Convert.ToString(_gtProclaim.Invoke(null, new object[] { kl.StringId, TourneyIds() })); _thHeroesSent = true; }
                catch (TargetInvocationException e) { r = "BLAD " + (e.InnerException ?? e).GetType().Name; WStumble("GtProclaim", e.InnerException ?? e); }
            }
            else
            {
                var mgr = Campaign.Current.TournamentManager;
                if (mgr.GetTournamentGame(kl.Town) == null) mgr.AddTournament(Campaign.Current.Models.TournamentModel.CreateTournament(kl.Town));
                r = "bez GrandTourney - turniej gry";
            }
            bool ok = Campaign.Current.TournamentManager.GetTournamentGame(kl.Town) != null;
            if (!ok) { TourneySkip("turniej nie powstal (" + r + ")", day); return; }
            _thPhase = 2;
            Log.Info(WTag + "Turniej Reki OBWIESZCZONY w " + kl.StringId + " (dzien prologu " + D(day) + ") | GrandTourney: " + r + " | uczestnicy kanonu: " + TourneyHeroesText(kl) + ".");
            Log.Player("King Robert proclaims a great tourney at King's Landing in honour of his new Hand, Lord Eddard Stark. The lists open in four days, and any knight may ride.");
        }

        private static void TourneyResolve(Settlement kl, double day)
        {
            var mgr = Campaign.Current.TournamentManager;
            var game = mgr.GetTournamentGame(kl.Town);
            if (game == null)
            {
                _thPhase = 3; _thDay = day; _thWinner = "?"; _thHow = "zniknal bez linii zwyciezcy";
                Log.Info(WTag + "Turniej Reki: turnieju w " + kl.StringId + " juz nie ma (bez linii zwyciezcy) - koniec sceny (dzien prologu " + D(day) + ").");
                return;
            }
            _thHow = "rozstrzygniecie gry w dniu " + TourneyResolveBy;
            mgr.ResolveTournament(game, kl.Town);   // TournamentFinished (OnTourneyFinished) albo TournamentCancelled (oblezenie)
            if (_thPhase == 2) { _thPhase = 3; _thDay = day; Log.Info(WTag + "Turniej Reki: rozstrzygniety bez zdarzenia konca (dzien prologu " + D(day) + ")."); }
        }

        /// <summary>CampaignEvents.TournamentFinished: Turniej Reki rozstrzygniety (gracz albo AI) - linia i wiesc.</summary>
        internal static void OnTourneyFinished(CharacterObject winner, MBReadOnlyList<CharacterObject> participants, Town town, ItemObject prize)
        {
            try
            {
                if (!Active || town == null || town.Settlement == null || town.Settlement.StringId != KingsLandingId || _thPhase != 2) return;
                double day = PrologueDay();
                _thPhase = 3; _thDay = day;
                _thWinner = winner != null && winner.Name != null ? winner.Name.ToString() : "?";
                bool player = participants != null && participants.Contains(CharacterObject.PlayerCharacter);
                if (player) _thHow = "z graczem";
                else if (string.IsNullOrEmpty(_thHow)) _thHow = "AI gry";
                var canon = new List<string>();
                foreach (var h in TourneyHeroes()) canon.Add(WhoS(h) + " " + (participants != null && participants.Contains(h.CharacterObject) ? "TAK" : "NIE"));
                Log.Info(WTag + "Turniej Reki ROZSTRZYGNIETY w " + KingsLandingId + " (dzien prologu " + D(day) + ", " + _thHow + ") | zwyciezca " + _thWinner
                         + " | uczestnicy kanonu w drabince: " + string.Join(", ", canon.ToArray()) + " | gracz " + (player ? "TAK" : "NIE") + " | uczestnikow " + (participants != null ? participants.Count : 0)
                         + " | nagroda " + (prize != null ? prize.Name.ToString() : "-") + ".");
                if (!player) Log.Player("The Tourney of the Hand is over. " + _thWinner + " is hailed as champion in King's Landing.");
            }
            catch (Exception e) { WStumble("OnTourneyFinished", e); }
        }

        /// <summary>CampaignEvents.TournamentCancelled: Turniej Reki odwolany (oblezenie miasta).</summary>
        internal static void OnTourneyCancelled(Town town)
        {
            try
            {
                if (!Active || town == null || town.Settlement == null || town.Settlement.StringId != KingsLandingId || _thPhase != 2) return;
                TourneySkip("odwolany przez gre (oblezenie miasta albo bunt)", PrologueDay());
            }
            catch (Exception e) { WStumble("OnTourneyCancelled", e); }
        }

        private static string TourneyLine()
        {
            string st = _thPhase < 0 ? "pominiety/odwolany (" + _thWhy + ")" : _thPhase == 0 ? "przed (wezwania od d" + TourneySummonFrom + ")" : _thPhase == 1 ? "uczestnicy w drodze"
                        : _thPhase == 2 ? "obwieszczony (listy od d125, rozstrzygniecie najpozniej d" + TourneyResolveBy + ")" : "ROZSTRZYGNIETY d" + D(_thDay) + " (" + _thHow + "), zwyciezca " + _thWinner;
            if (_thPhase == 1 || _thPhase == 2)
            {
                var kl = KingsLanding();
                st += ", " + TourneyHeroesText(kl);
                var g = kl != null && kl.Town != null ? Campaign.Current.TournamentManager.GetTournamentGame(kl.Town) : null;
                st += ", turniej w miescie " + (g != null ? "TAK" : "NIE");
            }
            return "turniej: " + st;
        }
    }
}
