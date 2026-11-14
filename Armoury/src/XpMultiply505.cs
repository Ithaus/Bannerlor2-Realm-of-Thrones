using System;
using System.Globalization;
using TaleWorlds.CampaignSystem;

namespace Armoury
{
    /// <summary>
    /// 505 (perki XP mnoza wage FairXp; Jeff 14.11 "tak, mnozyc"). ExplainedNumber gry: wynik = baza x (1 + suma czynnikow) - czynniki
    /// sie DODAJA. FairXp (FairXp.cs) doklada wage obrazen w bitwie jako czynnik (waga - 1), wiec premie perkow siedzace w tej samej sumie
    /// (PerkXp503 z 503, InspiringLeader RBM, NavalDLC +50% wojsku w bitwie morskiej / NavalFightingTraining towarzyszy, polityka
    /// FraternalFleetDoctrine -15%) licza sie od BAZY RBM, nie od wyniku po wadze: przy zabojstwie (waga ok. 5.3) perk +10% daje ok. +1.9%,
    /// a przy bloku (waga 0.25) ten sam perk daje +40%.
    /// NAPRAWA: w galezi bitwy FairXp czynnik wagi = (1 + suma czynnikow juz w wyniku) x (waga - 1), czyli wynik = baza x (1 + suma) x waga -
    /// waga mnozy calosc, kazdy perk dziala swoim procentem od kazdego ciosu. Arena i turniej bez zmian (tam stawka % i Gladiator BK - decyzja
    /// Jeffa). Klucz CombatXpPerksMultiply (TAK; NIE = dodawanie jak dotad). Bledy liczone (potkniecia) - przy wyjatku czynnik jak dotad.
    /// Linie: "Perki XP x waga (505): start sesji ..." (OnSessionLaunched) i "Perki XP x waga (505): dzien N ..." (doba: XP z ciosow bitwy
    /// wedlug dodawania i wedlug mnozenia z tego samego biegu). Stan tylko liczniki doby, bez zapisu.
    /// </summary>
    internal static class XpMultiply505
    {
        internal const string P = "Perki XP x waga (505): ";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static int _stumbles, _dStumbles;
        private static int _dHits, _dWithBonus;
        private static double _dAdd, _dMul, _dAddBonus, _dMulBonus;

        private static string F1(double v) { return v.ToString("0.#", Inv); }

        private static void ResetDay()
        {
            _dHits = 0; _dWithBonus = 0; _dAdd = 0; _dMul = 0; _dAddBonus = 0; _dMulBonus = 0; _dStumbles = 0;
        }

        /// <summary>
        /// FairXpPatch.Postfix (galaz bitwy): czynnik do AddFactor dla wagi weight (juz przycietej). Klucz NIE albo wyjatek = weight - 1 (jak dotad).
        /// </summary>
        internal static float Factor(ref ExplainedNumber result, float weight)
        {
            float additive = weight - 1f;
            try
            {
                var s = Settings.Current;
                if (s == null || !s.CombatXpPerksMultiply) return additive;
                float sum = result.SumOfFactors;          // perki, InspiringLeader, Naval itd. - wszystko, co juz w wyniku
                if (!(1f + sum > 0f)) return additive;    // wynik <= 0 i tak odrzucony wyzej; ostroznie
                float mul = (1f + sum) * (weight - 1f);

                // tylko log: ten sam cios liczony obiema drogami
                float b = result.BaseNumber;
                double xAdd = b * (1f + sum + additive), xMul = b * (1f + sum) * weight;
                _dHits++; _dAdd += xAdd; _dMul += xMul;
                if (sum > 1e-4f || sum < -1e-4f) { _dWithBonus++; _dAddBonus += xAdd; _dMulBonus += xMul; }
                return mul;
            }
            catch (Exception e)
            {
                _stumbles++; _dStumbles++;
                if (_stumbles <= 3 || _stumbles % 100 == 0) Log.Error("XpMultiply505.Factor (potkniecie " + _stumbles + ")", e);
                return additive;
            }
        }

        /// <summary>OnSessionLaunched: zerowanie licznikow i linia startu.</summary>
        internal static void Start()
        {
            ResetDay();
            var s = Settings.Current;
            Log.Info(P + "start sesji | klucz CombatXpPerksMultiply " + (s != null && s.CombatXpPerksMultiply ? "TAK (waga obrazen mnozy perki)" : "NIE (dodawanie jak dotad)")
                     + " | FairXp " + (s != null && s.CombatXpFixEnabled ? "TAK" : "NIE") + ", waga z obrazen " + (s != null && s.BattleXpScalesWithDamage ? "TAK" : "NIE")
                     + " | dotyczy bitwy i symulacji (arena, turniej bez zmian) | potkniecia " + _stumbles + ".");
        }

        /// <summary>ArmouryBehavior.OnDailyTickBodyInner: linia doby (gdy byly ciosy albo potkniecia TEJ doby) i zerowanie.</summary>
        internal static void Daily(int day)
        {
            try
            {
                if (_dHits > 0 || _dStumbles > 0)
                    Log.Info(P + "dzien " + day + " | wywolania XP za cios z waga (bohater 2x: umiejetnosc i udzial) " + _dHits + ", w tym z premia (perki / Naval / InspiringLeader) " + _dWithBonus
                             + " | XP dodawanie / mnozenie: wszystkie " + F1(_dAdd) + " / " + F1(_dMul)
                             + ", z premia " + F1(_dAddBonus) + " / " + F1(_dMulBonus)
                             + " | potkniecia doby " + _dStumbles + " (razem " + _stumbles + ").");
            }
            catch (Exception e) { _stumbles++; Log.Error("XpMultiply505.Daily", e); }
            ResetDay();
        }
    }
}
