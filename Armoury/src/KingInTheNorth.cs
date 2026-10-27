using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// PACZKA 272 - KROL POLNOCY (Jeff 27.10 ok. 15:25: "a tak potem jak dolaczaja do roberta to staja sie jednym krolestwem King in the North"; audyt 27.10 rozdz. 3:
    /// korona to jedno krolestwo 50 rodow, strona Robba rozbita na Polnoc (56) i Dorzecze (39), Polnoc nigdy nie walczy za Dorzecze). Klucz CanonNorthRiverlandsUnion.
    /// KANON: po zwyciestwach Robba lordowie rzeczni i polnocni oglaszaja go w Riverrun Krolem Polnocy (i Tridentu), 299. W modzie: Kronika okrzykuje Robba w oknie
    /// 002.2, okno ROT RiverlandsDeclareWarEvent (ok. dnia opowiesci 108) m116 A4 przepisuje na "The River Lords Bend the Knee" - do niego przypieta unia.
    /// WYZWALACZ (przeglad co godzine, raz na kampanie, flaga w zapisie arm_k272):
    ///  1. klucz TAK; 2. znacznik: RotEventState(RiverlandsDeclareWarEvent) == 2 (ROT konczy okno tylko przy zywym Dorzeczu - relacje +80), albo stan ROT -1/0 i dzien
    ///     opowiesci >= plan Fabuli tego okna + Prologue.RiverNetDays (zapas A2), albo flaga autotestu; 3. kanon: Robb zyje i rzadzi Polnoca, Krwawe Gody nie ruszyly
    ///     (RedWeddingEvent &lt; 1 - ten sam znacznik co 264), oba krolestwa zyja i sa w wojnie z korona, Dorzeczem rzadzi rod lord_1_3 (Tully), Polnoc i Dorzecze nie sa
    ///     w wojnie, gracz nie rzadzi zadnym (jak A3), brak wojny domowej Diplomacy w Dorzeczu; 4. bezpieczna chwila: gracz bez bitwy, spotkania poza osada i misji,
    ///     zadna partia wojenna Dorzecza ani twierdza Dorzecza w MapEvent, Dorzecze bez wojen, ktorych Polnoc nie ma. Czekanie najwyzej Prologue.RiverAllyRetryDays (7)
    ///     dob od pierwszej godziny czekania - potem unia i tak (wojny tylko Dorzecza - pokoj MakePeaceAction przed unia: rada Robba nie bierze cudzych sporow).
    ///     Robb martwy / Krwawe Gody / krolestwo zniszczone przed unia - "nigdy" (st=2). W zapisie Jeffa znacznik dawno minal - unia w pierwszej bezpiecznej godzinie.
    /// UNIA (jedna transakcja; kazdy krok w swoim try, linie "Krol Polnocy (272): "):
    ///  - migawka rodow (rola, lenna, partie, ludzie, wplywy, dlug wobec krolestwa, ShouldStayInKingdomUntil, mnoznik najemnika), armii, portfeli, jencow gracza;
    ///  - decyzje rady Dorzecza anulowane (RemoveDecision + OnKingdomDecisionCancelled - inaczej UpdateKingdomDecisions rozstrzyga je przy upadku krolestwa);
    ///  - wojny tylko Dorzecza - pokoj; sojusze Dorzecza (A3 i inne) - EndAlliance (porozumienia wezwan koncza sie droga gry - 168 EndPostfix);
    ///  - pieniadze (zamknieta ekonomia): dlug Banku najpierw ze skarbca (CrownBorrow.RepayOnFall, jak G14), reszta skarbca do skarbca Polnocy (CrownIncome.ShiftSnapshot -
    ///    przelew, nie wplyw dnia), reszta dlugu Banku przeniesiona (CrownBorrow.MoveLoan + koszt kredytu IronBank), portfele gry najemnikow i daniny (dlugi rodow - placa
    ///    je teraz rody Polnocy, w tym rzeczne), portfel wezwan tylko gdy oba nieujemne (pole netto - Measure31.CtwOther); pozyczki koron od miast (korona2), umowy
    ///    najemnikow 185, sluzba rent 180, wielcy jency i nagrody 178 - przepisane na Polnoc; kontrola: zdjete = dopisane;
    ///  - rody: gracz najpierw (zaden upadek krolestwa bez lenn - FactionDiscontinuation przy wyjsciu gracza), najemnicy AI, wasale, rod panujacy na koncu. Wasal -
    ///    ApplyByJoinToKingdom (JoinKingdom: bez kary relacji, lenna, zalogi, wiesniacy i karawany ida z rodem, barwy Polnocy); najemnik - ApplyByJoinFactionAsMercenary
    ///    bez wyjscia (wyjscie robi pokoj rodu z wrogami - PrisonerRelease uwolnilby jencow gracza). Wplywy: pole Clan._influence 0 przed ruchem i z powrotem po (gra
    ///    zeruje wplywy przy kazdej zmianie, setter dalby XP za "wydane"); dlug wobec krolestwa i dzien odnowienia kontraktu gracza - z migawki;
    ///  - armie ZACHOWANE: prefiks Clan.LeaveKingdomInternal (tylko w czasie unii) odpina na chwile pole MobileParty._army partii armii Dorzecza, wiec gra ich nie
    ///    wypisuje (Army = null konczylo MapEventSide i BesiegerCamp - MobileParty.cs:1786); po ruchu Army.Kingdom = Polnoc (spojnosc, czlonkowie, oblezenie bez
    ///    zmian, bez kosztu wplywow OnAddPartyInternal). Bez latki - odtworzenie (new Army, czlonkowie, spojnosc z migawki, wplywy wodza bez zmian). Oblegajacy
    ///    dostaja SetMoveBesiegeSettlement znowu (ChangeKingdomAction stawia partie bez MapEvent na postoj);
    ///  - koniec: RulingClan = null, DestroyKingdomAction (tylko gdy w krolestwie nie ma juz rodow - inaczej gra zniszczylaby rody);
    ///  - kontrola (rody w Polnocy, zadnego zniszczonego, lenna / partie / ludzie / jency gracza przed = po), flaga, okno i Log.Player.
    /// Nazwa i herb Polnocy bez zmian (Jeff: "jednym krolestwem King in the North"); tytuly BK bez zmian (Hoster/Edmure dalej Lord Paramount, jak w ksiazkach).
    /// KRWAWE GODY: ROT RedWeddingQuest.ExecuteRobb przenosi Freyow, Mootonow, Coxow i Brackenow do korony tylko z krolestwa Dorzecza - postfiks przenosi tych z unii.
    /// KRONIKA: po unii "riverlands" w warunkach politycznych (war, peace, kexists, leads, prisoner, p:vassal) = Polnoc; ziemia (own, region, p:region) = osada
    /// Polnocy z listy twierdz z chwili unii (wies - jej twierdza) albo kultury Dorzecza; relk - glowy rodow z unii; plotka - siedziba Dorzecza z chwili unii.
    /// Zapis "arm_k272" (starsza DLL go pomija). Bledy: licznik potkniec, bez globalnego wylacznika.
    /// RECENZJA 272: (1) kariera najemnika BK (MercenaryCareer) przepisana z Dorzecza na Polnoc (punkty, przywileje, czas przywilejow, Kingdom; termin kontraktu bez
    /// zmian) - inaczej Tick BK widzi inne krolestwo i AddKingdom zaczyna od zera z nowym rokiem kontraktu; (2) Krwawe Gody: po pokoju ROT Polnoc-korona Dorzecze
    /// znowu osobno (ReactivateKingdom, wladca Tully, rody z unii - wasale AI - wracaja, wojna z korona jak wpis ROT bez konca, ktory unia usunela) - stan 3, aliasy
    /// Kroniki wylaczone; (3) gracz przenoszony po najemnikach i wasalach, przed rodem panujacym (gdy ten ma lenna) - zadania od lordow rzecznych nie przepadaja;
    /// (4) zakres jak klucze Wojen kanonu: tylko Prologue.CanonHere (kampania z prologiem albo nowa z m104 - decyzja Jeffa nr 5).
    /// </summary>
    internal static partial class KingInTheNorth
    {
        internal const string P = "Krol Polnocy (272): ";
        internal const string RiverId = "riverlands", NorthId = "battania", CrownId = "vlandia";
        private const string RobbId = "lord_5_1", TullyLordId = "lord_1_3";
        private const string RotEvent = "RiverlandsDeclareWarEvent", RedWeddingEvent = "RedWeddingEvent";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // ---------------------------------------------------------------- stan (zapis arm_k272)
        private static int _st;                     // 0 czeka, 1 unia zrobiona, 2 nigdy, 3 rozdzielone po Krwawych Godach (recenzja)
        private static int _day = int.MinValue, _dayC = -1;   // dzien opowiesci (calkowity) i dzien kampanii unii
        private static string _wy = "";             // rodzaj wyzwalacza: rot / plan / test (+n - nadrobienie)
        private static string _player = "-";        // rola gracza w chwili unii: - / najemnik / wasal
        private static double _waitSince = -1.0;     // pierwsza godzina kampanii czekania (znacznik i kanon spelnione)
        private static bool _rw;                    // Krwawe Gody obsluzone
        private static string _seat = "", _culture = "", _never = "";
        private static readonly List<string> _clans = new List<string>(), _forts = new List<string>(), _mercs = new List<string>();
        private static readonly HashSet<string> _clanSet = new HashSet<string>(StringComparer.Ordinal), _fortSet = new HashSet<string>(StringComparer.Ordinal);

        // ---------------------------------------------------------------- sesja (bez zapisu)
        internal static bool Moving;                // w czasie przenoszenia rodow (MercContract, MercTenure, prefiks LeaveKingdomInternal)
        private static Harmony _harmony;
        private static string _wireRw = "nie wpieta", _wireArmy = "leniwie (przy unii)";
        private static bool _rwTried, _armyTried, _armyPatched;
        private static int _sessionDays, _stumbles, _importBad, _mergeCount;
        private static bool _sessionChecked, _markerAtStart;
        private static string _lastWait = "";
        private static int _lastWaitDay = int.MinValue;
        private static readonly HashSet<string> _err = new HashSet<string>();

        internal static bool On { get { var s = Settings.Current; return s != null && s.CanonNorthRiverlandsUnion; } }

        /// <summary>Unia zrobiona (aliasy Kroniki, Krwawe Gody, A3).</summary>
        internal static bool Done { get { return _st == 1; } }

        internal static void SetHarmony(Harmony h) { _harmony = h; }

        internal static void Reset()
        {
            _st = 0; _day = int.MinValue; _dayC = -1; _wy = ""; _player = "-"; _waitSince = -1.0; _rw = false; _seat = ""; _culture = ""; _never = "";
            _clans.Clear(); _forts.Clear(); _mercs.Clear(); _clanSet.Clear(); _fortSet.Clear();
            Moving = false; _sessionDays = 0; _stumbles = 0; _importBad = 0; _mergeCount = 0; _sessionChecked = false; _markerAtStart = false;
            _lastWait = ""; _lastWaitDay = int.MinValue; _err.Clear(); _detached.Clear();
            TestReset();
        }

        internal static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_err.Add(where)) Log.Error("KingInTheNorth." + where, e); } catch { }
        }

        private static Kingdom K(string id)
        {
            foreach (var k in Kingdom.All) if (k != null && k.StringId == id) return k;
            return null;
        }

        private static bool Alive(Kingdom k) { return k != null && !k.IsEliminated; }

        private static readonly Dictionary<string, Hero> _heroes = new Dictionary<string, Hero>(StringComparer.Ordinal);   // pamiec wyszukiwan (bohater nie znika z gry; kampania - Reset)
        private static Campaign _heroesCamp;

        private static Hero HeroOf(string id)
        {
            try
            {
                if (_heroesCamp != Campaign.Current) { _heroes.Clear(); _heroesCamp = Campaign.Current; }
                Hero h;
                if (_heroes.TryGetValue(id, out h) && h != null) return h;
                h = Hero.FindFirst(x => x != null && x.StringId == id);
                if (h != null) _heroes[id] = h;
                return h;
            }
            catch { return null; }
        }

        private static bool PlayerRules(Kingdom k)
        {
            try { return k != null && k.RulingClan != null && k.RulingClan == Clan.PlayerClan; } catch { return false; }
        }

        private static string Nm(object o)
        {
            try
            {
                var c = o as Clan; if (c != null) return c.Name != null ? c.Name.ToString() : c.StringId;
                var k = o as Kingdom; if (k != null) return k.Name != null ? k.Name.ToString() : k.StringId;
                var h = o as Hero; if (h != null) return h.Name != null ? h.Name.ToString() : h.StringId;
                var s = o as Settlement; if (s != null) return s.Name != null ? s.Name.ToString() : s.StringId;
                var m = o as MobileParty; if (m != null) return m.Name != null ? m.Name.ToString() : m.StringId;
                return o != null ? o.ToString() : "-";
            }
            catch { return "?"; }
        }

        private static double StoryDay() { try { return Chronicle.Terms.StoryDay(); } catch { return 0.0; } }
        private static string F1(double d) { return d.ToString("0.0", Inv); }

        // ---------------------------------------------------------------- Kronika (aliasy po unii)

        /// <summary>Warunek polityczny Kroniki: po unii "riverlands" = Polnoc (war, peace, kexists, leads, prisoner, p:vassal). Inaczej id bez zmian.</summary>
        internal static string AliasKingdom(string id) { return _st == 1 && id == RiverId ? NorthId : id; }

        /// <summary>Po unii id "riverlands" w warunku ziemi / grupie / miejscu plotki.</summary>
        internal static bool IsRiverAlias(string id) { return _st == 1 && id == RiverId; }

        /// <summary>Ziemia Tridentu: twierdza z listy z chwili unii (wies - jej twierdza) albo osada kultury Dorzecza.</summary>
        internal static bool TridentLand(Settlement s)
        {
            try
            {
                if (s == null) return false;
                var b = s.IsVillage && s.Village != null && s.Village.Bound != null ? s.Village.Bound : s;
                if (b.StringId != null && _fortSet.Contains(b.StringId)) return true;
                return _culture.Length > 0 && b.Culture != null && b.Culture.StringId == _culture;
            }
            catch { return false; }
        }

        /// <summary>Osada Polnocy na ziemi Tridentu (own:X:riverlands, region:H:riverlands, p:region:riverlands po unii).</summary>
        internal static bool HeldAsRiverlands(Settlement s)
        {
            try { return s != null && s.MapFaction != null && s.MapFaction.StringId == NorthId && TridentLand(s); } catch { return false; }
        }

        /// <summary>Rody z unii dalej w Polnocy (bez najemnikow i wyeliminowanych) - grupa "riverlands" w relk po unii.</summary>
        internal static List<Clan> RiverLords()
        {
            var l = new List<Clan>();
            try
            {
                foreach (var c in Clan.All)
                    if (c != null && c.StringId != null && _clanSet.Contains(c.StringId) && !c.IsEliminated && !c.IsUnderMercenaryService && c.Kingdom != null && c.Kingdom.StringId == NorthId) l.Add(c);
            }
            catch (Exception e) { Stumble("RiverLords", e); }
            return l;
        }

        /// <summary>Siedziba Dorzecza z chwili unii (miejsce plotki "riverlands" po unii); pusty - brak.</summary>
        internal static string SeatId { get { return _seat; } }

        // ---------------------------------------------------------------- zapis
        internal static string Export()
        {
            var sb = new StringBuilder(256 + _clans.Count * 24 + _forts.Count * 12);
            sb.Append("v1|").Append(_st.ToString(Inv)).Append('|').Append(_day.ToString(Inv)).Append('|').Append(_dayC.ToString(Inv)).Append('|').Append(_wy).Append('|')
              .Append(_player).Append('|').Append(_waitSince.ToString("0.###", Inv)).Append('|').Append(_rw ? "1" : "0").Append('|').Append(_seat).Append('|').Append(_culture)
              .Append('|').Append(string.Join(",", _clans.ToArray())).Append('|').Append(string.Join(",", _forts.ToArray())).Append('|').Append(string.Join(",", _mercs.ToArray()))
              .Append('|').Append(_never);
            return sb.ToString();
        }

        internal static void Import(string v)
        {
            _st = 0; _day = int.MinValue; _dayC = -1; _wy = ""; _player = "-"; _waitSince = -1.0; _rw = false; _seat = ""; _culture = ""; _never = "";
            _clans.Clear(); _forts.Clear(); _mercs.Clear(); _clanSet.Clear(); _fortSet.Clear(); _importBad = 0;
            if (string.IsNullOrEmpty(v)) return;
            try
            {
                var f = v.Split('|');
                if (f.Length < 14 || f[0] != "v1") { _importBad++; return; }
                int.TryParse(f[1], NumberStyles.Integer, Inv, out _st);
                if (_st < 0 || _st > 3) { _st = 0; _importBad++; }
                if (!int.TryParse(f[2], NumberStyles.Integer, Inv, out _day)) _day = int.MinValue;
                if (!int.TryParse(f[3], NumberStyles.Integer, Inv, out _dayC)) _dayC = -1;
                _wy = f[4]; _player = f[5].Length > 0 ? f[5] : "-";
                if (!double.TryParse(f[6], NumberStyles.Float, Inv, out _waitSince)) _waitSince = -1.0;
                _rw = f[7] == "1"; _seat = f[8]; _culture = f[9];
                foreach (var x in f[10].Split(',')) if (x.Length > 0) { _clans.Add(x); _clanSet.Add(x); }
                foreach (var x in f[11].Split(',')) if (x.Length > 0) { _forts.Add(x); _fortSet.Add(x); }
                foreach (var x in f[12].Split(',')) if (x.Length > 0) _mercs.Add(x);
                _never = f[13];
            }
            catch (Exception e) { _importBad++; Stumble("Import", e); }
        }

        // ---------------------------------------------------------------- start sesji
        internal static void SessionStart()
        {
            _sessionDays = 0; _sessionChecked = false; _markerAtStart = false; _lastWait = ""; _lastWaitDay = int.MinValue;
            InstallRedWedding();
            string st = _st == 1 ? "zrobione (dzien opowiesci " + (_day != int.MinValue ? _day.ToString(Inv) : "?") + ", dzien kampanii " + _dayC + ", rodow " + _clans.Count + ")"
                       : _st == 2 ? "nigdy (" + _never + ")" : _st == 3 ? "rozdzielone po Krwawych Godach (unia dnia opowiesci " + (_day != int.MinValue ? _day.ToString(Inv) : "?") + ", rodow " + _clans.Count + ")" : "czeka";
            string robb = "-", river = "-", role = "-";
            int rs = -9;
            try
            {
                rs = Prologue.RotEventState(RotEvent);
                var h = HeroOf(RobbId); var north = K(NorthId); var rv = K(RiverId);
                robb = h == null ? "brak" : !h.IsAlive ? "nie zyje" : (Alive(north) && north.Leader == h ? "zyje, krol Polnocy" : "zyje, nie rzadzi Polnoca");
                if (Alive(rv))
                {
                    int n = 0, m = 0; foreach (var c in rv.Clans) if (c != null && !c.IsEliminated) { n++; if (c.IsUnderMercenaryService) m++; }
                    river = "wladca " + (rv.RulingClan != null ? Nm(rv.RulingClan) + " (" + rv.RulingClan.StringId + ")" : "-") + ", rodow " + n + " (najemnikow " + m + ")";
                }
                else river = rv == null ? "brak krolestwa" : "zniszczone";
                var pc = Clan.PlayerClan;
                role = pc == null || pc.Kingdom == null ? "bez krolestwa" : (pc.IsUnderMercenaryService ? "najemnik " : PlayerRules(pc.Kingdom) ? "wladca " : "wasal ") + pc.Kingdom.StringId;
            }
            catch (Exception e) { Stumble("SessionStart", e); }
            Log.Info(P + "start sesji | klucz " + (On ? "TAK" : "NIE") + " | stan " + st + " | ROT " + RotEvent + " stan " + rs + ", Krwawe Gody stan " + SafeRot(RedWeddingEvent)
                     + " | Robb " + robb + " | Dorzecze: " + river + " | gracz: " + role + (_st == 1 ? " (w chwili unii: " + _player + ")" : "")
                     + " | latki: Krwawe Gody " + _wireRw + ", armie " + _wireArmy + " | dzien opowiesci " + F1(StoryDay()) + (_importBad > 0 ? " | bledy zapisu " + _importBad : "") + ".");
        }

        private static int SafeRot(string ev) { try { return Prologue.RotEventState(ev); } catch { return -9; } }

        internal static void Daily()
        {
            _sessionDays++;
            if (_st != 3) return;
            try { SplitWar(); } catch (Exception e) { Stumble("SplitWar", e); }
        }

        // ---------------------------------------------------------------- przeglad co godzine
        internal static void Hourly()
        {
            if (Campaign.Current == null) return;
            if (_st != 0) { TestAfter(); return; }
            try { Tick(); } catch (Exception e) { Stumble("Tick", e); }
            TestAfter();
        }

        /// <summary>Linia "czeka": najwyzej raz na dobe opowiesci; ten sam rodzaj powodu (tekst przed nawiasem) - najwyzej raz na Prologue.RiverAllyRetryDays dob.</summary>
        private static void Wait(string why, double sd)
        {
            int day = (int)Math.Floor(sd);
            int br = why.IndexOf(" (", StringComparison.Ordinal);
            string key = br > 0 ? why.Substring(0, br) : why;
            if (_lastWaitDay != int.MinValue && day == _lastWaitDay) return;
            if (key == _lastWait && _lastWaitDay != int.MinValue && day - _lastWaitDay < Prologue.RiverAllyRetryDays272) return;
            _lastWait = key; _lastWaitDay = day;
            Log.Info(P + "czeka - " + why + " | dzien opowiesci " + F1(sd) + ".");
        }

        private static void Never(string why, double sd)
        {
            _st = 2; _never = Clean(why);
            Log.Info(P + "nigdy - " + why + " (dzien opowiesci " + F1(sd) + ") - unia nie wejdzie w tej kampanii.");
        }

        private static string Clean(string s) { return (s ?? "").Replace('|', '/').Replace(',', ';'); }

        private static void Tick()
        {
            if (!On) return;
            double sd = StoryDay();
            // recenzja: zakres jak klucze Wojen kanonu (decyzja Jeffa nr 5) - bez zapisu stanu (Wait), wiec nic nie zamyka sie na stale
            if (!Prologue.CanonHere) { Wait("zakres - kampania klasyczna bez prologu i bez znacznika m104 (decyzja Jeffa nr 5, jak klucze Wojen kanonu)", sd); return; }
            var river = K(RiverId); var north = K(NorthId); var crown = K(CrownId);
            var robb = HeroOf(RobbId);
            // nigdy
            if (river == null || north == null) { Never("brak krolestwa (" + (river == null ? RiverId : NorthId) + ")", sd); return; }
            if (robb != null && !robb.IsAlive) { Never("Robb Stark nie zyje", sd); return; }
            if (SafeRot(RedWeddingEvent) >= 1) { Never("Krwawe Gody ruszyly (RedWeddingEvent)", sd); return; }
            if (river.IsEliminated) { Never("Dorzecze zniszczone przed unia", sd); return; }
            if (north.IsEliminated) { Never("Polnoc zniszczona", sd); return; }
            // znacznik
            int rs = SafeRot(RotEvent);
            string kind = null;
            if (rs == 2) kind = "rot";
            else if (rs <= 0)
            {
                double pd;
                if (Chronicle.Terms.PlanDay(RotEvent, out pd) && sd >= pd + Prologue.RiverNetDays272) kind = "plan";
            }
            if (kind == null && TestForce()) kind = "test";
            if (!_sessionChecked) { _sessionChecked = true; _markerAtStart = kind == "rot" || kind == "plan"; }
            if (kind == null)
            {
                double pd; bool hasPlan = Chronicle.Terms.PlanDay(RotEvent, out pd);
                Wait("hold lordow rzecznych jeszcze nie nastapil (ROT " + RotEvent + " stan " + rs + (hasPlan ? ", plan Fabuli dzien " + F1(pd) : ", bez osi Fabuli") + ")", sd);
                return;
            }
            // kanon
            string why = null;
            if (robb == null) why = "brak bohatera Robba " + RobbId;
            else if (north.Leader != robb) why = "Robb nie rzadzi Polnoca (wladca " + Nm(north.Leader) + ")";
            else if (!Alive(crown)) why = "brak korony (" + CrownId + ")";
            else if (!north.IsAtWarWith(crown) || !river.IsAtWarWith(crown)) why = "wojna z korona: Polnoc " + (north.IsAtWarWith(crown) ? "TAK" : "NIE") + ", Dorzecze " + (river.IsAtWarWith(crown) ? "TAK" : "NIE");
            else if (north.IsAtWarWith(river)) why = "Polnoc w wojnie z Dorzeczem";
            else if (PlayerRules(north) || PlayerRules(river)) why = "krolestwo gracza - poza regula";
            else if (!TullyRules(river)) why = "Dorzeczem nie rzadzi rod " + TullyLordId + " (wladca " + Nm(river.RulingClan) + ")";
            else if (RiverCivilWar(river)) why = "wojna domowa Diplomacy w Dorzeczu";
            if (why != null) { _waitSince = -1.0; Wait(why, sd); return; }   // kanon nie stoi - czekanie na bezpieczna chwile liczone od nowa
            // bezpieczna chwila (najwyzej Prologue.RiverAllyRetryDays dob od pierwszej godziny czekania)
            double now = CampaignTime.Now.ToHours;
            if (_waitSince < 0) _waitSince = now;
            bool cap = now - _waitSince >= Prologue.RiverAllyRetryDays272 * 24.0;
            string unsafeWhy = UnsafeWhy(river, north);
            if (unsafeWhy != null && !cap) { Wait("bezpieczna chwila (" + unsafeWhy + "; najwyzej " + Prologue.RiverAllyRetryDays272 + " dob, minelo " + F1((now - _waitSince) / 24.0) + ")", sd); return; }
            bool catchUp = kind != "test" && _markerAtStart;
            Merge(river, north, crown, kind, catchUp, cap ? unsafeWhy : null, sd);
        }

        private static bool TullyRules(Kingdom river)
        {
            var h = HeroOf(TullyLordId);
            if (h == null || h.Clan == null) return river.RulingClan != null;   // bez bohatera ROT - wladca jakikolwiek (linia startu mowi kto)
            return river.RulingClan == h.Clan;
        }

        private static bool RiverCivilWar(Kingdom river)
        {
            try
            {
                var t = AccessTools.TypeByName("Diplomacy.CivilWar.RebelFactionManager");
                if (t == null) return false;
                var pi = AccessTools.Property(t, "Instance"); var inst = pi != null ? pi.GetValue(null, null) : null;
                var pr = inst != null ? AccessTools.Property(t, "RebelFactions") : null;
                var dict = pr != null ? pr.GetValue(inst, null) as System.Collections.IDictionary : null;
                if (dict == null || !dict.Contains(river)) return false;
                var list = dict[river] as System.Collections.IEnumerable;
                if (list == null) return false;
                foreach (var f in list)
                {
                    if (f == null) continue;
                    var pa = AccessTools.Property(f.GetType(), "AtWar");
                    if (pa != null && Convert.ToBoolean(pa.GetValue(f, null), Inv)) return true;
                }
            }
            catch (Exception e) { Stumble("RiverCivilWar", e); }
            return false;
        }

        /// <summary>Wojny krolestw Dorzecza, ktorych Polnoc nie ma (bez siebie nawzajem).</summary>
        private static List<Kingdom> RiverOnlyWars(Kingdom river, Kingdom north)
        {
            var l = new List<Kingdom>();
            foreach (var f in river.FactionsAtWarWith.ToList())
            {
                var k = f as Kingdom;
                if (k != null && !k.IsEliminated && k != north && !north.IsAtWarWith(k)) l.Add(k);
            }
            return l;
        }

        private static string UnsafeWhy(Kingdom river, Kingdom north)
        {
            var mp = MobileParty.MainParty;
            if (mp != null && (mp.MapEvent != null || mp.SiegeEvent != null || (PlayerEncounter.Current != null && PlayerEncounter.EncounterSettlement == null))) return "gracz w bitwie, oblezeniu albo spotkaniu";
            try { if (TaleWorlds.MountAndBlade.Mission.Current != null) return "gracz w misji"; } catch { }
            var ow = RiverOnlyWars(river, north);
            if (ow.Count > 0) return "wojny tylko Dorzecza (" + string.Join(", ", ow.Select(k => k.StringId).ToArray()) + ")";
            foreach (var c in river.Clans)
            {
                if (c == null || c.IsEliminated) continue;
                foreach (var w in c.WarPartyComponents)
                {
                    var p = w != null ? w.MobileParty : null;
                    if (p != null && p.IsActive && p.MapEvent != null) return "partia Dorzecza w bitwie (" + Nm(p) + ")";
                }
                foreach (var t in c.Fiefs)
                    if (t != null && t.Settlement != null && t.Settlement.Party != null && t.Settlement.Party.MapEvent != null) return "twierdza Dorzecza w bitwie (" + Nm(t.Settlement) + ")";
            }
            return null;
        }

        // ---------------------------------------------------------------- unia
        private sealed class ClanSnap
        {
            public Clan C; public bool Merc, Player, Ruler; public float Inf; public int Debt, Mult; public CampaignTime Stay;
            public int Fiefs, Villages, Parties, Men, Garrison; public bool Ok; public string Note = "";
        }

        private sealed class ArmySnap
        {
            public Army A; public MobileParty Leader; public List<MobileParty> Members = new List<MobileParty>(); public HashSet<MobileParty> Attached = new HashSet<MobileParty>();
            public float Cohesion; public Army.ArmyTypes Type; public IMapPoint Obj; public Settlement Besieged;
        }

        private sealed class SiegeSnap { public MobileParty P; public Settlement S; public MobileParty.NavigationType Nav; }

        private static FieldInfo _fInf, _fArmy;
        private static bool _fTried;
        private static MethodInfo _setMercWallet;
        private static readonly List<KeyValuePair<MobileParty, Army>> _detached = new List<KeyValuePair<MobileParty, Army>>();
        private static readonly HashSet<Army> _keep = new HashSet<Army>();

        private static void Fields()
        {
            if (_fTried) return;
            _fTried = true;
            try { _fInf = AccessTools.Field(typeof(Clan), "_influence"); } catch { _fInf = null; }
            try { _fArmy = AccessTools.Field(typeof(MobileParty), "_army"); } catch { _fArmy = null; }
            try { _setMercWallet = AccessTools.PropertySetter(typeof(Kingdom), "MercenaryWallet"); } catch { _setMercWallet = null; }
        }

        private static bool SetInfluenceRaw(Clan c, float v)
        {
            if (_fInf == null || c == null) return false;
            try { _fInf.SetValue(c, v); return true; } catch (Exception e) { Stumble("SetInfluenceRaw", e); return false; }
        }

        private static int Men(Clan c, out int parties, out int garrison)
        {
            int men = 0; parties = 0; garrison = 0;
            foreach (var w in c.WarPartyComponents)
            {
                var p = w != null ? w.MobileParty : null;
                if (p == null || !p.IsActive) continue;
                parties++; men += p.MemberRoster.TotalManCount;
            }
            foreach (var t in c.Fiefs) if (t != null && t.GarrisonParty != null) garrison += t.GarrisonParty.MemberRoster.TotalManCount;
            return men;
        }

        private static List<string> PlayerPrisoners(out int total)
        {
            var l = new List<string>(); total = 0;
            try
            {
                var mp = MobileParty.MainParty;
                if (mp == null) return l;
                var r = mp.PrisonRoster;
                for (int i = 0; i < r.Count; i++)
                {
                    var e = r.GetElementCopyAtIndex(i);
                    total += e.Number;
                    if (e.Character != null && e.Character.IsHero && e.Character.HeroObject != null) l.Add(e.Character.HeroObject.StringId);
                }
                l.Sort(StringComparer.Ordinal);
            }
            catch (Exception e) { Stumble("PlayerPrisoners", e); }
            return l;
        }

        // ostatnie liczby unii (proba autotestu)
        private static int _tClans, _tMoved, _tDestroyed, _tArmies, _tArmiesOk, _tFiefs0, _tFiefs1, _tParties0, _tParties1, _tMen0, _tMen1;
        private static long _tControl = long.MinValue;
        private static bool _tPlayerOk = true, _tPrisOk = true, _tRiverGone, _tAllyGone, _tCrownWar;
        private static int _tCohOk;

        private static void Merge(Kingdom river, Kingdom north, Kingdom crown, string kind, bool catchUp, string forcedWhy, double sd)
        {
            Fields();
            _mergeCount++;
            int dayC = (int)CampaignTime.Now.ToDays;
            var pc = Clan.PlayerClan;
            // ---- 0. migawka
            var snaps = new List<ClanSnap>();
            var ruler = river.RulingClan;
            int totF = 0, totV = 0, totP = 0, totM = 0, totG = 0, mercN = 0;
            foreach (var c in river.Clans.ToList())
            {
                if (c == null || c.IsEliminated) continue;
                var s = new ClanSnap { C = c, Merc = c.IsUnderMercenaryService, Player = c == pc, Ruler = c == ruler, Inf = c.Influence, Debt = c.DebtToKingdom, Mult = c.MercenaryAwardMultiplier, Stay = c.ShouldStayInKingdomUntil };
                s.Fiefs = c.Fiefs.Count; s.Villages = c.Villages.Count;
                s.Men = Men(c, out s.Parties, out s.Garrison);
                totF += s.Fiefs; totV += s.Villages; totP += s.Parties; totM += s.Men; totG += s.Garrison; if (s.Merc) mercN++;
                snaps.Add(s);
            }
            float renewal = -1f;
            try { renewal = Campaign.Current.KingdomManager.PlayerMercenaryServiceNextRenewalDay; } catch { }
            int prisTot0; var pris0 = PlayerPrisoners(out prisTot0);
            var armies = new List<ArmySnap>();
            _keep.Clear();
            var seen = new HashSet<Army>();
            foreach (var s in snaps)
                foreach (var w in s.C.WarPartyComponents)
                {
                    var p = w != null ? w.MobileParty : null;
                    var a = p != null ? p.Army : null;
                    if (a == null || !seen.Add(a)) continue;
                    var x = new ArmySnap { A = a, Leader = a.LeaderParty, Cohesion = a.Cohesion, Type = a.ArmyType, Obj = a.AiBehaviorObject, Besieged = a.LeaderParty != null ? a.LeaderParty.BesiegedSettlement : null };
                    foreach (var m in a.Parties) { x.Members.Add(m); if (m.AttachedTo != null) x.Attached.Add(m); }
                    armies.Add(x); _keep.Add(a);
                }
            var sieges = new List<SiegeSnap>();
            foreach (var s in snaps)
                foreach (var w in s.C.WarPartyComponents)
                {
                    var p = w != null ? w.MobileParty : null;
                    if (p == null || !p.IsActive || p.IsMainParty || p.AttachedTo != null || p.MapEvent != null || p.BesiegerCamp == null || p.BesiegedSettlement == null) continue;
                    sieges.Add(new SiegeSnap { P = p, S = p.BesiegedSettlement, Nav = p.DesiredAiNavigationType });
                }
            string role = "-";
            foreach (var s in snaps) if (s.Player) role = s.Merc ? "najemnik" : "wasal";
            _forts.Clear(); _fortSet.Clear(); _clans.Clear(); _clanSet.Clear(); _mercs.Clear();
            foreach (var s in snaps)
            {
                _clans.Add(s.C.StringId); _clanSet.Add(s.C.StringId);
                if (s.Merc) _mercs.Add(s.C.StringId);
                foreach (var t in s.C.Fiefs) if (t != null && t.Settlement != null && _fortSet.Add(t.Settlement.StringId)) _forts.Add(t.Settlement.StringId);
            }
            _seat = ruler != null && ruler.HomeSettlement != null ? ruler.HomeSettlement.StringId : (Settlement.Find("town_V2") != null ? "town_V2" : "");
            _culture = river.Culture != null ? river.Culture.StringId : "";
            Log.Info(P + "UNIA start | dzien opowiesci " + F1(sd) + " (dzien kampanii " + dayC + ") | wyzwalacz: "
                     + (kind == "rot" ? "ROT " + RotEvent + " zakonczone" : kind == "plan" ? "zapas planu Fabuli" : "test (flaga " + AutoFlagName + ")")
                     + (catchUp ? " - nadrobienie po wczytaniu" : "") + (forcedWhy != null ? " | po " + Prologue.RiverAllyRetryDays272 + " dobach czekania mimo: " + forcedWhy : "")
                     + " | Dorzecze: rodow " + snaps.Count + " (najemnikow " + mercN + ", gracz " + role + "), twierdz " + totF + ", wsi " + totV + ", partii " + totP + ", ludzi " + totM
                     + " (zalogi " + totG + "), armii " + armies.Count + " | wladca " + Nm(ruler) + " (" + (ruler != null ? ruler.StringId : "-") + ") | Polnoc przed: rodow " + north.Clans.Count
                     + ", twierdz " + north.Fiefs.Count + " | jency gracza " + prisTot0 + " (bohaterowie: " + (pris0.Count > 0 ? string.Join(",", pris0.ToArray()) : "-") + ").");
            _tClans = snaps.Count; _tFiefs0 = totF; _tParties0 = totP; _tMen0 = totM; _tCareerOk = true; _tCareer = "kariera BK gracza nie dotyczy (" + role + ")";
            int bk0 = MercContract.BkDownUnion;
            Moving = true;
            long control = 0;
            bool popup = false;
            try
            {
                // ---- 1. decyzje rady Dorzecza
                try
                {
                    foreach (var d in river.UnresolvedDecisions.ToList())
                    {
                        bool inv = false;
                        try { inv = d.DetermineChooser().Leader.IsHumanPlayerCharacter || d.DetermineSupporters().Any(x => x.IsPlayer); } catch { }
                        river.RemoveDecision(d);
                        CampaignEventDispatcher.Instance.OnKingdomDecisionCancelled(d, inv);
                        Log.Info(P + "decyzja rady Dorzecza anulowana - " + d.GetType().Name + " (proponujacy " + Nm(d.ProposerClan) + ", wplywy proponujacego bez zwrotu - jak anulowanie gry).");
                    }
                }
                catch (Exception e) { Stumble("Merge.Decisions", e); }
                // ---- 2. wojny tylko Dorzecza
                try
                {
                    foreach (var x in RiverOnlyWars(river, north))
                    {
                        MakePeaceAction.Apply(river, x);
                        Log.Info(P + "pokoj Dorzecza z " + Nm(x) + " (" + x.StringId + ") - wojna tylko Dorzecza, Polnoc jej nie ma; rada Robba nie bierze cudzych sporow | pokoj " + (river.IsAtWarWith(x) ? "NIE" : "TAK") + ".");
                    }
                }
                catch (Exception e) { Stumble("Merge.Wars", e); }
                // ---- 3. sojusze
                try
                {
                    var ab = Campaign.Current.GetCampaignBehavior<IAllianceCampaignBehavior>();
                    if (ab != null)
                        foreach (var ak in river.AlliedKingdoms.ToList())
                        {
                            if (ak == null) continue;
                            ab.EndAlliance(river, ak);
                            Log.Info(P + "sojusz Dorzecza z " + Nm(ak) + " (" + ak.StringId + ") zakonczony " + (ab.IsAllyWithKingdom(river, ak) ? "NIE" : "TAK") + (ak == north ? " (sojusz A3 - teraz jedno krolestwo)" : "") + ".");
                        }
                }
                catch (Exception e) { Stumble("Merge.Alliances", e); }
                // ---- 4. pieniadze
                control = Money(river, north);
                // ---- 5. rody: najemnicy AI, wasale, gracz, rod panujacy (recenzja: gracz po dawcach zadan - zadania gry "Army Needs Supplies", "Raid", "Scout",
                //      "Conquest" koncza sie, gdy gracz wychodzi z krolestwa dawcy; Dorzecze ma wtedy jeszcze lenna wladcy - FactionDiscontinuation go nie konczy).
                //      Rod panujacy bez lenn - gracz pierwszy jak dotad (wyjscie gracza z krolestwa bez osad konczy je razem z rodami).
                var ps0 = snaps.FirstOrDefault(s => s.Player);
                var rs0 = snaps.FirstOrDefault(s => !s.Player && s.Ruler);
                bool playerLate = ps0 != null && rs0 != null && rs0.Fiefs > 0;
                if (ps0 != null) ps0.Note += playerLate ? " (kolejnosc: po najemnikach i wasalach, przed rodem panujacym - zadania od lordow rzecznych zostaja)"
                                                        : " (kolejnosc: pierwszy - rod panujacy bez lenn)";
                var order = new List<ClanSnap>();
                if (!playerLate) order.AddRange(snaps.Where(s => s.Player));
                order.AddRange(snaps.Where(s => !s.Player && s.Merc));
                order.AddRange(snaps.Where(s => !s.Player && !s.Merc && !s.Ruler));
                if (playerLate) order.AddRange(snaps.Where(s => s.Player));
                order.AddRange(snaps.Where(s => !s.Player && !s.Merc && s.Ruler));
                EnsureArmyPatch();
                foreach (var s in order) MoveClan(s, river, north, renewal);
                // drugie podejscie dla rodow, ktore zostaly (wyjatek przy pierwszym)
                foreach (var s in order) if (!s.Ok && !s.C.IsEliminated && s.C.Kingdom == river) MoveClan(s, river, north, renewal);
                RestoreDetached();
                // ---- 6. armie
                Armies(armies, north);
                foreach (var g in sieges)
                {
                    try
                    {
                        if (g.P == null || !g.P.IsActive || g.P.MapEvent != null || g.P.AttachedTo != null || g.S == null || g.P.BesiegerCamp == null) continue;
                        if (!g.S.MapFaction.IsAtWarWith(g.P.MapFaction)) continue;
                        g.P.SetMoveBesiegeSettlement(g.S, g.Nav != MobileParty.NavigationType.None ? g.Nav : g.P.NavigationCapability);
                        Log.Info(P + "oblezenie " + Nm(g.S) + " - " + Nm(g.P) + " dalej oblega (rozkaz oblezenia znowu po postoju ChangeKingdomAction).");
                    }
                    catch (Exception e) { Stumble("Merge.Siege", e); }
                }
                foreach (var s in snaps)
                    foreach (var w in s.C.WarPartyComponents)
                    {
                        var p = w != null ? w.MobileParty : null;
                        if (p != null && p.IsActive && !p.IsMainParty && p.MapEvent == null) p.Ai.RethinkAtNextHourlyTick = true;
                    }
                // ---- 7. stan modow wedlug id krolestwa
                ReKey(river, north);
                // ---- 8. koniec krolestwa
                int left = river.Clans.Count(c => c != null && !c.IsEliminated);
                if (left == 0)
                {
                    try
                    {
                        river.RulingClan = null;
                        DestroyKingdomAction.Apply(river);
                    }
                    catch (Exception e) { Stumble("Merge.Destroy", e); }
                }
                else Log.Info(P + "Dorzecze NIE zniszczone - zostalo rodow " + left + " (DestroyKingdomAction zniszczylby je) - sprawdz linie rodow wyzej.");
            }
            finally
            {
                Moving = false;
                RestoreDetached();
                _keep.Clear();
            }
            // ---- 9. kontrola i komunikat
            int moved = 0, destroyed = 0, f1 = 0, p1 = 0, m1 = 0;
            foreach (var s in snaps)
            {
                if (s.C.IsEliminated) { destroyed++; continue; }
                if (s.C.Kingdom == north) moved++;
                int pp, gg; m1 += Men(s.C, out pp, out gg); p1 += pp; f1 += s.C.Fiefs.Count;
            }
            int prisTot1; var pris1 = PlayerPrisoners(out prisTot1);
            bool prisSame = prisTot1 == prisTot0 && string.Join(",", pris0.ToArray()) == string.Join(",", pris1.ToArray());
            _st = 1; _day = (int)Math.Floor(sd); _dayC = dayC; _wy = kind + (catchUp ? "+n" : ""); _player = role; _waitSince = -1.0;
            _tMoved = moved; _tDestroyed = destroyed; _tFiefs1 = f1; _tParties1 = p1; _tMen1 = m1; _tControl = control; _tPrisOk = prisSame;
            _tRiverGone = river.IsEliminated;
            try { var ab = Campaign.Current.GetCampaignBehavior<IAllianceCampaignBehavior>(); _tAllyGone = ab == null || !ab.IsAllyWithKingdom(north, river); } catch { _tAllyGone = false; }
            _tCrownWar = Alive(crown) && north.IsAtWarWith(crown) && snaps.All(s => s.C.IsEliminated || s.C.MapFaction == null || s.C.MapFaction.IsAtWarWith(crown));
            var ps = snaps.FirstOrDefault(s => s.Player);
            _tPlayerOk = ps == null || (pc.Kingdom == north && pc.IsUnderMercenaryService == ps.Merc && (!ps.Merc || pc.MercenaryAwardMultiplier == ps.Mult));
            try { popup = ShowPopup(catchUp, ps); } catch (Exception e) { Stumble("Merge.Popup", e); }
            Log.Info(P + "UNIA ZAKONCZONA | Dorzecze zniszczone " + (river.IsEliminated ? "TAK" : "NIE") + " | rodow przeniesionych " + moved + "/" + snaps.Count + ", zniszczonych " + destroyed
                     + " | lenna " + f1 + "/" + totF + ", partie " + p1 + "/" + totP + ", ludzie " + m1 + "/" + totM + " | Polnoc po: rodow " + north.Clans.Count + ", twierdz " + north.Fiefs.Count
                     + ", armii " + north.Armies.Count + " | jency gracza przed " + prisTot0 + " (" + (pris0.Count > 0 ? string.Join(",", pris0.ToArray()) : "-") + ") / po " + prisTot1 + " ("
                     + (pris1.Count > 0 ? string.Join(",", pris1.ToArray()) : "-") + ") " + (prisSame ? "TAK" : "NIE") + " | gracz " + role + " -> " + (pc != null && pc.Kingdom != null ? (pc.IsUnderMercenaryService ? "najemnik " : "wasal ") + pc.Kingdom.StringId : "-")
                     + " | zadatki BK wstrzymane (ta sama sluzba) " + (MercContract.BkDownUnion - bk0) + " | komunikat gracza " + (popup ? "TAK" : "NIE") + " | potkniecia " + _stumbles + ".");
            TestRun(river, north, crown);
        }

        // ---------------------------------------------------------------- pieniadze
        private static long Money(Kingdom river, Kingdom north)
        {
            long taken = 0, added = 0;
            var sb = new StringBuilder(400);
            try
            {
                long w0 = river.KingdomBudgetWallet;
                long paid = w0 > 0 ? CrownBorrow.RepayOnFall(river, w0) : 0;
                if (paid > 0) river.KingdomBudgetWallet -= (int)paid;
                taken += paid; added += paid;   // skarbiec Dorzecza -> kapital Banku (RepayOnFall dopisal dokladnie tyle)
                int rest = river.KingdomBudgetWallet;   // ze znakiem - ujemny skarbiec tez przechodzi (dlug korony)
                long nv0 = north.KingdomBudgetWallet;
                river.KingdomBudgetWallet -= rest;
                north.KingdomBudgetWallet += rest;
                long got = (long)north.KingdomBudgetWallet - nv0;
                CrownIncome.ShiftSnapshot(north, got);   // 165: skarb przejety, nie wplyw dnia (jak G14 a)
                taken += rest; added += got;
                double loan = CrownBorrow.MoveLoan(river, north);
                sb.Append("skarbiec Dorzecza ").Append(w0).Append(" -> splata Banku ").Append(paid).Append(", do skarbca Polnocy ").Append(rest)
                  .Append(" | dlug Banku przeniesiony ").Append((long)Math.Round(loan));
                // portfele gry: najemnicy i danina (ujemne - dlug rodow wobec portfela; placa go teraz rody Polnocy)
                int mw = river.MercenaryWallet;
                if (mw != 0 && _setMercWallet != null)
                {
                    int nm0 = north.MercenaryWallet;
                    _setMercWallet.Invoke(river, new object[] { 0 });
                    _setMercWallet.Invoke(north, new object[] { nm0 + mw });
                    taken += mw; added += (long)north.MercenaryWallet - nm0;
                }
                int tw = river.TributeWallet;
                if (tw != 0)
                {
                    int nt0 = north.TributeWallet;
                    river.TributeWallet = 0; north.TributeWallet = nt0 + tw;
                    taken += tw; added += (long)north.TributeWallet - nt0;
                }
                int cw = river.CallToWarWallet, ncw0 = north.CallToWarWallet;
                string ctw;
                if (cw == 0) ctw = "0";
                else if (cw > 0 && ncw0 >= 0)
                {
                    int rc0 = cw;
                    river.CallToWarWallet = 0; north.CallToWarWallet = ncw0 + cw;
                    Measure31.CtwOther(river, rc0); Measure31.CtwOther(north, ncw0);   // pole netto: suma dodatnich bez zmiany
                    taken += cw; added += (long)north.CallToWarWallet - ncw0;
                    ctw = cw + " przeniesiony";
                }
                else ctw = cw + " zostaje (pole netto: Dorzecze " + cw + ", Polnoc " + ncw0 + " - jak przy kazdym upadku krolestwa)";
                sb.Append(" | portfele gry: najemnicy ").Append(mw).Append(_setMercWallet == null && mw != 0 ? " (BRAK settera - zostaje)" : "").Append(", danina ").Append(tw).Append(", wezwania ").Append(ctw);
                double kl = TownFolk.KlMoveKingdom(river.StringId, north.StringId);
                sb.Append(" | pozyczki od miast (korona2) przeniesione ").Append((long)Math.Round(kl));
            }
            catch (Exception e) { Stumble("Merge.Money", e); sb.Append(" | BLAD ").Append(e.GetType().Name); }
            long diff = taken - added;
            Log.Info(P + sb.ToString() + " | kontrola: zdjete " + taken + " - dopisane " + added + " = " + diff + ".");
            return diff;
        }

        private static void ReKey(Kingdom river, Kingdom north)
        {
            try
            {
                int deals = MercContract.MoveDeals(river.StringId, north.StringId);
                int rents = CrownRents.MoveKingdom(river.StringId, north.StringId);
                int captives, rewards; Ransom178.MoveKingdom(river.StringId, north.StringId, out captives, out rewards);
                Log.Info(P + "stan wedlug krolestwa -> Polnoc | umowy najemnikow 185: " + deals + " | sluzba rent 180: " + rents + " | wielcy jency 178: " + captives + ", nagrody 178: " + rewards
                         + " | kredyt Banku 186 i koszt kredytu (zysk Banku) - w linii skarbca.");
            }
            catch (Exception e) { Stumble("Merge.ReKey", e); }
        }

        // ---------------------------------------------------------------- rody
        private static void MoveClan(ClanSnap s, Kingdom river, Kingdom north, float renewal)
        {
            var c = s.C;
            if (c == null || c.IsEliminated || c.Kingdom == north) return;
            try
            {
                bool raw = SetInfluenceRaw(c, 0f);   // gra zeruje wplywy przy wyjsciu (ChangeClanInfluenceAction - XP za "wydane") - przy 0 nic nie robi
                if (s.Merc) ChangeKingdomAction.ApplyByJoinFactionAsMercenary(c, north, s.Stay, s.Mult, false);
                else ChangeKingdomAction.ApplyByJoinToKingdom(c, north, s.Stay, false);
                if (raw) SetInfluenceRaw(c, s.Inf);
                else
                {
                    float d = s.Inf - c.Influence;
                    if (d != 0f) ChangeClanInfluenceAction.Apply(c, d);
                    s.Note += " (wplywy przez akcje gry - brak pola _influence)";
                }
                c.DebtToKingdom = s.Debt;
                if (s.Player && s.Merc && renewal > 0f) Campaign.Current.KingdomManager.PlayerMercenaryServiceNextRenewalDay = renewal;
                s.Ok = c.Kingdom == north;
                if (s.Merc && s.Ok)
                {
                    bool bkOk;
                    string bk = BkCareerMove(c, river, north, out bkOk);
                    s.Note += " | " + bk;
                    if (s.Player) { _tCareerOk = bkOk; _tCareer = bk; }
                }
            }
            catch (Exception e) { Stumble("MoveClan", e); s.Note += " (wyjatek " + e.GetType().Name + ")"; }
            int pp, gg; int men = Men(c, out pp, out gg);
            string kind = s.Player ? (s.Merc ? "gracz-najemnik" : "gracz-wasal") : s.Merc ? "najemnik" : s.Ruler ? "wladca Dorzecza" : "wasal";
            Log.Info(P + "rod " + Nm(c) + " (" + c.StringId + ") " + kind + " -> Polnoc " + (s.Ok ? "TAK" : "NIE") + " | lenna " + c.Fiefs.Count + "/" + s.Fiefs + ", wsie " + c.Villages.Count + "/" + s.Villages
                     + ", partie " + pp + "/" + s.Parties + ", ludzie " + men + "/" + s.Men + ", zalogi " + gg + "/" + s.Garrison + " | wplywy " + s.Inf.ToString("0.#", Inv) + " -> " + c.Influence.ToString("0.#", Inv)
                     + " | dlug wobec krolestwa " + s.Debt + " -> " + c.DebtToKingdom + " | zostaje do dnia " + s.Stay.ToDays.ToString("0", Inv) + (s.Merc ? " | mnoznik " + s.Mult + " -> " + c.MercenaryAwardMultiplier : "") + s.Note + ".");
        }

        // ---------------------------------------------------------------- kariera najemnika BK (recenzja 272)
        // BK MercenaryCareer.Tick: "if (Clan.IsUnderMercenaryService && Kingdom != Clan.Kingdom) AddKingdom(Clan.Kingdom)" - AddKingdom zaklada puste przywileje, punkty 0,
        // czas przywilejow = teraz i termin kontraktu = rok od dzis. Unia to ta sama sluzba (okno: "on the same terms"), wiec wpisy Dorzecza ida pod Polnoc (scalone, gdy
        // Polnoc juz ma wpis z dawnej sluzby: punkty dodane, przywilej - wyzszy poziom, czas przywilejow - pozniejszy), Kingdom = Polnoc; ContractDueDate bez zmian.
        private static bool _bkTried;
        private static Type _tBkBeh;
        private static MethodInfo _mBkGetCareer, _mBkGetBeh, _setCKingdom;
        private static PropertyInfo _pCKingdom, _pCPriv, _pCProg, _pCTimes, _pCDue;
        private static bool _tCareerOk = true;
        private static string _tCareer = "-";

        private static int PrivLevel(object x)
        {
            try { var p = x != null ? AccessTools.Property(x.GetType(), "Level") : null; return p != null ? Convert.ToInt32(p.GetValue(x, null), Inv) : -1; } catch { return -1; }
        }

        private static string BkCareerMove(Clan c, Kingdom river, Kingdom north, out bool ok)
        {
            ok = true;
            try
            {
                if (!_bkTried)
                {
                    _bkTried = true;
                    _tBkBeh = AccessTools.TypeByName("BannerKings.Behaviours.Mercenary.BKMercenaryCareerBehavior");
                    var tC = AccessTools.TypeByName("BannerKings.Behaviours.Mercenary.MercenaryCareer");
                    if (_tBkBeh != null && tC != null)
                    {
                        _mBkGetCareer = AccessTools.Method(_tBkBeh, "GetCareer", new[] { typeof(Clan) });
                        var gm = typeof(Campaign).GetMethod("GetCampaignBehavior", Type.EmptyTypes);
                        _mBkGetBeh = gm != null ? gm.MakeGenericMethod(_tBkBeh) : null;
                        _pCKingdom = AccessTools.Property(tC, "Kingdom");
                        _setCKingdom = AccessTools.PropertySetter(tC, "Kingdom");
                        _pCPriv = AccessTools.Property(tC, "KingdomPrivileges");
                        _pCProg = AccessTools.Property(tC, "KingdomProgress");
                        _pCTimes = AccessTools.Property(tC, "PrivilegeTimes");
                        _pCDue = AccessTools.Property(tC, "ContractDueDate");
                    }
                }
                if (_tBkBeh == null) return "kariera BK bez zmian (brak BannerKings)";
                if (_mBkGetCareer == null || _mBkGetBeh == null || _setCKingdom == null || _pCKingdom == null || _pCPriv == null || _pCProg == null || _pCTimes == null)
                { ok = false; return "kariera BK przeniesiona NIE (brak odczytu MercenaryCareer)"; }
                var beh = _mBkGetBeh.Invoke(Campaign.Current, null);
                if (beh == null) { ok = false; return "kariera BK przeniesiona NIE (brak BKMercenaryCareerBehavior)"; }
                var car = _mBkGetCareer.Invoke(beh, new object[] { c });
                if (car == null) return "kariera BK bez zmian (rod bez kariery)";
                var k0 = _pCKingdom.GetValue(car, null) as Kingdom;
                var priv = _pCPriv.GetValue(car, null) as System.Collections.IDictionary;
                var prog = _pCProg.GetValue(car, null) as System.Collections.IDictionary;
                var times = _pCTimes.GetValue(car, null) as System.Collections.IDictionary;
                if (priv == null || prog == null || times == null) { ok = false; return "kariera BK przeniesiona NIE (brak slownikow kariery)"; }
                bool hasR = priv.Contains(river) || prog.Contains(river) || times.Contains(river);
                if (!hasR && k0 != river)
                    return "kariera BK bez zmian (kariera " + (k0 != null ? k0.StringId : "-") + ", bez wpisu Dorzecza)";
                // punkty
                float pR = prog.Contains(river) ? Convert.ToSingle(prog[river], Inv) : 0f;
                float pN = prog.Contains(north) ? Convert.ToSingle(prog[north], Inv) : 0f;
                if (prog.Contains(river)) { prog[north] = pN + pR; prog.Remove(river); }
                // przywileje
                int nPriv = 0;
                var lr = priv.Contains(river) ? priv[river] as System.Collections.IList : null;
                if (lr != null)
                {
                    var ln = priv.Contains(north) ? priv[north] as System.Collections.IList : null;
                    if (ln == null) { priv[north] = lr; nPriv = lr.Count; }
                    else
                        foreach (var x in lr)
                        {
                            int at = -1;
                            for (int i = 0; i < ln.Count; i++) if (ln[i] != null && ln[i].Equals(x)) { at = i; break; }
                            if (at < 0) { ln.Add(x); nPriv++; }
                            else if (PrivLevel(x) > PrivLevel(ln[at])) { ln[at] = x; nPriv++; }
                        }
                    priv.Remove(river);
                }
                // czas przywilejow (2 sezony od ostatniego przywileju / najmu)
                string tTxt = "-";
                if (times.Contains(river))
                {
                    var tR = (CampaignTime)times[river];
                    if (!times.Contains(north) || (CampaignTime)times[north] < tR) times[north] = tR;
                    times.Remove(river);
                }
                // Tick BK czyta KingdomProgress[Kingdom] i KingdomPrivileges[Kingdom] bez sprawdzenia - wpisy Polnocy musza byc (jak AddKingdom, bez terminu kontraktu)
                if (!prog.Contains(north)) prog[north] = 0f;
                if (!priv.Contains(north))
                {
                    var ga = priv.GetType().GetGenericArguments();
                    if (ga.Length == 2) priv[north] = Activator.CreateInstance(ga[1]);
                }
                if (!times.Contains(north)) times[north] = CampaignTime.Now;
                if (times.Contains(north)) tTxt = ((CampaignTime)times[north]).ToDays.ToString("0", Inv);
                if (!priv.Contains(north)) { ok = false; return "kariera BK przeniesiona NIE (brak listy przywilejow Polnocy)"; }
                if (k0 != north) _setCKingdom.Invoke(car, new object[] { north });   // rod jest juz najemnikiem Polnocy - to samo zrobilby Tick, ale bez AddKingdom
                var k1 = _pCKingdom.GetValue(car, null) as Kingdom;
                string due = "?";
                try { if (_pCDue != null) due = ((CampaignTime)_pCDue.GetValue(car, null)).ToDays.ToString("0", Inv); } catch { }
                ok = k1 == north;
                return "kariera BK przeniesiona " + (ok ? "TAK" : "NIE") + " (punkty " + pR.ToString("0", Inv) + (pN != 0f ? " + " + pN.ToString("0", Inv) + " z dawnej sluzby Polnocy" : "")
                       + ", przywileje " + nPriv + ", czas przywilejow dzien " + tTxt + ", termin kontraktu dzien " + due + " bez zmian, kariera " + (k1 != null ? k1.StringId : "-") + ")";
            }
            catch (Exception e) { Stumble("BkCareerMove", e); ok = false; return "kariera BK przeniesiona NIE (wyjatek " + e.GetType().Name + ")"; }
        }

        // ---------------------------------------------------------------- armie
        /// <summary>Prefiks Clan.LeaveKingdomInternal (tylko w czasie unii): partie armii zachowywanych odpiete na chwile od pola _army (gra ich nie wypisuje z armii).</summary>
        public static void LeavePrefix(Clan __instance)
        {
            if (!Moving || __instance == null || _fArmy == null || _keep.Count == 0) return;
            try
            {
                foreach (var w in __instance.WarPartyComponents.ToList())
                {
                    var p = w != null ? w.MobileParty : null;
                    var a = p != null ? p.Army : null;
                    if (a == null || !_keep.Contains(a)) continue;
                    _fArmy.SetValue(p, null);
                    _detached.Add(new KeyValuePair<MobileParty, Army>(p, a));
                }
            }
            catch (Exception e) { Stumble("LeavePrefix", e); RestoreDetached(); }
        }

        public static void LeavePostfix() { if (_detached.Count > 0) RestoreDetached(); }

        private static void RestoreDetached()
        {
            if (_detached.Count == 0) return;
            try { foreach (var kv in _detached) if (kv.Key != null && _fArmy != null && kv.Key.Army == null) _fArmy.SetValue(kv.Key, kv.Value); }
            catch (Exception e) { Stumble("RestoreDetached", e); }
            _detached.Clear();
        }

        private static void EnsureArmyPatch()
        {
            if (_armyTried) return;
            _armyTried = true;
            try
            {
                Fields();
                if (_harmony == null) { _wireArmy = "BRAK (bez Harmony)"; return; }
                if (_fArmy == null) { _wireArmy = "BRAK (pole MobileParty._army)"; return; }
                var m = AccessTools.Method(typeof(Clan), "LeaveKingdomInternal");
                if (m == null) { _wireArmy = "BRAK (Clan.LeaveKingdomInternal)"; return; }
                _harmony.Patch(m, prefix: new HarmonyMethod(typeof(KingInTheNorth), nameof(LeavePrefix)), postfix: new HarmonyMethod(typeof(KingInTheNorth), nameof(LeavePostfix)));
                _armyPatched = true; _wireArmy = "TAK";
            }
            catch (Exception e) { Stumble("EnsureArmyPatch", e); _wireArmy = "BLAD (" + e.GetType().Name + ")"; }
            Log.Info(P + "latka armii (prefiks Clan.LeaveKingdomInternal na czas unii): " + _wireArmy + (_armyPatched ? "" : " - armie zostana odtworzone po ruchu rodow") + ".");
        }

        private static void Armies(List<ArmySnap> list, Kingdom north)
        {
            _tArmies = list.Count; _tArmiesOk = 0; _tCohOk = 0;
            foreach (var x in list)
            {
                try
                {
                    string how; Army a = x.A;
                    bool alive = x.Leader != null && x.Leader.IsActive && x.Leader.LeaderHero != null && x.Leader.LeaderHero.IsAlive && !x.Leader.LeaderHero.IsPrisoner;
                    if (_armyPatched && a != null && a.LeaderParty == x.Leader && x.Leader != null && x.Leader.Army == a)
                    {
                        if (a.Kingdom != north) a.Kingdom = north;
                        how = "zachowana pod Polnoca";
                    }
                    else if (!alive) { Log.Info(P + "armia " + Nm(x.Leader) + " NIE (wodz nieaktywny, martwy albo w niewoli) | partii " + x.Members.Count + "."); continue; }
                    else if (x.Leader.Army != null) { Log.Info(P + "armia " + Nm(x.Leader) + " NIE (wodz juz w innej armii) | partii " + x.Members.Count + "."); continue; }
                    else
                    {
                        // bez latki: odtworzenie - spojnosc z migawki, czlonkowie z powrotem, wplywy wodza bez kosztu OnAddPartyInternal
                        var lc = x.Leader.LeaderHero.Clan;
                        float inf = lc != null ? lc.Influence : 0f;
                        a = new Army(north, x.Leader, x.Type);
                        if (x.Obj != null) a.AiBehaviorObject = x.Obj;
                        foreach (var m in x.Members)
                        {
                            if (m == null || m == x.Leader || !m.IsActive || m.Army != null || m.MapFaction != north) continue;
                            m.Army = a;
                            if (x.Attached.Contains(m)) m.AttachedTo = x.Leader;
                        }
                        a.Cohesion = x.Cohesion;
                        if (lc != null && !SetInfluenceRaw(lc, inf)) { float d = inf - lc.Influence; if (d != 0f) ChangeClanInfluenceAction.Apply(lc, d); }
                        try { CampaignEventDispatcher.Instance.OnArmyCreated(a); } catch (Exception e) { Stumble("Armies.OnArmyCreated", e); }
                        how = "odtworzona pod Polnoca";
                    }
                    int k = 0; foreach (var m in x.Members) if (m != null && m.Army == a) k++;
                    bool coh = Math.Abs(a.Cohesion - x.Cohesion) < 0.01f;
                    if (k == x.Members.Count) _tArmiesOk++;
                    if (coh) _tCohOk++;
                    var bs = a.LeaderParty != null ? a.LeaderParty.BesiegedSettlement : null;
                    Log.Info(P + "armia " + Nm(x.Leader) + " " + how + " | partii " + k + "/" + x.Members.Count + ", spojnosc " + x.Cohesion.ToString("0.#", Inv) + " -> " + a.Cohesion.ToString("0.#", Inv)
                             + " | oblezenie " + (x.Besieged != null ? Nm(x.Besieged) : "-") + " -> " + (bs != null ? Nm(bs) : "-") + " | krolestwo armii " + (a.Kingdom != null ? a.Kingdom.StringId : "-") + ".");
                }
                catch (Exception e) { Stumble("Armies", e); }
            }
        }

        // ---------------------------------------------------------------- komunikat
        private const string PopupTitle = "The King in the North and the Trident";
        private const string PopupBody = "Before Riverrun the river lords kneel to Robb Stark as the northmen did before them. House Tully and every lord of the Trident now hold their castles, villages and hosts of the King in the North. The Riverlands are no longer a realm of their own: one king leads the North and the Trident against the crown.";
        private const string PopupCatchUp = "Since the river lords bent the knee to Robb Stark, House Tully and every lord of the Trident hold their castles, villages and hosts of the King in the North. The Riverlands are no longer a realm of their own: one king leads the North and the Trident against the crown.";
        private const string PopupMerc = "Your company's contract passes from House Tully to King Robb on the same terms.";
        private const string PopupVassal = "You now hold your lands of King Robb Stark.";

        private static bool ShowPopup(bool catchUp, ClanSnap ps)
        {
            string text = catchUp ? PopupCatchUp : PopupBody;
            if (ps != null && ps.Ok) text += "\n\n" + (ps.Merc ? PopupMerc : PopupVassal);
            InformationManager.ShowInquiry(new InquiryData(PopupTitle, text, true, false, "Continue", null, null, null), true);
            Log.Player("The river lords bend the knee: the Riverlands are now part of the realm of the King in the North." + (ps != null && ps.Ok ? " " + (ps.Merc ? PopupMerc : PopupVassal) : ""));
            return true;
        }

        // ---------------------------------------------------------------- Krwawe Gody (ROT RedWeddingQuest.ExecuteRobb)
        private static void InstallRedWedding()
        {
            if (_rwTried) return;
            _rwTried = true;
            try
            {
                if (_harmony == null) { _wireRw = "BRAK (bez Harmony)"; return; }
                var t = AccessTools.TypeByName("ROT.Events.RedWeddingEvent+RedWeddingQuest");
                if (t == null) { var o = AccessTools.TypeByName("ROT.Events.RedWeddingEvent"); t = o != null ? AccessTools.Inner(o, "RedWeddingQuest") : null; }
                var m = t != null ? AccessTools.Method(t, "ExecuteRobb") : null;
                if (m == null) { _wireRw = "BRAK (ROT RedWeddingQuest.ExecuteRobb)"; return; }
                _harmony.Patch(m, postfix: new HarmonyMethod(typeof(KingInTheNorth), nameof(RedWeddingPostfix)));
                _wireRw = "TAK";
            }
            catch (Exception e) { Stumble("InstallRedWedding", e); _wireRw = "BLAD (" + e.GetType().Name + ")"; }
        }

        /// <summary>Postfiks ROT RedWeddingQuest.ExecuteRobb: ROT przenosi Freyow, Mootonow, Coxow i Brackenow do korony tylko z krolestwa Dorzecza - po unii robimy to
        /// dla rodow z unii, ktore sa w Polnocy (ROT zawarl juz w tym wywolaniu pokoj Polnoc-korona). Nie rzuca.</summary>
        public static void RedWeddingPostfix()
        {
            try
            {
                if (_st != 1 || _rw) return;
                var west = K(CrownId); var north = K(NorthId);
                var t = AccessTools.TypeByName("ROT.Misc.ROTClans");
                var res = new List<string>();
                int n = 0;
                foreach (var name in new[] { "Frey", "Mooton", "Cox", "Bracken" })
                {
                    Clan c = null;
                    try { var pi = t != null ? AccessTools.Property(t, name) : null; c = pi != null ? pi.GetValue(null, null) as Clan : null; } catch { c = null; }
                    if (c == null) { res.Add(name + " brak"); continue; }
                    if (c.IsEliminated) { res.Add(name + " wyeliminowany"); continue; }
                    if (!_clanSet.Contains(c.StringId)) { res.Add(name + " nie z unii"); continue; }
                    if (c.Kingdom != north) { res.Add(name + " poza Polnoca (" + (c.Kingdom != null ? c.Kingdom.StringId : "-") + ")"); continue; }
                    if (!Alive(west)) { res.Add(name + " - korony brak"); continue; }
                    ChangeKingdomAction.ApplyByJoinToKingdom(c, west, default(CampaignTime), true);
                    bool ok = c.Kingdom == west;
                    if (ok) n++;
                    res.Add(name + " (" + c.StringId + ") -> " + west.StringId + " " + (ok ? "TAK" : "NIE"));
                }
                _rw = true;
                Log.Info(P + "Krwawe Gody - rody Dorzecza ROT do korony (jak ROT dla krolestwa Dorzecza): " + string.Join(", ", res.ToArray()) + " | przeniesionych " + n + " | Polnoc-korona "
                         + (Alive(west) && Alive(north) && north.IsAtWarWith(west) ? "w wojnie" : "pokoj") + ".");
                try { SplitAtRedWedding(north, west); } catch (Exception e) { Stumble("SplitAtRedWedding", e); }
            }
            catch (Exception e) { Stumble("RedWeddingPostfix", e); }
        }

        private static Clan RotClan(string name)
        {
            try
            {
                var t = AccessTools.TypeByName("ROT.Misc.ROTClans");
                var pi = t != null ? AccessTools.Property(t, name) : null;
                return pi != null ? pi.GetValue(null, null) as Clan : null;
            }
            catch { return null; }
        }

        /// <summary>Recenzja 272 - Krwawe Gody: ROT zawarl pokoj Polnoc-korona (ExecuteRobb), wiec rzeczni lordowie z unii byli w pokoju z korona, a okna Kroniki
        /// war:riverlands:vlandia (063.1 Oblezenie Riverrun, 070.2 Raventree) przepadaly. Bez unii ROT trzyma wojne Westerlands-Riverlands bez konca (wpis usuniety, gdy
        /// unia zniszczyla Dorzecze). Teraz: Dorzecze wskrzeszone (Kingdom.ReactivateKingdom), wladca Tully (lord_1_3; zapas Blackfish ROT, potem rod z najwieksza
        /// liczba lenn) - ChangeRulingClanAction przed wejsciem rodu jak gra przy CreateKingdom; wasale AI z unii, ktorzy sa jeszcze w Polnocy, wracaja
        /// (ApplyByJoinToKingdom, wplywy i dlug wobec krolestwa jak w unii); najemnicy i gracz zostaja u Polnocy; wojna z korona (DeclareWarAction z flaga ROT
        /// CallingDeclareWar) i potem co dobe SplitWar (jak wpis ROT). Stan 3 - aliasy Kroniki wylaczone (riverlands = Dorzecze). Nie rzuca (wywolujacy ma try).</summary>
        private static void SplitAtRedWedding(Kingdom north, Kingdom crown)
        {
            Fields();
            var river = K(RiverId);
            var pc = Clan.PlayerClan;
            string skip = null;
            var back = new List<Clan>();
            if (river == null) skip = "brak krolestwa " + RiverId;
            else if (!Alive(north)) skip = "Polnoc zniszczona";
            else if (PlayerRules(north)) skip = "Polnoca rzadzi gracz - rody zostaja";
            else
            {
                foreach (var c in Clan.All)
                    if (c != null && c.StringId != null && _clanSet.Contains(c.StringId) && !c.IsEliminated && c != pc && !c.IsUnderMercenaryService && c.Kingdom == north) back.Add(c);
                if (back.Count == 0) skip = "zaden wasal AI z unii nie zostal w Polnocy";
            }
            if (skip != null) { Log.Info(P + "Krwawe Gody - Dorzecze NIE rozdzielone (" + skip + ") - rody z unii zostaja w Polnocy, aliasy Kroniki dalej."); return; }
            Clan ruler = null; string rwhy = "";
            var th = HeroOf(TullyLordId);
            if (th != null && th.Clan != null && back.Contains(th.Clan)) { ruler = th.Clan; rwhy = "Tully"; }
            else
            {
                var bf = RotClan("TullyBlackfish");
                if (bf != null && back.Contains(bf)) { ruler = bf; rwhy = "Blackfish - Tully poza lista"; }
            }
            if (ruler == null) { ruler = back.OrderByDescending(c => c.Fiefs.Count).First(); rwhy = "najwiecej lenn - bez Tully i Blackfisha"; }
            back.Remove(ruler); back.Insert(0, ruler);
            bool wasDead = river.IsEliminated;
            if (wasDead) river.ReactivateKingdom();
            ChangeRulingClanAction.Apply(river, ruler);
            int moved = 0, fiefs = 0;
            foreach (var c in back)
            {
                try
                {
                    float inf = c.Influence; int debt = c.DebtToKingdom; var stay = c.ShouldStayInKingdomUntil; int f0 = c.Fiefs.Count;
                    bool raw = SetInfluenceRaw(c, 0f);
                    ChangeKingdomAction.ApplyByJoinToKingdom(c, river, stay, false);
                    if (raw) SetInfluenceRaw(c, inf);
                    else { float d = inf - c.Influence; if (d != 0f) ChangeClanInfluenceAction.Apply(c, d); }
                    c.DebtToKingdom = debt;
                    bool ok = c.Kingdom == river;
                    if (ok) { moved++; fiefs += c.Fiefs.Count; }
                    Log.Info(P + "Krwawe Gody - rod " + Nm(c) + " (" + c.StringId + ") " + (c == ruler ? "wladca " : "wasal ") + "-> Dorzecze " + (ok ? "TAK" : "NIE") + " | lenna " + c.Fiefs.Count + "/" + f0
                             + " | wplywy " + inf.ToString("0.#", Inv) + " -> " + c.Influence.ToString("0.#", Inv) + " | dlug wobec krolestwa " + debt + " -> " + c.DebtToKingdom + ".");
                }
                catch (Exception e) { Stumble("Split.Clan", e); }
            }
            if (moved == 0)
            {
                // nic nie weszlo - Dorzecze z powrotem zniszczone (bez rodow DestroyKingdomAction nikogo nie niszczy), unia trwa
                try { if (wasDead && river.Clans.Count(c => c != null && !c.IsEliminated) == 0) { river.RulingClan = null; DestroyKingdomAction.Apply(river); } } catch (Exception e) { Stumble("Split.Undo", e); }
                Log.Info(P + "Krwawe Gody - Dorzecze NIE rozdzielone (zaden rod nie wszedl) - unia trwa.");
                return;
            }
            if (river.RulingClan != ruler && ruler.Kingdom == river) ChangeRulingClanAction.Apply(river, ruler);
            string war;
            if (!Alive(crown)) war = "korony brak";
            else if (PlayerRules(crown) || PlayerRules(river)) war = "krolestwo gracza - poza regula";
            else war = river.IsAtWarWith(crown) || Prologue.RotDeclareWar272(river, crown) ? "TAK" : "NIE";
            _st = 3;
            string role = pc == null || pc.Kingdom == null ? "bez krolestwa" : (pc.IsUnderMercenaryService ? "najemnik " : "wasal ") + pc.Kingdom.StringId;
            Log.Info(P + "Krwawe Gody - Dorzecze znowu osobno | krolestwo " + (wasDead ? "wskrzeszone" : "bylo zywe") + " | wladca " + Nm(ruler) + " (" + ruler.StringId + ", " + rwhy + ") | rodow przeniesionych "
                     + moved + "/" + back.Count + ", lenna " + fiefs + " | wojna z korona " + war + " | gracz zostaje: " + role + " | aliasy Kroniki wylaczone (riverlands = Dorzecze).");
            Log.Player("With the Young Wolf dead, the river lords hold to House Tully: the Riverlands stand as a realm of their own again" + (war == "TAK" ? ", still at war with the crown." : "."));
        }

        /// <summary>Stan 3 (co dobe): wojna Dorzecze-korona jak wpis ROT Westerlands-Riverlands bez konca (usuniety przez ROT, gdy unia zniszczyla Dorzecze).</summary>
        private static void SplitWar()
        {
            var river = K(RiverId); var crown = K(CrownId);
            if (!Alive(river) || !Alive(crown) || PlayerRules(river) || PlayerRules(crown) || river.IsAtWarWith(crown)) return;
            bool now = Prologue.RotDeclareWar272(river, crown);
            Log.Info(P + "Dorzecze po Krwawych Godach - wojna z korona wznowiona " + (now ? "TAK" : "NIE") + " (jak wpis ROT Westerlands-Riverlands bez konca) | dzien opowiesci " + F1(StoryDay()) + ".");
        }
    }

    /// <summary>Drobne odczyty stalych A2/A3 (Prologue.River.cs) bez zmiany ich widocznosci w tamtym pliku (264 zmienia te same linie).</summary>
    internal static partial class Prologue
    {
        internal static double RiverNetDays272 { get { return RiverNetDays; } }
        internal static int RiverAllyRetryDays272 { get { return RiverAllyRetryDays; } }

        /// <summary>Recenzja 272: DeclareWarAction z flaga ROT CallingDeclareWar na czas wywolania (jak RiverWar / EssosDaily). Zwraca stan wojny po wywolaniu.</summary>
        internal static bool RotDeclareWar272(IFaction a, IFaction b)
        {
            object beh = null; bool prev = false;
            try { RotWarsReady(); beh = RotWarsBehavior(); } catch { beh = null; }
            try
            {
                if (_fCalling != null && beh != null) { prev = (bool)_fCalling.GetValue(beh); _fCalling.SetValue(beh, true); }
                DeclareWarAction.ApplyByDefault(a, b);
            }
            finally { if (_fCalling != null && beh != null) _fCalling.SetValue(beh, prev); }
            return FactionManager.IsAtWarAgainstFaction(a, b);
        }
    }

    /// <summary>272: zachowanie kampanii - przeglad co godzine, start sesji, zapis arm_k272.</summary>
    internal sealed class KingInTheNorthBehavior : CampaignBehaviorBase
    {
        public KingInTheNorthBehavior(Harmony h) { KingInTheNorth.Reset(); KingInTheNorth.SetHarmony(h); }

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, delegate (CampaignGameStarter s) { try { KingInTheNorth.SessionStart(); } catch (Exception e) { KingInTheNorth.Stumble("SessionStart", e); } });
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, DayProfile.Hr("KingInTheNorth.Hourly", delegate { try { KingInTheNorth.Hourly(); } catch (Exception e) { KingInTheNorth.Stumble("Hourly", e); } }));
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, delegate { try { KingInTheNorth.Daily(); } catch (Exception e) { KingInTheNorth.Stumble("Daily", e); } });
        }

        public override void SyncData(IDataStore dataStore)
        {
            string v = "";
            try { if (dataStore.IsSaving) v = KingInTheNorth.Export(); } catch (Exception e) { KingInTheNorth.Stumble("Export", e); v = ""; }
            SaveText.Sync(dataStore, "arm_k272", ref v);
            if (dataStore.IsLoading) { try { KingInTheNorth.Import(v); } catch (Exception e) { KingInTheNorth.Stumble("Import", e); } }
        }
    }
}
