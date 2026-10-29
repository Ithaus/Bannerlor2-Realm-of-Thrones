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
              .Append(" | ").Append(MarchToContact.Rush291Segment())
              .Append('.');
            Log.Info(sb.ToString());
        }
    }
}
