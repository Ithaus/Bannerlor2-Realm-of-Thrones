using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// E8/G1 (260, PLAN 8.12a-b; projekt etapu 8 v2 rozdz. 3 "E8/G1"; odpowiedz Jeffa 3 z 12.10: TAK) - KARAWANY: BK W MIESCIE CO GODZINE I WYSPY BEZ DROGI.
    ///  1. DLAWIK BKROT TYLKO W DRODZE (klucz CaravanTownHourly): BKROTPatch.Patches.BKCaravansHourlyThrottlePatch.Prefix puszcza godzinny tik BK
    ///     (BKCaravansBehavior.HourlyTickParty) karawanie AI raz na 24 h (hash id % 24 == godzina). W miescie BK rusza karawane z szansa 1/3 na tik
    ///     (HourlyTickPartyImpl: CurrentSettlement.Town != null, bez oblezenia), wiec postoj trwal srednio ok. 3 doby zamiast ok. 3 h. POSTFIKS NA SAMEJ METODZIE
    ///     PREFIKSU BKROT (krytyka K2-1: prefiks o wyzszym priorytecie nie pomoze - Harmony przed prefiksem z wynikiem bool sprawdza runOriginal, a wynik BKROT
    ///     i tak go nadpisze): gdy BKROT zwrocil false, a karawana stoi w miescie albo zamku (Town != null) bez oblezenia - wynik true. W drodze i we wsi
    ///     wynik BKROT bez zmian. Postfiks na BK HourlyTickParty tylko mierzy: czas tikow, ktore otworzylismy, i ile z nich dalo karawanie nowy cel.
    ///     LIMIT NA POSTOJ (poprawka A2 po biegu probnym 2, TRIAZ-2 G1-4 "G1b"; klucz CaravanTownHourlyMaxTicks, 0 = bez limitu): po tylu otwartych tikach w tym
    ///     samym postoju karawana wraca do tiku BKROT raz na dobe (jak przed E8/G1). Karawana bez celu powtarzala pelna decyzje BK (zakup + dwa przebiegi po
    ///     miastach) co trzeci tik bez konca - 62% kosztu otwartych tikow (1.4-1.7 s na dobe) szlo na karawany, ktore i tak nie wyjezdzaja. Tylko bramka tiku.
    ///  2. ZAKUP KARAWANY LORDA BK W SLEPYM ZAULKU (klucz IslandCaravanFix): BKLordPropertyBehavior.OnSettlementEntered kupuje lordowi karawane w osadzie,
    ///     do ktorej wjechal (takze we wsi i zamku), z szablonem ladowym poza portem. Na wyspie z jednym miastem taka karawana nie ma dokad jechac
    ///     (Z33: 2 karawany lordow stoja 118 dob). Postfiks na ShouldHaveCaravan(Hero, int): osada bez drogi ladowej do dwoch miast (miasto - do zadnego
    ///     innego) i szablon bez statkow (ten sam wybor co BK: w porcie szablon ze statkami, poza portem ladowy, brak pasujacego - dowolny) -> false,
    ///     zloto zostaje u lorda (BK zdejmuje cene dopiero po tym warunku).
    ///  3. KARAWANA BEZ DROGI (ten sam klucz; krytyka D4 i K2-8 - stan, nie czas postoju): karawana AI bez statkow w miescie, z ktorego zadne inne miasto
    ///     nie jest osiagalne ladem (IslandRoads - ta sama pamiec co linia "Wyspy i drogi", potem swiezy odczyt z miasta i z pozycji partii) -
    ///     raz na dobe: towar sprzedany miastu zwykla droga gry (SellItemsAction), ale tylko z nadwyzki kasy (Seal31.Spare, R1), potem
    ///     DestroyPartyAction.Apply(null, karawana) - reszte robia CZYNNE reguly rozwiazanej partii: kiesa z utargiem do wlasciciela (CaravanCapital.RoutePurse,
    ///     E3/7), ludzie do domu (PeopleBook.ArmsPartyGone, E4/8), sakiewka ludzi do kasy najblizszego miasta (MenPurse, wpis 89), niesprzedany dobytek na
    ///     polke miasta bez zaplaty (HorseFate.Spoils, E6/K1). Bez tych regul (wylaczone klucze) karawana zostaje - nic nie idzie w nicosc przez nasz rozkaz.
    ///     ZWROT CENY (projekt: "przed kodem sprawdzic w dekompilacji BK"): BK nie ma zwrotu ceny karawany - GetCaravanPrice jest czytane tylko przy zakupie,
    ///     a BK usuwa karawany wylacznie przez DestroyPartyAction.Apply(null, ..) (DailyTick: bez wlasciciela, rod wymarly). Gra przy kazdym zniszczeniu
    ///     karawany wyplaca wlascicielowi z perkiem Insurance Plans 5 000 z niczego (DestroyPartyAction.ApplyInternal) - u AI ucina to SmallGold
    ///     (TradePerkGiftsAiOff); bez tego ciecia karawany wlasciciela z perkiem nie rozwiazujemy. Karawany rodu gracza nie sa ruszane nigdy.
    ///  4. LIMIT KARAWAN KUPCA (poprawka A1 po biegu probnym 2, TRIAZ-2 S2; klucz NotableCaravanCap, 0 = regula gry): gra daje kupcowi nowa karawane
    ///     (CaravansCampaignBehavior.DailyTickHero, szansa 0.75 na dobe), gdy zadna jego karawana nie ma wlaczonego AI (DefaultCaravanModel.CanHeroCreateCaravan liczy
    ///     tylko !Ai.IsDisabled; czynny NavalDLCCaravanModel oddaje do BaseModel - sprawdzone ilspycmd), a nocny oboz wylacza AI karawanom spiacym w polu
    ///     (NightRest: Ai.DisableForHours) - kupiec z tikiem dobowym w nocy dostawal kolejna karawane bez granicy (d120: 1570 karawan, 4.7 na notabla).
    ///     Postfiks na DefaultCaravanModel.CanHeroCreateCaravan tylko zmienia true na false, gdy bohater ma juz limit karawan liczonych WSZYSTKICH (spiace,
    ///     w osadzie, na morzu). Rod gracza bez limitu. Latka zakladana W KAMPANII raz na proces (Seal31.EnsureHooks - CLAUDE.md 7), stan w linii doby.
    ///     Blokada jest PRZED utworzeniem partii - nic nie zostaje pobrane w polowie (kapital, ludzie, juczne, wplyw notabla).
    /// Stan tylko w pamieci (liczniki doby, pamiec slepych zaulkow, wlasciciele rozwiazanych karawan) - Reset z WorldMeasure.Reset. Nic w zapisie.
    /// Linia doby "Karawany (E8/G1)" (takze przy wylaczonych kluczach - wtedy liczy, co by zrobila). Wyjatek = licznik potkniec, bez globalnego wylacznika.
    /// </summary>
    internal static class CaravanFlow
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static int _stumbles, _stumblesDay;
        private static readonly HashSet<string> _errSites = new HashSet<string>();
        private static readonly List<string> _wired = new List<string>(), _missing = new List<string>();
        private static int _wiredN, _wantedN;

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumblesDay++;
            try { if (_errSites.Add(where)) Log.Error("CaravanFlow." + where, e); } catch { }
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

        // ------------------------------------------------------------ 1. dlawik BKROT: stan wywolania i liczniki doby (glowny watek - takt godzinny partii nie jest rownolegly)
        [ThreadStatic] private static MobileParty _fParty;
        [ThreadStatic] private static Settlement _fAt, _fTarget;
        [ThreadStatic] private static long _fT0;
        private static int _dRoad, _dSiege, _dTown, _dOpened, _dRan, _dOrders;
        private static long _dTicks;
        private static readonly HashSet<MobileParty> _dCars = new HashSet<MobileParty>();
        // pomiar (tylko licznik): otwarte tiki ponad LongStayTicks w tym samym postoju - karawana, ktora mimo tiku co godzine nie wyjezdza (brak celu, ranni);
        // przy szansie 1/3 na tik karawana z celem zostaje tak dlugo raz na ok. 17 000 postojow
        private sealed class Stay { public Settlement At; public int N; public double Last; public bool Shut; }
        private static readonly Dictionary<MobileParty, Stay> _stay = new Dictionary<MobileParty, Stay>();
        private const int LongStayTicks = 24;
        private static int _dLongTicks;
        private static readonly HashSet<MobileParty> _dLongCars = new HashSet<MobileParty>();
        // A2 (G1b): tiki zamkniete po limicie otwartych tikow na postoj (CaravanTownHourlyMaxTicks) i stan karawany przy pierwszym zamknieciu postoju
        private static int _dShut, _dShutNew;
        private static readonly HashSet<MobileParty> _dShutCars = new HashSet<MobileParty>();
        private static readonly Dictionary<string, int> _dShutWhy = new Dictionary<string, int>();
        // audyt [latki]: otwarty tik, w ktorym cialo BK nie pobieglo (inny prefiks po BKROT zwrocil false - kontrakt surowca 174)
        private static int _dBlocked;
        private static long _dBlockedTicks;

        // ------------------------------------------------------------ 2. zakup karawany lorda BK
        private static int _dBuyAsk, _dBuyShips, _dBuySkip, _dBuyShadow;
        private static readonly Dictionary<string, int> _dBuyAt = new Dictionary<string, int>();
        private static readonly Dictionary<Settlement, bool> _deadEnd = new Dictionary<Settlement, bool>();   // osada bez drogi ladowej do dwoch miast (pamiec sesji)
        private static string _census;

        // ------------------------------------------------------------ 3. karawana bez drogi
        private static int _dStuck, _dDone, _dLeft, _dGoneShadow, _dFail, _dAgain, _dBelow, _dMen;
        private static long _dSoldU, _dSoldG, _dUnsoldU, _dUnsoldV, _dPurse, _dOwnerGot, _dMenPurse;
        private static readonly Dictionary<string, int> _dDoneAt = new Dictionary<string, int>(), _dKept = new Dictionary<string, int>();
        private static readonly List<string> _dEx = new List<string>();
        private const int MaxEx = 4;
        private static readonly Dictionary<Hero, int> _ownerDay = new Dictionary<Hero, int>();   // wlasciciel -> doba rozwiazania jego karawany (ochrona przed petla: rozwiaz - kup - rozwiaz)
        // m6b (6, klucz IdleCaravanDisband): karawana bez statkow, ktora stoi w tym samym miescie z droga do innych miast od IdleDisbandDays+ dob (BK nie znajduje
        // zysku - bieg roczny: kupiec z Thenn 289 dob), rozwiazywana ta sama droga co karawana bez drogi (Resolve): towar z nadwyzki kasy, kiesa do wlasciciela
        private const double IdleDisbandDays = 28.0;
        private static int _dIdle, _dIdleDone, _dIdleLeft;
        private static double _dIdleMax;
        private static readonly Dictionary<string, int> _dIdleAt = new Dictionary<string, int>();

        // ------------------------------------------------------------ 4. limit karawan kupca (A1)
        private static int _dCapAsk, _dCapHeld;
        private static readonly HashSet<Hero> _dCapWho = new HashSet<Hero>();
        private static bool _capTried;                                          // latka raz na proces - nie zerowane w Reset
        private static string _capHook = "nie wpieta";

        /// <summary>Nowa kampania i kazde wczytanie (WorldMeasure.Reset - konstruktor ArmouryBehavior).</summary>
        internal static void Reset()
        {
            _stumbles = 0; _errSites.Clear();
            _deadEnd.Clear(); _census = null; _ownerDay.Clear(); _stay.Clear();
            _fParty = null; _fAt = null; _fTarget = null;
            ClearDay();
        }

        private static void ClearDay()
        {
            _stumblesDay = 0;
            _dRoad = _dSiege = _dTown = _dOpened = _dRan = _dOrders = 0; _dTicks = 0; _dCars.Clear();
            _dLongTicks = 0; _dLongCars.Clear();
            _dShut = _dShutNew = 0; _dShutCars.Clear(); _dShutWhy.Clear();
            _dBlocked = 0; _dBlockedTicks = 0;
            _dBuyAsk = _dBuyShips = _dBuySkip = _dBuyShadow = 0; _dBuyAt.Clear();
            _dStuck = _dDone = _dLeft = _dGoneShadow = _dFail = _dAgain = _dBelow = _dMen = 0;
            _dSoldU = _dSoldG = _dUnsoldU = _dUnsoldV = _dPurse = _dOwnerGot = _dMenPurse = 0;
            _dDoneAt.Clear(); _dKept.Clear(); _dEx.Clear();
            _dIdle = _dIdleDone = _dIdleLeft = 0; _dIdleMax = 0; _dIdleAt.Clear();   // m6b (6)
            _dCapAsk = _dCapHeld = 0; _dCapWho.Clear();
        }

        // ------------------------------------------------------------ 1. dlawik BKROT tylko w drodze

        /// <summary>Postfiks BKROTPatch BKCaravansHourlyThrottlePatch.Prefix(MobileParty): BKROT zamknal tik BK (false tylko dla karawany AI poza jej godzina doby),
        /// a karawana stoi w miescie albo zamku bez oblezenia - tik BK biegnie (true). W drodze wynik BKROT bez zmian.</summary>
        public static void ThrottlePostfix(MobileParty __0, ref bool __result)
        {
            _fParty = null;
            if (__result || __0 == null) return;                                // BKROT przepuscil: nie karawana, rod gracza albo godzina tej karawany
            try
            {
                var s = Settings.Current;
                if (s == null) return;
                var at = __0.CurrentSettlement;
                if (at == null || at.Town == null) { _dRoad++; return; }        // w drodze (albo we wsi) - dlawik BKROT zostaje
                if (at.IsUnderSiege) { _dSiege++; return; }                     // oblezone miasto: BK i tak nic nie robi (karawana czeka na koniec oblezenia)
                _dTown++;
                if (!s.CaravanTownHourly) return;                               // w cieniu: tylko licznik
                // ksiegowanie postoju PRZED otwarciem (A2/G1b)
                Stay st;
                if (!_stay.TryGetValue(__0, out st)) { st = new Stay(); _stay[__0] = st; }
                double now = CampaignTime.Now.ToHours;
                if (st.At != at || now - st.Last > 2.5) { st.At = at; st.N = 0; st.Shut = false; }   // inne miasto albo przerwa w tikach (wyjazd i powrot, oblezenie) = nowy postoj
                st.Last = now;                                                  // takze przy zamknietym tiku - inaczej "przerwa" otworzylaby te sama karawane od nowa
                int lim = s.CaravanTownHourlyMaxTicks;
                if (lim > 0 && st.N >= lim)                                     // limit postoju: wynik BKROT zostaje (false) - tik BK raz na dobe, jak przed E8/G1
                {
                    _dShut++; _dShutCars.Add(__0);
                    if (!st.Shut) { st.Shut = true; _dShutNew++; Classify(__0, at); }
                    return;
                }
                st.N++;
                __result = true;
                _dOpened++; _dCars.Add(__0);
                if (st.N > LongStayTicks) { _dLongTicks++; _dLongCars.Add(__0); }
                _fAt = at; _fTarget = __0.TargetSettlement; _fT0 = Stopwatch.GetTimestamp(); _fParty = __0;
            }
            catch (Exception e) { Stumble("ThrottlePostfix", e); }
        }

        /// <summary>Raz na postoj, przy pierwszym tiku zamknietym po limicie: stan karawany z tanich pol (cechy sie nie wykluczaja). Bez ThinkNextDestination BK
        /// do pomiaru "cel null" - to pelna decyzja (ok. 3.7 ms) i dotyka slownikow BK. Progi rannych jak w BK HourlyTickPartyImpl (2.5% - mniejsza szansa
        /// wyjazdu, ponad 40% - zadna).</summary>
        private static void Classify(MobileParty c, Settlement at)
        {
            try
            {
                bool any = false;
                if (c.DefaultBehavior == AiBehavior.Hold || c.ShortTermBehavior == AiBehavior.Hold) { Bump(_dShutWhy, "Hold"); any = true; }
                var mr = c.MemberRoster;
                int men = mr != null ? mr.TotalManCount : 0, wnd = mr != null ? mr.TotalWounded : 0;
                if (men > 0 && wnd > 0.4f * men) { Bump(_dShutWhy, "ranni ponad 40%"); any = true; }
                else if (men > 0 && wnd > 0.025f * men) { Bump(_dShutWhy, "ranni 2.5-40%"); any = true; }
                if (c.PartyTradeGold < 100) { Bump(_dShutWhy, "kiesa ponizej 100"); any = true; }
                if (c.ItemRoster == null || c.ItemRoster.Count == 0) { Bump(_dShutWhy, "juki puste"); any = true; }
                if (c.HasNavalNavigationCapability) { Bump(_dShutWhy, "ze statkami"); any = true; }
                if (c.HomeSettlement == at) { Bump(_dShutWhy, "dom tutaj"); any = true; }
                if (!any) Bump(_dShutWhy, "bez cechy");
            }
            catch (Exception e) { Stumble("Classify", e); }
        }

        /// <summary>Postfiks BK BKCaravansBehavior.HourlyTickParty(MobileParty) - tylko pomiar tiku otwartego wyzej: czas (z prefiksami po BKROT i cialem BK)
        /// i czy karawana dostala nowy cel poza miastem postoju. Audyt [latki]: postfiks biegnie takze, gdy cialo BK zostalo pominiete przez inny prefiks
        /// (MaterialOrders.HourlyPrefix - karawana z kontraktem 174) - taki przebieg nie jest tikiem BK: osobny licznik, poza "tiki BK" i kosztem.</summary>
        public static void BkHourlyPostfix(MobileParty __0, bool __runOriginal)
        {
            var p = _fParty;
            if (p == null) return;
            _fParty = null;
            try
            {
                if (!ReferenceEquals(p, __0)) return;
                long dt = Stopwatch.GetTimestamp() - _fT0;
                if (!__runOriginal)
                {
                    _dBlocked++; _dBlockedTicks += dt;
                    Stay st;                                                    // BK nie mogl zdecydowac - ten tik nie zuzywa limitu postoju
                    if (_stay.TryGetValue(p, out st) && st.N > 0) st.N--;
                    return;
                }
                _dTicks += dt;
                _dRan++;
                var t = __0.TargetSettlement;
                if (t != null && t != _fAt && t != _fTarget) _dOrders++;
            }
            catch (Exception e) { Stumble("BkHourlyPostfix", e); }
            finally { _fAt = null; _fTarget = null; }
        }

        // ------------------------------------------------------------ 2. zakup karawany lorda BK w slepym zaulku

        private static bool Road(MapDistanceModel m, Settlement a, Settlement b)
        {
            float d = m.GetDistance(a, b, false, false, MobileParty.NavigationType.Default);
            return d >= 0f && d < CartTownExit.BkLimit;
        }

        /// <summary>Osada, z ktorej karawana bez statkow nie ma handlu: miasto bez innego miasta w zasiegu ladu (IslandRoads - ta sama pamiec co linia
        /// "Wyspy i drogi"), zamek albo wies - mniej niz dwa miasta w zasiegu ladu (pamiec drog gry trzyma kazda pare osad raz, posortowana - wynik jest
        /// ten sam w obie strony; pary, ktorej nie ma w pliku mapy, gra dolicza sama przy pierwszym odczycie).</summary>
        private static bool DeadEnd(Settlement at)
        {
            bool v;
            if (_deadEnd.TryGetValue(at, out v)) return v;
            if (at.IsTown) v = IslandRoads.LonelyTown(at);
            else
            {
                var m = Campaign.Current.Models.MapDistanceModel;
                int n = 0;
                foreach (var t in Town.AllTowns)
                {
                    var st = t != null ? t.Settlement : null;
                    if (st == null || st == at || !st.IsTown) continue;
                    if (Road(m, at, st) && ++n >= 2) break;
                }
                v = n < 2;
            }
            _deadEnd[at] = v;
            return v;
        }

        /// <summary>Czy karawana, ktora BK kupilby lordowi w tej osadzie, mialaby statki - ten sam wybor szablonu co BKLordPropertyBehavior: w miescie z portem
        /// szablon ze statkami, poza portem ladowy; gdy pasujacego nie ma - dowolny (czyli wszystkie sa drugiego rodzaju). false = kultura bez szablonow (BK nie kupi).</summary>
        private static bool Template(Hero lord, Settlement at, out bool ships)
        {
            ships = false;
            var list = lord.Culture != null ? lord.Culture.CaravanPartyTemplates : null;
            if (list == null || list.Count == 0) return false;
            bool anyLand = false, anyNaval = false;
            foreach (var t in list)
            {
                if (t == null) continue;
                if (t.ShipHulls == null || t.ShipHulls.Count == 0) anyLand = true; else anyNaval = true;
            }
            bool atPort = at.IsTown && at.HasPort;
            ships = atPort ? anyNaval : !anyLand;
            return true;
        }

        /// <summary>Postfiks BK BKLordPropertyBehavior.ShouldHaveCaravan(Hero, int) (jedyny wolajacy: OnSettlementEntered lorda, ktory wlasnie wjechal do osady -
        /// EnterSettlementAction ustawia CurrentSettlement przed zdarzeniem): karawana bez statkow w osadzie bez drogi ladowej do dwoch miast - bez zakupu.</summary>
        public static void LordBuyPostfix(Hero __0, ref bool __result)
        {
            if (!__result || __0 == null) return;
            try
            {
                var s = Settings.Current;
                if (s == null || Campaign.Current == null) return;
                var party = __0.PartyBelongedTo;
                var at = party != null ? party.CurrentSettlement : null;
                if (at == null) return;
                bool ships;
                if (!Template(__0, at, out ships)) return;
                _dBuyAsk++;
                if (ships) { _dBuyShips++; return; }
                if (!DeadEnd(at)) return;
                if (!s.IslandCaravanFix) { _dBuyShadow++; return; }
                __result = false;
                _dBuySkip++; Bump(_dBuyAt, Name(at));
            }
            catch (Exception e) { Stumble("LordBuyPostfix", e); }
        }

        /// <summary>Raz na sesje (pierwszy takt doby): ile osad mapy jest slepym zaulkiem dla karawany bez statkow - kontrola samej reguly
        /// (gdyby pamiec drog nie znala par wies - miasto, wyszlyby prawie wszystkie wsie).</summary>
        private static string Census()
        {
            int t = 0, c = 0, v = 0, all = 0;
            long t0 = Stopwatch.GetTimestamp(), lim = Stopwatch.Frequency / 5;      // 200 ms: para spoza pliku mapy to szukanie drogi - spis nie moze zatrzymac taktu doby
            foreach (var st in Settlement.All)
            {
                if (st == null || !(st.IsTown || st.IsCastle || st.IsVillage)) continue;
                if (Stopwatch.GetTimestamp() - t0 > lim) return "przerwany po " + all + " osadach (ponad 200 ms): miasta " + t + ", zamki " + c + ", wsie " + v;
                all++;
                if (!DeadEnd(st)) continue;
                if (st.IsTown) t++; else if (st.IsCastle) c++; else v++;
            }
            return "miasta " + t + ", zamki " + c + ", wsie " + v + " z " + all + " osad";
        }

        // ------------------------------------------------------------ 3. karawana bez drogi

        /// <summary>Swiezy odczyt (bez pamieci sesji): z miasta do innego miasta ladem albo z pozycji partii (ta sama odleglosc co straznik BK).</summary>
        private static bool RoadNow(MobileParty c, Settlement at)
        {
            var m = Campaign.Current.Models.MapDistanceModel;
            foreach (var t in Town.AllTowns)
            {
                var st = t != null ? t.Settlement : null;
                if (st == null || st == at || !st.IsTown) continue;
                if (Road(m, at, st)) return true;
                float lr;
                float d = m.GetDistance(c, st, false, MobileParty.NavigationType.Default, out lr);
                if (d >= 0f && d < CartTownExit.BkLimit) return true;
            }
            return false;
        }

        /// <summary>Glowa, do ktorej regula E3/7 (CaravanCapital.RoutePurse) odda kiese rozwiazanej karawany - tylko do pomiaru "przybylo".</summary>
        private static Hero Head(MobileParty c, Hero owner)
        {
            if (owner != null && owner.IsNotable) return owner;
            var clan = c.ActualClan ?? (owner != null ? owner.Clan : null);
            return clan != null && !clan.IsBanditFaction ? clan.Leader : null;
        }

        /// <summary>Powod, dla ktorego karawana bez drogi zostaje (null = mozna rozwiazac).</summary>
        private static string Blocker(MobileParty c, Settlement at, Hero owner, int day, bool idle = false)
        {
            if (c.IsMainParty || c.ActualClan == Clan.PlayerClan || (owner != null && owner == Hero.MainHero)) return "rod gracza";
            if (owner == null) return "bez wlasciciela (usuwa ja BK)";
            if (c.MapEvent != null || c.SiegeEvent != null || at.IsUnderSiege || c.IsDisbanding) return "bitwa albo oblezenie";
            if (c.IsCurrentlyUsedByAQuest) return "karawana z zadania";
            if (MaterialOrders.HasContract(c)) return "kontrakt surowca";
            if (Measure31.IsDteConvoy(c)) return "konwoj DTE";
            if (!c.IsPartyTradeActive || c.Ai == null || c.Ai.DoNotMakeNewDecisions) return "nie handluje";
            if ((c.MemberRoster != null && c.MemberRoster.TotalHeroes > 0) || (c.PrisonRoster != null && c.PrisonRoster.TotalManCount > 0)) return "bohater w skladzie albo jency";
            int was;
            // m6b (6, recenzja): karawana bez zysku - sam prog postoju (IdleDisbandDays) jest hamulcem petli (nowa karawana musi stac 28+ dob), wiec ochrona
            // blokuje tylko rozwiazanie blizsze niz IdleDisbandDays od poprzedniego; inaczej druga karawana tego samego kupca stalaby do konca sesji
            if (_ownerDay.TryGetValue(owner, out was) && was != day && (!idle || day - was < IdleDisbandDays)) { _dAgain++; return "wlasciciel mial juz rozwiazana karawane (ochrona przed petla)"; }
            if (!CaravanCapital.On) return "Caravan Capital Real wylaczone (kiesa przepadlaby)";
            if (!HorseFate.SpoilsOn) return "Spoils Of Broken Parties wylaczone (towar przepadlby)";
            if (!SmallGold.PerksOn && owner.GetPerkValue(DefaultPerks.Trade.InsurancePlans)) return "perk Insurance Plans bez ciecia (zloto z niczego)";
            if (!idle && RoadNow(c, at)) return "droga jest w swiezym odczycie";   // m6b (6): karawana bez zysku ma droge - stoi, bo BK nie widzi zysku
            return null;
        }

        /// <summary>Towar karawany na targ miasta zwykla droga gry (SellItemsAction: sztuki na polke, zloto z kasy miasta do kiesy karawany), najwyzej za
        /// nadwyzke kasy (Seal31.Spare liczone przed kazda porcja; cena porcji = cena pierwszej sztuki x n, a kazda nastepna sztuka jest tansza - suma nie
        /// przekroczy nadwyzki). Sztuki, ktorych regula dobytku E6/K1 nie wystawia na targ (nie-towar, sztandar, zadanie, unikat), zostaja.</summary>
        private static void SellOut(MobileParty c, Settlement st, out int soldU, out long soldG, out int leftU, out long leftV)
        {
            soldU = 0; soldG = 0; leftU = 0; leftV = 0;
            var town = st.Town; var pack = c.ItemRoster;
            if (town == null || pack == null || town.Owner == null) return;
            var els = new List<EquipmentElement>();
            for (int i = 0; i < pack.Count; i++)
            {
                var el = pack.GetElementCopyAtIndex(i);
                var it = el.EquipmentElement.Item;
                if (el.Amount <= 0 || it == null) continue;
                if (it.NotMerchandise || it.IsBannerItem || el.EquipmentElement.IsQuestItem || UniqueSpoils.Is(it)) continue;
                els.Add(el.EquipmentElement);
            }
            foreach (var ee in els)
            {
                for (int guard = 0; guard < 500; guard++)
                {
                    int at = pack.FindIndexOfElement(ee);
                    int have = at >= 0 ? pack.GetElementNumber(at) : 0;
                    if (have <= 0) break;
                    long spare = Seal31.Spare(st);
                    int price = Math.Max(1, town.GetItemPrice(ee, c, true));
                    int n = (int)Math.Min(Math.Min(have, 10), spare / price);
                    if (n <= 0) break;                                           // miasto bez nadwyzki na te sztuke - zostaje regule dobytku
                    int purse = c.PartyTradeGold;
                    try { SellItemsAction.Apply(c.Party, town.Owner, pack.GetElementCopyAtIndex(at), n, st); }
                    catch (Exception e) { Stumble("SellOut(sprzedaz)", e); break; }
                    int at2 = pack.FindIndexOfElement(ee);
                    int moved = have - (at2 >= 0 ? pack.GetElementNumber(at2) : 0);
                    long got = Math.Max(0, c.PartyTradeGold - purse);
                    if (moved <= 0) break;
                    soldU += moved; soldG += got;
                    if (got > spare) _dBelow++;                                  // prog 0: wyplata ponad nadwyzke
                }
            }
            for (int i = 0; i < pack.Count; i++)
            {
                var el = pack.GetElementCopyAtIndex(i);
                if (el.Amount <= 0 || el.EquipmentElement.Item == null) continue;
                leftU += el.Amount; leftV += (long)el.Amount * Math.Max(0, el.EquipmentElement.ItemValue);
            }
        }

        private static void Resolve(MobileParty c, Settings s, int day, bool idle = false)
        {
            var at = c.CurrentSettlement;
            if (at == null) return;
            Hero owner = null;
            try { owner = c.Owner ?? (c.Party != null ? c.Party.Owner : null); } catch { owner = null; }
            string why = Blocker(c, at, owner, day, idle);
            if (why != null) { if (idle) _dIdleLeft++; else _dLeft++; Bump(_dKept, (idle ? "bez zysku: " : "") + why); return; }
            if (idle ? !s.IdleCaravanDisband : !s.IslandCaravanFix) { if (idle) _dIdleLeft++; else { _dLeft++; _dGoneShadow++; } return; }       // w cieniu: tylko licznik
            string town = Name(at), who = "?";
            try { who = owner.Name != null ? owner.Name.ToString() : "?"; } catch { }
            int men = c.MemberRoster != null ? c.MemberRoster.TotalManCount : 0;
            int menPurse = MenPurse.Get(c);
            int soldU, leftU; long soldG, leftV;
            SellOut(c, at, out soldU, out soldG, out leftU, out leftV);
            _dSoldU += soldU; _dSoldG += soldG;                                  // sprzedaz juz zaszla - liczona takze, gdy partia nie zniknie
            int purse = c.PartyTradeGold;
            var head = Head(c, owner);
            int g0 = head != null ? head.Gold : 0;
            _ownerDay[owner] = day;
            DestroyPartyAction.Apply(null, c);
            if (c.IsActive) { _dFail++; if (idle) _dIdleLeft++; else _dLeft++; return; }
            if (idle) { _dIdleDone++; Bump(_dIdleAt, town); } else { _dDone++; Bump(_dDoneAt, town); }
            _dUnsoldU += leftU; _dUnsoldV += leftV; _dPurse += purse; _dMen += men; _dMenPurse += menPurse;
            if (head != null) _dOwnerGot += Math.Max(0, head.Gold - g0);
            if (_dEx.Count < MaxEx)
                _dEx.Add(town + ": " + who + " - towar " + soldU + " szt. za " + soldG + " zl, niesprzedane " + leftU + " szt., kiesa " + purse + " zl, ludzie " + men);
        }

        private static void Islands(Settings s, int day)
        {
            List<MobileParty> cand = null, idle = null;
            var all = MobileParty.AllCaravanParties;
            if (all == null) return;
            foreach (var c in all)
            {
                try
                {
                    if (c == null || !c.IsActive || !c.IsCaravan) continue;
                    var at = c.CurrentSettlement;
                    if (at == null || !at.IsTown) continue;
                    if (!c.HasLandNavigationCapability || c.HasNavalNavigationCapability) continue;
                    if (!IslandRoads.LonelyTown(at))
                    {
                        // m6b (6): miasto z droga do innych miast - karawana stoi, bo BK nie widzi zysku; od IdleDisbandDays dob w tym samym miescie - rozwiazanie
                        double stay = IslandRoads.StayDays(c);
                        if (stay < IdleDisbandDays) continue;
                        _dIdle++; if (stay > _dIdleMax) _dIdleMax = stay;
                        if (idle == null) idle = new List<MobileParty>();
                        idle.Add(c);
                        continue;
                    }
                    _dStuck++;
                    if (cand == null) cand = new List<MobileParty>();
                    cand.Add(c);
                }
                catch (Exception e) { Stumble("Islands(przeglad)", e); }
            }
            if (cand != null)
                foreach (var c in cand)                                          // rozwiazanie po przegladzie - lista karawan gry zmienia sie przy usuwaniu
                {
                    try { Resolve(c, s, day); }
                    catch (Exception e) { _dLeft++; Stumble("Islands(rozwiazanie)", e); }
                }
            if (idle != null)
                foreach (var c in idle)                                          // m6b (6): karawany bez zysku - ta sama droga rozwiazania
                {
                    try { Resolve(c, s, day, true); }
                    catch (Exception e) { _dIdleLeft++; Stumble("Islands(bez zysku)", e); }
                }
        }

        // ------------------------------------------------------------ 4. limit karawan kupca (A1)

        /// <summary>Karawany bohatera liczone WSZYSTKIE (spiace w polu, w osadzie, na morzu) - gra w DefaultCaravanModel liczy tylko te z wlaczonym AI.</summary>
        private static int Owned(Hero h)
        {
            var own = h.OwnedCaravans;
            if (own == null) return 0;
            int n = 0;
            for (int i = 0; i < own.Count; i++) { var c = own[i]; if (c != null && c.MobileParty != null) n++; }
            return n;
        }

        /// <summary>Postfiks DefaultCaravanModel.CanHeroCreateCaravan(Hero): gra zgodzila sie dac karawane (true), choc bohater juz jakies ma (wszystkie z wylaczonym
        /// AI - spia w polu) - przy limicie wynik false. Tylko zaweza (true -> false); bohater bez karawany - regula gry. Rod gracza bez limitu.</summary>
        public static void NotableCapPostfix(Hero __0, ref bool __result)
        {
            if (!__result || __0 == null) return;
            try
            {
                if (__0 == Hero.MainHero || (__0.Clan != null && __0.Clan == Clan.PlayerClan)) return;
                int n = Owned(__0);
                if (n <= 0) return;                                             // pierwsza karawana - jak w grze
                _dCapAsk++;
                var s = Settings.Current;
                int cap = s != null ? s.NotableCaravanCap : 0;
                if (cap <= 0 || n < cap) return;                                // 0 = regula gry; ponizej limitu - kolejna karawana jak w grze
                __result = false;
                _dCapHeld++; _dCapWho.Add(__0);
            }
            catch (Exception e) { Stumble("NotableCapPostfix", e); }
        }

        /// <summary>Raz na dobe: rozklad karawan na notabla-wlasciciela (ta sama liczba, ktora czyta limit - Owned).</summary>
        private static void CapCensus(out int one, out int two, out int more, out int max, out int total)
        {
            one = two = more = max = total = 0;
            var all = MobileParty.AllCaravanParties;
            if (all == null) return;
            var seen = new HashSet<Hero>();
            foreach (var c in all)
            {
                var o = c != null ? c.Owner : null;
                if (o == null || !o.IsNotable || !seen.Add(o)) continue;
                int n = Owned(o);
                if (n <= 0) continue;
                total += n; if (n > max) max = n;
                if (n == 1) one++; else if (n == 2) two++; else more++;
            }
        }

        /// <summary>Latka na modelu gry - zakladana W KAMPANII, raz na proces (Seal31.EnsureHooks z ArmouryBehavior.OnSessionLaunched; CLAUDE.md 7: modeli gry
        /// nie latac w SubModuleMain). Cel: DefaultCaravanModel.CanHeroCreateCaravan - tam jest regula; czynny NavalDLCCaravanModel oddaje do BaseModel, nikt inny
        /// jej nie nadpisuje ani nie lata (skan DLL modulow). Linia startowa "latki przy starcie gry - wpiete 3/3" bez zmian; stan tej latki w linii doby.</summary>
        internal static void EnsureHooks()
        {
            if (_capTried) return;
            _capTried = true;
            try
            {
                var h = Seal31.H;
                var m = AccessTools.Method(typeof(TaleWorlds.CampaignSystem.GameComponents.DefaultCaravanModel), "CanHeroCreateCaravan", new[] { typeof(Hero) });
                var cm = Campaign.Current != null ? Campaign.Current.Models.CaravanModel : null;
                if (h == null) _capHook = "BRAK (brak Harmony)";
                else if (m == null || m.ReturnType != typeof(bool)) _capHook = "BRAK (DefaultCaravanModel.CanHeroCreateCaravan nie znaleziona)";
                else
                {
                    h.Patch(m, postfix: new HarmonyMethod(typeof(CaravanFlow), nameof(NotableCapPostfix)));
                    _capHook = "wpieta 1/1 (DefaultCaravanModel.CanHeroCreateCaravan, czynny model " + (cm != null ? cm.GetType().Name : "brak") + ")";
                }
            }
            catch (Exception e) { _capHook = "BRAK (blad: " + e.Message + ")"; Log.Error("CaravanFlow.EnsureHooks", e); }
            var s = Settings.Current;
            Log.Info("Karawany (E8/G1): latka w kampanii - limit karawan kupca " + _capHook + " | Notable Caravan Cap " + (s != null ? s.NotableCaravanCap.ToString(Inv) : "?")
                     + " (0 = regula gry; liczone wszystkie karawany kupca, takze spiace w polu).");
        }

        // ------------------------------------------------------------ takt doby

        /// <summary>Raz na dobe (ArmouryBehavior.OnDailyTick, zaraz po Stage8World.Daily - PRZED MoneyLedger.BlockOpen: sprzedaz towaru miastu jest zwyklym
        /// handlem karawany, nie pozycja naszego rozliczenia doby): karawany bez drogi i linia "Karawany (E8/G1)".</summary>
        internal static void Daily()
        {
            var s = Settings.Current;
            if (s == null || Campaign.Current == null) return;
            long t0 = Stopwatch.GetTimestamp();
            int day = (int)CampaignTime.Now.ToDays - 1;
            try { Islands(s, day); } catch (Exception e) { Stumble("Islands", e); }
            try
            {
                List<MobileParty> gone = null;                                   // pamiec postojow: karawana wyjechala albo znikla
                foreach (var kv in _stay)
                {
                    bool ok = false;
                    try { ok = kv.Key != null && kv.Key.IsActive && kv.Key.CurrentSettlement == kv.Value.At; } catch { }
                    if (!ok) { if (gone == null) gone = new List<MobileParty>(); gone.Add(kv.Key); }
                }
                if (gone != null) foreach (var k in gone) _stay.Remove(k);
            }
            catch (Exception e) { Stumble("Daily(postoje)", e); }
            if (_census == null)
            {
                try { _census = Census(); } catch (Exception e) { _census = "blad odczytu"; Stumble("Census", e); }
            }
            int cap1 = 0, cap2 = 0, cap3 = 0, capMax = 0, capAll = 0;
            try { CapCensus(out cap1, out cap2, out cap3, out capMax, out capAll); } catch (Exception e) { Stumble("CapCensus", e); }
            try
            {
                double tickMs = _dTicks * 1000.0 / Stopwatch.Frequency;
                var sb = new StringBuilder(1400);
                sb.Append("Karawany (E8/G1): dzien ").Append(day)
                  .Append(" | dlawik BKROT (Caravan Town Hourly ").Append(s.CaravanTownHourly ? "CZYNNE" : "WYLACZONE").Append("): tiki BK zamkniete karawanom AI - w drodze ").Append(_dRoad)
                  .Append(" (zostaje raz na dobe), w oblezonym miescie ").Append(_dSiege).Append(", w miescie ").Append(_dTown).Append(" - otwarte ").Append(_dOpened)
                  .Append(" u ").Append(_dCars.Count).Append(" karawan (w cieniu ").Append(_dTown - _dOpened - _dShut).Append("), w tym ponad ").Append(LongStayTicks)
                  .Append(" tiki w tym samym postoju: ").Append(_dLongTicks).Append(" tikow u ").Append(_dLongCars.Count).Append(" karawan (bez celu albo ranni)")
                  .Append(" | po otwarciu: tiki BK ").Append(_dRan).Append(", nowy cel karawany ").Append(_dOrders)
                  .Append(", koszt dodatkowych tikow ").Append(tickMs.ToString("0.00", Inv)).Append(" ms/dobe")
                  .Append(" | wyspy (Island Caravan Fix ").Append(s.IslandCaravanFix ? "CZYNNE" : "WYLACZONE").Append("): BK chcial kupic karawane lordowi ").Append(_dBuyAsk)
                  .Append(" razy (ze statkami ").Append(_dBuyShips).Append("), w osadzie bez drogi ladowej do dwoch miast i bez statkow: zakup pominiety ").Append(_dBuySkip)
                  .Append(Top(_dBuyAt, 4)).Append(" (w cieniu ").Append(_dBuyShadow).Append(")")
                  .Append(" | karawany bez drogi (bez statkow w miescie bez innego miasta w zasiegu ladu): stan ").Append(_dStuck).Append(", rozwiazane ").Append(_dDone)
                  .Append(Top(_dDoneAt, 4)).Append(", zostalo ").Append(_dLeft).Append(" (w cieniu ").Append(_dGoneShadow).Append("; powody").Append(_dKept.Count > 0 ? Top(_dKept, 6) : " -")
                  .Append("), ponowne u tego samego wlasciciela ").Append(_dAgain)
                  .Append(" | rozwiazane dzis: towar sprzedany miastu ").Append(_dSoldU).Append(" szt. za ").Append(_dSoldG)
                  .Append(" zl z nadwyzki kasy (kasy ponizej zapasu przez nasza wyplate ").Append(_dBelow).Append("), niesprzedane ").Append(_dUnsoldU).Append(" szt. warte ").Append(_dUnsoldV)
                  .Append(" zl (polka miasta bez zaplaty - E6/K1), kiesa z utargiem ").Append(_dPurse).Append(" zl (do wlasciciela - E3/7; wlascicielom przybylo ").Append(_dOwnerGot)
                  .Append("), ludzie ").Append(_dMen).Append(" (do domu - E4/8), sakiewki ludzi ").Append(_dMenPurse).Append(" zl (kasa najblizszego miasta - wpis 89), nie zniknely mimo rozkazu ").Append(_dFail)
                  .Append(_dEx.Count > 0 ? "; przyklady: " + string.Join("; ", _dEx) : "")
                  .Append(" | slepe zaulki mapy dla karawany bez statkow (raz na sesje): ").Append(_census ?? "-")
                  .Append(" | latki wpiete ").Append(_wiredN).Append('/').Append(_wantedN)
                  .Append(_missing.Count > 0 ? " (BRAK: " + string.Join(", ", _missing.ToArray()) + ")" : "")
                  .Append(" | koszt linii ").Append(((Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency).ToString("0.00", Inv)).Append(" ms")
                  .Append(" | potkniecia dzis ").Append(_stumblesDay).Append(" (od wczytania ").Append(_stumbles).Append(")");
                // dopiski NA KONCU linii (pola wyzej czyta narzedzie progow - bez zmian)
                sb.Append(" | limit karawan kupca (Notable Caravan Cap ").Append(s.NotableCaravanCap).Append(s.NotableCaravanCap > 0 ? "" : " - WYLACZONE, regula gry")
                  .Append("; latka ").Append(_capHook).Append("): gra chciala dac kolejna karawane kupcowi, ktory juz ma karawany, ").Append(_dCapAsk)
                  .Append(" razy, wstrzymane limitem ").Append(_dCapHeld).Append(" u ").Append(_dCapWho.Count).Append(" kupcow")
                  .Append(" | karawany notabli (wszystkie, takze spiace): kupcow z 1 karawana ").Append(cap1).Append(", z 2 ").Append(cap2).Append(", z 3 i wiecej ").Append(cap3)
                  .Append(" (najwiecej ").Append(capMax).Append("), razem karawan notabli ").Append(capAll);
                sb.Append(" | limit postoju (Caravan Town Hourly Max Ticks ").Append(s.CaravanTownHourlyMaxTicks).Append(s.CaravanTownHourlyMaxTicks > 0 ? "" : " - bez limitu")
                  .Append("): zamkniete po limicie ").Append(s.CaravanTownHourlyMaxTicks).Append(" tikow w tym samym postoju: ").Append(_dShut).Append(" tikow u ").Append(_dShutCars.Count)
                  .Append(" karawan (wracaja do tiku BKROT raz na dobe; nowe postoje po limicie dzis ").Append(_dShutNew).Append(", wedlug stanu").Append(_dShutWhy.Count > 0 ? Top(_dShutWhy, 8) : " -")
                  .Append(") | po otwarciu cialo tiku BK pominiete przez inny prefiks (kontrakt 174): ").Append(_dBlocked).Append(" tikow, ")
                  .Append((_dBlockedTicks * 1000.0 / Stopwatch.Frequency).ToString("0.00", Inv)).Append(" ms (poza 'tiki BK' i kosztem dodatkowych tikow)");
                // m6b (6): dopisek na koncu linii
                sb.Append(" | karawany bez zysku (Idle Caravan Disband ").Append(s.IdleCaravanDisband ? "CZYNNE" : "WYLACZONE").Append("; bez statkow, w tym samym miescie z droga do innych miast od ")
                  .Append(IdleDisbandDays.ToString("0", Inv)).Append("+ dob): stan ").Append(_dIdle).Append(", najdluzej ").Append(_dIdleMax.ToString("0.0", Inv)).Append(" dob, rozwiazane ").Append(_dIdleDone)
                  .Append(Top(_dIdleAt, 4)).Append(", zostalo ").Append(_dIdleLeft).Append(" (powody w 'karawany bez drogi' z przedrostkiem 'bez zysku')");
                sb.Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally { ClearDay(); }
        }

        // ------------------------------------------------------------ wpiecie (SubModuleMain - po Stage8World; BKROT i BK sa juz zaladowane)

        private static void Patch(Harmony h, string label, MethodBase m, string post)
        {
            _wantedN++;
            try
            {
                if (h == null) { _missing.Add(label + " (brak Harmony)"); return; }
                if (m == null) { _missing.Add(label); return; }
                h.Patch(m, postfix: new HarmonyMethod(typeof(CaravanFlow), post));
                _wired.Add(label); _wiredN++;
            }
            catch (Exception e) { _missing.Add(label + " (blad: " + e.Message + ")"); }
        }

        internal static void ApplyAll(Harmony h)
        {
            _wired.Clear(); _missing.Clear(); _wiredN = 0; _wantedN = 0;
            try
            {
                // 1. dlawik BKROT: sama metoda prefiksu (static bool Prefix(MobileParty)) i tik BK (pomiar)
                var tThr = QuartermasterLaw.FindType("BKROTPatch.Patches.BKCaravansHourlyThrottlePatch");
                var pre = tThr != null ? AccessTools.Method(tThr, "Prefix") : null;
                var pp = pre != null ? pre.GetParameters() : null;
                if (pre != null && (!pre.IsStatic || pre.ReturnType != typeof(bool) || pp.Length != 1 || pp[0].ParameterType != typeof(MobileParty))) pre = null;
                Patch(h, "dlawik BKROT (BKCaravansHourlyThrottlePatch.Prefix)", pre, nameof(ThrottlePostfix));
                var tCar = AccessTools.TypeByName("BannerKings.Behaviours.BKCaravansBehavior");
                var tick = tCar != null ? AccessTools.Method(tCar, "HourlyTickParty", new[] { typeof(MobileParty) }) : null;
                if (pre == null) tick = null;                                    // bez dlawika nie ma czego mierzyc
                Patch(h, "tik BK karawan - pomiar (BKCaravansBehavior.HourlyTickParty)", tick, nameof(BkHourlyPostfix));
                // 2. zakup karawany lorda BK
                var tLord = AccessTools.TypeByName("BannerKings.Behaviours.BKLordPropertyBehavior");
                var should = tLord != null ? AccessTools.Method(tLord, "ShouldHaveCaravan", new[] { typeof(Hero), typeof(int) }) : null;
                if (should != null && should.ReturnType != typeof(bool)) should = null;
                Patch(h, "zakup karawany lorda BK (BKLordPropertyBehavior.ShouldHaveCaravan)", should, nameof(LordBuyPostfix));
            }
            catch (Exception e) { Log.Error("CaravanFlow.ApplyAll", e); }
            var s = Settings.Current;
            Log.Info("Karawany (E8/G1): latki przy starcie gry - wpiete " + _wiredN + "/" + _wantedN + ": " + (_wired.Count > 0 ? string.Join(", ", _wired.ToArray()) : "-")
                     + "; BRAK: " + (_missing.Count > 0 ? string.Join(", ", _missing.ToArray()) : "-")
                     + " | Caravan Town Hourly " + (s != null && s.CaravanTownHourly ? "CZYNNE" : "wylaczone") + " (tik BK karawany AI w miescie co godzine; w drodze dlawik BKROT raz na dobe zostaje)"
                     + ", Island Caravan Fix " + (s != null && s.IslandCaravanFix ? "CZYNNE" : "wylaczone") + " (lord nie kupuje karawany bez statkow w osadzie bez drogi do dwoch miast; karawana bez drogi rozwiazywana raz na dobe).");
        }
    }
}
