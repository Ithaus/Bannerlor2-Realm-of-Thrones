using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// 537 E0 (projekt docs/projekty/KONIE-UPRZEZE.md, rozdz. 1 i 6; Jeff 15.11 "komplet kon + uprzaz = 1 powinien liczyc sie, ze zolnierz ma na czym jechac").
    /// PARA MARSZOWA = jeden kon (Stables.IsPlainMount) i jedna uprzaz, ktora do niego pasuje (SaddleLaw.Fits: ta sama rodzina, samo siodlo do kazdego konia;
    /// bez siodel transportowych CampCart.IsPackSaddle). Wymog pary = max(Difficulty konia, Difficulty uprzezy) wedlug Jazdy (ItemReq.SkillFor).
    /// E0 = SAMA DIAGNOSTYKA: linia doby "Pary marszowe (gracz)" w Armoury.log. Nic nie zmienia w grze (bez latek, bez wpisow predkosci i wagi, bez kluczy).
    /// Of(mp) liczy rekord bez pamieci - wolany tylko z Daily (raz na dobe, watek glowny). E1 dolozy pamiec i HUD na tym samym rekordzie.
    /// Zrodla (tabela 1.1 projektu):
    ///  - jazda gracza (kon we wzorcu): podglad DTE KitPreview (Rode / Harnessed na typ - po strazach wystawienia); bez podgladu - wlasny dobor ze zbrojowni;
    ///    jezdziec na koniu bez uprzezy (D6) i bez konia idzie do puli piechoty;
    ///  - piechota, ranni jezdzcy (D9: ranny potrzebuje pary), jezdzcy bez pary, bohaterowie bez konia: pula z ladunku + wolne sztuki zbrojowni po jezdzie (D11);
    ///  - bohaterowie z wlasnym koniem: poza pula (wlasne Equipment);
    ///  - partie rodu gracza (D3): tylko ladunek, bez podgladu DTE (jazda wedlug gry).
    /// Dobor (rozdz. 1.2): w obrebie rodziny kon rosnaco z uprzeza rodzinna rosnaco, samo siodlo na koncu; potem pary rosnaco wedlug wymogu i ludzie rosnaco
    /// wedlug Jazdy - kazdy bierze najlatwiejsza pare, ktora umie poprowadzic (najwieksza liczba jadacych).
    /// Potkniecia liczone (wyjatek = brak linii dla tej partii), bez wylacznika.
    /// E2 (klucz MarchPairsSpeed): predkosc gracza i rodu z tego samego rekordu - ApplySpeed (wpisy roznicy wobec gry), ColumnOf (kolumna MarchPace),
    /// RideQ (HorsePace: "Horse quality: spare horses" z koni w parach). Opis przy sekcji E2 nizej.
    /// E3 (klucz RiddenHarnessWeightless): waga uprzezy z ladunku na koniach pod piechota (WeightPostfix - "Harness on ridden horses") i udzwig z par
    /// (PackMarket: UnderPacks, "Saddled spare horses", "Ridden horses" gracza - CargoUnderMen / CapRiders). Opis przy sekcji E3 nizej.
    /// E6 (FootStable): stajnia piechoty gracza "Footmen's mounts" - trzecie zrodlo par (SStable, najpierw przy tym samym wymogu - Rank); kon w stajni poza
    /// NumberOfMounts gry (FreeHerd, Compute.FreeO, CapRiders licza go jak kon spoza ladunku), uprzaz poza waga ladunku. Rekord.RiddenBy mowi stajni, co ma stac.
    /// </summary>
    internal static class MarchPairs
    {
        private const byte SCargo = 0, SArm = 1, SStable = 2;   // 537 E6: SStable - stajnia piechoty "Footmen's mounts" (FootStable)
        /// <summary>E6: kolejnosc zrodel przy tym samym wymogu - stajnia, ladunek, zbrojownia (bez stajni jak dotad: ladunek przed zbrojownia). Stajnia najpierw:
        /// para, ktora juz w niej stoi, jedzie dalej (bez przerzucania sztuk tam i z powrotem przy kazdym przeliczeniu).</summary>
        private static int Rank(byte s) { return s == SStable ? 0 : s == SCargo ? 1 : 2; }
        // rodzaj czlowieka w puli piechoty
        private const byte KFoot = 0, KWoundedRider = 1, KCavWalk = 2, KHero = 3;

        // E2: Md i Q (szybkosc konia z modyfikatorem, HorsePace.Q) - jakosc koni w parach piechoty ("Horse quality: spare horses")
        private sealed class Lot { public ItemObject It; public ItemModifier Md; public float Q; public int Req; public int N; public byte Src; }
        // Q - suma szybkosci koni pary (E2); E3: UIt/UMd - uprzaz z LADUNKU w tej grupie par (zwolnienie wagi), UW - waga sztuki do kolejnosci (ciezsza najpierw)
        private sealed class PairLot { public int Req; public int N; public byte HSrc, USrc; public double Q; public ItemObject UIt; public ItemModifier UMd; public float UW;
            public readonly List<PairSub> Subs = new List<PairSub>(); }
        // E5: sztuki w grupie par (kon i uprzaz z modyfikatorem) - tylko do widoku "Ridden by"; liczby jadacych, Q i waga dalej z PairLot (bez zmian)
        private sealed class PairSub { public ItemObject HIt, UIt; public ItemModifier HMd, UMd; public int N, Left; }
        /// <summary>E5: kto jedzie na jakiej parze (kon + uprzaz, zrodlo) - widok "Ridden by" w ladunku i w Review the army. Tylko watek glowny czyta.</summary>
        internal sealed class RideLot { public CharacterObject Ch; public byte Kind; public ItemObject HIt, UIt; public ItemModifier HMd, UMd; public byte HSrc, USrc; public int Req, N; }
        /// <summary>E5: jazda z podgladu DTE (albo z wlasnego doboru) wedlug typu - z para, na koniu bez uprzezy, bez konia / kon zdjety.</summary>
        internal sealed class CavLot { public CharacterObject Ch; public int Pairs, NoHarness, NoHorse; }
        internal const byte SrcCargo = SCargo, SrcArmoury = SArm, SrcStable = SStable;
        internal const byte KindFoot = KFoot, KindWoundedRider = KWoundedRider, KindCavWalk = KCavWalk, KindHero = KHero, KindCav = 4;
        /// <summary>E3: uprzaz z ladunku na koniu pod piechurem (para przydzielona w Assign) - sztuka i liczba; waga liczona przez model przy wpisie wagi.</summary>
        internal struct HLot { public EquipmentElement E; public int N; }
        /// <summary>E2: konie pod piechota z par (szybkosc z modyfikatorem, liczba) - HorsePace liczy z nich "Horse quality: spare horses" i sufit kolumny.</summary>
        internal struct QLot { public float Q; public int N; }
        private sealed class ManLot { public CharacterObject Ch; public int Riding, Eff; public int N; public byte Kind; public int Got, Skill; }

        internal sealed class Rec
        {
            public bool Ok, AtSea, Preview; public string PreviewWhy;
            public int Men, Wounded, Heroes, Prisoners, Attached;
            // ladunek
            public int CMounts, CPlain, COtherMount, CPack, CLive, GameMounts, CHarFam, CHarSaddle, CHarOther, CPackSaddle;
            // zbrojownia DTE gracza
            public int AHorses, AHarness, AHarOther, AFreeHorses, AFreeHarness;
            // E6: stajnia piechoty gracza (FootStable): konie wierzchowe, uprzeze pasujace, inne sztuki (wroca do ladunku); jadacy na koniach ze stajni, pary
            // puli z koniem ze stajni, pary niewykorzystane z koniem ze stajni
            public int SMounts, SHarness, SOther, RideStableHorse, PoolPairsStable, UnusedStableHorse;
            // jazda
            public int CavMen, CavPairs, CavNoHarness, CavNoHorse, CavNoSaddle, CavWounded;
            public int HeroMounted, HeroNoHarness, HeroFoot;
            // pula piechoty
            public int PoolMen, PoolPairs, PoolPairsCargo, PoolPairsArm, PoolHorses, PoolHarness, Ride, RideCargoHorse, Walk, WalkNoHorse, WalkNoHarness, WalkSkill;
            public int PairReqMin = -1, PairReqMax = -1, UnusedPairs, UnusedReqMin = -1;
            public readonly List<string> SkillByType = new List<string>();
            // gra
            public int GameFoot, GameNum10, GameHerd, OurHerd, FreeHerd, PackLimit;
            public float GameFootF, OurFootF, GameHerdF, OurHerdF;
            // E1: braki Jazdy wedlug typu po angielsku (podpowiedz HUD), dzien i czas liczenia rekordu
            public readonly List<string> SkillLowEn = new List<string>();
            public int Day; public double Ms;
            /// <summary>E5: pary jadace (kto, kon, uprzaz) i pary niewykorzystane (Ch null) - widok "Ridden by"; jazda wedlug typu.</summary>
            public readonly List<RideLot> RiddenBy = new List<RideLot>(), UnusedBy = new List<RideLot>();
            public readonly List<CavLot> CavBy = new List<CavLot>();
            /// <summary>E2: konie pod piechota z puli (pary przydzielone w Assign) - szybkosc i liczba; rekord po opublikowaniu tylko czytany (watki tiku).</summary>
            public readonly List<QLot> RideQ = new List<QLot>();
            internal void AddRideQ(float q, int n)
            {
                if (n <= 0) return;
                for (int i = 0; i < RideQ.Count; i++) if (Math.Abs(RideQ[i].Q - q) < 0.001f) { var x = RideQ[i]; x.N += n; RideQ[i] = x; return; }
                RideQ.Add(new QLot { Q = q, N = n });
            }
            /// <summary>E3: uprzeze z ladunku na koniach pod piechota (pary jadace) - wpis wagi "Harness on ridden horses"; rekord po opublikowaniu tylko czytany.</summary>
            public readonly List<HLot> RideHarness = new List<HLot>();
            /// <summary>E3: pary niewykorzystane z koniem z ladunku (pod jencow - D2) i waga uprzezy par jadacych w chwili liczenia (linia doby, podpis).</summary>
            public int UnusedCargoHorse; public float RideHarnessKg;
            internal void AddRideHarness(ItemObject it, ItemModifier md, int n)
            {
                if (it == null || n <= 0) return;
                for (int i = 0; i < RideHarness.Count; i++)
                    if (RideHarness[i].E.Item == it && RideHarness[i].E.ItemModifier == md) { var x = RideHarness[i]; x.N += n; RideHarness[i] = x; return; }
                RideHarness.Add(new HLot { E = new EquipmentElement(it, md), N = n });
            }
            /// <summary>E3: jency na parach z koniem z ladunku (D2 - jeniec tez potrzebuje pary; najpierw pary z koniem z ladunku).</summary>
            public int PrisCargo { get { return AtSea ? 0 : Math.Min(Math.Max(0, Prisoners), Math.Max(0, UnusedCargoHorse)); } }
            /// <summary>E1: koni pod ludzmi (pary jazdy + pary puli); na morzu 0 - nikt nie jedzie (rozdz. 1.3). Ta sama liczba w linii doby i w HUD.</summary>
            public int Ridden { get { return AtSea ? 0 : CavPairs + Ride; } }
            /// <summary>E1: wszystkie konie wierzchowe - ladunek + zbrojownia DTE (mianownik HUD, D10 a).</summary>
            public int AllHorses { get { return CMounts + AHorses + SMounts; } }
            /// <summary>E1: wszystkie pasujace uprzeze - ladunek + zbrojownia DTE.</summary>
            public int AllHarness { get { return CHarFam + CHarSaddle + AHarness + SHarness; } }
        }

        private static int _stumbles, _stumblesDay;
        private static readonly HashSet<string> _errSites = new HashSet<string>();
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        internal static void Reset()
        {
            Interlocked.Exchange(ref _stumbles, 0); Interlocked.Exchange(ref _stumblesDay, 0);
            lock (_errSites) _errSites.Clear();
            _main = null; _mainKey = null; _mainAt = Environment.TickCount - 20000; _mainCount = 0;
            // E2: pamiec rodu, ostatnie liczenie gracza, liczniki odswiezen predkosci
            _clan = new ConcurrentDictionary<MobileParty, CRec>();
            _lastCalc = null; _invalidations = 0; _clanBuilds = 0; _applyCalls = 0;
            // E3: liczniki wpisu wagi (pola pamieci gry - stan sprawdzenia zostaje, to cecha wersji gry)
            _weightInval = 0; _wCalls = 0; _wHits = 0; _wLastMain = 0f;
        }

        /// <summary>E2: watki tiku tez tu trafiaja (model predkosci partii rodu) - liczniki Interlocked, miejsca pod blokada.</summary>
        private static void Stumble(string where, Exception e)
        {
            Interlocked.Increment(ref _stumbles); Interlocked.Increment(ref _stumblesDay);
            bool first; lock (_errSites) first = _errSites.Add(where);
            if (first) { try { Log.Error("MarchPairs." + where, e); } catch { } }
        }

        /// <summary>Wymog Jazdy sztuki (0 - bez wymogu albo wymog innej umiejetnosci).</summary>
        private static int Req(ItemObject it)
        {
            try
            {
                if (it == null || it.Difficulty <= 0) return 0;
                var sk = ItemReq.SkillFor(it);
                return sk != null && sk == DefaultSkills.Riding ? it.Difficulty : 0;
            }
            catch { return 0; }
        }

        private static int Riding(CharacterObject ch)
        {
            try { return ch != null ? ch.GetSkillValue(DefaultSkills.Riding) : 0; } catch { return 0; }
        }

        private static void AddLot(List<Lot> l, ItemObject it, int n, byte src) { AddLot(l, it, null, n, src); }

        /// <summary>E2: z modyfikatorem (jakosc konia w parze); uprzeze i zbrojownia DTE (slownik bez modyfikatorow) - md null.</summary>
        private static void AddLot(List<Lot> l, ItemObject it, ItemModifier md, int n, byte src)
        {
            if (it == null || n <= 0) return;
            int rq = Req(it);
            foreach (var x in l) if (x.It == it && x.Md == md && x.Src == src) { x.N += n; return; }
            float q = 0f;
            try { q = it.HasHorseComponent ? HorsePace.Q(it, md) : 0f; } catch { q = 0f; }
            l.Add(new Lot { It = it, Md = md, Q = q, Req = rq, N = n, Src = src });
        }

        /// <summary>Uprzaz pod zwyklego konia: 1 rodzinna (z pancerzem), 2 samo siodlo, 3 inna rodzina, 4 siodlo transportowe, 0 nie uprzaz.</summary>
        private static int HarnessKind(ItemObject it)
        {
            if (it == null || it.ItemType != ItemObject.ItemTypeEnum.HorseHarness) return 0;
            if (CampCart.IsPackSaddle(it)) return 4;
            if (!SaddleLaw.Fits(it)) return 3;
            return it.ArmorComponent != null ? 1 : 2;
        }

        /// <summary>Pary kon + uprzaz z puli (rozdz. 1.2 krok 1): kon rosnaco wedlug wymogu, najpierw uprzaz rodzinna (rosnaco), samo siodlo na koncu.</summary>
        private static List<PairLot> MakePairs(List<Lot> horses, List<Lot> harness)
        {
            var res = new List<PairLot>();
            horses.Sort((a, b) => a.Req != b.Req ? a.Req.CompareTo(b.Req) : Rank(a.Src).CompareTo(Rank(b.Src)));   // E6: Rank (bez stajni jak dotad)
            var fam = new List<Lot>(); var sad = new List<Lot>();
            foreach (var h in harness) { var c = new Lot { It = h.It, Md = h.Md, Q = h.Q, Req = h.Req, N = h.N, Src = h.Src }; if (h.It.ArmorComponent != null) fam.Add(c); else sad.Add(c); }
            Comparison<Lot> asc = (a, b) => a.Req != b.Req ? a.Req.CompareTo(b.Req) : Rank(a.Src).CompareTo(Rank(b.Src));
            fam.Sort(asc); sad.Sort(asc);
            var left = new int[horses.Count];
            for (int i = 0; i < horses.Count; i++) left[i] = horses[i].N;
            for (int pass = 0; pass < 2; pass++)
            {
                var pool = pass == 0 ? fam : sad;
                for (int i = 0; i < horses.Count; i++)
                {
                    var ho = horses[i];
                    for (int j = 0; j < pool.Count && left[i] > 0; j++)
                    {
                        var u = pool[j];
                        if (u.N <= 0 || !SaddleLaw.Fits(u.It, ho.It)) continue;
                        int t = Math.Min(left[i], u.N);
                        left[i] -= t; u.N -= t;
                        AddPair(res, Math.Max(ho.Req, u.Req), t, ho.Src, u.Src, t * (double)ho.Q, u, ho);
                    }
                }
            }
            // E3: przy tym samym wymogu i zrodle konia - ciezsza uprzaz z ladunku najpierw pod czlowieka (jak dawne HarnessOne "najciezsze najpierw");
            // liczba jadacych bez zmian (kolejnosc wymogu ta sama)
            res.Sort((a, b) => a.Req != b.Req ? a.Req.CompareTo(b.Req) : a.HSrc != b.HSrc ? Rank(a.HSrc).CompareTo(Rank(b.HSrc)) : b.UW.CompareTo(a.UW));
            return res;
        }

        /// <summary>E3: uprzaz z ladunku trzymana osobno wedlug sztuki i modyfikatora (wpis wagi); uprzaz ze zbrojowni DTE - bez sztuki (nie lezy w ladunku).</summary>
        private static void AddPair(List<PairLot> l, int req, int n, byte hs, byte us, double q, Lot u, Lot ho)
        {
            ItemObject uit = us == SCargo && u != null ? u.It : null;
            ItemModifier umd = uit != null ? u.Md : null;
            float uw = 0f;
            if (uit != null) { try { uw = uit.Weight; } catch { uw = 0f; } }
            PairLot pl = null;
            foreach (var x in l) if (x.Req == req && x.HSrc == hs && x.USrc == us && x.UIt == uit && x.UMd == umd) { x.N += n; x.Q += q; pl = x; break; }
            if (pl == null) { pl = new PairLot { Req = req, N = n, HSrc = hs, USrc = us, Q = q, UIt = uit, UMd = umd, UW = uw }; l.Add(pl); }
            // E5: sztuki tej grupy (kon i uprzaz z modyfikatorem, takze uprzaz ze zbrojowni) - tylko widok
            var hit = ho != null ? ho.It : null; var hmd = ho != null ? ho.Md : null; var sit = u != null ? u.It : null; var smd = u != null ? u.Md : null;
            foreach (var s in pl.Subs) if (s.HIt == hit && s.HMd == hmd && s.UIt == sit && s.UMd == smd) { s.N += n; return; }
            pl.Subs.Add(new PairSub { HIt = hit, HMd = hmd, UIt = sit, UMd = smd, N = n });
        }

        /// <summary>E5: dopisanie n par (sztuki u, grupa p) do listy widoku - te same pola sumowane.</summary>
        private static void AddRide(List<RideLot> l, CharacterObject ch, byte kind, PairLot p, PairSub u, int n)
        {
            if (n <= 0 || u == null) return;
            foreach (var x in l)
                if (x.Ch == ch && x.Kind == kind && x.HIt == u.HIt && x.HMd == u.HMd && x.UIt == u.UIt && x.UMd == u.UMd && x.HSrc == p.HSrc && x.USrc == p.USrc) { x.N += n; return; }
            l.Add(new RideLot { Ch = ch, Kind = kind, HIt = u.HIt, HMd = u.HMd, UIt = u.UIt, UMd = u.UMd, HSrc = p.HSrc, USrc = p.USrc, Req = p.Req, N = n });
        }

        /// <summary>E5: t par z grupy p dla czlowieka m - po sztukach grupy (kolejnosc doboru); liczby predkosci i wagi bez zmian (PairLot).</summary>
        private static void TakeSubs(PairLot p, int t, CharacterObject ch, byte kind, Rec r)
        {
            foreach (var s in p.Subs)
            {
                if (t <= 0) break;
                if (s.Left <= 0) continue;
                int k = Math.Min(t, s.Left);
                s.Left -= k; t -= k;
                AddRide(r.RiddenBy, ch, kind, p, s, k);
            }
        }

        private static int FirstRank(byte k) { return k == KCavWalk ? 0 : k == KWoundedRider ? 1 : 2; }

        /// <summary>t par z grupy pp dla czlowieka m - liczniki rekordu (wydzielone z Assign, 538).</summary>
        private static void Take(PairLot pp, int t, ManLot m, Rec r)
        {
            r.Ride += t;
            if (pp.N > 0) r.AddRideQ((float)(pp.Q / pp.N), t);   // E2: srednia szybkosc koni tej grupy par
            if (pp.HSrc == SCargo) r.RideCargoHorse += t;
            else if (pp.HSrc == SStable) r.RideStableHorse += t;   // E6: kon ze stajni (poza NumberOfMounts gry)
            if (pp.UIt != null) r.AddRideHarness(pp.UIt, pp.UMd, t);   // E3: uprzaz z ladunku na koniu pod czlowiekiem
            try { TakeSubs(pp, t, m.Ch, m.Kind, r); } catch (Exception e) { Stumble("Assign.RiddenBy", e); }   // E5: tylko widok
        }

        private static void AddMan(List<ManLot> l, CharacterObject ch, int n, byte kind)
        {
            if (ch == null || n <= 0) return;
            foreach (var x in l) if (x.Ch == ch && x.Kind == kind) { x.N += n; return; }
            int rd = Riding(ch);
            l.Add(new ManLot { Ch = ch, Riding = rd, Eff = Eff(rd, kind), N = n, Kind = kind });
        }

        /// <summary>538 (Jeff 15.11): piechur jedzie na koniu tylko w marszu, w bitwie walczy pieszo - wymog Jazdy konia i uprzezy do
        /// FootmenRidingWaiver (20) go nie dotyczy. Jazda "do doboru" = max(Jazda, prog); jezdzcy, ranni jezdzcy i bohaterowie bez zmian.</summary>
        private static int Eff(int riding, byte kind)
        {
            if (kind != KFoot && kind != KHero) return riding;   // 553 (Jeff 15.11): bohater bez wlasnego konia tez tylko przemieszcza sie po mapie
            var s = Settings.Current; int w = s != null ? s.FootmenRidingWaiver : 0;
            return w > riding ? w : riding;
        }

        /// <summary>Krok 3 rozdz. 1.2: ludzie rosnaco wedlug Jazdy, pary rosnaco wedlug wymogu; kazdy bierze najlatwiejsza pare, ktora umie poprowadzic.</summary>
        private static void Assign(List<ManLot> men, List<PairLot> pairs, Rec r)
        {
            // 538 (recenzja): jezdzcy bez pary, ranni jezdzcy i bohaterowie maja pierwszenstwo przed piechota z ulga Jazdy (inaczej rekrut z ulga
            // zabieralby jezdzcowi pare, 6d nie oddawaloby jej do zbrojowni i jazda walczylaby pieszo). Krok 1: oni malejaco wedlug Jazdy, kazdy
            // najtrudniejsza pare, ktora umie poprowadzic (latwe zostaja dla piechoty). Krok 2: piechota rosnaco wedlug Eff, najlatwiejsze pary.
            var pl = new int[pairs.Count];
            for (int i = 0; i < pairs.Count; i++) { pl[i] = pairs[i].N; foreach (var s in pairs[i].Subs) s.Left = s.N; }   // E5: sztuki grupy od nowa
            var first = new List<ManLot>(); var foot = new List<ManLot>();
            foreach (var m in men) (m.Kind == KFoot ? foot : first).Add(m);
            // jezdziec bez pary przed rannym jezdzcem (6d oddaje zbrojowni tylko pary KCavWalk), bohater na koncu; w rodzaju malejaco wedlug Jazdy.
            // Przy "najtrudniejsza para w zasiegu" liczba jadacych nie zalezy od kolejnosci ludzi (wymogi progowe).
            first.Sort((a, b) => a.Kind != b.Kind ? FirstRank(a.Kind).CompareTo(FirstRank(b.Kind)) : b.Eff.CompareTo(a.Eff));
            foreach (var m in first)
            {
                int want = m.N;
                // dwa przejscia: najpierw pary ze stajni/ladunku (6d liczy tylko je; wolne pary zbrojowni odejmuje osobno), potem pary calkiem ze zbrojowni
                for (int pass = 0; pass < 2 && want > 0; pass++)
                {
                    int top = pairs.Count - 1;
                    while (top >= 0 && pairs[top].Req > m.Eff) top--;   // najtrudniejszy blok wymogu w zasiegu tego czlowieka
                    while (top >= 0 && want > 0)
                    {
                        int lo = top; while (lo > 0 && pairs[lo - 1].Req == pairs[top].Req) lo--;
                        // w bloku jednego wymogu kolejnosc MakePairs (stajnia, ladunek, zbrojownia; ciezsza uprzaz najpierw)
                        for (int q = lo; q <= top && want > 0; q++)
                        {
                            if (pl[q] <= 0) continue;
                            bool arm = pairs[q].HSrc == SArm && pairs[q].USrc == SArm;
                            if (arm != (pass == 1)) continue;
                            int t = Math.Min(want, pl[q]);
                            want -= t; pl[q] -= t; m.Got += t;
                            Take(pairs[q], t, m, r);
                        }
                        top = lo - 1;
                    }
                }
            }
            foot.Sort((a, b) => a.Eff != b.Eff ? a.Eff.CompareTo(b.Eff) : a.Riding.CompareTo(b.Riding));   // 538: jazda do doboru
            int p = 0;
            foreach (var m in foot)
            {
                int want = m.N;
                while (want > 0 && p < pairs.Count)
                {
                    if (pl[p] <= 0) { p++; continue; }
                    if (pairs[p].Req > m.Eff) break;   // ta i dalsze pary za trudne dla tego czlowieka
                    int t = Math.Min(want, pl[p]);
                    want -= t; pl[p] -= t; m.Got += t;
                    Take(pairs[p], t, m, r);
                }
            }
            // E5: pary niewykorzystane po sztukach (pod jencow albo za trudne) - widok "Ridden by"
            try { for (int i = 0; i < pairs.Count; i++) if (pl[i] > 0) foreach (var s in pairs[i].Subs) if (s.Left > 0) AddRide(r.UnusedBy, null, 0, pairs[i], s, s.Left); }
            catch (Exception e) { Stumble("Assign.UnusedBy", e); }
            int unused = 0, umin = -1, ucargo = 0, ustable = 0;
            for (int i = 0; i < pairs.Count; i++) if (pl[i] > 0) { unused += pl[i]; if (pairs[i].HSrc == SCargo) ucargo += pl[i]; else if (pairs[i].HSrc == SStable) ustable += pl[i]; if (umin < 0 || pairs[i].Req < umin) umin = pairs[i].Req; }
            r.UnusedPairs = unused; r.UnusedReqMin = umin; r.UnusedCargoHorse = ucargo; r.UnusedStableHorse = ustable;
            // pieszo z powodu Jazdy: tyle, ile zostalo par (najlepsi z idacych pieszo - najblizej wymogu); reszta to brak konia albo uprzezy
            int walk = 0; foreach (var m in men) walk += m.N - m.Got;
            int skill = Math.Min(walk, unused);
            r.Walk = walk; r.WalkSkill = skill;
            int lack = walk - skill;
            r.WalkNoHorse = Math.Min(lack, Math.Max(0, r.PoolMen - r.PoolHorses));
            r.WalkNoHarness = lack - r.WalkNoHorse;
            if (skill > 0)
            {
                var byDesc = new List<ManLot>(men);
                byDesc.Sort((a, b) => b.Eff != a.Eff ? b.Eff.CompareTo(a.Eff) : b.Riding.CompareTo(a.Riding));
                int s = skill;
                foreach (var m in byDesc) { if (s <= 0) break; int w = m.N - m.Got; if (w <= 0) continue; int t = Math.Min(s, w); m.Skill = t; s -= t; }
                foreach (var m in byDesc)
                    if (m.Skill > 0)
                    {
                        string nm = m.Ch.Name != null ? m.Ch.Name.ToString() : m.Ch.StringId;
                        r.SkillByType.Add(nm + " (" + KindPl(m.Kind) + ", Jazda " + m.Riding + ") " + m.Skill);
                        r.SkillLowEn.Add(m.Skill + "x " + nm + " (Riding " + m.Riding + ")");
                    }
            }
        }

        private static string KindPl(byte k) { return k == KWoundedRider ? "ranny jezdziec" : k == KCavWalk ? "jezdziec bez pary" : k == KHero ? "bohater" : "piechur"; }

        private static float Herding(int men, int herd)
        {
            int h = herd - men;
            if (h <= 0) return 0f;
            if (men == 0) return -0.8f;
            return Math.Max(-0.8f, -0.3f * h / men);
        }

        /// <summary>Rekord par jednej partii (bez pamieci - E0 woła tylko raz na dobe). Gracz: jazda z podgladu DTE; partia rodu: sam ladunek.</summary>
        internal static Rec Of(MobileParty mp) { return Of(mp, false); }

        /// <summary>light (E1, pasek mapy): podglad DTE z pamieci (KitPreview.Last), gdy zgadza sie wersja zbrojowni i skladu - sama zmiana ladunku
        /// (jedzenie co godzine gry, przy szybkim czasie co chwile) nie liczy podgladu od nowa (10-17 ms). Linia doby - light false (pelny podglad).</summary>
        internal static Rec Of(MobileParty mp, bool light)
        {
            var r = new Rec();
            if (mp == null || mp.MemberRoster == null || mp.Party == null) return r;
            bool main = mp.IsMainParty;
            r.AtSea = mp.IsCurrentlyAtSea;
            var mr = mp.MemberRoster;
            r.Men = mr.TotalManCount; r.Wounded = mr.TotalWounded;
            r.Prisoners = mp.PrisonRoster != null ? mp.PrisonRoster.TotalManCount : 0;
            r.Attached = mp.AttachedParties != null ? mp.AttachedParties.Count : 0;

            var horses = new List<Lot>(); var harness = new List<Lot>();
            // ladunek
            var ir = mp.ItemRoster;
            if (ir != null)
            {
                r.GameMounts = ir.NumberOfMounts; r.CPack = ir.NumberOfPackAnimals; r.CLive = ir.NumberOfLivestockAnimals;
                for (int i = 0; i < ir.Count; i++)
                {
                    var el = ir.GetElementCopyAtIndex(i);
                    var it = el.EquipmentElement.Item;
                    if (it == null || el.Amount <= 0) continue;
                    if (KitPreview.RidingMount(it))
                    {
                        r.CMounts += el.Amount;
                        if (Stables.IsPlainMount(it)) { r.CPlain += el.Amount; AddLot(horses, it, el.EquipmentElement.ItemModifier, el.Amount, SCargo); }
                        else r.COtherMount += el.Amount;
                        continue;
                    }
                    switch (HarnessKind(it))
                    {
                        // E3: uprzaz z ladunku z modyfikatorem (zwolnienie wagi tej samej sztuki, ktora lezy w ladunku)
                        case 1: r.CHarFam += el.Amount; AddLot(harness, it, el.EquipmentElement.ItemModifier, el.Amount, SCargo); break;
                        case 2: r.CHarSaddle += el.Amount; AddLot(harness, it, el.EquipmentElement.ItemModifier, el.Amount, SCargo); break;
                        case 3: r.CHarOther += el.Amount; break;
                        case 4: r.CPackSaddle += el.Amount; break;
                    }
                }
            }

            // E6: stajnia piechoty gracza (FootStable, watek glowny - Of gracza wolany tylko z Main/Daily): konie i uprzeze jak z ladunku, zrodlo SStable
            // (kon poza NumberOfMounts gry, uprzaz poza waga ladunku); sztuki spoza par (SOther) wroca do ladunku przy nastepnym przeliczeniu stajni
            if (main)
            {
                try
                {
                    var st = FootStable.ForRecord();
                    if (st != null)
                        for (int i = 0; i < st.Count; i++)
                        {
                            var el = st.GetElementCopyAtIndex(i);
                            var it = el.EquipmentElement.Item;
                            if (it == null || el.Amount <= 0) continue;
                            if (KitPreview.RidingMount(it))
                            {
                                r.SMounts += el.Amount;
                                if (Stables.IsPlainMount(it)) AddLot(horses, it, el.EquipmentElement.ItemModifier, el.Amount, SStable);
                                else r.SOther += el.Amount;
                                continue;
                            }
                            int hk = HarnessKind(it);
                            if (hk == 1 || hk == 2) { r.SHarness += el.Amount; AddLot(harness, it, el.EquipmentElement.ItemModifier, el.Amount, SStable); }
                            else r.SOther += el.Amount;
                        }
                }
                catch (Exception e) { Stumble("Of.stajnia", e); }
            }

            var men = new List<ManLot>();
            // bohaterowie: wlasny kon poza pula; bez konia - do puli piechoty z wlasna Jazda
            // szeregowi: kon we wzorcu (IsMounted - ta sama miara co NumberOfMenWithHorse gry) - jazda; reszta - piechota (z rannymi, D9)
            var cavHealthy = new Dictionary<CharacterObject, int>();
            // recenzja 537: bez TroopsFightWithOwnKitOnly gra liczy KAZDEGO jezdzca wzorca (z rannymi - PartyBase.RecalculateNumberOfMenWithHorses) jako
            // jazde (Compute: CavO = cavG) - wtedy ranny jezdziec i jezdziec bez pary nie biora par z puli piechoty (ten sam czlowiek nie jest i jazda, i piechurem)
            bool cavRule = main && GarrisonKit.OwnKitOn;
            for (int i = 0; i < mr.Count; i++)
            {
                var el = mr.GetElementCopyAtIndex(i);
                var ch = el.Character;
                if (ch == null || el.Number <= 0) continue;
                if (ch.IsHero)
                {
                    r.Heroes += el.Number;
                    ItemObject hh = null, hu = null;
                    try { var eq = ch.Equipment; if (eq != null) { hh = eq[EquipmentIndex.Horse].Item; hu = eq[EquipmentIndex.HorseHarness].Item; } } catch { }
                    if (hh != null) { r.HeroMounted += el.Number; if (hu == null || !SaddleLaw.Fits(hu, hh)) r.HeroNoHarness += el.Number; }
                    else { r.HeroFoot += el.Number; AddMan(men, ch, el.Number, KHero); }
                    continue;
                }
                bool rider = false;
                try { rider = ch.IsMounted; } catch { rider = false; }
                if (!rider) { AddMan(men, ch, el.Number, KFoot); continue; }
                int wnd = Math.Min(el.Number, Math.Max(0, el.WoundedNumber));
                int ok = el.Number - wnd;
                if (main) { r.CavWounded += wnd; if (cavRule) AddMan(men, ch, wnd, KWoundedRider); if (ok > 0) cavHealthy[ch] = ok; }
                else r.CavMen += el.Number;   // partia rodu: jazda wedlug gry (bez podgladu DTE)
            }

            if (main)
            {
                // zbrojownia DTE gracza
                var arm = HorseFate.PlayerArmory();
                var aH = new Dictionary<ItemObject, int>(); var aU = new Dictionary<ItemObject, int>();
                if (arm != null)
                    for (int i = 0; i < arm.Count; i++)
                    {
                        var el = arm.GetElementCopyAtIndex(i);
                        var it = el.EquipmentElement.Item;
                        if (it == null || el.Amount <= 0) continue;
                        if (Stables.IsPlainMount(it)) { r.AHorses += el.Amount; int v; aH.TryGetValue(it, out v); aH[it] = v + el.Amount; }
                        else { int k = HarnessKind(it); if (k == 1 || k == 2) { r.AHarness += el.Amount; int v; aU.TryGetValue(it, out v); aU[it] = v + el.Amount; } else if (k == 3) r.AHarOther += el.Amount; }
                    }
                foreach (var kv in cavHealthy) r.CavMen += kv.Value;
                KitPreview.Result pv = null;
                // E1: przy CrashScribe sprzed 133a podglad pisze szum - jak ForArmoury / meldunek QM: bez podgladu (wlasny dobor ze zbrojowni)
                bool quiet = false;
                try { quiet = KitPreview.CsQuiet; } catch (Exception e) { Stumble("CsQuiet", e); }
                // recenzja 537 (1): dawne "if (quiet && pv == null) ... else" nadpisywalo dobry podglad z LastIfSame wynikiem Ok=false - pasek mapy liczyl jazde
                // wlasnym doborem, linia doby podgladem; teraz else tylko przy CrashScribe sprzed 133a
                if (quiet && light) { try { pv = LastIfSame(mp); } catch (Exception e) { Stumble("KitPreview.Last", e); pv = null; } }
                if (!quiet) pv = new KitPreview.Result { Why = "CrashScribe sprzed 133a - podglad pomijany" };
                else if (pv == null) { try { pv = KitPreview.Get(false); } catch (Exception e) { Stumble("KitPreview", e); pv = null; } }
                if (pv != null && pv.Ok)
                {
                    r.Preview = true;
                    // jazda z podgladu: para = kon + uprzaz (Harnessed); na koniu bez uprzezy (D6) i bez konia - do puli piechoty
                    foreach (var t in pv.Rows)
                    {
                        if (t.Troop == null || t.Troop.IsHero) continue;
                        r.CavPairs += t.Harnessed;
                        int noHar = Math.Max(0, t.Rode - t.Harnessed);
                        r.CavNoHarness += noHar; r.CavNoHorse += t.NoHorse; r.CavNoSaddle += t.NoSaddle;
                        int walkers = noHar + t.NoHorse + t.NoSaddle;
                        if (t.Harnessed > 0 || walkers > 0) r.CavBy.Add(new CavLot { Ch = t.Troop, Pairs = t.Harnessed, NoHarness = noHar, NoHorse = t.NoHorse + t.NoSaddle });   // E5
                        int healthy; cavHealthy.TryGetValue(t.Troop, out healthy);
                        walkers = Math.Min(walkers, healthy);   // tylko jezdzcy wzorca (piechur z podgladu nie jest jezdzcem bez pary)
                        if (cavRule) AddMan(men, t.Troop, walkers, KCavWalk);
                    }
                    // wolne sztuki zbrojowni po jezdzie (D11): zbrojownia minus to, na czym jazda wyjedzie
                    foreach (var kv in aH) { int u; pv.Rode.TryGetValue(kv.Key, out u); int f = kv.Value - u; if (f > 0) { r.AFreeHorses += f; AddLot(horses, kv.Key, f, SArm); } }
                    foreach (var kv in aU) { int u; pv.Saddles.TryGetValue(kv.Key, out u); int f = kv.Value - u; if (f > 0) { r.AFreeHarness += f; AddLot(harness, kv.Key, f, SArm); } }
                }
                else
                {
                    // bez podgladu: wlasny dobor ze zbrojowni dla zdrowych jezdzcow wzorca (ta sama regula pary), reszta zbrojowni do puli piechoty
                    r.PreviewWhy = pv != null ? pv.Why : "podglad DTE nie policzony";
                    var ah = new List<Lot>(); var au = new List<Lot>();
                    foreach (var kv in aH) AddLot(ah, kv.Key, kv.Value, SArm);
                    foreach (var kv in aU) AddLot(au, kv.Key, kv.Value, SArm);
                    var cp = MakePairs(ah, au);
                    var cm = new List<ManLot>();
                    foreach (var kv in cavHealthy) AddMan(cm, kv.Key, kv.Value, KCavWalk);
                    var tmp = new Rec();
                    Assign(cm, cp, tmp);
                    r.CavPairs = tmp.Ride;
                    if (cavRule) foreach (var m in cm) AddMan(men, m.Ch, m.N - m.Got, KCavWalk);
                    // E5: jazda z wlasnego doboru - pary po sztukach (kon i uprzaz ze zbrojowni) i typy
                    foreach (var x in tmp.RiddenBy) { x.Kind = KindCav; r.RiddenBy.Add(x); }
                    foreach (var m in cm) if (m.N > 0) r.CavBy.Add(new CavLot { Ch = m.Ch, Pairs = m.Got, NoHarness = 0, NoHorse = m.N - m.Got });
                    // zuzyte pary jazdy zdejmujemy z puli zbrojowni DOKLADNIE po sztukach, na ktorych jazda jedzie (tmp.RiddenBy - jak pv.Rode/pv.Saddles w galezi
                    // z podgladem). Recenzja 537 (2): dawne "pierwsze N z listy" bralo uprzeze w kolejnosci slownika (MakePairs sortuje tylko kopie uprzezy),
                    // wiec do puli piechoty szla uprzaz spod jezdzca. Gdy sztuki widoku nie skladaja sie na tmp.Ride (potkniecie TakeSubs) - dawna regula liczby.
                    var uH = new Dictionary<ItemObject, int>(); var uU = new Dictionary<ItemObject, int>();
                    int subN = 0;
                    foreach (var x in tmp.RiddenBy)
                    {
                        if (x.N <= 0) continue;
                        subN += x.N;
                        if (x.HIt != null) { int v; uH.TryGetValue(x.HIt, out v); uH[x.HIt] = v + x.N; }
                        if (x.UIt != null) { int v; uU.TryGetValue(x.UIt, out v); uU[x.UIt] = v + x.N; }
                    }
                    if (subN == tmp.Ride)
                    {
                        foreach (var kv in aH) { int u; uH.TryGetValue(kv.Key, out u); int f = kv.Value - u; if (f > 0) { r.AFreeHorses += f; AddLot(horses, kv.Key, f, SArm); } }
                        foreach (var kv in aU) { int u; uU.TryGetValue(kv.Key, out u); int f = kv.Value - u; if (f > 0) { r.AFreeHarness += f; AddLot(harness, kv.Key, f, SArm); } }
                    }
                    else
                    {
                        // 538: jazda bierze najtrudniejsze pary w zasiegu - zdejmujemy od konca list (ah/au rosnaco po wymogu po MakePairs)
                        int usedH = tmp.Ride, usedU = tmp.Ride;
                        ah.Reverse(); au.Reverse();
                        foreach (var x in ah) { int t = Math.Min(usedH, x.N); usedH -= t; int f = x.N - t; if (f > 0) { r.AFreeHorses += f; AddLot(horses, x.It, f, SArm); } }
                        foreach (var x in au) { int t = Math.Min(usedU, x.N); usedU -= t; int f = x.N - t; if (f > 0) { r.AFreeHarness += f; AddLot(harness, x.It, f, SArm); } }
                    }
                }
            }

            // pula piechoty
            foreach (var m in men) r.PoolMen += m.N;
            foreach (var h in horses) r.PoolHorses += h.N;
            foreach (var u in harness) r.PoolHarness += u.N;
            var pairs = MakePairs(horses, harness);
            foreach (var p in pairs)
            {
                r.PoolPairs += p.N;
                if (p.HSrc == SCargo) r.PoolPairsCargo += p.N; else if (p.HSrc == SStable) r.PoolPairsStable += p.N; else r.PoolPairsArm += p.N;
                if (r.PairReqMin < 0 || p.Req < r.PairReqMin) r.PairReqMin = p.Req;
                if (p.Req > r.PairReqMax) r.PairReqMax = p.Req;
            }
            if (r.AtSea) { r.Walk = r.PoolMen; }   // na morzu nikt nie jedzie (rozdz. 1.3)
            else Assign(men, pairs, r);

            // gra wobec nas (bez mokra i perkow - wartosci na sucho)
            r.GameFoot = mp.Party.NumberOfMenWithoutHorse;
            r.GameNum10 = Math.Min(r.GameFoot, r.GameMounts);
            r.GameHerd = r.CPack + r.CLive + Math.Max(0, r.GameMounts - r.GameNum10);
            r.FreeHerd = Math.Max(0, r.CMounts + r.SMounts - r.RideCargoHorse - r.RideStableHorse);   // E6: kon ze stajni bez jezdzca tez idzie w stadzie
            r.OurHerd = r.CPack + r.CLive + r.FreeHerd;
            r.PackLimit = Math.Max(0, r.Men - r.CLive - r.FreeHerd);
            if (r.Men > 0)
            {
                r.GameFootF = 0.15f * r.GameNum10 / r.Men;
                r.OurFootF = 0.15f * r.Ride / r.Men;
            }
            r.GameHerdF = Herding(r.Men, r.GameHerd);
            r.OurHerdF = Herding(r.Men, r.OurHerd);
            try { r.RideHarnessKg = r.AtSea ? 0f : HarnessKg(mp, r); } catch (Exception e) { Stumble("Of.HarnessKg", e); r.RideHarnessKg = 0f; }   // E3
            r.Ok = true;
            return r;
        }

        // ---------------------------------------------------------------- E1: pamiec rekordu gracza (jeden rekord dla linii doby i HUD)
        private static Rec _main;
        private static string _mainKey;
        private static int _mainAt;              // Environment.TickCount ostatniego liczenia
        private static int _mainCount;           // ile razy liczony od startu sesji (linia doby)

        /// <summary>Klucz pamieci: wersje zbrojowni DTE, skladu, jencow i ladunku, dzien, morze, prawo siodla. Tanie (bez liczenia).</summary>
        private static string MainKey(MobileParty mp)
        {
            var arm = HorseFate.PlayerArmory();
            var ir = mp.ItemRoster;
            return (arm != null ? arm.VersionNo : -1) + "|" + mp.MemberRoster.VersionNo + "|" + (mp.PrisonRoster != null ? mp.PrisonRoster.VersionNo : -1)
                   + "|" + (ir != null ? ir.VersionNo : -1) + "|" + (int)CampaignTime.Now.ToDays + "|" + (mp.IsCurrentlyAtSea ? 1 : 0) + (SaddleLaw.On ? 1 : 0)
                   + "|" + FootStable.Version   // E6: stajnia piechoty
                   + "|" + (Settings.Current != null ? Settings.Current.FootmenRidingWaiver : 0);   // 538: prog ulgi Jazdy piechoty (MCM)
        }

        /// <summary>Recenzja 537 (3): konie i uprzeze ZBROJOWNI DTE pod piechota, jezdzcami bez pary i bohaterami (pary D11, zrodlo SArm,
        /// bez KindCav i rannych jezdzcow - ci sa juz w Rode/Saddles podgladu wagi ForWeight) wedlug przedmiotu - StashWeight dopisuje je do listy "na ludziach" (waga i Escrow), zeby uprzaz
        /// pod piechurem ze zbrojowni nie wazyla, a ta sama z ladunku nie (E3). sig - podpis do klucza pamieci StashWeight. null - brak rekordu albo klucze
        /// MarchPairsSpeed i RiddenHarnessWeightless NIE (wtedy par nie ma w predkosci ani udzwigu). Watek glowny (Main(false) - pamiec najwyzej raz na 1 s).</summary>
        internal static Dictionary<ItemObject, int> FootArmouryWorn(out string sig)
        {
            sig = "-";
            if (!Needed) return null;
            var r = Main(false);
            if (r == null || !r.Ok || r.AtSea) return null;
            var d = new Dictionary<ItemObject, int>();
            int h = 0, u = 0;
            foreach (var x in r.RiddenBy)
            {
                if (x == null || x.N <= 0 || x.Kind == KindCav || x.Kind == KindWoundedRider) continue;   // ranny jezdziec: ForWeight daje mu kit jak zdrowemu (D4) - juz w Rode/Saddles
                if (x.HSrc == SArm && x.HIt != null) { int v; d.TryGetValue(x.HIt, out v); d[x.HIt] = v + x.N; h += x.N; }
                if (x.USrc == SArm && x.UIt != null) { int v; d.TryGetValue(x.UIt, out v); d[x.UIt] = v + x.N; u += x.N; }
            }
            long acc = 0;
            foreach (var kv in d) acc += (long)System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(kv.Key) * 1000003L + kv.Value;   // podpis tresci (bez kolejnosci)
            sig = h + ":" + u + ":" + acc;
            return d;
        }

        /// <summary>E1: rekord par gracza z pamieci. Liczony od nowa najwyzej raz na 1 s i tylko po zmianie klucza, albo co 10 s (sprzet bohaterow,
        /// przelaczniki podgladu - bez wersji); force - od razu (linia doby). Przy otwartej zbrojowni (Escrow chowa sztuki) i w misji - ostatni rekord.
        /// Watek glowny (takt doby, pasek mapy). Nigdy nie rzuca; null - jeszcze nic nie policzone.</summary>
        internal static Rec Main(bool force)
        {
            try
            {
                var mp = MobileParty.MainParty;
                if (Campaign.Current == null || mp == null || !mp.IsActive || mp.MemberRoster == null) return _main;
                if (QuartermasterEscrow.Active || TaleWorlds.MountAndBlade.Mission.Current != null) return _main;
                int now = Environment.TickCount;
                int since = unchecked(now - _mainAt);
                string key = null;
                if (!force)
                {
                    if (_main != null && since < 1000) return _main;
                    key = MainKey(mp);
                    if (_main != null && key == _mainKey && since < 10000) return _main;
                }
                else key = MainKey(mp);
                long t0 = Stopwatch.GetTimestamp();
                Rec r = null;
                try { r = Of(mp, !force); } catch (Exception e) { Stumble("Of(gracz)", e); }
                _mainAt = now;
                if (r == null || !r.Ok) return _main;   // potkniecie - zostaje stary rekord (HUD bez migania), proba za 1 s
                r.Day = (int)CampaignTime.Now.ToDays;
                r.Ms = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
                // klucz po liczeniu: podglad DTE mogl podbic wersje (bez petli - nastepne liczenie dopiero po zmianie albo za 10 s)
                try { _mainKey = MainKey(mp); } catch { _mainKey = key; }
                // E2: zmiana liczb predkosci (jazda, pary piechoty, stado, jakosc koni) - gra liczy predkosc od nowa przy nastepnym wolaniu (pamiec predkosci
                // gry trzyma wynik do zmiany wersji partii, a nasz rekord przychodzi do 1 s pozniej); watek glowny, tylko przy zmianie
                // E3: to samo dla wagi i udzwigu (uprzeze par jadacych, jezdzcy, luzaki pod jukami) - pamiec wagi gry trzymana do zmiany wersji partii
                var old = _main;
                _main = r; _mainCount++;
                if ((SpeedOn || WeightOn) && (old == null || SpeedSig(old) != SpeedSig(r))) Invalidate(mp);
                return r;
            }
            catch (Exception e) { Stumble("Main", e); return _main; }
        }

        /// <summary>E1: KitPreview.Last, gdy udany i z ta sama wersja zbrojowni DTE i skladu (poczatek klucza KitPreview.Key: "zbrojownia|sklad|"); inaczej null.</summary>
        private static KitPreview.Result LastIfSame(MobileParty mp)
        {
            var l = KitPreview.Last;
            if (l == null || !l.Ok || l.Key == null) return null;
            var arm = QuartermasterLaw.DteArmory();
            string pre = (arm != null ? arm.VersionNo : -1) + "|" + mp.MemberRoster.VersionNo + "|";
            return l.Key.StartsWith(pre, StringComparison.Ordinal) ? l : null;
        }

        /// <summary>Ostatni rekord gracza bez liczenia (podpowiedz HUD).</summary>
        internal static Rec Last { get { return _main; } }

        // ---------------------------------------------------------------- E2: predkosc z par (projekt rozdz. 1.3; gracz i partie jego rodu - D3)
        // Trzy wpisy jako ROZNICA wobec gry (te same wzory co DefaultPartySpeedCalculatingModel.CalculateLandBaseSpeed 1.4.8, z mokrem -30% i perkami):
        //  - "Riders without harness": gra liczy jazde z NumberOfMenWithHorse (wzorzec, z rannymi); gracz - jezdzcy z para (jazda z podgladu DTE + bohaterowie
        //    na wlasnym koniu); reszta zdejmuje "Cavalry" (-0.3 x n / N, mokro x0.7) i jest w puli piechoty (D6, D9). Tylko przy TroopsFightWithOwnKitOnly
        //    (bez niej DTE daje konia wzorca - jak RidersWithoutHorseWalk). Zastepuje u gracza "Riders without horses" HorsePace (ten sam czlowiek raz).
        //  - "Footmen on horses: ...": num10 gry = min(piesi, NumberOfMounts) zastapione parami puli piechoty (z Unhorsed - D11 wolne pary zbrojowni po jezdzie);
        //    zastepuje u gracza "Footmen on horses (riders without horses)" / "(armoury)" HorsePace. Doczepione partie armii - wedlug gry (bez zmian).
        //  - "Herding: horses without riders": stado gry = juczne + bydlo + max(0, NumberOfMounts - num10); nasze - konie ladunku bez jezdzca z pary;
        //    z perkiem Shepherd jak gra. Obowiazkowe (inaczej kon bez uprzezy "jedzie" w stadzie gry).
        // Gracz: rekord Main z pamieci (bez liczenia w modelu predkosci - watki tiku); odswiezany na watku glownym (pasek mapy co 0.2 s, takze przy wylaczonym
        // HUD, i linia doby), zmiana liczb -> Invalidate (gra liczy predkosc od nowa). Partia rodu: rekord z ladunku (bez podgladu DTE) w pamieci po kluczu
        // wersji (liczony tam, gdzie pyta model - tanie, bez stanu wspolnego poza ConcurrentDictionary). Na morzu - nic (nikt nie jedzie).

        private sealed class CRec { public int Key; public Rec R; }
        private static ConcurrentDictionary<MobileParty, CRec> _clan = new ConcurrentDictionary<MobileParty, CRec>();
        private static long _clanBuilds, _applyCalls;
        private static int _invalidations;
        private static int _pureState;   // 0 nie sprawdzane, 1 pole jest, 2 brak pola (linia doby)
        private static AccessTools.FieldRef<MobileParty, int> _pureVer;
        private static TextObject _txFootLess, _txFootMore, _txCav, _txHerd;
        private static TextObject TxFootLess { get { return _txFootLess ?? (_txFootLess = new TextObject("{=!}Footmen on horses: no harness or skill")); } }
        private static TextObject TxFootMore { get { return _txFootMore ?? (_txFootMore = new TextObject("{=!}Footmen on horses: armoury pairs")); } }
        private static TextObject TxCav { get { return _txCav ?? (_txCav = new TextObject("{=!}Riders without harness")); } }
        private static TextObject TxHerd { get { return _txHerd ?? (_txHerd = new TextObject("{=!}Herding: horses without riders")); } }
        private static TextObject _txFootStable;
        private static TextObject TxFootStable { get { return _txFootStable ?? (_txFootStable = new TextObject("{=!}Footmen on horses: footmen's mounts")); } }   // E6

        /// <summary>E2: klucz MarchPairsSpeed (NOWY).</summary>
        internal static bool SpeedOn { get { var s = Settings.Current; return s != null && s.MarchPairsSpeed; } }

        /// <summary>E2: liczenie korekt jednej partii (te same liczby we wpisach, w kolumnie i w linii doby).</summary>
        internal sealed class Calc
        {
            public int N, CavG, CavO, FootG, MountsG, Num10G, Num10O, HerdG, HerdO, RideO, FreeO, AttRide, Att;
            public float Wet = 1f, Nomad, Shep, DCav, DFoot, DHerd;
            public bool CavRule;
        }
        private static volatile Calc _lastCalc;   // ostatnie liczenie gracza w modelu predkosci (linia doby)

        private static bool InClan(MobileParty x)
        {
            if (x.IsGarrison || x.IsCaravan || x.IsVillager || x.IsMilitia) return false;
            var lh = x.LeaderHero; var pc = Clan.PlayerClan;
            return lh != null && pc != null && lh.Clan == pc;
        }

        /// <summary>E2: rekord par do predkosci (null - klucz wylaczony, morze, poza zasiegiem, nic nie policzone). Gracz - ostatni rekord Main bez liczenia;
        /// partia rodu - pamiec po kluczu wersji (liczona tu). Dowolny watek.</summary>
        internal static Rec SpeedRec(MobileParty mp)
        {
            if (!SpeedOn) return null;
            return PairsRec(mp);
        }

        /// <summary>E3: rekord par bez wzgledu na klucz predkosci (waga i udzwig - klucz RiddenHarnessWeightless). Ten sam zasieg i ta sama pamiec co SpeedRec.
        /// Dowolny watek.</summary>
        internal static Rec PairsRec(MobileParty mp)
        {
            if (mp == null || mp.MemberRoster == null || mp.Party == null || mp.IsCurrentlyAtSea) return null;
            if (mp.IsMainParty) { var r = _main; return r != null && r.Ok && !r.AtSea ? r : null; }
            if (!mp.IsActive || !InClan(mp)) return null;
            return ClanRec(mp);
        }

        private static Rec ClanRec(MobileParty x)
        {
            int key;
            unchecked
            {
                key = x.MemberRoster.VersionNo;
                key = key * 397 + (x.ItemRoster != null ? x.ItemRoster.VersionNo : -1);
                key = key * 397 + (x.PrisonRoster != null ? x.PrisonRoster.VersionNo : -1);   // E3: jency na parach (luzaki pod jukami)
                key = key * 397 + (Settings.Current != null ? Settings.Current.FootmenRidingWaiver : 0);   // 538: prog ulgi z MCM
                key = key * 397 + (int)CampaignTime.Now.ToDays;   // Jazda bohaterow i ludzi (awans zmienia wersje skladu, nauka - nie)
            }
            CRec c;
            var d = _clan;
            if (d.TryGetValue(x, out c) && c != null && c.Key == key) return c.R;
            Rec r = null;
            try { r = Of(x); } catch (Exception e) { Stumble("Of(rod, predkosc)", e); }
            if (r == null || !r.Ok) return null;
            d[x] = new CRec { Key = key, R = r };   // podmiana w calosci
            Interlocked.Increment(ref _clanBuilds);
            return r;
        }

        /// <summary>Podpis liczb predkosci rekordu (zmiana = Invalidate).</summary>
        private static int SpeedSig(Rec r)
        {
            unchecked
            {
                int h = r.CavPairs;
                h = h * 397 + r.HeroMounted; h = h * 397 + r.Ride; h = h * 397 + r.RideCargoHorse; h = h * 397 + r.CMounts;
                h = h * 397 + r.UnusedPairs; h = h * 397 + r.PoolMen; h = h * 397 + (r.AtSea ? 1 : 0);
                foreach (var q in r.RideQ) { h = h * 397 + (int)(q.Q * 10f); h = h * 397 + q.N; }
                // E3: waga i udzwig - uprzeze par jadacych, pary pod jencami, prawo wagi (przelaczenie klucza tez liczy wage od nowa)
                h = h * 397 + (int)(r.RideHarnessKg * 10f); h = h * 397 + r.UnusedCargoHorse; h = h * 397 + r.Prisoners;
                h = h * 397 + (WeightOn ? 1 : 0) + (SpeedOn ? 2 : 0);
                h = h * 397 + r.SMounts; h = h * 397 + r.RideStableHorse; h = h * 397 + r.SHarness;   // E6: stajnia piechoty
                return h;
            }
        }

        /// <summary>Pamiec predkosci gry gracza (MobileParty._partyPureSpeedLastCheckVersion = -1: CalculateSpeedForPartyUnified liczy baze i wynik od nowa).
        /// Watek glowny, tylko przy zmianie liczb. Bez pola - linia doby mowi BRAK (predkosc dojdzie przy nastepnej zmianie wersji partii).</summary>
        /// <summary>537 E4: odswiezenie pamieci wagi i predkosci gry po zmianie wagi zbrojowni (StashWeight.Refresh - watek glowny, tylko przy zmianie wyniku).</summary>
        internal static void InvalidateWeight(MobileParty mp) { if (mp != null) Invalidate(mp); }

        private static void Invalidate(MobileParty mp)
        {
            // E3: pamiec wagi gry (MobileParty._partyWeightLastCheckVersionNo = -1: TotalWeightCarried liczy wage od nowa; NIE wersja partii - bez kaskady
            // VersionNo). Predkosc bierze wage z tej pamieci, wiec po wadze takze predkosc od nowa (pole wyzej).
            if (_weightState != 2)
            {
                try
                {
                    if (_weightVer == null)
                    {
                        if (AccessTools.DeclaredField(typeof(MobileParty), "_partyWeightLastCheckVersionNo") == null) _weightState = 2;
                        else { _weightVer = AccessTools.FieldRefAccess<MobileParty, int>("_partyWeightLastCheckVersionNo"); _weightState = 1; }
                    }
                    if (_weightVer != null) { _weightVer(mp) = -1; _weightInval++; }
                }
                catch (Exception e) { Stumble("Invalidate.waga", e); }
            }
            if (_pureState == 2) return;
            try
            {
                if (_pureVer == null)
                {
                    if (AccessTools.DeclaredField(typeof(MobileParty), "_partyPureSpeedLastCheckVersion") == null) { _pureState = 2; return; }
                    _pureVer = AccessTools.FieldRefAccess<MobileParty, int>("_partyPureSpeedLastCheckVersion");
                    _pureState = 1;
                }
                _pureVer(mp) = -1;
                _invalidations++;
            }
            catch (Exception e) { Stumble("Invalidate", e); }
        }

        private static float Herd(int men, int herd) { return Herding(men, herd); }

        /// <summary>E2: korekty jednej partii z rekordu r. Liczby gry (piesi, luzaki, jazda, stado, z doczepionymi) zawsze swieze; nasze z rekordu
        /// (u gracza do 1 s starsze - przyciete do skladu teraz). Wzory jak gra (dekompilacja 1.4.8).</summary>
        internal static Calc Compute(MobileParty mp, Rec r)
        {
            var c = new Calc();
            var ir = mp.ItemRoster;
            int menM = mp.MemberRoster.TotalManCount;
            int cavG = mp.Party.NumberOfMenWithHorse;
            int footM = mp.Party.NumberOfMenWithoutHorse;
            int mountsM = ir != null ? ir.NumberOfMounts : 0;
            int herdBase = ir != null ? ir.NumberOfPackAnimals + ir.NumberOfLivestockAnimals : 0;
            int n = menM, footA = 0, mountsA = 0;
            var att = mp.AttachedParties;
            if (att != null)
                for (int i = 0; i < att.Count; i++)
                {
                    var ap = att[i];
                    if (ap == null || ap.MemberRoster == null || ap.Party == null) continue;
                    c.Att++;
                    n += ap.MemberRoster.TotalManCount;
                    footA += ap.Party.NumberOfMenWithoutHorse;
                    var ar = ap.ItemRoster;
                    if (ar != null) { mountsA += ar.NumberOfMounts; herdBase += ar.NumberOfPackAnimals + ar.NumberOfLivestockAnimals; }
                }
            c.N = n; c.CavG = cavG;
            if (n <= 0) return c;
            c.FootG = footM + footA; c.MountsG = mountsM + mountsA;
            c.Num10G = Math.Min(c.FootG, c.MountsG);
            c.HerdG = herdBase + Math.Max(0, c.MountsG - c.Num10G);
            c.CavRule = mp.IsMainParty && GarrisonKit.OwnKitOn;
            c.CavO = c.CavRule ? Math.Min(cavG, r.CavPairs + r.HeroMounted) : cavG;
            int poolM = Math.Max(0, menM - c.CavO);
            c.RideO = Math.Min(r.Ride, poolM);
            int rideCargo = Math.Min(r.RideCargoHorse + r.RideStableHorse, c.RideO);   // E6: konie ze stajni pod piechota tez nie ida w stadzie
            c.AttRide = Math.Min(footA, mountsA);
            c.Num10O = c.RideO + c.AttRide;
            c.FreeO = Math.Max(0, r.CMounts + r.SMounts - rideCargo);
            c.HerdO = herdBase + c.FreeO + (mountsA - c.AttRide);
            c.Wet = HorsePace.WetOf(mp);
            try { var lh = mp.LeaderHero; var pk = DefaultPerks.Riding.NomadicTraditions; if (lh != null && pk != null && lh.GetPerkValue(pk)) c.Nomad = pk.PrimaryBonus; } catch { c.Nomad = 0f; }
            try { var pk = DefaultPerks.Riding.Shepherd; if (pk != null && mp.HasPerk(pk)) c.Shep = pk.PrimaryBonus; } catch { c.Shep = 0f; }
            c.DCav = -0.3f * (cavG - c.CavO) / n * c.Wet;
            c.DFoot = 0.15f * (c.Num10O - c.Num10G) / n * (c.Wet + c.Nomad);
            c.DHerd = (Herd(n, c.HerdO) - Herd(n, c.HerdG)) * (1f + c.Shep);
            return c;
        }

        /// <summary>E2: MarchPace.SpeedPostfix PRZED sufitem kolumny (obok HorsePace.Apply): trzy wpisy roznicy. Dowolny watek; tylko odczyt pamieci (gracz).</summary>
        internal static void ApplySpeed(MobileParty mp, ref ExplainedNumber result)
        {
            if (mp == null || !SpeedOn) return;
            if (!SpeedDepth.OutermostFinal) return;
            try
            {
                var r = SpeedRec(mp);
                if (r == null) return;
                var c = Compute(mp, r);
                if (c.N <= 0) return;
                Interlocked.Increment(ref _applyCalls);
                if (mp.IsMainParty) _lastCalc = c;
                if (Math.Abs(c.DCav) > 1e-6f) result.AddFactor(c.DCav, TxCav);
                // E6: konie stajni piechoty sa poza NumberOfMounts gry - wpis dodatni nazwany od stajni, gdy w niej jada ludzie
                if (Math.Abs(c.DFoot) > 1e-6f) result.AddFactor(c.DFoot, c.DFoot < 0f ? TxFootLess : r.RideStableHorse > 0 ? TxFootStable : TxFootMore);
                if (Math.Abs(c.DHerd) > 1e-6f) result.AddFactor(c.DHerd, TxHerd);
            }
            catch (Exception e) { Stumble("ApplySpeed", e); }
        }

        /// <summary>E2: kolumna MarchPace z par (sufit 311d i 311d2, "riding double"). Wejscie: piesi i luzaki kolumny z doczepionymi (jak CountColumn; mounts juz
        /// z wolnymi konmi zbrojowni przy K0 - withArmory), walk - HorsePace.WalkOf. Wyjscie: piesi = pula piechoty partii (gracz: bez jezdzcow z para, z Unhorsed
        /// i rannymi jezdzcami - D9) + piesi doczepionych; luzaki = pary jadace + pary niewykorzystane (pod jencow - D2) + luzaki doczepionych; rideCap - ilu
        /// pieszych moze jechac (pary, nie sam kon). Partia rodu: jazda wedlug gry, jezdzcy bez konia (HorsePace krok 3) i wolne konie zbrojowni AI (K0) jak dotad.
        /// false - bez par (wejscie bez zmian).</summary>
        internal static bool ColumnOf(MobileParty mp, bool withArmory, int walk, ref int foot, ref int mounts, out int rideCap)
        {
            rideCap = -1;
            try
            {
                var r = SpeedRec(mp);
                if (r == null) return false;
                var ir = mp.ItemRoster;
                int footM = mp.Party.NumberOfMenWithoutHorse;
                int mountsM = ir != null ? ir.NumberOfMounts : 0;
                int armM = withArmory ? HorseState.FreeOf(mp) : 0;
                int footA = Math.Max(0, foot - footM), mountsA = Math.Max(0, mounts - mountsM - armM);
                int menM = mp.MemberRoster.TotalManCount;
                int cavG = mp.Party.NumberOfMenWithHorse;
                bool main = mp.IsMainParty;
                int cavO = main && GarrisonKit.OwnKitOn ? Math.Min(cavG, r.CavPairs + r.HeroMounted) : cavG;
                int poolM = Math.Max(0, menM - cavO);
                int rideO = Math.Min(r.Ride, poolM);
                int extra = main ? 0 : Math.Max(0, walk);    // gracz: jezdzcy bez konia sa juz w puli (MarchPairs); rod - krok 3 HorsePace jak dotad
                int armRide = main ? 0 : armM;               // gracz: wolne pary zbrojowni juz w puli (D11); rod - wolne konie zbrojowni AI (K0) jak dotad
                int mainFoot = poolM + extra;
                int mainRide = Math.Min(mainFoot, rideO + armRide);
                foot = mainFoot + footA;
                rideCap = mainRide + Math.Min(footA, mountsA);
                mounts = rideO + armRide + Math.Max(0, r.UnusedPairs) + mountsA;
                return true;
            }
            catch (Exception e) { Stumble("ColumnOf", e); rideCap = -1; return false; }
        }

        // ---------------------------------------------------------------- E3: waga uprzezy par i udzwig z par (projekt rozdz. 2 i 2.1; gracz i partie jego rodu - D3)
        // - "Harness on ridden horses" (postfiks DefaultInventoryCapacityModel.CalculateTotalWeightCarried, DeclaredMethod): minus waga uprzezy z LADUNKU, ktore sa
        //   w parach jadacych pod piechota (rekord: RideHarness). Waga przez model (GetItemEffectiveWeight - NavalDLC owija model), sztuka po sztuce z modyfikatorem,
        //   najwyzej tyle, ile tej sztuki jest teraz w ladunku (rekord gracza do 1 s starszy - w razie watpliwosci za ciezko, nigdy ponizej ladunku). Na morzu nic.
        // - udzwig (PackMarket): dawny dopisek "Harness worn by spare horses" NIE liczy sie przy tym kluczu (inaczej uprzaz dwa razy); "Saddled spare horses
        //   (saddlebags)" = konie z ladunku w parach pod piechota i jencami; luzaki pod jukami (UnderPacks) = konie ladunku bez pary pod czlowiekiem;
        //   "Ridden horses (saddlebags)" gracza = pary jazdy + bohaterowie na wlasnym koniu + piechota na koniach zbrojowni (kazdy kon raz, wedlug roli - tabela 2.1).
        // Pamiec: rekord gracza (Main) i rodu (ClanRec) jak E2; zmiana podpisu -> Invalidate (pamiec wagi i predkosci gry, bez VersionNo).
        private static int _weightState;   // 0 nie sprawdzane, 1 pole jest, 2 brak pola
        private static AccessTools.FieldRef<MobileParty, int> _weightVer;
        private static int _weightInval;
        private static long _wCalls, _wHits;
        private static volatile float _wLastMain;   // ostatni wpis wagi gracza (kg, linia doby)
        private static TextObject _txHarnessKg;
        private static TextObject TxHarnessKg { get { return _txHarnessKg ?? (_txHarnessKg = new TextObject("{=!}Harness on ridden horses")); } }

        /// <summary>E3: klucz RiddenHarnessWeightless (NOWY).</summary>
        internal static bool WeightOn { get { var s = Settings.Current; return s != null && s.RiddenHarnessWeightless; } }

        /// <summary>E3: rekord gracza potrzebny (pasek mapy odswieza go takze bez pozycji HUD).</summary>
        internal static bool Needed { get { return SpeedOn || WeightOn; } }

        /// <summary>E3: waga (kg, przez model) uprzezy z ladunku na koniach pod piechota - najwyzej tyle sztuk, ile jest teraz w ladunku. Dowolny watek (tylko odczyt).</summary>
        internal static float HarnessKg(MobileParty mp, Rec r)
        {
            if (mp == null || r == null || r.AtSea || r.RideHarness.Count == 0) return 0f;
            var ir = mp.ItemRoster;
            var c = Campaign.Current;
            var model = c != null && c.Models != null ? c.Models.InventoryCapacityModel : null;
            if (ir == null || model == null) return 0f;
            float kg = 0f;
            for (int i = 0; i < r.RideHarness.Count; i++)
            {
                var h = r.RideHarness[i];
                if (h.N <= 0 || h.E.Item == null) continue;
                int at = ir.FindIndexOfElement(h.E);
                int have = at >= 0 ? ir.GetElementNumber(at) : 0;
                int n = Math.Min(h.N, have);
                if (n <= 0) continue;
                TextObject d;
                float w = model.GetItemEffectiveWeight(h.E, mp, false, out d);
                if (w > 0f) kg += w * n;
            }
            return kg;
        }

        /// <summary>E3: konie z LADUNKU pod czlowiekiem w parach (piechota + jency - D2) - najwyzej luzaki gry. -1: bez par (klucz NIE, morze, poza zasiegiem).
        /// PackMarket: luzaki pod jukami = mounts - ta liczba; "Saddled spare horses (saddlebags)" = ta liczba. Dowolny watek.</summary>
        internal static int CargoUnderMen(MobileParty mp, int mounts)
        {
            if (!WeightOn) return -1;
            var r = PairsRec(mp);
            if (r == null || r.AtSea) return -1;
            return Math.Min(Math.Max(0, mounts), Math.Max(0, r.RideCargoHorse + r.PrisCargo));
        }

        /// <summary>E3: jezdzcy gracza do "Ridden horses (saddlebags)": pary jazdy + bohaterowie na wlasnym koniu + piechota na koniach ze zbrojowni (kon poza
        /// NumberOfMounts - gra nie daje za niego 20 kg). -1: dawna regula PackMarket.Riders (rod, klucz NIE, bez TroopsFightWithOwnKitOnly - jazda wedlug gry).</summary>
        internal static int CapRiders(MobileParty mp)
        {
            if (!WeightOn || mp == null || !mp.IsMainParty || !GarrisonKit.OwnKitOn) return -1;
            var r = PairsRec(mp);
            if (r == null || r.AtSea) return -1;
            return Math.Max(0, r.CavPairs + r.HeroMounted + Math.Max(0, r.Ride - r.RideCargoHorse));
        }

        /// <summary>E3: postfiks DefaultInventoryCapacityModel.CalculateTotalWeightCarried - wpis "Harness on ridden horses" (minus waga uprzezy par jadacych z ladunku).
        /// Na morzu nic. Dowolny watek: gracz - rekord z pamieci; rod - pamiec po kluczu wersji (liczona tu, tania - bez podgladu DTE).</summary>
        public static void WeightPostfix(MobileParty __0, bool __1, ref ExplainedNumber __result)
        {
            if (__0 == null) return;
            // 537 E4: wpisy "Troops' armoury (unused)" (waga zbrojowni DTE gracza minus to, co na ludziach) - ten sam postfiks, wlasny klucz ArmouryHasWeight,
            // takze na morzu (zbrojownia plynie na statku; waga przez model z flaga morza)
            StashWeight.Add(__0, __1, ref __result);
            try
            {
                if (__1 || !WeightOn) return;
                Interlocked.Increment(ref _wCalls);
                var r = PairsRec(__0);
                if (r == null || r.AtSea) return;
                float kg = HarnessKg(__0, r);
                if (__0.IsMainParty) _wLastMain = kg;
                if (kg <= 0f) return;
                __result.Add(-kg, TxHarnessKg);
                Interlocked.Increment(ref _wHits);
            }
            catch (Exception e) { Stumble("WeightPostfix", e); }
        }

        /// <summary>E3: czesc linii doby (gracz): wpis wagi, udzwig z par, porownanie z dawnym dopiskiem uprzezy.</summary>
        private static void WeightLine(StringBuilder sb, MobileParty mp, Rec r)
        {
            sb.Append(" | E3 waga i udzwig z par (klucz RiddenHarnessWeightless ").Append(WeightOn ? "TAK" : "NIE").Append(')');
            if (!WeightOn) { sb.Append(" - waga i udzwig jak przed E3 (Harness On Spare Horses ").Append(Settings.Current != null && Settings.Current.HarnessOnSpareHorses ? "TAK" : "NIE").Append(')'); return; }
            var pr = PairsRec(mp);
            if (pr == null) { sb.Append(": brak rekordu").Append(r != null && r.AtSea ? " (na morzu - waga uprzezy liczy sie cala)" : ""); return; }
            int n = 0; foreach (var h in pr.RideHarness) n += h.N;
            float now = HarnessKg(mp, pr);
            sb.Append(": uprzeze z ladunku na koniach pod piechota ").Append(n).Append(" szt. (rodzajow ").Append(pr.RideHarness.Count).Append(") = ")
              .Append(now.ToString("0.0", Inv)).Append(" kg teraz (w rekordzie ").Append(pr.RideHarnessKg.ToString("0.0", Inv)).Append(", ostatni wpis wagi ")
              .Append(_wLastMain.ToString("0.0", Inv)).Append(')');
            int mounts = mp.ItemRoster != null ? mp.ItemRoster.NumberOfMounts : 0;
            int under = CargoUnderMen(mp, mounts);
            sb.Append(" | konie ladunku pod ludzmi z para ").Append(under).Append(" (piechota ").Append(Math.Min(pr.RideCargoHorse, mounts)).Append(", jency ").Append(pr.PrisCargo)
              .Append(") z luzakow gry ").Append(mounts).Append(" -> pod jukami ").Append(Math.Max(0, mounts - Math.Max(0, under)));
            int cr = CapRiders(mp);
            sb.Append(" | Ridden horses (saddlebags): ").Append(cr >= 0 ? cr + " (jazda " + pr.CavPairs + ", bohaterowie " + pr.HeroMounted + ", piechota na koniach zbrojowni " + Math.Max(0, pr.Ride - pr.RideCargoHorse) + ")"
                                                                       : "dawna regula (wzorzec) " + PackMarket.RidersOld(mp) + (GarrisonKit.OwnKitOn ? "" : " - TroopsFightWithOwnKitOnly NIE"))
              .Append(" (dawniej wedlug wzorca ").Append(PackMarket.RidersOld(mp)).Append(')');
            try
            {
                var c = Campaign.Current;
                var m = c != null && c.Models != null ? c.Models.InventoryCapacityModel : null;
                if (m != null)
                    sb.Append(" | waga gracza ").Append(m.CalculateTotalWeightCarried(mp, mp.IsCurrentlyAtSea).ResultNumber.ToString("0", Inv))
                      .Append(" (pamiec gry ").Append(mp.TotalWeightCarried.ToString("0", Inv)).Append(") / udzwig ")
                      .Append(m.CalculateInventoryCapacity(mp, mp.IsCurrentlyAtSea).ResultNumber.ToString("0", Inv))
                      .Append(" | dawny dopisek udzwigu Harness worn by spare horses (usuniety): bylby +").Append(PackMarket.OldHarnessOwnKg(mp).ToString("0", Inv)).Append(" kg");
            }
            catch (Exception e) { Stumble("WeightLine.model", e); }
            sb.Append(" | wpis wagi: wywolan ").Append(Interlocked.Read(ref _wCalls)).Append(", z wpisem ").Append(Interlocked.Read(ref _wHits))
              .Append(", odswiezen pamieci wagi gry ").Append(_weightInval).Append(" (pole ").Append(_weightState == 1 ? "TAK" : _weightState == 2 ? "BRAK" : "nie uzyte").Append(')');
        }

        /// <summary>E3: dopisek partii rodu do linii doby - wpis wagi i konie ladunku pod ludzmi.</summary>
        private static string WeightClan(MobileParty x)
        {
            if (!WeightOn) return "";
            try
            {
                var r = PairsRec(x);
                if (r == null) return ", E3: brak (morze albo poza zasiegiem)";
                int mounts = x.ItemRoster != null ? x.ItemRoster.NumberOfMounts : 0;
                return ", E3: uprzeze par " + HarnessKg(x, r).ToString("0.0", Inv) + " kg mniej, luzaki pod ludzmi " + CargoUnderMen(x, mounts) + "/" + mounts;
            }
            catch (Exception e) { Stumble("WeightClan", e); return ", E3: blad"; }
        }

        /// <summary>E2: dopisek partii rodu do linii doby - wpisy predkosci z rekordu do predkosci (ten sam co model).</summary>
        private static string SpeedClan(MobileParty x)
        {
            if (!SpeedOn) return "";
            try
            {
                var sr = SpeedRec(x);
                if (sr == null) return ", E2: brak (morze albo poza zasiegiem)";
                var c = Compute(x, sr);
                return ", E2: num10 " + c.Num10O + "/gra " + c.Num10G + ", stado " + c.HerdO + "/gra " + c.HerdG + ", Footmen " + F3(c.DFoot) + ", Herding " + F3(c.DHerd);
            }
            catch (Exception e) { Stumble("SpeedClan", e); return ", E2: blad"; }
        }

        /// <summary>E2: czesc linii doby (gracz): liczenie na swiezo (Compute) i ostatnie z modelu predkosci, kolumna MarchPace.Explain, stan odswiezen.</summary>
        private static void SpeedLine(StringBuilder sb, MobileParty mp, Rec r)
        {
            sb.Append(" | E2 predkosc z par (klucz MarchPairsSpeed ").Append(SpeedOn ? "TAK" : "NIE").Append(')');
            if (!SpeedOn) { sb.Append(" - gra i HorsePace jak dotad"); return; }
            var sr = SpeedRec(mp);
            if (sr == null) { sb.Append(": brak rekordu do predkosci").Append(r != null && r.AtSea ? " (na morzu)" : ""); return; }
            var c = Compute(mp, sr);
            sb.Append(": ludzie ").Append(c.N).Append(c.Att > 0 ? " (z " + c.Att + " doczepionymi)" : "")
              .Append(" | gra: jazda ").Append(c.CavG).Append(", piesi ").Append(c.FootG).Append(", luzaki ").Append(c.MountsG).Append(", num10 ").Append(c.Num10G).Append(", stado ").Append(c.HerdG)
              .Append(" | pary: jazda ").Append(c.CavO).Append(c.CavRule ? "" : " (jak gra - TroopsFightWithOwnKitOnly NIE)").Append(", piechota na parach ").Append(c.RideO)
              .Append(c.Att > 0 ? " + doczepione " + c.AttRide : "").Append(" = num10 ").Append(c.Num10O).Append(", wolne konie w stadzie ").Append(c.FreeO).Append(", stado ").Append(c.HerdO)
              .Append(" | wpisy: Riders without harness ").Append(F3(c.DCav)).Append(", Footmen on horses ").Append(F3(c.DFoot)).Append(", Herding: horses without riders ").Append(F3(c.DHerd))
              .Append(" (mokro x").Append(c.Wet.ToString("0.0", Inv)).Append(", NomadicTraditions ").Append(c.Nomad.ToString("0.00", Inv)).Append(", Shepherd ").Append(c.Shep.ToString("0.00", Inv)).Append(')');
            var lc = _lastCalc;
            if (lc != null) sb.Append(" | ostatnio w modelu predkosci: ").Append(F3(lc.DCav)).Append(" / ").Append(F3(lc.DFoot)).Append(" / ").Append(F3(lc.DHerd))
                              .Append(" (num10 ").Append(lc.Num10O).Append(", stado ").Append(lc.HerdO).Append(')');
            int qn = 0; double qs = 0; foreach (var q in sr.RideQ) { qn += q.N; qs += q.N * (double)q.Q; }
            sb.Append(" | konie pod piechota z par ").Append(qn).Append(qn > 0 ? " (szybkosc srednio " + (qs / qn).ToString("0.0", Inv) + ")" : "");
            try
            {
                var col = MarchPace.Explain(mp);
                if (col != null) sb.Append(" | kolumna: z par ").Append(col.Pairs ? "TAK" : "NIE").Append(", piesi ").Append(col.Foot).Append(", luzaki/pary ").Append(col.Horses)
                                   .Append(", moze jechac ").Append(col.RideCap).Append(", na luzakach ").Append(col.RidingFoot).Append(", idacych ").Append(col.Walkers)
                                   .Append(", na drugiego ").Append(col.Double).Append(", rodzaj ").Append(col.Kind).Append(" (N ").Append(col.N).Append("), sufit ").Append(col.Cap.ToString("0.00", Inv));
            }
            catch (Exception e) { Stumble("SpeedLine.Explain", e); }
            sb.Append(" | odswiezen predkosci gry ").Append(_invalidations).Append(" (pole ").Append(_pureState == 1 ? "TAK" : _pureState == 2 ? "BRAK" : "nie uzyte").Append(')')
              .Append(", wywolan wpisow ").Append(Interlocked.Read(ref _applyCalls)).Append(", rekordow rodu liczonych ").Append(Interlocked.Read(ref _clanBuilds));
        }

        private static string F3(float v) { return (v >= 0 ? "+" : "") + v.ToString("0.000", Inv); }

        /// <summary>Raz na dobe (ArmouryBehavior.OnDailyTick po HorsePace.Daily): linia "Pary marszowe (gracz)" (tylko log).</summary>
        internal static void Daily()
        {
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                var mp = MobileParty.MainParty;
                if (Campaign.Current == null || mp == null || !mp.IsActive) return;
                int day = (int)CampaignTime.Now.ToDays - 1;
                // E1: ten sam rekord co HUD (pamiec Main) - liczony od nowa teraz; potkniecie - rekord z pamieci (zaznaczone w linii)
                int cnt0 = _mainCount;
                Rec r = Main(true);
                bool fresh = _mainCount != cnt0;
                var sb = new StringBuilder(1600);
                sb.Append("Pary marszowe (gracz): dzien ").Append(day);
                if (r != null && r.Ok && !fresh) sb.Append(" | REKORD Z PAMIECI (liczenie teraz nieudane - dzien rekordu ").Append(r.Day - 1).Append(')');
                if (r != null && r.Ok)
                {
                    sb.Append(" | ludzie ").Append(r.Men).Append(" (ranni ").Append(r.Wounded).Append(", bohaterowie ").Append(r.Heroes).Append(", jency ").Append(r.Prisoners)
                      .Append(", doczepionych partii ").Append(r.Attached).Append(" - liczona sama partia gracza), na morzu ").Append(r.AtSea ? "TAK" : "NIE");
                    sb.Append(" | ladunek: konie wierzchowe ").Append(r.CMounts).Append(" (zwykle ").Append(r.CPlain).Append(", inne wierzchowe ").Append(r.COtherMount)
                      .Append("; gra NumberOfMounts ").Append(r.GameMounts).Append("), juczne ").Append(r.CPack).Append(", bydlo ").Append(r.CLive)
                      .Append(", uprzeze pasujace ").Append(r.CHarFam + r.CHarSaddle).Append(" (rodzinne ").Append(r.CHarFam).Append(", same siodla ").Append(r.CHarSaddle)
                      .Append("), innej rodziny ").Append(r.CHarOther).Append(", siodla transportowe ").Append(r.CPackSaddle);
                    sb.Append(" | zbrojownia DTE: konie ").Append(r.AHorses).Append(", uprzeze pasujace ").Append(r.AHarness).Append(", innej rodziny ").Append(r.AHarOther)
                      .Append("; wolne po jezdzie: konie ").Append(r.AFreeHorses).Append(", uprzeze ").Append(r.AFreeHarness);
                    sb.Append(" | jazda (").Append(r.Preview ? "podglad DTE" : "wlasny dobor - " + (r.PreviewWhy ?? "-")).Append("): zdrowych jezdzcow wzorca ").Append(r.CavMen)
                      .Append(", par jazdy ").Append(r.CavPairs);
                    if (r.Preview) sb.Append(", na koniu bez uprzezy ").Append(r.CavNoHarness).Append(", bez konia ").Append(r.CavNoHorse).Append(", kon zdjety bez uprzezy ").Append(r.CavNoSaddle);
                    sb.Append(", ranni jezdzcy ").Append(r.CavWounded).Append(" (do puli piechoty, D9)")
                      .Append(" | bohaterowie: na wlasnym koniu ").Append(r.HeroMounted).Append(" (bez pasujacej uprzezy ").Append(r.HeroNoHarness).Append("), bez konia ").Append(r.HeroFoot);
                    sb.Append(" | pula piechoty: ludzi ").Append(r.PoolMen).Append(", konie ").Append(r.PoolHorses).Append(", uprzeze ").Append(r.PoolHarness)
                      .Append(", par ").Append(r.PoolPairs).Append(" (kon z ladunku ").Append(r.PoolPairsCargo).Append(", ze zbrojowni ").Append(r.PoolPairsArm).Append(")")
                      .Append(", wymog par ").Append(r.PairReqMin < 0 ? "-" : r.PairReqMin + "-" + r.PairReqMax)
                      .Append(" -> jedzie ").Append(r.Ride).Append(", pieszo ").Append(r.Walk).Append(" (bez konia ").Append(r.WalkNoHorse).Append(", bez uprzezy ").Append(r.WalkNoHarness)
                      .Append(", za niska Jazda ").Append(r.WalkSkill).Append("), par niewykorzystanych ").Append(r.UnusedPairs)
                      .Append(r.UnusedReqMin >= 0 ? " (najnizszy wymog " + r.UnusedReqMin + ")" : "");
                    sb.Append(" | braki Jazdy wedlug typu: ").Append(r.SkillByType.Count > 0 ? string.Join(", ", r.SkillByType) : "-");
                    sb.Append(" | gra: piesi ").Append(r.GameFoot).Append(", num10 = min(piesi, NumberOfMounts) ").Append(r.GameNum10)
                      .Append(", Footmen on horses ").Append(F3(r.GameFootF))
                      .Append(", stado ").Append(r.GameHerd).Append(", Herding ").Append(F3(r.GameHerdF));
                    sb.Append(" | pary: jedzie piechoty ").Append(r.Ride).Append(", Footmen ").Append(F3(r.OurFootF)).Append(" (roznica ").Append(F3(r.OurFootF - r.GameFootF)).Append(")")
                      .Append(", wolne konie w stadzie ").Append(r.FreeHerd).Append(", stado ").Append(r.OurHerd).Append(", Herding ").Append(F3(r.OurHerdF))
                      .Append(" (roznica ").Append(F3(r.OurHerdF - r.GameHerdF)).Append(")");
                    sb.Append(" | HUD (D10 a, E1 - te same liczby na pasku mapy): Horses ").Append(MarchHud.HorsesText(r)).Append(", Harness ").Append(MarchHud.HarnessText(r))
                      .Append(" (uprzezy wszystkich ").Append(r.AllHarness).Append("), Pack ").Append(MarchHud.PackText(r))
                      .Append(" (ludzie - bydlo - wolne konie)").Append(r.AtSea ? " - na morzu nikt nie jedzie" : "")
                      .Append(" | pamiec rekordu: liczen od startu sesji ").Append(_mainCount).Append(", ostatnie ").Append(r.Ms.ToString("0.0", Inv)).Append(" ms")
                      .Append(" | ").Append(MarchHud.State());
                    try { sb.Append(" | ").Append(RiddenBy.State(r)); } catch (Exception e) { Stumble("RiddenBy.State", e); }   // E5
                    sb.Append(" | stajnia piechoty (E6): konie ").Append(r.SMounts).Append(", uprzeze ").Append(r.SHarness).Append(", inne ").Append(r.SOther)
                      .Append(", pary puli z koniem ze stajni ").Append(r.PoolPairsStable).Append(", jedzie na koniach stajni ").Append(r.RideStableHorse)
                      .Append(", par stajni niewykorzystanych ").Append(r.UnusedStableHorse);
                    try { SpeedLine(sb, mp, r); } catch (Exception e) { Stumble("SpeedLine", e); }   // E2
                    try { WeightLine(sb, mp, r); } catch (Exception e) { Stumble("WeightLine", e); }   // E3
                    // HorsePace dla porownania (krok 1 i 3 - E2 zastapi wpis "riders without horses")
                    try
                    {
                        var hp = HorsePace.Get(mp);
                        if (hp != null) sb.Append(" | HorsePace: bez konia ").Append(hp.Unhorsed).Append(", pieszo ").Append(hp.Walk).Append(", wolne konie zbrojowni (Free) ").Append(hp.Free)
                                          .Append(", piechurow ponad gre ").Append(hp.Extra);
                    }
                    catch (Exception e) { Stumble("HorsePace", e); }
                }
                else sb.Append(" | brak rekordu");
                // partie rodu gracza (D3): sam ladunek, jazda wedlug gry
                var clan = new List<string>();
                try
                {
                    var pc = Clan.PlayerClan;
                    if (pc != null)
                        foreach (var x in MobileParty.All)
                        {
                            try
                            {
                                if (x == null || x == mp || !x.IsActive || x.MemberRoster == null || x.IsGarrison || x.IsCaravan || x.IsVillager || x.IsMilitia) continue;
                                var lh = x.LeaderHero;
                                if (lh == null || lh.Clan != pc) continue;
                                var q = Of(x);
                                if (q == null || !q.Ok) continue;
                                clan.Add("'" + (x.Name != null ? x.Name.ToString() : x.StringId) + "' ludzie " + q.Men + ", piesi gry " + q.GameFoot + ", konie ladunku " + q.CMounts
                                         + " (gra " + q.GameMounts + "), uprzeze " + (q.CHarFam + q.CHarSaddle) + ", par " + q.PoolPairs + ", jedzie " + q.Ride + "/num10 " + q.GameNum10
                                         + " (za niska Jazda " + q.WalkSkill + "), stado " + q.OurHerd + "/gra " + q.GameHerd
                                         + SpeedClan(x) + WeightClan(x));
                            }
                            catch (Exception e) { Stumble("Of(rod)", e); }
                        }
                }
                catch (Exception e) { Stumble("rod", e); }
                sb.Append(" | partie rodu (sam ladunek): ").Append(clan.Count > 0 ? string.Join("; ", clan) : "-");
                // E2: porzadek pamieci rodu - partie nieczynne albo spoza rodu
                try { foreach (var kv in _clan) if (kv.Key == null || !kv.Key.IsActive || !InClan(kv.Key)) { CRec x; _clan.TryRemove(kv.Key, out x); } }
                catch (Exception e) { Stumble("rod.porzadek", e); }
                sb.Append(" | potkniecia dzis ").Append(_stumblesDay).Append(", od startu sesji ").Append(_stumbles);
                if (_errSites.Count > 0) sb.Append(" (").Append(string.Join(", ", _errSites)).Append(')');
                sb.Append(" | linia ").Append(((Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency).ToString("0.0", Inv)).Append(" ms.");
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally { _stumblesDay = 0; }
        }
    }
}
