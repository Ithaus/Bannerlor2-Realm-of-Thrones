using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace CrashScribe
{
    /// <summary>UW: pelny skan naprawy opisu stosu na starcie sesji i raz na dobe (lapie metody zalatane / skompilowane pozniej niz skan w menu).</summary>
    internal sealed class UnwindRepairBehavior : CampaignBehaviorBase
    {
        private int _day = -1;
        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, _ => { try { UnwindRepair.Scan("sesja"); } catch { } });
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, () =>
            {
                try { int d = (int)CampaignTime.Now.ToDays; if (d == _day) return; _day = d; UnwindRepair.Scan("doba"); } catch { }
            });
        }
        public override void SyncData(IDataStore dataStore) { }
    }

    /// <summary>
    /// NAPRAWA OPISU STOSU (UW) - zawieszenie gry 19.10 16:09.
    ///
    /// Co sie stalo (prostymi slowami): mala metoda (np. pusty SyncData, lambda "() => {}") ma po skompilowaniu 1-4 bajty
    /// kodu. Harmony, zakladajac na niej latke, wkleja na jej poczatek 5-bajtowy skok. Tuz za tak krotkim kodem lezy
    /// "opis stosu" (UNWIND_INFO), ktorego Windows uzywa, gdy musi przejsc po stosie wywolan - a jego pierwszy bajt
    /// (oryginalnie 0x19) wypada dokladnie pod tym skokiem i skok go psuje (na 0xFD). Dopoki nikt tam nie zaglada, gra
    /// dziala. Ale gdy odsmiecanie pamieci (GC) zatrzymuje watek stojacy akurat na tym skoku i Windows 11 25H2 probuje
    /// przejsc jego stos, widzi zepsuty opis i rzuca - a odpowiedz na to wymaga nowej pamieci, ktorej nie da sie przydzielic
    /// w trakcie GC. Gra zawisa na zawsze (proces zyje, nic sie nie dzieje).
    ///
    /// Naprawa (sposob C, wybrany po tescie w uprzezy - patrz docs/noc-1920/zawieszenie-CHANGELOG.md): dla kazdej malej
    /// zalatanej metody przepisujemy skok tak, by prowadzil przez wlasna trampoline polozona tak, ze wysoki bajt
    /// przesuniecia skoku (ten, ktory wypada na +4) znow rowna sie oryginalnemu 0x19. Opis stosu jest wtedy poprawny,
    /// a skok dalej trafia tam, gdzie trafial - latka dziala bez zmian.
    ///
    /// BEZPIECZENSTWO: piszemy do pamieci TYLKO gdy caly wzorzec pasuje (E9 na starcie, naglowek kodu zgodny z uchwytem
    /// metody, opis stosu dokladnie na +4, bajty za nakladka 00 00 00 i niezerowy adres procedury obslugi na +8). Przy
    /// jakiejkolwiek niezgodnosci - pomijamy i liczymy w logu (metoda zostaje jak byla, nic jej nie psujemy). Zapis skoku
    /// jest atomowy (InterlockedCompareExchange64 ze starym wzorcem jako oczekiwanym), wiec watek czytajacy instrukcje
    /// widzi albo stary, albo nowy skok - oba poprawne i oba trafiaja w ten sam cel. Zdjecie latki przez Harmony/MonoMod
    /// przywraca zapisana kopie oryginalnych bajtow (bez sprawdzania biezacych), wiec nasza zmiana mu nie przeszkadza.
    /// </summary>
    internal static class UnwindRepair
    {
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

        private enum R { Repaired, AlreadyOk, SkipNotJmp, SkipAlign, SkipHeader, SkipNoOverlap, SkipPattern, SkipAlloc, SkipRace, SkipError }

        // metody, ktore raz naprawilismy - lekkie sprawdzenie co ok. 30 s, czy ktoras latka nie nadpisala naszego skoku
        private static readonly List<KeyValuePair<IntPtr, IntPtr>> _watched = new List<KeyValuePair<IntPtr, IntPtr>>();
        private static int _nextLightMs;
        private static bool _ran;

        /// <summary>Pelny skan: wszystkie zalatane metody; male (kod IL <= progu) sprawdzamy i w razie potrzeby naprawiamy. Wolac w znanych chwilach latkowania.</summary>
        internal static void Scan(string phase)
        {
            if (!Config.UnwindRepair) { if (!_ran) { _ran = true; Scribe.Line("Opis stosu (UW): naprawa WYLACZONA (klucz UnwindRepair=false) - male zalatane metody z zepsutym opisem stosu zostaja."); } return; }
            _ran = true;
            int patched = 0, tiny = 0, overlap = 0, fixedN = 0, already = 0;
            var skip = new Dictionary<string, int>();
            try
            {
                IEnumerable<MethodBase> all;
                try { all = Harmony.GetAllPatchedMethods(); } catch (Exception e) { Scribe.Line("Opis stosu (UW): nie dostalem listy zalatanych metod (" + e.GetType().Name + ")."); return; }
                foreach (var m in all)
                {
                    patched++;
                    int il;
                    try { var body = m.GetMethodBody(); var arr = body != null ? body.GetILAsByteArray() : null; il = arr != null ? arr.Length : int.MaxValue; }
                    catch { continue; }
                    if (il > Config.NetTinyIlBytes) continue;   // tylko male metody moga miec opis stosu pod skokiem
                    tiny++;
                    IntPtr start, mdesc;
                    try { start = m.MethodHandle.GetFunctionPointer(); mdesc = m.MethodHandle.Value; }
                    catch { Bump(skip, "uchwyt metody"); continue; }
                    string why;
                    IntPtr resolved; R r = Repair(start, mdesc, out why, out resolved);
                    if (r == R.Repaired) { fixedN++; overlap++; Remember(resolved, mdesc); }
                    else if (r == R.AlreadyOk) { already++; overlap++; Remember(resolved, mdesc); }
                    else if (r == R.SkipNoOverlap || r == R.SkipNotJmp) { /* nie nachodzi - nic do roboty */ }
                    else { overlap++; Bump(skip, why ?? r.ToString()); }
                }
            }
            catch (Exception e) { Scribe.Line("Opis stosu (UW): blad skanu (" + e.GetType().Name + ")."); return; }

            var sb = new System.Text.StringBuilder();
            sb.Append("Opis stosu (UW, ").Append(phase).Append("): zalatanych metod ").Append(patched)
              .Append(", malych ").Append(tiny).Append(", nachodzacych na opis stosu ").Append(overlap)
              .Append(", naprawionych teraz ").Append(fixedN).Append(", juz dobrych ").Append(already);
            if (skip.Count > 0)
            {
                sb.Append(", pominietych ");
                int tot = 0; foreach (var kv in skip) tot += kv.Value; sb.Append(tot).Append(" (");
                bool first = true; foreach (var kv in skip) { if (!first) sb.Append("; "); sb.Append(kv.Key).Append(' ').Append(kv.Value); first = false; }
                sb.Append(")");
            }
            sb.Append(".");
            Scribe.Line(sb.ToString());
        }

        /// <summary>Lekkie sprawdzenie z OnApplicationTick: raz na ok. 30 s porownanie bajtow znanych metod (bez alokacji w kazdej klatce).
        /// Jesli czyjas latka nadpisala nasz skok (bajt +4 znow zepsuty) - naprawiamy ponownie.</summary>
        internal static void LightTick()
        {
            if (!Config.UnwindRepair) return;
            int now = Environment.TickCount;
            if (_nextLightMs == 0) { _nextLightMs = now + 30000; return; }
            if (unchecked(now - _nextLightMs) < 0) return;
            _nextLightMs = now + 30000;
            int recheck = 0, refixed = 0;
            try
            {
                for (int i = 0; i < _watched.Count; i++)
                {
                    IntPtr start = _watched[i].Key, mdesc = _watched[i].Value;
                    int b;
                    try { b = Marshal.ReadByte(start, 4); } catch { continue; }
                    if (b == OrigUnwindByte) continue;   // nasz skok nietkniety
                    recheck++;
                    string why; IntPtr resolved; if (Repair(start, mdesc, out why, out resolved) == R.Repaired) refixed++;
                }
            }
            catch { }
            if (refixed > 0) Scribe.Line("Opis stosu (UW, straz 30s): nadpisanych latka " + recheck + ", naprawionych ponownie " + refixed + ".");
        }

        private static void Remember(IntPtr start, IntPtr mdesc)
        {
            for (int i = 0; i < _watched.Count; i++) if (_watched[i].Key == start) return;
            _watched.Add(new KeyValuePair<IntPtr, IntPtr>(start, mdesc));
        }

        private static void Bump(Dictionary<string, int> d, string k) { int v; d.TryGetValue(k, out v); d[k] = v + 1; }

        private static unsafe R Repair(IntPtr start, IntPtr mdescValue, out string why, out IntPtr resolved)
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
}
