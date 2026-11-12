using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// 435-L10 - odczyty sprzetu wsi dla MIGRACJI ZA PRACA (WorkMigration.cs). Sam odczyt stanu L3-L5 pod Gate, zadnej zmiany sprzetu ani potrzeb.
    /// Liczby z ostatniego BeforeProduction (wczorajsza doba - migracja biegnie przed nim, po OutlawLaw.Daily).
    /// </summary>
    internal static partial class VillageGear
    {
        /// <summary>Srednie m okregow (wazone rekami, jak "m srednie wazone" linii L3) z ostatniej doby - tylko okregi z droga produkcji i czesciami LP.
        /// Klucz - id osady wsi gry. Nie rzuca.</summary>
        internal static void MigDistrictM(Dictionary<string, double> into, int today)
        {
            into.Clear();
            try
            {
                lock (Gate)
                {
                    foreach (var kv in _byDist)
                    {
                        double wH = 0, wM = 0;
                        foreach (var r in kv.Value)
                        {
                            if (r == null || !r.Path || r.SeenDay < today - 2 || !(r.WLp > 1e-9)) continue;
                            double hN = r.NewCraft >= 0 ? Math.Max(0.0, r.HN) : 0.0;
                            double hE = hN > 0 ? Math.Max(0.0, r.Hs - hN) : Math.Max(0.0, r.Hs);
                            wH += hE; wM += hE * MAt(r.Craft, r.Ratio);
                            if (hN > 0 && r.NewCraft >= 0) { wH += hN; wM += hN * MAt(r.NewCraft, r.RatioNew); }
                        }
                        if (wH >= 1.0) into[kv.Key] = Math.Max(0.0, Math.Min(1.0, wM / wH));
                    }
                }
            }
            catch (Exception e) { Stumble("MigDistrictM", e); into.Clear(); }
        }

        /// <summary>
        /// Okreg wsi gry dla migracji: `need` - rece bez sprzetu (ludzie; suma potrzeb czesci z niedoborem, bez nowego zajecia w zmianie), `cap` - ilu ludzi okreg
        /// przyjmie, nie tworzac ani jednej reki bez sprzetu: przybysi rozkladaja sie na czesci jak ludzie w domu (VillageBook - wedlug osad stojacych), wiec
        /// cap = min po czesciach LP (bezczynny sprzet czesci / jej udzial w ludziach w domu). Bezczynny sprzet czesci = K - max(srednia rak, rece dzis) - ludzie poza
        /// domem (uchodzcy: sprzet czeka na nich, II.4 / II.16); czesc w zmianie zajecia, w domykaniu bilansu otwarcia albo bez drogi materialu - 0 (okreg nie przyjmuje).
        /// Czesc poza LP (stado) nie ogranicza (m = 1 przy kazdej liczbie rak). false - klucz wylaczony, okreg bez wpisu albo bez swiezej doby. Nie rzuca.
        /// 435-L10-p (recenzja): `needNow` - rece bez sprzetu liczone z ludzi w domu DZIS: min(srednia rak, rece dzis) zamiast samej sredniej (srednia z 7 dob
        /// po przebiegu sprzed tygodnia widzi tylko ok. 63% ludzi, ktorzy wyszli) - tyle najwyzej wychodzi ze zrodla; ta sama ostroznosc co max(srednia, dzis) u celu.
        /// `need` (cel: wies z rekami bez sprzetu nie jest celem) - bez zmian, ze sredniej.
        /// </summary>
        internal static bool MigOf(Village v, List<VillageBook.PartView> book, int today, bool road, out double need, out double cap, out double needNow)
        {
            need = 0; cap = 0; needNow = 0;
            if (v == null || v.Settlement == null || v.Settlement.StringId == null || book == null || book.Count == 0 || !On) return false;
            try
            {
                lock (Gate)
                {
                    List<Rec> list;
                    if (!_byDist.TryGetValue(v.Settlement.StringId, out list) || list == null) return false;
                    double home = 0;
                    foreach (var p in book) home += Math.Max(0.0, p.Home);
                    if (!(home >= 1.0)) return false;
                    double c = double.MaxValue; bool anyLp = false, fresh = false;
                    foreach (var p in book)
                    {
                        Rec r = Find(list, DistrictWorks.PartKey(p.Uid));
                        if (r == null) continue;
                        if (r.SeenDay >= today - 2) fresh = true;
                        need += Math.Max(0.0, r.Need);
                        if (r.Need > 0)
                        {
                            double hs = Math.Min(Math.Max(0.0, r.Hs), Math.Max(0.0, p.Home));
                            double hE = r.NewCraft >= 0 && r.HN > 0 ? Math.Max(0.0, hs - r.HN) : hs;
                            needNow += Math.Min(r.Need, Math.Max(0.0, hE - r.K));
                        }
                        if (!(r.WLp > 1e-9)) continue;                                 // czesc poza LP - bez ograniczenia
                        double s = Math.Max(0.0, p.Home) / home;
                        if (!(s > 1e-9)) continue;                                     // czesc bez ludzi w domu nie dostanie przybyszow
                        anyLp = true;
                        double idle = 0;
                        bool ok = road && r.Path && r.SeenDay >= today - 2 && r.K >= 0 && r.NewCraft < 0 && today - r.Day0 >= OpenDays && !(r.Ratio < 1.0);
                        if (ok) idle = r.K - Math.Max(r.Hs, p.Home) - Math.Max(0.0, p.Away);
                        c = Math.Min(c, Math.Max(0.0, idle) / s);
                    }
                    if (!fresh) { need = 0; needNow = 0; return false; }
                    cap = anyLp && c < double.MaxValue ? Math.Max(0.0, c) : 0.0;
                    return true;
                }
            }
            catch (Exception e) { Stumble("MigOf", e); need = 0; cap = 0; needNow = 0; return false; }
        }
    }
}
