using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// LAS WSI - DREWNO Z PRAWDZIWEJ PRODUKCJI KAZDEJ WSI (paczka 126; zastepuje drewno, ktore do 125 RealisticBannerlord dosypywal
    /// z niczego). Pomiar (testy 07.10, opis paczki 126): swiat zuzywa w pokoju ok. 1500-1700 ladunkow drewna na dobe (budowy ok. 1170-1320,
    /// warsztaty zbrojne ok. 320-380, mieszczanie i zapasy obozowe BK ok. 35-200), a drwale (45 wsi, lista typu wsi, LumberOutputMultiplier
    /// x3) daja ok. 250. Reszta - ok. 1400-1600 - przychodzila z dosypki RBL: ok. 10 ladunkow z kazdego wyjazdu wozu kazdej wsi (ok. 1000
    /// na dobe, rozwozone do miast przez ok. 100 wozow dziennie) i dopelnienie kazdego miasta do 30.
    /// Dlaczego nie wiecej drwali (LumberOutputMultiplier): ilosc da sie wyrownac (x20 = ok. 1700), ale nie rozwiezienie - 45 wsi drwali
    /// wysyla ok. 8 wozow dziennie po 20-30 t, miasta z budowami zjadaja dostawe w kilka dob, a karawany woza drewna po kilka ladunkow.
    /// Model rynku drewna (paczka 126, sym\rynek_drewna.py) daje wtedy ok. 40-45 z 97 miast bez drewna (dzis 2-11), a w grze widac to
    /// samo na rudzie: 26 kopalni, wozy z calym magazynem - 44-60 miast bez rudy. Historycznie drewno opalowe i budulec szly do miasta
    /// z lasow okolicznych wsi (kazda parafia miala las - opal, budulec, wegiel), a drwale specjalni zasilali wielkie budowy i stocznie.
    /// Regula: kazda wies, ktora NIE jest wsia drwali (jej typ nie ma drewna na liscie produkcji - drwale tna dalej wedle modelu i mnoznika),
    /// dopisuje co dobe VillageWoodlotLoads ladunkow (100 kg) drewna ze swojego lasu - w tej samej chwili i pod ta sama bramka co reszte
    /// produkcji wsi (postfiks VillageGoodProductionCampaignBehavior.TickGoodProduction: gra wola go tylko dla wsi nieopuszczonej i z
    /// magazynem ponizej 1.5 W; BK zastepuje cialo metody prefiksem - postfiks biegnie i tak), tylko w stanie Normal (wies najezdzana,
    /// spladrowana albo przymuszona nie tnie - jak model BK). Magazyn wsi (Village.GetWarehouseCapacity = 5 dob produkcji z listy typu)
    /// liczy tez las: W + ceil(5 x stawka) - bez tego drewno lasu zapychaloby magazyn szybciej, niz wies wysyla woz (model magazynow:
    /// zatkane 1.9% przy W z lasem, 3.4% bez - tyle, co z dosypka RBL). Woz wsi zabiera drewno razem z reszta magazynu (122) i sprzedaje
    /// w miescie, ktore najlepiej placi (119) - ok. 14 ladunkow (1.4 t) na kurs, jak dotad 10 ladunkow dosypki.
    /// Zastepstwo, nie dodatek: las wsi dziala tylko wtedy, gdy RealisticBannerlord NIE dosypuje drewna (FreeSupplies.RblFeeds false:
    /// blokada 125 wpieta i wlaczona albo RBL nieobecny) - przy wylaczonym No Free Timber And Tools drewno idzie z dosypki jak dotad.
    /// Ksiega drewna: sztuki ida wprost do OreLedger (pozycja "las wsi"; zdarzenia gry OnItemProduced nie wolamy - poza nasza ksiega nikt
    /// go nie slucha, skan modulow 07.10; "wsie dopisaly" zostaje wydobyciem drwali porownywalnym z "model").
    /// Jednostka: suwak w ladunkach 100 kg; liczba sztuk = stawka x 100 kg / waga sztuki (przed HistoricalPrices.Apply - ticki startowe nowej
    /// kampanii - sztuka drewna wazy 10 kg, StartStock przelicza je potem na ladunki: tyle samo kilogramow). Stan: tylko liczniki doby.
    /// Start nowej kampanii (recenzja 126): gra rozdaje miastom 5 przebiegow produkcji kazdej wsi (TickGoodProduction z
    /// initialProductionForTowns) - las wsi idzie tam razem z reszta plonu, jak w grze do miasta handlowego wsi (TradeBound: wies miejska -
    /// swoje miasto, wies zamkowa - miasto targowe, jak wozy wpisu 100; zamek drewna nie zuzywa). Bez tego miasta zaczynalyby kampanie
    /// bez drewna lasu (dotad dopelnial je RBL w 1.-2. dobie) i przez pierwsze dni, zanim dojada wozy, budowy stalyby na "brak materialow".
    /// </summary>
    internal static class VillageWoodlot
    {
        private const float LoadKg = 100f;
        private static bool _wiredTick, _wiredCap;
        private static ItemObject _wood;
        private static readonly Dictionary<VillageType, bool> _lumber = new Dictionary<VillageType, bool>();
        private static int _made, _villages, _notNormal, _stumbles, _stumblesDay;
        private static int _startPasses;      // recenzja 126: rozdanie startowe miastom (initialProductionForTowns) - przebiegi wsi i kilogramy
        private static double _startKg;

        /// <summary>Stawka z suwaka (ladunki 100 kg na wies na dobe); 0 albo mniej = wylaczone.</summary>
        internal static float Rate { get { var s = Settings.Current; return s != null && s.VillageWoodlotLoads > 0f ? s.VillageWoodlotLoads * (s.WoodlotStep > 0f ? s.WoodlotStep : 1f) : 0f; } }   // 174.3: x WoodlotStep (1.6 - wegiel dla kuzni)

        /// <summary>Las wsi tnie: obie latki wpiete (produkcja i magazyn - jedna bez drugiej nic nie zmienia), suwak powyzej 0 i RBL nie
        /// dosypuje drewna z niczego.</summary>
        internal static bool On { get { return _wiredTick && _wiredCap && Rate > 0f && !FreeSupplies.RblFeeds; } }

        /// <summary>Nowa gra albo wczytanie (konstruktor ArmouryBehavior): liczniki doby, potkniecia i pamiec typow wsi od zera.</summary>
        internal static void Reset()
        {
            _made = 0; _villages = 0; _notNormal = 0; _stumbles = 0; _stumblesDay = 0; _startPasses = 0; _startKg = 0.0;
            lock (_lumber) _lumber.Clear();
            _wood = null;
            _climateK = -1f;
            _cls = null;
            Array.Clear(_clsLoads, 0, 5); Array.Clear(_clsVil, 0, 5);
        }

        // --- T8 (noc 08/09.10): LAS WSI WEDLUG KLIMATU (audyt 02 C3/N4) ---
        // Dotad kazda wies bez drwali tnie tyle samo, takze na pustyniach Dorne, Qarthu i Zatoki Niewolniczej (audyt 02 L9). Wspolczynnik
        // z kultury wsi ROT (id sprawdzone w ROT-Content spcultures.xml): pustynia 0.3, step Dothrakow 0.5, srodziemnomorskie 0.8,
        // lesne 1.2, reszta 1.0; stala wyrownujaca K = liczba wsi z lasem / suma wspolczynnikow - swiat razem tnie tyle samo co dotad
        // (drewno ma juz lekki deficyt, wiec wyrownanie zamiast ciecia). K liczona raz przy starcie sesji (po zmianie typow wsi T8)
        // i wypisana w logu; przed tym (ticki startowe nowej kampanii) - leniwie przy pierwszym uzyciu. Wylacznik WoodlotByClimate.
        // Recenzja T8: klasa klimatu kazdej wsi zapamietana w tym samym przebiegu co K (zdjecie _cls) - BK zmienia kulture wsi w trakcie
        // gry (kultura dominujaca przy starcie sesji, asymilacja >= 0.55 co dobe), a K i wspolczynniki musza pochodzic z jednego zdjecia,
        // zeby suma swiata sie domykala. Zdjecie odswieza sie przy nastepnym starcie sesji.
        private static readonly float[] ClassF = { 0.3f, 0.5f, 0.8f, 1.0f, 1.2f };
        private static readonly string[] ClassName = { "pustynia", "step", "srodziemnomorskie", "reszta", "lesne" };
        private const int ClassRest = 3;
        private static readonly Dictionary<string, int> ClimateIdx = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            { "aserai", 0 }, { "ghiscari", 0 }, { "qartheen", 0 },                                  // pustynie (Dorne, Ghis, Qarth)
            { "khuzait", 1 },                                                                     // step Dothrakow
            { "lyseni", 2 }, { "tyroshi", 2 }, { "myrish", 2 }, { "volantine", 2 }, { "valyrian", 2 },   // srodziemnomorskie
            { "battania", 4 }, { "river", 4 }, { "vale", 4 }, { "qohorik", 4 }, { "ibbenese", 4 }, { "nord", 4 }, { "freefolk", 4 }   // lesne (battania = Polnoc, nord = Lorath)
        };
        private static float _climateK = -1f;     // -1 = nieliczona, 0 = nie da sie policzyc (wtedy wspolczynnik 1)
        private static volatile Dictionary<Village, int> _cls;   // zdjecie klas klimatu z Calibrate (podmieniane w calosci - czytelnicy bez zamka)
        private static readonly int[] _clsLoads = new int[5], _clsVil = new int[5];   // licznik doby: ladunki i wsie z tickiem lasu wedlug klasy

        private static int RawClass(Village v)
        {
            var c = v != null && v.Settlement != null ? v.Settlement.Culture : null;
            int i;
            return c != null && c.StringId != null && ClimateIdx.TryGetValue(c.StringId, out i) ? i : ClassRest;
        }

        /// <summary>Klasa klimatu wsi ze zdjecia startowego (Calibrate); wies spoza zdjecia - z biezacej kultury.</summary>
        private static int ClassOf(Village v)
        {
            var m = _cls;
            int i;
            return m != null && v != null && m.TryGetValue(v, out i) ? i : RawClass(v);
        }

        /// <summary>Mnoznik lasu tej wsi: K x wspolczynnik klimatu; 1, gdy WoodlotByClimate wylaczony albo K nie dala sie policzyc.</summary>
        internal static float ClimateFactor(Village v)
        {
            var s = Settings.Current;
            if (s == null || !s.WoodlotByClimate || v == null) return 1f;
            if (_climateK < 0f) Calibrate(false);
            return _climateK > 0f ? _climateK * ClassF[ClassOf(v)] : 1f;
        }

        /// <summary>Stala wyrownujaca K i zdjecie klas z obecnych typow i kultur wsi (pelny przeglad wsi - tylko przy starcie sesji).
        /// log = linia startowa ze wspolczynnikami.</summary>
        internal static void Calibrate(bool log)
        {
            try
            {
                int n = 0; double sum = 0.0;
                var byC = new int[5];
                var map = new Dictionary<Village, int>();
                foreach (var v in Village.All)
                {
                    if (v == null || v.Settlement == null || IsLumber(v)) continue;
                    int c = RawClass(v);
                    map[v] = c;
                    n++; sum += ClassF[c]; byC[c]++;
                }
                _climateK = n > 0 && sum > 0.0 ? (float)(n / sum) : 0f;
                _cls = map;
                if (!log) return;
                var inv = CultureInfo.InvariantCulture;
                var s = Settings.Current;
                var sb = new StringBuilder("Las wsi wedlug klimatu (T8): ");
                if (s == null || !s.WoodlotByClimate) sb.Append("WYLACZONY w ustawieniach (Woodlot By Climate) - kazda wies tnie stawke bez wspolczynnika");
                else if (!On) sb.Append("NIEAKTYWNY - las wsi nie tnie (suwak Village Woodlot Loads = 0, RBL dosypuje albo latki nie wpiete - linia startowa VillageWoodlot i linia dnia \"Las wsi (126)\"); K policzona na zapas");
                else sb.Append("CZYNNY");
                sb.Append("; wsi z lasem (bez drwali) ").Append(n).Append(", stala wyrownujaca K = ").Append(_climateK.ToString("0.000", inv))
                  .Append(" (wsi / suma wspolczynnikow ").Append(sum.ToString("0.0", inv)).Append("); wspolczynnik x K -> wsi:");
                for (int i = 0; i < 5; i++)
                    if (byC[i] > 0)
                        sb.Append(' ').Append(ClassName[i]).Append(' ').Append(ClassF[i].ToString("0.0", inv)).Append(" x K = ").Append((ClassF[i] * _climateK).ToString("0.000", inv)).Append(" -> ").Append(byC[i]).Append(';');
                sb.Append(" pustynia (aserai, ghiscari, qartheen), step (khuzait), srodziemnomorskie (lyseni, tyroshi, myrish, volantine, valyrian), lesne (battania, river, vale, qohorik, ibbenese, nord, freefolk)")
                  .Append("; klasa wsi zapamietana do nastepnego startu sesji (kultura zmieniona przez BK w trakcie gry liczy sie od nastepnego wczytania)")
                  .Append("; suma swiata bez zmian: ").Append(n).Append(" x ").Append(Rate.ToString("0.##", inv)).Append(" ladunku na dobe.");
                Log.Info(sb.ToString());
            }
            catch (Exception e) { _climateK = 0f; Log.Error("VillageWoodlot.Calibrate", e); }
        }

        /// <summary>Drewno gry - ten sam przedmiot, ktory dosypywal RBL i ktory blokuje FreeSupplies (DefaultItems.HardWood = "hardwood").</summary>
        private static ItemObject Wood()
        {
            if (_wood == null)
            {
                try { _wood = DefaultItems.HardWood; } catch { _wood = null; }
                if (_wood == null && MBObjectManager.Instance != null) _wood = MBObjectManager.Instance.GetObject<ItemObject>("hardwood");
            }
            return _wood;
        }

        /// <summary>Wies drwali: typ wsi ma drewno na swojej liscie produkcji (tnie wedle modelu i LumberOutputMultiplier) - lasu wsi nie dostaje.</summary>
        internal static bool IsLumber(Village v)
        {
            var t = v != null ? v.VillageType : null;
            if (t == null) return false;
            bool r;
            lock (_lumber)    // magazyn wsi pytaja tez inne petle gry i mody - pamiec typow pisana pod zamkiem (kilkanascie typow, potem same odczyty)
            {
                if (_lumber.TryGetValue(t, out r)) return r;
                r = false;
                var w = Wood();
                if (t.Productions != null)
                    foreach (var p in t.Productions) if (p.Item1 != null && (p.Item1 == w || p.Item1.StringId == "hardwood")) { r = true; break; }
                _lumber[t] = r;
            }
            return r;
        }

        /// <summary>Sztuki drewna na dobe w jednostce chwili (ladunek 100 kg = 1 sztuka po HistoricalPrices, 10 sztuk przed).
        /// T8: dla wsi (v != null) razy mnoznik klimatu ClimateFactor; v = null - stawka swiata bez klimatu (linia dnia).</summary>
        private static float PiecesPerDay(ItemObject wood, Village v)
        {
            float kg = wood != null && wood.Weight > 0.05f ? wood.Weight : LoadKg;
            float d = Rate * LoadKg / kg;
            return v != null ? d * ClimateFactor(v) : d;
        }

        /// <summary>Postfiks VillageGoodProductionCampaignBehavior.TickGoodProduction(Village, bool): gra wola go raz na dobe dla wsi
        /// nieopuszczonej, gdy magazyn jest ponizej 1.5 W (TickProductions). Rozdanie startowe nowej kampanii (initialProductionForTowns,
        /// 5 przebiegow gry): drewno lasu - jak w grze reszta plonu - do miasta handlowego wsi (TradeBound), bez ksiegi (gra i BK tez go
        /// nie ksieguja; StartStock przelicza te sztuki 10 kg na ladunki razem z reszta zapasu startowego).</summary>
        public static void TickPostfix(Village __0, bool __1)
        {
            if (!On) return;
            try
            {
                var v = __0;
                if (v == null || v.Settlement == null || v.Settlement.ItemRoster == null || IsLumber(v)) return;
                if (v.VillageState != Village.VillageStates.Normal) { if (!__1) _notNormal++; return; }
                var wood = Wood();
                if (wood == null) return;
                int n = MBRandom.RoundRandomized(PiecesPerDay(wood, v));
                if (!__1) { int c = ClassOf(v); _clsVil[c]++; if (n > 0) _clsLoads[c] += n; }   // T8: licznik doby wedlug klasy klimatu (takze wies z 0 sztuk dzis)
                if (n <= 0) return;
                if (__1)
                {
                    var to = v.TradeBound;                 // gra: TradeBound (null - jak w grze nic); wies miejska - swoje miasto
                    if (to == null || to.ItemRoster == null) return;
                    to.ItemRoster.AddToCounts(wood, n);
                    _startPasses++; _startKg += n * (double)Math.Max(0.1f, wood.Weight);
                    return;
                }
                v.Settlement.ItemRoster.AddToCounts(wood, n);
                OreLedger.NoteWoodlot(wood, v.Settlement, n);
                _made += n; _villages++;
            }
            catch (Exception e)
            {
                _stumbles++; _stumblesDay++;
                if (_stumbles <= 3) Log.Error("VillageWoodlot.TickPostfix", e);
            }
        }

        /// <summary>Postfiks Village.GetWarehouseCapacity(): magazyn = 5 dob produkcji, wiec i 5 dob lasu (W + ceil(5 x stawka)).
        /// Ta sama liczba dla bramki produkcji gry (1.5 W), progu wyjazdu wozu (W), miernika wozow (MarketCarts) i MarketRoad.</summary>
        public static void CapPostfix(Village __instance, ref int __result)
        {
            if (!On) return;
            try
            {
                var v = __instance;
                if (v == null || IsLumber(v) || v.VillageState != Village.VillageStates.Normal) return;
                float d = PiecesPerDay(Wood(), v);
                if (d > 0f) __result += (int)Math.Ceiling(5f * d - 1e-4f);
            }
            catch { }
        }

        /// <summary>Raz na dobe (ArmouryBehavior.OnDailyTick, po linii "Dosypka z niczego"): ile drewna dopisal las wsi od poprzedniej linii. Tylko log.</summary>
        internal static void Daily()
        {
            if (!_wiredTick) return;
            try
            {
                int lumber = 0, able = 0, clogged = 0, deserted = 0, other = 0;
                if (On)
                    foreach (var v in Village.All)
                    {
                        try   // jedna wies bez danych nie gasi linii (licznik potkniec)
                        {
                            if (v == null || v.Settlement == null) continue;
                            if (IsLumber(v)) { lumber++; continue; }
                            if (v.VillageState == Village.VillageStates.Looted) { deserted++; continue; }
                            if (v.VillageState != Village.VillageStates.Normal) { other++; continue; }
                            able++;
                            var r = v.Settlement.ItemRoster;
                            if (r == null) continue;
                            int n = 0;
                            for (int i = 0; i < r.Count; i++) n += r[i].Amount;
                            if (n >= v.GetWarehouseCapacity() * 1.5f) clogged++;
                        }
                        catch { _stumbles++; _stumblesDay++; }
                    }
                var inv = CultureInfo.InvariantCulture;
                var sb = new StringBuilder();
                sb.Append("Las wsi (126): dzien ").Append((int)CampaignTime.Now.ToDays - 1).Append(" - ");
                if (Rate <= 0f) sb.Append("WYLACZONY (suwak Village Woodlot Loads = 0)");
                else if (FreeSupplies.RblFeeds) sb.Append("WSTRZYMANY - RealisticBannerlord dosypuje drewno z niczego (No Free Timber And Tools wylaczony albo latka 125 nie wpieta) - las wsi nie doklada drugi raz");
                else if (!On) sb.Append("NIEAKTYWNY - latki nie wpiete (linia startowa VillageWoodlot)");
                else
                {
                    float d = PiecesPerDay(Wood(), null);
                    var stc = Settings.Current;
                    sb.Append("CZYNNY: wsie (bez wsi drwali) dopisaly ").Append(_made).Append(" ladunkow drewna w ").Append(_villages).Append(" wsiach")
                      .Append(" (stawka swiata bez klimatu ").Append(Rate.ToString("0.##", inv)).Append(" ladunku 100 kg na wies na dobe; magazyn wsi W +").Append((int)Math.Ceiling(5f * d - 1e-4f))
                      .Append(" szt. przy stawce bez klimatu");
                    if (stc != null && stc.WoodlotByClimate && _climateK > 0f)   // T8: magazyn wsi liczy las z klimatem - zakres od pustyni do lasu
                        sb.Append(", z klimatem od +").Append((int)Math.Ceiling(5f * d * _climateK * ClassF[0] - 1e-4f)).Append(" do +").Append((int)Math.Ceiling(5f * d * _climateK * ClassF[4] - 1e-4f));
                    sb.Append("); wsi z lasem w stanie Normal ").Append(able).Append(", w tym z magazynem >= 1.5 W (dzis nie tna) ").Append(clogged)
                      .Append("; nie tna: spladrowane ").Append(deserted).Append(", najezdzane albo przymuszone ").Append(other)
                      .Append(" (tick produkcji poza stanem Normal: ").Append(_notNormal).Append(")")
                      .Append("; wsi drwali (drewno z listy typu wsi, x").Append(Settings.Current != null ? Settings.Current.LumberOutputMultiplier.ToString("0.0", inv) : "?").Append(") ").Append(lumber);
                }
                if (_startPasses > 0)   // tylko pierwsza linia nowej kampanii
                    sb.Append("; start kampanii: rozdanie startowe gry (5 przebiegow produkcji wsi do miast) - las wsi dal miastom handlowym ")
                      .Append((_startKg / LoadKg).ToString("0", inv)).Append(" ladunkow 100 kg w ").Append(_startPasses).Append(" przebiegach wsi");
                sb.Append("; potkniecia dzis ").Append(_stumblesDay).Append(" (od wczytania ").Append(_stumbles).Append(").");
                {   // T8: stan lasu wedlug klimatu na koncu linii (poczatek linii bez zmian dla parserow)
                    var st = Settings.Current;
                    sb.Append(" Klimat lasu (T8): ").Append(st != null && st.WoodlotByClimate
                        ? (_climateK > 0f ? "CZYNNY, K = " + _climateK.ToString("0.000", inv) : "NIEAKTYWNY (K nie policzona - wspolczynnik 1)")
                        : "WYLACZONY");
                    if (On)   // recenzja T8: liczby, ktore odrozniaja czynny klimat od nieczynnego (srednio na wies z tickiem lasu, w stawkach swiata)
                    {
                        float d0 = PiecesPerDay(Wood(), null);
                        sb.Append("; wedlug klasy (ladunki w wsiach z tickiem lasu = srednio na wies x stawka):");
                        for (int i = 0; i < 5; i++)
                            if (_clsVil[i] > 0)
                                sb.Append(' ').Append(ClassName[i]).Append(' ').Append(_clsLoads[i]).Append(" lad. w ").Append(_clsVil[i]).Append(" wsi = ")
                                  .Append(d0 > 0f ? ((double)_clsLoads[i] / _clsVil[i] / d0).ToString("0.00", inv) : "?").Append(" x;");
                    }
                    sb.Append('.');
                }
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Log.Error("VillageWoodlot.Daily", e); }
            finally { _made = 0; _villages = 0; _notNormal = 0; _stumblesDay = 0; _startPasses = 0; _startKg = 0.0; Array.Clear(_clsLoads, 0, 5); Array.Clear(_clsVil, 0, 5); }
        }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var tb = AccessTools.TypeByName("TaleWorlds.CampaignSystem.CampaignBehaviors.VillageGoodProductionCampaignBehavior");
                var tick = tb != null ? AccessTools.Method(tb, "TickGoodProduction", new[] { typeof(Village), typeof(bool) }) : null;
                var cap = AccessTools.Method(typeof(Village), "GetWarehouseCapacity", Type.EmptyTypes);
                if (tick == null || cap == null || cap.ReturnType != typeof(int))
                {
                    Log.Info("VillageWoodlot: BRAK " + (tick == null ? "VillageGoodProductionCampaignBehavior.TickGoodProduction(Village, bool)" : "Village.GetWarehouseCapacity()")
                             + " w tej wersji gry - las wsi NIEAKTYWNY (drewno tylko od drwali).");
                    return;
                }
                // obie latki albo zadna (On wymaga obu): drewno lasu bez W w magazynie zapychaloby wsie (model: zatkane 1.9% -> 3.4%),
                // a W z lasem bez lasu wydluzalby kursy wozow o drewno, ktorego nie ma
                h.Patch(cap, postfix: new HarmonyMethod(typeof(VillageWoodlot), nameof(CapPostfix)));
                _wiredCap = true;
                h.Patch(tick, postfix: new HarmonyMethod(typeof(VillageWoodlot), nameof(TickPostfix)));
                _wiredTick = true;
                var s = Settings.Current;
                Log.Info("VillageWoodlot: las wsi wpiety (postfiks TickGoodProduction + magazyn wsi GetWarehouseCapacity) - kazda wies bez drwali tnie "
                         + (s != null ? s.VillageWoodlotLoads.ToString("0.##", CultureInfo.InvariantCulture) : "?") + " ladunku drewna (100 kg) na dobe z wlasnego lasu, tylko gdy RealisticBannerlord nie dosypuje"
                         + " (teraz: " + (FreeSupplies.RblFeeds ? "RBL DOSYPUJE - las wsi wstrzymany" : "dosypki brak - las wsi tnie") + "); liczby - linia dnia \"Las wsi (126)\".");
            }
            catch (Exception e)
            {
                Log.Error("VillageWoodlot.ApplyAll", e);
                if (_wiredCap != _wiredTick) Log.Info("VillageWoodlot: wpieta tylko jedna z dwoch latek - las wsi NIEAKTYWNY (oba postfiksy nic nie robia).");
            }
        }
    }
}
