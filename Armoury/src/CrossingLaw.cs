using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// PRAWO PRZEPRAWY (Jeff 30.08, screen spod Twins: "jak to mozliwe, ze
    /// jako wroga armia moge przejsc przez Twins?! Ten, kto ma zamek,
    /// przepuszcza sojusznikow, a wrogow nie!"). Bliznaki to JEDYNY most
    /// przez Zielona Galaz - Freyowie zyli z bramkowania przeprawy, a gra
    /// traktuje most jak zwykly teren. Odtad: partia GRACZA w stanie WOJNY
    /// z wlascicielem przeprawy nie wjedzie w jej strefe - straz mostu
    /// wypycha ja tam, skad przyszla (SetMoveGoToPoint na ostatnia
    /// bezpieczna pozycje - zero teleportow, naturalny ruch). Sojusznicy
    /// i neutralni przechodza. Chcesz na druga strone? Zdobadz zamek,
    /// zawrzyj pokoj albo plyn morzem.
    /// ETAP 2 (Jeff 31.08): takze WROGIE PARTIE AI. Zabezpieczenie przed
    /// zapetleniem pathfindingu: odbita partia dostaje CEL-ODWROT (punkt po
    /// swojej stronie strefy) i osobisty COOLDOWN 3 h - w tym czasie straz
    /// jej nie tyka, wiec AI dochodzi do celu i podejmuje wlasna decyzje
    /// zamiast wibrowac na granicy. Oblezenie przeprawy jest LEGALNE
    /// (BesiegerCamp = skip) - wroga droga na druga strone to zdobycie
    /// warowni, pokoj albo morze. Eskorty armii ida za wodzem (skip
    /// doczepionych; odbijamy tylko partie prowadzace).
    /// E8/Q (262, ERRORS/E7; projekt etapu 8 v2 rozdz. 3 "E8/Q"): etap 2 nie dzialal nigdy - AiTick stoi za wyjsciem "gracz poza wroga strefa",
    /// wiec biegl tylko wtedy, gdy straz wypychala samego gracza (120 dob testu: 0 linii). Przy kluczu CrossingLawAiClock petla biegnie raz na
    /// godzine gry (AiHourly, HourlyTickEvent), niezaleznie od gracza: po MobileParty.AllLordParties, odleglosc od przeprawy pierwsza, spiacy
    /// (SleepGate E7/O1) pomijani PRZED rozkazem i przerwa. Probka co godzine lapie partie gdziekolwiek w strefie (takze za mostem), wiec punkt
    /// odwrotu to jej ostatnia pozycja SPRZED strefy (pamiec partii w pasie 8 jedn. wokol strefy - "wracaj, skad przyszedles", jak u gracza);
    /// bez pamieci - prosto od zamku jak dotad. Linia doby "Straz przeprawy (E8/Q)" i dopisek do linii E7/B. Klucz wylaczony = AiTick jak dotad.
    /// </summary>
    internal static class CrossingLaw
    {
        // przeprawy-warownie: id osad ROT (Jeff 31.08: "Crossing law - robimy")
        // The Twins = ROT_town3, Moat Cailin = castle_B1, Bloody Gate = castle_EN2
        private static readonly string[] CrossingIds = { "ROT_town3", "castle_B1", "castle_EN2" };

        private static float _acc;
        private static Vec2 _lastSafe;
        private static bool _haveSafe;
        private static DateTime _lastShout = DateTime.MinValue;

        /// <summary>E8/S (246, PLAN 8.11): nowa kampania albo wczytanie (konstruktor ArmouryBehavior przez StateHygiene.ResetAll). Dotad ostatnia bezpieczna
        /// pozycja gracza i przerwy odbitych partii AI zyly do konca procesu: w nowej kampanii straz przeprawy odsylala gracza w miejsce z POPRZEDNIEJ
        /// (teraz jak po uruchomieniu gry - bez pozycji: prosto od zamku), a slownik trzymal partie starej gry. Zwraca, ile bylo w pamieci (do linii startu).</summary>
        internal static int Reset()
        {
            int had = (_haveSafe ? 1 : 0) + _aiCooldown.Count + _aiOutside.Count;
            _lastSafe = default(Vec2); _haveSafe = false;
            _aiCooldown.Clear();
            // E8/Q (262): pamiec drogi partii AI, liczniki doby i potkniecia petli godzinnej
            _aiOutside.Clear(); _aiDrop.Clear(); _errSites.Clear();
            _stamp = 0; _stumbles = 0;
            had += _aimMem.Count + _aimBlock.Count;
            _aimMem.Clear(); _aimBlock.Clear(); AimAny = false;     // A12 (Q-3): pamiec zawrocen i blokady celow - tylko ta kampania (bez zapisu)
            DayReset();
            return had;
        }

        internal static void OnTick(float dt)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.CrossingLawEnabled) return;
                _acc += dt;
                if (_acc < 0.5f) return;
                _acc = 0f;

                var main = MobileParty.MainParty;
                if (main == null || !main.IsActive) return;
                if (main.CurrentSettlement != null || main.MapEvent != null) return;
                if (main.AttachedTo != null) return;   // w cudzej armii decyduje jej dowodca

                Vec2 pos = main.Position.ToVec2();
                bool inHostileZone = false;
                Settlement zone = null;

                foreach (var id in CrossingIds)
                {
                    var st = Settlement.Find(id);
                    if (st == null) continue;
                    float d = pos.Distance(st.GetPosition2D);
                    if (d > s.CrossingRadius) continue;
                    var owner = st.MapFaction;
                    var mine = main.MapFaction;
                    if (owner == null || mine == null || owner == mine) continue;
                    if (!FactionManager.IsAtWarAgainstFaction(owner, mine)) continue;
                    inHostileZone = true; zone = st;
                    break;
                }

                if (!inHostileZone)
                {
                    _lastSafe = pos; _haveSafe = true;
                    return;
                }

                // straz mostu wypycha: wracaj, skad przyszedles; po wczytaniu
                // save'a W strefie - odepchnij prosto od zamku
                Vec2 back;
                if (_haveSafe) back = _lastSafe;
                else
                {
                    Vec2 away = pos - zone.GetPosition2D;
                    if (away.LengthSquared < 0.01f) away = new Vec2(1f, 0f);
                    away.Normalize();
                    back = zone.GetPosition2D + away * (s.CrossingRadius + 1f);
                }
                main.SetMoveGoToPoint(new CampaignVec2(back, false), MobileParty.NavigationType.Default);

                if ((DateTime.Now - _lastShout).TotalSeconds > 5)
                {
                    _lastShout = DateTime.Now;
                    Log.Player("The " + zone.Name + " bar the crossing - no enemy of " + (zone.OwnerClan != null ? zone.OwnerClan.Name.ToString() : "the holders")
                               + " rides this bridge. Take the castle, make peace, or go by sea.", true);
                    Log.Info("CrossingLaw: gracz odepchniety od przeprawy " + zone.StringId + ".");
                }

                if (!s.CrossingLawAiClock) AiTick(s);   // E8/Q (262): przy zegarze gry petla AI biegnie z HourlyTickEvent (AiHourly), nie stad
            }
            catch (Exception e) { Log.Error("CrossingLaw.OnTick", e); }
        }

        // ---- etap 2: straz mostu zawraca wrogie partie AI ----
        private static readonly System.Collections.Generic.Dictionary<MobileParty, CampaignTime> _aiCooldown =
            new System.Collections.Generic.Dictionary<MobileParty, CampaignTime>();
        private static float _aiAcc;
        private static int _aiShoutStock = 3;   // max meldunki na dobe, zeby nie zalewac logu gracza

        private static void AiTick(Settings s)
        {
            _aiAcc += 0.5f;
            if (_aiAcc < 2f) return;   // AI co ~2 s realne wystarczy
            _aiAcc = 0f;
            try
            {
                if (!s.CrossingLawAi) return;
                foreach (var id in CrossingIds)
                {
                    var st = Settlement.Find(id);
                    if (st == null) continue;
                    var owner = st.MapFaction;
                    if (owner == null) continue;
                    Vec2 gate = st.GetPosition2D;

                    foreach (var mp in MobileParty.All)
                    {
                        if (mp == null || !mp.IsActive || mp == MobileParty.MainParty) continue;
                        if (!mp.IsLordParty) continue;                       // strzezemy przed wojskiem, nie chlopstwem
                        if (mp.CurrentSettlement != null || mp.MapEvent != null) continue;
                        if (mp.BesiegerCamp != null) continue;               // oblega przeprawe - legalna droga
                        if (mp.AttachedTo != null) continue;                 // eskorta idzie za wodzem
                        var f = mp.MapFaction;
                        if (f == null || f == owner || !FactionManager.IsAtWarAgainstFaction(owner, f)) continue;
                        if (gate.Distance(mp.GetPosition2D) > s.CrossingRadius) continue;

                        CampaignTime until;
                        if (_aiCooldown.TryGetValue(mp, out until) && until > CampaignTime.Now) continue;

                        // cel-odwrot: punkt po stronie, z ktorej partia przyszla
                        Vec2 away = mp.GetPosition2D - gate;
                        if (away.LengthSquared < 0.01f) away = new Vec2(1f, 0f);
                        away.Normalize();
                        Vec2 back = gate + away * (s.CrossingRadius + 2f);
                        mp.SetMoveGoToPoint(new CampaignVec2(back, false), MobileParty.NavigationType.Default);
                        _aiCooldown[mp] = CampaignTime.HoursFromNow(3f);

                        if (_aiShoutStock > 0)
                        {
                            _aiShoutStock--;
                            Log.Player("The " + st.Name + " turn back " + mp.Name + " - the crossing is barred to enemies.", false);
                        }
                        Log.Info("CrossingLaw: AI " + mp.StringId + " zawrocone od " + st.StringId + " (cooldown 3h).");
                    }
                }
                // odswiez pule meldunkow raz na dobe i przytnij slownik z martwych partii
                if (CampaignTime.Now.GetHourOfDay == 12) _aiShoutStock = 3;
                if (_aiCooldown.Count > 200)
                {
                    var dead = new System.Collections.Generic.List<MobileParty>();
                    foreach (var kv in _aiCooldown)
                        if (kv.Key == null || !kv.Key.IsActive || kv.Value <= CampaignTime.Now) dead.Add(kv.Key);
                    foreach (var k in dead) _aiCooldown.Remove(k);
                }
            }
            catch (Exception e) { Log.Error("CrossingLaw.AiTick", e); }
        }

        // ---- E8/Q (262): petla AI na zegarze gry (klucz CrossingLawAiClock; wylaczony = AiTick wyzej, jak dotad) ----
        private const float OuterRing = 8f;        // pas pamieci drogi wokol strefy: partia nie przejdzie go miedzy dwiema probkami godzinnymi (jak strefa E7/B)
        private const int EventLinesMax = 30;      // linie "CrossingLaw: AI ... zawrocone" na dobe (reszta tylko w liczniku linii doby)
        private const int RepeatLinesMax = 10;     // drobne2 (Q-3, diagnoza): linie zawrocenia nr 3+ tej doby z celem AI - osobny limit na dobe
        private static readonly float[] RadialSteps = { 2f, 1f, 3f, 4f };   // punkt odwrotu prosto od zamku: promien strefy + tyle (pierwszy wazny; 2 = jak dotad)

        private struct Outside { public Vec2 Pos; public Settlement At; public int Stamp; }

        // ostatnia pozycja wrogiej partii lordow SPRZED strefy (tylko partie w pasie wokol przeprawy; wpis znika, gdy partia odejdzie, zginie albo nastanie pokoj)
        private static readonly Dictionary<MobileParty, Outside> _aiOutside = new Dictionary<MobileParty, Outside>();
        private static readonly Dictionary<MobileParty, int> _dayTurns = new Dictionary<MobileParty, int>();
        private static readonly List<MobileParty> _aiDrop = new List<MobileParty>();
        private static readonly HashSet<string> _errSites = new HashSet<string>();
        private static readonly int[] _dTurnedAt = new int[CrossingIds.Length];
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static int _stamp, _stumbles, _stumblesDay;
        // liczniki doby (zerowane z linia "Straz przeprawy (E8/Q)")
        private static int _dPasses, _dNear, _dZone, _dTurned, _dFromMemory, _dBadPoint, _dBusy, _dSea, _dEscort, _dSiege, _dSleep, _dAiOff, _dCool;
        private static int _dCross, _dCrossCool, _dEventLines, _dMissing, _dRepeatLines;
        private static long _dSeen, _ticks;

        private static bool ClockOn(Settings s) { return s != null && s.CrossingLawEnabled && s.CrossingLawAi && s.CrossingLawAiClock; }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumblesDay++;
            if (_errSites.Add(where)) Log.Error("CrossingLaw." + where, e);   // jedna linia ERROR na miejsce w sesji; petla biegnie dalej (potkniecia w linii doby)
        }

        private static void DayReset()
        {
            _dPasses = _dNear = _dZone = _dTurned = _dFromMemory = _dBadPoint = _dBusy = _dSea = _dEscort = _dSiege = _dSleep = _dAiOff = _dCool = 0;
            _dCross = _dCrossCool = _dEventLines = _dMissing = _dRepeatLines = 0;
            _dAimNew = _dAimZeroed = _dAimHit = _dAimNoTarget = _dAimParties = 0;     // A12 (Q-3)
            _dSeen = 0; _ticks = 0; _stumblesDay = 0;
            Array.Clear(_dTurnedAt, 0, _dTurnedAt.Length);
            _dayTurns.Clear();
        }

        /// <summary>Raz na godzine gry (HourlyTickEvent, po probce E7/B): wrogie partie lordow w strefie przeprawy dostaja rozkaz odwrotu. Jedna wywrotka
        /// na jednej partii nie przerywa przebiegu ani nie gasi petli - potkniecia sa liczone (CLAUDE.md 7).</summary>
        internal static void AiHourly()
        {
            var s = Settings.Current;
            if (!ClockOn(s) || Campaign.Current == null)
            {
                if (_aiOutside.Count > 0) _aiOutside.Clear();
                return;
            }
            long t0 = Stopwatch.GetTimestamp();
            if (s.CrossingLawAimBlock && !_aimTried) { try { EnsureAimHook(); } catch (Exception e) { Stumble("EnsureAimHook", e); } }   // A12 (Q-3): latka wyboru celu AI - raz, w kampanii
            try
            {
                int n = CrossingIds.Length, found = 0;
                var cs = new Settlement[n];
                var gate = new Vec2[n];
                for (int j = 0; j < n; j++)
                {
                    cs[j] = Settlement.Find(CrossingIds[j]);   // co przebieg: obiekt osady jest inny w kazdej kampanii (bez pamieci miedzy wczytaniami)
                    if (cs[j] != null) { gate[j] = cs[j].GetPosition2D; found++; }
                }
                _dPasses++; _stamp++;
                _dMissing = n - found;
                if (found == 0) return;
                float r = s.CrossingRadius, r2 = r * r, o = r + OuterRing, o2 = o * o;
                var main = MobileParty.MainParty;
                var list = MobileParty.AllLordParties;
                for (int i = 0; i < list.Count; i++)
                {
                    try { Watch(list[i], main, cs, gate, r, r2, o2); }
                    catch (Exception e) { Stumble("AiHourly(partia)", e); }
                }
                // wpisy pamieci drogi partii, ktore odeszly od przeprawy, zginely albo przestaly byc wrogiem
                if (_aiOutside.Count > 0)
                {
                    _aiDrop.Clear();
                    foreach (var kv in _aiOutside) if (kv.Value.Stamp != _stamp) _aiDrop.Add(kv.Key);
                    for (int i = 0; i < _aiDrop.Count; i++) _aiOutside.Remove(_aiDrop[i]);
                    _aiDrop.Clear();
                }
            }
            catch (Exception e) { Stumble("AiHourly", e); }
            finally { _ticks += Stopwatch.GetTimestamp() - t0; }
        }

        /// <summary>Jedna partia lordow w jednym przebiegu. Kolejnosc = koszt: odleglosc od przepraw (ok. 600 partii x 3), dopiero potem wojna z panem
        /// przeprawy i reszta warunkow (kilka-kilkanascie partii przy przeprawach).</summary>
        private static void Watch(MobileParty mp, MobileParty main, Settlement[] cs, Vec2[] gate, float r, float r2, float o2)
        {
            if (mp == null || !mp.IsActive || mp == main) return;
            _dSeen++;
            Vec2 pos = mp.GetPosition2D;
            int k = -1; float d2 = 0f;
            for (int j = 0; j < cs.Length; j++)
            {
                if (cs[j] == null) continue;
                float q = pos.DistanceSquared(gate[j]);
                if (q <= o2) { k = j; d2 = q; break; }
            }
            if (k < 0) return;
            var st = cs[k];
            var owner = st.MapFaction;
            var f = mp.MapFaction;
            if (owner == null || f == null || f == owner || !FactionManager.IsAtWarAgainstFaction(owner, f)) return;
            _dNear++;
            bool sea = mp.IsCurrentlyAtSea;
            Outside mem;
            bool has = _aiOutside.TryGetValue(mp, out mem) && mem.At == st;
            if (d2 > r2)
            {
                // przed strefa: zapamietaj, skad idzie (pozycja na ladzie) - tam odesle ja straz
                if (!sea) { mem.Pos = pos; mem.At = st; mem.Stamp = _stamp; _aiOutside[mp] = mem; }
                return;
            }
            if (has) { mem.Stamp = _stamp; _aiOutside[mp] = mem; }   // w strefie: wpis zyje, pozycja sprzed strefy bez zmian
            _dZone++;
            if (mp.CurrentSettlement != null || mp.MapEvent != null) { _dBusy++; return; }
            if (sea) { _dSea++; return; }                                             // "go by sea" - droga woda jest legalna
            if (mp.AttachedTo != null) { _dEscort++; return; }                        // eskorta idzie za wodzem
            if (mp.BesiegerCamp != null || SiegeBound(mp, st)) { _dSiege++; return; } // oblezenie przeprawy - legalna droga (takze dojscie do niego)
            // spiacy PRZED rozkazem i przerwa: bramka E7/O1 zatrzymalaby SetMoveGoToPoint (i zapisala punkt odwrotu jako rozkaz na swit), a licznik i tak by urosl
            if (NightRest.SleepKind(mp) != NightRest.SkNone) { _dSleep++; return; }
            // AI wylaczone cudza reka albo partia bez wlasnych decyzji (rozwiazywana, zadanie): po naszym rozkazie nie podjelaby nowej decyzji - stalaby w punkcie odwrotu
            var ai = mp.Ai;
            if (ai == null || ai.IsDisabled || ai.DoNotMakeNewDecisions) { _dAiOff++; return; }
            CampaignTime until;
            if (_aiCooldown.TryGetValue(mp, out until) && until > CampaignTime.Now) { _dCool++; return; }

            // cel-odwrot: skad partia przyszla (ostatnia probka sprzed strefy); bez pamieci (wczytanie w strefie, wyjscie z bitwy) - prosto od zamku, jak dotad
            // Punkt zawsze NA LADZIE i wazny dla gry (IsOnLand = true, IsValid): gra wyznacza do niego droge. Punktu niewaznego (woda, gory, dziura siatki)
            // nie dajemy wcale - MobileParty.DoAIMove przy takim celu co klatke nie znajduje drogi (ComputePath -> FailedAssert) i pcha partie po prostej.
            CampaignVec2 point = default(CampaignVec2);
            bool ok = false, fromMem = false;
            if (has) { point = new CampaignVec2(mem.Pos, true); ok = fromMem = point.IsValid(); }
            if (!ok)
            {
                Vec2 away = pos - gate[k];
                if (away.LengthSquared < 0.01f) away = new Vec2(1f, 0f);
                away.Normalize();
                for (int t = 0; t < RadialSteps.Length && !ok; t++)
                {
                    point = new CampaignVec2(gate[k] + away * (r + RadialSteps[t]), true);
                    ok = point.IsValid();
                }
            }
            if (!ok) { _dBadPoint++; return; }   // brak punktu na ladzie po tej stronie - bez rozkazu i bez przerwy (liczone)
            if (fromMem) _dFromMemory++;
            // drobne2 (Q-3, diagnoza): cel AI przy 3. i kolejnym zawroceniu tej doby - odczyt PRZED rozkazem odwrotu (SetMoveGoToPoint zmienia zachowanie)
            int c0; _dayTurns.TryGetValue(mp, out c0);
            string aim = c0 + 1 >= 3 ? AiAim(mp) : null;
            // A12 (Q-3): drugie zawrocenie w 72 h - cel AI partii (osada za przeprawa) zablokowany na AimBlockDays dob; odczyt PRZED rozkazem odwrotu (jak AiAim)
            string block = null;
            if (AimOn()) { try { block = AimTurn(mp, st); } catch (Exception e) { Stumble("AimTurn", e); } }
            mp.SetMoveGoToPoint(point, MobileParty.NavigationType.Default);
            _aiCooldown[mp] = CampaignTime.HoursFromNow(3f);
            _dTurned++; _dTurnedAt[k]++;
            int c; _dayTurns.TryGetValue(mp, out c); _dayTurns[mp] = c + 1;

            // meldunek tylko o partii, ktora gracz widzi na mapie (petla biegnie teraz na calym swiecie) - najwyzej 3 na dobe
            if (_aiShoutStock > 0 && mp.IsVisible)
            {
                _aiShoutStock--;
                Log.Player("The watch at " + st.Name + " turns back " + mp.Name + " - the crossing is barred to enemies.", false);
            }
            bool rep = aim != null && _dRepeatLines < RepeatLinesMax;
            if (rep || _dEventLines < EventLinesMax)
            {
                if (rep) _dRepeatLines++; else _dEventLines++;
                Log.Info("CrossingLaw: AI " + mp.StringId + " zawrocone od " + st.StringId + " (E8/Q, zegar gry; " + (fromMem ? "tam, skad przyszla" : "prosto od zamku")
                         + ", " + Math.Sqrt(d2).ToString("0.0", Inv) + " jedn. od przeprawy, zawrocenie nr " + (c + 1) + " tej doby, cooldown 3h" + (aim ?? "") + (block ?? "") + ").");
            }
        }

        // ---- A12 (Q-3): zmiana celu AI po drugim zawroceniu (klucz CrossingLawAimBlock; tylko na zegarze gry E8/Q) ----
        // Dowod (bieg 6-letni 14/15.10, 354 linie "cel AI (Q-3"): przy 3. zawroceniu doby cel AI ten sam co wczesniej (DefendSettlement ROT_castle16, GoToSettlement
        // ROT_town33, RaidSettlement ROT_town33_village2 ...) - AI po przerwie 3 h wybiera znow te sama osade za mostem; zawrocen nr 3-6 tej doby 354 w 3 biegach.
        // Regula: drugie zawrocenie tej partii przy tej przeprawie w AimWindowHours - osada, do ktorej AI ja prowadzi (TargetSettlement; gdy teraz brak - z poprzedniego
        // zawrocenia) dostaje blokade na AimBlockDays dob: postfiks CampaignEventDispatcher.AiHourlyTick (po wszystkich sluchaczach zachowan, przed wyborem najlepszego
        // w AiPartyThinkBehavior) daje wynik -1 kazdemu zachowaniu z ta osada (nigdy wybrane). Przeprawa sama nie jest blokowana (oblezenie legalne). Bez zapisu (blokada 5 dob;
        // po wczytaniu - od nowa po dwoch zawroceniach). Pokoj z panem przeprawy - blokada nie dziala (postfiks sprawdza wojne).
        // ROT (dekompilacja ROT.dll): AIThinkPatch.Patch1 - postfiks tej samej metody (usuwa cele za Murem: Reset + AddBehaviorScore z kopia wynikow - nasze -1 zostaja
        // w obu kolejnosciach) i prefiks zastepujacy PartyHourlyAiTick tym samym wyborem (start -1, bierze tylko wiekszy) - zgodne.
        private const int AimTurns = 2, AimTargetsMax = 4;
        private const float AimWindowHours = 72f, AimBlockDays = 5f;
        private sealed class AimMem { public Settlement At; public Settlement LastAim; public readonly List<CampaignTime> Turns = new List<CampaignTime>(4); }
        private sealed class AimBlockRec { public Settlement At; public CampaignTime Until; public readonly List<Settlement> Targets = new List<Settlement>(AimTargetsMax); }
        private static readonly Dictionary<MobileParty, AimMem> _aimMem = new Dictionary<MobileParty, AimMem>();
        private static readonly Dictionary<MobileParty, AimBlockRec> _aimBlock = new Dictionary<MobileParty, AimBlockRec>();
        private static volatile bool AimAny;          // szybka bramka postfiksu (wola go kazda decyzja AI kazdej partii)
        private static bool _aimTried, _aimHooked;
        private static string _aimHookNote = "nie probowano";
        private static int _dAimNew, _dAimZeroed, _dAimHit, _dAimNoTarget, _dAimParties;

        private static bool AimOn() { var s = Settings.Current; return s != null && s.CrossingLawAimBlock && ClockOn(s); }

        /// <summary>Latka (wlasna instancja Harmony - bez SubModuleMain): postfiks CampaignEventDispatcher.AiHourlyTick(MobileParty, PartyThinkParams) - metoda zadeklarowana
        /// w tej klasie (override), cel przez DeclaredMethod (CLAUDE.md 7). Raz na proces; blad - zapamietany w linii doby, petla strazy dziala dalej bez zmiany celu.</summary>
        private static void EnsureAimHook()
        {
            _aimTried = true;
            try
            {
                var m = AccessTools.DeclaredMethod(typeof(CampaignEventDispatcher), "AiHourlyTick", new[] { typeof(MobileParty), typeof(PartyThinkParams) });
                if (m == null) { _aimHookNote = "BRAK (CampaignEventDispatcher.AiHourlyTick)"; return; }
                new Harmony("com.armoury.crossingaim").Patch(m, postfix: new HarmonyMethod(typeof(CrossingLaw), nameof(AimPostfix)));
                _aimHooked = true; _aimHookNote = "wpieta";
                Log.Info("CrossingLaw (Q-3, A12): latka wyboru celu AI (postfiks CampaignEventDispatcher.AiHourlyTick) wpieta.");
            }
            catch (Exception e) { _aimHookNote = "BRAK (" + e.GetType().Name + ")"; Stumble("EnsureAimHook", e); }
        }

        /// <summary>Rozkaz odwrotu (Watch): pamiec zawrocen partii przy przeprawie (okno AimWindowHours), przy drugim - blokada celu AI. Zwraca dopisek do linii zawrocenia.</summary>
        private static string AimTurn(MobileParty mp, Settlement st)
        {
            var now = CampaignTime.Now;
            var ts = mp.TargetSettlement;
            Settlement aim = ts != null && ts != st ? ts : null;       // przeprawa sama - oblezenie legalne (SiegeBound i tak ja pomija)
            AimBlockRec b;
            if (aim != null && _aimBlock.TryGetValue(mp, out b) && b.Until > now && b.Targets.Contains(aim)) _dAimHit++;   // zablokowany cel mimo blokady (armia, eskorta) - licznik
            AimMem m;
            if (!_aimMem.TryGetValue(mp, out m) || m.At != st) { m = new AimMem { At = st }; _aimMem[mp] = m; }
            for (int i = m.Turns.Count - 1; i >= 0; i--) if (m.Turns[i].ElapsedHoursUntilNow > AimWindowHours) m.Turns.RemoveAt(i);
            m.Turns.Add(now);
            if (aim != null) m.LastAim = aim; else aim = m.LastAim;   // teraz bez celu (Hold, punkt odwrotu) - cel z poprzedniego zawrocenia
            if (m.Turns.Count < AimTurns) return null;
            m.Turns.Clear();                                           // nastepna blokada dopiero po kolejnych dwoch zawroceniach
            if (aim == null || aim == st) { _dAimNoTarget++; return "; cel AI nieznany - bez blokady (Q-3, A12)"; }
            if (!_aimBlock.TryGetValue(mp, out b) || b.Until <= now) { b = new AimBlockRec(); _aimBlock[mp] = b; _dAimParties++; }
            b.At = st; b.Until = CampaignTime.DaysFromNow(AimBlockDays);
            if (!b.Targets.Contains(aim))
            {
                if (b.Targets.Count >= AimTargetsMax) b.Targets.RemoveAt(0);
                b.Targets.Add(aim);
                _dAimNew++;
            }
            AimAny = true;
            return "; cel zablokowany na " + AimBlockDays.ToString("0", Inv) + " dob (Q-3, A12): " + aim.StringId + (_aimHooked ? "" : " - latka AI " + _aimHookNote);
        }

        /// <summary>Postfiks CampaignEventDispatcher.AiHourlyTick: wszystkie zachowania juz maja wynik, wybor najlepszego jeszcze nie zapadl (AiPartyThinkBehavior). Partia
        /// z blokada (wojna z panem przeprawy trwa) - wynik zachowan z zablokowana osada = -1 (wybor gry zaczyna od -1 i bierze tylko wiekszy - przy 0 i rozkazie Hold
        /// gra wykonalaby zachowanie z wynikiem 0). Nie rzuca (potkniecia liczone).</summary>
        public static void AimPostfix(MobileParty party, PartyThinkParams partyThinkParams)
        {
            if (!AimAny || party == null || partyThinkParams == null) return;
            try
            {
                AimBlockRec b;
                if (!_aimBlock.TryGetValue(party, out b) || b.Until <= CampaignTime.Now || !AimOn()) return;
                var owner = b.At != null ? b.At.MapFaction : null;
                var f = party.MapFaction;
                if (owner == null || f == null || f == owner || !FactionManager.IsAtWarAgainstFaction(owner, f)) return;
                var list = partyThinkParams.AIBehaviorScores;
                List<AIBehaviorData> hit = null;
                for (int i = 0; i < list.Count; i++)
                {
                    var e = list[i];
                    if (e.Item2 <= -1f) continue;                  // juz zablokowane (albo gra dala -1)
                    var tgt = e.Item1.Party as Settlement;
                    if (tgt == null || !b.Targets.Contains(tgt)) continue;
                    if (hit == null) hit = new List<AIBehaviorData>(2);
                    hit.Add(e.Item1);
                }
                if (hit == null) return;
                for (int i = 0; i < hit.Count; i++)
                {
                    var d = hit[i];
                    partyThinkParams.SetBehaviorScore(in d, -1f);   // ponizej startu wyboru gry (-1): nigdy najlepszy, takze gdy reszta ma 0 (Hold daje num2 = 1)
                    _dAimZeroed++;
                }
            }
            catch (Exception e) { Stumble("AimPostfix", e); }
        }

        /// <summary>Daily: blokady i pamiec zawrocen, ktore wygasly albo partia zniknela; odcinek linii "Straz przeprawy (E8/Q)" (tylko z kluczem).</summary>
        private static string AimDay()
        {
            var now = CampaignTime.Now;
            _aiDrop.Clear();
            foreach (var kv in _aimBlock) if (kv.Key == null || !kv.Key.IsActive || kv.Value.Until <= now) _aiDrop.Add(kv.Key);
            for (int i = 0; i < _aiDrop.Count; i++) _aimBlock.Remove(_aiDrop[i]);
            _aiDrop.Clear();
            foreach (var kv in _aimMem)
            {
                if (kv.Key == null || !kv.Key.IsActive) { _aiDrop.Add(kv.Key); continue; }
                var t = kv.Value.Turns;
                if (t.Count == 0 || t[t.Count - 1].ElapsedHoursUntilNow > AimWindowHours) _aiDrop.Add(kv.Key);
            }
            for (int i = 0; i < _aiDrop.Count; i++) _aimMem.Remove(_aiDrop[i]);
            _aiDrop.Clear();
            AimAny = _aimBlock.Count > 0;
            int targets = 0; foreach (var v in _aimBlock.Values) targets += v.Targets.Count;
            return " | zmiana celu AI (Q-3, A12): latka " + _aimHookNote + ", partii z blokada " + _aimBlock.Count + " (celow " + targets + "; nowe dzis: partii " + _dAimParties
                   + ", celow " + _dAimNew + "), wyniki zachowan AI zablokowane (-1) " + _dAimZeroed + ", zawrocone z zablokowanym celem " + _dAimHit
                   + ", drugie zawrocenie bez znanego celu " + _dAimNoTarget + " (blokada " + AimBlockDays.ToString("0", Inv) + " dob po " + AimTurns + " zawroceniach w " + AimWindowHours.ToString("0", Inv) + " h)";
        }

        /// <summary>drobne2 (Q-3, diagnoza): dokad AI prowadzi partie zawracana trzeci raz w dobie - zachowanie, osada celu (z frakcja), partia celu, armia.
        /// Bieg dwuletni: lord_5_16_party_1 3-4 zawrocenia na dobe przez 6 dob (d514-519) i znow d529-530; po przerwie 3 h AI wybiera ten sam cel za mostem.</summary>
        private static string AiAim(MobileParty mp)
        {
            try
            {
                var ts = mp.TargetSettlement; var tp = mp.TargetParty; var army = mp.Army;
                string tsf = "-";
                try { tsf = ts != null && ts.MapFaction != null && ts.MapFaction.Name != null ? ts.MapFaction.Name.ToString() : "-"; } catch { tsf = "?"; }
                return "; cel AI (Q-3, diagnoza drobne2): zachowanie " + mp.DefaultBehavior + ", osada " + (ts != null ? ts.StringId + " (" + tsf + ")" : "-")
                       + ", partia " + (tp != null ? tp.StringId : "-") + ", armia " + (army != null && army.LeaderParty != null ? army.LeaderParty.StringId + (army.LeaderParty == mp ? " (wodz)" : "") : "-");
            }
            catch { return "; cel AI: ?"; }
        }

        /// <summary>Partia idaca oblegac albo szturmowac TE przeprawe, albo idaca do wodza armii, ktory ja oblega lub idzie oblegac ("Oblezenie przeprawy jest
        /// LEGALNE"; sam BesiegerCamp to dopiero stan po dojsciu pod mury - bez tego straz zawracalaby kazde wojsko w drodze na oblezenie).</summary>
        private static bool SiegeBound(MobileParty mp, Settlement st)
        {
            if (Besieging(mp, st)) return true;
            var army = mp.Army;
            var lead = army != null ? army.LeaderParty : null;
            return lead != null && lead != mp && (lead.BesiegedSettlement == st || Besieging(lead, st));
        }

        private static bool Besieging(MobileParty p, Settlement st)
        {
            return p.TargetSettlement == st && (p.DefaultBehavior == AiBehavior.BesiegeSettlement || p.DefaultBehavior == AiBehavior.AssaultSettlement);
        }

        /// <summary>Licznik E7/B (TwinsCrossing.Cross): partia lordow zmienila brzeg przy Bliznakach - jesli jej strona wojuje z panem przeprawy, liczymy
        /// przejscie mimo strazy (osobno te w ciagu 3 h po zawroceniu: przerwa albo powrot zza mostu). Sam licznik, takze przy wylaczonej petli.</summary>
        internal static void NoteBridgeCrossed(MobileParty mp, Settlement st)
        {
            try
            {
                if (mp == null || st == null) return;
                var owner = st.MapFaction;
                var f = mp.MapFaction;
                if (owner == null || f == null || f == owner || !FactionManager.IsAtWarAgainstFaction(owner, f)) return;
                _dCross++;
                CampaignTime until;
                if (_aiCooldown.TryGetValue(mp, out until) && until > CampaignTime.Now) _dCrossCool++;
            }
            catch (Exception e) { Stumble("NoteBridgeCrossed", e); }
        }

        /// <summary>Dopisek do linii doby "Przeprawa Bliznaki (E7/B)" - czytany PRZED Daily(), ktore zeruje liczniki (kolejnosc w OnDailyTick).</summary>
        internal static string TwinsSuffix()
        {
            try
            {
                return " | straz przeprawy (E8/Q): zegar gry " + (ClockOn(Settings.Current) ? "TAK" : "NIE") + ", zawrocone przy Bliznakach " + _dTurnedAt[0]
                       + ", wrogich lordow przez most " + _dCross + " (w ciagu 3 h po zawroceniu " + _dCrossCool + ")";
            }
            catch { return ""; }
        }

        /// <summary>Linia "Straz przeprawy (E8/Q)" i zerowanie licznikow doby (OnDailyTick, zaraz po TwinsCrossing.Daily).</summary>
        internal static void Daily()
        {
            var s = Settings.Current;
            if (s == null) return;
            string head = "Straz przeprawy (E8/Q): dzien " + (int)CampaignTime.Now.ToDays;
            bool on = ClockOn(s);
            try
            {
                string bridge = " | Bliznaki (licznik E7/B): " + (TwinsCrossing.Counting ? "wrogich lordow przez most " + _dCross + " (w ciagu 3 h po zawroceniu " + _dCrossCool + ")"
                                                                                         : "nieczynny (Twins Crossing Count albo brak osi rzeki)");
                if (!on)
                {
                    Log.Info(head + " | WYLACZONE (" + (!s.CrossingLawEnabled ? "Crossing Law Enabled" : !s.CrossingLawAi ? "Crossing Law Ai"
                             : "Crossing Law Ai Clock - petla jak przed E8/Q: tylko gdy straz wypycha gracza") + ")" + bridge + ".");
                    return;
                }
                int most = 0, over3 = 0; string who = "-";
                foreach (var kv in _dayTurns)
                {
                    if (kv.Value > 3) over3++;
                    if (kv.Value > most) { most = kv.Value; who = kv.Key != null ? kv.Key.StringId : "?"; }
                }
                double ms = _ticks * 1000.0 / Stopwatch.Frequency;
                var sb = new StringBuilder(720);
                sb.Append(head).Append(" | zegar gry TAK (promien strefy ").Append(s.CrossingRadius.ToString("0.0", Inv)).Append(", pamiec drogi do ")
                  .Append((s.CrossingRadius + OuterRing).ToString("0.0", Inv)).Append(" jedn.; przeprawy ").Append(CrossingIds.Length - _dMissing).Append("/").Append(CrossingIds.Length).Append(")")
                  .Append(" | przebiegi ").Append(_dPasses).Append(", partii lordow przejrzanych ").Append(_dSeen.ToString(Inv))
                  .Append(" (srednio ").Append(_dPasses > 0 ? (_dSeen / _dPasses).ToString(Inv) : "0").Append(" na przebieg)")
                  .Append(", wrogich przy przeprawach ").Append(_dNear).Append(" partio-godzin, w strefie ").Append(_dZone)
                  .Append(" | zawrocone ").Append(_dTurned).Append(" (");
                for (int j = 0; j < CrossingIds.Length; j++) sb.Append(j > 0 ? ", " : "").Append(CrossingIds[j]).Append(" ").Append(_dTurnedAt[j]);
                sb.Append("; punkt odwrotu: skad przyszla ").Append(_dFromMemory).Append(" / prosto od zamku ").Append(_dTurned - _dFromMemory).Append(")")
                  .Append(" | w strefie bez rozkazu: spiacy ").Append(_dSleep).Append(", w przerwie 3 h ").Append(_dCool).Append(", oblezenie przeprawy ").Append(_dSiege)
                  .Append(", eskorty armii ").Append(_dEscort).Append(", w osadzie albo bitwie ").Append(_dBusy).Append(", na wodzie ").Append(_dSea)
                  .Append(", AI wylaczone ").Append(_dAiOff).Append(", bez punktu na ladzie ").Append(_dBadPoint)
                  .Append(" | najwiecej zawrocen jednej partii ").Append(most).Append(" (").Append(who).Append("), partii zawroconych ponad 3 razy ").Append(over3);
                string aimSeg = AimDay();                                   // A12 (Q-3): sprzatanie blokad zawsze, odcinek tylko z kluczem (wylaczony - linia jak dotad)
                if (s.CrossingLawAimBlock) sb.Append(aimSeg);
                sb.Append(bridge)
                  .Append(" | koszt ").Append(ms.ToString("0.000", Inv)).Append(" ms/dobe")
                  .Append(" | potkniecia dzis ").Append(_stumblesDay).Append(" (razem ").Append(_stumbles).Append(").");
                Log.Info(sb.ToString());
                // przerwy, ktore minely, i partie, ktorych juz nie ma (dotad dopiero powyzej 200 wpisow)
                if (_aiCooldown.Count > 0)
                {
                    _aiDrop.Clear();
                    foreach (var kv in _aiCooldown)
                        if (kv.Key == null || !kv.Key.IsActive || kv.Value <= CampaignTime.Now) _aiDrop.Add(kv.Key);
                    for (int i = 0; i < _aiDrop.Count; i++) _aiCooldown.Remove(_aiDrop[i]);
                    _aiDrop.Clear();
                }
                _aiShoutStock = 3;   // pula meldunkow na dobe (stara petla odswieza ja o 12:00)
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally { DayReset(); }
        }
    }
}
