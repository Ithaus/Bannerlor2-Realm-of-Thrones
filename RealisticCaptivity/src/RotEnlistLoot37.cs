using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;

namespace RealisticCaptivity
{
    /// <summary>
    /// KROK 3.7 (PLAN-DO-KONCA-MODA-2026-10-09, etap 3 - szczelnosc): wycena lorda w lupie zaciagnietego gracza (ROT) bez okupu, ktorego nikt nie placi.
    /// ROT (EnlistmentPatches.patch17) przy zaciagu pomija pojmanie bohaterow (stan LootParty zamiast CaptureHeroes), a patch18 (prefiks
    /// PlayerEncounter.DoLootMembersAndPrisonersOfParty, zwraca false) wycenia udzial gracza: ludzie po koszcie werbunku, jency po
    /// RansomValueCalculationModel.PrisonerRansomValue(jeniec, gracz) - takze LORDOWIE, ktorzy zostali w liscie jencow. Patch19 zamienia te wartosc
    /// na do 3 losowych przedmiotow z niczego. Dla lorda nasza cena (FairRansomPatch.LordPrice: Armoury 178 - gotowka, ktora rod odda, albo cena wedlug
    /// pozycji RC) zaklada platnika (rod jenca) - tu nikt nie placi, jeniec do gracza nie trafia. Wiec w tej jednej wycenie liczymy cene gry
    /// (_vanillaOnly - ta sama flaga, ktorej RC uzywa do liczenia ceny gry przy ekranie druzyny). Reszta lupu zaciagu (wymiana lupu na przedmioty,
    /// koszt werbunku ludzi) to mechanika ROT - bez zmian.
    /// Prefiks z priorytetem First (przed ROT) + finalizer (biegnie zawsze, takze gdy ROT pomija oryginal). Poza zaciagiem nic nie robi.
    /// Wylacznik RotEnlistLootGamePrice. Wyjatki liczone (potkniecia), pierwszy do logu.
    /// </summary>
    internal static class RotEnlistLoot37
    {
        private static bool _lookupDone, _errLogged;
        private static FieldInfo _behField, _isEnlistedField;
        private static int _stumbles;

        /// <summary>Czy gracz jest zaciagniety u lorda ROT (refleksja - RC dziala tez bez ROT).</summary>
        private static bool RotEnlisted()
        {
            try
            {
                if (!_lookupDone)
                {
                    _lookupDone = true;
                    var sub = Type.GetType("ROT.SubModule, ROT");
                    if (sub != null)
                    {
                        _behField = sub.GetField("EnlistmentBehavior", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                        if (_behField != null && _behField.FieldType != null)
                            _isEnlistedField = _behField.FieldType.GetField("IsEnlisted", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    }
                }
                if (_behField == null || _isEnlistedField == null) return false;
                var beh = _behField.GetValue(null);
                if (beh == null) return false;
                var v = _isEnlistedField.GetValue(beh);
                return v is bool && (bool)v;
            }
            catch { return false; }
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_errLogged) return;
            _errLogged = true;
            Log.Error("RotEnlistLoot37." + where, e);
        }

        public static void Prefix(PlayerEncounter __instance, out bool __state)
        {
            __state = false;
            try
            {
                var s = Settings.Current;
                if (s == null || !s.RotEnlistLootGamePrice || __instance == null || !RotEnlisted()) return;
                // tylko log: ilu lordow wyceni ROT i o ile mniej niz nasz okup (dwie wyceny na lorda - zdarzenie rzadkie)
                int n = 0; long ours = 0, game = 0;
                var roster = __instance.RosterToReceiveLootPrisoners;
                var model = Campaign.Current != null ? Campaign.Current.Models.RansomValueCalculationModel : null;
                if (roster != null && model != null)
                {
                    foreach (var el in roster.GetTroopRoster())
                    {
                        var ch = el.Character;
                        if (ch == null || !ch.IsHero || ch.HeroObject == null || el.Number <= 0) continue;
                        int o = model.PrisonerRansomValue(ch, Hero.MainHero), g;
                        FairRansomPatch._vanillaOnly = true;
                        try { g = model.PrisonerRansomValue(ch, Hero.MainHero); } finally { FairRansomPatch._vanillaOnly = false; }
                        n += el.Number; ours += (long)o * el.Number; game += (long)g * el.Number;
                    }
                }
                FairRansomPatch._vanillaOnly = true;
                __state = true;
                if (n > 0)
                    Log.Info("ROT zaciag (3.7): lup po bitwie - " + n + " lordow w udziale gracza wycenionych cena gry " + game + " zamiast okupu " + ours
                             + " (roznica " + (ours - game) + " bez platnika nie zamienia sie w przedmioty; jency nie trafiaja do gracza)" + (_stumbles > 0 ? " | potkniecia " + _stumbles : "") + ".");
            }
            catch (Exception e) { Stumble("Prefix", e); }
        }

        public static Exception Finalizer(Exception __exception, bool __state)
        {
            if (__state) FairRansomPatch._vanillaOnly = false;
            return __exception;
        }

        internal static void ApplyAll(Harmony harmony)
        {
            try
            {
                var m = AccessTools.Method(typeof(PlayerEncounter), "DoLootMembersAndPrisonersOfParty");
                if (m == null) { Log.Info("ROT zaciag (3.7): BRAK PlayerEncounter.DoLootMembersAndPrisonersOfParty - wycena lordow w lupie zaciagu bez zmian."); return; }
                harmony.Patch(m, prefix: new HarmonyMethod(typeof(RotEnlistLoot37), nameof(Prefix)) { priority = Priority.First },
                                 finalizer: new HarmonyMethod(typeof(RotEnlistLoot37), nameof(Finalizer)));
                Log.Info("ROT zaciag (3.7): wycena lordow w lupie zaciagnietego gracza cena gry - wpiete (wylacznik Rot Enlist Loot Game Price w MCM).");
            }
            catch (Exception e) { Log.Error("RotEnlistLoot37.ApplyAll", e); }
        }
    }
}
