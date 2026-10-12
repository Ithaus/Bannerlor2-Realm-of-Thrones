using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Threading;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// E4/1 (189, PLAN 4.12) - ZIMA WEDLUG KRAIN (projekt etapu 4 rozdz. 3 "E4/1", regula R11; Jeff 09.10 "b, c TAK po probie zimy").
    /// Jedno zrodlo zimy zostaje WinterBite (WinterSource); tu tylko CZYNNIK KRAINY, ktory WinterBite mnozy przez gradient polnocy w czterech
    /// skutkach zimy (jedzenie partii, produkcja wsi, zywnosc wsi, spichlerz warowni) i w odczytach VillageCut / PartyFoodBonus linii "Zima (E4/0)":
    ///  - Wyspy Letnie ("summer") 0 - bez zimy;
    ///  - cieple: Dorne ("aserai"), Qarth, Zatoka Niewolnicza ("ghiscari"), Lys, Tyrosh, Myr, Volantis, Valyria - WinterWarmRealmFactor (0.4):
    ///    Dorne 50% x 0.4 x gradient ok. 0.75 = ok. 15% plonu zamiast 37.5%;
    ///  - reszta 1.0 (Polnoc bez zmian).
    /// KRAINA = kultura REGIONU (warownia OutlawLaw - wies: jej warownia, warownia: ona sama, partia: najblizsza warownia pod partia) z pierwszej
    /// doby sesji - ta sama migawka co linia "Zima (E4/0)" (Measure40.CultureAtStart, U3/U18); do E4/3 (kultura kalibracji w zapisie) tak zostaje.
    /// Wies Dorne przejeta kulturowo przez Reach zostaje w klimacie Dorne. Partia: region liczony raz na dobe na partie (227 wezlow - pamiec doby),
    /// bo jedzenie partii gra liczy wiele razy na dobe. Czytane tylko w zimie (WinterBite.WinterNow) - poza zima zadnych odczytow.
    /// KONIE JEDZA ZIMA (P10 Z4, ES:211 e "kon jak 2.5 czlowieka"): w zimie partia doplaca WinterHorseFeedShare (0.5) pelnej paszy zwierzat
    /// x czynnik krainy. Pasza liczona TU, po glowie (poprawka po recenzji E4/1): kazdy konny w skladzie partii (MemberRoster, Character.IsMounted,
    /// element.Number - BEZ jencow, jency koni nie maja) x stawka konia + kazde zwierze w taborze (ItemRoster) x jego stawka. Stawki BK
    /// (BKPartyConsumptionModel: kon 0.25, kon bojowy 0.5, juczny 0.15, krowa 0.175, swinia 0.1, owca 0.05 na dobe) czytane refleksja z modelu BK
    /// z listy modeli gry, bez BK - te same liczby z kodu. NIE z BK CalculateAnimalFoodNeed: ta liczy JEDNEGO konia na TYP oddzialu (num += 1
    /// na element rosteru), konnych jencow jak konie, a zwyklego konia w taborze stawka ptaka (0.025). Pol paszy konia = 0.125 = 2.5 x czlowiek (1/20).
    /// Bez doplaty: partia na stepie (TerrainType.Steppe - wypas zimowy; regula wedlug terenu, nie kultury).
    /// PASZA BK: gdy regula paszy BK naprawde biegnie (BK CalculateDailyFoodConsumptionf w aktywnym lancuchu modelu jedzenia), BK sam karmi
    /// swoja formula na pustyni (pelna pasza, caly rok) i w sniegu/zamieci (pol). Tam E4/1 doplaca w zimie TYLKO ROZNICE: swoja pasza minus
    /// to, co naliczy BK (CalculateAnimalFoodNeed obiektu BK z aktywnego lancucha: pustynia x1 bez wielbladow, snieg/zamiec x0.5 - te same
    /// warunki terenu i pogody co BK), najmniej 0. W zimie pasza = wieksza z dwoch (E4/1 albo BK) - bez podwojnej paszy i bez dziury tam, gdzie
    /// zima najostrzejsza (poprawka po recenzji E4/6a: wczesniej E4/1 nie doplacal tam wcale, a formula BK daje np. w sniegu ok. 6x mniej);
    /// poza zima pasze na pustyni i w sniegu liczy sam BK (jego regula - Jeff 11.10 "reguly BK dokladaja sie").
    /// PASZA BK PO GLOWIE (wariant (a) Jeffa 11.10, wylacznik BkAnimalFeedPerHead, na zywo): pasze koni liczy JEDNA zasada przez caly rok -
    /// postfiks na BK CalculateAnimalFoodNeed (BkAnimalPostfix) oddaje zamiast formuly BK nasza liczbe zwierzat (AnimalNeed - ta sama co
    /// pasza zimowa E4/1: konni po glowie bez jencow x kon, zwierzeta taboru x stawka, kon/wielblad w taborze stawka konia). Warunki i mnozniki
    /// terenu zostaja BK (BK CalculateDailyFoodConsumptionf: pustynia - pelna, bez wielbladow, ignoreCamels true - ten sam test StringId
    /// "camel"; poza pustynia snieg/zamiec - pol). BK wola CalculateAnimalFoodNeed tylko tam (dekompilacja 11.10; zaden inny modul jej nie wola
    /// ani nie latuje - skan DLL), a E4/1 czyta ja przez BkNeed - wiec zimowa doplata roznicy liczy sie juz wzgledem paszy po glowie
    /// (przy udziale 0.5: pustynia - BK pelna >= E4/1 0.5 x kraina, doplata tylko za wielblady; snieg - BK pol >= E4/1 0.5 x kraina, bez doplaty).
    /// U Jeffa aktywnym modelem jest RBL RealisticFoodConsumptionModel (RBL laduje sie po ROT, dziedziczy po Default i nie wola modelu
    /// wewnetrznego) - bez E4/6a BK nie karmi nigdzie, wiec doplata jest pelna takze na pustyni i w sniegu. Od E4/6a (FoodChain, FoodModelChain)
    /// RBL oddaje swoje metody lancuchowi pod soba (ROT -> NavalDLC -> BEE -> BK) - regula paszy BK biegnie; FeedInfo idzie wtedy przez RBL
    /// w glab (FoodChain.Chains). Lancuch ustalany raz na kampanie (FeedInfo):
    /// od aktywnego modelu w glab przez owijki (typ wprost po abstrakcyjnym MobilePartyFoodConsumptionModel - ROT _previousModel, BEE _baseModel,
    /// NavalDLC BaseModel); typ po klasie konkretnej (RBL po Default) konczy lancuch - ten sam test, ktorym ROT sprawdza modele (ValidateGameModel).
    /// Doplata wchodzi do bazy jedzenia (WinterBite.FoodPostfix na DefaultMobilePartyFoodConsumptionModel.CalculateDailyBaseFoodConsumptionf -
    /// RBL i BK jej nie nadpisuja, owijki ja wolaja), wiec obejmuja ja te same mnozniki co ludzi: zima x gradient x kraina, perki, Rations -40%
    /// na zewnatrz (kon zostaje 2.5 x czlowiek w kazdej porze).
    /// Watki: jedzenie partii bywa liczone rownolegle - pamieci pod jedna blokada; wyjatki per obiekt liczone (potkniecia), bez wylacznikow.
    /// </summary>
    internal static class WinterRealm
    {
        private const string SummerIsles = "summer";
        private static readonly HashSet<string> Warm = new HashSet<string>(StringComparer.Ordinal)
            { "aserai", "qartheen", "ghiscari", "lyseni", "tyroshi", "myrish", "volantine", "valyrian" };
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const string BkModel = "BannerKings.Models.Vanilla.BKPartyConsumptionModel";

        private static readonly object Gate = new object();
        private static Campaign _camp;                                    // kampania pamieci (nowa kampania / wczytanie = od zera)
        private static readonly Dictionary<Settlement, string> _regionCulture = new Dictionary<Settlement, string>();
        private sealed class Spot { public int Day; public string Culture; }
        private static readonly Dictionary<MobileParty, Spot> _party = new Dictionary<MobileParty, Spot>();

        // pasza: lancuch modelu jedzenia i stawki - raz na kampanie
        private sealed class Feed
        {
            public Campaign Camp;
            public string Active = "?";          // typ aktywnego modelu (Campaign.Current.Models.MobilePartyFoodConsumptionModel)
            public string Chain = "?";           // lancuch od aktywnego modelu w glab
            public bool BkFeeds;                 // BK CalculateDailyFoodConsumptionf w aktywnym lancuchu - BK sam karmi na pustyni i w sniegu
            public Func<MobileParty, bool, float> BkNeed;   // BK CalculateAnimalFoodNeed obiektu BK z aktywnego lancucha (doplata roznicy E4/1 - BK)
            public float Horse = 0.25f, Warhorse = 0.5f, Mule = 0.15f, Cattle = 0.175f, Pig = 0.1f, Sheep = 0.05f;   // stawki BK (domyslne = liczby z BK)
            public string RatesFrom = "domyslne (liczby BK z kodu)";
            public bool Logged;
        }
        private static volatile Feed _feed;

        private static int _stumbles, _stumblesDay;
        private static readonly Dictionary<string, int> _stumbleAt = new Dictionary<string, int>();

        // wariant (a) Jeffa 11.10: pasza BK po glowie (postfiks na BK CalculateAnimalFoodNeed)
        private static bool _bkFeedHooked;
        private static string _bkFeedStart = "nie wpieta";
        [ThreadStatic] private static float _bkOrig;     // wynik formuly BK sprzed podmiany (ostatnie wywolanie w tym watku) - do linii "przed"
        [ThreadStatic] private static bool _bkOrigSet;

        internal const int WhyOff = 0, WhyFed = 1, WhySteppe = 2, WhyDesert = 3, WhySnow = 4, WhyNoRealm = 5, WhyNoAnimals = 6,
                           WhyTopDesert = 7, WhyTopSnow = 8, WhyCount = 9;   // Desert/Snow = pasza BK nie mniejsza (bez doplaty), Top* = doplata roznicy

        private static bool On { get { var s = Settings.Current; return s != null && s.WinterByRealm; } }

        /// <summary>0 = Wyspy Letnie, 1 = cieple, 2 = reszta.</summary>
        private static int GroupOf(string culture)
        {
            if (culture == SummerIsles) return 0;
            if (culture != null && Warm.Contains(culture)) return 1;
            return 2;
        }

        /// <summary>Czynnik krainy wedlug kultury regionu (1 przy wylaczonym WinterByRealm albo nieznanej kulturze).</summary>
        internal static float FactorOf(string culture)
        {
            var s = Settings.Current;
            if (s == null || !s.WinterByRealm || string.IsNullOrEmpty(culture)) return 1f;
            int g = GroupOf(culture);
            if (g == 0) return 0f;
            if (g == 1) return MBMath.ClampFloat(s.WinterWarmRealmFactor, 0f, 1f);
            return 1f;
        }

        private static void Fresh()   // pod Gate
        {
            var cur = Campaign.Current;
            if (ReferenceEquals(cur, _camp)) return;
            _camp = cur; _regionCulture.Clear(); _party.Clear(); _feed = null;
            _stumbles = 0; _stumblesDay = 0; _stumbleAt.Clear();
        }

        private static void Stumble(string where, Exception e)
        {
            int n = Interlocked.Increment(ref _stumbles);
            Interlocked.Increment(ref _stumblesDay);
            lock (Gate) { int v; _stumbleAt.TryGetValue(where, out v); _stumbleAt[where] = v + 1; }
            if (n <= 3) Log.Error("WinterRealm." + where, e);
        }

        /// <summary>Kultura regionu z pierwszej doby sesji (Measure40.CultureAtStart - jedna migawka z linia "Zima (E4/0)"), z pamiecia.</summary>
        private static string RegionCulture(Settlement region)
        {
            if (region == null) return null;
            lock (Gate)
            {
                Fresh();
                string c;
                if (_regionCulture.TryGetValue(region, out c)) return c;
                c = Measure40.CultureAtStart(region);
                _regionCulture[region] = c;
                return c;
            }
        }

        /// <summary>Czynnik krainy wsi: region wsi (jej warownia).</summary>
        internal static float OfVillage(Village v)
        {
            if (!On || v == null || v.Settlement == null) return 1f;
            try { return FactorOf(RegionCulture(OutlawLaw.RegionFor(v.Settlement))); }
            catch (Exception e) { Stumble("OfVillage", e); return 1f; }
        }

        /// <summary>Czynnik krainy warowni (spichlerz): warownia jest swoim regionem.</summary>
        internal static float OfTown(Town t)
        {
            if (!On || t == null || t.Settlement == null) return 1f;
            try { return FactorOf(RegionCulture(OutlawLaw.RegionFor(t.Settlement))); }
            catch (Exception e) { Stumble("OfTown", e); return 1f; }
        }

        /// <summary>Czynnik krainy partii: region pod partia (OutlawLaw.RegionAt), liczony raz na dobe na partie.</summary>
        internal static float OfParty(MobileParty mp)
        {
            if (!On || mp == null) return 1f;
            try { return FactorOf(PartyCulture(mp)); }
            catch (Exception e) { Stumble("OfParty", e); return 1f; }
        }

        private static string PartyCulture(MobileParty mp)
        {
            int day = (int)CampaignTime.Now.ToDays;
            lock (Gate)
            {
                Fresh();
                Spot p;
                if (_party.TryGetValue(mp, out p) && p.Day == day) return p.Culture;
            }
            string c = RegionCulture(OutlawLaw.RegionAt(mp.GetPosition2D));   // 227 wezlow poza blokada - tylko odczyt pozycji
            lock (Gate) { _party[mp] = new Spot { Day = day, Culture = c }; }
            return c;
        }

        // ------------------------------------------------------------ pasza: lancuch modelu jedzenia i stawki (raz na kampanie)

        private static Feed FeedInfo()
        {
            var cur = Campaign.Current;
            var f = _feed;
            if (f != null && ReferenceEquals(f.Camp, cur)) return f;
            lock (Gate)
            {
                Fresh();
                f = _feed;
                if (f != null && ReferenceEquals(f.Camp, cur)) return f;
                f = new Feed { Camp = cur };
                var models = cur != null ? cur.Models : null;
                if (models == null) return f;   // bez pamieci - modeli jeszcze nie ma, nastepny odczyt sprobuje znowu
                try
                {
                    // stawki z modelu BK z listy modeli gry (niezaleznie od tego, czy BK jest w aktywnym lancuchu)
                    foreach (var gm in models.GetGameModels())
                    {
                        if (gm == null) continue;
                        var t = gm.GetType();
                        if (AccessTools.Method(t, "CalculateAnimalFoodNeed", new[] { typeof(MobileParty), typeof(bool) }) == null) continue;
                        f.Horse = Rate(gm, t, "HorseFood", f.Horse);
                        f.Warhorse = Rate(gm, t, "WarhorseFood", f.Warhorse);
                        f.Mule = Rate(gm, t, "MuleFood", f.Mule);
                        f.Cattle = Rate(gm, t, "CattleFood", f.Cattle);
                        f.Pig = Rate(gm, t, "PigFood", f.Pig);
                        f.Sheep = Rate(gm, t, "SheepFood", f.Sheep);
                        f.RatesFrom = t.FullName;
                        break;
                    }
                }
                catch (Exception e) { Stumble("FeedInfo(stawki)", e); }
                try
                {
                    object m = models.MobilePartyFoodConsumptionModel;
                    f.Active = m != null ? m.GetType().FullName : "BRAK";
                    var sb = new StringBuilder();
                    var seen = new List<object>();
                    for (int i = 0; m != null && i < 16; i++)
                    {
                        bool again = false;
                        foreach (var o in seen) if (ReferenceEquals(o, m)) { again = true; break; }
                        if (again) { sb.Append(" (petla)"); break; }
                        seen.Add(m);
                        var t = m.GetType();
                        sb.Append(i > 0 ? " -> " : "").Append(t.Name);
                        var calc = t.GetMethod("CalculateDailyFoodConsumptionf", new[] { typeof(MobileParty), typeof(ExplainedNumber) });
                        if (calc != null && calc.DeclaringType != null && calc.DeclaringType.FullName == BkModel)
                        {
                            f.BkFeeds = true;
                            try   // pasza BK tego samego obiektu, ktory karmi w lancuchu - do doplaty roznicy E4/1 na pustyni i w sniegu
                            {
                                var an = AccessTools.Method(t, "CalculateAnimalFoodNeed", new[] { typeof(MobileParty), typeof(bool) });
                                if (an != null && !an.IsStatic && an.ReturnType == typeof(float))
                                    f.BkNeed = (Func<MobileParty, bool, float>)Delegate.CreateDelegate(typeof(Func<MobileParty, bool, float>), m, an);
                            }
                            catch (Exception e) { Stumble("FeedInfo(pasza BK)", e); f.BkNeed = null; }
                            break;
                        }
                        // E4/6a: RBL z naprawionym lancuchem (FoodChain) oddaje metody swojemu BaseModel - idziemy dalej w glab (pole BaseModel gry)
                        if (t.BaseType != typeof(MobilePartyFoodConsumptionModel) && !FoodChain.Chains(m)) { sb.Append(" (konczy lancuch - nie wola modelu wewnetrznego)"); break; }
                        if (t.BaseType != typeof(MobilePartyFoodConsumptionModel)) sb.Append(" (lancuch E4/6a)");
                        m = Inner(m, t);
                    }
                    f.Chain = sb.ToString();
                }
                catch (Exception e) { Stumble("FeedInfo(lancuch)", e); }
                _feed = f;
                return f;
            }
        }

        private static float Rate(object model, Type t, string name, float def)
        {
            var p = AccessTools.Property(t, name);
            if (p == null || p.PropertyType != typeof(float)) return def;
            float v = (float)p.GetValue(model, null);
            return v >= 0f && v <= 5f ? v : def;
        }

        /// <summary>Model wewnetrzny owijki: pierwsze pole typu MobilePartyFoodConsumptionModel (od typu w dol hierarchii - ROT _previousModel,
        /// BEE _baseModel, na koncu pole wlasciwosci BaseModel gry, z ktorego korzysta NavalDLC).</summary>
        private static object Inner(object m, Type t)
        {
            for (var k = t; k != null && k != typeof(object); k = k.BaseType)
                foreach (var fi in k.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (!typeof(MobilePartyFoodConsumptionModel).IsAssignableFrom(fi.FieldType)) continue;
                    var v = fi.GetValue(m);
                    if (v != null && !ReferenceEquals(v, m)) return v;
                }
            return null;
        }

        /// <summary>Pelna pasza zwierzat partii na dobe: konni w skladzie po glowie x kon (E6/K0 FodderFromHorseState: konie pod jezdzcami + wolne konie
        /// zbrojowni ze stanu koni partii - HorseState.FodderMounts) + zwierzeta w taborze x ich stawka (stawki BK).
        /// ignoreCamels - warunek terenu BK (pustynia): wielblady w taborze bez paszy (ten sam test co BK: StringId zawiera "camel").
        /// Jedna liczba zwierzat dla zimy E4/1 (HorseFeed, ignoreCamels false) i dla paszy BK (BkAnimalPostfix, wariant (a)).</summary>
        private static float AnimalNeed(MobileParty party, Feed f, bool ignoreCamels, out int riders, out int herd)
        {
            riders = 0; herd = 0;
            float need = 0f;
            // E6/K0 (FodderFromHorseState): konie, ktore partia naprawde ma - pod jezdzcami (bez jezdzcow bez konia ze stanu koni partii) i wolne
            // w zbrojowni DTE, zamiast konnych po glowie; partia bez stanu (bez zbrojowni, gracz) i wylacznik wylaczony - po glowie jak dotad
            int heads, free, unhorsed;
            int horses = HorseState.FodderMounts(party, out heads, out free, out unhorsed);
            var s = Settings.Current;
            riders = s != null && s.FodderFromHorseState ? horses : heads;
            need += riders * f.Horse;
            var ir = party.ItemRoster;
            if (ir != null)
                for (int i = 0; i < ir.Count; i++)
                {
                    var el = ir[i];
                    var item = el.EquipmentElement.Item;
                    if (item == null || el.Amount <= 0 || !item.HasHorseComponent || item.HorseComponent == null) continue;
                    if (ignoreCamels && item.StringId != null && item.StringId.Contains("camel")) continue;
                    float rate;
                    if (item.HorseComponent.IsPackAnimal) rate = f.Mule;
                    else if (item.ItemCategory == DefaultItemCategories.WarHorse) rate = f.Warhorse;
                    else if (item.ItemCategory == DefaultItemCategories.Sheep) rate = f.Sheep;
                    else if (item.ItemCategory == DefaultItemCategories.Hog) rate = f.Pig;
                    else if (item.ItemCategory == DefaultItemCategories.Cow) rate = f.Cattle;
                    else rate = f.Horse;   // kon wierzchowy, szlachetny, wielblad (BK tu bierze stawke ptaka 0.025 - u nas kon jak kon)
                    need += el.Amount * rate;
                    herd += el.Amount;
                }
            return need;
        }

        /// <summary>Gdzie BK karmi zwierzeta (BK CalculateDailyFoodConsumptionf): 1 = teren pod partia pustynia (pelna pasza, bez wielbladow),
        /// 2 = poza pustynia snieg/zamiec w miejscu partii (pol paszy), 0 = BK nie karmi.</summary>
        private static int BkFeedAt(MobileParty party, TerrainType terrain)
        {
            if (terrain == TerrainType.Desert) return 1;
            var w = Campaign.Current.Models.MapWeatherModel.GetWeatherEventInPosition(party.GetPosition2D);
            return w == MapWeatherModel.WeatherEvent.Snowy || w == MapWeatherModel.WeatherEvent.Blizzard ? 2 : 0;
        }

        /// <summary>Doplata paszy koni w zimie (dodatnia liczba jedzenia na dobe; 0 = bez doplaty, powod w why) - jedna regula dla
        /// WinterBite.FoodPostfix i linii dobowej. Wolac tylko w zimie. need = pelna pasza zwierzat partii (przed udzialem i kraina);
        /// bk = pasza, ktora w tym miejscu nalicza sam BK (pustynia / snieg przy regule paszy BK w lancuchu; gdzie indziej 0) - doplata to wtedy
        /// tylko roznica ponad nia (w zimie pasza = wieksza z E4/1 i BK).</summary>
        internal static float HorseFeed(MobileParty party, float realm, out int why, out float need, out float bk, out int riders, out int herd)
        {
            why = WhyOff; need = 0f; bk = 0f; riders = 0; herd = 0;
            var s = Settings.Current;
            if (s == null || !s.WinterHorsesEat || party == null) return 0f;
            float share = MBMath.ClampFloat(s.WinterHorseFeedShare, 0f, 1f);
            if (share <= 0f) return 0f;
            if (realm <= 0f) { why = WhyNoRealm; return 0f; }
            try
            {
                var f = FeedInfo();
                var terrain = Campaign.Current.MapSceneWrapper.GetFaceTerrainType(party.CurrentNavigationFace);
                if (terrain == TerrainType.Steppe) { why = WhySteppe; return 0f; }
                // regula paszy BK biegnie: 1 = pustynia (BK pelna pasza), 2 = snieg/zamiec (BK pol) - ten sam warunek co BK
                int bkAt = f.BkFeeds ? BkFeedAt(party, terrain) : 0;   // (najpierw teren pod partia, potem pogoda w miejscu partii)
                need = AnimalNeed(party, f, false, out riders, out herd);
                if (need <= 0f) { why = WhyNoAnimals; return 0f; }
                float own = need * share * realm;
                if (bkAt == 0) { why = WhyFed; return own; }
                // tam karmi BK (w CalculateDailyFoodConsumptionf, ta sama ExplainedNumber co nasza baza) - doplacamy tylko roznice;
                // BkNeed idzie przez BkAnimalPostfix - przy BkAnimalFeedPerHead to juz pasza po glowie (wariant (a)), bez podwojnej paszy;
                // pasza BK nieczytelna - jak przed poprawka: bez doplaty (bez ryzyka podwojnej paszy)
                if (f.BkNeed == null) { why = bkAt == 1 ? WhyDesert : WhySnow; return 0f; }
                bk = bkAt == 1 ? f.BkNeed(party, true) : f.BkNeed(party, false) / 2f;
                if (!(bk > 0f)) bk = 0f;
                float top = own - bk;
                if (top <= 0f) { why = bkAt == 1 ? WhyDesert : WhySnow; return 0f; }
                why = bkAt == 1 ? WhyTopDesert : WhyTopSnow;
                return top;
            }
            catch (Exception e) { Stumble("HorseFeed", e); why = WhyOff; need = 0f; bk = 0f; riders = 0; herd = 0; return 0f; }
        }

        // ------------------------------------------------------------ wariant (a) Jeffa 11.10: pasza BK po glowie

        /// <summary>Czy pasza BK liczy sie teraz po glowie (latka stoi i wylacznik wlaczony) - do opisow w liniach.</summary>
        internal static bool BkPerHeadNow { get { var s = Settings.Current; return _bkFeedHooked && s != null && s.BkAnimalFeedPerHead; } }

        /// <summary>Start (SubModuleMain, obok FoodChain.ApplyAll): postfiks na BK CalculateAnimalFoodNeed - wpiety zawsze, gdy BK jest;
        /// wylacznik BkAnimalFeedPerHead czytany przy kazdym wywolaniu (na zywo).</summary>
        internal static void ApplyBkFeed(Harmony h)
        {
            try
            {
                var t = AccessTools.TypeByName(BkModel);
                var m = t != null ? AccessTools.DeclaredMethod(t, "CalculateAnimalFoodNeed", new[] { typeof(MobileParty), typeof(bool) }) : null;
                if (m == null || m.IsStatic || m.ReturnType != typeof(float))
                {
                    _bkFeedStart = t == null ? "brak BannerKings" : "brak BK CalculateAnimalFoodNeed(MobileParty, bool)";
                    Log.Info("WinterRealm: pasza BK po glowie (wariant a) NIE wpieta - " + _bkFeedStart + "; pasza BK jak dotad.");
                    return;
                }
                h.Patch(m, postfix: new HarmonyMethod(typeof(WinterRealm), nameof(BkAnimalPostfix)) { priority = Priority.Last });
                _bkFeedHooked = true;
                _bkFeedStart = "wpieta";
                Log.Info("WinterRealm: pasza BK po glowie (wariant a Jeffa 11.10) - wpieta na BKPartyConsumptionModel.CalculateAnimalFoodNeed: konni po glowie"
                         + " (bez jencow) x kon, zwierzeta taboru x stawka BK (kon i wielblad w taborze stawka konia); warunki terenu BK bez zmian (pustynia pelna"
                         + " bez wielbladow, snieg/zamiec pol); wylacznik BkAnimalFeedPerHead (na zywo) teraz " + (Settings.Current.BkAnimalFeedPerHead ? "TAK" : "NIE") + ".");
            }
            catch (Exception e) { _bkFeedHooked = false; _bkFeedStart = "wyjatek przy wpinaniu " + e.GetType().Name; Log.Error("WinterRealm.ApplyBkFeed", e); }
        }

        /// <summary>BK CalculateAnimalFoodNeed: zamiast formuly BK (1 kon na typ oddzialu, konni jency, kon w taborze stawka ptaka) nasza liczba
        /// zwierzat po glowie - ta sama AnimalNeed co pasza zimowa E4/1; ignoreCamels BK (pustynia) zachowane. Wynik BK zapamietany w watku
        /// (linia "przed"). Wylacznik wylaczony / wyjatek - wynik BK jak dotad (wyjatek liczony jako potkniecie, bez wylacznika).</summary>
        public static void BkAnimalPostfix(MobileParty __0, bool __1, ref float __result)
        {
            _bkOrig = __result; _bkOrigSet = true;
            var s = Settings.Current;
            if (s == null || !s.BkAnimalFeedPerHead || __0 == null) return;
            try
            {
                int riders, herd;
                __result = AnimalNeed(__0, FeedInfo(), __1, out riders, out herd);
            }
            catch (Exception e) { Stumble("BkAnimal", e); __result = _bkOrig; }
        }

        /// <summary>Do linii zimy (codziennie w zimie) i linii lancucha E4/6a (raz na kampanie): partie, ktore BK karmi teraz (te same warunki
        /// co BK), pasza formula BK (przed) i po glowie (po) - na dobe, przed mnoznikami (zima, perki, Rations).</summary>
        internal static string BkFeedText()
        {
            var s = Settings.Current;
            var sb = new StringBuilder(300);
            sb.Append("pasza BK po glowie (wariant a, BkAnimalFeedPerHead): ").Append(s != null && s.BkAnimalFeedPerHead ? "TAK" : "NIE - formula BK");
            if (!_bkFeedHooked) return sb.Append(" - latka nie stoi (").Append(_bkFeedStart).Append("), liczy formula BK").ToString();
            var f = FeedInfo();
            if (!f.BkFeeds || f.BkNeed == null)
                return sb.Append(" - regula paszy BK poza aktywnym lancuchem modelu jedzenia (BK nigdzie nie karmi), poprawiac nie ma czego").ToString();
            var fm = Campaign.Current.Models.MobilePartyFoodConsumptionModel;
            int n = 0, nDesert = 0, nSnow = 0, fixedN = 0; double before = 0, after = 0;
            foreach (var mp in MobileParty.All)
            {
                try
                {
                    if (mp == null || !mp.IsActive || fm == null || !fm.DoesPartyConsumeFood(mp)) continue;
                    int at = BkFeedAt(mp, Campaign.Current.MapSceneWrapper.GetFaceTerrainType(mp.CurrentNavigationFace));
                    if (at == 0) continue;
                    _bkOrigSet = false;
                    float now = f.BkNeed(mp, at == 1);           // przez BkAnimalPostfix: po glowie (albo formula BK przy wylaczniku)
                    float was = _bkOrigSet ? _bkOrig : now;      // formula BK sprzed podmiany
                    if (at == 2) { now /= 2f; was /= 2f; }
                    n++; if (at == 1) nDesert++; else nSnow++;
                    before += was; after += now;
                    if (Math.Abs(now - was) > 0.0005f) fixedN++;
                }
                catch (Exception e) { Stumble("BkFeedText(partia)", e); }
            }
            sb.Append(" - partii, ktore BK karmi teraz: ").Append(n).Append(" (pustynia ").Append(nDesert).Append(", snieg/zamiec ").Append(nSnow)
              .Append("), poprawionych po glowie ").Append(fixedN).Append(" | pasza tych partii: formula BK ").Append(before.ToString("0.0", Inv))
              .Append("/d -> po glowie ").Append(after.ToString("0.0", Inv)).Append("/d (przed mnoznikami)");
            return sb.ToString();
        }

        /// <summary>Linia startowa (WinterBite.ApplyAll): stan kluczy E4/1.</summary>
        internal static string StartText()
        {
            var s = Settings.Current;
            if (s == null) return "zima wedlug krain (E4/1): brak ustawien";
            return "zima wedlug krain (E4/1): " + (s.WinterByRealm ? "TAK - Wyspy Letnie x0, cieple (Dorne, Qarth, Zatoka, Lys, Tyrosh, Myr, Volantis, Valyria) x"
                       + MBMath.ClampFloat(s.WinterWarmRealmFactor, 0f, 1f).ToString("0.00", Inv) + ", reszta x1 (kultura regionu z pierwszej doby sesji)" : "NIE (ta sama zima wszedzie)")
                 + "; konie jedza zima: " + (s.WinterHorsesEat ? "TAK - " + MBMath.ClampFloat(s.WinterHorseFeedShare, 0f, 1f).ToString("0.00", Inv)
                       + " pelnej paszy (stawki BK, konni po glowie bez jencow, zwierzeta taboru) x kraina, bez doplaty na stepie; na pustyni i w sniegu,"
                       + " gdy regula paszy BK jest w aktywnym modelu jedzenia, tylko roznica ponad pasze BK (linia 'pasza koni zima - model jedzenia' w pierwszej dobie)" : "NIE");
        }

        /// <summary>Raz na dobe (WinterBite.OnDaily, po WesterosClimate.Daily): pamiec doby od nowa; raz na kampanie linia modelu jedzenia;
        /// w zimie linia "Zima wedlug krain (E4/1)".</summary>
        internal static void Daily()
        {
            lock (Gate) { Fresh(); _party.Clear(); }   // region pod partia od nowa (i bez partii, ktore zniknely)
            try
            {
                var s = Settings.Current;
                if (s != null && s.WinterBiteEnabled && s.WinterHorsesEat)
                {
                    var f = FeedInfo();
                    if (!f.Logged && f.Camp != null) { f.Logged = true; Log.Info(FeedText(f)); }
                }
            }
            catch (Exception e) { Stumble("FeedText", e); }
            if (!WinterBite.WinterNow()) { Interlocked.Exchange(ref _stumblesDay, 0); return; }
            try { Log.Info(Line()); }
            catch (Exception e) { Stumble("Line", e); }
            Interlocked.Exchange(ref _stumblesDay, 0);
        }

        private static string FeedText(Feed f)
        {
            return "Zima wedlug krain (E4/1): pasza koni zima - model jedzenia partii aktywny: " + f.Active + " | lancuch: " + f.Chain
                 + " | regula paszy BK (pustynia pelna, snieg/zamiec pol): " + (f.BkFeeds
                       ? (f.BkNeed != null ? "BIEGNIE - tam w zimie doplata E4/1 tylko roznicy ponad pasze BK (pasza = wieksza z E4/1 i BK; poza zima sam BK)"
                                           : "BIEGNIE - pasza BK nieczytelna (CalculateAnimalFoodNeed), tam bez doplaty zimowej")
                       : "NIE BIEGNIE (BK poza aktywnym lancuchem) - doplata zimowa pelna takze na pustyni i w sniegu")
                 + " | zwierzeta w paszy BK: " + (BkPerHeadNow ? "po glowie (wariant a - ta sama liczba co zima E4/1)" : "formula BK (1 kon na typ oddzialu; " + (_bkFeedHooked ? "wylacznik BkAnimalFeedPerHead" : "latka " + _bkFeedStart) + ")")
                 + " | stawki paszy (" + f.RatesFrom + "): kon " + f.Horse.ToString("0.###", Inv) + ", kon bojowy " + f.Warhorse.ToString("0.###", Inv)
                 + ", juczny " + f.Mule.ToString("0.###", Inv) + ", krowa " + f.Cattle.ToString("0.###", Inv) + ", swinia " + f.Pig.ToString("0.###", Inv)
                 + ", owca " + f.Sheep.ToString("0.###", Inv) + "; " + (Settings.Current != null && Settings.Current.FodderFromHorseState
                       ? "konie partii (E6/K0: pod jezdzcami bez jezdzcow bez konia + wolne w zbrojowni DTE; partia bez zbrojowni - konni po glowie) x kon"
                       : "konni po glowie (sklad partii, bez jencow) x kon") + ", zwierzeta taboru x stawka.";
        }

        private static string Line()
        {
            long t0 = Stopwatch.GetTimestamp();
            var s = Settings.Current;
            int day = (int)CampaignTime.Now.ToDays - 1;   // jak linie E4/0 (Measure40)
            var sb = new StringBuilder(800);
            sb.Append("Zima wedlug krain (E4/1): dzien ").Append(day);
            sb.Append(" | czynnik krainy x gradient polnocy: ").Append(s.WinterByRealm ? "TAK" : "NIE (wylaczony - wszedzie 1)")
              .Append(" - Wyspy Letnie ").Append(FactorOf(SummerIsles).ToString("0.00", Inv))
              .Append(", cieple (Dorne, Qarth, Zatoka, Lys, Tyrosh, Myr, Volantis, Valyria) ").Append(FactorOf("aserai").ToString("0.00", Inv))
              .Append(", reszta ").Append(FactorOf("battania").ToString("0.00", Inv));

            // wsie wedlug grup krain (ten sam rachunek co produkcja wsi - WinterBite.VillageCut)
            var vn = new int[3]; var vcut = new double[3];
            var rn = new int[3];
            foreach (var st in Settlement.All)
            {
                try
                {
                    if (st == null) continue;
                    if (st.IsTown || st.IsCastle) rn[GroupOf(RegionCulture(st))]++;
                    if (!st.IsVillage || st.Village == null) continue;
                    int g = GroupOf(RegionCulture(OutlawLaw.RegionFor(st)));
                    vn[g]++; vcut[g] += WinterBite.VillageCut(st.Village);
                }
                catch (Exception e) { Stumble("Line(osada)", e); }
            }
            sb.Append(" | regiony (warownie): Wyspy Letnie ").Append(rn[0]).Append(", cieple ").Append(rn[1]).Append(", reszta ").Append(rn[2]);
            sb.Append(" | ciecie plonu wsi dzis: Wyspy Letnie ").Append(Pct(vcut[0], vn[0])).Append(" (").Append(vn[0]).Append(" wsi), cieple ").Append(Pct(vcut[1], vn[1]))
              .Append(" (").Append(vn[1]).Append("), reszta ").Append(Pct(vcut[2], vn[2])).Append(" (").Append(vn[2]).Append(")");

            // spichlerze warowni: dodatkowy ubytek zima (ten sam rachunek co TownFoodPostfix)
            double townX = 0; int townN = 0;
            foreach (var t in Town.AllTowns) { try { townX += WinterBite.TownExtra(t); townN++; } catch (Exception e) { Stumble("Line(miasto)", e); } }
            foreach (var t in Town.AllCastles) { try { townX += WinterBite.TownExtra(t); townN++; } catch (Exception e) { Stumble("Line(zamek)", e); } }
            sb.Append(" | spichlerze warowni: dodatkowy ubytek zima -").Append(townX.ToString("0.0", Inv)).Append("/d w ").Append(townN).Append(" warowniach");

            // pasza koni: partie, ktore jedza (aktywny model gry), stan w chwili odczytu
            var f = FeedInfo();
            var fm = Campaign.Current.Models.MobilePartyFoodConsumptionModel;
            int eat = 0, fed = 0, fedDesert = 0, fedSnow = 0; long riders = 0, herd = 0;
            long k0Horses = 0, k0Heads = 0, k0Free = 0, k0Unhorsed = 0;   // E6/K0: pasza z konmi partii wobec konnych po glowie (partie z doplata)
            double feed = 0, needFed = 0, men = 0, bonus = 0, bkTop = 0;
            var why = new int[WhyCount];
            foreach (var mp in MobileParty.All)
            {
                try
                {
                    if (mp == null || !mp.IsActive || fm == null || !fm.DoesPartyConsumeFood(mp)) continue;
                    eat++;
                    float realm = OfParty(mp);
                    int n = mp.MemberRoster != null ? mp.MemberRoster.TotalManCount : 0;
                    men += n; bonus += n * WinterBite.PartyFoodBonus(mp);
                    int w, r, h; float need, bkp;
                    float x = HorseFeed(mp, realm, out w, out need, out bkp, out r, out h);
                    why[w]++;
                    if (x <= 0f) continue;
                    fed++; feed += x; needFed += need; riders += r; herd += h;
                    int kh, kf, ku; k0Horses += HorseState.FodderMounts(mp, out kh, out kf, out ku); k0Heads += kh; k0Free += kf; k0Unhorsed += ku;
                    if (w == WhyTopDesert || w == WhyTopSnow) bkTop += bkp;
                    if (!f.BkFeeds)
                    {
                        var terrain = Campaign.Current.MapSceneWrapper.GetFaceTerrainType(mp.CurrentNavigationFace);
                        if (terrain == TerrainType.Desert) fedDesert++;
                        else
                        {
                            var we = Campaign.Current.Models.MapWeatherModel.GetWeatherEventInPosition(mp.GetPosition2D);
                            if (we == MapWeatherModel.WeatherEvent.Snowy || we == MapWeatherModel.WeatherEvent.Blizzard) fedSnow++;
                        }
                    }
                }
                catch (Exception e) { Stumble("Line(partia)", e); }
            }
            sb.Append(" | jedzenie partii zima: partii jedzacych ").Append(eat).Append(", dodatek ludzi srednio +").Append(men > 0 ? (100.0 * bonus / men).ToString("0.0", Inv) : "0").Append('%');
            sb.Append(" | pasza koni zima (").Append(s.WinterHorsesEat ? MBMath.ClampFloat(s.WinterHorseFeedShare, 0f, 1f).ToString("0.00", Inv) + " pelnej paszy x kraina" : "WYLACZONA").Append("): z doplata ").Append(fed)
              .Append(" partii (konnych ").Append(riders).Append(", zwierzat w taborach ").Append(herd).Append("), +").Append(feed.ToString("0.0", Inv))
              .Append(" jedzenia/d razem przed mnoznikami (pelna pasza tych partii ").Append(needFed.ToString("0.0", Inv)).Append("/d)");
            if (!f.BkFeeds) sb.Append("; w tym na pustyni ").Append(fedDesert).Append(", w sniegu/zamieci ").Append(fedSnow).Append(" (BK tam nie karmi)");
            else sb.Append("; w tym doplata roznicy ponad pasze BK: pustynia ").Append(why[WhyTopDesert]).Append(", snieg/zamiec ").Append(why[WhyTopSnow])
                   .Append(" (pasza BK tych partii ").Append(bkTop.ToString("0.0", Inv)).Append("/d - liczy ja BK)");
            sb.Append("; bez doplaty: step ").Append(why[WhySteppe]).Append(", pustynia (pasza BK nie mniejsza) ").Append(why[WhyDesert])
              .Append(", snieg/zamiec (pasza BK nie mniejsza) ").Append(why[WhySnow])
              .Append(", kraina bez zimy ").Append(why[WhyNoRealm]).Append(", bez zwierzat ").Append(why[WhyNoAnimals]);
            sb.Append(" | pasza (E6/K0, FodderFromHorseState ").Append(s.FodderFromHorseState ? "TAK" : "NIE").Append("): konie partii ").Append(k0Horses)
              .Append(" (dawniej po glowie ").Append(k0Heads).Append("; wolne w zbrojowni jedza ").Append(k0Free).Append(", jezdzcy bez konia nie jedza ").Append(k0Unhorsed).Append(')');
            sb.Append(" | model jedzenia: ").Append(f.Active).Append(", regula paszy BK ").Append(f.BkFeeds ? "BIEGNIE" : "NIE BIEGNIE");
            try { sb.Append(" | ").Append(BkFeedText()); } catch (Exception e) { Stumble("Line(pasza BK)", e); }
            sb.Append(" | potkniecia dzis ").Append(Volatile.Read(ref _stumblesDay)).Append(", od startu sesji ").Append(Volatile.Read(ref _stumbles));
            lock (Gate) { if (_stumbleAt.Count > 0) { sb.Append(" ("); bool first = true; foreach (var kv in _stumbleAt) { sb.Append(first ? "" : ", ").Append(kv.Key).Append(' ').Append(kv.Value); first = false; } sb.Append(')'); } }
            sb.Append(" | czas ").Append(((Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency).ToString("0.0", Inv)).Append(" ms.");
            return sb.ToString();
        }

        private static string Pct(double sum, int n) { return n > 0 ? (100.0 * sum / n).ToString("0.0", Inv) + "%" : "-"; }
    }
}
