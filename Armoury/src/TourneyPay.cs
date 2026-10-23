using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// TP (16.10, Jeff "kolejny blad miasto nie moglo zaplacic mi 1300 za stake w turnieju"; zrzut Seagard: "Seagard could pay only 1205 of the 1425 denars you
    /// won on your bet" i "Seagard could pay only 0 of the 1300 denars of prize money"; log 10-14-41, linia doby 109019: zloto TXP nalezne 1700, zaplacone 400,
    /// niedoplata 1300 w 1; wygrane turnieju nalezne 1425, zaplacone 1203, niedoplata 222 w 1). Ogloszona nagroda i wygrany zaklad musza byc zaplacone.
    /// Klucze: TourneyPayerChain (D1+D4), TourneyDebts (D2), TourneySeagardCatchUp (D3). Wszystkie wylaczone = PlayerFees8 jak przed ta zmiana.
    ///
    /// D1 LANCUCH PLATNIKOW (PayChain) - nagroda turnieju (zloto TXP: nagroda i premie meczu; cena nagrody zamiast przedmiotu), wygrana zakladu turniejowego
    /// (stawka gracza wraca zawsze z kasy, jak dotad), wygrana planszy w karczmie, nagroda areny treningowej. Kolejnosc, az kwota bedzie pokryta:
    ///   1. kasa osady-gospodarza: to, co liczy wolajacy (PlayerFees8.CapOf - miasto z nadwyzki Seal31.Spare, zamek CastlePurse.Spendable; zaklad: plus stawka gracza);
    ///   2. GOSPODARZ = rod, do ktorego nalezy osada (Settlement.OwnerClan) - z kiesy glowy rodu;
    ///   3. rod panujacy krolestwa osady (Kingdom.RulingClan), jesli to inny rod niz gospodarz - z kiesy glowy rodu.
    ///   Reszta: przy TourneyDebts - DLUG osady wobec gracza (D2), bez - przepada (licznik, komunikat).
    ///   OSADA GRACZA (recenzja 16.10): gdy osada nalezy do rodu gracza, gospodarzem jest sam gracz - lancuch konczy sie na kasie osady (bez rodu panujacego -
    ///   korona nie placi nagrody w miescie gracza), reszta przepada (gospodarz placilby sam sobie), dlug wobec siebie nie powstaje.
    /// Ile rod moze dac (ClanSpare): tylko rod AI z budzetem 166 (ClanBudget.ChestOf: nie gracz, nie dluznik 168, nie rod w zajeciu, nie kompania najemna,
    /// nie Straz bez zoldu, nie Inni), najwyzej
    ///   min(kiesa rodziny - rezerwa rodu R, kiesa glowy - max(Family Purse Floor, zold dnia rodu)),
    /// gdzie R = rezerwa wojny z budzetu 166 (max(War Reserve Floor; War Reserve Days x D) + War Reserve Per Adult x dorosli - ta sama rezerwa, ktorej pilnuja
    /// skrzynia wojenna i chleb biedoty TownFolk.BreadRest), a zold dnia = partie + zalogi z ostatniego rozliczenia rodu (ClanIncomeBook.WageOf) - glowa zostaje
    /// z zoldem dnia, jak przy dworze (CourtFull, regula 5).
    ///   PIERWSZA DOBA PO WCZYTANIU (recenzja 16.10): budzet 166 nie jest w zapisie i liczy go dopiero pierwsze ClanBudget.Daily - do tego czasu (ClanBudget.Pending)
    ///   R tym samym wzorem z D z zapisu budzetu (D wczorajszej doby) albo G/60 (start 166), te same wykluczenia (ClanBudget.PendingChestOf). Budzet 166 wylaczony -
    ///   rody nie placa.
    ///   SPLATA DLUGU (nie wyplata w turnieju) dodatkowo najwyzej TEMPO jak chleb biedoty: ta czesc tempa skrzyni (kiesa rodziny - R) / War Chest Days na dobe,
    ///   ktorej nie bierze zold (wojna 1 - War Chest To Wages, pokoj 1 - Peace Chest Share x War Chest To Wages), minus chleb zaplacony dzis przez ten rod i minus
    ///   splaty dlugow tego rodu juz dzis. Wyplata w turnieju - pelna podloga (bez tempa).
    /// Przelewy prawdziwe: kasa osady -> gracz (GiveGoldAction.ApplyForSettlementToCharacter), glowa rodu -> gracz (GiveGoldAction.ApplyBetweenCharacters) -
    /// zdarzenie gry HeroOrPartyTradedGold z prawdziwym platnikiem (ksiega pieniadza widzi przelew, nic z niczego); RealPayer.GivePrefix nie rusza (dajacy nie null).
    /// Wyjatek przed pierwszym przelewem: PayChain zwraca false i wolajacy idzie stara sciezka (kasa wedlug CapOf, "could pay only"); po przelewie - tylko licznik.
    ///
    /// D2 DLUGI (TourneyDebts): reszta, ktorej nikt nie zaplacil, to dlug osady wobec gracza (id osady -> kwota, rodzaje, doba powstania; w zapisie "arm_tp_debts",
    /// SaveText.Sync). Splata co dobe (ArmouryBehavior: osobne wywolanie PO ClanBudget.Daily i po ksiedze pieniadza - rody placa z budzetu TEJ doby, przelew poza
    /// blokiem ksiegi doby, jak wyplata w turnieju) i w pierwszej godzinie po starcie sesji (bez przelewow w OnSessionLaunched): ten sam lancuch - kasa osady
    /// z nadwyzki, gospodarz, panujacy (ten, kto TERAZ trzyma osade i krolestwo; osada TERAZ gracza - tylko kasa osady), kazdy najwyzej to, co moze dac tej doby
    /// (regula wyzej, z tempem). Splata dopiero od min(reszta dlugu, 50) - drobne kwoty zostaja u platnikow do wiekszej splaty (bez codziennych komunikatow
    /// o kilku denarach). Krolestwo osady w wojnie z krolestwem gracza - nic do pokoju (licznik). Komunikat gracza przy kazdej splacie, ktora przeszla.
    ///
    /// D3 WYROWNANIE SEAGARD (TourneySeagardCatchUp): decyzja RAZ, przy pierwszym starcie sesji po zmianie; flaga w zapisie: 0 nierozstrzygniete, 1 zrobione,
    /// 2 nie dotyczy, 3 dotyczy i czeka na klucze. Dotyczy tylko zapisu wczytanego BEZ klucza arm_tp_debts (zapis sprzed tej zmiany - Import(null); nowa kampania
    /// nie przechodzi przez Import, zapis po zmianie ma klucz), bohatera "Ithauser" (kampania Jeffa), gdy ROT_town33 (Seagard) jest miastem i czas kampanii przy
    /// starcie >= 109020 (po dobie turnieju 109019 - zapis sprzed konca tej doby: turniej rozegrany jeszcze raz zaplaci lancuch, wiec bez wyrownania).
    /// Inaczej flaga 2 od razu (nowa kampania z tym samym imieniem i zapis sprzed turnieju nie dostana 1522). Czeka (flaga 3) tylko na wylaczony klucz
    /// TourneySeagardCatchUp albo TourneyDebts - nigdy na czas. Wtedy dlug Seagard +1522 = 1300 (nagroda TXP, zaplacone 0) + 222 (zysk zakladu: 1425 - 1203 -
    /// kwoty z logu; zrzut mowil 1205), flaga 1, linia logu; komunikat gracza i splata w pierwszej godzinie (przy starcie sesji czesto jeszcze ekran wczytywania)
    /// regula WYPLATY (pelna podloga, bez tempa - to zalegla wyplata z turnieju), reszta jak kazdy dlug.
    ///
    /// Stan: dlugi i flaga D3 (zapis), znacznik "zapis sprzed zmiany", liczniki doby, splaty rodow dzis i flaga splaty po starcie sesji (pamiec; Reset
    /// z PlayerFees8.Reset - konstruktor ArmouryBehavior, PRZED SyncData). Wyjatek w naszym kodzie - licznik potkniec, nic nie gasimy. Linie "Turniej-platnik (TP):".
    /// </summary>
    internal static class TourneyPay
    {
        private const string P = "Turniej-platnik (TP): ";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // D3: turniej w Seagard 16.10 (log Armoury-2026-10-16_10-14-41.log: 10:21:45 nagroda, linia "Wydatki gracza (E8/D2): dzien 109019")
        private const string SeagardId = "ROT_town33", JeffHero = "Ithauser";
        private const double CatchUpFromDay = 109020.0;
        private const int CatchUpPrize = 1300, CatchUpBet = 222;
        private const int FOpen = 0, FDone = 1, FNo = 2, FWait = 3;   // flaga D3 w zapisie

        private const int MinRepay = 50;   // splata dlugu dopiero od min(reszta, 50) - bez codziennych komunikatow o kilku denarach

        internal const int MTourney = 1, MOther = 2, MPrisoners = 4;   // rodzaje dlugu: turniej (nagroda, zaklad, cena nagrody) / arena i plansza / jency u posrednika (m110-jency)

        private static bool Key(Func<Settings, bool> k) { var s = Settings.Current; return s != null && k(s); }
        internal static bool ChainOn { get { return Key(s => s.TourneyPayerChain); } }
        internal static bool DebtsOn { get { return Key(s => s.TourneyDebts); } }
        internal static bool CatchUpOn { get { return Key(s => s.TourneySeagardCatchUp); } }
        /// <summary>Nowy tor wyplaty (lancuch albo dlugi) - inaczej PlayerFees8 jak przed zmiana.</summary>
        internal static bool AnyOn { get { return ChainOn || DebtsOn; } }

        internal struct Parts
        {
            public int Town, Host, Ruler, Debt, Lost;
            public Hero HostHero, RulerHero;
            public bool Own;   // osada rodu gracza - lancuch skonczyl sie na kasie osady
            public int Paid { get { return Town + Host + Ruler; } }
        }

        /// <summary>Ile kto da (liczone przed przelewem - drobna splata moze poczekac).</summary>
        private struct Plan
        {
            public int Town, Host, Ruler;
            public Clan HostClan, RulerClan;
            public Hero HostHero, RulerHero;
            public double HostR, RulerR;
            public bool HostOld, RulerOld;   // R z budzetu z zapisu (pierwsza doba po wczytaniu)
            public bool Own;
            public int Sum { get { return Town + Host + Ruler; } }
        }

        private sealed class Rec { public string Id; public long Amount; public int Mask; public int Day; }

        private static readonly Dictionary<string, Rec> _debts = new Dictionary<string, Rec>(StringComparer.Ordinal);
        private static int _flag;           // D3 (zapis): FOpen / FDone / FNo / FWait
        private static bool _legacy;        // zapis wczytany bez klucza arm_tp_debts (sprzed tej zmiany) - tylko wtedy D3 moze dotyczyc
        private static bool _pending;       // splata w pierwszej godzinie po starcie sesji
        private static bool _catchNow;      // D3 zrobione w tej sesji: w pierwszej godzinie dlug Seagard regula wyplaty
        private static string _catchMsg;    // komunikat D3 dla gracza - w pierwszej godzinie
        private static string _catchText = "nie sprawdzane";
        private static readonly Dictionary<Clan, long> _repToday = new Dictionary<Clan, long>();   // splaty dlugow rodu dzis (tempo)
        private static int _repDay = -1;

        // liczniki doby (linia "Turniej-platnik (TP): dzien N")
        private static int _chainN, _chainShortN, _newDebtN, _repayN, _warWait, _unknown, _belowR, _stumbles, _stumblesDay, _smallWait, _oldBudget;
        private static long _cTown, _cHost, _cRuler, _cDebt, _cLost, _cOwnLost, _rTown, _rHost, _rRuler, _debtStart = -1, _newDebt, _repaid;

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumblesDay++;
            try { Log.Error("TourneyPay." + where, e); } catch { }
        }

        private static void ClearDay()
        {
            _chainN = _chainShortN = _newDebtN = _repayN = _warWait = _unknown = _belowR = _stumblesDay = _smallWait = _oldBudget = 0;
            _cTown = _cHost = _cRuler = _cDebt = _cLost = _cOwnLost = _rTown = _rHost = _rRuler = _newDebt = _repaid = 0;
            _debtStart = DebtSum();
        }

        /// <summary>Nowa kampania i kazde wczytanie (PlayerFees8.Reset - WorldMeasure.Reset, konstruktor ArmouryBehavior, PRZED SyncData).</summary>
        internal static void Reset()
        {
            _debts.Clear(); _flag = FOpen; _legacy = false; _pending = false; _catchNow = false; _catchMsg = null; _catchText = "nie sprawdzane"; _stumbles = 0;
            _repToday.Clear(); _repDay = -1;
            ClearDay();
            try { TourneyFolkPurse.Reset(); } catch { }   // f8-sakiewka (18.10): liczniki sakiewki turniejowej od mieszczan od nowa (pamiec, nic w zapisie)
        }

        private static long DebtSum() { long s = 0; foreach (var r in _debts.Values) if (r.Amount > 0) s += r.Amount; return s; }

        private static string NameOf(Settlement s) { return s != null && s.Name != null ? s.Name.ToString() : "The town"; }
        private static string NameOf(Hero h) { return h != null && h.Name != null ? h.Name.ToString() : "its lord"; }

        // ------------------------------------------------------------ platnicy

        /// <summary>Ile kasa osady moze dac na splate dlugu: miasto z nadwyzki (Seal31.Spare), zamek CastlePurse.Spendable (przez Spare), najwyzej cala kasa.</summary>
        private static int TownCap(Settlement st)
        {
            if (st == null || st.SettlementComponent == null || st.Town == null) return 0;
            long gold = Math.Max(0, st.SettlementComponent.Gold);
            return (int)Math.Max(0L, Math.Min(int.MaxValue, Math.Min(gold, Seal31.Spare(st))));
        }

        internal static long RepaidToday(Clan c)
        {
            long v;
            return c != null && _repDay == (int)CampaignTime.Now.ToDays && _repToday.TryGetValue(c, out v) ? v : 0;
        }

        private static void NoteRepaid(Clan c, long x)
        {
            if (c == null || x <= 0) return;
            int d = (int)CampaignTime.Now.ToDays;
            if (d != _repDay) { _repToday.Clear(); _repDay = d; }
            long v; _repToday.TryGetValue(c, out v); _repToday[c] = v + x;
        }

        /// <summary>Ile rod moze dac teraz (regula w naglowku). repay - splata dlugu: dodatkowo tempo jak chleb biedoty. R - rezerwa uzyta w regule,
        /// old - R z budzetu z zapisu (pierwsza doba po wczytaniu).</summary>
        private static int ClanSpare(Clan c, bool repay, out Hero head, out double R, out bool old)
        {
            head = null; R = 0; old = false;
            try
            {
                if (c == null || c == Clan.PlayerClan || c.IsEliminated) return 0;
                var h = c.Leader;
                if (h == null || !h.IsAlive || ReferenceEquals(h, Hero.MainHero)) return 0;
                double G; bool war;
                if (!ClanBudget.ChestOf(c, out G, out R, out war))
                {
                    if (!ClanBudget.PendingChestOf(c, out G, out R, out war)) return 0;
                    old = true;
                }
                var s = Settings.Current;
                long keep = Math.Max(s != null ? Math.Max(0, s.FamilyPurseFloor) : 5000, ClanIncomeBook.WageOf(c));
                long fam = ClanIncomeBook.FamilyGoldOf(c);
                long rr = (long)Math.Ceiling(Math.Max(0, R));
                long can = Math.Min(fam - rr, (long)h.Gold - keep);
                if (repay && s != null)
                {
                    double rate = TownFolk.BreadLordShare(s, war) * Math.Max(0, fam - rr) / Math.Max(1f, s.WarChestDays) - TownFolk.BreadLordToday(c) - RepaidToday(c);
                    can = Math.Min(can, (long)Math.Floor(Math.Max(0, rate)));
                }
                if (can <= 0) return 0;
                head = h;
                return (int)Math.Min(int.MaxValue, can);
            }
            catch (Exception e) { Stumble("ClanSpare", e); head = null; return 0; }
        }

        private static bool OwnedByPlayer(Settlement st) { return st != null && st.OwnerClan != null && st.OwnerClan == Clan.PlayerClan; }

        /// <summary>Kto ile da: kasa osady (townCap), gospodarz, panujacy (inny rod) - kazdy najwyzej to, co moze dac; osada gracza - tylko kasa.</summary>
        private static Plan PlanOf(Settlement st, int due, int townCap, bool repay)
        {
            var q = new Plan();
            int left = due;
            q.Town = Math.Max(0, Math.Min(left, townCap)); left -= q.Town;
            q.Own = OwnedByPlayer(st);
            if (left <= 0 || q.Own || !ChainOn) return q;
            var host = st.OwnerClan;
            Hero hh; double hr; bool ho;
            int a = Math.Min(left, ClanSpare(host, repay, out hh, out hr, out ho));
            if (a > 0 && hh != null) { q.Host = a; q.HostClan = host; q.HostHero = hh; q.HostR = hr; q.HostOld = ho; left -= a; }
            if (left <= 0) return q;
            var k = st.MapFaction as Kingdom;
            var ruler = k != null ? k.RulingClan : null;
            if (ruler == null || ReferenceEquals(ruler, host)) return q;
            Hero rh; double rr; bool ro;
            int b = Math.Min(left, ClanSpare(ruler, repay, out rh, out rr, out ro));
            if (b > 0 && rh != null) { q.Ruler = b; q.RulerClan = ruler; q.RulerHero = rh; q.RulerR = rr; q.RulerOld = ro; }
            return q;
        }

        private static void AfterClan(Clan c, double R, bool old, int x, bool repay)
        {
            if (repay) NoteRepaid(c, x);
            if (old) _oldBudget++;
            try { if (ClanIncomeBook.FamilyGoldOf(c) < R) _belowR++; } catch { }   // kontrola: rod po wyplacie nie ponizej rezerwy (oczekiwane 0)
        }

        /// <summary>Przelewy wedlug planu w kolejnosci kasa, gospodarz, panujacy; p - tylko to, co naprawde przeszlo (wyjatek zatrzymuje dalsze).</summary>
        private static void Execute(Settlement st, Hero me, Plan q, bool dn, bool repay, ref Parts p)
        {
            p.Own = q.Own;
            if (q.Town > 0) { GiveGoldAction.ApplyForSettlementToCharacter(st, me, q.Town, dn); p.Town = q.Town; }
            if (q.Host > 0 && q.HostHero != null)
            {
                GiveGoldAction.ApplyBetweenCharacters(q.HostHero, me, q.Host, dn); p.Host = q.Host; p.HostHero = q.HostHero;
                AfterClan(q.HostClan, q.HostR, q.HostOld, q.Host, repay);
            }
            if (q.Ruler > 0 && q.RulerHero != null)
            {
                GiveGoldAction.ApplyBetweenCharacters(q.RulerHero, me, q.Ruler, dn); p.Ruler = q.Ruler; p.RulerHero = q.RulerHero;
                AfterClan(q.RulerClan, q.RulerR, q.RulerOld, q.Ruler, repay);
            }
        }

        /// <summary>D1: wyplata graczowi lancuchem. townCap - ile da kasa osady (regula wolajacego). true - p to czesci (wolajacy liczy swoje liczniki i mowi
        /// graczowi); false - wyjatek, zanim cokolwiek przeszlo: wolajacy idzie stara sciezka (kasa wedlug CapOf i "could pay only").</summary>
        internal static bool PayChain(Settlement st, int due, int townCap, bool dn, int mask, out Parts p)
        {
            p = new Parts();
            var me = Hero.MainHero;
            int g0 = 0;
            try
            {
                if (st == null || st.SettlementComponent == null || me == null || due <= 0) return false;
                g0 = me.Gold;
                var q = PlanOf(st, due, townCap, false);
                try { Execute(st, me, q, dn, false, ref p); }
                catch (Exception e)
                {
                    Stumble("PayChain(przelew)", e);
                    if (p.Paid == 0 && me.Gold == g0) return false;   // nic nie przeszlo - stara sciezka u wolajacego
                }
                int left = Math.Max(0, due - p.Paid);   // z czesci naprawde zaplaconych (takze po wyjatku w drugim platniku)
                if (left > 0)
                {
                    // m110-jency (recenzja): jency w osadzie gracza - kupuje KASA osady, nie kiesa gracza: reszta to dlug kasy (splata z jej nadwyzki, PayDebts - osada gracza tylko kasa)
                    if (DebtsOn && (!q.Own || mask == MPrisoners)) { AddDebt(st, left, mask); p.Debt = left; _newDebtN++; _newDebt += left; }
                    else { p.Lost = left; if (q.Own) _cOwnLost += left; }
                }
                _chainN++; _cTown += p.Town; _cHost += p.Host; _cRuler += p.Ruler; _cDebt += p.Debt; _cLost += p.Lost;
                if (p.Paid < due) _chainShortN++;
                return true;
            }
            catch (Exception e)
            {
                Stumble("PayChain", e);
                bool moved = p.Paid > 0 || p.Debt > 0;
                try { if (!moved && me != null && me.Gold != g0) moved = true; } catch { moved = true; }
                if (moved) p.Lost = Math.Max(0, due - p.Paid - p.Debt);
                return moved;
            }
        }

        /// <summary>m110-jency (recenzja): osada krolestwa w wojnie z graczem - bez lordow (wrog nie placi od reki), cala reszta w dlug (splata dopiero w pokoju,
        /// PayDebts). Zwraca czesci jak PayChain (Debt albo Lost przy kluczu TourneyDebts NIE).</summary>
        internal static Parts DebtOnly(Settlement st, int due, int mask)
        {
            var p = new Parts();
            try
            {
                if (st == null || due <= 0) return p;
                if (DebtsOn) { AddDebt(st, due, mask); p.Debt = due; _newDebtN++; _newDebt += due; }
                else p.Lost = due;
                _chainN++; _cDebt += p.Debt; _cLost += p.Lost; _chainShortN++;
            }
            catch (Exception e) { Stumble("DebtOnly", e); }
            return p;
        }

        internal static bool AtWarWithMe(Settlement st) { try { return st != null && AtWarWithPlayer(st); } catch { return false; } }

        private static void AddDebt(Settlement st, long amount, int mask)
        {
            if (st == null || st.StringId == null || amount <= 0) return;
            Rec r;
            if (!_debts.TryGetValue(st.StringId, out r))
            {
                r = new Rec { Id = st.StringId, Day = (int)CampaignTime.Now.ToDays };
                _debts[st.StringId] = r;
            }
            r.Amount += amount; r.Mask |= mask;
        }

        private static long DebtOf(Settlement st) { Rec r; return st != null && st.StringId != null && _debts.TryGetValue(st.StringId, out r) ? r.Amount : 0; }

        // ------------------------------------------------------------ komunikaty gracza (D4)

        internal const int TPrize = 0, TBonus = 1, TBet = 2, TBoard = 3, TArena = 4, TItem = 5, TPris = 6;

        private static string Join(List<string> l)
        {
            if (l.Count == 1) return l[0];
            var sb = new StringBuilder();
            for (int i = 0; i < l.Count; i++) sb.Append(i == 0 ? "" : i == l.Count - 1 ? " and " : ", ").Append(l[i]);
            return sb.ToString();
        }

        /// <summary>Kto zaplacil i co zostalo (wolane, gdy kasa osady sama nie pokryla kwoty).</summary>
        internal static void Tell(int what, Settlement st, int due, Parts p, int townBefore = 0)
        {
            try
            {
                // m110-jency: townBefore - czesc, ktora kasa osady dala juz wczesniej (RealPayer z nadwyzki), lancuch dostal tylko reszte
                p.Town += Math.Max(0, townBefore);
                string n = NameOf(st);
                string subj, verb = "was";
                if (what == TBonus) subj = "Your match bonus of " + due + " denars";
                else if (what == TBet) { subj = "Your bet winnings of " + due + " denars"; verb = "were"; }
                else if (what == TBoard) { subj = "Your winnings of " + due + " denars at the board"; verb = "were"; }
                else if (what == TArena) subj = "Your purse of " + due + " denars for the arena bouts";
                else if (what == TItem) subj = "The price of your prize, " + due + " denars,";
                else if (what == TPris) subj = "The price of your prisoners, " + due + " denars,";
                else subj = "The prize of " + due + " denars";
                var who = new List<string>(3);
                if (p.Town > 0) who.Add(n + " (" + p.Town + ")");
                if (p.Host > 0) who.Add(NameOf(p.HostHero) + " (" + p.Host + ")");
                if (p.Ruler > 0) who.Add(NameOf(p.RulerHero) + " (" + p.Ruler + ")");
                var sb = new StringBuilder(200);
                if (who.Count > 0)
                {
                    sb.Append(subj).Append(' ').Append(verb).Append(p.Paid < due ? " paid in part by " : " paid by ").Append(Join(who)).Append('.');
                    if (p.Debt <= 0 && p.Lost > 0)
                    {
                        if (p.Own) sb.Append(" You hold ").Append(n).Append(" yourself, so the remaining ").Append(p.Lost).Append(" was yours to pay.");
                        else sb.Append(" Neither ").Append(n).Append(" nor its lords could pay the remaining ").Append(p.Lost).Append('.');
                    }
                }
                else if (p.Debt > 0) sb.Append(subj).Append(" could not be paid now.");
                else if (p.Own) sb.Append(subj).Append(" could not be paid: you hold ").Append(n).Append(" yourself, so it was yours to pay.");
                else sb.Append(subj).Append(" could not be paid: neither ").Append(n).Append(" nor its lords had the money to spare.");
                if (p.Debt > 0)
                {
                    long all = DebtOf(st);
                    sb.Append(' ').Append(n).Append(" owes you ").Append(p.Debt).Append(all > p.Debt ? " (" + all + " in all)" : "").Append(" - it will pay when it can.");
                }
                Log.Player(sb.ToString(), p.Debt > 0 || p.Lost > 0);
                Log.Info(P + "wyplata - " + n + ": nalezne " + due + ", kasa osady " + p.Town + ", gospodarz " + (p.Host > 0 ? NameOf(p.HostHero) + " " + p.Host : "0")
                         + ", panujacy " + (p.Ruler > 0 ? NameOf(p.RulerHero) + " " + p.Ruler : "0") + ", w dlug " + p.Debt + ", przepadlo " + p.Lost
                         + (p.Own ? " (osada gracza - gospodarzem gracz, bez panujacego)" : "")
                         + " (rodzaj " + what + "; klucze: lancuch " + (ChainOn ? "tak" : "NIE") + ", dlugi " + (DebtsOn ? "tak" : "NIE")
                         + (ClanBudget.Pending ? "; budzet 166 jeszcze nie liczony w tej sesji - rody z R wedlug zapisu" : "") + ").");
            }
            catch (Exception e) { Stumble("Tell", e); }
        }

        // ------------------------------------------------------------ splata dlugow (D2)

        private static bool AtWarWithPlayer(Settlement st)
        {
            var me = Hero.MainHero;
            var a = st.MapFaction; var b = me != null ? me.MapFaction : null;
            return a != null && b != null && !ReferenceEquals(a, b) && FactionManager.IsAtWarAgainstFaction(a, b);
        }

        /// <summary>Splata dlugow lancuchem. only - tylko ta osada, skip - bez tej osady (null - wszystkie); repay - regula splaty (z tempem rodow),
        /// false - regula wyplaty (pelna podloga; tylko wyrownanie D3 w pierwszej godzinie).</summary>
        private static void PayDebts(string when, string only, string skip, bool repay)
        {
            if (!DebtsOn || _debts.Count == 0) return;
            var me = Hero.MainHero;
            if (me == null) return;
            var gone = new List<string>();
            foreach (var r in new List<Rec>(_debts.Values))
            {
                if (only != null && r.Id != only) continue;
                if (skip != null && r.Id == skip) continue;
                try
                {
                    if (r.Amount <= 0) { gone.Add(r.Id); continue; }
                    var st = Settlement.Find(r.Id);
                    if (st == null || st.Town == null || st.SettlementComponent == null) { _unknown++; continue; }
                    if (AtWarWithPlayer(st)) { _warWait++; continue; }
                    int left = (int)Math.Min(int.MaxValue, r.Amount);
                    var p = new Parts();
                    try
                    {
                        var q = PlanOf(st, left, TownCap(st), repay);   // osada TERAZ gracza - tylko kasa (PlanOf)
                        if (q.Sum <= 0 || q.Sum < Math.Min(r.Amount, (long)MinRepay)) { if (q.Sum > 0) _smallWait++; continue; }   // drobna splata czeka do wiekszej
                        Execute(st, me, q, true, repay, ref p);
                    }
                    catch (Exception e) { Stumble("PayDebts(platnicy " + r.Id + ")", e); }
                    int paid = p.Paid;   // dlug schodzi o to, co naprawde przeszlo (takze po wyjatku w drugim platniku)
                    if (paid <= 0) continue;
                    r.Amount -= paid;
                    _repayN++; _repaid += paid; _rTown += p.Town; _rHost += p.Host; _rRuler += p.Ruler;
                    string n = NameOf(st);
                    var who = new List<string>(3);
                    if (p.Town > 0) who.Add(n + " " + p.Town);
                    if (p.Host > 0) who.Add(NameOf(p.HostHero) + " " + p.Host);
                    if (p.Ruler > 0) who.Add(NameOf(p.RulerHero) + " " + p.Ruler);
                    Log.Player(n + " paid you " + paid + " of the " + (r.Mask == MTourney ? "tournament money" : r.Mask == MPrisoners ? "money for your prisoners" : "money") + " it owed you (" + Join(who) + "). "
                               + (r.Amount > 0 ? "It still owes " + r.Amount + "." : "The debt is settled."));
                    Log.Info(P + "splata (" + when + (repay ? "" : ", regula wyplaty") + ") - " + n + ": " + paid + " (kasa osady " + p.Town + ", gospodarz "
                             + (p.Host > 0 ? NameOf(p.HostHero) + " " + p.Host : "0") + ", panujacy " + (p.Ruler > 0 ? NameOf(p.RulerHero) + " " + p.Ruler : "0")
                             + (p.Own ? "; osada gracza - tylko kasa" : "") + "), zostalo " + Math.Max(0, r.Amount) + ".");
                    if (r.Amount <= 0) gone.Add(r.Id);
                }
                catch (Exception e) { Stumble("PayDebts(" + r.Id + ")", e); }
            }
            foreach (var id in gone) _debts.Remove(id);
        }

        // ------------------------------------------------------------ wyrownanie Seagard (D3)

        /// <summary>Czy D3 dotyczy tej kampanii (decyzja raz, przy pierwszym starcie sesji): null - dotyczy, inaczej powod "nie dotyczy".</summary>
        private static string Applies()
        {
            if (!_legacy) return "zapis po tej zmianie albo nowa kampania (bez wczytania zapisu sprzed zmiany)";
            var me = Hero.MainHero;
            string nm = me != null && me.Name != null ? me.Name.ToString().Trim() : "";
            if (!string.Equals(nm, JeffHero, StringComparison.Ordinal)) return "bohater '" + nm + "', nie " + JeffHero;
            var st = Settlement.Find(SeagardId);
            if (st == null || !st.IsTown || st.Town == null) return "brak miasta " + SeagardId;
            double now = CampaignTime.Now.ToDays;
            if (now < CatchUpFromDay)
                return "zapis z doby " + now.ToString("0.0", Inv) + ", przed koncem doby turnieju 109019 (turniej rozegrany jeszcze raz zaplaci lancuch)";
            return null;
        }

        /// <summary>D3: przy starcie sesji decyzja (flaga 0 -> 2 albo 3/1); flaga 3 - czeka tylko na klucze (start sesji i doba). Bez przelewow - splata w pierwszej godzinie.</summary>
        private static void CatchUp(string when)
        {
            try
            {
                if (_flag == FDone) { _catchText = "zrobione (flaga 1 w zapisie)"; return; }
                if (_flag == FNo) { if (_catchText == "nie sprawdzane") _catchText = "nie dotyczy (flaga 2 w zapisie)"; return; }
                if (_flag != FWait)
                {
                    string why = Applies();
                    if (why != null)
                    {
                        _flag = FNo;
                        _catchText = "nie dotyczy - " + why + " (flaga 2 w zapisie)";
                        Log.Info(P + "wyrownanie Seagard 16.10 (" + when + "): " + _catchText + ".");
                        return;
                    }
                    _flag = FWait;   // kampania Jeffa, zapis sprzed zmiany, po turnieju - decyzja zapadla, czeka juz tylko na klucze
                }
                if (!CatchUpOn) { _catchText = "dotyczy - czeka na klucz Tourney Seagard Catch Up (WYLACZONY; flaga 3 w zapisie)"; return; }
                if (!DebtsOn) { _catchText = "dotyczy - czeka na klucz Tourney Debts (WYLACZONY; flaga 3 w zapisie)"; return; }
                var st = Settlement.Find(SeagardId);
                if (st == null || !st.IsTown || st.Town == null) { _flag = FNo; _catchText = "nie dotyczy - brak miasta " + SeagardId + " (flaga 2 w zapisie)"; return; }
                int due = CatchUpPrize + CatchUpBet;
                AddDebt(st, due, MTourney);
                _flag = FDone;
                _newDebtN++; _newDebt += due;
                _catchText = "zrobione teraz (" + when + "): dlug " + NameOf(st) + " +" + due;
                Log.Info(P + "wyrownanie Seagard 16.10 (" + when + ") - dlug " + NameOf(st) + " (" + SeagardId + ") wobec gracza +" + due + ": nagroda TXP " + CatchUpPrize
                         + " (zaplacone 0) i zysk zakladu " + CatchUpBet + " (1425 nalezne, 1203 zaplacone) - log 16.10 10:21:45, linia doby 109019; dlug osady razem " + DebtOf(st)
                         + "; zapis sprzed zmiany, czas " + CampaignTime.Now.ToDays.ToString("0.0", Inv) + "; splata w pierwszej godzinie regula wyplaty (kasa, gospodarz, panujacy), reszta co dobe.");
                _catchMsg = NameOf(st) + " owes you " + due + " for the tournament you won there (" + CatchUpPrize + " of prize money and " + CatchUpBet
                            + " of your bet) - it will pay when it can.";
                _catchNow = true;
                _pending = true;
            }
            catch (Exception e) { Stumble("CatchUp", e); }
        }

        // ------------------------------------------------------------ takty

        /// <summary>Z OnSessionLaunched (po PlayerFees8.SessionStart, ustawienia MCM juz wczytane): linia startu, decyzja D3; splata w pierwszej godzinie.</summary>
        internal static void SessionStart()
        {
            try
            {
                CatchUp("start sesji");   // nowy dlug D3 liczy sie w "nowe" doby (poczatek doby = stan z zapisu, ustawiony w Import / Reset)
                if (_debts.Count > 0 && DebtsOn) _pending = true;
                Log.Info(P + "start sesji | " + KeysText() + " | " + DebtText() + " | wyrownanie Seagard 16.10: " + _catchText
                         + " | zapis " + (_legacy ? "sprzed zmiany (bez klucza arm_tp_debts)" : "z kluczem arm_tp_debts albo nowa kampania")
                         + (_pending ? " | splata w pierwszej godzinie: kasa osady z nadwyzki, rody " + (ClanBudget.On ? "z rezerwa R wedlug zapisu budzetu 166 (D z zapisu albo G/60), od pierwszej polnocy z budzetu doby" : "nie (budzet 166 WYLACZONY)") : "")
                         + " | potkniecia " + _stumbles + ".");
            }
            catch (Exception e) { Stumble("SessionStart", e); }
        }

        /// <summary>Pierwsza splata po starcie sesji: komunikat D3, dlug Seagard regula wyplaty (gdy D3 zrobione teraz), reszta dlugow regula splaty.</summary>
        private static void FirstPay(string when)
        {
            try
            {
                if (_catchMsg != null) { var t = _catchMsg; _catchMsg = null; Log.Player(t, true); }
                if (_catchNow) { _catchNow = false; PayDebts(when, SeagardId, null, false); PayDebts(when, null, SeagardId, true); }
                else PayDebts(when, null, null, true);
            }
            catch (Exception e) { Stumble("FirstPay", e); }
        }

        /// <summary>Co godzine (ArmouryBehavior.RegisterEvents): tylko gdy czeka splata po starcie sesji - potem nic.</summary>
        internal static void Hourly()
        {
            if (!_pending) return;
            _pending = false;
            FirstPay("pierwsza godzina sesji");
        }

        /// <summary>Raz na dobe (ArmouryBehavior.OnDailyTickBodyInner - PO ClanBudget.Daily i po ksiedze pieniadza, rody z budzetu tej doby): wyrownanie D3
        /// czekajace na klucze (flaga 3), splata dlugow, linia doby.</summary>
        internal static void Daily(int day)
        {
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                if (_flag == FWait) CatchUp("doba");   // tylko czekanie na klucze - nigdy na czas
                if (_pending) { _pending = false; FirstPay("doba (przed pierwsza godzina)"); }
                else PayDebts("doba", null, null, true);
                long end = DebtSum();
                long start = _debtStart < 0 ? end : _debtStart;
                long diff = start + _newDebt - _repaid - end;
                var sb = new StringBuilder(1000);
                sb.Append(P).Append("dzien ").Append(day).Append(" | ").Append(KeysText())
                  .Append(" | wyplaty lancuchem ").Append(_chainN).Append(" (niepelne ").Append(_chainShortN).Append("): kasa osady ").Append(_cTown).Append(", gospodarz ").Append(_cHost)
                  .Append(", panujacy ").Append(_cRuler).Append(", w dlug ").Append(_cDebt).Append(", przepadlo ").Append(_cLost).Append(" (w tym osada gracza ").Append(_cOwnLost).Append(')')
                  .Append(" | splaty dlugow ").Append(_repayN).Append(": ").Append(_repaid).Append(" (kasa osady ").Append(_rTown).Append(", gospodarz ").Append(_rHost)
                  .Append(", panujacy ").Append(_rRuler).Append("), czeka na pokoj ").Append(_warWait).Append(", osada nieznana ").Append(_unknown)
                  .Append(", drobna splata czeka (ponizej ").Append(MinRepay).Append(") ").Append(_smallWait)
                  .Append(" | nowe dlugi ").Append(_newDebtN).Append(": ").Append(_newDebt)
                  .Append(" | ").Append(DebtText())
                  .Append(" | wyrownanie Seagard 16.10: ").Append(_catchText)
                  .Append(" | rody z rezerwa wedlug zapisu budzetu (pierwsza doba po wczytaniu) ").Append(_oldBudget)
                  .Append(" | kontrola (prog 0): rod ponizej rezerwy po wyplacie ").Append(_belowR).Append(", roznica rachunku dlugow ").Append(diff)
                  .Append(" (poczatek ").Append(start).Append(" + nowe ").Append(_newDebt).Append(" - splaty ").Append(_repaid).Append(" - koniec ").Append(end).Append(')')
                  .Append(" | koszt ").Append(((Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency).ToString("0.00", Inv)).Append(" ms/dobe")
                  .Append(" | potkniecia dzis ").Append(_stumblesDay).Append(" (od wczytania ").Append(_stumbles).Append(").");
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally { ClearDay(); }
        }

        private static string KeysText()
        {
            return "klucze: Tourney Payer Chain " + (ChainOn ? "tak" : "WYLACZONE") + ", Tourney Debts " + (DebtsOn ? "tak" : "WYLACZONE")
                   + ", Tourney Seagard Catch Up " + (CatchUpOn ? "tak" : "WYLACZONE");
        }

        private static string DebtText()
        {
            var l = new List<Rec>();
            foreach (var r in _debts.Values) if (r.Amount > 0) l.Add(r);
            l.Sort((a, b) => b.Amount.CompareTo(a.Amount));
            var sb = new StringBuilder(300);
            sb.Append("dlugi osad wobec gracza ").Append(l.Count).Append(" na sume ").Append(DebtSum());
            if (l.Count > 0)
            {
                sb.Append(" [");
                for (int i = 0; i < l.Count && i < 12; i++)
                {
                    var r = l[i];
                    string n = r.Id;
                    try { var st = Settlement.Find(r.Id); if (st != null) n = NameOf(st); } catch { }
                    sb.Append(i > 0 ? ", " : "").Append(n).Append(' ').Append(r.Amount).Append(" (od doby ").Append(r.Day).Append(')');
                }
                if (l.Count > 12) sb.Append(", ...");
                sb.Append(']');
            }
            return sb.ToString();
        }

        // ------------------------------------------------------------ zapis ("arm_tp_debts", SaveText.Sync)

        /// <summary>"v1|flaga D3 0-3|id,kwota,rodzaje,doba;..." - id osad bez przecinkow, srednikow i kresek. Zawsze niepusty (zapis po zmianie = klucz obecny).</summary>
        internal static string Export()
        {
            var sb = new StringBuilder(64 + _debts.Count * 32);
            sb.Append("v1|").Append(_flag.ToString(Inv)).Append('|');
            bool first = true;
            foreach (var r in _debts.Values)
            {
                if (r.Amount <= 0 || string.IsNullOrEmpty(r.Id)) continue;
                if (!first) sb.Append(';');
                first = false;
                sb.Append(r.Id).Append(',').Append(r.Amount.ToString(Inv)).Append(',').Append(r.Mask.ToString(Inv)).Append(',').Append(r.Day.ToString(Inv));
            }
            return sb.ToString();
        }

        /// <summary>Brak klucza (zapis sprzed zmiany): pusto, flaga D3 0 i znacznik "zapis sprzed zmiany" (jedyny przypadek, w ktorym D3 moze dotyczyc).
        /// Inna wersja - pusto, flaga 0 (D3 nie dotyczy - zapis nie jest sprzed zmiany).</summary>
        internal static void Import(string s)
        {
            _debts.Clear(); _flag = FOpen; _legacy = false;
            try
            {
                if (string.IsNullOrEmpty(s)) { _legacy = true; return; }
                var f = s.Split('|');
                if (f.Length < 3 || f[0] != "v1") { Log.Info(P + "zapis - nieznany format, dlugi pominiete."); return; }
                int fl;
                _flag = int.TryParse(f[1], NumberStyles.Integer, Inv, out fl) && fl >= FOpen && fl <= FWait ? fl : FOpen;
                if (f[2].Length > 0)
                    foreach (var e in f[2].Split(';'))
                    {
                        var g = e.Split(',');
                        long amt; int mask, day;
                        if (g.Length < 4 || g[0].Length == 0 || !long.TryParse(g[1], NumberStyles.Integer, Inv, out amt) || amt <= 0) continue;
                        int.TryParse(g[2], NumberStyles.Integer, Inv, out mask);
                        int.TryParse(g[3], NumberStyles.Integer, Inv, out day);
                        _debts[g[0]] = new Rec { Id = g[0], Amount = amt, Mask = mask, Day = day };
                    }
            }
            catch (Exception e) { Stumble("Import", e); }
            finally { _debtStart = DebtSum(); }
        }
    }
}
