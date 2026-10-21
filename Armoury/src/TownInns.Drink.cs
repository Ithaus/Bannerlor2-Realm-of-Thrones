using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    internal static partial class TownInns
    {
        // m98-plotki (Rumours.Drink.cs): kufel przed plotkami placony ta sama droga co nocleg gracza - jedna regula na jedno zjawisko (uslugi karczmarzy).
        private static readonly List<Hero> NoComp = new List<Hero>();

        /// <summary>Kufel w karczmie miasta: gracz -> kasa miasta (GiveGoldAction) -> kiesa ludu miasta (TownFolk.EarnFromTown, KLife) - jak HeroPays noclegu.
        /// Komunikat gry wylaczony przy PlayerPaymentReasons (PayNotes). Zwraca zaplacone; `folk` - ile doszlo do kiesy ludu.</summary>
        internal static int DrinkPayTown(Settlement st, int cost, out int folk)
        {
            folk = 0;
            if (st == null || st.Town == null) return 0;
            return HeroPays(Hero.MainHero, st, cost, PayNotes.Quiet(false), out folk);
        }

        /// <summary>Kufel w zajezdzie: gracz -> kiesa wsi okregu, podatek pana na licznik, udzial korony DS - jak RoadHeroPays noclegu.</summary>
        internal static int DrinkPayRoad(Settlement vs, int cost, out int tax, out int crown)
        {
            tax = 0; crown = 0;
            return RoadHeroPays(Hero.MainHero, vs, cost, PayNotes.Quiet(false), out tax, out crown);
        }

        /// <summary>Kontrola obiegu kufla w miescie: te same posiadacze co nocleg (World) bez towarzyszy.</summary>
        internal static long DrinkWorldTown(Settlement st) { return World(st, NoComp); }

        /// <summary>Kontrola obiegu kufla w zajezdzie: te same posiadacze co nocleg (RoadWorld) bez towarzyszy (korona DS doliczana przez wolajacego).</summary>
        internal static long DrinkWorldRoad(Settlement vs) { return RoadWorld(vs, NoComp); }
    }
}
