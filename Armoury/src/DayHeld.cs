using System;
using System.Globalization;
using TaleWorlds.CampaignSystem;

namespace Armoury
{
    /// <summary>
    /// A14 / G20 (audyt integracji 14.10, zapis pkt 2; drobne2) - DOBA PO WCZYTANIU ZE SRODKA DOBY. Klucz MidDayLoadKeepsDay (wylaczony = stan 449d453: zapis
    /// powstaje, ale nic nie jest odtwarzane). Klucz zapisu "arm_dayheld" (SaveText): "1|doba zapisu|budowy|naDay|gotowka W5|lvDay|gotowka W7"
    /// (BuildFunding.HeldExport, KingdomTreasury.RefundHeldExport). Brak klucza (stary zapis) - jak dotad.
    ///  (a) budowy: punkty pana i ludu tej doby - odtwarzane tylko, gdy doba zapisu == dzis (BuildFunding.Held.cs);
    ///  (b) zwrot 70%: gotowka W5/W7 wczorajszego kroku i jego doba (KingdomTreasury.DayHeld.cs).
    /// Nie odtwarzane (przyjete, CHANGELOG): robocizna 11c od skupu (TownFolk SkupWin - zostaje w kasie kupcow), kara P1b reszty doby (FolkAidLoyalty._snap).
    /// Linia "Doba po wczytaniu (G20)" przy kazdym starcie sesji z kluczem w zapisie. Stan tylko w pamieci miedzy SyncData i OnSessionLaunched.
    /// </summary>
    internal static class DayHeld
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static int _savedDay = int.MinValue;
        private static bool _had;

        internal static bool On { get { var s = Settings.Current; return s != null && s.MidDayLoadKeepsDay; } }

        internal static void Reset() { _savedDay = int.MinValue; _had = false; BuildFunding.HeldReset(); KingdomTreasury.RefundHeldReset(); }

        internal static string Export()
        {
            try
            {
                int day = Campaign.Current != null ? (int)CampaignTime.Now.ToDays : int.MinValue;
                return "1|" + day.ToString(Inv) + "|" + BuildFunding.HeldExport() + "|" + KingdomTreasury.RefundHeldExport();
            }
            catch (Exception e) { Log.Error("DayHeld.Export", e); return ""; }
        }

        internal static void Import(string v)
        {
            _savedDay = int.MinValue; _had = false;
            BuildFunding.HeldImport(null); KingdomTreasury.RefundHeldImport(null);
            if (string.IsNullOrEmpty(v)) return;
            try
            {
                var f = v.Split('|');
                if (f.Length != 7 || f[0] != "1" || !int.TryParse(f[1], NumberStyles.Integer, Inv, out _savedDay)) { Log.Info("Doba po wczytaniu (G20): zapis w nieznanym formacie - pominiety."); return; }
                _had = true;
                BuildFunding.HeldImport(f[2]);
                KingdomTreasury.RefundHeldImport(f[3] + "|" + f[4] + "|" + f[5] + "|" + f[6]);
            }
            catch (Exception e) { _had = false; Log.Error("DayHeld.Import", e); }
        }

        /// <summary>OnSessionLaunched, PO McmSettings.Apply (klucz z MCM): odtworzenie (przy kluczu) i jedna linia logu. Nowa kampania / stary zapis - nic.</summary>
        internal static void SessionStart()
        {
            try
            {
                if (!_had) { BuildFunding.HeldResolve(false); KingdomTreasury.RefundHeldResolve(false); return; }
                _had = false;
                bool on = On;
                int today = (int)CampaignTime.Now.ToDays;
                bool same = _savedDay == today;
                string b = BuildFunding.HeldResolve(on && same);
                string r = KingdomTreasury.RefundHeldResolve(on);
                Log.Info("Doba po wczytaniu (G20): dzien " + today + ", zapis z doby " + _savedDay + (same ? " (ta sama doba)" : " (inna doba - budowy nie)")
                         + " | klucz Mid Day Load Keeps Day " + (on ? "tak" : "nie (jak dotad - nic nie odtwarzane)") + " | " + b + " | " + r
                         + " | nie odtwarzane (przyjete): robocizna 11c od skupu, kara P1b reszty doby.");
            }
            catch (Exception e) { Log.Error("DayHeld.SessionStart", e); }
        }
    }
}
