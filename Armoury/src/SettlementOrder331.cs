using System;
using System.Globalization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// 331 - ROZKAZ DO OSADY WYGRYWA Z NASZYM POSTOJEM (Jeff 05.11: "klikniecie i wjezdzam do miasta"). Przyczyna (CHANGELOG 331): nasze automatyczne postoje gracza
    /// (trening przed snem 311e, oboz / pytanie o zmierzchu, zapasc po 3 nocach bez snu, zapasc marszu forsownego) wolaly SetMoveModeHold bez wzgledu na to, ze gracz
    /// kliknal osade gry i stoi pare jednostek od bramy - kolumna stawala pod murami, menu miasta sie nie otwieralo (log 04.11: "cel porzucony ... odleglosc od bramy
    /// 0.78 | menu arm_camp_drill_wait").
    /// REGULA: partia gracza z rozkazem do osady gry (TargetSettlement = wies / miasto / zamek, DefaultBehavior albo ShortTermBehavior GoToSettlement), poza osada,
    /// bitwa i cudza armia, osada dostepna (nie wroga, bez oblezenia, bez bitwy / rabunku przy niej), dojazd <= PlayerSettlementOrderMaxHours h gry
    /// (trasa MapDistanceModel / predkosc partii, ta sama miara co NightMarch.HoursToSettlement) - automatyczny postoj NIE staje: partia wjezdza, cwiczenia i sen
    /// odbywaja sie w osadzie (m109 musztra w osadzie, nocleg w miescie / karczmie). Dalej niz limit - bez zmian. Recznie wywolany oboz (klawisz O, menu obozu,
    /// odpowiedz na pytanie Night Falls) bez zmian. Linia przy kazdym pominieciu: "Oboz (331): gracz jedzie do X (h) - postoj pominiety: ...".
    /// Klucze NOWE: PlayerSettlementOrderBeatsCamp (TAK), PlayerSettlementOrderMaxHours (3). Klucz NIE = jak dotad.
    /// </summary>
    internal static class SettlementOrder331
    {
        private const string P = "Oboz (331): ";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static int _skips, _stumbles;

        /// <summary>true = automatyczny postoj `what` pominiety (gracz jedzie do osady w zasiegu) - linia logu. Nie rzuca; blad = false (postoj jak dotad).</summary>
        internal static bool Beats(MobileParty mp, string what)
        {
            try
            {
                Settlement st; float h, route;
                if (!InReach(mp, out st, out h, out route)) return false;
                _skips++;
                Log.Info(P + "gracz jedzie do " + st.Name + " (" + st.StringId + ", " + Kind(st) + ", " + h.ToString("0.0", Inv) + " h, trasa " + route.ToString("0.0", Inv)
                         + " jedn., od bramy " + mp.GetPosition2D.Distance(st.GatePosition.ToVec2()).ToString("0.0", Inv) + ") - postoj pominiety: " + what
                         + " | ruch " + mp.DefaultBehavior + "/" + mp.ShortTermBehavior + " | limit " + Lim().ToString("0.0", Inv) + " h | pominiec w sesji " + _skips + ".");
                return true;
            }
            catch (Exception e) { if (++_stumbles <= 3) Log.Error("SettlementOrder331.Beats", e); return false; }
        }

        private static float Lim()
        {
            var s = Settings.Current;
            return s == null ? 0f : Math.Max(0f, Math.Min(24f, s.PlayerSettlementOrderMaxHours));
        }

        /// <summary>Partia gracza jedzie do dostepnej osady gry, do ktorej dojedzie w limicie godzin.</summary>
        internal static bool InReach(MobileParty mp, out Settlement st, out float hours, out float route)
        {
            st = null; hours = route = -1f;
            var s = Settings.Current;
            if (s == null || !s.PlayerSettlementOrderBeatsCamp || mp == null || mp != MobileParty.MainParty || Campaign.Current == null) return false;
            if (mp.CurrentSettlement != null || mp.MapEvent != null || mp.SiegeEvent != null || mp.BesiegerCamp != null || mp.AttachedTo != null) return false;
            if (mp.Army != null && mp.Army.LeaderParty != mp) return false;
            var t = mp.TargetSettlement;
            if (t == null || !(t.IsTown || t.IsCastle || t.IsVillage)) return false;
            if (mp.DefaultBehavior != AiBehavior.GoToSettlement && mp.ShortTermBehavior != AiBehavior.GoToSettlement) return false;
            // dostepna: nie wroga, bez oblezenia i bez bitwy / rabunku przy osadzie (wtedy spotkanie nie jest wjazdem)
            if (t.MapFaction != null && mp.MapFaction != null && FactionManager.IsAtWarAgainstFaction(t.MapFaction, mp.MapFaction)) return false;
            if (t.SiegeEvent != null || (t.Party != null && t.Party.MapEvent != null)) return false;
            float lim = Lim();
            if (lim <= 0f) return false;
            float lr;
            float d = -1f;
            try { d = Campaign.Current.Models.MapDistanceModel.GetDistance(mp, t, mp.IsTargetingPort, MobileParty.NavigationType.Default, out lr); } catch { d = -1f; }
            if (!(d > 0f && d < 1000000f)) d = mp.GetPosition2D.Distance(t.GatePosition.ToVec2()) * 1.1f;
            float sp = 0f;
            try { sp = mp.Speed; } catch { }
            if (sp <= 0f) sp = 2.4f;
            float uph = Math.Max(0.05f, sp / 1.2f);   // jedn. na godzine gry (4320 s gry na jednostke dt - NightMarch.UnitsPerHour)
            float h = d / uph;
            if (h > lim) return false;
            st = t; hours = h; route = d;
            return true;
        }

        private static string Kind(Settlement st) { return st.IsTown ? "miasto" : st.IsCastle ? "zamek" : "wies"; }
    }
}
