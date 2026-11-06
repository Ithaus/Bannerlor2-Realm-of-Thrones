using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 333 (A) "Poscig z glowa" (06.11, Jeff po bitwie: "strzelam do kawalerii, oni jada za mna, wyciagam ich pod swoje wojsko pod lucznikow, jak widza moje wojsko
    /// zaczynaja sie odwracac plecami i jada do swoich, ja jade za nimi i strzelam w plecy, oni sie znowu wracaja ... normalnie kawaleria, ktora zauwazyla, ze jest ich 30,
    /// jada za mna i w koncu widza, ze tam moje wojsko stoi - nie scigaliby mnie, tylko wrocili do swoich ... moga tez odwracajac sie wysylac na mnie kilku kawalerzystow,
    /// ale oni zgina, bo w koncu beda musieli sie odwrocic. Trzeba to po prostu robic z glowa"). Klucz RidersBreakOffNearShootersArmy (przy kluczu 204).
    /// Przyczyna (log Jeffa 05.11 23:16, rozpoznanie 333): jezdziec 204 zawracal sam dopiero 15 m od nietknietego szyku strony strzelca (CounterFootGap - prog "nie wjezdzaj
    /// we wlocznie", a nie "nie wjezdzaj pod luki"), kazdy osobno co 0.25 s (wszyscy naraz, bez strazy); po powrocie 30 s przerwy, a nowy strzal = nowe wykrycie i EndEp
    /// zerowal poziom - nowy epizod od razu L2/L3 "cala jazda" (bitwa 03:04: 289 jezdzcow, 746 wejsc z szyku w szarzy, powroty "przy szyku strzelca" 284, "smycz" 476);
    /// odciecie (RoleCut) celowalo 30 m ZA strzelcem, czyli w strone jego wojska.
    ///
    /// STREFA ARMII STRZELCA (co pass 1 s, z szykow stron HForms): kazdy nietkniety szyk (od IntactMin ludzi, nie w odwrocie) strony strzelca, poza szykiem samego
    /// strzelca (konni lucznicy szykiem - "szyk na szyk" 204 zostaje): piechota promien szyku + 50 m (oszczepy, krotka szarza na konia w miejscu), lucznicy piesi promien
    /// + 0.8 x zasieg szyku (MissileRangeAdjusted gry, 60..120 m - dalej strzala w jezdzca jest juz loteria), konnica i konni lucznicy promien + 60 m (kontra jazdy).
    /// Odleglosc punktu od strefy g = najmniejsze (odleglosc od srodka szyku - promien strefy); g < 0 = w strefie.
    /// STANY strzelca (pamiec WEDLUG CZLOWIEKA - przezywa koniec zagrozenia, nowy strzal jej nie zeruje):
    ///  - WOLNY (bez pamieci): 204 jak dotad; nowy poscig calosci tylko, gdy g >= 30 m (ZStartGap); strzelec przy swojej armii (g < 30 m) - jezdzcy nie ruszaja
    ///    (lucznicy strony R1 dalej strzelaja).
    ///  - POSCIG: trwa, dopoki g >= 10 m (ZBreakGap; histereza 20 m wzgledem startu). Punkt kazdego jezdzca (przeciecie, odciecie, zaslona) przyciety do 10 m przed
    ///    strefa (ZClampGap) - nikt nie celuje w punkt pod lukami; jezdziec, ktory mimo to jest w strefie (armia strzelca podeszla) - wraca sam ("w strefie").
    ///    Kandydat w 10 m od strefy nie jest wysylany.
    ///  - ZERWANY (przyneta): strzelec wszedl w g < 10 m - CALA grupa wraca naraz (decyzja na poziomie zagrozenia, nie jezdzca), przerwa jezdzca tylko 5 s (moze
    ///    stanac w strazy), pamiec "przyneta" od tej chwili. Pelny poscig wraca dopiero, gdy strzelec jest naprawde daleko od swoich: g >= 100 m (ZReChaseGap, 10x
    ///    margines zerwania) bez przerwy przez 3 s i po 60 s od zerwania (ZCalm - ochloniecie) - wtedy stan WOLNY.
    ///  - TYLNA STRAZ (tylko w stanie ZERWANY): strzelec jedzie za wracajacymi i oddalil sie od swoich na g >= 40 m (ZGuardGap) - z najblizszego szyku konnicy AI
    ///    odlacza sie straz: 12% szyku, 3..5 jezdzcow, najwyzej 1/3 szyku (najblizsi strzelcowi - tyl wracajacych); reszta dalej wraca. Straz zrywa cala naraz, gdy
    ///    strzelec wroci w g < 20 m (ZGuardBreakGap - ostrozniej niz calosc), po 40 s (ZGuardMax) albo jezdziec wjedzie w strefe; potem straz nie rusza 30 s
    ///    (ZGuardCool), jezdzcy po 30 s przerwy (CounterCool). Straz to rola przeciecia (bez odciecia za strzelcem).
    /// Koszt: strefa raz na pass (szyki stron), odleglosc strefy dla strzelca raz na pass i dla jezdzca w kroku 0.25 s (najwyzej TrackPerStep) - kilkanascie szykow.
    /// Linie: "AI bitwy (333) zerwanie / straz / ponowny poscig" (najwyzej 12 na bitwe) i segment "poscig z glowa" linii "AI bitwy (333): misja ..." raz na bitwe polowa.
    /// </summary>
    internal static partial class HorsePursuit
    {
        // ------------------------------------------------------------ stale 333 (opis klasy)
        internal const float ZFootAdd = 50f, ZCavAdd = 60f, ZShotShare = 0.8f, ZShotMin = 60f, ZShotMax = 120f;
        internal const float ZBreakGap = 10f, ZStartGap = 30f, ZClampGap = 10f, ZGuardGap = 40f, ZGuardBreakGap = 20f, ZReChaseGap = 100f;
        internal const float ZCalm = 60f, ZReChaseHold = 3f, ZBreakCool = 5f, ZGuardMax = 40f, ZGuardCool = 30f, ZBackHitWindow = 20f;
        internal const float ZGuardShare = 0.12f;
        internal const int ZGuardMin = 3, ZGuardMaxN = 5;
        private const int ZMaxLines = 12;
        private const int BFull = 0, BGuard = 1, BNone = 2;

        private sealed class Bait
        {
            public bool On;
            public float BrokeAt = -1f, FarSince = -1f, GuardCool = -1f;
            public int Breaks;
        }

        private static bool _zOn;
        private static readonly Dictionary<Agent, Bait> Baits = new Dictionary<Agent, Bait>();
        private static readonly List<Vec2>[] ZC = { new List<Vec2>(), new List<Vec2>() };
        private static readonly List<float>[] ZR = { new List<float>(), new List<float>() };
        private static readonly List<Formation>[] ZF = { new List<Formation>(), new List<Formation>() };
        private static readonly List<Hunter> ZTmp = new List<Hunter>();
        private static readonly HashSet<int> ZNoStartSeen = new HashSet<int>();
        private static int _zFullBefore;
        // liczniki misji
        private static int _zBreaks, _zBreakRiders, _zEpBreakMax, _zShooterBreakMax, _zLoops, _zReChase, _zFull, _zNoStart, _zGuardEps, _zGuardRiders, _zGuardZone,
                           _zGuardTime, _zRiderIn, _zClamp, _zBackHits, _zLines, _zStum, _zZoneMax;

        private static void Bait333Clear()
        {
            Baits.Clear();
            for (int s = 0; s < 2; s++) { ZC[s].Clear(); ZR[s].Clear(); ZF[s].Clear(); }
            ZTmp.Clear(); ZNoStartSeen.Clear();
            _zFullBefore = 0;
            _zBreaks = _zBreakRiders = _zEpBreakMax = _zShooterBreakMax = _zLoops = _zReChase = _zFull = _zNoStart = _zGuardEps = _zGuardRiders = _zGuardZone = 0;
            _zGuardTime = _zRiderIn = _zClamp = _zBackHits = _zLines = _zStum = _zZoneMax = 0;
        }

        // ------------------------------------------------------------ strefa (pass, po BuildHForms)

        private static void ZoneBuild()
        {
            for (int s = 0; s < 2; s++)
            {
                ZC[s].Clear(); ZR[s].Clear(); ZF[s].Clear();
                if (!_zOn) continue;
                var fl = HForms[s];
                for (int i = 0; i < fl.Count; i++)
                {
                    var fi = fl[i];
                    if (fi.N < IntactMin || fi.Retreat) continue;
                    float add;
                    if (fi.Cav) add = ZCavAdd;
                    else if (fi.Rng)
                    {
                        float r = 0f;
                        try { var q = fi.F.QuerySystem; r = q.MissileRangeAdjusted; if (r <= 1f) r = q.MaximumMissileRange; } catch { r = 0f; }
                        if (r <= 1f) r = ZShotMax / ZShotShare;
                        add = Math.Max(ZShotMin, Math.Min(ZShotMax, ZShotShare * r));
                    }
                    else if (fi.Foot) add = ZFootAdd;
                    else continue;
                    ZC[s].Add(fi.C);
                    ZR[s].Add(fi.R + add);
                    ZF[s].Add(fi.F);
                }
                if (ZC[s].Count > _zZoneMax) _zZoneMax = ZC[s].Count;
            }
        }

        /// <summary>Odleglosc punktu od krawedzi strefy armii strony side (bez szyku ex); float.MaxValue = strona bez szyku w strefie.</summary>
        private static float ZoneGap(int side, Vec2 p, Formation ex)
        {
            if (side < 0 || side > 1) return float.MaxValue;
            var cs = ZC[side];
            var rs = ZR[side];
            var fs = ZF[side];
            float g = float.MaxValue;
            for (int i = 0; i < cs.Count; i++)
            {
                if (ex != null && ReferenceEquals(fs[i], ex)) continue;
                float d = p.Distance(cs[i]) - rs[i];
                if (d < g) g = d;
            }
            return g;
        }

        /// <summary>Punkt jezdzca odsuniety na ZClampGap przed strefe (najwyzej 2 przejscia - dwie nakladajace sie strefy).</summary>
        private static Vec2 ZoneClamp(Threat t, Vec2 p)
        {
            int side = t.SSide;
            if (side < 0 || side > 1) return p;
            var cs = ZC[side];
            var rs = ZR[side];
            var fs = ZF[side];
            bool moved = false;
            for (int it = 0; it < 2; it++)
            {
                int bi = -1;
                float bv = 0f;
                for (int i = 0; i < cs.Count; i++)
                {
                    if (t.ZEx != null && ReferenceEquals(fs[i], t.ZEx)) continue;
                    float v = rs[i] + ZClampGap - p.Distance(cs[i]);
                    if (v > bv) { bv = v; bi = i; }
                }
                if (bi < 0) break;
                Vec2 c = cs[bi];
                Vec2 d = p - c;
                float l = d.Length;
                if (l < 0.1f) break;
                p = c + d * ((rs[bi] + ZClampGap) / l);
                moved = true;
            }
            if (moved) _zClamp++;
            return p;
        }

        // ------------------------------------------------------------ brama zagrozenia (ThreatPass, przed wysylaniem jezdzcow)

        /// <summary>Stan strzelca 333: BFull - 204 jak dotad, BGuard - tylko tylna straz, BNone - bez jezdzcow. Zrywa poscig calej grupy i straz.</summary>
        private static int BaitGate(Mission m, Threat t, Vec2 sp, float now, bool mounted, bool rx)
        {
            _zFullBefore = 0;
            t.Z333 = _zOn && _hKey && mounted && !(rx && MeleeKind(t));
            if (!t.Z333) return BFull;
            var s = t.S;
            Formation ex = null;
            try { ex = s.Formation; } catch { ex = null; }
            t.ZEx = ex;
            float g = ZoneGap(t.SSide, sp, ex);
            t.Gap333 = g;
            int full = 0, guard = 0;
            for (int i = 0; i < t.Hs.Count; i++)
            {
                var h = t.Hs[i];
                if (h.Role != RoleIntercept && h.Role != RoleCut) continue;
                if (h.Guard) guard++; else full++;
            }
            _zFullBefore = full;
            Bait b;
            Baits.TryGetValue(s, out b);
            // 1. zerwanie calosci - strzelec doprowadzil poscig pod swoja armie
            if (full > 0 && g < ZBreakGap)
            {
                if (b == null) { b = new Bait(); Baits[s] = b; }
                b.On = true;
                b.BrokeAt = now;
                b.FarSince = -1f;
                b.Breaks++;
                if (b.Breaks > _zShooterBreakMax) _zShooterBreakMax = b.Breaks;
                t.EpBreaks++;
                if (t.EpBreaks > _zEpBreakMax) _zEpBreakMax = t.EpBreaks;
                _zBreaks++;
                int n = ReleaseRiders(t, now, false);
                _zBreakRiders += n;
                ZLine("zerwanie: strzelec " + Who(s) + " (" + (s.IsMainAgent ? "gracz" : "AI") + ") " + F0(g) + " m od strefy swojej armii (prog "
                      + F0(ZBreakGap) + " m) | cala grupa wraca: jezdzcow " + n + " | zerwanie nr " + b.Breaks + " tego strzelca | ponowny poscig dopiero " + F0(ZReChaseGap)
                      + " m od strefy przez " + F0(ZReChaseHold) + " s, po " + F0(ZCalm) + " s");
                return BNone;
            }
            if (b != null && b.On)
            {
                if (g >= ZReChaseGap) { if (b.FarSince < 0f) b.FarSince = now; }
                else b.FarSince = -1f;
                if (now - b.BrokeAt >= ZCalm && b.FarSince >= 0f && now - b.FarSince >= ZReChaseHold)
                {
                    b.On = false;
                    _zReChase++;
                    for (int i = 0; i < t.Hs.Count; i++) t.Hs[i].Guard = false;   // straz staje sie poscigiem calosci
                    ZLine("ponowny poscig: strzelec " + Who(s) + " " + F0(g) + " m od strefy swojej armii od " + (now - b.FarSince).ToString("0.0", Inv) + " s, "
                          + (now - b.BrokeAt).ToString("0", Inv) + " s po zerwaniu");
                    return BFull;
                }
                return GuardState(t, b, g, now, guard);
            }
            if (full == 0 && g < ZStartGap)
            {
                if (ZNoStartSeen.Add(s.Index)) _zNoStart++;
                return BNone;
            }
            return BFull;
        }

        /// <summary>Tylna straz w stanie ZERWANY: trwa / zrywa / wolno odlaczyc.</summary>
        private static int GuardState(Threat t, Bait b, float g, float now, int guard)
        {
            if (guard > 0)
            {
                bool zone = g < ZGuardBreakGap, time = false;
                for (int i = 0; i < t.Hs.Count; i++)
                {
                    var h = t.Hs[i];
                    if (h.Guard && now - h.Start >= ZGuardMax) { time = true; break; }
                }
                if (zone || time)
                {
                    ReleaseRiders(t, now, true);
                    b.GuardCool = now + ZGuardCool;
                    if (zone) _zGuardZone++; else _zGuardTime++;
                    return BNone;
                }
                return BGuard;
            }
            return g >= ZGuardGap && now >= b.GuardCool ? BGuard : BNone;
        }

        private static int GuardSize(int n)
        {
            int k = (int)Math.Ceiling(ZGuardShare * n);
            if (k < ZGuardMin) k = ZGuardMin;
            if (k > ZGuardMaxN) k = ZGuardMaxN;
            int cap = Math.Max(1, n / 3);
            return Math.Min(k, cap);
        }

        /// <summary>Tylna straz z najblizszego szyku konnicy AI strony (bez szykow gracza i w odwrocie), w smyczy od strzelca.</summary>
        private static void GuardTake(Mission m, Threat t, List<FI> fl, Vec2 sp, float now, ref int sent, ref int sentCharge)
        {
            int guard = 0;
            for (int i = 0; i < t.Hs.Count; i++) if (t.Hs[i].Guard) guard++;
            int best = -1;
            float bd = float.MaxValue;
            for (int i = 0; i < fl.Count; i++)
            {
                var fi = fl[i];
                if (!fi.Cav || fi.Ha || fi.Retreat) continue;
                float e = Math.Max(0f, sp.Distance(fi.C) - fi.R);
                if (fi.Player) { NotePlayerForm(t, fi, e); continue; }
                if (e > _maxFrom || e >= bd) continue;
                bd = e;
                best = i;
            }
            if (best < 0) return;
            int want = GuardSize(fl[best].N) - guard;
            if (want <= 0) return;
            int before = t.Hs.Count;
            Take(m, t, fl[best], want, now, ref sent, ref sentCharge);
            int added = 0;
            for (int i = before; i < t.Hs.Count; i++)
            {
                var h = t.Hs[i];
                if (h.Role != RoleIntercept && h.Role != RoleCut) continue;
                h.Guard = true;
                h.Role = RoleIntercept;
                added++;
            }
            if (added <= 0) return;
            _zGuardRiders += added;
            if (guard > 0) return;
            _zGuardEps++;
            ZLine("straz: strzelec " + Who(t.S) + " " + F0(t.Gap333) + " m od strefy swojej armii (prog " + F0(ZGuardGap) + " m) | z szyku " + fl[best].F.FormationIndex
                  + " (" + fl[best].N + " ludzi) odlaczonych " + added + ", reszta wraca | zrywa przy " + F0(ZGuardBreakGap) + " m albo po " + F0(ZGuardMax) + " s");
        }

        /// <summary>Jezdzcy strzelca oddani grze (guardsOnly - tylko straz). Przerwa: zerwanie calosci ZBreakCool (moga stanac w strazy), straz CounterCool.</summary>
        private static int ReleaseRiders(Threat t, float now, bool guardsOnly)
        {
            ZTmp.Clear();
            for (int i = 0; i < t.Hs.Count; i++)
            {
                var h = t.Hs[i];
                if (h.Role != RoleIntercept && h.Role != RoleCut) continue;
                if (guardsOnly && !h.Guard) continue;
                ZTmp.Add(h);
            }
            int n = 0;
            for (int i = 0; i < ZTmp.Count; i++)
            {
                var h = ZTmp[i];
                Hunter cur;
                if (h.A == null || !Hunters.TryGetValue(h.A, out cur) || !ReferenceEquals(cur, h)) continue;   // zwolniony wczesniej (koniec epizodu)
                var a = h.A;
                try { EndHunter(h, UZone, now, false, false); }
                catch (Exception e) { _zStum++; HStumble("HorsePursuit.Poscig333", e); DropHunter(h); }
                HuntCool[a] = now + (guardsOnly ? CounterCool : ZBreakCool);
                n++;
            }
            ZTmp.Clear();
            return n;
        }

        /// <summary>ThreatPass po wysylce w stanie BFull: nowy poscig calosci (licznik), petla przynety (poscig w ZCalm po zerwaniu - oczekiwane 0).</summary>
        private static void BaitFullSent(Threat t, float now, int sent)
        {
            if (!t.Z333 || sent <= 0 || _zFullBefore > 0) return;
            _zFull++;
            t.EpBreaks = 0;
            Bait b;
            if (t.S != null && Baits.TryGetValue(t.S, out b) && b.BrokeAt >= 0f && now - b.BrokeAt < ZCalm) _zLoops++;
        }

        /// <summary>OnRiderShot (glowny watek): jezdziec AI trafiony przez strzelca w ZBackHitWindow s po zerwaniu poscigu za nim (strzaly w plecy wracajacych).</summary>
        private static void Bait333Hit(Mission m, Agent s)
        {
            if (!_zOn || s == null) return;
            Bait b;
            if (!Baits.TryGetValue(s, out b) || b.BrokeAt < 0f) return;
            float now = m.CurrentTime;
            if (now - b.BrokeAt <= ZBackHitWindow) _zBackHits++;
        }

        private static void ZLine(string txt)
        {
            if (_zLines >= ZMaxLines) return;
            _zLines++;
            Log.Info("AI bitwy (333) " + txt + ".");
        }

        // ------------------------------------------------------------ linia "AI bitwy (333): misja ..." (raz na bitwe polowa)

        private static void AiBattle333Write()
        {
            if (!_field) return;
            var c = Settings.Current;
            bool keyA = c != null && c.RidersBreakOffNearShootersArmy;
            var sb = new StringBuilder(1500);
            sb.Append("AI bitwy (333): misja ").Append(_kind).Append(" | poscig z glowa klucz ").Append(keyA ? "TAK" : "NIE").Append(" (jazda 204 ").Append(_hKey ? "TAK" : "NIE")
              .Append("; strefa armii strzelca: piechota +").Append(F0(ZFootAdd)).Append(" m, lucznicy +").Append(ZShotShare.ToString("0.0", Inv)).Append(" zasiegu ")
              .Append(F0(ZShotMin)).Append('-').Append(F0(ZShotMax)).Append(" m, konnica +").Append(F0(ZCavAdd)).Append(" m; zerwanie < ").Append(F0(ZBreakGap))
              .Append(" m, start >= ").Append(F0(ZStartGap)).Append(" m, straz >= ").Append(F0(ZGuardGap)).Append(" m (zrywa < ").Append(F0(ZGuardBreakGap)).Append(" m, ")
              .Append(F0(ZGuardMax)).Append(" s, przerwa ").Append(F0(ZGuardCool)).Append(" s), ponowny poscig >= ").Append(F0(ZReChaseGap)).Append(" m przez ")
              .Append(F0(ZReChaseHold)).Append(" s po ").Append(F0(ZCalm)).Append(" s)")
              .Append(" | poscigi calosci ").Append(_zFull).Append(", zerwania calosci ").Append(_zBreaks).Append(" (jezdzcow ").Append(_zBreakRiders)
              .Append("), zerwan na poscig najwiecej ").Append(_zEpBreakMax).Append(", zerwan na strzelca najwiecej ").Append(_zShooterBreakMax)
              .Append(", nawroty po zerwaniu ").Append(_zLoops).Append(" (oczekiwane 0), ponowne poscigi po ochlonieciu ").Append(_zReChase)
              .Append(", strzelcy przy swojej armii bez poscigu ").Append(_zNoStart)
              .Append(" | tylna straz: odlaczen ").Append(_zGuardEps).Append(" (jezdzcow ").Append(_zGuardRiders).Append("), zerwana przy strefie ").Append(_zGuardZone)
              .Append(", po czasie ").Append(_zGuardTime)
              .Append(" | jezdzcy zawroceni w strefie ").Append(_zRiderIn).Append(", nie wyslani przy strefie ").Append(Nw[NwZone]).Append(", punktow przycietych do strefy ")
              .Append(_zClamp).Append(", szykow w strefie najwiecej ").Append(_zZoneMax)
              .Append(" | trafienia wracajacych w ").Append(F0(ZBackHitWindow)).Append(" s po zerwaniu ").Append(_zBackHits)
              .Append(" | ");
            Pendulum333.Append(sb);
            sb.Append(" | potkniecia 333 ").Append(_zStum + Pendulum333.Stumbles).Append('.');
            Log.Info(sb.ToString());
        }
    }
}
