using System;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;

namespace CrashScribe
{
    /// <summary>
    /// B10b (docs/ERRORS.md A7 klasa 7; DECYZJA JEFFA 14.10 "tak napraw") - ATUTY BK Z BITEW. Klasa BK
    /// VanillaModelTweakPatches+BKBattleRewardTweakPatches w 1.4.8 pada na postfiksie CalculateInfluenceGain (nazwy parametrow
    /// party / influenceValueOfBattle / contributionShare - w 1.4.8 winnerParty / ...ForWinnerSide / ...OfWinnerParty), a przerwanie
    /// klasy zabiera tez postfiks CalculateRenownGain. Lup (GetLootedItemFromTroop, pierwsze zadanie klasy) dziala - NIE ruszamy
    /// (ponowne CreateClassProcessor na klasie BK podwoiloby postfiks lupu: LootScale 0.5 -> 0.25).
    /// Tu te same dwa postfiksy co w BK, z parametrami pozycyjnymi (__0 = winnerParty), na DefaultBattleRewardModel (lancuch gry:
    /// NavalDLCBattleRewardModel -> BaseModel = DefaultBattleRewardModel, log Armoury "model lupu ... dochodzi do DefaultBattleRewardModel"):
    ///  - CalculateInfluenceGain: dowodca z atutem BK Commander "Warband" - wplyw z bitwy x1.25;
    ///  - CalculateRenownGain: atut Mercenary "Famous Sellswords" - slawa x1.20; styl zycia Cataphract - slawa x1.12.
    /// Wartosci i warunki 1:1 z BK (VanillaModelTweakPatches.cs). Dotyczy gracza i AI. Typy BK przez refleksje (CrashScribe nie
    /// odwoluje sie do BannerKings.dll), rozwiazywane leniwie przy pierwszym uzyciu (obiekty BK istnieja dopiero w kampanii).
    /// Wpiecie raz na proces przy pierwszym OnSessionLaunched (po BK Main.OnGameStart), z kontrola Harmony.GetPatchInfo: gdy postfiks
    /// wlasciciela "BannerKings" juz jest na metodzie (BK po aktualizacji sam to naprawil) - nie wpinamy (premia liczylaby sie 2 razy).
    /// Klucz BkBattlePerksFix (domyslnie true; false = stan sprzed, atuty bez dzialania). Wyjatek w postfiksie - wynik gry bez zmian.
    /// </summary>
    internal static class BkBattlePerks
    {
        private const string HarmonyId = "com.jeff.crashscribe";
        private static Harmony _harmony;
        private static bool _hooksTried, _resolved, _resolveFailed;
        private static string _state = "nie wpiete";

        // refleksja BK (leniwie)
        private static PropertyInfo _cfgInstance, _eduManager, _lifestyle;
        private static MethodInfo _getEducation, _hasPerk;
        private static object _perkWarband, _perkSellswords, _lsCataphract;
        private static TextObject _nWarband, _nSellswords, _nCataphract;

        private static int _dInfl, _dRenS, _dRenC, _tInfl, _tRenS, _tRenC, _stumbles;

        private static void Stumble(string where, Exception e)
        {
            int n = Interlocked.Increment(ref _stumbles);
            if (n <= 5 || n % 100 == 0) { try { Scribe.Report("CrashScribe", e, "BkBattlePerks." + where + " (potkniecie " + n + ")", null); } catch { } }
        }

        internal static void ResetSession() { _dInfl = _dRenS = _dRenC = 0; _tInfl = _tRenS = _tRenC = 0; }

        internal static void OnSession()
        {
            try { EnsureHooks(); } catch (Exception e) { _state = "wyjatek " + e.GetType().Name; Stumble("EnsureHooks", e); }
            try
            {
                Scribe.Line("BK atuty bitew (B10b): " + _state + " | klucz BkBattlePerksFix " + (Config.BkBattlePerksFix ? "TAK" : "NIE")
                            + " | wplyw z bitwy x1.25 (Commander Warband), slawa x1.20 (Famous Sellswords), x1.12 (styl Cataphract) - jak w BK; lup BK bez zmian.");
            }
            catch (Exception e) { Stumble("StartLine", e); }
        }

        private static void EnsureHooks()
        {
            if (_hooksTried) return;
            _hooksTried = true;
            if (!Config.BkBattlePerksFix) { _state = "WYLACZONE (klucz)"; return; }
            if (AccessTools.TypeByName("BannerKings.BannerKingsConfig") == null) { _state = "BRAK (BannerKings nie zaladowany)"; return; }
            var mInfl = AccessTools.Method(typeof(DefaultBattleRewardModel), "CalculateInfluenceGain");
            var mRen = AccessTools.Method(typeof(DefaultBattleRewardModel), "CalculateRenownGain");
            if (mInfl == null || mRen == null) { _state = "BRAK (metody DefaultBattleRewardModel)"; return; }
            if (HasBkPostfix(mInfl) || HasBkPostfix(mRen)) { _state = "NIE wpiete - BK ma juz postfiks (aktualizacja BK?), premia liczylaby sie 2 razy"; return; }
            if (_harmony == null) _harmony = new Harmony(HarmonyId);
            int ok = 0;
            try { _harmony.Patch(mInfl, postfix: new HarmonyMethod(typeof(BkBattlePerks), nameof(InfluencePostfix))); ok++; }
            catch (Exception e) { Stumble("Patch(CalculateInfluenceGain)", e); }
            try { _harmony.Patch(mRen, postfix: new HarmonyMethod(typeof(BkBattlePerks), nameof(RenownPostfix))); ok++; }
            catch (Exception e) { Stumble("Patch(CalculateRenownGain)", e); }
            _state = "wpiete " + ok + "/2" + (ok == 2 ? "" : " (BRAK - patrz raport)");
        }

        private static bool HasBkPostfix(MethodBase m)
        {
            try
            {
                var info = Harmony.GetPatchInfo(m);
                if (info == null) return false;
                foreach (var p in info.Postfixes) if (p != null && p.owner == "BannerKings") return true;
            }
            catch { }
            return false;
        }

        private static bool Resolve()
        {
            if (_resolved) return true;
            if (_resolveFailed) return false;
            try
            {
                var cfgT = AccessTools.TypeByName("BannerKings.BannerKingsConfig");
                var perksT = AccessTools.TypeByName("BannerKings.Managers.Skills.BKPerks");
                var lsT = AccessTools.TypeByName("BannerKings.Managers.Education.Lifestyles.DefaultLifestyles");
                var eduMgrT = AccessTools.TypeByName("BannerKings.Managers.EducationManager");
                var eduT = AccessTools.TypeByName("BannerKings.Managers.Education.EducationData");
                _cfgInstance = cfgT != null ? AccessTools.Property(cfgT, "Instance") : null;
                _eduManager = cfgT != null ? AccessTools.Property(cfgT, "EducationManager") : null;
                _getEducation = eduMgrT != null ? AccessTools.Method(eduMgrT, "GetHeroEducation", new[] { typeof(Hero) }) : null;
                _hasPerk = eduT != null ? AccessTools.Method(eduT, "HasPerk") : null;
                _lifestyle = eduT != null ? AccessTools.Property(eduT, "Lifestyle") : null;
                object perks = perksT != null ? StaticInstance(perksT) : null;
                object ls = lsT != null ? StaticInstance(lsT) : null;
                _perkWarband = perks != null ? Traverse.Create(perks).Property("CommanderWarband").GetValue() : null;
                _perkSellswords = perks != null ? Traverse.Create(perks).Property("MercenaryFamousSellswords").GetValue() : null;
                _lsCataphract = ls != null ? Traverse.Create(ls).Property("Cataphract").GetValue() : null;
                _nWarband = NameOf(_perkWarband);
                _nSellswords = NameOf(_perkSellswords);
                _nCataphract = NameOf(_lsCataphract);
                if (_cfgInstance == null || _eduManager == null || _getEducation == null || _hasPerk == null || _lifestyle == null
                    || _perkWarband == null || _perkSellswords == null)
                {
                    _resolveFailed = true;
                    Scribe.Line("BK atuty bitew (B10b): BRAK typow / atutow BK - premie nie dzialaja (cfg " + (_cfgInstance != null) + ", edukacja "
                                + (_getEducation != null) + ", HasPerk " + (_hasPerk != null) + ", Warband " + (_perkWarband != null) + ", Sellswords "
                                + (_perkSellswords != null) + ", Cataphract " + (_lsCataphract != null) + ").");
                    return false;
                }
                _resolved = true;
                return true;
            }
            catch (Exception e) { _resolveFailed = true; Stumble("Resolve", e); return false; }
        }

        private static object StaticInstance(Type t)
        {
            // DefaultTypeInitializer<T, X>.Instance - statyczna wlasciwosc w generycznej klasie bazowej
            for (var b = t; b != null; b = b.BaseType)
            {
                var p = b.GetProperty("Instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly);
                if (p != null) return p.GetValue(null, null);
            }
            return null;
        }

        private static TextObject NameOf(object o)
        {
            try { var n = o != null ? Traverse.Create(o).Property("Name").GetValue() as TextObject : null; return n ?? new TextObject("{=!}BK"); }
            catch { return new TextObject("{=!}BK"); }
        }

        private static object EducationOf(Hero h)
        {
            object cfg = _cfgInstance.GetValue(null, null);
            object mgr = cfg != null ? _eduManager.GetValue(cfg, null) : null;
            return mgr != null ? _getEducation.Invoke(mgr, new object[] { h }) : null;
        }

        /// <summary>Postfiks DefaultBattleRewardModel.CalculateInfluenceGain (__0 = winnerParty) - jak BK CalculateInfluenceGainPostfix.</summary>
        public static void InfluencePostfix(PartyBase __0, ref ExplainedNumber __result)
        {
            try
            {
                if (!Config.BkBattlePerksFix) return;
                var hero = __0 != null ? __0.LeaderHero : null;
                if (hero == null || !Resolve()) return;
                var edu = EducationOf(hero);
                if (edu == null) return;
                if ((bool)_hasPerk.Invoke(edu, new[] { _perkWarband }))
                {
                    __result.AddFactor(0.25f, _nWarband);
                    _dInfl++; _tInfl++;
                }
            }
            catch (Exception e) { Stumble("InfluencePostfix", e); }
        }

        /// <summary>Postfiks DefaultBattleRewardModel.CalculateRenownGain (__0 = winnerParty) - jak BK CalculateRenownGainPostfix.</summary>
        public static void RenownPostfix(PartyBase __0, ref ExplainedNumber __result)
        {
            try
            {
                if (!Config.BkBattlePerksFix) return;
                var hero = __0 != null ? __0.LeaderHero : null;
                if (hero == null || !Resolve()) return;
                var edu = EducationOf(hero);
                if (edu == null) return;
                if ((bool)_hasPerk.Invoke(edu, new[] { _perkSellswords }))
                {
                    __result.AddFactor(0.2f, _nSellswords);
                    _dRenS++; _tRenS++;
                }
                var ls = _lifestyle.GetValue(edu, null);
                if (ls != null && _lsCataphract != null && ls.Equals(_lsCataphract))
                {
                    __result.AddFactor(0.12f, _nCataphract);
                    _dRenC++; _tRenC++;
                }
            }
            catch (Exception e) { Stumble("RenownPostfix", e); }
        }

        internal static void Daily()
        {
            try
            {
                if (!Config.BkBattlePerksFix) return;
                Scribe.Line("BK atuty bitew (B10b): dzien " + (int)CampaignTime.Now.ToDays + " | Warband wplyw x1.25: " + _dInfl + ", Famous Sellswords slawa x1.20: "
                            + _dRenS + ", Cataphract slawa x1.12: " + _dRenC + " | od startu sesji " + _tInfl + " / " + _tRenS + " / " + _tRenC
                            + " | potkniecia " + _stumbles + ".");
            }
            catch (Exception e) { Stumble("Daily", e); }
            _dInfl = _dRenS = _dRenC = 0;
        }
    }

    /// <summary>B10b (14.10): wpiecie atutow BK z bitew przy starcie sesji i linia doby. Bez zapisu.</summary>
    internal sealed class BkBattlePerksBehavior : CampaignBehaviorBase
    {
        public BkBattlePerksBehavior() { BkBattlePerks.ResetSession(); }

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, delegate (CampaignGameStarter s) { BkBattlePerks.OnSession(); });
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, delegate { BkBattlePerks.Daily(); });
        }

        public override void SyncData(IDataStore dataStore) { }
    }
}
