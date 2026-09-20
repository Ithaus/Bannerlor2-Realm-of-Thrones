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
                var s = Settings.Current;
                Log.Info("ManLedger: ksiega ludzi czynna - kazdy ubytek z partii gracza trafi do logu z nazwa winowajcy."
                         + (s != null && s.PlagueSparesYourMen ? " Tarcza przed zaraza WLACZONA - choroba nie zabije juz twoich ludzi." : ""));
            }
            catch (Exception e) { Log.Error("ManLedger.ApplyAll", e); }
        }

        private static int _shielded;

        public static bool LossPrefix(object __instance, int __0, int __1, ref int __result)
        {
            try
            {
                if (__1 >= 0) return true;                             // to nie strata
                var roster = __instance as TroopRoster;
                if (roster == null) return true;
                var main = MobileParty.MainParty;
                if (main == null || main.MemberRoster != roster) return true;   // tylko partia gracza

                string who = WhoAt(roster, __0);

                string blame = Blame();

                // ===== TARCZA PRZED ZARAZA (Jeff 20.09) =====
                // Ksiega ludzi wskazala winowajce: DiseaseEffectSystem.KillTroopsFromDisease
                // z moda AIInfluence (zaciemniony Confuserem, nie do zdekompilowania). Jego dane
                // pokazaly 35 czynnych ognisk "The Dock Fever" naraz na partii gracza, przy jego
                // wlasnym ustawieniu "maksymalnie 3 choroby naraz". Dwie proby zalatwienia tego
                // po dobroci ZAWIODLY i to jest udokumentowane:
                //   1. wyleczenie 35 ognisk w jego pliku danych (disease_instances.json) - mod
                //      nadpisal plik przy nastepnym zapisie i wszystkie 36 wrocily jako czynne,
                //      bo prawdziwy stan trzyma w save, a JSON jest tylko wypisem;
                //   2. MCM "DiseaseMaxDeathChance" 0.3 -> 0.0 - ustawienie PRZETRWALO start gry
                //      (sprawdzone w jego json), a ludzie i tak gina; ten suwak nie bramkuje
                //      smierci ZOLNIERZY.
                // Zostaje tarcza w jedynym miejscu, ktore na pewno dziala: tu, gdzie sztuka
                // schodzi z rostera. Gdy ubytek z partii GRACZA pochodzi z systemu chorob -
                // odmawiamy go. Reszta swiata chowa swoich umarlych normalnie (partie AI
                // w ogole tu nie wchodza). Chorobie zostaja wszystkie kary - morale, predkosc,
                // skille - tylko nie zabija. Dokladnie to, o co Jeff prosil.
                var cfg = Settings.Current;
                if (cfg != null && cfg.PlagueSparesYourMen
                    && blame.IndexOf("Disease", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _shielded += -__1;
                    int every = cfg.PlagueShieldLogEvery > 0 ? cfg.PlagueShieldLogEvery : 20;
                    if (_shielded <= 3 || _shielded % every == 0)
                        Log.Info("TARCZA PRZED ZARAZA: odmowiono smierci " + (-__1) + " x " + WhoAt(roster, __0)
                                 + " | probowal: " + blame + " | uratowanych w sesji: " + _shielded
                                 + " | partia ma dalej " + main.MemberRoster.TotalManCount + ".");
                    __result = __0;
                    return false;                                      // oryginal NIE biegnie - nikt nie ginie
                }

                Tally tal;
                if (!_byBlame.TryGetValue(blame, out tal)) { tal = new Tally(); _byBlame[blame] = tal; }
                tal.Men += -__1; tal.Hits++; _menTotal += -__1;
                if (tal.Hits > 3 && tal.Hits % 20 != 0) return true;   // ta sama przyczyna - nie zasypuj pliku

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
            return true;
        }

        /// <summary>Nazwa oddzialu spod wskazanego wpisu rostera (po zmianie wpis moze zniknac).</summary>
        private static string WhoAt(TroopRoster roster, int index)
        {
            try
            {
                var ch = index >= 0 && index < roster.Count ? roster.GetCharacterAtIndex(index) : null;
                if (ch != null) return ch.Name != null ? ch.Name.ToString() : ch.StringId;
            }
            catch { }
            return "?";
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
