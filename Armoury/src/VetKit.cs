using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// C3 (Jeff 14.10 "tak"; projekt etapu 4 docs/etap4/PLAN-KODU-E410.md rozdz. 6 i pytanie 2, PRZEKAZANIE rozdz. 25) - KOMPLET WETERANA. Klucz VeteranTakesKit.
    ///
    /// Bylo: zolnierz, ktory odchodzi do karczmy (zwolniony 166, przepelnienie W4c, rycerz 179, rozbity H3, rozwiazana partia - strumienie, w ktorych odchodzi
    /// ze swoim dobytkiem, PeopleBook.VSrcHorse), bral sakiewke (VetPurse) i konia z rzedem (HorseFate.VetTakeHorses), a zbroja i bron zostawaly w zbrojowni
    /// pana; najemnik z tej karczmy przychodzil do nowego pana z dobytkiem tieru 1 z domu (RecruitKit.OwnOf - z niczego).
    /// Teraz ta sama droga co kon (jedna regula):
    ///  - WEJSCIE (PeopleBook.VetCommit): t ludzi typu X bierze ze zbrojowni DTE partii swoje komplety bez koni i rzedow - regula A7 (GarrisonArmory.KitFor:
    ///    udzial wedlug typu, gdy zbrojownia ma braki - proporcjonalnie do ludzi, ktorzy zostaja; sztuka wzorca, potem ten sam tier, wyzsze, nizsze; bez unikatow)
    ///    - do puli kompletow miasta karczmy razem ze stanem sztuk (zapis obitych AiWear.TakeWornShare). Recenzja C3 (Jeff "tak" - takze u AI, jedna regula):
    ///    zolnierze druzyny gracza (do karczmy trafiaja jako rozbici H3 - takze z WYGRANEJ bitwy, bo gra zdejmuje uciekajacych z rosteru) biora swoje komplety
    ///    z czesci LUDZI zbrojowni druzyny DTE tak samo jak przy przeniesieniu ludzi do zalogi (GarrisonArmory.KitsToGarrison: Twoja ksiega nietknieta,
    ///    bez stanow na plus - zostaja w druzynie); kon gracza - bez zmian (HorseFate: zbrojownia gracza nietknieta przy koniu, osobna regula E6/K1).
    ///  - NAJEM (RecruitKit przypadek 7 - AI i gracz, PlayerMerc): najemnik bierze komplet swojego typu z puli tej karczmy; rodzaje, ktore dostal z puli,
    ///    zastepuja dobytek z domu (OwnOf) - nie dwa razy; reszta dobytku jak dotad. Sztuki ze stanem (obite) ida obite.
    ///  - DO DOMU (raz na dobe): sztuki ponad tych, ktorzy dalej czekaja w karczmie (na rodzaj pancerza i tarcze najwyzej 1 na czlowieka, bron i pociski najwyzej
    ///    4 na czlowieka) - od najtanszych - na polke targu miasta (weteran odszedl do domu albo najal go ktos bez puli: zostawil dobytek w miescie, jak kon).
    ///    Wylaczony klucz - cala pula na polki (nic w nicosc).
    /// Stan puli w zapisie (arm_vetkit) i w ksiedze uzbrojenia E6/0 (ArmsBook - czesc stanu, przeniesienie zbrojownia -> pula -> zbrojownia / polka).
    /// </summary>
    internal static class VetKit
    {
        internal static bool On { get { var s = Settings.Current; return s != null && s.VeteranTakesKit && PeopleBook.VetLive && AiGear.Armories() != null; } }

        // id miasta -> przedmiot -> id modyfikatora ("" = sprawna) -> sztuk
        private static readonly Dictionary<string, Dictionary<ItemObject, Dictionary<string, int>>> _pool = new Dictionary<string, Dictionary<ItemObject, Dictionary<string, int>>>();
        private static string _pending;
        private static long _poolPrev = -1;

        // liczniki doby
        private static int _dMen, _dMenNoKit, _dPlayer, _dNoArm, _dHireMen, _dHireNoKit, _dTowns, _stumbles, _dStumbles;
        private static long _dIn, _dOut, _dOwnSwapped, _dHome, _dPlayerPcs, _dPlayerPlus;
        private static readonly HashSet<string> _errWhere = new HashSet<string>();

        internal static void Reset()
        {
            _pool.Clear(); _pending = null; _poolPrev = -1; _stumbles = 0; _errWhere.Clear();
            ClearDay();
        }

        private static void ClearDay()
        {
            _dMen = _dMenNoKit = _dPlayer = _dNoArm = _dHireMen = _dHireNoKit = _dTowns = _dStumbles = 0;
            _dIn = _dOut = _dOwnSwapped = _dHome = _dPlayerPcs = _dPlayerPlus = 0;
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _dStumbles++;
            try { if (_errWhere.Add(where)) Log.Error("VetKit." + where, e); } catch { }
        }

        private static bool Kit(ItemObject it)
        {
            return it != null && SupplyDemand.Equipmentish(it) && !MenPurse.HorseKind(it) && !ArmsPricing.IsUnique(it);
        }

        private static Dictionary<ItemObject, Dictionary<string, int>> PoolOf(string town, bool make)
        {
            EnsureLoaded();
            Dictionary<ItemObject, Dictionary<string, int>> p;
            if (town == null) return null;
            if (!_pool.TryGetValue(town, out p) && make) { p = new Dictionary<ItemObject, Dictionary<string, int>>(); _pool[town] = p; }
            return p;
        }

        private static void Put(Dictionary<ItemObject, Dictionary<string, int>> p, ItemObject it, string mod, int n)
        {
            if (n <= 0) return;
            Dictionary<string, int> m;
            if (!p.TryGetValue(it, out m)) { m = new Dictionary<string, int>(); p[it] = m; }
            int c; m.TryGetValue(mod ?? "", out c); m[mod ?? ""] = c + n;
        }

        private static int Count(Dictionary<string, int> m) { int n = 0; foreach (var v in m.Values) n += Math.Max(0, v); return n; }

        /// <summary>Zdejmuje n sztuk przedmiotu z puli (najpierw sprawne, potem obite) - zwraca sztuki ze stanem.</summary>
        private static List<EquipmentElement> Pull(Dictionary<ItemObject, Dictionary<string, int>> p, ItemObject it, int n)
        {
            var got = new List<EquipmentElement>();
            Dictionary<string, int> m;
            if (n <= 0 || !p.TryGetValue(it, out m)) return got;
            foreach (var mod in m.Keys.OrderBy(k => k.Length == 0 ? 0 : 1).ToList())
            {
                if (n <= 0) break;
                int c = m[mod], k = Math.Min(c, n);
                if (k <= 0) continue;
                ItemModifier im = null;
                if (mod.Length > 0) { try { im = MBObjectManager.Instance.GetObject<ItemModifier>(mod); } catch { im = null; } }
                for (int i = 0; i < k; i++) got.Add(new EquipmentElement(it, im));
                if (c - k > 0) m[mod] = c - k; else m.Remove(mod);
                n -= k;
            }
            if (m.Count == 0) p.Remove(it);
            return got;
        }

        // ------------------------------------------------------------ wejscie (PeopleBook.VetCommit)
        /// <summary>t ludzi typu troop odchodzi z partii do karczmy miasta town - bierze swoje komplety ze zbrojowni DTE partii (bez koni i rzedow - HorseFate).
        /// inRoster - odchodzacy sa jeszcze w rosterze partii (166, W4c, 179, rozwiazane); false - juz zeszli (rozbici H3).</summary>
        internal static void Take(MobileParty party, CharacterObject troop, Settlement town, int n, bool inRoster)
        {
            if (n <= 0 || troop == null || town == null || town.StringId == null || !On) return;
            try
            {
                if (party == null) { _dNoArm += n; return; }
                if (party.IsMainParty) { TakePlayer(party, troop, town, n, inRoster); return; }   // recenzja C3: ta sama regula z czesci ludzi zbrojowni druzyny
                var all = AiGear.Armories();
                Dictionary<ItemObject, int> arm;
                if (all == null || !all.TryGetValue(party.Id, out arm) || arm == null || arm.Count == 0) { _dNoArm += n; return; }
                var avail = new Dictionary<ItemObject, int>();
                foreach (var kv in arm) if (kv.Value > 0 && Kit(kv.Key)) avail[kv.Key] = kv.Value;
                var taken = GarrisonArmory.KitFor(avail, troop, n, GarrisonArmory.NeedLeftWithout(party, troop, inRoster ? n : 0));
                var p = PoolOf(town.StringId, true);
                long pcs = 0; bool synced = false;
                foreach (var kv in taken)
                {
                    if (kv.Key == null || kv.Value <= 0) continue;
                    int c; if (!arm.TryGetValue(kv.Key, out c) || c <= 0) continue;
                    int k = Math.Min(kv.Value, c);
                    // stan sztuk idzie z ludzmi (udzial obitych jak przy przeniesieniu do zalogi) - przed zdjeciem ze slownika dawcy
                    var worn = AiWear.TakeWornShare(party, kv.Key, k, c, !synced); synced = true;
                    int wn = 0;
                    foreach (var w in worn) { if (w.Key == null || w.Value <= 0) continue; int x = Math.Min(w.Value, k - wn); if (x <= 0) break; Put(p, kv.Key, w.Key.StringId, x); wn += x; }
                    Put(p, kv.Key, "", k - wn);
                    if (c - k > 0) arm[kv.Key] = c - k; else arm.Remove(kv.Key);
                    pcs += k;
                }
                if (p.Count == 0) _pool.Remove(town.StringId);
                _dIn += pcs;
                if (pcs > 0) _dMen += n; else _dMenNoKit += n;
            }
            catch (Exception e) { Stumble("Take", e); }
        }

        /// <summary>Recenzja C3: zolnierze druzyny gracza (rozbici H3) - komplety z czesci LUDZI zbrojowni druzyny DTE (ArmyArmory), ta sama droga co ludzie
        /// przenoszeni ekranem do zalogi (GarrisonArmory.KitsToGarrison): Twoja ksiega na najgorszych egzemplarzach nietknieta, ludzie biora najlepsze;
        /// bez koni, rzedow i unikatow; sztuki ze stanem na plus zostaja w druzynie (zbrojownia AI ich nie zna - stracilyby wartosc u nowego pana);
        /// obita sztuka idzie do puli ze swoim stanem. Przy pojmaniu (C4) zbrojownia jest juz u zdobywcy - wtedy nic do wziecia.</summary>
        private static void TakePlayer(MobileParty main, CharacterObject troop, Settlement town, int n, bool inRoster)
        {
            var armory = QuartermasterLaw.DteArmory();
            if (armory == null || QuartermasterEscrow.Active) { _dNoArm += n; return; }
            var avail = new Dictionary<ItemObject, int>();
            var copies = new Dictionary<ItemObject, List<SwapMath.Piece>>();
            foreach (var type in QuartermasterLaw.KitTypes)
            {
                if (type == ItemObject.ItemTypeEnum.Horse || type == ItemObject.ItemTypeEnum.HorseHarness) continue;   // kon i rzad - HorseFate (gracz bez zmian)
                foreach (var pc in QuartermasterLaw.KitPieces(armory, type, true))   // own = Twoja ksiega na najgorszych egzemplarzach id
                {
                    var el = QuartermasterLaw.ElOf(pc); var it = el.Item; var m = el.ItemModifier;
                    if (it == null || pc.MenTotal <= 0 || !Kit(it)) continue;
                    if (m != null && m.PriceMultiplier > 1f) { _dPlayerPlus += pc.MenTotal; continue; }
                    int v; avail.TryGetValue(it, out v); avail[it] = v + pc.MenTotal;
                    List<SwapMath.Piece> l; if (!copies.TryGetValue(it, out l)) copies[it] = l = new List<SwapMath.Piece>(); l.Add(pc);
                }
            }
            var taken = GarrisonArmory.KitFor(avail, troop, n, GarrisonArmory.NeedLeftWithout(main, troop, inRoster ? n : 0));
            var p = PoolOf(town.StringId, true);
            long pcs = 0;
            foreach (var kv in taken)
            {
                List<SwapMath.Piece> l;
                if (kv.Key == null || kv.Value <= 0 || !copies.TryGetValue(kv.Key, out l)) continue;
                l.Sort((a, b) => SwapMath.WorseFirst(b, a));   // najlepsze egzemplarze ludzi najpierw (Twoja czesc to najgorsze) - jak KitsToGarrison
                int left = kv.Value;
                foreach (var pc in l)
                {
                    if (left <= 0) break;
                    int k = Math.Min(left, pc.MenTotal);
                    if (k <= 0) continue;
                    var el = QuartermasterLaw.ElOf(pc);
                    armory.AddToCounts(el, -k);
                    Put(p, kv.Key, el.ItemModifier != null ? el.ItemModifier.StringId : "", k);
                    left -= k; pcs += k;
                }
            }
            if (p.Count == 0) _pool.Remove(town.StringId);
            _dIn += pcs; _dPlayer += n; _dPlayerPcs += pcs;
            if (pcs > 0) _dMen += n; else _dMenNoKit += n;
        }

        // ------------------------------------------------------------ najem (RecruitKit przypadek 7, PlayerMerc)
        /// <summary>Jeden najemnik typu troop z karczmy miasta town: komplet z puli (sztuki ze stanem) zastepuje dobytek z domu tych samych rodzajow w items.</summary>
        internal static void Hire(Settlement town, CharacterObject troop, List<EquipmentElement> items)
        {
            if (town == null || town.StringId == null || troop == null || items == null || !On) return;
            try
            {
                var p = PoolOf(town.StringId, false);
                if (p == null || p.Count == 0) { _dHireNoKit++; return; }
                var avail = new Dictionary<ItemObject, int>();
                foreach (var kv in p) { int c = Count(kv.Value); if (c > 0) avail[kv.Key] = c; }
                var taken = GarrisonArmory.KitFor(avail, troop, 1, null);
                var got = new List<EquipmentElement>();
                foreach (var kv in taken) if (kv.Key != null && kv.Value > 0) got.AddRange(Pull(p, kv.Key, kv.Value));
                if (p.Count == 0) _pool.Remove(town.StringId);
                if (got.Count == 0) { _dHireNoKit++; return; }
                var types = new HashSet<ItemObject.ItemTypeEnum>();
                foreach (var e in got) types.Add(e.Item.ItemType);
                int before = items.Count;
                items.RemoveAll(e => e.Item != null && !MenPurse.HorseKind(e.Item) && types.Contains(e.Item.ItemType));
                _dOwnSwapped += before - items.Count;
                items.AddRange(got);
                _dOut += got.Count; _dHireMen++;
            }
            catch (Exception e) { Stumble("Hire", e); }
        }

        // ------------------------------------------------------------ do domu (raz na dobe)
        private static int Group(ItemObject it)
        {
            switch (it.ItemType)
            {
                case ItemObject.ItemTypeEnum.HeadArmor: return 0;
                case ItemObject.ItemTypeEnum.BodyArmor: return 1;
                case ItemObject.ItemTypeEnum.LegArmor: return 2;
                case ItemObject.ItemTypeEnum.HandArmor: return 3;
                case ItemObject.ItemTypeEnum.Cape: return 4;
                default: return 5;   // bron, pociski, tarcza - 4 sloty broni
            }
        }

        private static Settlement MarketOf(Settlement st)
        {
            if (st == null) return null;
            if (st.IsVillage && st.Village != null) st = st.Village.Bound;
            return st != null && st.Town != null && st.ItemRoster != null ? st : null;
        }

        internal static void Daily()
        {
            EnsureLoaded();
            int day = 0; try { day = (int)CampaignTime.Now.ToDays; } catch { }
            bool on = On;
            try
            {
                var drop = new List<string>();
                foreach (var kv in _pool)
                {
                    try
                    {
                        var st = Settlement.Find(kv.Key);
                        var m = MarketOf(st);
                        if (m == null) continue;   // miasto nieznane - pula czeka (stan w ksiedze)
                        int men = 0;
                        if (on) { int mounted, old; men = PeopleBook.VetInTown(st, out mounted, out old); }
                        var cap = new[] { men, men, men, men, men, 4 * men };
                        var have = new int[6];
                        foreach (var x in kv.Value) have[Group(x.Key)] += Count(x.Value);
                        bool moved = false;
                        var cheap = new List<ItemObject>(kv.Value.Keys);
                        cheap.Sort((a, b) => a.Value.CompareTo(b.Value));
                        foreach (var it in cheap)
                        {
                            int g = Group(it), ex = have[g] - cap[g];
                            if (ex <= 0) continue;
                            int k = Math.Min(ex, Count(kv.Value[it]));
                            foreach (var e in Pull(kv.Value, it, k)) m.ItemRoster.AddToCounts(e, 1);
                            have[g] -= k; _dHome += k; moved = true;
                        }
                        if (moved) _dTowns++;
                        if (kv.Value.Count == 0) drop.Add(kv.Key);
                    }
                    catch (Exception e) { Stumble("Daily(miasto)", e); }
                }
                foreach (var k in drop) _pool.Remove(k);
            }
            catch (Exception e) { Stumble("Daily", e); }
            try
            {
                long now = 0; int towns = 0;
                foreach (var p in _pool.Values) { long t = 0; foreach (var x in p.Values) t += Count(x); now += t; if (t > 0) towns++; }
                var s = Settings.Current;
                if (s != null && s.VeteranTakesKit || now > 0 || _dIn + _dOut + _dHome > 0)
                {
                    string ctl = _poolPrev < 0 ? "pula na starcie sesji " + now
                        : "kontrola: pula wczoraj " + _poolPrev + " + weszlo " + _dIn + " - najem " + _dOut + " - do domu " + _dHome + " = " + (_poolPrev + _dIn - _dOut - _dHome)
                          + " (teraz " + now + ", roznica " + (now - (_poolPrev + _dIn - _dOut - _dHome)) + ")";
                    Log.Info("Komplet weterana (C3): dzien " + day + " | " + (on ? "CZYNNE" : "wylaczone (VeteranTakesKit / weterani) - pula na polki")
                             + " | weszlo do karczm: ludzi z kompletem " + _dMen + ", sztuk " + _dIn + "; bez kompletu - zbrojownia bez ich rodzajow " + _dMenNoKit
                             + ", bez zbrojowni (jency, rozbici bez partii) " + _dNoArm + "; z druzyny gracza ludzi " + _dPlayer + ", sztuk " + _dPlayerPcs
                             + " (stan na plus zostal w druzynie " + _dPlayerPlus + ")"
                             + " | najem: ludzi z kompletem z puli " + _dHireMen + ", sztuk " + _dOut + " (zastapiony dobytek z domu " + _dOwnSwapped + "), bez kompletu w puli " + _dHireNoKit
                             + " | do domu (na polki miast): " + _dHome + " szt. z " + _dTowns + " miast | pula " + now + " szt. w " + towns + " miastach | " + ctl
                             + " | potkniecia " + _dStumbles + " (razem " + _stumbles + ").");
                }
                _poolPrev = now;
            }
            catch (Exception e) { Stumble("Line", e); }
            ClearDay();
        }

        /// <summary>Ksiega uzbrojenia E6/0: sztuki w pulach kompletow weteranow (czesc stanu).</summary>
        internal static void ForEach(Action<ItemObject, int> f)
        {
            EnsureLoaded();
            foreach (var p in _pool.Values) foreach (var kv in p) { int c = Count(kv.Value); if (c > 0 && kv.Key != null) f(kv.Key, c); }
        }

        // ------------------------------------------------------------ zapis (arm_vetkit)
        internal static string Export()
        {
            EnsureLoaded();
            var sb = new StringBuilder();
            foreach (var kv in _pool)
            {
                if (kv.Value.Count == 0 || kv.Key.IndexOf(':') >= 0) continue;
                sb.Append("k:").Append(kv.Key).Append(':');
                bool first = true;
                foreach (var x in kv.Value)
                    foreach (var m in x.Value)
                    {
                        if (x.Key == null || m.Value <= 0) continue;
                        if (!first) sb.Append(',');
                        sb.Append(x.Key.StringId).Append('|').Append(m.Key).Append('=').Append(m.Value);
                        first = false;
                    }
                sb.Append('\n');
            }
            return sb.ToString();
        }

        internal static void Import(string data) { _pool.Clear(); _poolPrev = -1; _pending = data; }

        private static void EnsureLoaded()
        {
            if (_pending == null) return;
            string data = _pending; _pending = null;
            try
            {
                foreach (var line in data.Split('\n'))
                {
                    if (!line.StartsWith("k:")) continue;
                    int a = line.IndexOf(':', 2);
                    if (a < 0) continue;
                    string town = line.Substring(2, a - 2);
                    var p = new Dictionary<ItemObject, Dictionary<string, int>>();
                    foreach (var pair in line.Substring(a + 1).Split(','))
                    {
                        int e = pair.IndexOf('='), b = pair.IndexOf('|');
                        if (e <= 0 || b <= 0 || b > e) continue;
                        int n;
                        if (!int.TryParse(pair.Substring(e + 1), out n) || n <= 0) continue;
                        var it = MBObjectManager.Instance.GetObject<ItemObject>(pair.Substring(0, b));
                        if (it == null) continue;
                        Put(p, it, pair.Substring(b + 1, e - b - 1), n);
                    }
                    if (p.Count > 0) _pool[town] = p;
                }
            }
            catch (Exception e) { Stumble("EnsureLoaded", e); }
        }
    }
}
