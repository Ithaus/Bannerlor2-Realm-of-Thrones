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
    /// roku - "pokoleniowa zima" to czysta fabula. Nasz kalendarz (rok 168
    /// dni) daje zime 42-dniowa co roku i te zime uzbrajamy:
    ///  - wojsko je wiecej (konsumpcja partii +50%),
    ///  - wioski nie rodza (produkcja -50%) - ceny zywnosci rosna SAME,
    ///    emergentnie, bo podaz wysycha,
    ///  - miasta przejadaja spichlerze szybciej - zimowe oblezenie z glodem
    ///    i nasza zaraza lamie twierdze jak w kronikach,
    ///  - jesienia AI gromadzi (cap zapasow BK x2) - kto nie zdazyl, gloduje,
    ///  - GRADIENT POLNOCY: im dalej na polnoc (posY mapy), tym zima gryzie
    ///    mocniej (+-25%): pod Winterfell przednowek, w Dorne lagodnie,
    ///  - DLUGA NOC: gdy Nocny Krol jest w polu, KAZDY dzien liczy sie jak
    ///    zimowy - Inni niosa zime ze soba (a sami nie jedza, wiec ich to
    ///    nie boli; ROT pomija umarlych w konsumpcji przed delegacja).
    /// Marszu zima nie tniemy - vanilla juz ma -10% na sniegu.
    /// Patche TYLKO na modelach bazowych (ROT/BK deleguja/postfixuja) -
    /// zero podwojnego mnozenia.
    /// </summary>
    internal static class WinterBite
    {
        private static readonly TextObject _txtWinter = new TextObject("{=!}Winter");
        private static int _lastSeason = -1;
        private static bool _longNight;

        internal static bool WinterNow()
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.WinterBiteEnabled) return false;
                if (_longNight && s.LongNightWithNK) return true;
                return CampaignTime.Now.GetSeasonOfYear == CampaignTime.Seasons.Winter;
            }
            catch { return false; }
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
                // Dluga Noc: Nocny Krol w polu = zima maszeruje z umarlymi
                bool nk = false;
                if (s.LongNightWithNK)
                {
                    foreach (var h in Hero.AllAliveHeroes)
                    {
                        if (h == null || h.Culture == null || h.Culture.StringId != "whitewalker") continue;
                        if (h.PartyBelongedTo != null) { nk = true; break; }
                    }
                }
                if (nk != _longNight)
                {
                    _longNight = nk;
                    Log.Player(nk ? "The Long Night falls - winter marches with the dead."
                                  : "Dawn breaks over the realm - the Long Night is over.", true);
                }

                int season = (int)CampaignTime.Now.GetSeasonOfYear;
                if (season != _lastSeason)
                {
                    _lastSeason = season;
                    if (CampaignTime.Now.GetSeasonOfYear == CampaignTime.Seasons.Winter)
                        Log.Player("Winter has come - the granaries will be tested.", true);
                    else if (CampaignTime.Now.GetSeasonOfYear == CampaignTime.Seasons.Spring)
                        Log.Player("The thaw - spring loosens winter's grip.", false);
                }
            }
            catch (Exception e) { Log.Error("WinterBite.OnDaily", e); }
        }

        /// <summary>Ile dni zapasow AI trzyma DZIS (jesienia gromadzi na zime).</summary>
        internal static int SupplyDaysCapNow()
        {
            var s = Settings.Current;
            int cap = s != null ? s.BkSupplyDaysCap : 4;
            try
            {
                if (s != null && s.WinterBiteEnabled
                    && CampaignTime.Now.GetSeasonOfYear == CampaignTime.Seasons.Autumn)
                    cap = (int)(cap * Math.Max(1f, s.AutumnStockMultiplier));
            }
            catch { }
            return cap;
        }

        // ---- patche ----

        /// <summary>Konsumpcja partii zima: +50% razy gradient polnocy.</summary>
        public static void FoodPostfix(MobileParty party, ref ExplainedNumber __result)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !WinterNow() || party == null) return;
                float bonus = Math.Max(0, s.WinterPartyFoodBonusPercent) / 100f * Northness(party.GetPosition2D.Y);
                if (bonus > 0f) __result.AddFactor(bonus, _txtWinter);
            }
            catch { }
        }

        /// <summary>Produkcja wioski zima: -50% razy gradient polnocy.</summary>
        public static void VillageProdPostfix(Village village, ref ExplainedNumber __result)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !WinterNow() || village == null || __result.ResultNumber <= 0f) return;
                float cut = Math.Max(0, Math.Min(90, s.WinterVillageOutputCutPercent)) / 100f
                          * Northness(village.Settlement.GetPosition2D.Y);
                __result.AddFactor(-MBMath.ClampFloat(cut, 0f, 0.95f), _txtWinter);
            }
            catch { }
        }

        public static void VillageFoodPostfix(Village village, ref float __result)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !WinterNow() || village == null || __result <= 0f) return;
                float cut = Math.Max(0, Math.Min(90, s.WinterVillageOutputCutPercent)) / 100f
                          * Northness(village.Settlement.GetPosition2D.Y);
                __result *= 1f - MBMath.ClampFloat(cut, 0f, 0.95f);
            }
            catch { }
        }

        /// <summary>Miasto zima: spichlerz topnieje szybciej (skala z prosperity).</summary>
        public static void TownFoodPostfix(Town town, ref ExplainedNumber __result)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !WinterNow() || town == null) return;
                float extra = town.Prosperity / 1000f * Math.Max(0f, s.WinterTownAppetitePer1000)
                            * Northness(town.Settlement.GetPosition2D.Y);
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
                         + s.NorthGradientPercent + "%, Dluga Noc " + (s.LongNightWithNK ? "TAK" : "nie") + ").");
            }
            catch (Exception e) { Log.Error("WinterBite.ApplyAll", e); }
        }
    }
}
