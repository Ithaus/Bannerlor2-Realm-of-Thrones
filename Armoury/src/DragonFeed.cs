using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// 24c SMOKI JEDZA OWCE (kanon; Jeff 09.10 "tak"; propozycja Z11 audytu 09.10; inwentarz B7). Kanon [H]: Drogon porywa owce i kozy z pastwisk wokol
    /// Meereen ("Taniec ze smokami"). Dotad smok ROT byl darmowy na mapie - caly kod smokow ROT jest w bitwie (ROTDragonBehavior na mapie tylko perk Beast
    /// Whisperer; FoodConsumptionBehaviorPatch tylko wyklucza smoki z rozmnazania zwierzat) - nie jadl nic.
    /// KTO MA SMOKA: bohater z wierzchowcem-smokiem w slocie konia zestawu bojowego (ROT: MonsterUsage "dragon"/"dragonfly", id dragon_*) - po czystce CS
    /// (Mends.DragonPurge) tylko Daenerys i rod gracza; do tego smoki w taborze partii GRACZA (quest Valyrian Thief - CS czysci tabory AI).
    /// REGULA (jedna dla Daenerys, gracza i kazdego innego): kazdy smok zjada co dobe DragonMeat (2) "owce" miesa: owca 1, swinia 2, krowa 4 (mieso z uboju
    /// BK: owca 1, swinia 2, krowa 4-6). Najpierw zwierzeta z taboru partii jezdzca (owce, potem swinie, potem krowy), a gdy ich brak - poluje na stada
    /// najblizszej wsi w promieniu DragonRange (50 jednostek mapy, ok. 240 km): bierze zwierzeta z magazynu wsi (towar, ktory wies wiezie na targ), od
    /// najblizszej wsi z zapasem; wies spalona albo rabowana - pominieta. Nadwyzka z krowy przechodzi na nastepna dobe (syty smok nie poluje); brak - smok
    /// glodny (licznik, bez innych skutkow). Jezdziec bez partii w osadzie poluje wokol osady; w niewoli albo bez polozenia - smok nie je (licznik).
    /// OBIEG: zwierzeta znikaja jawnie - ksiega towarow, ujscie "zjedzone przez smoka (24c)" (wlasna ramka GoodsLedger.FDragon: tabor albo stada wsi);
    /// wies traci towar (i utarg, ktory by z niego miala); nikt nic nie placi (smok bierze). Gracz: komunikat po angielsku przy kazdym posilku.
    /// Sytosc (nadwyzka z krowy) nie idzie do zapisu - po wczytaniu smok zaczyna od zera (najwyzej jedna owca roznicy).
    /// Wylacznik DragonsEatHerds (off = 449d453). Linia doby "Smoki (24c)" (tylko log).
    /// </summary>
    internal static class DragonFeed
    {
        private const float DragonMeat = 2f;       // owiec miesa na dobe na smoka [S, Z11: "2 owce albo 1 krowa"]
        private const float DragonRange = 50f;     // promien polowania (jednostki mapy; Winterfell - White Harbor ok. 100)
        private static readonly string[] Animals = { "sheep", "hog", "cow" };   // kolejnosc: owce najpierw (kanon), potem swinie, potem krowy
        private static readonly float[] Meat = { 1f, 2f, 4f };

        private static readonly Dictionary<string, float> _sated = new Dictionary<string, float>();   // smok -> nadwyzka miesa z wczoraj (bez zapisu)
        private static int _dDragons, _dHungry, _dIdle, _stumbles;
        private static readonly int[] _dHerd = new int[3], _dVillage = new int[3];
        private static readonly Dictionary<string, int> _dVillages = new Dictionary<string, int>();
        private static readonly List<string> _dWho = new List<string>();
        private static ItemObject[] _items;

        internal static void Reset()
        {
            _sated.Clear(); _dVillages.Clear(); _dWho.Clear(); _items = null;
            _dDragons = _dHungry = _dIdle = _stumbles = 0;
            for (int i = 0; i < 3; i++) { _dHerd[i] = 0; _dVillage[i] = 0; }
        }

        private static bool On { get { var s = Settings.Current; return s != null && s.DragonsEatHerds; } }

        /// <summary>Ta sama definicja smoka co CS Mends.IsDragonMount (id dragon_* jako kon albo MonsterUsage dragon / dragonfly).</summary>
        internal static bool IsDragon(ItemObject it)
        {
            try
            {
                if (it == null) return false;
                var id = it.StringId ?? "";
                if (id.StartsWith("dragon_", StringComparison.Ordinal) && it.ItemType == ItemObject.ItemTypeEnum.Horse) return true;
                var mu = it.HorseComponent != null && it.HorseComponent.Monster != null ? (it.HorseComponent.Monster.MonsterUsage ?? "") : "";
                return mu == "dragon" || mu == "dragonfly";
            }
            catch { return false; }
        }

        private static bool EnsureItems()
        {
            if (_items != null) return true;
            var a = new ItemObject[3];
            for (int i = 0; i < 3; i++)
            {
                try { a[i] = TaleWorlds.ObjectSystem.MBObjectManager.Instance.GetObject<ItemObject>(Animals[i]); } catch { }
            }
            _items = a;
            return true;
        }

        /// <summary>Doba: wolane PRZED GoodsLedger.Daily (ten sam dzien w ksiedze towarow i w linii "Smoki (24c)").</summary>
        internal static void Daily()
        {
            if (!On) return;
            EnsureItems();
            // smoki: bohaterowie (slot konia zestawu bojowego) + tabor partii gracza
            var eaters = new List<KeyValuePair<string, object>>();   // klucz smoka -> Hero albo MobileParty (tabor gracza)
            foreach (var h in Hero.AllAliveHeroes)
            {
                try
                {
                    if (h == null) continue;
                    var eq = h.BattleEquipment;
                    if (eq == null) continue;
                    var it = eq[EquipmentIndex.Horse].Item;
                    if (it == null || !IsDragon(it)) continue;
                    eaters.Add(new KeyValuePair<string, object>(h.StringId + ":" + it.StringId, h));
                }
                catch { _stumbles++; }
            }
            try
            {
                var mp = MobileParty.MainParty;
                if (mp != null && mp.ItemRoster != null)
                    for (int i = 0; i < mp.ItemRoster.Count; i++)
                    {
                        var it = mp.ItemRoster.GetItemAtIndex(i);
                        if (it == null || !IsDragon(it)) continue;
                        int n = mp.ItemRoster.GetElementNumber(i);
                        for (int k = 0; k < n; k++) eaters.Add(new KeyValuePair<string, object>("@roster:" + it.StringId + ":" + k, mp));
                    }
            }
            catch { _stumbles++; }
            foreach (var e in eaters)
            {
                try { Feed(e.Key, e.Value); }
                catch (Exception ex) { _stumbles++; if (_stumbles <= 3) Log.Error("DragonFeed.Feed", ex); }
            }
        }

        private static void Feed(string key, object who)
        {
            var hero = who as Hero;
            MobileParty party = hero != null ? hero.PartyBelongedTo : who as MobileParty;
            if (hero != null && (hero.IsPrisoner || party == null && hero.CurrentSettlement == null)) { _dIdle++; return; }
            _dDragons++;
            string[] kp = key.Split(':');
            _dWho.Add((hero != null ? hero.Name.ToString() : "gracz (tabor)") + " " + (kp.Length > 1 ? kp[1] : key));
            float sated; _sated.TryGetValue(key, out sated);
            float need = DragonMeat - sated;
            if (need <= 0f) { _sated[key] = -need; return; }
            bool player = party != null && party == MobileParty.MainParty;
            var eaten = new int[3];
            // 1. tabor partii jezdzca
            if (party != null && party.ItemRoster != null)
                need = Take(party.ItemRoster, need, eaten, _dHerd, "z taboru", party);
            if (player && (eaten[0] + eaten[1] + eaten[2]) > 0)
                Say("Your dragon ate {ANIMALS} from your own herd.", eaten, null);
            // 2. stada najblizszych wsi
            if (need > 0f)
            {
                Vec2 pos;
                try { pos = party != null ? party.GetPosition2D : hero.CurrentSettlement.GetPosition2D; } catch { pos = Vec2.Invalid; }
                if (pos.IsValid)
                {
                    var near = new List<KeyValuePair<float, Village>>();
                    foreach (var v in Village.All)
                    {
                        if (v == null || v.Settlement == null || v.VillageState != Village.VillageStates.Normal) continue;
                        float d = pos.Distance(v.Settlement.GetPosition2D);
                        if (d <= DragonRange) near.Add(new KeyValuePair<float, Village>(d, v));
                    }
                    near.Sort((a, b) => a.Key.CompareTo(b.Key));
                    foreach (var kv in near)
                    {
                        if (need <= 0f) break;
                        var ro = kv.Value.Settlement.ItemRoster;
                        if (ro == null) continue;
                        var got = new int[3];
                        need = Take(ro, need, got, _dVillage, "ze stad wsi", kv.Value.Settlement);
                        int n = got[0] + got[1] + got[2];
                        if (n <= 0) continue;
                        string vn = kv.Value.Name.ToString();
                        int c; _dVillages.TryGetValue(vn, out c); _dVillages[vn] = c + n;
                        if (player) Say("Your dragon took {ANIMALS} from the herds of {VILLAGE}.", got, vn);
                    }
                }
            }
            if (need > 0f)
            {
                _dHungry++;
                _sated[key] = 0f;
                if (player) InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=!}Your dragon found nothing to eat today.").ToString()));
                return;
            }
            _sated[key] = -need;   // nadwyzka (np. z krowy) na jutro
        }

        /// <summary>Zabiera z rosteru zwierzeta (owce, swinie, krowy) az do pokrycia potrzeby; ramka ksiegi towarow FDragon z etykieta zrodla.</summary>
        private static float Take(ItemRoster ro, float need, int[] got, int[] day, string label, object ctx)
        {
            var gf = GoodsLedger.Begin(GoodsLedger.FDragon, ctx);
            if (gf != null) gf.Info = label;
            try
            {
                for (int a = 0; a < 3 && need > 0f; a++)
                {
                    var it = _items[a];
                    if (it == null) continue;
                    int have = ro.GetItemNumber(it);
                    if (have <= 0) continue;
                    int n = Math.Min(have, (int)Math.Ceiling(need / Meat[a]));
                    if (n <= 0) continue;
                    ro.AddToCounts(it, -n);
                    got[a] += n; day[a] += n;
                    need -= n * Meat[a];
                }
            }
            finally { GoodsLedger.End(gf); }
            return need;
        }

        private static void Say(string fmt, int[] got, string village)
        {
            try
            {
                var parts = new List<string>();
                for (int a = 0; a < 3; a++)
                {
                    if (got[a] <= 0 || _items[a] == null) continue;
                    parts.Add(got[a] + " " + (got[a] == 1 ? _items[a].Name.ToString() : Plural(a)));
                }
                var t = new TextObject("{=!}" + fmt);
                t.SetTextVariable("ANIMALS", string.Join(", ", parts.ToArray()));
                if (village != null) t.SetTextVariable("VILLAGE", village);
                InformationManager.DisplayMessage(new InformationMessage(t.ToString()));
            }
            catch { }
        }

        private static string Plural(int a) { return a == 0 ? "sheep" : a == 1 ? "hogs" : "cows"; }

        /// <summary>Linia doby (po Daily, przed GoodsLedger.Daily - ta sama doba co ksiega towarow: dzien zakonczony).</summary>
        internal static void Line()
        {
            if (!On) return;
            int day = (int)CampaignTime.Now.ToDays - 1;
            var sb = new StringBuilder();
            sb.Append("Smoki (24c): dzien ").Append(day).Append(" | smokow jedzacych ").Append(_dDragons);
            if (_dWho.Count > 0) sb.Append(" (").Append(string.Join(", ", _dWho.ToArray())).Append(')');
            sb.Append(", bez partii albo w niewoli ").Append(_dIdle)
              .Append(" | zjedzone z taborow: owce ").Append(_dHerd[0]).Append(", swinie ").Append(_dHerd[1]).Append(", krowy ").Append(_dHerd[2])
              .Append(" | ze stad wsi: owce ").Append(_dVillage[0]).Append(", swinie ").Append(_dVillage[1]).Append(", krowy ").Append(_dVillage[2])
              .Append(" (wsi ").Append(_dVillages.Count);
            if (_dVillages.Count > 0)
            {
                var l = new List<string>();
                foreach (var kv in _dVillages) l.Add(kv.Key + " " + kv.Value);
                sb.Append(": ").Append(string.Join(", ", l.ToArray()));
            }
            sb.Append(") | glodne smokodoby ").Append(_dHungry).Append(" | potkniecia ").Append(_stumbles).Append('.');
            Log.Info(sb.ToString());
            _dDragons = _dHungry = _dIdle = 0;
            for (int i = 0; i < 3; i++) { _dHerd[i] = 0; _dVillage[i] = 0; }
            _dVillages.Clear(); _dWho.Clear();
        }
    }
}
