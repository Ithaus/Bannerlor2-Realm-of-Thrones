using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// PACZKA 139b - KARAWANA HANDLUJE UMIEJETNOSCIA DOWODCY (Jeff 30.10 ok. 01:20: "I czy jak towarzysz kotremu daje karawane ma duzo trade to lepiej handluje
    /// czy nie ma to znaczenia?" - po odpowiedzi "prawie bez znaczenia" i propozycji: "tak zrob").
    ///
    /// Sprawdzone w dekompilacji 1.4.8 (DefaultTradeItemPriceFactorModel.GetTradePenalty): karawana nie dostaje czlonu 1.5 + 0.25 za tier, num = 0.06
    /// [+0.5 wojna, wies +1 / +0.1, kupiec-karawana +1 / +0.1] x 0.5 (karawana) x tf, tf = PartyTradeModel.GetTradePenaltyFactor = 1 / (1 + 0.002 x Handel
    /// przywodcy; bez bohatera MemberRoster[0] - mistrz karawany ROT bez Handlu, tf = 1), potem perki AddFactor, BK x5 (bron, zbroje, siodla), BKROT -0.1.
    /// Wynik lancucha jest liniowy w tf. Bohatera-dowodce maja tylko karawany gracza z rozmowy z towarzyszem (gra, BK i BK wlasnosci lordow tworza karawany
    /// z caravanLeader: null) - wiec AI = 138 dokladnie.
    ///
    /// (1) CENY (klucz CaravanLeaderHaggle): ta sama miara targu co gracz w 300b (PlayerHaggle.Terms: T = Handel, P = Persuasion.Measure wobec kultury kupca,
    ///     H = 1 / (1 / tfT + E(P))). Dowodca targuje sie jak gracz przy tym samym S (podstawa partii x H), a karawana zachowuje swoj staly upust - polowe
    ///     podstawy, ktora karawana z mistrzem bez Handlu dostaje w 138:
    ///       kara = podstawa x H - 0.5 x podstawa = wynik lancucha x max(0, 2H - 1) / tfT
    ///     S = 0 - dokladnie 138; S >= 500 - kara 0 (kupno = odsprzedaz w tym samym miescie przy tej samej polce; wiecej niz kara dawaloby petle "kup i odsprzedaj
    ///     tu", bo czynnik bazowy kupna i odkupu tej samej sztuki jest ten sam). Czynnik jest w [0, 1] (H <= tfT <= 1), wiec kara nigdy nie rosnie.
    ///     Wojna, wies, perki, BK x5, x0.95 BK i BKROT zostaja jako mnozniki lancucha. Karawana bez bohatera - wczesne wyjscie, zero zmian.
    ///     Wpiete w postfiks 300b (PlayerHaggle.Postfix) - ta sama latka na kazdym modelu cen i ten sam licznik zagniezdzenia.
    /// (2) OCENA (klucz CaravanLeaderJudgement): SkillJudge(mp) = 1 - tf (sam Handel - ocena rynku to wiedza, nie urok); bez dowodcy 0. W 139b nikt jej nie czyta -
    ///     to pomocnik dla rundy 139 (czekanie na rynek, trasa BK).
    /// (3) XP HANDLU (klucz CaravanLeaderTradeXp): regula gracza (TradeSkillCampaignBehavior: XP = 0.5 x zysk sprzedazy ponad koszt zakupu) przy kazdej sprzedazy
    ///     karawany z dowodca miastu (CaravanGoods.TradePostfix, ksiega kosztu E3-T1); XP z wyplat nadwyzki (259, E3/9, wyplata gry, wplata BK) dla tego dowodcy
    ///     pominiete (prefiks na OnTradeProfitMade(Hero) - ten sam zysk nie liczy sie dwa razy).
    /// Wyjatek per wywolanie - licznik potkniec, nic nie gasimy. Reset w konstruktorze ArmouryBehavior.
    /// </summary>
    internal static class CaravanLeader139b
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        internal const float XpPerProfit = 0.5f;                  // DefaultSkillLevelingManager.OnTradeProfitMade: XP = 0.5 x zysk
        private static readonly int[] CurveS = { 0, 50, 150, 300, 500, 700 };
        private static readonly int[] CurveT = { 0, 50, 150, 300 };
        private static readonly int[] TestS = { 0, 150, 300, 700 };

        [ThreadStatic] private static int _probe1;   // sonda: S + 1 na karawanie bez dowodcy (0 = brak sondy)
        [ThreadStatic] private static bool _quiet;   // wycena do logu (NoteSale / NoteBuy) - bez licznikow wywolan
        [ThreadStatic] internal static bool Off;     // samotest: jak przy kluczach wylaczonych

        private static readonly object _lock = new object();
        // doba
        private static long _c1, _c1c, _c0;
        private static int _sellQ, _buyQ, _xpSales, _noCost, _payR;
        private static long _sellG, _buyB, _profitZ, _profitZk, _payA;
        private static double _sellG0, _buyB0, _maxOld;
        private static readonly Dictionary<Hero, double> _xpHero = new Dictionary<Hero, double>();
        private static int _dayErrors, _errors;
        // sesja
        private static readonly List<string> _xpHooks = new List<string>();
        private static bool _xpOk;

        internal static bool On { get { if (Off) return false; var s = Settings.Current; return s != null && s.CaravanLeaderHaggle; } }
        internal static bool XpOn { get { if (Off) return false; var s = Settings.Current; return s != null && s.CaravanLeaderTradeXp; } }
        internal static bool JudgeOn { get { if (Off) return false; var s = Settings.Current; return s != null && s.CaravanLeaderJudgement; } }

        internal static void Reset()
        {
            lock (_lock) { DayZero(); _errors = 0; }
        }

        private static void DayZero()
        {
            _c1 = _c1c = _c0 = 0; _sellQ = _buyQ = _xpSales = _noCost = _payR = 0;
            _sellG = _buyB = _profitZ = _profitZk = _payA = 0; _sellG0 = _buyB0 = _maxOld = 0.0;
            _xpHero.Clear(); _dayErrors = 0;
        }

        internal static void Stumble(string where, Exception e)
        {
            _dayErrors++;
            if (++_errors <= 5 || _errors % 100 == 0) Log.Error("CaravanLeader139b." + where + " (potkniecie " + _errors + ")", e);
        }

        private static float E(int points)
        {
            var se = DefaultSkillEffects.TradePenaltyReduction;
            return se != null ? se.GetSkillEffectValue(points) : 0.002f * points;
        }

        /// <summary>Czynnik kary karawany z dowodca: max(0, 2H - 1) / tfT (w [0, 1]).</summary>
        internal static float FactorOf(float h, float tf)
        {
            if (float.IsNaN(h) || float.IsNaN(tf) || tf <= 0f) return 1f;
            float f = Math.Max(0f, 2f * h - 1f) / tf;
            return f < 0f ? 0f : f > 1f ? 1f : f;
        }

        internal static float Factor(PlayerHaggle.Terms t) { return t == null ? 1f : FactorOf(t.H, t.TfT); }

        /// <summary>Czynnik umiejetnosci gry dla partii (PartyTradeModel.GetTradePenaltyFactor); blad albo poza (0, 1] - 1.</summary>
        internal static float TfOf(MobileParty mp)
        {
            try
            {
                var m = Campaign.Current != null ? Campaign.Current.Models : null;
                float tf = m != null && m.PartyTradeModel != null && mp != null ? m.PartyTradeModel.GetTradePenaltyFactor(mp) : 1f;
                return float.IsNaN(tf) || float.IsInfinity(tf) || tf <= 0f || tf > 1f ? 1f : tf;
            }
            catch { return 1f; }
        }

        /// <summary>Kultura do perswazji: osada kupca (jak 300b), a bez kupca (wyceny wewnetrzne karawan) - osada, w ktorej karawana stoi.</summary>
        internal static CultureObject CultureFor(MobileParty mp, PartyBase merchant)
        {
            var c = PlayerHaggle.CultureOf(merchant);
            if (c == null && mp != null && mp.CurrentSettlement != null) { try { c = mp.CurrentSettlement.Culture; } catch { } }
            return c;
        }

        /// <summary>Warunki targu dowodcy karawany (pamiec 300b na partie, kulture i godzine); null = bez dowodcy.</summary>
        internal static PlayerHaggle.Terms TermsOf(MobileParty mp, PartyBase merchant)
        {
            if (mp == null || mp.LeaderHero == null) return null;
            return PlayerHaggle.GetFor(mp, CultureFor(mp, merchant), -1);
        }

        /// <summary>
        /// Wolane z PlayerHaggle.Postfix (zewnetrzne wywolanie GetTradePenalty, Priority.Last - po BK) dla kazdej karawany. Karawana bez dowodcy - wczesne wyjscie
        /// (poza sonda samotestu). Klucz wylaczony - nic.
        /// </summary>
        internal static void Penalty(TradeItemPriceFactorModel inst, ItemObject it, MobileParty mp, PartyBase merchant, bool selling, ref float result)
        {
            try
            {
                if (!On) return;
                if (mp.LeaderHero == null)
                {
                    int ps = _probe1 - 1;
                    if (ps < 0) { if (!_quiet) _c0++; return; }
                    float tf = TfOf(mp);
                    float inv = 1f / tf + E(ps);
                    float rp = result * FactorOf(inv > 0.05f ? 1f / inv : 20f, tf);
                    if (!float.IsNaN(rp) && !float.IsInfinity(rp)) result = rp;
                    return;
                }
                var t = TermsOf(mp, merchant);
                if (t == null) return;
                float r = result * Factor(t);
                if (float.IsNaN(r) || float.IsInfinity(r)) return;
                if (!_quiet) { _c1++; if (r != result) _c1c++; }
                result = r;
            }
            catch (Exception e) { Stumble("Penalty", e); }
        }

        /// <summary>
        /// POMOCNIK DLA RUNDY 139 (ocena rynku dowodcy): J = 1 - PartyTradeModel.GetTradePenaltyFactor(mp) dla karawany z bohaterem-dowodca, w [0, 1]; bez dowodcy
        /// albo przy kluczu CaravanLeaderJudgement NIE - 0 (dokladnie jak bez 139b). Handel 0 / 50 / 150 / 300: 0 / 0.091 / 0.231 / 0.375. Tylko odczyt.
        /// Gdzie 139 ma to wpiac (139b niczego nie wpina): (a) CaravanGoods.AgeMarket, regula "czekanie kosztuje zold": najdluzsze czekanie x (1 - J);
        /// (b) CaravanRoute294c, GetTradeScoreForTown / ThinkNextDestination z kosztem drogi 139: dopuszczalna odleglosc x (1 + J), wagi kosztu drogi bez zmian;
        /// (c) limit BK BuyCategory 1500 - bez J (limit wplywu na rynek, nie wiedzy).
        /// </summary>
        internal static float SkillJudge(MobileParty mp)
        {
            try
            {
                if (!JudgeOn || mp == null || !mp.IsCaravan || mp.LeaderHero == null) return 0f;
                float j = 1f - TfOf(mp);
                return j < 0f ? 0f : j > 1f ? 1f : j;
            }
            catch (Exception e) { Stumble("SkillJudge", e); return 0f; }
        }

        // ------------------------------------------------------------ handel (CaravanGoods.TradePostfix)
        private static void Pens(MobileParty mp, ItemObject it, Settlement st, bool selling, out float old, out float now)
        {
            old = now = 0f;
            var model = Campaign.Current != null && Campaign.Current.Models != null ? Campaign.Current.Models.TradeItemPriceFactorModel : null;
            if (model == null || it == null) return;
            bool q = _quiet;
            try
            {
                _quiet = true;
                old = PlayerHaggle.Pen(model, it, mp, st != null ? st.Party : null, selling, 0f, 0f, 0f, true, -1);
                now = PlayerHaggle.Pen(model, it, mp, st != null ? st.Party : null, selling, 0f, 0f, 0f, false, -1);
            }
            finally { _quiet = q; }
        }

        /// <summary>Kupno karawany w miescie (Kind 1): pomiar oszczednosci wobec kary 138 (karawany z dowodca). Tylko odczyt.</summary>
        internal static void NoteBuy(MobileParty mp, ItemObject it, Settlement st, int got, long paid)
        {
            try
            {
                if (mp == null || mp.LeaderHero == null || got <= 0 || paid <= 0) return;
                float old, now;
                Pens(mp, it, st, false, out old, out now);
                lock (_lock)
                {
                    _buyQ += got; _buyB += paid;
                    _buyB0 += paid * (1.0 + old) / Math.Max(0.05, 1.0 + now);
                    if (old > _maxOld) _maxOld = old;
                }
            }
            catch (Exception e) { Stumble("NoteBuy", e); }
        }

        /// <summary>
        /// Sprzedaz karawany miastu (Kind 2, po NoteSale 284): moved sztuk, m z nich z kosztem w ksiedze (przed zdjeciem), got utarg przed mytem, toll myto, costRm koszt
        /// zdjety z ksiegi. Pomiar premii wobec kary 138 i XP dowodcy: zysk = round(got x m / moved - toll x m / moved - costRm); zysk > 0 i klucz XP - regula gracza
        /// (SkillLevelingManager.OnTradeProfitMade(PartyBase): 0.5 x zysk dla PartyLeader). Sztuki bez kosztu w ksiedze - bez XP (jak u gracza).
        /// </summary>
        internal static void NoteSale(MobileParty mp, ItemObject it, Settlement st, int moved, int m, int got, int toll, double costRm)
        {
            try
            {
                if (mp == null || mp.LeaderHero == null || moved <= 0) return;
                float old, now;
                Pens(mp, it, st, true, out old, out now);
                int profit = 0;
                if (m > 0 && got > 0) profit = (int)Math.Round((double)got * m / moved - (double)Math.Max(0, toll) * m / moved - costRm);
                Hero who = null;
                if (profit > 0 && XpOn)
                {
                    who = mp.GetEffectiveRoleHolder(PartyRole.PartyLeader);
                    if (who != null) SkillLevelingManager.OnTradeProfitMade(mp.Party, profit);
                }
                lock (_lock)
                {
                    _sellQ += moved;
                    if (got > 0) { _sellG += got; _sellG0 += got * (1.0 + now) / Math.Max(0.05, 1.0 + old); }
                    if (old > _maxOld) _maxOld = old;
                    if (moved > m) _noCost += moved - Math.Max(0, m);
                    if (profit > 0) _profitZ += profit;
                    if (who != null)
                    {
                        _profitZk += profit; _xpSales++;
                        double x; _xpHero.TryGetValue(who, out x); _xpHero[who] = x + XpPerProfit * profit;
                    }
                }
            }
            catch (Exception e) { Stumble("NoteSale", e); }
        }

        // ------------------------------------------------------------ XP z wyplat (prefiks OnTradeProfitMade(Hero, int))
        /// <summary>Prefiks DefaultSkillLevelingManager (i czynnego menedzera, gdy inny - NavalDLC) OnTradeProfitMade(Hero, int): przy kluczu XP pomija XP z wyplaty
        /// nadwyzki dla bohatera, ktory dowodzi karawana (ten zysk dostal juz XP przy sprzedazy). Reszta (wlasciciel-notabl, gracz) bez zmian.</summary>
        public static bool XpPrefix(Hero __0, int __1)
        {
            try
            {
                if (__0 == null || __1 <= 0 || !XpOn) return true;
                var p = __0.PartyBelongedTo;
                if (p == null || !p.IsCaravan || p.LeaderHero != __0) return true;
                lock (_lock) { _payR++; _payA += __1; }
                return false;
            }
            catch (Exception e) { Stumble("XpPrefix", e); return true; }
        }

        internal static void Install(Harmony h)
        {
            var types = new List<Type> { typeof(DefaultSkillLevelingManager) };
            try { var nv = AccessTools.TypeByName("NavalDLC.CharacterDevelopment.NavalSkillLevellingManager"); if (nv != null) types.Add(nv); } catch { }
            foreach (var t in types)
            {
                try
                {
                    var m = AccessTools.DeclaredMethod(t, "OnTradeProfitMade", new[] { typeof(Hero), typeof(int) });
                    if (m == null || m.IsAbstract) continue;
                    h.Patch(m, prefix: new HarmonyMethod(typeof(CaravanLeader139b), nameof(XpPrefix)) { priority = Priority.First });
                    _xpHooks.Add(t.Name);
                    if (t == typeof(DefaultSkillLevelingManager)) _xpOk = true;
                }
                catch (Exception e) { Log.Error("CaravanLeader139b.Install(" + t.Name + ")", e); }
            }
            Log.Info("Karawany - dowodca (139b): XP z wyplat - prefiks OnTradeProfitMade(Hero) wpiety w " + (_xpHooks.Count == 0 ? "-" : string.Join(", ", _xpHooks)) + ".");
        }

        /// <summary>Czynny menedzer umiejetnosci: "Default" albo "Naval -> Default TAK/NIE" (pole typu DefaultSkillLevelingManager - Naval przekazuje OnTradeProfitMade).</summary>
        private static string ManagerChain(out bool ok)
        {
            ok = false;
            try
            {
                object cur = Campaign.Current != null ? Campaign.Current.SkillLevelingManager : null;
                if (cur == null) return "brak menedzera";
                var sb = new StringBuilder();
                for (int depth = 0; cur != null && depth < 4; depth++)
                {
                    var t = cur.GetType();
                    if (depth > 0) sb.Append(" -> ");
                    sb.Append(t.Name == "DefaultSkillLevelingManager" ? "Default" : t.Name == "NavalSkillLevellingManager" ? "Naval" : t.Name);
                    if (cur is DefaultSkillLevelingManager) { ok = _xpOk; break; }
                    object next = null;
                    foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                        if (typeof(ISkillLevelingManager).IsAssignableFrom(f.FieldType)) { next = f.GetValue(cur); break; }
                    cur = next;
                }
                return sb.ToString() + " " + (ok ? "TAK" : "NIE");
            }
            catch (Exception e) { Stumble("ManagerChain", e); return "blad"; }
        }

        // ------------------------------------------------------------ linia startowa i samotest
        private static string F3(float v) { return float.IsNaN(v) ? "-" : v.ToString("0.000", Inv); }
        private static string F4(float v) { return float.IsNaN(v) ? "-" : v.ToString("0.0000", Inv); }
        private static string YN(bool b) { return b ? "TAK" : "NIE"; }

        private static float Probe(TradeItemPriceFactorModel model, ItemObject it, MobileParty mp, PartyBase mer, bool selling, int s, bool raw)
        {
            int p0 = _probe1;
            try { _probe1 = s < 0 ? 0 : s + 1; return PlayerHaggle.Pen(model, it, mp, mer, selling, 0f, 0f, 0f, raw, -1); }
            finally { _probe1 = p0; }
        }

        private static void Census(out int k, out int hAll, out int hp, out int ho)
        {
            k = hAll = hp = ho = 0;
            foreach (var mp in MobileParty.AllCaravanParties)
            {
                if (mp == null || !mp.IsActive) continue;
                k++;
                if (mp.LeaderHero == null) continue;
                hAll++;
                if (mp.ActualClan == Clan.PlayerClan) hp++; else ho++;
            }
        }

        /// <summary>Start sesji (po PlayerHaggle.StartLine): linia "Karawany - dowodca (139b): start" z krzywa i samotestem. Tylko odczyt.</summary>
        internal static void Start()
        {
            try
            {
                var s = Settings.Current;
                var model = Campaign.Current != null && Campaign.Current.Models != null ? Campaign.Current.Models.TradeItemPriceFactorModel : null;
                if (s == null || model == null) { Log.Info("Karawany - dowodca (139b): start niemozliwy (brak ustawien albo modelu cen)."); return; }
                bool nav;
                string chain = ManagerChain(out nav);
                // sonda: karawany bez dowodcy (tf mistrza), towary handlowe z polek miast
                var bare = new List<MobileParty>();
                foreach (var mp in MobileParty.AllCaravanParties)
                {
                    if (bare.Count >= 20) break;
                    if (mp != null && mp.IsActive && mp.LeaderHero == null) bare.Add(mp);
                }
                var towns = new List<Settlement>();
                foreach (var st in Settlement.All)
                {
                    if (towns.Count >= 3) break;
                    if (st != null && st.IsTown && st.Town != null && st.Town.MarketData != null && st.ItemRoster != null) towns.Add(st);
                }
                var goods = new List<EquipmentElement>();
                if (towns.Count > 0)
                {
                    var r = towns[0].ItemRoster;
                    for (int i = 0; i < r.Count && goods.Count < 3; i++)
                    {
                        var e = r.GetElementCopyAtIndex(i);
                        var it = e.EquipmentElement.Item;
                        if (e.Amount <= 0 || it == null || !it.IsTradeGood || e.EquipmentElement.ItemModifier != null) continue;
                        if (!goods.Exists(g => g.Item == it)) goods.Add(new EquipmentElement(it));
                    }
                }
                ItemObject weapon = null;
                try
                {
                    foreach (var it in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
                        if (it != null && it.HasWeaponComponent && !it.IsTradeGood && !it.IsCraftedByPlayer && it.Value > 0) { weapon = it; break; }
                }
                catch { }
                var probeItems = new List<ItemObject>();
                foreach (var g in goods) probeItems.Add(g.Item);
                if (weapon != null) probeItems.Add(weapon);
                var p0 = bare.Count > 0 ? bare[0] : null;
                float tf0 = p0 != null ? TfOf(p0) : float.NaN;
                var sb = new StringBuilder();
                sb.Append("Karawany - dowodca (139b): start | klucze: targ ").Append(YN(s.CaravanLeaderHaggle)).Append(", XP ").Append(YN(s.CaravanLeaderTradeXp))
                  .Append(", ocena ").Append(YN(s.CaravanLeaderJudgement))
                  .Append(" | wpiete: kara w ").Append(PlayerHaggle.ModelCount).Append(" modelach (latka 300b), XP OnTradeProfitMade(Hero) ").Append(YN(_xpOk))
                  .Append(" (").Append(chain).Append("), sprzedaz SellItemsAction ").Append(YN(CaravanGoods.Hooked))
                  .Append(" | E ").Append(E(1).ToString("0.0000", Inv)).Append("/pkt, XP ").Append(XpPerProfit.ToString("0.00", Inv)).Append("/zl");
                // krzywa: czynnik analityczny przy tf = 1 i kara z lancucha na karawanie bez dowodcy (sonda S), towar w miescie, pokoj, bez kupca
                var fac = new float[CurveS.Length]; var pen = new float[CurveS.Length];
                for (int i = 0; i < CurveS.Length; i++)
                {
                    fac[i] = FactorOf(1f / (1f + E(CurveS[i])), 1f);
                    pen[i] = p0 != null && goods.Count > 0 ? Probe(model, goods[0].Item, p0, null, false, CurveS[i], false) : float.NaN;
                }
                sb.Append(" | czynnik S=");
                for (int i = 0; i < CurveS.Length; i++) { if (i > 0) sb.Append('/'); sb.Append(CurveS[i]); }
                sb.Append(": ");
                for (int i = 0; i < CurveS.Length; i++) { if (i > 0) sb.Append('/'); sb.Append(F3(fac[i])); }
                sb.Append(", kara ");
                for (int i = 0; i < CurveS.Length; i++) { if (i > 0) sb.Append('/'); sb.Append(F4(pen[i])); }
                sb.Append(", obrot x");
                for (int i = 0; i < CurveS.Length; i++)
                {
                    if (i > 0) sb.Append('/');
                    float o = float.IsNaN(pen[i]) || float.IsNaN(pen[0]) ? float.NaN : (float)Math.Pow((1.0 + pen[0]) / (1.0 + pen[i]), 2.0);
                    sb.Append(F3(o));
                }
                sb.Append(" (sonda: karawana bez dowodcy tf ").Append(F3(tf0)).Append(", towar ").Append(goods.Count > 0 ? goods[0].Item.StringId : "-").Append(')');
                sb.Append(" | J Handel ");
                for (int i = 0; i < CurveT.Length; i++) { if (i > 0) sb.Append('/'); sb.Append(CurveT[i]); }
                sb.Append(": ");
                for (int i = 0; i < CurveT.Length; i++) { if (i > 0) sb.Append('/'); sb.Append(F3(1f - 1f / (1f + E(CurveT[i])))); }
                // samotest
                int aOk = 0, aN = 0, bOk = 0, bN = 0, cOk = 0, cN = 0, dOk = 0, dN = 0, eOk = 0, eN = 0;
                var mers = new List<PartyBase> { null };
                if (towns.Count > 0) mers.Add(towns[0].Party);
                foreach (var mp in bare)
                    foreach (var it in probeItems)
                        foreach (var mer in mers)
                            for (int sell = 0; sell < 2; sell++)
                            {
                                // (a) bez dowodcy = 138: postfiks bez sondy nic nie zmienia
                                aN++;
                                if (Probe(model, it, mp, mer, sell == 1, -1, false) == Probe(model, it, mp, mer, sell == 1, -1, true)) aOk++;
                                // (b) S = 0 = 138 (sonda przy tf = 1 - mistrz bez Handlu)
                                if (TfOf(mp) == 1f)
                                {
                                    bN++;
                                    if (Probe(model, it, mp, mer, sell == 1, 0, false) == Probe(model, it, mp, mer, sell == 1, -1, true)) bOk++;
                                }
                                // (d) klucz NIE = 138 przy sondzie S = 300
                                bool o0 = Off;
                                try
                                {
                                    Off = true; dN++;
                                    if (Probe(model, it, mp, mer, sell == 1, 300, false) == Probe(model, it, mp, mer, sell == 1, -1, true)) dOk++;
                                }
                                finally { Off = o0; }
                            }
                // (c) sprzedaz <= kupno w tym samym miescie przy tej samej polce (3 miasta x 3 towary x S)
                if (p0 != null)
                    foreach (var st in towns)
                        foreach (var g in goods)
                            foreach (int S in TestS)
                            {
                                int pr = _probe1;
                                try
                                {
                                    _probe1 = S + 1;
                                    var md = st.Town.MarketData;
                                    int buy = md.GetPrice(g, p0, false, st.Party), sell = md.GetPrice(g, p0, true, st.Party);
                                    cN++;
                                    if (sell <= buy) cOk++;
                                }
                                catch (Exception e) { Stumble("Start(c)", e); }
                                finally { _probe1 = pr; }
                            }
                // (e) J bez dowodcy = 0
                foreach (var mp in bare) { eN++; if (SkillJudge(mp) == 0f) eOk++; }
                int k, hAll, hp, ho;
                Census(out k, out hAll, out hp, out ho);
                sb.Append(" | samotest: bez dowodcy = 138 ").Append(aOk).Append('/').Append(aN)
                  .Append(", S=0 = 138 ").Append(bOk).Append('/').Append(bN)
                  .Append(", sprzedaz <= kupno ").Append(towns.Count).Append(" miasta x ").Append(goods.Count).Append(" towary x S 0/150/300/700 ").Append(cOk).Append('/').Append(cN)
                  .Append(", klucz NIE = 138 ").Append(dOk).Append('/').Append(dN)
                  .Append(", J bez dowodcy 0 ").Append(eOk).Append('/').Append(eN)
                  .Append(" | karawan ").Append(k).Append(", z dowodca ").Append(hAll).Append(" (rod gracza ").Append(hp).Append(", inne ").Append(ho).Append(").");
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Start", e); }
        }

        // ------------------------------------------------------------ linia doby
        private static string Pct(double v) { return double.IsNaN(v) ? "-" : (100.0 * v).ToString("0.00", Inv); }

        /// <summary>Doba (po CaravanGoods.Daily): linia "Karawany - dowodca (139b): dzien N" w dobie pomiaru albo gdy sa karawany z dowodca / potkniecia; zawsze zeruje liczniki doby.</summary>
        internal static void Daily()
        {
            int day = (int)CampaignTime.Now.ToDays - 1;
            try
            {
                var s = Settings.Current;
                if (s == null) return;
                int k = 0, hAll = 0, hp = 0, ho = 0, hm = 0, tMin = int.MaxValue, tMax = int.MinValue;
                double tSum = 0, pSum = 0, sSum = 0, fSum = 0, jSum = 0; float fMin = float.MaxValue, fMax = float.MinValue;
                var who = new StringBuilder(); int listed = 0;
                foreach (var mp in MobileParty.AllCaravanParties)
                {
                    if (mp == null || !mp.IsActive) continue;
                    k++;
                    var hero = mp.LeaderHero;
                    if (hero == null) continue;
                    hAll++;
                    if (mp.ActualClan == Clan.PlayerClan) hp++; else ho++;
                    var t = TermsOf(mp, null);
                    if (t == null) continue;
                    float f = Factor(t), j = SkillJudge(mp);
                    hm++;
                    tSum += t.T; pSum += t.P.Total; sSum += t.S; fSum += f; jSum += j;
                    if (t.T < tMin) tMin = t.T; if (t.T > tMax) tMax = t.T;
                    if (f < fMin) fMin = f; if (f > fMax) fMax = f;
                    if (listed < 10)
                    {
                        double xp; lock (_lock) _xpHero.TryGetValue(hero, out xp);
                        who.Append(" || '").Append(mp.Name != null ? mp.Name.ToString() : mp.StringId).Append("' (wodz: Handel ").Append(t.T).Append(" (+").Append(xp.ToString("0", Inv))
                           .Append(" XP dzis), perswazja ").Append(t.P.Total).Append(", czynnik ").Append(F3(f)).Append(')');
                        listed++;
                    }
                }
                if (!LogGate.Lines && hAll == 0 && _dayErrors == 0) return;
                double n = Math.Max(1, hm);
                var sb = new StringBuilder();
                sb.Append("Karawany - dowodca (139b): dzien ").Append(day)
                  .Append(" | klucze: targ ").Append(YN(s.CaravanLeaderHaggle)).Append(", XP ").Append(YN(s.CaravanLeaderTradeXp)).Append(", ocena ").Append(YN(s.CaravanLeaderJudgement))
                  .Append(" | karawan ").Append(k).Append(", z dowodca ").Append(hAll).Append(" (gracza ").Append(hp).Append(", inne ").Append(ho).Append(')');
                if (hm > 0)
                    sb.Append(" | Handel sr ").Append((tSum / n).ToString("0.0", Inv)).Append(" (").Append(tMin).Append("..").Append(tMax).Append("), perswazja sr ").Append((pSum / n).ToString("0.0", Inv))
                      .Append(", S sr ").Append((sSum / n).ToString("0.0", Inv)).Append(", czynnik sr ").Append((fSum / n).ToString("0.000", Inv)).Append(" (").Append(F3(fMin)).Append("..").Append(F3(fMax))
                      .Append("), J sr ").Append((jSum / n).ToString("0.000", Inv));
                else sb.Append(" | Handel sr - (-..-), perswazja sr -, S sr -, czynnik sr - (-..-), J sr -");
                lock (_lock)
                {
                    double prem = _sellG0 > 0 ? _sellG / _sellG0 - 1.0 : double.NaN, save = _buyB0 > 0 ? 1.0 - _buyB / _buyB0 : double.NaN;
                    sb.Append(" | wywolan kary: z dowodca ").Append(_c1).Append(" (zmienione ").Append(_c1c).Append("), bez dowodcy ").Append(_c0).Append(" (zmienione 0)")
                      .Append(" | sprzedaz z dowodca ").Append(_sellQ).Append(" szt. utarg ").Append(_sellG).Append(" (przy karze 138 ok. ").Append(_sellG0.ToString("0", Inv))
                      .Append(", premia +").Append(Pct(prem)).Append("%), kupno ").Append(_buyQ).Append(" szt. ").Append(_buyB).Append(" (przy 138 ok. ").Append(_buyB0.ToString("0", Inv))
                      .Append(", oszczednosc ").Append(Pct(save)).Append("%), kara 138 max ").Append(Pct(_maxOld)).Append('%')
                      .Append(" | zysk dowodcow ").Append(_profitZ).Append(" zl, XP ").Append((XpPerProfit * _profitZk).ToString("0", Inv)).Append(" pkt surowe (")
                      .Append(XpPerProfit.ToString("0.00", Inv)).Append(" x ").Append(_profitZk).Append("; sprzedazy ").Append(_xpSales).Append("; sztuk bez kosztu ").Append(_noCost)
                      .Append("), XP z wyplat zastapione ").Append(_payR).Append(" (").Append(_payA).Append(" zl)")
                      .Append(" | potkniecia ").Append(_dayErrors).Append(" (sesja ").Append(_errors).Append(')');
                }
                sb.Append(who).Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally { lock (_lock) DayZero(); }
        }
    }
}
