using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
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
    /// ZMIANA (klucz CompanionWageByLevel): dotyczy towarzyszy gracza (Hero.IsPlayerCompanion) w rosterze partii, ktorej NIE sa wodzem (druzyna gracza i inne
    /// partie rodu) - to ten sam zbior, dla ktorego BK woluje GetHeroWage. Towarzysz-wodz wlasnej partii - jak dotad (BK).
    ///  - postfiks na CharacterObject.TroopWage (getter): taki towarzysz - zold z tabeli (ekran druzyny pokazuje go przy postaci; BK czesc (1) liczy od niego);
    ///  - postfiks na BKCompanionPrices.GetHeroWage (metoda zadeklarowana w BK): wynik = zold z tabeli (z PaidInPromise wodza) MINUS to, co BK juz policzyl w (1)
    ///    od TroopWage x BaseWage - razem dokladnie zold z tabeli (przy BaseWage 1.25 linia z imieniem w opisie zoldu jest lekko ujemna: czesc (1) jest wieksza);
    ///  - SoldierPay.Route (partia rodu, czesc zaplacona): czesc towarzyszy (tabela x odsetek zaplaconego zoldu partii) do Hero.Gold towarzysza, reszta do sakiewki
    ///    ludzi jak dotad (PayFromWage). Zold oddany do obiegu - MoneyLedger.NoteWageRouted, jak sakiewki.
    /// Cena najmu (CompanionPay.OurPrice) bierze zold z tabeli (WageFor) - 20 dni zoldu jak dotad. Latki raz na proces w kampanii (OnSessionLaunched, po CompanionPay).
    /// Linie: "Towarzysze (CW): start sesji" (latki, tabela, samosprawdzenie) i "Towarzysze (CW): dzien N" (zold druzyny gracza przed/po, wyplaty, kiesy).
    /// Nic w zapisie. Wolane z watkow AI (zold partii) - tylko odczyty i liczniki Interlocked; potkniecia liczone, pierwsze do Log.Error.
    /// </summary>
    internal static class CompanionWage
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly int[] Lv = { 1, 5, 10, 15, 20, 25, 30 };
        private static readonly float[] Wg = { 1f, 3f, 8f, 15f, 25f, 40f, 60f };

        internal static bool On { get { var s = Settings.Current; return s != null && s.CompanionWageByLevel; } }

        private static Harmony _h;
        private static bool _troopHooked, _heroHooked;
        private static MethodInfo _mTroop, _mHero;
        [ThreadStatic] private static bool _bypass;   // linia doby i samosprawdzenie: zold BK bez naszej reguly
        private static int _stumbles, _stDay;
        private static bool _errLogged;
        // liczniki doby (watek gry - SoldierPay.Route)
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

        /// <summary>Czy regula dotyczy bohatera: klucz, towarzysz gracza (nie gracz), zywy, w partii nie jako jej wodz (albo bez partii - ekran, cena).</summary>
        internal static bool Applies(Hero h)
        {
            if (_bypass || h == null || !On || Campaign.Current == null) return false;
            if (!h.IsPlayerCompanion || h == Hero.MainHero || !h.IsAlive) return false;
            var mp = h.PartyBelongedTo;
            return mp == null || mp.LeaderHero != h;
        }

        /// <summary>Mnoznik PaidInPromise wodza partii towarzysza (jak BK GetVanillaWage); bez perku - 1.</summary>
        private static float PromiseOf(Hero h)
        {
            var mp = h.PartyBelongedTo;
            var lead = mp != null ? mp.LeaderHero : null;
            if (lead == null) return 1f;
            var pk = DefaultPerks.Steward.PaidInPromise;
            return pk != null && lead.GetPerkValue(pk) ? 1f + pk.PrimaryBonus : 1f;
        }

        /// <summary>Zold nalezny towarzyszowi za dobe (tabela z PaidInPromise wodza) - tyle wchodzi do zoldu partii i tyle dostaje przy pelnej wyplacie.</summary>
        internal static int DueOf(Hero h)
        {
            int t = DailyOf(h.Level);
            float p = PromiseOf(h);
            return p != 1f ? TaleWorlds.Library.MathF.Round(t * p) : t;
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
                // BK GetVanillaWage policzyl juz za tego bohatera (int)max(TroopWage x BaseWage, 1) (TroopWage = nasza tabela), z PaidInPromise wodza zaokraglone
                int t = DailyOf(__0.Level);
                int c1 = (int)Math.Max(t * CompanionPay.BkBaseWage, 1f);
                float p = PromiseOf(__0);
                if (p != 1f) c1 = TaleWorlds.Library.MathF.Round(c1 * p);
                __result = DueOf(__0) - c1;
            }
            catch (Exception e) { Stumble("HeroWagePostfix", e); }
        }

        // ------------------------------------------------------------ zamkniety obieg: zold towarzyszy do ich kies (SoldierPay.Route)
        /// <summary>Partia rodu, zaplacony zold amt z naliczonego wage: towarzysze w rosterze (Applies) dostaja nalezny zold x odsetek zaplaconego (najwyzej amt)
        /// do Hero.Gold. Zwraca, ile poszlo do towarzyszy (reszta - sakiewka ludzi jak dotad). Wyjatek - 0 (calosc jak dotad).</summary>
        internal static int PayFromWage(MobileParty mp, int amt, int wage)
        {
            if (!On || mp == null || amt <= 0) return 0;
            int given = 0;
            try
            {
                var r = mp.MemberRoster;
                if (r == null) return 0;
                double k = -1.0;   // odsetek zaplaconego zoldu partii - liczony przy pierwszym towarzyszu
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
                        // zeby przy pustej kiesie towarzysze nie brali przed zolnierzami
                        int tw = 0;
                        try { tw = mp.TotalWage; } catch { tw = 0; }
                        int full = Math.Max(wage, tw);
                        k = full > 0 ? Math.Min(1.0, amt / (double)full) : 1.0;
                    }
                    int due = DueOf(h);
                    int pay = Math.Min(amt - given, (int)Math.Round(due * k));
                    _dDue += due;
                    if (pay <= 0) continue;
                    h.ChangeHeroGold(pay);
                    given += pay; _dPaid += pay; _dPayN++; any = true;
                }
                if (any) _dParties++;
                if (given > 0) MoneyLedger.NoteWageRouted(true, given);   // zold oddany do obiegu (jak sakiewki ludzi)
            }
            catch (Exception e) { Stumble("PayFromWage", e); }
            return given;
        }

        // ------------------------------------------------------------ wpiecie (OnSessionLaunched, po CompanionPay.SessionStart)
        internal static void SessionStart()
        {
            const string Head = "Towarzysze (CW): start sesji | ";
            try
            {
                _dPaid = _dDue = 0; _dPayN = _dParties = 0; _stDay = 0;
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
                var sb = new System.Text.StringBuilder();
                sb.Append(Head).Append("klucz CompanionWageByLevel ").Append(On ? "TAK" : "NIE (WYLACZONE)")
                  .Append(" | tabela d/dobe:");
                foreach (int lv in new[] { 1, 5, 10, 15, 19, 20, 25, 30 }) sb.Append(' ').Append(lv).Append('=').Append(DailyOf(lv));
                sb.Append(" | latki: TroopWage ").Append(_troopHooked ? "wpieta" : "BRAK").Append(", GetHeroWage ").Append(_heroHooked ? "wpieta" : "BRAK (bez Banner Kings - zold BK nie istnieje)")
                  .Append(", BaseWage BK ").Append(CompanionPay.BkBaseWage.ToString("0.00", Inv))
                  .Append(" | samosprawdzenie: ").Append(Check()).Append(" | potkniecia ").Append(_stumbles).Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("SessionStart", e); Log.Info(Head + "wyjatek przy wpieciu (ERROR w logu) | potkniecia " + _stumbles + "."); }
        }

        /// <summary>Samosprawdzenie na druzynie gracza: TroopWage i GetHeroWage kazdego towarzysza wobec tabeli; zold partii z regula i bez (tylko log).</summary>
        private static string Check()
        {
            var main = MobileParty.MainParty;
            if (main == null || main.MemberRoster == null) return "brak druzyny gracza";
            var bk = BkModel();
            int n = 0, bad = 0;
            var parts = new List<string>();
            var r = main.MemberRoster;
            for (int i = 0; i < r.Count; i++)
            {
                var ch = r.GetCharacterAtIndex(i);
                if (ch == null || !ch.IsHero || !Applies(ch.HeroObject)) continue;
                var h = ch.HeroObject;
                n++;
                int tw = ch.TroopWage, want = DailyOf(h.Level);
                int hw = 0; bool hwOk = true;
                if (_heroHooked && bk != null)
                {
                    int c1 = (int)Math.Max(want * CompanionPay.BkBaseWage, 1f);
                    float p = PromiseOf(h);
                    if (p != 1f) c1 = TaleWorlds.Library.MathF.Round(c1 * p);
                    hw = (int)_mHero.Invoke(bk, new object[] { h });
                    hwOk = c1 + hw == DueOf(h);
                }
                if (tw != want || !hwOk) bad++;
                if (parts.Count < 4) parts.Add(h.Name + " poz. " + h.Level + ": TroopWage " + tw + " (tabela " + want + "), GetHeroWage " + hw + (hwOk ? "" : " ROZJAZD"));
            }
            if (n == 0) return "w druzynie gracza brak towarzyszy (tabela bez probki)";
            return n + " towarzyszy - " + (bad == 0 ? "ZGODNE" : "ROZJAZD " + bad) + " (" + string.Join("; ", parts.ToArray()) + ")";
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

        /// <summary>Z ArmouryBehavior.OnDailyTick (po CompanionPay.Daily): linia "Towarzysze (CW): dzien N" - zold druzyny gracza przed/po, wyplaty, kiesy.</summary>
        internal static void Daily()
        {
            try
            {
                if (Campaign.Current == null) return;
                var main = MobileParty.MainParty;
                int comp = 0, inMain = 0; long gold = 0, dueMain = 0;
                var lv = new List<string>();
                foreach (var h in Clan.PlayerClan.Companions)
                {
                    if (h == null || !h.IsAlive) continue;
                    comp++; gold += h.Gold;
                    if (main != null && h.PartyBelongedTo == main && Applies(h))
                    {
                        inMain++; dueMain += DueOf(h);
                        if (lv.Count < 6) lv.Add(h.Name + " poz. " + h.Level + " " + DueOf(h) + " d");
                    }
                }
                int before = -1, after = -1;
                if (main != null && main.MemberRoster != null) { after = PartyWage(main, false); before = inMain > 0 ? PartyWage(main, true) : after; }
                int sd = _stDay; _stDay = 0;
                Log.Info("Towarzysze (CW): dzien " + (int)CampaignTime.Now.ToDays + " | klucz CompanionWageByLevel " + (On ? "TAK" : "NIE") + " | towarzysze gracza " + comp
                         + ", w druzynie gracza " + inMain + (lv.Count > 0 ? " (" + string.Join(", ", lv.ToArray()) + ")" : "")
                         + " | zold druzyny gracza: przed (BK) " + before + " -> po " + after + " d/dobe (w tym towarzysze wedlug poziomu " + dueMain + ")"
                         + " | wyplacone towarzyszom od wczoraj " + _dPaid + " d w " + _dPayN + " wyplatach z " + _dParties + " partii (nalezne " + _dDue + ")"
                         + " | kiesy towarzyszy razem " + gold + " | potkniecia " + sd + " (sesja " + _stumbles + ").");
                _dPaid = _dDue = 0; _dPayN = _dParties = 0;
            }
            catch (Exception e) { Stumble("Daily", e); }
        }
    }
}
