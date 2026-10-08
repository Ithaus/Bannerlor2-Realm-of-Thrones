using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// CENA SPRZEDAZY SPRZETU, czesc A (decyzja Jeffa 07.10: "tak, jesli to poprawia realizm ekonomii";
    /// docs/PROJEKT-CENA-SPRZEDAZY-SPRZETU-2026-10-07.md): KARA HANDLOWA BK LICZONA RAZ (x5).
    ///
    /// BK (BKEconomyLayerInstaller.InstallPriceFactorTradePenaltyPostfix, w OnSubModuleLoad) wpina postfiks
    /// GetTradePenaltyPostfix (x5 bron/zbroja/siodla, zamek x3, Gladiator x0.8) na KAZDA podklase
    /// DefaultTradeItemPriceFactorModel, ktora sama deklaruje GetTradePenalty - i na sam Default. Podklasa
    /// BKROTPatch.Models.BKROTPriceModel wola base.GetTradePenalty (Default, juz z postfiksem BK), a potem
    /// postfiks BK mnozy jej wynik drugi raz: x25 dla kazdego sprzetu (zamek x225). Proba na prawdziwych DLL
    /// i logi 05.10 (kupno x2.000 = 0.8 x (1 + 0.06 x 25)) to potwierdzaja.
    ///
    /// Zdejmujemy TYLKO dubel: z kazdej podklasy, ktora (1) ma postfiks BK, (2) w oryginalnym IL wola "call"
    /// GetTradePenalty typu bazowego i (3) ta metoda bazowa tez ma postfiks BK. Postfiks na Default zostaje
    /// i dziala raz wewnatrz base; blogoslawienstwo ROT (-10%) w BKROT dziala dalej raz. Nic nie jest wpisane
    /// na sztywno - gdyby BK sam naprawil dubel albo BKROT przestal wolac base, nie bedzie czego zdejmowac.
    /// Na starcie kazdej sesji kontrola: kara aktywnego modelu dla zbroi bez partii i kupca / czysty wzor gry
    /// (0.2 x (0.06 + 1.5 + 0.25 x max(0, Tierf - 1))) = x5.0; inna liczba = ERROR w logu (nic sie nie wylacza).
    /// Wylacznik BkTradePenaltyOnce = false: dubel wraca (ten sam postfiks BK, ten sam wlasciciel) - jak w 127.
    /// </summary>
    internal static class BkPenaltyOnce
    {
        private const string BkInstaller = "BannerKings.Patches.BetterEconomy.BKEconomyLayerInstaller";
        private const string BkPostfixName = "GetTradePenaltyPostfix";

        private class Dub { internal MethodBase Target; internal Patch Bk; internal bool Lifted; }
        private static readonly List<Dub> _dubs = new List<Dub>();
        private static Harmony _h;
        private static bool _scanned;
        private static int _errors;

        private static bool IsBk(MethodInfo m)
        {
            return m != null && m.Name == BkPostfixName && m.DeclaringType != null && m.DeclaringType.FullName == BkInstaller;
        }

        private static Patch BkPatchOn(MethodBase m)
        {
            if (m == null) return null;
            var pi = Harmony.GetPatchInfo(m);
            if (pi == null) return null;
            foreach (var p in pi.Postfixes) if (IsBk(p.PatchMethod)) return p;
            return null;
        }

        /// <summary>Metoda GetTradePenalty typu bazowego, ktora podklasa wola przez "call" (base.GetTradePenalty) - z oryginalnego IL.</summary>
        private static MethodInfo BaseCall(Type t, MethodInfo m)
        {
            foreach (var ci in PatchProcessor.GetOriginalInstructions(m))
            {
                if (ci.opcode != OpCodes.Call) continue;
                var callee = ci.operand as MethodInfo;
                if (callee == null || callee.Name != "GetTradePenalty" || callee.DeclaringType == null || callee.DeclaringType == t) continue;
                if (!callee.DeclaringType.IsAssignableFrom(t)) continue;
                return callee;
            }
            return null;
        }

        private static void Scan()
        {
            if (_scanned) return;
            _scanned = true;
            var baseT = typeof(DefaultTradeItemPriceFactorModel);
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException rtle) { types = rtle.Types; }
                catch { continue; }
                foreach (var t in types)
                {
                    try
                    {
                        if (t == null || t.IsAbstract || t == baseT || !baseT.IsAssignableFrom(t)) continue;
                        var m = AccessTools.DeclaredMethod(t, "GetTradePenalty");
                        if (m == null) continue;
                        var bk = BkPatchOn(m);
                        if (bk == null) continue;
                        var callee = BaseCall(t, m);
                        if (callee == null || BkPatchOn(callee) == null) continue;   // nie wola base albo baza bez BK - jedna kara, nic do zdjecia
                        _dubs.Add(new Dub { Target = m, Bk = bk });
                    }
                    catch (Exception e) { if (++_errors <= 3) Log.Error("BkPenaltyOnce.Scan(" + (t != null ? t.FullName : "?") + ")", e); }
                }
            }
        }

        private static string Names()
        {
            if (_dubs.Count == 0) return "-";
            var sb = new StringBuilder();
            foreach (var d in _dubs) { if (sb.Length > 0) sb.Append(", "); sb.Append(d.Target.DeclaringType != null ? d.Target.DeclaringType.FullName : d.Target.Name); }
            return sb.ToString();
        }

        /// <summary>Przy starcie (OnBeforeInitialModuleScreenSetAsRoot, BK juz wpiety): wykrycie dubla i zdjecie wedle wartosci startowej wylacznika.</summary>
        internal static void ApplyAll(Harmony h)
        {
            try
            {
                _h = h;
                Scan();
                bool once = Settings.Current == null || Settings.Current.BkTradePenaltyOnce;
                Set(once);
                Log.Info("Kara handlowa BK: " + (_dubs.Count == 0
                    ? "zaden model nie liczy jej dwa razy (nic do zdjecia)"
                    : "dubel postfiksu BK w " + Names() + (once ? " - ZDJETY (postfiks BK zostaje na DefaultTradeItemPriceFactorModel i dziala raz, w base)"
                                                              : " - zostaje (wylacznik BkTradePenaltyOnce = false, jak w 127)"))
                         + "; kontrola mnoznika na starcie sesji.");
            }
            catch (Exception e) { Log.Error("BkPenaltyOnce.ApplyAll", e); }
        }

        /// <summary>true = postfiks BK zdjety z kazdego dubla; false = przywrocony (ten sam postfiks, wlasciciel, priorytet).</summary>
        internal static void Set(bool once)
        {
            foreach (var d in _dubs)
            {
                try
                {
                    if (once && !d.Lifted)
                    {
                        (_h ?? new Harmony("com.jeff.armoury")).Unpatch(d.Target, d.Bk.PatchMethod);
                        d.Lifted = true;
                    }
                    else if (!once && d.Lifted)
                    {
                        new Harmony(d.Bk.owner).Patch(d.Target, postfix: new HarmonyMethod(d.Bk.PatchMethod, d.Bk.priority, d.Bk.before, d.Bk.after));
                        d.Lifted = false;
                    }
                }
                catch (Exception e) { if (++_errors <= 6) Log.Error("BkPenaltyOnce.Set(" + once + ", " + d.Target.DeclaringType + ")", e); }
            }
        }

        /// <summary>Mnoznik kary BK dla sprzedazy zbroi bez partii i kupca (bez Gladiatora, zamku i blogoslawienstwa): x5 = raz, x25 = dubel.</summary>
        internal static float Measure(TradeItemPriceFactorModel model, ItemObject armour)
        {
            float p = model.GetTradePenalty(armour, null, null, true, 0f, 0f, 0f);
            float v = 0.2f * (0.06f + 1.5f + 0.25f * Math.Max(0f, armour.Tierf - 1f));
            return v > 0f ? p / v : 0f;
        }

        private static ItemObject AnyArmour()
        {
            try
            {
                foreach (var it in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
                    if (it != null && it.HasArmorComponent && !it.IsTradeGood && !it.HasHorseComponent && !it.IsCraftedByPlayer) return it;
            }
            catch { }
            return null;
        }

        /// <summary>Start sesji (po McmSettings.Apply): wylacznik z MCM i kontrola mnoznika.</summary>
        internal static void Sync()
        {
            bool once = Settings.Current.BkTradePenaltyOnce;
            Set(once);
            try
            {
                var model = Campaign.Current != null && Campaign.Current.Models != null ? Campaign.Current.Models.TradeItemPriceFactorModel : null;
                var armour = AnyArmour();
                if (model == null || armour == null) { Log.Info("Kara handlowa BK: kontrola niemozliwa (brak modelu cen albo zbroi)."); return; }
                float r = Measure(model, armour);
                bool bkHere = BkPatchOn(AccessTools.DeclaredMethod(typeof(DefaultTradeItemPriceFactorModel), "GetTradePenalty")) != null;
                string probe = " (model " + model.GetType().FullName + ", zbroja " + armour.StringId + ", bez partii i kupca)";
                string x = r.ToString("0.0", CultureInfo.InvariantCulture);
                // recenzja 07.10 (projekt: "inna liczba = Log.Error"): przy wlaczonym wylaczniku kazda liczba poza x5.0 to ERROR - takze gdy BK
                // zmieni nazwe postfiksu (wtedy go nie widzimy, a moze dalej mnozyc dwa razy); x1.0 bez postfiksu BK = BK nieobecny (Info)
                if (!once)
                    Log.Info("Kara handlowa BK: x" + x + " (wylacznik BkTradePenaltyOnce = false - jak w 127; dubel: " + Names() + ")" + probe + ".");
                else if (Math.Abs(r - 5f) < 0.05f)
                    Log.Info("Kara handlowa BK: x5.0 (raz; " + (_dubs.Count > 0 ? "zdjety dubel z " + Names() : "dubla nie bylo") + ")" + probe + ".");
                else if (!bkHere && Math.Abs(r - 1f) < 0.05f)
                    Log.Info("Kara handlowa BK: x1.0 - postfiksu BK nie ma na DefaultTradeItemPriceFactorModel (BK nieobecny)" + probe + ".");
                else
                    Log.Error("BkPenaltyOnce: kara handlowa BK x" + x + " zamiast x5.0" + probe + "; dubel: " + Names()
                              + (bkHere ? "" : "; postfiksu BK nie ma na DefaultTradeItemPriceFactorModel (BK zmieniony?)") + " - sprawdzic latki GetTradePenalty", null);
            }
            catch (Exception e) { if (++_errors <= 6) Log.Error("BkPenaltyOnce.Sync", e); }
        }
    }

    /// <summary>
    /// CENA SPRZEDAZY SPRZETU, czesc B (decyzje Jeffa 07.10: (2) "tak" - sufit 1/10 ceny nowej sztuki; (3) "tak" - handel
    /// odnowionym sprzetem miedzy miastami zostaje). Cena, jaka kupiec placi za bron, zbroje i konie, idzie za stanem sztuki:
    ///  B1 podloga MinSellPercentOfValue od WARTOSCI ZE STANEM (EquipmentElement.ItemValue) - SupplyDemand, ScrapFloor, MarketGlut;
    ///  B2 gdy lancuch gry dal swoje minimum 1 zl - prawdziwy ulamek (wartosc ze stanem x mnoznik ceny aktywnego modelu), potem polka;
    ///  B3 sufit: kupiec w miescie/zamku nie da za bron, zbroje, siodlo ani amunicje wiecej niz SellCapPercentOfNewAsk (10%) ceny, jaka TA
    ///     polka zada za NOWA sztuke tej samej jakosci, ani wiecej niz sam bierze za jej wrak - petla "kup wrak, odnow, sprzedaj" w jednym
    ///     miescie nie zarabia (konie i zwierzeta juczne bez sufitu);
    ///  B4 hurt miedzy miastami (SupplyDemand.DailyTrade) od wartosci ze stanem.
    /// Wylacznik SellPriceByCondition = false: jak w 127 (podloga i hurt od czystej wartosci, bez ulamka, bez sufitu).
    ///
    /// KSIEGA SKUPU (tylko log): kazda sprzedaz gracza do handel.log (PlayerInventoryExchangeEvent) i linia dnia "Skup sprzetu"
    /// wedlug sprzedajacego: gracz (ekran handlu), lordowie AI i inne partie (SellItemsAction), sakiewki ludzi (MenPurse), notable
    /// (RecruitKit) - sztuk, wartosc, wartosc ze stanem, zaplacono, ile na podlodze, ile na suficie. Nic tu nie zmienia ceny ani zlota.
    /// </summary>
    internal static class SellByCondition
    {
        internal static bool On { get { var s = Settings.Current; return s != null && s.SellPriceByCondition; } }

        /// <summary>B1: od czego liczy sie podloga i stawka skupu - wartosc ze stanem (wylacznik: czysta wartosc, jak w 127).</summary>
        internal static int FloorWorth(EquipmentElement el)
        {
            if (el.Item == null) return 0;
            return On ? el.ItemValue : el.Item.Value;
        }

        /// <summary>
        /// B2: prawdziwy ulamek ceny gry (najwyzej 1), gdy gra dala swoje minimum 1 zl: wartosc ze stanem x GetBasePriceFactor / (1 + GetTradePenalty)
        /// aktywnego modelu z tymi samymi argumentami co GetPrice (DefaultTradeItemPriceFactorModel.GetPriceFactor dla sprzedazy).
        /// </summary>
        internal static float Fraction(TradeItemPriceFactorModel model, EquipmentElement el, MobileParty client, PartyBase merchant, float inStore, float supply, float demand)
        {
            try
            {
                var item = el.Item;
                if (model == null || item == null) return 1f;
                float bpf = model.GetBasePriceFactor(item.GetItemCategory(), inStore, supply, demand, true, item.Value);
                float tp = model.GetTradePenalty(item, client, merchant, true, inStore, supply, demand);
                float pf = bpf * 1f / (1f + tp);
                float v = (float)el.ItemValue * pf;
                if (float.IsNaN(v) || float.IsInfinity(v) || v < 0f) return 1f;
                return Math.Min(1f, v);
            }
            catch (Exception e) { Stumble("Fraction", e); return 1f; }
        }

        /// <summary>
        /// B3: sufit skupu = SellCapPercentOfNewAsk % ceny, jaka ta polka (mnoznik f, marza kupca) zada za NOWA sztuke tej rzeczy tej samej
        /// jakosci (max(Value, ItemValue): lepsze niz zwykle licza od swojej wartosci) - i nie wiecej niz ta polka zada za jej WRAK (sztuke
        /// warta tyle procent, zaokraglona jak EquipmentElement.ItemValue). Proba: sam 1/10 ceny nowej przebijal cene wraku o zaokraglenie
        /// (miecz 104: wrak wart round(10.4) = 10, przy polce x6 kupno 66, a 1/10 ceny nowej 69) i petla "kup wrak, odnow, sprzedaj" w jednym
        /// miescie dawala +1. 0 = bez sufitu. Tylko gdy cena kupna idzie z wartosci (RetailFromWorth i ceny historyczne).
        /// Bron, zbroja, siodla I AMUNICJA (recenzja 07.10): strzaly i belty nie maja naprawy w kuzni, ale CleanseAmmo (start sesji i koniec
        /// kazdej bitwy gracza) zdejmuje z nich za darmo kazdy ujemny stan - bez sufitu proba dala petle "kup zuzyte strzaly, bitwa, sprzedaj
        /// czyste w tym samym miescie" z zyskiem w 37 z 336 przypadkow (Handel 300, do +80 zl na kolczanie wartym 60; w 127: 0).
        /// Bez sufitu tylko zywy inwentarz i towar (IsBeast / IsGoods): kon idzie u gry za ok. 43% wartosci (BK go nie mnozy), sufit 11%
        /// scialby go kilkukrotnie - tego Jeff nie postanowil.
        /// </summary>
        internal static int Cap(EquipmentElement el, float f)
        {
            var s = Settings.Current;
            if (s == null || el.Item == null || s.SellCapPercentOfNewAsk <= 0 || !HistoricalPrices.On || !s.RetailFromWorth) return 0;
            if (ArmouryBehavior.IsBeast(el.Item) || ArmouryBehavior.IsGoods(el.Item)) return 0;
            int newWorth = Math.Max(el.Item.Value, el.ItemValue);
            float share = s.SellCapPercentOfNewAsk / 100f;
            float markup = 1f + Math.Max(0f, s.RetailMarkupPercent) / 100f;
            int tenth = (int)Math.Round(newWorth * markup * f * share);
            int wreck = (int)Math.Round(Math.Max(1, (int)TaleWorlds.Library.MathF.Round((float)newWorth * share)) * markup * f);   // jak cena kupna w PricePostfix
            return Math.Max(1, Math.Min(tenth, wreck));
        }

        /// <summary>Dopisek do linii PodazPopyt (sprzedaz): stan, wartosc ze stanem, granice podloga..sufit i co wiaze.</summary>
        internal static string Bounds(EquipmentElement el, int floor, int cap, int bound)
        {
            float pm = el.ItemModifier != null ? el.ItemModifier.PriceMultiplier : 1f;
            return ", stan x" + pm.ToString("0.00", CultureInfo.InvariantCulture) + (el.ItemModifier != null ? " " + el.ItemModifier.StringId : "")
                   + ", wartosc ze stanem " + el.ItemValue + ", granice " + (floor > 0 ? floor.ToString() : "-") + ".." + (cap > 0 ? cap.ToString() : "-")
                   + ", wiaze: " + (bound == 1 ? "podloga" : bound == 2 ? "sufit" : "-");
        }

        // ------------------------------------------------------------ ksiega skupu (tylko log)
        internal const int Player = 1, Lords = 2, OtherParties = 3, Men = 4, Notable = 5;
        private static readonly string[] Who = { "-", "gracz", "lordowie AI (SellItemsAction)", "inne partie (SellItemsAction)", "sakiewki ludzi", "notable (rzeczy ochotnikow)" };
        private class Tally { internal int N, AtFloor, AtCap; internal long Value, Worth, Paid; }
        private static readonly Tally[] _day = { new Tally(), new Tally(), new Tally(), new Tally(), new Tally(), new Tally() };
        // gracz: ile sztuk danej rzeczy poszlo w biezacym ekranie handlu na podlodze [0] / na suficie [1] (rozliczane przy zamknieciu ekranu)
        private static readonly Dictionary<EquipmentElement, int[]> _screen = new Dictionary<EquipmentElement, int[]>();
        [ThreadStatic] private static int _ctx;                      // kto teraz sprzedaje (kontekst z TransferItem / SellItemsAction)
        [ThreadStatic] private static ItemObject _lastItem;
        [ThreadStatic] private static ItemModifier _lastMod;
        [ThreadStatic] private static int _lastPrice, _lastBound;
        private static int _errors, _dayErrors;

        internal static void Reset()
        {
            foreach (var t in _day) { t.N = t.AtFloor = t.AtCap = 0; t.Value = t.Worth = t.Paid = 0; }
            _screen.Clear(); _dayErrors = 0;
        }

        private static void Stumble(string where, Exception e)
        {
            _dayErrors++;
            if (++_errors <= 5) Log.Error("SellByCondition." + where, e);
        }

        private static bool AtFloorByEquality(EquipmentElement el, int price)
        {
            var s = Settings.Current;
            if (s == null || s.MinSellPercentOfValue <= 0 || el.Item == null || el.Item.Value <= 0) return false;
            int floor = Math.Max(1, (int)((float)FloorWorth(el) * s.MinSellPercentOfValue / 100f));
            return price == floor;
        }

        private static void Add(int who, EquipmentElement el, int n, long paid, int bound)
        {
            var t = _day[who];
            t.N += n; t.Value += (long)el.Item.Value * n; t.Worth += (long)el.ItemValue * n; t.Paid += paid;
            if (bound == 1) t.AtFloor += n; else if (bound == 2) t.AtCap += n;
        }

        /// <summary>
        /// Wynik ceny SPRZEDAZY sprzetu po wszystkich naszych warstwach (wolane z SupplyDemand.PricePostfix). bound: 1 = podloga podniosla
        /// cene, 2 = sufit ja obnizyl, 0 = nic, -1 = nie wiadomo (poza prawem podazy - liczymy "na podlodze", gdy cena = podloga ScrapFloor).
        /// </summary>
        internal static void Seen(EquipmentElement el, bool selling, int price, int bound)
        {
            if (!selling || el.Item == null) return;
            try
            {
                if (!SupplyDemand.Equipmentish(el.Item)) return;
                _lastItem = el.Item; _lastMod = el.ItemModifier; _lastPrice = price; _lastBound = bound;
                int who = _ctx;
                if (who == 0) return;
                if (bound < 0) bound = AtFloorByEquality(el, price) ? 1 : 0;
                if (who == Player)
                {
                    int[] c;
                    if (!_screen.TryGetValue(el, out c)) _screen[el] = c = new int[2];
                    if (bound == 1) c[0]++; else if (bound == 2) c[1]++;
                }
                else if (who == Lords || who == OtherParties) Add(who, el, 1, price, bound);   // SellItemsAction wycenia kazda sztuke osobno
            }
            catch (Exception e) { Stumble("Seen", e); }
        }

        /// <summary>Sprzedaz kupcowi poza ekranem handlu i SellItemsAction (sakiewki ludzi, notable): n sztuk po unit.</summary>
        internal static void NoteSale(int who, EquipmentElement el, int n, int unit)
        {
            try
            {
                if (n <= 0 || el.Item == null || who <= 0 || who >= _day.Length || !SupplyDemand.Equipmentish(el.Item)) return;
                int bound = _lastItem == el.Item && _lastMod == el.ItemModifier && _lastPrice == unit ? _lastBound : -1;
                if (bound < 0) bound = AtFloorByEquality(el, unit) ? 1 : 0;
                Add(who, el, n, (long)unit * n, bound);
            }
            catch (Exception e) { Stumble("NoteSale", e); }
        }

        /// <summary>Zamkniecie ekranu handlu gracza (PlayerInventoryExchangeEvent): kazda sprzedana rzecz do handel.log i do linii dnia.</summary>
        internal static void OnPlayerExchange(List<(ItemRosterElement, int)> purchased, List<(ItemRosterElement, int)> sold, bool isTrading)
        {
            try
            {
                if (!isTrading || QuartermasterEscrow.Active || sold == null) return;
                string where = "poza osada";
                try { var st = Settlement.CurrentSettlement ?? (MobileParty.MainParty != null ? MobileParty.MainParty.CurrentSettlement : null); if (st != null) where = st.Name.ToString(); } catch { }
                foreach (var x in sold)
                {
                    var el = x.Item1.EquipmentElement;
                    int n = x.Item1.Amount, sum = x.Item2;
                    if (el.Item == null || n <= 0 || !SupplyDemand.Equipmentish(el.Item)) continue;
                    int[] c; _screen.TryGetValue(el, out c);
                    int fl = c != null ? Math.Min(n, c[0]) : 0, cp = c != null ? Math.Min(n, c[1]) : 0;
                    var t = _day[Player];
                    t.N += n; t.Value += (long)el.Item.Value * n; t.Worth += (long)el.ItemValue * n; t.Paid += sum; t.AtFloor += fl; t.AtCap += cp;
                    float pm = el.ItemModifier != null ? el.ItemModifier.PriceMultiplier : 1f;
                    Log.Info("PodazPopyt: SPRZEDAZ GRACZA w " + where + ": " + el.Item.Name + " (" + el.Item.StringId + ") x" + n
                             + ", stan " + (el.ItemModifier != null ? el.ItemModifier.StringId : "czysta") + " x" + pm.ToString("0.00", CultureInfo.InvariantCulture)
                             + ", wartosc " + el.Item.Value + ", ze stanem " + el.ItemValue
                             + ", cena " + (sum / n) + " za szt. (razem " + sum + ") = " + Pct(sum, (long)el.Item.Value * n) + " wartosci, " + Pct(sum, (long)el.ItemValue * n) + " ze stanem"
                             + "; na podlodze " + fl + ", na suficie " + cp + ".");
                }
            }
            catch (Exception e) { Stumble("OnPlayerExchange", e); }
            finally { _screen.Clear(); }
        }

        private static string Pct(long part, long whole)
        {
            if (whole <= 0) return "-";
            return (part * 100.0 / whole).ToString("0.0", CultureInfo.InvariantCulture) + "%";
        }

        /// <summary>Linia dnia "Skup sprzetu" (tylko log).</summary>
        internal static void Daily()
        {
            var sb = new StringBuilder();
            for (int w = 1; w < _day.Length; w++)
            {
                var t = _day[w];
                if (t.N <= 0) continue;
                if (sb.Length > 0) sb.Append("; ");
                sb.Append(Who[w]).Append(' ').Append(t.N).Append(" szt. (wartosc ").Append(t.Value).Append(", ze stanem ").Append(t.Worth)
                  .Append(", zaplacono ").Append(t.Paid).Append(" = ").Append(Pct(t.Paid, t.Value)).Append(" wartosci, ").Append(Pct(t.Paid, t.Worth))
                  .Append(" ze stanem; na podlodze ").Append(t.AtFloor).Append(", na suficie ").Append(t.AtCap).Append(')');
            }
            if (sb.Length > 0 || _dayErrors > 0)
            {
                var s = Settings.Current;
                Log.Info("Skup sprzetu (doba): " + (sb.Length > 0 ? sb.ToString() : "nic") + (_dayErrors > 0 ? "; potkniecia " + _dayErrors : "")
                         + " [BkTradePenaltyOnce " + s.BkTradePenaltyOnce + ", SellPriceByCondition " + s.SellPriceByCondition + ", sufit " + s.SellCapPercentOfNewAsk
                         + "% ceny nowej, podloga " + s.MinSellPercentOfValue + "%].");
            }
            foreach (var t in _day) { t.N = t.AtFloor = t.AtCap = 0; t.Value = t.Worth = t.Paid = 0; }
            _dayErrors = 0;
        }

        /// <summary>Start sesji: ksiega od zera, wylacznik kary BK z MCM i kontrola mnoznika.</summary>
        internal static void OnSessionLaunched()
        {
            Reset();
            BkPenaltyOnce.Sync();
        }

        // ------------------------------------------------------------ kontekst sprzedajacego (tylko log; __state = poprzedni kontekst + 1)
        public static void TransferPrefix(InventoryLogic __instance, ref TransferCommand transferCommand, out int __state)
        {
            __state = _ctx + 1;
            try
            {
                if (__instance != null && __instance.IsTrading && !QuartermasterEscrow.Active
                    && transferCommand.ToSide == InventoryLogic.InventorySide.OtherInventory
                    && (transferCommand.FromSide == InventoryLogic.InventorySide.PlayerInventory || InventoryLogic.IsEquipmentSide(transferCommand.FromSide)))
                    _ctx = Player;
            }
            catch { }
        }

        public static void SellActionPrefix(PartyBase __0, PartyBase __1, out int __state)
        {
            __state = _ctx + 1;
            try
            {
                // sprzedajacy partia (nie gracz), kupuje osada; cene kazdej sztuki liczy w srodku Town.GetItemPrice
                if (__1 != null && __1.IsSettlement && __0 != null && __0.MobileParty != null && !__0.MobileParty.IsMainParty)
                    _ctx = __0.MobileParty.IsLordParty ? Lords : OtherParties;
            }
            catch { }
        }

        public static Exception ContextFinalizer(Exception __exception, int __state)
        {
            if (__state > 0) _ctx = __state - 1;
            return __exception;
        }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var fin = new HarmonyMethod(typeof(SellByCondition), "ContextFinalizer");
                var tr = AccessTools.Method(typeof(InventoryLogic), "TransferItem");
                if (tr != null) h.Patch(tr, prefix: new HarmonyMethod(typeof(SellByCondition), "TransferPrefix"), finalizer: fin);
                var sa = AccessTools.Method(typeof(SellItemsAction), "ApplyInternal");
                if (sa != null) h.Patch(sa, prefix: new HarmonyMethod(typeof(SellByCondition), "SellActionPrefix"), finalizer: fin);
                Log.Info("Skup sprzetu: ksiega sprzedazy sprzetu kupcom (tylko log) - ekran handlu gracza " + (tr != null ? "wpiety" : "BRAK InventoryLogic.TransferItem")
                         + ", SellItemsAction " + (sa != null ? "wpiety" : "BRAK ApplyInternal") + "; linia dnia 'Skup sprzetu', kazda sprzedaz gracza w handel.log.");
            }
            catch (Exception e) { Log.Error("SellByCondition.ApplyAll", e); }
        }
    }
}
