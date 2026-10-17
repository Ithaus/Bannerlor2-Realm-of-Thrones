using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// KS - PRODUKCJA ZASTOSOWANA (projekt rozdz. 11.2 "KS-A"): po wlaczeniu kazda doba liczy z ludzi doby, udzialow zajec blokow (stan w zapisie) i tabel swiata
    /// produkcje okregu w prawdziwych kg (x zima WinterBite x E4/5 x E4/7 - raz), odejmuje to, co okreg zjada sam (udzial z wlasnego pola stanow ZC x lepszy stol z Planu),
    /// i publikuje stawki wsi na jutro w sztukach gry (kg / (sigma x kg sztuki)). Haki:
    ///  - MaterialLaw.ProdPostfix (ProdStep): towar KS wsi zastosowanej = stawka KS (zastepuje stawke typu, ludzi wsi P1, stawki GL5, mnozniki kopalni i lasu, owoce i jaja
    ///    BK, wielblady, regulator 221, hodowle K4, W2 i przejscia ZC1); dalej sufit dymarki, RockGold i ksiega rudy jak dotad; wies nie Normal - 0 (jak gra);
    ///  - MineralOnce.Postfix (ListStep): towary KS ze stawka > 0 na liscie BK (dopisane z waga 0), jaja BK i krok W2 pominiete (KS liczy jaja i ryby sam);
    ///  - magazyn wsi (CapOverride): W + 5 dob towarow KS spoza listy typu (zamiast dodatkow owocow i jaj);
    ///  - zywnosc wsi gry (TickFoodProduction - losowe ziarno, ryby i inne z niczego) - wylaczona dla wsi zastosowanych;
    ///  - spichlerz (GranaryFloor): racje = max(racje KS, wzor dzis);
    ///  - pola mieszczan: plon na polke miasta (ramka ksiegi towarow "pola mieszczan (KS)"), kasa miasta placi ludowi (TownFolk.KsFieldPay) - bez zaplaty towar nie wchodzi.
    /// Klucz Start Calibration wylaczony - nic z tego (produkcja jak dzis), stan w zapisie zostaje.
    /// </summary>
    internal static partial class StartCalibration
    {
        // ------------------------------------------------------------ towary dostawy (sztuki gry); indeks KS towaru -> towar dostawy
        internal static readonly string[] ItemIds =
        {
            "grain", "fish", "cheese", "butter", "Egg", "apple", "carrot", "orange", "grape", "olives", "date_fruit", "salt", "honey", "spice", "wool", "flax", "cotton",
            "hardwood", "clay", "limestone", "marble", "iron", "silver", "gold_ore", "fur", "whale_oil", "walrus_tusk", "cow", "hog", "sheep", "WhaleMeat", "meat"
        };
        internal const int ItemCount = 32;
        private const int IGrain = 0, IFish = 1, ICheese = 2, IButter = 3, IEgg = 4, IApple = 5, ICarrot = 6, IOrange = 7, IGrape = 8, IOlives = 9, IDate = 10, ISalt = 11,
                          IHoney = 12, ISpice = 13, IWool = 14, IFlax = 15, ICotton = 16, IWood = 17, IClay = 18, ILime = 19, IMarble = 20, IIron = 21, ISilver = 22, IGold = 23,
                          IFur = 24, IWhaleOil = 25, ITusk = 26, ICow = 27, IHog = 28, ISheep = 29, IWhaleMeat = 30, IMeat = 31;
        private static readonly int[] GoodToItem = BuildG2I();
        private static int[] BuildG2I()
        {
            var a = new int[KsTab.NG];
            for (int g = 0; g < KsTab.NG; g++) a[g] = Array.IndexOf(ItemIds, KsTab.Goods[g]);
            a[KsTab.GMeat] = -1; a[KsTab.GHides] = -1; a[KsTab.GHorse] = -1;    // mieso i skory z uboju robia miasta (rzemieslnicy BK); konie - lancuch gry
            return a;
        }
        private static readonly double[] ItemKcal = BuildKcal();
        private static double[] BuildKcal()
        {
            var k = new double[ItemCount];
            k[IGrain] = 3300; k[IFish] = 1100; k[ICheese] = 3600; k[IButter] = 7200; k[IEgg] = 1450; k[IApple] = 500; k[ICarrot] = 400; k[IOrange] = 450; k[IGrape] = 700;
            k[IOlives] = 1150; k[IDate] = 2800; k[IHoney] = 3000; k[IWhaleMeat] = 2200; k[IMeat] = 2200;
            k[ICow] = 2200 * KsTab.MeatOfLive; k[IHog] = 2200 * KsTab.MeatOfLive; k[ISheep] = 2200 * KsTab.MeatOfLive;
            return k;
        }

        private sealed class LiveBlock
        {
            public byte[] C; public float[] X;                 // zajecia bloku i udzial rak (suma 1)
            public byte[] RC; public float[] RF;               // regale: zajecie i udzial rak bloku przy zlozu
            public float Rent;                                 // renta ziemi z Planu (d/ha/d)
            public int Held;                                   // regula zmiany: doby z rzedu z lepszym zajeciem >= 1.25 x biezacego
            public sbyte Cur = -1, Target = -1; public int TStart = -1, TEnd = -1; public float X0, Cost, Paid, H0;
            // pamiec sesji
            public KsBlock K; public string Uid; public double Hands, HomeFrac; public List<int> Cand;
        }

        private sealed class LiveDist
        {
            public string Id; public bool TF; public int Market; public float[] NeedMult = Ones(KsTab.NN); public LiveBlock[] B;
            // pamiec sesji
            public Village V; public Town T; public Settlement St; public KsDistrict K; public LandDist L;
            public double[] OwnNeed = new double[KsTab.NN]; public double PeopleHome; public double[] Frac = new double[ItemCount];
            public double Rations = -1; public double Wf;
            public float[] Saved;                              // stawki z zapisu (pierwsza doba po wczytaniu, przed LiveDaily)
        }

        private sealed class DistResult { public double[] Prod = new double[ItemCount], Own = new double[ItemCount], MarketKg = new double[ItemCount], Market = new double[ItemCount]; public double OwnKcal, KcalNeed; }

        private static float[] Ones(int n) { var a = new float[n]; for (int i = 0; i < n; i++) a[i] = 1f; return a; }

        private static Dictionary<string, LiveDist> _live = new Dictionary<string, LiveDist>(StringComparer.Ordinal);
        private static volatile Dictionary<Village, float[]> _rates = new Dictionary<Village, float[]>();
        private static Dictionary<ItemObject, int> _itemIx;
        private static ItemObject[] _items;
        private static double[] _itemKg;
        private static int _liveStumbles;
        private static readonly TextObject _txt = new TextObject("{=!}Armoury: hands and land of the district (start calibration)");

        private static void LiveReset()
        {
            _live = new Dictionary<string, LiveDist>(StringComparer.Ordinal);
            _rates = new Dictionary<Village, float[]>();
            _itemIx = null; _items = null; _itemKg = null; _liveStumbles = 0;
            RuleReset();
        }

        private static void EnsureItems()
        {
            if (_itemIx != null) return;
            var ix = new Dictionary<ItemObject, int>();
            var items = new ItemObject[ItemCount]; var kg = new double[ItemCount];
            for (int i = 0; i < ItemCount; i++)
            {
                var it = ItemOf(ItemIds[i]);
                items[i] = it;
                kg[i] = it != null && it.Weight > 0.01f ? it.Weight : 10.0;
                if (it != null && !ix.ContainsKey(it)) ix[it] = i;
            }
            _items = items; _itemKg = kg; _itemIx = ix;
        }

        // ============================================================ haki (moga isc z watkow AI - sam odczyt opublikowanej pamieci)
        /// <summary>Wies ze stawkami KS (stan zastosowany, klucz wlaczony).</summary>
        internal static bool AppliedFor(Village v)
        {
            if (v == null || _state != StateApplied) return false;
            var r = _rates;
            return r.Count > 0 && r.ContainsKey(v) && KeyOn;
        }

        /// <summary>MaterialLaw.ProdPostfix (pierwszy krok): towar KS wsi zastosowanej - wynik = stawka KS (sztuki/d), potem sufity kruszcow i ksiega rudy jak dotad.
        /// true - wynik ustawiony (reszta lancucha pominieta).</summary>
        internal static bool ProdStep(Village v, ItemObject item, ref ExplainedNumber r)
        {
            if (v == null || item == null || _state != StateApplied) return false;
            try
            {
                var rates = _rates; float[] a; int i;
                var ix = _itemIx;
                if (ix == null || rates.Count == 0 || !rates.TryGetValue(v, out a) || !ix.TryGetValue(item, out i) || !KeyOn) return false;
                float x = v.VillageState == Village.VillageStates.Normal && v.TradeBound != null ? a[i] : 0f;   // jak model gry: wies najezdzana, spladrowana albo bez targu - 0
                if (!(x > 0f) || float.IsNaN(x) || float.IsInfinity(x)) x = 0f;
                r = new ExplainedNumber(x, false, null);
                Bloomery.Cap(v, item, ref r);
                RockGold.Cap(v, item, ref r);
                OreLedger.NoteModel(v, item, r.ResultNumber);
                return true;
            }
            catch (Exception e) { Interlocked.Increment(ref _liveStumbles); if (_liveStumbles <= 3) Log.Error("StartCalibration.ProdStep", e); return false; }
        }

        /// <summary>MineralOnce.Postfix: lista produkcji BK wsi zastosowanej - towary spoza KS bez zmian (konie, drob, papirus ...), towary KS ze stawka > 0 (brakujace
        /// dopisane z waga 0); null - wies poza KS (lista jak dotad).</summary>
        internal static List<(ItemObject, float)> ListStep(Village v, List<(ItemObject, float)> list)
        {
            if (v == null || list == null || _state != StateApplied) return null;
            var rates = _rates; float[] a; var ix = _itemIx; var items = _items;
            if (ix == null || items == null || rates.Count == 0 || !rates.TryGetValue(v, out a) || !KeyOn) return null;
            var res = new List<(ItemObject, float)>(list.Count + 8);
            var seen = new bool[ItemCount];
            foreach (var e in list)
            {
                int i;
                if (e.Item1 != null && ix.TryGetValue(e.Item1, out i)) { if (seen[i]) continue; seen[i] = true; }
                res.Add(e);
            }
            for (int i = 0; i < ItemCount; i++) if (!seen[i] && items[i] != null && a[i] > 0f) res.Add((items[i], 0f));
            return res;
        }

        /// <summary>VillageFruit.CapPostfix (pierwszy krok): magazyn wsi zastosowanej = W gry + 5 dob towarow KS spoza listy typu. true - dodatki owocow i jaj pominiete.</summary>
        internal static bool CapOverride(Village v, ref int result)
        {
            if (v == null || _state != StateApplied) return false;
            var rates = _rates; float[] a; var items = _items;
            if (items == null || rates.Count == 0 || !rates.TryGetValue(v, out a) || !KeyOn) return false;
            try
            {
                if (v.VillageState != Village.VillageStates.Normal || v.VillageType == null) return true;
                double x = 0;
                for (int i = 0; i < ItemCount; i++)
                {
                    if (!(a[i] > 0f) || items[i] == null) continue;
                    bool inType = false;
                    foreach (var p in v.VillageType.Productions) if (p.Item1 == items[i]) { inType = true; break; }
                    if (!inType) x += a[i];
                }
                int add = (int)Math.Ceiling(5.0 * x - 1e-4);
                if (add > 0) result += add;
            }
            catch { }
            return true;
        }

        /// <summary>Prefiks VillageGoodProductionCampaignBehavior.TickFoodProduction: wies zastosowana nie dostaje losowej zywnosci z niczego (KS liczy jej plon sam).</summary>
        public static bool TickFoodPrefix(Village __0, bool __1)
        {
            try { return __1 || !AppliedFor(__0); } catch { return true; }
        }

        /// <summary>Famine.Daily: plon spichlerza wsi zastosowanej = max(racje KS z czesci domowej okregu, wzor dzis) - podloga do czasu zakupow ZC4.</summary>
        internal static double GranaryFloor(Village v, double harvest)
        {
            if (v == null || _state != StateApplied || !KeyOn) return harvest;
            try
            {
                var st = v.Settlement; LiveDist ld;
                if (st == null || st.StringId == null || !_live.TryGetValue(st.StringId, out ld) || !(ld.Rations >= 0)) return harvest;
                if (ld.Rations > harvest) { _granKsAbove++; return ld.Rations; }
                _granFloorUp++;
                return harvest;
            }
            catch { return harvest; }
        }
        private static int _granFloorUp, _granKsAbove;

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var m = AccessTools.Method(typeof(VillageGoodProductionCampaignBehavior), "TickFoodProduction", new[] { typeof(Village), typeof(bool) });
                if (m != null) h.Patch(m, prefix: new HarmonyMethod(typeof(StartCalibration), nameof(TickFoodPrefix)) { priority = Priority.High });
                Log.Info("StartCalibration (KS): zywnosc wsi gry dla wsi zastosowanych " + (m != null ? "wpieta (TickFoodProduction)" : "BRAK TickFoodProduction") + "; produkcja KS przez MaterialLaw.ProdPostfix, MineralOnce, magazyn wsi i spichlerz (bez wlasnych latek).");
            }
            catch (Exception e) { Log.Error("StartCalibration.ApplyAll", e); }
        }

        // ============================================================ budowa stanu (z Planu) i rachunek okregu
        /// <summary>Stan blokow z Planu: kazdy okreg z pliku ziemi w stalej kolejnosci (20 blokow wsi gry albo pol mieszczan, potem wioski poboczne pliku);
        /// udzialy zajec z logitu (>= 0.2% rak), regale (udzial rak bloku przy zlozu), renta ziemi. Blok bez Planu (wioska poza ukladem) - zajecie startowe.</summary>
        private static List<LiveDist> BuildLive(KsResult R, KsInput inp, bool forApply)
        {
            var list = new List<LiveDist>(inp.Districts.Count);
            var byUid = new Dictionary<string, KsBlock>(StringComparer.Ordinal);
            foreach (var d in inp.Districts)
            {
                LandDist land;
                string lid = d.TownFields ? d.Id.Replace("#pola", "") : d.Id;
                if (!(d.TownFields ? _landTown : _landVil).TryGetValue(lid, out land)) continue;
                double[] need; R.DistNeed.TryGetValue(d.Id, out need);
                var ld = new LiveDist { Id = d.Id, TF = d.TownFields, Market = d.Market, L = land, K = d };
                if (need != null) for (int k = 0; k < KsTab.NN; k++) ld.NeedMult[k] = d.Need0[k] > 1e-9 ? (float)Math.Max(0.25, Math.Min(4.0, need[k] / d.Need0[k])) : 1f;
                if (!d.TownFields && d.PplHome != null && d.Bask != null)
                {
                    double home = 0; foreach (var v in d.PplHome) home += v;
                    KsCore.NeedsOfKeep(d.PplHome, d.Bask, _keep, d.Oil, home, ld.OwnNeed);
                    for (int k = 0; k < KsTab.NN; k++) ld.OwnNeed[k] *= ld.NeedMult[k];
                    ld.PeopleHome = home;
                }
                byUid.Clear(); foreach (var b in d.Blocks) if (b.Uid != null) byUid[b.Uid] = b;
                int n = 20 + (d.TownFields ? 0 : land.Side.Count);
                ld.B = new LiveBlock[n];
                for (int i = 0; i < n; i++)
                {
                    var row = i < 20 ? land.Main[i] : land.Side[i - 20];
                    var lb = new LiveBlock { Uid = row.Uid };
                    KsBlock kb;
                    if (byUid.TryGetValue(row.Uid, out kb)) FromPlan(R, kb, lb);
                    else
                    {
                        int st;
                        string start = i < 20 ? (d.TownFields ? "garden" : d.Type) : row.Start;
                        if (start == null || !KsTab.CraftIndex.TryGetValue(start, out st) || KsTab.IsRegale[st]) st = KsTab.CSheep;
                        lb.C = new[] { (byte)st }; lb.X = new[] { 1f };
                    }
                    lb.K = kb;
                    ld.B[i] = lb;
                }
                list.Add(ld);
            }
            return list;
        }

        private static void FromPlan(KsResult R, KsBlock kb, LiveBlock lb)
        {
            int b = kb.Index, a = R.BlockStart[b], e = R.BlockStart[b + 1];
            var cs = new List<byte>(); var xs = new List<float>(); double tot = 0;
            for (int k = a; k < e; k++) if (R.X[k] >= 0.002) { cs.Add((byte)R.PairCraft[k]); xs.Add((float)R.X[k]); tot += R.X[k]; }
            if (cs.Count == 0) { int best = a; for (int k = a; k < e; k++) if (R.X[k] > R.X[best]) best = k; cs.Add((byte)R.PairCraft[best]); xs.Add(1f); tot = 1; }
            for (int i = 0; i < xs.Count; i++) xs[i] = (float)(xs[i] / tot);
            lb.C = cs.ToArray(); lb.X = xs.ToArray();
            double W = R.BlockW[b];
            var rc = new List<byte>(); var rf = new List<float>();
            if (W > 0) foreach (int c in KsTab.Regale) { double hnd = R.RegHands[b * KsTab.NC + c]; if (hnd > 0) { rc.Add((byte)c); rf.Add((float)(hnd / W)); } }
            lb.RC = rc.Count > 0 ? rc.ToArray() : null; lb.RF = rf.Count > 0 ? rf.ToArray() : null;
            lb.Rent = (float)R.Rent[b];
        }

        /// <summary>Produkcja okregu w kg/d na towar dostawy, zjedzone w okregu, na targ (kg i sztuki gry). factors - zima, E4/5 i E4/7 (w grze; Plan - nie: rece Planu juz z czynnikami rak).</summary>
        private static DistResult ComputeDistrict(LiveDist ld, bool factors, string unused)
        {
            var d = ld.K;
            if (d == null || ld.B == null) return null;
            EnsureItems();
            var res = new DistResult();
            var prod = res.Prod;
            string type = d.Type;
            for (int i = 0; i < ld.B.Length; i++)
            {
                var lb = ld.B[i];
                if (lb == null || lb.K == null || lb.C == null) continue;
                var kb = lb.K;
                double W, wH, lg, lh;
                KsCore.HomeLabour(d.Wf, kb.Share, d.Need0[KsTab.NH], kb.O, type, out W, out wH, out lg, out lh);
                lb.Hands = W; lb.HomeFrac = W > 0 ? wH / W : 0;
                double rsum = 0; if (lb.RF != null) for (int j = 0; j < lb.RF.Length; j++) rsum += lb.RF[j];
                double scale = Math.Max(0.0, 1.0 - rsum);
                double rem = W - wH;
                for (int j = 0; j < lb.C.Length; j++)
                {
                    int c = lb.C[j]; double x = lb.X[j] * scale;
                    if (!(x > 0)) continue;
                    prod[IEgg] += x * wH * KsTab.PHEgg * lh; prod[IHoney] += x * wH * KsTab.PHHoney * lh;
                    double sub = 0;
                    if (c != KsTab.CWheat && lg > 0)
                    {
                        sub = Math.Min(rem, KsTab.OwnGrainShare(lg) * KsCore.GrainOwnNeed(d.Need0) * kb.Share / (KsTab.PGrainSub * lg));
                        prod[IGrain] += x * sub * KsTab.PGrainSub * lg;
                    }
                    double cl = rem - sub;
                    double L = KsCore.LandEff(kb, c, type);
                    foreach (var kv in KsTab.P0[c])
                    {
                        int it = kv.Key == KsTab.GWhaleMeat && c == KsTab.CHunter ? IMeat : GoodToItem[kv.Key];
                        if (it >= 0) prod[it] += x * cl * kv.Value * L;
                    }
                }
                if (lb.RC != null)
                    for (int j = 0; j < lb.RC.Length; j++)
                    {
                        int c = lb.RC[j]; double lab = lb.RF[j] * W;
                        double L = (kb.Main && KsTab.Crafts[c] == type) ? KsCore.LandEff(kb, c, type) : KsTab.Land2(c, kb.O, type);
                        foreach (var kv in KsTab.P0[c]) { int it = GoodToItem[kv.Key]; if (it >= 0) prod[it] += lab * kv.Value * L; }
                    }
            }
            double f = 1.0;
            if (factors)
            {
                try
                {
                    if (ld.TF) f = 1.0 - WinterBite.TownCut(ld.T);
                    else if (ld.V != null) f = (1.0 - WinterBite.VillageCut(ld.V)) * Devastation.YieldFactor(ld.V) * FreeHands.YieldFactor(ld.V);
                }
                catch { f = 1.0; }
                if (!(f >= 0) || double.IsInfinity(f)) f = 1.0;
                for (int i = 0; i < ItemCount; i++) prod[i] *= f;
            }
            // co okreg zje sam (wies; pola mieszczan - nic, ich ludzie sa w potrzebach miasta)
            var own = res.Own;
            if (!ld.TF)
            {
                var n = ld.OwnNeed;
                var want = new double[ItemCount];
                want[IGrain] = n[KsTab.NG_] + n[KsTab.NBeer] * KsTab.BeerGrain;
                want[IFish] = n[KsTab.NFish]; want[ICheese] = n[KsTab.NCheese]; want[IButter] = n[KsTab.NButter]; want[IEgg] = n[KsTab.NEgg];
                want[IApple] = n[KsTab.NApple]; want[ICarrot] = n[KsTab.NCarrot]; want[IOrange] = n[KsTab.NOrange]; want[IDate] = n[KsTab.NDate];
                want[IGrape] = n[KsTab.NGrape] + n[KsTab.NWine] * KsTab.WineGrape;
                want[IOlives] = n[KsTab.NOlives] + n[KsTab.NOil] * KsTab.OilOlive;
                want[IHoney] = n[KsTab.NHoney] + n[KsTab.NMead] * KsTab.MeadHoney;
                want[IFlax] = n[KsTab.NPlotno] * KsTab.LinenFlax; want[IWool] = n[KsTab.NSukno] * KsTab.FeltWool;
                for (int i = 0; i < ItemCount; i++) if (want[i] > 0) own[i] = Math.Min(prod[i], want[i]);
                // mieso: dziczyzna i mieso wielorybow najpierw, potem uboj wlasnego zywca (mieso = polowa zywca)
                double meat = n[KsTab.NMeat];
                foreach (int it in new[] { IMeat, IWhaleMeat }) { double t = Math.Min(prod[it], meat); own[it] = t; meat -= t; }
                double live = prod[ICow] + prod[IHog] + prod[ISheep];
                if (meat > 0 && live > 0)
                {
                    double need = Math.Min(live, meat / KsTab.MeatOfLive), k = need / live;
                    own[ICow] = prod[ICow] * k; own[IHog] = prod[IHog] * k; own[ISheep] = prod[ISheep] * k;
                }
                for (int i = 0; i < ItemCount; i++) res.OwnKcal += own[i] * ItemKcal[i];
                res.OwnKcal += n[KsTab.NH] * 1200.0;
                // pelna potrzeba kcal ludzi w domu (caly koszyk - takze to, co z targu): racja = jedzenie jednego czlowieka na dobe
                var a = d.Need0;
                double kc = a[KsTab.NG_] * 3300 + a[KsTab.NH] * 1200 + a[KsTab.NMeat] * 2200 + a[KsTab.NFish] * 1100 + (a[KsTab.NCheese] + a[KsTab.NButter] + a[KsTab.NEgg]) * 2000
                            + (a[KsTab.NApple] + a[KsTab.NCarrot] + a[KsTab.NOrange] + a[KsTab.NGrape] + a[KsTab.NOlives] + a[KsTab.NDate]) * 450 + a[KsTab.NOil] * 8800
                            + a[KsTab.NHoney] * 3000 + a[KsTab.NBeer] * 400 + a[KsTab.NWine] * 750 + a[KsTab.NMead] * 1000;
                res.KcalNeed = d.People > 0 ? kc * Math.Min(1.0, ld.PeopleHome / d.People) : 0.0;
            }
            double sigma = Math.Max(1, DailyLife.KsSigma);
            for (int i = 0; i < ItemCount; i++)
            {
                double m = Math.Max(0.0, prod[i] - own[i]);
                res.MarketKg[i] = m;
                res.Market[i] = m / (sigma * _itemKg[i]);
            }
            return res;
        }

        // ============================================================ wlaczenie (Apply)
        private static void Apply(int today)
        {
            var list = BuildLive(_plan, _planIn, true);
            var map = new Dictionary<string, LiveDist>(StringComparer.Ordinal);
            int nb = 0;
            foreach (var ld in list) { map[ld.Id] = ld; nb += ld.B.Length; foreach (var b in ld.B) b.K = null; }
            _live = map;
            _state = StateApplied; _applyDay = today;
            try { _campaign = Campaign.Current != null ? Campaign.Current.UniqueGameId ?? "" : ""; } catch { _campaign = ""; }
            uint lay; LayoutMatches(out lay);
            _saveLandCrc = _landCrc; _saveLayoutCrc = lay; _saveSigma = DailyLife.KsSigma;
            RuleReset();
            ResolveLive();
            Log.Info(PK + "WLACZONE doba " + today + " (autotest: klucz Start Calibration Autotest Apply albo plik " + ApplyFlagName + ") - zajecia blokow z Planu doby " + _planDay + " (plan nr " + _plans + "): okregow " + map.Count
                     + ", blokow " + nb + "; od jutra wsie robia tyle, ile daja ich rece na ich ziemi (stawki KS), zywnosc wsi gry z niczego wylaczona, pola mieszczan na polki miast"
                     + " | nieodwracalne: towar juz wyprodukowany i sprzedany (i zloto, ktore przy tym przeszlo zwyklym handlem); KS nie rusza kies ludu ani racji spichlerzy"
                     + " | wylacznik: Start Calibration = off (nastepna doba produkcja jak dzis).");
            LiveDaily(today);
        }

        /// <summary>Powiazanie stanu z obiektami gry (sesja): wsie, miasta, bloki z ziemi pliku, kandydaci regule zmiany. Okreg bez wsi albo pliku - pominiety (licznik).</summary>
        private static void ResolveLive()
        {
            EnsureItems();
            int bad = 0;
            foreach (var ld in _live.Values)
            {
                try
                {
                    string lid = ld.TF ? ld.Id.Replace("#pola", "") : ld.Id;
                    var st = Settlement.Find(lid);
                    LandDist land;
                    if (st == null || !(ld.TF ? _landTown : _landVil).TryGetValue(lid, out land)) { bad++; ld.St = null; continue; }
                    ld.St = st; ld.L = land;
                    if (ld.TF) ld.T = st.Town; else ld.V = st.Village;
                    if ((ld.TF && ld.T == null) || (!ld.TF && ld.V == null)) { bad++; ld.St = null; continue; }
                    string type = ld.TF ? "town_fields" : (ld.V.VillageType != null ? ld.V.VillageType.StringId : "wheat_farm");
                    var kd = new KsDistrict { Id = ld.Id, Type = type, Market = ld.Market, TownFields = ld.TF, Area = land.Area };
                    for (int i = 0; i < ld.B.Length; i++)
                    {
                        var row = i < 20 ? land.Main[i] : (i - 20 < land.Side.Count ? land.Side[i - 20] : null);
                        if (row == null) { ld.B[i] = null; continue; }
                        var o = row.O;
                        if (!ld.TF && i == 0 && IsFishType(type) && o.DSea <= 12 && o.Kind != "coast") { o = o.Clone(); o.Kind = "coast"; }
                        if (ld.B[i] == null) continue;
                        var kb = new KsBlock { Uid = row.Uid, Main = i < 20, Block0 = !ld.TF && i == 0, Start = i < 20 ? (ld.TF ? "garden" : type) : row.Start, O = o, Share = i < 20 ? 0.05 : 0.0 };
                        kd.Blocks.Add(kb);
                        ld.B[i].K = kb; ld.B[i].Uid = row.Uid; ld.B[i].Cand = KsCore.Candidates(kb, type);
                    }
                    ld.K = kd;
                }
                catch (Exception e) { bad++; ld.St = null; Stumble("ResolveLive", e); }
            }
            _resolveBad = bad;
            // stawki z zapisu - od razu (pierwsza doba po wczytaniu), LiveDaily policzy nowe w pierwszym takcie doby
            var rates = new Dictionary<Village, float[]>();
            foreach (var ld in _live.Values) if (!ld.TF && ld.V != null && ld.Saved != null) rates[ld.V] = ld.Saved;
            if (rates.Count > 0) { _rates = rates; _ratesKeep = rates; }
        }
        private static int _resolveBad;

        // ============================================================ doba stanu zastosowanego
        private static readonly double[][] _keep = BuildKeep();
        private static double[][] BuildKeep()
        {
            var k = new double[6][];
            for (int e = 0; e < 6; e++) { k[e] = new double[13]; for (int g = 0; g < 13; g++) k[e][g] = DailyLife.KsKeepVillage(e, g); }
            return k;
        }

        private static void LiveDaily(int today)
        {
            int day = today - 1;
            var recs = new List<DailyLife.KsRec>(900); int zc;
            bool fresh = DailyLife.KsSnapshot(recs, today, out zc);
            var byS = new Dictionary<Settlement, DailyLife.KsRec>();
            foreach (var r in recs) if (r.St != null) byS[r.St] = r;
            var rates = new Dictionary<Village, float[]>();
            var side = new Dictionary<string, double>(StringComparer.Ordinal);
            uint lay; bool layout = LayoutMatches(out lay);
            var mk = new double[ItemCount]; var tfMade = new double[ItemCount];
            int nv = 0, ntf = 0, stale = 0; long tfPaid = 0; int tfPieces = 0, tfUnpaid = 0;
            double ownKcal = 0, kcalNeed = 0;
            int granUp = _granFloorUp, granAbove = _granKsAbove;          // Famine.Daily biegnie wczesniej w tej samej dobie
            _granFloorUp = _granKsAbove = 0;
            var regionHands = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (var ld in _live.Values)
            {
                if (ld.St == null || ld.K == null) continue;
                try
                {
                    DailyLife.KsRec rec;
                    if (!byS.TryGetValue(ld.TF ? ld.T.Settlement : ld.V.Settlement, out rec)) { stale++; if (!ld.TF && ld.V != null) { float[] old; if (_ratesKeep.TryGetValue(ld.V, out old)) rates[ld.V] = old; } continue; }
                    var bask = DailyLife.KsBasket(rec.Cul, true);
                    bool oil = DailyLife.OilProf(rec.ProfId), north = DailyLife.NorthFur(rec.ProfId);
                    var kd = ld.K;
                    if (ld.TF)
                    {
                        kd.Wf = KsCore.TownFieldHands(rec.N);
                        foreach (var b in kd.Blocks) b.Share = 0.05;
                        Array.Clear(kd.Need0, 0, KsTab.NN);
                    }
                    else
                    {
                        double away = 0; try { away = PeopleBook.RefugeesOf(ld.V); } catch { away = 0; }
                        double home = Math.Max(0.0, rec.L), all = home + Math.Max(0.0, away);
                        var ppl = new double[6]; var pplH = new double[6];
                        for (int e = 0; e < 6; e++) { ppl[e] = all * rec.Share[e]; pplH[e] = home * rec.Share[e]; }
                        KsCore.NeedsOf(ppl, bask, oil, north, true, false, all, kd.Need0);
                        kd.People = all; kd.Wf = KsCore.VillageHands(ppl);
                        KsCore.NeedsOfKeep(pplH, bask, _keep, oil, home, ld.OwnNeed);
                        for (int k = 0; k < KsTab.NN; k++) ld.OwnNeed[k] *= ld.NeedMult[k];
                        ld.PeopleHome = home;
                        // udzialy osad
                        double main = 1.0; bool hs = false;
                        if (layout) { try { hs = VillageBook.KsShares(ld.V, side, out main); } catch { hs = false; } }
                        if (!hs) { main = 1.0; side.Clear(); }
                        double tot = 0;
                        for (int i = 0; i < ld.B.Length; i++)
                        {
                            var lb = ld.B[i]; if (lb == null || lb.K == null) continue;
                            double sh = 0;
                            if (i < 20) sh = main / 20.0; else side.TryGetValue(lb.K.Uid, out sh);
                            lb.K.Share = sh > 0 ? sh : 0; tot += lb.K.Share;
                        }
                        if (tot > 0) foreach (var b in kd.Blocks) b.Share /= tot;
                    }
                    ld.Wf = kd.Wf;
                    var res = ComputeDistrict(ld, true, null);
                    if (res == null) continue;
                    string reg = ld.TF ? ld.St.StringId : (ld.V.Bound != null ? ld.V.Bound.StringId : ld.St.StringId);
                    double rh; regionHands.TryGetValue(reg ?? "-", out rh); regionHands[reg ?? "-"] = rh + kd.Wf;
                    if (ld.TF)
                    {
                        ntf++;
                        int pcs; long paid; int unpaid;
                        DeliverTownFields(ld, res, out pcs, out paid, out unpaid, tfMade);
                        tfPieces += pcs; tfPaid += paid; tfUnpaid += unpaid;
                        continue;
                    }
                    nv++;
                    var a = new float[ItemCount];
                    for (int i = 0; i < ItemCount; i++) { a[i] = (float)res.Market[i]; mk[i] += res.Market[i]; }
                    rates[ld.V] = a;
                    ld.Rations = res.KcalNeed > 0 ? ld.PeopleHome * Math.Min(1.5, res.OwnKcal / res.KcalNeed) : -1;
                    ownKcal += res.OwnKcal; kcalNeed += res.KcalNeed;
                }
                catch (Exception e) { Stumble("LiveDaily(okreg)", e); }
            }
            _ratesKeep = rates;
            _rates = rates;
            _regionHands = regionHands;
            try { RuleDaily(today); } catch (Exception e) { Stumble("RuleDaily", e); }
            if (!LogGate.Lines) return;
            var sb = new StringBuilder(2500);
            sb.Append(PK).Append("produkcja (zastosowane) dzien ").Append(day).Append(" | od doby ").Append(_applyDay).Append(" | wsie ze stawkami KS ").Append(nv).Append(", pola mieszczan ").Append(ntf)
              .Append(stale > 0 ? ", bez stanow ZC dzis " + stale + " (stawki wczorajsze)" : "").Append(fresh ? "" : " (stany ZC nieswieze)")
              .Append(" | na targ jutro szt./d (wsie; w nawiasie wsie wczoraj z ksiegi towarow):");
            bool first = true;
            for (int i = 0; i < ItemCount; i++)
            {
                double y = Ema(ItemIds[i], GoodsLedger.KsVil);
                if (mk[i] < 0.5 && y < 0.5) continue;
                sb.Append(first ? " " : ", ").Append(ItemIds[i]).Append(' ').Append(F0(mk[i])).Append(" (").Append(F0(LastLedger(ItemIds[i], GoodsLedger.KsVil))).Append(')');
                first = false;
            }
            int tfn; long tfp2 = TownFolk.KsTakePaid(out tfn);
            sb.Append(" | pola mieszczan: na polki ").Append(tfPieces).Append(" szt., zaplacone ludowi ").Append(tfPaid).Append(" zl (").Append(tfp2).Append(" zl w ").Append(tfn).Append(" przelewach kiesy), bez zaplaty (kasa pusta) ").Append(tfUnpaid).Append(" szt.");
            sb.Append(" | spichlerz: racje KS ponad wzor dzis w ").Append(granAbove).Append(" wsiach, podloga (wzor dzis) w ").Append(granUp)
              .Append(" (kcal z wlasnego pola i domu ").Append(kcalNeed > 0 ? (100 * ownKcal / kcalNeed).ToString("0", Inv) : "-").Append("% potrzeby)");
            sb.Append(" | ").Append(RuleText()).Append(" | potkniecia dzis ").Append(_stumblesDay).Append(" (sesja ").Append(_stumbles).Append(", haki ").Append(_liveStumbles)
              .Append(", okregi bez obiektu gry ").Append(_resolveBad).Append(").");
            Log.Info(sb.ToString());
        }
        private static Dictionary<Village, float[]> _ratesKeep = new Dictionary<Village, float[]>();
        private static Dictionary<string, double> _regionHands = new Dictionary<string, double>(StringComparer.Ordinal);

        private static double LastLedger(string id, int k) { double[] a; return _ledgerTmp.TryGetValue(id, out a) && a != null && k < a.Length ? a[k] : 0.0; }

        private static void LivePublishOff()
        {
            if (_rates.Count > 0) _rates = new Dictionary<Village, float[]>();
        }

        /// <summary>Plon pol mieszczan na polke miasta: ulamki sztuk przechodza na jutro; kazda sztuka placona przez kase miasta ludowi po cenie skupu miasta - bez zaplaty nie wchodzi.</summary>
        private static void DeliverTownFields(LiveDist ld, DistResult res, out int pieces, out long paid, out int unpaid, double[] made)
        {
            pieces = 0; paid = 0; unpaid = 0;
            var town = ld.T;
            if (town == null || town.Settlement == null || town.Settlement.ItemRoster == null || ld.St.IsUnderSiege) return;
            var f = GoodsLedger.Begin(GoodsLedger.FKs, town);
            if (f != null) f.Info = "pola mieszczan (KS)";
            try
            {
                for (int i = 0; i < ItemCount; i++)
                {
                    var it = _items[i];
                    if (it == null) continue;
                    ld.Frac[i] += res.Market[i];
                    int n = (int)Math.Floor(ld.Frac[i]);
                    if (n <= 0) continue;
                    ld.Frac[i] -= n;
                    int price = 0;
                    try { price = town.MarketData.GetPrice(it, null, true, null); } catch { price = Math.Max(1, it.Value); }
                    if (price < 1) price = 1;
                    int can = (int)Math.Min(n, Math.Max(0, town.Gold) / price);   // tyle sztuk, na ile kasa ma - placone co do grosza
                    if (can < n) unpaid += n - can;
                    if (can <= 0) continue;
                    long want = (long)can * price;
                    int coffer0 = town.Gold;
                    long got = TownFolk.KsFieldPay(town.Settlement, want);
                    _mTfTown += coffer0 - town.Gold; _mTfFolk += got;
                    int ok = got >= want ? can : 0;                                // kiesa nieczynna albo czesciowa zaplata - towar nie wchodzi
                    if (ok < can) unpaid += can - ok;
                    if (ok <= 0) continue;
                    town.Settlement.ItemRoster.AddToCounts(it, ok);
                    pieces += ok; paid += got; made[i] += ok;
                }
            }
            catch (Exception e) { Stumble("DeliverTownFields", e); }
            finally { GoodsLedger.End(f); }
        }

        // ============================================================ zapis stanu zastosowanego (kompaktowo: bloki w stalej kolejnosci, "=" - jak poprzedni)
        private const string B36 = "0123456789abcdefghijklmnopqrstuvwxyz";
        private static string E36(int v, int w) { var c = new char[w]; for (int i = w - 1; i >= 0; i--) { c[i] = B36[v % 36]; v /= 36; } return new string(c); }
        private static int D36(string s, int a, int w) { int v = 0; for (int i = 0; i < w; i++) { int d = B36.IndexOf(s[a + i]); if (d < 0) return -1; v = v * 36 + d; } return v; }

        private static string BlockText(LiveBlock b)
        {
            if (b == null || b.C == null) return "-";
            var sb = new StringBuilder(24);
            for (int i = 0; i < b.C.Length; i++) sb.Append(B36[b.C[i]]).Append(E36(Math.Max(0, Math.Min(1295, (int)Math.Round(b.X[i] * 1000))), 2));
            sb.Append('^');
            if (b.RC != null) for (int i = 0; i < b.RC.Length; i++) sb.Append(B36[b.RC[i]]).Append(E36(Math.Max(0, Math.Min(46655, (int)Math.Round(b.RF[i] * 40000))), 3));
            sb.Append('^').Append(E36(Math.Max(0, Math.Min(1295, (int)Math.Round(b.Rent * 2000))), 2));
            if (b.Held > 0 || b.Target >= 0)
                sb.Append('^').Append(b.Held.ToString(Inv)).Append(',').Append(b.Cur).Append(',').Append(b.Target).Append(',').Append(b.TStart).Append(',').Append(b.TEnd).Append(',')
                  .Append(b.X0.ToString("0.####", Inv)).Append(',').Append(b.Cost.ToString("0.#", Inv)).Append(',').Append(b.Paid.ToString("0.#", Inv)).Append(',').Append(b.H0.ToString("0", Inv));
            return sb.ToString();
        }

        private static void ExportLive(StringBuilder sb)
        {
            bool firstD = true;
            foreach (var ld in _live.Values)
            {
                if (!firstD) sb.Append(';');
                firstD = false;
                sb.Append(Clean(ld.Id)).Append('~').Append(ld.TF ? 'T' : 'V').Append('~').Append(ld.Market).Append('~');
                bool fm = true;
                for (int k = 0; k < KsTab.NN; k++)
                    if (Math.Abs(ld.NeedMult[k] - 1f) > 1e-4f) { if (!fm) sb.Append(','); fm = false; sb.Append(k).Append(':').Append(ld.NeedMult[k].ToString("0.####", Inv)); }
                sb.Append('~');
                string prev = null;
                for (int i = 0; i < ld.B.Length; i++)
                {
                    if (i > 0) sb.Append('!');
                    string t = BlockText(ld.B[i]);
                    if (prev != null && t == prev && i < 20) sb.Append('='); else sb.Append(t);
                    prev = t;
                }
                // stawki na jutro (wies) - po wczytaniu dzialaja od razu, zanim pierwsza doba policzy nowe
                sb.Append('~');
                float[] r;
                if (!ld.TF && ld.V != null && _rates.TryGetValue(ld.V, out r) && r != null)
                {
                    bool fr = true;
                    for (int i = 0; i < r.Length; i++) if (r[i] > 0.0005f) { if (!fr) sb.Append(','); fr = false; sb.Append(i).Append(':').Append(r[i].ToString("0.####", Inv)); }
                }
            }
        }

        private static LiveBlock ParseBlock(string t)
        {
            if (t == "-" || t.Length == 0) return null;
            var p = t.Split('^');
            if (p.Length < 3) throw new FormatException("blok");
            var b = new LiveBlock();
            int n = p[0].Length / 3; b.C = new byte[n]; b.X = new float[n];
            for (int i = 0; i < n; i++) { int c = B36.IndexOf(p[0][3 * i]); int v = D36(p[0], 3 * i + 1, 2); if (c < 0 || c >= KsTab.NC || v < 0) throw new FormatException("zajecie"); b.C[i] = (byte)c; b.X[i] = v / 1000f; }
            double s = 0; foreach (var x in b.X) s += x; if (s > 0) for (int i = 0; i < n; i++) b.X[i] = (float)(b.X[i] / s);
            int m = p[1].Length / 4;
            if (m > 0) { b.RC = new byte[m]; b.RF = new float[m]; for (int i = 0; i < m; i++) { int c = B36.IndexOf(p[1][4 * i]); int v = D36(p[1], 4 * i + 1, 3); if (c < 0 || c >= KsTab.NC || v < 0) throw new FormatException("regale"); b.RC[i] = (byte)c; b.RF[i] = v / 40000f; } }
            int r = D36(p[2], 0, 2); b.Rent = r >= 0 ? r / 2000f : 0f;
            if (p.Length >= 4)
            {
                var a = p[3].Split(',');
                if (a.Length >= 9)
                {
                    int.TryParse(a[0], NumberStyles.Integer, Inv, out b.Held);
                    int cur, tg; int.TryParse(a[1], NumberStyles.Integer, Inv, out cur); int.TryParse(a[2], NumberStyles.Integer, Inv, out tg); b.Cur = (sbyte)cur; b.Target = (sbyte)tg;
                    int.TryParse(a[3], NumberStyles.Integer, Inv, out b.TStart); int.TryParse(a[4], NumberStyles.Integer, Inv, out b.TEnd);
                    float.TryParse(a[5], NumberStyles.Float, Inv, out b.X0); float.TryParse(a[6], NumberStyles.Float, Inv, out b.Cost); float.TryParse(a[7], NumberStyles.Float, Inv, out b.Paid); float.TryParse(a[8], NumberStyles.Float, Inv, out b.H0);
                }
            }
            return b;
        }

        private static bool ImportLive(string s, out int nd, out int nb, out int bad)
        {
            nd = nb = bad = 0;
            var map = new Dictionary<string, LiveDist>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(s)) { _live = map; return true; }
            foreach (var ent in s.Split(';'))
            {
                if (ent.Length == 0) continue;
                try
                {
                    var f = ent.Split('~');
                    if (f.Length < 5) { bad++; continue; }
                    var ld = new LiveDist { Id = f[0], TF = f[1] == "T" };
                    int mk; int.TryParse(f[2], NumberStyles.Integer, Inv, out mk); ld.Market = mk == 0 ? 0 : 1;
                    if (f[3].Length > 0)
                        foreach (var kv in f[3].Split(','))
                        {
                            var q = kv.Split(':'); int k; float v;
                            if (q.Length == 2 && int.TryParse(q[0], NumberStyles.Integer, Inv, out k) && k >= 0 && k < KsTab.NN && float.TryParse(q[1], NumberStyles.Float, Inv, out v) && v > 0) ld.NeedMult[k] = v;
                        }
                    var bs = f[4].Split('!');
                    ld.B = new LiveBlock[bs.Length];
                    string prevT = null; LiveBlock prev = null;
                    for (int i = 0; i < bs.Length; i++)
                    {
                        string t = bs[i] == "=" && prevT != null ? prevT : bs[i];
                        var b = ParseBlock(t);
                        ld.B[i] = b; prevT = t; prev = b;
                        if (b != null) nb++;
                    }
                    if (f.Length >= 6 && f[5].Length > 0)
                    {
                        var sv = new float[ItemCount];
                        foreach (var kv in f[5].Split(','))
                        {
                            var q = kv.Split(':'); int k; float v;
                            if (q.Length == 2 && int.TryParse(q[0], NumberStyles.Integer, Inv, out k) && k >= 0 && k < ItemCount && float.TryParse(q[1], NumberStyles.Float, Inv, out v) && v > 0 && v < 1e6f) sv[k] = v;
                        }
                        ld.Saved = sv;
                    }
                    map[ld.Id] = ld; nd++;
                }
                catch { bad++; }
            }
            _live = map;
            return nd > 0 || bad == 0;
        }
    }
}
