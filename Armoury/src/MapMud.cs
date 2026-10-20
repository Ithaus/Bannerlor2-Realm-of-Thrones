using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// p5-bloto (R3-07c, POMYSLY-35 pkt 19; Jeff 20.10: "28 - nie, 29 - nie. Pozostale tak") - BLOTO POZA DROGA.
    /// Zrodlo: audyt drog 09.10 (docs/audyt-2026-10-09/08-drogi-wioski-widok.md, tabela 4.3 M-3): "poza droga -15% w deszczu, na drodze bez kary";
    /// Mortimer: pieszy 15-20 mil dziennie, w zla pogode 6-8; legion na nieutwardzonej drodze po deszczu prawie stoi. Wiosna/jesien z audytu - NIE
    /// (pory Westeros trwaja latami, kara bylaby przez lata) - tylko deszcz.
    ///
    /// REGULA: partia ladowa (gracz i AI - lordowie, karawany, wozy wsi, bandy, patrole; ta sama regula) poza droga w deszczu jedzie
    /// MapMudSpeedPercent% (domyslnie 15) wolniej - wpis "Mud" w podpowiedzi predkosci. Deszcz = pogoda mapy gry w punkcie partii
    /// (MapWeatherModel.GetWeatherEventInPosition): LightRain (w grze: mokro - padalo w ostatnich 24 h) albo HeavyRain (pada teraz). Snieg i zamiec -
    /// bez blota (maja swoja kare "Snow", TerrainEase); Storm (burza morska NavalDLC) - bez blota (zjawisko morza, gra nie liczy go w pamieci predkosci
    /// ladu tak samo jak deszczu). Na drodze bez kary: "na drodze" = ta sama siatka i ten sam margines, co premia drogi (MapRoads.KindAt: trakt, a przy
    /// MapSideRoadSpeedBonus takze droga boczna do wioski). Wymaga MapRoadSpeedBonus (bez siatki drog nie wiadomo, gdzie jest droga - bloto nieczynne).
    ///
    /// GDZIE: MarchPace.SpeedPostfix (jedna latka na kazdy CalculateFinalSpeed, Priority.Last, najbardziej zewnetrzny poziom lancucha - SpeedDepth), po
    /// MapRoads.SpeedRoad. Kara = Add(-wynik x procent / (1 + suma wspolczynnikow)) - jak premia drogi, tylko w dol. Sufit kolumny (MarchPace) zbija
    /// wynik wpisem Add do sufitu, wiec po naszej karze wynik jest pod sufitem i kara nie ginie. Gdy inny model trzyma wynik samym LimitMax (bez Add) i kara
    /// zginelaby pod sufitem - sufit w dol o ten sam procent (licznik "sufit obnizony"). Kara snu (NightRest) i karczma (TownInns) biegna PO nas (procent
    /// od wyniku) - mnoza sie z blotem. Na podlodze gry (LimitMin) kara moze zginac - audyt liczy to osobno.
    ///
    /// PAMIEC PREDKOSCI GRY (dekompilacja 1.4.8, MobileParty.CalculateSpeedForPartyUnified): IsBaseSpeedCacheInvalid porownuje
    /// GetWeatherEffectOnTerrainForPosition (Default / Wet; Wet = LightRain, HeavyRain, Snowy, Blizzard) z tym przy ostatnim liczeniu - poczatek i koniec
    /// deszczu w punkcie partii = gra liczy predkosc od nowa sama. Wejscie na droge / zejscie z drogi - postfiks pamieci MapRoads (stan drogi partii).
    /// Brzeg: przejscie snieg <-> deszcz (oba Wet) bez zmiany sciany siatki - kara wchodzi przy nastepnym przeliczeniu (zmiana sciany, noc, droga).
    ///
    /// KOSZT: sciezka predkosci (watki robocze gry, PartiesThink / TWParallel) - bez alokacji i bez blokad: odczyt pogody (tablica wezlow gry; NavalDLC
    /// przy Clear przeglada liste burz), siatka drog tylko w deszczu. Liczniki bez blokad (przyblizone "ok.", jak MapRoads), potkniecia przez Interlocked.
    /// POMIAR (tylko log): spis partio-godzin w dobach pomiaru (LogGate.Today), audyt w takcie linii (do 8 partii AI w blocie + gracz: model wprost bez
    /// blota i z blotem), linia "Bloto (BL): dzien N". Bledy liczone (potkniecia), nigdy wylacznik calej funkcji. Nic nie idzie do zapisu gry.
    /// Klucze NOWE: MapMudSpeedPenalty (TAK), MapMudSpeedPercent (15, przyciete do 0..50). Wylaczony klucz = sciezka sprzed zmiany (Speed wychodzi od razu).
    /// </summary>
    internal static class MapMud
    {
        private const string P = "Bloto (BL): ";
        private const int MaxPercent = 50;
        private const int AuditMax = 8;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly TextObject _txtMud = new TextObject("{=!}Mud");

        // audyt: [ThreadStatic] - tylko watek glowny audytu; watki robocze gry widza false
        [ThreadStatic] private static bool _suppress;   // liczenie "bez blota"

        // liczniki z watkow roboczych (przyblizone - bez blokad, jak MapRoads): liczenia, w deszczu, z kara (lekki deszcz / ulewa), w deszczu na drodze,
        // sufit obnizony, probka czasu sciezki (co 64. wywolanie)
        private static long _calls, _wet, _mudLight, _mudHeavy, _roadWet, _capLowered, _ticks, _sampled;
        private static int _parStumbles, _parStumblesAll, _stumbles, _stumblesAll;
        private static Exception _firstPar;

        // spis partio-godzin (watek glowny): 0 rody, 1 karawany, 2 wozy wsi, 3 bandy, 4 inne, 5 gracz - w ruchu / w deszczu / w blocie
        private static readonly int[] _hN = new int[6], _hWet = new int[6], _hMud = new int[6];
        private static int _hours;
        private static long _censusTicks;
        private static readonly System.Random _rng = new System.Random(2010);
        private static readonly List<MobileParty> _cand = new List<MobileParty>();
        private static AccessTools.FieldRef<MobileParty, float> _lastSpeedRef;
        private static bool _lastSpeedTried;

        private static bool On(Settings s) { return s != null && s.MapMudSpeedPenalty && s.MapRoadSpeedBonus; }

        private static float PctOf(Settings s)
        {
            int p = s.MapMudSpeedPercent;
            return (p < 0 ? 0 : p > MaxPercent ? MaxPercent : p) / 100f;
        }

        /// <summary>Deszcz w punkcie: 0 sucho (takze snieg, zamiec, burza morska), 1 LightRain (mokro po deszczu), 2 HeavyRain (pada).</summary>
        private static int WetAt(MapWeatherModel wm, float x, float y)
        {
            var w = wm.GetWeatherEventInPosition(new Vec2(x, y));
            return w == MapWeatherModel.WeatherEvent.HeavyRain ? 2 : w == MapWeatherModel.WeatherEvent.LightRain ? 1 : 0;
        }

        private static void ParStumble(Exception e)
        {
            Interlocked.Increment(ref _parStumbles);
            Interlocked.Increment(ref _parStumblesAll);
            if (_firstPar == null) _firstPar = e;
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumblesAll++;
            if (_stumblesAll <= 3) Log.Error("MapMud." + where, e);
        }

        // ------------------------------------------------------------------ sciezka predkosci (watki robocze gry)
        /// <summary>MarchPace.SpeedPostfix (po MapRoads.SpeedRoad, ten sam poziom lancucha): partia ladowa poza droga w deszczu -x% wyniku jako "Mud".</summary>
        internal static void Speed(MobileParty mp, ref ExplainedNumber r)
        {
            if (_suppress) return;
            var s = Settings.Current;
            if (!On(s)) return;
            long t0 = (++_calls & 63) == 1 ? Stopwatch.GetTimestamp() : 0L;
            try
            {
                if (mp == null || !SpeedDepth.OutermostFinal) return;
                if (mp.IsCurrentlyAtSea || mp.IsTransitionInProgress) return;   // morze i przejscie lad-morze - prawa morza
                var c = Campaign.Current;
                if (c == null) return;
                var wm = c.Models.MapWeatherModel;
                if (wm == null) return;
                var p = mp.Position;
                float x = p.X, y = p.Y;
                int wet = WetAt(wm, x, y);
                if (wet == 0) return;
                _wet++;
                int kind = MapRoads.KindAt(x, y);   // -1 brak siatki / klucz drog wylaczony, 0 poza droga, 1 boczna, 2 trakt
                if (kind != 0) { if (kind > 0) _roadWet++; return; }
                float pct = PctOf(s);
                if (pct <= 0f) return;
                float res = r.ResultNumber;
                if (res <= 0f) return;
                float f = 1f + r.SumOfFactors;
                float cut = res * pct;
                r.Add(f > 0.01f ? -cut / f : -cut, _txtMud);
                // inny model trzyma wynik samym LimitMax (bez Add) - kara zginelaby pod sufitem: sufit w dol o ten sam procent
                float lim = r.LimitMaxValue;
                if (lim > 0f && lim < float.MaxValue * 0.5f && r.ResultNumber >= lim - 0.0005f && lim > res - cut + 0.001f)
                {
                    r.LimitMax(lim * (1f - pct));
                    _capLowered++;
                }
                if (wet == 2) _mudHeavy++; else _mudLight++;
            }
            catch (Exception e) { ParStumble(e); }
            finally { if (t0 != 0) { _ticks += Stopwatch.GetTimestamp() - t0; _sampled++; } }
        }

        // ------------------------------------------------------------------ sesja i doba (watek glowny)
        private static void ZeroDay()
        {
            for (int i = 0; i < _hN.Length; i++) { _hN[i] = 0; _hWet[i] = 0; _hMud[i] = 0; }
            _hours = 0; _censusTicks = 0;
            ZeroSpeed();
        }

        private static void ZeroSpeed()
        {
            _calls = 0; _wet = 0; _mudLight = 0; _mudHeavy = 0; _roadWet = 0; _capLowered = 0; _ticks = 0; _sampled = 0;
            _parStumbles = 0; _stumbles = 0;
        }

        /// <summary>Stan kary do linii: CZYNNA / NIECZYNNA z powodem.</summary>
        private static string Status(Settings s)
        {
            if (s == null || !s.MapMudSpeedPenalty) return "WYLACZONA (MapMudSpeedPenalty)";
            if (!s.MapRoadSpeedBonus) return "NIECZYNNA (wymaga MapRoadSpeedBonus - bez siatki drog)";
            if (!MapRoads.Ready) return "NIECZYNNA (siatka drog niewczytana - patrz linia Drogi (DR))";
            if (MapRoads.SpeedModels <= 0) return "NIECZYNNA (latka predkosci MarchPace niewpieta przy starcie gry - potrzebny restart gry)";
            return "CZYNNA";
        }

        /// <summary>Start sesji kampanii (OnSessionLaunched, PO MapRoads.SessionStart - siatka drog wczytana): liczniki od zera, linia startu.</summary>
        internal static void SessionStart()
        {
            ZeroDay();
            _parStumblesAll = 0; _stumblesAll = 0; _firstPar = null;
            var s = Settings.Current;
            string wm = "-";
            try { var m = Campaign.Current != null ? Campaign.Current.Models.MapWeatherModel : null; wm = m != null ? m.GetType().FullName : "brak"; } catch (Exception e) { Stumble("SessionStart", e); }
            Log.Info(P + "start sesji - klucz MapMudSpeedPenalty " + (s != null && s.MapMudSpeedPenalty ? "tak" : "nie")
                     + ", kara " + (s != null ? (PctOf(s) * 100f).ToString("0", Inv) : "-") + "% predkosci poza droga w deszczu (LightRain - mokro po deszczu, HeavyRain - ulewa; snieg i burza morska bez blota)"
                     + " | na drodze bez kary: siatka premii drog (trakty" + (s != null && s.MapSideRoadSpeedBonus ? " + drogi boczne" : "") + ", klucz MapRoadSpeedBonus " + (s != null && s.MapRoadSpeedBonus ? "tak" : "nie")
                     + ", siatka " + (MapRoads.Ready ? "wczytana" : "NIE") + ") | latka predkosci MarchPace w " + MapRoads.SpeedModels + " modelach | model pogody " + wm
                     + " | kara " + Status(s) + ".");
        }

        /// <summary>Spis godzinny w dobach pomiaru (LogGate.Today): partie ladowe w ruchu (bez doczepionych do armii) - ile w deszczu, ile w blocie.</summary>
        internal static void Hourly()
        {
            var s = Settings.Current;
            if (!On(s) || !MapRoads.Ready || Campaign.Current == null || !LogGate.Today) return;
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                var wm = Campaign.Current.Models.MapWeatherModel;
                if (wm == null) return;
                var all = MobileParty.All;
                var main = MobileParty.MainParty;
                for (int i = 0; i < all.Count; i++)
                {
                    var mp = all[i];
                    try
                    {
                        if (mp == null || !mp.IsActive || mp.AttachedTo != null || mp.IsCurrentlyAtSea || mp.IsTransitionInProgress || !mp.IsMoving) continue;
                        int k = mp == main ? 5 : mp.IsLordParty ? 0 : mp.IsCaravan ? 1 : mp.IsVillager ? 2 : mp.IsBandit ? 3 : 4;
                        var p = mp.Position;
                        _hN[k]++;
                        if (WetAt(wm, p.X, p.Y) == 0) continue;
                        _hWet[k]++;
                        if (MapRoads.KindAt(p.X, p.Y) == 0) _hMud[k]++;
                    }
                    catch (Exception e) { Stumble("Hourly", e); }
                }
                _hours++;
            }
            catch (Exception e) { Stumble("Hourly", e); }
            finally { _censusTicks += Stopwatch.GetTimestamp() - t0; }
        }

        private static string Pct(long a, long b) { return b > 0 ? (100.0 * a / b).ToString("0.0", Inv) : "-"; }

        /// <summary>Takt doby (po MapRoads.Daily): w dobie linii (LogGate.Lines) audyt i linia "Bloto (BL): dzien N" za dobe spisu; potem zerowanie.</summary>
        internal static void Daily()
        {
            var s = Settings.Current;
            if (s == null) return;
            try
            {
                if (!On(s)) { ZeroDay(); return; }
                // ciche logi: doba bez linii - spis godzinny zostaje (zbiera tylko w dobie pomiaru), liczniki predkosci i potkniec od zera (jak MapRoads)
                if (!LogGate.Lines) { ZeroSpeed(); return; }
                Line(s, (int)CampaignTime.Now.ToDays - 1);
            }
            catch (Exception e) { Stumble("Daily", e); }
            ZeroDay();
        }

        private static void Line(Settings s, int day)
        {
            var ci = Inv;
            var sb = new StringBuilder(1100);
            float pct = PctOf(s);
            sb.Append(P).Append("dzien ").Append(day)
              .Append(" | klucz MapMudSpeedPenalty tak, kara ").Append((pct * 100f).ToString("0", ci)).Append("% poza droga w deszczu, kara ").Append(Status(s));
            int tot = 0, totW = 0, totM = 0;
            for (int k = 0; k < 5; k++) { tot += _hN[k]; totW += _hWet[k]; totM += _hMud[k]; }
            sb.Append(" | partio-godziny AI w ruchu na ladzie (godzin spisu ").Append(_hours).Append("): ").Append(tot)
              .Append(", w deszczu ").Append(totW).Append(" (").Append(Pct(totW, tot)).Append("%)")
              .Append(", w blocie (deszcz poza droga) ").Append(totM).Append(" (").Append(Pct(totM, tot)).Append("%)")
              .Append(", w deszczu na drodze (bez kary) ").Append(totW - totM)
              .Append(" | w blocie / w ruchu: rody ").Append(_hMud[0]).Append('/').Append(_hN[0])
              .Append(", karawany ").Append(_hMud[1]).Append('/').Append(_hN[1])
              .Append(", wozy wsi ").Append(_hMud[2]).Append('/').Append(_hN[2])
              .Append(", bandy ").Append(_hMud[3]).Append('/').Append(_hN[3])
              .Append(", inne ").Append(_hMud[4]).Append('/').Append(_hN[4])
              .Append(" | gracz godzin w ruchu ").Append(_hN[5]).Append(", w deszczu ").Append(_hWet[5]).Append(", w blocie ").Append(_hMud[5]);
            long sc = _calls, mud = _mudLight + _mudHeavy;
            sb.Append(" | liczenia predkosci (ok.) ").Append(sc).Append(", w deszczu ").Append(_wet)
              .Append(", z kara blota ").Append(mud).Append(" (").Append(Pct(mud, sc)).Append("%; mokro ").Append(_mudLight).Append(", ulewa ").Append(_mudHeavy).Append(')')
              .Append(", w deszczu na drodze bez kary ").Append(_roadWet).Append(", sufit obnizony ").Append(_capLowered);
            Audit(s, sb, pct);
            double censusMs = _censusTicks * 1000.0 / Stopwatch.Frequency;
            double us = _sampled > 0 ? _ticks * 1e6 / Stopwatch.Frequency / _sampled : 0;
            sb.Append(" | koszt: spis ").Append(censusMs.ToString("0.000", ci)).Append(" ms/dobe, sciezka predkosci ").Append(us.ToString("0.000", ci))
              .Append(" us/wywolanie (probka ").Append(_sampled).Append(')')
              .Append(" | potkniecia dzis ").Append(_stumbles).Append(" (od wczytania ").Append(_stumblesAll).Append("), w predkosci dzis ").Append(_parStumbles)
              .Append(" (od wczytania ").Append(_parStumblesAll).Append(")")
              .Append(_firstPar != null ? " pierwsze: " + _firstPar.GetType().Name + " " + (_firstPar.Message ?? "") : "").Append('.');
            Log.Info(sb.ToString());
        }

        /// <summary>Audyt w takcie linii: do 8 losowych partii AI w ruchu w blocie + gracz na ladzie - model predkosci wprost: bez blota (r0) i z blotem
        /// (r1, z opisami): kara rzeczywista 1 - r1/r0, wpis "Mud", kara zjedzona (r1 > r0 x (1 - kara) x 1.005), na podlodze LimitMin (poza srednia),
        /// pamiec gry (_lastCalculatedSpeed) zgodna z r1 +-2%.</summary>
        private static void Audit(Settings s, StringBuilder sb, float pct)
        {
            var ci = Inv;
            if (Campaign.Current == null || !MapRoads.Ready) { sb.Append(" | audyt: brak siatki drog | gracz: -"); return; }
            var model = Campaign.Current.Models.PartySpeedCalculatingModel;
            var wm = Campaign.Current.Models.MapWeatherModel;
            if (model == null || wm == null) { sb.Append(" | audyt: brak modelu predkosci / pogody | gracz: -"); return; }
            if (!_lastSpeedTried)
            {
                _lastSpeedTried = true;
                try { _lastSpeedRef = AccessTools.FieldRefAccess<MobileParty, float>("_lastCalculatedSpeed"); } catch { _lastSpeedRef = null; }
            }
            _cand.Clear();
            var all = MobileParty.All;
            var main = MobileParty.MainParty;
            for (int i = 0; i < all.Count; i++)
            {
                var mp = all[i];
                try
                {
                    if (mp == null || mp == main || !mp.IsActive || mp.AttachedTo != null || mp.IsCurrentlyAtSea || mp.IsTransitionInProgress || !mp.IsMoving) continue;
                    var p = mp.Position;
                    if (WetAt(wm, p.X, p.Y) != 0 && MapRoads.KindAt(p.X, p.Y) == 0) _cand.Add(mp);
                }
                catch (Exception e) { Stumble("Audit.Scan", e); }
            }
            int candN = _cand.Count;
            int n = 0, nb = 0, noLine = 0, eaten = 0, atFloor = 0, cacheOk = 0, cacheN = 0;
            double sum = 0, mn = double.MaxValue, mx = double.MinValue;
            int kmax = Math.Min(AuditMax, _cand.Count);
            for (int i = 0; i < kmax; i++)
            {
                int j = i + _rng.Next(_cand.Count - i);
                var tmp = _cand[i]; _cand[i] = _cand[j]; _cand[j] = tmp;
                float r0, r1, lmin, cached;
                bool line;
                if (!Probe(model, _cand[i], out r0, out r1, out lmin, out line, out cached)) continue;
                n++;
                if (!line) noLine++;
                if (cached > 0f) { cacheN++; if (Math.Abs(cached - r1) <= 0.02f * r1) cacheOk++; }
                if (r0 * (1f - pct) <= lmin + 0.001f) { atFloor++; continue; }
                nb++;
                double b = r0 > 0.0001f ? 1.0 - r1 / r0 : 0.0;
                sum += b; if (b < mn) mn = b; if (b > mx) mx = b;
                if (r1 > r0 * (1f - pct) * 1.005f) eaten++;
            }
            _cand.Clear();
            sb.Append(" | audyt AI w blocie: kandydatow ").Append(candN)
              .Append("; probek ").Append(n)
              .Append(", kara rzeczywista sr. ").Append(nb > 0 ? (100.0 * sum / nb).ToString("0.00", ci) : "-").Append("% (min ").Append(nb > 0 ? (100.0 * mn).ToString("0.00", ci) : "-")
              .Append(", maks ").Append(nb > 0 ? (100.0 * mx).ToString("0.00", ci) : "-").Append(')')
              .Append(", bez wpisu Mud ").Append(noLine).Append(", kara zjedzona ").Append(eaten)
              .Append(", pamiec gry zgodna ").Append(cacheOk).Append('/').Append(cacheN)
              .Append(", na podlodze LimitMin (poza srednia) ").Append(atFloor);
            // gracz (na ladzie): zawsze - pogoda, droga, predkosc bez blota i z blotem
            try
            {
                if (main != null && main.IsActive && !main.IsCurrentlyAtSea && !main.IsTransitionInProgress)
                {
                    var p = main.Position;
                    var w = wm.GetWeatherEventInPosition(new Vec2(p.X, p.Y));
                    int kind = MapRoads.KindAt(p.X, p.Y);
                    float r0, r1, lmin, cached;
                    bool line;
                    if (Probe(model, main, out r0, out r1, out lmin, out line, out cached))
                        sb.Append(" | gracz: pogoda ").Append(w.ToString()).Append(", na drodze ").Append(kind == 2 ? "tak (trakt)" : kind == 1 ? "tak (boczna)" : kind == 0 ? "nie" : "brak siatki")
                          .Append(", bez blota ").Append(r0.ToString("0.000", ci)).Append(", z blotem ").Append(r1.ToString("0.000", ci))
                          .Append(", wpis Mud ").Append(line ? "tak" : "nie").Append(", pamiec gry ").Append(cached.ToString("0.000", ci));
                    else sb.Append(" | gracz: audyt nieudany");
                }
                else sb.Append(" | gracz: na morzu / w przejsciu / nieczynny");
            }
            catch (Exception e) { Stumble("Audit.Player", e); sb.Append(" | gracz: blad audytu"); }
        }

        private static bool Probe(PartySpeedModel model, MobileParty mp, out float r0, out float r1, out float lmin, out bool line, out float cached)
        {
            r0 = r1 = cached = 0f; lmin = float.MinValue; line = false;
            try
            {
                _suppress = true;
                try { var e0 = model.CalculateFinalSpeed(mp, model.CalculateBaseSpeed(mp, false)); r0 = e0.ResultNumber; lmin = e0.LimitMinValue; }
                finally { _suppress = false; }
                var en = model.CalculateFinalSpeed(mp, model.CalculateBaseSpeed(mp, true));
                r1 = en.ResultNumber;
                string mud = _txtMud.ToString();
                foreach (var l in en.GetLines()) if (l.Item1 == mud) { line = true; break; }
                if (_lastSpeedRef != null) cached = _lastSpeedRef(mp);
                return true;
            }
            catch (Exception e) { Stumble("Audit.Probe", e); return false; }
            finally { _suppress = false; }
        }
    }
}
