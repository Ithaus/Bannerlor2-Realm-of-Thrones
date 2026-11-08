using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// 364 (Jeff 08.11: "jak garnizon jest pelny to wiecej nie rekrutujemy") - LUDZIE NA PATROLU BK SA CZESCIA ZALOGI W AUTOWERBUNKU.
    /// Przyczyna (diagnoza 08.11, dekompilacja): BK HandleGarrison (5%/dobe, zaloga >= 100 zdrowych) wysyla patrol - GarrisonPartyComponent.CreateParty zdejmuje
    /// z zalogi 30..50% zdrowych. Autowerbunek gry (GarrisonRecruitmentCampaignBehavior.GetAutoRecruitmentGarrisonChangeExplainedNumber, LimitMax "Party Size
    /// Limit" = PartySizeLimit - NumberOfAllMembers) widzi wolne miejsce i dobiera (K8 2/d + budynki 338). Po >48 h BKPartyBehavior.AddPatrolBehavior wrzuca caly
    /// patrol z powrotem do zalogi bez sprawdzenia miejsca - zaloga ponad limit, gra zdejmuje 25% nadwyzki na dobe (W4c odsyla ich do wsi), zloto werbunku przepada.
    /// Tutaj: postfiks z ref ExplainedNumber na tej samej metodzie, PRZED A5LimitPostfix (Priority.High; A5 ma domyslny 400): miejsce = PartySizeLimit (ten sam
    /// limit, ktorego uzywa dezercja) - NumberOfAllMembers - ludzie w czynnych patrolach BK tej twierdzy (GarrisonArmory.MenOnPatrol - mapa patroli doby, nowy patrol
    /// dopisywany przez postfiks BK CreateParty). Mniejsze od biezacego LimitMaxValue -> LimitMax(max(0, miejsce)). Bez patroli - nic (szybkie wyjscie).
    /// Jedna regula dla AI i gracza; obejmuje gre, K8, budynki 338, zaloge frontu 326c i podpowiedz zalogi w UI (ta sama metoda). Powrotu patrolu i dezercji nie ruszamy.
    /// Linie: "Patrole BK a werbunek zalog (364)" (swiat) i "Zalogi gracza (364)" (kazda twierdza rodu gracza: stan, limit, patrol, werbunek, przepelnienie, zold).
    /// </summary>
    internal static class GarrisonPatrol364
    {
        internal static bool On { get { var s = Settings.Current; return s != null && s.GarrisonCountsPatrols; } }

        private static readonly object _lock = new object();

        // liczniki od ostatniej linii (okno A5 bez dodatkowych wywolan zalogi frontu 326c - te liczy jej wlasna linia)
        private static int _stopTowns, _stopPlayerTowns, _limTowns, _stumbles;
        private static long _stopMen, _stopPlayerMen;
        // twierdze rodu gracza: zwerbowani (calosc okna TickAutoRecruitmentGarrisonChange, z zaloga frontu) i zatrzymani przez patrol
        private static readonly Dictionary<Settlement, int> _rec = new Dictionary<Settlement, int>();
        private static readonly Dictionary<Settlement, int> _stop = new Dictionary<Settlement, int>();
        private static readonly HashSet<string> _err = new HashSet<string>();

        internal static void Reset()
        {
            lock (_lock) { _stopTowns = _stopPlayerTowns = _limTowns = _stumbles = 0; _stopMen = _stopPlayerMen = 0; _rec.Clear(); _stop.Clear(); }
            _err.Clear();
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_err.Add(where)) Log.Error("GarrisonPatrol364." + where, e); } catch { }
        }

        private static int CeilMen(float x) { return x > 0f ? (int)Math.Ceiling(x) : 0; }

        /// <summary>Postfiks GetAutoRecruitmentGarrisonChangeExplainedNumber(Town) - Priority.High (przed A5LimitPostfix, ktory tylko czyta wynik do licznikow A5/K8/338).</summary>
        public static void LimitPostfix(Town __0, ref ExplainedNumber __result)
        {
            if (__0 == null || !On) return;
            try
            {
                var st = __0.Settlement;
                var g = __0.GarrisonParty;
                if (st == null || g == null || g.Party == null) return;              // bez zalogi gra liczy limit z modelu - patrolu i tak nie ma
                // poprawka recenzji 364: bez budzetu zoldu albo bez ochotnikow gra nie dodaje bazy ani limitow (LimitMaxValue = float.MaxValue) - nie ma czego
                // ograniczac (inaczej zbedna linia w UI i zawyzony licznik "twierdz z limitem patrolu")
                if (!(__result.BaseNumber > 0f)) return;
                int pat = GarrisonArmory.MenOnPatrol(st);
                if (pat <= 0) return;
                // poprawka recenzji 364: porownanie juz po obcieciu do 0 - limit gry bywa ujemny (zaloga ponad limit), LimitMax nadpisuje, wiec nie wolno go podniesc do 0
                int cap = Math.Max(0, g.Party.PartySizeLimit - g.Party.NumberOfAllMembers - pat);
                if (!((float)cap < __result.LimitMaxValue)) return;
                int before = CeilMen(__result.ResultNumber);
                __result.LimitMax(cap, new TextObject("Party Size Limit (men on patrol)"));   // tekst tworzony dopiero tu (bez statycznego TextObject przy starcie)
                if (!ArmsFromPeople.InA5Window(__0)) return;                          // UI i zaloga frontu: limit tak, liczniki nie
                int stopped = before - CeilMen(__result.ResultNumber);
                bool player = st.OwnerClan == Clan.PlayerClan;
                lock (_lock)
                {
                    _limTowns++;
                    if (stopped > 0)
                    {
                        _stopTowns++; _stopMen += stopped;
                        if (player) { _stopPlayerTowns++; _stopPlayerMen += stopped; int v; _stop.TryGetValue(st, out v); _stop[st] = v + stopped; }
                    }
                }
            }
            catch (Exception e) { Stumble("LimitPostfix", e); }
        }

        /// <summary>Prefiks TickAutoRecruitmentGarrisonChange(Town): stan zalogi twierdzy rodu gracza przed werbunkiem (-1 - nie gracza).</summary>
        public static void RecPre(Town __0, out int __state)
        {
            __state = -1;
            try
            {
                if (__0 == null || __0.Settlement == null || __0.Settlement.OwnerClan != Clan.PlayerClan) return;
                var g = __0.GarrisonParty;
                __state = g != null ? g.MemberRoster.TotalManCount : 0;
            }
            catch (Exception e) { __state = -1; Stumble("RecPre", e); }
        }

        public static Exception RecFin(Exception __exception, Town __0, int __state)
        {
            if (__state < 0) return __exception;
            try
            {
                var g = __0 != null ? __0.GarrisonParty : null;
                int d = (g != null ? g.MemberRoster.TotalManCount : 0) - __state;
                if (d > 0) lock (_lock) { int v; _rec.TryGetValue(__0.Settlement, out v); _rec[__0.Settlement] = v + d; }
            }
            catch (Exception e) { Stumble("RecFin", e); }
            return __exception;
        }

        /// <summary>Wolane na poczatku ArmsFromPeople.Daily (przed linia "Wojsko z ludzi"): dwie linie, zerowanie licznikow (takze bez logu).</summary>
        internal static void Daily(bool log)
        {
            int day = 0;
            try { day = (int)CampaignTime.Now.ToDays - 1; } catch { }
            int stopTowns, stopPlayerTowns, limTowns, stumbles; long stopMen, stopPlayerMen;
            Dictionary<Settlement, int> rec, stop;
            lock (_lock)
            {
                stopTowns = _stopTowns; stopPlayerTowns = _stopPlayerTowns; limTowns = _limTowns; stumbles = _stumbles; stopMen = _stopMen; stopPlayerMen = _stopPlayerMen;
                rec = new Dictionary<Settlement, int>(_rec); stop = new Dictionary<Settlement, int>(_stop);
                _stopTowns = _stopPlayerTowns = _limTowns = _stumbles = 0; _stopMen = _stopPlayerMen = 0; _rec.Clear(); _stop.Clear();
            }
            if (!log) { try { OverflowHome.ClearPlayerOut(); } catch { } return; }
            try
            {
                Log.Info("Patrole BK a werbunek zalog (364): dzien " + day + " | regula " + (On ? "WLACZONA" : "WYLACZONA (Garrison Counts Patrols)")
                         + " | twierdz z limitem patrolu " + limTowns + ", zatrzymane przez patrol BK: twierdz " + stopTowns + ", ludzi " + stopMen
                         + " (w tym gracza: twierdz " + stopPlayerTowns + ", ludzi " + stopPlayerMen + ")" + (stumbles > 0 ? " | potkniecia " + stumbles : ""));
            }
            catch (Exception e) { Stumble("Daily(swiat)", e); }
            try
            {
                var sb = new StringBuilder(512);
                sb.Append("Zalogi gracza (364): dzien ").Append(day);
                int n = 0;
                var pc = Clan.PlayerClan;
                if (pc != null)
                    foreach (var t in pc.Fiefs)
                    {
                        try
                        {
                            var st = t != null ? t.Settlement : null;
                            if (st == null) continue;
                            n++;
                            var g = t.GarrisonParty;
                            int men = 0, lim = 0, wage = 0;
                            if (g != null && g.Party != null) { men = g.Party.NumberOfAllMembers; lim = g.Party.PartySizeLimit; wage = g.TotalWage; }
                            int r, s; rec.TryGetValue(st, out r); stop.TryGetValue(st, out s);
                            sb.Append(" | ").Append(st.Name).Append(": stan ").Append(men).Append(" / limit ").Append(lim)
                              .Append(", na patrolu ").Append(GarrisonArmory.MenOnPatrol(st))
                              .Append(", werbunek dzis ").Append(r);
                            if (s > 0) sb.Append(" (zatrzymane przez patrol ").Append(s).Append(")");
                            sb.Append(", odeszli z przepelnienia ").Append(OverflowHome.TakePlayerOut(st)).Append(" (W4c").Append(OverflowHome.On ? "" : " wylaczone").Append(")")
                              .Append(", zold ").Append(wage).Append(" / limit zoldu ").Append(st.GarrisonWagePaymentLimit);
                        }
                        catch (Exception e) { Stumble("Daily(osada)", e); }
                    }
                if (n == 0) sb.Append(" | brak twierdz rodu gracza");
                OverflowHome.ClearPlayerOut();
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Daily(gracz)", e); }
        }
    }
}
