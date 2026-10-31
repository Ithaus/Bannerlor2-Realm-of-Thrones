using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// m112-inwest (24.10, Jeff zainwestowal 19 854 zl w prace wioski przy White Harbor, po 9 dobach 5 zl i nic nie widzial: "i ma byc to poakzane w thiefts zeby
    /// wedizal gdzie mam majtek i inwestycje"): odczyty prac gracza dla zakladki Fiefs ekranu rodu (FiefInvestments) i komunikatu dnia. Same odczyty pod Gate,
    /// nazwy i stan osad poza blokada; nie rzuca (potkniecie liczone w DistrictWorks).
    /// </summary>
    internal static partial class DistrictWorks
    {
        /// <summary>Jedna praca gracza (wiersz arm_works rodu gracza) do zakladki Fiefs.</summary>
        internal sealed class MyWork
        {
            public string Village, District, Market, Kind; public bool Main;
            public double Units, Eff, Standing, Homesteads, Idle, Lost, Reb, RebCost;
            public int FullDay = -1, RebDay = -1, Today;
            public long In, PaidAll; public double Paid28; public int PaidDaysN = 28;   // PaidDaysN - ile ostatnich dob obejmuje Paid28 (mniej po zapisie sprzed m112)
            public int AgeDays = -1;                                 // 305e: doby od zakupu prac (r.Day0; -1 - nieznane, wiersz bez Day0)
            public bool SinceBuy;                                    // 305e: PaidDaysN przyciete do dob od zakupu (klucz WorksPaidSinceBuy) - prace mlodsze niz historia
            public string WarWith, Paused;                           // wojna z krolestwem okregu (bez wyplat); wstrzymane z innego powodu (klucz, droga produkcji)
            internal string Dist, Part;
        }

        /// <summary>Prace rodu gracza (z jednostkami albo wplatami), w kolejnosci zapisu. Pusta lista - brak prac albo rodu gracza.</summary>
        internal static List<MyWork> PlayerWorks()
        {
            var res = new List<MyWork>();
            try
            {
                string pid = PlayerClanId();
                if (pid == null) return res;
                int today = Today();
                int hn = HistDays(today);                                    // m112-inwest po recenzji: zapis sprzed m112 - doby od wczytania
                var st0 = Settings.Current;
                bool sinceBuy = st0 != null && st0.WorksPaidSinceBuy;      // 305e: srednia od zakupu, gdy prace mlodsze niz historia (Seven Elms: 22 zl w 13 dobach pokazane jako 1 zl/d)
                lock (Gate)
                    foreach (var r in _rows)
                    {
                        if (r.Clan != pid || (!(r.Held() > 1e-6) && r.In <= 0)) continue;
                        var w = new MyWork { Dist = r.Dist, Part = r.Part, Main = r.Part == MainPart, Today = today, In = r.In, PaidAll = r.Out, Paid28 = r.PaidSince(today - 27), PaidDaysN = hn };
                        if (r.Day0 >= 0) w.AgeDays = Math.Max(1, today - r.Day0 + 1);
                        if (sinceBuy && w.AgeDays > 0 && w.AgeDays < hn) { w.PaidDaysN = w.AgeDays; w.SinceBuy = true; }   // Paid28 = PaidSince(today - 27) obejmuje i tak tylko doby od zakupu
                        w.Kind = r.Kind >= 0 && r.Kind < Kinds ? KindName[r.Kind] : "works";
                        w.Units = r.U + r.Building(); w.Eff = r.Eff(today); w.Idle = r.I; w.Lost = r.Z; w.Reb = r.Rebuildable();
                        foreach (var l in r.Ramp) if (l.Units > 1e-9 && l.Day + l.Ready > w.FullDay) w.FullDay = l.Day + l.Ready;
                        foreach (var rb in r.Rebs) if (rb.Units > 1e-9 && (w.RebDay < 0 || rb.Deadline < w.RebDay)) w.RebDay = rb.Deadline;
                        if (w.Reb > 1e-6 && r.Kind >= 0 && r.Kind < Kinds) w.RebCost = KR[r.Kind] * r.Kit * w.Reb;
                        res.Add(w);
                    }
                if (res.Count == 0) return res;
                var pc = Clan.PlayerClan;
                var book = new List<VillageBook.PartView>();
                foreach (var w in res)
                {
                    var st = Settlement.Find(w.Dist);
                    var v = st != null ? st.Village : null;
                    w.District = v != null ? Name(v) : w.Dist;
                    w.Village = w.Main ? w.District : w.Part;
                    var mk = v != null ? MarketOf(v) : null;
                    w.Market = mk != null && mk.Name != null ? mk.Name.ToString() : "?";
                    if (v != null && VillageBook.PartsOf(v, book))
                        foreach (var p in book)
                            if (PartKey(p.Uid) == w.Part) { if (!w.Main && !string.IsNullOrEmpty(p.Name)) w.Village = p.Name; w.Homesteads = p.Nw; w.Standing = Math.Max(0, p.Nw - p.Br - p.Bf); break; }
                    if (!w.Main && w.Village == w.Part) w.Village = "a side village of " + w.District;   // m112-inwest po recenzji: czesci nie ma w ksiedze wsi - bez surowego uid
                    IFaction mf = pc != null ? pc.MapFaction : null, df = st != null ? st.MapFaction : null;
                    if (mf != null && df != null && mf != df && FactionManager.IsAtWarAgainstFaction(mf, df)) w.WarWith = df.Name != null ? df.Name.ToString() : "them";
                    if (!On) w.Paused = "frozen - District Works is switched off in the mod settings";
                    else if (v != null && !PathOf(v)) w.Paused = "paused - the district's new economy is still settling in, nothing is paid until it does";
                }
            }
            catch (Exception e) { Stumble("PlayerWorks", e); }
            return res;
        }

        /// <summary>Wyplaty prac rodu gracza w dobie day: razem i wedlug wioski (nazwa czesci z wsia gry). Komunikat dnia (FiefInvestments.Daily).</summary>
        internal static double PlayerPaidOn(int day, List<KeyValuePair<string, double>> per, out int works)
        {
            works = 0;
            if (per != null) per.Clear();
            double all = 0;
            try
            {
                string pid = PlayerClanId();
                if (pid == null) return 0;
                var got = new List<KeyValuePair<Row, double>>();
                lock (Gate)
                    foreach (var r in _rows)
                    {
                        if (r.Clan != pid) continue;
                        if (r.Held() > 1e-6) works++;
                        double x; if (r.PaidDays.TryGetValue(day, out x) && x > 0) got.Add(new KeyValuePair<Row, double>(r, x));
                    }
                var idx = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var kv in got)
                {
                    var st = Settlement.Find(kv.Key.Dist);
                    string n = st != null && st.Village != null ? PartName(st.Village, kv.Key.Part) : kv.Key.Part;
                    all += kv.Value;
                    if (per == null) continue;
                    int i;
                    if (idx.TryGetValue(n, out i)) per[i] = new KeyValuePair<string, double>(n, per[i].Value + kv.Value);
                    else { idx[n] = per.Count; per.Add(new KeyValuePair<string, double>(n, kv.Value)); }
                }
            }
            catch (Exception e) { Stumble("PlayerPaidOn", e); }
            return all;
        }
    }
}
