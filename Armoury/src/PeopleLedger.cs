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
    ///    z jawna reszta (nowe partie z szablonu, wymiana z garnizonem); odsetek mezczyzn pod bronia;
    ///  E4/5: w "Ludzie:" uchodzcy poza domem i ludnosc razem z nimi; w CSV regionow uchodzcy, udzial spustoszenia, zabici przy spustoszeniu, dni od najazdu
    ///    (linia "Ludzie (spustoszenie)" - Devastation.DayLine, zaraz po tej ksiedze);
    ///  "Ludzie: przyrost naturalny" (E4/4, PopulationLaw.GrowthDaily) - ile ludzi rodzi sie wsiom dzis i wedlug jakich skladowych, krainy, podloga 10 (R17),
    ///    stan ludnosci miast wobec dobrobytu (bilans hearth wsi z RESZTA - jedno miejsce: linia "Hearth (E4/0)");
    ///  "Ludzie (regiony):" - 8 regionow najbardziej obciazonych: (zaloga + wyrzutki + bandy) wobec mezczyzn regionu;
    ///  plik Logs/[sesja]/ludzie-regiony.csv - wszystkie regiony, wiersz na region na dobe.
    /// Czego ksiega NIE widzi (do kroku 5): z jakiego regionu pochodzi zolnierz partii rodu; przyrost garnizonow (gra dopisuje
    /// ludzi bez zdarzenia - widac tylko zmiane stanu); werbunek jencow i ochotnikow "z mapy" ma w zdarzeniu te sama postac.
    ///
    /// E4/0 (188, projekt etapu 4 - tylko log, przy PeopleMeasureEnabled): nasluchy dostaja podzial wedlug frakcji (Nocna Straz, Wolni Ludzie, Inni);
    /// nowe nasluchy partii (utworzone, zniszczone, usuniete bez zniszczenia); linie "Partie (E4/0)", "Krainy (4.15)" (regiony zgrupowane po kulturze
    /// z pierwszej doby sesji) i "Straz i Wolni Ludzie (4.15)" (tabela strat i przyrostu - Jeff 07.10); ludzie-regiony.csv dostaje kolumny budzetu
    /// hearth (Measure40), "gracz" i "kultura_start" - bez nowych plikow (U8).
    /// </summary>
    internal static class PeopleLedger
    {
        private const float MenShare = 0.27f;      // mezczyzni 16-60 w ludnosci (SZACUNEK z dokumentu demografii, rozdz. 4.0)
        private const float FreeHands = 0.20f;     // prog wolnych rak: 20% mezczyzn (decyzja Jeffa 05.10)
        private const int Top = 8;
        private const string CsvName = "ludzie-regiony.csv";
        private const string CsvHeader = "dzien;region_id;region;rodzaj;kultura;krolestwo;rod;wsie;wsie_spalone;ludzie;ludzie_zmiana;ludzie_osada;ludzie_wsie;hearth;dobrobyt;"
                                         + "zaloga;milicja;wyrzutki;bandy_partie;bandy_ludzie;zabici_dzis;zwerbowani_dzis;obciazenie_proc_mezczyzn;bezpieczenstwo;zywnosc;"
                                         + "bilans_zywnosci;glod;wojna;hearth_tick;hearth_tabory;hearth_rabunek;hearth_inne;gracz;kultura_start;"
                                         + "uchodzcy;spustoszenie_proc;zabici_spustoszenie;dni_od_spustoszenia";      // E4/5: konto UCHODZCY regionu (PeopleBook)

        // rodzaje partii
        private const int KLord = 0, KGarrison = 1, KMilitia = 2, KBandit = 3, KCaravan = 4, KVillager = 5, KOther = 6, Kinds = 7;
        private static readonly string[] KName = { "partie rodow", "garnizony", "milicje", "bandy", "karawany", "tabory wsi", "inne" };

        private sealed class Row
        {
            public Settlement St;
            public float PeopleNode, PeopleVillages, Hearth, Pool, Burden = -1f;
            public int Villages, Looted, Garrison, Militia, Bands, BandMen;
            public float FoodChange; public bool HasFood;
            public float FoodStocks;                         // T6: zapas warowni (odczyt w tej samej petli co FoodChange)
            public float People { get { return PeopleNode + PeopleVillages; } }
        }

        // stan miedzy dobami (bez zapisu - po wczytaniu pierwsza doba jest bez zmian)
        private static readonly Dictionary<string, float> _lastPeople = new Dictionary<string, float>();
        private static readonly int[] _lastMen = new int[Kinds];
        private static bool _haveLast;
        private static double _lastAway;           // E4/5: uchodzcy swiata poza domem wczoraj (konto UCHODZCY)
        private static string _csvPath;

        // liczniki doby
        private static readonly int[] _killed = new int[Kinds];
        private static readonly Dictionary<string, int> _killedIn = new Dictionary<string, int>(), _recruitedIn = new Dictionary<string, int>();
        private static int _fights, _wounded, _routed;
        private static int _rNotableParty, _rNotableOther, _rTavern, _rNoPlace, _rPlayer, _rLed, _desLord, _desOther;
        private static int _rRotEcho;              // 171 A1: echo werbunku ROT (ten sam czlowiek) - nie liczony jako nowy
        private static int _stumbles;              // potkniecia nasluchow - liczymy, nie gasimy
        // T4: odczyt dla WarLedger (pomiar przed/po, tylko czyta)
        internal static int DesertedLordToday { get { return _desLord; } }
        internal static int StumblesToday { get { return _stumbles; } }
        internal static int KilledMilitiaToday { get { return _killed[KMilitia]; } }   // E4/0: odcinek milicji w linii "Zalogi (169c)"

        // ------------------------------------------------------------ E4/0 (188): frakcje, partie, krainy (tylko log)
        internal const int GOther = 0, GWatch = 1, GFree = 2, GOthers = 3, Groups = 4;
        private static readonly int[] _gKilled = new int[Groups], _gWounded = new int[Groups], _gRouted = new int[Groups], _gRecruited = new int[Groups], _gDeserted = new int[Groups], _gNewMen = new int[Groups];
        private static readonly long[] _gSessIn = new long[Groups], _gSessDead = new long[Groups], _gSessDes = new long[Groups];
        private static readonly int[] _gLast = new int[Groups];
        private static bool _gHaveLast;
        private const int NLord = 0, NCaravan = 1, NVillager = 2, NBoat = 3, NBandit = 4, NConvoy = 5, NOthers = 6, NMilitia = 7, NOther = 8, NKinds = 9;
        private static readonly string[] NName = { "partie rodow", "karawany", "tabory wsi", "lodzie", "bandy", "konwoje BK", "Inni", "milicje i zalogi", "inne" };
        private static readonly int[] _nNew = new int[NKinds], _nNewMen = new int[NKinds];
        private static int _goneBattleN, _goneBattleMen, _goneDisbN, _goneDisbHealthy, _goneDisbWounded, _goneDisbPris, _goneBandN, _goneBandMen, _goneWights,
                           _goneGameN, _goneGameHealthy, _goneGameWounded, _goneRemovedN, _goneRemovedMen, _e40Stumbles;
        private static readonly Dictionary<MobileParty, int> _pLast = new Dictionary<MobileParty, int>(), _pCreatedMen = new Dictionary<MobileParty, int>();
        private static readonly HashSet<MobileParty> _pDestroyed = new HashSet<MobileParty>();

        /// <summary>E4/0: grupa frakcji - Inni (partia nieumarlych albo frakcja kultury whitewalker), Nocna Straz (krolestwo nightswatch), Wolni Ludzie
        /// (krolestwo freefolk), reszta. Krolestwo po StringId frakcji (CrownGifts.IsWatch, wzor Leaks310), nie po kulturze.</summary>
        internal static int GroupOf(IFaction f, MobileParty mp)
        {
            try
            {
                if (mp != null && Undead.Party(mp)) return GOthers;
                if (f == null) return GOther;
                if (f.Culture != null && f.Culture.StringId == "whitewalker") return GOthers;
                if (CrownGifts.IsWatch(f)) return GWatch;
                if (f.StringId == "freefolk") return GFree;
                return GOther;
            }
            catch { return GOther; }
        }

        private static int NKindOf(MobileParty mp)
        {
            if (mp == null) return NOther;
            var pc = mp.PartyComponent;
            string tn = pc != null ? pc.GetType().Name : "";
            if (mp.IsVillager) return tn.IndexOf("Fishing", StringComparison.Ordinal) >= 0 ? NBoat : NVillager;
            if (tn == "PopulationPartyComponent") return NConvoy;
            if (mp.IsMilitia || mp.IsGarrison) return NMilitia;
            if (Undead.Party(mp)) return NOthers;
            if (mp.IsLordParty) return NLord;
            if (mp.IsCaravan) return NCaravan;
            if (mp.IsBandit) return NBandit;
            return NOther;
        }

        private static int Wights(TroopRoster r)
        {
            int n = 0;
            if (r == null) return 0;
            for (int i = 0; i < r.Count; i++) { var e = r.GetElementCopyAtIndex(i); if (e.Character != null && !e.Character.IsHero && e.Number > 0 && Undead.Character(e.Character)) n += e.Number; }
            return n;
        }

        /// <summary>E4/0: nowa partia - rodzaj i ludzie przy utworzeniu (gra wysyla zdarzenie po Create/Initialize komponentu).</summary>
        internal static void OnPartyCreated(MobileParty mp)
        {
            try
            {
                if (mp == null || !Measure40.On) return;
                int k = NKindOf(mp), men = mp.MemberRoster != null ? mp.MemberRoster.TotalManCount : 0;
                _nNew[k]++; _nNewMen[k] += men;
                _pCreatedMen[mp] = men;
                _gNewMen[GroupOf(mp.MapFaction, mp)] += men;
            }
            catch { _e40Stumbles++; }
        }

        /// <summary>E4/2: sklad nowej partii zmieniony jeszcze w zdarzeniu utworzenia (banda Innych przycieta do puli cial w postfiksie nasluchu ROT) -
        /// jesli nasz nasluch juz ja policzyl (kolejnosc nasluchow zalezy od kolejnosci zachowan), poprawka o roznice; jesli nie - policzy juz sklad po przycieciu.</summary>
        internal static void FixCreatedMen(MobileParty mp)
        {
            try
            {
                if (mp == null || !Measure40.On) return;
                int was; if (!_pCreatedMen.TryGetValue(mp, out was)) return;
                int now = mp.MemberRoster != null ? mp.MemberRoster.TotalManCount : 0, d = now - was;
                if (d == 0) return;
                _nNewMen[NKindOf(mp)] += d; _pCreatedMen[mp] = now; _gNewMen[GroupOf(mp.MapFaction, mp)] += d;
            }
            catch { _e40Stumbles++; }
        }

        /// <summary>E4/0: partia zniszczona (z niszczycielem = w bitwie - ostatni stan dobowy) albo rozwiazana (sklad przy rozwiazaniu - gra czysci go po zdarzeniu).</summary>
        internal static void OnPartyDestroyed(MobileParty mp, PartyBase destroyer)
        {
            try
            {
                if (mp == null || !Measure40.On) return;
                _pDestroyed.Add(mp);
                int last; if (!_pLast.TryGetValue(mp, out last)) _pCreatedMen.TryGetValue(mp, out last);
                if (destroyer != null) { _goneBattleN++; _goneBattleMen += last; return; }
                var r = mp.MemberRoster;
                int tot = r != null ? r.TotalManCount : 0, wnd = r != null ? r.TotalWounded : 0;
                if (OutlawLaw.On && OutlawLaw.IsOutlawParty(mp))
                {
                    // OutlawLaw.PartyDestroyed oddaje sklad i jencow rozwiazanej bandy do puli - w tym wighty ("druga droga wightow", C 8)
                    _goneBandN++; _goneBandMen += tot; _goneWights += Wights(r) + Wights(mp.PrisonRoster);
                    return;
                }
                _goneDisbN++; _goneDisbHealthy += tot - wnd; _goneDisbWounded += wnd;
                if (mp.PrisonRoster != null) _goneDisbPris += mp.PrisonRoster.TotalRegulars;
                if (mp.IsVillager) { _goneGameN++; _goneGameHealthy += tot - wnd; _goneGameWounded += wnd; }   // R18: tabor albo lodz rozwiazane bez bitwy
            }
            catch { _e40Stumbles++; }
        }

        /// <summary>E4/0: partia usunieta bez zniszczenia (gra wysyla tylko OnPartyRemoved - sklad juz wyczyszczony; ostatni stan dobowy).</summary>
        internal static void OnPartyRemoved(PartyBase pb)
        {
            try
            {
                var mp = pb != null && pb.IsMobile ? pb.MobileParty : null;
                if (mp == null || !Measure40.On || _pDestroyed.Contains(mp)) return;
                int last; if (!_pLast.TryGetValue(mp, out last)) _pCreatedMen.TryGetValue(mp, out last);
                _goneRemovedN++; _goneRemovedMen += last;
            }
            catch { _e40Stumbles++; }
        }

        internal static void Reset()
        {
            _lastPeople.Clear(); Array.Clear(_lastMen, 0, Kinds); _haveLast = false; _lastAway = 0.0; _csvPath = null;
            Array.Clear(_gSessIn, 0, Groups); Array.Clear(_gSessDead, 0, Groups); Array.Clear(_gSessDes, 0, Groups); Array.Clear(_gLast, 0, Groups); _gHaveLast = false;
            _pLast.Clear();
            ClearDay();
        }

        private static void ClearDay()
        {
            Array.Clear(_killed, 0, Kinds); _killedIn.Clear(); _recruitedIn.Clear();
            _fights = _wounded = _routed = 0;
            _rNotableParty = _rNotableOther = _rTavern = _rNoPlace = _rPlayer = _rLed = _desLord = _desOther = 0;
            _rRotEcho = 0;
            _stumbles = 0;
            ClearDay40();
        }

        private static void ClearDay40()
        {
            Array.Clear(_gKilled, 0, Groups); Array.Clear(_gWounded, 0, Groups); Array.Clear(_gRouted, 0, Groups); Array.Clear(_gRecruited, 0, Groups); Array.Clear(_gDeserted, 0, Groups); Array.Clear(_gNewMen, 0, Groups);
            Array.Clear(_nNew, 0, NKinds); Array.Clear(_nNewMen, 0, NKinds);
            _goneBattleN = _goneBattleMen = _goneDisbN = _goneDisbHealthy = _goneDisbWounded = _goneDisbPris = _goneBandN = _goneBandMen = _goneWights = 0;
            _goneGameN = _goneGameHealthy = _goneGameWounded = _goneRemovedN = _goneRemovedMen = _e40Stumbles = 0;
            _pCreatedMen.Clear(); _pDestroyed.Clear();
            try { PeopleBook.ZeroDisband40(); } catch { }   // E4/3 (R18): ludzie rozwiazanych taborow, ktorzy wrocili do wsi - to samo okno co liczniki linii Partie
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
                        int g = p.Party != null ? GroupOf(p.Party.MapFaction, p.Party.MobileParty) : GOther;   // E4/0: podzial frakcji (tylko licznik)
                        if (p.WoundedInBattle != null) { int w = p.WoundedInBattle.TotalManCount; _wounded += w; _gWounded[g] += w; }
                        if (p.RoutedInBattle != null) { int r = p.RoutedInBattle.TotalManCount; _routed += r; _gRouted[g] += r; }
                        int d = p.DiedInBattle != null ? p.DiedInBattle.TotalManCount : 0;
                        if (d <= 0) continue;
                        dead += d;
                        _killed[KindOf(p.Party != null ? p.Party.MobileParty : null)] += d;
                        _gKilled[g] += d;
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
                if (RecruitSources.IsRotEcho(settlement, source)) { _rRotEcho += amount; return; }   // 171 A1: ten sam czlowiek, zmiana oznaki - nie nowy werbunek
                if (amount <= 0) return;
                if (recruiter != null && recruiter == Hero.MainHero) _rPlayer += amount;
                else if (source != null) { if (recruiter != null) _rNotableParty += amount; else _rNotableOther += amount; }
                else if (settlement != null) _rTavern += amount;
                else _rNoPlace += amount;
                // do bilansu partii rodow tylko werbunek do partii rodu (karawana z towarzyszem na czele tez ma "wodza")
                var rp = recruiter != null ? recruiter.PartyBelongedTo : null;
                if (rp != null && rp.IsLordParty) _rLed += amount;
                AddRegion(_recruitedIn, settlement ?? (source != null ? source.CurrentSettlement : null), amount);
                // E4/0: werbunek wedlug frakcji werbujacego (wodz; bez wodza - osada, czyli jej zaloga)
                _gRecruited[GroupOf(rp != null ? rp.MapFaction : (recruiter != null ? recruiter.MapFaction : (settlement != null ? settlement.MapFaction : null)), rp)] += amount;
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
                _gRecruited[GroupOf(Hero.MainHero != null ? Hero.MainHero.MapFaction : null, MobileParty.MainParty)] += amount;   // E4/0
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
                _gDeserted[GroupOf(party != null ? party.MapFaction : null, party)] += n;   // E4/0
            }
            catch { _stumbles++; }
        }

        // ------------------------------------------------------------ raz na dobe
        private static string Sg(long n) { return (n >= 0 ? "+" : "") + n; }
        private static string F(float v, string fmt) { return v.ToString(fmt, CultureInfo.InvariantCulture); }
        private static string Clean(string s) { return string.IsNullOrEmpty(s) ? "" : s.Replace(';', ',').Replace('\n', ' ').Replace('\r', ' '); }

        // ------------------------------------------------------------ T6 (d): zapasy warowni w dniach (sam odczyt, wylacznik WorldMeasureLog)
        private const string NorthCulture = "battania";      // kultura "North" w ROT (ROT-Content/ModuleData/spcultures.xml, id battania)

        // dni zapasu = FoodStocks / -FoodChange; FoodChange >= 0 = "bez ubytku" (osobno, nie w medianie)
        private static string StockPart(List<Row> rows, bool north)
        {
            var days = new List<float>();
            int all = 0, steady = 0, below90 = 0, below30 = 0; float min = float.MaxValue; string minName = "";
            foreach (var r in rows)
            {
                try
                {
                    if (!r.HasFood) continue;
                    if (north && (r.St.Culture == null || r.St.Culture.StringId != NorthCulture)) continue;
                    all++;
                    if (r.FoodChange >= 0f) { steady++; continue; }
                    float d = Math.Max(0f, r.FoodStocks) / -r.FoodChange;
                    days.Add(d);
                    if (d < 90f) below90++;
                    if (d < 30f) below30++;
                    if (d < min) { min = d; minName = r.St.Name != null ? r.St.Name.ToString() : r.St.StringId; }
                }
                catch { _stumbles++; }
            }
            var sb = new StringBuilder();
            sb.Append(all).Append(" warowni, z ubytkiem ").Append(days.Count);
            if (days.Count > 0)
            {
                days.Sort();
                int n = days.Count;
                float med = (n % 2 == 1) ? days[n / 2] : 0.5f * (days[n / 2 - 1] + days[n / 2]);
                sb.Append(": dni zapasu mediana ").Append(F(med, "0")).Append(", minimum ").Append(F(min, "0")).Append(" (").Append(Clean(minName)).Append(')');
            }
            sb.Append(", ponizej 90 dni ").Append(below90).Append(", ponizej 30 dni ").Append(below30).Append(", bez ubytku ").Append(steady);
            return sb.ToString();
        }

        private static string StockNote(List<Row> rows)
        {
            try
            {
                var c = Settings.Current;
                if (c == null || !c.WorldMeasureLog) return "";
                int before = _stumbles;
                string s = " | Zapasy warowni (T6): swiat " + StockPart(rows, false) + "; Polnoc (" + NorthCulture + ") " + StockPart(rows, true);
                if (_stumbles > before) s += ", potkniecia zapasow " + (_stumbles - before);   // wypisane tu - licznik ksiegi juz poszedl w linii
                int noRead = 0;
                foreach (var r in rows) { try { if (!r.HasFood && r.St.Town != null) noRead++; } catch { noRead++; } }
                if (noRead > 0) s += ", warownie bez odczytu zywnosci " + noRead;            // model zywnosci rzucil (licznik w "Potkniecia ksiegi")
                return s + ".";
            }
            catch { return " | Zapasy warowni (T6): wyjatek."; }
        }

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
                // E4/0: migawka partii (ostatni stan znikajacych), stan Strazy i Wolnych Ludzi, partie rodow wedlug kultury krolestwa (Kingdom.Culture - U19)
                bool e40 = Measure40.On;
                long t40 = 0;
                var snap = e40 ? new Dictionary<MobileParty, int>(_pLast.Count + 64) : null;
                var gPar = new int[Groups]; var gGar = new int[Groups];
                var lordByCult = new Dictionary<string, int>();
                foreach (var mp in MobileParty.All)
                {
                    try
                    {
                        if (mp == null || !mp.IsActive || mp.MemberRoster == null) continue;
                        int n = mp.MemberRoster.TotalManCount;
                        int k = KindOf(mp);
                        men[k] += n; count[k]++;
                        if (e40)
                        {
                            long tt = System.Diagnostics.Stopwatch.GetTimestamp();
                            try
                            {
                                snap[mp] = n;
                                int g = GroupOf(mp.MapFaction, mp);
                                if (g == GWatch || g == GFree) { if (mp.IsGarrison) gGar[g] += n; else gPar[g] += n; }
                                if (k == KLord)
                                {
                                    var kg = mp.ActualClan != null ? mp.ActualClan.Kingdom : null;
                                    string kc = kg != null && kg.Culture != null ? kg.Culture.StringId : null;
                                    if (kc != null) { int lv; lordByCult.TryGetValue(kc, out lv); lordByCult[kc] = lv + n; }
                                }
                            }
                            catch { _e40Stumbles++; }
                            t40 += System.Diagnostics.Stopwatch.GetTimestamp() - tt;
                        }
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
                int over = 0, noPeople = 0, starving = 0, foodMinus = 0, fiefs = 0, r5 = 0;
                var ranked = new List<Row>();
                foreach (var r in order)
                {
                    people += r.People; peopleNodes += r.PeopleNode; pool += r.Pool;
                    float last;
                    if (_lastPeople.TryGetValue(r.St.StringId, out last)) lastPeople += last;
                    if (r.People > 0f)
                    {
                        // E4/7 (U19): przy czynnej ksiedze pochodzenia obciazenie = a z R5 ((sluzba i jency z regionu + polegli netto) / mezczyzni w dniu siewu), prog = f krainy;
                        // przed siewem / wylaczone - stara definicja (zaloga + wyrzutki + bandy) / mezczyzni regionu, prog 20%
                        double a5, f5;
                        if (Armoury.FreeHands.AF(r.St, out a5, out f5)) { r.Burden = (float)a5; r5++; if (a5 > f5 && f5 < 0.999) over++; }
                        else
                        {
                            r.Burden = (r.Garrison + r.Pool + r.BandMen) / (r.People * MenShare);
                            if (r.Burden > FreeHands) over++;
                        }
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
                            r.FoodStocks = r.St.Town.FoodStocks;                        // T6: dni zapasu (sam odczyt)
                            if (r.FoodChange < 0f) foodMinus++;
                        }
                    }
                    catch { _stumbles++; }                                          // T6 po recenzji: model zywnosci rzucil - liczymy (warownia bez odczytu)
                }
                ranked.Sort((a, b) => b.Burden.CompareTo(a.Burden));
                double menWorld = people * MenShare;
                double away = 0.0;
                try { away = PeopleBook.RefugeesWorld(); } catch { _stumbles++; }       // E4/5: uchodzcy zeszli z hearth wsi, ale zyja
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
                    // E4/5: ludnosc z uchodzcami (konto UCHODZCY ksiegi ludzi) - prog ludnosci E4/4 liczy sie z "razem z nimi" (S15), nie z samej ludnosci w domu
                    if (Devastation.On || away > 0.0 || _lastAway > 0.0)
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
                  .Append(", duplikaty ROT (ten sam czlowiek, nie liczeni) ").Append(_rRotEcho)
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
                      .Append(r5 > 0 ? "% mezczyzn swiata; regionow ponad prog wolnych rak (R5, wedlug krain): " : "% mezczyzn swiata; regionow ponad prog 20% mezczyzn: ").Append(over).Append(" z ").Append(order.Count)
                      .Append(" (bez ludnosci w ksiedze ").Append(noPeople).Append(')');
                sb.Append(" | warownie glodne ").Append(starving).Append(", z ujemnym bilansem zywnosci ").Append(foodMinus).Append(" z ").Append(fiefs).Append('.');
                if (_stumbles > 0) sb.Append(" Potkniecia ksiegi: ").Append(_stumbles).Append('.');
                sb.Append(StockNote(order));                                         // T6 (d): zawsze NA KONCU linii - poczatek parsuje sprawdz_logi.py
                int reported = _stumbles;
                Log.Info(sb.ToString());
                // E4/4 (109n): linia "Ludzie: przyrost naturalny" - zaraz po "Ludzie:" (tylko log; wlasny try - U24)
                try { PopulationLaw.GrowthDaily(day); } catch (Exception e) { Log.Error("PeopleLedger.GrowthDaily", e); }

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
                        // E4/0: budzet hearth regionu (Measure40: tick, tabory i lodzie, rabunek, inne = zmiana - okna), warownia rodu gracza (U27), kultura z pierwszej doby sesji (U3)
                        var bud = Measure40.RegionBudget(st);
                        for (int bi = 0; bi < 4; bi++) csv.Append(';').Append(bud != null ? F(bud[bi], "0.###") : "");
                        csv.Append(';').Append(clan != null && clan == Clan.PlayerClan ? 1 : 0).Append(';').Append(Measure40.CultureAtStart(st));
                        // E4/5: uchodzcy wsi regionu poza domem, ich udzial w ludziach wsi regionu (dom + uchodzcy), zabici przy spustoszeniu od poczatku, dni od ostatniego najazdu
                        double rAway, rDead; PeopleBook.RegionRefugees(st, out rAway, out rDead);
                        int lastStrike; bool clock = PeopleBook.LastStrike(st, out lastStrike);
                        csv.Append(';').Append(rAway.ToString("0.##", CultureInfo.InvariantCulture)).Append(';')
                           .Append(calibrated && r.PeopleVillages + rAway > 0.0 ? (100.0 * rAway / (r.PeopleVillages + rAway)).ToString("0.####", CultureInfo.InvariantCulture) : "").Append(';')
                           .Append(rDead.ToString("0.##", CultureInfo.InvariantCulture)).Append(';')
                           .Append(clock ? ((int)CampaignTime.Now.ToDays - lastStrike).ToString(CultureInfo.InvariantCulture) : "")
                           .Append(Environment.NewLine);
                    }
                    catch { csv.Length = rowStart; _stumbles++; }      // niepelny wiersz nie trafia do pliku
                }
                string path = Log.Csv(CsvName, CsvHeader, csv.ToString());
                if (path != null && _csvPath != path) { _csvPath = path; Log.Info("Ludzie: plik wszystkich regionow (wiersz na region na dobe): " + path); }
                if (_stumbles > reported) Log.Info("Ludzie: dzien " + day + " - pominiete wiersze pliku regionow (wyjatek przy regionie): " + (_stumbles - reported) + ".");

                // 5b. E4/0: linie "Partie (E4/0)", "Krainy (4.15)", "Straz i Wolni Ludzie (4.15)" (przy PeopleMeasureEnabled)
                if (e40)
                {
                    long tt = System.Diagnostics.Stopwatch.GetTimestamp();
                    try { Lines40(day, order, snap, gPar, gGar, lordByCult, t40); }
                    catch (Exception e) { Log.Error("PeopleLedger.Lines40", e); }
                    _pLast.Clear(); foreach (var kv in snap) _pLast[kv.Key] = kv.Value;
                    t40 += System.Diagnostics.Stopwatch.GetTimestamp() - tt;
                }
                else { _pLast.Clear(); _gHaveLast = false; }

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

        // ------------------------------------------------------------ E4/0 (188): linie dobowe (tylko log)
        private static string ProgOf(string c) { return c == "sturgia" ? "40" : c == "freefolk" ? "100" : c == "nightswatch" ? "-" : "20"; }

        private static void Lines40(int day, List<Row> order, Dictionary<MobileParty, int> snap, int[] gPar, int[] gGar, Dictionary<string, int> lordByCult, long t40)
        {
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            var ci = CultureInfo.InvariantCulture;
            // zaraza od ostatniej linii (Measure40.NoteDisease) - wedlug krainy i grupy frakcji
            var disC = new Dictionary<string, int>(); var disG = new int[Groups]; int disDead, disUnhit;
            Measure40.TakeDisease(disC, disG, out disDead, out disUnhit);

            // 1. Partie
            var sb = new StringBuilder(1200);
            sb.Append("Partie (E4/0): dzien ").Append(day).Append(" | nowe (ludzie przy utworzeniu): ");
            for (int i = 0; i < NKinds; i++) sb.Append(i > 0 ? ", " : "").Append(NName[i]).Append(' ').Append(_nNew[i]).Append(" (").Append(_nNewMen[i]).Append(')');
            sb.Append(" | zniknely: rozbite w bitwie ").Append(_goneBattleN).Append(" (ostatni stan ").Append(_goneBattleMen).Append(" ludzi)")
              .Append(", rozwiazane ").Append(_goneDisbN).Append(" (przy rozwiazaniu zdrowi ").Append(_goneDisbHealthy).Append(", ranni ").Append(_goneDisbWounded)
              .Append(", jency ").Append(_goneDisbPris).Append(" - znikaja z partia; w tym tabory i lodzie rozwiazane przez gre bez bitwy ").Append(_goneGameN)
              .Append(", zdrowi ").Append(_goneGameHealthy).Append(", ranni ").Append(_goneGameWounded).Append(" - R18; wrocili do swojej wsi (E4/3) ").Append(PeopleBook.Disband40)
              .Append(", kontrola zdrowi + ranni - wrocili = ").Append(_goneGameHealthy + _goneGameWounded - PeopleBook.Disband40).Append(")")
              .Append(", bandy rozwiazane do puli wyrzutkow ").Append(_goneBandN).Append(" (ludzi ").Append(_goneBandMen).Append("; w tym wighty z rozwiazanych band do puli ").Append(_goneWights)
              .Append(Settings.Current != null && Settings.Current.OthersRiseOnlyFromFallen ? " - przy E4/2 do niczego, OutlawLaw ich nie dopisuje)" : ")")
              .Append(", usuniete bez zniszczenia ").Append(_goneRemovedN).Append(" (ostatni stan ").Append(_goneRemovedMen).Append(" ludzi)")
              .Append(" | partii na mapie ").Append(snap.Count);
            if (_e40Stumbles > 0) sb.Append(" | potkniecia ").Append(_e40Stumbles);
            double f = 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            sb.Append(" | czas E4/0 w ksiedze ludzi ").Append(((t40 + System.Diagnostics.Stopwatch.GetTimestamp() - t0) * f).ToString("0.0", ci)).Append(" ms.");
            Log.Info(sb.ToString());
            long t1 = System.Diagnostics.Stopwatch.GetTimestamp();   // czas linii Krainy + Straz (po linii Partie) - na koncu linii Straz (T8 calosc)

            // 2. Krainy - regiony po kulturze z pierwszej doby sesji
            var kr = new Dictionary<string, double[]>();   // [ludzie, zaloga + wyrzutki + bandy, wyrzutki, zabici dzis, E4/7: S + K_m, 0.27 x L_ref, prog f]
            bool r5 = false;
            foreach (var r in order)
            {
                try
                {
                    string c = Measure40.CultureAtStart(r.St);
                    double[] a; if (!kr.TryGetValue(c, out a)) { a = new double[7]; kr[c] = a; }
                    int kin; _killedIn.TryGetValue(r.St.StringId, out kin);
                    a[0] += r.People; a[1] += r.Garrison + r.Pool + r.BandMen; a[2] += r.Pool; a[3] += kin;
                    // E4/7 (U19): obciazenie krainy z jednej definicji R5 - suma (S + K_m) / suma (0.27 x L_ref) jej regionow, prog f krainy
                    var cell = Armoury.FreeHands.Of(r.St);
                    if (cell != null && cell.LRef > 0) { a[4] += cell.S + cell.Km; a[5] += PeopleBook.MenShare * cell.LRef; a[6] = cell.F; r5 = true; }
                }
                catch { _e40Stumbles++; }
            }
            foreach (var kv in lordByCult) if (!kr.ContainsKey(kv.Key)) kr[kv.Key] = new double[7];
            foreach (var kv in disC) if (!kr.ContainsKey(kv.Key)) kr[kv.Key] = new double[7];
            var keys = new List<string>(kr.Keys);
            keys.Sort((a, b) => kr[b][0].CompareTo(kr[a][0]));
            bool slavesData = Measure169c.LinesOn;
            // E4/9: niewolni ksiegi trzymani w krainie (kraina regionu trzymania z dnia kalibracji) - obok niewolnych BK (169c)
            var thrHeld = new Dictionary<string, double>();
            bool thrOn = ThrallLaw.On;
            if (thrOn) { try { double u; PeopleBook.ThrallsByRealm(thrHeld, null, out u); } catch { _e40Stumbles++; } }
            var kb = new StringBuilder(4000);
            kb.Append("Krainy (4.15): dzien ").Append(day).Append(" | kraina = kultura regionu z pierwszej doby sesji; obciazenie ")
              .Append(r5 ? "R5 (E4/7, jedna definicja z ksiegi pochodzenia): (sluzba i jency z regionu + polegli netto) / mezczyzni w dniu siewu, prog wedlug krain"
                         : "wedlug PeopleLedger: (zaloga + wyrzutki + bandy) / mezczyzni regionu (ksiega pochodzenia E4/7 przed siewem albo wylaczona)")
              .Append("; partie rodow wedlug Kingdom.Culture |");
            for (int i = 0; i < keys.Count; i++)
            {
                var c = keys[i]; var a = kr[c];
                double menK = a[0] * MenShare;
                int lords; lordByCult.TryGetValue(c, out lords);
                int dis; disC.TryGetValue(c, out dis);
                long sl = 0; bool hasSl = slavesData && Measure169c.SlavesOf(c, day, out sl);
                kb.Append(i > 0 ? "; " : " ").Append(c).Append(": ludzie ").Append(a[0].ToString("0", ci)).Append(" / mezczyzni ").Append(menK.ToString("0", ci))
                  .Append(" / obciazenie ").Append(r5 ? (a[5] > 0 ? (100.0 * a[4] / a[5]).ToString("0.00", ci) + "%" : "-") : (menK > 0 ? (100.0 * a[1] / menK).ToString("0.00", ci) + "%" : "-"))
                  .Append(" [prog ").Append(r5 && a[5] > 0 ? (c == "nightswatch" ? "- (Dar: " + (100.0 * a[6]).ToString("0", ci) + " tylko plon)" : (100.0 * a[6]).ToString("0.#", ci)) : ProgOf(c)).Append(']')
                  .Append(" / partie rodow ").Append(lords).Append(" / wyrzutki ").Append(a[2].ToString("0", ci))
                  .Append(" / niewolni BK ").Append(hasSl ? sl.ToString(ci) : "brak danych 169c")
                  .Append(" / niewolni (ksiega E4/9) ").Append(thrOn ? (thrHeld.ContainsKey(c) ? thrHeld[c].ToString("0", ci) : "0") : "-")
                  .Append(" / zabici dzis ").Append(a[3].ToString("0", ci)).Append(" / zmarli z zarazy dzis ").Append(dis);
            }
            kb.Append(" | zaraza razem dzis ").Append(disDead).Append(" (z rosteru), nietrafione HitRandom ").Append(disUnhit).Append(" (CampFever zadal ").Append(disDead + disUnhit).Append(")");
            if (_e40Stumbles > 0) kb.Append(" | potkniecia ").Append(_e40Stumbles);
            kb.Append('.');
            Log.Info(kb.ToString());

            // 3. Straz i Wolni Ludzie - tabela strat i przyrostu (Jeff 07.10)
            var wb = new StringBuilder(1200);
            wb.Append("Straz i Wolni Ludzie (4.15): dzien ").Append(day);
            int[] gs = { GWatch, GFree }; string[] gn = { "Nocna Straz", "Wolni Ludzie" };
            for (int j = 0; j < 2; j++)
            {
                int g = gs[j];
                int state = gPar[g] + gGar[g];
                int inflow = _gRecruited[g] + _gNewMen[g];
                int deadN = _gKilled[g] + disG[g];
                _gSessIn[g] += inflow; _gSessDead[g] += deadN; _gSessDes[g] += _gDeserted[g];
                wb.Append(" | ").Append(gn[j]).Append(": stan ").Append(state).Append(" (partie ").Append(gPar[g]).Append(", zalogi ").Append(gGar[g]).Append(')');
                if (_gHaveLast)
                {
                    int d = state - _gLast[g];
                    int named = _gRecruited[g] - _gKilled[g] - disG[g] - _gDeserted[g] + _gNewMen[g];
                    wb.Append(", zmiana ").Append(Sg(d)).Append(" = zwerbowani ").Append(_gRecruited[g]).Append(" - zabici ").Append(_gKilled[g]).Append(" - zmarli z zarazy ").Append(disG[g])
                      .Append(" - dezercja ").Append(_gDeserted[g]).Append(" + nowe partie ").Append(_gNewMen[g]).Append(" + reszta ").Append(Sg(d - named))
                      .Append(" (zalogi z modelu, jency, \"na Mur\" z I1, wymiana, zwolnieni przez 166 do wsi, rozbici, rozwiazane) [ranni w bitwach ").Append(_gWounded[g]).Append(", rozbici ").Append(_gRouted[g]).Append(']');
                }
                else wb.Append(" - pierwsza doba sesji, zmiany od nastepnej");
                wb.Append(" | ").Append(gn[j]).Append(" od startu sesji: przybylo ").Append(_gSessIn[g]).Append(", zgineli ").Append(_gSessDead[g]).Append(", zdezerterowali ").Append(_gSessDes[g]);
                _gLast[g] = state;
            }
            _gHaveLast = true;
            wb.Append(" | Inni (dla porownania): zabici ").Append(_gKilled[GOthers]).Append(", zwerbowani ").Append(_gRecruited[GOthers]).Append(", nowe partie ").Append(_gNewMen[GOthers]);
            // E4/9: skad bracia (WatchLaw.Daily - migawka tej doby; linia biegnie po niej)
            wb.Append(" | Straz z Westeros (E4/9): ").Append(WatchLaw.Live ? "" : "prawo nieczynne; ").Append("z Muru ").Append(WatchLaw.LastWall.ToString("0", ci)).Append(", z pul ").Append(WatchLaw.LastPool.ToString("0", ci))
              .Append(", z ludnosci ").Append(WatchLaw.LastPeople.ToString("0", ci)).Append(", bez pokrycia ").Append(WatchLaw.LastNone.ToString("0", ci))
              .Append("; zwolnieni 166: ").Append(WatchLaw.LastReleased).Append("; rabunki Strazy: ").Append(WatchLaw.LastRaids);
            try { wb.Append(ClanBudget.FreeFolkNote()); } catch { }   // WL1 (wyspy-wolni): zold Wolnych Ludzi na czlowieka, zalegly zold
            wb.Append(" | czas linii Krainy i Straz ").Append(((System.Diagnostics.Stopwatch.GetTimestamp() - t1) * f).ToString("0.0", ci)).Append(" ms.");
            Log.Info(wb.ToString());
        }
    }
}
