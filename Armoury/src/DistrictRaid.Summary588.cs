using System;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using Armoury.Villages;

namespace Armoury
{
    /// <summary>
    /// 588 (Jeff 16.11: "ile spladrowalem, jaka to jest czesc ich kasy, ile ma wioska?") - PODSUMOWANIE RABUNKU GRACZA. Sesja E7/R1/R2, w ktorej bierze udzial druzyna
    /// gracza (wodz - sam albo jako wodz armii; albo partia w cudzej armii - wtedy "the army took", gracz bez udzialu w zlocie - zloto idzie do wodza jak w grze, w taborze
    /// gracza tylko sztuki, ktore weszly do jego taboru). Przy koncu sesji (EndFin: spalenie, "End the raid" z okienka, "End Raiding", przerwanie walka/odsiecza/pokojem)
    /// JEDEN krotki komunikat po angielsku (liczby na poczatku: ile zlota z ilu w kiesie wsi i jaki procent, towar, osady, ludzie) i linia logu po polsku ze wszystkimi
    /// polami. Okienko kroku E7/R2 dopisuje kiese wsi teraz i wziete dotad. Tylko odczyt sum zbieranych tam, gdzie i tak liczymy lup (Loot, StrikeChunk, Eat) - ekonomia
    /// bez zmian. Kiesa "na starcie" = kiesa wsi przy pierwszym ticku sesji (Decide); sumy bez zapisu - po wczytaniu od wczytania (komunikat mowi "since loading").
    /// Klucz PlayerRaidSummary. Wioski poboczne (SideVillageVisit.FinishRaid) - ten sam ksztalt komunikatu (SummaryText).
    /// </summary>
    internal static partial class DistrictRaid
    {
        internal static bool SummaryOn { get { var s = Settings.Current; return s != null && s.PlayerRaidSummary; } }

        /// <summary>588: druzyna gracza jest po stronie napastnika (cudza armia albo dolaczenie do rabunku).</summary>
        private static bool PlayerInSide(RaidEventComponent comp)
        {
            var mp = MobileParty.MainParty;
            var att = comp != null ? comp.AttackerSide : null;
            if (mp == null || att == null) return false;
            foreach (var mep in att.Parties)
                if (mep != null && mep.Party != null && ReferenceEquals(mep.Party.MobileParty, mp)) return true;
            return false;
        }

        /// <summary>588: poczatek sesji (Decide) - kiesa wsi i zegar. Po wczytaniu (Restored) - od wczytania.</summary>
        private static void SumBegin(Sess ss)
        {
            try
            {
                ss.SessH0 = CampaignTime.Now.ToHours;
                if (ss.V != null) ss.Purse0 = Math.Max(0, ss.V.Gold);
            }
            catch (Exception e) { Stumble("SumBegin588", e); }
        }

        private static string N0(double x) { return Math.Round(Math.Max(0.0, x)).ToString("N0", Inv); }

        /// <summary>588: tekst komunikatu (wspolny z wioskami pobocznymi). purse0 &lt; 0 - nieznana. Krotko, liczby na poczatku, separatory tysiecy.</summary>
        internal static string SummaryText(string name, bool army, bool broken, long gold, double purse0, long port, long goods, long mine,
                                           int burned, int n, double dead, double fled, double refugees, long captives, bool sinceLoad)
        {
            var sb = new StringBuilder(160);
            sb.Append(name ?? "?");
            if (broken) sb.Append(" (raid broken off)");
            sb.Append(": ").Append(army ? "the army took " : "took ").Append(gold.ToString("N0", Inv));
            if (purse0 >= 1.0) sb.Append(" of ").Append(N0(purse0)).Append(" denars (").Append(Math.Round(100.0 * gold / purse0).ToString("0", Inv)).Append("%)");
            else sb.Append(" denars");
            if (port > 0) sb.Append(" +").Append(port.ToString("N0", Inv)).Append(" port");
            sb.Append(", ").Append(goods.ToString("N0", Inv)).Append(" goods");
            if (army && mine > 0) sb.Append(" (").Append(mine.ToString("N0", Inv)).Append(" yours)");
            sb.Append("; ").Append(burned.ToString("N0", Inv)).Append(" of ").Append(n.ToString("N0", Inv)).Append(" settlements burned; ");
            sb.Append(N0(dead)).Append(" killed, ").Append(N0(fled)).Append(" fled, ").Append(N0(refugees)).Append(" refugees");
            if (captives > 0) sb.Append(", ").Append(captives.ToString("N0", Inv)).Append(" captives");
            sb.Append('.');
            if (sinceLoad) sb.Append(" (since loading)");
            return sb.ToString();
        }

        /// <summary>588: linia okienka kroku E7/R2 - kiesa wsi teraz i wziete dotad w tej sesji. null - klucz zdjety.</summary>
        private static string PurseLine(Sess ss)
        {
            if (!SummaryOn || ss == null || ss.V == null) return null;
            long now = Math.Max(0, ss.V.Gold);
            double p0 = double.IsNaN(ss.Purse0) ? now + ss.TGold : ss.Purse0;
            string pct = p0 >= 1.0 ? " (" + Math.Round(100.0 * ss.TGold / p0).ToString("0", Inv) + "%)" : "";
            return "Village purse: " + now.ToString("N0", Inv) + " denars left of " + N0(p0) + "; you took " + ss.TGold.ToString("N0", Inv) + pct + " so far"
                   + (ss.TPort > 0 ? ", +" + ss.TPort.ToString("N0", Inv) + " from the port" : "") + (ss.Restored ? " (since loading)" : "") + ".";
        }

        /// <summary>588: koniec sesji z udzialem gracza (EndFin) - komunikat i linia logu. captives - jency E4/9 tej sesji (roznica CapTakenAll wokol konca).</summary>
        private static void Summary(Sess ss, RaidEventComponent comp, long captives)
        {
            var v = ss.V;
            var st = v != null ? v.Settlement : null;
            if (st == null) return;
            string name = st.Name != null ? st.Name.ToString() : st.StringId;
            long purse1 = Math.Max(0, v.Gold);
            double p0 = double.IsNaN(ss.Purse0) ? purse1 + ss.TGold : ss.Purse0;
            double settl = ss.Player ? ss.Settl : (ss.Done ? ss.M : 0.0);
            int burned = (int)Math.Round(Math.Max(0.0, settl));
            string vname, dname; int bookBurned, n; bool sb0, wb0;
            if (!VillageBook.LootNews(v, comp, out vname, out dname, out bookBurned, out n, out sb0, out wb0))
                n = Math.Max(1, (int)Math.Round(ss.Sv > 0.0 ? ss.L0 / ss.Sv : 1.0));
            n = Math.Max(n, burned);
            bool army = !ss.Player;
            double days = double.IsNaN(ss.SessH0) ? double.NaN : Math.Max(0.0, CampaignTime.Now.ToHours - ss.SessH0) / 24.0;
            string leader = "?";
            try { var lp = comp.AttackerSide != null ? comp.AttackerSide.LeaderParty : null; if (lp != null && lp.Name != null) leader = lp.Name.ToString(); } catch { }
            string why = ss.Done ? "spalone (koniec pelnego kroku / okienko / End Raiding)" : ss.Lost ? "przerwany walka (napastnik przegral albo bez ludzi)" : "przerwany (wodz odszedl, odsiecz, pokoj albo koniec zdarzenia)";
            Log.Info("Rabunek gracza (588): " + name + " (" + st.StringId + ") - " + why + " | " + (ss.Player ? "wodz gracz" : "gracz w armii " + leader)
                     + " | kiesa wsi: start " + N0(p0) + ", wziete " + ss.TGold + " (" + (p0 >= 1.0 ? F(100.0 * ss.TGold / p0, "0.0") : "-") + "%), po " + purse1
                     + (ss.TPort > 0 ? ", nabrzeze portu " + ss.TPort : "")
                     + " | towar: do taborow " + ss.TTake + " szt. (wartosc " + ss.TTakeVal + ", bez stada), spalone " + ss.TBurn + (ss.Player ? "" : ", do taboru gracza " + ss.TMine)
                     + " | spichlerz: zjedzone " + F(ss.TFoodEat, "0") + " racji, spalone " + F(ss.TFoodBurn, "0") + " racji"
                     + " | osady spalone " + F(settl, "0.##") + " z " + n + " (ksiega: spalone teraz " + bookBurned + "), krokow " + ss.Steps
                     + " | trafieni " + F(ss.Hit, "0.0") + ": zabici " + F(ss.Dead, "0.0") + ", las " + F(ss.Fled, "0.0") + ", uchodzcy " + F(ss.Ref, "0.0") + ", jency E4/9 " + captives
                     + " | noce postoju " + ss.Nights + ", doby zegara " + (double.IsNaN(days) ? "?" : F(days, "0.0")) + ", doby pracy " + F(ss.LootDays, "0.0")
                     + (ss.Restored ? " | sesja z zapisu - sumy od wczytania" : "") + (SummaryOn ? "" : " | komunikat wylaczony (Player Raid Summary)") + ".");
            if (!SummaryOn) return;
            // uchodzcy bez pojmanych (E4/9 bierze jencow z konta uchodzcow przy koncu wygranego rabunku - Devastation.Hit)
            Log.Player(SummaryText(name, army, !ss.Done, ss.TGold, p0, ss.TPort, ss.TTake, ss.TMine, burned, n, ss.Dead, ss.Fled, Math.Max(0.0, ss.Ref - captives), captives, ss.Restored));
        }
    }
}
