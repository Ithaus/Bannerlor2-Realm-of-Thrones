using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace CrashScribe
{
    /// <summary>
    /// 353 / 353b (Jeff 07.11: "tak rob"): StrategicCampaignAI145.StrategicAiHelpers.NearbyEnemyLordStrength liczone przez nas, z pamiecia sily partii na godzine gry.
    ///
    /// DOWOD: probnik stosu ClrMD (300 migawek watku glownego, sesja Jeffa 07.11 ok. 19:20, mapa w biegu, sr. klatka 92 ms wobec 16 ms wczesniej):
    /// 52 ze 137 migawek taktu gry w NearbyEnemyLordStrength, wolanej z StrategicTargetScoreModel.GetTargetScoreForFaction
    /// (NearbyEnemyLordStrength(cel, mobileParty.MapFaction, 95f) > ourStrength * 1.25f) <- AiMilitaryBehavior.FindBestTargetAndItsValueForFaction
    /// <- CalculateMilitaryBehaviorForSettlement <- AiHourlyTick <- ROT AIThinkPatch.PartyHourlyAiTick <- TickPartialHourlyAi.
    /// Cialo (dekompilacja): suma GetTotalLandStrengthWithFollowers(false) po WSZYSTKICH MobileParty.AllLordParties wrogich frakcji w promieniu -
    /// kazda sila idzie przez GetPowerOfParty (NavalDLC, ROT, nasze Armoury p9) -> MobileParty.Morale -> postfiks morale BK (zaopatrzenie BK, MCM, TextObject).
    /// 353 (pamiec wyniku na klucz osada/frakcja/promien) w autoteste 10 dob: z pamieci tylko 11-28%, liczone wolanie 4.0 -> 8.9 ms, 8-16 s na dobe gry.
    /// Ten sam lord liczony jest setki razy na godzine gry (kazdy cel, kazda frakcja pytajaca) - 353b pamieta sile PARTII, nie wynik celu.
    ///
    /// REGULA 353b: prefiks (Priority.First) liczy wynik sam i zwraca false. Te same filtry i kolejnosc co oryginal: MobileParty.AllLordParties w kolejnosci listy,
    /// p.IsActive && p.MapFaction != null && StrategicAiHelpers.IsEnemy(faction, p.MapFaction) && p.GetPosition2D.Distance(settlement.GetPosition2D) <= radius;
    /// suma jak Enumerable.Sum(IEnumerable&lt;float&gt;) z System.Core .NET Framework 4.x (sprawdzone w dekompilacji System.Core.dll z GAC: akumulator double,
    /// na koncu (float)). IsEnemy SAI przez delegat zbudowany raz (bez Invoke na wolanie); bez delegatu latka nie jest wpinana.
    /// Sila partii: slownik partia -> GetTotalLandStrengthWithFollowers(false), czyszczony przy zmianie (int)CampaignTime.Now.ToHours (partie i ich pozycje zawsze biezace,
    /// sila najwyzej sprzed godziny - zgoda Jeffa na pamiec godzinowa). Wpis pamieta tez sklad sumy (partia liczona przy EscortParty, armia, liczba dolaczonych czlonkow u wodza):
    /// powstanie / rozpad armii, dolaczenie czlonka albo eskorta w tej godzinie = sila liczona od nowa (przeglad 353b). Straty w bitwie w godzinie - dalej najwyzej godzina zwloki. Pamiec czyszczona tez przy nowej grze / wczytaniu (konstruktor MendsBehavior), przy OnSessionLaunched
    /// i przy wyjsciu z gry (OnGameEnd). Bez Campaign.Current albo z null w argumentach - oryginal. Wyjatek w naszej petli: potkniecie (licznik), oryginal liczy jak bez latki -
    /// bez globalnego wylacznika. Klucz SaiStrengthCache (settings.xml, domyslnie true); latka wpinana raz na proces przy pierwszym OnSessionLaunched, tylko gdy klucz true i mod jest.
    /// Linia doby: ile wolan, ile policzyla latka, ile oddane oryginalowi, czas naszego prefiksu (Stopwatch), sila partii liczona / z pamieci, 5 celow i 5 frakcji pytajacych.
    /// </summary>
    internal static class SaiStrength353
    {
        private const string P = "Mends: SAI sila wrogow (353) - ";

        // para do linii doby: cel i frakcja pytajaca
        private struct Pair : IEquatable<Pair>
        {
            public Settlement S; public IFaction F;
            public bool Equals(Pair o) { return ReferenceEquals(S, o.S) && ReferenceEquals(F, o.F); }
            public override bool Equals(object o) { return o is Pair && Equals((Pair)o); }
            public override int GetHashCode() { unchecked { return (RuntimeHelpers.GetHashCode(S) * 397) ^ RuntimeHelpers.GetHashCode(F); } }
        }

        // rownosc po referencji (obiekty gry sa jedyne w kampanii; bez cudzych Equals/GetHashCode)
        private sealed class RefEq<T> : IEqualityComparer<T> where T : class
        {
            public bool Equals(T a, T b) { return ReferenceEquals(a, b); }
            public int GetHashCode(T o) { return RuntimeHelpers.GetHashCode(o); }
        }

        private static readonly object _sync = new object();
        // sila partii z pamieci godziny + stan, od ktorego zalezy sklad sumy GetTotalLandStrengthWithFollowers(false) (dekompilacja MobileParty):
        // partia liczona (EscortParty -> TargetParty), jej armia, a gdy jest wodzem - ile czlonkow dolaczonych (AttachedTo != null). Inny stan = liczone od nowa.
        private struct Snap
        {
            public float S; public MobileParty Eff; public Army A; public int N;
        }
        private static readonly Dictionary<MobileParty, Snap> _str = new Dictionary<MobileParty, Snap>(new RefEq<MobileParty>());
        private static readonly Dictionary<Pair, int> _dTargets = new Dictionary<Pair, int>();
        private static readonly Dictionary<IFaction, int> _dFactions = new Dictionary<IFaction, int>(new RefEq<IFaction>());
        private static int _hour = int.MinValue;

        private static bool _tried, _patched;
        private static string _state = "nie wpinane";
        private static MethodInfo _target;   // ten sam MethodInfo do Patch, GetPatchInfo i Unpatch
        private static Func<IFaction, IFaction, bool> _isEnemy;   // StrategicAiHelpers.IsEnemy SAI, delegat zbudowany raz

        // liczniki doby (d) i od wczytania (t): wolan, policzone przez latke, oddane oryginalowi, czas prefiksu (tylko policzone), sila partii liczona / z pamieci
        private static long _dCalls, _dOurs, _dOrig, _dTicks, _dPCalc, _dPHit;
        private static long _tCalls, _tOurs, _tOrig, _tTicks, _tPCalc, _tPHit;
        private static int _dStumbles, _tStumbles, _pStumbles;   // p = od startu procesu

        /// <summary>Nowa gra / wczytanie (konstruktor MendsBehavior) i wyjscie z gry: pamiec i liczniki od zera. Nie rzuca.</summary>
        internal static void Reset()
        {
            try
            {
                lock (_sync)
                {
                    _str.Clear(); _dTargets.Clear(); _dFactions.Clear(); _hour = int.MinValue;
                    _dCalls = _dOurs = _dOrig = _dTicks = _dPCalc = _dPHit = 0;
                    _tCalls = _tOurs = _tOrig = _tTicks = _tPCalc = _tPHit = 0;
                    _dStumbles = _tStumbles = 0;
                }
            }
            catch { }
        }

        /// <summary>OnSessionLaunched: wpiecie latki (raz na proces) i linia startu. Nie rzuca.</summary>
        internal static void Session()
        {
            try
            {
                lock (_sync) { _str.Clear(); _hour = int.MinValue; }
                if (!Config.SaiStrengthCache)
                {
                    Scribe.Line(P + "wylaczone (SaiStrengthCache=false)" + (_patched ? ", ale latka wpieta wczesniej w tym procesie - dziala do restartu gry" : ", latka nie wpieta") + ".");
                    return;
                }
                Hook();
                if (!_patched) { Scribe.Line(P + _state + " - nic nie zmienione."); return; }
                Scribe.Line(P + "353b: suma liczona przez latke (filtry i kolejnosc jak oryginal, IsEnemy SAI), sila partii z pamieci na godzine gry - wpieta TAK (" + _state + ")"
                            + "; wyjatek w petli = oryginal | potkniecia od startu procesu " + _pStumbles + ".");
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
                // IsEnemy SAI: delegat raz; bez niego nie wpinamy (nie zgadujemy reguly wroga)
                var ie = AccessTools.DeclaredMethod(t, "IsEnemy", new[] { typeof(IFaction), typeof(IFaction) });
                if (ie == null || !ie.IsStatic || ie.ReturnType != typeof(bool)) { _state = "brak metody StrategicAiHelpers.IsEnemy(IFaction, IFaction) -> bool (inna wersja moda)"; return; }
                try { _isEnemy = (Func<IFaction, IFaction, bool>)Delegate.CreateDelegate(typeof(Func<IFaction, IFaction, bool>), ie); }
                catch (Exception e) { _state = "nie da sie zbudowac delegatu StrategicAiHelpers.IsEnemy (" + e.GetType().Name + ")"; return; }
                if (_isEnemy == null) { _state = "nie da sie zbudowac delegatu StrategicAiHelpers.IsEnemy"; return; }
                _target = m;
                var pre = AccessTools.DeclaredMethod(typeof(SaiStrength353), nameof(Prefix));
                h.Patch(_target, prefix: new HarmonyMethod(pre) { priority = Priority.First });
                // pulapka CLAUDE.md (15.10): Patch potrafi nie wejsc po cichu - sprawdzamy w Harmony, ze prefiks jest na celu
                var info = Harmony.GetPatchInfo(_target);
                bool preOk = false; int others = 0;
                if (info != null)
                {
                    foreach (var p in info.Prefixes) { if (p.owner == h.Id && p.PatchMethod == pre) preOk = true; else others++; }
                    others += info.Postfixes.Count + info.Transpilers.Count + info.Finalizers.Count;
                }
                if (!preOk)
                {
                    _state = "OSTRZEZENIE: Patch bez bledu, ale Harmony.GetPatchInfo nie widzi prefiksu na StrategicAiHelpers.NearbyEnemyLordStrength";
                    return;
                }
                _patched = true;
                _state = m.DeclaringType.FullName + ".NearbyEnemyLordStrength, prefiks potwierdzony w Harmony.GetPatchInfo, IsEnemy jako delegat, cudzych latek na celu " + others;
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
            lock (_sync) { _dStumbles++; _tStumbles++; _dOrig++; _tOrig++; n = ++_pStumbles; }
            if (n <= 3) { try { Scribe.Line(P + where + ": wyjatek " + e.GetType().Name + " (" + e.Message + ") - oryginal liczy jak bez latki (potkniecie " + n + ")."); } catch { } }
        }

        /// <summary>Prefiks: liczy sume sam (false) - te same filtry, kolejnosc i akumulator co oryginal; sila partii z pamieci tej godziny. Wyjatek -> true (oryginal).</summary>
        public static bool Prefix(Settlement __0, IFaction __1, float __2, ref float __result)
        {
            try
            {
                if (__0 == null || __1 == null || Campaign.Current == null || _isEnemy == null)
                {
                    lock (_sync) { _dCalls++; _tCalls++; _dOrig++; _tOrig++; }
                    return true;
                }
                long t0 = Stopwatch.GetTimestamp();
                int hour = (int)CampaignTime.Now.ToHours;
                var pr = new Pair { S = __0, F = __1 };
                lock (_sync)   // wolanie tylko z watku glownego (probnik 353); lock bez rywali = tani; Monitor jest wielowejsciowy (reentrant), wiec ponowne wejscie z cudzego kodu w petli nie zakleszczy
                {
                    if (hour != _hour) { _str.Clear(); _hour = hour; }
                    _dCalls++; _tCalls++;
                    int c;
                    _dTargets.TryGetValue(pr, out c); _dTargets[pr] = c + 1;
                    _dFactions.TryGetValue(__1, out c); _dFactions[__1] = c + 1;

                    Vec2 at = __0.GetPosition2D;   // wlasciwosc bez skutkow ubocznych - ta sama wartosc co w kazdym obrocie lambdy oryginalu
                    double sum = 0.0;   // jak Enumerable.Sum(IEnumerable<float>) .NET Framework: double, na koncu (float)
                    long calc = 0, hit = 0;
                    foreach (MobileParty p in MobileParty.AllLordParties)
                    {
                        if (!p.IsActive) continue;
                        IFaction mf = p.MapFaction;
                        if (mf == null) continue;
                        if (!_isEnemy(__1, mf)) continue;
                        if (!(p.GetPosition2D.Distance(at) <= __2)) continue;
                        Snap now = State(p);
                        Snap old;
                        float s;
                        if (_str.TryGetValue(p, out old) && ReferenceEquals(old.Eff, now.Eff) && ReferenceEquals(old.A, now.A) && old.N == now.N) { s = old.S; hit++; }
                        else { s = p.GetTotalLandStrengthWithFollowers(false); now.S = s; _str[p] = now; calc++; }
                        sum += (double)s;
                    }
                    __result = (float)sum;
                    long dt = Stopwatch.GetTimestamp() - t0;
                    _dOurs++; _tOurs++; _dTicks += dt; _tTicks += dt;
                    _dPCalc += calc; _tPCalc += calc; _dPHit += hit; _tPHit += hit;
                }
                return false;
            }
            catch (Exception e) { Stumble("prefiks", e); return true; }
        }

        /// <summary>Stan skladu sumy partii (bez GetPowerOfParty): te same warunki co GetTotalLandStrengthWithFollowers(false) w MobileParty 1.4.8.</summary>
        private static Snap State(MobileParty p)
        {
            var r = new Snap { Eff = p.DefaultBehavior == AiBehavior.EscortParty ? p.TargetParty : p, N = -1 };
            if (r.Eff == null) return r;   // oryginal rzuci przy liczeniu - ten sam wyjatek i potkniecie, jak bez pamieci
            r.A = r.Eff.Army;
            if (r.A != null && r.Eff == r.A.LeaderParty)
            {
                int n = 0;
                foreach (MobileParty m in r.A.Parties)
                    if (m != null && m.Army != null && m.Army.LeaderParty != m && m.AttachedTo != null) n++;
                r.N = n;
            }
            return r;
        }

        /// <summary>DailyTickEvent: linia doby (tylko przy wpietej latce), liczniki doby od zera. Nie rzuca.</summary>
        internal static void Daily()
        {
            if (!_patched) return;
            try
            {
                long calls, ours, orig, ticks, pc, ph, tc, to, tor, tt, tpc, tph; int st, tst;
                List<KeyValuePair<Pair, int>> targets; List<KeyValuePair<IFaction, int>> facs;
                lock (_sync)
                {
                    calls = _dCalls; ours = _dOurs; orig = _dOrig; ticks = _dTicks; pc = _dPCalc; ph = _dPHit; st = _dStumbles;
                    tc = _tCalls; to = _tOurs; tor = _tOrig; tt = _tTicks; tpc = _tPCalc; tph = _tPHit; tst = _tStumbles;
                    targets = new List<KeyValuePair<Pair, int>>(_dTargets);
                    facs = new List<KeyValuePair<IFaction, int>>(_dFactions);
                    _dCalls = _dOurs = _dOrig = _dTicks = _dPCalc = _dPHit = 0; _dStumbles = 0;
                    _dTargets.Clear(); _dFactions.Clear();
                }
                targets.Sort((a, b) => b.Value.CompareTo(a.Value));
                facs.Sort((a, b) => b.Value.CompareTo(a.Value));
                var ci = System.Globalization.CultureInfo.InvariantCulture;
                var sb = new System.Text.StringBuilder(P);
                sb.Append("dzien ").Append((int)CampaignTime.Now.ToDays).Append(" (ostatnia doba)");
                sb.Append(" | wolan ").Append(calls).Append(", liczone przez latke ").Append(ours).Append(", oddane oryginalowi ").Append(orig);
                sb.Append(", czas latki ").Append(Ms(ticks)).Append(" ms (srednio ").Append(ours > 0 ? (ticks * 1000.0 / Stopwatch.Frequency / ours).ToString("0.####", ci) : "0").Append(" ms na wolanie)");
                sb.Append(" | sila partii: liczona ").Append(pc).Append(", z pamieci ").Append(ph).Append(" (").Append(Pct(ph, pc + ph)).Append("%)");
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
                sb.Append(" | od wczytania: wolan ").Append(tc).Append(", liczone przez latke ").Append(to).Append(", oddane oryginalowi ").Append(tor)
                  .Append(", czas latki ").Append(Ms(tt)).Append(" ms, sila partii liczona ").Append(tpc).Append(", z pamieci ").Append(tph).Append(" (").Append(Pct(tph, tpc + tph)).Append("%)")
                  .Append(" | potkniecia doby ").Append(st).Append(", od wczytania ").Append(tst).Append(".");
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
