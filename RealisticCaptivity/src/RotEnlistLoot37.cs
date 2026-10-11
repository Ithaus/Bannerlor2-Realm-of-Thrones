using System;
using System.Collections.Generic;
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
    /// pozycji RC) zaklada platnika (rod jenca) - tu nikt nie placi, jeniec do gracza nie trafia. Wiec w tej jednej wycenie lord kosztuje
    /// MIN(nasz okup, cena gry): nigdy wiecej niz cena gry (jak kazdy jeniec w lupie ROT) i nigdy wiecej niz dotad (poprawka po recenzji: przy 178 nasz
    /// okup dla gracza jako sprzedajacego to gotowka rodu CashCap / CrownCash - przy biednym rodzie albo skarbcu ponizej rezerwy 0 albo mniej niz cena gry;
    /// sama cena gry podnosilaby wtedy wycene i ROT dawalby WIECEJ przedmiotow z niczego). Obie wyceny liczone w prefiksie (cena gry przez _vanillaOnly -
    /// ta sama flaga, ktorej RC uzywa przy ekranie druzyny), wynik na bohatera w slowniku _cap, ktory czyta FairRansomPatch.LordPrice (TryCap).
    /// Reszta lupu zaciagu (wymiana lupu na przedmioty, koszt werbunku ludzi) to mechanika ROT - bez zmian.
    /// Prefiks z priorytetem First (przed ROT) + finalizer (biegnie zawsze, takze gdy ROT pomija oryginal; czysci slownik). Poza zaciagiem nic nie robi.
    /// Wylacznik RotEnlistLootGamePrice. Wyjatki liczone (potkniecia), pierwszy do logu.
    /// </summary>
    internal static class RotEnlistLoot37
    {
        private static bool _lookupDone, _errLogged;
        private static FieldInfo _behField, _isEnlistedField;
        private static int _stumbles;

        // wycena lorda w oknie lupu zaciagu: bohater -> min(nasz okup, cena gry); null poza oknem (ustawia Prefix, czysci Finalizer)
        [ThreadStatic] private static Dictionary<Hero, int> _cap;

        /// <summary>Z FairRansomPatch.LordPrice: w oknie lupu zaciagu ROT podaje wycene bohatera min(nasz okup, cena gry). false = poza oknem albo inny bohater.</summary>
        internal static bool TryCap(Hero h, ref int result)
        {
            var d = _cap;
            int c;
            if (d == null || h == null || !d.TryGetValue(h, out c)) return false;
            result = c;
            return true;
        }

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

        public static void Prefix(PlayerEncounter __instance)
        {
            try
            {
                _cap = null;
                var s = Settings.Current;
                if (s == null || !s.RotEnlistLootGamePrice || __instance == null || !RotEnlisted()) return;
                var roster = __instance.RosterToReceiveLootPrisoners;
                var model = Campaign.Current != null ? Campaign.Current.Models.RansomValueCalculationModel : null;
                if (roster == null || model == null) return;
                // dwie wyceny na bohatera (nasza i gry - zdarzenie rzadkie: lord w udziale lupu zaciagu), zastosowana = mniejsza
                Dictionary<Hero, int> cap = null;
                int n = 0, nCut = 0; long ours = 0, game = 0, used = 0;
                foreach (var el in roster.GetTroopRoster())
                {
                    var ch = el.Character;
                    if (ch == null || !ch.IsHero || ch.HeroObject == null || el.Number <= 0) continue;
                    int o = model.PrisonerRansomValue(ch, Hero.MainHero), g;
                    FairRansomPatch._vanillaOnly = true;
                    try { g = model.PrisonerRansomValue(ch, Hero.MainHero); } finally { FairRansomPatch._vanillaOnly = false; }
                    int c = Math.Max(0, Math.Min(o, g));
                    if (cap == null) cap = new Dictionary<Hero, int>();
                    cap[ch.HeroObject] = c;
                    n += el.Number; ours += (long)o * el.Number; game += (long)g * el.Number; used += (long)c * el.Number;
                    if (c < o) nCut += el.Number;
                }
                if (cap == null) return;
                _cap = cap;
                Log.Info("ROT zaciag (3.7): lup po bitwie - " + n + " lordow w udziale gracza wycenionych min(okup, cena gry) = " + used + " (okup " + ours + ", cena gry " + game
                         + "; dotad ROT liczyl okup - przedmiotow z niczego mniej o " + (ours - used) + ", cena gry ucina " + nCut + " z " + n
                         + ", u reszty okup nizszy i zostaje; jency nie trafiaja do gracza)" + (_stumbles > 0 ? " | potkniecia " + _stumbles : "") + ".");
            }
            catch (Exception e) { Stumble("Prefix", e); }
        }

        public static Exception Finalizer(Exception __exception)
        {
            _cap = null;
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
                Log.Info("ROT zaciag (3.7): wycena lordow w lupie zaciagnietego gracza min(okup, cena gry) - wpiete (wylacznik Rot Enlist Loot Game Price w MCM).");
            }
            catch (Exception e) { Log.Error("RotEnlistLoot37.ApplyAll", e); }
        }
    }
}
