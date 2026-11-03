using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using CW = Armoury.CirculationWindows;

namespace Armoury
{
    /// <summary>
    /// 322b (audyt ekonomii 03.11, wyciek 5): odcinek "budowy" bisekcji f19-e1 gubi 180-540 zl KAZDEJ doby i cala reszta siedzi w kroku "Mark budowy", czyli w
    /// TownFolk.DaWorksRun (jedyny modul miedzy Probe169("BuildFunding.Daily") a Mark(MBuild)), choc jego kontrolki pokazuja 0. TYLKO POMIAR, bez zmiany logiki:
    ///  - kazdy podkrok DaWorksRun (zakup materialu z polki - DsWorksBuy, zaplata wsi - DaPayVillage, zaplata ludu miasta - DaPayTown, danina od skarbca wsi -
    ///    DaVillageDue) czyta zloto DOTKNIETYCH posiadaczy przed i po (kiesa wsi, kasa targu, kasa osady macierzystej, kiesy ludu targu i placacego, kiesa pana)
    ///    i CW.BlockNamed; reszta podkroku = zmiana dotknietych w swiecie - zmiana pozycji nazwanych (0 = przelew zamkniety);
    ///  - zloto swiata wedlug posiadaczy (ReadHolders) na wejsciu i wyjsciu DaWorksRun: swiat - zmierzone w podkrokach = zloto, ktore zmienilo sie POZA dotknietymi
    ///    posiadaczami (np. cudza latka na ChangeHeroGold / ChangeGold, kiesa ludu poza lista posiadaczy) - z nazwa posiadacza;
    ///  - probki swiata: przy kilku pierwszych wywolaniach kazdego podkroku na dobe pelny przeglad swiata przed i po (reszta swiata wywolania - reszta dotknietych
    ///    = "poza dotknietymi" tego podkroku);
    ///  - raz na sesje: latki Harmony (wszystkich modow) na Hero.ChangeHeroGold / Hero.Gold i SettlementComponent.ChangeGold / Gold.
    /// Linia doby "Budowy - pomiar (322b): dzien N" (zaraz po DaWorksRun, przed Mark budowy), grupa budowy322b w tools/sprawdz_logi.py. Klucz: ten sam co bisekcja
    /// (LedgerSegmentBisectLog przy wlaczonej ksiedze obiegu) - wylaczony = zadnego odczytu wiecej niz dotad. Wyjatek w pomiarze - licznik potkniec, pomiar tej doby
    /// dalej (bez globalnego wylacznika, CLAUDE.md 7); pomiar niczego nie zapisuje w grze.
    /// </summary>
    internal static partial class TownFolk
    {
        private const int P2Buy = 0, P2Vil = 1, P2Town = 2, P2Due = 3, P2Steps = 4;
        private static readonly string[] P2Name = { "zakup materialu (DsWorksBuy)", "zaplata wsi (DaPayVillage)", "zaplata ludu miasta (DaPayTown)", "danina od skarbca wsi (DaVillageDue)" };
        private const int P2SampleMax = 6, P2PreMax = 12;   // probki swiata na podkrok i dobe: skuteczne (zloto sie ruszylo) / wszystkie przeglady "przed"
        private const int P2TouchMax = 10;
        private const int TSet = 0, TFolkK = 1, TFolkG = 2, THero = 3;   // rodzaj dotknietego: kasa osady, kiesy ludu (W/R/P albo podzamcze), gmina (KG), kiesa bohatera

        // ---- stan doby (od P2Begin do P2End)
        private static bool _p2On;
        private static long[] _p2H0;
        private static long _p2N0, _p2Ticks;
        private static readonly long[] _p2Hl = new long[64];                // zmiana dotknietych w podkrokach wedlug posiadacza swiata
        private static long _p2Outside;                                      // zmiana dotknietych poza swiatem (bohater Dead/Disabled, kiesa ludu poza lista posiadaczy)
        private static readonly int[] _p2Calls = new int[P2Steps], _p2Moved = new int[P2Steps], _p2Smp = new int[P2Steps], _p2Pre = new int[P2Steps];
        private static readonly long[] _p2Out = new long[P2Steps], _p2InSet = new long[P2Steps], _p2InFolk = new long[P2Steps], _p2InHero = new long[P2Steps],
                                       _p2Named = new long[P2Steps], _p2Res = new long[P2Steps], _p2SmpW = new long[P2Steps], _p2SmpL = new long[P2Steps];
        private static readonly int[] _p2ResN = new int[P2Steps];             // wywolania z reszta dotknietych != 0
        private static int _p2FolkOff, _p2HeroOff, _p2HeroGone, _p2St, _p2StSess;
        private static long _p2FolkOffG, _p2HeroOffG;
        private static readonly HashSet<object> _p2Fk = new HashSet<object>();      // kiesy ludu na liscie posiadaczy (_fk) - migawka doby
        private static readonly HashSet<Hero> _p2Alive = new HashSet<Hero>();       // Hero.AllAliveHeroes - migawka doby

        // ---- jedno wywolanie (podkroki nie sa zagniezdzone; watek glowny)
        private static int _p2Step = -1, _p2Nt;
        private static readonly object[] _p2O = new object[P2TouchMax];
        private static readonly int[] _p2K = new int[P2TouchMax], _p2Hd = new int[P2TouchMax];
        private static readonly long[] _p2B = new long[P2TouchMax];
        private static long _p2CallN0, _p2CallW0;
        private static bool _p2CallScan;

        // ---- sesja
        private static int _p2Days;
        private static long _p2SessWorld, _p2SessUnexpl;
        private static readonly long[] _p2SessRes = new long[P2Steps], _p2SessSmpOut = new long[P2Steps];
        private static readonly int[] _p2SessSmpN = new int[P2Steps];
        private static string _p2Patches;                                    // latki na zloto (raz na sesje)

        /// <summary>DaReset: pomiar 322b od nowa (nowa kampania / wczytanie).</summary>
        private static void P2Reset()
        {
            _p2On = false; _p2Days = 0; _p2SessWorld = _p2SessUnexpl = 0; _p2StSess = 0; _p2Patches = null;
            Array.Clear(_p2SessRes, 0, P2Steps); Array.Clear(_p2SessSmpOut, 0, P2Steps); Array.Clear(_p2SessSmpN, 0, P2Steps);
        }

        private static void P2Stumble(string where, Exception e)
        {
            _p2St++; _p2StSess++;
            try { if (_daErr.Add("P2." + where)) Log.Error("TownFolk.Pomiar322b." + where, e); } catch { }
        }

        private static long P2World()
        {
            var h = MoneyLedger.HoldersNow();
            long t = 0;
            for (int i = 0; i < h.Length; i++) t += h[i];
            return t;
        }

        /// <summary>Wejscie DaWorksRun: posiadacze swiata i pozycje nazwane; zera doby. Tylko w bloku dobowym przy kluczu bisekcji.</summary>
        private static void P2Begin()
        {
            _p2On = false; _p2Step = -1;
            try
            {
                if (!MoneyLedger.InBlock || !MoneyLedger.BisectOn) return;
                long t0 = Stopwatch.GetTimestamp();
                Array.Clear(_p2Hl, 0, _p2Hl.Length); _p2Outside = 0;
                Array.Clear(_p2Calls, 0, P2Steps); Array.Clear(_p2Moved, 0, P2Steps); Array.Clear(_p2Smp, 0, P2Steps); Array.Clear(_p2Pre, 0, P2Steps);
                Array.Clear(_p2Out, 0, P2Steps); Array.Clear(_p2InSet, 0, P2Steps); Array.Clear(_p2InFolk, 0, P2Steps); Array.Clear(_p2InHero, 0, P2Steps);
                Array.Clear(_p2Named, 0, P2Steps); Array.Clear(_p2Res, 0, P2Steps); Array.Clear(_p2SmpW, 0, P2Steps); Array.Clear(_p2SmpL, 0, P2Steps);
                Array.Clear(_p2ResN, 0, P2Steps);
                _p2FolkOff = _p2HeroOff = _p2HeroGone = 0; _p2FolkOffG = _p2HeroOffG = 0; _p2St = 0;
                _p2Fk.Clear(); for (int i = 0; i < _fk.Count; i++) if (_fk[i] != null) _p2Fk.Add(_fk[i]);
                _p2Alive.Clear(); foreach (var h in Hero.AllAliveHeroes) if (h != null) _p2Alive.Add(h);
                _p2H0 = MoneyLedger.HoldersNow();
                _p2N0 = CW.BlockNamed;
                _p2On = true;
                _p2Ticks = Stopwatch.GetTimestamp() - t0;
            }
            catch (Exception e) { _p2On = false; P2Stumble("Begin", e); }
        }

        /// <summary>Poczatek podkroku: dotkniete dopisuje wolajacy (P2Touch*), opcjonalnie przeglad swiata "przed" (probka).</summary>
        private static void P2Open(int step)
        {
            if (!_p2On) return;
            try
            {
                long t0 = Stopwatch.GetTimestamp();
                _p2Step = step; _p2Nt = 0; _p2CallScan = false;
                if (_p2Smp[step] < P2SampleMax && _p2Pre[step] < P2PreMax) { _p2CallW0 = P2World(); _p2CallScan = true; _p2Pre[step]++; }
                _p2CallN0 = CW.BlockNamed;   // po przegladzie (przeglad nic nie ksieguje, ale kolejnosc jak w SubMark169)
                _p2Ticks += Stopwatch.GetTimestamp() - t0;
            }
            catch (Exception e) { _p2Step = -1; P2Stumble("Open", e); }
        }

        private static void P2Add(object o, int kind, int holder, long before)
        {
            for (int i = 0; i < _p2Nt; i++) if (ReferenceEquals(_p2O[i], o) && _p2K[i] == kind) return;
            if (_p2Nt >= P2TouchMax) return;
            _p2O[_p2Nt] = o; _p2K[_p2Nt] = kind; _p2Hd[_p2Nt] = holder; _p2B[_p2Nt] = before; _p2Nt++;
        }

        private static long P2Read(object o, int kind)
        {
            switch (kind)
            {
                case TSet: { var st = (Settlement)o; return st.SettlementComponent != null ? st.SettlementComponent.Gold : 0; }
                case TFolkK: { var f = (Folk)o; return f.Kind == 0 ? f.KW + f.KR + f.KP : f.KW; }
                case TFolkG: { var f = (Folk)o; return f.KG; }
                default: return ((Hero)o).Gold;
            }
        }

        private static void P2TouchSt(Settlement st)
        {
            if (!_p2On || _p2Step < 0 || st == null || st.SettlementComponent == null) return;
            try { P2Add(st, TSet, MoneyLedger.HolderOfSettlement322(st), P2Read(st, TSet)); }
            catch (Exception e) { P2Stumble("TouchSt", e); }
        }

        /// <summary>Kiesy ludu: W/R/P (miasto) albo W (podzamcze) i gmina (KG, tylko miasto) - te same pola co TotalTown / TotalCastle / TotalGmina;
        /// kiesa spoza listy posiadaczy (_fk) - poza swiatem (licznik).</summary>
        private static void P2TouchFolk(Folk f)
        {
            if (!_p2On || _p2Step < 0 || f == null) return;
            try
            {
                bool listed = f.Kind <= 1 && _p2Fk.Contains(f);
                int hk = !listed ? -1 : (f.Kind == 0 ? MoneyLedger.HolderFolkTown322 : MoneyLedger.HolderFolkCastle322);
                P2Add(f, TFolkK, hk, P2Read(f, TFolkK));
                if (f.Kind == 0) P2Add(f, TFolkG, listed ? MoneyLedger.HolderGmina322 : -1, f.KG);
            }
            catch (Exception e) { P2Stumble("TouchFolk", e); }
        }

        private static void P2TouchHero(Hero h)
        {
            if (!_p2On || _p2Step < 0 || h == null) return;
            try
            {
                int hk = MoneyLedger.HolderOfHero322(h);
                if (hk >= 0 && !_p2Alive.Contains(h)) hk = -1;          // zywy wedlug stanu, a poza Hero.AllAliveHeroes - kiesa poza posiadaczami (licznik w P2Close)
                P2Add(h, THero, hk, h.Gold);
            }
            catch (Exception e) { P2Stumble("TouchHero", e); }
        }

        /// <summary>Danina od skarbca wsi: dotkniete jak w DaVillageDue (kiesa wsi, kasa osady macierzystej, kiesa glowy rodu wlasciciela) - sam odczyt.</summary>
        private static void P2TouchDue(Village vil)
        {
            if (!_p2On || _p2Step < 0 || vil == null) return;
            try
            {
                P2TouchSt(vil.Settlement);
                if (vil.Bound != null) P2TouchSt(vil.Bound);
                var clan = vil.Settlement != null ? vil.Settlement.OwnerClan : null;
                if (clan != null && clan.Leader != null) P2TouchHero(clan.Leader);
            }
            catch (Exception e) { P2Stumble("TouchDue", e); }
        }

        /// <summary>Koniec podkroku: zmiany dotknietych, reszta podkroku, probka swiata. Nie rzuca.</summary>
        private static void P2Close()
        {
            if (!_p2On || _p2Step < 0) return;
            int step = _p2Step; _p2Step = -1;
            try
            {
                long t0 = Stopwatch.GetTimestamp();
                long dn = CW.BlockNamed - _p2CallN0;
                long inWorld = 0; bool moved = dn != 0;
                for (int i = 0; i < _p2Nt; i++)
                {
                    long d;
                    try { d = P2Read(_p2O[i], _p2K[i]) - _p2B[i]; }
                    catch (Exception e) { P2Stumble("Close(odczyt)", e); continue; }
                    if (d == 0) continue;
                    moved = true;
                    if (d < 0) _p2Out[step] -= d;
                    else if (_p2K[i] == TSet) _p2InSet[step] += d;
                    else if (_p2K[i] == THero) _p2InHero[step] += d;
                    else _p2InFolk[step] += d;
                    int hk = _p2Hd[i];
                    if (hk >= 0 && hk < _p2Hl.Length) { _p2Hl[hk] += d; inWorld += d; }
                    else
                    {
                        _p2Outside += d;
                        if (_p2K[i] == THero)
                        {
                            var h = (Hero)_p2O[i];
                            if (CW.OutOfWorld(h)) { _p2HeroGone++; } else { _p2HeroOff++; _p2HeroOffG += d; }
                        }
                        else if (_p2K[i] != TSet) { _p2FolkOff++; _p2FolkOffG += d; }
                    }
                }
                for (int i = 0; i < _p2Nt; i++) _p2O[i] = null;
                _p2Calls[step]++;
                if (moved) _p2Moved[step]++;
                _p2Named[step] += dn;
                long res = inWorld - dn;
                _p2Res[step] += res;
                if (res != 0) _p2ResN[step]++;
                if (_p2CallScan)
                {
                    long w = P2World() - _p2CallW0;
                    long wr = w - (CW.BlockNamed - _p2CallN0);
                    if (moved || wr != 0) { _p2Smp[step]++; _p2SmpW[step] += wr; _p2SmpL[step] += res; }
                }
                _p2Ticks += Stopwatch.GetTimestamp() - t0;
            }
            catch (Exception e) { P2Stumble("Close", e); }
        }

        /// <summary>Latki Harmony (wszystkich modow) na metodach zlota bohatera i kasy osady - raz na sesje.</summary>
        private static string P2PatchList()
        {
            var sb = new StringBuilder(256);
            P2PatchOne(sb, "Hero.ChangeHeroGold", AccessTools.DeclaredMethod(typeof(Hero), "ChangeHeroGold"));
            P2PatchOne(sb, "Hero.Gold (set)", AccessTools.DeclaredPropertySetter(typeof(Hero), "Gold"));
            P2PatchOne(sb, "SettlementComponent.ChangeGold", AccessTools.DeclaredMethod(typeof(SettlementComponent), "ChangeGold"));
            P2PatchOne(sb, "SettlementComponent.Gold (set)", AccessTools.DeclaredPropertySetter(typeof(SettlementComponent), "Gold"));
            var tg = AccessTools.DeclaredMethod(typeof(Town), "ChangeGold"); if (tg != null) P2PatchOne(sb, "Town.ChangeGold", tg);
            var vg = AccessTools.DeclaredMethod(typeof(Village), "ChangeGold"); if (vg != null) P2PatchOne(sb, "Village.ChangeGold", vg);
            return sb.ToString();
        }

        private static void P2PatchOne(StringBuilder sb, string name, MethodBase m)
        {
            if (sb.Length > 0) sb.Append("; ");
            sb.Append(name).Append(": ");
            if (m == null) { sb.Append("brak metody"); return; }
            var pi = Harmony.GetPatchInfo(m);
            if (pi == null) { sb.Append("bez latek"); return; }
            int n = 0;
            n += P2PatchKind(sb, "prefiksy", pi.Prefixes, n);
            n += P2PatchKind(sb, "postfiksy", pi.Postfixes, n);
            n += P2PatchKind(sb, "transpilery", pi.Transpilers, n);
            n += P2PatchKind(sb, "finalizery", pi.Finalizers, n);
            if (n == 0) sb.Append("bez latek");
        }

        private static int P2PatchKind(StringBuilder sb, string kind, IEnumerable<Patch> ps, int before)
        {
            if (ps == null) return 0;
            int n = 0;
            foreach (var p in ps)
            {
                if (p == null) continue;
                sb.Append(before + n > 0 ? ", " : "").Append(kind).Append(' ').Append(p.owner ?? "?");
                if (p.PatchMethod != null && p.PatchMethod.DeclaringType != null) sb.Append(" (").Append(p.PatchMethod.DeclaringType.FullName).Append('.').Append(p.PatchMethod.Name).Append(')');
                n++;
            }
            return n;
        }

        private static string P2S(long n) { return (n >= 0 ? "+" : "") + n; }

        /// <summary>Wyjscie DaWorksRun: swiat wedlug posiadaczy wobec zmierzonego w podkrokach; linia "Budowy - pomiar (322b): dzien N". Nie rzuca.</summary>
        private static void P2End()
        {
            if (!_p2On) return;
            _p2On = false; _p2Step = -1;
            try
            {
                long t0 = Stopwatch.GetTimestamp();
                var h1 = MoneyLedger.HoldersNow();
                long dn = CW.BlockNamed - _p2N0;
                int nh = Math.Min(Math.Min(h1.Length, _p2H0 != null ? _p2H0.Length : 0), _p2Hl.Length);
                long dw = 0, local = 0;
                var gaps = new StringBuilder(256);
                for (int k = 0; k < nh; k++)
                {
                    long wd = h1[k] - _p2H0[k];
                    dw += wd; local += _p2Hl[k];
                    long g = wd - _p2Hl[k];
                    if (g != 0) { if (gaps.Length > 0) gaps.Append(", "); gaps.Append(MoneyLedger.HolderName(k)).Append(' ').Append(P2S(wd)).Append(" (zmierzone ").Append(P2S(_p2Hl[k])).Append(", poza ").Append(P2S(g)).Append(')'); }
                }
                long world = dw - dn;                       // reszta DaWorksRun = krok "Mark budowy" bisekcji f19-e1 tej doby
                long stepSum = 0; for (int i = 0; i < P2Steps; i++) stepSum += _p2Res[i];
                long unexpl = world - stepSum;             // reszta poza dotknietymi posiadaczami (swiat - suma reszt podkrokow)
                if (_p2Patches == null) { try { _p2Patches = P2PatchList(); } catch (Exception e) { _p2Patches = "blad odczytu"; P2Stumble("PatchList", e); } }
                _p2Ticks += Stopwatch.GetTimestamp() - t0;

                _p2Days++; _p2SessWorld += world; _p2SessUnexpl += unexpl;
                for (int i = 0; i < P2Steps; i++) { _p2SessRes[i] += _p2Res[i]; _p2SessSmpOut[i] += _p2SmpW[i] - _p2SmpL[i]; _p2SessSmpN[i] += _p2Smp[i]; }

                // gdzie reszta: podkrok z najwieksza |reszta dotknietych|; inaczej - poza dotknietymi: podkrok, ktorego probki swiata maja reszte poza dotknietymi
                string where;
                int best = -1; for (int i = 0; i < P2Steps; i++) if (_p2Res[i] != 0 && (best < 0 || Math.Abs(_p2Res[i]) > Math.Abs(_p2Res[best]))) best = i;
                int bestS = -1; for (int i = 0; i < P2Steps; i++) { long o = _p2SmpW[i] - _p2SmpL[i]; if (o != 0 && (bestS < 0 || Math.Abs(o) > Math.Abs(_p2SmpW[bestS] - _p2SmpL[bestS]))) bestS = i; }
                if (world == 0 && stepSum == 0) where = "brak reszty";
                else if (best >= 0 && Math.Abs(_p2Res[best]) >= Math.Abs(unexpl)) where = "podkrok " + P2Name[best] + " (w dotknietych posiadaczach " + P2S(_p2Res[best]) + ")";
                else if (bestS >= 0) where = "poza dotknietymi posiadaczami " + P2S(unexpl) + ", probki swiata wskazuja podkrok " + P2Name[bestS] + " (" + P2S(_p2SmpW[bestS] - _p2SmpL[bestS]) + " w " + _p2Smp[bestS] + " probkach)";
                else where = "poza dotknietymi posiadaczami " + P2S(unexpl) + ", probki swiata bez reszty (poza podkrokami albo w wywolaniach bez probki)";

                var sb = new StringBuilder(2048);
                sb.Append("Budowy - pomiar (322b): dzien ").Append((int)CampaignTime.Now.ToDays - 1)   // numer doby jak linie ksiegi (MoneyLedger.Daily: Now - 1) - para z bisekcja f19-e1
                  .Append(" | DaWorksRun swiat ").Append(P2S(world)).Append(" (zloto swiata ").Append(P2S(dw)).Append(" - pozycje nazwane ").Append(P2S(dn))
                  .Append("; = krok 'Mark budowy' bisekcji f19-e1) | reszta pojawia sie: ").Append(where)
                  .Append(" | podkroki (reszta = zmiana dotknietych w swiecie - pozycje nazwane): ");
                for (int i = 0; i < P2Steps; i++)
                {
                    if (i > 0) sb.Append("; ");
                    sb.Append(P2Name[i]).Append(": wywolan ").Append(_p2Calls[i]).Append(" (zloto ruszone ").Append(_p2Moved[i]).Append("), placacy -").Append(_p2Out[i])
                      .Append(", kasy osad +").Append(_p2InSet[i]).Append(", kiesy ludu +").Append(_p2InFolk[i]).Append(", bohaterowie +").Append(_p2InHero[i])
                      .Append(", nazwane ").Append(P2S(_p2Named[i])).Append(", reszta ").Append(P2S(_p2Res[i])).Append(" (wywolan z reszta ").Append(_p2ResN[i]).Append(')')
                      .Append(", probki swiata ").Append(_p2Smp[i]).Append(" (przegladow ").Append(_p2Pre[i]).Append("): swiat ").Append(P2S(_p2SmpW[i])).Append(" / dotkniete ").Append(P2S(_p2SmpL[i]))
                      .Append(" / poza ").Append(P2S(_p2SmpW[i] - _p2SmpL[i]));
                }
                sb.Append(" | suma reszt podkrokow ").Append(P2S(stepSum)).Append(", poza dotknietymi ").Append(P2S(unexpl))
                  .Append(" | posiadacze (swiat wobec zmierzonego w podkrokach): ").Append(gaps.Length > 0 ? gaps.ToString() : "zgodne")
                  .Append(" | dotkniete poza swiatem ").Append(P2S(_p2Outside)).Append(": pan Dead/Disabled ").Append(_p2HeroGone)
                  .Append(", pan zywy poza Hero.AllAliveHeroes ").Append(_p2HeroOff).Append(" (").Append(P2S(_p2HeroOffG)).Append(')')
                  .Append(", kiesa ludu poza lista posiadaczy ").Append(_p2FolkOff).Append(" (").Append(P2S(_p2FolkOffG)).Append(')')
                  .Append(" | sesja (").Append(_p2Days).Append(" dob): swiat ").Append(P2S(_p2SessWorld)).Append(", poza dotknietymi ").Append(P2S(_p2SessUnexpl)).Append(", reszty: ");
                for (int i = 0; i < P2Steps; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append(P2Name[i]).Append(' ').Append(P2S(_p2SessRes[i])).Append(" (probki ").Append(_p2SessSmpN[i]).Append(", poza ").Append(P2S(_p2SessSmpOut[i])).Append(')');
                }
                sb.Append(" | latki na zloto: ").Append(_p2Patches ?? "-")
                  .Append(" | czas ").Append(Ms(_p2Ticks)).Append(" ms | potkniecia ").Append(_p2St).Append(" (sesja ").Append(_p2StSess).Append(").");
                Log.Info(sb.ToString());
            }
            catch (Exception e) { P2Stumble("End", e); }
        }
    }
}
