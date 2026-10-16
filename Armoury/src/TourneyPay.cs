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
    /// Ile rod moze dac (ClanSpare - JEDNA regula dla wyplaty w turnieju i dla splat dlugu): tylko rod AI z budzetem 166 policzonym na te dobe
    /// (ClanBudget.ChestOf: nie gracz, nie dluznik 168, nie rod w zajeciu, nie kompania najemna, nie Inni), najwyzej
    ///   min(kiesa rodziny - rezerwa rodu R, kiesa glowy - max(Family Purse Floor, zold dnia rodu)),
    /// gdzie R = rezerwa wojny z budzetu 166 (max(War Reserve Floor; War Reserve Days x D) + War Reserve Per Adult x dorosli - ta sama rezerwa, ktorej pilnuja
    /// skrzynia wojenna i chleb biedoty TownFolk.BreadRest), a zold dnia = partie + zalogi z ostatniego rozliczenia rodu (ClanIncomeBook.WageOf) - glowa zostaje
    /// z zoldem dnia, jak przy dworze (CourtFull, regula 5). Rod bez budzetu (pierwsza doba po wczytaniu, budzet 166 wylaczony) nie placi - czeka dlug.
    /// Przelewy prawdziwe: kasa osady -> gracz (GiveGoldAction.ApplyForSettlementToCharacter), glowa rodu -> gracz (GiveGoldAction.ApplyBetweenCharacters) -
    /// zdarzenie gry HeroOrPartyTradedGold z prawdziwym platnikiem (ksiega pieniadza widzi przelew, nic z niczego); RealPayer.GivePrefix nie rusza (dajacy nie null).
    ///
    /// D2 DLUGI (TourneyDebts): reszta, ktorej nikt nie zaplacil, to dlug osady wobec gracza (id osady -> kwota, rodzaje, doba powstania; w zapisie "arm_tp_debts",
    /// SaveText.Sync). Splata co dobe (PlayerFees8.Daily - przed blokiem ksiegi doby, jak wyplata w turnieju) i w pierwszej godzinie po starcie sesji (bez przelewow
    /// w OnSessionLaunched): ten sam lancuch - kasa osady z nadwyzki, gospodarz, panujacy (ten, kto TERAZ trzyma osade i krolestwo), kazdy najwyzej to, co moze dac
    /// tej doby (regula wyzej). Krolestwo osady w wojnie z krolestwem gracza - nic do pokoju (licznik). Komunikat gracza przy kazdej splacie.
    ///
    /// D3 WYROWNANIE SEAGARD (TourneySeagardCatchUp): raz na kampanie, tylko bohater "Ithauser" (kampania Jeffa), osada ROT_town33 (Seagard) jest miastem,
    /// czas kampanii >= 109020 (po dobie turnieju 109019), flaga w zapisie nieustawiona, dlugi czynne: dlug Seagard +1522 = 1300 (nagroda TXP, zaplacone 0)
    /// + 222 (zysk zakladu: 1425 - 1203 - kwoty z logu; zrzut mowil 1205). Flaga, linia logu, splata w pierwszej godzinie (kasa) i w dobach (rody).
    ///
    /// Stan: dlugi i flaga D3 (zapis), liczniki doby i flaga splaty po starcie sesji (pamiec; Reset z PlayerFees8.Reset - konstruktor ArmouryBehavior,
    /// PRZED SyncData). Wyjatek w naszym kodzie - licznik potkniec, nic nie gasimy. Linie "Turniej-platnik (TP):".
    /// </summary>
    internal static class TourneyPay
    {
        private const string P = "Turniej-platnik (TP): ";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // D3: turniej w Seagard 16.10 (log Armoury-2026-10-16_10-14-41.log: 10:21:45 nagroda, linia "Wydatki gracza (E8/D2): dzien 109019")
        private const string SeagardId = "ROT_town33", JeffHero = "Ithauser";
        private const double CatchUpFromDay = 109020.0;
        private const int CatchUpPrize = 1300, CatchUpBet = 222;

        internal const int MTourney = 1, MOther = 2;   // rodzaje dlugu: turniej (nagroda, zaklad, cena nagrody) / arena i plansza

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
            public int Paid { get { return Town + Host + Ruler; } }
        }

        private sealed class Rec { public string Id; public long Amount; public int Mask; public int Day; }

        private static readonly Dictionary<string, Rec> _debts = new Dictionary<string, Rec>(StringComparer.Ordinal);
        private static bool _caught;        // D3 zrobione (zapis)
        private static bool _pending;       // splata w pierwszej godzinie po starcie sesji
        private static string _catchText = "nie sprawdzane";

        // liczniki doby (linia "Turniej-platnik (TP): dzien N")
        private static int _chainN, _chainShortN, _newDebtN, _repayN, _warWait, _unknown, _belowR, _stumbles, _stumblesDay;
        private static long _cTown, _cHost, _cRuler, _cDebt, _cLost, _rTown, _rHost, _rRuler, _debtStart = -1, _newDebt, _repaid;

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumblesDay++;
            try { Log.Error("TourneyPay." + where, e); } catch { }
        }

        private static void ClearDay()
        {
            _chainN = _chainShortN = _newDebtN = _repayN = _warWait = _unknown = _belowR = _stumblesDay = 0;
            _cTown = _cHost = _cRuler = _cDebt = _cLost = _rTown = _rHost = _rRuler = _newDebt = _repaid = 0;
            _debtStart = DebtSum();
        }

        /// <summary>Nowa kampania i kazde wczytanie (PlayerFees8.Reset - WorldMeasure.Reset, konstruktor ArmouryBehavior, PRZED SyncData).</summary>
        internal static void Reset()
        {
            _debts.Clear(); _caught = false; _pending = false; _catchText = "nie sprawdzane"; _stumbles = 0;
            ClearDay();
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

        /// <summary>Ile rod moze dac teraz (regula w naglowku: rod AI z budzetem 166, ponad rezerwe R rodziny i ponad zold dnia glowy).</summary>
        internal static int ClanSpare(Clan c, out Hero head)
        {
            head = null;
            try
            {
                if (c == null || c == Clan.PlayerClan || c.IsEliminated) return 0;
                var h = c.Leader;
                if (h == null || !h.IsAlive || ReferenceEquals(h, Hero.MainHero)) return 0;
                double G, R; bool war;
                if (!ClanBudget.ChestOf(c, out G, out R, out war)) return 0;
                head = h;
                var s = Settings.Current;
                long keep = Math.Max(s != null ? Math.Max(0, s.FamilyPurseFloor) : 5000, ClanIncomeBook.WageOf(c));
                long fam = ClanIncomeBook.FamilyGoldOf(c);
                long can = Math.Min(fam - (long)Math.Ceiling(Math.Max(0, R)), (long)h.Gold - keep);
                return (int)Math.Max(0L, Math.Min(int.MaxValue, can));
            }
            catch (Exception e) { Stumble("ClanSpare", e); head = null; return 0; }
        }

        private static int FromClan(Clan c, int left, bool dn, out Hero head)
        {
            int x = Math.Min(left, ClanSpare(c, out head));
            if (x <= 0 || head == null) return 0;
            GiveGoldAction.ApplyBetweenCharacters(head, Hero.MainHero, x, dn);
            try
            {
                double G, R; bool war;
                if (ClanBudget.ChestOf(c, out G, out R, out war) && ClanIncomeBook.FamilyGoldOf(c) < R) _belowR++;   // kontrola (oczekiwane 0)
            }
            catch { }
            return x;
        }

        /// <summary>Gospodarz (rod osady), potem panujacy krolestwa osady (inny rod) - kazdy najwyzej to, co moze dac.</summary>
        private static int Clans(Settlement st, int left, bool dn, ref Parts p)
        {
            var host = st.OwnerClan;
            Hero hh;
            int a = FromClan(host, left, dn, out hh);
            if (a > 0) { p.Host += a; p.HostHero = hh; left -= a; }
            if (left <= 0) return 0;
            var k = st.MapFaction as Kingdom;
            var ruler = k != null ? k.RulingClan : null;
            if (ruler == null || ReferenceEquals(ruler, host)) return left;
            Hero rh;
            int b = FromClan(ruler, left, dn, out rh);
            if (b > 0) { p.Ruler += b; p.RulerHero = rh; left -= b; }
            return left;
        }

        /// <summary>D1: wyplata graczowi lancuchem. townCap - ile da kasa osady (regula wolajacego). Zwraca czesci; wolajacy liczy swoje liczniki i mowi graczowi.</summary>
        internal static Parts PayChain(Settlement st, int due, int townCap, bool dn, int mask)
        {
            var p = new Parts();
            var me = Hero.MainHero;
            if (st == null || st.SettlementComponent == null || me == null || due <= 0) return p;
            int left = due;
            int t = Math.Max(0, Math.Min(left, townCap));
            if (t > 0) { GiveGoldAction.ApplyForSettlementToCharacter(st, me, t, dn); p.Town = t; left -= t; }
            try
            {
                if (left > 0 && ChainOn) Clans(st, left, dn, ref p);
            }
            catch (Exception e) { Stumble("PayChain(rody)", e); }
            left = Math.Max(0, due - p.Paid);   // z czesci naprawde zaplaconych (takze po wyjatku w drugim rodzie)
            if (left > 0)
            {
                if (DebtsOn) { AddDebt(st, left, mask); p.Debt = left; _newDebtN++; _newDebt += left; }
                else p.Lost = left;
            }
            _chainN++; _cTown += p.Town; _cHost += p.Host; _cRuler += p.Ruler; _cDebt += p.Debt; _cLost += p.Lost;
            if (p.Paid < due) _chainShortN++;
            return p;
        }

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

        internal const int TPrize = 0, TBonus = 1, TBet = 2, TBoard = 3, TArena = 4, TItem = 5;

        private static string Join(List<string> l)
        {
            if (l.Count == 1) return l[0];
            var sb = new StringBuilder();
            for (int i = 0; i < l.Count; i++) sb.Append(i == 0 ? "" : i == l.Count - 1 ? " and " : ", ").Append(l[i]);
            return sb.ToString();
        }

        /// <summary>Kto zaplacil i co zostalo (wolane, gdy kasa osady sama nie pokryla kwoty).</summary>
        internal static void Tell(int what, Settlement st, int due, Parts p)
        {
            try
            {
                string n = NameOf(st);
                string subj, verb = "was";
                if (what == TBonus) subj = "Your match bonus of " + due + " denars";
                else if (what == TBet) { subj = "Your bet winnings of " + due + " denars"; verb = "were"; }
                else if (what == TBoard) { subj = "Your winnings of " + due + " denars at the board"; verb = "were"; }
                else if (what == TArena) subj = "Your purse of " + due + " denars for the arena bouts";
                else if (what == TItem) subj = "The price of your prize, " + due + " denars,";
                else subj = "The prize of " + due + " denars";
                var who = new List<string>(3);
                if (p.Town > 0) who.Add(n + " (" + p.Town + ")");
                if (p.Host > 0) who.Add(NameOf(p.HostHero) + " (" + p.Host + ")");
                if (p.Ruler > 0) who.Add(NameOf(p.RulerHero) + " (" + p.Ruler + ")");
                var sb = new StringBuilder(200);
                if (who.Count > 0) sb.Append(subj).Append(' ').Append(verb).Append(p.Paid < due ? " paid in part by " : " paid by ").Append(Join(who)).Append('.');
                else sb.Append(subj).Append(" could not be paid now.");
                if (p.Debt > 0)
                {
                    long all = DebtOf(st);
                    sb.Append(' ').Append(n).Append(" owes you ").Append(p.Debt).Append(all > p.Debt ? " (" + all + " in all)" : "").Append(" - it will pay when it can.");
                }
                else if (p.Lost > 0) sb.Append(" Neither ").Append(n).Append(" nor its lords could pay the remaining ").Append(p.Lost).Append('.');
                Log.Player(sb.ToString(), p.Debt > 0 || p.Lost > 0);
                Log.Info(P + "wyplata - " + n + ": nalezne " + due + ", kasa osady " + p.Town + ", gospodarz " + (p.Host > 0 ? NameOf(p.HostHero) + " " + p.Host : "0")
                         + ", panujacy " + (p.Ruler > 0 ? NameOf(p.RulerHero) + " " + p.Ruler : "0") + ", w dlug " + p.Debt + ", przepadlo " + p.Lost
                         + " (rodzaj " + what + "; klucze: lancuch " + (ChainOn ? "tak" : "NIE") + ", dlugi " + (DebtsOn ? "tak" : "NIE") + ").");
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

        private static void PayDebts(string when)
        {
            if (!DebtsOn || _debts.Count == 0) return;
            var me = Hero.MainHero;
            if (me == null) return;
            var gone = new List<string>();
            foreach (var r in new List<Rec>(_debts.Values))
            {
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
                        int t = Math.Min(left, TownCap(st));
                        if (t > 0) { GiveGoldAction.ApplyForSettlementToCharacter(st, me, t, true); p.Town = t; left -= t; }
                        if (left > 0 && ChainOn) Clans(st, left, true, ref p);
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
                    Log.Player(n + " paid you " + paid + " of the " + ((r.Mask & MOther) == 0 ? "tournament money" : "money") + " it owed you (" + Join(who) + "). "
                               + (r.Amount > 0 ? "It still owes " + r.Amount + "." : "The debt is settled."));
                    Log.Info(P + "splata (" + when + ") - " + n + ": " + paid + " (kasa osady " + p.Town + ", gospodarz " + (p.Host > 0 ? NameOf(p.HostHero) + " " + p.Host : "0")
                             + ", panujacy " + (p.Ruler > 0 ? NameOf(p.RulerHero) + " " + p.Ruler : "0") + "), zostalo " + Math.Max(0, r.Amount) + ".");
                    if (r.Amount <= 0) gone.Add(r.Id);
                }
                catch (Exception e) { Stumble("PayDebts(" + r.Id + ")", e); }
            }
            foreach (var id in gone) _debts.Remove(id);
        }

        // ------------------------------------------------------------ wyrownanie Seagard (D3)

        private static void CatchUp(string when)
        {
            try
            {
                if (_caught) { _catchText = "zrobione (flaga w zapisie)"; return; }
                if (!CatchUpOn) { _catchText = "klucz WYLACZONY"; return; }
                var me = Hero.MainHero;
                string nm = me != null && me.Name != null ? me.Name.ToString().Trim() : "";
                if (!string.Equals(nm, JeffHero, StringComparison.Ordinal)) { _catchText = "nie ta kampania (bohater nie " + JeffHero + ")"; return; }
                var st = Settlement.Find(SeagardId);
                if (st == null || !st.IsTown || st.Town == null) { _catchText = "brak miasta " + SeagardId; return; }
                if (!DebtsOn) { _catchText = "czeka - klucz Tourney Debts WYLACZONY"; return; }
                double now = CampaignTime.Now.ToDays;
                if (now < CatchUpFromDay) { _catchText = "czeka - doba " + now.ToString("0.0", Inv) + " przed koncem doby turnieju " + CatchUpFromDay.ToString("0", Inv); return; }
                int due = CatchUpPrize + CatchUpBet;
                AddDebt(st, due, MTourney);
                _caught = true;
                _newDebtN++; _newDebt += due;
                _catchText = "zrobione teraz (" + when + "): dlug " + NameOf(st) + " +" + due;
                Log.Info(P + "wyrownanie Seagard 16.10 (" + when + ") - dlug " + NameOf(st) + " (" + SeagardId + ") wobec gracza +" + due + ": nagroda TXP " + CatchUpPrize
                         + " (zaplacone 0) i zysk zakladu " + CatchUpBet + " (1425 nalezne, 1203 zaplacone) - log 16.10 10:21:45, linia doby 109019; dlug osady razem " + DebtOf(st)
                         + "; splata lancuchem w pierwszej godzinie i co dobe.");
                Log.Player(NameOf(st) + " owes you " + due + " for the tournament you won there (" + CatchUpPrize + " of prize money and " + CatchUpBet
                           + " of your bet) - it will pay when it can.", true);
                _pending = true;
            }
            catch (Exception e) { Stumble("CatchUp", e); }
        }

        // ------------------------------------------------------------ takty

        /// <summary>Z OnSessionLaunched (po PlayerFees8.SessionStart, ustawienia MCM juz wczytane): linia startu, wyrownanie D3; splata w pierwszej godzinie.</summary>
        internal static void SessionStart()
        {
            try
            {
                CatchUp("start sesji");   // nowy dlug D3 liczy sie w "nowe" doby (poczatek doby = stan z zapisu, ustawiony w Import / Reset)
                if (_debts.Count > 0 && DebtsOn) _pending = true;
                Log.Info(P + "start sesji | " + KeysText() + " | " + DebtText() + " | wyrownanie Seagard 16.10: " + _catchText
                         + (_pending ? " | splata w pierwszej godzinie" : "") + " | potkniecia " + _stumbles + ".");
            }
            catch (Exception e) { Stumble("SessionStart", e); }
        }

        /// <summary>Co godzine (ArmouryBehavior.RegisterEvents): tylko gdy czeka splata po starcie sesji - potem nic.</summary>
        internal static void Hourly()
        {
            if (!_pending) return;
            _pending = false;
            try { PayDebts("pierwsza godzina sesji"); }
            catch (Exception e) { Stumble("Hourly", e); }
        }

        /// <summary>Raz na dobe (PlayerFees8.Daily - przed blokiem ksiegi doby): wyrownanie D3 (gdy czekalo na czas), splata dlugow, linia doby.</summary>
        internal static void Daily(int day)
        {
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                if (!_caught) CatchUp("doba");
                PayDebts("doba");
                _pending = false;   // dlugi wlasnie przeszly przez lancuch - bez drugiej proby w najblizszej godzinie
                long end = DebtSum();
                long start = _debtStart < 0 ? end : _debtStart;
                long diff = start + _newDebt - _repaid - end;
                var sb = new StringBuilder(900);
                sb.Append(P).Append("dzien ").Append(day).Append(" | ").Append(KeysText())
                  .Append(" | wyplaty lancuchem ").Append(_chainN).Append(" (niepelne ").Append(_chainShortN).Append("): kasa osady ").Append(_cTown).Append(", gospodarz ").Append(_cHost)
                  .Append(", panujacy ").Append(_cRuler).Append(", w dlug ").Append(_cDebt).Append(", przepadlo ").Append(_cLost)
                  .Append(" | splaty dlugow ").Append(_repayN).Append(": ").Append(_repaid).Append(" (kasa osady ").Append(_rTown).Append(", gospodarz ").Append(_rHost)
                  .Append(", panujacy ").Append(_rRuler).Append("), czeka na pokoj ").Append(_warWait).Append(", osada nieznana ").Append(_unknown)
                  .Append(" | nowe dlugi ").Append(_newDebtN).Append(": ").Append(_newDebt)
                  .Append(" | ").Append(DebtText())
                  .Append(" | wyrownanie Seagard 16.10: ").Append(_catchText)
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

        /// <summary>"v1|flaga D3 0/1|id,kwota,rodzaje,doba;..." - id osad bez przecinkow, srednikow i kresek.</summary>
        internal static string Export()
        {
            var sb = new StringBuilder(64 + _debts.Count * 32);
            sb.Append("v1|").Append(_caught ? '1' : '0').Append('|');
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

        /// <summary>Brak klucza (zapis sprzed zmiany) albo inna wersja - pusto, flaga D3 nieustawiona.</summary>
        internal static void Import(string s)
        {
            _debts.Clear(); _caught = false;
            try
            {
                if (string.IsNullOrEmpty(s)) return;
                var f = s.Split('|');
                if (f.Length < 3 || f[0] != "v1") { Log.Info(P + "zapis - nieznany format, dlugi pominiete."); return; }
                _caught = f[1] == "1";
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
            _debtStart = DebtSum();
        }
    }
}
