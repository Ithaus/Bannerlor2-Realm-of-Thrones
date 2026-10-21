using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// PL - PLOTKI (sw-zajazdy, Jeff 16.10: "gdzie sa bandyci lub jacys zboje i ich kryjowki, a moze skarby; slyszalem, ze banda Lysego Johna rabuje od miesiaca,
    /// oblowila sie tak mocno, ze ciezko ich bedzie teraz wykurzyc"; STAN-PRAC 52 pkt 2). Klucz InnGossip. "Listen to the gossip" w menu zajazdu przy szlaku
    /// (RoadsideInns) i w menu karczmy miasta (TownInns, "arm_inn") - okno z tekstem (InformationManager.ShowInquiry), zadnej zmiany menu, nic nie kosztuje.
    /// Wszystko z PRAWDZIWEGO stanu swiata, nic nie jest losowane poza slowami i przesada:
    ///  - bandy w okolicy (do 70 jedn.): ludzie (MemberRoster), zloto = kiesa bandy (PartyTradeGold - bandy maja tylko to, co zrabuja: OutlawLaw), od ilu dni rabuja
    ///    (ksiega ponizej: doba narodzin bandy z MobilePartyCreated, zapis arm_rumours), herszt: bohater BK albo przezwisko deterministyczne (imie z listy kultury
    ///    krainy + przydomek z ziarna id bandy - ta sama banda zawsze tak samo);
    ///  - kryjowki (do 160 jedn., zajete przez bandy): miejsce, ile band, skarbiec = kasa kryjowki (SettlementComponent.Gold - skarbiec band OutlawLaw "nskarbiec",
    ///    to zabiera ten, kto kryjowke oczysci - HideoutPurge);
    ///  - wojska panow (armie do 180 jedn., inaczej najwiekszy oddzial pana): wodz, krolestwo, ludzie, co robia (oblezenie / marsz na / zbiorka);
    ///  - karawany (do 90 jedn.): wlasciciel, dokad, straz;
    ///  - ceny w 3 najblizszych miastach (do 260 jedn.): towar o najwiekszej roznicy ceny (Town.GetItemPrice - ta sama cena co w ekranie handlu);
    ///  - wiesci z wojen (ksiega 60 dni: wojny, pokoje, zdobyte twierdze, spalone wsie, wielkie bitwy, oblezenia) i trwajace oblezenia; bez wiesci - kto z kim wojuje.
    /// Dokladnosc: q = 1 - odleglosc/skala - wiek/30 (bandy, kryjowki, wojska skala 150, wiesci 600). q >= 0.75 - liczby zaokraglone ("about 40"), >= 0.45 - "some 40",
    /// nizej - przesada x1.4-3.0 ("hundreds of men", "a king's ransom"); miejsce: blisko i dokladnie - najblizsza nazwana osada, inaczej kierunek i dni jazdy.
    /// Te same plotki w tym samym miejscu tego samego dnia (ziarno = miejsce + doba). Ksiega: zapis "arm_rumours" (v1, straznik rozmiaru); brak klucza = bandy
    /// "od dawna" (bez liczby dni), wiesci od zera. Linia "Plotki (PL): dzien N"; tools/sprawdz_logi.py --grupa nzajazdy.
    /// </summary>
    internal static partial class Rumours   // m98-plotki: turnieje, swaty i kufel w Rumours.Drink.cs
    {
        private const string P = "Plotki (PL): ";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        internal static bool On { get { var s = Settings.Current; return s != null && s.InnGossip; } }
        // n20-plotki: stopnie slowne zamiast kwot / banda od herszta / strony w ramce - kazde pod wlasnym kluczem
        private static bool WordsOn { get { var s = Settings.Current; return s != null && s.InnGossip && s.InnGossipLootWords; } }
        private static bool NamesOn { get { var s = Settings.Current; return s != null && s.InnGossip && s.InnGossipLeaderNames; } }
        private static bool PagesOn { get { var s = Settings.Current; return s != null && s.InnGossip && s.InnGossipPages; } }

        // ------------------------------------------------------------ ksiega (zapis arm_rumours)
        // n20-plotki: Name/Female - nadane imie herszta (bez bohatera), zapis jako 5. i 6. pole wpisu bandy (stary DLL czyta 4 pierwsze - zgodne w obie strony)
        private sealed class Band { public int First; public bool Known; public int Raids; public string Name; public bool Female; }
        private sealed class News { public int Kind, Day, N; public float X, Y; public string A = "", B = "", C = ""; }
        private const int NWar = 1, NPeace = 2, NCaptured = 3, NRaided = 4, NBattle = 5, NSiege = 6;
        private const int NewsMax = 60, NewsDays = 60, BandsMax = 6000, SaveMaxChars = 400000;
        // n22f (blad galezi p5-kruki: NotYet przy ksiedze 60 z sw-zajazdy): przy RavenNews wiesc zostaje w ksiedze, dopoki moze jeszcze leciec z nia kruk (Ravens.HoldDays),
        // najwyzej NewsMaxRavens. Dowod (8 biegow 19/20.10, linia "wiesci N (nowe dzis M)"): 9.7-12.1 nowych wiesci na dobe, ksiega pelna (60) w 82-97% dob, wiec wiesc
        // zyla mediana 5-6 dob - karczma dalej niz ok. 580-730 jedn. (Pentos - Winterfell 643, Qarth - KP 1062) nie mogla jej uslyszec (NotYet czeka do 12 dob).
        // Ksiega z oknem krukow (HoldDays = 13 dob, wiesc w wieku 0-13) w symulacji tych biegow: mediana 145-178 wiesci, najwiecej 238 - sufit 300 z zapasem. Klucz wylaczony - 60 jak dotad.
        private const int NewsMaxRavens = 300;
        private static readonly Dictionary<string, Band> _bands = new Dictionary<string, Band>(StringComparer.Ordinal);
        private static readonly List<News> _news = new List<News>();

        // liczniki doby i sesji
        private static int _dHeard, _dHeardRoad, _dHeardTown, _dItems, _dEmpty, _dBandsNew, _dBandsGone, _dNews, _dStumbles, _stumbles;
        private static readonly int[] _dCat = new int[9];   // 0 bandy, 1 kryjowki, 2 wojska, 3 karawany, 4 ceny, 5 wojny, 6 pusto; m98-plotki: 7 turnieje, 8 swaty
        private static long _dBuildTicks;
        private static int _dBuilds;   // m98-plotki (recenzja gra): budowy puli doby (sluchanie bez kufla i pula kufla, takze z warunku opcji) - dzielnik "budowa srednio"
        // n20-plotki: liczniki doby - herszci (bohater / z ksiegi / nadane), stopnie lupu (kiesy band, skarbce kryjowek), okna i strony
        private static int _dNameHero, _dNameBook, _dNameNew, _dWindows, _dPages, _dPagesTurned, _dMaxLines;
        private static readonly int[] _dPurseTier = new int[6], _dHoardTier = new int[6];
        private static bool _errLogged;
        private static string _importNote = "brak klucza (stary zapis albo nowa kampania)";

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _dStumbles++;
            if (!_errLogged) { _errLogged = true; Log.Error("Rumours." + where, e); }
        }

        internal static void Reset()
        {
            _bands.Clear(); _news.Clear();
            _priceItems = null;   // recenzja: przedmioty gry z MBObjectManager tej kampanii - nastepna kampania (nowa gra / wczytanie) buduje liste od nowa
            DrinkReset();         // m98-plotki: pula miejsca i piwo tej kampanii
            _stumbles = 0; _errLogged = false; _importNote = "brak klucza (stary zapis albo nowa kampania)";
            DayReset();
        }

        private static void DayReset()
        {
            _dHeard = _dHeardRoad = _dHeardTown = _dItems = _dEmpty = _dBandsNew = _dBandsGone = _dNews = _dStumbles = 0;
            Array.Clear(_dCat, 0, _dCat.Length);
            _dBuildTicks = 0; _dBuilds = 0;
            _dNameHero = _dNameBook = _dNameNew = _dWindows = _dPages = _dPagesTurned = _dMaxLines = 0;
            Array.Clear(_dPurseTier, 0, _dPurseTier.Length); Array.Clear(_dHoardTier, 0, _dHoardTier.Length);
        }

        private static int Today { get { return (int)CampaignTime.Now.ToDays; } }

        // ------------------------------------------------------------ zdarzenia (ArmouryBehavior.RegisterEvents)
        internal static void OnPartyCreated(MobileParty mp)
        {
            if (!On || mp == null) return;
            try { if (mp.IsBandit && !string.IsNullOrEmpty(mp.StringId) && _bands.Count < BandsMax) { _bands[mp.StringId] = new Band { First = Today, Known = true }; _dBandsNew++; } }
            catch (Exception e) { Stumble("OnPartyCreated", e); }
        }

        internal static void OnPartyDestroyed(MobileParty mp, PartyBase by)
        {
            if (!On || mp == null) return;
            try { if (!string.IsNullOrEmpty(mp.StringId) && _bands.Remove(mp.StringId)) _dBandsGone++; }
            catch (Exception e) { Stumble("OnPartyDestroyed", e); }
        }

        private static string Clean(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length);
            foreach (char ch in s) sb.Append(ch == ',' || ch == ';' || ch == '|' || ch == '\n' || ch == '\r' ? ' ' : ch);
            return sb.Length > 60 ? sb.ToString(0, 60) : sb.ToString();
        }

        private static void AddNews(int kind, Vec2 at, string a, string b, string c, int n)
        {
            _news.Add(new News { Kind = kind, Day = Today, X = at.x, Y = at.y, A = Clean(a), B = Clean(b), C = Clean(c), N = n });
            _dNews++;
            // n22f: ponad NewsMax najstarsza wiesc odpada jak dotad - przy krukach dopiero, gdy jest starsza niz najdluzszy lot kruka (albo ksiega ponad NewsMaxRavens)
            int hold = Ravens.On ? Ravens.HoldDays : -1, today = Today;
            while (_news.Count > NewsMax && (hold < 0 || _news.Count > NewsMaxRavens || today - _news[0].Day > hold)) _news.RemoveAt(0);
        }

        internal static void OnRaidCompleted(BattleSideEnum winner, RaidEventComponent raid)
        {
            if (!On || raid == null) return;
            try
            {
                var st = raid.MapEventSettlement;
                if (st == null || winner != BattleSideEnum.Attacker) return;
                var lp = raid.AttackerSide != null ? raid.AttackerSide.LeaderParty : null;
                var mp = lp != null ? lp.MobileParty : null;
                if (mp != null && mp.IsBandit && !string.IsNullOrEmpty(mp.StringId)) { Band b; if (_bands.TryGetValue(mp.StringId, out b)) b.Raids++; }
                string who = lp != null && lp.LeaderHero != null ? lp.LeaderHero.Name.ToString() : lp != null ? lp.Name.ToString() : "raiders";
                string fac = lp != null && lp.MapFaction != null ? lp.MapFaction.Name.ToString() : "";
                // n20-plotki (InnGossipLeaderNames): banda w wiesci - "Jon Rivers and his bandits", bez "of Broken Men"
                if (mp != null && mp.IsBandit && NamesOn) { who = BandWho(mp); fac = ""; }
                AddNews(NRaided, st.GetPosition2D, who, fac, st.Name.ToString(), 0);
            }
            catch (Exception e) { Stumble("OnRaidCompleted", e); }
        }

        internal static void OnWarDeclared(IFaction a, IFaction b, DeclareWarAction.DeclareWarDetail d)
        {
            if (!On || a == null || b == null || !a.IsKingdomFaction || !b.IsKingdomFaction) return;
            try { AddNews(NWar, FactionPos(a), a.Name.ToString(), b.Name.ToString(), "", 0); }
            catch (Exception e) { Stumble("OnWarDeclared", e); }
        }

        internal static void OnMakePeace(IFaction a, IFaction b, MakePeaceAction.MakePeaceDetail d)
        {
            if (!On || a == null || b == null || !a.IsKingdomFaction || !b.IsKingdomFaction) return;
            try { AddNews(NPeace, FactionPos(a), a.Name.ToString(), b.Name.ToString(), "", 0); }
            catch (Exception e) { Stumble("OnMakePeace", e); }
        }

        internal static void OnOwnerChanged(Settlement st, bool openToClaim, Hero newOwner, Hero oldOwner, Hero capturer, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
        {
            if (!On || st == null || detail != ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.BySiege || !st.IsFortification) return;
            try
            {
                var who = capturer ?? newOwner;
                string fac = who != null && who.MapFaction != null ? who.MapFaction.Name.ToString() : "";
                AddNews(NCaptured, st.GetPosition2D, who != null ? who.Name.ToString() : "", fac, st.Name.ToString(), st.IsTown ? 1 : 0);
            }
            catch (Exception e) { Stumble("OnOwnerChanged", e); }
        }

        internal static void OnMapEventEnded(MapEvent me)
        {
            if (!On || me == null) return;
            try
            {
                if (!me.IsFieldBattle || !me.HasWinner) return;
                var w = me.Winner;
                var l = w == me.AttackerSide ? me.DefenderSide : me.AttackerSide;
                if (w == null || l == null || w.LeaderParty == null || l.LeaderParty == null) return;
                int men = w.HealthyTroopCountAtMapEventStart + l.HealthyTroopCountAtMapEventStart;
                if (men < 200) return;
                var wm = w.LeaderParty.MobileParty; var lm = l.LeaderParty.MobileParty;
                if ((wm == null || !wm.IsLordParty) && (lm == null || !lm.IsLordParty)) return;   // wielkie bitwy panow, nie potyczki band
                string wn = w.LeaderParty.LeaderHero != null ? w.LeaderParty.LeaderHero.Name.ToString() : w.LeaderParty.Name.ToString();
                string ln = l.LeaderParty.LeaderHero != null ? l.LeaderParty.LeaderHero.Name.ToString() : l.LeaderParty.Name.ToString();
                // n20-plotki (InnGossipLeaderNames): banda po jednej ze stron - od herszta, nie "Broken Men"
                if (NamesOn)
                {
                    if (wm != null && wm.IsBandit) wn = BandWho(wm);
                    if (lm != null && lm.IsBandit) ln = BandWho(lm);
                }
                Vec2 pos = me.Position.ToVec2();
                var near = NearestNamed(pos, 40f);
                AddNews(NBattle, pos, wn, ln, near != null ? near.Name.ToString() : "", men);
            }
            catch (Exception e) { Stumble("OnMapEventEnded", e); }
        }

        internal static void OnSiegeStarted(SiegeEvent se)
        {
            if (!On || se == null || se.BesiegedSettlement == null) return;
            try
            {
                var lp = se.BesiegerCamp != null ? se.BesiegerCamp.LeaderParty : null;
                string who = lp != null && lp.LeaderHero != null ? lp.LeaderHero.Name.ToString() : lp != null ? lp.Name.ToString() : "";
                string fac = lp != null && lp.MapFaction != null ? lp.MapFaction.Name.ToString() : "";
                AddNews(NSiege, se.BesiegedSettlement.GetPosition2D, who, fac, se.BesiegedSettlement.Name.ToString(), 0);
            }
            catch (Exception e) { Stumble("OnSiegeStarted", e); }
        }

        private static Vec2 FactionPos(IFaction f)
        {
            try
            {
                var k = f as Kingdom;
                var leader = k != null ? k.Leader : null;
                var seat = leader != null ? leader.HomeSettlement : null;
                if (seat != null) return seat.GetPosition2D;
                var mp = MobileParty.MainParty;
                return mp != null ? mp.GetPosition2D : Vec2.Zero;
            }
            catch { return Vec2.Zero; }
        }

        // ------------------------------------------------------------ doba
        /// <summary>Doba: ksiega band zgodna ze swiatem (zywe bandy bez wpisu - "od dawna", martwe - usuniete), wiesci starsze niz 60 dni - usuniete; linia logu.</summary>
        internal static void Daily(int day)
        {
            if (!On) { DayReset(); return; }
            var sw = Stopwatch.StartNew();
            int alive = 0, added = 0, removed = 0;
            try
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var p in MobileParty.AllBanditParties)
                {
                    if (p == null || string.IsNullOrEmpty(p.StringId)) continue;
                    alive++;
                    seen.Add(p.StringId);
                    if (!_bands.ContainsKey(p.StringId) && _bands.Count < BandsMax) { _bands[p.StringId] = new Band { First = Today, Known = false }; added++; }
                }
                var dead = new List<string>();
                foreach (var k in _bands.Keys) if (!seen.Contains(k)) dead.Add(k);
                foreach (var k in dead) _bands.Remove(k);
                removed = dead.Count;
                _news.RemoveAll(n => Today - n.Day > NewsDays);
            }
            catch (Exception e) { Stumble("Daily", e); }
            try
            {
                int known = 0; long ageSum = 0;
                foreach (var b in _bands.Values) if (b.Known) { known++; ageSum += Today - b.First; }
                double buildMs = _dBuilds > 0 ? _dBuildTicks * 1000.0 / Stopwatch.Frequency / _dBuilds : 0.0;   // m98-plotki: srednio na budowe (dotad na sluchanie - to samo bez kufla)
                Log.Info(P + "dzien " + day + " | klucz Inn Gossip tak | sluchane " + _dHeard + " (zajazdy " + _dHeardRoad + ", karczmy miast " + _dHeardTown + "), plotek " + _dItems
                         + " (bandy " + _dCat[0] + ", kryjowki " + _dCat[1] + ", wojska " + _dCat[2] + ", karawany " + _dCat[3] + ", ceny " + _dCat[4] + ", wojny " + _dCat[5] + "), bez plotek " + _dEmpty
                         + ", budowa srednio " + buildMs.ToString("0.00", Inv) + " ms"
                         + " | ksiega band: zywych w swiecie " + alive + ", wpisow " + _bands.Count + " (z doba narodzin " + known + ", srednio " + (known > 0 ? (ageSum / (double)known).ToString("0.0", Inv) : "-")
                         + " dni), nowe dzis " + _dBandsNew + ", rozbite dzis " + _dBandsGone + ", dopisane bez doby " + added + ", usuniete martwe " + removed
                         + " | wiesci " + _news.Count + " (nowe dzis " + _dNews + ")"
                         + " | porzadki " + sw.ElapsedMilliseconds + " ms | wyjatki budowy dzis " + _dStumbles + " (od wczytania " + _stumbles + ").");
                try { TalesLine(day); } catch (Exception e) { Stumble("TalesLine", e); }   // n20-plotki: osobna linia (linia doby wyzej ma stala postac dla sprawdz_logi.py)
            }
            catch (Exception e) { Stumble("DailyLog", e); }
            finally { DayReset(); }
        }

        internal static void SessionStart()
        {
            try
            {
                Log.Info(P + "start sesji | klucz Inn Gossip " + (On ? "tak" : "WYLACZONE") + " | ksiega z zapisu: " + _importNote + " | potkniecia " + _stumbles + ".");
            }
            catch (Exception e) { Stumble("SessionStart", e); }
        }

        // ------------------------------------------------------------ zapis
        /// <summary>"v1|id,doba,znana,napady;...|rodzaj,doba,x,y,n,a,b,c;..." - straznik rozmiaru (najwyzej BandsMax band, wiesci NewsMax, przy krukach NewsMaxRavens; ponad SaveMaxChars bandy pominiete).
        /// Klucz wylaczony - "" (nic do zapisu).</summary>
        internal static string Export()
        {
            if (!On) return "";
            try
            {
                var sb = new StringBuilder("v1|");
                int n = 0;
                foreach (var kv in _bands)
                {
                    if (kv.Key.IndexOfAny(new[] { ',', ';', '|' }) >= 0 || kv.Key.Length > 80) continue;
                    if (n++ > 0) sb.Append(';');
                    sb.Append(kv.Key).Append(',').Append(kv.Value.First.ToString(Inv)).Append(',').Append(kv.Value.Known ? '1' : '0').Append(',').Append(kv.Value.Raids.ToString(Inv));
                    if (!string.IsNullOrEmpty(kv.Value.Name)) sb.Append(',').Append(Clean(kv.Value.Name)).Append(',').Append(kv.Value.Female ? '1' : '0');   // n20-plotki: imie herszta
                    if (sb.Length > SaveMaxChars) { sb.Length = 3; break; }   // straznik: bandy pominiete (wiesci zostaja)
                }
                sb.Append('|');
                for (int i = 0; i < _news.Count; i++)
                {
                    var w = _news[i];
                    if (i > 0) sb.Append(';');
                    sb.Append(w.Kind.ToString(Inv)).Append(',').Append(w.Day.ToString(Inv)).Append(',').Append(w.X.ToString("0.0", Inv)).Append(',').Append(w.Y.ToString("0.0", Inv))
                      .Append(',').Append(w.N.ToString(Inv)).Append(',').Append(w.A).Append(',').Append(w.B).Append(',').Append(w.C);
                }
                return sb.ToString();
            }
            catch (Exception e) { Stumble("Export", e); return ""; }
        }

        internal static void Import(string data)
        {
            _bands.Clear(); _news.Clear();
            if (string.IsNullOrEmpty(data)) { _importNote = "brak klucza (stary zapis albo nowa kampania)"; return; }
            if (data.Length > SaveMaxChars * 3) { _importNote = "zapis za dlugi (" + data.Length + ") - pominiety"; return; }
            try
            {
                var parts = data.Split('|');
                if (parts.Length < 3 || parts[0] != "v1") { _importNote = "nieznany format - pominiety"; return; }
                int bad = 0, named = 0;
                foreach (var e in parts[1].Split(';'))
                {
                    if (e.Length == 0) continue;
                    var a = e.Split(',');
                    int first, raids;
                    if (a.Length < 4 || !int.TryParse(a[1], NumberStyles.Integer, Inv, out first) || !int.TryParse(a[3], NumberStyles.Integer, Inv, out raids)) { bad++; continue; }
                    if (_bands.Count < BandsMax)
                    {
                        var nb = new Band { First = first, Known = a[2] == "1", Raids = Math.Max(0, raids) };
                        if (a.Length >= 6 && a[4].Trim().Length > 0) { nb.Name = a[4].Trim(); nb.Female = a[5] == "1"; named++; }   // n20-plotki: imie herszta z zapisu
                        _bands[a[0]] = nb;
                    }
                }
                foreach (var e in parts[2].Split(';'))
                {
                    if (e.Length == 0) continue;
                    var a = e.Split(',');
                    int kind, day, n; float x, y;
                    if (a.Length < 8 || !int.TryParse(a[0], NumberStyles.Integer, Inv, out kind) || !int.TryParse(a[1], NumberStyles.Integer, Inv, out day)
                        || !float.TryParse(a[2], NumberStyles.Float, Inv, out x) || !float.TryParse(a[3], NumberStyles.Float, Inv, out y)
                        || !int.TryParse(a[4], NumberStyles.Integer, Inv, out n) || kind < NWar || kind > NSiege) { bad++; continue; }
                    if (_news.Count < NewsMaxRavens) _news.Add(new News { Kind = kind, Day = day, X = x, Y = y, N = n, A = a[5], B = a[6], C = a[7] });   // n22f: ksiega z krukami
                }
                if (!Ravens.On && _news.Count > NewsMax) _news.RemoveRange(0, _news.Count - NewsMax);   // n22f: klucz wylaczony - 60 najnowszych od razu po wczytaniu, jak AddNews
                int fixedOld = NamesOn ? OldBandNames() : 0;
                _importNote = "band " + _bands.Count + " (z imieniem herszta " + named + "), wiesci " + _news.Count + (bad > 0 ? ", zlych wpisow " + bad : "")
                              + (fixedOld > 0 ? ", przerobione stare wiesci z nazwa bandy " + fixedOld : "");
            }
            catch (Exception e) { _bands.Clear(); _news.Clear(); _importNote = "blad odczytu - pominiety"; Stumble("Import", e); }
        }

        /// <summary>Przeglad calosci 20.10: wiesci zapisane przed n20-plotki (WGRANIE 88) maja w A/B nazwe klanu band (BanditPartyComponent.Name = MapFaction.Name,
        /// np. "Broken Men") - przy InnGossipLeaderNames zamiana na "a band of outlaws" (spalone wsie i bitwy). Zwraca liczbe przerobionych. Wyjatek - bez zmian.</summary>
        private static int OldBandNames()
        {
            int n = 0;
            try
            {
                if (Campaign.Current == null || _news.Count == 0) return 0;
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var c in Clan.BanditFactions) if (c != null && c.Name != null) names.Add(c.Name.ToString());
                if (names.Count == 0) return 0;
                const string Band = "a band of outlaws";
                foreach (var w in _news)
                {
                    if (w.Kind == NRaided && (names.Contains(w.A ?? "") || names.Contains(w.B ?? ""))) { w.A = Band; w.B = ""; n++; }
                    else if (w.Kind == NBattle)
                    {
                        bool ch = false;
                        if (names.Contains(w.A ?? "")) { w.A = Band; ch = true; }
                        if (names.Contains(w.B ?? "")) { w.B = Band; ch = true; }
                        if (ch) n++;
                    }
                }
            }
            catch (Exception e) { Stumble("OldBandNames", e); }
            return n;
        }

        // ------------------------------------------------------------ menu
        internal static bool TownGossipCondition(MenuCallbackArgs args)
        {
            try
            {
                args.optionLeaveType = GameMenuOption.LeaveType.Conversation;
                MBTextManager.SetTextVariable("ARM_GOSSIP_TOWN", "Listen to the gossip");   // m98-plotki: etykieta z cena kufla przy InnGossipNeedsDrink
                var st = Settlement.CurrentSettlement;
                if (!On || !TownInns.On || st == null || !st.IsTown) return false;
                if (InnDrink.On) return DrinkCondition(args, "ARM_GOSSIP_TOWN", st.GetPosition2D, st.StringId, st, st.Town != null ? null : "Nobody pours ale here now.");
                args.Tooltip = new TextObject("{=!}Sit by the fire with a cup and listen to what the drinkers and the innkeeper say - outlaws, lairs, hosts on the march, caravans, prices and the wars.");
                return true;
            }
            catch (Exception e) { Stumble("TownGossipCondition", e); return false; }
        }

        internal static void TownGossipConsequence(MenuCallbackArgs args)
        {
            try
            {
                var st = Settlement.CurrentSettlement;
                if (st == null) return;
                if (InnDrink.On) { DrinkConsequence(st.GetPosition2D, st.StringId, st.Name.ToString(), true, st, null); return; }   // m98-plotki: kufel przed plotkami
                _dHeardTown++;
                Show(st.GetPosition2D, st.StringId, st.Name.ToString(), true);
            }
            catch (Exception e) { Stumble("TownGossipConsequence", e); }
        }

        internal static bool RoadGossipCondition(MenuCallbackArgs args)
        {
            try
            {
                args.optionLeaveType = GameMenuOption.LeaveType.Conversation;
                MBTextManager.SetTextVariable("ARM_GOSSIP_ROAD", "Listen to the gossip");   // m98-plotki: etykieta z cena kufla przy InnGossipNeedsDrink
                if (!On || !RoadsideInns.On) return false;
                var inn = RoadsideInns.CurrentOrNear();
                if (inn == null) return false;
                if (InnDrink.On) { var at = new Vec2(inn.X, inn.Y); return DrinkCondition(args, "ARM_GOSSIP_ROAD", at, inn.Uid, RoadTown(inn, at), TownInns.RoadDrinkBarred(inn)); }
                args.Tooltip = new TextObject("{=!}Drovers, pilgrims and pedlars by the hearth trade the news of the road - the nearer and fresher, the truer.");
                return true;
            }
            catch (Exception e) { Stumble("RoadGossipCondition", e); return false; }
        }

        internal static void RoadGossipConsequence(MenuCallbackArgs args)
        {
            try
            {
                var inn = RoadsideInns.CurrentOrNear();
                if (inn == null) return;
                if (InnDrink.On) { DrinkConsequence(new Vec2(inn.X, inn.Y), inn.Uid, inn.Name, false, null, inn); return; }   // m98-plotki: kufel przed plotkami
                _dHeardRoad++;
                Show(new Vec2(inn.X, inn.Y), inn.Uid, inn.Name, false);
            }
            catch (Exception e) { Stumble("RoadGossipConsequence", e); }
        }

        // ------------------------------------------------------------ plotki
        // m98-plotki: Ref - id turnieju (miasto) / kandydata (bohater) do sprawdzenia prawdy; Id - tozsamosc wiesci do ksiegi uslyszanych (recenzja gra: ten sam
        // turniej, ta sama banda w tym samym miejscu = ta sama plotka, choc tekst zmienia sie z dniem, przesada i godzina); null - tekst
        private sealed class Item { public int Cat; public float Score; public string Text; public string Ref; public string Id; }

        /// <summary>m98-plotki: miejsce wiesci do tozsamosci - najblizsza osada do 25 jedn. (jak "near X" w Where), dalej kratka 50 jedn. Banda, ktora przeszla w inne
        /// miejsce, to nowa wiesc; ta sama banda w tym samym miejscu - nie.</summary>
        private static string PlaceId(Vec2 p)
        {
            var s = NearestNamed(p, 25f);
            if (s != null && s.StringId != null) return s.StringId;
            return "g" + ((int)Math.Floor(p.X / 50f)).ToString(Inv) + "_" + ((int)Math.Floor(p.Y / 50f)).ToString(Inv);
        }

        // m98-plotki: given/headGiven - plotki wybrane za kufel (Rumours.Drink) i naglowek z zaplata; null - jak dotad (cala pula, zwykly naglowek)
        private static void Show(Vec2 at, string key, string place, bool town, List<Item> given = null, string headGiven = null)
        {
            long t0 = Stopwatch.GetTimestamp();
            List<Item> items;
            if (given != null) items = given;
            else
            {
                try { items = Build(at, key); }
                catch (Exception e) { Stumble("Build", e); items = new List<Item>(); }
                _dBuildTicks += Stopwatch.GetTimestamp() - t0; _dBuilds++;
            }
            _dHeard++;
            string head = headGiven ?? (town ? "The innkeeper leans on his counter, and the drinkers are happy to talk:" : "By the hearth, drovers, pilgrims and pedlars trade the news of the road:");
            var cats = new int[NewCats];
            if (items.Count == 0) { _dEmpty++; cats[6]++; }
            foreach (var it in items) { cats[it.Cat]++; _dCat[it.Cat]++; _dItems++; }
            // n20-plotki (InnGossipPages): strony mieszczace sie w ramce; klucz wylaczony - jedno okno jak dotad
            var lines = new List<int>();
            List<string> pages;
            if (PagesOn) pages = Paginate(head, items, lines);
            else
            {
                var sb = new StringBuilder(head);
                if (items.Count == 0) sb.Append("\n\n").Append(Empty);
                foreach (var it in items) sb.Append("\n\n- ").Append(it.Text);
                pages = new List<string> { sb.ToString() };
                lines.Add(EstLines(pages[0]));
            }
            _dWindows++; _dPages += pages.Count;
            foreach (var n in lines) if (n > _dMaxLines) _dMaxLines = n;
            double ms = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
            Log.Info(P + "sluchane w " + place + " (" + (town ? "karczma miasta" : "zajazd") + ", " + key + ") dzien " + Today + " - plotek " + items.Count + " (bandy " + cats[0] + ", kryjowki " + cats[1]
                     + ", wojska " + cats[2] + ", karawany " + cats[3] + ", ceny " + cats[4] + ", wojny " + cats[5]
                     + (TournOn || MatchOn || cats[CatTourn] + cats[CatMatch] > 0 ? ", turnieje " + cats[CatTourn] + ", swaty " + cats[CatMatch] : "") + "), czas " + ms.ToString("0.0", Inv) + " ms"
                     + " | strony " + pages.Count + " (linie ok. " + JoinInts(lines) + ", miesci sie " + PageLines + ")" + (PagesOn ? "" : " - klucz Inn Gossip Pages WYLACZONE") + ".");
            // m100-kryjowki: plotka o kryjowce znana dopiero, gdy gracz zobaczy strone z nia (recenzja 21.10: "Enough talk" na 1. stronie - dalsze nieprzeczytane)
            List<List<string>> heard = null;
            foreach (var it in items)
            {
                if (it == null || it.Id == null || !it.Id.StartsWith("h|")) continue;
                if (heard == null) { heard = new List<List<string>>(); for (int k = 0; k < pages.Count; k++) heard.Add(null); }
                int pg = 0;
                for (int k = 0; k < pages.Count; k++) if (it.Text != null && pages[k].Contains(it.Text)) { pg = k; break; }
                if (heard[pg] == null) heard[pg] = new List<string>();
                heard[pg].Add(it.Id.Substring(2));
            }
            ShowPage(place, pages, 0, heard);
        }

        // ------------------------------------------------------------ strony w ramce (n20-plotki, klucz InnGossipPages)
        // Ramka okna gry (prefab SingleQueryPopup z ROT-Content) nie przewija opisu: tekst ma najwyzej 480 px wysokosci, a cala lista okna 645 px
        // (tytul ok. 83 + marginesy opisu 40 + przyciski 70) - na opis zostaje ok. 452 px = 19 linii pisma FiraSansExtraCondensed 20 (wysokosc linii 38/32 x 20 = 23.75 px).
        // Szerokosc opisu 512 - 2 x 30 = 452 px. Liczymy linie z szerokosci znakow czcionki gry (xadvance z .fnt, rozmiar 32) z zapasem: zawijanie przy 430 px, strona do 17 linii.
        private const string Empty = "The talk is all of the weather and the harvest - nothing worth a copper.";
        private const float WrapPx = 430f, FontScale = 20f / 32f;
        internal const int PageLines = 17;
        private static readonly byte[] Adv =
        {
            6,7,11,13,14,22,19,6,8,8,12,13,6,10,6,13,15,11,12,13,14,13,14,11,14,14,6,6,13,13,13,12,
            27,15,16,14,17,13,12,16,17,7,8,15,12,21,17,18,15,18,16,14,13,17,15,22,14,14,13,8,13,8,14,13,
            8,14,15,12,15,14,9,13,15,7,7,13,7,22,15,15,15,15,10,12,9,15,13,19,13,13,11,8,10,8,12
        };

        private static float WordPx(string w)
        {
            int u = 0;
            foreach (char ch in w) u += ch >= 32 && ch <= 126 ? Adv[ch - 32] : 16;
            return u * FontScale;
        }

        /// <summary>Linie tekstu w ramce okna (zawijanie po slowach przy WrapPx; pusty akapit = 1 linia).</summary>
        internal static int EstLines(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            float space = Adv[0] * FontScale;
            int lines = 0;
            foreach (var para in text.Split('\n'))
            {
                lines++;
                float cur = 0f;
                foreach (var word in para.Split(' '))
                {
                    if (word.Length == 0) continue;
                    float w = WordPx(word);
                    if (cur <= 0f) cur = w;
                    else if (cur + space + w <= WrapPx) cur += space + w;
                    else { lines++; cur = w; }
                    while (cur > WrapPx) { lines++; cur -= WrapPx; }   // slowo dluzsze niz linia (nie zdarza sie - na wszelki wypadek liczy, nie psuje)
                }
            }
            return lines;
        }

        /// <summary>Plotki na strony: naglowek na pierwszej, wpisy w kolejnosci, pusta linia miedzy wpisami; nowa strona, gdy wpis by sie nie zmiescil (PageLines).</summary>
        private static List<string> Paginate(string head, List<Item> items, List<int> lines)
        {
            var pages = new List<string>();
            var sb = new StringBuilder(head);
            int n = EstLines(head), onPage = 0;
            var texts = new List<string>();
            if (items.Count == 0) texts.Add(Empty);
            foreach (var it in items) texts.Add("- " + it.Text);
            foreach (var t in texts)
            {
                int need = EstLines(t) + 1;   // + pusta linia przed wpisem
                if (onPage > 0 && n + need > PageLines)
                {
                    pages.Add(sb.ToString()); lines.Add(n);
                    sb.Length = 0; n = 0; onPage = 0;
                }
                if (sb.Length > 0) sb.Append("\n\n"); else need--;
                sb.Append(t); n += need; onPage++;
            }
            pages.Add(sb.ToString()); lines.Add(n);
            return pages;
        }

        /// <summary>Okno strony i: "Hear more" (nastepna strona - gra kolejkuje okno i pokazuje je po zamknieciu tego, GauntletQueryManager.CloseQuery) i "Enough talk";
        /// ostatnia strona - samo "Enough talk" jak dotad.</summary>
        private static void ShowPage(string place, List<string> pages, int i, List<List<string>> heard = null)
        {
            if (heard != null && i < heard.Count && heard[i] != null) foreach (var id in heard[i]) HideoutSight.Heard(id, place);   // m100-kryjowki
            bool more = i + 1 < pages.Count;
            string title = place + " - gossip" + (pages.Count > 1 ? " (" + (i + 1).ToString(Inv) + "/" + pages.Count.ToString(Inv) + ")" : "");
            Action next = null;
            if (more)
            {
                int k = i + 1;
                next = () => { try { _dPagesTurned++; ShowPage(place, pages, k, heard); } catch (Exception e) { Stumble("NextPage", e); } };
            }
            InformationManager.ShowInquiry(new InquiryData(title, pages[i], true, more, more ? "Hear more" : "Enough talk", more ? "Enough talk" : null, next, null), true);
        }

        private static string JoinInts(List<int> v)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < v.Count; i++) { if (i > 0) sb.Append('+'); sb.Append(v[i].ToString(Inv)); }
            return sb.ToString();
        }

        /// <summary>Plotki dla miejsca (deterministycznie: miejsce + doba). Najwyzej 2 o bandach, 1 o kryjowce, 1 o wojsku, 1 o karawanie, 1 o cenach, 2 o wojnach.</summary>
        private static List<Item> Build(Vec2 at, string key)
        {
            int today = Today;
            uint seed = MapVillageData.Fnv(key + "|" + today.ToString(Inv));
            var all = new List<Item>();
            try { Bandits(all, at, seed, today); } catch (Exception e) { Stumble("Bandits", e); }
            try { Hideouts(all, at, seed); } catch (Exception e) { Stumble("Hideouts", e); }
            try { Armies(all, at, seed); } catch (Exception e) { Stumble("Armies", e); }
            try { Caravans(all, at, seed); } catch (Exception e) { Stumble("Caravans", e); }
            try { Prices(all, at, seed); } catch (Exception e) { Stumble("Prices", e); }
            try { Wars(all, at, seed, today); } catch (Exception e) { Stumble("Wars", e); }
            try { if (TournOn) Tournaments(all, at, seed); } catch (Exception e) { Stumble("Tournaments", e); }   // m98-plotki (GossipTournaments)
            try { if (MatchOn) Matches(all, at, seed); } catch (Exception e) { Stumble("Matches", e); }           // m98-plotki (GossipMatchmaking)
            all.Sort((x1, x2) => x2.Score.CompareTo(x1.Score));
            var res = new List<Item>();
            // m98-plotki: dawne kategorie najwyzej 6 jak dotad; turnieje i swaty po 2 na wlasnych miejscach (klucze wylaczone - pula jak przed galezia)
            var cap = new[] { 2, 1, 1, 1, 1, 2, 0, 2, 2 };
            var used = new int[NewCats];
            int oldN = 0;
            foreach (var it in all)
            {
                bool added = it.Cat >= CatTourn;
                if (!added && oldN >= 6) continue;
                if (used[it.Cat] >= cap[it.Cat]) continue;
                used[it.Cat]++;
                if (!added) oldN++;
                res.Add(it);
            }
            return res;
        }

        // ------------------------------------------------------------ dokladnosc, liczby, miejsca
        private static float Exact(float dist, int age, float scale) { return Math.Max(0.05f, Math.Min(1f, 1f - dist / scale - age / 30f)); }

        internal static int Round(int n)
        {
            if (n < 20) return n;
            if (n < 100) return (int)(Math.Round(n / 5.0) * 5);
            if (n < 1000) return (int)(Math.Round(n / 10.0) * 10);
            if (n < 10000) return (int)(Math.Round(n / 100.0) * 100);
            return (int)(Math.Round(n / 1000.0) * 1000);
        }

        private static int Coarse(int n)
        {
            if (n < 10) return n;
            double m = Math.Pow(10, Math.Floor(Math.Log10(n)));
            return (int)(Math.Round(n / m) * m);
        }

        private static double Blow(uint seed) { return 1.4 + (seed % 100u) / 100.0 * 1.6; }

        /// <summary>Liczba ludzi wedlug dokladnosci: "about 40" / "some 40" / przesada ("as many as 90", "hundreds of", "thousands of"); dalej idzie rzeczownik.</summary>
        private static string Men(int n, float q, uint seed)
        {
            if (n <= 0) return "a handful of";
            if (q >= 0.75f) return "about " + Round(n).ToString("N0", Inv);
            if (q >= 0.45f) return "some " + Coarse(n).ToString("N0", Inv);
            int e = (int)(n * Blow(seed));
            if (e >= 1000) return "thousands of";
            if (e >= 200) return "hundreds of";
            return "as many as " + Coarse(e).ToString("N0", Inv);
        }

        private static string Coin(int g, float q, uint seed)
        {
            if (q >= 0.75f) return "about " + Round(g).ToString("N0", Inv) + " denars";
            if (q >= 0.45f) return "near " + Coarse(g).ToString("N0", Inv) + " denars";
            int e = (int)(g * Blow(seed));
            if (e >= 20000) return "a king's ransom";
            if (e >= 5000) return "chests of silver";
            return "as much as " + Coarse(e).ToString("N0", Inv) + " denars";
        }

        // n20-plotki (InnGossipLootWords): stopnie lupu zamiast kwot. Skala z gry Jeffa 19.10 (log 14:41): ok. 930 band ma razem ok. 204 tys. (srednio ok. 220 na bande),
        // skarbce 58 kryjowek ok. 93 tys. (srednio ok. 1 600, najwiekszy ok. 7 200). Progi: do 50 - bieda, do 250 - pare monet, do 700 - niezly lup, do 1 800 - bogaty,
        // do 4 500 - wielki, wyzej - ogromny skarb. Jedna skala dla kies band i skarbcow (slowo znaczy wszedzie to samo). Daleko (dokladnosc < 0.45) plotka rosnie x1.4-3.0.
        private static readonly int[] LootSteps = { 50, 250, 700, 1800, 4500 };
        private static readonly string[] PurseWords = { "nothing", "a few stolen coins", "a fair haul", "rich spoils", "a great haul of plunder", "a huge hoard" };
        private static readonly string[] HoardWords = { "little", "a little coin", "a fair hoard", "a rich hoard", "a great hoard", "a huge hoard" };

        private static int LootTier(int g, float q, uint seed)
        {
            double e = q >= 0.45f ? g : g * Blow(seed);
            int t = 0;
            while (t < LootSteps.Length && e > LootSteps[t]) t++;
            return t;
        }

        private static string Dir(Vec2 from, Vec2 to)
        {
            double a = Math.Atan2(to.y - from.y, to.x - from.x) * 180.0 / Math.PI;   // x na wschod, y na polnoc (mapa ROT: Polnoc u gory, Essos na wschodzie)
            string[] d = { "east", "north-east", "north", "north-west", "west", "south-west", "south", "south-east" };
            int i = (int)Math.Floor(((a + 360.0 + 22.5) % 360.0) / 45.0);
            return d[Math.Max(0, Math.Min(7, i))];
        }

        private static string Days(float d)
        {
            if (d < 22f) return "half a day's ride";
            if (d < 60f) return "a day's ride";
            if (d < 110f) return "two days' ride";
            if (d < 160f) return "three days' ride";
            return "many days' ride";
        }

        /// <summary>Najblizsza nazwana osada (miasto, zamek, wies - bez kryjowek) do r jedn.</summary>
        private static Settlement NearestNamed(Vec2 p, float r)
        {
            Settlement best = null;
            float bd = r;
            foreach (var s in Settlement.All)
            {
                if (s == null || !(s.IsTown || s.IsCastle || s.IsVillage)) continue;
                float d = s.GetPosition2D.Distance(p);
                if (d < bd) { bd = d; best = s; }
            }
            return best;
        }

        /// <summary>Miejsce: blisko i dokladnie - "near X" (najblizsza osada); inaczej "two days' ride to the north-east".</summary>
        private static string Where(Vec2 at, Vec2 p, float q)
        {
            float d = at.Distance(p);
            if (q >= 0.5f && d <= 70f)
            {
                var s = NearestNamed(p, 25f);
                if (s != null) return "near " + s.Name;
            }
            if (d < 6f) return "close by";
            return Days(d) + " to the " + Dir(at, p);
        }

        // ------------------------------------------------------------ bandy
        private static readonly string[] Pre = { "Bald", "Red", "Black", "Mad", "Old", "Lame", "Long", "Big", "Little", "Grim", "Crooked", "Toothless", "Pale", "Ugly",
                                                 "Fat", "Dirty", "Quiet", "Grey", "One-Eyed", "Scarred" };
        private static readonly string[] Post = { "the Butcher", "Crowfoot", "Half-Ear", "the Flayer", "Sweetbread", "the Knife", "Ironjaw", "the Weasel", "Stonehand",
                                                  "the Toad", "Pigface", "the Fox", "Three-Fingers", "No-Nose", "the Hangman", "Kettlebelly" };
        private static readonly string[] Plain = { "Jon", "Will", "Tom", "Harry", "Jory", "Rolf", "Dick", "Wat", "Hal", "Ned", "Gerold", "Osric", "Hugh", "Alyn" };

        private static Settlement NearestTown(Vec2 p)
        {
            Settlement best = null; float bd = float.MaxValue;
            foreach (var t in Town.AllTowns)
            {
                var s = t != null ? t.Settlement : null;
                if (s == null) continue;
                float d = s.GetPosition2D.Distance(p);
                if (d < bd) { bd = d; best = s; }
            }
            return best;
        }

        /// <summary>Herszt bandy: bohater (BK) albo przezwisko deterministyczne z id bandy - imie z listy kultury krainy (najblizsze miasto), przydomek z listy.</summary>
        private static string Leader(MobileParty p)
        {
            if (p.LeaderHero != null) return p.LeaderHero.Name.ToString();
            uint h = MapVillageData.Fnv(p.StringId ?? "band");
            string first = null;
            try
            {
                var town = NearestTown(p.GetPosition2D);
                var c = town != null ? town.Culture : null;
                var list = c != null ? c.MaleNameList : null;
                if (list != null && list.Count > 0) first = list[(int)(h % (uint)list.Count)].ToString();
            }
            catch { first = null; }
            if (string.IsNullOrEmpty(first) || first.IndexOf('{') >= 0) first = Plain[h % (uint)Plain.Length];
            return ((h >> 9) & 1u) == 0 ? Pre[(h >> 10) % (uint)Pre.Length] + " " + first : first + " " + Post[(h >> 10) % (uint)Post.Length];
        }

        // n20-plotki (InnGossipLeaderNames): banda od herszta - imie i nazwisko. Westeros: nazwisko bekarta krainy (Snow, Rivers, Hill...) albo nazwisko prostego ludu;
        // Wolni Ludzie - przydomek; Essos - "z miasta" (tam nie ma nazwisk). Kraina = kultura najblizszego miasta od DOMU bandy (kryjowka / osada bandy; bez domu - miejsce
        // bandy przy pierwszym nadaniu), imie z list imion tej kultury w grze. Nadane imie zostaje w ksiedze band (zapis arm_rumours) - ta sama banda zawsze tak samo.
        private static readonly string[] PlainF = { "Alys", "Jeyne", "Bella", "Wynafryd", "Mya", "Hilda", "Sarra", "Ryella" };
        private static readonly string[] Smallfolk = { "Tanner", "Fletcher", "Cooper", "Thatcher", "Miller", "Potter", "Cobb", "Heddle", "Barleycorn", "Greenhands", "Marsh", "Brook", "Hook", "Shepherd" };
        private static readonly string[] FreeBy = { "Bonebreaker", "Frostfinger", "Longspear", "Redbeard", "Wolfskin", "Stonecrow", "Icehand", "Crowkiller", "Ironbelly", "Thornhide" };
        private static readonly string[] Kinds = { "bandits", "outlaws", "cutthroats" };

        /// <summary>Nazwisko bekarta krainy Westeros wedlug id kultury ROT (null - brak: Wolni Ludzie / Essos / nieznana).</summary>
        private static string Bastard(string cid)
        {
            switch (cid)
            {
                case "battania": case "skagosi": case "nightswatch": case "whitewalker": return "Snow";
                case "river": return "Rivers";
                case "vlandia": return "Hill";
                case "reach": return "Flowers";
                case "vale": return "Stone";
                case "crownlands": case "dragonstone": return "Waters";
                case "stormlands": return "Storm";
                case "aserai": return "Sand";
                case "sturgia": return "Pyke";
                default: return null;
            }
        }

        private static string MakeName(MobileParty p, out bool female)
        {
            uint h = MapVillageData.Fnv((p.StringId ?? "band") + "|herszt");
            female = h % 10u == 0u;   // co dziesiaty herszt bez bohatera to kobieta
            Vec2 home = p.GetPosition2D;
            try { var hs = p.HomeSettlement; if (hs != null) home = hs.GetPosition2D; } catch { }
            Settlement town = null;
            try { town = NearestTown(home); } catch { town = null; }
            var c = town != null ? town.Culture : null;
            string first = null;
            try
            {
                var list = c == null ? null : female ? c.FemaleNameList : c.MaleNameList;
                if (list != null && list.Count > 0) first = list[(int)((h >> 4) % (uint)list.Count)].ToString();
            }
            catch { first = null; }
            if (string.IsNullOrEmpty(first) || first.IndexOf('{') >= 0) first = female ? PlainF[(h >> 4) % (uint)PlainF.Length] : Plain[(h >> 4) % (uint)Plain.Length];
            string cid = c != null && c.StringId != null ? c.StringId : "";
            uint r = h >> 12;
            string sur;
            string bast = Bastard(cid);
            if (bast != null) sur = (r & 1u) == 0 ? bast : Smallfolk[(r >> 1) % (uint)Smallfolk.Length];
            else if (cid == "freefolk" || cid == "wildlings") sur = FreeBy[(r >> 1) % (uint)FreeBy.Length];
            else if (town != null && cid.Length > 0) sur = "of " + town.Name;
            else sur = Smallfolk[(r >> 1) % (uint)Smallfolk.Length];
            return Clean(first + " " + sur).Trim();
        }

        /// <summary>Herszt bandy: bohater - pelne imie; inaczej imie z ksiegi band albo nadane teraz (i zapisane w ksiedze).</summary>
        private static string BandLeader(MobileParty p, out bool female)
        {
            var hero = p.LeaderHero;
            if (hero != null) { female = hero.IsFemale; _dNameHero++; return hero.Name.ToString(); }
            Band b = null;
            if (!string.IsNullOrEmpty(p.StringId)) _bands.TryGetValue(p.StringId, out b);
            if (b != null && !string.IsNullOrEmpty(b.Name)) { female = b.Female; _dNameBook++; return b.Name; }
            string name = MakeName(p, out female);
            _dNameNew++;
            if (b != null) { b.Name = name; b.Female = female; }
            return name;
        }

        /// <summary>m102-tablica (listy gonczne kapitana strazy): nazwa bandy jak w plotkach ("Jon Rivers and his bandits"); klucze plotek z imionami wylaczone albo blad - null
        /// (tablica bierze nazwe gry). Wolane raz przy wywieszeniu listu (nazwa zostaje w liscie).</summary>
        internal static string BandName(MobileParty p)
        {
            try { return p != null && NamesOn ? BandWho(p) : null; }
            catch (Exception e) { Stumble("BandName", e); return null; }
        }

        /// <summary>"Jon Rivers and his bandits" / "Alys Snow and her outlaws" - rodzaj ludzi staly dla bandy (z id).</summary>
        private static string BandWho(MobileParty p)
        {
            bool female;
            string leader = BandLeader(p, out female);
            uint h = MapVillageData.Fnv((p.StringId ?? "band") + "|ludzie");
            return leader + " and " + (female ? "her " : "his ") + Kinds[h % (uint)Kinds.Length];
        }

        // ------------------------------------------------------------ linia doby n20-plotki + proba przy graczu
        /// <summary>Linia "Plotki (PL): opowiesci dzien N": klucze, herszci, stopnie lupu, okna i strony; proba = plotki przy graczu bez okna (autotest nie klika plotek).</summary>
        private static void TalesLine(int day)
        {
            string probe = "-";
            var mp = MobileParty.MainParty;
            if (mp != null)
            {
                List<Item> items = null;
                try { items = Build(mp.GetPosition2D, "proba"); } catch (Exception e) { Stumble("Probe", e); }
                if (items != null)
                {
                    var lines = new List<int>();
                    var pages = PagesOn ? Paginate("By the hearth, drovers, pilgrims and pedlars trade the news of the road:", items, lines) : null;
                    string sample = null;
                    foreach (var it in items) if (it.Cat == 0) { sample = it.Text; break; }
                    if (sample == null) foreach (var it in items) if (it.Cat == 1) { sample = it.Text; break; }
                    probe = "plotek " + items.Count + (pages != null ? ", stron " + pages.Count + " (linie ok. " + JoinInts(lines) + ")" : ", strony WYLACZONE")
                            + (sample != null ? ", przyklad: \"" + sample + "\"" : "");
                }
            }
            Log.Info(P + "opowiesci dzien " + day + " | klucze: Inn Gossip Loot Words " + (WordsOn ? "tak" : "WYLACZONE") + ", Inn Gossip Leader Names " + (NamesOn ? "tak" : "WYLACZONE")
                     + ", Inn Gossip Pages " + (PagesOn ? "tak" : "WYLACZONE")
                     + " | herszci: bohaterowie " + _dNameHero + ", z ksiegi " + _dNameBook + ", nadane " + _dNameNew + " (imion w ksiedze " + NamedInBook() + ")"
                     + " | lup slowami: kiesy band " + JoinArr(_dPurseTier) + ", skarbce kryjowek " + JoinArr(_dHoardTier) + " (stopnie 0-5)"
                     + " | okna " + _dWindows + ", strony " + _dPages + ", przewiniete " + _dPagesTurned + ", najdluzsza strona " + _dMaxLines + " linii (miesci sie " + PageLines + ")"
                     + " | proba przy graczu (bez okna): " + probe + ".");
        }

        private static int NamedInBook()
        {
            int n = 0;
            foreach (var b in _bands.Values) if (!string.IsNullOrEmpty(b.Name)) n++;
            return n;
        }

        private static string JoinArr(int[] v)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < v.Length; i++) { if (i > 0) sb.Append('/'); sb.Append(v[i].ToString(Inv)); }
            return sb.ToString();
        }

        private static string Since(Band b, int today)
        {
            if (b == null || !b.Known) return "for a good while now";
            int d = today - b.First;
            if (d < 3) return "these last few days";
            if (d < 10) return "for a week or so";
            if (d < 21) return "for a fortnight";
            if (d < 45) return "for a month now";
            if (d < 120) return "for months";
            return "for as long as anyone remembers";
        }

        private static void Bandits(List<Item> all, Vec2 at, uint seed, int today)
        {
            const float R = 70f;
            var found = new List<KeyValuePair<float, MobileParty>>();
            foreach (var p in MobileParty.AllBanditParties)
            {
                if (p == null || !p.IsActive || p.CurrentSettlement != null || p.MemberRoster == null) continue;
                int men = p.MemberRoster.TotalManCount;
                if (men < 5) continue;
                float d = p.GetPosition2D.Distance(at);
                if (d > R) continue;
                float score = (men + Math.Max(0, p.PartyTradeGold) / 50f) / (1f + d / 15f);
                found.Add(new KeyValuePair<float, MobileParty>(score, p));
            }
            found.Sort((x1, x2) => x2.Key.CompareTo(x1.Key));
            for (int i = 0; i < found.Count && i < 2; i++)
            {
                var p = found[i].Value;
                float d = p.GetPosition2D.Distance(at);
                float q = Exact(d, 0, 150f);
                uint s = seed ^ MapVillageData.Fnv(p.StringId ?? "");
                Band b; _bands.TryGetValue(p.StringId ?? "", out b);
                int men = p.MemberRoster.TotalManCount, gold = Math.Max(0, p.PartyTradeGold);
                bool rich = gold >= Math.Max(1500, 40 * men);
                // n20-plotki (InnGossipLeaderNames): "Jon Rivers and his bandits" zamiast przezwiska i nazwy rodzaju bandy ("Broken Men")
                string who;
                if (NamesOn) who = BandWho(p);
                else who = Leader(p) + " and his " + (p.ActualClan != null ? p.ActualClan.Name.ToString() : "outlaws");
                var t = new StringBuilder();
                t.Append(i == 0 ? "They say " : "Others speak of how ").Append(who).Append(" have been robbing the roads ").Append(Where(at, p.GetPosition2D, q))
                 .Append(' ').Append(Since(b, today)).Append(" - ").Append(Men(men, q, s)).Append(" men");
                if (WordsOn)
                {
                    // n20-plotki (InnGossipLootWords): nikt nie liczyl kiesy bandy - stopien slowny (daleko plotka rosnie)
                    int tier = LootTier(gold, q, s >> 3);
                    _dPurseTier[tier]++;
                    t.Append(tier == 0 ? ", but poor as church mice." : ", with " + PurseWords[tier] + ".");
                }
                else if (gold <= 50) t.Append(", but poor as church mice.");
                else t.Append(rich ? ", and fat with plunder: " : ", with ").Append(Coin(gold, q, s >> 3)).Append(" in their purses.");
                if (b != null && b.Raids > 0) t.Append(" They have burned ").Append(b.Raids == 1 ? "a village" : b.Raids + " villages").Append(" already.");
                if (rich) t.Append(" It will be hard to smoke them out now.");
                all.Add(new Item { Cat = 0, Score = 3f + found[i].Key / 10f, Text = t.ToString(), Id = "b|" + p.StringId + "|" + PlaceId(p.GetPosition2D) + "|" + (b != null ? b.Raids : 0).ToString(Inv) });
            }
        }

        // ------------------------------------------------------------ kryjowki
        private static void Hideouts(List<Item> all, Vec2 at, uint seed)
        {
            const float R = 160f;
            Settlement best = null; float bs = -1f; int bestBands = 0;
            foreach (var h in Hideout.All)
            {
                var st = h != null ? h.Settlement : null;
                if (st == null) continue;
                if (!HideoutSight.RumourFits(h)) continue;   // m100-kryjowki: przy kluczu plotka tylko o zajetej, niewypatrzonej kryjowce
                float d = st.GetPosition2D.Distance(at);
                if (d > R) continue;
                int bands = 0;
                foreach (var p in st.Parties) if (p != null && p.IsBandit) bands++;
                if (bands == 0) continue;
                int gold = st.SettlementComponent != null ? Math.Max(0, st.SettlementComponent.Gold) : 0;
                float score = (gold / 200f + bands * 5f) / (1f + d / 40f);
                if (score > bs) { bs = score; best = st; bestBands = bands; }
            }
            if (best == null) return;
            float dd = best.GetPosition2D.Distance(at);
            float q = Exact(dd, 0, 150f);
            uint s = seed ^ MapVillageData.Fnv(best.StringId ?? "");
            int g = best.SettlementComponent != null ? Math.Max(0, best.SettlementComponent.Gold) : 0;
            var t = new StringBuilder("There is an outlaw lair in the wilds ");
            t.Append(Where(at, best.GetPosition2D, q)).Append(" - ").Append(q >= 0.45f ? (bestBands == 1 ? "one band goes" : bestBands + " bands go") : "many bands go").Append(" in and out of it");
            if (WordsOn)
            {
                // n20-plotki (InnGossipLootWords): skarbiec kryjowki slowami, ta sama skala co kiesy band
                int tier = LootTier(g, q, s);
                _dHoardTier[tier]++;
                t.Append(tier == 0 ? ", though folk say there is little worth taking." : ", and folk swear there is " + HoardWords[tier] + " hidden there for whoever clears it out.");
            }
            else if (g < 100) t.Append(", though folk say there is little worth taking.");
            else t.Append(", and folk swear ").Append(Coin(g, q, s)).Append(" lies hidden there for whoever clears it out.");
            all.Add(new Item { Cat = 1, Score = 2.5f + bs / 10f, Text = t.ToString(), Id = "h|" + best.StringId });
        }

        // ------------------------------------------------------------ wojska
        private static void Armies(List<Item> all, Vec2 at, uint seed)
        {
            Army best = null; float bd = 180f;
            foreach (var k in Kingdom.All)
            {
                if (k == null || k.IsEliminated) continue;
                foreach (var a in k.Armies)
                {
                    if (a == null || a.LeaderParty == null) continue;
                    float d = a.LeaderParty.GetPosition2D.Distance(at);
                    if (d < bd) { bd = d; best = a; }
                }
            }
            if (best != null)
            {
                var lp = best.LeaderParty;
                float q = Exact(bd, 0, 150f);
                uint s = seed ^ MapVillageData.Fnv(lp.StringId ?? "");
                string leader = lp.LeaderHero != null ? lp.LeaderHero.Name.ToString() : lp.Name.ToString();
                string realm = best.Kingdom != null ? " of " + best.Kingdom.Name : "";
                string doing, did;
                if (lp.SiegeEvent != null && lp.SiegeEvent.BesiegedSettlement != null) { doing = "laying siege to " + lp.SiegeEvent.BesiegedSettlement.Name; did = "s:" + lp.SiegeEvent.BesiegedSettlement.StringId; }
                else
                {
                    var target = best.AiBehaviorObject as Settlement;
                    if (target != null && best.Kingdom != null && target.MapFaction != null && FactionManager.IsAtWarAgainstFaction(best.Kingdom, target.MapFaction)) { doing = "marching on " + target.Name; did = "m:" + target.StringId; }
                    else if (target != null) { doing = "gathering by " + target.Name; did = "g:" + target.StringId; }
                    else { doing = "on the march"; did = "w:" + PlaceId(lp.GetPosition2D); }
                }
                all.Add(new Item { Cat = 2, Score = 2.8f + (1f - bd / 180f), Text = "The host of " + leader + realm + " - " + Men(best.TotalManCount, q, s) + " men - is " + doing + ", " + Where(at, lp.GetPosition2D, q) + ".",
                                   Id = "a|" + lp.StringId + "|" + did });
                return;
            }
            MobileParty big = null; float bb = 120f;
            foreach (var p in MobileParty.AllLordParties)
            {
                if (p == null || !p.IsActive || p.LeaderHero == null || p.CurrentSettlement != null || p.MemberRoster == null || p.MemberRoster.TotalManCount < 80) continue;
                float d = p.GetPosition2D.Distance(at);
                if (d < bb) { bb = d; big = p; }
            }
            if (big == null) return;
            float q2 = Exact(bb, 0, 150f);
            string fac = big.MapFaction != null ? " of " + big.MapFaction.Name : "";
            all.Add(new Item { Cat = 2, Score = 2f + (1f - bb / 120f), Text = big.LeaderHero.Name + fac + " rides with " + Men(big.MemberRoster.TotalManCount, q2, seed ^ MapVillageData.Fnv(big.StringId ?? "")) + " men " + Where(at, big.GetPosition2D, q2) + ".",
                               Id = "l|" + big.StringId + "|" + PlaceId(big.GetPosition2D) });
        }

        // ------------------------------------------------------------ karawany
        private static void Caravans(List<Item> all, Vec2 at, uint seed)
        {
            MobileParty best = null; float bd = 90f; int near = 0;
            foreach (var p in MobileParty.AllCaravanParties)
            {
                if (p == null || !p.IsActive || p.CurrentSettlement != null) continue;
                float d = p.GetPosition2D.Distance(at);
                if (d > 90f) continue;
                near++;
                if (p.TargetSettlement != null && d < bd) { bd = d; best = p; }
            }
            if (best == null) return;
            float q = Exact(bd, 0, 150f);
            string owner = best.Party != null && best.Party.Owner != null ? best.Party.Owner.Name.ToString() : best.ActualClan != null ? best.ActualClan.Name.ToString() : "a merchant";
            int guards = best.MemberRoster != null ? best.MemberRoster.TotalManCount : 0;
            string text = "A caravan of " + owner + " passed " + Where(at, best.GetPosition2D, q) + ", bound for " + best.TargetSettlement.Name + " with " + Men(guards, q, seed ^ MapVillageData.Fnv(best.StringId ?? "")) + " guards."
                          + (near >= 3 ? " The roads are busy - " + near + " caravans are about." : "");
            all.Add(new Item { Cat = 3, Score = 1.8f + (1f - bd / 90f), Text = text, Id = "c|" + best.StringId + "|" + best.TargetSettlement.StringId });
        }

        // ------------------------------------------------------------ ceny
        private static readonly string[] PriceIds = { "grain", "fish", "meat", "cheese", "butter", "beer", "wine", "oil", "salt", "linen", "wool", "hides", "tools", "iron", "pottery", "fur", "velvet" };
        private static List<ItemObject> _priceItems;

        private static void Prices(List<Item> all, Vec2 at, uint seed)
        {
            if (_priceItems == null)
            {
                _priceItems = new List<ItemObject>();
                foreach (var id in PriceIds) { try { var it = MBObjectManager.Instance.GetObject<ItemObject>(id); if (it != null) _priceItems.Add(it); } catch { } }
            }
            if (_priceItems.Count == 0) return;
            var towns = new List<KeyValuePair<float, Town>>();
            foreach (var t in Town.AllTowns)
            {
                var st = t != null ? t.Settlement : null;
                if (st == null || !st.IsTown || st.IsUnderSiege) continue;
                float d = st.GetPosition2D.Distance(at);
                if (d <= 260f) towns.Add(new KeyValuePair<float, Town>(d, t));
            }
            if (towns.Count < 2) return;
            towns.Sort((x1, x2) => x1.Key.CompareTo(x2.Key));
            if (towns.Count > 3) towns.RemoveRange(3, towns.Count - 3);
            ItemObject bi = null; Town hiT = null, loT = null; int hiP = 0, loP = 0; double br = 1.25;
            foreach (var it in _priceItems)
            {
                Town h = null, l = null; int hp = 0, lp = int.MaxValue;
                foreach (var kv in towns)
                {
                    int pr = kv.Value.GetItemPrice(it, null, false);
                    if (pr <= 0) continue;
                    if (pr > hp) { hp = pr; h = kv.Value; }
                    if (pr < lp) { lp = pr; l = kv.Value; }
                }
                if (h == null || l == null || h == l || lp <= 0) continue;
                double r = hp / (double)lp;
                if (r > br) { br = r; bi = it; hiT = h; loT = l; hiP = hp; loP = lp; }
            }
            if (bi == null) return;
            float far = Math.Max(hiT.Settlement.GetPosition2D.Distance(at), loT.Settlement.GetPosition2D.Distance(at));
            bool exact = Exact(far, 0, 600f) >= 0.75f;
            string a = exact ? hiP.ToString(Inv) : "about " + Round(hiP).ToString(Inv);
            string b = exact ? loP.ToString(Inv) : "about " + Round(loP).ToString(Inv);
            all.Add(new Item { Cat = 4, Score = 1.5f + (float)Math.Min(2.0, br - 1.0), Text = bi.Name + " fetches " + a + " in " + hiT.Name + ", but only " + b + " in " + loT.Name + " - a carter could do well.",
                               Id = "p|" + bi.StringId + "|" + hiT.Settlement.StringId + "|" + loT.Settlement.StringId });
        }

        // p5-kruki (R3-05c): dzien poczatku oblezenia z wiesci NSiege tej osady (najnowszej); brak wiesci (np. sprzed wczytania) = jak dotad
        private static bool SiegeNotYet(Settlement st, float d)
        {
            string nm = Clean(st.Name != null ? st.Name.ToString() : ""); int day = -1;
            foreach (var n in _news) if (n.Kind == NSiege && n.C == nm && n.Day > day) day = n.Day;
            return day >= 0 && Ravens.NotYet(d, day);
        }

        // ------------------------------------------------------------ wojny
        private static void Wars(List<Item> all, Vec2 at, uint seed, int today)
        {
            var cands = new List<Item>();   // m98-plotki: z tozsamoscia wiesci (Id) - "Just yesterday" / "A few days ago" to ta sama wiesc
            foreach (var n in _news)
            {
                int age = Math.Max(0, today - n.Day);
                if (age > NewsDays) continue;
                var p = new Vec2(n.X, n.Y);
                float d = p.Distance(at);
                if (Ravens.NotYet(d, n.Day)) continue;   // p5-kruki (R3-05c): dalekiej wiesci nie ma w karczmie, zanim nie doleci z nia kruk
                float q = Exact(d, age, 600f);
                string when = age <= 1 ? "Just yesterday" : age < 7 ? "A few days ago" : age < 21 ? "Some weeks ago" : "A month or more ago";
                string place = q >= 0.35f && n.C.Length > 0 ? n.C : null;
                string text = null;
                switch (n.Kind)
                {
                    case NWar: text = when + " " + n.A + " declared war on " + n.B + "."; break;
                    case NPeace: text = when + " " + n.A + " and " + n.B + " made peace" + (age < 7 ? " - may it last." : "."); break;
                    case NCaptured:
                        text = when + " " + (place ?? (n.N == 1 ? "a town" : "a castle") + " " + Days(d) + " to the " + Dir(at, p)) + " fell to " + (n.A.Length > 0 ? n.A : "its besiegers")
                               + (n.B.Length > 0 ? " of " + n.B : "") + ".";
                        break;
                    case NRaided:
                        text = when + " " + (n.A.Length > 0 ? n.A : "raiders") + (n.B.Length > 0 && n.B != n.A ? " of " + n.B : "") + " burned " + (place ?? "a village " + Days(d) + " to the " + Dir(at, p)) + ".";
                        break;
                    case NBattle:
                        text = when + " " + n.A + " beat " + n.B + " in a great battle " + (place != null ? "near " + place : Days(d) + " to the " + Dir(at, p)) + " - "
                               + Men(n.N, q, seed ^ (uint)(n.Day * 7919)) + " men took the field.";
                        break;
                    case NSiege:
                        text = when + " " + (n.A.Length > 0 ? n.A : "a host") + (n.B.Length > 0 ? " of " + n.B : "") + " laid siege to " + (place ?? "a stronghold " + Days(d) + " to the " + Dir(at, p)) + ".";
                        break;
                }
                if (text == null) continue;
                float score = 1.2f + (1f - age / (float)NewsDays) * (1f / (1f + d / 250f)) * (n.Kind == NWar || n.Kind == NPeace ? 1.3f : 1f);
                cands.Add(new Item { Cat = 5, Score = score, Text = text, Id = "w|" + n.Kind.ToString(Inv) + "|" + n.Day.ToString(Inv) + "|" + n.A + "|" + n.B + "|" + n.C });
            }
            // trwajace oblezenia w poblizu (stan teraz)
            foreach (var st in Settlement.All)
            {
                if (st == null || !st.IsFortification || st.SiegeEvent == null) continue;
                float d = st.GetPosition2D.Distance(at);
                if (d > 300f) continue;
                if (SiegeNotYet(st, d)) continue;   // p5-kruki (R3-05c): trwajace oblezenie z daleka - dopiero, gdy doleci kruk z wiescia o nim
                var lp = st.SiegeEvent.BesiegerCamp != null ? st.SiegeEvent.BesiegerCamp.LeaderParty : null;
                string who = lp != null && lp.LeaderHero != null ? lp.LeaderHero.Name.ToString() : "a great host";
                cands.Add(new Item { Cat = 5, Score = 2.2f + (1f - d / 300f), Text = st.Name + " is under siege - " + who + (lp != null && lp.MapFaction != null ? " of " + lp.MapFaction.Name : "") + " sits before its walls.",
                                     Id = "s|" + st.StringId + "|" + (lp != null ? lp.StringId : "-") });
            }
            if (cands.Count == 0)
            {
                var town = NearestTown(at);
                var k = town != null ? town.MapFaction as Kingdom : null;
                if (k == null) return;
                var foes = new List<string>();
                foreach (var o in Kingdom.All) if (o != null && o != k && !o.IsEliminated && FactionManager.IsAtWarAgainstFaction(k, o)) foes.Add(o.Name.ToString());
                cands.Add(new Item { Cat = 5, Score = 1f, Text = foes.Count == 0 ? k.Name + " is at peace, the gods be thanked." : "Folk say " + k.Name + " is at war with " + string.Join(", ", foes.ToArray()) + ".",
                                     Id = "k|" + k.StringId + "|" + string.Join(",", foes.ToArray()) });
            }
            cands.Sort((x1, x2) => x2.Score.CompareTo(x1.Score));
            for (int i = 0; i < cands.Count && i < 2; i++) all.Add(cands[i]);
        }
    }
}
