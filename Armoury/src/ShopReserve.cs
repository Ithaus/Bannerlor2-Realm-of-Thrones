using System;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// PACZKA 174b.4 - REZERWA KRAMU (docs/PROJEKT-174B-DOWOZ-2026-10-09.md rozdz. 3.4, rozdz. 8 pytanie 1 i "Krytyka i odpowiedzi" uwagi 13 i 21;
    /// wylacznik ShopKeepsLastArmour, liczba ShopKeepPieces = 1).
    /// Regula: w MIESCIE (zamek to nie targ - 171 C2.3) kupujacy HURTEM dla oddzialu nie zabiera ostatnich ShopKeepPieces sztuk (bez unikatow) pasma t1-2 /
    /// t3-4 / t5-6 zbroi: tulow, glowa, nogi, rece. Hurt: AiGear.BuyLoop (takze zamowienia zamkow na polce miasta), AiGear.BuySubstitutes, VolunteerKit.BuyCore
    /// (notable dla ochotnikow), MenPurse.BuyPlayerGaps (sakiewka ludzi GRACZA - jak ludzie lorda), SupplyDemand.DailyTrade (wywoz kupcow z polki zrodla).
    /// Zakup osobisty przy straganie (ekran handlu) - bez limitu. W grze kupuje tak tylko gracz, wiec to WYJATEK od "jednej reguly" (krytyka 21) - przywilej
    /// kupujacego osobiscie (prawo miejskie przeciw wykupywaniu - forestalling, engrossing - chronilo klienta z ulicy przed hurtownikiem); wariant "wedlug
    /// wielkosci zakupu" (takze gracz hurtem na ekranie handlu) wymaga latki ekranu handlu - pytanie 1 do Jeffa.
    /// Bron, tarcze i amunicja bez rezerwy. Cena bez zmian (Stock liczy cala polke - rezerwa nie zbija ceny przy niedoborze). Sygnal dla kowali zostaje:
    /// AiGear.OnShelf liczy tylko sztuki ponad rezerwe (zamowienie jak przy pustej polce); VolunteerKit - sztuka z rezerwy nie jest kandydatem (awans cofniety,
    /// osobny licznik). Licznik pasma z pamieci polki (ShelfIndex.Band) - bez dodatkowych przejsc; przy wylaczonej pamieci - przejscie polki.
    /// </summary>
    internal static class ShopReserve
    {
        internal static long Stumbles;      // poprawka 174b (recenzja): wyjatki od startu sesji - Log.Error tylko pierwszy (goraca sciezka zakupow), licznik w linii "Koszt 171-174 (doba)"
        private static bool _errLogged;

        /// <summary>Nowa gra / wczytanie (z ShelfIndex.Reset - konstruktor ArmouryBehavior).</summary>
        internal static void Reset() { Stumbles = 0; _errLogged = false; }
        /// <summary>Ile ostatnich sztuk pasma zostaje na straganie dla kupujacego osobiscie; 0 = rezerwa wylaczona.</summary>
        internal static int Pieces
        {
            get { var s = Settings.Current; return s != null && s.ShopKeepsLastArmour ? Math.Max(0, Math.Min(3, s.ShopKeepPieces)) : 0; }
        }

        private static int TierOf(ItemObject it) { try { return Math.Max(1, Math.Min(6, (int)it.Tier + 1)); } catch { return 1; } }

        private static bool ArmourType(ItemObject.ItemTypeEnum t)
        {
            return t == ItemObject.ItemTypeEnum.BodyArmor || t == ItemObject.ItemTypeEnum.HeadArmor || t == ItemObject.ItemTypeEnum.LegArmor || t == ItemObject.ItemTypeEnum.HandArmor;
        }

        /// <summary>Ile sztuk pasma tego przedmiotu kupujacy hurtem moze zdjac z polki targu (int.MaxValue = rezerwa nie dotyczy: wylaczona, zamek, nie zbroja, unikat).</summary>
        internal static int Free(Settlement market, ItemObject it)
        {
            if (it == null || !ArmourType(it.ItemType)) return int.MaxValue;
            try { if (ArmsPricing.IsUnique(it)) return int.MaxValue; } catch { }
            return FreeBand(market, it.ItemType, TierOf(it));
        }

        /// <summary>Jak Free, wedlug typu i tieru (pasmo tieru); unikaty nie licza sie do pasma.</summary>
        internal static int FreeBand(Settlement market, ItemObject.ItemTypeEnum type, int tier)
        {
            int keep = Pieces;
            if (keep <= 0 || market == null || !market.IsTown || market.ItemRoster == null || !ArmourType(type)) return int.MaxValue;
            long tc = Cost174.Begin(Cost174.SReserve);   // 174b.5 F6 (probka 1/16, tylko log)
            try
            {
                int band = Measure174b.Band(tier);
                int n = ShelfIndex.Band(market.ItemRoster, type, band);
                if (n < 0) n = Scan(market.ItemRoster, type, band);
                return Math.Max(0, n - keep);
            }
            catch (Exception e) { Stumbles++; if (!_errLogged) { _errLogged = true; Log.Error("ShopReserve.FreeBand", e); } return int.MaxValue; }   // poprawka 174b: raz w logu
            finally { Cost174.End(Cost174.SReserve, tc); }
        }

        private static int Scan(ItemRoster r, ItemObject.ItemTypeEnum type, int band)
        {
            int n = 0;
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                var it = el.EquipmentElement.Item;
                if (el.Amount <= 0 || it == null || it.ItemType != type || Measure174b.Band(TierOf(it)) != band || ArmsPricing.IsUnique(it)) continue;
                n += el.Amount;
            }
            return n;
        }
    }
}
