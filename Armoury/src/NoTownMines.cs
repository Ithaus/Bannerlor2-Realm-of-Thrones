using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// N10 (noc 16.10, audyt ekonomii 15.10 rozdz. 3.1, tabela problemow nr 24) - BEZ KOPALNI BK W MIASTACH. BK EconomyPatches.DecideBestWorkshopTypePostfix
    /// (dekompilacja BK :748): gdy wylosowany typ juz stoi w miescie, w 30% powtorzen daje "mines" (reszta - losowa kuznia). Kopalnia w miescie nie ma rudy
    /// (WorkshopLaw FreeRawExtraction + Bloomery.TownMineBlocked) - w tescie 4-letnim 18 -> 2 kopalni, 0 cykli od doby 180; u Jeffa 11 martwych kopalni.
    /// Regula (klucz NoTownMines): postfiks na metode gry WorkshopsCampaignBehavior.DecideBestWorkshopType (DeclaredMethod - klasa deklarujaca), priorytet Low:
    /// PO postfiksie BK (Normal 400), PRZED licznikiem WorkshopInputs.DecidePostfix (Last 0) - licznik widzi wynik koncowy. Wynik "mines" w miescie (IsTown)
    /// -> typ o najlepszym wyniku z cen targu tego miasta: najlepsza linia z wsadem (suma wyrobow x cena - suma wsadu x cena) x szybkosc linii; najpierw typy,
    /// ktorych miasto jeszcze nie ma (zamysl BK: bez dubli), potem kazdy; bez ukrytych, bez kopalni i bez typu wykluczonego (bankrut). Cena kategorii = cena
    /// w tym miescie sztuki o srodkowej wartosci kategorii (Town.MarketData.GetPrice - z naszymi prawami ceny). Brak typu z wynikiem - kopalnia zostaje
    /// (licznik). Istniejacych warsztatow nie przestawia (tylko przyszle wybory typu: bankructwo, odebranie, start nowej gry); warsztatow gracza nie dotyka
    /// (gra nie wola tej metody dla warsztatu gracza w toku). Obieg: zloto i towar bez zmian - inny typ warsztatu tej samej akcji gry.
    /// Recenzja (noc 16.10): typy, ktorym brak wsadu w okregu (bramka E6/M2 WorkshopInputs: linia towarowa bez surowca w slowniku gry i bez kontraktu), ida
    /// na koniec - wybrane tylko, gdy zaden typ z wsadem nie ma wyniku (inaczej np. tlocznia bez winnic stoi albo bankrutuje); licznik "typ bez wsadu odlozony".
    /// F4 (noc 17/18.10, paczka f4-warsztaty, klucz WorkshopRetoolFairScore): ocena typu (Score) i wybor przy przezbrojeniu/bankructwie (WorkshopRetool) ida przez
    /// WorkshopFairScore - cena sprawiedliwa cyklu zamiast ceny srodkowej sztuki kategorii (Price), cykle ograniczone wsadem regionu, zmierzony wynik 60 dob, kara za
    /// dubel w regionie rynku. MarketScore, Price i BestByPrice zostaja bez zmian dla drogi z wylaczonym kluczem.
    /// </summary>
    internal static class NoTownMines
    {
        private const string MinesId = "mines";
        private static System.Reflection.MethodBase _target;
        internal static string Hook = "nie wpiete";
        private static int _picks, _swaps, _kept, _stumbles, _gated;
        private static readonly List<string> _samples = new List<string>();
        private static readonly Dictionary<ItemCategory, ItemObject> _sample = new Dictionary<ItemCategory, ItemObject>();
        private static bool _sampleBuilt;

        internal static bool On { get { var s = Settings.Current; return s != null && s.NoTownMines; } }

        internal static void Reset() { _picks = _swaps = _kept = _stumbles = _gated = 0; _samples.Clear(); _sample.Clear(); _sampleBuilt = false; _f4Picks = _f4Fallback = 0; }

        /// <summary>Start sesji (PO HistoricalPrices.Apply): sztuki kategorii od nowa - wybory typu przy starcie nowej gry biegna przed cenami historycznymi.</summary>
        internal static void SessionStart() { _sample.Clear(); _sampleBuilt = false; }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var m = AccessTools.DeclaredMethod(typeof(WorkshopsCampaignBehavior), "DecideBestWorkshopType");
                if (m == null) { Hook = "BRAK DecideBestWorkshopType"; Log.Info("NoTownMines (N10): " + Hook + " - typy warsztatow jak w grze i BK."); return; }
                h.Patch(m, postfix: new HarmonyMethod(typeof(NoTownMines), nameof(Postfix)) { priority = Priority.Low });
                _target = m;
                Hook = "wpiete";
                Log.Info("NoTownMines (N10): postfiks DecideBestWorkshopType wpiety (priorytet Low - po BK, przed licznikiem WorkshopInputs); klucz No Town Mines czytany na zywo.");
            }
            catch (Exception e) { Hook = "wyjatek " + e.GetType().Name; Log.Error("NoTownMines.ApplyAll", e); }
        }

        /// <summary>Kolejnosc postfiksow celu (wlasciciel:priorytet, w kolejnosci wykonania) - do linii startu sesji (kontrola kolejnosci Harmony).</summary>
        internal static string Order()
        {
            try
            {
                if (_target == null) return Hook;
                var info = Harmony.GetPatchInfo(_target);
                if (info == null || info.Postfixes == null) return "brak informacji Harmony";
                var l = new List<Patch>(info.Postfixes);
                l.Sort((a, b) => a.priority != b.priority ? b.priority.CompareTo(a.priority) : a.index.CompareTo(b.index));
                var parts = new List<string>();
                foreach (var p in l) parts.Add(p.owner + "/" + (p.PatchMethod != null && p.PatchMethod.DeclaringType != null ? p.PatchMethod.DeclaringType.Name + "." + p.PatchMethod.Name : "?") + ":" + p.priority);
                return string.Join(" -> ", parts.ToArray());
            }
            catch (Exception e) { return "wyjatek " + e.GetType().Name; }
        }

        public static void Postfix(Settlement currentSettlement, bool atGameStart, WorkshopType workshopToExclude, ref WorkshopType __result)
        {
            if (currentSettlement == null || !currentSettlement.IsTown || currentSettlement.Town == null) return;
            if (On)
            {
                try
                {
                    _picks++;
                    if (__result != null && __result.StringId == MinesId)
                    {
                        var best = Best(currentSettlement.Town, workshopToExclude, __result);
                        if (best == null) _kept++;
                        else
                        {
                            if (_samples.Count < 6) _samples.Add((currentSettlement.Name != null ? currentSettlement.Name.ToString() : currentSettlement.StringId) + " -> " + best.StringId + (atGameStart ? " (start gry)" : ""));
                            __result = best;
                            _swaps++;
                        }
                    }
                }
                catch (Exception e) { if (++_stumbles <= 1) Log.Error("NoTownMines.Postfix", e); }
            }
            // E3-D3 (a) (noc-ekonomia3 czesc 2, klucz WorkshopRetoolByPrice): bankructwo i warsztat odebrany graczowi (atGameStart = false) - typ z cen targu po placach,
            // gdy zarabia > 1.25 x wybor gry/BK (po podmianie kopalni wyzej); ta sama regula wyboru co dobowe przezbrojenie (BestByPrice, wlasny try i licznik)
            if (!atGameStart) WorkshopRetool.BankruptStep(currentSettlement.Town, workshopToExclude, ref __result);
        }

        /// <summary>Sztuka o srodkowej wartosci kazdej kategorii (raz na sesje; wartosci po cenach historycznych).</summary>
        private static void BuildSamples()
        {
            if (_sampleBuilt) return;
            _sampleBuilt = true;
            var byCat = new Dictionary<ItemCategory, List<ItemObject>>();
            foreach (var it in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
            {
                if (it == null || it.ItemCategory == null || it.Value <= 0 || it.NotMerchandise) continue;
                List<ItemObject> l;
                if (!byCat.TryGetValue(it.ItemCategory, out l)) byCat[it.ItemCategory] = l = new List<ItemObject>();
                l.Add(it);
            }
            foreach (var kv in byCat)
            {
                kv.Value.Sort((a, b) => a.Value.CompareTo(b.Value));
                _sample[kv.Key] = kv.Value[kv.Value.Count / 2];
            }
        }

        internal static float Price(Town town, ItemCategory c)
        {
            BuildSamples();
            ItemObject it;
            if (c == null || !_sample.TryGetValue(c, out it) || it == null) return -1f;
            return town.MarketData.GetPrice(it, null, false, null);
        }

        /// <summary>Wynik typu w tym miescie: najlepsza linia z wsadem (wyroby - wsad po cenach targu) x szybkosc; -1e9 - typ bez linii do policzenia.
        /// F4 (noc 17/18.10, klucz WorkshopRetoolFairScore): fair - ta sama ocena co przezbrojenie (WorkshopFairScore.Score: cena sprawiedliwa cyklu, cykle z wsadu
        /// regionu, zmierzony wynik >= 60 dob); wolane z fair tylko po tygodniu pomiaru wsadu w kampanii (Best), start gry i pierwszy tydzien - jak dotad.</summary>
        private static float Score(Town town, WorkshopType t, bool fair)
        {
            if (fair) return WorkshopFairScore.Score(town, t, null).Score;
            return MarketScore(town, t, false);
        }

        /// <summary>F4: wybory N10 ocena F4 i powroty do oceny z cen (zaden typ bez wyniku F4 > 0 - kopalnia by zostala) - do linii E3-D3 (zeruje TakeF4).</summary>
        private static int _f4Picks, _f4Fallback;
        internal static void TakeF4(out int picks, out int fallback) { picks = _f4Picks; fallback = _f4Fallback; _f4Picks = _f4Fallback = 0; }

        /// <summary>
        /// Jedna regula wyboru typu z cen targu (N10 kopalnie; E3-D3 przezbrojenie i bankructwo): najlepsza linia z wsadem (wyroby - wsad po cenach targu) x szybkosc.
        /// net (E3-D3, uwaga recenzji): tylko linie TOWAROWE (kuznia - narzedzia, bez broni i war_horse) i wynik po placach cyklu (WorkshopTrade.CycleLabourAt) -
        /// ta sama miara dla typu obecnego i nowego. -1e9 - typ bez linii do policzenia.
        /// </summary>
        internal static float MarketScore(Town town, WorkshopType t, bool net)
        {
            float best = -1e9f;
            if (town == null || t == null || t.Productions == null) return best;
            BuildSamples();
            foreach (var p in t.Productions)
            {
                if (p.Inputs == null || p.Inputs.Count == 0 || p.Outputs == null || p.Outputs.Count == 0) continue;   // linia bez wsadu: "z niczego" (WorkshopLaw ja zamyka) - nie liczymy
                if (net && !WorkshopTrade.IsTradeLine(p)) continue;
                float v = 0f; bool ok = true;
                foreach (var o in p.Outputs) { float pr = Price(town, o.Item1); if (pr < 0f) { ok = false; break; } v += pr * o.Item2; }
                if (!ok) continue;
                foreach (var i in p.Inputs) { float pr = Price(town, i.Item1); if (pr < 0f) { ok = false; break; } v -= pr * i.Item2; }
                if (!ok) continue;
                if (net) v -= WorkshopTrade.CycleLabourAt(town.Settlement, t, p);
                v *= Math.Max(0.01f, p.ConversionSpeed);
                if (v > best) best = v;
            }
            return best;
        }

        /// <summary>
        /// E3-D3: typ o najlepszym wyniku netto (MarketScore net) w tym miescie sposrod typow, ktorych wsad jest w okregu (dict - WorkshopInputs.InputsNear: wsie,
        /// polka, kontrakt 174); bez ukrytych, kopalni i typu `exclude`. Kuznia moze byc celem (jedyny wytworca narzedzi). null - zaden typ z wynikiem > 0.
        /// Poprawki (uwaga recenzji 2): wynik typu juz obecnego w miescie x 0.8 za kazdy taki warsztat (jak dywersyfikacja BK w DecideBestWorkshopType) - miasto nie
        /// dostaje co 30 dob kolejnego warsztatu tego samego najlepszego typu.
        /// </summary>
        private const float DupKeep = 0.8f;
        internal static WorkshopType BestByPrice(Town town, WorkshopType exclude, IDictionary<ItemCategory, float> dict, out float score)
        {
            score = -1e9f;
            WorkshopType pick = null;
            if (town == null) return null;
            foreach (var t in WorkshopType.All)
            {
                if (t == null || t.IsHidden || t == exclude || t.StringId == MinesId) continue;
                if (!WorkshopInputs.NeedsInput(t) || !WorkshopInputs.HasInput(t, dict)) continue;
                float sc = MarketScore(town, t, true);
                if (!(sc > 0f)) continue;
                int dup = 0;
                if (town.Workshops != null) foreach (var w in town.Workshops) if (w != null && w.WorkshopType == t) dup++;
                if (dup > 0) sc *= (float)Math.Pow(DupKeep, dup);
                if (pick == null || sc > score) { pick = t; score = sc; }
            }
            return pick;
        }

        /// <summary>N10: typ w miejsce kopalni. F4 (klucz WorkshopRetoolFairScore razem z WorkshopRetoolByPrice - jak mowi podpowiedz MCM, pomiar wsadu gotowy): najpierw
        /// ocena F4; gdy zaden typ nie ma w niej wyniku > 0, ocena z cen jak dotad - martwa kopalnia (0 cykli) jest gorsza od kazdego typu, wiec F4 nie zostawia jej
        /// czesciej niz dotad. Recenzja (drobne): przy wylaczonym Workshop Retool By Price podmiana kopalni ocenia z cen jak dotad (pomiar wsadu i tak biegnie).</summary>
        private static WorkshopType Best(Town town, WorkshopType exclude, WorkshopType mines)
        {
            bool fair = WorkshopRetool.On && WorkshopFairScore.Ready;
            var pick = Pick(town, exclude, mines, fair);
            if (fair)
            {
                _f4Picks++;
                if (pick == null) { pick = Pick(town, exclude, mines, false); if (pick != null) _f4Fallback++; }
            }
            return pick;
        }

        private static WorkshopType Pick(Town town, WorkshopType exclude, WorkshopType mines, bool fair)
        {
            BuildSamples();
            var have = new HashSet<WorkshopType>();
            foreach (var w in town.Workshops) if (w != null && w.WorkshopType != null) have.Add(w.WorkshopType);
            // [0] typy z wsadem w okregu (albo bez potrzeby wsadu), [1] typy bez wsadu w okregu (bramka E6/M2) - na koniec
            var bestNew = new WorkshopType[2]; var bestAny = new WorkshopType[2];
            var sNew = new float[2]; var sAny = new float[2];
            foreach (var t in WorkshopType.All)
            {
                if (t == null || t.IsHidden || t == mines || t == exclude || t.StringId == MinesId) continue;
                float sc = Score(town, t, fair);
                if (!(sc > 0f)) continue;
                int g = WorkshopInputs.InputMissing(t) ? 1 : 0;
                if (!have.Contains(t) && (bestNew[g] == null || sc > sNew[g])) { bestNew[g] = t; sNew[g] = sc; }
                if (bestAny[g] == null || sc > sAny[g]) { bestAny[g] = t; sAny[g] = sc; }
            }
            var pick = bestNew[0] ?? bestAny[0];
            // licznik: bez bramki wygralby typ bez wsadu w okregu (ta sama kolejnosc: najpierw typy nowe w miescie)
            var oldNew = bestNew[0] != null && (bestNew[1] == null || sNew[0] >= sNew[1]) ? bestNew[0] : bestNew[1];
            var oldAny = bestAny[0] != null && (bestAny[1] == null || sAny[0] >= sAny[1]) ? bestAny[0] : bestAny[1];
            var old = oldNew ?? oldAny;
            if (old != null && pick != null && old != pick) _gated++;
            return pick ?? bestNew[1] ?? bestAny[1];
        }

        /// <summary>NightEcon.Daily: "wyborow typu w miastach N, kopalnia BK -> inny typ Z [przyklady], bez lepszego typu K" (zeruje liczniki doby).</summary>
        internal static string DayText(out int swaps, out int stumbles)
        {
            swaps = _swaps; stumbles = _stumbles;
            string t = "wyborow typu w miastach " + _picks + ", kopalnia -> inny typ " + _swaps + (_samples.Count > 0 ? " [" + string.Join("; ", _samples.ToArray()) + "]" : "") + ", bez lepszego typu (kopalnia zostala) " + _kept
                       + " (typ bez wsadu w okregu odlozony " + _gated + ")";
            _picks = _swaps = _kept = _stumbles = _gated = 0; _samples.Clear();
            return t;
        }
    }
}
