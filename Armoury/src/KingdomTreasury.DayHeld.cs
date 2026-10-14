using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;

namespace Armoury
{
    /// <summary>
    /// A14 / G20 (audyt zapis 2b, drobne2): gotowka doplat W5 (z W6 tarczowym i W5b) i poboru W7 wczorajszego kroku po wczytaniu. Zwrot 70% (WageRefund, krok
    /// doby D+1) zdejmuje z podstawy zold oplacony ta gotowka (NeedAidRefundCut / LordLevyRefundCut przy _naDay / _lvDay == dzis - 1). Stan byl tylko w pamieci:
    /// po wczytaniu zapisu z doby D (po kroku D) _naDay = _lvDay = MinValue, wiec zwrot D+1 liczyl ten zold drugi raz (korona placila 70% zoldu juz oplaconego).
    /// Zapis (DayHeld, klucz arm_dayheld): "naDay|idRodu=gotowka;...|lvDay|idRodu=gotowka;..."; odtwarzane przy starcie sesji (klucz MidDayLoadKeepsDay) -
    /// doba kroku i gotowka. _naDay == dzis po odtworzeniu = krok W5 dzis juz byl (ten sam warunek, ktory pamiec mialaby bez wczytania). Zloto nie rusza.
    /// </summary>
    internal static partial class KingdomTreasury
    {
        private static string _rhRaw;

        internal static void RefundHeldReset() { _rhRaw = null; }

        private static readonly char[] RhBad = { '|', ';', '=' };

        private static void RhCash(StringBuilder sb, Dictionary<Clan, long> d)
        {
            bool first = true;
            foreach (var kv in d)
            {
                if (kv.Key == null || kv.Value <= 0 || kv.Key.StringId == null || kv.Key.StringId.IndexOfAny(RhBad) >= 0) continue;
                if (!first) sb.Append(';'); first = false;
                sb.Append(kv.Key.StringId).Append('=').Append(kv.Value.ToString(CultureInfo.InvariantCulture));
            }
        }

        /// <summary>"naDay|gotowka W5|lvDay|gotowka W7" (cztery pola).</summary>
        internal static string RefundHeldExport()
        {
            try
            {
                var sb = new StringBuilder(64 + (_naCash.Count + _lvCash.Count) * 24);
                sb.Append(_naDay.ToString(CultureInfo.InvariantCulture)).Append('|');
                RhCash(sb, _naCash);
                sb.Append('|').Append(_lvDay.ToString(CultureInfo.InvariantCulture)).Append('|');
                RhCash(sb, _lvCash);
                return sb.ToString();
            }
            catch (Exception e) { Log.Error("KingdomTreasury.RefundHeldExport", e); return ""; }
        }

        internal static void RefundHeldImport(string raw) { _rhRaw = raw; }

        private static int RhFill(string part, Dictionary<string, Clan> clans, Dictionary<Clan, long> into, bool apply, ref long sum, ref int bad)
        {
            int n = 0;
            if (string.IsNullOrEmpty(part)) return 0;
            foreach (var p in part.Split(';'))
            {
                var kv = p.Split('='); long v; Clan c;
                if (kv.Length != 2 || !long.TryParse(kv[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out v) || v <= 0 || !clans.TryGetValue(kv[0], out c) || c == null) { bad++; continue; }
                n++; sum += v;
                if (apply) into[c] = v;
            }
            return n;
        }

        /// <summary>Start sesji (DayHeld.SessionStart): przy `apply` odtwarza _naDay / _naCash i _lvDay / _lvCash z zapisu. Zwraca napis do linii.</summary>
        internal static string RefundHeldResolve(bool apply)
        {
            string raw = _rhRaw;
            _rhRaw = null;
            if (string.IsNullOrEmpty(raw)) return "zwrot 70%: brak gotowki W5/W7 w zapisie";
            try
            {
                var f = raw.Split('|');
                int naDay, lvDay;
                if (f.Length != 4 || !int.TryParse(f[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out naDay) || !int.TryParse(f[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out lvDay))
                    return "zwrot 70%: bledny format w zapisie - nic";
                var clans = new Dictionary<string, Clan>(StringComparer.Ordinal);
                foreach (var c in Clan.All) if (c != null && c.StringId != null && !clans.ContainsKey(c.StringId)) clans[c.StringId] = c;
                long naSum = 0, lvSum = 0; int bad = 0;
                if (apply) { _naCash.Clear(); _lvCash.Clear(); }
                int naN = RhFill(f[1], clans, _naCash, apply, ref naSum, ref bad);
                int lvN = RhFill(f[3], clans, _lvCash, apply, ref lvSum, ref bad);
                if (apply) { _naDay = naDay; _lvDay = lvDay; }
                int today = (int)CampaignTime.Now.ToDays;
                return "zwrot 70%: gotowka W5 z doby " + (naDay == int.MinValue ? "-" : naDay.ToString(CultureInfo.InvariantCulture)) + " u " + naN + " rodow (" + naSum + " zl), W7 z doby "
                       + (lvDay == int.MinValue ? "-" : lvDay.ToString(CultureInfo.InvariantCulture)) + " u " + lvN + " rodow (" + lvSum + " zl), bledne " + bad
                       + (apply ? " - odtworzone (zwrot doby " + (today + 1) + " zdejmie ja z podstawy, gdy krok byl dzis)" : " - NIE odtworzone");
            }
            catch (Exception e) { Log.Error("KingdomTreasury.RefundHeldResolve", e); return "zwrot 70%: blad odczytu - nic"; }
        }
    }
}
