using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// N5-B (poprawka 5 po biegu rocznym 364 doby, decyzja Jeffa 13.10; RAPORT-ROCZNY-2026-10-13 niespojnosc 13) - SKORA Z KAZDEGO UBOJU.
    /// Skory surowe nie maja zrodla we wsi: powstaja tylko z uboju w miastach (ukryci rzemieslnicy "artisans" i warsztaty rzeznikow). Ukryty warsztat ma dwie
    /// linie na to samo zwierze - gry (SandBox spworkshops.xml: owca -> 1 mieso, swinia -> 2 miesa, BEZ skory, szybkosc 4 i 8) i Banner Kings (workshops.xml:
    /// owca -> mieso + skora + welna, swinia -> 2 miesa + skora, szybkosc 2 i 4). Linie gry sa dwa razy szybsze i zabieraja zwierzeta pierwsze: bieg roczny,
    /// doba 364 - 54 swinie i 44 owce dziennie ubite bez skory (ksiega "Towary: hog / sheep" - "hog>2meat 54", "sheep>meat 44") przy 320 skorach dziennie
    /// z calego uboju i zuzyciu 382 (zapas skor -62 na dobe, 43 miasta bez skory wyprawionej).
    /// Regula: linia ukrytych rzemieslnikow z JEDNYM wsadem - owca, swinia albo krowa - ktora daje mieso bez skory, dostaje 1 skore na zwierze (jak linie BK).
    /// Skora idzie zwykla droga wyrobu: polka miasta, kasa miasta placi rzemieslnikom cene polki (BK ProduceOutputPrefix, ArtisanInputs 147 i E8/B dla
    /// dodatkowych cykli) - sztuka z prawdziwego zwierzecia, zloto miedzy dwiema kasami. Welny linia gry nie dostaje (welna z uboju - E8/B).
    /// Jak: raz na sesje po WorkshopRecipes.Apply; lista wyrobow linii (obiekt z XML wspolny dla kopii struktury Production) dostaje wpis na koncu - pierwszy
    /// wyrob (mieso) dalej ustala liczbe cykli. Typy warsztatow nie ida do zapisu - stary zapis wczytuje sie bez zmian. Reset i wylaczony klucz zdejmuja wpis.
    /// Wylacznik: SlaughterKeepsHides (czytany przy starcie sesji; off = receptury gry).
    /// </summary>
    internal static class SlaughterHides
    {
        // lista wyrobow linii (obiekt z XML, porownanie po referencji) - dopisany wpis skory jest ostatni
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
                    if (i >= 0 && l[i].Item1 != null && l[i].Item1.StringId == "hides") l.RemoveAt(i);
                }
                catch (Exception e) { _stumbles++; if (_stumbles <= 1) Log.Error("SlaughterHides.Restore", e); }
            }
            _added.Clear();
            try { GoodsLedger.ForgetLines(); } catch { }   // opisy linii w ksiedze towarow od nowa
        }

        /// <summary>Z ArmouryBehavior.OnSessionLaunched zaraz po WorkshopRecipes.Apply (wlasny try - nigdy nie rzuca). Linia startowa "Skora z uboju (N5-B):".</summary>
        internal static void Apply()
        {
            try
            {
                Restore();   // druga sesja tego samego procesu / zmiana klucza - zawsze od receptury gry
                var s = Settings.Current;
                if (s == null || !s.SlaughterKeepsHides) { Log.Info("Skora z uboju (N5-B): WYLACZONE (Slaughter Keeps Hides) - linie uboju jak w grze."); return; }
                if (WorkshopType.All == null) { Log.Info("Skora z uboju (N5-B): NIECZYNNE - brak typow warsztatow."); return; }
                ItemCategory hides = null;
                foreach (var c in ItemCategories.All) if (c != null && c.StringId == "hides") { hides = c; break; }
                if (hides == null) { Log.Info("Skora z uboju (N5-B): NIECZYNNE - brak kategorii hides."); return; }
                var parts = new List<string>();
                int had = 0;
                foreach (var wt in WorkshopType.All)
                {
                    if (wt == null || !wt.IsHidden || wt.Productions == null) continue;
                    foreach (var p in wt.Productions)
                    {
                        try
                        {
                            // Production to struct - listy wsadu i wyrobow wspolne (List)
                            if (p.Inputs == null || p.Inputs.Count != 1 || p.Outputs == null || p.Outputs.Count == 0) continue;
                            var inCat = p.Inputs[0].Item1;
                            string id = inCat != null ? inCat.StringId : null;
                            if (id != "sheep" && id != "hog" && id != "cow") continue;
                            bool meat = false, hide = false;
                            foreach (var o in p.Outputs)
                            {
                                if (o.Item1 == null) continue;
                                if (o.Item1.StringId == "meat") meat = true; else if (o.Item1.StringId == "hides") hide = true;
                            }
                            if (!meat) continue;
                            if (hide) { had++; continue; }
                            List<(ItemCategory, int)> outs = p.Outputs;
                            int n = Math.Max(1, p.Inputs[0].Item2);
                            outs.Add((hides, n));
                            _added.Add(outs);
                            parts.Add((wt.StringId ?? "?") + ": " + id + " -> mieso + " + n + " skora (szybkosc " + p.ConversionSpeed.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + ")");
                        }
                        catch (Exception e) { _stumbles++; if (_stumbles <= 1) Log.Error("SlaughterHides.Apply (linia " + (wt.StringId ?? "?") + ")", e); }
                    }
                }
                if (_added.Count > 0) { try { GoodsLedger.ForgetLines(); } catch { } }
                Log.Info("Skora z uboju (N5-B): " + (_added.Count > 0 ? "WLACZONE" : "WLACZONE (nic do zmiany)") + " - linie uboju ukrytych rzemieslnikow bez skory dostaly 1 skore na zwierze: ["
                         + string.Join("; ", parts.ToArray()) + "]; linii uboju ze skora juz w recepturze " + had + "; dopisane " + _added.Count + "; potkniecia " + _stumbles + ".");
            }
            catch (Exception e) { _stumbles++; Log.Error("SlaughterHides.Apply", e); }
        }
    }
}
