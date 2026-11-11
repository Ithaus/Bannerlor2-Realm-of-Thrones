using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// 385d (Jeff 11.11 "tak, rob wszystko 2-4", pkt 2 "twoi zwykli zolnierze w niewoli"): REJESTR "MOI ZOLNIERZE W NIEWOLI" - przyblizenie.
    /// Gra nie pamieta, czyj byl szeregowy jeniec (lista jencow to typ + liczba). Rejestr trzyma dla kazdego trzymajacego (druzyna "p/id" albo loch
    /// osady "s/id") i typu wojska: Mine - ilu z tych jencow to ludzie gracza (ulamek), Seen - stan listy przy ostatnim uzgodnieniu.
    /// WEJSCIA:
    ///  (1) wlasna latka MapEvent.CaptureDefeatedPartyMembers (jedyna droga gry, ktora szeregowy pokonanej partii staje sie jencem; prefiks + finalizer, tylko
    ///      odczyt list, niezalezna od PeopleBook / OriginLive): wsrod pokonanych jest partia "moja" (druzyna gracza, druzyna lorda rodu gracza, zaloga osady
    ///      rodu gracza; bez milicji, karawan i taborow) -> przyrost typu u kazdego odbiorcy x (moi tego typu / wszyscy pokonani tego typu). Dokladne, gdy tego
    ///      typu mial tylko gracz; inaczej proporcja (gra losuje odbiorce czlowiek po czlowieku bez sladu).
    ///  (2) Note() - ranni spod murow przeniesieni przez SiegeWounded373 (373 / 388 / 387c) z partii "mojej" do lochu.
    /// UBYTKI (bez latek): Sync - stan listy spadl ponizej Seen -> Mine maleje w tej samej proporcji (strata rowno na moich i obcych: sprzedaz w warowni,
    ///  wcielenie przez AI, ucieczka ponad limit, dobowy ubytek lochu), Mine nigdy ponad stan listy; nasluch OnPrisonerSoldEvent (sprzedaz miedzy dobami -
    ///  proporcja liczona od stanu sprzed sprzedazy); trzymajacy zniknal -> wiersz znika. Sprzedani w warowni nie gina: BK SendOffPrisoners + PrisonerLaw
    ///  odsyla ich do ludnosci ("do domu") albo w niewole osady - nie ma juz kogo wykupic.
    /// Blad przyblizenia: obcy tego samego typu dopisani i zdjeci miedzy uzgodnieniami moga zawyzyc Mine - nigdy ponad stan listy (gracz nie wykupi ludzi,
    ///  ktorych fizycznie nie ma). Rejestr pisze zawsze (tani: bez moich partii wsrod pokonanych prefiks wychodzi po jednej petli); klucz
    ///  PrisonerTermsReleaseMySoldiers steruje tylko linia rozmowy 384. Zapis "arm_cap385d". Potkniecia liczone, nic sie nie gasi.
    /// </summary>
    internal static class PlayerCaptives385d
    {
        internal const string Tag = "Moi w niewoli (385d)";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private sealed class Rec { public double Mine; public int Seen; }
        internal sealed class Row { public PartyBase Holder; public CharacterObject Troop; public int N; }
        public sealed class Snap
        {
            public Dictionary<CharacterObject, int> Mine = new Dictionary<CharacterObject, int>(), All = new Dictionary<CharacterObject, int>();
            public List<PartyBase> Recv = new List<PartyBase>();
            public List<Dictionary<CharacterObject, int>> Before = new List<Dictionary<CharacterObject, int>>();
        }

        private static readonly Dictionary<string, Dictionary<string, Rec>> _h = new Dictionary<string, Dictionary<string, Rec>>(StringComparer.Ordinal);
        private static readonly Dictionary<string, PartyBase> _cache = new Dictionary<string, PartyBase>(StringComparer.Ordinal);
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static string _hook = "nie wpinana";
        private static int _stumbles, _dBattles, _dNoted;
        private static double _dTaken, _dLost, _dSold, _dGone, _dFreed;

        internal static void Reset()
        {
            _h.Clear(); _cache.Clear(); _err.Clear(); _stumbles = 0; _dBattles = 0; _dNoted = 0; _dTaken = 0; _dLost = 0; _dSold = 0; _dGone = 0; _dFreed = 0;
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_err.Add(where)) Log.Error("PlayerCaptives385d." + where, e); } catch { }
        }

        private static string F(double v) { return v.ToString("0.#", Inv); }

        // ------------------------------------------------------------ kto jest "moj", kto trzyma
        /// <summary>Partia gracza w sensie rejestru: druzyna gracza, druzyna lorda rodu gracza, zaloga osady rodu gracza. Nie: milicja, karawany, tabory.</summary>
        internal static bool MineParty(PartyBase pb)
        {
            try
            {
                var mp = pb != null ? pb.MobileParty : null;
                var pc = Clan.PlayerClan;
                if (mp == null || pc == null) return false;
                if (mp.IsMainParty) return true;
                if (mp.IsGarrison) return mp.HomeSettlement != null && mp.HomeSettlement.OwnerClan == pc;
                return mp.IsLordParty && mp.ActualClan == pc;
            }
            catch { return false; }
        }

        private static string Id(PartyBase pb)
        {
            if (pb == null) return null;
            if (pb.IsSettlement) return pb.Settlement != null && pb.Settlement.StringId != null ? "s/" + pb.Settlement.StringId : null;
            var mp = pb.MobileParty;
            return mp != null && mp.StringId != null ? "p/" + mp.StringId : null;
        }

        /// <summary>Trzymajacy z klucza rejestru; null - nie istnieje / nieaktywny.</summary>
        private static PartyBase Resolve(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length < 3) return null;
            PartyBase pb;
            if (_cache.TryGetValue(id, out pb) && pb != null)
            {
                if (pb.IsSettlement) return pb;
                var m = pb.MobileParty;
                if (m != null && m.IsActive && m.StringId != null && "p/" + m.StringId == id) return pb;
                _cache.Remove(id);
                return null;   // ta sama partia nie wraca pod tym samym id
            }
            string raw = id.Substring(2);
            if (id[0] == 's')
            {
                var st = Settlement.Find(raw);
                pb = st != null ? st.Party : null;
            }
            else
            {
                foreach (var mp in MobileParty.All)
                    if (mp != null && mp.IsActive && mp.StringId == raw) { pb = mp.Party; break; }
            }
            if (pb != null) _cache[id] = pb;
            return pb;
        }

        private static Rec Row0(PartyBase holder, CharacterObject troop, bool create)
        {
            string id = Id(holder);
            if (id == null || troop == null || troop.StringId == null) return null;
            Dictionary<string, Rec> d;
            if (!_h.TryGetValue(id, out d))
            {
                if (!create) return null;
                d = new Dictionary<string, Rec>(StringComparer.Ordinal); _h[id] = d; _cache[id] = holder;
            }
            Rec r;
            if (!d.TryGetValue(troop.StringId, out r))
            {
                if (!create) return null;
                r = new Rec(); d[troop.StringId] = r;
            }
            return r;
        }

        // ------------------------------------------------------------ uzgodnienie ze stanem listy
        /// <summary>Uzgodnienie jednego trzymajacego: ubytek listy zdejmuje moich proporcjonalnie, Mine nie ponad stan listy; puste wiersze znikaja.</summary>
        private static void Sync(string id)
        {
            Dictionary<string, Rec> d;
            if (id == null || !_h.TryGetValue(id, out d)) return;
            try
            {
                var pb = Resolve(id);
                var pr = pb != null ? pb.PrisonRoster : null;
                if (pr == null)
                {
                    double sum = 0; foreach (var r in d.Values) sum += r.Mine;
                    _dGone += sum; _h.Remove(id); _cache.Remove(id);
                    Log.Info(Tag + ": trzymajacy " + id + " juz nie istnieje - wiersze zdjete (" + F(sum) + " ludzi: odbici, rozpuszczeni albo w innych rekach).");
                    return;
                }
                List<string> dead = null;
                foreach (var kv in d)
                {
                    var r = kv.Value;
                    var troop = CharacterObject.Find(kv.Key);
                    int now = troop != null ? pr.GetTroopCount(troop) : 0;
                    double was = r.Mine;
                    if (now < r.Seen && r.Seen > 0) r.Mine *= (double)now / r.Seen;
                    r.Seen = now;
                    if (r.Mine > now) r.Mine = now;
                    if (was > r.Mine) _dLost += was - r.Mine;
                    if (r.Mine < 0.5) { if (dead == null) dead = new List<string>(); dead.Add(kv.Key); }
                }
                if (dead != null) foreach (var k in dead) d.Remove(k);
                if (d.Count == 0) { _h.Remove(id); _cache.Remove(id); }
            }
            catch (Exception e) { Stumble("Sync", e); }
        }

        private static void SyncAll()
        {
            if (_h.Count == 0) return;
            foreach (var id in new List<string>(_h.Keys)) Sync(id);
        }

        // ------------------------------------------------------------ wejscie (1): latka CaptureDefeatedPartyMembers
        /// <summary>SubModuleMain (menu glowne, raz na proces): prefiks + finalizer MapEvent.CaptureDefeatedPartyMembers (metoda zadeklarowana w MapEvent -
        /// DeclaredMethod). Bez priorytetu: tylko czyta listy przed / po, niczego nie zmienia; na tym samym celu PeopleBook CapturePre(First) / CaptureFin(Last).</summary>
        internal static void Apply(Harmony h)
        {
            try
            {
                var m = AccessTools.DeclaredMethod(typeof(MapEvent), "CaptureDefeatedPartyMembers");
                if (m == null) _hook = "BRAK MapEvent.CaptureDefeatedPartyMembers";
                else
                {
                    h.Patch(m, prefix: new HarmonyMethod(typeof(PlayerCaptives385d), nameof(CapPre)), finalizer: new HarmonyMethod(typeof(PlayerCaptives385d), nameof(CapFin)));
                    _hook = "wpieta";
                }
            }
            catch (Exception e) { _hook = "blad " + e.GetType().Name; Log.Error("PlayerCaptives385d.Apply", e); }
            try { Log.Info(Tag + ": latka rejestru - " + _hook + " (prefiks + finalizer MapEvent.CaptureDefeatedPartyMembers; tylko odczyt list jencow)."); } catch { }
        }

        private static PartyBase Receiver(MapEventParty mep)
        {
            var pb = mep != null ? mep.Party : null;
            if (pb == null || pb == PartyBase.MainParty) return null;   // gracz-zwyciezca: ekran lupu, nie rejestr
            var mp = pb.MobileParty;
            if (mp != null && (mp.IsMilitia || mp.IsGarrison)) return mp.HomeSettlement != null ? mp.HomeSettlement.Party : null;   // jak MapEventParty.RosterToReceiveLootPrisoners
            return pb;
        }

        public static void CapPre(MBReadOnlyList<MapEventParty> __0, MBReadOnlyList<MapEventParty> __1, out Snap __state)
        {
            __state = null;
            try
            {
                if (__0 == null || __1 == null || Campaign.Current == null || Clan.PlayerClan == null) return;
                bool any = false;
                foreach (var mep in __1) if (mep != null && MineParty(mep.Party)) { any = true; break; }
                if (!any) return;
                var s = new Snap();
                foreach (var mep in __1)
                {
                    var pb = mep != null ? mep.Party : null;
                    var mr = pb != null ? pb.MemberRoster : null;
                    if (mr == null) continue;
                    bool mine = MineParty(pb);
                    for (int i = 0; i < mr.Count; i++)
                    {
                        var c = mr.GetCharacterAtIndex(i);
                        int n = mr.GetElementNumber(i);
                        if (c == null || c.IsHero || n <= 0) continue;
                        int a; s.All.TryGetValue(c, out a); s.All[c] = a + n;
                        if (mine) { int m; s.Mine.TryGetValue(c, out m); s.Mine[c] = m + n; }
                    }
                }
                if (s.Mine.Count == 0) return;
                foreach (var mep in __0)
                {
                    var rc = Receiver(mep);
                    if (rc == null || rc.PrisonRoster == null || s.Recv.Contains(rc)) continue;
                    Sync(Id(rc));   // stare wiersze tego odbiorcy uzgodnione przed przyrostem
                    var b = new Dictionary<CharacterObject, int>();
                    foreach (var c in s.Mine.Keys) b[c] = rc.PrisonRoster.GetTroopCount(c);
                    s.Recv.Add(rc); s.Before.Add(b);
                }
                if (s.Recv.Count > 0) __state = s;
            }
            catch (Exception e) { __state = null; Stumble("CapPre", e); }
        }

        public static Exception CapFin(Exception __exception, Snap __state)
        {
            if (__state == null) return __exception;
            try
            {
                var s = __state;
                double took = 0;
                var sb = new StringBuilder();
                for (int j = 0; j < s.Recv.Count; j++)
                {
                    var rc = s.Recv[j];
                    var pr = rc.PrisonRoster;
                    if (pr == null) continue;
                    double here = 0;
                    foreach (var kv in s.Mine)
                    {
                        int before; s.Before[j].TryGetValue(kv.Key, out before);
                        int now = pr.GetTroopCount(kv.Key);
                        int gain = now - before;
                        int all; s.All.TryGetValue(kv.Key, out all);
                        if (gain <= 0 || all <= 0) continue;
                        double mine = gain * (double)kv.Value / all;
                        var r = Row0(rc, kv.Key, true);
                        if (r == null) continue;
                        r.Mine = Math.Min(now, r.Mine + mine); r.Seen = now;
                        here += mine;
                    }
                    if (here <= 0) continue;
                    took += here;
                    if (sb.Length < 400) sb.Append(sb.Length > 0 ? ", " : "").Append(rc.Name != null ? rc.Name.ToString() : Id(rc)).Append(' ').Append(F(here));
                }
                int lost = 0; foreach (var v in s.Mine.Values) lost += v;
                _dBattles++; _dTaken += took;
                Log.Info(Tag + ": bitwa przegrana przez partie gracza - szeregowych gracza przed rozliczeniem " + lost + ", wzietych do niewoli (przyblizenie) " + F(took)
                         + (sb.Length > 0 ? " [" + sb + "]" : "") + " | reszta polegla, rozproszona albo typ nie do pojmania.");
            }
            catch (Exception e) { Stumble("CapFin", e); }
            return __exception;
        }

        // ------------------------------------------------------------ wejscie (2): ranni spod murow (SiegeWounded373)
        /// <summary>n ludzi typu troop przeszlo z partii source do listy jencow holder (juz przeniesieni). Liczy tylko, gdy source jest partia gracza. Nie rzuca.</summary>
        internal static void Note(PartyBase holder, PartyBase source, CharacterObject troop, int n)
        {
            try
            {
                if (n <= 0 || holder == null || troop == null || troop.IsHero || holder.PrisonRoster == null || !MineParty(source)) return;
                int now = holder.PrisonRoster.GetTroopCount(troop);
                var r = Row0(holder, troop, false);
                if (r == null)
                {
                    r = Row0(holder, troop, true);
                    if (r == null) return;
                }
                else if (now - n < r.Seen && r.Seen > 0) { r.Mine *= Math.Max(0, now - n) / (double)r.Seen; }   // ubytek sprzed tego przeniesienia
                r.Mine = Math.Min(now, r.Mine + n); r.Seen = now;
                _dNoted += n; _dTaken += n;
            }
            catch (Exception e) { Stumble("Note", e); }
        }

        // ------------------------------------------------------------ ubytki
        /// <summary>Nasluch CampaignEvents.OnPrisonerSoldEvent (po zdjeciu z listy sprzedajacego): proporcja od stanu sprzed sprzedazy.</summary>
        internal static void OnSold(PartyBase seller, PartyBase buyer, TroopRoster sold)
        {
            if (_h.Count == 0 || seller == null || sold == null) return;
            try
            {
                string id = Id(seller);
                Dictionary<string, Rec> d;
                if (id == null || !_h.TryGetValue(id, out d)) return;
                var pr = seller.PrisonRoster;
                for (int i = 0; i < sold.Count; i++)
                {
                    var c = sold.GetCharacterAtIndex(i);
                    int k = sold.GetElementNumber(i);
                    Rec r;
                    if (c == null || c.IsHero || k <= 0 || c.StringId == null || !d.TryGetValue(c.StringId, out r)) continue;
                    int now = pr != null ? pr.GetTroopCount(c) : 0;
                    int pre = now + k;
                    double was = r.Mine;
                    if (was > pre) was = pre;
                    r.Mine = pre > 0 ? was * now / pre : 0;
                    r.Seen = now;
                    _dSold += was - r.Mine;
                }
                Sync(id);   // puste wiersze precz
            }
            catch (Exception e) { Stumble("OnSold", e); }
        }

        /// <summary>Doba: uzgodnienie wszystkich trzymajacych i linia stanu (gdy cos jest albo cos sie zdarzylo).</summary>
        internal static void Daily()
        {
            try
            {
                SyncAll();
                double sum = 0; int rows = 0;
                foreach (var d in _h.Values) foreach (var r in d.Values) { sum += r.Mine; rows++; }
                if (rows > 0 || _dBattles > 0 || _dNoted > 0 || _dSold + _dLost + _dGone + _dFreed > 0)
                {
                    var sb = new StringBuilder();
                    int shown = 0;
                    foreach (var kv in _h)
                    {
                        if (shown++ >= 8) { sb.Append(", ..."); break; }
                        double m = 0; foreach (var r in kv.Value.Values) m += r.Mine;
                        var pb = Resolve(kv.Key);
                        sb.Append(sb.Length > 0 ? ", " : "").Append(pb != null && pb.Name != null ? pb.Name.ToString() : kv.Key).Append(' ').Append(F(m));
                    }
                    Log.Info(Tag + ": doba " + (int)CampaignTime.Now.ToDays + " | moi w niewoli: " + F(sum) + " u " + _h.Count + " trzymajacych (" + rows + " wierszy)" + (sb.Length > 0 ? " [" + sb + "]" : "")
                             + " | dzis: bitwy przegrane " + _dBattles + ", wzieci " + F(_dTaken) + " (w tym ranni spod murow " + _dNoted + "), sprzedani w warowni (wrocili do domu / w niewole osady) " + F(_dSold)
                             + ", inny ubytek listy (wcieleni, zbiegli, dobowy ubytek lochu) " + F(_dLost) + ", trzymajacy zniknal " + F(_dGone) + ", uwolnieni umowa " + F(_dFreed)
                             + " | latka " + _hook + " | potkniecia " + _stumbles + ".");
                }
                _dBattles = 0; _dNoted = 0; _dTaken = 0; _dLost = 0; _dSold = 0; _dGone = 0; _dFreed = 0;
            }
            catch (Exception e) { Stumble("Daily", e); }
        }

        // ------------------------------------------------------------ odczyt dla rozmowy 384 i wykonania umowy
        /// <summary>Ludzie gracza u trzymajacych z zakresu (po uzgodnieniu): wiersze (trzymajacy, typ, ilu calych ludzi - nie ponad stan listy).</summary>
        internal static List<Row> Held(Func<PartyBase, bool> scope)
        {
            var list = new List<Row>();
            if (_h.Count == 0) return list;
            try
            {
                SyncAll();
                foreach (var kv in _h)
                {
                    var pb = Resolve(kv.Key);
                    if (pb == null || pb.PrisonRoster == null || (scope != null && !scope(pb))) continue;
                    foreach (var tr in kv.Value)
                    {
                        var troop = CharacterObject.Find(tr.Key);
                        if (troop == null) continue;
                        int n = Math.Min((int)Math.Round(tr.Value.Mine), pb.PrisonRoster.GetTroopCount(troop));
                        if (n > 0) list.Add(new Row { Holder = pb, Troop = troop, N = n });
                    }
                }
            }
            catch (Exception e) { Stumble("Held", e); }
            return list;
        }

        /// <summary>Po uwolnieniu k ludzi umowa (lista juz zmniejszona przez wolajacego).</summary>
        internal static void Freed(PartyBase holder, CharacterObject troop, int k)
        {
            try
            {
                var r = Row0(holder, troop, false);
                if (r == null || k <= 0) return;
                r.Mine = Math.Max(0, r.Mine - k);
                r.Seen = holder.PrisonRoster != null ? holder.PrisonRoster.GetTroopCount(troop) : 0;
                if (r.Mine > r.Seen) r.Mine = r.Seen;
                _dFreed += k;
                Sync(Id(holder));
            }
            catch (Exception e) { Stumble("Freed", e); }
        }

        // ------------------------------------------------------------ zapis ("arm_cap385d"): v1|trzymajacy,typ,moi,stan;...
        internal static string Export()
        {
            try
            {
                var sb = new StringBuilder("v1|");
                bool f = true;
                foreach (var kv in _h)
                    foreach (var tr in kv.Value)
                    {
                        if (kv.Key.IndexOfAny(Bad) >= 0 || tr.Key.IndexOfAny(Bad) >= 0 || tr.Value.Mine < 0.5) continue;
                        if (!f) sb.Append(';'); f = false;
                        sb.Append(kv.Key).Append(',').Append(tr.Key).Append(',').Append(tr.Value.Mine.ToString("R", Inv)).Append(',').Append(tr.Value.Seen.ToString(Inv));
                    }
                return sb.ToString();
            }
            catch (Exception e) { Stumble("Export", e); return ""; }
        }

        private static readonly char[] Bad = { '|', ';', ',' };

        internal static void Import(string data)
        {
            Reset();
            try
            {
                if (string.IsNullOrEmpty(data)) return;
                var f = data.Split('|');
                if (f.Length < 2 || f[0] != "v1") { Log.Info(Tag + ": zapis nieczytelny - od zera."); return; }
                int bad = 0, rows = 0; double sum = 0;
                foreach (var p in f[1].Split(';'))
                {
                    if (p.Length == 0) continue;
                    var x = p.Split(',');
                    double m; int seen;
                    if (x.Length != 4 || x[0].Length < 3 || x[1].Length == 0 || !double.TryParse(x[2], NumberStyles.Float, Inv, out m) || !int.TryParse(x[3], NumberStyles.Integer, Inv, out seen) || m <= 0 || seen < 0) { bad++; continue; }
                    Dictionary<string, Rec> d;
                    if (!_h.TryGetValue(x[0], out d)) { d = new Dictionary<string, Rec>(StringComparer.Ordinal); _h[x[0]] = d; }
                    d[x[1]] = new Rec { Mine = m, Seen = seen };
                    rows++; sum += m;
                }
                if (rows > 0 || bad > 0) Log.Info(Tag + ": wczytano z zapisu - " + rows + " wierszy u " + _h.Count + " trzymajacych, razem " + F(sum) + " ludzi" + (bad > 0 ? " (bledne " + bad + ")" : "") + ".");
            }
            catch (Exception e) { Stumble("Import", e); }
        }
    }
}
