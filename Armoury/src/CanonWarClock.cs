using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// 274 - ZEGAR WOJNY KANONU: koniec Wojny Pieciu Krolow "do Krwawych Godow" (wspolny znacznik paczek 264 i 274).
    /// Regula (ta sama co CanonPeaceLock.CanonOver w galezi noc/264-pokoj-wojny-kanonu, recenzja 264 (1)): kanon skonczony, gdy ROT RedWeddingEvent ma stan 2
    /// (CompleteEvent - wesele sie odbylo albo ROT je odwolal na zawsze: MainCheck nie przeszedl) ALBO Robb (lord_5_1) nie zyje. Stan 1 (wesele zapowiedziane,
    /// ROT czeka 14 dni) - wojna trwa; -1 (wydarzenia nie widac) - wojna trwa do smierci Robba.
    /// Pamiec na godzine gry (RotEventState i szukanie bohatera raz na godzine); IsAlive Robba czytane na zywo (ROT ExecuteRobb zabija go w srodku godziny).
    /// SCALENIE: gdy 264 wejdzie pierwsze - Over() moze wolac CanonPeaceLock.CanonOver(RedWeddingState, RobbDead); gdy 274 pierwsze - RefreshCache 264 moze czytac
    /// ten zegar. Obie paczki koncza sie tej samej doby, bo regula i dane sa te same (stala 274 R274-7 w sprawdz_logi porownuje dzien konca z linia 264).
    /// Dodatkowo (tylko do linii doby): warunki ROT RedWeddingEvent.MainCheck - Roose (lord_1_17) zyje, Walder Frey (lord_1_5) zyje, Blizniaki (ROT_town3) w rekach
    /// rodu Frey (clan_empire_north_3), Robb zyje, Polnoc (battania) istnieje. Gdy ktorys pada, ROT odwoluje wesele na zawsze (stan 2) - wtedy koniec przychodzi i tak.
    /// </summary>
    internal static class CanonWarClock
    {
        internal const string RobbId = "lord_5_1", RooseId = "lord_1_17", WalderId = "lord_1_5", FreyClanId = "clan_empire_north_3", TwinsId = "ROT_town3";

        private static long _hour = long.MinValue;
        private static int _rw = -1;
        private static Hero _robb, _roose, _walder;
        private static Settlement _twins;

        internal static void Reset() { _hour = long.MinValue; _rw = -1; _robb = _roose = _walder = null; _twins = null; }

        /// <summary>Koniec kanonu: RedWeddingEvent skonczone (stan 2) albo Robb nie zyje. Czysta funkcja (autotest T5) - ta sama co CanonPeaceLock.CanonOver (264).</summary>
        internal static bool Over(int redWedding, bool robbDead) { return redWedding >= 2 || robbDead; }

        private static Hero FindHero(string id)
        {
            try { return Hero.FindFirst(x => x != null && x.StringId == id); } catch { return null; }
        }

        private static void Refresh()
        {
            long h;
            try { h = (long)Math.Floor(CampaignTime.Now.ToHours); } catch { return; }
            if (h == _hour) return;
            _hour = h;
            _rw = Prologue.RotEventState("RedWeddingEvent");
            if (_robb == null) _robb = FindHero(RobbId);
            if (_roose == null) _roose = FindHero(RooseId);
            if (_walder == null) _walder = FindHero(WalderId);
            if (_twins == null) { try { _twins = Settlement.Find(TwinsId); } catch { _twins = null; } }
        }

        /// <summary>Stan ROT RedWeddingEvent (-1 nie widac, 0 czeka, 1 trwa, 2 skonczone) z pamieci godziny.</summary>
        internal static int RedWeddingState { get { Refresh(); return _rw; } }

        /// <summary>Robb nie zyje (bohater znany i martwy); IsAlive na zywo.</summary>
        internal static bool RobbDead { get { Refresh(); return _robb != null && !_robb.IsAlive; } }

        /// <summary>Kanon skonczony teraz (pamiec godziny + Robb na zywo). Nie rzuca.</summary>
        internal static bool OverNow()
        {
            try { return Over(RedWeddingState, RobbDead); } catch { return false; }
        }

        private static string T(bool b) { return b ? "T" : "N"; }

        /// <summary>Warunki ROT RedWeddingEvent.MainCheck do linii doby: "Roose T, Walder T, Blizniaki Frey T, Robb T, Polnoc T".</summary>
        internal static string Conditions()
        {
            try
            {
                Refresh();
                bool roose = _roose != null && _roose.IsAlive, walder = _walder != null && _walder.IsAlive, robb = _robb != null && _robb.IsAlive;
                bool twins = _twins != null && _twins.OwnerClan != null && _twins.OwnerClan.StringId == FreyClanId;
                Kingdom north = null;
                foreach (var k in Kingdom.All) if (k != null && k.StringId == Prologue.NorthId) { north = k; break; }
                bool n = north != null && !north.IsEliminated;
                return "Roose " + T(roose) + ", Walder " + T(walder) + ", Blizniaki Frey " + T(twins) + ", Robb " + T(robb) + ", Polnoc " + T(n);
            }
            catch { return "?"; }
        }
    }
}
