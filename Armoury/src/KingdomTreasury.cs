using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// SKARBIEC KROLESTWA BEZ ZLOTA Z NICZEGO (Jeff 04.10: "jak z niczego dosypuje? ... tak, wylacz").
    /// Vanilla ClanVariablesCampaignBehavior.DailyTickClan, przy rozliczeniu rodu krola:
    ///   - skarbiec < 2 mln -> +1000 dziennie, z niczego;
    ///   - skarbiec < 1 mln -> losowanie 0.5% (ponizej 100 tys.: 1%) dziennie na +100/200/400 tys., z niczego.
    /// Transpiler zeruje te stale (ldc.i4 1000 / 100000 / 200000 / 400000 w tej jednej metodzie).
    /// Zostaje prawdziwy przeplyw: rody AI z ponad 100 tys. wplacaja 1% nadwyzki dziennie
    /// (DefaultClanFinanceModel), BK swoje wplaty; zapomogi dla biednych rodow placi skarbiec z tego,
    /// co w nim faktycznie jest. Startowe 2 mln na poczatek kampanii zostaja.
    /// </summary>
    internal static class KingdomTreasury
    {
        private static int _swapped;

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var s = Settings.Current;
            bool on = s == null || s.NoFreeKingdomGold;
            foreach (var ci in instructions)
            {
                if (on && ci.opcode == OpCodes.Ldc_I4 && ci.operand is int)
                {
                    int v = (int)ci.operand;
                    if (v == 1000 || v == 100000 || v == 200000 || v == 400000) { ci.operand = 0; _swapped++; }
                }
                yield return ci;
            }
        }

        // ------------------------------------------------------------ powinnosci wasali
        // Jeff 04.10: "prawdziwe zasilanie skarbca - kazdy rod oddaje koronie czesc dochodu, tak jak bylo historycznie".
        // Historycznie (Anglia XIII-XIV w.): w pokoju wasal dawal glownie sluzbe i okazjonalne "pomoce" - male
        // pieniadze; wielkie podatki (pietnastka i dziesiecina od ruchomosci, tarczowe zamiast sluzby) korona
        // dostawala na WOJNE. Stad dwie stawki od dziennego dochodu rodu (model finansow + nasze renty od ludnosci):
        // pokoj `CrownDuesPeacePercent` (2%), wojna krolestwa `CrownDuesWarPercent` (10%). Placa rody wasalne, takze
        // gracza; nie rod krola (to jego skarbiec), nie najemnicy. Zloto od glowy rodu do skarbca krolestwa.
        internal static void Daily()
        {
            var s = Settings.Current;
            if (s == null || !s.CrownDuesEnabled || TaleWorlds.CampaignSystem.Campaign.Current == null) return;
            try
            {
                long total = 0; int payers = 0; int playerPaid = 0;
                var model = TaleWorlds.CampaignSystem.Campaign.Current.Models.ClanFinanceModel;
                foreach (var c in TaleWorlds.CampaignSystem.Clan.All)
                {
                    if (c == null || c.IsEliminated || c.Kingdom == null || c.Leader == null || !c.Leader.IsAlive) continue;
                    if (c.IsUnderMercenaryService || c == c.Kingdom.RulingClan || c.IsBanditFaction) continue;
                    float income = 0f;
                    try { income = model.CalculateClanIncome(c, false, false, false).ResultNumber; } catch { }
                    int rent; PopulationLaw.RentToday.TryGetValue(c, out rent);
                    income += rent;
                    if (income <= 0f) continue;
                    bool war = false;
                    try { foreach (var k in TaleWorlds.CampaignSystem.Kingdom.All) if (k != c.Kingdom && !k.IsEliminated && c.Kingdom.IsAtWarWith(k)) { war = true; break; } } catch { }
                    float rate = (war ? s.CrownDuesWarPercent : s.CrownDuesPeacePercent) / 100f;
                    int pay = (int)Math.Min(income * rate, Math.Max(0, c.Leader.Gold));
                    if (pay <= 0) continue;
                    c.Leader.ChangeHeroGold(-pay);
                    c.Kingdom.KingdomBudgetWallet += pay;
                    total += pay; payers++;
                    if (c == TaleWorlds.CampaignSystem.Clan.PlayerClan) playerPaid = pay;
                }
                Log.Info("Korona: dzien " + (int)TaleWorlds.CampaignSystem.CampaignTime.Now.ToDays + " - powinnosci wasali " + total + " zl od " + payers + " rodow do skarbcow krolestw"
                         + (playerPaid > 0 ? " (rod gracza " + playerPaid + ")" : "") + ".");
            }
            catch (Exception e) { Log.Error("KingdomTreasury.Daily", e); }
        }

        // ------------------------------------------------------------ zwrot zoldu w wojnie
        // Jeff 05.10: "skarbiec krolestwa w czasie wojny zwraca lordom polowe dziennego zoldu, dopoki ma z czego" (audyt 05.10
        // pieniadz/P3, fundament K11: skarbce tylko rosly - BK wylacza jedyna wyplate gry). Raz na dobe, po powinnosciach i
        // daninach: krolestwo w wojnie z innym krolestwem zwraca kazdemu platnikowi `CrownWageRefundPercent` (50%) zoldu, ktory
        // jego partie (i garnizony - `CrownWageRefundGarrisons`) NAPRAWDE zaplacily od poprzedniego rozliczenia (SoldierPay liczy
        // to, co zeszlo z kies). Gdy skarbiec nie ma dosc - wszystkim proporcjonalnie mniej; skarbiec nie schodzi ponizej zera.
        // Rod krola i rod gracza na tych samych zasadach; najemnicy, rody bez krolestwa i krolestwa w pokoju - nic.
        // Zloto: skarbiec krolestwa -> kiesa platnika (glowa rodu albo rycerz, ktory sam oplacil swoja partie).
        internal struct RefundRow { public long Paid, Due, Given; public int Clans; }
        private static readonly Dictionary<Kingdom, RefundRow> _refund = new Dictionary<Kingdom, RefundRow>();

        internal static void Reset() { _refund.Clear(); _refundErr = false; }

        private static bool AtWar(Kingdom k)
        {
            try { foreach (var o in Kingdom.All) if (o != k && !o.IsEliminated && k.IsAtWarWith(o)) return true; } catch { }
            return false;
        }

        /// <summary>Jaka czesc kasy miasta ponad prog bierze dzis danina wojenna tego krolestwa (0 w pokoju albo gdy wylaczona) - jak w Levies.</summary>
        internal static float LaySubsidyTownRate(Kingdom k)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || k == null || !s.LaySubsidyEnabled || !AtWar(k)) return 0f;
                return Math.Max(0f, s.LaySubsidyTownShare) * (k.ActivePolicies.Contains(DefaultPolicies.WarTax) ? Math.Max(1f, s.LaySubsidyWarTaxMultiplier) : 1f);
            }
            catch { return 0f; }
        }

        /// <summary>Podzial zwrotu: kazdemu `due[i]`, a gdy skarbiec (`have`) nie starcza - proporcjonalnie mniej, w dol; suma nigdy ponad `have`.</summary>
        internal static int[] Split(int[] due, long have)
        {
            var give = new int[due.Length];
            long want = 0;
            for (int i = 0; i < due.Length; i++) if (due[i] > 0) want += due[i];
            if (want <= 0 || have <= 0) return give;
            for (int i = 0; i < due.Length; i++)
            {
                if (due[i] <= 0) continue;
                give[i] = have >= want ? due[i] : (int)((long)due[i] * have / want);
            }
            return give;
        }

        /// <summary>Dopisek do linii "Skarbce:" - zwrot zoldu tego krolestwa w dzisiejszym rozliczeniu.</summary>
        internal static string RefundNote(Kingdom k)
        {
            RefundRow r;
            if (k == null || !_refund.TryGetValue(k, out r)) return "";
            return ", zwrot zoldu " + r.Given + " dla " + r.Clans + " rodow" + (r.Given < r.Due ? " (nalezne " + r.Due + " - skarbiec nie mial dosc)" : "");
        }

        internal static void WageRefund()
        {
            _refund.Clear();
            var s = Settings.Current;
            if (s == null || Campaign.Current == null) return;
            try
            {
                var paid = SoldierPay.TakePaid();           // zawsze oproznia licznik doby
                if (!s.CrownWageRefundEnabled) return;
                float pct = Math.Max(0f, Math.Min(100f, s.CrownWageRefundPercent)) / 100f;
                var byKingdom = new Dictionary<Kingdom, List<KeyValuePair<Hero, int>>>();
                long peace = 0, noKingdom = 0, merc = 0;
                foreach (var kv in paid)
                {
                    var h = kv.Key;
                    if (h == null || kv.Value <= 0) continue;
                    var c = h.Clan;
                    if (c == null || c.IsEliminated) continue;
                    if (!h.IsAlive) { h = c.Leader; if (h == null || !h.IsAlive) continue; }   // platnik nie dozyl rozliczenia - zwrot bierze glowa jego rodu
                    var k = c.Kingdom;
                    if (k == null || k.IsEliminated) { noKingdom += kv.Value; continue; }
                    if (c.IsUnderMercenaryService) { merc += kv.Value; continue; }
                    List<KeyValuePair<Hero, int>> list;
                    if (!byKingdom.TryGetValue(k, out list)) { list = new List<KeyValuePair<Hero, int>>(); byKingdom[k] = list; }
                    list.Add(new KeyValuePair<Hero, int>(h, kv.Value));
                }
                long totalGiven = 0, totalDue = 0, totalPaid = 0, left = 0, toDebtors = 0; int kingdoms = 0, shortK = 0, emptyK = 0, playerGot = 0, stumbles = 0;
                var allClans = new HashSet<Clan>(); var debtors = new HashSet<Clan>();
                foreach (var kk in byKingdom)
                {
                    var k = kk.Key; var list = kk.Value;
                    long paidSum = 0, want = 0, given = 0, have = 0; var clans = new HashSet<Clan>();
                    bool counted = false;
                    // wyjatek przy jednym krolestwie nie zatrzymuje zwrotu w pozostalych (pierwszy do pliku, kolejne liczone)
                    try
                    {
                        foreach (var kv in list) paidSum += kv.Value;
                        if (!AtWar(k)) { peace += paidSum; continue; }
                        var due = new int[list.Count];
                        for (int i = 0; i < list.Count; i++) { due[i] = (int)(list[i].Value * pct); want += due[i]; }
                        if (want <= 0) continue;
                        counted = true;
                        have = Math.Max(0, k.KingdomBudgetWallet);      // Diplomacy potrafi zostawic skarbiec na minusie - wtedy nic nie placimy
                        var give = Split(due, have);
                        for (int i = 0; i < list.Count; i++)
                        {
                            if (give[i] <= 0) continue;
                            var h = list[i].Key;
                            k.KingdomBudgetWallet -= give[i];   // najpierw skarbiec, potem platnik: skarbiec oddaje dokladnie tyle, ile dostal platnik
                            h.ChangeHeroGold(give[i]);
                            given += give[i];
                            clans.Add(h.Clan); allClans.Add(h.Clan);
                            if (h.Clan == Clan.PlayerClan) playerGot += give[i];
                            // tylko pomiar: rod dluzny koronie (Clan.DebtToKingdom) - gra przy najblizszym rozliczeniu zabierze mu kiese na splate
                            // dlugu (vanilla AddPaymentForDebts), a ta splata nie trafia do skarbca
                            if (h.Clan != null && h.Clan.DebtToKingdom > 0) { toDebtors += give[i]; debtors.Add(h.Clan); }
                        }
                    }
                    catch (Exception e)
                    {
                        stumbles++;
                        if (!_refundErr) { _refundErr = true; Log.Error("KingdomTreasury.WageRefund(" + (k != null && k.Name != null ? k.Name.ToString() : "?") + ")", e); }
                    }
                    if (!counted) continue;
                    _refund[k] = new RefundRow { Paid = paidSum, Due = want, Given = given, Clans = clans.Count };
                    kingdoms++; totalGiven += given; totalDue += want; totalPaid += paidSum; left += k.KingdomBudgetWallet;
                    if (given < want) { shortK++; if (have <= 0) emptyK++; }
                }
                Log.Info("Korona: dzien " + (int)CampaignTime.Now.ToDays + " - zwrot zoldu ze skarbcow krolestw w wojnie: " + totalGiven + " zl dla " + allClans.Count + " rodow w "
                         + kingdoms + " krolestwach (" + (pct * 100f).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "% z " + totalPaid
                         + " zaplaconego zoldu = nalezne " + totalDue + "); skarbiec nie mial dosc w " + shortK + " krolestwach (w tym pusty: " + emptyK + "), niedoplata "
                         + (totalDue - totalGiven) + "; w skarbcach tych krolestw zostalo " + left
                         + (playerGot > 0 ? "; rod gracza dostal " + playerGot : "")
                         + (toDebtors > 0 ? "; z tego " + toDebtors + " dla " + debtors.Count + " rodow dluznych koronie (gra zabierze im to na splate dlugu, ktora nie wraca do skarbca)" : "")
                         + ". Bez zwrotu: zold krolestw w pokoju " + peace + ", rodow bez krolestwa " + noKingdom + ", najemnikow " + merc + "."
                         + (stumbles > 0 ? " Potkniecia: wyjatek przy " + stumbles + " krolestwach." : ""));
                if (playerGot > 0)
                    Log.Player("The crown repaid your house " + playerGot + " denars of the wages it paid today - the realm is at war.");
            }
            catch (Exception e) { Log.Error("KingdomTreasury.WageRefund", e); }
        }
        private static bool _refundErr;

        // ------------------------------------------------------------ danina wojenna, clo, mennica, monopole (wpisy 55-56)
        // Jeff 04.10 "tak" na E i G (docs/ZRODLA-DOCHODU.md C3-C9). Historycznie: pietnastka i dziesiecina (1290, 1334) placili
        // poddani, nie panowie; clo od handlu (1275) mial kazdy krol; oplata menniczna szla z monety w obiegu; monopole z
        // dzierzaw. Wszystko z kiesy kogos - zadnego zlota z powietrza.
        internal static void Levies()
        {
            var s = Settings.Current;
            if (s == null || Campaign.Current == null) return;
            try
            {
                long sub = 0, cus = 0, deb = 0, mon = 0;
                foreach (var k in Kingdom.All)
                {
                    if (k == null || k.IsEliminated) continue;
                    bool war = false;
                    try { foreach (var o in Kingdom.All) if (o != k && !o.IsEliminated && k.IsAtWarWith(o)) { war = true; break; } } catch { }
                    bool warTax = k.ActivePolicies.Contains(DefaultPolicies.WarTax);
                    bool duty = k.ActivePolicies.Contains(DefaultPolicies.CrownDuty);
                    bool debase = k.ActivePolicies.Contains(DefaultPolicies.DebasementOfTheCurrency);
                    bool mono = k.ActivePolicies.Contains(DefaultPolicies.StateMonopolies);
                    var ruler = k.RulingClan != null ? k.RulingClan.Leader : null;
                    foreach (var st in k.Settlements)
                    {
                        if (st == null || st.SettlementComponent == null) continue;
                        bool town = st.IsTown && st.Town != null;
                        if (!town && !st.IsVillage) continue;
                        if (st.IsUnderSiege || (st.IsVillage && st.Village.VillageState != Village.VillageStates.Normal)) continue;
                        int gold = st.SettlementComponent.Gold;
                        // krok K6: granica to zapas kupcow miasta (TownPurse.TaxFloor; przy wylaczonym K6 dotychczasowe TownRentFloorGold)
                        int spare = town ? Math.Max(0, gold - TownPurse.TaxFloor(st.Town)) : Math.Max(0, gold);
                        // danina wojenna (lay subsidy)
                        if (s.LaySubsidyEnabled && war && spare > 0)
                        {
                            float share = (town ? s.LaySubsidyTownShare : s.LaySubsidyVillageShare) * (warTax ? Math.Max(1f, s.LaySubsidyWarTaxMultiplier) : 1f);
                            int x = (int)Math.Min(spare, spare * Math.Max(0f, share));
                            if (x > 0) { st.SettlementComponent.ChangeGold(-x); k.KingdomBudgetWallet += x; sub += x; spare -= x; }
                        }
                        if (!town) continue;
                        // clo od handlu - z licznika cel miasta
                        if (s.CrownCustomsEnabled && st.Town.TradeTaxAccumulated > 0)
                        {
                            float share = Math.Max(0f, s.CrownCustomsShare) * (duty ? Math.Max(1f, s.CrownCustomsDutyMultiplier) : 1f);
                            int x = (int)(st.Town.TradeTaxAccumulated * Math.Min(1f, share));
                            // wpis 90 (audyt, kod gry): licznik cel to MIARA handlu (prowizja dopisywana, nikt jej nie placi) -
                            // do skarbca idzie tyle, ile realnie odda kasa miasta ponad prog
                            int paid = Math.Min(x, spare);
                            if (x > 0) st.Town.TradeTaxAccumulated -= x;
                            if (paid > 0) { st.SettlementComponent.ChangeGold(-paid); k.KingdomBudgetWallet += paid; cus += paid; spare -= paid; }
                        }
                        if (!s.PolicyIncomeConserved || ruler == null) continue;
                        // mennica: oplata z kasy miasta do krola (zamiast 100 d na lenno z niczego)
                        if (debase && spare > 0)
                        {
                            int x = (int)(spare * Math.Max(0f, s.DebasementShare));
                            if (x > 0) { st.SettlementComponent.ChangeGold(-x); ruler.ChangeHeroGold(x); deb += x; spare -= x; }
                        }
                        // monopole: 5% zysku warsztatow w miastach rodu krola, z KAPITALU warsztatu
                        if (mono && st.OwnerClan == k.RulingClan)
                            foreach (var w in st.Town.Workshops)
                            {
                                if (w == null || w.Owner == null || w.Owner == ruler) continue;
                                int x = Math.Min((int)(w.ProfitMade * 0.05f), Math.Max(0, w.Capital));
                                if (x > 0) { w.ChangeGold(-x); ruler.ChangeHeroGold(x); mon += x; }
                            }
                    }
                }
                Log.Info("Korona: dzien " + (int)CampaignTime.Now.ToDays + " - danina wojenna z kas osad " + sub + ", clo od handlu miast " + cus
                         + " (do skarbcow krolestw); mennica " + deb + ", monopole " + mon + " (do krolow, z kas miast i kapitalu warsztatow).");
            }
            catch (Exception e) { Log.Error("KingdomTreasury.Levies", e); }
        }

        // Debasement (100 d na lenno) i State Monopolies (5% zysku, nie odejmowane) w AddRulingClanIncome sa z niczego - odejmujemy je
        // w tym samym rozliczeniu (pobieramy je realnie w Levies)
        // wpis 88 (audyt pkt 5, sprawdzone w vanilla AddRulingClanIncome): Road Tolls i State Monopolies dodaja sie NARASTAJACO
        // w petli po miastach (krol z miastami A, B, C dostaje 3a+2b+c), Land Tax (5% handlu wsi) i War Tax (5% podatku miast)
        // nie zdejmuja nic z nikogo. Odejmujemy DOKLADNIE to, co dodala gra (z narastaniem), i doliczamy kwote prawdziwa,
        // pobrana z kas osad (gdy gra naprawde rozlicza dzien - applyWithdrawals): mytem z kas miast krola, podatkiem gruntowym
        // z kas wsi krolestwa, podatkiem wojennym z kas miast. Mennica i monopole - pobierane w Levies (z kas i kapitalu).
        public static void RulingIncomePostfix(Clan clan, ref ExplainedNumber goldChange, bool applyWithdrawals)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.PolicyIncomeConserved || clan == null || clan.Kingdom == null) return;
                var k = clan.Kingdom;
                var pol = k.ActivePolicies;
                float smooth = 5f;
                try { smooth = Math.Max(0.01f, Campaign.Current.Models.ClanFinanceModel.RevenueSmoothenFraction()); } catch { }
                float vanilla = 0f, real = 0f;
                // wpis 90 (audyt, kod gry): Crown Duty - gra daje krolowi 5% LICZNIKA cel (miara handlu, nikt jej nie placi);
                // licznik zdejmuje sama, my odejmujemy kwote z niczego i bierzemy ja z kasy miasta ponad prog
                if (pol.Contains(DefaultPolicies.CrownDuty))
                    foreach (var f in k.Fiefs)
                    {
                        if (f == null) continue;
                        // po zdjeciu przez gre licznik ma 95% dawnej wartosci: dawna x 5% = obecna x 5/95
                        int due = applyWithdrawals ? (int)(f.TradeTaxAccumulated * 5f / 95f) : (int)(f.TradeTaxAccumulated * 0.05f);
                        vanilla += due;
                        int x = Math.Min(due, Math.Max(0, f.Gold - TownPurse.TaxFloor(f)));   // krok K6: zapas kupcow miasta (zamek: jak dotad)
                        if (applyWithdrawals && x > 0) f.ChangeGold(-x);
                        real += x;
                    }
                // mennica: 100 d na lenno z niczego (pobierana w Levies)
                if (pol.Contains(DefaultPolicies.DebasementOfTheCurrency)) vanilla += k.Fiefs.Count * 100;
                // Land Tax: 5% licznika handlu wsi cudzych rodow - z kas tych wsi
                if (pol.Contains(DefaultPolicies.LandTax))
                    foreach (var v in k.Villages)
                    {
                        if (v == null || v.IsOwnerUnassigned || v.Settlement.OwnerClan == clan || v.VillageState == Village.VillageStates.Looted || v.VillageState == Village.VillageStates.BeingRaided) continue;
                        int due = (int)((int)(v.TradeTaxAccumulated / smooth) * 0.05f);
                        vanilla += due;
                        int x = Math.Min(due, Math.Max(0, v.Settlement.SettlementComponent.Gold));
                        if (applyWithdrawals && x > 0) v.Settlement.SettlementComponent.ChangeGold(-x);
                        real += x;
                    }
                // War Tax: 5% podatku miast krolestwa - z kas tych miast
                if (pol.Contains(DefaultPolicies.WarTax))
                {
                    float sum = 0f;
                    foreach (var f in k.Fiefs) { try { sum += Campaign.Current.Models.SettlementTaxModel.CalculateTownTax(f).ResultNumber; } catch { } }
                    vanilla += (int)(sum * 0.05f);
                    foreach (var f in k.Fiefs)
                    {
                        if (f == null) continue;
                        float t = 0f; try { t = Campaign.Current.Models.SettlementTaxModel.CalculateTownTax(f).ResultNumber; } catch { }
                        int x = Math.Min((int)(t * 0.05f), Math.Max(0, f.Gold - TownPurse.TaxFloor(f)));   // wpis 90: prog kasy miasta (krok K6: zapas kupcow)
                        if (x <= 0) continue;
                        if (applyWithdrawals) f.ChangeGold(-x);
                        real += x;
                    }
                }
                // Road Tolls i Monopole: narastajaco jak w grze; myto prawdziwe - z kas miast krola
                bool tolls = pol.Contains(DefaultPolicies.RoadTolls), mono = pol.Contains(DefaultPolicies.StateMonopolies);
                int n6 = 0, n7 = 0;
                foreach (var st in clan.Settlements)
                {
                    if (st == null || !st.IsTown || st.Town == null) continue;
                    if (tolls)
                    {
                        // po zdjeciu przez gre licznik ma 29/30 dawnej wartosci: dawna/30 = obecna/29
                        int r = applyWithdrawals ? st.Town.TradeTaxAccumulated / 29 : st.Town.TradeTaxAccumulated / 30;
                        n6 += r;
                        int x = Math.Min(r, Math.Max(0, st.Town.Gold - TownPurse.TaxFloor(st.Town)));   // wpis 90: prog kasy miasta (krok K6: zapas kupcow)
                        if (applyWithdrawals && x > 0) st.Town.ChangeGold(-x);
                        real += x;
                    }
                    if (mono) { int sum = 0; foreach (var w in st.Town.Workshops) if (w != null) sum += w.ProfitMade; n7 += (int)(sum * 0.05f); }
                    if (n6 > 0) vanilla += n6;
                    if (n7 > 0) vanilla += n7;
                }
                float delta = real - vanilla;
                if (Math.Abs(delta) > 0.5f) goldChange.Add(delta, _txtPolicy);
            }
            catch { }
        }
        private static readonly TaleWorlds.Localization.TextObject _txtPolicy = new TaleWorlds.Localization.TextObject("{=!}Crown dues are collected from the towns, villages and workshops themselves");

        public static void CaravanVisitPostfix(ref int __result) { var s = Settings.Current; if (s != null && s.PolicyIncomeConserved) __result = 0; }
        public static bool TaxOfficePrefix() { var s = Settings.Current; return s == null || !s.PolicyIncomeConserved; }

        // ROT ROTCoreBehavior.DailyTickClan: kazdy rod AI z kiesa <= 10 000 dostaje CODZIENNIE Tier x 5000 z niczego
        // (audyt 04.10, spis stalych modow) - dlatego nikt nie bankrutowal ani nie pozyczal w Banku Zelaznym.
        public static bool RotBailoutPrefix() { var s = Settings.Current; return s == null || !s.NoRotClanBailout; }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var m = AccessTools.Method(typeof(ClanVariablesCampaignBehavior), "DailyTickClan");
                if (m != null) h.Patch(m, transpiler: new HarmonyMethod(typeof(KingdomTreasury), nameof(Transpiler)));
                var rot = AccessTools.TypeByName("ROT.CampaignBehaviors.ROTCoreBehavior");
                var rm = rot != null ? AccessTools.Method(rot, "DailyTickClan") : null;
                if (rm != null) h.Patch(rm, prefix: new HarmonyMethod(typeof(KingdomTreasury), nameof(RotBailoutPrefix)));
                var ri = AccessTools.Method(typeof(TaleWorlds.CampaignSystem.GameComponents.DefaultClanFinanceModel), "AddRulingClanIncome");
                if (ri != null) h.Patch(ri, postfix: new HarmonyMethod(typeof(KingdomTreasury), nameof(RulingIncomePostfix)));
                int tv = 0;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type[] types;
                    try { types = asm.GetTypes(); } catch (System.Reflection.ReflectionTypeLoadException e) { types = e.Types; } catch { continue; }
                    foreach (var t in types)
                    {
                        try
                        {
                            if (t == null || t.IsAbstract || !typeof(TaleWorlds.CampaignSystem.ComponentInterfaces.TradeAgreementModel).IsAssignableFrom(t)) continue;
                            var pm = t.GetMethod("GetProfitPerCaravanVisit", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly);
                            if (pm != null) { h.Patch(pm, postfix: new HarmonyMethod(typeof(KingdomTreasury), nameof(CaravanVisitPostfix))); tv++; }
                        }
                        catch { }
                    }
                }
                var to = AccessTools.Method("BannerKings.Models.Vanilla.BKTaxModel:AddVillagePopulationTaxes");
                if (to != null) h.Patch(to, prefix: new HarmonyMethod(typeof(KingdomTreasury), nameof(TaxOfficePrefix)));
                Log.Info("KingdomTreasury: polityki bez zlota z niczego - mennica/monopole " + (ri != null ? "wpiete" : "BRAK") + ", umowy handlowe w " + tv + " modelach, Tax Office BK " + (to != null ? "wpiety" : "BRAK") + ".");
                Log.Info("KingdomTreasury: zapomoga ROT dla biednych rodow " + (rm != null ? "przechwycona (MCM No Rot Clan Bailout)" : "BRAK ROTCoreBehavior.DailyTickClan") + ".");
                Log.Info("KingdomTreasury: zloto z niczego do skarbca krolestw " + (m != null ? "wylaczone (podmienionych stalych " + _swapped + ", oczekiwane 4)" : "BRAK DailyTickClan") + ".");
            }
            catch (Exception e) { Log.Error("KingdomTreasury.ApplyAll", e); }
        }
    }
}
