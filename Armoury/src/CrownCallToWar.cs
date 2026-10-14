using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameComponents;

namespace Armoury
{
    /// <summary>
    /// 168 DODATEK - WEZWANIE SOJUSZNIKA DO WOJNY PLACI KORONA (decyzja Jeffa 10.10 "tak, korona"; zamyka czesc zrodla zaliczki gry OBIEG-1).
    /// Gra (AllianceCampaignBehavior.StartCallToWarAgreement): przy porozumieniu portfel wezwania krolestwa wzywajacego idzie w minus o cene (dlug jego rodow),
    /// a portfel wezwanego w plus o te sama cene od razu - rody wezwanego dostaja ja z niczego (AddIncomeFromCallToWarAgrements), a rody wzywajacego splacaja
    /// swoj udzial w nicosc (AddExpensesForCallToWarAgreements, przy braku - DebtToKingdom i portfel uznany w calosci). Cena = dzienna stawka x 42 doby sluzby.
    /// Przy CrownPaysCallToWar (i 165):
    ///  - rody wzywajacego nie placa nic (prefiks AddExpensesForCallToWarAgreements - AI i gracz);
    ///  - wezwany nie dostaje ceny z gory (postfiks StartCallToWarAgreement cofa plus portfela wezwanego); portfel wzywajacego zostaje dlugiem korony;
    ///  - codziennie w kolejnosci 165 (po kontraktach 185, przed zwrotem) skarbiec wzywajacego placi z reszty wplywow dnia czesc dnia = cena / 42 (do konca ceny):
    ///    skarbiec -> portfel wezwanego (gra rozdziela go na jego rody jak dotad) - prawdziwy platnik;
    ///  - gdy wplywow dnia nie starcza na czesc dnia - porozumienie konczy sie (EndCallToWarAgreement gry) i sojusznik wychodzi z tej wojny (MakePeaceAction,
    ///    gdy nie trzyma go w niej inne wezwanie); reszta ceny skreslona (nikt jej nie dostal);
    ///  - porozumienie skonczone przez gre (termin, pokoj) - reszta ceny skreslona.
    /// Wylaczone: porozumienia w toku wracaja do gry (portfel wezwanego dostaje niezaplacona reszte jak przy starcie gry). Zapis: "arm_ctw168".
    /// P3/W4 (bieg roczny 364 doby, TRIAZ-3 O1-7 A; klucz CallToWarBreakRestoresWallet): ZERWANE WEZWANIE NIE WRACA JAKO "DLUG SPRZED PACZKI". Przy
    /// zerwaniu z braku wplywow dnia wolamy EndCallToWarAgreement gry, a reszte ceny mial zdjac z portfela wzywajacego nasz postfiks (EndPostfix) - w tym
    /// jednym miejscu nie jest wolany (krotka metoda gry, 2 wywolania, wklejana przez kompilator w nasz Daily; w wywolaniach samej gry postfiks dziala).
    /// Wpis usuwalismy "na wypadek", portfel zostawal na minusie i nastepnej doby wykrywanie "dlugu sprzed paczki" bralo go za stary dlug: skarbiec placil
    /// te sama kwote w nicosc po 1/182 na dobe (5 zerwan w roku = 3.56 mln zaksiegowane, 2.0 mln zniszczone). Teraz: po wywolaniu gry, jesli wpis dalej
    /// jest na liscie - sami zdejmujemy reszte z portfela (ta sama arytmetyka co EndPostfix; warunek "wpis dalej na liscie" chroni przed podwojnym
    /// zdjeciem). Szukanie dlugu sprzed paczki - tylko raz: w pierwszej dobie po wczytaniu zapisu bez klucza 168 albo po ponownym wlaczeniu paczki;
    /// w kampanii zaczetej z paczka nigdy. Klucz wylaczony = stan sprzed (szukanie co dobe, bez wyrownania).
    /// PIENIADZE ROKU 2 (docs/pieniadze-rok-2/PROJEKT-WYBRANY.md 3.2-3.4), trzy osobne klucze: M2a CallToWarPaysForWar - rata tylko za dobe wojny wezwanego
    /// z celem, po 3 dobach laski i 7 dobach bez wojny porozumienie wygasa bez pokoju; M2b CallToWarAfterOwnWages - rata dopiero po rezerwie wlasnych zolnierzy
    /// (wczorajszy zwrot nalezny + wczorajszy brak W5), po rezerwie nie starcza - dzis bez raty; M2c CallToWarNoBreak - brak wplywow na rate nie zrywa porozumienia
    /// (czesc dnia przepada, bez MakePeaceAction). Gra i Diplomacy nie czytaja portfela wezwan przy decyzji o pokoju; ponizej -100 000 gra blokuje NOWE wezwania.
    /// </summary>
    internal static class CrownCallToWar
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private sealed class Agr { public string Calling = "", Called = "", Against = ""; public long Total, Daily, Left; public int Day; public int NoWar; }   // M2a: NoWar - doby bez wojny z rzedu (pamiec sesji, bez zapisu)

        // M2a (CallToWarPaysForWar): doby laski od porozumienia (spoznione wypowiedzenie, blokady ROT) i doby bez wojny z rzedu, po ktorych porozumienie wygasa
        private const int NoWarGraceDays = 3, NoWarEndDays = 7;
        private static long _dExpired, _dPaidNoWar; private static int _dNoWarN, _dExpiredN;
        // M2b (CallToWarAfterOwnWages): wstrzymane dzis po rezerwie wlasnych zolnierzy, korony z wstrzymaniem, kontrolka "rata ponad reszte minus rezerwe"
        private static long _dHeld, _dOverRes; private static int _dHeldN; private static readonly HashSet<string> _dHeldK = new HashSet<string>();
        // M2c (CallToWarNoBreak): czesci dnia, ktore przepadly z braku wplywow (zamiast zerwania)
        private static long _dLapsed; private static int _dLapsedN;
        private static readonly List<Agr> _agr = new List<Agr>();

        internal static long LastPaid, LastEnded, LastUnpaid, LastLegacyPaid; internal static int LastEndedN;
        private static long _dPaid, _dEnded, _dUnpaid, _dCancelled, _dLegacy, _dLegacyPaid, _dUndone; private static int _dEndedN, _dPaidN, _dPeaceN, _dNewN;
        private static int _stumbles, _importN = -1, _importBad;
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static bool _ending;   // nasze EndCallToWarAgreement - postfiks nie liczy drugi raz
        // P3/W4: szukac dlugu portfela sprzed paczki w najblizszej dobie (stary zapis bez klucza 168 albo paczka byla wylaczona); reszta ceny zdjeta z portfela
        // przez nas po zerwaniu (postfiks nie zadzialal) - doba
        private static bool _legacyScan;
        private static long _dRestored;

        internal static bool On { get { var s = Settings.Current; return s != null && s.CrownPaysCallToWar && CrownIncome.On; } }

        internal static void Reset() { _agr.Clear(); ZeroDay(); ZeroEvents(); _legacyScan = false; LastPaid = LastEnded = LastUnpaid = 0; LastEndedN = 0; _stumbles = 0; _err.Clear(); _importN = -1; _importBad = 0; _ending = false; }
        private static void ZeroDay() { _dPaid = _dEnded = _dUnpaid = _dLegacy = _dLegacyPaid = _dUndone = _dRestored = 0; _dEndedN = _dPaidN = _dPeaceN = 0; _dExpired = _dPaidNoWar = 0; _dNoWarN = _dExpiredN = 0; _dHeld = _dOverRes = 0; _dHeldN = 0; _dHeldK.Clear(); _dLapsed = 0; _dLapsedN = 0; }
        /// <summary>P3/W4: liczniki zdarzen miedzy dobami (nowe porozumienia - StartPostfix, skreslenia przez gre - EndPostfix) zerowane PO linii doby.
        /// Dotad zerowal je poczatek Daily, zanim linia je wypisala - "nowe dzis" i "skreslone: koniec porozumienia przez gre" pokazywaly zawsze 0.</summary>
        private static void ZeroEvents() { _dCancelled = 0; _dNewN = 0; }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_err.Add(where)) Log.Error("CrownCallToWar." + where, e); } catch { }
        }

        private static Kingdom KingdomById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var k in Kingdom.All) if (k != null && k.StringId == id) return k;
            return null;
        }

        private static float Days() { try { return Math.Max(1f, (float)Campaign.Current.Models.AllianceModel.MaxDurationOfWarParticipation.ToDays); } catch { return 42f; } }

        // ------------------------------------------------------------ latki gry (w kampanii - pulapka konstruktora statycznego modelu finansow)
        private static Harmony _harmony;
        private static bool _hooksTried;
        private static readonly List<string> _wired = new List<string>(), _missing = new List<string>();
        internal static void SetHarmony(Harmony h) { _harmony = h; }

        internal static void EnsureHooks()
        {
            if (_hooksTried || _harmony == null || Campaign.Current == null) return;
            _hooksTried = true;
            var fin = AccessTools.Method(typeof(DefaultClanFinanceModel), "AddExpensesForCallToWarAgreements", new[] { typeof(Clan), typeof(TaleWorlds.CampaignSystem.ExplainedNumber).MakeByRefType(), typeof(bool) });
            Wire("rody nie placa wezwania (AddExpensesForCallToWarAgreements)", fin, nameof(ClanSharePrefix), null);
            Wire("porozumienie bez ceny z gory (StartCallToWarAgreement)", AccessTools.Method(typeof(AllianceCampaignBehavior), "StartCallToWarAgreement",
                 new[] { typeof(Kingdom), typeof(Kingdom), typeof(Kingdom), typeof(int), typeof(bool) }), nameof(StartPrefix), nameof(StartPostfix));
            Wire("koniec porozumienia (EndCallToWarAgreement)", AccessTools.Method(typeof(AllianceCampaignBehavior), "EndCallToWarAgreement",
                 new[] { typeof(Kingdom), typeof(Kingdom), typeof(Kingdom) }), null, nameof(EndPostfix));
            Log.Info("Wezwania do wojny (168): latki - " + (_wired.Count > 0 ? string.Join(", ", _wired.ToArray()) : "-") + "; BRAK: " + (_missing.Count > 0 ? string.Join(", ", _missing.ToArray()) : "-") + ".");
        }

        private static void Wire(string label, MethodBase m, string pre, string post)
        {
            try
            {
                if (m == null) { _missing.Add(label); return; }
                _harmony.Patch(m, prefix: pre != null ? new HarmonyMethod(typeof(CrownCallToWar), pre) { priority = Priority.First } : null,
                                  postfix: post != null ? new HarmonyMethod(typeof(CrownCallToWar), post) { priority = Priority.Last } : null);
                _wired.Add(label);
            }
            catch (Exception e) { _missing.Add(label + " (blad: " + e.Message + ")"); }
        }

        /// <summary>Rody krolestwa wzywajacego nie placa ceny wezwania - placi skarbiec (Daily).</summary>
        public static bool ClanSharePrefix() { try { return !On; } catch { return true; } }

        public struct StartState { public int Called, Calling; }

        public static void StartPrefix(Kingdom __0, Kingdom __1, out StartState __state)
        {
            __state = new StartState { Called = int.MinValue };
            try { if (On && __0 != null && __1 != null) __state = new StartState { Called = __1.CallToWarWallet, Calling = __0.CallToWarWallet }; } catch { }
        }

        public static void StartPostfix(Kingdom __0, Kingdom __1, Kingdom __2, int __3, bool __4, StartState __state)
        {
            try
            {
                if (__state.Called == int.MinValue || __4 || __0 == null || __1 == null || __2 == null || __3 <= 0 || !On) return;   // gracz placacy z kiesy - prawdziwy platnik, jak w grze
                if (__1.CallToWarWallet - __state.Called != __3 || __state.Calling - __0.CallToWarWallet != __3) return;          // porozumienie nie powstalo
                __1.CallToWarWallet -= __3;   // wezwany nie dostaje ceny z niczego z gory - dostaje to, co skarbiec wzywajacego naprawde zaplaci
                long daily = (long)Math.Ceiling(__3 / Days());
                _agr.Add(new Agr { Calling = __0.StringId, Called = __1.StringId, Against = __2.StringId, Total = __3, Daily = Math.Max(1, daily), Left = __3, Day = (int)CampaignTime.Now.ToDays });
                _dNewN++;
                Log.Info("Wezwania do wojny (168): " + __0.Name + " wzywa " + __1.Name + " przeciw " + __2.Name + " - cena " + __3 + " zl placi skarbiec " + __0.Name
                         + " z wplywow dnia po " + Math.Max(1, daily) + " zl przez " + Days().ToString("0", Inv) + " dob; rody nie placa.");
            }
            catch (Exception e) { Stumble("StartPostfix", e); }
        }

        /// <summary>Porozumienie skonczone (gra: termin, pokoj; albo my - brak wplywow): reszta ceny skreslona z portfela wzywajacego.</summary>
        public static void EndPostfix(Kingdom __0, Kingdom __1, Kingdom __2)
        {
            try
            {
                if (__0 == null || __1 == null || __2 == null) return;
                for (int i = _agr.Count - 1; i >= 0; i--)
                {
                    var a = _agr[i];
                    if (a.Calling != __0.StringId || a.Called != __1.StringId || a.Against != __2.StringId) continue;
                    if (a.Left > 0) { __0.CallToWarWallet += (int)Math.Min(int.MaxValue, a.Left); if (!_ending) _dCancelled += a.Left; }   // recenzja D: bez przycinania do 0 (portfel moze miec plus wezwanego)
                    _agr.RemoveAt(i);
                    break;
                }
            }
            catch (Exception e) { Stumble("EndPostfix", e); }
        }

        // ------------------------------------------------------------ raz na dobe (krok korony 165: po kontraktach 185, przed zwrotem)
        internal static void Daily()
        {
            ZeroDay();
            if (Campaign.Current == null) return;
            if (!On)
            {
                // wylaczone: porozumienia w toku wracaja do gry - wezwany dostaje niezaplacona reszte (jak przy starcie gry), rody wzywajacego splacaja portfel
                foreach (var a in _agr) { try { var called = KingdomById(a.Called); if (called != null && a.Left > 0) { int ow0 = called.CallToWarWallet; called.CallToWarWallet += (int)Math.Min(int.MaxValue, a.Left); _dUndone += a.Left; Measure31.CtwOther(called, ow0); } } catch { } }   // 3.1 (31, E3/0): Measure31.CtwOther
                _agr.Clear();
                _legacyScan = true;   // P3/W4: po ponownym wlaczeniu - jedno szukanie dlugu portfela (rody wzywajacego splacaly go grze, reszta zostaje koronie)
                ZeroEvents();
                return;
            }
            try
            {
                var s0 = Settings.Current;
                bool fix = s0 != null && s0.CallToWarBreakRestoresWallet;   // P3/W4; wylaczony = stan sprzed
                // portfel wzywajacego ponizej dlugu z naszych porozumien = dlug rodow sprzed paczki: wezwany dostal go juz od gry z niczego, a rody splacalyby
                // go w nicosc (to rownowazylo zloto gry). Recenzja D: nie skreslany (zostaloby zloto z niczego) - placi skarbiec wzywajacego czesciami dnia w nicosc
                var owed = new Dictionary<string, long>();
                foreach (var a in _agr) { long v; owed.TryGetValue(a.Calling, out v); owed[a.Calling] = v + a.Left; }
                int today = (int)CampaignTime.Now.ToDays;
                // P3/W4: szukanie tylko raz - po wczytaniu zapisu bez klucza 168 albo po ponownym wlaczeniu paczki. W kampanii prowadzonej z paczka portfel
                // schodzi ponizej dlugu z naszych porozumien tylko przez nasz wlasny blad (zerwanie bez wyrownania) - to nie jest dlug sprzed paczki
                bool scan = !fix || _legacyScan;
                _legacyScan = false;
                if (scan)
                foreach (var k in Kingdom.All)
                {
                    if (k == null || k.IsEliminated || k.StringId == null) continue;
                    long o; owed.TryGetValue(k.StringId, out o);
                    if (k.CallToWarWallet < -o)
                    {
                        long x = -o - k.CallToWarWallet;
                        _agr.Add(new Agr { Calling = k.StringId, Called = "", Against = "", Total = x, Daily = Math.Max(1, (long)Math.Ceiling(x / Days())), Left = x, Day = today });
                        _dLegacy += x;
                    }
                }
                var beh = Campaign.Current.GetCampaignBehavior<AllianceCampaignBehavior>();
                bool m2b = s0 != null && s0.CallToWarAfterOwnWages;
                var resv = new Dictionary<Kingdom, long>();   // M2b: rezerwa wzywajacego - liczona zawsze (kontrolka przy kluczu wylaczonym)
                foreach (var a in _agr.ToArray())
                {
                    try
                    {
                        var calling = KingdomById(a.Calling);
                        if (a.Called.Length == 0)
                        {
                            // dlug portfela sprzed paczki: skarbiec placi czesc dnia z reszty wplywow dnia w nicosc (wezwany juz dostal); bez wplywow - czeka
                            if (calling == null || calling.IsEliminated) { _agr.Remove(a); continue; }
                            var kl = CrownIncome.DayOf(calling);
                            if (kl == null) continue;
                            long pay = Math.Min(Math.Min(a.Left, a.Daily), Math.Min(CrownIncome.LeftFor(calling), (long)Math.Max(0, calling.KingdomBudgetWallet)));
                            if (pay > 0)
                            {
                                int lw0 = calling.CallToWarWallet;   // 3.1 (31, E3/0)
                                calling.KingdomBudgetWallet -= (int)pay; calling.CallToWarWallet += (int)pay; a.Left -= pay;
                                CrownIncome.Spent(calling, pay); kl.CallToWar += pay; _dLegacyPaid += pay;
                                Measure31.CtwLegacy(calling, lw0, pay);   // 3.1 (31, E3/0): nazwane ujscie "dlug portfela sprzed paczki"
                            }
                            if (a.Left <= 0) _agr.Remove(a);
                            continue;
                        }
                        var called = KingdomById(a.Called); var against = KingdomById(a.Against);
                        if (calling == null || called == null || against == null || calling.IsEliminated || called.IsEliminated || against.IsEliminated)
                        {
                            if (calling != null && a.Left > 0) { int cw0 = calling.CallToWarWallet; calling.CallToWarWallet += (int)Math.Min(int.MaxValue, a.Left); _dCancelled += a.Left; Measure31.CtwOther(calling, cw0); }   // 3.1 (31, E3/0)
                            _agr.Remove(a);
                            continue;
                        }
                        long due = Math.Min(a.Left, a.Daily);
                        if (due <= 0) { _agr.Remove(a); continue; }
                        var kd = CrownIncome.DayOf(calling);
                        if (kd == null) continue;
                        // M2a (CallToWarPaysForWar): rata tylko za dobe wojny wezwanego z celem; po laskie liczone doby bez wojny z rzedu - po NoWarEndDays porozumienie
                        // konczy gra (EndCallToWarAgreement), reszta ceny skreslona tym samym wyrownaniem co zerwanie P3/W4, BEZ MakePeaceAction (nie ma wojny do konczenia)
                        bool atWar = called.IsAtWarWith(against);
                        if (atWar) a.NoWar = 0;
                        else if (s0 != null && s0.CallToWarPaysForWar)
                        {
                            _dNoWarN++;
                            if (today - a.Day >= NoWarGraceDays) a.NoWar++;
                            if (a.NoWar >= NoWarEndDays && beh != null) ExpireNoWar(beh, calling, called, against, a);
                            continue;   // dzis bez raty - zloto zostaje w skarbcu wzywajacego (zwrot, W5, renty tej doby)
                        }
                        long avail = CrownIncome.LeftFor(calling);
                        // M2b (CallToWarAfterOwnWages): najpierw wlasni zolnierze - rezerwa = wczorajszy zwrot nalezny + wczorajsza niepokryta potrzeba W5 (przed M1)
                        long res;
                        if (!resv.TryGetValue(calling, out res)) { res = CrownIncome.RefundDueYesterday(calling) + KingdomTreasury.NeedAidUncovYesterday(calling); resv[calling] = res; }
                        if (avail >= due && (!m2b || avail - res >= due))
                        {
                            if (!atWar) _dPaidNoWar += Math.Min(due, Math.Max(0L, (long)calling.KingdomBudgetWallet));   // M2a: kontrolka (klucz wylaczony - rata za dobe bez wojny jak dotad)
                            if (avail - res < due) _dOverRes += due;                                                      // M2b: kontrolka (klucz wylaczony - rata kosztem wlasnych zolnierzy)
                            Pay(calling, called, a, due, kd);
                            if (a.Left <= 0) _agr.Remove(a);
                            continue;
                        }
                        if (m2b && avail >= due)
                        {
                            // po rezerwie nie starcza - dzis bez raty, porozumienie trwa; czesc dnia nie przechodzi na jutro (reszte ceny skresla gra na koncu porozumienia)
                            _dHeld += due; _dHeldN++; _dHeldK.Add(calling.StringId ?? "?");
                            continue;
                        }
                        // M2c (CallToWarNoBreak): skarbca nie stac na czesc dnia - czesc dnia przepada, porozumienie trwa (bez zerwania i bez pokoju sojusznika)
                        if (s0 != null && s0.CallToWarNoBreak) { _dLapsed += due; _dLapsedN++; continue; }
                        // skarbca nie stac na czesc dnia - porozumienie konczy sie, sojusznik wychodzi z wojny
                        if (beh != null)
                        {
                            long left = a.Left;
                            _ending = true;
                            try { beh.EndCallToWarAgreement(calling, called, against); }
                            finally { _ending = false; }
                            // P3/W4: postfiks (EndPostfix) w tym wywolaniu nie jest wolany - wpis zostaje na liscie, a portfel wzywajacego na minusie o reszte
                            // ceny. Zdejmujemy ja sami (ta sama arytmetyka co EndPostfix); gdy postfiks jednak zadzialal, wpisu juz nie ma - nic nie robimy
                            if (fix && a.Left > 0 && _agr.Contains(a))
                            {
                                int rw0 = calling.CallToWarWallet;
                                calling.CallToWarWallet += (int)Math.Min(int.MaxValue, a.Left);
                                _dRestored += a.Left;
                                Measure31.CtwOther(calling, rw0);   // 3.1 (31, E3/0): zmiana dodatniej czesci pola bez platnika (skreslenie reszty), jak przy upadku krolestwa
                            }
                            _agr.Remove(a);   // na wypadek, gdyby postfiks nie znalazl wpisu
                            _dEnded += left; _dEndedN++;
                            bool peace = false;
                            Kingdom other;
                            if (called.IsAtWarWith(against) && !beh.IsAtWarByCallToWarAgreement(called, against, out other))
                            {
                                // E7/0 (224): para znaczona na czas Apply - linia "Wojna i pokoj (E7/0)" podaje przyczyne "wezwanie 168 zerwane" zamiast Default
                                try { WorldClock7.Mark168(called, against); MakePeaceAction.Apply(called, against); peace = true; _dPeaceN++; } catch (Exception e) { Stumble("MakePeace", e); }
                                finally { WorldClock7.Clear168(); }
                            }
                            Log.Info("Wezwania do wojny (168): skarbiec " + calling.Name + " nie ma z wplywow dnia " + due + " zl (zostalo " + avail + ") - porozumienie z " + called.Name
                                     + " przeciw " + against.Name + " zerwane, reszta ceny " + left + " skreslona" + (peace ? ", " + called.Name + " wychodzi z wojny (pokoj)" : ", wojna trwa z innych powodow") + ".");
                            if (Clan.PlayerClan != null && (Clan.PlayerClan.Kingdom == calling || Clan.PlayerClan.Kingdom == called))
                                Log.Player(calling.Name + " can no longer pay " + called.Name + " for its war against " + against.Name + ". The agreement is ended" + (peace ? " and " + called.Name + " makes peace." : "."), true);
                        }
                        else
                        {
                            // bez zachowania gry - zaplata proporcjonalna, niedoplata w liczniku (bez zlota z niczego, bez dlugu rodow)
                            if (avail > 0) Pay(calling, called, a, avail, kd);
                            _dUnpaid += due - Math.Max(0, avail);
                            if (a.Left <= 0) _agr.Remove(a);
                        }
                    }
                    catch (Exception e) { Stumble("Daily(porozumienie)", e); }
                }
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally
            {
                LastPaid = _dPaid; LastEnded = _dEnded; LastEndedN = _dEndedN; LastUnpaid = _dUnpaid; LastLegacyPaid = _dLegacyPaid;
                try { Line(); } catch (Exception e) { Stumble("Line", e); }
                ZeroEvents();   // P3/W4: liczniki zdarzen miedzy dobami - po linii
            }
        }

        private static void Pay(Kingdom calling, Kingdom called, Agr a, long x, CrownIncome.KDay kd)
        {
            int ix = (int)Math.Min(int.MaxValue, Math.Min(x, (long)Math.Max(0, calling.KingdomBudgetWallet)));
            if (ix <= 0) return;
            int wc0 = calling.CallToWarWallet, wd0 = called.CallToWarWallet;   // 3.1 (31, E3/0): pole netto portfeli przed wplata (ksiega)
            calling.KingdomBudgetWallet -= ix;    // skarbiec wzywajacego
            calling.CallToWarWallet += ix;        // dlug korony za wezwanie maleje
            called.CallToWarWallet += ix;         // portfel wezwanego - gra rozdziela go na jego rody (AddIncomeFromCallToWarAgrements)
            a.Left -= ix;
            CrownIncome.Spent(calling, ix);
            kd.CallToWar += ix;
            _dPaid += ix; _dPaidN++;
            Measure31.CtwPay(calling, wc0, called, wd0, ix);   // 3.1 (31, E3/0): wplata do ujemnego portfela - ujscie, reszta to przelew (portfel jest posiadaczem)
        }

        /// <summary>M2a: wezwany nie byl w wojnie z celem przez NoWarEndDays dob z rzedu (po lasce) - porozumienie konczy gra, reszta ceny skreslona z portfela
        /// wzywajacego (wyrownanie P3/W4 zawsze: postfiks EndPostfix w naszym wywolaniu nie jest wolany - patrz naglowek). Bez MakePeaceAction.</summary>
        private static void ExpireNoWar(AllianceCampaignBehavior beh, Kingdom calling, Kingdom called, Kingdom against, Agr a)
        {
            long left = a.Left;
            _ending = true;
            try { beh.EndCallToWarAgreement(calling, called, against); }
            finally { _ending = false; }
            if (a.Left > 0 && _agr.Contains(a))
            {
                int rw0 = calling.CallToWarWallet;
                calling.CallToWarWallet += (int)Math.Min(int.MaxValue, a.Left);   // bez _dRestored: ta pozycja linii (P3W4-2) liczy tylko zerwania z braku wplywow
                Measure31.CtwOther(calling, rw0);   // 3.1 (31, E3/0): skreslenie reszty ceny (bez platnika), jak przy zerwaniu
            }
            _agr.Remove(a);
            _dExpired += left; _dExpiredN++;
            Log.Info("Wezwania do wojny (168): " + called.Name + " nie walczy z " + against.Name + " od " + a.NoWar + " dob (M2a) - porozumienie z " + calling.Name
                     + " wygaslo bez pokoju, reszta ceny " + left + " skreslona.");
            if (Clan.PlayerClan != null && (Clan.PlayerClan.Kingdom == calling || Clan.PlayerClan.Kingdom == called))
                Log.Player(called.Name + " has not taken the field against " + against.Name + ". " + calling.Name + " stops paying for the war and the agreement is ended.", true);
        }

        private static void Line()
        {
            var s = Settings.Current;
            if (s == null || !s.LogEnabled || !On) return;
            if (_agr.Count == 0 && _dPaid + _dEnded + _dCancelled + _dLegacy + _dLegacyPaid + _dUndone + _dExpired + _dHeld + _dLapsed == 0 && _dNewN == 0 && _dExpiredN == 0 && _importN < 0) return;
            long left = 0; foreach (var a in _agr) left += a.Left;
            var sb = new StringBuilder(400);
            sb.Append("Wezwania do wojny (168): dzien ").Append((int)CampaignTime.Now.ToDays)
              .Append(" | porozumien w toku ").Append(_agr.Count).Append(" (nowe dzis ").Append(_dNewN).Append("), reszta cen do zaplaty przez korony ").Append(left)
              .Append(" | zaplacone dzis ze skarbcow wzywajacych do portfeli wezwanych ").Append(_dPaid).Append(" (").Append(_dPaidN).Append(" czesci dnia)")
              .Append(" | zerwane z braku wplywow dnia ").Append(_dEndedN).Append(" (reszta ceny skreslona ").Append(_dEnded).Append(", pokoj sojusznika ").Append(_dPeaceN).Append(")")
              .Append(", niedoplata (bez zachowania gry) ").Append(_dUnpaid)
              .Append(" | skreslone: koniec porozumienia przez gre ").Append(_dCancelled)
              // M2a (pieniadze roku 2): rata tylko za dobe wojny wezwanego z celem; kontrolka "rata w dobie bez wojny" (klucz wylaczony - jak dotad, wiecej niz 0)
              .Append(" | M2a za wojne (Call To War Pays For War ").Append(s.CallToWarPaysForWar ? "tak" : "nie").Append("): porozumien bez wojny wezwanego dzis ").Append(_dNoWarN)
              .Append(", wygasle bez wojny (").Append(NoWarGraceDays).Append(" doby laski + ").Append(NoWarEndDays).Append(" dob bez wojny) ").Append(_dExpiredN)
              .Append(" (reszta ceny skreslona ").Append(_dExpired).Append("), rata zaplacona w dobie bez wojny ").Append(_dPaidNoWar)
              // M2b (pieniadze roku 2): rata dopiero po rezerwie wlasnych zolnierzy wzywajacego; kontrolka "rata ponad reszte wplywow minus rezerwe" (klucz wylaczony - wiecej niz 0)
              .Append(" | M2b po wlasnych zolnierzach (Call To War After Own Wages ").Append(s.CallToWarAfterOwnWages ? "tak" : "nie").Append("): wstrzymane dzis ").Append(_dHeld)
              .Append(" zl (czesci ").Append(_dHeldN).Append(") u ").Append(_dHeldK.Count).Append(" koron (rezerwa: zwrot nalezny wczoraj + niepokryta potrzeba W5 wczoraj)")
              .Append(", rata ponad reszte wplywow minus rezerwe ").Append(_dOverRes)
              // M2c (pieniadze roku 2): bez zerwan - czesc dnia przepada (przy kluczu "tak" pozycja "zerwane z braku wplywow dnia" = 0)
              .Append(" | M2c bez zerwan (Call To War No Break ").Append(s.CallToWarNoBreak ? "tak" : "nie").Append("): przepadlo dzis z braku wplywow ").Append(_dLapsed)
              .Append(" zl (czesci ").Append(_dLapsedN).Append(")")
              .Append(" | dlug portfela sprzed paczki (wezwany dostal od gry z niczego): nowy ").Append(_dLegacy).Append(", splacone dzis ze skarbcow w nicosc (rownowazy) ").Append(_dLegacyPaid)
              .Append(" | P3/W4 po zerwaniu reszta ceny zdjeta z portfela wzywajacego przez nas (latka konca porozumienia nie zadzialala) ").Append(_dRestored)
              .Append(s.CallToWarBreakRestoresWallet ? "; dlug sprzed paczki szukany tylko raz po wczytaniu zapisu bez klucza 168 (Call To War Break Restores Wallet)"
                                                     : "; Call To War Break Restores Wallet wylaczone - bez wyrownania, dlug szukany co dobe jak dotad")
              .Append(" | rody nic nie placa (latki: ").Append(_wired.Count > 0 ? string.Join(", ", _wired.ToArray()) : "-").Append("; BRAK: ").Append(_missing.Count > 0 ? string.Join(", ", _missing.ToArray()) : "-").Append(')')
              .Append(_stumbles > 0 ? " | potkniecia " + _stumbles : "").Append('.');
            if (_importN >= 0) { sb.Append(" Wczytano z zapisu: porozumien ").Append(_importN).Append(" (bledne ").Append(_importBad).Append(")."); _importN = -1; }
            Log.Info(sb.ToString());
        }

        // ------------------------------------------------------------ zapis (SaveText, "arm_ctw168")
        private static readonly char[] Bad = { '|', ';', ',' };

        internal static string Export()
        {
            try
            {
                var sb = new StringBuilder(8 + _agr.Count * 64);
                sb.Append("v1|");
                bool first = true;
                foreach (var a in _agr)
                {
                    if (a.Calling.IndexOfAny(Bad) >= 0 || a.Called.IndexOfAny(Bad) >= 0 || a.Against.IndexOfAny(Bad) >= 0 || a.Left <= 0) continue;
                    if (!first) sb.Append(';'); first = false;
                    sb.Append(a.Calling).Append(',').Append(a.Called).Append(',').Append(a.Against).Append(',').Append(a.Total.ToString(Inv)).Append(',').Append(a.Daily.ToString(Inv))
                      .Append(',').Append(a.Left.ToString(Inv)).Append(',').Append(a.Day.ToString(Inv));
                }
                return sb.ToString();
            }
            catch (Exception e) { Stumble("Export", e); return ""; }
        }

        internal static void Import(string data)
        {
            _agr.Clear(); _importN = 0; _importBad = 0; _legacyScan = false;
            try
            {
                if (string.IsNullOrEmpty(data)) { _legacyScan = true; return; }   // P3/W4: zapis bez klucza 168 - jedno szukanie dlugu portfela sprzed paczki
                var f = data.Split('|');
                if (f.Length < 2 || f[0] != "v1") { _importBad++; return; }
                if (f[1].Length == 0) return;
                foreach (var rec in f[1].Split(';'))
                {
                    var x = rec.Split(','); long t, d, l; int day;
                    if (x.Length == 7 && long.TryParse(x[3], NumberStyles.Integer, Inv, out t) && long.TryParse(x[4], NumberStyles.Integer, Inv, out d) && long.TryParse(x[5], NumberStyles.Integer, Inv, out l)
                        && int.TryParse(x[6], NumberStyles.Integer, Inv, out day) && l > 0)
                    { _agr.Add(new Agr { Calling = x[0], Called = x[1], Against = x[2], Total = t, Daily = Math.Max(1, d), Left = l, Day = day }); _importN++; }
                    else _importBad++;
                }
            }
            catch (Exception e) { Stumble("Import", e); }
        }
    }
}
