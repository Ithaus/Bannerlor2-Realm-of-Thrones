using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// KSIEGA RUDY I DREWNA (wpis 94, przebudowana wpisem 98). Tylko log - niczego nie zmienia w grze.
    /// Wpis 94 zapisywal wynik MODELU x mnoznik przy pierwszym wywolaniu modelu na wies - a to nie bylo wydobycie:
    /// mnoznik sie dodawal zamiast mnozyc (gra dostawala ok. 1/3), model woluja tez podpowiedzi i pojemnosc magazynu
    /// (takze w doby, gdy tick produkcji stoi), a BK ma mineral wsi gorniczej na liscie produkcji dwa razy.
    /// Teraz liczymy to, co FAKTYCZNIE dopisano osadom (zdarzenie gry OnItemProduced - BK wola je z liczba sztuk),
    /// obok wynik modelu do porownania, zuzycie warsztatow zbrojnych (WorkshopLaw), zuzycie linii towarowych gry
    /// (OnItemConsumed: narzedzia z rudy, deski z drewna) i zapas rozbity na miasta / zamki / wsie / tabory.
    /// "Bez wyjasnienia" = zmiana calego zapasu - (dopisane - zuzyte): kucie i przetop gracza, spalone wsie, cudze mody.
    /// Poprawki po fundamencie (docs/EKONOMIA-FUNDAMENT-2026-10-05.md, B2 i C3): osobna pozycja "budowy" - material, ktory
    /// nasz BuildFunding zdejmuje z targow (dotad ok. 1646 ladunkow drewna dziennie siedzialo w "bez wyjasnienia");
    /// "tabory" rozbite na wiesniakow / karawany / lordow / inne (kto trzyma towar w drodze); "model" liczony tylko
    /// we wsiach z wynikiem powyzej zera (dotad "w 92 wsiach" przy 26 kopalniach - model pytany takze o wsie bez rudy).
    /// </summary>
    internal static class OreLedger
    {
        private sealed class Book
        {
            public readonly string Id, Name, TownSource, LineName;
            public int Villages, Towns, Lines, Shops, Builds;      // dopisane wsiom / miastom i zamkom, zuzyte przez linie towarowe / warsztaty zbrojne / budowy
            public float Model;
            public readonly HashSet<Settlement> Makers = new HashSet<Settlement>();
            public readonly HashSet<Village> Modelled = new HashSet<Village>();
            public int LastAll = -1, LastTowns = -1;

            public Book(string id, string name, string townSource, string lineName) { Id = id; Name = name; TownSource = townSource; LineName = lineName; }
            public void NewDay() { Villages = 0; Towns = 0; Lines = 0; Shops = 0; Builds = 0; Model = 0f; Makers.Clear(); Modelled.Clear(); }
            public void Reset() { NewDay(); LastAll = -1; LastTowns = -1; }
        }

        private static readonly Book _iron = new Book("iron", "Ruda", "kopalnie miast i zamkow", "narzedzia");
        private static readonly Book _wood = new Book("hardwood", "Drewno", "miasta i zamki", "deski");

        private static Book Of(ItemObject it)
        {
            if (it == null) return null;
            string id = it.StringId;
            return id == "iron" ? _iron : (id == "hardwood" ? _wood : null);
        }

        internal static void Reset() { _iron.Reset(); _wood.Reset(); }

        /// <summary>StartStock przeliczyl zapas startowy na ladunki: liczniki zebrane do tej chwili pochodza z tickow startowych nowej
        /// kampanii i sa w sztukach z gry (10 kg), a ksiega liczy w ladunkach. Oddajemy je do logu przeliczenia i zerujemy -
        /// pierwsza linia ksiegi opisuje wtedy sama pierwsza dobe. Wczytana kampania: liczniki sa puste, zwraca "".</summary>
        internal static string TakeStartTicks(ItemObject item)
        {
            try
            {
                var b = Of(item);
                if (b == null) return "";
                // Shops (warsztaty zbrojne) i Builds (budowy, wpis 102) przy starcie gry stoja na zerze (WorkshopLaw puszcza wtedy
                // vanille, BuildFunding jeszcze nie kupuje), ale NewDay zeruje i te liczniki - gdyby cos w nich bylo, ma trafic
                // do logu, a nie zniknac po cichu
                string s = b.Villages == 0 && b.Towns == 0 && b.Lines == 0 && b.Shops == 0 && b.Builds == 0 && b.Model <= 0f ? ""
                    : "wsiom dopisano " + b.Villages + " (" + b.Makers.Count + " wsi; model " + b.Model.ToString("0.#") + " w " + b.Modelled.Count + " wsiach), "
                      + b.TownSource + " +" + b.Towns + ", wsad linii towarowych (" + b.LineName + ") " + b.Lines
                      + (b.Shops != 0 ? ", warsztaty zbrojne " + b.Shops : "")
                      + (b.Builds != 0 ? ", budowy " + b.Builds : "");
                b.NewDay();
                return s;
            }
            catch { return ""; }
        }

        /// <summary>Wynik modelu produkcji wsi PO naszym mnozniku (MaterialLaw.ProdPostfix): pierwszy DODATNI na wies w dobie ksiegi
        /// (model jest pytany takze o wsie, ktore tego towaru nie daja - wynik 0 nie robi z nich "wsi w modelu").</summary>
        internal static void NoteModel(Village v, ItemObject item, float amount)
        {
            try
            {
                var b = Of(item);
                if (b == null || v == null || amount <= 0f || !b.Modelled.Add(v)) return;
                b.Model += amount;
            }
            catch { }
        }

        /// <summary>Zdarzenie gry: sztuki faktycznie dopisane osadzie (wies = wydobycie, miasto albo zamek = kopalnia lub warsztat).</summary>
        internal static void OnProduced(ItemObject item, Settlement st, int count)
        {
            try
            {
                var b = Of(item);
                if (b == null || st == null || count <= 0) return;
                if (st.IsVillage) { b.Villages += count; b.Makers.Add(st); }
                else b.Towns += count;
            }
            catch { }
        }

        /// <summary>Zdarzenie gry: wsad linii towarowej warsztatu (kuznia narzedzi, tartak). Warsztaty zbrojne ida przez NoteWorkshop.</summary>
        internal static void OnConsumed(ItemObject item, Settlement st, int count)
        {
            try { var b = Of(item); if (b != null && count > 0) b.Lines += count; }
            catch { }
        }

        internal static void NoteWorkshop(ItemObject item, int loads)
        {
            var b = Of(item);
            if (b != null && loads > 0) b.Shops += loads;
        }

        /// <summary>Material zdjety z targu przez nasze budowy (BuildFunding.BuyMaterials / BuyOneCheapest). Tylko licznik.</summary>
        internal static void NoteBuild(ItemObject item, int loads)
        {
            try { var b = Of(item); if (b != null && loads > 0) b.Builds += loads; }
            catch { }
        }

        internal static void Daily()
        {
            try { Line(_iron); Line(_wood); }
            catch (Exception e) { Log.Error("OreLedger", e); }
        }

        private static string Signed(int n) { return (n >= 0 ? "+" : "") + n; }

        private static void Line(Book b)
        {
            var it = MBObjectManager.Instance.GetObject<ItemObject>(b.Id);
            if (it == null) { b.NewDay(); return; }
            int towns = 0, castles = 0, villages = 0, road = 0, empty = 0, townCount = 0;
            int roadVillagers = 0, roadCaravans = 0, roadLords = 0, roadOther = 0, roadBandits = 0;
            foreach (var st in Settlement.All)
            {
                if (st == null || st.ItemRoster == null) continue;
                int n = st.ItemRoster.GetItemNumber(it);
                if (st.IsTown) { towns += n; townCount++; if (n <= 0) empty++; }
                else if (st.IsCastle) castles += n;
                else if (st.IsVillage) villages += n;
            }
            foreach (var mp in MobileParty.All)
            {
                if (mp == null || mp.ItemRoster == null) continue;
                int n = mp.ItemRoster.GetItemNumber(it);
                if (n == 0) continue;
                road += n;
                if (mp.IsVillager) roadVillagers += n;
                else if (mp.IsCaravan) roadCaravans += n;
                else if (mp.IsLordParty) roadLords += n;
                else { roadOther += n; if (mp.IsBandit) roadBandits += n; }
            }

            float kg = Math.Max(0.1f, it.Weight);
            int all = towns + castles + villages + road;
            var sb = new StringBuilder();
            sb.Append(b.Name).Append(": dzien ").Append((int)CampaignTime.Now.ToDays - 1)
              .Append(" - wsie dopisaly ").Append(b.Villages).Append(" ladunkow (").Append((b.Villages * kg / 1000f).ToString("0.0"))
              .Append(" t, ").Append(b.Makers.Count).Append(" wsi; model ").Append(b.Model.ToString("0.#")).Append(" w ").Append(b.Modelled.Count)
              .Append(" wsiach z wynikiem > 0), ").Append(b.TownSource).Append(" +").Append(b.Towns)
              .Append("; zuzycie: warsztaty zbrojne ").Append(b.Shops).Append(", linie towarowe (").Append(b.LineName).Append(") ").Append(b.Lines)
              .Append(", budowy ").Append(b.Builds)
              .Append("; zapas: miasta ").Append(towns);
            if (b.LastTowns >= 0) sb.Append(" (").Append(Signed(towns - b.LastTowns)).Append(")");
            sb.Append(", zamki ").Append(castles).Append(", wsie ").Append(villages).Append(", tabory ").Append(road)
              .Append(" (wiesniacy ").Append(roadVillagers).Append(", karawany ").Append(roadCaravans).Append(", lordowie ").Append(roadLords)
              .Append(", inne ").Append(roadOther).Append(" - w tym bandy ").Append(roadBandits).Append("), razem ").Append(all);
            if (b.LastAll >= 0)
            {
                int delta = all - b.LastAll;
                int known = b.Villages + b.Towns - b.Shops - b.Lines - b.Builds;
                sb.Append(" (").Append(Signed(delta)).Append(", bez wyjasnienia ").Append(Signed(delta - known)).Append(")");
            }
            sb.Append("; miast bez towaru ").Append(empty).Append(" z ").Append(townCount)
              .Append("; zima: ").Append(WinterBite.WinterNow() ? "TAK" : "nie")
              .Append(". Ladunek = ").Append(kg.ToString("0")).Append(" kg.");
            Log.Info(sb.ToString());
            b.LastAll = all; b.LastTowns = towns; b.NewDay();
        }
    }
}
