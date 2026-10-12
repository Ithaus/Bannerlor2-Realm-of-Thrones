using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// ZUZYCIE I NAPRAWY SPRZETU AI (wpis 85; Jeff 05.10: "jak AI naprawia sprzet zepsuty i ile to trwa?" - nie naprawialo,
    /// bo nic sie nie zuzywalo). Zbrojownia DTE lorda AI to lista "przedmiot: ile" bez stanu - obok prowadzimy wlasny zapis:
    /// ile sztuk danego przedmiotu jest obitych i w jakim stanie (modyfikator gry). Reszta = sprawna.
    ///  - Po bitwie (wygranej albo przegranej, kto przezyl): BattleWear - TroopWearPercent sztuk w uzyciu dostaje zuzycie walki;
    ///    lup, ktory DTE dolozyl do zbrojowni (przybytek od ostatniego spisu po bitwie), wchodzi obity jak lup gracza.
    ///  - Zakupy AiGear i komplety rekrutow - sprawne (przybytek bez bitwy).
    ///  - W miescie, raz dziennie (wejscie + dzienny tick postoju): kowale miasta naprawiaja tyle, ile zdaza (rece rzemieslnikow
    ///    x udzial platnerzy i miecznikow x 18 h pracy / 0.6 h na sztuke), placi sakiewka ludzi do kasy miasta. Wraki zostaja.
    ///  - Sprzedaz nadwyzek (MenPurse) bierze najgorsze obite sztuki ze swoim stanem; sprawne ida jako sprawne.
    /// </summary>
    internal static class AiWear
    {
        private static bool On { get { var s = Settings.Current; return s != null && s.AiWearEnabled; } }

        // partia -> przedmiot -> modyfikator -> ile obitych
        private static readonly Dictionary<string, Dictionary<string, Dictionary<string, int>>> _worn = new Dictionary<string, Dictionary<string, Dictionary<string, int>>>();
        // partia -> przedmiot -> ile bylo w zbrojowni przy ostatnim spisie
        private static readonly Dictionary<string, Dictionary<string, int>> _known = new Dictionary<string, Dictionary<string, int>>();
        private static readonly HashSet<string> _battleSince = new HashSet<string>();
        private static readonly Dictionary<string, int> _lastMend = new Dictionary<string, int>();
        private static int _dayLoot, _dayWorn, _dayMended, _dayPaid, _dayStamp = -1;
        // poprawka po audycie TOWARY 3 (krok 139 planu K13): material napraw AI z polek miast - liczniki doby (tylko log)
        private static int _dayMat, _dayWait, _dayWaitMask, _dayNoSmith, _dayTowns;
        private static int _dayByKind, _dayOldRule;    // T3 (noc 08/09.10): naprawione sztuki z metalem w recepturze - wedlug rodzaju / stara regula (kolczuga, nieznane, wylacznik)
        private static float _dayKgByKind, _dayKgOldRule;   // T3: kg metalu (surowki) tych sztuk - osobno, zeby sklad napraw nie mylil testu
        private static readonly float[] _dayKg = new float[MendMaterial.Kinds];
        private static readonly int[] _dayWaitBy = new int[MendMaterial.Kinds];
        // E6/N1 (217, R16): partia -> (doba, przedmiot -> ile sztuk nosza ludzie) - dopasowanie raz na dobe (naprawy liczone swiezo, rezerwa sakiewki z pamieci)
        private static readonly Dictionary<string, KeyValuePair<int, Dictionary<string, int>>> _menUsed = new Dictionary<string, KeyValuePair<int, Dictionary<string, int>>>();
        private static int _daySkipLoot, _dayFolk;   // E6/N1: obite sztuki pominiete (lup nienoszony) w zleceniach doby; robocizna do kiesy ludu (tylko log)
        private static int _usedErr;                 // E6/N1: potkniecia dopasowania (do logu najwyzej 3; partia wraca wtedy do starej reguly - wszystkie obite)

        private static bool WornOnlyOn { get { var s = Settings.Current; return s != null && s.AiMendsWornOnly; } }

        // E8/P2 (244, PLAN 8.7a, 8.8c, 8.4a): pomiary doby - tylko liczniki i linia "Zuzycie AI - pomiary (E8/P2)" (Stage8MeasureLog); gra bez zmian
        private static bool P2On { get { var s = Settings.Current; return s != null && s.Stage8MeasureLog; } }
        private static int _p2LootG, _p2WornG, _p2MendG, _p2LootL, _p2WornL, _p2MendL;   // zalogi (G) i pozostale partie (L) liczone osobno - suma ma dac liczby linii "Zuzycie AI"
        private static int _p2Visits, _p2Jobs, _p2PurseP, _p2PurseN, _p2HoursP, _p2HoursN, _p2MatP, _p2OtherN;   // zlecenia napraw: partie i sztuki, ktore stanely - wedlug powodu
        private static long _p2PurseLeft;                                                  // reszta sakiewek partii, ktorych naprawy stanely na sakiewce
        private static readonly float[] _p2AmmoGone = new float[3];                       // "zeszloby": strzaly, belty, miotane (ulamki sumowane, bez losowania)
        private static readonly int[] _p2AmmoUse = new int[3];                            // sztuki pociskow w uzyciu u partii po bitwie
        private static float _p2AmmoWin, _p2AmmoLose, _p2IntSum;
        private static int _p2AmmoParties, _p2IntN, _p2Stumbles;

        private static void P2Clear()
        {
            _p2LootG = _p2WornG = _p2MendG = _p2LootL = _p2WornL = _p2MendL = 0;
            _p2Visits = _p2Jobs = _p2PurseP = _p2PurseN = _p2HoursP = _p2HoursN = _p2MatP = _p2OtherN = 0; _p2PurseLeft = 0;
            Array.Clear(_p2AmmoGone, 0, 3); Array.Clear(_p2AmmoUse, 0, 3); _p2AmmoWin = _p2AmmoLose = _p2IntSum = 0f; _p2AmmoParties = _p2IntN = 0;
        }

        internal static void Reset()
        {
            _worn.Clear(); _known.Clear(); _battleSince.Clear(); _lastMend.Clear(); _dayLoot = _dayWorn = _dayMended = _dayPaid = 0; _dayStamp = -1;
            _menUsed.Clear(); _daySkipLoot = _dayFolk = _usedErr = 0;
            ClearMatDay();
            P2Clear(); _p2Stumbles = 0;   // E8/P2
        }

        private static void ClearMatDay()
        {
            _dayMat = _dayWait = _dayWaitMask = _dayNoSmith = _dayTowns = 0; _dayByKind = _dayOldRule = 0; _dayKgByKind = _dayKgOldRule = 0f;
            Array.Clear(_dayKg, 0, _dayKg.Length); Array.Clear(_dayWaitBy, 0, _dayWaitBy.Length);
        }

        internal static string Export()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var p in _worn)
                foreach (var it in p.Value)
                    foreach (var m in it.Value)
                        if (m.Value > 0) sb.Append(p.Key).Append(',').Append(it.Key).Append(',').Append(m.Key).Append(',').Append(m.Value).Append(';');
            return sb.ToString();
        }

        internal static void Import(string s)
        {
            _worn.Clear(); _known.Clear(); _battleSince.Clear();
            if (string.IsNullOrEmpty(s)) return;
            foreach (var rec in s.Split(';'))
            {
                var a = rec.Split(','); int n;
                if (a.Length != 4 || !int.TryParse(a[3], out n) || n <= 0) continue;
                AddWorn(a[0], a[1], a[2], n);
            }
        }

        private static void AddWorn(string p, string item, string mod, int n)
        {
            Dictionary<string, Dictionary<string, int>> byItem;
            if (!_worn.TryGetValue(p, out byItem)) _worn[p] = byItem = new Dictionary<string, Dictionary<string, int>>();
            Dictionary<string, int> byMod;
            if (!byItem.TryGetValue(item, out byMod)) byItem[item] = byMod = new Dictionary<string, int>();
            int v; byMod.TryGetValue(mod, out v); v += n;
            if (v > 0) byMod[mod] = v; else byMod.Remove(mod);
            if (byMod.Count == 0) byItem.Remove(item);
            if (byItem.Count == 0) _worn.Remove(p);
        }

        private static int WornOf(string p, string item)
        {
            Dictionary<string, Dictionary<string, int>> byItem; Dictionary<string, int> byMod;
            if (!_worn.TryGetValue(p, out byItem) || !byItem.TryGetValue(item, out byMod)) return 0;
            int n = 0; foreach (var v in byMod.Values) n += v; return n;
        }

        private static ItemModifier Mod(string id) { try { return MBObjectManager.Instance.GetObject<ItemModifier>(id); } catch { return null; } }

        private static Dictionary<ItemObject, int> Armory(MobileParty mp)
        {
            var d = AiGear.Armories(); Dictionary<ItemObject, int> a;
            return d != null && mp != null && d.TryGetValue(mp.Id, out a) ? a : null;
        }

        /// <summary>Spis zbrojowni: przybytek po bitwie = lup (obity), bez bitwy = sprawny; ubytki przycinaja zapis obitych.</summary>
        private static void Sync(MobileParty mp)
        {
            var arm = Armory(mp);
            if (arm == null) return;
            string p = mp.StringId;
            Dictionary<string, int> known;
            bool first = !_known.TryGetValue(p, out known);
            if (first) _known[p] = known = new Dictionary<string, int>();
            bool battle = _battleSince.Remove(p);
            var seen = new HashSet<string>();
            foreach (var kv in arm)
            {
                if (kv.Key == null || !SupplyDemand.Equipmentish(kv.Key) || ArmouryBehavior.NoWear(kv.Key) || MenPurse.HorseKind(kv.Key)) continue;
                string id = kv.Key.StringId; seen.Add(id);
                int was; known.TryGetValue(id, out was);
                int now = Math.Max(0, kv.Value);
                if (!first && battle && now > was)
                    for (int k = 0; k < now - was; k++)
                    {
                        var m = ArmouryBehavior.PickWornModifier(kv.Key);
                        if (m != null) { AddWorn(p, id, m.StringId, 1); _dayLoot++; if (mp.IsGarrison) _p2LootG++; else _p2LootL++; }   // E8/P2: zalogi osobno (tylko licznik)
                    }
                // ubytek (polegli, sprzedaz): obitych nie wiecej niz sztuk
                int w = WornOf(p, id);
                if (w > now) TrimWorn(p, id, w - now);
                known[id] = now;
            }
            foreach (var id in known.Keys.ToList()) if (!seen.Contains(id)) { known.Remove(id); int w = WornOf(p, id); if (w > 0) TrimWorn(p, id, w); }
        }

        private static void TrimWorn(string p, string item, int n)
        {
            Dictionary<string, Dictionary<string, int>> byItem; Dictionary<string, int> byMod;
            if (!_worn.TryGetValue(p, out byItem) || !byItem.TryGetValue(item, out byMod)) return;
            foreach (var m in byMod.Keys.ToList()) { if (n <= 0) break; int c = Math.Min(n, byMod[m]); AddWorn(p, item, m, -c); n -= c; }
        }

        /// <summary>wpis 89: kupiona sztuka ze stanem z polki - obita zostaje obita (dotad AI kupowalo tanio zuzyte i mialo je jako nowe).</summary>
        internal static void NoteBought(MobileParty mp, EquipmentElement el, int n)
        {
            if (!On || mp == null || el.Item == null) return;
            NoteSound(mp, el.Item, n);
            var m = el.ItemModifier;
            if (m != null && m.PriceMultiplier < 1f && !MenPurse.HorseKind(el.Item) && !ArmouryBehavior.NoWear(el.Item)) AddWorn(mp.StringId, el.Item.StringId, m.StringId, n);
        }

        internal static void Forget(MobileParty mp)
        {
            if (mp == null) return;
            string p = mp.StringId; _worn.Remove(p); _known.Remove(p); _battleSince.Remove(p); _lastMend.Remove(p); _menUsed.Remove(p);
        }

        /// <summary>171 (recenzja): czy zapis zuzycia jest prowadzony - zalogi sprzedaja nadwyzke ze stanem z zapisu tylko wtedy.</summary>
        internal static bool BookOn { get { return On; } }

        /// <summary>
        /// 171 (recenzja, jedna regula zuzycia dla zalog): n sztuk przedmiotu przechodzi z from do to (partia lorda <-> zaloga, rozwiazana partia) - razem
        /// z nimi czesc zapisu obitych: udzial proporcjonalny (nie wiemy, ktorzy ludzie maja obite), ale nie mniej niz trzeba, zeby u dawcy obitych nie bylo
        /// wiecej niz sztuk, i nie wiecej niz n. Wolac PRZED zdjeciem sztuk ze zbrojowni dawcy (countBefore = ile mial). Dotad sztuki z zalogi wchodzily
        /// jako sprawne, a obite lorda w zalodze stawaly sie sprawne (naprawa bez kowala i materialu).
        /// </summary>
        internal static void MoveWorn(MobileParty from, MobileParty to, ItemObject it, int n, int countBefore, bool sync = true)
        {
            if (!On || from == null || to == null || it == null || n <= 0) return;
            if (sync) Sync(from);   // sync = false: spis dawcy zrobiony juz w tym samym przeniesieniu (Known trzyma go w zgodzie)
            NoteSound(to, it, n);   // przybytek u odbiorcy to nie lup (spis od razu, gdy odbiorca ma spis)
            if (ArmouryBehavior.NoWear(it) || MenPurse.HorseKind(it)) return;
            string pf = from.StringId, id = it.StringId;
            int w = WornOf(pf, id);
            if (w > 0)
            {
                int c = Math.Max(1, countBefore);
                int k = (int)Math.Round(w * (double)n / c);
                k = Math.Max(k, w - Math.Max(0, countBefore - n));
                k = Math.Min(k, Math.Min(w, n));
                Dictionary<string, Dictionary<string, int>> byItem; Dictionary<string, int> byMod;
                if (k > 0 && _worn.TryGetValue(pf, out byItem) && byItem.TryGetValue(id, out byMod))
                    foreach (var m in byMod.Keys.ToList())
                    {
                        if (k <= 0) break;
                        int t = Math.Min(k, byMod[m]);
                        AddWorn(pf, id, m, -t); AddWorn(to.StringId, id, m, t);
                        k -= t;
                    }
            }
            Known(from, it, -n);
        }

        /// <summary>
        /// Poprawki sklad7 (ekran druzyny gracza): n sztuk przedmiotu wychodzi ze zbrojowni AI (zaloga) do zbrojowni druzyny gracza, ktora trzyma stan
        /// w modyfikatorze sztuki. Ten sam udzial obitych co MoveWorn (proporcjonalnie, nie mniej niz trzeba, nie wiecej niz n) - zdjety z zapisu dawcy
        /// i zwrocony (stan, ile); reszta sztuk jest sprawna. Wolac PRZED zdjeciem sztuk ze slownika dawcy (countBefore = ile mial).
        /// </summary>
        internal static List<KeyValuePair<ItemModifier, int>> TakeWornShare(MobileParty from, ItemObject it, int n, int countBefore, bool sync = true)
        {
            var list = new List<KeyValuePair<ItemModifier, int>>();
            if (!On || from == null || it == null || n <= 0) return list;
            if (sync) Sync(from);
            if (!ArmouryBehavior.NoWear(it) && !MenPurse.HorseKind(it))
            {
                string pf = from.StringId, id = it.StringId;
                int w = WornOf(pf, id);
                if (w > 0)
                {
                    int c = Math.Max(1, countBefore);
                    int k = (int)Math.Round(w * (double)n / c);
                    k = Math.Max(k, w - Math.Max(0, countBefore - n));
                    k = Math.Min(k, Math.Min(w, n));
                    Dictionary<string, Dictionary<string, int>> byItem; Dictionary<string, int> byMod;
                    if (k > 0 && _worn.TryGetValue(pf, out byItem) && byItem.TryGetValue(id, out byMod))
                        foreach (var m in byMod.Keys.ToList())
                        {
                            if (k <= 0) break;
                            int t = Math.Min(k, byMod[m]);
                            if (t <= 0) continue;
                            AddWorn(pf, id, m, -t);
                            var mod = Mod(m);
                            if (mod != null) list.Add(new KeyValuePair<ItemModifier, int>(mod, t));   // nieznany stan - sztuka idzie jako sprawna
                            k -= t;
                        }
                }
            }
            Known(from, it, -n);
            return list;
        }

        /// <summary>AiGear dolozyl kupione sztuki - sprawne, spis od razu.</summary>
        internal static void NoteSound(MobileParty mp, ItemObject it, int n)
        {
            if (!On || mp == null || it == null) return;
            Dictionary<string, int> known;
            if (!_known.TryGetValue(mp.StringId, out known)) return;   // brak spisu - pierwszy Sync uzna calosc za sprawna
            int was; known.TryGetValue(it.StringId, out was); known[it.StringId] = was + n;
        }

        // ------------------------------------------------------------ bitwa
        internal static void OnMapEventEnded(MapEvent me)
        {
            try
            {
                if (!On || me == null) return;
                Day();
                var s = Settings.Current;
                float share = MBMath.ClampFloat(s.TroopWearPercent, 0f, 100f) / 100f;
                bool p2 = P2On;   // E8/P2 (244): pomiar "Amunicja AI: zeszloby" (tylko licznik)
                foreach (var side in new[] { me.AttackerSide, me.DefenderSide })
                {
                    if (side == null) continue;
                    foreach (var mep in side.Parties)
                    {
                        var mp = mep != null && mep.Party != null ? mep.Party.MobileParty : null;
                        // K1 (przeglad): takze zalogi - od K1-C dostaja w bitwie gracza lup DTE i walcza sprzetem ze zbrojowni (dotad lup zalogi
                        // wchodzil jako sprawny, a jej sprzet nie obijal sie w walce; taki "sprawny" lup szedl do kupca albo graczowi jako zwrot B6)
                        if (mp == null || mp.IsMainParty || !(mp.IsLordParty || mp.IsGarrison) || !mp.IsActive) continue;
                        _battleSince.Add(mp.StringId);   // przybytek do nastepnego spisu = lup (DTE doklada go w tym samym zdarzeniu)
                        if (p2) P2Ammo(me, side, mep, mp);   // E8/P2 (244, PLAN 8.4a): ile pociskow zeszloby tej partii - nic nie zdejmuje, bez losowania
                        if (share <= 0f) continue;
                        // zuzycie walki: czesc sztuk W UZYCIU (do liczby ludzi) - sprawne staja sie obite
                        var arm = Armory(mp); if (arm == null) continue;
                        var need = AiGear.NeedBuckets(mp);
                        // wpis 95 (audyt zuzycia pkt 4): potrzeba koszyka ROZDZIELANA miedzy przedmioty (dotad liczona od nowa dla
                        // kazdego przedmiotu w koszyku - 10 roznych zbroi t3 dawalo ~5x za duzo sztuk "w uzyciu")
                        foreach (var kv in arm.ToList())
                        {
                            var it = kv.Key;
                            if (it == null || kv.Value <= 0 || !SupplyDemand.Equipmentish(it) || ArmouryBehavior.NoWear(it) || MenPurse.HorseKind(it)) continue;
                            int bk = AiGear.Bucket(it);
                            int nd; need.TryGetValue(bk, out nd);
                            int inUse = Math.Min(nd, kv.Value);
                            need[bk] = Math.Max(0, nd - inUse);
                            int sound = kv.Value - WornOf(mp.StringId, it.StringId);
                            int hits = Math.Min(sound, (int)Math.Floor(inUse * share + MBRandom.RandomFloat));
                            for (int k = 0; k < hits; k++)
                            {
                                var m = ArmouryBehavior.MildWornModifier(it);   // wpis 95 (pkt 5): bitwa = jeden stopien, nie loteria lupu
                                if (m != null) { AddWorn(mp.StringId, it.StringId, m.StringId, 1); _dayWorn++; if (mp.IsGarrison) _p2WornG++; else _p2WornL++; }   // E8/P2: zalogi osobno (tylko licznik)
                            }
                        }
                    }
                }
            }
            catch (Exception e) { Log.Error("AiWear.OnMapEventEnded", e); }
        }

        /// <summary>
        /// E8/P2 (244, PLAN 8.4a; wzor dla E8/W "amunicja AI"): ile kolczanow, snopow beltow i wiazek broni miotanej zeszloby tej partii w tej bitwie, gdyby
        /// AI zuzywalo pociski. Sztuki pociskow W UZYCIU (koszyki wzorcow do liczby sztuk w zbrojowni - ta sama miara co zuzycie) x natezenie bitwy tej
        /// partii (R4, wzor gracza z autobitwy: (polegli + ranni) / zdrowi na starcie / TroopWearBaseCasualtyShare, 0.1-3) x udzial strony: pokonany 1.0,
        /// zwyciezca 1 - odzysk (AmmoRecoverPercent + AmmoRepairPercent - regula AmmoRecovery "zwyciezca zbiera pole", domyslnie 0.35); najwyzej tyle,
        /// ile w uzyciu. Ulamki sumowane - bez losowania (MBRandom nietkniety). Wlasny slownik potrzeb, zbrojownia tylko czytana - nic nie schodzi.
        /// </summary>
        private static void P2Ammo(MapEvent me, MapEventSide side, MapEventParty mep, MobileParty mp)
        {
            try
            {
                var arm = Armory(mp); if (arm == null) return;
                var s = Settings.Current;
                int lost = (mep.DiedInBattle != null ? mep.DiedInBattle.TotalManCount : 0) + (mep.WoundedInBattle != null ? mep.WoundedInBattle.TotalManCount : 0);
                float intensity = MBMath.ClampFloat((lost / (float)Math.Max(1, mep.HealthyManCountAtStart)) / Math.Max(0.01f, s.TroopWearBaseCasualtyShare), 0.1f, 3f);
                _p2IntSum += intensity; _p2IntN++;
                bool won = me.WinningSide != BattleSideEnum.None && side.MissionSide == me.WinningSide;
                float keep = MBMath.ClampFloat((s.AmmoRecoverPercent + s.AmmoRepairPercent) / 100f, 0f, 1f);
                float f = Math.Min(1f, intensity * (won ? 1f - keep : 1f));
                Dictionary<int, int> need = null;
                bool any = false;
                foreach (var kv in arm)
                {
                    var it = kv.Key;
                    if (it == null || kv.Value <= 0) continue;
                    int kind = it.ItemType == ItemObject.ItemTypeEnum.Arrows ? 0 : it.ItemType == ItemObject.ItemTypeEnum.Bolts ? 1 : it.ItemType == ItemObject.ItemTypeEnum.Thrown ? 2 : -1;
                    if (kind < 0) continue;
                    if (need == null) need = AiGear.NeedBuckets(mp);
                    int bk = AiGear.Bucket(it), nd;
                    need.TryGetValue(bk, out nd);
                    int inUse = Math.Min(nd, kv.Value);
                    if (inUse <= 0) continue;
                    need[bk] = nd - inUse;
                    float gone = inUse * f;
                    _p2AmmoUse[kind] += inUse; _p2AmmoGone[kind] += gone;
                    if (won) _p2AmmoWin += gone; else _p2AmmoLose += gone;
                    any = true;
                }
                if (any) _p2AmmoParties++;
            }
            catch (Exception e) { if (++_p2Stumbles <= 3) Log.Error("AiWear.P2Ammo (potkniecie " + _p2Stumbles + ")", e); }   // per partia, bez wylacznika (CLAUDE.md 7)
        }

        // ------------------------------------------------------------ stan przy sprzedazy nadwyzek
        /// <summary>Sztuka idzie do kupca: najgorsza obita, jesli jest; inaczej sprawna (null).</summary>
        internal static ItemModifier TakeCondition(MobileParty mp, ItemObject it) { return TakeCondition(mp, it, true); }

        /// <summary>171: sync = false - spis zrobiony juz w tej samej sprzedazy (seria sztuk z jednej zbrojowni; Known trzyma spis w zgodzie).</summary>
        internal static ItemModifier TakeCondition(MobileParty mp, ItemObject it, bool sync)
        {
            if (!On || mp == null || it == null) return ArmouryBehavior.PickWornModifier(it);
            if (sync) Sync(mp);
            Dictionary<string, Dictionary<string, int>> byItem; Dictionary<string, int> byMod;
            if (!_worn.TryGetValue(mp.StringId, out byItem) || !byItem.TryGetValue(it.StringId, out byMod) || byMod.Count == 0) { Known(mp, it, -1); return null; }
            string worst = null; float wm = float.MaxValue;
            foreach (var id in byMod.Keys) { var m = Mod(id); float pm = m != null ? m.PriceMultiplier : 1f; if (pm < wm) { wm = pm; worst = id; } }
            AddWorn(mp.StringId, it.StringId, worst, -1);
            Known(mp, it, -1);
            return Mod(worst);
        }

        /// <summary>K1c (przeglad K1b, "Hand kit to the garrison"): ile sztuk przedmiotu w zbrojowni partii jest obitych i w jakim stanie - bez
        /// zdejmowania (TakeCondition zdejmuje). Po spisie (Sync), jak TakeCondition. Wylaczone zuzycie - pusto.</summary>
        internal static List<KeyValuePair<ItemModifier, int>> WornSplit(MobileParty mp, ItemObject it)
        {
            var list = new List<KeyValuePair<ItemModifier, int>>();
            if (!On || mp == null || it == null) return list;
            try
            {
                Sync(mp);
                Dictionary<string, Dictionary<string, int>> byItem; Dictionary<string, int> byMod;
                if (!_worn.TryGetValue(mp.StringId, out byItem) || !byItem.TryGetValue(it.StringId, out byMod)) return list;
                foreach (var kv in byMod)
                {
                    if (kv.Value <= 0) continue;
                    var m = Mod(kv.Key);
                    if (m != null) list.Add(new KeyValuePair<ItemModifier, int>(m, kv.Value));
                }
            }
            catch { list.Clear(); }
            return list;
        }

        /// <summary>K1: sztuka wzieta przez TakeCondition nie poszla do kupca (kasa miasta pusta) - wraca do zbrojowni ze swoim stanem.</summary>
        internal static void PutBack(MobileParty mp, ItemObject it, ItemModifier mod)
        {
            if (!On || mp == null || it == null) return;
            if (mod != null && mod.PriceMultiplier < 1f && !MenPurse.HorseKind(it) && !ArmouryBehavior.NoWear(it)) AddWorn(mp.StringId, it.StringId, mod.StringId, 1);
            Known(mp, it, 1);
        }

        private static void Known(MobileParty mp, ItemObject it, int d)
        {
            Dictionary<string, int> known;
            if (!_known.TryGetValue(mp.StringId, out known)) return;
            int v; known.TryGetValue(it.StringId, out v); known[it.StringId] = Math.Max(0, v + d);
        }

        // ------------------------------------------------------------ naprawy w miescie
        private static int UnitCost(ItemObject it, ItemModifier m, Settlement st)
        {
            int old = Math.Max(1, (int)(it.Value * (1f - (m != null ? m.PriceMultiplier : 1f)) * 0.25f));
            // regula kowali miasta: ta sama robota co u gracza (MendMaterial.Labor); st null (zalegle naprawy w sakiewce) - zwykla dniowka
            return MendMaterial.RuleOn ? MendMaterial.Labor(it, MendMaterial.Share(new EquipmentElement(it, m)), st, old) : old;
        }
        private static bool Mendable(ItemModifier m) { return m != null && m.PriceMultiplier < 1f && !LootPrices.IsWreck(m); }   // wpis 97

        internal static int OutstandingCost(MobileParty mp) { return OutstandingCost(mp, null); }

        /// <summary>Rezerwa sakiewki na zalegle naprawy. Przy naprawach z materialem (MendMaterial.MenAndLordsOn) i podanym MIESCIE -
        /// robocizna + szacunek materialu z polki tego miasta (Bench.Estimate), sztuki bez receptury kowala pominiete; robocizna jak dotad.</summary>
        internal static int OutstandingCost(MobileParty mp, Settlement st)
        {
            if (!On || mp == null) return 0;
            int sum = 0;
            Dictionary<string, Dictionary<string, int>> byItem;
            if (!_worn.TryGetValue(mp.StringId, out byItem)) return 0;
            MendMaterial.Bench bench = MendMaterial.MenAndLordsOn && st != null && st.IsTown ? new MendMaterial.Bench(st) : null;
            if (bench != null && !bench.Ok) bench = null;
            // E6/N1 (217): rezerwa tylko na to, co kowale naprawia - przy AiMendsWornOnly obite sztuki, ktore nosza ludzie (dopasowanie z pamieci doby);
            // dotad rezerwa obejmowala tez lup ponad potrzebe, ktorego nikt nie naprawi (sakiewka trzymana na zawsze)
            var used = WornOnlyOn ? UsedByMen(mp, byItem, false) : null;
            var arm = used != null ? Armory(mp) : null;
            foreach (var it in byItem)
            {
                var item = MBObjectManager.Instance.GetObject<ItemObject>(it.Key); if (item == null) continue;
                if (used != null)
                {
                    int skipped = 0;
                    foreach (var lot in MenWornLots(it.Value, FreeOf(arm, item, used), ref skipped))
                        sum += LotCost(item, Mod(lot.Key), bench) * lot.Value;
                    continue;
                }
                foreach (var m in it.Value)
                    sum += LotCost(item, Mod(m.Key), bench) * m.Value;
            }
            return sum;
        }

        /// <summary>Rezerwa na jedna sztuke (wydzielone z OutstandingCost w E6/N1 - bez zmiany): robocizna + szacunek materialu z polki; 0 - nie do naprawy.</summary>
        private static int LotCost(ItemObject item, ItemModifier mod, MendMaterial.Bench bench)
        {
            if (!Mendable(mod)) return 0;
            int unit = UnitCost(item, mod, null);
            if (bench != null)
            {
                var need = MendMaterial.Needs(new EquipmentElement(item, mod));
                if (need == null) return 0;
                unit += MendMaterial.Gold(bench.Estimate(need));
            }
            return unit;
        }

        // ------------------------------------------------------------ E6/N1 (217, PLAN 6.17, R16): AI naprawia tylko to, co nosza ludzie
        // Zbrojownia DTE to "przedmiot: ile"; ksiega _worn mowi, ile z nich jest obitych. Ile sztuk nosza ludzie - to samo dopasowanie co nadwyzki
        // (MenPurse.AiWornByMen = AiFitted z AiSurplusPlan: ludzie nosza najlepsze, co udzwigna). Wolne sztuki przedmiotu (ile - noszone) to najgorsze
        // obite (ta sama kolejnosc co sprzedaz nadwyzki - TakeCondition bierze najgorsza); obite ponad wolne nosza ludzie - tylko te ida do kowala.
        // Obity lup ponad potrzebe zostaje obity: sprzedaz nadwyzek (MenPurse.SellArmorySurplus) oddaje go kupcowi w jego stanie (wrak - zlom, paczka 158).

        /// <summary>Ile sztuk kazdego przedmiotu nosza ludzie partii (id -> sztuk). fresh = false - wynik z tej samej doby, jesli jest. null - blad (jak dotad: wszystkie).
        /// Dopasowanie tylko dla typow, w ktorych sa obite sztuki do naprawy (koszt).</summary>
        private static Dictionary<string, int> UsedByMen(MobileParty mp, Dictionary<string, Dictionary<string, int>> byItem, bool fresh)
        {
            int today = (int)CampaignTime.Now.ToDays;
            KeyValuePair<int, Dictionary<string, int>> c;
            if (!fresh && _menUsed.TryGetValue(mp.StringId, out c) && c.Key == today) return c.Value;
            var res = new Dictionary<string, int>();
            try
            {
                var arm = Armory(mp);
                if (arm != null)
                {
                    var types = new HashSet<ItemObject.ItemTypeEnum>();
                    foreach (var it in byItem)
                    {
                        bool any = false;
                        foreach (var m in it.Value) if (m.Value > 0 && Mendable(Mod(m.Key))) { any = true; break; }
                        if (!any) continue;
                        var io = MBObjectManager.Instance.GetObject<ItemObject>(it.Key);
                        if (io != null) types.Add(io.ItemType);
                    }
                    if (types.Count > 0)
                        foreach (var kv in MenPurse.AiWornByMen(arm, GarrisonArmory.FitRoster(mp), types)) res[kv.Key.StringId] = kv.Value;
                }
            }
            catch (Exception e) { if (++_usedErr <= 3) Log.Error("AiWear.UsedByMen (potkniecie " + _usedErr + ")", e); return null; }   // per partia, bez wylacznika (CLAUDE.md 7)
            _menUsed[mp.StringId] = new KeyValuePair<int, Dictionary<string, int>>(today, res);
            return res;
        }

        /// <summary>Wolne sztuki przedmiotu w zbrojowni: ile jest minus ile nosza ludzie (nie mniej niz 0).</summary>
        private static int FreeOf(Dictionary<ItemObject, int> arm, ItemObject item, Dictionary<string, int> used)
        {
            int cnt = 0, u;
            if (arm != null && item != null) arm.TryGetValue(item, out cnt);
            used.TryGetValue(item.StringId, out u);
            return Math.Max(0, cnt - u);
        }

        /// <summary>Obite sztuki przedmiotu, ktore nosza ludzie (stan, ile): najgorsze najpierw wypelniaja wolne sztuki (free) i zostaja; skipped - ile
        /// sztuk do naprawy (nie wrakow) tak pominieto.</summary>
        private static List<KeyValuePair<string, int>> MenWornLots(Dictionary<string, int> byMod, int free, ref int skipped)
        {
            var list = new List<KeyValuePair<string, int>>();
            var mods = new List<KeyValuePair<string, float>>();
            foreach (var m in byMod) { if (m.Value <= 0) continue; var md = Mod(m.Key); mods.Add(new KeyValuePair<string, float>(m.Key, md != null ? md.PriceMultiplier : 1f)); }
            mods.Sort((a, b) => a.Value.CompareTo(b.Value));   // najgorszy stan najpierw - jak TakeCondition
            foreach (var m in mods)
            {
                int n = byMod[m.Key];
                int k = Math.Min(free, n);
                free -= k; n -= k;
                if (k > 0 && Mendable(Mod(m.Key))) skipped += k;
                if (n > 0) list.Add(new KeyValuePair<string, int>(m.Key, n));
            }
            return list;
        }

        /// <summary>Dzien postoju w miescie: kowale naprawiaja, ile zdaza i na ile starcza sakiewki ludzi.</summary>
        internal static void MendInTown(MobileParty mp, Settlement st)
        {
            try
            {
                if (!On || mp == null || st == null || !st.IsTown || st.Town == null || mp.IsMainParty) return;
                int today = (int)CampaignTime.Now.ToDays, last;
                if (_lastMend.TryGetValue(mp.StringId, out last) && last == today) return;   // dzien pracy kowali raz na dobe
                _lastMend[mp.StringId] = today;
                Day();
                Sync(mp);
                Dictionary<string, Dictionary<string, int>> byItem;
                if (!_worn.TryGetValue(mp.StringId, out byItem)) return;
                var s = Settings.Current;
                // wpis 91: z WSPOLNEJ puli kowali miasta - lordowie i gracz dziela te same rece
                float perPiece = Math.Max(0.05f, s.MendLootHoursPerPiece);
                int cap = (int)(SmithHours.Available(st.Town) / perPiece);
                var jobs = new List<KeyValuePair<string, string>>();   // przedmiot, modyfikator - najgorsze najpierw
                // E6/N1 (217, R16): przy AiMendsWornOnly tylko obite sztuki, ktore nosza ludzie (dopasowanie swieze - raz na dobe na partie, pamiec dla rezerwy)
                var used = WornOnlyOn ? UsedByMen(mp, byItem, true) : null;
                if (used != null)
                {
                    var arm = Armory(mp);
                    int skipped = 0;
                    foreach (var it in byItem)
                    {
                        var io = MBObjectManager.Instance.GetObject<ItemObject>(it.Key); if (io == null) continue;
                        foreach (var lot in MenWornLots(it.Value, FreeOf(arm, io, used), ref skipped))
                            if (Mendable(Mod(lot.Key))) for (int k = 0; k < lot.Value; k++) jobs.Add(new KeyValuePair<string, string>(it.Key, lot.Key));
                    }
                    _daySkipLoot += skipped;
                }
                else
                    foreach (var it in byItem) foreach (var m in it.Value) if (Mendable(Mod(m.Key))) for (int k = 0; k < m.Value; k++) jobs.Add(new KeyValuePair<string, string>(it.Key, m.Key));
                jobs.Sort((a, b) => { var ma = Mod(a.Value); var mb = Mod(b.Value); return (ma != null ? ma.PriceMultiplier : 1f).CompareTo(mb != null ? mb.PriceMultiplier : 1f); });
                if (MendMaterial.MenAndLordsOn) { MendWithMaterial(mp, st, jobs, cap, perPiece); return; }   // poprawka po audycie TOWARY 3: material jak na lawie
                int done = 0, paid = 0;
                foreach (var j in jobs)
                {
                    if (done >= cap) break;
                    var item = MBObjectManager.Instance.GetObject<ItemObject>(j.Key); if (item == null) continue;
                    int unit = UnitCost(item, Mod(j.Value), st);
                    if (MenPurse.Get(mp) < unit) break;
                    MenPurse.Take(mp, unit);
                    _dayFolk += MendMaterial.PaySmiths(st, unit, unit);   // E6/N1: sama robocizna - do kiesy ludu (B1), reszta kasa; linia kas 169 w PaySmiths
                    AddWorn(mp.StringId, j.Key, j.Value, -1);
                    done++; paid += unit;
                }
                SmithHours.Use(st.Town, done * perPiece);
                _dayMended += done; _dayPaid += paid;
                P2Mend(mp, jobs.Count, done, 0, 0, done < cap, done >= cap);   // E8/P2 (244): zalogi osobno i powod stania (tylko licznik)
            }
            catch (Exception e) { Log.Error("AiWear.MendInTown", e); }
        }

        /// <summary>
        /// E8/P2 (244, PLAN 8.7a, 8.8c): zlecenie napraw partii w tej dobie - naprawione sztuki osobno dla zalog i pozostalych partii oraz powod, dla ktorego
        /// reszta sztuk do naprawy (jobs - done - czekajace na material - nie robota kowala) stanela: sakiewka (petla konczy sie na pierwszej sztuce, na
        /// ktora nie stac - poor) albo godziny kowali miasta (capHit). Tylko liczniki linii "Zuzycie AI - pomiary (E8/P2)".
        /// </summary>
        private static void P2Mend(MobileParty mp, int jobs, int done, int wait, int noSmith, bool poor, bool capHit)
        {
            if (mp.IsGarrison) _p2MendG += done; else _p2MendL += done;
            if (jobs <= 0) return;
            _p2Visits++; _p2Jobs += jobs;
            if (wait > 0) _p2MatP++;
            int rest = jobs - done - wait - noSmith;
            if (rest <= 0) return;
            if (poor) { _p2PurseP++; _p2PurseN += rest; _p2PurseLeft += Math.Max(0, MenPurse.Get(mp)); }
            else if (capHit) { _p2HoursP++; _p2HoursN += rest; }
            else _p2OtherN += rest;
        }

        /// <summary>
        /// Poprawka po audycie TOWARY 3 (krok 139 planu K13): kowale miasta naprawiaja AI ta sama regula co lawe gracza - MendMaterial.Order:
        /// material wedle stanu z polki miasta albo zapasu kowali, po cenie targu; brak materialu - ta sztuka czeka (kolejne sztuki tego samego
        /// przedmiotu i stanu tez), inne ida dalej; sakiewka ludzi placi robote + material w calych pensach do kasy miasta. Najpierw plan na
        /// kopii (polka i zapas bez zmian), potem Commit, zaplata i zapis stanu - tylko dla zaplanowanych sztuk. Godziny kowali - za gotowe.
        /// </summary>
        private static void MendWithMaterial(MobileParty mp, Settlement st, List<KeyValuePair<string, string>> jobs, int cap, float perPiece)
        {
            var o = new MendMaterial.Order(st);
            int purse = MenPurse.Get(mp);
            // ta sama sztuka i ten sam stan - jeden stos (kolejnosc: najgorsze najpierw, jak lista jobs); brak materialu - caly stos czeka
            var order = new List<KeyValuePair<string, string>>();
            var count = new Dictionary<string, int>();
            foreach (var j in jobs) { string key = j.Key + "|" + j.Value; int c; if (!count.TryGetValue(key, out c)) order.Add(j); count[key] = c + 1; }
            foreach (var j in order)
            {
                if (o.Pieces >= cap) break;
                var item = MBObjectManager.Instance.GetObject<ItemObject>(j.Key); if (item == null) continue;
                var mod = Mod(j.Value);
                var ee = new EquipmentElement(item, mod);
                o.AddLot(ee, MendMaterial.Needs(ee), UnitCost(item, mod, st), count[j.Key + "|" + j.Value], purse, cap);
                if (o.Poor > 0) break;   // sakiewka nie starcza - jak dotad koniec na dzis
            }
            int done = 0, paid = 0;
            if (o.Pieces > 0)
            {
                o.Bench.Commit();
                foreach (var job in o.Jobs) { AddWorn(mp.StringId, job.El.Item.StringId, job.El.ItemModifier.StringId, -job.N); done += job.N; MetalTally(job); }
                paid = MenPurse.Take(mp, o.Total);
                _dayFolk += MendMaterial.PaySmiths(st, paid, o.Labor);   // E6/N1: robocizna do kiesy ludu (B1), material do kasy miasta; linia kas 169 w PaySmiths
                _dayMat += o.MatGold; _dayTowns++;
                for (int k = 0; k < MendMaterial.Kinds; k++) _dayKg[k] += o.Bench.UsedKg[k];
            }
            SmithHours.Use(st.Town, done * perPiece);
            _dayMended += done; _dayPaid += paid;
            _dayWait += o.Wait; _dayWaitMask |= o.WaitMask; _dayNoSmith += o.NoSmith;
            for (int k = 0; k < MendMaterial.Kinds; k++) _dayWaitBy[k] += o.WaitBy[k];
            P2Mend(mp, jobs.Count, done, o.Wait, o.NoSmith, o.Poor > 0, o.Pieces >= cap);   // E8/P2 (244): zalogi osobno i powod stania (tylko licznik)
        }

        private static void Day()
        {
            int d = (int)CampaignTime.Now.ToDays;
            if (_dayStamp == d) return;
            if (_dayStamp >= 0 && (_dayLoot + _dayWorn + _dayMended + _dayWait) > 0)   // _dayWait: tylko przy naprawach z materialem (dzien, w ktorym wszystko czekalo, tez ma linie)
            {
                int parties = _worn.Count, pieces = 0; foreach (var p in _worn.Values) foreach (var it in p.Values) foreach (var v in it.Values) pieces += v;
                Log.Info("Zuzycie AI: dzien " + _dayStamp + " - lup obity " + _dayLoot + " szt., zuzyte w bitwach " + _dayWorn + ", naprawione w miastach " + _dayMended
                         + " za " + _dayPaid + " z sakiewek ludzi; obitych razem " + pieces + " szt. w " + parties + " partiach"
                         + "; pominiete (lup nienoszony) " + (WornOnlyOn ? _daySkipLoot + " szt." : "- (AiMendsWornOnly WYL)")   // E6/N1 (217)
                         + ", robocizna do kiesy ludu " + _dayFolk + "."
                         + (MendMaterial.MenAndLordsOn ? MatDay() : ""));
            }
            if (_dayStamp >= 0 && P2On) P2Line();   // E8/P2 (244): przed zerowaniem licznikow doby - te same doby i ta sama etykieta co linia "Zuzycie AI"
            _dayLoot = _dayWorn = _dayMended = _dayPaid = 0; _dayStamp = d;
            _daySkipLoot = _dayFolk = 0;
            ClearMatDay();
            P2Clear();
        }

        /// <summary>
        /// E8/P2 (244): linia "Zuzycie AI - pomiary (E8/P2)" - przy kazdej zmianie doby ksiegi (takze w dobie bez linii "Zuzycie AI"). Zalogi i pozostale
        /// partie liczone osobno (suma = liczby linii "Zuzycie AI" - prog P2-2); powody stania napraw (PLAN 8.8c); "Amunicja AI: zeszloby" (PLAN 8.4a).
        /// </summary>
        private static void P2Line()
        {
            try
            {
                var ci = System.Globalization.CultureInfo.InvariantCulture;
                float gone = _p2AmmoGone[0] + _p2AmmoGone[1] + _p2AmmoGone[2];
                Log.Info("Zuzycie AI - pomiary (E8/P2): dzien " + _dayStamp
                         + " | w tym zalogi: lup obity " + _p2LootG + ", zuzyte " + _p2WornG + ", naprawione " + _p2MendG
                         + " | pozostale partie: lup obity " + _p2LootL + ", zuzyte " + _p2WornL + ", naprawione " + _p2MendL
                         + " | razem (linia Zuzycie AI): lup obity " + _dayLoot + ", zuzyte " + _dayWorn + ", naprawione " + _dayMended
                         + " | naprawy: zlecen " + _p2Visits + " (partia x doba z robota), sztuk do naprawy " + _p2Jobs + "; stoja - sakiewka " + _p2PurseP + " partii / " + _p2PurseN
                         + " szt. (reszta sakiewek tych partii " + _p2PurseLeft.ToString(ci) + " zl), godziny kowali " + _p2HoursP + " partii / " + _p2HoursN
                         + " szt., material " + _p2MatP + " partii / " + _dayWait + " szt., nie robota kowala " + _dayNoSmith + " szt., inne " + _p2OtherN + " szt."
                         + " | Amunicja AI: zeszloby " + gone.ToString("0.0", ci) + " szt. (strzaly " + _p2AmmoGone[0].ToString("0.0", ci) + ", belty " + _p2AmmoGone[1].ToString("0.0", ci)
                         + ", miotane " + _p2AmmoGone[2].ToString("0.0", ci) + "; zwyciezcy " + _p2AmmoWin.ToString("0.0", ci) + ", pokonani " + _p2AmmoLose.ToString("0.0", ci)
                         + ") z " + (_p2AmmoUse[0] + _p2AmmoUse[1] + _p2AmmoUse[2]) + " w uzyciu (strzaly " + _p2AmmoUse[0] + ", belty " + _p2AmmoUse[1] + ", miotane " + _p2AmmoUse[2]
                         + ") w " + _p2AmmoParties + " partiach; natezenie bitew srednio " + (_p2IntN > 0 ? (_p2IntSum / _p2IntN).ToString("0.00", ci) : "-") + " (" + _p2IntN
                         + " partii po bitwie) | potkniecia (sesja) " + _p2Stumbles + ".");
            }
            catch (Exception e) { if (++_p2Stumbles <= 3) Log.Error("AiWear.P2Line (potkniecie " + _p2Stumbles + ")", e); }
        }

        /// <summary>Dopisek linii dnia: material napraw AI (poprawka po audycie TOWARY 3).</summary>
        private static string MatDay()
        {
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            return " Material napraw z targu: " + _dayMat + " zl w " + _dayTowns + " zleceniach, zuzyto kg: metal " + _dayKg[MendMaterial.Metal].ToString("0.0", ci)
                   + ", drewno " + _dayKg[MendMaterial.Wood].ToString("0.0", ci) + ", skora " + _dayKg[MendMaterial.Leather].ToString("0.0", ci)
                   + ", plotno " + _dayKg[MendMaterial.Cloth].ToString("0.0", ci) + "; czeka na material " + _dayWait + " szt. (metal " + _dayWaitBy[MendMaterial.Metal]
                   + ", drewno " + _dayWaitBy[MendMaterial.Wood] + ", skora " + _dayWaitBy[MendMaterial.Leather] + ", plotno " + _dayWaitBy[MendMaterial.Cloth]
                   + "), nie robota kowala " + _dayNoSmith + "; " + MetalPerPiece(ci) + "; " + MendMaterial.Describe() + ".";
        }

        /// <summary>T3 (noc 08/09.10): metal (kg surowki wedlug receptury) na naprawiona sztuke - do porownania w autotescie (odniesienie 0.18 kg, doby 1-40).</summary>
        private static string MetalPerPiece(System.Globalization.CultureInfo ci)
        {
            var s = Settings.Current;
            string per = _dayMended > 0 ? (_dayKg[MendMaterial.Metal] / _dayMended).ToString("0.000", ci) : "-";
            return "metal na naprawiona sztuke " + per + " kg (T3 metal wedlug rodzaju " + (s != null && s.MendMetalByKind ? "WL" : "WYL")
                   + ": wedlug rodzaju " + _dayKgByKind.ToString("0.00", ci) + " kg / " + _dayByKind + " szt., stara regula " + _dayKgOldRule.ToString("0.00", ci)
                   + " kg / " + _dayOldRule + " szt., bez metalu w recepturze " + Math.Max(0, _dayMended - _dayByKind - _dayOldRule) + " szt.)";
        }

        /// <summary>T3 (poprawka po recenzji): kg metalu i sztuki osobno dla nowej i starej reguly; tylko sztuki z metalem w recepturze. O(1) na stos.</summary>
        private static void MetalTally(MendMaterial.Job job)
        {
            try
            {
                var c = ArmsPricing.CostOf(job.El.Item);
                if (c == null || c.MetalKg <= 0.001f) return;
                if (MendMaterial.MetalByKind(job.El.Item)) { _dayByKind += job.N; _dayKgByKind += job.MetalKg; }
                else { _dayOldRule += job.N; _dayKgOldRule += job.MetalKg; }
            }
            catch (Exception e) { Log.Error("AiWear.MetalTally", e); }
        }
    }
}
