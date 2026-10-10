using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// ZOLD DO OBIEGU (Jeff 05.10: "zold partii do sakiewek ludzi (wydaja w miastach), zold garnizonu do kasy jego osady - takze
    /// u gracza"; audyt 05.10 pieniadz/P2, fundament K9). Dotad zold schodzil z kies i znikal: gra i BK zdejmuja go z kiesy
    /// (Hero.Gold / PartyTradeGold) albo wliczaja do salda rodu i nikt go nie dostaje - najwieksze ujscie zlota swiata.
    ///
    /// Jak gra i BK pobieraja zold (dekompilacja: DefaultClanFinanceModel.AddExpensesFromPartiesAndGarrisons, AddPartyExpense,
    /// AddExpenseFromLeaderParty; BK EconomyPatches.ClanFinancesPatches.PartyExpensesPrefix - w tym zestawie modow czynny jest BK):
    ///  - kazda partia rodu przechodzi przez DefaultClanFinanceModel.CalculatePartyWage(partia, budzet, applyWithdrawals) - wynik to
    ///    min(zold, budzet rodu); BK wola ja refleksja, vanilla wprost. To jedyny wspolny punkt (ten sam, ktory liczy MoneyLedger);
    ///  - partia GLOWY rodu: kwota wchodzi do salda rodu, a saldo gra dopisuje glowie jednym GiveGoldAction (kiesa nie schodzi
    ///    ponizej zera - gdy saldo sie nie miesci, czesc wydatkow nie zostala zaplacona);
    ///  - pozostale partie rodu z wodzem: kwota schodzi od razu z kiesy wodza (BK: rycerz z lennem placi sam; innemu wodzowi rod
    ///    wyrownuje kiese z salda); partia bez wodza: z kiesy glowy rodu;
    ///  - garnizon i karawana: z kiesy partii (PartyTradeGold), rod wyrownuje ja z salda.
    ///  W kazdym przypadku z kiesy schodzi najwyzej tyle, ile w niej jest (Hero.Gold i PartyTradeGold obcinaja do zera).
    ///
    /// Co robimy: prefiks i postfiks na ClanVariablesCampaignBehavior.DailyTickClan otwieraja i zamykaja rozliczenie jednego rodu;
    /// postfiks na CalculatePartyWage zapisuje dla kazdej partii, ile za chwile zejdzie z kiesy platnika; postfiks na
    /// CalculateClanGoldChange czynnego modelu finansow zapamietuje kiese glowy i saldo tuz przed jego dopisaniem. Po rozliczeniu
    /// rodu: jesli saldo nie zmiescilo sie w kiesie glowy, wszystkie kwoty tego rodu sa proporcjonalnie przycinane o brak
    /// (pusta kiesa = brak wyplaty = brak wplaty); potem partia rodu -> sakiewka jej ludzi (MenPurse - wydaja w miastach),
    /// garnizon -> kasa jego miasta albo zamku. Nic ponad to, co naprawde zeszlo z kies. Karawany bez zmian.
    /// Zaplacone kwoty sa tez podstawa zwrotu ze skarbca krolestwa w wojnie (KingdomTreasury.WageRefund).
    ///
    /// Brak ukryty w dlugu wobec korony (przeglad paczki, proba na prawdziwym kodzie gry): gdy wydatki rodu przekraczaja kiese
    /// z dochodem dnia, a krolestwo ma do oplacenia najemnikow albo danine (MercenaryWallet / TributeWallet / CallToWarWallet
    /// ponizej zera - wystarczy -1) albo rod juz jest dluzny, vanilla DefaultClanFinanceModel.ApplyShareForExpenses i
    /// AddPaymentForDebts dopisuja brak do Clan.DebtToKingdom i WYROWNUJA saldo dokladnie do kiesy glowy. Saldo wyglada wtedy na
    /// zaplacone, choc zlota nie bylo - samo porownanie salda z kiesa nie widzi braku. Dlatego kazdy przyrost DebtToKingdom
    /// w trakcie rozliczenia rodu liczymy jako brak (takze niezaplacony udzial w najemnikach - bezpieczna strona: mniej wplat).
    ///
    /// Kiedy latamy (docs/ERRORS.md, pulapka z 14.09): Harmony kompiluje zalatana metode juz przy zakladaniu latki, a kompilacja
    /// metody, ktora czyta pole statyczne klasy, uruchamia konstruktor statyczny tej klasy. Konstruktor DefaultClanFinanceModel
    /// czyta Game.Current - przy starcie gry jeszcze pusty; nieudana proba zabija klase na caly proces. Dlatego przy starcie gry
    /// zakladamy tylko pare na DailyTickClan (ta metoda ma juz od dawna nasz transpiler), a latki na modelach - zold partii,
    /// saldo rodu i regulator kasy - dopiero w kampanii: przy pierwszym rozliczeniu rodu (EnsureHooks) i przy pierwszej wplacie
    /// pod tarcza (EnsureShieldHook). Latamy jedna implementacje salda i jedna regulatora: te, ktorej uzywa czynny model.
    /// </summary>
    internal static class SoldierPay
    {
        private struct Rec
        {
            public MobileParty Party;
            public int Wage;          // naliczone przez gre (min(zold, budzet))
            public int Paid;          // tyle zejdzie z kiesy platnika (albo wejdzie do salda rodu)
            public bool Balance;      // partia glowy rodu - kwota w saldzie rodu
            public Hero Payer;        // wodz placacy wprost z wlasnej kiesy (null: kiesa partii)
            public int PayerGold;     // jego kiesa przed zdjeciem zoldu
            public bool LedgerLord;   // 169b (tylko log): partia rodu wedlug ksiegi pieniadza w chwili naliczenia (nie zaloga, nie karawana, partia lorda)
        }

        // ------------------------------------------------------------ rozliczenie jednego rodu (miedzy prefiksem a postfiksem DailyTickClan)
        private static Clan _clan;
        private static readonly List<Rec> _recs = new List<Rec>();
        private static bool _haveNet;
        private static int _goldMid, _net;                 // kiesa glowy tuz przed dopisaniem salda i samo saldo
        private static int _debtBefore;                    // dlug rodu wobec korony przed rozliczeniem (jego przyrost = brak zapisany przez gre jako dlug)
        private static object _netModel;
        private static Type _netDecl;

        // ------------------------------------------------------------ zaplacone dzis wedlug platnika (podstawa zwrotu ze skarbca)
        private static readonly Dictionary<Hero, int> _paidToday = new Dictionary<Hero, int>();
        // B-2 (Z8): zold zalog ZAMKOW wplacony do kas zamkow od poprzedniego zwrotu, na rod (tylko zamki, z ktorych zold moze wrocic panu zaworem -
        // CastlePurse.ComesHome); gorna granica ciecia zwrotu w TakePaid
        private static readonly Dictionary<Clan, long> _castlePay = new Dictionary<Clan, long>();

        // ------------------------------------------------------------ liczniki doby (linia "Zold:")
        private static long _dLordAcc, _dLordTaken, _dGarAcc, _dGarTaken, _dToPurse, _dPlayer, _dToTowns, _dToCastles;
        private static long _dUndead, _dNoTown, _dOff, _dOther, _dCut, _dBlindGold, _dDebtCut;
        private static long _dGarToPurse; private static int _dGarToPurseN;   // K1 (A2): zold zalog do ich sakiewek
        private static long _dGarHome; private static int _dGarHomeN;         // 114-p / B-2 (Z8): zold zalog zamkow, ktory wrocil panu zaworem - bez zwrotu korony (rody)
        // B-4 (Z8): rody z krolestwem w wojnie (zwrot korony) i zoldem zalog we wlasnych zamkach - zold do kas tych zamkow, zawor panom, nalezny zwrot od reszty
        private static long _dZ8Pay, _dZ8Dues, _dZ8Refund; private static int _dZ8N;
        private static int _dLordN, _dGarN, _dToPurseN, _dToTownsN, _dToCastlesN, _dCutClans, _dBlind, _dDupes, _stumbles, _dDebtClans;
        private static bool _errLogged;

        // paczka 169 (tylko log): liczby doby dla linii "Obieg" (MoneyLedger.Daily czyta je pozniej w tym samym bloku);
        // zerowane w Reset i przez MoneyLedger.ClearLast169() na poczatku bloku (D20)
        internal static long LastLordAcc, LastLordTaken, LastGarAcc, LastGarTaken, LastToPurse, LastToTowns, LastToCastles, LastOther;
        internal static bool LastWatching;
        // 169b (tylko log): ten sam zbior partii co linia "Zold:" (liczba partii) i uzgodnienie z licznikiem ksiegi pieniadza (MoneyLedger.WagePostfix
        // liczy KAZDE wywolanie modelu i rodzaj partii w chwili naliczenia; my - rekord na partie w oknie rozliczenia i rodzaj przy rozdziale)
        internal static long LastLordN, LastGarN, LastToLordGold, LastFromLordGold, LastDupLordGold, LastOutLordGold;
        internal static int LastToLordN, LastFromLordN, LastDupLordN, LastOutLordN;
        private static long _dToLordGold, _dFromLordGold, _dDupLordGold, _dOutLordGold;
        private static int _dToLordN, _dFromLordN, _dDupLordN, _dOutLordN;
        // 169b, poprawka po recenzji (tylko log): naliczenia zerowe i ujemne (ksiega liczy kazdy wynik, my tylko > 0)
        internal static long LastNonPosLordGold; internal static int LastNonPosLordN;
        private static long _dNonPosLordGold; private static int _dNonPosLordN;

        internal static void ZeroLast()
        {
            LastLordAcc = LastLordTaken = LastGarAcc = LastGarTaken = LastToPurse = LastToTowns = LastToCastles = LastOther = 0;
            LastWatching = false;
            LastLordN = LastGarN = LastToLordGold = LastFromLordGold = LastDupLordGold = LastOutLordGold = 0;
            LastToLordN = LastFromLordN = LastDupLordN = LastOutLordN = 0;
            LastNonPosLordGold = 0; LastNonPosLordN = 0;
        }

        /// <summary>169b: partia rodu wedlug ksiegi pieniadza (MoneyLedger.WagePostfix: zaloga, karawana, partia lorda, inne - w tej kolejnosci).</summary>
        private static bool LedgerLordParty(MobileParty mp) { return mp != null && !mp.IsGarrison && !mp.IsCaravan && mp.IsLordParty; }

        private static readonly Type[] NetArgs = { typeof(Clan), typeof(bool), typeof(bool), typeof(bool) };

        // ------------------------------------------------------------ latki na modelach: zakladane w kampanii, raz na proces (Reset ich nie rusza)
        private static Harmony _harmony;
        private static bool _wageTried, _wageHooked;
        private static readonly HashSet<Type> _netHooked = new HashSet<Type>(), _regHooked = new HashSet<Type>();   // klasy z zalatana implementacja
        private static object _netTriedFor, _regTriedFor;  // model tej kampanii, dla ktorego juz sprawdzalismy
        private static bool _regReady;

        internal static void Reset()
        {
            _clan = null; _recs.Clear(); _haveNet = false; _netModel = null; _netDecl = null; _debtBefore = 0;
            _paidToday.Clear(); _castlePay.Clear(); _errLogged = false;
            _held.Clear(); _court.Clear(); _regModel = null; _regDecl = null; _dCourtIn = _dCourtKept = _dCourtOut = _dCourtTrim = _dCourtDel = 0;
            _netTriedFor = null; _regTriedFor = null; _regReady = false;   // nowa kampania = nowe obiekty modeli (latki zostaja w procesie)
            ClearDay();
            ZeroLast();                                     // paczka 169
        }

        private static void ClearDay()
        {
            _dLordAcc = _dLordTaken = _dGarAcc = _dGarTaken = _dToPurse = _dPlayer = _dToTowns = _dToCastles = 0;
            _dUndead = _dNoTown = _dOff = _dOther = _dCut = _dBlindGold = _dDebtCut = 0;
            _dGarToPurse = 0; _dGarToPurseN = 0;
            _dGarHome = 0; _dGarHomeN = 0;
            _dZ8Pay = _dZ8Dues = _dZ8Refund = 0; _dZ8N = 0;
            _dLordN = _dGarN = _dToPurseN = _dToTownsN = _dToCastlesN = _dCutClans = _dBlind = _dDupes = _stumbles = _dDebtClans = 0;
            _dShielded = 0; _dShieldTicks = 0;
            _dToLordGold = _dFromLordGold = _dDupLordGold = _dOutLordGold = 0; _dToLordN = _dFromLordN = _dDupLordN = _dOutLordN = 0;   // 169b
            _dNonPosLordGold = 0; _dNonPosLordN = 0;
        }

        /// <summary>Czy ktorakolwiek czesc mechanizmu jest wlaczona (bez tego latki tylko wracaja).</summary>
        private static bool Watching
        {
            get { var s = Settings.Current; return s != null && (s.SoldierPayToPurse || s.GarrisonPayToCoffers || s.CrownWageRefundEnabled); }
        }

        private static bool Live { get { var c = Campaign.Current; return c != null && c.GameStarted; } }

        /// <summary>Wyjatek przy jednym rodzie albo partii: pierwszy do pliku, kolejne liczone (nigdy nie gasimy mechanizmu).</summary>
        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_errLogged) return;
            _errLogged = true;
            Log.Error(where, e);
        }

        // ------------------------------------------------------------ rachunek (czyste funkcje)
        /// <summary>O ile saldo rodu nie zmiescilo sie w kiesie glowy (tyle wydatkow nie zostalo naprawde zaplacone).</summary>
        internal static long Shortfall(int goldBefore, int net)
        {
            long after = (long)goldBefore + net;
            return after < 0 ? -after : 0L;
        }

        /// <summary>Czesc kwoty jednej partii, ktora naprawde zeszla z kies, gdy rodowi zabraklo `shortfall` na wszystkie `owed`.</summary>
        internal static int Share(int paid, long owed, long shortfall)
        {
            if (paid <= 0 || owed <= 0 || shortfall >= owed) return 0;
            if (shortfall <= 0) return paid;
            return (int)((long)paid * (owed - shortfall) / owed);   // w dol - suma nigdy nie przekroczy tego, co zeszlo
        }

        // ------------------------------------------------------------ latki
        /// <summary>ClanVariablesCampaignBehavior.DailyTickClan - poczatek dziennego rozliczenia rodu.</summary>
        public static void ClanTickPrefix(Clan __0)
        {
            try
            {
                _clan = null; _recs.Clear(); _haveNet = false;
                if (__0 == null || __0.IsBanditFaction || __0.Leader == null || !Watching || !Live) return;
                EnsureHooks();
                _debtBefore = __0.DebtToKingdom;
                _clan = __0;
            }
            catch (Exception e) { _clan = null; Stumble("SoldierPay.ClanTickPrefix", e); }
        }

        /// <summary>
        /// Latki na model finansow, zakladane w kampanii (Game.Current i Campaign.Current istnieja): postfiks na CalculatePartyWage
        /// (raz na proces) i postfiks na implementacji CalculateClanGoldChange, ktorej uzywa czynny model (raz na klase).
        /// Wolane z prefiksu DailyTickClan - przed cialem gry, wiec juz pierwsze rozliczenie rodu jest liczone.
        /// </summary>
        private static void EnsureHooks()
        {
            var h = _harmony;
            if (h == null) return;
            var active = Campaign.Current.Models.ClanFinanceModel;
            if (_wageTried && (active == null || ReferenceEquals(active, _netTriedFor))) return;
            if (!_wageTried)
            {
                _wageTried = true;
                try
                {
                    var m = AccessTools.Method(typeof(DefaultClanFinanceModel), "CalculatePartyWage");
                    if (m != null) { h.Patch(m, postfix: new HarmonyMethod(typeof(SoldierPay), nameof(WagePostfix))); _wageHooked = true; }
                }
                catch (Exception e) { Log.Error("SoldierPay.EnsureHooks(CalculatePartyWage)", e); }
            }
            if (active == null || ReferenceEquals(active, _netTriedFor)) return;
            _netTriedFor = active;
            string where = "BRAK (rod z pusta kiesa nie dostanie przekazania)";
            try
            {
                var decl = DeclOf(active.GetType());
                if (decl != null && _netHooked.Contains(decl)) where = decl.FullName + " (wpiete wczesniej)";
                else if (decl != null)
                {
                    var m = decl.GetMethod("CalculateClanGoldChange", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, null, NetArgs, null);
                    if (m != null && !m.IsAbstract)
                    {
                        h.Patch(m, postfix: new HarmonyMethod(typeof(SoldierPay), nameof(NetPostfix)) { priority = Priority.Last });
                        _netHooked.Add(decl);
                        where = decl.FullName + " (wpiete teraz)";
                    }
                }
            }
            catch (Exception e) { Log.Error("SoldierPay.EnsureHooks(saldo rodu)", e); }
            Log.Info("SoldierPay: latki modelu finansow (zakladane w kampanii) - zold partii " + (_wageHooked ? "wpiete" : "BRAK - nic nie bedzie przekazywane")
                     + "; saldo rodu z " + where + ", model czynny " + active.GetType().FullName + ".");
        }

        /// <summary>DefaultClanFinanceModel.CalculatePartyWage(partia, budzet, applyWithdrawals) - zaraz po niej gra i BK zdejmuja wynik z kiesy.</summary>
        public static void WagePostfix(MobileParty __0, bool __2, int __result)
        {
            try
            {
                var clan = _clan;
                if (!__2 || __0 == null) return;
                if (__result <= 0)
                {
                    // 169b (tylko log): naliczenie zerowe albo UJEMNE - gra (AddPartyExpense) daje partii z wodzem i kiesa < 500 budzet
                    // min(kiesa rodu + saldo, 250), gdy rod ma < 4000; przy ujemnej sumie wynik < 0 (i gra dopisuje partii zloto).
                    // Ksiega pieniadza liczy kazdy wynik, my rekord tylko dla > 0 - do uzgodnienia w linii "Obieg"
                    try { if (Live && LedgerLordParty(__0)) { _dNonPosLordN++; _dNonPosLordGold += __result; } } catch { }
                    return;
                }
                if (clan == null)
                {
                    // 169b (tylko log): wyplata poza naszym oknem rozliczenia (rod bez glowy, banda, mechanizm wylaczony) - ksiega pieniadza ja liczy
                    try { if (Live && LedgerLordParty(__0)) { _dOutLordN++; _dOutLordGold += __result; } } catch { }
                    return;
                }
                for (int i = 0; i < _recs.Count; i++)
                    if (ReferenceEquals(_recs[i].Party, __0))
                    {
                        _dDupes++;                                          // druga wyplata tej samej partii w jednym rozliczeniu - nie liczymy
                        try { if (LedgerLordParty(__0)) { _dDupLordN++; _dDupLordGold += __result; } } catch { }   // 169b (tylko log)
                        return;
                    }
                var r = new Rec { Party = __0, Wage = __result };
                try { r.LedgerLord = LedgerLordParty(__0); } catch { }      // 169b (tylko log): rodzaj partii w chwili naliczenia
                var head = clan.Leader;
                if (head != null && ReferenceEquals(head.PartyBelongedTo, __0))
                {
                    r.Balance = true; r.Paid = __result;                // partia glowy rodu: kwota wchodzi do salda rodu
                }
                else if (__0.IsLordParty)
                {
                    var payer = __0.LeaderHero ?? (__0.ActualClan != null ? __0.ActualClan.Leader : null);
                    int gold = payer != null ? payer.Gold : 0;
                    r.Payer = payer; r.PayerGold = gold;
                    r.Paid = Math.Min(__result, Math.Max(0, gold));     // kiesa nie schodzi ponizej zera
                }
                else
                {
                    r.PayerGold = __0.PartyTradeGold;                   // garnizon, karawana: kiesa partii
                    r.Paid = Math.Min(__result, Math.Max(0, r.PayerGold));
                }
                _recs.Add(r);
            }
            catch (Exception e) { Stumble("SoldierPay.WagePostfix", e); }
        }

        /// <summary>CalculateClanGoldChange czynnego modelu finansow (najbardziej zewnetrzna implementacja) - gra zaraz dopisze wynik glowie rodu.</summary>
        public static void NetPostfix(object __instance, MethodBase __originalMethod, Clan __0, bool __2, ExplainedNumber __result)
        {
            try
            {
                var clan = _clan;
                if (clan == null || !__2 || !ReferenceEquals(__0, clan)) return;
                var active = Campaign.Current.Models.ClanFinanceModel;
                if (!ReferenceEquals(__instance, active)) return;                  // model opakowany przez inny - liczy zewnetrzny
                if (!ReferenceEquals(active, _netModel)) { _netModel = active; _netDecl = DeclOf(active.GetType()); }
                if (__originalMethod == null || __originalMethod.DeclaringType != _netDecl) return;   // metoda bazowa wolana przez nadpisanie
                var head = clan.Leader;
                if (head == null) return;
                _goldMid = head.Gold;
                _net = TaleWorlds.Library.MathF.Round(__result.ResultNumber);      // tak samo zaokragla DailyTickClan
                _haveNet = true;
            }
            catch (Exception e) { Stumble("SoldierPay.NetPostfix", e); }
        }

        private static Type DeclOf(Type t)
        {
            try
            {
                var m = t.GetMethod("CalculateClanGoldChange", BindingFlags.Public | BindingFlags.Instance, null, NetArgs, null);
                return m != null ? m.DeclaringType : null;
            }
            catch { return null; }
        }

        /// <summary>ClanVariablesCampaignBehavior.DailyTickClan - koniec rozliczenia rodu: saldo dopisane, kiesy po wyplatach.</summary>
        public static void ClanTickPostfix()
        {
            var clan = _clan;
            _clan = null;
            if (clan == null) return;
            try { Settle(clan); }
            catch (Exception e) { Stumble("SoldierPay.Settle", e); }
            finally { _recs.Clear(); _haveNet = false; }
        }

        // ------------------------------------------------------------ rozdzial zaplaconego zoldu
        private static void Settle(Clan clan)
        {
            if (_recs.Count == 0) return;
            var s = Settings.Current;
            if (s == null) return;
            long owed = 0;
            for (int i = 0; i < _recs.Count; i++) owed += _recs[i].Paid;
            var head = clan.Leader;
            long shortfall = 0;
            if (owed > 0)
            {
                bool blind = false;
                if (_haveNet) shortfall = Shortfall(_goldMid, _net);
                else if (head == null || head.Gold <= 0)
                {
                    // saldo nieznane (model finansow spoza naszych latek), a kiesa glowy pusta: nie wiemy, ile naprawde zaplacono - nic nie przekazujemy
                    _dBlind++; _dBlindGold += owed;
                    shortfall = owed; blind = true;
                }
                if (!blind)
                {
                    // brak ukryty w dlugu wobec korony: gra dopisala go do Clan.DebtToKingdom i wyrownala saldo do kiesy (opis w naglowku klasy)
                    long hidden = Math.Max(0L, (long)clan.DebtToKingdom - _debtBefore);
                    if (hidden > 0)
                    {
                        if (shortfall < owed) { _dDebtClans++; _dDebtCut += Math.Min(hidden, owed - shortfall); }
                        shortfall += hidden;
                    }
                    if (shortfall > 0) { _dCutClans++; _dCut += Math.Min(shortfall, owed); }
                }
                if (shortfall > 0) ClanIncomeBook.NoteWageCut(clan, Math.Min(shortfall, owed), blind);   // 169c: miara bankructwa K39 (tylko licznik, wlasny try); blind - saldo nieznane, liczone osobno
            }
            // takze gdy nic nie zeszlo z kies: Route dolicza zold naliczony (linia "Zold:" ma sie zgadzac z licznikiem ksiegi pieniadza)
            for (int i = 0; i < _recs.Count; i++)
            {
                var r = _recs[i];
                try { Route(clan, r, Share(r.Paid, owed, shortfall), s); }
                catch (Exception e) { Stumble("SoldierPay.Route", e); }
            }
        }

        private static void Route(Clan clan, Rec r, int amt, Settings s)
        {
            var mp = r.Party;
            // 169b (tylko log): partia zmienila rodzaj miedzy naliczeniem a rozdzialem - linia "Zold:" i ksiega pieniadza licza ja w roznych pozycjach
            try
            {
                bool spLord = !mp.IsGarrison && mp.IsLordParty;
                if (spLord && !r.LedgerLord) { _dToLordN++; _dToLordGold += r.Wage; }
                else if (!spLord && r.LedgerLord) { _dFromLordN++; _dFromLordGold += r.Wage; }
            }
            catch { }
            if (mp.IsGarrison)
            {
                _dGarAcc += r.Wage; _dGarTaken += amt; _dGarN++;
                if (amt <= 0) return;
                var st = mp.CurrentSettlement ?? mp.HomeSettlement;
                var town = st != null ? st.Town : null;
                bool toCoffers = s.GarrisonPayToCoffers && town != null;
                // K1 (A2, Jeff 09.10 "za swoje sami sie zbroja z lupow i zoldu"): MenGearSavePercent zaplaconego zoldu do sakiewki zalogi
                // (braki i lepszy sprzet z targu swojej osady), najwyzej do MenGearSaveDays dni zoldu - ponad limit caly zold do kasy osady.
                // Pieniadze i tak koncza w tej samej kasie, tylko pozniej i jako zakup sprzetu (decyzja z 05.10 "zold garnizonu do kasy
                // jego osady" w mocy co do miejsca). GarrisonShare tylko czyta (sakiewke i ustawienia) - liczone przed zwrotem korony
                int toPurse = toCoffers ? GarrisonShare(mp, amt, r.Wage, s) : 0, coffers = amt - toPurse;
                // 114-p / B-2 (Z8, 2.0b): zold zalogi ZAMKU, ktory wraca panu zaworem, nie jest podstawa zwrotu korony. B-2: "wraca" liczone przy
                // zwrocie (TakePaid) z tego, co pan naprawde dostal z zaworu swoich zamkow, najwyzej ten zold - nie przewidywane tu przy wplacie
                // (114-p HomePart odliczal cala czesc ponad zapasem kasy, a kupcy podzamcza wydaja ja na towar, zanim zawor ja wezmie)
                // 165 (regula 3, D-5, S7): przy koronie z biezacych wplywow zaloga nie jest podstawa zwrotu - to koszt pana, wraca mu zaworem;
                // CrownWageRefundGarrisons dziala tylko przy wylaczonym 165 (wtedy jak dotad)
                if (s.CrownWageRefundGarrisons && !CrownIncome.On)
                {
                    AddPaid(clan.Leader, amt, s);                       // kiese zalogi wyrownuje rod z salda - placi glowa
                    if (toCoffers && coffers > 0 && s.CrownWageRefundEnabled && clan.Leader != null && CastlePurse.ComesHome(st))
                    {
                        long c0; _castlePay.TryGetValue(clan, out c0); _castlePay[clan] = c0 + coffers;
                    }
                }
                if (!s.GarrisonPayToCoffers) { _dOff += amt; return; }
                if (town == null) { _dNoTown += amt; return; }
                if (toPurse > 0)
                {
                    MenPurse.NoteWage(toPurse);                         // licznik linii "Sakiewka ludzi:"
                    MenPurse.Add(mp, toPurse);
                    MoneyLedger.NoteWageRouted(true, toPurse);
                    _dGarToPurse += toPurse; _dGarToPurseN++;
                    MenUpgrade.NoteGarrisonWage(toPurse);
                }
                if (coffers > 0)
                {
                    town.ChangeGold(coffers);                           // zaloga wydaje zold na miejscu - kasa jej miasta albo zamku
                    ClanIncomeBook.NoteOwnPaid(st, clan, coffers);      // 169c: "wlasne" D stalego - zold zalogi rodu w kasie jego osady (tylko licznik)
                    if (st.IsTown) { _dToTowns += coffers; _dToTownsN++; Hold(st, coffers); } else { _dToCastles += coffers; _dToCastlesN++; }
                    MoneyLedger.Note(MoneyLedger.NWage, st, coffers);   // ksiega przeplywow osad (tylko licznik)
                    MoneyLedger.NoteWageRouted(false, coffers);
                }
                // 150: zaloga na zoldzie zdziera odziez - miasto z polki bez zlota, zamek placi miastu. K1: `paid` sluzy tu tylko jako czesc
                // zaplaconej doby (zuzycie odziezy), nie jako pieniadze - dostaje caly zaplacony zold (inaczej zuzycie odziezy spadloby o polowe)
                ArmyClothing.OnGarrisonPaid(mp, st, amt, r.Wage);
            }
            else if (mp.IsLordParty)
            {
                _dLordAcc += r.Wage; _dLordTaken += amt; _dLordN++;
                if (amt <= 0) return;
                // kto poniosl koszt: wodz, ktoremu rod nie wyrownal kiesy (rycerz z lennem u BK), inaczej glowa rodu
                var payer = clan.Leader;
                if (!r.Balance && r.Payer != null && r.Payer != payer && r.Payer.Gold < r.PayerGold) payer = r.Payer;
                AddPaid(payer, amt, s);
                if (!s.SoldierPayToPurse || !MenPurse.On) { _dOff += amt; return; }
                if (Undead.Party(mp)) { _dUndead += amt; return; }      // trup zoldu nie wyda - bez sakiewki (jak dotad)
                MenPurse.NoteWage(amt);                                 // licznik linii "Sakiewka ludzi:" (najpierw - zamyka poprzednia dobe przed wplata)
                MenPurse.Add(mp, amt);                                  // ludzie wydadza w miescie: naprawy, braki, zycie
                _dToPurse += amt; _dToPurseN++;
                if (mp.IsMainParty) _dPlayer += amt;
                MoneyLedger.NoteWageRouted(true, amt);
            }
            else _dOther += amt;                                        // karawany i inne partie: bez zmian
        }

        /// <summary>K1 (A2): czesc zaplaconego zoldu zalogi do jej sakiewki - MenGearSavePercent, do limitu MenGearSaveDays dni zoldu; trup nie wyda.</summary>
        private static int GarrisonShare(MobileParty mp, int amt, int wage, Settings s)
        {
            try
            {
                if (amt <= 0 || !MenUpgrade.GarrisonPurseOn || Undead.Party(mp)) return 0;
                long want = (long)amt * Math.Max(0, Math.Min(100, s.MenGearSavePercent)) / 100;
                long room = (long)Math.Max(0, s.MenGearSaveDays) * Math.Max(0, wage) - MenPurse.Get(mp);
                return (int)Math.Max(0L, Math.Min(want, room));
            }
            catch { return 0; }
        }

        private static void AddPaid(Hero payer, int amt, Settings s)
        {
            if (payer == null || amt <= 0 || !s.CrownWageRefundEnabled) return;
            int v; _paidToday.TryGetValue(payer, out v);
            _paidToday[payer] = v + amt;
        }

        /// <summary>
        /// Zold partii i garnizonow naprawde zaplacony od poprzedniego rozliczenia korony, wedlug platnika; czysci licznik. B-2 (Z8): glowie rodu
        /// odejmujemy to, co dzis wrocilo jej z zaworu zamkow rodu (CastlePurse.LordDuesToday - CastlePurse.Daily biegnie w tym samym ticku przed
        /// zwrotem korony), najwyzej zold zalog tych zamkow od poprzedniego rozliczenia (_castlePay) - ta czesc nie jest podstawa zwrotu.
        /// Ciecie to GORNA granica: LordDuesToday obejmuje tez zawor z innych wplat do kasy zamku (np. place budow BuildFunding), a nie
        /// tylko z zoldu zalogi - dlatego najwyzej _castlePay. B-4: ciecie i liczniki tylko dla rodow, ktorym korona zwraca zold (krolestwo
        /// w wojnie, nie najemnik - te same warunki co KingdomTreasury.WageRefund); rody w pokoju, bez krolestwa i najemnicy zwrotu nie maja,
        /// wiec ich zold nie zawyza licznika "bez zwrotu korony". Licznik Z8 (linia "Zold:"): z 1 zl zoldu zalog wplaconego do kas wlasnych
        /// zamkow takich rodow wraca nalezny zwrot korony od reszty zoldu + zawor panom - prog < 1 (sprawdz_logi, Z8 2.0b).
        /// </summary>
        internal static List<KeyValuePair<Hero, int>> TakePaid()
        {
            var list = new List<KeyValuePair<Hero, int>>(_paidToday.Count);
            var s = Settings.Current;
            double pct = s != null ? Math.Max(0f, Math.Min(100f, s.CrownWageRefundPercent)) / 100.0 : 0.0;
            foreach (var kv in _paidToday)
            {
                int v = kv.Value;
                try
                {
                    var h = kv.Key;
                    var c = h != null ? h.Clan : null;
                    long pay;
                    if (c != null && h == c.Leader && v > 0 && _castlePay.TryGetValue(c, out pay) && pay > 0 && RefundedClan(c))
                    {
                        int dues; CastlePurse.LordDuesToday.TryGetValue(c, out dues);
                        long cut = Math.Min(Math.Min(pay, (long)Math.Max(0, dues)), v);
                        if (cut > 0) { v -= (int)cut; _dGarHome += cut; _dGarHomeN++; }
                        _dZ8Pay += pay; _dZ8Dues += Math.Max(0, dues); _dZ8Refund += (long)((pay - Math.Max(0L, cut)) * pct); _dZ8N++;
                    }
                }
                catch (Exception e) { Stumble("SoldierPay.TakePaid", e); }
                list.Add(new KeyValuePair<Hero, int>(kv.Key, v));
            }
            _paidToday.Clear(); _castlePay.Clear();
            return list;
        }

        /// <summary>B-4: rod, ktoremu korona zwraca dzis zold - krolestwo w wojnie, nie najemnik (warunki KingdomTreasury.WageRefund).</summary>
        private static bool RefundedClan(Clan c)
        {
            if (c == null || c.IsEliminated || c.IsUnderMercenaryService) return false;
            var k = c.Kingdom;
            return k != null && !k.IsEliminated && KingdomTreasury.AtWar(k);
        }

        // ------------------------------------------------------------ zabezpieczenie: tarcza zoldu w kasie miasta (TownWageShield, domyslnie WLACZONA - decyzja Jeffa 06.10; bez niej regulator kas kasuje ok. 81% zoldu wplaconego miastom)
        // Regulator kasy gry (vanilla DefaultSettlementEconomyModel.GetTownGoldChange: co dobe 0.25 x (cel - kasa), cel = 10 000 +
        // 12 x dobrobyt) kasuje cwierc kazdej nadwyzki dziennie, a zawor renty pana (PopulationLaw: 7% kasy ponad prog, z pulapem
        // renty naleznej) jest wolniejszy - z zoldu wplaconego do kasy miasta do pana wraca ok. 17%, reszte zjada regulator.
        // Tarcza: zold wplacony do kasy MIASTA (zaloga, wydatki ludzi "na zycie") dostaje znacznik. Regulator nie kasuje czesci
        // nadwyzki objetej znacznikiem (nigdy niczego nie dosypuje z jego powodu). Znacznik wygasa co dobe w tempie, w jakim zawor
        // i danina wojenna wyciagaja zloto z kasy (7% + 1% w wojnie): w miescie bez pulapu renty zold wraca wtedy do pana i korony,
        // a gdzie pulap wiaze - po ok. dwoch tygodniach znacznik wygasa i nadwyzke bierze regulator jak dotad (nic nie zostaje
        // uwiezione). Znacznik nie jest zlotem - to tylko liczba; kasy zamkow (bez renty) tarcza nie obejmuje.
        private const float RegulatorRate = 0.25f;
        private static readonly Dictionary<string, float> _held = new Dictionary<string, float>();   // id miasta -> zold w kasie "w drodze do pana"
        private static object _regModel;
        private static Type _regDecl;
        private static long _dShielded;
        private static int _dShieldTicks;

        private static bool ShieldOn { get { var s = Settings.Current; return s != null && s.TownWageShield; } }

        // ------------------------------------------------------------ 162m: tarcza dworu (HouseholdShield) - znacznik wplat dworu do kasy MIASTA siedziby
        // Inaczej niz znacznik zoldu: NIE wygasa z czasem. Schodzi tylko o czesc zlota, ktora zawor renty pana (PopulationLaw) i danina wojenna
        // (KingdomTreasury.Levies) naprawde wyciagnely dzis z tej kasy (proporcjonalnie: znacznik / kasa ponad prog), i nigdy nie jest wiekszy niz
        // nadwyzka kasy ponad cel regulatora (przyciecie = miasto wydalo to zloto na towar - nie skasowane). Regulator nie kasuje czesci nadwyzki pod
        // znacznikiem (nic nie dosypuje). Bez tego regulator gry zjadalby ok. 83% wplaty dworu (projekt 166, R17).
        private static readonly Dictionary<string, float> _court = new Dictionary<string, float>();
        private static long _dCourtIn, _dCourtKept, _dCourtOut, _dCourtTrim, _dCourtDel;
        private static bool CourtOn { get { var s = Settings.Current; return s != null && s.HouseholdShield && s.ClanBudgetEnabled; } }

        /// <summary>162m: wplata dworu do kasy miasta - znacznik tarczy dworu. Samo zloto wplaca wolajacy (ClanBudget).</summary>
        internal static void HoldCourt(Settlement st, int amount)
        {
            try
            {
                if (amount <= 0 || st == null || !st.IsTown || st.StringId == null || !CourtOn) return;
                if (!EnsureShieldHook()) return;
                float v; _court.TryGetValue(st.StringId, out v);
                _court[st.StringId] = v + amount;
                _dCourtIn += amount;
            }
            catch (Exception e) { Stumble("SoldierPay.HoldCourt", e); }
        }

        /// <summary>Dopisek do linii "Budzet rodow (166)": tarcza dworu dzis (liczniki zerowane po odczycie).</summary>
        internal static string CourtNote()
        {
            float sum = 0f; foreach (var v in _court.Values) sum += v;
            string t = "tarcza dworu " + (CourtOn ? "wlaczona: znacznik " + (long)sum + " w " + _court.Count + " miastach, regulator nie skasowal " + _dCourtKept
                                               + ", zeszlo z zaworem i danina " + _dCourtOut + ", miasto wydalo (znacznik przyciety do nadwyzki) " + _dCourtTrim
                                               + ", skasowane przez regulator " + _dCourtDel : "wylaczona");
            _dCourtIn = _dCourtKept = _dCourtOut = _dCourtTrim = _dCourtDel = 0;
            return t;
        }

        internal static string ExportCourt()
        {
            var parts = new List<string>();
            foreach (var kv in _court)
                if (kv.Value >= 1f) parts.Add(kv.Key + "=" + ((int)kv.Value).ToString(System.Globalization.CultureInfo.InvariantCulture));
            return string.Join(";", parts.ToArray());
        }

        internal static void ImportCourt(string data)
        {
            _court.Clear();
            if (string.IsNullOrEmpty(data)) return;
            foreach (var p in data.Split(';'))
            {
                var a = p.Split('='); int v;
                if (a.Length == 2 && a[0].Length > 0 && int.TryParse(a[1], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out v) && v > 0) _court[a[0]] = v;
            }
        }

        /// <summary>Zold wplacony do kasy miasta (zaloga albo wydatki ludzi z sakiewki) - dopisz znacznik tarczy. Samo zloto wplaca wolajacy.</summary>
        internal static void Hold(Settlement st, int amount)
        {
            try
            {
                if (amount <= 0 || st == null || !ShieldOn || !st.IsTown || st.StringId == null) return;
                if (!EnsureShieldHook()) return;                       // bez latki na regulatorze znacznik nic by nie chronil
                float v; _held.TryGetValue(st.StringId, out v);
                _held[st.StringId] = v + amount;
            }
            catch (Exception e) { Stumble("SoldierPay.Hold", e); }
        }

        /// <summary>
        /// Latka na regulator kasy - tylko przy wlaczonej tarczy i dopiero w kampanii: postfiks (First - przed licznikiem ksiegi
        /// pieniadza, zeby ksiega widziala wynik po tarczy) na implementacji GetTownGoldChange, ktorej uzywa czynny model.
        /// </summary>
        private static bool EnsureShieldHook()
        {
            var h = _harmony;
            if (h == null || Campaign.Current == null) return false;
            var active = Campaign.Current.Models.SettlementEconomyModel;
            if (active == null) return false;
            if (ReferenceEquals(active, _regTriedFor)) return _regReady;
            _regTriedFor = active; _regReady = false;
            string where = "BRAK - tarcza nie dziala";
            try
            {
                var decl = RegDeclOf(active.GetType());
                if (decl != null && _regHooked.Contains(decl)) { _regReady = true; where = decl.FullName + " (wpiete wczesniej)"; }
                else if (decl != null)
                {
                    var m = decl.GetMethod("GetTownGoldChange", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, null, new[] { typeof(Town) }, null);
                    if (m != null && !m.IsAbstract)
                    {
                        h.Patch(m, postfix: new HarmonyMethod(typeof(SoldierPay), nameof(RegulatorPostfix)) { priority = Priority.First });
                        _regHooked.Add(decl); _regReady = true;
                        where = decl.FullName + " (wpiete teraz)";
                    }
                }
            }
            catch (Exception e) { Log.Error("SoldierPay.EnsureShieldHook", e); }
            Log.Info("SoldierPay: tarcza zoldu w kasach miast (MCM Town Wage Shield) - regulator kasy z " + where + ", model czynny " + active.GetType().FullName + ".");
            return _regReady;
        }

        /// <summary>GetTownGoldChange czynnego modelu kasy osad: kasowanie nadwyzki pomniejszone o czesc objeta znacznikiem zoldu.</summary>
        public static void RegulatorPostfix(object __instance, MethodBase __originalMethod, Town __0, ref int __result)
        {
            if (_held.Count == 0 && _court.Count == 0) return;          // tarcze wylaczone albo nic nie wplacono - bez zmian
            try
            {
                if (__0 == null || !Live) return;
                bool wageOn = ShieldOn, courtOn = CourtOn;
                if (!wageOn && !courtOn) return;
                var active = Campaign.Current.Models.SettlementEconomyModel;
                if (!ReferenceEquals(__instance, active)) return;                  // model opakowany przez inny - liczy zewnetrzny
                if (!ReferenceEquals(active, _regModel)) { _regModel = active; _regDecl = RegDeclOf(active.GetType()); }
                if (__originalMethod == null || __originalMethod.DeclaringType != _regDecl) return;
                var st = __0.Settlement;
                if (st == null || !st.IsTown || st.StringId == null) return;
                float held = 0f, court = 0f;
                bool hasHeld = wageOn && _held.TryGetValue(st.StringId, out held) && held > 0f;
                bool hasCourt = courtOn && _court.TryGetValue(st.StringId, out court) && court > 0f;
                if (!hasHeld && !hasCourt) return;
                bool due = MoneyLedger.RegulatorDue(__0);                          // dzienny tick osady (nie pytania z ekranow) - tylko wtedy liczniki i przyciecie znacznika dworu
                if (__result >= 0)
                {
                    // kasa nie ponad celem: zold zastapil dosypke albo juz wyszedl - nie ma czego chronic; znacznik dworu schodzi do zera - miasto wydalo to zloto
                    if (hasHeld) _held.Remove(st.StringId);
                    if (hasCourt && due) { _dCourtTrim += (long)court; _court.Remove(st.StringId); }
                    return;
                }
                float surplus = -__result / RegulatorRate;                         // nadwyzka ponad cel, ktora regulator zdejmuje po cwierci dziennie
                // znaczniki nigdy ponad faktyczna nadwyzke - najpierw miejsce dla znacznika dworu (nie wygasa), reszta dla znacznika zoldu (wygasa)
                if (hasCourt && court > surplus) { if (due) { _dCourtTrim += (long)(court - surplus); _court[st.StringId] = surplus; } court = surplus; }
                if (hasHeld && held > surplus - court) { held = Math.Max(0f, surplus - court); if (held <= 0f) _held.Remove(st.StringId); else _held[st.StringId] = held; }
                int keepW = hasHeld ? (int)(held * RegulatorRate) : 0, keepC = hasCourt ? (int)(court * RegulatorRate) : 0;
                int keep = Math.Min(-__result, keepW + keepC);
                if (keep <= 0) return;
                __result += keep;                                                  // zostaje <= 0: tarcza niczego nie dosypuje
                if (due)
                {
                    int kc = Math.Min(keep, keepC);
                    if (keep - kc > 0) { _dShielded += keep - kc; _dShieldTicks++; }   // liczymy tylko dzienny tick osady, nie pytania z ekranow
                    _dCourtKept += kc;
                    // kontrola: regulator kasuje tylko czesc nadwyzki bez znacznikow; czesc dworu skasowana = 0 z budowy (licznik testu "dwor: skasowane przez regulator")
                    long removed = -__result, plain = (long)Math.Ceiling(Math.Max(0f, surplus - held - court) * RegulatorRate);
                    if (hasCourt && removed > plain + 1) _dCourtDel += removed - plain;
                }
            }
            catch (Exception e) { Stumble("SoldierPay.RegulatorPostfix", e); }
        }

        private static Type RegDeclOf(Type t)
        {
            try
            {
                var m = t.GetMethod("GetTownGoldChange", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(Town) }, null);
                return m != null ? m.DeclaringType : null;
            }
            catch { return null; }
        }

        /// <summary>Raz na dobe: znacznik wygasa w tempie zaworu renty (i daniny wojennej); przy wylaczonej tarczy znika caly.</summary>
        private static void DecayHeld()
        {
            DecayCourt();
            if (_held.Count == 0) return;
            var s = Settings.Current;
            if (s == null || !s.TownWageShield) { _held.Clear(); return; }
            EnsureShieldHook();                                         // znaczniki z zapisu gry: latka takze bez nowej wplaty
            float rent = Math.Max(0f, Math.Min(1f, s.TownRentShare));
            foreach (var id in new List<string>(_held.Keys))
            {
                float rate = rent;
                try
                {
                    var st = Settlement.Find(id);
                    var k = st != null && st.OwnerClan != null ? st.OwnerClan.Kingdom : null;
                    if (k != null) rate += KingdomTreasury.LaySubsidyTownRate(k);
                }
                catch { }
                float v = _held[id] * (1f - Math.Min(1f, rate));
                if (v < 1f) _held.Remove(id); else _held[id] = v;
            }
        }

        /// <summary>162m: znacznik dworu schodzi tylko o czesc zlota, ktora zawor renty i danina wojenna naprawde wyciagnely dzis z kasy miasta.</summary>
        private static void DecayCourt()
        {
            if (_court.Count == 0) return;
            if (!CourtOn) { _court.Clear(); return; }
            EnsureShieldHook();                                         // znaczniki z zapisu gry: latka takze bez nowej wplaty
            foreach (var id in new List<string>(_court.Keys))
            {
                try
                {
                    var st = Settlement.Find(id);
                    if (st == null || !st.IsTown) { _court.Remove(id); continue; }
                    long pay, avail; PopulationLaw.RentOf(st, out pay, out avail);
                    int levy; KingdomTreasury.TownLevyToday.TryGetValue(st, out levy);
                    float v = _court[id];
                    long outGold = Math.Max(0, pay) + Math.Max(0, levy);
                    if (outGold <= 0) continue;
                    // czesc wyciagnietego zlota przypadajaca na znacznik: znacznik / kasa ponad prog (avail - przed zaworem)
                    float part = (float)Math.Min((double)v, outGold * Math.Min(1.0, v / Math.Max(1.0, Math.Max((double)v, avail))));
                    _dCourtOut += (long)part;
                    v -= part;
                    if (v < 1f) _court.Remove(id); else _court[id] = v;
                }
                catch (Exception e) { Stumble("SoldierPay.DecayCourt", e); }
            }
        }

        /// <summary>169c (tylko odczyt): znacznik tarczy zoldu w kasie miasta (zl "w drodze do pana").</summary>
        internal static long HeldOf(Settlement st)
        {
            try { float v; return st != null && st.StringId != null && _held.TryGetValue(st.StringId, out v) ? (long)v : 0; } catch { return 0; }
        }

        internal static string ExportHeld()
        {
            var parts = new List<string>();
            foreach (var kv in _held)
                if (kv.Value >= 1f) parts.Add(kv.Key + "=" + ((int)kv.Value).ToString(System.Globalization.CultureInfo.InvariantCulture));
            return string.Join(";", parts.ToArray());
        }

        internal static void ImportHeld(string data)
        {
            _held.Clear();
            if (string.IsNullOrEmpty(data)) return;
            foreach (var p in data.Split(';'))
            {
                var a = p.Split('='); int v;
                if (a.Length == 2 && a[0].Length > 0 && int.TryParse(a[1], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out v) && v > 0) _held[a[0]] = v;
            }
        }

        // ------------------------------------------------------------ raz na dobe: linia "Zold:"
        internal static void Daily()
        {
            try
            {
                _paidToday.Clear(); _castlePay.Clear();                 // gdyby zwrot ze skarbca dzis nie biegl
                if (Campaign.Current == null) return;
                float heldSum = 0f; foreach (var v in _held.Values) heldSum += v;
                int heldTowns = _held.Count;
                if (Watching || _dLordAcc + _dGarAcc > 0)
                {
                    long total; int count, max, orphans; long orphanGold; string maxKey;
                    MenPurse.Stats(out total, out count, out max, out maxKey, out orphans, out orphanGold);
                    Log.Info("Zold: dzien " + (int)CampaignTime.Now.ToDays
                             + " | partie rodow: naliczony " + _dLordAcc + ", z kies zeszlo " + _dLordTaken + " (" + _dLordN + " partii) -> do sakiewek ludzi " + _dToPurse
                             + " (" + _dToPurseN + " partii, w tym ludzie gracza " + _dPlayer + ")"
                             + " | garnizony: naliczony " + _dGarAcc + ", z kies zeszlo " + _dGarTaken + " (" + _dGarN + " zalog) -> do kas miast " + _dToTowns + " (" + _dToTownsN
                             + "), do kas zamkow " + _dToCastles + " (" + _dToCastlesN + "), zalogi do sakiewek " + _dGarToPurse + " (" + _dGarToPurseN + ")"
                             + ", zold zalog zamkow, ktory wrocil panom zaworem - bez zwrotu korony (Z8, B-2; B-4: tylko rody w wojnie) " + _dGarHome + " (" + _dGarHomeN + " rodow)"
                             + ", Z8 rody w wojnie z zoldem zalog we wlasnych zamkach: zold do kas zamkow " + _dZ8Pay + ", zawor panom " + _dZ8Dues + ", nalezny zwrot korony od reszty " + _dZ8Refund + " (" + _dZ8N + " rodow)"
                             + " | nie przekazano: nieumarli " + _dUndead + ", zaloga bez osady " + _dNoTown + ", wylaczone w ustawieniach " + _dOff
                             + "; karawany i inne partie (bez zmian) " + _dOther
                             + " | przyciete, bo saldo rodu nie zmiescilo sie w kiesie glowy: " + _dCut + " w " + _dCutClans + " rodach (w tym brak zapisany przez gre jako dlug wobec korony: "
                             + _dDebtCut + " w " + _dDebtClans + " rodach); rody z pusta kiesa i nieznanym saldem (nic nie przekazano): "
                             + _dBlind + " (" + _dBlindGold + ")"
                             + " | sakiewki ludzi: razem " + total + " w " + count + " partiach, najwieksza " + max + (maxKey != null ? " (" + maxKey + ")" : "")
                             + ", po partiach, ktorych juz nie ma: " + orphanGold + " (" + orphans + ")"
                             + " | tarcza zoldu w kasach miast: " + (ShieldOn ? "wlaczona, znacznik " + (long)heldSum + " w " + heldTowns + " miastach, regulator nie skasowal dzis "
                                                                              + _dShielded + " (" + _dShieldTicks + " tickow miast)" : "wylaczona")
                             + (_dDupes + _stumbles > 0 ? " | potkniecia: powtorzone wyplaty " + _dDupes + ", wyjatki " + _stumbles : "") + ".");
                }
            }
            catch (Exception e) { Stumble("SoldierPay.Daily", e); }
            finally
            {
                // paczka 169: liczby doby dla linii "Obieg" - przed zerowaniem (bez zmian logiki)
                LastLordAcc = _dLordAcc; LastLordTaken = _dLordTaken; LastGarAcc = _dGarAcc; LastGarTaken = _dGarTaken;
                LastToPurse = _dToPurse + _dGarToPurse; LastToTowns = _dToTowns;   // K1: sakiewki ludzi - takze zalog
                LastToCastles = _dToCastles; LastOther = _dOther;
                LastLordN = _dLordN; LastGarN = _dGarN;                                                                    // 169b
                LastToLordN = _dToLordN; LastToLordGold = _dToLordGold; LastFromLordN = _dFromLordN; LastFromLordGold = _dFromLordGold;
                LastDupLordN = _dDupLordN; LastDupLordGold = _dDupLordGold; LastOutLordN = _dOutLordN; LastOutLordGold = _dOutLordGold;
                LastNonPosLordN = _dNonPosLordN; LastNonPosLordGold = _dNonPosLordGold;
                try { LastWatching = Watching; } catch { LastWatching = false; }
                ClearDay();
            }
            try { DecayHeld(); } catch (Exception e) { Stumble("SoldierPay.DecayHeld", e); }
        }

        // ------------------------------------------------------------ wpiecie
        /// <summary>Przy starcie gry: tylko para na DailyTickClan. Latki na modelach zaklada EnsureHooks / EnsureShieldHook w kampanii.</summary>
        internal static void ApplyAll(Harmony h)
        {
            string tick = "BRAK";
            _harmony = h;
            try
            {
                var m = AccessTools.Method(typeof(ClanVariablesCampaignBehavior), "DailyTickClan");
                if (m != null)
                {
                    h.Patch(m, prefix: new HarmonyMethod(typeof(SoldierPay), nameof(ClanTickPrefix)) { priority = Priority.First },
                               postfix: new HarmonyMethod(typeof(SoldierPay), nameof(ClanTickPostfix)) { priority = Priority.Last });
                    tick = "wpiete";
                }
            }
            catch (Exception e) { Log.Error("SoldierPay.ApplyAll(DailyTickClan)", e); }
            Log.Info("SoldierPay: zold do obiegu (partia -> sakiewka ludzi, garnizon -> kasa osady, zwrot ze skarbca w wojnie) - rozliczenie rodu " + tick
                     + "; latki na model finansow (zold partii, saldo rodu) dojda przy pierwszym rozliczeniu rodu w kampanii, a latka na regulator kasy"
                     + " tylko przy wlaczonej tarczy zoldu (MCM Town Wage Shield, domyslnie wylaczona).");
        }
    }
}
