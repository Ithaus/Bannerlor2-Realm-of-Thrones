using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// m6b (6) KASY ZAULKOW - POMIAR (tylko log, klucz AlleyCashProbe). Raport roczny 13.10 wiersz 24: Thenn i Frostfang's Camp (Wolni Ludzie) bez pieniedzy -
    /// kasa ponizej 1 000 zl przez 38 dob, Thenn 331 dob ponizej zapasu kupcow. Rozpoznanie z dostepnych danych (kasy-miast.csv, kiesa-ludu.csv): zawor do pana
    /// i korony, podatki ("pobrane") i budowy = 0 od ok. 30. doby (kasa ponizej zapasu - zawor nie dziala); kasa spada przez "doplyw netto" (handel i przelewy),
    /// a linia swiata "Przeplywy osad (kasy miast)" ma tylko sumy wszystkich miast - nie da sie z niej powiedziec, KTORY kanal oproznia Thenn (tabory wsi, lup
    /// lordow, place ludu, karawany). Ta linia rozbija dobowa zmiane kasy KAZDEGO miasta ponizej 1/4 zapasu kupcow na kanaly:
    ///  - nasz tick dobowy (zawor, korona, lud: zakupy i place E5/B1, markietani, renty - migawka kas na poczatku i koncu ArmouryBehavior.OnDailyTick);
    ///  - tabory wsi (SellGoodsForTradeAction.ApplyInternal - kasa przed i po sprzedazy taboru);
    ///  - przelewy gry (zdarzenie HeroOrPartyTradedGold - kazdy GiveGoldAction) wedlug drugiej strony: lordowie, notable, karawany, inne partie, wsie,
    ///    inne osady, "nic" (z niczego / w nicosc); poza naszym tickiem i poza sprzedaza taborow (tam juz policzone);
    ///  - reszta (ticki gry: konsumpcja i zakupy ludu poza tickiem, warsztaty BK, zycie zolnierzy i band w miescie, inne).
    /// Niczego nie zmienia w grze. Stan tylko w pamieci (bez zapisu), czyszczony w Reset. Linia doby "Kasy zaulkow (m6b)".
    /// </summary>
    internal static class AlleyProbe
    {
        internal static bool On { get { var s = Settings.Current; return s != null && s.AlleyCashProbe && s.LogEnabled; } }
        private const double ShowBelow = 0.25;   // miasto w linii: kasa ponizej 1/4 zapasu kupcow (TownPurse.Reserve)
        private const int MaxShown = 6;
        private const int KLord = 0, KNotable = 1, KCaravan = 2, KParty = 3, KVillage = 4, KSettl = 5, KNone = 6, K = 7;
        private static readonly string[] KName = { "lordowie", "notable", "karawany", "inne partie", "wsie", "inne osady", "z niczego / w nicosc" };

        private sealed class Acc
        {
            public long Gold0 = long.MinValue;   // kasa na koncu poprzedniego taktu (long.MinValue - pierwsza doba sesji)
            public long Tick, Carts;
            public readonly long[] In = new long[K], Out = new long[K];
            public void Clear() { Tick = 0; Carts = 0; Array.Clear(In, 0, K); Array.Clear(Out, 0, K); }
        }

        private static readonly Dictionary<Town, Acc> _acc = new Dictionary<Town, Acc>();
        private static readonly Dictionary<Town, long> _tick0 = new Dictionary<Town, long>();
        private static readonly object _lock = new object();
        private static bool _inTick;
        [ThreadStatic] private static int _cartDepth;
        private static int _stumbles, _stumblesDay;
        private static readonly HashSet<string> _errSites = new HashSet<string>();
        private static string _hooks = "nie wpiete";

        internal static void Reset()
        {
            lock (_lock) { _acc.Clear(); _tick0.Clear(); _inTick = false; }
            _stumbles = 0; _stumblesDay = 0; _errSites.Clear();
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumblesDay++;
            if (_errSites.Add(where)) Log.Error("AlleyProbe." + where, e);
        }

        private static Acc Get(Town t)
        {
            Acc a;
            if (!_acc.TryGetValue(t, out a)) { a = new Acc(); _acc[t] = a; }
            return a;
        }

        // ------------------------------------------------------------ tabory wsi
        public static void CartPre(Settlement __0, out long __state)
        {
            __state = long.MinValue;
            try
            {
                if (!On || __0 == null || __0.Town == null || !__0.IsTown) return;
                __state = __0.Town.Gold;
                _cartDepth++;
            }
            catch (Exception e) { __state = long.MinValue; Stumble("CartPre", e); }
        }

        public static Exception CartFin(Exception __exception, Settlement __0, long __state)
        {
            if (__state == long.MinValue) return __exception;
            try
            {
                if (_cartDepth > 0) _cartDepth--;
                var t = __0 != null ? __0.Town : null;
                if (t != null) lock (_lock) { if (!_inTick) Get(t).Carts += t.Gold - __state; }
            }
            catch (Exception e) { Stumble("CartFin", e); }
            return __exception;
        }

        // ------------------------------------------------------------ przelewy gry
        private static int ClassOf(Hero h, PartyBase p)
        {
            if (h != null) return h.IsNotable ? KNotable : KLord;
            if (p == null) return KNone;
            if (p.IsSettlement) return p.Settlement != null && p.Settlement.IsVillage ? KVillage : KSettl;
            var mp = p.MobileParty;
            if (mp == null) return KParty;
            if (mp.IsCaravan) return KCaravan;
            if (mp.IsLordParty) return KLord;
            if (mp.IsVillager) return KVillage;
            return KParty;
        }

        /// <summary>Nasluch HeroOrPartyTradedGold: przelew z udzialem kasy miasta (osada-miasto po stronie dajacego albo biorcy). Tylko odczyt.</summary>
        internal static void OnGold((Hero, PartyBase) giver, (Hero, PartyBase) recipient, (int, string) amount, bool show)
        {
            try
            {
                int a = amount.Item1;
                if (a == 0 || !On || _cartDepth > 0) return;
                Hero gh = giver.Item1, rh = recipient.Item1;
                PartyBase gp = giver.Item2, rp = recipient.Item2;
                if (a < 0) { var th = gh; gh = rh; rh = th; var tp = gp; gp = rp; rp = tp; a = -a; }   // gra zapisuje "osada -> bohater" ujemna kwota w druga strone (jak MoneyLedger)
                Town gt = gh == null && gp != null && gp.IsSettlement && gp.Settlement != null && gp.Settlement.IsTown ? gp.Settlement.Town : null;
                Town rt = rh == null && rp != null && rp.IsSettlement && rp.Settlement != null && rp.Settlement.IsTown ? rp.Settlement.Town : null;
                if (gt == null && rt == null) return;
                lock (_lock)
                {
                    if (_inTick) return;                                             // w naszym tiku - policzone migawka "nasz tick dobowy"
                    if (gt != null) Get(gt).Out[ClassOf(rh, rp)] += a;
                    if (rt != null) Get(rt).In[ClassOf(gh, gp)] += a;
                }
            }
            catch (Exception e) { Stumble("OnGold", e); }
        }

        // ------------------------------------------------------------ nasz tick dobowy
        /// <summary>Poczatek ArmouryBehavior.OnDailyTickBody: kasy miast przed naszym rozliczeniem doby.</summary>
        internal static void TickOpen()
        {
            if (!On) return;
            try
            {
                lock (_lock)
                {
                    _tick0.Clear();
                    foreach (var t in Town.AllTowns) if (t != null) _tick0[t] = t.Gold;
                    _inTick = true;
                }
            }
            catch (Exception e) { Stumble("TickOpen", e); }
        }

        /// <summary>Koniec ArmouryBehavior.OnDailyTickBody: zmiana kas w naszym ticku, potem linia doby.</summary>
        internal static void TickClose()
        {
            if (!_inTick) return;
            try
            {
                lock (_lock)
                {
                    _inTick = false;
                    foreach (var kv in _tick0) if (kv.Key != null) Get(kv.Key).Tick += kv.Key.Gold - kv.Value;
                    _tick0.Clear();
                }
            }
            catch (Exception e) { _inTick = false; Stumble("TickClose", e); }
            Daily();
        }

        private static void Daily()
        {
            if (!On) return;
            var inv = CultureInfo.InvariantCulture;
            try
            {
                int day = (int)CampaignTime.Now.ToDays - 1;
                var rows = new List<KeyValuePair<double, string>>();
                int below = 0; bool first = false;
                long sD = 0, sT = 0, sC = 0, sR = 0; var sIn = new long[K]; var sOut = new long[K];
                lock (_lock)
                {
                    foreach (var t in Town.AllTowns)
                    {
                        if (t == null || t.Settlement == null) continue;
                        var a = Get(t);
                        long g = t.Gold;
                        if (a.Gold0 == long.MinValue) { a.Gold0 = g; a.Clear(); first = true; continue; }
                        long d = g - a.Gold0;
                        long known = a.Tick + a.Carts;
                        for (int k = 0; k < K; k++) known += a.In[k] - a.Out[k];
                        long rest = d - known;
                        int res = 0;
                        try { res = TownPurse.Reserve(t); } catch { res = 0; }
                        double ratio = res > 0 ? (double)g / res : 1.0;
                        if (ratio < ShowBelow)
                        {
                            below++;
                            sD += d; sT += a.Tick; sC += a.Carts; sR += rest;
                            for (int k = 0; k < K; k++) { sIn[k] += a.In[k]; sOut[k] += a.Out[k]; }
                            var sb = new StringBuilder(300);
                            sb.Append(t.Settlement.Name != null ? t.Settlement.Name.ToString() : t.Settlement.StringId).Append(": kasa ").Append(g).Append(" (zapas ").Append(res)
                              .Append("), zmiana ").Append(d >= 0 ? "+" : "").Append(d).Append(" = nasz tick dobowy ").Append(a.Tick).Append(", tabory wsi ").Append(a.Carts);
                            for (int k = 0; k < K; k++) if (a.In[k] != 0 || a.Out[k] != 0) sb.Append(", ").Append(KName[k]).Append(" +").Append(a.In[k]).Append("/-").Append(a.Out[k]);
                            sb.Append(", reszta (ticki gry) ").Append(rest);
                            rows.Add(new KeyValuePair<double, string>(ratio, sb.ToString()));
                        }
                        a.Gold0 = g; a.Clear();
                    }
                }
                rows.Sort((x, y) => x.Key.CompareTo(y.Key));
                var line = new StringBuilder(1200);
                line.Append("Kasy zaulkow (m6b): dzien ").Append(day).Append(" | ");
                if (first) line.Append("pierwsza doba pomiaru - migawka kas, rozbicie od jutra | ");
                line.Append("miast z kasa ponizej 1/4 zapasu kupcow ").Append(below).Append(" (pokazane ").Append(Math.Min(MaxShown, rows.Count)).Append(")");
                for (int i = 0; i < rows.Count && i < MaxShown; i++) line.Append(" | ").Append(rows[i].Value);
                line.Append(" | razem tych miast: zmiana ").Append(sD).Append(" = nasz tick ").Append(sT).Append(", tabory wsi ").Append(sC);
                for (int k = 0; k < K; k++) if (sIn[k] != 0 || sOut[k] != 0) line.Append(", ").Append(KName[k]).Append(" +").Append(sIn[k]).Append("/-").Append(sOut[k]);
                line.Append(", reszta ").Append(sR).Append(" | wpiecia: ").Append(_hooks).Append(" | potkniecia dzis ").Append(_stumblesDay).Append(" (razem ").Append(_stumbles).Append(").");
                Log.Info(line.ToString());
            }
            catch (Exception e) { Stumble("Daily", e); }
            _stumblesDay = 0;
        }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var m = AccessTools.Method(typeof(SellGoodsForTradeAction), "ApplyInternal");
                var ps = m != null ? m.GetParameters() : null;
                if (m == null || ps == null || ps.Length < 1 || ps[0].ParameterType != typeof(Settlement)) _hooks = "BRAK SellGoodsForTradeAction.ApplyInternal - tabory wsi w reszcie";
                else
                {
                    h.Patch(m, prefix: new HarmonyMethod(typeof(AlleyProbe), nameof(CartPre)), finalizer: new HarmonyMethod(typeof(AlleyProbe), nameof(CartFin)));
                    _hooks = "tabory wsi wpiete, przelewy gry (nasluch), migawki naszego ticku";
                }
            }
            catch (Exception e) { _hooks = "blad wpiecia " + e.GetType().Name; Log.Error("AlleyProbe.ApplyAll", e); }
            Log.Info("AlleyProbe (m6b): " + _hooks + "; klucz AlleyCashProbe (teraz " + (On ? "TAK" : "NIE") + ") - tylko log.");
        }
    }
}
