using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;

namespace Armoury
{
    /// <summary>
    /// PACZKA 165 - KORONA Z BIEZACYCH WPLYWOW (projekt etapu 2, krok C1; PLAN 2.3, 2.6). Wylacznik glowny CrownCurrentIncome, czynny tylko
    /// razem z ClanBudgetEnabled (R1: "165 nigdy bez 166" - zwrot z samych wplywow bez hamulca wojska to ok. -170 tys. zl/dobe dla rodow).
    ///
    /// Regula (rozdz. 165 pkt 1-6):
    ///  - WPLYWY DNIA krolestwa = to, co od wczorajszego zamkniecia korony wplynelo do skarbca: powinnosci, danina wojenna, clo, 1/3 zaworu zamkow
    ///    (114), podatek gry/BK od bogatych rodow, splaty dlugu wobec korony, trybut i wszystko inne (zmiana skarbca od wczorajszej migawki -
    ///    gra, BK, Diplomacy) + raty reparacji przyjete (dochodza w trakcie) + 1/CrownReserveReleaseDays (360) zapasu ponad CrownReserveGold
    ///    (500 000). Zapas = skarbiec minus dzisiejsze wplywy; 1/360 zawiera sie we wplywach (nigdzie drugi raz).
    ///  - KOLEJNOSC WYDATKOW: dary (182) -> raty reparacji -> kontrakty najemnikow (185) -> (nagroda za wielkiego jenca - 178, krok D) -> zwrot
    ///    zoldu -> renty 180 (krok C2, CrownRents: reszta do rodow wedlug lenn, udzialy rodow bez warunku zostaja w skarbcu). Czego nie ma - nie jest
    ///    placone (niedoplata przepada, bez dlugu korony).
    ///  - REPARACJE Diplomacy (KingdomWalletCost.ApplyCost z portfelami "Reparations"): zamiast zabrac placacemu skarbiec ponad 2 mln i dlug
    ///    trybutu rodow, a odbiorcy dac z gory 1/3 krolowi i 1/6 najemnikom - dlug korona A -> korona B, rata najwyzej CrownReparationShare (50%)
    ///    wplywow dnia A, B dostaje dokladnie rate do skarbca. Rody nie placa, DebtToKingdom z reparacji nie powstaje.
    ///  - SPLATA DebtToKingdom (stary zapis; gra AddPaymentForDebts): z kiesy rodu do skarbca jego krolestwa (dotad w nicosc) - poza splata zaliczki gry
    ///    (recenzja C1, OBIEG-1: czesc dlugu, ktora gra uznala odbiorcy portfela z niczego - jej splata dalej w nicosc).
    ///  - Zwrot (KingdomTreasury.WageRefund): tylko partie w polu (bez zalog), minus wydatki ich ludzi we wlasnych miastach rodu (K3).
    ///
    /// Platnik -> odbiorca: skarbiec A -> skarbiec B (raty), glowa rodu -> skarbiec (splata dlugu). Zadnego zlota z niczego; kazdy przelew
    /// ma licznik w linii "Korona: wplywy dnia (165)" i w "Obieg" (korona). Zapis: migawka skarbcow i dlugi reparacji (SaveText, "arm_crown165").
    /// </summary>
    internal static class CrownIncome
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        internal static bool On
        {
            get { var s = Settings.Current; return s != null && s.CrownCurrentIncome && s.ClanBudgetEnabled && s.CrownWageRefundEnabled; }
        }

        // ------------------------------------------------------------ stan doby na krolestwo
        internal sealed class KDay
        {
            public long Wallet0, Own, Other, Measured, Stock, Release, Spend, Left;
            public long GiftOut, GiftIn, RepOut, RepIn, Contract, RefundDue, RefundGiven;
            public long Rent, RentHeld;   // 180: renty do glow rodow i udzialy wstrzymane (rody bez warunku, zaokraglenia - zostaja w skarbcu)
            public bool Snap;
        }

        private static readonly Dictionary<Kingdom, KDay> _day = new Dictionary<Kingdom, KDay>();
        private static readonly Dictionary<string, long> _wEnd = new Dictionary<string, long>();   // id krolestwa -> skarbiec przy zamknieciu korony
        private static int _wEndDay = -1;                                                         // doba gry tej migawki
        private static bool _open;                                                                // Begin bylo dzis, End jeszcze nie

        // dlugi reparacji (korona -> korona)
        private sealed class Debt { public string Payer, Receiver; public long Left, Total; public int Day; }
        private static readonly List<Debt> _debts = new List<Debt>();

        // wydatki ludzi partii w polu we wlasnych miastach rodu od ostatniego zwrotu (K3)
        private static readonly Dictionary<Clan, long> _ownSpend = new Dictionary<Clan, long>();

        // recenzja C1 (OBIEG-1): ZALICZKA GRY w dlugu wobec korony. Gra uznaje portfel najemnikow / trybutu / wezwania do wojny w calosci, choc rod nie
        // mial na swoj udzial (brak dopisuje do DebtToKingdom) - odbiorca dostal te kwote z niczego. Czesc tego przyrostu SoldierPay rownowazy obcietym
        // zoldem (zloto zeszlo z kiesy, a nie trafilo do ludzi); reszta to zaliczka z niczego. Przed C1 splata dlugu ginela i domykala ten cykl; od C1 idzie
        // do skarbca - bez tej ewidencji ta sama kwota istnialaby dwa razy (u odbiorcy zaliczki i w skarbcu). Splata najpierw gasi zaliczke (w nicosc, jak przed
        // C1), do skarbca idzie tylko reszta (dlug z obcietego zoldu i dlugi ze starego zapisu - bez wpisu tutaj - tabela 165). Zrodlo (uznawanie portfela
        // w calosci) zamyka 168 (krok D). Zapis w arm_crown165.
        private static readonly Dictionary<string, long> _advance = new Dictionary<string, long>();   // id rodu -> niesplacona zaliczka gry (najwyzej dlug)
        internal static long LastAdvanceRepaid, LastAdvanceNew;
        private static long _dAdvRepaid, _dAdvNew;

        // liczniki doby (linia i "Obieg")
        internal static long LastMeasured, LastOwn, LastOther, LastRelease, LastSpend, LastGiftOut, LastRepOut, LastRepIn, LastContract, LastRefund, LastLeft;
        internal static long LastRent, LastRentHeld;   // 180
        internal static long LastDebtRepaid, LastOwnCut, LastNewDebt; internal static int LastNewDebtN;
        private static long _dDebtRepaid, _dNewDebt, _dDropped; private static int _dNewDebtN, _dDroppedN;
        private static int _stumbles;
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static bool _repWired, _debtWired;
        private static int _importN = -1, _importBad;

        internal static void Reset()
        {
            _day.Clear(); _wEnd.Clear(); _wEndDay = -1; _open = false; _debts.Clear(); _ownSpend.Clear(); _advance.Clear();
            ZeroLast(); _dDebtRepaid = _dNewDebt = _dDropped = 0; _dNewDebtN = _dDroppedN = 0; _dAdvRepaid = _dAdvNew = 0;
            _stumbles = 0; _err.Clear(); _importN = -1; _importBad = 0;
        }

        internal static void ZeroLast()
        {
            LastMeasured = LastOwn = LastOther = LastRelease = LastSpend = LastGiftOut = LastRepOut = LastRepIn = LastContract = LastRefund = LastLeft = 0;
            LastRent = LastRentHeld = 0;
            LastDebtRepaid = LastOwnCut = LastNewDebt = 0; LastNewDebtN = 0; LastAdvanceRepaid = LastAdvanceNew = 0;
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_err.Add(where)) Log.Error("CrownIncome." + where, e); } catch { }
        }

        /// <summary>Stan doby krolestwa (null, gdy 165 dzis nie liczylo - wylaczone albo krolestwo bez wpisu).</summary>
        internal static KDay DayOf(Kingdom k)
        {
            KDay d;
            return k != null && _open && _day.TryGetValue(k, out d) ? d : null;
        }

        /// <summary>Ile z wplywow dnia krolestwo moze jeszcze wydac (0 poza 165).</summary>
        internal static long LeftFor(Kingdom k) { var d = DayOf(k); return d != null ? Math.Max(0, Math.Min(d.Left, (long)k.KingdomBudgetWallet)) : 0; }

        /// <summary>Wydatek z wplywow dnia (dar, kontrakt, zwrot) - skarbiec placi wolajacy; tu tylko licznik reszty.</summary>
        internal static void Spent(Kingdom k, long amount)
        {
            var d = DayOf(k);
            if (d == null || amount <= 0) return;
            d.Left = Math.Max(0, d.Left - amount);
        }

        /// <summary>Wplyw do wydania tego samego dnia (np. dar przechodzacy przez skarbiec odbiorcy) - dopisany do reszty krolestwa.</summary>
        internal static void Received(Kingdom k, long amount)
        {
            var d = DayOf(k);
            if (d == null || amount <= 0) return;
            d.Left += amount;
        }

        // ------------------------------------------------------------ K3: wydatki ludzi partii w polu we wlasnych miastach rodu
        /// <summary>MenPurse.OnLeft: ludzie partii rodu wydali "na zycie" w miescie tego samego rodu - zwrot korony od tego zoldu przepada (K3).</summary>
        internal static void NoteOwnSpend(Clan c, int amount)
        {
            try
            {
                if (c == null || amount <= 0 || !On) return;
                long v; _ownSpend.TryGetValue(c, out v); _ownSpend[c] = v + amount;
            }
            catch (Exception e) { Stumble("NoteOwnSpend", e); }
        }

        /// <summary>Wydatki we wlasnych miastach od ostatniego zwrotu, na rod - oproznia licznik (wola WageRefund).</summary>
        internal static Dictionary<Clan, long> TakeOwnSpend()
        {
            var d = new Dictionary<Clan, long>(_ownSpend);
            _ownSpend.Clear();
            return d;
        }

        // ------------------------------------------------------------ poczatek: wplywy dnia i 1/360 zapasu (po powinnosciach, daninie i cle)
        internal static void Begin()
        {
            _day.Clear(); _open = false;
            var s = Settings.Current;
            if (s == null || Campaign.Current == null) return;
            if (!On) { _ownSpend.Clear(); return; }
            try
            {
                int today = (int)CampaignTime.Now.ToDays;
                bool snapOk = _wEndDay >= 0 && today - _wEndDay == 1;   // migawka z wczorajszego zamkniecia korony
                long reserve = Math.Max(0, s.CrownReserveGold);
                double days = Math.Max(1.0, s.CrownReserveReleaseDays);
                foreach (var k in Kingdom.All)
                {
                    try
                    {
                        if (k == null || k.IsEliminated || k.StringId == null) continue;
                        var d = new KDay();
                        long w = k.KingdomBudgetWallet;
                        d.Wallet0 = w;
                        long dues; KingdomTreasury.DuesToday.TryGetValue(k, out dues);
                        long lev; KingdomTreasury.LeviesToday.TryGetValue(k, out lev);
                        long cas; CastlePurse.CrownToday.TryGetValue(k, out cas);
                        d.Own = dues + lev + cas;
                        long w0;
                        if (snapOk && _wEnd.TryGetValue(k.StringId, out w0)) { d.Snap = true; d.Other = w - w0 - d.Own; }
                        else d.Other = 0;                               // brak wczorajszej migawki (wczytanie starego zapisu, nowe krolestwo) - tylko nasze liczniki
                        d.Measured = Math.Max(0, d.Own + d.Other);     // netto: wydatki skarbca spoza nas (Diplomacy, gra) zmniejszaja wplywy dnia
                        d.Stock = Math.Max(0, w - d.Measured);
                        d.Release = (long)(Math.Max(0, d.Stock - reserve) / days);
                        d.Spend = Math.Max(0, Math.Min(Math.Max(0, w), d.Measured + d.Release));
                        d.Left = d.Spend;
                        _day[k] = d;
                    }
                    catch (Exception e) { Stumble("Begin(krolestwo)", e); }
                }
                _open = true;
            }
            catch (Exception e) { Stumble("Begin", e); }
        }

        // ------------------------------------------------------------ raty reparacji (po darach 182, przed kontraktami 185)
        internal static void Reparations()
        {
            if (!_open || !On) return;
            var s = Settings.Current;
            try
            {
                float share = Math.Max(0f, Math.Min(1f, s.CrownReparationShare));
                var paidBy = new Dictionary<Kingdom, long>();
                for (int i = 0; i < _debts.Count; i++)
                {
                    var debt = _debts[i];
                    try
                    {
                        var a = FindKingdom(debt.Payer); var b = FindKingdom(debt.Receiver);
                        if (a == null || b == null || a.IsEliminated || b.IsEliminated)
                        {
                            _dDropped += debt.Left; _dDroppedN++; debt.Left = 0;   // krolestwa nie ma - dlug przepada (nikt nie dostaje z niczego)
                            continue;
                        }
                        var da = DayOf(a);
                        if (da == null || debt.Left <= 0) continue;
                        long p0; paidBy.TryGetValue(a, out p0);
                        long cap = (long)(share * da.Spend) - p0;        // najwyzej 50% wplywow dnia placacego - na wszystkie jego raty razem
                        long pay = Math.Min(Math.Min(debt.Left, cap), Math.Min(da.Left, (long)Math.Max(0, a.KingdomBudgetWallet)));
                        if (pay <= 0) continue;
                        int ip = (int)Math.Min(int.MaxValue, pay);
                        a.KingdomBudgetWallet -= ip;                     // najpierw placacy, potem odbiorca: odbiorca dostaje dokladnie rate
                        b.KingdomBudgetWallet += ip;
                        debt.Left -= ip; paidBy[a] = p0 + ip;
                        da.Left -= ip; da.RepOut += ip;
                        var db = DayOf(b);
                        if (db != null) { db.RepIn += ip; db.Left += ip; }   // rata przyjeta - wplyw dnia odbiorcy (do zwrotu tego samego dnia)
                    }
                    catch (Exception e) { Stumble("Reparations(dlug)", e); }
                }
                _debts.RemoveAll(x => x.Left <= 0);
            }
            catch (Exception e) { Stumble("Reparations", e); }
        }

        private static Kingdom FindKingdom(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var k in Kingdom.All) if (k != null && k.StringId == id) return k;
            return null;
        }

        // ------------------------------------------------------------ koniec: migawka skarbcow i linia "Korona: wplywy dnia (165)"
        internal static void End()
        {
            try
            {
                if (Campaign.Current == null) return;
                int today = (int)CampaignTime.Now.ToDays;
                // migawka zawsze (takze przy wylaczonym 165) - wlaczenie w trakcie kampanii ma od nastepnej doby pelne wplywy
                _wEnd.Clear();
                foreach (var k in Kingdom.All) if (k != null && !k.IsEliminated && k.StringId != null) _wEnd[k.StringId] = k.KingdomBudgetWallet;
                _wEndDay = today;
                LastAdvanceRepaid = _dAdvRepaid; LastAdvanceNew = _dAdvNew; _dAdvRepaid = 0; _dAdvNew = 0;   // recenzja C1 (OBIEG-1): zaliczka gry (licznik doby)
                if (!_open) { LastDebtRepaid = _dDebtRepaid; _dDebtRepaid = 0; LastNewDebt = _dNewDebt; LastNewDebtN = _dNewDebtN; _dNewDebt = 0; _dNewDebtN = 0; return; }
                long meas = 0, own = 0, oth = 0, rel = 0, spend = 0, gift = 0, giftIn = 0, repO = 0, repI = 0, con = 0, refD = 0, refG = 0, left = 0, rent = 0, rentH = 0; int noSnap = 0;
                var parts = new List<KeyValuePair<long, string>>();
                foreach (var kv in _day)
                {
                    var k = kv.Key; var d = kv.Value;
                    meas += d.Measured; own += d.Own; oth += d.Other; rel += d.Release; spend += d.Spend; gift += d.GiftOut; giftIn += d.GiftIn; repO += d.RepOut; repI += d.RepIn;
                    con += d.Contract; refD += d.RefundDue; refG += d.RefundGiven; left += Math.Max(0, d.Left); rent += d.Rent; rentH += d.RentHeld;
                    if (!d.Snap) noSnap++;
                    if (d.Spend <= 0 && d.RepIn <= 0 && d.GiftIn <= 0) continue;
                    parts.Add(new KeyValuePair<long, string>(d.Spend, (k.Name != null ? k.Name.ToString() : k.StringId) + " " + d.Measured + "+" + d.Release
                              + (d.GiftOut > 0 ? " dar -" + d.GiftOut : "") + (d.GiftIn > 0 ? " dar +" + d.GiftIn : "") + (d.RepOut > 0 ? " rata -" + d.RepOut : "") + (d.RepIn > 0 ? " rata +" + d.RepIn : "")
                              + (d.Contract > 0 ? " kontr. " + d.Contract : "") + (d.RefundDue > 0 ? " zwrot " + d.RefundGiven + "/" + d.RefundDue : "")
                              + (d.Rent > 0 || d.RentHeld > 0 ? " renty " + d.Rent + " wstrz. " + d.RentHeld : "")));
                }
                parts.Sort((x, y) => y.Key.CompareTo(x.Key));
                var txt = new List<string>(); foreach (var p in parts) txt.Add(p.Value);
                long debtSum = 0; foreach (var dbt in _debts) debtSum += dbt.Left;
                LastMeasured = meas; LastOwn = own; LastOther = oth; LastRelease = rel; LastSpend = spend; LastGiftOut = gift; LastRepOut = repO; LastRepIn = repI; LastContract = con; LastRefund = refG; LastLeft = left;
                LastRent = rent; LastRentHeld = rentH;
                LastDebtRepaid = _dDebtRepaid; _dDebtRepaid = 0; LastNewDebt = _dNewDebt; LastNewDebtN = _dNewDebtN; _dNewDebt = 0; _dNewDebtN = 0;
                var s = Settings.Current;
                if (s != null && s.LogEnabled)
                {
                    var sb = new StringBuilder(2048);
                    sb.Append("Korona: wplywy dnia (165): dzien ").Append(today)
                      .Append(" | wplywy dnia ").Append(meas).Append(" (nasze: powinnosci, danina wojenna, clo, 1/3 zaworu zamkow ").Append(own)
                      .Append("; inne do skarbcow od wczoraj - gra, BK, Diplomacy, splata dlugu wobec korony ").Append(oth).Append(")")
                      .Append(" + 1/").Append(Math.Max(1f, s.CrownReserveReleaseDays).ToString("0", Inv)).Append(" zapasu ponad ").Append(Math.Max(0, s.CrownReserveGold)).Append(' ').Append(rel)
                      .Append(" = do wydania ").Append(spend)
                      .Append(" | wydane z wplywow: dary (182) ").Append(gift).Append(" (przeszly do rodow odbiorcow ").Append(giftIn).Append(")")
                      .Append(", raty reparacji ").Append(repO).Append(" (przyjete ").Append(repI).Append(")")
                      .Append(", kontrakty najemnikow (185) ").Append(con)
                      .Append(", zwrot zoldu ").Append(refG).Append(" (nalezny ").Append(refD).Append(refD > 0 ? ", wyplacone " + (100.0 * refG / refD).ToString("0.0", Inv) + "%" : "").Append(")")
                      .Append(", renty (180) ").Append(rent).Append(" (wstrzymane w skarbcach - rody bez warunku i zaokraglenia ").Append(rentH).Append(CrownRents.On ? ")" : "; renty wylaczone)")
                      .Append(" | zostalo z wplywow dnia w skarbcach ").Append(left)
                      .Append(" | dlugi reparacji: ").Append(_debts.Count).Append(" na ").Append(debtSum).Append(" zl, nowe dzis ").Append(LastNewDebtN).Append(" na ").Append(LastNewDebt)
                      .Append(", przepadly (krolestwa nie ma) ").Append(_dDroppedN).Append(" na ").Append(_dDropped)
                      .Append(" | splata dlugu wobec korony do skarbcow (stary zapis) ").Append(LastDebtRepaid).Append(" (niezaplacone mimo wpisu w saldzie - nie do skarbca ").Append(_dDebtUnpaid).Append(')')
                      .Append(", splata zaliczki gry (portfel uznany z niczego) - w nicosc ").Append(LastAdvanceRepaid).Append(" (nowe zaliczki gry od wczoraj ").Append(LastAdvanceNew)
                      .Append(", niesplacone razem ").Append(AdvanceTotal()).Append(" u ").Append(_advance.Count).Append(" rodow)")
                      .Append(" | krolestwa bez wczorajszej migawki (tylko nasze liczniki) ").Append(noSnap)
                      .Append(" | reparacje Diplomacy przechwycone: ").Append(_repWired ? "TAK" : "BRAK").Append(", splata dlugu wobec korony: ").Append(_debtWired ? "TAK" : "BRAK")
                      .Append(" | na krolestwo (wplywy+zapas, wydatki): ").Append(txt.Count > 0 ? string.Join(", ", txt.ToArray()) : "-")
                      .Append(_stumbles > 0 ? " | potkniecia " + _stumbles : "").Append('.');
                    if (_importN >= 0) { sb.Append(" Wczytano z zapisu: dlugow reparacji ").Append(_importN).Append(" (bledne ").Append(_importBad).Append(")."); _importN = -1; }
                    Log.Info(sb.ToString());
                }
                _dDropped = 0; _dDroppedN = 0; _dDebtUnpaid = 0;
            }
            catch (Exception e) { Stumble("End", e); }
            finally { _open = false; }
        }

        // ------------------------------------------------------------ Diplomacy: odszkodowania jako dlug korona -> korona
        private static System.Reflection.PropertyInfo _pPayer, _pRecv, _pPayerW, _pRecvW, _pValue;

        /// <summary>Prefiks KingdomWalletCost.ApplyCost (Diplomacy, wirtualna - wolana przez HybridCost przy pokoju): portfele "Reparations" -> dlug.</summary>
        public static bool ReparationPrefix(object __instance)
        {
            try
            {
                var s = Settings.Current;
                if (__instance == null || s == null || !On || !s.CrownReparationsRealm) return true;
                var t = __instance.GetType();
                if (_pPayer == null || _pPayer.DeclaringType != t)
                {
                    _pPayer = AccessTools.Property(t, "PayingKingdom"); _pRecv = AccessTools.Property(t, "ReceivingKingdom");
                    _pPayerW = AccessTools.Property(t, "PayerWallet"); _pRecvW = AccessTools.Property(t, "ReceiverWallet"); _pValue = AccessTools.Property(t, "Value");
                }
                if (_pPayer == null || _pRecv == null || _pPayerW == null || _pRecvW == null || _pValue == null) return true;
                var a = _pPayer.GetValue(__instance, null) as Kingdom;
                var b = _pRecv.GetValue(__instance, null) as Kingdom;
                if (a == null || b == null || a == b || a.StringId == null || b.StringId == null) return true;
                // GiveGoldToKingdomAction.WalletType: None 0, Mercenary 1, Tribute 2, Budget 3, Reparations 4
                if (Convert.ToInt32(_pPayerW.GetValue(__instance, null), Inv) != 4 || Convert.ToInt32(_pRecvW.GetValue(__instance, null), Inv) != 4) return true;
                long amount = (long)Convert.ToSingle(_pValue.GetValue(__instance, null), Inv);
                if (amount <= 0) return true;
                _debts.Add(new Debt { Payer = a.StringId, Receiver = b.StringId, Left = amount, Total = amount, Day = (int)CampaignTime.Now.ToDays });
                _dNewDebt += amount; _dNewDebtN++;
                Log.Info("Korona: reparacje (165) - " + a.Name + " jest winne " + b.Name + " " + amount + " zl; splata ratami z wplywow dnia placacego (najwyzej "
                         + (Math.Max(0f, Math.Min(1f, s.CrownReparationShare)) * 100f).ToString("0", Inv) + "% dziennie), rody nie placa.");
                if (Clan.PlayerClan != null && (Clan.PlayerClan.Kingdom == a || Clan.PlayerClan.Kingdom == b))
                    Log.Player(a.Name + " owes " + b.Name + " " + amount + " denars in war reparations - the crown pays them from its daily income, in instalments.");
                return false;   // Diplomacy nie rusza skarbcow, trybutu ani kies (zloto z niczego dla krola i najemnikow odbiorcy - tez nie)
            }
            catch (Exception e) { Stumble("ReparationPrefix", e); return true; }
        }

        // ------------------------------------------------------------ splata DebtToKingdom do skarbca (gra: AddPaymentForDebts - dotad w nicosc)
        // przeglad C1 (uwaga 3): splata w AddPaymentForDebts to wpis w saldzie rodu, a saldo dopisuje gra na koncu rozliczenia (DailyTickClan) - kiesa
        // glowy nie schodzi ponizej 0, a BK dopisuje wydatki PO tym kroku. Skarbiec dostaje wiec dopiero na koncu rozliczenia rodu (SoldierPay.ClanTickPostfix)
        // i tylko czesc, ktora glowa naprawde zaplacila: splata minus brak salda (saldo, ktore nie zmiescilo sie w kiesie). Ujemna "splata" gry (gra dopisuje
        // rodowi brak jako dlug) - nic.
        private static Clan _debtClan; private static int _debtPaid;
        private static long _dDebtUnpaid;

        public static void DebtPrefix(Clan __0, bool __2, out int __state)
        {
            __state = -1;
            try { if (__2 && __0 != null && On) { __state = __0.DebtToKingdom; if (!ReferenceEquals(_debtClan, __0)) { _debtClan = null; _debtPaid = 0; } } } catch { __state = -1; }
        }

        public static void DebtPostfix(Clan __0, int __state)
        {
            try
            {
                if (__state < 0 || __0 == null || __0.Kingdom == null) return;
                int paid = __state - __0.DebtToKingdom;
                if (paid <= 0) return;
                _debtClan = __0; _debtPaid += paid;                // do zaplaty skarbcowi na koncu rozliczenia rodu (ClanTickEnd)
            }
            catch (Exception e) { Stumble("DebtPostfix", e); }
        }

        /// <summary>SoldierPay.ClanTickPostfix (koniec rozliczenia rodu): splata dlugu wobec korony do skarbca - najwyzej to, co glowa naprawde zaplacila,
        /// minus zaliczka gry (OBIEG-1 - ta czesc w nicosc). haveNet - znane saldo i kiesa glowy przed jego dopisaniem; bez nich - nic (nie wiemy, czy zaplacono).
        /// Zwraca, o ile gra zmniejszyla dzis dlug rodu w AddPaymentForDebts (0 - bez splaty) - SoldierPay liczy z tego brutto nowego dlugu.</summary>
        internal static int ClanTickEnd(Clan c, bool haveNet, int goldMid, int net)
        {
            int paid = 0;
            try
            {
                if (_debtClan != null && ReferenceEquals(_debtClan, c)) paid = _debtPaid;
                _debtClan = null; _debtPaid = 0;
                if (paid > 0 && c.Kingdom != null && On)
                {
                    long gap = haveNet ? Math.Max(0L, -((long)goldMid + net)) : paid;   // brak salda - najpierw obciaza splate dlugu
                    long real = Math.Max(0L, paid - gap);
                    _dDebtUnpaid += paid - real;
                    if (real > 0)
                    {
                        // recenzja C1 (OBIEG-1): splata najpierw gasi zaliczke gry - ta czesc wraca w nicosc (odbiorca portfela dostal ja juz z niczego);
                        // do skarbca tylko reszta (glowa zaplacila w saldzie - przelew rod -> skarbiec, nie w nicosc)
                        long adv = TakeAdvance(c, real);
                        long toWallet = real - adv;
                        if (toWallet > 0) { c.Kingdom.KingdomBudgetWallet += (int)toWallet; _dDebtRepaid += toWallet; }
                        _dAdvRepaid += adv;
                    }
                }
                else if (paid > 0) TakeAdvance(c, paid);   // bez skarbca (165 wylaczone w trakcie, rod bez krolestwa) - splata w nicosc gasi zaliczke tak samo
                TrimAdvance(c);                             // zaliczka nigdy wieksza niz dlug (dlug darowany albo splacony poza nasza latka)
            }
            catch (Exception e) { Stumble("ClanTickEnd", e); }
            return paid;
        }

        /// <summary>Recenzja C1 (OBIEG-1), SoldierPay.Settle: czesc dzisiejszego przyrostu dlugu rodu, ktorej nie zrownowazyl obciety zold - zaliczka gry z niczego.</summary>
        internal static void NoteAdvance(Clan c, long amount)
        {
            try
            {
                if (c == null || c.StringId == null || amount <= 0 || !On) return;
                long v; _advance.TryGetValue(c.StringId, out v);
                v = Math.Min(v + amount, Math.Max(0L, (long)c.DebtToKingdom));
                if (v > 0) _advance[c.StringId] = v; else _advance.Remove(c.StringId);
                _dAdvNew += amount;
            }
            catch (Exception e) { Stumble("NoteAdvance", e); }
        }

        /// <summary>Splata `amount` gasi najpierw zaliczke gry rodu - zwraca zgaszona czesc.</summary>
        private static long TakeAdvance(Clan c, long amount)
        {
            long v;
            if (c == null || c.StringId == null || amount <= 0 || !_advance.TryGetValue(c.StringId, out v)) return 0;
            long adv = Math.Min(amount, v);
            v -= adv;
            if (v > 0) _advance[c.StringId] = v; else _advance.Remove(c.StringId);
            return adv;
        }

        private static void TrimAdvance(Clan c)
        {
            long v;
            if (c == null || c.StringId == null || !_advance.TryGetValue(c.StringId, out v)) return;
            long debt = Math.Max(0L, (long)c.DebtToKingdom);
            if (debt <= 0) _advance.Remove(c.StringId);
            else if (v > debt) _advance[c.StringId] = debt;
        }

        private static long AdvanceTotal() { long t = 0; foreach (var kv in _advance) t += kv.Value; return t; }

        private static Harmony _harmony;

        /// <summary>Przy starcie gry: tylko Diplomacy (klasa bez pol statycznych gry). Latka na model finansow gry - w kampanii (EnsureHooks).</summary>
        internal static void ApplyAll(Harmony h)
        {
            _harmony = h;
            string rep = "BRAK (brak Diplomacy)";
            try
            {
                var t = AccessTools.TypeByName("Diplomacy.Costs.KingdomWalletCost");
                var m = t != null ? AccessTools.Method(t, "ApplyCost", Type.EmptyTypes) : null;
                if (m != null)
                {
                    // po oknie ksiegi obiegu (Priority.First) - okno widzi, ze skarbce sie nie zmienily
                    h.Patch(m, prefix: new HarmonyMethod(typeof(CrownIncome), nameof(ReparationPrefix)) { priority = Priority.Normal });
                    _repWired = true; rep = "wpiete";
                }
            }
            catch (Exception e) { Log.Error("CrownIncome.ApplyAll(reparacje)", e); }
            Log.Info("CrownIncome (165): korona z biezacych wplywow - reparacje Diplomacy jako dlug korona-korona " + rep
                     + "; splata dlugu wobec korony do skarbca dojdzie w kampanii; wylacznik Crown Current Income (czynny tylko z Clan Budget Enabled).");
        }

        /// <summary>
        /// W kampanii (OnSessionLaunched), raz na proces: postfiks na DefaultClanFinanceModel.AddPaymentForDebts. Pulapka z SoldierPay: kompilacja
        /// metody czytajacej pole statyczne DefaultClanFinanceModel uruchamia jego konstruktor statyczny (czyta Game.Current) - przy starcie gry zabiloby to klase.
        /// </summary>
        internal static void EnsureHooks()
        {
            if (_debtTried || _harmony == null || Campaign.Current == null) return;
            _debtTried = true;
            try
            {
                var m = AccessTools.Method(typeof(DefaultClanFinanceModel), "AddPaymentForDebts");
                if (m != null)
                {
                    _harmony.Patch(m, prefix: new HarmonyMethod(typeof(CrownIncome), nameof(DebtPrefix)), postfix: new HarmonyMethod(typeof(CrownIncome), nameof(DebtPostfix)));
                    _debtWired = true;
                }
            }
            catch (Exception e) { Log.Error("CrownIncome.EnsureHooks(dlug wobec korony)", e); }
            Log.Info("CrownIncome (165): splata dlugu wobec korony (AddPaymentForDebts) do skarbca " + (_debtWired ? "wpieta" : "BRAK") + ".");
        }
        private static bool _debtTried;

        // ------------------------------------------------------------ zapis (SaveText, "arm_crown165")
        /// <summary>"v1|doba migawki|id=skarbiec;...|placacy>odbiorca>reszta>calosc>doba;...|idRodu=zaliczka gry;..." (piate pole - recenzja C1, OBIEG-1;
        /// zapis bez niego wczytuje sie jak dotad, starsza wersja moda piate pole pomija)</summary>
        internal static string Export()
        {
            try
            {
                var sb = new StringBuilder(64 + _wEnd.Count * 24 + _debts.Count * 48 + _advance.Count * 32);
                sb.Append("v1|").Append(_wEndDay.ToString(Inv)).Append('|');
                bool first = true;
                foreach (var kv in _wEnd)
                {
                    if (kv.Key == null || kv.Key.IndexOfAny(Bad) >= 0) continue;
                    if (!first) sb.Append(';'); first = false;
                    sb.Append(kv.Key).Append('=').Append(kv.Value.ToString(Inv));
                }
                sb.Append('|');
                first = true;
                foreach (var d in _debts)
                {
                    if (d.Left <= 0 || d.Payer == null || d.Receiver == null || d.Payer.IndexOfAny(Bad) >= 0 || d.Receiver.IndexOfAny(Bad) >= 0) continue;
                    if (!first) sb.Append(';'); first = false;
                    sb.Append(d.Payer).Append('>').Append(d.Receiver).Append('>').Append(d.Left.ToString(Inv)).Append('>').Append(d.Total.ToString(Inv)).Append('>').Append(d.Day.ToString(Inv));
                }
                sb.Append('|');
                first = true;
                if (_advance.Count > 0)
                {
                    // tylko zywe rody z dlugiem (rod wymarly albo bez dlugu - zaliczki juz nie ma czym splacac)
                    var live = new Dictionary<string, int>();
                    foreach (var c in Clan.All) if (c != null && !c.IsEliminated && c.StringId != null && c.DebtToKingdom > 0) live[c.StringId] = c.DebtToKingdom;
                    foreach (var kv in _advance)
                    {
                        int debt;
                        if (kv.Key == null || kv.Key.IndexOfAny(Bad) >= 0 || kv.Value <= 0 || !live.TryGetValue(kv.Key, out debt)) continue;
                        if (!first) sb.Append(';'); first = false;
                        sb.Append(kv.Key).Append('=').Append(Math.Min(kv.Value, (long)debt).ToString(Inv));
                    }
                }
                return sb.ToString();
            }
            catch (Exception e) { Stumble("Export", e); return ""; }
        }
        private static readonly char[] Bad = { '|', ';', '=', '>' };

        internal static void Import(string data)
        {
            _wEnd.Clear(); _wEndDay = -1; _debts.Clear(); _advance.Clear(); _importN = 0; _importBad = 0;
            try
            {
                if (string.IsNullOrEmpty(data)) return;
                var f = data.Split('|');
                if (f.Length < 4 || f[0] != "v1") { _importBad++; return; }
                int.TryParse(f[1], NumberStyles.Integer, Inv, out _wEndDay);
                if (f[2].Length > 0)
                    foreach (var p in f[2].Split(';'))
                    {
                        var kv = p.Split('='); long v;
                        if (kv.Length == 2 && kv[0].Length > 0 && long.TryParse(kv[1], NumberStyles.Integer, Inv, out v)) _wEnd[kv[0]] = v;
                    }
                if (f[3].Length > 0)
                    foreach (var p in f[3].Split(';'))
                    {
                        var x = p.Split('>'); long left, total; int day;
                        if (x.Length == 5 && x[0].Length > 0 && x[1].Length > 0 && long.TryParse(x[2], NumberStyles.Integer, Inv, out left) && long.TryParse(x[3], NumberStyles.Integer, Inv, out total)
                            && int.TryParse(x[4], NumberStyles.Integer, Inv, out day) && left > 0)
                        { _debts.Add(new Debt { Payer = x[0], Receiver = x[1], Left = left, Total = total, Day = day }); _importN++; }
                        else _importBad++;
                    }
                if (f.Length >= 5 && f[4].Length > 0)   // recenzja C1 (OBIEG-1): zaliczki gry; zapis bez tego pola - brak zaliczek (dlugi zostaja przy skarbcu, tabela 165)
                    foreach (var p in f[4].Split(';'))
                    {
                        var kv = p.Split('='); long v;
                        if (kv.Length == 2 && kv[0].Length > 0 && long.TryParse(kv[1], NumberStyles.Integer, Inv, out v) && v > 0) _advance[kv[0]] = v;
                        else _importBad++;
                    }
            }
            catch (Exception e) { Stumble("Import", e); }
        }
    }
}
