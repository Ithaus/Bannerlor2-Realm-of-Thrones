using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
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
    /// (pory Westeros trwaja latami) - tylko pogoda w punkcie partii.
    ///
    /// REGULA (po recenzji 20.10): partia ladowa (gracz i AI - lordowie, karawany, wozy wsi, bandy, patrole; ta sama regula) poza droga:
    ///  - PADA (pogoda mapy HeavyRain, albo Storm - burza NavalDLC nad ladem wybrzeza): -MapMudSpeedPercent% (domyslnie 15), wpis "Mud";
    ///  - MOKRO PO DESZCZU (LightRain - w grze to NIE lekki deszcz: teraz nie pada, ale padalo w ktoryms z 5 poprzednich okresow po 4 h, do ok. doby
    ///    wstecz; DefaultMapWeatherModel.SetIsRainingOrWetFromFunction): -MapMudWetSpeedPercent% (domyslnie 7.5, polowa), wpis "Wet ground".
    /// Dlaczego dwa stopnie (recenzja, uwaga wazna): czestosc deszczu gry zalezy od pory roku nieba (CalculateTimeFactorForRain = 1 od polowy jesieni
    /// przez zime), a pory Westeros trwaja latami (WesterosClimate: DefaultMapWeatherModel w SkyTypes). W krainach deszczowych "mokro" bywa wtedy 50-97%
    /// czasu (ulewa najwyzej 45%) - pelna kara za "mokro" dawala ok. -14.6% sredniej predkosci przez lata. Teraz w najbardziej deszczowej krainie
    /// ok. 0.45 x 15 + 0.52 x 7.5 = ok. -10.6% sredniej, przy tekstury deszczu 0.8 ok. -7.6%, przy 0.7 ok. -4.7% (a nie -14.6 / -11.7 / -7.7).
    /// Snieg i zamiec - bez blota (maja swoja kare "Snow", TerrainEase). Na drodze bez kary: "na drodze" = ta sama siatka i ten sam margines co premia
    /// drogi (MapRoads.KindAt: trakt, a przy MapSideRoadSpeedBonus takze droga boczna do wioski). Wymaga MapRoadSpeedBonus (bez siatki drog nie wiadomo,
    /// gdzie jest droga - bloto nieczynne).
    ///
    /// GDZIE: MarchPace.SpeedPostfix (jedna latka na kazdy CalculateFinalSpeed, Priority.Last, najbardziej zewnetrzny poziom lancucha - SpeedDepth), po
    /// MapRoads.SpeedRoad. Kara = Add(-wynik x procent / (1 + suma wspolczynnikow)) - jak premia drogi, tylko w dol. Sufit kolumny (MarchPace) zbija
    /// wynik wpisem Add do sufitu, wiec po naszej karze wynik jest pod sufitem i kara nie ginie. Gdy inny model trzyma wynik samym LimitMax (bez Add) i kara
    /// zginelaby pod sufitem - sufit w dol o ten sam procent (licznik "sufit obnizony"). Kara snu (NightRest) i karczma (TownInns) biegna PO nas (procent
    /// od wyniku) - mnoza sie z blotem. Na podlodze gry (LimitMin) kara moze zginac - audyt liczy to osobno.
    ///
    /// PAMIEC PREDKOSCI GRY (dekompilacja 1.4.8, MobileParty.CalculateSpeedForPartyUnified): gra przelicza predkosc sama tylko przy zmianie
    /// GetWeatherEffectOnTerrainForPosition (Default / Wet; Wet = LightRain, HeavyRain, Snowy, Blizzard, a w NavalDLC takze zasieg burzy), sciany siatki,
    /// nocy, wiatru. Przejscia ulewa <-> mokro, snieg <-> deszcz, Clear+burza <-> deszcz to Wet -> Wet - gra by ich nie zauwazyla. Dlatego (recenzja,
    /// uwaga drobna, rozwiazana w calosci): NASZ postfiks MobileParty.IsLastSpeedCacheInvalid (CachePostfix, wpinany w MapRoads.ApplyAll obok postfiksu
    /// drog, DeclaredMethod): pamiec partii trzyma stopien pogody (0 sucho / snieg, 1 mokro, 2 pada) uzyty przy ostatnim liczeniu, ktore gra zapisala
    /// do _lastCalculatedSpeed (ten sam straznik zapisu co stan drogi - MapRoads.Recording); inny stopien w biezacym punkcie = "pamiec niewazna" i gra
    /// liczy od nowa (raz na zmiane pogody w punkcie). Wejscie na droge / zejscie z drogi - postfiks pamieci MapRoads (stan drogi partii) jak dotad.
    /// PAMIEC PARTII: tablica int bez blokad (1 << 17 slotow, indeks = Party.Index modulo): Party.Index << 2 | stopien; -1 = pusto; inna partia
    /// w slocie = decyduje gra (jak bez latki). Czyszczona przy kazdym starcie sesji.
    /// Zmiana kluczy blota w MCM w trakcie gry: stara predkosc zostaje do nastepnego przeliczenia (pogoda, sciana, noc, droga) - jak premia drogi.
    ///
    /// KOSZT: sciezka predkosci i postfiks pamieci (watki robocze gry, PartiesThink / TWParallel) - bez alokacji i bez blokad: odczyt pogody (tablica
    /// wezlow gry; NavalDLC przy Clear przeglada liste burz - gra robi to samo w IsBaseSpeedCacheInvalid przy kazdym liczeniu), siatka drog tylko
    /// w deszczu. Liczniki bez blokad (przyblizone "ok.", jak MapRoads), potkniecia przez Interlocked.
    /// POMIAR (tylko log): spis partio-godzin w dobach pomiaru (LogGate.Today), audyt w takcie linii (do 8 partii AI w blocie + gracz: model wprost bez
    /// blota i z blotem), pora nieba i czynnik deszczu gry w linii doby, linia "Bloto (BL): dzien N". Bledy liczone (potkniecia), nigdy wylacznik calej
    /// funkcji. Nic nie idzie do zapisu gry.
    /// Klucze NOWE: MapMudSpeedPenalty (TAK), MapMudSpeedPercent (15, przyciete do 0..50), MapMudWetSpeedPercent (7.5, przyciete do 0..50).
    /// Wylaczony klucz = sciezka sprzed zmiany (Speed i postfiks pamieci wychodza od razu).
    /// </summary>
    internal static class MapMud
    {
        private const string P = "Bloto (BL): ";
        private const int MaxPercent = 50;
        private const int AuditMax = 8;
        private const int SlotBits = 17;
        private const int SlotMask = (1 << SlotBits) - 1;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly TextObject _txtMud = new TextObject("{=!}Mud");          // pada (ulewa, burza nad ladem)
        private static readonly TextObject _txtWet = new TextObject("{=!}Wet ground");   // mokro po deszczu (do ok. doby)

        // audyt: [ThreadStatic] - tylko watek glowny audytu; watki robocze gry widza false
        [ThreadStatic] private static bool _suppress;   // liczenie "bez blota"

        // pamiec partii (stopien pogody ostatniego liczenia zapisanego przez gre): Party.Index << 2 | stopien, -1 pusto
        private static readonly int[] _slots = NewSlots();
        private static bool _cacheHooked;
        private static string _hookNote = "nie wpinana";

        // liczniki z watkow roboczych (przyblizone - bez blokad, jak MapRoads): liczenia, w deszczu, z kara (mokro / pada), w deszczu na drodze,
        // sufit obnizony, probka czasu sciezki (co 64. wywolanie); postfiks pamieci: wywolania, przeliczenia wymuszone zmiana pogody, probka czasu
        private static long _calls, _wet, _mudLight, _mudHeavy, _roadWet, _capLowered, _ticks, _sampled;
        private static long _cacheCalls, _flips, _cacheTicks, _cacheSampled, _recSkipped;
        private static int _parStumbles, _parStumblesAll, _stumbles, _stumblesAll;
        private static Exception _firstPar;

        // spis partio-godzin (watek glowny): 0 rody, 1 karawany, 2 wozy wsi, 3 bandy, 4 inne, 5 gracz - w ruchu / w deszczu / w blocie (pada / mokro)
        private static readonly int[] _hN = new int[6], _hWet = new int[6], _hMudH = new int[6], _hMudL = new int[6];
        private static int _hours;
        private static long _censusTicks;
        private static readonly System.Random _rng = new System.Random(2010);
        private static readonly List<MobileParty> _cand = new List<MobileParty>();
        private static AccessTools.FieldRef<MobileParty, float> _lastSpeedRef;
        private static bool _lastSpeedTried;

        private static int[] NewSlots()
        {
            var a = new int[1 << SlotBits];
            for (int i = 0; i < a.Length; i++) a[i] = -1;
            return a;
        }

        private static bool On(Settings s) { return s != null && s.MapMudSpeedPenalty && s.MapRoadSpeedBonus; }

        private static float Clip(float p)
        {
            if (!(p > 0f)) return 0f;   // takze NaN
            return (p > MaxPercent ? MaxPercent : p) / 100f;
        }

        /// <summary>Kara stopnia pogody: 2 pada - MapMudSpeedPercent, 1 mokro - MapMudWetSpeedPercent, 0 - brak (ulamek 0..0.5).</summary>
        private static float PctOf(Settings s, int lvl)
        {
            return lvl == 2 ? Clip(s.MapMudSpeedPercent) : lvl == 1 ? Clip(s.MapMudWetSpeedPercent) : 0f;
        }

        /// <summary>Stopien pogody w punkcie: 0 sucho (takze snieg, zamiec), 1 LightRain (mokro - padalo w ostatniej dobie), 2 HeavyRain (pada) albo
        /// Storm (burza NavalDLC nad ladem wybrzeza - pada).</summary>
        private static int LevelAt(MapWeatherModel wm, float x, float y)
        {
            var w = wm.GetWeatherEventInPosition(new Vec2(x, y));
            return w == MapWeatherModel.WeatherEvent.HeavyRain || w == MapWeatherModel.WeatherEvent.Storm ? 2
                 : w == MapWeatherModel.WeatherEvent.LightRain ? 1 : 0;
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
        /// <summary>MarchPace.SpeedPostfix (po MapRoads.SpeedRoad, ten sam poziom lancucha): partia ladowa poza droga - pada -x% ("Mud"), mokro po deszczu
        /// -y% ("Wet ground"). Stopien pogody tego liczenia do pamieci partii (postfiks pamieci), gdy gra zapisuje to liczenie.</summary>
        internal static void Speed(MobileParty mp, ref ExplainedNumber r)
        {
            if (_suppress) return;
            var s = Settings.Current;
            if (!On(s)) return;
            if (mp == null || !SpeedDepth.OutermostFinal) return;   // lancuch modeli - liczymy raz, na poziomie zewnetrznym
            long t0 = (++_calls & 63) == 1 ? Stopwatch.GetTimestamp() : 0L;
            try
            {
                if (mp.IsCurrentlyAtSea || mp.IsTransitionInProgress) return;   // morze i przejscie lad-morze - prawa morza
                var c = Campaign.Current;
                if (c == null) return;
                var wm = c.Models.MapWeatherModel;
                if (wm == null) return;
                var p = mp.Position;
                float x = p.X, y = p.Y;
                int lvl = LevelAt(wm, x, y);
                // stopien do pamieci partii tylko z liczenia, ktore gra zapisuje do _lastCalculatedSpeed (straznik zapisu MapRoads)
                if (MapRoads.Recording(mp)) Record(mp, lvl);
                else _recSkipped++;
                if (lvl == 0) return;
                _wet++;
                int kind = MapRoads.KindAt(x, y);   // -1 brak siatki / klucz drog wylaczony, 0 poza droga, 1 boczna, 2 trakt
                if (kind != 0) { if (kind > 0) _roadWet++; return; }
                float pct = PctOf(s, lvl);
                if (pct <= 0f) return;
                float res = r.ResultNumber;
                if (res <= 0f) return;
                float f = 1f + r.SumOfFactors;
                float cut = res * pct;
                float target = res - cut;   // cel: wynik przed kara x (1 - procent)
                r.Add(f > 0.01f ? -cut / f : -cut, lvl == 2 ? _txtMud : _txtWet);
                // inny model trzyma wynik samym LimitMax (bez Add) - kara zginelaby pod sufitem. Recenzja p6-sztormy 20.10: decyzja wzgledem celu PO Add
                // (dotad: tylko gdy wynik PO Add stal na suficie - wynik bez obciecia tuz nad sufitem gubil czesc kary); nie nizej niz LimitMin. Na ladzie
                // gra ma tylko LimitMin (NavalDLC LimitMax 10 - tylko morze), wiec dzis ta galaz praktycznie nie wchodzi
                float lim = r.LimitMaxValue;
                if (lim > 0f && lim < float.MaxValue * 0.5f && res >= lim - 0.0005f && r.ResultNumber > target + 0.001f)
                {
                    float lo = r.LimitMinValue;
                    r.LimitMax(target > lo ? target : lo);
                    _capLowered++;
                }
                if (lvl == 2) _mudHeavy++; else _mudLight++;
            }
            catch (Exception e) { ParStumble(e); }
            finally { if (t0 != 0) { _ticks += Stopwatch.GetTimestamp() - t0; _sampled++; } }
        }

        private static void Record(MobileParty mp, int lvl)
        {
            int idx = mp.Party.Index;
            Volatile.Write(ref _slots[idx & SlotMask], (idx << 2) | (lvl & 3));
        }

        /// <summary>Postfiks MobileParty.IsLastSpeedCacheInvalid (prywatna, DeclaredMethod; obok postfiksu drog): gra uznala pamiec predkosci za dobra -
        /// sprawdzamy, czy stopien pogody w biezacym punkcie jest ten sam co przy ostatnim zapisanym liczeniu tej partii; inny = pamiec niewazna (gra liczy
        /// od nowa, Speed zapisze nowy stopien). Lapie przejscia Wet -> Wet (ulewa <-> mokro, snieg <-> deszcz, burza), ktorych gra nie widzi.</summary>
        public static void CachePostfix(MobileParty __instance, ref bool __result)
        {
            if (__result) return;
            var s = Settings.Current;
            if (!On(s)) return;
            long t0 = (++_cacheCalls & 63) == 1 ? Stopwatch.GetTimestamp() : 0L;
            try
            {
                var mp = __instance;
                if (mp == null || mp.IsCurrentlyAtSea || mp.IsTransitionInProgress) return;
                int idx = mp.Party.Index;
                int v = Volatile.Read(ref _slots[idx & SlotMask]);
                if ((v >> 2) != idx) return;          // pusto (-1) albo inna partia w slocie - decyduje gra
                var c = Campaign.Current;
                if (c == null) return;
                var wm = c.Models.MapWeatherModel;
                if (wm == null) return;
                var p = mp.Position;
                if (LevelAt(wm, p.X, p.Y) != (v & 3)) { __result = true; _flips++; }
            }
            catch (Exception e) { ParStumble(e); }
            finally { if (t0 != 0) { _cacheTicks += Stopwatch.GetTimestamp() - t0; _cacheSampled++; } }
        }

        /// <summary>MapRoads.ApplyAll (menu glowne, po latce pamieci drog - ta sama metoda gry m): postfiks pamieci stopnia pogody. Wpinany zawsze przy
        /// MapRoadSpeedBonus (bloto bez drog i tak nieczynne); klucz blota wylaczony = postfiks wychodzi od razu.</summary>
        internal static void ApplyCache(Harmony h, MethodInfo m)
        {
            try
            {
                if (m == null) { _hookNote = "NIE wpieta (brak metody MobileParty.IsLastSpeedCacheInvalid)"; return; }
                h.Patch(m, postfix: new HarmonyMethod(typeof(MapMud).GetMethod("CachePostfix", BindingFlags.Public | BindingFlags.Static)));
                var pi = Harmony.GetPatchInfo(m);
                bool ok = false;
                if (pi != null) foreach (var x in pi.Postfixes) if (x.PatchMethod != null && x.PatchMethod.DeclaringType == typeof(MapMud)) { ok = true; break; }
                _cacheHooked = ok;
                _hookNote = ok ? "wpieta" : "NIE wpieta (brak postfiksu po Patch)";
            }
            catch (Exception e) { _hookNote = "NIE wpieta (" + e.GetType().Name + ")"; Log.Error("MapMud.ApplyCache", e); }
            Log.Info(P + "latka pamieci predkosci MobileParty.IsLastSpeedCacheInvalid (stopien pogody: sucho / mokro / pada) " + _hookNote
                     + " - zmiana pogody w punkcie partii przelicza predkosc takze przy przejsciach, ktorych gra nie widzi (ulewa <-> mokro, snieg <-> deszcz, burza).");
        }

        // ------------------------------------------------------------------ sesja i doba (watek glowny)
        private static void ZeroDay()
        {
            for (int i = 0; i < _hN.Length; i++) { _hN[i] = 0; _hWet[i] = 0; _hMudH[i] = 0; _hMudL[i] = 0; }
            _hours = 0; _censusTicks = 0;
            ZeroSpeed();
        }

        private static void ZeroSpeed()
        {
            _calls = 0; _wet = 0; _mudLight = 0; _mudHeavy = 0; _roadWet = 0; _capLowered = 0; _ticks = 0; _sampled = 0;
            _cacheCalls = 0; _flips = 0; _cacheTicks = 0; _cacheSampled = 0; _recSkipped = 0;
            _parStumbles = 0; _stumbles = 0;
        }

        /// <summary>Stan kary do linii: CZYNNA / NIECZYNNA z powodem.</summary>
        private static string Status(Settings s)
        {
            if (s == null || !s.MapMudSpeedPenalty) return "WYLACZONA (MapMudSpeedPenalty)";
            if (!s.MapRoadSpeedBonus) return "NIECZYNNA (wymaga MapRoadSpeedBonus - bez siatki drog)";
            if (!MapRoads.Ready) return "NIECZYNNA (siatka drog niewczytana - patrz linia Drogi (DR))";
            if (MapRoads.SpeedModels <= 0) return "NIECZYNNA (latka predkosci MarchPace niewpieta przy starcie gry - potrzebny restart gry)";
            return _cacheHooked ? "CZYNNA" : "CZYNNA (bez latki pamieci pogody - ulewa <-> mokro dopiero przy przeliczeniu gry: sciana, noc, droga)";
        }

        private static string PctText(Settings s)
        {
            return s == null ? "-" : "pada (HeavyRain, burza nad ladem) " + (PctOf(s, 2) * 100f).ToString("0.#", Inv) + "% (wpis Mud), mokro po deszczu (LightRain - do ok. doby) "
                   + (PctOf(s, 1) * 100f).ToString("0.#", Inv) + "% (wpis Wet ground)";
        }

        /// <summary>Start sesji kampanii (OnSessionLaunched, PO MapRoads.SessionStart - siatka drog wczytana): liczniki i pamiec partii od zera, linia startu.</summary>
        internal static void SessionStart()
        {
            ZeroDay();
            for (int i = 0; i < _slots.Length; i++) _slots[i] = -1;   // indeksy partii sa z kampanii
            _parStumblesAll = 0; _stumblesAll = 0; _firstPar = null;
            var s = Settings.Current;
            string wm = "-";
            try { var m = Campaign.Current != null ? Campaign.Current.Models.MapWeatherModel : null; wm = m != null ? m.GetType().FullName : "brak"; } catch (Exception e) { Stumble("SessionStart", e); }
            Log.Info(P + "start sesji - klucz MapMudSpeedPenalty " + (s != null && s.MapMudSpeedPenalty ? "tak" : "nie")
                     + ", kara poza droga: " + PctText(s) + "; snieg i zamiec bez blota"
                     + " | na drodze bez kary: siatka premii drog (trakty" + (s != null && s.MapSideRoadSpeedBonus ? " + drogi boczne" : "") + ", klucz MapRoadSpeedBonus " + (s != null && s.MapRoadSpeedBonus ? "tak" : "nie")
                     + ", siatka " + (MapRoads.Ready ? "wczytana" : "NIE") + ") | latka predkosci MarchPace w " + MapRoads.SpeedModels + " modelach | latka pamieci pogody " + _hookNote
                     + " | model pogody " + wm + " | kara " + Status(s) + ".");
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
                        int lvl = LevelAt(wm, p.X, p.Y);
                        if (lvl == 0) continue;
                        _hWet[k]++;
                        if (MapRoads.KindAt(p.X, p.Y) == 0) { if (lvl == 2) _hMudH[k]++; else _hMudL[k]++; }
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

        /// <summary>Pora nieba (ta, ktora kieruje pogoda - WesterosClimate.SkySeason) i czynnik deszczu gry (0 lato .. 1 od polowy jesieni i w zimie).</summary>
        private static void Sky(StringBuilder sb)
        {
            try
            {
                var t = CampaignTime.Now;
                var season = WesterosClimate.SkySeason(ref t);
                double yp = WesterosClimate.SkySeasons(ref t) % 4.0;
                float sf = 0f, rf = 0f;
                var wm = Campaign.Current != null ? Campaign.Current.Models.MapWeatherModel : null;
                if (wm != null) wm.GetSeasonTimeFactorOfCampaignTime(CampaignTime.Now, out sf, out rf);
                sb.Append(" | pora nieba ").Append(season.ToString()).Append(" (postep roku nieba ").Append(yp.ToString("0.00", Inv))
                  .Append("), czynnik deszczu gry ").Append(rf.ToString("0.00", Inv)).Append(" (0 lato .. 1 od polowy jesieni i w zimie), sniegu ").Append(sf.ToString("0.00", Inv));
            }
            catch (Exception e) { Stumble("Sky", e); sb.Append(" | pora nieba: blad"); }
        }

        private static void Line(Settings s, int day)
        {
            var ci = Inv;
            var sb = new StringBuilder(1400);
            sb.Append(P).Append("dzien ").Append(day)
              .Append(" | klucz MapMudSpeedPenalty tak, kara poza droga: ").Append(PctText(s)).Append(", kara ").Append(Status(s));
            Sky(sb);
            int tot = 0, totW = 0, totH = 0, totL = 0;
            for (int k = 0; k < 5; k++) { tot += _hN[k]; totW += _hWet[k]; totH += _hMudH[k]; totL += _hMudL[k]; }
            int totM = totH + totL;
            sb.Append(" | partio-godziny AI w ruchu na ladzie (godzin spisu ").Append(_hours).Append("): ").Append(tot)
              .Append(", w deszczu ").Append(totW).Append(" (").Append(Pct(totW, tot)).Append("%)")
              .Append(", w blocie (deszcz poza droga) ").Append(totM).Append(" (").Append(Pct(totM, tot)).Append("%; pada ").Append(totH).Append(" = ").Append(Pct(totH, tot))
              .Append("%, mokro ").Append(totL).Append(" = ").Append(Pct(totL, tot)).Append("%)")
              .Append(", w deszczu na drodze (bez kary) ").Append(totW - totM)
              .Append(" | w blocie (pada+mokro) / w ruchu: rody ").Append(_hMudH[0] + _hMudL[0]).Append('/').Append(_hN[0])
              .Append(", karawany ").Append(_hMudH[1] + _hMudL[1]).Append('/').Append(_hN[1])
              .Append(", wozy wsi ").Append(_hMudH[2] + _hMudL[2]).Append('/').Append(_hN[2])
              .Append(", bandy ").Append(_hMudH[3] + _hMudL[3]).Append('/').Append(_hN[3])
              .Append(", inne ").Append(_hMudH[4] + _hMudL[4]).Append('/').Append(_hN[4])
              .Append(" | gracz godzin w ruchu ").Append(_hN[5]).Append(", w deszczu ").Append(_hWet[5]).Append(", w blocie pada ").Append(_hMudH[5]).Append(", mokro ").Append(_hMudL[5]);
            long sc = _calls, mud = _mudLight + _mudHeavy;
            sb.Append(" | liczenia predkosci (ok.) ").Append(sc).Append(", w deszczu ").Append(_wet)
              .Append(", z kara blota ").Append(mud).Append(" (").Append(Pct(mud, sc)).Append("%; mokro ").Append(_mudLight).Append(", pada ").Append(_mudHeavy).Append(')')
              .Append(", w deszczu na drodze bez kary ").Append(_roadWet).Append(", sufit obnizony ").Append(_capLowered)
              .Append(" | pamiec pogody: przeliczenia wymuszone zmiana pogody (ok.) ").Append(_flips).Append(" przy ").Append(_cacheCalls)
              .Append(" wywolaniach postfiksu, liczenia bez zapisu stopnia (nie do pamieci gry) ").Append(_recSkipped);
            Audit(s, sb);
            double censusMs = _censusTicks * 1000.0 / Stopwatch.Frequency;
            double us = _sampled > 0 ? _ticks * 1e6 / Stopwatch.Frequency / _sampled : 0;
            double cus = _cacheSampled > 0 ? _cacheTicks * 1e6 / Stopwatch.Frequency / _cacheSampled : 0;
            sb.Append(" | koszt: spis ").Append(censusMs.ToString("0.000", ci)).Append(" ms/dobe, sciezka predkosci ").Append(us.ToString("0.000", ci))
              .Append(" us/wywolanie (probka ").Append(_sampled).Append("), postfiks pamieci ").Append(cus.ToString("0.000", ci))
              .Append(" us/wywolanie (probka ").Append(_cacheSampled).Append(')')
              .Append(" | potkniecia dzis ").Append(_stumbles).Append(" (od wczytania ").Append(_stumblesAll).Append("), w predkosci dzis ").Append(_parStumbles)
              .Append(" (od wczytania ").Append(_parStumblesAll).Append(")")
              .Append(_firstPar != null ? " pierwsze: " + _firstPar.GetType().Name + " " + (_firstPar.Message ?? "") : "").Append('.');
            Log.Info(sb.ToString());
        }

        /// <summary>Audyt w takcie linii: do 8 losowych partii AI w ruchu w blocie + gracz na ladzie - model predkosci wprost: bez blota (r0) i z blotem
        /// (r1, z opisami): kara rzeczywista 1 - r1/r0 osobno dla "pada" i "mokro", zgodnosc z kara oczekiwana stopnia, wpis "Mud" / "Wet ground",
        /// kara zjedzona (r1 > r0 x (1 - kara) x 1.005), na podlodze LimitMin (poza srednia), pamiec gry (_lastCalculatedSpeed) zgodna z r1 +-2%.</summary>
        private static void Audit(Settings s, StringBuilder sb)
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
                    if (LevelAt(wm, p.X, p.Y) != 0 && MapRoads.KindAt(p.X, p.Y) == 0) _cand.Add(mp);
                }
                catch (Exception e) { Stumble("Audit.Scan", e); }
            }
            int candN = _cand.Count;
            int n = 0, noLine = 0, eaten = 0, atFloor = 0, cacheOk = 0, cacheN = 0;
            int[] nb = { 0, 0, 0 };
            double[] sum = { 0, 0, 0 };
            double ratio = 0, mn = double.MaxValue, mx = double.MinValue;
            int nr = 0;
            int kmax = Math.Min(AuditMax, _cand.Count);
            for (int i = 0; i < kmax; i++)
            {
                int j = i + _rng.Next(_cand.Count - i);
                var tmp = _cand[i]; _cand[i] = _cand[j]; _cand[j] = tmp;
                var cp = _cand[i].Position;
                int lvl;
                try { lvl = LevelAt(wm, cp.X, cp.Y); } catch (Exception e) { Stumble("Audit.Level", e); continue; }
                float exp = PctOf(s, lvl);
                float r0, r1, lmin, cached;
                bool line;
                if (!Probe(model, _cand[i], out r0, out r1, out lmin, out line, out cached)) continue;
                n++;
                if (!line && exp > 0f) noLine++;
                if (cached > 0f) { cacheN++; if (Math.Abs(cached - r1) <= 0.02f * r1) cacheOk++; }
                if (exp <= 0f || r0 * (1f - exp) <= lmin + 0.001f) { atFloor++; continue; }
                double b = r0 > 0.0001f ? 1.0 - r1 / r0 : 0.0;
                nb[lvl]++; sum[lvl] += b;
                double q = b / exp;
                ratio += q; nr++; if (q < mn) mn = q; if (q > mx) mx = q;
                if (r1 > r0 * (1f - exp) * 1.005f) eaten++;
            }
            _cand.Clear();
            sb.Append(" | audyt AI w blocie: kandydatow ").Append(candN)
              .Append("; probek ").Append(n)
              .Append(", kara rzeczywista sr.: pada ").Append(nb[2] > 0 ? (100.0 * sum[2] / nb[2]).ToString("0.00", ci) : "-").Append("% (n ").Append(nb[2]).Append(')')
              .Append(", mokro ").Append(nb[1] > 0 ? (100.0 * sum[1] / nb[1]).ToString("0.00", ci) : "-").Append("% (n ").Append(nb[1]).Append(')')
              .Append(", zgodnosc z kara stopnia sr. ").Append(nr > 0 ? (100.0 * ratio / nr).ToString("0.0", ci) : "-").Append("% (min ").Append(nr > 0 ? (100.0 * mn).ToString("0.0", ci) : "-")
              .Append(", maks ").Append(nr > 0 ? (100.0 * mx).ToString("0.0", ci) : "-").Append(')')
              .Append(", bez wpisu Mud/Wet ground ").Append(noLine).Append(", kara zjedzona ").Append(eaten)
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
                          .Append(", wpis Mud/Wet ground ").Append(line ? "tak" : "nie").Append(", pamiec gry ").Append(cached.ToString("0.000", ci));
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
                string mud = _txtMud.ToString(), wet = _txtWet.ToString();
                foreach (var l in en.GetLines()) if (l.Item1 == mud || l.Item1 == wet) { line = true; break; }
                if (_lastSpeedRef != null) cached = _lastSpeedRef(mp);
                return true;
            }
            catch (Exception e) { Stumble("Audit.Probe", e); return false; }
            finally { _suppress = false; }
        }
    }
}
