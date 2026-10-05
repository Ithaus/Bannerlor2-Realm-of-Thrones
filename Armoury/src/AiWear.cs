using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// ZUZYCIE SPRZETU AI (wpis 85 - na razie szkic dla wpisu 84). Zbrojownie DTE lordow AI to lista "przedmiot: ile" bez stanu.
    /// Do czasu wlasnego zapisu stanu: sztuka sprzedawana z nadwyzek dostaje stan jak lup gracza (wrak albo ~45%).
    /// </summary>
    internal static class AiWear
    {
        internal static ItemModifier TakeCondition(MobileParty mp, ItemObject it) { return ArmouryBehavior.PickWornModifier(it); }
        internal static int OutstandingCost(MobileParty mp) { return 0; }
        internal static void Reset() { }
    }
}
