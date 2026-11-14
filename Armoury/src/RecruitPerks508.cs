using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// 508 (audyt perkow 14.11, punkty 4, 7, 13, 15, 18, 24; decyzja Jeffa "ok"): ZNIZKI WERBUNKU Z PERKOW przy cenie werbunku z dni zoldu.
    ///
    /// Dowod (dekompilacja): gra (DefaultPartyWageModel.GetTroopRecruitmentCost, tw-campaign :233-264) daje kupujacemu (buyerHero, perk
    /// drugorzedny - wodz druzyny) AddFactor(SecondaryBonus): HeadHunter (Throwing) -20% oddzialom tier >= 2 (troop.Tier, kazdy rodzaj);
    /// piechocie (IsInfantry) ChinkInTheArmor (OneHanded), ShowOfStrength (TwoHanded), HardyFrontline (Polearm) po -20%; strzelcom (IsRanged,
    /// "else if" - nie piechocie) RenownedArcher (Bow) -30%, Piercer (Crossbow) -20% (DefaultPerks.cs:1970, 1976, 2004, 2026, 2032, 2065).
    /// BK (BKPartyWageModel.cs:383) zaraz potem stawia LimitMin(zold x 10), a nasz RecruitCost (Postfix) ustawia podstawe na zold x
    /// RecruitCostDays (10) - wynik = cel x (1 + czynniki), przyciety od dolu do zold x 10 = cel. Kazda znizka ponizej celu ginie: perki
    /// nie daja nic (najemnicy - cel x2 - dopiero ponizej -50%).
    ///
    /// Zmiana: gdy RecruitCost liczy cene (HistoricalRecruitCost) i kupujacy ma te perki, dolna granica BK schodzi o te same czynniki
    /// (min x (1 + suma znizek perkow, ten sam warunek co w grze), najmniej 1 - jak LimitMin(1) gry). Bez innych czynnikow wynik = cel x
    /// (1 + perki) - dokladnie jak w grze; inne znizki (kultura -5%, Frugal, ranga klanu) dalej trzyma podloga BK (to nie zakres 508).
    /// Zloto: AI placi mniej i notabl dostaje mniej (LevyGold liczy z tego samego modelu w tej samej chwili), gracz placi notablowi przez BK
    /// mniej - nic z niczego. Koszt: kilka HasPerk na wycene (to samo, co gra robi chwile wczesniej), bez alokacji.
    /// </summary>
    internal static class RecruitPerks508
    {
        private static readonly string[] Names = { "HeadHunter", "ChinkInTheArmor", "ShowOfStrength", "HardyFrontline", "RenownedArcher", "Piercer" };
        // liczniki doby (wyceny, nie zakupy - model wolaja tez okna, AI przy decyzji i LevyGold przy zaplacie)
        private static int _dEval, _dPlayer, _dCut, _dStumbles, _stumbles;
        private static long _dGold;                       // suma (cena z podloga BK - cena po 508) na wycenach, gdzie podloga ciela
        private static readonly int[] _dPerk = new int[6];
        private static bool _started;

        internal static void Reset()
        {
            _dEval = _dPlayer = _dCut = _dStumbles = 0; _dGold = 0;
            Array.Clear(_dPerk, 0, _dPerk.Length);
            _started = false;
        }

        private static PerkObject Perk(int i)
        {
            switch (i)
            {
                case 0: return DefaultPerks.Throwing.HeadHunter;
                case 1: return DefaultPerks.OneHanded.ChinkInTheArmor;
                case 2: return DefaultPerks.TwoHanded.ShowOfStrength;
                case 3: return DefaultPerks.Polearm.HardyFrontline;
                case 4: return DefaultPerks.Bow.RenownedArcher;
                default: return DefaultPerks.Crossbow.Piercer;
            }
        }

        /// <summary>Czy perk i dotyczy tego oddzialu - ten sam warunek co DefaultPartyWageModel (:235-264).</summary>
        private static bool Applies(int i, CharacterObject troop)
        {
            if (i == 0) return troop.Tier >= 2;
            if (i <= 3) return troop.IsInfantry;
            return !troop.IsInfantry && troop.IsRanged;
        }

        /// <summary>Wolane z RecruitCost.Postfix (zewnetrzna wycena, po ustawieniu podstawy na dni zoldu): obniza dolna granice BK o znizki perkow.</summary>
        internal static void Floor(CharacterObject troop, Hero buyer, ref ExplainedNumber result)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.PerkRecruitDiscounts || troop == null || buyer == null) return;
                float min = result.LimitMinValue;
                if (min <= 1f) return;                    // bez podlogi BK (inny model) - gra i tak liczy perki
                float sum = 0f; int mask = 0;
                for (int i = 0; i < 6; i++)
                {
                    if (!Applies(i, troop)) continue;
                    var p = Perk(i);
                    if (p == null || !buyer.GetPerkValue(p)) continue;
                    float b = p.SecondaryBonus;
                    if (b >= 0f) continue;                // tylko znizki
                    sum += b; mask |= 1 << i;
                }
                if (mask == 0) return;
                float before = result.ResultNumber;
                result.LimitMin(Math.Max(1f, min * (1f + sum)));
                _dEval++;
                if (buyer == Hero.MainHero) _dPlayer++;
                for (int i = 0; i < 6; i++) if ((mask & (1 << i)) != 0) _dPerk[i]++;
                float after = result.ResultNumber;
                if (after < before - 0.5f) { _dCut++; _dGold += (long)Math.Round(before - after); }
            }
            catch (Exception e)
            {
                _stumbles++; _dStumbles++;
                if (_stumbles <= 5) Log.Error("RecruitPerks508.Floor", e);   // potkniecie liczone, bez wylacznika
            }
        }

        /// <summary>Linia doby (z RecruitCost.Daily); przy pierwszej dobie sesji linia startu z wartosciami perkow z gry.</summary>
        internal static void Daily()
        {
            var s = Settings.Current;
            if (s == null) return;
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            if (!_started)
            {
                _started = true;
                var parts = new List<string>();
                for (int i = 0; i < 6; i++)
                {
                    PerkObject p = null;
                    try { p = Perk(i); } catch { }
                    parts.Add(Names[i] + " " + (p != null ? (p.SecondaryBonus * 100f).ToString("0", inv) + "%" : "BRAK"));
                }
                Log.Info("Perki werbunku (508): start | klucz PerkRecruitDiscounts " + (s.PerkRecruitDiscounts ? "TAK" : "NIE") + ", HistoricalRecruitCost "
                         + (s.HistoricalRecruitCost ? "TAK" : "NIE (508 nieczynny - podloga BK zostaje)") + " | perki z gry: " + string.Join(", ", parts.ToArray())
                         + " | podloga BK (zold x 10) schodzi o te znizki (HeadHunter tier 2+, trzy pierwsze piechota, dwa ostatnie strzelcy).");
            }
            if (_dEval > 0 || _dStumbles > 0)
            {
                var per = new List<string>();
                for (int i = 0; i < 6; i++) if (_dPerk[i] > 0) per.Add(Names[i] + " " + _dPerk[i]);
                Log.Info("Perki werbunku (508): dzien " + ((int)CampaignTime.Now.ToDays - 1) + " | wyceny z perkiem znizki " + _dEval + " (gracz " + _dPlayer + ")"
                         + ", podloga BK ciela znizke w " + _dCut + " (oddane perkom razem " + _dGold.ToString(inv) + " zlota na wycenach)"
                         + " | per perk: " + (per.Count > 0 ? string.Join(", ", per.ToArray()) : "-") + " | potkniecia doby " + _dStumbles + " (razem " + _stumbles + ").");
            }
            _dEval = _dPlayer = _dCut = _dStumbles = 0; _dGold = 0;
            Array.Clear(_dPerk, 0, _dPerk.Length);
        }
    }
}
