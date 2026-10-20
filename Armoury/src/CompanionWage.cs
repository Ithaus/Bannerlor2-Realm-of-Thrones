using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

namespace Armoury
{
    /// <summary>
    /// ZOLD TOWARZYSZY GRACZA WEDLUG POZIOMU (CW; Jeff 19.10: "czemu bohaterowie biora 100 zlota ... Powinni brac per poziom, i dopiero im wyzszy tym duzo
    /// drozej"; decyzja 2e "tak wgraj to": tabela dziennego zoldu poz. 1 ok. 1, 5 = 3, 10 = 8, 15 = 15, 20 = 25, 25 = 40, 30 = 60 d, miedzy punktami liniowo,
    /// powyzej 30 dalej o 4 d na poziom; cena najmu dalej 20 dni zoldu; zloto z kiesy gracza do sakiewki towarzysza - zamkniety obieg).
    ///
    /// DOWOD (dekompilacja BK 1.4.8 BKPartyWageModel, BKCompanionPrices, gra CharacterObject): zold bohatera partii poza wodzem ma dwie czesci:
    ///  (1) GetVanillaWage: num15 = (int)max(CharacterObject.TroopWage x BaseWage BK, 1), u bohatera TroopWage = 2 + 2 x poziom (BaseWage 1.25 u Jeffa),
    ///      z perkiem PaidInPromise wodza Round(num15 x (1 + bonus)) - liczone dla kazdego bohatera rosteru poza glowa rodu (takze wodza partii);
    ///  (2) GetTotalWage: + BKCompanionPrices.GetHeroWage(bohater) = RoundRandomized(suma umiejetnosci > 30 x czynnik x 0.005) dla bohatera poza wodzem
    ///      partii i poza graczem (linia z imieniem bohatera w opisie zoldu).
    ///  Cassel poz. 19: (2 + 38) x 1.25 = 50 + ok. 50 z umiejetnosci = ok. 100 d/dobe ("Main party wages 109" przy jednym towarzyszu). Ten sam model liczy
    ///  zold kazdej partii (gracz, rod gracza, lordowie AI); wedrowcy w karczmach zoldu nie biora - z niego liczymy tylko ich cene (CompanionPay C2).
    ///  ExplainedNumber: wynik = suma Add x (1 + suma AddFactor) - kolejnosc bez znaczenia, czynniki (kultura, perki) dzialaja na towarzysza jak dotad.
    ///
    /// ZMIANA (klucz CompanionWageByLevel): dotyczy BOHATEROW RODU GRACZA (towarzysze - Hero.Clan = CompanionOf - i rodzina) poza graczem i glowa rodu (BK glowy
    /// rodu w zoldzie nie liczy), w kazdej partii rodu - takze jako jej wodz (recenzja 19/20.10: Jeff pytal o "bohaterow" ogolnie, zasada 100%).
    ///  - postfiks na CharacterObject.TroopWage (getter): taki bohater - zold z tabeli (ekran druzyny pokazuje go przy postaci; BK czesc (1) liczy od niego);
    ///  - postfiks na BKCompanionPrices.GetHeroWage (metoda zadeklarowana w BK; BK woluje ja dla bohatera poza wodzem): wynik = zold z tabeli (z PaidInPromise
    ///    wodza) MINUS to, co BK juz policzyl w (1) od TroopWage x BaseWage - razem dokladnie zold z tabeli (przy BaseWage 1.25 linia z imieniem w opisie
    ///    zoldu jest lekko ujemna: czesc (1) jest wieksza);
    ///  - postfiks na BKPartyWageModel.GetTotalWage (zadeklarowana w BK, ogniwo lancucha BKROT -> ROT -> NavalDLC -> BEE -> BK): wodz z rodu gracza (GetHeroWage
    ///    mu BK nie liczy) - korekta (tabela - czesc (1)) jedna linia z jego imieniem; Priority.High - przed postfiksem MountedWage na tej samej metodzie;
    ///  - SoldierPay.Route (partia rodu, czesc zaplacona): czesc tych bohaterow (tabela x czynniki partii x odsetek zaplaconego zoldu partii) do ich Hero.Gold,
    ///    reszta do sakiewki ludzi jak dotad (PayFromWage). Zold oddany do obiegu - MoneyLedger.NoteWageRouted, jak sakiewki. Limity sakiewki ludzi
    ///    (MenGearSaveDays x zold partii, MenPurse) licza zold partii BEZ tych bohaterow (DueInRoster) - ich zold do sakiewki ludzi juz nie idzie.
    /// Cena najmu (CompanionPay.OurPrice) bierze zold z tabeli (WageFor) - 20 dni zoldu jak dotad. Latki raz na proces w kampanii (OnSessionLaunched, po CompanionPay).
    /// Linie: "Towarzysze (CW): start sesji" (latki, tabela, samosprawdzenie) i "Towarzysze (CW): dzien N" (zold druzyny gracza przed/po, wyplaty, kiesy).
    /// SAMOSPRAWDZENIE (recenzja): BaseWage z ZYWEGO BKPartyWageModel (pole baseWage ogniwa lancucha) i miejsce wywolania TroopWage w BK - prywatna
    /// GetVanillaWage tego ogniwa na partii z takim bohaterem z regula i bez: roznica musi sie rownac sumie (czesc (1) z tabeli - czesc (1) BK); wbudowany przez
    /// JIT TroopWage albo zla BaseWage = "ROZJAZD". Bez probki przy starcie - raz w linii doby, gdy bohater sie pojawi.
    /// Nic w zapisie. Wolane z watkow AI (zold partii) - tylko odczyty i liczniki Interlocked; potkniecia liczone, pierwsze do Log.Error.
    /// </summary>
    internal static class CompanionWage
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly int[] Lv = { 1, 5, 10, 15, 20, 25, 30 };
        private static readonly float[] Wg = { 1f, 3f, 8f, 15f, 25f, 40f, 60f };

        internal static bool On { get { var s = Settings.Current; return s != null && s.CompanionWageByLevel; } }

        private static Harmony _h;
        private static bool _troopHooked, _heroHooked, _totalHooked;
        private static MethodInfo _mTroop, _mHero, _mTotal;
        [ThreadStatic] private static bool _bypass;   // linia doby i samosprawdzenie: zold BK bez naszej reguly
        private static int _stumbles, _stDay;
        private static bool _errLogged;
        // BaseWage BK tej sesji: z zywego ogniwa BKPartyWageModel (pole baseWage), inaczej z ustawien BK (CompanionPay); 1 bez BK
        private static float _bw = 1f;
        private static string _bwSrc = "-";
        private static bool _checkedDay;              // samosprawdzenie z probka juz bylo w tej sesji (linia doby)
        // liczniki doby (watek gry - SoldierPay.Route i proba autotestu)
        private static long _dPaid, _dDue; private static int _dPayN, _dParties;

        private static void Stumble(string where, Exception e)
        {
            System.Threading.Interlocked.Increment(ref _stumbles); System.Threading.Interlocked.Increment(ref _stDay);
            if (!_errLogged) { _errLogged = true; Log.Error("CompanionWage." + where, e); }
        }

        // ------------------------------------------------------------ tabela Jeffa
        /// <summary>Dzienny zold towarzysza wedlug poziomu (d): punkty Jeffa, liniowo miedzy nimi, powyzej 30 dalej o 4 d na poziom, ponizej 1 - 1.</summary>
        internal static float Table(int level)
        {
            if (level <= Lv[0]) return Wg[0];
            int n = Lv.Length;
            if (level >= Lv[n - 1]) return Wg[n - 1] + (level - Lv[n - 1]) * (Wg[n - 1] - Wg[n - 2]) / (Lv[n - 1] - Lv[n - 2]);
            for (int i = 1; i < n; i++)
                if (level <= Lv[i])
                    return Wg[i - 1] + (level - Lv[i - 1]) * (Wg[i] - Wg[i - 1]) / (Lv[i] - Lv[i - 1]);
            return Wg[n - 1];
        }

        /// <summary>Zold z tabeli w calych d (najmniej 1).</summary>
        internal static int DailyOf(int level) { return Math.Max(1, (int)Math.Round(Table(level), MidpointRounding.AwayFromZero)); }

        /// <summary>CompanionPay (cena najmu i linia "Towarzysze (C)"): zold dzienny, od ktorego liczymy cene - przy kluczu z tabeli, inaczej oczekiwany zold BK.</summary>
        internal static float WageFor(int level, float bkWage) { return On ? DailyOf(level) : bkWage; }

        /// <summary>Czy regula dotyczy bohatera: klucz, bohater rodu gracza (towarzysz - Clan = CompanionOf - albo rodzina), zywy, nie gracz i nie glowa rodu
        /// (BK glowy rodu w zoldzie nie liczy). Takze wodz partii (recenzja 19/20.10). Szybkie wyjscie dla bohaterow AI (Clan inny niz gracza).</summary>
        internal static bool Applies(Hero h)
        {
            if (_bypass || h == null || !On || Campaign.Current == null) return false;
            var pc = Clan.PlayerClan;
            if (pc == null || h.Clan != pc) return false;
            return h != Hero.MainHero && h != pc.Leader && h.IsAlive;
        }

        /// <summary>Mnoznik PaidInPromise wodza partii bohatera (jak BK GetVanillaWage; wodz - jego wlasny perk); bez perku - 1.</summary>
        private static float PromiseOf(Hero h) { return PromiseOfParty(h.PartyBelongedTo); }

        private static float PromiseOfParty(MobileParty mp)
        {
            var lead = mp != null ? mp.LeaderHero : null;
            if (lead == null) return 1f;
            var pk = DefaultPerks.Steward.PaidInPromise;
            return pk != null && lead.GetPerkValue(pk) ? 1f + pk.PrimaryBonus : 1f;
        }

        /// <summary>Zold nalezny bohaterowi za dobe (tabela z PaidInPromise wodza) - tyle wchodzi do podstawy zoldu partii (przed czynnikami partii).</summary>
        internal static int DueOf(Hero h)
        {
            int t = DailyOf(h.Level);
            float p = PromiseOf(h);
            return p != 1f ? TaleWorlds.Library.MathF.Round(t * p) : t;
        }

        /// <summary>Czesc (1) zoldu bohatera tak, jak liczy ja BK GetVanillaWage: (int)max(TroopWage x BaseWage, 1), z PaidInPromise wodza partii zaokraglone.</summary>
        private static int BkPart1(int troopWage, MobileParty mp)
        {
            int c1 = (int)Math.Max(troopWage * _bw, 1f);
            float p = PromiseOfParty(mp);
            return p != 1f ? TaleWorlds.Library.MathF.Round(c1 * p) : c1;
        }

        /// <summary>MenPurse (limity sakiewki ludzi): suma zoldu z tabeli bohaterow z regula w rosterze partii - ta czesc zoldu do sakiewki ludzi nie idzie.
        /// Tylko odczyty; partie spoza rodu gracza - 0 od razu.</summary>
        internal static int DueInRoster(MobileParty mp)
        {
            try
            {
                if (mp == null || !On) return 0;
                var pc = Clan.PlayerClan;
                if (pc == null || mp.ActualClan != pc) return 0;
                var r = mp.MemberRoster;
                if (r == null) return 0;
                int sum = 0;
                for (int i = 0; i < r.Count; i++)
                {
                    var ch = r.GetCharacterAtIndex(i);
                    if (ch == null || !ch.IsHero) continue;
                    var h = ch.HeroObject;
                    if (Applies(h)) sum += DueOf(h);
                }
                return sum;
            }
            catch (Exception e) { Stumble("DueInRoster", e); return 0; }
        }

        // ------------------------------------------------------------ latki
        public static void TroopWagePostfix(CharacterObject __instance, ref int __result)
        {
            try
            {
                if (__instance == null || !__instance.IsHero) return;
                var h = __instance.HeroObject;
                if (Applies(h)) __result = DailyOf(h.Level);
            }
            catch (Exception e) { Stumble("TroopWagePostfix", e); }
        }

        public static void HeroWagePostfix(Hero __0, ref int __result)
        {
            try
            {
                if (!Applies(__0)) return;
                // BK GetVanillaWage policzyl juz za tego bohatera czesc (1) od TroopWage (= nasza tabela) - tu reszta do tabeli
                __result = DueOf(__0) - BkPart1(DailyOf(__0.Level), __0.PartyBelongedTo);
            }
            catch (Exception e) { Stumble("HeroWagePostfix", e); }
        }

        /// <summary>BK GetTotalWage: wodz partii z regula - BK policzyl mu tylko czesc (1) od TroopWage x BaseWage (GetHeroWage wodzowi nie liczy); korekta do tabeli.</summary>
        public static void TotalPostfix(MobileParty __0, ref ExplainedNumber __result)
        {
            try
            {
                if (__0 == null || !On) return;
                var lead = __0.LeaderHero;
                if (lead == null || !Applies(lead)) return;
                int corr = DueOf(lead) - BkPart1(DailyOf(lead.Level), __0);
                if (corr != 0) __result.Add(corr, lead.Name);
            }
            catch (Exception e) { Stumble("TotalPostfix", e); }
        }

        // ------------------------------------------------------------ zamkniety obieg: zold bohaterow rodu do ich kies (SoldierPay.Route)
        /// <summary>Partia rodu, zaplacony zold amt z naliczonego wage: bohaterowie z regula w rosterze dostaja nalezny zold x czynniki partii x odsetek zaplaconego
        /// (najwyzej amt) do Hero.Gold. Zwraca, ile poszlo do nich (reszta - sakiewka ludzi jak dotad). Wyjatek - to, co juz wyplacone.</summary>
        internal static int PayFromWage(MobileParty mp, int amt, int wage) { return Pay(mp, amt, wage, true); }

        private static int Pay(MobileParty mp, int amt, int wage, bool ledger)
        {
            if (!On || mp == null || amt <= 0) return 0;
            int given = 0;
            try
            {
                var r = mp.MemberRoster;
                if (r == null) return 0;
                double k = -1.0, f = 1.0;   // odsetek zaplaconego zoldu partii i czynniki partii - liczone przy pierwszym bohaterze
                bool any = false;
                for (int i = 0; i < r.Count; i++)
                {
                    var ch = r.GetCharacterAtIndex(i);
                    if (ch == null || !ch.IsHero) continue;
                    var h = ch.HeroObject;
                    if (!Applies(h)) continue;
                    if (k < 0.0)
                    {
                        // naliczone (wage) bywa juz przyciete do budzetu rodu (CalculatePartyWage = min(zold, budzet)) - odsetek od PELNEGO zoldu partii,
                        // zeby przy pustej kiesie bohaterowie nie brali przed zolnierzami
                        int tw = 0;
                        try { tw = mp.TotalWage; } catch { tw = 0; }
                        int full = Math.Max(wage, tw);
                        k = full > 0 ? Math.Min(1.0, amt / (double)full) : 1.0;
                        f = PartyFactor(mp);
                    }
                    int due = DueOf(h);
                    int pay = Math.Min(amt - given, (int)Math.Round(due * f * k));
                    _dDue += due;
                    if (pay <= 0) continue;
                    h.ChangeHeroGold(pay);
                    given += pay; _dPaid += pay; _dPayN++; any = true;
                }
                if (any) _dParties++;
            }
            catch (Exception e) { Stumble("PayFromWage", e); }
            if (ledger && given > 0) MoneyLedger.NoteWageRouted(true, given);   // zold oddany do obiegu (jak sakiewki ludzi) - takze czesc wyplacona przed wyjatkiem
            return given;
        }

        /// <summary>Czynniki zoldu partii (kultura, perki, cechy; ExplainedNumber: wynik = podstawa x (1 + czynniki)) - ResultNumber / BaseNumber modelu, 0-2;
        /// bez podstawy - 1. Udzial bohatera w zoldzie partii to jego podstawa x te czynniki - tyle dostaje przy pelnej wyplacie (jedna zasada z zoldem partii).</summary>
        private static double PartyFactor(MobileParty mp)
        {
            try
            {
                var en = Campaign.Current.Models.PartyWageModel.GetTotalWage(mp, mp.MemberRoster, false);
                if (!(en.BaseNumber > 0.5f)) return 1.0;
                double f = en.ResultNumber / (double)en.BaseNumber;
                return f < 0.0 ? 0.0 : f > 2.0 ? 2.0 : f;
            }
            catch { return 1.0; }
        }

        /// <summary>Proba autotestu (CompanionAutotest): zold rodu gracza nie poszedl od wczoraj (w zapisie autotestu partia gracza nie dostaje wyplaty) -
        /// pelna doba zoldu bohaterow druzyny gracza ta sama droga (Pay), zloto z kiesy gracza (przelew gracz -> bohater, bez ksiegi zoldu). Zwraca kwote.</summary>
        internal static int AutotestPay(MobileParty main)
        {
            if (main == null || Hero.MainHero == null) return 0;
            int due = DueInRoster(main);
            if (due <= 0 || _dPayN > 0) return 0;
            int tw = 0;
            try { tw = main.TotalWage; } catch { tw = 0; }
            int amt = Math.Max(tw, due * 2);
            if (Hero.MainHero.Gold < amt) return 0;
            int given = Pay(main, amt, amt, false);
            if (given > 0) Hero.MainHero.ChangeHeroGold(-given);
            return given;
        }

        /// <summary>Ile bohaterow z regula jest w rosterze partii.</summary>
        internal static int CountIn(MobileParty mp)
        {
            if (mp == null || mp.MemberRoster == null) return 0;
            int n = 0;
            var r = mp.MemberRoster;
            for (int i = 0; i < r.Count; i++)
            {
                var ch = r.GetCharacterAtIndex(i);
                if (ch != null && ch.IsHero && Applies(ch.HeroObject)) n++;
            }
            return n;
        }

        // ------------------------------------------------------------ wpiecie (OnSessionLaunched, po CompanionPay.SessionStart)
        internal static void SessionStart()
        {
            const string Head = "Towarzysze (CW): start sesji | ";
            try
            {
                _dPaid = _dDue = 0; _dPayN = _dParties = 0; _stDay = 0; _checkedDay = false;
                if (_h == null) _h = new Harmony("com.jeff.armoury.companionwage");
                if (!_troopHooked)
                {
                    _mTroop = AccessTools.DeclaredPropertyGetter(typeof(CharacterObject), "TroopWage");
                    if (_mTroop != null) { _h.Patch(_mTroop, postfix: new HarmonyMethod(typeof(CompanionWage), nameof(TroopWagePostfix)) { priority = Priority.Last }); _troopHooked = true; }
                }
                if (!_heroHooked)
                {
                    var bk = AccessTools.TypeByName("BannerKings.Models.Vanilla.BKCompanionPrices");
                    _mHero = bk != null ? AccessTools.DeclaredMethod(bk, "GetHeroWage", new[] { typeof(Hero) }) : null;
                    if (_mHero != null) { _h.Patch(_mHero, postfix: new HarmonyMethod(typeof(CompanionWage), nameof(HeroWagePostfix)) { priority = Priority.Last }); _heroHooked = true; }
                }
                if (!_totalHooked)
                {
                    // wodz partii z regula: metoda zadeklarowana w BK (DeclaredMethod - CLAUDE.md 7); Priority.High - przed MountedWage.TotalPostfix (Last)
                    var bkw = AccessTools.TypeByName("BannerKings.Models.Vanilla.BKPartyWageModel");
                    _mTotal = bkw != null ? AccessTools.DeclaredMethod(bkw, "GetTotalWage", new[] { typeof(MobileParty), typeof(TroopRoster), typeof(bool) }) : null;
                    if (_mTotal != null) { _h.Patch(_mTotal, postfix: new HarmonyMethod(typeof(CompanionWage), nameof(TotalPostfix)) { priority = Priority.High }); _totalHooked = true; }
                }
                ReadBaseWage();
                var sb = new System.Text.StringBuilder();
                sb.Append(Head).Append("klucz CompanionWageByLevel ").Append(On ? "TAK" : "NIE (WYLACZONE)")
                  .Append(" | kogo: bohaterowie rodu gracza (towarzysze i rodzina) poza graczem i glowa rodu, takze wodzowie partii | tabela d/dobe:");
                foreach (int lv in new[] { 1, 5, 10, 15, 19, 20, 25, 30 }) sb.Append(' ').Append(lv).Append('=').Append(DailyOf(lv));
                sb.Append(" | latki: TroopWage ").Append(_troopHooked ? "wpieta" : "BRAK").Append(", GetHeroWage ").Append(_heroHooked ? "wpieta" : "BRAK (bez Banner Kings - zold BK nie istnieje)")
                  .Append(", GetTotalWage BK (wodz) ").Append(_totalHooked ? "wpieta" : "BRAK")
                  .Append(", BaseWage BK ").Append(_bw.ToString("0.00", Inv)).Append(" (").Append(_bwSrc).Append(')')
                  .Append(" | samosprawdzenie: ").Append(Check(false)).Append(" | potkniecia ").Append(_stumbles).Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("SessionStart", e); Log.Info(Head + "wyjatek przy wpieciu (ERROR w logu) | potkniecia " + _stumbles + "."); }
        }

        // ------------------------------------------------------------ samosprawdzenie (recenzja: przez zywe ogniwo BK, nie ta sama formula po obu stronach)
        private static object _bkWage;
        private static MethodInfo _mVanilla;
        private static FieldInfo _fBw;

        /// <summary>Ogniwo BKPartyWageModel w lancuchu czynnego modelu zoldu (pola z modelem zoldu, takze BaseModel / _baseModel) - raz na sesje.</summary>
        private static object FindBkWage()
        {
            var bkT = AccessTools.TypeByName("BannerKings.Models.Vanilla.BKPartyWageModel");
            if (bkT == null || Campaign.Current == null || Campaign.Current.Models == null) return null;
            _mVanilla = AccessTools.DeclaredMethod(bkT, "GetVanillaWage");
            _fBw = AccessTools.DeclaredField(bkT, "baseWage");
            var seen = new List<object>();
            var q = new Queue<object>();
            q.Enqueue(Campaign.Current.Models.PartyWageModel);
            while (q.Count > 0 && seen.Count < 40)
            {
                var x = q.Dequeue();
                if (x == null) continue;
                bool was = false;
                foreach (var y in seen) if (ReferenceEquals(x, y)) { was = true; break; }
                if (was) continue;
                seen.Add(x);
                if (bkT.IsInstanceOfType(x)) return x;
                for (var t = x.GetType(); t != null && t != typeof(object); t = t.BaseType)
                    foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    {
                        if (f.FieldType.IsValueType) continue;
                        if (!typeof(PartyWageModel).IsAssignableFrom(f.FieldType) && !f.FieldType.IsAssignableFrom(typeof(PartyWageModel))) continue;
                        object v = null;
                        try { v = f.GetValue(x); } catch { v = null; }
                        if (v is PartyWageModel) q.Enqueue(v);
                    }
            }
            return null;
        }

        /// <summary>BaseWage: pole baseWage zywego ogniwa BK (BK czyta ustawienia raz na proces - wazne jest to, co ma ogniwo), przy 0 albo bez ogniwa - ustawienia BK
        /// (CompanionPay); bez BK - 1.</summary>
        private static void ReadBaseWage()
        {
            _bw = CompanionPay.BkBaseWage > 0f ? CompanionPay.BkBaseWage : 1f;
            _bwSrc = "ustawienia BK (CompanionPay)";
            try
            {
                _bkWage = FindBkWage();
                if (_bkWage == null) { _bwSrc += ", ogniwa BKPartyWageModel w lancuchu BRAK"; return; }
                float live = _fBw != null ? Convert.ToSingle(_fBw.GetValue(_bkWage)) : 0f;
                if (live > 0f)
                {
                    if (Math.Abs(live - _bw) > 0.0001f) _bwSrc = "ogniwo BK " + live.ToString("0.00", Inv) + " - ROZNA od ustawien " + _bw.ToString("0.00", Inv) + ", liczymy od ogniwa";
                    else _bwSrc = "ogniwo BK = ustawienia";
                    _bw = live;
                }
                else _bwSrc = "ogniwo BK jeszcze nie czytalo (0) - ustawienia BK";
            }
            catch (Exception e) { Stumble("ReadBaseWage", e); _bwSrc += ", odczyt ogniwa - wyjatek"; }
        }

        private static float VanillaBase(MobileParty mp, bool bypass)
        {
            bool old = _bypass;
            _bypass = bypass;
            try { return ((ExplainedNumber)_mVanilla.Invoke(_bkWage, new object[] { mp, false })).BaseNumber; }
            finally { _bypass = old; }
        }

        /// <summary>Probka: partia rodu gracza z bohaterem z regula (najpierw druzyna gracza). Prywatna GetVanillaWage zywego ogniwa BK z regula i bez - roznica wobec
        /// sumy (czesc (1) od tabeli - czesc (1) od TroopWage gry): sprawdza miejsce wywolania TroopWage w BK i BaseWage. Do tego GetHeroWage i korekta wodza.</summary>
        private static string Check(bool fromDay)
        {
            if (!On) return "klucz wylaczony";
            var pc = Clan.PlayerClan;
            if (pc == null) return "brak rodu gracza";
            MobileParty sample = null;
            if (CountIn(MobileParty.MainParty) > 0) sample = MobileParty.MainParty;
            else
                foreach (var w in pc.WarPartyComponents)
                {
                    var mp = w != null ? w.MobileParty : null;
                    if (mp != null && CountIn(mp) > 0) { sample = mp; break; }
                }
            if (sample == null) return "w partiach rodu gracza brak bohaterow z regula (tabela bez probki" + (fromDay ? "" : "; probka w linii doby, gdy sie pojawia") + ")";
            _checkedDay = true;
            var parts = new List<string>();
            int n = 0, bad = 0;
            long want = 0;
            var r = sample.MemberRoster;
            for (int i = 0; i < r.Count; i++)
            {
                var ch = r.GetCharacterAtIndex(i);
                if (ch == null || !ch.IsHero || !Applies(ch.HeroObject)) continue;
                var h = ch.HeroObject;
                n++;
                int tw = ch.TroopWage, tab = DailyOf(h.Level), tw0;
                bool old = _bypass; _bypass = true;
                try { tw0 = ch.TroopWage; } finally { _bypass = old; }
                want += BkPart1(tab, sample) - BkPart1(tw0, sample);
                string hw = "-";
                if (h != sample.LeaderHero && _heroHooked)
                {
                    object bkc = BkModel();
                    if (bkc != null)
                    {
                        int v = (int)_mHero.Invoke(bkc, new object[] { h });
                        hw = v.ToString(Inv);
                        if (BkPart1(tab, sample) + v != DueOf(h)) { bad++; hw += " ROZJAZD"; }
                    }
                }
                if (tw != tab) bad++;
                if (parts.Count < 4) parts.Add(h.Name + " poz. " + h.Level + (h == sample.LeaderHero ? " (wodz)" : "") + ": TroopWage " + tw + " (tabela " + tab + ", gra " + tw0 + "), GetHeroWage " + hw);
            }
            string site;
            if (_bkWage == null || _mVanilla == null) site = "miejsce wywolania BK: BRAK ogniwa / GetVanillaWage - niesprawdzone";
            else
            {
                float a = VanillaBase(sample, false), b = VanillaBase(sample, true);
                bool ok = Math.Abs((a - b) - want) < 0.5f;
                if (!ok) bad++;
                site = "GetVanillaWage BK " + b.ToString("0", Inv) + " -> " + a.ToString("0", Inv) + " (roznica " + (a - b).ToString("0", Inv) + ", oczekiwana " + want + ") " + (ok ? "zgodna" : "ROZJAZD (TroopWage wbudowany w BK albo inna BaseWage)");
            }
            return (sample.IsMainParty ? "druzyna gracza" : "partia " + sample.Name) + ", " + n + " bohaterow - " + (bad == 0 ? "ZGODNE" : "ROZJAZD " + bad)
                   + " | " + site + " | " + string.Join("; ", parts.ToArray());
        }

        private static object BkModel()
        {
            try
            {
                var cfgT = AccessTools.TypeByName("BannerKings.BannerKingsConfig");
                var cfg = cfgT != null ? AccessTools.Property(cfgT, "Instance").GetValue(null, null) : null;
                return cfg != null ? AccessTools.Property(cfgT, "CompanionModel").GetValue(cfg, null) : null;
            }
            catch { return null; }
        }

        /// <summary>Zold partii przez model (bez opisu); bypass - bez naszej reguly (zold BK, jak dotad).</summary>
        private static int PartyWage(MobileParty mp, bool bypass)
        {
            bool old = _bypass;
            _bypass = bypass;
            try { return (int)Campaign.Current.Models.PartyWageModel.GetTotalWage(mp, mp.MemberRoster, false).ResultNumber; }
            finally { _bypass = old; }
        }

        /// <summary>Z ArmouryBehavior.OnDailyTick (po CompanionPay.Daily, po CompanionAutotest.Daily): linia "Towarzysze (CW): dzien N" - zold druzyny gracza
        /// przed/po, bohaterowie z regula w partiach rodu (wodzowie), wyplaty, kiesy; raz na sesje samosprawdzenie z probka, gdy bohater sie pojawi.</summary>
        internal static void Daily()
        {
            try
            {
                if (Campaign.Current == null || Clan.PlayerClan == null) return;
                var main = MobileParty.MainParty;
                int inMain = 0, inOther = 0, leads = 0, comp = 0; long gold = 0, dueMain = 0;
                var lv = new List<string>();
                foreach (var h in Clan.PlayerClan.Companions) if (h != null && h.IsAlive) comp++;
                foreach (var w in Clan.PlayerClan.WarPartyComponents)
                {
                    var mp = w != null ? w.MobileParty : null;
                    if (mp == null || mp.MemberRoster == null) continue;
                    var r = mp.MemberRoster;
                    for (int i = 0; i < r.Count; i++)
                    {
                        var ch = r.GetCharacterAtIndex(i);
                        if (ch == null || !ch.IsHero) continue;
                        var h = ch.HeroObject;
                        if (!Applies(h)) continue;
                        gold += h.Gold;
                        if (mp == main)
                        {
                            inMain++; dueMain += DueOf(h);
                            if (lv.Count < 6) lv.Add(h.Name + " poz. " + h.Level + (h.IsPlayerCompanion ? "" : " (rodzina)") + " " + DueOf(h) + " d");
                        }
                        else { inOther++; if (mp.LeaderHero == h) leads++; }
                    }
                }
                int before = -1, after = -1;
                if (main != null && main.MemberRoster != null) { after = PartyWage(main, false); before = inMain > 0 ? PartyWage(main, true) : after; }
                string chk = !_checkedDay && On && (inMain > 0 || inOther > 0) ? " | samosprawdzenie: " + Check(true) : "";
                int sd = _stDay; _stDay = 0;
                Log.Info("Towarzysze (CW): dzien " + (int)CampaignTime.Now.ToDays + " | klucz CompanionWageByLevel " + (On ? "TAK" : "NIE") + " | towarzysze gracza " + comp
                         + " | bohaterowie z regula: w druzynie gracza " + inMain + (lv.Count > 0 ? " (" + string.Join(", ", lv.ToArray()) + ")" : "")
                         + ", w innych partiach rodu " + inOther + " (w tym wodzow " + leads + ")"
                         + " | zold druzyny gracza: przed (BK) " + before + " -> po " + after + " d/dobe (w tym bohaterowie wedlug poziomu " + dueMain + " przed czynnikami)"
                         + " | wyplacone od wczoraj " + _dPaid + " d w " + _dPayN + " wyplatach z " + _dParties + " partii (nalezne " + _dDue + ")"
                         + " | kiesy bohaterow z regula razem " + gold + chk + " | potkniecia " + sd + " (sesja " + _stumbles + ").");
                _dPaid = _dDue = 0; _dPayN = _dParties = 0;
            }
            catch (Exception e) { Stumble("Daily", e); }
        }
    }
}
