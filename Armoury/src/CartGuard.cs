using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// m106-wozy W2 (22.10, diagnoza scratchpad wozy/diagnoza-wozow.md P2, P4, P5): STRAZNIK ZATKANEGO WOZU WSI i linia doby "Wozy (m106)".
    /// Gra nie wystawi wsi nowego wozu, dopoki stary istnieje poza domem (VillagerCampaignBehavior.ThinkAboutSendingItemToTown), a wysyla woz z domu tylko przy magazynie >= W -
    /// woz, ktory utknal (brzeg bez drogi ladowej - Harbay i Volmark 165-170 dob, brama wroga, obcy zamek, w domu z jukami i rozkazem - King's Valley 154 doby), zeruje utarg wsi
    /// na miesiace. Raz na dobe (klucz Village Cart Stuck Guard):
    ///  - woz w polu: postep = droga do celu (pamiec drog gry) krotsza o ponad 1 jedn. od najlepszej w tym kursie; bez postepu ponad max(2T, 3) dob (T - szacunek kursu z wyboru
    ///    miasta, MarketCarts.Choose; bez wyboru - 2 d(dom, cel) / tempo + czekanie) albo cel bez drogi ladowej z miejsca, gdzie stoi - do domu (gdy dom osiagalny ladem),
    ///    inaczej do najblizszego niewrogiego, nieoblezonego miasta osiagalnego ladem i bez bramy wroga; najwyzej 2 razy na kurs (kurs - od wyjazdu do powrotu do domu albo
    ///    sprzedazy w miescie). Pomijany tej doby, gdy bitwa, tratwa albo ucieczka (FleeToPoint);
    ///  - woz w obcej osadzie (zamek albo wies, nie dom; miasta - CartTownExit): od pierwszej obserwacji ponad max(2T, 3) dob - jak wyzej (nie przy oblezeniu i bitwie);
    ///  - woz w domu z jukami (towar bez koni) i rozkazem jazdy (GoToSettlement, cel nie dom): po 5 dobach (W = 5 dob produkcji) AI wlaczone i swiezy wybor miasta
    ///    (MarketRoad.PickTown; bez wyboru - stary cel, przy W1 bez drogi ladowej najblizsze miasto ladem), po 10 dobach towar z jukow do magazynu wsi (konie zostaja; dosypka
    ///    przy wyjezdzie skreslona) i rozkaz do domu - gra zaladuje i wysle woz, gdy magazyn dojdzie do W; po 20 dobach od rozladunku woz wyslany ponownie i dalej stojacy -
    ///    licznik "bez wyjscia" i koniec dzialan w tym epizodzie (niewyslany - koniec epizodu bez licznika).
    ///  - woz w obcej osadzie: "do domu / do miasta" liczone nastepnej doby, gdy woz wyjechal (inaczej "rozkaz odrzucony"); "ponad 30 dob" od pierwszej doby w osadzie.
    /// Jednorazowo w pierwszej dobie sesji (klucz Village Cart Land Road Only): woz w polu, ktorego cel nie ma drogi ladowej z miejsca, gdzie stoi - do domu (zapisy z wozami
    /// juz stojacymi na brzegu). Zamknieta ekonomia: tylko rozkazy gry (MarketRoad.Move - trasa gry) i towar miedzy jukami a magazynem tej samej wsi; zloto sie nie rusza.
    /// H1 (tylko log): przyczyny, dla ktorych woz w domu z rozkazem nie wyjezdza (warunek gry MobileParty.CheckExitingSettlementParallel: AI wylaczone, Hold, cel chwilowy =
    /// ta osada; tratwa; predkosc), co 5 dob 5 przykladow.
    /// Klucz wylaczony - nic (bez pamieci i dzialan; linia doby z zerami i dopiskiem WYLACZONY); wszystkie piec kluczy W2 wylaczone - bez linii doby.
    /// m107-gospodarka K1 (Village Cart Flee Unload, przy Village Cart Stuck Guard; WZ-1b: 1355 z 1355 przypadkow H1 to "cel chwilowy = osada"): woz z jukami i rozkazem jazdy,
    /// ktorego cel chwilowy gry to wlasna wies (ucieczka: wynik inicjatywy > 1 - MobilePartyAi.GetBehaviors, potem GetFleeBehavior trzyma woz w osadzie; bandy z domem w tej
    /// wsi kraza wokol niej), po FleeUnloadDays dobach w tym stanie (wies Normal, bez bitwy wozu i wsi): juki do magazynu tej wsi, rozkaz do domu i WSTRZYMANIE wsi - prefiks
    /// VillagerCampaignBehavior.ThinkAboutSendingItemToTown zwraca false (gra nie laduje i nie wysyla wozu ponownie - dotad cofala rozladunek po 10 dobach). Raz na dobe
    /// wstrzymana wies sprawdzana tym samym modelem AI gry (MobilePartyAIModel.GetBestInitiativeBehavior: wynik > 1 i zachowanie ucieczki); brak zagrozenia, brak wozu albo
    /// wyjatek - zdjecie wstrzymania (bez limitu dob: woz rusza najpozniej dobe po odejsciu wroga). Pomiar: wsie bez wozu ponad 14 dob z przyczyna wedlug warunku gry.
    /// Zamknieta ekonomia: towar tylko miedzy jukami a magazynem tej samej wsi, zloto bez ruchu. Wstrzymanie sesyjne (bez zapisu). Klucz wylaczony - jak w 5da5f304.
    /// </summary>
    internal static class CartGuard
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        internal const int HomeOrderDays = 5, HomeUnloadDays = 10, HomeNoExitDays = 20, MaxActs = 2, LongDays = 30, ExampleEvery = 5;

        private sealed class St
        {
            public Settlement To; public double T = -1; public int Start, LastProg, Acts; public float BestD = float.MaxValue; public bool NoExit;
            public Settlement InAt; public int InSince = -1;
            public int FirstIn = -1;                                       // recenzja Z6: pierwsza doba w tej obcej osadzie (Act jej nie zeruje - licznik "ponad 30 dob")
            public Settlement PendAt, PendGo;                              // recenzja Z6: rozkaz z obcej osady - sukces liczony nastepnej doby, gdy woz z niej wyjechal
            public int HomeSince = -1, Unload = -1; public bool Ordered, HomeDone;
            public int FleeSince = -1;                                     // m107 K1: pierwsza doba w domu z jukami, rozkazem i celem chwilowym = dom (ucieczka gry)
        }
        private sealed class Note { public Settlement To; public double T; public int Day; }
        private sealed class Exm { public int Key; public MobileParty Cart; public Settlement Home; public string What; public Exm(int k, MobileParty c, Settlement h, string w) { Key = k; Cart = c; Home = h; What = w; } }

        private static readonly Dictionary<MobileParty, St> _st = new Dictionary<MobileParty, St>();
        private static readonly Dictionary<MobileParty, Note> _notes = new Dictionary<MobileParty, Note>();
        private static bool _onceDone;
        private static readonly HashSet<string> _errSites = new HashSet<string>();

        // ------------------------------------------------------------ liczniki doby
        private static int _tracked, _over, _overField, _overForeign, _toHome, _toTown, _aiOn, _rejected, _noExit;
        private static int _homeOver, _homeOrder, _homeUnload, _homeUnloadUnits, _homeNoExit, _y1, _y2, _y3;
        private static int _w1Alt, _w1None, _once, _stumbles;
        private static int _h1Ai, _h1Hold, _h1Short, _h1Raft, _h1Speed, _h1Other;

        internal static bool On { get { var s = Settings.Current; return s != null && s.VillageCartStuckGuard; } }

        // ------------------------------------------------------------ m107 K1: ucieczka w domu - rozladunek i wstrzymanie zaladunku
        internal const int FleeUnloadDays = 3, NoCartDays = 14, M107ExMax = 3;
        private static readonly Dictionary<Village, int> _fleeHold = new Dictionary<Village, int>();     // wies wstrzymana -> doba wstrzymania (sesja)
        private static readonly Dictionary<Village, int> _noCartSince = new Dictionary<Village, int>();  // wies bez wozu -> pierwsza doba bez wozu (pomiar)
        private static bool _thinkWired;
        private static List<string> _ex7;                                                                 // przyklady m107 tej doby (co 5 dob) albo null
        private static int _fuUnload, _fuUnits, _fuNew, _fuFree, _fuChecks, _fuStill, _fuLong, _fuBlocked;
        private static int _ncOver, _ncStore, _ncHearth, _ncState, _ncOther;

        /// <summary>K1 dziala przy wlaczonym strazniku (Home wolany z Guard).</summary>
        internal static bool FleeOn { get { var s = Settings.Current; return s != null && s.VillageCartFleeUnload && s.VillageCartStuckGuard; } }

        /// <summary>297b (linia "Kopalnie rudy (297b)"): doba wstrzymania wsi (m107 K1) albo -1.</summary>
        internal static int HeldSince(Village v) { int d; return v != null && _fleeHold.TryGetValue(v, out d) ? d : -1; }

        internal static void Reset()
        {
            _st.Clear(); _notes.Clear(); _onceDone = false; _errSites.Clear();
            _fleeHold.Clear(); _noCartSince.Clear();
            CartEscort.Reset();   // 297b: eskorta milicji (pamiec sesji)
            ClearDay();
        }

        private static void ClearDay()
        {
            _tracked = _over = _overField = _overForeign = _toHome = _toTown = _aiOn = _rejected = _noExit = 0;
            _homeOver = _homeOrder = _homeUnload = _homeUnloadUnits = _homeNoExit = _y1 = _y2 = _y3 = 0;
            _w1Alt = _w1None = _once = _stumbles = 0;
            _h1Ai = _h1Hold = _h1Short = _h1Raft = _h1Speed = _h1Other = 0;
            _fuUnload = _fuUnits = _fuNew = _fuFree = _fuChecks = _fuStill = _fuLong = _fuBlocked = 0;
            _ncOver = _ncStore = _ncHearth = _ncState = _ncOther = 0;
        }

        /// <summary>m107 K1: MarketRoad.ApplyAll - prefiks VillagerCampaignBehavior.ThinkAboutSendingItemToTown (wstrzymanie zaladunku wsi pod ucieczka). Brak metody - linia
        /// "BRAK" (sam rozladunek dalej dziala; gra moze zaladowac ponownie - jak dotad).</summary>
        internal static void Wire(Harmony h)
        {
            try
            {
                var m = AccessTools.DeclaredMethod(typeof(VillagerCampaignBehavior), "ThinkAboutSendingItemToTown");
                if (m == null) { Log.Info("Wozy (m107): BRAK VillagerCampaignBehavior.ThinkAboutSendingItemToTown - wstrzymanie zaladunku wylaczone (rozladunek wozu pod ucieczka dziala)."); return; }
                h.Patch(m, prefix: new HarmonyMethod(typeof(CartGuard), nameof(ThinkPrefix)));
                _thinkWired = true;
                Log.Info("Wozy (m107): latka VillagerCampaignBehavior.ThinkAboutSendingItemToTown wpieta (wstrzymanie zaladunku wsi pod ucieczka, klucz Village Cart Flee Unload).");
            }
            catch (Exception e) { Log.Error("CartGuard.Wire", e); }
        }

        /// <summary>m107 K1: wies wstrzymana (rozladowany woz pod ucieczka) - gra nie laduje, nie dobiera ludzi i nie wystawia wozu (return false). Inaczej - gra jak dotad.</summary>
        private static bool ThinkPrefix(Village village)
        {
            try
            {
                if (_fleeHold.Count == 0 || village == null || !_fleeHold.ContainsKey(village) || !FleeOn) return true;
                _fuBlocked++;
                return false;
            }
            catch { return true; }
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_errSites.Add(where)) Log.Error(where, e);
        }

        private static int Today() { try { return (int)CampaignTime.Now.ToDays; } catch { return 0; } }

        private static string Name(Settlement s) { try { return s != null && s.Name != null ? s.Name.ToString() : "?"; } catch { return "?"; } }

        // ------------------------------------------------------------ drogi (pamiec drog gry, droga ladowa)
        /// <summary>Osada -> osada: brak drogi ladowej (pamiec drog: 1e30 przy NavalDLC, 1e8 poza tablica; &lt;= 0 albo NaN - tez brak).</summary>
        internal static bool NoLandRoad(float d) { return float.IsNaN(d) || d <= 0f || d >= CartTownExit.BkLimit; }

        internal static float DistSS(Settlement a, Settlement b)
        {
            return Campaign.Current.Models.MapDistanceModel.GetDistance(a, b, false, false, MobileParty.NavigationType.Default);
        }

        private static float DistParty(MobileParty mp, Settlement s)
        {
            float lr;
            return Campaign.Current.Models.MapDistanceModel.GetDistance(mp, s, false, MobileParty.NavigationType.Default, out lr);
        }

        /// <summary>Droga ladowa wozu do osady: z osady, w ktorej stoi, albo z miejsca w polu.</summary>
        private static float Dist(MobileParty cart, Settlement s) { var at = cart.CurrentSettlement; return at != null ? DistSS(at, s) : DistParty(cart, s); }

        /// <summary>Najblizsze (droga ladowa) miasto: niewrogie frakcji wozu, nieoblezone, osiagalne ladem z miejsca wozu, bez bramy wroga po drodze (gate; klucz Village Cart
        /// Enemy Gate), inne niz osada, w ktorej woz stoi; w dowolnej odleglosci. null - brak.</summary>
        internal static Settlement NearestReachableTown(MobileParty cart, Settlement home, bool gate)
        {
            if (cart == null) return null;
            try
            {
                var at = cart.CurrentSettlement;
                Settlement best = null; float bd = float.MaxValue;
                foreach (var t in Town.AllTowns)
                {
                    var st = t != null ? t.Settlement : null;
                    if (st == null || st == at || st.IsUnderSiege || MarketCarts.Hostile(cart, home ?? st, st)) continue;
                    float d = at != null ? DistSS(at, st) : DistParty(cart, st);
                    if (NoLandRoad(d) || d >= bd) continue;
                    if (gate && CartRisk.GateBlocked(cart, home, at, st, d) != null) continue;
                    best = st; bd = d;
                }
                return best;
            }
            catch (Exception e) { Stumble("CartGuard.NearestReachableTown", e); return null; }
        }

        /// <summary>MarketRoad.RoutePrefix (klucz Village Cart Land Road Only): miasto wsi za morzem - zastepcze miasto ladem (true) albo brak celu ladem (false).</summary>
        internal static void NoteW1(bool alt) { if (alt) _w1Alt++; else _w1None++; }

        /// <summary>MarketCarts.Choose: szacunek kursu T (doby tam i z powrotem z czekaniem) dla wybranego miasta - straznik liczy z niego prog bez postepu.</summary>
        internal static void NoteTrip(MobileParty cart, Settlement to, double T)
        {
            if (cart == null || to == null || !On) return;
            try { _notes[cart] = new Note { To = to, T = T, Day = Today() }; } catch (Exception e) { Stumble("CartGuard.NoteTrip", e); }
        }

        /// <summary>MarketRoad.OnPartyDestroyed: woz zniknal - pamiec straznika wygasa.</summary>
        internal static void Forget(MobileParty party)
        {
            if (party == null) return;
            _st.Remove(party); _notes.Remove(party);
        }

        private static VillagerCampaignBehavior Vcb()
        {
            try { return Campaign.Current != null ? Campaign.Current.GetCampaignBehavior<VillagerCampaignBehavior>() : null; } catch { return null; }
        }

        /// <summary>Szacunek kursu bez wyboru miasta (BK albo gra): 2 d(dom, cel) / tempo wozu + czekanie (MarketCarts.CycleWait); bez drogi - 7 dob.</summary>
        private static double Est(Settlement home, Settlement to)
        {
            try
            {
                var s = Settings.Current;
                if (home == null || to == null || s == null) return 7.0;
                float d = DistSS(home, to);
                if (NoLandRoad(d)) return 7.0;
                double pd = MarketCarts.PerDay(s);
                return pd > 0 ? 2.0 * d / pd + MarketCarts.CycleWait : 7.0;
            }
            catch { return 7.0; }
        }

        private static int Lim(double T) { return Math.Max(3, (int)Math.Ceiling(2.0 * Math.Max(0.0, T))); }

        private static bool Skip(MobileParty cart)
        {
            return cart.MapEvent != null || cart.IsInRaftState || cart.ShortTermBehavior == AiBehavior.FleeToPoint;
        }

        /// <summary>Woz na nowy cel: do domu, gdy dom osiagalny ladem z miejsca wozu, inaczej do najblizszego osiagalnego niewrogiego miasta. AI wlaczone, gdy wylaczone
        /// (MobilePartyAi.EnableAi). Rozkaz gry (trasa gry); kontrola jak w CartTownExit: GoToSettlement i cel = nadany. null - bez celu albo rozkaz odrzucony.
        /// defer (woz w obcej osadzie, recenzja Z6): "do domu / do miasta" liczone dopiero nastepnej doby, gdy woz naprawde wyjechal (ucieczka gry - GetFleeBehavior - trzyma
        /// woz w osadzie mimo przyjetego rozkazu).</summary>
        private static Settlement Rescue(MobileParty cart, Settlement home, VillagerCampaignBehavior vcb, bool defer = false)
        {
            if (cart.Ai != null && cart.Ai.IsDisabled) { cart.Ai.EnableAi(); _aiOn++; }
            Settlement goal = null;
            if (home != null && cart.CurrentSettlement != home)
            {
                float dh = Dist(cart, home);
                if (!NoLandRoad(dh)) goal = home;
            }
            if (goal == null) goal = NearestReachableTown(cart, home, true);
            if (goal == null) { _noExit++; return null; }
            MarketRoad.Move(vcb, cart, goal);
            if (cart.DefaultBehavior == AiBehavior.GoToSettlement && cart.TargetSettlement == goal)
            {
                if (!defer) { if (goal == home) _toHome++; else _toTown++; }
                return goal;
            }
            _rejected++;
            return null;
        }

        private static bool HasLoad(MobileParty cart)
        {
            var r = cart.ItemRoster;
            if (r == null) return false;
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                if (el.Amount > 0 && el.EquipmentElement.Item != null && !el.EquipmentElement.Item.HasHorseComponent) return true;
            }
            return false;
        }

        private static int LoadUnits(MobileParty cart)
        {
            int n = 0; var r = cart.ItemRoster;
            if (r != null) for (int i = 0; i < r.Count; i++) { var el = r.GetElementCopyAtIndex(i); if (el.Amount > 0 && el.EquipmentElement.Item != null && !el.EquipmentElement.Item.HasHorseComponent) n += el.Amount; }
            return n;
        }

        /// <summary>Towar z jukow (bez koni) do magazynu wsi domowej - pozycja po pozycji (najpierw zdjecie z jukow, potem do magazynu; gdy dolozenie sie nie uda - towar wraca).</summary>
        private static int Unload(MobileParty cart, Settlement home)
        {
            var r = cart.ItemRoster; var store = home.ItemRoster;
            if (r == null || store == null) return 0;
            var list = new List<KeyValuePair<EquipmentElement, int>>();
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                if (el.Amount > 0 && el.EquipmentElement.Item != null && !el.EquipmentElement.Item.HasHorseComponent) list.Add(new KeyValuePair<EquipmentElement, int>(el.EquipmentElement, el.Amount));
            }
            int moved = 0;
            foreach (var l in list)
            {
                r.AddToCounts(l.Key, -l.Value);
                try { store.AddToCounts(l.Key, l.Value); moved += l.Value; }
                catch (Exception e) { r.AddToCounts(l.Key, l.Value); Stumble("CartGuard.Unload", e); break; }
            }
            return moved;
        }

        // ------------------------------------------------------------ sesja i doba
        /// <summary>OnSessionLaunched: linia startu (klucze W2 i pamiec tras z zapisu).</summary>
        internal static void SessionStart()
        {
            _onceDone = false;
            try
            {
                var s = Settings.Current;
                if (s == null) return;
                int loaded = CartRisk.Loaded;
                Log.Info("Wozy (m106): start sesji | Village Cart Land Road Only " + (s.VillageCartLandRoadOnly ? "TAK" : "NIE")
                         + " | Village Cart Enemy Gate " + (s.VillageCartEnemyGate ? "TAK" : "NIE")
                         + " | Village Cart Route Risk " + (s.VillageCartRouteRisk ? "TAK" : "NIE") + " (par z zapisu " + Math.Max(0, loaded) + (loaded < 0 ? " - brak klucza w zapisie" : "")
                         + ", przezywalnosc swiata " + CartRisk.WorldNow().ToString("0.000", Inv) + ", polowa po " + CartRisk.HalfDays.ToString("0", Inv) + " dobach, start neutralny k="
                         + CartRisk.Prior.ToString("0", Inv) + ")"
                         + " | Village Cart Stuck Guard " + (s.VillageCartStuckGuard ? "TAK" : "NIE") + " (brak postepu > max(2T,3) doby; w domu z jukami " + HomeOrderDays + "/" + HomeUnloadDays + "/"
                         + HomeNoExitDays + " dob; najwyzej " + MaxActs + " razy na kurs)"
                         + " | Village Cart Top Up On Departure " + (s.VillageCartTopUpOnDeparture ? "TAK" : "NIE") + ".");
            }
            catch (Exception e) { Stumble("CartGuard.SessionStart", e); }
        }

        /// <summary>Raz na dobe (po CartTownExit.Daily): jednorazowe zawrocenie wozow bez drogi (pierwsza doba sesji), straznik i linia "Wozy (m106): dzien".</summary>
        internal static void Daily()
        {
            var s = Settings.Current;
            if (s == null) return;
            long t0 = Stopwatch.GetTimestamp();
            int today = Today(), day = today - 1;
            var vcb = Vcb();
            var ex = new List<Exm>();
            bool exDay = day % ExampleEvery == 0;
            if (!_onceDone)
            {
                _onceDone = true;
                if (s.VillageCartLandRoadOnly) { try { Once(vcb); } catch (Exception e) { Stumble("CartGuard.Once", e); } }
            }
            bool on = s.VillageCartStuckGuard;
            if (!on && !s.VillageCartLandRoadOnly && !s.VillageCartEnemyGate && !s.VillageCartRouteRisk && !s.VillageCartTopUpOnDeparture)
            {
                // recenzja Z11: wszystkie klucze W2 wylaczone - bez linii doby (kod jak w a7a767e8); liczniki doby tylko zerowane
                _st.Clear(); _notes.Clear();
                MarketCarts.TakeM106(); CartRisk.SkipDay(); ClearDay();
                CartEscort.Daily(day, false, true);   // 297b: eskorty w domu wracaja do milicji (tylko gdy jakies sa), bez linii doby (recenzja 297b: jak reszta tej drogi)
                return;
            }
            var ex7 = new List<string>();
            bool flee = on && s.VillageCartFleeUnload;   // m107 K1 (przy strazniku)
            _ex7 = exDay ? ex7 : null;
            if (flee) { try { HoldCheck(today, _ex7); } catch (Exception e) { Stumble("CartGuard.HoldCheck", e); } }   // przed Guard: wstrzymane wczoraj i wczesniej
            else { _fleeHold.Clear(); _noCartSince.Clear(); }
            if (on) Guard(s, today, vcb, exDay ? ex : null);
            else { _st.Clear(); _notes.Clear(); }
            CartRisk.Tidy();
            double gateMs;
            var m = MarketCarts.TakeM106();
            var sb = new StringBuilder(2400);
            sb.Append("Wozy (m106): dzien ").Append(day)
              .Append(" | W1 drogi").Append(s.VillageCartLandRoadOnly ? "" : " - WYLACZONE").Append(": odrzucone bez drogi ladowej ").Append(m.W1Cut).Append(" (wlasne miasto ").Append(m.W1Own)
              .Append("), wlokno bez drogi ").Append(m.W1Fibre).Append(", zastepcze miasto ladem ").Append(_w1Alt).Append(", bez celu ladem ").Append(_w1None)
              .Append(", jednorazowo zawrocone ").Append(_once)
              .Append(" | brama wroga").Append(s.VillageCartEnemyGate ? "" : " - WYLACZONA").Append(": odrzucone ").Append(m.GateCut).Append(" (wozow ").Append(m.GateCarts)
              .Append("; najczesciej ").Append(m.GateTop ?? "-").Append("), wszystkie za brama ").Append(m.GateAll);
            CartRisk.DayText(sb, m.RiskChanged, out gateMs);
            sb.Append(" | straznik").Append(on ? "" : " - WYLACZONY").Append(": sledzone ").Append(_tracked).Append(", bez postepu ponad prog ").Append(_overField + _overForeign)
              .Append(" (w drodze ").Append(_overField).Append(", w obcej osadzie ").Append(_overForeign).Append("), do domu ").Append(_toHome).Append(", do miasta ").Append(_toTown)
              .Append(", AI wlaczone ").Append(_aiOn).Append(", rozkaz odrzucony ").Append(_rejected).Append(", bez wyjscia ").Append(_noExit)
              .Append("; w domu z jukami ponad ").Append(HomeOrderDays).Append(" dob ").Append(_homeOver).Append(" (nowy rozkaz ").Append(_homeOrder).Append(", rozladowane ").Append(_homeUnload)
              .Append(" szt. ").Append(_homeUnloadUnits).Append(", bez wyjscia ").Append(_homeNoExit).Append("); bez postepu ponad ").Append(LongDays).Append(" dob ").Append(_y1 + _y2 + _y3)
              .Append(" (w drodze ").Append(_y1).Append(", w obcej osadzie ").Append(_y2).Append(", w domu ").Append(_y3).Append(")")
              .Append(" | dosypka").Append(s.VillageCartTopUpOnDeparture ? "" : " - WYLACZONA").Append(": przy wyjezdzie ").Append(m.TuDone).Append(" (szt. ").Append(m.TuUnits).Append("), czeka ").Append(m.TuWait)
              .Append(", przepadla ").Append(m.TuLost)
              .Append(" | H1 w domu: AI wylaczone ").Append(_h1Ai).Append(", Hold ").Append(_h1Hold).Append(", cel chwilowy = osada ").Append(_h1Short).Append(", tratwa ").Append(_h1Raft)
              .Append(", predkosc 0 ").Append(_h1Speed).Append(", inne ").Append(_h1Other);
            // m107 K1: ucieczka w domu i wsie bez wozu (klucz wylaczony - zera i dopisek WYLACZONE)
            sb.Append(" | m107 ucieczka w domu (Village Cart Flee Unload ").Append(flee ? "TAK" : "NIE - WYLACZONE").Append(_thinkWired ? "" : ", latka zaladunku BRAK").Append("): rozladowane ").Append(_fuUnload)
              .Append(" (szt. ").Append(_fuUnits).Append("), wsie wstrzymane ").Append(_fleeHold.Count).Append(" (nowe ").Append(_fuNew).Append(", zwolnione ").Append(_fuFree)
              .Append(", sprawdzenia ").Append(_fuChecks).Append(", wrog dalej ").Append(_fuStill).Append(", ponad ").Append(LongDays).Append(" dob ").Append(_fuLong).Append("), odmowy zaladunku ").Append(_fuBlocked)
              .Append(" | bez wozu ponad ").Append(NoCartDays).Append(" dob ").Append(_ncOver).Append(" (magazyn < W ").Append(_ncStore).Append(", ogniska ponizej minimum ").Append(_ncHearth)
              .Append(", wies nie Normal ").Append(_ncState).Append(", inne ").Append(_ncOther).Append(')');
            double ms = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
            sb.Append(" | czas ").Append(ms.ToString("0.00", Inv)).Append(" ms (brama w wyborach ").Append(gateMs.ToString("0.00", Inv)).Append(" ms)")
              .Append(" | potkniecia ").Append(_stumbles + CartRisk.StumblesDay + m.Stumbles).Append('.');
            Log.Info(sb.ToString());
            if (ex.Count > 0)
            {
                ex.Sort((a, b) => b.Key.CompareTo(a.Key));
                int n = Math.Min(5, ex.Count);
                for (int i = 0; i < n; i++) Log.Info("Wozy (m106) przyklad: dzien " + day + " - " + (i + 1) + "/" + n + " " + Ex(ex[i].Cart, ex[i].Home, ex[i].What) + ".");   // pola i najblizszy wrog tylko dla 5 przykladow
            }
            foreach (var l in ex7) Log.Info(l);   // m107 K1: przyklady wstrzymanych i bez wozu (co 5 dob, najwyzej 3 i 3)
            _ex7 = null;
            ClearDay();
            CartEscort.Daily(day, on, false);     // 297b: ksiega eskort milicji i linie "Eskorta wozow (297b)", "Kopalnie rudy (297b)"
        }

        /// <summary>m107 K1: raz na dobe kazda wstrzymana wies - woz brak / nie w domu / nieczynny - zdjecie; inaczej zagrozenie wedlug modelu AI gry (to samo zapytanie, ktore gra
        /// zadaje wozowi co godzine: GetBestInitiativeBehavior, wynik &gt; 1 i zachowanie ucieczki - MobilePartyAi.GetBehaviors); brak zagrozenia albo wyjatek - zdjecie.
        /// Wolane w dobie kampanii (miedzy tickami AI - statyczna lista DangerousPartiesAndTheirVecs gra buduje od nowa przy kazdym wywolaniu). Przyklady co 5 dob.</summary>
        private static void HoldCheck(int today, List<string> ex7)
        {
            if (_fleeHold.Count == 0) return;
            var model = Campaign.Current != null && Campaign.Current.Models != null ? Campaign.Current.Models.MobilePartyAIModel : null;
            var free = new List<Village>();
            int shown = 0;
            foreach (var kv in _fleeHold)
            {
                var v = kv.Key;
                try
                {
                    var cart = v != null && v.VillagerPartyComponent != null ? v.VillagerPartyComponent.MobileParty : null;
                    if (cart == null || !cart.IsActive || v.Settlement == null || cart.CurrentSettlement != v.Settlement || model == null) { free.Add(v); continue; }
                    _fuChecks++;
                    AiBehavior b; MobileParty foe; float sc; Vec2 av;
                    bool threat;
                    try { model.GetBestInitiativeBehavior(cart, out b, out foe, out sc, out av); threat = sc > 1f && MobileParty.IsFleeBehavior(b); }
                    catch (Exception e) { Stumble("CartGuard.HoldCheck(model)", e); threat = false; b = AiBehavior.None; foe = null; sc = 0f; }
                    if (!threat) { free.Add(v); continue; }
                    // 297b: zagrozenie trwa, magazyn >= W - eskorta milicji wsi (CartEscort); dostala albo zagrozenia juz nie ma - zdjecie wstrzymania (gra laduje i wysyla woz)
                    if (CartEscort.HoldStep(cart, v, model)) { free.Add(v); continue; }
                    _fuStill++;
                    int since = today - kv.Value;
                    if (since > LongDays) _fuLong++;
                    if (ex7 != null && shown < M107ExMax)
                    {
                        shown++;
                        string fn; try { fn = foe != null && foe.Name != null ? foe.Name.ToString() + " [" + foe.StringId + "]" : "-"; } catch { fn = "?"; }
                        ex7.Add("Wozy (m107) wstrzymana: wies " + Name(v.Settlement) + " [" + v.Settlement.StringId + "] od " + since + " dob, wrog " + fn + " (wynik " + sc.ToString("0.00", Inv)
                                + ", " + b + "), magazyn " + StoreText(v) + ".");
                    }
                }
                catch (Exception e) { Stumble("CartGuard.HoldCheck(wies)", e); free.Add(v); }
            }
            foreach (var v in free) { _fleeHold.Remove(v); _fuFree++; }
        }

        private static string StoreText(Village v)
        {
            int store = 0, W = 0;
            try { var r = v.Settlement.ItemRoster; if (r != null) for (int i = 0; i < r.Count; i++) store += r.GetElementNumber(i); W = v.GetWarehouseCapacity(); } catch { }
            return store + "/" + W;
        }

        /// <summary>m107 K1 (pomiar): wies bez wozu (VillagerPartyComponent null albo woz nieczynny) - doba pierwszej obserwacji; ponad NoCartDays dob - przyczyna wedlug warunku gry
        /// przed wystawieniem wozu (VillagerCampaignBehavior.TickVillageThink / ThinkAboutSendingItemToTown: wies Normal, magazyn &gt;= W, ogniska &gt; minimum gry). Tylko log.</summary>
        private static void NoCart(Village v, int today, List<string> ex7)
        {
            int since;
            if (!_noCartSince.TryGetValue(v, out since)) { _noCartSince[v] = today; return; }
            int d = today - since;
            if (d <= NoCartDays) return;
            _ncOver++;
            int store = 0, W = 0, min = 0;
            try { var r = v.Settlement.ItemRoster; if (r != null) for (int i = 0; i < r.Count; i++) store += r.GetElementNumber(i); W = v.GetWarehouseCapacity(); } catch { }
            try { min = Campaign.Current.Models.PartySizeLimitModel.MinimumNumberOfVillagersAtVillagerParty; } catch { }
            string why;
            if (v.VillageState != Village.VillageStates.Normal) { _ncState++; why = "wies " + v.VillageState; }
            else if (store < W) { _ncStore++; why = "magazyn < W"; }
            else if (v.Hearth <= min) { _ncHearth++; why = "ogniska ponizej minimum"; }
            else { _ncOther++; why = "inne"; }
            if (ex7 != null && _ncOver <= M107ExMax)
                ex7.Add("Wozy (m107) bez wozu: wies " + Name(v.Settlement) + " [" + (v.Settlement != null ? v.Settlement.StringId : "?") + "] od " + d + " dob, magazyn " + store + "/" + W
                        + ", ogniska " + v.Hearth.ToString("0", Inv) + " (min " + min + "), " + why + ".");
        }

        /// <summary>Klucz Village Cart Land Road Only, pierwsza doba sesji: woz w polu, ktorego cel nie ma drogi ladowej z miejsca, gdzie stoi (pamiec drog &gt;= prog BK) - nowy cel.</summary>
        private static void Once(VillagerCampaignBehavior vcb)
        {
            foreach (var v in Village.All)
            {
                try
                {
                    var cart = v != null && v.VillagerPartyComponent != null ? v.VillagerPartyComponent.MobileParty : null;
                    if (cart == null || !cart.IsActive || cart.CurrentSettlement != null || cart.MapEvent != null) continue;
                    var tg = cart.TargetSettlement;
                    if (tg == null) continue;
                    float d = DistParty(cart, tg);
                    if (!(float.IsNaN(d) || d >= CartTownExit.BkLimit)) continue;
                    if (Rescue(cart, cart.HomeSettlement, vcb) != null) _once++;
                }
                catch (Exception e) { Stumble("CartGuard.Once(woz)", e); }
            }
        }

        private static void Guard(Settings s, int today, VillagerCampaignBehavior vcb, List<Exm> ex)
        {
            var present = new HashSet<MobileParty>();
            foreach (var v in Village.All)
            {
                try
                {
                    var cart = v != null && v.VillagerPartyComponent != null ? v.VillagerPartyComponent.MobileParty : null;
                    if (cart == null || !cart.IsActive)
                    {
                        if (v != null && v.Settlement != null && FleeOn) NoCart(v, today, _ex7);   // m107 K1: pomiar wsi bez wozu
                        continue;
                    }
                    if (_noCartSince.Count > 0) _noCartSince.Remove(v);
                    present.Add(cart);
                    _tracked++;
                    St st;
                    if (!_st.TryGetValue(cart, out st)) { st = new St(); _st[cart] = st; }
                    var home = cart.HomeSettlement; var at = cart.CurrentSettlement;
                    if (st.PendAt != null)
                    {
                        // recenzja Z6: rozkaz wydany wczoraj w obcej osadzie - "do domu / do miasta" dopiero, gdy woz z niej wyjechal; stoi dalej - "rozkaz odrzucony"
                        if (at != st.PendAt) { if (st.PendGo == home) _toHome++; else _toTown++; } else _rejected++;
                        st.PendAt = st.PendGo = null;
                    }
                    if (at == null) Field(cart, st, home, today, vcb, ex);
                    else if (at == home) Home(s, cart, st, v, home, today, vcb, ex);
                    else if (at.IsTown) { ClearHome(st); st.InAt = null; st.InSince = -1; st.FirstIn = -1; st.To = null; st.Acts = 0; st.NoExit = false; }   // miasto - CartTownExit; sprzedaz = koniec kursu
                    else Foreign(cart, st, at, home, today, vcb, ex);
                }
                catch (Exception e) { Stumble("CartGuard.Guard(woz)", e); }
            }
            List<MobileParty> gone = null;
            foreach (var k in _st.Keys) if (!present.Contains(k)) (gone ?? (gone = new List<MobileParty>())).Add(k);
            if (gone != null) foreach (var k in gone) _st.Remove(k);
            gone = null;
            foreach (var kv in _notes) if (!present.Contains(kv.Key) || today - kv.Value.Day > 60) (gone ?? (gone = new List<MobileParty>())).Add(kv.Key);
            if (gone != null) foreach (var k in gone) _notes.Remove(k);
        }

        private static void ClearHome(St st) { st.HomeSince = -1; st.Unload = -1; st.Ordered = false; st.HomeDone = false; st.FleeSince = -1; }   // m107: FleeSince - poza domem od nowa

        private static void Field(MobileParty cart, St st, Settlement home, int today, VillagerCampaignBehavior vcb, List<Exm> ex)
        {
            ClearHome(st); st.InAt = null; st.InSince = -1; st.FirstIn = -1;
            var tg = cart.TargetSettlement;
            if (tg == null || cart.DefaultBehavior != AiBehavior.GoToSettlement) { st.To = null; return; }   // bez celu (Hold, ucieczka) - gra wyda rozkaz sama co godzine
            float d = DistParty(cart, tg);
            if (tg != st.To)
            {
                st.To = tg; st.Start = today; st.BestD = float.IsNaN(d) ? float.MaxValue : d; st.LastProg = today;
                Note n;
                st.T = _notes.TryGetValue(cart, out n) && n.To == tg && today - n.Day <= 30 ? n.T : Est(home, tg);
            }
            bool noRoad = float.IsNaN(d) || d >= CartTownExit.BkLimit;
            if (!noRoad && d < st.BestD - 1f) { st.BestD = d; st.LastProg = today; }
            int idle = today - st.LastProg, lim = Lim(st.T);
            if (idle > LongDays) _y1++;
            if (!noRoad && idle <= lim) return;
            _overField++;
            if (ex != null) ex.Add(new Exm(idle, cart, home, "w drodze bez postepu " + idle + " dob (prog " + lim + (noRoad ? ", cel bez drogi ladowej" : "") + "), cel "
                                                                       + (tg.StringId ?? "?") + " d " + (noRoad ? "brak" : d.ToString("0", Inv)) + " jedn."));
            if (Skip(cart)) return;
            Act(cart, st, home, today, vcb);
        }

        private static void Foreign(MobileParty cart, St st, Settlement at, Settlement home, int today, VillagerCampaignBehavior vcb, List<Exm> ex)
        {
            ClearHome(st);
            if (st.InAt != at) { st.InAt = at; st.InSince = today; st.FirstIn = today; }
            double T = st.T >= 0 ? st.T : Est(home, at);
            int idle = today - st.InSince, lim = Lim(T);
            if (today - st.FirstIn > LongDays) _y2++;                       // recenzja Z6: od pierwszej doby w osadzie (InSince zeruje kazde dzialanie)
            if (idle <= lim) return;
            _overForeign++;
            if (ex != null) ex.Add(new Exm(today - st.FirstIn, cart, home, "w obcej osadzie " + (at.StringId ?? "?") + (at.IsCastle ? " (zamek)" : "") + " od " + (today - st.FirstIn)
                                                                               + " dob (prog " + lim + ", od ostatniego dzialania " + idle + ")"));
            if (at.IsUnderSiege || (at.Party != null && at.Party.MapEvent != null) || cart.MapEvent != null) return;
            var goal = Act(cart, st, home, today, vcb, true);
            if (goal != null) { st.PendAt = at; st.PendGo = goal; }
            st.InSince = today;
        }

        /// <summary>Dzialanie straznika na kurs: najwyzej MaxActs razy, potem licznik "bez wyjscia" (raz na kurs). defer - woz w obcej osadzie (Rescue). Zwraca nowy cel albo null.</summary>
        private static Settlement Act(MobileParty cart, St st, Settlement home, int today, VillagerCampaignBehavior vcb, bool defer = false)
        {
            if (st.Acts >= MaxActs)
            {
                if (!st.NoExit) { st.NoExit = true; _noExit++; }
                return null;
            }
            st.Acts++;
            var goal = Rescue(cart, home, vcb, defer);
            st.LastProg = today;
            if (goal != null) { st.To = goal; st.Start = today; st.BestD = float.MaxValue; st.T = Est(home, goal); }
            return goal;
        }

        private static void Home(Settings s, MobileParty cart, St st, Village v, Settlement home, int today, VillagerCampaignBehavior vcb, List<Exm> ex)
        {
            st.To = null; st.Acts = 0; st.NoExit = false; st.InAt = null; st.InSince = -1; st.FirstIn = -1; st.T = -1;   // powrot do domu - koniec kursu
            bool load = HasLoad(cart);
            var tg = cart.TargetSettlement;
            bool order = cart.DefaultBehavior == AiBehavior.GoToSettlement && tg != null && tg != home;
            bool lo = load && order;
            // 297b: eskorta milicji wsi (CartEscort.HomeStep) - zaladowany woz pod ucieczka dostaje eskorte od razu (bez 3 dob i rozladunku), eskorta wozu bez jukow
            // liczona od nowa albo wraca do milicji; klucz wylaczony - eskorta w domu wraca do milicji
            if (CartEscort.HomeStep(cart, v, home, lo)) { ClearHome(st); return; }
            // m107 K1: ucieczka gry trzyma zaladowany woz w domu (cel chwilowy = dom - warunek z H1) - po FleeUnloadDays dobach rozladunek do magazynu tej wsi, rozkaz do domu
            // i wstrzymanie zaladunku (ThinkPrefix) do doby bez zagrozenia (HoldCheck); epizod konczy sie bez krokow 5/10/20. Bez ucieczki - logika jak dotad.
            if (s.VillageCartFleeUnload)
            {
                if (lo && cart.ShortTermTargetSettlement == home)
                {
                    if (st.FleeSince < 0) st.FleeSince = today;
                    if (today - st.FleeSince >= FleeUnloadDays && v.VillageState == Village.VillageStates.Normal && cart.MapEvent == null && (home.Party == null || home.Party.MapEvent == null))
                    {
                        int n = Unload(cart, home);
                        _fuUnload++; _fuUnits += n;
                        MarketCarts.ForgetPending(cart);
                        MarketRoad.Move(vcb, cart, home);
                        if (!_fleeHold.ContainsKey(v)) { _fleeHold[v] = today; _fuNew++; }
                        ClearHome(st); st.FleeSince = -1;
                        return;
                    }
                }
                else st.FleeSince = -1;
            }
            if (st.HomeSince < 0)
            {
                if (!lo) return;
                st.HomeSince = today; st.Unload = -1; st.Ordered = false; st.HomeDone = false;
            }
            if (st.Unload < 0 && !lo) { ClearHome(st); return; }   // przed rozladunkiem: woz bez towaru albo bez rozkazu - koniec epizodu
            int age = today - st.HomeSince;
            if (lo && age > HomeOrderDays)                         // recenzja Z8: H1 tylko dla wozu, ktory stoi ponad zwykla chwile przed wyjazdem (gra wysyla w ticku, woz rusza w nastepnym)
            {
                bool any = false;
                if (cart.Ai != null && cart.Ai.IsDisabled) { _h1Ai++; any = true; }
                if (cart.DefaultBehavior == AiBehavior.Hold || cart.ShortTermBehavior == AiBehavior.Hold) { _h1Hold++; any = true; }
                if (cart.ShortTermTargetSettlement == cart.CurrentSettlement) { _h1Short++; any = true; }
                if (cart.IsInRaftState) { _h1Raft++; any = true; }
                float sp = 0f; try { sp = cart.Speed; } catch { sp = -1f; }
                if (sp >= 0f && sp < 0.1f) { _h1Speed++; any = true; }
                if (!any) _h1Other++;
            }
            if (lo && age > LongDays) _y3++;                       // recenzja Z9: tylko woz z jukami i rozkazem (nie pusty woz po rozladunku, ktorego gra jeszcze nie wyslala)
            if (age <= HomeOrderDays) return;
            if (lo)
            {
                _homeOver++;
                if (ex != null) ex.Add(new Exm(age, cart, home, "w domu z jukami i rozkazem " + age + " dob" + (st.Unload >= 0 ? " (rozladowany " + (today - st.Unload) + " dob temu)" : "")));
            }
            if (st.HomeDone) return;
            if (v.VillageState != Village.VillageStates.Normal || cart.MapEvent != null || (home.Party != null && home.Party.MapEvent != null)) return;
            if (!st.Ordered)
            {
                st.Ordered = true;
                if (cart.Ai != null && cart.Ai.IsDisabled) { cart.Ai.EnableAi(); _aiOn++; }
                var go = MarketRoad.PickTown(cart, home, v, s, false);
                if (go == null)
                {
                    go = tg;                                                     // bez miasta w zasiegu - ten sam cel od nowa (swiezy rozkaz gry)
                    // recenzja D1 (klucz Village Cart Land Road Only): stary cel bez drogi ladowej z domu - najblizsze miasto osiagalne ladem; brak - bez rozkazu
                    if (go != null && s.VillageCartLandRoadOnly && NoLandRoad(DistSS(home, go))) go = NearestReachableTown(cart, home, s.VillageCartEnemyGate);
                }
                if (go == null) return;
                MarketRoad.Move(vcb, cart, go);
                if (cart.DefaultBehavior == AiBehavior.GoToSettlement && cart.TargetSettlement == go) _homeOrder++; else _rejected++;
                return;
            }
            if (st.Unload < 0)
            {
                if (age <= HomeUnloadDays) return;
                int n = Unload(cart, home);
                st.Unload = today;
                _homeUnload++; _homeUnloadUnits += n;
                MarketCarts.ForgetPending(cart);
                // recenzja R4: rozkaz do domu - woz bez celu poza domem (gra nie wysle pustego wozu w stary kurs; ThinkAboutSendingItemToTown zaladuje i wysle go, gdy magazyn dojdzie do W)
                MarketRoad.Move(vcb, cart, home);
                return;
            }
            if (today - st.Unload > HomeNoExitDays)
            {
                if (lo) { st.HomeDone = true; _homeNoExit++; }   // gra wyslala woz ponownie, a on dalej stoi - "bez wyjscia"
                else ClearHome(st);                              // gra jeszcze go nie wyslala (magazyn ponizej W) - koniec epizodu bez licznika
            }
        }

        /// <summary>Przyklad do logu (co 5 dob): wies, stan, zachowania, cel chwilowy, AI, tratwa, predkosc, waga / udzwig, juki, magazyn / W, najblizszy wrog w 5 jedn.</summary>
        private static string Ex(MobileParty cart, Settlement home, string what)
        {
            try
            {
                var v = home != null ? home.Village : null;
                int store = 0, W = 0;
                if (v != null && home.ItemRoster != null) { var r = home.ItemRoster; for (int i = 0; i < r.Count; i++) store += r.GetElementNumber(i); W = v.GetWarehouseCapacity(); }
                float sp = 0f; try { sp = cart.Speed; } catch { sp = -1f; }
                var sb = new StringBuilder(300);
                sb.Append("wies ").Append(Name(home)).Append(" [").Append(home != null ? home.StringId : "?").Append("] - ").Append(what)
                  .Append(", zachowanie ").Append(cart.DefaultBehavior.ToString()).Append(" -> ").Append(cart.TargetSettlement != null ? cart.TargetSettlement.StringId : "-")
                  .Append(", chwilowe ").Append(cart.ShortTermBehavior.ToString()).Append(" -> ").Append(cart.ShortTermTargetSettlement != null ? cart.ShortTermTargetSettlement.StringId : "-")
                  .Append(", AI ").Append(cart.Ai != null && cart.Ai.IsDisabled ? "wylaczone" : "czynne").Append(", tratwa ").Append(cart.IsInRaftState ? 1 : 0)
                  .Append(", predkosc ").Append(sp.ToString("0.00", Inv)).Append(", waga ").Append(cart.TotalWeightCarried.ToString("0", Inv)).Append('/').Append(cart.InventoryCapacity).Append(" kg")
                  .Append(", juki ").Append(LoadUnits(cart)).Append(" szt., magazyn ").Append(store).Append('/').Append(W)
                  .Append(", najblizszy wrog ").Append(Enemy(cart));
                return sb.ToString();
            }
            catch (Exception e) { Stumble("CartGuard.Ex", e); return "wies " + Name(home) + " - " + what; }
        }

        private static string Enemy(MobileParty cart)
        {
            var fac = cart.MapFaction;
            Vec2 pos = cart.Position.ToVec2();
            MobileParty best = null; float bd = 25f;
            foreach (var p in MobileParty.All)
            {
                if (p == null || p == cart || !p.IsActive || p.CurrentSettlement != null) continue;
                bool foe = p.IsBandit || (fac != null && p.MapFaction != null && p.MapFaction != fac && p.MapFaction.IsAtWarWith(fac));
                if (!foe) continue;
                float d2 = p.Position.ToVec2().DistanceSquared(pos);
                if (d2 < bd) { bd = d2; best = p; }
            }
            if (best == null) return "brak w 5 jedn.";
            string n; try { n = best.Name != null ? best.Name.ToString() : "?"; } catch { n = "?"; }
            return n + " " + Math.Sqrt(bd).ToString("0.0", Inv) + " jedn.";
        }
    }
}
