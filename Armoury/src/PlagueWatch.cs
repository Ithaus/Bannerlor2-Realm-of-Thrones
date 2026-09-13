using System;
using System.Collections;
using System.Reflection;
using TaleWorlds.CampaignSystem;

namespace Armoury
{
    /// <summary>
    /// MELDUNEK O CHOROBIE (Jeff 13.09: "skad mam wiedziec, ze to JA jestem chory?").
    /// System chorob pochodzi z AI Influence i nie mowi graczowi nic: dowiadujesz sie
    /// dopiero w miejskim szpitalu albo z dymka predkosci, ktory pokazuje sama linie
    /// "Disease" bez slowa o tym, kto choruje. Raz na dzien wypisujemy wiec wprost:
    /// nazwa choroby, jak daleko zaszla i czy trwa kuracja.
    /// LICZBA TO POSTEP CHOROBY, NIE LECZENIA: 0% = zdrowy, im wiecej tym gorzej.
    /// Doradzamy leczyc ponizej 30%, bo sila leku zalezy od mnoznika trudnosci
    /// (2 - postep/50): przy 10% dziala z sila x1.8, przy 50% juz tylko x1.0,
    /// a jedna kuracja jest warta okolo 30-35 punktow postepu - kupiona za pozno
    /// nie dowiezie do zera i pieniadze przepadaja.
    /// Wszystko przez refleksje i miekko: bez AI Influence po prostu spi.
    /// </summary>
    internal static class PlagueWatch
    {
        private static bool _looked;
        private static PropertyInfo _pInstance;      // DiseaseManager.Instance
        private static MethodInfo _mHeroDiseases;    // GetHeroDiseases(Hero)
        private static PropertyInfo _pName, _pProgress, _pRecovered, _pTreated, _pPostDays;
        private static int _lastDay = -1;

        private static void Look()
        {
            if (_looked) return;
            _looked = true;
            try
            {
                Type tMgr = null, tInst = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (asm.GetName().Name != "AIInfluence") continue;
                    tMgr = asm.GetType("AIInfluence.Diseases.DiseaseManager", false);
                    tInst = asm.GetType("AIInfluence.Diseases.DiseaseInstance", false);
                    break;
                }
                if (tMgr == null || tInst == null) return;
                _pInstance = tMgr.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                _mHeroDiseases = tMgr.GetMethod("GetHeroDiseases", new[] { typeof(Hero) });
                _pName = tInst.GetProperty("DiseaseName");
                _pProgress = tInst.GetProperty("DiseaseProgress");
                _pRecovered = tInst.GetProperty("IsRecovered");
                _pTreated = tInst.GetProperty("IsTreated");
                _pPostDays = tInst.GetProperty("PostTreatmentDaysRemaining");
            }
            catch { }
        }

        /// <summary>Raz na dzien gry: czy gracz choruje i jak zle jest.</summary>
        internal static void DailyReport()
        {
            try
            {
                var c = Settings.Current;
                if (c == null || !c.PlagueWatchEnabled) return;
                if (Campaign.Current == null || Hero.MainHero == null) return;
                int day = (int)CampaignTime.Now.ToDays;
                if (day == _lastDay) return;
                _lastDay = day;

                Look();
                if (_pInstance == null || _mHeroDiseases == null || _pProgress == null) return;
                var mgr = _pInstance.GetValue(null, null);
                if (mgr == null) return;
                var list = _mHeroDiseases.Invoke(mgr, new object[] { Hero.MainHero }) as IEnumerable;
                if (list == null) return;

                foreach (var inst in list)
                {
                    if (inst == null) continue;
                    if (_pRecovered != null && (bool)_pRecovered.GetValue(inst, null)) continue;
                    string name = _pName != null ? (_pName.GetValue(inst, null) as string) : null;
                    if (string.IsNullOrEmpty(name)) name = "an illness";
                    float prog = Convert.ToSingle(_pProgress.GetValue(inst, null));
                    bool treated = _pTreated != null && (bool)_pTreated.GetValue(inst, null);
                    int post = _pPostDays != null ? Convert.ToInt32(_pPostDays.GetValue(inst, null)) : 0;

                    // rada zalezna od tego, gdzie realnie stoi - patrz komentarz klasy
                    string advice;
                    if (post > 0) advice = "mending on its own for " + post + " more " + (post == 1 ? "day" : "days") + " - treat again the day that ends";
                    else if (treated) advice = "under treatment";
                    else if (prog >= 30f) advice = "too far gone for one course - buy treatment now and again right after it ends";
                    else advice = "treat it now, while it is still cheap to beat";

                    Log.Player("You are ill: " + name + " at " + ((int)prog) + "% and rising - " + advice + ".", true);
                    Log.Info("Choroba gracza: " + name + " " + prog.ToString("0.0") + "% (leczona=" + treated
                             + ", dni poleczenia=" + post + ").");
                }
            }
            catch (Exception e) { Log.Error("PlagueWatch.DailyReport", e); }
        }
    }
}
