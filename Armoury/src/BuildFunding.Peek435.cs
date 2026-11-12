using System;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// 435-L9 (lancuch podazy, krok 9 - PRZEZBROJENIE STOJACEGO WARSZTATU; WorkshopIdleRetool.cs): SAM ODCZYT polki - ile zlota materialu robot (te same zapasy polki
    /// co DsWorksBuy: narzedzia 5 sztuk, drewno max(50, zapas rzemiosla miasta), glina ponad potrzebe garncarzy; sufit ceny CapOf) lezy dzis na polce `market`
    /// w materialach `items` (null = nie liczyc). Przezbrojenie placi polowe ceny materialem - bez tej polowy na polce nic nie schodzi z kapitalu ani z kiesy
    /// (zaden przelew przed sprawdzeniem). Cena sztuki = cena polki teraz (zakup idzie po rosnacej cenie, wiec wynik to gorna granica). Nic nie zmienia, nie rzuca.
    /// </summary>
    internal static partial class BuildFunding
    {
        internal static long DsWorksPeek(Settlement market, ItemObject[] items)
        {
            try
            {
                if (market == null || market.Town == null || market.ItemRoster == null || items == null || items.Length < MatIds.Length) return 0;
                var roster = market.ItemRoster;
                var town = market.Town;
                long val = 0;
                for (int k = 0; k < MatIds.Length; k++)
                {
                    var it = items[k];
                    if (it == null) continue;
                    int have = roster.GetItemNumber(it);
                    if (have <= 0) continue;
                    if (MatIds[k] == "tools") have -= ToolsKeep;
                    else if (MatIds[k] == "clay") have -= F7Workshops.ClayHold(market, it, true);
                    else if (MatIds[k] == "hardwood") have -= Math.Max(WoodKeep, WoodHold(market, it, true));
                    if (have <= 0) continue;
                    int price = Math.Max(1, town.GetItemPrice(it, null, false));
                    int cap = CapOf(it);
                    if (cap > 0 && price > cap) continue;
                    val += (long)have * price;
                }
                return val;
            }
            catch (Exception e) { FolkStumble("DsWorksPeek", e); return 0; }
        }
    }
}
