using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// STRAZ LICZNIKA PODATKU OSADY (Village/Town.TradeTaxAccumulated, int gry) - przepelnienie o 2^31.
    ///
    /// Przyczyna (BK, nie nasz kod): BannerKings EstateData.AccumulateTradeTax, wolane tylko z BK VillagerSettlementEnterPatch (powrot taboru
    /// do wsi). Dla kazdego majatku: f = podatek x GetEstateWorkforceProportion x (1 - stawka). Proporcja = (ludzie + niewolnicy majatku) /
    /// LandData.AvailableWorkForce, przyciete MathF.Clamp(0..1) - ale 0/0 daje NaN, a Clamp przepuszcza NaN. MBRandom.RoundRandomized(NaN) =
    /// (int)NaN = int.MinValue, wiec suma udzialow majatkow num dostaje +int.MinValue, a licznik wsi dostaje "podatek - num" = prawdziwa kwota
    /// +-2^31 (mod 2^32). Kazdy powrot taboru takiej wsi (takze z pusta sakwa - 0 x NaN = NaN) przestawia licznik o 2^31 w gore albo w dol:
    /// raz ok. +2.147 mld, raz ok. -2.147 mld (log WGRANIA 50 d699-725 i WGRANIA 43 d287-292, wies Tanos). Zloto majatkow nie skacze
    /// (BK wyplaca tylko dodatnie num2).
    ///
    /// Straz: wartosc ujemna albo ponad 100 mln u jednej osady = blad. Szkoda z tej przyczyny to zawsze dokladnie 2^31 (mod 2^32), wiec
    /// odwrocenie bitu znaku oddaje prawdziwy licznik (podatek z sakwy taboru minus udzialy majatkow) - bierzemy go, jesli jest w granicach
    /// 0..1 mln (zwykly licznik wsi to setki denarow); inaczej licznik idzie do 0 (to nie jest prawdziwe zloto - nikt go nie wplacil).
    /// Dwa miejsca: (1) postfiks Priority.First na VillagerCampaignBehavior.OnSettlementEntered - zaraz po latce BK, przed naszymi pomiarami
    /// (112, ksiega pieniadza) i przed jakakolwiek wyplata; (2) przeglad wszystkich wsi i miast raz na dobe przed "Pieniadz swiata";
    /// (3, recenzja) BeforeSettlementEntered (z MoneyLedger.OnBeforeEntered) - licznik bledny juz przed oknem ksiegi naprawiony przed odczytem.
    /// Wyjatek przy jednej osadzie: liczony, nie gasi strazy. Bez pol zapisu (liczniki tylko na sesje).
    /// </summary>
    internal static class TaxCounterGuard
    {
        internal const int Max = 100000000;          // ponad to u jednej osady = blad
        private const int FlipMax = 1000000;         // odwrocony licznik przyjmujemy tylko do 1 mln, inaczej 0
        private const int LogCap = 40;               // pelne linie logu na sesje; potem co setna

        private static int _trips, _flipped, _zeroed, _stumbles;
        // recenzja: doba - liczba napraw i korekta licznikow (now - was); linia dobowa w Sweep, poza bilansem "Pieniadz swiata"
        private static int _dayN, _dayFlip, _dayZero;
        private static long _dayFix;

        internal static int Trips { get { return _trips; } }

        /// <summary>Nowa sesja (konstruktor ArmouryBehavior): liczniki potkniec od zera.</summary>
        internal static void Reset() { _trips = _flipped = _zeroed = _stumbles = 0; _dayN = _dayFlip = _dayZero = 0; _dayFix = 0; BkEstateShare307.Reset(); }   // 307c

        internal static bool Bad(int v) { return v < 0 || v > Max; }

        /// <summary>Wartosc po naprawie: bez zmian, gdy w granicach; odwrocony bit znaku, gdy wynik 0..1 mln; inaczej 0.</summary>
        internal static int Repaired(int v, out bool flipped)
        {
            flipped = false;
            if (!Bad(v)) return v;
            int f = v ^ int.MinValue;
            if (f >= 0 && f <= FlipMax) { flipped = true; return f; }
            return 0;
        }

        private static void Note(string where, Settlement st, bool village, int was, int now, bool flipped)
        {
            _trips++;
            if (flipped) _flipped++; else _zeroed++;
            _dayN++; if (flipped) _dayFlip++; else _dayZero++;
            _dayFix += (long)now - was;
            if (_trips > LogCap && _trips % 100 != 0) return;
            string name = "?";
            try { name = st != null && st.Name != null ? st.Name.ToString() : "?"; } catch { }
            Log.Info("Licznik podatku osady: przepelnienie (" + where + ") | " + (village ? "wies " : "miasto/zamek ") + name + " | wartosc " + was
                     + " -> " + now + (flipped ? " (odwrocony bit znaku 2^31 - szkoda z majatku BK z proporcja NaN)" : " (zero)")
                     + " | potkniecia w sesji " + _trips + " (odwrocone " + _flipped + ", zerowane " + _zeroed + ", wyjatki " + _stumbles + ")"
                     + (_trips == LogCap ? "; dalej co setna linia" : "") + ".");
        }

        internal static void FixVillage(Village v, string where)
        {
            if (v == null) return;
            try
            {
                int was = v.TradeTaxAccumulated;
                if (!Bad(was)) return;
                bool flipped;
                int now = Repaired(was, out flipped);
                v.TradeTaxAccumulated = now;
                Note(where, v.Settlement, true, was, now, flipped);
            }
            catch (Exception e) { _stumbles++; if (_stumbles == 1) Log.Error("TaxCounterGuard.FixVillage", e); }
        }

        internal static void FixTown(Town t, string where)
        {
            if (t == null) return;
            try
            {
                int was = t.TradeTaxAccumulated;
                if (!Bad(was)) return;
                bool flipped;
                int now = Repaired(was, out flipped);
                t.TradeTaxAccumulated = now;
                Note(where, t.Settlement, false, was, now, flipped);
            }
            catch (Exception e) { _stumbles++; if (_stumbles == 1) Log.Error("TaxCounterGuard.FixTown", e); }
        }

        /// <summary>Postfiks Priority.First na VillagerCampaignBehavior.OnSettlementEntered: zaraz po latce BK (prefiks zwraca false, postfiksy
        /// biegna dalej) - wies wejscia (tam BK dopisuje) i wies macierzysta taboru. Zawsze, niezaleznie od klucza 112.</summary>
        public static void EnterGuardPostfix(MobileParty __0, Settlement __1)
        {
            try
            {
                if (__0 == null || __1 == null || !__0.IsVillager || !__1.IsVillage) return;
                var c = Campaign.Current;
                if (c == null || !c.GameStarted) return;
                FixVillage(__1.Village, "powrot taboru");
                var hs = __0.HomeSettlement;
                if (hs != null && !ReferenceEquals(hs, __1)) FixVillage(hs.Village, "powrot taboru - wies macierzysta");
            }
            catch (Exception e) { _stumbles++; if (_stumbles == 1) Log.Error("TaxCounterGuard.EnterGuardPostfix", e); }
        }

        /// <summary>Recenzja: BeforeSettlementEntered (pierwsza instrukcja MoneyLedger.OnBeforeEntered) - naprawa PRZED otwarciem okien ksiegi
        /// i 112. Bledny licznik sprzed okna (np. wczytany zapis z dawnym skokiem) bylby inaczej zmierzony w oknie jako "utarg zniknal" / "nadwyzka"
        /// rzedu 2 mld (ujscie zlota, ktorego nie bylo). Po pierwszej dobie Sweep i postfiks nie zostawiaja blednych licznikow - tu zwykle nic.</summary>
        internal static void BeforeEnter(MobileParty mp, Settlement st)
        {
            try
            {
                if (mp == null || st == null || !mp.IsVillager || !st.IsVillage) return;
                FixVillage(st.Village, "przed powrotem taboru");
                var hs = mp.HomeSettlement;
                if (hs != null && !ReferenceEquals(hs, st)) FixVillage(hs.Village, "przed powrotem taboru - wies macierzysta");
            }
            catch (Exception e) { _stumbles++; if (_stumbles == 1) Log.Error("TaxCounterGuard.BeforeEnter", e); }
        }

        /// <summary>Raz na dobe, przed migawka "Pieniadz swiata": wszystkie wsie i miasta/zamki; potem jedna linia doby, gdy byly naprawy.</summary>
        internal static void Sweep()
        {
            if (Campaign.Current == null) return;
            foreach (var v in Village.All) FixVillage(v, "przeglad doby");
            foreach (var t in Town.AllFiefs) FixTown(t, "przeglad doby");
            if (_dayN > 0)
            {
                // korekta licznika, nie ujscie ani zrodlo zlota: okna ksiegi (utarg wsi, 112) i migawki "Pieniadz swiata" widza juz naprawione
                // liczniki, wiec falszywe +-2^31 nie wchodzi do bilansu; ta linia tylko pokazuje, ile falszu cofnieto
                Log.Info("Licznik podatku osady: doba | napraw " + _dayN + " (odwrocone " + _dayFlip + ", zerowane " + _dayZero + ") | korekta licznikow "
                         + (_dayFix >= 0 ? "+" : "") + _dayFix + " - falszywe zloto spoza obiegu (nikt go nie wplacil), poza bilansem 'Pieniadz swiata': nie jest ujsciem ani zrodlem"
                         + " | potkniecia w sesji " + _trips + ", wyjatki " + _stumbles + ".");
                _dayN = _dayFlip = _dayZero = 0; _dayFix = 0;
            }
        }
    }
}
