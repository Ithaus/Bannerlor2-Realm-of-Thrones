using System;
using System.Runtime.CompilerServices;
using System.Threading;
using HarmonyLib;

// Uprzaz UW: czy GC zakleszcza proces, gdy watek glowny stoi na skoku Harmony w metodzie 1-bajtowej.
// Wariant A: latka (postfix) na pustej metodzie wirtualnej (kod JIT = ret).
// Wariant B: latka na metodzie nie-tycia (kilkadziesiat bajtow kodu).
// Wariant C: pusta metoda BEZ latki (tak dziala naprawa CrashScribe: male metody bez sieci).
// Proces co sekunde wypisuje licznik petli i liczbe GC; straznik jest ZEWNETRZNY (straz.py), bo przy
// zakleszczeniu GC stoja wszystkie watki zarzadzane - wewnetrzny straznik nic by nie wypisal.
public class Target
{
    public virtual void Tiny() { }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public virtual int NonTiny(int x)
    {
        int a = x * 7 + 3;
        if (a > 1000) a -= x;
        for (int i = 0; i < 3; i++) a ^= (a << 1) + i;
        return a;
    }
}

public class Derived : Target { }

public static class Patches
{
    public static long Hits;
    public static void Post() { Hits++; }
    public static void PostInt(ref int __result) { Hits++; }
}

public static class Program
{
    static long _loops;
    static volatile bool _stop;
    static object _sink;
    [MethodImpl(MethodImplOptions.NoInlining)] static Target Make(string k) { return k == "x" ? new Derived() : new Target(); }
    // JIT wielu duzych metod z Reflection.Emit (glowna sterta kodu rosnie), zanim powstanie kod Tiny
    static void Fill(int mb)
    {
        var ab = System.Reflection.Emit.AssemblyBuilder.DefineDynamicAssembly(new System.Reflection.AssemblyName("fill"), System.Reflection.Emit.AssemblyBuilderAccess.Run);
        var mod = ab.DefineDynamicModule("fill");
        var tb = mod.DefineType("F", System.Reflection.TypeAttributes.Public);
        int n = mb * 16;   // ok. 64 KB kodu na metode
        for (int i = 0; i < n; i++)
        {
            var mbld = tb.DefineMethod("M" + i, System.Reflection.MethodAttributes.Public | System.Reflection.MethodAttributes.Static, typeof(int), new[] { typeof(int) });
            var il = mbld.GetILGenerator();
            il.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);
            for (int k = 0; k < 8000; k++) { il.Emit(System.Reflection.Emit.OpCodes.Ldc_I4, k * 31 + i); il.Emit(System.Reflection.Emit.OpCodes.Xor); }
            il.Emit(System.Reflection.Emit.OpCodes.Ret);
        }
        var ft = tb.CreateType();
        foreach (var m in ft.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)) RuntimeHelpers.PrepareMethod(m.MethodHandle);
        Console.WriteLine("wypelnienie: " + n + " metod");
    }
    // bajty kodu natywnego od startu metody (tylko odczyt, diagnoza ukladu)
    static string Bytes(System.Reflection.MethodBase m, int n) { var sb = new System.Text.StringBuilder(); IntPtr p = m.MethodHandle.GetFunctionPointer(); for (int i = 0; i < n; i++) sb.Append(System.Runtime.InteropServices.Marshal.ReadByte(p, i).ToString("X2")).Append(' '); return "@" + p.ToString("X") + ": " + sb; }

    // wariant L: rdzen UnwindCore na swiezych metodach (bez wywolania), naprawa, zdjecie i ponowne zalozenie latki, wspolny blok trampolin
    static int Lambda(int x) { return x; }
    [MethodImpl(MethodImplOptions.NoInlining)] public static void StaticTiny() { }
    static Target _lt;
    // cel skoku E9 na starcie metody; jesli prowadzi do naszej trampoliny (FF 25 00000000 + adres) - adres z trampoliny
    static long FinalTarget(IntPtr st)
    {
        long t = st.ToInt64() + 5 + System.Runtime.InteropServices.Marshal.ReadInt32(st, 1);
        if (System.Runtime.InteropServices.Marshal.ReadByte((IntPtr)t) == 0xFF && System.Runtime.InteropServices.Marshal.ReadByte((IntPtr)t, 1) == 0x25 && System.Runtime.InteropServices.Marshal.ReadInt32((IntPtr)t, 2) == 0)
            t = System.Runtime.InteropServices.Marshal.ReadInt64((IntPtr)t, 6);
        return t;
    }
    static string Line(string what, System.Reflection.MethodBase m)
    {
        IntPtr st; int off, n;
        int nt = CrashScribe.UnwindCore.NativeTiny(m);   // najpierw - kompiluje metode
        bool ok = CrashScribe.UnwindCore.Locate(m.MethodHandle.GetFunctionPointer(), m.MethodHandle.Value, out st, out off, out n);
        return what + ": NativeTiny " + nt + ", naglowek " + ok + ", opis stosu +" + off + ", RUNTIME_FUNCTION " + n + " | " + Bytes(m, 12);
    }
    static int LocateTest()
    {
        var tiny = AccessTools.DeclaredMethod(typeof(Target), nameof(Target.Tiny));
        var stat = AccessTools.DeclaredMethod(typeof(Program), nameof(StaticTiny));
        var lam = AccessTools.DeclaredMethod(typeof(Program), nameof(Lambda));
        var non = AccessTools.DeclaredMethod(typeof(Target), nameof(Target.NonTiny));
        Console.WriteLine(Line("Tiny (wirtualna, nie wolana)", tiny));
        Console.WriteLine(Line("StaticTiny (statyczna, nie wolana)", stat));
        Console.WriteLine(Line("Lambda x=>x", lam));
        Console.WriteLine(Line("NonTiny", non));
        var h = new Harmony("uw.L");
        var post = new HarmonyMethod(typeof(Patches), nameof(Patches.Post));
        int bad = 0;
        _lt = Make("");   // wywolanie wirtualne przez pole - JIT nie zinlinuje
        foreach (var m in new System.Reflection.MethodBase[] { tiny, stat })
        {
            h.Patch(m, postfix: post);
            IntPtr st; int off, n; string why;
            CrashScribe.UnwindCore.Locate(m.MethodHandle.GetFunctionPointer(), m.MethodHandle.Value, out st, out off, out n);
            var s = new CrashScribe.UnwindCore.Small { Name = m.Name, Start = st, Off = off, NUnw = n };
            Console.WriteLine(m.Name + " z latka: " + Bytes(m, 12) + " | NeedsCheck " + CrashScribe.UnwindCore.NeedsCheck(s));
            long before = FinalTarget(st);
            var r1 = CrashScribe.UnwindCore.Check(s, out why);
            long after = FinalTarget(st);
            Console.WriteLine(m.Name + " cel skoku przed " + before.ToString("X") + ", po naprawie (przez trampoline) " + after.ToString("X") + (before == after ? " - TEN SAM" : " - ROZNY!"));
            if (before != after) bad++;
            var r2 = CrashScribe.UnwindCore.Check(s, out why);
            Console.WriteLine(m.Name + " naprawa " + r1 + " (" + (why ?? "ok") + "), drugi raz " + r2 + ", NeedsCheck " + CrashScribe.UnwindCore.NeedsCheck(s) + " | " + Bytes(m, 12));
            if (r1 != CrashScribe.UnwindCore.R.Repaired || r2 != CrashScribe.UnwindCore.R.AlreadyOk) bad++;
            long hits = Patches.Hits;
            if (m == tiny) _lt.Tiny(); else StaticTiny();
            Console.WriteLine(m.Name + " wywolanie po naprawie: postfix trafien +" + (Patches.Hits - hits));
            if (Patches.Hits - hits != 1) bad++;
            // zdjecie latki (MonoMod przywraca kopie oryginalnych bajtow) i ponowne zalozenie
            h.Unpatch(m, HarmonyPatchType.All, h.Id);
            Console.WriteLine(m.Name + " po zdjeciu latki: " + Bytes(m, 12) + " | NeedsCheck " + CrashScribe.UnwindCore.NeedsCheck(s) + ", Check " + CrashScribe.UnwindCore.Check(s, out why));
            h.Patch(m, postfix: post);
            Console.WriteLine(m.Name + " latka ponownie: " + Bytes(m, 12) + " | NeedsCheck " + CrashScribe.UnwindCore.NeedsCheck(s));
            var r3 = CrashScribe.UnwindCore.Check(s, out why);
            hits = Patches.Hits;
            if (m == tiny) _lt.Tiny(); else StaticTiny();
            Console.WriteLine(m.Name + " naprawa po ponownej latce " + r3 + " (" + (why ?? "ok") + "), postfix trafien +" + (Patches.Hits - hits) + " | " + Bytes(m, 12));
            if (r3 != CrashScribe.UnwindCore.R.Repaired || Patches.Hits - hits != 1) bad++;
        }
        h.UnpatchAll(h.Id);
        Console.WriteLine("UnpatchAll bez wyjatku; Tiny " + Bytes(tiny, 12) + "; blokow trampolin " + CrashScribe.UnwindCore.Blocks);
        Console.WriteLine(bad == 0 ? "L: WSZYSTKO ZGODNE" : "L: NIEZGODNOSCI " + bad);
        return bad == 0 ? 0 : 4;
    }

    public static int Main(string[] args)
    {
        string v = args.Length > 0 ? args[0] : "A";
        if (v == "L") return LocateTest();
        int seconds = args.Length > 1 ? int.Parse(args[1]) : 300;
        int allocThreads = args.Length > 2 ? int.Parse(args[2]) : 3;
        Target t = Make(args.Length > 3 ? args[3] : "");
        int fillMb = args.Length > 4 ? int.Parse(args[4]) : 0;
        if (fillMb > 0)
        {
            // najpierw jedna latka na metode pomocnicza - sterta kodu DMD powstaje NIZEJ, potem wypelnienie glownej sterty
            new Harmony("uw.pre").Patch(AccessTools.DeclaredMethod(typeof(Target), nameof(Target.NonTiny)), postfix: new HarmonyMethod(typeof(Patches), nameof(Patches.PostInt)));
            new Harmony("uw.pre").UnpatchAll("uw.pre");
            Fill(fillMb);
        }
        Console.WriteLine("Tiny przed latka " + Bytes(AccessTools.DeclaredMethod(typeof(Target), nameof(Target.Tiny)), 16));
        var h = new Harmony("uw.harness");
        if (v == "A" || v == "D")
            h.Patch(AccessTools.DeclaredMethod(typeof(Target), nameof(Target.Tiny)), postfix: new HarmonyMethod(typeof(Patches), nameof(Patches.Post)));
        else if (v == "B")
            h.Patch(AccessTools.DeclaredMethod(typeof(Target), nameof(Target.NonTiny)), postfix: new HarmonyMethod(typeof(Patches), nameof(Patches.PostInt)));
        // wywolanie przed petla - JIT metody docelowej
        t.Tiny(); t.NonTiny(1);
        var tm = AccessTools.DeclaredMethod(typeof(Target), nameof(Target.Tiny)); IntPtr tp = tm.MethodHandle.GetFunctionPointer();
        if (System.Runtime.InteropServices.Marshal.ReadByte(tp) == 0xE9) { long dst = tp.ToInt64() + 5 + System.Runtime.InteropServices.Marshal.ReadInt32(tp, 1); Console.WriteLine("skok Tiny -> " + dst.ToString("X") + " (roznica " + (dst - tp.ToInt64()) / 1048576.0 + " MB)"); }
        Console.WriteLine("Tiny " + Bytes(AccessTools.DeclaredMethod(typeof(Target), nameof(Target.Tiny)), 24));
        Console.WriteLine("NonTiny " + Bytes(AccessTools.DeclaredMethod(typeof(Target), nameof(Target.NonTiny)), 24));
        if (v == "D")
        {
            var tmd = AccessTools.DeclaredMethod(typeof(Target), nameof(Target.Tiny));
            RuntimeHelpers.PrepareMethod(tmd.MethodHandle);
            IntPtr st; int off, n; string why;
            bool found = CrashScribe.UnwindCore.Locate(tmd.MethodHandle.GetFunctionPointer(), tmd.MethodHandle.Value, out st, out off, out n);
            var sm = new CrashScribe.UnwindCore.Small { Name = "Tiny", Start = st, Off = off, NUnw = n };
            var r = found ? CrashScribe.UnwindCore.Check(sm, out why) : CrashScribe.UnwindCore.R.SkipError;
            var r2 = found ? CrashScribe.UnwindCore.Check(sm, out why) : CrashScribe.UnwindCore.R.SkipError;
            Console.WriteLine("NAPRAWA naglowek " + found + " off " + off + " n " + n + " -> " + r + ", drugi raz " + r2 + " | Tiny po naprawie " + Bytes(tmd, 12));
        }
        Console.WriteLine("START wariant " + v + " czas " + seconds + " s, watki alokujace " + allocThreads + ", Environment.OSVersion " + Environment.OSVersion + ", CLR " + Environment.Version);
        Console.Out.Flush();

        for (int i = 0; i < allocThreads; i++)
        {
            var th = new Thread(() =>
            {
                var rnd = new Random(Thread.CurrentThread.ManagedThreadId);
                while (!_stop)
                {
                    var arr = new object[64];
                    for (int k = 0; k < arr.Length; k++) arr[k] = new byte[rnd.Next(16, 512)];
                    _sink = arr;
                }
            }) { IsBackground = true };
            th.Start();
        }
        var rep = new Thread(() =>
        {
            var start = DateTime.UtcNow;
            while (!_stop)
            {
                Thread.Sleep(1000);
                Console.WriteLine("T " + (int)(DateTime.UtcNow - start).TotalSeconds + " petli " + Interlocked.Read(ref _loops) + " gc0 " + GC.CollectionCount(0) + " gc1 " + GC.CollectionCount(1) + " gc2 " + GC.CollectionCount(2) + " hits " + Patches.Hits);
                Console.Out.Flush();
                if ((DateTime.UtcNow - start).TotalSeconds >= seconds) { _stop = true; }
            }
        }) { IsBackground = true };
        rep.Start();

        int acc = 0;
        while (!_stop)
        {
            for (int i = 0; i < 1000; i++)
            {
                if (v == "B") acc += t.NonTiny(i); else t.Tiny();
            }
            Interlocked.Add(ref _loops, 1000);
        }
        Console.WriteLine("KONIEC wariant " + v + " bez zawieszenia, petli " + Interlocked.Read(ref _loops) + " gc0 " + GC.CollectionCount(0) + " acc " + acc);
        return 0;
    }
}
