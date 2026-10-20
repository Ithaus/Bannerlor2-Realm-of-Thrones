using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// m96-dlugi (Jeff 20.10 "dlugi miast tak, robimy") - dostep rady miasta (TownDebt.cs) do zaleglosci firmy DS z kasy. Dwie funkcje, bez zmiany regul DS:
    ///  - TdArrears: odczyt zaleglosci pana (AL) i korony (AC) z kasy oraz raty tygodniowej z kasy ((FL + FC) / 52); czesc z oszczednosci ludu (ALf) placi lud, nie kasa - poza;
    ///  - TdPayArrears: splata zaleglosci z kasy pieniedzmi, ktore rada przed chwila do tej kasy wplacila (pozyczka, podatek nadzwyczajny) - ta sama droga przelewu co rata
    ///    (ToLord: GiveGoldAction kasa -> pan; ToCrown: kasa -> skarbiec), w stosunku AL : AC, bez podlogi kasy (pieniadz przyszedl na ten cel). Pan: RentToday (D "ziemia",
    ///    jak rata - PopulationLaw.ValveTowns), korona: TownPurse.DsCrownFolk (wplyw dnia, CrownIncome.Begin czyta pozniej tej doby). Licznik linii DS "zaleglosci: splacone"
    ///    i kontrola "platnicy - odbiorcy" (ToLord / ToCrown licza obie strony). Nie rzuca.
    /// </summary>
    internal static partial class DsTowns
    {
        /// <summary>cover - ile tej zaleglosci kasa sama zaplaci w najblizszy dzien raty (kasa ponad podloge firm, FloorOf) - na to rada nie pozycza.</summary>
        internal static bool TdArrears(Settlement st, out double al, out double ac, out double weekly, out double cover)
        {
            al = ac = weekly = cover = 0;
            try
            {
                if (st == null || !FarmsLive) return false;
                var f = FarmOf(st, false);
                if (f == null || f.Fixed < 0) return false;
                al = Math.Max(0, f.AL); ac = Math.Max(0, f.AC);
                weekly = Math.Max(0, (f.FL + f.FC) / Weeks * NewShare);
                var s = Settings.Current;
                if (s != null && st.Town != null) cover = Math.Max(0, st.Town.Gold - FloorOf(s, TownPurse.Reserve(st.Town)));
                return true;
            }
            catch (Exception e) { Stumble("TdArrears", e); return false; }
        }

        internal static long TdPayArrears(Settlement st, long budget, out long toLord, out long toCrown)
        {
            toLord = toCrown = 0;
            try
            {
                if (budget <= 0 || st == null || st.Town == null || !FarmsLive) return 0;
                var f = FarmOf(st, false);
                if (f == null) return 0;
                var t = st.Town;
                var clan = st.OwnerClan;
                var lord = clan != null ? clan.Leader : null;
                if (lord == null || !lord.IsAlive) return 0;                      // bez zywego pana zaleglosc czeka (jak rata)
                var k = clan.Kingdom;
                if (k != null && k.IsEliminated) k = null;
                if (k == null && f.AC > 0) { f.AL += f.AC; f.AC = 0; }            // rod bez krolestwa - calosc panu (jak Pay)
                double tot = f.AL + f.AC;
                if (tot < 1) return 0;
                long pay = (long)Math.Floor(Math.Min(tot, Math.Min((double)budget, (double)Math.Max(0, t.Gold))));
                if (pay <= 0) return 0;
                long pL = (long)Math.Floor(pay * f.AL / tot), pC = pay - pL;
                long gL = pL > 0 ? ToLord(st, t, lord, pL) : 0;
                f.AL = Math.Max(0, f.AL - gL); toLord = gL;
                long gC = pC > 0 && k != null ? ToCrown(t, k, pC) : 0;
                f.AC = Math.Max(0, f.AC - gC); toCrown = gC;
                if (gL > 0) { int r0; PopulationLaw.RentToday.TryGetValue(clan, out r0); PopulationLaw.RentToday[clan] = (int)Math.Min(int.MaxValue, (long)r0 + gL); }
                if (gC > 0) TownPurse.DsCrownFolk(k, gC);
                _arrPaid += gL + gC;
                return gL + gC;
            }
            catch (Exception e) { Stumble("TdPayArrears", e); return toLord + toCrown; }
        }
    }
}
