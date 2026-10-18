using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// A17 - LINIE DIAGNOZY (TYLKO LOG, niczego nie zmienia w grze) dla pozycji "NIE WIEM" rozpoznania A17 (docs/reszta-2026-10-14/ROZPOZNANIE-A17.md rozdz. 3).
    /// Klucz A17DiagnosticLines (wylaczony = bez linii i bez kosztu). Zrodlo: podsluch ksiegi towarow na ItemRoster.AddToCounts (GoodsLedger.Tap - wola tu
    /// dla kazdej zmiany rosteru, w ramce i poza nia; tylko watek glowny).
    ///  D1 konie (poz. 3, K1-3: -52 konia/d w ksiedze koni, 3/4 to juczne - sprawca nieznany): netto koni wedlug kategorii ksiegi koni (HorseBook.Cat) w kazdej
    ///     ramce ksiegi towarow i poza ramkami wedlug posiadacza rosteru; pierwsze 5 stosow na sesje dla zmian poza ramkami >= 3 sztuk.
    ///     Linia "Konie wedlug ramek (A17 diagnoza)". Uwaga: zakup konia do zbrojowni DTE / na bohatera to -n na rosterze bez +n (stan poza rosterami).
    ///  D2 ryby (poz. 7, M6b-5a: +34..100 ryb/d bez zrodla): zmiany "fish" poza ramkami wedlug posiadacza (+ i - osobno) i pierwsze 5 stosow na sesje.
    ///     Linia "Ryby poza ramkami (A17 diagnoza)".
    ///  D3 unikat z polki (poz. 4, renly_armor w biegu dwuletnim): ubytek unikatu (UniqueSpoils.Is) z rosteru osady - ramka albo "poza ramkami" i stos,
    ///     najwyzej 20 linii na sesje; zdjecie na czas konsumpcji osady (UniqueShelf.Busy) pomijane. Linia "Unikat z polki (A17 diagnoza D3)".
    /// </summary>
    internal static class A17Diag
    {
        internal static bool On { get { var s = Settings.Current; return s != null && s.A17DiagnosticLines; } }

        private const int Cats = 5, Frames = 40;
        private static readonly string[] CatName = { "zwykly", "bojowy", "szlachetny", "juczny", "egzotyczny" };
        private const int HTown = 0, HCastle = 1, HVillage = 2, HStash = 3, HOtherSettl = 4, HCaravan = 5, HLord = 6, HVillager = 7, HBk = 8, HBand = 9, HPlayer = 10, HOther = 11, HUnknown = 12, Holders = 13;
        private static readonly string[] HolderName = { "polki miast", "polki zamkow", "magazyny wsi", "schowki osad", "inne osady", "karawany", "lordowie", "tabory wsi",
                                                        "partie BK (konwoje, patrole)", "bandy", "gracz", "inne partie", "roster nieznany (nowy)" };
        private const int StackMax = 5, D3Max = 20;

        private static readonly long[,] _hFrame = new long[Frames, Cats];
        private static readonly long[,] _hOut = new long[Holders, Cats];
        private static readonly long[] _fishPlus = new long[Holders], _fishMinus = new long[Holders];
        private static long _fishInFrames;
        private static int _hStacks, _fStacks, _d3, _stumbles;
        private static bool _err;

        // A17 D1b (noc 17/18.10, klucz A17AuthorLines): autorzy zmian koni w tickach osad i miast oraz poza ramkami, ryb poza ramkami - netto doby wedlug autora
        // (metoda wolajaca + zachowanie kampanii ze stosu); szukamy reszty ksiegi koni (fin80 -18 jucznych/d) i ryb "bez wyjasnienia" (+69/d). Tylko log.
        internal static bool AuthOn { get { var s = Settings.Current; return s != null && s.A17AuthorLines; } }
        private const int CapH = 1200, CapF = 800, AuthTop = 12;
        private static readonly Dictionary<string, long[]> _authH = new Dictionary<string, long[]>();
        private static readonly Dictionary<string, long> _authF = new Dictionary<string, long>();
        private static readonly Dictionary<MethodBase, string> _authCache = new Dictionary<MethodBase, string>();
        private static int _traceH, _traceF;
        private static long _authTicks, _authTmpH, _authTmpF;   // rostery spoza mapy (tymczasowe - np. lup bitwy przed oddaniem zwyciezcy - albo partie z ostatniej godziny): poza autorami

        private static readonly Dictionary<ItemRoster, int> _holder = new Dictionary<ItemRoster, int>();
        private static readonly Dictionary<ItemRoster, string> _shelfName = new Dictionary<ItemRoster, string>();
        private static double _builtHour = -1.0;

        internal static void Reset()
        {
            ClearDay();
            _hStacks = _fStacks = _d3 = 0; _stumbles = 0; _err = false;
            _holder.Clear(); _shelfName.Clear(); _builtHour = -1.0;
            A17Fix.Reset();   // A17 F1-F5 (noc 17/18.10): liczniki doby i znaczniki bitew - wolane tu, zeby nie dopisywac do listy Reset w ArmouryBehavior
        }

        private static void ClearDay()
        {
            Array.Clear(_hFrame, 0, _hFrame.Length); Array.Clear(_hOut, 0, _hOut.Length);
            Array.Clear(_fishPlus, 0, Holders); Array.Clear(_fishMinus, 0, Holders); _fishInFrames = 0;
            _authH.Clear(); _authF.Clear(); _traceH = _traceF = 0; _authTicks = 0; _authTmpH = _authTmpF = 0;
        }

        /// <summary>GoodsLedger.Tap (postfiks ItemRoster.AddToCounts, zmiana faktycznie zaszla): frame = rodzaj najbardziej wewnetrznej otwartej ramki ksiegi towarow
        /// na tym watku albo -1 (poza ramkami). Tylko watek glowny. Nie rzuca.</summary>
        internal static void Tap(ItemRoster r, ItemObject it, int n, int frame)
        {
            if (it == null || r == null || n == 0) return;
            try
            {
                if (it.ItemType == ItemObject.ItemTypeEnum.Horse)
                {
                    int c = HorseBook.Cat(it);
                    if (c >= 0 && c < Cats)
                    {
                        if (AuthOn && (frame < 0 || frame == GoodsLedger.FTickSettle || frame == GoodsLedger.FTickTown))   // A17 D1b - roster tymczasowy liczony osobno (inaczej lup bitwy liczylby sie dwa razy)
                        { if (HolderOf(r) == HUnknown) _authTmpH += n; else NoteHorseAuthor(c, n); }
                        if (frame >= 0 && frame < Frames) _hFrame[frame, c] += n;
                        else
                        {
                            int h = HolderOf(r);
                            _hOut[h, c] += n;
                            if (Math.Abs(n) >= 3 && _hStacks < StackMax)
                            {
                                _hStacks++;
                                Log.Info("Konie poza ramkami (A17 diagnoza D1, stos " + _hStacks + "/" + StackMax + "): " + it.StringId + " (" + CatName[c] + ") " + (n > 0 ? "+" : "") + n
                                         + " | " + HolderName[h] + Where(r) + " | stos: " + Stack());
                            }
                        }
                    }
                }
                else if (it.StringId == "fish")
                {
                    if (frame >= 0) _fishInFrames += n;
                    else
                    {
                        int h = HolderOf(r);
                        if (AuthOn) { if (h == HUnknown) _authTmpF += n; else NoteFishAuthor(n); }   // A17 D1b
                        if (n > 0) _fishPlus[h] += n; else _fishMinus[h] -= n;
                        if (Math.Abs(n) >= 2 && _fStacks < StackMax)
                        {
                            _fStacks++;
                            Log.Info("Ryby poza ramkami (A17 diagnoza D2, stos " + _fStacks + "/" + StackMax + "): " + (n > 0 ? "+" : "") + n + " | " + HolderName[h] + Where(r) + " | stos: " + Stack());
                        }
                    }
                }
                if (n < 0 && _d3 < D3Max && !UniqueShelf.Busy && UniqueSpoils.Is(it))
                {
                    int h = HolderOf(r);
                    if (h <= HOtherSettl)
                    {
                        _d3++;
                        Log.Info("Unikat z polki (A17 diagnoza D3): " + it.StringId + " " + n + " z " + HolderName[h] + Where(r) + " | ramka " + (frame >= 0 ? GoodsLedger.FrameName(frame) : "poza ramkami")
                                 + " | dzien " + (int)CampaignTime.Now.ToDays + " | linia " + _d3 + "/" + D3Max + " | stos: " + Stack());
                    }
                }
            }
            catch (Exception e) { _stumbles++; if (!_err) { _err = true; Log.Error("A17Diag.Tap", e); } }
        }

        private static void NoteHorseAuthor(int c, int n)
        {
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                string who = _traceH < CapH ? Author() : "(limit sladow doby)";
                if (_traceH < CapH) _traceH++;
                long[] a;
                if (!_authH.TryGetValue(who, out a)) { a = new long[Cats]; _authH[who] = a; }
                a[c] += n;
            }
            catch { }
            finally { _authTicks += Stopwatch.GetTimestamp() - t0; }
        }

        private static void NoteFishAuthor(int n)
        {
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                string who = _traceF < CapF ? Author() : "(limit sladow doby)";
                if (_traceF < CapF) _traceF++;
                long v; _authF.TryGetValue(who, out v); _authF[who] = v + n;
            }
            catch { }
            finally { _authTicks += Stopwatch.GetTimestamp() - t0; }
        }

        /// <summary>A17 D1b: autor zmiany rosteru - pierwsza znaczaca metoda wolajaca (bez A17Diag, ksiegi towarow, ItemRoster, System, Harmony) i pierwsze zachowanie
        /// kampanii (CampaignBehaviorBase) wyzej na stosie; ramki metod latanych przez Harmony - oryginal (GetOriginalMethodFromStackframe).</summary>
        private static string Author()
        {
            var st = new StackTrace(2, false);
            string first = null, beh = null;
            int n = st.FrameCount;
            for (int k = 0; k < n && k < 40; k++)
            {
                var fr = st.GetFrame(k);
                if (fr == null) continue;
                MethodBase m = fr.GetMethod();
                if (m == null || m.DeclaringType == null || m.Name.StartsWith("DMD<", StringComparison.Ordinal))
                {
                    MethodBase o = null;
                    try { o = Harmony.GetOriginalMethodFromStackframe(fr); } catch { o = null; }
                    if (o == null) continue;
                    m = o;
                }
                string c;
                if (!_authCache.TryGetValue(m, out c)) { c = Classify(m); _authCache[m] = c; }
                if (c == null) continue;
                if (c == "#") break;
                if (c[0] == '!') { beh = c.Substring(1); break; }
                if (first == null) first = c;
            }
            if (first == null) return beh ?? "(nieznany)";
            return beh != null ? first + " < " + beh : first;
        }

        private static string Classify(MethodBase m)
        {
            var t = m.DeclaringType;
            if (t == null) return null;
            try { if (t.Assembly.IsDynamic) return null; } catch { return null; }
            if (t == typeof(A17Diag) || t == typeof(GoodsLedger) || t == typeof(ItemRoster)) return null;
            string ns = t.Namespace ?? "";
            if (ns.StartsWith("System", StringComparison.Ordinal) || ns.StartsWith("HarmonyLib", StringComparison.Ordinal) || ns.StartsWith("MonoMod", StringComparison.Ordinal)) return null;
            if (t == typeof(CampaignEventDispatcher) || t.Name.StartsWith("MbEvent", StringComparison.Ordinal) || t == typeof(CampaignEvents)) return "#";
            var top = t;
            while (top.DeclaringType != null) top = top.DeclaringType;
            string asm = top.Assembly != null ? top.Assembly.GetName().Name : "?";
            if (typeof(CampaignBehaviorBase).IsAssignableFrom(top)) return "!" + asm + ":" + top.Name;
            return asm + ":" + top.Name + "." + m.Name;
        }

        private static string AuthorsLine(int day)
        {
            var hs = new List<KeyValuePair<string, long[]>>(_authH);
            hs.Sort((a, b) => { long x = 0, y = 0; for (int c = 0; c < Cats; c++) { x += Math.Abs(a.Value[c]); y += Math.Abs(b.Value[c]); } return y.CompareTo(x); });
            var fs = new List<KeyValuePair<string, long>>(_authF);
            fs.Sort((a, b) => Math.Abs(b.Value).CompareTo(Math.Abs(a.Value)));
            var sb = new StringBuilder(1500);
            sb.Append("Konie i ryby - autorzy zmian (A17 D1b): dzien ").Append(day).Append(" | konie w tickach osad i miast oraz poza ramkami (netto doby, ")
              .Append(string.Join("/", CatName)).Append("; najwyzej ").Append(AuthTop).Append(" autorow): ");
            int k = 0;
            foreach (var kv in hs)
            {
                bool any = false;
                for (int c = 0; c < Cats; c++) if (kv.Value[c] != 0) any = true;
                if (!any) continue;
                if (k++ >= AuthTop) break;
                sb.Append(k > 1 ? "; " : "").Append(kv.Key).Append(' ').Append(string.Join("/", Array.ConvertAll(kv.Value, x => x.ToString())));
            }
            if (k == 0) sb.Append('-');
            sb.Append(" | ryby poza ramkami (netto doby): ");
            k = 0;
            foreach (var kv in fs)
            {
                if (kv.Value == 0) continue;
                if (k++ >= AuthTop) break;
                sb.Append(k > 1 ? "; " : "").Append(kv.Key).Append(' ').Append(kv.Value > 0 ? "+" : "").Append(kv.Value);
            }
            if (k == 0) sb.Append('-');
            sb.Append(" | rostery spoza mapy (lup przed oddaniem, partie z ostatniej godziny - poza autorami): konie ").Append(_authTmpH).Append(", ryby ").Append(_authTmpF);
            sb.Append(" | sladow koni ").Append(_traceH).Append('/').Append(CapH).Append(", ryb ").Append(_traceF).Append('/').Append(CapF)
              .Append(", czas ").Append((_authTicks * 1000.0 / Stopwatch.Frequency).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)).Append(" ms.");
            return sb.ToString();
        }

        private static string Where(ItemRoster r)
        {
            string s;
            return _shelfName.TryGetValue(r, out s) ? " (" + s + ")" : "";
        }

        private static string Stack()
        {
            try
            {
                var frames = new StackTrace(3, false).GetFrames();
                if (frames == null) return "-";
                var sb = new StringBuilder(500);
                int k = 0;
                foreach (var f in frames)
                {
                    var m = f != null ? f.GetMethod() : null;
                    if (m == null) continue;
                    if (k > 0) sb.Append(" <- ");
                    sb.Append(m.DeclaringType != null ? m.DeclaringType.Name : "?").Append('.').Append(m.Name);
                    if (++k >= 16) break;
                }
                return k > 0 ? sb.ToString() : "-";
            }
            catch { return "?"; }
        }

        /// <summary>Posiadacz rosteru (mapa budowana z osad i partii; przy nieznanym rosterze przebudowa najwyzej raz na godzine gry).</summary>
        private static int HolderOf(ItemRoster r)
        {
            int h;
            if (_holder.TryGetValue(r, out h)) return h;
            double now = CampaignTime.Now.ToHours;
            if (now - _builtHour >= 1.0 || _builtHour < 0.0)
            {
                _builtHour = now;
                Build();
                if (_holder.TryGetValue(r, out h)) return h;
            }
            return HUnknown;
        }

        private static void Build()
        {
            _holder.Clear(); _shelfName.Clear();
            foreach (var st in Settlement.All)
            {
                if (st == null) continue;
                try
                {
                    int k = st.IsTown ? HTown : st.IsCastle ? HCastle : st.IsVillage ? HVillage : HOtherSettl;
                    if (st.ItemRoster != null) { _holder[st.ItemRoster] = k; _shelfName[st.ItemRoster] = st.Name != null ? st.Name.ToString() : st.StringId; }
                    if (st.Stash != null) _holder[st.Stash] = HStash;
                }
                catch { }
            }
            foreach (var mp in MobileParty.All)
            {
                if (mp == null || mp.ItemRoster == null) continue;
                try { _holder[mp.ItemRoster] = KindOf(mp); } catch { }
            }
        }

        private static int KindOf(MobileParty mp)
        {
            if (mp.IsMainParty) return HPlayer;
            if (mp.IsCaravan) return HCaravan;
            if (mp.IsVillager) return HVillager;
            if (OutlawLaw.IsOutlawParty(mp)) return HBand;
            var c = mp.PartyComponent;
            string tn = c != null ? c.GetType().Name : "";
            if (tn == "PopulationPartyComponent" || tn == "GarrisonPartyComponent") return HBk;
            if (mp.IsLordParty) return HLord;
            return HOther;
        }

        private static string Row(long[,] a, int i)
        {
            var sb = new StringBuilder();
            for (int c = 0; c < Cats; c++) sb.Append(c > 0 ? "/" : "").Append(a[i, c]);
            return sb.ToString();
        }

        private static bool Any(long[,] a, int i) { for (int c = 0; c < Cats; c++) if (a[i, c] != 0) return true; return false; }

        /// <summary>Linie doby D1 i D2 (ArmouryBehavior, po ksiedze towarow). Dzien zakonczony - jak ksiega towarow (dzis - 1).</summary>
        internal static void Daily()
        {
            if (!On) { ClearDay(); return; }
            try
            {
                int day = (int)CampaignTime.Now.ToDays - 1;
                var sb = new StringBuilder(1500);
                sb.Append("Konie wedlug ramek (A17 diagnoza): dzien ").Append(day).Append(" | kategorie ").Append(string.Join("/", CatName)).Append(" | w ramkach ksiegi towarow: ");
                var inSum = new long[Cats]; var outSum = new long[Cats];
                bool any = false;
                for (int f = 0; f < Frames; f++)
                {
                    if (!Any(_hFrame, f)) continue;
                    sb.Append(any ? "; " : "").Append(GoodsLedger.FrameName(f)).Append(' ').Append(Row(_hFrame, f));
                    for (int c = 0; c < Cats; c++) inSum[c] += _hFrame[f, c];
                    any = true;
                }
                if (!any) sb.Append('-');
                sb.Append(" | poza ramkami wedlug posiadacza: ");
                any = false;
                for (int h = 0; h < Holders; h++)
                {
                    if (!Any(_hOut, h)) continue;
                    sb.Append(any ? "; " : "").Append(HolderName[h]).Append(' ').Append(Row(_hOut, h));
                    for (int c = 0; c < Cats; c++) outSum[c] += _hOut[h, c];
                    any = true;
                }
                if (!any) sb.Append('-');
                sb.Append(" | razem w ramkach ").Append(string.Join("/", Array.ConvertAll(inSum, x => x.ToString()))).Append(", poza ramkami ").Append(string.Join("/", Array.ConvertAll(outSum, x => x.ToString())))
                  .Append(" (zakup konia do zbrojowni DTE / na bohatera = ubytek z rosteru bez przybytku - stan poza rosterami w linii Konie E6/0)")
                  .Append(" | stosy ").Append(_hStacks).Append('/').Append(StackMax);
                if (_stumbles > 0) sb.Append(" | potkniecia ").Append(_stumbles);
                Log.Info(sb.Append('.').ToString());
                var fb = new StringBuilder(600);
                long plus = 0, minus = 0;
                fb.Append("Ryby poza ramkami (A17 diagnoza): dzien ").Append(day).Append(" | wedlug posiadacza (+ / -): ");
                any = false;
                for (int h = 0; h < Holders; h++)
                {
                    if (_fishPlus[h] == 0 && _fishMinus[h] == 0) continue;
                    fb.Append(any ? "; " : "").Append(HolderName[h]).Append(" +").Append(_fishPlus[h]).Append(" / -").Append(_fishMinus[h]);
                    plus += _fishPlus[h]; minus += _fishMinus[h];
                    any = true;
                }
                if (!any) fb.Append('-');
                fb.Append(" | razem +").Append(plus).Append(" / -").Append(minus).Append(" (netto ").Append(plus - minus >= 0 ? "+" : "").Append(plus - minus).Append(")")
                  .Append(" | w ramkach netto ").Append(_fishInFrames).Append(" | stosy ").Append(_fStacks).Append('/').Append(StackMax).Append(" | unikat z polki D3 ").Append(_d3).Append('/').Append(D3Max).Append('.');
                Log.Info(fb.ToString());
                if (AuthOn) Log.Info(AuthorsLine(day));   // A17 D1b (noc 17/18.10)
            }
            catch (Exception e) { Log.Error("A17Diag.Daily", e); }
            ClearDay();
        }
    }
}
