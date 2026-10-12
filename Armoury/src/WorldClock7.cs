using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// E7/0 (224) - POMIAR ETAPU 7, TYLKO LOG (projekt etapu 7 v2, rozdz. 3 "E7/0"; klucz Stage7MeasureLog). Niczego nie zmienia w grze: zadna latka,
    /// zaden rozkaz, zadne zloto. Trzy rzeczy, ktorych dotad nie bylo widac:
    ///  1. WOJNA I POKOJ z przyczyna - sluchacze CampaignEvents.WarDeclared (DeclareWarDetail) i MakePeace (MakePeaceDetail); linia na kazde zdarzenie
    ///     miedzy krolestwami (dni od poprzedniego zdarzenia tej pary - pamiec sesji), przy przyczynie Default - kto wolal akcje gry (pierwsza ramka stosu
    ///     za MakePeaceAction / DeclareWarAction; zdarzenia rzadkie). Pokoj z zerwanego wezwania 168 (CrownCallToWar) znaczony para (Mark168) - linia podaje
    ///     te przyczyne zamiast golego Default. Upadek krolestwa (KingdomDestroyedEvent) - osobna linia: gra konczy wtedy jego wojny bez zdarzenia pokoju,
    ///     a KRONIKA WOJEN (CrashScribe) traci te pary. Zdarzenia z rodami spoza krolestw tylko w liczniku dobowym.
    ///  2. ZBIORKA ARMII w nocy - czlonek armii niedoczepiony (Army != null, AttachedTo == null, nie wodz) w godzinach obozu: LordNight (NightMarch.cs)
    ///     liczy sen / marsz z powodem / alarm, petla obozu swiata (NightRest.cs) - sen dlugu, ksiega snu AI (AiSleepLedger) - ruch w minionej godzinie
    ///     obozu bez wpisu powodu; linia raz na noc o swicie (po ostatniej godzinie obozu).
    ///  3. TURNIEJE wedlug tygodnia pory - sluchacz TournamentStarted, w toku z TournamentManager gry (raz na dobe, po miastach).
    /// Stan czyszczony w konstruktorze ArmouryBehavior (Reset); bez zapisu (pamiec sesji).
    /// </summary>
    internal static class WorldClock7
    {
        private const int QuickWarDays = 21;   // prog linii: nowa wojna pary krotko po pokoju (projekt rozdz. 3 E7/0)

        private static bool On { get { var s = Settings.Current; return s != null && s.Stage7MeasureLog; } }

        // ---- wojna i pokoj
        private struct PairEvent { public int Day; public bool War; }
        private static readonly Dictionary<string, PairEvent> _pairs = new Dictionary<string, PairEvent>();   // para krolestw (id posortowane) -> ostatnie zdarzenie
        private static readonly Dictionary<string, int> _dCauses = new Dictionary<string, int>();              // krolestwa: "wojna X" / "pokoj X" -> liczba od poprzedniej linii
        private static readonly Dictionary<string, int> _dOtherCauses = new Dictionary<string, int>();         // zdarzenia z rodami spoza krolestw
        private static readonly List<string> _dQuickList = new List<string>();
        private static int _dWar, _dPeace, _dOther, _dDestroyed, _dQuick, _sQuick;
        // wezwanie 168: para, ktorej pokoj zawiera CrownCallToWar (zerwane porozumienie) - tylko na czas MakePeaceAction.Apply; zdarzenia zagniezdzone
        // (inne pary, wywolane przez sluchaczy tego pokoju) nie dziedzicza przyczyny
        [ThreadStatic] private static IFaction _p168a;
        [ThreadStatic] private static IFaction _p168b;
        internal const string Cause168 = "wezwanie 168 zerwane";

        // ---- turnieje
        private static readonly List<string> _dStarted = new List<string>();
        private static double _lastStartAt = -1;   // CampaignTime.ToDays ostatniego startu widzianego w tej sesji

        // ---- zbiorka (partio-godziny okna obozu od ostatniego switu)
        internal const byte MSleep = 0, MFlee = 1, MChase = 2, MRelief = 3, MAlarm = 4;
        private static int _mHours, _mSleep, _mDebt, _mFlee, _mChase, _mRelief, _mAlarm, _mMovedNoReason, _mMovedWoken, _lField, _lSleep;

        private static int _stumbles;

        internal static void Reset()
        {
            _pairs.Clear(); _sQuick = 0; _lastStartAt = -1; _stumbles = 0;
            _p168a = null; _p168b = null;
            ZeroDay(); ZeroNight();
        }

        private static void ZeroDay()
        {
            _dCauses.Clear(); _dOtherCauses.Clear(); _dQuickList.Clear(); _dStarted.Clear();
            _dWar = _dPeace = _dOther = _dDestroyed = _dQuick = 0;
        }

        private static void ZeroNight()
        {
            _mHours = _mSleep = _mDebt = _mFlee = _mChase = _mRelief = _mAlarm = _mMovedNoReason = _mMovedWoken = _lField = _lSleep = 0;
        }

        private static void Stumble(string where, Exception e)
        {
            // potkniecia liczone, funkcja dziala dalej (CLAUDE.md 7 - bez globalnego wylacznika)
            _stumbles++;
            if (_stumbles <= 3) Log.Error("WorldClock7." + where, e);
        }

        // ------------------------------------------------------------ wojna i pokoj

        /// <summary>CrownCallToWar: przed MakePeaceAction.Apply(called, against) - przyczyna tej pary to zerwane wezwanie 168. Clear168 w finally.</summary>
        internal static void Mark168(IFaction a, IFaction b) { _p168a = a; _p168b = b; }
        internal static void Clear168() { _p168a = null; _p168b = null; }

        internal static void OnWarDeclared(IFaction a, IFaction b, DeclareWarAction.DeclareWarDetail detail)
        {
            try
            {
                if (!On || a == null || b == null || a.IsBanditFaction || b.IsBanditFaction) return;
                if (!a.IsKingdomFaction || !b.IsKingdomFaction) { _dOther++; Bump(_dOtherCauses, "wojna " + detail); return; }
                string kind = detail.ToString();
                Event(a, b, true, kind, detail == DeclareWarAction.DeclareWarDetail.Default ? Caller() : null);
            }
            catch (Exception e) { Stumble("OnWarDeclared", e); }
        }

        internal static void OnMakePeace(IFaction a, IFaction b, MakePeaceAction.MakePeaceDetail detail)
        {
            try
            {
                if (!On || a == null || b == null || a.IsBanditFaction || b.IsBanditFaction) return;
                if (!a.IsKingdomFaction || !b.IsKingdomFaction) { _dOther++; Bump(_dOtherCauses, "pokoj " + detail); return; }
                bool c168 = _p168a != null && ((_p168a == a && _p168b == b) || (_p168a == b && _p168b == a));
                string kind = c168 ? Cause168 : detail.ToString();
                Event(a, b, false, kind, !c168 && detail == MakePeaceAction.MakePeaceDetail.Default ? Caller() : null);
            }
            catch (Exception e) { Stumble("OnMakePeace", e); }
        }

        internal static void OnKingdomDestroyed(Kingdom k)
        {
            try
            {
                if (!On || k == null) return;
                // recenzja E7/0: DestroyKingdomAction.ApplyInternal (G) zdejmuje postawy (RemoveFactionsFromCampaignWars) i znaczy krolestwo jako zniszczone
                // PRZED tym zdarzeniem - FactionManager zwracal tu zawsze 0 wojen. Wojny z pamieci sesji: pary, ktorych ostatnie zdarzenie to WOJNA;
                // para dostaje koniec wojny (bez linii POKOJ), zeby upadek drugiej strony nie liczyl jej drugi raz
                int day = (int)CampaignTime.Now.ToDays;
                string id = k.StringId ?? "";
                var ended = new List<string>();
                foreach (var kv in _pairs)
                {
                    if (!kv.Value.War) continue;
                    int bar = kv.Key.IndexOf('|');
                    if (bar < 0) continue;
                    if (kv.Key.Substring(0, bar) == id || kv.Key.Substring(bar + 1) == id) ended.Add(kv.Key);
                }
                var foes = new List<string>();
                for (int i = 0; i < ended.Count; i++)
                {
                    string key = ended[i];
                    int bar = key.IndexOf('|');
                    string other = key.Substring(0, bar) == id ? key.Substring(bar + 1) : key.Substring(0, bar);
                    foes.Add(KingdomName(other));
                    _pairs[key] = new PairEvent { Day = day, War = false };
                }
                _dDestroyed++;
                Log.Info("Wojna i pokoj (E7/0): dzien " + day + " | " + k.Name + ": KROLESTWO ZNISZCZONE - gra konczy jego wojny bez zdarzenia pokoju (znane z pamieci sesji: "
                         + foes.Count + (foes.Count > 0 ? " [" + string.Join(", ", foes) + "]" : "") + "), KRONIKA WOJEN (CS) traci te pary");
            }
            catch (Exception e) { Stumble("OnKingdomDestroyed", e); }
        }

        private static void Event(IFaction a, IFaction b, bool war, string kind, string caller)
        {
            int day = (int)CampaignTime.Now.ToDays;
            string ia = a.StringId ?? "", ib = b.StringId ?? "";
            string key = string.CompareOrdinal(ia, ib) <= 0 ? ia + "|" + ib : ib + "|" + ia;
            PairEvent prev;
            bool had = _pairs.TryGetValue(key, out prev);
            var sb = new StringBuilder(220);
            sb.Append("Wojna i pokoj (E7/0): dzien ").Append(day).Append(" | ").Append(a.Name).Append(" - ").Append(b.Name).Append(": ")
              .Append(war ? "WOJNA" : "POKOJ").Append(" (przyczyna ").Append(kind);
            if (caller != null) sb.Append(", wola: ").Append(caller);
            sb.Append(") | dni od poprzedniego zdarzenia tej pary ");
            if (had) sb.Append(day - prev.Day).Append(" (").Append(prev.War ? "WOJNA" : "POKOJ").Append(')');
            else sb.Append("- (brak w pamieci sesji)");
            if (war && had && !prev.War && day - prev.Day < QuickWarDays)
            {
                _dQuick++; _sQuick++;
                _dQuickList.Add(a.Name + " - " + b.Name + " po " + (day - prev.Day) + " d (" + kind + ")");
                sb.Append(" - NOWA WOJNA < ").Append(QuickWarDays).Append(" DNI PO POKOJU");
            }
            _pairs[key] = new PairEvent { Day = day, War = war };
            if (war) _dWar++; else _dPeace++;
            Bump(_dCauses, (war ? "wojna " : "pokoj ") + kind);
            Log.Info(sb.ToString());
        }

        /// <summary>Kto wolal akcje gry: pierwsza ramka z nazwa za ostatnia ramka MakePeaceAction / DeclareWarAction (metody dynamiczne Harmony - bez nazwy,
        /// pomijane). Tylko przy przyczynie Default i tylko dla par krolestw (zdarzenia rzadkie).</summary>
        private static string Caller()
        {
            try
            {
                var st = new StackTrace(1, false);
                int n = st.FrameCount, last = -1;
                for (int i = 0; i < n; i++)
                {
                    var t = st.GetFrame(i)?.GetMethod()?.DeclaringType;
                    if (t == typeof(MakePeaceAction) || t == typeof(DeclareWarAction)) last = i;
                }
                for (int i = last + 1; i < n; i++)
                {
                    var m = st.GetFrame(i)?.GetMethod();
                    var t = m?.DeclaringType;
                    if (t == null) continue;
                    if (last < 0 && (t == typeof(WorldClock7) || t.Name.StartsWith("MbEvent", StringComparison.Ordinal)
                                     || t.Name == "CampaignEventDispatcher" || t.Name == "CampaignEvents")) continue;   // akcja niewidoczna - pomijamy rozsylanie zdarzenia
                    return t.FullName + "." + m.Name + " [" + t.Assembly.GetName().Name + "]";
                }
            }
            catch { }
            return "?";
        }

        private static string KingdomName(string id)
        {
            var all = Kingdom.All;
            for (int i = 0; i < all.Count; i++)
            {
                var o = all[i];
                if (o != null && o.StringId == id) return o.Name != null ? o.Name.ToString() : id;
            }
            return id;
        }

        private static void Bump(Dictionary<string, int> d, string k)
        {
            int v;
            d.TryGetValue(k, out v);
            d[k] = v + 1;
        }

        private static string Join(Dictionary<string, int> d)
        {
            var sb = new StringBuilder();
            foreach (var kv in d)
            {
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(kv.Key).Append(' ').Append(kv.Value);
            }
            return sb.ToString();
        }

        // ------------------------------------------------------------ turnieje

        internal static void OnTournamentStarted(Town town)
        {
            try
            {
                if (!On || town == null) return;
                _dStarted.Add(town.Name != null ? town.Name.ToString() : "?");
                _lastStartAt = CampaignTime.Now.ToDays;
            }
            catch (Exception e) { Stumble("OnTournamentStarted", e); }
        }

        // ------------------------------------------------------------ zbiorka (NightMarch / NightRest - tylko liczniki)

        /// <summary>LordNight: wynik godziny obozu dla czlonka armii niedoczepionego (member) albo wodza armii (leader) w polu.</summary>
        internal static void MusterHour(bool leader, bool member, byte kind)
        {
            if (!On) return;
            if (leader) { _lField++; if (kind == MSleep) _lSleep++; return; }
            if (!member) return;
            _mHours++;
            switch (kind)
            {
                case MSleep: _mSleep++; break;
                case MFlee: _mFlee++; break;
                case MChase: _mChase++; break;
                case MRelief: _mRelief++; break;
                default: _mAlarm++; break;
            }
        }

        /// <summary>Petla obozu swiata: partia w polu spi snem dlugu (AiDebtCamp) - czlonek armii niedoczepiony albo wodz armii.</summary>
        internal static void MusterDebt(TaleWorlds.CampaignSystem.Party.MobileParty mp)
        {
            if (!On || mp == null || mp.Army == null || mp.AttachedTo != null) return;
            if (mp.Army.LeaderParty == mp) { _lField++; _lSleep++; return; }
            _mHours++; _mSleep++; _mDebt++;
        }

        /// <summary>Ksiega snu AI: czlonek armii niedoczepiony ruszyl sie w minionej godzinie obozu bez wpisu powodu (woken - byl uspiony).</summary>
        internal static void MusterMoved(bool woken)
        {
            if (!On) return;
            _mMovedNoReason++;
            if (woken) _mMovedWoken++;
        }

        /// <summary>Swit ksiegi snu AI (po ostatniej godzinie obozu): linia nocy i zerowanie.</summary>
        internal static void MusterDawn(bool byReason)
        {
            try
            {
                if (!On) { ZeroNight(); return; }
                int go = _mFlee + _mChase + _mRelief;
                var sb = new StringBuilder(320);
                sb.Append("Zbiorka (E7/0): noc ").Append((int)CampaignTime.Now.ToDays).Append(" (oboz ").Append(NightRest.CampStart).Append(":00-").Append(NightRest.CampEnd)
                  .Append(":00) | czlonkowie armii w drodze do wodza w oknie obozu: partio-godzin ").Append(_mHours)
                  .Append(", spia ").Append(_mSleep).Append(" (").Append(_mHours > 0 ? (100.0 * _mSleep / _mHours).ToString("0.0", CultureInfo.InvariantCulture) : "-")
                  .Append("%; w tym sen dlugu ").Append(_mDebt).Append("), ida z powodem ").Append(go)
                  .Append(" (ucieczka ").Append(_mFlee).Append(", poscig ").Append(_mChase).Append(", odsiecz ").Append(_mRelief).Append("), alarm ").Append(_mAlarm)
                  .Append(", ida bez powodu ").Append(_mMovedNoReason).Append(" (ruch w godzinie obozu bez wpisu powodu; w tym uspieni ").Append(_mMovedWoken)
                  .Append(") | wodzowie armii obozuja ").Append(_lSleep).Append('/').Append(_lField).Append(" (partio-godziny w polu)");
                if (!byReason) sb.Append(" | regula powodow T10 wylaczona (AiNightMarchByReason) - czlonkowie armii ida za wodzem po staremu, licznik nie liczy");
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("MusterDawn", e); }
            ZeroNight();
        }

        // ------------------------------------------------------------ linie dobowe (po ksiedze pieniadza)

        internal static void Daily()
        {
            if (!On) { ZeroDay(); return; }
            var sw = Stopwatch.StartNew();
            // dzien linii = doba ksiegi (dzien gry - 1, jak "Pieniadz swiata"): tick dobowy biegnie na poczatku doby, liczniki sa z doby, ktora minela
            int day = (int)CampaignTime.Now.ToDays - 1;
            try
            {
                var sb = new StringBuilder(300);
                sb.Append("Wojna i pokoj (E7/0): dzien ").Append(day).Append(" | od poprzedniej linii: wojen ").Append(_dWar).Append(", pokojow ").Append(_dPeace)
                  .Append(" (krolestwa").Append(_dCauses.Count > 0 ? "; " + Join(_dCauses) : "").Append("), zdarzen z udzialem rodow spoza krolestw ").Append(_dOther)
                  .Append(_dOtherCauses.Count > 0 ? " (" + Join(_dOtherCauses) + ")" : "").Append(", krolestw zniszczonych ").Append(_dDestroyed)
                  .Append(" | par z nowa wojna < ").Append(QuickWarDays).Append(" dni po pokoju: ").Append(_dQuick)
                  .Append(_dQuickList.Count > 0 ? " (" + string.Join("; ", _dQuickList) + ")" : "").Append(", od startu sesji ").Append(_sQuick)
                  .Append(" | potkniecia ").Append(_stumbles);
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Daily(wojna)", e); }
            try
            {
                int active = 0;
                double youngest = -1;
                var tm = Campaign.Current != null ? Campaign.Current.TournamentManager : null;
                var towns = Town.AllTowns;
                if (tm != null && towns != null)
                {
                    for (int i = 0; i < towns.Count; i++)
                    {
                        var t = towns[i];
                        if (t == null) continue;
                        var g = tm.GetTournamentGame(t);
                        if (g == null) continue;
                        active++;
                        double age = g.CreationTime.ElapsedDaysUntilNow;
                        if (youngest < 0 || age < youngest) youngest = age;
                    }
                }
                // ostatni start: z tej sesji albo najmlodszy turniej w toku (zapis wczytany w polowie turnieju)
                double now = CampaignTime.Now.ToDays, last = _lastStartAt;
                if (youngest >= 0 && now - youngest > last) last = now - youngest;
                var ct = CampaignTime.Now - CampaignTime.Days(1f);   // tydzien pory doby linii
                var sb = new StringBuilder(260);
                sb.Append("Turnieje (E7/0): dzien ").Append(day).Append(", tydzien pory ").Append(ct.GetWeekOfSeason).Append(" (0-").Append(CampaignTime.WeeksInSeason - 1)
                  .Append(", pora ").Append(ct.GetSeasonOfYear).Append(") | rozpoczete od poprzedniej linii ").Append(_dStarted.Count);
                if (_dStarted.Count > 0) sb.Append(" [").Append(string.Join(", ", _dStarted)).Append(']');
                sb.Append(", w toku ").Append(active).Append(", dni od ostatniego startu ")
                  .Append(last >= 0 ? (now - last).ToString("0.0", CultureInfo.InvariantCulture) : "- (brak startu w tej sesji i turnieju w toku)");
                sb.Append(" | koszt E7/0 ").Append(sw.Elapsed.TotalMilliseconds.ToString("0.000", CultureInfo.InvariantCulture)).Append(" ms/dobe");
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Daily(turnieje)", e); }
            ZeroDay();
        }
    }
}
