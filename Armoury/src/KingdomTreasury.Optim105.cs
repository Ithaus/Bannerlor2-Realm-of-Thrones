using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// m105-optym (21.10, pomiar 102/n31: KingdomTreasury.Daily 66 ms/dobe, ok. 537 wywolan modelu finansow tylko dla skutkow ubocznych): klucz OptimKtSkipModel
    /// (domyslnie TAK; NIE = kod jak dotad 1:1). Przy podstawie powinnosci "ziemia" D stalego (165, CrownDuesFromLand) wynik modelu dochodu jest nadpisywany
    /// przez StableLand - liczba modelu szla tylko do porownania 169b (NoteModelIncome, sam log). Model liczy sie dalej u rodow, ktorym cos zapisuje albo
    /// moze zapisac (z lennami - TownBase / pensje rady; z wsiami BK z tytulu - podatek wsi BK; z warsztatem glowy - workshopTaxes BK; gracz), pomijany tylko
    /// u rodu, ktory spelnia wszystko naraz:
    ///   - podstawa ziemia i StableLand(c) &gt;= 0 (odczyt policzony przed modelem - model tego rodu nie dotyka ksiegi ClanIncomeBook),
    ///   - c.Fiefs.Count == 0 (gra: AddSettlementIncome, BK VillageIncomePrefix - petla po lennach pusta: bez CalculateTownTax / TownBase / FastProbe),
    ///   - BK GetActualVillages(c) puste (VillageIncomePrefix nie wola podatku wsi BK, ktory dla osady bez danych inicjuje GetPopData - zapis),
    ///   - glowa rodu bez warsztatu (gra: CalculateHeroIncomeFromWorkshops(clan.Leader) - BK CalculateOwnerIncomeFromWorkshop zapisuje workshopTaxes),
    ///   - nie rod gracza (alejki, podpowiedz wioski pobocznej SideVillageFief.IncomeViewPostfix).
    /// Reszta sciezki przy applyWithdrawals = false to odczyty (dekompilacja 1.4.8 / BK / ROT / NavalDLC: docs/noc-1920/m105-optym-CHANGELOG.md).
    /// Straznik (codziennie, przed petla rodow): lancuch czynnego modelu tylko ze znanych typow (ROT -> NavalDLC -> BK -> gra) i na kazdej metodzie tej sciezki
    /// tylko sprawdzone latki Harmony (nasze cztery, trzy prefiksy BK, postfiks BKROTPatch) - inna latka albo inny model = pominiecie wylaczone na te dobe
    /// (linia "Optym (m105)" mowi dlaczego). Tryb kontrolny OptimVerify: model liczony jak dotad (gra bierze stara droge), przed i po nim sondy zapisow
    /// (wywolania TownBase, workshopTaxes BK - liczba i suma, kiesy bohaterow rodu, cztery portfele krolestwa), StableLand przed / po i stan generatora
    /// losowego gry (MBFastRandom - pominiety model nie moze przesuwac losowan; straznik porownuje tylko nazwy latek, nie ich tresc) - kazda zmiana to roznica.
    /// </summary>
    internal static partial class KingdomTreasury
    {
        private static bool _ktSkipOn, _ktSkipVerify;
        private static string _ktGuard = "-";                     // powod wylaczenia przez straznika tej doby (null = przeszedl)
        private static int _ktCand, _ktSkip, _ktCmp, _ktDiff, _ktErr;   // doba: rody sprawdzone (podstawa ziemia), pominiete, porownania i roznice (Verify), potkniecia
        private static long _ktSkipSess, _ktCmpSess, _ktDiffSess, _ktErrSess;
        private static string _ktDiffNote;                         // pierwsza roznica doby (rod i sonda) - do linii kontrolnej
        private static Func<Clan, List<Village>> _ktActual;         // BK ClanExtensions.GetActualVillages (delegat raz na proces)
        private static bool _ktActualTried;
        private static List<MethodBase> _ktPath;                    // metody sciezki modelu dla rodu bez lenn (raz na proces; straznik latek)
        private static string _ktPathMiss;                          // brakujace typy BK przy budowie listy (null = komplet)
        private static object _ktBk;                                // BKClanFinanceModel z lancucha (sonda workshopTaxes w trybie kontrolnym)
        private static FieldInfo _ktWsTax, _ktBaseCalls;            // sondy: BK workshopTaxes, CouncilWages35._baseCalls
        private static bool _ktProbeTried;

        private const string TRot = "ROT.Models.ROTClanFinanceModel", TNaval = "NavalDLC.GameComponents.NavalDLCClanFinanceModel",
                             TBk = "BannerKings.Models.Vanilla.BKClanFinanceModel";

        // sprawdzone latki sciezki (zestaw:typ.metoda) - kazda tylko czyta przy applyWithdrawals = false albo dotyczy gracza / najemnika (m105-optym-CHANGELOG)
        // Straznik porownuje tylko nazwy: zmiana TRESCI naszych czterech latek (MercContract.cs, CouncilWages35.cs, SideVillageFief.cs) albo SmallGold.CutsTier
        // (wolane z TierPostfix) = przeglad KtSkip (zapis albo MBRandom przy applyWithdrawals = false). Siatka: sonda losowania w Optim Verify.
        private static readonly HashSet<string> KtKnownPatches = new HashSet<string>(StringComparer.Ordinal)
        {
            "Armoury:MercContract.MercIncomePrefix",            // AddMercenaryIncome: tylko najemnik z umowa (ci sa poza petla powinnosci)
            "Armoury:MercContract.TierPostfix",                 // CalculateClanIncomeInternal: zapis licznikow tylko przy applyWithdrawals
            "Armoury:CouncilWages35.IncomesPostfix",            // BK AddIncomes: wychodzi przy applyWithdrawals = false
            "Armoury:SideVillageFief.IncomeViewPostfix",        // CalculateClanIncome czynnego modelu: tylko rod gracza (pominiety); m115: linia prac tylko przy FinanceBox.In, sam odczyt
            "BannerKings:ClanFinancesPatches.KingdomBudgetPrefix",
            "BannerKings:ClanFinancesPatches.AddIncomeFromPartyPrefix",
            "BannerKings:ClanFinancesPatches.VillageIncomePrefix",   // AddSettlementIncome: lenna i wsie z tytulu (u pominietych puste)
            "BKROTPatch:CalculateHeroSuzerainPatch.Postfix",    // TitleManager.CalculateHeroSuzerain: filtr wyniku
            // 259: AddIncomeFromParty - rozliczenie karawan rodow; przy applyWithdrawals = false tylko odczyt (kiesa, juki, ksiega E3-T1 bez zapisu,
            // model kapitalu gry), bez MBRandom; zapis (kiesa, XP, slawa, zdarzenie gracza, straz doby) tylko przy applyWithdrawals = true
            "Armoury:CaravanSettle.IncomePrefix",
        };

        /// <summary>259 (CaravanSettle - linia startu): czy latka jest na liscie sprawdzonych latek sciezki modelu (inaczej straznik m105 wylacza pominiecie).</summary>
        internal static bool KtKnows(string key) { return key != null && KtKnownPatches.Contains(key); }

        /// <summary>259 (CaravanSettle.Test T5): wynik straznika m105 teraz (null - pominiecie dozwolone). Tylko autotest.</summary>
        internal static string KtGuardProbe()
        {
            try { return KtGuard(); }
            catch (Exception e) { return "wyjatek " + e.GetType().Name; }
            finally { _ktBk = null; }   // straznik ustawia _ktBk (sondy petli rodow) - poza petla nie trzymamy modelu
        }

        /// <summary>Poczatek petli rodow KingdomTreasury.Daily (po OptBegin): klucz tej doby i straznik.</summary>
        private static void KtSkipBegin(Settings s, bool land)
        {
            _ktCand = _ktSkip = _ktCmp = _ktDiff = _ktErr = 0; _ktDiffNote = null;
            _ktSkipOn = false; _ktSkipVerify = false; _ktBk = null;   // _ktBk: ustawia KtChain w strazniku tej doby
            if (s == null || !s.OptimKtSkipModel) { _ktGuard = "klucz Optim Kt Skip Model NIE"; return; }
            if (!land) { _ktGuard = "podstawa powinnosci nie ziemia (Crown Dues From Land NIE albo CrownIncome nieczynne) - wynik modelu potrzebny"; return; }
            try { _ktGuard = KtGuard(); }
            catch (Exception e) { _ktGuard = "straznik: wyjatek " + e.GetType().Name; KtStumble("straznik", e); }
            _ktSkipOn = _ktGuard == null;
            _ktSkipVerify = _ktSkipOn && Optim105.VerifyOn(s);
        }

        /// <summary>Czy model dochodu tego rodu mozna pominac (warunki z opisu klasy); preLand - StableLand policzony przed modelem (-1, gdy nie liczony).
        /// Wlasny try: wyjatek = model liczony jak dotad (rod nie traci powinnosci przez sprawdzenie).</summary>
        private static bool KtSkip(Clan c, out double preLand)
        {
            preLand = -1;
            if (!_ktSkipOn) return false;
            try
            {
                if (c == null || ReferenceEquals(c, Clan.PlayerClan) || c.Fiefs == null || c.Fiefs.Count != 0) return false;
                var ld = c.Leader;
                if (ld == null || ld.OwnedWorkshops == null || ld.OwnedWorkshops.Count != 0) return false;
                double l = ClanIncomeBook.StableLand(c);
                if (l < 0) return false;
                _ktCand++;
                var vs = _ktActual(c);
                if (vs == null || vs.Count != 0) return false;
                preLand = l;
                _ktSkip++; _ktSkipSess++;
                return true;
            }
            catch (Exception e) { preLand = -1; KtStumble("warunki", e); return false; }
        }

        // ------------------------------------------------------------ tryb kontrolny: sondy zapisow wokol modelu (tylko pominiete rody)
        private static long _pbBase, _pbWsN, _pbWsSum, _pbGold, _pbBudget, _pbTrib, _pbCall, _pbMerc;
        private static PerfD2.RngState _pbRng;                       // stan generatora losowego gry przed modelem (Ok falsz - sonda niedostepna)

        private static void KtProbe(Clan c, out long baseCalls, out long wsN, out long wsSum, out long gold, out long budget, out long trib, out long call, out long merc)
        {
            baseCalls = _ktBaseCalls != null ? Convert.ToInt64(_ktBaseCalls.GetValue(null)) : -1;
            wsN = -1; wsSum = 0;
            var o = _ktBk != null && _ktWsTax != null ? _ktWsTax.GetValue(_ktBk) : null;
            var dg = o as Dictionary<TaleWorlds.CampaignSystem.Settlements.Workshops.Workshop, int>;   // BK: Dictionary<Workshop, int> - bez pakowania wpisow
            if (dg != null) { wsN = dg.Count; foreach (var e in dg) wsSum += e.Value; }
            else
            {
                var d = o as IDictionary;   // inny typ pola w innej wersji BK - przeglad niegeneryczny jak dotad
                if (d != null) { wsN = d.Count; foreach (DictionaryEntry e in d) if (e.Value is int) wsSum += (int)e.Value; }
            }
            gold = 0;
            var hs = c.Heroes;
            if (hs != null) { for (int i = 0; i < hs.Count; i++) { var h = hs[i]; if (h != null) gold += h.Gold; } }
            else if (c.Leader != null) gold = c.Leader.Gold;
            var k = c.Kingdom;
            budget = k != null ? k.KingdomBudgetWallet : 0; trib = k != null ? k.TributeWallet : 0;
            call = k != null ? k.CallToWarWallet : 0; merc = k != null ? k.MercenaryWallet : 0;
        }

        /// <summary>Verify: stan przed modelem pominietego rodu.</summary>
        private static void KtProbeBefore(Clan c)
        {
            _pbRng = PerfD2.RngNow();   // wlasny try (blad - Ok falsz, sonda losowania milczy)
            try { KtProbe(c, out _pbBase, out _pbWsN, out _pbWsSum, out _pbGold, out _pbBudget, out _pbTrib, out _pbCall, out _pbMerc); }
            catch (Exception e) { KtStumble("sonda przed", e); _pbWsN = long.MinValue; }
        }

        /// <summary>Verify: stan po modelu i StableLand po modelu (stara droga) wobec przed - kazda zmiana to roznica (model mial skutek, ktorego pominiecie by nie dalo).</summary>
        private static void KtProbeAfter(Clan c, double preLand, double postLand)
        {
            try
            {
                var rng = PerfD2.RngNow();
                long b, n, sum, g, bu, tr, ca, me;
                KtProbe(c, out b, out n, out sum, out g, out bu, out tr, out ca, out me);
                string what = null;
                if (_pbWsN == long.MinValue) what = "sonda przed (wyjatek)";
                else if (b != _pbBase) what = "TownBase " + _pbBase + " -> " + b;
                else if (n != _pbWsN || sum != _pbWsSum) what = "workshopTaxes " + _pbWsN + "/" + _pbWsSum + " -> " + n + "/" + sum;
                else if (g != _pbGold) what = "kiesy bohaterow rodu " + _pbGold + " -> " + g;
                else if (bu != _pbBudget || tr != _pbTrib || ca != _pbCall || me != _pbMerc) what = "portfele krolestwa";
                else if (Math.Abs(postLand - preLand) > 1e-9) what = "StableLand " + preLand + " -> " + postLand;
                else if (_pbRng.Ok && rng.Ok && (rng.X != _pbRng.X || rng.Y != _pbRng.Y || rng.Z != _pbRng.Z || rng.W != _pbRng.W)) what = "losowanie (MBFastRandom)";
                _ktCmp++; _ktCmpSess++;
                if (what != null)
                {
                    _ktDiff++; _ktDiffSess++;
                    if (_ktDiffNote == null) _ktDiffNote = (c.Name != null ? c.Name.ToString() : c.StringId) + ": " + what;
                }
            }
            catch (Exception e) { KtStumble("sonda po", e); }
        }

        private static readonly HashSet<string> _ktErrLogged = new HashSet<string>();
        private static void KtStumble(string site, Exception e)
        {
            _ktErr++; _ktErrSess++;
            try { if (_ktErrLogged.Add(site)) Log.Error("KingdomTreasury.Optim105(" + site + ")", e); } catch { }
        }

        // ------------------------------------------------------------ straznik
        /// <summary>null = mozna pomijac; inaczej powod (do linii). Codziennie: lancuch modelu i latki na sciezce (cudza latka moze dojsc w trakcie sesji).</summary>
        private static string KtGuard()
        {
            if (!_ktActualTried)
            {
                _ktActualTried = true;
                try
                {
                    var ext = AccessTools.TypeByName("BannerKings.Extensions.ClanExtensions");
                    var m = ext != null ? AccessTools.Method(ext, "GetActualVillages", new[] { typeof(Clan) }) : null;
                    if (m != null && m.IsStatic && m.ReturnType == typeof(List<Village>))
                        _ktActual = (Func<Clan, List<Village>>)Delegate.CreateDelegate(typeof(Func<Clan, List<Village>>), m);
                }
                catch (Exception e) { _ktActual = null; KtStumble("GetActualVillages", e); }
            }
            if (_ktActual == null) return "brak BK ClanExtensions.GetActualVillages(Clan)";
            var model = Campaign.Current != null && Campaign.Current.Models != null ? Campaign.Current.Models.ClanFinanceModel : null;
            if (model == null) return "brak modelu finansow";
            string chainWhy = KtChain(model);
            if (chainWhy != null) return chainWhy;
            if (_ktPath == null) KtBuildPath();
            if (_ktPathMiss != null) return "brak typow BK sciezki: " + _ktPathMiss;
            var unknown = new List<string>();
            foreach (var m in _ktPath)
            {
                var info = Harmony.GetPatchInfo(m);
                if (info == null) continue;
                KtCheck(info.Prefixes, m, unknown); KtCheck(info.Postfixes, m, unknown);
                KtCheck(info.Transpilers, m, unknown); KtCheck(info.Finalizers, m, unknown);
                // Harmony 2.3+ ma tez ILManipulators (pole albo wlasciwosc - zalezy od wersji; w bibliotece kompilacji go nie ma)
                var ilf = AccessTools.Field(info.GetType(), "ILManipulators");
                var ilp = ilf == null ? AccessTools.Property(info.GetType(), "ILManipulators") : null;
                if (ilf != null) KtCheck(ilf.GetValue(info) as IEnumerable, m, unknown);
                else if (ilp != null) KtCheck(ilp.GetValue(info, null) as IEnumerable, m, unknown);
            }
            if (unknown.Count > 0) return "niesprawdzone latki na sciezce modelu: " + string.Join(", ", unknown.GetRange(0, Math.Min(4, unknown.Count)).ToArray()) + (unknown.Count > 4 ? " (+" + (unknown.Count - 4) + ")" : "");
            return null;
        }

        private static void KtCheck(IEnumerable patches, MethodBase target, List<string> unknown)
        {
            if (patches == null) return;
            foreach (var o in patches)
            {
                var p = o as HarmonyLib.Patch;
                var pm = p != null ? p.PatchMethod : null;
                var dt = pm != null ? pm.DeclaringType : null;
                string key = dt != null ? dt.Assembly.GetName().Name + ":" + dt.Name + "." + pm.Name : "?";
                if (!KtKnownPatches.Contains(key)) unknown.Add(key + " na " + (target.DeclaringType != null ? target.DeclaringType.Name : "?") + "." + target.Name);
            }
        }

        /// <summary>Lancuch czynnego modelu (BaseModel i pola typu ClanFinanceModel, do 12 wezlow): tylko ROT / NavalDLC (przekazuja CalculateClanIncome dalej
        /// 1:1), BK (gra + AddIncomes) i gra; BK musi byc. null = znany.</summary>
        private static string KtChain(object model)
        {
            var seen = new List<object>(); var queue = new Queue<object>(); var names = new List<string>();
            queue.Enqueue(model); _ktBk = null;
            bool bk = false;
            while (queue.Count > 0 && seen.Count < 12)
            {
                var m = queue.Dequeue();
                bool dup = false;
                for (int i = 0; i < seen.Count; i++) if (ReferenceEquals(seen[i], m)) { dup = true; break; }
                if (dup) continue;
                seen.Add(m);
                var t = m.GetType();
                string fn = t.FullName;
                names.Add(t.Name);
                if (fn == TBk) { bk = true; _ktBk = m; }
                else if (t != typeof(DefaultClanFinanceModel) && fn != TRot && fn != TNaval) return "nieznany model w lancuchu: " + fn;
                try { var bp = AccessTools.Property(t, "BaseModel"); var v = bp != null ? bp.GetValue(m, null) : null; if (v != null && !ReferenceEquals(v, m)) queue.Enqueue(v); } catch { }
                for (var tt = t; tt != null && tt != typeof(object); tt = tt.BaseType)
                    foreach (var f in tt.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    {
                        if (!typeof(ClanFinanceModel).IsAssignableFrom(f.FieldType)) continue;
                        object v = null;
                        try { v = f.GetValue(m); } catch { v = null; }
                        if (v != null && !ReferenceEquals(v, m)) queue.Enqueue(v);
                    }
            }
            if (queue.Count > 0) return "lancuch modelu dluzszy niz 12 (" + string.Join(" -> ", names.ToArray()) + ")";
            if (!bk) return "brak BKClanFinanceModel w lancuchu (" + string.Join(" -> ", names.ToArray()) + ")";
            if (!_ktProbeTried)
            {
                _ktProbeTried = true;
                try { _ktWsTax = AccessTools.Field(_ktBk.GetType(), "workshopTaxes"); } catch { _ktWsTax = null; }
                try { _ktBaseCalls = AccessTools.Field(typeof(CouncilWages35), "_baseCalls"); } catch { _ktBaseCalls = null; }
                if (_ktBaseCalls != null && (!_ktBaseCalls.IsStatic || _ktBaseCalls.FieldType != typeof(long))) _ktBaseCalls = null;
            }
            return null;
        }

        /// <summary>Metody, ktore model dochodu wola dla rodu wasalnego bez lenn (dekompilacja: gra 1.4.8 CalculateClanIncomeInternal, BK, ROT, NavalDLC).</summary>
        private static void KtBuildPath()
        {
            var list = new List<MethodBase>(); var miss = new List<string>();
            KtAdd(list, typeof(DefaultClanFinanceModel), miss, "CalculateClanIncome", "CalculateClanIncomeInternal", "AddMercenaryIncome", "AddSettlementIncome",
                "CalculateHeroIncomeFromWorkshops", "AddIncomeFromParties", "AddIncomeFromParty", "AddIncomeFromTribute", "CalculateShareFactor",
                "AddIncomeFromCallToWarAgrements", "AddIncomeFromTradeAgreements", "AddIncomeFromKingdomBudget");
            KtAdd(list, AccessTools.TypeByName(TRot), null, "CalculateClanIncome");       // bez ROT / NavalDLC lancuch i tak ich nie ma
            KtAdd(list, AccessTools.TypeByName(TNaval), null, "CalculateClanIncome");
            KtAdd(list, KtNeed(TBk, miss), miss, "CalculateClanIncome", "AddIncomes", "CalculateOwnerIncomeFromEstates");
            KtAdd(list, KtNeed("BannerKings.Managers.TitleManager", miss), miss, "CalculateVassals", "CalculateHeroSuzerain", "GetAllDeJure", "GetHighestTitle",
                "GetImmediateSuzerain", "GetTitleFaction", "GetHighestTitleWithinFaction");
            KtAdd(list, KtNeed("BannerKings.Managers.CourtManager", miss), miss, "GetHeroPositions");
            KtAdd(list, KtNeed("BannerKings.Managers.PopulationManager", miss), miss, "GetEstates");
            KtAdd(list, KtNeed("BannerKings.Behaviours.Retainer.BKRetainerBehavior", miss), miss, "GetContract");
            KtAdd(list, KtNeed("BannerKings.Extensions.ClanExtensions", miss), miss, "GetActualVillages");
            KtAdd(list, KtNeed("BannerKings.Utils.Extensions.HeroExtensions", miss), miss, "GetVillages");
            _ktPath = list;
            _ktPathMiss = miss.Count > 0 ? string.Join(", ", miss.ToArray()) : null;
        }

        private static Type KtNeed(string name, List<string> miss)
        {
            var t = AccessTools.TypeByName(name);
            if (t == null) miss.Add(name);
            return t;
        }

        private static void KtAdd(List<MethodBase> list, Type t, List<string> miss, params string[] names)
        {
            if (t == null) return;
            foreach (var n in names)
            {
                bool any = false;
                foreach (var m in AccessTools.GetDeclaredMethods(t))
                    if (m.Name == n && !m.IsAbstract) { list.Add(m); any = true; }
                if (!any && miss != null) miss.Add(t.Name + "." + n);
            }
        }

        // ------------------------------------------------------------ linie
        /// <summary>Koniec petli rodow: linia doby (przy LogEnabled) - pominiete, straznik, kontrola.</summary>
        private static void KtSkipEnd(Settings s)
        {
            _ktBk = null;   // model BK (graf kampanii) potrzebny tylko sondom w petli rodow - nie trzymac do nastepnej doby / kampanii
            try
            {
                if (s == null || !s.LogEnabled || !s.OptimKtSkipModel) return;
                var sb = new System.Text.StringBuilder(256);
                sb.Append("Optym (m105): dzien ").Append((int)CampaignTime.Now.ToDays).Append(" | KingdomTreasury.Daily model dochodu: ");
                if (!_ktSkipOn) sb.Append("pominiecie WYLACZONE - ").Append(_ktGuard ?? "-");
                else
                {
                    sb.Append("pominiety u ").Append(_ktSkip).Append(" rodow (z ").Append(_ktCand).Append(" bez lenn i warsztatu glowy z podstawa ziemia; sesja ").Append(_ktSkipSess).Append(')');
                    if (_ktSkipVerify)
                        sb.Append(" | kontrola (Optim Verify) model bez lenn: porownan ").Append(_ktCmp).Append(", roznic ").Append(_ktDiff)
                          .Append(" (sesja: porownan ").Append(_ktCmpSess).Append(", roznic ").Append(_ktDiffSess).Append(')')
                          .Append(_ktDiffNote != null ? "; pierwsza: " + _ktDiffNote : "");
                }
                sb.Append(" | potkniecia ").Append(_ktErr).Append(" (sesja ").Append(_ktErrSess).Append(").");
                Log.Info(sb.ToString());
            }
            catch { }
        }

        /// <summary>Start kampanii / wczytanie (z OptimStartLine): liczniki sesji od zera.</summary>
        private static void KtSkipReset()
        {
            _ktSkipSess = _ktCmpSess = _ktDiffSess = _ktErrSess = 0;
            _ktErrLogged.Clear(); _ktBk = null; _ktGuard = "-"; _ktSkipOn = false; _ktSkipVerify = false;
        }
    }
}
