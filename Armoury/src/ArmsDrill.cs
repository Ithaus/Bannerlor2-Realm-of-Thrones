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
    /// </summary>
    internal static class ArmsDrill
    {
        private static Harmony _h;
        private static readonly HashSet<Type> _trainHooked = new HashSet<Type>(), _xpHooked = new HashSet<Type>();
        [ThreadStatic] private static int _tDepth;
        [ThreadStatic] private static int _gDepth;
        private static readonly Dictionary<MobileParty, float> _share = new Dictionary<MobileParty, float>();
        private static readonly Dictionary<CharacterObject, ItemObject.ItemTypeEnum> _mainType = new Dictionary<CharacterObject, ItemObject.ItemTypeEnum>();
        private static readonly HashSet<MobileParty> _counted = new HashSet<MobileParty>();
        private static int _shareDay = -1;
        // liczniki doby (linia "Cwiczenia (171)")
        private static int _gTowns, _gZero, _gFull, _pParties, _pZero, _pFull, _stumbles, _errDay = -1;
        private static double _gSum, _pSum, _pLost;
        private static readonly HashSet<string> _errWhere = new HashSet<string>();

        internal static void Reset()
        {
            _share.Clear(); _mainType.Clear(); _counted.Clear(); _shareDay = -1;
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
        /// <summary>Glowna bron oddzialu: strzelec - luk/kusza w sl. 0-3; inaczej pierwsza bron biala; inaczej bron rzucana; Invalid - bez broni.</summary>
        private static ItemObject.ItemTypeEnum MainWeaponType(CharacterObject ch)
        {
            ItemObject.ItemTypeEnum r;
            if (_mainType.TryGetValue(ch, out r)) return r;
            r = ItemObject.ItemTypeEnum.Invalid;
            Equipment eq = null; try { eq = ch.Equipment; } catch { }
            if (eq != null)
            {
                if (ch.IsRanged)
                    for (int sl = 0; sl < 4 && r == ItemObject.ItemTypeEnum.Invalid; sl++)
                    {
                        var it = eq[(EquipmentIndex)sl].Item;
                        if (it != null && (it.ItemType == ItemObject.ItemTypeEnum.Bow || it.ItemType == ItemObject.ItemTypeEnum.Crossbow)) r = it.ItemType;
                    }
                for (int sl = 0; sl < 4 && r == ItemObject.ItemTypeEnum.Invalid; sl++)
                {
                    var it = eq[(EquipmentIndex)sl].Item;
                    if (it != null && (it.ItemType == ItemObject.ItemTypeEnum.OneHandedWeapon || it.ItemType == ItemObject.ItemTypeEnum.TwoHandedWeapon || it.ItemType == ItemObject.ItemTypeEnum.Polearm)) r = it.ItemType;
                }
                for (int sl = 0; sl < 4 && r == ItemObject.ItemTypeEnum.Invalid; sl++)
                {
                    var it = eq[(EquipmentIndex)sl].Item;
                    if (it != null && it.ItemType == ItemObject.ItemTypeEnum.Thrown) r = it.ItemType;
                }
            }
            _mainType[ch] = r;
            return r;
        }

        private static void DayCheck()
        {
            int d = (int)CampaignTime.Now.ToDays;
            if (d != _shareDay) { _shareDay = d; _share.Clear(); }
        }

        /// <summary>Udzial ludzi (bez bohaterow), ktorzy maja w zbrojowni DTE bron swojego rodzaju (dowolny tier); 1.0 - DTE brak albo bez ludzi. Pamiec na dobe.</summary>
        internal static float ArmedShare(MobileParty mp)
        {
            if (mp == null) return 1f;
            DayCheck();
            float v;
            if (_share.TryGetValue(mp, out v)) return v;
            v = 1f;
            var dict = AiGear.Armories();
            var roster = mp.MemberRoster;
            if (dict != null && roster != null)
            {
                var need = new Dictionary<ItemObject.ItemTypeEnum, int>();
                int total = 0;
                for (int i = 0; i < roster.Count; i++)
                {
                    var el = roster.GetElementCopyAtIndex(i);
                    if (el.Character == null || el.Character.IsHero || el.Number <= 0) continue;
                    var t = MainWeaponType(el.Character);
                    if (t == ItemObject.ItemTypeEnum.Invalid) continue;
                    int n; need.TryGetValue(t, out n); need[t] = n + el.Number; total += el.Number;
                }
                if (total > 0)
                {
                    var have = new Dictionary<ItemObject.ItemTypeEnum, int>();
                    Dictionary<ItemObject, int> arm;
                    if (dict.TryGetValue(mp.Id, out arm) && arm != null)
                        foreach (var kv in arm)
                        {
                            if (kv.Key == null || kv.Value <= 0 || !need.ContainsKey(kv.Key.ItemType)) continue;
                            int n; have.TryGetValue(kv.Key.ItemType, out n); have[kv.Key.ItemType] = n + kv.Value;
                        }
                    long armed = 0;
                    foreach (var kv in need) { int h; have.TryGetValue(kv.Key, out h); armed += Math.Min(kv.Value, h); }
                    v = (float)armed / total;
                }
            }
            _share[mp] = v;
            return v;
        }

        /// <summary>Kto cwiczy wedlug broni: zaloga - nie Innych, nie gracza (chyba ze GarrisonBuysGearPlayer); partia - rod AI (nie gracz, nie Inni).</summary>
        private static bool Gated(MobileParty mp)
        {
            var s = Settings.Current;
            if (mp == null || s == null || Undead.Party(mp)) return false;
            if (mp.IsGarrison)
            {
                if (!s.GarrisonDrillNeedsArms) return false;
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
                float share = ArmedShare(__0);
                if (!__0.IsGarrison && _counted.Add(__0)) { _pParties++; _pSum += share; if (share <= 0f) _pZero++; else if (share >= 1f) _pFull++; }
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
            ItemObject.ItemTypeEnum.LegArmor, ItemObject.ItemTypeEnum.HandArmor
        };
        private static readonly string[] CovNames = { "korpus", "helm", "tarcza", "nogi", "rece" };

        private sealed class Cov
        {
            internal long Men, Armed; internal readonly long[] Need = new long[5], Lack = new long[5]; internal long LackAll, LackBody, LackWeapon;
            internal void Add(MobileParty mp)
            {
                var dict = AiGear.Armories(); Dictionary<ItemObject, int> arm = null;
                if (dict != null) dict.TryGetValue(mp.Id, out arm);
                var needOut = new Dictionary<int, int>();
                var lack = AiGear.Deficit(mp, arm, needOut);
                int men = mp.MemberRoster.TotalManCount - mp.MemberRoster.TotalHeroes;
                Men += men; Armed += (long)Math.Round(ArmedShare(mp) * men);
                foreach (var kv in needOut) { int i = Array.IndexOf(CovTypes, (ItemObject.ItemTypeEnum)(kv.Key / 10)); if (i >= 0) Need[i] += kv.Value; }
                foreach (var kv in lack)
                {
                    if (kv.Value <= 0) continue;
                    var ty = (ItemObject.ItemTypeEnum)(kv.Key / 10);
                    LackAll += kv.Value;
                    int i = Array.IndexOf(CovTypes, ty); if (i >= 0) Lack[i] += kv.Value;
                    if (ty == ItemObject.ItemTypeEnum.BodyArmor) LackBody += kv.Value;
                    if (ty == ItemObject.ItemTypeEnum.OneHandedWeapon || ty == ItemObject.ItemTypeEnum.TwoHandedWeapon || ty == ItemObject.ItemTypeEnum.Polearm
                        || ty == ItemObject.ItemTypeEnum.Bow || ty == ItemObject.ItemTypeEnum.Crossbow) LackWeapon += kv.Value;
                }
            }
            internal string Text(string name)
            {
                var sb = new System.Text.StringBuilder();
                sb.Append(name).Append(" (").Append(Men).Append(" ludzi): ").Append(CovNames[0]).Append(' ').Append(Pct(Need[0], Lack[0]))
                  .Append(", glowna bron ").Append(Men > 0 ? (100 * Armed / Men) + "%" : "-");
                for (int i = 1; i < 5; i++) sb.Append(", ").Append(CovNames[i]).Append(' ').Append(Pct(Need[i], Lack[i]));
                return sb.ToString();
            }
            private static string Pct(long need, long lack) { return need > 0 ? (100 * (need - lack) / need) + "%" : "-"; }
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
                     + castlesLow + " z " + castles + "; brakuje razem " + (lords.LackAll + gar.LackAll) + " szt. (korpus " + (lords.LackBody + gar.LackBody) + ", glowna bron "
                     + (lords.LackWeapon + gar.LackWeapon) + "); w drodze do zamkow " + GarrisonCarts.InTransitPieces() + " szt.; czas przegladu " + sw.ElapsedMilliseconds + " ms.");
        }
    }
}
