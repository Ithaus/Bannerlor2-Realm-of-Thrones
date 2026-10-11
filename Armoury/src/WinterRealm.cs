using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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
    /// KONIE JEDZA ZIMA (P10 Z4, ES:211 e "kon jak 2.5 czlowieka"): w zimie partia doplaca WinterHorseFeedShare (0.5) paszy BK
    /// (BKPartyConsumptionModel.CalculateAnimalFoodNeed(party, false): kon 0.25, kon bojowy 0.5, juczny 0.15, ... na dobe) x czynnik krainy -
    /// pol paszy konia = 0.125 = 2.5 x czlowiek (1/20). Bez doplaty, gdy BK juz liczy pasze (pustynia - pelna, snieg/zamiec - pol; ten sam
    /// warunek co BK: teren pod partia, pogoda w miejscu partii) albo partia stoi na stepie (TerrainType.Steppe - wypas zimowy; regula wedlug
    /// terenu, nie kultury). Doplata wchodzi do bazy jedzenia (WinterBite.FoodPostfix na DefaultMobilePartyFoodConsumptionModel - wolany
    /// na modelu BK, ktory po nim dziedziczy; ROT/NavalDLC/BEE go owijaja), wiec obejmuja ja te same mnozniki co ludzi: zima x gradient x kraina,
    /// perki, Rations -40% na zewnatrz (kon zostaje 2.5 x czlowiek w kazdej porze). Model bez CalculateAnimalFoodNeed (bez BK) = bez doplaty
    /// (linia: "model paszy BRAK").
    /// Watki: jedzenie partii bywa liczone rownolegle - pamieci pod jedna blokada; wyjatki per obiekt liczone (potkniecia), bez wylacznikow.
    /// </summary>
    internal static class WinterRealm
    {
        private const string SummerIsles = "summer";
        private static readonly HashSet<string> Warm = new HashSet<string>(StringComparer.Ordinal)
            { "aserai", "qartheen", "ghiscari", "lyseni", "tyroshi", "myrish", "volantine", "valyrian" };
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static readonly object Gate = new object();
        private static Campaign _camp;                                    // kampania pamieci (nowa kampania / wczytanie = od zera)
        private static readonly Dictionary<Settlement, string> _regionCulture = new Dictionary<Settlement, string>();
        private sealed class Spot { public int Day; public string Culture; }
        private static readonly Dictionary<MobileParty, Spot> _party = new Dictionary<MobileParty, Spot>();

        // pasza BK: CalculateAnimalFoodNeed modelu, na ktorym gra liczy baze jedzenia (BK), jako delegat zwiazany z instancja modelu
        private sealed class Hook { public object Owner; public Func<MobileParty, bool, float> Need; public string Name; }
        private static volatile Hook _hook;                               // szybka sciezka - ostatni dzialajacy (model BK kampanii)
        private static readonly Dictionary<object, Hook> _hooks = new Dictionary<object, Hook>();

        private static int _stumbles, _stumblesDay;
        private static readonly Dictionary<string, int> _stumbleAt = new Dictionary<string, int>();

        internal const int WhyOff = 0, WhyFed = 1, WhySteppe = 2, WhyDesert = 3, WhySnow = 4, WhyNoRealm = 5, WhyNoHook = 6, WhyNoAnimals = 7;

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
            _camp = cur; _regionCulture.Clear(); _party.Clear(); _hooks.Clear(); _hook = null;
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

        private static Hook HookOf(object model)
        {
            if (model == null) return null;
            var h = _hook;
            if (h != null && ReferenceEquals(h.Owner, model)) return h;
            lock (Gate)
            {
                Fresh();
                if (_hooks.TryGetValue(model, out h)) return h;
                h = new Hook { Owner = model };
                try
                {
                    var mi = AccessTools.Method(model.GetType(), "CalculateAnimalFoodNeed", new[] { typeof(MobileParty), typeof(bool) });
                    if (mi != null && !mi.IsStatic && mi.ReturnType == typeof(float))
                    {
                        h.Need = (Func<MobileParty, bool, float>)Delegate.CreateDelegate(typeof(Func<MobileParty, bool, float>), model, mi);
                        h.Name = mi.DeclaringType != null ? mi.DeclaringType.FullName : model.GetType().FullName;
                    }
                    else h.Name = model.GetType().FullName;
                }
                catch (Exception e) { Stumble("HookOf", e); }
                _hooks[model] = h;
                if (h.Need != null) _hook = h;
                return h;
            }
        }

        /// <summary>Doplata paszy koni w zimie (dodatnia liczba jedzenia na dobe; 0 = bez doplaty, powod w why) - jedna regula dla
        /// WinterBite.FoodPostfix i linii dobowej. Wolac tylko w zimie. model = instancja modelu jedzenia, na ktorej gra liczy baze (BK).</summary>
        internal static float HorseFeed(object model, MobileParty party, float realm, out int why, out float need)
        {
            why = WhyOff; need = 0f;
            var s = Settings.Current;
            if (s == null || !s.WinterHorsesEat || party == null) return 0f;
            float share = MBMath.ClampFloat(s.WinterHorseFeedShare, 0f, 1f);
            if (share <= 0f) return 0f;
            if (realm <= 0f) { why = WhyNoRealm; return 0f; }
            try
            {
                var h = HookOf(model);
                if (h == null || h.Need == null) { why = WhyNoHook; return 0f; }
                var terrain = Campaign.Current.MapSceneWrapper.GetFaceTerrainType(party.CurrentNavigationFace);
                if (terrain == TerrainType.Steppe) { why = WhySteppe; return 0f; }
                if (terrain == TerrainType.Desert) { why = WhyDesert; return 0f; }        // BK: pelna pasza na pustyni
                var w = Campaign.Current.Models.MapWeatherModel.GetWeatherEventInPosition(party.GetPosition2D);
                if (w == MapWeatherModel.WeatherEvent.Snowy || w == MapWeatherModel.WeatherEvent.Blizzard) { why = WhySnow; return 0f; }   // BK: pol paszy w sniegu
                need = h.Need(party, false);
                if (need <= 0f) { why = WhyNoAnimals; return 0f; }
                why = WhyFed;
                return need * share * realm;
            }
            catch (Exception e) { Stumble("HorseFeed", e); why = WhyOff; need = 0f; return 0f; }
        }

        /// <summary>Linia startowa (WinterBite.ApplyAll): stan kluczy E4/1.</summary>
        internal static string StartText()
        {
            var s = Settings.Current;
            if (s == null) return "zima wedlug krain (E4/1): brak ustawien";
            return "zima wedlug krain (E4/1): " + (s.WinterByRealm ? "TAK - Wyspy Letnie x0, cieple (Dorne, Qarth, Zatoka, Lys, Tyrosh, Myr, Volantis, Valyria) x"
                       + MBMath.ClampFloat(s.WinterWarmRealmFactor, 0f, 1f).ToString("0.00", Inv) + ", reszta x1 (kultura regionu z pierwszej doby sesji)" : "NIE (ta sama zima wszedzie)")
                 + "; konie jedza zima: " + (s.WinterHorsesEat ? "TAK - " + MBMath.ClampFloat(s.WinterHorseFeedShare, 0f, 1f).ToString("0.00", Inv)
                       + " paszy BK x kraina, bez doplaty na stepie, na pustyni i w sniegu (BK liczy)" : "NIE");
        }

        /// <summary>Raz na dobe (WinterBite.OnDaily, po WesterosClimate.Daily): pamiec doby od nowa; w zimie linia "Zima wedlug krain (E4/1)".</summary>
        internal static void Daily()
        {
            lock (Gate) { Fresh(); _party.Clear(); }   // region pod partia od nowa (i bez partii, ktore zniknely)
            if (!WinterBite.WinterNow()) { Interlocked.Exchange(ref _stumblesDay, 0); return; }
            try { Log.Info(Line()); }
            catch (Exception e) { Stumble("Line", e); }
            Interlocked.Exchange(ref _stumblesDay, 0);
        }

        private static string Line()
        {
            long t0 = Stopwatch.GetTimestamp();
            var s = Settings.Current;
            int day = (int)CampaignTime.Now.ToDays - 1;   // jak linie E4/0 (Measure40)
            var sb = new StringBuilder(700);
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

            // pasza koni: partie, ktore jedza (model gry), stan w chwili odczytu
            var hook = _hook;
            var fm = Campaign.Current.Models.MobilePartyFoodConsumptionModel;
            int eat = 0, fed = 0; double feed = 0, needFed = 0, men = 0, bonus = 0;
            var why = new int[8];
            foreach (var mp in MobileParty.All)
            {
                try
                {
                    if (mp == null || !mp.IsActive || fm == null || !fm.DoesPartyConsumeFood(mp)) continue;
                    eat++;
                    float realm = OfParty(mp);
                    int n = mp.MemberRoster != null ? mp.MemberRoster.TotalManCount : 0;
                    men += n; bonus += n * WinterBite.PartyFoodBonus(mp);
                    if (hook == null) { why[WhyNoHook]++; continue; }
                    int w; float need;
                    float f = HorseFeed(hook.Owner, mp, realm, out w, out need);
                    why[w]++;
                    if (f > 0f) { fed++; feed += f; needFed += need; }
                }
                catch (Exception e) { Stumble("Line(partia)", e); }
            }
            sb.Append(" | jedzenie partii zima: partii jedzacych ").Append(eat).Append(", dodatek ludzi srednio +").Append(men > 0 ? (100.0 * bonus / men).ToString("0.0", Inv) : "0").Append('%');
            sb.Append(" | pasza koni zima (").Append(s.WinterHorsesEat ? MBMath.ClampFloat(s.WinterHorseFeedShare, 0f, 1f).ToString("0.00", Inv) + " paszy BK x kraina" : "WYLACZONA").Append("): z doplata ").Append(fed)
              .Append(" partii, +").Append(feed.ToString("0.0", Inv)).Append(" jedzenia/d razem przed mnoznikami (potrzeba BK tych partii ").Append(needFed.ToString("0.0", Inv)).Append("/d)")
              .Append("; bez doplaty: step ").Append(why[WhySteppe]).Append(", pustynia (BK) ").Append(why[WhyDesert]).Append(", snieg/zamiec (BK) ").Append(why[WhySnow])
              .Append(", kraina bez zimy ").Append(why[WhyNoRealm]).Append(", bez zwierzat ").Append(why[WhyNoAnimals]).Append(", model bez paszy ").Append(why[WhyNoHook]);
            sb.Append(" | model paszy: ").Append(hook != null && hook.Need != null ? hook.Name : "BRAK (CalculateAnimalFoodNeed nie znaleziony albo jeszcze nie odczytany - konie bez doplaty)");
            sb.Append(" | potkniecia dzis ").Append(Volatile.Read(ref _stumblesDay)).Append(", od startu sesji ").Append(Volatile.Read(ref _stumbles));
            lock (Gate) { if (_stumbleAt.Count > 0) { sb.Append(" ("); bool first = true; foreach (var kv in _stumbleAt) { sb.Append(first ? "" : ", ").Append(kv.Key).Append(' ').Append(kv.Value); first = false; } sb.Append(')'); } }
            sb.Append(" | czas ").Append(((Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency).ToString("0.0", Inv)).Append(" ms.");
            return sb.ToString();
        }

        private static string Pct(double sum, int n) { return n > 0 ? (100.0 * sum / n).ToString("0.0", Inv) + "%" : "-"; }
    }
}
