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
    /// </summary>
    internal static class NoTownMines
    {
        private const string MinesId = "mines";
        private static System.Reflection.MethodBase _target;
        internal static string Hook = "nie wpiete";
        private static int _picks, _swaps, _kept, _stumbles;
        private static readonly List<string> _samples = new List<string>();
        private static readonly Dictionary<ItemCategory, ItemObject> _sample = new Dictionary<ItemCategory, ItemObject>();
        private static bool _sampleBuilt;

        internal static bool On { get { var s = Settings.Current; return s != null && s.NoTownMines; } }

        internal static void Reset() { _picks = _swaps = _kept = _stumbles = 0; _samples.Clear(); _sample.Clear(); _sampleBuilt = false; }

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
            if (!On) return;
            try
            {
                if (currentSettlement == null || !currentSettlement.IsTown || currentSettlement.Town == null) return;
                _picks++;
                if (__result == null || __result.StringId != MinesId) return;
                var best = Best(currentSettlement.Town, workshopToExclude, __result);
                if (best == null) { _kept++; return; }
                if (_samples.Count < 6) _samples.Add((currentSettlement.Name != null ? currentSettlement.Name.ToString() : currentSettlement.StringId) + " -> " + best.StringId + (atGameStart ? " (start gry)" : ""));
                __result = best;
                _swaps++;
            }
            catch (Exception e) { if (++_stumbles <= 1) Log.Error("NoTownMines.Postfix", e); }
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

        private static float Price(Town town, ItemCategory c)
        {
            ItemObject it;
            if (c == null || !_sample.TryGetValue(c, out it) || it == null) return -1f;
            return town.MarketData.GetPrice(it, null, false, null);
        }

        /// <summary>Wynik typu w tym miescie: najlepsza linia z wsadem (wyroby - wsad po cenach targu) x szybkosc; -1e9 - typ bez linii do policzenia.</summary>
        private static float Score(Town town, WorkshopType t)
        {
            float best = -1e9f;
            if (t.Productions == null) return best;
            foreach (var p in t.Productions)
            {
                if (p.Inputs == null || p.Inputs.Count == 0 || p.Outputs == null || p.Outputs.Count == 0) continue;   // linia bez wsadu: "z niczego" (WorkshopLaw ja zamyka) - nie liczymy
                float v = 0f; bool ok = true;
                foreach (var o in p.Outputs) { float pr = Price(town, o.Item1); if (pr < 0f) { ok = false; break; } v += pr * o.Item2; }
                if (!ok) continue;
                foreach (var i in p.Inputs) { float pr = Price(town, i.Item1); if (pr < 0f) { ok = false; break; } v -= pr * i.Item2; }
                if (!ok) continue;
                v *= Math.Max(0.01f, p.ConversionSpeed);
                if (v > best) best = v;
            }
            return best;
        }

        private static WorkshopType Best(Town town, WorkshopType exclude, WorkshopType mines)
        {
            BuildSamples();
            var have = new HashSet<WorkshopType>();
            foreach (var w in town.Workshops) if (w != null && w.WorkshopType != null) have.Add(w.WorkshopType);
            WorkshopType bestNew = null, bestAny = null;
            float sNew = 0f, sAny = 0f;
            foreach (var t in WorkshopType.All)
            {
                if (t == null || t.IsHidden || t == mines || t == exclude || t.StringId == MinesId) continue;
                float sc = Score(town, t);
                if (!(sc > 0f)) continue;
                if (!have.Contains(t) && (bestNew == null || sc > sNew)) { bestNew = t; sNew = sc; }
                if (bestAny == null || sc > sAny) { bestAny = t; sAny = sc; }
            }
            return bestNew ?? bestAny;
        }

        /// <summary>NightEcon.Daily: "wyborow typu w miastach N, kopalnia BK -> inny typ Z [przyklady], bez lepszego typu K" (zeruje liczniki doby).</summary>
        internal static string DayText(out int swaps, out int stumbles)
        {
            swaps = _swaps; stumbles = _stumbles;
            string t = "wyborow typu w miastach " + _picks + ", kopalnia -> inny typ " + _swaps + (_samples.Count > 0 ? " [" + string.Join("; ", _samples.ToArray()) + "]" : "") + ", bez lepszego typu (kopalnia zostala) " + _kept;
            _picks = _swaps = _kept = _stumbles = 0; _samples.Clear();
            return t;
        }
    }
}
