using System;
using System.Collections.Generic;
using System.Globalization;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// f28-wyceny (B) - XP LOTROSTWA ZA NAJAZD, RABUNEK I WYMUSZENIE ZAOPATRZENIA OD WARTOSCI TOWAROW SPRZED PRZELICZENIA (audyt pkt 11;
    /// Jeff 19.10: "TAK NAPRAW TERAZ").
    ///
    /// Gra (DefaultSkillLevelingManager 1.4.8): OnRaid = TradeGoodsTotalValue x 0.5 + konie x 100 + bydlo/juczne x 25; OnLoot = (TradeGoodsTotalValue +
    /// konie x 200 + bydlo/juczne x 50) x (wiesniacy 0.75 napad / 0.5 bez walki, karawana 0.15 / 0.1); OnForceSupplies = to samo x 0.75 / 0.5.
    /// TradeGoodsTotalValue = suma Value x ilosc sztuk IsTradeGood (ItemRoster.OnRosterUpdated) - po cenach historycznych towary wsi i karawan sa
    /// 3-20 x tansze, wiec Lotrostwo rosnie tyle razy wolniej (gracz i lordowie AI). Czesc "za zwierze" bez zmian (sztuki, nie wartosc).
    /// Postfiks na trzech metodach (AccessTools.DeclaredMethod na klasie deklarujacej - pulapka 15.10; NavalDLC przekazuje do niej przez pole):
    /// delta = suma (OrigUnit - Value) x ilosc po sztukach IsTradeGood, ktore przeliczono (OrigUnit != Value; OrigUnit = Orig x CountScale - ladunek
    /// rudy/drewna to 10 starych sztuk), razy TEN SAM mnoznik co gra; dodawana ta sama
    /// droga co gra (HeroDeveloper.AddSkillXp(Roguery, xp, true, true) = OnPersonalSkillExercised). Delta <= 0 (towar drozszy niz dawniej) - nic:
    /// nigdy mniej niz gra. Latki W KAMPANII, raz na proces, tylko przy wlaczonym kluczu (bez klucza - sciezka WGRANIA 87).
    /// Log: linia startu "w N/3 metodach", linia na zdarzenie gracza (do 40 na dobe), linia doby (wszyscy).
    /// </summary>
    internal static class RogueryXp28
    {
        internal static bool On { get { var s = Settings.Current; return s != null && s.HistRogueryXpOldValue && HistoricalPrices.On && HistoricalPrices.Applied; } }

        private static Harmony _h;
        private static bool _tried;
        private static int _hooked, _stumbles;
        private static string _status = "";
        // licznik doby (Daily zeruje)
        private static int _ev, _evAdd, _evP, _linesP;
        private static double _xpGame, _xpAdd, _xpGameP, _xpAddP;

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_stumbles <= 5) Log.Error("RogueryXp28." + where, e);
        }

        public static void RaidPost(MobileParty attackerParty, ItemRoster lootedItems)
        {
            if (lootedItems == null) return;
            float game = (float)lootedItems.TradeGoodsTotalValue * 0.5f + (float)(lootedItems.NumberOfMounts * 100) + (float)(lootedItems.NumberOfLivestockAnimals * 25) + (float)(lootedItems.NumberOfPackAnimals * 25);
            Add("najazd na wies", attackerParty, lootedItems, 0.5f, game);
        }

        public static void LootPost(MobileParty attackerParty, MobileParty forcedParty, ItemRoster lootedItems, bool attacked)
        {
            if (lootedItems == null || forcedParty == null) return;
            float f = 0f; string src = "rabunek";
            if (forcedParty.IsVillager) { f = attacked ? 0.75f : 0.5f; src = attacked ? "napad na wiesniakow" : "wiesniacy oddali towar"; }
            else if (forcedParty.IsCaravan) { f = attacked ? 0.15f : 0.1f; src = attacked ? "napad na karawane" : "karawana oddala towar"; }
            float game = (float)(lootedItems.TradeGoodsTotalValue + lootedItems.NumberOfMounts * 200 + lootedItems.NumberOfLivestockAnimals * 50 + lootedItems.NumberOfPackAnimals * 50) * f;
            Add(src, attackerParty, lootedItems, f, game);
        }

        public static void SupplyPost(MobileParty attackerParty, ItemRoster lootedItems, bool attacked)
        {
            if (lootedItems == null) return;
            float f = attacked ? 0.75f : 0.5f;
            float game = (float)(lootedItems.TradeGoodsTotalValue + lootedItems.NumberOfMounts * 200 + lootedItems.NumberOfLivestockAnimals * 50 + lootedItems.NumberOfPackAnimals * 50) * f;
            Add("wymuszone zaopatrzenie", attackerParty, lootedItems, f, game);
        }

        private static void Add(string src, MobileParty party, ItemRoster loot, float f, float game)
        {
            if (!On || party == null || f <= 0f) return;
            try
            {
                Hero h = party.LeaderHero;
                if (h == null || h.HeroDeveloper == null) return;   // gra tez nic nie daje bez wodza
                long sv = 0, so = 0;
                for (int i = 0; i < loot.Count; i++)
                {
                    var el = loot.GetElementCopyAtIndex(i);
                    var it = el.EquipmentElement.Item;
                    if (it == null || !it.IsTradeGood || el.Amount <= 0) continue;
                    int v = it.Value, o = HistoricalPrices.OrigUnit(it);   // poprawka: ladunek rudy/drewna = 10 starych sztuk (CountScale)
                    if (o <= 0 || o == v) continue;   // tylko sztuki przeliczone
                    sv += (long)v * el.Amount; so += (long)o * el.Amount;
                }
                bool player = party == MobileParty.MainParty;
                _ev++; _xpGame += game; if (player) { _evP++; _xpGameP += game; }
                if (so <= sv) return;   // nigdy mniej niz gra
                float add = (float)(so - sv) * f;
                if (add <= 0f) return;
                h.HeroDeveloper.AddSkillXp(DefaultSkills.Roguery, add, true, true);
                _evAdd++; _xpAdd += add;
                if (player)
                {
                    _xpAddP += add;
                    if (_linesP < 40)
                    {
                        _linesP++;
                        Log.Info("Lotrostwo (f28): " + src + " - " + h.Name + ": towary przeliczone wg gry " + sv + " d, sprzed przeliczenia " + so + " d (mnoznik "
                                 + f.ToString("0.##", CultureInfo.InvariantCulture) + "); XP gry " + game.ToString("0", CultureInfo.InvariantCulture) + " + dodane "
                                 + add.ToString("0", CultureInfo.InvariantCulture) + ".");
                    }
                }
            }
            catch (Exception e) { Stumble("Add(" + src + ")", e); }
        }

        /// <summary>Raz na dobe (ArmouryBehavior.OnDailyTick): linia doby, zerowanie licznikow.</summary>
        internal static void Daily()
        {
            try
            {
                if (_hooked > 0 && On)
                    Log.Info("Lotrostwo (f28): dzien " + ((int)CampaignTime.Now.ToDays - 1) + " - zdarzen " + _ev + " (z dodatkiem " + _evAdd + "), XP gry "
                             + _xpGame.ToString("0", CultureInfo.InvariantCulture) + ", dodane od wartosci sprzed przeliczenia " + _xpAdd.ToString("0", CultureInfo.InvariantCulture)
                             + " | gracz: zdarzen " + _evP + ", XP gry " + _xpGameP.ToString("0", CultureInfo.InvariantCulture) + ", dodane " + _xpAddP.ToString("0", CultureInfo.InvariantCulture)
                             + (_stumbles > 0 ? " | potkniecia " + _stumbles : "") + ".");
            }
            catch (Exception e) { Stumble("Daily", e); }
            _ev = _evAdd = _evP = _linesP = 0; _xpGame = _xpAdd = _xpGameP = _xpAddP = 0;
        }

        /// <summary>Z ArmouryBehavior.OnSessionLaunched (po HistoricalPrices.Apply): latki raz na proces, gdy klucz wlaczony.</summary>
        internal static void SessionStart()
        {
            _ev = _evAdd = _evP = _linesP = 0; _xpGame = _xpAdd = _xpGameP = _xpAddP = 0;
            var s = Settings.Current;
            if (_tried) { Log.Info("Lotrostwo (f28, klucz Hist Roguery Xp Old Value): " + (On ? "CZYNNE" : "NIECZYNNE (klucz albo ceny historyczne wylaczone)") + " - " + _status + "."); return; }
            if (s == null || !s.HistRogueryXpOldValue || !HistoricalPrices.On)
            {
                Log.Info("Lotrostwo (f28, klucz Hist Roguery Xp Old Value): " + (s != null && s.HistRogueryXpOldValue ? "ceny historyczne wylaczone" : "WYLACZONE (MCM)") + " - nic nie wpiete, XP jak w grze.");
                return;   // bez _tried: wlaczony pozniej - wpina przy nastepnym wczytaniu
            }
            _tried = true;
            var miss = new List<string>();
            try
            {
                if (_h == null) _h = new Harmony("com.jeff.armoury.f28rogue");
                var t = typeof(DefaultSkillLevelingManager);
                var plan = new[]
                {
                    new KeyValuePair<string, Type[]>("OnRaid", new[] { typeof(MobileParty), typeof(ItemRoster) }),
                    new KeyValuePair<string, Type[]>("OnLoot", new[] { typeof(MobileParty), typeof(MobileParty), typeof(ItemRoster), typeof(bool) }),
                    new KeyValuePair<string, Type[]>("OnForceSupplies", new[] { typeof(MobileParty), typeof(ItemRoster), typeof(bool) }),
                };
                var posts = new[] { nameof(RaidPost), nameof(LootPost), nameof(SupplyPost) };
                for (int i = 0; i < plan.Length; i++)
                {
                    try
                    {
                        var m = AccessTools.DeclaredMethod(t, plan[i].Key, plan[i].Value);
                        if (m == null) { miss.Add(plan[i].Key + " BRAK"); continue; }
                        _h.Patch(m, postfix: new HarmonyMethod(typeof(RogueryXp28), posts[i]));
                        _hooked++;
                    }
                    catch (Exception e) { miss.Add(plan[i].Key + " BLAD (" + e.GetType().Name + ")"); Stumble("SessionStart(" + plan[i].Key + ")", e); }
                }
            }
            catch (Exception e) { Stumble("SessionStart", e); miss.Add("wyjatek"); }
            _status = "wpiete w " + _hooked + "/3 metodach (DefaultSkillLevelingManager.OnRaid / OnLoot / OnForceSupplies)" + (miss.Count > 0 ? "; BRAK: " + string.Join(", ", miss.ToArray()) : "");
            Log.Info("Lotrostwo (f28, klucz Hist Roguery Xp Old Value): XP Lotrostwa od wartosci towarow sprzed przeliczenia " + _status + ".");
        }
    }
}
