using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// MATERIAL DO NAPRAWY Z TARGU MIASTA (Jeff 07.10: naprawa u kwatermistrza Spoils "placi kasie miasta (kowale), zuzywa material
    /// z targu wedle stanu, wrakow nie odnawia"; plan K13 pkt 139 - od poprawki po audycie TOWARY 3 ta sama regula obejmuje naprawy ludzi gracza
    /// i lordow AI: TroopSelfMend.HourlyWithMaterial / RunWithMaterial, AiWear.MendWithMaterial, wylacznik MendMaterialMenAndLords).
    /// Jedna regula dla naprawy u kowali miasta:
    ///  - ILE: udzial materialu pelnej sztuki = MendMaterialMaxShare x (1 - stan) - ta sama regula co naprawa wlasnymi rekami
    ///    (SmithMenu.SelfMendParts: "naprawiamy, nie kujemy od nowa"); Plundered 55% -> 9%, Damaged 40% -> 12%, Battered 25% -> 15%;
    ///  - Z CZEGO: receptura sztuki z ArmsPricing.CostOf - ta sama, z ktorej warsztaty zbrojne miasta licza wsad (WorkshopLaw.Needs):
    ///    metal w kg surowki (kazdy stopien gatunku x1.25, jak w warsztatach), drewno (trzonki, drzewce, luby + wegiel kuzni
    ///    WorkshopForgeWoodPerMetalKg na kg metalu), skora, plotno (len, a gdy go brak - welna, jak w warsztatach);
    ///  - SKAD METAL: surowka i sztaby z polki (crude iron .. stal, bez valyrianskiej), ZLOM = wraki z polki (polowa metalu receptury -
    ///    regula przetopu Recipes.SmeltYield; unikatow i legend nie topimy) albo RUDA przez dymarke (ladunek rudy daje
    ///    WorkshopCrudeKgPerOre kg surowki z 10 kg i zjada WorkshopWoodPerOre ladunkow drewna na wegiel - jak w warsztatach).
    ///    Kowal kupuje z jednego zrodla tyle calych sztuk, ile najtaniej pokrywa brak (3 sztaby surowki zamiast ladunku rudy na latke
    ///    helmu); gdy zadne zrodlo samo nie pokryje braku - wszystko z najtanszego za kg i dalej z nastepnego;
    ///  - CENA: ta sama co dla warsztatow (WorkshopLaw.MatPrice: ruda, drewno, skora, len, welna; sztaby i wraki - cena targu sztuki),
    ///    z chwili zlecenia; zlecajacy placi za to, co zuzyto (srednia cena zapasu kowali);
    ///  - CALE SZTUKI: kowale zdejmuja z polki cale sztuki (ladunek rudy, skora, bela plotna, wrak); reszta zostaje w ich zapasie
    ///    (miasto, rodzaj: kg i wartosc) na nastepne naprawy w tym miescie - zapisana w grze, nic nie znika i nic nie powstaje;
    ///  - BRAK materialu na polce i w zapasie = sztuka czeka (nic sie nie dzieje, nic nie znika).
    /// Bench liczy na kopii (proba) - polka i zapas zmieniaja sie dopiero w Commit.
    /// </summary>
    internal static class MendMaterial
    {
        internal const int Metal = 0, Wood = 1, Leather = 2, Cloth = 3, Kinds = 4;
        internal static readonly string[] KindName = { "metal", "drewno", "skora", "plotno" };
        internal static readonly string[] KindEn = { "iron (crude iron, scrap or ore)", "wood", "leather", "linen or wool" };
        private const float Eps = 0.0001f;

        /// <summary>Zapas kowali miasta: kg kazdego rodzaju i ich wartosc (cena, za ktora zdjeto je z polki - placi ten, kto zuzyje).</summary>
        private sealed class Stock { public readonly float[] Kg = new float[Kinds], Val = new float[Kinds]; }
        private static readonly Dictionary<string, Stock> _stock = new Dictionary<string, Stock>();

        private static ItemObject _ore, _wood, _leather, _linen, _wool;
        private static readonly ItemObject[] _bars = new ItemObject[5];
        private static readonly int[] _barSteps = { 0, 1, 2, 3, 4 };
        private static bool _resolved;

        /// <summary>Nowa gra albo wczytanie: zapas kowali od zera (wczytanie oddaje go w Import), przedmioty szukane od nowa.</summary>
        internal static void Reset()
        {
            _stock.Clear();
            _resolved = false; _ore = _wood = _leather = _linen = _wool = null;
            Array.Clear(_bars, 0, _bars.Length);
        }

        private static void Resolve()
        {
            if (_resolved) return;
            _resolved = true;
            var om = MBObjectManager.Instance;
            if (om == null) return;
            _ore = om.GetObject<ItemObject>("iron");
            _wood = om.GetObject<ItemObject>("hardwood");
            _leather = om.GetObject<ItemObject>("leather");
            _linen = om.GetObject<ItemObject>("linen");
            _wool = om.GetObject<ItemObject>("wool");
            var mats = new[] { CraftingMaterials.Iron1, CraftingMaterials.Iron2, CraftingMaterials.Iron3, CraftingMaterials.Iron4, CraftingMaterials.Iron5 };
            for (int i = 0; i < mats.Length; i++) { _bars[i] = Recipes.MaterialItem(mats[i]); _barSteps[i] = WorkshopLaw.StepsOf(mats[i]); }
        }

        private static float UnitKg(ItemObject it) { return it != null && it.Weight > 0.05f ? it.Weight : 10f; }

        /// <summary>Udzial pelnej sztuki, jaki zjada naprawa do stanu czystego: MendMaterialMaxShare x (1 - stan).</summary>
        internal static float Share(EquipmentElement el)
        {
            float pm = el.ItemModifier != null ? el.ItemModifier.PriceMultiplier : 1f;
            if (pm < 0f) pm = 0f; if (pm > 1f) pm = 1f;
            var s = Settings.Current;
            return Math.Max(0f, s != null ? s.MendMaterialMaxShare : 0.2f) * (1f - pm);
        }

        /// <summary>Potrzeba na jedna sztuke (kg): [0] metal w kg surowki, [1] drewno, [2] skora, [3] plotno.
        /// null - to nie robota kowala (ArmsPricing nie zna receptury: kon, towar, sztandar).</summary>
        internal static float[] Needs(EquipmentElement el)
        {
            return NeedsShare(el.Item, Share(el));
        }

        /// <summary>To samo dla stanu z ksiegi zuzycia (uprzaz na grzbiecie: brak 0..1, modyfikator zostaje oryginalny).</summary>
        internal static float[] NeedsFor(ItemObject it, float missing)
        {
            var s = Settings.Current;
            return NeedsShare(it, Math.Max(0f, s != null ? s.MendMaterialMaxShare : 0.2f) * Math.Max(0f, Math.Min(1f, missing)));
        }

        private static float[] NeedsShare(ItemObject it, float share)
        {
            var c = it != null ? ArmsPricing.CostOf(it) : null;
            if (c == null) return null;
            var s = Settings.Current;
            float crude = c.MetalKg * (float)Math.Pow(1.25, WorkshopLaw.StepsOf(c.Grade));
            float wood = c.WoodKg + c.MetalKg * Math.Max(0f, s.WorkshopForgeWoodPerMetalKg);
            return new[] { crude * share, wood * share, c.LeatherKg * share, c.LinenKg * share };
        }

        // ------------------------------------------------------------ robota kowali miasta: dniowka historyczna x dobrobyt miasta
        // Jeff 07.10: "stawka robocizny ma byc zalezna od dobrobytu miasta ... na stawkach w naszej grze - nasze stawki sa oparte
        // o stawki historyczne - wszystko, co dotyczy placenia, musi byc spojne". Naprawa = ulamek wykonania sztuki, liczony tak samo
        // jak jej wykonanie w wartosci (HistoricalPrices.HistCost): dni roboty x dniowka mistrza wedle tieru x zysk mistrza; ulamek - ten
        // sam co materialu (Share: MendMaterialMaxShare x zniszczenie); dniowka w tym miescie = dniowka historyczna x LocalWage. Material
        // po cenie targu bez narzutu (kowal kupuje go na targu dla zlecajacego). Jedna stawka dla lawy, kwatermistrza, ludzi i AI.

        /// <summary>Wlaczona regula kowali miasta (SmithMendFromMarket): robota z dniowek, material z targu, bez wrakow.</summary>
        internal static bool RuleOn { get { var s = Settings.Current; return s != null && s.SmithMendFromMarket; } }

        /// <summary>Poprawka po audycie TOWARY 3 (krok 139 planu K13): naprawy ludzi gracza (TroopSelfMend) i lordow AI (AiWear.MendInTown)
        /// tez biora material z targu (Order) - przy regule kowali miasta i wlaczniku MendMaterialMenAndLords.</summary>
        internal static bool MenAndLordsOn { get { var s = Settings.Current; return s != null && s.SmithMendFromMarket && s.MendMaterialMenAndLords; } }

        /// <summary>Wskaznik plac w miescie: dobrobyt / TownWageRefProsperity (mediana miast), 0.5 - 1.5; poza miastem albo przy 0 - 1.</summary>
        internal static float LocalWage(Settlement st) { return TownWage.Index(st); }   // jeden wzor dla calej gry (TownWage)

        /// <summary>Robota naprawy w pensach (bez rabatow): share x robota wykonania (HistoricalPrices.MakingLabor; bez cen historycznych -
        /// ArmsPricing.Labor) x (1 + zysk mistrza) x LocalWage. -1 = brak receptury (wolajacy liczy po staremu).</summary>
        internal static float LaborF(ItemObject it, float share, Settlement st)
        {
            var c = it != null ? ArmsPricing.CostOf(it) : null;
            if (c == null) return -1f;
            var s = Settings.Current;
            bool hist = HistoricalPrices.On;
            float making = hist ? HistoricalPrices.MakingLabor(it, c) : c.Labor;
            float profit = hist ? s.HistProfitPercent : s.SmithProfitPercent;
            return Math.Max(0f, share) * making * (1f + Math.Max(0f, profit) / 100f) * LocalWage(st);
        }

        /// <summary>To samo w calych pensach (najmniej 1); brak receptury - fallback (stara stawka wolajacego).</summary>
        internal static int Labor(ItemObject it, float share, Settlement st, int fallback)
        {
            float x = LaborF(it, share, st);
            return x < 0f ? fallback : Math.Max(1, (int)Math.Round(x));
        }

        /// <summary>Dopisek do podpowiedzi: placa w tym miescie wzgledem zwyklej (pusty przy 95-105%).</summary>
        internal static string WageNote(Settlement st)
        {
            float w = LocalWage(st);
            if (Math.Abs(w - 1f) < 0.05f || st == null) return "";
            return " Wages in " + st.Name + " run at " + (int)Math.Round(w * 100f) + "% of the usual" + (w > 1f ? " - a rich town." : " - a poor town.");
        }

        /// <summary>Material placony w calych pensach, w gore (ulamek pensa za zuzyty material placi zlecajacy, nie miasto).</summary>
        internal static int Gold(float m) { return m <= 0.001f ? 0 : (int)Math.Ceiling(m - 0.001f); }

        /// <summary>Rodzaje z maski braku po angielsku ("iron (...) or leather").</summary>
        internal static string KindsEn(int mask)
        {
            var l = new List<string>();
            for (int k = 0; k < Kinds; k++) if ((mask & (1 << k)) != 0) l.Add(KindEn[k]);
            if (l.Count == 0) return "materials";
            if (l.Count == 1) return l[0];
            return string.Join(", ", l.GetRange(0, l.Count - 1).ToArray()) + " or " + l[l.Count - 1];
        }

        /// <summary>Rodzaje z maski braku po polsku (log).</summary>
        internal static string KindsPl(int mask)
        {
            var l = new List<string>();
            for (int k = 0; k < Kinds; k++) if ((mask & (1 << k)) != 0) l.Add(KindName[k]);
            return l.Count == 0 ? "-" : string.Join(", ", l.ToArray());
        }

        // ------------------------------------------------------------ zlecenie u kowali miasta (naprawa za monete - jedna regula)
        // Lawa naprawcza Armoury u kowala (lup z sakw, sztuka na wybor, zbrojownia wojska, uprzaz na grzbiecie) - a od poprawki po audycie TOWARY 3 (plan K13 pkt 139)
        // naprawy ludzi (TroopSelfMend) i AI (AiWear.MendInTown): Order na zlecenie, AddLot na kazda sztuke (robocizne liczy wolajacy -
        // stawka jego miejsca), Commit, zaplata Total do kasy miasta od tego, kto placi. Regula w jednym miejscu: wrak (LootPrices.IsWreck,
        // wpis 97) - nie za monete; bez receptury kowala - nie; material wedle stanu z polki / zapasu kowali (Bench); brak - sztuka czeka.

        internal const int Done = 1, Waits = 0, Wreck = -2, NoRecipe = -3;   // wynik Quote (NoRecipe: nie robota kowala)

        internal sealed class Job { public EquipmentElement El; public int N, Labor; public float Mat; }

        internal sealed class Order
        {
            public readonly Bench Bench;
            public readonly List<Job> Jobs = new List<Job>();
            public readonly int[] WaitBy = new int[Kinds];
            public int Pieces, Labor, Wrecks, NoSmith, Wait, WaitMask, Poor, CutOff;
            public float Mat;
            public bool Ok { get { return Bench.Ok; } }
            public int MatGold { get { return Gold(Mat); } }
            public int Total { get { return Labor + Gold(Mat); } }

            internal Order(Settlement st) { Bench = new Bench(st); }

            /// <summary>
            /// amount sztuk tego samego rodzaju (ee: przedmiot i stan; need - z Needs(ee) albo NeedsFor dla uprzezy, null = nie robota kowala).
            /// Kazda sztuka osobno: wrak - nie; material z lawy; razem (robocizna + material w calych pensach) najwyzej limit zlota;
            /// najwyzej maxPieces sztuk na zlecenie. Ta sama sztuka i ten sam brak: reszta stosu tez czeka. Zwraca, ile sztuk zaplanowano.
            /// </summary>
            internal int AddLot(EquipmentElement ee, float[] need, int labor, int amount, long limit, int maxPieces)
            {
                if (amount <= 0 || ee.Item == null) return 0;
                if (LootPrices.IsWreck(ee.ItemModifier)) { Wrecks += amount; return 0; }   // wpis 97: wrak - tylko wlasne rece z materialem albo przetop
                if (need == null) { NoSmith += amount; return 0; }
                Job job = null;
                for (int k = 0; k < amount; k++)
                {
                    if (Pieces >= maxPieces) { CutOff += amount - k; break; }
                    float cost; int miss;
                    long sofar = Labor;
                    float mat0 = Mat;
                    int r = Bench.TryMend(need, c => sofar + labor + Gold(mat0 + c) <= limit, out cost, out miss);
                    if (r == 0)
                    {
                        int rest = amount - k;
                        Wait += rest; WaitMask |= miss;
                        for (int m = 0; m < Kinds; m++) if ((miss & (1 << m)) != 0) WaitBy[m] += rest;
                        break;
                    }
                    if (r < 0) { Poor += amount - k; break; }
                    if (job == null) { job = new Job { El = ee }; Jobs.Add(job); }
                    job.N++; job.Labor += labor; job.Mat += cost;
                    Pieces++; Labor += labor; Mat += cost;
                }
                return job != null ? job.N : 0;
            }

            /// <summary>Wycena jednej sztuki bez zmian w lawie (lista i okno wyboru): Done + zloto razem albo Waits (miss) / Wreck / NoRecipe.</summary>
            internal int Quote(EquipmentElement ee, float[] need, int labor, out int total, out float mat, out int miss)
            {
                total = 0; mat = 0f; miss = 0;
                if (ee.Item == null) return NoRecipe;
                if (LootPrices.IsWreck(ee.ItemModifier)) return Wreck;
                if (need == null) return NoRecipe;
                float cost;
                int r = Bench.TryMend(need, c => false, out cost, out miss);   // accept = nie: lawa bez zmian, koszt policzony
                if (r == 0) return Waits;
                mat = cost; total = labor + Gold(cost);
                return Done;
            }

            /// <summary>Co zostalo i dlaczego - po angielsku dla gracza (podpowiedz opcji, komunikat po robocie); pusty, gdy nic nie zostalo.</summary>
            internal string LeftEn(string town)
            {
                var sb = new StringBuilder();
                if (Wait > 0) sb.Append(' ').Append(Wait).Append(Wait == 1 ? " piece waits" : " pieces wait").Append(" for materials - the market of ").Append(town)
                                .Append(" has not enough ").Append(KindsEn(WaitMask)).Append('.');
                if (Wrecks > 0) sb.Append(' ').Append(Wrecks).Append(Wrecks == 1 ? " wreck (Mangled) is" : " wrecks (Mangled) are")
                                  .Append(" not restored for coin - mend wrecks yourself with your own materials, or melt them down.");
                if (NoSmith > 0) sb.Append(' ').Append(NoSmith).Append(NoSmith == 1 ? " piece is no smith's work." : " pieces are no smith's work.");
                if (Poor > 0) sb.Append(' ').Append(Poor).Append(Poor == 1 ? " piece awaits" : " pieces await").Append(" a fuller purse.");
                return sb.ToString();
            }

            /// <summary>Do logu: co zrobiono i co zostalo (po polsku).</summary>
            internal string LogPl()
            {
                var sb = new StringBuilder();
                sb.Append("naprawiono ").Append(Pieces).Append(" szt., zaplata ").Append(Total).Append(" zl do kasy miasta (robocizna ").Append(Labor)
                  .Append(" + material ").Append(MatGold).Append(", wartosc zuzytego materialu ").Append(Bench.UsedValue.ToString("0.00", CultureInfo.InvariantCulture))
                  .Append("); z polki: ");
                if (Bench.TakenById.Count == 0) sb.Append("nic");
                else { bool first = true; foreach (var kv in Bench.TakenById) { if (!first) sb.Append(", "); sb.Append(kv.Key).Append(' ').Append(kv.Value); first = false; } }
                sb.Append("; zuzyto kg: metal ").Append(Bench.UsedKg[Metal].ToString("0.00", CultureInfo.InvariantCulture))
                  .Append(", drewno ").Append(Bench.UsedKg[Wood].ToString("0.0", CultureInfo.InvariantCulture))
                  .Append(", skora ").Append(Bench.UsedKg[Leather].ToString("0.00", CultureInfo.InvariantCulture))
                  .Append(", plotno ").Append(Bench.UsedKg[Cloth].ToString("0.00", CultureInfo.InvariantCulture))
                  .Append("; czeka na material ").Append(Wait).Append(" (brak: ").Append(KindsPl(WaitMask)).Append(")")
                  .Append(", wrakow pominietych ").Append(Wrecks).Append(", nie robota kowala ").Append(NoSmith)
                  .Append(", za malo zlota ").Append(Poor).Append(", poza czasem roboty ").Append(CutOff);
                return sb.ToString();
            }
        }

        // ------------------------------------------------------------ zapis (ArmouryBehavior.SyncData, klucz arm_mendstock)

        internal static string Export()
        {
            var sb = new StringBuilder();
            foreach (var kv in _stock)
            {
                bool any = false;
                for (int k = 0; k < Kinds; k++) if (kv.Value.Kg[k] > Eps) any = true;
                if (!any) continue;
                if (sb.Length > 0) sb.Append(';');
                sb.Append(kv.Key).Append('=');
                for (int k = 0; k < Kinds; k++)
                {
                    if (k > 0) sb.Append(',');
                    sb.Append(kv.Value.Kg[k].ToString("R", CultureInfo.InvariantCulture)).Append('/').Append(kv.Value.Val[k].ToString("R", CultureInfo.InvariantCulture));
                }
            }
            return sb.ToString();
        }

        internal static void Import(string s)
        {
            _stock.Clear();
            if (string.IsNullOrEmpty(s)) return;
            foreach (var p in s.Split(';'))
            {
                var a = p.Split('=');
                if (a.Length != 2 || a[0].Length == 0) continue;
                var parts = a[1].Split(',');
                if (parts.Length != Kinds) continue;
                var st = new Stock();
                bool ok = true;
                for (int k = 0; k < Kinds && ok; k++)
                {
                    var kv = parts[k].Split('/');
                    float kg = 0f, val = 0f;
                    ok = kv.Length == 2 && float.TryParse(kv[0], NumberStyles.Float, CultureInfo.InvariantCulture, out kg)
                         && float.TryParse(kv[1], NumberStyles.Float, CultureInfo.InvariantCulture, out val);
                    if (ok) { st.Kg[k] = Math.Max(0f, kg); st.Val[k] = Math.Max(0f, val); }
                }
                if (ok) _stock[a[0]] = st;
            }
        }

        /// <summary>Do linii dnia: w ilu miastach kowale maja zapas i ile (kg) oraz jego wartosc.</summary>
        internal static string Describe()
        {
            int towns = 0; var kg = new float[Kinds]; float val = 0f;
            foreach (var kv in _stock)
            {
                bool any = false;
                for (int k = 0; k < Kinds; k++) { if (kv.Value.Kg[k] > Eps) any = true; kg[k] += kv.Value.Kg[k]; val += kv.Value.Val[k]; }
                if (any) towns++;
            }
            return "zapas kowali (reszty calych sztuk) w " + towns + " miastach: metal " + kg[Metal].ToString("0.0", CultureInfo.InvariantCulture) + " kg, drewno "
                   + kg[Wood].ToString("0", CultureInfo.InvariantCulture) + " kg, skora " + kg[Leather].ToString("0.0", CultureInfo.InvariantCulture) + " kg, plotno "
                   + kg[Cloth].ToString("0.0", CultureInfo.InvariantCulture) + " kg, wart. " + val.ToString("0", CultureInfo.InvariantCulture) + " zl";
        }

        // ------------------------------------------------------------ lawa kowali jednego zlecenia

        private struct Source { public EquipmentElement El; public float Kg, Price; public int WoodUnits; }

        /// <summary>
        /// Lawa kowali miasta na jedno zlecenie: kopia zapasu, ceny z chwili zlecenia, lista wrakow i sztab z polki.
        /// TryMend planuje jedna sztuke (na kopii; brak czegokolwiek = nic sie nie zmienia), Commit zdejmuje z polki cale sztuki
        /// i zapisuje zapas. Proba (menu) nie wola Commit - nic w swiecie sie nie zmienia.
        /// </summary>
        internal sealed class Bench
        {
            private readonly Settlement _st;
            private readonly ItemRoster _shelf;
            private readonly float[] _kg = new float[Kinds], _val = new float[Kinds];
            private readonly Dictionary<EquipmentElement, int> _taken = new Dictionary<EquipmentElement, int>();
            private readonly List<Source> _metal = new List<Source>();     // sztaby i wraki z polki (ruda osobno - potrzebuje drewna)
            private readonly float _pOre, _pWood, _pLea, _pLin, _pWool, _crudePerLoad;
            private readonly int _smeltWood;
            private readonly EquipmentElement _eOre, _eWood, _eLea, _eLin, _eWool;
            public readonly bool Ok;
            public readonly float[] UsedKg = new float[Kinds];
            public float UsedValue;                                          // wartosc zuzytego materialu (placi zlecajacy)
            public readonly Dictionary<string, int> TakenById = new Dictionary<string, int>();   // do logu: co zdjeto z polki (po Commit)
            public int ScrapTaken;

            internal Bench(Settlement st)
            {
                _st = st;
                var town = st != null ? st.Town : null;
                if (town == null || st.ItemRoster == null) return;
                Resolve();
                _shelf = st.ItemRoster;
                Stock s0;
                if (_stock.TryGetValue(st.StringId, out s0)) { Array.Copy(s0.Kg, _kg, Kinds); Array.Copy(s0.Val, _val, Kinds); }
                var s = Settings.Current;
                _eOre = new EquipmentElement(_ore); _eWood = new EquipmentElement(_wood); _eLea = new EquipmentElement(_leather);
                _eLin = new EquipmentElement(_linen); _eWool = new EquipmentElement(_wool);
                _pOre = _ore != null ? Math.Max(0f, WorkshopLaw.MatPrice(town, _ore, 0)) : 0f;
                _pWood = _wood != null ? Math.Max(0f, WorkshopLaw.MatPrice(town, _wood, 1)) : 0f;
                _pLea = _leather != null ? Math.Max(0f, WorkshopLaw.MatPrice(town, _leather, 2)) : 0f;
                _pLin = _linen != null ? Math.Max(0f, WorkshopLaw.MatPrice(town, _linen, 3)) : 0f;
                _pWool = _wool != null ? Math.Max(0f, WorkshopLaw.MatPrice(town, _wool, 3)) : 0f;
                float oreKg = UnitKg(_ore);
                _crudePerLoad = oreKg / 10f * Math.Max(0.1f, s.WorkshopCrudeKgPerOre);                       // 100 kg rudy -> 15 kg surowki
                _smeltWood = (int)Math.Ceiling(oreKg * Math.Max(0f, s.WorkshopWoodPerOre) / UnitKg(_wood) - Eps);   // 100 kg rudy -> 5 ladunkow drewna
                // sztaby z polki (surowka .. stal): kg surowki = waga x 1.25^stopien
                for (int i = 0; i < _bars.Length; i++)
                {
                    var b = _bars[i];
                    if (b == null) continue;
                    float kg = Math.Max(0.05f, b.Weight) * (float)Math.Pow(1.25, _barSteps[i]);
                    _metal.Add(new Source { El = new EquipmentElement(b), Kg = kg, Price = Math.Max(0f, town.GetItemPrice(b, null, false)) });
                }
                // zlom: wraki z polki - polowa metalu receptury (regula przetopu), w kg surowki wedle gatunku
                for (int i = 0; i < _shelf.Count; i++)
                {
                    var el = _shelf.GetElementCopyAtIndex(i);
                    var it = el.EquipmentElement.Item;
                    var m = el.EquipmentElement.ItemModifier;
                    if (el.Amount <= 0 || it == null || m == null || !LootPrices.IsWreck(m)) continue;
                    if (ArmsPricing.IsUnique(it) || LegendaryLaw.IsLegend(it)) continue;
                    var c = ArmsPricing.CostOf(it);
                    if (c == null || c.MetalKg <= 0.01f) continue;
                    float kg = 0.5f * c.MetalKg * (float)Math.Pow(1.25, WorkshopLaw.StepsOf(c.Grade));
                    _metal.Add(new Source { El = el.EquipmentElement, Kg = kg, Price = Math.Max(0f, town.GetItemPrice(el.EquipmentElement, null, false)) });
                }
                Ok = true;
            }

            private int Avail(EquipmentElement e, Dictionary<EquipmentElement, int> took)
            {
                if (e.Item == null) return 0;
                int i = _shelf.FindIndexOfElement(e);
                int have = i >= 0 ? _shelf.GetElementCopyAtIndex(i).Amount : 0;
                int t; if (_taken.TryGetValue(e, out t)) have -= t;
                if (took != null && took.TryGetValue(e, out t)) have -= t;
                return have;
            }

            private static void Add(Dictionary<EquipmentElement, int> d, EquipmentElement e, int n) { int t; d.TryGetValue(e, out t); d[e] = t + n; }

            /// <summary>Ile calych sztuk zrodla jest jeszcze na polce (ruda: tyle ladunkow, na ile starczy tez drewna do dymarki).</summary>
            private int Units(Source c, Dictionary<EquipmentElement, int> took)
            {
                int a = Avail(c.El, took);
                if (c.WoodUnits > 0) a = Math.Min(a, Avail(_eWood, took) / c.WoodUnits);
                return Math.Max(0, a);
            }

            /// <summary>Zakup kowali na brakujace r kg rodzaju k: to zrodlo i tyle calych sztuk, ile najtaniej pokrywa brak (np. 3 sztaby
            /// surowki zamiast ladunku rudy na latke helmu); gdy zadne zrodlo samo nie pokryje braku - wszystko, co jest, z najtanszego za kg
            /// (petla bierze dalej z nastepnych).</summary>
            private bool Pick(int k, float r, Dictionary<EquipmentElement, int> took, out Source best, out int n)
            {
                best = default(Source); n = 0;
                var cand = new List<Source>();
                if (k == Metal)
                {
                    cand.AddRange(_metal);
                    if (_ore != null && _wood != null)
                        cand.Add(new Source { El = _eOre, Kg = _crudePerLoad, Price = _pOre + _smeltWood * _pWood, WoodUnits = _smeltWood });
                }
                else if (k == Wood) { if (_wood != null) cand.Add(new Source { El = _eWood, Kg = UnitKg(_wood), Price = _pWood }); }
                else if (k == Leather) { if (_leather != null) cand.Add(new Source { El = _eLea, Kg = UnitKg(_leather), Price = _pLea }); }
                else
                {
                    if (_linen != null && Avail(_eLin, took) > 0) cand.Add(new Source { El = _eLin, Kg = UnitKg(_linen), Price = _pLin });
                    else if (_wool != null) cand.Add(new Source { El = _eWool, Kg = UnitKg(_wool), Price = _pWool });   // welna za len - jak warsztaty
                }
                bool found = false; float bestTotal = 0f;
                foreach (var c in cand)
                {
                    int a = Units(c, took);
                    if (a <= 0 || c.Kg <= Eps) continue;
                    int u = (int)Math.Ceiling((r - Eps) / c.Kg);
                    if (u < 1) u = 1;
                    if (u > a) continue;
                    float total = u * c.Price;
                    if (!found || total < bestTotal) { best = c; n = u; bestTotal = total; found = true; }
                }
                if (found) return true;
                foreach (var c in cand)
                {
                    int a = Units(c, took);
                    if (a <= 0 || c.Kg <= Eps) continue;
                    if (!found || c.Price / c.Kg < best.Price / best.Kg) { best = c; n = a; found = true; }
                }
                return found;
            }

            /// <summary>
            /// Material na jedna sztuke. 1 = zaplanowane (zapas kowali i lista zdjec zmienione w lawie, cost = wartosc zuzytego);
            /// 0 = brak materialu (miss: bity rodzajow, ktorych brak; nic sie nie zmienia); -1 = accept odrzucil koszt (nic sie nie zmienia).
            /// </summary>
            internal int TryMend(float[] need, Func<float, bool> accept, out float cost, out int miss)
            {
                cost = 0f; miss = 0;
                if (!Ok || need == null) return 0;
                var kg = (float[])_kg.Clone(); var val = (float[])_val.Clone();
                var took = new Dictionary<EquipmentElement, int>();
                for (int k = 0; k < Kinds; k++)
                {
                    float x = need[k];
                    if (x <= Eps) continue;
                    int guard = 0;
                    while (kg[k] + Eps < x)
                    {
                        Source src; int n;
                        if (guard++ > 64 || !Pick(k, x - kg[k], took, out src, out n) || n <= 0) { miss |= 1 << k; break; }
                        Add(took, src.El, n);
                        if (src.WoodUnits > 0) Add(took, _eWood, src.WoodUnits * n);
                        kg[k] += src.Kg * n; val[k] += src.Price * n;
                    }
                }
                if (miss != 0) return 0;
                float c0 = 0f;
                var use = new float[Kinds];
                for (int k = 0; k < Kinds; k++)
                {
                    float x = need[k];
                    if (x <= Eps || kg[k] <= Eps) continue;
                    float part = val[k] * Math.Min(1f, x / kg[k]);
                    c0 += part; use[k] = x;
                    kg[k] = Math.Max(0f, kg[k] - x); val[k] = Math.Max(0f, val[k] - part);
                    if (kg[k] <= Eps) { kg[k] = 0f; val[k] = 0f; }
                }
                cost = c0;
                if (accept != null && !accept(c0)) return -1;
                Array.Copy(kg, _kg, Kinds); Array.Copy(val, _val, Kinds);
                foreach (var kv in took) Add(_taken, kv.Key, kv.Value);
                for (int k = 0; k < Kinds; k++) UsedKg[k] += use[k];
                UsedValue += c0;
                return 1;
            }

            /// <summary>Szacunek kosztu materialu bez zmian w lawie (kolejnosc napraw w budzecie): kg x najtansza cena za kg tego, co jest.</summary>
            internal float Estimate(float[] need)
            {
                if (!Ok || need == null) return 0f;
                float sum = 0f;
                for (int k = 0; k < Kinds; k++)
                {
                    if (need[k] <= Eps) continue;
                    if (_kg[k] > Eps) { sum += need[k] * _val[k] / _kg[k]; continue; }
                    Source src; int n;
                    if (Pick(k, 1e9f, null, out src, out n)) sum += need[k] * src.Price / Math.Max(0.01f, src.Kg);
                }
                return sum;
            }

            /// <summary>Zdjecie z polki calych sztuk zaplanowanych w tym zleceniu i zapis zapasu kowali. Woluje sie raz, po planowaniu.</summary>
            internal void Commit()
            {
                if (!Ok) return;
                var gf = GoodsLedger.Begin(GoodsLedger.FMend, _st);   // ksiega towarow (146): zdjete z polki jako ujscie "naprawy kowali miasta (135)" (tylko licznik)
                try
                {
                    foreach (var kv in _taken)
                    {
                        if (kv.Value <= 0) continue;
                        int i = _shelf.FindIndexOfElement(kv.Key);
                        int have = i >= 0 ? _shelf.GetElementCopyAtIndex(i).Amount : 0;
                        int n = Math.Min(have, kv.Value);
                        if (n <= 0) continue;
                        _shelf.AddToCounts(kv.Key, -n);
                        var it = kv.Key.Item;
                        string id = it.StringId + (kv.Key.ItemModifier != null ? "[" + kv.Key.ItemModifier.StringId + "]" : "");
                        int t; TakenById.TryGetValue(id, out t); TakenById[id] = t + n;
                        if (kv.Key.ItemModifier != null) ScrapTaken += n;
                    }
                }
                finally { GoodsLedger.End(gf); }
                _taken.Clear();
                Stock s0;
                if (!_stock.TryGetValue(_st.StringId, out s0)) { s0 = new Stock(); _stock[_st.StringId] = s0; }
                Array.Copy(_kg, s0.Kg, Kinds); Array.Copy(_val, s0.Val, Kinds);
            }
        }
    }
}
