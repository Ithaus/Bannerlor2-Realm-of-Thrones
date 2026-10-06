using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// KSIEGA LUDZI - sam log (krok 1 demografii, docs/DEMOGRAFIA-SILA-ROBOCZA-2026-10-05.md rozdz. 5.2; nazwa ManLedger jest zajeta).
    /// Niczego nie zmienia w grze i nie ma stanu w zapisie: odczyt stanu raz na dobe i nasluch zdarzen gry.
    /// Region = miasto albo zamek + jego wsie (ta sama geografia co pula wyrzutkow, OutlawLaw); banda nalezy do regionu
    /// najblizszej warowni, zabici - do regionu, w ktorym stoczono walke; werbunek - do regionu osady notabla albo karczmy.
    ///
    /// Raz na dobe:
    ///  "Ludzie:" - swiat: ludnosc z PopulationLaw.PeopleOf i jej zmiana dobowa, zolnierze wedlug rodzaju partii (partie rodow,
    ///    garnizony, milicje, bandy, karawany, tabory wsi), jency, pula wyrzutkow; zabici dzis (wszystkie starcia, wedlug rodzaju
    ///    partii), zwerbowani dzis (zdarzenia gry: od notabli, z karczmy, bez osady, gracz), dezercja; bilans partii rodow
    ///    z jawna reszta (nowe partie z szablonu, wymiana z garnizonem); odsetek mezczyzn pod bronia; od kroku 2 takze
    ///    "hearth za ludzi dzis" - ile hearth wsie oddaly za tabory, wyrzutkow i wymuszonych rekrutow (liczniki PeopleUnit);
    ///  "Ludzie: przyrost naturalny" (krok 3, PopulationLaw.GrowthDaily) - ile ludzi przybywa wsiom dzis i wedle jakich skladowych,
    ///    krainy, rozliczenie zmiany hearth wsi od wczoraj (reszta = hearth z niczego), stan ludnosci miast wobec dobrobytu;
    ///  "Ludzie (spustoszenie):" (krok 4, Devastation.RegionsNote) - rabunki i zerowanie doby w osobodniach i ludziach, powroty
    ///    uchodzcow, 8 regionow najbardziej spustoszonych; w samej linii "Ludzie:" pozycje: zdjeci, zabici, w las, uchodzcy, powroty;
    ///  "Ludzie (regiony):" - 8 regionow najbardziej obciazonych: (zaloga + wyrzutki + bandy) wobec mezczyzn regionu;
    ///  plik Logs/[sesja]/ludzie-regiony.csv - wszystkie regiony, wiersz na region na dobe.
    /// Czego ksiega NIE widzi (do kroku 5): z jakiego regionu pochodzi zolnierz partii rodu; przyrost garnizonow (gra dopisuje
    /// ludzi bez zdarzenia - widac tylko zmiane stanu); werbunek jencow i ochotnikow "z mapy" ma w zdarzeniu te sama postac.
    /// </summary>
    internal static class PeopleLedger
    {
        private const float MenShare = 0.27f;      // mezczyzni 16-60 w ludnosci (SZACUNEK z dokumentu demografii, rozdz. 4.0)
        private const float FreeHands = 0.20f;     // prog wolnych rak: 20% mezczyzn (decyzja Jeffa 05.10)
        private const int Top = 8;
        private const string CsvName = "ludzie-regiony.csv";
        private const string CsvHeader = "dzien;region_id;region;rodzaj;kultura;krolestwo;rod;wsie;wsie_spalone;ludzie;ludzie_zmiana;ludzie_osada;ludzie_wsie;hearth;dobrobyt;"
                                         + "zaloga;milicja;wyrzutki;bandy_partie;bandy_ludzie;zabici_dzis;zwerbowani_dzis;obciazenie_proc_mezczyzn;bezpieczenstwo;zywnosc;"
                                         + "bilans_zywnosci;glod;wojna;uchodzcy;spustoszenie_proc;zabici_spustoszenie";

        // rodzaje partii
        private const int KLord = 0, KGarrison = 1, KMilitia = 2, KBandit = 3, KCaravan = 4, KVillager = 5, KOther = 6, Kinds = 7;
        private static readonly string[] KName = { "partie rodow", "garnizony", "milicje", "bandy", "karawany", "tabory wsi", "inne" };

        private sealed class Row
        {
            public Settlement St;
            public float PeopleNode, PeopleVillages, Hearth, Pool, Burden = -1f;
            public int Villages, Looted, Garrison, Militia, Bands, BandMen;
            public float FoodChange; public bool HasFood;
            public float People { get { return PeopleNode + PeopleVillages; } }
        }

        // stan miedzy dobami (bez zapisu - po wczytaniu pierwsza doba jest bez zmian)
        private static readonly Dictionary<string, float> _lastPeople = new Dictionary<string, float>();
        private static readonly int[] _lastMen = new int[Kinds];
        private static bool _haveLast;
        private static double _lastAway;           // uchodzcy swiata poza domem wczoraj (krok 4)
        private static string _csvPath;

        // liczniki doby
        private static readonly int[] _killed = new int[Kinds];
        private static readonly Dictionary<string, int> _killedIn = new Dictionary<string, int>(), _recruitedIn = new Dictionary<string, int>();
        private static int _fights, _wounded, _routed;
        private static int _rNotableParty, _rNotableOther, _rTavern, _rNoPlace, _rPlayer, _rLed, _desLord, _desOther;
        private static int _stumbles;              // potkniecia nasluchow - liczymy, nie gasimy

        internal static void Reset()
        {
            _lastPeople.Clear(); Array.Clear(_lastMen, 0, Kinds); _haveLast = false; _lastAway = 0.0; _csvPath = null;
            ClearDay();
        }

        private static void ClearDay()
        {
            Array.Clear(_killed, 0, Kinds); _killedIn.Clear(); _recruitedIn.Clear();
            _fights = _wounded = _routed = 0;
            _rNotableParty = _rNotableOther = _rTavern = _rNoPlace = _rPlayer = _rLed = _desLord = _desOther = 0;
            _stumbles = 0;
            PeopleUnit.NewDay();       // liczniki hearth za ludzi (krok 2) zyja w PeopleUnit, doba konczy sie razem z ksiega
            Devastation.NewDay();      // liczniki spustoszenia (krok 4) tak samo
        }

        private static int KindOf(MobileParty mp)
        {
            if (mp == null) return KOther;
            if (mp.IsLordParty) return KLord;
            if (mp.IsGarrison) return KGarrison;
            if (mp.IsMilitia) return KMilitia;
            if (mp.IsBandit) return KBandit;
            if (mp.IsCaravan) return KCaravan;
            if (mp.IsVillager) return KVillager;
            return KOther;
        }

        private static void Add(Dictionary<string, int> d, string key, int n)
        {
            int v; d.TryGetValue(key, out v); d[key] = v + n;
        }

        private static void AddRegion(Dictionary<string, int> d, Settlement place, int n)
        {
            if (place == null || n <= 0) return;
            var region = OutlawLaw.RegionFor(place);
            if (region != null) Add(d, region.StringId, n);
        }

        // ------------------------------------------------------------ nasluch zdarzen gry
        /// <summary>Koniec starcia: zabici wedlug rodzaju partii (wszystkie starcia - kronika "Bitwy:" pomija male) i wedlug regionu.</summary>
        internal static void OnMapEventEnded(MapEvent me)
        {
            try
            {
                if (me == null) return;
                int dead = 0;
                foreach (var side in new[] { me.AttackerSide, me.DefenderSide })
                {
                    if (side == null) continue;
                    foreach (var p in side.Parties)
                    {
                        if (p == null) continue;
                        if (p.WoundedInBattle != null) _wounded += p.WoundedInBattle.TotalManCount;
                        if (p.RoutedInBattle != null) _routed += p.RoutedInBattle.TotalManCount;
                        int d = p.DiedInBattle != null ? p.DiedInBattle.TotalManCount : 0;
                        if (d <= 0) continue;
                        dead += d;
                        _killed[KindOf(p.Party != null ? p.Party.MobileParty : null)] += d;
                    }
                }
                if (dead <= 0) return;
                _fights++;
                var region = OutlawLaw.RegionAt(me.Position.ToVec2());
                if (region != null) Add(_killedIn, region.StringId, dead);
            }
            catch { _stumbles++; }
        }

        /// <summary>Werbunek AI (gra: RecruitmentCampaignBehavior i wcielanie jencow): kto, skad, ilu.</summary>
        internal static void OnTroopRecruited(Hero recruiter, Settlement settlement, Hero source, CharacterObject troop, int amount)
        {
            try
            {
                if (amount <= 0) return;
                if (recruiter != null && recruiter == Hero.MainHero) _rPlayer += amount;
                else if (source != null) { if (recruiter != null) _rNotableParty += amount; else _rNotableOther += amount; }
                else if (settlement != null) _rTavern += amount;
                else _rNoPlace += amount;
                // do bilansu partii rodow tylko werbunek do partii rodu (karawana z towarzyszem na czele tez ma "wodza")
                var rp = recruiter != null ? recruiter.PartyBelongedTo : null;
                if (rp != null && rp.IsLordParty) _rLed += amount;
                AddRegion(_recruitedIn, settlement ?? (source != null ? source.CurrentSettlement : null), amount);
            }
            catch { _stumbles++; }
        }

        /// <summary>Werbunek gracza (ekran werbunku BK, karczma, jency).</summary>
        internal static void OnUnitRecruited(CharacterObject troop, int amount)
        {
            try
            {
                if (amount <= 0) return;
                _rPlayer += amount; _rLed += amount;
                AddRegion(_recruitedIn, Settlement.CurrentSettlement, amount);
            }
            catch { _stumbles++; }
        }

        internal static void OnTroopsDeserted(MobileParty party, TroopRoster roster)
        {
            try
            {
                if (roster == null) return;
                int n = roster.TotalManCount;
                if (party != null && party.IsLordParty) _desLord += n; else _desOther += n;
            }
            catch { _stumbles++; }
        }

        // ------------------------------------------------------------ raz na dobe
        private static string Sg(long n) { return (n >= 0 ? "+" : "") + n; }
        private static string F(float v, string fmt) { return v.ToString(fmt, CultureInfo.InvariantCulture); }
        private static string Clean(string s) { return string.IsNullOrEmpty(s) ? "" : s.Replace(';', ',').Replace('\n', ' ').Replace('\r', ' '); }

        internal static void Daily()
        {
            try
            {
                if (Campaign.Current == null) return;
                int day = (int)CampaignTime.Now.ToDays - 1;
                bool calibrated = PopulationLaw.Calibrated;     // sami kalibracji nie wywolujemy (to stan PopulationLaw w zapisie)

                // 1. regiony: ludzie, zaloga, milicja, pula wyrzutkow
                var rows = new Dictionary<Settlement, Row>();
                var order = new List<Row>();
                foreach (var st in OutlawLaw.RegionNodes())
                {
                    if (st == null || rows.ContainsKey(st)) continue;
                    var r = new Row { St = st };
                    try
                    {
                        if (calibrated) r.PeopleNode = PopulationLaw.PeopleOf(st);
                        r.Militia = (int)st.Militia;
                        var g = st.Town != null ? st.Town.GarrisonParty : null;
                        if (g != null && g.MemberRoster != null) r.Garrison = g.MemberRoster.TotalManCount;
                        if (st.BoundVillages != null)
                            foreach (var v in st.BoundVillages)
                            {
                                if (v == null) continue;
                                r.Villages++; r.Hearth += v.Hearth;
                                if (v.VillageState == Village.VillageStates.Looted || v.VillageState == Village.VillageStates.BeingRaided) r.Looted++;
                                if (v.Settlement != null)
                                {
                                    r.Militia += (int)v.Settlement.Militia;
                                    if (calibrated) r.PeopleVillages += PopulationLaw.PeopleOf(v.Settlement);
                                }
                            }
                        r.Pool = OutlawLaw.PoolIn(st);
                    }
                    catch { _stumbles++; }
                    rows[st] = r; order.Add(r);
                }

                // 2. partie: zolnierze wedlug rodzaju, bandy wedlug regionu
                var men = new int[Kinds]; var count = new int[Kinds];
                int prisoners = 0, dungeons = 0;
                foreach (var mp in MobileParty.All)
                {
                    try
                    {
                        if (mp == null || !mp.IsActive || mp.MemberRoster == null) continue;
                        int n = mp.MemberRoster.TotalManCount;
                        int k = KindOf(mp);
                        men[k] += n; count[k]++;
                        if (mp.PrisonRoster != null) prisoners += mp.PrisonRoster.TotalManCount;
                        if (k != KBandit || n <= 0) continue;
                        var region = OutlawLaw.RegionAt(mp.GetPosition2D);
                        Row r;
                        if (region != null && rows.TryGetValue(region, out r)) { r.Bands++; r.BandMen += n; }
                    }
                    catch { _stumbles++; }
                }
                foreach (var st in Settlement.All)
                    if (st != null && st.Party != null && st.Party.PrisonRoster != null) dungeons += st.Party.PrisonRoster.TotalManCount;

                // 3. swiat i obciazenie regionow
                double people = 0, peopleNodes = 0, lastPeople = 0; float pool = 0f;
                int over = 0, noPeople = 0, starving = 0, foodMinus = 0, fiefs = 0;
                var ranked = new List<Row>();
                foreach (var r in order)
                {
                    people += r.People; peopleNodes += r.PeopleNode; pool += r.Pool;
                    float last;
                    if (_lastPeople.TryGetValue(r.St.StringId, out last)) lastPeople += last;
                    if (r.People > 0f)
                    {
                        r.Burden = (r.Garrison + r.Pool + r.BandMen) / (r.People * MenShare);
                        if (r.Burden > FreeHands) over++;
                        ranked.Add(r);
                    }
                    else noPeople++;
                    try
                    {
                        if (r.St.Town != null)
                        {
                            fiefs++;
                            if (r.St.IsStarving) starving++;
                            r.FoodChange = r.St.Town.FoodChange; r.HasFood = true;      // model zywnosci - raz na warownie na dobe
                            if (r.FoodChange < 0f) foodMinus++;
                        }
                    }
                    catch { }
                }
                ranked.Sort((a, b) => b.Burden.CompareTo(a.Burden));
                double menWorld = people * MenShare;
                double away = 0.0;
                try { away = Devastation.AwayWorld(); } catch { _stumbles++; }
                int killed = 0; foreach (var k in _killed) killed += k;
                int recruited = _rNotableParty + _rNotableOther + _rTavern + _rNoPlace + _rPlayer;
                var sb = new StringBuilder();
                sb.Append("Ludzie: dzien ").Append(day).Append(" | swiat: ");
                if (calibrated)
                {
                    sb.Append("ludnosc ").Append((people / 1e6).ToString("0.000", CultureInfo.InvariantCulture)).Append(" mln");
                    if (_haveLast && lastPeople > 0) sb.Append(" (").Append(Sg((long)Math.Round(people - lastPeople))).Append(" ludzi)");
                    sb.Append(" [PopulationLaw: wsie ").Append(((people - peopleNodes) / 1e6).ToString("0.000", CultureInfo.InvariantCulture))
                      .Append(", miasta ").Append((peopleNodes / 1e6).ToString("0.000", CultureInfo.InvariantCulture)).Append("], mezczyzn 16-60 ok. ")
                      .Append((menWorld / 1e6).ToString("0.000", CultureInfo.InvariantCulture)).Append(" mln (27%)");
                    // krok 4: uchodzcy zeszli z hearth wsi, ale zyja - ksiega pokazuje ich osobno i razem z ludnoscia w domu
                    if (away > 0.0 || _lastAway > 0.0)
                    {
                        sb.Append("; uchodzcy poza domem ").Append(((long)Math.Round(away)).ToString(CultureInfo.InvariantCulture)).Append(", razem z nimi ")
                          .Append(((people + away) / 1e6).ToString("0.000", CultureInfo.InvariantCulture)).Append(" mln");
                        if (_haveLast && lastPeople > 0) sb.Append(" (").Append(Sg((long)Math.Round(people + away - lastPeople - _lastAway))).Append(" ludzi)");
                    }
                }
                else sb.Append("ludnosc nieskalibrowana (PopulationLaw wylaczone albo przed pierwszym rozliczeniem rent)");
                sb.Append(" | zolnierze: ");
                for (int k = 0; k < Kinds; k++)
                {
                    if (k > 0) sb.Append(", ");
                    sb.Append(KName[k]).Append(' ').Append(men[k]).Append(" (").Append(count[k]).Append(" partii");
                    if (_haveLast) sb.Append("; ").Append(Sg(men[k] - _lastMen[k]));
                    sb.Append(')');
                }
                sb.Append("; jency w partiach ").Append(prisoners).Append(", w lochach osad ").Append(dungeons)
                  .Append(" | wyrzutki w puli ").Append((int)pool)
                  .Append(" | dzis: zabici ").Append(killed).Append(" w ").Append(_fights).Append(" starciach (");
                for (int k = 0; k < Kinds; k++) { if (k > 0) sb.Append(", "); sb.Append(KName[k]).Append(' ').Append(_killed[k]); }
                sb.Append("), ranni ").Append(_wounded).Append(", rozbici ").Append(_routed)
                  .Append("; zwerbowani ").Append(recruited).Append(" (od notabli do partii rodow ").Append(_rNotableParty)
                  .Append(", od notabli bez wodza - garnizony, karawany ").Append(_rNotableOther).Append(", z karczmy ").Append(_rTavern)
                  .Append(", bez osady - jency albo ochotnicy z mapy ").Append(_rNoPlace).Append(", gracz ").Append(_rPlayer)
                  .Append("); dezercja ").Append(_desLord + _desOther).Append(" (partie rodow ").Append(_desLord).Append(')');
                if (_haveLast)
                {
                    int delta = men[KLord] - _lastMen[KLord];
                    int known = _rLed - _killed[KLord] - _desLord;
                    sb.Append(" | bilans partii rodow: zmiana ").Append(Sg(delta)).Append(" = zwerbowani przez wodzow +").Append(_rLed)
                      .Append(" - zabici ").Append(_killed[KLord]).Append(" - dezercja ").Append(_desLord)
                      .Append(" + reszta ").Append(Sg(delta - known)).Append(" (nowe partie z szablonu, wymiana z garnizonem, rozwiazane partie, jency)");
                }
                if (calibrated && menWorld > 0)
                    sb.Append(" | pod bronia (partie rodow + garnizony): ").Append((100.0 * (men[KLord] + men[KGarrison]) / menWorld).ToString("0.00", CultureInfo.InvariantCulture))
                      .Append("% mezczyzn swiata; regionow ponad prog 20% mezczyzn: ").Append(over).Append(" z ").Append(order.Count)
                      .Append(" (bez ludnosci w ksiedze ").Append(noPeople).Append(')');
                // demografia krok 2: ile hearth wsie oddaly i odzyskaly dzis za ludzi (tabory, wyrzutki, pobor wymuszony, zadania, incydenty)
                try { sb.Append(PeopleUnit.DayNote()); } catch { _stumbles++; }
                // demografia krok 4: spustoszenie doby (zdjeci, zabici, w las, uchodzcy, powroty) i stan konta uchodzcow
                try { sb.Append(Devastation.DayNote()); } catch { _stumbles++; }
                sb.Append(" | warownie glodne ").Append(starving).Append(", z ujemnym bilansem zywnosci ").Append(foodMinus).Append(" z ").Append(fiefs).Append('.');
                if (_stumbles > 0) sb.Append(" Potkniecia ksiegi: ").Append(_stumbles).Append('.');
                int reported = _stumbles;
                Log.Info(sb.ToString());
                // demografia krok 3: linia "Ludzie: przyrost naturalny" - tu, bo rozlicza zmiane hearth wsi z licznikami doby PeopleUnit (zeruje je ClearDay nizej)
                PopulationLaw.GrowthDaily(day);
                // demografia krok 4: pomiar doby i regiony najbardziej spustoszone (liczniki zeruje ClearDay nizej)
                Devastation.RegionsNote(day);

                // 4. osiem najbardziej obciazonych regionow
                if (ranked.Count > 0)
                {
                    var parts = new List<string>();
                    for (int i = 0; i < ranked.Count && i < Top; i++)
                    {
                        var r = ranked[i];
                        float last; bool had = _lastPeople.TryGetValue(r.St.StringId, out last);
                        int kin, rin; _killedIn.TryGetValue(r.St.StringId, out kin); _recruitedIn.TryGetValue(r.St.StringId, out rin);
                        parts.Add(r.St.Name + " [" + (r.St.IsTown ? "miasto" : "zamek") + ", " + (r.St.Culture != null ? r.St.Culture.StringId : "?") + "] ludzie " + (long)r.People
                                  + (had && _haveLast ? " (" + Sg((long)Math.Round(r.People - last)) + ")" : "") + ", zaloga " + r.Garrison + ", wyrzutki " + (int)r.Pool
                                  + ", bandy " + r.BandMen + " w " + r.Bands + ", zabici dzis " + kin + ", zwerbowani dzis " + rin
                                  + ", obciazenie " + F(r.Burden * 100f, "0.0") + "%");
                    }
                    Log.Info("Ludzie (regiony): dzien " + day + " - " + Math.Min(Top, ranked.Count) + " najbardziej obciazonych z " + order.Count
                             + " (zaloga + wyrzutki + bandy wobec mezczyzn regionu, 27% ludnosci): " + string.Join("; ", parts.ToArray()) + ".");
                }

                // 5. plik CSV wszystkich regionow
                var war = new Dictionary<IFaction, bool>();
                var csv = new StringBuilder();
                foreach (var r in order)
                {
                    int rowStart = csv.Length;
                    try
                    {
                        var st = r.St;
                        float last; bool had = _lastPeople.TryGetValue(st.StringId, out last);
                        int kin, rin; _killedIn.TryGetValue(st.StringId, out kin); _recruitedIn.TryGetValue(st.StringId, out rin);
                        var f = st.MapFaction; bool atWar = false;
                        if (f != null && !war.TryGetValue(f, out atWar))
                        {
                            foreach (var k in Kingdom.All) if (k != null && k != f && !k.IsEliminated && f.IsAtWarWith(k)) { atWar = true; break; }
                            war[f] = atWar;
                        }
                        var clan = st.OwnerClan;
                        csv.Append(day).Append(';').Append(st.StringId).Append(';').Append(Clean(st.Name != null ? st.Name.ToString() : "")).Append(';')
                           .Append(st.IsTown ? "miasto" : "zamek").Append(';').Append(st.Culture != null ? st.Culture.StringId : "").Append(';')
                           .Append(Clean(clan != null && clan.Kingdom != null ? clan.Kingdom.Name.ToString() : "")).Append(';')
                           .Append(Clean(clan != null ? clan.Name.ToString() : "")).Append(';')
                           .Append(r.Villages).Append(';').Append(r.Looted).Append(';')
                           .Append(calibrated ? ((long)r.People).ToString() : "").Append(';')
                           .Append(calibrated && had && _haveLast ? ((long)Math.Round(r.People - last)).ToString() : "").Append(';')
                           .Append(calibrated ? ((long)r.PeopleNode).ToString() : "").Append(';').Append(calibrated ? ((long)r.PeopleVillages).ToString() : "").Append(';')
                           .Append(F(r.Hearth, "0.#")).Append(';').Append(st.Town != null ? F(st.Town.Prosperity, "0.#") : "").Append(';')
                           .Append(r.Garrison).Append(';').Append(r.Militia).Append(';').Append(F(r.Pool, "0.#")).Append(';')
                           .Append(r.Bands).Append(';').Append(r.BandMen).Append(';').Append(kin).Append(';').Append(rin).Append(';')
                           .Append(r.Burden >= 0f ? F(r.Burden * 100f, "0.##") : "").Append(';')
                           .Append(st.Town != null ? F(st.Town.Security, "0.#") : "").Append(';').Append(st.Town != null ? F(st.Town.FoodStocks, "0.#") : "").Append(';')
                           .Append(r.HasFood ? F(r.FoodChange, "0.#") : "").Append(';').Append(st.IsStarving ? 1 : 0).Append(';').Append(atWar ? 1 : 0);
                        // krok 4: uchodzcy wsi regionu poza domem, ich udzial w ludziach wsi sprzed spustoszenia, zabici przy spustoszeniu od poczatku
                        double rAway, rDead; Devastation.RegionOf(st, out rAway, out rDead);
                        csv.Append(';').Append(((long)Math.Round(rAway)).ToString(CultureInfo.InvariantCulture)).Append(';')
                           .Append(calibrated && r.PeopleVillages + rAway > 0.0 ? F((float)(100.0 * rAway / (r.PeopleVillages + rAway)), "0.###") : "").Append(';')
                           .Append(((long)Math.Round(rDead)).ToString(CultureInfo.InvariantCulture))
                           .Append(Environment.NewLine);
                    }
                    catch { csv.Length = rowStart; _stumbles++; }      // niepelny wiersz nie trafia do pliku
                }
                string path = Log.Csv(CsvName, CsvHeader, csv.ToString());
                if (path != null && _csvPath != path) { _csvPath = path; Log.Info("Ludzie: plik wszystkich regionow (wiersz na region na dobe): " + path); }
                if (_stumbles > reported) Log.Info("Ludzie: dzien " + day + " - pominiete wiersze pliku regionow (wyjatek przy regionie): " + (_stumbles - reported) + ".");

                // 6. stan na jutro
                _lastPeople.Clear();
                if (calibrated) foreach (var r in order) _lastPeople[r.St.StringId] = r.People;
                Array.Copy(men, _lastMen, Kinds);
                _lastAway = away;
                _haveLast = true;
            }
            catch (Exception e) { Log.Error("PeopleLedger.Daily", e); }
            finally { ClearDay(); }
        }
    }
}
