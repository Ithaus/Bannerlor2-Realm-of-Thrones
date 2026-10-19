using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// f28-wyceny (A) - ILOSCI W ZADANIACH OD WARTOSCI SPRZED PRZELICZENIA (audyt docs/AUDYT-WYCENY-2026-10-19.md pkt 1-10; Jeff 19.10: "TAK NAPRAW TERAZ").
    ///
    /// Gra liczy ilosc towaru w kilku zadaniach jako STALA w starej monecie podzielona przez wartosc (albo cene) przedmiotu. Po cenach historycznych
    /// towar jest kilka-kilkadziesiat razy tanszy, wiec zadanie zada setek sztuk (surowka 750 / 2 d = 375 zamiast 18, Sztuka handlu: 833 worki zboza).
    /// Regula - ta sama co XP kowalstwa (XpPostfix), badania czesci (f27) i okna nagrod (FixedSums38): kwota nie-cenowa w starej monecie porownywana
    /// z wartoscia SPRZED przeliczenia (HistoricalPrices.Orig). Transpiler podmienia TYLKO odczyt wartosci stojacy przy stalej (kotwica ldc - jak
    /// BookTranspiler), wiec nagroda, koszt zastepczy i cel w denarach licza sie dalej z dzisiejszej ceny (w nowej monecie; stala czesc nagrody
    /// skaluje QuestPay8). Towar, ktory gracz dostaje (Sztuka handlu, mieso za zwierzeta), wychodzi z tej samej poprawionej liczby - nic z niczego.
    /// Miejsca (IL 1.4.8 sprawdzony, kotwica -> podmiana):
    ///  1 GangLeaderNeedsWeaponsIssue.get_RequestedWeaponAmount: ldc.i4 20000, ldfld _averagePriceForItem -> OldAvg (srednia siekier na polkach
    ///    miast liczona petla gry z wartoscia sprzed przeliczenia; RewardGold dalej czyta pole - nowa moneta);
    ///  2 GangLeaderNeedsWeaponsIssueQuest.OnSettlementLeft: ldc.i4 1500, straz zatrzymuje bron do sumy 1500 - Item.Value -> Old;
    ///  3 VillageNeedsCraftingMaterialsIssue.get__numberOfRequestedItem: ldc.r4 750; 4 LandLordTheArtOfTheTradeIssue.get_SelectedItemObjectCount:
    ///    ldc.r4 5000; 5 ArtisanOverpricedGoodsIssue.CalculateTradeGoodsAmountAndReward: ldc.r4 10000; 6 VillageNeedsToolsIssue..ctor: ldc.r4 2500
    ///    (tylko pierwszy odczyt - nagroda i wymiana dalej z Value); 7 HeadmanNeedsToDeliverAHerdIssue.get_AnimalCountToDeliver: ldc.r4 5000;
    ///  8 HeadmanVillageNeedsDraughtAnimalsIssue..ctor: ldc.r4 5000, get_OfferedMeatAmount: ldc.i4 300 (oba odczyty - zwierze i mieso);
    ///  9 GangLeaderNeedsToOffloadStolenGoodsIssue.get_StolenTradeGoodAmount: ldc.r4 10000, SettlementComponent.GetItemPrice -> OldPrice
    ///    (cena x Orig / Value); 10 SmugglersIssueQuest.GiveGoodsToParty: ldc.r4 3000 (towar dla przemytnikow z niczego - mniej sztuk).
    /// Latki W KAMPANII (CLAUDE.md 7: konstruktory statyczne cudzych klas), raz na proces, gdy klucz wlaczony; bez klucza nic nie wpiete (sciezka
    /// WGRANIA 87). Klucz zdjety w trakcie gry: pomocniki zwracaja wartosc gry. Ilosc trzyma obiekt sprawy/zadania od chwili utworzenia (pola
    /// zapisu), wiec sprawy z konstruktorem (narzedzia, zwierzeta pociagowe, rzemieslnik) utworzone przed wgraniem zostaja jak byly.
    /// Log: linia startu "w N/M metodach"; przy przyjeciu zadania jedna linia "Ilosci zadan (f28): ... gra X -> Y".
    /// </summary>
    internal static class QuestQty28
    {
        internal static bool On { get { var s = Settings.Current; return s != null && s.HistQuestQtyOldCoin && HistoricalPrices.On && HistoricalPrices.Applied; } }

        private enum Swap { Value, Price, AvgField }

        private sealed class Site
        {
            public int Id; public string Outer, Inner, Method, Label; public Type[] Args; public double Anchor; public int Want; public Swap Kind; public string Field;
            public bool Note; public MethodBase M; public int Swaps = -1;
        }

        private sealed class Rec { public string Text; }
        private sealed class Ratio { public double K; }

        private const string NS = "TaleWorlds.CampaignSystem.Issues.";
        private static readonly Site[] Sites =
        {
            new Site { Id = 1, Outer = "GangLeaderNeedsWeaponsIssueQuestBehavior", Inner = "GangLeaderNeedsWeaponsIssue", Method = "get_RequestedWeaponAmount", Label = "bron gangu (ilosc siekier)", Anchor = 20000, Want = 1, Kind = Swap.AvgField, Field = "_averagePriceForItem", Note = true },
            new Site { Id = 2, Outer = "GangLeaderNeedsWeaponsIssueQuestBehavior", Inner = "GangLeaderNeedsWeaponsIssueQuest", Method = "OnSettlementLeft", Label = "bron gangu (straz przy bramie 1500)", Anchor = 1500, Want = 1 },
            new Site { Id = 3, Outer = "VillageNeedsCraftingMaterialsIssueBehavior", Inner = "VillageNeedsCraftingMaterialsIssue", Method = "get__numberOfRequestedItem", Label = "surowiec kuzni wsi", Anchor = 750, Want = 1, Note = true },
            new Site { Id = 4, Outer = "LandLordTheArtOfTheTradeIssueBehavior", Inner = "LandLordTheArtOfTheTradeIssue", Method = "get_SelectedItemObjectCount", Label = "sztuka handlu", Anchor = 5000, Want = 1, Note = true },
            new Site { Id = 5, Outer = "ArtisanOverpricedGoodsIssueBehavior", Inner = "ArtisanOverpricedGoodsIssue", Method = "CalculateTradeGoodsAmountAndReward", Label = "rzemieslnik i drogi towar", Anchor = 10000, Want = 1, Note = true },
            new Site { Id = 6, Outer = "VillageNeedsToolsIssueBehavior", Inner = "VillageNeedsToolsIssue", Method = ".ctor", Args = new[] { typeof(Hero), typeof(ItemObject) }, Label = "narzedzia dla wsi", Anchor = 2500, Want = 1, Note = true },
            new Site { Id = 7, Outer = "HeadmanNeedsToDeliverAHerdIssueBehavior", Inner = "HeadmanNeedsToDeliverAHerdIssue", Method = "get_AnimalCountToDeliver", Label = "stado do zagnania", Anchor = 5000, Want = 1, Note = true },
            new Site { Id = 8, Outer = "HeadmanVillageNeedsDraughtAnimalsIssueBehavior", Inner = "HeadmanVillageNeedsDraughtAnimalsIssue", Method = ".ctor", Args = new[] { typeof(Hero) }, Label = "zwierzeta pociagowe", Anchor = 5000, Want = 1, Note = true },
            new Site { Id = 9, Outer = "HeadmanVillageNeedsDraughtAnimalsIssueBehavior", Inner = "HeadmanVillageNeedsDraughtAnimalsIssue", Method = "get_OfferedMeatAmount", Label = "mieso za zwierzeta pociagowe", Anchor = 300, Want = 2 },
            new Site { Id = 10, Outer = "GangLeaderNeedsToOffloadStolenGoodsIssueBehavior", Inner = "GangLeaderNeedsToOffloadStolenGoodsIssue", Method = "get_StolenTradeGoodAmount", Label = "skradziony towar gangu", Anchor = 10000, Want = 1, Kind = Swap.Price, Note = true },
            new Site { Id = 11, Outer = "SmugglersIssueBehavior", Inner = "SmugglersIssueQuest", Method = "GiveGoodsToParty", Label = "towar przemytnikow", Anchor = 3000, Want = 1, Note = true },
        };

        private static readonly Dictionary<IntPtr, Site> _byHandle = new Dictionary<IntPtr, Site>();
        private static readonly ConditionalWeakTable<object, Rec> _notes = new ConditionalWeakTable<object, Rec>();
        private static readonly ConditionalWeakTable<object, Ratio> _ratio = new ConditionalWeakTable<object, Ratio>();
        private static Harmony _h;
        private static bool _tried;
        private static int _hooked, _stumbles, _logged;
        private static string _status = "";
        private static MethodInfo _getValue, _old, _oldPrice, _oldAvg, _pDiff;
        private static FieldInfo _fAvg;       // GangLeaderNeedsWeaponsIssue._averagePriceForItem
        private static PropertyInfo _pClass;  // GangLeaderNeedsWeaponsIssue.RequestedWeaponClass

        // ostatnia podmiana na tym watku (gra -> sprzed przeliczenia) - tylko do linii logu w postfiksie tej samej metody
        [ThreadStatic] private static int _lGame, _lOld;
        [ThreadStatic] private static ItemObject _lItem;

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_stumbles <= 5) Log.Error("QuestQty28." + where, e);
        }

        // ------------------------------------------------------------ pomocniki wolane z IL gry (te same stosy co podmienione instrukcje)

        /// <summary>Zamiast ItemObject.get_Value przy stalej: wartosc sprzed przeliczenia (OrigUnit - Orig x CountScale: ladunek rudy/drewna = 10 starych
        /// sztuk; nieprzeliczone - Value). Null - ten sam wyjatek co gra.</summary>
        public static int Old(ItemObject it)
        {
            int v = it.Value;
            if (!On) return v;
            try
            {
                int o = HistoricalPrices.OrigUnit(it);   // poprawka: ladunek 100 kg liczy sie jak 10 starych sztuk 10 kg (inaczej 10 x towaru)
                if (o <= 0) return v;
                _lItem = it; _lGame = v; _lOld = o;
                return o;
            }
            catch (Exception e) { Stumble("Old", e); return v; }
        }

        /// <summary>Zamiast SettlementComponent.GetItemPrice przy stalej: cena w starej monecie = cena x OrigUnit / Value.</summary>
        public static int OldPrice(SettlementComponent sc, ItemObject item, MobileParty party, bool isSelling)
        {
            int p = sc.GetItemPrice(item, party, isSelling);
            if (!On || item == null) return p;
            try
            {
                int v = item.Value, o = HistoricalPrices.OrigUnit(item), r = p;
                if (v > 0 && o > 0 && o != v) r = Math.Max(1, (int)Math.Round(p * (double)o / v));
                _lItem = item; _lGame = p; _lOld = r;
                return r;
            }
            catch (Exception e) { Stumble("OldPrice", e); return p; }
        }

        /// <summary>Zamiast ldfld _averagePriceForItem przy stalej 20000: srednia cena siekier w starej monecie (pole x stosunek z polek miast).</summary>
        public static int OldAvg(object issue)
        {
            int avg = (int)_fAvg.GetValue(issue);
            if (!On || avg <= 0) return avg;
            try
            {
                Ratio r;
                if (!_ratio.TryGetValue(issue, out r)) { r = new Ratio { K = AxeRatio(issue) }; _ratio.Add(issue, r); }   // raz na sprawe - ilosc stala miedzy rozmowa a przyjeciem
                int o = Math.Max(1, (int)Math.Round(avg * r.K));
                _lItem = null; _lGame = avg; _lOld = o;
                return o;
            }
            catch (Exception e) { Stumble("OldAvg", e); return avg; }
        }

        /// <summary>Petla gry (CalculateAveragePriceForWeaponClass: polki miast, ItemValue po elemencie) - stosunek sumy wartosci sprzed przeliczenia do dzisiejszej.</summary>
        private static double AxeRatio(object issue)
        {
            WeaponClass wc = WeaponClass.OneHandedAxe;
            try { if (_pClass != null) wc = (WeaponClass)_pClass.GetValue(issue, null); } catch { }
            double sv = 0, sw = 0;
            foreach (Settlement st in Settlement.All)
            {
                if (st == null || !st.IsTown || st.ItemRoster == null) continue;
                var ro = st.ItemRoster;
                for (int i = 0; i < ro.Count; i++)
                {
                    var el = ro.GetElementCopyAtIndex(i);
                    var it = el.EquipmentElement.Item;
                    var w = it != null ? it.WeaponComponent : null;
                    if (w == null || w.PrimaryWeapon == null || w.PrimaryWeapon.WeaponClass != wc) continue;
                    int iv = el.EquipmentElement.ItemValue, v = it.Value, o = HistoricalPrices.Orig(it);
                    sv += iv; sw += v > 0 && o > 0 ? iv * (double)o / v : iv;
                }
            }
            if (sv > 0) return sw / sv;
            // pusto na polkach (gra i tak nie stworzy wtedy sprawy) - srednia z definicji przedmiotow tej klasy
            double sum = 0; int n = 0;
            foreach (var it in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
            {
                var w = it != null ? it.WeaponComponent : null;
                if (w == null || it.NotMerchandise || w.PrimaryWeapon == null || w.PrimaryWeapon.WeaponClass != wc || it.Value <= 0) continue;
                int o = HistoricalPrices.Orig(it);
                if (o > 0) { sum += o / (double)it.Value; n++; }
            }
            return n > 0 ? sum / n : 1.0;
        }

        private static object Get(object o, string name)
        {
            var m = AccessTools.Field(o.GetType(), name);
            if (m != null) return m.GetValue(o);
            var p = AccessTools.Property(o.GetType(), name);
            return p != null ? p.GetValue(o, null) : null;
        }

        // ------------------------------------------------------------ transpiler (kotwica: pierwsza stala K w metodzie, potem Want podmian)

        private static bool IsAnchor(CodeInstruction ci, double k)
        {
            if (ci.opcode == OpCodes.Ldc_R4 && ci.operand is float) return (float)ci.operand == (float)k;
            if (ci.opcode == OpCodes.Ldc_I4 && ci.operand is int) return (int)ci.operand == (int)k;
            return false;
        }

        public static IEnumerable<CodeInstruction> Il(IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            var list = new List<CodeInstruction>(instructions);
            Site site = null;
            try { if (original != null) _byHandle.TryGetValue(original.MethodHandle.Value, out site); } catch { }
            if (site == null) return list;
            bool armed = false, used = false; int left = 0, n = 0;
            foreach (var ci in list)
            {
                if (!armed && !used && IsAnchor(ci, site.Anchor)) { armed = true; used = true; left = site.Want; continue; }
                if (!armed || left <= 0) continue;
                bool hit = false;
                if (site.Kind == Swap.Value)
                {
                    if ((ci.opcode == OpCodes.Callvirt || ci.opcode == OpCodes.Call) && Equals(ci.operand, _getValue)) { ci.opcode = OpCodes.Call; ci.operand = _old; hit = true; }
                }
                else if (site.Kind == Swap.Price)
                {
                    var mi = ci.operand as MethodInfo;
                    if ((ci.opcode == OpCodes.Callvirt || ci.opcode == OpCodes.Call) && mi != null && mi.Name == "GetItemPrice" && typeof(SettlementComponent).IsAssignableFrom(mi.DeclaringType))
                    {
                        var ps = mi.GetParameters();
                        if (ps.Length == 3 && ps[0].ParameterType == typeof(ItemObject) && ps[1].ParameterType == typeof(MobileParty) && ps[2].ParameterType == typeof(bool))
                        { ci.opcode = OpCodes.Call; ci.operand = _oldPrice; hit = true; }
                    }
                }
                else
                {
                    var fi = ci.operand as FieldInfo;
                    if (ci.opcode == OpCodes.Ldfld && fi != null && fi.Name == site.Field) { ci.opcode = OpCodes.Call; ci.operand = _oldAvg; hit = true; }
                }
                if (hit) { n++; if (--left <= 0) armed = false; }
            }
            site.Swaps = n;   // przypisanie, nie dodawanie - Harmony puszcza transpilery od nowa przy kazdej kolejnej latce na te metode
            return list;
        }

        // ------------------------------------------------------------ linia logu: ilosc gry (ten sam wzor z wartoscia dzisiejsza) i nasza

        /// <summary>Prefiks miejsc z linia logu: zeruje zapis ostatniej podmiany (bez podmiany w tym wywolaniu - bez linii).</summary>
        public static void Pre() { _lGame = 0; _lOld = 0; _lItem = null; }

        public static void IntPost(MethodBase __originalMethod, object __instance, int __result) { Note(__originalMethod, __instance, __result); }

        public static void VoidPost(MethodBase __originalMethod, object __instance) { Note(__originalMethod, __instance, int.MinValue); }

        private static void Note(MethodBase m, object inst, int result)
        {
            if (inst == null || !On) return;
            try
            {
                Rec dummy;
                if (_notes.TryGetValue(inst, out dummy)) return;
                Site site;
                if (m == null || !_byHandle.TryGetValue(m.MethodHandle.Value, out site) || !site.Note) return;
                int g = _lGame, o = _lOld; var item = _lItem;
                if (g <= 0) return;
                float d = 0f;
                if (inst is IssueBase && _pDiff != null) d = (float)_pDiff.Invoke(inst, null);
                int game, now;
                string what = item != null ? item.StringId : "?", extra = "";
                switch (site.Id)
                {
                    case 1:
                        {
                            float f = (d >= 0.1f && d < 0.3f) ? 0.1f : ((!(d >= 0.3f) || !(d < 0.6f)) ? 0.3f : 0.2f);
                            game = (int)((float)(20000 / g) * f); now = result; what = "siekiery 1H (srednia cena gry " + g + " d, sprzed przeliczenia " + o + ")"; break;
                        }
                    case 3: game = TaleWorlds.Library.MathF.Round((float)(int)(750f / (float)g) * d); now = result; break;
                    case 4: game = Math.Max(1, TaleWorlds.Library.MathF.Round((float)(int)(5000f / (float)g) * d)); now = result; break;
                    case 5: game = Math.Max((int)(10000f / (float)g * d), 1); now = Convert.ToInt32(Get(inst, "RequestedTradeGoodAmount")); break;
                    case 6: game = TaleWorlds.Library.MathF.Round((float)(int)(2500f / (float)g) * d); now = Convert.ToInt32(Get(inst, "_numberOfRequestedItem")); break;
                    case 7: game = (int)TaleWorlds.Library.MathF.Clamp(TaleWorlds.Library.MathF.Round(5000f * d / (float)g), 10f, 75f); now = result; break;
                    case 8:
                        {
                            game = TaleWorlds.Library.MathF.Round((float)(int)(5000f / (float)g) * d); now = Convert.ToInt32(Get(inst, "_requestedAnimalAmount"));
                            if (Convert.ToBoolean(Get(inst, "_isQuestWithMeatOffer")))
                            {
                                var meat = MBObjectManager.Instance.GetObject<ItemObject>("meat");
                                if (meat != null && meat.Value > 0)
                                {
                                    int meatGame = (300 + g * game) / meat.Value * 2;
                                    int meatNow = Convert.ToInt32(Get(inst, "OfferedMeatAmount"));   // getter po naszym transpilerze
                                    extra = "; mieso w zamian: gra " + meatGame + " -> " + meatNow;
                                }
                            }
                            break;
                        }
                    case 10: game = TaleWorlds.Library.MathF.Ceiling(10000f / (float)g * d); now = result; what += " (cena w miescie " + g + " d, w starej monecie " + o + ")"; break;
                    case 11:
                        {
                            float sd = Convert.ToSingle(Get(inst, "_issueDifficulty"));
                            int num = TaleWorlds.Library.MathF.Ceiling(400f + 3000f * sd);
                            game = num / g; now = o > 0 ? num / o : game;
                            break;
                        }
                    default: return;
                }
                if (site.Id != 1 && site.Id != 10) what += " (wartosc gry " + g + " d, sprzed przeliczenia " + o + ")";
                string text = site.Label + ": " + what + ", trudnosc " + (site.Id == 11 ? "-" : d.ToString("0.00", CultureInfo.InvariantCulture)) + " - ilosc: gra " + game + " -> " + now + extra;
                _notes.Add(inst, new Rec { Text = text });
                if (site.Id == 11 && _logged < 200) { _logged++; Log.Info("Ilosci zadan (f28): " + text + "."); }   // zadanie gracza, wolane przy wystawieniu przemytnikow
            }
            catch (Exception e) { Stumble("Note", e); }
        }

        /// <summary>Przyjecie zadania: linia z iloscia gry i nasza (wpis z chwili policzenia ilosci w sprawie).</summary>
        private static void OnQuestStarted(QuestBase quest)
        {
            if (quest == null || !On) return;
            try
            {
                IssueBase iss = null;
                var g = quest.QuestGiver;
                if (g != null && g.Issue != null && (ReferenceEquals(g.Issue.IssueQuest, quest) || g.Issue.IssueQuest == null)) iss = g.Issue;
                if (iss == null) iss = IssueManager.GetIssueOfQuest(quest);
                Rec r;
                if (iss == null || !_notes.TryGetValue(iss, out r)) return;
                if (_logged >= 200) return;
                _logged++;
                Log.Info("Ilosci zadan (f28): zadanie przyjete " + quest.StringId + " - " + r.Text + ".");
            }
            catch (Exception e) { Stumble("OnQuestStarted", e); }
        }

        // ------------------------------------------------------------ start sesji

        /// <summary>Z ArmouryBehavior.OnSessionLaunched (po HistoricalPrices.Apply i QuestPay8.SessionStart): nasluch przyjecia zadan; latki raz na proces.</summary>
        internal static void SessionStart(CampaignBehaviorBase owner)
        {
            _logged = 0;
            try { CampaignEvents.OnQuestStartedEvent.AddNonSerializedListener(owner, OnQuestStarted); }
            catch (Exception e) { Stumble("SessionStart(nasluch)", e); }
            EnsureHooks();
        }

        private static void EnsureHooks()
        {
            var s = Settings.Current;
            if (_tried) { Log.Info("Ilosci zadan (f28, klucz Hist Quest Qty Old Coin): " + (On ? "CZYNNE" : "NIECZYNNE (klucz albo ceny historyczne wylaczone)") + " - " + _status + (_stumbles > 0 ? "; potkniecia " + _stumbles : "") + "."); return; }
            if (s == null || !s.HistQuestQtyOldCoin || !HistoricalPrices.On)
            {
                Log.Info("Ilosci zadan (f28, klucz Hist Quest Qty Old Coin): " + (s != null && s.HistQuestQtyOldCoin ? "ceny historyczne wylaczone" : "WYLACZONE (MCM)") + " - nic nie wpiete, zadania licza ilosci jak gra.");
                return;   // bez _tried: wlaczony pozniej - wpina przy nastepnym wczytaniu
            }
            _tried = true;
            var done = new List<string>(); var miss = new List<string>();
            int swaps = 0;
            try
            {
                if (_h == null) _h = new Harmony("com.jeff.armoury.f28qty");
                _getValue = AccessTools.PropertyGetter(typeof(ItemObject), "Value");
                _old = AccessTools.Method(typeof(QuestQty28), nameof(Old));
                _oldPrice = AccessTools.Method(typeof(QuestQty28), nameof(OldPrice));
                _oldAvg = AccessTools.Method(typeof(QuestQty28), nameof(OldAvg));
                _pDiff = AccessTools.PropertyGetter(typeof(IssueBase), "IssueDifficultyMultiplier");
                var tr = new HarmonyMethod(typeof(QuestQty28), nameof(Il));
                foreach (var site in Sites)
                {
                    try
                    {
                        var outer = AccessTools.TypeByName(NS + site.Outer);
                        var t = outer != null ? AccessTools.Inner(outer, site.Inner) : null;
                        MethodBase m = null;
                        if (t != null) m = site.Method == ".ctor" ? (MethodBase)AccessTools.DeclaredConstructor(t, site.Args) : AccessTools.DeclaredMethod(t, site.Method);
                        if (m == null) { miss.Add(site.Label + " BRAK " + site.Inner + "." + site.Method); continue; }
                        site.M = m;
                        _byHandle[m.MethodHandle.Value] = site;
                        HarmonyMethod pre = null, post = null;
                        if (site.Note)
                        {
                            pre = new HarmonyMethod(typeof(QuestQty28), nameof(Pre));
                            post = new HarmonyMethod(typeof(QuestQty28), m is MethodInfo && ((MethodInfo)m).ReturnType == typeof(int) ? nameof(IntPost) : nameof(VoidPost));
                        }
                        if (site.Kind == Swap.AvgField)
                        {
                            _fAvg = AccessTools.Field(t, site.Field);
                            _pClass = AccessTools.Property(t, "RequestedWeaponClass");
                            if (_fAvg == null || _fAvg.FieldType != typeof(int)) { miss.Add(site.Label + " BRAK pola " + site.Field); continue; }
                        }
                        _h.Patch(m, prefix: pre, transpiler: tr, postfix: post);
                        if (site.Swaps == site.Want) { _hooked++; swaps += site.Swaps; done.Add(site.Label); }
                        else miss.Add(site.Label + " (podmian " + Math.Max(0, site.Swaps) + "/" + site.Want + ")");
                    }
                    catch (Exception e) { miss.Add(site.Label + " BLAD (" + e.GetType().Name + ")"); Stumble("EnsureHooks(" + site.Label + ")", e); }
                }
            }
            catch (Exception e) { Stumble("EnsureHooks", e); miss.Add("wyjatek"); }
            _status = "wpiete w " + _hooked + "/" + Sites.Length + " metodach (podmian " + swaps + "): " + string.Join(", ", done.ToArray()) + (miss.Count > 0 ? "; BRAK: " + string.Join(", ", miss.ToArray()) : "");
            Log.Info("Ilosci zadan (f28, klucz Hist Quest Qty Old Coin): ilosci zadan od wartosci sprzed przeliczenia " + _status + ".");
        }
    }
}
