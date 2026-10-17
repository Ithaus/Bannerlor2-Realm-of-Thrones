using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// ZIMA Z ZEBAMI (Jeff 31.08, "rob"). ROT ma mechanicznie zwykle 4 pory
    /// roku - "pokoleniowa zima" to czysta fabula. Nasz kalendarz (rok 364
    /// dni) daje zime 91-dniowa co roku i te zime uzbrajamy:
    ///  - wojsko je wiecej (konsumpcja partii +50%),
    ///  - wioski nie rodza (produkcja -50%) - ceny zywnosci rosna SAME,
    ///    emergentnie, bo podaz wysycha,
    ///  - miasta przejadaja spichlerze szybciej - zimowe oblezenie z glodem
    ///    i nasza zaraza lamie twierdze jak w kronikach,
    ///  - jesienia AI gromadzi (cap zapasow BK x2) - kto nie zdazyl, gloduje,
    ///  - GRADIENT POLNOCY: im dalej na polnoc (posY mapy), tym zima gryzie
    ///    mocniej (+-25%): pod Winterfell przednowek, w Dorne lagodnie.
    ///  - E4/1 (189) ZIMA WEDLUG KRAIN (WinterRealm): kazdy z czterech skutkow razy czynnik krainy regionu - Wyspy Letnie 0,
    ///    cieple poludnie (Dorne, Qarth, Zatoka, Lys, Tyrosh, Myr, Volantis, Valyria) 0.4, reszta 1; konie zima doplacaja pasze.
    /// DLUGA NOC USUNIETA (wpis 99, Jeff 05.10: "wywal dluga noc"): warunek "zywy
    /// bohater kultury whitewalker w partii" byl prawda od pierwszej doby kazdej
    /// kampanii ROT (Inni maja partie od startu), wiec caly swiat mial wieczna zime:
    /// wsie -38..-62% produkcji, wojsko +50% jedzenia. Zima jest teraz tylko z kalendarza.
    /// Marszu zima nie tniemy - vanilla juz ma -10% na sniegu.
    /// Patche TYLKO na modelach bazowych (ROT/BK deleguja/postfixuja) -
    /// zero podwojnego mnozenia.
    /// </summary>
    internal static class WinterBite
    {
        private static readonly TextObject _txtWinter = new TextObject("{=!}Winter");
        private static readonly TextObject _txtHorses = new TextObject("{=!}Horses eat fodder in winter");   // E4/1
        private static int _lastSeason = -1;

        internal static bool WinterNow()
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.WinterBiteEnabled) return false;
                return WesterosClimate.Now() == CampaignTime.Seasons.Winter;
            }
            catch { return false; }
        }

        /// <summary>N2-E4 (opal mieszczan, poprawka po recenzji 16.10): gradient polnocy w polozeniu osady - ten sam mnoznik, ktorym zima mnozy kazdy swoj skutek. 1 bez osady.</summary>
        internal static float NorthOf(Settlement st)
        {
            if (st == null) return 1f;
            return Northness(st.GetPosition2D.Y);
        }

        /// <summary>Mnoznik geografii: polnoc (wysokie Y) ~1.25, Dorne ~0.75.</summary>
        private static float Northness(float y)
        {
            var s = Settings.Current;
            float g = s != null ? Math.Max(0, Math.Min(60, s.NorthGradientPercent)) / 100f : 0.25f;
            float n = MBMath.ClampFloat((y - 100f) / 1000f, 0f, 1f);
            return 1f + (n - 0.5f) * 2f * g;
        }

        internal static void OnDaily()
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.WinterBiteEnabled) return;

                int season = (int)WesterosClimate.Now();
                if (season != _lastSeason)
                {
                    _lastSeason = season;
                    if (WesterosClimate.Now() == CampaignTime.Seasons.Winter)
                        Log.Player("Winter has come - the granaries will be tested.", true);
                    else if (WesterosClimate.Now() == CampaignTime.Seasons.Spring)
                        Log.Player("The thaw - spring loosens winter's grip.", false);
                }
            }
            catch (Exception e) { Log.Error("WinterBite.OnDaily", e); }
            try { WinterRealm.Daily(); } catch (Exception e) { Log.Error("WinterRealm.Daily", e); }   // E4/1: pamiec doby regionu partii; w zimie linia "Zima wedlug krain (E4/1)"
        }

        /// <summary>Ile dni zapasow AI trzyma DZIS (jesienia gromadzi na zime).</summary>
        internal static int SupplyDaysCapNow()
        {
            var s = Settings.Current;
            int cap = s != null ? s.BkSupplyDaysCap : 4;
            try
            {
                if (s != null && s.WinterBiteEnabled
                    && WesterosClimate.Now() == CampaignTime.Seasons.Autumn)
                    cap = (int)(cap * Math.Max(1f, s.AutumnStockMultiplier));
            }
            catch { }
            return cap;
        }

        /// <summary>Ciecie plonu wsi zima: procent x gradient polnocy x czynnik krainy (E4/1, WinterRealm) - jeden rachunek dla produkcji wsi,
        /// zywnosci wsi i odczytu E4/0. Wolac tylko w zimie.</summary>
        private static float CutOf(Settings s, Village village)
        {
            float cut = Math.Max(0, Math.Min(90, s.WinterVillageOutputCutPercent)) / 100f
                      * Northness(village.Settlement.GetPosition2D.Y) * WinterRealm.OfVillage(village);
            return MBMath.ClampFloat(cut, 0f, 0.95f);
        }

        /// <summary>E4/0 (pomiar, tylko odczyt): ciecie plonu wsi dzis - ten sam rachunek co VillageProdPostfix / VillageFoodPostfix (0 poza zima).
        /// Uwaga dla E4/4 (109 dodawal identyczna metode): NIE dublowac - od E4/1 z czynnikiem krainy (CutOf).</summary>
        internal static float VillageCut(Village village)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !WinterNow() || village == null || village.Settlement == null) return 0f;
                return CutOf(s, village);
            }
            catch { return 0f; }
        }

        /// <summary>KS (kalibracja startu): ciecie plonu pol mieszczan zima - ten sam procent co wsi x gradient polnocy w polozeniu miasta x czynnik krainy miasta
        /// (WinterRealm.OfTown). 0 poza zima. Nie rzuca.</summary>
        internal static float TownCut(Town town)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !WinterNow() || town == null || town.Settlement == null) return 0f;
                float cut = Math.Max(0, Math.Min(90, s.WinterVillageOutputCutPercent)) / 100f * Northness(town.Settlement.GetPosition2D.Y) * WinterRealm.OfTown(town);
                return MBMath.ClampFloat(cut, 0f, 0.95f);
            }
            catch { return 0f; }
        }

        /// <summary>E4/0 (pomiar, tylko odczyt): dodatek jedzenia partii zima - ten sam rachunek co FoodPostfix (0 poza zima; od E4/1 z czynnikiem krainy).</summary>
        internal static float PartyFoodBonus(MobileParty party)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !WinterNow() || party == null) return 0f;
                return Math.Max(0f, Math.Max(0, s.WinterPartyFoodBonusPercent) / 100f * Northness(party.GetPosition2D.Y) * WinterRealm.OfParty(party));
            }
            catch { return 0f; }
        }

        /// <summary>Dodatkowy ubytek spichlerza warowni zima (dodatni, 0 poza zima) - ten sam rachunek co TownFoodPostfix (od E4/1 z czynnikiem krainy).</summary>
        internal static float TownExtra(Town town)
        {
            var s = Settings.Current;
            if (s == null || !WinterNow() || town == null || town.Settlement == null) return 0f;
            return Math.Max(0f, town.Prosperity / 1000f * Math.Max(0f, s.WinterTownAppetitePer1000)
                                * Northness(town.Settlement.GetPosition2D.Y) * WinterRealm.OfTown(town));
        }

        // ---- patche ----

        /// <summary>Konsumpcja partii zima: +50% razy gradient polnocy razy czynnik krainy pod partia (E4/1); konie doplacaja pasze (E4/1,
        /// WinterRealm.HorseFeed - w bazie, wiec mnozniki zimy, perkow i Rations obejmuja ja jak ludzi). Pasze liczy WinterRealm sam (konni po
        /// glowie, zwierzeta taboru, stawki BK) - niezaleznie od tego, ktory model jedzenia jest aktywny (u Jeffa RBL, nie BK).
        /// E4/6a (FoodChain): gdy RBL oddaje baze lancuchowi, ta metoda biegnie dwa razy - w najglebszym modelu (BK dziedziczy ja z Default)
        /// i na zewnatrz (RBL, wynik juz z lancucha); zima liczy sie tylko w najglebszym, zewnetrzny poziom pomijamy (FoodChain.BaseFromChain).</summary>
        public static void FoodPostfix(DefaultMobilePartyFoodConsumptionModel __instance, MobileParty party, ref ExplainedNumber __result)
        {
            try
            {
                if (FoodChain.BaseFromChain(__instance)) return;
                var s = Settings.Current;
                if (s == null || !WinterNow() || party == null) return;
                float realm = WinterRealm.OfParty(party);
                int why, riders, herd; float need, bk;
                float horse = WinterRealm.HorseFeed(party, realm, out why, out need, out bk, out riders, out herd);   // na pustyni / w sniegu przy paszy BK - tylko roznica
                if (horse > 0f) __result.Add(-horse, _txtHorses);
                float bonus = Math.Max(0, s.WinterPartyFoodBonusPercent) / 100f * Northness(party.GetPosition2D.Y) * realm;
                if (bonus > 0f) __result.AddFactor(bonus, _txtWinter);
            }
            catch { }
        }

        /// <summary>Produkcja wioski zima: -50% razy gradient polnocy razy czynnik krainy (E4/1).</summary>
        public static void VillageProdPostfix(Village village, ref ExplainedNumber __result)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !WinterNow() || village == null || village.Settlement == null || __result.ResultNumber <= 0f) return;
                float cut = CutOf(s, village);
                if (cut > 0f) __result.AddFactor(-cut, _txtWinter);
            }
            catch { }
        }

        public static void VillageFoodPostfix(Village village, ref float __result)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !WinterNow() || village == null || village.Settlement == null || __result <= 0f) return;
                __result *= 1f - CutOf(s, village);
            }
            catch { }
        }

        /// <summary>Miasto zima: spichlerz topnieje szybciej (skala z prosperity; od E4/1 razy czynnik krainy).</summary>
        public static void TownFoodPostfix(Town town, ref ExplainedNumber __result)
        {
            try
            {
                float extra = TownExtra(town);
                if (extra > 0f) __result.Add(-extra, _txtWinter);
            }
            catch { }
        }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.WinterBiteEnabled) { Log.Info("WinterBite: wylaczone."); return; }
                var mFood = AccessTools.Method(typeof(DefaultMobilePartyFoodConsumptionModel), "CalculateDailyBaseFoodConsumptionf");
                var mProd = AccessTools.Method(typeof(DefaultVillageProductionCalculatorModel), "CalculateDailyProductionAmount");
                var mVFood = AccessTools.Method(typeof(DefaultVillageProductionCalculatorModel), "CalculateDailyFoodProductionAmount");
                var mTown = AccessTools.Method(typeof(DefaultSettlementFoodModel), "CalculateTownFoodStocksChange");
                if (mFood != null) h.Patch(mFood, postfix: new HarmonyMethod(typeof(WinterBite), "FoodPostfix"));
                if (mProd != null) h.Patch(mProd, postfix: new HarmonyMethod(typeof(WinterBite), "VillageProdPostfix"));
                if (mVFood != null) h.Patch(mVFood, postfix: new HarmonyMethod(typeof(WinterBite), "VillageFoodPostfix"));
                if (mTown != null) h.Patch(mTown, postfix: new HarmonyMethod(typeof(WinterBite), "TownFoodPostfix"));
                Log.Info("WinterBite: zima uzbrojona (jedzenie +" + s.WinterPartyFoodBonusPercent
                         + "%, wioski -" + s.WinterVillageOutputCutPercent + "%, gradient polnocy "
                         + s.NorthGradientPercent + "%; tylko zima z kalendarza - Dluga Noc usunieta wpisem 99; " + WinterRealm.StartText() + ").");
            }
            catch (Exception e) { Log.Error("WinterBite.ApplyAll", e); }
        }
    }
}
