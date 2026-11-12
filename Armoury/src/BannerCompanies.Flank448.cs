using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 448 (audyt AI bitwy 11.11, pakiet 6 kroki 6.1 + 6.2; Jeff 11.11 nr 6: "Twoj oddzial dostaje cios w plecy - ma sie lamac latwiej, tak samo oddzialy wroga").
    /// Klucz RearAndFlankHitsBreakCompanies (MCM "Rear And Flank Hits Break Companies", domyslnie TAK). Trzy nowe zrodla rzutu na zalamanie kompanii
    /// (BannerCompaniesMission.Roll): 7 TYL, 8 BOK, 9 SASIAD - kazde jako zdarzenie (evt: M z udzialu strat, bez bramki morale gry) z wlasnym dolnym progiem
    /// M >= DirMinM (1.0 = jak kompania po stracie polowy skladu). Reszta wzoru bez zmian: szansa BannerBreakChance x F x M x T(tier) x L(dowodztwo) x B(stan bitwy).
    ///  Kierunek liczony na CZLOWIEKU: wektor ofiara -> napastnik (pocisk: odwrotnosc kierunku lotu z Blow.Direction; 448-p: jezdziec w ruchu od CoRide m/s -
    ///    odwrotnosc kierunku jazdy konia, skad nadjechal) wobec LookDirection ofiary w chwili przetworzenia (ta sama albo nastepna klatka). cos >= FrontCos
    ///    przod, cos <= RearCos tyl, reszta bok. 448-p: bok / tyl wedlug czlowieka sprawdzany drugi raz wobec sredniego patrzenia ludzi kompanii (gdy dlugosc
    ///    sredniej >= CoMeanMin - kompania patrzy w jedna strone); liczy sie lagodniejszy wynik - czlowiek obrocony w zwarciu do innego wroga z tej samej linii
    ///    nie daje BOK. Nie z kierunku szyku - czworobok 446 i zawrocona linia licza sie dobrze (kazda kompania patrzy w swoja strone).
    ///  Prog (przeciw falszywemu TYL od jazdy przejezdzajacej przez szyk): wynik kompanii gasnie wykladniczo (Tau s), cios pieszego 1, pocisk i cios jezdzca 0.5; rzut, gdy
    ///    wynik >= RearAt (tyl) albo FlankAt (bok) razy max(1, ludzie w szeregu / SizeRef) (448-p: duza kompania lapie wiecej ciosow przy tym samym udziale) i
    ///    trafilo co najmniej MinFoes ROZNYCH wrogow; potem wynik zerowany i przerwa Cooldown s dla tego zrodla.
    ///    Jezdziec trafiony z boku nie liczy sie do BOK (szarza przez szyk), z tylu tak.
    ///  SASIAD: zalamanie kompanii (Break, kazde zrodlo) - kazda niezlamana kompania tej samej strony, ktorej srodek jest w NbRange m od srodka zlamanej albo
    ///    ktorej czlowiek stoi w NbTouch m od czlowieka zlamanej (448-p: styk szeregow zamiast numeru Index +-1, ktory nie mowi nic o polozeniu), rzuca raz
    ///    po NbDelay s (widzi ucieczke), przerwa Cooldown s. 448-p (tlumienie lawiny): pokolenie - sasiedzi zalamania z innego zrodla to pokolenie 1 (F = NbF),
    ///    sasiedzi zalamania z rzutu SASIAD pokolenia g - pokolenie g+1 z F = NbF x NbDecay^g; zalamanie z pokolenia NbMaxGen nie kolejkuje nikogo (NbCut448).
    ///    Bez wstrzasu morale: gra sama zabiera morale sasiadom uciekajacego (Mission.OnAgentFleeing / AgentMoraleInteractionLogic). 448-p: rzut SASIAD nie
    ///    czyta morale gry, ale przy kluczu BannerEventRollByLoss NIE albo BannerLossBreakByShare NIE inne rzuty kompanii czytaja morale (bramka 50, M z morale)
    ///    i spadek morale po ucieczce sasiada juz je podbija - wtedy SASIAD pominiety (NbMorale448), jedno zjawisko jedna droga.
    ///  Kto ofiara: czlowiek w niezlamanej kompanii, nie bohater, nie w PANICE (SurvivalMode.InPanic), nie w odwrocie rannego 143 ani w zbiorce 359, nie uciekajacy; napastnik -
    ///    wrog (MissionTeams.Enemies; kon -> jego jezdziec). Obie strony jednakowo, takze wojsko gracza.
    /// Watki: OnAgentHit bywa wolany z ticku asynchronicznego - tam tylko kopia do kolejki pod zamkiem (wzor 313d, Bleed313.Enqueue); _mem, wyniki, rzuty -
    /// wylacznie w OnMissionTick (glowny watek), kolejka oprozniana co klatke. Klucz NIE: zadnego rzutu ani MBRandom - tylko pomiar ("bylby przy NIE").
    /// Wyjatek na trafienie / kompanie liczony (Stumbles448 + BannerCompanies.Stumble) - bez globalnego wylacznika.
    /// Linia: segment "ciosy z tylu i z boku (448)" w "Choragwie (CH) koniec" (przed segmentem 318c i potknieciami).
    /// </summary>
    internal static partial class BannerCompanies
    {
        internal const int SrcRear = 7, SrcFlank = 8, SrcNb = 9;
        internal const float DirMinM = 1.0f;                       // dolny prog M zdarzen 448 (2 x udzial strat ponizej 0.5 -> 1.0)
        internal const float FrontCos = 0.34f, RearCos = -0.5f;    // przod do ok. 70 st. od patrzenia, tyl od 120 st.
        internal const float Tau = 5f, RearAt = 3f, FlankAt = 5f, MissileW = 0.5f, Cooldown = 15f;
        internal const int MinFoes = 2, QueueMax448 = 4096;
        internal const float NbRange = 25f, NbDelay = 1f;
        internal const float RearF = 1f, FlankF = 0.7f, NbF = 0.6f;
        // 448-p (recenzja 448): styk szeregow, tlumienie lawiny SASIAD, prog wedlug wielkosci kompanii, kierunek wobec kompanii i z ruchu jezdzca
        internal const float NbTouch = 8f, NbDecay = 0.5f, SizeRef = 20f, CoMeanMin = 0.5f, CoRide = 2f;
        internal const int NbMaxGen = 2;

        // ---- liczniki 448 (glowny watek; zerowane w ResetCounters)
        // HitDir448: 0..2 wrecz przod / bok / tyl, 3..5 pociski przod / bok / tyl
        internal static readonly int[] HitDir448 = new int[6];
        // indeks 0 TYL, 1 BOK, 2 SASIAD: prog osiagniety (TAK - rzut), rzut odbyty, bylby przy NIE
        internal static readonly int[] Trig448 = new int[3], Roll448 = new int[3], Would448 = new int[3];
        internal static int HitSkip448, RiderFlank448, NbSkip448, Stumbles448;
        internal static int HitLost448;   // przepelnienie kolejki - przepisywane z misji przy linii konca
        // 448-p: bok/tyl czlowieka uznany za przod wedlug patrzenia kompanii, kierunek jezdzca z ruchu konia, prog podniesiony wielkoscia (wynik ponizej),
        // zalamania bez dalszego lancucha (pokolenie NbMaxGen), SASIAD pominiety przy rzutach z morale gry
        internal static int ToFront448, RideDir448, SizeHeld448, NbCut448, NbMorale448;

        internal static void Reset448()
        {
            Array.Clear(HitDir448, 0, HitDir448.Length);
            Array.Clear(Trig448, 0, 3); Array.Clear(Roll448, 0, 3); Array.Clear(Would448, 0, 3);
            HitSkip448 = RiderFlank448 = NbSkip448 = Stumbles448 = HitLost448 = 0;
            ToFront448 = RideDir448 = SizeHeld448 = NbCut448 = NbMorale448 = 0;
        }

        /// <summary>448: segment linii "Choragwie (CH) koniec" (bez znaku '|' w srodku segmentu poza separatorem na poczatku).</summary>
        internal static void Append448(StringBuilder sb, Settings c)
        {
            sb.Append(" | ciosy z tylu i z boku (448) ").Append(c != null && c.RearAndFlankHitsBreakCompanies ? "TAK" : "NIE")
              .Append(": trafienia wedlug kierunku - wrecz przod ").Append(HitDir448[0]).Append(", bok ").Append(HitDir448[1]).Append(", tyl ").Append(HitDir448[2])
              .Append("; pociski przod ").Append(HitDir448[3]).Append(", bok ").Append(HitDir448[4]).Append(", tyl ").Append(HitDir448[5])
              .Append("; pominiete (poza kompania, zlamani, panika, ranni, zbiorka, bohater, nie wrog) ").Append(HitSkip448)
              .Append(", jezdzcy trafieni z boku ").Append(RiderFlank448).Append(", poza kolejka ").Append(HitLost448)
              .Append("; rzutow TYL ").Append(Roll448[0]).Append(" (prog ").Append(Trig448[0]).Append("), BOK ").Append(Roll448[1]).Append(" (prog ").Append(Trig448[1])
              .Append("), SASIAD ").Append(Roll448[2]).Append(" (prog ").Append(Trig448[2]).Append(", juz zlamany lub przerwa ").Append(NbSkip448).Append(')')
              .Append("; zalamania wedlug zrodla TYL ").Append(BreakBySrc[SrcRear]).Append(", BOK ").Append(BreakBySrc[SrcFlank]).Append(", SASIAD ").Append(BreakBySrc[SrcNb])
              .Append("; przod wedlug kompanii ").Append(ToFront448).Append(", kierunek z jazdy konia ").Append(RideDir448).Append(", prog wielkosci trzymal ").Append(SizeHeld448)
              .Append(", lancuch uciety ").Append(NbCut448).Append(", SASIAD pominiety (rzuty z morale gry) ").Append(NbMorale448)
              .Append("; bylby przy NIE TYL ").Append(Would448[0]).Append(", BOK ").Append(Would448[1]).Append(", SASIAD ").Append(Would448[2])
              .Append("; potkniecia 448 ").Append(Stumbles448);
        }
    }

    internal sealed partial class BannerCompaniesMission
    {
        /// <summary>448: stan kompanii - wynik trafien (0 TYL, 1 BOK), czas ostatniego dopisania, rozni napastnicy, przerwy po rzucie (0 TYL, 1 BOK, 2 SASIAD).</summary>
        private sealed class Dir448
        {
            public readonly float[] Score = new float[2], At = new float[2];
            public readonly List<Agent>[] Foes = { new List<Agent>(4), new List<Agent>(4) };
            public readonly float[] NextOk = { -100f, -100f, -100f };
        }

        private struct Hit448
        {
            public Agent V, A;
            public bool Missile;
            public Vec3 Dir;
        }

        private struct Nb448
        {
            public Company Co;
            public float Due;
            public int Gen;   // 448-p: pokolenie lancucha (1 = sasiad zalamania z innego zrodla)
        }

        private readonly object _hq = new object();
        private List<Hit448> _hIn = new List<Hit448>(64), _hWork = new List<Hit448>(64);
        private int _hLost;                       // pod _hq
        private volatile bool _hOn;               // glowny watek pisze (rozstawienie / koniec), OnAgentHit czyta
        private readonly List<Nb448> _nb = new List<Nb448>();
        private readonly List<Company> _nbHit = new List<Company>();
        private readonly List<Vec2> _nbPos = new List<Vec2>();
        private int _nbGen;                       // 448-p: pokolenie rzutu SASIAD w toku (0 = rzut z innego zrodla) - glowny watek

        /// <summary>448: dowolny watek - tylko kopia do kolejki pod zamkiem. Czlowiek, cios z obrazeniami, nie blok tarcza, nie cios od siebie.</summary>
        public override void OnAgentHit(Agent affectedAgent, Agent affectorAgent, in MissionWeapon affectorWeapon, in Blow blow, in AttackCollisionData attackCollisionData)
        {
            if (!_hOn) return;
            try
            {
                if (affectedAgent == null || affectorAgent == null || !affectedAgent.IsHuman) return;
                if (blow.InflictedDamage <= 0 || attackCollisionData.AttackBlockedWithShield) return;
                if (blow.OwnerId == affectedAgent.Index || ReferenceEquals(affectorAgent, affectedAgent)) return;
                var h = new Hit448 { V = affectedAgent, A = affectorAgent, Missile = blow.IsMissile, Dir = blow.Direction };
                lock (_hq)
                {
                    if (_hIn.Count < BannerCompanies.QueueMax448) _hIn.Add(h);
                    else _hLost++;
                }
            }
            catch { }   // dowolny watek - bez Stumble (RoutHold.Stumble pisze log); utracone trafienie nie zmienia stanu
        }

        /// <summary>448: wlaczenie kolejki po rozstawieniu (glowny watek) - takze przy kluczu NIE (pomiar "bylby").</summary>
        private void Start448(Settings c)
        {
            _hOn = _field && c != null && c.BannerCompaniesEnabled;
        }

        private void Clear448()
        {
            _hOn = false;
            lock (_hq) { _hIn.Clear(); _hWork.Clear(); }
            _nb.Clear(); _nbHit.Clear(); _nbPos.Clear(); _nbGen = 0;
        }

        /// <summary>448: koniec misji - przepelnienie kolejki do licznika linii.</summary>
        private void End448()
        {
            _hOn = false;
            lock (_hq) { BannerCompanies.HitLost448 = _hLost; _hLost = 0; }
        }

        /// <summary>448 (glowny watek, co klatke): kolejka trafien i zalegle rzuty sasiadow.</summary>
        private void Tick448(Mission m)
        {
            var c = Settings.Current;
            if (c == null || !c.BannerCompaniesEnabled) { lock (_hq) { _hIn.Clear(); } _nb.Clear(); return; }
            float now = m.CurrentTime;
            bool on = c.RearAndFlankHitsBreakCompanies;
            List<Hit448> work;
            lock (_hq)
            {
                work = _hIn;
                _hIn = _hWork;
                _hWork = work;
            }
            for (int i = 0; i < work.Count; i++)
            {
                try { OneHit(work[i], now, on, c); }
                catch (Exception e) { BannerCompanies.Stumbles448++; BannerCompanies.Stumble("BannerCompaniesMission.Hit448", e); }
            }
            work.Clear();
            for (int i = _nb.Count - 1; i >= 0; i--)
            {
                var x = _nb[i];
                if (now < x.Due) continue;
                _nb.RemoveAt(i);
                try { NbDue(x.Co, x.Gen, now, on, c); }
                catch (Exception e) { BannerCompanies.Stumbles448++; BannerCompanies.Stumble("BannerCompaniesMission.Nb448", e); }
            }
        }

        private void OneHit(Hit448 h, float now, bool on, Settings c)
        {
            var v = h.V;
            Company co;
            if (v == null || !v.IsActive() || !_mem.TryGetValue(v, out co) || co.Broken || v.IsHero || v.IsRunningAway || SurvivalMode.InPanic(v))
            { BannerCompanies.HitSkip448++; return; }
            var k = RoutHold.Live;
            if (k != null && (k.Withdrawing(v) || k.InRally(v))) { BannerCompanies.HitSkip448++; return; }   // ranny 143, zawrocony w zbiorce 359
            var a = h.A;
            if (a != null && a.IsMount && a.RiderAgent != null) a = a.RiderAgent;
            if (a == null || !MissionTeams.Enemies(a.Team, v.Team)) { BannerCompanies.HitSkip448++; return; }
            // kierunek: skad przyszedl cios (ofiara -> napastnik), wzgledem patrzenia ofiary
            Vec2 from;
            if (h.Missile)
            {
                from = new Vec2(-h.Dir.x, -h.Dir.y);
                if (!(from.Length > 1e-3f)) from = (a.Position - v.Position).AsVec2;
            }
            else
            {
                from = (a.Position - v.Position).AsVec2;
                // 448-p: jezdziec w ruchu - skad nadjechal (odwrotnosc jazdy konia), nie gdzie stoi w chwili ciosu: szarza od czola, ktora weszla w szyk, to przod
                var mt = a.MountAgent;
                if (mt != null)
                {
                    Vec2 vel = mt.Velocity.AsVec2;
                    if (vel.Length >= BannerCompanies.CoRide) { from = new Vec2(-vel.x, -vel.y); BannerCompanies.RideDir448++; }
                }
            }
            float fl = from.Length;
            Vec2 look = v.LookDirection.AsVec2;
            float ll = look.Length;
            if (!(fl > 1e-3f) || !(ll > 1e-3f)) { BannerCompanies.HitSkip448++; return; }
            float cos = from.DotProduct(look) / (fl * ll);
            int d = cos >= BannerCompanies.FrontCos ? 0 : cos <= BannerCompanies.RearCos ? 2 : 1;
            if (d != 0)
            {
                // 448-p: drugi sedzia - srednie patrzenie kompanii (linia patrzy na wroga); lagodniejszy wynik. Kociol (patrza w rozne strony) - tylko czlowiek
                Vec2 mean;
                if (CoLook(co, out mean))
                {
                    float c2 = from.DotProduct(mean) / (fl * mean.Length);
                    int d2 = c2 >= BannerCompanies.FrontCos ? 0 : c2 <= BannerCompanies.RearCos ? 2 : 1;
                    if (d2 < d) { d = d2; if (d == 0) BannerCompanies.ToFront448++; }
                }
            }
            BannerCompanies.HitDir448[(h.Missile ? 3 : 0) + d]++;
            if (d == 0) return;
            if (d == 1 && v.HasMount) { BannerCompanies.RiderFlank448++; return; }   // jezdziec w szarzy przez szyk - bok to zwykla walka
            int s = d == 2 ? 0 : 1;
            var st = co.D ?? (co.D = new Dir448());
            float sc = st.Score[s];
            if (sc > 0f) sc *= (float)Math.Exp(-(now - st.At[s]) / BannerCompanies.Tau);
            if (sc < 0.25f) { sc = 0f; st.Foes[s].Clear(); }
            sc += h.Missile || a.HasMount ? BannerCompanies.MissileW : 1f;   // pocisk i cios jezdzca pol - szarza przez szyk sama nie wystarcza
            st.Score[s] = sc; st.At[s] = now;
            var foes = st.Foes[s];
            if (foes.Count < 4 && !foes.Contains(a)) foes.Add(a);
            float at = s == 0 ? BannerCompanies.RearAt : BannerCompanies.FlankAt;
            if (sc < at || foes.Count < BannerCompanies.MinFoes || now < st.NextOk[s]) return;
            // 448-p: prog wedlug wielkosci - wynik to suma ciosow na cala kompanie, wiec przy tym samym udziale trafionych duza kompania doszlaby do progu szybciej
            float big = InRanksLive(co, false) / BannerCompanies.SizeRef;
            if (big > 1f && sc < at * big) { BannerCompanies.SizeHeld448++; return; }
            st.Score[s] = 0f; foes.Clear();
            st.NextOk[s] = now + BannerCompanies.Cooldown;
            if (!on) { BannerCompanies.Would448[s]++; return; }
            BannerCompanies.Trig448[s]++;
            if (Roll(co, s == 0 ? BannerCompanies.RearF : BannerCompanies.FlankF, c, now, false, -1f, s == 0 ? BannerCompanies.SrcRear : BannerCompanies.SrcFlank))
                BannerCompanies.Roll448[s]++;
        }

        /// <summary>448 (Break, glowny watek): sasiedzi zlamanej kompanii do kolejki rzutu SASIAD po NbDelay s - takze przy kluczu NIE (pomiar).</summary>
        private void Neighbors448(Company br, Vec3 cen, float now)
        {
            if (br == null || br.T == null || br.Peak < 4) return;
            // 448-p: tlumienie lawiny - zalamanie z rzutu SASIAD ostatniego pokolenia nie kolejkuje dalej
            int gen = _nbGen + 1;
            if (gen > BannerCompanies.NbMaxGen) { BannerCompanies.NbCut448++; return; }
            var side = br.T.Side;
            float r2 = BannerCompanies.NbRange * BannerCompanies.NbRange;
            float t2 = BannerCompanies.NbTouch * BannerCompanies.NbTouch;
            _nbHit.Clear();
            _nbPos.Clear();
            for (int j = 0; j < br.Men.Count; j++)
            {
                var a = br.Men[j];
                if (a != null && a.IsActive()) _nbPos.Add(a.Position.AsVec2);
            }
            Vec2 c0 = cen.AsVec2;
            for (int i = 0; i < _all.Count; i++)
            {
                var x = _all[i];
                if (ReferenceEquals(x, br) || x.Broken || x.T == null || x.T.Side != side) continue;
                Vec2 p = Vec2.Zero;
                int n = 0;
                for (int j = 0; j < x.Men.Count; j++)
                {
                    var a = x.Men[j];
                    if (a == null || !a.IsActive()) continue;
                    p += a.Position.AsVec2; n++;
                }
                if (n == 0) continue;
                p = p * (1f / n);
                bool nb = p.DistanceSquared(c0) <= r2;
                // 448-p: styk szeregow (szeroka linia: srodki dalej niz NbRange) - czlowiek tej kompanii w NbTouch m od czlowieka zlamanej
                for (int j = 0; j < x.Men.Count && !nb; j++)
                {
                    var a = x.Men[j];
                    if (a == null || !a.IsActive()) continue;
                    Vec2 q = a.Position.AsVec2;
                    for (int k = 0; k < _nbPos.Count; k++)
                        if (q.DistanceSquared(_nbPos[k]) <= t2) { nb = true; break; }
                }
                if (nb) _nbHit.Add(x);
            }
            for (int i = 0; i < _nbHit.Count; i++)
            {
                bool queued = false;
                for (int j = 0; j < _nb.Count && !queued; j++) queued = ReferenceEquals(_nb[j].Co, _nbHit[i]);
                if (!queued) _nb.Add(new Nb448 { Co = _nbHit[i], Due = now + BannerCompanies.NbDelay, Gen = gen });
            }
            _nbHit.Clear();
            _nbPos.Clear();
        }

        private void NbDue(Company co, int gen, float now, bool on, Settings c)
        {
            if (co == null) return;
            var st = co.D ?? (co.D = new Dir448());
            if (co.Broken || now < st.NextOk[2]) { BannerCompanies.NbSkip448++; return; }
            // 448-p: inne rzuty czytaja morale gry - spadek morale po ucieczce sasiada (OnAgentFleeing) juz je podbija; bez drugiej drogi
            if (!c.BannerEventRollByLoss || !c.BannerLossBreakByShare) { BannerCompanies.NbMorale448++; return; }
            st.NextOk[2] = now + BannerCompanies.Cooldown;
            if (!on) { BannerCompanies.Would448[2]++; return; }
            BannerCompanies.Trig448[2]++;
            if (gen < 1) gen = 1;
            float f = BannerCompanies.NbF;
            for (int i = 1; i < gen; i++) f *= BannerCompanies.NbDecay;
            _nbGen = gen;
            try
            {
                if (Roll(co, f, c, now, false, -1f, BannerCompanies.SrcNb)) BannerCompanies.Roll448[2]++;
            }
            finally { _nbGen = 0; }
        }

        /// <summary>448-p: srednie patrzenie ludzi kompanii (czynni, nie uciekajacy); false, gdy patrza w rozne strony (dlugosc sredniej wektorow
        /// jednostkowych ponizej CoMeanMin) albo nikogo nie ma.</summary>
        private static bool CoLook(Company co, out Vec2 mean)
        {
            mean = Vec2.Zero;
            int n = 0;
            for (int i = 0; i < co.Men.Count; i++)
            {
                var a = co.Men[i];
                if (a == null || !a.IsActive() || a.IsRunningAway) continue;
                Vec2 l = a.LookDirection.AsVec2;
                float len = l.Length;
                if (!(len > 1e-3f)) continue;
                mean += l * (1f / len); n++;
            }
            if (n == 0) return false;
            mean = mean * (1f / n);
            return mean.Length >= BannerCompanies.CoMeanMin;
        }
    }
}
