using System;
using System.Threading;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// GL-B / 11c (projekt docs/gospodarka-od-ludzi/PROJEKT-GOSPODARKA-OD-LUDZI-2026-10-13.md rozdz. 9 ryzyko 11 lekarstwo (c)) - LUD ZARABIA OD SKUPU PLONU WSI.
    /// Robocizna handlu (TownLive krok 2: 20% jedzenia / 10% towaru / 4% broni) liczyla sie tylko od SPRZEDAZY kasy kupcow; kiedy kasa KUPUJE plon od wozu wsi,
    /// nikt w miescie nie pracowal. Teraz od zaplaty kasy wozowi wsi (SellGoodsForTradeAction - tabor wsi sprzedaje w miescie; po zwrocie nadplaty MarketCarts,
    /// czyli zaplata netto) TownFolkLabourVillageBuys (10%) idzie jutro z kasy do kiesy ludu tego miasta jako robocizna (wyladunek, miara, przechowanie,
    /// przemial) - ta sama droga co robocizna handlu (FromCoffer, ELabour; kasa placi najwyzej tyle, ile ma - wspolne przyciecie k calej robocizny i obrotu).
    /// Zarobek ludu rosnie wiec z plonem okregu (P1 - plon idzie za ludzmi). Klucz FolkEarnFromVillageBuys; tylko miasta (zamek kupuje z kasy zamku - bez zmian).
    /// Obieg: zloto kasa miasta -> kiesa ludu (jak robocizna), nic nie powstaje; kontrola kies i ksiega obiegu bez zmian (ten sam przelew). Klucz wylaczony:
    /// latka nic nie zapisuje, okno 0 - robocizna bit w bit jak 97dd474.
    /// </summary>
    internal static partial class TownFolk
    {
        private static bool SkupOn { get { var s = Settings.Current; return s != null && s.FolkEarnFromVillageBuys && Live; } }
        private static long _skupN, _skupStumbles;

        /// <summary>Prefiks SellGoodsForTradeAction.ApplyInternal: sakwa taboru wsi przed sprzedaza w miescie (-1 = nie liczymy).</summary>
        public static void SkupPrefix(Settlement __0, MobileParty __1, out int __state)
        {
            __state = -1;
            try
            {
                if (__1 == null || !__1.IsVillager || __0 == null || !__0.IsTown || !SkupOn || OffMain()) return;
                __state = __1.PartyTradeGold;
            }
            catch (Exception e) { __state = -1; Stumble("SkupPrefix", e); }
        }

        /// <summary>Postfiks (Priority.Last - po zwrocie nadplaty MarketCarts.SellPostfix): zaplata netto kasy wozowi do okna miasta (Folk.SkupWin).</summary>
        public static void SkupPostfix(Settlement __0, MobileParty __1, int __state)
        {
            if (__state < 0 || __1 == null) return;
            try
            {
                long paid = (long)__1.PartyTradeGold - __state;
                if (paid <= 0) return;
                var f = Get(__0);
                if (f == null || f.Kind != 0) return;
                f.SkupWin += paid;
                Interlocked.Increment(ref _skupN);
            }
            catch (Exception e) { Interlocked.Increment(ref _skupStumbles); Stumble("SkupPostfix", e); }
        }

        internal static void ApplySkup(Harmony h)
        {
            try
            {
                var m = AccessTools.Method(typeof(SellGoodsForTradeAction), "ApplyInternal");
                var ps = m != null ? m.GetParameters() : null;
                if (ps == null || ps.Length < 2 || ps[0].ParameterType != typeof(Settlement) || ps[1].ParameterType != typeof(MobileParty))
                {
                    Log.Info("Kiesa ludu (11c): BRAK SellGoodsForTradeAction.ApplyInternal(Settlement, MobileParty, ..) - robocizna od skupu plonu wsi nieczynna.");
                    return;
                }
                h.Patch(m, prefix: new HarmonyMethod(typeof(TownFolk), nameof(SkupPrefix)) { priority = Priority.First }, postfix: new HarmonyMethod(typeof(TownFolk), nameof(SkupPostfix)) { priority = Priority.Last });
                Log.Info("Kiesa ludu (11c): robocizna od skupu plonu wsi - latka wpieta (SellGoodsForTradeAction.ApplyInternal: prefiks Priority.First - przed prefiksem BK SellGoodsPatch, jak MarketCarts; postfiks Priority.Last - po zwrocie nadplaty MarketCarts); klucz Folk Earn From Village Buys czytany na zywo.");
            }
            catch (Exception e) { Log.Error("TownFolk.ApplySkup", e); }
        }

        /// <summary>Linia "Kiesa ludu": dopisek 11c do "robocizna" (pusty przy kluczu wylaczonym).</summary>
        private static string SkupText(long labS, long skup)
        {
            var s = Settings.Current;
            if (s == null || !s.FolkEarnFromVillageBuys) return "";
            long n = Interlocked.Exchange(ref _skupN, 0), st = Interlocked.Exchange(ref _skupStumbles, 0);
            return " (w tym od skupu plonu wsi (11c) " + labS + " = " + Unit(s.TownFolkLabourVillageBuys).ToString("0.00", Inv) + " x skup kas od taborow wsi " + skup + ", sprzedazy " + n
                   + (st > 0 ? ", potkniecia " + st : "") + ")";
        }
    }
}
