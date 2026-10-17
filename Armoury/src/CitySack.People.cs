using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// L6 - JEDNO LICZYDLO LUDZI (projekt lupu miast rozdz. 1.7 D4, rozdz. 6 L6). Klucz SackKilledInPeopleBook.
    /// Dzis: Banner Kings przy grabiezy zabija 10-16%, przy spustoszeniu 16-24% swojej ludnosci osady (BK BKSettlementBehavior.SiegeConsequences - sluchacz
    /// zdarzenia OnSiegeAftermathApplied), a nasza ksiega ludzi miasta (PeopleBook, stan miasta E4/4 "t:") tego nie widzi: W37 Saltpans BK -13%, ksiega -0.3%;
    /// "zabici" dalej jedza, zarabiaja i placa (kiesy ludu licza glowy od ludzi ksiegi - BookScale / skala rynku PeopleScale, przeliczana co dobe).
    /// Odtad: prefiks i postfiks SiegeConsequences BK mierza ubytek TotalPop tej osady (f = zabici BK / ludnosc BK przed); w miescie ten sam ulamek
    /// stanu miasta schodzi z ksiegi jako zabici przy zdobyciu (PeopleBook.TownSackKilled - przyczyna MSackTown, zgony cywilow regionu jak glod E4/6).
    /// Zamek: ksiega nie ma ludzi zamku (stan "t:" tylko miasta; podzamcze liczy glowy od wsi zamku) - tylko pomiar. Laska: BK nikogo nie zabija - nic.
    /// Linia "Lup miast (L6)" przy kazdym wywolaniu BK; czesc "L6:" linii doby. Wyjatek per wywolanie - licznik potkniec, bez globalnego wylacznika.
    /// </summary>
    internal static partial class CitySack
    {
        internal static bool PeopleOn { get { var s = Settings.Current; return s != null && s.SackKilledInPeopleBook; } }

        // ------------------------------------------------------------ BK: ludnosc osady (refleksja, ten sam wzor co TownFolk.Probe - najpierw IsSettlementPopulated)
        private static PropertyInfo _bkCfg, _bkPopMgr, _bkTotal;
        private static MethodInfo _bkGetPop, _bkPopulated;
        private static bool _bkLooked;

        private static void BkResolve()
        {
            if (_bkLooked) return;
            _bkLooked = true;
            try
            {
                var cfgT = AccessTools.TypeByName("BannerKings.BannerKingsConfig");
                _bkCfg = cfgT != null ? AccessTools.Property(cfgT, "Instance") : null;
                _bkPopMgr = cfgT != null ? AccessTools.Property(cfgT, "PopulationManager") : null;
                var pmT = _bkPopMgr != null ? _bkPopMgr.PropertyType : null;
                _bkGetPop = pmT != null ? AccessTools.Method(pmT, "GetPopData", new[] { typeof(Settlement) }) : null;
                _bkPopulated = pmT != null ? AccessTools.Method(pmT, "IsSettlementPopulated", new[] { typeof(Settlement) }) : null;
                var pdT = _bkGetPop != null ? _bkGetPop.ReturnType : null;
                _bkTotal = pdT != null ? AccessTools.Property(pdT, "TotalPop") : null;
            }
            catch (Exception e) { Stumble("BkResolve", e); }
        }

        /// <summary>Ludnosc BK osady; -1 = brak danych (osada bez danych BK albo BK nieczynny). Nie tworzy danych BK.</summary>
        private static int BkPop(Settlement st)
        {
            BkResolve();
            if (st == null || _bkCfg == null || _bkPopMgr == null || _bkGetPop == null || _bkPopulated == null || _bkTotal == null) return -1;
            var cfg = _bkCfg.GetValue(null, null);
            var pm = cfg != null ? _bkPopMgr.GetValue(cfg, null) : null;
            if (pm == null || !(bool)_bkPopulated.Invoke(pm, new object[] { st })) return -1;
            var pd = _bkGetPop.Invoke(pm, new object[] { st });
            return pd != null ? Convert.ToInt32(_bkTotal.GetValue(pd, null), CultureInfo.InvariantCulture) : -1;
        }

        // ------------------------------------------------------------ prefiks / postfiks BK SiegeConsequences(Settlement, SiegeAftermath, MobileParty)
        private static Settlement _l6St;
        private static int _l6Pop0 = -1;
        private static int _d6Calls, _d6Towns, _d6Castles, _d6NoBook, _d6NoBk;
        private static long _d6BkKilled;
        private static double _d6BookKilled, _d6MaxGap;

        public static void BkSiegePre(Settlement __0)
        {
            _l6St = null; _l6Pop0 = -1;
            try
            {
                if (!Live || __0 == null || __0.Town == null) return;
                _l6Pop0 = BkPop(__0);
                _l6St = __0;
            }
            catch (Exception e) { _l6St = null; Stumble("BkSiegePre", e); }
        }

        public static void BkSiegePost(Settlement __0, SiegeAftermathAction.SiegeAftermath __1)
        {
            var st = _l6St; int pop0 = _l6Pop0;
            _l6St = null; _l6Pop0 = -1;
            try
            {
                if (st == null || !ReferenceEquals(st, __0)) return;
                _d6Calls++;
                int pop1 = BkPop(st);
                if (pop0 <= 0 || pop1 < 0) { _d6NoBk++; return; }
                int killed = Math.Max(0, pop0 - pop1);
                double f = (double)killed / pop0;
                _d6BkKilled += killed;
                string what;
                double book0 = 0, book1 = 0, got = 0;
                if (!st.IsTown) { _d6Castles++; what = "zamek - ksiega nie ma ludzi zamku (podzamcze liczy glowy od wsi), tylko pomiar"; }
                else
                {
                    float p;
                    if (!PeopleBook.TryTown(st, out p)) { _d6NoBook++; what = "miasto bez stanu ksiegi (Town People Frozen wylaczone) - nic do zdjecia"; }
                    else
                    {
                        _d6Towns++;
                        book0 = p;
                        if (PeopleOn && killed > 0) got = PeopleBook.TownSackKilled(st, f * p);
                        float p1; book1 = PeopleBook.TryTown(st, out p1) ? p1 : book0;
                        _d6BookKilled += got;
                        // recenzja (kod, drobne): roznica z FAKTYCZNEJ zmiany stanu ksiegi (przed - po), nie z kwoty, ktora sami podalismy - niezalezna kontrola LUP-17
                        double gap = book0 > 0 ? Math.Abs(100.0 * f - 100.0 * (book0 - book1) / book0) : 0.0;
                        if (PeopleOn && gap > _d6MaxGap) _d6MaxGap = gap;
                        what = PeopleOn ? "zdjete ze stanu miasta (zabici przy zdobyciu)" : "WYLACZONE (Sack Killed In People Book) - ksiega bez zmian";
                    }
                }
                if (MeasureOn)
                {
                    var sb = new StringBuilder(400);
                    sb.Append("Lup miast (L6): dzien ").Append((int)CampaignTime.Now.ToDays).Append(" | osada ").Append(st.StringId).Append(" (").Append(st.IsTown ? "miasto" : "zamek")
                      .Append(") | wybor ").Append(KName[KindOf(__1)])
                      .Append(" | BK ludnosc ").Append(pop0).Append(" -> ").Append(pop1).Append(" (zabici ").Append(killed).Append(" = ").Append((100.0 * f).ToString("0.00", Inv)).Append("%)")
                      .Append(" | ksiega ludzi miasta ").Append(book0.ToString("0", Inv)).Append(" -> ").Append(book1.ToString("0", Inv))
                      .Append(" (zabici ").Append(got.ToString("0.0", Inv)).Append(" = ").Append(book0 > 0 ? (100.0 * (book0 - book1) / book0).ToString("0.00", Inv) : "-").Append("%)")
                      .Append(" | ").Append(what).Append('.');
                    Log.Info(sb.ToString());
                }
            }
            catch (Exception e) { Stumble("BkSiegePost", e); }
        }

        private static void L6Part(StringBuilder sb)
        {
            sb.Append(" | L6: ");
            if (!PeopleOn) sb.Append("WYLACZONE (Sack Killed In People Book) - zabici BK poza ksiega; ");
            sb.Append("BK przy nastepstwach ").Append(_d6Calls).Append(" (miasta ").Append(_d6Towns).Append(", zamki ").Append(_d6Castles).Append(", miasto bez stanu ksiegi ").Append(_d6NoBook)
              .Append(", bez danych BK ").Append(_d6NoBk).Append("), zabici BK ").Append(_d6BkKilled).Append(", zdjete z ksiegi ludzi miast ").Append(_d6BookKilled.ToString("0.0", Inv))
              .Append(", najwieksza roznica ubytku BK i ksiegi ").Append(_d6MaxGap.ToString("0.00", Inv)).Append(" pkt proc.");
        }

        private static void L6Clear() { _d6Calls = _d6Towns = _d6Castles = _d6NoBook = _d6NoBk = 0; _d6BkKilled = 0; _d6BookKilled = 0; _d6MaxGap = 0; }

        private static bool L6Any { get { return _d6Calls > 0; } }

        // ------------------------------------------------------------ ZC7 (DailyLife.War.cs, klucz DailyLifeWarScar): RUINA PRZY ZLUPIENIU
        // Postfiks OnSiegeAftermathApplied - po lupie z monety (L2, prefiks AftermathPre) i po karze gry: zlupione (grabiez, spustoszenie) i ZDOBYTE miasto spycha
        // kupcow do rzemieslnikow i rzemieslnikow do biedoty (DailyLife.SackScar - udzialy stanow, oszczednosci z ludzmi, dlug blizny, okno ruiny 30 dob).
        // Zdobycie: z prefiksu tego samego wywolania (_zcSt/_zcCap - wzor L0), bez niego - strona osady = strona zdobywcy. Laska, zamek, bez zdobycia - nic.
        private static Settlement _zcSt;
        private static bool _zcCap;

        public static void ZcScarPost(MobileParty __0, Settlement __1, SiegeAftermathAction.SiegeAftermath __2, Clan __3, Dictionary<MobileParty, float> __4)
        {
            var st0 = _zcSt; bool cap0 = _zcCap;
            _zcSt = null;
            try
            {
                if (!Live || __1 == null || __1.Town == null) return;
                bool captured = ReferenceEquals(st0, __1) ? cap0 : (__0 != null && __1.MapFaction == __0.MapFaction);
                DailyLife.SackScar(__1, KindOf(__2), captured, __0, __4, __3, (int)CampaignTime.Now.ToDays);
            }
            catch (Exception e) { Stumble("ZcScarPost", e); }
        }

        private static void ZcPatch()
        {
            Seal31.Patch("zycie codzienne ZC7: ruina przy zlupieniu (OnSiegeAftermathApplied, postfiks)", AccessTools.Method(typeof(SiegeAftermathCampaignBehavior), "OnSiegeAftermathApplied"),
                         typeof(CitySack), null, nameof(ZcScarPost), null, Priority.Normal, Priority.Low);
        }

        private static void L6Patch()
        {
            var bk = AccessTools.TypeByName("BannerKings.Behaviours.BKSettlementBehavior");
            Seal31.Patch("lup miast L6: zabici BK przy zdobyciu w ksiedze ludzi (BKSettlementBehavior.SiegeConsequences)", bk != null ? AccessTools.Method(bk, "SiegeConsequences") : null,
                         typeof(CitySack), nameof(BkSiegePre), nameof(BkSiegePost), null, Priority.Normal, Priority.Normal);
        }
    }
}
