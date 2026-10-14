using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// L3 - W LUPIONYM MIESCIE WOJSKO NIE PLACI (projekt lupu miast rozdz. 3.2 "Dlaczego 3 doby", decyzja Jeffa 13.10 pkt 3). Klucz SackNoSpending, doby SackNoSpendingDays (3).
    ///  - Przez SackNoSpendingDays dob od grabiezy albo spustoszenia druzyny krolestwa zdobywcy (MapFaction w chwili lupu) nie zostawiaja w tej osadzie zoldu:
    ///    przy wyjezdzie z miasta "zycie" (MenPurse.OnLeft) nie schodzi z sakiewki, markietani (Sutlers.One) nie kieruja do niej pieniadza - pieniadz zostaje
    ///    w sakiewce i schodzi pozniej wedlug zwyklych regul (inne miasto, markietani po oknie). Przy lasce wojsko placi jak zwykle. Inne krolestwa - jak zwykle.
    ///    Zamek: "zycie" i markietani i tak nie ida do zamkow (MenPurse.cs:279, Sutlers - tylko miasta).
    ///  - Jedno pole zapisu (SaveText.Sync "arm_sack"): osady po nastepstwach z ostatnich 30 dob (doba, krolestwo zdobywcy, wybor, ile zabrano, liczniki pomiaru L0) -
    ///    okno lupu, "Little is left here" i linia w oknie mieszkancow (L5) przezywaja zapis. Brak klucza (zapis sprzed paczki) - pusto.
    /// Zloto nie powstaje i nie znika: wstrzymany wydatek zostaje w sakiewce (MenPurse - posiadacz "Pieniadz swiata"). Wyjatek - wydatek jak dotad (nic nie blokuje).
    /// </summary>
    internal static partial class CitySack
    {
        internal static bool NoSpendOn { get { var s = Settings.Current; return s != null && s.SackNoSpending && MenPurse.On; } }

        private static int _dBlockLifeN, _dBlockCampN;
        private static long _dBlockLife, _dBlockCamp;

        /// <summary>L3: true = w tej osadzie wojsko krolestwa zdobywcy nie placi (okno lupu) - wolajacy nie zdejmuje kwoty. Nie rzuca (wyjatek = false, wydatek jak dotad).</summary>
        internal static bool NoSpend(Settlement st, MobileParty mp, long amount, bool sutler = false)
        {
            try
            {
                if (!NoSpendOn || st == null || mp == null || amount <= 0) return false;
                Ensure();
                if (_track.Count == 0) return false;
                Track t;
                if (!_track.TryGetValue(st, out t) || t == null || t.Kind == KMercy) return false;
                if (Now - t.Day0 >= WindowDays) return false;
                if (mp.MapFaction == null || mp.MapFaction.StringId != t.Faction) return false;
                if (sutler) { _dBlockCampN++; _dBlockCamp += amount; }
                else
                {
                    _dBlockLifeN++; _dBlockLife += amount;
                    if (mp.IsMainParty) Log.Player("Your men are still gorged on the sack of " + st.Name + " - they spend nothing here.");
                }
                return true;
            }
            catch (Exception e) { Stumble("NoSpend", e); return false; }
        }

        private static void L3Part(StringBuilder sb)
        {
            int open = 0;
            foreach (var kv in _track) if (kv.Value != null && kv.Value.Kind != KMercy && Now - kv.Value.Day0 < WindowDays) open++;
            sb.Append(" | L3: okna lupu czynne ").Append(open).Append(" (").Append(WindowDays).Append(" dob)");
            if (!NoSpendOn) sb.Append(" [WYLACZONE Sack No Spending / Men Purse Enabled]");
            sb.Append(", wstrzymane (zostaje w sakiewkach): zycie ").Append(_dBlockLifeN).Append(" wyjazdow za ").Append(_dBlockLife)
              .Append(", markietani ").Append(_dBlockCampN).Append(" druzyn za ").Append(_dBlockCamp);
        }

        private static void L3Clear() { _dBlockLifeN = _dBlockCampN = 0; _dBlockLife = _dBlockCamp = 0; }

        private static bool L3Any { get { return _dBlockLifeN > 0 || _dBlockCampN > 0; } }

        // ------------------------------------------------------------ zapis (arm_sack) - wersja 1
        private static string _pending;                       // zapis wczytany - osady rozwiazywane przy pierwszym uzyciu (Ensure)

        /// <summary>Wpisy z zapisu rozwiazywane przy pierwszym uzyciu (po wczytaniu swiata) - wolane na poczatku kazdego wejscia, ktore czyta _track.</summary>
        private static void Ensure()
        {
            if (_pending == null) return;
            string s = _pending;
            _pending = null;
            try { Parse(s); } catch (Exception e) { Stumble("Ensure", e); }
        }

        internal static string Export()
        {
            if (_pending != null) return _pending;                   // nic jeszcze nie czytalo - zapis bez zmian
            var sb = new StringBuilder(64 + 120 * _track.Count);
            sb.Append('1');
            foreach (var kv in _track)
            {
                var t = kv.Value;
                if (t == null || kv.Key == null || kv.Key.StringId == null) continue;
                sb.Append(';').Append(kv.Key.StringId).Append('|').Append(t.Day0.ToString("R", Inv)).Append('|').Append(t.Faction ?? "").Append('|').Append(t.Kind)
                  .Append('|').Append(t.Captured ? 1 : 0).Append('|').Append(t.Kasa0).Append('|').Append(t.Folk0).Append('|').Append(t.Merch).Append('|').Append(t.FolkTaken)
                  .Append('|').Append(t.InWinSacker).Append('|').Append(t.InWinOther).Append('|').Append(t.AfterSacker).Append('|').Append(t.AfterOther).Append('|').Append(t.Pris);
            }
            return sb.ToString();
        }

        internal static void Import(string s)
        {
            _track.Clear();
            _pending = string.IsNullOrEmpty(s) ? null : s;
        }

        private static void Parse(string s)
        {
            int bad = 0;
            var parts = s.Split(';');
            if (parts.Length == 0 || parts[0] != "1") { Log.Info("Lup miast: zapis arm_sack w nieznanej wersji - pominiety."); return; }
            for (int i = 1; i < parts.Length; i++)
            {
                try
                {
                    var a = parts[i].Split('|');
                    if (a.Length < 14) { bad++; continue; }
                    var st = Settlement.Find(a[0]);
                    if (st == null) { bad++; continue; }
                    var t = new Track { St = st, Day0 = float.Parse(a[1], NumberStyles.Float, Inv), Faction = a[2], Kind = int.Parse(a[3], Inv), Captured = a[4] == "1",
                                        Kasa0 = long.Parse(a[5], Inv), Folk0 = long.Parse(a[6], Inv), Merch = long.Parse(a[7], Inv), FolkTaken = long.Parse(a[8], Inv),
                                        InWinSacker = long.Parse(a[9], Inv), InWinOther = long.Parse(a[10], Inv), AfterSacker = long.Parse(a[11], Inv), AfterOther = long.Parse(a[12], Inv),
                                        Pris = long.Parse(a[13], Inv) };
                    if (t.Kind < 0 || t.Kind > 2) { bad++; continue; }
                    _track[st] = t;
                }
                catch { bad++; }
            }
            if (bad > 0) Log.Info("Lup miast: zapis arm_sack - pominieto " + bad + " wpisow (osada nieznana albo uszkodzony wpis).");
        }
    }
}
