using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

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
    /// </summary>
    internal static class MarchPairs
    {
        private const byte SCargo = 0, SArm = 1;
        // rodzaj czlowieka w puli piechoty
        private const byte KFoot = 0, KWoundedRider = 1, KCavWalk = 2, KHero = 3;

        private sealed class Lot { public ItemObject It; public int Req; public int N; public byte Src; }
        private sealed class PairLot { public int Req; public int N; public byte HSrc, USrc; }
        private sealed class ManLot { public CharacterObject Ch; public int Riding; public int N; public byte Kind; public int Got, Skill; }

        internal sealed class Rec
        {
            public bool Ok, AtSea, Preview; public string PreviewWhy;
            public int Men, Wounded, Heroes, Prisoners, Attached;
            // ladunek
            public int CMounts, CPlain, COtherMount, CPack, CLive, GameMounts, CHarFam, CHarSaddle, CHarOther, CPackSaddle;
            // zbrojownia DTE gracza
            public int AHorses, AHarness, AHarOther, AFreeHorses, AFreeHarness;
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
            /// <summary>E1: koni pod ludzmi (pary jazdy + pary puli); na morzu 0 - nikt nie jedzie (rozdz. 1.3). Ta sama liczba w linii doby i w HUD.</summary>
            public int Ridden { get { return AtSea ? 0 : CavPairs + Ride; } }
            /// <summary>E1: wszystkie konie wierzchowe - ladunek + zbrojownia DTE (mianownik HUD, D10 a).</summary>
            public int AllHorses { get { return CMounts + AHorses; } }
            /// <summary>E1: wszystkie pasujace uprzeze - ladunek + zbrojownia DTE.</summary>
            public int AllHarness { get { return CHarFam + CHarSaddle + AHarness; } }
        }

        private static int _stumbles, _stumblesDay;
        private static readonly HashSet<string> _errSites = new HashSet<string>();
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        internal static void Reset() { _stumbles = 0; _stumblesDay = 0; _errSites.Clear(); _main = null; _mainKey = null; _mainAt = Environment.TickCount - 20000; _mainCount = 0; }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumblesDay++;
            if (_errSites.Add(where)) { try { Log.Error("MarchPairs." + where, e); } catch { } }
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

        private static void AddLot(List<Lot> l, ItemObject it, int n, byte src)
        {
            if (it == null || n <= 0) return;
            int rq = Req(it);
            foreach (var x in l) if (x.It == it && x.Src == src) { x.N += n; return; }
            l.Add(new Lot { It = it, Req = rq, N = n, Src = src });
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
            horses.Sort((a, b) => a.Req != b.Req ? a.Req.CompareTo(b.Req) : a.Src.CompareTo(b.Src));
            var fam = new List<Lot>(); var sad = new List<Lot>();
            foreach (var h in harness) { var c = new Lot { It = h.It, Req = h.Req, N = h.N, Src = h.Src }; if (h.It.ArmorComponent != null) fam.Add(c); else sad.Add(c); }
            Comparison<Lot> asc = (a, b) => a.Req != b.Req ? a.Req.CompareTo(b.Req) : a.Src.CompareTo(b.Src);
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
                        AddPair(res, Math.Max(ho.Req, u.Req), t, ho.Src, u.Src);
                    }
                }
            }
            res.Sort((a, b) => a.Req != b.Req ? a.Req.CompareTo(b.Req) : a.HSrc.CompareTo(b.HSrc));
            return res;
        }

        private static void AddPair(List<PairLot> l, int req, int n, byte hs, byte us)
        {
            foreach (var x in l) if (x.Req == req && x.HSrc == hs && x.USrc == us) { x.N += n; return; }
            l.Add(new PairLot { Req = req, N = n, HSrc = hs, USrc = us });
        }

        private static void AddMan(List<ManLot> l, CharacterObject ch, int n, byte kind)
        {
            if (ch == null || n <= 0) return;
            foreach (var x in l) if (x.Ch == ch && x.Kind == kind) { x.N += n; return; }
            l.Add(new ManLot { Ch = ch, Riding = Riding(ch), N = n, Kind = kind });
        }

        /// <summary>Krok 3 rozdz. 1.2: ludzie rosnaco wedlug Jazdy, pary rosnaco wedlug wymogu; kazdy bierze najlatwiejsza pare, ktora umie poprowadzic.</summary>
        private static void Assign(List<ManLot> men, List<PairLot> pairs, Rec r)
        {
            men.Sort((a, b) => a.Riding.CompareTo(b.Riding));
            var pl = new int[pairs.Count];
            for (int i = 0; i < pairs.Count; i++) pl[i] = pairs[i].N;
            int p = 0;
            foreach (var m in men)
            {
                int want = m.N;
                while (want > 0 && p < pairs.Count)
                {
                    if (pl[p] <= 0) { p++; continue; }
                    if (pairs[p].Req > m.Riding) break;   // ta i dalsze pary za trudne dla tego czlowieka
                    int t = Math.Min(want, pl[p]);
                    want -= t; pl[p] -= t; m.Got += t;
                    r.Ride += t;
                    if (pairs[p].HSrc == SCargo) r.RideCargoHorse += t;
                }
            }
            int unused = 0, umin = -1;
            for (int i = 0; i < pairs.Count; i++) if (pl[i] > 0) { unused += pl[i]; if (umin < 0 || pairs[i].Req < umin) umin = pairs[i].Req; }
            r.UnusedPairs = unused; r.UnusedReqMin = umin;
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
                byDesc.Sort((a, b) => b.Riding.CompareTo(a.Riding));
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
                        if (Stables.IsPlainMount(it)) { r.CPlain += el.Amount; AddLot(horses, it, el.Amount, SCargo); }
                        else r.COtherMount += el.Amount;
                        continue;
                    }
                    switch (HarnessKind(it))
                    {
                        case 1: r.CHarFam += el.Amount; AddLot(harness, it, el.Amount, SCargo); break;
                        case 2: r.CHarSaddle += el.Amount; AddLot(harness, it, el.Amount, SCargo); break;
                        case 3: r.CHarOther += el.Amount; break;
                        case 4: r.CPackSaddle += el.Amount; break;
                    }
                }
            }

            var men = new List<ManLot>();
            // bohaterowie: wlasny kon poza pula; bez konia - do puli piechoty z wlasna Jazda
            // szeregowi: kon we wzorcu (IsMounted - ta sama miara co NumberOfMenWithHorse gry) - jazda; reszta - piechota (z rannymi, D9)
            var cavHealthy = new Dictionary<CharacterObject, int>();
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
                if (main) { r.CavWounded += wnd; AddMan(men, ch, wnd, KWoundedRider); if (ok > 0) cavHealthy[ch] = ok; }
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
                if (quiet && light) { try { pv = LastIfSame(mp); } catch (Exception e) { Stumble("KitPreview.Last", e); pv = null; } }
                if (quiet && pv == null) { try { pv = KitPreview.Get(false); } catch (Exception e) { Stumble("KitPreview", e); pv = null; } }
                else pv = new KitPreview.Result { Why = "CrashScribe sprzed 133a - podglad pomijany" };
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
                        int healthy; cavHealthy.TryGetValue(t.Troop, out healthy);
                        walkers = Math.Min(walkers, healthy);   // tylko jezdzcy wzorca (piechur z podgladu nie jest jezdzcem bez pary)
                        AddMan(men, t.Troop, walkers, KCavWalk);
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
                    foreach (var m in cm) AddMan(men, m.Ch, m.N - m.Got, KCavWalk);
                    // zuzyte pary jazdy zdejmujemy z puli zbrojowni: najlatwiejsze pary poszly do jazdy, wiec do piechoty idzie reszta wedlug liczby
                    int usedH = tmp.Ride, usedU = tmp.Ride;
                    foreach (var x in ah) { int t = Math.Min(usedH, x.N); usedH -= t; int f = x.N - t; if (f > 0) { r.AFreeHorses += f; AddLot(horses, x.It, f, SArm); } }
                    foreach (var x in au) { int t = Math.Min(usedU, x.N); usedU -= t; int f = x.N - t; if (f > 0) { r.AFreeHarness += f; AddLot(harness, x.It, f, SArm); } }
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
                if (p.HSrc == SCargo) r.PoolPairsCargo += p.N; else r.PoolPairsArm += p.N;
                if (r.PairReqMin < 0 || p.Req < r.PairReqMin) r.PairReqMin = p.Req;
                if (p.Req > r.PairReqMax) r.PairReqMax = p.Req;
            }
            if (r.AtSea) { r.Walk = r.PoolMen; }   // na morzu nikt nie jedzie (rozdz. 1.3)
            else Assign(men, pairs, r);

            // gra wobec nas (bez mokra i perkow - wartosci na sucho)
            r.GameFoot = mp.Party.NumberOfMenWithoutHorse;
            r.GameNum10 = Math.Min(r.GameFoot, r.GameMounts);
            r.GameHerd = r.CPack + r.CLive + Math.Max(0, r.GameMounts - r.GameNum10);
            r.FreeHerd = Math.Max(0, r.CMounts - r.RideCargoHorse);
            r.OurHerd = r.CPack + r.CLive + r.FreeHerd;
            r.PackLimit = Math.Max(0, r.Men - r.CLive - r.FreeHerd);
            if (r.Men > 0)
            {
                r.GameFootF = 0.15f * r.GameNum10 / r.Men;
                r.OurFootF = 0.15f * r.Ride / r.Men;
            }
            r.GameHerdF = Herding(r.Men, r.GameHerd);
            r.OurHerdF = Herding(r.Men, r.OurHerd);
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
                   + "|" + (ir != null ? ir.VersionNo : -1) + "|" + (int)CampaignTime.Now.ToDays + "|" + (mp.IsCurrentlyAtSea ? 1 : 0) + (SaddleLaw.On ? 1 : 0);
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
                _main = r; _mainCount++;
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
                                         + " (za niska Jazda " + q.WalkSkill + "), stado " + q.OurHerd + "/gra " + q.GameHerd);
                            }
                            catch (Exception e) { Stumble("Of(rod)", e); }
                        }
                }
                catch (Exception e) { Stumble("rod", e); }
                sb.Append(" | partie rodu (sam ladunek): ").Append(clan.Count > 0 ? string.Join("; ", clan) : "-");
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
