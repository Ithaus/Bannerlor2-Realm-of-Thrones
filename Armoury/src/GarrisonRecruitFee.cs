using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// E5 DECYZJA 9 JEFFA (12.10): OPLATA ZA WERBUNEK ZALOG NIE ZNIKA - DO SPRZEDAJACEGO (wylacznik GarrisonRecruitFeeToSeller; AUDYT-PELNY-WERBUNEK H6).
    ///
    /// Gra (dec32 GarrisonRecruitmentCampaignBehavior.TickAutoRecruitmentGarrisonChange :76-93): zaloga bierze ochotnika ze slotu notabla miasta albo jego
    /// wsi (VolunteerTypes[i] = null) i dopisuje rodowi pana dlug Clan.AutoRecruitmentExpenses += koszt werbunku (PartyWageModel.GetTroopRecruitmentCost).
    /// Rozliczenie rodu (DefaultClanFinanceModel.AddExpensesForAutoRecruitment :800-806, takze z BK ClanFinancesPatches dla bogatych rodow - przez
    /// refleksje na tej samej metodzie) co dobe zdejmuje 1/5 dlugu z salda rodu (applyWithdrawals) - a gra dopisuje saldo glowie rodu zdarzeniem
    /// "nic -> glowa" (ClanVariablesCampaignBehavior.DailyTickClan :413-414). Ta czesc salda szla w nicosc: notabl, ktory dal czlowieka, nic nie dostawal.
    /// Przy K8 (do 2 rekrutow na dobe) ujscie roslo. Teraz (jak LevyGold / RecruitGoldToSeller dla druzyn):
    ///  1. werbunek (prefiks + postfiks TickAutoRecruitmentGarrisonChange): migawka slotow notabli miasta i jego wsi; kazdy slot wziety przez zaloge to
    ///     pozycja kolejki rodu (notabl, jego osada, kwota). Kwoty = przyrost AutoRecruitmentExpenses (to, co gra naprawde dopisala), dzielony wedlug
    ///     kosztu werbunku slotu; przyrost bez rozpoznanego slotu - kasa miasta zalogi;
    ///  2. splata (prefiks + postfiks AddExpensesForAutoRecruitment): ile gra zdjela z dlugu w tym rozliczeniu rodu (przed - po);
    ///  3. rozliczenie (postfiks DailyTickClan, priorytet Normal - po ciele gry, w oknie pomiaru ksiegi pieniadza (Low) i przed SoldierPay (Last)):
    ///     czesc splaty bez pokrycia w kiesie glowy - ta sama regula co rada 3.5 (CouncilWages35: brak salda najpierw obciaza zold rodu i splate dlugu
    ///     korony; reszta braku obciaza i rade, i te oplate - ostroznie, nic z niczego); pokryta czesc - do sprzedajacych z kolejki (proporcjonalnie, jak gra splaca 1/5 calego dlugu): notabl zywy -
    ///     jego kiesa, zmarly - kasa jego osady (wies - kiesa wsi). Saldo sprzed paczki (albo z czasu, gdy byla wylaczona) nie ma znanego sprzedajacego -
    ///     splacane w tej samej proporcji i znika jak dotad (licznik). Kolejka w zapisie (arm_garfee), przycinana do salda gry.
    /// Platnik -> odbiorca: glowa rodu (saldo rozliczenia) -> notabl / kasa osady. Ksiega pieniadza: przelew w oknie rozliczenia rodu (zmiana zlota swiata
    /// tego rozliczenia mniejsza o oddane - "rozliczenia rodow na minus"), nazwa w linii "Pieniadz swiata (rody - przyczyny)" (MoneyLedger.NoteGarrisonFee).
    /// Gracz = AI (zalogi gracza tez werbuja od notabli). Bez zmiany kwot gry: rod placi tyle samo i tak samo rozlozone w czasie.
    /// </summary>
    internal static class GarrisonRecruitFee
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        internal static bool On { get { var s = Settings.Current; return s != null && s.GarrisonRecruitFeeToSeller; } }

        private sealed class Due { public Hero H; public Settlement S; public long V; }
        private static readonly Dictionary<Clan, List<Due>> _q = new Dictionary<Clan, List<Due>>();
        private static string _pending;   // zapis czeka na rozwiazanie (w SyncData bohaterowie gry nie sa jeszcze do znalezienia - RecruitKit 161)

        // rozliczenie rodu w toku (DailyTickClan)
        private static Clan _cur;
        private static long _curX;

        // liczniki doby (linia "Oplata za werbunek zalog")
        private static int _dayStamp = -1;
        private static long _dAccrued, _dNoSlot, _dTaken, _dToNotables, _dToSettl, _dUncovered, _dLegacy, _dNoTarget, _dOutside, _dTrim;
        private static int _dRecruits, _dSettled, _dBlind;
        private static int _stumbles;
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static bool _wired;

        internal static void Reset()
        {
            _q.Clear(); _pending = null; _cur = null; _curX = 0; _dayStamp = -1; _stumbles = 0; _err.Clear();
            ZeroDay();
        }

        private static void ZeroDay()
        {
            _dAccrued = _dNoSlot = _dTaken = _dToNotables = _dToSettl = _dUncovered = _dLegacy = _dNoTarget = _dOutside = _dTrim = 0;
            _dRecruits = _dSettled = _dBlind = 0;
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_err.Add(where)) Log.Error("GarrisonRecruitFee." + where, e); } catch { }
        }

        // ------------------------------------------------------------ 1. werbunek zalogi
        public sealed class TickState { public Town T; public Clan C; public int Before; public List<KeyValuePair<Hero, CharacterObject[]>> Snap; }

        public static void TickPre(Town __0, out TickState __state)
        {
            __state = null;
            try
            {
                if (!On || __0 == null || __0.Settlement == null || Campaign.Current == null || !Campaign.Current.GameStarted) return;
                var c = __0.Settlement.OwnerClan;
                if (c == null) return;
                var snap = new List<KeyValuePair<Hero, CharacterObject[]>>();
                Action<Settlement> add = st =>
                {
                    if (st == null || st.Notables == null) return;
                    foreach (var h in st.Notables) if (h != null && h.VolunteerTypes != null) snap.Add(new KeyValuePair<Hero, CharacterObject[]>(h, (CharacterObject[])h.VolunteerTypes.Clone()));
                };
                add(__0.Settlement);
                if (__0.Villages != null) foreach (var v in __0.Villages) add(v != null ? v.Settlement : null);
                __state = new TickState { T = __0, C = c, Before = c.AutoRecruitmentExpenses, Snap = snap };
            }
            catch (Exception e) { __state = null; Stumble("TickPre", e); }
        }

        public static void TickPost(TickState __state)
        {
            if (__state == null) return;
            try
            {
                Resolve(); Tick();
                var c = __state.C;
                long delta = (long)c.AutoRecruitmentExpenses - __state.Before;
                if (delta <= 0) return;
                var hs = new List<Hero>(); var ts = new List<CharacterObject>(); var ws = new List<long>();
                long wsum = 0;
                foreach (var kv in __state.Snap)
                {
                    var h = kv.Key; var before = kv.Value; var now = h.VolunteerTypes;
                    if (now == null) continue;
                    for (int i = 0; i < before.Length && i < now.Length; i++)
                    {
                        if (before[i] == null || now[i] != null) continue;
                        long w = 1;
                        try { w = Math.Max(1, Campaign.Current.Models.PartyWageModel.GetTroopRecruitmentCost(before[i], c.Leader).RoundedResultNumber); } catch { w = 1; }
                        hs.Add(h); ts.Add(before[i]); ws.Add(w); wsum += w;
                    }
                }
                var list = QueueOf(c);
                _dAccrued += delta;
                if (hs.Count == 0)
                {
                    // przyrost bez rozpoznanego slotu (inny mod, zmiana slotu w trakcie) - sprzedajacy nieznany: kasa miasta zalogi
                    Add(list, null, __state.T.Settlement, delta);
                    _dNoSlot += delta;
                    return;
                }
                long left = delta;
                for (int i = 0; i < hs.Count; i++)
                {
                    long v = i == hs.Count - 1 ? left : delta * ws[i] / Math.Max(1, wsum);
                    if (v > left) v = left;
                    left -= v;
                    _dRecruits++;
                    if (v <= 0) continue;
                    var h = hs[i];
                    Add(list, h, h.CurrentSettlement ?? h.HomeSettlement ?? __state.T.Settlement, v);
                }
            }
            catch (Exception e) { Stumble("TickPost", e); }
        }

        private static List<Due> QueueOf(Clan c)
        {
            List<Due> l;
            if (!_q.TryGetValue(c, out l)) { l = new List<Due>(); _q[c] = l; }
            return l;
        }

        // jedna pozycja na sprzedajacego (notabl + osada) - kolejka rodu ma najwyzej tylu, ilu notabli jego twierdz i wsi (maly zapis)
        private static void Add(List<Due> list, Hero h, Settlement s, long v)
        {
            if (v <= 0) return;
            for (int i = 0; i < list.Count; i++)
                if (ReferenceEquals(list[i].H, h) && ReferenceEquals(list[i].S, s)) { list[i].V += v; return; }
            list.Add(new Due { H = h, S = s, V = v });
        }

        // ------------------------------------------------------------ 2. splata dlugu w rozliczeniu rodu
        public static void ExpPre(Clan __0, bool __2, out int __state)
        {
            __state = int.MinValue;
            try { if (__2 && __0 != null) __state = __0.AutoRecruitmentExpenses; } catch { }
        }

        public static void ExpPost(Clan __0, bool __2, int __state)
        {
            if (!__2 || __0 == null || __state == int.MinValue) return;
            try
            {
                long x = (long)__state - __0.AutoRecruitmentExpenses;
                if (x <= 0) return;
                if (ReferenceEquals(__0, _cur)) { _curX += x; return; }
                // splata poza rozliczeniem rodu (model wolany przez inny mod z applyWithdrawals) - nie wiemy, czy saldo zeszlo z kiesy: kolejka schodzi, nic nie oddajemy
                Resolve(); Tick();
                _dOutside += x;
                long legacy;
                Consume(__0, x, 0, out legacy);
            }
            catch (Exception e) { Stumble("ExpPost", e); }
        }

        // ------------------------------------------------------------ 3. rozliczenie rodu
        public static void ClanPre(Clan __0) { _cur = __0; _curX = 0; }

        public static void ClanPost(Clan __0)
        {
            var c = _cur; long x = _curX;
            _cur = null; _curX = 0;
            if (c == null || x <= 0 || !ReferenceEquals(c, __0)) return;
            try
            {
                Resolve(); Tick();
                _dTaken += x; _dSettled++;
                long legacy;
                if (!On) { Consume(c, x, 0, out legacy); return; }   // wylaczone - oplata znika jak dotad, kolejka schodzi razem z saldem gry
                // pokrycie: regula rady 3.5 (CouncilWages35.ClanTickPostfix) - brak salda obciaza najpierw zold rodu i splate dlugu korony
                long uncovered = 0, gap, owed;
                if (SoldierPay.PeekShortfall(c, out gap, out owed))
                    uncovered = Math.Min(x, Math.Max(0L, gap - owed - CrownIncome.DebtCutNow(c, gap)));
                else
                {
                    _dBlind++;
                    var head = c.Leader;
                    if (head == null || head.Gold <= 0) uncovered = x;   // saldo nieznane, kiesa glowy pusta - nic nie zaplacono (regula SoldierPay)
                }
                _dUncovered += uncovered;
                Consume(c, x, x - uncovered, out legacy);
            }
            catch (Exception e) { Stumble("ClanPost", e); }
        }

        /// <summary>Zdejmuje x z kolejki rodu c (saldo sprzed paczki - bez znanego sprzedajacego - i pozycje kolejki proporcjonalnie, jak gra zdejmuje
        /// 1/5 calego dlugu), oddajac sprzedajacym laczne paid (paid <= x, w dol - suma oddanych nigdy ponad paid). Kolejka przycinana do salda gry
        /// (Clan.AutoRecruitmentExpenses po splacie).</summary>
        private static void Consume(Clan c, long x, long paid, out long legacy)
        {
            legacy = 0;
            List<Due> list;
            _q.TryGetValue(c, out list);
            long queued = 0;
            if (list != null) foreach (var d in list) queued += d.V;
            long before = (long)c.AutoRecruitmentExpenses + x;            // saldo gry przed ta splata
            long legacyBal = Math.Max(0L, before - queued);                 // czesc salda bez kolejki - sprzed paczki albo z czasu wylaczenia
            legacy = before > 0 ? Math.Min(x, x * legacyBal / before) : 0;  // splacana w tej samej proporcji co reszta (gra: 1/5 calego dlugu)
            if (legacy > 0)
            {
                long lp = paid > 0 ? legacy * paid / x : 0;
                _dLegacy += lp;                                            // splacone, ale bez znanego sprzedajacego - w nicosc jak dotad
            }
            long rest = Math.Min(x - legacy, queued);
            if (rest > 0 && list != null && list.Count > 0)
            {
                long left = rest;
                for (int i = 0; i < list.Count && left > 0; i++)
                {
                    var d = list[i];
                    long k = i == list.Count - 1 ? Math.Min(left, d.V) : Math.Min(Math.Min(left, d.V), rest * d.V / queued);
                    left -= k; d.V -= k;
                    long give = paid > 0 ? k * paid / x : 0;
                    if (give > 0) Give(d, give);
                }
                list.RemoveAll(d => d.V <= 0);
            }
            // rozjazd z saldem gry (kolejka wieksza niz dlug) - najstarsze pozycje przyciete
            if (list != null)
            {
                long q = 0; foreach (var d in list) q += d.V;
                long over = q - Math.Max(0, c.AutoRecruitmentExpenses);
                while (over > 0 && list.Count > 0)
                {
                    var d = list[0];
                    long k = Math.Min(over, d.V);
                    over -= k; d.V -= k; _dTrim += k;
                    if (d.V <= 0) list.RemoveAt(0);
                }
                if (list.Count == 0) _q.Remove(c);
            }
        }

        private static void Give(Due d, long amount)
        {
            int a = (int)Math.Min(int.MaxValue, amount);
            if (d.H != null && d.H.IsAlive)
            {
                d.H.ChangeHeroGold(a);                                     // notabl, ktory dal czlowieka (jak LevyGold: bez zdarzenia - saldo gry zeszlo z glowy)
                _dToNotables += a;
            }
            else if (d.S != null && d.S.SettlementComponent != null)
            {
                d.S.SettlementComponent.ChangeGold(a);                     // notabl zmarly albo nieznany - kasa jego osady (wies - kiesa wsi)
                MoneyLedger.Note169(MoneyLedger.N169Other, d.S, a);        // kasa osady poza GiveGoldAction (tylko licznik)
                _dToSettl += a;
            }
            else { _dNoTarget += a; return; }
            MoneyLedger.NoteGarrisonFee(a);                                // ksiega pieniadza: nazwa przelewu w linii rodow (tylko licznik)
        }

        // ------------------------------------------------------------ linia
        private static void Tick()
        {
            int day = (int)CampaignTime.Now.ToDays;
            if (_dayStamp == day) return;
            if (_dayStamp >= 0 && (_dAccrued + _dTaken + _dOutside + _dTrim > 0 || _stumbles > 0)) Log.Info(Line(_dayStamp));
            ZeroDay();
            _dayStamp = day;
        }

        private static string Line(int day)
        {
            long q = 0; int n = 0;
            foreach (var kv in _q) { n++; foreach (var d in kv.Value) q += d.V; }
            var sb = new StringBuilder(640);
            sb.Append("Oplata za werbunek zalog (E5 decyzja 9): dzien ").Append(day).Append(On ? "" : " (WYLACZONE - Garrison Recruit Fee To Seller)")
              .Append(" | naliczone przy werbunku ").Append(_dAccrued).Append(" za ").Append(_dRecruits).Append(" rekrutow (bez rozpoznanego slotu - kasa miasta zalogi ").Append(_dNoSlot).Append(')')
              .Append(" | splacone przez rody ").Append(_dTaken).Append(" w ").Append(_dSettled).Append(" rozliczeniach: do notabli ").Append(_dToNotables)
              .Append(", do kas osad (notabl zmarly) ").Append(_dToSettl).Append(", bez pokrycia w kiesie glowy ").Append(_dUncovered)
              .Append(" (saldo nieznane ").Append(_dBlind).Append(" rodow), saldo sprzed paczki - bez znanego sprzedajacego, w nicosc jak dotad ").Append(_dLegacy)
              .Append(", bez odbiorcy ").Append(_dNoTarget)
              .Append(" | poza rozliczeniem rodu (nic nie oddane) ").Append(_dOutside)
              .Append(" | kolejka ").Append(q).Append(" u ").Append(n).Append(" rodow, przyciete do salda gry ").Append(_dTrim);
            if (_stumbles > 0) sb.Append(" | potkniecia ").Append(_stumbles);
            return sb.ToString();
        }

        // ------------------------------------------------------------ zapis (arm_garfee): rod,notabl,osada,kwota;...
        internal static string Export()
        {
            try
            {
                if (_pending != null) return _pending;   // wczytane, jeszcze nierozwiazane - oddajemy bez zmian
                var sb = new StringBuilder();
                foreach (var kv in _q)
                {
                    if (kv.Key == null || kv.Key.IsEliminated || kv.Key.StringId == null) continue;
                    foreach (var d in kv.Value)
                    {
                        if (d.V <= 0) continue;
                        if (sb.Length > 0) sb.Append(';');
                        sb.Append(kv.Key.StringId).Append(',').Append(d.H != null && d.H.StringId != null ? d.H.StringId : "-").Append(',')
                          .Append(d.S != null && d.S.StringId != null ? d.S.StringId : "-").Append(',').Append(d.V.ToString(Inv));
                    }
                }
                return sb.ToString();
            }
            catch (Exception e) { Stumble("Export", e); return ""; }
        }

        internal static void Import(string data)
        {
            _q.Clear();
            _pending = string.IsNullOrEmpty(data) ? null : data;
        }

        private static void Resolve()
        {
            if (_pending == null) return;
            string data = _pending;
            _pending = null;
            try
            {
                var clans = new Dictionary<string, Clan>();
                foreach (var c in Clan.All) if (c != null && c.StringId != null) clans[c.StringId] = c;
                var heroes = new Dictionary<string, Hero>();
                try { foreach (var h in Hero.AllAliveHeroes) if (h != null && h.StringId != null) heroes[h.StringId] = h; } catch { }
                try { foreach (var h in Hero.DeadOrDisabledHeroes) if (h != null && h.StringId != null && !heroes.ContainsKey(h.StringId)) heroes[h.StringId] = h; } catch { }
                foreach (var part in data.Split(';'))
                {
                    var p = part.Split(',');
                    if (p.Length < 4) continue;
                    Clan c; long v;
                    if (!clans.TryGetValue(p[0], out c) || !long.TryParse(p[3], NumberStyles.Integer, Inv, out v) || v <= 0) continue;
                    Hero h = null; heroes.TryGetValue(p[1], out h);
                    Settlement s = p[2] != "-" ? Settlement.Find(p[2]) : null;
                    Add(QueueOf(c), h, s, v);
                }
            }
            catch (Exception e) { Stumble("Resolve", e); }
        }

        // ------------------------------------------------------------ latki
        internal static void ApplyAll(Harmony h)
        {
            string a = "BRAK", b = "BRAK", c = "BRAK";
            try
            {
                var m = AccessTools.Method(typeof(GarrisonRecruitmentCampaignBehavior), "TickAutoRecruitmentGarrisonChange", new[] { typeof(Town) });
                if (m != null) { h.Patch(m, prefix: new HarmonyMethod(typeof(GarrisonRecruitFee), nameof(TickPre)), postfix: new HarmonyMethod(typeof(GarrisonRecruitFee), nameof(TickPost))); a = "wpiete"; }
            }
            catch (Exception e) { Log.Error("GarrisonRecruitFee.ApplyAll(werbunek)", e); }
            try
            {
                var m = AccessTools.Method(typeof(DefaultClanFinanceModel), "AddExpensesForAutoRecruitment");
                if (m != null) { h.Patch(m, prefix: new HarmonyMethod(typeof(GarrisonRecruitFee), nameof(ExpPre)), postfix: new HarmonyMethod(typeof(GarrisonRecruitFee), nameof(ExpPost))); b = "wpiete"; }
            }
            catch (Exception e) { Log.Error("GarrisonRecruitFee.ApplyAll(splata)", e); }
            try
            {
                var m = AccessTools.Method(typeof(ClanVariablesCampaignBehavior), "DailyTickClan");
                if (m != null)
                {
                    h.Patch(m, prefix: new HarmonyMethod(typeof(GarrisonRecruitFee), nameof(ClanPre)),
                               postfix: new HarmonyMethod(typeof(GarrisonRecruitFee), nameof(ClanPost)) { priority = Priority.Normal });
                    c = "wpiete";
                }
            }
            catch (Exception e) { Log.Error("GarrisonRecruitFee.ApplyAll(rozliczenie)", e); }
            _wired = a == "wpiete" && b == "wpiete" && c == "wpiete";
            Log.Info("GarrisonRecruitFee (E5 decyzja 9): oplata za werbunek zalog do sprzedajacego - werbunek " + a + ", splata (AddExpensesForAutoRecruitment) " + b
                     + ", rozliczenie rodu (DailyTickClan, Normal) " + c + (_wired ? "" : " - NIEPELNE: oplata znika jak dotad") + "; wylacznik Garrison Recruit Fee To Seller.");
        }
    }
}
