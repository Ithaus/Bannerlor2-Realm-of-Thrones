using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// PRAWO KWATERMISTRZA. Zbrojownia DTE to szafa mundurowa wojska - a Jeff
    /// wyniosl z niej WSZYSTKO i sprzedal, po czym lucznicy stali w polu bez
    /// lukow jak piechota. Odtad kwatermistrz wydaje TYLKO NADWYZKI: bierzemy
    /// progi potrzeb wprost z DTE (EquipmentAndThresholds - ile sztuk danego
    /// typu wojsko musi miec na stanie przy tylu ludziach) i kazda proba
    /// zabrania ponizej progu zostaje odbita z komunikatem. Wkladanie do
    /// zbrojowni - zawsze wolne.
    /// </summary>
    internal static class QuartermasterLaw
    {
        private static FieldInfo _fRosters;           // InventoryLogic._rosters
        private static FieldInfo _fArmory;            // DTE ArmyArmory.Armory (static ItemRoster)
        private static FieldInfo _fThresholds;        // DTE EveryoneCampaignBehavior.EquipmentAndThresholds
        private static DateTime _lastShout = DateTime.MinValue;

        internal static ItemRoster DteArmory()
        {
            try { return _fArmory != null ? _fArmory.GetValue(null) as ItemRoster : null; }
            catch { return null; }
        }

        internal static System.Collections.IDictionary Thresholds()
        {
            try { return _fThresholds != null ? _fThresholds.GetValue(null) as System.Collections.IDictionary : null; }
            catch { return null; }
        }

        /// <summary>Ilu zolnierzy (bez bohaterow) nosi dzis barwy oddzialu.</summary>
        internal static int TroopCount()
        {
            try
            {
                int n = 0;
                var r = MobileParty.MainParty.MemberRoster;
                for (int i = 0; i < r.Count; i++)
                {
                    var el = r.GetElementCopyAtIndex(i);
                    if (el.Character != null && !el.Character.IsHero) n += el.Number;
                }
                return n;
            }
            catch { return 0; }
        }

        /// <summary>
        /// REALNY stan noszony, nie magazynowe zachcianki DTE. Formuly DTE to
        /// max(2x wojsko, wojsko+100) sztuk KAZDEGO typu (broni az 4x+400!) -
        /// stad "brakuje 127 koni" przy 32 ludziach. My liczymy PO CZLOWIEKU
        /// I PO FACHU: kto wedle wzorca nosi luk, temu liczymy luk i kolczany;
        /// kto kusze - kusze i belty; dwureczna tylko tym, ktorzy nia robia.
        /// </summary>
        internal sealed class Needs
        {
            public int Troops, Mounted, Bows, Xbows, TwoH, Pole, Shields, OneH, Thrown;
        }

        internal static Needs CountNeeds()
        {
            var n = new Needs();
            try
            {
                var r = MobileParty.MainParty.MemberRoster;
                for (int i = 0; i < r.Count; i++)
                {
                    var el = r.GetElementCopyAtIndex(i);
                    var c = el.Character;
                    if (c == null || c.IsHero) continue;
                    int k = el.Number;
                    n.Troops += k;
                    if (c.IsMounted) n.Mounted += k;
                    bool bow = false, xb = false, th = false, twoh = false, pole = false, sh = false, oneh = false;
                    try
                    {
                        var eq = c.Equipment;   // wzorzec bojowy oddzialu - kto CO ma nosic
                        for (int s = 0; s < 4; s++)
                        {
                            var it = eq[(EquipmentIndex)s].Item;
                            if (it == null) continue;
                            switch (it.ItemType)
                            {
                                case ItemObject.ItemTypeEnum.Bow: bow = true; break;
                                case ItemObject.ItemTypeEnum.Crossbow: xb = true; break;
                                case ItemObject.ItemTypeEnum.Thrown: th = true; break;
                                case ItemObject.ItemTypeEnum.TwoHandedWeapon: twoh = true; break;
                                case ItemObject.ItemTypeEnum.Polearm: pole = true; break;
                                case ItemObject.ItemTypeEnum.Shield: sh = true; break;
                                case ItemObject.ItemTypeEnum.OneHandedWeapon: oneh = true; break;
                            }
                        }
                    }
                    catch { }
                    if (bow) n.Bows += k;
                    if (xb) n.Xbows += k;
                    if (th) n.Thrown += k;
                    if (twoh) n.TwoH += k;
                    if (pole) n.Pole += k;
                    if (sh) n.Shields += k;
                    if (oneh) n.OneH += k;
                }
            }
            catch { }
            return n;
        }

        internal static int WornFor(ItemObject.ItemTypeEnum type, Needs n)
        {
            switch (type)
            {
                case ItemObject.ItemTypeEnum.HeadArmor:
                case ItemObject.ItemTypeEnum.BodyArmor:
                case ItemObject.ItemTypeEnum.LegArmor:
                case ItemObject.ItemTypeEnum.HandArmor:
                case ItemObject.ItemTypeEnum.Cape:
                    return n.Troops;                                 // sztuka na czlowieka
                case ItemObject.ItemTypeEnum.OneHandedWeapon:
                    return n.OneH;                                   // wedle wzorca oddzialu
                case ItemObject.ItemTypeEnum.Shield:
                    return n.Shields;
                case ItemObject.ItemTypeEnum.Polearm:
                    return n.Pole;
                case ItemObject.ItemTypeEnum.TwoHandedWeapon:
                    return n.TwoH;
                case ItemObject.ItemTypeEnum.Horse:
                case ItemObject.ItemTypeEnum.HorseHarness:
                    return n.Mounted;                                // kon na jezdzca, nie na papierze
                case ItemObject.ItemTypeEnum.Bow:
                    return n.Bows;
                case ItemObject.ItemTypeEnum.Crossbow:
                    return n.Xbows;
                case ItemObject.ItemTypeEnum.Arrows:
                    return n.Bows * 2;                               // kolczan i zapasowy na lucznika
                case ItemObject.ItemTypeEnum.Bolts:
                    return n.Xbows * 2;
                case ItemObject.ItemTypeEnum.Thrown:
                    return n.Thrown * 2;
                default:
                    return 0;
            }
        }

        private static int NeedFor(ItemObject.ItemTypeEnum type)
        {
            return WornFor(type, CountNeeds());
        }

        /// <summary>To samo dla escrow: ile sztuk tego typu potrzebuje kompania.</summary>
        internal static int NeedForType(ItemObject.ItemTypeEnum type)
        {
            return NeedFor(type);
        }

        internal static readonly ItemObject.ItemTypeEnum[] KitTypes =
        {
            ItemObject.ItemTypeEnum.HeadArmor, ItemObject.ItemTypeEnum.BodyArmor,
            ItemObject.ItemTypeEnum.LegArmor, ItemObject.ItemTypeEnum.HandArmor,
            ItemObject.ItemTypeEnum.Cape, ItemObject.ItemTypeEnum.OneHandedWeapon,
            ItemObject.ItemTypeEnum.TwoHandedWeapon, ItemObject.ItemTypeEnum.Polearm,
            ItemObject.ItemTypeEnum.Shield, ItemObject.ItemTypeEnum.Bow,
            ItemObject.ItemTypeEnum.Crossbow, ItemObject.ItemTypeEnum.Arrows,
            ItemObject.ItemTypeEnum.Bolts, ItemObject.ItemTypeEnum.Thrown,
            ItemObject.ItemTypeEnum.Horse, ItemObject.ItemTypeEnum.HorseHarness
        };

        /// <summary>Pelna lista brakow "Typ noszone/potrzebne" wzgledem stanu zbrojowni.</summary>
        internal static List<string> ShortageLines()
        {
            var lines = new List<string>();
            try
            {
                if (QuartermasterEscrow.Active) return lines;   // polki wlasnie schowane - nie liczyc na slepo
                var armory = DteArmory();
                if (armory == null) return lines;
                var needs = CountNeeds();
                foreach (var type in KitTypes)
                {
                    int need = WornFor(type, needs);
                    if (need <= 0) continue;
                    int have = 0;
                    for (int i = 0; i < armory.Count; i++)
                    {
                        var el = armory[i];
                        var it = el.EquipmentElement.Item;
                        if (it != null && it.ItemType == type && el.Amount > 0) have += el.Amount;
                    }
                    if (have < need) lines.Add(type + " " + have + "/" + need);
                }
            }
            catch { }
            return lines;
        }

        /// <summary>
        /// Kwatermistrz MELDUJE braki na glos - po bitwie i co rano, nie tylko
        /// w ekranie zbrojowni ("czemu ja o tym nie wiem!" - Jeff, 2026).
        /// Zwraca true, gdy bylo co meldowac.
        /// </summary>
        internal static bool ShoutShortages(string headline)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.QuartermasterShouts) return false;
                var lines = ShortageLines();
                if (lines.Count == 0) return false;
                InformationManager.DisplayMessage(new InformationMessage(headline, Colors.Red));
                for (int i = 0; i < lines.Count; i += 5)
                {
                    int n = Math.Min(5, lines.Count - i);
                    InformationManager.DisplayMessage(new InformationMessage(
                        "  " + string.Join(", ", lines.GetRange(i, n).ToArray()), Colors.Red));
                }
                return true;
            }
            catch { return false; }
        }

        /// <summary>Ile sztuk danego typu lezy jeszcze w zbrojowni.</summary>
        internal static int HaveFor(ItemRoster armory, ItemObject.ItemTypeEnum type)
        {
            int n = 0;
            try
            {
                for (int i = 0; i < armory.Count; i++)
                {
                    var el = armory[i];
                    var it = el.EquipmentElement.Item;
                    if (it != null && it.ItemType == type) n += el.Amount;
                }
            }
            catch { }
            return n;
        }

        /// <summary>
        /// ZBROJOWNIA TO NIE SMIETNIK (Jeff 27.08: "discarduje pancerz za XP
        /// i znika CALY ekwipunek"). Ekran zbrojowni DTE otwiera sie w trybie,
        /// w ktorym vanilla wlacza CanGainXpFromDiscarding - a wtedy przy
        /// zatwierdzeniu _rosters[0] (czyli CALA zbrojownia!) leci eventem
        /// OnItemsDiscardedByPlayer jako stos porzuconych rzeczy: XP liczy sie
        /// od wszystkiego, a magazyn potrafi wyparowac. Po kazdym przeliczeniu
        /// donacji gasimy flage, gdy lewa strona to zbrojownia. XP za discard
        /// dziala dalej na ekranach lupow - tam lewa strona to prawdziwy smietnik.
        /// </summary>
        public static void XpDonationsPostfix(InventoryLogic __instance)
        {
            try
            {
                if (_fRosters == null) return;
                var rosters = _fRosters.GetValue(__instance) as ItemRoster[];
                var armory = DteArmory();
                if (rosters == null || rosters.Length == 0 || armory == null || rosters[0] != armory) return;
                if (!__instance.CanGainXpFromDiscarding) return;
                var f = AccessTools.Field(typeof(InventoryLogic), "<CanGainXpFromDiscarding>k__BackingField");
                if (f != null)
                {
                    f.SetValue(__instance, false);
                    Log.Info("QuartermasterLaw: ekran zbrojowni - XP za discard zgaszone (magazyn to nie smietnik).");
                }
            }
            catch (Exception e) { Log.Error("XpDonationsPostfix", e); }
        }

        internal static bool Prefix(InventoryLogic __instance, ref TransferCommand transferCommand, ref List<TransferCommandResult> __result)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.ArmouryProtectUsed) return true;
                if (_fRosters == null || _fArmory == null) return true;

                var rosters = _fRosters.GetValue(__instance) as ItemRoster[];
                var armory = DteArmory();
                if (rosters == null || rosters.Length == 0 || armory == null || rosters[0] != armory) return true;

                // KSIEGA WKLADOW: kazdy ruch na ekranie zbrojowni ksiegowany -
                // wkladasz = twoje rosnie, wyjmujesz = twoje maleje (ekran
                // i tak pokazuje tylko twoje, wiec wyjecie cudzych niemozliwe)
                try
                {
                    var itL = transferCommand.ElementToTransfer.EquipmentElement.Item;
                    if (itL != null && itL.StringId != null)
                    {
                        int nL = Math.Max(1, transferCommand.Amount);
                        if (transferCommand.FromSide == InventoryLogic.InventorySide.OtherInventory)
                            ArmouryBehavior.StockWithdraw(itL.StringId, nL);
                        else if (transferCommand.ToSide == InventoryLogic.InventorySide.OtherInventory)
                        {
                            ArmouryBehavior.StockDeposit(itL.StringId, nL);
                            // wklad zapisany do WYMIANY BARTEROWEJ (rozliczy sie
                            // przy zamknieciu ekranu - Jeff: "wrzucam t6 luki,
                            // maja mi wydac gorsze, ktore sprzedam")
                            QuartermasterEscrow.NoteDeposit(itL, nL,
                                transferCommand.ElementToTransfer.EquipmentElement.ItemValue);
                        }
                    }
                }
                catch { }

                if (QuartermasterEscrow.Active) return true;   // lista juz pokazuje tylko wklady gracza
                if (transferCommand.FromSide != InventoryLogic.InventorySide.OtherInventory) return true;   // wkladasz - wolno zawsze

                var item = transferCommand.ElementToTransfer.EquipmentElement.Item;
                if (item == null) return true;

                int troops = TroopCount();
                int need = NeedFor(item.ItemType);
                if (need <= 0) return true;                                   // typ bez progu - wolny
                int have = HaveFor(armory, item.ItemType);
                int take = Math.Max(1, transferCommand.Amount);
                if (have - take >= need) return true;                          // zostaje zapas - wydaj

                int surplus = Math.Max(0, have - need);
                if ((DateTime.Now - _lastShout).TotalMilliseconds > 700)
                {
                    _lastShout = DateTime.Now;
                    InformationManager.DisplayMessage(new InformationMessage(
                        "Quartermaster: the men still use these - " + need + " " + item.ItemType +
                        " must stay for " + troops + " soldiers (" + (surplus > 0 ? surplus + " spare to take" : "no spares") + ").",
                        Colors.Red));
                }
                __result = new List<TransferCommandResult>();
                return false;                                                  // sprzet w uzyciu nie wychodzi
            }
            catch (Exception e) { Log.Error("QuartermasterLaw", e); return true; }
        }

        /// <summary>DLL DTE nazywa sie z numerem wersji (v1.4.7) - szukamy typu po WSZYSTKICH zestawach.</summary>
        internal static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try { var t = asm.GetType(fullName); if (t != null) return t; } catch { }
            }
            return null;
        }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var tArm = FindType("DynamicTroopEquipmentReupload.ArmyArmory");
                var tEvery = FindType("DynamicTroopEquipmentReupload.EveryoneCampaignBehavior");
                if (tArm == null || tEvery == null) { Log.Info("QuartermasterLaw: DTE nieobecne."); return; }
                _fArmory = AccessTools.Field(tArm, "Armory");
                _fThresholds = AccessTools.Field(tEvery, "EquipmentAndThresholds");
                _fRosters = AccessTools.Field(typeof(InventoryLogic), "_rosters");
                if (_fArmory == null || _fRosters == null)
                { Log.Info("QuartermasterLaw: brak pol (_rosters/Armory)."); return; }

                var m = AccessTools.Method(typeof(InventoryLogic), "TransferItem");
                if (m == null) { Log.Info("QuartermasterLaw: brak InventoryLogic.TransferItem."); return; }
                h.Patch(m, prefix: new HarmonyMethod(typeof(QuartermasterLaw), "Prefix"));

                // zbrojownia to nie smietnik: po kazdym przeliczeniu donacji
                // gasimy XP-za-discard, gdy lewa strona ekranu to magazyn DTE
                var mXp = AccessTools.Method(typeof(InventoryLogic), "InitializeXpGainFromDonations");
                if (mXp != null)
                    h.Patch(mXp, postfix: new HarmonyMethod(typeof(QuartermasterLaw), "XpDonationsPostfix"));

                // PROSCIEJ, jak chcial Jeff: przed otwarciem ekranu zbrojowni sprzet
                // noszony przez ludzi jest CHOWANY - widzisz tylko wolne nadwyzki.
                var tBeh = FindType("DynamicTroopEquipmentReupload.ArmyArmoryBehavior");
                var mOpen = tBeh != null ? AccessTools.Method(tBeh, "OpenArmoryScreen") : null;
                if (mOpen != null)
                    h.Patch(mOpen, prefix: new HarmonyMethod(typeof(QuartermasterEscrow), "HoldPrefix"));
                var tHelp = Type.GetType("Helpers.InventoryScreenHelper, TaleWorlds.CampaignSystem");
                var mClose = tHelp != null ? AccessTools.Method(tHelp, "CloseInventoryPresentation") : null;
                if (mClose != null)
                    h.Patch(mClose, postfix: new HarmonyMethod(typeof(QuartermasterEscrow), "ReleasePostfix"));
                var mClose2 = tHelp != null ? AccessTools.Method(tHelp, "CloseScreen") : null;
                if (mClose2 != null)
                    h.Patch(mClose2, postfix: new HarmonyMethod(typeof(QuartermasterEscrow), "ReleasePostfix"));
                Log.Info("QuartermasterLaw: ekran zbrojowni pokazuje TYLKO nadwyzki (open=" + (mOpen != null)
                         + ", close=" + (mClose != null) + "/" + (mClose2 != null) + ").");
            }
            catch (Exception e) { Log.Error("QuartermasterLaw.ApplyAll", e); }
        }
    }

    /// <summary>
    /// Depozyt kwatermistrza. Na czas ekranu zbrojowni sprzet NOSZONY przez
    /// zolnierzy (najlepsze sztuki kazdego typu, wedle progow DTE) jest
    /// wyjmowany z widoku, a po zamknieciu ekranu wraca na polki. Gracz widzi
    /// wylacznie wolne nadwyzki - bo luk lucznika nie lezy w magazynie,
    /// tylko wisi mu na plecach.
    /// </summary>
    internal static class QuartermasterEscrow
    {
        internal static bool Active;
        private static readonly List<KeyValuePair<EquipmentElement, int>> _held =
            new List<KeyValuePair<EquipmentElement, int>>();

        internal static void HoldPrefix() { HoldReserve(); }
        internal static void ReleasePostfix() { ReleaseReserve(); }

        internal static void HoldReserve()
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.ArmouryProtectUsed || Active) return;
                var armory = QuartermasterLaw.DteArmory();
                if (armory == null) return;
                _pendingSwaps.Clear();   // swieza sesja ekranu = swiezy rejestr wymian
                var needs = QuartermasterLaw.CountNeeds();

                // meldunek brakow PRZED schowaniem polek (pelna lista, z amunicja)
                bool anyShort = QuartermasterLaw.ShoutShortages("Quartermaster: the men go SHORT (have/need):");

                // info Jeffa: zuzyte sztuki na polkach naprawia kowal w miescie
                // (liczone PRZED depozytem - ludzie nosza najlepsze, takze zuzyte)
                if (s.TroopMendEnabled)
                {
                    int wornPieces = 0;
                    for (int i = 0; i < armory.Count; i++)
                    {
                        var el = armory[i];
                        var it = el.EquipmentElement.Item;
                        var m = el.EquipmentElement.ItemModifier;
                        if (it != null && el.Amount > 0 && !ArmouryBehavior.NoWear(it)
                            && m != null && m.PriceMultiplier < 0.999f && m.PriceMultiplier > 0f)
                            wornPieces += el.Amount;
                    }
                    if (wornPieces > 0)
                        InformationManager.DisplayMessage(new InformationMessage(
                            "Quartermaster: " + wornPieces + " pieces of the men's kit are battle-worn - a worn piece protects far less. The town smith will mend them (Work the forge).",
                            Colors.Yellow));
                }

                // NOWY LAD (Jeff 29.08): lupy 60% to WLASNOSC WOJSKA - dla oczu
                // gracza CALY skarbiec wojskowy znika; na liscie zostaja
                // WYLACZNIE sztuki z ksiegi wkladow gracza (to, co sam wrzucil,
                // moze zabrac z powrotem - reszty nie tyka)
                var allowance = new Dictionary<string, int>();
                for (int i = armory.Count - 1; i >= 0; i--)
                {
                    var el = armory[i];
                    var it = el.EquipmentElement.Item;
                    if (it == null || el.Amount <= 0) continue;
                    string id = it.StringId ?? "";
                    int allowLeft;
                    if (!allowance.TryGetValue(id, out allowLeft)) allowLeft = ArmouryBehavior.StockOf(id);
                    int visible = Math.Min(el.Amount, Math.Max(0, allowLeft));
                    allowance[id] = allowLeft - visible;
                    int hide = el.Amount - visible;
                    if (hide <= 0) continue;
                    armory.AddToCounts(el.EquipmentElement, -hide);
                    _held.Add(new KeyValuePair<EquipmentElement, int>(el.EquipmentElement, hide));
                }
                Active = _held.Count > 0;
                if (Active)
                {
                    int pieces = 0;
                    foreach (var kv in _held) pieces += kv.Value;
                    Log.Info("Kwatermistrz: skarbiec wojskowy (" + pieces + " szt.) schowany - na liscie tylko wklady gracza.");
                    InformationManager.DisplayMessage(new InformationMessage(
                        "Quartermaster: the company war-chest is the men's, not yours - your own deposits only are listed.",
                        Colors.Yellow));
                }
                if (!anyShort)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        "Quartermaster: every man carries his full kit.", Colors.Green));
                }
            }
            catch (Exception e) { Log.Error("Escrow.Hold", e); }
        }

        // wklady tej sesji ekranu - kandydaci do wymiany barterowej
        private static readonly List<KeyValuePair<ItemObject, KeyValuePair<int, int>>> _pendingSwaps =
            new List<KeyValuePair<ItemObject, KeyValuePair<int, int>>>();   // item -> (ile, wartosc szt.)

        internal static void NoteDeposit(ItemObject item, int n, int value)
        {
            try
            {
                if (item == null || n <= 0) return;
                // strzaly i belty TEZ podlegaja wymianie (Jeff 29.08: "nie
                // przyjmuje mi kwatermistrz strzal tier 6" - wykluczenie
                // amunicji zostawialo wklad bez rozliczenia i bez komunikatu)
                bool weaponish = item.HasWeaponComponent;
                if (!weaponish && !item.HasArmorComponent) return;
                _pendingSwaps.Add(new KeyValuePair<ItemObject, KeyValuePair<int, int>>(
                    item, new KeyValuePair<int, int>(n, value)));
            }
            catch { }
        }

        internal static void ReleaseReserve()
        {
            try
            {
                if (!Active && _held.Count == 0) return;
                var armory = QuartermasterLaw.DteArmory();
                if (armory != null)
                    foreach (var kv in _held) armory.AddToCounts(kv.Key, kv.Value);
                _held.Clear();
                Active = false;
                ProcessSwaps(armory);
            }
            catch (Exception e) { Log.Error("Escrow.Release", e); }
        }

        /// <summary>
        /// WYMIANA BARTEROWA (Jeff 29.08: "wrzucam luki t6, lucznicy je biora,
        /// a mnie wydaja gorsze luki, ktore sprzedam"). Za kazda wlozona sztuke
        /// LEPSZA od najgorszej wojskowej tego samego TYPU kwatermistrz wydaje
        /// graczowi te najgorsza DO SAKW - a wklad przechodzi na wlasnosc
        /// wojska (ksiega wkladow -1). Sztuki przypisane w ksiedze musztry
        /// nie sa wydawane. Bez gorszej sztuki - wklad zostaje wkladem gracza
        /// (mozna cofnac przy nastepnym otwarciu).
        /// </summary>
        /// <summary>Czy ktokolwiek w kompanii udzwignie ten przedmiot
        /// (zasada nadrzedna). Wypluwa tez najlepszy posiadany skill.</summary>
        private static bool AnyoneCanUse(ItemObject item, out int bestSkill, out string skillName)
        {
            bestSkill = 0; skillName = "";
            try
            {
                var skill = item.RelevantSkill;
                if (skill == null && item.HasArmorComponent) skill = TaleWorlds.Core.DefaultSkills.Athletics;
                if (skill == null || item.Difficulty <= 0) return true;
                skillName = skill.Name.ToString();
                var roster = TaleWorlds.CampaignSystem.Party.MobileParty.MainParty.MemberRoster;
                for (int i = 0; i < roster.Count; i++)
                {
                    var el = roster.GetElementCopyAtIndex(i);
                    if (el.Character == null || el.Character.IsHero || el.Number <= 0) continue;
                    int have = el.Character.GetSkillValue(skill);
                    if (have > bestSkill) bestSkill = have;
                }
                return bestSkill >= item.Difficulty;
            }
            catch { return true; }
        }

        private static void ProcessSwaps(ItemRoster armory)
        {
            try
            {
                if (_pendingSwaps.Count == 0) return;
                if (armory == null) { _pendingSwaps.Clear(); return; }

                int given = 0;
                foreach (var dep in _pendingSwaps)
                {
                    var newItem = dep.Key;
                    int count = dep.Value.Key;
                    int newVal = dep.Value.Value;

                    // JAK Z PANCERZEM (Jeff 29.08: "wrzucam lepszy, znika,
                    // dostaje w zamian starszy"). Zaden prog nadwyzki - wklad
                    // ZAWSZE przechodzi na wojsko i znika z polki gracza,
                    // a w zamian wyjezdzaja najgorsze sztuki tego typu, ile
                    // ich wojsko ma. Wczesniejszy prog blokowal wymiane przy
                    // brakach - a wtedy strzaly zostawaly na polce gracza
                    // i wygladalo, ze kwatermistrz ich nie przyjmuje.
                    int swapped = 0;

                    // NIKT NIE UDZWIGNIE = zadnej wymiany, wklad zostaje twoj,
                    // a kwatermistrz mowi wprost czemu (Jeff: "jak nie wymienili,
                    // to znaczy ze nie ma skilli - i dostaje komunikat")
                    int bestSkill; string skillName;
                    if (!AnyoneCanUse(newItem, out bestSkill, out skillName))
                    {
                        Log.Player("Quartermaster: no man of the company can handle the " + newItem.Name
                                   + " (needs " + skillName + " " + newItem.Difficulty + "; the best of them has "
                                   + bestSkill + "). It stays on YOUR shelf.", true);
                        continue;
                    }

                    for (int k = 0; k < count; k++)
                    {
                        // najgorsza wojskowa sztuka tego samego typu, gorsza od wkladu
                        int bestIdx = -1; int bestVal = int.MaxValue;
                        for (int i = 0; i < armory.Count; i++)
                        {
                            var el = armory[i];
                            var it = el.EquipmentElement.Item;
                            if (it == null || el.Amount <= 0 || it.ItemType != newItem.ItemType) continue;
                            // ten sam przedmiot w GORSZYM stanie tez jest wymiana
                            // (zwykle strzaly za "Balanced" - prog v<newVal nizej
                            // i tak odsieje sztuki identyczne albo lepsze)
                            var id = it.StringId ?? "";
                            if (MusterBook.IsPinnedItem(id)) continue;               // rozkaz z ksiegi swiety
                            int warPart = el.Amount - Math.Min(el.Amount, ArmouryBehavior.StockOf(id));
                            if (warPart <= 0) continue;                              // to wklady gracza
                            int v = el.EquipmentElement.ItemValue;
                            if (v >= newVal) continue;                               // wydajemy tylko GORSZE
                            if (v < bestVal) { bestVal = v; bestIdx = i; }
                        }
                        if (bestIdx < 0) break;
                        var old = armory[bestIdx].EquipmentElement;
                        // stara sztuka zostaje w magazynie, ale PRZECHODZI NA
                        // GRACZA (ksiega +1) - przy nastepnym otwarciu lezy na
                        // jego liscie ZAMIAST wkladu; wklad idzie na wojsko
                        ArmouryBehavior.StockDeposit(old.Item != null ? old.Item.StringId : "", 1);
                        ArmouryBehavior.StockWithdraw(newItem.StringId, 1);
                        given++; swapped++;
                    }

                    // COFNIETE 29.08 (Jeff: "teraz pozera kazda ilosc strzal!").
                    // Probowalem tu oddawac wojsku niewymieniona reszte wkladu -
                    // skutek byl taki, ze wszystko wrzucone znikalo z polki
                    // gracza bezpowrotnie i nie dostawal NIC w zamian.
                    // ZASADA: wymiana jest 1:1 albo jej nie ma. Czego wojsko
                    // nie odkupilo swoim starym sprzetem, ZOSTAJE WLASNOSCIA
                    // GRACZA i moze to w kazdej chwili wyjac.
                    int kept = count - swapped;
                    if (kept > 0)
                        Log.Info("Kwatermistrz: " + kept + " szt. " + newItem.StringId
                                 + " bez pary do wymiany - zostaje na polce gracza.");
                }
                _pendingSwaps.Clear();
                if (given > 0)
                {
                    Log.Info("Kwatermistrz: wymiana barterowa - " + given + " starych sztuk przeksiegowano na gracza.");
                    Log.Player("Quartermaster's exchange: the men take your better gear - " + given
                               + " of their old pieces now lie on YOUR shelf (open the armoury to take them).", true);
                }
            }
            catch (Exception e) { Log.Error("Escrow.ProcessSwaps", e); }
        }
    }
}
