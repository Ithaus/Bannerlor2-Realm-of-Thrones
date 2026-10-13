using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
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
    /// Recenzja A4 (uwaga 1): WSTRZYMUJEMY TYLKO TAM, GDZIE KASA OSADY JEST PRAWDZIWYM PIENIADZEM (PurseReal) - miasto: A1 (TownPurse.On: regulator
    /// kas miast w trybie 1/2 i latki 110 wpiete), zamek: 110 (CastlePurseEnabled i latka regulatora wpieta). Inaczej regulator gry kasuje co dobe
    /// 25% nadwyzki kasy ponad cel (G DefaultSettlementEconomyModel.GetTownGoldChange :75-79; z31-A L:31686 "regulator dzis: skasowal 248065") - ok.
    /// 25 / (25 + 7) = 78% wstrzymanego zlota trafiloby do ujscia "regulator kas" zamiast do pana i korony. Tam osada placi jak dotad, a licznik
    /// "zaplacone mimo oblezenia - kasa pod regulatorem gry" to pokazuje. Wylaczony A1 (albo 110) = automatycznie stan sprzed paczki dla tych osad.
    /// W tym drzewie A1 nie ma - TownPurse to zaslepka (TownPurseA4Stub.cs, On = false): miasta placa jak dotad, wstrzymanie dziala dla zamkow.
    /// "Ile by zaplacila" = kwota policzona tuz przed przelewem ta sama formula i od tego samego progu co wolajacy; polityki licza sie tylko przy
    /// prawdziwym rozliczeniu (applyWithdrawals), nie przy prognozie ekranu rodu.
    /// Wylacznik wylaczony = stan sprzed paczki: oblezone miasto placi rente, a twierdza polityki (licznik "zaplacone mimo oblezenia - wylacznik");
    /// danina, clo, zawor zamku i wsie - jak dotad (bez placenia, poza wylacznikiem).
    /// Recenzja A4 (uwaga 3): KONTROLA NIEZALEZNA OD BRAMKI - co naprawde zeszlo z kas oblezonych osad: renta (PopulationLaw.RentOf), zawor 110
    /// (CastlePurse.DuesOf), danina (KingdomTreasury.TownLevyToday) - odczyt w linii doby (ten sam tick, po nich); polityki - zmiana kasy oblezonych
    /// twierdz krolestwa miedzy PolicyOpen a PolicyClose (poczatek i koniec RulingIncomePostfix, tylko przy applyWithdrawals; nikt inny w tym
    /// czasie kasy nie rusza). To, czego nie wyjasnia licznik bramki (wylacznik / kasa pod regulatorem gry) = "bez wyjasnienia" (prog A4-3: 0) -
    /// wykrywa zgubiona bramke po scaleniu i nowa droge poboru bez Holds.
    /// Scalanie: A1 - usunac TownPurseA4Stub.cs (dwie definicje TownPurse = blad kompilacji CS0101, wiec scalenie nie przejdzie po cichu) i
    /// OBOWIAZKOWO w TownPurse.Valve zastapic bezwarunkowe `if (st.IsUnderSiege) { _vSiege++; return true; }` bramka: najpierw
    /// `int pay = CastlePurse.Dues(...)`, potem `if (BesiegedPay.Holds(st, BesiegedPay.KTownRent, pay, true)) { _vSiege++; return true; }` (klucz
    /// wylaczony = zawor pobiera jak zwykle i licznik "zaplacone mimo oblezenia"). Galaz renty miast w PopulationLaw.Daily jest przy czynnym zaworze A1
    /// pomijana - jej bramka zostaje dla A1 wylaczonego (wtedy przepuszcza i liczy). B2 (podatek wojenny 184 - TownFolk.PayCrown) i B1 (oplaty stale wsi)
    /// wolaja te sama bramke (Holds). E5/B2: rodzaj KFolkWarTax - wstrzymanie przy samym kluczu (placi kiesa ludu, nie kasa osady - zawsze prawdziwy pieniadz),
    /// a w kontroli "co naprawde zeszlo z kas oblezonych" go nie ma (nie schodzi z kasy osady).
    /// Jeff 12.10 (raport nocny, pytanie 4 - jedna zasada): rodzaj KAlley - zaulki (NotableGold, Alley Income From Town: 30 zl/d za zaulek z kasy miasta)
    /// w oblezonym miescie wstrzymane ta sama bramka (przy kasie prawdziwym pieniadzem); w kontroli "co naprawde zeszlo" pozycja zaulki (NoteAlleyOut).
    /// Wyjatek - per wywolanie, licznik potkniec; bramka przy wyjatku przepuszcza (pobor jak dotad), bez globalnego wylacznika.
    /// </summary>
    internal static class BesiegedPay
    {
        internal const int KTownRent = 0, KCrownDuty = 1, KWarTax = 2, KRoadTolls = 3;
        internal const int KFolkDues = 4;   // E5/B1 (173): podatki ludu od zarobku i farma gminy - w oblezonej osadzie zostaja w kiesie / gminie
        internal const int KFolkWarTax = 5;   // E5/B2 (184): podatek wojenny od zarobku ludu - placi kiesa ludu (nie kasa osady), wiec wstrzymanie zawsze przy wlaczonym kluczu (scalenie B: 4 -> 5, K4 zajete przez B1)
        internal const int KAlley = 6;   // Jeff 12.10: zaulki notabli z kasy miasta (NotableGold) - w oblezonym miescie wstrzymane jak inne oplaty
        internal const int KFolkAid = 7;   // P1 (obieg kiesy ludu): danina wojenna z oszczednosci ludu - placi kiesa ludu (jak 184: wstrzymanie przy samym kluczu, poza kontrola kas osad)
        private const int KN = 8;
        private static readonly string[] Names = { "renta miast", "Crown Duty", "War Tax", "Road Tolls", "podatki ludu i farma gminy (E5/B1)", "podatek wojenny ludu (184)", "zaulki", "danina z oszczednosci ludu (P1)" };

        internal static bool On { get { var s = Settings.Current; return s != null && s.BesiegedPayNothing; } }

        // liczniki od poprzedniej linii (renta - nasz blok doby, polityki - dzienny tick rodu krola w grze)
        private static readonly long[] _held = new long[KN], _paid = new long[KN], _reg = new long[KN];
        private static readonly int[] _heldN = new int[KN], _paidN = new int[KN], _regN = new int[KN];
        private static readonly HashSet<Settlement> _heldBy = new HashSet<Settlement>(), _paidBy = new HashSet<Settlement>(), _regBy = new HashSet<Settlement>();
        // kontrola (uwaga 3): zaplacone jawnie przez bramke (wylacznik / kasa pod regulatorem) i polityki, ktore naprawde zeszly z kas oblezonych twierdz
        private static readonly Dictionary<Settlement, long> _expl = new Dictionary<Settlement, long>(), _polOut = new Dictionary<Settlement, long>();
        private static readonly Dictionary<Settlement, long> _alleyOut = new Dictionary<Settlement, long>();   // Jeff 12.10: zaulki, ktore naprawde zeszly z kas oblezonych miast
        private static readonly List<KeyValuePair<Town, int>> _snap = new List<KeyValuePair<Town, int>>();
        private static int _stumbles;
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static bool _startLogged;
        private static int _regHooked = -1;                  // latka regulatora 110 wpieta (raz na proces - latki zaklada menu glowne, przed kampania)

        /// <summary>Konstruktor ArmouryBehavior (nowa kampania i kazde wczytanie).</summary>
        internal static void Reset()
        {
            _startLogged = false;
            _stumbles = 0; _err.Clear();
            ClearDay();
        }

        private static void ClearDay()
        {
            Array.Clear(_held, 0, KN); Array.Clear(_paid, 0, KN); Array.Clear(_reg, 0, KN);
            Array.Clear(_heldN, 0, KN); Array.Clear(_paidN, 0, KN); Array.Clear(_regN, 0, KN);
            _heldBy.Clear(); _paidBy.Clear(); _regBy.Clear();
            _expl.Clear(); _polOut.Clear(); _snap.Clear(); _alleyOut.Clear();
        }

        /// <summary>Kontrola (Jeff 12.10): NotableGold.AlleyPost - zaulki, ktore naprawde zeszly z kasy miasta; liczy sie tylko oblezone. Zloto nie rusza.</summary>
        internal static void NoteAlleyOut(Settlement st, long x)
        {
            try { if (st != null && x > 0 && st.IsUnderSiege) AddTo(_alleyOut, st, x); }
            catch (Exception e) { Stumble("NoteAlleyOut", e); }
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_err.Add(where)) Log.Error("BesiegedPay." + where, e); } catch { }
        }

        /// <summary>
        /// Latka regulatora 110 (CastlePurse.RegulatorPostfix na GetTownGoldChange) wpieta - bez niej regulator gry kasuje nadwyzke kasy zamku mimo
        /// CastlePurseEnabled. Odczyt latek Harmony raz na proces (po scaleniu z A1 rownowazne CastlePurse.HookedModels > 0). Wyjatek - "nie" (pobor jak dotad).
        /// </summary>
        private static bool CastleRegHooked()
        {
            if (_regHooked >= 0) return _regHooked > 0;
            int n = 0;
            try
            {
                var pf = typeof(CastlePurse).GetMethod(nameof(CastlePurse.RegulatorPostfix), BindingFlags.Public | BindingFlags.Static);
                if (pf != null)
                    foreach (var m in Harmony.GetAllPatchedMethods())
                    {
                        var info = m != null ? Harmony.GetPatchInfo(m) : null;
                        if (info == null || info.Postfixes == null) continue;
                        foreach (var p in info.Postfixes) if (p != null && p.PatchMethod == pf) { n++; break; }
                    }
            }
            catch (Exception e) { Stumble("CastleRegHooked", e); }
            _regHooked = n;
            return n > 0;
        }

        /// <summary>
        /// Kasa osady jest prawdziwym pieniadzem (regulator gry nie skasuje tego, co w niej zostanie): miasto - A1 (TownPurse.On; w tym drzewie zaslepka
        /// = false), zamek - 110 (CastlePurseEnabled i latka regulatora). Inne osady - nie (pobor jak dotad; wies oblezona nie bywa).
        /// </summary>
        internal static bool PurseReal(Settlement st)   // E5/A5 (BeeGifts): ta sama regula dla prawdziwej wplaty gracza do kasy osady
        {
            if (st.IsTown) return TownPurse.On;
            if (st.IsCastle) { var s = Settings.Current; return s != null && s.CastlePurseEnabled && CastleRegHooked(); }
            return false;
        }

        /// <summary>
        /// Bramka R22: true = osada jest oblezona, klucz BesiegedPayNothing wlaczony i kasa osady jest prawdziwym pieniadzem (PurseReal) - wolajacy pomija
        /// przelew. `amount` - ile by zaplacila (policzone przez wolajacego, ta sama formula i prog); `count` = prawdziwe rozliczenie (nie prognoza ekranu).
        /// Oblezenie przy wylaczonym kluczu albo przy kasie pod regulatorem gry - false i licznik "zaplacone mimo oblezenia" (z przyczyna; kontrola
        /// uznaje te kwote za wyjasniona). Nieoblezona osada - false bez licznika. Wyjatek - false (pobor jak dotad), licznik potkniec.
        /// </summary>
        internal static bool Holds(Settlement st, int kind, long amount, bool count)
        {
            try
            {
                if (st == null || !st.IsUnderSiege) return false;
                bool on = On;
                bool folk = kind == KFolkWarTax || kind == KFolkAid;        // P1: danina z oszczednosci - ta sama regula co 184 (placi kiesa ludu)
                bool hold = on && (folk || PurseReal(st));   // E5/B2: 184 placi kiesa ludu - prawdziwy pieniadz poza regulatorem gry (bez warunku PurseReal)
                if (count && amount > 0 && kind >= 0 && kind < KN)
                {
                    if (hold) { _held[kind] += amount; _heldN[kind]++; _heldBy.Add(st); }
                    else
                    {
                        if (!on) { _paid[kind] += amount; _paidN[kind]++; _paidBy.Add(st); }
                        else { _reg[kind] += amount; _regN[kind]++; _regBy.Add(st); }
                        if (!folk) { long e0; _expl.TryGetValue(st, out e0); _expl[st] = e0 + amount; }   // E5/B2: kontrola liczy tylko zloto z kas osad - 184 (i danina P1) idzie z kiesy ludu
                    }
                }
                return hold;
            }
            catch (Exception e) { Stumble("Holds", e); return false; }
        }

        /// <summary>
        /// Kontrola (uwaga 3) - poczatek KingdomTreasury.RulingIncomePostfix: kasa kazdej oblezonej twierdzy krolestwa przed poborem polityk (tylko
        /// przy prawdziwym rozliczeniu). Zloto nie rusza.
        /// </summary>
        internal static void PolicyOpen(Kingdom k, bool apply)
        {
            _snap.Clear();
            if (!apply || k == null) return;
            try
            {
                foreach (var f in k.Fiefs)
                {
                    try { if (f != null && f.Settlement != null && f.Settlement.IsUnderSiege) _snap.Add(new KeyValuePair<Town, int>(f, f.Gold)); }
                    catch (Exception e) { Stumble("PolicyOpen(" + (f != null && f.Settlement != null ? f.Settlement.StringId : "?") + ")", e); }
                }
            }
            catch (Exception e) { Stumble("PolicyOpen", e); }
        }

        /// <summary>Kontrola - koniec poboru polityk (przed delta): ile naprawde zeszlo z kas oblezonych twierdz. Zloto nie rusza.</summary>
        internal static void PolicyClose()
        {
            try
            {
                foreach (var kv in _snap)
                {
                    try
                    {
                        int d = kv.Value - kv.Key.Gold;
                        if (d <= 0) continue;
                        var st = kv.Key.Settlement;
                        long o; _polOut.TryGetValue(st, out o); _polOut[st] = o + d;
                    }
                    catch (Exception e) { Stumble("PolicyClose(" + (kv.Key != null && kv.Key.Settlement != null ? kv.Key.Settlement.StringId : "?") + ")", e); }
                }
            }
            catch (Exception e) { Stumble("PolicyClose", e); }
            finally { _snap.Clear(); }
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

        private static void AddTo(Dictionary<Settlement, long> d, Settlement st, long x)
        {
            long o; d.TryGetValue(st, out o); d[st] = o + x;
        }

        // ------------------------------------------------------------ linia (ArmouryBehavior - po liniach etapu 5 za ksiega pieniadza; zloto nie rusza)
        internal static void Daily()
        {
            try
            {
                var s = Settings.Current;
                if (Campaign.Current == null || s == null || !s.LogEnabled) return;
                bool townReal = TownPurse.On, castleReal = s.CastlePurseEnabled && CastleRegHooked();
                if (!_startLogged)
                {
                    _startLogged = true;
                    Log.Info("Oblezone (E5/A4): start | wylacznik Besieged Pay Nothing " + (On ? "tak" : "nie")
                             + " | bramka R22: renta miast (PopulationLaw.Daily; renty Population Rent Enabled " + (PopulationLaw.On ? "tak" : "nie")
                             + "), Crown Duty / War Tax / Road Tolls z kas twierdz (KingdomTreasury.RulingIncomePostfix; Policy Income Conserved "
                             + (s.PolicyIncomeConserved ? "tak" : "nie - polityki z niczego jak w grze, kasy nic nie placa")
                             + "), zaulki z kasy miasta (NotableGold; Alley Income From Town " + (s.AlleyIncomeFromTown ? "tak" : "nie")
                             + ") | wstrzymanie tylko przy kasie prawdziwym pieniadzem (inaczej regulator gry skasowalby wstrzymane - osada placi jak dotad): miasta - kasy miast A1 "
                             + (townReal ? "tak" : "nie") + ", zamki - Castle Purse Enabled " + (s.CastlePurseEnabled ? "tak" : "nie") + ", latka regulatora 110 " + (CastleRegHooked() ? "wpieta" : "BRAK")
                             + " | od dawna, poza wylacznikiem: danina, clo, mennica i monopole (KingdomTreasury.Levies), zawor zamkow (110), wsie w rabunku i spladrowane (renta, danina, Land Tax).");
                }
                int towns = 0, castles = 0, castleN = 0, floorAbove = 0;
                var raided = new HashSet<Settlement>();
                int floorG = (int)Math.Max(0f, s.TownRentFloorGold);
                // kontrola (uwaga 3): co naprawde zeszlo z kas oblezonych osad - renta i zawor 110 (nasz blok doby), danina (Levies), polityki (PolicyClose)
                var outBy = new Dictionary<Settlement, long>();
                long oRent = 0, oDues = 0, oLevy = 0, oPol = 0, oAlley = 0;
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
                        if ((st.IsTown || st.IsCastle) && st.IsUnderSiege && !outBy.ContainsKey(st))
                        {
                            long rp, ra, dp, ds; int lv;
                            PopulationLaw.RentOf(st, out rp, out ra);
                            CastlePurse.DuesOf(st, out dp, out ds);
                            KingdomTreasury.TownLevyToday.TryGetValue(st, out lv);
                            rp = Math.Max(0L, rp); dp = Math.Max(0L, dp); long l = Math.Max(0, lv);
                            oRent += rp; oDues += dp; oLevy += l;
                            outBy[st] = rp + dp + l;
                        }
                    }
                    catch (Exception e) { Stumble("Daily(" + (st != null ? st.StringId : "?") + ")", e); }
                }
                foreach (var kv in _polOut) { oPol += kv.Value; AddTo(outBy, kv.Key, kv.Value); }
                foreach (var kv in _alleyOut) { oAlley += kv.Value; AddTo(outBy, kv.Key, kv.Value); }
                long oSum = 0, explained = 0, unexpl = 0; int oN = 0, unN = 0;
                foreach (var kv in outBy)
                {
                    if (kv.Value <= 0) continue;
                    oSum += kv.Value; oN++;
                    long e; _expl.TryGetValue(kv.Key, out e);
                    long ok = Math.Min(kv.Value, Math.Max(0L, e));
                    explained += ok;
                    if (kv.Value > ok) { unexpl += kv.Value - ok; unN++; }
                }
                var sb = new StringBuilder(1400);
                sb.Append("Oblezone (E5/A4): dzien ").Append((int)CampaignTime.Now.ToDays);
                sb.Append(" | Besieged Pay Nothing: ").Append(On ? "tak" : "nie (oblezone miasto placi rente, twierdza polityki korony - jak dotad)");
                sb.Append(" | kasa prawdziwym pieniadzem (wstrzymanie tylko tam): miasta (A1) ").Append(townReal ? "tak" : "nie").Append(", zamki (110) ").Append(castleReal ? "tak" : "nie");
                sb.Append(" | oblezone teraz: miasta ").Append(towns).Append(", zamki ").Append(castles).Append("; wsie w rabunku lub spladrowane ").Append(raided.Count);
                sb.Append(" | wstrzymane (oblezona osada nie placi - zostaje w jej kasie): ");
                AppendKinds(sb, _held, _heldN, _heldBy.Count);
                sb.Append(" | zaplacone mimo oblezenia - wylacznik wylaczony: ");
                AppendKinds(sb, _paid, _paidN, _paidBy.Count);
                sb.Append(" | zaplacone mimo oblezenia - kasa pod regulatorem gry (miasta bez A1, zamki bez 110): ");
                AppendKinds(sb, _reg, _regN, _regBy.Count);
                sb.Append(" | kontrola (niezalezna od bramki - co naprawde zeszlo z kas oblezonych): renta ").Append(oRent).Append(", zawor 110 ").Append(oDues)
                  .Append(", danina ").Append(oLevy).Append(", polityki ").Append(oPol).Append(", zaulki ").Append(oAlley).Append("; razem ").Append(oSum).Append(" z ").Append(oN).Append(" osad")
                  .Append("; w tym zaplacone jawnie (wyzej) ").Append(explained).Append("; bez wyjasnienia ").Append(unexpl).Append(" z ").Append(unN).Append(" osad (prog 0)");
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
