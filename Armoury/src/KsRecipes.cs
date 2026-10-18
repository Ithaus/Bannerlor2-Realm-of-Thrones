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
    ///
    /// f4-ksiega-i-skory (noc 17/18.10, koncowy przeglad ekonomii, pakiet P1):
    ///  - KSIEGA (klucz Ks Ledger Current Recipes): GoodsLedger pamieta opis linii (Line: wyroby i wsad jednego cyklu) z pierwszego cyklu sesji, a Ensure zmienia
    ///    liczby w tych samych listach - ksiega liczyla mieso i piwo rzemieslnikow wedlug STAREJ receptury ("cow>4meat+2hides") i pokazywala "mnoznik BK
    ///    rzemieslnikow +907 miesa / +753 piwa" na dobe, ktorych nikt nie zrobil (fin86 d109235). Teraz po wlaczeniu i po przywroceniu receptur
    ///    GoodsLedger.ForgetLines() (jak SlaughterHides), a ArtisanInputs.YieldOf przy recepturach KS daje 1 (jak YieldNow - bez drugiego piwa ZC1).
    ///  - SKORY Z UBOJU (klucz Ks Hides By Table): jedna regula wydajnosci skory dla uboju we wsi i w miescie - tabela swiata KsTab.HideOfLive (kg skory na kg
    ///    zywca) x mieso z receptury wagowej / MeatOfLive: krowa 10 x 0.08 / 0.5 = 1.6, swinia 6 x 0.05 / 0.5 = 0.6, owca 2 x 0.10 / 0.5 = 0.4 skory na sztuke
    ///    (te same liczby co skory wlasnego uboju wsi KS - StartCalibration.Deliver - i przetwory miast ZC5 - SideRatio). Dotad miejska owca dawala cala skore
    ///    (2.5 raza wiecej niz we wsi), a krowa rzeznika 5. Liczba wywolan skory w linii uboju = ceil(skory na cykl) (krowa 2, swinia 1, owca 1; rzeznik
    ///    BK 5/2/2 -> 2/1/1), ulamek w pamieci osady (KsHides, zapis arm_hide_frac): rzemieslnicy BK - ArtisanInputs.OutPost (cel wywolania z licznika osady,
    ///    nadwyzka BK zdjeta ze zwrotem zaplaty jak kazda sztuka bez wsadu), warsztaty rzeznikow - KsHides.ItemsPost (lista wyrobow cyklu bez skor, ktorych nie ma).
    ///    Przywrocenie receptur (KS albo klucz wylaczone) - liczby gry co do sztuki; ulamki zostaja w pamieci osad (mniej niz sztuka na osade).
    /// </summary>
    internal static class KsRecipes
    {
        private sealed class Orig { public List<(ItemCategory, int)> L; public int Index; public int Count; public int Want; public string What; }
        private static readonly List<Orig> _orig = new List<Orig>();
        private static bool _on;
        private static bool _errLogged;

        /// <summary>Czynne (KS zastosowany, klucz) - ArtisanInputs.YieldNow nie dokłada wtedy drugiego piwa ZC1 (receptura juz wedlug wagi).</summary>
        internal static bool Active { get { return _on && _orig.Count > 0; } }

        // f4-ksiega-i-skory: linia uboju (wsad: krowa, swinia albo owca) z wyrobem skory - klucz: lista wyrobow linii (obiekt z XML, porownanie po referencji,
        // jak GoodsLedger.LineOf); Kind 0 krowa, 1 swinia, 2 owca; PerCycle - skory z tabeli na cykl (sztuki zywca x skory na sztuke), Calls - wywolan skory
        // w recepturze (ceil), PerCall = PerCycle / Calls (<= 1, wiec cel wywolania nigdy nie przekracza liczby cykli)
        internal sealed class HideLine { public int Kind; public int Calls; public float PerCycle; public float PerCall; }
        private static readonly Dictionary<object, HideLine> _hideLines = new Dictionary<object, HideLine>();
        private static bool _hidesApplied;
        private static readonly float[] _perHead = new float[3];
        internal static readonly string[] HideKindName = { "krowa", "swinia", "owca" };

        private static bool HidesKeyOn { get { var s = Settings.Current; return s != null && s.KsHidesByTable; } }

        /// <summary>f4: klucz Ks Ledger Current Recipes - ksiega towarow liczy z aktualnych receptur (ForgetLines po zmianie, YieldOf 1 przy recepturach KS).</summary>
        internal static bool LedgerKeyOn { get { var s = Settings.Current; return s != null && s.KsLedgerCurrentRecipes; } }

        /// <summary>f4: regula skor z uboju wedlug tabeli swiata czynna (receptury KS wlaczone razem z kluczem Ks Hides By Table).</summary>
        internal static bool HidesActive { get { return _on && _hidesApplied && _hideLines.Count > 0; } }

        /// <summary>f4: skory na sztuke zywca z tabeli swiata (0 krowa, 1 swinia, 2 owca) - liczone przy wlaczeniu; 0 przed nim.</summary>
        internal static float PerHead(int kind) { return kind >= 0 && kind < 3 ? _perHead[kind] : 0f; }

        /// <summary>f4: linia uboju z wyrobem skory pod regula (outs = WorkshopType.Production.Outputs). false - nie nasza linia albo regula nieczynna.</summary>
        internal static bool TryHideLine(object outs, out HideLine h)
        {
            h = null;
            return outs != null && HidesActive && _hideLines.TryGetValue(outs, out h) && h != null;
        }

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
                bool hides = on && HidesKeyOn;   // f4: skory z uboju wedlug tabeli swiata (tylko razem z recepturami KS)
                if (on && _on && hides != _hidesApplied)
                {
                    // f4: zmiana klucza w sesji - od receptury gry i ponizej od nowa (wlaczenie w tej samej dobie)
                    if (_orig.Count > 0) Restore(" Powod: klucz Ks Hides By Table " + (hides ? "WLACZONY" : "WYLACZONY") + " - receptury od nowa w tej dobie.");
                    else { _on = false; _hidesApplied = false; _hideLines.Clear(); }
                }
                if (!on)
                {
                    if (_orig.Count == 0) { _on = false; _hidesApplied = false; _hideLines.Clear(); return; }
                    Restore("");
                    return;
                }
                if (_on) return;
                var parts = new List<string>();
                var hideParts = new List<string>();   // f4: linie uboju ze skora pod regula tabeli swiata
                if (hides) PerHeadInit();
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
                            if (hides && outId == "hides")
                            {
                                // f4: skory z uboju - wywolan skory w linii = ceil(sztuki zywca x skory na sztuke z tabeli swiata), ulamek w pamieci osady (KsHides)
                                int kind = KindOf(inId);
                                if (kind < 0 || !(_perHead[kind] > 0f) || _hideLines.ContainsKey(outs)) continue;
                                int heads = Math.Max(1, ins[0].Item2);
                                double perCycle = (double)heads * _perHead[kind];
                                int calls = Math.Max(1, (int)Math.Ceiling(perCycle - 0.0001));
                                int was = outs[k].Item2;
                                bool ch = Change(outs, k, calls, wt.StringId + ": " + inId + " -> hides (tabela swiata)");
                                _hideLines[outs] = new HideLine { Kind = kind, Calls = calls, PerCycle = (float)perCycle, PerCall = (float)(perCycle / calls) };
                                hideParts.Add(wt.StringId + " " + inId + "->hides " + (ch ? was + "->" : "") + calls + " wywol. ("
                                              + perCycle.ToString("0.00", CultureInfo.InvariantCulture) + " szt./cykl)");
                                continue;
                            }
                            if (!Rule.TryGetValue(inId + ">" + outId, out r)) continue;
                            bool a = Change(ins, 0, r.Key, wt.StringId + ": wsad " + inId), b = Change(outs, k, r.Value, wt.StringId + ": " + inId + " -> " + outId);
                            if (a || b) { lines++; parts.Add(wt.StringId + " " + inId + "->" + outId + " " + r.Key + ":" + r.Value); }
                        }
                    }
                }
                _on = true;
                _hidesApplied = hides;
                Log.Info("KS: receptury jedzenia wedlug wagi (jak tabela przetworow Planu; klucz Ks Real Recipes) - linii zmienionych " + lines
                         + (parts.Count > 0 ? ": " + string.Join(", ", parts) : "") + (hides ? "; welna bez zmian, skory z uboju - linia nizej." : "; skory i welna bez zmian."));
                if (hides)
                {
                    var ci = CultureInfo.InvariantCulture;
                    Log.Info("KS: skory z uboju w miescie wedlug tabeli swiata (klucz Ks Hides By Table) - skory na sztuke zywca: krowa " + _perHead[0].ToString("0.00", ci)
                             + ", swinia " + _perHead[1].ToString("0.00", ci) + ", owca " + _perHead[2].ToString("0.00", ci)
                             + " (KsTab.HideOfLive x mieso z receptury wagowej / MeatOfLive - jak skory wlasnego uboju wsi KS i przetwory miast ZC5); linii uboju ze skora "
                             + _hideLines.Count + (hideParts.Count > 0 ? ": " + string.Join(", ", hideParts) : "")
                             + "; ulamki skory w pamieci osady (zapis arm_hide_frac), rzemieslnicy BK - cel wywolania z licznika osady, rzeznicy - lista wyrobow cyklu bez skor, ktorych nie ma; linia doby 'Skory z uboju (KS-S)'.");
                }
                if (LedgerKeyOn) { try { GoodsLedger.ForgetLines(); } catch { } }   // f4: opisy linii ksiegi od nowa - receptury wlasnie zmienione (jak SlaughterHides)
            }
            catch (Exception e)
            {
                if (!_errLogged) { _errLogged = true; Log.Error("KsRecipes", e); }
            }
        }

        /// <summary>f4: przywraca receptury gry (wszystkie zmienione liczby), czysci linie skor; why - dopisek do linii logu ("" = linia jak dotad).</summary>
        private static void Restore(string why)
        {
            foreach (var o in _orig) Set(o.L, o.Index, o.Count);
            Log.Info("KS: receptury jedzenia PRZYWROCONE do gry (" + _orig.Count + " liczb w liniach warsztatow)." + why);
            _orig.Clear(); _on = false;
            _hidesApplied = false; _hideLines.Clear();
            if (LedgerKeyOn) { try { GoodsLedger.ForgetLines(); } catch { } }   // f4: opisy linii ksiegi od nowa - receptury gry wrocily
        }

        private static int KindOf(string animal) { return animal == "cow" ? 0 : animal == "hog" ? 1 : animal == "sheep" ? 2 : -1; }

        /// <summary>f4: skory na sztuke zywca z tabeli swiata = sztuk miesa z receptury wagowej (Rule "zwierze>meat") x HideOfLive / MeatOfLive x kg miesa / kg skory
        /// (kg z przedmiotow gry jak KS - Weight, inaczej 10 kg). Krowa 10 x 0.08 / 0.5 = 1.6, swinia 6 x 0.05 / 0.5 = 0.6, owca 2 x 0.10 / 0.5 = 0.4.</summary>
        private static void PerHeadInit()
        {
            double kgMeat = 10.0, kgHides = 10.0;
            try
            {
                var om = TaleWorlds.ObjectSystem.MBObjectManager.Instance;
                var m = om != null ? om.GetObject<ItemObject>("meat") : null;
                var h = om != null ? om.GetObject<ItemObject>("hides") : null;
                if (m != null && m.Weight > 0.05f) kgMeat = m.Weight;
                if (h != null && h.Weight > 0.05f) kgHides = h.Weight;
            }
            catch { }
            string[] ids = { "cow", "hog", "sheep" };
            int[] g = { KsTab.GCow, KsTab.GHog, KsTab.GSheep };
            for (int i = 0; i < 3; i++)
            {
                KeyValuePair<int, int> r;
                _perHead[i] = Rule.TryGetValue(ids[i] + ">meat", out r) && r.Key > 0
                    ? (float)((double)r.Value / r.Key * KsTab.HideOfLive(g[i]) / KsTab.MeatOfLive * kgMeat / kgHides) : 0f;
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
