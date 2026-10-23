using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// AUTOTEST PODGLADU POSTACI W EKWIPUNKU (Jeff 23.10 20:19: "postac lezy w ekwipunku - blad z dzisiaj, wczesniej dzialalo") - tylko pod plikiem
    /// CrashScribe\armoury-ekwipunek.flag i DLL autotestu (LogGate 1/2); w zwyklej grze nic nie robi. Na mapie (bez misji, rozmowy i okien) otwiera ekwipunek
    /// gracza w kilku wariantach sprzetu bitewnego bohatera, robi zdjecie silnika po 5 s i zamyka ekran (Cancel), potem przywraca sprzet sprzed wariantu.
    /// Warianty: jak jest, bez rekawic, bez stanow (modyfikatory zdjete), bez zbroi, bez broni, bez konia, nagi. Linie "Ekwipunek (test m110): ...".
    /// Zapis gry niczego z tego nie dostaje: sprzet wraca przed nastepnym wariantem i na koncu.
    /// SKAN OBRAZKOW (Jeff 23.10: "dlaczego rekawice nie maja obrazka, zobacz, czy nie ma wiecej przedmiotow bez obrazka"): obrazek przedmiotu gra rysuje z jego
    /// modelu (ItemObject.MultiMeshName); przed pierwszym wariantem lista modeli silnika (MetaMesh.GetAllMultiMeshes) wobec modeli wszystkich przedmiotow -
    /// brak modelu albo model bez LOD = brak obrazka. Linia "Ekwipunek (test m110): skan obrazkow ..." i plik CrashScribe\zdjeciaez-obrazka-m110.tsv.
    /// </summary>
    internal static class InventoryLookTest
    {
        private const string P = "Ekwipunek (test m110): ";
        private const string FlagName = "armoury-ekwipunek.flag";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly string[] Variants = { "jak-jest", "bez-broni-0", "bez-broni-1", "bez-strzal", "bez-broni", "nagi", "bez-rekawic", "bez-stanow" };   // 20:41: zbroja, kon, rekawice i stany bez wplywu - bron
        private static int _want = -1, _variant, _stage, _stumbles;
        private static float _clock, _acc, _at, _readyFor;
        private static bool _done;
        private static Equipment _keep;

        private static bool Wanted()
        {
            if (_want >= 0) return _want == 1;
            _want = 0;
            int m = LogGate.Mode;
            if (m != 1 && m != 2) return false;
            try { if (File.Exists(Path.Combine(CrashScribeDir(), FlagName))) _want = 1; } catch { }
            return _want == 1;
        }

        private static string CrashScribeDir()
        {
            var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return Path.Combine(Path.Combine(docs, "Mount and Blade II Bannerlord"), "CrashScribe");
        }

        private static string Blocker()
        {
            if (Campaign.Current == null || Hero.MainHero == null || !Hero.MainHero.IsAlive || Hero.MainHero.IsPrisoner) return "gracz";
            if (Mission.Current != null) return "misja";
            if (!(Game.Current != null && Game.Current.GameStateManager != null && Game.Current.GameStateManager.ActiveState is MapState)) return "stan gry";
            if (InformationManager.IsAnyInquiryActive()) return "okno";
            var cm = Campaign.Current.ConversationManager;
            if (cm != null && cm.IsConversationInProgress) return "rozmowa";
            return null;
        }

        /// <summary>SubModuleMain.OnApplicationTick: co 0,5 s; bez pliku flagi - od razu wyjscie.</summary>
        internal static void AppTick(float dt)
        {
            if (_done || !Wanted()) return;
            _clock += dt;
            _acc += dt;
            if (_acc < 0.5f) return;
            float step = _acc;
            _acc = 0f;
            try { Step(step); }
            catch (Exception e)
            {
                _stumbles++;
                if (_stumbles <= 3) Log.Error("InventoryLookTest.Step", e);
                try { if (InventoryScreenHelper.GetActiveInventoryState() != null) InventoryScreenHelper.CloseScreen(true); } catch { }
                Restore();
                Log.Info(P + "przerwany wyjatkiem " + e.GetType().Name + " w wariancie " + (_variant < Variants.Length ? Variants[_variant] : "-") + ".");
                _done = true;
            }
        }

        private static void Step(float step)
        {
            if (_stage == 0)
            {
                if (_variant >= Variants.Length) { _done = true; Log.Info(P + "koniec - wariantow " + Variants.Length + ", zdjecia w CrashScribe\\zdjecia (ekw-m110-*.png), potkniecia " + _stumbles + "."); return; }
                if (Blocker() != null) { _readyFor = 0f; return; }
                _readyFor += step;
                if (_readyFor < (_variant == 0 ? 20f : 8f) || _clock < _at) return;   // 8 s na mapie miedzy wariantami - odblokowanie autotestu zdejmowalo ekran po kilku z rzedu
                var h = Hero.MainHero;
                var eq = h.BattleEquipment;
                if (_variant == 0) { Describe(h); ScanIcons(); }
                _keep = eq.Clone(false);
                Apply(eq, Variants[_variant]);
                Log.Info(P + "wariant " + Variants[_variant] + " | sprzet bitewny " + eq.CalculateEquipmentCode() + ".");
                InventoryScreenHelper.OpenScreenAsInventory();
                _stage = 1; _at = _clock + 5f;
                return;
            }
            if (_clock < _at) return;
            if (_stage == 1)
            {
                bool open = InventoryScreenHelper.GetActiveInventoryState() != null;
                string shot = "-";
                if (open)
                {
                    string dir = Path.Combine(CrashScribeDir(), "zdjecia");
                    Directory.CreateDirectory(dir);
                    string name = "ekw-m110-" + _variant + "-" + Variants[_variant] + "-" + DateTime.Now.ToString("HHmmss", Inv) + ".png";
                    TaleWorlds.Engine.Utilities.TakeScreenshot(Path.Combine(dir, name).Replace('\\', '/'));
                    shot = "zdjecia\\" + name;
                }
                Log.Info(P + "wariant " + Variants[_variant] + " | ekran otwarty " + (open ? "TAK" : "NIE") + " | zdjecie " + shot + ".");
                _stage = 2; _at = _clock + 1f;
                return;
            }
            if (InventoryScreenHelper.GetActiveInventoryState() != null) InventoryScreenHelper.CloseScreen(true);
            Restore();
            _variant++; _stage = 0; _readyFor = 0f; _at = _clock + 2f;
        }

        private static void Apply(Equipment eq, string v)
        {
            switch (v)
            {
                case "bez-rekawic": eq[EquipmentIndex.Gloves] = EquipmentElement.Invalid; break;
                case "bez-broni-0": eq[EquipmentIndex.Weapon0] = EquipmentElement.Invalid; break;
                case "bez-broni-1": eq[EquipmentIndex.Weapon1] = EquipmentElement.Invalid; break;
                case "bez-strzal": eq[EquipmentIndex.Weapon2] = EquipmentElement.Invalid; eq[EquipmentIndex.Weapon3] = EquipmentElement.Invalid; break;
                case "bez-stanow":
                    for (int i = 0; i < Equipment.EquipmentSlotLength; i++)
                    {
                        var el = eq[i];
                        if (el.Item != null && el.ItemModifier != null) eq[i] = new EquipmentElement(el.Item, null);
                    }
                    break;
                case "bez-zbroi":
                    eq[EquipmentIndex.Head] = EquipmentElement.Invalid; eq[EquipmentIndex.Body] = EquipmentElement.Invalid; eq[EquipmentIndex.Leg] = EquipmentElement.Invalid;
                    eq[EquipmentIndex.Gloves] = EquipmentElement.Invalid; eq[EquipmentIndex.Cape] = EquipmentElement.Invalid;
                    break;
                case "bez-broni": for (int i = 0; i <= (int)EquipmentIndex.ExtraWeaponSlot; i++) eq[i] = EquipmentElement.Invalid; break;
                case "bez-konia": eq[EquipmentIndex.Horse] = EquipmentElement.Invalid; eq[EquipmentIndex.HorseHarness] = EquipmentElement.Invalid; break;
                case "nagi": for (int i = 0; i < Equipment.EquipmentSlotLength; i++) eq[i] = EquipmentElement.Invalid; break;
            }
        }

        private static void Restore()
        {
            try
            {
                if (_keep != null && Hero.MainHero != null) Hero.MainHero.BattleEquipment.FillFrom(_keep, true);
                _keep = null;
            }
            catch (Exception e) { _stumbles++; Log.Error("InventoryLookTest.Restore", e); }
        }

        private static void ScanIcons()
        {
            try
            {
                var list = new List<TaleWorlds.Engine.MetaMesh>();
                TaleWorlds.Engine.MetaMesh.GetAllMultiMeshes(ref list);
                var lods = new Dictionary<string, bool>(StringComparer.Ordinal);
                foreach (var m in list)
                {
                    if (m == null) continue;
                    string n = m.GetName();
                    if (string.IsNullOrEmpty(n)) continue;
                    bool any = false;
                    try { any = m.HasAnyLods(); } catch { }
                    bool was;
                    lods[n] = lods.TryGetValue(n, out was) ? (was || any) : any;
                }
                int items = 0, noMesh = 0, noLod = 0, noName = 0;
                var rows = new List<string>();
                foreach (var it in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
                {
                    if (it == null || it.Type == ItemObject.ItemTypeEnum.Goods && it.IsTradeGood && string.IsNullOrEmpty(it.MultiMeshName)) continue;
                    items++;
                    string mesh = it.MultiMeshName;
                    string why = null;
                    bool any;
                    if (string.IsNullOrEmpty(mesh)) { noName++; why = "bez nazwy modelu"; }
                    else if (!lods.TryGetValue(mesh, out any)) { noMesh++; why = "brak modelu"; }
                    else if (!any) { noLod++; why = "model bez LOD"; }
                    if (why != null) rows.Add(it.StringId + "\t" + (it.Name != null ? it.Name.ToString() : "-") + "\t" + it.ItemType + "\t" + (mesh ?? "-") + "\t" + why
                                              + "\t" + (it.NotMerchandise ? "nie na sprzedaz" : "w handlu"));
                }
                string dir = Path.Combine(CrashScribeDir(), "zdjecia");
                Directory.CreateDirectory(dir);
                File.WriteAllLines(Path.Combine(dir, "bez-obrazka-m110.tsv"), rows.ToArray());
                var names = new List<string>(lods.Keys);
                names.Sort(StringComparer.OrdinalIgnoreCase);
                File.WriteAllLines(Path.Combine(dir, "modele-m110.txt"), names.ToArray());   // lista modeli silnika - do podstawienia brakujacych
                var head = new List<string>();
                for (int i = 0; i < rows.Count && i < 40; i++) head.Add(rows[i].Replace("\t", " | "));
                Log.Info(P + "skan obrazkow | modeli w silniku " + list.Count + " (nazw " + lods.Count + ") | przedmiotow " + items + ": brak modelu " + noMesh + ", model bez LOD " + noLod
                         + ", bez nazwy modelu " + noName + " | plik zdjecia\\bez-obrazka-m110.tsv | pierwsze: " + (head.Count > 0 ? string.Join("; ", head.ToArray()) : "brak") + ".");
            }
            catch (Exception e) { _stumbles++; Log.Error("InventoryLookTest.ScanIcons", e); }
        }

        private static void Describe(Hero h)
        {
            try
            {
                var c = h.CharacterObject;
                var bp = h.BodyProperties;
                Log.Info(P + "start | bohater " + h.Name + " | plec " + (h.IsFemale ? "K" : "M") + ", rasa " + (c != null ? c.Race.ToString(Inv) : "?") + ", wiek " + h.Age.ToString("0.#", Inv)
                         + " | cialo: waga " + bp.Weight.ToString("0.###", Inv) + ", budowa " + bp.Build.ToString("0.###", Inv) + ", wiek ciala " + bp.Age.ToString("0.#", Inv)
                         + " | klucz ciala " + bp.ToString() + " | sprzet cywilny " + h.CivilianEquipment.CalculateEquipmentCode() + " | akcje: act_inventory_idle_start "
                         + ActionIndexCache.act_inventory_idle_start.Index + ", act_tableau_hand_armor_pose " + ActionIndexCache.act_tableau_hand_armor_pose.Index + ".");
                for (int i = 0; i <= (int)EquipmentIndex.ExtraWeaponSlot; i++)
                {
                    var it = h.BattleEquipment[i].Item;
                    if (it == null) continue;
                    var sb = new System.Text.StringBuilder();
                    sb.Append(P).Append("bron ").Append(i).Append(" | ").Append(it.StringId).Append(" \"").Append(it.Name).Append("\" ").Append(it.ItemType)
                      .Append(" | wykuta ").Append(it.IsCraftedByPlayer ? "TAK" : "NIE").Append(", waga ").Append(it.Weight.ToString("0.###", Inv))
                      .Append(", model ").Append(it.MultiMeshName ?? "-").Append(", kabura ").Append(it.HolsterMeshName ?? "-")
                      .Append(", przesuniecie kabury ").Append(it.HolsterPositionShift.ToString());
                    if (it.ItemHolsters != null) sb.Append(", kabury ").Append(string.Join("/", it.ItemHolsters));
                    var pw = it.PrimaryWeapon;
                    if (pw != null) sb.Append(" | klasa ").Append(pw.WeaponClass).Append(", uzycie ").Append(pw.ItemUsage ?? "-").Append(", dlugosc ").Append(pw.WeaponLength);
                    var wd = it.WeaponDesign;
                    if (wd != null)
                    {
                        sb.Append(" | wzor ").Append(wd.Template != null ? wd.Template.StringId : "-").Append(" czesci:");
                        if (wd.UsedPieces != null)
                            foreach (var up in wd.UsedPieces)
                                if (up != null && up.CraftingPiece != null && !up.CraftingPiece.IsEmptyPiece)
                                    sb.Append(' ').Append(up.CraftingPiece.StringId).Append(" x").Append(up.ScalePercentage).Append('%');
                    }
                    Log.Info(sb.Append('.').ToString());
                }
            }
            catch (Exception e) { _stumbles++; Log.Error("InventoryLookTest.Describe", e); }
        }
    }
}
