using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// DIAGNOZA ZATKANYCH WSI (paczka "diagnoza zatkanych wsi", TYLKO LOG - niczego w grze nie zmienia).
    /// Po 122 (woz zabiera caly magazyn) modele recenzentow obiecywaly 1-3% wsi z zapasem >= 1.5 W (bramka gry, przy ktorej wies
    /// nie produkuje NICZEGO, takze zywnosci), a testy 07.10 daja 29 w 16. dobie i 60-61 w 40. (121-124 i 121-126), liniowo od ok. 6. doby.
    /// Produkcja wsi w grze = model gry (vanilla przez NavalDLC; postfiksy BK i Armoury tylko mnoza) dla listy TYPU wsi + zywnosc, a W
    /// liczy dokladnie to samo x 5 dob (+ 5 dob lasu wsi 126), wiec wies w stanie Normal moze przejsc bramke tylko wtedy, gdy jej woz
    /// nie wyjechal z domu z calym magazynem przez ok. 7.5 doby (albo W spadlo, albo do magazynu wchodzi cos spoza listy). Linia dnia
    /// "Zatkane wsie (diagnoza):" rozbija zatkane wsie na te drogi: typ wsi, zapas / W, sklad zapasu (lista typu, zywnosc gry, las wsi,
    /// spoza listy - W tego nie liczy, zwierzeta), stan wsi (spladrowana, najezdzana, targ oblezony), stan wozu (w domu - i czemu stoi,
    /// w drodze - ile dob od wyjazdu z domu wedle zapisu gry, dokad, czy sie rusza; w miescie / zamku / innej osadzie; brak wozu - od
    /// kiedy i czy gra moze wystawic nowy) i przeplyw przez bramke (weszlo / wyszlo dzis). Raz na 5 dob 10 przykladow (linia
    /// "Zatkane wsie (przyklad)"). Odczyt: Village.All, magazyn wsi, Village.GetWarehouseCapacity (ta sama liczba co bramka gry i
    /// MarketRoad), VillagerPartyComponent, prywatny zapis gry VillagerCampaignBehavior._villageLastVillagerSendTime (czas ostatniego
    /// wyjazdu z domu - tylko odczyt przez refleksje), zdarzenie MobilePartyDestroyed (kiedy rozbito woz). Stan tylko w pamieci (bez
    /// zapisu), czyszczony w Reset (konstruktor ArmouryBehavior). Wyjatek przy jednej wsi - licznik potkniec, reszta liczy dalej.
    /// Wlacznik VillageClogDiagnostics (domyslnie wlaczony; wylaczony - nic nie liczy i nic nie pisze).
    /// Recenzja: druga linia dnia "Zatkane wsie (wozy w drodze):" rozbija droge 3e analizy (woz zatkanej wsi poza domem > 7.5 doby) od 3b
    /// (daleki kurs do miasta): doby od wyjazdu wedle celu (miasto / dom / bez celu), ruch od wczoraj (ile jednostek przejechal; ten sam cel
    /// i blizej, ten sam cel i NIE blizej - kreci sie / ucieka / utknal, inny cel, wczoraj nie w drodze), ucieczka (ShortTermBehavior), juki.
    /// W linii glownej: zatkane wsie Normal mimo wyjazdu wozu w ciagu 7.5 doby (wbrew fundamentowi analizy - drogi 3f / 3g / 3h). Porownania
    /// "od wczoraj" (przeplyw, stoi, W mniejsze) tylko dla kolejnej doby - po przerwie (wylacznik) liczone od nastepnej.
    /// </summary>
    internal static class VillageClogDiag
    {
        private const float Gate = 1.5f;              // bramka gry: TickProductions produkuje tylko przy zapasie < 1.5 W
        private const double Window = 7.5;            // doby od wyjazdu z pustym magazynem do bramki przy produkcji W/5 na dobe
        private const float StillUnits = 1f;          // woz "stoi": od wczoraj przejechal mniej niz 1 jednostke mapy (tabor jedzie kilkadziesiat na dobe)
        private const int ExampleEvery = 5, ExampleCount = 10;

        private enum Cart { Home, Road, Town, Castle, Other, None, Inactive }

        private sealed class Ex { public Village V; public string Type; public int Stock, W; public double Ratio; public string Top; public string CartText; }

        /// <summary>Woz w drodze wczoraj: pozycja, cel (GoToSettlement; null = bez celu) i odleglosc do celu w linii prostej (-1 = brak).</summary>
        private struct Seen { public Vec2 P; public Settlement T; public float D; }

        // stan sesji (pamiec, bez zapisu)
        private static readonly HashSet<Village> _prev = new HashSet<Village>();             // zatkane wczoraj (przeplyw przez bramke)
        private static bool _havePrev;
        private static double _lastNow = double.NaN;                                          // czas poprzedniej linii (porownania "od wczoraj" tylko dla kolejnej doby)
        private static readonly Dictionary<Village, int> _wPrev = new Dictionary<Village, int>();
        private static readonly Dictionary<Village, double> _noCart = new Dictionary<Village, double>();   // wies bez wozu od dnia (rozbicie albo pierwsza obserwacja)
        private static readonly Dictionary<Village, double> _lost = new Dictionary<Village, double>();     // dzien rozbicia wozu wsi (zdarzenie gry)
        private static Dictionary<MobileParty, Seen> _pos = new Dictionary<MobileParty, Seen>();           // wozy w drodze wczoraj (pozycja, cel, odleglosc do celu)
        private static int _lastExamples = int.MinValue;
        private static int _stumbles, _stumblesDay;
        private static readonly HashSet<string> _errSites = new HashSet<string>();
        private static FieldInfo _sendField;
        private static bool _sendSought;

        internal static void Reset()
        {
            _prev.Clear(); _havePrev = false; _lastNow = double.NaN; _wPrev.Clear(); _noCart.Clear(); _lost.Clear(); _pos = new Dictionary<MobileParty, Seen>();
            _lastExamples = int.MinValue; _stumbles = 0; _stumblesDay = 0; _errSites.Clear();
            _sendField = null; _sendSought = false;
        }

        private static bool Flee(AiBehavior b) { return b == AiBehavior.FleeToPoint || b == AiBehavior.FleeToGate || b == AiBehavior.FleeToParty || b == AiBehavior.GoAroundParty; }

        /// <summary>Juki wozu: sztuki i rodzaje (gra: woz bez celu z wiecej niz 1 rodzajem towaru jedzie na targ, inaczej do domu - HourlyTickParty).</summary>
        private static void Cargo(MobileParty mp, out int pieces, out int kinds)
        {
            pieces = 0; kinds = 0;
            var r = mp.ItemRoster;
            if (r == null) return;
            kinds = r.Count;
            for (int i = 0; i < r.Count; i++) pieces += r[i].Amount;
        }

        private static bool On { get { var s = Settings.Current; return s != null && s.VillageClogDiagnostics; } }

        /// <summary>Wyjatek przy jednej wsi albo jednym wozie: liczony zawsze, do pliku raz na miejsce (diagnozy nie gasimy).</summary>
        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumblesDay++;
            if (_errSites.Add(where)) Log.Error(where, e);
        }

        /// <summary>Zdarzenie gry MobilePartyDestroyed: zapamietaj, kiedy wies stracila woz (tylko odczyt). Pomija "sieroty" - stary tabor,
        /// ktory gra niszczy, gdy wies ma juz inny (VillagerCampaignBehavior.HourlyTickParty).</summary>
        internal static void OnPartyDestroyed(MobileParty party, PartyBase destroyer)
        {
            if (!On || party == null) return;
            try
            {
                if (!party.IsVillager) return;
                var home = party.HomeSettlement;
                var v = home != null ? home.Village : null;
                if (v == null) return;
                var cur = v.VillagerPartyComponent != null ? v.VillagerPartyComponent.MobileParty : null;
                if (cur != null && !ReferenceEquals(cur, party)) return;
                _lost[v] = CampaignTime.Now.ToDays;
            }
            catch (Exception e) { Stumble("VillageClogDiag.OnPartyDestroyed", e); }
        }

        /// <summary>Prywatny zapis gry: wies -> czas ostatniego wyjazdu wozu z domu (LoadAndSendVillagerParty). Tylko odczyt.</summary>
        private static IDictionary SendTimes()
        {
            try
            {
                if (!_sendSought) { _sendSought = true; _sendField = AccessTools.Field(typeof(VillagerCampaignBehavior), "_villageLastVillagerSendTime"); }
                if (_sendField == null || Campaign.Current == null) return null;
                var beh = Campaign.Current.GetCampaignBehavior<VillagerCampaignBehavior>();
                return beh != null ? _sendField.GetValue(beh) as IDictionary : null;
            }
            catch (Exception e) { Stumble("VillageClogDiag.SendTimes", e); return null; }
        }

        /// <summary>Rodzina typu wsi (mapa ROT: 24 typy) - do zbiorczego podzialu; obok i tak idzie podzial wedle VillageType.</summary>
        private static int Family(string id)
        {
            if (id == null) return 5;
            if (id.Contains("mine")) return 0;                                                   // gornicze
            if (id.Contains("lumber") || id.Contains("trapper")) return 1;                       // drwale i traperzy
            if (id.Contains("ranch") || id.Contains("cattle") || id.Contains("sheep") || id.Contains("swine") || id.Contains("hog")) return 2;   // hodowlane
            if (id.Contains("fish") || id.Contains("whal") || id.Contains("walrus")) return 3;   // rybackie
            return 4;                                                                             // rolnicze i reszta
        }
        private static readonly string[] FamilyName = { "gornicze", "drwale i traperzy", "hodowlane", "rybackie", "rolnicze", "bez typu" };

        private static string F1(double x) { return x.ToString("0.0", CultureInfo.InvariantCulture); }
        private static string F2(double x) { return x.ToString("0.00", CultureInfo.InvariantCulture); }
        private static string Pct(long part, long whole) { return whole > 0 ? (100.0 * part / whole).ToString("0", CultureInfo.InvariantCulture) + "%" : "-"; }

        private static string Name(Settlement s)
        {
            try { return s.Name != null ? s.Name.ToString() : s.StringId; } catch { return s != null ? s.StringId : "?"; }
        }

        /// <summary>Raz na dobe (ArmouryBehavior.OnDailyTick, po linii "Dowoz (skutki)"): linie "Zatkane wsie (diagnoza):" i "Zatkane wsie (wozy w drodze):",
        /// co 5 dob przyklady. Tylko log.</summary>
        internal static void Daily()
        {
            if (!On) return;
            long t0 = Stopwatch.GetTimestamp();
            var inv = CultureInfo.InvariantCulture;
            double now = CampaignTime.Now.ToDays;
            int day = (int)now - 1;
            // recenzja: porownania "od wczoraj" tylko dla kolejnej doby (po przerwie - wylacznik - wczorajsza pamiec jest sprzed kilku dob)
            double gap = now - _lastNow;
            bool cons = !double.IsNaN(gap) && gap > 0.5 && gap < 1.5;
            bool flows = _havePrev && cons, pause = _havePrev && !cons;
            var send = SendTimes();
            HashSet<ItemObject> food = new HashSet<ItemObject>();
            try
            {
                var dvt = Campaign.Current != null ? Campaign.Current.DefaultVillageTypes : null;
                if (dvt != null && dvt.ConsumableRawItems != null) foreach (var it in dvt.ConsumableRawItems) if (it != null) food.Add(it);
            }
            catch (Exception e) { Stumble("VillageClogDiag.Food", e); }
            bool woodOn = false; ItemObject wood = null;
            try { woodOn = VillageWoodlot.On; if (woodOn) wood = DefaultItems.HardWood; } catch (Exception e) { Stumble("VillageClogDiag.Wood", e); }
            int minMen = 12;
            try { if (Campaign.Current != null && Campaign.Current.Models != null && Campaign.Current.Models.PartySizeLimitModel != null) minMen = Campaign.Current.Models.PartySizeLimitModel.MinimumNumberOfVillagersAtVillagerParty; }
            catch (Exception e) { Stumble("VillageClogDiag.MinMen", e); }

            var typeLists = new Dictionary<VillageType, HashSet<ItemObject>>();
            var clogNow = new HashSet<Village>();
            var posNow = new Dictionary<MobileParty, Seen>();
            var types = new Dictionary<string, int[]>();
            var fam = new int[6, 2];
            var items = new Dictionary<ItemObject, long>();
            var outside = new Dictionary<ItemObject, long>();
            var exs = new List<Ex>();
            int all = 0, clog = 0, castleClog = 0, normalClog = 0, looted = 0, raided = 0, forced = 0, boundSiege = 0, villageRaid = 0, wDrop = 0;
            double sumRatio = 0.0, maxRatio = 0.0, sumRatioN = 0.0;
            long stockAll = 0, inList = 0, inFood = 0, inWood = 0, inOut = 0, animals = 0;
            // woz zatkanych
            int home = 0, homeReady = 0, homeState = 0, homeFight = 0, homeRaft = 0, road = 0, roadTown = 0, roadHome = 0, roadCastle = 0, roadOther = 0, roadIdle = 0, roadSea = 0, roadStill = 0;
            int town = 0, townSiege = 0, castle = 0, other = 0, none = 0, noneLowHearth = 0, inactive = 0, staleClog = 0, noEntry = 0, cartFight = 0, cartAiOff = 0;
            double homeD = 0, roadD = 0, roadMax = 0, townD = 0, townMax = 0, noneD = 0, noneMax = 0, homeMax = 0;
            int homeDn = 0, roadDn = 0, townDn = 0, roadTownDistN = 0;
            double roadTownDist = 0.0, roadTownDistMax = 0.0;
            // wszystkie wsie (tlo)
            int aNone = 0, aRoad = 0, aStill = 0, aTown = 0, aStale = 0, aEntry = 0, aMovedN = 0;
            double aMoved = 0.0;
            // przeplyw przez bramke
            int entered = 0, enRoad = 0, enNone = 0, enHome = 0, enOther = 0, left = 0, leftCart = 0, leftW = 0;
            // recenzja: zatkane Normal mimo wyjazdu wozu w ciagu 7.5 doby (wbrew fundamentowi: 3f / 3g / 3h) i woz zatkanej wsi w drodze (3b / 3e)
            int freshClog = 0, freshClog1 = 0;
            int gTownN = 0, gHomeN = 0, gIdleN = 0, gOtherN = 0, gHomeDistN = 0, gIdleDistN = 0, gMovedN = 0;
            double gTownD = 0, gTownMax = 0, gHomeD = 0, gHomeMax = 0, gIdleD = 0, gIdleMax = 0, gOtherD = 0, gHomeDist = 0, gIdleDist = 0, gMoved = 0;
            int gCloser = 0, gNotCloser = 0, gRetarget = 0, gNew = 0, gNoTarget = 0, gFlee = 0, gMulti = 0, gCargoN = 0;
            long gCargo = 0;

            foreach (var v in Village.All)
            {
                try
                {
                    if (v == null || v.Settlement == null || v.Bound == null) continue;
                    var st = v.Settlement;
                    all++;
                    var r = st.ItemRoster;
                    int stock = 0;
                    if (r != null) for (int i = 0; i < r.Count; i++) stock += r[i].Amount;
                    int W = v.GetWarehouseCapacity();
                    bool isClog = stock >= W * Gate;     // ta sama bramka co gra (TickProductions) i "Dowoz (skutki)"
                    string tid = v.VillageType != null ? v.VillageType.StringId : "?";
                    int[] tc; if (!types.TryGetValue(tid, out tc)) { tc = new int[2]; types[tid] = tc; }
                    tc[1]++;
                    int fm = v.VillageType != null ? Family(tid) : 5;
                    fam[fm, 1]++;
                    int wp; bool hadW = _wPrev.TryGetValue(v, out wp) && cons;     // W wczoraj - tylko dla kolejnej doby
                    _wPrev[v] = W;

                    double since = -1.0;                 // doby od ostatniego wyjazdu wozu z domu (zapis gry); -1 = brak wpisu
                    if (send != null && send.Contains(v)) { var o = send[v]; if (o is CampaignTime) since = ((CampaignTime)o).ElapsedDaysUntilNow; }
                    if (since >= 0.0) { aEntry++; if (since > Window) aStale++; }

                    // woz wsi
                    var mp = v.VillagerPartyComponent != null ? v.VillagerPartyComponent.MobileParty : null;
                    Cart cs;
                    if (mp == null) cs = Cart.None;
                    else if (!mp.IsActive) cs = Cart.Inactive;
                    else if (mp.CurrentSettlement == st) cs = Cart.Home;
                    else if (mp.CurrentSettlement == null) cs = Cart.Road;
                    else if (mp.CurrentSettlement.IsTown) cs = Cart.Town;
                    else if (mp.CurrentSettlement.IsCastle) cs = Cart.Castle;
                    else cs = Cart.Other;
                    double noCartDays = 0.0;
                    if (cs == Cart.None)
                    {
                        aNone++;
                        double since0;
                        if (!_noCart.TryGetValue(v, out since0))
                        {
                            double lost; since0 = _lost.TryGetValue(v, out lost) ? lost : now;
                            _noCart[v] = since0;
                        }
                        noCartDays = Math.Max(0.0, now - since0);
                    }
                    else { _noCart.Remove(v); _lost.Remove(v); }
                    bool still = false, hadSeen = false;
                    float moved = -1f, dTgt = -1f;
                    Settlement tgt = null;
                    Seen seen = default(Seen);
                    if (cs == Cart.Road)
                    {
                        aRoad++;
                        Vec2 p = mp.Position.ToVec2();
                        if (mp.DefaultBehavior == AiBehavior.GoToSettlement && mp.TargetSettlement != null)
                        {
                            tgt = mp.TargetSettlement;
                            try { dTgt = p.Distance(tgt.Position.ToVec2()); } catch (Exception e) { Stumble("VillageClogDiag.Distance", e); }
                        }
                        posNow[mp] = new Seen { P = p, T = tgt, D = dTgt };
                        if (cons && _pos.TryGetValue(mp, out seen))
                        {
                            hadSeen = true;
                            moved = p.Distance(seen.P);
                            aMoved += moved; aMovedN++;
                            if (moved < StillUnits) { still = true; aStill++; }
                        }
                    }
                    else if (cs == Cart.Town) aTown++;

                    if (!isClog)
                    {
                        if (flows && _prev.Contains(v))
                        {
                            left++;
                            if (since >= 0.0 && since < 1.0) leftCart++;
                            else if (hadW && W > wp) leftW++;
                        }
                        continue;
                    }

                    // ------------------------------------------------ wies zatkana
                    clogNow.Add(v);
                    clog++; tc[0]++; fam[fm, 0]++;
                    if (v.Bound.IsCastle) castleClog++;
                    double ratio = stock / (double)Math.Max(1, W);
                    sumRatio += ratio; if (ratio > maxRatio) maxRatio = ratio;
                    if (hadW && W < wp) wDrop++;
                    var state = v.VillageState;
                    if (state == Village.VillageStates.Normal) { normalClog++; sumRatioN += ratio; }
                    else if (state == Village.VillageStates.Looted) looted++;
                    else if (state == Village.VillageStates.BeingRaided) raided++;
                    else forced++;
                    if (v.Bound.IsUnderSiege) boundSiege++;
                    if (st.IsUnderRaid) villageRaid++;
                    if (since >= 0.0) { if (since > Window) staleClog++; } else noEntry++;
                    // fundament analizy: wies Normal po wyjezdzie wozu z calym magazynem dochodzi do bramki dopiero po ok. 7.5 doby - zatkana
                    // wczesniej = woz nie zabral magazynu (3g), produkcja ponad W (3f) albo spadek W (3h)
                    if (state == Village.VillageStates.Normal && since >= 0.0 && since <= Window) { freshClog++; if (since < 1.0) freshClog1++; }

                    // sklad zapasu
                    HashSet<ItemObject> list;
                    if (v.VillageType == null) list = new HashSet<ItemObject>();
                    else if (!typeLists.TryGetValue(v.VillageType, out list))
                    {
                        list = new HashSet<ItemObject>();
                        if (v.VillageType.Productions != null) foreach (var pr in v.VillageType.Productions) if (pr.Item1 != null) list.Add(pr.Item1);
                        typeLists[v.VillageType] = list;
                    }
                    bool lumber = woodOn && VillageWoodlot.IsLumber(v);
                    var top = new List<KeyValuePair<ItemObject, int>>();
                    if (r != null)
                        for (int i = 0; i < r.Count; i++)
                        {
                            var el = r[i];
                            var it = el.EquipmentElement.Item;
                            int n = el.Amount;
                            if (it == null || n <= 0) continue;
                            stockAll += n;
                            long c0; items.TryGetValue(it, out c0); items[it] = c0 + n;
                            if (list.Contains(it)) inList += n;
                            else if (food.Contains(it)) inFood += n;
                            else if (woodOn && !lumber && ReferenceEquals(it, wood)) inWood += n;
                            else { inOut += n; long c1; outside.TryGetValue(it, out c1); outside[it] = c1 + n; }
                            if (it.HasHorseComponent) animals += n;
                            top.Add(new KeyValuePair<ItemObject, int>(it, n));
                        }

                    // stan wozu zatkanej wsi
                    string cartText, extra = "";
                    switch (cs)
                    {
                        case Cart.None:
                            none++; noneD += noCartDays; if (noCartDays > noneMax) noneMax = noCartDays;
                            bool low = v.Hearth <= minMen;
                            if (low) noneLowHearth++;
                            cartText = "brak wozu od " + F1(noCartDays) + " doby" + (_lost.ContainsKey(v) ? " (rozbity)" : " (od wczytania / pierwszej obserwacji)")
                                     + (low ? ", hearth " + F1(v.Hearth) + " <= " + minMen + " - gra nie wystawi nowego" : ", hearth " + F1(v.Hearth));
                            break;
                        case Cart.Inactive:
                            inactive++; cartText = "woz nieczynny (IsActive false)"; break;
                        case Cart.Home:
                            {
                                home++;
                                string why;
                                if (state != Village.VillageStates.Normal) { homeState++; why = "wies " + state; }
                                else if ((st.Party != null && st.Party.MapEvent != null) || mp.MapEvent != null) { homeFight++; why = "bitwa / najazd na wies"; }
                                else if (mp.IsInRaftState) { homeRaft++; why = "na tratwie"; }
                                else { homeReady++; why = "gotowy do wyjazdu (Normal, bez bitwy; gra: 15% na godzine przy zapasie >= W)"; }
                                if (since >= 0.0) { homeD += since; homeDn++; if (since > homeMax) homeMax = since; }
                                cartText = "w domu - " + why + (since >= 0.0 ? "; od ostatniego wyjazdu " + F1(since) + " doby" : "; bez wpisu wyjazdu");
                                break;
                            }
                        case Cart.Road:
                            {
                                road++;
                                if (since >= 0.0) { roadD += since; roadDn++; if (since > roadMax) roadMax = since; }
                                var tg = tgt;                    // cel GoToSettlement (null = bez celu) - wyliczony wyzej razem z odlegloscia woz -> cel
                                string dest;
                                if (tg != null)
                                {
                                    // odleglosci w linii prostej (droga ok. x1.25): wies -> cel i woz -> cel - dlugi kurs czy woz stoi
                                    float dHome = -1f, dLeft = dTgt;
                                    try { dHome = st.Position.ToVec2().Distance(tg.Position.ToVec2()); } catch (Exception e) { Stumble("VillageClogDiag.Distance", e); }
                                    string far = dHome >= 0f ? " (" + dHome.ToString("0", inv) + " jedn. w linii prostej od wsi; woz " + dLeft.ToString("0", inv) + " jedn. od celu)" : "";
                                    if (tg == st)
                                    {
                                        roadHome++; dest = "do domu" + (dLeft >= 0f ? " (" + dLeft.ToString("0", inv) + " jedn. w linii prostej)" : "");
                                        if (since >= 0.0) { gHomeD += since; gHomeN++; if (since > gHomeMax) gHomeMax = since; }
                                        if (dLeft >= 0f) { gHomeDist += dLeft; gHomeDistN++; }
                                    }
                                    else if (tg.IsTown)
                                    {
                                        roadTown++; dest = "do miasta " + Name(tg) + (tg.IsUnderSiege ? " (oblezone)" : "") + far;
                                        if (dHome >= 0f) { roadTownDist += dHome; roadTownDistN++; if (dHome > roadTownDistMax) roadTownDistMax = dHome; }
                                        if (since >= 0.0) { gTownD += since; gTownN++; if (since > gTownMax) gTownMax = since; }
                                    }
                                    else
                                    {
                                        if (tg.IsCastle) { roadCastle++; dest = "do zamku " + Name(tg) + far; }
                                        else { roadOther++; dest = "do osady " + Name(tg) + far; }
                                        if (since >= 0.0) { gOtherD += since; gOtherN++; }
                                    }
                                }
                                else
                                {
                                    roadIdle++; dest = "bez celu (" + mp.DefaultBehavior + ")";
                                    if (since >= 0.0) { gIdleD += since; gIdleN++; if (since > gIdleMax) gIdleMax = since; }
                                    try { gIdleDist += mp.Position.ToVec2().Distance(st.Position.ToVec2()); gIdleDistN++; } catch (Exception e) { Stumble("VillageClogDiag.Distance", e); }
                                }
                                bool sea = false;
                                try { sea = mp.IsCurrentlyAtSea || mp.IsInRaftState; } catch { }
                                if (sea) roadSea++;
                                if (still) roadStill++;
                                cartText = "w drodze " + (since >= 0.0 ? F1(since) + " doby od wyjazdu" : "(bez wpisu wyjazdu)") + " - " + dest
                                         + (sea ? ", na morzu / tratwie" : "") + (still ? ", STOI (od wczoraj mniej niz 1 jedn.)" : "");
                                // recenzja: ruch od wczoraj - jedzie (blizej celu), kreci sie / ucieka / utknal (ten sam cel, nie blizej), zmienia cel
                                string mv;
                                if (!hadSeen) { gNew++; mv = cons ? "wczoraj nie w drodze" : "od jutra"; }
                                else
                                {
                                    gMoved += moved; gMovedN++;
                                    mv = "od wczoraj " + moved.ToString("0", inv) + " jedn.";
                                    if (tg == null) { gNoTarget++; mv += ", bez celu"; }
                                    else if (!ReferenceEquals(seen.T, tg)) { gRetarget++; mv += ", cel inny niz wczoraj"; }
                                    else if (dTgt >= 0f && seen.D >= 0f && dTgt < seen.D - StillUnits) { gCloser++; mv += ", do celu blizej o " + (seen.D - dTgt).ToString("0", inv) + " jedn."; }
                                    else { gNotCloser++; mv += ", do celu NIE blizej (wczoraj " + seen.D.ToString("0", inv) + " jedn.)"; }
                                }
                                AiBehavior stb = mp.ShortTermBehavior;
                                if (Flee(stb)) { gFlee++; mv += ", chwilowo " + stb; }
                                int pcs, kinds; Cargo(mp, out pcs, out kinds);
                                gCargo += pcs; gCargoN++; if (kinds > 1) gMulti++;
                                extra = "; ruch: " + mv + "; juki " + pcs + " szt. (" + kinds + " rodz.)";
                                break;
                            }
                        case Cart.Town:
                            {
                                town++;
                                bool sg = mp.CurrentSettlement.IsUnderSiege;
                                if (sg) townSiege++;
                                if (since >= 0.0) { townD += since; townDn++; if (since > townMax) townMax = since; }
                                string far = "";
                                try { far = " (" + st.Position.ToVec2().Distance(mp.CurrentSettlement.Position.ToVec2()).ToString("0", inv) + " jedn. w linii prostej od wsi)"; } catch (Exception e) { Stumble("VillageClogDiag.Distance", e); }
                                cartText = "w miescie " + Name(mp.CurrentSettlement) + (sg ? " (OBLEZONE)" : "") + far + (since >= 0.0 ? ", " + F1(since) + " doby od wyjazdu" : "");
                                break;
                            }
                        case Cart.Castle:
                            castle++; cartText = "w zamku " + Name(mp.CurrentSettlement) + (since >= 0.0 ? ", " + F1(since) + " doby od wyjazdu" : ""); break;
                        default:
                            other++; cartText = "w osadzie " + Name(mp.CurrentSettlement) + (since >= 0.0 ? ", " + F1(since) + " doby od wyjazdu" : ""); break;
                    }
                    if (mp != null && cs != Cart.None)
                    {
                        // woz w bitwie (gra nie rusza go ani z domu, ani w drodze) albo z wylaczonym AI (nikt nie wyda mu rozkazu)
                        if (mp.MapEvent != null) { cartFight++; if (cs != Cart.Home) cartText += ", W BITWIE"; }
                        if (mp.Ai != null && mp.Ai.IsDisabled) { cartAiOff++; cartText += ", AI WYLACZONE"; }
                    }
                    if (state != Village.VillageStates.Normal && cs != Cart.Home) cartText += "; wies " + state;   // przy wozie w domu stan wsi juz jest w powodzie
                    if (v.Bound.IsUnderSiege) cartText += "; " + (v.Bound.IsCastle ? "zamek" : "miasto") + " wsi oblezone";
                    if (mp != null && cs != Cart.None && cs != Cart.Road)
                    {
                        int pcs, kinds; Cargo(mp, out pcs, out kinds);
                        extra = "; juki " + pcs + " szt. (" + kinds + " rodz.)";
                    }
                    cartText += extra;
                    if (flows && !_prev.Contains(v))
                    {
                        entered++;
                        if (cs == Cart.Road || cs == Cart.Town || cs == Cart.Castle || cs == Cart.Other) enRoad++;
                        else if (cs == Cart.None) enNone++;
                        else if (cs == Cart.Home) enHome++;
                        else enOther++;
                    }
                    top.Sort((a, b) => b.Value.CompareTo(a.Value));
                    var sbt = new StringBuilder();
                    for (int i = 0; i < top.Count && i < 3; i++) { if (i > 0) sbt.Append(", "); sbt.Append(top[i].Key.StringId).Append(' ').Append(top[i].Value); }
                    exs.Add(new Ex { V = v, Type = tid, Stock = stock, W = W, Ratio = ratio, Top = sbt.ToString(), CartText = cartText });
                }
                catch (Exception e) { Stumble("VillageClogDiag.Daily(wies)", e); }
            }

            // przeplyw: wsie zatkane wczoraj, ktorych dzis nie ma w Village.All (np. zniszczone) - nie licza sie jako "wyszly"
            _prev.Clear(); foreach (var v in clogNow) _prev.Add(v);
            _havePrev = true;
            _pos = posNow;
            _lastNow = now;

            try
            {
                var sb = new StringBuilder();
                sb.Append("Zatkane wsie (diagnoza): dzien ").Append(day).Append(" - zatkanych (zapas >= 1.5 W) ").Append(clog).Append(" z ").Append(all)
                  .Append(" (wsie zamkowe ").Append(castleClog).Append(", miejskie ").Append(clog - castleClog).Append(")")
                  .Append("; stan wsi: Normal ").Append(normalClog).Append(", spladrowane ").Append(looted).Append(", najezdzane ").Append(raided).Append(", przymuszone ").Append(forced)
                  .Append(" (poza Normal model daje 0 - W gry = 5 szt., liczy sie kazdy zapas >= 8), najazd teraz ").Append(villageRaid).Append(", zamek / miasto wsi oblezone ").Append(boundSiege);
                sb.Append("; rodziny (zatkane/wszystkie):");
                for (int f = 0; f < 6; f++) if (fam[f, 1] > 0) sb.Append(' ').Append(FamilyName[f]).Append(' ').Append(fam[f, 0]).Append('/').Append(fam[f, 1]).Append(',');
                if (sb[sb.Length - 1] == ',') sb.Length--;
                var tl = new List<KeyValuePair<string, int[]>>(types);
                tl.Sort((a, b) => a.Value[0] != b.Value[0] ? b.Value[0].CompareTo(a.Value[0]) : string.CompareOrdinal(a.Key, b.Key));
                sb.Append("; typy:");
                int shown = 0;
                foreach (var kv in tl) { if (kv.Value[0] <= 0) break; sb.Append(' ').Append(kv.Key).Append(' ').Append(kv.Value[0]).Append('/').Append(kv.Value[1]).Append(','); shown++; }
                if (shown == 0) sb.Append(" -"); else sb.Length--;
                sb.Append("; zapas/W srednio ").Append(clog > 0 ? F2(sumRatio / clog) : "-").Append(" (Normal ").Append(normalClog > 0 ? F2(sumRatioN / normalClog) : "-").Append("), max ").Append(clog > 0 ? F2(maxRatio) : "-")
                  .Append(", W mniejsze niz wczoraj ").Append(wDrop);
                var il = new List<KeyValuePair<ItemObject, long>>(items);
                il.Sort((a, b) => b.Value.CompareTo(a.Value));
                sb.Append("; zapas zatkanych ").Append(stockAll).Append(" szt., top 3:");
                for (int i = 0; i < il.Count && i < 3; i++) sb.Append(' ').Append(il[i].Key.StringId).Append(' ').Append(il[i].Value).Append(" (").Append(Pct(il[i].Value, stockAll)).Append(")").Append(i < 2 && i < il.Count - 1 ? "," : "");
                if (il.Count == 0) sb.Append(" -");
                sb.Append("; sklad: lista typu wsi ").Append(Pct(inList, stockAll)).Append(", zywnosc gry ").Append(Pct(inFood, stockAll)).Append(", las wsi (126) ").Append(Pct(inWood, stockAll))
                  .Append(", SPOZA LISTY (W tego nie liczy) ").Append(inOut).Append(" szt. = ").Append(Pct(inOut, stockAll));
                if (outside.Count > 0)
                {
                    var ol = new List<KeyValuePair<ItemObject, long>>(outside);
                    ol.Sort((a, b) => b.Value.CompareTo(a.Value));
                    sb.Append(" [");
                    for (int i = 0; i < ol.Count && i < 3; i++) sb.Append(i > 0 ? ", " : "").Append(ol[i].Key.StringId).Append(' ').Append(ol[i].Value);
                    sb.Append("]");
                }
                sb.Append(", zwierzeta ").Append(animals).Append(" szt. = ").Append(Pct(animals, stockAll));
                sb.Append("; woz zatkanych: w domu ").Append(home).Append(" (gotowy do wyjazdu ").Append(homeReady).Append(", wies poza Normal ").Append(homeState).Append(", bitwa / najazd ").Append(homeFight).Append(", tratwa ").Append(homeRaft)
                  .Append(homeDn > 0 ? "; od wyjazdu srednio " + F1(homeD / homeDn) + " max " + F1(homeMax) + " doby" : "").Append(")")
                  .Append(", w drodze ").Append(road).Append(" (od wyjazdu srednio ").Append(roadDn > 0 ? F1(roadD / roadDn) : "-").Append(" max ").Append(roadDn > 0 ? F1(roadMax) : "-").Append(" doby; do miasta ").Append(roadTown)
                  .Append(roadTownDistN > 0 ? " (wies -> miasto w linii prostej srednio " + (roadTownDist / roadTownDistN).ToString("0", inv) + ", max " + roadTownDistMax.ToString("0", inv) + " jedn.)" : "")
                  .Append(", do domu ").Append(roadHome).Append(", do zamku ").Append(roadCastle).Append(", do innej osady ").Append(roadOther).Append(", bez celu ").Append(roadIdle)
                  .Append(", na morzu / tratwie ").Append(roadSea).Append(", stoi od wczoraj ").Append(roadStill).Append(")")
                  .Append(", w miescie ").Append(town).Append(" (oblezonym ").Append(townSiege).Append(townDn > 0 ? "; od wyjazdu srednio " + F1(townD / townDn) + " max " + F1(townMax) + " doby" : "").Append(")")
                  .Append(", w zamku ").Append(castle).Append(", w innej osadzie ").Append(other)
                  .Append(", BRAK WOZU ").Append(none).Append(" (od ").Append(none > 0 ? F1(noneD / none) : "-").Append(" doby srednio, max ").Append(none > 0 ? F1(noneMax) : "-").Append("; hearth <= ").Append(minMen).Append(" - gra nie wystawi nowego: ").Append(noneLowHearth).Append(")")
                  .Append(", nieczynny ").Append(inactive).Append("; woz w bitwie ").Append(cartFight).Append(", z wylaczonym AI ").Append(cartAiOff)
                  .Append("; od wyjazdu > ").Append(F1(Window)).Append(" doby: zatkanych ").Append(staleClog).Append(" (bez wpisu wyjazdu ").Append(noEntry).Append("), wszystkich wsi ").Append(aStale).Append(" z ").Append(aEntry).Append(" z wpisem")
                  .Append("; zatkane Normal MIMO wyjazdu w ciagu ").Append(F1(Window)).Append(" doby ").Append(freshClog).Append(" (w ostatniej dobie ").Append(freshClog1).Append(")")
                  .Append("; wszystkie wsie: bez wozu ").Append(aNone).Append(", wozy w drodze ").Append(aRoad).Append(" (stoi od wczoraj ").Append(aStill).Append("), w miescie ").Append(aTown);
                if (flows)
                    sb.Append("; bramka dzis: weszlo ").Append(entered).Append(" (woz poza domem ").Append(enRoad).Append(", brak wozu ").Append(enNone).Append(", woz w domu ").Append(enHome).Append(enOther > 0 ? ", inne " + enOther : "").Append(")")
                      .Append(", wyszlo ").Append(left).Append(" (wyjazd wozu w ostatniej dobie ").Append(leftCart).Append(", wieksze W ").Append(leftW).Append(", inne ").Append(left - leftCart - leftW).Append(")");
                else sb.Append("; bramka dzis: pierwsza doba po wczytaniu - przeplyw od jutra").Append(pause ? " (przerwa w diagnozie - pamiec sprzed niej pominieta)" : "");
                sb.Append("; zapis gry o wyjazdach ").Append(send != null ? "czytany" : "NIEDOSTEPNY");
                double ms = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
                sb.Append("; koszt ").Append(ms.ToString("0.0", inv)).Append(" ms; potkniecia dzis ").Append(_stumblesDay).Append(" (od wczytania ").Append(_stumbles).Append(").");
                Log.Info(sb.ToString());

                // recenzja: druga linia - woz zatkanej wsi w drodze: daleki kurs do miasta (3b) czy woz, ktory nie wraca (3e) i dlaczego
                var sr = new StringBuilder();
                sr.Append("Zatkane wsie (wozy w drodze): dzien ").Append(day).Append(" - wozow zatkanych wsi w drodze ").Append(road)
                  .Append("; od wyjazdu z domu: do miasta ").Append(gTownN).Append(" (srednio ").Append(gTownN > 0 ? F1(gTownD / gTownN) : "-").Append(", max ").Append(gTownN > 0 ? F1(gTownMax) : "-").Append(" doby)")
                  .Append(", do domu ").Append(gHomeN).Append(" (srednio ").Append(gHomeN > 0 ? F1(gHomeD / gHomeN) : "-").Append(", max ").Append(gHomeN > 0 ? F1(gHomeMax) : "-").Append(" doby; do domu w linii prostej srednio ").Append(gHomeDistN > 0 ? (gHomeDist / gHomeDistN).ToString("0", inv) : "-").Append(" jedn.)")
                  .Append(", bez celu ").Append(gIdleN).Append(" (srednio ").Append(gIdleN > 0 ? F1(gIdleD / gIdleN) : "-").Append(", max ").Append(gIdleN > 0 ? F1(gIdleMax) : "-").Append(" doby; od domu w linii prostej srednio ").Append(gIdleDistN > 0 ? (gIdleDist / gIdleDistN).ToString("0", inv) : "-").Append(" jedn.)")
                  .Append(", do zamku / innej osady ").Append(gOtherN).Append(" (srednio ").Append(gOtherN > 0 ? F1(gOtherD / gOtherN) : "-").Append(" doby)");
                if (cons)
                    sr.Append("; ruch od wczoraj: przejechaly srednio ").Append(gMovedN > 0 ? (gMoved / gMovedN).ToString("0.0", inv) : "-").Append(" jedn. (wszystkie wozy w drodze ").Append(aMovedN > 0 ? (aMoved / aMovedN).ToString("0.0", inv) : "-").Append(")")
                      .Append(", stoi (< ").Append(StillUnits.ToString("0", inv)).Append(" jedn.) ").Append(roadStill)
                      .Append(", ten sam cel i blizej ").Append(gCloser).Append(", ten sam cel i NIE blizej ").Append(gNotCloser).Append(", cel inny niz wczoraj ").Append(gRetarget)
                      .Append(", bez celu ").Append(gNoTarget).Append(", wczoraj nie w drodze ").Append(gNew);
                else sr.Append("; ruch od wczoraj: pierwsza doba po wczytaniu albo przerwie - od jutra");
                sr.Append("; uciekaja / omijaja (zachowanie chwilowe) ").Append(gFlee)
                  .Append("; juki srednio ").Append(gCargoN > 0 ? ((double)gCargo / gCargoN).ToString("0", inv) : "-").Append(" szt., wiecej niz 1 rodzaj towaru ").Append(gMulti).Append(" (gra: woz bez celu z takim jukiem jedzie na targ, inaczej do domu).");
                Log.Info(sr.ToString());

                if (exs.Count > 0 && (_lastExamples == int.MinValue || day - _lastExamples >= ExampleEvery))
                {
                    _lastExamples = day;
                    exs.Sort((a, b) => b.Ratio.CompareTo(a.Ratio));
                    int k = Math.Min(ExampleCount, exs.Count);
                    for (int i = 0; i < k; i++)
                    {
                        var e = exs[k == exs.Count ? i : (int)((long)i * exs.Count / k)];   // rownomiernie wedle zapas/W, od najwiekszego
                        Log.Info("Zatkane wsie (przyklad): dzien " + day + " - " + (i + 1) + "/" + k + " z " + exs.Count + " - " + Name(e.V.Settlement) + " [" + e.V.Settlement.StringId + "], typ " + e.Type
                                 + ", wies " + (e.V.Bound.IsCastle ? "zamkowa" : "miejska") + ", zapas/W " + F2(e.Ratio) + " (" + e.Stock + "/" + e.W + "), top: " + e.Top + "; woz: " + e.CartText + ".");
                    }
                }
            }
            catch (Exception e) { Log.Error("VillageClogDiag.Daily", e); }
            finally { _stumblesDay = 0; }     // recenzja: zerowane PO linii - potkniecia sluchacza rozbicia miedzy liniami wchodza do "dzis"
        }
    }
}
