using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// K1-C - ZALOGI (Jeff 09.10: "ja rowniez moge ich dozbroic na zasadzie wrzuc im lepsza zbroje, a oni wydaja mi swoja gorsza jako
    /// wymiane"; dotyczy jego druzyny I jego zalog). docs/paczki/K1-dozbrajanie.md B6 i A10.
    ///  - B6: menu miasta i zamku Twojego rodu - "Hand kit to the garrison". Ekran: lewa strona pusta, przeciagasz sztuki z sakw.
    ///    Po zamknieciu ta sama wymiana 1:1 co w zbrojowni (SwapMath.Swap) na rosterze zalogi i jej zbrojowni DTE: noszone wklady
    ///    ida do zbrojowni, za kazdy, ktory wyparl sztuke zalogi, dostajesz jej najgorsza (ze stanem z AiWear) od razu do sakw;
    ///    nienoszone wracaja do sakw. Konie i rzedy (Stajnia), unikaty / klingi lore / sprzet umarlych, nie-sprzet i sztuki z
    ///    modyfikatorem na plus (zbrojownia AI nie zna stanow na plus - stracilyby wartosc) wracaja od razu z komunikatem.
    ///    Zadnej trwalej "polki gracza" w zalodze - nic do zapisu.
    ///  - A10: zaloga w bitwie gracza walczy sprzetem ze swojej zbrojowni (rozdzielacz DTE jak dla partii lorda - DTE robi go tylko
    ///    partiom z LeaderHero). K1 (Jeff 09.10, P2 "reszta walczy bez uzbrojenia po prostu", GarrisonFightsWithArmoryOnly): TYLKO tym, co
    ///    ma, przy kazdym pokryciu - kto nie ma sztuki, walczy bez niej (bez zestawu awaryjnego DTE i bez dopelniania wzorca). Wylaczone -
    ///    jak dotad: >= GarrisonArmoryMinFillPercent slotow wzorca ze zbrojowni, ponizej we wzorcu. Straz SkillLawWard (CrashScribe) obejmie ja sama.
    ///  - B6 (Jeff 09.10 04:40): zaloga bierze sztuke gracza tylko w puste rece albo gdy jest LEPSZA od jej sztuki i ktos ja udzwignie;
    ///    gorsza, rowna albo za trudna wraca do sakw z powodem.
    ///  - sklad7b (Jeff 09.10 07:35, "jesli nie ma sprzetu, to nie ma sprzetu, nic nie dostaje z kosmosu"; "c - walcza tylko tym, co maja"):
    ///    TA SAMA regula "tylko to, co ma" w bitwie gracza dla druzyny gracza i partii lordow AI (takze towarzyszy i partii rodu gracza) -
    ///    TroopsFightWithOwnKitOnly. Druzyna gracza: bez zestawu awaryjnego DTE (ApplyEmergencyLoadout - sprzet podstawowego zolnierza kultury
    ///    z niczego). Lordowie AI: bez zestawu awaryjnego i bez dopelniania wzorca (FillEmptySlots); lord bez wpisu w zbrojowniach DTE dostaje
    ///    pusty wpis (jak zaloga - inaczej DTE puszcza go w pelnym wzorcu z niczego). Kon i rzad jezdzca AI: jego wlasny (oplacony przy awansie -
    ///    Stajnia, kon jest wlasnoscia zolnierza), ze wzorca, pozyczony na bitwe (slot tymczasowy - nie wraca do zbrojowni ani do lupu). Straz
    ///    "nic z niczego" przy samym spawnie (OwnKitSpawn z DressCode, Priority.Last - po DTE, strazach CrashScribe RealmWard/ArmourWard i
    ///    DragonUnmount): kazda sztuka slotow 0-3 i 5-9 musi miec pokrycie w zbrojowni partii, inaczej slot pusty. DressCode tych ludzi nie
    ///    ubiera. Autorozstrzygniecie gracza (lup DTE z ludzi lordow AI i zalog): bez dopelniania wzorca i ze straza na cala partie.
    ///    Bohaterowie bez zmian (DTE ich nie ubiera). Umarli (Nocny Krol) bez zmian - sprzet umarlych jest poza gospodarka.
    /// </summary>
    internal static class GarrisonKit
    {
        private static ItemRoster _screen;
        private static Settlement _screenSt;
        private static string _battle = "BRAK";

        internal static bool IsScreenRoster(ItemRoster r) { return r != null && ReferenceEquals(r, _screen); }

        private static bool MenuOn { get { var s = Settings.Current; return s != null && s.GarrisonKitMenu && s.ArmouryProtectUsed; } }

        /// <summary>
        /// Poprawki sklad7: tryb "tylko to, co ma" (K1 P2, GarrisonFightsWithArmoryOnly) tylko wtedy, gdy zbrojownie zalog naprawde trwaja: przy
        /// wylaczonych zakupach AI (AiBuysGear) DTE GarbageCollectParties kasuje je co dobe (AiGear.KeepGarrisonArmory dziala tylko przy AiGear.On),
        /// a bez GarrisonArmorySurvivesSave kazde wczytanie zostawia je puste - w obu razach wszystkie zalogi walczylyby nago. Wtedy tryb progu.
        /// </summary>
        internal static bool BareOn
        {
            get
            {
                var s = Settings.Current;
                return s != null && s.GarrisonArmoryInBattle && s.GarrisonFightsWithArmoryOnly && s.GarrisonArmorySurvivesSave && AiGear.On;
            }
        }

        /// <summary>sklad7b (Jeff 09.10 07:35): regula "tylko to, co ma" dla druzyny gracza i partii lordow AI w bitwie gracza (zalogi - BareOn).</summary>
        internal static bool OwnKitOn { get { var s = Settings.Current; return s != null && s.TroopsFightWithOwnKitOnly; } }

        internal static void Reset()
        {
            _screen = null; _screenSt = null; _hintKey = null; _hint = null; _lastLogic = null; _garAssign.Clear(); _lordAssign.Clear(); _bareDist = null; _bareMission = false;
            _spawn.Clear(); _asgParty.Clear(); _guardLog = 0; _ownAssign.Clear(); _simParty = null; _battleMission = null; ClearBattle();
        }

        private static MobileParty GarrisonOf(Settlement st)
        {
            try { return st != null && st.Town != null ? st.Town.GarrisonParty : null; } catch { return null; }
        }

        private static Dictionary<ItemObject, int> ArmoryOf(MobileParty g)
        {
            var all = AiGear.Armories();
            Dictionary<ItemObject, int> arm;
            return g != null && all != null && all.TryGetValue(g.Id, out arm) ? arm : null;
        }

        // ------------------------------------------------------------ menu (B6)
        internal static void AddMenus(CampaignGameStarter starter)
        {
            try
            {
                foreach (var menu in new[] { "town", "castle" })
                    starter.AddGameMenuOption(menu, "arm_garrison_kit_" + menu, "{=!}Hand kit to the garrison", Condition, Consequence, false, 5);
                var s = Settings.Current;
                // poprawki sklad7: osobna linia kontrolna dla LordBattleKitIsLent (nowe zachowanie spoza scalenia - domyslnie WYLACZONE do decyzji Jeffa)
                // sklad7b: regula "tylko to, co ma" dla druzyny gracza i lordow AI (TroopsFightWithOwnKitOnly); LordBattleKitIsLent dziala tylko przy niej wylaczonej
                Log.Info("GarrisonKit: latka DTE (zaloga w bitwie) " + _battle + "; menu zalogi wpiete; nic z niczego w bitwie gracza (TroopsFightWithOwnKitOnly, sklad7b): "
                         + (OwnKitOn ? "TAK - druzyna gracza bez zestawu awaryjnego DTE, lordowie AI (takze towarzysze i partie rodu) tylko tym, co maja, kon jezdzca AI pozyczony"
                                     : "nie; sloty lordow AI z niczego tylko pozyczone na bitwe (LordBattleKitIsLent): " + (s != null && s.LordBattleKitIsLent ? "WLACZONE" : "wylaczone (jak przed sklad7)"))
                         + "; zaloga tylko tym, co ma: " + (BareOn ? "TAK" : "nie (prog)") + ".");
            }
            catch (Exception e) { Log.Error("GarrisonKit.AddMenus", e); }
        }

        private static string _hintKey, _hint;

        private static bool Condition(MenuCallbackArgs args)
        {
            try
            {
                args.optionLeaveType = GameMenuOption.LeaveType.Manage;
                if (!MenuOn) return false;
                var st = Settlement.CurrentSettlement;
                var g = GarrisonOf(st);
                if (st == null || st.OwnerClan != Clan.PlayerClan || g == null || g.MemberRoster == null || g.MemberRoster.TotalRegulars <= 0) return false;
                if (AiGear.Armories() == null) return false;   // bez DTE zaloga nie ma zbrojowni
                // podpowiedz liczona raz na godzine gry i osade (warunek menu bywa wolany wiele razy)
                string key = st.StringId + "|" + (int)CampaignTime.Now.ToHours;
                if (key != _hintKey)
                {
                    var shorts = new List<string>();
                    int fill = FillPercent(g, ArmoryOf(g), shorts);
                    _hint = "Garrison kit: " + fill + "% of slots filled" + (shorts.Count > 0 ? "; short: " + string.Join(", ", shorts.ToArray()) : "")
                            + ". Drop kit on the left - they take what fills empty hands or beats their own (if they meet its skill requirement) and hand you their worst of that kind for each piece it replaced; the rest comes back to you.";
                    _hintKey = key;
                }
                args.Tooltip = new TextObject("{=!}" + _hint);
                return true;
            }
            catch { return false; }
        }

        private static void Consequence(MenuCallbackArgs args)
        {
            try
            {
                var st = Settlement.CurrentSettlement;
                if (GarrisonOf(st) == null) return;
                _screen = new ItemRoster();
                _screenSt = st;
                InventoryScreenHelper.OpenScreenAsReceiveItems(_screen, new TextObject("{=!}Garrison of " + st.Name), OnDone);
            }
            catch (Exception e) { Log.Error("GarrisonKit.Consequence", e); }
        }

        /// <summary>Zamkniecie ekranu (takze Cancel - wtedy gra cofnela rostery i lewa strona jest pusta).</summary>
        private static void OnDone()
        {
            var roster = _screen; var st = _screenSt;
            _screen = null; _screenSt = null; _hintKey = null;
            if (roster == null) return;
            try { if (Count(roster) > 0) Settle(roster, st); }
            catch (Exception e) { Log.Error("GarrisonKit.Done", e); }
            finally { ReturnAll(roster); }   // cokolwiek zostalo po lewej - wraca do sakw (nic nie znika)
        }

        private static int Count(ItemRoster r) { int n = 0; for (int i = 0; i < r.Count; i++) n += Math.Max(0, r.GetElementCopyAtIndex(i).Amount); return n; }

        private static int ReturnAll(ItemRoster r)
        {
            int n = 0;
            try
            {
                var bags = MobileParty.MainParty.ItemRoster;
                for (int i = r.Count - 1; i >= 0; i--)
                {
                    var el = r.GetElementCopyAtIndex(i);
                    if (el.Amount <= 0) continue;
                    bags.AddToCounts(el.EquipmentElement, el.Amount);
                    r.AddToCounts(el.EquipmentElement, -el.Amount);
                    n += el.Amount;
                }
            }
            catch (Exception e) { Log.Error("GarrisonKit.ReturnAll", e); }
            return n;
        }

        private static int ArmCount(Dictionary<ItemObject, int> arm) { int n = 0; if (arm != null) foreach (var v in arm.Values) n += Math.Max(0, v); return n; }

        private static void Settle(ItemRoster roster, Settlement st)
        {
            var g = GarrisonOf(st);
            var bags = MobileParty.MainParty.ItemRoster;
            if (g == null || st == null) { Log.Player("There is no garrison here to take your kit - it goes back to your bags.", true); return; }
            var arm = ArmoryOf(g);
            int before = Count(roster) + ArmCount(arm);
            // 1) odrzucone: konie i rzedy, unikaty / lore / umarli, nie-sprzet, plus
            int rHorse = 0, rUnique = 0, rPlus = 0, rOther = 0;
            var plusNames = new List<string>();
            var deposits = new List<KeyValuePair<EquipmentElement, int>>();
            for (int i = roster.Count - 1; i >= 0; i--)
            {
                var el = roster.GetElementCopyAtIndex(i);
                var it = el.EquipmentElement.Item;
                if (it == null || el.Amount <= 0) continue;
                var m = el.EquipmentElement.ItemModifier;
                bool back = true;
                if (MenPurse.HorseKind(it)) rHorse += el.Amount;
                else if (!SupplyDemand.Equipmentish(it)) rOther += el.Amount;
                else if (ArmsPricing.IsUnique(it) || QuartermasterLaw.BarredInBattle(it)) rUnique += el.Amount;
                else if (m != null && m.PriceMultiplier > 1f) { rPlus += el.Amount; if (plusNames.Count < 3) plusNames.Add(it.Name + " (" + m.Name + ")"); }
                else { back = false; deposits.Add(new KeyValuePair<EquipmentElement, int>(el.EquipmentElement, el.Amount)); }
                if (back) { bags.AddToCounts(el.EquipmentElement, el.Amount); roster.AddToCounts(el.EquipmentElement, -el.Amount); }
            }
            int rejected = rHorse + rUnique + rPlus + rOther;
            if (rPlus > 0) Log.Player("The garrison books only plain kit: " + string.Join(", ", plusNames.ToArray()) + (rPlus > plusNames.Count ? " and more" : "") + " stays with you.");
            if (rHorse + rUnique + rOther > 0)
                Log.Player((rHorse + rUnique + rOther) + " pcs went straight back to your bags - horses and harness belong to the stables, heirlooms and the dead's kit are not issued, and the rest is no war gear.");
            // 2) wymiana 1:1 per typ (SwapMath.Swap - ta sama regula co zbrojownia druzyny)
            int worn = 0, filled = 0, x = 0, keptWorse = 0, keptHard = 0, failedAdd = 0;
            var takenIds = new List<string>(); var backIds = new List<string>(); var takenNames = new List<string>(); var backNames = new List<string>();
            foreach (var type in QuartermasterLaw.KitTypes)
            {
                if (type == ItemObject.ItemTypeEnum.Horse || type == ItemObject.ItemTypeEnum.HorseHarness) continue;
                var mine = deposits.FindAll(kv => kv.Key.Item != null && kv.Key.Item.ItemType == type);
                if (mine.Count == 0) continue;
                var pieces = MenUpgrade.AiPieces(arm, type);
                SplitByCondition(g, pieces, mine);   // K1c (przeglad K1b): sztuki zalogi ze stanem z AiWear (obita nie jest "rowna" nowej)
                var byKey = new Dictionary<string, SwapMath.Piece>();
                foreach (var p in pieces) byKey[p.Key] = p;
                foreach (var kv in mine)
                {
                    SwapMath.Piece p;
                    string key = QuartermasterLaw.KeyOf(kv.Key);
                    if (byKey.TryGetValue(key, out p)) { p.Total += kv.Value; p.Own += kv.Value; }   // identyczny egzemplarz - najpierw nosza swoje
                    else { p = QuartermasterLaw.PieceOf(kv.Key, kv.Value); p.Own = kv.Value; pieces.Add(p); byKey[key] = p; }
                }
                List<CharacterObject> troops; int[] men; SkillObject skill;
                // K1 (przeglad): bron po slotach wzorca - ta sama regula co nadwyzki i zakupy brakow zalogi (wklad, ktory tu "wypelnia brak",
                // nie moze nazajutrz pojsc do kupca jako nadwyzka); pancerz jak dotad u kazdego - DTE ubiera z zbrojowni takze sloty spoza wzorca
                QuartermasterLaw.MenOf(g.MemberRoster, type, pieces, out troops, out men, out skill, QuartermasterLaw.MenPerSlot, false);
                var sw = SwapMath.Swap(men, troops.Count, QuartermasterLaw.MeetsOf(troops), pieces, true);   // wyniki w egzemplarzach: OwnWorn, Back
                // noszone wklady -> zbrojownia zalogi (ze stanem: AiWear), najpierw, zeby zwroty braly z pelnej zbrojowni
                bool failed = false;
                foreach (var p in pieces)
                {
                    if (p.OwnWorn <= 0) continue;
                    var el = QuartermasterLaw.ElOf(p);
                    if (!AiGear.AddToArmory(g, el.Item, p.OwnWorn)) { failed = true; failedAdd += p.OwnWorn; continue; }   // DTE nie przyjal - sztuka zostaje po lewej i wraca do sakw
                    roster.AddToCounts(el, -p.OwnWorn);
                    AiWear.NoteBought(g, el, p.OwnWorn);
                    worn += p.OwnWorn;
                    if (takenIds.Count < 8) takenIds.Add(p.Id + (p.Mod.Length > 0 ? "(" + p.Mod + ")" : "") + " x" + p.OwnWorn);
                    if (takenNames.Count < 3) takenNames.Add(p.OwnWorn + "x " + el.Item.Name);
                }
                arm = ArmoryOf(g);   // DTE mogl dopiero teraz zalozyc zbrojownie
                // K1 (Jeff 09.10 04:40): co zalogi nie wziela i dlaczego - K1c (przeglad K1b): liczone PRZED "failed", inaczej nienoszone sztuki
                // typu, ktorego DTE nie przyjal w calosci, szly do "the stores would not take" zamiast "nobody wanted" / wymog
                keptWorse += sw.KeptWorse; keptHard += sw.KeptHard;
                if (failed) continue;   // K1 (przeglad): wklad tego typu nie wszedl - bez zwrotow (inaczej gorsze sztuki zalogi za nic)
                filled += sw.Filled;
                // X gorszych sztuk zalogi -> do sakw gracza, kazda ze swoim stanem (najgorsza obita najpierw)
                foreach (var p in pieces)
                {
                    if (p.Back <= 0 || arm == null) continue;
                    var it = QuartermasterLaw.ElOf(p).Item;
                    int got = 0;
                    for (int k = 0; k < p.Back; k++)
                    {
                        int cnt;
                        if (!arm.TryGetValue(it, out cnt) || cnt <= 0) break;
                        var mod = AiWear.TakeCondition(g, it);
                        if (cnt > 1) arm[it] = cnt - 1; else arm.Remove(it);
                        bags.AddToCounts(new EquipmentElement(it, mod), 1);
                        got++;
                    }
                    x += got;
                    if (got <= 0) continue;
                    if (backIds.Count < 8) backIds.Add(p.Id + " x" + got);
                    if (backNames.Count < 3) backNames.Add(got + "x " + it.Name);
                }
            }
            // 3) reszta wkladow (nikt nie nosi albo nie lepsza) - z powrotem do sakw
            int returned = ReturnAll(roster);
            int after = ArmCount(ArmoryOf(g));
            // B7.1: liczba sztuk w zbrojowni i w sakwach przed i po ta sama (zwroty X wyszly ze zbrojowni do sakw)
            int armBefore = before - (worn + returned + rejected);   // lewa strona ekranu = odrzucone + noszone + zwrocone
            bool ok = after == armBefore + worn - x;
            // K1 (Jeff 09.10 04:40): gorsza, rowna albo za trudna sztuka nie jest brana - wraca do sakw z powodem (nikt jej nie chcial albo
            // nie udzwignal); reszta zwrotow to noszone wklady, ktorych zbrojownia DTE nie przyjela (failedAdd)
            int refused = Math.Max(0, returned - keptWorse - keptHard);
            string why = QuartermasterLaw.KeptWhy(keptWorse, keptHard, refused, "the stores would not take");
            if (worn > 0 || x > 0)
                Log.Player("The garrison of " + st.Name + " took " + worn + " pcs (" + string.Join(", ", takenNames.ToArray()) + "): " + filled + " filled empty hands, "
                           + x + " replaced worse kit" + (x > 0 ? " - you got the worse ones back (" + string.Join(", ", backNames.ToArray()) + ")" : "") + "."
                           + (returned > 0 ? " " + returned + " pcs went back to your bags" + why + "." : ""));
            else if (returned > 0)
                Log.Player("The garrison of " + st.Name + " took nothing - " + returned + " pcs went back to your bags" + why + ".");
            Log.Info("Wymiana zalogi " + st.Name + ": przyjete " + worn + " (" + string.Join(", ", takenIds.ToArray()) + "), braki " + filled + ", nie lepsze od ich " + keptWorse + ", za trudne " + keptHard
                     + (failedAdd > 0 ? ", DTE NIE PRZYJAL " + failedAdd + " (wrocily do sakw, bez zwrotow tego typu)" : "") + ", oddane graczowi " + x
                     + " (" + string.Join(", ", backIds.ToArray()) + "), zwrocone " + returned + ", odrzucone " + rejected + " (kon " + rHorse + "/unikat " + rUnique + "/plus " + rPlus
                     + "/nie sprzet " + rOther + "); zbrojownia " + armBefore + " -> " + after + (ok ? "" : " - NIEZMIENNIK: oczekiwano " + (armBefore + worn - x)) + ".");
        }

        /// <summary>K1c (przeglad K1b): sztuki zalogi tych id, ktore gracz wklada, dziela sie na egzemplarze wedlug stanu z AiWear (obite
        /// osobno, reszta sprawna). Dotad cala sztuka zalogi szla do porownania Better z jakoscia 1 i bez wraku - nowy egzemplarz gracza byl
        /// "rowny" obitej sztuce zalogi i wracal jako "nobody wanted", a lekko zuzyty - "gorszy" nawet od wraku zalogi.</summary>
        private static void SplitByCondition(MobileParty g, List<SwapMath.Piece> pieces, List<KeyValuePair<EquipmentElement, int>> mine)
        {
            try
            {
                var ids = new HashSet<string>();
                foreach (var kv in mine) if (kv.Key.Item != null) ids.Add(kv.Key.Item.StringId ?? "");
                for (int i = pieces.Count - 1; i >= 0; i--)
                {
                    var p = pieces[i];
                    if (!ids.Contains(p.Id)) continue;
                    var it = QuartermasterLaw.ElOf(p).Item;
                    var worn = AiWear.WornSplit(g, it);
                    if (it == null || worn.Count == 0) continue;
                    int left = p.Total;
                    var add = new List<SwapMath.Piece>();   // najpierw caly podzial, potem zmiana listy (wyjatek w polowie nie dubluje sztuk)
                    foreach (var w in worn)
                    {
                        int c = Math.Min(w.Value, left);
                        if (c <= 0) continue;
                        add.Add(QuartermasterLaw.PieceOf(new EquipmentElement(it, w.Key), c));
                        left -= c;
                    }
                    if (add.Count == 0) continue;
                    pieces.AddRange(add);
                    if (left > 0) p.Total = left; else pieces.RemoveAt(i);
                }
            }
            catch (Exception e) { Log.Error("GarrisonKit.SplitByCondition", e); }
        }

        // ------------------------------------------------------------ pokrycie slotow wzorca (A10, podpowiedz menu)
        private static string Plural(ItemObject.ItemTypeEnum t)
        {
            switch (t)
            {
                case ItemObject.ItemTypeEnum.HeadArmor: return "helmets";
                case ItemObject.ItemTypeEnum.BodyArmor: return "body armours";
                case ItemObject.ItemTypeEnum.LegArmor: return "boots";
                case ItemObject.ItemTypeEnum.HandArmor: return "gloves";
                case ItemObject.ItemTypeEnum.Cape: return "capes";
                case ItemObject.ItemTypeEnum.OneHandedWeapon: return "one-handed weapons";
                case ItemObject.ItemTypeEnum.TwoHandedWeapon: return "two-handed weapons";
                case ItemObject.ItemTypeEnum.Polearm: return "polearms";
                case ItemObject.ItemTypeEnum.Shield: return "shields";
                case ItemObject.ItemTypeEnum.Bow: return "bows";
                case ItemObject.ItemTypeEnum.Crossbow: return "crossbows";
                case ItemObject.ItemTypeEnum.Arrows: return "quivers";
                case ItemObject.ItemTypeEnum.Bolts: return "bolt cases";
                case ItemObject.ItemTypeEnum.Thrown: return "throwing stacks";
                default: return t.ToString();
            }
        }

        internal static int FillPercent(MobileParty g, Dictionary<ItemObject, int> arm, List<string> shorts) { return FillPercent(g, arm, shorts, false); }

        /// <summary>Ile procent slotow wzorca zalogi (bez koni) jej zbrojownia obsadzi dopasowaniem SwapMath; shorts - braki po typach.
        /// K1 (przeglad): pancerz tylko w slotach, ktore wzorzec ma, bron po slotach wzorca (QuartermasterLaw.MenTemplate) - dotad kazdy
        /// czlowiek "potrzebowal" 5 typow pancerza, takze plaszcza i rekawic spoza wzorca, ktorych zakupy zalogi nigdy nie kupia, wiec piechota
        /// bez nich miala najwyzej 5/7 = 71% i nigdy nie przechodzila progu 75%; battle=true - tylko zdrowi (DTE ubiera do bitwy tylko ich).</summary>
        internal static int FillPercent(MobileParty g, Dictionary<ItemObject, int> arm, List<string> shorts, bool battle)
        {
            int need = 0, used = 0;
            try
            {
                foreach (var type in QuartermasterLaw.KitTypes)
                {
                    if (type == ItemObject.ItemTypeEnum.Horse || type == ItemObject.ItemTypeEnum.HorseHarness) continue;
                    var pieces = MenUpgrade.AiPieces(arm, type);
                    List<CharacterObject> troops; int[] men; SkillObject skill;
                    QuartermasterLaw.MenOf(g.MemberRoster, type, pieces, out troops, out men, out skill, QuartermasterLaw.MenTemplate, battle);
                    if (men.Length == 0) continue;
                    int unfit = pieces.Count > 0 ? SwapMath.Fit(men, troops.Count, QuartermasterLaw.MeetsOf(troops), pieces) : men.Length;
                    need += men.Length; used += men.Length - unfit;
                    if (unfit > 0 && shorts != null && shorts.Count < 4) shorts.Add(unfit + " " + Plural(type));
                }
            }
            catch (Exception e) { Log.Error("GarrisonKit.FillPercent", e); }
            return need > 0 ? (int)((long)used * 100 / need) : 100;
        }

        // ------------------------------------------------------------ zaloga w bitwie (A10)
        private static FieldInfo _fInit, _fDist, _fSides;
        private static ConstructorInfo _ctor;
        private static MethodInfo _run, _sanitize;
        private static WeakReference _lastLogic;
        // K1 (przeglad): przydzialy (DTE Assignment) z rozdzielaczy zalog w biezacej misji - ich sloty wypelnione przez FillEmptySlots oznaczamy
        // jako tymczasowe (patrz FillPostfix)
        private static readonly HashSet<object> _garAssign = new HashSet<object>();
        // sklad7 (zestaw z niczego DTE, decyzja B i "nic z niczego"): przydzialy rozdzielaczy partii lordow AI w biezacej misji - sloty, ktore DTE
        // wypelnia im z niczego (FillEmptySlots: sztuka wzorca albo losowa tego typu i tieru), oznaczamy jako tymczasowe (LordBattleKitIsLent):
        // czlowiek walczy tym jak dotad, ale po bitwie DTE nie dopisuje tych sztuk do zbrojowni lorda (dotad mennica - a z niej lup gracza)
        private static readonly HashSet<object> _lordAssign = new HashSet<object>();
        private static FieldInfo _fAssigns, _fEq;
        private static MethodInfo _markTemp, _getRef, _setEq, _isTemp;
        private static int _stumbles, _guardLog;
        private static bool _tempErr;
        // K1 (Jeff 09.10, P2: "reszta walczy bez uzbrojenia po prostu"): GarrisonFightsWithArmoryOnly - rozdzielacz zalogi bez progu, bez
        // zestawu awaryjnego DTE (_bareDist na czas RunAsync) i bez dopelniania wzorca przy spawnie (_bareMission)
        private static object _bareDist;
        private static bool _bareMission;
        private static string _emerg = "BRAK";
        // K1c (przeglad K1b): ekwipunki przydzialow zalog w trybie "tylko to, co ma" (Assignment.Equipment - ta sama referencja, ktora DTE daje
        // agentowi jako AgentOverridenSpawnEquipment). DressCode ich nie ubiera - dotad kazdy pusty slot pancerza dostawal sztuke wzorca
        // z niczego (na klonie, bez oznaczenia jako tymczasowa), a po bitwie DTE oddawal ja do zbrojowni zalogi (mennica).
        // sklad7b: to samo dla przydzialow druzyny gracza i lordow AI (OwnKitOn) - ekwipunek -> (przydzial, rodzaj, partia) dla strazy przy spawnie.
        // _asgParty: przydzial -> Id partii (zalogi, sklad7b: takze lorda AI) dla strazy "nic z niczego" (Guard).
        private sealed class SpawnInfo
        {
            internal readonly object Asg; internal readonly int Kind; internal readonly MBGUID Party;   // Kind: 0 druzyna gracza, 1 lord AI, 2 zaloga
            internal SpawnInfo(object asg, int kind, MBGUID party) { Asg = asg; Kind = kind; Party = party; }
        }
        private static readonly Dictionary<object, SpawnInfo> _spawn = new Dictionary<object, SpawnInfo>(RefEq.I);
        private static readonly Dictionary<object, MBGUID> _asgParty = new Dictionary<object, MBGUID>(RefEq.I);
        // sklad7b: przydzialy partii lordow AI (takze towarzyszy i partii rodu gracza) w trybie "tylko to, co ma" (FillPrefix: bez dopelniania wzorca)
        private static readonly HashSet<object> _ownAssign = new HashSet<object>();
        private static FieldInfo _fDistParty, _fDistMission;
        private static MethodInfo _canMount;
        private static bool _mountsOk;
        // sklad7b: autorozstrzygniecie gracza (EveryoneCampaignBehavior.DistributePlayerSimulationLoot -> CreateMapEventAssignments): partia, ktorej
        // przydzialy licza lup tylko z tego, co ma (null - poza tym wywolaniem albo partia bez reguly)
        private static MobileParty _simParty;
        private static int _simSkipped;
        // sklad7b: liczniki jednej bitwy gracza (linia "Bitwa gracza (sklad7b)" na koniec misji)
        private static WeakReference _battleMission;
        private static int _bPlayerMen, _bPlayerMiss, _bLordMen, _bLordMiss, _bGarMen, _bGarMiss, _bGuardP, _bGuardL, _bGuardG, _bNoEmergP, _bNoEmergL, _bHorses, _bNoEntry, _bUndead;

        private static void ClearBattle()
        {
            _bPlayerMen = _bPlayerMiss = _bLordMen = _bLordMiss = _bGarMen = _bGarMiss = _bGuardP = _bGuardL = _bGuardG = _bNoEmergP = _bNoEmergL = _bHorses = _bNoEntry = _bUndead = 0;
        }

        private static bool BattleTouched()
        {
            return _bPlayerMen + _bLordMen + _bGarMen + _bGuardP + _bGuardL + _bGuardG + _bNoEmergP + _bNoEmergL + _bHorses + _bNoEntry + _bUndead > 0;
        }

        private sealed class RefEq : IEqualityComparer<object>
        {
            internal static readonly RefEq I = new RefEq();
            bool IEqualityComparer<object>.Equals(object a, object b) { return ReferenceEquals(a, b); }
            public int GetHashCode(object o) { return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(o); }
        }


        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var tLogic = QuartermasterLaw.FindType("DynamicTroopEquipmentReupload.DynamicTroopMissionLogic");
                var tDist = QuartermasterLaw.FindType("DynamicTroopEquipmentReupload.PartyEquipmentDistributor");
                var tEvery = QuartermasterLaw.FindType("DynamicTroopEquipmentReupload.EveryoneCampaignBehavior");
                if (tLogic == null || tDist == null || tEvery == null) { _battle = "BRAK (DTE nieobecne)"; return; }
                _fInit = AccessTools.Field(tLogic, "_areDistributorsInitialized");
                _fDist = AccessTools.Field(tLogic, "Distributors");
                _fSides = AccessTools.Field(tLogic, "PartyBattleSides");
                _ctor = tDist.GetConstructor(new[] { typeof(Mission), typeof(MobileParty), typeof(Dictionary<ItemObject, int>) });
                _run = AccessTools.Method(tDist, "RunAsync");
                _sanitize = AccessTools.Method(tEvery, "SanitizePartyArmory");
                var m = AccessTools.Method(tLogic, "TryInitializeDistributors");
                if (_fInit == null || _fDist == null || _fSides == null || _ctor == null || _run == null || _sanitize == null || m == null)
                { _battle = "BRAK (zmienione DTE)"; return; }
                // K1 (przeglad): bez oznaczania slotow "z niczego" rozdzielacz zalogi bylby mennica (patrz FillPostfix) - wtedy wcale go nie wpinamy
                var tAssign = QuartermasterLaw.FindType("DynamicTroopEquipmentReupload.Assignment");
                var mFill = tAssign != null ? AccessTools.Method(tAssign, "FillEmptySlots") : null;
                _fAssigns = AccessTools.Field(tDist, "Assignments");
                _fEq = tAssign != null ? AccessTools.Field(tAssign, "Equipment") : null;
                _markTemp = tAssign != null ? AccessTools.Method(tAssign, "MarkSlotAsTemporary") : null;
                _setEq = tAssign != null ? AccessTools.Method(tAssign, "SetEquipment") : null;          // K1c: straz "nic z niczego"
                _isTemp = tAssign != null ? AccessTools.Method(tAssign, "IsTemporarySlot") : null;
                if (mFill == null || _fAssigns == null || _fEq == null || _markTemp == null || _setEq == null || _isTemp == null)
                { _battle = "BRAK (zmienione DTE - brak oznaczania slotow tymczasowych)"; return; }
                // K1c (przeglad K1b): postfiks PO strazach CrashScribe (ArmourWard podmienia sztuke na sztuke wzorca) - straz "nic z niczego"
                // w FillPostfix widzi koncowy przydzial
                h.Patch(mFill, prefix: new HarmonyMethod(typeof(GarrisonKit), nameof(FillPrefix)),
                        postfix: new HarmonyMethod(typeof(GarrisonKit), nameof(FillPostfix)) { priority = Priority.Last });
                h.Patch(m, postfix: new HarmonyMethod(typeof(GarrisonKit), nameof(DistributorsPostfix)));
                // K1 (P2): zestaw awaryjny DTE (sprzet podstawowego zolnierza kultury w puste sloty, z niczego) nie dla rozdzielaczy zalog
                _getRef = AccessTools.PropertyGetter(tAssign, "ReferenceEquipment");
                var mEmerg = AccessTools.Method(tDist, "ApplyEmergencyLoadout");
                if (mEmerg != null) { h.Patch(mEmerg, prefix: new HarmonyMethod(typeof(GarrisonKit), nameof(EmergencyPrefix))); _emerg = "wpieta"; }
                // sklad7b: rozdzielacz -> partia i misja (zestaw awaryjny dla druzyny gracza i lordow AI wolany w TryInitializeDistributors, zanim
                // DistributorsPostfix zobaczy rozdzielacze), kon jezdzca AI tylko tam, gdzie DTE wpuszcza konie (CanUseMountEquipment)
                _fDistParty = AccessTools.Field(tDist, "_party");
                _fDistMission = AccessTools.Field(tDist, "_mission");
                _canMount = AccessTools.PropertyGetter(tAssign, "CanUseMountEquipment");
                string sim = "BRAK", end = "BRAK";
                try
                {
                    // sklad7b: autorozstrzygniecie gracza - lup DTE z ludzi lordow AI i zalog liczony z ich przydzialow (z dopelnieniem wzorca z niczego)
                    var mSim = AccessTools.Method(tDist, "CreateMapEventAssignments");
                    if (mSim != null)
                    {
                        h.Patch(mSim, prefix: new HarmonyMethod(typeof(GarrisonKit), nameof(SimPrefix)), postfix: new HarmonyMethod(typeof(GarrisonKit), nameof(SimPostfix)),
                                finalizer: new HarmonyMethod(typeof(GarrisonKit), nameof(SimFinalizer)));
                        sim = "wpiety";
                    }
                }
                catch (Exception e) { Log.Error("GarrisonKit.ApplyAll(sim)", e); }
                try
                {
                    var mEnd = AccessTools.Method(tLogic, "OnEndMission");
                    if (mEnd != null) { h.Patch(mEnd, postfix: new HarmonyMethod(typeof(GarrisonKit), nameof(EndMissionPostfix))); end = "wpieta"; }
                }
                catch (Exception e) { Log.Error("GarrisonKit.ApplyAll(end)", e); }
                _battle = "wpieta, bez zestawu awaryjnego DTE " + _emerg + ", straz nic z niczego wpieta; sloty lordow AI z niczego tylko na bitwe (sklad7) wpiete"
                          + "; nic z niczego (sklad7b): rozdzielacz->partia " + (_fDistParty != null && _fDistMission != null ? "jest" : "BRAK (druzyna gracza i lordowie z zestawem awaryjnym)")
                          + ", kon jezdzca AI " + (_canMount != null ? "wedlug przydzialu i misji" : "tylko wedlug misji") + ", lup autorozstrzygniecia " + sim + ", linia bitwy " + end;
            }
            catch (Exception e) { _battle = "BRAK (" + e.Message + ")"; Log.Error("GarrisonKit.ApplyAll", e); }
        }

        /// <summary>Po TryInitializeDistributors DTE (bitwa gracza): rozdzielacz ze zbrojowni dla kazdej zalogi bioracej udzial. K1 (Jeff 09.10,
        /// P2): przy GarrisonFightsWithArmoryOnly zawsze, bez progu - zaloga walczy TYLKO tym, co ma w zbrojowni, kto nie ma sztuki, walczy
        /// bez niej (bez zestawu awaryjnego DTE i bez dopelniania wzorca); wylaczone - jak dotad: ponizej progu slotow wzorca zaloga walczy
        /// we wzorcu za darmo. Raz na misje - DTE wola TryInitializeDistributors przy kazdym spawnie.
        /// K1c (przeglad K1b): zaloga bez wpisu w DTE PartyArmories dostaje pusty wpis (jak w PartyEquipmentDistributor.Spawn) - bez niego
        /// SpawnAgentPatch odrzuca partie (MobileParty.IsValid: bez wpisu i bez LeaderHero = false), ludzie szli w PELNYM wzorcu za darmo,
        /// a po bitwie DTE oddawal ich caly sprzet do zbrojowni przez nasz rozdzielacz (mennica). Rozdzielacz i strona bitwy tylko przy wpisie.
        /// sklad7b: przy OwnKitOn - przydzialy druzyny gracza i lordow AI w trybie "tylko to, co ma" (OwnAssignments, przed zalogami);
        /// zalogi umarlych (Nocny Krol) bez naszego rozdzielacza - jak przed K1 (sprzet umarlych poza gospodarka).</summary>
        public static void DistributorsPostfix(object __instance)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || (!s.GarrisonArmoryInBattle && !s.LordBattleKitIsLent && !s.TroopsFightWithOwnKitOnly) || __instance == null) return;
                if (_lastLogic != null && ReferenceEquals(_lastLogic.Target, __instance)) return;
                if (!(bool)_fInit.GetValue(__instance)) return;
                _lastLogic = new WeakReference(__instance);
                _garAssign.Clear(); _spawn.Clear(); _asgParty.Clear(); _lordAssign.Clear(); _ownAssign.Clear();   // nowa misja - przydzialy poprzedniej nieaktualne
                var me = MapEvent.PlayerMapEvent;
                var dists = _fDist.GetValue(__instance) as IDictionary;
                var sides = _fSides.GetValue(__instance) as IDictionary;
                var mb = __instance as MissionBehavior;
                var mission = mb != null ? mb.Mission : null;
                if (me == null || dists == null || sides == null || mission == null) return;
                BattleFor(mission);
                _mountsOk = MountsOk(mission);
                // sklad7b: jedna regula dla druzyny gracza i lordow AI (OwnKitOn); "tylko pozyczone" (LordBattleKitIsLent) tylko bez niej
                if (OwnKitOn) OwnAssignments(dists, sides, me, mission);   // PRZED rozdzielaczami zalog - te dochodza nizej
                else if (s.LordBattleKitIsLent) LordAssignments(dists);
                if (!s.GarrisonArmoryInBattle) return;
                int min = Math.Max(0, Math.Min(100, s.GarrisonArmoryMinFillPercent));
                bool bare = BareOn;
                if (s.GarrisonFightsWithArmoryOnly && !bare)
                    Log.Info("Zaloga w bitwie: tryb 'tylko to, co ma' WYLACZONY na te bitwe - zbrojownie zalog nie przetrwaja (Ai Buys Gear " + (AiGear.On ? "wl." : "WYL.")
                             + ", Garrison Armory Survives Save " + (s.GarrisonArmorySurvivesSave ? "wl." : "WYL.") + ") - prog " + min + "% slotow wzorca, ponizej we wzorcu.");
                _bareMission = bare;
                foreach (var pb in me.InvolvedParties)
                {
                    // K1 (przeglad): osobny try na zaloge - potkniecie jednej nie zatrzymuje rozdzielaczy pozostalych
                    try
                    {
                        var g = pb != null ? pb.MobileParty : null;
                        if (g == null || !g.IsGarrison || !g.IsActive || g.LeaderHero != null || dists.Contains(g.Id)) continue;
                        var st = g.CurrentSettlement ?? g.HomeSettlement;
                        string name = st != null ? st.Name.ToString() : g.StringId;
                        if (Undead.Party(g))
                        {
                            // sklad7b: umarli poza gospodarka (bez zakupow, zoldu, sprzedazy) - bez naszego rozdzielacza, jak przed K1
                            _bUndead++;
                            Log.Info("Zaloga w bitwie: " + name + " - umarli (Nocny Krol), bez rozdzielacza zbrojowni (jak przed K1).");
                            continue;
                        }
                        var arm = _sanitize.Invoke(null, new object[] { g.Id }) as Dictionary<ItemObject, int>;
                        int fill = arm != null ? FillPercent(g, arm, null, true) : 0;   // tylko zdrowi - jak DTE
                        if (!bare && (arm == null || arm.Count == 0 || fill < min))
                        {
                            Log.Info("Zaloga w bitwie: " + name + " zbrojownia " + fill + "% (prog " + min + ") - we wzorcu (za malo).");
                            MenUpgrade.NoteBattle(false);
                            continue;
                        }
                        // K1c: P2 - bez zbrojowni nikt nie ma sztuki, walcza bez niej; pusty wpis w DTE, zeby spawn szedl przez rozdzielacz
                        var all = AiGear.Armories();
                        bool made = false;
                        if (arm == null && all != null)
                        {
                            Dictionary<ItemObject, int> ex;
                            if (all.TryGetValue(g.Id, out ex) && ex != null) arm = ex;
                            else { arm = new Dictionary<ItemObject, int>(); all[g.Id] = arm; made = true; }
                        }
                        if (arm == null || all == null || !all.ContainsKey(g.Id))
                        {
                            // bezpiecznik: bez wpisu DTE spawn i tak pojdzie wzorcem - wtedy bez rozdzielacza i bez zwrotow (jak przed K1)
                            Log.Info("Zaloga w bitwie: " + name + " - BRAK wpisu w zbrojowniach DTE, we wzorcu (bez rozdzielacza i bez zwrotow).");
                            MenUpgrade.NoteBattle(false);
                            continue;
                        }
                        var d = _ctor.Invoke(new object[] { mission, g, arm });
                        _bareDist = bare ? d : null;
                        try { _run.Invoke(d, null); }
                        finally { _bareDist = null; }
                        var al = _fAssigns.GetValue(d) as IEnumerable;
                        if (al != null)
                            foreach (var a in al)
                            {
                                if (a == null) continue;
                                _garAssign.Add(a);
                                _asgParty[a] = g.Id;
                                if (bare) { var eq = _fEq.GetValue(a); if (eq != null) _spawn[eq] = new SpawnInfo(a, 2, g.Id); }
                            }
                        dists[g.Id] = d;
                        sides[g.Id] = pb.Side;
                        Log.Info("Zaloga w bitwie: " + name + " zbrojownia " + fill + "%" + (made ? " (bez wpisu DTE - pusty wpis zalozony)" : "")
                                 + (bare ? " - walczy tylko tym, co ma (kto nie ma sztuki, walczy bez niej)." : " (prog " + min + ") - walczy tym, co ma."));
                        MenUpgrade.NoteBattle(true);
                    }
                    catch (Exception e) { if (++_stumbles <= 3) Log.Error("GarrisonKit.DistributorsPostfix(zaloga)", e); }
                }
            }
            catch (Exception e) { Log.Error("GarrisonKit.DistributorsPostfix", e); }
        }

        /// <summary>sklad7: przydzialy rozdzielaczy DTE partii lordow AI (wszystkie poza druzyna gracza; zalog DTE nie tworzy - robi je DistributorsPostfix).</summary>
        private static void LordAssignments(IDictionary dists)
        {
            try
            {
                var main = MobileParty.MainParty;
                foreach (DictionaryEntry e in dists)
                {
                    if (e.Value == null) continue;
                    if (main != null && e.Key is MBGUID && (MBGUID)e.Key == main.Id) continue;   // druzyna gracza: FillEmptySlots jej nie dotyczy
                    var al = _fAssigns.GetValue(e.Value) as IEnumerable;
                    if (al == null) continue;
                    foreach (var a in al) if (a != null) _lordAssign.Add(a);
                }
            }
            catch (Exception ex) { if (++_stumbles <= 3) Log.Error("GarrisonKit.LordAssignments", ex); }
        }

        /// <summary>sklad7b: partia lorda AI w bitwie gracza, ktora DTE ubiera z jej zbrojowni (jak DTE: wodz-bohater, nie karawana, wies, milicja,
        /// bandyci ani patrol); takze towarzysze i partie rodu gracza; bez umarlych (Nocny Krol - sprzet umarlych poza gospodarka).</summary>
        private static bool LordInBattle(MobileParty mp)
        {
            return mp != null && mp.IsActive && mp.IsLordParty && mp.LeaderHero != null && mp.LeaderHero != Hero.MainHero && !mp.IsMainParty
                   && !mp.IsGarrison && !mp.IsCaravan && !mp.IsVillager && !mp.IsMilitia && !mp.IsBandit && !mp.IsPatrolParty && !Undead.Party(mp);
        }

        /// <summary>sklad7b (Jeff 09.10 07:35, (1) i (2c)): przydzialy rozdzielaczy DTE druzyny gracza i partii lordow AI (takze towarzyszy i partii
        /// rodu gracza) w trybie "tylko to, co ma" - lordowie: FillPrefix bez dopelniania wzorca; wszyscy: straz przy spawnie (OwnKitSpawn) i DressCode
        /// ich nie ubiera. Najpierw partie lordow bez wpisu w zbrojowniach DTE (nowa partia po LevyGold - bez darmowego kompletu - dopoki nic nie
        /// kupi): DTE nie robi im rozdzielacza, wiec ich ludzie szli w PELNYM wzorcu z niczego; teraz pusty wpis i rozdzielacz, jak zalogi (K1c) -
        /// nic nie maja, walcza bez. Zalogi dochodza pozniej (DistributorsPostfix), umarli bez zmian.</summary>
        private static void OwnAssignments(IDictionary dists, IDictionary sides, MapEvent me, Mission mission)
        {
            var main = MobileParty.MainParty;
            var byId = new Dictionary<MBGUID, MobileParty>();
            try
            {
                var all = AiGear.Armories();
                foreach (var pb in me.InvolvedParties)
                {
                    try
                    {
                        var mp = pb != null ? pb.MobileParty : null;
                        if (mp == null) continue;
                        byId[mp.Id] = mp;
                        if (all == null || mp == main || !LordInBattle(mp) || dists.Contains(mp.Id)) continue;
                        Dictionary<ItemObject, int> arm;
                        if (!all.TryGetValue(mp.Id, out arm) || arm == null) { arm = new Dictionary<ItemObject, int>(); all[mp.Id] = arm; }
                        var d = _ctor.Invoke(new object[] { mission, mp, arm });
                        _run.Invoke(d, null);   // zestaw awaryjny DTE - EmergencyPrefix (OwnKitOn) go nie da
                        dists[mp.Id] = d;
                        sides[mp.Id] = pb.Side;
                        _bNoEntry++;
                        Log.Info("Bitwa gracza: " + mp.Name + " - bez wpisu w zbrojowniach DTE (dotad pelny wzorzec z niczego) - pusty wpis, walcza tym, co maja.");
                    }
                    catch (Exception e) { if (++_stumbles <= 3) Log.Error("GarrisonKit.OwnAssignments(lord bez wpisu)", e); }
                }
            }
            catch (Exception e) { if (++_stumbles <= 3) Log.Error("GarrisonKit.OwnAssignments(wpisy)", e); }
            try
            {
                foreach (DictionaryEntry e in dists)
                {
                    if (e.Value == null || !(e.Key is MBGUID)) continue;
                    var id = (MBGUID)e.Key;
                    bool isMain = main != null && id == main.Id;
                    MobileParty mp;
                    if (isMain) mp = main;
                    else if (!byId.TryGetValue(id, out mp)) mp = _fDistParty != null ? _fDistParty.GetValue(e.Value) as MobileParty : null;
                    if (mp == null || mp.IsGarrison) continue;   // zalogi - DistributorsPostfix (BareOn)
                    if (Undead.Party(mp)) { _bUndead++; continue; }   // umarli - jak dotad (DTE dopelnia wzorzec)
                    var al = _fAssigns.GetValue(e.Value) as IEnumerable;
                    if (al == null) continue;
                    foreach (var a in al)
                    {
                        if (a == null) continue;
                        if (!isMain) { _ownAssign.Add(a); _asgParty[a] = id; }
                        var eq = _fEq.GetValue(a);
                        if (eq != null) _spawn[eq] = new SpawnInfo(a, isMain ? 0 : 1, id);
                    }
                }
            }
            catch (Exception ex) { if (++_stumbles <= 3) Log.Error("GarrisonKit.OwnAssignments", ex); }
        }

        /// <summary>sklad7b: czy DTE w tej misji wpuszcza konie (jak FillEmptySlots: nie morze, kryjowka ani oblezenie).</summary>
        private static bool MountsOk(Mission mission)
        {
            try
            {
                if (mission == null || mission.IsNavalBattle || mission.IsNavalRaidBattle || HideoutAlarm.IsHideout(mission)) return false;
                foreach (var b in mission.MissionBehaviors)
                {
                    var n = b != null ? b.GetType().Name : null;
                    if (n == "MissionSiegeEnginesLogic") return false;
                }
                return true;
            }
            catch { return false; }
        }

        /// <summary>sklad7b: liczniki nalezace do tej misji (poprzednia bez konca misji - jej linia teraz).</summary>
        private static void BattleFor(Mission mission)
        {
            if (mission == null) return;
            if (_battleMission != null && ReferenceEquals(_battleMission.Target, mission)) return;
            if (BattleTouched()) BattleLine("poprzednia misja bez konca");
            ClearBattle();
            _battleMission = new WeakReference(mission);
        }

        /// <summary>sklad7b: linia jednej bitwy gracza (do autotestu: ile slotow wzorca zostalo pustych - gracz / lordowie AI / zalogi).</summary>
        private static void BattleLine(string why)
        {
            Log.Info("Bitwa gracza (sklad7b, nic z niczego; " + why + "): sloty wzorca bez sztuki (walcza bez) - gracz " + _bPlayerMiss + " (ludzi " + _bPlayerMen
                     + "), lordowie AI " + _bLordMiss + " (ludzi " + _bLordMen + "), zalogi " + _bGarMiss + " (ludzi " + _bGarMen + "); straz 'nic z niczego' zatrzymala - gracz "
                     + _bGuardP + ", lordowie " + _bGuardL + ", zalogi " + _bGuardG + "; bez zestawu awaryjnego DTE - druzyna gracza " + _bNoEmergP + ", partie lordow " + _bNoEmergL
                     + "; konie i rzedy jezdzcow AI pozyczone na bitwe " + _bHorses + "; lordowie bez wpisu DTE (pusty wpis) " + _bNoEntry + "; umarli bez zmian " + _bUndead + ".");
        }

        /// <summary>sklad7b: po DTE DynamicTroopMissionLogic.OnEndMission (zwroty juz policzone) - linia bitwy i koniec przydzialow tej misji.</summary>
        public static void EndMissionPostfix()
        {
            try { if (BattleTouched()) BattleLine("koniec misji"); }
            catch (Exception e) { Log.Error("GarrisonKit.EndMission", e); }
            ClearBattle(); _battleMission = null;
            _spawn.Clear(); _ownAssign.Clear(); _garAssign.Clear(); _lordAssign.Clear(); _asgParty.Clear();
        }

        /// <summary>K1 (przeglad): DTE przy spawnie czlowieka partii innej niz gracza wola Assignment.FillEmptySlots - kazdy pusty slot dostaje
        /// sztuke wzorca albo losowa sztuke tego typu, tieru i kultury, NIE oznaczona jako tymczasowa. Spawn zdejmuje ze zbrojowni tylko to,
        /// co w niej jest, a po bitwie ReturnEquipmentFromAgents i ItemsToRecover oddaja do zbrojowni partii wszystko nietymczasowe - sprzet
        /// z niczego, ktory potem szedl do kupca (dawniej K1 GarrisonDay, od sklad7 tygodniowe nadwyzki GarrisonArmory.SellWeek) albo graczowi jako zwrot B6. Dla przydzialow rozdzielaczy ZALOG zapamietujemy
        /// puste sloty przed i oznaczamy wypelnione po (MarkSlotAsTemporary - tak, jak DTE robi to sam w ApplyEmergencyLoadout): czlowiek walczy
        /// tym, ale ani spawn tego nie zdejmuje, ani zwrot nie oddaje. Lordowie AI - to samo tylko przy LordBattleKitIsLent (sklad7, _lordAssign;
        /// domyslnie WYLACZONE do decyzji Jeffa - bez tego mennica DTE sprzed K1 zostaje).
        /// K1 (P2, tryb "tylko to, co ma"): oryginal nie biegnie - pusty slot zostaje pusty. Stan (__state): [0..11] sloty puste przed,
        /// [12] tryb "tylko to, co ma"; null - nie przydzial zalogi. K1c (przeglad K1b): wyjatek zawodzi w strone braku (oryginal nie biegnie),
        /// nie mennicy - dotad catch puszczal oryginal bez stanu, wiec wypelnione sloty nie byly oznaczane jako tymczasowe.
        /// sklad7b: lordowie AI przy OwnKitOn (_ownAssign) - jak zaloga w trybie "tylko to, co ma" ([14] = lord AI); kon i rzad jezdzca AI
        /// (zaloga i lord) - jego wlasny, pozyczony (LendMount). Autorozstrzygniecie gracza (_simParty): oryginal nie biegnie (lup tylko z tego,
        /// co mieli; straz na cala partie - SimPostfix).</summary>
        public static bool FillPrefix(object __instance, out bool[] __state)
        {
            __state = null;
            bool bare = false, lord = false, own = false;
            try
            {
                if (__instance == null) return true;
                if (_garAssign.Count > 0 && _garAssign.Contains(__instance)) bare = _bareMission;
                else if (_ownAssign.Count > 0 && _ownAssign.Contains(__instance)) { bare = true; own = true; }   // sklad7b: lord AI - tylko to, co ma
                else if (_lordAssign.Count > 0 && _lordAssign.Contains(__instance)) lord = true;   // sklad7: lord AI - oryginal biegnie, sloty pozyczone
                else if (_simParty != null) { _simSkipped++; return false; }   // sklad7b: autorozstrzygniecie gracza - bez dopelniania wzorca z niczego
                else return true;
            }
            catch { return true; }   // nie wiadomo, czy przydzial zalogi - jak DTE
            var st = new bool[15];
            st[12] = bare;
            st[13] = lord;
            st[14] = own;
            __state = st;
            try
            {
                var eq = _fEq.GetValue(__instance) as Equipment;
                if (eq == null) return lord;   // sklad7: lord AI - jak DTE (bez oznaczania), zaloga - w strone braku
                if (bare)
                {
                    // K1 (Jeff 09.10, P2): zaloga walczy tylko tym, co ma - pusty slot zostaje pusty (bez sztuki wzorca i bez losowej z niczego);
                    // licznik: sloty broni i pancerza, ktore wzorzec ma, a czlowiek nie (tylko log)
                    if (!own)
                    {
                        int bareN = 0;
                        var rf = _getRef != null ? _getRef.Invoke(__instance, null) as Equipment : null;
                        if (rf != null)
                            foreach (int i in BareSlots)
                            {
                                var e = eq[(EquipmentIndex)i]; var r = rf[(EquipmentIndex)i];
                                if ((e.IsEmpty || e.Item == null) && !r.IsEmpty && r.Item != null) bareN++;
                            }
                        MenUpgrade.NoteBareSlots(bareN);
                    }
                    // sklad7b: kon jezdzca AI jest jego (Stajnia: oplacony przy awansie) - pozyczony na bitwe, nie do zbrojowni
                    try { _bHorses += LendMount(__instance, eq); }
                    catch (Exception ex) { if (++_stumbles <= 3) Log.Error("GarrisonKit.LendMount", ex); }
                }
                else
                    for (int i = 0; i < 12; i++) { var e = eq[(EquipmentIndex)i]; st[i] = e.IsEmpty || e.Item == null; }
            }
            catch { for (int i = 0; i < 12; i++) st[i] = false; return lord; }   // sklad7: lord AI - wyjatek nie oslabia jego ludzi (jak przed sklad7)
            return !bare;
        }

        private static readonly int[] BareSlots = { 0, 1, 2, 3, 5, 6, 7, 8, 9 };   // bron 0-3, pancerz i plaszcz (bez proporca i konia)

        /// <summary>K1 (Jeff 09.10, P2): DTE po rozdaniu zbrojowni wklada w puste sloty sprzet podstawowego zolnierza kultury (ApplyEmergencyLoadout,
        /// z niczego, jako tymczasowy). Rozdzielacz zalogi przy GarrisonFightsWithArmoryOnly go nie dostaje - kto nie ma sztuki, walczy bez niej.
        /// sklad7b (Jeff 09.10 07:35, (1) "nic nie dostaje z kosmosu" i (2c)): przy OwnKitOn takze rozdzielacze druzyny gracza i partii lordow AI
        /// w misji (wolane w TryInitializeDistributors DTE, przed DistributorsPostfix - stad rozpoznanie po polach rozdzielacza). Bez misji
        /// (autorozstrzygniecie - lup DTE) jak dotad: tam zestaw awaryjny jest tymczasowy i do lupu nie idzie. Zalogi w trybie progu i umarli - jak dotad.</summary>
        public static bool EmergencyPrefix(object __instance)
        {
            if (_bareDist != null && ReferenceEquals(__instance, _bareDist)) { MenUpgrade.NoteNoEmergency(); return false; }
            try
            {
                if (__instance == null || !OwnKitOn || _fDistParty == null || _fDistMission == null) return true;
                var mission = _fDistMission.GetValue(__instance) as Mission;
                if (mission == null) return true;
                var mp = _fDistParty.GetValue(__instance) as MobileParty;
                if (mp == null || mp.IsGarrison || Undead.Party(mp)) return true;
                BattleFor(mission);
                if (mp.IsMainParty) _bNoEmergP++; else _bNoEmergL++;
                return false;
            }
            catch { return true; }
        }

        /// <summary>sklad7b: kon i rzad jezdzca AI (lord, zaloga) w trybie "tylko to, co ma" - jego wlasny (oplacony przy awansie - Stajnia; konny
        /// ochotnik ma swojego - paczka 160), a zbrojownie AI koni nie trzymaja (ColdStart, 171, MenPurse - "bez koni"). Gdy DTE nie dal mu konia
        /// ze zbrojowni, slot dostaje konia (i rzad tej samej rodziny) wzorca jako TYMCZASOWY: jedzie na nim, spawn go nie zdejmuje, a zwrot i lup
        /// DTE go nie oddaja (dotad FillEmptySlots dawal go jako zwykla sztuke - kazda bitwa dopisywala konie do zbrojowni lorda). Tylko tam,
        /// gdzie DTE wpuszcza konie (jak FillEmptySlots: nie morze, kryjowka ani oblezenie). Zwraca liczbe slotow.</summary>
        private static int LendMount(object asg, Equipment eq)
        {
            if (!_mountsOk || asg == null || eq == null || _getRef == null || _setEq == null || _markTemp == null) return 0;
            if (_canMount != null && !(bool)_canMount.Invoke(asg, null)) return 0;
            var rf = _getRef.Invoke(asg, null) as Equipment;
            if (rf == null) return 0;
            int n = 0;
            var horse = eq[EquipmentIndex.Horse];
            var rh = rf[EquipmentIndex.Horse];
            if ((horse.IsEmpty || horse.Item == null) && !rh.IsEmpty && rh.Item != null)
            {
                _setEq.Invoke(asg, new object[] { EquipmentIndex.Horse, new EquipmentElement(rh.Item) });
                _markTemp.Invoke(asg, new object[] { EquipmentIndex.Horse, rh.Item });
                horse = eq[EquipmentIndex.Horse];
                n++;
            }
            var harn = eq[EquipmentIndex.HorseHarness];
            var rr = rf[EquipmentIndex.HorseHarness];
            if (!horse.IsEmpty && horse.Item != null && (harn.IsEmpty || harn.Item == null) && !rr.IsEmpty && rr.Item != null && SameFamily(horse.Item, rr.Item))
            {
                _setEq.Invoke(asg, new object[] { EquipmentIndex.HorseHarness, new EquipmentElement(rr.Item) });
                _markTemp.Invoke(asg, new object[] { EquipmentIndex.HorseHarness, rr.Item });
                n++;
            }
            return n;
        }

        /// <summary>Rzad tej samej rodziny co wierzchowiec (DragonUnmount, crash 14.09: inna rodzina = AccessViolation w AddMountMesh).</summary>
        private static bool SameFamily(ItemObject horse, ItemObject harness)
        {
            try
            {
                var mc = horse != null && horse.HorseComponent != null ? horse.HorseComponent.Monster : null;
                return mc != null && harness != null && harness.ArmorComponent != null && harness.ArmorComponent.FamilyType == mc.FamilyType;
            }
            catch { return false; }
        }

        /// <summary>Po FillEmptySlots (Priority.Last - po strazach CrashScribe): sloty wypelnione z niczego jako tymczasowe (nie w trybie
        /// "tylko to, co ma" - tam oryginal nie biegl) i straz "nic z niczego" (Guard).</summary>
        public static void FillPostfix(object __instance, bool[] __state)
        {
            if (__state == null || __state.Length < 13) return;
            try
            {
                var eq = _fEq.GetValue(__instance) as Equipment;
                if (eq == null) return;
                bool bare = __state[12];
                int n = 0;
                if (!bare)
                    for (int i = 0; i < 12; i++)
                    {
                        if (!__state[i]) continue;
                        var e = eq[(EquipmentIndex)i];
                        if (e.IsEmpty || e.Item == null) continue;
                        _markTemp.Invoke(__instance, new object[] { (EquipmentIndex)i, e.Item });
                        n++;
                    }
                if (__state.Length > 13 && __state[13]) { MenUpgrade.NoteLordLent(n); return; }   // sklad7: lord AI - tylko pozyczone sloty, bez strazy zalogi
                // sklad7b: lord AI w trybie "tylko to, co ma" - ta sama straz co zaloga (jego zbrojownia, _asgParty), licznik bitwy osobno
                bool own = __state.Length > 14 && __state[14];
                int g = Guard(__instance, eq, bare);
                if (own) _bGuardL += g;
                else
                {
                    MenUpgrade.NoteTempSlots(n);
                    MenUpgrade.NoteNothingSlots(g);
                    if (bare) _bGuardG += g;
                }
            }
            catch (Exception ex) { if (!_tempErr) { _tempErr = true; Log.Error("GarrisonKit.FillPostfix", ex); } }
        }

        /// <summary>K1c (przeglad K1b) - STRAZ "NIC Z NICZEGO" dla przydzialow zalog, tuz przed spawnem (spawn zdejmuje ze zbrojowni zaraz potem,
        /// w SpawnAgentPatch.Postfix, wiec zbrojownia jest tu pomniejszona o wczesniej wystawionych ludzi). Straze CrashScribe podmieniaja sztuke
        /// zbrojowni na sztuke WZORCA z niczego, bez oznaczenia jako tymczasowa (RealmWard - obca zza Waskiego Morza, ArmourWard - unikat, sprzet
        /// umarlych, pancerz ponad Atletyke); po bitwie DTE oddawal taka sztuke do zbrojowni zalogi (mennica), a w trybie "tylko to, co ma"
        /// zaloga walczyla czyms, czego nie ma. Kazda nietymczasowa sztuka slotow 0-3 i 5-9 musi miec pokrycie w zbrojowni zalogi (licznik
        /// w obrebie czlowieka); bez pokrycia: tryb "tylko to, co ma" - slot pusty, inaczej - slot tymczasowy (walczy tym, spawn nie zdejmuje,
        /// zwrot nie oddaje). Kon i rzad (10-11) poza straza - do zbrojowni zalogi nie trafiaja, a dopelnienie FillEmptySlots jest wyzej
        /// oznaczane jako tymczasowe. Zwraca liczbe zatrzymanych slotow.</summary>
        private static int Guard(object asg, Equipment eq, bool bare)
        {
            MBGUID gid;
            if (asg == null || eq == null || !_asgParty.TryGetValue(asg, out gid)) return 0;
            var all = AiGear.Armories();
            Dictionary<ItemObject, int> arm = null;
            if (all != null) all.TryGetValue(gid, out arm);
            Dictionary<string, int> used = null;
            int n = 0;
            foreach (int i in BareSlots)
            {
                var e = eq[(EquipmentIndex)i]; var it = e.Item;
                if (e.IsEmpty || it == null) continue;
                if ((bool)_isTemp.Invoke(asg, new object[] { (EquipmentIndex)i, it })) continue;
                string id = it.StringId ?? "";
                if (used == null) used = new Dictionary<string, int>();
                int u; used.TryGetValue(id, out u);
                if (u < Have(arm, it)) { used[id] = u + 1; continue; }
                if (bare) _setEq.Invoke(asg, new object[] { (EquipmentIndex)i, default(EquipmentElement) });
                else _markTemp.Invoke(asg, new object[] { (EquipmentIndex)i, it });
                n++;
                if (_guardLog < 10)
                {
                    _guardLog++;
                    Log.Info("Bitwa gracza: straz nic z niczego - " + id + " (slot " + i + ", " + (_ownAssign.Contains(asg) ? "lord AI" : "zaloga") + ") bez pokrycia w zbrojowni partii - "
                             + (bare ? "slot pusty (walczy bez tej sztuki)." : "slot tymczasowy (nie wraca do zbrojowni)."));
                }
            }
            return n;
        }

        /// <summary>Ile sztuk przedmiotu jest w zbrojowni (klucze DTE sa rozwiazane po id - najpierw ta sama sztuka, potem to samo id).</summary>
        private static int Have(Dictionary<ItemObject, int> arm, ItemObject it)
        {
            if (arm == null || it == null) return 0;
            int c;
            if (arm.TryGetValue(it, out c)) return Math.Max(0, c);
            string id = it.StringId;
            if (string.IsNullOrEmpty(id)) return 0;
            foreach (var kv in arm) if (kv.Key != null && kv.Key.StringId == id) return Math.Max(0, kv.Value);
            return 0;
        }

        // ------------------------------------------------------------ sklad7b: straz przy spawnie, autorozstrzygniecie
        /// <summary>sklad7b: wolane z DressCode.Prefix (Mission.SpawnAgent, Priority.Last - po prefiksie DTE, FillEmptySlots ze strazami CrashScribe
        /// i DragonUnmount). Ekwipunek przydzialu w trybie "tylko to, co ma" (druzyna gracza, lord AI, zaloga): ostatnia straz "nic z niczego"
        /// (SpawnGuard) i liczniki bitwy. true - DressCode go nie ubiera (kto nie ma sztuki, walczy bez niej).</summary>
        internal static bool OwnKitSpawn(Equipment eq)
        {
            SpawnInfo si;
            if (eq == null || _spawn.Count == 0 || !_spawn.TryGetValue(eq, out si) || si == null) return false;
            try
            {
                int g = SpawnGuard(si, eq);
                int miss = Missing(si.Asg, eq);
                if (si.Kind == 0) { _bPlayerMen++; _bPlayerMiss += miss; _bGuardP += g; }
                else if (si.Kind == 1) { _bLordMen++; _bLordMiss += miss; _bGuardL += g; }
                else { _bGarMen++; _bGarMiss += miss; _bGuardG += g; }
            }
            catch (Exception e) { if (++_stumbles <= 3) Log.Error("GarrisonKit.OwnKitSpawn", e); }
            return true;
        }

        /// <summary>sklad7b - STRAZ "NIC Z NICZEGO" PRZY SPAWNIE, jedna dla druzyny gracza, lordow AI i zalog (lordowie i zalogi maja tez Guard
        /// w FillPostfix; tu dochodza podmiany DragonUnmount - legenda, sprzet olbrzymow, bron i pancerz ponad umiejetnosc, kon ponad Jazde - robione
        /// na tym samym ekwipunku z niczego, a u druzyny gracza RealmWard - sztuka wzorca w miejsce obcej zza Waskiego Morza). Zbrojownia jest tu
        /// pomniejszona o wczesniej wystawionych ludzi (DTE zdejmuje sztuki w postfiksie spawnu). Sloty 0-3 i 5-9: kazda nietymczasowa sztuka
        /// musi miec pokrycie (licznik w obrebie czlowieka), inaczej slot pusty. Kon i rzad: druzyna gracza - tylko ze zbrojowni (bez pokrycia
        /// pieszo, rzad schodzi z koniem); jezdziec AI - jego wlasny: bez pokrycia slot tymczasowy (pozyczony). Zbrojownia nieznana - bez strazy
        /// (nie rozbieramy ludzi przez blad odczytu). Zwraca liczbe zatrzymanych slotow 0-9.</summary>
        private static int SpawnGuard(SpawnInfo si, Equipment eq)
        {
            if (_isTemp == null || _setEq == null || _markTemp == null) return 0;
            Func<ItemObject, int> have;
            if (si.Kind == 0)
            {
                var stock = MainStock(eq);
                if (stock == null) return 0;
                have = it => { int c; return it != null && stock.TryGetValue(it.StringId ?? "", out c) ? c : 0; };
            }
            else
            {
                var all = AiGear.Armories();
                Dictionary<ItemObject, int> arm = null;
                if (all == null || !all.TryGetValue(si.Party, out arm) || arm == null) return 0;
                have = it => Have(arm, it);
            }
            var used = new Dictionary<string, int>();
            int n = 0;
            for (int k = 0; k < 12; k++)
            {
                if (k == 4) continue;   // proporzec
                var idx = (EquipmentIndex)k;
                var e = eq[idx]; var it = e.Item;
                if (e.IsEmpty || it == null) continue;
                if ((bool)_isTemp.Invoke(si.Asg, new object[] { idx, it })) continue;
                string id = it.StringId ?? "";
                int u; used.TryGetValue(id, out u);
                if (u < have(it)) { used[id] = u + 1; continue; }
                if (k >= 10 && si.Kind != 0)
                {
                    _markTemp.Invoke(si.Asg, new object[] { idx, it });   // kon/rzad jezdzca AI - jego wlasny, pozyczony
                    _bHorses++;
                    continue;
                }
                _setEq.Invoke(si.Asg, new object[] { idx, default(EquipmentElement) });
                if (k == 10)
                {
                    var hr = eq[EquipmentIndex.HorseHarness];
                    if (!hr.IsEmpty && hr.Item != null) _setEq.Invoke(si.Asg, new object[] { EquipmentIndex.HorseHarness, default(EquipmentElement) });
                }
                if (k < 10) n++;
                if (_guardLog < 10)
                {
                    _guardLog++;
                    Log.Info("Bitwa gracza: straz nic z niczego przy spawnie - " + id + " (slot " + k + ", " + (si.Kind == 0 ? "druzyna gracza" : si.Kind == 1 ? "lord AI" : "zaloga")
                             + ") bez pokrycia w zbrojowni - slot pusty.");
                }
            }
            return n;
        }

        /// <summary>sklad7b: ile sztuk (po StringId, jak DTE ArmyArmory.AssignEquipment) ma zbrojownia druzyny gracza z tych, ktore czlowiek nosi;
        /// null - zbrojownia nieznana.</summary>
        private static Dictionary<string, int> MainStock(Equipment eq)
        {
            var r = QuartermasterLaw.DteArmory();
            if (r == null) return null;
            var want = new HashSet<string>();
            for (int k = 0; k < 12; k++) { if (k == 4) continue; var it = eq[(EquipmentIndex)k].Item; if (it != null) want.Add(it.StringId ?? ""); }
            var d = new Dictionary<string, int>();
            if (want.Count == 0) return d;
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i); var it = el.EquipmentElement.Item;
                if (it == null || el.Amount <= 0) continue;
                string id = it.StringId ?? "";
                if (!want.Contains(id)) continue;
                int c; d.TryGetValue(id, out c); d[id] = c + el.Amount;
            }
            return d;
        }

        /// <summary>sklad7b (linia bitwy): ile sztuk wzorca czlowiek nie ma - pancerz po slotach (5-9), bron po liczbie (0-3: DTE uklada bron
        /// w innej kolejnosci niz wzorzec, a jego dodatki zajmuja wolne sloty).</summary>
        private static int Missing(object asg, Equipment eq)
        {
            var rf = _getRef != null && asg != null ? _getRef.Invoke(asg, null) as Equipment : null;
            if (rf == null || eq == null) return 0;
            int miss = 0, rw = 0, ew = 0;
            for (int i = 0; i < 4; i++)
            {
                var r = rf[(EquipmentIndex)i]; var e = eq[(EquipmentIndex)i];
                if (!r.IsEmpty && r.Item != null) rw++;
                if (!e.IsEmpty && e.Item != null) ew++;
            }
            for (int i = 5; i <= 9; i++)
            {
                var r = rf[(EquipmentIndex)i]; var e = eq[(EquipmentIndex)i];
                if (!r.IsEmpty && r.Item != null && (e.IsEmpty || e.Item == null)) miss++;
            }
            return miss + Math.Max(0, rw - ew);
        }

        /// <summary>sklad7b: autorozstrzygniecie gracza - DTE EveryoneCampaignBehavior.DistributePlayerSimulationLoot liczy lup z poleglych
        /// pokonanych z przydzialow PartyEquipmentDistributor.CreateMapEventAssignments, a te dopelnia FillEmptySlots (sztuka wzorca albo losowa
        /// z niczego, nie tymczasowa - szla do Twojego lupu). Partia lorda AI (OwnKitOn) i zaloga (BareOn) - lup tylko z tego, co mieli.</summary>
        public static void SimPrefix(MobileParty __0)
        {
            _simParty = null; _simSkipped = 0;
            try { if (SimRule(__0)) _simParty = __0; } catch { _simParty = null; }
        }

        private static bool SimRule(MobileParty mp)
        {
            if (mp == null || mp.IsMainParty || Undead.Party(mp)) return false;
            return mp.IsGarrison ? BareOn : OwnKitOn;
        }

        /// <summary>sklad7b: po CreateMapEventAssignments - straz "nic z niczego" na cala partie (bez spawnu nic nie schodzi ze zbrojowni, wiec licznik
        /// idzie po wszystkich przydzialach): podmiany strazy CrashScribe (RealmWard, ArmourWard) ponad stan zbrojowni - slot pusty.</summary>
        public static void SimPostfix(MobileParty __0, Dictionary<ItemObject, int> __2, object __result)
        {
            var mp = _simParty;
            if (mp == null || __0 == null || !ReferenceEquals(mp, __0) || _isTemp == null || _setEq == null) return;
            try
            {
                var dict = __result as IDictionary;
                if (dict == null) return;
                var total = new Dictionary<string, int>();
                int men = 0, n = 0;
                foreach (var a in dict.Values)
                {
                    if (a == null) continue;
                    var eq = _fEq.GetValue(a) as Equipment;
                    if (eq == null) continue;
                    men++;
                    foreach (int i in BareSlots)
                    {
                        var idx = (EquipmentIndex)i; var e = eq[idx]; var it = e.Item;
                        if (e.IsEmpty || it == null) continue;
                        if ((bool)_isTemp.Invoke(a, new object[] { idx, it })) continue;
                        string id = it.StringId ?? "";
                        int t; total.TryGetValue(id, out t);
                        if (t < Have(__2, it)) { total[id] = t + 1; continue; }
                        _setEq.Invoke(a, new object[] { idx, default(EquipmentElement) });
                        n++;
                    }
                }
                if (men > 0)
                    Log.Info("Autorozstrzygniecie gracza (sklad7b, lup): " + mp.Name + " - ludzi " + men + ", bez dopelniania wzorca z niczego " + _simSkipped
                             + ", straz zatrzymala " + n + " szt. (lup tylko z ich zbrojowni).");
            }
            catch (Exception e) { if (++_stumbles <= 3) Log.Error("GarrisonKit.SimPostfix", e); }
        }

        public static Exception SimFinalizer(Exception __exception) { _simParty = null; _simSkipped = 0; return __exception; }

        // ------------------------------------------------------------ K1c: zapis zbrojowni zalog - sklad7: przeniesione do GarrisonArmory
        // K1c zapisywal zbrojownie zalog pod kluczem "arm_garrisonarmory" (format osada,przedmiot,ile;), a 171 pod TYM SAMYM kluczem (format v1|) - dwa
        // zapisy jednego stanu. Zostal jeden: GarrisonArmory.Export/Import/Restore (takze zbrojownie partii lordow bez wodza, stary zapis - dorobek
        // startowy C9a); dawny format K1c czyta GarrisonArmory.LegacyK1c.
    }
}
