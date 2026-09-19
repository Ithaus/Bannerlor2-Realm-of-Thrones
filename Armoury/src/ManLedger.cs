using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

namespace Armoury
{
    /// <summary>
    /// KSIEGA LUDZI (Jeff 19.09, trzeci raz tego samego dnia: "czemu nadal trace ludzi,
    /// zaczalem od 318, teraz mam 313"). Szukanie winnego po objawach zawiodlo kilka razy:
    /// raz zrzucilem strate na bitwe, raz na nasze prawo zaleglego zoldu (slusznie, ale
    /// tylko w czesci), a i tak zostawaly ubytki bez wyjasnienia. Koniec zgadywania.
    ///
    /// KAZDY ubytek z partii GRACZA jest teraz zapisywany do pliku z nazwa oddzialu,
    /// liczba i - co najwazniejsze - z NAZWA KODU, ktory go zabral. Wszystkie drogi
    /// ubytku schodza sie w jednym miejscu: TroopRoster.AddToCountsAtIndex (dekompilacja
    /// 19.09: AddToCounts, RemoveTroop i WoundTroop wszystkie do niego wolaja). Prefix,
    /// a nie postfix, bo po zmianie wpis potrafi zniknac (removeDepleted) i nie bedzie
    /// juz komu odczytac nazwy oddzialu.
    ///
    /// Tylko partia gracza i tylko STRATY (countChange ujemne) - reszta swiata nie placi
    /// za to ani grosza. Rany (woundedCount) nie sa ubytkiem i nie sa logowane.
    /// Zeby bitwa nie zasypala pliku, ta sama przyczyna dostaje pelny wpis trzy razy,
    /// potem co pary dziesiat, ale licznik biegnie caly czas i jest w kazdej linii.
    /// </summary>
    internal static class ManLedger
    {
        private sealed class Tally { internal int Men; internal int Hits; }
        private static readonly Dictionary<string, Tally> _byBlame = new Dictionary<string, Tally>();
        private static int _menTotal;

        internal static void ApplyAll(Harmony harmony)
        {
            try
            {
                var m = AccessTools.Method(typeof(TroopRoster), "AddToCountsAtIndex");
                if (m == null) { Log.Info("ManLedger: TroopRoster.AddToCountsAtIndex nieznalezione - ubytki ludzi NIE beda nazywane."); return; }
                harmony.Patch(m, prefix: new HarmonyMethod(typeof(ManLedger), "LossPrefix"));
                Log.Info("ManLedger: ksiega ludzi czynna - kazdy ubytek z partii gracza trafi do logu z nazwa winowajcy.");
            }
            catch (Exception e) { Log.Error("ManLedger.ApplyAll", e); }
        }

        public static void LossPrefix(object __instance, int __0, int __1)
        {
            try
            {
                if (__1 >= 0) return;                                  // to nie strata
                var roster = __instance as TroopRoster;
                if (roster == null) return;
                var main = MobileParty.MainParty;
                if (main == null || main.MemberRoster != roster) return;   // tylko partia gracza

                string who = "?";
                try
                {
                    var ch = __0 >= 0 && __0 < roster.Count ? roster.GetCharacterAtIndex(__0) : null;
                    if (ch != null) who = ch.Name != null ? ch.Name.ToString() : ch.StringId;
                }
                catch { }

                string blame = Blame();
                Tally tal;
                if (!_byBlame.TryGetValue(blame, out tal)) { tal = new Tally(); _byBlame[blame] = tal; }
                tal.Men += -__1; tal.Hits++; _menTotal += -__1;
                if (tal.Hits > 3 && tal.Hits % 20 != 0) return;        // ta sama przyczyna - nie zasypuj pliku

                string gdzie = "";
                try
                {
                    if (main.MapEvent != null) gdzie = ", w bitwie";
                    else if (main.BesiegerCamp != null) gdzie = ", w obozie oblezniczym";
                    else if (main.CurrentSettlement != null) gdzie = ", w osadzie " + main.CurrentSettlement.Name;
                }
                catch { }

                Log.Info("KSIEGA LUDZI: -" + (-__1) + " " + who + gdzie
                         + " | zabral: " + blame
                         + " | ta przyczyna dzis: " + tal.Men + " ludzi w " + tal.Hits + " ruchach"
                         + " | razem w sesji: " + _menTotal
                         + " | partia ma teraz " + main.MemberRoster.TotalManCount + ".");
            }
            catch { }
        }

        /// <summary>Kto wolal: pierwsze klatki stosu spoza rostera, Harmony i nas samych.
        /// Bez plikow i numerow linii (szybciej), sama nazwa typu i metody.</summary>
        private static string Blame()
        {
            try
            {
                var st = new System.Diagnostics.StackTrace(2, false);
                var parts = new List<string>();
                int n = st.FrameCount;
                for (int i = 0; i < n && parts.Count < 3; i++)
                {
                    var mb = st.GetFrame(i).GetMethod();
                    if (mb == null) continue;
                    var dt = mb.DeclaringType;
                    string tn = dt != null ? dt.Name : "?";
                    string ns = dt != null && dt.Namespace != null ? dt.Namespace : "";
                    if (tn == "TroopRoster" || tn == "ManLedger") continue;
                    if (ns.StartsWith("HarmonyLib") || ns.StartsWith("MonoMod")) continue;
                    if (tn.StartsWith("<") || tn.Contains("__")) continue;      // lambdy i maszyny stanu
                    parts.Add(tn + "." + mb.Name);
                }
                return parts.Count > 0 ? string.Join(" <- ", parts.ToArray()) : "(stos nieczytelny)";
            }
            catch { return "(stos niedostepny)"; }
        }
    }
}
