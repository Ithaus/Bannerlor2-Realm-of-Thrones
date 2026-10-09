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
    ///  - Ochotnicy z poczatku kampanii (dorobek stuleci, ColdStart) maja komplet wzorca; brak zapisu (stary save) - wzorzec, w logu.
    /// Gracz: bez zmian (DTE), do decyzji.
    /// </summary>
    internal static class RecruitKit
    {
        internal static bool On { get { var s = Settings.Current; return s != null && s.KitFromNotable && s.VolunteerKitEnabled; } }

        private class Kit { internal CharacterObject Troop; internal bool Template; internal List<EquipmentElement> Items = new List<EquipmentElement>(); }
        private static readonly Dictionary<Hero, List<Kit>> _kits = new Dictionary<Hero, List<Kit>>();
        private static int _dayKits, _dayLegacy, _dayTier1, _daySold, _dayStamp = -1;
        // 161: zapis z SyncData czeka na rozwiazanie do startu sesji - w SyncData bohaterowie gry nie sa jeszcze
        // do znalezienia (MBObjectManager.GetObject<Hero> = null), wiec dotad KAZDE wczytanie gubilo wszystkie komplety
        private static string _pending;

        internal static void Reset() { _kits.Clear(); _pending = null; _dayKits = _dayLegacy = _dayTier1 = _daySold = 0; _dayStamp = -1; }

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

        private static List<EquipmentElement> Materialize(Kit k) { var l = new List<EquipmentElement>(); if (k == null) return l; if (k.Template) l.AddRange(TemplateItems(k.Troop)); l.AddRange(k.Items); return l; }

        /// <summary>Awans w puli: komplet przechodzi na nowy szczebel i rosnie o kupione sztuki.</summary>
        internal static void OnUpgrade(Hero n, CharacterObject x, CharacterObject y, List<EquipmentElement> bought)
        {
            if (!On || n == null || x == null || y == null) return;
            var old = Pop(n, x);
            var items = old != null ? Materialize(old) : new List<EquipmentElement>();
            if (old == null)
            {
                // dobytek X, ktory Y tez nosi (tier 1 - wlasny; starszy bez zapisu - przyblizenie)
                var ySet = new HashSet<ItemObject>(); foreach (var e in TemplateItems(y)) ySet.Add(e.Item);
                foreach (var e in TemplateItems(x)) if (ySet.Contains(e.Item)) items.Add(e);
            }
            if (bought != null) items.AddRange(bought);
            Push(n, new Kit { Troop = y, Items = items });
        }

        /// <summary>Ochotnik zniknal z puli bez werbunku: notabl sprzedaje jego kupione rzeczy na targu.</summary>
        internal static void OnVanished(Hero n, CharacterObject x, Settlement market)
        {
            if (!On) return;
            SellOff(n, Pop(n, x), market);
        }

        /// <summary>Kupione rzeczy kompletu na targ: notabl dostaje cene skupu, nie wiecej niz kasa miasta.</summary>
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
                MoneyLedger.Note169(MoneyLedger.N169Other, market, -price);   // paczka 169: linia kas (tylko licznik)
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
        /// notabl nie zyje albo nie jest juz notablem: rzeczy wracaja na targ jego osady bez zaplaty (zloto zmarlego
        /// nikomu by nie przypadlo). Zostaja najnowsze komplety danego oddzialu.
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

        /// <summary>Prefiks DTE OnTroopRecruited (AI): zamiast kompletu z niczego - zapisane komplety ochotnikow.</summary>
        internal static bool OnRecruited(Hero recruiterHero, Hero recruitmentSource, CharacterObject troop, int amount)
        {
            if (!On || recruiterHero == null || troop == null || amount <= 0) return true;
            // gracz: komplet DTE bez zmian, ale zapis kompletow tych ochotnikow zdejmujemy (inaczej zostalby sierota w puli)
            if (recruiterHero == Hero.MainHero) { for (int i = 0; i < amount; i++) Pop(recruitmentSource, troop); return true; }
            if (troop.Tier <= 1) { _dayTier1 += amount; return true; }   // wlasny dobytek rekruta - komplet DTE jak dotad
            var mp = recruiterHero.PartyBelongedTo;
            if (mp == null) return true;
            Day();
            for (int i = 0; i < amount; i++)
            {
                var k = Pop(recruitmentSource, troop);
                List<EquipmentElement> items;
                if (k != null) { items = Materialize(k); _dayKits++; }
                else { items = TemplateItems(troop); _dayLegacy++; }   // ochotnik bez zapisu (sprzed systemu) - wzorzec
                foreach (var e in items)
                {
                    if (e.Item == null) continue;
                    if (AiGear.AddToArmory(mp, e.Item, 1)) AiWear.NoteBought(mp, e, 1);
                }
            }
            return false;
        }

        /// <summary>Dorobek stuleci: ochotnicy tieru 2+ w pulach na starcie kampanii maja komplet wzorca.</summary>
        internal static int SeedCampaignStart()
        {
            int n = 0;
            foreach (var h in Hero.AllAliveHeroes)
            {
                if (h == null || !h.IsNotable || h.VolunteerTypes == null) continue;
                foreach (var c in h.VolunteerTypes) if (c != null && c.Tier >= 2) { Push(h, new Kit { Troop = c, Template = true }); n++; }
            }
            return n;
        }

        private static void Day()
        {
            int d = (int)CampaignTime.Now.ToDays;
            if (d == _dayStamp) return;
            if (_dayStamp >= 0 && (_dayKits + _dayLegacy + _dayTier1 + _daySold) > 0)
                Log.Info("Komplet rekruta: dzien " + _dayStamp + " - werbunek AI z kompletem od notabla " + _dayKits + ", bez zapisu (wzorzec) " + _dayLegacy
                         + ", tier 1 z wlasnym dobytkiem " + _dayTier1 + "; rzeczy ochotnikow, ktorzy odeszli, sprzedane " + _daySold + " szt.");
            _dayKits = _dayLegacy = _dayTier1 = _daySold = 0; _dayStamp = d;
        }

        // ------------------------------------------------------------ zapis: notabl>oddzial>T|przedmiot:stan,...~
        internal static string Export()
        {
            if (_pending != null) ResolvePending("zapis przed startem sesji");
            var sb = new StringBuilder();
            foreach (var kv in _kits)
                foreach (var k in kv.Value)
                {
                    if (kv.Key == null || k.Troop == null) continue;
                    sb.Append(kv.Key.StringId).Append('>').Append(k.Troop.StringId).Append('>').Append(k.Template ? "T" : "-").Append('>');
                    for (int i = 0; i < k.Items.Count; i++)
                    {
                        var e = k.Items[i]; if (e.Item == null) continue;
                        if (i > 0) sb.Append(',');
                        sb.Append(e.Item.StringId); if (e.ItemModifier != null) sb.Append(':').Append(e.ItemModifier.StringId);
                    }
                    sb.Append('~');
                }
            return sb.ToString();
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
            int recs = 0, noHero = 0, noTroop = 0, ok = 0;
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
                foreach (var tok in a[3].Split(','))
                {
                    if (tok.Length == 0) continue;
                    var p = tok.Split(':');
                    var it = om.GetObject<ItemObject>(p[0]); if (it == null) continue;
                    ItemModifier m = null; if (p.Length > 1) { try { m = om.GetObject<ItemModifier>(p[1]); } catch { } }
                    k.Items.Add(new EquipmentElement(it, m));
                }
                Push(h, k);
            }
            Log.Info("Komplet rekruta: z zapisu (" + why + ") " + ok + " kompletow z " + recs + " (brak notabla " + noHero + ", brak oddzialu " + noTroop + ").");
            Reconcile("po wczytaniu");
        }
    }
}
