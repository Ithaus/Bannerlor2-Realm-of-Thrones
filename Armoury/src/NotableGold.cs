using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using CW = Armoury.CirculationWindows;

namespace Armoury
{
    /// <summary>
    /// E5/A3 (paczka 164c, PLAN 5.3) - ZLOTO NOTABLI PRZEZ KASE MIASTA HANDLU (projekt etapu 5 v2, regula R10). Wylaczniki NotableGoldReal (pasmo i nowy notabl)
    /// i NotableCaravanCapitalReal (kapital nowej karawany notabla).
    ///
    /// Dzis (dekompilacja gry 1.4.8): NotablePowerManagementBehavior.BalanceGoldAndPowerOfNotable (raz na dobe kazdego notabla) zamienia zloto ponad 10 500
    /// na sile - GiveGoldAction notabl -> nic (w nicosc), a brak ponizej 4 500 przy sile > 0 - nic -> notabl (z niczego), po 500 zl za punkt sily
    /// (G NotablePowerManagementBehavior.cs:47-61); NotablesCampaignBehavior.OnHeroCreated daje nowemu notablowi 10 000 z niczego (G :41-47);
    /// CaravanPartyComponent.CreateCaravanParty nadaje karawanie kapital z modelu (InitializePartyTrade - z niczego, G CaravanPartyComponent.cs:247),
    /// notabl nic nie placi. Bieg z31-A d120: pasmo w nicosc -107 500 / z niczego +69 000, kapital nowych karawan notabli 30 000 (L:31727).
    ///
    /// Regula R10 - druga strona wymiany to KASA MIASTA, W KTORYM NOTABL HANDLUJE: notabl miasta - kasa tego miasta; notabl wsi (ok. 89%) - kasa miasta
    /// targowego wsi (Village.TradeBound; brak albo nie miasto - Village.Bound, zamek przez CastlePurse.Spendable). Kiesa wsi nie jest strona (do B1 bez
    /// definicji nadwyzki, renta 20% oddawalaby wplaty notabli panom). Nadwyzka osady = Seal31.Spare (miasto: kasa ponad zapas kupcow i tarcze; zamek:
    /// CastlePurse.Spendable) - A1 podmienia jej wnetrze na TownPurse.Spare, ta paczka wola jedna funkcje.
    ///  - pasmo: nadwyzka ponad 10 500 -> kasa miasta (przelew; sila jak w grze); brak ponizej 4 500 -> z nadwyzki tej kasy w pelnych 500 (punkt sily), czego
    ///    kasa nie ma - zlota nie ma i sila nie schodzi (finalizer oddaje punkty, ktorych gra nie powinna byla zdjac);
    ///  - nowy notabl: 10 000 z nadwyzki kasy (zloto zmarlego notabla zostaje w osadzie - E3/8c), brak - mniej;
    ///  - kapital nowej karawany notabla: z kiesy notabla ponad 4 500, reszta z nadwyzki kasy miasta startu (kupcy inwestuja), brak - mniejszy kapital
    ///    (ta sama regula co E3/7 dla lordow i gracza). Nadany kapital idzie przez model (okno O17 widzi go jako "kapital nadany z niczego"), dlatego
    ///    zdjecie z kiesy notabla i z kasy miasta jest nazwana korekta zrodel ksiegi (Seal31.KCapNotable) - netto 0, jak kupno karawany BK (KCapFromPrice).
    /// Mechanizm (wzor RealPayer E3/1): prefiks Low + finalizer High na metodzie gry otwiera kontekst (stos do 4), prefiks Low na GiveGoldAction.ApplyInternal
    /// zamienia strone "nic" na kase miasta i przycina kwote do nadwyzki; okna ksiegi obiegu (O03, O04, O17 - prefiksy First) widza przelew, nie zloto
    /// z niczego. Kapital karawany - przez latki E3/7 (CaravanCapital.CreatePre/CapPostfix/CreateFin), gdy nie ma innego kontekstu E3/7 (konwoj DTE, kupno BK,
    /// karawana gracza maja swoje reguly).
    /// Bilans otwarcia (projekt v2, E5K1-05/K4): przed Campaign.GameStarted (tworzenie swiata: SpawnNotablesAtGameStart, ok. 26.8 mln) i dla karawan
    /// z isInitialSpawn regula gry zostaje - to zloto jest w stanie poczatkowym ksiegi (pierwsza doba = sam stan); liczba i kwota w linii startowej.
    /// Gracz = AI (notable w osadach gracza - ta sama regula). Wyjatek per zdarzenie - licznik potkniec, bez globalnego wylacznika.
    ///
    /// E5/A3' (audyt zlota z niczego 11.10, rozdz. 5a; wylacznik NotableBkGiftCut): BK zastepuje HeroCreator.CreateNotable wlasnym prefiksem (BK NotablePatches.cs:21-92):
    /// CreateSpecialHero (zdarzenie HeroCreated - gra daje 10 000 w NotablesCampaignBehavior.OnHeroCreated, to lapie A3) i DRUGIE GiveGoldAction(nic, notabl, 10 000)
    /// (BK :77-81) - z niczego (ksiega: "inne"). Prefiks First (przed prefiksem BK, ktory zwraca false) + finalizer High otwiera kontekst KBk; bohatera wiaze NewPre
    /// (pierwsze HeroCreated w tym oknie); GivePrefix tnie pierwszy dar "nic -> ten notabl" w oknie KBk do 0 (decyzja z audytu: notabl dostaje 10 000 jak w grze,
    /// z kasy miasta przez A3). Przy wylaczonym A3 NewPre i tak kladzie na stos bierny kontekst daru gry (Swapped) - dar gry nie zostanie wziety za dar BK.
    /// Tworzenie swiata (przed GameStarted) - regula BK zostaje (bilans otwarcia, liczba w linii startowej). Bez BK okno KBk niczego nie widzi (licznik okien).
    /// Zaulki (audyt rozdz. 1b wiersz 10, 5b; wylacznik AlleyIncomeFromTown): ClanVariablesCampaignBehavior.DailyTickHero wyplaca notablowi dochod z modelu jednym
    /// GiveGoldAction(nic, notabl) - w nim 30 zl za kazdy zaulek notabla w osadzie, w ktorej jest (G DefaultClanFinanceModel.cs:897-903) - z niczego (reszta to
    /// przelew z kapitalu warsztatow i kas karawan - K1). Prefiks Low otwiera kontekst KAlley (ta sama liczba zaulkow co gra), GivePrefix zdejmuje z wyplaty
    /// czesc za zaulki (najwyzej cala wyplate), a postfiks wyplaca ja zdarzeniem gry z kasy tego miasta (GiveGoldAction.ApplyForSettlementToCharacter - MoneyLedger
    /// widzi przelew kasa miasta -> notabl, platnik i odbiorca), najwyzej z nadwyzki (Seal31.Spare - jak pasmo A3); czego kasa nie ma - 0 (licznik).
    /// </summary>
    internal static class NotableGold
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        internal static bool On { get { var s = Settings.Current; return s != null && s.NotableGoldReal; } }
        internal static bool CapOn { get { var s = Settings.Current; return s != null && s.NotableCaravanCapitalReal; } }
        internal static bool BkCutOn { get { var s = Settings.Current; return s != null && s.NotableBkGiftCut; } }
        internal static bool AlleyOn { get { var s = Settings.Current; return s != null && s.AlleyIncomeFromTown; } }
        private static bool Live { get { var c = Campaign.Current; return c != null && c.GameStarted; } }

        private const int BandHigh = 10500, BandLow = 4500, PowerGold = 500;   // pasmo gry (G NotablePowerManagementBehavior.cs:49, :55, :51)
        private const int CaravanKeep = 4500;                                  // notabl wklada w karawane tylko zloto ponad dolna granice pasma
        private const int KBand = 0, KNew = 1, KBk = 2, KAlley = 3;
        private const int AlleyGold = 30;                                      // dochod za zaulek (G DefaultClanFinanceModel.cs:901)

        internal sealed class Ctx
        {
            public int Kind; public Hero H; public Settlement Town; public bool Village, Opening, Swapped;
            public long Due, Paid;
            public long Pend;                                                  // zaulki: do wyplaty z kasy miasta w postfiksie DailyTickHero
        }

        /// <summary>Kontekst nowej karawany notabla (CaravanCapital.CreatePre - CapPostfix - CreateFin).</summary>
        internal sealed class Car { public Hero H; public Settlement Town; public bool Opening, Seen; public int Depth; }

        private static readonly Ctx[] _stack = new Ctx[4];
        private static int _depth;
        private static readonly Car[] _car = new Car[4];
        private static int _carDepth;

        // liczniki doby (linia "Notable (E5/A3)" i dopisek "Obieg: notable i karawany")
        private static long _bandToTown, _bandToTownV, _bandFromTown, _bandFromTownV, _bandDue, _bandCut, _powerKept;
        private static int _bandToN, _bandFromN, _bandCutN, _bandCalls;
        private static long _newPaid, _newDue;
        private static int _newN, _newCutN;
        private static long _carK, _carF, _carT;
        private static int _carN, _carZero, _carNoModel, _carOff;
        private static long _noTownOut, _noTownIn, _noTownNew, _noTownCar;
        private static int _noTownN, _strays, _overflow, _offN;
        private static int _stumbles;
        // E5/A3' (dar BK) i zaulki - liczniki doby
        private static long _bkCut;
        private static int _bkWin, _bkCutN, _bkOffN;
        private static long _alDue, _alPaid, _alCut;
        private static int _alN, _alCutN, _alOffN, _alNoPay;
        private static readonly HashSet<string> _err = new HashSet<string>();
        // bilans otwarcia (tworzenie swiata) - do linii startowej sesji
        private static long _openGold, _openCarGold, _openBkGold;
        private static int _openN, _openCarN, _openBkN;
        private static bool _startLogged;
        private static readonly List<string> _wired = new List<string>(), _missing = new List<string>();
        private static string _wiredText = "-", _missingText = "-", _others = "-";

        /// <summary>Konstruktor ArmouryBehavior (nowa kampania i kazde wczytanie) - przed tworzeniem swiata, wiec bilans otwarcia liczy sie od zera.</summary>
        internal static void Reset()
        {
            Array.Clear(_stack, 0, _stack.Length); _depth = 0;
            Array.Clear(_car, 0, _car.Length); _carDepth = 0;
            _openGold = _openCarGold = _openBkGold = 0; _openN = _openCarN = _openBkN = 0; _startLogged = false;
            _stumbles = 0; _err.Clear();
            ClearDay();
        }

        private static void ClearDay()
        {
            _bandToTown = _bandToTownV = _bandFromTown = _bandFromTownV = _bandDue = _bandCut = _powerKept = 0;
            _bandToN = _bandFromN = _bandCutN = _bandCalls = 0;
            _newPaid = _newDue = 0; _newN = _newCutN = 0;
            _carK = _carF = _carT = 0; _carN = _carZero = _carNoModel = _carOff = 0;
            _noTownOut = _noTownIn = _noTownNew = _noTownCar = 0; _noTownN = _strays = _overflow = _offN = 0;
            _bkCut = 0; _bkWin = _bkCutN = _bkOffN = 0;
            _alDue = _alPaid = _alCut = 0; _alN = _alCutN = _alOffN = _alNoPay = 0;
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_err.Add(where)) Log.Error("NotableGold." + where, e); } catch { }
        }

        /// <summary>E5/0 (scalenie porcji A, recenzja A3 R2-1 c): kwota nalezna z kasy miasta handlu w biezacym pasmie tego notabla (kontekst A3 do finalizera CtxFin) - 0 bez kontekstu. Tylko odczyt.</summary>
        internal static long BandDueNow(Hero h)
        {
            try { if (_depth <= 0 || h == null) return 0; var c = _stack[_depth - 1]; return c != null && c.Kind == KBand && ReferenceEquals(c.H, h) && c.Due > 0 ? c.Due : 0; }
            catch { return 0; }
        }

        private static bool Push(Ctx c) { if (_depth >= _stack.Length) { _overflow++; return false; } _stack[_depth++] = c; return true; }

        private static void Pop(Ctx c)
        {
            if (c == null || _depth == 0) return;
            if (ReferenceEquals(_stack[_depth - 1], c)) { _stack[--_depth] = null; return; }
            for (int i = _depth - 1; i >= 0; i--) if (ReferenceEquals(_stack[i], c)) { for (int j = i; j < _depth - 1; j++) _stack[j] = _stack[j + 1]; _stack[--_depth] = null; _strays++; return; }
        }

        /// <summary>Kasa, w ktorej notabl handluje: miasto (albo zamek) - ta osada; wies - miasto targowe (TradeBound), brak albo nie miasto - Bound. null - brak.</summary>
        internal static Settlement TradeTown(Hero h, out bool village)
        {
            village = false;
            // osada, w ktorej notabl mieszka - jak gra dla notabla (Hero.UpdateHomeSettlement: CurrentSettlement, potem osada urodzenia); HomeSettlement
            // bez tego bylaby siedziba rodu, gdyby notabl mial rod; przy tworzeniu (przed EnterSettlementAction) CurrentSettlement jeszcze null
            var st = h != null ? (h.CurrentSettlement ?? h.HomeSettlement) : null;
            if (st == null) return null;
            if (st.IsTown || st.IsCastle) return st.Town != null ? st : null;
            if (!st.IsVillage || st.Village == null) return null;
            village = true;
            var tb = st.Village.TradeBound;
            if (tb != null && tb.IsTown && tb.Town != null) return tb;
            var b = st.Village.Bound;
            return b != null && b.Town != null ? b : null;
        }

        // ------------------------------------------------------------ konteksty: pasmo i nowy notabl
        /// <summary>BalanceGoldAndPowerOfNotable(notabl) - prefiks Low: kontekst tylko, gdy gra zaraz zamieni zloto i sile (te same progi co gra).</summary>
        public static void BandPre(Hero __0, out Ctx __state)
        {
            __state = null;
            try
            {
                if (__0 == null || !Live) return;
                _bandCalls++;
                int g = __0.Gold;
                if (!(g > BandHigh || (g < BandLow && __0.Power > 0f))) return;
                if (!On) { _offN++; return; }
                bool v;
                var c = new Ctx { Kind = KBand, H = __0, Town = TradeTown(__0, out v), Village = v };
                if (Push(c)) __state = c;
            }
            catch (Exception e) { __state = null; Stumble("BandPre", e); }
        }

        /// <summary>NotablesCampaignBehavior.OnHeroCreated(bohater, ...) - prefiks Low: nowy notabl dostaje 10 000 z kasy miasta handlu; przed startem gry - bilans otwarcia.</summary>
        public static void NewPre(Hero __0, out Ctx __state)
        {
            __state = null;
            try
            {
                if (__0 == null) return;
                bool live = Live;
                // E5/A3': pierwsze HeroCreated w oknie CreateNotable (BK: CreateSpecialHero) - to jest nowy notabl, ktoremu BK da drugie 10 000
                var top = _depth > 0 ? _stack[_depth - 1] : null;
                bool inBk = top != null && top.Kind == KBk && top.H == null && !top.Swapped;
                if (inBk) top.H = __0;
                if (live && !On)
                {
                    if (!inBk) return;
                    var p = new Ctx { Kind = KNew, H = __0, Swapped = true };   // A3 wylaczone: dar gry jak dotad (z niczego) - bierny kontekst, zeby okno KBk nie wzielo go za dar BK
                    if (Push(p)) __state = p;
                    return;
                }
                bool v = false;
                var town = live ? TradeTown(__0, out v) : null;
                var c = new Ctx { Kind = KNew, H = __0, Opening = !live, Town = town, Village = v };
                if (Push(c)) __state = c;
            }
            catch (Exception e) { __state = null; Stumble("NewPre", e); }
        }

        /// <summary>E5/A3': HeroCreator.CreateNotable - prefiks First (przed prefiksem BK, ktory zastepuje metode gry): kontekst daru BK; bohatera wiaze NewPre.</summary>
        public static void BkPre(out Ctx __state)
        {
            __state = null;
            try
            {
                bool live = Live;
                if (live && !BkCutOn) { _bkOffN++; return; }
                var c = new Ctx { Kind = KBk, Opening = !live };
                if (Push(c)) { __state = c; if (live) _bkWin++; }
            }
            catch (Exception e) { __state = null; Stumble("BkPre", e); }
        }

        /// <summary>Zaulki: ClanVariablesCampaignBehavior.DailyTickHero(notabl) - prefiks Low: kontekst, gdy notabl ma zaulki w osadzie, w ktorej jest (te same warunki co gra).</summary>
        public static void AlleyPre(Hero __0, out Ctx __state)
        {
            __state = null;
            try
            {
                if (__0 == null || !Live || !__0.IsActive || !__0.IsNotable) return;
                var st = __0.CurrentSettlement;
                var al = st != null ? st.Alleys : null;
                if (al == null || al.Count == 0) return;
                int n = 0;
                for (int i = 0; i < al.Count; i++) { var a = al[i]; if (a != null && ReferenceEquals(a.Owner, __0)) n++; }
                if (n == 0) return;
                if (!AlleyOn) { _alOffN++; return; }
                var c = new Ctx { Kind = KAlley, H = __0, Town = st, Due = (long)n * AlleyGold };
                if (Push(c)) __state = c;
            }
            catch (Exception e) { __state = null; Stumble("AlleyPre", e); }
        }

        /// <summary>Zaulki: postfiks DailyTickHero (przed finalizerami) - czesc za zaulki zdjeta z wyplaty gry placi kasa miasta zdarzeniem gry (przelew w ksiedze).</summary>
        public static void AlleyPost(Ctx __state)
        {
            var c = __state;
            if (c == null || c.Kind != KAlley || !c.Swapped) return;
            try
            {
                long pay = c.Pend;
                c.Pend = 0;
                if (pay <= 0 || c.Town == null || c.Town.Party == null || c.Town.SettlementComponent == null || c.H == null) return;
                int before = c.H.Gold;
                GiveGoldAction.ApplyForSettlementToCharacter(c.Town, c.H, (int)pay, true);   // zdarzenie gry: kasa miasta -> notabl (kwota w nadwyzce, wiec bez obciecia kasy do 0)
                long got = (long)c.H.Gold - before;
                c.Paid = got;
                _alPaid += got;
                if (got < c.Due) { _alCut += c.Due - got; _alCutN++; }
            }
            catch (Exception e) { Stumble("AlleyPost", e); }
        }

        /// <summary>Wspolny finalizer (High - przed finalizerami okien ksiegi): zdjecie kontekstu; pasmo - punkty sily za zloto, ktorego kasa nie miala, wracaja.</summary>
        public static void CtxFin(Ctx __state)
        {
            if (__state == null) return;
            Pop(__state);
            try
            {
                var c = __state;
                if (c.Kind == KBand && c.Due > c.Paid && c.H != null)
                {
                    long back = (c.Due - c.Paid) / PowerGold;
                    if (back > 0) { c.H.AddPower(back); _powerKept += back; }
                }
            }
            catch (Exception e) { Stumble("CtxFin", e); }
        }

        // ------------------------------------------------------------ wspolny prefiks na GiveGoldAction.ApplyInternal (Low; RealPayer ma osobny)
        public static void GivePrefix(Hero __0, ref PartyBase __1, Hero __2, ref PartyBase __3, ref int __4)
        {
            if (_depth == 0) return;
            try
            {
                var c = _stack[_depth - 1];
                if (c == null || c.Swapped || __4 <= 0) return;
                if (c.Kind == KBk)
                {
                    // E5/A3': dar BK "nic -> nowy notabl" (BK NotablePatches.cs:80); bohater zwiazany w NewPre, bez wiazania - pierwszy dar z niczego dla notabla
                    bool gift = __0 == null && __1 == null && __3 == null && __2 != null && (c.H != null ? ReferenceEquals(__2, c.H) : __2.IsNotable);
                    if (!gift) { if (!c.Opening && __0 == null && __1 == null) _strays++; return; }
                    c.Swapped = true; c.H = __2;
                    if (c.Opening) { _openBkN++; _openBkGold += __4; return; }   // tworzenie swiata - regula BK (bilans otwarcia)
                    _bkCutN++; _bkCut += __4;
                    __4 = 0;                                                     // uciete - zloto nie powstaje (GiveGoldAction z 0: kiesy bez zmian, zdarzenie z 0 ksiega pomija)
                    return;
                }
                if (c.H == null) return;
                if (c.Kind == KAlley)
                {
                    // zaulki: tylko wyplata dochodu "nic -> ten notabl"; inne zdarzenia w dobie notabla (przelew podatku BK E3/8a) nie sa nasze
                    if (!(__0 == null && __1 == null && ReferenceEquals(__2, c.H) && __3 == null)) return;
                    c.Swapped = true;
                    long part = Math.Min(c.Due, (long)__4);                     // czesc za zaulki (przy wyplacie mniejszej niz zaulki - cala wyplata)
                    c.Due = part;
                    __4 = (int)(__4 - part);                                     // reszta jak w grze (przelew z warsztatow i karawan - K1)
                    long spA = c.Town != null && c.Town.SettlementComponent != null ? Seal31.Spare(c.Town) : 0;
                    c.Pend = Math.Max(0L, Math.Min(part, spA));
                    _alN++; _alDue += part;
                    if (c.Pend <= 0) { _alNoPay++; _alCut += part; _alCutN++; }
                    return;
                }
                bool toH = __0 == null && __1 == null && ReferenceEquals(__2, c.H) && __3 == null;                     // nic -> notabl
                bool fromH = c.Kind == KBand && ReferenceEquals(__0, c.H) && __1 == null && __2 == null && __3 == null;  // notabl -> nic (tylko pasmo)
                if (!toH && !fromH) { if (!c.Opening) _strays++; return; }   // tworzenie swiata: inne zdarzenia w oknie nowego bohatera - bez licznika
                c.Swapped = true;
                if (c.Opening) { _openN++; _openGold += __4; return; }   // tworzenie swiata - regula gry (bilans otwarcia)
                var town = c.Town;
                if (town == null || town.Party == null || town.SettlementComponent == null)
                {
                    _noTownN++;                                          // bez kasy handlu - jak w grze (z niczego / w nicosc), licznik (prog 0)
                    if (fromH) _noTownOut += Math.Min(__4, Math.Max(0, c.H.Gold)); else if (c.Kind == KNew) _noTownNew += __4; else _noTownIn += __4;
                    return;
                }
                if (fromH)
                {
                    int amt = Math.Min(__4, Math.Max(0, c.H.Gold));     // gra obcina do kiesy notabla (GiveGoldAction.cs:13)
                    __3 = town.Party;
                    c.Paid = amt;
                    if (c.Village) _bandToTownV += amt; else _bandToTown += amt;
                    _bandToN++;
                    return;
                }
                long due = __4, sp = Seal31.Spare(town);
                long pay = c.Kind == KBand ? Math.Min(due, sp / PowerGold * PowerGold) : Math.Min(due, sp);
                if (pay < 0) pay = 0;
                __1 = town.Party;                                          // dajacy = kasa miasta handlu (gra obcina do calej kasy - nadwyzka liczona tu)
                __4 = (int)pay;
                c.Due = due; c.Paid = pay;
                if (c.Kind == KBand)
                {
                    _bandDue += due; _bandFromN++;
                    if (c.Village) _bandFromTownV += pay; else _bandFromTown += pay;
                    if (pay < due) { _bandCut += due - pay; _bandCutN++; }
                }
                else
                {
                    _newN++; _newDue += due; _newPaid += pay;
                    if (pay < due) _newCutN++;
                }
            }
            catch (Exception e) { Stumble("GivePrefix", e); }
        }

        // ------------------------------------------------------------ kapital nowej karawany notabla (wolane z latek E3/7 w CaravanCapital)
        /// <summary>CaravanCapital.CreatePre, gdy nie ma kontekstu E3/7: karawana notabla - kontekst (depth = glebokosc CreateCaravanParty).</summary>
        internal static Car CarOpen(Hero owner, Settlement spawn, bool initial, int depth)
        {
            try
            {
                if (owner == null || !owner.IsNotable) return null;
                bool opening = initial || !Live;
                if (!opening && !CapOn) { _carOff++; return null; }   // wylaczone - kapital z niczego jak dotad (licznik)
                if (_carDepth >= _car.Length) { _overflow++; return null; }
                var c = new Car { H = owner, Town = spawn, Opening = opening, Depth = depth };
                _car[_carDepth++] = c;
                return c;
            }
            catch (Exception e) { Stumble("CarOpen", e); return null; }
        }

        /// <summary>CaravanCapital.CapPostfix (GetInitialTradeGold czynnego modelu, Low - przed oknem O17): pierwszy wynik w oknie tej karawany.
        /// Kapital = z kiesy notabla ponad 4 500 + z nadwyzki kasy miasta startu; zdjecie nazwane korekta zrodel ksiegi (O17 zobaczy kapital jako nadany).</summary>
        internal static bool CarCap(int depth, ref int result)
        {
            if (_carDepth == 0) return false;
            try
            {
                var c = _car[_carDepth - 1];
                if (c == null || c.Seen || c.Depth != depth) return false;
                c.Seen = true;
                long k = Math.Max(0, result);
                if (c.Opening) { _openCarN++; _openCarGold += k; return true; }   // tworzenie swiata - regula gry (bilans otwarcia)
                long f = Math.Min(k, Math.Max(0L, (long)c.H.Gold - CaravanKeep));
                long t = 0;
                if (c.Town != null && c.Town.SettlementComponent != null) t = Math.Min(k - f, Seal31.Spare(c.Town));
                else { _noTownN++; _noTownCar += k - f; }
                if (t < 0) t = 0;
                if (f > 0) { c.H.ChangeHeroGold(-(int)f); CW.NoteHeroGold(c.H, -f); }
                if (t > 0)
                {
                    c.Town.SettlementComponent.ChangeGold(-(int)t);
                    MoneyLedger.Note169(MoneyLedger.N169Other, c.Town, -(int)t);   // "Przeplywy osad (przyczyny)": kasa miasta -> karawana (tylko licznik)
                }
                result = (int)(f + t);
                Seal31.AddWorld(Seal31.KCapNotable, f + t);                         // korekta zrodel: O17 policzy ten kapital jako "nadany z niczego"
                _carN++; _carK += k; _carF += f; _carT += t;
                if (f + t == 0) _carZero++;
                return true;
            }
            catch (Exception e) { Stumble("CarCap", e); return false; }
        }

        /// <summary>CaravanCapital.CreateFin: zdjecie kontekstu (takze po wyjatku w metodzie gry).</summary>
        internal static void CarClose(Car c, MobileParty created)
        {
            if (c == null) return;
            try
            {
                if (_carDepth > 0 && ReferenceEquals(_car[_carDepth - 1], c)) _car[--_carDepth] = null;
                else
                {
                    for (int i = _carDepth - 1; i >= 0; i--) if (ReferenceEquals(_car[i], c)) { for (int j = i; j < _carDepth - 1; j++) _car[j] = _car[j + 1]; _car[--_carDepth] = null; _strays++; break; }
                }
                if (!c.Seen && created != null)
                {
                    // recenzja E5/A3 1-2: latka modelu (CapPostfix) wpina sie w OnSessionLaunched (Seal31.EnsureHooks), a gra tworzy karawany startowe wczesniej
                    // (OnNewGameCreated -> CaravansCampaignBehavior.cs:342-350) - w swiezym procesie bilans otwarcia liczy sie tu: kapital = kiesa karawany
                    // zaraz po utworzeniu (przed startem gry wejscie do miasta nie handluje - CaravansCampaignBehavior.cs:694). Gdy latka juz jest, Seen = true.
                    if (c.Opening) { _openCarN++; _openCarGold += Math.Max(0, created.PartyTradeGold); }
                    else _carNoModel++;                                         // latka modelu niewpieta - kapital z niczego jak dotad (prog 0)
                }
            }
            catch (Exception e) { Stumble("CarClose", e); }
        }

        // ------------------------------------------------------------ wpiecia (SubModuleMain - po oknach ksiegi i Seal31)
        private static void Patch(Harmony h, string label, MethodBase m, string pre, string fin, int prePrio, int finPrio)
        {
            try
            {
                if (h == null) { _missing.Add(label + " (brak Harmony)"); return; }
                if (m == null) { _missing.Add(label); return; }
                h.Patch(m, prefix: pre != null ? new HarmonyMethod(typeof(NotableGold), pre) { priority = prePrio } : null,
                           finalizer: fin != null ? new HarmonyMethod(typeof(NotableGold), fin) { priority = finPrio } : null);
                _wired.Add(label);
            }
            catch (Exception e) { _missing.Add(label + " (blad: " + e.Message + ")"); }
        }

        internal static void ApplyAll(Harmony h)
        {
            _wired.Clear(); _missing.Clear();
            var band = AccessTools.Method(typeof(NotablePowerManagementBehavior), "BalanceGoldAndPowerOfNotable", new[] { typeof(Hero) });
            var fresh = AccessTools.Method(typeof(NotablesCampaignBehavior), "OnHeroCreated", new[] { typeof(Hero), typeof(bool) });
            var give = AccessTools.Method(typeof(GiveGoldAction), "ApplyInternal");
            var bkNew = AccessTools.Method(typeof(HeroCreator), "CreateNotable");
            var tick = AccessTools.Method(typeof(ClanVariablesCampaignBehavior), "DailyTickHero", new[] { typeof(Hero) });
            Patch(h, "pasmo notabli (BalanceGoldAndPowerOfNotable)", band, nameof(BandPre), nameof(CtxFin), Priority.Low, Priority.High);
            Patch(h, "nowy notabl (NotablesCampaignBehavior.OnHeroCreated)", fresh, nameof(NewPre), nameof(CtxFin), Priority.Low, Priority.High);
            // E5/A3': prefiks First - przed prefiksem BK (Normal), ktory zastepuje CreateNotable i zwraca false; finalizer biegnie zawsze
            Patch(h, "dar BK nowego notabla (HeroCreator.CreateNotable)", bkNew, nameof(BkPre), nameof(CtxFin), Priority.First, Priority.High);
            // zaulki: prefiks Low i postfiks (wyplata z kasy miasta) - przed finalizerami okna O01 i E3/8a (Seal31B.HeroTickFin)
            try
            {
                if (h == null) _missing.Add("zaulki (brak Harmony)");
                else if (tick == null) _missing.Add("zaulki (ClanVariablesCampaignBehavior.DailyTickHero)");
                else
                {
                    h.Patch(tick, prefix: new HarmonyMethod(typeof(NotableGold), nameof(AlleyPre)) { priority = Priority.Low },
                                  postfix: new HarmonyMethod(typeof(NotableGold), nameof(AlleyPost)) { priority = Priority.Normal },
                                  finalizer: new HarmonyMethod(typeof(NotableGold), nameof(CtxFin)) { priority = Priority.High });
                    _wired.Add("zaulki (ClanVariablesCampaignBehavior.DailyTickHero)");
                }
            }
            catch (Exception e) { _missing.Add("zaulki (blad: " + e.Message + ")"); }
            Patch(h, "strona przelewu (GiveGoldAction.ApplyInternal)", give, nameof(GivePrefix), null, Priority.Low, Priority.Normal);
            _wiredText = _wired.Count > 0 ? string.Join(", ", _wired.ToArray()) : "-";
            _missingText = _missing.Count > 0 ? string.Join(", ", _missing.ToArray()) : "-";
            // przy kodzie (projekt rozdz. 5.3): kto jeszcze lata metody pasma i nowego notabla
            try
            {
                var others = new List<string>();
                foreach (var m in new MethodBase[] { band, fresh, bkNew })
                {
                    var info = m != null ? Harmony.GetPatchInfo(m) : null;
                    if (info == null) continue;
                    foreach (var o in info.Owners) if (h == null || o != h.Id) if (!others.Contains(o)) others.Add(o);
                }
                _others = others.Count > 0 ? string.Join(", ", others.ToArray()) : "brak";
            }
            catch (Exception e) { _others = "odczyt nieudany (" + e.Message + ")"; }
            Log.Info("Notable (E5/A3): latki przy starcie gry - wpiete: " + _wiredText + "; BRAK: " + _missingText + "; latki innych modow na pasmie, nowym notablu i CreateNotable (BK): " + _others
                     + "; kapital karawan notabli - przez latki E3/7 (CaravanCapital: CreateCaravanParty, GetInitialTradeGold w kampanii).");
        }

        // ------------------------------------------------------------ linie (ArmouryBehavior - PO MoneyLedger.Daily, ktora czyta ObiegPart tej doby)
        internal static void Daily()
        {
            try
            {
                var s = Settings.Current;
                if (Campaign.Current == null || s == null || !s.LogEnabled) return;
                int day = (int)CampaignTime.Now.ToDays - 1;
                if (!_startLogged)
                {
                    _startLogged = true;
                    Log.Info("Notable (E5/A3): start | wylaczniki: Notable Gold Real " + (On ? "tak" : "nie") + ", Notable Caravan Capital Real " + (CapOn ? "tak" : "nie")
                             + ", Notable Bk Gift Cut " + (BkCutOn ? "tak" : "nie") + ", Alley Income From Town " + (AlleyOn ? "tak" : "nie")
                             + " | latki: " + _wiredText + "; BRAK: " + _missingText + " | bilans otwarcia (tworzenie swiata - regula gry, poza E5/A3): "
                             + (_openN + _openCarN + _openBkN > 0 ? "notable " + _openN + " (zloto gry " + _openGold + "), dar BK nowych notabli " + _openBkN + " (zloto BK " + _openBkGold + "), karawany startowe " + _openCarN + " (kapital gry " + _openCarGold + ")"
                                                         : "brak w tej sesji (wczytany zapis - notable i karawany juz istnieja, regula od pierwszej doby)") + ".");
                }
                var sb = new StringBuilder(1200);
                sb.Append("Notable (E5/A3): dzien ").Append(day);
                if (!On) sb.Append(" | pasmo i nowi notable: WYLACZONE (Notable Gold Real) - z niczego / w nicosc jak dotad (pominiete konteksty ").Append(_offN).Append(')');
                sb.Append(" | pasmo 4500-10500 przez kase miasta handlu: nadwyzka do kas ").Append(_bandToTown + _bandToTownV).Append(" (notable miast ").Append(_bandToTown)
                  .Append(" / wsi ").Append(_bandToTownV).Append("; zdarzen ").Append(_bandToN).Append("), brak z kas ").Append(_bandFromTown + _bandFromTownV).Append(" (notable miast ")
                  .Append(_bandFromTown).Append(" / wsi ").Append(_bandFromTownV).Append("; zdarzen ").Append(_bandFromN).Append(", nalezne ").Append(_bandDue).Append("), uciete ").Append(_bandCut)
                  .Append(" (kasa bez nadwyzki u ").Append(_bandCutN).Append(" notabli - sila nie zeszla ").Append(_powerKept).Append(" pkt; okien pasma ").Append(_bandCalls).Append(')')
                  .Append(" | nowi notable: ").Append(_newN).Append(", z kas ").Append(_newPaid).Append(" (nalezne ").Append(_newDue).Append(", uciete u ").Append(_newCutN).Append(')')
                  .Append(" | kapital karawan notabli").Append(CapOn ? "" : " WYLACZONE (Notable Caravan Capital Real - z niczego jak dotad, karawan " + _carOff + ")").Append(": ").Append(_carN)
                  .Append(" karawan, gra dalaby ").Append(_carK).Append(", z kies notabli ").Append(_carF).Append(", z kas miast startu ").Append(_carT).Append(", uciete ").Append(_carK - _carF - _carT)
                  .Append(" (bez kapitalu ").Append(_carZero).Append(')')
                  .Append(" | dar BK nowych notabli (A3')").Append(BkCutOn ? "" : " WYLACZONE (Notable Bk Gift Cut - z niczego jak dotad, okien " + _bkOffN + ")").Append(": uciete ").Append(_bkCut)
                  .Append(" u ").Append(_bkCutN).Append(" notabli (okien CreateNotable ").Append(_bkWin).Append(')')
                  .Append(" | zaulki z kasy miasta").Append(AlleyOn ? "" : " WYLACZONE (Alley Income From Town - z niczego jak dotad, notabli " + _alOffN + ")").Append(": nalezne ").Append(_alDue)
                  .Append(" u ").Append(_alN).Append(" notabli, z kas ").Append(_alPaid).Append(", uciete ").Append(_alCut).Append(" (kasa bez nadwyzki u ").Append(_alCutN)
                  .Append(", w tym nic ").Append(_alNoPay).Append(')')
                  .Append(" | bez kasy handlu - jak w grze (prog 0): zdarzen ").Append(_noTownN).Append(", w nicosc ").Append(_noTownOut).Append(", z niczego ").Append(_noTownIn + _noTownNew + _noTownCar)
                  .Append(" | kontrola (prog 0): zdarzenia w kontekscie niedopasowane ").Append(_strays).Append(", stos przepelniony ").Append(_overflow)
                  .Append(", karawana bez odczytu modelu ").Append(_carNoModel);
                if (_stumbles > 0) sb.Append(" | potkniecia ").Append(_stumbles).Append(" (pierwszy wyjatek z miejsca w logu jako ERROR)");
                sb.Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally { ClearDay(); }
        }

        /// <summary>Dopisek linii "Obieg: notable i karawany" (MoneyLedger.Daily - przed Daily tej klasy, ktora zeruje liczniki).</summary>
        internal static string ObiegPart()
        {
            try
            {
                return " | E5/A3 przez kase miasta handlu [P]: pasmo do kas " + (_bandToTown + _bandToTownV).ToString(Inv) + ", z kas " + (_bandFromTown + _bandFromTownV).ToString(Inv)
                       + " (uciete " + _bandCut.ToString(Inv) + "), nowi notable z kas " + _newPaid.ToString(Inv) + ", kapital karawan notabli z kies notabli " + _carF.ToString(Inv)
                       + " i kas miast " + _carT.ToString(Inv) + " (w 'kapital nadany' nizej - korekta zrodel bilansu)" + (On ? "" : " [pasmo WYLACZONE]") + (CapOn ? "" : " [kapital WYLACZONY]")
                       + ", dar BK nowych notabli uciety " + _bkCut.ToString(Inv) + (BkCutOn ? "" : " [WYLACZONE]") + ", zaulki z kas miast " + _alPaid.ToString(Inv) + " (przelew; uciete " + _alCut.ToString(Inv) + ")" + (AlleyOn ? "" : " [WYLACZONE]");
            }
            catch { return ""; }
        }
    }
}
