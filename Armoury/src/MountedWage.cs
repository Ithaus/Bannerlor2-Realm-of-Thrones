using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// KONNY BIERZE WIEKSZY ZOLD (paczka 160, decyzja Jeffa 08.10: "albo najemnik ma konia, wtedy jest konny, albo przychodzi bez konia,
    /// wtedy jest pieszy ... jak ma konia, to chce wiekszy zold pewnie"; wylacznik MountedWagePremium, suwak MountedWageFactor).
    ///
    /// Sprawdzone w kodzie (1.4.8, czynny model BKROTPatch.Models.BKROTPartyWageModel : BannerKings BKPartyWageModel : DefaultPartyWageModel):
    /// zold jednostki to DefaultPartyWageModel.GetCharacterWage - tylko tier (1, 2, 3, 5, 8, 12, 17, 23) i najemnik x1.5; BK i BKROTPatch
    /// go nie nadpisuja (BK liczy zold partii z CharacterObject.TroopWage = GetCharacterWage, razy BaseWage 1.25 z MCM BK, kultura,
    /// garnizon -50%, perki), ROT (ROTPartyWageModel - nieczynny) zmienia tylko Unsullied i Innych. Konny bierze tyle samo co pieszy
    /// tego samego tieru - jedyna roznica w BK to perk CataphractEquites (-10% zoldu za konnych). Historycznie konny ok. x2 (Anglia,
    /// kampania Crecy-Calais 1346-47, rachunki Garderoby: lucznik konny 6 d, pieszy 3 d, hobelar 6 d, piechur walijski 2 d, man-at-arms
    /// 12 d, rycerz 24 d - A. Ayton, Knights and Warhorses, 1994; M. Prestwich, Armies and Warfare in the Middle Ages, 1996).
    ///
    /// Teraz: postfiks na GetCharacterWage kazdego modelu zoldu (licznik zagniezdzenia - liczymy raz, na zewnatrz): jednostka konna
    /// (IsMounted, nie bohater) - zold x MountedWageFactor (domyslnie 1.5: t4 8 -> 12 d = man-at-arms, t6 17 -> 26 d ~ rycerz 24 d;
    /// x2 dawalby t4 16, t6 34 - ponad historie, bo piesi od tieru 3 sa w grze juz oplacani ponad historie, a 86% jednostek konnych ROT
    /// to tiery 4-6). Ta sama liczba idzie wszedzie, gdzie gra i mody czytaja zold jednostki: zold partii (BK), ekran druzyny, budzet
    /// zoldu AI (werbunek, jency), cena werbunku "dni zoldu" (RecruitCost), koszt awansu, limit zoldu i dezercja.
    /// Tylko partie, ktorych zold idzie do ludzi (SoldierPay: partia rodu -> sakiewki ludzi, garnizon -> kasa osady): partie rodow
    /// (takze gracz) i garnizony. Karawana (zold w nicosc jak w grze) i inne partie - bez premii: kontekst partii z GetTotalWage
    /// (zold partii) i z RecruitmentCampaignBehavior.CheckRecruiting (werbunek AI - karawana placi dni zoldu bez premii).
    ///
    /// Kiedy latamy (pulapka z SoldierPay): GetCharacterWage i CheckRecruiting - przy starcie gry (ciala bez pol statycznych; te same
    /// klasy co latki RecruitCost); GetTotalWage modeli (BK, BKROTPatch czytaja statyczne singletony BK) - dopiero w kampanii, przy
    /// OnSessionLaunched (EnsureContextHooks), raz na klase. Linia dnia "Zold konnych (160)": sklad wojska (konni / wszyscy) i premia
    /// wedle stawek jednostek - tylko log.
    ///
    /// Sluzba bez zoldu w zlocie (jedna regula, CrownGifts): Straz (182) i Wolni Ludzie (WL1) - zold jednostki 0 w zoldzie partii (WagePostfix);
    /// zalogi okretow Zelaznych Wysp (WW-J, IronbornHalfWageInPlunder) - polowa zoldu partii w zlocie (TotalPostfix na GetTotalWage).
    /// </summary>
    internal static class MountedWage
    {
        [ThreadStatic] private static int _depth;
        [ThreadStatic] private static MobileParty _party;   // partia, ktorej zold (GetTotalWage) albo werbunek (CheckRecruiting) liczymy; null = poza nimi
        [ThreadStatic] private static bool _raw;            // linia dnia: stawka jednostki bez premii
        [ThreadStatic] private static bool _total;          // 182: liczymy zold partii (GetTotalWage) - nie cene werbunku (CheckRecruiting)

        private static Harmony _harmony;
        private static readonly HashSet<Type> _totalHooked = new HashSet<Type>();
        private static int _wageModels, _recruitHook;

        internal static bool On { get { var s = Settings.Current; return s != null && s.MountedWagePremium && Factor(s) > 1f; } }

        internal static float Factor(Settings s) { return s == null ? 1f : Math.Max(1f, Math.Min(8f, s.MountedWageFactor)); }

        /// <summary>Czy partia dostaje premie konnego: partia rodu (takze gracz) albo garnizon; null (poza zoldem partii i werbunkiem AI) - tak.</summary>
        internal static bool PartyPays(MobileParty p) { return p == null || p.IsLordParty || p.IsGarrison; }

        /// <summary>Zold jednostki z premia konnego (raw - zold z modelu gry).</summary>
        internal static int Apply(int raw, CharacterObject c, MobileParty p)
        {
            if (raw <= 0 || c == null || c.IsHero || !c.IsMounted || !PartyPays(p)) return raw;
            var s = Settings.Current;
            if (s == null || !s.MountedWagePremium) return raw;
            float f = Factor(s);
            if (f <= 1f) return raw;
            return Math.Max(raw, (int)Math.Round(raw * f, MidpointRounding.AwayFromZero));
        }

        // ------------------------------------------------------------ GetCharacterWage (kazdy model zoldu)
        public static void WagePrefix() { _depth++; }
        public static Exception WageFinalizer(Exception __exception) { if (_depth > 0) _depth--; return __exception; }
        public static void WagePostfix(CharacterObject __0, ref int __result)
        {
            if (_depth > 1 || _raw) return;
            try
            {
                // 182 (WatchUnpaid, [D] 07.10): bracia Strazy nie biora zoldu - w partiach i zalogach Strazy zold jednostki 0 (tylko zold partii; cena
                // werbunku i stawka nominalna - jak dotad); liczebnosc trzyma pulap 166 w ludziach (ClanBudget), dlatego tylko z ClanBudgetEnabled
                // WL1 (wyspy-wolni): ta sama regula dla Wolnych Ludzi (FreeFolkUnpaid) - CrownGifts.IsUnpaidParty
                if (_total && _party != null && __0 != null && !__0.IsHero && __result > 0 && CrownGifts.IsUnpaidParty(_party)) { __result = 0; return; }
                __result = Apply(__result, __0, _party);
            }
            catch { }
        }

        /// <summary>182: stawka nominalna jednostki (premia konnego, bez Strazy bez zoldu) - pulap Strazy w ludziach (166).</summary>
        internal static int Nominal(CharacterObject c, MobileParty p)
        {
            try
            {
                var m = Campaign.Current != null ? Campaign.Current.Models.PartyWageModel : null;
                if (m == null || c == null) return 0;
                return Apply(Raw(m, c), c, p);
            }
            catch { return 0; }
        }

        /// <summary>133e (okno armii "Pay", tylko odczyt): stawka jednostki tak, jak widzi ja zold partii mp (GetTotalWage - kontekst _party/_total, WagePostfix):
        /// Straz i Wolni Ludzie 0 (CrownGifts.IsUnpaidParty), inaczej premia konnego wedlug partii (Apply/PartyPays). Ta sama regula co WagePostfix.</summary>
        internal static int InParty(CharacterObject c, MobileParty mp)
        {
            var m = Campaign.Current != null ? Campaign.Current.Models.PartyWageModel : null;
            if (m == null || c == null) return 0;
            int raw = Raw(m, c);
            if (!c.IsHero && raw > 0 && mp != null && CrownGifts.IsUnpaidParty(mp)) return 0;
            return Apply(raw, c, mp);
        }

        /// <summary>133e: czy stawka jednostki w zoldzie partii mp ma premie konnego (warunki Apply bez stawki).</summary>
        internal static bool PremiumFor(CharacterObject c, MobileParty mp) { return On && c != null && !c.IsHero && c.IsMounted && PartyPays(mp); }

        // ------------------------------------------------------------ kontekst partii: GetTotalWage (zold partii) i CheckRecruiting (werbunek AI)
        public static void PartyPrefix(MobileParty __0, out TotalCtx __state) { __state = new TotalCtx { P = _party, T = _total }; _party = __0; _total = false; }   // werbunek AI: cena, nie zold partii
        public static Exception PartyFinalizer(Exception __exception, TotalCtx __state) { _party = __state.P; _total = __state.T; return __exception; }

        // 182: kontekst zoldu partii (GetTotalWage) - partia i znacznik "zold partii" (CheckRecruiting wolany wewnatrz zostaje bez znacznika po finalizerze)
        internal struct TotalCtx { public MobileParty P; public bool T; }
        public static void TotalPrefix(MobileParty __0, out TotalCtx __state) { __state = new TotalCtx { P = _party, T = _total }; _party = __0; _total = true; }
        public static Exception TotalFinalizer(Exception __exception, TotalCtx __state) { _party = __state.P; _total = __state.T; return __exception; }

        // WW-J (wyspy-wolni, decyzja Jeffa 14.10 "Tak, polowa w lupie"): zalogi okretow Zelaznych Wysp (CrownGifts.IsIronbornCrew - druzyny w polu rodow krolestwa
        // Wysp, takze gracza-wasala; nie zalogi twierdz, nie karawany, nie najemnicy) biora w zlocie polowe zoldu partii. Postfiks na GetTotalWage kazdego modelu
        // zoldu (ten sam punkt co kontekst 182/WL1), tylko w zewnetrznym wywolaniu dla tej partii (BKROTPartyWageModel wola base BKPartyWageModel - oba latane;
        // wewnetrzne ma w __state T = true i te sama partie). Add(-(1 - udzial) x baza): wynik modelu = baza x (1 + suma czynnikow), wiec po Add dokladnie
        // udzial x wynik (czynniki BK - kultura, garnizon, perki - zostaja), a podpowiedz zoldu (TotalWageExplained) ma linie "half paid in plunder".
        // Dlaczego nie zold jednostki (jak Straz): stawki sa calkowite (tier 1-4: 1, 2, 3, 5), a BK bierze max(stawka x 1.25, 1) - polowa jednostki dawalaby
        // 0-67% zamiast 50%. Zloto nie powstaje: rod placi mniej, sakiewki ludzi dostaja mniej (SoldierPay), druga polowa to ich dola z lupu (bez przelewu).
        [ThreadStatic] private static bool _fullGold;       // WW-J: linia doby - zold partii bez polowy w lupie ("przed")
        private static TaleWorlds.Localization.TextObject _ironTxt;
        private static int _ironStumbles;
        private static bool _ironErr;
        internal static int IronStumbles { get { return _ironStumbles; } }

        public static void TotalPostfix(MobileParty __0, ref ExplainedNumber __result, TotalCtx __state)
        {
            if (_fullGold || (__state.T && __state.P == __0)) return;   // linia "przed" albo wywolanie wewnetrzne (base) tej samej partii
            try
            {
                if (__0 != null && __0.IsCaravan) { if (CaravanHalfOn) CaravanHalf(__0, ref __result); return; }   // 288: karawany (Wysp w IsIronbornCrew i tak nie ma)
                EfficientCampaignerAlways(__0, ref __result);   // 289: perk kwatermistrza takze poza armia
                if (!CrownGifts.IsIronbornCrew(__0)) return;
                float b = __result.BaseNumber;
                if (!(b > 0f)) return;
                if (_ironTxt == null) _ironTxt = new TaleWorlds.Localization.TextObject("{=!}half paid in plunder (the iron price)");
                __result.Add(-(1f - CrownGifts.IronbornGoldWageShare) * b, _ironTxt);
            }
            catch (Exception e)
            {
                _ironStumbles++;   // licznik potkniec (bez globalnego wylacznika - CLAUDE.md 7); pierwszy do pliku
                if (!_ironErr) { _ironErr = true; try { Log.Error("MountedWage.TotalPostfix (WW-J)", e); } catch { } }
            }
        }

        // 289 (Jeff 28.10: "tak nie tylko w armii efektywny campaigner oznacza ze caly czas a nie ze w armii"; klucz EfficientCampaignerAlways): BK dodaje
        // Steward.EfficientCampaigner (SecondaryBonus -0.25, kwatermistrz - HasPerk(perk, true)) tylko przy Army != null; poza armia ten sam czynnik tu, ten sam
        // perk i ta sama wartosc, z nazwa perka w podpowiedzi zoldu. W armii nic (BK juz dodal) - bez dublowania. Kazda partia z perkiem (gracz i lordowie AI).
        private static int _ecHits;
        internal static int EfficientCampaignerHits { get { return _ecHits; } }
        private static void EfficientCampaignerAlways(MobileParty mp, ref ExplainedNumber r)
        {
            var s = Settings.Current;
            if (s == null || !s.EfficientCampaignerAlways || mp == null || mp.Army != null) return;
            var perk = DefaultPerks.Steward.EfficientCampaigner;
            if (perk == null || !mp.HasPerk(perk, true)) return;
            if (!(r.BaseNumber > 0f)) return;
            r.AddFactor(perk.SecondaryBonus, perk.Name);
            _ecHits++;
        }

        // 288 (Jeff 28.10: "no to bez sensu jest obniz koszta karawany o 50% dla calego swiata, czyli kupiec zbrojny 3 straznik 4 weteran 6"; klucz CaravanWageHalf):
        // kazda karawana swiata (notabli, lordow, gracza) placi ludziom eskorty polowe zoldu. Ten sam punkt i ta sama arytmetyka co WW-J: Add(-(1 - udzial) x podstawa
        // ludzi) - wynik modelu = podstawa x (1 + czynniki), wiec zold ludzi dokladnie x0.5 po czynnikach BK (kultura, karawana -25%, perki), a podpowiedz zoldu
        // ma linie "Merchant escort wage". Tylko ludzie (nie bohaterowie): zold wodza-towarzysza gracza idzie z tabeli CW (CompanionWage, decyzja Jeffa) i
        // CompanionWage.Pay liczy jego udzial jako nalezny x (wynik / podstawa) - ten stosunek Add nie zmienia. Karawana bez bohatera (prawie wszystkie) - cala
        // podstawa (dokladnie); z bohaterem - podstawa ludzi ta sama formula co BK GetVanillaWage ((int)max(stawka x BaseWage, 1) x liczba, z Aid Corps tylko zdrowi),
        // najwyzej cala podstawa. Kazdy czytelnik zoldu karawany (TotalWage: 259, 284, 285/286, 166, E3/4, sakiewki) widzi te sama liczbe; FullTotalWage - przed.
        // Zloto nie powstaje: kiesa karawany (albo notabla) placi mniej, sakiewka ludzi (E3/4) dostaje mniej i mniej wydaje "na zycie" w miastach.
        internal const float CaravanWageShare = 0.5f;
        internal static bool CaravanHalfOn { get { var s = Settings.Current; return s != null && s.CaravanWageHalf; } }
        private static TaleWorlds.Localization.TextObject _carTxt;
        private static int _carStumbles;
        private static bool _carErr;
        internal static int CarStumbles { get { return _carStumbles; } }

        private static void CaravanHalf(MobileParty mp, ref ExplainedNumber r)
        {
            try
            {
                float b = r.BaseNumber;
                if (!(b > 0f)) return;
                float t = b;
                var ros = mp.MemberRoster;
                if (ros != null && ros.TotalHeroes > 0) t = Math.Min(b, TroopBase(mp));
                if (!(t > 0f)) return;
                if (_carTxt == null) _carTxt = new TaleWorlds.Localization.TextObject("{=!}Merchant escort wage (half pay)");
                r.Add(-(1f - CaravanWageShare) * t, _carTxt);
            }
            catch (Exception e)
            {
                _carStumbles++;   // licznik potkniec (bez globalnego wylacznika - CLAUDE.md 7); pierwszy do pliku
                if (!_carErr) { _carErr = true; try { Log.Error("MountedWage.CaravanHalf (288)", e); } catch { } }
            }
        }

        /// <summary>288: podstawa zoldu ludzi (bez bohaterow) partii tak, jak liczy ja BK GetVanillaWage: (int)max(stawka jednostki x BaseWage, 1) x liczba
        /// (z perkiem Aid Corps tylko zdrowi; jednostki bez kultury BK pomija). Stawka jednostki w tej partii - InParty (karawana bez premii konnego).</summary>
        internal static float TroopBase(MobileParty mp)
        {
            var ros = mp != null ? mp.MemberRoster : null;
            if (ros == null) return 0f;
            float bw = CompanionWage.BaseWageNow();
            bool aid = false;
            try { aid = mp.HasPerk(TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks.Steward.AidCorps); } catch { aid = false; }
            long sum = 0;
            for (int i = 0; i < ros.Count; i++)
            {
                var el = ros.GetElementCopyAtIndex(i);
                var c = el.Character;
                if (c == null || c.IsHero || c.Culture == null || el.Number <= 0) continue;
                int n = aid ? el.Number - el.WoundedNumber : el.Number;
                if (n <= 0) continue;
                sum += (long)CompanionWage.BkPerMan(InParty(c, mp), bw) * n;
            }
            return sum;
        }

        /// <summary>WW-J: zold partii bez polowy w lupie (linia doby "przed"; ta sama droga modelu zoldu co TotalWage). 288: takze bez polowy zoldu eskorty karawan.</summary>
        internal static int FullTotalWage(MobileParty mp)
        {
            bool was = _fullGold; _fullGold = true;
            try { return mp != null ? mp.TotalWage : 0; }
            finally { _fullGold = was; }
        }

        /// <summary>Zold jednostki z modelu gry bez premii (linia dnia).</summary>
        private static int Raw(PartyWageModel m, CharacterObject c)
        {
            bool was = _raw; _raw = true;
            try { return m.GetCharacterWage(c); }
            finally { _raw = was; }
        }

        internal static void ApplyAll(Harmony h)
        {
            _harmony = h;
            int n = 0;
            var seen = new HashSet<Type>();
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); } catch { continue; }
                foreach (var t in types)
                {
                    try
                    {
                        if (t == null || t.IsAbstract || !typeof(PartyWageModel).IsAssignableFrom(t)) continue;
                        var m = t.GetMethod("GetCharacterWage", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(CharacterObject) }, null);
                        if (m == null || m.DeclaringType != t || m.IsAbstract || seen.Contains(t)) continue;
                        seen.Add(t);
                        h.Patch(m, prefix: new HarmonyMethod(typeof(MountedWage), nameof(WagePrefix)) { priority = Priority.First },
                                   postfix: new HarmonyMethod(typeof(MountedWage), nameof(WagePostfix)) { priority = Priority.Last },
                                   finalizer: new HarmonyMethod(typeof(MountedWage), nameof(WageFinalizer)));
                        n++;
                    }
                    catch (Exception e) { Log.Error("MountedWage.ApplyAll(" + (t != null ? t.FullName : "?") + ")", e); }
                }
            }
            _wageModels = n;
            try
            {
                var cr = AccessTools.Method(typeof(RecruitmentCampaignBehavior), "CheckRecruiting", new[] { typeof(MobileParty), typeof(Settlement) });
                if (cr != null)
                {
                    h.Patch(cr, prefix: new HarmonyMethod(typeof(MountedWage), nameof(PartyPrefix)), finalizer: new HarmonyMethod(typeof(MountedWage), nameof(PartyFinalizer)));
                    _recruitHook = 1;
                }
            }
            catch (Exception e) { Log.Error("MountedWage.CheckRecruiting", e); }
            var s = Settings.Current;
            Log.Info("MountedWage: premia zoldu konnego (paczka 160) " + (On ? "CZYNNA x" + Factor(s).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) : "WYLACZONA")
                     + " - zold jednostki wpiety w " + n + " modelach zoldu (GetCharacterWage), werbunek AI (CheckRecruiting) " + (_recruitHook == 1 ? "wpiety" : "BRAK")
                     + "; zold partii (GetTotalWage - karawany bez premii) dojdzie przy starcie kampanii.");
        }

        /// <summary>W kampanii (OnSessionLaunched): kontekst partii na GetTotalWage kazdej implementacji modelu zoldu - raz na klase.</summary>
        internal static void EnsureContextHooks()
        {
            var h = _harmony;
            if (h == null) return;
            int now = 0;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); } catch { continue; }
                foreach (var t in types)
                {
                    try
                    {
                        if (t == null || t.IsAbstract || !typeof(PartyWageModel).IsAssignableFrom(t) || _totalHooked.Contains(t)) continue;
                        var m = t.GetMethod("GetTotalWage", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, null,
                                            new[] { typeof(MobileParty), typeof(TroopRoster), typeof(bool) }, null);
                        if (m == null || m.IsAbstract) continue;
                        _totalHooked.Add(t);
                        // WW-J: postfiks Last (po czynnikach innych latek) - polowa zoldu zalog okretow Wysp; __state wspolny z prefiksem (wywolanie wewnetrzne pomijane)
                        h.Patch(m, prefix: new HarmonyMethod(typeof(MountedWage), nameof(TotalPrefix)) { priority = Priority.First },
                                   postfix: new HarmonyMethod(typeof(MountedWage), nameof(TotalPostfix)) { priority = Priority.Last },
                                   finalizer: new HarmonyMethod(typeof(MountedWage), nameof(TotalFinalizer)));
                        now++;
                    }
                    catch (Exception e) { Log.Error("MountedWage.EnsureContextHooks(" + (t != null ? t.FullName : "?") + ")", e); }
                }
            }
            if (now > 0 || _totalHooked.Count == 0)
            {
                string active = "?";
                try { active = Campaign.Current.Models.PartyWageModel.GetType().FullName; } catch { }
                Log.Info("MountedWage: zold partii (GetTotalWage) wpiety w " + _totalHooked.Count + " modelach (teraz " + now + "), model czynny " + active
                         + " - premia konnego w partiach rodow i garnizonach, karawany i inne partie bez premii; zalogi okretow Wysp - polowa zoldu w lupie (WW-J, klucz Ironborn Half Wage In Plunder "
                         + (CrownGifts.IronbornHalfOn ? "tak" : "nie") + ").");
            }
        }

        // ------------------------------------------------------------ linia dnia "Zold konnych (160)" (tylko log)
        private sealed class Row { public int Parties; public long Men, Mounted, All0, All1, Mnt0, Mnt1; }

        internal static void Daily()
        {
            var s = Settings.Current;
            if (s == null || Campaign.Current == null) return;
            var model = Campaign.Current.Models.PartyWageModel;
            if (model == null) return;
            var lords = new Row(); var player = new Row(); var gar = new Row(); var car = new Row();
            var cache = new Dictionary<CharacterObject, int>();
            bool census = HorseCensus.On;   // 175.0: spis koni AI w tej samej petli (bez drugiej petli po partiach)
            bool k3 = CavalryTemplate.LineOn;   // E6/K3 (219): spis jazdy wobec celu szablonow w tej samej petli
            foreach (var mp in MobileParty.All)
            {
                if (mp == null || !mp.IsActive) continue;
                if (census) HorseCensus.CountParty(mp);
                if (k3) CavalryTemplate.CountParty(mp);
                Row r = mp.IsMainParty ? player : mp.IsLordParty ? lords : mp.IsGarrison ? gar : mp.IsCaravan ? car : null;
                if (r == null) continue;
                var roster = mp.MemberRoster;
                if (roster == null) continue;
                r.Parties++;
                for (int i = 0; i < roster.Count; i++)
                {
                    var el = roster.GetElementCopyAtIndex(i);
                    var c = el.Character;
                    if (c == null || c.IsHero || el.Number <= 0) continue;
                    int raw;
                    if (!cache.TryGetValue(c, out raw)) { raw = Raw(model, c); cache[c] = raw; }
                    int paid = Apply(raw, c, mp);
                    long n = el.Number;
                    r.Men += n; r.All0 += n * raw; r.All1 += n * paid;
                    if (c.IsMounted) { r.Mounted += n; r.Mnt0 += n * raw; r.Mnt1 += n * paid; }
                }
            }
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            Func<string, Row, bool, string> F = (name, r, prem) =>
            {
                string t = name + " (" + r.Parties + "): konnych " + r.Mounted + " z " + r.Men + " ludzi (" + (r.Men > 0 ? (100.0 * r.Mounted / r.Men).ToString("0.0", inv) : "0") + "%)";
                t += ", stawki dzienne: konni " + r.Mnt1 + (prem ? " (bez premii " + r.Mnt0 + ")" : "") + " z " + r.All1;
                if (prem) t += " (bez premii " + r.All0 + ", +" + (r.All0 > 0 ? (100.0 * (r.All1 - r.All0) / r.All0).ToString("0.0", inv) : "0") + "%)";
                else t += " (udzial konnych " + (r.All0 > 0 ? (100.0 * r.Mnt0 / r.All0).ToString("0.0", inv) : "0") + "%, bez premii)";
                return t;
            };
            Log.Info("Zold konnych (160, premia " + (On ? "CZYNNA x" + Factor(s).ToString("0.00", inv) : "WYLACZONA") + "): dzien " + (int)CampaignTime.Now.ToDays
                     + " | " + F("partie rodow AI", lords, true) + " | " + F("gracz", player, true) + " | " + F("garnizony", gar, true) + " | " + F("karawany", car, false)
                     + (CaravanHalfOn ? " (zold partii karawan ludzi x0.5 - 288, linia 'Karawany - zold eskorty')" : "")
                     + " | stawki jednostek z modelu zoldu (bez mnoznikow BK - BaseWage, kultura, garnizon -50%, perki - one mnoza zold partii po rowno, wiec procent premii jest ten sam)"
                     + "; latki: zold jednostki " + _wageModels + " modeli, zold partii " + _totalHooked.Count + ", werbunek AI " + _recruitHook + "/1.");
        }
    }
}
