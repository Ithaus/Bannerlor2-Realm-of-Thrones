using System;
using System.Collections.Generic;
using System.Text;

namespace Armoury
{
    /// <summary>
    /// N1(a) (noc 16.10, galaz noc-ekonomia cz. 2; raport audytu ekonomii 15.10 rozdz. 1.9 i 3.1) - SAM LOG, niczego nie zmienia w grze.
    /// Linia "Pompy - sztuczne przelewy (nekon): dzien N" - jedna liczba przymusu: suma przelewow "z urzedu" tej doby i jej udzial w zoldzie partii i zalog
    /// (miara: gdy naturalne ujscia zadzialaja, suma i % zoldu maleja). Te same liczniki co linia "Obieg: dzien" (MoneyLedger.ObiegLine, ta sama chwila
    /// MoneyLedger.Daily) i "Kiesa ludu" (TownFolk, robocizna / obrot P / doplata do 35%). Grupy jak w audycie (sztuczne/inwentarz.csv):
    ///  A kasy miast -> lud (robocizna handlu, obrot P, doplata do 35%; robocizna zamkow; recenzja n23: roboty miast z rak okregu p6-a7 - TownHire, kasy miast -> kiesy
    ///  wsi bez towaru, pozycja MTownHire ksiegi), B lud -> podatki od oszczednosci (kiesa2 + straznik + wsie z czescia
    ///  korony, danina P1, podatek wojenny 184, stawka W7), C kasy osad -> panowie i korony (zawor miast i zamkow, danina wojenna z kas, korona z utargu wsi),
    ///  D korona -> rody tej samej doby (zwrot zoldu 165, renty 180, W5, W5b, W6, M1), E rody -> dwor (sprzet niewydany E5/A2). Pozycje 0 tej doby - "uspione".
    /// Zwrot zoldu zostaje (decyzja Jeffa 15.10) - liczony, bo to przelew z urzedu. Wyjatek - "potkniecia" w linii (licz, nie gas).
    /// </summary>
    internal static partial class MoneyLedger
    {
        private static string PumpsLine(int day)
        {
            int st = 0;
            var sleep = new List<string>();
            Func<string, Func<long>, long> V = (name, f) =>
            {
                long v;
                try { v = f(); }
                catch { st++; v = 0; }
                if (v < 0) v = 0;
                if (v == 0) sleep.Add(name);
                return v;
            };
            bool watch = SoldierPay.LastWatching;
            long pay = (watch ? SoldierPay.LastLordAcc : _wage[WLord]) + (watch ? SoldierPay.LastGarAcc : _wage[WGarrison]);
            // A kasy miast -> lud
            long aLab = V("robocizna miast", () => TownFolk.LastLabourTowns), aTurn = V("obrot P", () => TownFolk.LastTurnTowns), aTop = V("doplata do 35%", () => TownFolk.LastTopUpTowns);
            long aCas = V("robocizna zamkow", () => TownFolk.LastLabourCastles);
            long aHire = V("roboty miast p6-a7", () => -_mark[CTown, MTownHire]);   // recenzja n23: TownHire - przelew reguly (nic sie nie buduje), zloto zdjete z kas miast tej doby
            long A = aLab + aTurn + aTop + aCas + aHire;
            // B lud -> podatki od oszczednosci
            long bHoard = V("kiesa2 i straznik", () => TownFolk.LastHoardLord + TownFolk.LastHoardTown + TownFolk.LastHoardCastle + TownFolk.LastHoardCrown
                                                       + TownFolk.LastVillHoardLord + TownFolk.LastVillHoardTown + TownFolk.LastVillHoardCastle);
            long bP1 = V("danina P1", () => KingdomTreasury.LastFolkWarAid), b184 = V("podatek 184", () => KingdomTreasury.LastFolkWarTax), bW7 = V("W7", () => KingdomTreasury.LastLordLevy);
            long B = bHoard + bP1 + b184 + bW7;
            // C kasy osad -> panowie i korony
            long cTown = V("zawor miast", () => PopulationLaw.DayTownRent + (TownPurse.On ? TownPurse.LastCrownPaid : 0));
            long cCastle = V("zawor zamkow", () => CastlePurse.LastLordPaid + CastlePurse.LastCrownPaid);
            long cWar = V("danina wojenna z kas", () => KingdomTreasury.LastSubsidy), cVill = V("korona z utargu wsi", () => -_mark[CVill, MCrown]);
            long C = cTown + cCastle + cWar + cVill;
            // D korona -> rody tej samej doby
            long dRef = V("zwrot zoldu", () => KingdomTreasury.LastRefundGiven), dRent = V("renty 180", () => CrownRents.LastPaid), dW5 = V("W5", () => KingdomTreasury.LastNeedAid);
            long dW5b = V("W5b", () => KingdomTreasury.LastCrownSurplusAid), dW6 = V("W6", () => KingdomTreasury.LastScutageOut), dM1 = V("M1", () => KingdomTreasury.LastNeedAidRent);
            bool w5cOn = (Settings.Current != null && Settings.Current.CrownWarAidMiddle) || KingdomTreasury.LastMidAid != 0;   // f15: klucz wylaczony i 0 - linia jak dotad
            long dW5c = w5cOn ? V("W5c", () => KingdomTreasury.LastMidAid) : 0;   // f15: skarbiec dla srednich (skarbce ponad zapas wojny -> glowy rodow)
            long D = dRef + dRent + dW5 + dW5b + dW6 + dM1 + dW5c;
            // E rody -> dwor
            long E = V("sprzet niewydany -> dwor", () => CourtFull.LastPaid);
            // uspione poza suma (mechanizmy dzis bez ruchu): chleb M3, pozyczki koron od miast, dosypka regulatora kas miast
            long m3 = V("chleb M3", () => TownFolk.LastBreadCrown + TownFolk.LastBreadLord), crownLoan = V("pozyczki koron od miast", () => TownFolk.LastCrownLoanLent);
            long reg = V("dosypka regulatora kas miast", () => _regIn[CTown]);
            long all = A + B + C + D + E;
            // F (E3, noc-ekonomia3 16.10) naturalne z obrotu - POZA suma sztucznych: myto targowe (E3-G3), czynsz gruntowy (E3-G4), danina pana od markietanow (E3-G2);
            // rozbicie wedlug platnika (uwaga recenzji 1): z ludu i wsi (kiesy W/R, czesc wsi markietanow, utarg wozow wsi przed podzialem 112) / z kies karawan
            long fCart = V("myto wozow wsi", () => TownToll.LastCart), fCar = V("myto karawan", () => TownToll.LastCaravan);
            long fRent = V("czynsz gruntowy", () => TownFolk.LastGroundRent), fSut = V("danina markietanow", () => Sutlers.LastVillTribute);
            long fFolk = fCart + fRent + fSut, F = fFolk + fCar;
            var sb = new StringBuilder(1024);
            sb.Append("Pompy - sztuczne przelewy (nekon): dzien ").Append(day)
              .Append(" | razem ").Append(all).Append(" zl = ").Append(pay > 0 ? (100.0 * all / pay).ToString("0.0", Inv) : "-").Append("% zoldu (zold partii i zalog ").Append(pay).Append(')')
              .Append(" | A kasy miast -> lud ").Append(A).Append(" (robocizna ").Append(aLab).Append(", obrot P ").Append(aTurn).Append(", doplata do 35% ").Append(aTop).Append(", zamki robocizna ").Append(aCas).Append(", roboty miast p6-a7 ").Append(aHire).Append(')')
              .Append(" | B lud -> podatki od oszczednosci ").Append(B).Append(" (kiesa2 i straznik ").Append(bHoard).Append(", danina P1 ").Append(bP1).Append(", podatek 184 ").Append(b184).Append(", W7 ").Append(bW7).Append(')')
              .Append(" | C kasy osad -> panowie i korony ").Append(C).Append(" (zawor miast ").Append(cTown).Append(", zawor zamkow ").Append(cCastle).Append(", danina wojenna z kas ").Append(cWar)
              .Append(", korona z utargu wsi ").Append(cVill).Append(')')
              .Append(" | D korona -> rody tej samej doby ").Append(D).Append(" (zwrot zoldu ").Append(dRef).Append(", renty 180 ").Append(dRent).Append(", W5 ").Append(dW5).Append(", W5b ").Append(dW5b)
              .Append(", W6 ").Append(dW6).Append(", M1 ").Append(dM1).Append(w5cOn ? ", W5c " + dW5c : "").Append(')')
              .Append(" | E rody -> dwor (sprzet niewydany) ").Append(E)
              .Append(" | F naturalne z obrotu (E3, poza suma) ").Append(F).Append(" (z ludu i wsi ").Append(fFolk).Append(": czynsz gruntowy z kies W/R ").Append(fRent)
              .Append(", danina markietanow zamiast do kies wsi ").Append(fSut).Append(", myto wozow wsi - utarg wsi przed 112 ").Append(fCart).Append("; z kies karawan - wlasciciele ").Append(fCar)
              .Append("), udzial z ludu w zwrocie od ludu F-lud / (B + F-lud) ").Append(B + fFolk > 0 ? (100.0 * fFolk / (B + fFolk)).ToString("0.0", Inv) + "%" : "-")
              .Append(" | poza suma: chleb M3 ").Append(m3).Append(", pozyczki koron od miast ").Append(crownLoan).Append(", dosypka regulatora kas miast ").Append(reg)
              .Append(" | uspione (0 dzis): ").Append(sleep.Count > 0 ? string.Join(", ", sleep.ToArray()) : "-")
              .Append(" | potkniecia ").Append(st).Append('.');
            return sb.ToString();
        }
    }
}
