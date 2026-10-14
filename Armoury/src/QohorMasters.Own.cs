using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// 177-2 / wariant B (Jeff 09.10 "wlasny miecz z Qohoru wedlug projektu z kuzni i z wlasna nazwa - TAK, osobna paczka"; projekt 177 rozdz. 5.8):
    /// mistrzowie Qohoru przekuwaja klinge valyrianska 1 MIARY na miecz wedlug PROJEKTU GRACZA z kuzni i z NAZWA, ktora gracz nada (jak Joffrey nazwal
    /// Wdowi Lament). Ta sama oplata, te same dni, rece kowali i wegiel co przekucie w seryjny wzor (jedna regula), takze "Take back" i czekanie.
    ///  - PROJEKT: z historii kuzni gracza (ICraftingCampaignBehavior.CraftingHistory - ostatnie projekty) albo z mieczy wykutych przez gracza w taborze;
    ///    tylko szablony mieczy (OneHandedSword, TwoHandedSword, Dual) - takie, na jakich ROT sam sklada swoje miecze valyrianskie (ROTassets.xml).
    ///  - STAL: jelec, rekojesc i glowica z projektu gracza (ze skala), KLINGA - z klng valyrianskich ROT (valsteelblade_1/2, valyrian_short_blade,
    ///    val_red_blade, val_blue_blade; w szablonach "Dual" ich odmiany _duel), najblizsza dlugoscia klindze projektu, ze skala projektu; dlugosc ponizej
    ///    progu wielkiego miecza (ValyrianGreatswordMinCm) - wynik ma 1 miare jak wejscie (stal ani nie przybywa, ani nie ubywa).
    ///  - PRZEDMIOT: Crafting.GenerateItem (gra) z id kolejnego wyrobu kuzni (crafted_item_N, CraftingCampaignBehavior.GetNextCraftedItemId), rejestracja
    ///    w menedzerze obiektow i zdarzenie OnNewItemCrafted z isCraftingOrderItem = true (gra zapisuje projekt w _craftedItemDictionary i odtwarza miecz
    ///    po wczytaniu; nasz ArmouryBehavior.OnNewItemCrafted wycenia i NIE zabiera miecza na "wykonczenie" jak zwykle kucie).
    ///  - REJESTR: ValyrianBlades.AddOwn - id wpisane jako stal valyrianska 1 miary (spis, straznik, przetop zablokowany, cena, unikat, podpowiedz),
    ///    potem RegistryMove(wejscie -> wynik) jak przy kazdym przekuciu. Lista wlasnych mieczy w zapisie rejestru (arm_vs_registry, czesc 4).
    ///  - AI: nie zleca (lord nie ma projektu z kuzni). Wylacznik QohorOwnDesign (off = 449d453: opcji nie ma; zlecenia w toku - odbior i zwrot dzialaja).
    /// Zapis zlecenia: pole 12 rekordu QohorMasters (spec projektu, base64) - starszy DLL widzi wynik "@own" jako zniknieta pozycje i oddaje klinge.
    /// </summary>
    internal static partial class QohorMasters
    {
        internal const string OwnOut = "@own";
        private static readonly string[] VsBlades = { "valsteelblade_1", "valsteelblade_2", "valyrian_short_blade", "val_red_blade", "val_blue_blade" };
        private static int _dOwnPlaced, _dOwnMade, _dOwnFailed;

        private static bool OwnOn { get { var s = Settings.Current; return s != null && s.QohorReworkEnabled && s.QohorOwnDesign; } }

        private static bool SwordTemplate(CraftingTemplate t)
        {
            return t != null && t.StringId != null && t.StringId.IndexOf("Sword", StringComparison.Ordinal) >= 0;
        }

        /// <summary>Projekty gracza do wyboru: historia kuzni + miecze wykute przez gracza w taborze (bez powtorzen), tylko szablony mieczy z klinga.</summary>
        private static List<KeyValuePair<string, WeaponDesign>> OwnDesigns()
        {
            var l = new List<KeyValuePair<string, WeaponDesign>>();
            var seen = new HashSet<string>();
            Action<WeaponDesign, string> add = (d, label) =>
            {
                try
                {
                    if (d == null || !SwordTemplate(d.Template) || d.UsedPieces == null || d.UsedPieces.Length < 4) return;
                    var b = d.UsedPieces[(int)CraftingPiece.PieceTypes.Blade];
                    if (b == null || !b.IsValid || b.CraftingPiece == null) return;
                    if (VsPieceFor(d) == null) return;   // klingi valyrianskiej ROT brak w grze - projekt nie do wykucia
                    string key = Spec(d, "");
                    if (!seen.Add(key)) return;
                    l.Add(new KeyValuePair<string, WeaponDesign>(label, d));
                }
                catch { }
            };
            try
            {
                var ccb = Campaign.Current != null ? Campaign.Current.GetCampaignBehavior<ICraftingCampaignBehavior>() : null;
                if (ccb != null && ccb.CraftingHistory != null)
                    foreach (var d in ccb.CraftingHistory.Reverse())
                        add(d, (d.WeaponName != null && !TextObject.IsNullOrEmpty(d.WeaponName) ? d.WeaponName.ToString() : d.Template.StringId) + " (forge design)");
            }
            catch { }
            try
            {
                var r = MobileParty.MainParty != null ? MobileParty.MainParty.ItemRoster : null;
                if (r != null)
                    for (int i = 0; i < r.Count; i++)
                    {
                        var it = r.GetItemAtIndex(i);
                        if (it == null || !it.IsCraftedByPlayer || it.WeaponDesign == null || ValyrianBlades.Is(it)) continue;
                        add(it.WeaponDesign, it.Name + " (in your baggage)");
                    }
            }
            catch { }
            return l;
        }

        private static bool OwnCondition(MenuCallbackArgs a)
        {
            a.optionLeaveType = GameMenuOption.LeaveType.Craft;
            if (!OwnOn) return false;
            int fee = Fee(1);
            if (Blades(1).Count == 0)
            {
                a.IsEnabled = false;
                a.Tooltip = new TextObject("{=!}You carry no Valyrian blade of one measure in your baggage. A blade you wear must be put in your baggage first; a greatsword of two measures must first be reforged into two blades.");
                return true;
            }
            if (OwnDesigns().Count == 0)
            {
                a.IsEnabled = false;
                a.Tooltip = new TextObject("{=!}The masters need a design of yours: forge a sword at a smithy first (or carry one you forged) - its guard, grip and pommel and the length of its blade.");
                return true;
            }
            if (Hero.MainHero.Gold < fee)
            {
                a.IsEnabled = false;
                a.Tooltip = new TextObject("{=!}The masters ask " + fee + " gold. You have " + Hero.MainHero.Gold + ".");
                return true;
            }
            a.Tooltip = new TextObject("{=!}Your own design and your own name for the blade. Fee: " + FeeText(1) + ". " + Math.Max(1, Settings.Current.QohorDays) + " days of work.");
            return true;
        }

        private static void OwnChooseBlade()
        {
            try
            {
                var els = new List<InquiryElement>();
                var r = MobileParty.MainParty.ItemRoster;
                foreach (var el in Blades(1))
                {
                    int n = r.GetItemNumber(el.Item);
                    els.Add(new InquiryElement(el, el.Item.Name + (el.ItemModifier != null ? " (" + el.ItemModifier.Name + ")" : "") + (n > 1 ? "  x" + n : ""), SmithMenu.ItemPic(el.Item), true, "One measure of Valyrian steel."));
                }
                if (els.Count == 0) return;
                MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                    "The Masters of Qohor", "Which blade goes into the fire?", els, true, 1, 1, "This one", "Not now",
                    delegate (List<InquiryElement> sel)
                    {
                        if (sel == null || sel.Count == 0 || !(sel[0].Identifier is EquipmentElement)) return;
                        OwnChooseDesign((EquipmentElement)sel[0].Identifier);
                    },
                    delegate (List<InquiryElement> _) { }), true);
            }
            catch (Exception e) { Log.Error("QohorMasters.OwnChooseBlade", e); }
        }

        private static void OwnChooseDesign(EquipmentElement blade)
        {
            try
            {
                var els = new List<InquiryElement>();
                foreach (var kv in OwnDesigns())
                {
                    var d = kv.Value;
                    var b = d.UsedPieces[(int)CraftingPiece.PieceTypes.Blade];
                    string hint = d.Template.StringId + ", blade " + (b.ScaledLength * 100f).ToString("0", CultureInfo.InvariantCulture) + " cm; the masters forge the blade in Valyrian steel ("
                                  + (VsPieceFor(d) != null ? VsPieceFor(d).Name.ToString() : "?") + ") and keep your guard, grip and pommel.";
                    els.Add(new InquiryElement(d, kv.Key, null, true, hint));
                }
                if (els.Count == 0) return;
                MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                    "The Masters of Qohor", "Which of your designs shall the new blade follow?", els, true, 1, 1, "This design", "Back",
                    delegate (List<InquiryElement> sel)
                    {
                        var d = sel != null && sel.Count > 0 ? sel[0].Identifier as WeaponDesign : null;
                        if (d == null) return;
                        OwnAskName(blade, d);
                    },
                    delegate (List<InquiryElement> _) { }), true);
            }
            catch (Exception e) { Log.Error("QohorMasters.OwnChooseDesign", e); }
        }

        private static string CleanName(string s)
        {
            if (s == null) return "";
            var sb = new StringBuilder();
            foreach (char c in s.Trim()) if (c != '{' && c != '}' && c != '\n' && c != '\r' && c != '\t') sb.Append(c);
            string r = sb.ToString().Trim();
            return r.Length > 40 ? r.Substring(0, 40).Trim() : r;
        }

        private static void OwnAskName(EquipmentElement blade, WeaponDesign d)
        {
            try
            {
                string def = d.WeaponName != null && !TextObject.IsNullOrEmpty(d.WeaponName) ? CleanName(d.WeaponName.ToString()) : "";
                InformationManager.ShowTextInquiry(new TextInquiryData(
                    "The Masters of Qohor", "What name shall the new blade bear? (up to 40 letters)", true, true, "Name it", "Back",
                    delegate (string text)
                    {
                        string name = CleanName(text);
                        if (name.Length == 0) { Log.Player("A Valyrian blade needs a name.", true); return; }
                        OwnConfirm(blade, d, name);
                    },
                    delegate { }, false, null, "", def), true);
            }
            catch (Exception e) { Log.Error("QohorMasters.OwnAskName", e); }
        }

        private static void OwnConfirm(EquipmentElement blade, WeaponDesign d, string name)
        {
            int fee = Fee(1);
            int days = Math.Max(1, Settings.Current.QohorDays);
            InformationManager.ShowInquiry(new InquiryData("The Masters of Qohor",
                blade.Item.Name + " goes into the fire. In " + days + " days of work the masters will hand you " + name + ", a blade of Valyrian steel forged to your own design. Their fee of " + fee
                + " gold is paid now and is not returned.", true, true, "Pay " + fee, "Not now",
                delegate { Place(blade, new List<ItemObject>(), 1, fee, days, Spec(d, name)); },
                delegate { }), true);
        }

        // ------------------------------------------------------------ spec projektu (zapis zlecenia)
        /// <summary>"szablon|klinga:skala|jelec:skala|rekojesc:skala|glowica:skala|nazwa" (nazwa base64 UTF-8; pusty element - brak czesci).</summary>
        private static string Spec(WeaponDesign d, string name)
        {
            var sb = new StringBuilder(d.Template.StringId);
            for (int i = 0; i < 4; i++)
            {
                var el = i < d.UsedPieces.Length ? d.UsedPieces[i] : null;
                sb.Append('|');
                if (el != null && el.IsValid && el.CraftingPiece != null) sb.Append(el.CraftingPiece.StringId).Append(':').Append(el.ScalePercentage.ToString(CultureInfo.InvariantCulture));
            }
            sb.Append('|').Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(name ?? "")));
            return sb.ToString();
        }

        private static string SpecName(string spec)
        {
            try
            {
                var f = (spec ?? "").Split('|');
                return f.Length >= 6 ? Encoding.UTF8.GetString(Convert.FromBase64String(f[5])) : "";
            }
            catch { return ""; }
        }

        /// <summary>Klinga valyrianska dla szablonu projektu: z listy ROT (w szablonach "Dual" odmiany _duel, gdy sa w szablonie), najblizsza dlugoscia
        /// klindze projektu przy skali projektu.</summary>
        private static CraftingPiece VsPieceFor(WeaponDesign d)
        {
            var b = d.UsedPieces[(int)CraftingPiece.PieceTypes.Blade];
            float want = b != null && b.IsValid ? b.ScaledLength : 0.9f;
            int scale = b != null && b.IsValid ? b.ScalePercentage : 100;
            bool dual = d.Template.StringId.IndexOf("Dual", StringComparison.Ordinal) >= 0 && !d.Template.StringId.EndsWith("Dual2", StringComparison.Ordinal);
            CraftingPiece best = null; float bestD = float.MaxValue;
            foreach (var id0 in VsBlades)
            {
                CraftingPiece p = null;
                try { p = MBObjectManager.Instance.GetObject<CraftingPiece>(dual ? id0 + "_duel" : id0); } catch { }
                if (p == null) try { p = MBObjectManager.Instance.GetObject<CraftingPiece>(id0); } catch { }
                if (p == null) continue;
                float len = WeaponDesignElement.CreateUsablePiece(p, scale).ScaledLength;
                float dd = Math.Abs(len - want);
                if (dd < bestD) { bestD = dd; best = p; }
            }
            return best;
        }

        /// <summary>Miecz ze spec: czesci projektu gracza, klinga valyrianska (VsPieceFor), id kolejnego wyrobu kuzni; null + powod - mistrzowie oddaja klinge.</summary>
        private static ItemObject BuildOwn(string spec, out string why)
        {
            why = null;
            var f = (spec ?? "").Split('|');
            if (f.Length < 6) { why = "zly zapis projektu"; return null; }
            var tpl = CraftingTemplate.GetTemplateFromId(f[0]);
            if (tpl == null) { why = "brak szablonu " + f[0]; return null; }
            string name = SpecName(spec);
            if (name.Length == 0) name = "Valyrian Blade";
            var els = new WeaponDesignElement[4];
            for (int i = 0; i < 4; i++)
            {
                var p = f[1 + i].Split(':');
                CraftingPiece piece = null; int sc = 100;
                if (p[0].Length > 0) { try { piece = MBObjectManager.Instance.GetObject<CraftingPiece>(p[0]); } catch { } }
                if (p.Length > 1) int.TryParse(p[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out sc);
                if (p[0].Length > 0 && piece == null) { why = "brak czesci " + p[0]; return null; }
                els[i] = piece != null ? WeaponDesignElement.CreateUsablePiece(piece, sc) : WeaponDesignElement.GetInvalidPieceForType((CraftingPiece.PieceTypes)i);
            }
            var probe = new WeaponDesign(tpl, null, els, tpl.StringId);
            var vs = VsPieceFor(probe);
            if (vs == null) { why = "brak klingi valyrianskiej ROT"; return null; }
            int bs = els[0].IsValid ? els[0].ScalePercentage : 100;
            float min = (Settings.Current != null ? Settings.Current.ValyrianGreatswordMinCm : 105f) - 1f;
            while (bs > 50 && WeaponDesignElement.CreateUsablePiece(vs, bs).ScaledLength * 100f > min) bs--;   // 1 miara jak wejscie
            els[0] = WeaponDesignElement.CreateUsablePiece(vs, bs);
            string id = NextCraftedId();
            var tname = new TextObject("{=!}" + name);
            var design = new WeaponDesign(tpl, tname, els, id);
            ItemObject item = null;
            Crafting.GenerateItem(design, tname, Hero.MainHero.Culture, null, ref item, id);
            if (item == null) { why = "gra nie zlozyla przedmiotu"; return null; }
            ItemObject.InitAsPlayerCraftedItem(ref item);
            item.IsReady = true;
            MBObjectManager.Instance.RegisterObject(item);
            // gra: projekt do _craftedItemDictionary (zapis i odtworzenie po wczytaniu); nasz handler: isCraftingOrderItem = true - bez "wykonczenia"
            CampaignEventDispatcher.Instance.OnNewItemCrafted(item, null, true);
            return item;
        }

        private static string NextCraftedId()
        {
            try
            {
                var ccb = Campaign.Current.GetCampaignBehavior<CraftingCampaignBehavior>();
                var m = AccessTools.Method(typeof(CraftingCampaignBehavior), "GetNextCraftedItemId");
                var id = ccb != null && m != null ? m.Invoke(ccb, null) as string : null;
                if (!string.IsNullOrEmpty(id) && MBObjectManager.Instance.GetObject<ItemObject>(id) == null) return id;
            }
            catch { }
            string alt;
            int k = 0;
            do { alt = "qohor_vs_" + (int)CampaignTime.Now.ToDays + "_" + k++; } while (MBObjectManager.Instance.GetObject<ItemObject>(alt) != null);
            return alt;
        }

        /// <summary>Wydanie zlecenia wlasnego projektu (tylko gracz). Blad - mistrzowie oddaja klinge (nic nie ginie).</summary>
        private static void DeliverOwn(Order o)
        {
            string why;
            ItemObject it = null;
            try { it = BuildOwn(o.Own, out why); }
            catch (Exception e) { why = "wyjatek " + e.GetType().Name; Log.Error("QohorMasters.BuildOwn", e); }
            if (it == null)
            {
                _dOwnFailed++;
                Log.Info("QohorMasters: wlasny projekt gracza nie do wykucia (" + why + ") - mistrzowie oddaja " + string.Join(", ", o.InIds.ToArray()) + "; oplata zostaje w kasie Qohoru.");
                ReturnInputs(o);
                _orders.Remove(o);
                Log.Player("The masters could not forge your design and hand back " + string.Join(" and ", o.InIds.Select(NameOf).ToArray()) + " as it was.", true);
                return;
            }
            ValyrianBlades.AddOwn(it.StringId, Hero.MainHero != null ? Hero.MainHero.StringId : "");
            MobileParty.MainParty.ItemRoster.AddToCounts(new EquipmentElement(it), 1);
            _orders.Remove(o);
            ValyrianBlades.RegistryMove(o.InIds, new[] { it.StringId });
            _dOwnMade++;
            try { CraftPopup.Show(it, null, 1); } catch { }
            Log.Player("The masters of Qohor lay " + it.Name + " before you, forged to your own design from the steel of " + string.Join(" and ", o.InIds.Select(NameOf).ToArray()) + ".");
            string blade = "?";
            try { blade = it.WeaponDesign.UsedPieces[0].CraftingPiece.StringId + " " + (ValyrianBlades.BladeCm(it)).ToString("0.0", CultureInfo.InvariantCulture) + " cm"; } catch { }
            Log.Info("Kronika unikatow: Qohor - " + string.Join(" + ", o.InIds.ToArray()) + " przekuty na wlasny projekt gracza " + it.StringId + " \"" + it.Name + "\" (szablon "
                     + (it.WeaponDesign != null ? it.WeaponDesign.Template.StringId : "?") + ", klinga " + blade + ", miary " + o.InIds.Sum(id => ValyrianBlades.Measures(ValyrianBlades.Item(id)))
                     + " -> " + ValyrianBlades.Measures(it) + ", wartosc " + it.Value + "), odbiera gracz.");
        }

        private static string OwnLine()
        {
            string s = "; wlasne projekty gracza: zlecone " + _dOwnPlaced + ", wykute " + _dOwnMade + ", oddane bez wykucia " + _dOwnFailed + ", wlasnych mieczy w rejestrze " + ValyrianBlades.OwnCount;
            _dOwnPlaced = _dOwnMade = _dOwnFailed = 0;
            return s;
        }
    }
}
