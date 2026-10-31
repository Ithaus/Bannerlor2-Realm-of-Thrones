using System;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements.Workshops;

namespace Armoury
{
    /// <summary>
    /// 306b - odczyty dla sond autotestu (Probes306; tylko DLL autotestu i pliki armoury-karawana305.flag / armoury-warsztat305.flag). Nic nie zmieniaja:
    /// podglad wydatkow rodu gracza z opisem (jak samotest 305a, ale wszystkie linie) i kapital wlasny warsztatu z ksiegi 259 (WsRec.Basis).
    /// </summary>
    internal static partial class CaravanSettle
    {
        /// <summary>Podglad CalculateClanExpenses(gracz, includeDescriptions = true, apply = false, detale = false) bez rozbicia 305a (_suppress305) i z nim:
        /// sumy, wszystkie linie z rozbiciem, 'Caravan wages' i 'Garrison and Party Expense'. ok = sumy rowne, Garrison po + wages = Garrison przed i wages &lt; 0,
        /// gdy gracz ma karawane. skip = klucz 305a NIE, brak latek albo brak karawan gracza (nic do sprawdzenia).</summary>
        internal static string ExpProbe306(int caravans, out bool ok, out bool skip)
        {
            ok = false; skip = true;
            var s = S;
            var pc = Clan.PlayerClan;
            if (s == null || pc == null || !Live) return "brak kampanii";
            var fm = Campaign.Current.Models.ClanFinanceModel;
            bool q = _suppress305;
            TaleWorlds.CampaignSystem.ExplainedNumber a;
            try { _suppress305 = true; a = fm.CalculateClanExpenses(pc, true, false, false); }
            finally { _suppress305 = q; }
            var b = fm.CalculateClanExpenses(pc, true, false, false);
            string gName = TxtOf(ref _tGarrison, "{=ChUDSiJw}Garrison and Party Expense"), wName = TxtOf(ref _tCarWage, "{=arm305cw}Caravan wages");
            float ga = 0, gb = 0, wb = 0;
            var lines = new StringBuilder(400);
            foreach (var l in a.GetLines()) if (l.name == gName) ga += l.number;
            foreach (var l in b.GetLines())
            {
                if (l.name == gName) gb += l.number; else if (l.name == wName) wb += l.number;
                if (lines.Length > 0) lines.Append("; ");
                lines.Append('\'').Append(l.name).Append("' ").Append(Math.Round(l.number).ToString(Inv));
            }
            float diff = b.ResultNumber - a.ResultNumber;
            bool on = s.CaravanWagesSeparateLine && _w305Exp && _w305Cpw && _bkRefund;
            skip = !on || caravans <= 0;
            ok = !skip && Math.Abs(diff) < 0.5f && Math.Abs((gb + wb) - ga) < 0.5f && wb < -0.5f;
            return "podglad z opisem: suma bez rozbicia " + Math.Round(a.ResultNumber).ToString(Inv) + ", z rozbiciem " + Math.Round(b.ResultNumber).ToString(Inv)
                   + " (roznica " + Math.Round(diff, 1).ToString(Inv) + ") | 'Caravan wages' " + Math.Round(wb).ToString(Inv) + ", 'Garrison and Party Expense' " + Math.Round(gb).ToString(Inv)
                   + " (bez rozbicia " + Math.Round(ga).ToString(Inv) + ") | klucz CaravanWagesSeparateLine " + Tn(s.CaravanWagesSeparateLine) + ", latki 305a " + ((_w305Exp ? 1 : 0) + (_w305Cpw ? 1 : 0))
                   + "/2, BK PartyExpensesPrefix " + Tn(_bkRefund) + " | linie (z rozbiciem): " + (lines.Length > 0 ? lines.ToString() : "-");
        }

        /// <summary>Kapital wlasny gracza w warsztacie z ksiegi 259 (WsRec.Basis): -1 = rekord bez kapitalu wlasnego (Basis &lt; 0), -2 = brak rekordu.</summary>
        internal static long WsBasis306(Workshop w)
        {
            try
            {
                lock (_lk)
                {
                    var r = Ws(w, false);
                    if (r == null) return -2;
                    return r.Basis < 0 ? -1 : r.Basis;
                }
            }
            catch { return -2; }
        }
    }
}
