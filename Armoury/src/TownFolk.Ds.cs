using System;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// DS F3 (DsTowns.cs) - wejscia kiesy ludu dla miast i zamkow przy ustalonych firmach:
    ///  - M6 WYPLATA KUPCOM (EarnDraw): kasa miasta -> kiesy P (udzial pShare) i R jako zarobek okna (podatek pana i 184 nastepnej doby) - zamiast "5% zaworu dla ludu";
    ///    w ksiedze kies jak czesc ludu zaworu (_win.ValveT, EValve). Zamek - EarnCastleValve (kiesa podzamcza, jak 15% zaworu zamku).
    ///  - ZAJECIE (DistrainCommune): zaleglosc firmy miasta z kiesy gminy - najwyzej 2/3 jej stanu w dzien raty; DistrainUndo oddaje zdjete po wyjatku przelewu.
    /// Ksiega kies: _ctlIn / _ctlOut i LDayIn / LDayOut (kontrola zamknieta kiesa-ludu.csv). Nie rzucaja.
    /// </summary>
    internal static partial class TownFolk
    {
        /// <summary>M6: kasa miasta -> kiesy P i R (zarobek okna). Zwraca to, co zeszlo z kasy (0 - kiesa nieczynna, poza watkiem, brak ludu).</summary>
        internal static long EarnDraw(Settlement st, long x, double pShare)
        {
            try
            {
                if (x <= 0 || st == null || !st.IsTown || st.Town == null || !Live || OffMain()) return 0;
                var f = Get(st);
                if (f == null || f.Kind != 0) return 0;
                var town = st.Town;
                double ps = double.IsNaN(pShare) ? 0.7 : Math.Max(0, Math.Min(1, pShare));
                // f5-lud (Ks Folk Income R P): czesc P x kappa P, czesc R x kappa R - z kasy schodzi tylko zlecone, reszta zostaje w kasie (przeglad firmy wedlug nadwyzki
                // kasy oddaje ja panu i koronie); bez reguly albo czynniki 1 - droga co do monety jak dotad
                double f5R, f5P;
                long xP = 0, xR = 0, aP = 0, aR = 0;
                bool f5 = KsFolkIncome.RP(st, out f5R, out f5P);
                long want = x;
                if (f5)
                {
                    xP = Math.Min(x, (long)Math.Round(x * ps)); xR = x - xP;
                    aP = (long)(xP * Math.Max(0.0, Math.Min(1.0, f5P))); aR = (long)(xR * Math.Max(0.0, Math.Min(1.0, f5R)));
                    want = aP + aR;
                    if (want <= 0) { try { KsFolkIncome.NoteM6(xP, xR, 0, 0, 0, 0); } catch (Exception e) { Stumble("KsFolkIncome.NoteM6", e); } return 0; }
                }
                int ask = (int)Math.Min(int.MaxValue, Math.Min(want, Math.Max(0, town.Gold)));
                if (ask <= 0) return 0;
                int had = town.Gold;
                town.ChangeGold(-ask);
                long got = had - town.Gold;
                if (got <= 0) return 0;
                long p, r;
                if (!f5) { p = Math.Min(got, (long)Math.Round(got * ps)); r = got - p; }
                else
                {
                    p = got >= want ? aP : (long)Math.Floor(got * (double)aP / want); r = got - p;
                    try { KsFolkIncome.NoteM6(xP, xR, aP, aR, p, r); } catch (Exception e) { Stumble("KsFolkIncome.NoteM6", e); }
                }
                f.KR += r; f.KP += p; f.EarnWin[1] += r; f.EarnWin[2] += p;
                f.LDayIn += got; f.EarnK[EValve] += got;
                _ctlIn += got; _win.ValveT += got; _win.Earn[EValve] += got;
                return got;
            }
            catch (Exception e) { Stumble("EarnDraw", e); return 0; }
        }

        /// <summary>Zajecie zaleglosci firmy z kiesy gminy miasta: najwyzej `want` i 2/3 stanu kiesy gminy. Zwraca zdjete (wolajacy przelewa panu i koronie).</summary>
        internal static long DistrainCommune(Settlement st, long want)
        {
            try
            {
                if (want <= 0 || st == null || !st.IsTown || !Live || OffMain()) return 0;
                var f = Get(st);
                if (f == null || f.Kind != 0) return 0;
                long cap = (long)Math.Floor(Math.Max(0, f.KG) * 2.0 / 3.0);
                long x = Math.Min(want, cap);
                if (x > int.MaxValue / 2) x = int.MaxValue / 2;
                if (x <= 0) return 0;
                f.KG -= x; _ctlOut += x; f.LDayOut += x;
                return x;
            }
            catch (Exception e) { Stumble("DistrainCommune", e); return 0; }
        }

        private static readonly long[] _dsFr = new long[3];   // ostatnie zdjecie DsFolkRate wedlug klasy (zwrot po wyjatku przelewu)

        /// <summary>
        /// DS F3 (recenzja 17.10, "scot and lot"): czesc firmy pana z oszczednosci ludu (dawny podatek od skarbca ludu - czesc pana - i W7 z ludu) placi lud osady: kiesy
        /// W / R / P miasta (zamek: kiesa podzamcza) ponad zapas dni (W TownFolkReserveDays / R 21 / P 30 / podzamcze 10) i ponad oferte nadwyzki na jutro - ta sama miara
        /// co W7 (LevyRoom), bez wymogu dzisiejszej doby ludu (dzien raty biegnie przed doba ludu); proporcjonalnie do nadwyzki klas. Zwraca zdjete (wolajacy przelewa
        /// DOKLADNIE tyle panu). Ksiega kies: _ctlOut / LDayOut. Nie rzuca.
        /// </summary>
        internal static long DsFolkRate(Settlement st, long want)
        {
            _dsFr[0] = _dsFr[1] = _dsFr[2] = 0;
            try
            {
                if (want <= 0 || st == null || !Live || OffMain()) return 0;
                Folk f;
                if (!_f.TryGetValue(st, out f) || f == null || f.Kind < 0 || f.Kind > 1 || !f.Cal) return 0;
                if (f.Kind == 1 && !CastleLive) return 0;
                var s = Settings.Current;
                double rdW = Math.Max(1, Math.Min(60, s != null ? s.TownFolkReserveDays : 10));
                int n = f.Kind == 1 ? 1 : 3;
                var room = new double[3];
                double e = 0;
                for (int c = 0; c < n; c++)
                {
                    double rd = f.Kind == 1 ? RdC : (c == 0 ? rdW : c == 1 ? RdR : RdP);
                    double now = PurseOf(f, c) - rd * Math.Max(0, f.Need[c]) - Math.Max(0, f.Offer[c]);
                    room[c] = now >= 1 ? Math.Floor(now) : 0;
                    e += room[c];
                }
                if (e < 1) return 0;
                long goal = (long)Math.Min((double)want, e);
                long took = 0;
                for (int c = 0; c < n; c++)
                {
                    long x = (long)Math.Min(Math.Floor(goal * room[c] / e), room[c]);
                    if (x <= 0) continue;
                    AddPurse(f, c, -x); _dsFr[c] += x; took += x; room[c] -= x;
                }
                for (int c = 0; c < n && took < goal; c++)                 // reszta z zaokraglen - z klas, ktorym jeszcze zostala nadwyzka
                {
                    long x = (long)Math.Min(goal - took, room[c]);
                    if (x <= 0) continue;
                    AddPurse(f, c, -x); _dsFr[c] += x; took += x;
                }
                if (took > 0) { _ctlOut += took; f.LDayOut += took; }
                return took;
            }
            catch (Exception ex) { Stumble("DsFolkRate", ex); return 0; }
        }

        /// <summary>Zwrot zdjetego przez DsFolkRate (wyjatek przy przelewie do pana - zloto wraca do tych samych klas; nic w nicosc).</summary>
        internal static void DsFolkRateUndo(Settlement st, long x)
        {
            try
            {
                if (x <= 0 || st == null) return;
                Folk f;
                if (!_f.TryGetValue(st, out f) || f == null) return;
                long back = 0;
                for (int c = 0; c < 3; c++) { if (_dsFr[c] > 0) { AddPurse(f, c, _dsFr[c]); back += _dsFr[c]; _dsFr[c] = 0; } }
                if (back < x) { AddPurse(f, 0, x - back); back = x; }
                _ctlOut -= back; f.LDayOut -= back;
            }
            catch (Exception e) { Stumble("DsFolkRateUndo", e); }
        }

        /// <summary>Zwrot zdjetego z kiesy gminy (wyjatek przy przelewie - nic w nicosc).</summary>
        internal static void DistrainUndo(Settlement st, long x)
        {
            try
            {
                if (x <= 0 || st == null) return;
                Folk f;
                if (!_f.TryGetValue(st, out f) || f == null) return;
                f.KG += x; _ctlOut -= x; f.LDayOut -= x;
            }
            catch (Exception e) { Stumble("DistrainUndo", e); }
        }
    }
}
