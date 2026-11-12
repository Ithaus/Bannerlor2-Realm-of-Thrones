using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// 471b-3 (Jeff 13.11, odpowiedz D3: "Okay rabunek ma sens i zmniejsza opinie o lordzie"). FURAZ OBOZU OBLEZNICZEGO.
    /// DZIS (test 60 dob, badanie 471): obozowi AI konczy sie jedzenie po 8-16 dobach (wiersze SZ: 8.5 -> 2.1 doby) i to konczy oblezenia - cele 471 (40-150 dob)
    /// byly fizycznie niemozliwe.
    /// ODTAD (klucz SiegeCampForage): raz na dobe kazda druzyna obozu AI (regula 471 dziala - SiegeWalls.Acts471: nie gracz, nie Inni, nie skrypt fabuly, nie
    /// osada klanu gracza do 471b-5) z zapasem ponizej KeepDays (5) dob bierze zboze ze SPICHLERZY wsi okregu oblezonej osady (Settlement.BoundVillages) - bez
    /// zaplaty: na dobe najwyzej MaxDays (2) dob swojego zuzycia, do KeepDays. Ta sama droga co rabunek E7/R1 (DistrictRaid.Eat): Famine.Take(wies, sztuk, 20
    /// racji za sztuke) zdejmuje racje ze spichlerza okregu, druzyna dostaje tyle sztuk zboza (DefaultItems.Grain) w ramce GoodsLedger FRaid (zrodlo "zboze ze
    /// spichlerza okregu dla napastnikow") - zamknieta ekonomia: kazda sztuka ma platnika (spichlerz wsi), nic z niczego. Wies, ktorej spichlerz pusty, gloduje
    /// regula Famine (E4/6) - bez osobnego kodu. Tylko wsie strony wroga (wojna frakcji wsi z druzyna); najpierw wies z najwiekszym spichlerzem.
    /// OPINIA: kazdego dnia, gdy ludzie lorda (LeaderHero druzyny) zabrali cos z danej wsi, relacja lorda z kazdym notablem tej wsi -1
    /// (ChangeRelationAction.ApplyRelationChangeBetweenHeroes, bez komunikatu), najwyzej RelCap (20) na pare lord-wies w jednym oblezeniu.
    /// Kontrola (linia doby): racje ubyle ze spichlerzy = sztuki zboza przybyle do obozu x 20 (roznica do logu).
    /// Klucz NIE albo spichlerze wylaczone (Famine.On) = nic sie nie dzieje (oboz zyje z tego, co niesie - jak dotad). Wyjatek per druzyna = licznik potkniec.
    /// </summary>
    internal static class SiegeForage471
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        internal const double KeepDays = 5.0, MaxDays = 2.0, RationsPerGrain = 20.0;
        internal const int RelCap = 20;

        internal static bool On { get { var s = Settings.Current; return s != null && s.SiegeCampForage; } }
        private static bool LogOn { get { var s = Settings.Current; return s != null && s.SiegeWallsLog && s.LogEnabled; } }

        private sealed class Rec { public long Pieces; public int Days, Rel; public readonly Dictionary<string, int> RelOf = new Dictionary<string, int>(StringComparer.Ordinal); public readonly HashSet<string> Vill = new HashSet<string>(StringComparer.Ordinal), Done = new HashSet<string>(StringComparer.Ordinal); }
        private static readonly Dictionary<SiegeEvent, Rec> _recs = new Dictionary<SiegeEvent, Rec>();
        private static int _stumbles;
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static long _dPieces; private static double _dDrop, _dBal; private static int _dParties, _dRel, _dSieges, _dEmpty;

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_err.Add(where)) Log.Error("SiegeForage471." + where + " (potkniecie " + _stumbles + ")", e); } catch { }
        }
        private static string F(double v, string f) { return v.ToString(f, Inv); }

        /// <summary>Z SiegeWalls.Reset (nowa kampania i kazde wczytanie).</summary>
        internal static void Reset() { _recs.Clear(); _stumbles = 0; _err.Clear(); Zero(); }
        private static void Zero() { _dPieces = 0; _dDrop = _dBal = 0.0; _dParties = _dRel = _dSieges = _dEmpty = 0; }

        /// <summary>Z SiegeWalls.SessionStart: linia startowa 471b-3.</summary>
        internal static void SessionStart()
        {
            try
            {
                Log.Info("Furaz obozu (471b-3 start): klucz Siege Camp Forages The Villages " + (On ? "TAK" : "NIE") + ", spichlerze wsi (Famine) " + (Famine.On ? "tak" : "NIE - furaz niemozliwy")
                         + " | druzyna obozu AI ponizej " + F(KeepDays, "0") + " dob jedzenia bierze najwyzej " + F(MaxDays, "0") + " doby zuzycia na dobe ze spichlerzy wsi okregu (" + F(RationsPerGrain, "0")
                         + " racji za sztuke zboza, bez zaplaty) | opinia: relacja lorda z notablami wsi -1 za dobe rabunku, najwyzej " + RelCap + " na oblezenie.");
            }
            catch (Exception e) { Stumble("SessionStart", e); }
        }

        /// <summary>Z ArmouryBehavior (doba, po SiegeCamp471.Daily). Nie rzuca.</summary>
        internal static void Daily()
        {
            try
            {
                List<SiegeEvent> gone = null;
                foreach (var kv in _recs) if (kv.Key == null || kv.Key.BesiegedSettlement == null || kv.Key.BesiegedSettlement.SiegeEvent != kv.Key) { if (gone == null) gone = new List<SiegeEvent>(); gone.Add(kv.Key); }
                if (gone != null) foreach (var ev in gone) _recs.Remove(ev);
                if (!On || !Famine.On) return;
                var mgr = Campaign.Current != null ? Campaign.Current.SiegeEventManager : null;
                if (mgr == null || mgr.SiegeEvents == null) return;
                var grain = DefaultItems.Grain;
                if (grain == null) return;
                var sb = new StringBuilder(800);
                foreach (var ev in new List<SiegeEvent>(mgr.SiegeEvents))
                {
                    try { One(ev, grain, sb); }
                    catch (Exception e) { Stumble("One", e); }
                }
                if (LogOn)
                    Log.Info("Furaz obozu (471b-3 doba): dzien " + (int)CampaignTime.Now.ToDays + " | oblezen z furazem " + _dSieges + ", druzyn nakarmionych " + _dParties + ", bez jedzenia we wsiach " + _dEmpty
                             + " | zboze do obozow " + _dPieces + " szt. (" + F(_dPieces * RationsPerGrain, "0") + " racji), spichlerze wsi ubyly " + F(_dDrop, "0") + " racji, kontrola (przybyle - ubyle) " + F(_dBal, "0.0")
                             + " | relacja z notablami -" + _dRel + " | " + (sb.Length == 0 ? "-" : sb.ToString()) + (_stumbles > 0 ? " | potkniecia " + _stumbles : "") + ".");
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally { Zero(); }
        }

        private static void One(SiegeEvent ev, ItemObject grain, StringBuilder sb)
        {
            if (SiegeWalls.Acts471(ev) != null) return;
            var st = ev.BesiegedSettlement;
            var camp = ev.BesiegerCamp;
            if (st == null || camp == null || st.BoundVillages == null || st.BoundVillages.Count == 0) return;
            var vills = new List<KeyValuePair<double, Village>>();
            foreach (var v in st.BoundVillages)
            {
                double f;
                if (v != null && v.Settlement != null && Famine.FoodOf(v, out f) && f >= RationsPerGrain) vills.Add(new KeyValuePair<double, Village>(f, v));
            }
            Rec rec = null;
            var took = new Dictionary<string, int>(StringComparer.Ordinal);
            int fed = 0, hungry = 0;
            var seen = new HashSet<MobileParty>();
            foreach (var pb in camp.GetInvolvedPartiesForEventType())
            {
                var mp = pb != null ? pb.MobileParty : null;
                if (mp == null || !seen.Add(mp) || mp.IsMainParty || mp.MapEvent != null || mp.ItemRoster == null || Undead.Party(mp)) continue;
                try
                {
                    float ch = mp.FoodChange;
                    if (!(ch < 0f)) continue;
                    double days = mp.Food / -ch;
                    if (days >= KeepDays) continue;
                    int want = (int)Math.Ceiling(Math.Min(KeepDays - days, MaxDays) * -ch);
                    if (want <= 0) continue;
                    hungry++;
                    int got = 0;
                    vills.Sort((a, b) => b.Key.CompareTo(a.Key));
                    for (int i = 0; i < vills.Count && got < want; i++)
                    {
                        var v = vills[i].Value;
                        var vf = v.Settlement.MapFaction;
                        if (vf == null || mp.MapFaction == null || !FactionManager.IsAtWarAgainstFaction(vf, mp.MapFaction)) continue;   // tylko wsie wroga
                        double f0, f1;
                        Famine.FoodOf(v, out f0);
                        int g0 = mp.ItemRoster.GetItemNumber(grain);
                        int n = Famine.Take(v, want - got, RationsPerGrain);
                        if (n <= 0) continue;
                        var gf = GoodsLedger.Begin(GoodsLedger.FRaid, mp);
                        try { mp.ItemRoster.AddToCounts(grain, n); }
                        finally { GoodsLedger.End(gf); }
                        int g1 = mp.ItemRoster.GetItemNumber(grain);
                        double drop = Famine.FoodOf(v, out f1) ? f0 - f1 : n * RationsPerGrain;
                        vills[i] = new KeyValuePair<double, Village>(f0 - drop, v);
                        got += n;
                        _dPieces += n; _dDrop += drop; _dBal += (g1 - g0) * RationsPerGrain - drop;
                        int c; took.TryGetValue(v.Settlement.StringId, out c); took[v.Settlement.StringId] = c + n;
                        if (rec == null && !_recs.TryGetValue(ev, out rec)) { rec = new Rec(); _recs[ev] = rec; }
                        rec.Pieces += n; rec.Vill.Add(v.Settlement.StringId);
                        Opinion(rec, mp.LeaderHero, v);
                    }
                    if (got > 0) { fed++; _dParties++; }
                }
                catch (Exception e) { Stumble("Party", e); }
            }
            if (rec != null && took.Count > 0) { rec.Days++; _dSieges++; }
            if (hungry > 0 && took.Count == 0) _dEmpty++;
            if ((hungry > 0 || took.Count > 0) && sb.Length < 2500)
            {
                sb.Append(sb.Length > 0 ? "; " : "").Append(st.StringId).Append(": glodnych druzyn ").Append(hungry).Append(", nakarmionych ").Append(fed);
                if (took.Count == 0) sb.Append(vills.Count == 0 ? " (spichlerze wsi puste)" : " (bez wsi wroga ze zbozem)");
                else { sb.Append(", wsie"); foreach (var kv in took) sb.Append(' ').Append(kv.Key).Append(' ').Append(kv.Value).Append(" szt."); }
            }
        }

        /// <summary>Relacja lorda z notablami wsi -1 (raz na dobe i pare lord-wies, najwyzej RelCap w oblezeniu).</summary>
        private static void Opinion(Rec rec, Hero lord, Village v)
        {
            try
            {
                if (lord == null || !lord.IsAlive || v == null || v.Settlement == null) return;
                string key = lord.StringId + "|" + v.Settlement.StringId;
                int day = (int)CampaignTime.Now.ToDays;
                string dayKey = key + "|" + day;
                if (!rec.Done.Add(dayKey)) return;   // ta para juz dzis
                int n; rec.RelOf.TryGetValue(key, out n);
                if (n >= RelCap) return;
                bool any = false;
                foreach (var h in v.Settlement.Notables)
                {
                    if (h == null || !h.IsAlive || h == lord) continue;
                    ChangeRelationAction.ApplyRelationChangeBetweenHeroes(lord, h, -1, false);
                    _dRel++; any = true;
                }
                if (any) { rec.RelOf[key] = n + 1; rec.Rel++; }
            }
            catch (Exception e) { Stumble("Opinion", e); }
        }

        /// <summary>Do linii "(471) koniec": furaz tego oblezenia.</summary>
        internal static string EndText(SiegeEvent ev)
        {
            Rec r;
            if (ev == null || !_recs.TryGetValue(ev, out r)) return "furaz (471b-3): " + (On ? "brak" : "klucz NIE");
            return "furaz (471b-3): " + r.Pieces + " szt. zboza (" + F(r.Pieces * RationsPerGrain, "0") + " racji) z " + r.Vill.Count + " wsi w " + r.Days + " dob, relacja lord-notable obnizona " + r.Rel + " razy";
        }
    }
}
