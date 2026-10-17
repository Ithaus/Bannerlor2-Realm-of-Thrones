// KS-R4 koszt: mikro-pomiar typow operacji z goracych sciezek KS (bez gry; .NET Framework 4 x64 jak Bannerlord).
// Cel: kalibracja szacunkow ns/operacje (Array.IndexOf na string[], slowniki z kluczem obiektu gry, przeglad polki, Math.Pow, petla Income).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

class Obj   // jak MBObjectBase: wirtualne Equals/GetHashCode po Id
{
    public uint Id; public int Type; public string StringId;
    public override int GetHashCode() { return Id.GetHashCode(); }
    public override bool Equals(object o) { var b = o as Obj; return b != null && b.Id == Id; }
}
sealed class RefEq : IEqualityComparer<Obj> { public bool Equals(Obj a, Obj b) { return ReferenceEquals(a, b); } public int GetHashCode(Obj o) { return RuntimeHelpers.GetHashCode(o); } }
struct Elem { public Obj Item, Mod, Cos; public bool Quest; public int Amount; }
class Roster
{
    public Elem[] D; public int Count;
    [MethodImpl(MethodImplOptions.NoInlining)] public Elem GetElementCopyAtIndex(int i) { return D[i]; }
    public int GetItemNumber(Obj it) { int n = 0; for (int i = 0; i < Count; i++) if (D[i].Item == it) n += D[i].Amount; return n; }
    public int FindIndexOfElement(Obj it) { for (int i = 0; i < Count; i++) if (D[i].Item == it && D[i].Mod == null && D[i].Cos == null && !D[i].Quest) return i; return -1; }
}

static class P
{
    static double Time(string name, int reps, int perRep, Func<int, double> f)
    {
        f(1);   // JIT
        var sw = Stopwatch.StartNew(); double acc = 0;
        for (int r = 0; r < reps; r++) acc += f(r);
        double ns = sw.Elapsed.TotalMilliseconds * 1e6 / ((double)reps * perRep);
        Console.WriteLine(string.Format("{0,-58} {1,8:0.0} ns/op   (chk {2})", name, ns, acc % 7));
        return ns;
    }

    static void Main()
    {
        var rnd = new Random(1);
        string[] zids = { "grain", "bread", "pie", "meat", "WhaleMeat", "fish", "cheese", "butter", "Egg", "apple", "carrot", "orange", "grape", "olives", "date_fruit", "oil", "salt", "honey", "spice",
                          "beer", "wine", "mead", "linen", "felt", "leather", "tools", "pottery", "whale_oil", "hardwood", "fur", "flax", "wool", "hides", "iron", "clay", "limestone", "marble" };
        string[] itemIds = { "grain", "fish", "cheese", "butter", "Egg", "apple", "carrot", "orange", "grape", "olives", "date_fruit", "salt", "honey", "spice", "wool", "flax", "cotton",
                             "hardwood", "clay", "limestone", "marble", "iron", "silver", "gold_ore", "fur", "whale_oil", "walrus_tusk", "cow", "hog", "sheep", "WhaleMeat", "meat", "hides" };
        // kopie napisow (jak z XML - inne obiekty niz literaly)
        var q = new string[itemIds.Length]; for (int i = 0; i < q.Length; i++) q[i] = new string(itemIds[i].ToCharArray());
        // A: RampZ - Array.IndexOf(ZIds, ItemIds[i])
        Time("A Array.IndexOf(string[37], id) (RampZ)", 20000, q.Length, r => { int s = 0; for (int i = 0; i < q.Length; i++) s += Array.IndexOf(zids, q[i]); return s; });
        // B: StartIndex - PriceOne.Contains, GoodIndex.TryGetValue, Perish.Contains
        var priceOne = new HashSet<string>(StringComparer.Ordinal) { "Egg", "honey", "carrot", "apple", "hides", "cow", "hog", "sheep", "spice", "whale_oil", "walrus_tusk" };
        var perish = new HashSet<string>(StringComparer.Ordinal) { "fish", "meat", "WhaleMeat", "Egg", "butter", "apple", "carrot", "orange", "grape", "date_fruit" };
        var gidx = new Dictionary<string, int>(StringComparer.Ordinal); for (int i = 0; i < 34 && i < itemIds.Length; i++) gidx[itemIds[i]] = i;
        var zq = new string[zids.Length]; for (int i = 0; i < zq.Length; i++) zq[i] = new string(zids[i].ToCharArray());
        var Pp = new double[68]; for (int i = 0; i < Pp.Length; i++) Pp[i] = 0.7 + rnd.NextDouble();
        Func<string, double> startIndex = id =>
        {
            if (id == null || priceOne.Contains(id)) return 1.0;
            int g; if (!gidx.TryGetValue(id == "WhaleMeat" ? "meat" : id, out g)) return 1.0;
            double p = Pp[g]; p = Math.Max(0.7, Math.Min(1.5, p)); if (perish.Contains(id)) p = Math.Max(1.0, p); return p;
        };
        Time("B StartIndex (2 HashSet<string> + Dictionary<string,int>)", 20000, zq.Length, r => { double s = 0; for (int i = 0; i < zq.Length; i++) s += startIndex(zq[i]); return s; });
        // C: LandGradZ - TownLandOf (Dictionary<Obj,string>) + grad[land] + landCut[land]
        var towns = new Obj[97]; for (int i = 0; i < towns.Length; i++) towns[i] = new Obj { Id = (uint)(0x40000 + i * 13), StringId = "town_" + i };
        var townLand = new Dictionary<Obj, string>(); string[] lands = { "reach", "vale", "river", "north", "volantine", "qartheen", "dorne", "crownlands", "stormlands", "lys", "myr", "tyrosh" };
        foreach (var t in towns) townLand[t] = new string(lands[rnd.Next(lands.Length)].ToCharArray());
        var grad = new Dictionary<string, double[]>(StringComparer.Ordinal); var cut = new Dictionary<string, double[]>(StringComparer.Ordinal);
        foreach (var l in lands) { var a = new double[34]; for (int g = 0; g < 34; g++) a[g] = 0.9 + 0.2 * rnd.NextDouble(); grad[l] = a; if (rnd.Next(3) == 0) cut[l] = a; }
        Func<Obj, int, double> landGrad = (mk, g) =>
        {
            string land; if (!townLand.TryGetValue(mk, out land)) land = "?";
            double v = 1.0; double[] a, c;
            if (grad.TryGetValue(land, out a) && a[g] > 0) v = a[g];
            if (cut.TryGetValue(land, out c) && c[g] > 0 && c[g] < 1.0) v *= c[g];
            return v;
        };
        Time("C LandGradZ (Dictionary<Obj,string> + 2 x Dictionary<string,..>)", 20000, 37, r => { double s = 0; var t = towns[r % 97]; for (int z = 0; z < 37; z++) s += landGrad(t, z % 34); return s; });
        // D: ZTownOf - Dictionary<Obj, object> (domyslny komparator, wirtualne GetHashCode/Equals) i RefEq
        var map = new Dictionary<Obj, double[]>(); foreach (var t in towns) map[t] = new double[37];
        var mapR = new Dictionary<Obj, double[]>(new RefEq()); foreach (var t in towns) mapR[t] = new double[37];
        Time("D Dictionary<Obj,T>.TryGetValue (domyslny)", 20000, 97, r => { double s = 0; double[] a; for (int i = 0; i < 97; i++) if (map.TryGetValue(towns[i], out a)) s += a.Length; return s; });
        Time("D' Dictionary<Obj,T>.TryGetValue (RefEq)", 20000, 97, r => { double s = 0; double[] a; for (int i = 0; i < 97; i++) if (mapR.TryGetValue(towns[i], out a)) s += a.Length; return s; });
        // E: przeglad polki miasta: R wierszy (25% towary handlowe), GetElementCopyAtIndex + slownik indeksow koszyka (37 towarow) na kazdy wiersz
        int R = 300;
        var goods = new Obj[160]; for (int i = 0; i < goods.Length; i++) goods[i] = new Obj { Id = (uint)(0x10000 + i), Type = 1 };
        var gear = new Obj[2000]; for (int i = 0; i < gear.Length; i++) gear[i] = new Obj { Id = (uint)(0x20000 + i), Type = 2 };
        var zIx = new Dictionary<Obj, int>(); for (int z = 0; z < 37; z++) zIx[goods[z * 4]] = z;
        var zIxR = new Dictionary<Obj, int>(new RefEq()); foreach (var kv in zIx) zIxR[kv.Key] = kv.Value;
        var ro = new Roster { D = new Elem[R], Count = R };
        for (int i = 0; i < R; i++) ro.D[i] = new Elem { Item = i % 4 == 0 ? goods[rnd.Next(goods.Length)] : gear[rnd.Next(gear.Length)], Amount = 1 + rnd.Next(50) };
        var typeOk = new bool[4]; typeOk[1] = true;
        Time("E przeglad polki: kopia wiersza + slownik (na wiersz)", 5000, R, r => { int s = 0; for (int k = 0; k < ro.Count; k++) { var el = ro.GetElementCopyAtIndex(k); int zi; if (el.Item != null && el.Mod == null && zIx.TryGetValue(el.Item, out zi)) s += zi; } return s; });
        Time("E' przeglad polki: filtr typu przed slownikiem RefEq", 5000, R, r => { int s = 0; for (int k = 0; k < ro.Count; k++) { var el = ro.GetElementCopyAtIndex(k); int zi; if (el.Item != null && typeOk[el.Item.Type] && el.Mod == null && zIxR.TryGetValue(el.Item, out zi)) s += zi; } return s; });
        // H: GetItemNumber / FindIndexOfElement (liniowo) na polce R
        Time("H GetItemNumber(item) na polce 300 wierszy (na wywolanie)", 20000, 1, r => ro.GetItemNumber(goods[(r * 7) % 160]));
        Time("H' FindIndexOfElement na polce 300 wierszy (na wywolanie)", 20000, 1, r => ro.FindIndexOfElement(goods[(r * 7) % 160]));
        // F: Math.Pow (ShelfDays)
        Time("F Math.Pow(x, -1/0.6) (ShelfDays)", 200000, 1, r => 7.0 * Math.Pow(Math.Max(0.3, 0.5 + (r & 255) / 256.0), -1.0 / 0.6));
        // G: Income - petla po 34 towarach z rzadka macierza mv i Px(px, g) - na wywolanie; i z gotowa suma (L*V - koszt)
        var mv = new double[33 * 34]; for (int c = 0; c < 33; c++) for (int k = 0; k < 3; k++) mv[c * 34 + rnd.Next(34)] = rnd.NextDouble();
        var px = new double[33]; for (int i = 0; i < 33; i++) px[i] = 0.8 + 0.4 * rnd.NextDouble();
        var g2i = new int[34]; for (int g = 0; g < 34; g++) g2i[g] = g < 33 ? g : -1;
        Func<double[], int, double> Px = (a, g) => { if (g == 3) return a[31]; if (g == 33) return a[32]; int it = g2i[g]; return it >= 0 ? a[it] : 1.0; };
        Func<int, double> income = c => { double v = 0; int o = c * 34; for (int g = 0; g < 34; g++) { double m = mv[o + g]; if (m != 0) v += m * Px(px, g); } return 1.3 * v - 0.2 - 0.01 * 0.5; };
        Time("G Income (petla 34 towarow, rzadka mv)", 200000, 1, r => income(r % 33));
        var V = new double[33]; for (int c = 0; c < 33; c++) { double v = 0; int o = c * 34; for (int g = 0; g < 34; g++) { double m = mv[o + g]; if (m != 0) v += m * Px(px, g); } V[c] = v; }
        Time("G' Income z gotowa suma miasta V[c]", 200000, 1, r => 1.3 * V[r % 33] - 0.2 - 0.01 * 0.5);
        // I: Dictionary<string,double> po StringId (regionHands) - na operacje get+set
        var reg = new Dictionary<string, double>(StringComparer.Ordinal); var rid = new string[300]; for (int i = 0; i < rid.Length; i++) { rid[i] = "castle_village_" + i; reg[rid[i]] = 0; }
        Time("I Dictionary<string,double> get+set (regionHands)", 20000, 300, r => { double s = 0; for (int i = 0; i < 300; i++) { double v; reg.TryGetValue(rid[i], out v); reg[rid[i]] = v + 1; s += v; } return s; });
        // J: Dictionary<Obj,float[]> wstawienie do nowego slownika (rates/zv/zvp co dobe) - na wstawienie
        var vil = new Obj[661]; for (int i = 0; i < vil.Length; i++) vil[i] = new Obj { Id = (uint)(0x30000 + i * 3) };
        var arr = new float[33];
        Time("J nowy Dictionary<Obj,float[]> + 661 wstawien (na wstawienie)", 2000, 661, r => { var d = new Dictionary<Obj, float[]>(); for (int i = 0; i < vil.Length; i++) d[vil[i]] = arr; return d.Count; });
        // K: double.ToString("0.00", Inv) - formatowanie liczb w liniach logu
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        Time("K double.ToString(\"0.00\", Inv) + StringBuilder.Append", 20000, 50, r => { var sb = new System.Text.StringBuilder(800); for (int i = 0; i < 50; i++) sb.Append((i * 1.37 + r).ToString("0.00", inv)).Append(' '); return sb.Length; });
    }
}
