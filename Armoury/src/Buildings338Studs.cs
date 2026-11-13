using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// 338 grupa 12 - STADNINA KONI BOJOWYCH ZAMKU (BK Warhorse Studs: "Horses will be added to the Stash. Their limit is based on local pastureland and studs level").
    /// BK RunStuds (BKBuildingsBehavior:369-403): (a) siedzi w ticku miast (DailyTickTownEvent), a wymaga IsCastle - nie biegnie nigdy; (b) warunek odwrocony (tylko
    /// zamki BEZ rancza); (c) kon kultury zamku - dla kultur ROT FirstOrDefault = null -> NRE w ItemRoster; (d) kon z niczego.
    /// Naprawa (wlasny tick zamkow, jak porty - grupa 9): zamek ze Studs &gt; 0 (z ranczem i bez); liczby BK: limit = pastwiska / 10000 + 2 x poziom koni bojowych
    /// w Stash, szansa 2% na dobe. Typ konia nigdy pusty: kon bojowy kultury zamku -> kultury pana -> kultury krolestwa -> dowolny kon bojowy z handlu.
    /// Bez konia z niczego: hodowla zamienia 1 zwyklego konia (kategoria Horse) na 1 bojowego - najpierw ze Stash zamku, potem z magazynu zamku, potem z magazynu
    /// wsi zamku (kasa zamku placi wsi cene konia; bez kasy - brak przyrostu). Brak zwyklego konia = brak przyrostu. Prefiks na BK RunStuds: zamek przy kluczu - pominiety
    /// (gdyby BK kiedys tykal zamki - bez podwojnej hodowli).
    /// </summary>
    internal static class Buildings338Studs
    {
        internal const string StudsId = "bk_building_castle_studs";
        private const float Chance = 0.02f;
        private static bool _hooked;
        private static int _castles, _bred, _fromStash, _fromCastle, _fromVillage, _noSource, _full, _noType;
        private static List<ItemObject> _warHorses;
        private static long _sumBred, _sumNoSource; private static int _sumDays;   // 338c: suma biegu (sesji) do linii doby

        internal static void SessionStart()
        {
            _warHorses = null;
            _sumBred = _sumNoSource = 0; _sumDays = 0;
            var bkb = AccessTools.TypeByName("BannerKings.Behaviours.BKBuildingsBehavior");
            var m = bkb != null ? AccessTools.Method(bkb, "RunStuds", new[] { typeof(Town) }) : null;
            if (!_hooked && m != null)
            {
                Buildings338.H.Patch(m, prefix: new HarmonyMethod(typeof(Buildings338Studs), nameof(RunStudsPrefix)) { priority = Priority.First });
                _hooked = true;
            }
            Buildings338.Hook("stadniny: BK RunStuds " + (_hooked ? "przejety dla zamkow" : "BRAK"));
        }

        public static bool RunStudsPrefix(Town town)
        {
            if (town != null && town.IsCastle && Buildings338.StudsOn && !Buildings338Ports.BkTicksCastles) return false;
            if (town != null && town.IsCastle && Buildings338Works.On && Buildings338Ports.BkTicksCastles && !Buildings338Works.HasPasture(town)) return false;   // 476-p3 (recenzja A4): BK tyka zamki sam - bez pastwiska tez nie hoduje
            return true;
        }   // BK tyka zamki sam - nasz tick stoi, wiec BK hoduje (recenzja 338)

        private static List<ItemObject> WarHorses()
        {
            var l = _warHorses;
            if (l != null) return l;
            l = new List<ItemObject>();
            foreach (var it in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
                if (it != null && it.ItemCategory == DefaultItemCategories.WarHorse && !it.NotMerchandise && it.IsReady) l.Add(it);
            _warHorses = l;
            return l;
        }

        private static ItemObject Pick(CultureObject c)
        {
            if (c == null) return null;
            foreach (var it in WarHorses()) if (it.Culture == c) return it;
            return null;
        }

        private static ItemObject HorseType(Town t)
        {
            var st = t.Settlement;
            var it = Pick(t.Culture);
            if (it == null && st.Owner != null) it = Pick(st.Owner.Culture);
            if (it == null && st.MapFaction != null) it = Pick(st.MapFaction.Culture);
            if (it == null)   // recenzja 338: najtanszy kon bojowy z handlu (nie przypadkowy pierwszy z listy)
                foreach (var x in WarHorses()) if (it == null || x.Value < it.Value) it = x;
            return it;
        }

        /// <summary>Zwykly kon (kategoria Horse) z rosteru; null - brak.</summary>
        private static ItemObject TakeHorse(ItemRoster r)
        {
            if (r == null) return null;
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                var it = el.EquipmentElement.Item;
                if (it != null && el.Amount > 0 && it.ItemCategory == DefaultItemCategories.Horse && el.EquipmentElement.ItemModifier == null)
                {
                    r.AddToCounts(it, -1);
                    return it;
                }
            }
            return null;
        }

        internal static void CastleTick()
        {
            _castles = _bred = _fromStash = _fromCastle = _fromVillage = _noSource = _full = _noType = 0;
            if (!Buildings338.StudsOn || Buildings338Ports.BkTicksCastles || Campaign.Current == null) return;
            foreach (var t in Town.AllCastles)
            {
                try
                {
                    int lvl = t != null ? Buildings338.Level(t, StudsId) : 0;
                    if (lvl <= 0 || t.Settlement == null || t.Settlement.Stash == null) continue;
                    _castles++;
                    float limit = Buildings338.Pasture(Buildings338.PopData(t.Settlement)) / 10000f + lvl * 2f;
                    int have = 0;
                    var stash = t.Settlement.Stash;
                    for (int i = 0; i < stash.Count; i++)
                    {
                        var el = stash.GetElementCopyAtIndex(i);
                        if (el.EquipmentElement.Item != null && el.EquipmentElement.Item.ItemCategory == DefaultItemCategories.WarHorse) have += el.Amount;
                    }
                    if (!(have < limit)) { _full++; continue; }
                    if (Buildings338Works.StudsNoPasture(t)) continue;                                  // 476-a: bez pastwiska okolicy brak hodowli
                    if (!(MBRandom.RandomFloat < Buildings338Works.StudsChance(lvl))) continue;        // 476-a: 2% x poziom (NIE - 2%)
                    var war = HorseType(t);
                    if (war == null) { _noType++; continue; }
                    // zrodlo: zwykly kon (bez konia z niczego)
                    // recenzja 338: Stash zamku gracza to jego schowek - jego koni hodowla nie bierze (zaczyna od magazynu zamku)
                    bool player = t.OwnerClan == Clan.PlayerClan;
                    ItemObject src = player ? null : TakeHorse(stash);
                    if (src != null) _fromStash++;
                    else if ((src = TakeHorse(t.Settlement.ItemRoster)) != null) _fromCastle++;
                    else
                    {
                        foreach (var v in t.Villages)
                        {
                            if (v == null || v.Settlement == null || v.Settlement.ItemRoster == null) continue;
                            ItemObject h = null;
                            var vr = v.Settlement.ItemRoster;
                            for (int i = 0; i < vr.Count && h == null; i++)
                            {
                                var el = vr.GetElementCopyAtIndex(i);
                                var it = el.EquipmentElement.Item;
                                if (it != null && el.Amount > 0 && it.ItemCategory == DefaultItemCategories.Horse && el.EquipmentElement.ItemModifier == null) h = it;
                            }
                            if (h == null) continue;
                            int price = Math.Max(1, v.GetItemPrice(h, null, false));
                            if (t.Gold < price) continue;
                            t.ChangeGold(-price); v.ChangeGold(price);   // kasa zamku placi wsi cene konia
                            vr.AddToCounts(h, -1);
                            src = h; _fromVillage++;
                            break;
                        }
                    }
                    if (src == null) { _noSource++; continue; }
                    stash.AddToCounts(war, 1);
                    _bred++;
                    try { GoodsLedger.NoteKnownSource(war, 1, "stadnina BK zamku (z konia)"); GoodsLedger.NoteKnownSource(src, -1, "stadnina BK zamku (kon do hodowli)"); } catch { }
                }
                catch (Exception e) { Buildings338.Stumble(Buildings338.GStuds, "Studs.CastleTick", e); }
            }
        }

        internal static void Daily(int day)
        {
            _sumDays++; _sumBred += _bred; _sumNoSource += _noSource;
            Log.Info("Budynki (338) stadniny: dzien " + day + " | zamkow ze Studs " + _castles + " (pelnych " + _full + ") | konie bojowe +" + _bred + " (ze Stash " + _fromStash
                     + ", z magazynu zamku " + _fromCastle + ", z wsi " + _fromVillage + "), bez zrodla " + _noSource + ", bez typu konia " + _noType
                     + " | w biegu: konie bojowe +" + _sumBred + ", bez zrodla " + _sumNoSource + " w " + _sumDays + " dobach" + Buildings338.Tail(Buildings338.GStuds));
        }
    }
}
