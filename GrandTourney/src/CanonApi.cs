using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GrandTourney
{
    /// <summary>
    /// m104-e5 (prolog 298, Armoury Prologue.World): wejscie dla scen Armoury, wolane odbiciem (Armoury nie zna tego DLL). Bez Armoury albo w kampanii klasycznej
    /// wszystko zostaje jak dotad (pola puste).
    /// SkipSummon - herold (TourneyBehavior.SummonLords) nie wzywa partii, ktora prowadzi scena (orszak krola, Beric, uczestnicy kanonu w drodze); null = jak dotad.
    /// ProclaimRoyal - TURNIEJ KANONU (Turniej Reki w Krolewskiej Przystani): turniej w miescie (nowy albo przejety), obwieszczony jak Grand bez reguly osmiu i bez
    /// spadku do lokalnego przy wojnie krolestwa (wpis "|c" w gt_events), zjazd GatherDays dni (listy zamkniete, gra go nie rozstrzyga - NoEarlyEnd), potem otwarty;
    /// uczestnicy kanonu (Stringi bohaterow) wchodza do drabinki przed innymi lordami (NoblesFightPatch). Rozstrzygniecie - jak kazdy turniej (gracz albo AI); Armoury
    /// rozstrzyga go sam (TournamentManager.ResolveTournament) w dniu, w ktorym musi sie odbyc.
    /// </summary>
    public static class CanonApi
    {
        public static Func<MobileParty, bool> SkipSummon;

        // turniej kanonu: miasto i uczestnicy (bez zapisu - Armoury podaje je ponownie po wczytaniu przez SetHeroes)
        private static string _town;
        private static readonly HashSet<string> _heroes = new HashSet<string>(StringComparer.Ordinal);

        internal static bool Skip(MobileParty mp)
        {
            var f = SkipSummon;
            if (f == null || mp == null) return false;
            try { return f(mp); } catch { return false; }
        }

        /// <summary>Turniej kanonu w miescie townId; zwraca opis do logu Armoury (albo powod odmowy).</summary>
        public static string ProclaimRoyal(string townId, string[] canonHeroIds)
        {
            try
            {
                var b = TourneyBehavior.Instance;
                if (b == null) return "BRAK zachowania GrandTourney";
                if (!Settings.Current.Enabled) return "GrandTourney wylaczony (Enabled)";
                var st = Settlement.Find(townId);
                if (st == null || st.Town == null) return "brak miasta " + townId;
                SetHeroes(townId, canonHeroIds);
                return b.ProclaimCanon(st.Town);
            }
            catch (Exception e) { Log.Error("CanonApi.ProclaimRoyal", e); return "BLAD " + e.GetType().Name; }
        }

        /// <summary>Uczestnicy kanonu turnieju w miescie townId (po wczytaniu zapisu - Armoury podaje ich ponownie); null/puste - brak.</summary>
        public static void SetHeroes(string townId, string[] canonHeroIds)
        {
            _heroes.Clear();
            _town = string.IsNullOrEmpty(townId) ? null : townId;
            if (canonHeroIds != null) foreach (var id in canonHeroIds) if (!string.IsNullOrEmpty(id)) _heroes.Add(id);
        }

        /// <summary>NoblesFightPatch: uczestnicy kanonu dla tej osady (null - nie ten turniej).</summary>
        internal static HashSet<string> HeroesFor(Settlement s)
        {
            if (_town == null || _heroes.Count == 0 || s == null || s.StringId != _town) return null;
            return _heroes;
        }

        /// <summary>Turniej w tym miescie zakonczony / odwolany - koniec pierwszenstwa kanonu.</summary>
        internal static void Done(Settlement s)
        {
            if (_town != null && s != null && s.StringId == _town) { _town = null; _heroes.Clear(); }
        }
    }
}
