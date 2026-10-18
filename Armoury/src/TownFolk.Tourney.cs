using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// f8-sakiewka (decyzja Jeffa 18.10 "4 tak"; projekt docs/kiesa-ludu/PROJEKT-WYDATKI-2026-10-13.md P5) - STRONA LUDU sakiewki turniejowej: mieszczanie
    /// miasta-gospodarza dokladaja do nagrody turnieju jedna dzienna oferte swojego ludu. Przelew do zwyciezcy - TourneyFolkPurse.cs.
    ///  - ILE: oferta nadwyzki na towar z ostatniej doby ludu tego miasta (f.Offer W/R/P - przy liscie dostatku 1/FolkSurplusSpendDays kiesy ponad zapas dni
    ///    po daninie; R i P - 30%, ktore nie ida na place) x TourneyFolkPurseDays. Miasto bez nadwyzki ma oferte 0 - sakiewki nie ma; bogate - duza.
    ///  - SKAD: kazda klasa najwyzej swoja czesc oferty x dni i najwyzej kiese ponad zapas dni (W TownFolkReserveDays / R 21 / P 30) i ponad oferte na jutro
    ///    (to, co lud juz postanowil wydac na towar) - ta sama miara "ponad zapas" co W7 (LevyRoom), danina DS (DsFolkRate) i nabrzeze (WaterfrontRoom).
    ///    Doba ludu tej osady musiala biec dzis albo wczoraj (turnieje AI rozstrzyga tick dobowy osad rozlozony na cala dobe - takze przed nasza doba ludu).
    ///  - Tylko miasto z kiesa czynna i skalibrowana (Live, Kind 0, Cal). Zamki i wsie turniejow nie maja.
    ///  - Zdjecie: kiesa klasy -> wolajacy (przelewa DOKLADNIE tyle zwyciezcy), ksiega kies: _ctlOut i LDayOut (kontrola zamknieta "wydatki" r = 0 i wiersz
    ///    kiesa-ludu.csv, jak W7 LevyTake). Zwrot po wyjatku przy przelewie - te same klasy (nic w nicosc). Bez nowych pol zapisu. Nie rzuca.
    /// </summary>
    internal static partial class TownFolk
    {
        // powod braku sakiewki (TourneyPurseTake.why)
        internal const int TpOk = 0, TpNoFolk = 1, TpStale = 2, TpNoOffer = 3, TpNoRoom = 4, TpBelowMin = 5;
        private static readonly long[] _tpFr = new long[3];   // ostatnie zdjecie wedlug klas (zwrot po wyjatku przy przelewie)
        private static Settlement _tpSt;                      // osada ostatniego zdjecia

        /// <summary>
        /// f8: zdejmuje sakiewke turniejowa z kies ludu miasta st: klasa c najwyzej floor(days x Offer[c]) i najwyzej kiesa - zapas dni - oferta na jutro;
        /// cap - najwyzej tyle razem (miejsce w kiesie odbiorcy do int.MaxValue). Mniej niz min - nic (why TpBelowMin). cls - zdjete wedlug klas (W, R, P).
        /// offer - oferta dnia osady (W + R + P, przed mnoznikiem dni). below - klasy ponizej zapasu dni po zdjeciu (kontrola, oczekiwane 0).
        /// Zwraca zdjete naprawde (roznica kies przed i po). Ksiega kies: _ctlOut / LDayOut. Nie rzuca.
        /// </summary>
        internal static long TourneyPurseTake(Settlement st, double days, long min, long cap, long[] cls, out int why, out long offer, out int below)
        {
            why = TpNoFolk; offer = 0; below = 0;
            if (cls != null && cls.Length >= 3) cls[0] = cls[1] = cls[2] = 0;
            _tpFr[0] = _tpFr[1] = _tpFr[2] = 0; _tpSt = null;
            try
            {
                if (st == null || !Live || OffMain()) return 0;
                Folk f;
                if (!_f.TryGetValue(st, out f) || f == null || f.Kind != 0 || !f.Cal) return 0;
                int today = (int)CampaignTime.Now.ToDays;
                if (f.ADay < 0 || f.ADay < today - 2) { why = TpStale; return 0; }   // oferta z doby ludu sprzed wiecej niz wczoraj - nie wiemy, ile lud ma ponad zapas
                var s = Settings.Current;
                double rdW = Math.Max(1, Math.Min(60, s != null ? s.TownFolkReserveDays : 10));
                if (double.IsNaN(days) || days < 0) days = 0;
                var want = new long[3]; var room = new long[3];
                long wsum = 0;
                for (int c = 0; c < 3; c++)
                {
                    double of = Math.Max(0, f.Offer[c]);
                    offer += (long)of;
                    if (!(f.Need[c] > 0)) continue;                                  // bez kotwicy potrzeby nie ma miary zapasu - ta klasa nie daje
                    double rd = c == 0 ? rdW : c == 1 ? RdR : RdP;
                    double now = PurseOf(f, c) - rd * f.Need[c] - of;
                    room[c] = now >= 1 ? (long)Math.Floor(now) : 0;
                    want[c] = (long)Math.Floor(days * of);
                    wsum += want[c];
                }
                if (wsum < 1) { why = TpNoOffer; return 0; }
                var x = new long[3];
                long tot = 0;
                for (int c = 0; c < 3; c++) { x[c] = Math.Max(0, Math.Min(want[c], room[c])); tot += x[c]; }
                if (tot <= 0) { why = TpNoRoom; return 0; }
                if (cap < tot)
                {
                    long over = tot - Math.Max(0, cap);                              // odbiorca bez miejsca w kiesie (int.MaxValue) - mniej, od klasy P w gore
                    for (int c = 2; c >= 0 && over > 0; c--) { long d = Math.Min(over, x[c]); x[c] -= d; over -= d; tot -= d; }
                }
                if (tot < Math.Max(1, min)) { why = TpBelowMin; return 0; }
                long k0 = f.KW + f.KR + f.KP;
                for (int c = 0; c < 3; c++)
                {
                    if (x[c] <= 0) continue;
                    AddPurse(f, c, -x[c]); _tpFr[c] = x[c];
                    if (cls != null && cls.Length >= 3) cls[c] = x[c];
                }
                long took = k0 - (f.KW + f.KR + f.KP);
                if (took > 0) { _ctlOut += took; f.LDayOut += took; _tpSt = st; }
                for (int c = 0; c < 3; c++)
                {
                    if (x[c] <= 0) continue;
                    double rd = c == 0 ? rdW : c == 1 ? RdR : RdP;
                    if (PurseOf(f, c) + 0.5 < rd * f.Need[c]) below++;
                }
                why = TpOk;
                return took;
            }
            catch (Exception e) { Stumble("TourneyPurseTake", e); return 0; }
        }

        /// <summary>f8: zwrot ostatniego zdjecia (wyjatek przy przelewie do zwyciezcy) - zloto wraca do tych samych klas; zwraca oddane. Ksiega kies: _ctlOut / LDayOut z powrotem.</summary>
        internal static long TourneyPurseUndo(Settlement st)
        {
            try
            {
                Folk f;
                if (st == null || !ReferenceEquals(st, _tpSt) || !_f.TryGetValue(st, out f) || f == null) return 0;
                long back = 0;
                for (int c = 0; c < 3; c++) { if (_tpFr[c] > 0) { AddPurse(f, c, _tpFr[c]); back += _tpFr[c]; _tpFr[c] = 0; } }
                _ctlOut -= back; f.LDayOut -= back; _tpSt = null;
                return back;
            }
            catch (Exception e) { Stumble("TourneyPurseUndo", e); return 0; }
        }
    }
}
