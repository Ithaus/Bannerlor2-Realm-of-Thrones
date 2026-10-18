using System;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// f5-lud (noc 17/18.10, KsFolkIncome.cs) - pomocnicze drogi kiesy ludu dla dochodu klas wedlug koszyka:
    ///  - SplitF5: podzial zarobku rodzaju `kind` na pelne czesci klas W / R / P - TE SAME zaokraglenia co Credit (R i P w dol, W reszta; praca niewolnych z czesci W do
    ///    wlascicieli R40/P60);
    ///  - CreditF5 (TownFolk.Earn): czesci klas x kappa klas (W2 / R / P) do kies - zwraca przyjete (wolajacy Earn daje kasie miasta reszte - ten sam protokol co przy
    ///    kiesie nieczynnej); ksiega jak Credit (LDayIn, EarnK rodzaju, zarobek okna klas, praca niewolnych);
    ///  - FromCofferF5 (EarnFromTown - przelew gracza przez kase): z kasy schodzi tylko czesc przyjeta (reszta nigdy nie schodzi z kasy).
    /// Wolane TYLKO, gdy KsFolkIncome.Factors da true (miasto z regula i czynnik &lt; 1) - inaczej stara droga Credit / FromCoffer co do monety. Nie rzucaja.
    /// </summary>
    internal static partial class TownFolk
    {
        // recenzja f5 (uwaga 1): czesc zatrzymana przez regule f5 w ostatnim CreditF5 (watek glowny) - czyta ja tylko EarnBack (wolajacy, ktory czesc nieprzyjeta oddaje
        // placacemu spoza miasta - kiesie wsi), zeby linia f5 nie liczyla jej jako "oddane kasom miast"
        private static long _f5Cut;

        /// <summary>
        /// Recenzja f5 (uwaga 1): Earn dla placacego spoza miasta, ktory czesc nieprzyjeta bierze z powrotem (kiesa wsi: roboty gminy DS - DaPayVillage, przebudowa zajecia
        /// wioski - CraftsRebuildPay). `f5Cut` - czesc zatrzymana w tym wywolaniu przez regule f5 (0 bez reguly albo przy kiesie nieczynnej). Wolajacy po zwrocie
        /// do wsi wola NoteF5VillageBack(min(zwrot, f5Cut)). Zarobek co do monety jak Earn. Nie rzuca.
        /// </summary>
        private static long EarnBack(Settlement st, int amt, int kind, out long f5Cut)
        {
            _f5Cut = 0;
            long fin = Earn(st, amt, kind);
            f5Cut = Math.Max(0, _f5Cut);
            _f5Cut = 0;
            return fin;
        }

        /// <summary>Recenzja f5 (uwaga 1): czesc zatrzymana przez regule f5 wrocila do kiesy wsi (placacego), nie do kasy miasta - licznik linii f5 "wrocilo do wsi".</summary>
        private static void NoteF5VillageBack(long x)
        {
            if (x <= 0) return;
            try { KsFolkIncome.NoteVillageBack(x); } catch (Exception e) { Stumble("KsFolkIncome.NoteVillageBack", e); }
        }

        /// <summary>Pelne czesci klas zarobku `amt` (jak Credit); `slaveR` / `slaveP` - czesc praca niewolnych (z czesci W) w czesciach R i P.</summary>
        private static void SplitF5(Folk f, long amt, int kind, out long fW, out long fR, out long fP, out double slaveR, out double slaveP)
        {
            double w, r, p;
            SplitOf(kind, out w, out r, out p);
            slaveR = slaveP = 0;
            if (f.Slave && f.HS + f.HW > 0 && w > 0)
            {
                double y = w * f.HS / (f.HS + f.HW);
                w -= y; r += y * OwnR; p += y * OwnP;
                slaveR = amt * y * OwnR; slaveP = amt * y * OwnP;
            }
            fR = (long)(amt * r); fP = (long)(amt * p); fW = amt - fR - fP;
        }

        /// <summary>Zapis przyjetych czesci klas (miasto). `slave` - przyjeta czesc pracy niewolnych (linia "praca niewolnych do wlascicieli").</summary>
        private static void CreditParts(Folk f, long aW, long aR, long aP, int kind, long slave)
        {
            aW = Math.Max(0, aW); aR = Math.Max(0, aR); aP = Math.Max(0, aP);
            long amt = aW + aR + aP;
            if (amt <= 0) return;
            f.LDayIn += amt;
            if (kind >= 0 && kind < EKinds) f.EarnK[kind] += amt;
            f.KW += aW; f.KR += aR; f.KP += aP;
            f.EarnWin[0] += aW; f.EarnWin[1] += aR; f.EarnWin[2] += aP;
            if (slave > 0) _win.SlaveW += slave;
        }

        /// <summary>TownFolk.Earn (miasto z regula): czesci klas x kappa (W2 / R / P) do kies; zwraca przyjete (reszta - u wolajacego, do kasy miasta).</summary>
        private static long CreditF5(Folk f, long amt, int kind, double kw2, double kr, double kp)
        {
            if (amt <= 0) return 0;
            long fW, fR, fP; double sR, sP;
            SplitF5(f, amt, kind, out fW, out fR, out fP, out sR, out sP);
            long aW = (long)(fW * Clamp01(kw2)), aR = (long)(fR * Clamp01(kr)), aP = (long)(fP * Clamp01(kp));
            long slave = (long)(sR * Clamp01(kr) + sP * Clamp01(kp));
            CreditParts(f, aW, aR, aP, kind, slave);
            try { KsFolkIncome.NoteExt(fW, fR, fP, aW, aR, aP); } catch (Exception e) { Stumble("KsFolkIncome.NoteExt", e); }
            _f5Cut = Math.Max(0, amt - (aW + aR + aP));                 // recenzja f5 (uwaga 1): dla EarnBack (placacy spoza miasta)
            return aW + aR + aP;
        }

        /// <summary>EarnFromTown (miasto z regula): kasa -> kiesy tylko czesc przyjeta (najwyzej stan kasy; przy przycieciu klasy w tej samej proporcji). Zwraca to,
        /// co naprawde zeszlo z kasy.</summary>
        private static long FromCofferF5(Folk f, long x, int kind, double kw2, double kr, double kp)
        {
            if (x <= 0 || f == null || f.St == null || f.St.Town == null) return 0;
            long fW, fR, fP; double sR, sP;
            SplitF5(f, x, kind, out fW, out fR, out fP, out sR, out sP);
            long gW = (long)(fW * Clamp01(kw2)), gR = (long)(fR * Clamp01(kr)), gP = (long)(fP * Clamp01(kp));
            long ask0 = gW + gR + gP;
            var town = f.St.Town;
            long got = 0;
            if (ask0 > 0)
            {
                int ask = (int)Math.Min(int.MaxValue, Math.Min(ask0, Math.Max(0, town.Gold)));
                if (ask > 0)
                {
                    int had = town.Gold;
                    town.ChangeGold(-ask);
                    got = Math.Max(0, had - town.Gold);
                }
            }
            long pW = 0, pR = 0, pP = 0;
            if (got > 0)
            {
                if (got >= ask0) { pW = gW; pR = gR; pP = gP; }
                else { pW = (long)Math.Floor(got * (double)gW / ask0); pR = (long)Math.Floor(got * (double)gR / ask0); pP = got - pW - pR; }
                double kr1 = Clamp01(kr), kp1 = Clamp01(kp);
                long slave = gR + gP > 0 ? (long)((sR * kr1 + sP * kp1) * (double)(pR + pP) / (gR + gP)) : 0;
                CreditParts(f, pW, pR, pP, kind, slave);
                _ctlIn += got;
            }
            try { KsFolkIncome.NoteCoffer(0, fW, gW, pW); KsFolkIncome.NoteCoffer(1, fR, gR, pR); KsFolkIncome.NoteCoffer(2, fP, gP, pP); }
            catch (Exception e) { Stumble("KsFolkIncome.NoteCoffer", e); }
            return got;
        }
    }
}
