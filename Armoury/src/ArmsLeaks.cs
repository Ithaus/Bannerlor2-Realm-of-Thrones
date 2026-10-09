using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// PACZKA 174.0 - KONIEC ZNIKANIA UZBROJENIA (docs/PROJEKT-174-PRODUKCJA-UZBROJENIA-2026-10-09.md rozdz. 3.1; Jeff 08.10: "jak znika to zamykamy,
    /// ma byc logiczny system ekonomii, ze wszystko z czegos wynika"). Autotest 171+172 (40 dob): z polek miast znikalo bez sladu 0.7-1.0 tys. sztuk
    /// uzbrojenia na dobe. Trzy ujscia w kodzie (dekompilacje gry i BK):
    ///  (a) mieszczanie "zjadali" bron i zbroje: prefiks BK ItemConsumptionPatch (zamiast MakeConsumption gry) bierze z polki kazda kategorie
    ///      z budzetem (BK CalculateBudget, mnozony przez nasz HistoricalPrices.BudgetPostfix), zloto z niczego idzie do kasy osady; BK wola
    ///      MakeConsumptionInTown takze dla zamkow. Przy ArmsNotHouseholdGoods budzet kategorii uzbrojenia = 0 (ten sam predykat co cechy
    ///      warsztatow: "_armor", "melee_weapons", "ranged_weapons", "shield", "horse_equipment") - takze przy wylaczonym TownHouseholdUse.
    ///      Odziez cywilna t1 (kategoria "garment") i strzaly ("arrows", 172) bez zmian.
    ///  (b) gra kasowala co dobe 1 sztuke z kazdego stosu z modyfikatorem z szansa 5% (ItemConsumptionBehavior.DeleteOverproducedItems; kazdy
    ///      wyrob warsztatu ma losowy modyfikator). Przy ArmsNoStallDecay prefiks robi te sama petle co gra z pominieciem uzbrojenia (bez koni).
    ///      Wyroby gracza i sztandary - cale stosy jak w grze (jawna regula z powodem technicznym: kazda sztuka kuta przez gracza to osobny
    ///      obiekt zapisu - trzymanie ich na polkach rozdyma zapis); liczone osobno w linii dnia.
    ///  (c) BK zaopatrzenie partii AI kupowalo i niszczylo bron i tarcze (WeaponsNeed, ShieldsNeed) - latki w BkSupplyTemper (wzor 172).
    /// Sklad nie jest darmowy (e): kowale miasta codziennie czyszcza i oliwia zapas na polce (ArmsStallUpkeepManDaysPerPiece roboczodnia na sztuke
    /// na rok - Tower lat 1340.) - rece platnerzy i miecznikow, nie zloto (WorkshopLaw.LineShare, obok napraw SmithHours).
    /// Pomiar (d): ksiega towarow (GoodsLedger) sledzi uzbrojenie w swoich ramkach (podsluch AddToCounts) - linia "Uzbrojenie (ujscia 174)".
    /// </summary>
    internal static class ArmsLeaks
    {
        // ------------------------------------------------------------ stan doby (tylko liczniki do linii)
        private static int _skipStacks, _otherPieces, _decayTowns, _budgetZero, _stumbles, _stumblesAll;
        private static double _upkeepDay;
        private static readonly Dictionary<Town, KeyValuePair<int, float>> _upkeep = new Dictionary<Town, KeyValuePair<int, float>>();
        // ksiega uzbrojenia z ramek GoodsLedger: kind -> [uzbrojenie, odziez garment, wyroby gracza, konie] (sztuki zdjete ze swiata, + = ubylo)
        private static long[,] _kindOut;
        private static readonly Dictionary<int, long> _byKey = new Dictionary<int, long>();   // typ*10+tier -> sztuki zdjete (ujscia, bez koni i wyrobow gracza)
        private static long _bkAiWeap, _bkAiShield, _bkPlWeap, _bkPlShield, _lost;
        internal static int BkZeroCalls, BkReset;    // BkSupplyTemper: przeliczenia potrzeby z wynikiem 0 i wyzerowane zapisane potrzeby (bron, tarcze)
        private static int _lastStacks = -1;
        internal static bool DecayHooked;
        private static readonly HashSet<string> _errSites = new HashSet<string>();

        internal static void Reset()
        {
            _skipStacks = _otherPieces = _decayTowns = _budgetZero = _stumbles = _stumblesAll = 0;
            _upkeepDay = 0; _upkeep.Clear(); _kindOut = null; _byKey.Clear();
            _bkAiWeap = _bkAiShield = _bkPlWeap = _bkPlShield = _lost = 0; BkZeroCalls = BkReset = 0;
            _lastStacks = -1; _errSites.Clear();
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumblesAll++;
            if (_errSites.Add(where)) Log.Error("ArmsLeaks." + where, e);
        }

        internal static bool NotHousehold { get { var s = Settings.Current; return s != null && s.ArmsNotHouseholdGoods; } }
        internal static bool NoStallDecay { get { var s = Settings.Current; return s != null && s.ArmsNoStallDecay; } }

        /// <summary>Kategoria uzbrojenia (ten sam predykat co WorkshopLaw.GuildOf i ColdStart.GuildOfCategory, bez "garment" i "arrows").</summary>
        internal static bool IsArmsCategory(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            return id.EndsWith("_armor") || id.StartsWith("melee_weapons") || id.StartsWith("ranged_weapons") || id.StartsWith("shield") || id.StartsWith("horse_equipment");
        }

        /// <summary>(a) HistoricalPrices.BudgetPostfix: budzet mieszczan na te kategorie = 0? (licznik wierszy polek)</summary>
        internal static bool ZeroTownBudget(ItemCategory cat)
        {
            if (cat == null || !NotHousehold || !IsArmsCategory(cat.StringId)) return false;
            _budgetZero++;
            return true;
        }

        private static int TierOf(ItemObject it) { try { return Math.Max(1, Math.Min(6, (int)it.Tier + 1)); } catch { return 1; } }

        /// <summary>Uzbrojenie bez koni (SupplyDemand.Equipmentish) - to, czego nie kasujemy i co liczymy jako zapas sklepu.</summary>
        internal static bool ArmsPiece(ItemObject it)
        {
            return it != null && it.ItemType != ItemObject.ItemTypeEnum.Horse && SupplyDemand.Equipmentish(it);
        }

        // ------------------------------------------------------------ (b) kasowanie gry
        /// <summary>Prefiks ItemConsumptionBehavior.DeleteOverproducedItems(Town): petla gry bez uzbrojenia (bez koni). Wylaczone - oryginal.</summary>
        public static bool DecayPrefix(Town __0)
        {
            if (!NoStallDecay || __0 == null) return true;
            try
            {
                var roster = __0.Owner != null ? __0.Owner.ItemRoster : null;
                if (roster == null) return true;
                _decayTowns++;
                for (int i = roster.Count - 1; i >= 0; i--)
                {
                    var el = roster.GetElementCopyAtIndex(i);
                    var it = el.EquipmentElement.Item;
                    int amount = el.Amount;
                    if (it == null) continue;
                    if (amount > 0 && (it.IsCraftedByPlayer || it.IsBannerItem))
                    {
                        // jawna regula gry (powod techniczny: sztuka gracza to osobny obiekt zapisu) - liczona w linii
                        roster.AddToCounts(el.EquipmentElement, -amount);
                    }
                    else if (el.EquipmentElement.ItemModifier != null)
                    {
                        if (ArmsPiece(it)) { if (amount > 0) _skipStacks++; continue; }   // zbroja nie gnije na straganie - stan sztuki liczy jej zuzycie
                        if (MBRandom.RandomFloat < 0.05f)
                        {
                            roster.AddToCounts(el.EquipmentElement, -1);
                            if (it.ItemType != ItemObject.ItemTypeEnum.Horse) _otherPieces++;
                        }
                    }
                }
            }
            catch (Exception e) { Stumble("DecayPrefix", e); }   // po wyjatku tez bez oryginalu (podwojne kasowanie gorsze niz dzien bez)
            return false;
        }

        // ------------------------------------------------------------ (e) konserwacja zapasu
        /// <summary>Roboczodni kowali miasta na dzis: sztuki uzbrojenia na polce x ArmsStallUpkeepManDaysPerPiece / 364 (pamiec dnia na miasto).</summary>
        internal static float UpkeepManDays(Town town)
        {
            try
            {
                var s = Settings.Current;
                if (town == null || s == null || s.ArmsStallUpkeepManDaysPerPiece <= 0f) return 0f;
                int day = (int)CampaignTime.Now.ToDays;
                KeyValuePair<int, float> c;
                if (_upkeep.TryGetValue(town, out c) && c.Key == day) return c.Value;
                int pieces = 0;
                var r = town.Owner != null ? town.Owner.ItemRoster : null;
                if (r != null)
                    for (int i = 0; i < r.Count; i++)
                    {
                        var el = r.GetElementCopyAtIndex(i);
                        if (el.Amount > 0 && ArmsPiece(el.EquipmentElement.Item)) pieces += el.Amount;
                    }
                float v = pieces * s.ArmsStallUpkeepManDaysPerPiece / 364f;
                _upkeep[town] = new KeyValuePair<int, float>(day, v);
                _upkeepDay += v;
                return v;
            }
            catch (Exception e) { Stumble("UpkeepManDays", e); return 0f; }
        }

        // ------------------------------------------------------------ (d) ksiega uzbrojenia (z ramek GoodsLedger)
        // klucz podsluchu: typ*10 + tier; +1000 odziez "garment"; +2000 wyrob gracza (konie - typ Horse)
        internal static int KeyOf(ItemObject it)
        {
            if (it == null || !SupplyDemand.Equipmentish(it)) return -1;
            int k = (int)it.ItemType * 10 + TierOf(it);
            if (it.IsCraftedByPlayer) k += 2000;
            else if (it.ItemCategory != null && it.ItemCategory.StringId == "garment") k += 1000;
            return k;
        }

        private const int CArms = 0, CGarment = 1, CPlayer = 2, CHorse = 3;

        private static int ClassOf(int key)
        {
            if (key >= 2000) return CPlayer;
            if (key >= 1000) return CGarment;
            if (key / 10 == (int)ItemObject.ItemTypeEnum.Horse) return CHorse;
            return CArms;
        }

        /// <summary>GoodsLedger.Settle: zmiana uzbrojenia na rosterach swiata w zamknietej ramce (suma = zrodlo + / ujscie -).</summary>
        internal static void SettleFrame(int kind, object a, Dictionary<int, int> net, int kinds, int supplyKind, Func<object, MobileParty> partyOf)
        {
            try
            {
                if (_kindOut == null || _kindOut.GetLength(0) != kinds) _kindOut = new long[kinds, 4];
                MobileParty party = null; bool partyKnown = false;
                foreach (var kv in net)
                {
                    if (kv.Value == 0) continue;
                    int c = ClassOf(kv.Key);
                    _kindOut[kind, c] -= kv.Value;   // + = ubylo ze swiata
                    if (c == CArms && kv.Value < 0 && LeakKind(kind)) { long v; _byKey.TryGetValue(kv.Key, out v); _byKey[kv.Key] = v - kv.Value; }
                    if (kind == supplyKind && kv.Value < 0 && c != CHorse)
                    {
                        if (!partyKnown) { partyKnown = true; try { party = partyOf != null ? partyOf(a) : null; } catch { } }
                        bool shield = (kv.Key % 1000) / 10 == (int)ItemObject.ItemTypeEnum.Shield;
                        bool player = party != null && party == MobileParty.MainParty;
                        if (player) { if (shield) _bkPlShield -= kv.Value; else _bkPlWeap -= kv.Value; }
                        else { if (shield) _bkAiShield -= kv.Value; else _bkAiWeap -= kv.Value; }
                    }
                }
            }
            catch (Exception e) { Stumble("SettleFrame", e); }
        }

        // ramki, ktore sa ujsciem (a nie handlem albo zakupem): konsumpcja osad, kasowanie gry, zaopatrzenie BK, ticki BK, BetterEconomy, ticki dobowe
        private static int[] _leakKinds = new int[0];
        internal static void SetLeakKinds(int[] kinds) { _leakKinds = kinds ?? new int[0]; }
        private static bool LeakKind(int kind) { return Array.IndexOf(_leakKinds, kind) >= 0; }

        /// <summary>GoodsLedger.OnPartyDestroyed: uzbrojenie, ktore przepada z rozbita albo rozwiazana partia (tylko licznik).</summary>
        internal static void NoteLost(MobileParty mp)
        {
            try
            {
                var r = mp != null ? mp.ItemRoster : null;
                if (r == null) return;
                for (int i = 0; i < r.Count; i++)
                {
                    var el = r.GetElementCopyAtIndex(i);
                    if (el.Amount > 0 && ArmsPiece(el.EquipmentElement.Item)) _lost += el.Amount;
                }
            }
            catch (Exception e) { Stumble("NoteLost", e); }
        }

        /// <summary>Linia "Uzbrojenie (ujscia 174)" - z GoodsLedger.Daily (ta sama doba ksiegi, ten sam wylacznik GoodsLedgerEnabled).</summary>
        internal static string DailyLine(int day, int stacks, long pieces, string[] kindNames, int fCons, int fDecay, int fSupplyUse, int[] tickKinds)
        {
            try
            {
                var inv = CultureInfo.InvariantCulture;
                var sb = new StringBuilder();
                Func<int, int, long> K = (k, c) => _kindOut != null && k >= 0 && k < _kindOut.GetLength(0) ? _kindOut[k, c] : 0;
                sb.Append("Uzbrojenie (ujscia 174): dzien ").Append(day).Append(" - mieszczanie: uzbrojenie ").Append(K(fCons, CArms)).Append(" szt., odziez (garment) ")
                  .Append(K(fCons, CGarment)).Append(" szt. (miasta i zamki; budzetow uzbrojenia wyzerowanych ").Append(_budgetZero).Append(", ").Append(NotHousehold ? "regula CZYNNA" : "regula wylaczona").Append(')');
                sb.Append("; kasowanie gry: uzbrojenie ").Append(K(fDecay, CArms)).Append(" szt. (pominieto ").Append(_skipStacks).Append(" stosow, oczekiwane ok. ")
                  .Append((_skipStacks * 0.05).ToString("0", inv)).Append(" szt.; miast ").Append(_decayTowns).Append(", ").Append(NoStallDecay ? "regula CZYNNA" : "regula wylaczona - kasuje gra")
                  .Append("), wyroby gracza ").Append(K(fDecay, CPlayer)).Append(" szt., konie ").Append(K(fDecay, CHorse)).Append(" szt., inne z modyfikatorem ").Append(_otherPieces).Append(" szt.");
                sb.Append("; zaopatrzenie BK: partie AI bron ").Append(_bkAiWeap).Append(" / tarcze ").Append(_bkAiShield).Append(" (przeliczen z wynikiem 0 ").Append(BkZeroCalls)
                  .Append(", potrzeb wyzerowanych ").Append(BkReset).Append("), gracz bron ").Append(_bkPlWeap).Append(" / tarcze ").Append(_bkPlShield);
                sb.Append("; przepadlo z rozbitymi partiami ").Append(_lost);
                long ticks = 0; foreach (var tk in tickKinds) ticks += K(tk, CArms);
                sb.Append("; inne ticki ").Append(ticks);
                // pozostale ramki z ruchem uzbrojenia (netto, + = ubylo ze swiata; produkcja warsztatow i strzelarzy ze znakiem -)
                var rest = new List<string>();
                if (_kindOut != null)
                    for (int k = 0; k < _kindOut.GetLength(0); k++)
                    {
                        if (k == fCons || k == fDecay || k == fSupplyUse || Array.IndexOf(tickKinds, k) >= 0) continue;
                        long v = K(k, CArms) + K(k, CGarment);
                        if (v != 0) rest.Add((k < kindNames.Length ? kindNames[k] : "?") + " " + (v > 0 ? "+" : "") + v);
                    }
                sb.Append("; inne ramki (netto, + = ubylo) [").Append(rest.Count > 0 ? string.Join(", ", rest.ToArray()) : "-").Append(']');
                // wedlug typu i tieru - ujscia (ramki konsumpcji, kasowania, BK, tickow), bez koni i wyrobow gracza
                var byType = new SortedDictionary<int, long[]>();
                foreach (var kv in _byKey)
                {
                    long[] t; int ty = kv.Key / 10, tr = kv.Key % 10;
                    if (!byType.TryGetValue(ty, out t)) { t = new long[7]; byType[ty] = t; }
                    if (tr >= 1 && tr <= 6) { t[tr] += kv.Value; t[0] += kv.Value; }
                }
                sb.Append("; ujscia wedlug typu i tieru [");
                bool first = true;
                foreach (var kv in byType)
                {
                    if (kv.Value[0] == 0) continue;
                    if (!first) sb.Append(", ");
                    first = false;
                    sb.Append((ItemObject.ItemTypeEnum)kv.Key).Append(' ').Append(kv.Value[0]).Append(" (");
                    bool f2 = true;
                    for (int t = 1; t <= 6; t++) { if (kv.Value[t] == 0) continue; if (!f2) sb.Append(' '); f2 = false; sb.Append('t').Append(t).Append(' ').Append(kv.Value[t]); }
                    sb.Append(')');
                }
                if (first) sb.Append('-');
                sb.Append(']');
                sb.Append("; stosy uzbrojenia na polkach miast i zamkow ").Append(stacks).Append(" (sztuk ").Append(pieces).Append(')');
                if (_lastStacks >= 0) sb.Append(" (zmiana ").Append(stacks - _lastStacks >= 0 ? "+" : "").Append(stacks - _lastStacks).Append(')');
                sb.Append("; konserwacja ").Append(_upkeepDay.ToString("0.0", inv)).Append(" roboczodni kowali; potkniecia dzis ").Append(_stumbles).Append(" (od wczytania ").Append(_stumblesAll).Append(").");
                _lastStacks = stacks;
                return sb.ToString();
            }
            catch (Exception e) { Stumble("DailyLine", e); return null; }
            finally { NewDay(); }
        }

        private static void NewDay()
        {
            _skipStacks = _otherPieces = _decayTowns = _budgetZero = _stumbles = 0;
            _upkeepDay = 0; _byKey.Clear(); if (_kindOut != null) Array.Clear(_kindOut, 0, _kindOut.Length);
            _bkAiWeap = _bkAiShield = _bkPlWeap = _bkPlShield = _lost = 0; BkZeroCalls = BkReset = 0;
        }

        // ------------------------------------------------------------ wpiecie i linia startowa
        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var m = AccessTools.Method(typeof(ItemConsumptionBehavior), "DeleteOverproducedItems");
                if (m != null && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(Town))
                {
                    h.Patch(m, prefix: new HarmonyMethod(typeof(ArmsLeaks), nameof(DecayPrefix)));
                    DecayHooked = true;
                }
                Log.Info("ArmsLeaks (174.0): kasowanie 5% stosow (DeleteOverproducedItems) - " + (DecayHooked ? "latka wpieta" : "BRAK latki (gra kasuje jak dotad)") + ".");
            }
            catch (Exception e) { Log.Error("ArmsLeaks.ApplyAll", e); }
        }

        /// <summary>Linia startowa (OnSessionLaunched, po McmSettings.Apply).</summary>
        internal static void SessionStart()
        {
            try
            {
                var s = Settings.Current;
                if (s == null) return;
                Log.Info("ArmsLeaks (174.0): mieszczanie bez uzbrojenia " + (s.ArmsNotHouseholdGoods ? "CZYNNE" : "wylaczone") + " (BK CalculateBudget " + (HistoricalPrices.BudgetHooked ? "wpiety" : "BRAK")
                         + "; miasta i zamki); kasowanie 5% stosow uzbrojenia " + (s.ArmsNoStallDecay ? (DecayHooked ? "WYLACZONE (latka wpieta)" : "BRAK latki") : "jak w grze (MCM)")
                         + "; BK bron i tarcze partii AI = 0 - " + (s.BkSuppliesNoArms ? (BkSupplyTemper.ArmsHooked ? "wpiete" : "BRAK") : "wylaczone w MCM")
                         + " (zerowanie zapisanej potrzeby: " + (BkSupplyTemper.ArmsResetReady && BkSupplyTemper.NeedsBuyHooked ? "wpiete" : "BRAK") + "; wymaga zakupow AI - " + (AiGear.On ? "czynne" : "WYLACZONE, wiec BK jak dotad") + ")"
                         + "; gracz: " + (s.BkSuppliesNoArmsPlayer ? "bron, tarcze i strzaly BK = 0" : "BK jak dotad") + "; konserwacja zapasu "
                         + s.ArmsStallUpkeepManDaysPerPiece.ToString("0.###", CultureInfo.InvariantCulture) + " roboczodnia na sztuke na rok.");
            }
            catch (Exception e) { Log.Error("ArmsLeaks.SessionStart", e); }
        }
    }
}
