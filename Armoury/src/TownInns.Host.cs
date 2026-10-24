using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// n20-wioska (poprawka recenzji, WAZNE): NOCLEG WE WLASNEJ WIOSCE POBOCZNEJ GRACZA (SideVillageFief, klucz SideVillageFiefLodging). Dotad "Rest the night here" byl
    /// zwyklym snem obozowym (NightRest.BeginInnSleep bez stanu noclegu: namiot, mnoznik 1, bez premii), a podpowiedz obiecywala dach i wypoczecie. Teraz ten sam stan
    /// noclegu co karczma miasta i zajazd przy szlaku (_rest), jakosci 0 ("wspolna izba": slome i dach) - BEZ OPLATY (to ludzie gracza; nikt nie placi i nikt nie dostaje,
    /// wiec bez przeplywu zlota i bez ksiegi pieniadza):
    ///  - HostUid = uid wioski gracza, TownId = wies gry okregu, TownName = nazwa wioski; premie QSpeed / QMorale / QSleep / QHeal [0] (predkosc +5%, morale +2, sen x1,
    ///    bez gojenia), bez premii cen;
    ///  - sen w menu NightRest jak w zajezdzie przy szlaku: partia poza osada przy wiosce (do RoadSleepR od obrazka wioski) albo w wsi gry okregu (wejscie "Visit X");
    ///    pobudka nie przed switem (gdy swit w 12 h), premia "wypoczeci" do zmierzchu po przespanej bazie (TownInns.Wake bez zmian), sen przerwany - bez premii;
    ///  - na mapie partia stoi poza osada - namiot jak przy zajezdzie przy szlaku (NightRest.SleepInit bez zmian; zwykly nocny postoj i tak stawia namiot);
    ///  - zapis: arm_inns pole 17 (uid wioski), starsza DLL je pomija.
    /// </summary>
    internal static partial class TownInns
    {
        private const string PH = "Wioska gracza (n20): ";
        internal const int HostQ = 0;   // jakosc noclegu we wlasnej wiosce: wspolna izba

        /// <summary>Premia po noclegu we wlasnej wiosce - tekst podpowiedzi (angielski).</summary>
        internal static string HostBonusText()
        {
            return "+" + QSpeed[HostQ].ToString("0", Inv) + "% speed and +" + QMorale[HostQ].ToString("0", Inv) + " morale until dusk";
        }

        /// <summary>Opcja "Rest the night here" w menu wioski gracza (zwykle menu - SwitchToMenu do snu wolno): stan noclegu bez oplaty. false - bez stanu (sen jak w obozie).</summary>
        internal static bool HostSleep(string distId, string uid, string name)
        {
            try
            {
                if (!On || distId == null || uid == null) return false;
                long now = NowMin();
                _rest = new Rest { Phase = 1, Q = HostQ, PlayerQ = HostQ, TownId = distId, TownName = name ?? uid, HostUid = uid, CommitMin = now,
                                   Speed = QSpeed[HostQ], Morale = QMorale[HostQ], Sleep = QSleep[HostQ], Heal = QHeal[HostQ], Price = false };
                return true;
            }
            catch (Exception e) { Stumble("HostSleep", e); return false; }
        }

        /// <summary>Partia gracza u swojej wioski: w wsi gry okregu (wejscie "Visit") albo poza osada najwyzej RoadSleepR od obrazka wioski. Watek glowny.</summary>
        private static bool HostNear(Rest r)
        {
            try
            {
                var mp = MobileParty.MainParty;
                if (r == null || r.HostUid == null || mp == null) return false;
                if (!SideVillageFief.IsMine(r.HostUid)) return SideVillageVisit.HostNear(r.HostUid, r.TownId, RoadSleepR);   // m97-wioski: wioska poboczna na ziemi rodu gracza
                var st = mp.CurrentSettlement;
                if (st != null) return st.StringId == r.TownId;
                return SideVillageFief.PlayerDist(r.HostUid) <= RoadSleepR;   // m113-zakup: ta wioska gracza (moze byc dodatkowa)
            }
            catch { return false; }
        }

        /// <summary>Menu wioski gracza: partia stoi u swojej wioski (warunek opcji noclegu).</summary>
        internal static bool HostNearNow(string distId, string uid)
        {
            return HostNear(new Rest { HostUid = uid, TownId = distId });
        }

        /// <summary>NightRest.SleepInit po wyborze noclegu we wlasnej wiosce: poczatek snu, pobudka nie przed switem (gdy swit w 12 h), tlo wsi gry okregu, tekst.
        /// null = partia nie stoi u wioski (tekst NightRest jak dotad; stan zejdzie przy pobudce bez premii albo po 2 h - Hourly).</summary>
        private static string HostSleepInit(MenuCallbackArgs args, Rest r)
        {
            if (r == null || r.Phase != 1 || !HostNear(r)) return null;
            long now = NowMin();
            if (r.StartMin <= 0)
            {
                r.StartMin = now;
                long dawn = NextAt(now, NightRest.PlayerDawn);
                r.WakeMin = dawn - now <= HoldMaxMin ? dawn : 0;
                Log.Info(PH + (IsHouse(r) ? "nocleg we wlasnym domu w " : "nocleg pod dachem w ") + r.Town + " (" + r.HostUid + ", okreg " + r.TownId + ") od " + When(now) + ", "
                         + (r.WakeMin > 0 ? "pobudka nie przed " + When(r.WakeMin) + " (swit)" : "bez czekania do switu (swit dalej niz 12 h) - sen do wyspania")
                         + ", bez oplaty, jakosc " + QLog[Math.Max(0, Math.Min(2, r.Q))] + ": predkosc +" + F1(r.Speed) + "%, morale +" + F1(r.Morale) + ", sen x" + r.Sleep.ToString("0.00", Inv) + ".");
            }
            try
            {
                var st = Settlement.Find(r.TownId);
                string mesh = st != null && st.SettlementComponent != null ? st.SettlementComponent.WaitMeshName : null;
                if (!string.IsNullOrEmpty(mesh)) args.MenuContext.SetBackgroundMeshName(mesh);
            }
            catch { }
            // m110-sam: bez zolnierzy - tekst snu o graczu (sam) albo o nim i towarzyszach
            if (IsHouse(r))   // r4-dom: wlasny dom w wiosce gracza (dobry dom)
                return "You sleep in your own bed in your house at " + r.Town + PartyWords.Pick("; the men bed down in its hall and the horses in its barns", "", "; your companions bed down in its hall and the horses in its barns")
                       + (r.WakeMin > 0 ? " - nobody stirs before dawn." : ".");
            return "Your people of " + r.Town + PartyWords.Pick(" take the men in for the night: they sleep on straw under roofs, the horses in the barns",
                                                                " take you in for the night: you sleep on straw under a roof",
                                                                " take you and your companions in for the night: you sleep on straw under their roofs, the horses in the barns")
                   + (r.WakeMin > 0 ? " - nobody stirs before dawn." : ".");
        }
    }
}
