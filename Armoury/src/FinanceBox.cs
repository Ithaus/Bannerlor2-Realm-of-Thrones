using System;
using HarmonyLib;

namespace Armoury
{
    /// <summary>
    /// m115: flaga - tylko panel Finance ekranu rodu (Jeff 24.10: "skoro wioksa mi placi to ma byc na zielon w inocme i na drugim zdjeicuy").
    /// ClanManagementVM.RefreshDailyValues liczy "Total Income" z CalculateClanIncome(rod) i "Daily Change" / "Expected" z niego. Prefiks podnosi licznik watku,
    /// finalizer go zdejmuje (takze przy wyjatku gry - przepuszczony bez zmian). SideVillageFief.IncomeViewPostfix dopisuje linie prac gracza TYLKO przy In:
    /// nasze wlasne odczyty CalculateClanIncome (ClanIncomeBook, KingdomTreasury, IronBank, BuildFunding) linii nie widza - bez liczenia przychodu dwa razy.
    /// Metoda klasy bazowej (NavalClanManagementVM jej nie nadpisuje; mixin BK tez nie). Wpiecie raz na proces, w kampanii (SideVillageFief.EnsureHooks).
    /// </summary>
    internal static class FinanceBox
    {
        [ThreadStatic] private static int _in;
        private static bool _tried, _ok;
        private static string _why = "nie probowano";
        private static int _st;

        internal static bool In { get { return _in > 0; } }

        /// <summary>Stan wpiecia do linii logu: "wpiety" albo "BRAK (powod)".</summary>
        internal static string State { get { return _ok ? "wpiety" : "BRAK (" + _why + ")"; } }

        public static void BoxPrefix() { _in++; }

        public static Exception BoxFinalizer(Exception __exception)
        {
            if (_in > 0) _in--;
            return __exception;   // tylko wyjatek gry - nasz kod nic nie rzuca
        }

        /// <summary>Latka na ClanManagementVM.RefreshDailyValues (prefiks + finalizer), raz na proces. true - wpieta.</summary>
        internal static bool Wire(Harmony h)
        {
            if (_tried) return _ok;
            if (h == null) return false;
            _tried = true;
            try
            {
                var m = AccessTools.DeclaredMethod(typeof(TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.ClanManagementVM), "RefreshDailyValues", Type.EmptyTypes);
                if (m == null) _why = "brak metody";
                else if (m.ReturnType != typeof(void)) _why = "inny typ wyniku " + m.ReturnType.Name;
                else if (m.IsAbstract) _why = "metoda abstrakcyjna";
                else if (m.IsStatic) _why = "metoda statyczna";
                else
                {
                    h.Patch(m, prefix: new HarmonyMethod(typeof(FinanceBox), nameof(BoxPrefix)), finalizer: new HarmonyMethod(typeof(FinanceBox), nameof(BoxFinalizer)));
                    _ok = true; _why = null;
                }
            }
            catch (Exception e) { _ok = false; _why = "blad latki"; Stumble("Wire", e); }
            Log.Info("[Fiefs m115] panel Finance: RefreshDailyValues " + State + (_st > 0 ? " | potkniecia " + _st : "") + ".");
            return _ok;
        }

        private static void Stumble(string where, Exception e)
        {
            _st++;
            if (_st == 1 || _st % 200 == 0) Log.Error("FinanceBox." + where + " (potkniecie " + _st + ")", e);
        }
    }
}
