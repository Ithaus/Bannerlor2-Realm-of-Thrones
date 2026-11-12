using System;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Threading;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 439 = PAKIET 2 audytu AI bitwy (docs/audyty-2026-11-11/AUDYT-AI-BITWA.md rozdz. 3 "Obronca trzyma pozycje"; Jeff 11.11 "rob wszystko po kolei tak jak
    /// radzisz", decyzja nr 3 "Wrog broni wzgorza ... Rekomendacja: TAK"). Tylko decyzje, zadnych bonusow do statystyk. Dwa klucze (domyslnie TAK), NIE = jak dotad.
    /// Dotyczy WYLACZNIE druzyny obroncy w trzymaniu 291 (Doctrine.Hold291: _dhRef = druzyna, _dhOn = trzyma - nie puszczona przez S2 / 291e / 320d /
    /// "napastnik nie podchodzi" / zwarcie 25 m); bez klucza DefendersHoldTheirGround nic.
    ///
    /// 2.1 + 2.2 DefenderHoldsUntilMeleeContact ("Defender Holds Until Melee Contact"). DOWOD (audyt rozdz. 2 tabela "Obronca ma wzgorze ... schodzi do szarzy
    ///     na 75 m, takze na samych harcownikow" - ZLE; licznik 291 'taktyka obrony w Engage (RBM 75 m) N s' niezerowy w 11 z 73 linii autotestu).
    ///     Przyczyna [K] (rbmai): RBM TacticDefensiveEngagementPatch / TacticDefensiveLinePatch.PrefixHasBattleBeenJoined (return false) daje
    ///     Utilities.HasBattleBeenJoined(_mainInfantry, ..) = szyk pieszy ALBO STRZELCOW wroga (FindSignificantEnemy includeRanged) blizej niz 75 m (mediany + pol
    ///     glebokosci, +5 m histerezy), albo 35% piechoty w walce wrecz, albo sama jazda zostala - wtedy TickOccasionally taktyki wola Engage (RBM FixCharge -
    ///     piechota BehaviorCharge) i obronca zbiega z pozycji. Te same 75 m maja splity obrony RBM (RBMTacticDefendSplitInfantry :865 i
    ///     RBMTacticDefendSplitArchers :1118 - wlasne prywatne HasBattleBeenJoined, Engage = Charge calej piechoty).
    ///     Zmiana: postfiks (Priority.Last, after com.rbmai) na prywatnych HasBattleBeenJoined zadeklarowanych w TacticDefensiveEngagement, TacticDefensiveLine
    ///     (mb:39836, 40035) i - swiadomie OBSLUZONE, nie pominiete - w obu splitach obrony RBM (DeclaredMethod; typy z 438 po nazwie). Gdy wynik TAK, druzyna
    ///     trzyma (291), glowna piechota taktyki (_mainInfantry, pole TacticComponent) ma ludzi, a NIE MA zwarcia - wynik NIE. Zwarcie = najblizszy szyk wroga
    ///     (dowolny, takze jazda - CachedClosestEnemyFormation, odczyt jak w HasBattleBeenJoined gry) w S1ContactGap (25 m) mediany minus pol glebokosci, albo
    ///     flaga glownego watku: ktorykolwiek szyk piechoty druzyny ma >= 35% ludzi z ciosem wrecz zadanym / otrzymanym w 6 s (miara RBM FormationFightingInMelee).
    ///     "Sama jazda zostala" (glowna piechota pusta) - bez zmian (RBM). NIE latamy RBMAI.Utilities.HasBattleBeenJoined (wolaja ja plany ataku i splity ataku).
    ///     2.2 wynika z tego samego: strzelcy wroga w 75 m nie przelaczaja obrony w szarze - od tego sa reguly ostrzalu (S2 141 / 291e / 320d / 438), ktore
    ///     PUSZCZAJA trzymanie 291 (_dhOn NIE), a wtedy postfiks nic nie zmienia i RBM dziala jak dotad.
    /// 2.3 DefenderSeeksHighGround ("Defender Seeks High Ground"). Przyczyna [K]: RBM OverrideBehaviorHoldHighGround.PostfixCalculateCurrentOrder (rbmai:3229)
    ///     - wrog pieszy / strzelcy dalej niz 200 m: pamiec = srednia szyku + Direction x 10 m (szyk idzie naprzod), blizej - rozkaz Move w ostatnia pamiec
    ///     (szyk "zamarza w marszu"); punkt wzgorza gry (HighGroundCloseToForeseenBattleGround, mb:43006 - stok w kwadracie o boku polowy drogi do wroga, wiec
    ///     takze ku wrogowi) zostaje tylko w czasie > 200 m. Zmiana: punkt wybierany w GLOWNYM watku (H439Tick z Doctrine.Tick co 2 s) dla druzyny w trzymaniu 291:
    ///     najpierw znaczniki sceny TacticalPosition typu HighGround (TeamAI.TacticalPositions; kwalifikacja jak gra mb:40132 - zwrocony ku wrogowi albo
    ///     IsInsurmountable), potem stok ZA szykiem z zapytania silnika (Mission.FindPositionWithBiggestSlopeTowardsDirectionInSquare - to samo co gra, kwadrat
    ///     cofniety od wroga o r = 20% drogi, 20..60 m). Warunki punktu: wyzej o >= 2 m od mediany glownej piechoty, najwyzej min(120 m, pol drogi do wroga) od niej,
    ///     nie blizej wroga niz 75% obecnej drogi, w granicach pola i >= 40 m od granicy; wrog dalej niz 60 m. Punkt liczony od nowa co takt w rozstawieniu
    ///     (gotowy przed pierwszym rozkazem - AI w rozstawieniu nie tyka), po rozstawieniu raz i ZAMROZONY (bez wedrowki punktu - zapadka 344 obcialaby powrot).
    ///     Postfiks (Priority.Last, after com.rbmai) BehaviorHoldHighGround.CalculateCurrentOrder (zadeklarowana w BehaviorHoldHighGround): szyk druzyny w
    ///     trzymaniu 291 z punktem - _currentOrder = Move(punkt), IsCurrentOrderChanged; kierunek twarza (RBM / gra - ku wrogowi) bez zmian. Bez punktu - jak dotad.
    ///     Postfiks tylko czyta tablice (referencja punktu podmieniana atomowo z glownego watku) i pisze pola wlasnej instancji zachowania (jak RBM).
    /// POMIAR (linia "Obronca trzyma (439)" po linii 438): decyzje Engage zatrzymane / bylby (klucz NIE) / puszczone zwarciem 25 m / 35% wrecz, druzyn z
    ///     zatrzymaniem i maks. naraz (ozywiony kod: ile druzyn naraz stoi zamiast szarzowac), sekundy splitu RBM w Engage przy trzymaniu (gdyby JIT wkleil
    ///     male HasBattleBeenJoined RBM - latka bez skutku, widac tu), punkty wzgorza (znacznik / stok / brak wedlug powodu, sr. wzniesienie i odleglosc),
    ///     rozkazy z punktem, bylby (klucz NIE); w chwili kontaktu (pierwszy raz glowna piechota <= 25 m od pieszego wroga) - wysokosc obroncy wzgledem wroga
    ///     (sr., wyzej >= 2 m, nizej <= -2 m), osobno druzyny z punktem 439 i ile z nich stalo na punkcie (<= max(15 m, glebokosc)).
    /// Watki: H439Tick (punkt, flaga wrecz, kontakt, liczniki) w glownym; postfiksy w ticku druzyn / szykow (watek silnika) - odczyt + Interlocked.
    /// Potkniecia liczone (_h439Stum), bez globalnego wylacznika.
    ///
    /// 439-p (recenzja 439) - opis wyzej poprawiony tymi punktami (gdzie sie rozni, obowiazuje ten):
    ///  a) RBM CheckIfOnlyCavRemaining patrzy na WROGA (same szyki jazdy), a FindSignificantEnemy == null tez daje TAK - nie na pusta nasza piechote. Wrog bez
    ///     szyku pieszego / strzelcow (flaga glownego watku _h439NoFoot, ta sama petla co FootGap) - wynik RBM bez zmian (jak dotad, bez ciecia).
    ///  b) Zwolnienie Engage liczone w GLOWNYM watku (H439Tick) i z histereza: PIESZY wrog (bez jazdy, jak FootGap 291) w P440Gap (25 m / porywczy 40 m)
    ///     albo >= 35% GLOWNEJ piechoty wrecz w 6 s (jak RBM FormationFightingInMelee na _mainInfantry, nie kazdy szyk piechoty). Raz zwolniona druzyna
    ///     zostaje zwolniona, dopoki warunek trwa, i jeszcze H439RelHold (10 s) - bez wahadla Engage/Defend przy krazacej jezdzie. Jazda wroga w 25 m sama
    ///     nie zwalnia (RBM tez jej nie liczy - includeCavalry NIE); uderzenie jazdy w glowna piechote zwalnia przez wrecz.
    ///  c) Punkt wzgorza kasowany do konca bitwy, gdy trzymanie 291 sie skonczy (S2 / 291e / 320d / zwarcie / napastnik nie podchodzi - po powrocie trzymania
    ///     po S2 piechota nie wraca pod ostrzalem na stary punkt), gdy wrog jest blizej punktu niz 75% drogi glownej piechoty do wroga (obszedl szyk) albo gdy
    ///     piechota jest dalej od punktu niz H439MaxShift.
    ///  d) 291 "napastnik nie podchodzi" dla druzyny z punktem (klucz DefenderSeeksHighGround TAK) mierzy odleglosc wroga od STALEGO punktu (H439StallGap), nie
    ///     TeamGap - odwrot obroncy na punkt nie zjada zblizenia napastnika. Zmiana miary = nowa podstawa odleglosci, zegar 90 s bez zmian.
    ///  e) Pomiar: "w trzymaniu" przy kontakcie ze stanu poprzedniego taktu (DhTick w tym samym takcie juz puszcza zwarciem); "naraz maks" = druzyny z
    ///     zatrzymaniem w tym samym takcie 2 s; split RBM w Engage liczony tylko bez prawa do zwolnienia od 4 s (wieksze od 0 = latka bez skutku). Inlining:
    ///     MonoMod.Core (Harmony 2.4) wylacza inlining latanej metody (TryDisableInlining), latki zakladane w AfterStart pierwszej bitwy - przed pierwszym
    ///     JIT TickOccasionally splitu; miernik to rozstrzyga.
    /// </summary>
    internal static partial class Doctrine
    {
        internal const float H439MeleeShare = 0.35f, H439MeleeWin = 6f, H439MinRise = 2f, H439MaxShift = 120f, H439MinGap = 60f, H439EdgeMin = 40f;
        internal const float H439Toward = 0.75f, H439SlopeMin = 20f, H439SlopeMax = 60f, H439SlopeShare = 0.2f, H439OnPoint = 15f;
        internal const float H439RelHold = 10f, H439Settle = 4f;   // 439-p: histereza zwolnienia Engage; miernik splitu - ile s po zwolnieniu nie liczyc
        private static readonly IFormatProvider H439Inv = CultureInfo.InvariantCulture;

        private sealed class H439Point
        {
            public WorldPosition Pos;
            public Vec2 At;
            public float Rise, Shift;
            public byte Src;   // 1 znacznik HighGround sceny, 2 stok silnika
        }

        // ---- klucze (kopie z glownego watku; postfiksy czytaja)
        private static volatile bool _h439HoldKey = true, _h439HighKey = true;
        private static string _h439Patch = "nie wpiete";
        private static AccessTools.FieldRef<TacticComponent, Formation> _f439Main;
        private static FieldInfo _f439SplitJoinedInf, _f439SplitJoinedArch;

        // ---- stan druzyn (glowny watek pisze; postfiksy czytaja _h439Melee / _h439Pt)
        private static readonly bool[] _h439Melee = new bool[MaxTeams];
        private static readonly H439Point[] _h439Pt = new H439Point[MaxTeams];
        private static readonly bool[] _h439Frozen = new bool[MaxTeams];
        private static readonly bool[] _h439Contact = new bool[MaxTeams];
        private static readonly bool[] _h439CutTeam = new bool[MaxTeams];   // pisze postfiks (zatrzymal Engage od ostatniego taktu), czysci glowny
        private static readonly bool[] _h439CutEver = new bool[MaxTeams];
        // 439-p: glowny watek pisze, postfiks czyta - wrog bez pieszych / strzelcow, odleglosc do pieszego wroga, zwolnienie Engage (histereza)
        private static readonly bool[] _h439NoFoot = new bool[MaxTeams];
        private static readonly float[] _h439Gap = new float[MaxTeams];
        private static readonly bool[] _h439Free = new bool[MaxTeams];
        private static readonly bool[] _h439FreeEarly = new bool[MaxTeams];   // zwolnienie samym dystansem 25-40 m (porywczy 440)
        private static readonly float[] _h439FreeAt = new float[MaxTeams];    // ostatni takt z warunkiem zwolnienia
        private static readonly float[] _h439LetAt = new float[MaxTeams];     // ostatni takt, w ktorym postfiks przepuszczal TAK RBM (miernik splitu)
        private static readonly bool[] _h439OnLast = new bool[MaxTeams];      // _dhOn z konca poprzedniego taktu 439 (przed DhTick tego taktu)
        private static readonly bool[] _h439CutNow = new bool[MaxTeams];      // zatrzymanie od ostatniego taktu (naraz maks)
        private static readonly H439Point[] _h439StallSrc = new H439Point[MaxTeams];   // punkt, od ktorego DhTick mierzy podstawe 'napastnik nie podchodzi'

        // ---- liczniki misji (Reset w Battle291.Begin); postfiksy - Interlocked
        private static int _h439Cut, _h439Dry, _h439Near, _h439MeleeN, _h439HighSet, _h439HighDry;
        private static int _h439CavOnly, _h439FreeCalls, _h439Back, _h439KillRel, _h439KillSide, _h439KillFar, _h439StallPtTeams, _h439StallPtN;
        private static int _h439CutTeams, _h439PeakTeams, _h439Stum;
        private static float _h439SplitEngSec;
        private static int _h439PtMark, _h439PtSlope, _h439PtNoRise, _h439PtClose, _h439PtNoData, _h439PtNaval;
        private static float _h439RiseSum, _h439ShiftSum;
        private static int _h439CtN, _h439CtHi, _h439CtLo, _h439CtPtN, _h439CtOnPt, _h439CtHold;
        private static float _h439CtDz, _h439CtPtDz;
        // flaga wrecz (glowny watek)
        private static float _mlNow;
        private static int _mlAll, _mlIn;

        /// <summary>Battle291.Begin (AfterStart): liczniki linii 439 od zera, stan druzyn bez misji.</summary>
        internal static void H439Reset()
        {
            for (int i = 0; i < MaxTeams; i++) H439ClearTeam(i);
            Interlocked.Exchange(ref _h439Cut, 0); Interlocked.Exchange(ref _h439Dry, 0); Interlocked.Exchange(ref _h439Near, 0);
            Interlocked.Exchange(ref _h439MeleeN, 0); Interlocked.Exchange(ref _h439HighSet, 0); Interlocked.Exchange(ref _h439HighDry, 0);
            _h439CutTeams = _h439PeakTeams = _h439Stum = 0;
            Interlocked.Exchange(ref _h439CavOnly, 0); Interlocked.Exchange(ref _h439FreeCalls, 0);
            _h439Back = _h439KillRel = _h439KillSide = _h439KillFar = _h439StallPtTeams = _h439StallPtN = 0;
            _h439SplitEngSec = 0f;
            _h439PtMark = _h439PtSlope = _h439PtNoRise = _h439PtClose = _h439PtNoData = _h439PtNaval = 0;
            _h439RiseSum = _h439ShiftSum = 0f;
            _h439CtN = _h439CtHi = _h439CtLo = _h439CtPtN = _h439CtOnPt = _h439CtHold = 0;
            _h439CtDz = _h439CtPtDz = 0f;
            var c = Settings.Current;
            if (c != null) H439Keys(c);
        }

        /// <summary>DhClearTeam (rejestracja 291 i Doctrine.Clear): stan druzyny 439 od zera.</summary>
        private static void H439ClearTeam(int i)
        {
            _h439Melee[i] = false; _h439Pt[i] = null; _h439Frozen[i] = false; _h439Contact[i] = false; _h439CutTeam[i] = false; _h439CutEver[i] = false;
            _h439NoFoot[i] = false; _h439Gap[i] = float.MaxValue; _h439Free[i] = false; _h439FreeEarly[i] = false; _h439FreeAt[i] = -1f; _h439LetAt[i] = -1f;
            _h439OnLast[i] = false; _h439CutNow[i] = false; _h439StallSrc[i] = null;
        }

        private static void H439Keys(Settings c)
        {
            _h439HoldKey = c.DefenderHoldsUntilMeleeContact;
            _h439HighKey = c.DefenderSeeksHighGround;
        }

        // ------------------------------------------------------------ latki (raz na proces - ApplyMissionPatches, po O438ApplyPatch: typy splitow)

        private static string H439ApplyPatch(Harmony h)
        {
            var miss = new StringBuilder(60);
            int want = 0, ok = 0;
            try { _f439Main = AccessTools.FieldRefAccess<TacticComponent, Formation>("_mainInfantry"); } catch { _f439Main = null; }
            if (_f439Main == null) miss.Append(" pole _mainInfantry");
            else
            {
                var join = new HarmonyMethod(typeof(Doctrine), nameof(H439JoinPost)) { priority = Priority.Last, after = new[] { "com.rbmai" } };
                foreach (var t in new[] { typeof(TacticDefensiveEngagement), typeof(TacticDefensiveLine), _tDefInf, _tDefArch })
                {
                    if (t == null) continue;
                    want++;
                    if (H439PatchOne(h, t, "HasBattleBeenJoined", typeof(bool), join, nameof(H439JoinPost))) ok++; else miss.Append(' ').Append(t.Name);
                }
            }
            try { _f439SplitJoinedInf = _tDefInf != null ? AccessTools.DeclaredField(_tDefInf, "_hasBattleBeenJoined") : null; } catch { _f439SplitJoinedInf = null; }
            try { _f439SplitJoinedArch = _tDefArch != null ? AccessTools.DeclaredField(_tDefArch, "_hasBattleBeenJoined") : null; } catch { _f439SplitJoinedArch = null; }
            if (_f439SplitJoinedInf != null && _f439SplitJoinedInf.FieldType != typeof(bool)) _f439SplitJoinedInf = null;
            if (_f439SplitJoinedArch != null && _f439SplitJoinedArch.FieldType != typeof(bool)) _f439SplitJoinedArch = null;
            bool high = H439PatchOne(h, typeof(BehaviorHoldHighGround), "CalculateCurrentOrder", typeof(void),
                new HarmonyMethod(typeof(Doctrine), nameof(H439HighPost)) { priority = Priority.Last, after = new[] { "com.rbmai" } }, nameof(H439HighPost));
            _h439Patch = "HasBattleBeenJoined " + ok + "/" + want + (miss.Length > 0 ? " (NIE:" + miss + ")" : "") + ", HoldHighGround " + (high ? "TAK" : "NIE");
            return _h439Patch;
        }

        private static bool H439PatchOne(Harmony h, Type t, string name, Type ret, HarmonyMethod post, string postName)
        {
            try
            {
                var target = AccessTools.DeclaredMethod(t, name, Type.EmptyTypes);
                if (target == null || target.ReturnType != ret) return false;
                h.Patch(target, postfix: post);
                var info = Harmony.GetPatchInfo(target);
                if (info != null) foreach (var p in info.Postfixes) if (p != null && p.owner == h.Id && p.PatchMethod != null && p.PatchMethod.Name == postName) return true;
                return false;
            }
            catch (Exception e)
            {
                RoutHold.Stumble(RoutHold.SiteDoctrine, "Doctrine.Latka439 " + t.Name + "." + name, e);
                return false;
            }
        }

        // ------------------------------------------------------------ 2.1 / 2.2 postfiks HasBattleBeenJoined (tick druzyn)

        /// <summary>Postfiks HasBattleBeenJoined taktyk obrony gry i splitow obrony RBM: druzyna w trzymaniu 291 bez zwarcia - NIE (bez Engage). Bez alokacji.</summary>
        private static void H439JoinPost(object __instance, ref bool __result)
        {
            if (!_on || !__result) return;
            try
            {
                var tc = __instance as TacticComponent;
                var team = tc != null ? tc.Team : null;
                if (team == null || team.Side != BattleSideEnum.Defender) return;
                int ti = team.TeamIndex;
                if (ti < 0 || ti >= MaxTeams || !ReferenceEquals(_dhRef[ti], team) || !_dhOn[ti]) return;
                var fr = _f439Main;
                var inf = fr != null ? fr(tc) : null;
                if (inf == null || inf.CountOfUnits <= 0) return;   // brak glownej piechoty (RBM CheckIfOnlyCavRemaining(null) = TAK) - jak dotad
                // 439-p (a): wrog bez szyku pieszego / strzelcow (RBM: sama jazda wroga albo FindSignificantEnemy null - TAK) - jak dotad
                if (_h439NoFoot[ti]) { Interlocked.Increment(ref _h439CavOnly); return; }
                // 439-p (b): zwolnienie z glownego watku (pieszy wrog w 25 m / porywczy 40 m albo >= 35% glownej piechoty wrecz) z histereza 10 s
                if (_h439Free[ti])
                {
                    if (_h439FreeEarly[ti]) Interlocked.Increment(ref _cpGapEarly);   // 440 (3.3): porywczy - pieszy wrog w 25-40 m
                    Interlocked.Increment(ref _h439FreeCalls);
                    return;
                }
                P440GapDry(team, ti, _h439Gap[ti]);
                if (!_h439HoldKey) { Interlocked.Increment(ref _h439Dry); return; }
                __result = false;
                _h439CutTeam[ti] = true;
                Interlocked.Increment(ref _h439Cut);
            }
            catch { Interlocked.Increment(ref _postErr); }
        }

        // ------------------------------------------------------------ 2.3 postfiks BehaviorHoldHighGround.CalculateCurrentOrder (tick szykow)

        /// <summary>Postfiks CalculateCurrentOrder: szyk druzyny w trzymaniu 291 z punktem wzgorza 439 - rozkaz Move w punkt. Bez alokacji.</summary>
        private static void H439HighPost(BehaviorHoldHighGround __instance, ref MovementOrder ____currentOrder, ref bool ___IsCurrentOrderChanged)
        {
            if (!_on || __instance == null) return;
            try
            {
                var f = __instance.Formation;
                var team = f != null ? f.Team : null;
                if (team == null || team.Side != BattleSideEnum.Defender) return;
                int ti = team.TeamIndex;
                if (ti < 0 || ti >= MaxTeams || !ReferenceEquals(_dhRef[ti], team) || !_dhOn[ti]) return;
                var pt = _h439Pt[ti];
                if (pt == null) return;
                if (!_h439HighKey) { Interlocked.Increment(ref _h439HighDry); return; }
                ____currentOrder = MovementOrder.MovementOrderMove(pt.Pos);
                ___IsCurrentOrderChanged = true;
                Interlocked.Increment(ref _h439HighSet);
            }
            catch { Interlocked.Increment(ref _postErr); }
        }

        // ------------------------------------------------------------ takt (glowny watek, Doctrine.Tick po DhTick)

        /// <summary>Doctrine.Tick (co TickEvery, glowny watek) dla druzyny obroncy 291: zwolnienie Engage, punkt wzgorza, kontakt, liczniki.</summary>
        private static void H439Tick(Mission m, Team t, int ti, Settings c, float now)
        {
            H439Keys(c);
            bool dep = m.IsDeploymentFinished;
            var inf = MainInfantry(t);
            bool hold = dep && _dhOn[ti];
            // pieszy wrog glownej piechoty (jak FootGap 291 - bez jazdy i konnych lucznikow): zwolnienie 2.1 i pomiar kontaktu
            Formation ef = null;
            float fgap = float.MaxValue;
            if (dep && inf != null) fgap = H439FootEnemy(t, inf, out ef);
            // 2.1 + 439-p (a)(b): flagi dla postfiksu - tylko druzyna w trzymaniu po rozstawieniu
            bool melee = false;
            if (hold && inf != null)
            {
                bool baseMelee;
                melee = H439InMelee(inf, now, P440MeleeShare(t), out baseMelee);   // 440 (3.3): prog wrecz wedlug profilu wodza (klucz NIE - 35%)
                P440NoteMelee(melee, baseMelee);
                _h439NoFoot[ti] = ef == null;
                _h439Gap[ti] = ef != null ? fgap : float.MaxValue;
                bool near = ef != null && fgap <= P440Gap(t, ti);
                if (melee || near)
                {
                    if (!_h439Free[ti]) { _h439Free[ti] = true; if (melee) _h439MeleeN++; else _h439Near++; }
                    _h439FreeEarly[ti] = near && !melee && fgap > S1ContactGap;
                    _h439FreeAt[ti] = now;
                }
                else if (_h439Free[ti] && now - _h439FreeAt[ti] >= H439RelHold)
                {
                    _h439Free[ti] = false; _h439FreeEarly[ti] = false; _h439Back++;   // powrot do trzymania (RBM Defend przy nastepnym TickOccasionally)
                }
            }
            else
            {
                _h439NoFoot[ti] = false; _h439Gap[ti] = float.MaxValue; _h439Free[ti] = false; _h439FreeEarly[ti] = false;
            }
            _h439Melee[ti] = melee;
            if (_h439Free[ti] || _h439NoFoot[ti]) _h439LetAt[ti] = now;
            // 439-p (e): ile druzyn naraz stoi zamiast Engage - zatrzymanie od ostatniego taktu (postfiks znaczy), nie "kiedykolwiek"
            bool cutNow = _h439CutTeam[ti];
            _h439CutTeam[ti] = false;
            _h439CutNow[ti] = cutNow;
            if (cutNow && !_h439CutEver[ti]) { _h439CutEver[ti] = true; _h439CutTeams++; }
            H439Peak();
            // pomiar: split obrony RBM w Engage przy trzymaniu BEZ prawa do zwolnienia od H439Settle s (latka bez skutku = sekundy rosna)
            if (hold && _h439HoldKey && !_h439Free[ti] && !_h439NoFoot[ti] && now - _h439LetAt[ti] >= H439Settle
                && _dhOnAt[ti] >= 0f && now - _dhOnAt[ti] >= H439Settle)
            {
                var cur = CurTactic(t);
                if (cur != null)
                {
                    var ty = cur.GetType();
                    FieldInfo fj = ty == _tDefInf && ty != null ? _f439SplitJoinedInf : (ty == _tDefArch && ty != null ? _f439SplitJoinedArch : null);
                    if (fj != null) { object v = null; try { v = fj.GetValue(cur); } catch { v = null; } if (v is bool b && b) _h439SplitEngSec += TickEvery; }
                }
            }
            // 2.3: punkt wzgorza - w rozstawieniu co takt, po rozstawieniu raz (zamrozony)
            if (!_h439Frozen[ti] && c.DefendersHoldTheirGround)
            {
                byte why = 0;
                H439Point pt = null;
                if (m.IsNavalBattle) why = 4;
                else if (inf == null) why = 3;
                else pt = H439Pick(m, t, inf, out why);
                _h439Pt[ti] = pt;   // podmiana referencji (atomowa) - postfiks widzi stary albo nowy punkt
                if (dep)
                {
                    _h439Frozen[ti] = true;
                    if (pt != null)
                    {
                        if (pt.Src == 1) _h439PtMark++; else _h439PtSlope++;
                        _h439RiseSum += pt.Rise; _h439ShiftSum += pt.Shift;
                    }
                    else if (why == 1) _h439PtNoRise++;
                    else if (why == 2) _h439PtClose++;
                    else if (why == 4) _h439PtNaval++;
                    else _h439PtNoData++;
                }
            }
            // pomiar: wysokosc obroncy wzgledem wroga w chwili kontaktu (pierwszy raz glowna piechota <= 25 m od pieszego wroga)
            if (dep && !_h439Contact[ti] && inf != null && ef != null && fgap <= S1ContactGap)
            {
                _h439Contact[ti] = true;
                float dz = inf.CachedMedianPosition.GetNavMeshZ() - ef.CachedMedianPosition.GetNavMeshZ();
                if (!float.IsNaN(dz) && !float.IsInfinity(dz))
                {
                    _h439CtN++; _h439CtDz += dz;
                    if (dz >= H439MinRise) _h439CtHi++; else if (dz <= -H439MinRise) _h439CtLo++;
                    // 439-p (e): stan trzymania z poprzedniego taktu - DhTick tego taktu juz puscil druzyne zwarciem (_dhOff 2)
                    if (_h439OnLast[ti]) _h439CtHold++;
                    var pt = _h439Pt[ti];
                    if (pt != null)
                    {
                        _h439CtPtN++; _h439CtPtDz += dz;
                        if (inf.CachedAveragePosition.Distance(pt.At) <= Math.Max(H439OnPoint, inf.Depth)) _h439CtOnPt++;
                    }
                }
            }
            // 439-p (c): punkt zamrozony kasowany do konca bitwy - koniec trzymania, wrog obszedl punkt, piechota daleko od punktu
            if (dep && _h439Frozen[ti])
            {
                var pt = _h439Pt[ti];
                if (pt != null)
                {
                    byte kill = 0;
                    if (_h439OnLast[ti] && !_dhOn[ti]) kill = 1;
                    else if (_dhOn[ti] && inf != null)
                    {
                        var q = t.QuerySystem;
                        Vec2 ev = q != null ? q.MedianTargetFormationPosition.AsVec2 : Vec2.Invalid;
                        Vec2 p0 = inf.CachedAveragePosition;
                        if (ev.IsValid && p0.IsValid && pt.At.Distance(ev) < H439Toward * p0.Distance(ev)) kill = 2;
                        else if (p0.IsValid && p0.Distance(pt.At) > H439MaxShift) kill = 3;
                    }
                    if (kill != 0)
                    {
                        _h439Pt[ti] = null;
                        if (kill == 1) _h439KillRel++; else if (kill == 2) _h439KillSide++; else _h439KillFar++;
                    }
                }
            }
            _h439OnLast[ti] = _dhOn[ti];
        }

        /// <summary>439-p (e): maks. liczba druzyn z zatrzymaniem Engage w tym samym takcie (okno TickEvery).</summary>
        private static void H439Peak()
        {
            int n = 0;
            for (int i = 0; i < MaxTeams; i++) if (_h439CutNow[i] && _dhRef[i] != null) n++;
            if (n > _h439PeakTeams) _h439PeakTeams = n;
        }

        /// <summary>439-p (d) (DhTick, glowny watek): miara "napastnik nie podchodzi" 291. Druzyna z zamrozonym punktem wzgorza (klucz DefenderSeeksHighGround TAK)
        /// - odleglosc najblizszego szyku wroga od STALEGO punktu (mediana minus pol glebokosci wroga) zamiast TeamGap; src = ten punkt. Inaczej TeamGap, src null.</summary>
        private static float H439StallGap(Team t, int ti, Settings c, float teamGap, out H439Point src)
        {
            src = null;
            if (c == null || !c.DefenderSeeksHighGround || !_h439Frozen[ti]) return teamGap;
            var pt = _h439Pt[ti];
            if (pt == null) return teamGap;
            float best = float.MaxValue;
            var teams = t.Mission != null ? t.Mission.Teams : null;
            int n = teams != null ? teams.Count : 0;
            for (int i = 0; i < n; i++)
            {
                var e = teams[i];
                if (!MissionTeams.Enemies(t, e)) continue;
                var fl = e.FormationsIncludingSpecialAndEmpty;
                int nf = fl != null ? fl.Count : 0;
                for (int j = 0; j < nf; j++)
                {
                    var f = fl[j];
                    if (f == null || f.CountOfUnits <= 0) continue;
                    float d = pt.At.Distance(f.CachedMedianPosition.AsVec2) - 0.5f * f.Depth;
                    if (d < best) best = d;
                }
            }
            if (best == float.MaxValue) return teamGap;
            src = pt;
            return best < 0f ? 0f : best;
        }

        /// <summary>439-p (d) (DhTick): zmiana miary 'napastnik nie podchodzi' (TeamGap / punkt 439) - true = podstawa odleglosci od nowa (zegar bez zmian).</summary>
        private static bool H439StallRebase(int ti, H439Point src, bool started)
        {
            if (!started) { _h439StallSrc[ti] = src; return false; }
            if (ReferenceEquals(src, _h439StallSrc[ti])) return false;
            _h439StallSrc[ti] = src;
            if (src != null) _h439StallPtTeams++;
            return true;
        }

        /// <summary>439-p (d) (DhTick): 'napastnik nie podchodzi' puscil druzyne przy mierze od punktu 439.</summary>
        private static void H439StallFired(int ti)
        {
            if (_h439StallSrc[ti] != null) _h439StallPtN++;
        }

        /// <summary>2.1 (glowny watek): >= prog ludzi GLOWNEJ piechoty z ciosem wrecz zadanym / otrzymanym w ostatnich 6 s (jak RBM FormationFightingInMelee na _mainInfantry).</summary>
        private static bool H439InMelee(Formation f, float now, float share, out bool baseHit)
        {
            baseHit = false;
            if (f == null || f.CountOfUnits <= 0) return false;
            _mlNow = now; _mlAll = _mlIn = 0;
            f.ApplyActionOnEachUnit(H439MeleeMan);
            if (_mlAll <= 0) return false;
            baseHit = _mlIn >= H439MeleeShare * _mlAll;   // 440: prog 439 bez profilu (licznik "wczesniej / pozniej")
            return _mlIn >= share * _mlAll;
        }

        private static readonly Action<Agent> H439MeleeMan = a =>
        {
            if (a == null || !a.IsActive()) return;
            _mlAll++;
            if (_mlNow - a.LastMeleeAttackTime < H439MeleeWin || _mlNow - a.LastMeleeHitTime < H439MeleeWin) _mlIn++;
        };

        /// <summary>Najblizszy PIESZY szyk wroga (jak FootGap) i odleglosc mediana - mediana minus pol glebokosci.</summary>
        private static float H439FootEnemy(Team team, Formation f, out Formation best)
        {
            best = null;
            float bd = float.MaxValue;
            var my = f.CachedMedianPosition.AsVec2;
            var teams = team.Mission != null ? team.Mission.Teams : null;
            int n = teams != null ? teams.Count : 0;
            for (int i = 0; i < n; i++)
            {
                var t = teams[i];
                if (!MissionTeams.Enemies(team, t)) continue;
                var fl = t.FormationsIncludingSpecialAndEmpty;
                int nf = fl != null ? fl.Count : 0;
                for (int j = 0; j < nf; j++)
                {
                    var e = fl[j];
                    if (e == null || e.CountOfUnits <= 0) continue;
                    var eq = e.QuerySystem;
                    if (eq == null || eq.IsCavalryFormation || eq.IsRangedCavalryFormation) continue;
                    float d = my.Distance(e.CachedMedianPosition.AsVec2) - 0.5f * (f.Depth + e.Depth);
                    if (d < bd) { bd = d; best = e; }
                }
            }
            return bd < 0f ? 0f : bd;
        }

        /// <summary>2.3 (glowny watek): punkt wzgorza dla glownej piechoty. why: 1 brak wzniesienia >= 2 m, 2 wrog blizej niz 60 m, 3 brak danych.</summary>
        private static H439Point H439Pick(Mission m, Team t, Formation inf, out byte why)
        {
            why = 3;
            var q = t.QuerySystem;
            if (q == null) return null;
            WorldPosition e = q.MedianTargetFormationPosition;
            Vec2 ev = e.AsVec2;
            if (!ev.IsValid) return null;
            Vec2 p0 = inf.CachedAveragePosition;
            if (!p0.IsValid) return null;
            float z0 = inf.CachedMedianPosition.GetNavMeshZ();
            if (float.IsNaN(z0) || float.IsInfinity(z0)) return null;
            float gap = p0.Distance(ev);
            if (gap < H439MinGap) { why = 2; return null; }
            why = 1;
            Vec2 dir = (ev - p0).Normalized();
            float maxShift = Math.Min(H439MaxShift, 0.5f * gap);
            H439Point best = null;
            float bestScore = float.MinValue;
            // 1. znaczniki HighGround sceny
            var ai = t.TeamAI;
            var tps = ai != null ? ai.TacticalPositions : null;
            int n = tps != null ? tps.Count : 0;
            for (int i = 0; i < n; i++)
            {
                var tp = tps[i];
                if (tp == null || tp.TacticalPositionType != TacticalPosition.TacticalPositionTypeEnum.HighGround) continue;
                WorldPosition wp = tp.Position;
                Vec2 pv = wp.AsVec2;
                if (!pv.IsValid) continue;
                float d = p0.Distance(pv);
                if (d > maxShift || pv.Distance(ev) < H439Toward * gap) continue;
                if (!tp.IsInsurmountable && (ev - pv).Normalized().DotProduct(tp.Direction) <= 0.5f) continue;
                if (!H439InField(m, pv)) continue;
                float rise = wp.GetNavMeshZ() - z0;
                if (float.IsNaN(rise) || rise < H439MinRise) continue;
                float score = rise - 0.02f * d;
                if (score > bestScore) { bestScore = score; best = new H439Point { Pos = wp, At = pv, Rise = rise, Shift = d, Src = 1 }; }
            }
            if (best != null) return best;
            // 2. stok za szykiem (zapytanie silnika - jak HighGroundCloseToForeseenBattleGround gry, kwadrat cofniety od wroga)
            float r = Math.Max(H439SlopeMin, Math.Min(H439SlopeMax, H439SlopeShare * gap));
            WorldPosition center = inf.CachedMedianPosition;
            center.SetVec2(p0 - dir * r);
            WorldPosition refPos = e;
            WorldPosition s = m.FindPositionWithBiggestSlopeTowardsDirectionInSquare(ref center, r, ref refPos);
            Vec2 sv = s.AsVec2;
            if (!sv.IsValid || s.GetNavMesh() == UIntPtr.Zero) return null;
            float ds = p0.Distance(sv);
            if (ds > maxShift || sv.Distance(ev) < H439Toward * gap || !H439InField(m, sv)) return null;
            float rs = s.GetNavMeshZ() - z0;
            if (float.IsNaN(rs) || rs < H439MinRise) return null;
            why = 0;
            return new H439Point { Pos = s, At = sv, Rise = rs, Shift = ds, Src = 2 };
        }

        private static bool H439InField(Mission m, Vec2 p)
        {
            if (!m.IsPositionInsideBoundaries(p)) return false;
            return m.GetClosestBoundaryPosition(p).Distance(p) >= H439EdgeMin;
        }

        // ------------------------------------------------------------ linia

        /// <summary>Battle291.Write (po linii 438): linia "Obronca trzyma (439)".</summary>
        internal static string H439Line(string kind)
        {
            var c = Settings.Current;
            bool k1 = c != null && c.DefenderHoldsUntilMeleeContact, k2 = c != null && c.DefenderSeeksHighGround;
            int pts = _h439PtMark + _h439PtSlope;
            var sb = new StringBuilder(1000);
            sb.Append("Obronca trzyma (439): misja ").Append(kind ?? "-")
              .Append(" | klucze: trzyma do zwarcia ").Append(k1 ? "TAK" : "NIE").Append(", szuka wzgorza ").Append(k2 ? "TAK" : "NIE")
              .Append(", obronca trzyma (291) ").Append(c != null && c.DefendersHoldTheirGround ? "TAK" : "NIE")
              .Append(" | 2.1 Engage RBM 75 m zatrzymany (wywolan) ").Append(Volatile.Read(ref _h439Cut))
              .Append(", bylby (klucz NIE) ").Append(Volatile.Read(ref _h439Dry))
              .Append(", bez ciecia: wrog bez pieszych i strzelcow (RBM, wywolan) ").Append(Volatile.Read(ref _h439CavOnly))
              .Append(", zwolniony (wywolan) ").Append(Volatile.Read(ref _h439FreeCalls))
              .Append("; zwolnienia (zdarzen): pieszy wrog <= ").Append(S1ContactGap.ToString("0", H439Inv)).Append("/40 m ").Append(_h439Near)
              .Append(", glowna piechota wrecz >= ").Append(((int)(H439MeleeShare * 100f)).ToString(H439Inv)).Append("% ").Append(_h439MeleeN)
              .Append(", powrot do trzymania po ").Append(H439RelHold.ToString("0", H439Inv)).Append(" s ").Append(_h439Back)
              .Append("; druzyn z zatrzymaniem ").Append(_h439CutTeams).Append(", naraz maks (ten sam takt) ").Append(_h439PeakTeams)
              .Append(", split obrony RBM w Engage bez zwolnienia ").Append(_h439SplitEngSec.ToString("0", H439Inv)).Append(" s")
              .Append(" | 2.3 punkty wzgorza ").Append(pts).Append(" (znacznik sceny ").Append(_h439PtMark).Append(", stok silnika ").Append(_h439PtSlope)
              .Append("; sr. wzniesienie ").Append(pts > 0 ? (_h439RiseSum / pts).ToString("0.0", H439Inv) : "-")
              .Append(" m, odleglosc ").Append(pts > 0 ? (_h439ShiftSum / pts).ToString("0", H439Inv) : "-")
              .Append(" m), bez punktu: brak wzniesienia ").Append(_h439PtNoRise).Append(", wrog blizej ").Append(H439MinGap.ToString("0", H439Inv)).Append(" m ").Append(_h439PtClose)
              .Append(", morska ").Append(_h439PtNaval).Append(", brak danych ").Append(_h439PtNoData)
              .Append("; rozkazy w punkt ").Append(Volatile.Read(ref _h439HighSet)).Append(", bylby (klucz NIE) ").Append(Volatile.Read(ref _h439HighDry))
              .Append("; punkt skasowany: koniec trzymania ").Append(_h439KillRel).Append(", wrog obszedl ").Append(_h439KillSide)
              .Append(", piechota dalej niz ").Append(H439MaxShift.ToString("0", H439Inv)).Append(" m ").Append(_h439KillFar)
              .Append("; 291 'napastnik nie podchodzi' od punktu: druzyn ").Append(_h439StallPtTeams).Append(", puszczen ").Append(_h439StallPtN)
              .Append(" | kontakt (glowna piechota <= ").Append(S1ContactGap.ToString("0", H439Inv)).Append(" m od pieszego wroga): druzyn ").Append(_h439CtN)
              .Append(", w trzymaniu (poprzedni takt) ").Append(_h439CtHold)
              .Append(", wysokosc wzgledem wroga sr. ").Append(_h439CtN > 0 ? (_h439CtDz / _h439CtN).ToString("0.0", H439Inv) : "-")
              .Append(" m (wyzej >= ").Append(H439MinRise.ToString("0", H439Inv)).Append(" m ").Append(_h439CtHi).Append(", nizej ").Append(_h439CtLo)
              .Append("), z punktem 439 ").Append(_h439CtPtN).Append(" (sr. ").Append(_h439CtPtN > 0 ? (_h439CtPtDz / _h439CtPtN).ToString("0.0", H439Inv) : "-")
              .Append(" m, na punkcie ").Append(_h439CtOnPt).Append(')')
              .Append(" | latka ").Append(_h439Patch).Append(" | potkniecia ").Append(_h439Stum).Append('.');
            return sb.ToString();
        }
    }
}
