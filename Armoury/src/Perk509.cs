using System;
using System.Threading;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// 509 (perki, punkty 9, 56, 59; Jeff 14.11 "9 Confidence do BuildFunding: ok", "56 SelfMadeMan (do BuildFunding)", "59 ArchitecturalCommisions (placi kasa miasta): ok").
    ///
    /// 9 Confidence / 56 SelfMadeMan (klucz PerkBuildSpeedOnPaidWork; wymaga Engineering On Paid Work, bo idzie przez BuildFunding.EngExtra):
    ///   GRA: DefaultBuildingConstructionModel - AddPerkBonusForTown(TwoHanded.Confidence) dla IsMilitaryProject (+30%, AddFactor, zarzadca na miejscu) i
    ///   AddPerkBonusForTown(Trade.SelfMadeMan) dla targu (+30%). BKConstructionModel: Confidence dla mur, koszar, placu cwiczen, strazni, warsztatu oblezniczego
    ///   (miasto i zamek), SelfMadeMan dla targu i wodociagu. PowerPostfix (BuildFunding.cs) podmienia wynik modelu na punkty pana - obie premie przepadaly
    ///   w osadach objetych rozliczeniem (audyt PERKI.md: BuildFunding.cs:154-178). Opis gry SelfMadeMan mowi "marketplace, kiln and aqueduct" - wypalarnia
    ///   (lime kilns) w 1.4.8 juz nie istnieje (Building.cs: building_settlement_lime_kilns -> roads_and_paths), wiec lista jak BK: targ i wodociag.
    ///   ZMIANA: w EngExtra (ta sama podstawa i ten sam warunek obecnosci zarzadcy co Carpenters / Military Planner / Stonecutters) AddFactor perka -
    ///   Confidence: IsMilitaryProject gry albo lista BK; SelfMadeMan: targ albo wodociag. Lenna gracza bez rozliczenia ida modelem BK, ktory to juz ma - bez dubla.
    ///
    /// 59 ArchitecturalCommisions (klucz ArchitecturalCommissionsTownPays):
    ///   GRA/BK: BK VillageIncomePrefix (prefiks zwracajacy false na DefaultClanFinanceModel.AddSettlementIncome) dodaje rodowi 20 zl/dobe "Settlement Projects"
    ///   za kazde lenno z projektem dziennym i zarzadca z perkiem (bez warunku obecnosci) - zloto z niczego (model gry CalculateTownIncomeFromProjects: to samo).
    ///   ZMIANA: postfiks na BK VillageIncomePrefix (DeclaredMethod, w kampanii - klasa ma pole statyczne z modelem finansow, pulapka CLAUDE.md 7), tylko gdy BK naprawde
    ///   policzyl (prefiks zwrocil false): te 20 zl placi KASA OSADY (town.Gold) - przy wyplacie (applyWithdrawals) town.ChangeGold(-zaplacone); czego kasa nie ma,
    ///   tego rod nie dostaje (pozycja "Settlement projects (town treasury short)" na minus, takze w podgladzie). Jeden platnik (kasa osady), jeden odbiorca (rod).
    ///   Bez BK (prefiks BK nie dziala) - model gry bez zmian (nie u Jeffa).
    /// Potkniecia liczone, bez globalnego wylacznika. Linia doby - czesc linii "Praca jencow (509)".
    /// </summary>
    internal static class Perk509
    {
        private const string HarmonyId = "com.jeff.armoury.perk509";
        private static bool _tried, _archWired;
        internal static string Note = "nie zakladany";
        private static int _stumbles;
        private static bool _errLogged;
        private static TextObject _txtShort;

        // liczniki doby (zbierane w bloku dobowym - EngExtra z count, i przy wyplacie ArchitecturalCommisions)
        private static int _dConf, _dSelf, _dArchN, _dArchShortN;
        private static long _dArchPaid, _dArchShort;

        internal static bool PerksOn { get { var s = Settings.Current; return s != null && s.PerkBuildSpeedOnPaidWork; } }
        internal static bool ArchOn { get { var s = Settings.Current; return s != null && s.ArchitecturalCommissionsTownPays; } }

        private static void Stumble(string where, Exception e)
        {
            Interlocked.Increment(ref _stumbles);
            if (_errLogged) return;
            _errLogged = true;
            try { Log.Error("Perk509." + where, e); } catch { }
        }

        private static bool Military(BuildingType bt)
        {
            if (bt.IsMilitaryProject) return true;
            return bt == DefaultBuildingTypes.CastleFortifications || bt == DefaultBuildingTypes.SettlementFortifications || bt == DefaultBuildingTypes.CastleBarracks
                || bt == DefaultBuildingTypes.SettlementBarracks || bt == DefaultBuildingTypes.CastleTrainingFields || bt == DefaultBuildingTypes.SettlementTrainingFields
                || bt == DefaultBuildingTypes.CastleGuardHouse || bt == DefaultBuildingTypes.SettlementGuardHouse || bt == DefaultBuildingTypes.CastleSiegeWorkshop
                || bt == DefaultBuildingTypes.SettlementSiegeWorkshop;
        }

        /// <summary>Wolane z BuildFunding.EngExtra (zarzadca zywy i na miejscu, czolo kolejki bt): Confidence i SelfMadeMan jako AddFactor. Czyste poza licznikami (count).</summary>
        internal static void BuildPerks(Town town, Hero g, BuildingType bt, ref ExplainedNumber e, bool count)
        {
            if (!PerksOn || town == null || g == null || bt == null) return;
            try
            {
                var conf = DefaultPerks.TwoHanded.Confidence;
                if (conf != null && Military(bt) && g.GetPerkValue(conf))
                {
                    e.AddFactor(conf.SecondaryBonus, null);
                    if (count) _dConf++;
                }
                var self = DefaultPerks.Trade.SelfMadeMan;
                if (self != null && (bt == DefaultBuildingTypes.SettlementMarketplace || bt == DefaultBuildingTypes.SettlementWaterworks) && g.GetPerkValue(self))
                {
                    e.AddFactor(self.SecondaryBonus, null);
                    if (count) _dSelf++;
                }
            }
            catch (Exception ex) { Stumble("BuildPerks", ex); }
        }

        // ------------------------------------------------------------ 59 ArchitecturalCommisions
        /// <summary>Postfiks na BK ClanFinancesPatches.VillageIncomePrefix: 20 zl "Settlement Projects" placi kasa osady. __result true = BK nie liczyl (nic).</summary>
        public static void ArchPostfix(Clan clan, ref ExplainedNumber goldChange, bool applyWithdrawals, bool __result)
        {
            if (__result || !_archWired || clan == null) return;
            try
            {
                if (!ArchOn) return;
                var perk = DefaultPerks.Engineering.ArchitecturalCommisions;
                if (perk == null) return;
                int shortAll = 0;
                foreach (var t in clan.Fiefs)
                {
                    try
                    {
                        if (t == null || t.CurrentDefaultBuilding == null || t.Governor == null || !t.Governor.GetPerkValue(perk)) continue;   // warunek BK co do znaku
                        int due = (int)perk.SecondaryBonus;
                        if (due <= 0) continue;
                        int pay = Math.Max(0, Math.Min(due, t.Gold));
                        if (applyWithdrawals)
                        {
                            if (pay > 0) t.ChangeGold(-pay);
                            _dArchN++; _dArchPaid += pay;
                            if (pay < due) { _dArchShortN++; _dArchShort += due - pay; }
                        }
                        shortAll += due - pay;
                    }
                    catch (Exception e) { Stumble("ArchPostfix(lenno)", e); }
                }
                if (shortAll > 0)
                {
                    if (_txtShort == null) _txtShort = new TextObject("{=!}Settlement projects (town treasury short)");
                    goldChange.Add(-shortAll, _txtShort);
                }
            }
            catch (Exception e) { Stumble("ArchPostfix", e); }
        }

        /// <summary>OnSessionLaunched (raz na proces): postfiks na BK VillageIncomePrefix - w kampanii (pole statyczne klasy BK z modelem finansow, CLAUDE.md 7).</summary>
        internal static void EnsureHooks()
        {
            if (_tried) return;
            _tried = true;
            try
            {
                var t = AccessTools.TypeByName("BannerKings.Patches.EconomyPatches+ClanFinancesPatches")
                        ?? AccessTools.TypeByName("BannerKings.Patches.EconomyPatches.ClanFinancesPatches");
                var m = t != null ? AccessTools.DeclaredMethod(t, "VillageIncomePrefix") : null;
                if (t == null) Note = "bez BK (brak ClanFinancesPatches) - model gry bez zmian";
                else if (m == null || m.ReturnType != typeof(bool)) Note = "BRAK (ClanFinancesPatches.VillageIncomePrefix)";
                else
                {
                    new Harmony(HarmonyId).Patch(m, postfix: new HarmonyMethod(typeof(Perk509), nameof(ArchPostfix)));
                    bool ours = false;
                    var info = Harmony.GetPatchInfo(m);
                    if (info != null) foreach (var p in info.Postfixes) if (p != null && p.owner == HarmonyId) ours = true;
                    _archWired = ours;
                    Note = ours ? "postfiks BK VillageIncomePrefix wpiety" : "BRAK (latka nie weszla)";
                }
            }
            catch (Exception e) { _archWired = false; Note = "BRAK (wyjatek " + e.GetType().Name + ")"; Stumble("EnsureHooks", e); }
        }

        /// <summary>Fragment linii doby (PrisonLabor509.Daily) - liczniki od ostatniej linii, potem zero.</summary>
        internal static string DayLine()
        {
            string r = "Confidence przy oplaconej budowie " + _dConf + " osad, SelfMadeMan " + _dSelf + "; Architectural Commissions: wyplat " + _dArchN + ", z kasy osad " + _dArchPaid
                       + " zl, brak w kasie " + _dArchShortN + " (" + _dArchShort + " zl nie wyplacone)" + (_stumbles > 0 ? ", potkniecia " + _stumbles : "");
            _dConf = _dSelf = _dArchN = _dArchShortN = 0; _dArchPaid = _dArchShort = 0; _stumbles = 0;
            return r;
        }
    }
}
