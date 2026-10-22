using System;
using System.Security.Cryptography;
using System.Text;

namespace Armoury
{
    internal static partial class Chronicle
    {
        /// <summary>
        /// m105-kronika K0 (krok A3) - LEKTOR OKIEN KRONIKI (plan P9, KRONIKA-FORMAT 1 i 16.5). Nagrania: ModuleData/Lektor/EN/kr_NNN_w[war].ogg + manifest
        /// arm_lektor_kronika.tsv (tools/lektor/nagraj_kronika.py w repo glownym; czyta i gra Lektor). ZASADA md5: nagranie gra tylko, gdy md5 tekstu z manifestu =
        /// md5 UTF-8 tekstu okna zlozonego DOKLADNIE jak nagraj_kronika.py: tytul, gora, dol bez prefiksu {=...} (Data.Plain; komorki juz obciete jak strip() skryptu),
        /// tytul.rstrip('.!?') + "." + "\n\n" + gora + "\n\n" + dol, potem zamiany wymowy SAY ("R'hllor" -> "the Red God"). Tekst z '{' (zmienne) - bez glosu
        /// (skrypt takiego okna nie nagrywa). Wariant bez wlasnego tekstu ma tekst i klucz wiersza glownego (RowDef.NarrationKey) - to samo nagranie.
        /// Test zgodnosci z pythonem: scratchpad kr-a3 (md5_test.py + harness - wszystkie wiersze danych).
        /// </summary>
        internal static class Voice
        {
            // zamiany wymowy - DOKLADNIE slownik SAY z nagraj_kronika.py (w tej samej kolejnosci)
            private static readonly string[][] Say = { new[] { "R'hllor", "the Red God" } };

            /// <summary>Tekst nagrania wiersza (jak nagraj_kronika.py) albo null + why (zmienne w tekscie, brak wiersza).</summary>
            internal static string Text(Data.RowDef r, out string why)
            {
                why = null;
                if (r == null) { why = "brak wiersza"; return null; }
                string t = Data.Plain(r.Title), u = Data.Plain(r.Upper), l = Data.Plain(r.Lower);
                if ((t + u + l).IndexOf('{') >= 0) { why = "zmienne w tekscie"; return null; }
                string s = t.TrimEnd('.', '!', '?') + ".\n\n" + u + "\n\n" + l;
                foreach (var p in Say) s = s.Replace(p[0], p[1]);
                return s;
            }

            /// <summary>md5 (hex, male litery) UTF-8 tekstu nagrania wiersza albo null + why.</summary>
            internal static string Md5(Data.RowDef r, out string why)
            {
                string s = Text(r, out why);
                if (s == null) return null;
                using (var m = MD5.Create()) return Lektor.Hex(m.ComputeHash(new UTF8Encoding(false).GetBytes(s)));
            }

            /// <summary>Glos okna: null = nagranie Kroniki zgodne z tekstem (gra, gdy klucze lektora TAK); inaczej powod. md5 - md5 tekstu okna (do Spec.VoiceMd5;
            /// null przy zmiennych).</summary>
            internal static string Why(Data.RowDef r, out string md5)
            {
                md5 = null;
                try
                {
                    string why;
                    md5 = Md5(r, out why);
                    if (md5 == null) return why;
                    return Lektor.VoiceWhy(r.NarrationKey, md5);
                }
                catch (Exception e) { Stumble("Voice.Why", e); return "blad " + e.GetType().Name; }
            }

            /// <summary>Opis do linii "okno ... pokazane": "kr_NNN_w TAK" / "kr_NNN_w NIE (powod)" / "kr_NNN_w - (lektor=0)".</summary>
            internal static string LineText(Data.RowDef r, string why)
            {
                string k = r != null ? r.NarrationKey : "-";
                if (why == null) return k + (Lektor.WindowsEnabled ? " TAK" : " NIE (klucze lektora: okna NIE)");
                if (r != null && !r.Narrated && (why == "brak nagrania" || why == "zmienne w tekscie")) return k + " - (lektor=0)";
                return k + " NIE (" + why + ")";
            }
        }
    }
}
