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
    ///      - None -> Stand (POSTOJ): HP wodza &lt;= LitterHpThreshold albo loze Grave (ta sama regula co nosze gracza 501) ALBO ranni &gt;= RestShare%;
    ///        postoj tylko w bezpiecznym miejscu (jedzenie na 2 doby, bez silniejszego wroga w promieniu AiCampDangerRadius, najblizsza osada nie wroga),
    ///        inaczej od razu Fort. Skrot: wlasna / sojusznicza twierdza blizej niz RestSkipHours drogi (albo partia w niej stoi) - od razu Fort.
    ///      - Stand -> Fort: HP &gt; LitterHpRelease, bez loza Grave i ranni &lt;= RestExitShare% (zdolna do drogi) ALBO RestMaxDays dob postoju ALBO zagrozenie /
    ///        glod ALBO cudzy rozkaz drugi raz przestawil partie (bez migotania - ustepujemy). Gdy przy wyjsciu c &lt; 0.25 - od razu None.
    ///      - Fort -> None: c &lt; 0.25 albo FortMaxDays (10) dob (zapas na brak twierdzy).
    ///     Przy zmianie stanu RethinkAtNextHourlyTick (mysl w ciagu godziny, nie rozkaz). Postoj: SetMoveModeHold raz (gdy AI czynne, poza osada, bez ucieczki).
    ///     Migawka dla watkow AI: slownik podmieniany w calosci (volatile, wzor NightMarch._aiPenalty), nigdy nie zmieniany po publikacji.
    /// (B) POSTOJ - postfiks High (600) CampaignEventDispatcher.AiHourlyTick: Stand = DoNotChangeBehavior (408, ArmsFirst, pogrzeb, LordEscort, RescueHunt,
    ///     PrisonerTerms... wychodza same; gra nie zmienia rozkazu). Inicjatywa (ucieczka) dziala dalej.
    /// (C) TWIERDZA - regula 408 (HeroWounds.Ai.cs) bierze tez partie w stanie Fort (WantsFort): opcje bitwy -1, wyjazd do twierdzy rodu / frakcji.
    /// (D) OSTROZNOSC W MYSLI - postfiks 55 (po 408 = 60, przed wetami 50/48/45 i Last): opcje bitwy (te same co 408 - HeroWounds.AiIsBattle) o ocenie &gt; 0
    ///     x (1 - K c); GoToSettlement do nieoblezonej twierdzy swojej frakcji x (1 + 0.5 c). Ocen &lt;= 0 nie ruszamy (cudze blokady), obrona osady rodu zostaje.
    ///     Stand: opcje zwolania armii -1 (gra tworzy armie mimo DoNotChangeBehavior - AiPartyThinkBehavior, galaz WillGatherArmy przed sprawdzeniem flagi).
    /// (E) OSTROZNOSC W INICJATYWIE - postfiks DefaultMobilePartyAIModel.CalculateInitiativeScoresForEnemy (prywatna, watki AI co 0.25 h): atak x (1 - K c),
    ///     unikanie x (1 + c) (gra liczy unikanie tylko wobec silniejszych i tylko gdy atak &lt; 1); Stand - atak 0. Wyjatki p9: obrona osady rodu
    ///     (ArmsFirst.DefendsOwn), obronca / chroniony 335 (LordEscort.GuardAllowsAttack).
    /// (F) ARMIE - bramki ArmsFirst (CanLordCreateArmy gry i BK, filtr kandydatow, wezwania BK, okno armii gracza) pytaja ArmsFirst.ArmyOut =
    ///     Unready || AiWounded500b.ArmyOut: c &gt;= ArmyGate% albo Stand / Fort. Czlonek juz w armii zostaje.
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

        internal struct Snap { internal float C; internal byte St; }

        private sealed class Row
        {
            internal Hero Lead; internal byte St; internal double SinceH; internal bool Held; internal int ReHolds; internal int Err; internal int ErrDay = -1;
        }

        private static readonly Dictionary<MobileParty, Row> _rows = new Dictionary<MobileParty, Row>();
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
        private static int _iCut, _iZero, _iAvoid, _iKeep, _gArmy;
        private static int _cMax, _cHalf, _cSome;   // ostatni przeglad: partie z c > 0, c >= 0.5, liczba w stanie
        private static long _scanTicks, _scanMaxTicks; private static int _scans;
        private static int _sScan, _sThink, _sCaution, _sInit, _sArmy, _sParty;   // potkniecia na miejsce

        internal static TextObject ArmyText { get { return _armyText ?? (_armyText = new TextObject("{=!}Its men are badly wounded - it must recover before joining an army.")); } }

        internal static void Reset()
        {
            _rows.Clear(); _snap = Empty; _anyFort = false; _drop.Clear(); _copy.Clear(); _day = -1;
            ClearDay();
            _sScan = _sThink = _sCaution = _sInit = _sArmy = _sParty = 0;
        }

        private static void ClearDay()
        {
            _dStand = _dStandFort = _dFortDirect = _dFortFromStand = _dStandEnd = _dFortEnd = _dHold = _dReHold = _dYield = _dThreat = _dHunger = _dMaxDays = _dFortMax = _dUnsafe = 0;
            _dThinkStand = _dArmyOff = _dCut = _dBoost = _dThinkC = _dSkipStory = 0;
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
            else if (day != _day) { DayLine(s); ClearDay(); _day = day; }
            bool rest = s.AiWoundedRestEnabled, caution = s.AiWoundedCaution;
            _live = rest || caution;
            if (!_live)
            {
                if (_rows.Count > 0 || _snap.Count > 0) { _rows.Clear(); _snap = Empty; _anyFort = false; }
                return;
            }
            long t0 = Stopwatch.GetTimestamp();
            Dictionary<MobileParty, Snap> neu = null;
            bool anyFort = false;
            int cSome = 0, cHalf = 0, cState = 0;
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
                        if (c <= 0f && st == None) continue;
                        if (c > 0f) { cSome++; if (c >= 0.5f) cHalf++; }
                        if (st == Fort) anyFort = true;
                        if (neu == null) neu = new Dictionary<MobileParty, Snap>();
                        neu[mp] = new Snap { C = caution ? c : 0f, St = st };
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
            _cSome = cSome; _cHalf = cHalf; _cMax = cState;
            long dt = Stopwatch.GetTimestamp() - t0;
            _scanTicks += dt; _scans++; if (dt > _scanMaxTicks) _scanMaxTicks = dt;
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
            if (mp.Army != null) return Clear(mp, row);                                                              // armia - decyduje wodz armii
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
            int on = Math.Max(1, s.LitterHpThreshold), off = Math.Max(on, s.LitterHpRelease);
            float w = Share(mp);
            float restShare = Math.Max(1, Math.Min(100, s.AiWoundedRestSharePercent)) / 100f;
            float exitShare = Math.Max(0, Math.Min(100, s.AiWoundedRestExitSharePercent)) / 100f;
            if (exitShare > restShare) exitShare = restShare;

            if (keep == None)
            {
                bool hurt = hp <= on || grade == HeroWounds.Grave;
                bool battered = w >= 0f && w >= restShare;
                if (!hurt && !battered) return None;
                row = new Row { Lead = lh, SinceH = now };
                _rows[mp] = row;
                string why = hurt ? (grade == HeroWounds.Grave ? "wodz na lozu Grave" : "HP wodza " + hp) : "ranni " + Pct(w);
                // skrot: w twierdzy swojej frakcji albo twierdza blisko
                float hrs;
                var at = mp.CurrentSettlement;
                if (at != null && at.IsFortification && at.MapFaction == mp.MapFaction)
                { Enter(mp, row, Fort, now); _dFortDirect++; Say(s, mp, lh, w, "-> TWIERDZA (stoi w " + at.StringId + ", " + why + ")"); return Fort; }
                var near = NearFort(mp, out hrs);
                int skip = Math.Max(0, s.AiWoundedRestSkipHours);
                if (near != null && skip > 0 && hrs <= skip)
                { Enter(mp, row, Fort, now); _dFortDirect++; Say(s, mp, lh, w, "-> TWIERDZA (" + near.StringId + " " + hrs.ToString("F1", Inv) + " h drogi <= " + skip + " h, " + why + ")"); return Fort; }
                string unsafeWhy = Unsafe(mp, s);
                if (unsafeWhy != null)
                { Enter(mp, row, Fort, now); _dFortDirect++; _dUnsafe++; Say(s, mp, lh, w, "-> TWIERDZA (postoj niebezpieczny: " + unsafeWhy + ", " + why + ")"); return Fort; }
                Enter(mp, row, Stand, now); _dStand++;
                HoldOnce(mp, row);
                Say(s, mp, lh, w, "-> POSTOJ (" + why + (near != null ? ", najblizsza twierdza " + near.StringId + " " + hrs.ToString("F1", Inv) + " h" : ", brak twierdzy z droga") + ")");
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
                if (why == null) { HoldOnce(mp, row); return Stand; }
                _dStandEnd++;
                if (c < FortCalm)
                {
                    _rows.Remove(mp); mp.Ai.RethinkAtNextHourlyTick = true;
                    Say(s, mp, lh, w, "POSTOJ -> KONIEC (" + why + ", po " + days.ToString("F1", Inv) + " dob)");
                    row = null; return None;
                }
                Enter(mp, row, Fort, now); _dFortFromStand++;
                Say(s, mp, lh, w, "POSTOJ -> TWIERDZA (" + why + ", po " + days.ToString("F1", Inv) + " dob)");
                return Fort;
            }

            // Fort
            double fd = (now - row.SinceH) / 24.0;
            if (c < FortCalm || fd >= FortMaxDays)
            {
                if (fd >= FortMaxDays) _dFortMax++;
                _dFortEnd++;
                _rows.Remove(mp); mp.Ai.RethinkAtNextHourlyTick = true;
                Say(s, mp, lh, w, "TWIERDZA -> KONIEC (" + (c < FortCalm ? "ostroznosc " + c.ToString("F2", Inv) : "limit " + FortMaxDays.ToString("F0", Inv) + " dob") + ", po " + fd.ToString("F1", Inv) + " dob)");
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
        private static void HoldOnce(MobileParty mp, Row row)
        {
            if (mp.CurrentSettlement != null) return;   // w osadzie stoi sama (DoNotChangeBehavior)
            if (mp.DefaultBehavior == AiBehavior.Hold) { row.Held = true; return; }
            var ai = mp.Ai;
            if (ai == null || ai.IsDisabled || ai.DoNotMakeNewDecisions) return;   // noc NightRest / cudza blokada - po przebudzeniu
            var stb = mp.ShortTermBehavior;
            if (MobileParty.IsFleeBehavior(stb) || stb == AiBehavior.EngageParty) return;   // ucieczka / zaczepka z inicjatywy - nie przerywamy
            // odwrot po bitwie (gra: AiMilitaryBehavior.OnMapEventEnded - SetMoveGoToPoint od zwyciezcy; 458 odwrot wodza; CrossingLaw zawrocenie) - najpierw dojechac
            if (mp.DefaultBehavior == AiBehavior.GoToPoint && mp.IsMoving) return;
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

        /// <summary>Postoj niebezpieczny: powod albo null - glod / malo jedzenia, silniejszy wrog w promieniu alarmu obozu (AiCampDangerRadius, ta sama miara co
        /// NightRest), najblizsza osada wroga.</summary>
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

        /// <summary>Najblizsza nieoblezona twierdza frakcji partii (po tej samej stronie Muru, droga ladowa z modelu gry) i godziny jazdy.</summary>
        private static Settlement NearFort(MobileParty mp, out float hours)
        {
            hours = float.MaxValue;
            var f = mp.MapFaction;
            var fiefs = f != null ? f.Fiefs : null;
            if (fiefs == null || fiefs.Count == 0) return null;
            var pos = mp.GetPosition2D;
            var bw = ArmsFirst.Beyond();
            bool pB = false;
            if (bw != null) { try { pB = bw(pos); } catch { bw = null; } }
            Settlement best = null; float bd = float.MaxValue;
            for (int i = 0; i < fiefs.Count; i++)
            {
                var st = fiefs[i] != null ? fiefs[i].Settlement : null;
                if (st == null || !st.IsFortification || st.IsUnderSiege || st.MapFaction != f) continue;
                if (bw != null) { try { if (bw(st.GetPosition2D) != pB) continue; } catch { } }
                float d = pos.DistanceSquared(st.GetPosition2D);
                if (d < bd) { bd = d; best = st; }
            }
            if (best == null) return null;
            float road = -1f;
            try
            {
                float lr;
                float d = Campaign.Current.Models.MapDistanceModel.GetDistance(mp, best, false, MobileParty.NavigationType.Default, out lr);
                if (CartGuard.NoLandRoad(d)) return null;
                road = d;
            }
            catch { road = (float)Math.Sqrt(bd) * 1.1f; }
            float sp = 0f;
            try { sp = mp.Speed; } catch { }
            if (sp <= 0f) sp = 2.4f;
            hours = road / Math.Max(0.05f, sp / 1.2f);   // jedn./h jak NightMarch.UnitsPerHour
            return best;
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
                partyThinkParams.DoNotChangeBehavior = true;
                _dThinkStand++;
            }
            catch (Exception e) { Stumble(ref _sThink, "StandPostfix", e); }
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
                    for (int i = 0; i < _copy.Count; i++)
                    {
                        if (!_copy[i].Item1.WillGatherArmy || !(_copy[i].Item2 > -1f)) continue;
                        var d = _copy[i].Item1;
                        partyThinkParams.SetBehaviorScore(in d, -1f);
                        _dArmyOff++;
                    }
                    _copy.Clear();
                    return;
                }
                if (!s.AiWoundedCaution || x.C <= 0f || partyThinkParams.DoNotChangeBehavior) { _copy.Clear(); return; }
                var clan = party.ActualClan;
                var f = party.MapFaction;
                float k = K(s) * x.C;
                bool any = false;
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
        public static void InitPostfix(MobileParty __0, MobileParty __1, ref float __2, ref float __3)
        {
            var snap = _snap;
            if (snap.Count == 0 || __0 == null || __1 == null) return;
            try
            {
                Snap x;
                if (!snap.TryGetValue(__0, out x)) return;
                if (__0.Army != null) return;
                var s = Settings.Current;
                if (s == null) return;
                bool stand = x.St == Stand && s.AiWoundedRestEnabled;
                float c = stand ? 1f : (s.AiWoundedCaution ? x.C : 0f);
                if (c <= 0f) return;
                if (__3 > 0f)
                {
                    if (ArmsFirst.DefendsOwn(__0, __1) || LordEscort.GuardAllowsAttack(__0, __1)) Interlocked.Increment(ref _iKeep);
                    else if (stand) { __3 = 0f; Interlocked.Increment(ref _iZero); }
                    else { __3 *= 1f - K(s) * c; Interlocked.Increment(ref _iCut); }
                }
                if (__2 > 0f) { __2 *= 1f + c; Interlocked.Increment(ref _iAvoid); }
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
                Log.Info(Tag + "postoj " + (s.AiWoundedRestEnabled ? "TAK" : "NIE") + " (wejscie: HP wodza <= " + s.LitterHpThreshold + " albo loze Grave, albo ranni >= "
                         + s.AiWoundedRestSharePercent + "%; wyjscie: HP > " + s.LitterHpRelease + " i ranni <= " + s.AiWoundedRestExitSharePercent + "%, najdluzej "
                         + s.AiWoundedRestMaxDays + " dob; twierdza blizej niz " + s.AiWoundedRestSkipHours + " h - od razu do niej; potem twierdza regula 408)"
                         + " | ostroznosc " + (s.AiWoundedCaution ? "TAK" : "NIE") + " (sila " + s.AiWoundedCautionPercent + "%, od HP < " + s.AiWoundedCautionHpStartPercent
                         + "% albo rannych > " + s.AiWoundedCautionShareStartPercent + "%; armia zamknieta od " + s.AiWoundedArmyGatePercent + "%)"
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
                if (stand + fort + _dStand + _dFortDirect + _dStandEnd + _dFortEnd + _cSome + stumbles == 0 && !s.AiWoundedLog) return;
                double f = 1000.0 / Stopwatch.Frequency;
                Log.Info(Tag + "doba " + _day + " - teraz postoj " + stand + ", do twierdzy " + fort
                         + " | wejscia: postoj " + _dStand + ", od razu twierdza " + _dFortDirect + " (z tego postoj niebezpieczny " + _dUnsafe + "), postoj -> twierdza " + _dFortFromStand
                         + ", koniec postoju " + _dStandEnd + " (limit dob " + _dMaxDays + ", zagrozenie " + _dThreat + ", glod/jedzenie " + _dHunger + ", ustapienie cudzemu rozkazowi " + _dYield
                         + "), koniec twierdzy " + _dFortEnd + " (limit " + FortMaxDays.ToString("F0", Inv) + " dob " + _dFortMax + ")"
                         + " | postoj: Hold " + _dHold + ", Hold ponownie po cudzym rozkazie " + _dReHold + ", mysli z DoNotChangeBehavior " + _dThinkStand + ", opcje zwolania armii -1 " + _dArmyOff
                         + " | ostroznosc: partie c>0 " + _cSome + " (c>=0.5 " + _cHalf + "), mysli z cieciem " + _dThinkC + ", opcje bitwy przyciete " + _dCut + ", twierdze podbite " + _dBoost
                         + ", lordowie fabuly pominieci " + _dSkipStory
                         + " | inicjatywa: ataki przyciete " + Volatile.Read(ref _iCut) + ", zgaszone (postoj) " + Volatile.Read(ref _iZero) + ", zostawione (obrona swoich) " + Volatile.Read(ref _iKeep)
                         + ", unikanie wzmocnione " + Volatile.Read(ref _iAvoid)
                         + " | bramki armii: odmowy (wywolania predykatu) " + Volatile.Read(ref _gArmy)
                         + " | przeglad " + _scans + " razy, sr " + (_scans > 0 ? (_scanTicks * f / _scans).ToString("F2", Inv) : "0") + " ms, max " + (_scanMaxTicks * f).ToString("F2", Inv) + " ms"
                         + " | potkniecia: przeglad " + _sScan + ", partie " + _sParty + ", postoj " + _sThink + ", mysl " + _sCaution + ", inicjatywa " + _sInit + ", armie " + _sArmy + ".");
            }
            catch (Exception e) { Stumble(ref _sScan, "DayLine", e); }
        }
    }
}
