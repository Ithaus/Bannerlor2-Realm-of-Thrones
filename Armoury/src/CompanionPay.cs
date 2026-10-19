using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// CENA WERBUNKU TOWARZYSZA WEDLUG ZOLDU (decyzja Jeffa 15.10 TAK; DECYZJA CLAUDE noc 15/16.10: polowa sprzetu + 20 dni zoldu, umiejetnosci morskie 3 / 10 / 10).
    /// Dowod [K]: cena (BK BKCompanionPrices.GetHiringPrice, dec-bk :17-93) = sprzet bojowy i cywilny po cenie targu / 2 + 10 x poziom + suma (umiejetnosc > 30) x czynnik
    /// (GetCostFactor :109-125: 3 bojowe, 6 urok / lotrostwo, 10 zwiad / kowalstwo / medycyna / inzynieria / handel, 15 reszta); premia za umiejetnosci jest poza
    /// naszymi cenami i robi 90-97% ceny postaci ROT (mediana ok. 14.8 tys. d przy 2.9 tys. zlota gracza - CrashScribe 10-56-29 12:10:57 [P - szacunek poziomu]),
    /// a umiejetnosci NavalDLC (Mariner, Boatswain, Shipmaster - ROT-Content spspecialcharacters_ROTcustomwanderers.xml: 100-200 u wszystkich 63 postaci) BK,
    /// nie znajac ich, liczy jak dowodzenie (15) - ok. 40% premii.
    /// (C1) CompanionNavalSkillsPriced: postfiks na BKCompanionPrices.GetCostFactor (metoda zadeklarowana w BK - pulapka Harmony z 15.10): zeglarz 3 (jak jazda i atletyka),
    /// bosman i kapitan 10 (role w druzynie jak zwiad i inzynieria). Ta jedna regula BK dziala w cenie werbunku, w zoldzie bohatera (GetHeroWage - BKPartyWageModel.cs:336,
    /// takze bohaterowie w partiach AI) i w kontrakcie BK gracza-najemnika (Contract.cs:42, 54). SAMOSPRAWDZENIE po wpieciu: czynniki i zold jednego wedrowca z
    /// umiejetnosciami morskimi wobec wlasnego rachunku; zapas OSOBNO dla kazdej metody (poprawka po recenzji 16.10): zold (GetHeroWage) i cena bez sprzetu
    /// (GetHiringPrice) porownane z wynikiem z nowymi czynnikami i z wynikiem BK (15) - zapas danej metody (postfiks odejmuje czesc morska) wlacza sie tylko wtedy,
    /// gdy ta metoda daje wynik BK (latka czynnika w niej niewidoczna); wynik ani nowy, ani BK - rozjazd bez zapasu (nie odejmujemy drugi raz). Nasz wlasny rachunek
    /// (SkillSum) bierze czynniki morskie wprost (NavalFactor), nie przez metode BK - jest niezalezny od tego, czy latka dziala.
    /// (C2) CompanionHireByWage: postfiks na GetCompanionHiringPrice zadeklarowanej w typie czynnego modelu (DeclaringType -> DeclaredMethod): cena = czesc sprzetu
    /// BK (cena z BK - cena BK bez sprzetu = sprzet / 2 x perki) + CompanionHireDays x zold dzienny x perki; zold = (int)max((2 + 2 x poziom) x BaseWage BK, 1) + suma
    /// umiejetnosci x czynnik x 0.005 (oczekiwana wartosc BK GetHeroWage); perki gracza (PaidInPromise, GreatInvestor) = cena BK bez sprzetu / (10 x poziom + suma).
    /// 20 dni = zaciag najemnika (RecruitCostDays 10 x 2). Dialog woluje model 3 razy (LordConversationsCampaignBehavior.cs:1987/2018/2030) - wynik deterministyczny,
    /// pokazana = zaplacona; zloto dalej gracz -> towarzysz (GiveGoldAction). Kontrakt BK uzywa GetHiringPrice wprost - bez C2. Bez BK (bez GetHiringPrice) - nic.
    /// Latki w kampanii (OnSessionLaunched), raz na proces. Nic w zapisie. Wyjatek - potkniecie (licznik, pierwsze do Log.Error), wynik modelu zostaje.
    /// </summary>
    internal static class CompanionPay
    {
        private static Harmony _h;
        private static bool _factorHooked, _priceHooked;
        private static bool _fbWage, _fbHire;               // zapas C1 czynny w tej sesji: zold (GetHeroWage) / cena bez sprzetu (GetHiringPrice) - kazdy osobno z samosprawdzenia
        private static bool _fbWageHooked, _fbHireHooked;   // postfiksy zapasu wpiete (raz na proces; dzialaja tylko przy fladze sesji)
        private static Type _bkType;
        private static MethodInfo _mFactor, _mHiring, _mWage;
        private static PropertyInfo _pSet, _pBaseWage;
        private static float _baseWage = 1f;
        /// <summary>BaseWage BK tej sesji (CompanionWage: czesc zoldu bohatera, ktora BK liczy od TroopWage).</summary>
        internal static float BkBaseWage { get { return _baseWage; } }
        private static string _hook = "nie wpiete", _check = "-";
        [ThreadStatic] private static bool _bypass;   // linia doby i samosprawdzenie: cena BK bez naszych latek (C1 i C2)
        [ThreadStatic] private static bool _stat;     // linia doby: wyceny statystyki nie licza sie do "wycen w grze"
        private static readonly object Gate = new object();
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static int _stumbles, _stDay, _priced;
        private static bool _errLogged;
        private static readonly Dictionary<object, Func<SkillObject, int>> _factorOf = new Dictionary<object, Func<SkillObject, int>>();

        private static void Stumble(string where, Exception e)
        {
            lock (Gate) { _stumbles++; _stDay++; }
            if (!_errLogged) { _errLogged = true; Log.Error("CompanionPay." + where, e); }
        }

        private static int NavalFactor(string id)
        {
            if (id == "Mariner") return 3;
            if (id == "Boatswain" || id == "Shipmaster") return 10;
            return 0;
        }

        private static bool NavalOn { get { var s = Settings.Current; return s != null && s.CompanionNavalSkillsPriced; } }
        private static bool HireOn { get { var s = Settings.Current; return s != null && s.CompanionHireByWage; } }

        // ------------------------------------------------------------ C1: czynnik umiejetnosci morskich
        public static void FactorPostfix(SkillObject __0, ref int __result)
        {
            if (_bypass || __0 == null || !NavalOn) return;
            int f = NavalFactor(__0.StringId);
            if (f > 0) __result = f;
        }

        /// <summary>Zapas C1 (metoda BK nie widzi latki czynnika): roznica czesci morskiej bohatera (umiejetnosc > 30 x (15 - nowy czynnik)).</summary>
        private static float NavalCut(Hero h)
        {
            float cut = 0f;
            foreach (var id in new[] { "Mariner", "Boatswain", "Shipmaster" })
            {
                var sk = MBObjectManager.Instance.GetObject<SkillObject>(id);
                if (sk == null) continue;
                int v = h.GetSkillValue(sk);
                if (v > 30) cut += v * (15 - NavalFactor(id));
            }
            return cut;
        }

        public static void WageFallbackPostfix(Hero __0, ref int __result)
        {
            try { if (_bypass || !_fbWage || __0 == null || !NavalOn || CompanionWage.Applies(__0)) return; __result = Math.Max(0, __result - (int)Math.Round(NavalCut(__0) * 0.005f)); }
            catch (Exception e) { Stumble("WageFallbackPostfix", e); }
        }

        public static void HiringFallbackPostfix(Hero __0, ref int __result)
        {
            try { if (_bypass || !_fbHire || __0 == null || !NavalOn) return; __result = Math.Max(1, __result - (int)Math.Round(NavalCut(__0))); }   // perki gracza pominiete (ok. 1)
            catch (Exception e) { Stumble("HiringFallbackPostfix", e); }
        }

        // ------------------------------------------------------------ C2: cena werbunku
        private static Func<SkillObject, int> FactorOf(object model)
        {
            Func<SkillObject, int> f;
            lock (Gate) if (_factorOf.TryGetValue(model, out f)) return f;
            f = (Func<SkillObject, int>)Delegate.CreateDelegate(typeof(Func<SkillObject, int>), model, _mFactor);
            lock (Gate) _factorOf[model] = f;
            return f;
        }

        /// <summary>Suma umiejetnosci (> 30) x czynnik: przy wlaczonym C1 (i bez _bypass) czynniki morskie wprost z NavalFactor, reszta przez metode BK (delegat) -
        /// wynik nie zalezy od tego, czy latka czynnika jest widoczna w metodzie (poprawka po recenzji 16.10: dotad zapas odejmowal czesc morska drugi raz).
        /// Przy _bypass albo wylaczonym C1 - czynniki BK (15).</summary>
        private static float SkillSum(object model, Hero h)
        {
            var f = FactorOf(model);
            bool naval = NavalOn && !_bypass;
            float sum = 0f;
            foreach (var sk in MBObjectManager.Instance.GetObjectTypeList<SkillObject>())
            {
                if (sk == null) continue;
                int v = h.GetSkillValue(sk);
                if (!(v > 30)) continue;
                int nf = naval ? NavalFactor(sk.StringId) : 0;
                sum += (float)v * (nf > 0 ? nf : f(sk));
            }
            return sum;
        }

        /// <summary>Zold dzienny towarzysza wedlug BK (oczekiwana wartosc): (int)max((2 + 2 x poziom) x BaseWage, 1) + suma umiejetnosci x czynnik x 0.005.</summary>
        private static float WageOf(int level, float skills)
        {
            return (int)Math.Max((2 + 2 * level) * _baseWage, 1f) + skills * 0.005f;
        }

        /// <summary>Nasza cena z wyniku BK (withGear) - ta sama w dialogu i w linii doby.</summary>
        private static int OurPrice(object model, Hero h, int withGear, out float wage)
        {
            wage = 0f;
            var s = Settings.Current;
            int noGear = (int)_mHiring.Invoke(model, new object[] { h, false });
            int level = h.CharacterObject != null ? h.CharacterObject.Level : h.Level;
            float skills = SkillSum(model, h);
            float raw0 = 10f * level + skills;
            float perk = raw0 > 0.5f ? noGear / raw0 : 1f;
            if (!(perk > 0.05f) || perk > 5f) perk = 1f;
            float gear = Math.Max(0f, withGear - noGear);
            wage = CompanionWage.WageFor(level, WageOf(level, skills));   // CW (19.10): przy CompanionWageByLevel zold z tabeli Jeffa wedlug poziomu
            return Math.Max(1, (int)Math.Round(gear + Math.Max(0f, s.CompanionHireDays) * wage * perk));
        }

        public static void PricePostfix(object __instance, Hero __0, ref int __result)
        {
            if (_bypass || __0 == null || __instance == null || !HireOn || _mHiring == null || _mFactor == null || !_bkType.IsInstanceOfType(__instance)) return;
            try
            {
                float w;
                int p = OurPrice(__instance, __0, __result, out w);
                __result = p;
                if (!_stat) lock (Gate) _priced++;
            }
            catch (Exception e) { Stumble("PricePostfix", e); }
        }

        // ------------------------------------------------------------ wpiecie i samosprawdzenie (OnSessionLaunched)
        internal static void SessionStart()
        {
            const string Head = "Towarzysze (C): start sesji | ";
            try
            {
                var s = Settings.Current;
                lock (Gate) _factorOf.Clear();   // nowa kampania = nowe modele BK
                _fbWage = _fbHire = false;        // zapas od nowa - samosprawdzenie tej sesji mierzy metody BK bez niego
                if (_h == null) _h = new Harmony("com.jeff.armoury.companionpay");
                if (_bkType == null) _bkType = AccessTools.TypeByName("BannerKings.Models.Vanilla.BKCompanionPrices");
                if (_bkType == null) { Log.Info(Head + "bez Banner Kings (BKCompanionPrices) - cena i zold towarzyszy jak w grze | potkniecia 0."); return; }
                _mFactor = AccessTools.DeclaredMethod(_bkType, "GetCostFactor", new[] { typeof(SkillObject) });
                _mHiring = AccessTools.DeclaredMethod(_bkType, "GetHiringPrice", new[] { typeof(Hero), typeof(bool) });
                _mWage = AccessTools.DeclaredMethod(_bkType, "GetHeroWage", new[] { typeof(Hero) });
                if (!_factorHooked && _mFactor != null)
                {
                    _h.Patch(_mFactor, postfix: new HarmonyMethod(typeof(CompanionPay), nameof(FactorPostfix)) { priority = Priority.Last });
                    _factorHooked = true;
                }
                // C2: metoda zadeklarowana w typie czynnego modelu (pulapka Harmony 15.10 - nie przez model.GetType())
                var model = Campaign.Current != null && Campaign.Current.Models != null ? Campaign.Current.Models.CompanionHiringPriceCalculationModel : null;
                MethodInfo decl = null;
                if (model != null)
                {
                    var mm = AccessTools.Method(model.GetType(), "GetCompanionHiringPrice", new[] { typeof(Hero) });
                    if (mm != null && mm.DeclaringType != null) decl = AccessTools.DeclaredMethod(mm.DeclaringType, "GetCompanionHiringPrice", new[] { typeof(Hero) });
                }
                if (!_priceHooked && decl != null && _mHiring != null && _bkType.IsAssignableFrom(decl.DeclaringType))
                {
                    _h.Patch(decl, postfix: new HarmonyMethod(typeof(CompanionPay), nameof(PricePostfix)) { priority = Priority.Last });
                    _priceHooked = true;
                }
                _hook = "czynnik " + (_factorHooked ? "wpiety (" + _bkType.Name + ".GetCostFactor)" : "BRAK") + ", cena " + (_priceHooked ? "wpieta (" + (decl != null ? decl.DeclaringType.Name : _bkType.Name) + ".GetCompanionHiringPrice)"
                        : decl == null ? "BRAK metody czynnego modelu" : "NIE - czynny model " + (model != null ? model.GetType().Name : "?") + " nie jest BK");
                // BaseWage BK (raz na sesje; BKPartyWageModel czyta ja raz na proces)
                _baseWage = 1f;
                try
                {
                    var setT = AccessTools.TypeByName("BannerKings.Settings.BannerKingsSettings");
                    _pSet = setT != null ? AccessTools.Property(setT, "Instance") : null;
                    _pBaseWage = setT != null ? AccessTools.Property(setT, "BaseWage") : null;
                    var inst = _pSet != null ? _pSet.GetValue(null, null) : null;
                    if (inst != null && _pBaseWage != null) { float bw = Convert.ToSingle(_pBaseWage.GetValue(inst, null)); if (bw > 0f) _baseWage = bw; }
                }
                catch (Exception e) { Stumble("SessionStart(BaseWage)", e); }
                SelfCheck(model, s, Head);
            }
            catch (Exception e) { Stumble("SessionStart", e); Log.Info(Head + "wyjatek przy wpieciu (ERROR w logu) - " + _hook + " | potkniecia " + _stumbles + "."); }
        }

        private static void SelfCheck(object model, Settings s, string head)
        {
            var parts = new List<string>();
            bool factorOk = true;
            object bk = model != null && _bkType.IsInstanceOfType(model) ? model : null;
            if (bk == null)
            {
                try
                {
                    var cfgT = AccessTools.TypeByName("BannerKings.BannerKingsConfig");
                    var cfg = cfgT != null ? AccessTools.Property(cfgT, "Instance").GetValue(null, null) : null;
                    bk = cfg != null ? AccessTools.Property(cfgT, "CompanionModel").GetValue(cfg, null) : null;
                }
                catch { bk = null; }
            }
            head += "klucz CompanionNavalSkillsPriced " + (NavalOn ? "TAK" : "NIE") + ", CompanionHireByWage " + (HireOn ? "TAK" : "NIE") + " (" + Math.Max(0f, s != null ? s.CompanionHireDays : 0f).ToString("0", Inv) + " dni) | ";
            if (bk == null || _mFactor == null) { Log.Info(head + _hook + " | samosprawdzenie: BRAK modelu BK | potkniecia " + _stumbles + "."); return; }
            var f = FactorOf(bk);
            foreach (var id in new[] { "Mariner", "Boatswain", "Shipmaster" })
            {
                var sk = MBObjectManager.Instance.GetObject<SkillObject>(id);
                if (sk == null) { parts.Add(id + " brak umiejetnosci"); continue; }
                int got = f(sk), want = NavalOn ? NavalFactor(id) : 15;
                if (got != want) factorOk = false;
                parts.Add(id + " " + got + " (oczekiwane " + want + ")");
            }
            // wedrowiec z najwieksza czescia morska (wyrazna roznica wyniku z nowymi czynnikami i wyniku BK)
            string wageTxt = "zold: brak wedrowca z umiejetnosciami morskimi", hireTxt = "cena bez sprzetu: -", priceTxt = "";
            bool wageOk = true, hireOk = true, wageBk = false, hireBk = false;
            Hero probe = null;
            try
            {
                float best = 0f;
                foreach (var h in Hero.AllAliveHeroes)
                {
                    if (h == null || !h.IsWanderer || h.IsPlayerCompanion) continue;   // CW: towarzysz gracza ma zold z tabeli - probka mierzy BK
                    float c = NavalCut(h);
                    if (c > best) { best = c; probe = h; }
                }
                if (probe != null)
                {
                    float sOurs = SkillSum(bk, probe), s15;
                    _bypass = true;
                    try { s15 = SkillSum(bk, probe); }
                    finally { _bypass = false; }
                    // zold: GetHeroWage BK wobec rachunku z nowymi czynnikami i z czynnikami BK (BK zaokragla losowo - roznica < 1)
                    if (_mWage != null)
                    {
                        float want = sOurs * 0.005f, want15 = s15 * 0.005f;
                        int got = (int)_mWage.Invoke(bk, new object[] { probe });
                        wageOk = Math.Abs(got - want) < 1.0001f;
                        wageBk = !wageOk && Math.Abs(got - want15) < 1.0001f;
                        wageTxt = "zold BK " + probe.Name + " " + got + " (oczekiwane " + want.ToString("0.0", Inv) + ", BK bez latki " + want15.ToString("0.0", Inv) + ") - "
                                  + (wageOk ? "zgodny" : wageBk ? "rozjazd: latka czynnika niewidoczna" : "rozjazd nieznany");
                    }
                    // cena bez sprzetu: GetHiringPrice BK (perki gracza w obu wynikach) wobec wyniku BK bez latki x (10 x poziom + suma nowa) / (10 x poziom + suma BK)
                    if (_mHiring != null)
                    {
                        int p15, p;
                        _bypass = true;
                        try { p15 = (int)_mHiring.Invoke(bk, new object[] { probe, false }); }
                        finally { _bypass = false; }
                        p = (int)_mHiring.Invoke(bk, new object[] { probe, false });
                        int lv = probe.CharacterObject != null ? probe.CharacterObject.Level : probe.Level;
                        float r15 = 10f * lv + s15;
                        float exp = r15 > 0.5f ? p15 * (10f * lv + sOurs) / r15 : p15;
                        float tol = 2f + 0.002f * Math.Abs(exp);
                        hireOk = Math.Abs(p - exp) <= tol;
                        hireBk = !hireOk && Math.Abs(p - p15) <= tol;
                        hireTxt = "cena bez sprzetu " + p + " (oczekiwane " + exp.ToString("0", Inv) + ", BK bez latki " + p15 + ") - "
                                  + (hireOk ? "zgodna" : hireBk ? "rozjazd: latka czynnika niewidoczna" : "rozjazd nieznany");
                    }
                }
            }
            catch (Exception e) { Stumble("SelfCheck(zold, cena)", e); wageTxt = "zold i cena: wyjatek (ERROR w logu)"; hireTxt = "-"; wageOk = hireOk = false; }
            // zapas C1 OSOBNO dla kazdej metody: tylko gdy ta metoda daje wynik BK (latka niewidoczna); bez wedrowca do pomiaru - wedlug czynnika przez metode BK.
            // Wynik ani nowy, ani BK - bez zapasu (nie odejmujemy czesci morskiej drugi raz)
            bool needW = NavalOn && (probe != null ? wageBk : !factorOk);
            bool needH = NavalOn && (probe != null ? hireBk : !factorOk);
            try
            {
                if (needW && !_fbWageHooked && _mWage != null) { _h.Patch(_mWage, postfix: new HarmonyMethod(typeof(CompanionPay), nameof(WageFallbackPostfix)) { priority = Priority.Last }); _fbWageHooked = true; }
                if (needH && !_fbHireHooked && _mHiring != null) { _h.Patch(_mHiring, postfix: new HarmonyMethod(typeof(CompanionPay), nameof(HiringFallbackPostfix)) { priority = Priority.Last }); _fbHireHooked = true; }
            }
            catch (Exception e) { Stumble("SelfCheck(zapas)", e); }
            _fbWage = needW && _fbWageHooked; _fbHire = needH && _fbHireHooked;
            // cena calego werbunku po decyzji o zapasie (ta, ktora zobaczy gracz)
            if (probe != null && _priceHooked && HireOn && model != null)
            {
                try
                {
                    int bkPrice, ours;
                    _bypass = true;
                    try { bkPrice = Campaign.Current.Models.CompanionHiringPriceCalculationModel.GetCompanionHiringPrice(probe); }
                    finally { _bypass = false; }
                    _stat = true;
                    try { ours = Campaign.Current.Models.CompanionHiringPriceCalculationModel.GetCompanionHiringPrice(probe); }
                    finally { _stat = false; }
                    priceTxt = "; cena " + bkPrice + " -> " + ours + " d (poziom " + probe.Level + ")";
                }
                catch (Exception e) { Stumble("SelfCheck(cena)", e); priceTxt = "; cena: wyjatek (ERROR w logu)"; hireOk = false; }
            }
            bool allOk = factorOk && wageOk && hireOk;
            string fb = _fbWage || _fbHire ? " (zapas: " + (_fbWage ? "GetHeroWage" : "") + (_fbWage && _fbHire ? " i " : "") + (_fbHire ? "GetHiringPrice" : "") + " odejmuje czesc morska)"
                      : (allOk ? "" : " (zapas nie wlaczony)");
            _check = string.Join(", ", parts.ToArray()) + "; " + wageTxt + "; " + hireTxt + priceTxt;
            Log.Info(head + _hook + " | samosprawdzenie (czynnik: " + (factorOk ? "zgodny" : "rozjazd") + ", zold: " + (probe == null || _mWage == null ? "-" : wageOk ? "zgodny" : "rozjazd")
                     + ", cena: " + (probe == null || _mHiring == null ? "-" : hireOk ? "zgodna" : "rozjazd") + ") - czynnik " + _check + (allOk ? " - ZGODNE" : " - ROZJAZD" + fb)
                     + " | umiejetnosci morskie wedlug rol " + (NavalOn ? "tak" : "nie (WYLACZONE)") + " | cena wedlug zoldu " + (HireOn ? "tak (" + Math.Max(0f, s.CompanionHireDays).ToString("0", Inv) + " dni, BaseWage " + _baseWage.ToString("0.00", Inv) + ")" : "nie (WYLACZONE)")
                     + " | potkniecia " + _stumbles + ".");
        }

        private static string Med(List<float> l, string fmt)
        {
            if (l.Count == 0) return "-";
            l.Sort();
            return l[l.Count / 2].ToString(fmt, Inv) + " (" + l[0].ToString(fmt, Inv) + "-" + l[l.Count - 1].ToString(fmt, Inv) + ")";
        }

        /// <summary>Z ArmouryBehavior.OnDailyTick: linia "Towarzysze (C): dzien" - wedrowcy w osadach, cena BK (bez naszych latek) i nasza, zold, poziom (tylko log).</summary>
        internal static void Daily()
        {
            var s = Settings.Current;
            if (s == null || _bkType == null || Campaign.Current == null) return;
            var model = Campaign.Current.Models.CompanionHiringPriceCalculationModel;
            var bkL = new List<float>(); var ourL = new List<float>(); var wL = new List<float>(); var lvL = new List<float>(); var rL = new List<float>();
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            int n = 0;
            if (model != null && _mHiring != null && _mFactor != null && _bkType.IsInstanceOfType(model))
            {
                foreach (var st in Settlement.All)
                {
                    if (st == null || (!st.IsTown && !st.IsCastle)) continue;
                    foreach (var h in st.HeroesWithoutParty)
                    {
                        try
                        {
                            if (h == null || !h.IsWanderer || h.Clan != null || !h.IsAlive) continue;
                            int bkPrice;
                            _bypass = true;
                            try { bkPrice = model.GetCompanionHiringPrice(h); }
                            finally { _bypass = false; }
                            int ours;
                            _stat = true;
                            try { ours = model.GetCompanionHiringPrice(h); }
                            finally { _stat = false; }
                            int level = h.CharacterObject != null ? h.CharacterObject.Level : h.Level;
                            float w = CompanionWage.WageFor(level, WageOf(level, SkillSum(model, h)));
                            n++;
                            bkL.Add(bkPrice); ourL.Add(ours); wL.Add(w); lvL.Add(level);
                            if (bkPrice > 0) rL.Add(ours / (float)bkPrice);
                        }
                        catch (Exception e) { Stumble("Daily(wedrowiec)", e); }
                    }
                }
            }
            double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            int sd, pr;
            lock (Gate) { sd = _stDay; pr = _priced; _stDay = 0; _priced = 0; }
            Log.Info("Towarzysze (C): dzien " + (int)CampaignTime.Now.ToDays + " | umiejetnosci morskie wedlug rol " + (NavalOn ? "tak" : "nie") + ", cena wedlug zoldu " + (HireOn ? "tak (" + Math.Max(0f, s.CompanionHireDays).ToString("0", Inv) + " dni)" : "nie")
                     + ", zold wedlug poziomu " + (CompanionWage.On ? "tak" : "nie")
                     + (_fbWage || _fbHire ? ", ZAPAS C1 czynny (" + (_fbWage ? "zold" : "") + (_fbWage && _fbHire ? " i " : "") + (_fbHire ? "cena" : "") + ")" : "") + " | wedrowcy w osadach " + n + " | cena BK mediana " + Med(bkL, "0") + " -> nasza " + Med(ourL, "0")
                     + " | zold/d mediana " + Med(wL, "0.0") + " (poziom mediana " + Med(lvL, "0") + ") | nasza/BK mediana " + Med(rL, "0.00")
                     + " | wycen w grze od wczoraj " + pr + " | gracz: zloto " + (Hero.MainHero != null ? Hero.MainHero.Gold : 0) + " | czas " + ms.ToString("0.0", Inv) + " ms | potkniecia " + sd + " (sesja " + _stumbles + ").");
        }
    }
}
