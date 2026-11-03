using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using CW = Armoury.CirculationWindows;

namespace Armoury
{
    /// <summary>
    /// 322b (audyt ekonomii 03.11, wyciek 5 - odcinek "budowy" gubi 180-540 zl co dobe w kroku TownFolk.DaWorksRun): klasy posiadaczy swiata dla pomiaru
    /// w krokach DaWorksRun (TownFolk.Pomiar322b.cs) - TA SAMA klasyfikacja co ReadHolders (jedna definicja posiadacza). Tylko odczyt, niczego nie zmienia.
    /// </summary>
    internal static partial class MoneyLedger
    {
        internal static int HoldersCount { get { return Holders; } }
        internal const int HolderFolkTown322 = HFolkTown, HolderFolkCastle322 = HFolkCastle, HolderGmina322 = HGmina;

        /// <summary>Posiadacz kasy osady (jak ReadHolders: kasy miast / zamkow / kiesy wsi / kryjowki i inne).</summary>
        internal static int HolderOfSettlement322(Settlement st)
        {
            if (st == null || st.SettlementComponent == null) return -1;
            int c = ClassOf(st);
            return c == CTown ? HTowns : (c == CCastle ? HCastles : (c == CVill ? HVillages : HOtherSettlements));
        }

        /// <summary>Posiadacz kiesy bohatera (jak ReadHolders); -1 - bohater poza swiatem (Dead / Disabled - poza Hero.AllAliveHeroes, zmiane ksieguje CW.NoteHeroGold).</summary>
        internal static int HolderOfHero322(Hero hero)
        {
            if (hero == null || CW.OutOfWorld(hero)) return -1;
            if (hero == Hero.MainHero) return HPlayer;
            if (hero.IsNotable) return HNotables;
            if (hero.IsLord) return hero.Clan != null && hero.Clan.Leader == hero ? HLeaders : HLords;
            if (hero.IsWanderer) return HWanderers;
            return HOtherHeroes;
        }
    }
}
