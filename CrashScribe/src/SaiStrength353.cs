using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace CrashScribe
{
    /// <summary>
    /// 353 (Jeff 07.11: "tak rob"): pamiec StrategicCampaignAI145.StrategicAiHelpers.NearbyEnemyLordStrength na godzine gry.
    ///
    /// DOWOD: probnik stosu ClrMD (300 migawek watku glownego, sesja Jeffa 07.11 ok. 19:20, mapa w biegu, sr. klatka 92 ms wobec 16 ms wczesniej):
    /// 52 ze 137 migawek taktu gry w NearbyEnemyLordStrength, wolanej z StrategicTargetScoreModel.GetTargetScoreForFaction
    /// (NearbyEnemyLordStrength(cel, mobileParty.MapFaction, 95f) > ourStrength * 1.25f) <- AiMilitaryBehavior.FindBestTargetAndItsValueForFaction
    /// <- CalculateMilitaryBehaviorForSettlement <- AiHourlyTick <- ROT AIThinkPatch.PartyHourlyAiTick <- TickPartialHourlyAi.
    /// Cialo (dekompilacja): suma GetTotalLandStrengthWithFollowers(false) po WSZYSTKICH MobileParty.AllLordParties wrogich frakcji w promieniu -
    /// kazda sila idzie przez GetPowerOfParty (NavalDLC, ROT, nasze Armoury p9) -> MobileParty.Morale -> postfiks morale BK (zaopatrzenie BK, MCM, TextObject).
    /// Ta sama osada, ta sama frakcja i ten sam promien w jednej godzinie gry pytane sa wielokrotnie (kazda partia AI tej frakcji ocenia te same cele).
    ///
    /// REGULA: prefiks (Priority.First) + postfiks (Priority.Last) na celu; klucz (osada, frakcja, promien) -> wynik ORYGINALU z pierwszego wolania w tej
    /// godzinie (int CampaignTime.Now.ToHours). Wartosci nie zmieniamy nigdy - przy trafieniu oddajemy liczbe policzona przez sam oryginal w tej godzinie
    /// (partie poruszaja sie w ciagu godziny - to jedyna roznica, zgoda Jeffa). Pamiec czyszczona przy zmianie godziny, przy nowej grze / wczytaniu
    /// (konstruktor MendsBehavior), przy OnSessionLaunched i przy wyjsciu z gry (OnGameEnd). Bez Campaign.Current - zawsze oryginal, bez liczenia.
    /// Wyjatek w naszym kodzie: potkniecie (licznik), oryginal liczy jak bez latki - bez globalnego wylacznika.
    /// Klucz SaiStrengthCache (settings.xml, domyslnie true); latki wpinane raz na proces przy pierwszym OnSessionLaunched, tylko gdy klucz true i mod jest.
    /// Linia doby: ile wolan, ile z pamieci, czas liczenia (Stopwatch: prefiks -> postfiks, tylko liczone), 5 najczestszych celow i 5 frakcji pytajacych.
    /// </summary>
    internal static class SaiStrength353
    {
        private const string P = "Mends: SAI sila wrogow (353) - ";

        // klucz pamieci: osada, frakcja, promien (rownosc po referencji - obiekty gry sa jedyne w kampanii)
        private struct Key : IEquatable<Key>
        {
            public Settlement S; public IFaction F; public float R;
            public bool Equals(Key o) { return ReferenceEquals(S, o.S) && ReferenceEquals(F, o.F) && R == o.R; }
            public override bool Equals(object o) { return o is Key && Equals((Key)o); }
            public override int GetHashCode()
            {
                unchecked { return (RuntimeHelpers.GetHashCode(S) * 397) ^ (RuntimeHelpers.GetHashCode(F) * 31) ^ R.GetHashCode(); }
            }
        }

        // para do linii doby: cel i frakcja pytajaca
        private struct Pair : IEquatable<Pair>
        {
            public Settlement S; public IFaction F;
            public bool Equals(Pair o) { return ReferenceEquals(S, o.S) && ReferenceEquals(F, o.F); }
            public override bool Equals(object o) { return o is Pair && Equals((Pair)o); }
            public override int GetHashCode() { unchecked { return (RuntimeHelpers.GetHashCode(S) * 397) ^ RuntimeHelpers.GetHashCode(F); } }
        }

        private sealed class RefEq : IEqualityComparer<IFaction>
        {
            public bool Equals(IFaction a, IFaction b) { return ReferenceEquals(a, b); }
            public int GetHashCode(IFaction f) { return RuntimeHelpers.GetHashCode(f); }
        }

        private static readonly object _sync = new object();
        private static readonly Dictionary<Key, float> _cache = new Dictionary<Key, float>();
        private static readonly Dictionary<Pair, int> _dTargets = new Dictionary<Pair, int>();
        private static readonly Dictionary<IFaction, int> _dFactions = new Dictionary<IFaction, int>(new RefEq());
        private static int _hour = int.MinValue;

        private static bool _tried, _patched;
        private static string _state = "nie wpinane";
        private static MethodInfo _target;   // ten sam MethodInfo do Patch i GetPatchInfo

        // liczniki doby (d) i od wczytania (t)
        private static long _dCalls, _dHits, _dMiss, _dTicks, _tCalls, _tHits, _tMiss, _tTicks;
        private static int _dStumbles, _tStumbles, _pStumbles;   // p = od startu procesu

        /// <summary>Nowa gra / wczytanie (konstruktor MendsBehavior) i wyjscie z gry: pamiec i liczniki od zera. Nie rzuca.</summary>
        internal static void Reset()
        {
            try
            {
                lock (_sync)
                {
                    _cache.Clear(); _dTargets.Clear(); _dFactions.Clear(); _hour = int.MinValue;
                    _dCalls = _dHits = _dMiss = _dTicks = 0; _tCalls = _tHits = _tMiss = _tTicks = 0;
                    _dStumbles = _tStumbles = 0;
                }
            }
            catch { }
        }

        /// <summary>OnSessionLaunched: wpiecie latek (raz na proces) i linia startu. Nie rzuca.</summary>
        internal static void Session()
        {
            try
            {
                lock (_sync) { _cache.Clear(); _hour = int.MinValue; }
                if (!Config.SaiStrengthCache)
                {
                    Scribe.Line(P + "wylaczone (SaiStrengthCache=false)" + (_patched ? ", ale latka wpieta wczesniej w tym procesie - dziala do restartu gry" : ", latka nie wpieta") + ".");
                    return;
                }
                Hook();
                if (!_patched) { Scribe.Line(P + _state + " - nic nie zmienione."); return; }
                Scribe.Line(P + "pamiec wyniku na godzine gry wpieta TAK (" + _state + "); klucz osada / frakcja / promien, wartosc liczy zawsze oryginal"
                            + " | potkniecia od startu procesu " + _pStumbles + ".");
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "SaiStrength353.Session", null); } catch { } }
        }

        private static void Hook()
        {
            if (_tried) return;
            _tried = true;
            try
            {
                var h = Mends.LateHarmony;
                if (h == null) { _state = "brak Harmony CrashScribe"; return; }
                var t = AccessTools.TypeByName("StrategicCampaignAI145.StrategicAiHelpers");
                if (t == null) { _state = "StrategicCampaignAI145 nieobecny (brak typu StrategicCampaignAI145.StrategicAiHelpers)"; return; }
                var m = AccessTools.DeclaredMethod(t, "NearbyEnemyLordStrength", new[] { typeof(Settlement), typeof(IFaction), typeof(float) });
                if (m == null || !m.IsStatic || m.ReturnType != typeof(float)) { _state = "brak metody StrategicAiHelpers.NearbyEnemyLordStrength(Settlement, IFaction, float) -> float (inna wersja moda)"; return; }
                _target = m;
                var pre = AccessTools.DeclaredMethod(typeof(SaiStrength353), nameof(Prefix));
                var post = AccessTools.DeclaredMethod(typeof(SaiStrength353), nameof(Postfix));
                h.Patch(_target, prefix: new HarmonyMethod(pre) { priority = Priority.First }, postfix: new HarmonyMethod(post) { priority = Priority.Last });
                // pulapka CLAUDE.md (15.10): Patch potrafi nie wejsc po cichu - sprawdzamy w Harmony, ze oba sa na celu
                var info = Harmony.GetPatchInfo(_target);
                bool preOk = false, postOk = false; int others = 0;
                if (info != null)
                {
                    foreach (var p in info.Prefixes) { if (p.owner == h.Id && p.PatchMethod == pre) preOk = true; else others++; }
                    foreach (var p in info.Postfixes) { if (p.owner == h.Id && p.PatchMethod == post) postOk = true; else others++; }
                    others += info.Transpilers.Count + info.Finalizers.Count;
                }
                if (!preOk || !postOk)
                {
                    _state = "OSTRZEZENIE: Patch bez bledu, ale Harmony.GetPatchInfo nie widzi " + (!preOk ? "prefiksu" : "") + (!preOk && !postOk ? " i " : "") + (!postOk ? "postfiksu" : "")
                             + " na StrategicAiHelpers.NearbyEnemyLordStrength";
                    if (preOk || postOk) { try { h.Unpatch(_target, preOk ? pre : post); } catch { } }   // pol latki nie zostawiamy (ten sam MethodInfo)
                    return;
                }
                _patched = true;
                _state = m.DeclaringType.FullName + ".NearbyEnemyLordStrength, prefiks i postfiks potwierdzone w Harmony.GetPatchInfo, cudzych latek na celu " + others;
            }
            catch (Exception e)
            {
                _state = "wyjatek przy wpinaniu " + e.GetType().Name;
                try { Scribe.Report("CrashScribe", e, "SaiStrength353.Hook", null); } catch { }
            }
        }

        private static void Stumble(string where, Exception e)
        {
            int n;
            lock (_sync) { _dStumbles++; _tStumbles++; n = ++_pStumbles; }
            if (n <= 3) { try { Scribe.Line(P + where + ": wyjatek " + e.GetType().Name + " (" + e.Message + ") - oryginal liczy jak bez latki (potkniecie " + n + ")."); } catch { } }
        }

        /// <summary>Prefiks: trafienie w pamieci tej godziny -> wynik oryginalu z pamieci (bez liczenia); inaczej oryginal (start Stopwatch w __state).</summary>
        public static bool Prefix(Settlement __0, IFaction __1, float __2, ref float __result, out long __state)
        {
            __state = 0;
            try
            {
                if (__0 == null || __1 == null || Campaign.Current == null) return true;
                int hour = (int)CampaignTime.Now.ToHours;
                var k = new Key { S = __0, F = __1, R = __2 };
                var pr = new Pair { S = __0, F = __1 };
                lock (_sync)
                {
                    if (hour != _hour) { _cache.Clear(); _hour = hour; }
                    _dCalls++; _tCalls++;
                    int c;
                    _dTargets.TryGetValue(pr, out c); _dTargets[pr] = c + 1;
                    _dFactions.TryGetValue(__1, out c); _dFactions[__1] = c + 1;
                    float v;
                    if (_cache.TryGetValue(k, out v)) { _dHits++; _tHits++; __result = v; return false; }
                }
                __state = Stopwatch.GetTimestamp();   // zawsze > 0
                return true;
            }
            catch (Exception e) { __state = 0; Stumble("prefiks", e); return true; }
        }

        /// <summary>Postfiks: tylko po liczeniu oryginalu (__state > 0) - czas i zapis wyniku do pamieci tej godziny. Wyniku nie zmienia.</summary>
        public static void Postfix(Settlement __0, IFaction __1, float __2, float __result, long __state)
        {
            if (__state == 0) return;
            try
            {
                long dt = Stopwatch.GetTimestamp() - __state;
                if (Campaign.Current == null) return;
                int hour = (int)CampaignTime.Now.ToHours;
                lock (_sync)
                {
                    _dMiss++; _tMiss++; _dTicks += dt; _tTicks += dt;
                    if (hour != _hour) { _cache.Clear(); _hour = hour; }
                    _cache[new Key { S = __0, F = __1, R = __2 }] = __result;
                }
            }
            catch (Exception e) { Stumble("postfiks", e); }
        }

        /// <summary>DailyTickEvent: linia doby (tylko przy wpietej latce), liczniki doby od zera. Nie rzuca.</summary>
        internal static void Daily()
        {
            if (!_patched) return;
            try
            {
                long calls, hits, miss, ticks, tc, th, tm, tt; int st, tst;
                List<KeyValuePair<Pair, int>> targets; List<KeyValuePair<IFaction, int>> facs;
                lock (_sync)
                {
                    calls = _dCalls; hits = _dHits; miss = _dMiss; ticks = _dTicks; st = _dStumbles;
                    tc = _tCalls; th = _tHits; tm = _tMiss; tt = _tTicks; tst = _tStumbles;
                    targets = new List<KeyValuePair<Pair, int>>(_dTargets);
                    facs = new List<KeyValuePair<IFaction, int>>(_dFactions);
                    _dCalls = _dHits = _dMiss = _dTicks = 0; _dStumbles = 0;
                    _dTargets.Clear(); _dFactions.Clear();
                }
                targets.Sort((a, b) => b.Value.CompareTo(a.Value));
                facs.Sort((a, b) => b.Value.CompareTo(a.Value));
                var sb = new System.Text.StringBuilder(P);
                sb.Append("dzien ").Append((int)CampaignTime.Now.ToDays).Append(" (ostatnia doba)");
                sb.Append(" | wolan ").Append(calls).Append(", z pamieci ").Append(hits).Append(" (").Append(Pct(hits, calls)).Append("%), liczone ").Append(miss);
                sb.Append(", czas liczenia ").Append(Ms(ticks)).Append(" ms (srednio ").Append(miss > 0 ? (Ms(ticks) / miss).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) : "0").Append(" ms)");
                sb.Append(" | najczestsze cele: ");
                for (int i = 0; i < targets.Count && i < 5; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append(Nm(targets[i].Key.S)).Append(" / ").Append(Fn(targets[i].Key.F)).Append(" x").Append(targets[i].Value);
                }
                if (targets.Count == 0) sb.Append("brak");
                sb.Append(" (par cel-frakcja ").Append(targets.Count).Append(")");
                sb.Append(" | frakcje pytajace: ");
                for (int i = 0; i < facs.Count && i < 5; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append(Fn(facs[i].Key)).Append(" x").Append(facs[i].Value);
                }
                if (facs.Count == 0) sb.Append("brak");
                sb.Append(" (frakcji ").Append(facs.Count).Append(")");
                sb.Append(" | od wczytania: wolan ").Append(tc).Append(", z pamieci ").Append(th).Append(" (").Append(Pct(th, tc)).Append("%), liczone ").Append(tm)
                  .Append(", czas liczenia ").Append(Ms(tt)).Append(" ms | potkniecia doby ").Append(st).Append(", od wczytania ").Append(tst).Append(".");
                Scribe.Line(sb.ToString());
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "SaiStrength353.Daily", null); } catch { } }
        }

        private static string Pct(long a, long b) { return b > 0 ? (100.0 * a / b).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) : "0"; }
        private static double Ms(long ticks) { return Math.Round(ticks * 1000.0 / Stopwatch.Frequency, 1); }
        private static string Nm(Settlement s)
        {
            try { return s == null ? "?" : (s.Name != null ? s.Name.ToString() : "?") + " (" + s.StringId + ")"; } catch { return "?"; }
        }
        private static string Fn(IFaction f)
        {
            try { return f == null ? "?" : (f.Name != null ? f.Name.ToString() : "?") + " (" + f.StringId + ")"; } catch { return "?"; }
        }
    }
}
