using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// m110-znacznik (Jeff 23.10 20:02, zrzut: "zadanie zaczete, banda 'marked on your map', ale nic nie widze"): ZNACZNIK BANDY LISTU GONCZEGO NA MAPIE.
    /// Przyczyna (dekompilacja 1.4.8, SandBox MapTrackerProvider.CanAddMobileParty): gra rysuje znacznik cudzej partii tylko dla partii bez bohatera
    /// (LeaderHero == null) z IsCurrentlyUsedByAQuest i sledzonej w VisualTrackerManager - bandy (IsBandit) poza tym nigdy. BountyQuest rejestrowal bande
    /// w VisualTrackerManager (AddTrackedObject), ale nie ustawial IsCurrentlyUsedByAQuest, a bandy z hersztem-bohaterem (BK) i tak by odpadly.
    /// Jeff 20:15 / 20:20 (105.2 szedl za banda): "nie moze dzialac jak GPS", "nie znacznik, tylko tam, gdzie ostatnio byli widziani, i niech gracz szuka".
    /// Teraz: znacznik mapy gry (MapMarkerManager.CreateMapMarker - MapTrackerProvider pokazuje kazdy MapMarker) "<banda> - last seen" z herbem jej klanu,
    /// w miejscu, gdzie banda byla przy przyjeciu listu; przesuwa sie tylko wtedy, gdy gracz sam ja zobaczy (MobileParty.IsVisible przy takcie godzinnym) -
    /// wtedy na miejsce, gdzie ja widzial. Miejsce w zapisie (SaveText arm_bounty_seen: "klucz listu,x,y;") - po wczytaniu znacznik nie przeskakuje do bandy.
    /// Sam znacznik nie idzie do zapisu: zdejmowany przed kazdym zapisem i stawiany po nim oraz po wczytaniu (BandMarkersSync - stan znacznikow = zadania _bq
    /// w toku z czynna banda), wiec cofniecie DLL nie zostawia sladu na mapie (wzor m110-okolica). Klucz BountyBandMarker (NIE = jak 105.1: brak znacznika).
    /// Linie "Listy goncze - zadania (m110): znacznik ...".
    /// </summary>
    internal static partial class NoticeBoard
    {
        private const string PM = "Listy goncze - zadania (m110): ";
        private const string BandMarkerTag = "arm_bounty_band";
        private const int BandMarkerDetail = 20;
        private static readonly Dictionary<BountyQuest, MapMarker> _bmk = new Dictionary<BountyQuest, MapMarker>();
        private static readonly Dictionary<string, Vec2> _bmkSpot = new Dictionary<string, Vec2>();   // klucz listu (BaseId) -> miejsce "ostatnio widziani"
        private static MethodInfo _bmkPosSet;
        private static bool _bmkPosTried;
        private static int _bmkLines, _bmkPut, _bmkTake, _bmkMove, _bmkStumbles, _bmkImported = -1;

        internal static bool BandMarkerOn { get { var s = Settings.Current; return s != null && s.BountyBandMarker && s.WatchBountyQuests; } }

        private static void BmStumble(string where, Exception e)
        {
            _bmkStumbles++;
            if (_bmkStumbles <= 3 || _bmkStumbles % 50 == 0) { try { Log.Error("NoticeBoard.BandMarker." + where + " (" + _bmkStumbles + ")", e); } catch { } }
        }

        /// <summary>ResetWatch (nowa kampania i wczytanie - przed SyncData): tylko pamiec - znaczniki starej kampanii zniknely razem z nia.</summary>
        private static void BandMarkersReset() { _bmk.Clear(); _bmkSpot.Clear(); _bmkImported = -1; }

        private static string SpotKey(BountyQuest q)
        {
            var n = q != null ? q.Tag as Notice : null;
            return n != null ? BaseId(n) : null;
        }

        /// <summary>OnBountyFinalized: zadanie zamkniete - miejsce zapomniane (zdjecie przed zapisem miejsca nie rusza).</summary>
        private static void ForgetBandSpot(BountyQuest q)
        {
            string k = SpotKey(q);
            if (k != null) _bmkSpot.Remove(k);
        }

        private static bool BandLive(BountyQuest q, out MobileParty mp)
        {
            mp = null;
            var n = q != null ? q.Tag as Notice : null;
            if (n == null || !q.IsOngoing || q.Detached || !_bq.Contains(q)) return false;
            mp = n.Mp;
            return mp != null && mp.IsActive;
        }

        private static Banner BandBanner(MobileParty mp)
        {
            Banner b = null;
            try { if (mp.ActualClan != null) b = mp.ActualClan.Banner; } catch { }
            if (b == null) { var cl = mp.MapFaction as Clan; if (cl != null) b = cl.Banner; }
            if (b == null) foreach (var k in Clan.BanditFactions) if (k != null && k.Banner != null) { b = k.Banner; break; }
            return b;
        }

        private static Vec3 SpotPos(Vec2 v) { return new CampaignVec2(v, true).AsVec3(); }   // wysokosc z mapy (brak trafienia - 0, bez wyjatku)

        /// <summary>Stan znacznikow = zadania w toku z czynna banda: brakujace stawia (miejsce z pamieci albo tu, gdzie banda jest teraz), zbedne zdejmuje;
        /// banda w zasiegu wzroku gracza - miejsce "ostatnio widziani" przechodzi tam, gdzie ja widzi. why != null - linia.</summary>
        internal static void BandMarkersSync(string why)
        {
            var c = Campaign.Current;
            var mm = c != null ? c.MapMarkerManager : null;
            if (mm == null) return;
            int put = 0, take = 0, move = 0;
            try
            {
                bool on = BandMarkerOn && !_bqLatch;
                foreach (var q in new List<BountyQuest>(_bmk.Keys))
                {
                    MobileParty mp;
                    if (on && BandLive(q, out mp)) continue;
                    if (TakeBandMarker(q)) take++;
                }
                if (on)
                    foreach (var q in _bq)
                    {
                        MobileParty mp;
                        if (!BandLive(q, out mp)) continue;
                        string key = SpotKey(q);
                        if (key == null) continue;
                        Vec2 spot;
                        bool known = _bmkSpot.TryGetValue(key, out spot);
                        if (!known || mp.IsVisible) { spot = mp.Position.ToVec2(); _bmkSpot[key] = spot; }   // przyjecie albo gracz sam ja widzi
                        MapMarker m;
                        if (_bmk.TryGetValue(q, out m) && m != null)
                        {
                            if (MoveBandMarker(m, SpotPos(spot))) move++;
                            m.IsVisibleOnMap = !mp.IsVisible;   // recenzja U3: bande widac na mapie - znacznik schowany (jak znacznik partii w grze)
                            continue;
                        }
                        var b = BandBanner(mp);
                        if (b == null) continue;
                        var n = q.Tag as Notice;
                        m = mm.CreateMapMarker(b, new TextObject("{=!}" + BandTitle(n) + " - last seen"), SpotPos(spot), !mp.IsVisible, BandMarkerTag);   // gra od razu RegisterObject - przypiety
                        if (m == null) continue;
                        _bmk[q] = m; put++;
                        if (_bmkLines < BandMarkerDetail)
                        {
                            _bmkLines++;
                            Log.Info(PM + "znacznik bandy postawiony - " + q.StringId + " (" + mp.StringId + ", " + BandTitle(n) + ", bohater " + (mp.LeaderHero != null ? "TAK" : "NIE")
                                     + ", miejsce " + (known ? "z pamieci" : "teraz") + " " + spot.x.ToString("0.0", Inv) + "," + spot.y.ToString("0.0", Inv) + ", banda od niego "
                                     + mp.Position.ToVec2().Distance(spot).ToString("0.0", Inv) + " jedn., widoczna " + YN(mp.IsVisible) + ")" + (why != null ? " | " + why : "") + ".");
                        }
                    }
            }
            catch (Exception e) { BmStumble("Sync", e); }
            _bmkPut += put; _bmkTake += take; _bmkMove += move;
            if (why != null && (put > 0 || take > 0))
                Log.Info(PM + "znaczniki band - " + why + ": postawione " + put + ", zdjete " + take + ", stoi " + _bmk.Count + ", miejsc w pamieci " + _bmkSpot.Count
                         + (_bmkImported >= 0 ? " (z zapisu " + _bmkImported + ")" : "") + " | sesja: postawione " + _bmkPut + ", zdjete " + _bmkTake + ", przesuniecia (gracz widzial bande) "
                         + _bmkMove + ", potkniecia " + _bmkStumbles + ".");
        }

        private static bool MoveBandMarker(MapMarker m, Vec3 p)
        {
            try
            {
                if (!_bmkPosTried) { _bmkPosTried = true; _bmkPosSet = AccessTools.PropertySetter(typeof(MapMarker), "Position"); }
                if (_bmkPosSet == null) return false;
                if (m.Position.AsVec2.DistanceSquared(p.AsVec2) < 0.0001f) return false;
                _bmkPosSet.Invoke(m, new object[] { p });
                return true;
            }
            catch (Exception e) { BmStumble("Move", e); return false; }
        }

        /// <summary>Zdejmuje znacznik zadania (MapMarkerManager i VisualTrackerManager - forceRemove czysci licznik). true - byl. Miejsce zostaje (ForgetBandSpot).</summary>
        private static bool TakeBandMarker(BountyQuest q)
        {
            MapMarker m;
            if (q == null || !_bmk.TryGetValue(q, out m)) return false;
            _bmk.Remove(q);
            try
            {
                var c = Campaign.Current;
                if (m == null || c == null) return true;
                if (c.MapMarkerManager != null) c.MapMarkerManager.RemoveMapMarker(m);
                if (c.VisualTrackerManager != null && c.VisualTrackerManager.CheckTracked(m)) c.VisualTrackerManager.RemoveTrackedObject(m, true);
            }
            catch (Exception e) { BmStumble("Take", e); }
            return true;
        }

        /// <summary>OnBeforeSave (przed BountyBeforeSave): wszystkie nasze znaczniki z mapy - takze zablakane po naszym znaku - zapis ich nie ma; BountySaveOver ->
        /// BandMarkersSync stawia je z powrotem w tym samym miejscu (z _bmkSpot).</summary>
        internal static void BandMarkersOff()
        {
            var c = Campaign.Current;
            var mm = c != null ? c.MapMarkerManager : null;
            if (mm == null) return;
            int n = 0;
            try
            {
                foreach (var q in new List<BountyQuest>(_bmk.Keys)) if (TakeBandMarker(q)) n++;
                foreach (var m in new List<MapMarker>(mm.GetMapMarkersByQuestId(BandMarkerTag)))
                {
                    mm.RemoveMapMarker(m);
                    if (c.VisualTrackerManager != null && c.VisualTrackerManager.CheckTracked(m)) c.VisualTrackerManager.RemoveTrackedObject(m, true);
                    n++;
                }
            }
            catch (Exception e) { BmStumble("Off", e); }
            _bmkTake += n;
        }

        // ------------------------------------------------------------ zapis miejsc (SaveText arm_bounty_seen; stara DLL klucza nie czyta)

        /// <summary>"klucz,x,y;" tylko dla listow z zadaniem w toku.</summary>
        internal static string ExportBandSpots()
        {
            var sb = new StringBuilder();
            try
            {
                var live = new HashSet<string>();
                foreach (var q in _bq) { string k = SpotKey(q); if (k != null) live.Add(k); }
                foreach (var kv in _bmkSpot)
                    if (live.Contains(kv.Key))
                        sb.Append(kv.Key).Append(',').Append(kv.Value.x.ToString("R", Inv)).Append(',').Append(kv.Value.y.ToString("R", Inv)).Append(';');
            }
            catch (Exception e) { BmStumble("Export", e); }
            return sb.ToString();
        }

        internal static void ImportBandSpots(string txt)
        {
            _bmkSpot.Clear(); _bmkImported = 0;
            if (string.IsNullOrEmpty(txt)) return;
            try
            {
                foreach (var part in txt.Split(';'))
                {
                    var p = part.Split(',');
                    float x, y;
                    if (p.Length != 3 || p[0].Length == 0 || !float.TryParse(p[1], NumberStyles.Float, Inv, out x) || !float.TryParse(p[2], NumberStyles.Float, Inv, out y)) continue;
                    _bmkSpot[p[0]] = new Vec2(x, y); _bmkImported++;
                }
            }
            catch (Exception e) { BmStumble("Import", e); }
        }
    }
}
