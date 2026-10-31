using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// 307e (autotest 143a bitwa 4; Jeff 31.10 "sprawdz bitwe czy wszystko dobrze dziala") - UCIEKINIER ZE ZWYCIESKIEJ STRONY WRACA DO SWOJEJ DRUZYNY.
    /// GRA (dekompilacja TaleWorlds.CampaignSystem 1.4.8): PartyGroupAgentOrigin.SetRouted -> MapEventSide.OnTroopRouted -> MapEventParty.OnTroopRouted:
    /// szeregowy, ktory zszedl z pola jako Routed (nie na rozkaz odwrotu, nie obronca oblezenia), od razu MemberRoster.AddToCounts(-1) i RoutedInBattle +1
    /// (MapEventParty.cs:316-323) - bez wzgledu na wynik bitwy. Gra RoutedInBattle czyta tylko w DesertersCampaignBehavior (pobita strona, druzyny lordow).
    /// Nasze H3 (OutlawLaw.RoutedH3Body) dzieli rozbitych KAZDEJ bitwy: pobita druzyna lorda - N7 (StragglersRally, 55% pod choragiew), reszta i ZWYCIEZCY -
    /// OutlawRoutedShare do lasu, reszta do domu. Dowod [L] (143a bitwa 4, Armoury-2026-10-31_10-09-39.log 10:15:16-10:15:22): 'KSIEGA LUDZI: -1 Riverlands
    /// Swordmaster, w bitwie | zabral: ... MapEventParty.OnTroopRouted' x20; przy zlamaniu RoutHold (BrokenMenNeverRally) kazdy zlamany idzie az za pole,
    /// wiec w dlugiej bitwie strona, ktora pole utrzymala, tracila uciekinierow jak pobita.
    /// REGULA (jedna dla wszystkich partii zwycieskiej strony - gracz, lordowie, bandy, karawany, zalogi): w pierwszym nasluchu MapEventEnded Armoury
    /// (OutlawLaw.OnMapEventEnded, PRZED DoomedPartyMen i H3) z rozbitych kazdej partii strony WinningSide wraca do jej skladu RoutedWinnersReturnShare
    /// (domyslnie 0.9, losowanie zaokraglenia jak gra - RoundRandomized), zdrowi (zeszli z pola zdrowi); reszta zostaje w RoutedInBattle i idzie droga H3
    /// jak dotad (dom / las - czesc dezerteruje i po zwyciestwie). Pobita strona i bitwa bez zwyciezcy (odwrot, limit czasu) - bez zmian (N7 / H3).
    /// Ksiega ludzi: wracajacy nie zeszli ze slownika pochodzenia (H3 bierze Take dopiero z RoutedInBattle) - udzialy zostaja przy druzynie, nikt nie powstaje.
    /// Zloto, sprzet: bez zmian (DTE zwraca sprzet zywych jak dotad). Wyjatek liczony per partia (bez globalnego wylacznika). Linia: segment w dopisku H3
    /// linii "Wyrzutki" (doba) i linia przy bitwie gracza.
    /// </summary>
    internal static class RoutedReturn307e
    {
        private static int _men, _parties, _kept, _over, _player, _stumbles;   // _over: ponad limit partii - zostali w rozbitych (H3)
        private static bool _err;

        internal static void NewDay() { _men = _parties = _kept = _over = _player = _stumbles = 0; }

        internal static void Reset() { NewDay(); _err = false; }

        internal static string DayText()
        {
            var s = Settings.Current;
            float sh = s != null ? MBMath.ClampFloat(s.RoutedWinnersReturnShare, 0f, 1f) : 0f;
            return " | rozbici zwyciezcow wracaja (307e, udzial " + sh.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "): do druzyn " + _men
                   + " (druzyn " + _parties + ", w tym gracza " + _player + "), zostali w rozbitych (H3) " + _kept + " (w tym ponad limit partii " + _over + ")" + (_stumbles > 0 ? ", potkniecia " + _stumbles : "");
        }

        /// <summary>OutlawLaw.OnMapEventEnded (przed DoomedPartyMen.Before i H3): czesc rozbitych zwycieskiej strony z powrotem do skladu ich partii.</summary>
        internal static void Apply(MapEvent me)
        {
            var s = Settings.Current;
            if (me == null || s == null) return;
            float share = MBMath.ClampFloat(s.RoutedWinnersReturnShare, 0f, 1f);
            if (share <= 0f) return;
            BattleSideEnum win;
            try { win = me.WinningSide; } catch { return; }
            if (win == BattleSideEnum.None) return;
            MapEventSide side;
            try { side = me.GetMapEventSide(win); } catch { return; }
            if (side == null || side.Parties == null) return;
            bool playerHere = false;
            int pBack = 0, pRouted = 0, pOver = 0;
            foreach (var mep in side.Parties)
            {
                try
                {
                    if (mep == null || mep.Party == null || mep.RoutedInBattle == null || mep.RoutedInBattle.TotalRegulars <= 0) continue;
                    var mp = mep.Party.MobileParty;
                    if (mp != null && !mp.IsActive) continue;
                    var roster = mep.RoutedInBattle;
                    var list = new List<TroopRosterElement>(roster.GetTroopRoster());   // kopia - AddToCounts usuwa element przy 0
                    int back = 0, all = 0, over = 0;
                    // recenzja 307e: powrot nie ponad limit partii gry (PartySizeLimit - sklad), nadwyzka zostaje w rozbitych i idzie do H3 jak reszta
                    int room = int.MaxValue;
                    try { room = Math.Max(0, mep.Party.PartySizeLimit - mep.Party.MemberRoster.TotalManCount); } catch { room = int.MaxValue; }
                    foreach (var e in list)
                    {
                        if (e.Character == null || e.Character.IsHero || e.Number <= 0) continue;
                        int n = e.Number;
                        all += n;
                        int b = Math.Min(n, Math.Max(0, MBRandom.RoundRandomized(n * share)));
                        if (b > room) { over += b - room; b = room; }
                        if (b <= 0) continue;
                        room -= b;
                        mep.Party.MemberRoster.AddToCounts(e.Character, b);
                        roster.AddToCounts(e.Character, -b);
                        back += b;
                    }
                    _over += over;
                    bool isPl = mp != null && mp == MobileParty.MainParty;
                    if (isPl) { playerHere = true; pRouted += all; pOver += over; }
                    if (back <= 0) { _kept += all; continue; }
                    _men += back; _kept += all - back; _parties++;
                    if (isPl) { _player += back; pBack += back; }
                }
                catch (Exception e) { _stumbles++; if (!_err) { _err = true; Log.Error("RoutedReturn307e.Apply", e); } }
            }
            if (playerHere)
                Log.Info("Rozbici zwyciezcow wracaja (307e): bitwa wygrana przez strone gracza | uciekli z pola " + pRouted + ", wrocilo do druzyny gracza " + pBack
                         + ", reszta (" + (pRouted - pBack) + ", w tym ponad limit druzyny " + pOver + ") jak rozbici dotad (H3: dom albo las).");
        }
    }
}
