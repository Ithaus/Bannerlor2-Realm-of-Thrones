using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.MountAndBlade;
using R = CrashScribe.UnwindCore.R;

namespace CrashScribe
{
    /// <summary>UW: skan naprawy opisu stosu po starcie sesji (zlecony na pierwszy tick po WSZYSTKICH sluchaczach OnSessionLaunched,
    /// takze innych modow, ktore wtedy latkuja) i raz na dobe.</summary>
    internal sealed class UnwindRepairBehavior : CampaignBehaviorBase
    {
        private int _day = -1;
        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, _ => { try { UnwindRepair.Request("sesja"); } catch { } });
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, () =>
            {
                try { int d = (int)CampaignTime.Now.ToDays; if (d == _day) return; _day = d; UnwindRepair.Scan("doba", false); } catch { }
            });
        }
        public override void SyncData(IDataStore dataStore) { }
    }

    /// <summary>
    /// NAPRAWA OPISU STOSU (UW) - zawieszenie gry 19.10 16:09.
    ///
    /// Co sie stalo (prostymi slowami): mala metoda (np. pusty SyncData, lambda "() => {}", MBSubModuleBase.OnNetworkTick) ma po
    /// skompilowaniu 1-4 bajty kodu. Harmony, zakladajac na niej latke, wkleja na jej poczatek 5-bajtowy skok. Tuz za tak krotkim
    /// kodem lezy "opis stosu" (UNWIND_INFO), ktorego Windows uzywa, gdy musi przejsc po stosie wywolan - a jego pierwszy bajt
    /// (oryginalnie 0x19) wypada dokladnie pod tym skokiem i skok go psuje (na 0xFD). Dopoki nikt tam nie zaglada, gra dziala. Ale gdy
    /// odsmiecanie pamieci (GC) zatrzymuje watek stojacy akurat na tym skoku i Windows 11 25H2 probuje przejsc jego stos, widzi zepsuty
    /// opis i rzuca - a odpowiedz na to wymaga nowej pamieci, ktorej nie da sie przydzielic w trakcie GC. Gra zawisa na zawsze.
    ///
    /// Naprawa (sposob C, wybrany po tescie w uprzezy - patrz docs/noc-1920/zawieszenie-CHANGELOG.md): przepisujemy skok tak, by
    /// prowadzil przez wlasna trampoline polozona tak, ze wysoki bajt przesuniecia skoku (ten na +4) znow rowna sie 0x19. Opis stosu
    /// jest wtedy poprawny, a skok dalej trafia tam, gdzie trafial - latka dziala bez zmian. Rdzen (bez zaleznosci od gry): UnwindCore.cs.
    ///
    /// Ktore metody: kazda zalatana (Harmony.GetAllPatchedMethods), ktorej opis stosu lezy pod skokiem na jej poczatku. Polozenie
    /// opisu czytamy z naglowka kodu JIT (RealCodeHeader: phdrMDesc musi rownac sie MethodHandle.Value), dlugosc skoku z jego bajtow.
    /// Naprawiamy tylko skok E9 przy opisie na +4 (jedyny przypadek widziany w grze: 142 z 142); inne nakladki liczymy i wypisujemy
    /// z nazwa metody. Piszemy do pamieci TYLKO przy dokladnym wzorcu; przy niezgodnosci metoda zostaje jak byla i jest liczona w logu.
    ///
    /// Kiedy: zlecony skan na pierwszy OnApplicationTick po OnBeforeInitialModuleScreenSetAsRoot (wszystkie mody juz zalatane) i po
    /// OnSessionLaunched (wszystkie sluchacze), raz na dobe, pelny skan co 5 min (nowe metody - np. latki zakladane w bitwie; znane z pamieci
    /// podrecznej, wiec tanio) i lekka straz co 30 s (bajty znanych malych metod).
    /// </summary>
    internal static class UnwindRepair
    {
        const int UwIlMax = 64;             // WLASNY prog wstepny (IL, bajty) - tylko by nie czytac naglowkow 16 tys. duzych metod; niezalezny od sieci (NetTinyIlBytes)
        const int LightMs = 30000;          // lekka straz: porownanie bajtow znanych malych metod
        const int FullMs = 300000;          // pelny skan co 5 min

        // -1 = bezpieczna na zawsze (duza albo opis stosu daleko), -2 = nierozpoznana (brak zgodnego naglowka kodu), >= 0 = indeks w _small
        private static readonly Dictionary<MethodBase, int> _known = new Dictionary<MethodBase, int>();
        private static readonly List<UnwindCore.Small> _small = new List<UnwindCore.Small>();
        private static readonly List<string> _unknownNames = new List<string>();
        private static string _pending;
        private static int _nextLightMs, _nextFullMs;
        private static bool _offLogged;
        private static int _lastSkipCount = -1, _lastUnknown = -1;

        /// <summary>Zlec pelny skan na najblizszy OnApplicationTick (po wszystkich modach w biezacej chwili latkowania).</summary>
        internal static void Request(string phase) { _pending = phase; }

        /// <summary>Z OnApplicationTick (co klatke): zlecony skan, pelny skan co 5 min, lekka straz co 30 s. Poza tymi chwilami
        /// dwa porownania Environment.TickCount, bez alokacji.</summary>
        internal static void Tick()
        {
            string p = _pending;
            if (p != null) { _pending = null; Scan(p, false); return; }   // przy UnwindRepair=false Scan pisze raz linie "naprawa WYLACZONA"
            if (!Config.UnwindRepair) return;
            int now = Environment.TickCount;
            if (_nextLightMs == 0 && _nextFullMs == 0) { _nextLightMs = now + LightMs; _nextFullMs = now + FullMs; return; }
            if (unchecked(now - _nextFullMs) >= 0) { _nextFullMs = now + FullMs; _nextLightMs = now + LightMs; Scan("5 min", true); return; }
            if (unchecked(now - _nextLightMs) < 0) return;
            _nextLightMs = now + LightMs;
            Light();
        }

        /// <summary>Pelny skan wszystkich zalatanych metod. quiet = linia tylko, gdy cos sie zmienilo (skan co 5 min).</summary>
        internal static void Scan(string phase, bool quiet)
        {
            if (!Config.UnwindRepair)
            {
                if (!_offLogged) { _offLogged = true; Scribe.Line("Opis stosu (UW): naprawa WYLACZONA (klucz UnwindRepair=false) - male zalatane metody z zepsutym opisem stosu zostaja."); }
                return;
            }
            int patched = 0, small = 0, overlap = 0, fixedN = 0, already = 0, unknown = 0, skipped = 0;
            var skip = new Dictionary<string, int>();
            var skipNames = new List<string>();
            string sApp = "bez latki", sAsync = "bez latki", sNet = "bez latki";
            var mbsb = typeof(MBSubModuleBase);
            try
            {
                IEnumerable<MethodBase> all;
                try { all = Harmony.GetAllPatchedMethods(); } catch (Exception e) { Scribe.Line("Opis stosu (UW, " + phase + "): nie dostalem listy zalatanych metod (" + e.GetType().Name + ")."); return; }
                foreach (var m in all)
                {
                    if (m == null) continue;
                    patched++;
                    string st;
                    try
                    {
                        int idx;
                        if (!_known.TryGetValue(m, out idx)) { idx = Classify(m); _known[m] = idx; }
                        if (idx == -1) st = "bez nakladki";
                        else if (idx == -2) { unknown++; st = "nierozpoznana"; }
                        else
                        {
                            small++;
                            var s = _small[idx];
                            string why;
                            R r = UnwindCore.Check(s, out why);
                            switch (r)
                            {
                                case R.NoOverlap: st = "bez nakladki"; break;
                                case R.Repaired: fixedN++; overlap++; st = "naprawiona"; break;
                                case R.AlreadyOk: already++; overlap++; st = "dobra"; break;
                                default:
                                    overlap++; skipped++;
                                    string w = why ?? r.ToString();
                                    Bump(skip, w);
                                    if (skipNames.Count < 20) skipNames.Add(s.Name + " (" + w + ")");
                                    st = "POMINIETA - " + w;
                                    break;
                            }
                        }
                    }
                    catch (Exception e) { st = "blad " + e.GetType().Name; Bump(skip, st); overlap++; skipped++; }
                    // potwierdzenie dla metod wolanych co klatke (na jednej z nich gra zawisla 19.10 16:09)
                    if (m.DeclaringType == mbsb)
                    {
                        string n = m.Name;
                        if (n == "OnApplicationTick") sApp = st; else if (n == "AfterAsyncTickTick") sAsync = st; else if (n == "OnNetworkTick") sNet = st;
                    }
                }
            }
            catch (Exception e) { Scribe.Line("Opis stosu (UW, " + phase + "): blad skanu (" + e.GetType().Name + ")."); return; }

            bool newSkip = skipped != _lastSkipCount, newUnknown = unknown != _lastUnknown;
            if (quiet && fixedN == 0 && !newSkip && !newUnknown) return;
            _lastSkipCount = skipped; _lastUnknown = unknown;

            var sb = new System.Text.StringBuilder();
            sb.Append("Opis stosu (UW, ").Append(phase).Append("): zalatanych metod ").Append(patched)
              .Append(", malych (kod <= 12 B) ").Append(small)
              .Append(", nachodzacych na opis stosu ").Append(overlap)
              .Append(" - naprawionych teraz ").Append(fixedN).Append(", juz dobrych ").Append(already)
              .Append(", pominietych ").Append(skipped);
            if (skip.Count > 0)
            {
                sb.Append(" (");
                bool first = true; foreach (var kv in skip) { if (!first) sb.Append("; "); sb.Append(kv.Key).Append(' ').Append(kv.Value); first = false; }
                sb.Append(")");
            }
            sb.Append("; nierozpoznanych ").Append(unknown).Append(" (brak zgodnego naglowka kodu; sprawdzane tylko IL <= ").Append(UwIlMax).Append(" B)");
            sb.Append("; blokow trampolin ").Append(UnwindCore.Blocks);
            sb.Append(" | co klatke: MBSubModuleBase.OnApplicationTick ").Append(sApp)
              .Append(", AfterAsyncTickTick ").Append(sAsync)
              .Append(", OnNetworkTick ").Append(sNet).Append('.');
            Scribe.Line(sb.ToString());
            if (skipNames.Count > 0 && (newSkip || !quiet))
                Scribe.Line("Opis stosu (UW, " + phase + "): pominiete (do 20): " + string.Join("; ", skipNames.ToArray()) + ".");
            if (_unknownNames.Count > 0 && newUnknown)
                Scribe.Line("Opis stosu (UW, " + phase + "): nierozpoznane (pierwsze " + _unknownNames.Count + "): " + string.Join("; ", _unknownNames.ToArray()) + ".");
        }

        /// <summary>Lekka straz co ok. 30 s: tylko znane male metody, porownanie bajtow (bez alokacji, dopoki nic sie nie zmienilo).
        /// Jesli czyjas latka nadpisala skok i opis stosu znow jest zepsuty - naprawiamy ponownie; linia w logu tylko przy zmianie.</summary>
        private static void Light()
        {
            int changed = 0, refixed = 0, failed = 0;
            string lastFail = null;
            try
            {
                for (int i = 0; i < _small.Count; i++)
                {
                    var s = _small[i];
                    if (!UnwindCore.NeedsCheck(s)) continue;
                    changed++;
                    string why;
                    R r = UnwindCore.Check(s, out why);
                    if (r == R.Repaired) refixed++;
                    else if (r != R.AlreadyOk && r != R.NoOverlap) { failed++; lastFail = s.Name + " (" + why + ")"; }
                }
            }
            catch { }
            if (changed > 0)
                Scribe.Line("Opis stosu (UW, straz 30s): zmienionych skokow nad opisem stosu " + changed + ", naprawionych ponownie " + refixed
                            + (failed > 0 ? ", NIE naprawionych " + failed + " (np. " + lastFail + ")" : "") + ".");
        }

        private static void Bump(Dictionary<string, int> d, string k) { int v; d.TryGetValue(k, out v); d[k] = v + 1; }

        private static string NameOf(MethodBase m)
        {
            try { return (m.DeclaringType != null ? m.DeclaringType.FullName : "?") + "." + m.Name; } catch { return "?"; }
        }

        /// <summary>Raz na metode: -1 bezpieczna (duze IL albo opis stosu daleko), -2 nierozpoznana, >= 0 nowy wpis w _small.</summary>
        private static int Classify(MethodBase m)
        {
            int il;
            try { var body = m.GetMethodBody(); var arr = body != null ? body.GetILAsByteArray() : null; il = arr != null ? arr.Length : 0; }
            catch { il = 0; }
            if (il > UwIlMax) return -1;
            IntPtr entry, mdesc;
            try { entry = m.MethodHandle.GetFunctionPointer(); mdesc = m.MethodHandle.Value; }
            catch { AddUnknown(m); return -2; }
            IntPtr start; int off, n;
            if (!UnwindCore.Locate(entry, mdesc, out start, out off, out n)) { AddUnknown(m); return -2; }
            if (off < 0 || off >= UnwindCore.SafeOff) return -1;
            _small.Add(new UnwindCore.Small { Name = NameOf(m), Start = start, Off = off, NUnw = n });
            return _small.Count - 1;
        }

        private static void AddUnknown(MethodBase m) { if (_unknownNames.Count < 12) _unknownNames.Add(NameOf(m)); }
    }
}
