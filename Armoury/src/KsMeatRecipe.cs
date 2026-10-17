using System;
using System.Collections.Generic;
using System.Globalization;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// KS runda 4 (17.10, Jeff: "a czemu tak jest" - rzeznie miast daja mniej miesa niz uboj domowy): ukryty warsztat "artisans" (receptura Banner Kings,
    /// BannerKings.Redux/ModuleData/workshops.xml) robi z krowy 6, ze swini 2, z owcy 1 sztuke miesa (10 kg) - to 30%, 17% i 25% wagi zywca (200 / 120 / 40 kg),
    /// liczby z balansu gry, nie z uboju. Naprawde zostaje ok. polowa wagi (bydlo 50-55%, owca 45-50%, swinia 65-70%). Plan KS liczy mieso z zywca na targ
    /// MeatOfLive (0.5); r3 final: realne mieso 50% Planu, polki miesa 10-19% celu, cena 3-5 x startu.
    /// Regula: przy KS zastosowanym liczba miesa linii uboju (wsad krowa / swinia / owca, wyrob kategorii meat) = waga zywca x MeatOfLive / waga miesa
    /// (krowa 10, swinia 6, owca 2) - ta sama wydajnosc co w Planie; skory i welna bez zmian. Poza KS (stan Plan, klucz wylaczony, inny zapis w tym samym procesie)
    /// - receptura BK przywracana co do sztuki. Zmienia tylko liczbe w MBList wyrobow linii (MBReadOnlyList dziedziczy List) - bez plikow BK, bez zapisu.
    /// </summary>
    internal static class KsMeatRecipe
    {
        private sealed class Orig { public WorkshopType.Production P; public int Index; public int Count; public string Live; public int Want; }
        private static readonly List<Orig> _orig = new List<Orig>();
        private static bool _on;
        private static bool _errLogged;
        private static readonly string[] Live = { "cow", "hog", "sheep" };

        /// <summary>StartCalibration.Daily: on = KS zastosowany. Wlacza (raz, linia logu) albo przywraca recepture BK. Nie rzuca.</summary>
        internal static void Ensure(bool on)
        {
            try
            {
                if (!on)
                {
                    if (_orig.Count == 0) { _on = false; return; }
                    foreach (var o in _orig) Set(o.P, o.Index, o.Count);
                    Log.Info("KS: receptura uboju (artisans) PRZYWROCONA do BK - " + Describe(false) + ".");
                    _orig.Clear(); _on = false;
                    return;
                }
                if (_on) return;
                var wt = WorkshopType.Find("artisans");
                var meat = MBObjectManager.Instance != null ? MBObjectManager.Instance.GetObject<ItemObject>("meat") : null;
                if (wt == null || wt.Productions == null || meat == null || !(meat.Weight > 0f)) { _on = true; Log.Info("KS: receptura uboju (artisans) - brak warsztatu albo miesa, bez zmian."); return; }
                foreach (var p in wt.Productions)
                {
                    if (p.Inputs == null || p.Outputs == null) continue;   // Production to struct - kopia, ale lista wyrobow (MBList) wspolna
                    string live = null;
                    foreach (var i in p.Inputs) if (i.Item1 != null && Array.IndexOf(Live, i.Item1.StringId) >= 0 && i.Item2 > 0) { live = i.Item1.StringId; break; }
                    if (live == null) continue;
                    var liveIt = MBObjectManager.Instance.GetObject<ItemObject>(live);
                    if (liveIt == null || !(liveIt.Weight > 0f)) continue;
                    int perCycle = 0;
                    foreach (var i in p.Inputs) if (i.Item1 != null && i.Item1.StringId == live) perCycle += Math.Max(1, i.Item2);
                    for (int k = 0; k < p.Outputs.Count; k++)
                    {
                        var o = p.Outputs[k];
                        if (o.Item1 == null || o.Item1.StringId != "meat") continue;
                        int want = Math.Max(1, (int)Math.Round(perCycle * liveIt.Weight * KsTab.MeatOfLive / meat.Weight));
                        if (want == o.Item2) continue;
                        _orig.Add(new Orig { P = p, Index = k, Count = o.Item2, Live = live, Want = want });
                        Set(p, k, want);
                    }
                }
                _on = true;
                Log.Info("KS: receptura uboju (artisans) wedlug wagi zywca x " + KsTab.MeatOfLive.ToString("0.00", CultureInfo.InvariantCulture) + " / mieso "
                         + meat.Weight.ToString("0.#", CultureInfo.InvariantCulture) + " kg (jak Plan KS) - " + Describe(true) + "; skory i welna bez zmian.");
            }
            catch (Exception e)
            {
                if (!_errLogged) { _errLogged = true; Log.Error("KsMeatRecipe", e); }
            }
        }

        private static void Set(WorkshopType.Production p, int k, int count)
        {
            var list = p.Outputs as List<(ItemCategory, int)>;
            if (list == null || k < 0 || k >= list.Count) return;
            list[k] = (list[k].Item1, count);
        }

        private static string Describe(bool now)
        {
            if (_orig.Count == 0) return "linii do zmiany 0";
            var parts = new List<string>();
            foreach (var o in _orig) parts.Add(o.Live + " " + o.Count + (now ? " -> " + o.Want : " <- " + o.Want));
            return "mieso z linii: " + string.Join(", ", parts);
        }
    }
}
