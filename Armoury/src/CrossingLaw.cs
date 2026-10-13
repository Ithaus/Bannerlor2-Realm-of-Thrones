using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
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
        private static int _dCross, _dCrossCool, _dEventLines, _dMissing;
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
            _dCross = _dCrossCool = _dEventLines = _dMissing = 0;
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
            if (_dEventLines < EventLinesMax)
            {
                _dEventLines++;
                Log.Info("CrossingLaw: AI " + mp.StringId + " zawrocone od " + st.StringId + " (E8/Q, zegar gry; " + (fromMem ? "tam, skad przyszla" : "prosto od zamku")
                         + ", " + Math.Sqrt(d2).ToString("0.0", Inv) + " jedn. od przeprawy, zawrocenie nr " + (c + 1) + " tej doby, cooldown 3h).");
            }
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
                  .Append(" | najwiecej zawrocen jednej partii ").Append(most).Append(" (").Append(who).Append("), partii zawroconych ponad 3 razy ").Append(over3)
                  .Append(bridge)
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
