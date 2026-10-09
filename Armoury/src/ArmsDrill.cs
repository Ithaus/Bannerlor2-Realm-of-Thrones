using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// 171 CWICZENIA WLASNA BRONIA (docs/paczki/171-zbrojenie-zalog.md, D1-D4; Jeff 08.10 "tak potwierdzam" (3): szkolenie zalogi = codzienne
    /// cwiczenia wlasna bronia). Z8: doswiadczenie dnia razy udzial ludzi, ktorzy maja bron swojego rodzaju w zbrojowni DTE - musztra bez broni
    /// nie uczy walki. Dotyczy:
    ///  - D2 zalog AI: mnoznik doswiadczenia zalogi (CalculateGarrisonXpBonusMultiplier - budynki Training Fields i projekt "Drills"; BK mnozy go
    ///    swoja polityka x0.7/x1.3 - mnozenie jest przemienne), gra wola go raz na twierdze na dobe;
    ///  - D3 partii rodow AI: codzienne cwiczenia druzyny (GetEffectiveDailyExperience - BK 10-15 + 2-3 x tier na czlowieka) - glowny silnik awansow AI,
    ///    a wiec popytu na sprzet; jedyny hamulec, ktory bez tworzenia czegokolwiek z niczego dopasowuje tempo awansow do ilosci broni w swiecie.
    /// Latki modeli zakladane w kampanii (OnSessionLaunched, raz na klase - flagi na caly proces): BK BKPartyTrainningModel czyta statyczne
    /// singletony BK, a Harmony kompiluje metode juz przy zakladaniu latki (pulapka z SoldierPay/MountedWage). Licznik zagniezdzenia: NavalDLC
    /// tylko przekazuje do modelu BK - liczymy raz, na zewnatrz. Gracz, jego rod i Inni - bez zmian (zalogi gracza tylko przy GarrisonBuysGearPlayer).
    /// Linia "Pokrycie zbrojowni AI (171)" - pelny przeglad raz na 5 dob (krytyka 17), tylko log.
    /// Recenzja kodu 171: udzial uzbrojonych liczony wedlug SZCZEBLA broni (sztuka typu glownej broni o tierze >= t-1, jak AiGear.Deficit), dla partii na oddzial
    /// (element rosteru); cwiczenia wedlug broni tylko przy zakupach AI (AiGear.On, zaloga - takze GarrisonBuysGear); "Pokrycie" - takze strzaly, belty i przedmioty t3+.
    /// </summary>
    internal static class ArmsDrill
    {
        private static Harmony _h;
        private static readonly HashSet<Type> _trainHooked = new HashSet<Type>(), _xpHooked = new HashSet<Type>();
        [ThreadStatic] private static int _tDepth;
        [ThreadStatic] private static int _gDepth;
        // recenzja 171: udzial uzbrojonych wedlug SZCZEBLA broni (koszyk typ x tier glownej broni wzorca; pokrywa sztuka tego typu o tierze >= t-1, jak w
        // AiGear.Deficit) - All dla calej partii/zalogi, ByKey dla oddzialow (cwiczenia druzyny licza sie na element rosteru)
        private sealed class Share { internal float All = 1f; internal Dictionary<int, float> ByKey; }
        private static readonly Dictionary<MobileParty, Share> _share = new Dictionary<MobileParty, Share>();
        private static readonly Dictionary<CharacterObject, int> _mainKey = new Dictionary<CharacterObject, int>();
        private static readonly HashSet<MobileParty> _counted = new HashSet<MobileParty>();
        private static int _shareDay = -1;
        // liczniki doby (linia "Cwiczenia (171)")
        private static int _gTowns, _gZero, _gFull, _pParties, _pZero, _pFull, _stumbles, _errDay = -1;
        private static double _gSum, _pSum, _pLost;
        private static readonly HashSet<string> _errWhere = new HashSet<string>();

        internal static void Reset()
        {
            _share.Clear(); _mainKey.Clear(); _counted.Clear(); _shareDay = -1;
            ClearDay(); _stumbles = 0; _errDay = -1; _errWhere.Clear();
        }

        private static void ClearDay() { _gTowns = _gZero = _gFull = _pParties = _pZero = _pFull = 0; _gSum = _pSum = _pLost = 0; _counted.Clear(); }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try
            {
                int d = (int)CampaignTime.Now.ToDays;
                if (d != _errDay) { _errDay = d; _errWhere.Clear(); }
                if (_errWhere.Add(where)) Log.Error("ArmsDrill." + where, e);
            }
            catch { }
        }

        // ------------------------------------------------------------ D4: udzial uzbrojonych
        /// <summary>Glowna bron oddzialu: strzelec - luk/kusza w sl. 0-3; inaczej pierwsza bron biala; inaczej bron rzucana; null - bez broni.</summary>
        private static ItemObject MainWeapon(CharacterObject ch)
        {
            Equipment eq = null; try { eq = ch.Equipment; } catch { }
            if (eq == null) return null;
            if (ch.IsRanged)
                for (int sl = 0; sl < 4; sl++)
                {
                    var it = eq[(EquipmentIndex)sl].Item;
                    if (it != null && (it.ItemType == ItemObject.ItemTypeEnum.Bow || it.ItemType == ItemObject.ItemTypeEnum.Crossbow)) return it;
                }
            for (int sl = 0; sl < 4; sl++)
            {
                var it = eq[(EquipmentIndex)sl].Item;
                if (it != null && (it.ItemType == ItemObject.ItemTypeEnum.OneHandedWeapon || it.ItemType == ItemObject.ItemTypeEnum.TwoHandedWeapon || it.ItemType == ItemObject.ItemTypeEnum.Polearm)) return it;
            }
            for (int sl = 0; sl < 4; sl++)
            {
                var it = eq[(EquipmentIndex)sl].Item;
                if (it != null && it.ItemType == ItemObject.ItemTypeEnum.Thrown) return it;
            }
            return null;
        }

        /// <summary>Koszyk glownej broni oddzialu (typ*10 + tier, jak AiGear.Bucket); -1 - bez broni. Pamiec na CharacterObject.</summary>
        private static int MainKey(CharacterObject ch)
        {
            int k;
            if (_mainKey.TryGetValue(ch, out k)) return k;
            var it = MainWeapon(ch);
            k = it != null ? AiGear.Bucket(it) : -1;
            _mainKey[ch] = k;
            return k;
        }

        private static int TierOf(ItemObject it) { try { return Math.Max(1, Math.Min(6, (int)it.Tier + 1)); } catch { return 1; } }

        private static void DayCheck()
        {
            int d = (int)CampaignTime.Now.ToDays;
            if (d != _shareDay) { _shareDay = d; _share.Clear(); }
        }

        /// <summary>
        /// Udzial ludzi (bez bohaterow), ktorzy maja w zbrojowni DTE glowna bron swojego rodzaju I SZCZEBLA: sztuka tego typu o tierze >= t-1 (t = tier glownej
        /// broni wzorca) - te same sztuki, ktore AiGear.Deficit uznaje za pokrycie. Recenzja 171: dotad liczyla sie kazda sztuka typu, wiec wlocznia t1 z rekrutacji
        /// "uzbrajala" czlowieka t5 i hamulec awansow (D3) prawie nigdy nie dzialal. Przydzial: najpierw potrzeby najwyzszego szczebla (ich sztuki nadaja sie
        /// tez nizszym - wybor nie psuje pokrycia nizszych). 1.0 - DTE brak albo bez ludzi. Pamiec na dobe.
        /// </summary>
        internal static float ArmedShare(MobileParty mp) { var sh = ShareOf(mp); return sh != null ? sh.All : 1f; }

        /// <summary>Udzial uzbrojonych dla oddzialu ch w partii mp (jego koszyk glownej broni); oddzial bez broni albo bohater - udzial calej partii.</summary>
        private static float ShareFor(MobileParty mp, CharacterObject ch)
        {
            var sh = ShareOf(mp);
            if (sh == null) return 1f;
            float v;
            if (ch != null && !ch.IsHero && sh.ByKey != null && sh.ByKey.TryGetValue(MainKey(ch), out v)) return v;
            return sh.All;
        }

        private static Share ShareOf(MobileParty mp)
        {
            if (mp == null) return null;
            DayCheck();
            Share sh;
            if (_share.TryGetValue(mp, out sh)) return sh;
            sh = new Share();
            var dict = AiGear.Armories();
            var roster = mp.MemberRoster;
            if (dict != null && roster != null)
            {
                var need = new Dictionary<int, int>();
                int total = 0;
                for (int i = 0; i < roster.Count; i++)
                {
                    var el = roster.GetElementCopyAtIndex(i);
                    if (el.Character == null || el.Character.IsHero || el.Number <= 0) continue;
                    int k = MainKey(el.Character);
                    if (k < 0) continue;
                    int n; need.TryGetValue(k, out n); need[k] = n + el.Number; total += el.Number;
                }
                if (total > 0)
                {
                    // sztuki zbrojowni: typ -> liczba wedlug tieru 1..6 (tylko typy potrzebne)
                    var avail = new Dictionary<int, int[]>();
                    foreach (var k in need.Keys) if (!avail.ContainsKey(k / 10)) avail[k / 10] = new int[7];
                    Dictionary<ItemObject, int> arm;
                    if (dict.TryGetValue(mp.Id, out arm) && arm != null)
                        foreach (var kv in arm)
                        {
                            int[] a;
                            if (kv.Key == null || kv.Value <= 0 || !avail.TryGetValue((int)kv.Key.ItemType, out a)) continue;
                            a[TierOf(kv.Key)] += kv.Value;
                        }
                    var keys = new List<int>(need.Keys);
                    keys.Sort((x, y) => (y % 10).CompareTo(x % 10));   // najwyzszy szczebel najpierw
                    sh.ByKey = new Dictionary<int, float>();
                    long armed = 0;
                    foreach (var k in keys)
                    {
                        int want = need[k], left = want, t = k % 10;
                        var a = avail[k / 10];
                        for (int s = Math.Max(1, t - 1); s <= 6 && left > 0; s++) { int c = Math.Min(left, a[s]); a[s] -= c; left -= c; }
                        sh.ByKey[k] = (float)(want - left) / want;
                        armed += want - left;
                    }
                    sh.All = (float)armed / total;
                }
            }
            _share[mp] = sh;
            return sh;
        }

        /// <summary>
        /// Kto cwiczy wedlug broni: zaloga - nie Innych, nie gracza (chyba ze GarrisonBuysGearPlayer); partia - rod AI (nie gracz, nie Inni). Recenzja 171: tylko gdy
        /// AI kupuje bron (AiGear.On; zaloga - takze GarrisonBuysGear) - bez zakupow zbrojownie zalog kasuje co dobe DTE, a zalogi nie maja skad uzupelnic broni,
        /// wiec "cwiczenia wlasna bronia" zatrzymalyby szkolenie wszystkich zalog AI.
        /// </summary>
        private static bool Gated(MobileParty mp)
        {
            var s = Settings.Current;
            if (mp == null || s == null || Undead.Party(mp) || !AiGear.On) return false;
            if (mp.IsGarrison)
            {
                if (!s.GarrisonDrillNeedsArms || !s.GarrisonBuysGear) return false;
                var st = mp.CurrentSettlement ?? mp.HomeSettlement;
                return st == null || st.OwnerClan != Clan.PlayerClan || s.GarrisonBuysGearPlayer;
            }
            return s.PartyDrillNeedsArms && mp.IsLordParty && mp != MobileParty.MainParty && mp.ActualClan != Clan.PlayerClan;
        }

        // ------------------------------------------------------------ D2: zalogi
        public static void GarrisonXpPrefix() { _gDepth++; }
        public static Exception GarrisonXpFinalizer(Exception __exception) { if (_gDepth > 0) _gDepth--; return __exception; }
        public static void GarrisonXpPostfix(Town __0, ref float __result)
        {
            if (_gDepth > 1) return;   // wewnetrzny model - zewnetrzny policzy
            try
            {
                var g = __0 != null ? __0.GarrisonParty : null;
                if (g == null || !Gated(g)) return;
                float share = ArmedShare(g);
                __result *= share;
                _gTowns++; _gSum += share; if (share <= 0f) _gZero++; else if (share >= 1f) _gFull++;
            }
            catch (Exception e) { Stumble("GarrisonXpPostfix", e); }
        }

        // ------------------------------------------------------------ D3: partie rodow AI (i perki gubernatora zalog)
        public static void TrainingPrefix() { _tDepth++; }
        public static Exception TrainingFinalizer(Exception __exception) { if (_tDepth > 0) _tDepth--; return __exception; }
        public static void TrainingPostfix(MobileParty __0, TroopRosterElement __1, ref ExplainedNumber __result)
        {
            if (_tDepth > 1) return;
            try
            {
                if (__result.ResultNumber <= 0f || __0 == null || !Gated(__0)) return;
                if (!__0.IsGarrison && _counted.Add(__0)) { float all = ArmedShare(__0); _pParties++; _pSum += all; if (all <= 0f) _pZero++; else if (all >= 1f) _pFull++; }
                // recenzja 171: udzial oddzialu (jego glowna bron i szczebel) - cwiczy wolniej ten, kogo nie ma czym uzbroic na jego szczeblu, nie cala druzyna
                float share = ShareFor(__0, __1.Character);
                if (share >= 1f) return;
                if (!__0.IsGarrison && __1.Character != null && !__1.Character.IsHero) _pLost += __result.ResultNumber * (1f - share) * __1.Number;
                __result = new ExplainedNumber(__result.ResultNumber * share);   // opisy gubimy swiadomie - partii AI nikt nie oglada
            }
            catch (Exception e) { Stumble("TrainingPostfix", e); }
        }

        // ------------------------------------------------------------ wpiecie
        internal static void ApplyAll(Harmony h)
        {
            if (h == null || _h != null) return;
            _h = h;
            Log.Info("ArmsDrill: cwiczenia wlasna bronia (171) - latki modeli szkolenia i doswiadczenia zalog dojda przy starcie kampanii (modele BK czytaja singletony BK).");
        }

        /// <summary>Z OnSessionLaunched: postfiks na kazdej klasie modelu deklarujacej metode - raz na klase na caly proces.</summary>
        internal static void EnsureHooks()
        {
            var h = _h;
            if (h == null) return;
            int nowT = 0, nowX = 0;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); } catch { continue; }
                foreach (var t in types)
                {
                    if (t == null || t.IsAbstract) continue;
                    try
                    {
                        if (typeof(PartyTrainingModel).IsAssignableFrom(t) && !_trainHooked.Contains(t))
                        {
                            var m = t.GetMethod("GetEffectiveDailyExperience", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, null, new[] { typeof(MobileParty), typeof(TroopRosterElement) }, null);
                            if (m != null && !m.IsAbstract)
                            {
                                _trainHooked.Add(t);
                                h.Patch(m, prefix: new HarmonyMethod(typeof(ArmsDrill), nameof(TrainingPrefix)) { priority = Priority.First },
                                           postfix: new HarmonyMethod(typeof(ArmsDrill), nameof(TrainingPostfix)) { priority = Priority.Last },
                                           finalizer: new HarmonyMethod(typeof(ArmsDrill), nameof(TrainingFinalizer)));
                                nowT++;
                            }
                        }
                        else if (typeof(DailyTroopXpBonusModel).IsAssignableFrom(t) && !_xpHooked.Contains(t))
                        {
                            var m = t.GetMethod("CalculateGarrisonXpBonusMultiplier", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, null, new[] { typeof(Town) }, null);
                            if (m != null && !m.IsAbstract)
                            {
                                _xpHooked.Add(t);
                                h.Patch(m, prefix: new HarmonyMethod(typeof(ArmsDrill), nameof(GarrisonXpPrefix)) { priority = Priority.First },
                                           postfix: new HarmonyMethod(typeof(ArmsDrill), nameof(GarrisonXpPostfix)) { priority = Priority.Last },
                                           finalizer: new HarmonyMethod(typeof(ArmsDrill), nameof(GarrisonXpFinalizer)));
                                nowX++;
                            }
                        }
                    }
                    catch (Exception e) { Log.Error("ArmsDrill.EnsureHooks(" + t.FullName + ")", e); }
                }
            }
            if (nowT + nowX > 0 || (_trainHooked.Count + _xpHooked.Count) == 0)
            {
                string act = "?", actX = "?";
                try { act = Campaign.Current.Models.PartyTrainingModel.GetType().FullName; actX = Campaign.Current.Models.DailyTroopXpBonusModel.GetType().FullName; } catch { }
                Log.Info("ArmsDrill: cwiczenia wlasna bronia - modele szkolenia " + _trainHooked.Count + " (wpiete, teraz " + nowT + "; czynny " + act + "), modele doswiadczenia zalog "
                         + _xpHooked.Count + " (wpiete, teraz " + nowX + "; czynny " + actX + ").");
            }
        }

        // ------------------------------------------------------------ linie dnia
        internal static void Daily()
        {
            var s = Settings.Current;
            if (s == null) return;
            int today = (int)CampaignTime.Now.ToDays;
            var inv = CultureInfo.InvariantCulture;
            if (s.GarrisonDrillNeedsArms || s.PartyDrillNeedsArms || _stumbles > 0)
                Log.Info("Cwiczenia (171): dzien " + today + " - zalogi AI: twierdz z cwiczeniami " + _gTowns + ", mnoznik srednio x" + (_gTowns > 0 ? (_gSum / _gTowns).ToString("0.00", inv) : "-")
                         + " (zero: " + _gZero + ", pelny: " + _gFull + "); partie rodow AI: " + _pParties + ", mnoznik srednio x" + (_pParties > 0 ? (_pSum / _pParties).ToString("0.00", inv) : "-")
                         + " (zero: " + _pZero + ", pelny: " + _pFull + "), doswiadczenia mniej przez brak broni ok. " + (long)_pLost + "; potkniecia " + _stumbles + ".");
            ClearDay(); _stumbles = 0;
            if (s.ArmsCoverageLog && today % 5 == 0)
                try { Coverage(today); } catch (Exception e) { Stumble("Coverage", e); }
        }

        private static readonly ItemObject.ItemTypeEnum[] CovTypes =
        {
            ItemObject.ItemTypeEnum.BodyArmor, ItemObject.ItemTypeEnum.HeadArmor, ItemObject.ItemTypeEnum.Shield,
            ItemObject.ItemTypeEnum.LegArmor, ItemObject.ItemTypeEnum.HandArmor, ItemObject.ItemTypeEnum.Arrows, ItemObject.ItemTypeEnum.Bolts
        };
        private static readonly string[] CovNames = { "korpus", "helm", "tarcza", "nogi", "rece", "strzaly", "belty" };   // recenzja 171: + strzaly i belty

        private sealed class Cov
        {
            internal long Men, Armed; internal readonly long[] Need = new long[7], Lack = new long[7]; internal long LackAll, LackBody, LackWeapon;
            internal long NeedBody3, LackBody3, NeedWeap3, LackWeap3;   // recenzja 171: przedmioty tieru 3+ (sprzet wyzszych szczebli)
            // 174.0 (f): miara "dowolny szczebel" - potrzeba wedlug TYPU (suma tierow wzorcow) wobec sztuk tego typu w zbrojowni w dowolnym tierze;
            // na partie min(potrzeba, sztuki), suma po partiach - odsetek ludzi z czymkolwiek na tym miejscu (to zalozy DTE w bitwie: tier <= t+2 bez dolnej granicy).
            // Kolejnosc: korpus, glowna bron, helm, tarcza, rekawice, nogi. Miara szczebla (wyzej) bez zmian - na nia patrza cwiczenia i awanse.
            internal readonly long[] AnyNeed = new long[6], AnyHave = new long[6];
            private static readonly ItemObject.ItemTypeEnum[] AnyTypes =
            {
                ItemObject.ItemTypeEnum.BodyArmor, ItemObject.ItemTypeEnum.Invalid, ItemObject.ItemTypeEnum.HeadArmor,
                ItemObject.ItemTypeEnum.Shield, ItemObject.ItemTypeEnum.HandArmor, ItemObject.ItemTypeEnum.LegArmor
            };
            private static readonly string[] AnyNames = { "korpus", "glowna bron", "helm", "tarcza", "rekawice", "nogi" };
            private static bool Weapon(ItemObject.ItemTypeEnum ty)
            {
                return ty == ItemObject.ItemTypeEnum.OneHandedWeapon || ty == ItemObject.ItemTypeEnum.TwoHandedWeapon || ty == ItemObject.ItemTypeEnum.Polearm
                       || ty == ItemObject.ItemTypeEnum.Bow || ty == ItemObject.ItemTypeEnum.Crossbow;
            }
            internal void Add(MobileParty mp)
            {
                var dict = AiGear.Armories(); Dictionary<ItemObject, int> arm = null;
                if (dict != null) dict.TryGetValue(mp.Id, out arm);
                var needOut = new Dictionary<int, int>();
                var lack = AiGear.Deficit(mp, arm, needOut);
                int men = mp.MemberRoster.TotalManCount - mp.MemberRoster.TotalHeroes;
                Men += men; Armed += (long)Math.Round(ArmedShare(mp) * men);
                try { AddAny(mp, arm, needOut); } catch (Exception e) { Stumble("Coverage(dowolny szczebel)", e); }
                foreach (var kv in needOut)
                {
                    var ty0 = (ItemObject.ItemTypeEnum)(kv.Key / 10);
                    int i = Array.IndexOf(CovTypes, ty0); if (i >= 0) Need[i] += kv.Value;
                    if (kv.Key % 10 >= 3) { if (ty0 == ItemObject.ItemTypeEnum.BodyArmor) NeedBody3 += kv.Value; else if (Weapon(ty0)) NeedWeap3 += kv.Value; }
                }
                foreach (var kv in lack)
                {
                    if (kv.Value <= 0) continue;
                    var ty = (ItemObject.ItemTypeEnum)(kv.Key / 10);
                    LackAll += kv.Value;
                    int i = Array.IndexOf(CovTypes, ty); if (i >= 0) Lack[i] += kv.Value;
                    if (ty == ItemObject.ItemTypeEnum.BodyArmor) LackBody += kv.Value;
                    if (Weapon(ty)) LackWeapon += kv.Value;
                    if (kv.Key % 10 >= 3) { if (ty == ItemObject.ItemTypeEnum.BodyArmor) LackBody3 += kv.Value; else if (Weapon(ty)) LackWeap3 += kv.Value; }
                }
            }
            /// <summary>174.0 (f): dowolny szczebel dla jednej partii (sztuki zbrojowni wedlug typu w dowolnym tierze).</summary>
            private void AddAny(MobileParty mp, Dictionary<ItemObject, int> arm, Dictionary<int, int> needOut)
            {
                var have = new Dictionary<int, long>();
                if (arm != null) foreach (var kv in arm) { if (kv.Key == null || kv.Value <= 0) continue; int ty = (int)kv.Key.ItemType; long h; have.TryGetValue(ty, out h); have[ty] = h + kv.Value; }
                var need = new Dictionary<int, long>();
                foreach (var kv in needOut) { int ty = kv.Key / 10; long n; need.TryGetValue(ty, out n); need[ty] = n + kv.Value; }
                for (int i = 0; i < AnyTypes.Length; i++)
                {
                    if (i == 1) continue;   // glowna bron - nizej, wedlug oddzialow
                    int ty = (int)AnyTypes[i];
                    long n, h; need.TryGetValue(ty, out n); have.TryGetValue(ty, out h);
                    if (n <= 0) continue;
                    AnyNeed[i] += n; AnyHave[i] += Math.Min(n, h);
                }
                // glowna bron: ludzie wedlug typu glownej broni wzorca (MainWeapon) wobec sztuk tego typu w zbrojowni w dowolnym tierze
                var mainNeed = new Dictionary<int, long>();
                var roster = mp.MemberRoster;
                for (int i = 0; i < roster.Count; i++)
                {
                    var el = roster.GetElementCopyAtIndex(i);
                    if (el.Character == null || el.Character.IsHero || el.Number <= 0) continue;
                    int key = MainKey(el.Character);
                    if (key < 0) continue;
                    int ty = key / 10; long n; mainNeed.TryGetValue(ty, out n); mainNeed[ty] = n + el.Number;
                }
                foreach (var kv in mainNeed) { long h; have.TryGetValue(kv.Key, out h); AnyNeed[1] += kv.Value; AnyHave[1] += Math.Min(kv.Value, h); }
            }

            internal string AnyText()
            {
                var sb = new System.Text.StringBuilder(" | dowolny szczebel: ");
                for (int i = 0; i < AnyNames.Length; i++) { if (i > 0) sb.Append(", "); sb.Append(AnyNames[i]).Append(' ').Append(AnyNeed[i] > 0 ? (100 * AnyHave[i] / AnyNeed[i]) + "%" : "-"); }
                return sb.Append(" (ludzi z czymkolwiek)").ToString();
            }

            internal string Text(string name)
            {
                var sb = new System.Text.StringBuilder();
                sb.Append(name).Append(" (").Append(Men).Append(" ludzi): ").Append(CovNames[0]).Append(' ').Append(Pct(Need[0], Lack[0]))
                  .Append(", glowna bron (wedlug szczebla) ").Append(Men > 0 ? (100 * Armed / Men) + "%" : "-");
                for (int i = 1; i < CovTypes.Length; i++) sb.Append(", ").Append(CovNames[i]).Append(' ').Append(Pct(Need[i], Lack[i]));
                sb.Append(" | przedmioty t3+: korpus ").Append(Pct(NeedBody3, LackBody3)).Append(", bron ").Append(Pct(NeedWeap3, LackWeap3));
                sb.Append(AnyText());   // 174.0 (f)
                return sb.ToString();
            }
            private static string Pct(long need, long lack) { return need > 0 ? (100 * (need - lack) / need) + "%" : "-"; }

            /// <summary>174b.0 (krytyka P6): odsetek ludzi z czymkolwiek na tulowiu (dowolny szczebel) i z korpusem szczebla - ulamki bez zaokraglenia (-1 = brak ludzi).</summary>
            internal double AnyBodyShare { get { return AnyNeed[0] > 0 ? (double)AnyHave[0] / AnyNeed[0] : -1.0; } }
            internal double BodyShare { get { return Need[0] > 0 ? (double)(Need[0] - Lack[0]) / Need[0] : -1.0; } }
        }

        /// <summary>174b.0: srednia partii i zalog wazona liczba ludzi (wzor "razem" progu P6 w docs/PROJEKT-174B rozdz. 6).</summary>
        private static string Together(Cov a, Cov b, bool any)
        {
            double sa = any ? a.AnyBodyShare : a.BodyShare, sb = any ? b.AnyBodyShare : b.BodyShare;
            double w = 0, v = 0;
            if (sa >= 0 && a.Men > 0) { w += a.Men; v += sa * a.Men; }
            if (sb >= 0 && b.Men > 0) { w += b.Men; v += sb * b.Men; }
            return w > 0 ? (100.0 * v / w).ToString("0.0", CultureInfo.InvariantCulture) + "%" : "-";
        }

        /// <summary>Linia "Pokrycie zbrojowni AI (171)" - pelny przeglad partii rodow AI i zalog AI (tylko log, raz na 5 dob).</summary>
        private static void Coverage(int today)
        {
            if (AiGear.Armories() == null) return;
            var sw = Stopwatch.StartNew();
            var s = Settings.Current;
            var lords = new Cov(); var gar = new Cov();
            int castles = 0, castlesLow = 0;
            foreach (var mp in MobileParty.AllLordParties)
            {
                try
                {
                    if (mp == null || !mp.IsActive || mp.LeaderHero == null || mp == MobileParty.MainParty || mp.ActualClan == Clan.PlayerClan || Undead.Party(mp) || mp.MemberRoster == null) continue;
                    lords.Add(mp);
                }
                catch (Exception e) { Stumble("Coverage(partia)", e); }
            }
            foreach (var st in Settlement.All)
            {
                try
                {
                    if (st == null || !st.IsFortification || st.Town == null || st.Town.GarrisonParty == null) continue;
                    var g = st.Town.GarrisonParty;
                    if ((st.OwnerClan == Clan.PlayerClan && !s.GarrisonBuysGearPlayer) || Undead.Party(g) || g.MemberRoster == null) continue;
                    gar.Add(g);
                    if (st.IsCastle) { castles++; if (ArmedShare(g) < 0.5f) castlesLow++; }
                }
                catch (Exception e) { Stumble("Coverage(zaloga)", e); }
            }
            sw.Stop();
            Log.Info("Pokrycie zbrojowni AI (171): dzien " + today + " - " + lords.Text("partie rodow AI") + "; " + gar.Text("zalogi AI") + "; zamki z pokryciem glownej broni < 50%: "
                     + castlesLow + " z " + castles + "; brakuje razem " + (lords.LackAll + gar.LackAll) + " szt. (korpus " + (lords.LackBody + gar.LackBody) + ", bron "
                     + (lords.LackWeapon + gar.LackWeapon) + "); w drodze do zamkow " + GarrisonCarts.InTransitPieces() + " szt.; razem (partie i zalogi wazone liczba ludzi): cokolwiek na tulowiu "
                     + Together(lords, gar, true) + ", korpus szczebla " + Together(lords, gar, false) + "; czas przegladu " + sw.ElapsedMilliseconds + " ms.");
        }
    }
}
