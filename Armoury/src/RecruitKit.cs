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

        internal static void Reset() { _kits.Clear(); _dayKits = _dayLegacy = _dayTier1 = _daySold = 0; _dayStamp = -1; }

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
            var k = Pop(n, x);
            if (k == null || k.Items.Count == 0 || market == null || market.Town == null) return;
            foreach (var e in k.Items)
            {
                int price = MenPurse.SellPrice(e, market, null);
                if (market.Town.Gold < price) break;
                market.ItemRoster.AddToCounts(e, 1);
                market.Town.ChangeGold(-price);
                n.ChangeHeroGold(price);
                _daySold++;
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

        internal static void Import(string s)
        {
            _kits.Clear();
            if (string.IsNullOrEmpty(s)) return;
            var om = MBObjectManager.Instance;
            foreach (var rec in s.Split('~'))
            {
                var a = rec.Split('>'); if (a.Length != 4) continue;
                var h = om.GetObject<Hero>(a[0]); var c = om.GetObject<CharacterObject>(a[1]);
                if (h == null || c == null) continue;
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
        }
    }
}
