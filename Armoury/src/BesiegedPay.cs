using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// E5/A4 (paczka 196, PLAN 5.4) - OSADA OBLEZONA NIE PLACI NIC (projekt etapu 5 v2, regula R22). Wylacznik BesiegedPayNothing.
    ///
    /// Dzis (baza 392c519): oblezonej osady nie biora danina wojenna, clo, mennica i monopole korony (KingdomTreasury.Levies - `st.IsUnderSiege` :389)
    /// ani zawor zamku (110, CastlePurse.Daily :573); wies w rabunku i spladrowana nie placi renty (PopulationLaw.Daily :1183), daniny (Levies :389) ani
    /// Land Tax (KingdomTreasury.RulingIncomePostfix :494). Placi natomiast: renta miasta panu (PopulationLaw.Daily :1192-1201 - sprawdza tylko stan wsi)
    /// i polityki korony brane z kas twierdz w KingdomTreasury.RulingIncomePostfix - Crown Duty (:476-487, miasta i zamki), War Tax (:502-516, miasta
    /// i zamki), Road Tolls (:518-531, miasta rodu krola). Gra tych polityk w oblezeniu nie wstrzymuje (G DefaultClanFinanceModel.AddRulingClanIncome
    /// :233-319 - bez warunku oblezenia; BKClanFinanceModel wola base). Wies nie bywa oblezona: Settlement.IsUnderSiege = SiegeEvent != null
    /// (G Settlement.cs:458), a oblezenie dotyczy tylko twierdz - "oblezeniem" wsi jest rabunek (od dawna bez placenia).
    ///
    /// Regula R22: oblezona osada nie placi nic - jedna regula dla miasta, zamku i wsi. Ta paczka domyka dwie ostatnie drogi: renta miasta i polityki korony
    /// z kas twierdz. Czego oblezona osada nie zaplaci, zostaje w jej kasie - mniej przelewow, nic nie znika i nic nie powstaje (MoneyLedger bez nowych
    /// pozycji): pan i korona dostaja tego dnia mniej, a kasa oddaje ten pieniadz zaworem i podatkami od nadwyzki po zdjeciu oblezenia, jak zawsze.
    /// Zalezy od A1 (projekt): bez A1 regulator gry kasuje co dobe 25% nadwyzki kasy MIASTA ponad cel (nazwane ujscie "regulator kas") - wtedy wieksza
    /// czesc wstrzymanej renty i polityk miasta zniknelaby tam (ok. 25 / (25 + 7)), zamiast wrocic panu; z A1 (regulator nic nie kasuje) zostaje w obiegu.
    /// Zamki - regulator nie kasuje od 110. Dlatego A4 jedzie tylko w zbiorze A razem z A1.
    /// "Ile by zaplacila" = kwota policzona tuz przed przelewem ta sama formula i od tego samego progu co wolajacy; polityki licza sie tylko przy
    /// prawdziwym rozliczeniu (applyWithdrawals), nie przy prognozie ekranu rodu.
    /// Wylacznik wylaczony = stan sprzed paczki: oblezone miasto placi rente, a twierdza polityki (licznik "zaplacone mimo oblezenia"); danina, clo,
    /// zawor zamku i wsie - jak dotad (bez placenia, poza wylacznikiem).
    /// Scalanie: A1 (TownPurse.Valve) pomija oblezone miasto sama (bezwarunkowo, jak zawor zamku 110), a przy czynnym zaworze A1 galaz renty miast
    /// w PopulationLaw.Daily jest pomijana - bramka renty dziala wtedy tylko przy A1 wylaczonym; B2 (podatek wojenny 184 przy Earn) i B1 (oplaty stale
    /// wsi) wolaja te sama bramke (Holds).
    /// Wyjatek - per wywolanie, licznik potkniec; bramka przy wyjatku przepuszcza (pobor jak dotad), bez globalnego wylacznika.
    /// </summary>
    internal static class BesiegedPay
    {
        internal const int KTownRent = 0, KCrownDuty = 1, KWarTax = 2, KRoadTolls = 3;
        private const int KN = 4;
        private static readonly string[] Names = { "renta miast", "Crown Duty", "War Tax", "Road Tolls" };

        internal static bool On { get { var s = Settings.Current; return s != null && s.BesiegedPayNothing; } }

        // liczniki od poprzedniej linii (renta - nasz blok doby, polityki - dzienny tick rodu krola w grze)
        private static readonly long[] _held = new long[KN], _paid = new long[KN];
        private static readonly int[] _heldN = new int[KN], _paidN = new int[KN];
        private static readonly HashSet<Settlement> _heldBy = new HashSet<Settlement>(), _paidBy = new HashSet<Settlement>();
        private static int _stumbles;
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static bool _startLogged;

        /// <summary>Konstruktor ArmouryBehavior (nowa kampania i kazde wczytanie).</summary>
        internal static void Reset()
        {
            _startLogged = false;
            _stumbles = 0; _err.Clear();
            ClearDay();
        }

        private static void ClearDay()
        {
            Array.Clear(_held, 0, KN); Array.Clear(_paid, 0, KN);
            Array.Clear(_heldN, 0, KN); Array.Clear(_paidN, 0, KN);
            _heldBy.Clear(); _paidBy.Clear();
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_err.Add(where)) Log.Error("BesiegedPay." + where, e); } catch { }
        }

        /// <summary>
        /// Bramka R22: true = osada jest oblezona i przy wlaczonym BesiegedPayNothing nic nie placi (wolajacy pomija przelew). `amount` - ile by zaplacila
        /// (policzone przez wolajacego, ta sama formula i prog); `count` = prawdziwe rozliczenie (nie prognoza ekranu). Oblezenie przy wylaczonym kluczu -
        /// false i licznik "zaplacone mimo oblezenia". Nieoblezona osada - false bez licznika. Wyjatek - false (pobor jak dotad), licznik potkniec.
        /// </summary>
        internal static bool Holds(Settlement st, int kind, long amount, bool count)
        {
            try
            {
                if (st == null || !st.IsUnderSiege) return false;
                bool on = On;
                if (count && amount > 0 && kind >= 0 && kind < KN)
                {
                    if (on) { _held[kind] += amount; _heldN[kind]++; _heldBy.Add(st); }
                    else { _paid[kind] += amount; _paidN[kind]++; _paidBy.Add(st); }
                }
                return on;
            }
            catch (Exception e) { Stumble("Holds", e); return false; }
        }

        private static void AppendKinds(StringBuilder sb, long[] gold, int[] n, int places)
        {
            long sum = 0;
            for (int i = 0; i < KN; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(Names[i]).Append(' ').Append(gold[i]).Append(" (").Append(n[i]).Append(')');
                sum += gold[i];
            }
            sb.Append("; razem ").Append(sum).Append(" z ").Append(places).Append(" osad");
        }

        // ------------------------------------------------------------ linia (ArmouryBehavior - po liniach etapu 5 za ksiega pieniadza; zloto nie rusza)
        internal static void Daily()
        {
            try
            {
                var s = Settings.Current;
                if (Campaign.Current == null || s == null || !s.LogEnabled) return;
                if (!_startLogged)
                {
                    _startLogged = true;
                    Log.Info("Oblezone (E5/A4): start | wylacznik Besieged Pay Nothing " + (On ? "tak" : "nie")
                             + " | bramka R22: renta miast (PopulationLaw.Daily; renty Population Rent Enabled " + (PopulationLaw.On ? "tak" : "nie")
                             + "), Crown Duty / War Tax / Road Tolls z kas twierdz (KingdomTreasury.RulingIncomePostfix; Policy Income Conserved "
                             + (s.PolicyIncomeConserved ? "tak" : "nie - polityki z niczego jak w grze, kasy nic nie placa")
                             + ") | od dawna, poza wylacznikiem: danina, clo, mennica i monopole (KingdomTreasury.Levies), zawor zamkow (110), wsie w rabunku i spladrowane (renta, danina, Land Tax).");
                }
                int towns = 0, castles = 0, castleN = 0, floorAbove = 0;
                var raided = new HashSet<Settlement>();
                int floorG = (int)Math.Max(0f, s.TownRentFloorGold);
                foreach (var st in Settlement.All)
                {
                    try
                    {
                        if (st == null) continue;
                        if (st.IsTown) { if (st.IsUnderSiege) towns++; }
                        else if (st.IsCastle)
                        {
                            castleN++;
                            if (st.IsUnderSiege) castles++;
                            // prog podatkow korony zamku (B-4: CastlePurse.PayFloor = max(prog miasta, zapas kupcow)) ponad zapasem kupcow (D4 zamku: Reserve) - tylko odczyt
                            if (st.Town != null && CastlePurse.PayFloor(st.Town, floorG) > CastlePurse.Reserve(st.Town)) floorAbove++;
                        }
                        else if (st.IsVillage && st.Village != null
                                 && (st.Village.VillageState == Village.VillageStates.BeingRaided || st.Village.VillageState == Village.VillageStates.Looted))
                            raided.Add(st);                  // ROT podaje w Settlement.All dwie wsie dwa razy - zbior
                    }
                    catch (Exception e) { Stumble("Daily(" + (st != null ? st.StringId : "?") + ")", e); }
                }
                var sb = new StringBuilder(900);
                sb.Append("Oblezone (E5/A4): dzien ").Append((int)CampaignTime.Now.ToDays);
                sb.Append(" | Besieged Pay Nothing: ").Append(On ? "tak" : "nie (oblezone miasto placi rente, twierdza polityki korony - jak dotad)");
                sb.Append(" | oblezone teraz: miasta ").Append(towns).Append(", zamki ").Append(castles).Append("; wsie w rabunku lub spladrowane ").Append(raided.Count);
                sb.Append(" | wstrzymane (oblezona osada nie placi - zostaje w jej kasie): ");
                AppendKinds(sb, _held, _heldN, _heldBy.Count);
                sb.Append(" | zaplacone mimo oblezenia (prog 0 przy wlaczonym): ");
                AppendKinds(sb, _paid, _paidN, _paidBy.Count);
                sb.Append(" | od dawna (poza wylacznikiem): danina i clo (Levies), zawor zamkow (110), wsie w rabunku i spladrowane - nie placa");
                sb.Append(" | prog podatkow korony zamkow (B-4 max(").Append(floorG).Append(", zapas kupcow)) ponad zapasem kupcow (D4): ").Append(floorAbove).Append(" z ").Append(castleN).Append(" zamkow");
                if (_stumbles > 0) sb.Append(" | potkniecia ").Append(_stumbles).Append(" (pierwszy wyjatek z miejsca w logu jako ERROR)");
                sb.Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally { ClearDay(); }
        }
    }
}
