using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// f4b-glod (noc 17/18.10): PRZYCZYNA zgonow mieszczan w warowni przy Food 0 - tylko POMIAR (segment linii "Spichlerze (E4/6)"), gra liczy jak dotad.
    /// Test fin87 (f5fd0ae3, sciezka Jeffa 160 dob): NKS-15 policzyl 163 zmarlych mieszczan Sharp Point jako "poza oblezeniami i spustoszeniem", bo przyczyne bral
    /// z OKREGOW WSI tej doby (licznik "okregi glodne: oblezone / spustoszone"), a glodne bylo samo miasto - oblezone dwa razy z rzedu (109118-109138 Bryce Caron,
    /// 109139-109157 Renly Baratheon, przerwa jednej doby; Food 16.6 tys. -> 4.2 tys. w pierwszym oblezeniu, 0 w 8. dobie drugiego, potem zdobyte). Okregi wsi
    /// Sharp Point glodu nie mialy (spichlerze 800+ dni), wiec dzienny licznik okregow dawal 0 oblezonych. Tu przyczyne bierze sama warownia w chwili zgonu:
    ///  - oblezona (Settlement.IsUnderSiege - ta sama chwila, w ktorej Famine.Daily liczy niedobor z FoodChange oblezonego miasta),
    ///  - po najezdzie (rabunek okregu w ciagu roku - ta sama regula co okregi wsi, StruckWithinYear - albo wies warowni nie w stanie Normal: zlupiona / w rabunku),
    ///  - bez przyczyny (reszta - prawdziwy glod miasta poza wojna, ktory NKS-15 ma lapac).
    /// Schronieni i uchodzcy z najazdu w murach gina tylko w oblezonej warowni (krok 3 i krok 7 Famine.Daily) - osobna liczba. Kontrola: suma przyczyn = "mieszczanie"
    /// linii. Bez nowego klucza MCM (jak RT-1/RT-5 fix-uchodzcy): nic w grze sie nie zmienia, linia dostaje segment.
    /// </summary>
    internal static partial class Famine
    {
        private sealed class TownCauseTally
        {
            public readonly double[] Dead = new double[3];   // 0 - oblezona, 1 - po najezdzie, 2 - bez przyczyny
            public readonly int[] N = new int[3];            // miast z zgonami tej doby wedlug przyczyny
            public readonly List<string> Siege = new List<string>(), Raid = new List<string>(), None = new List<string>();
        }

        /// <summary>Krok 7 Famine.Daily, po PeopleBook.TownDie: przyczyna zgonow mieszczan warowni `node` (su - jednostki Food uchodzcow w murach, cons - spozycie
        /// razem z nimi). Nie rzuca (potkniecie per warownia).</summary>
        private static void TownCauseAdd(TownCauseTally tc, Settlement node, int today, double dead, double su, double cons)
        {
            if (tc == null || node == null || !(dead > 0.0)) return;
            try
            {
                int c = node.IsUnderSiege ? 0 : (StruckWithinYear(node, today) || BoundVillageHit(node) ? 1 : 2);
                tc.Dead[c] += dead; tc.N[c]++;
                string name = NameOf(node);
                if (c == 0) { if (tc.Siege.Count < Top) tc.Siege.Add(name + " (uchodzcy w murach " + F(cons > 0.0 ? 100.0 * Math.Max(0.0, su) / cons : 0.0, "0") + "% spozycia)"); }
                else if (c == 1) { if (tc.Raid.Count < Top) tc.Raid.Add(name); }
                else if (tc.None.Count < Top) tc.None.Add(name);
            }
            catch (Exception e) { Stumble("Famine.TownCause", e); }
        }

        private static bool BoundVillageHit(Settlement node)
        {
            var bv = node.BoundVillages;
            if (bv == null) return false;
            foreach (var v in bv)
                if (v != null && v.VillageState != Village.VillageStates.Normal) return true;
            return false;
        }

        /// <summary>Segment linii "Spichlerze (E4/6)" (zawsze, takze z zerami - narzedzie pozna, ze log ma pomiar przyczyny). deadWalls - schronieni i uchodzcy
        /// z najazdu w murach (gina tylko w oblezonej warowni).</summary>
        private static void TownCausePart(StringBuilder sb, TownCauseTally tc, double deadTown, double deadWalls)
        {
            if (tc == null) return;
            double sum = tc.Dead[0] + tc.Dead[1] + tc.Dead[2];
            double ctl = Math.Round(sum - deadTown, 2);
            if (ctl == 0.0) ctl = 0.0;                                           // -0 -> +0 (znak zera jak RT-1)
            sb.Append(" | zgony mieszczan warowni przy Food 0 wedlug przyczyny (f4b): oblezone ").Append(F(tc.Dead[0], "0.##")).Append(" (miast ").Append(tc.N[0])
              .Append("), po najezdzie ").Append(F(tc.Dead[1], "0.##")).Append(" (miast ").Append(tc.N[1])
              .Append("), bez przyczyny ").Append(F(tc.Dead[2], "0.##")).Append(" (miast ").Append(tc.N[2])
              .Append(") = ").Append(F(sum, "0.##")).Append(" (kontrola ").Append(Sg(ctl, "0.##")).Append(')')
              .Append("; schronieni i uchodzcy z najazdu w murach oblezonych ").Append(F(Math.Max(0.0, deadWalls), "0.##"));
            if (tc.Siege.Count > 0) sb.Append("; oblezone: ").Append(string.Join(", ", tc.Siege.ToArray()));
            if (tc.Raid.Count > 0) sb.Append("; po najezdzie: ").Append(string.Join(", ", tc.Raid.ToArray()));
            if (tc.None.Count > 0) sb.Append("; bez przyczyny: ").Append(string.Join(", ", tc.None.ToArray()));
        }
    }
}
