using System;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 457 (odwod piechoty AI w szyku 8, InfantryReserve457): kompanie nie widza szyku 8 (podzial i przeglad 3 s pomijaja szyki >= 8, BannerCompanies.cs:316 /
    /// 353 / 927), wiec czlowiek przelozony do odwodu bylby dalej czlonkiem starej kompanii - lamalby sie z nia i dostawal jej wstrzasy 50 m za linia.
    /// Free457 (odczyt, wybor ludzi - przy kluczu TAK i NIE ten sam): do odwodu nie idzie chorazy, biegacz po sztandar, dowodca kompanii ani czlonek kompanii
    /// zlamanej / w falach zalamania. Detach457 (tylko klucz TAK, tuz przed przelozeniem): wypisanie z kompanii (Men, _mem, Peak - 1 jak przy przeniesieniu w Put).
    /// Po wyjsciu odwodu do szyku 0-7 przeglad 3 s zapisuje go do kompanii tego szyku (Joined) - zwykla droga posilkow. Glowny watek.
    /// </summary>
    internal static partial class BannerCompanies
    {
        /// <summary>true = czlowiek moze isc do odwodu (bez misji choragwi albo poza kompaniami - true).</summary>
        internal static bool Free457(Agent a)
        {
            var k = RunLive;
            if (k == null || a == null || !ReferenceEquals(k.Mission, a.Mission)) return true;
            try { return k.Free457(a); }
            catch (Exception e) { Stumble("BannerCompaniesMission.Free457", e); return false; }
        }

        /// <summary>1 = wypisany z kompanii, 0 = nie byl w kompanii, -1 = odmowa (patrz Free457). keepShare (459-p, zmiana linii w trakcie bitwy):
        /// Peak zmniejszony tak, by udzial strat kompanii nie wzrosl (patrz BannerCompaniesMission.Detach457).</summary>
        internal static int Detach457(Agent a, bool keepShare = false)
        {
            var k = RunLive;
            if (k == null || a == null || !ReferenceEquals(k.Mission, a.Mission)) return 0;
            try { return k.Detach457(a, keepShare); }
            catch (Exception e) { Stumble("BannerCompaniesMission.Detach457", e); return -1; }
        }
    }

    internal sealed partial class BannerCompaniesMission
    {
        internal bool Free457(Agent a)
        {
            if (_bearerOf.ContainsKey(a) || _runners.ContainsKey(a)) return false;
            Company co;
            if (!_mem.TryGetValue(a, out co)) return true;
            if (co.Broken || co.Queue != null) return false;
            if (ReferenceEquals(co.Bearer, a)) return false;
            if (ReferenceEquals(co.Commander, a) && !co.CmdDown) return false;
            return true;
        }

        internal int Detach457(Agent a, bool keepShare)
        {
            if (!Free457(a)) return -1;
            Company co;
            if (!_mem.TryGetValue(a, out co)) return 0;
            // 459-p (recenzja 459): zywy wypisany z kompanii ze stratami (r w szeregu < Peak) - Peak - 1 podnosilby udzial strat 1 - r/Peak ((r-1)/(P-1) < r/P),
            // choc nikt nie padl (Peak 20, w szeregu 14 -> 5 wypisanych: 0.30 -> 0.40, krok 1 -> 2, rzut zlamania). Przy keepShare Peak' = floor(P x (r-1) / r)
            // na tej samej mierze "w szeregu" co rzut strat (BannerBreakEarly: InRanksLive, inaczej InRanks) - udzial nie rosnie; przy r == P to dokladnie P - 1.
            int peak = co.Peak > 0 ? co.Peak - 1 : 0;
            if (keepShare && co.Peak > 0)
            {
                var c = Settings.Current;
                int r = c != null && c.BannerBreakEarly ? InRanksLive(co, false) : InRanks(co);
                if (r > 0 && r < co.Peak) peak = (int)((long)co.Peak * (r - 1) / r);
            }
            co.Men.Remove(a);
            co.Peak = peak;
            _mem.Remove(a);
            return 1;
        }
    }
}
