using System;
using System.Text;
using System.Threading;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 157 P3 (26.10) "kontra 1-2 jezdzcow na strzelca (20 s)" - WYCOFANA w 204 (26.10, Jeff ok. 22:15 po bitwie z WGRANIEM 126: "jezdzcy widza ze do nich
    /// strzelam to nie stoja jak kretyni tylko jada w moim kierunku ... za malo sa agresywni maja oslaniac i pilnowac i bronic sie"). Kontre zastepuje
    /// "Jazda na strzelca (204)" - HorsePursuit.Hunt.cs (wykrycie z kazdego pocisku konnego strzelca, takze pudla i strzaly w piechote; odpowiedz oddzialem,
    /// calym szykiem albo cala jazda). Ten plik trzyma juz tylko:
    ///  - wspolne API "jezdziec pod naszym sterowaniem" (tablica _countering wedlug Agent.Index, pisze tylko glowny watek): teraz jezdzcy 204 w poscigu za
    ///    strzelcem (rola wrecz - pozycja skryptowa). Wolajacy (zasada 05.10 - wszyscy sprawdzeni): MarchToContact.PostSpeed (cap marszu pominiety),
    ///    ShieldsUp.One (bron i preferencja zamrozone - Countering), HorsePursuit.RidersPass (148 nie bierze jezdzca 204), BannerCompanies.Pickup (biegacz
    ///    sztandaru: Fit i przywrocenie celu - HuntAnyThread);
    ///  - pomocnicze FootNear / Home (strzelec przy nietknietym szyku pieszym swojej strony; miejsce szyku);
    ///  - wejscie OnRiderShot z ShieldsUp.RiderHit - juz tylko licznik "przejete przez 204" (trafienia jezdzcow, w tym w szarzy szyku) do segmentu linii 148.
    /// Klucz RidersRunDownShooters jest teraz kluczem 204.
    /// </summary>
    internal static partial class HorsePursuit
    {
        // ------------------------------------------------------------ stale (157, uzywane przez 204)
        internal const float CounterFootGap = 15f;    // strzelec blizej niz tyle od nietknietego szyku pieszego swojej strony - jezdzcy nie wjezdzaja w wlocznie (204: zaslona)
        internal const float CounterCool = 30f;       // przerwa jezdzca po poscigu za strzelcem (204: odpoczynek przed ponownym wyslaniem)

        // dla latki PostSpeed (watki robocze): jezdziec 204 w poscigu za strzelcem wedlug Agent.Index; pisze tylko glowny watek (wzor _riding)
        private static Agent[] _countering;

        // liczniki misji (glowny watek; _cCapSkip przez Interlocked)
        private static int _cShots, _cShotsCharge;
        private static int _cCapSkip;

        // ------------------------------------------------------------ dla innych paczek

        /// <summary>MarchToContact.PostSpeed (watki robocze): czlowiek jest jezdzcem 204 w poscigu za strzelcem (pozycja skryptowa) - bez capu marszu /
        /// odpoczynku. Bez blokady.</summary>
        internal static bool CounterAnyThread(Agent a)
        {
            if (a == null) return false;
            var k = Volatile.Read(ref _countering);
            if (k == null) return false;
            int i = a.Index;
            return i >= 0 && i < k.Length && ReferenceEquals(k[i], a);
        }

        /// <summary>MarchToContact.PostSpeed: cap marszu pominiety u jezdzca 204 (licznik linii 204).</summary>
        internal static void NoteCounterCapSkip() { Interlocked.Increment(ref _cCapSkip); }

        /// <summary>ShieldsUp.One (glowny watek): jezdziec teraz w poscigu 204 za strzelcem (bron i preferencja zamrozone).</summary>
        internal static bool Countering(Agent a)
        {
            Hunter h;
            return a != null && Hunters.TryGetValue(a, out h) && h.Role != RoleRanged;
        }

        private static void MarkCounter(Agent a, bool on)
        {
            MarkIn(ref _countering, a, on);
        }

        /// <summary>Tablica wedlug Agent.Index (wzor MarkRiding): powieksza tylko glowny watek, czytelnicy przez Volatile.Read.</summary>
        private static void MarkIn(ref Agent[] arr, Agent a, bool on)
        {
            if (a == null) return;
            int i = a.Index;
            if (i < 0 || i >= RidingMaxIndex) return;
            var k = arr;
            if (k == null || i >= k.Length)
            {
                if (!on) return;
                int n = k == null ? 1024 : k.Length;
                while (n <= i) n *= 2;
                var nk = new Agent[n];
                if (k != null) Array.Copy(k, nk, k.Length);
                nk[i] = a;
                Volatile.Write(ref arr, nk);
                return;
            }
            if (on) k[i] = a;
            else if (ReferenceEquals(k[i], a)) k[i] = null;
        }

        private static int CountIn(Agent[] k)
        {
            if (k == null) return 0;
            int n = 0;
            for (int i = 0; i < k.Length; i++) if (k[i] != null) n++;
            return n;
        }

        private static void CounterClear()
        {
            Volatile.Write(ref _countering, null);
            _cShots = _cShotsCharge = 0;
            Interlocked.Exchange(ref _cCapSkip, 0);
        }

        // ------------------------------------------------------------ wejscie (ShieldsUp.RiderHit, glowny watek)

        /// <summary>Jezdziec AI z szyku konnicy AI trafiony pociskiem (on albo kon) przez strzelca s; charging = szyk w szarzy (ShieldsUp.FormCharging).
        /// 204: tylko licznik - wykrycie i odpowiedz robi HorsePursuit.OnFire (ta sama kolejka 140, kazde trafienie, pudlo i przelot).</summary>
        internal static void OnRiderShot(Mission m, Agent a, Agent s, bool horse, bool charging)
        {
            if (!_live || a == null || m == null) return;
            var w = _m;
            if (w == null || !ReferenceEquals(w.Target, m)) return;
            _cShots++;
            if (charging) _cShotsCharge++;
        }

        /// <summary>Miejsce szyku: punkt rozkazu (oslona skrzydla - punkt trzymania), bez niego srodek szyku.</summary>
        private static Vec2 Home(Formation f)
        {
            Vec2 p = Vec2.Invalid;
            try { p = f.OrderPosition; } catch { p = Vec2.Invalid; }
            if (p.IsValid && (p.x != 0f || p.y != 0f)) return p;
            return f.CachedAveragePosition;
        }

        /// <summary>Strzelec blizej niz CounterFootGap od nietknietego szyku pieszego swojej strony (szyk od IntactMin ludzi, nie konnica ani konni lucznicy;
        /// odleglosc od mediany minus promien szyku, najwyzej 40 m - jak IntactNear 148).</summary>
        private static bool FootNear(Mission m, Agent s)
        {
            var st = s.Team;
            if (st == null) return false;
            Vec2 p = s.Position.AsVec2;
            var teams = m.Teams;
            int nt = teams != null ? teams.Count : 0;
            for (int i = 0; i < nt; i++)
            {
                Team t;
                try { t = teams[i]; } catch { continue; }
                if (!MissionTeams.Valid(t) || t.Side != st.Side) continue;
                var fl = t.FormationsIncludingSpecialAndEmpty;
                int nf = fl != null ? fl.Count : 0;
                for (int j = 0; j < nf; j++)
                {
                    var f = fl[j];
                    if (f == null || f.CountOfUnits < IntactMin) continue;
                    var q = f.QuerySystem;
                    if (q == null || q.IsCavalryFormation || q.IsRangedCavalryFormation) continue;
                    var mp = f.CachedMedianPosition;
                    if (!mp.IsValid) continue;
                    float r = Math.Min(RadiusCap, 0.5f * Math.Max(f.Width, f.Depth));
                    if (p.Distance(mp.AsVec2) - r < CounterFootGap) return true;
                }
            }
            return false;
        }

        // ------------------------------------------------------------ segment linii "poscig (148)"

        private static void AppendCounter(StringBuilder sb)
        {
            sb.Append(" | kontra na strzelca (157): zastapiona przez 204 (linia 'Jazda na strzelca (204)') - trafien jezdzcow AI ").Append(_cShots)
              .Append(" (szyk w szarzy ").Append(_cShotsCharge).Append("), przejete przez 204");
        }
    }
}
