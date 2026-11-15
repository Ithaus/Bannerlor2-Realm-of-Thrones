using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// 537 E5 (projekt docs/projekty/KONIE-UPRZEZE.md rozdz. 4 "widok wirtualny" i 6): kto jedzie na jakiej parze kon + uprzaz - BEZ przenoszenia sztuk.
    ///  - panel przedmiotu w ekwipunku i handlu (ItemMenuVM.SetItem, ten sam cel co ArmouryStats / SteelItemMenu - osobny postfiks): kon i uprzaz w ladunku
    ///    gracza (strona PlayerInventory) - "Ridden by", sztuki w parze bez jezdzca, bez pasujacej uprzezy / konia; przy otwartym stash (Escrow) takze strona
    ///    zbrojowni (OtherInventory) - pary piechoty z wolnych sztuk zbrojowni (D11), liczone wedlug przedmiotu (zbrojownia DTE bez modyfikatorow w rekordzie);
    ///  - "Review the army" -> "Horses and harness": strona "Who rides what" dopisana na koncu (ArmyReview.HorsePages).
    /// Liczby TYLKO z rekordu MarchPairs.Main (ten sam co HUD na pasku mapy i linia doby "Pary marszowe (gracz)"); nic nie liczymy w UI poza grupowaniem
    /// list rekordu. Wolane przy najechaniu na przedmiot i przy otwarciu okna - nigdy co klatke. Tylko gracz (D3). Klucz RiddenByView.
    /// Bledy liczone (Stumble), bez wylacznika.
    /// </summary>
    internal static class RiddenBy
    {
        private static int _stumbles, _panels, _pages;
        private static readonly HashSet<string> _errSites = new HashSet<string>();

        internal static bool On { get { var s = Settings.Current; return s != null && s.RiddenByView; } }

        internal static void Reset()
        {
            _stumbles = 0; _panels = 0; _pages = 0;
            lock (_errSites) _errSites.Clear();
        }

        private static void Stumble(string where, Exception e)
        {
            Interlocked.Increment(ref _stumbles);
            bool first; lock (_errSites) first = _errSites.Add(where);
            if (first) { try { Log.Error("RiddenBy." + where, e); } catch { } }
        }

        private static string Nm(CharacterObject ch)
        {
            try { return ch == null ? "-" : ch.Name != null ? ch.Name.ToString() : ch.StringId; } catch { return "-"; }
        }

        private static string Nm(ItemObject it, ItemModifier md)
        {
            try { return it == null ? "-" : new EquipmentElement(it, md).GetModifiedItemName().ToString(); }
            catch { try { return it.Name != null ? it.Name.ToString() : it.StringId; } catch { return "-"; } }
        }

        /// <summary>"12x Northern Spearman, 4x Levy" - najliczniejsi najpierw, najwyzej max nazw, reszta "and N more".</summary>
        private static string Men(Dictionary<CharacterObject, int> by, int max)
        {
            var l = new List<KeyValuePair<CharacterObject, int>>(by);
            l.Sort((a, b) => b.Value.CompareTo(a.Value));
            var sb = new StringBuilder();
            int shown = 0, rest = 0;
            foreach (var kv in l)
            {
                if (kv.Value <= 0) continue;
                if (shown >= max) { rest += kv.Value; continue; }
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(kv.Value).Append("x ").Append(Nm(kv.Key));
                shown++;
            }
            if (rest > 0) sb.Append(" and ").Append(rest).Append(" more");
            return sb.Length > 0 ? sb.ToString() : "nobody";
        }

        private static void Add(Dictionary<CharacterObject, int> d, CharacterObject ch, int n)
        {
            if (ch == null || n <= 0) return;
            int v; d.TryGetValue(ch, out v); d[ch] = v + n;
        }

        // ---------------------------------------------------------------- panel przedmiotu (ekwipunek, handel, stash)
        /// <summary>Wiersze panelu (nazwa, tresc) dla sztuki el (amount w tym wierszu ekranu); null - nic do dopisania. armourySide - strona zbrojowni
        /// przy otwartym stash (pary piechoty z wolnych sztuk zbrojowni, wedlug przedmiotu).</summary>
        internal static List<string[]> PanelRows(EquipmentElement el, int amount, bool armourySide) { return PanelRows(el, amount, armourySide, false); }

        /// <summary>537 E6: stableSide - strona stajni piechoty (okno "Footmen's mounts"): sztuki ze stajni, wedlug przedmiotu i modyfikatora (stajnia trzyma stan).</summary>
        internal static List<string[]> PanelRows(EquipmentElement el, int amount, bool armourySide, bool stableSide)
        {
            var it = el.Item;
            if (it == null || amount <= 0) return null;
            bool horse = KitPreview.RidingMount(it);
            bool harness = it.ItemType == ItemObject.ItemTypeEnum.HorseHarness;
            if (!horse && !harness) return null;
            if (harness && CampCart.IsPackSaddle(it)) return null;   // siodlo transportowe - poza parami (osobny system jukow)
            var r = MarchPairs.Main(false);
            if (r == null || !r.Ok) return null;
            var rows = new List<string[]>();
            rows.Add(new[] { " ", " " });
            rows.Add(new[] { "Marching pairs", " " });
            if (r.AtSea) { rows.Add(new[] { "At sea", "nobody rides - pairs count again on land" }); return rows; }
            if (horse && !Stables.IsPlainMount(it)) { rows.Add(new[] { "Not in a pair", "only a riding horse takes a harness for the march" }); return rows; }
            if (harness && !SaddleLaw.Fits(it)) { rows.Add(new[] { "Not in a pair", "this harness fits no riding horse" }); return rows; }
            byte src = stableSide ? MarchPairs.SrcStable : armourySide ? MarchPairs.SrcArmoury : MarchPairs.SrcCargo;
            if (stableSide) armourySide = false;   // stajnia trzyma modyfikator - liczenie jak ladunek (wedlug wiersza)
            var by = new Dictionary<CharacterObject, int>();
            var with = new Dictionary<string, int>();   // druga polowa pary (kon -> uprzeze, uprzaz -> konie)
            int ridden = 0, paired = 0, req = -1;
            foreach (var x in r.RiddenBy)
            {
                if (x.Kind == MarchPairs.KindCav) continue;   // jazda: zbrojownia, sztuki schowane przez kwatermistrza
                if (!Match(x, it, el.ItemModifier, horse, src, armourySide)) continue;
                ridden += x.N; Add(by, x.Ch, x.N);
                string o = horse ? Nm(x.UIt, x.UMd) : Nm(x.HIt, x.HMd);
                int v; with.TryGetValue(o, out v); with[o] = v + x.N;
            }
            foreach (var x in r.UnusedBy)
            {
                if (!Match(x, it, el.ItemModifier, horse, src, armourySide)) continue;
                paired += x.N; if (req < 0 || x.Req < req) req = x.Req;
            }
            // rekord z chwili liczenia (do 1 s, przy otwartym stash - z otwarcia): przyciete do liczby w tym wierszu
            int all = armourySide ? Math.Max(amount, ridden + paired) : amount;
            ridden = Math.Min(ridden, all); paired = Math.Min(paired, all - ridden);
            int lone = Math.Max(0, all - ridden - paired);
            rows.Add(new[] { "Ridden by", Men(by, 4) });
            if (with.Count > 0)
            {
                var wl = new List<KeyValuePair<string, int>>(with);
                wl.Sort((a, b) => b.Value.CompareTo(a.Value));
                var sb = new StringBuilder();
                int k = 0, rest = 0;
                foreach (var kv in wl) { if (k >= 3) { rest += kv.Value; continue; } if (sb.Length > 0) sb.Append(", "); sb.Append(kv.Value).Append("x ").Append(kv.Key); k++; }
                if (rest > 0) sb.Append(" and ").Append(rest).Append(" more");
                rows.Add(new[] { horse ? "With harness" : "On horses", sb.ToString() });
            }
            if (paired > 0)
            {
                string why = r.Walk > 0 ? "needs Riding " + Math.Max(0, req) + (r.PrisCargo > 0 ? ", or a prisoner rides it" : "")
                                        : r.Prisoners > 0 ? "a spare pair - prisoners ride spare pairs" : "a spare pair - every man already rides";
                rows.Add(new[] { "Paired, not ridden", paired + " (" + why + ")" });
            }
            if (lone > 0) rows.Add(new[] { horse ? "No fitting harness" : "No horse for it", lone + (horse ? " - in the herd" : " - weighs as cargo") });
            if (harness && !armourySide && ridden > 0 && MarchPairs.WeightOn) rows.Add(new[] { "Weight", ridden + " on ridden horses weigh nothing" });
            if (armourySide) rows.Add(new[] { "Counted", "all pieces of this kind in the troops' armoury" });
            if (stableSide) rows.Add(new[] { "Footmen's mounts", "kept apart from the baggage; take a pair out to sell it - the pairs are counted again later" });
            rows.Add(new[] { "Horses (map bar)", MarchHud.HorsesText(r) + " ridden" + (QuartermasterEscrow.Active ? " (as when the stash opened)" : "") });
            return rows;
        }

        private static bool Match(MarchPairs.RideLot x, ItemObject it, ItemModifier md, bool horse, byte src, bool armourySide)
        {
            if (horse) return x.HSrc == src && x.HIt == it && (armourySide || x.HMd == md);
            return x.USrc == src && x.UIt == it && (armourySide || x.UMd == md);
        }

        // ---------------------------------------------------------------- Review the army: strona "Who rides what"
        private static string KindEn(byte k)
        {
            return k == MarchPairs.KindWoundedRider ? " (wounded rider)" : k == MarchPairs.KindCavWalk ? " (rider without his own pair)" : k == MarchPairs.KindHero ? " (hero)" : "";
        }

        /// <summary>Tekst strony (akapity rozdzielone pustym wierszem - ArmyReview.Paginate); null - klucz wylaczony albo brak rekordu.</summary>
        internal static string PageText(MarchPairs.Rec r)
        {
            if (!On || r == null || !r.Ok) return null;
            Interlocked.Increment(ref _pages);
            var sb = new StringBuilder(1200);
            sb.Append("Who rides what - a man rides only on a pair: a horse, a harness that fits it, and Riding enough for both. The same pairs as Horses on the map bar.");
            if (r.AtSea) { sb.Append("\n\nAt sea nobody rides; the pairs count again on land."); return sb.ToString(); }
            int foot = Math.Max(0, r.Men - r.Ridden - r.HeroMounted);
            sb.Append("\n\nMen mounted ").Append(r.Ridden + r.HeroMounted).Append(" of ").Append(r.Men).Append(": ").Append(r.CavPairs).Append(" cavalry, ")
              .Append(r.Ride).Append(" footmen on spare horses");
            if (r.HeroMounted > 0) sb.Append(", ").Append(r.HeroMounted).Append(r.HeroMounted == 1 ? " hero" : " heroes").Append(" on their own horses");
            sb.Append('.');
            if (foot > 0)
                sb.Append(" On foot ").Append(foot).Append(": ").Append(r.WalkNoHorse).Append(" no horse, ").Append(r.WalkNoHarness).Append(" no harness, ")
                  .Append(r.WalkSkill).Append(" Riding too low.");
            // jazda wedlug typu
            if (r.CavBy.Count > 0)
            {
                sb.Append("\n\nCavalry (kit from the troops' armoury").Append(r.Preview ? ", the same hand-out as the next battle" : "").Append("):");
                var l = new List<MarchPairs.CavLot>(r.CavBy);
                l.Sort((a, b) => (b.Pairs + b.NoHarness + b.NoHorse).CompareTo(a.Pairs + a.NoHarness + a.NoHorse));
                foreach (var c in l)
                {
                    sb.Append("\n ").Append(Nm(c.Ch)).Append(": ").Append(c.Pairs).Append(" ride a pair");
                    if (c.NoHarness > 0) sb.Append("; ").Append(c.NoHarness).Append(" have a horse without harness and march with the footmen");
                    if (c.NoHorse > 0) sb.Append("; ").Append(c.NoHorse).Append(" have no horse and march with the footmen");
                }
            }
            // piechota (i jezdzcy bez pary, ranni, bohaterowie bez konia) - kto na czym
            var foots = new Dictionary<CharacterObject, List<MarchPairs.RideLot>>();
            var order = new List<CharacterObject>();
            var tot = new Dictionary<CharacterObject, int>();
            foreach (var x in r.RiddenBy)
            {
                if (x.Kind == MarchPairs.KindCav || x.Ch == null) continue;
                List<MarchPairs.RideLot> l;
                if (!foots.TryGetValue(x.Ch, out l)) { l = new List<MarchPairs.RideLot>(); foots[x.Ch] = l; order.Add(x.Ch); }
                l.Add(x); Add(tot, x.Ch, x.N);
            }
            if (order.Count > 0)
            {
                order.Sort((a, b) => tot[b].CompareTo(tot[a]));
                sb.Append("\n\nFootmen on spare horses:");
                foreach (var ch in order)
                {
                    var l = foots[ch];
                    l.Sort((a, b) => b.N.CompareTo(a.N));
                    int riding = 0; try { riding = ch.GetSkillValue(DefaultSkills.Riding); } catch { }
                    sb.Append("\n ").Append(tot[ch]).Append("x ").Append(Nm(ch)).Append(KindEn(l[0].Kind)).Append(" (Riding ").Append(riding).Append("): ");
                    int k = 0, rest = 0;
                    foreach (var x in l)
                    {
                        if (k >= 3) { rest += x.N; continue; }
                        if (k > 0) sb.Append("; ");
                        sb.Append(x.N).Append("x ").Append(Nm(x.HIt, x.HMd)).Append(" with ").Append(Nm(x.UIt, x.UMd));
                        if (x.HSrc == MarchPairs.SrcArmoury || x.USrc == MarchPairs.SrcArmoury) sb.Append(" (troops' armoury)");
                        else if (x.HSrc == MarchPairs.SrcStable || x.USrc == MarchPairs.SrcStable) sb.Append(" (footmen's mounts)");   // 537 E6
                        k++;
                    }
                    if (rest > 0) sb.Append("; ").Append(rest).Append(" on other pairs");
                }
            }
            // pary niewykorzystane, wolne konie, uprzeze bez konia
            int unusedH = Math.Max(0, r.AllHorses - r.Ridden), unusedU = Math.Max(0, r.AllHarness - r.Ridden);
            sb.Append("\n\nNot ridden: ").Append(unusedH).Append(unusedH == 1 ? " horse" : " horses").Append(" (").Append(r.FreeHerd).Append(" from the baggage in the herd)")
              .Append(", ").Append(unusedU).Append(" harness without a rider");
            if (r.UnusedPairs > 0)
            {
                sb.Append("; ").Append(r.UnusedPairs).Append(r.UnusedPairs == 1 ? " pair" : " pairs").Append(" nobody rides");
                if (r.Walk > 0 && r.UnusedReqMin >= 0) sb.Append(" (the easiest needs Riding ").Append(r.UnusedReqMin).Append(')');
                if (r.PrisCargo > 0) sb.Append(", ").Append(r.PrisCargo).Append(r.PrisCargo == 1 ? " prisoner rides" : " prisoners ride").Append(" one");
            }
            sb.Append('.');
            if (r.SkillLowEn.Count > 0) sb.Append("\n\nRiding too low for the pairs left: ").Append(string.Join(", ", r.SkillLowEn)).Append('.');
            // 537 E6: przy stajni piechoty pary piechoty stoja osobno (Footmen's mounts), wolne konie i uprzeze w ladunku
            if (FootStable.On || r.SMounts + r.SHarness > 0)
                sb.Append("\n\nThe footmen's pairs stand apart in the footmen's mounts (").Append(r.SMounts).Append(" horses, ").Append(r.SHarness)
                  .Append(" harness - town or camp menu); your baggage keeps the free horses and harness, the troops' armoury the cavalry's kit. The hover panel of a horse or harness shows who rides it.");
            else
                sb.Append("\n\nNothing is moved: the horses and harness stay in your baggage and in the troops' armoury. The hover panel of a horse or harness in your inventory shows who rides it.");
            return sb.ToString();
        }

        // ---------------------------------------------------------------- linia doby
        internal static string State(MarchPairs.Rec r)
        {
            int ride = 0, cav = 0, unused = 0;
            if (r != null) { foreach (var x in r.RiddenBy) if (x.Kind == MarchPairs.KindCav) cav++; else ride++; unused = r.UnusedBy.Count; }
            return "widok Ridden by (E5, klucz RiddenByView " + (On ? "TAK" : "NIE") + "): wierszy par piechoty " + ride + ", jazdy (wlasny dobor) " + cav
                   + ", typow jazdy " + (r != null ? r.CavBy.Count : 0) + ", grup par niewykorzystanych " + unused
                   + "; panel przedmiotu pokazany " + _panels + ", strona okna " + _pages + ", potkniecia " + _stumbles
                   + (_errSites.Count > 0 ? " (" + string.Join(", ", _errSites) + ")" : "");
        }

        internal static void CountPanel() { Interlocked.Increment(ref _panels); }
        internal static void Fail(string where, Exception e) { Stumble(where, e); }
    }

    /// <summary>537 E5: panel przedmiotu w ekwipunku / handlu / stash - wiersze "Marching pairs" (RiddenBy.PanelRows). Osobny postfiks obok ArmouryStats i SteelItemMenu.</summary>
    [HarmonyPatch(typeof(TaleWorlds.CampaignSystem.ViewModelCollection.Inventory.ItemMenuVM), "SetItem")]
    internal static class RiddenByItemMenuPatch
    {
        private static System.Reflection.FieldInfo _targetField;

        private static void Postfix(TaleWorlds.CampaignSystem.ViewModelCollection.Inventory.ItemMenuVM __instance,
                                    [HarmonyArgument("item")] TaleWorlds.CampaignSystem.ViewModelCollection.Inventory.SPItemVM spItem)
        {
            try
            {
                if (!RiddenBy.On || spItem == null || Campaign.Current == null || MobileParty.MainParty == null) return;
                var side = spItem.InventorySide;
                bool armoury = false, stable = false;
                if (side == TaleWorlds.CampaignSystem.Inventory.InventoryLogic.InventorySide.OtherInventory)
                {
                    if (FootStable.ScreenOpen) stable = true;  // 537 E6: okno "Footmen's mounts" - stajnia piechoty po stronie OtherInventory
                    else if (!QuartermasterEscrow.Active) return;   // strona kupca / lupu - nie nasze sztuki
                    else armoury = true;                       // stash DTE: OpenScreenAsStash(ArmyArmory.Armory) - zbrojownia po stronie OtherInventory
                }
                else if (side != TaleWorlds.CampaignSystem.Inventory.InventoryLogic.InventorySide.PlayerInventory) return;   // rynsztunek bohatera - wlasne Equipment
                if (_targetField == null)
                    _targetField = AccessTools.Field(typeof(TaleWorlds.CampaignSystem.ViewModelCollection.Inventory.ItemMenuVM), "_targetItem");
                var itemVm = _targetField != null ? _targetField.GetValue(__instance) as TaleWorlds.Core.ViewModelCollection.ItemVM : null;
                if (itemVm == null) return;
                var rows = RiddenBy.PanelRows(itemVm.ItemRosterElement.EquipmentElement, itemVm.ItemRosterElement.Amount, armoury, stable);
                if (rows == null) return;
                var list = __instance.TargetItemProperties;
                if (list == null) return;
                foreach (var w in rows)
                    list.Add(new TaleWorlds.CampaignSystem.ViewModelCollection.Inventory.ItemMenuTooltipPropertyVM(w[0], w[1], 0, false, null, null, false));
                RiddenBy.CountPanel();
            }
            catch (Exception e) { RiddenBy.Fail("ItemMenu", e); }
        }
    }
}
