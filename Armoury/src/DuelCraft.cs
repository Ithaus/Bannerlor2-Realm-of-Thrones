using System;
using System.Collections.Generic;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// m111-walka faza 1 (24.10, Jeff: "jak atakuje caly czas, przeciwnik sie tylko broni"): ZRYWY W POJEDYNKU (klucz DuelPressureBursts).
    ///
    /// Dowod [K]: RBM odswieza wspolczynniki AI co 3 s (StanceLogic.cs:1923-1932), a zaden kod AI nie czyta postury - przeciwnik nie reaguje ani na
    /// zlamana garde gracza, ani na to, ze sam stoi pod gradem ciosow; jedyna droga to wydarzenie ciosu -> chwilowa zmiana wspolczynnikow -> odswiezenie.
    ///
    /// Zasady:
    ///  - OTWARCIE: postura atakujacego peka na obronie AI (DuelGuard, hak handleAttacker), obronca ma poziom >= 0.40 - przez 1.2 s atak po decyzji
    ///    0.90, decyzja o ataku 0.10, bez trzymania zamachu;
    ///  - NACISK: AI obronilo sie 3 razy w 4 s albo po kontakcie ma postury ponizej 40% maksimum, poziom >= 0.56 - przez 1.5 s atak po decyzji
    ///    co najmniej 0.60, decyzja o ataku x0.6, atak po parowaniu co najmniej 0.80, kopniecie 0.45, chamber 0.20, trzymanie 0.05 s; potem 3 s przerwy
    ///    (tylko dla nacisku - otwarcie startuje zawsze i przykrywa trwajacy nacisk);
    ///  - nakladke robi DuelAi.Apply (postfiks SetAiRelatedProperties); start i koniec zrywu - agent.UpdateAgentStats() TYLKO z OnMissionTick
    ///    (BattleSense, glowny watek), najwyzej 8 na klatke, nigdy z wnetrza wywolania ciosu;
    ///  - stan bez blokad dla czytelnika: tablice wedlug agent.Index, ROSNA tylko w ticku (glowny watek) przez podmiane referencji; postfiks moze
    ///    biec w ticku asynchronicznym druzyn i tylko czyta. Zgloszenia z hakow ciosu ida do kolejki pod wlasna blokada.
    /// Wyjatek liczony (RoutHold.Stumble, miejsce "pojedynek").
    /// </summary>
    internal static class DuelCraft
    {
        internal const int None = 0, Opening = 1, Pressed = 2;
        internal const float OpeningSeconds = 1.2f, PressedSeconds = 1.5f, Cooldown = 3f;
        internal const float RingWindow = 4f;          // 3 obrony w 4 s
        internal const float PressedPostureFrac = 0.40f;
        internal const int PerTick = 8;                // najwyzej tyle UpdateAgentStats na klatke
        internal const float SwingAfter = 1.2f;        // zryw "z atakiem": AI zaczelo zamach do 1.2 s po starcie zrywu (probkowanie DuelGuard)
        // m113-walka2 po recenzji: w bitwie wyzwalacze strzelaja niemal stale ("3 obrony w 4 s" i postura < 40% to zwykly stan bohatera w tloku) -
        // tylko w bitwie: przerwa nacisku 6 s (nakladka najwyzej ok. 20% czasu zamiast ok. 1/3), otwarcie w trakcie otwarcia odrzucone (bez nowego
        // startu i odswiezenia), po otwarciu 2 s przerwy; pojedynek i turniej bez zmian
        internal const float FieldPressedCooldown = 6f, FieldOpeningCooldown = 2f;

        // ---- czytane przez postfiks (bez blokad); podmieniane tylko w ticku
        private static float[] _until = new float[0];
        private static byte[] _kind = new byte[0];
        // ---- tylko glowny watek (tick)
        private static float[] _cool = new float[0];
        private static float[] _coolOpen = new float[0];   // m113-walka2: przerwa po otwarciu (tylko bitwa)
        private static float[] _started = new float[0];
        private static readonly List<Agent> _active = new List<Agent>();
        private static readonly List<Agent> _refresh = new List<Agent>();

        // ---- kolejka zgloszen i pierscien obron (haki ciosu) - pod _lock
        private struct Req { public Agent A; public int Kind; }
        private static readonly object _lock = new object();
        private static readonly Req[] _queue = new Req[64];
        private static readonly Req[] _work = new Req[64];
        private static int _qn;
        private static float[] _ring = new float[0];   // 3 ostatnie obrony na agenta

        // ---- liczniki misji (glowny watek; czytane przy linii DU)
        internal static int StartedOpening, StartedPressed, Dropped, Cooled, Refreshed, WithSwing;

        internal static void Reset()
        {
            lock (_lock)
            {
                _qn = 0;
                Array.Clear(_queue, 0, _queue.Length);
                for (int i = 0; i < _ring.Length; i++) _ring[i] = -100f;   // "brak obrony" (0 udawaloby obrone w pierwszych 4 s misji)
            }
            Array.Clear(_until, 0, _until.Length);
            Array.Clear(_kind, 0, _kind.Length);
            Array.Clear(_cool, 0, _cool.Length);
            Array.Clear(_coolOpen, 0, _coolOpen.Length);
            for (int i = 0; i < _started.Length; i++) _started[i] = -1f;   // -1 = "bez zrywu" jak w Grow (0 liczylo zamachy z pierwszych 1.2 s misji)
            _active.Clear();
            _refresh.Clear();
            StartedOpening = StartedPressed = Dropped = Cooled = Refreshed = WithSwing = 0;
        }

        /// <summary>Postfiks (dowolny watek): rodzaj trwajacego zrywu.</summary>
        internal static int BurstOf(int idx, float now)
        {
            var u = _until;
            var k = _kind;
            if (idx < 0 || idx >= u.Length || idx >= k.Length) return None;
            return now < u[idx] ? k[idx] : None;
        }

        /// <summary>Probkowanie (glowny watek): czas startu ostatniego zrywu agenta albo -1.</summary>
        internal static float StartedAt(int idx)
        {
            var s = _started;
            return idx >= 0 && idx < s.Length ? s[idx] : -1f;
        }

        /// <summary>Hak ciosu: zgloszenie zrywu (wykona tick).</summary>
        internal static void Request(Agent a, int kind)
        {
            if (a == null) return;
            lock (_lock)
            {
                if (_qn < _queue.Length) _queue[_qn++] = new Req { A = a, Kind = kind };
                else Dropped++;
            }
        }

        /// <summary>Hak ciosu (obrona AI): pierscien 3 ostatnich obron; true = 3 obrony w 4 s.</summary>
        internal static bool NoteDefence(int idx, float now)
        {
            if (idx < 0) return false;
            lock (_lock)
            {
                int need = (idx + 1) * 3;
                if (_ring.Length < need)
                {
                    var n = new float[Math.Max(need, _ring.Length * 2 + 48)];
                    for (int i = 0; i < n.Length; i++) n[i] = -100f;
                    Array.Copy(_ring, n, _ring.Length);
                    _ring = n;
                }
                int b = idx * 3;
                // najstarsza wypada (pierscien jako przesuniecie - 3 pola)
                _ring[b] = _ring[b + 1];
                _ring[b + 1] = _ring[b + 2];
                _ring[b + 2] = now;
                return _ring[b] > -50f && now - _ring[b] <= RingWindow;
            }
        }

        private static void Grow(int idx)
        {
            if (idx < _until.Length && idx < _kind.Length && idx < _cool.Length && idx < _coolOpen.Length && idx < _started.Length) return;
            int n = Math.Max(idx + 1, _until.Length * 2 + 32);
            var u = new float[n]; Array.Copy(_until, u, _until.Length);
            var k = new byte[n]; Array.Copy(_kind, k, _kind.Length);
            var c = new float[n]; Array.Copy(_cool, c, _cool.Length);
            var co = new float[n]; Array.Copy(_coolOpen, co, _coolOpen.Length);
            var s = new float[n]; for (int i = _started.Length; i < n; i++) s[i] = -1f; Array.Copy(_started, s, _started.Length);
            // kolejnosc: najpierw rodzaj, potem czas (czytelnik bierze obie referencje osobno - najwyzej jedno odswiezenie bez zrywu)
            _kind = k;
            _until = u;
            _cool = c;
            _coolOpen = co;
            _started = s;
        }

        /// <summary>BattleSense.OnMissionTick (glowny watek): zgloszenia -> zrywy, konce zrywow, odswiezenie najwyzej 8 agentow.
        /// field - bitwa (m113-walka2): przerwy FieldPressedCooldown / FieldOpeningCooldown, otwarcie w trakcie otwarcia odrzucone.</summary>
        internal static void Tick(Mission m, bool field = false)
        {
            if (m == null) return;
            float now = m.CurrentTime;
            int n;
            lock (_lock)
            {
                n = _qn;
                Array.Copy(_queue, _work, n);
                Array.Clear(_queue, 0, n);
                _qn = 0;
            }
            for (int i = 0; i < n; i++)
            {
                var a = _work[i].A;
                int kind = _work[i].Kind;
                _work[i].A = null;
                try
                {
                    if (a == null || !a.IsActive() || a.Mission != m) continue;
                    int idx = a.Index;
                    if (idx < 0) continue;
                    Grow(idx);
                    // przerwa 3 s tylko dla NACISKU (plan pkt 3); OTWARCIE zawsze startuje (1.2 s) i przykrywa trwajacy nacisk - po recenzji: RBM wola
                    // handleDefender przed handleAttacker (StanceLogic.cs:523/527), wiec ten sam cios dawal [nacisk, otwarcie] i otwarcie ginelo w przerwie
                    if (kind == Pressed)
                    {
                        if (now < _cool[idx]) { Cooled++; continue; }
                        if (now < _until[idx] && _kind[idx] == Opening) continue;   // trwa otwarcie - nacisk poczeka
                    }
                    else if (field)
                    {
                        // bitwa: kazde zlamanie gardy atakujacego na bohaterze w tloku dawalo nowe otwarcie - trwajace nie startuje od nowa (bez
                        // licznika i odswiezenia), po koncu 2 s przerwy
                        if (now < _until[idx] && _kind[idx] == Opening) continue;
                        if (now < _coolOpen[idx]) { Cooled++; continue; }
                    }
                    float dur = kind == Opening ? OpeningSeconds : PressedSeconds;
                    _kind[idx] = (byte)kind;
                    _until[idx] = now + dur;
                    _started[idx] = now;
                    if (kind == Pressed) { _cool[idx] = now + dur + (field ? FieldPressedCooldown : Cooldown); StartedPressed++; }
                    else { if (field) _coolOpen[idx] = now + dur + FieldOpeningCooldown; StartedOpening++; }
                    if (!_active.Contains(a)) _active.Add(a);
                    if (!_refresh.Contains(a)) _refresh.Add(a);
                }
                catch (Exception e) { RoutHold.Stumble(RoutHold.SiteDuel, "DuelCraft.Tick", e); }
            }
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var a = _active[i];
                try
                {
                    int idx = a != null ? a.Index : -1;
                    bool gone = a == null || !a.IsActive() || a.Mission != m || idx < 0 || idx >= _until.Length;
                    if (gone || now >= _until[idx])
                    {
                        _active.RemoveAt(i);
                        if (!gone && !_refresh.Contains(a)) _refresh.Add(a);
                    }
                }
                catch (Exception e) { _active.RemoveAt(i); RoutHold.Stumble(RoutHold.SiteDuel, "DuelCraft.Tick koniec", e); }
            }
            int done = 0;
            while (_refresh.Count > 0 && done < PerTick)
            {
                var a = _refresh[0];
                _refresh.RemoveAt(0);
                try
                {
                    if (a != null && a.IsActive() && a.Mission == m) { a.UpdateAgentStats(); Refreshed++; done++; }
                }
                catch (Exception e) { RoutHold.Stumble(RoutHold.SiteDuel, "DuelCraft.UpdateAgentStats", e); }
            }
        }
    }
}
