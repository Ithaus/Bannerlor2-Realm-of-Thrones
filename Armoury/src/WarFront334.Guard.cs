using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Threading;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// 334c - STRAZNIK POZYCJI PARTII (klucz PartyPositionGuard334c; kazda kampania, nie tylko kanonu - to naprawa wywrotki, nie regula balansu).
    /// DOWOD (test 334b, doba 78, dzien 109604, 15:37:43): IndexOutOfRangeException w DefaultMapWeatherModel.GetWeatherEventInPosition <- NavalDLCMapWeatherModel
    /// .GetWeatherEffectOnTerrainForPosition <- MobileParty.IsBaseSpeedCacheInvalid <- CalculateSpeedForPartyUnified (ParallelTickMovingParties) - 26 s po
    /// "FABULA: BlackwaterSiegeEvent WYSTARTOWALO" (dzien 767). GetWeatherEventInPosition liczy indeks (int)(x / komorka) bez obciecia - wyjatek daje tylko (int)NaN
    /// = int.MinValue, czyli pozycja partii NaN.
    /// PRZYCZYNA (kod gry MobileParty.FinishNavigationTransitionInternal): po zejsciu na lad / wejsciu na statki _position = koniec przejscia (wlasny albo wodza armii
    /// dla przypietych), gdy partia nie stoi w osadzie. Setter MobileParty.CurrentSettlement = null (LeaveSettlementAction) ZAWSZE ustawia koniec przejscia na
    /// CampaignVec2.Invalid (NaN), a przejscie zaczete w osadzie (InitializeNavigationTransitionParallel) konca w ogole nie zapisuje. Kazde wyjscie z osady w trakcie
    /// przejscia bez CancelNavigationTransition konczy sie wiec pozycja NaN (po recenzji 334c: szersza klasa przypadkow niz "przejscie zaczete w osadzie"). Tak robi ROT BlackwaterSiegeEvent.SetupSiegeAttackers (Stannis i lordowie Smoczej Skaly - wyspa,
    /// czesto w trakcie wyplywania) i DefendSiegeTarget (Joffrey, Lancel).
    /// ZMIANA:
    ///  (1) zrodlo: prefiks ROT BlackwaterSiegeEvent.SetupSiegeAttackers - anuluje trwajace przejscia Stannisa i partii wojennych rodow Smoczej Skaly PRZED teleportem
    ///      (wzor BookHoldings.Teleport); postfiks - czlonkowie SiegeArmy: przejscie anulowane jeszcze raz, pozycja NaN naprawiona; prefiks DefendSiegeTarget - to samo
    ///      dla Joffreya i Lancela. Linia "Blackwater (334c)".
    ///  (2) straznik ogolny (watek glowny, RealTick): PREFIKS MobileParty.FinishNavigationTransitionInternal (po recenzji 334c) - gra wzielaby zly koniec przejscia:
    ///      partia bez wodza -> przejscie anulowane, metoda gry pominieta (partia zostaje na swojej, poprawnej pozycji i w swoim stanie lad/morze; gra wystartuje
    ///      przejscie od nowa, gdy droga tego wymaga); przypiety w armii, wodz z poprawna pozycja -> koniec przejscia wodza = pozycja wodza (gra sama ustawi dobra
    ///      _position przed ComputePath i zdarzeniem). POSTFIKS (zapas) - pozycja NaN po przejsciu -> pozycja wodza, brama osady, w ktorej stoi, brama osady domowej,
    ///      pierwsza twierdza frakcji; potem nowa sciezka (ComputePath); linia "NaN po przejsciu".
    ///  (3) siatka bezpieczenstwa: prefiks DefaultMapWeatherModel.GetWeatherEventInPosition - pozycja poza siatka pogody (NaN, ujemna, za mapa) -> pogoda Clear zamiast
    ///      wyjatku (watek roboczy: tylko licznik Interlocked i kolejka; partia z MapRoads.CurrentWriter), naprawa partii w nastepnej klatce na watku glownym
    ///      (TickEvent) i linia "potkniecie 334c pogoda" z partia i pozycja. Partia nieznana (pogoda pytana spoza liczenia predkosci) - przeglad wszystkich partii.
    ///  Raz na dobe przeglad wszystkich partii (pozycja NaN) i linia "Pozycje partii (334c): dzien N". Nic nie tworzy - tylko pozycja i stan przejscia.
    /// </summary>
    internal static class NavGuard334
    {
        internal const string P = "Pozycje partii (334c): ";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        internal static bool On { get { var s = Settings.Current; return s != null && s.PartyPositionGuard334c; } }

        // ---- latki (raz na proces)
        private static bool _hooksTried;
        private static string _wFinish = "BRAK", _wWeather = "BRAK", _wBw = "BRAK";
        private static FieldInfo _fSiegeArmy;

        // ---- siatka pogody (raz na kampanie; watki robocze tylko czytaja)
        private static volatile bool _gridOk;
        private static float _cellX, _cellY;
        private static int _dim;

        // ---- watki robocze -> watek glowny
        private struct Hit { public MobileParty Party; public float X, Y; }
        private static readonly ConcurrentQueue<Hit> _hits = new ConcurrentQueue<Hit>();
        private static int _queued;                 // w kolejce (Interlocked) - najwyzej QueueCap
        private const int QueueCap = 256;
        private static int _dWeather, _sWeather;    // trafienia pogody poza siatka (Interlocked)
        private static int _dWeatherUnknown;        // ... bez partii (pogoda pytana spoza liczenia predkosci)

        // ---- watek glowny
        private static int _dNanFinish, _dNanFixed, _dQueueFixed, _dScanFixed, _dDeferred, _sNan, _stumbles, _dStumbles;
        private static int _dBwPre, _dBwPost, _dBwFixed, _dBwDefend;
        private static int _dLines;
        private static int _dPreSkip, _dPreLead;          // prefiks konca przejscia: pominiete + anulowane / koniec wodza poprawiony
        private static double _lastUnknownScanH = -1e9;   // przeglad partii po trafieniu pogody bez partii - najwyzej raz na godzine gry
        private static MethodInfo _mComputePath, _mSetEnd;
        [ThreadStatic] private static bool _probe;        // T7: proba siatki na watku glownym - bez licznikow i kolejki
        private static int _probeHits;
        private const int LinesPerDay = 20;
        private static readonly List<string> _dNames = new List<string>();
        private static readonly HashSet<string> _errSites = new HashSet<string>(StringComparer.Ordinal);

        internal static string WiredText
        {
            get
            {
                bool ok = _wFinish == "ok" && _wWeather == "ok" && (_wBw == "ok" || _wBw.StartsWith("bez ROT", StringComparison.Ordinal));
                return ok ? "ok" : "BRAK[przejscie " + _wFinish + "; pogoda " + _wWeather + "; Blackwater " + _wBw + "]";
            }
        }

        private static int Today { get { try { return (int)CampaignTime.Now.ToDays; } catch { return -1; } } }

        /// <summary>TYLKO watek glowny (licznik i HashSet bez blokad) - nie wolac z WeatherPrefix ani innego kodu watkow roboczych.</summary>
        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _dStumbles++;
            try { if (_errSites.Add(where)) Log.Error("NavGuard334." + where + " (potkniecie " + _stumbles + ")", e); } catch { }
        }

        private static string Nm(MobileParty mp) { try { return mp != null && mp.Name != null ? mp.Name.ToString() : "-"; } catch { return "?"; } }
        private static string Nm(IFaction f) { try { return f != null && f.Name != null ? f.Name.ToString() : "-"; } catch { return "?"; } }
        private static string Nm(Settlement st) { try { return st != null && st.Name != null ? st.Name.ToString() : "-"; } catch { return "?"; } }
        private static string V(Vec2 v) { return "(" + v.x.ToString("0.0", Inv) + "; " + v.y.ToString("0.0", Inv) + ")"; }

        private static bool Ok(Vec2 v) { return !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsInfinity(v.x) && !float.IsInfinity(v.y); }
        private static bool Ok(MobileParty mp) { try { return mp != null && Ok(mp.Position.ToVec2()); } catch { return false; } }

        // ================================================================ start sesji

        internal static void SessionStart()
        {
            _gridOk = false;
            Hit h; while (_hits.TryDequeue(out h)) { }
            Interlocked.Exchange(ref _queued, 0);
            Interlocked.Exchange(ref _dWeather, 0); Interlocked.Exchange(ref _sWeather, 0); Interlocked.Exchange(ref _dWeatherUnknown, 0);
            _sNan = 0;
            ZeroDay();
            EnsureHooks();
            InitGrid();
        }

        private static void InitGrid()
        {
            try
            {
                var c = Campaign.Current;
                if (c == null || c.MapSceneWrapper == null) return;
                var ts = c.MapSceneWrapper.GetTerrainSize();
                int dim = c.DefaultWeatherNodeDimension;
                if (dim <= 0 || !(ts.X > 0f) || !(ts.Y > 0f)) return;
                _cellX = ts.X / dim; _cellY = ts.Y / dim; _dim = dim;
                _gridOk = true;
            }
            catch (Exception e) { Stumble("InitGrid", e); }
        }

        private static void EnsureHooks()
        {
            if (_hooksTried) return;
            _hooksTried = true;
            var h = new Harmony("com.jeff.armoury.navguard334c");
            try
            {
                _mComputePath = AccessTools.DeclaredMethod(typeof(MobileParty), "ComputePath", new[] { typeof(CampaignVec2), typeof(MobileParty.NavigationType), typeof(bool) });
                _mSetEnd = AccessTools.PropertySetter(typeof(MobileParty), "EndPositionForNavigationTransition");
            }
            catch (Exception e) { Stumble("EnsureHooks.methods", e); }
            try
            {
                var m = AccessTools.Method(typeof(MobileParty), "FinishNavigationTransitionInternal", Type.EmptyTypes);
                if (m == null) _wFinish = "BRAK (MobileParty.FinishNavigationTransitionInternal)";
                else
                {
                    h.Patch(m, prefix: new HarmonyMethod(typeof(NavGuard334), nameof(FinishPrefix)), postfix: new HarmonyMethod(typeof(NavGuard334), nameof(FinishPostfix)));
                    _wFinish = "ok";
                }
            }
            catch (Exception e) { Stumble("EnsureHooks.finish", e); _wFinish = "BLAD (" + e.GetType().Name + ")"; }
            try
            {
                var m = AccessTools.DeclaredMethod(typeof(DefaultMapWeatherModel), "GetWeatherEventInPosition", new[] { typeof(Vec2) });
                if (m == null) _wWeather = "BRAK (DefaultMapWeatherModel.GetWeatherEventInPosition)";
                else
                {
                    h.Patch(m, prefix: new HarmonyMethod(typeof(NavGuard334), nameof(WeatherPrefix)) { priority = Priority.First });
                    _wWeather = "ok";
                }
            }
            catch (Exception e) { Stumble("EnsureHooks.weather", e); _wWeather = "BLAD (" + e.GetType().Name + ")"; }
            try
            {
                var t = AccessTools.TypeByName("ROT.Events.BlackwaterSiegeEvent");
                if (t == null) _wBw = "bez ROT (brak ROT.Events.BlackwaterSiegeEvent)";
                else
                {
                    var ms = AccessTools.DeclaredMethod(t, "SetupSiegeAttackers", Type.EmptyTypes);
                    var md = AccessTools.DeclaredMethod(t, "DefendSiegeTarget", new[] { typeof(Hero) });
                    _fSiegeArmy = AccessTools.Field(t, "SiegeArmy");
                    if (ms == null || md == null) _wBw = "BRAK (SetupSiegeAttackers " + (ms != null) + ", DefendSiegeTarget " + (md != null) + ")";
                    else
                    {
                        h.Patch(ms, prefix: new HarmonyMethod(typeof(NavGuard334), nameof(BwSetupPrefix)), postfix: new HarmonyMethod(typeof(NavGuard334), nameof(BwSetupPostfix)));
                        h.Patch(md, prefix: new HarmonyMethod(typeof(NavGuard334), nameof(BwDefendPrefix)));
                        _wBw = "ok";
                    }
                }
            }
            catch (Exception e) { Stumble("EnsureHooks.blackwater", e); _wBw = "BLAD (" + e.GetType().Name + ")"; }
            try
            {
                Log.Info(P + "start | klucz " + (On ? "TAK" : "NIE") + " | latki: przejscie " + _wFinish + ", pogoda " + _wWeather + ", Blackwater " + _wBw
                         + " | naprawa: wodz armii, brama osady, brama osady domowej, pierwsza twierdza frakcji.");
            }
            catch { }
        }

        // ================================================================ (3) pogoda - watki robocze

        /// <summary>Prefiks DefaultMapWeatherModel.GetWeatherEventInPosition: pozycja poza siatka pogody - Clear zamiast IndexOutOfRangeException. Watek roboczy: bez
        /// logu i bez zmian stanu gry - licznik i kolejka do watku glownego. Nie rzuca.</summary>
        public static bool WeatherPrefix(Vec2 pos, ref MapWeatherModel.WeatherEvent __result)
        {
            try
            {
                if (!_gridOk) return true;
                var s = Settings.Current;
                if (s == null || !s.PartyPositionGuard334c) return true;
                float cx = _cellX, cy = _cellY; int dim = _dim;
                // dokladnie warunek gry (po recenzji 334c): idx = (int)(y/c)*dim + (int)(x/c), wyjatek tylko przy idx < 0 albo idx >= dim*dim; (int)NaN / (int)Inf = int.MinValue
                int ix = (int)(pos.x / cx), iy = (int)(pos.y / cy);
                long idx = (long)iy * dim + ix;
                if (idx >= 0 && idx < (long)dim * dim) return true;
                __result = MapWeatherModel.WeatherEvent.Clear;
                if (_probe) { _probeHits++; return false; }
                Interlocked.Increment(ref _dWeather);
                Interlocked.Increment(ref _sWeather);
                var mp = MapRoads.CurrentWriter;
                if (mp == null) Interlocked.Increment(ref _dWeatherUnknown);
                if (Interlocked.Increment(ref _queued) <= QueueCap) _hits.Enqueue(new Hit { Party = mp, X = pos.x, Y = pos.y });
                else Interlocked.Decrement(ref _queued);
                return false;
            }
            catch { return true; }
        }

        /// <summary>TickEvent (watek glowny): naprawa partii z kolejki pogody; trafienia bez partii - przeglad wszystkich partii. Pusta kolejka - jedno porownanie.</summary>
        internal static void Tick(float dt)
        {
            if (Volatile.Read(ref _queued) == 0) return;
            try
            {
                bool scan = false;
                var seen = new HashSet<MobileParty>();
                Hit h;
                while (_hits.TryDequeue(out h))
                {
                    Interlocked.Decrement(ref _queued);
                    if (h.Party == null) { scan = true; continue; }
                    if (!h.Party.IsActive || !seen.Add(h.Party)) continue;
                    string fix = Ok(h.Party) ? "pozycja juz poprawna (gra sama ja ustawila)" : Repair(h.Party);
                    if (!fix.StartsWith("pozycja juz", StringComparison.Ordinal)) _dQueueFixed++;
                    Note(h.Party);
                    Say("potkniecie 334c pogoda", h.Party, new Vec2(h.X, h.Y), fix);
                }
                double nowH = -1;
                try { nowH = CampaignTime.Now.ToHours; } catch { }
                if (scan && nowH - _lastUnknownScanH >= 1.0)
                {
                    _lastUnknownScanH = nowH;
                    int n = ScanAll("potkniecie 334c pogoda (partia nieznana)");
                    if (n == 0) Say("potkniecie 334c pogoda (partia nieznana)", null, new Vec2(float.NaN, float.NaN), "przeglad partii: zadnej z pozycja NaN (pozycja spoza partii - np. model wolany wprost)");
                }
            }
            catch (Exception e) { Stumble("Tick", e); }
        }

        // ================================================================ (2) koniec przejscia - watek glowny

        public struct EndPair { public Vec2 Own, Lead; public bool Attached; }

        /// <summary>Prefiks MobileParty.FinishNavigationTransitionInternal (po recenzji 334c, naprawa przed gra): gdy gra wzielaby zly koniec przejscia (partia poza
        /// osada, koniec Invalid) - partia bez wodza: przejscie anulowane i metoda gry pominieta (false); przypiety w armii: koniec przejscia wodza = pozycja wodza.
        /// Nie rzuca; przy bledzie gra dziala jak dotad (postfiks naprawia).</summary>
        public static bool FinishPrefix(MobileParty __instance, out EndPair __state)
        {
            __state = default(EndPair);
            try
            {
                var mp = __instance;
                if (mp == null) return true;
                __state.Own = mp.EndPositionForNavigationTransition.ToVec2();
                __state.Attached = mp.AttachedTo != null;
                var a = mp.Army;
                var lp = a != null ? a.LeaderParty : null;
                __state.Lead = lp != null ? lp.EndPositionForNavigationTransition.ToVec2() : new Vec2(float.NaN, float.NaN);
                if (!On || mp.CurrentSettlement != null) return true;
                if (a != null && mp.AttachedTo != null)
                {
                    // gra wezmie koniec przejscia wodza (Army.LeaderParty); wodz konczy swoje przejscie przed przypietymi - jego pozycja juz ustawiona
                    if (lp == null || lp.EndPositionForNavigationTransition.IsValid() || !Ok(lp) || _mSetEnd == null) return true;
                    _mSetEnd.Invoke(lp, new object[] { lp.Position });
                    _dPreLead++;
                    Note(mp);
                    Say("koniec przejscia wodza poprawiony", mp, lp.Position.ToVec2(), "przypiety dostanie pozycje wodza " + Nm(lp) + " (koniec przejscia wodza byl Invalid)");
                    return true;
                }
                if (mp.EndPositionForNavigationTransition.IsValid() || !Ok(mp)) return true;
                // koniec Invalid poza osada (wyjscie z osady w trakcie przejscia): przejscie anulowane, partia zostaje na swojej pozycji i w swoim stanie lad/morze
                mp.CancelNavigationTransition();
                _dPreSkip++;
                Note(mp);
                Say("przejscie bez konca anulowane", mp, mp.Position.ToVec2(), "metoda gry pominieta, partia zostaje na swojej pozycji (morze " + (mp.IsCurrentlyAtSea ? "TAK" : "NIE") + ")");
                return false;
            }
            catch (Exception e) { Stumble("FinishPrefix", e); return true; }
        }

        /// <summary>Postfiks MobileParty.FinishNavigationTransitionInternal: pozycja NaN po przejsciu -> naprawa. Przypiety z wodzem bez pozycji - czeka na postfiks wodza
        /// (wodz konczy przejscie po swoich przypietych i naprawia ich razem z soba). Nie rzuca.</summary>
        public static void FinishPostfix(MobileParty __instance, EndPair __state)
        {
            try
            {
                var mp = __instance;
                if (mp == null || !On || Ok(mp)) return;
                _dNanFinish++; _sNan++;
                Note(mp);
                var at = mp.AttachedTo;
                if (at != null && !Ok(at))
                {
                    _dDeferred++;
                    Say("NaN po przejsciu", mp, mp.Position.ToVec2(), "czeka na wodza " + Nm(at) + " (wodz bez pozycji - naprawi przypietych) | koniec przejscia wlasny " + V(__state.Own) + ", wodza " + V(__state.Lead));
                    return;
                }
                string fix = Repair(mp);
                _dNanFixed++;
                Say("NaN po przejsciu", mp, new Vec2(float.NaN, float.NaN), fix + " | koniec przejscia wlasny " + V(__state.Own) + ", wodza " + V(__state.Lead) + (__state.Attached ? ", przypiety" : ""));
            }
            catch (Exception e) { Stumble("FinishPostfix", e); }
        }

        // ================================================================ naprawa

        /// <summary>Pozycja dla partii bez pozycji: wodz (przypiety albo armia), brama osady, w ktorej stoi, brama osady domowej, pierwsza twierdza frakcji, pierwsze
        /// miasto. Anuluje trwajace przejscie. Przypieci do naprawianej partii dostaja te sama pozycje. Zwraca opis (linia).</summary>
        private static string Repair(MobileParty mp)
        {
            try
            {
                if (mp.IsTransitionInProgress) mp.CancelNavigationTransition();
                MobileParty lead = mp.AttachedTo;
                if (lead == null && mp.Army != null && mp.Army.LeaderParty != mp) lead = mp.Army.LeaderParty;
                string how;
                if (lead != null && Ok(lead))
                {
                    mp.Position = lead.Position;   // po recenzji 334c: najpierw pozycja - setter IsCurrentlyAtSea odpala zdarzenie gry
                    if (mp.IsCurrentlyAtSea != lead.IsCurrentlyAtSea) mp.IsCurrentlyAtSea = lead.IsCurrentlyAtSea;
                    how = "pozycja wodza " + Nm(lead) + " " + V(lead.Position.ToVec2()) + (lead.IsCurrentlyAtSea ? " (morze)" : "");
                }
                else
                {
                    Settlement st = mp.CurrentSettlement, home = null;
                    string kind = "brama osady, w ktorej stoi";
                    if (st == null)
                    {
                        try { home = mp.HomeSettlement; } catch { home = null; }
                        if (home == null && mp.LeaderHero != null) home = mp.LeaderHero.HomeSettlement;
                        st = home; kind = "brama osady domowej";
                    }
                    if (st == null) { st = FirstFort(mp.MapFaction); kind = "brama pierwszej twierdzy frakcji"; }
                    if (st == null) { st = FirstFort(null); kind = "brama pierwszego miasta"; }
                    if (st == null) return "BRAK celu naprawy (brak osad)";
                    mp.Position = st.GatePosition;
                    if (mp.IsCurrentlyAtSea) mp.IsCurrentlyAtSea = false;
                    how = kind + " " + Nm(st) + " " + V(st.GatePosition.ToVec2());
                    // po recenzji 334c: flota bez kotwicy (gra zresetowala ja przy wejsciu na statki) - kotwica w porcie osady albo przy bramie
                    try
                    {
                        if (mp.Ships != null && mp.Ships.Count > 0 && mp.Anchor != null && !mp.Anchor.IsValid)
                        {
                            if (st.HasPort) mp.Anchor.SetSettlement(st); else mp.Anchor.SetPosition(mp.Position);
                            how += ", kotwica " + (st.HasPort ? "w porcie" : "przy bramie");
                        }
                    }
                    catch (Exception e) { Stumble("Repair.anchor", e); }
                }
                // przypieci (wodz naprawiany po swoich przypietych - FinishPostfix ich odlozyl)
                try
                {
                    var ap = mp.AttachedParties;
                    if (ap != null)
                        for (int i = 0; i < ap.Count; i++)
                        {
                            var a = ap[i];
                            if (a == null || Ok(a)) continue;
                            if (a.IsTransitionInProgress) a.CancelNavigationTransition();
                            a.Position = mp.Position;
                            if (a.IsCurrentlyAtSea != mp.IsCurrentlyAtSea) a.IsCurrentlyAtSea = mp.IsCurrentlyAtSea;
                            _dNanFixed++;
                            how += ", przypiety " + Nm(a) + " razem";
                        }
                }
                catch (Exception e) { Stumble("Repair.attached", e); }
                // po recenzji 334c: sciezka liczona od NaN (ComputePath w Finish) - nowa od poprawnej pozycji
                try
                {
                    if (_mComputePath != null && mp.MoveTargetPoint.IsValid())
                    {
                        _mComputePath.Invoke(mp, new object[] { mp.MoveTargetPoint, mp.NavigationCapability, true });
                        how += ", nowa sciezka";
                    }
                }
                catch (Exception e) { Stumble("Repair.path", e); }
                return how;
            }
            catch (Exception e) { Stumble("Repair", e); return "BLAD naprawy (" + e.GetType().Name + ")"; }
        }

        private static Settlement FirstFort(IFaction f)
        {
            try
            {
                foreach (var t in Town.AllFiefs)
                {
                    var st = t != null ? t.Settlement : null;
                    if (st == null) continue;
                    if (f == null ? st.IsTown : st.MapFaction == f) return st;
                }
            }
            catch { }
            return null;
        }

        /// <summary>Przeglad wszystkich partii: pozycja NaN -> naprawa. Zwraca liczbe naprawionych.</summary>
        private static int ScanAll(string why)
        {
            int n = 0;
            try
            {
                var all = MobileParty.All;
                for (int i = 0; i < all.Count; i++)
                {
                    var mp = all[i];
                    if (mp == null || !mp.IsActive || Ok(mp)) continue;
                    if (mp.AttachedTo != null && Ok(mp.AttachedTo) == false) continue;   // wodz bez pozycji - naprawi przypietych
                    string fix = Repair(mp);
                    n++; _dScanFixed++;
                    Note(mp);
                    Say(why, mp, new Vec2(float.NaN, float.NaN), fix);
                }
            }
            catch (Exception e) { Stumble("ScanAll", e); }
            return n;
        }

        private static void Note(MobileParty mp)
        {
            if (_dNames.Count < 8) { var n = Nm(mp); if (!_dNames.Contains(n)) _dNames.Add(n); }
        }

        private static void Say(string what, MobileParty mp, Vec2 pos, string fix)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.LogEnabled || _dLines >= LinesPerDay) return;
                _dLines++;
                string party = "-";
                if (mp != null)
                {
                    var a = mp.Army;
                    party = Nm(mp) + " (" + mp.StringId + ", " + Nm(mp.MapFaction) + ", armia " + (a == null ? "-" : a.LeaderParty == mp ? "wodz" : "czlonek " + Nm(a.LeaderParty))
                            + (mp.AttachedTo != null ? ", przypiety" : "") + ", morze " + (mp.IsCurrentlyAtSea ? "TAK" : "NIE") + ", przejscie " + (mp.IsTransitionInProgress ? "TAK" : "NIE")
                            + ", osada " + Nm(mp.CurrentSettlement) + ")";
                }
                Log.Info(P + what + " | dzien " + Today + " | partia " + party + ", pozycja " + V(pos) + " -> " + fix + ".");
            }
            catch { }
        }

        // ================================================================ (1) Blackwater (ROT)

        private static Kingdom Dragonstone()
        {
            try { foreach (var k in Kingdom.All) if (k != null && k.StringId == WarFront334.DragonstoneId) return k; } catch { }
            return null;
        }

        private static Hero RotLord(string name)
        {
            try
            {
                var t = AccessTools.TypeByName("ROT.Misc.ROTLords");
                var pi = t != null ? AccessTools.Property(t, name) : null;
                return pi != null ? pi.GetValue(null, null) as Hero : null;
            }
            catch { return null; }
        }

        private static Settlement KingsLanding()
        {
            try
            {
                var t = AccessTools.TypeByName("ROT.Misc.ROTSettlements");
                var pi = t != null ? AccessTools.Property(t, "KingsLanding") : null;
                return pi != null ? pi.GetValue(null, null) as Settlement : null;
            }
            catch { return null; }
        }

        /// <summary>Druzyna lorda, u ktorego gracz sluzy w ROT (ROT przenosi wtedy tez MainParty); null - gracz nie sluzy albo brak pol ROT.</summary>
        private static MobileParty EnlistedLordParty()
        {
            try
            {
                var sub = AccessTools.TypeByName("ROT.SubModule");
                var fb = sub != null ? AccessTools.Field(sub, "EnlistmentBehavior") : null;
                var beh = fb != null ? fb.GetValue(null) : null;
                if (beh == null) return null;
                var fi = AccessTools.Field(beh.GetType(), "IsEnlisted");
                var fh = AccessTools.Field(beh.GetType(), "EnlistedHero");
                if (fi == null || fh == null || !(fi.GetValue(beh) is bool b) || !b) return null;
                var h = fh.GetValue(beh) as Hero;
                return h != null ? h.PartyBelongedTo : null;
            }
            catch { return null; }
        }

        /// <summary>Ten sam filtr, ktorym ROT SetupSiegeAttackers wybiera partie Smoczej Skaly do przeniesienia.</summary>
        private static bool RotMoves(MobileParty mp, MobileParty siegeLeader)
        {
            return mp != null && !mp.IsMainParty && mp != siegeLeader && mp.IsActive && mp.IsLordParty && mp.LeaderHero != null && mp.MapEvent == null && mp.SiegeEvent == null;
        }

        private static bool CancelIf(MobileParty mp, List<string> names)
        {
            if (mp == null || !mp.IsTransitionInProgress) return false;
            mp.CancelNavigationTransition();
            if (names.Count < 10) names.Add(Nm(mp));
            return true;
        }

        /// <summary>Prefiks ROT BlackwaterSiegeEvent.SetupSiegeAttackers: przejscia Stannisa i partii wojennych rodow Smoczej Skaly anulowane PRZED teleportem. Nie rzuca.</summary>
        public static void BwSetupPrefix()
        {
            if (!On) return;
            try
            {
                var names = new List<string>();
                int n = 0, seen = 0;
                var stannis = RotLord("StannisBaratheon");
                var sp = stannis != null ? stannis.PartyBelongedTo : null;
                var kl = KingsLanding();
                // po recenzji 334c: warunek ROT (flag) - Stannis juz oblega Krolewska Przystan -> ROT nikogo nie przenosi, nic nie anulujemy
                bool moves = sp != null && (sp.SiegeEvent == null || kl == null || sp.SiegeEvent != kl.SiegeEvent);
                var k = Dragonstone();
                var enl = EnlistedLordParty();
                if (moves)
                {
                    if (CancelIf(sp, names)) n++;
                    if (k != null)
                        foreach (var cl in k.Clans)
                        {
                            if (cl == null || cl.WarPartyComponents == null) continue;
                            foreach (var wp in cl.WarPartyComponents)
                            {
                                var mp = wp != null ? wp.MobileParty : null;
                                if (!RotMoves(mp, sp)) continue;   // ten sam filtr co ROT - partii, ktorych ROT nie przenosi, nie ruszamy
                                seen++;
                                if (CancelIf(mp, names)) n++;
                                if (enl != null && mp == enl && CancelIf(MobileParty.MainParty, names)) n++;   // gracz w sluzbie - ROT przenosi tez MainParty
                            }
                        }
                }
                _dBwPre += n;
                var s = Settings.Current;
                if (s != null && s.LogEnabled)
                    Log.Info(P + "Blackwater (334c): anulowane przejscia przed teleportem " + n + (names.Count > 0 ? " (partie " + string.Join(", ", names.ToArray()) + ")" : "")
                             + " | dzien " + Today + " | Stannis " + (sp != null ? Nm(sp) : "bez druzyny") + (moves ? "" : " (juz oblega - ROT bez teleportu)") + ", partii Smoczej Skaly " + seen
                             + (k == null ? " (brak krolestwa dragonstone)" : "") + ".");
            }
            catch (Exception e) { Stumble("BwSetupPrefix", e); }
        }

        /// <summary>Postfiks SetupSiegeAttackers: czlonkowie SiegeArmy (i wodz) - przejscie anulowane, pozycja NaN naprawiona. Nie rzuca.</summary>
        public static void BwSetupPostfix(object __instance)
        {
            if (!On) return;
            try
            {
                var army = _fSiegeArmy != null && __instance != null ? _fSiegeArmy.GetValue(__instance) as Army : null;
                if (army == null) return;
                var names = new List<string>();
                int n = 0, fixd = 0, parts = 0;
                var lp = army.LeaderParty;
                var list = new List<MobileParty>();
                if (lp != null) list.Add(lp);
                foreach (var mp in army.Parties) if (mp != null && mp != lp) list.Add(mp);
                foreach (var mp in list)
                {
                    parts++;
                    if (CancelIf(mp, names)) n++;
                    if (!Ok(mp)) { Repair(mp); fixd++; Note(mp); }
                }
                _dBwPost += n; _dBwFixed += fixd;
                var s = Settings.Current;
                if (s != null && s.LogEnabled)
                    Log.Info(P + "Blackwater (334c): po teleporcie | dzien " + Today + " | armia " + Nm(lp) + ", partii " + parts + ", anulowane przejscia " + n
                             + (names.Count > 0 ? " (" + string.Join(", ", names.ToArray()) + ")" : "") + ", naprawione pozycje " + fixd + ".");
            }
            catch (Exception e) { Stumble("BwSetupPostfix", e); }
        }

        /// <summary>Prefiks ROT BlackwaterSiegeEvent.DefendSiegeTarget(Hero): przejscie druzyny obroncy anulowane przed teleportem do Krolewskiej Przystani. Nie rzuca.</summary>
        public static void BwDefendPrefix(Hero hero)
        {
            if (!On || hero == null) return;
            try
            {
                // po recenzji 334c: warunki ROT DefendSiegeTarget / CanDefendSiegeTarget - gdy ROT wyjdzie od razu, rejsu nie przerywamy
                var kl = KingsLanding();
                var cs = hero.CurrentSettlement;
                var pb = hero.PartyBelongedTo;
                if (cs != null && (cs == kl || cs.IsUnderRaid || cs.IsUnderSiege)) return;
                if (hero.IsPrisoner || pb == null || pb.MapEvent != null || pb.LeaderHero != hero || !(hero.MapFaction is Kingdom)) return;
                var names = new List<string>();
                var enl = EnlistedLordParty();
                if (enl != null && enl == pb && CancelIf(MobileParty.MainParty, names)) _dBwDefend++;
                if (CancelIf(pb, names))
                {
                    _dBwDefend++;
                    var s = Settings.Current;
                    if (s != null && s.LogEnabled) Log.Info(P + "Blackwater (334c): obronca " + Nm(hero.PartyBelongedTo) + " - przejscie anulowane przed teleportem | dzien " + Today + ".");
                }
            }
            catch (Exception e) { Stumble("BwDefendPrefix", e); }
        }

        // ================================================================ T7 (autotest): proba siatki bezpieczenstwa

        /// <summary>T7 (watek glowny, raz): model pogody gry (NavalDLC -> Default) z pozycja NaN - bez wyjatku i z trafieniem prefiksu. Trafienia proby nie wchodza do
        /// licznikow doby (B334-23) ani do kolejki. ok - brak wyjatku i 2 trafienia; opis do linii testu.</summary>
        internal static bool Probe(out string detail)
        {
            detail = "";
            if (!On) { detail = "klucz NIE"; return false; }
            if (!_gridOk) { detail = "siatka BRAK"; return false; }
            int before = _probeHits;
            string err = null;
            _probe = true;
            try
            {
                var m = Campaign.Current.Models.MapWeatherModel;
                var nan = new Vec2(float.NaN, float.NaN);
                m.GetWeatherEffectOnTerrainForPosition(nan);
                m.GetWeatherEventInPosition(nan);
            }
            catch (Exception e) { err = e.GetType().Name; }
            finally { _probe = false; }
            int hits = _probeHits - before;
            detail = "model " + Campaign.Current.Models.MapWeatherModel.GetType().Name + ", trafienia prefiksu " + hits + " z 2" + (err != null ? ", WYJATEK " + err : ", bez wyjatku");
            return err == null && hits == 2;
        }

        // ================================================================ linia doby

        private static void ZeroDay()
        {
            _dNanFinish = _dNanFixed = _dQueueFixed = _dScanFixed = _dDeferred = _dStumbles = 0;
            _dBwPre = _dBwPost = _dBwFixed = _dBwDefend = 0;
            _dPreSkip = _dPreLead = 0;
            _dLines = 0;
            _dNames.Clear();
        }

        /// <summary>DailyTickEvent: przeglad wszystkich partii (pozycja NaN) i linia doby. Nie rzuca.</summary>
        internal static void Daily()
        {
            try
            {
                if (Campaign.Current == null) return;
                if (!_gridOk) InitGrid();
                int scan = On ? ScanAll("NaN w przegladzie doby") : 0;
                int w = Interlocked.Exchange(ref _dWeather, 0), wu = Interlocked.Exchange(ref _dWeatherUnknown, 0);
                var s = Settings.Current;
                if (s != null && s.LogEnabled)
                {
                    var sb = new StringBuilder(400);
                    sb.Append(P).Append("dzien ").Append(Today).Append(" | klucz ").Append(On ? "TAK" : "NIE").Append(", latki: przejscie ").Append(_wFinish).Append(", pogoda ").Append(_wWeather)
                      .Append(", Blackwater ").Append(_wBw).Append(", siatka ").Append(_gridOk ? _dim + "x" + _dim : "BRAK")
                      .Append(" | NaN po przejsciu ").Append(_dNanFinish).Append(" (naprawione ").Append(_dNanFixed).Append(", u wodza ").Append(_dDeferred).Append(')')
                      .Append(", pogoda poza siatka ").Append(w).Append(" (bez partii ").Append(wu).Append(", naprawione z kolejki ").Append(_dQueueFixed).Append(')')
                      .Append(", przeglad NaN ").Append(scan)
                      .Append(" (partie: ").Append(_dNames.Count > 0 ? string.Join(", ", _dNames.ToArray()) : "-").Append(')')
                      .Append(" | przed gra: przejscia bez konca anulowane ").Append(_dPreSkip).Append(", koniec wodza poprawiony ").Append(_dPreLead)
                      .Append(" | Blackwater: anulowane przed teleportem ").Append(_dBwPre).Append(", po ").Append(_dBwPost).Append(", obroncy ").Append(_dBwDefend).Append(", naprawione ").Append(_dBwFixed)
                      .Append(" | sesja: NaN ").Append(_sNan).Append(", pogoda ").Append(Volatile.Read(ref _sWeather)).Append(" | potkniecia ").Append(_dStumbles).Append(" (sesja ").Append(_stumbles).Append(").");
                    Log.Info(sb.ToString());
                }
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally { ZeroDay(); }
        }
    }
}
