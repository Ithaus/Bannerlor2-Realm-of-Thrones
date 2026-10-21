using System;
using TaleWorlds.CampaignSystem.Party;

namespace GrandTourney
{
    /// <summary>
    /// m104-e5 (prolog 298, Armoury Prologue.World): wejscie dla scen Armoury, wolane odbiciem (Armoury nie zna tego DLL). Bez Armoury albo w kampanii klasycznej
    /// wszystko zostaje jak dotad (pola puste).
    /// SkipSummon - herold (TourneyBehavior.SummonLords) nie wzywa partii, ktora prowadzi scena (orszak krola, Beric, uczestnicy kanonu w drodze); null = jak dotad.
    /// </summary>
    public static class CanonApi
    {
        public static Func<MobileParty, bool> SkipSummon;

        internal static bool Skip(MobileParty mp)
        {
            var f = SkipSummon;
            if (f == null || mp == null) return false;
            try { return f(mp); } catch { return false; }
        }
    }
}
