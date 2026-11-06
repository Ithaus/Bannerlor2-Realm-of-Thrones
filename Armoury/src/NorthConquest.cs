using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// 326b - ZWROT ZDOBYCZY POLNOCY (Jeff 05.11: "oddaj greyjoyom zabrane ziemie przez Roba"; "Clegane's Keep, Deep Den ... oddaj to po zdobyciu prawowitym wlascicielom").
    /// DOWOD: Armoury-2026-11-04_23-31-36.log, dzien 109348 - "Rownowaga wojny (274)": twierdze Polnocy 44 (start 14), korony 14 (start 21); Ten Towers (Harlaw) u Boltonow
    /// (Polnoc), Theon jencem w Ten Towers; OB5 "Clegane's Keep ... ZDOBYTE" przez Polnoc. W ksiazkach Robb wygrywa bitwy, ale nie trzyma zamkow Zachodu ani wysp.
    /// REGULA (klucz NorthConquestReturn): dopoki kanon Polnocy trwa (KingInTheNorth.FightsOnEnd == null - do wyjscia fazy 2 po Bitwie Bekartow), twierdza (miasto/zamek
    /// z wioskami), ktorej pan jest w krolestwie Polnocy ("battania"), a ktorej pan z poczatku kampanii (ROT-Map settlements.xml owner=) byl rodem spoza Polnocy
    /// (ROT-Content spclans.xml super_faction != battania) i nie jest dzis jej panem - wraca:
    ///  - do rodu pierwotnego, gdy zyje, ma wolna zywa glowe, nie jest rodem gracza i ma krolestwo (takze, gdy dzis sluzy Polnocy - np. rody rzeczne po unii 272:
    ///    zamek wraca do swojego pana wewnatrz krolestwa, zaloga zostaje);
    ///  - inaczej do rodu panujacego krolestwa, w ktorym rod pierwotny byl na poczatku (domena krola - jak Rw320), gdy krolestwo zyje i nie jest Polnoca;
    ///  - inaczej zostaje (log: powod; np. Dorzecze wchloniete unia 272 i rodu pierwotnego brak).
    ///  Wyjatki: Harrenhal (ROT_town32 - Boltonowie trzymaja go dla Robba, jak w ksiazce i BookHoldings), Red Keep (ROT_castle61 - blizniak ROT, idzie z King's Landing).
    /// KIEDY: (1) raz po wczytaniu zapisu bez flagi (arm_k326b) - spis wszystkich takich twierdz do kolejki; (2) na biezaco - zdobycie przez Polnoc (BySiege) dopisuje
    /// twierdze do kolejki, zwrot najwczesniej po MinDays dobach od zdobycia. Kolejka probowana co godzine, gdy gracz stoi na mapie (BookHoldings.PlayerOnMapFor);
    /// sama zmiana pana droga BookHoldings (TransferFor -> TryRow): warunki chwilowe (oblezenie, bitwa przy bramie), bohaterowie bez druzyny wyprowadzeni, zaloga Polnocy
    /// do partii Polnocy (bez ludzi z niczego), roszczenie SCA Polnocy jak przed zmiana, ApplyByDefault (nie BySiege - bez kar i bez naszych sluchaczy zdobycia),
    /// jency swoi do zalogi, BEZ obsady zalogi (326x, Jeff 05.11: zaloga z werbunku nowego pana, na ile go stac); po zmianie roszczenie SCA krolestwa odbiorcy na te osade zdjete. Polnoc zatrzymuje lup gry
    /// z oblezenia (nic nie tworzymy). Gracz: twierdza gracza, odbiorca = rod gracza, Polnoc albo krolestwo odbiorcy pod rzadami gracza - pominiete z linia.
    /// Rw320 (zwrot po Krwawych Godach) dotyczy twierdz rodow, ktore przeszly do korony - te nie sa juz w Polnocy, wiec 326b ich nie rusza (bez zderzenia).
    /// Linie "Zwrot zdobyczy Polnocy (326b)": spis po wczytaniu, kazde zdobycie, kazdy zwrot (linia TryRow z tym znacznikiem), podsumowanie dobowe.
    /// PO RECENZJI (3 recenzje 05.11):
    ///  - zwrot najwczesniej 24 GODZINY po zdobyciu (kolejka liczy godziny, nie doby - zdobycie o 23:00 nie wraca o polnocy);
    ///  - WETO 60 dob: zwrocona twierdza (i blizniak, i jej wsie przy najezdzie) nie jest celem oblezenia ani najazdu Polnocy - postfiks mysli AI
    ///    (CampaignEventDispatcher.AiHourlyTick, ocena -1 jak weta 274) i BK (FindArmyObjective -> brak celu); trwajace oblezenie Polnocy nie jest zdejmowane;
    ///    bez tego Polnoc krecila zdobycie-zwrot-zdobycie (lup, wplywy, straty zalog); weto w zapisie (arm_k326b, format nc2);
    ///  - jency-bohaterowie Polnocy (w wojnie z Polnoca) z lochu oddawanej twierdzy przeniesieni przed zmiana do lochu najblizszej twierdzy Polnocy
    ///    (TransferPrisonerAction - droga gry), inaczej gra wypuszcza ich przy zmianie pana (Theon w Ten Towers); brak takiej twierdzy - czekamy;
    ///  - glowa rodu pierwotnego w niewoli: czekamy (do 15 dob, potem krol); w lochu tej samej twierdzy - zwrot od razu (gra wypuszcza ja w tym samym zdarzeniu);
    ///  - rod pierwotny najemnikiem - jak brak rodu (krol); Dorzecze po unii 272 (brak krolestwa riverlands) - zwrot tylko rodowi, ktory dzis jest w Polnocy;
    ///  - Skagos (skagosi) i Wolni Ludzie (freefolk) poza regula; tylko kampania kanonu (Prologue.CanonHere); koniec takze po naturalnej Bitwie Bekartow
    ///    (BastardsSiegeEvent 2), nie tylko po FightsOnEnd;
    ///  - najwyzej 3 zwroty na dobe (spis po wczytaniu rozlozony), obsada zalogi tylko od partii krolestwa odbiorcy w promieniu 80 od bramy;
    ///  - czekanie najwyzej 30 dob na osade (potem z kolejki z linia); klucz wlaczony ponownie w tej samej grze - spis jeszcze raz.
    /// </summary>
    internal static class NorthConquest
    {
        /// <summary>Gracz stoi w tej twierdzy, jej blizniaku albo jej wsi (autotest z armoury-wlosci-test.flag przepuszcza gracza w miescie - tu wtedy czekamy).</summary>
        private static bool PlayerAt(Settlement st, Settlement twin)
        {
            var mp = MobileParty.MainParty;
            var cs = mp != null ? mp.CurrentSettlement : null;
            if (cs == null) return false;
            if (cs == st || (twin != null && cs == twin)) return true;
            var v = cs.Village;
            var b = v != null ? v.Bound : null;
            return b != null && (b == st || (twin != null && b == twin));
        }

        internal const string Tag = "Zwrot zdobyczy Polnocy (326b): ";
        private const string NorthId = "battania", KingsLandingId = "town_EW4", RedKeepId = "ROT_castle61";
        internal const int MinDays = 1;
        private const int MinHours = MinDays * 24;
        private const int GiveUpDays = 30;          // tyle dob czekania na osade - potem z kolejki
        private const int PrisonerKingDays = 15;    // glowa rodu pierwotnego w niewoli tak dlugo - potem rod panujacy
        internal const int VetoDays = 60;           // weto oblezen i najazdow Polnocy na zwrocona twierdze
        private const int MaxPerDay = 3;            // zwrotow na dobe (spis po wczytaniu rozlozony - obsada zalog z krolestw odbiorcow)
        private const float DonorRadius = 80f;      // obsada zalogi tylko od partii krolestwa odbiorcy w tym promieniu od bramy
        private const int MaxErrors = 3;
        private static readonly HashSet<string> Keep = new HashSet<string>(StringComparer.Ordinal) { "ROT_town32", RedKeepId };
        private static readonly HashSet<string> NotForeign = new HashSet<string>(StringComparer.Ordinal) { NorthId, "skagosi", "freefolk" };   // Skagos - wasal Polnocy; Wolni Ludzie - wojna stala
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        internal static bool On { get { var s = Settings.Current; return s != null && s.NorthConquestReturn; } }

        // ---- zapis arm_k326b
        private static string _init = "";                                                       // "" - spis czeka, "done" - zrobiony
        private static readonly Dictionary<string, int> _queue = new Dictionary<string, int>(StringComparer.Ordinal);   // osada -> GODZINA zdobycia (spis: teraz - 24)
        private static int _total;                                                              // zwroconych od paczki
        private static readonly Dictionary<string, int> _veto = new Dictionary<string, int>(StringComparer.Ordinal);    // osada -> godzina konca weta Polnocy

        // ---- sesja
        private static readonly Dictionary<string, int> _err = new Dictionary<string, int>(StringComparer.Ordinal);
        private static readonly HashSet<string> _errSites = new HashSet<string>(StringComparer.Ordinal);
        private static int _stumbles, _today, _todayDay = -1, _waitDay = -1, _sumDay = -1;
        private static string _waitText;
        private static readonly List<string> _todayDone = new List<string>();
        private static readonly List<string> _todaySkip = new List<string>();
        private static string _lastWaits = "";
        private static bool _wasOff, _notCanonLogged;
        private static int _vSiege, _vRaid, _vBk, _vExpired, _vSayDay = -1;
        private static readonly HashSet<string> _vSaid = new HashSet<string>(StringComparer.Ordinal);
        private static Harmony _harmony;
        private static bool _hooksTried;
        private static string _wThink = "BRAK", _wBk = "BRAK";

        internal static void Reset()
        {
            _init = ""; _queue.Clear(); _total = 0; _veto.Clear();
            _err.Clear(); _errSites.Clear(); _stumbles = 0; _today = 0; _todayDay = -1; _waitDay = -1; _sumDay = -1; _waitText = null;
            _todayDone.Clear(); _todaySkip.Clear(); _lastWaits = "";
            _wasOff = false; _notCanonLogged = false; _vSiege = _vRaid = _vBk = _vExpired = 0; _vSayDay = -1; _vSaid.Clear();
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_errSites.Add(where)) Log.Error("NorthConquest." + where, e); } catch { }
        }

        private static string Nm(Settlement s) { try { return s != null && s.Name != null ? s.Name.ToString() : "-"; } catch { return "?"; } }
        private static string Nm(IFaction f) { try { return f != null && f.Name != null ? f.Name.ToString() : "-"; } catch { return "?"; } }

        private static Kingdom K(string id)
        {
            foreach (var k in Kingdom.All) if (k != null && k.StringId == id) return k;
            return null;
        }

        private static bool PlayerRules(Kingdom k)
        {
            try { return k != null && Hero.MainHero != null && k.Leader == Hero.MainHero; } catch { return false; }
        }

        // ------------------------------------------------------------ zapis
        internal static string Export()
        {
            var sb = new StringBuilder(128);
            sb.Append("nc2|").Append(_init).Append('|');
            Map(sb, _queue);
            sb.Append('|').Append(_total.ToString(Inv)).Append('|');
            Map(sb, _veto);
            return sb.ToString();
        }

        private static void Map(StringBuilder sb, Dictionary<string, int> d)
        {
            bool first = true;
            foreach (var kv in d) { if (!first) sb.Append(';'); first = false; sb.Append(kv.Key).Append('=').Append(kv.Value.ToString(Inv)); }
        }

        private static void ReadMap(string v, Dictionary<string, int> d, int mul)
        {
            if (string.IsNullOrEmpty(v)) return;
            foreach (var e in v.Split(';'))
            {
                int i = e.IndexOf('='); int x;
                if (i > 0 && int.TryParse(e.Substring(i + 1), NumberStyles.Integer, Inv, out x)) d[e.Substring(0, i)] = x * mul;
            }
        }

        internal static void Import(string v)
        {
            Reset();
            if (string.IsNullOrEmpty(v)) return;   // zapis sprzed paczki - spis czeka
            try
            {
                var p = v.Split('|');
                bool v1 = p.Length >= 4 && p[0] == "nc1", v2 = p.Length >= 5 && p[0] == "nc2";
                if (!v1 && !v2) { _init = "done"; Log.Info(Tag + "zapis w nieznanym formacie - spis uznany za zrobiony, kolejka pusta."); return; }
                _init = p[1] == "done" ? "done" : "";
                ReadMap(p[2], _queue, v1 ? 24 : 1);   // nc1 - doby, nc2 - godziny
                int t; _total = int.TryParse(p[3], NumberStyles.Integer, Inv, out t) ? t : 0;
                if (v2) ReadMap(p[4], _veto, 1);
            }
            catch (Exception e) { _init = "done"; Stumble("Import", e); }
        }

        internal static void SetHarmony(Harmony h) { _harmony = h; }

        internal static void SessionStart()
        {
            try
            {
                EnsureHooks();
                Log.Info(Tag + "start sesji | klucz North Conquest Return " + (On ? "TAK" : "NIE") + " | zakres: " + (Prologue.CanonHere ? "kampania kanonu" : "kampania klasyczna - nie dotyczy")
                         + " | spis po wczytaniu " + (_init == "done" ? "zrobiony" : "czeka (pierwsza godzina na mapie)")
                         + " | w kolejce " + _queue.Count + (_queue.Count > 0 ? " [" + string.Join(", ", _queue.Keys.ToArray()) + "]" : "") + " | zwroconych od paczki " + _total
                         + " | weta Polnocy (oblezenie/najazd zwroconych, " + VetoDays + " dob) " + _veto.Count + (_veto.Count > 0 ? " [" + VetoText() + "]" : "")
                         + " | latki weta: mysl AI " + _wThink + ", BK cel armii " + _wBk + ".");
            }
            catch (Exception e) { Stumble("SessionStart", e); }
        }

        private static int NowHour { get { return (int)CampaignTime.Now.ToHours; } }

        private static string VetoText()
        {
            var l = new List<string>();
            foreach (var kv in _veto)
            {
                var s = Settlement.Find(kv.Key);
                l.Add((s != null ? Nm(s) : kv.Key) + " do dnia " + (kv.Value / 24));
            }
            return string.Join(", ", l.ToArray());
        }

        // ------------------------------------------------------------ regula
        /// <summary>Kanon Polnocy skonczony: wyjscie fazy 2 (FightsOnEnd) albo naturalna Bitwa Bekartow (BastardsSiegeEvent 2). Nie rzuca.</summary>
        private static bool CanonOver(out string why)
        {
            why = null;
            try
            {
                if (KingInTheNorth.FightsOnEnd != null) { why = KingInTheNorth.FightsOnEnd; return true; }
                if (Prologue.RotEventState(CanonPeaceLock.FightsOnEvent) >= 2) { why = "Bitwa Bekartow skonczona (" + CanonPeaceLock.FightsOnEvent + " 2)"; return true; }
            }
            catch (Exception e) { Stumble("CanonOver", e); }
            return false;
        }

        /// <summary>Twierdza Polnocy spoza jej ziem z poczatku kampanii. Nie rzuca.</summary>
        private static bool Candidate(Settlement st, out string origId, out string origK)
        {
            origId = null; origK = null;
            try
            {
                if (st == null || !st.IsFortification || st.Town == null || Keep.Contains(st.StringId)) return false;
                var oc = st.OwnerClan;
                if (oc == null || oc.Kingdom == null || oc.Kingdom.StringId != NorthId) return false;
                origId = KingInTheNorth.OrigOwnerOf(st.StringId, out origK);
                if (origId == null || origK == null || NotForeign.Contains(origK)) return false;
                return origId != oc.StringId;
            }
            catch (Exception e) { Stumble("Candidate", e); return false; }
        }

        private static bool PrisonerAt(Hero h, Settlement st, Settlement twin)
        {
            var pp = h != null && h.IsPrisoner ? h.PartyBelongedToAsPrisoner : null;
            return pp != null && ((st != null && pp == st.Party) || (twin != null && pp == twin.Party));
        }

        /// <summary>
        /// Prawowity pan: rod pierwotny, inaczej rod panujacy jego krolestwa z poczatku. null i stay=false - czekaj (why); null i stay=true - zostaje (why).
        /// here = glowa odbiorcy jencem w lochu tej twierdzy (zwrot dozwolony - gra wypuszcza ja przy zmianie pana).
        /// </summary>
        private static Clan Recipient(Settlement st, Settlement twin, string origId, string origK, Kingdom north, int waitedHours, out string why, out bool stay, out bool here)
        {
            why = null; stay = true; here = false;
            var orig = KingInTheNorth.ClanByIdFor(origId);
            var pc = Clan.PlayerClan;
            if (orig == pc) { why = "rod pierwotny to gracz - decyzja gracza, zostaje"; return null; }
            var kr = origK == "riverlands" ? K("riverlands") : null;
            bool riverGone = origK == "riverlands" && (kr == null || kr.IsEliminated);
            bool usable = orig != null && !orig.IsEliminated && orig.Kingdom != null && !orig.IsUnderMercenaryService && orig.Leader != null && orig.Leader.IsAlive;
            if (usable)
            {
                if (riverGone && orig.Kingdom != north) { why = "Dorzecze w Polnocy (unia 272), rod pierwotny dzis w " + Nm(orig.Kingdom) + " - zostaje"; return null; }
                if (!orig.Leader.IsPrisoner) { why = "rod pierwotny" + (orig.Kingdom == north ? " (dzis w Polnocy - zwrot wewnatrz krolestwa)" : ""); return orig; }
                if (PrisonerAt(orig.Leader, st, twin)) { here = true; why = "rod pierwotny (glowa w lochu tej twierdzy - gra wypuszcza ja przy zmianie pana)"; return orig; }
                if (waitedHours < PrisonerKingDays * 24) { stay = false; why = "glowa rodu pierwotnego " + Nm(orig.Leader) + " w niewoli - czekamy (do " + PrisonerKingDays + " dob, potem krol)"; return null; }
            }
            string whyOrig = orig == null ? "rodu pierwotnego " + origId + " brak" : orig.IsEliminated ? "rod pierwotny wyeliminowany"
                           : orig.Kingdom == null ? "rod pierwotny bez krolestwa" : orig.IsUnderMercenaryService ? "rod pierwotny najemnikiem"
                           : orig.Leader == null || !orig.Leader.IsAlive ? "rod pierwotny bez zywej glowy" : "glowa rodu pierwotnego w niewoli ponad " + PrisonerKingDays + " dob";
            if (riverGone) { why = whyOrig + ", krolestwa riverlands brak (Dorzecze w Polnocy - unia 272) - zostaje"; return null; }
            var k = K(ReachJoinsCrown.AliasKingdom(origK));   // 332: Reach w koronie - rod panujacy korony
            if (k == null || k.IsEliminated) { why = whyOrig + ", krolestwa " + origK + " brak - zostaje"; return null; }
            if (k == north) { why = whyOrig + ", krolestwo pierwotne to Polnoc - zostaje"; return null; }
            var rc = k.RulingClan;
            if (rc == null || rc.IsEliminated || rc == pc || rc.Leader == null || !rc.Leader.IsAlive) { why = whyOrig + ", rod panujacy " + Nm(k) + " niedostepny - zostaje"; return null; }
            if (rc.Leader.IsPrisoner)
            {
                if (PrisonerAt(rc.Leader, st, twin)) { here = true; why = "rod panujacy " + Nm(k) + " - " + whyOrig + " (glowa w lochu tej twierdzy)"; return rc; }
                stay = false; why = whyOrig + ", glowa rodu panujacego " + Nm(k) + " w niewoli - czekamy"; return null;
            }
            why = "rod panujacy " + Nm(k) + " - " + whyOrig;
            return rc;
        }

        /// <summary>
        /// Jency-bohaterowie Polnocy (w wojnie z Polnoca) z lochu oddawanych twierdz - do lochu najblizszej twierdzy Polnocy spoza reguly (TransferPrisonerAction, jak gra
        /// przy rozwiazaniu partii). Bez tego gra wypuszcza ich przy zmianie pana. false - brak twierdzy docelowej (czekamy). Nie rusza gracza ani glowy odbiorcy.
        /// </summary>
        private static bool MovePrisoners(Settlement st, Settlement twin, Kingdom north, Hero keep, out string info)
        {
            info = null;
            var heroes = new List<KeyValuePair<Hero, Settlement>>();
            foreach (var s in new[] { st, twin })
            {
                if (s == null || s.Party == null || s.Party.PrisonRoster == null) continue;
                var pr = s.Party.PrisonRoster;
                for (int i = 0; i < pr.Count; i++)
                {
                    var c = pr.GetCharacterAtIndex(i);
                    var h = c != null && c.IsHero ? c.HeroObject : null;
                    if (h == null || h == Hero.MainHero || h == keep || !h.IsPrisoner || h.PartyBelongedToAsPrisoner != s.Party) continue;
                    if (h.MapFaction == null || !h.MapFaction.IsAtWarWith(north)) continue;
                    heroes.Add(new KeyValuePair<Hero, Settlement>(h, s));
                }
            }
            if (heroes.Count == 0) return true;
            Settlement dest = null; float best = float.MaxValue;
            var p = st.GatePosition.ToVec2();
            foreach (var s in Settlement.All)
            {
                if (s == null || s == st || s == twin || !s.IsFortification || s.Town == null || s.Party == null || s.IsUnderSiege) continue;
                var oc = s.OwnerClan;
                if (oc == null || oc.Kingdom != north || _queue.ContainsKey(s.StringId)) continue;
                string a, b;
                if (Candidate(s, out a, out b)) continue;   // tez do oddania - loch nie na dlugo
                float d = s.GatePosition.ToVec2().Distance(p);
                if (d < best) { best = d; dest = s; }
            }
            if (dest == null) { info = "brak twierdzy Polnocy na " + heroes.Count + " jencow-bohaterow"; return false; }
            var moved = new List<string>();
            foreach (var kv in heroes)
            {
                try
                {
                    TransferPrisonerAction.Apply(kv.Key.CharacterObject, kv.Value.Party, dest.Party);
                    moved.Add(Nm(kv.Key));
                }
                catch (Exception e) { Stumble("MovePrisoner", e); }
            }
            info = "jency-bohaterowie Polnocy do lochu " + Nm(dest) + ": " + moved.Count + (moved.Count > 0 ? " [" + string.Join(", ", moved.ToArray()) + "]" : "");
            return true;
        }

        private static string Nm(Hero h) { try { return h != null && h.Name != null ? h.Name.ToString() : "-"; } catch { return "?"; } }

        // ------------------------------------------------------------ zdarzenia
        internal static void OnOwnerChanged(Settlement st, bool openToClaim, Hero newOwner, Hero oldOwner, Hero capturer, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
        {
            try
            {
                if (!On || detail != ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.BySiege || st == null) return;
                if (!Prologue.CanonHere) return;
                string over;
                if (CanonOver(out over)) return;
                string origId, origK;
                if (!Candidate(st, out origId, out origK)) return;
                int hour = NowHour;
                if (!_queue.ContainsKey(st.StringId)) _queue[st.StringId] = hour;
                int vu;
                bool vetoed = _veto.TryGetValue(st.StringId, out vu) && vu > hour;
                Log.Info(Tag + "dzien " + (hour / 24) + " | " + Nm(st) + " [" + st.StringId + "] zdobyta przez " + (newOwner != null && newOwner.Clan != null ? newOwner.Clan.Name.ToString() : "-")
                         + " (Polnoc) od " + (oldOwner != null && oldOwner.Clan != null ? oldOwner.Clan.Name.ToString() : "-") + " | pierwotnie " + origId + " (" + origK + ")"
                         + " | do kolejki - zwrot najwczesniej za " + MinHours + " godzin, gdy przy osadzie spokoj (lup oblezenia zostaje u zdobywcow)"
                         + (vetoed ? " | ZDOBYTA MIMO WETA (do dnia " + (vu / 24) + ") - droga spoza mysli AI i BK (gracz w armii, inny mod)" : "") + ".");
            }
            catch (Exception e) { Stumble("OnOwnerChanged", e); }
        }

        /// <summary>HourlyTickEvent (NorthConquestBehavior). Nie rzuca.</summary>
        internal static void Hourly()
        {
            if (!On) { _wasOff = true; return; }
            try
            {
                var c = Campaign.Current;
                if (c == null || !c.GameStarted) return;
                int hour = NowHour, day = hour / 24;
                if (day != _todayDay) { Summary(); _todayDay = day; _today = 0; _todayDone.Clear(); _todaySkip.Clear(); _vSiege = _vRaid = _vBk = _vExpired = 0; }
                if (_wasOff)
                {
                    _wasOff = false;
                    if (_init == "done") { _init = ""; Log.Info(Tag + "klucz wlaczony ponownie - spis twierdz jeszcze raz (zdobycze z czasu wylaczenia)."); }
                }
                if (!Prologue.CanonHere)
                {
                    if (!_notCanonLogged) { _notCanonLogged = true; Log.Info(Tag + "kampania klasyczna - regula nie dotyczy."); }
                    return;
                }
                foreach (var id in _veto.Keys.ToList()) if (_veto[id] <= hour) { _veto.Remove(id); _vExpired++; }
                var north = K(NorthId);
                if (north == null || north.IsEliminated) { if (_queue.Count > 0 || _veto.Count > 0) { Log.Info(Tag + "Polnocy brak - kolejka (" + _queue.Count + ") i weta (" + _veto.Count + ") wyczyszczone."); _queue.Clear(); _veto.Clear(); } _init = "done"; return; }
                string over;
                if (CanonOver(out over)) { if (_queue.Count > 0 || _veto.Count > 0 || _init != "done") { Log.Info(Tag + "kanon Polnocy skonczony (" + over + ") - regula nie dotyczy, kolejka (" + _queue.Count + ") i weta (" + _veto.Count + ") wyczyszczone."); _queue.Clear(); _veto.Clear(); _init = "done"; } return; }
                if (PlayerRules(north)) { WaitLog(day, "gracz rzadzi Polnoca - jego decyzja"); return; }
                string why;
                if (!BookHoldings.PlayerOnMapFor(out why)) { WaitLog(day, why); return; }

                if (_init != "done")
                {
                    _init = "done";
                    var list = new List<string>();
                    foreach (var st in Settlement.All)
                    {
                        string origId, origK;
                        if (!Candidate(st, out origId, out origK)) continue;
                        if (!_queue.ContainsKey(st.StringId)) _queue[st.StringId] = hour - MinHours;
                        list.Add(Nm(st) + " [" + st.StringId + "] u " + (st.OwnerClan != null ? st.OwnerClan.Name.ToString() : "-") + ", pierwotnie " + origId + " (" + origK + ")");
                    }
                    Log.Info(Tag + "SPIS po wczytaniu (raz na zapis) - dzien " + day + " | twierdz Polnocy spoza jej ziem z poczatku kampanii: " + list.Count
                             + (list.Count > 0 ? " [" + string.Join("; ", list.ToArray()) + "]" : "") + " | dane ROT: " + KingInTheNorth.OrigDataText()
                             + " | wyjatki: Harrenhal (Boltonowie dla Robba), Red Keep (z King's Landing), Skagos i Wolni Ludzie | zwroty najwyzej " + MaxPerDay + " na dobe.");
                }
                if (_queue.Count == 0) return;

                var waits = new List<string>();
                var msg = new List<string>();
                var ids = _queue.Keys.ToList();
                ids.Sort((a, b) => _queue[a].CompareTo(_queue[b]));
                foreach (var id in ids)
                {
                    try
                    {
                        var st = Settlement.Find(id);
                        string origId, origK;
                        if (st == null || !Candidate(st, out origId, out origK))
                        { _queue.Remove(id); _todaySkip.Add(id + " (juz nie u Polnocy albo poza regula)"); continue; }
                        int waited = hour - _queue[id];
                        if (waited < MinHours) { waits.Add(Nm(st) + ": swiezo zdobyta"); continue; }
                        if (waited >= (GiveUpDays + MinDays) * 24)
                        {
                            _queue.Remove(id); _todaySkip.Add(Nm(st) + " (czekanie ponad " + GiveUpDays + " dob)");
                            Log.Info(Tag + "dzien " + day + " | " + Nm(st) + " [" + id + "] - czekanie ponad " + GiveUpDays + " dob, z kolejki (zostaje u " + Nm(st.OwnerClan) + "; ostatnio: " + (_lastWaits.Length > 0 ? _lastWaits : "-") + ").");
                            continue;
                        }
                        if (_today >= MaxPerDay) { waits.Add(Nm(st) + ": limit " + MaxPerDay + " zwrotow na dobe"); continue; }
                        Settlement twin = id == KingsLandingId ? Settlement.Find(RedKeepId) : null;
                        string rwhy; bool stay, here;
                        var to = Recipient(st, twin, origId, origK, north, waited - MinHours, out rwhy, out stay, out here);
                        if (to == null && !stay) { waits.Add(Nm(st) + ": " + rwhy); continue; }
                        if (to == null) { _queue.Remove(id); _todaySkip.Add(Nm(st) + " (" + rwhy + ")"); Log.Info(Tag + "dzien " + day + " | " + Nm(st) + " [" + id + "] zostaje u " + Nm(st.OwnerClan) + ": " + rwhy + "."); continue; }
                        if (PlayerRules(to.Kingdom)) { _queue.Remove(id); _todaySkip.Add(Nm(st) + " (odbiorca w krolestwie gracza)"); Log.Info(Tag + "dzien " + day + " | " + Nm(st) + " [" + id + "] - odbiorca " + Nm(to) + " w krolestwie gracza - pominiete (decyzja gracza)."); continue; }
                        if (PlayerAt(st, twin)) { waits.Add(Nm(st) + ": gracz w tej osadzie (albo jej wsi)"); continue; }   // autotest (armoury-wlosci-test.flag): gracz w miescie nie blokuje calej proby - nigdy zmiana pana z graczem w srodku
                        string pinfo;
                        if (!MovePrisoners(st, twin, north, to.Leader, out pinfo)) { waits.Add(Nm(st) + ": " + pinfo); continue; }
                        string wait;
                        string res = BookHoldings.TransferFor(Tag, id, twin != null ? RedKeepId : null, to, day, out wait, here, DonorRadius, noFill: true);   // 326x: bez obsady (Jeff 05.11)
                        if (res == null) { waits.Add(Nm(st) + ": " + wait + (pinfo != null ? " (" + pinfo + ")" : "")); continue; }
                        _queue.Remove(id); _err.Remove(id);
                        if (res.StartsWith("ok", StringComparison.Ordinal))
                        {
                            _total++; _today++;
                            string sca = BookHoldings.ScaDropClaim(to.Kingdom, id);
                            if (twin != null) sca += "; " + BookHoldings.ScaDropClaim(to.Kingdom, RedKeepId);
                            int until = hour + VetoDays * 24;
                            _veto[id] = until;
                            if (twin != null) _veto[RedKeepId] = until;
                            _todayDone.Add(Nm(st) + " -> " + Nm(to) + " (" + rwhy + ")");
                            msg.Add(Nm(st) + " to House " + Nm(to));
                            Log.Info(Tag + "dzien " + day + " | ZWROT " + Nm(st) + " [" + id + "] -> " + Nm(to) + " (" + to.StringId + ", " + Nm(to.Kingdom) + "; " + rwhy + ") | " + sca
                                     + (pinfo != null ? " | " + pinfo : "") + " | WETO oblezen i najazdow Polnocy do dnia " + (until / 24) + " (aktywnych wet " + _veto.Count + ").");
                        }
                        else { _todaySkip.Add(Nm(st) + " (" + res + ")"); Log.Info(Tag + "dzien " + day + " | " + Nm(st) + " [" + id + "] bez zwrotu: " + res + " (linia wyzej)."); }
                    }
                    catch (Exception e)
                    {
                        Stumble("Row(" + id + ")", e);
                        int n; _err.TryGetValue(id, out n); _err[id] = ++n;
                        if (n >= MaxErrors) { _queue.Remove(id); _todaySkip.Add(id + " (" + n + " wyjatki)"); Log.Info(Tag + "dzien " + day + " | " + id + " - " + n + " wyjatki z rzedu, pominieta (blad w logu wyzej)."); }
                    }
                }
                _lastWaits = string.Join("; ", waits.ToArray());
                if (msg.Count > 0)
                {
                    try { Log.Player("The North gives back what it took beyond its own lands: " + string.Join(", ", msg.ToArray()) + "."); } catch { }
                }
            }
            catch (Exception e) { Stumble("Hourly", e); }
        }

        private static void WaitLog(int day, string why)
        {
            if (day == _waitDay && why == _waitText) return;
            if (day == _waitDay && why != null && _waitText != null && why.StartsWith("gracz", StringComparison.Ordinal) && _waitText.StartsWith("gracz", StringComparison.Ordinal)) return;
            _waitDay = day; _waitText = why;
            if (_init == "done" && _queue.Count == 0) return;
            Log.Info(Tag + "czeka - dzien " + day + " | " + why + " | w kolejce " + _queue.Count + " (proba co godzine).");
        }

        private static void Summary()
        {
            try
            {
                if (_todayDay < 0) return;
                if (_sumDay == _todayDay) return;
                _sumDay = _todayDay;
                int vHits = _vSiege + _vRaid + _vBk;
                if (_today == 0 && _todaySkip.Count == 0 && _queue.Count == 0 && _veto.Count == 0 && vHits == 0 && _vExpired == 0) return;
                Log.Info(Tag + "doba " + _todayDay + " | zwrocone " + _today + (_todayDone.Count > 0 ? " [" + string.Join("; ", _todayDone.ToArray()) + "]" : "")
                         + " | pominiete " + _todaySkip.Count + (_todaySkip.Count > 0 ? " [" + string.Join("; ", _todaySkip.ToArray()) + "]" : "")
                         + " | w kolejce " + _queue.Count + (_lastWaits.Length > 0 ? " (czeka: " + _lastWaits + ")" : "") + " | od paczki " + _total
                         + " | weta Polnocy: aktywne " + _veto.Count + (_veto.Count > 0 ? " [" + VetoText() + "]" : "") + ", wygasle " + _vExpired
                         + ", zdjete oceny celow: oblezenia " + _vSiege + ", najazdy " + _vRaid + ", BK cel armii " + _vBk + " | potkniecia " + _stumbles + ".");
            }
            catch (Exception e) { Stumble("Summary", e); }
        }

        // ------------------------------------------------------------ weto
        /// <summary>Weto Polnocy na te osade (twierdza zwrocona przez 326b; wies - wedlug twierdzy, do ktorej nalezy). Twierdza znow u Polnocy - bez weta. Nie rzuca.</summary>
        private static bool Vetoed(Settlement st, int hour)
        {
            if (st == null) return false;
            var fort = st.IsVillage && st.Village != null ? st.Village.Bound : st;
            int until;
            if (fort == null || !_veto.TryGetValue(fort.StringId, out until) || until <= hour) return false;
            var oc = fort.OwnerClan;
            return oc == null || oc.Kingdom == null || oc.Kingdom.StringId != NorthId;
        }

        /// <summary>334 B8: weto 326b dla krolestwa k na osade st teraz (te same warunki co BkObjectivePostfix). Tylko odczyt.</summary>
        internal static bool VetoedFor(Kingdom k, Settlement st)
        {
            if (_veto.Count == 0 || k == null || st == null || k.StringId != NorthId || !On || PlayerRules(k)) return false;
            return Vetoed(st, NowHour) && !NorthBesieging(st);
        }

        private static bool NorthBesieging(Settlement st)
        {
            try
            {
                var ev = st.SiegeEvent;
                var lp = ev != null && ev.BesiegerCamp != null ? ev.BesiegerCamp.LeaderParty : null;
                return lp != null && lp.MapFaction != null && lp.MapFaction.StringId == NorthId;
            }
            catch { return false; }
        }

        private static void SayVeto(string kind, Settlement st, string who)
        {
            int day = NowHour / 24;
            if (day != _vSayDay) { _vSayDay = day; _vSaid.Clear(); }
            if (!_vSaid.Add(kind + "|" + st.StringId)) return;
            int until; var fort = st.IsVillage && st.Village != null ? st.Village.Bound : st;
            _veto.TryGetValue(fort != null ? fort.StringId : st.StringId, out until);
            Log.Info(Tag + "weto | dzien " + day + " | " + kind + " " + Nm(st) + " (" + st.StringId + ", " + Nm(st.MapFaction) + ") - " + who + " | zwrocona przez 326b, weto do dnia " + (until / 24)
                     + " | dzis zdjete: oblezenia " + _vSiege + ", najazdy " + _vRaid + ", BK " + _vBk + ".");
        }

        private static void EnsureHooks()
        {
            if (_hooksTried || _harmony == null) return;
            _hooksTried = true;
            try
            {
                var m = AccessTools.Method(typeof(CampaignEventDispatcher), "AiHourlyTick", new[] { typeof(MobileParty), typeof(PartyThinkParams) });
                if (m == null) _wThink = "BRAK (CampaignEventDispatcher.AiHourlyTick)";
                else { _harmony.Patch(m, postfix: new HarmonyMethod(typeof(NorthConquest), nameof(VetoPostfix)) { priority = 45 }); _wThink = "TAK"; }
            }
            catch (Exception e) { Stumble("EnsureHooks.think", e); _wThink = "BLAD (" + e.GetType().Name + ")"; }
            try
            {
                var t = AccessTools.TypeByName("BannerKings.Behaviours.BKArmyBehavior");
                if (t == null) _wBk = "BRAK (brak BannerKings BKArmyBehavior)";
                else
                {
                    var m = AccessTools.Method(t, "FindArmyObjective", new[] { typeof(MobileParty), typeof(Kingdom) });
                    if (m == null) _wBk = "BRAK (FindArmyObjective)";
                    else if (m.ReturnType != typeof(ValueTuple<Settlement, Army.ArmyTypes>?)) _wBk = "BRAK (sygnatura FindArmyObjective: " + m.ReturnType.Name + ")";
                    else { _harmony.Patch(m, postfix: new HarmonyMethod(typeof(NorthConquest), nameof(BkObjectivePostfix))); _wBk = "TAK"; }
                }
            }
            catch (Exception e) { Stumble("EnsureHooks.bk", e); _wBk = "BLAD (" + e.GetType().Name + ")"; }
        }

        /// <summary>Postfiks (priorytet 45) CampaignEventDispatcher.AiHourlyTick: oblezenie / najazd Polnocy na zwrocona twierdze i jej wsie = -1. Nie rzuca.</summary>
        public static void VetoPostfix(MobileParty party, PartyThinkParams partyThinkParams)
        {
            if (_veto.Count == 0 || party == null || partyThinkParams == null) return;
            try
            {
                if (!party.IsLordParty || party.IsMainParty) return;
                var mf = party.MapFaction;
                if (mf == null || mf.StringId != NorthId || !On) return;
                if (PlayerRules(mf as Kingdom)) return;
                var scores = partyThinkParams.AIBehaviorScores;
                if (scores == null || scores.Count == 0) return;
                int hour = NowHour;
                for (int i = 0; i < scores.Count; i++)
                {
                    var e = scores[i];
                    if (e.Item2 <= -1f) continue;
                    bool siege = e.Item1.AiBehavior == AiBehavior.BesiegeSettlement, raid = e.Item1.AiBehavior == AiBehavior.RaidSettlement;
                    if (!siege && !raid) continue;
                    var st = e.Item1.Party as Settlement;
                    if (st == null || !Vetoed(st, hour)) continue;
                    if (siege && NorthBesieging(st)) continue;   // trwajace oblezenie Polnocy - dolaczenie bez weta
                    var d = e.Item1;
                    partyThinkParams.SetBehaviorScore(in d, -1f);
                    if (siege) _vSiege++; else _vRaid++;
                    SayVeto(siege ? "oblezenie" : "najazd", st, "pierwszy " + (party.Name != null ? party.Name.ToString() : "-") + (e.Item1.WillGatherArmy ? ", zbiorka armii" : ""));
                }
            }
            catch (Exception e) { Stumble("VetoPostfix", e); }
        }

        /// <summary>Postfiks BKArmyBehavior.FindArmyObjective(MobileParty, Kingdom): cel Polnocy z wetem 326b -> null (BK nie zaklada armii). Nie rzuca.</summary>
        public static void BkObjectivePostfix(MobileParty __0, Kingdom __1, ref ValueTuple<Settlement, Army.ArmyTypes>? __result)
        {
            if (_veto.Count == 0 || !__result.HasValue) return;
            try
            {
                if (__1 == null || __1.StringId != NorthId || !On || PlayerRules(__1)) return;
                var v = __result.Value;
                if (v.Item1 == null || !Vetoed(v.Item1, NowHour)) return;
                if (NorthBesieging(v.Item1)) return;
                __result = null;
                _vBk++;
                SayVeto("BK cel armii (" + v.Item2 + ")", v.Item1, "pierwszy " + (__0 != null && __0.Name != null ? __0.Name.ToString() : "-"));
            }
            catch (Exception e) { Stumble("BkObjectivePostfix", e); }
        }
    }

    /// <summary>326b: spis po wczytaniu i kolejka zwrotow (zapis arm_k326b), zdobycia Polnocy, proba co godzine.</summary>
    internal sealed class NorthConquestBehavior : CampaignBehaviorBase
    {
        public NorthConquestBehavior() { NorthConquest.Reset(); }

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, _ => NorthConquest.SessionStart());
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, NorthConquest.Hourly);
            CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, NorthConquest.OnOwnerChanged);
        }

        public override void SyncData(IDataStore dataStore)
        {
            try
            {
                string v = dataStore.IsSaving ? NorthConquest.Export() : null;
                SaveText.Sync(dataStore, "arm_k326b", ref v);
                if (dataStore.IsLoading) NorthConquest.Import(v);
            }
            catch (Exception e) { Log.Error("SyncData.arm_k326b", e); }
        }
    }
}
