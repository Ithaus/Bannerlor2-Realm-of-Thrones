using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// 500-b AI RANNI (Jeff 14.11: "lordowie stoja az sie wylecza a potem jada do zamku bo tam sie szybciej wylecza"; "dodaj tez zeby Ai wiedialo ze rany sa
    /// powazne i zeby lrodwie jak glupi nie biegali i ryzykowlai gdy to niepotrzebne"; pamiec "AI walczy o zycie"). Decyzja: docs/audyty-2026-11-14/PERKI-DECYZJE.md.
    ///
    /// Dlaczego: 500 / 500-a - w marszu ladem leczenie x MovingHealPercent (10%), w spoczynku pelne, w twierdzy gra dodaje swoje (szeregowi i bohaterowie);
    /// loze HeroWounds w twierdzy schodzi szybciej. Poturbowany lord, ktory dalej jezdzi, nie leczy sie wcale - wiec najpierw POSTOJ, potem TWIERDZA.
    ///
    /// (A) PRZEGLAD GODZINOWY (ArmouryBehavior, HourlyTick; jedyne miejsce zmiany stanu, watek glowny). Partie lordow AI poza armia, bitwa, oblezeniem, morzem,
    ///     rodem gracza i lordami fabuly (te same wyjatki co 408). Ostroznosc c w [0,1] = max(cH, cW): cH z HP wodza (HpStart% -> prog rannego gry
    ///     WoundedHealthLimit), cW z udzialu rannych szeregowych (ShareStart% -> RestShare%, od 10 ludzi); loze HeroWounds wodza = 1.
    ///     Stan partii (tylko w pamieci, czyszczony w Reset):
    ///      - None -> Stand (POSTOJ): HP wodza &lt;= AiWoundedRestHp (E3: 20 = prog rannego gry; nosze gracza 501 maja swoje LitterHp*) albo loze Grave
    ///        ALBO ranni &gt;= RestShare%;
    ///        postoj tylko w bezpiecznym miejscu (jedzenie na 2 doby, bez silniejszego wroga w promieniu AiCampDangerRadius, najblizsza osada nie wroga),
    ///        inaczej od razu Fort. Skrot: twierdza, ktora wybierze 408 (HeroWounds.AiRestFort: najblizsza RODU, potem FRAKCJI, potem SOJUSZNIKA - E2),
    ///        blizej niz RestSkipHours drogi (albo partia stoi w twierdzy swojej frakcji / sojusznika) - od razu Fort. Brak twierdzy z droga przy niebezpiecznym postoju - None (zwykle decyzje,
    ///        ostroznosc dziala) i CoolHours bez ponownego wejscia.
    ///      - Stand -> Fort: HP &gt; AiWoundedRestReleaseHp (E3: 30), bez loza Grave i ranni &lt;= RestExitShare% (zdolna do drogi) ALBO RestMaxDays dob postoju ALBO zagrozenie /
    ///        glod ALBO cudzy rozkaz drugi raz przestawil partie (bez migotania - ustepujemy). Gdy przy wyjsciu c &lt; 0.25 - od razu None; gdy nie ma
    ///        twierdzy z droga - None i CoolHours bez wejscia (bez petli postoj -> Fort bez celu -> postoj).
    ///      - Fort -> None: c &lt; 0.25 albo FortMaxDays (10) dob (zapas; potem CoolHours bez wejscia) - ale nie przed koncem loza HeroWounds wodza (524).
    ///      - 524 JEDZENIE (w zamku nie kupi: gra i BK kupuja tylko w miescie / wsi; gra przy glodzie odejmuje rannych): glod albo zapas ponizej progu zamku
    ///        (HeroWounds.CastleFoodDays = NeededFoodsInDaysThresholdForSiege, 12) przy ZAMKU jako twierdzy spoczynku - Fort konczy sie, postoj nie jedzie do zamku,
    ///        wejscie nie nastepuje (glod - zadne wejscie); stan None BEZ blokady CoolHours, cel liczony ponownie najwczesniej po FoodRecheckHours. Miasto - bez zmian.
    ///     Przy zmianie stanu RethinkAtNextHourlyTick (mysl w ciagu godziny, nie rozkaz). Postoj: SetMoveModeHold raz (gdy AI czynne, poza osada, bez ucieczki,
    ///     bez zaczepki z INICJATYWY; poscig z mysli - EngageParty / GoAroundParty jako rozkaz domyslny - przerywamy Holdem).
    ///     Migawka dla watkow AI: slownik podmieniany w calosci (volatile, wzor NightMarch._aiPenalty), nigdy nie zmieniany po publikacji.
    /// (B) POSTOJ - postfiks High (600) CampaignEventDispatcher.AiHourlyTick: Stand = DoNotChangeBehavior (408, ArmsFirst, pogrzeb, LordEscort, RescueHunt,
    ///     PrisonerTerms... wychodza same; gra nie zmienia rozkazu). Inicjatywa (ucieczka) dziala dalej. Rozkazu domyslnego szukajacego bitwy (poscig, oblezenie,
    ///     rabunek - gdy Hold jeszcze nie wszedl) NIE zamrazamy: mysl dziala, a postfiks 55 daje w postoju opcjom bitwy -1.
    /// (C) TWIERDZA - regula 408 (HeroWounds.Ai.cs) bierze tez partie w stanie Fort (WantsFort): opcje bitwy -1, wyjazd do twierdzy rodu / frakcji.
    /// (D) OSTROZNOSC W MYSLI - postfiks 55 (po 408 = 60, przed wetami 50/48/45 i Last): opcje bitwy (te same co 408 - HeroWounds.AiIsBattle) o ocenie &gt; 0
    ///     x (1 - K c); GoToSettlement do nieoblezonej twierdzy swojej frakcji x (1 + 0.5 c) (524: ZAMEK bez podbicia przy glodzie / malym zapasie). Ocen &lt;= 0 nie ruszamy (cudze blokady), obrona osady rodu zostaje.
    ///     Stand: opcje zwolania armii -1 (ROT AIThinkPatch / gra tworzy armie przed sprawdzeniem DoNotChangeBehavior - galaz WillGatherArmy); bez flagi
    ///     (rozkaz szukajacy bitwy - B) takze opcje bitwy -1.
    /// (E) OSTROZNOSC W INICJATYWIE - postfiks DefaultMobilePartyAIModel.CalculateInitiativeScoresForEnemy (prywatna, watki AI co 0.25 h). Gra mnozy atak
    ///     z bliska x100 (num5, wrog blizej niz NeededMaximumLandDistance x 5), wiec samo ciecie skali nic nie daje: atak tylko z wyrazna przewaga sil
    ///     (localAdvantage &gt;= 1 + AdvPerCaution K c), inaczej min(atak, 0.99) x (1 - K c) (ponizej progu 1 = bez zaczepki); z przewaga atak x (1 - K c).
    ///     Unikanie x (1 + c) (gra liczy unikanie tylko wobec silniejszych i tylko gdy atak &lt; 1). Stand i Fort - atak 0. Wyjatki p9: obrona osady rodu
    ///     (ArmsFirst.DefendsOwn), obronca / chroniony 335 (LordEscort.GuardAllowsAttack).
    /// (F) ARMIE - bramki ArmsFirst (CanLordCreateArmy gry i BK, filtr kandydatow, wezwania BK, okno armii gracza) pytaja ArmsFirst.ArmyOut =
    ///     Unready || AiWounded500b.ArmyOut: c &gt;= ArmyGate% albo Stand / Fort. Czlonek juz w armii zostaje.
    /// (G) DECYZJE JEFFA 14.11 (E1-E4): E1 obrona zagrozonej osady RODU z postoju / drogi do twierdzy tylko z szansa wygranej - DefendOk: zdrowi partii
    ///     (Party.EstimatedStrength, ta sama miara co Unsafe) + partie polowe strony osady w bitwie (bez garnizonu i milicji - jak localAdvantage gry) &gt;=
    ///     AiWoundedDefendEdgePercent (120) x napastnicy (druga strona bitwy albo obozu oblegajacych), i sens: osada do AiWoundedDefendMaxHours (12) h jazdy. W postoju: przy szansie mysl wolna (bez DoNotChangeBehavior),
    ///     a postfiks 55 zostawia tylko obrone z szansa (reszta -1); HoldOnce nie przerywa jazdy na obrone z szansa. Bez szansy obrona -1 (55 i 408). Inicjatywa
    ///     (postoj / Fort / wodz na lozu w drodze 408 - Snap.Road): atak na napastnika osady rodu tylko przy localAdvantage gry &gt;= ten sam prog. E2 twierdza sojusznika (HeroWounds.AiFort).
    ///     E3 wlasne progi HP AI 20/30. E4 wodz ARMII: tylko ostroznosc w inicjatywie (migawka C, stan None) - bez rozwiazywania armii, bez mysli, bez bramek.
    /// Bez namiotow i wizerunkow; nic co klatke; bledy liczone na miejsce i partie (5 potkniec partii = poza migawka do konca doby), bez globalnego wylacznika.
    /// </summary>
    internal static class AiWounded500b
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const string Tag = "AI ranni (500-b): ";
        private const byte None = 0, Stand = 1, Fort = 2;
        private const float FortCalm = 0.25f;      // ponizej tej ostroznosci koniec jazdy do twierdzy
        private const double FortMaxDays = 10.0;   // zapas: najdluzej tyle dob w stanie Fort (brak twierdzy z droga - 408 liczy _aiNoFort)
        private const float SafeFoodDays = 2f;     // postoj tylko z jedzeniem na tyle dob
        private const int MinMen = 10;             // udzial rannych liczony od tylu szeregowych
        private const int ReHoldMax = 2;           // tyle razy cudzy rozkaz przestawil partie w postoju - ustepujemy (Fort)
        private const int PartyErrMax = 5;         // potkniecia partii do konca doby
        private const double CoolHours = 24.0;     // po wyjsciu bez wyleczenia (brak twierdzy, limit Fort) - tyle godzin bez ponownego wejscia
        private const float AdvPerCaution = 2f;    // inicjatywa: atak tylko przy przewadze sil >= 1 + 2 K c (pelna ostroznosc, K 75%: 2.5 x sila wroga)
        private const double FoodRecheckHours = 6.0;   // 524: glodna partia z zamkiem za cel - ponowne liczenie celu najwczesniej po tylu godzinach (tylko koszt; najedzona wchodzi od razu)

        internal struct Snap { internal float C; internal byte St; internal bool Road; }   // Road: wodz na lozu, ktorego regula 408 prowadzi do twierdzy (E1 w inicjatywie)

        private sealed class Row
        {
            internal Hero Lead; internal byte St; internal double SinceH; internal bool Held; internal int ReHolds; internal int Err; internal int ErrDay = -1;
        }

        private static readonly Dictionary<MobileParty, Row> _rows = new Dictionary<MobileParty, Row>();
        private static readonly Dictionary<MobileParty, double> _cool = new Dictionary<MobileParty, double>();   // partia -> godzina, do ktorej bez wejscia
        private static readonly Dictionary<MobileParty, double> _foodWait = new Dictionary<MobileParty, double>();   // 524: partia -> godzina ponownego liczenia celu (glod / zamek bez zapasu)
        private static readonly Dictionary<MobileParty, Snap> Empty = new Dictionary<MobileParty, Snap>();
        private static volatile Dictionary<MobileParty, Snap> _snap = Empty;
        private static volatile bool _live;                 // klucz odpoczynku albo ostroznosci wlaczony (odczyt w bramkach armii)
        private static volatile bool _anyFort;              // jest partia w stanie Fort (szybkie wyjscie 408)
        private static readonly List<MobileParty> _drop = new List<MobileParty>(16);
        private static readonly List<(AIBehaviorData, float)> _copy = new List<(AIBehaviorData, float)>(32);
        private static TextObject _armyText;

        // latki (raz na proces)
        private static Harmony _h;
        private static string _hStand = "nie wpieta", _hCaution = "nie wpieta", _hInit = "nie wpieta";

        // linia doby (watek glowny; inicjatywa i bramki - Interlocked)
        private static int _day = -1;
        private static int _dStand, _dStandFort, _dFortDirect, _dFortFromStand, _dStandEnd, _dFortEnd, _dHold, _dReHold, _dYield, _dThreat, _dHunger, _dMaxDays, _dFortMax, _dUnsafe;
        private static int _dThinkStand, _dArmyOff, _dCut, _dBoost, _dThinkC, _dSkipStory;
        private static int _dNoFort, _dCool, _dThinkFree, _dStandBattleOff;
        private static int _dDefThink, _dDefOff, _dDefOnly, _dDefRide, _dDefFar;   // E1: mysli wolne dla obrony z szansa, obrony bez szansy -1, inne opcje -1 przy obronie, godziny jazdy na obrone
        private static int _dFoodEnd, _dFoodStand, _dFoodNoEnter, _dBoostFood, _dFortBed;   // 524: koniec twierdzy z glodu, postoj bez wyjazdu na zamek (glod), wejscia wstrzymane (glod / zamek bez zapasu), zamki bez podbicia (malo jedzenia), godziny Fort ponad limit z lozem HW
        private static int _iDefOff, _iArmyLead;                         // E1 inicjatywa: atak na napastnika osady rodu bez szansy zgaszony; E4: inicjatywa wodza armii przycieta
        private static int _cArmy;                                        // E4: ostatni przeglad - wodzowie armii z c > 0
        private static int _iCut, _iCap, _iZero, _iZeroFort, _iAvoid, _iKeep, _gArmy;
        private static int _cMax, _cHalf, _cSome;   // ostatni przeglad: partie z c > 0, c >= 0.5, liczba w stanie
        private static long _scanTicks, _scanMaxTicks; private static int _scans;
        private static int _sScan, _sThink, _sCaution, _sInit, _sArmy, _sParty;   // potkniecia na miejsce

        internal static TextObject ArmyText { get { return _armyText ?? (_armyText = new TextObject("{=!}Its men are badly wounded - it must recover before joining an army.")); } }

        internal static void Reset()
        {
            _rows.Clear(); _cool.Clear(); _foodWait.Clear(); _snap = Empty; _anyFort = false; _drop.Clear(); _copy.Clear(); _day = -1;
            ClearDay();
            _sScan = _sThink = _sCaution = _sInit = _sArmy = _sParty = 0;
        }

        private static void ClearDay()
        {
            _dStand = _dStandFort = _dFortDirect = _dFortFromStand = _dStandEnd = _dFortEnd = _dHold = _dReHold = _dYield = _dThreat = _dHunger = _dMaxDays = _dFortMax = _dUnsafe = 0;
            _dThinkStand = _dArmyOff = _dCut = _dBoost = _dThinkC = _dSkipStory = 0;
            _dNoFort = _dCool = _dThinkFree = _dStandBattleOff = 0;
            _dDefThink = _dDefOff = _dDefOnly = _dDefRide = _dDefFar = 0;
            _dFoodEnd = _dFoodStand = _dFoodNoEnter = _dBoostFood = _dFortBed = 0;
            Interlocked.Exchange(ref _iDefOff, 0); Interlocked.Exchange(ref _iArmyLead, 0);
            Interlocked.Exchange(ref _iCap, 0); Interlocked.Exchange(ref _iZeroFort, 0);
            Interlocked.Exchange(ref _iCut, 0); Interlocked.Exchange(ref _iZero, 0); Interlocked.Exchange(ref _iAvoid, 0); Interlocked.Exchange(ref _iKeep, 0);
            Interlocked.Exchange(ref _gArmy, 0);
            _scanTicks = 0; _scanMaxTicks = 0; _scans = 0;
        }

        private static void Stumble(ref int counter, string where, Exception e)
        {
            int n = Interlocked.Increment(ref counter);
            try { if (n <= 3 || n % 100 == 0) Log.Error("AiWounded500b." + where + " (potkniecie " + n + ")", e); } catch { }
        }

        private static float Clamp01(float v) { return v < 0f ? 0f : v > 1f ? 1f : v; }
        private static float K(Settings s) { return Math.Max(0, Math.Min(100, s.AiWoundedCautionPercent)) / 100f; }

        // ------------------------------------------------------------ odczyty dla innych regul (dowolny watek - tylko migawka)
        /// <summary>Partia w stanie Fort - regula 408 prowadzi ja do twierdzy (HeroWounds.AiRestPostfix).</summary>
        internal static bool AnyFort { get { return _anyFort; } }

        internal static bool WantsFort(MobileParty mp)
        {
            if (mp == null || !_anyFort) return false;
            Snap x; return _snap.TryGetValue(mp, out x) && x.St == Fort;
        }

        /// <summary>Partia odpoczywa (postoj albo jazda do twierdzy) - dla HeroWounds.RestingParty (LordEscort 223, IronSiege 334e: bez dolaczania do armii,
        /// jazdy za silniejszym, zwolywania).</summary>
        internal static bool Resting(MobileParty mp)
        {
            if (mp == null || mp.Army != null) return false;
            var snap = _snap;
            if (snap.Count == 0) return false;
            Snap x; return snap.TryGetValue(mp, out x) && x.St != None;
        }

        /// <summary>(F) Bramki armii (ArmsFirst.ArmyOut): poturbowana partia nie tworzy armii, nie jest kandydatem i nie idzie na wezwanie. Dowolny watek.</summary>
        internal static bool ArmyOut(MobileParty mp)
        {
            if (!_live || mp == null || mp.IsMainParty || mp.Army != null || mp.MapEvent != null || mp.BesiegedSettlement != null) return false;
            var snap = _snap;
            if (snap.Count == 0) return false;
            try
            {
                Snap x;
                if (!snap.TryGetValue(mp, out x)) return false;
                var s = Settings.Current;
                if (s == null) return false;
                bool out1 = (x.St != None && s.AiWoundedRestEnabled)
                            || (s.AiWoundedCaution && x.C >= Math.Max(1, Math.Min(100, s.AiWoundedArmyGatePercent)) / 100f);
                if (out1) Interlocked.Increment(ref _gArmy);
                return out1;
            }
            catch (Exception e) { Stumble(ref _sArmy, "ArmyOut", e); return false; }
        }

        // ------------------------------------------------------------ (A) przeglad godzinowy
        /// <summary>Tik godzinowy (ArmouryBehavior): stan partii, migawka, raz na dobe linia logu. Nie rzuca.</summary>
        internal static void Hourly()
        {
            var s = Settings.Current;
            if (s == null) return;
            int day = (int)CampaignTime.Now.ToDays;
            if (_day < 0) _day = day;
            else if (day != _day) { DayLine(s); ClearDay(); _day = day; PruneCool(); }
            bool rest = s.AiWoundedRestEnabled, caution = s.AiWoundedCaution;
            _live = rest || caution;
            if (!_live)
            {
                if (_rows.Count > 0 || _snap.Count > 0 || _cool.Count > 0 || _foodWait.Count > 0) { _rows.Clear(); _cool.Clear(); _foodWait.Clear(); _snap = Empty; _anyFort = false; }
                return;
            }
            long t0 = Stopwatch.GetTimestamp();
            Dictionary<MobileParty, Snap> neu = null;
            bool anyFort = false;
            int cSome = 0, cHalf = 0, cState = 0, cArmy = 0;
            try
            {
                double now = CampaignTime.Now.ToHours;
                foreach (var mp in MobileParty.AllLordParties)
                {
                    if (mp == null) continue;
                    Row row = null;
                    try
                    {
                        _rows.TryGetValue(mp, out row);
                        if (row != null && row.ErrDay == day && row.Err >= PartyErrMax) continue;
                        float c;
                        byte st = Step(mp, ref row, s, rest, now, out c);
                        if (row != null && row.St != None) cState++;
                        // recenzja E1: wodz na lozu HW w drodze do twierdzy (regula 408, stan None) - ta sama bramka szansy w inicjatywie co postoj / Fort
                        bool road = st == None && rest && mp.Army == null && HeroWounds.RestingParty(mp);
                        if (c <= 0f && st == None && !road) continue;
                        if (c > 0f) { cSome++; if (c >= 0.5f) cHalf++; if (mp.Army != null) cArmy++; }
                        if (st == Fort) anyFort = true;
                        if (neu == null) neu = new Dictionary<MobileParty, Snap>();
                        neu[mp] = new Snap { C = caution ? c : 0f, St = st, Road = road };
                    }
                    catch (Exception e)
                    {
                        Stumble(ref _sParty, "Hourly.party", e);
                        if (row != null) { if (row.ErrDay != day) { row.ErrDay = day; row.Err = 0; } row.Err++; }
                    }
                }
                // partie, ktore zniknely albo odpadly z przegladu
                if (_rows.Count > 0)
                {
                    _drop.Clear();
                    foreach (var kv in _rows) if (kv.Key == null || !kv.Key.IsActive || kv.Value.St == None) _drop.Add(kv.Key);
                    foreach (var p in _drop) _rows.Remove(p);
                    _drop.Clear();
                }
            }
            catch (Exception e) { Stumble(ref _sScan, "Hourly", e); }
            _snap = neu ?? Empty;
            _anyFort = anyFort;
            _cSome = cSome; _cHalf = cHalf; _cMax = cState; _cArmy = cArmy;
            long dt = Stopwatch.GetTimestamp() - t0;
            _scanTicks += dt; _scans++; if (dt > _scanMaxTicks) _scanMaxTicks = dt;
        }

        private static void PruneCool()
        {
            double now = CampaignTime.Now.ToHours;
            if (_cool.Count > 0)
            {
                _drop.Clear();
                foreach (var kv in _cool) if (kv.Key == null || !kv.Key.IsActive || kv.Value <= now) _drop.Add(kv.Key);
                foreach (var p in _drop) _cool.Remove(p);
                _drop.Clear();
            }
            if (_foodWait.Count > 0)
            {
                _drop.Clear();
                foreach (var kv in _foodWait) if (kv.Key == null || !kv.Key.IsActive || kv.Value <= now) _drop.Add(kv.Key);
                foreach (var p in _drop) _foodWait.Remove(p);
                _drop.Clear();
            }
        }

        /// <summary>524: partia glodna albo z zapasem ponizej progu zamku (HeroWounds.CastleFoodDays, gra: 12 dni). Watek glowny i mysl.</summary>
        private static bool LowFood(MobileParty mp, out bool starving)
        {
            starving = mp.Party != null && mp.Party.IsStarving;
            return starving || HeroWounds.CastleFoodLow(mp);
        }

        /// <summary>524: cel bez jedzenia - glod (wszedzie) albo ZAMEK przy zapasie ponizej progu (w zamku nie kupi: gra i BK kupuja tylko w miescie / wsi).</summary>
        private static bool NoFoodAt(Settlement fort, bool low, bool starving)
        {
            return starving || (low && fort != null && !fort.IsTown);
        }

        /// <summary>524: wyjscie / brak wejscia z powodu jedzenia - stan None BEZ blokady CoolHours (partia jedzie po zapasy droga gry; najedzona moze wejsc
        /// od razu); tylko ponowne liczenie celu najwczesniej po FoodRecheckHours, dopoki brakuje jedzenia.</summary>
        private static byte FoodOut(MobileParty mp, ref Row row, double now)
        {
            if (row != null) { _rows.Remove(mp); row = null; }
            _foodWait[mp] = now + FoodRecheckHours;
            try { mp.Ai.RethinkAtNextHourlyTick = true; } catch { }
            return None;
        }

        /// <summary>Wyjscie bez wyleczenia - stan None i CoolHours bez ponownego wejscia (bez petli wejsc co godzine / co FortMaxDays).</summary>
        private static byte Cool(MobileParty mp, ref Row row, double now)
        {
            _rows.Remove(mp); row = null;
            _cool[mp] = now + CoolHours;
            try { mp.Ai.RethinkAtNextHourlyTick = true; } catch { }
            return None;
        }

        /// <summary>Krok jednej partii: ostroznosc c i nowy stan. Zwraca stan (None, Stand, Fort); row tworzony tylko przy wejsciu w stan.</summary>
        private static byte Step(MobileParty mp, ref Row row, Settings s, bool rest, double now, out float c)
        {
            c = 0f;
            if (!mp.IsActive || mp.IsMainParty || mp.IsDisbanding) return Clear(mp, row);
            var lh = mp.LeaderHero;
            var clan = mp.ActualClan;
            if (lh == null || lh == Hero.MainHero || clan == null || clan == Clan.PlayerClan) return Clear(mp, row);
            if (row != null && row.Lead != lh) { _rows.Remove(mp); row = null; mp.Ai.RethinkAtNextHourlyTick = true; }   // nowy wodz - stan od zera
            if (mp.Army != null)
            {
                // armia - decyduje wodz armii (stan None). E4 (Jeff "tylko ostroznosc"): wodz armii dostaje ostroznosc c tylko dla inicjatywy (InitPostfix);
                // bez postoju, bez mysli (CautionPostfix wychodzi przy armii), bez bramek armii i bez rozwiazywania. Lordowie fabuly - te same wyjatki
                Clear(mp, row); row = null;
                if (mp.Army.LeaderParty != mp || !s.AiWoundedCaution) return None;
                if (SiegePhantomLift.StoryLeaderOf(mp) != null || StoryNeed(lh)) return None;
                c = Caution(mp, lh, s);
                return None;
            }
            byte keep = row != null ? row.St : None;
            // bitwa, oblezenie, morze, przejscie - stan bez zmian (ani wejscia, ani wyjscia)
            if (mp.MapEvent != null || mp.BesiegedSettlement != null || mp.SiegeEvent != null || mp.IsCurrentlyAtSea || mp.IsTransitionInProgress
                || (mp.CurrentSettlement != null && mp.CurrentSettlement.IsUnderSiege))
            { c = Caution(mp, lh, s); return keep; }

            c = Caution(mp, lh, s);
            int grade = HeroWounds.GradeOf(lh);
            if (c > 0f || keep != None)
            {
                // lordowie fabuly (te same wyjatki co 408)
                if (SiegePhantomLift.StoryLeaderOf(mp) != null || StoryNeed(lh)) { _dSkipStory++; c = 0f; return Clear(mp, row); }
            }
            if (!rest) { c = c > 0f ? c : 0f; return Clear(mp, row); }

            int hp = lh.HitPoints;
            int on = Math.Max(1, s.AiWoundedRestHp), off = Math.Max(on, s.AiWoundedRestReleaseHp);   // E3: wlasne progi AI (20 / 30), nie nosze gracza
            float w = Share(mp);
            float restShare = Math.Max(1, Math.Min(100, s.AiWoundedRestSharePercent)) / 100f;
            float exitShare = Math.Max(0, Math.Min(100, s.AiWoundedRestExitSharePercent)) / 100f;
            if (exitShare > restShare) exitShare = restShare;

            if (keep == None)
            {
                bool hurt = hp <= on || grade == HeroWounds.Grave;
                bool battered = w >= 0f && w >= restShare;
                if (!hurt && !battered) return None;
                double until;
                if (_cool.TryGetValue(mp, out until))
                {
                    if (now < until) { _dCool++; return None; }
                    _cool.Remove(mp);
                }
                // 524 (log 14.11: Jon Snow w castle_N5 40% -> 95% rannych; glod w zamku odejmuje rannych, wiec c = 1 i znow Fort): glodna partia nie wchodzi
                // wcale (najpierw jedzenie - droga gry, jak 408 _aiStarve), a z zapasem ponizej progu zamku nie wchodzi, gdy celem jest ZAMEK (miasto - tak, tam kupi)
                bool starving;
                bool low = LowFood(mp, out starving);
                if (low)
                {
                    double fw;
                    if (_foodWait.TryGetValue(mp, out fw) && now < fw) { _dFoodNoEnter++; return None; }
                    if (starving) { _dFoodNoEnter++; return FoodOut(mp, ref row, now); }
                }
                else _foodWait.Remove(mp);
                string why = hurt ? (grade == HeroWounds.Grave ? "wodz na lozu Grave" : "HP wodza " + hp) : "ranni " + Pct(w);
                // skrot: w twierdzy swojej frakcji albo twierdza blisko - ta sama, ktora wybierze 408 (recenzja: wczesniej najblizsza frakcji, a 408 jechal do rodu)
                float hrs;
                var at = mp.CurrentSettlement;
                if (at != null && HeroWounds.FriendlyFort(at, mp.MapFaction))   // E2: takze twierdza sojusznika
                {
                    if (NoFoodAt(at, low, starving)) { _dFoodNoEnter++; return FoodOut(mp, ref row, now); }
                    row = new Row { Lead = lh, SinceH = now };
                    _rows[mp] = row;
                    Enter(mp, row, Fort, now); _dFortDirect++; Say(s, mp, lh, w, "-> TWIERDZA (stoi w " + at.StringId + ", " + why + ")"); return Fort;
                }
                var near = NearFort(mp, out hrs);
                // 524: zamek bez zapasu jedzenia - ani twierdza, ani postoj (postoj to wstep do zamku); partia najpierw jedzie po zapasy (zwykle decyzje)
                if (near != null && NoFoodAt(near, low, starving)) { _dFoodNoEnter++; return FoodOut(mp, ref row, now); }
                row = new Row { Lead = lh, SinceH = now };
                _rows[mp] = row;
                int skip = Math.Max(0, s.AiWoundedRestSkipHours);
                if (near != null && skip > 0 && hrs <= skip)
                { Enter(mp, row, Fort, now); _dFortDirect++; Say(s, mp, lh, w, "-> TWIERDZA (" + FortName(near, hrs) + " <= " + skip + " h, " + why + ")"); return Fort; }
                string unsafeWhy = Unsafe(mp, s);
                if (unsafeWhy != null)
                {
                    if (near == null)
                    {
                        _dUnsafe++; _dNoFort++;
                        Say(s, mp, lh, w, "-> BEZ POSTOJU (postoj niebezpieczny: " + unsafeWhy + ", brak twierdzy z droga; zwykle decyzje z ostroznoscia, " + CoolHours.ToString("F0", Inv) + " h bez wejscia, " + why + ")");
                        return Cool(mp, ref row, now);
                    }
                    Enter(mp, row, Fort, now); _dFortDirect++; _dUnsafe++;
                    Say(s, mp, lh, w, "-> TWIERDZA (" + FortName(near, hrs) + "; postoj niebezpieczny: " + unsafeWhy + ", " + why + ")"); return Fort;
                }
                Enter(mp, row, Stand, now); _dStand++;
                HoldOnce(mp, row, s);
                Say(s, mp, lh, w, "-> POSTOJ (" + why + (near != null ? ", potem twierdza " + FortName(near, hrs) : ", brak twierdzy z droga") + ")");
                return Stand;
            }

            if (keep == Stand)
            {
                double days = (now - row.SinceH) / 24.0;
                bool fit = hp > off && grade != HeroWounds.Grave && (w < 0f || w <= exitShare);
                string why = null;
                if (fit) why = "zdolna do drogi";
                else if (days >= Math.Max(1, s.AiWoundedRestMaxDays)) { why = "limit " + Math.Max(1, s.AiWoundedRestMaxDays) + " dob postoju"; _dMaxDays++; }
                else if (row.ReHolds >= ReHoldMax) { why = "cudzy rozkaz " + row.ReHolds + " razy"; _dYield++; }
                else
                {
                    string u = Unsafe(mp, s);
                    if (u != null) { why = u; if (u.StartsWith("glod", StringComparison.Ordinal) || u.StartsWith("jedzenie", StringComparison.Ordinal)) _dHunger++; else _dThreat++; }
                }
                if (why == null) { HoldOnce(mp, row, s); return Stand; }
                _dStandEnd++;
                if (c < FortCalm)
                {
                    _rows.Remove(mp); mp.Ai.RethinkAtNextHourlyTick = true;
                    Say(s, mp, lh, w, "POSTOJ -> KONIEC (" + why + ", po " + days.ToString("F1", Inv) + " dob)");
                    row = null; return None;
                }
                float fh;
                var fort = NearFort(mp, out fh);
                if (fort == null)
                {
                    _dNoFort++;
                    Say(s, mp, lh, w, "POSTOJ -> KONIEC (" + why + ", po " + days.ToString("F1", Inv) + " dob; brak twierdzy z droga - zwykle decyzje z ostroznoscia, " + CoolHours.ToString("F0", Inv) + " h bez wejscia)");
                    return Cool(mp, ref row, now);
                }
                bool starvingS;
                bool lowS = LowFood(mp, out starvingS);
                if (NoFoodAt(fort, lowS, starvingS))
                {
                    // 524: glod albo zamek bez zapasu - bez wyjazdu do twierdzy; None bez blokady 24 h (najpierw zapasy - droga gry)
                    _dFoodStand++;
                    Say(s, mp, lh, w, "POSTOJ -> KONIEC (" + why + ", po " + days.ToString("F1", Inv) + " dob; " + (starvingS ? "glod" : "zamek " + fort.StringId + " bez zapasu jedzenia")
                        + " - najpierw zapasy, " + FoodRecheckHours.ToString("F0", Inv) + " h do ponownego liczenia celu)");
                    return FoodOut(mp, ref row, now);
                }
                Enter(mp, row, Fort, now); _dFortFromStand++;
                Say(s, mp, lh, w, "POSTOJ -> TWIERDZA (" + FortName(fort, fh) + "; " + why + ", po " + days.ToString("F1", Inv) + " dob)");
                return Fort;
            }

            // Fort
            double fd = (now - row.SinceH) / 24.0;
            // 524 (1): glod albo zapas ponizej progu zamku, gdy twierdza spoczynku (ta, w ktorej stoi, albo cel 408) jest ZAMKIEM - w zamku nie kupi, a gra
            // przy glodzie odejmuje rannych (DefaultPartyHealingModel) - petla glod -> ranni -> c = 1 -> Fort. Koniec bez blokady 24 h (najpierw zapasy).
            bool starvingF;
            bool lowF = LowFood(mp, out starvingF);
            if (lowF)
            {
                Settlement ff = null;
                var atF = mp.CurrentSettlement;
                // recenzja 524: stoi w MIESCIE - kupuje co godzine (PartiesBuyFood HourlyTickParty / BK), maly zapas nie konczy postoju (tylko glod);
                // stoi w zamku - ten zamek; poza osada - cel 408 (NearFort)
                bool inTown = atF != null && atF.IsTown;
                if (!starvingF && !inTown) { if (atF != null && atF.IsFortification) ff = atF; else { float fh2; ff = NearFort(mp, out fh2); } }   // tylko przy malym zapasie; glod - koniec wszedzie
                if (NoFoodAt(ff, lowF, starvingF))
                {
                    _dFoodEnd++; _dFortEnd++;
                    Say(s, mp, lh, w, "TWIERDZA -> KONIEC (" + (starvingF ? "glod" : "zamek " + ff.StringId + ", jedzenie na " + mp.GetNumDaysForFoodToLast() + " dni < "
                        + HeroWounds.CastleFoodDays().ToString("F0", Inv)) + " - najpierw zapasy, bez blokady " + CoolHours.ToString("F0", Inv) + " h, po " + fd.ToString("F1", Inv) + " dob)");
                    return FoodOut(mp, ref row, now);
                }
            }
            // 524 (4): wodz na lozu HeroWounds - limit dob nie konczy twierdzy przed koncem loza. Wybor: trzymac, nie wracac w kolko - loze i tak prowadzi
            // regula 408 do twierdzy (wyjscie po 10 dobach dawalo 24 h zwyklych decyzji bez stanu, potem ponowne wejscie); loze jest skonczone (w twierdzy
            // schodzi 2 x szybciej), a glod konczy stan wyzej
            bool bedHold = fd >= FortMaxDays && grade > 0 && c >= FortCalm;
            if (bedHold) _dFortBed++;
            if (c < FortCalm || (fd >= FortMaxDays && !bedHold))
            {
                bool calm = c < FortCalm;
                if (!calm) _dFortMax++;
                _dFortEnd++;
                Say(s, mp, lh, w, "TWIERDZA -> KONIEC (" + (calm ? "ostroznosc " + c.ToString("F2", Inv) : "limit " + FortMaxDays.ToString("F0", Inv) + " dob, " + CoolHours.ToString("F0", Inv) + " h bez wejscia") + ", po " + fd.ToString("F1", Inv) + " dob)");
                if (!calm) return Cool(mp, ref row, now);
                _rows.Remove(mp); mp.Ai.RethinkAtNextHourlyTick = true;
                row = null; return None;
            }
            return Fort;
        }

        private static byte Clear(MobileParty mp, Row row)
        {
            if (row != null) { _rows.Remove(mp); try { mp.Ai.RethinkAtNextHourlyTick = true; } catch { } }
            return None;
        }

        private static void Enter(MobileParty mp, Row row, byte st, double now)
        {
            row.St = st; row.SinceH = now; row.Held = false; row.ReHolds = 0;
            mp.Ai.RethinkAtNextHourlyTick = true;   // mysl w ciagu godziny (nie rozkaz)
        }

        /// <summary>Postoj: Hold raz po wejsciu (poza osada, AI czynne, bez ucieczki / zaczepki z inicjatywy). Partia przestawiona potem cudzym rozkazem
        /// (np. NightRest oddaje o swicie rozkaz sprzed snu) - Hold jeszcze raz, najwyzej ReHoldMax razy, potem Fort (bez migotania).</summary>
        private static void HoldOnce(MobileParty mp, Row row, Settings s)
        {
            if (mp.CurrentSettlement != null) return;   // w osadzie stoi sama (DoNotChangeBehavior)
            if (mp.DefaultBehavior == AiBehavior.Hold) { row.Held = true; return; }
            var ai = mp.Ai;
            if (ai == null || ai.IsDisabled || ai.DoNotMakeNewDecisions) return;   // noc NightRest / cudza blokada - po przebudzeniu
            var stb = mp.ShortTermBehavior;
            var db = mp.DefaultBehavior;
            // ucieczka / zaczepka z INICJATYWY - nie przerywamy; poscig z mysli (rozkaz domyslny EngageParty / GoAroundParty, short-term EngageParty blisko
            // celu - MobilePartyAi.GetGoAroundPartyBehavior) przerywamy Holdem (recenzja: inaczej DoNotChangeBehavior zamrazal poscig na caly postoj)
            if (MobileParty.IsFleeBehavior(stb) || (stb == AiBehavior.EngageParty && db != AiBehavior.EngageParty && db != AiBehavior.GoAroundParty)) return;
            // E1: jazda na obrone zagrozonej osady rodu z szansa wygranej - nie przerywamy (i nie liczymy jako cudzy rozkaz); bez szansy - Hold jak zwykle
            if (db == AiBehavior.DefendSettlement)
            {
                var ts = mp.TargetSettlement;
                if (ts != null && ts.OwnerClan == mp.ActualClan && DefendOk(mp, ts, s)) { row.Held = false; _dDefRide++; return; }
            }
            // odwrot po bitwie (gra: AiMilitaryBehavior.OnMapEventEnded - SetMoveGoToPoint od zwyciezcy; 458 odwrot wodza; CrossingLaw zawrocenie) - najpierw dojechac
            if (db == AiBehavior.GoToPoint && mp.IsMoving) return;
            if (row.Held) { row.ReHolds++; _dReHold++; if (row.ReHolds >= ReHoldMax) return; }   // przy nastepnym kroku: Fort
            mp.SetMoveModeHold();
            row.Held = true; _dHold++;
        }

        /// <summary>Ostroznosc c: max(cH, cW); loze HeroWounds = 1.</summary>
        private static float Caution(MobileParty mp, Hero lh, Settings s)
        {
            if (HeroWounds.GradeOf(lh) > 0) return 1f;
            float cH = 0f;
            int max = Math.Max(1, lh.MaxHitPoints);
            float hpP = lh.HitPoints * 100f / max;
            float lim = lh.WoundedHealthLimit * 100f / max;
            float start = Math.Max(1, Math.Min(100, s.AiWoundedCautionHpStartPercent));
            if (start > lim + 0.5f) cH = Clamp01((start - hpP) / (start - lim));
            else cH = hpP <= lim ? 1f : 0f;
            float cW = 0f;
            float w = Share(mp);
            if (w > 0f)
            {
                float a = Math.Max(0, Math.Min(100, s.AiWoundedCautionShareStartPercent)) / 100f;
                float b = Math.Max(1, Math.Min(100, s.AiWoundedRestSharePercent)) / 100f;
                cW = b > a + 0.005f ? Clamp01((w - a) / (b - a)) : (w >= b ? 1f : 0f);
            }
            return Math.Max(cH, cW);
        }

        /// <summary>Udzial rannych szeregowych (-1 = za malo ludzi).</summary>
        private static float Share(MobileParty mp)
        {
            var ro = mp.MemberRoster;
            if (ro == null) return -1f;
            int tot = ro.TotalRegulars;
            if (tot < MinMen) return -1f;
            return ro.TotalWoundedRegulars / (float)tot;
        }

        private static string Pct(float w) { return w < 0f ? "-" : (w * 100f).ToString("F0", Inv) + "%"; }

        private static bool StoryNeed(Hero lh)
        {
            if (!StoryGuard.Needed(lh)) return false;
            double need = StoryGuard.NextNeedDay(lh);
            return need >= 0 && need - CanonCompanions.StoryDay() < 14.0;   // ta sama granica co 408 (AiNeedDays)
        }

        /// <summary>Postoj niebezpieczny: powod albo null - glod / malo jedzenia, silniejszy wrog w promieniu alarmu obozu AiCampDangerRadius, ale co najmniej
        /// 3 jedn. (jak ArmyRestDay - min 6): 0 w MCM wylacza alarm obozu NightRest, nie sprawdzenie bezpieczenstwa postoju rannych; najblizsza osada wroga.</summary>
        private static string Unsafe(MobileParty mp, Settings s)
        {
            if (mp.Party.IsStarving) return "glod";
            if (mp.CurrentSettlement == null && mp.GetNumDaysForFoodToLast() < SafeFoodDays) return "jedzenie < " + SafeFoodDays.ToString("F0", Inv) + " dob";
            var f = mp.MapFaction;
            var pos = mp.GetPosition2D;
            float r = Math.Max(3f, s.AiCampDangerRadius);
            float own = mp.Party != null ? mp.Party.EstimatedStrength : 0f;
            var data = MobileParty.StartFindingLocatablesAroundPosition(pos, r);
            for (var t = MobileParty.FindNextLocatable(ref data); t != null; t = MobileParty.FindNextLocatable(ref data))
            {
                if (t == mp || !t.IsActive || t.AttachedTo != null || t.CurrentSettlement != null) continue;
                if (!t.IsLordParty && !t.IsBandit && !t.IsMainParty) continue;
                if (t.IsCurrentlyAtSea != mp.IsCurrentlyAtSea) continue;
                bool hostile = t.IsBandit ? !mp.IsBandit : (f != null && t.MapFaction != null && t.MapFaction != f && f.IsAtWarWith(t.MapFaction));
                if (!hostile) continue;
                if (pos.Distance(t.GetPosition2D) > r) continue;
                float ts = t.Army != null && t.Army.LeaderParty == t ? t.Army.EstimatedStrength : (t.Party != null ? t.Party.EstimatedStrength : 0f);
                if (ts > own) return "silniejszy wrog " + (t.StringId ?? "?") + " (" + ts.ToString("F0", Inv) + " vs " + own.ToString("F0", Inv) + ")";
            }
            if (mp.CurrentSettlement == null && f != null)
            {
                Settlement best = null; float bd = float.MaxValue;
                var sd = Settlement.StartFindingLocatablesAroundPosition(pos, 2f * r);
                for (var st = Settlement.FindNextLocatable(ref sd); st != null; st = Settlement.FindNextLocatable(ref sd))
                {
                    if (!(st.IsTown || st.IsCastle || st.IsVillage)) continue;
                    float d = st.GetPosition2D.Distance(pos);
                    if (d < bd) { bd = d; best = st; }
                }
                if (best != null && best.MapFaction != null && best.MapFaction != f && f.IsAtWarWith(best.MapFaction)) return "wroga ziemia (" + best.StringId + ")";
            }
            return null;
        }

        /// <summary>Twierdza spoczynku - ta sama, ktora wybierze 408 (HeroWounds.AiRestFort: najblizsza rodu, potem frakcji, po tej samej stronie Muru, z droga
        /// ladowa, do AiRoadChecks kandydatow) i godziny jazdy. Null = brak.</summary>
        private static Settlement NearFort(MobileParty mp, out float hours)
        {
            hours = float.MaxValue;
            byte tier; float road;
            var best = HeroWounds.AiRestFort(mp, out tier, out road);
            if (best == null) return null;
            if (road < 0f) road = mp.GetPosition2D.Distance(best.GetPosition2D) * 1.1f;
            hours = RideHours(mp, road);
            return best;
        }

        private static string FortName(Settlement st, float hrs) { return (st.StringId ?? "?") + " " + hrs.ToString("F1", Inv) + " h drogi"; }

        // ------------------------------------------------------------ (G) E1 - obrona osady rodu tylko z szansa
        private static float Edge(Settings s) { return Math.Max(100, Math.Min(300, s.AiWoundedDefendEdgePercent)) / 100f; }

        /// <summary>E1: stosunek sil przy obronie zagrozonej osady - (zdrowi partii + polowe partie po stronie osady) / napastnicy; EstimatedStrength jak Unsafe
        /// (liczy tylko zdrowych). Bitwa o osade (rabunek, szturm): strona osady vs druga strona; oblezenie bez bitwy: sama partia vs oboz oblegajacych.
        /// Recenzja: garnizonu, milicji, samej osady i partii zamknietych w oblezonej osadzie NIE liczymy - gra (DefaultMobilePartyAIModel, localAdvantage
        /// inicjatywy) tez ich pomija (IsGarrison / IsMilitia / CurrentSettlement.SiegeEvent), a to ona przy podejsciu decyduje atak albo ucieczke; z garnizonem
        /// mysl wysylalaby lorda na obrone, ktorej inicjatywa (i nasza bramka E1 w InitPostfix) nie stoczy. Bez zagrozenia - 0. Watek glowny.</summary>
        internal static float DefendOdds(MobileParty mp, Settlement st)
        {
            if (mp == null || st == null || mp.Party == null) return 0f;
            float ours = mp.Party.EstimatedStrength, def = 0f, att = 0f;
            var sp = st.Party;
            var me = sp != null ? sp.MapEvent : null;
            if (me != null)
            {
                var side = sp.MapEventSide;
                for (int k = 0; k < 2; k++)
                {
                    var ms = k == 0 ? me.AttackerSide : me.DefenderSide;
                    if (ms == null) continue;
                    var ps = ms.Parties;
                    for (int i = 0; i < ps.Count; i++)
                    {
                        var pb = ps[i] != null ? ps[i].Party : null;
                        if (pb == null || pb == mp.Party) continue;
                        if (ms != side) { att += pb.EstimatedStrength; continue; }
                        var m = pb.MobileParty;   // strona osady: tylko partie polowe (bez osady, garnizonu, milicji) - jak localAdvantage gry
                        if (m == null || m.IsGarrison || m.IsMilitia) continue;
                        def += pb.EstimatedStrength;
                    }
                }
            }
            else if (st.SiegeEvent != null && st.SiegeEvent.BesiegerCamp != null)
            {
                // obroncow za murami gra w inicjatywie nie liczy (CurrentSettlement.SiegeEvent != null) - tylko sama partia vs oboz
                foreach (var pb in st.SiegeEvent.BesiegerCamp.GetInvolvedPartiesForEventType()) if (pb != null && pb != mp.Party) att += pb.EstimatedStrength;
            }
            else return 0f;
            if (att <= 0f) return 99f;
            return (ours + def) / att;
        }

        /// <summary>E1 (Jeff: "tylko gdy ma to sens i jest szansa ze wygra"): obrona osady rodu - sens: osada w zasiegu AiWoundedDefendMaxHours jazdy
        /// (godziny jak NearFort: odleglosc x 1.1 / (predkosc / 1.2)), szansa: DefendOdds &gt;= AiWoundedDefendEdgePercent. Nie rzuca (blad = bez szansy, licznik).</summary>
        internal static bool DefendOk(MobileParty mp, Settlement st, Settings s)
        {
            if (s == null || st == null || mp == null) return false;
            try
            {
                if (RideHours(mp, mp.GetPosition2D.Distance(st.GetPosition2D) * 1.1f) > Math.Max(1, s.AiWoundedDefendMaxHours)) { _dDefFar++; return false; }
                return DefendOdds(mp, st) >= Edge(s);
            }
            catch (Exception e) { Stumble(ref _sThink, "DefendOk", e); return false; }
        }

        /// <summary>Godziny jazdy po drodze road (jedn. mapy) - ta sama rachuba co NearFort (jedn./h jak NightMarch.UnitsPerHour).</summary>
        private static float RideHours(MobileParty mp, float road)
        {
            float sp = 0f;
            try { sp = mp.Speed; } catch { }
            if (sp <= 0f) sp = 2.4f;
            return road / Math.Max(0.05f, sp / 1.2f);
        }

        /// <summary>E1: w ocenach jest obrona zagrozonej osady rodu (bez zwolania armii, ocena &gt; 0) z szansa wygranej.</summary>
        private static bool AnyDefendOk(MobileParty party, MBReadOnlyList<(AIBehaviorData, float)> scores, Settings s)
        {
            var clan = party.ActualClan;
            if (clan == null || scores == null) return false;
            for (int i = 0; i < scores.Count; i++)
            {
                if (!(scores[i].Item2 > 0f)) continue;
                var d = scores[i].Item1;
                if (d.WillGatherArmy || !ArmsFirst.IsOwnDefend(in d, clan)) continue;
                if (DefendOk(party, d.Party as Settlement, s)) return true;
            }
            return false;
        }

        private static void Say(Settings s, MobileParty mp, Hero lh, float w, string what)
        {
            if (!s.AiWoundedLog) return;
            try
            {
                Log.Info(Tag + "partia " + (mp.StringId ?? "?") + " (wodz " + (lh.Name != null ? lh.Name.ToString() : "?") + ", HP " + lh.HitPoints + "/" + lh.MaxHitPoints
                         + ", ranni " + (mp.MemberRoster != null ? mp.MemberRoster.TotalWoundedRegulars + "/" + mp.MemberRoster.TotalRegulars : "?") + " = " + Pct(w) + ") " + what + ".");
            }
            catch { }
        }

        // ------------------------------------------------------------ (B) postoj - postfiks High
        /// <summary>Postfiks High (600) CampaignEventDispatcher.AiHourlyTick: partia w postoju nie zmienia rozkazu (DoNotChangeBehavior). Pusta migawka = jedno porownanie.</summary>
        public static void StandPostfix(MobileParty party, PartyThinkParams partyThinkParams)
        {
            var snap = _snap;
            if (snap.Count == 0 || party == null || partyThinkParams == null) return;
            try
            {
                Snap x;
                if (!snap.TryGetValue(party, out x) || x.St != Stand) return;
                if (party.Army != null || party.MapEvent != null) return;
                var s = Settings.Current;
                if (s == null || !s.AiWoundedRestEnabled) return;
                if (Aggressive(party)) { _dThinkFree++; return; }   // poscig / oblezenie / rabunek bez Hold - mysl dziala, 55 daje bitwie -1
                // E1: obrona zagrozonej osady rodu z szansa wygranej - mysl wolna, 55 zostawia tylko te obrone
                if (AnyDefendOk(party, partyThinkParams.AIBehaviorScores, s)) { _dDefThink++; return; }
                partyThinkParams.DoNotChangeBehavior = true;
                _dThinkStand++;
            }
            catch (Exception e) { Stumble(ref _sThink, "StandPostfix", e); }
        }

        /// <summary>Rozkaz domyslny szukajacy bitwy (lista jak HeroWounds.AiIsBattle: obrona osady wlasnego rodu nie).</summary>
        private static bool Aggressive(MobileParty mp)
        {
            var b = mp.DefaultBehavior;
            if (b == AiBehavior.DefendSettlement) { var t = mp.TargetSettlement; return t != null && t.OwnerClan != mp.ActualClan; }
            return b == AiBehavior.EngageParty || b == AiBehavior.GoAroundParty || b == AiBehavior.BesiegeSettlement || b == AiBehavior.AssaultSettlement
                   || b == AiBehavior.RaidSettlement;
        }

        // ------------------------------------------------------------ (D) ostroznosc w mysli - postfiks 55
        /// <summary>Postfiks 55 CampaignEventDispatcher.AiHourlyTick (po 408, przed wetami): oceny bitwy x (1 - K c), wlasne twierdze x (1 + 0.5 c);
        /// postoj - opcje zwolania armii -1. Nie rzuca.</summary>
        public static void CautionPostfix(MobileParty party, PartyThinkParams partyThinkParams)
        {
            var snap = _snap;
            if (snap.Count == 0 || party == null || partyThinkParams == null) return;
            try
            {
                Snap x;
                if (!snap.TryGetValue(party, out x)) return;
                if (party.Army != null || party.MapEvent != null || party.BesiegedSettlement != null || party.SiegeEvent != null) return;
                var s = Settings.Current;
                if (s == null) return;
                var scores = partyThinkParams.AIBehaviorScores;
                if (scores == null || scores.Count == 0) return;
                _copy.Clear();
                for (int i = 0; i < scores.Count; i++) _copy.Add(scores[i]);
                if (x.St == Stand && s.AiWoundedRestEnabled)
                {
                    // gra tworzy armie mimo DoNotChangeBehavior (galaz WillGatherArmy przed sprawdzeniem flagi) - zamykamy u zrodla
                    // bez DoNotChangeBehavior (rozkaz szukajacy bitwy - StandPostfix go nie zamrozil) takze opcje bitwy -1 (ta sama lista co 408)
                    // E1: obrona zagrozonej osady rodu - z szansa zostaje i wtedy jest JEDYNA opcja (reszta -1), bez szansy -1
                    bool free = !partyThinkParams.DoNotChangeBehavior;
                    var sc = party.ActualClan;
                    bool defend = false;
                    if (free && sc != null)
                        for (int i = 0; i < _copy.Count && !defend; i++)
                        {
                            if (!(_copy[i].Item2 > 0f)) continue;
                            var d = _copy[i].Item1;
                            if (!d.WillGatherArmy && ArmsFirst.IsOwnDefend(in d, sc) && DefendOk(party, d.Party as Settlement, s)) defend = true;
                        }
                    for (int i = 0; i < _copy.Count; i++)
                    {
                        if (!(_copy[i].Item2 > -1f)) continue;
                        var d = _copy[i].Item1;
                        if (d.WillGatherArmy) { partyThinkParams.SetBehaviorScore(in d, -1f); _dArmyOff++; }
                        else if (!free) continue;
                        else if (sc != null && ArmsFirst.IsOwnDefend(in d, sc))
                        {
                            if (!DefendOk(party, d.Party as Settlement, s)) { partyThinkParams.SetBehaviorScore(in d, -1f); _dDefOff++; }
                        }
                        else if (defend) { partyThinkParams.SetBehaviorScore(in d, -1f); _dDefOnly++; }
                        else if (HeroWounds.AiIsBattle(in d, sc)) { partyThinkParams.SetBehaviorScore(in d, -1f); _dStandBattleOff++; }
                    }
                    _copy.Clear();
                    return;
                }
                if (!s.AiWoundedCaution || x.C <= 0f || partyThinkParams.DoNotChangeBehavior) { _copy.Clear(); return; }
                var clan = party.ActualClan;
                var f = party.MapFaction;
                float k = K(s) * x.C;
                bool any = false;
                int food = -1;   // 524: -1 nie liczone, 0 zapas w porzadku, 1 glod albo zapas ponizej progu zamku (liczone raz, przy pierwszym zamku)
                for (int i = 0; i < _copy.Count; i++)
                {
                    float v = _copy[i].Item2;
                    if (!(v > 0f)) continue;   // ocen <= 0 (cudze blokady, nasze -1) nie ruszamy
                    var d = _copy[i].Item1;
                    if (HeroWounds.AiIsBattle(in d, clan))
                    {
                        if (k <= 0f) continue;
                        partyThinkParams.SetBehaviorScore(in d, v * (1f - k));
                        _dCut++; any = true;
                    }
                    else if (d.AiBehavior == AiBehavior.GoToSettlement && !d.WillGatherArmy)
                    {
                        var st = d.Party as Settlement;
                        if (st == null || !st.IsFortification || st.IsUnderSiege || st.MapFaction != f) continue;
                        if (!st.IsTown)
                        {
                            // 524 (2): zamek dla glodnej / z malym zapasem bez podbicia (jak 408 _aiStarve / _aiFood) - w zamku nie kupi, wygrywa wizyta gry po zapasy
                            if (food < 0) { bool sv; food = LowFood(party, out sv) ? 1 : 0; }
                            if (food == 1) { _dBoostFood++; continue; }
                        }
                        partyThinkParams.SetBehaviorScore(in d, v * (1f + 0.5f * x.C));
                        _dBoost++; any = true;
                    }
                }
                _copy.Clear();
                if (any) _dThinkC++;
            }
            catch (Exception e) { _copy.Clear(); Stumble(ref _sCaution, "CautionPostfix", e); }
        }

        // ------------------------------------------------------------ (E) ostroznosc w inicjatywie (watki AI)
        /// <summary>Postfiks DefaultMobilePartyAIModel.CalculateInitiativeScoresForEnemy(mobileParty, enemyParty, out avoidScore, out attackScore, ...).
        /// Watki AI: tylko migawka (podmieniana w calosci), liczniki Interlocked, bez alokacji. Nie rzuca.</summary>
        public static void InitPostfix(MobileParty __0, MobileParty __1, ref float __2, ref float __3, float __4)
        {
            var snap = _snap;
            if (snap.Count == 0 || __0 == null || __1 == null) return;
            try
            {
                Snap x;
                if (!snap.TryGetValue(__0, out x)) return;
                var army = __0.Army;
                if (army != null && army.LeaderParty != __0) return;   // E4: z armii tylko wodz (sama ostroznosc, stan None)
                bool lead = army != null;
                var s = Settings.Current;
                if (s == null) return;
                bool stand = !lead && x.St == Stand && s.AiWoundedRestEnabled;
                bool fort = !lead && x.St == Fort && s.AiWoundedRestEnabled;   // recenzja: jazda do twierdzy jak postoj - bez zaczepek po drodze
                bool road = !lead && x.Road;   // recenzja E1: wodz na lozu w drodze do twierdzy 408 - ta sama bramka szansy co postoj / Fort
                float c = stand || fort ? 1f : (s.AiWoundedCaution ? x.C : 0f);
                if (c <= 0f && !road) return;
                if (__3 > 0f)
                {
                    bool own = ArmsFirst.DefendsOwn(__0, __1);
                    // E1: z postoju / drogi do twierdzy napastnika osady rodu atakujemy tylko z szansa (localAdvantage gry >= prog)
                    if (own && (stand || fort || road) && __4 < Edge(s)) { __3 = 0f; Interlocked.Increment(ref _iDefOff); }
                    else if (own || LordEscort.GuardAllowsAttack(__0, __1)) Interlocked.Increment(ref _iKeep);
                    else if (stand) { __3 = 0f; Interlocked.Increment(ref _iZero); }
                    else if (fort) { __3 = 0f; Interlocked.Increment(ref _iZeroFort); }
                    else
                    {
                        float kc = K(s) * c;
                        if (kc > 0f)
                        {
                            // gra z bliska mnozy atak x100 - ciecie skali nie wystarcza; bez wyraznej przewagi sil (__4 = localAdvantage) atak ponizej progu 1
                            if (__4 < 1f + AdvPerCaution * kc) { __3 = Math.Min(__3, 0.99f) * (1f - kc); Interlocked.Increment(ref _iCap); }
                            else { __3 *= 1f - kc; Interlocked.Increment(ref _iCut); }
                            if (lead) Interlocked.Increment(ref _iArmyLead);
                        }
                    }
                }
                if (__2 > 0f && c > 0f) { __2 *= 1f + c; Interlocked.Increment(ref _iAvoid); }
            }
            catch (Exception e) { Stumble(ref _sInit, "InitPostfix", e); }
        }

        // ------------------------------------------------------------ latki, start i linia doby
        /// <summary>OnSessionLaunched (raz na proces): trzy postfiksy; linia startu. Nie rzuca.</summary>
        internal static void SessionStart()
        {
            if (_h == null)
            {
                _h = new Harmony("com.jeff.armoury.aiwounded500b");
                var me = typeof(AiWounded500b);
                try
                {
                    var m = AccessTools.DeclaredMethod(typeof(CampaignEventDispatcher), "AiHourlyTick", new[] { typeof(MobileParty), typeof(PartyThinkParams) });
                    if (m == null) { _hStand = _hCaution = "BRAK (CampaignEventDispatcher.AiHourlyTick)"; }
                    else
                    {
                        _h.Patch(m, postfix: new HarmonyMethod(me, nameof(StandPostfix)) { priority = Priority.High });
                        _hStand = "TAK (High)";
                        _h.Patch(m, postfix: new HarmonyMethod(me, nameof(CautionPostfix)) { priority = 55 });
                        _hCaution = "TAK (55)";
                    }
                }
                catch (Exception e) { _hStand = _hCaution = "wyjatek " + e.GetType().Name; Stumble(ref _sScan, "SessionStart.Think", e); }
                try
                {
                    var m = AccessTools.DeclaredMethod(typeof(DefaultMobilePartyAIModel), "CalculateInitiativeScoresForEnemy");
                    var ps = m != null ? m.GetParameters() : null;
                    if (m == null) _hInit = "BRAK (DefaultMobilePartyAIModel.CalculateInitiativeScoresForEnemy)";
                    else if (ps.Length < 4 || ps[0].ParameterType != typeof(MobileParty) || ps[1].ParameterType != typeof(MobileParty)
                             || ps[2].ParameterType != typeof(float).MakeByRefType() || ps[3].ParameterType != typeof(float).MakeByRefType())
                        _hInit = "BRAK (inna sygnatura)";
                    else
                    {
                        _h.Patch(m, postfix: new HarmonyMethod(me, nameof(InitPostfix)) { priority = Priority.Low });
                        _hInit = "TAK";
                    }
                }
                catch (Exception e) { _hInit = "wyjatek " + e.GetType().Name; Stumble(ref _sInit, "SessionStart.Init", e); }
            }
            try
            {
                var s = Settings.Current;
                if (s == null) return;
                _live = s.AiWoundedRestEnabled || s.AiWoundedCaution;
                Log.Info(Tag + "postoj " + (s.AiWoundedRestEnabled ? "TAK" : "NIE") + " (wejscie: HP wodza <= " + s.AiWoundedRestHp + " albo loze Grave, albo ranni >= "
                         + s.AiWoundedRestSharePercent + "%; wyjscie: HP > " + Math.Max(s.AiWoundedRestHp, s.AiWoundedRestReleaseHp) + " i ranni <= " + s.AiWoundedRestExitSharePercent + "%, najdluzej "
                         + s.AiWoundedRestMaxDays + " dob; twierdza blizej niz " + s.AiWoundedRestSkipHours + " h - od razu do niej; potem twierdza regula 408: rod, frakcja, sojusznik;"
                         + " obrona osady rodu tylko do " + Math.Max(1, s.AiWoundedDefendMaxHours) + " h jazdy i z szansa >= " + Edge(s).ToString("F2", Inv) + " x napastnicy, bez garnizonu)"
                         + " | ostroznosc " + (s.AiWoundedCaution ? "TAK" : "NIE") + " (sila " + s.AiWoundedCautionPercent + "%, od HP < " + s.AiWoundedCautionHpStartPercent
                         + "% albo rannych > " + s.AiWoundedCautionShareStartPercent + "%; armia zamknieta od " + s.AiWoundedArmyGatePercent + "%; wodz armii - tylko inicjatywa)"
                         + " | latki: postoj " + _hStand + ", mysl " + _hCaution + ", inicjatywa (CalculateInitiativeScoresForEnemy) " + _hInit
                         + " | log partii " + (s.AiWoundedLog ? "TAK" : "NIE") + ".");
            }
            catch (Exception e) { Stumble(ref _sScan, "SessionStart", e); }
        }

        private static void DayLine(Settings s)
        {
            try
            {
                int stand = 0, fort = 0;
                foreach (var r in _rows.Values) { if (r.St == Stand) stand++; else if (r.St == Fort) fort++; }
                int stumbles = _sScan + _sThink + _sCaution + _sInit + _sArmy + _sParty;
                if (stand + fort + _dStand + _dFortDirect + _dStandEnd + _dFortEnd + _cSome + _dDefThink + _dDefOff + _dFoodNoEnter + _dBoostFood + stumbles == 0 && !s.AiWoundedLog) return;
                double f = 1000.0 / Stopwatch.Frequency;
                Log.Info(Tag + "doba " + _day + " - teraz postoj " + stand + ", do twierdzy " + fort
                         + " | wejscia: postoj " + _dStand + ", od razu twierdza " + _dFortDirect + " (z tego postoj niebezpieczny " + _dUnsafe + "), postoj -> twierdza " + _dFortFromStand
                         + ", koniec postoju " + _dStandEnd + " (limit dob " + _dMaxDays + ", zagrozenie " + _dThreat + ", glod/jedzenie " + _dHunger + ", ustapienie cudzemu rozkazowi " + _dYield
                         + "), koniec twierdzy " + _dFortEnd + " (limit " + FortMaxDays.ToString("F0", Inv) + " dob " + _dFortMax + ", koniec twierdzy: glod " + _dFoodEnd + ")"
                         + ", godziny ponad limit dob z lozem HW " + _dFortBed + ", postoj bez wyjazdu na zamek (glod / malo jedzenia) " + _dFoodStand
                         + ", wejscia wstrzymane (glod / zamek bez zapasu jedzenia) " + _dFoodNoEnter + ", zamki bez podbicia (malo jedzenia) " + _dBoostFood
                         + ", bez twierdzy z droga (koniec bez celu) " + _dNoFort + ", wejscia wstrzymane (" + CoolHours.ToString("F0", Inv) + " h po wyjsciu bez wyleczenia) " + _dCool
                         + " | postoj: Hold " + _dHold + ", Hold ponownie po cudzym rozkazie " + _dReHold + ", mysli z DoNotChangeBehavior " + _dThinkStand
                         + ", mysli bez flagi (rozkaz szukajacy bitwy) " + _dThinkFree + ", opcje bitwy -1 " + _dStandBattleOff + ", opcje zwolania armii -1 " + _dArmyOff
                         + " | obrona osady rodu (E1): mysli wolne z szansa " + _dDefThink + ", obrony bez szansy -1 " + _dDefOff + ", inne opcje -1 przy obronie " + _dDefOnly
                         + ", godziny jazdy na obrone bez Hold " + _dDefRide + ", obrony za daleko " + _dDefFar + ", inicjatywa bez szansy zgaszona " + Volatile.Read(ref _iDefOff)
                         + " | ostroznosc: partie c>0 " + _cSome + " (c>=0.5 " + _cHalf + "), mysli z cieciem " + _dThinkC + ", opcje bitwy przyciete " + _dCut + ", twierdze podbite " + _dBoost
                         + ", lordowie fabuly pominieci " + _dSkipStory
                         + " | inicjatywa: ataki bez przewagi ponizej progu " + Volatile.Read(ref _iCap) + ", z przewaga przyciete " + Volatile.Read(ref _iCut)
                         + ", zgaszone (postoj " + Volatile.Read(ref _iZero) + ", do twierdzy " + Volatile.Read(ref _iZeroFort) + "), zostawione (obrona swoich) " + Volatile.Read(ref _iKeep)
                         + ", unikanie wzmocnione " + Volatile.Read(ref _iAvoid) + " | wodzowie armii (E4): z c>0 " + _cArmy + ", ataki przyciete " + Volatile.Read(ref _iArmyLead)
                         + " | bramki armii: odmowy (wywolania predykatu) " + Volatile.Read(ref _gArmy)
                         + " | przeglad " + _scans + " razy, sr " + (_scans > 0 ? (_scanTicks * f / _scans).ToString("F2", Inv) : "0") + " ms, max " + (_scanMaxTicks * f).ToString("F2", Inv) + " ms"
                         + " | potkniecia: przeglad " + _sScan + ", partie " + _sParty + ", postoj " + _sThink + ", mysl " + _sCaution + ", inicjatywa " + _sInit + ", armie " + _sArmy + ".");
            }
            catch (Exception e) { Stumble(ref _sScan, "DayLine", e); }
        }
    }
}
