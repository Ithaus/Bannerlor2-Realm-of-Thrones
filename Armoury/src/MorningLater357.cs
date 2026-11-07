using System;
using TaleWorlds.CampaignSystem;

namespace Armoury
{
    /// <summary>
    /// 357 - PORANNY MELDUNEK KWATERMISTRZA RANO, NIE W TAKCIE DOBY (Jeff 07.11 "monitoruj czy nie da sie zrobic gry max plynnej" -> zaciecie o polnocy "tak").
    /// Dowod: Armoury-2026-11-07_20-51-37.log (sesja Jeffa) - "Profil doby (E8/0)": takt doby ok. 877 ms, najwieksza pozycja MorningReport 177 ms
    /// (QuartermasterLaw.ShoutShortages liczy podglad bitwy KitPreview). Meldunek tylko czyta stan armii gracza i pisze na ekran - nic w takcie doby z niego nie korzysta
    /// (ten sam meldunek wola sie tez po bitwie i przy otwarciu zbrojowni), wiec mozna go puscic w innej klatce.
    /// Takt doby oznacza dzien (MarkDue); pierwszy takt godziny co najmniej DelayHours po takcie doby wola meldunek (Hourly). Odstep liczony od chwili taktu doby,
    /// nie od godziny zegara: takt doby nie wypada o polnocy (DayGate 280 - pora startu kampanii), a takt godziny ma przesuniecie gry - liczenie od chwili
    /// gwarantuje osobna klatke. Stan bez zapisu: wczytanie przed pora meldunku = tej doby meldunku nie ma (jak dotad przy wczytaniu po takcie).
    /// </summary>
    internal static class MorningLater357
    {
        internal const int DelayHours = 6;

        private static double _dueH = -1;   // chwila taktu doby z oczekujacym meldunkiem (godziny kampanii, CampaignTime.ToHours), -1 = nic nie czeka

        internal static void Reset() { _dueH = -1; }

        /// <summary>Takt doby: meldunek tej doby czeka na godzine DelayHours po takcie. Poprzedni niewykonany (np. kilka dob w jednej klatce) przepada.</summary>
        internal static void MarkDue()
        {
            _dueH = CampaignTime.Now.ToHours;
        }

        /// <summary>Takt godziny: meldunek, gdy minelo DelayHours od taktu doby.</summary>
        internal static void Hourly()
        {
            if (_dueH < 0) return;
            double now = CampaignTime.Now.ToHours;
            if (now < _dueH) { _dueH = now; return; }   // czas cofniety (nowa kampania bez Reset) - licz od teraz
            if (now - _dueH < DelayHours - 0.02) return;   // ok. minuta luzu na przesuniecie taktu godziny
            _dueH = -1;
            // poranny meldunek: braki wykrzyczane na glos, zeby Jeff WIEDZIAL bez otwierania zbrojowni ("czemu ja o tym nie wiem!")
            QuartermasterLaw.ShoutShortages("QM morning: short (have/need):");
        }
    }
}
