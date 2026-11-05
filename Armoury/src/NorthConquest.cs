using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
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
    /// jency swoi do zalogi, obsada zalogi ludzmi krolestwa nowego pana; po zmianie roszczenie SCA krolestwa odbiorcy na te osade zdjete. Polnoc zatrzymuje lup gry
    /// z oblezenia (nic nie tworzymy). Gracz: twierdza gracza, odbiorca = rod gracza, Polnoc albo krolestwo odbiorcy pod rzadami gracza - pominiete z linia.
    /// Rw320 (zwrot po Krwawych Godach) dotyczy twierdz rodow, ktore przeszly do korony - te nie sa juz w Polnocy, wiec 326b ich nie rusza (bez zderzenia).
    /// Linie "Zwrot zdobyczy Polnocy (326b)": spis po wczytaniu, kazde zdobycie, kazdy zwrot (linia TryRow z tym znacznikiem), podsumowanie dobowe.
    /// </summary>
    internal static class NorthConquest
    {
        internal const string Tag = "Zwrot zdobyczy Polnocy (326b): ";
        private const string NorthId = "battania", KingsLandingId = "town_EW4", RedKeepId = "ROT_castle61";
        internal const int MinDays = 1;
        private const int MaxErrors = 3;
        private static readonly HashSet<string> Keep = new HashSet<string>(StringComparer.Ordinal) { "ROT_town32", RedKeepId };
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        internal static bool On { get { var s = Settings.Current; return s != null && s.NorthConquestReturn; } }

        // ---- zapis arm_k326b
        private static string _init = "";                                                       // "" - spis czeka, "done" - zrobiony
        private static readonly Dictionary<string, int> _queue = new Dictionary<string, int>(StringComparer.Ordinal);   // osada -> doba dopisania
        private static int _total;                                                              // zwroconych od paczki

        // ---- sesja
        private static readonly Dictionary<string, int> _err = new Dictionary<string, int>(StringComparer.Ordinal);
        private static readonly HashSet<string> _errSites = new HashSet<string>(StringComparer.Ordinal);
        private static int _stumbles, _today, _todayDay = -1, _waitDay = -1, _sumDay = -1;
        private static string _waitText;
        private static readonly List<string> _todayDone = new List<string>();
        private static readonly List<string> _todaySkip = new List<string>();
        private static string _lastWaits = "";

        internal static void Reset()
        {
            _init = ""; _queue.Clear(); _total = 0;
            _err.Clear(); _errSites.Clear(); _stumbles = 0; _today = 0; _todayDay = -1; _waitDay = -1; _sumDay = -1; _waitText = null;
            _todayDone.Clear(); _todaySkip.Clear(); _lastWaits = "";
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
            sb.Append("nc1|").Append(_init).Append('|');
            bool first = true;
            foreach (var kv in _queue) { if (!first) sb.Append(';'); first = false; sb.Append(kv.Key).Append('=').Append(kv.Value.ToString(Inv)); }
            sb.Append('|').Append(_total.ToString(Inv));
            return sb.ToString();
        }

        internal static void Import(string v)
        {
            Reset();
            if (string.IsNullOrEmpty(v)) return;   // zapis sprzed paczki - spis czeka
            try
            {
                var p = v.Split('|');
                if (p.Length < 4 || p[0] != "nc1") { _init = "done"; Log.Info(Tag + "zapis w nieznanym formacie - spis uznany za zrobiony, kolejka pusta."); return; }
                _init = p[1] == "done" ? "done" : "";
                if (p[2].Length > 0)
                    foreach (var e in p[2].Split(';'))
                    {
                        int i = e.IndexOf('='); int d;
                        if (i > 0 && int.TryParse(e.Substring(i + 1), NumberStyles.Integer, Inv, out d)) _queue[e.Substring(0, i)] = d;
                    }
                int t; _total = int.TryParse(p[3], NumberStyles.Integer, Inv, out t) ? t : 0;
            }
            catch (Exception e) { _init = "done"; Stumble("Import", e); }
        }

        internal static void SessionStart()
        {
            try
            {
                Log.Info(Tag + "start sesji | klucz North Conquest Return " + (On ? "TAK" : "NIE") + " | spis po wczytaniu " + (_init == "done" ? "zrobiony" : "czeka (pierwsza godzina na mapie)")
                         + " | w kolejce " + _queue.Count + (_queue.Count > 0 ? " [" + string.Join(", ", _queue.Keys.ToArray()) + "]" : "") + " | zwroconych od paczki " + _total + ".");
            }
            catch (Exception e) { Stumble("SessionStart", e); }
        }

        // ------------------------------------------------------------ regula
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
                if (origId == null || origK == null || origK == NorthId) return false;
                return origId != oc.StringId;
            }
            catch (Exception e) { Stumble("Candidate", e); return false; }
        }

        /// <summary>Prawowity pan: rod pierwotny, inaczej rod panujacy jego krolestwa z poczatku; null - zostaje (why).</summary>
        private static Clan Recipient(Settlement st, string origId, string origK, Kingdom north, out string why)
        {
            why = null;
            var orig = KingInTheNorth.ClanByIdFor(origId);
            var pc = Clan.PlayerClan;
            if (orig != null && !orig.IsEliminated && orig != pc && orig.Kingdom != null && orig.Leader != null && orig.Leader.IsAlive && !orig.Leader.IsPrisoner)
            { why = "rod pierwotny" + (orig.Kingdom == north ? " (dzis w Polnocy - zwrot wewnatrz krolestwa)" : ""); return orig; }
            string whyOrig = orig == null ? "rodu pierwotnego " + origId + " brak" : orig.IsEliminated ? "rod pierwotny wyeliminowany" : orig == pc ? "rod pierwotny to gracz"
                           : orig.Kingdom == null ? "rod pierwotny bez krolestwa" : orig.Leader == null || !orig.Leader.IsAlive ? "rod pierwotny bez zywej glowy" : "glowa rodu pierwotnego w niewoli";
            if (orig == pc) { why = "rod pierwotny to gracz - decyzja gracza, zostaje"; return null; }
            var k = K(origK);
            if (k == null || k.IsEliminated) { why = whyOrig + ", krolestwa " + origK + " brak" + (origK == "riverlands" ? " (Dorzecze w Polnocy - unia 272)" : "") + " - zostaje"; return null; }
            if (k == north) { why = whyOrig + ", krolestwo pierwotne to Polnoc - zostaje"; return null; }
            var rc = k.RulingClan;
            if (rc == null || rc.IsEliminated || rc == pc || rc.Leader == null || !rc.Leader.IsAlive) { why = whyOrig + ", rod panujacy " + Nm(k) + " niedostepny - zostaje"; return null; }
            why = "rod panujacy " + Nm(k) + " - " + whyOrig;
            return rc;
        }

        // ------------------------------------------------------------ zdarzenia
        internal static void OnOwnerChanged(Settlement st, bool openToClaim, Hero newOwner, Hero oldOwner, Hero capturer, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
        {
            try
            {
                if (!On || detail != ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.BySiege || st == null) return;
                if (KingInTheNorth.FightsOnEnd != null) return;
                string origId, origK;
                if (!Candidate(st, out origId, out origK)) return;
                int day = (int)CampaignTime.Now.ToDays;
                if (!_queue.ContainsKey(st.StringId)) _queue[st.StringId] = day;
                Log.Info(Tag + "dzien " + day + " | " + Nm(st) + " [" + st.StringId + "] zdobyta przez " + (newOwner != null && newOwner.Clan != null ? newOwner.Clan.Name.ToString() : "-")
                         + " (Polnoc) od " + (oldOwner != null && oldOwner.Clan != null ? oldOwner.Clan.Name.ToString() : "-") + " | pierwotnie " + origId + " (" + origK + ")"
                         + " | do kolejki - zwrot najwczesniej za " + MinDays + " dobe, gdy przy osadzie spokoj (lup oblezenia zostaje u zdobywcow).");
            }
            catch (Exception e) { Stumble("OnOwnerChanged", e); }
        }

        /// <summary>HourlyTickEvent (NorthConquestBehavior). Nie rzuca.</summary>
        internal static void Hourly()
        {
            if (!On) return;
            try
            {
                var c = Campaign.Current;
                if (c == null || !c.GameStarted) return;
                int day = (int)CampaignTime.Now.ToDays;
                if (day != _todayDay) { Summary(); _todayDay = day; _today = 0; _todayDone.Clear(); _todaySkip.Clear(); }
                var north = K(NorthId);
                if (north == null || north.IsEliminated) { if (_queue.Count > 0) { Log.Info(Tag + "Polnocy brak - kolejka wyczyszczona (" + _queue.Count + ")."); _queue.Clear(); } _init = "done"; return; }
                if (KingInTheNorth.FightsOnEnd != null) { if (_queue.Count > 0 || _init != "done") { Log.Info(Tag + "kanon Polnocy skonczony (" + KingInTheNorth.FightsOnEnd + ") - regula nie dotyczy, kolejka wyczyszczona (" + _queue.Count + ")."); _queue.Clear(); _init = "done"; } return; }
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
                        if (!_queue.ContainsKey(st.StringId)) _queue[st.StringId] = day - MinDays;
                        list.Add(Nm(st) + " [" + st.StringId + "] u " + (st.OwnerClan != null ? st.OwnerClan.Name.ToString() : "-") + ", pierwotnie " + origId + " (" + origK + ")");
                    }
                    Log.Info(Tag + "SPIS po wczytaniu (raz na zapis) - dzien " + day + " | twierdz Polnocy spoza jej ziem z poczatku kampanii: " + list.Count
                             + (list.Count > 0 ? " [" + string.Join("; ", list.ToArray()) + "]" : "") + " | dane ROT: " + KingInTheNorth.OrigDataText() + " | wyjatki: Harrenhal (Boltonowie dla Robba), Red Keep (z King's Landing).");
                }
                if (_queue.Count == 0) return;

                var waits = new List<string>();
                var msg = new List<string>();
                foreach (var id in _queue.Keys.ToList())
                {
                    try
                    {
                        var st = Settlement.Find(id);
                        string origId, origK;
                        if (st == null || !Candidate(st, out origId, out origK))
                        { _queue.Remove(id); _todaySkip.Add(id + " (juz nie u Polnocy albo poza regula)"); continue; }
                        if (day - _queue[id] < MinDays) { waits.Add(Nm(st) + ": swiezo zdobyta"); continue; }
                        string rwhy;
                        var to = Recipient(st, origId, origK, north, out rwhy);
                        if (to == null) { _queue.Remove(id); _todaySkip.Add(Nm(st) + " (" + rwhy + ")"); Log.Info(Tag + "dzien " + day + " | " + Nm(st) + " [" + id + "] zostaje u " + Nm(st.OwnerClan) + ": " + rwhy + "."); continue; }
                        if (PlayerRules(to.Kingdom)) { _queue.Remove(id); _todaySkip.Add(Nm(st) + " (odbiorca w krolestwie gracza)"); Log.Info(Tag + "dzien " + day + " | " + Nm(st) + " [" + id + "] - odbiorca " + Nm(to) + " w krolestwie gracza - pominiete (decyzja gracza)."); continue; }
                        string wait;
                        string res = BookHoldings.TransferFor(Tag, id, id == KingsLandingId ? RedKeepId : null, to, day, out wait);
                        if (res == null) { waits.Add(Nm(st) + ": " + wait); continue; }
                        _queue.Remove(id); _err.Remove(id);
                        if (res.StartsWith("ok", StringComparison.Ordinal))
                        {
                            _total++; _today++;
                            string sca = BookHoldings.ScaDropClaim(to.Kingdom, id);
                            _todayDone.Add(Nm(st) + " -> " + Nm(to) + " (" + rwhy + ")");
                            msg.Add(Nm(st) + " to House " + Nm(to));
                            Log.Info(Tag + "dzien " + day + " | ZWROT " + Nm(st) + " [" + id + "] -> " + Nm(to) + " (" + to.StringId + ", " + Nm(to.Kingdom) + "; " + rwhy + ") | " + sca + ".");
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
                if (_today == 0 && _todaySkip.Count == 0 && _queue.Count == 0) return;
                Log.Info(Tag + "doba " + _todayDay + " | zwrocone " + _today + (_todayDone.Count > 0 ? " [" + string.Join("; ", _todayDone.ToArray()) + "]" : "")
                         + " | pominiete " + _todaySkip.Count + (_todaySkip.Count > 0 ? " [" + string.Join("; ", _todaySkip.ToArray()) + "]" : "")
                         + " | w kolejce " + _queue.Count + (_lastWaits.Length > 0 ? " (czeka: " + _lastWaits + ")" : "") + " | od paczki " + _total + " | potkniecia " + _stumbles + ".");
            }
            catch (Exception e) { Stumble("Summary", e); }
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
