using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

// Kopia rdzenia naprawy z CrashScribe/src/UnwindRepair.cs (ten sam kod pisania do pamieci) - do testu w uprzezy.
public static class UnwindRepairCore
{
    public enum R { Repaired, AlreadyOk, SkipNotJmp, SkipAlign, SkipHeader, SkipNoOverlap, SkipPattern, SkipAlloc, SkipRace, SkipError }

    [DllImport("kernel32")] static extern IntPtr VirtualAlloc(IntPtr addr, UIntPtr size, uint type, uint prot);
        [DllImport("kernel32")] static extern bool VirtualProtect(IntPtr addr, UIntPtr size, uint prot, out uint old);
        [DllImport("kernel32")] static extern UIntPtr VirtualQuery(IntPtr addr, out MEMORY_BASIC_INFORMATION buf, UIntPtr len);
        [StructLayout(LayoutKind.Sequential)]
        struct MEMORY_BASIC_INFORMATION { public IntPtr BaseAddress, AllocationBase; public uint AllocationProtect; public IntPtr RegionSize; public uint State, Protect, Type; }
        const uint MEM_COMMIT = 0x1000, MEM_RESERVE = 0x2000, PAGE_EXECUTE_READWRITE = 0x40;
        const uint STATE_COMMIT = 0x1000, PAGE_GUARD = 0x100, PAGE_NOACCESS = 0x01;
        const byte OrigUnwindByte = 0x19;   // wersja 1 + EHANDLER|UHANDLER - jaki maja wszystkie te metody (dowod: watek glowny gry 16:09 i niezalatana metoda w uprzezy)

        // czy [p, p+len) lezy w jednej zatwierdzonej, czytelnej stronie (zeby nie czytac spod niezmapowanego adresu = AV, ktorego .NET 4 nie zlapie)
        private static bool Readable(IntPtr p, long len)
        {
            try
            {
                MEMORY_BASIC_INFORMATION mbi;
                if (VirtualQuery(p, out mbi, (UIntPtr)(uint)Marshal.SizeOf(typeof(MEMORY_BASIC_INFORMATION))) == UIntPtr.Zero) return false;
                if (mbi.State != STATE_COMMIT) return false;
                if ((mbi.Protect & (PAGE_GUARD | PAGE_NOACCESS)) != 0 || mbi.Protect == 0) return false;
                long end = (long)mbi.BaseAddress + (long)mbi.RegionSize;
                return (long)p + len <= end;
            }
            catch { return false; }
        }

    public static unsafe R Repair(IntPtr start, IntPtr mdescValue, out string why, out IntPtr resolved)
        {
            why = null;
            resolved = start;
            try
            {
                // GetFunctionPointer moze oddac prekod (E9 do wlasciwego kodu); podazamy za jednym skokiem i dopiero tam liczymy na naglowek metody
                if (!Readable(start, 12)) { why = "start nieczytelny"; return R.SkipError; }
                if (*(byte*)start == 0xE9 && !(Readable(start - 8, 8) && *(IntPtr*)(start - 8) != IntPtr.Zero && Readable(*(IntPtr*)(start - 8) + 0x18, 8) && *(IntPtr*)(*(IntPtr*)(start - 8) + 0x18) == mdescValue))
                {
                    long follow = (long)start + 5 + *(int*)(start + 1);
                    if (Readable((IntPtr)follow, 12)) start = (IntPtr)follow;
                }

                resolved = start;
                byte* p = (byte*)start;
                if (p[0] != 0xE9) { why = "nie E9"; return R.SkipNotJmp; }
                if (((long)start & 7) != 0) { why = "start nie wyrownany do 8"; return R.SkipAlign; }

                if (!Readable(start - 8, 8)) { why = "brak naglowka kodu"; return R.SkipHeader; }
                IntPtr hdr = *(IntPtr*)(start - 8);
                if (hdr == IntPtr.Zero || !Readable(hdr + 0x18, 0x10)) { why = "brak naglowka kodu"; return R.SkipHeader; }
                byte* h = (byte*)hdr;
                IntPtr phdrMDesc = *(IntPtr*)(h + 0x18);
                if (phdrMDesc != mdescValue) { why = "naglowek niezgodny z uchwytem"; return R.SkipHeader; }
                uint nUnwind = *(uint*)(h + 0x20);
                if (nUnwind < 1 || nUnwind > 64) { why = "zle nUnwindInfos"; return R.SkipHeader; }
                uint begin = *(uint*)(h + 0x24);
                uint unwind = *(uint*)(h + 0x24 + 8);
                long off = (long)unwind - (long)begin;
                if (off != 4) { why = "opis stosu nie na +4"; return R.SkipNoOverlap; }

                if (p[5] != 0 || p[6] != 0 || p[7] != 0) { why = "bajty za nakladka nie 00"; return R.SkipPattern; }
                uint personality = *(uint*)(start + 8);
                if (personality == 0) { why = "brak adresu obslugi"; return R.SkipPattern; }

                int oldRel = *(int*)(start + 1);
                if (((oldRel >> 24) & 0xFF) == OrigUnwindByte) return R.AlreadyOk;   // juz naprawione (nasz skok), nic nie alokuj
                long target = (long)start + 5 + oldRel;

                long b = (long)start + 5;
                long lo = (b + 0x19000000L + 0xFFFFL) & ~0xFFFFL;
                long hi = b + 0x19FFFFFFL;
                IntPtr tramp = IntPtr.Zero;
                for (long a = lo; a + 16 <= hi; a += 0x10000)
                {
                    var got = VirtualAlloc((IntPtr)a, (UIntPtr)16, MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE);
                    if (got != IntPtr.Zero) { tramp = got; break; }
                }
                if (tramp == IntPtr.Zero) { why = "brak miejsca na trampoline"; return R.SkipAlloc; }

                long newRel = (long)tramp - b;
                if (((newRel >> 24) & 0xFF) != OrigUnwindByte || newRel < int.MinValue || newRel > int.MaxValue)
                { why = "zly wysoki bajt przesuniecia"; return R.SkipAlloc; }

                byte* t = (byte*)tramp;
                t[0] = 0xFF; t[1] = 0x25; t[2] = 0; t[3] = 0; t[4] = 0; t[5] = 0;
                *(long*)(tramp + 6) = target;

                long oldWord = *(long*)start;
                long newWord = (oldWord & unchecked((long)0xFFFFFF0000000000UL)) | 0xE9L | (((long)(uint)newRel) << 8);
                uint oldProt;
                if (!VirtualProtect(start, (UIntPtr)8, PAGE_EXECUTE_READWRITE, out oldProt)) { why = "VirtualProtect"; return R.SkipError; }
                long seen = Interlocked.CompareExchange(ref *(long*)start, newWord, oldWord);
                VirtualProtect(start, (UIntPtr)8, oldProt, out oldProt);
                if (seen != oldWord) { why = "bajty zmienily sie w trakcie"; return R.SkipRace; }
                return R.Repaired;
            }
            catch (Exception e) { why = e.GetType().Name; return R.SkipError; }
        }
}
