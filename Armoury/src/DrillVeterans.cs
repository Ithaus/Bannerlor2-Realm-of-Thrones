using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// 319b (Jeff 02.11: "jak sa jednostki, ktore maja juz max tier 6, to ich doswiadczenie nie przepada, tylko oni szkola swoich - lucznicy lucznikow,
    /// piechota piechote, kawaleria kawalerie; im wiecej doswiadczonych, tym wiecej nauczycieli ... ich XP, ktore mialo przepadac, rozdziela sie na
    /// wojsko, ktore szkola"). Weterani ucza swoja bron.
    ///
    /// Gra: MobilePartyTrainingBehavior.OnDailyTickParty dodaje kazdemu oddzialowi Round(wynik modelu x Number); PartyBase.OnXpChanged przycina Xp do
    /// Number x najwyzszy koszt awansu - oddzial bez UpgradeTargets ma limit 0, wiec CALE jego dobowe XP z musztry przepada (log 02.11: 856-1000 XP na dobe
    /// u gracza, "szczyt linii").
    ///
    /// Regula (jedna dla gracza i AI, tylko partie pod musztra - Drill.Shape how != 0):
    ///  - weteran: czlowiek spoza bohaterow, oddzial bez UpgradeTargets i Tier >= VeteranTeacherMinTier (6). Jego x = Round(wynik x Number) idzie do puli
    ///    jego broni (jazda / lucznicy / piechota wedlug DefaultFormationClass - jak MusterBook), a wynik modelu dla niego = 0 (gra doda 0 - nic z niczego);
    ///  - uczen: oddzial tej samej broni z awansem (Max > 0), z miejscem room = Number x Max - Xp (po dodaniu XP przez gre) i wlasna musztra x > 0;
    ///    w = min(room, R x x) - najwyzej tyle, ile sam dzis wycwiczyl (R = VeteranTeachMaxRatio);
    ///  - pokrycie f = min(1, K x weteranow / ludzi-uczniow) (K = VeteranPupilsEach); przekazane M = floor(min(pula, f x suma w)), dzielone wedlug w;
    ///  - suma przekazanych <= M <= pula: XP tylko przesuwane. Reszta puli przepada jak dotad (licznik: bez ucznia / limit ucznia / limit awansu).
    /// Bez nowych latek: zbieranie w ArmsDrill.TrainingPostfix (po Drill.Done - liczniki musztry licza wynik PRZED zerowaniem), nauka na poczatku
    /// Drill.TickPostfix (po dodaniu XP przez gre, przed pomiarem "przyjete" gracza). Stan zyje w jednym tiku partii - nic do zapisu.
    /// </summary>
    internal static partial class Drill
    {
        private const int VJ = 0, VL = 1, VP = 2, VArms = 3;          // jazda, lucznicy, piechota
        private static readonly string[] VArmName = { "jazda", "lucznicy", "piechota" };

        /// <summary>Wynik nauki (tik partii gracza albo suma doby AI).</summary>
        private sealed class VetRec
        {
            internal readonly long[] Pool = new long[VArms], Taught = new long[VArms], Cap = new long[VArms], U1 = new long[VArms], U2 = new long[VArms], U3 = new long[VArms];
            internal readonly int[] Vets = new int[VArms], Learn = new int[VArms], Got = new int[VArms];   // weteranow, ludzi-uczniow (L), ludzi, ktorzy dostali > 0
            internal readonly double[] Cover = new double[VArms];
            internal long DeadXp; internal int DeadMen, Parties;           // martwe konce ponizej progu (pomiar), partie z pula
            internal long PoolAll { get { return Pool[0] + Pool[1] + Pool[2]; } }
            internal long TaughtAll { get { return Taught[0] + Taught[1] + Taught[2]; } }
            internal void Clear()
            {
                Array.Clear(Pool, 0, VArms); Array.Clear(Taught, 0, VArms); Array.Clear(Cap, 0, VArms); Array.Clear(U1, 0, VArms); Array.Clear(U2, 0, VArms); Array.Clear(U3, 0, VArms);
                Array.Clear(Vets, 0, VArms); Array.Clear(Learn, 0, VArms); Array.Clear(Got, 0, VArms); Array.Clear(Cover, 0, VArms);
                DeadXp = 0; DeadMen = 0; Parties = 0;
            }
            internal void AddTo(VetRec d)
            {
                for (int c = 0; c < VArms; c++)
                {
                    d.Pool[c] += Pool[c]; d.Taught[c] += Taught[c]; d.Cap[c] += Cap[c]; d.U1[c] += U1[c]; d.U2[c] += U2[c]; d.U3[c] += U3[c];
                    d.Vets[c] += Vets[c]; d.Learn[c] += Learn[c]; d.Got[c] += Got[c];
                    if (Cover[c] > d.Cover[c]) d.Cover[c] = Cover[c];   // recenzja 319b: linia gracza pokazywala pokrycie 0.00 (jedna partia - jej pokrycie; zbiorczo - najwyzsze)
                }
                d.DeadXp += DeadXp; d.DeadMen += DeadMen;
                if (PoolAll > 0) d.Parties++;
            }
        }

        // tik partii (wielokrotnego uzytku, bez alokacji na partie)
        private static readonly VetRec _vT = new VetRec();
        private static bool _vAny;                                      // w tym tiku byla pula albo martwy koniec (do zlaczenia)
        private static readonly Dictionary<CharacterObject, int> _vX = new Dictionary<CharacterObject, int>();   // uczen -> x (dzisiejsza wlasna musztra)
        private static readonly List<int> _vIdx = new List<int>(), _vW = new List<int>(), _vCls = new List<int>(), _vN = new List<int>();
        private static readonly int[] _vLA = new int[VArms];           // ludzie-uczniowie bez wzgledu na miejsce (podzial strat)
        private static readonly long[] _vA = new long[VArms];          // suma R x x bez wzgledu na miejsce (podzial strat)
        // doba AI
        private static readonly VetRec _vDayAi = new VetRec();
        private static long _vTicks; private static int _vStumbles;

        private static int ArmOf(CharacterObject ch)
        {
            switch (ch.DefaultFormationClass)
            {
                case FormationClass.Cavalry: case FormationClass.HorseArcher: case FormationClass.LightCavalry: case FormationClass.HeavyCavalry: return VJ;
                case FormationClass.Ranged: return VL;
                default: return VP;
            }
        }

        private static int VMinTier(Settings s) { int t = s.VeteranTeacherMinTier; return t < 1 ? 1 : (t > 6 ? 6 : t); }

        /// <summary>TickPrefix (kazda partia) i TickFinalizer: czysty stan tiku.</summary>
        private static void VetBegin()
        {
            if (_vAny) { _vT.Clear(); _vAny = false; }
            if (_vX.Count > 0) _vX.Clear();
        }

        /// <summary>ArmsDrill.TrainingPostfix, po Drill.Done, przy how != 0: weteran oddaje x do puli swojej broni (wynik 0), uczen zapisuje swoje x.
        /// Tylko w prawdziwym tiku doby tej partii (_tick) - podpowiedzi UI (ekran partii wola ten sam model) nie zmieniaja sie.</summary>
        internal static void VetPool(MobileParty mp, TroopRosterElement el, ref ExplainedNumber res)
        {
            if (!_tickHooked || mp == null || _tick != mp) return;
            var s = Settings.Current;
            if (s == null || !s.VeteransTeachTheirOwn) return;
            Clk(PTrain);
            try
            {
                var ch = el.Character;
                if (ch == null || ch.IsHero || el.Number <= 0) return;
                int x = TaleWorlds.Library.MathF.Round(res.ResultNumber * el.Number);   // ta sama kwota, ktora doda gra (MobilePartyTrainingBehavior.cs:49)
                if (x <= 0) return;
                var ups = ch.UpgradeTargets;
                if (ups == null || ups.Length == 0)
                {
                    if (ch.Tier >= VMinTier(s))
                    {
                        int c = ArmOf(ch);
                        _vT.Pool[c] += x; _vT.Vets[c] += el.Number; _vAny = true;
                        res = new ExplainedNumber(0f);   // gra doda 0 (i tak by przyciela do 0) - XP idzie do uczniow w TickPostfix
                    }
                    else { _vT.DeadXp += x; _vT.DeadMen += el.Number; _vAny = true; }   // pomiar: martwy koniec ponizej progu (przepada jak dotad)
                }
                else _vX[ch] = x;
            }
            catch (Exception e) { _vStumbles++; Stumble("VetPool", e); }
            finally { Unclk(); }
        }

        /// <summary>Drill.TickPostfix, na poczatku (przed pomiarem "przyjete" gracza): nauka z puli, zlaczenie do doby. Zwraca XP przekazane (gracz).</summary>
        private static long VetTeach(MobileParty mp, Settings s)
        {
            if (!_vAny) return 0;
            long t0 = Stopwatch.GetTimestamp();
            long taught = 0;
            bool main = mp == MobileParty.MainParty;
            try
            {
                if (_vT.PoolAll > 0 && s != null && s.VeteransTeachTheirOwn) taught = Teach(mp, s, _vT);
                else for (int c = 0; c < VArms; c++) _vT.U1[c] += _vT.Pool[c];
            }
            catch (Exception e) { _vStumbles++; Stumble("Teach", e); }   // potkniecie liczone per partia - bez globalnego wylacznika
            finally
            {
                try
                {
                    if (main) { if (_pr != null) { if (_pr.V == null) _pr.V = new VetRec(); _vT.AddTo(_pr.V); } }
                    else _vT.AddTo(_vDayAi);
                }
                catch { }
                _vT.Clear(); _vAny = false; _vX.Clear();
                if (!main) _vTicks += Stopwatch.GetTimestamp() - t0;
            }
            return taught;
        }

        private static long Teach(MobileParty mp, Settings s, VetRec v)
        {
            var r = mp.MemberRoster;
            var party = mp.Party;
            if (r == null || party == null) { for (int c = 0; c < VArms; c++) v.U1[c] += v.Pool[c]; return 0; }
            int K = s.VeteranPupilsEach < 1 ? 1 : (s.VeteranPupilsEach > 20 ? 20 : s.VeteranPupilsEach);
            float R = s.VeteranTeachMaxRatio < 0f ? 0f : (s.VeteranTeachMaxRatio > 3f ? 3f : s.VeteranTeachMaxRatio);
            _vIdx.Clear(); _vW.Clear(); _vCls.Clear(); _vN.Clear();
            Array.Clear(_vLA, 0, VArms); Array.Clear(_vA, 0, VArms);
            // przebieg 1: uczniowie (ta sama bron co pula, z awansem, z wlasna musztra dzis); limit gry jak TickPrefix / PartyBase.OnXpChanged
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                var ch = el.Character;
                if (ch == null || ch.IsHero || el.Number <= 0) continue;
                int x;
                if (!_vX.TryGetValue(ch, out x) || x <= 0) continue;
                int c = ArmOf(ch);
                if (v.Pool[c] <= 0) continue;
                int max = 0;
                var ups = ch.UpgradeTargets;
                if (ups != null) for (int u = 0; u < ups.Length; u++) { int cost = ch.GetUpgradeXpCost(party, u); if (cost > max) max = cost; }
                if (max <= 0) continue;
                long cap = (long)Math.Floor(R * (double)x);
                _vLA[c] += el.Number; _vA[c] += cap;
                long room = (long)el.Number * max - el.Xp;
                long w = Math.Min(room, cap);
                if (w <= 0) continue;
                _vIdx.Add(i); _vW.Add((int)Math.Min(w, int.MaxValue)); _vCls.Add(c); _vN.Add(el.Number);
                v.Learn[c] += el.Number; v.Cap[c] += w;
            }
            // M_c = floor(min(pula, f x suma w)), f = min(1, K x weteranow / ludzi-uczniow)
            var M = new long[VArms];
            for (int c = 0; c < VArms; c++)
            {
                if (v.Pool[c] <= 0) continue;
                double f = v.Learn[c] > 0 ? Math.Min(1.0, K * (double)v.Vets[c] / v.Learn[c]) : 0.0;
                v.Cover[c] = f;
                M[c] = v.Cap[c] > 0 ? (long)Math.Floor(Math.Min((double)v.Pool[c], f * v.Cap[c])) : 0;
                if (M[c] > v.Pool[c]) M[c] = v.Pool[c];
            }
            // przebieg 2: t = floor(M x w / suma w), reszta z zaokraglen po kolei do miejsca (w); dodanie w ksiedze jako musztra
            var given = new long[VArms];
            var tl = new int[_vIdx.Count];
            for (int j = 0; j < _vIdx.Count; j++)
            {
                int c = _vCls[j];
                if (M[c] <= 0) continue;
                long t = M[c] * _vW[j] / v.Cap[c];
                if (t > _vW[j]) t = _vW[j];
                tl[j] = (int)t; given[c] += t;
            }
            for (int j = 0; j < _vIdx.Count; j++)
            {
                int c = _vCls[j];
                long rem = M[c] - given[c];
                if (rem <= 0) continue;
                long add = Math.Min(rem, (long)_vW[j] - tl[j]);
                if (add > 0) { tl[j] += (int)add; given[c] += add; }
            }
            long all = 0;
            int prev = SkillLedger.Enter(SkillLedger.SDrill);
            try
            {
                for (int j = 0; j < _vIdx.Count; j++)
                {
                    if (tl[j] <= 0) continue;
                    try
                    {
                        r.AddXpToTroopAtIndex(_vIdx[j], tl[j]);
                        int c = _vCls[j];
                        v.Taught[c] += tl[j]; v.Got[c] += _vN[j]; all += tl[j];
                    }
                    catch (Exception e) { _vStumbles++; Stumble("Teach(add)", e); }
                }
            }
            finally { SkillLedger.Leave(prev); }
            // podzial straty: bez ucznia (brak oddzialu tej broni z awansem i wlasna musztra), limit ucznia (pokrycie K i R), limit awansu (miejsce)
            for (int c = 0; c < VArms; c++)
            {
                long lost = v.Pool[c] - v.Taught[c];
                if (lost <= 0) continue;
                if (_vLA[c] <= 0) { v.U1[c] += lost; continue; }
                double fA = Math.Min(1.0, K * (double)v.Vets[c] / _vLA[c]);
                long ideal = (long)Math.Floor(Math.Min((double)v.Pool[c], fA * _vA[c]));
                long u2 = Math.Max(0L, Math.Min(lost, v.Pool[c] - ideal));
                v.U2[c] += u2; v.U3[c] += lost - u2;
            }
            return all;
        }

        /// <summary>m114 / 319b: zdanie wiadomosci gracza o nauce weteranow ("585 XP passed on - 22 veterans taught 18 riders, ...").</summary>
        private static void VetMessage(StringBuilder sb, VetRec v)
        {
            long t = v.TaughtAll;
            if (t <= 0) return;
            sb.Append("; ").Append(t).Append(" XP passed on - ");
            bool first = true;
            for (int c = 0; c < VArms; c++)
            {
                if (v.Taught[c] <= 0) continue;
                if (!first) sb.Append(", ");
                first = false;
                int vn = v.Vets[c], g = v.Got[c];
                sb.Append(vn).Append(vn == 1 ? " veteran taught " : " veterans taught ").Append(g).Append(' ')
                  .Append(c == VJ ? (g == 1 ? "rider" : "riders") : c == VL ? (g == 1 ? "archer" : "archers") : (g == 1 ? "foot soldier" : "foot soldiers"));
            }
        }

        /// <summary>Linie doby (Drill.Daily, przy DrillLog): "Weterani ucza (319b): dzien N gracz | ..." i "... AI | ...".</summary>
        private static void VetLines(int day, Settings s)
        {
            var inv = CultureInfo.InvariantCulture;
            string mode = s.VeteransTeachTheirOwn ? "CZYNNA" : "WYLACZONA";
            string par = " (prog tier " + VMinTier(s) + ", uczniow na weterana " + s.VeteranPupilsEach + ", limit ucznia x" + F2(s.VeteranTeachMaxRatio) + ")";
            // gracz
            var sb = new StringBuilder("Weterani ucza (319b): dzien ").Append(day).Append(" gracz | ").Append(mode).Append(par);
            var r = _prLast;
            if (r == null || r.Day < day - 1) sb.Append(" | brak treningu wedlug musztry w tej dobie");
            else if (r.V == null || (r.V.PoolAll <= 0 && r.V.DeadMen <= 0)) sb.Append(" | brak weteranow w druzynie (pula 0)");
            else
            {
                var v = r.V;
                for (int c = 0; c < VArms; c++)
                {
                    if (v.Pool[c] <= 0 && v.Vets[c] <= 0) continue;
                    long lost = v.Pool[c] - v.Taught[c];
                    sb.Append(" | ").Append(VArmName[c]).Append(" pula ").Append(v.Pool[c]).Append(" (weteranow ").Append(v.Vets[c]).Append(", uczniow ").Append(v.Learn[c])
                      .Append(", dostalo ").Append(v.Got[c]).Append(", pokrycie ").Append(F2(v.Cover[c])).Append(", limit uczniow ").Append(v.Cap[c]).Append(") -> przekazane ")
                      .Append(v.Taught[c]).Append(", przepadlo ").Append(lost).Append(" (bez ucznia ").Append(v.U1[c]).Append(", limit ucznia ").Append(v.U2[c])
                      .Append(", limit awansu ").Append(v.U3[c]).Append(')');
                }
                long pa = v.PoolAll, ta = v.TaughtAll;
                sb.Append(" | razem pula ").Append(pa).Append(", przekazane ").Append(ta).Append(", przepadlo ").Append(pa - ta)
                  .Append(", przekazane <= pula ").Append(ta <= pa ? "TAK" : "NIE")
                  .Append(" | martwe konce ponizej progu: ludzi ").Append(v.DeadMen).Append(", XP ").Append(v.DeadXp)
                  .Append(" | musztra: wyliczone ").Append(r.Computed).Append(", przyjete ").Append(r.Accepted).Append(" (w tym od weteranow ").Append(ta).Append("), uciete ").Append(r.Cut)
                  .Append(" -> ").Append(r.Accepted == r.Computed - r.Cut ? "ZGODNE" : "NIEZGODNE (roznica " + (r.Computed - r.Cut - r.Accepted) + ")");
            }
            Log.Info(sb.Append(" | potkniecia ").Append(_vStumbles).Append('.').ToString());
            // AI
            var a = _vDayAi;
            long pAll = a.PoolAll, tAll = a.TaughtAll;
            var sa = new StringBuilder("Weterani ucza (319b): dzien ").Append(day).Append(" AI | ").Append(mode).Append(par)
              .Append(" | partii z weteranami ").Append(a.Parties).Append(" (z ").Append(_aParties).Append(" partii pod musztra), weteranow ").Append(a.Vets[0] + a.Vets[1] + a.Vets[2])
              .Append(" (jazda ").Append(a.Vets[0]).Append(", lucznicy ").Append(a.Vets[1]).Append(", piechota ").Append(a.Vets[2]).Append(")")
              .Append(", pula ").Append(pAll.ToString(inv)).Append(" (").Append(a.Pool[0]).Append('/').Append(a.Pool[1]).Append('/').Append(a.Pool[2]).Append(")")
              .Append(", przekazane ").Append(tAll.ToString(inv)).Append(" (").Append(a.Taught[0]).Append('/').Append(a.Taught[1]).Append('/').Append(a.Taught[2]).Append(")")
              .Append(", uczniow (ludzi) ").Append(a.Learn[0] + a.Learn[1] + a.Learn[2]).Append(", dostalo ").Append(a.Got[0] + a.Got[1] + a.Got[2])
              .Append(", przepadlo bez ucznia ").Append(a.U1[0] + a.U1[1] + a.U1[2]).Append(" / limit ucznia ").Append(a.U2[0] + a.U2[1] + a.U2[2])
              .Append(" / limit awansu ").Append(a.U3[0] + a.U3[1] + a.U3[2])
              .Append(", przekazane <= pula ").Append(tAll <= pAll ? "TAK" : "NIE")
              .Append(" | martwe konce ponizej progu: ludzi ").Append(a.DeadMen).Append(", XP ").Append(a.DeadXp.ToString(inv))
              .Append(" | koszt ").Append((_vTicks * 1000.0 / Stopwatch.Frequency).ToString("0.00", inv)).Append(" ms | potkniecia ").Append(_vStumbles).Append('.');
            Log.Info(sa.ToString());
        }

        private static void VetClearDay() { _vDayAi.Clear(); _vTicks = 0; _vStumbles = 0; }
        private static void VetReset() { _vT.Clear(); _vAny = false; _vX.Clear(); VetClearDay(); }
    }
}
