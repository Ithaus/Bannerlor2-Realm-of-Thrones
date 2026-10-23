using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
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
    /// m109-przesluch: Where (tekst miejsca z Announce, ten sam co do znaku) i Mark (kryjowka ujawniona przez jenca
    /// w obozie - CampInterrogation: flaga z terminem jak po zwiadzie, bez drugiego meldunku przegladu godzinnego).
    /// m110-flaga (Jeff 23.10 "popraw ten znacznik"): flaga jest w zapisie gry (VisualTrackerManager), a termin jej zdjecia (_flagged) zyl tylko w pamieci -
    /// po wczytaniu flaga stala do odznaczenia recznie. Teraz termin idzie do zapisu (SaveText arm_hideout_flags: "id,godzina;"), odtwarzany przy siewie
    /// po wczytaniu (godzina po terminie - flaga schodzi w najblizszej godzinie). Stary zapis bez klucza: kazda sledzona kryjowka dostaje HideoutFlagDays
    /// od wczytania (nie da sie odroznic naszej flagi od recznej - kryjowek nikt inny nie oznacza na stale). Klucz HideoutFlagKeepsDeadline.
    /// </summary>
    internal sealed class HideoutSpotter : CampaignBehaviorBase
    {
        private readonly HashSet<Settlement> _seen = new HashSet<Settlement>();
        private readonly Dictionary<Settlement, CampaignTime> _flagged = new Dictionary<Settlement, CampaignTime>();
        private bool _seeded;
        private static HideoutSpotter _inst;   // m109-przesluch: instancja kampanii dla Mark (wzor HideoutSight)
        private string _loadedFlags;           // m110-flaga: terminy flag z zapisu (id,godzina;) - odtwarzane przy siewie; "" = klucz byl, pusty
        private bool _loadedOld;               // m110-flaga: wczytany zapis bez klucza arm_hideout_flags (sprzed m110)

        public HideoutSpotter() { _inst = this; }

        public override void RegisterEvents()
        {
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, DayProfile.Hr("HideoutSpotter.OnHourly", OnHourly));   // E8/0: opakowanie pomiaru (tylko log)
            CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, _ => { _seeded = false; });
        }

        public override void SyncData(IDataStore dataStore)
        {
            var s = Settings.Current;
            if (s == null || !s.HideoutFlagKeepsDeadline) return;
            try
            {
                string txt = null;
                if (dataStore.IsSaving)
                {
                    var sb = new StringBuilder(_loadedFlags ?? "");   // zapis przed siewem (pierwsza godzina po wczytaniu) nie gubi terminow z wczytania
                    foreach (var kv in _flagged)
                        if (kv.Key != null) sb.Append(kv.Key.StringId).Append(',').Append(kv.Value.ToHours.ToString("R", CultureInfo.InvariantCulture)).Append(';');
                    txt = sb.ToString();
                }
                SaveText.Sync(dataStore, "arm_hideout_flags", ref txt);
                if (dataStore.IsLoading) { _loadedOld = txt == null; _loadedFlags = txt; }
            }
            catch (Exception e) { Log.Error("HideoutSpotter.SyncData", e); }
        }

        /// <summary>m110-flaga: terminy flag z zapisu do _flagged (raz, przy siewie po wczytaniu). Stary zapis - kazda sledzona kryjowka HideoutFlagDays od teraz.</summary>
        private void RestoreFlags(VisualTrackerManager vt, Settings s)
        {
            string txt = _loadedFlags; bool old = _loadedOld;
            _loadedFlags = null; _loadedOld = false;
            if (vt == null || !s.HideoutFlagKeepsDeadline) return;
            int back = 0, due = 0, gone = 0, oldN = 0;
            double now = CampaignTime.Now.ToHours;
            if (!string.IsNullOrEmpty(txt))
                foreach (var part in txt.Split(';'))
                {
                    var p = part.Split(',');
                    double h;
                    if (p.Length != 2 || !double.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out h)) continue;
                    var st = Settlement.Find(p[0]);
                    if (st == null || !st.IsHideout || !vt.CheckTracked(st)) { gone++; continue; }   // flagi juz nie ma (zdjeta recznie) - nic do pilnowania
                    if (_flagged.ContainsKey(st)) continue;
                    _flagged[st] = CampaignTime.HoursFromNow((float)(h - now));   // po terminie - zejdzie w najblizszej godzinie
                    back++; if (h <= now) due++;
                }
            if (old && s.HideoutFlagDays > 0)
                foreach (var st in Settlement.All)
                    if (st != null && st.IsHideout && !_flagged.ContainsKey(st) && vt.CheckTracked(st))
                    {
                        _flagged[st] = CampaignTime.DaysFromNow(s.HideoutFlagDays);
                        oldN++;
                    }
            Log.Info("HideoutSpotter (m110): terminy flag z zapisu - odtworzone " + back + " (po terminie " + due + "), bez flagi na mapie " + gone
                     + (old ? " | zapis sprzed m110: sledzonych kryjowek bez terminu " + oldN + " -> " + s.HideoutFlagDays + " dob od teraz" : "") + ".");
        }

        private void OnHourly()
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.HideoutFlagEnabled) return;
                if (!_seeded)
                {
                    _seeded = true;
                    try { RestoreFlags(Campaign.Current != null ? Campaign.Current.VisualTrackerManager : null, s); } catch (Exception e) { Log.Error("HideoutSpotter.RestoreFlags", e); }   // m110-flaga
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
                    if (st == null || !st.IsHideout || st.Hideout == null) continue;
                    if (!st.Hideout.IsSpotted) { _seen.Remove(st); continue; }   // m100-kryjowki (recenzja 21.10): opustoszala (gra zeruje IsSpotted) - ponowne wypatrzenie znow z meldunkiem i flaga
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
                string where = Where(hideout);
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

        /// <summary>m109-przesluch: miejsce kryjowki dla gracza - "~45 km to the north-east, near X (6 km)" (km i strona swiata od partii gracza, najblizsza
        /// osada gry). Kod wyjety z Announce bez zmian (ten sam tekst meldunku).</summary>
        internal static string Where(Settlement hideout)
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
            return where;
        }

        /// <summary>m109-przesluch: kryjowka ujawniona przez jenca (CampInterrogation ustawil IsSpotted i IsVisible). Wpis do _seen (przeglad godzinny nie melduje
        /// drugi raz); przy HideoutFlagEnabled i HideoutFlagDays > 0 flaga na mapie z terminem zdjecia, jesli obiektu nikt jeszcze nie sledzi (flaga gracza,
        /// Wayfinder - nie przejmujemy). Dziala przed i po pierwszym przegladzie (siew tylko dopisuje do _seen, _flagged zostaje). Zwraca, czy flaga stoi.</summary>
        internal static bool Mark(Settlement hideout)
        {
            var me = _inst;
            if (me == null || hideout == null) return false;
            me._seen.Add(hideout);
            var s = Settings.Current;
            var vt = Campaign.Current != null ? Campaign.Current.VisualTrackerManager : null;
            if (s == null || !s.HideoutFlagEnabled || s.HideoutFlagDays <= 0 || vt == null) return false;
            if (vt.CheckTracked(hideout)) return true;   // flaga juz stoi (nasza, gracza albo Wayfindera) - bez przejmowania
            vt.RegisterObject(hideout);
            me._flagged[hideout] = CampaignTime.DaysFromNow(s.HideoutFlagDays);
            return true;
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
