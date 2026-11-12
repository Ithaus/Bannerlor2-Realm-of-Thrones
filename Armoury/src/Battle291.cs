using System;
using System.Text;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 291 (29.10, Jeff po grze na 136: "Bitwa nadal AI zachowuje sie srednio ..."): linia "Bitwa (291): misja ..." raz na bitwe polowa - trzy zasady w jednym
    /// miejscu dla autotestu i gry Jeffa (sprawdz_logi --grupa bitwa291): A armia idzie dalej mimo nekajacego (HorsePursuit.ReactHalt, klucz ArmyMarchesPastHarassers),
    /// B obronca trzyma pozycje (Doctrine.Hold291, DefendersHoldTheirGround), C piechota bez tarcz biegnie pod ostrzalem (MarchToContact, UnshieldedRushUnderFire).
    /// Begin - BattleSense.AfterStart (przed Begin paczek, liczniki B i C od zera; A zeruje HorsePursuit.Begin); Write - HorsePursuit.End (po MarchToContact.End
    /// i Doctrine.End w BattleSense.Clean, przed czyszczeniem stanu HorsePursuit), tylko bitwa polowa. Glowny watek.
    /// </summary>
    internal static class Battle291
    {
        internal static void Begin()
        {
            Doctrine.Dh291Reset();
            MarchToContact.Rush291Reset();
            OutshotRush.Reset();   // 313b: segment E
            Doctrine.O438Reset();   // 438: linia "Ostrzal piechoty (438)"
            Doctrine.H439Reset();   // 439: linia "Obronca trzyma (439)"
            Doctrine.P440Reset();   // 440: linia "Wodz AI (440)"
            Doctrine.C441Reset();   // 441: linia "Jazda AI (441)"
            Doctrine.T442Reset();   // 442: linia "Cel szarzy (442)"
            Doctrine.R443Reset();   // 443: linia "Rajd i ostrzal (443)"
            Doctrine.K444Reset();   // 444: linia "Kowadlo i mlot (444)"
            Doctrine.V445Reset();   // 445: linia "Jazda na jazde i oderwanie (445)"
            Doctrine.Q446Reset();   // 446: linia "Czworobok (446)"
        }

        internal static void Write(Mission m, string kind, string segA)
        {
            var c = Settings.Current;
            var sb = new StringBuilder(900);
            sb.Append("Bitwa (291): misja ").Append(kind ?? "-")
              .Append(" | klucze: armia idzie dalej ").Append(c != null && c.ArmyMarchesPastHarassers ? "TAK" : "NIE")
              .Append(", obronca trzyma ").Append(c != null && c.DefendersHoldTheirGround ? "TAK" : "NIE")
              .Append(", bieg bez tarcz ").Append(c != null && c.UnshieldedRushUnderFire ? "TAK" : "NIE")
              .Append(" | ").Append(segA ?? "A -")
              .Append(" | ").Append(Doctrine.Dh291Segment())
              .Append(" | ").Append(OutshotRush.Segment())   // 313b: 291e bilans ognia i bieg z tarczami (miedzy B i C - wyrazenia B i C bitwa291 bez zmian)
              .Append(" | ").Append(OutshotRush.Segment320())   // 320d: segment F (po E - wyrazenie E konczy sie na 'potkniecia N |')
              .Append(" | ").Append(MarchToContact.Rush291Segment())
              .Append('.');
            Log.Info(sb.ToString());
            Log.Info(Doctrine.O438Line(kind));   // 438: osobna linia (wyrazenia sprawdz_logi dla 'Bitwa (291)' bez zmian)
            Log.Info(Doctrine.H439Line(kind));   // 439: osobna linia
            Log.Info(Doctrine.P440Line(kind));   // 440: osobna linia
            Log.Info(Doctrine.C441Line(kind));   // 441: osobna linia
            Log.Info(Doctrine.T442Line(kind));   // 442: osobna linia
            Log.Info(Doctrine.R443Line(kind));   // 443: osobna linia
            Log.Info(Doctrine.K444Line(kind));   // 444: osobna linia
            Log.Info(Doctrine.V445Line(kind));   // 445: osobna linia
            Log.Info(Doctrine.Q446Line(kind));   // 446: osobna linia
        }
    }
}
