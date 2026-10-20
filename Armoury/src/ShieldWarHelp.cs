using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// R3-09a TARCZOWNICY: POMOC CIESLI W CZASIE WOJNY (Jeff 20.10 na liste 35 pomyslow, docs/POMYSLY-35-2026-10-20.md poz. 27: "28 - nie, 29 - nie.
    /// Pozostale tak"). Zrodlo: docs/PROJEKT-174-PRODUKCJA-UZBROJENIA-2026-10-09.md:1032 ("Tarczownicy 5% - jesli po 120 dobach pokrycie tarcz partii AI
    /// &lt; 35% (szczebel): wojenna pomoc ciesli jako nowy klucz wagi"), PLAN-DO-KONCA-MODA 6.8, ROZPOZNANIE-TOWARY-2 A7-5 (14.10: 36% - bez zmian).
    ///
    /// Regula (warunkowa - bez braku nic sie nie zmienia):
    ///  - POMIAR co 5 dob (doby podzielne przez 5 jak linia 171 i pierwsza doba sesji): miejsca na tarcze w zestawach zolnierzy partii rodow AI (te same partie i ta sama miara co tarcza
    ///    w linii "Pokrycie zbrojowni AI (171)": AiGear.Deficit - potrzeba wedlug wzorca do sufitu szczebla, brak po zbrojowni DTE i zastepstwie tierow),
    ///    osobno dla kazdego krolestwa (partie wedlug ActualClan.Kingdom) i dla swiata. Pokrycie = (potrzeba - brak) / potrzeba.
    ///  - DECYZJA raz na dobe dla kazdego krolestwa: w wojnie (KingdomTreasury.AtWar - ta sama miara wojny co danina wojenna i zwrot zoldu) i przy
    ///    pokryciu ponizej ShieldWarCoverageBelow (35%) ciesle miast tego krolestwa pomagaja tarczownikom. Krolestwo z mala potrzeba (ponizej
    ///    MinSlots miejsc na tarcze) - wedlug swiata (mala probka nie decyduje sama). Miasto rodu spoza krolestwa - wojna z jakimkolwiek krolestwem
    ///    i pokrycie swiata. Bez pomiaru (zbrojownie DTE niedostepne, swiat bez potrzeby) - bez pomocy.
    ///  - SKUTEK (WorkshopLaw.BuildPlan, plan rak warsztatu na dobe): cech tarczownikow czynnego warsztatu dostaje do rak wlasnych (rece warsztatu x waga
    ///    cechu 5%) jeszcze ShieldWarCarpenterHands (1) tyle samo rak ciesli. Rece ciesli sa POZA rekami rzemieslnikow broni (nikomu ich nie zabieraja -
    ///    kowale, naprawy i pozostale cechy bez zmian). Pomagaja tylko, gdy cech ma robote (linia czynna: surowiec, zysk i kapital jak dotad).
    ///  - Zamknieta ekonomia: nowego pieniadza ani towaru nie ma. Sztuka placi place za swoje dni pracy jak dotad (kapital warsztatu -> kasa miasta przy
    ///    oddaniu sztuki - ciesle sa oplaceni z tej samej placy), drewno, skora i zelazo z polek miasta jak dotad, gotowa tarcza idzie na targ tylko, gdy
    ///    miasto zaplaci z nadwyzki ponad zapas kupcow. Wiecej rak = szybciej skonczona sztuka, nie sztuka z niczego.
    /// Bez zapisu: pomiar i decyzja licza sie od nowa w pierwszej dobie po wczytaniu (stan czyszczony przy zmianie kampanii - porownanie Campaign.Current).
    /// Watek glowny (tick dobowy warsztatow miast), bez alokacji w planie warsztatu (slownik decyzji doby).
    /// Linia logu "Tarcze w wojnie (R3-09a): dzien" - pomiar, decyzja dla krolestw w wojnie, rece ciesli dnia poprzedniego.
    /// </summary>
    internal static class ShieldWarHelp
    {
        private const int MeasureEveryDays = 5;
        private const long MinSlots = 50;   // krolestwo z mniejsza potrzeba tarcz w partiach AI - wedlug swiata
        private const string Guild = "tarczownik";

        private static Campaign _campaign;
        private static int _decDay = -1, _measDay = -1;
        private static bool _measOk;
        private static long _wNeed, _wLack, _measMs;
        private static int _wParties;
        private static readonly Dictionary<IFaction, long[]> _cov = new Dictionary<IFaction, long[]>();   // krolestwo -> [potrzeba, brak]
        private static readonly Dictionary<IFaction, bool> _help = new Dictionary<IFaction, bool>();       // decyzja doby wedlug frakcji mapy miasta
        private static readonly Dictionary<int, int> _needOut = new Dictionary<int, int>();
        private static readonly HashSet<Town> _dTowns = new HashSet<Town>();
        private static float _dCarp, _dOwn;
        private static int _dShops, _stumbles;
        private static bool _errLogged;

        /// <summary>Cech tarczownikow (WorkshopLaw.GuildOf).</summary>
        internal static bool IsShieldGuild(string g) { return g == Guild; }

        /// <summary>
        /// Rece ciesli dla cechu tarczownikow warsztatu w miescie town na dzis (0 - bez pomocy). own - wlasne rece cechu (rece warsztatu x waga cechu).
        /// Wolane z WorkshopLaw.BuildPlan (do 3 rund planu) - bez licznikow; liczy Note po planie.
        /// </summary>
        internal static float ExtraHands(Town town, float own)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.ShieldWarCarpenters || town == null || own <= 0f) return 0f;
                float mult = MBMath.ClampFloat(s.ShieldWarCarpenterHands, 0f, 4f);
                if (mult <= 0f) return 0f;
                int day = (int)CampaignTime.Now.ToDays;
                if (day != _decDay || _campaign != Campaign.Current) NewDay(day);
                var f = town.Settlement != null ? town.Settlement.MapFaction : null;
                if (f == null) return 0f;
                bool h;
                if (!_help.TryGetValue(f, out h)) { h = Decide(f, Settings.Current); _help[f] = h; }
                return h ? own * mult : 0f;
            }
            catch (Exception e) { Stumble("ExtraHands", e); return 0f; }
        }

        /// <summary>Licznik doby: rece ciesli przydzielone w planie warsztatu (raz na plan, po ostatniej rundzie).</summary>
        internal static void Note(Town town, float carp, float own)
        {
            if (carp <= 0f) return;
            _dCarp += carp; _dOwn += own; _dShops++;
            if (town != null) _dTowns.Add(town);
        }

        // ------------------------------------------------------------ doba: linia za wczoraj, pomiar, decyzje
        private static void NewDay(int day)
        {
            if (_campaign != Campaign.Current)
            {
                _campaign = Campaign.Current;
                _decDay = _measDay = -1; _measOk = false; _cov.Clear(); _help.Clear();
                ZeroDay(); _stumbles = 0; _errLogged = false;
            }
            int prevDec = _decDay;
            _decDay = day;   // od razu - wyjatek nizej nie moze powtarzac pomiaru przy kazdym planie tej doby
            var s = Settings.Current;
            // co 5 dob w te same doby co linia "Pokrycie zbrojowni AI (171)" (dzien podzielny przez 5 - do porownania w logu) i w pierwszej dobie sesji
            if (_measDay < 0 || day < _measDay || day - _measDay >= MeasureEveryDays || (day % MeasureEveryDays == 0 && day != _measDay))
                try { Measure(day); } catch (Exception e) { Stumble("Measure", e); _measOk = false; }
            _help.Clear();
            int warK = 0, helpK = 0;
            var sb = new StringBuilder();
            var inv = CultureInfo.InvariantCulture;
            try
            {
                foreach (var k in Kingdom.All)
                {
                    if (k == null || k.IsEliminated) continue;
                    bool war = KingdomTreasury.AtWar(k);
                    bool h = war && Below(Coverage(k), s);
                    _help[k] = h;
                    if (!war) continue;
                    warK++; if (h) helpK++;
                    long[] a; bool own = _cov.TryGetValue(k, out a) && a[0] >= MinSlots;
                    if (sb.Length > 0) sb.Append(", ");
                    sb.Append(k.Name != null ? k.Name.ToString() : k.StringId).Append(' ')
                      .Append(own ? Pct(a[0], a[1]) + " (" + a[0] + ")" : "wedlug swiata (" + (a != null ? a[0] : 0) + ")")
                      .Append(h ? " POMOC" : " bez");
                }
            }
            catch (Exception e) { Stumble("Decide", e); }
            float thr = Threshold(s);
            try
            {
                Log.Info("Tarcze w wojnie (R3-09a): dzien " + day + " | CZYNNE, prog pokrycia "
                         + (100f * thr).ToString("0", inv) + "%, ciesle x" + MBMath.ClampFloat(s.ShieldWarCarpenterHands, 0f, 4f).ToString("0.00", inv) + " rak tarczownikow"
                         + " | pomiar z dnia " + _measDay + (_measOk ? ": swiat " + Pct(_wNeed, _wLack) + " (miejsc na tarcze " + _wNeed + ", brak " + _wLack + ", partii " + _wParties + ", " + _measMs + " ms)" : ": BRAK (zbrojownie DTE niedostepne albo swiat bez potrzeby tarcz) - bez pomocy")
                         + " | krolestwa w wojnie " + warK + ", z pomoca ciesli " + helpK + (sb.Length > 0 ? " [" + sb + "]" : "")
                         + " | poprzednia doba decyzji (dzien " + (prevDec >= 0 ? prevDec.ToString() : "-") + "): ciesle przy tarczach w " + _dTowns.Count + " miastach / " + _dShops + " warsztatach, "
                         + _dCarp.ToString("0", inv) + " roboczodni (wlasne rece tarczownikow tam " + _dOwn.ToString("0", inv) + ")"
                         + " | potkniecia " + _stumbles + ".");
            }
            catch (Exception e) { Stumble("Line", e); }
            ZeroDay(); _stumbles = 0;
        }

        private static void ZeroDay() { _dCarp = _dOwn = 0f; _dShops = 0; _dTowns.Clear(); }

        /// <summary>Decyzja dla frakcji mapy spoza Kingdom.All (rod bez krolestwa): wojna z jakimkolwiek krolestwem i pokrycie swiata.</summary>
        private static bool Decide(IFaction f, Settings s)
        {
            var k = f as Kingdom;
            if (k != null) return !k.IsEliminated && KingdomTreasury.AtWar(k) && Below(Coverage(k), s);
            bool war = false;
            var list = f.FactionsAtWarWith;
            if (list != null) for (int i = 0; i < list.Count; i++) { var o = list[i]; if (o != null && o != f && o.IsKingdomFaction && !o.IsEliminated) { war = true; break; } }
            return war && Below(WorldCoverage(), s);
        }

        private static float Threshold(Settings s) { return MBMath.ClampFloat(s != null ? s.ShieldWarCoverageBelow : 0.35f, 0f, 1f); }
        private static bool Below(double cov, Settings s) { return cov >= 0.0 && cov < Threshold(s); }

        /// <summary>Pokrycie tarcz krolestwa (0-1; -1 = bez pomiaru): wlasne przy potrzebie >= MinSlots, inaczej swiata.</summary>
        private static double Coverage(Kingdom k)
        {
            if (!_measOk) return -1.0;
            long[] a;
            if (k != null && _cov.TryGetValue(k, out a) && a[0] >= MinSlots) return (double)(a[0] - a[1]) / a[0];
            return WorldCoverage();
        }

        private static double WorldCoverage() { return _measOk && _wNeed > 0 ? (double)(_wNeed - _wLack) / _wNeed : -1.0; }

        private static string Pct(long need, long lack) { return need > 0 ? (100.0 * (need - lack) / need).ToString("0.0", CultureInfo.InvariantCulture) + "%" : "-"; }

        /// <summary>Pomiar: miejsca na tarcze w partiach rodow AI (filtry jak ArmsDrill.Coverage) - potrzeba i brak wedlug krolestwa i swiata.</summary>
        private static void Measure(int day)
        {
            _measDay = day; _measOk = false; _cov.Clear(); _wNeed = _wLack = 0; _wParties = 0; _measMs = 0;
            var arms = AiGear.Armories();
            if (arms == null) return;
            var sw = Stopwatch.StartNew();
            int shield = (int)ItemObject.ItemTypeEnum.Shield;
            foreach (var mp in MobileParty.AllLordParties)
            {
                try
                {
                    if (mp == null || !mp.IsActive || mp.LeaderHero == null || mp == MobileParty.MainParty || mp.ActualClan == Clan.PlayerClan || Undead.Party(mp) || mp.MemberRoster == null) continue;
                    Dictionary<ItemObject, int> arm;
                    arms.TryGetValue(mp.Id, out arm);
                    _needOut.Clear();
                    var lack = AiGear.Deficit(mp, arm, _needOut);
                    long n = 0, l = 0;
                    foreach (var kv in _needOut) if (kv.Key / 10 == shield && kv.Value > 0) n += kv.Value;
                    if (n <= 0) continue;
                    foreach (var kv in lack) if (kv.Key / 10 == shield && kv.Value > 0) l += kv.Value;
                    if (l > n) l = n;
                    _wNeed += n; _wLack += l; _wParties++;
                    var k = mp.ActualClan != null ? mp.ActualClan.Kingdom : null;
                    if (k == null) continue;
                    long[] a;
                    if (!_cov.TryGetValue(k, out a)) { a = new long[2]; _cov[k] = a; }
                    a[0] += n; a[1] += l;
                }
                catch (Exception e) { Stumble("Measure(partia)", e); }
            }
            sw.Stop();
            _measMs = sw.ElapsedMilliseconds;
            _measOk = _wNeed > 0;
        }

        // licz potkniecia, nie gas reguly (CLAUDE.md rozdz. 7); pierwszy blad sesji z pelnym sladem
        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_errLogged) return;
            _errLogged = true;
            try { Log.Error("ShieldWarHelp." + where, e); } catch { }
        }
    }
}
