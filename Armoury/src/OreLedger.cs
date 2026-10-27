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
    /// Od wpisu 105 (MineralOnce): BK ma mineral na liscie produkcji wsi RAZ, a wynik modelu jest mnozony przez liczbe
    /// zdjetych powtorzen (MineralOnce.Times) - wies kopiaca dopisuje wiec dokladnie tyle, ile pokazuje "model"
    /// ("wsie dopisaly" / liczba kopiacych wsi = "model" / liczba wsi w modelu; dotad dopisywala dwa razy tyle).
    /// Wyjatek: po wczytaniu gry mnoznik wsi pojawia sie dopiero przy pierwszym zlozeniu jej listy przez BK (tick produkcji),
    /// a magazyn wsi pyta model wczesniej - w pierwszej dobie "model" pokazuje wiec jeszcze wynik liczony raz (polowe).
    /// Paczka 126: drewno z lasu wsi (VillageWoodlot - kazda wies bez drwali) to osobna pozycja "las wsi" (NoteWoodlot): "wsie dopisaly"
    /// zostaje wydobyciem drwali porownywalnym z "model", a "bez wyjasnienia" liczy las wsi jak kazde dopisanie.
    /// fix-ksiega (noc 17/18.10, klucz OreLedgerTownCraftsColumn): wsad rzemiosla miasta - przetwory ZC5 (zelazo -> narzedzia przy KS) i rzemioslo 148 - wola
    /// OnItemConsumed tak samo jak linie warsztatow gry, ale ksiega towarow liczy go w ramce rzemiosla miasta (FTownCraft, "rzemioslo miasta (148)"), nie w "linie
    /// warsztatow" - kontrola ksiegi towarow byla NIEZGODNA w kazdej dobie z wsadem zelaza ZC5 (fin83: 52 z 119, roznica = wsad co do sztuki). W nawiasie
    /// TownCrafts.OnDailyTickTown (ta sama chwila co ramka FTownCraft ksiegi towarow) zdarzenie idzie do osobnej pozycji "przetwory miast (ZC5/148)"; IronLands
    /// nie dostaje go drugi raz jako "linie towarowe" (ten sam wsad juz jest w MaterialOrders.NoteUse - "rzemioslo"). Jeden ruch = jedna pozycja w obu ksiegach.
    /// </summary>
    internal static class OreLedger
    {
        private sealed class Book
        {
            public readonly string Id, Name, TownSource, LineName;
            public int Villages, Towns, Lines, Shops, Builds;      // dopisane wsiom / miastom i zamkom, zuzyte przez linie towarowe / warsztaty zbrojne / budowy
            public int Woodlot;                                    // paczka 126: drewno z lasu wsi (VillageWoodlot) - osobno od wydobycia drwali ("wsie dopisaly" i "model")
            public int Fletch;                                     // paczka 172: ruda i drewno strzelarzy miasta (TownFletchers) - osobno od warsztatow zbrojnych
            public int Scrap;                                      // 174, pytanie 4: ruda ze zlomu starego nadmiaru (ArmsScrap) - zrodlo
            public int DrillScrap;                                 // musztra: ruda ze zlomu zapasu cwiczebnego odkupiona przez kowali miasta (Drill) - zrodlo
            public int BloomWood;                                  // E6/R1 (214): drewno zdjete z polek miast przez dymarki przy kopalniach (Bloomery) - ujscie
            public int Coal;                                       // ZC1 (CharcoalBurners): drewno z magazynow wsi drwali wypalone na wegiel - ujscie
            public int Ingot;                                      // ZC1 (IronIngotsOffShelf): zelazo z przekutych sztab z polek osad - zrodlo
            public int Crafts;                                     // fix-ksiega: wsad rzemiosla miasta (przetwory ZC5, rzemioslo 148) - osobno od linii towarowych gry
            public float Model;
            public readonly HashSet<Settlement> Makers = new HashSet<Settlement>();
            public readonly HashSet<Settlement> WoodlotMakers = new HashSet<Settlement>();
            public readonly HashSet<Village> Modelled = new HashSet<Village>();
            public int LastAll = -1, LastTowns = -1;
            public int[] Shown; public int ShownDay = -1;           // paczka 146: liczby ostatniej wydrukowanej linii - kontrola ksiegi towarow (GoodsLedger)
            public long[] Folk; public int FolkDay = -1;            // 282 (F5): drewno - [dawne bez wyjasnienia, mieszczanie, pola mieszczan (KS), naprawy kowali, inne pozycje] z dzisiejszej linii

            public Book(string id, string name, string townSource, string lineName) { Id = id; Name = name; TownSource = townSource; LineName = lineName; }
            public void NewDay() { Villages = 0; Towns = 0; Lines = 0; Shops = 0; Builds = 0; Woodlot = 0; Fletch = 0; Scrap = 0; DrillScrap = 0; BloomWood = 0; Coal = 0; Ingot = 0; Crafts = 0; Model = 0f; Makers.Clear(); WoodlotMakers.Clear(); Modelled.Clear(); }
            public void Reset() { NewDay(); LastAll = -1; LastTowns = -1; Shown = null; ShownDay = -1; Folk = null; FolkDay = -1; }
        }

        /// <summary>Paczka 146: liczby dzisiejszej linii "Ruda:"/"Drewno:" (wsie, las wsi, miasta i zamki, warsztaty zbrojne, linie towarowe,
        /// budowy, zapas razem) - odczyt dla kontroli ksiegi towarow; null, gdy linia nie byla dzis drukowana.</summary>
        internal static int[] ShownToday(string id)
        {
            var b = id == "iron" ? _iron : (id == "hardwood" ? _wood : null);
            if (b == null || b.Shown == null || b.ShownDay != (int)CampaignTime.Now.ToDays) return null;
            return b.Shown;
        }

        private static readonly Book _iron = new Book("iron", "Ruda", "kopalnie miast i zamkow", "narzedzia");
        private static readonly Book _wood = new Book("hardwood", "Drewno", "miasta i zamki", "deski");

        private static Book Of(ItemObject it)
        {
            if (it == null) return null;
            string id = it.StringId;
            return id == "iron" ? _iron : (id == "hardwood" ? _wood : null);
        }

        internal static void Reset() { _iron.Reset(); _wood.Reset(); IronLands.Reset(); _craftDepth = 0; _w282 = null; }

        // ------------------------------------------------------------ 282: odczyty do linii "Ruda przeplyw (282)" i kontroli ksiegi towarow
        private static string _w282;   // zelazo swiata z dzisiejszej linii "Zelazo (lupy) (214)"/"Ruda:" (zapas razem, zmiana, miasta, karawany)

        /// <summary>282: zelazo swiata z dzisiejszej linii ksiegi rudy (MaterialOrders.Line282 biegnie pozniej w tej samej dobie).</summary>
        internal static string IronWorld282() { return _w282 ?? "brak linii ksiegi rudy"; }

        /// <summary>282 (F5): drewno - liczby dzisiejszej linii "Drewno:" do kontroli ksiegi towarow: [dawne bez wyjasnienia, mieszczanie, pola mieszczan (KS),
        /// naprawy kowali (135), inne pozycje ksiegi towarow] (netto: + zrodlo, - ujscie); null - linia nie byla dzis drukowana albo bez ksiegi towarow.</summary>
        internal static long[] WoodFolkToday()
        {
            var b = _wood;
            return b.Folk != null && b.FolkDay == (int)CampaignTime.Now.ToDays ? b.Folk : null;
        }

        // ------------------------------------------------------------ fix-ksiega: nawias rzemiosla miasta (TownCrafts.OnDailyTickTown)
        private static int _craftDepth;   // > 0 - trwa doba rzemiosla miasta (148 + przetwory ZC5); tylko watek glowny (DailyTickTown)
        private static bool CraftColumnOn { get { var s = Settings.Current; return s == null || s.OreLedgerTownCraftsColumn; } }

        /// <summary>TownCrafts.OnDailyTickTown: poczatek przerobu miasta (ta sama chwila co ramka FTownCraft ksiegi towarow). Para z CraftExit w finally.</summary>
        internal static void CraftEnter() { _craftDepth++; }

        /// <summary>Koniec przerobu miasta (finally) - licznik nie schodzi ponizej zera.</summary>
        internal static void CraftExit() { if (_craftDepth > 0) _craftDepth--; }

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
                string s = b.Villages == 0 && b.Towns == 0 && b.Lines == 0 && b.Shops == 0 && b.Builds == 0 && b.Woodlot == 0 && b.Model <= 0f ? ""
                    : "wsiom dopisano " + b.Villages + " (" + b.Makers.Count + " wsi; model " + b.Model.ToString("0.#") + " w " + b.Modelled.Count + " wsiach), "
                      + (b.Woodlot != 0 ? "las wsi (126) " + b.Woodlot + " (" + b.WoodlotMakers.Count + " wsi), " : "")
                      + b.TownSource + " +" + b.Towns + ", wsad linii towarowych (" + b.LineName + ") " + b.Lines
                      + (b.Shops != 0 ? ", warsztaty zbrojne " + b.Shops : "")
                      + (b.Builds != 0 ? ", budowy " + b.Builds : "")
                      + (b.Crafts != 0 ? ", przetwory miast (ZC5/148) " + b.Crafts : "");
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
                if (st.IsVillage) { b.Villages += count; b.Makers.Add(st); if (b == _iron) IronLands.NoteProduced(st, count); }   // P3/P2a: lupy wedlug ladow (tylko licznik)
                else b.Towns += count;
            }
            catch { }
        }

        /// <summary>Zdarzenie gry: wsad linii towarowej warsztatu (kuznia narzedzi, tartak). Warsztaty zbrojne ida przez NoteWorkshop.
        /// fix-ksiega: w nawiasie rzemiosla miasta (CraftEnter/CraftExit, klucz OreLedgerTownCraftsColumn) - pozycja "przetwory miast (ZC5/148)", bez IronLands.NoteLine
        /// (ten wsad IronLands ma juz z MaterialOrders.NoteUse jako "rzemioslo"); klucz wylaczony - jak dotad (linie towarowe).</summary>
        internal static void OnConsumed(ItemObject item, Settlement st, int count)
        {
            try
            {
                var b = Of(item);
                if (b == null || count <= 0) return;
                if (_craftDepth > 0 && CraftColumnOn) { b.Crafts += count; return; }
                b.Lines += count; if (b == _iron) IronLands.NoteLine(st, count);   // P3/P2a: wsad linii wedlug ladow (tylko licznik)
            }
            catch { }
        }

        /// <summary>Paczka 126: drewno, ktore las wsi (VillageWoodlot) dopisal wsi - sztuki w jednostce chwili, jak OnProduced. Osobna pozycja,
        /// zeby "wsie dopisaly" zostalo wydobyciem drwali porownywalnym z "model"; w "bez wyjasnienia" liczy sie jak kazde dopisanie.</summary>
        internal static void NoteWoodlot(ItemObject item, Settlement st, int count)
        {
            GoodsLedger.NoteWoodlot(item, count);   // paczka 146: ksiega towarow - ta czesc drewna w ramce produkcji wsi to las wsi (tylko licznik)
            try
            {
                var b = Of(item);
                if (b == null || count <= 0) return;
                b.Woodlot += count;
                if (st != null) b.WoodlotMakers.Add(st);
            }
            catch { }
        }

        internal static void NoteWorkshop(ItemObject item, int loads)
        {
            GoodsLedger.NoteArms(item, loads);   // paczka 146: ksiega towarow - wsad warsztatow zbrojnych (ruda, drewno, skora, len, welna) w ramce cyklu (tylko licznik, bez wyjatkow)
            var b = Of(item);
            if (b != null && loads > 0) b.Shops += loads;
        }

        /// <summary>Paczka 172: ruda i drewno zdjete z polki przez strzelarzy miasta (TownFletchers). Osobna pozycja - nie "warsztaty zbrojne"
        /// (te porownuje kontrola ksiegi towarow z ramka cyklu warsztatu); ksiega towarow liczy je w ramce "strzelarze (172)". Tylko licznik.</summary>
        internal static void NoteFletch(ItemObject item, int loads)
        {
            try { var b = Of(item); if (b != null && loads > 0) b.Fletch += loads; }
            catch { }
        }

        /// <summary>174, pytanie 4: ruda ze zlomu starego nadmiaru dopisana polce miasta (ArmsScrap). Tylko licznik.</summary>
        internal static void NoteScrap(ItemObject item, int loads)
        {
            try { var b = Of(item); if (b != null && loads > 0) b.Scrap += loads; }
            catch { }
        }

        /// <summary>Musztra: ruda ze zlomu zapasu cwiczebnego dopisana polce miasta (Drill.SellScrap). Tylko licznik.</summary>
        internal static void NoteDrillScrap(ItemObject item, int loads)
        {
            try { var b = Of(item); if (b != null && loads > 0) b.DrillScrap += loads; }
            catch { }
        }

        /// <summary>E6/R1 (214): drewno kupione z polki miasta handlowego przez dymarke kopalni (Bloomery.Daily) albo spalone przez kopalnie BK
        /// w miescie (Bloomery.TownMineBlocked) - ujscie drewna. Tylko licznik. F4a: takze drewno kupione wozem z dalszego miasta (w chwili zakupu) i wpis
        /// UJEMNY, gdy woz zawraca (drewno wraca na polke zrodla).</summary>
        internal static void NoteBloomeryWood(ItemObject item, int loads)
        {
            try { var b = Of(item); if (b != null && loads != 0) b.BloomWood += loads; }
            catch { }
        }

        /// <summary>ZC1 (GoodsPaths.Coal): ladunki drewna wypalone na wegiel we wsi drwali - ujscie drewna. Tylko licznik.</summary>
        internal static void NoteCoalWood(ItemObject item, int loads)
        {
            try { var b = Of(item); if (b != null && loads > 0) b.Coal += loads; }
            catch { }
        }

        /// <summary>ZC1 (GoodsPaths.Ingots): zelazo (lupy / ruda) z przekutych sztab z polek osad - zrodlo. Tylko licznik.</summary>
        internal static void NoteIngotIron(ItemObject item, int n)
        {
            try { var b = Of(item); if (b != null && n > 0) b.Ingot += n; }
            catch { }
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
            finally { Bloomery.NewDay(); }   // E6/R1 (214): liczniki doby dymarek - po liniach zelaza i drewna
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
            int carts = 0;   // P3/Z1: surowiec w wozach kupcow w drodze (MaterialCarts) - czesc zapasu swiata, inaczej zakup wozem bylby "bez wyjasnienia"
            try { carts = (int)MaterialCarts.InTransitOf(it); } catch { }
            int all = towns + castles + villages + road + carts;
            bool bloom = b == _iron && Bloomery.IsBloom(it);   // E6/R1 (214): "iron" to lupa - linia "Zelazo (lupy) (214)" zamiast "Ruda:"
            var sb = new StringBuilder();
            sb.Append(bloom ? "Zelazo (lupy) (214)" : b.Name).Append(": dzien ").Append((int)CampaignTime.Now.ToDays - 1)
              .Append(" - wsie dopisaly ").Append(b.Villages).Append(bloom ? " lup (" : " ladunkow (").Append((b.Villages * kg / 1000f).ToString("0.0"))
              .Append(" t, ").Append(b.Makers.Count).Append(" wsi; model ").Append(b.Model.ToString("0.#")).Append(" w ").Append(b.Modelled.Count)
              .Append(" wsiach z wynikiem > 0), ").Append(b.TownSource).Append(" +").Append(b.Towns);
            if (bloom) sb.Append(Bloomery.IronSegment(b.Villages));   // E6/R1 (214): lupy z tickow kopalni, wegiel dymarek, kopalnia bez wegla
            if (b == _wood) sb.Append(", las wsi (126) +").Append(b.Woodlot).Append(" (").Append(b.WoodlotMakers.Count).Append(" wsi)");
            if (b.Scrap > 0) sb.Append(", zlom z nadmiaru (174) +").Append(b.Scrap);
            if (b.DrillScrap > 0) sb.Append(", zlom z cwiczen (musztra) +").Append(b.DrillScrap);
            if (b.Ingot > 0) sb.Append(", sztaby z polek (ZC1) +").Append(b.Ingot);
            sb.Append("; zuzycie: warsztaty zbrojne ").Append(b.Shops).Append(", linie towarowe (").Append(b.LineName).Append(") ").Append(b.Lines)
              .Append(", budowy ").Append(b.Builds)
              .Append(", strzelarze (172) ").Append(b.Fletch);
            if (b.Coal > 0) sb.Append(", wegiel drwali (ZC1) ").Append(b.Coal);
            bool craftCol = CraftColumnOn;   // fix-ksiega: klucz wylaczony - linia co do znaku jak dotad
            if (craftCol) sb.Append(", przetwory miast (ZC5/148) ").Append(b.Crafts);
            if (b == _wood && (b.BloomWood > 0 || Bloomery.On)) sb.Append(Bloomery.WoodSegment(b.BloomWood));   // E6/R1 (214): dymarki przy kopalniach (targ - ujscie zapasu; las okregu - poza zapasem)
            if (bloom) sb.Append(Bloomery.SmeltSegment(b.Scrap, b.DrillScrap));
            // 282 (F5): drewno - mieszczanie (opal i koszyk BK), pola mieszczan (KS) i naprawy kowali (135) z ksiegi towarow (te same ramki, ta sama doba - ksiega towarow
            // drukuje zaraz po tej linii); dotad poza "znanym", wiec cale szly do "bez wyjasnienia" (log Jeffa 109002: -1940 + 380 - 2 = -1562). Bez ksiegi towarow - jak dotad
            long fTown = 0, fKs = 0, fMend = 0, fOther = 0;
            bool folk = b == _wood && GoodsLedger.OpenFolk282(b.Id, out fTown, out fKs, out fMend, out fOther);
            if (folk) sb.Append(", mieszczanie i pola (ksiega towarow, 282): mieszczanie ").Append(Signed((int)fTown)).Append(", pola mieszczan (KS) ").Append(Signed((int)fKs))
                         .Append(", naprawy kowali (135) ").Append(Signed((int)fMend)).Append(", inne pozycje ksiegi towarow (zamki, zaopatrzenie BK, ticki) ").Append(Signed((int)fOther));
            sb.Append("; zapas: miasta ").Append(towns);
            if (b.LastTowns >= 0) sb.Append(" (").Append(Signed(towns - b.LastTowns)).Append(")");
            sb.Append(", zamki ").Append(castles).Append(", wsie ").Append(villages).Append(", tabory ").Append(road)
              .Append(" (wiesniacy ").Append(roadVillagers).Append(", karawany ").Append(roadCaravans).Append(", lordowie ").Append(roadLords)
              .Append(", inne ").Append(roadOther).Append(" - w tym bandy ").Append(roadBandits).Append(")");
            if (carts > 0) sb.Append(", wozy kupcow w drodze (P3/Z1) ").Append(carts);
            sb.Append(", razem ").Append(all);
            if (b.LastAll >= 0)
            {
                int delta = all - b.LastAll;
                int known = b.Villages + b.Woodlot + b.Towns + b.Scrap + b.DrillScrap + b.Ingot - b.Shops - b.Lines - b.Builds - b.Fletch - b.BloomWood - b.Coal - b.Crafts;
                int raw = delta - known;
                if (folk) { known += (int)(fTown + fKs + fMend + fOther); b.Folk = new[] { (long)raw, fTown, fKs, fMend, fOther }; b.FolkDay = (int)CampaignTime.Now.ToDays; }   // 282 (F5)
                sb.Append(" (").Append(Signed(delta)).Append(", bez wyjasnienia ").Append(Signed(delta - known)).Append(")");
            }
            sb.Append("; miast bez towaru ").Append(empty).Append(" z ").Append(townCount)
              .Append("; zima: ").Append(WinterBite.WinterNow() ? "TAK" : "nie")
              .Append(bloom ? ". Lupa = " : ". Ladunek = ").Append(kg.ToString("0")).Append(" kg.");
            Log.Info(sb.ToString());
            if (b == _iron) _w282 = "razem " + all + (b.LastAll >= 0 ? " (" + Signed(all - b.LastAll) + ")" : " (pierwszy pomiar)") + ", miasta " + towns + ", karawany " + roadCaravans + ", wozy kupcow " + carts;   // 282
            if (b == _iron) IronLands.Daily(it, (int)CampaignTime.Now.ToDays - 1);   // P3/P2a: linia "Zelazo wedlug ladow" (tylko log; wlasny licznik potkniec)
            // fix-ksiega: przy kluczu osma liczba - przetwory miast (kontrola ksiegi towarow porownuje ja z ramka rzemiosla miasta); bez klucza 7 liczb jak dotad
            b.Shown = craftCol ? new[] { b.Villages, b.Woodlot, b.Towns, b.Shops, b.Lines, b.Builds, all, b.Crafts } : new[] { b.Villages, b.Woodlot, b.Towns, b.Shops, b.Lines, b.Builds, all };
            b.ShownDay = (int)CampaignTime.Now.ToDays;
            b.LastAll = all; b.LastTowns = towns; b.NewDay();
        }
    }
}
