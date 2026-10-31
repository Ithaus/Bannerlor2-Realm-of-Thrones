using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// 305a (31.10, Jeff: "czy mozna oddzielic koszt karawany od kosztu mojej armii, aby to bylo oddzielne pole w wydatkach"; "karawany, aby dzialaly i zarabialy").
    /// (1) OSOBNA LINIA ZOLDU KARAWAN (klucz CaravanWagesSeparateLine). BK ClanFinancesPatches.PartyExpensesPrefix (dekompilacja BannerKings.dll, prefiks zwraca false)
    ///     zastepuje cialo DefaultClanFinanceModel.AddExpensesFromPartiesAndGarrisons: druzyna glowna, karawany AliveLords/Companions, druzyny i zalogi rodu w jednej
    ///     linii "{=ChUDSiJw}Garrison and Party Expense" (przy includeDetails = false - podpowiedz zlota gry GetDenarTooltip, wersja zwykla; wersja z detalami ma
    ///     juz linie BK "Party wages <partia>"). Karawana: num4 = CalculatePartyWage; pominieta (bez linii, placi z kiesy) gdy wodz != glowa rodu i ma tytul Lordship
    ///     albo (applyWithdrawals i wodz to glowa swojego rodu - BK IsClanLeader); inaczej przy apply i kiesie < 5000 (po zdjeciu zoldu) num4 += 200, zwrot do kiesy
    ///     min(num4, budzet) i -num4 w linii. Nasze latki (w kampanii, z EnsureHooks, cel AccessTools.DeclaredMethod na klasie deklarujacej):
    ///       prefiks Priority.First na AddExpensesFromPartiesAndGarrisons - okno [ThreadStatic] tylko dla Clan.PlayerClan (lista karawan jak w BK, kiesy przed);
    ///       postfiks Priority.Last na CalculatePartyWage - koncowy wynik (po SoldierPay / MoneyLedger / GentryService) karawany z listy, bez pominietych przez BK;
    ///       postfiks Priority.Last na AddExpensesFromPartiesAndGarrisons - doplata = suma max(0, kiesa po - kiesa przed) karawan niepominietych (tylko apply);
    ///       przy includeDescriptions i !includeDetails: +zold do linii BK (ta sama nazwa - StatExplainer skleja), -zold "Caravan wages".
    ///       305f (przeglad scalenia): doplata BK +200 idzie tylko przy applyWithdrawals, a rozbicie tylko w podgladzie (bez apply) - w podgladzie doplata = 0,
    ///       wiec linii "Caravan purse top-up" nigdy nie bylo (galaz usunieta); doplata zostaje w linii BK rozliczenia doby i w licznikach (apply) linii 305a.
    ///       Suma (BaseNumber) bez zmian z budowy; przeplyw zlota bez zmian (tylko opis). Finalizer zamyka okno.
    ///     Linia ruszana tylko, gdy ma wartosc <= -(zold + doplata) (inaczej nie nasza sytuacja - licznik). Bez prefiksu BK (_bkRefund) - nic (gra: karawana placi sama).
    /// (2) DZIENNIK KARAWANY GRACZA (klucz CaravanPlayerDiary, tylko log): kazda transakcja (towar StringId, miasto, szt., cena/szt., cena polki miasta po transakcji,
    ///     Value, kategoria i jej indeks ceny w miescie, koszt/szt. z ksiegi E3-T1 przy sprzedazy) i raz na dobe miejsce / cel / zachowanie AI / doby od ostatniej
    ///     wizyty w miescie / doby bez handlu z rzedu - w linii 259 gracza. Dowod potrzeby: 108847-108850 kupno 7 szt. za 10297, sprzedaz 5 szt. za 118842 - z logu
    ///     nie da sie ustalic towaru ani miasta.
    /// (3) KONIEC KARAWANY (klucz CaravanLossNotice): MobilePartyDestroyed (nasluch dopisany OSTATNI - biegnie PIERWSZY, przed BeatenPurse i CaravanCapital.RoutePurse,
    ///     wiec kiesa jeszcze w karawanie): niszczyciel, jego frakcja, miejsce, kiesa i towar w chwili zdarzenia i z ostatniej doby, stan wodza; linia 259 i komunikat
    ///     dla gracza (gra ani BK go nie daja - sprawdzone w CaravansCampaignBehavior.OnMobilePartyDestroyed i napisach DLL). Rozwiazana bez bitwy: kiesa wraca do rodu
    ///     jak dotad (E3/7 RoutePurse -> NoteDeposit DepEnd); linia "zakonczona" mowi, czy wrocila.
    /// Linia doby "Karawany gracza - osobna linia zoldu (305a): dzien N" z samotestem: CalculateClanExpenses rodu gracza bez rozbicia i z rozbiciem - roznica sumy 0.
    /// </summary>
    internal static partial class CaravanSettle
    {
        private const string P305 = "Karawany gracza - osobna linia zoldu (305a): ";

        private static bool WageLineOn { get { var s = S; return s != null && s.CaravanWagesSeparateLine; } }
        private static bool DiaryOn { get { var s = S; return s != null && s.CaravanPlayerDiary; } }
        private static bool LossOn { get { var s = S; return s != null && s.CaravanLossNotice; } }

        // ------------------------------------------------------------ (1) okno rozliczenia wydatkow rodu gracza
        internal sealed class Win305
        {
            public Clan Clan; public bool Apply;
            public readonly HashSet<MobileParty> Set = new HashSet<MobileParty>();
            public readonly Dictionary<MobileParty, int> Gold0 = new Dictionary<MobileParty, int>();
            public readonly HashSet<MobileParty> Skip = new HashSet<MobileParty>();
            public long Wage; public int N; public long SkipWage; public float Gc0;
        }

        [ThreadStatic] private static Win305 _win305;
        [ThreadStatic] private static bool _suppress305;     // samotest: wywolanie bez rozbicia
        private static bool _w305Exp, _w305Cpw;
        private static string _w305Miss = "-";
        private static TextObject _tGarrison, _tCarWage, _tCarTop;

        // liczniki doby (watek gry; lock - jak reszta CaravanSettle)
        private static int _pApplyN, _pViewN, _pSplitN, _pNoLineN, _pDiffN, _pSkipN, _pSt, _sPSt;
        private static long _pApplyWage, _pApplyTop, _pApplyBk, _pViewWage, _pSkipWage;
        private static float _pDiffMax;

        private static void Stumble305(string where, Exception e)
        {
            lock (_lk) { _pSt++; _sPSt++; }
            Stumble(where, e);
        }

        /// <summary>Z EnsureHooks (kampania, raz na proces, po wykryciu _bkRefund).</summary>
        private static void EnsureHooks305(List<string> miss)
        {
            var m305 = new List<string>();
            try
            {
                var exp = AccessTools.DeclaredMethod(typeof(DefaultClanFinanceModel), "AddExpensesFromPartiesAndGarrisons");
                var ps = exp != null ? exp.GetParameters() : null;
                if (exp != null && ps.Length == 4 && ps[0].ParameterType == typeof(Clan) && ps[1].ParameterType == typeof(ExplainedNumber).MakeByRefType()
                    && ps[2].ParameterType == typeof(bool) && ps[3].ParameterType == typeof(bool))
                {
                    _harmony.Patch(exp, prefix: new HarmonyMethod(typeof(CaravanSettle), nameof(ExpPre305)) { priority = Priority.First },
                                        postfix: new HarmonyMethod(typeof(CaravanSettle), nameof(ExpPost305)) { priority = Priority.Last },
                                        finalizer: new HarmonyMethod(typeof(CaravanSettle), nameof(ExpFin305)));
                    _w305Exp = true;
                }
                else m305.Add("AddExpensesFromPartiesAndGarrisons (inna sygnatura)");
            }
            catch (Exception e) { m305.Add("AddExpensesFromPartiesAndGarrisons (blad: " + e.Message + ")"); Stumble305("EnsureHooks305(exp)", e); }
            try
            {
                var cpw = AccessTools.DeclaredMethod(typeof(DefaultClanFinanceModel), "CalculatePartyWage");
                var ps = cpw != null ? cpw.GetParameters() : null;
                if (cpw != null && cpw.ReturnType == typeof(int) && ps.Length == 3 && ps[0].ParameterType == typeof(MobileParty) && ps[2].ParameterType == typeof(bool))
                {
                    _harmony.Patch(cpw, postfix: new HarmonyMethod(typeof(CaravanSettle), nameof(WagePost305)) { priority = Priority.Last });
                    _w305Cpw = true;
                }
                else m305.Add("CalculatePartyWage (inna sygnatura)");
            }
            catch (Exception e) { m305.Add("CalculatePartyWage (blad: " + e.Message + ")"); Stumble305("EnsureHooks305(cpw)", e); }
            _w305Miss = m305.Count > 0 ? string.Join(", ", m305.ToArray()) : "-";
            miss.AddRange(m305);
        }

        /// <summary>BK PartyExpensesPrefix pomija karawane (bez linii, bez zwrotu): wodz != glowa rodu z Lordship albo (apply i wodz glowa swojego rodu).</summary>
        private static bool BkSkips(MobileParty mp, Clan clan, bool apply)
        {
            var h = mp.LeaderHero;
            if (h == null) return false;
            if (h != clan.Leader) { bool any, lord; Titles(h, out any, out lord); if (lord) return true; }
            return apply && h.Clan != null && h.Clan.Leader == h;
        }

        public static void ExpPre305(Clan __0, ref ExplainedNumber __1, bool __2, bool __3, out Win305 __state)
        {
            __state = _win305;
            _win305 = null;
            try
            {
                if (__0 == null || __0 != Clan.PlayerClan || !_bkRefund || !_w305Cpw || !Live || !WageLineOn) return;
                // okno tylko tam, gdzie cos robi: rozliczenie doby (liczniki) albo podglad z opisem bez detali (rozbicie linii); pasek mapy (MapInfoVM - detale) i
                // sumy ekranu rodu (bez opisu) - bez okna, bez alokacji
                if (!__2 && (__3 || !__1.IncludeDescriptions)) return;
                var w = new Win305 { Clan = __0, Apply = __2, Gc0 = __1.ResultNumber };
                // ta sama lista co BK: OwnedCaravans zywych lordow i towarzyszy rodu (bez filtra IsActive - jak BK)
                foreach (var h in __0.AliveLords) if (h != null && h.OwnedCaravans != null) foreach (var cp in h.OwnedCaravans) { var mp = cp != null ? cp.MobileParty : null; if (mp != null) w.Set.Add(mp); }
                foreach (var h in __0.Companions) if (h != null && h.OwnedCaravans != null) foreach (var cp in h.OwnedCaravans) { var mp = cp != null ? cp.MobileParty : null; if (mp != null) w.Set.Add(mp); }
                if (w.Set.Count == 0) return;
                foreach (var mp in w.Set) { w.Gold0[mp] = mp.PartyTradeGold; if (BkSkips(mp, __0, __2)) w.Skip.Add(mp); }
                _win305 = w;
            }
            catch (Exception e) { _win305 = null; Stumble305("ExpPre305", e); }
        }

        /// <summary>Koncowy wynik CalculatePartyWage (po wszystkich latkach) - tyle BK wpisuje do linii dla tej karawany (bez doplaty 200).</summary>
        public static void WagePost305(MobileParty __0, bool __2, int __result)
        {
            var w = _win305;
            if (w == null || __0 == null) return;
            try
            {
                if (!w.Set.Contains(__0)) return;
                if (w.Skip.Contains(__0)) { w.SkipWage += Math.Max(0, __result); return; }
                w.Wage += Math.Max(0, __result); w.N++;
            }
            catch (Exception e) { Stumble305("WagePost305", e); }
        }

        private static string TxtOf(ref TextObject cache, string id)
        {
            if (cache == null) cache = new TextObject(id);
            return cache.ToString();
        }

        public static void ExpPost305(Clan __0, ref ExplainedNumber __1, bool __2, bool __3, Win305 __state)
        {
            var w = _win305;
            if (w == null || !ReferenceEquals(w.Clan, __0)) return;
            try
            {
                long top = 0;
                if (w.Apply)
                    foreach (var kv in w.Gold0)
                        if (!w.Skip.Contains(kv.Key)) top += Math.Max(0, kv.Key.PartyTradeGold - kv.Value);
                long all = w.Wage + top;
                float bk = __1.ResultNumber - w.Gc0;
                if (w.Apply)
                {
                    lock (_lk) { _pApplyN++; _pApplyWage += w.Wage; _pApplyTop += top; _pApplyBk += (long)Math.Round(-bk); _pSkipN += w.Skip.Count; _pSkipWage += w.SkipWage; }
                }
                // rozbicie tylko w podgladzie: BKClanFinanceModel.CalculateClanGoldChange wymusza opis (includeDescriptions=true) takze w rozliczeniu doby,
                // wiec przy apply tylko liczniki rozliczenia (poprawka recenzji 305a)
                if (__2 || !__1.IncludeDescriptions || __3 || _suppress305 || all <= 0) return;
                lock (_lk) { _pViewN++; _pViewWage += w.Wage; }
                string gName = TxtOf(ref _tGarrison, "{=ChUDSiJw}Garrison and Party Expense");
                float gVal = 0f; bool has = false;
                foreach (var l in __1.GetLines()) if (l.name == gName) { gVal += l.number; has = true; }
                if (!has || gVal > -all + 0.5f) { lock (_lk) _pNoLineN++; return; }
                float before = __1.ResultNumber;
                __1.Add(all, new TextObject("{=ChUDSiJw}Garrison and Party Expense"));
                __1.Add(-w.Wage, new TextObject("{=arm305cw}Caravan wages"));
                // 305f: bez linii "Caravan purse top-up" - tu zawsze podglad (bez apply), a BK dolicza doplate 200 tylko przy applyWithdrawals, wiec top = 0
                float d = __1.ResultNumber - before;
                lock (_lk)
                {
                    _pSplitN++;
                    if (Math.Abs(d) > 0.5f) { _pDiffN++; if (Math.Abs(d) > _pDiffMax) _pDiffMax = Math.Abs(d); }
                }
            }
            catch (Exception e) { Stumble305("ExpPost305", e); }
        }

        public static Exception ExpFin305(Exception __exception, Win305 __state)
        {
            _win305 = __state;
            return __exception;
        }

        /// <summary>Raz na dobe (Daily, po PlayerDaily): liczniki i samotest - wydatki rodu gracza (podglad) bez rozbicia i z rozbiciem.</summary>
        private static void Daily305(Settings s, int day)
        {
            var pc = Clan.PlayerClan;
            if (pc == null || !Live) return;
            var sb = new StringBuilder(700);
            sb.Append(P305).Append("dzien ").Append(day).Append(" | klucze: osobna linia ").Append(Tn(s.CaravanWagesSeparateLine)).Append(", dziennik karawany ").Append(Tn(s.CaravanPlayerDiary))
              .Append(", koniec karawany ").Append(Tn(s.CaravanLossNotice))
              .Append(" | latki wpiete ").Append((_w305Exp ? 1 : 0) + (_w305Cpw ? 1 : 0)).Append("/2 (brak: ").Append(_w305Miss).Append("), BK PartyExpensesPrefix ").Append(Tn(_bkRefund));
            string test = "POMINIETY (klucz NIE albo brak latek / BK)";
            if (s.CaravanWagesSeparateLine && _w305Exp && _w305Cpw && _bkRefund)
            {
                try
                {
                    var fm = Campaign.Current.Models.ClanFinanceModel;
                    bool q = _suppress305;
                    ExplainedNumber a;
                    try { _suppress305 = true; a = fm.CalculateClanExpenses(pc, true, false, false); }
                    finally { _suppress305 = q; }
                    var b = fm.CalculateClanExpenses(pc, true, false, false);
                    string gName = TxtOf(ref _tGarrison, "{=ChUDSiJw}Garrison and Party Expense"), wName = TxtOf(ref _tCarWage, "{=arm305cw}Caravan wages"), tName = TxtOf(ref _tCarTop, "{=arm305ct}Caravan purse top-up");
                    float ga = 0, gb = 0, wb = 0, tb = 0;
                    foreach (var l in a.GetLines()) if (l.name == gName) ga += l.number;
                    foreach (var l in b.GetLines()) { if (l.name == gName) gb += l.number; else if (l.name == wName) wb += l.number; else if (l.name == tName) tb += l.number; }
                    float diff = b.ResultNumber - a.ResultNumber;
                    bool ok = Math.Abs(diff) < 0.5f && Math.Abs((gb + wb + tb) - ga) < 0.5f;
                    test = "wydatki rodu (podglad) bez rozbicia " + Math.Round(a.ResultNumber) + ", z rozbiciem " + Math.Round(b.ResultNumber) + " (roznica " + Math.Round(diff, 1).ToString(Inv)
                           + ") | linie: 'Garrison and Party Expense' " + Math.Round(ga) + " -> " + Math.Round(gb) + ", 'Caravan wages' " + Math.Round(wb) + ", 'Caravan purse top-up' " + Math.Round(tb)
                           + " -> " + (ok ? "ZGODNE" : "NIEZGODNE");
                }
                catch (Exception e) { test = "wyjatek " + e.GetType().Name; Stumble305("Daily305(samotest)", e); }
            }
            lock (_lk)
            {
                sb.Append(" | rozliczenie rodu (apply): wywolan ").Append(_pApplyN).Append(", zold karawan w linii BK ").Append(_pApplyWage).Append(", doplata BK do kiesy ").Append(_pApplyTop)
                  .Append(", linia partii i zalog BK razem ").Append(_pApplyBk).Append(", pominiete przez BK (Lordship / glowa rodu) ").Append(_pSkipN).Append(" (zold z kiesy ").Append(_pSkipWage).Append(')')
                  .Append(" | podglad (podpowiedz zlota): wywolan z opisem ").Append(_pViewN).Append(", rozbite ").Append(_pSplitN).Append(", bez linii BK albo za mala ").Append(_pNoLineN)
                  .Append(", zmiana sumy ").Append(_pDiffN).Append(" (max ").Append(_pDiffMax.ToString("0.0", Inv)).Append(')')
                  .Append(" | samotest: ").Append(test)
                  .Append(" | potkniecia ").Append(_pSt).Append(" (sesja ").Append(_sPSt).Append(").");
                _pApplyN = _pViewN = _pSplitN = _pNoLineN = _pDiffN = _pSkipN = _pSt = 0;
                _pApplyWage = _pApplyTop = _pApplyBk = _pViewWage = _pSkipWage = 0; _pDiffMax = 0f;
            }
            Log.Info(sb.ToString());
        }

        // ------------------------------------------------------------ (2) dziennik karawany gracza
        private const int TxMax = 30;   // transakcji na karawane na dobe w linii (reszta - licznik)

        private static string Nm(TextObject t, string fb) { try { return t != null ? t.ToString() : fb; } catch { return fb; } }

        /// <summary>NoteTrade (CaravanGoods.TradePostfix): wpis transakcji karawany gracza. kind 1 zakup, 2 sprzedaz; gold - zaplacone / utarg przed mytem.</summary>
        private static void Tx305(CarRec r, int kind, long gold, int toll, double costRm, ItemObject it, Settlement st, int units)
        {
            if (!DiaryOn || r == null || it == null) return;
            r.LastTradeDay = Today;
            if (st != null && st.IsTown) { r.LastTownDay = Today; r.LastTownName = Nm(st.Name, st.StringId); }
            if (r.DTx.Count >= TxMax) { r.DTxDrop++; return; }
            var town = st != null ? st.Town : null;
            int n = Math.Max(1, units);
            var sb = new StringBuilder(160);
            sb.Append(kind == 1 ? "kupno " : "sprzedaz ").Append(it.StringId).Append(" x").Append(units).Append(" w ").Append(st != null ? Nm(st.Name, st.StringId) : "?")
              .Append(" @").Append(Math.Round((double)gold / n).ToString(Inv));
            try
            {
                if (town != null)
                {
                    int shelf = town.GetItemPrice(it, null, kind == 2);   // polka po transakcji: kind 1 - cena, po ktorej miasto sprzedaje; 2 - po ktorej kupuje
                    sb.Append(" (polka ").Append(kind == 1 ? "sprzedaje " : "kupuje ").Append(shelf);
                    var cat = it.ItemCategory;
                    if (cat != null) sb.Append(", ").Append(cat.StringId).Append(" indeks ").Append(town.GetItemCategoryPriceIndex(cat).ToString("0.00", Inv));
                    sb.Append(", Value ").Append(it.Value).Append(')');
                }
                else sb.Append(" (Value ").Append(it.Value).Append(')');
            }
            catch { sb.Append(" (Value ").Append(it.Value).Append(')'); }
            if (kind == 2)
            {
                if (toll > 0) sb.Append(", myto ").Append(toll);
                if (costRm > 0 && units > 0)
                {
                    double cu = costRm / units, pu = (double)gold / units;
                    sb.Append(", koszt/szt ").Append(Math.Round(cu).ToString(Inv));
                    if (cu > 0 && pu / cu >= 5.0) sb.Append(" UWAGA x").Append((pu / cu).ToString("0.0", Inv));
                }
            }
            r.DTx.Add(sb.ToString());
        }

        /// <summary>SettlementEntered: karawana gracza wjechala do miasta (doby od ostatniej wizyty w linii 259).</summary>
        internal static void OnEntered305(MobileParty mp, Settlement st, Hero h)
        {
            try
            {
                if (mp == null || st == null || !st.IsTown || !mp.IsCaravan || !Live || !DiaryOn) return;
                if (mp.ActualClan != Clan.PlayerClan || Measure31.IsDteConvoy(mp)) return;
                var r = Car(mp, true);
                if (r == null) return;
                if (r.Name == "?") r.Name = NameOf(mp);
                r.LastTownDay = Today; r.LastTownName = Nm(st.Name, st.StringId);
            }
            catch (Exception e) { Stumble305("OnEntered305", e); }
        }

        private static string StName(Settlement st) { return st == null ? "-" : Nm(st.Name, st.StringId); }

        /// <summary>Najblizsza osada (miasto, zamek, wies) do pozycji partii - "w poblizu" w linii konca i komunikacie.</summary>
        private static Settlement Nearest305(MobileParty mp)
        {
            Settlement best = null; float bd = float.MaxValue;
            if (mp == null) return null;
            var p = mp.GetPosition2D;
            foreach (var t in Settlement.All)
            {
                if (t == null || !(t.IsTown || t.IsCastle || t.IsVillage)) continue;
                float d = p.DistanceSquared(t.GetPosition2D);
                if (d < bd) { bd = d; best = t; }
            }
            return best;
        }

        /// <summary>PlayerDaily (przed zerowaniem licznikow doby): miejsce, cel, zachowanie AI, ostatnia wizyta w miescie, doby bez handlu, transakcje dnia.</summary>
        private static void DiaryLine(StringBuilder sb, MobileParty mp, CarRec r, Calc now, int day)
        {
            r.LeaderRef = mp.LeaderHero ?? r.LeaderRef;
            r.LastPurse = now.Purse; r.LastGoods = now.Goods;
            if (!DiaryOn) return;
            bool traded = r.DSold > 0 || r.DBought > 0;
            if (traded) { r.NoTradeRun = 0; r.LastTradeDay = day; } else r.NoTradeRun++;
            if (r.NoTradeRun > r.MaxNoTradeRun) r.MaxNoTradeRun = r.NoTradeRun;
            var cur = mp.CurrentSettlement;
            if (cur != null && cur.IsTown) { r.LastTownDay = day; r.LastTownName = StName(cur); }
            var tg = mp.TargetSettlement;
            sb.Append(" | gdzie (305a): ").Append(cur != null ? "w " + StName(cur) + (cur.IsTown ? " (miasto)" : cur.IsCastle ? " (zamek)" : cur.IsVillage ? " (wies)" : "") : (mp.IsCurrentlyAtSea ? "na morzu" : "w drodze"))
              .Append(", cel ").Append(StName(tg));
            if (tg != null)
            {
                try
                {
                    if (tg.IsUnderSiege) sb.Append(" (OBLEZONY)");
                    if (tg.MapFaction != null && mp.MapFaction != null && tg.MapFaction.IsAtWarWith(mp.MapFaction)) sb.Append(" (WROG)");
                }
                catch { }
            }
            sb.Append(", zachowanie ").Append(mp.DefaultBehavior).Append('/').Append(mp.ShortTermBehavior)
              .Append(mp.MapEvent != null ? ", W BITWIE" : "").Append(mp.IsDisbanding ? ", ROZWIAZYWANA" : "")
              .Append(", ostatnio w miescie ").Append(r.LastTownDay >= 0 ? r.LastTownName + " w dobie " + r.LastTownDay + " (" + (day - r.LastTownDay) + " dob temu)" : "- (od wczytania nie)")
              .Append(", ostatni handel ").Append(r.LastTradeDay >= 0 ? "w dobie " + r.LastTradeDay : "- (od wczytania)")
              .Append(", bez handlu z rzedu ").Append(r.NoTradeRun).Append(" dob (najdluzej od wczytania ").Append(r.MaxNoTradeRun).Append(')');
            sb.Append(" | transakcje dnia (305a) ").Append(r.DTx.Count + r.DTxDrop);
            if (r.DTx.Count > 0) sb.Append(": ").Append(string.Join("; ", r.DTx.ToArray()));
            if (r.DTxDrop > 0) sb.Append("; ... i ").Append(r.DTxDrop).Append(" wiecej");
            r.DTx.Clear(); r.DTxDrop = 0;
        }

        // ------------------------------------------------------------ (3) koniec karawany gracza
        /// <summary>MobilePartyDestroyed (nasluch dopisany ostatni - biegnie pierwszy): przyczyna konca, linia i komunikat.</summary>
        internal static void OnDestroyed305(MobileParty mp, PartyBase destroyer)
        {
            try
            {
                if (mp == null || !mp.IsCaravan || !Live || !LossOn) return;
                if (mp.ActualClan != Clan.PlayerClan || Measure31.IsDteConvoy(mp)) return;
                var r = Car(mp, true);
                if (r == null) return;
                long cost, market, other; int kinds, units;
                long goods = CaravanGoods.WorthOf(mp, out cost, out market, out kinds, out units, out other);
                int purse = mp.PartyTradeGold;
                var leader = mp.LeaderHero ?? r.LeaderRef;
                var at = mp.CurrentSettlement ?? Nearest305(mp);
                string name = NameOf(mp);
                if (r.Name == "?") r.Name = name;
                string ldr = leader != null ? Nm(leader.Name, leader.StringId) : "-";
                string lstate = leader == null ? "-" : leader.IsDead ? "nie zyje" : leader.IsPrisoner ? "w niewoli" + (leader.PartyBelongedToAsPrisoner != null ? " (" + Nm(leader.PartyBelongedToAsPrisoner.Name, "?") + ")" : "") : "wolny";
                string dName = destroyer != null ? Nm(destroyer.Name, "?") : "-";
                var df = destroyer != null ? destroyer.MapFaction : null;
                string dFac = df != null ? Nm(df.Name, df.StringId) : "-";
                string dLead = destroyer != null && destroyer.LeaderHero != null ? Nm(destroyer.LeaderHero.Name, "?") : "-";
                bool battle = destroyer != null;
                r.EndCause = (battle ? "rozbita przez " + dName + " (frakcja " + dFac + ", wodz " + dLead + ")" : "rozwiazana bez bitwy (niszczyciel brak)")
                             + " w poblizu " + StName(at) + " w dobie " + Today + "; w chwili zdarzenia kiesa " + purse + ", towar " + goods + " (" + units + " szt.)"
                             + "; ostatnia doba: kiesa " + r.LastPurse + ", towar " + r.LastGoods + "; wodz " + ldr + " - " + lstate;
                Log.Info(PP + "karawana '" + name + "' - koniec (305a): " + r.EndCause + ".");
                if (battle)
                {
                    long coin = Math.Max(purse, r.LastPurse), gd = Math.Max(goods, r.LastGoods);
                    string txt = "Your caravan " + name + (leader != null ? " led by " + ldr : "") + " was destroyed by " + dName + (df != null ? " of " + dFac : "")
                                 + (at != null ? " near " + StName(at) : "") + ". At its last count it carried " + N(coin) + " denars in coin and goods worth about " + N(gd)
                                 + "; your clan has lost them.";
                    Log.Player(txt, true);
                    r.EndMsg = true;
                    Log.Info(PP + "komunikat - karawana rozbita | " + txt + " | podzial reszty kiesy ustala BeatenPurse 3.2 (zwyciezca, osada albo miasto)");
                }
            }
            catch (Exception e) { Stumble305("OnDestroyed305", e); }
        }

        /// <summary>PlayerDaily, petla zakonczonych: przyczyna do linii i komunikat dla rozwiazanej / zniknietej bez bitwy (kiesa wrocila albo nie).</summary>
        private static string EndText305(CarRec r)
        {
            string cause = r.EndCause ?? "bez zdarzenia zniszczenia (usunieta przez gre albo inny mod - OnPartyRemoved)";
            try
            {
                // rekord bez imienia (nigdy niewidziany w PlayerDaily ani w nasluchu z partia - Name zostal "?") - bez komunikatu z pustym imieniem, tylko linia w logu
                if (LossOn && !r.EndMsg && (string.IsNullOrEmpty(r.Name) || r.Name == "?")) { r.EndMsg = true; Log.Info(PP + "komunikat pominiety - karawana bez imienia (nie widziana w dobie) | przyczyna " + cause); }
                if (LossOn && !r.EndMsg)
                {
                    string txt = r.DPayEnd > 0
                        ? "Your caravan " + r.Name + " was disbanded without a fight; its purse of " + N(r.DPayEnd) + " denars came back to your clan."
                        : "Your caravan " + r.Name + " is gone without a battle; its purse did not come back to your clan (at its last count it held " + N(r.LastPurse) + " denars and goods worth about " + N(r.LastGoods) + ").";
                    Log.Player(txt, r.DPayEnd <= 0);
                    r.EndMsg = true;
                    Log.Info(PP + "komunikat - karawana zakonczona | " + txt);
                }
            }
            catch (Exception e) { Stumble305("EndText305", e); }
            return " | przyczyna (305a): " + cause;
        }
    }
}
