using System;

namespace Armoury
{
    /// <summary>
    /// 427 (audyt ekonomii 11.11, A1) - odczyty posiadaczy ksiegi pieniadza dla pomiarow EcoAudit427 i dopisek linii "Pompy". Niczego nie zmienia (same odczyty).
    /// </summary>
    internal static partial class MoneyLedger
    {
        /// <summary>427: gracz i lordowie AI (glowy rodow + pozostali) z konca poprzedniej doby ksiegi - rozbicie pozycji "lordowie" z LastShares. false - brak stanu.</summary>
        internal static bool A427LordsSplit(out long player, out long ai)
        {
            player = ai = 0;
            var h = _lastHold;
            if (h == null || h.Length < Holders) return false;
            player = h[HPlayer]; ai = h[HLeaders] + h[HLords];
            return true;
        }

        /// <summary>427: [lud miast, podzamcza, gminy, wsie, swiat razem] z wektora posiadaczy (null - brak wektora).</summary>
        private static long[] A427Folk(long[] h)
        {
            if (h == null || h.Length < Holders) return null;
            long w = 0; for (int i = 0; i < Holders; i++) w += h[i];
            return new[] { h[HFolkTown], h[HFolkCastle], h[HGmina], h[HVillages], w };
        }

        /// <summary>427: linia "Kiesy ludu - rozbicie zmiany (427)" - wolana z Daily zaraz po "Pompach" (`hold` - koniec tej doby, `last` - koniec poprzedniej).</summary>
        private static void A427FolkLine(int day, long[] hold, long[] last)
        {
            if (!EcoAudit427.On) return;
            try { EcoAudit427.FolkLine(day, A427Folk(hold), A427Folk(last)); }
            catch (Exception e) { EcoAudit427.Stumble("A427FolkLine", e); }
        }

        /// <summary>
        /// 427 A1: dopisek linii "Pompy" - pozycje grupy B (lud -> podatki od oszczednosci), ktorych dotad w niej nie bylo: danina od skarbca wsi DS (Ds Hoard Due
        /// Villages; okno linii "Akumulator kies") i czesc firmy pana z oszczednosci ludu (okno linii "Firmy miast i zamkow (DS)"). POZA dotychczasowym "razem" -
        /// szereg "razem" i "% zoldu" zostaje porownywalny z dawnymi logami; obok suma z nimi. Klucz wylaczony - pusty napis.
        /// </summary>
        private static string A427PumpsTail(long all, long pay, long b)
        {
            if (!EcoAudit427.On) return "";
            try
            {
                long vt = Math.Max(0, TownFolk.A427VillHoardTake), vl = Math.Max(0, TownFolk.A427VillHoardLord), vc = Math.Max(0, TownFolk.A427VillHoardCoffer);
                long lf = Math.Max(0, DsTowns.A427PaidFolk);
                long all2 = all + vt + lf;
                return " | 427 brakujace w B (poza 'razem' wyzej): danina od skarbca wsi DS " + vt + " (do panow " + vl + ", do kas osad " + vc + "), czesc firmy pana z oszczednosci ludu " + lf
                       + "; B z nimi " + (b + vt + lf) + ", razem z nimi " + all2 + " zl = " + (pay > 0 ? (100.0 * all2 / pay).ToString("0.0", Inv) : "-") + "% zoldu";
            }
            catch (Exception e) { EcoAudit427.Stumble("PumpsTail", e); return ""; }
        }
    }
}
