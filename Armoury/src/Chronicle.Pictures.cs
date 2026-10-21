namespace Armoury
{
    internal static partial class Chronicle
    {
        /// <summary>
        /// m105-kronika K0 (krok A2: tylko interfejs) - OBRAZKI OKIEN KRONIKI (KRONIKA-FORMAT 14, plan P8). Krok A3 wypelnia Pick: pula tematu = pliki
        /// Images/Kronika/kronika_&lt;temat&gt;_&lt;n&gt; + istniejace Images/Prolog/prolog_NN z kolumny prolog arm_kronika_tematy.tsv + sprite'y ROT tematu; indeks = staly skrot
        /// klucza okna mod n, przy powtorce poprzedniego obrazka sesji - nastepny; plik przez StoryPictures.Acquire (zwolnienie: Chronicle.OnClosed -> StoryPictures.Drop
        /// dla nazw arm_*). Teraz zawsze null - silnik bierze pierwszy sprite ROT tematu (Chronicle.DefaultSprite), jak Prologue.Scenes przy braku pliku.
        /// </summary>
        internal static class Pictures
        {
            /// <summary>Nazwa sprite'a do HeaderImage okna (arm_* z pliku albo sprite ROT) albo null (silnik - sprite domyslny tematu). how - opis do linii "okno ... pokazane".
            /// w - okno, r - wiersz, ktory wygral, id - NNN.w albo instancja NNN.w#klucz (staly skrot do indeksu puli).</summary>
            internal static string Pick(Data.WindowDef w, Data.RowDef r, string id, out string how)
            {
                how = "Pictures: krok A3";
                return null;
            }
        }
    }
}
