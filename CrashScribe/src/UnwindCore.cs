using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace CrashScribe
{
    /// <summary>
    /// UW: rdzen naprawy opisu stosu - czytanie naglowka kodu JIT, rozpoznanie skoku latki, naprawa skoku E9 przez trampoline.
    /// BEZ zaleznosci od gry i logu: ten sam plik kompiluje uprzaz testowa (tools/uw-uprzaz), wiec test sprawdza dokladnie ten kod.
    /// Opis zjawiska i zasad bezpieczenstwa: UnwindRepair.cs i docs/noc-1920/zawieszenie-CHANGELOG.md.
    /// </summary>
    internal static class UnwindCore
    {
        [DllImport("kernel32")] static extern IntPtr VirtualAlloc(IntPtr addr, UIntPtr size, uint type, uint prot);
        [DllImport("kernel32")] static extern bool VirtualProtect(IntPtr addr, UIntPtr size, uint prot, out uint old);
        [DllImport("kernel32")] static extern UIntPtr VirtualQuery(IntPtr addr, out MEMORY_BASIC_INFORMATION buf, UIntPtr len);
        [DllImport("kernel32")] static extern bool FlushInstructionCache(IntPtr process, IntPtr addr, UIntPtr size);
        [DllImport("kernel32")] static extern IntPtr GetCurrentProcess();
        [StructLayout(LayoutKind.Sequential)]
        struct MEMORY_BASIC_INFORMATION { public IntPtr BaseAddress, AllocationBase; public uint AllocationProtect; public IntPtr RegionSize; public uint State, Protect, Type; }
        const uint MEM_COMMIT = 0x1000, MEM_RESERVE = 0x2000, PAGE_EXECUTE_READWRITE = 0x40;
        const uint STATE_COMMIT = 0x1000, PAGE_GUARD = 0x100, PAGE_NOACCESS = 0x01;
        static readonly UIntPtr MbiSize = (UIntPtr)(uint)Marshal.SizeOf(typeof(MEMORY_BASIC_INFORMATION));

        internal const byte OrigUnwindByte = 0x19;   // wersja 1 + EHANDLER|UHANDLER - jaki maja te metody (dowod: watek glowny gry 16:09 i niezalatana metoda w uprzezy)
        internal const int SafeOff = 16;             // opis stosu na +16 albo dalej: zaden skok MonoMod (5/6/14 B, stary 12 B) go nie nakryje
        const int MaxFails = 3;                      // potkniecia per metoda (blad przejsciowy) - potem czekamy, az bajty sie zmienia
        const int BlockSize = 4096, SlotSize = 16;

        internal enum R { NoOverlap, Repaired, AlreadyOk, SkipKind, SkipPattern, SkipAlloc, SkipRace, SkipError, SkipKnown }

        /// <summary>Mala zalatana metoda (opis stosu blizej niz +16) - sprawdzana przy kazdym skanie i przez lekka straz.</summary>
        internal sealed class Small
        {
            public string Name; public IntPtr Start; public int Off; public int NUnw;
            public int Fails;        // potkniecia przejsciowe (VirtualAlloc / wyscig / wyjatek)
            public long Sig;         // pierwsze 8 B kodu przy ostatnim pominieciu, ktorego nie powtarzamy (0 = brak)
            public string Why;       // powod tamtego pominiecia
        }

        private static readonly List<long> _blocks = new List<long>();
        private static readonly List<int> _blockUsed = new List<int>();

        // czy [p, p+len) lezy w jednej zatwierdzonej, czytelnej stronie (zeby nie czytac spod niezmapowanego adresu = AV, ktorego .NET 4 nie zlapie)
        internal static bool Readable(IntPtr p, long len)
        {
            try
            {
                MEMORY_BASIC_INFORMATION mbi;
                if (VirtualQuery(p, out mbi, MbiSize) == UIntPtr.Zero) return false;
                if (mbi.State != STATE_COMMIT) return false;
                if ((mbi.Protect & (PAGE_GUARD | PAGE_NOACCESS)) != 0 || mbi.Protect == 0) return false;
                long end = (long)mbi.BaseAddress + (long)mbi.RegionSize;
                return (long)p + len <= end;
            }
            catch { return false; }
        }

        /// <summary>Czy metoda ma kod JIT 1-4 B (5-bajtowy skok Harmony nakrylby jej opis stosu). 1 = tak, 0 = nie, -1 = nie umiem sprawdzic.
        /// Kompiluje metode (PrepareMethod) - tak jak i tak zrobilby to MonoMod przy zakladaniu latki (PlatformTriple.Compile).</summary>
        internal static int NativeTiny(MethodBase m)
        {
            try
            {
                RuntimeHelpers.PrepareMethod(m.MethodHandle);
                IntPtr start; int off, n;
                if (!Locate(m.MethodHandle.GetFunctionPointer(), m.MethodHandle.Value, out start, out off, out n)) return -1;
                return off <= 4 ? 1 : 0;
            }
            catch { return -1; }
        }

        /// <summary>Z punktu wejscia do poczatku kodu JIT z naglowkiem zgodnym z uchwytem metody. Podaza za co najwyzej dwoma skokami
        /// prekodu (E9 rel32 albo 48 B8 adres FF E0 - wzorce, za ktorymi idzie i MonoMod). Zawsze weryfikacja phdrMDesc == mdesc.</summary>
        internal static unsafe bool Locate(IntPtr entry, IntPtr mdesc, out IntPtr start, out int off, out int n)
        {
            start = entry; off = -1; n = 0;
            for (int hop = 0; hop < 3; hop++)
            {
                if (start == IntPtr.Zero) return false;
                if (Header(start, mdesc, out off, out n)) return true;
                if (!Readable(start, 12)) return false;
                byte* p = (byte*)start;
                if (p[0] == 0xE9) start = (IntPtr)((long)start + 5 + *(int*)(p + 1));
                else if (p[0] == 0x48 && p[1] == 0xB8 && p[10] == 0xFF && p[11] == 0xE0) start = *(IntPtr*)(p + 2);
                // thunk metod typow MarshalByRefObject (np. pochodne Stream): test rcx,rcx; je; mov rax,[rcx]; mov r10,MT; cmp rax,r10; je; mov rax,adres
                // (pierwszy wzorzec MonoMod x86_64Arch.KnownMethodThunks; adres na +25)
                else if (Readable(start, 33) && p[0] == 0x48 && p[1] == 0x85 && p[2] == 0xC9 && p[3] == 0x74 && p[5] == 0x48 && p[6] == 0x8B && p[7] == 0x01
                         && p[8] == 0x49 && p[18] == 0x49 && p[19] == 0x3B && p[20] == 0xC2 && p[21] == 0x74 && p[23] == 0x48 && p[24] == 0xB8)
                    start = *(IntPtr*)(p + 25);
                else return false;
            }
            return false;
        }

        /// <summary>RealCodeHeader spod start-8: +0x18 phdrMDesc, +0x20 nUnwindInfos, +0x24 RUNTIME_FUNCTION[] (Begin, End, UnwindData - RVA).
        /// off = najblizsze nieujemne przesuniecie opisu stosu od startu metody (po wszystkich RUNTIME_FUNCTION).</summary>
        internal static unsafe bool Header(IntPtr start, IntPtr mdesc, out int off, out int n)
        {
            off = -1; n = 0;
            if (!Readable(start - 8, 8)) return false;
            IntPtr hdr = *(IntPtr*)(start - 8);
            if (hdr == IntPtr.Zero || !Readable(hdr + 0x18, 0x0C)) return false;
            if (*(IntPtr*)(hdr + 0x18) != mdesc) return false;
            uint cnt = *(uint*)(hdr + 0x20);
            if (cnt < 1 || cnt > 64) return false;
            if (!Readable(hdr + 0x24, 12L * cnt)) return false;
            uint begin = *(uint*)(hdr + 0x24), end = *(uint*)(hdr + 0x28);
            if (end <= begin) return false;
            long min = long.MaxValue;
            for (int i = 0; i < (int)cnt; i++)
            {
                long o = (long)*(uint*)(hdr + 0x24 + 12 * i + 8) - begin;
                if (o >= 0 && o < min) min = o;
            }
            off = min > int.MaxValue ? int.MaxValue : (int)min;
            n = (int)cnt;
            return true;
        }

        /// <summary>Dlugosc skoku na poczatku kodu (wzorce MonoMod.Core 1.3.3 x86_64: E9 rel32 = 5, FF 25 rel32 = 6, FF 25 0 + adres = 14;
        /// stary 48 B8 adres FF E0 = 12); 0 = brak rozpoznanego skoku. p musi miec 12 czytelnych bajtow.</summary>
        internal static unsafe int DetourLen(byte* p)
        {
            if (p[0] == 0xE9) return 5;
            if (p[0] == 0xFF && p[1] == 0x25) return *(int*)(p + 2) == 0 ? 14 : 6;
            if (p[0] == 0x48 && p[1] == 0xB8 && p[10] == 0xFF && p[11] == 0xE0) return 12;
            return 0;
        }

        /// <summary>Lekkie sprawdzenie bez alokacji: true = trzeba wolac Check (skok nad opisem stosu zmienil sie od ostatniej oceny).</summary>
        internal static unsafe bool NeedsCheck(Small s)
        {
            if (!Readable(s.Start, 16)) return false;
            byte* p = (byte*)s.Start;
            if (DetourLen(p) <= s.Off) return false;                                  // brak nakladki
            if (p[0] == 0xE9 && s.Off == 4 && p[4] == OrigUnwindByte) return false;     // nasz (albo szczesliwy) skok - opis stosu dobry
            if (s.Sig != 0 && s.Sig == *(long*)s.Start) return false;                  // juz znane pominiecie, bajty bez zmian
            return true;
        }

        /// <summary>Ocena biezacego stanu malej metody i - jesli trzeba i wolno - naprawa. Bez alokacji poza VirtualAlloc nowego bloku trampolin.</summary>
        internal static unsafe R Check(Small s, out string why)
        {
            why = null;
            try
            {
                if (!Readable(s.Start, 16)) { why = "start nieczytelny"; return R.SkipError; }
                byte* p = (byte*)s.Start;
                int len = DetourLen(p);
                if (len <= s.Off) return R.NoOverlap;
                if (p[0] == 0xE9 && s.Off == 4 && p[4] == OrigUnwindByte) return R.AlreadyOk;
                if (s.Sig != 0 && s.Sig == *(long*)s.Start) { why = s.Why; return R.SkipKnown; }

                R r;
                if (p[0] != 0xE9)
                {
                    why = p[0] == 0xFF ? (len == 14 ? "skok FF25+adres 14 B nad opisem stosu" : "skok FF25 6 B nad opisem stosu") : "skok 48B8 12 B nad opisem stosu";
                    r = R.SkipKind;
                }
                else r = RepairE9(s.Start, s.Off, s.NUnw, out why);

                if (r == R.Repaired || r == R.AlreadyOk) { s.Sig = 0; s.Why = null; return r; }
                bool permanent = r == R.SkipKind || r == R.SkipPattern;
                if (!permanent) s.Fails++;
                if (permanent || s.Fails >= MaxFails) { s.Sig = *(long*)s.Start; s.Why = why + (permanent ? "" : " (po " + s.Fails + " probach)"); }
                return r;
            }
            catch (Exception e) { why = e.GetType().Name; s.Fails++; return R.SkipError; }
        }

        /// <summary>Naprawa skoku E9 przy opisie stosu na +4: nowy skok przez trampoline (jmp [rip] + adres starego celu) polozona tak,
        /// by wysoki bajt przesuniecia (+4) = 0x19. Zapis atomowy 8 B ze starym wzorcem jako oczekiwanym.</summary>
        internal static unsafe R RepairE9(IntPtr start, int off, int nUnw, out string why)
        {
            why = null;
            byte* p = (byte*)start;
            if (p[0] != 0xE9) { why = "nie E9"; return R.SkipKind; }
            if (off != 4) { why = "E9, opis stosu na +" + off; return R.SkipPattern; }
            if (nUnw != 1) { why = "E9, kilka RUNTIME_FUNCTION"; return R.SkipPattern; }
            if (((long)start & 7) != 0) { why = "start nie wyrownany do 8"; return R.SkipPattern; }
            if (p[5] != 0 || p[6] != 0 || p[7] != 0) { why = "bajty za nakladka nie 00"; return R.SkipPattern; }
            if (*(uint*)(start + 8) == 0) { why = "brak adresu obslugi"; return R.SkipPattern; }

            int oldRel = *(int*)(start + 1);
            if (((oldRel >> 24) & 0xFF) == OrigUnwindByte) return R.AlreadyOk;
            long target = (long)start + 5 + oldRel;
            long b = (long)start + 5;

            IntPtr tramp = AllocSlot(b);
            if (tramp == IntPtr.Zero) { why = "brak miejsca na trampoline"; return R.SkipAlloc; }
            long newRel = (long)tramp - b;
            if (((newRel >> 24) & 0xFF) != OrigUnwindByte || newRel < int.MinValue || newRel > int.MaxValue)
            { why = "zly wysoki bajt przesuniecia"; return R.SkipAlloc; }

            byte* t = (byte*)tramp;
            t[0] = 0xFF; t[1] = 0x25; t[2] = 0; t[3] = 0; t[4] = 0; t[5] = 0;     // jmp [rip+0]
            *(long*)(tramp + 6) = target;                                          // ... dokladnie tam, gdzie skakal stary skok

            long oldWord = *(long*)start;
            if ((oldWord & 0xFF) != 0xE9 || (int)(oldWord >> 8) != oldRel) { why = "bajty zmienily sie w trakcie"; return R.SkipRace; }
            long newWord = (oldWord & unchecked((long)0xFFFFFF0000000000UL)) | 0xE9L | (((long)(uint)newRel) << 8);
            uint oldProt;
            if (!VirtualProtect(start, (UIntPtr)8, PAGE_EXECUTE_READWRITE, out oldProt)) { why = "VirtualProtect"; return R.SkipError; }
            long seen = Interlocked.CompareExchange(ref *(long*)start, newWord, oldWord);
            VirtualProtect(start, (UIntPtr)8, oldProt, out oldProt);
            try { FlushInstructionCache(GetCurrentProcess(), start, (UIntPtr)8); } catch { }
            if (seen != oldWord) { why = "bajty zmienily sie w trakcie"; return R.SkipRace; }
            return R.Repaired;
        }

        /// <summary>16-bajtowe miejsce na trampoline w oknie [b+0x19000000, b+0x19FFFFFF] (wtedy wysoki bajt przesuniecia skoku = 0x19).
        /// Bloki po 4 KB dzielone miedzy metodami lezacymi blisko siebie (zamiast jednej rezerwacji 64 KB na metode). Nie zwalniamy -
        /// inny watek moze akurat przez trampoline przechodzic; stare miejsce po ponownej naprawie to 16 B.</summary>
        private static IntPtr AllocSlot(long b)
        {
            const long Lo = 0x19000000L, Hi = 0x19FFFFFFL;
            for (int i = 0; i < _blocks.Count; i++)
            {
                if (_blockUsed[i] + SlotSize > BlockSize) continue;
                long a = _blocks[i] + _blockUsed[i];
                long rel = a - b;
                if (rel >= Lo && rel <= Hi) { _blockUsed[i] += SlotSize; return (IntPtr)a; }
            }
            long lo = (b + Lo + 0xFFFFL) & ~0xFFFFL;
            for (long a = lo; a + BlockSize - 1 - b <= Hi; a += 0x10000)
            {
                var got = VirtualAlloc((IntPtr)a, (UIntPtr)BlockSize, MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE);
                if (got == IntPtr.Zero) continue;
                if ((long)got != a) continue;   // (nie powinno sie zdarzyc) - nie ten adres, nie uzywamy
                _blocks.Add(a); _blockUsed.Add(SlotSize);
                return got;
            }
            return IntPtr.Zero;
        }

        /// <summary>Liczba blokow trampolin (do linii logu).</summary>
        internal static int Blocks { get { return _blocks.Count; } }
    }
}
