using System;
using System.Text;
using TaleWorlds.CampaignSystem;

namespace Armoury
{
    /// <summary>
    /// E8/S (246, PLAN 8.11; projekt etapu 8 v2 rozdz. 3 "E8/S"; rozpoznanie C poz. 11, 15, 17): PORZADEK STANU.
    /// Dwie rzeczy, obie pod kluczem StateHygieneEnabled (off = wszystko jak przed E8/S; wyjatek - audyt 13.10: pamiec paczki E8/H w SaddleLaw
    /// jest czyszczona zawsze, bo powstala po E8/S):
    ///  1. Pamiec statyczna jednej kampanii nie przechodzi do drugiej w tym samym uruchomieniu gry (nowa gra -> zapis -> wczytanie innego zapisu):
    ///     HideoutPurge (czekajace przeszukanie, odwet, lup), oboz gracza w NightRest (potkniecia namiotu, odcisk wizerunku, dzien pytania o nocleg,
    ///     flaga obozu), dzien audytu predkosci w TerrainEase, przerwa zakupow stajni AI (Stables._lastBuy - dzien z dalszej kampanii dawal ujemna
    ///     roznice = blokade zakupow koni), straz przeprawy (CrossingLaw: ostatnia bezpieczna pozycja gracza, przerwy partii AI).
    ///     ResetAll wola WorldMeasure.Reset, czyli konstruktor ArmouryBehavior (nowa kampania i kazde wczytanie, PRZED SyncData) - jak E8/P3,
    ///     bez dotykania linii konstruktora. Zadna z tych rzeczy nie jest w zapisie, wiec po wyzerowaniu stan jest taki jak po uruchomieniu gry.
    ///  2. Jedna wywrotka nie gasi funkcji do konca sesji (CLAUDE.md 7 "licz potkniecia, nie gas funkcji"): start klimatu (WesterosClimate.Ready -
    ///     ponowna proba w kolejnej dobie, najwyzej 3) i czekajace przeszukanie kryjowki (HideoutPurge.OnTick - najwyzej 3 proby).
    /// Do tego sen dlugu w osadzie zdejmuje martwy wpis obozu swiata (NightMarch.EnterDebt).
    /// Tu tylko zerowanie przez metody tych klas, liczniki i dwie linie logu ("Porzadek stanu (E8/S)": start sesji i doba). Zadnego zlota ani sztuk.
    /// </summary>
    internal static class StateHygiene
    {
        internal const int Classes = 5;   // HideoutPurge, NightRest (oboz gracza), TerrainEase (audyt), Stables, CrossingLaw

        internal static bool On { get { var s = Settings.Current; return s != null && s.StateHygieneEnabled; } }

        // zerowanie tej kampanii (konstruktor): ile klas wyzerowano (-1 = ResetAll nie biegl od poprzedniej linii startu), co zostalo w pamieci
        private static int _zeroed = -1, _resetStumbles;
        private static bool _wasOn;
        private static string _left = "";
        // liczniki doby (tylko log)
        private static int _dBuys, _dHold, _dFuture, _dHideStumbles, _dHideGivenUp, _dDeadOrders;
        private static int _stumbles;

        /// <summary>Nowa kampania i kazde wczytanie (WorldMeasure.Reset - konstruktor ArmouryBehavior, przed SyncData). Nie rzuca: wyjatek tutaj
        /// przerwalby konstruktor zachowania kampanii.</summary>
        internal static void ResetAll()
        {
            try
            {
                _zeroed = 0; _resetStumbles = 0; _left = ""; _stumbles = 0;
                _dBuys = _dHold = _dFuture = _dHideStumbles = _dHideGivenUp = _dDeadOrders = 0;
                _wasOn = On;
                // audyt 13.10 (drobne, [zapis]): pamiec paczki E8/H (SaddleLaw: spisy wzorcow i przedmiotow, doby sprzedazy uprzezy pod StringId partii,
                // potkniecia) to stan NOWEJ paczki, nie "stan sprzed E8/S" - czyszczona zawsze, takze przy wylaczonym kluczu (dotad jedyna droga do
                // SaddleLaw.Reset szla przez Stables.Reset pod kluczem). Przy wlaczonym kluczu bez zmian (Stables.Reset wola ja jak dotad).
                if (!_wasOn)
                {
                    try { SaddleLaw.Reset(); } catch (Exception e) { _resetStumbles++; try { Log.Error("StateHygiene.Reset (uprzaz E8/H, klucz wylaczony)", e); } catch { } }
                    return;
                }
                var sb = new StringBuilder();
                Step(sb, "kryjowka", HideoutPurge.Reset);
                Step(sb, "oboz gracza", NightRest.ResetCamp);
                Step(sb, "audyt predkosci", TerrainEase.ResetAudit);
                Step(sb, "stajnie AI (wpisy)", Stables.Reset);
                Step(sb, "przeprawa", CrossingLaw.Reset);
                _left = sb.ToString();
            }
            catch (Exception e) { _resetStumbles++; try { Log.Error("StateHygiene.ResetAll", e); } catch { } }
        }

        private static void Step(StringBuilder sb, string name, Func<int> reset)
        {
            try
            {
                int had = reset();
                _zeroed++;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(name).Append(' ').Append(had);
            }
            catch (Exception e) { _resetStumbles++; Log.Error("StateHygiene.Reset (" + name + ")", e); }
        }

        /// <summary>Linia startu sesji (ArmouryBehavior.OnSessionLaunched). Po linii znacznik zerowania wraca na -1: nastepna kampania bez ResetAll
        /// (zgubione wywolanie z konstruktora) da linie UWAGA zamiast liczby z poprzedniej.</summary>
        internal static void SessionStart()
        {
            int k, r; bool live;
            WesterosClimate.HygieneState(out k, out r, out live);
            string head;
            if (_zeroed < 0)
                head = "UWAGA - zerowanie nie bieglo przed ta sesja (brak wywolania z konstruktora); wyzerowane 0 klas";
            else if (!_wasOn)
                head = "WYLACZONE (StateHygieneEnabled) - wyzerowane 0 klas";
            else
                head = "wyzerowane " + _zeroed + " klas";
            Log.Info("Porzadek stanu (E8/S): " + head + "; klimat potkniecia " + k
                     + (_zeroed >= 0 && _wasOn ? " | z " + Classes + " klas (HideoutPurge, NightRest, TerrainEase, Stables, CrossingLaw); w pamieci po poprzedniej kampanii bylo: " + (_left.Length > 0 ? _left : "-")
                                                 + " | potkniecia zerowania " + _resetStumbles : ""));
            _zeroed = -1;
        }

        // ------------------------------------------------------------ liczniki (tylko log; wolane z jednego watku kampanii)
        /// <summary>Stables.OnSettlementEntered: wizyta lorda zatrzymana przerwa zakupow; future = znacznik zakupu pozniejszy niz dzis (pamiec innej kampanii).</summary>
        internal static void NoteStableHold(bool future) { _dHold++; if (future) _dFuture++; }
        internal static void NoteStableBuy() { _dBuys++; }
        /// <summary>HideoutPurge.OnTick: potkniecie przy czekajacym przeszukaniu; givenUp = trzecie z rzedu, przeszukanie porzucone.</summary>
        internal static void NoteHideout(bool givenUp) { _dHideStumbles++; if (givenUp) _dHideGivenUp++; }
        internal static void NoteDeadOrder() { _dDeadOrders++; }

        /// <summary>Linia doby (ArmouryBehavior.OnDailyTick, po WesterosClimate.Daily - stan klimatu juz po ponowieniu tej doby). Liczniki od poprzedniej linii.</summary>
        internal static void Daily()
        {
            try
            {
                if (Campaign.Current == null) return;
                int k, r; bool live;
                WesterosClimate.HygieneState(out k, out r, out live);
                bool on = On;
                Log.Info("Porzadek stanu (E8/S): dzien " + (int)CampaignTime.Now.ToDays + (on ? "" : " (WYLACZONE)")
                         + " | stajnie AI: zakupy " + _dBuys + ", wizyty w przerwie zakupow " + _dHold + ", w tym znacznik z przyszlosci " + _dFuture
                         + " | kryjowka: potkniecia przeszukania " + _dHideStumbles + ", porzucone " + _dHideGivenUp
                         + " | martwe wpisy obozu zdjete dluznikom w osadzie " + _dDeadOrders
                         + " | klimat: potkniecia " + k + "/3, ponowien " + r + ", czynny " + (!WesterosClimate.On ? "wylaczony" : live ? "tak" : "nie")
                         + " | potkniecia linii " + _stumbles);
                _dBuys = _dHold = _dFuture = _dHideStumbles = _dHideGivenUp = _dDeadOrders = 0;
            }
            catch (Exception e) { _stumbles++; Log.Error("StateHygiene.Daily", e); }
        }
    }
}
