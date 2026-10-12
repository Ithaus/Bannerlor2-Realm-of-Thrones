using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Armoury.Villages;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// E7/W1 (232, PLAN 7.10 a, R10/R14; projekt etapu 7 v2 rozdz. 3 "E7/W1") - WIESCI Z WIOSEK: "plonie Twoja wioska", "spalona przez", "splondrowana przez".
    /// Gra daje przy rabunku tylko wpis dziennika (CommentOnChangeVillageStateBehavior) - komunikat niczego nie dubluje. Tekst wedlug JEDNEGO stanu - ksiegi wiosek (R10):
    ///  - wejscie w rabunek (CampaignEvents.VillageStateChanged -> BeingRaided, RaidEventComponent.OnInitialize): VilLogBurningOwn (wies rodu gracza) / VilLogBurning
    ///    (krolestwo gracza); {VILLAGE} = wioska z ogniem (VillageBook.FireNames -> FxUid, ten sam wybor co widok). Jeden "plonie" na wies: druga proba tego samego
    ///    napastnika w tej samej albo nastepnej dobie (po walce z milicja gra zaklada drugi rabunek - RaidEventComponent.OnFinalize) pominieta;
    ///  - przejscie wioski na spalona w ksiedze (VillageBook.CheckUp przy uderzeniu Devastation.Strike - rabunek albo zerowanie armii): kolejka ksiegi, VilLogBurned /
    ///    VilLogBurnedPlayer - JEDYNE miejsce slowa "burned";
    ///  - Looted gry (VillageStateChanged w OnBeforeFinalize, po Devastation.EndPrefix - ostatnie uderzenie juz w ksiedze) przy sesji, ktora nic nie spalila:
    ///    VilLogPlundered / VilLogPlunderedPlayer z liczba osad spalonych z ksiegi; gdy ta sama sesja spalila wioske - nic drugi raz.
    /// Kto slyszy (klucz VillageRaidMessages): 0 nikt, 1 wsie rodu gracza (Village.Bound.OwnerClan - jak "your fief" w dymku) i rabunki gracza, 2 takze cale krolestwo gracza.
    /// Komunikat (MBInformationManager.AddQuickInformation + Log.Player) idzie w ticku godzinnym - nie z wnetrza Strike ani prefiksu Harmony; tekst budowany dla kazdego
    /// zdarzenia (licznik "by bylo" - dowod sciezki w autotescie bez lenna). Pamiec wojny (klucz VillageWarMemory) pisze ksiega (VillageBook.Remember, sekcja m:);
    /// tu tylko jej tekst dla dymka (MemoryText - nazwa na zywo, W2). Stan: kolejki i dedup tylko w pamieci (sesja); wyjatki liczone per miejsce, bez wylacznikow.
    /// </summary>
    internal static class VillageNews
    {
        private const int KBurning = 0, KPlundered = 1;
        private const int QMax = 64, SampleMax = 4;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private sealed class Note { public int Kind, Hears; public Village V; public string Raider, VName, DName; public bool Player; public int Burned, N; }

        private static readonly List<Note> _q = new List<Note>();
        private static readonly List<VillageBook.BurnNews> _burn = new List<VillageBook.BurnNews>();
        private static readonly Dictionary<string, KeyValuePair<MobileParty, int>> _lastBurning = new Dictionary<string, KeyValuePair<MobileParty, int>>(StringComparer.Ordinal);
        private static readonly List<string> _samples = new List<string>();
        private static readonly HashSet<string> _errSites = new HashSet<string>();
        private static int _stumbles, _stumblesAll;

        // liczniki doby (zeruje Daily)
        private static int _a, _b, _c, _dup, _qLost, _shown, _would, _f, _fNoBook, _same, _j, _k, _kWould, _l, _lWould;

        internal static void Reset()
        {
            _q.Clear(); _burn.Clear(); _lastBurning.Clear();
            lock (_errSites) _errSites.Clear();
            _stumblesAll = 0;
            DayReset();
        }

        private static void DayReset()
        {
            _a = _b = _c = _dup = _qLost = _shown = _would = _f = _fNoBook = _same = _j = _k = _kWould = _l = _lWould = 0;
            _samples.Clear();
            _stumbles = 0;
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumblesAll++;                    // liczymy potkniecia, niczego nie gasimy
            bool first;
            lock (_errSites) first = _errSites.Add(where);
            if (first) Log.Error(where, e);
        }

        private static int Mode(Settings s) { return s == null ? 0 : Math.Max(0, Math.Min(2, s.VillageRaidMessages)); }

        /// <summary>1 - wies rodu gracza (pan warowni wsi), 2 - wies krolestwa gracza, 0 - obca.</summary>
        private static int Hears(Village v)
        {
            if (v == null) return 0;
            var pc = Clan.PlayerClan;
            if (pc == null) return 0;
            var bound = v.Bound;
            if (bound != null && ReferenceEquals(bound.OwnerClan, pc)) return 1;
            var k = pc.Kingdom;
            if (k != null && v.Settlement != null && ReferenceEquals(v.Settlement.MapFaction, k)) return 2;
            return 0;
        }

        private static string RaiderName(MobileParty mp)
        {
            try
            {
                if (mp != null)
                {
                    var h = mp.LeaderHero;
                    if (h != null && h.Name != null) return h.Name.ToString();
                    if (mp.Name != null) return mp.Name.ToString();
                }
            }
            catch { }
            return "raiders";
        }

        private static string NameOf(Village v)
        {
            try { if (v != null && v.Settlement != null && v.Settlement.Name != null) return v.Settlement.Name.ToString(); } catch { }
            return "?";
        }

        // ------------------------------------------------------------ zdarzenie gry (watek glowny; tylko odczyt i kolejka)
        /// <summary>Sluchacz CampaignEvents.VillageStateChanged: BeingRaided - "plonie" (dedup milicji), Looted - "splondrowana" wedlug ksiegi. Nie rzuca.</summary>
        internal static void OnStateChanged(Village village, Village.VillageStates oldState, Village.VillageStates newState, MobileParty raider)
        {
            try
            {
                var s = Settings.Current;
                if (village == null || s == null || Campaign.Current == null) return;
                if (newState == Village.VillageStates.BeingRaided && raider != null)
                {
                    int day = (int)CampaignTime.Now.ToDays;
                    string key = village.Settlement != null ? village.Settlement.StringId : null;
                    KeyValuePair<MobileParty, int> last;
                    if (key != null && _lastBurning.TryGetValue(key, out last) && ReferenceEquals(last.Key, raider) && day - last.Value <= 1)
                    {
                        _dup++; _lastBurning[key] = new KeyValuePair<MobileParty, int>(raider, day);
                        return;
                    }
                    if (key != null) _lastBurning[key] = new KeyValuePair<MobileParty, int>(raider, day);
                    int h = Hears(village);
                    _a++; if (h == 1) _b++; else if (h == 2) _c++;
                    if (Mode(s) == 0) return;
                    if (_q.Count >= QMax) { _qLost++; return; }
                    _q.Add(new Note { Kind = KBurning, V = village, Hears = h, Raider = RaiderName(raider), Player = raider.IsMainParty });
                }
                else if (newState == Village.VillageStates.Looted)
                {
                    _f++;
                    object comp = null;
                    try { var p = village.Settlement != null ? village.Settlement.Party : null; var me = p != null ? p.MapEvent : null; comp = me != null ? me.Component : null; }
                    catch { comp = null; }
                    string vn, dn; int burned, n; bool sessBurned;
                    if (!VillageBook.LootNews(village, comp, out vn, out dn, out burned, out n, out sessBurned)) { _fNoBook++; return; }
                    if (sessBurned) { _same++; return; }           // ta sama sesja spalila wioske - "has been burned" juz w kolejce
                    _j++;
                    if (Mode(s) == 0) return;
                    if (_q.Count >= QMax) { _qLost++; return; }
                    _q.Add(new Note { Kind = KPlundered, V = village, Hears = Hears(village), Raider = RaiderName(raider), Player = raider != null && raider.IsMainParty,
                                      VName = vn, DName = dn, Burned = burned, N = n });
                }
            }
            catch (Exception e) { Stumble("VillageNews.OnStateChanged", e); }
        }

        // ------------------------------------------------------------ tick godzinny: komunikaty
        /// <summary>HourlyTickEvent: kolejka zdarzen gry i przejsc na spalona z ksiegi -> komunikaty (tekst zawsze, pokazany wedlug sluchacza). Pusta kolejka - nic.</summary>
        internal static void Hourly()
        {
            var s = Settings.Current;
            if (s == null || Campaign.Current == null) return;
            VillageBook.TakeNews(_burn);
            if (_q.Count == 0 && _burn.Count == 0) return;
            int mode = Mode(s);
            if (mode > 0)
            {
                foreach (var n in _q)
                {
                    try { if (n.Kind == KBurning) Burning(n, mode); else Plundered(n, mode); }
                    catch (Exception e) { Stumble("VillageNews.Hourly(" + (n.Kind == KBurning ? "plonie" : "splondrowana") + ")", e); }
                }
                foreach (var b in _burn)
                {
                    try { Burned(b, mode); }
                    catch (Exception e) { Stumble("VillageNews.Hourly(spalona)", e); }
                }
            }
            _q.Clear(); _burn.Clear();
        }

        private static void Burning(Note n, int mode)
        {
            string vn, dn;
            if (!VillageBook.FireNames(n.V, out vn, out dn)) { vn = dn = NameOf(n.V); }      // ksiega nieczynna - sama wies gry
            var t = n.Hears == 1
                ? VillageTexts.Make(VillageTexts.VilLogBurningOwn, "VILLAGE", vn, "RAIDER", n.Raider, "DISTRICT", dn)
                : VillageTexts.Make(VillageTexts.VilLogBurning, "RAIDER", n.Raider, "VILLAGE", vn, "DISTRICT", dn);
            bool show = !n.Player && ((n.Hears == 1 && mode >= 1) || (n.Hears == 2 && mode >= 2));   // wlasny rabunek gracza - gracz wie
            Emit(t, show, n.Hears == 1, ref _shown, ref _would);
        }

        private static void Plundered(Note n, int mode)
        {
            var t = n.Player
                ? VillageTexts.Make(VillageTexts.VilLogPlunderedPlayer, "VILLAGE", n.VName, "BURNED", n.Burned, "N", n.N)
                : VillageTexts.Make(VillageTexts.VilLogPlundered, "RAIDER", n.Raider, "VILLAGE", n.VName, "DISTRICT", n.DName, "BURNED", n.Burned, "N", n.N);
            bool show = n.Player || (n.Hears == 1 && mode >= 1) || (n.Hears == 2 && mode >= 2);
            Emit(t, show, !n.Player && n.Hears == 1, ref _l, ref _lWould);
        }

        private static void Burned(VillageBook.BurnNews b, int mode)
        {
            int h = Hears(b.V);
            string raider = null;
            try { if (!string.IsNullOrEmpty(b.Hero)) { var hero = Hero.Find(b.Hero); if (hero != null && hero.Name != null) raider = hero.Name.ToString(); } } catch { raider = null; }
            if (raider == null) raider = string.IsNullOrEmpty(b.Who) || b.Who == "?" ? "raiders" : b.Who;
            var t = b.Player
                ? VillageTexts.Make(VillageTexts.VilLogBurnedPlayer, "VILLAGE", b.VName)
                : VillageTexts.Make(VillageTexts.VilLogBurned, "VILLAGE", b.VName, "DISTRICT", b.DName, "RAIDER", raider);
            bool show = b.Player || (h == 1 && mode >= 1) || (h == 2 && mode >= 2);
            Emit(t, show, !b.Player && h == 1, ref _k, ref _kWould);
        }

        private static void Emit(TextObject t, bool show, bool bad, ref int shown, ref int would)
        {
            string str = t.ToString();
            if (_samples.Count < SampleMax) _samples.Add(str + (show ? "" : " [by bylo]"));
            if (!show) { would++; return; }
            shown++;
            try { MBInformationManager.AddQuickInformation(t); } catch (Exception e) { Stumble("VillageNews.Emit(AddQuickInformation)", e); }
            Log.Player(str, bad);
        }

        // ------------------------------------------------------------ pamiec wojny: tekst dla dymka (W2)
        /// <summary>
        /// Linia pamieci wojny wioski po uid (dymek E7/W2): "Burned by ..." tylko przy wpisie 'B' (przejscie na spalona w ksiedze), inaczej "Plundered by ...";
        /// napastnik na zywo z identyfikatora (bohater; bez bohatera w zapisie - rod; bohater nie istnieje - "a raider long gone"); pora i rok z kalendarza gry
        /// (rok Armoury 364 dni - LongYearTimeModel; rok AC ROT jak CampaignTime.GetYear). null - brak pamieci albo ksiega nieczynna.
        /// </summary>
        internal static string MemoryText(string uid)
        {
            try
            {
                VillageBook.WarMemory wm;
                if (!VillageBook.MemoryOf(uid, out wm)) return null;
                string raider = null;
                if (!string.IsNullOrEmpty(wm.Hero))
                {
                    var h = Hero.Find(wm.Hero);
                    if (h != null && h.Name != null) raider = h.Name.ToString();
                }
                else if (!string.IsNullOrEmpty(wm.Clan) && MBObjectManager.Instance != null)
                {
                    var c = MBObjectManager.Instance.GetObject<Clan>(wm.Clan);
                    if (c != null && c.Name != null) raider = c.Name.ToString();
                }
                object who = raider != null ? (object)raider : new TextObject(VillageTexts.VilMemGone);
                var when = CampaignTime.Days(wm.Day);
                TextObject season = GameTexts.FindText("str_season_" + when.GetSeasonOfYear);
                return VillageTexts.Make(wm.Burned ? VillageTexts.VilMemBurned : VillageTexts.VilMemPlundered, "RAIDER", who, "SEASON", season, "YEAR", when.GetYear,
                                         "FLED", (int)Math.Round(wm.Fled), "DEAD", (int)Math.Round(wm.Dead)).ToString();
            }
            catch (Exception e) { Stumble("VillageNews.MemoryText", e); return null; }
        }

        // ------------------------------------------------------------ doba
        /// <summary>ArmouryBehavior.OnDailyTick po MoneyLedger.Daily (po VillageBook.Daily): oproznia kolejki i pisze linie "Wioski - wiesci (E7/W1)"; zeruje liczniki.</summary>
        internal static void Daily()
        {
            var s = Settings.Current;
            if (s == null || Campaign.Current == null) return;
            try
            {
                Hourly();
                int burned, lost, memNew, memAll, burnsAll;
                VillageBook.TakeNewsCounts(out burned, out lost, out memNew, out memAll, out burnsAll);
                if (!s.LogEnabled) return;
                int day = (int)CampaignTime.Now.ToDays - 1;          // dzien jak w linii "Ksiega wiosek (E4/11)" (T5)
                var sb = new StringBuilder(900);
                sb.Append("Wioski - wiesci (E7/W1): dzien ").Append(day.ToString(Inv))
                  .Append(" | plonie: zdarzen ").Append(_a).Append(" (wsie gracza ").Append(_b).Append(", krolestwa gracza ").Append(_c)
                  .Append("; powtorka po milicji pominieta ").Append(_dup).Append("), komunikatow ").Append(_shown).Append(" (by bylo ").Append(_would).Append(')')
                  .Append("; spladrowane ").Append(_f).Append(" (bez ksiegi ").Append(_fNoBook).Append(')')
                  .Append(" | spalone w ksiedze ").Append(burned).Append(" (komunikatow \"burned\" ").Append(_k).Append(", by bylo ").Append(_kWould).Append(')')
                  .Append(", splondrowane bez spalenia ").Append(_j).Append(" (komunikatow \"plundered\" ").Append(_l).Append(", by bylo ").Append(_lWould)
                  .Append("; sesja spalila - bez drugiego ").Append(_same).Append(')')
                  .Append(" | pamiec wojny: wpisow ").Append(memAll).Append(" (nowe ").Append(memNew).Append(", spalen razem ").Append(burnsAll).Append(')')
                  .Append(" | VillageRaidMessages ").Append(Mode(s)).Append(", VillageWarMemory ").Append(s.VillageWarMemory ? "TAK" : "NIE");
                if (lost > 0 || _qLost > 0) sb.Append(" | zgubione ponad kolejke: spalone ").Append(lost).Append(", zdarzenia gry ").Append(_qLost);
                sb.Append(" | potkniecia dzis ").Append(_stumbles).Append(" (razem ").Append(_stumblesAll).Append(')');
                if (_samples.Count > 0) sb.Append(" | przyklady: ").Append(string.Join(" / ", _samples.ToArray()));
                sb.Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("VillageNews.Daily", e); }
            finally { DayReset(); }
        }
    }
}
