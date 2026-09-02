using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// KRYJOWKA WSKAZANA PALCEM (Jeff 02.09, screen: "Hideout spotted i nic
    /// nie widze!"). Vanilla pisze "Hideout spotted", gdy kryjowka wejdzie
    /// w zasieg wzroku (zwiad, perki, nasz wzrok za sloncem), ale ikona
    /// potrafi byc poza ekranem albo ginac miedzy drzewami. Co godzine
    /// sprawdzamy, ktore kryjowki sa swiezo wypatrzone: kazda nowa dostaje
    /// FLAGE na mapie (VisualTrackerManager, jak cel z Wayfindera) na
    /// HideoutFlagDays dni i meldunek: ile km, w ktora strone swiata,
    /// przy jakiej osadzie. Po wczytaniu gry juz wypatrzone kryjowki
    /// wpisujemy do pamieci bez meldunku - zero spamu przy starcie.
    /// </summary>
    internal sealed class HideoutSpotter : CampaignBehaviorBase
    {
        private readonly HashSet<Settlement> _seen = new HashSet<Settlement>();
        private readonly Dictionary<Settlement, CampaignTime> _flagged = new Dictionary<Settlement, CampaignTime>();
        private bool _seeded;

        public override void RegisterEvents()
        {
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourly);
            CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, _ => { _seeded = false; });
        }

        public override void SyncData(IDataStore dataStore) { }

        private void OnHourly()
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.HideoutFlagEnabled) return;
                if (!_seeded)
                {
                    _seeded = true;
                    foreach (var st in Settlement.All)
                        if (st != null && st.IsHideout && st.Hideout != null && st.Hideout.IsSpotted) _seen.Add(st);
                    Log.Info("HideoutSpotter: na starcie wypatrzonych kryjowek " + _seen.Count + " (bez meldunku).");
                    return;
                }
                var vt = Campaign.Current != null ? Campaign.Current.VisualTrackerManager : null;

                // zdejmij flagi po terminie
                if (_flagged.Count > 0)
                {
                    var due = new List<Settlement>();
                    foreach (var kv in _flagged) if (CampaignTime.Now >= kv.Value) due.Add(kv.Key);
                    foreach (var st in due)
                    {
                        _flagged.Remove(st);
                        try { if (vt != null && vt.CheckTracked(st)) vt.RemoveTrackedObject(st); } catch { }
                    }
                }

                foreach (var st in Settlement.All)
                {
                    if (st == null || !st.IsHideout || st.Hideout == null || !st.Hideout.IsSpotted) continue;
                    if (_seen.Contains(st)) continue;
                    _seen.Add(st);
                    Announce(st, vt, s);
                }
            }
            catch (Exception e) { Log.Error("HideoutSpotter", e); }
        }

        private void Announce(Settlement hideout, VisualTrackerManager vt, Settings s)
        {
            try
            {
                var mp = MobileParty.MainParty;
                string where = "";
                if (mp != null)
                {
                    Vec2 me = mp.Position.ToVec2();
                    Vec2 it = hideout.GetPosition2D;
                    float dist = me.Distance(it);
                    int km = (int)Math.Round(dist * Wayfinder.KmPerUnit);
                    where = "~" + km + " km to the " + Compass(it - me);
                }
                Settlement near = null; float best = float.MaxValue;
                foreach (var st in Settlement.All)
                {
                    if (st == null || !(st.IsTown || st.IsCastle || st.IsVillage)) continue;
                    float d = st.GetPosition2D.Distance(hideout.GetPosition2D);
                    if (d < best) { best = d; near = st; }
                }
                if (near != null)
                    where += (where.Length > 0 ? ", " : "") + "near " + near.Name + " (" + (int)Math.Round(best * Wayfinder.KmPerUnit) + " km)";
                string who = hideout.Hideout.IsInfested && hideout.Hideout.MapFaction != null ? hideout.Hideout.MapFaction.Name.ToString() : "bandits";
                Log.Player("Hideout spotted: " + who + " lair " + where + ". Flagged on the map for " + s.HideoutFlagDays + " days.", true);
                Log.Info("HideoutSpotter: " + hideout.StringId + " (" + who + ") " + where + ".");
                if (vt != null && s.HideoutFlagDays > 0 && !vt.CheckTracked(hideout))
                {
                    vt.RegisterObject(hideout);
                    _flagged[hideout] = CampaignTime.DaysFromNow(s.HideoutFlagDays);
                }
            }
            catch (Exception e) { Log.Error("HideoutSpotter.Announce", e); }
        }

        /// <summary>Osiem stron swiata; +Y to polnoc na mapie.</summary>
        private static string Compass(Vec2 d)
        {
            if (d.LengthSquared < 0.0001f) return "here";
            double a = Math.Atan2(d.y, d.x) * 180.0 / Math.PI;   // 0 = wschod, 90 = polnoc
            if (a < 0) a += 360.0;
            string[] names = { "east", "north-east", "north", "north-west", "west", "south-west", "south", "south-east" };
            int idx = (int)Math.Round(a / 45.0) % 8;
            return names[idx];
        }
    }
}
