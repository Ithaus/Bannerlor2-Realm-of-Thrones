using System;

namespace Armoury
{
    /// <summary>
    /// KS r4-polnoc (Jeff 17.10, STAN-PRAC ### 72: "Polnoc i za Murem - najpierw wyzywic siebie (hodowla, ryby, owies)"; PRZEKAZANIE-NOC 5A krok 3) - strona gry:
    ///  - klucz Ks Cold Land -> KsTab.ColdLand (jedna tabela swiata z prawdziwymi plonami zimnej ziemi; ustawiane na starcie sesji i co dobe, przed Planem i produkcja okregow);
    ///  - klucze Ks Self Feed First / Ks Self Feed Everywhere -> KsInput.SelfFeed (prog "najpierw wyzywic siebie" w Planie, StartCalibration.Feed.cs);
    ///  - linia "KS: wyzywienie krain" przy kazdym Planie (NKS-30, sprawdz_logi --grupa npolnoc);
    ///  - regula zmiany zajec (A7, klucz Start Calibration Switch Rule - domyslnie wylaczony): przy progu okreg, ktoremu w ostatnim Planie brakowalo kcal dostepu przy
    ///    udzialach rynku (ten sam warunek co prog Planu - KsResult.FeedShort), nie przechodzi z zajecia przy jedzeniu na zajecie tylko na targ (futra, len); okreg
    ///    wyzywiony idzie za cena jak w Planie (recenzja: jedna zasada na jedno zjawisko) - licznik w linii reguly.
    /// Bez nowych danych w zapisie (stan liczy sie z kluczy i Planu).
    /// </summary>
    internal static partial class StartCalibration
    {
        private static int _rFeed;                                        // A7: zmiany z jedzenia na targ wstrzymane progiem (od ostatniej linii reguly)

        /// <summary>Klucz Ks Cold Land do tabeli swiata (zmiana podbija KsTab.LandGen - pamiec przydatnosci ziemi blokow od nowa).</summary>
        private static void SyncColdLand()
        {
            try { var s = Settings.Current; KsTab.ColdLand = s != null && s.KsColdLand; } catch { }
        }

        /// <summary>Zakres progu "najpierw wyzywic siebie" Planu: 0 - wylaczony, 1 - okregi wsi stref zimnych, 2 - wszystkie okregi wsi.</summary>
        private static int SelfFeedMode()
        {
            var s = Settings.Current;
            if (s == null || !s.KsSelfFeedFirst) return 0;
            return s.KsSelfFeedEverywhere ? 2 : 1;
        }

        /// <summary>Prog Planu na samym wlasnym jedzeniu (klucz Ks Self Feed Own Food Only - regula Jeffa doslownie) zamiast na kcal dostepu.</summary>
        private static bool SelfFeedOwnOnly()
        {
            var s = Settings.Current;
            return s != null && s.KsSelfFeedOwnFoodOnly;
        }

        /// <summary>Linia wyzywienia krain z wyniku Planu (tekst z KsCore - ten sam w harnessie).</summary>
        private static void FeedLineLog(KsResult r)
        {
            if (r == null || r.Error != null) return;
            string t = KsCore.FeedLine(r, _wDay);
            if (t != null) Log.Info(t);
        }

        /// <summary>Zajecie daje jedzenie (towar z kcal albo zywiec) - z tabeli swiata.</summary>
        private static bool FoodCraft(int c)
        {
            if (c < 0 || c >= KsTab.NC) return false;
            foreach (var kv in KsTab.P0[c])
            {
                int g = kv.Key;
                if (KsTab.FoodKcal[g] > 0 || g == KsTab.GCow || g == KsTab.GHog || g == KsTab.GSheep || g == KsTab.GWhaleMeat) return true;
            }
            return false;
        }

        /// <summary>A7 przy progu: czesc okregu w zakresie progu nie zamienia zajecia przy jedzeniu na zajecie bez jedzenia (najpierw wyzywic siebie) - tylko okreg, ktoremu
        /// w ostatnim Planie brakowalo kcal dostepu przy udzialach rynku (prog Planu czynny; recenzja: dotad kazdy okreg strefy zimnej, takze wyzywiony). Po wczytaniu, do
        /// pierwszego Planu sesji (co 7 dob) - brak listy, okreg idzie za cena.</summary>
        private static bool FeedKeeps(LiveDist ld, int cur, int best)
        {
            int mode = SelfFeedMode();
            if (mode <= 0 || ld == null || ld.TF || ld.K == null || !(ld.K.People > 0)) return false;
            var pr = _plan; var fs = pr != null ? pr.FeedShort : null;
            if (fs == null || ld.Id == null || !fs.Contains(ld.Id)) return false;
            if (mode == 1)
            {
                var b0 = ld.K.Blocks.Count > 0 ? ld.K.Blocks[0] : null;
                if (b0 == null || b0.O == null || !KsTab.ColdZone(b0.O.Zone)) return false;
            }
            return FoodCraft(cur) && !FoodCraft(best);
        }

        /// <summary>Dopisek linii reguly A7 (liczniki od ostatniej linii).</summary>
        private static string FeedRuleText()
        {
            string t = " | najpierw wyzywic siebie (" + (SelfFeedMode() > 0 ? "TAK" : "WYLACZONE") + "): zmiany z jedzenia na zajecie tylko na targ wstrzymane " + _rFeed;
            _rFeed = 0;
            return t;
        }
    }
}
