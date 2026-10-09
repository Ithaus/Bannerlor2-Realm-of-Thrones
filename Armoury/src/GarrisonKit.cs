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
    /// </summary>
    internal static class GarrisonKit
    {
        private static ItemRoster _screen;
        private static Settlement _screenSt;
        private static string _battle = "BRAK";

        internal static bool IsScreenRoster(ItemRoster r) { return r != null && ReferenceEquals(r, _screen); }

        private static bool MenuOn { get { var s = Settings.Current; return s != null && s.GarrisonKitMenu && s.ArmouryProtectUsed; } }

        internal static void Reset() { _screen = null; _screenSt = null; _hintKey = null; _hint = null; _lastLogic = null; _garAssign.Clear(); _bareDist = null; _bareMission = false; _bareEq.Clear(); _asgParty.Clear(); _pendingArm = null; _guardLog = 0; }

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
                Log.Info("GarrisonKit: latka DTE (zaloga w bitwie) " + _battle + "; menu zalogi wpiete.");
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
        // _asgParty: przydzial -> Id zalogi dla strazy "nic z niczego" (Guard).
        private static readonly HashSet<object> _bareEq = new HashSet<object>(RefEq.I);
        private static readonly Dictionary<object, MBGUID> _asgParty = new Dictionary<object, MBGUID>(RefEq.I);

        private sealed class RefEq : IEqualityComparer<object>
        {
            internal static readonly RefEq I = new RefEq();
            bool IEqualityComparer<object>.Equals(object a, object b) { return ReferenceEquals(a, b); }
            public int GetHashCode(object o) { return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(o); }
        }

        /// <summary>K1c (przeglad K1b): ekwipunek przydzialu zalogi w trybie "tylko to, co ma" - DressCode go nie ubiera (P2: kto nie ma
        /// sztuki, walczy bez niej).</summary>
        internal static bool IsBareEquipment(Equipment eq) { return eq != null && _bareEq.Count > 0 && _bareEq.Contains(eq); }

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
                _battle = "wpieta, bez zestawu awaryjnego DTE " + _emerg + ", straz nic z niczego wpieta";
            }
            catch (Exception e) { _battle = "BRAK (" + e.Message + ")"; Log.Error("GarrisonKit.ApplyAll", e); }
        }

        /// <summary>Po TryInitializeDistributors DTE (bitwa gracza): rozdzielacz ze zbrojowni dla kazdej zalogi bioracej udzial. K1 (Jeff 09.10,
        /// P2): przy GarrisonFightsWithArmoryOnly zawsze, bez progu - zaloga walczy TYLKO tym, co ma w zbrojowni, kto nie ma sztuki, walczy
        /// bez niej (bez zestawu awaryjnego DTE i bez dopelniania wzorca); wylaczone - jak dotad: ponizej progu slotow wzorca zaloga walczy
        /// we wzorcu za darmo. Raz na misje - DTE wola TryInitializeDistributors przy kazdym spawnie.
        /// K1c (przeglad K1b): zaloga bez wpisu w DTE PartyArmories dostaje pusty wpis (jak w PartyEquipmentDistributor.Spawn) - bez niego
        /// SpawnAgentPatch odrzuca partie (MobileParty.IsValid: bez wpisu i bez LeaderHero = false), ludzie szli w PELNYM wzorcu za darmo,
        /// a po bitwie DTE oddawal ich caly sprzet do zbrojowni przez nasz rozdzielacz (mennica). Rozdzielacz i strona bitwy tylko przy wpisie.</summary>
        public static void DistributorsPostfix(object __instance)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.GarrisonArmoryInBattle || __instance == null) return;
                if (_lastLogic != null && ReferenceEquals(_lastLogic.Target, __instance)) return;
                if (!(bool)_fInit.GetValue(__instance)) return;
                _lastLogic = new WeakReference(__instance);
                _garAssign.Clear(); _bareEq.Clear(); _asgParty.Clear();   // nowa misja - przydzialy poprzedniej nieaktualne
                var me = MapEvent.PlayerMapEvent;
                var dists = _fDist.GetValue(__instance) as IDictionary;
                var sides = _fSides.GetValue(__instance) as IDictionary;
                var mb = __instance as MissionBehavior;
                var mission = mb != null ? mb.Mission : null;
                if (me == null || dists == null || sides == null || mission == null) return;
                int min = Math.Max(0, Math.Min(100, s.GarrisonArmoryMinFillPercent));
                bool bare = s.GarrisonFightsWithArmoryOnly;
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
                                if (bare) { var eq = _fEq.GetValue(a); if (eq != null) _bareEq.Add(eq); }
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

        /// <summary>K1 (przeglad): DTE przy spawnie czlowieka partii innej niz gracza wola Assignment.FillEmptySlots - kazdy pusty slot dostaje
        /// sztuke wzorca albo losowa sztuke tego typu, tieru i kultury, NIE oznaczona jako tymczasowa. Spawn zdejmuje ze zbrojowni tylko to,
        /// co w niej jest, a po bitwie ReturnEquipmentFromAgents i ItemsToRecover oddaja do zbrojowni partii wszystko nietymczasowe - sprzet
        /// z niczego, ktory potem szedl do kupca (GarrisonDay) albo graczowi jako zwrot B6. Dla przydzialow rozdzielaczy ZALOG zapamietujemy
        /// puste sloty przed i oznaczamy wypelnione po (MarkSlotAsTemporary - tak, jak DTE robi to sam w ApplyEmergencyLoadout): czlowiek walczy
        /// tym, ale ani spawn tego nie zdejmuje, ani zwrot nie oddaje. Lordowie AI - bez zmian (mennica DTE sprzed K1, poza ta paczka).
        /// K1 (P2, tryb "tylko to, co ma"): oryginal nie biegnie - pusty slot zostaje pusty. Stan (__state): [0..11] sloty puste przed,
        /// [12] tryb "tylko to, co ma"; null - nie przydzial zalogi. K1c (przeglad K1b): wyjatek zawodzi w strone braku (oryginal nie biegnie),
        /// nie mennicy - dotad catch puszczal oryginal bez stanu, wiec wypelnione sloty nie byly oznaczane jako tymczasowe.</summary>
        public static bool FillPrefix(object __instance, out bool[] __state)
        {
            __state = null;
            bool bare = false;
            try
            {
                if (_garAssign.Count == 0 || __instance == null || !_garAssign.Contains(__instance)) return true;
                bare = _bareMission;
            }
            catch { return true; }   // nie wiadomo, czy przydzial zalogi - jak DTE
            var st = new bool[13];
            st[12] = bare;
            __state = st;
            try
            {
                var eq = _fEq.GetValue(__instance) as Equipment;
                if (eq == null) return false;
                if (bare)
                {
                    // K1 (Jeff 09.10, P2): zaloga walczy tylko tym, co ma - pusty slot zostaje pusty (bez sztuki wzorca i bez losowej z niczego);
                    // licznik: sloty broni i pancerza, ktore wzorzec ma, a czlowiek nie (tylko log)
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
                else
                    for (int i = 0; i < 12; i++) { var e = eq[(EquipmentIndex)i]; st[i] = e.IsEmpty || e.Item == null; }
            }
            catch { for (int i = 0; i < 12; i++) st[i] = false; return false; }
            return !bare;
        }

        private static readonly int[] BareSlots = { 0, 1, 2, 3, 5, 6, 7, 8, 9 };   // bron 0-3, pancerz i plaszcz (bez proporca i konia)

        /// <summary>K1 (Jeff 09.10, P2): DTE po rozdaniu zbrojowni wklada w puste sloty sprzet podstawowego zolnierza kultury (ApplyEmergencyLoadout,
        /// z niczego, jako tymczasowy). Rozdzielacz zalogi przy GarrisonFightsWithArmoryOnly go nie dostaje - kto nie ma sztuki, walczy bez niej.</summary>
        public static bool EmergencyPrefix(object __instance)
        {
            if (_bareDist == null || !ReferenceEquals(__instance, _bareDist)) return true;
            MenUpgrade.NoteNoEmergency();
            return false;
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
                MenUpgrade.NoteTempSlots(n);
                MenUpgrade.NoteNothingSlots(Guard(__instance, eq, bare));
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
                    Log.Info("Zaloga w bitwie: straz nic z niczego - " + id + " (slot " + i + ") bez pokrycia w zbrojowni zalogi - "
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

        // ------------------------------------------------------------ K1c: zapis zbrojowni zalog
        // DTE zapisuje zbrojownie tylko partii z LeaderHero (EveryoneCampaignBehavior.ShouldPersistParty, CreateSerializedPartyArmories), a przy
        // wczytaniu czysci wszystkie (SyncData) - zbrojownie zalog ginely przy KAZDYM wczytaniu: sprzet kupiony z sakiewek zalog i z kiesy pana,
        // sztuki od gracza ("Hand kit to the garrison"). Zapisujemy je sami (SaveText.Sync, klucz "arm_garrisonarmory": osada,przedmiot,ile;),
        // a po wczytaniu (OnSessionLaunched - po SyncData i OnGameLoaded DTE) dokladamy przez DTE AddItemToPartyArmory. Zaloga, ktora DTE juz
        // ma w zbrojowniach (np. przyszla wersja DTE zapisze zalogi sama), jest pomijana - bez podwojenia.
        private static Dictionary<string, List<KeyValuePair<string, int>>> _pendingArm;
        private static readonly char[] Sep = { ',', ';' };

        internal static string ExportArmories()
        {
            var all = AiGear.Armories();
            if (all == null) return "";
            var sb = new System.Text.StringBuilder();
            int gar = 0, pcs = 0;
            foreach (var st in Settlement.All)
            {
                var g = GarrisonOf(st);
                Dictionary<ItemObject, int> arm;
                if (g == null || !g.IsActive || !all.TryGetValue(g.Id, out arm) || arm == null || arm.Count == 0) continue;
                string sid = st.StringId;
                if (string.IsNullOrEmpty(sid) || sid.IndexOfAny(Sep) >= 0) continue;
                bool any = false;
                foreach (var kv in arm)
                {
                    if (kv.Key == null || kv.Value <= 0) continue;
                    string id = kv.Key.StringId;
                    if (string.IsNullOrEmpty(id) || id.IndexOfAny(Sep) >= 0) continue;
                    sb.Append(sid).Append(',').Append(id).Append(',').Append(kv.Value).Append(';');
                    pcs += kv.Value; any = true;
                }
                if (any) gar++;
            }
            Log.Info("Zbrojownie zalog (K1c): zapisane " + gar + " zalog, " + pcs + " szt.");
            return sb.ToString();
        }

        internal static void ImportArmories(string s)
        {
            _pendingArm = null;
            if (string.IsNullOrEmpty(s)) return;
            var d = new Dictionary<string, List<KeyValuePair<string, int>>>();
            foreach (var rec in s.Split(';'))
            {
                var a = rec.Split(','); int n;
                if (a.Length != 3 || a[0].Length == 0 || a[1].Length == 0 || !int.TryParse(a[2], out n) || n <= 0) continue;
                List<KeyValuePair<string, int>> l;
                if (!d.TryGetValue(a[0], out l)) d[a[0]] = l = new List<KeyValuePair<string, int>>();
                l.Add(new KeyValuePair<string, int>(a[1], n));
            }
            _pendingArm = d;
        }

        /// <summary>Z OnSessionLaunched (po wczytaniu): zbrojownie zalog z zapisu z powrotem do DTE. Raz - potem rejestr pusty.</summary>
        internal static void RestoreArmories()
        {
            var pend = _pendingArm;
            _pendingArm = null;
            if (pend == null || pend.Count == 0) return;
            var all = AiGear.Armories();
            if (all == null) { Log.Info("Zbrojownie zalog (K1c): zapis ma " + pend.Count + " zalog, ale DTE nieobecne - nic nie odtworzone."); return; }
            int gar = 0, pcs = 0, noGar = 0, had = 0, noItem = 0, refused = 0;
            foreach (var kv in pend)
            {
                Settlement st = null;
                try { st = Settlement.Find(kv.Key); } catch { }
                var g = GarrisonOf(st);
                if (g == null || !g.IsActive) { noGar++; continue; }
                Dictionary<ItemObject, int> cur;
                if (all.TryGetValue(g.Id, out cur) && cur != null && cur.Count > 0) { had++; continue; }
                bool any = false;
                foreach (var e in kv.Value)
                {
                    ItemObject it = null;
                    try { it = MBObjectManager.Instance.GetObject<ItemObject>(e.Key) ?? ItemObject.GetCraftedItemObjectFromHashedCode(e.Key); } catch { }
                    if (it == null) { noItem += e.Value; continue; }
                    if (AiGear.AddToArmory(g, it, e.Value)) { pcs += e.Value; any = true; }
                    else refused += e.Value;
                }
                if (any) gar++;
            }
            Log.Info("Zbrojownie zalog (K1c): odtworzone z zapisu " + gar + " zalog, " + pcs + " szt.; pominiete - bez zalogi " + noGar
                     + ", DTE juz mial zbrojownie " + had + ", nieznane przedmioty " + noItem + " szt., DTE nie przyjal " + refused + " szt.");
        }
    }
}
