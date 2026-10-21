using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// POSILKI MARSZEM (m97-posilki; Jeff 20.10: "te Dynamic Reinforcements cos zle dziala, wojsko za szybko pojawia sie i powinno pojawiac sie tylko,
    /// jesli jest naprawde blisko, i im jest dalej, tym dluzej sie na nie czeka").
    /// ROZPOZNANIE (dekompilacja DynamicReinforcements v2.1.6, ReinforcementCalculator): ComputeArrivalTime bierze ulamek Distance / MaxScanDistance
    /// i stopnie 25/30/40/50/60 s x ArrivalCap/60, potem Math.Min(ArrivalCap). Przy ustawieniach Jeffa (MaxScanDistance 50, ArrivalCap 60) kazdy oddzial
    /// w promieniu 50 jedn. (ok. 240 km, kilkanascie-kilkadziesiat godzin marszu) staje w bitwie najpozniej po 60 s. Odmowa "za daleko" (DR_refuse_far)
    /// tylko przy niskim wyniku (Score) i d/scan > 0.6 - bliski czy daleki, wysoki wynik = przychodzi.
    /// ZASADA: czas z godzin marszu. Godziny = trasa / predkosc; trasa = 1.1 x linia prosta (ta sama zapasowa trasa co NightMarch), predkosc partii =
    /// ostatnia predkosc z mapy (_lastCalculatedSpeed - to, czym gra przesuwa partie; czlonek armii doczepiony - predkosc wodza) / 1.2 (4320 s gry
    /// na jednostke dt, jak NightMarch.UnitsPerHour); zaloga i milicja (oddzial wychodzi z murow pieszo) - wzor gry dla pieszych:
    /// BaseSpeed x (200 / (200 + ludzi))^0.4 x tempo swiata (WorldPacePercent) / 1.2. Czas w bitwie = DrDeploySeconds + godziny x DrSecondsPerMarchHour.
    /// Ponad DrMaxMarchHours godzin marszu oddzial NIE przychodzi: odrzucony z powodem DR "DR_refuse_far" (komunikat DR przy ExplainRefusals).
    /// KOD: prefiks Evaluate liczy marsz kazdego kandydata i zdejmuje dalekich z listy (DR nie liczy ich do progu, limitu ludzi ani grup); finalizer
    /// oddaje ich na koniec listy jako odrzuconych (Accepted = false, wynik DR z ComputeScore - kolejnosc komunikatow DR jak dotad); prefiks
    /// ComputeArrivalTime zwraca nasz czas - kolejnosc grup, odstep MinGapBetweenGroups i armia jako blok (Max z czlonkow) zostaja w kodzie DR.
    /// Daleki oddzial nie jest ogloszony, nie spawnuje sie i nie traci ludzi (SpawnManager.Register dostaje tylko przyjetych, straty zalog
    /// PendingCasualties tylko za spawnowanych). Liczba wojska i reszta regul DR bez zmian.
    /// Blad latki: kandydat bez pomiaru = czas DR po staremu (licznik potkniec), wyjatek nie wychodzi z latki. Klucz DrArrivalByMarch NIE = DR jak dotad.
    /// Latki w kampanii (OnSessionLaunched, raz na proces), watek glowny (BattleStateBehavior.AfterStart misji).
    /// </summary>
    internal static class DrMarchArrival
    {
        private const string Tag = "Posilki marszem (m97-posilki)";
        private const string CalcName = "DynamicReinforcements.Reinforcement.ReinforcementCalculator";
        private const string CandName = "DynamicReinforcements.Scanning.ReinforcementCandidate";
        private const string CfgName = "DynamicReinforcements.Settings.DRConfig";
        private const float RouteFactor = 1.1f;     // trasa = 1.1 x linia prosta (NightMarch.HoursToSettlement, zapasowo)
        private const float HoursPerDt = 1.2f;      // 4320 s gry na jednostke dt (Campaign.Tick) - Speed / 1.2 = jedn./h
        private const string FarId = "DR_refuse_far";
        private const string FarText = "{NAME} is too far to reach us in time.";   // tekst DR (SetRejectReason)

        private static Harmony _h;
        private static bool _tried, _noDr;
        private static int _wired;                  // 0..2: Evaluate (prefiks + finalizer), ComputeArrivalTime (prefiks)
        private static string _miss;

        private static FieldInfo _fParty, _fSrc, _fDist, _fAcc, _fScore, _fArr, _fRejId, _fRejDef, _fTroops, _fSide;
        private static PropertyInfo _pName, _pCfg;
        private static MethodInfo _mScore;
        private static AccessTools.FieldRef<MobileParty, float> _lastSpeed;

        private sealed class March
        {
            public float Dist, Uph, Hours, Time;
            public string From;
            public bool Far;
        }

        // stan jednego wywolania Evaluate (watek glowny misji); kandydat DR nie nadpisuje Equals - slownik po referencji
        private static readonly Dictionary<object, March> _march = new Dictionary<object, March>();
        private static readonly List<object> _far = new List<object>();
        private static bool _active;
        private static float _maxH, _secH, _depS;
        private static int _stumbles, _battles, _farTotal, _accTotal, _farMine;

        internal static void ApplyAll(Harmony h)
        {
            if (h != null && _h == null) _h = h;
        }

        /// <summary>Z OnSessionLaunched: latki na DR raz na proces (DR jest juz zaladowany), linia startu w kazdej sesji.</summary>
        internal static void EnsureHooks()
        {
            if (!_tried && _h != null)
            {
                _tried = true;
                try { Hook(); }
                catch (Exception e) { Log.Error("DrMarchArrival.Hook", e); _miss = "wyjatek przy latce"; }
            }
            var s = Settings.Current;
            bool on = s != null && s.DrArrivalByMarch;
            if (_noDr)
            {
                Log.Info(Tag + ": start - " + _miss + " - wylaczone (bitwy jak bez Armoury).");
                return;
            }
            float bs = BaseSpeed();
            Log.Info(Tag + ": start - klucz DrArrivalByMarch " + (on ? "TAK" : "NIE") + ", latki wpiete " + _wired + "/2 (DR ReinforcementCalculator.Evaluate"
                     + " + ComputeArrivalTime) | maks. " + F2(MaxH(s)) + " h marszu, " + F0(SecH(s)) + " s bitwy na godzine marszu, rozwiniecie " + F0(DepS(s))
                     + " s | trasa " + F2(RouteFactor) + " x linia prosta, predkosc partii z mapy / " + F2(HoursPerDt) + ", zaloga i milicja pieszo: "
                     + F2(bs) + " x (200/(200+ludzi))^0.4 x tempo swiata " + Pace(s) + "% / " + F2(HoursPerDt) + " (100 ludzi = " + F2(FootUph(100, bs, s))
                     + " jedn./h) | potkniecia " + _stumbles + (_miss != null ? " | " + _miss : "") + ".");
        }

        private static void Hook()
        {
            var calc = AccessTools.TypeByName(CalcName);
            var cand = AccessTools.TypeByName(CandName);
            var cfg = AccessTools.TypeByName(CfgName);
            if (calc == null || cand == null) { _noDr = true; _miss = "brak modu DynamicReinforcements (" + CalcName + ")"; return; }
            _fParty = AccessTools.Field(cand, "Party"); _fSrc = AccessTools.Field(cand, "SourceType"); _fDist = AccessTools.Field(cand, "Distance");
            _fAcc = AccessTools.Field(cand, "Accepted"); _fScore = AccessTools.Field(cand, "Score"); _fArr = AccessTools.Field(cand, "ArrivalTime");
            _fRejId = AccessTools.Field(cand, "RejectReasonId"); _fRejDef = AccessTools.Field(cand, "RejectReasonDefault"); _fTroops = AccessTools.Field(cand, "TroopCount");
            _fSide = AccessTools.Field(cand, "Side");
            _pName = AccessTools.Property(cand, "DisplayName");
            _pCfg = cfg != null ? AccessTools.Property(cfg, "Current") : null;
            _mScore = AccessTools.DeclaredMethod(calc, "ComputeScore");
            try { _lastSpeed = AccessTools.FieldRefAccess<MobileParty, float>("_lastCalculatedSpeed"); }
            catch (Exception e) { Log.Error("DrMarchArrival.Hook(_lastCalculatedSpeed)", e); _lastSpeed = null; }
            if (_fParty == null || _fSrc == null || _fDist == null || _fAcc == null || _fArr == null || _fRejId == null || _fRejDef == null || _fTroops == null || _fScore == null || _fSide == null)
            {
                _miss = "brak pol ReinforcementCandidate (inna wersja DR) - DR po staremu";
                return;
            }
            var ev = AccessTools.DeclaredMethod(calc, "Evaluate");
            var ca = AccessTools.DeclaredMethod(calc, "ComputeArrivalTime");
            if (ev == null || ca == null) { _miss = "brak metody Evaluate/ComputeArrivalTime (inna wersja DR) - DR po staremu"; return; }
            // najpierw czas (bez listy dalekich prefiks Evaluate nic nie psuje), potem Evaluate; null = wyjatek Harmony polkniety (BKROTLog)
            var r1 = _h.Patch(ca, prefix: new HarmonyMethod(typeof(DrMarchArrival), nameof(ArrivalPrefix)));
            if (r1 == null) { _miss = "Harmony.Patch(ComputeArrivalTime) zwrocil null (BKROTLog) - DR po staremu"; return; }
            _wired++;
            var r2 = _h.Patch(ev, prefix: new HarmonyMethod(typeof(DrMarchArrival), nameof(EvalPrefix)),
                              finalizer: new HarmonyMethod(typeof(DrMarchArrival), nameof(EvalFinalizer)));
            if (r2 == null) { _miss = "Harmony.Patch(Evaluate) zwrocil null (BKROTLog) - posilki marszem nie dzialaja, DR po staremu"; return; }
            _wired++;
        }

        // ------------------------------------------------------------ latki

        /// <summary>DR Evaluate(List&lt;ReinforcementCandidate&gt;, MapEvent): pomiar marszu kazdego kandydata; dalecy zdjeci z listy na czas Evaluate.</summary>
        public static void EvalPrefix(object __0)
        {
            _active = false;
            _march.Clear();
            _far.Clear();
            try
            {
                var s = Settings.Current;
                var list = __0 as IList;
                if (s == null || !s.DrArrivalByMarch || list == null || list.Count == 0 || _wired < 2) return;
                _maxH = MaxH(s); _secH = SecH(s); _depS = DepS(s);
                float bs = BaseSpeed();
                _active = true;
                for (int i = 0; i < list.Count; i++)
                {
                    var c = list[i];
                    if (c == null) continue;
                    try { _march[c] = Measure(c, bs, s); }
                    catch (Exception e) { Stumble("Measure", e); }   // bez pomiaru: DR liczy czas po swojemu, kandydat zostaje
                }
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    var c = list[i];
                    March m;
                    if (c != null && _march.TryGetValue(c, out m) && m.Far) { list.RemoveAt(i); _far.Add(c); }
                }
                _far.Reverse();   // kolejnosc skanu DR (od najblizszego)
            }
            catch (Exception e) { Stumble("EvalPrefix", e); }
        }

        /// <summary>Finalizer Evaluate (takze po wyjatku DR): dalecy wracaja na liste jako odrzuceni "za daleko"; linia logu bitwy.</summary>
        public static void EvalFinalizer(object __0, MapEvent __1)
        {
            if (!_active) return;
            _active = false;
            _farMine = 0;
            var list = __0 as IList;
            try
            {
                object cfg = null;
                try { cfg = _pCfg != null ? _pCfg.GetValue(null, null) : null; } catch { }
                var mySide = BattleSideEnum.None;
                try { var mp = PartyBase.MainParty; if (mp != null && mp.MapEventSide != null) mySide = mp.MapEventSide.MissionSide; } catch { }
                foreach (var c in _far)
                {
                    try
                    {
                        if (list != null) list.Add(c);
                        _fAcc.SetValue(c, false);
                        _fArr.SetValue(c, 0f);
                        // powod tylko po stronie gracza (tekst DR mowi "us"); daleki sojusznik wroga zostaje w domu po cichu - komunikat odmowy DR
                        // wybiera najwyzszy wynik z OBU stron, a dalekich jest wielu (promien skanu DR ok. 240 km)
                        bool mine = mySide != BattleSideEnum.None && _fSide.GetValue(c) is BattleSideEnum && (BattleSideEnum)_fSide.GetValue(c) == mySide;
                        _fRejId.SetValue(c, mine ? FarId : null);
                        _fRejDef.SetValue(c, mine ? FarText : null);
                        if (mine) _farMine++;
                        // wynik DR jak dla kazdego kandydata - kolejnosc komunikatu odmowy i opinii frakcji DR bez zmian
                        if (_mScore != null && cfg != null && __1 != null)
                        {
                            var sc = _mScore.Invoke(null, new object[] { c, __1, cfg });
                            if (sc is float) _fScore.SetValue(c, (float)sc);
                        }
                    }
                    catch (Exception e) { Stumble("EvalFinalizer(daleki)", e); }
                }
            }
            catch (Exception e) { Stumble("EvalFinalizer", e); }
            try { LogBattle(list); } catch (Exception e) { Stumble("LogBattle", e); }
            _march.Clear();   // ComputeArrivalTime wola tylko Evaluate - po bitwie bez referencji do kandydatow
            _far.Clear();
        }

        /// <summary>DR ComputeArrivalTime(c, cfg): nasz czas z godzin marszu; kandydat bez pomiaru (albo klucz NIE) - DR po swojemu.</summary>
        public static bool ArrivalPrefix(object __0, ref float __result)
        {
            try
            {
                March m;
                if (!_active || __0 == null || !_march.TryGetValue(__0, out m) || m.Far) return true;
                __result = m.Time;
                return false;
            }
            catch (Exception e) { Stumble("ArrivalPrefix", e); return true; }
        }

        // ------------------------------------------------------------ marsz

        private static March Measure(object c, float baseSpeed, Settings s)
        {
            var m = new March();
            m.Dist = Math.Max(0f, (float)_fDist.GetValue(c));
            int src = Convert.ToInt32(_fSrc.GetValue(c), CultureInfo.InvariantCulture);   // DRSourceType: Army 0, Lord 1, Garrison 2, Militia 3, ...
            var mp = _fParty.GetValue(c) as MobileParty;
            float uph = 0f;
            if (mp != null && src != 2 && src != 3 && _lastSpeed != null)
            {
                // czlonek armii doczepiony do wodza idzie z predkoscia wodza (MobileParty.CalculateSpeed)
                var basis = mp.AttachedTo ?? mp;
                float sp = _lastSpeed(basis);
                if (sp > 0.05f && sp < 50f) { uph = sp / HoursPerDt; m.From = mp.AttachedTo != null ? "armia" : "partia"; }
            }
            if (uph <= 0f)
            {
                int men = 0;
                try { men = (int)_fTroops.GetValue(c); } catch { }
                uph = FootUph(men, baseSpeed, s);
                m.From = src == 2 ? "zaloga pieszo" : src == 3 ? "milicja pieszo" : "pieszo";
            }
            m.Uph = Math.Max(0.05f, uph);
            m.Hours = m.Dist * RouteFactor / m.Uph;
            m.Far = m.Hours > _maxH;
            m.Time = _depS + m.Hours * _secH;
            return m;
        }

        /// <summary>Wzor gry dla pieszych (DefaultPartySpeedCalculatingModel.CalculateBaseSpeedForParty) x tempo swiata (WorldPace) / 1.2.</summary>
        private static float FootUph(int men, float baseSpeed, Settings s)
        {
            float v = baseSpeed * (float)Math.Pow(200.0 / (200.0 + Math.Max(1, men)), 0.4);
            return v * Pace(s) / 100f / HoursPerDt;
        }

        private static float BaseSpeed()
        {
            float b = 4f;
            try
            {
                var md = Campaign.Current != null && Campaign.Current.Models != null ? Campaign.Current.Models.PartySpeedCalculatingModel : null;
                if (md != null) b = md.BaseSpeed;
            }
            catch { }
            return b >= 1f && b <= 10f ? b : 4f;
        }

        // ta sama regula co WorldPace.BasePostfix: 5..99 = procent, poza tym bez zmiany
        private static int Pace(Settings s)
        {
            int p = s != null ? s.WorldPacePercent : 100;
            return p >= 100 || p < 5 ? 100 : p;
        }

        // przyciecie = suwak MCM (gen_mcm RANGES)
        private static float MaxH(Settings s) { return Clamp(s != null ? s.DrMaxMarchHours : 2f, 0.25f, 6f); }
        private static float SecH(Settings s) { return Clamp(s != null ? s.DrSecondsPerMarchHour : 120f, 10f, 600f); }
        private static float DepS(Settings s) { return Clamp(s != null ? s.DrDeploySeconds : 30f, 0f, 300f); }
        private static float Clamp(float v, float lo, float hi) { return float.IsNaN(v) ? lo : v < lo ? lo : v > hi ? hi : v; }

        // ------------------------------------------------------------ log

        private static void LogBattle(IList list)
        {
            if (list == null) return;
            _battles++;
            int acc = 0, nearRefused = 0, noMeasure = 0;
            var lines = new List<KeyValuePair<float, string>>();
            foreach (var c in list)
            {
                if (c == null) continue;
                March m;
                bool has = _march.TryGetValue(c, out m);
                if (!has) noMeasure++;
                bool a = _fAcc.GetValue(c) is bool && (bool)_fAcc.GetValue(c);
                if (has && m.Far) continue;
                if (!a) { nearRefused++; continue; }
                acc++;
                float arr = (float)_fArr.GetValue(c);
                var sb = new StringBuilder();
                sb.Append(Tag).Append(": przyjety ").Append(Name(c)).Append(" [").Append(_fSrc.GetValue(c)).Append("] ").Append((int)_fTroops.GetValue(c)).Append(" ludzi");
                if (has)
                    sb.Append(" | odleglosc ").Append(F2(m.Dist)).Append(" jedn., predkosc ").Append(F2(m.Uph)).Append(" jedn./h (").Append(m.From).Append("), marsz ")
                      .Append(F2(m.Hours)).Append(" h -> czas z marszu ").Append(F0(m.Time)).Append(" s");
                else
                    sb.Append(" | bez pomiaru (czas DR)");
                sb.Append(", w bitwie t+").Append(F0(arr)).Append(" s");
                lines.Add(new KeyValuePair<float, string>(arr, sb.ToString()));
            }
            _farTotal += _far.Count;
            _accTotal += acc;
            var far = new StringBuilder();
            int shown = 0;
            foreach (var c in _far)
            {
                March m;
                if (!_march.TryGetValue(c, out m)) continue;
                if (shown++ >= 3) break;
                far.Append(shown > 1 ? "; " : "").Append(Name(c)).Append(" [").Append(_fSrc.GetValue(c)).Append("] ").Append(F2(m.Dist)).Append(" jedn. / ")
                   .Append(F2(m.Uph)).Append(" jedn./h = ").Append(F2(m.Hours)).Append(" h");
            }
            Log.Info(Tag + ": bitwa " + _battles + " | kandydatow " + list.Count + ", za daleko " + _far.Count + " (ponad " + F2(_maxH) + " h marszu; po stronie gracza " + _farMine + "), przyjetych "
                     + acc + ", blisko a odmowa DR " + nearRefused + ", bez pomiaru " + noMeasure + " | czas = " + F0(_depS) + " s + h x " + F0(_secH)
                     + " s | najblizsi za daleko: " + (shown > 0 ? far.ToString() : "brak") + " | sesja: bitew " + _battles + ", przyjetych " + _accTotal
                     + ", za daleko " + _farTotal + ", potkniecia " + _stumbles + ".");
            lines.Sort((x, y) => x.Key.CompareTo(y.Key));
            int n = 0;
            foreach (var kv in lines) { if (n++ >= 30) break; Log.Info(kv.Value); }
        }

        private static string Name(object c)
        {
            try { var o = _pName != null ? _pName.GetValue(c, null) : null; return o != null ? o.ToString() : "?"; }
            catch { return "?"; }
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_stumbles <= 3) Log.Error("DrMarchArrival." + where, e);
        }

        private static string F0(float v) { return v.ToString("0", CultureInfo.InvariantCulture); }
        private static string F2(float v) { return v.ToString("0.00", CultureInfo.InvariantCulture); }
    }
}
