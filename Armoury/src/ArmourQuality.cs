using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// ZBROJA Z KUZNI JAK BRON (Jeff 07.10: "jak kujemy zbroje, to jest ta sama zasada jak przy broni, ze pancerz mozna
    /// zepsuc lub zrobic legendary, i dostaje bonusy"; "tak, ale daj mi tabele z przykladami").
    ///
    /// Bylo (do 127): zbroja z zakladki CRAFT (Banner Kings, CraftingMixin.ExecuteMainActionBK) NIGDY nie wychodzila zepsuta
    /// ani legendarna - BK szuka modyfikatora o mnozniku ceny dokladnie 0 albo 1 (ItemModifierGroupExtension.
    /// GetRandomModifierWithTarget(GetModifierForCraftedItem)), a zaden modyfikator zbroi w grze (RBM: 0.1 0.3 0.6 1.5 2.5 5)
    /// takiego nie ma; pekniecie liczyl BK z WLASNEJ trudnosci (1% za kazdy punkt ponad Smithing), bez progu.
    ///
    /// Teraz ta sama regula co luki i kusze (Forge.Smith): prog Recipe.SkillNeeded (SmithingSkillPerTier) - ponizej kowadlo
    /// odmawia, nic nie schodzi; pekniecie Forge.FailureChance i jakosc Forge.RollQuality z Recipe.Difficulty
    /// (SmithingDifficultyPerTier) z sufitem tieru (t1-3 najwyzej dobra, t4 najwyzej bardzo dobra); modyfikator z grupy
    /// zbroi wedle drabinki z cen (Grade - w grupie chain etykiety jakosci sa pomieszane). Material, stamina, XP,
    /// kolejka kowala (ForgeClock) i komunikaty - droga BK jak dotad. Zakladka pokazuje prog jako "Difficulty" i nasza
    /// szanse pekniecia jako "Botching Chance".
    ///
    /// Kontekst zyje tylko na czas JEDNEGO klikniecia Craft (prefiks -> finalizer ExecuteMainActionBK); poza nim
    /// CalculateBotchingChance i GetRandomModifierWithTarget dzialaja jak u BK (warsztaty BK, dymki tarcz i lukow).
    /// Wylacznik ArmourCraftLikeWeapons = false: zbroja z CRAFT jak w 127 (zwykla albo peka wedle BK).
    /// </summary>
    internal static class ArmourQuality
    {
        /// <summary>Wylacznik paczki (kucie zbroi w CRAFT i ksiega zuzycia sztuk z modyfikatorem).</summary>
        internal static bool On { get { var s = Settings.Current; return s != null && s.ArmourCraftLikeWeapons; } }

        /// <summary>Regula kucia zbroi - tylko przy wlaczonej kuzni Armoury (CraftingEnabled), jak luczarnia i kwit materialow.</summary>
        internal static bool CraftOn { get { var s = Settings.Current; return s != null && s.ArmourCraftLikeWeapons && s.CraftingEnabled; } }

        // ---------------------------------------------------------------- kontekst jednego klikniecia
        private static bool _active, _rolled, _postRan, _success;
        private static ItemObject _item;
        private static Hero _smith;
        private static Recipes.Recipe _r;
        private static int _skill, _before;
        private static float _crack = -1f;
        private static ItemModifier _mod;

        /// <summary>Nasza brama odmowila (za malo Smithing) - BK nie kuje; postfiks FletchForge nie uczy wzorow i nie pokazuje okna.</summary>
        internal static bool Refused;

        private static ItemObject _hintItem;          // dymek ArmorItemVM.GetHint (prefiks -> finalizer)

        // ---------------------------------------------------------------- liczniki kampanii (log) i drabinki
        private static Campaign _campaign;
        private static int _made, _cracked, _refused, _stumbles;
        private static readonly Dictionary<string, int> _byMod = new Dictionary<string, int>();
        private sealed class Ladder { public ItemModifier Legendary, Masterwork, Fine, Inferior, Poor; }
        private static readonly Dictionary<ItemModifierGroup, Ladder> _ladders = new Dictionary<ItemModifierGroup, Ladder>();

        /// <summary>Stan czyszczony miedzy kampaniami (wolane samo przy zmianie Campaign.Current).</summary>
        internal static void Reset()
        {
            _made = 0; _cracked = 0; _refused = 0; _stumbles = 0;
            _byMod.Clear(); _ladders.Clear();
            Clear(); Refused = false; _hintItem = null;
        }

        private static void Touch()
        {
            try
            {
                var c = Campaign.Current;
                if (!ReferenceEquals(c, _campaign)) { _campaign = c; Reset(); }
            }
            catch { }
        }

        private static void Clear()
        {
            _active = false; _rolled = false; _postRan = false; _success = false;
            _item = null; _smith = null; _r = default(Recipes.Recipe); _skill = 0; _before = 0; _crack = -1f; _mod = null;
        }

        /// <summary>Potkniecie na jednej sztuce: licznik, do logu tylko pierwsze 5 (dymek i lista kowadla wolaja co klatke).</summary>
        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_stumbles <= 5) Log.Error(where, e);
        }

        /// <summary>Gdy nasza regula przed chwila wykula te sztuke - jej dokladny stan (dla kolejki kowala i okna wyniku).</summary>
        internal static bool Fresh(ItemObject item, out EquipmentElement el)
        {
            el = default(EquipmentElement);
            if (!_active || !_rolled || item == null || item != _item) return false;
            el = new EquipmentElement(item, _mod);
            return true;
        }

        // ---------------------------------------------------------------- ExecuteMainActionBK
        /// <summary>
        /// Prefiks (po luczarni FletchForge): zbroja w trybie pancerzy - prog i kontekst naszych kosci. false = odmowa
        /// (BK nie zdejmuje materialu ani staminy). Gdy wczesniejszy prefiks juz przejal klikniecie (luk, wzor nieznany) - nic.
        /// </summary>
        public static bool CraftPrefix(object __instance, bool __runOriginal)
        {
            Clear(); Refused = false;
            if (!__runOriginal) return true;
            try
            {
                if (!CraftOn || __instance == null) return true;
                var tr = Traverse.Create(__instance);
                bool armorMode = false;
                try { armorMode = tr.Property("IsInArmorMode").GetValue<bool>(); } catch { }
                if (!armorMode) return true;
                var ac = tr.Field("armorCrafting").GetValue();
                var cur = ac != null ? Traverse.Create(ac).Property("CurrentItem").GetValue() : null;
                var item = cur != null ? Traverse.Create(cur).Property("Item").GetValue<ItemObject>() : null;
                if (item == null || !item.HasArmorComponent) return true;      // tarcze, luki, amunicja - nie ta regula
                Hero smith = null;
                try
                {
                    var cvm = tr.Field("crafting").GetValue();
                    var hvm = cvm != null ? Traverse.Create(cvm).Property("CurrentCraftingHero").GetValue() : null;
                    if (hvm != null) smith = Traverse.Create(hvm).Property("Hero").GetValue<Hero>();
                }
                catch { }
                return Begin(item, smith);
            }
            catch (Exception e) { Stumble("ArmourQuality.CraftPrefix", e); Clear(); return true; }
        }

        /// <summary>Prog jak przy lukach: za malo Smithing = odmowa; inaczej kontekst kosci dla tej jednej sztuki.</summary>
        internal static bool Begin(ItemObject item, Hero smith)
        {
            Clear(); Refused = false;
            try
            {
                if (!CraftOn || item == null || !item.HasArmorComponent) return true;
                Touch();
                var hero = smith ?? Hero.MainHero;
                var r = Recipes.For(item);
                int skill = hero.GetSkillValue(DefaultSkills.Crafting);
                if (skill < r.SkillNeeded)
                {
                    Refused = true; _refused++;
                    Log.Player("That is beyond your hand. You need " + r.SkillNeeded + " Smithing.", true);
                    Log.Info("Zbroja z kuzni: odmowa - " + item.StringId + " t" + Recipes.Grade(item) + " wymaga Smithing " + r.SkillNeeded
                             + ", kowal " + Who(hero) + " ma " + skill + " (nic nie zeszlo)." + Tally());
                    return false;
                }
                _active = true; _item = item; _smith = hero; _r = r; _skill = skill;
                _before = TotalOf(item);
                return true;
            }
            catch (Exception e) { Stumble("ArmourQuality.Begin", e); Clear(); return true; }   // ta jedna sztuka idzie droga BK
        }

        /// <summary>Postfiks z pierwszenstwem (przed kolejka kowala): czy BK dolozyl sztuke do sakw.</summary>
        public static void CraftPostfix()
        {
            if (!_active) return;
            try { _postRan = true; _success = TotalOf(_item) > _before; }
            catch (Exception e) { Stumble("ArmourQuality.CraftPostfix", e); }
        }

        /// <summary>Finalizer: linia logu i sprzatanie kontekstu (biegnie zawsze, takze po wyjatku BK).</summary>
        public static Exception CraftFinalizer(Exception __exception)
        {
            try
            {
                if (_active)
                {
                    string res;
                    if (!_postRan) res = "przerwane (wyjatek w BK)";
                    else if (!_success) { _cracked++; res = "PEKLA (material przepadl, pol staminy - jak u BK)"; }
                    else
                    {
                        _made++;
                        string key = _rolled ? (_mod != null ? _mod.StringId : "zwykla") : "bez grupy modyfikatorow";
                        int n; _byMod.TryGetValue(key, out n); _byMod[key] = n + 1;
                        res = "wykuta: " + key;
                    }
                    Log.Info("Zbroja z kuzni: " + _item.StringId + " t" + Recipes.Grade(_item) + " grupa " + GroupId(_item)
                             + " | kowal " + Who(_smith) + " Smithing " + _skill + ", prog " + _r.SkillNeeded
                             + ", trudnosc " + _r.Difficulty + " | pekniecie " + (_crack >= 0f ? (_crack * 100f).ToString("0.0") + "%" : "(BK nie pytal)")
                             + " -> " + res + "." + Tally());
                }
            }
            catch (Exception e) { Stumble("ArmourQuality.CraftFinalizer", e); }
            Clear(); Refused = false;
            return __exception;
        }

        /// <summary>Wszystkie sztuki tej zbroi w sakwach, we wszystkich stanach (GetItemNumber gry liczy tylko pierwszy stos).</summary>
        private static int TotalOf(ItemObject item)
        {
            int n = 0;
            try
            {
                var r = MobileParty.MainParty.ItemRoster;
                for (int i = 0; i < r.Count; i++) { var el = r.GetElementCopyAtIndex(i); if (el.EquipmentElement.Item == item) n += el.Amount; }
            }
            catch { }
            return n;
        }

        private static string Tally()
        {
            var parts = new List<string>();
            foreach (var kv in _byMod) parts.Add(kv.Key + " " + kv.Value);
            return " Od wczytania: wykute " + _made + (parts.Count > 0 ? " [" + string.Join(", ", parts) + "]" : "") + ", pekniete " + _cracked
                   + ", odmowy " + _refused + ", potkniecia " + _stumbles + ".";
        }

        private static string Who(Hero h) { try { return h != null && h.Name != null ? h.Name.ToString() : "?"; } catch { return "?"; } }

        private static string GroupId(ItemObject it)
        {
            try { var g = it.ItemComponent != null ? it.ItemComponent.ItemModifierGroup : null; return g != null ? g.StringId : "(brak)"; }
            catch { return "?"; }
        }

        // ---------------------------------------------------------------- kosci BK -> nasze (tylko w kontekscie)
        /// <summary>BKSmithingModel.CalculateBotchingChance: w kliknieciu i w dymku zbroi - nasze pekniecie (Forge.FailureChance).</summary>
        public static bool BotchPrefix(Hero __0, ref float __result)
        {
            try
            {
                if (!CraftOn) return true;
                if (_active)
                {
                    __result = Forge.FailureChance(_r, 1, __0 ?? _smith);
                    _crack = __result;
                    return false;
                }
                var it = _hintItem;
                if (it == null || !it.HasArmorComponent) return true;
                __result = Forge.FailureChance(Recipes.For(it), 1, __0 ?? Hero.MainHero);
                return false;
            }
            catch (Exception e) { Stumble("ArmourQuality.Botch", e); return true; }
        }

        /// <summary>ItemModifierGroupExtension.GetRandomModifierWithTarget: w kliknieciu - nasza jakosc (Forge.RollQuality).</summary>
        public static bool ModifierPrefix(ref ItemModifier __result)
        {
            if (!_active) return true;
            try
            {
                __result = Forge.RollQuality(_item, _r, 1, _smith);
                _rolled = true; _mod = __result;
                // nazwa stanu w komunikacie BK ("... with Rusty X quality"): gra sklada ja ze wspolnego tekstu modyfikatora
                if (__result != null) { try { new EquipmentElement(_item, __result).GetModifiedItemName(); } catch { } }
                return false;
            }
            catch (Exception e) { Stumble("ArmourQuality.Modifier", e); return true; }
        }

        /// <summary>BKSmithingModel.CalculateArmorDifficulty: "Difficulty" zbroi w zakladce = nasz prog (jak u lukow).</summary>
        public static void DifficultyPostfix(ItemObject __0, ref int __result)
        {
            try { if (CraftOn && __0 != null && __0.HasArmorComponent) __result = Recipes.For(__0).SkillNeeded; }
            catch (Exception e) { Stumble("ArmourQuality.Difficulty", e); }
        }

        public static void HintPrefix(object __instance)
        {
            _hintItem = null;
            try { if (CraftOn && __instance != null) _hintItem = Traverse.Create(__instance).Property("Item").GetValue<ItemObject>(); }
            catch { _hintItem = null; }
        }

        public static Exception HintFinalizer(Exception __exception) { _hintItem = null; return __exception; }

        // ---------------------------------------------------------------- drabinka jakosci zbroi
        /// <summary>
        /// Modyfikator zbroi danej jakosci. Drabinka z cen modyfikatorow, ktore wychodza z warsztatu (ProductionDropScore > 0 -
        /// bez stanow zniszczenia RBM "Damaged/Ruined"): trzy dobre od najdrozszego = legendarna, bardzo dobra, dobra; dwa zle
        /// = slaba (drozszy), zepsuta (tanszy). W grupie chain etykiety sa pomieszane (gra scala Native i RBM atrybut po
        /// atrybucie: Loose "poor" z RBM, Rusty "poor" z Native, "Scratched" x0.1 "inferior" z RBM) - wedle etykiet slaba
        /// kolczuga bylaby "Scratched" (cena x0.1), a zepsuta losowo Loose albo Rusty; drabinka daje kolczudze to samo co plycie,
        /// skorze i suknu: slaba = Loose (x0.6), zepsuta = Rusty (x0.3). Grupa o innej budowie - jak dotad, wedle etykiet.
        /// </summary>
        internal static ItemModifier Grade(ItemModifierGroup group, ItemQuality q)
        {
            if (group == null || q == ItemQuality.Common) return null;
            var lad = LadderOf(group);
            if (lad != null)
            {
                switch (q)
                {
                    case ItemQuality.Legendary: return lad.Legendary;
                    case ItemQuality.Masterwork: return lad.Masterwork;
                    case ItemQuality.Fine: return lad.Fine;
                    case ItemQuality.Inferior: return lad.Inferior;
                    case ItemQuality.Poor: return lad.Poor;
                }
            }
            var mods = group.GetModifiersBasedOnQuality(q);
            if (mods == null || mods.Count == 0) return null;
            return mods.Count == 1 ? mods[0] : mods[MBRandom.RandomInt(0, mods.Count)];
        }

        private static Ladder LadderOf(ItemModifierGroup group)
        {
            Touch();
            Ladder lad;
            if (_ladders.TryGetValue(group, out lad)) return lad;
            var good = new List<ItemModifier>();
            var bad = new List<ItemModifier>();
            var seen = new HashSet<ItemModifier>();
            foreach (var m in group.ItemModifiers)
            {
                if (m == null || m.ProductionDropScore <= 0f || !seen.Add(m)) continue;   // ta sama sztuka dopisana do grupy dwa razy - raz
                if (m.PriceMultiplier > 1.001f) good.Add(m);
                else if (m.PriceMultiplier < 0.999f && m.PriceMultiplier > 0f) bad.Add(m);
            }
            good.Sort((a, b) => b.PriceMultiplier.CompareTo(a.PriceMultiplier));
            bad.Sort((a, b) => b.PriceMultiplier.CompareTo(a.PriceMultiplier));
            lad = null;
            if (good.Count == 3 && bad.Count == 2
                && good[0].PriceMultiplier > good[1].PriceMultiplier && good[1].PriceMultiplier > good[2].PriceMultiplier
                && bad[0].PriceMultiplier > bad[1].PriceMultiplier)
                lad = new Ladder { Legendary = good[0], Masterwork = good[1], Fine = good[2], Inferior = bad[0], Poor = bad[1] };
            _ladders[group] = lad;
            try
            {
                Log.Info("Zbroja z kuzni: drabinka jakosci grupy " + group.StringId + ": " + (lad != null
                    ? "legendarna " + Desc(lad.Legendary) + ", bardzo dobra " + Desc(lad.Masterwork) + ", dobra " + Desc(lad.Fine)
                      + ", slaba " + Desc(lad.Inferior) + ", zepsuta " + Desc(lad.Poor)
                    : "inna budowa (" + good.Count + " dobrych i " + bad.Count + " zlych modyfikatorow z warsztatu) - wedle etykiet jakosci z pliku") + ".");
            }
            catch { }
            return lad;
        }

        private static string Desc(ItemModifier m)
        {
            return m.StringId + " (pancerz RBM " + (m.Armor >= 0 ? "+" : "") + m.Armor + (m.PriceMultiplier < 1f ? " - w grze ochrona x stan (ConditionScaling)" : " pkt") + ", cena x" + m.PriceMultiplier.ToString("0.##") + ", etykieta " + m.ItemQuality + ")";
        }

        // ---------------------------------------------------------------- wpiecie
        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var tModel = QuartermasterLaw.FindType("BannerKings.Models.Vanilla.BKSmithingModel");
                var tExt = QuartermasterLaw.FindType("BannerKings.Extensions.ItemModifierGroupExtension");
                var tMixin = QuartermasterLaw.FindType("BannerKings.UI.Extensions.CraftingMixin");
                var tItemVm = QuartermasterLaw.FindType("BannerKings.UI.Crafting.ArmorItemVM");
                if (tModel == null || tMixin == null) { Log.Info("Zbroja z kuzni: Banner Kings nieobecny - nic do wpiecia."); return; }
                var ok = new List<string>(); var miss = new List<string>();
                var mMain = AccessTools.Method(tMixin, "ExecuteMainActionBK");
                var mBotch = AccessTools.Method(tModel, "CalculateBotchingChance");
                var mMod = tExt != null ? AccessTools.Method(tExt, "GetRandomModifierWithTarget") : null;
                var mDiff = AccessTools.Method(tModel, "CalculateArmorDifficulty");
                var mHint = tItemVm != null ? AccessTools.Method(tItemVm, "GetHint") : null;
                // kosci i modyfikator - tylko razem z kliknieciem, inaczej kontekst nigdy by nie powstal
                if (mMain != null && mBotch != null && mMod != null)
                {
                    h.Patch(mMain,
                        prefix: new HarmonyMethod(typeof(ArmourQuality), nameof(CraftPrefix)) { priority = Priority.Low },
                        postfix: new HarmonyMethod(typeof(ArmourQuality), nameof(CraftPostfix)) { priority = Priority.First },
                        finalizer: new HarmonyMethod(typeof(ArmourQuality), nameof(CraftFinalizer)));
                    h.Patch(mBotch, prefix: new HarmonyMethod(typeof(ArmourQuality), nameof(BotchPrefix)));
                    h.Patch(mMod, prefix: new HarmonyMethod(typeof(ArmourQuality), nameof(ModifierPrefix)));
                    ok.Add("kucie CRAFT"); ok.Add("pekniecie"); ok.Add("jakosc");
                }
                else miss.Add("kucie CRAFT / pekniecie / jakosc (" + (mMain != null) + "/" + (mBotch != null) + "/" + (mMod != null) + ")");
                if (mDiff != null) { h.Patch(mDiff, postfix: new HarmonyMethod(typeof(ArmourQuality), nameof(DifficultyPostfix))); ok.Add("Difficulty = prog"); }
                else miss.Add("Difficulty");
                if (mHint != null)
                {
                    h.Patch(mHint, prefix: new HarmonyMethod(typeof(ArmourQuality), nameof(HintPrefix)),
                                   finalizer: new HarmonyMethod(typeof(ArmourQuality), nameof(HintFinalizer)));
                    ok.Add("dymek Botching Chance");
                }
                else miss.Add("dymek");
                Log.Info("Zbroja z kuzni: zbroja z zakladki CRAFT jak bron (prog, pekniecie i jakosc z kowalstwa) - "
                         + (CraftOn ? "CZYNNE" : On ? "uspione (kuznia Armoury wylaczona - CraftingEnabled)" : "wylaczone w MCM (ArmourCraftLikeWeapons)") + "; wpiete: " + string.Join(", ", ok)
                         + " | BRAK: " + (miss.Count > 0 ? string.Join(", ", miss) : "nic") + ".");
            }
            catch (Exception e) { Log.Error("ArmourQuality.ApplyAll", e); }
        }
    }
}
