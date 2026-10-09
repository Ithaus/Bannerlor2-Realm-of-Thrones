using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// KOMPLET REKRUTA (wpis 92; Jeff 05.10: "tak" - przy werbunku AI zbrojownia dostaje tylko to, co notabl naprawde kupil,
    /// plus wlasny dobytek rekruta tieru 1). Dotad DTE (OnTroopRecruited) wkladal do zbrojowni CALY komplet wzorca z niczego,
    /// a to, co notabl kupil na targu przy awansie ochotnika (VolunteerKit), przepadalo.
    ///  - Awans X -> Y w puli notabla: komplet Y = komplet X (albo dobytek X, ktory Y tez nosi) + kupione na targu.
    ///  - Ochotnik znika z puli bez werbunku: jego kupione rzeczy notabl sprzedaje na targu (cena skupu, nie wiecej niz kasa).
    ///  - Werbunek AI tieru 2+: do zbrojowni ida zapisane komplety; tier 1 - wlasny dobytek (komplet DTE jak dotad).
    ///  - Ochotnicy z poczatku kampanii (dorobek stuleci, ColdStart) maja komplet wzorca.
    /// 171 (docs/paczki/171-zbrojenie-zalog.md; Jeff 08.10 "tak potwierdzam" (2): rekrut bez kompletu przychodzi z tym, co ma):
    ///  - Kit.Own = dobytek czlowieka (z domu) osobno od Items (kupione przez notabla): dobytek idzie z nim, notabl sprzedaje tylko kupione (Z5).
    ///  - Bez zapisu: wolny czlowiek przynosi dobytek - rzeczy przodka tieru 1 jego linii, ktore jego oddzial tez nosi (regula wpisu 92, Z3);
    ///    najemnik z karczmy dodatkowo konia i rzad (160, do 167); jeniec - nic (Z4); echo ROT - nic (ten sam czlowiek, Z1).
    ///  - HouseLevies (OnSwap): komplet X przechodzi na Y; swiezy ochotnik tieru 2+ (OnFresh): zapis z kupionymi czesciami kluczowymi;
    ///    autowerbunek zalogi (OnGarrisonTook): komplet do zbrojowni zalogi zamiast sieroty.
    /// Gracz: bez zmian (DTE), zdejmujemy tylko zapisy.
    /// </summary>
    internal static class RecruitKit
    {
        internal static bool On { get { var s = Settings.Current; return s != null && s.KitFromNotable && s.VolunteerKitEnabled; } }
        internal static bool FreshOn { get { return On && Settings.Current.FreshVolunteerKit; } }

        private class Kit
        {
            internal CharacterObject Troop; internal bool Template;
            internal List<EquipmentElement> Items = new List<EquipmentElement>();   // kupione (notabl) - na targ przy odejsciu
            internal List<EquipmentElement> Own = new List<EquipmentElement>();     // 171: dobytek - idzie z czlowiekiem
        }
        private static readonly Dictionary<Hero, List<Kit>> _kits = new Dictionary<Hero, List<Kit>>();
        private static int _dayKits, _dayLegacy, _dayTier1, _daySold, _dayStamp = -1;
        // 171: liczniki doby (linie 1-3)
        private static int _dOwnNotable, _dOwnMerc, _dOwnNoSource, _dPrisoner, _dEcho, _dRecPcs, _dOwnPcs, _dMercHorsePcs, _dMountedNoHorse;
        private static int _dFreshBought, _dFreshOwn, _dFreshPcs, _dSwap, _dSwapNoKit;
        private static int _dGarMen, _dGarRec, _dGarOwn, _dGarT1, _dGarPcs, _dGarT1Pcs, _dGarPlayer, _stumbles, _errDay = -1;
        private static readonly HashSet<Settlement> _dGarTowns = new HashSet<Settlement>();
        private static readonly HashSet<string> _errWhere = new HashSet<string>();
        private static readonly int[] _tRec = new int[7], _tOwn = new int[7], _tPrisoner = new int[7], _tEcho = new int[7];
        // 171: mapa rodzicow (cel awansu -> oddzialy, z ktorych sie awansuje) i przodek tieru 1 - raz na sesje
        private static Dictionary<CharacterObject, List<CharacterObject>> _parents;
        private static readonly Dictionary<CharacterObject, CharacterObject> _root = new Dictionary<CharacterObject, CharacterObject>();
        internal static int SeedConverted;   // B5: zapisy sprzed startu zamienione na dorobek (linia ColdStart)
        // 161: zapis z SyncData czeka na rozwiazanie do startu sesji - w SyncData bohaterowie gry nie sa jeszcze
        // do znalezienia (MBObjectManager.GetObject<Hero> = null), wiec dotad KAZDE wczytanie gubilo wszystkie komplety
        private static string _pending;

        internal static void Reset()
        {
            _kits.Clear(); _pending = null; _dayKits = _dayLegacy = _dayTier1 = _daySold = 0; _dayStamp = -1;
            ClearDay171(); _stumbles = 0; _errDay = -1; _errWhere.Clear();
            _parents = null; _root.Clear(); SeedConverted = 0;
        }

        private static void ClearDay171()
        {
            _dOwnNotable = _dOwnMerc = _dOwnNoSource = _dPrisoner = _dEcho = _dRecPcs = _dOwnPcs = _dMercHorsePcs = _dMountedNoHorse = 0;
            _dFreshBought = _dFreshOwn = _dFreshPcs = _dSwap = _dSwapNoKit = 0;
            _dGarMen = _dGarRec = _dGarOwn = _dGarT1 = _dGarPcs = _dGarT1Pcs = _dGarPlayer = 0;
            _dGarTowns.Clear();
            Array.Clear(_tRec, 0, 7); Array.Clear(_tOwn, 0, 7); Array.Clear(_tPrisoner, 0, 7); Array.Clear(_tEcho, 0, 7);
        }

        /// <summary>Wyjatek przy jednym czlowieku/notablu: liczony w linii "Pule ochotnikow (171)", Log.Error najwyzej raz na dobe z miejsca.</summary>
        internal static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try
            {
                int d = (int)CampaignTime.Now.ToDays;
                if (d != _errDay) { _errDay = d; _errWhere.Clear(); }
                if (_errWhere.Add(where)) Log.Error("RecruitKit." + where, e);
            }
            catch { }
        }

        private static int TierIdx(CharacterObject c) { int t = c != null ? c.Tier : 0; return Math.Max(0, Math.Min(6, t)); }

        private static List<EquipmentElement> TemplateItems(CharacterObject c)
        {
            var l = new List<EquipmentElement>();
            Equipment eq = null; try { eq = c != null ? c.FirstBattleEquipment : null; } catch { }
            if (eq == null) return l;
            for (int i = 0; i < (int)EquipmentIndex.NumEquipmentSetSlots; i++) { var it = eq[i].Item; if (it != null && SupplyDemand.Equipmentish(it)) l.Add(new EquipmentElement(it)); }
            return l;
        }

        private static Kit Pop(Hero n, CharacterObject c)
        {
            List<Kit> l; if (n == null || c == null || !_kits.TryGetValue(n, out l)) return null;
            for (int i = 0; i < l.Count; i++) if (l[i].Troop == c) { var k = l[i]; l.RemoveAt(i); if (l.Count == 0) _kits.Remove(n); return k; }
            return null;
        }

        private static void Push(Hero n, Kit k) { List<Kit> l; if (!_kits.TryGetValue(n, out l)) _kits[n] = l = new List<Kit>(); l.Add(k); }

        /// <summary>Rzeczy czlowieka: wzorzec (dorobek stuleci) + dobytek + kupione.</summary>
        private static List<EquipmentElement> Materialize(Kit k)
        {
            var l = new List<EquipmentElement>(); if (k == null) return l;
            if (k.Template) l.AddRange(TemplateItems(k.Troop));
            l.AddRange(k.Own);
            l.AddRange(k.Items);
            return l;
        }

        // ------------------------------------------------------------ 171 B1: dobytek wolnego czlowieka
        private static void BuildParents()
        {
            if (_parents != null) return;
            _parents = new Dictionary<CharacterObject, List<CharacterObject>>();
            try
            {
                foreach (var c in MBObjectManager.Instance.GetObjectTypeList<CharacterObject>())
                {
                    if (c == null || c.IsHero || c.UpgradeTargets == null) continue;
                    foreach (var t in c.UpgradeTargets)
                    {
                        if (t == null) continue;
                        List<CharacterObject> l;
                        if (!_parents.TryGetValue(t, out l)) _parents[t] = l = new List<CharacterObject>();
                        if (!l.Contains(c)) l.Add(c);
                    }
                }
            }
            catch (Exception e) { Stumble("BuildParents", e); }
        }

        /// <summary>Przodek tieru 1 linii awansu (najpierw tej samej kultury), najwyzej 6 krokow w gore; null - brak.</summary>
        private static CharacterObject Tier1Root(CharacterObject y)
        {
            if (y == null) return null;
            CharacterObject r;
            if (_root.TryGetValue(y, out r)) return r;
            BuildParents();
            r = null;
            if (y.Tier <= 1) r = y;
            else
            {
                var frontier = new List<CharacterObject> { y };
                var seen = new HashSet<CharacterObject> { y };
                for (int step = 0; step < 6 && r == null && frontier.Count > 0; step++)
                {
                    var same = new List<CharacterObject>(); var other = new List<CharacterObject>();
                    foreach (var c in frontier)
                    {
                        List<CharacterObject> ps;
                        if (!_parents.TryGetValue(c, out ps)) continue;
                        foreach (var p in ps) if (p != null && seen.Add(p)) { if (p.Culture == y.Culture) same.Add(p); else other.Add(p); }
                    }
                    foreach (var p in same) if (p.Tier <= 1) { r = p; break; }
                    if (r == null) foreach (var p in other) if (p.Tier <= 1) { r = p; break; }
                    frontier = same; frontier.AddRange(other);
                }
            }
            _root[y] = r;
            return r;
        }

        /// <summary>Rzeczy oddzialu root, ktore oddzial y tez nosi (bez koni i rzedow).</summary>
        private static List<EquipmentElement> Shared(CharacterObject root, CharacterObject y)
        {
            var l = new List<EquipmentElement>();
            if (root == null || y == null || root == y) return l;
            var have = new HashSet<ItemObject>(); foreach (var e in TemplateItems(root)) have.Add(e.Item);
            foreach (var e in TemplateItems(y)) if (have.Contains(e.Item) && !MenPurse.HorseKind(e.Item)) l.Add(e);
            return l;
        }

        /// <summary>Dobytek wolnego czlowieka (Z3, regula wpisu 92): rzeczy przodka tieru 1 jego linii, ktore oddzial y tez nosi.</summary>
        private static List<EquipmentElement> OwnOf(CharacterObject y)
        {
            if (y == null) return new List<EquipmentElement>();
            var root = Tier1Root(y) ?? (y.Culture != null ? y.Culture.BasicTroop : null);
            return Shared(root, y);
        }

        // ------------------------------------------------------------ pule ochotnikow
        /// <summary>Awans w puli: komplet przechodzi na nowy szczebel i rosnie o kupione sztuki.</summary>
        internal static void OnUpgrade(Hero n, CharacterObject x, CharacterObject y, List<EquipmentElement> bought)
        {
            if (!On || n == null || x == null || y == null) return;
            var old = Pop(n, x);
            var k = new Kit { Troop = y };
            if (old != null)
            {
                // wzorzec ColdStart X staje sie dobytkiem: to rzeczy tego czlowieka, nie wzorzec Y
                k.Own.AddRange(old.Own);
                if (old.Template) k.Own.AddRange(TemplateItems(old.Troop));
                k.Items.AddRange(old.Items);
            }
            else
            {
                // 171 B4: dobytek tieru 1 linii X, ktory Y tez nosi (dla X tieru 1 - jak dotad, tylko jako dobytek)
                var root = x.Tier <= 1 ? x : (Tier1Root(x) ?? (x.Culture != null ? x.Culture.BasicTroop : null));
                k.Own.AddRange(Shared(root, y));
            }
            if (bought != null) k.Items.AddRange(bought);
            Push(n, k);
        }

        /// <summary>Ochotnik zniknal z puli bez werbunku: notabl sprzedaje jego kupione rzeczy na targu (dobytek odchodzi z nim).</summary>
        internal static void OnVanished(Hero n, CharacterObject x, Settlement market)
        {
            if (!On) return;
            SellOff(n, Pop(n, x), market);
        }

        /// <summary>171 A2: HouseLevies zamienia szlacheckiego X na czlowieka rodu Y - ten sam czlowiek, te same rzeczy.</summary>
        internal static void OnSwap(Hero n, CharacterObject x, CharacterObject y)
        {
            var s = Settings.Current;
            if (!On || s == null || !s.HouseLeviesKeepKit || n == null || x == null || y == null) return;
            Day();
            var k = Pop(n, x);
            if (k == null) { _dSwapNoKit++; return; }
            var nk = new Kit { Troop = y, Template = false };
            nk.Own.AddRange(k.Own);
            if (k.Template) nk.Own.AddRange(TemplateItems(k.Troop));   // wzorzec ColdStart X staje sie dobytkiem
            nk.Items = k.Items;
            Push(n, nk);
            _dSwap++;
        }

        /// <summary>171 A3: swiezy ochotnik tieru 2+ - zapis kupionych czesci kluczowych (bought) albo sam dobytek (null - notabl nie kupil).</summary>
        internal static void OnFresh(Hero n, CharacterObject y, List<EquipmentElement> bought)
        {
            if (!FreshOn || n == null || y == null) return;
            Day();
            var k = new Kit { Troop = y };
            k.Own.AddRange(OwnOf(y));
            if (bought != null) k.Items.AddRange(bought);
            Push(n, k);
            if (bought != null) { _dFreshBought++; _dFreshPcs += bought.Count; } else _dFreshOwn++;
        }

        /// <summary>171 A5: autowerbunek zalogi wzial X z puli notabla - jego rzeczy do zbrojowni zalogi (zalogi gracza - poza systemem jak dotad).</summary>
        internal static void OnGarrisonTook(Hero n, CharacterObject x, MobileParty garrison, Settlement place)
        {
            if (!On || n == null || x == null) return;
            Day();
            var s = Settings.Current;
            var owner = place != null ? place.OwnerClan : null;
            if (garrison == null || (owner == Clan.PlayerClan && !s.GarrisonBuysGearPlayer))
            {
                OnVanished(n, x, MarketOfNotable(n));   // jak dotad, tylko od razu (bez czekania na Reconcile)
                if (garrison != null) _dGarPlayer++;
                return;
            }
            List<EquipmentElement> items;
            if (x.Tier <= 1) { items = TemplateItems(x); _dGarT1++; _dGarT1Pcs += items.Count; }   // dobytek tieru 1 (regula wpisu 92, jak przy werbunku do partii)
            else
            {
                var k = Pop(n, x);
                if (k != null) { items = Materialize(k); _dGarRec++; }
                else { items = OwnOf(x); _dGarOwn++; }
            }
            foreach (var e in items) if (e.Item != null && AiGear.AddToArmory(garrison, e.Item, 1)) _dGarPcs++;
            _dGarMen++;
            if (place != null) _dGarTowns.Add(place);
        }

        /// <summary>171 A1: echo werbunku ROT pominiete (ten sam czlowiek, bez drugiego kompletu) - tylko licznik.</summary>
        internal static void NoteEcho(CharacterObject troop, int amount)
        {
            if (amount <= 0) return;
            try { Day(); } catch { }
            _dEcho += amount; _tEcho[TierIdx(troop)] += amount;
        }

        /// <summary>Kupione rzeczy kompletu na targ: notabl dostaje cene skupu, nie wiecej niz kasa miasta (dobytek odchodzi z czlowiekiem).</summary>
        private static int SellOff(Hero n, Kit k, Settlement market)
        {
            if (k == null || k.Items.Count == 0 || market == null || market.Town == null) return 0;
            int sold = 0;
            foreach (var e in k.Items)
            {
                if (e.Item == null) continue;
                int price = MenPurse.SellPrice(e, market, null);
                if (market.Town.Gold < price) break;
                market.ItemRoster.AddToCounts(e, 1);
                market.Town.ChangeGold(-price);
                n.ChangeHeroGold(price);
                _daySold++;
                sold++;
                SellByCondition.NoteSale(SellByCondition.Notable, e, 1, price);   // ksiega skupu sprzetu (tylko log)
            }
            return sold;
        }

        private static Settlement MarketOfNotable(Hero n)
        {
            Settlement st = null;
            try { st = n.HomeSettlement ?? n.CurrentSettlement; } catch { }
            return st != null ? VolunteerKit.MarketOf(st) : null;
        }

        /// <summary>
        /// 161: uzgodnienie kompletow z pulami ochotnikow (raz na dobe i po wczytaniu). Komplet bez ochotnika w puli
        /// to sierota (ochotnik odszedl poza zdarzeniami, ktore widzi VolunteerKit) - w tescie rocznym 12 927 kompletow
        /// u 2760 notabli. Nadmiar wobec puli: kupione rzeczy notabl sprzedaje na targu (jak przy odejsciu ochotnika);
        /// notabl nie zyje albo nie jest juz notablem: kupione rzeczy wracaja na targ jego osady bez zaplaty (zloto zmarlego
        /// nikomu by nie przypadlo). Zostaja najnowsze komplety danego oddzialu. Dobytek (171) odchodzi z ludzmi.
        /// </summary>
        internal static void Reconcile(string why)
        {
            if (!On || _kits.Count == 0) return;
            int before = 0, extra = 0, dead = 0, deadNotables = 0, sold = 0, returned = 0;
            foreach (var n in new List<Hero>(_kits.Keys))
            {
                var l = _kits[n];
                before += l.Count;
                bool gone = n == null || !n.IsAlive || !n.IsNotable || n.VolunteerTypes == null;
                if (gone)
                {
                    var market = n != null ? MarketOfNotable(n) : null;
                    foreach (var k in l)
                    {
                        if (market == null || market.ItemRoster == null) continue;
                        foreach (var e in k.Items) if (e.Item != null) { market.ItemRoster.AddToCounts(e, 1); returned++; }
                    }
                    dead += l.Count; deadNotables++;
                    _kits.Remove(n);
                    continue;
                }
                var pool = new Dictionary<CharacterObject, int>();
                foreach (var c in n.VolunteerTypes) if (c != null) { int v; pool.TryGetValue(c, out v); pool[c] = v + 1; }
                var kept = new Dictionary<CharacterObject, int>();
                var drop = new List<Kit>();
                for (int i = l.Count - 1; i >= 0; i--)
                {
                    var k = l[i];
                    int have = 0, room = 0;
                    if (k.Troop != null) { kept.TryGetValue(k.Troop, out have); pool.TryGetValue(k.Troop, out room); }
                    if (k.Troop != null && have < room) { kept[k.Troop] = have + 1; continue; }
                    drop.Add(k);
                    l.RemoveAt(i);
                }
                if (drop.Count > 0)
                {
                    var market = MarketOfNotable(n);
                    foreach (var k in drop) sold += SellOff(n, k, market);
                    extra += drop.Count;
                }
                if (l.Count == 0) _kits.Remove(n);
            }
            if (extra + dead > 0 || why != "doba")
            {
                int after = 0; foreach (var l in _kits.Values) after += l.Count;
                Log.Info("Komplet rekruta: uzgodnienie z pulami (" + why + ") - kompletow " + before + " -> " + after + " u " + _kits.Count + " notabli; nadmiar wobec puli "
                         + extra + " (kupione rzeczy sprzedane " + sold + " szt.), notable zmarli/bez puli " + deadNotables + " (" + dead + " kompletow, rzeczy na targ " + returned + " szt.).");
            }
        }

        /// <summary>
        /// Prefiks DTE OnTroopRecruited (AI): zamiast kompletu z niczego - zapisane komplety ochotnikow; 171 B2 - kazdy przypadek bez zapisu
        /// "z tym, co ma" (jeniec nic, najemnik dobytek + kon i rzad, ochotnik bez zapisu i czlowiek z drogi - dobytek). Echo ROT odcina AiGear.
        /// </summary>
        internal static bool OnRecruited(Hero recruiterHero, Settlement settlement, Hero recruitmentSource, CharacterObject troop, int amount)
        {
            if (!On || recruiterHero == null || troop == null || amount <= 0) return true;
            Day();
            // gracz: komplet DTE bez zmian, ale zapis kompletow tych ochotnikow zdejmujemy (inaczej zostalby sierota w puli)
            if (recruiterHero == Hero.MainHero) { for (int i = 0; i < amount; i++) Pop(recruitmentSource, troop); return true; }
            var s = Settings.Current;
            bool what = s.RecruitBringsWhatHeHas;
            // 2: jeniec - obszukany, jego sprzet wzial zwyciezca w lupie (Z4)
            if (what && RecruitSources.InPrisonerRecruit)
            {
                _dPrisoner += amount; _tPrisoner[TierIdx(troop)] += amount;
                if (troop.IsMounted) _dMountedNoHorse += amount;
                return false;
            }
            if (troop.Tier <= 1) { _dayTier1 += amount; return true; }   // 3: wlasny dobytek rekruta - komplet DTE jak dotad
            var mp = recruiterHero.PartyBelongedTo;
            if (mp == null) return true;                                 // 4: DTE i tak wychodzi
            for (int i = 0; i < amount; i++)
            {
                var k = Pop(recruitmentSource, troop);
                List<EquipmentElement> items;
                if (k != null) { items = Materialize(k); _dayKits++; _dRecPcs += items.Count; _tRec[TierIdx(troop)]++; }   // 5: z zapisu notabla
                else if (!what) { items = TemplateItems(troop); _dayLegacy++; }                                               // wylacznik: wzorzec jak dotad
                else
                {
                    items = OwnOf(troop);
                    if (recruitmentSource != null) { _dOwnNotable++; if (troop.IsMounted) _dMountedNoHorse++; }                // 6: ochotnik bez zapisu
                    else if (settlement != null)                                                                              // 7: najemnik z karczmy
                    {
                        _dOwnMerc++;
                        foreach (var e in TemplateItems(troop)) if (MenPurse.HorseKind(e.Item)) { items.Add(e); _dMercHorsePcs++; }   // 160: kon najemnika z wzorca (do 167)
                    }
                    else { _dOwnNoSource++; if (troop.IsMounted) _dMountedNoHorse++; }                                        // 8: bez zrodla (ochotnik z mapy, inne mody)
                    _dOwnPcs += items.Count; _tOwn[TierIdx(troop)]++;
                }
                foreach (var e in items)
                {
                    if (e.Item == null) continue;
                    if (AiGear.AddToArmory(mp, e.Item, 1)) AiWear.NoteBought(mp, e, 1);
                }
            }
            return false;
        }

        /// <summary>Dorobek stuleci: ochotnicy tieru 2+ w pulach na starcie kampanii maja komplet wzorca (171 B5: tyle zapisow, ilu ochotnikow).</summary>
        internal static int SeedCampaignStart()
        {
            int n = 0;
            SeedConverted = 0;
            foreach (var h in Hero.AllAliveHeroes)
            {
                if (h == null || !h.IsNotable || h.VolunteerTypes == null) continue;
                var pool = new Dictionary<CharacterObject, int>();
                foreach (var c in h.VolunteerTypes) if (c != null && c.Tier >= 2) { int v; pool.TryGetValue(c, out v); pool[c] = v + 1; }
                if (pool.Count == 0) continue;
                List<Kit> l; _kits.TryGetValue(h, out l);
                var have = new Dictionary<CharacterObject, int>();
                if (l != null)
                    foreach (var k in l)
                    {
                        if (k.Troop == null) continue;
                        // warunek bezpieczenstwa (krytyka 5): zapis z kupionymi rzeczami sprzed startu - kupione wracaja na targ, potem dorobek
                        if (k.Items.Count > 0) SellOff(h, k, MarketOfNotable(h));
                        if (k.Items.Count > 0 || k.Own.Count > 0 || !k.Template) SeedConverted++;
                        k.Template = true; k.Items.Clear(); k.Own.Clear();
                        int v; have.TryGetValue(k.Troop, out v); have[k.Troop] = v + 1;
                        n++;
                    }
                foreach (var kv in pool)
                {
                    int e; have.TryGetValue(kv.Key, out e);
                    for (int i = e; i < kv.Value; i++) { Push(h, new Kit { Troop = kv.Key, Template = true }); n++; }
                }
            }
            return n;
        }

        private static void Day()
        {
            int d = (int)CampaignTime.Now.ToDays;
            if (d == _dayStamp) return;
            if (_dayStamp >= 0) Flush();
            _dayKits = _dayLegacy = _dayTier1 = _daySold = 0; ClearDay171(); _dayStamp = d;
        }

        private static string Tiers(int[] a, int from)
        {
            var sb = new StringBuilder();
            for (int t = from; t <= 6; t++) { if (t > from) sb.Append(", "); sb.Append('t').Append(t).Append(' ').Append(a[t]); }
            return sb.ToString();
        }

        private static void Flush()
        {
            var s = Settings.Current;
            bool what = s == null || s.RecruitBringsWhatHeHas;
            int own = _dOwnNotable + _dOwnMerc + _dOwnNoSource;
            int st = _stumbles + RecruitSources.TakeStumbles(); _stumbles = 0;
            if ((_dayKits + _dayLegacy + _dayTier1 + _daySold + own + _dPrisoner + _dEcho) > 0)
            {
                // 1. linia dnia (zastepuje "Komplet rekruta: dzien ..." sprzed 171)
                var sb = new StringBuilder();
                sb.Append("Komplet rekruta: dzien ").Append(_dayStamp).Append(" - werbunek AI: z zapisu notabla ").Append(_dayKits);
                if (what)
                    sb.Append(", z tym co ma ").Append(own).Append(" (notabl bez zapisu ").Append(_dOwnNotable).Append(", najemnik z karczmy ").Append(_dOwnMerc)
                      .Append(", bez zrodla ").Append(_dOwnNoSource).Append("), jeniec bez niczego ").Append(_dPrisoner);
                if (!what || _dayLegacy > 0) sb.Append(", bez zapisu (wzorzec) ").Append(_dayLegacy);
                sb.Append(", tier 1 z wlasnym dobytkiem ").Append(_dayTier1)
                  .Append("; duplikat ROT pominiety ").Append(_dEcho).Append(" (ten sam czlowiek, bez drugiego kompletu)")
                  .Append("; do zbrojowni szt.: z zapisu ").Append(_dRecPcs).Append(", z tym co ma ").Append(_dOwnPcs)
                  .Append(", konie i rzedy najemnikow z wzorca (160) ").Append(_dMercHorsePcs)
                  .Append("; konni bez konia (do 167) ").Append(_dMountedNoHorse)
                  .Append("; rzeczy ochotnikow, ktorzy odeszli, sprzedane ").Append(_daySold).Append(" szt.");
                Log.Info(sb.ToString());
                // 2. tiery
                Log.Info("Komplet rekruta (tiery): dzien " + _dayStamp + " - z zapisu " + Tiers(_tRec, 2) + " | z tym co ma " + Tiers(_tOwn, 2)
                         + " | jeniec " + Tiers(_tPrisoner, 1) + " | duplikat ROT " + Tiers(_tEcho, 1) + ".");
            }
            if ((_dFreshBought + _dFreshOwn + _dSwap + _dSwapNoKit + _dGarMen + _dGarPlayer + st) > 0)
            {
                // 3. pule ochotnikow (171)
                Log.Info("Pule ochotnikow (171): dzien " + _dayStamp + " - swiezi ochotnicy t2+ " + (_dFreshBought + _dFreshOwn) + " (notabl kupil " + _dFreshBought
                         + ", z tym co ma " + _dFreshOwn + "; szt. " + _dFreshPcs + "); HouseLevies: komplet przeniesiony " + _dSwap + ", bez zapisu " + _dSwapNoKit
                         + "; autowerbunek zalog: ludzi " + _dGarMen + " w " + _dGarTowns.Count + " twierdzach (z zapisu " + _dGarRec + ", z tym co ma " + _dGarOwn
                         + ", tier 1 " + _dGarT1 + "), do zbrojowni zalog " + _dGarPcs + " szt. (w tym dobytek tieru 1 " + _dGarT1Pcs + "); zalogi gracza poza systemem "
                         + _dGarPlayer + "; potkniecia " + st + ".");
            }
        }

        // ------------------------------------------------------------ zapis: notabl>oddzial>T|przedmiot:stan,+dobytek:stan,...~
        internal static string Export()
        {
            if (_pending != null) ResolvePending("zapis przed startem sesji");
            var sb = new StringBuilder();
            foreach (var kv in _kits)
                foreach (var k in kv.Value)
                {
                    if (kv.Key == null || k.Troop == null) continue;
                    sb.Append(kv.Key.StringId).Append('>').Append(k.Troop.StringId).Append('>').Append(k.Template ? "T" : "-").Append('>');
                    bool first = true;
                    for (int i = 0; i < k.Items.Count; i++) AppendItem(sb, k.Items[i], false, ref first);
                    for (int i = 0; i < k.Own.Count; i++) AppendItem(sb, k.Own[i], true, ref first);   // 171: dobytek z przedrostkiem '+'
                    sb.Append('~');
                }
            return sb.ToString();
        }

        private static void AppendItem(StringBuilder sb, EquipmentElement e, bool own, ref bool first)
        {
            if (e.Item == null) return;
            if (!first) sb.Append(',');
            first = false;
            if (own) sb.Append('+');
            sb.Append(e.Item.StringId); if (e.ItemModifier != null) sb.Append(':').Append(e.ItemModifier.StringId);
        }

        /// <summary>Z SyncData: tylko zapamietanie - rozwiazanie w ResolvePending (start sesji).</summary>
        internal static void Import(string s)
        {
            _kits.Clear();
            _pending = string.IsNullOrEmpty(s) ? null : s;
        }

        /// <summary>Z OnSessionLaunched (i awaryjnie z Export): komplety z zapisu na zywych bohaterow gry.</summary>
        internal static void ResolvePending(string why)
        {
            var s = _pending;
            _pending = null;
            if (string.IsNullOrEmpty(s)) return;
            var om = MBObjectManager.Instance;
            var heroes = new Dictionary<string, Hero>();
            try { foreach (var hh in Hero.AllAliveHeroes) if (hh != null && hh.StringId != null) heroes[hh.StringId] = hh; } catch { }
            try { foreach (var hh in Hero.DeadOrDisabledHeroes) if (hh != null && hh.StringId != null && !heroes.ContainsKey(hh.StringId)) heroes[hh.StringId] = hh; } catch { }
            int recs = 0, noHero = 0, noTroop = 0, ok = 0, ownPcs = 0;
            foreach (var rec in s.Split('~'))
            {
                var a = rec.Split('>'); if (a.Length != 4) continue;
                recs++;
                Hero h; if (!heroes.TryGetValue(a[0], out h)) { try { h = om.GetObject<Hero>(a[0]); } catch { h = null; } }
                CharacterObject c = null; try { c = om.GetObject<CharacterObject>(a[1]); } catch { }
                if (h == null) { noHero++; continue; }
                if (c == null) { noTroop++; continue; }
                ok++;
                var k = new Kit { Troop = c, Template = a[2] == "T" };
                foreach (var tok0 in a[3].Split(','))
                {
                    if (tok0.Length == 0) continue;
                    bool own = tok0[0] == '+';
                    var tok = own ? tok0.Substring(1) : tok0;
                    if (tok.Length == 0) continue;
                    var p = tok.Split(':');
                    ItemObject it = null; try { it = om.GetObject<ItemObject>(p[0]); } catch { }
                    if (it == null) continue;
                    ItemModifier m = null; if (p.Length > 1) { try { m = om.GetObject<ItemModifier>(p[1]); } catch { } }
                    if (own) { k.Own.Add(new EquipmentElement(it, m)); ownPcs++; } else k.Items.Add(new EquipmentElement(it, m));
                }
                Push(h, k);
            }
            Log.Info("Komplet rekruta: z zapisu (" + why + ") " + ok + " kompletow z " + recs + " (brak notabla " + noHero + ", brak oddzialu " + noTroop + "; dobytek " + ownPcs + " szt.).");
            Reconcile("po wczytaniu");
        }
    }
}
