using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace CrashScribe
{
    /// <summary>
    /// AUDYT EKONOMII (Jeff 04.10: "AI powinno nie dostawac nic za darmo, ale
    /// najpierw audyt ekonomii - ile dziennie zarabia lord, krol itd., ile ma kasy").
    /// TYLKO ODCZYT - nic nie zmienia w grze. Raz po wczytaniu i raz na dobe:
    ///  - plik economy-RRRR-MM-DD_GG-MM-SS.csv (jeden na sesje, wiersz na rod na dzien):
    ///    zloto glowy rodu i calej rodziny, skarbiec krolestwa, fiefy, partie, wojsko,
    ///    zold dzienny, dochod/wydatki/bilans wg modelu finansow (BK, applyWithdrawals
    ///    = false, czyli bez wyplat - sprawdzone w BKClanFinanceModel), FAKTYCZNA
    ///    zmiana zlota rodziny od poprzedniego audytu (lupy, okupy, zakupy - wszystko,
    ///    czego model nie widzi) i po trzy najwieksze pozycje dochodu i wydatku;
    ///  - w logu sesji podsumowanie wedle pozycji (krol / rod krolestwa / rod mniejszy
    ///    / gracz): ile rodow, mediana i max zlota, mediana bilansu i faktycznej zmiany,
    ///    plus piec najbogatszych rodow.
    /// </summary>
    internal sealed class EconomyAudit : CampaignBehaviorBase
    {
        private static string _csv;
        private static bool _firstDone;
        // zloto rodziny przy poprzednim audycie (tylko ta sesja) - do faktycznej zmiany
        private static readonly Dictionary<Clan, KeyValuePair<double, long>> _prev = new Dictionary<Clan, KeyValuePair<double, long>>();

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, delegate { Run("dzien"); });
            // pierwszy raz od reki po wczytaniu - na pierwszej godzinie kampanii
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, delegate
            {
                if (_firstDone) return;
                _firstDone = true;
                Run("start");
            });
        }

        public override void SyncData(IDataStore dataStore) { }

        private sealed class Row
        {
            public Clan Clan; public string Cat; public long LeaderGold, FamilyGold;
            public int KingdomBudget, Towns, Castles, Villages, Parties, Troops, Wages;
            public int Income, Expense, Net; public double Delta = double.NaN;
            public string TopIn = "", TopOut = "";
        }

        private static void Run(string when)
        {
            try
            {
                if (Campaign.Current == null) return;
                if (_csv == null)
                {
                    _csv = Path.Combine(Scribe.ReportDir, "economy-" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".csv");
                    File.WriteAllText(_csv, "dzien;data;rod;krolestwo;kategoria;glowa;tier;zloto_glowy;zloto_rodziny;skarbiec_krolestwa;miasta;zamki;wioski;partie;wojsko;zold_dzienny;dochod_model;wydatki_model;bilans_model;faktyczna_zmiana_dzien;top_dochod;top_wydatki\r\n", Encoding.UTF8);
                }
                double today = CampaignTime.Now.ToDays;
                var model = Campaign.Current.Models.ClanFinanceModel;
                var rows = new List<Row>();
                foreach (var clan in Clan.All)
                {
                    try
                    {
                        if (clan == null || clan.IsEliminated || clan.IsBanditFaction || clan.Leader == null) continue;
                        var r = new Row { Clan = clan };
                        var k = clan.Kingdom;
                        if (clan == Clan.PlayerClan) r.Cat = "gracz";
                        else if (k != null && k.RulingClan == clan) r.Cat = "krol";
                        else if (k != null) r.Cat = "rod krolestwa";
                        else r.Cat = "rod mniejszy";
                        r.LeaderGold = clan.Leader.Gold;
                        foreach (var h in clan.Heroes) if (h != null && h.IsAlive) r.FamilyGold += h.Gold;
                        if (k != null && k.RulingClan == clan) { try { r.KingdomBudget = k.KingdomBudgetWallet; } catch { } }
                        foreach (var f in clan.Fiefs)
                        {
                            if (f == null) continue;
                            if (f.IsCastle) r.Castles++; else if (f.IsTown) r.Towns++;
                            try { r.Villages += f.Villages.Count; } catch { }
                        }
                        foreach (var w in clan.WarPartyComponents)
                        {
                            var mp = w != null ? w.MobileParty : null;
                            if (mp == null || !mp.IsActive) continue;
                            r.Parties++;
                            r.Troops += mp.MemberRoster.TotalManCount;
                            try { r.Wages += mp.TotalWage; } catch { }
                        }
                        var inc = model.CalculateClanIncome(clan, true, false, false);
                        var exp = model.CalculateClanExpenses(clan, true, false, false);
                        r.Income = inc.RoundedResultNumber;
                        r.Expense = exp.RoundedResultNumber;
                        r.Net = model.CalculateClanGoldChange(clan, false, false, false).RoundedResultNumber;
                        r.TopIn = Top(inc, true);
                        r.TopOut = Top(exp, false);
                        KeyValuePair<double, long> p;
                        if (_prev.TryGetValue(clan, out p) && today - p.Key >= 0.5)
                            r.Delta = (r.FamilyGold - p.Value) / (today - p.Key);
                        _prev[clan] = new KeyValuePair<double, long>(today, r.FamilyGold);
                        rows.Add(r);
                    }
                    catch (Exception e) { Scribe.Report("CrashScribe", e, "EconomyAudit.Clan " + (clan != null ? clan.StringId : "?"), null); }
                }

                var sb = new StringBuilder();
                string date = CampaignTime.Now.ToString();
                foreach (var r in rows)
                {
                    sb.Append(((int)today).ToString(CultureInfo.InvariantCulture)).Append(';').Append(Csv(date)).Append(';')
                      .Append(Csv(r.Clan.Name.ToString())).Append(';').Append(Csv(r.Clan.Kingdom != null ? r.Clan.Kingdom.Name.ToString() : "")).Append(';')
                      .Append(r.Cat).Append(';').Append(Csv(r.Clan.Leader.Name.ToString())).Append(';').Append(r.Clan.Tier).Append(';')
                      .Append(r.LeaderGold).Append(';').Append(r.FamilyGold).Append(';').Append(r.KingdomBudget).Append(';')
                      .Append(r.Towns).Append(';').Append(r.Castles).Append(';').Append(r.Villages).Append(';')
                      .Append(r.Parties).Append(';').Append(r.Troops).Append(';').Append(r.Wages).Append(';')
                      .Append(r.Income).Append(';').Append(r.Expense).Append(';').Append(r.Net).Append(';')
                      .Append(double.IsNaN(r.Delta) ? "" : Math.Round(r.Delta).ToString(CultureInfo.InvariantCulture)).Append(';')
                      .Append(Csv(r.TopIn)).Append(';').Append(Csv(r.TopOut)).Append("\r\n");
                }
                File.AppendAllText(_csv, sb.ToString(), Encoding.UTF8);

                // podsumowanie w logu sesji
                var line = new StringBuilder("EKONOMIA (" + when + ", " + date + ", rodow " + rows.Count + ", plik " + Path.GetFileName(_csv) + "):");
                foreach (var cat in new[] { "krol", "rod krolestwa", "rod mniejszy", "gracz" })
                {
                    var gold = new List<double>(); var net = new List<double>(); var delta = new List<double>(); var wages = new List<double>();
                    foreach (var r in rows)
                    {
                        if (r.Cat != cat) continue;
                        gold.Add(r.FamilyGold); net.Add(r.Net); wages.Add(r.Wages);
                        if (!double.IsNaN(r.Delta)) delta.Add(r.Delta);
                    }
                    if (gold.Count == 0) continue;
                    line.Append(" | ").Append(cat.ToUpperInvariant()).Append(" x").Append(gold.Count)
                        .Append(": zloto rodziny mediana ").Append(Fmt(Median(gold))).Append(", max ").Append(Fmt(Max(gold)))
                        .Append("; bilans/dzien wg modelu mediana ").Append(Fmt(Median(net)))
                        .Append("; zold/dzien mediana ").Append(Fmt(Median(wages)))
                        .Append(delta.Count > 0 ? "; faktycznie/dzien mediana " + Fmt(Median(delta)) : "");
                }
                rows.Sort((a, b) => b.FamilyGold.CompareTo(a.FamilyGold));
                line.Append(" | NAJBOGATSI: ");
                for (int i = 0; i < Math.Min(5, rows.Count); i++)
                    line.Append(i > 0 ? ", " : "").Append(rows[i].Clan.Name).Append(" (").Append(rows[i].Cat).Append(") ")
                        .Append(Fmt(rows[i].FamilyGold)).Append(", bilans ").Append(Fmt(rows[i].Net)).Append("/d");
                Scribe.Line(line.ToString());
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "EconomyAudit.Run", null); } catch { } }
        }

        private static string Top(ExplainedNumber n, bool positive)
        {
            try
            {
                var lines = n.GetLines();
                var pick = new List<KeyValuePair<string, float>>();
                foreach (var l in lines)
                {
                    if (positive ? l.number > 0f : l.number < 0f) pick.Add(new KeyValuePair<string, float>(l.name, l.number));
                }
                pick.Sort((a, b) => Math.Abs(b.Value).CompareTo(Math.Abs(a.Value)));
                var parts = new List<string>();
                for (int i = 0; i < Math.Min(3, pick.Count); i++) parts.Add(pick[i].Key + " " + Math.Round(pick[i].Value).ToString(CultureInfo.InvariantCulture));
                return string.Join(", ", parts.ToArray());
            }
            catch { return ""; }
        }

        private static string Csv(string s) { return (s ?? "").Replace(';', ',').Replace('\r', ' ').Replace('\n', ' '); }
        private static string Fmt(double v) { return Math.Round(v).ToString("#,0", CultureInfo.InvariantCulture).Replace(',', ' '); }
        private static double Max(List<double> v) { double m = double.MinValue; foreach (var x in v) if (x > m) m = x; return m; }
        private static double Median(List<double> v)
        {
            var c = new List<double>(v); c.Sort();
            int n = c.Count; if (n == 0) return 0;
            return n % 2 == 1 ? c[n / 2] : (c[n / 2 - 1] + c[n / 2]) / 2.0;
        }
    }
}
