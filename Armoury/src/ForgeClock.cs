using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// JEDEN ZEGAR KUZNI (wpis 83; Jeff 05.10: "klikam wykuj miecz, pokazuje X godzin - powinno wykuwac sie po X godzinach;
    /// wykuje 10 x 7 h, to powinienem dostawac co 7 godzin miecz, i nie kuje sie, jak mnie nie ma w miescie"; "godziny
    /// z ekranu BK"; "noc - tak").
    ///
    /// Bylo: trzy zegary. Pancerz w zakladce CRAFT (BK) wpadal do sakw od razu, BK sumowal godziny (stamina / 6) i po
    /// zamknieciu ekranu kazal je odsiedziec naraz (StartCraftingMenu); bron (vanilla) - to samo PLUS nasz projekt "van"
    /// z WeaponDaysPerTier, ktory kul dalej pod nieobecnosc (ForgeWorksWithoutYou) - czas liczony dwa razy.
    ///
    /// Teraz: Craft = material, stamina i wynik przy kowadle (jak bylo), a sztuka idzie do kolejki tego kowala z godzinami
    /// z ekranu BK (zuzyta stamina / 6). Kolejka kuje po kolei, sztuka wychodzi, gdy jest gotowa, tylko gdy jestes w tej
    /// osadzie (ForgeOnlyWhileThere), z przerwa nocna 23-5 (WorkshopNightRest). Menu odsiadki BK wylaczone.
    /// </summary>
    internal static class ForgeClock
    {
        internal static bool On { get { var s = Settings.Current; return s != null && s.ForgeOneClock; } }

        /// <summary>BK: "Hours spent for all actions" = zuzyta stamina / 6.</summary>
        internal static float HoursOf(int stamina) { return Math.Max(0.25f, stamina / 6f); }

        // ------------------------------------------------------------ C5 (Jeff 14.10 "jak u kowali AI"): czas kucia gracza = praca kowala w warsztacie AI
        // Bylo (audyt 05.10 produkcja/W8): gracz kul kolczuge t3 w 12.5 h (stamina / 6), warsztat AI te sama sztuke w 51 roboczodniach. Teraz jedna regula:
        // sztuka gracza trwa tyle dni pracy kowala, ile liczy warsztat AI (WorkshopLaw.Needs - HistDays przy cenach historycznych, inaczej dni receptury),
        // a dzien pracy kowala to WorkHoursPerManDay godzin przy kowadle (ta sama zamiana co SmithHours). Zaplata (zamowienia, dniowka kuzni) bez zmian.
        private static int _c5Pieces; private static double _c5Hours, _c5OldHours;   // liczniki sesji (linia przy kazdej sztuce, bez zapisu)

        internal static bool LikeTown { get { var s = Settings.Current; return s != null && s.ForgeLikeTownSmith; } }

        /// <summary>C5: dni pracy kowala nad ta sztuka - te same, ktore liczy warsztat AI. 0 - brak receptury (nie bron ani zbroja).</summary>
        internal static float SmithDays(ItemObject item)
        {
            if (item == null) return 0f;
            try
            {
                var c = ArmsPricing.CostOf(item);
                if (c == null) return 0f;
                return Math.Max(0.05f, HistoricalPrices.On ? HistoricalPrices.HistDays(item, c) : c.Days);
            }
            catch { return 0f; }
        }

        /// <summary>C5: godziny przy kowadle dla count sztuk (seria amunicji). Klucz wylaczony albo brak receptury - fallback (jak dotad).</summary>
        internal static float WorkHours(ItemObject item, int count, float fallback)
        {
            if (!LikeTown) return fallback;
            float d = SmithDays(item);
            if (d <= 0f) return fallback;
            return Math.Max(0.25f, d * Math.Max(1f, Settings.Current.WorkHoursPerManDay) * Math.Max(1, count));
        }

        /// <summary>C5: godziny pracy na dobe przy kowadle (przerwa nocna 23-5 przy WorkshopNightRest - AdvanceProjects).</summary>
        internal static float BenchHoursPerDay { get { var s = Settings.Current; return s != null && s.WorkshopNightRest ? 18f : 24f; } }

        /// <summary>C5: termin zamowienia lorda - dotychczasowe OrderDeadlineDays plus doby pracy przy kowadle nad ta sztuka (bez tego zamowienie
        /// zbroi bylo nie do wykonania: kolczuga 45 dob wobec 12 dob terminu). Zaplata zamowienia bez zmian.</summary>
        internal static float OrderDays(ItemObject item)
        {
            var s = Settings.Current;
            float d = s != null ? s.OrderDeadlineDays : 12f;
            if (!LikeTown || item == null) return d;
            return d + WorkHours(item, 1, 0f) / BenchHoursPerDay;
        }

        /// <summary>C5: napis dla gracza przy odlozeniu sztuki na lawe (po angielsku) i linia logu; oldHours - czas wedlug dawnej reguly (ekran BK).</summary>
        internal static void NoteQueued(ItemObject item, int count, float hours, float oldHours)
        {
            if (!LikeTown || item == null || TaleWorlds.CampaignSystem.Settlements.Settlement.CurrentSettlement == null || ArmouryBehavior.Instance == null) return;   // bez osady Queue daje sztuke od razu
            try
            {
                float per = Math.Max(1f, Settings.Current.WorkHoursPerManDay);
                _c5Pieces += Math.Max(1, count); _c5Hours += hours; _c5OldHours += oldHours;
                Log.Player("A town smith gives " + (hours / per).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " days of work to "
                           + (count > 1 ? count + " x " : "") + item.Name + ", and so must you: " + (int)Math.Ceiling(hours) + " hours at the anvil, about "
                           + (hours / BenchHoursPerDay).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " days here. The pay stays as it was.");
                Log.Info("Kuznia jak u kowali (C5): " + item.StringId + " x" + Math.Max(1, count) + " - dni kowala " + (hours / per).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
                         + ", godzin " + hours.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " (dawniej " + oldHours.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)
                         + "), dob przy kowadle " + (hours / BenchHoursPerDay).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " | w tej sesji sztuk " + _c5Pieces
                         + ", godzin " + _c5Hours.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " (dawniej " + _c5OldHours.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + ").");
            }
            catch (Exception e) { Log.Error("ForgeClock.NoteQueued", e); }
        }

        /// <summary>C5: przyklady do logu przy wycenie uzbrojenia (ArmsPricing.Build) - te same sztuki co jej linia.</summary>
        internal static string Examples(IEnumerable<ItemObject> items)
        {
            var l = new List<string>();
            float per = Math.Max(1f, Settings.Current.WorkHoursPerManDay);
            foreach (var it in items)
            {
                if (it == null) continue;
                float d = SmithDays(it);
                if (d <= 0f) continue;
                l.Add(it.StringId + " " + d.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " d = " + (int)Math.Ceiling(d * per) + " h ("
                      + (d * per / BenchHoursPerDay).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " dob)");
            }
            return "Kuznia jak u kowali (C5): " + (LikeTown ? "CZYNNA" : "wylaczona (ForgeLikeTownSmith)") + " - dzien kowala " + per + " h, przy kowadle "
                   + BenchHoursPerDay + " h na dobe; przyklady: " + (l.Count > 0 ? string.Join("; ", l.ToArray()) : "-") + ".";
        }

        // ------------------------------------------------------------ zakladka CRAFT (BK ExecuteMainActionBK)
        private static Hero _hero; private static int _stam; private static ItemObject _item; private static int _count;

        public static void CraftPre(object __instance)
        {
            _hero = null; _item = null; _stam = 0; _count = 0;
            try
            {
                if (!On) return;
                var tr = Traverse.Create(__instance);
                try
                {
                    var vm = tr.Field("crafting").GetValue();
                    var hv = vm != null ? Traverse.Create(vm).Property("CurrentCraftingHero").GetValue() : null;
                    if (hv != null) _hero = Traverse.Create(hv).Property("Hero").GetValue<Hero>();
                }
                catch { }
                if (_hero == null) _hero = Hero.MainHero;
                _stam = Forge.Stamina(_hero);
                bool armor = false;
                try { armor = tr.Property("IsInArmorMode").GetValue<bool>(); } catch { }
                if (!armor) return;
                var ac = tr.Field("armorCrafting").GetValue();
                var cur = ac != null ? Traverse.Create(ac).Property("CurrentItem").GetValue() : null;
                _item = cur != null ? Traverse.Create(cur).Property("Item").GetValue<ItemObject>() : null;
                if (_item != null) _count = MobileParty.MainParty.ItemRoster.GetItemNumber(_item);
            }
            catch { }
        }

        /// <summary>Pancerz kuty droga BK: swieza sztuka z sakw do kolejki kowala.</summary>
        public static void CraftPost()
        {
            try
            {
                if (!On || _item == null || FletchForge.RangedType(_item)) return;   // strzeleckie kolejkuje Forge.Smith
                var bag = MobileParty.MainParty.ItemRoster;
                // zbroja z kuzni jak bron: nasza regula wie, ze sie udalo i CO wyszlo spod mlota - na lawe idzie dokladnie ta sztuka
                // (z jej stanem). GetItemNumber liczy tylko PIERWSZY stos tej zbroi w sakwach: sztuka w innym stanie niz pierwszy
                // stos (np. druga kopia z kuzni) wygladalaby na "spartaczona" i zostawala w sakwach od razu, bez godzin kowala
                EquipmentElement exact;
                bool ours = ArmourQuality.Fresh(_item, out exact) && bag.FindIndexOfElement(exact) >= 0;
                if (!ours && bag.GetItemNumber(_item) <= _count) return;               // spartaczone - nic nie przybylo
                EquipmentElement fresh = default(EquipmentElement); bool found = false;
                if (ours) { fresh = exact; found = true; }
                for (int i = bag.Count - 1; i >= 0 && !found; i--)
                {
                    var el = bag.GetElementCopyAtIndex(i);
                    if (el.EquipmentElement.Item == _item && el.Amount > 0) { fresh = el.EquipmentElement; found = true; break; }
                }
                if (!found) return;
                int spent = Math.Max(0, _stam - Forge.Stamina(_hero));
                bag.AddToCounts(fresh, -1);
                float h = WorkHours(_item, 1, HoursOf(spent));   // C5: dni kowala z warsztatu AI (wylaczone - godziny z ekranu BK)
                Queue(_item, fresh.ItemModifier, 1, h);
                NoteQueued(_item, 1, h, HoursOf(spent));
            }
            catch (Exception e) { Log.Error("ForgeClock.CraftPost", e); }
            finally { _item = null; }
        }

        /// <summary>Wyrob do kolejki kowala w tej osadzie; wydanie w AdvanceProjects, gdy godziny miną.</summary>
        internal static void Queue(ItemObject item, ItemModifier mod, int count, float hours)
        {
            var here = TaleWorlds.CampaignSystem.Settlements.Settlement.CurrentSettlement;
            var b = ArmouryBehavior.Instance;
            if (b == null || here == null)
            {
                MobileParty.MainParty.ItemRoster.AddToCounts(new EquipmentElement(item, mod), Math.Max(1, count));
                return;
            }
            b.StartProject(item, 1, hours / 24f, here, "bk", mod != null ? mod.StringId : "", Math.Max(1, count));
            float queued = b.ProjectHoursLeftHere(here);
            Log.Player("On the bench: " + (count > 1 ? count + " x " : "") + item.Name + " - " + Project.TimeLabel(hours / 24f)
                       + " of work. Your queue here: " + Project.TimeLabel(queued / 24f) + ". The work goes on only while you stay in "
                       + here.Name + "; each piece comes off the anvil when it is done.");
        }

        /// <summary>BK StartCraftingMenu: zamiast odsiadki wszystkich godzin naraz - nasze czekanie przy kowadle.</summary>
        public static bool SkipBkWait()
        {
            if (!On) return true;
            try
            {
                var b = ArmouryBehavior.Instance;
                var here = TaleWorlds.CampaignSystem.Settlements.Settlement.CurrentSettlement;
                if (b != null && b.HasProjectsHere(here)) GameMenu.SwitchToMenu("arm_project_wait");
            }
            catch (Exception e) { Log.Error("ForgeClock.SkipBkWait", e); }
            return false;
        }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var tMixin = QuartermasterLaw.FindType("BannerKings.UI.Extensions.CraftingMixin");
                var mMain = tMixin != null ? AccessTools.Method(tMixin, "ExecuteMainActionBK") : null;
                if (mMain != null)
                    h.Patch(mMain, prefix: new HarmonyMethod(typeof(ForgeClock), nameof(CraftPre)) { priority = Priority.First },
                                   postfix: new HarmonyMethod(typeof(ForgeClock), nameof(CraftPost)) { priority = Priority.Last });
                var tAct = QuartermasterLaw.FindType("BannerKings.Behaviours.BKSettlementActions");
                var mWait = tAct != null ? AccessTools.Method(tAct, "StartCraftingMenu") : null;
                if (mWait != null)
                    h.Patch(mWait, prefix: new HarmonyMethod(typeof(ForgeClock), nameof(SkipBkWait)) { priority = Priority.Last });
                Log.Info("ForgeClock: jeden zegar kuzni " + (On ? "CZYNNY" : "wylaczony w MCM") + " - kucie CRAFT " + (mMain != null ? "wpiete" : "BRAK")
                         + ", odsiadka BK " + (mWait != null ? "zastapiona kolejka" : "BRAK") + ".");
            }
            catch (Exception e) { Log.Error("ForgeClock.ApplyAll", e); }
        }
    }
}
