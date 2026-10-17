using System;
using System.Collections.Generic;
using System.Globalization;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// KS runda 4 (17.10, Jeff: "a czemu tak jest" o rzezniach i "sprawdz tez czy inne jedzenie tez daje odpowiednie ilosci ... piwo, wino") - RECEPTURY JEDZENIA
    /// WEDLUG WAGI. Kazdy towar gry to sztuka 10 kg (zywiec: krowa 200, swinia 120, owca 40, kura i ges 5 kg). Receptury gry i Banner Kings (workshops.xml BK,
    /// spworkshops.xml) sa z balansu gry, nie z przetworstwa: oliwa 1 kg oliwek -> 1 kg (warsztat) / 2 kg (tlocznia) - naprawde 5-6 kg oliwek na 1 kg oliwy;
    /// wino 1 -> 1 / 2 - naprawde ok. 0.7; piwo 1 -> 1 (artisans) - naprawde 1 kg zboza na 2.5-3 kg slabego piwa; chleb w piekarni BK 1 -> 2 - naprawde ok. 1:1;
    /// pierogi 2 -> 4-6; mieso z krowy 30%, swini 17%, owcy 25% wagi - naprawde ok. polowa; kury i gesi u rzeznika 100% wagi.
    /// Regula (klucz Ks Real Recipes, tylko przy KS zastosowanym): liczby wsadu i wyrobu linii warsztatow (wszystkie typy, takze ukryty "artisans") = ta sama
    /// tabela przetworow co Plan KS (KsTab: piwo BeerGrain 0.40, wino WineGrape 1.30, oliwa OilOlive 5.0, miod pitny MeadHoney 0.35, mieso MeatOfLive 0.5, chleb 1:1)
    /// w najblizszych liczbach calkowitych; skory i welna bez zmian. Poza KS - receptury gry przywracane co do sztuki. Zmienia tylko liczby w listach MBList
    /// linii (MBReadOnlyList dziedziczy List) - bez plikow modow, bez zapisu.
    /// </summary>
    internal static class KsRecipes
    {
        private sealed class Orig { public List<(ItemCategory, int)> L; public int Index; public int Count; public int Want; public string What; }
        private static readonly List<Orig> _orig = new List<Orig>();
        private static bool _on;
        private static bool _errLogged;

        /// <summary>Czynne (KS zastosowany, klucz) - ArtisanInputs.YieldNow nie dokłada wtedy drugiego piwa ZC1 (receptura juz wedlug wagi).</summary>
        internal static bool Active { get { return _on && _orig.Count > 0; } }

        // (wsad, wyrob) -> (sztuk wsadu, sztuk wyrobu); "*" = dowolny wsad (pierogi: zboze + nadzienie -> 2)
        private static readonly Dictionary<string, KeyValuePair<int, int>> Rule = new Dictionary<string, KeyValuePair<int, int>>(StringComparer.Ordinal)
        {
            { "grain>bread", new KeyValuePair<int, int>(1, 1) },     // 10 kg zboza -> 10 kg chleba (maka ok. 80% + woda)
            { "grain>beer", new KeyValuePair<int, int>(2, 5) },      // BeerGrain 0.40 kg zboza na kg piwa
            { "grape>wine", new KeyValuePair<int, int>(4, 3) },      // WineGrape 1.30 kg winogron na kg wina (0.75)
            { "olives>oil", new KeyValuePair<int, int>(5, 1) },      // OilOlive 5.0 kg oliwek na kg oliwy
            { "honey>mead", new KeyValuePair<int, int>(1, 3) },      // MeadHoney 0.35 kg miodu na kg miodu pitnego
            { "cow>meat", new KeyValuePair<int, int>(1, 10) },       // 200 kg x 0.5 / 10 kg
            { "hog>meat", new KeyValuePair<int, int>(1, 6) },        // 120 kg x 0.5 / 10 kg
            { "sheep>meat", new KeyValuePair<int, int>(1, 2) },      // 40 kg x 0.5 / 10 kg
            { "chicken>meat", new KeyValuePair<int, int>(3, 1) },    // 15 kg zywca -> 10 kg miesa (ok. 2/3 - tuszka drobiu)
            { "goose>meat", new KeyValuePair<int, int>(3, 1) },
            { "*>pie", new KeyValuePair<int, int>(0, 2) },           // zboze + nadzienie (20 kg) -> 2 pierogi (20 kg); wsad bez zmian
            // Jeff 17.10 "przejrzyj wszystkie surowce, nie tylko jedzenie": przetwory poza bronia i zbroja (te - WorkshopLaw); te same stale co Plan (KsTab)
            { "iron>tools", new KeyValuePair<int, int>(4, 5) },      // lupka zelaza 15 kg, ToolsIron 1.2 kg na kg narzedzi (kucie, zgorzelina): 60 kg -> 50 kg (gra 1 -> 2 / kuznia 1 -> 4)
            { "clay>pottery", new KeyValuePair<int, int>(6, 5) },    // PotClay 1.2 kg gliny na kg naczyn (woda i wypal) (gra 1 -> 1 / garncarnia 1 -> 2)
            { "flax>linen", new KeyValuePair<int, int>(5, 4) },      // LinenFlax 1.25 kg lnu na kg plotna (tkalnia 1 -> 2)
            { "cotton>velvet", new KeyValuePair<int, int>(6, 5) },   // surowy jedwab, VelvetSilk 1.2 kg na kg aksamitu (1 -> 2)
            { "wool>garment", new KeyValuePair<int, int>(3, 2) },    // FeltWool 1.45 kg welny (pranie, gremplowanie) na kg sukna (1 -> 2)
            { "wool>felt", new KeyValuePair<int, int>(3, 2) },
            { "hides>leather", new KeyValuePair<int, int>(5, 2) },   // LeatherHide 2.4 kg skory surowej na kg wyprawionej (garbarnia 1 -> 2)
            { "hardwood>planks", new KeyValuePair<int, int>(1, 5) }, // pien 100 kg -> deski ok. 50% (tartak reczny) = 5 x 10 kg (gra 1 -> 2 = 20%)
            { "silver>jewelry", new KeyValuePair<int, int>(25, 1) }, // ruda srebra, JewelSilver 25 kg rudy na kg wyrobu (gra 1 -> 2)
        };

        private static bool KeyOn { get { var s = Settings.Current; return s == null || s.KsRealRecipes; } }

        /// <summary>StartCalibration.Daily: on = KS zastosowany. Wlacza (raz, linia logu) albo przywraca receptury gry. Nie rzuca.</summary>
        internal static void Ensure(bool on)
        {
            try
            {
                on = on && KeyOn;
                if (!on)
                {
                    if (_orig.Count == 0) { _on = false; return; }
                    foreach (var o in _orig) Set(o.L, o.Index, o.Count);
                    Log.Info("KS: receptury jedzenia PRZYWROCONE do gry (" + _orig.Count + " liczb w liniach warsztatow).");
                    _orig.Clear(); _on = false;
                    return;
                }
                if (_on) return;
                var parts = new List<string>();
                int lines = 0;
                foreach (var wt in WorkshopType.All)
                {
                    if (wt == null || wt.Productions == null) continue;
                    foreach (var p in wt.Productions)
                    {
                        var ins = p.Inputs as List<(ItemCategory, int)>;
                        var outs = p.Outputs as List<(ItemCategory, int)>;
                        if (ins == null || outs == null || ins.Count == 0) continue;
                        for (int k = 0; k < outs.Count; k++)
                        {
                            var oc = outs[k].Item1;
                            if (oc == null) continue;
                            string outId = oc.StringId;
                            KeyValuePair<int, int> r;
                            if (Rule.TryGetValue("*>" + outId, out r))
                            {
                                if (Change(outs, k, r.Value, wt.StringId + ": " + Desc(ins) + " -> " + outId)) { lines++; parts.Add(wt.StringId + " " + Desc(ins) + "->" + outId + " " + r.Value); }
                                continue;
                            }
                            if (ins.Count != 1 || ins[0].Item1 == null) continue;
                            string inId = ins[0].Item1.StringId;
                            if (!Rule.TryGetValue(inId + ">" + outId, out r)) continue;
                            bool a = Change(ins, 0, r.Key, wt.StringId + ": wsad " + inId), b = Change(outs, k, r.Value, wt.StringId + ": " + inId + " -> " + outId);
                            if (a || b) { lines++; parts.Add(wt.StringId + " " + inId + "->" + outId + " " + r.Key + ":" + r.Value); }
                        }
                    }
                }
                _on = true;
                Log.Info("KS: receptury jedzenia wedlug wagi (jak tabela przetworow Planu; klucz Ks Real Recipes) - linii zmienionych " + lines
                         + (parts.Count > 0 ? ": " + string.Join(", ", parts) : "") + "; skory i welna bez zmian.");
            }
            catch (Exception e)
            {
                if (!_errLogged) { _errLogged = true; Log.Error("KsRecipes", e); }
            }
        }

        private static bool Change(List<(ItemCategory, int)> l, int k, int want, string what)
        {
            if (want <= 0 || l[k].Item2 == want) return false;
            _orig.Add(new Orig { L = l, Index = k, Count = l[k].Item2, Want = want, What = what });
            Set(l, k, want);
            return true;
        }

        private static void Set(List<(ItemCategory, int)> l, int k, int count)
        {
            if (l == null || k < 0 || k >= l.Count) return;
            l[k] = (l[k].Item1, count);
        }

        private static string Desc(List<(ItemCategory, int)> ins)
        {
            var s = new List<string>();
            foreach (var i in ins) if (i.Item1 != null) s.Add(i.Item2 + "x" + i.Item1.StringId);
            return string.Join("+", s);
        }
    }
}
