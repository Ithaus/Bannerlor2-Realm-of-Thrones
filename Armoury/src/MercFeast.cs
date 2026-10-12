using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// E8/G2 (261, PLAN 8.12e; rozpoznanie C poz. 1; projekt etapu 8 v2 rozdz. 3 "E8/G2", krytyka K2-7; odpowiedz Jeffa 4 z 12.10: TAK) - NAJEMNICY I UCZTA BK.
    ///
    /// 1. KLANY NAJEMNIKOW BEZ LUDZI Z NICZEGO I BEZ WYRZUCANIA WODZA (klucz MercClansRealSoldiers, true).
    ///    BK BKClanBehavior.ConvertTroopsMercenaries(Clan) (co dobe z DailyClanTickImpl; sprawdzone w BannerKings.dll z gry, IL 561 bajtow):
    ///      klan typu "najemnicy" (Clan.IsClanTypeMercenary) W KROLESTWIE - kazdy wpis skladu druzyny spoza szablonu klanu, TAKZE BOHATER, traci jednego
    ///      czlowieka (AddToCounts(wpis, -1)) i dostaje w zamian zolnierza szablonu tego samego poziomu albo BasicTroop (szansa MercenaryConversionChance = 1).
    ///      Bohater (wodz, towarzysz) wypada tak z wlasnej druzyny (TroopRoster -> PartyBase.OnHeroRemoved -> Hero.PartyBelongedTo = null), a na jego miejsce
    ///      staje zolnierz z niczego - codziennie, w kazdej druzynie klanu;
    ///      klan BEZ krolestwa - 3-5 ludzi szablonu na druzyne z niczego. Ta galaz jest w tej wersji BK MARTWA: jedyny wolajacy (DailyClanTickImpl) wychodzi
    ///      przy clan.Kingdom == null, zanim dojdzie do ConvertTroopsMercenaries.
    ///    Teraz (jeden prefiks na tej metodzie - SpoilsCompany.BkConvertPrefix, okno wprost; dla klanu Spoils bez zmian - ma wlasny klucz):
    ///      klan najemnikow w krolestwie - zamiane 1:1 robimy sami tak jak BK (ta sama kolejnosc, ta sama szansa - metoda BK wolana refleksja), ale BEZ
    ///      bohaterow, i metoda BK nie biegnie; klan bez krolestwa - metoda BK nie biegnie (dosypka nie zachodzi takze wtedy, gdy BK kiedys ozywi te galaz).
    ///    Zamiana 1:1 zolnierza nie tworzy czlowieka (jeden schodzi, jeden wchodzi) - zostaje (krytyka K2-7).
    ///    TYLKO LICZNIK (poza oknem projektu, do decyzji): ochotnicy z mapy gry u klanow najemnikow (RecruitmentCampaignBehavior.HourlyTickParty: druzyna
    ///    lorda mniejszej frakcji poza osada, 5% na godzine, 3-7 BasicTroop bez osady i notabla) - to jedyne zywe zrodlo ludzi bez pochodzenia u klanu bez sluzby.
    ///
    /// 2. UCZTA BK BEZ TOWARU Z NICZEGO (klucz FeastNoFreeGoods, true).
    ///    BK Feast.Automanage (uczty gospodarzy AI, co dobe z DailyTickTown): za kazda pozycje zywnosci, wina i piwa z rynku miasta gospodarz placi, spizarnia
    ///    uczty (Settlement.Stash) dostaje n sztuk, a na koniec petla "zdejmij z rynku" robi Town.Owner.ItemRoster.AddToCounts(pozycja, +n) - rynek ROSNIE o n
    ///    zamiast zmalec. Kazdy zakup robi wiec 2n sztuk z niczego (n w spizarni, n na rynku).
    ///    Teraz (to samo okno co E5/A8 FeastTownGetsPaid - prefiks i finalizer BkDipGold na Automanage, jedna latka): prefiks zapamietuje stan rynku i spizarni
    ///    dla zywnosci, wina i piwa, finalizer ustawia rynek na "stan sprzed zakupu minus to, co przybylo spizarni" (zwykle zdejmuje 2n). Niezalezne od tresci
    ///    metody BK: gdyby BK sam zaczal zdejmowac towar, poprawka wynosi 0. Niczego nie dokladamy - tylko zdejmujemy z rynku to, co naprawde wyszlo.
    ///
    /// Stan tylko w pamieci (liczniki doby) - Reset z WorldMeasure.Reset (konstruktor ArmouryBehavior). Nic w zapisie. Linia doby "Najemnicy i uczta BK (E8/G2)"
    /// (takze przy wylaczonych kluczach - wtedy liczy, co robi BK) i linia startowa "latki przy starcie gry" (R11). Wyjatek = licznik potkniec, bez globalnego
    /// wylacznika. Zegar realny tylko do pomiaru kosztu (Stopwatch).
    /// </summary>
    internal static class MercFeast
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        internal static bool MercOn { get { var s = Settings.Current; return s != null && s.MercClansRealSoldiers; } }
        internal static bool GoodsOn { get { var s = Settings.Current; return s != null && s.FeastNoFreeGoods; } }

        // dlugosc IL sprawdzonej tresci BK BKClanBehavior.ConvertTroopsMercenaries (BannerKings.dll z gry, ilspycmd -il 12.10) - inna dlugosc = BK zmienil metode,
        // nasza zamiana 1:1 moze juz nie byc "jak BK" (linia startowa i G2-0 narzedzia)
        private const int BkConvertIl = 561;

        private static int _stumbles, _stumblesDay;
        private static readonly HashSet<string> _errSites = new HashSet<string>();
        private static readonly List<string> _wired = new List<string>(), _missing = new List<string>();
        private static int _wiredN, _wantedN;
        private static string _bodyText = "nie sprawdzono";

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumblesDay++;
            try { if (_errSites.Add(where)) Log.Error("MercFeast." + where, e); } catch { }
        }

        // ------------------------------------------------------------ 1. klany najemnikow: zamiana BK (wolane z SpoilsCompany.BkConvertPrefix)

        private static MethodInfo _mChance;                                   // BK BKClanBehavior.MercenaryConversionChance(CharacterObject merc, CharacterObject troop) - prywatna
        private static readonly object[] _chanceArgs = new object[2];
        private static readonly List<CharacterObject> _tpl = new List<CharacterObject>(16);
        private static readonly List<WarPartyComponent> _parties = new List<WarPartyComponent>(8);
        private static readonly List<TroopRosterElement> _work = new List<TroopRosterElement>(32);

        // liczniki od poprzedniej linii doby
        private static int _cCalls, _cKingdom, _cFree, _cFreeParties, _cNoTpl, _cSwapped, _cHeroes, _cNoTarget, _cNoLuck, _cDrift;
        private static int _sCalls, _sHeroes;                                 // w cieniu (klucz wylaczony): wywolania BK i bohaterowie, ktorych BK zamieni
        private static int _vTimes, _vMen;                                    // ochotnicy z mapy gry u klanow najemnikow (tylko licznik)
        private static long _cTicks;
        private static readonly Dictionary<string, int[]> _cBy = new Dictionary<string, int[]>();   // klan -> wywolan / zolnierzy zamienionych / bohaterow zostawionych

        private static int[] Row(Clan c)
        {
            string id = c.StringId ?? "?";
            int[] a;
            if (!_cBy.TryGetValue(id, out a)) { a = new int[3]; _cBy[id] = a; }
            return a;
        }

        private static int HeroesIn(Clan c)
        {
            int n = 0;
            var w = c.WarPartyComponents;
            if (w == null) return 0;
            foreach (var wp in w)
            {
                var mp = wp != null ? wp.MobileParty : null;
                if (mp != null && mp.MemberRoster != null) n += mp.MemberRoster.TotalHeroes;
            }
            return n;
        }

        /// <summary>Druzyny klanu z wolnym miejscem - tyle dosypek po 3-5 ludzi zrobilaby galaz BK "bez krolestwa".</summary>
        private static int Roomy(Clan c)
        {
            int n = 0;
            var w = c.WarPartyComponents;
            if (w == null) return 0;
            foreach (var wp in w)
            {
                var mp = wp != null ? wp.MobileParty : null;
                if (mp == null || mp.MemberRoster == null || mp.Party == null) continue;
                if (mp.Party.PartySizeLimit - mp.MemberRoster.TotalManCount > 0) n++;
            }
            return n;
        }

        private static float Chance(object bk, CharacterObject to, CharacterObject troop)
        {
            var m = _mChance;
            if (m == null || bk == null || !m.DeclaringType.IsInstanceOfType(bk)) return 1f;   // BK dzis: stale 1 (brak metody widac w linii startowej)
            _chanceArgs[0] = to; _chanceArgs[1] = troop;
            try { return (float)m.Invoke(bk, _chanceArgs); }
            finally { _chanceArgs[0] = null; _chanceArgs[1] = null; }
        }

        /// <summary>
        /// Z SpoilsCompany.BkConvertPrefix dla klanu innego niz klan Spoils (po Stage8World.MercBefore - pomiar E8/P3 obejmuje takze nasza zamiane).
        /// Zwraca wynik prefiksu: true = metoda BK biegnie jak dotad (klan nie jest klanem najemnikow albo klucz wylaczony), false = zrobione tutaj.
        /// </summary>
        internal static bool ConvertClan(object bk, Clan clan)
        {
            bool mine = false;
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                if (clan == null || !clan.IsClanTypeMercenary) return true;      // BK wychodzi sam w pierwszej linii
                bool inK = clan.Kingdom != null;
                if (!MercOn)
                {
                    _sCalls++;
                    if (inK) _sHeroes += HeroesIn(clan);                         // szansa BK = 1: kazdy bohater druzyn klanu zostanie zamieniony
                    return true;
                }
                mine = true;                                                     // od tej chwili metoda BK nie biegnie - takze po wyjatku (nic w polowie)
                _cCalls++;
                var row = Row(clan);
                row[0]++;
                _tpl.Clear();
                var tpl = clan.DefaultPartyTemplate;
                var stacks = tpl != null ? tpl.Stacks : null;
                if (stacks != null)
                    foreach (var st in stacks)
                        if (st.Character != null) _tpl.Add(st.Character);
                if (_tpl.Count == 0) { _cNoTpl++; return false; }                // BK: bez szablonu nic nie robi
                if (!inK)
                {
                    _cFree++; _cFreeParties += Roomy(clan);                      // galaz BK "bez krolestwa": 3-5 ludzi szablonu na druzyne z niczego - nie zachodzi
                    return false;
                }
                _cKingdom++;
                _parties.Clear();
                var w = clan.WarPartyComponents;
                if (w != null) foreach (var wp in w) _parties.Add(wp);
                for (int p = 0; p < _parties.Count; p++)
                {
                    var mp = _parties[p] != null ? _parties[p].MobileParty : null;
                    var roster = mp != null ? mp.MemberRoster : null;
                    if (roster == null) continue;
                    int before = roster.TotalManCount;
                    _work.Clear();
                    for (int i = 0; i < roster.Count; i++) _work.Add(roster.GetElementCopyAtIndex(i));   // kopia wpisow - jak BK (GetTroopRoster().ToList())
                    for (int i = 0; i < _work.Count; i++)
                    {
                        var troop = _work[i].Character;
                        if (troop == null || _work[i].Number < 1 || _tpl.Contains(troop)) continue;
                        if (troop.IsHero) { _cHeroes++; row[2]++; continue; }   // E8/G2: bohater zostaje w druzynie (BK: wypada, a w jego miejsce zolnierz z niczego)
                        CharacterObject to = null;
                        for (int k = 0; k < _tpl.Count; k++)
                            if (_tpl[k].Level == troop.Level) { to = _tpl[k]; break; }
                        if (to == null) to = clan.BasicTroop;
                        if (ReferenceEquals(to, troop)) continue;                // BK: zdejmuje i dodaje tego samego - bez skutku
                        if (to == null || to.IsHero) { _cNoTarget++; continue; } // bez wzorca nie zdejmujemy czlowieka (BK wywrocilby sie po zdjeciu)
                        if (!(MBRandom.RandomFloat < Chance(bk, to, troop))) { _cNoLuck++; continue; }
                        roster.AddToCounts(troop, -1);
                        roster.AddToCounts(to, 1);
                        _cSwapped++; row[1]++;
                    }
                    int after = roster.TotalManCount;
                    if (after != before) _cDrift += Math.Abs(after - before);    // kontrola: zamiana 1:1 nie zmienia liczby ludzi (prog 0)
                }
                return false;
            }
            catch (Exception e) { Stumble("Convert", e); return !mine; }
            finally
            {
                _tpl.Clear(); _parties.Clear(); _work.Clear();
                _cTicks += Stopwatch.GetTimestamp() - t0;
            }
        }

        /// <summary>Z SpoilsCompany.RecruitPrefix (RecruitmentCampaignBehavior.ApplyInternal) dla partii spoza klanu Spoils: ochotnicy z mapy gry u klanu
        /// najemnikow. Tylko licznik - niczego nie zmienia.</summary>
        internal static void NoteRecruit(MobileParty party, int number, object detail)
        {
            try
            {
                var c = party != null ? party.ActualClan : null;
                if (c == null || number <= 0 || !c.IsClanTypeMercenary) return;
                if (!(detail is RecruitmentCampaignBehavior.RecruitingDetail) || (RecruitmentCampaignBehavior.RecruitingDetail)detail != RecruitmentCampaignBehavior.RecruitingDetail.VolunteerFromMap) return;
                _vTimes++; _vMen += number;
            }
            catch (Exception e) { Stumble("NoteRecruit", e); }
        }

        // ------------------------------------------------------------ 2. uczta BK: towar z rynku (wolane z BkDipGold.FeastPre / FeastFin - okno na Feast.Automanage)

        /// <summary>Stan rynku i spizarni uczty sprzed zakupow BK - tylko zywnosc, wino i piwo (to samo sito co petla BK).</summary>
        internal sealed class Snap
        {
            public ItemRoster Market, Stash;
            public string Town;
            public readonly List<EquipmentElement> El = new List<EquipmentElement>(24);
            public readonly List<int> M0 = new List<int>(24), S0 = new List<int>(24);
        }

        private static int _gCalls, _gBuys, _gNoRoster, _gOdd, _gShort;
        private static long _gBought, _gBkAdded, _gTaken, _gFree, _gLost, _gTicks;
        private static readonly Dictionary<string, int> _gAt = new Dictionary<string, int>();

        private static bool FeastGood(ItemObject it)
        {
            if (it == null) return false;
            if (it.IsFood) return true;
            var id = it.StringId;
            return id == "wine" || id == "beer";                                 // BK Feast.Automanage: item.IsFood || wine || beer
        }

        private static int Amount(ItemRoster r, EquipmentElement el)
        {
            int i = r.FindIndexOfElement(el);
            return i >= 0 ? r.GetElementNumber(i) : 0;
        }

        /// <summary>Prefiks okna uczty: stan rynku miasta i spizarni uczty przed zakupami. Tylko odczyt.</summary>
        internal static Snap FeastBefore(Town t)
        {
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                if (t == null) return null;
                var owner = t.Owner;
                var market = owner != null ? owner.ItemRoster : null;
                var stash = t.Settlement != null ? t.Settlement.Stash : null;
                if (market == null || stash == null || ReferenceEquals(market, stash)) { _gNoRoster++; return null; }
                var g = new Snap { Market = market, Stash = stash, Town = t.Name != null ? t.Name.ToString() : "?" };
                for (int i = 0; i < market.Count; i++)
                {
                    var el = market.GetElementCopyAtIndex(i);
                    if (el.Amount <= 0 || !FeastGood(el.EquipmentElement.Item)) continue;
                    g.El.Add(el.EquipmentElement); g.M0.Add(el.Amount); g.S0.Add(Amount(stash, el.EquipmentElement));
                }
                return g;
            }
            catch (Exception e) { Stumble("FeastBefore", e); return null; }
            finally { _gTicks += Stopwatch.GetTimestamp() - t0; }
        }

        /// <summary>Finalizer okna uczty: rynek = stan sprzed zakupu minus to, co przybylo spizarni (BK dopisuje rynkowi +n zamiast -n). Nigdy niczego nie doklada.</summary>
        internal static void FeastAfter(Snap g)
        {
            if (g == null) return;
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                bool on = GoodsOn;
                _gCalls++;
                long bought = 0;
                for (int i = 0; i < g.El.Count; i++)
                {
                    var el = g.El[i];
                    int m0 = g.M0[i];
                    int dS = Amount(g.Stash, el) - g.S0[i];                      // tyle naprawde trafilo do spizarni
                    int m1 = Amount(g.Market, el);
                    if (dS <= 0)
                    {
                        if (m1 != m0) _gOdd++;                                   // rynek zmieniony bez zakupu do spizarni - nie nasze, nie ruszamy
                        continue;
                    }
                    bought += dS;
                    if (m1 > m0) _gBkAdded += m1 - m0;                           // BK dopisal rynkowi z niczego
                    int want = m0 - dS;                                          // rynek po prawdziwym zakupie
                    if (want < 0) { _gShort++; want = 0; }
                    if (on && m1 > want)
                    {
                        g.Market.AddToCounts(el, want - m1);
                        _gTaken += m1 - want;
                        m1 = Amount(g.Market, el);
                    }
                    int net = (m1 - m0) + dS;                                    // przyrost sztuk swiata po calosci: 0 = zakup, > 0 = z niczego
                    if (net > 0) _gFree += net; else if (net < 0) _gLost += -net;
                }
                _gBought += bought;
                if (bought > 0)
                {
                    _gBuys++;
                    int n; _gAt.TryGetValue(g.Town, out n); _gAt[g.Town] = n + 1;
                }
            }
            catch (Exception e) { Stumble("FeastAfter", e); }
            finally { _gTicks += Stopwatch.GetTimestamp() - t0; }
        }

        // ------------------------------------------------------------ stan

        /// <summary>Nowa kampania i kazde wczytanie (WorldMeasure.Reset - konstruktor ArmouryBehavior).</summary>
        internal static void Reset()
        {
            _stumbles = 0; _errSites.Clear();
            _tpl.Clear(); _parties.Clear(); _work.Clear();
            ClearDay();
        }

        private static void ClearDay()
        {
            _stumblesDay = 0;
            _cCalls = _cKingdom = _cFree = _cFreeParties = _cNoTpl = _cSwapped = _cHeroes = _cNoTarget = _cNoLuck = _cDrift = 0;
            _sCalls = _sHeroes = 0; _vTimes = _vMen = 0; _cTicks = 0; _cBy.Clear();
            _gCalls = _gBuys = _gNoRoster = _gOdd = _gShort = 0;
            _gBought = _gBkAdded = _gTaken = _gFree = _gLost = _gTicks = 0; _gAt.Clear();
        }

        // ------------------------------------------------------------ takt doby

        /// <summary>Raz na dobe (ArmouryBehavior.OnDailyTick, po CaravanFlow.Daily): linia "Najemnicy i uczta BK (E8/G2)". Tylko log.</summary>
        internal static void Daily()
        {
            var s = Settings.Current;
            if (s == null || Campaign.Current == null) return;
            long t0 = Stopwatch.GetTimestamp();
            int day = (int)CampaignTime.Now.ToDays - 1;
            try
            {
                int clans = 0, inK = 0, parties = 0, partiesK = 0, men = 0, heroes = 0, heroesK = 0, lead = 0, leadK = 0;
                var all = Clan.All;
                if (all != null)
                    foreach (var c in all)
                    {
                        try
                        {
                            if (c == null || c.IsEliminated || !c.IsClanTypeMercenary || SpoilsCompany.IsSpoilsClan(c)) continue;
                            bool k = c.Kingdom != null;
                            clans++; if (k) inK++;
                            var w = c.WarPartyComponents;
                            if (w != null)
                                foreach (var wp in w)
                                {
                                    var mp = wp != null ? wp.MobileParty : null;
                                    var r = mp != null ? mp.MemberRoster : null;
                                    if (r == null) continue;
                                    parties++; men += r.TotalManCount; heroes += r.TotalHeroes;
                                    if (k) { partiesK++; heroesK += r.TotalHeroes; }
                                }
                            var l = c.Leader;
                            var lp = l != null ? l.PartyBelongedTo : null;
                            if (lp != null && lp.LeaderHero == l) { lead++; if (k) leadK++; }
                        }
                        catch (Exception e) { Stumble("Daily(klan)", e); }
                    }
                var sb = new StringBuilder(1200);
                sb.Append("Najemnicy i uczta BK (E8/G2): dzien ").Append(day)
                  .Append(" | najemnicy (Merc Clans Real Soldiers ").Append(s.MercClansRealSoldiers ? "CZYNNE" : "WYLACZONE").Append("): klany najemnikow (bez klanu Spoils) ").Append(clans)
                  .Append(", w sluzbie krolestwa ").Append(inK).Append(", bez sluzby ").Append(clans - inK).Append("; druzyn ").Append(parties).Append(" (w sluzbie ").Append(partiesK)
                  .Append("), ludzi ").Append(men).Append(", bohaterow w druzynach ").Append(heroes).Append(" (w sluzbie ").Append(heroesK)
                  .Append("), wodzow prowadzacych druzyne ").Append(lead).Append(" z ").Append(clans).Append(" (w sluzbie ").Append(leadK).Append(" z ").Append(inK).Append(')')
                  .Append(" | zamiana BK ConvertTroopsMercenaries od poprzedniej linii: nasza zamiast BK ").Append(_cCalls).Append(" razy (w sluzbie ").Append(_cKingdom)
                  .Append(", bez sluzby ").Append(_cFree).Append("), zolnierzy zamienionych 1:1 na szablon klanu ").Append(_cSwapped)
                  .Append(", bohaterow zostawionych w druzynach ").Append(_cHeroes).Append(" (BK zamienilby kazdego na zolnierza z niczego)")
                  .Append(", dosypka BK 3-5 ludzi bez sluzby niezaszla w ").Append(_cFreeParties).Append(" druzynach")
                  .Append(", bez szablonu ").Append(_cNoTpl).Append(", bez wzorca zamiany ").Append(_cNoTarget).Append(", szansa BK ponizej 1: pominiete ").Append(_cNoLuck)
                  .Append(", rozjazd liczby ludzi ").Append(_cDrift)
                  .Append("; w cieniu (klucz wylaczony - BK jak dotad): wywolan ").Append(_sCalls).Append(", bohaterow do zamiany ").Append(_sHeroes)
                  .Append(" | wedlug klanu (wywolan/zamienionych/bohaterow):");
                if (_cBy.Count == 0) sb.Append(" -");
                else
                {
                    var ids = new List<string>(_cBy.Keys);
                    ids.Sort(string.CompareOrdinal);
                    for (int i = 0; i < ids.Count && i < 12; i++)
                    {
                        var a = _cBy[ids[i]];
                        sb.Append(i > 0 ? "," : "").Append(' ').Append(ids[i]).Append(' ').Append(a[0]).Append('/').Append(a[1]).Append('/').Append(a[2]);
                    }
                }
                sb.Append(" | ochotnicy z mapy gry u klanow najemnikow (poza paczka - tylko licznik): ").Append(_vTimes).Append(" razy / ").Append(_vMen).Append(" ludzi")
                  .Append(" | uczta (Feast No Free Goods ").Append(s.FeastNoFreeGoods ? "CZYNNE" : "WYLACZONE").Append("): zakupy BK Automanage ").Append(_gCalls)
                  .Append(" razy (z towarem ").Append(_gBuys).Append("), do spizarni ").Append(_gBought).Append(" szt., BK dopisal rynkowi ").Append(_gBkAdded)
                  .Append(" szt. z niczego, zdjete z rynku ").Append(_gTaken).Append(" szt., towar z niczego po calosci ").Append(_gFree).Append(" szt.")
                  .Append(", rynek stracil ponad zakup ").Append(_gLost).Append(" szt., rynek zmieniony bez zakupu ").Append(_gOdd)
                  .Append(", rynek mial mniej niz kupiono ").Append(_gShort).Append(", bez rynku albo spizarni ").Append(_gNoRoster);
                if (_gAt.Count > 0)
                {
                    var ks = new List<string>(_gAt.Keys);
                    ks.Sort(string.CompareOrdinal);
                    sb.Append(" [");
                    for (int i = 0; i < ks.Count && i < 6; i++) sb.Append(i > 0 ? ", " : "").Append(ks[i]).Append(' ').Append(_gAt[ks[i]]);
                    sb.Append(ks.Count > 6 ? ", ...]" : "]");
                }
                sb.Append(" | latki wpiete ").Append(_wiredN).Append('/').Append(_wantedN)
                  .Append(_missing.Count > 0 ? " (BRAK: " + string.Join(", ", _missing.ToArray()) + ")" : "")
                  .Append(" | koszt ").Append(((_cTicks + _gTicks + Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency).ToString("0.00", Inv)).Append(" ms/dobe")
                  .Append(" | potkniecia dzis ").Append(_stumblesDay).Append(" (od wczytania ").Append(_stumbles).Append(").");
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally { ClearDay(); }
        }

        // ------------------------------------------------------------ start gry (SubModuleMain - po SpoilsCompany, BkDipGold i CaravanFlow)

        private static void Want(string label, bool ok)
        {
            _wantedN++;
            if (ok) { _wired.Add(label); _wiredN++; } else _missing.Add(label);
        }

        /// <summary>Nowych latek nie zaklada: obie zmiany siedza w istniejacych oknach (prefiks SpoilsCompany na BK ConvertTroopsMercenaries, prefiks i finalizer
        /// BkDipGold na BK Feast.Automanage). Tu tylko odczyt metody szansy BK, kontrola tresci metody BK i linia "wpiete N/M" (R11).</summary>
        internal static void ApplyAll(Harmony h)
        {
            _wired.Clear(); _missing.Clear(); _wiredN = 0; _wantedN = 0; _mChance = null; _bodyText = "nie sprawdzono";
            try
            {
                var tBk = QuartermasterLaw.FindType("BannerKings.Behaviours.BKClanBehavior");
                Want("zamiana BK klanow najemnikow (prefiks SpoilsCompany na BKClanBehavior.ConvertTroopsMercenaries)", SpoilsCompany.BkConvertWired);
                var ch = tBk != null ? AccessTools.Method(tBk, "MercenaryConversionChance", new[] { typeof(CharacterObject), typeof(CharacterObject) }) : null;
                if (ch != null && (ch.IsStatic || ch.ReturnType != typeof(float))) ch = null;
                _mChance = ch;
                Want("szansa zamiany BK (BKClanBehavior.MercenaryConversionChance)", ch != null);
                Want("okno uczty (prefiks i finalizer BkDipGold na Feast.Automanage)", BkDipGold.FeastWired);
                var conv = tBk != null ? AccessTools.Method(tBk, "ConvertTroopsMercenaries", new[] { typeof(Clan) }) : null;
                var body = conv != null ? conv.GetMethodBody() : null;
                var il = body != null ? body.GetILAsByteArray() : null;
                _bodyText = il == null ? "brak metody" : il.Length == BkConvertIl ? "IL " + il.Length + " bajtow - zgodna ze sprawdzona" : "IL " + il.Length + " bajtow - INNA niz sprawdzona (" + BkConvertIl + "): porownac dekompilacje BK";
            }
            catch (Exception e) { Log.Error("MercFeast.ApplyAll", e); }
            Log.Info("Najemnicy i uczta BK (E8/G2): latki przy starcie gry - wpiete " + _wiredN + "/" + _wantedN + ": " + (_wired.Count > 0 ? string.Join(", ", _wired.ToArray()) : "-")
                     + "; BRAK: " + (_missing.Count > 0 ? string.Join(", ", _missing.ToArray()) : "-")
                     + " | tresc BK ConvertTroopsMercenaries: " + _bodyText
                     + " | Merc Clans Real Soldiers " + (MercOn ? "CZYNNE" : "wylaczone") + " (klan najemnikow: bohater zostaje w druzynie, zolnierze spoza szablonu zamieniani 1:1 jak w BK, nikt z niczego)"
                     + ", Feast No Free Goods " + (GoodsOn ? "CZYNNE" : "wylaczone") + " (zakup uczty BK schodzi z rynku miasta zamiast go powiekszac).");
        }
    }
}
