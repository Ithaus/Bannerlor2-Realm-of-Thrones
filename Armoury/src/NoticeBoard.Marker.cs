using System;
using System.Collections.Generic;
using System.Reflection;
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
    /// Teraz: znacznik mapy gry (MapMarkerManager.CreateMapMarker - MapTrackerProvider pokazuje kazdy MapMarker) z nazwa bandy i herbem jej klanu, przesuwany co
    /// godzine gry na pozycje bandy (setter Position przez odbicie). Zdejmowany przed kazdym zapisem i stawiany po nim oraz po wczytaniu (BandMarkersSync -
    /// stan znacznikow = zadania _bq w toku z czynna banda), wiec zapis nie ma naszych znacznikow (cofniecie DLL bez sladu - wzor m110-okolica). Klucz
    /// BountyBandMarker (NIE = jak 105.1: tylko tracker gry, niewidoczny). Linie "Listy goncze - zadania (m110): znacznik ...".
    /// Jeff 23.10 20:15 (105.2): "dziala, ale nie moze dzialac jak GPS - powinien ogolnie zaznaczyc okolice, a gracz juz sam sprawdzi". Znacznik to "<banda> - last
    /// seen": miejsce wiesci 2-6 jedn. od bandy w losowym kierunku (jeden-dwa zasiegi wzroku - okolica do przeszukania zwiadem), nowe wiesci raz na dobe gry albo
    /// od razu, gdy banda odejdzie ponad 15 jedn. od znacznika. Po wczytaniu - swieze wiesci (miejsce nie idzie do zapisu).
    /// </summary>
    internal static partial class NoticeBoard
    {
        private const string PM = "Listy goncze - zadania (m110): ";
        private const string BandMarkerTag = "arm_bounty_band";
        private const int BandMarkerDetail = 20;
        private static readonly Dictionary<BountyQuest, MapMarker> _bmk = new Dictionary<BountyQuest, MapMarker>();
        private static readonly Dictionary<BountyQuest, double> _bmkAt = new Dictionary<BountyQuest, double>();   // godzina kampanii ostatnich wiesci
        private const float SeenMin = 2f, SeenMax = 6f;   // rozrzut miejsca wiesci od bandy (jedn. mapy; zasieg wzroku gracza ok. 3-4)
        private const float SeenFar = 15f;                // banda dalej od znacznika - wiesci od razu
        private const double SeenEveryHours = 24.0;       // nowe wiesci raz na dobe gry
        private static MethodInfo _bmkPosSet;
        private static bool _bmkPosTried;
        private static int _bmkLines, _bmkPut, _bmkTake, _bmkMove, _bmkStumbles;

        internal static bool BandMarkerOn { get { var s = Settings.Current; return s != null && s.BountyBandMarker && s.WatchBountyQuests; } }

        private static void BmStumble(string where, Exception e)
        {
            _bmkStumbles++;
            if (_bmkStumbles <= 3 || _bmkStumbles % 50 == 0) { try { Log.Error("NoticeBoard.BandMarker." + where + " (" + _bmkStumbles + ")", e); } catch { } }
        }

        /// <summary>ResetWatch (nowa kampania i wczytanie): tylko pamiec - znaczniki starej kampanii zniknely razem z nia.</summary>
        private static void BandMarkersReset() { _bmk.Clear(); _bmkAt.Clear(); }

        /// <summary>Miejsce wiesci: SeenMin-SeenMax jedn. od bandy w losowym kierunku, w granicach mapy osad.</summary>
        private static Vec3 SeenSpot(MobileParty mp)
        {
            Vec2 at = mp.Position.ToVec2();
            float a = MBRandom.RandomFloat * 6.2831853f, d = SeenMin + MBRandom.RandomFloat * (SeenMax - SeenMin);
            float x = at.x + (float)Math.Cos(a) * d, y = at.y + (float)Math.Sin(a) * d;
            var c = Campaign.Current;
            if (c != null && c.MinSettlementX <= c.MaxSettlementX && c.MinSettlementY <= c.MaxSettlementY)
            {
                x = Math.Max(c.MinSettlementX, Math.Min(c.MaxSettlementX, x));
                y = Math.Max(c.MinSettlementY, Math.Min(c.MaxSettlementY, y));
            }
            return new CampaignVec2(new Vec2(x, y), true).AsVec3();   // wysokosc z mapy (brak trafienia - 0, bez wyjatku)
        }

        /// <summary>Czy pora na nowe wiesci: doba od ostatnich albo banda odeszla od znacznika ponad SeenFar.</summary>
        private static bool SeenDue(BountyQuest q, MapMarker m, MobileParty mp)
        {
            double at;
            if (!_bmkAt.TryGetValue(q, out at)) return true;
            if (CampaignTime.Now.ToHours - at >= SeenEveryHours) return true;
            return m.Position.AsVec2.DistanceSquared(mp.Position.ToVec2()) > SeenFar * SeenFar;
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

        /// <summary>Stan znacznikow = zadania w toku z czynna banda: brakujace stawia, zbedne zdejmuje, reszte przesuwa na pozycje bandy. why != null - linia.</summary>
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
                        MapMarker m;
                        if (_bmk.TryGetValue(q, out m) && m != null)
                        {
                            if (SeenDue(q, m, mp) && MoveBandMarker(m, SeenSpot(mp))) { _bmkAt[q] = CampaignTime.Now.ToHours; move++; }   // nowe wiesci - nie GPS
                            continue;
                        }
                        var b = BandBanner(mp);
                        if (b == null) continue;
                        var n = q.Tag as Notice;
                        m = mm.CreateMapMarker(b, new TextObject("{=!}" + BandTitle(n) + " - last seen"), SeenSpot(mp), true, BandMarkerTag);   // gra od razu RegisterObject - przypiety
                        if (m == null) continue;
                        _bmk[q] = m; _bmkAt[q] = CampaignTime.Now.ToHours; put++;
                        if (_bmkLines < BandMarkerDetail)
                        {
                            _bmkLines++;
                            Log.Info(PM + "znacznik bandy postawiony - " + q.StringId + " (" + mp.StringId + ", " + BandTitle(n) + ", bohater "
                                     + (mp.LeaderHero != null ? "TAK" : "NIE") + ", widoczna " + YN(mp.IsVisible) + ")" + (why != null ? " | " + why : "") + ".");
                        }
                    }
            }
            catch (Exception e) { BmStumble("Sync", e); }
            _bmkPut += put; _bmkTake += take; _bmkMove += move;
            if (why != null && (put > 0 || take > 0))
                Log.Info(PM + "znaczniki band - " + why + ": postawione " + put + ", zdjete " + take + ", stoi " + _bmk.Count + " | sesja: postawione "
                         + _bmkPut + ", zdjete " + _bmkTake + ", przesuniecia " + _bmkMove + ", potkniecia " + _bmkStumbles + ".");
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

        /// <summary>Zdejmuje znacznik zadania (MapMarkerManager i VisualTrackerManager - forceRemove czysci licznik). true - byl.</summary>
        private static bool TakeBandMarker(BountyQuest q)
        {
            MapMarker m;
            if (q == null || !_bmk.TryGetValue(q, out m)) return false;
            _bmk.Remove(q); _bmkAt.Remove(q);
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
        /// BandMarkersSync stawia je z powrotem.</summary>
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
    }
}
