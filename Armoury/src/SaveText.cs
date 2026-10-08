using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace Armoury
{
    /// <summary>
    /// 161: ZAPIS BEZ DLUGICH NAPISOW (08.10). Gra zapisuje dlugosc kazdego napisu w pliku zapisu na 2 bajtach
    /// ze znakiem (SaveContext.SaveStringTo: WriteShort((short)rozmiar)). Napis dluzszy niz 32767 B dostaje ucieta
    /// dlugosc i przy wczytaniu ArchiveDeserializer.LoadFrom czyta zla liczbe bajtow - wszystko dalej jest
    /// przesuniete, zapis sie nie wczytuje ("Source array was not long enough" / OverflowException).
    /// Nasze dane z SyncData sa jednym napisem na klucz: komplety rekrutow mialy w save034-039 150-220 KB,
    /// po roku 2.9 MB, zuzycie sprzetu AI 1.8 MB, wyrzutkowie 150 KB.
    ///
    /// Dwie czesci:
    ///  1. Sync: napis dluzszy niz Part znakow idzie do zapisu jako lista kawalkow (klucz + "_parts"), sam klucz
    ///     dostaje "". Kazdy kawalek to osobny napis w pliku, ponizej limitu. Krotkie napisy - jak dotad.
    ///  2. Ratunek przy wczytaniu (transpiler na LoadFrom): dla wpisu-napisu (rozszerzenie Txt) dlugosc z 4 bajtow
    ///     na poczatku napisu (gra pisze tam pelna dlugosc) zamiast ucietej - tylko gdy zgadza sie z ucieta na
    ///     16 bitach. Wczytuje stare zapisy z dlugimi napisami; dobre zapisy czytaja sie bajt w bajt jak dotad.
    /// </summary>
    internal static class SaveText
    {
        // znakow na kawalek: UTF-8 to najwyzej 3 B na znak (para zastepcza 4 B na 2 znaki) -> 8000 znakow < 32763 B
        private const int Part = 8000;
        private const string PartsSuffix = "_parts";

        /// <summary>SyncData dla napisu dowolnej dlugosci.</summary>
        internal static void Sync(IDataStore ds, string key, ref string value)
        {
            if (ds.IsSaving)
            {
                string v = value ?? "";
                if (v.Length <= Part)
                {
                    ds.SyncData(key, ref value);
                    return;
                }
                var parts = Split(v);
                string head = "";
                ds.SyncData(key, ref head);
                ds.SyncData(key + PartsSuffix, ref parts);
                return;
            }
            if (ds.IsLoading)
            {
                string head = null;
                ds.SyncData(key, ref head);
                List<string> parts = null;
                ds.SyncData(key + PartsSuffix, ref parts);
                value = parts != null && parts.Count > 0 ? string.Concat(parts) : head;
            }
        }

        private static List<string> Split(string v)
        {
            var l = new List<string>(v.Length / Part + 1);
            int i = 0;
            while (i < v.Length)
            {
                int n = Math.Min(Part, v.Length - i);
                // nie tniemy pary zastepczej (znak spoza BMP) - polowka zakodowalaby sie jako "?"
                if (n < v.Length - i && char.IsHighSurrogate(v[i + n - 1])) n--;
                l.Add(v.Substring(i, n));
                i += n;
            }
            return l;
        }

        // ------------------------------------------------------------ ratunek przy wczytaniu

        private const int Txt = 10;   // SaveEntryExtension.Txt - wpisy archiwum napisow
        private static bool _installed;
        internal static int Rescued;
        internal static long Largest;
        private static AccessTools.FieldRef<TaleWorlds.Library.BinaryReader, int> _cursor;

        /// <summary>Z OnSubModuleLoad: przed pierwszym wczytaniem zapisu.</summary>
        internal static void InstallRescue(Harmony h)
        {
            if (_installed) return;
            _installed = true;
            try
            {
                _cursor = AccessTools.FieldRefAccess<TaleWorlds.Library.BinaryReader, int>("_cursor");
                var t = AccessTools.TypeByName("TaleWorlds.SaveSystem.ArchiveDeserializer");
                var m = t != null ? AccessTools.Method(t, "LoadFrom", new[] { typeof(byte[]) }) : null;
                if (m == null) { Log.Info("Zapis: BRAK ArchiveDeserializer.LoadFrom - ratunek dlugich napisow nie stoi."); return; }
                h.Patch(m, transpiler: new HarmonyMethod(typeof(SaveText), nameof(Transpiler)));
                Log.Info("Zapis: ratunek dlugich napisow stoi (napis > 32767 B nie psuje wczytania; nowe zapisy dziela dlugie dane na kawalki).");
            }
            catch (Exception e) { Log.Error("SaveText.InstallRescue", e); }
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var list = new List<CodeInstruction>(instructions);
            var readShort = AccessTools.Method(typeof(TaleWorlds.Library.BinaryReader), "ReadShort");
            var readBytes = AccessTools.Method(typeof(TaleWorlds.Library.BinaryReader), "ReadBytes", new[] { typeof(int) });
            var fix = AccessTools.Method(typeof(SaveText), nameof(FixLength));
            int done = 0;
            for (int i = 0; i < list.Count; i++)
            {
                if (!(list[i].opcode == OpCodes.Callvirt || list[i].opcode == OpCodes.Call) || !Equals(list[i].operand, readBytes)) continue;
                // wstecz: stloc ext (ReadByte); ldloc reader; callvirt ReadShort; stloc len; ldloc reader; ldloc len; [tu]
                int rs = -1;
                for (int k = i - 1; k >= 0 && k >= i - 8; k--)
                    if ((list[k].opcode == OpCodes.Callvirt || list[k].opcode == OpCodes.Call) && Equals(list[k].operand, readShort)) { rs = k; break; }
                if (rs < 2) continue;
                var loadReader = list[rs - 1];
                var storeExt = list[rs - 2];
                if (!storeExt.IsStloc()) continue;
                var ins = new List<CodeInstruction>
                {
                    new CodeInstruction(loadReader.opcode, loadReader.operand),
                    LoadFromStore(storeExt),
                    new CodeInstruction(OpCodes.Call, fix)
                };
                list.InsertRange(i, ins);
                i += ins.Count;
                done++;
            }
            if (done != 1) Log.Info("Zapis: ratunek dlugich napisow - transpiler podmienil " + done + " miejsc (oczekiwane 1).");
            return list;
        }

        private static CodeInstruction LoadFromStore(CodeInstruction st)
        {
            if (st.opcode == OpCodes.Stloc_0) return new CodeInstruction(OpCodes.Ldloc_0);
            if (st.opcode == OpCodes.Stloc_1) return new CodeInstruction(OpCodes.Ldloc_1);
            if (st.opcode == OpCodes.Stloc_2) return new CodeInstruction(OpCodes.Ldloc_2);
            if (st.opcode == OpCodes.Stloc_3) return new CodeInstruction(OpCodes.Ldloc_3);
            if (st.opcode == OpCodes.Stloc_S) return new CodeInstruction(OpCodes.Ldloc_S, st.operand);
            return new CodeInstruction(OpCodes.Ldloc, st.operand);
        }

        /// <summary>Dlugosc wpisu: dla napisu dluzszego niz 32767 B - prawdziwa (4 + dlugosc tekstu).</summary>
        public static int FixLength(int len, TaleWorlds.Library.BinaryReader reader, int ext)
        {
            try
            {
                if ((ext & 0xFF) != Txt || reader == null || _cursor == null) return len;
                var data = reader.Data;
                int at = _cursor(reader);
                if (data == null || at < 0 || at + 4 > data.Length) return len;
                int tl = BitConverter.ToInt32(data, at);
                if (tl < 0) return len;
                long real = 4L + tl;
                if (real <= short.MaxValue || real > data.Length - at) return len;
                if (((int)real & 0xFFFF) != (len & 0xFFFF)) return len;
                Rescued++;
                if (real > Largest) Largest = real;
                return (int)real;
            }
            catch { return len; }
        }

        /// <summary>Po wczytaniu kampanii: jedna linia, jesli ratunek cos uratowal.</summary>
        internal static void ReportAfterLoad()
        {
            if (Rescued <= 0) return;
            Log.Info("Zapis: wczytany zapis mial " + Rescued + " napisow dluzszych niz 32767 B (najdluzszy " + Largest
                     + " B) - uratowane; nastepny zapis podzieli je na kawalki.");
            Rescued = 0;
            Largest = 0;
        }
    }
}
