using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// AM-30 (audyt modow 14.10, docs/audyt-modow-2026-10-14/RAPORT-AUDYT-MODOW.md rozdz. 3) - LINIE WARSZTATOW BK BEZ WSADU.
    /// Banner Kings (ModuleData/workshops.xml) wpisuje w czterech liniach id PRZEDMIOTU zamiast kategorii: rzeznik (chicken x2, goose x2 -> mieso),
    /// ukryci rzemieslnicy (chicken x4, goose x4 -> mieso), piekarnia (grain + carrot, grain + orange -> 4 ciasta). Gra przy nieznanej kategorii tylko
    /// wypisuje 'Unable to Find Item Category' (RGL) i dodaje linie BEZ tego wsadu (G WorkshopType.Deserialize): rzeznicy robili ok. 33 miesa dziennie
    /// z niczego, a 59% ciast swiata szlo z samego zboza (bieg dwuletni 06:01, skrypt ws.py).
    /// (A) Regula w WorkshopLaw.FreeRawLine (klucz WorkshopNoFreeRawAllTypes, podlegly WorkshopNoFreeRaw): linia bez wsadu z surowcem jako wyrobem nie
    ///     robi cyklu w kazdym typie warsztatu (notabla i gracza), nie tylko u ukrytych rzemieslnikow; kopalnia (wydobycie) bez zmian. Linii NIE usuwamy
    ///     z Productions (numery linii w zapisach i w oknie warsztatu). Tu tylko linia startu sesji: ktore linie regula obejmuje.
    /// (B) Klucz BkWorkshopInputsFix (domyslnie NIE - do testu z bilansem ciast, zboza i jedzenia miast): raz na sesje linie piekarni "zboze -> ciasta"
    ///     (sam wsad zboza i jedyny wyrob ciasto) dostaja wsad z kategorii 'apple' (carrot i orange to przedmioty tej kategorii), a linie rzeznika bez wsadu
    ///     z samym miesem - 2 sztuki kategorii 'animal' (kategoria kury i gesi). Lista wsadu linii to obiekt z XML wspolny dla kopii struktury Production
    ///     (jak WorkshopRecipes, SlaughterHides) - gra, BK i Armoury widza zmiane od razu. Typy warsztatow nie ida do zapisu; Reset i klucz off zdejmuja wpis.
    /// </summary>
    internal static class WorkshopInputsFix
    {
        private static readonly List<List<(ItemCategory, int)>> _added = new List<List<(ItemCategory, int)>>();
        private static int _stumbles;

        internal static void Reset() { Restore(); _stumbles = 0; }

        private static void Restore()
        {
            foreach (var l in _added)
            {
                try
                {
                    int i = l.Count - 1;
                    if (i >= 0 && l[i].Item1 != null && (l[i].Item1.StringId == "apple" || l[i].Item1.StringId == "animal")) l.RemoveAt(i);
                }
                catch (Exception e) { _stumbles++; if (_stumbles <= 1) Log.Error("WorkshopInputsFix.Restore", e); }
            }
            bool any = _added.Count > 0;
            _added.Clear();
            if (any) { try { GoodsLedger.ForgetLines(); } catch { } }   // opisy linii w ksiedze towarow od nowa
        }

        private static string Recipe(WorkshopType.Production p)
        {
            var sb = new System.Text.StringBuilder();
            if (p.Inputs != null) foreach (var x in p.Inputs) { if (sb.Length > 0) sb.Append('+'); sb.Append(x.Item2 > 1 ? x.Item2 + " " : "").Append(x.Item1 != null ? x.Item1.StringId : "?"); }
            sb.Append('>');
            bool first = true;
            if (p.Outputs != null) foreach (var x in p.Outputs) { if (!first) sb.Append('+'); first = false; sb.Append(x.Item2 > 1 ? x.Item2 + " " : "").Append(x.Item1 != null ? x.Item1.StringId : "?"); }
            return sb.ToString();
        }

        private static bool OnlyOutputs(WorkshopType.Production p, string id)
        {
            if (p.Outputs == null || p.Outputs.Count == 0) return false;
            foreach (var o in p.Outputs) if (o.Item1 == null || o.Item1.StringId != id) return false;
            return true;
        }

        /// <summary>Z ArmouryBehavior.OnSessionLaunched zaraz po SlaughterHides.Apply (wlasny try - nigdy nie rzuca). Linia startowa "Warsztaty bez wsadu (AM-30):".</summary>
        internal static void Apply()
        {
            try
            {
                Restore();   // druga sesja tego samego procesu / zmiana klucza - zawsze od receptury BK
                var s = Settings.Current;
                if (s == null || WorkshopType.All == null) { Log.Info("Warsztaty bez wsadu (AM-30): NIECZYNNE - brak ustawien albo typow warsztatow."); return; }
                // (A) - ktore linie obejmuje regula WorkshopLaw.FreeRawLine (sam odczyt)
                var blocked = new List<string>();
                var free = new List<string>();
                foreach (var wt in WorkshopType.All)
                {
                    if (wt == null || wt.Productions == null) continue;
                    foreach (var p in wt.Productions)
                    {
                        if (p.Inputs != null && p.Inputs.Count > 0) continue;
                        string r = (wt.StringId ?? "?") + ": " + Recipe(p);
                        if (WorkshopLaw.FreeRawRecipe(p, wt.StringId, s)) blocked.Add(r); else free.Add(r);
                    }
                }
                // (B) - wsad z kategorii dla linii piekarni i rzeznika
                var parts = new List<string>();
                if (s.BkWorkshopInputsFix)
                {
                    ItemCategory apple = null, animal = null;
                    foreach (var c in ItemCategories.All)
                    {
                        if (c == null) continue;
                        if (c.StringId == "apple") apple = c; else if (c.StringId == "animal") animal = c;
                    }
                    foreach (var wt in WorkshopType.All)
                    {
                        if (wt == null || wt.Productions == null) continue;
                        string tid = wt.StringId;
                        if (tid != "bakery" && tid != "butcher") continue;
                        foreach (var p in wt.Productions)
                        {
                            try
                            {
                                // Production to struct - lista wsadu wspolna (List)
                                List<(ItemCategory, int)> ins = p.Inputs;
                                if (ins == null) continue;
                                if (tid == "bakery" && apple != null && ins.Count == 1 && ins[0].Item1 != null && ins[0].Item1.StringId == "grain" && OnlyOutputs(p, "pie"))
                                {
                                    string before = Recipe(p);
                                    ins.Add((apple, 1));
                                    _added.Add(ins);
                                    parts.Add("bakery: " + before + " -> " + Recipe(p));
                                }
                                else if (tid == "butcher" && animal != null && ins.Count == 0 && OnlyOutputs(p, "meat"))
                                {
                                    string before = Recipe(p);
                                    ins.Add((animal, 2));
                                    _added.Add(ins);
                                    parts.Add("butcher: " + before + " -> " + Recipe(p));
                                }
                            }
                            catch (Exception e) { _stumbles++; if (_stumbles <= 1) Log.Error("WorkshopInputsFix.Apply (linia " + (tid ?? "?") + ")", e); }
                        }
                    }
                    if (apple == null || animal == null) parts.Add("BRAK kategorii" + (apple == null ? " apple" : "") + (animal == null ? " animal" : ""));
                    if (_added.Count > 0) { try { GoodsLedger.ForgetLines(); } catch { } }
                }
                Log.Info("Warsztaty bez wsadu (AM-30): (A) Workshop No Free Raw " + (s.WorkshopNoFreeRaw ? "TAK" : "NIE") + ", wszystkie typy (Workshop No Free Raw All Types) "
                         + (s.WorkshopNoFreeRaw && s.WorkshopNoFreeRawAllTypes ? "TAK" : "NIE") + " - linie bez wsadu zablokowane " + blocked.Count + " [" + string.Join("; ", blocked.ToArray())
                         + "], bez wsadu poza ta regula (kopalnia - wydobycie; uzbrojenie, odziez, strzaly - WorkshopLaw / TownFletchers) " + free.Count + " [" + string.Join("; ", free.ToArray()) + "] | (B) BK Workshop Inputs Fix "
                         + (s.BkWorkshopInputsFix ? "TAK - dopisane " + _added.Count + " [" + string.Join("; ", parts.ToArray()) + "]" : "NIE (linie piekarni zboze -> ciasta i rzeznika bez wsadu jak czyta je gra)")
                         + "; potkniecia " + _stumbles + ".");
            }
            catch (Exception e) { _stumbles++; Log.Error("WorkshopInputsFix.Apply", e); }
        }
    }
}
