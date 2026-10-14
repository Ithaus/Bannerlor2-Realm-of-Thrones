using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;

namespace Armoury
{
    /// <summary>
    /// A14 / G20 (audyt zapis 2a, drobne2): punkty budow tej doby po wczytaniu zapisu ze srodka doby. Blok dobowy (BuildFunding.Daily, FolkWorks) placi za
    /// material i robocizne i wpisuje punkty do _funded (pan) i _folk / _folkB (lud, czolo kolejki); gra czyta je w PowerPostfix przy ticku osady, a osady tykaja
    /// rozlozone przez cala dobe (CampaignPeriodicEventManager._dailyTickSettlementTicker). Slowniki byly tylko w pamieci - po wczytaniu w godzinie h osady bez
    /// ticku tej doby dostawaly 0 punktow, choc zaplacone. Zapis (DayHeld, klucz arm_dayheld): "idOsady=pktPana:pktLudu:idTypuBudowliLudu;..." z doby zapisu;
    /// odtwarzane przy starcie sesji tylko, gdy doba zapisu == dzis (klucz MidDayLoadKeepsDay). Budowla ludu po typie w osadzie (FolkHead dalej sprawdza, ze jest
    /// czolem kolejki). Zloto nie rusza (zaplacone w bloku przed zapisem).
    /// </summary>
    internal static partial class BuildFunding
    {
        private static readonly CultureInfo HeldInv = CultureInfo.InvariantCulture;
        private static string _heldRaw;

        internal static void HeldReset() { _heldRaw = null; }

        /// <summary>Stan punktow tej doby do zapisu (pusty napis - nic). Id ze znakami separatorow pomijane.</summary>
        internal static string HeldExport()
        {
            try
            {
                var towns = new HashSet<Town>();
                foreach (var kv in _funded) if (kv.Key != null && kv.Value > 0f) towns.Add(kv.Key);
                foreach (var kv in _folk) if (kv.Key != null && kv.Value > 0f) towns.Add(kv.Key);
                var sb = new StringBuilder(towns.Count * 32);
                foreach (var t in towns)
                {
                    string id = t.Settlement != null ? t.Settlement.StringId : null;
                    if (string.IsNullOrEmpty(id) || id.IndexOfAny(HeldBad) >= 0) continue;
                    float f, fl; Building b;
                    _funded.TryGetValue(t, out f); _folk.TryGetValue(t, out fl); _folkB.TryGetValue(t, out b);
                    string bt = b != null && b.BuildingType != null && b.BuildingType.StringId != null && b.BuildingType.StringId.IndexOfAny(HeldBad) < 0 ? b.BuildingType.StringId : "-";
                    if (sb.Length > 0) sb.Append(';');
                    sb.Append(id).Append('=').Append(f.ToString("R", HeldInv)).Append(':').Append(fl.ToString("R", HeldInv)).Append(':').Append(bt);
                }
                return sb.ToString();
            }
            catch (Exception e) { Log.Error("BuildFunding.HeldExport", e); return ""; }
        }
        private static readonly char[] HeldBad = { '|', ';', '=', ':' };

        internal static void HeldImport(string raw) { _heldRaw = raw; }

        /// <summary>Start sesji (DayHeld.SessionStart): przy `apply` odtwarza punkty z zapisu do _funded / _folk / _folkB. Zwraca napis do linii.</summary>
        internal static string HeldResolve(bool apply)
        {
            string raw = _heldRaw;
            _heldRaw = null;
            if (string.IsNullOrEmpty(raw)) return "budowy: brak punktow w zapisie";
            int towns = 0, folkN = 0, noB = 0, bad = 0; double lord = 0, folk = 0;
            try
            {
                foreach (var p in raw.Split(';'))
                {
                    try
                    {
                        var kv = p.Split('=');
                        if (kv.Length != 2) { bad++; continue; }
                        var x = kv[1].Split(':');
                        float f, fl;
                        if (x.Length != 3 || !float.TryParse(x[0], NumberStyles.Float, HeldInv, out f) || !float.TryParse(x[1], NumberStyles.Float, HeldInv, out fl)) { bad++; continue; }
                        var st = Settlement.Find(kv[0]);
                        var t = st != null ? st.Town : null;
                        if (t == null) { bad++; continue; }
                        towns++; lord += Math.Max(0f, f);
                        Building b = null;
                        if (fl > 0f && x[2] != "-")
                        {
                            foreach (var bb in t.Buildings) if (bb != null && bb.BuildingType != null && bb.BuildingType.StringId == x[2]) { b = bb; break; }
                            if (b == null) noB++;
                        }
                        if (b != null) { folkN++; folk += fl; }
                        if (!apply) continue;
                        if (f > 0f) _funded[t] = f;
                        if (b != null) { _folk[t] = fl; _folkB[t] = b; }
                    }
                    catch { bad++; }
                }
            }
            catch (Exception e) { Log.Error("BuildFunding.HeldResolve", e); }
            return "budowy: osad " + towns + " (pkt pana " + lord.ToString("0", HeldInv) + ", ludu " + folk.ToString("0", HeldInv) + " w " + folkN + " osadach; budowla ludu nieznaleziona "
                   + noB + ", bledne wpisy " + bad + ")" + (apply ? " - odtworzone" : " - NIE odtworzone");
        }
    }
}
