using System;
using System.Collections.Generic;
using System.Globalization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// TOWARZYSZE KUPUJA SPRZET ZA WLASNE ZLOTO (CG; Jeff 19.10: "placilem towarzyszowi 2500 - czemu nie kupi sobie lepszego sprzetu, tak jak armia?").
    /// Zloto towarzysza to Hero.Gold: cena najmu idzie gracz -> towarzysz (GiveGoldAction, CompanionPay), dzienny zold - CompanionWage.PayFromWage.
    ///
    /// Kiedy: druzyna gracza w MIESCIE (wjazd albo doba postoju; raz na dobe), nie w wojnie z miastem, nie w oblezeniu, nie w oknie lupu miasta (CitySack.StayWindow),
    /// po zakupach ludzi (sluchacze zarejestrowane po MenPurse). Kto: towarzysze gracza w druzynie gracza (Hero.IsPlayerCompanion, zywi, nie w niewoli).
    /// Budzet: kiesa towarzysza minus rezerwa CompanionGearReserveDays dni jego zoldu (tabela CompanionWage.DailyOf).
    /// Co (zestaw bojowy): zbroje - korpus, helm, nogi, rece, plaszcz - puste sloty albo sztuka co najmniej 1 tier nizej (AiGear.TierOf) i slabsza (RangedRank.Key);
    /// bron 0-3 - tylko wymiana tej samej klasy broni (miecz za miecz, kolczan za kolczan) o co najmniej 1 tier wyzej i silniejsza; puste sloty broni, kon i rzad
    /// zostaja (nie wiemy, czym towarzysz ma walczyc; rzad musi pasowac do konia). Sztuki unikatowe (ArmsPricing.IsUnique) nietkniete i nie kupowane.
    /// Wymogi jak na ekranie ekwipunku: CharacterHelper.CanUseItem (umiejetnosc, z naszym wymogiem Atletyki pancerza - HeroGear), plec przedmiotu, kultura jak u armii
    /// (GearFit.CultureOk przy GearFitCulture), u konnego bron uzywalna z siodla, jesli stara byla (MenUpgrade.MountOk); sprawna (bez modyfikatora < 1), nie zakazana
    /// w bitwie (QuartermasterLaw.BarredInBattle). Wybor: najwyzszy tier, potem najsilniejsza, potem tansza - na co go stac; kolejnosc slotow od korpusu.
    /// Zloto: kiesa towarzysza -> kasa miasta (Town.ChangeGold; MoneyLedger.Note169 N169Kit, TownFolk.Sale jak zakupy ludzi gracza), sztuka z polki miasta.
    /// Zdjeta sztuka -> ekwipunek druzyny gracza (MainParty.ItemRoster) - nic nie ginie. Jeden komunikat po angielsku na wizyte; linia "Towarzysze (CG):".
    /// Klucz CompanionBuysOwnGear (czytany na zywo). Nic w zapisie (bramka doby - stan sesji). Watek gry. Potkniecia liczone, pierwsze do Log.Error.
    /// </summary>
    internal static class CompanionGear
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        internal static bool On { get { var s = Settings.Current; return s != null && s.CompanionBuysOwnGear; } }

        private static readonly EquipmentIndex[] ArmorSlots = { EquipmentIndex.Body, EquipmentIndex.Head, EquipmentIndex.Leg, EquipmentIndex.Gloves, EquipmentIndex.Cape };
        private static readonly EquipmentIndex[] WeaponSlots = { EquipmentIndex.Weapon0, EquipmentIndex.Weapon1, EquipmentIndex.Weapon2, EquipmentIndex.Weapon3 };

        private static int _gateDay = -1;
        private static int _stumbles, _sessN; private static long _sessGold;
        private static bool _errLogged;

        private sealed class Ware { public EquipmentElement El; public int Left, Tier, Price = -1; public long Power; }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (!_errLogged) { _errLogged = true; Log.Error("CompanionGear." + where, e); }
        }

        internal static void SessionStart() { _gateDay = -1; _sessN = 0; _sessGold = 0; }

        // ------------------------------------------------------------ wejscia (sluchacze ArmouryBehavior)
        internal static void OnEntered(MobileParty mp, Settlement st, Hero h)
        {
            try { if (mp != null && mp.IsMainParty) Visit(st, "wjazd"); }
            catch (Exception e) { Stumble("OnEntered", e); }
        }

        internal static void OnDailyTickParty(MobileParty mp)
        {
            try { if (mp != null && mp.IsMainParty) Visit(mp.CurrentSettlement, "postoj"); }
            catch (Exception e) { Stumble("OnDailyTickParty", e); }
        }

        private static ItemObject.ItemTypeEnum ArmorType(EquipmentIndex i)
        {
            switch (i)
            {
                case EquipmentIndex.Head: return ItemObject.ItemTypeEnum.HeadArmor;
                case EquipmentIndex.Body: return ItemObject.ItemTypeEnum.BodyArmor;
                case EquipmentIndex.Leg: return ItemObject.ItemTypeEnum.LegArmor;
                case EquipmentIndex.Gloves: return ItemObject.ItemTypeEnum.HandArmor;
                default: return ItemObject.ItemTypeEnum.Cape;
            }
        }

        // ------------------------------------------------------------ wizyta
        private static void Visit(Settlement st, string why)
        {
            if (!On || st == null || !st.IsTown || st.Town == null || st.ItemRoster == null || Campaign.Current == null) return;
            var main = MobileParty.MainParty;
            if (main == null || main.CurrentSettlement != st || main.MapEvent != null || st.IsUnderSiege || main.MemberRoster == null) return;
            if (FactionManager.IsAtWarAgainstFaction(main.MapFaction, st.MapFaction)) return;
            if (CitySack.StayWindow(st, main)) return;   // lup miast L3: w oknie lupu nikt z druzyny gracza nie kupuje z polki lupionej osady
            var heroes = new List<Hero>();
            var r = main.MemberRoster;
            for (int i = 0; i < r.Count; i++)
            {
                var ch = r.GetCharacterAtIndex(i);
                if (ch == null || !ch.IsHero) continue;
                var h = ch.HeroObject;
                if (h != null && h != Hero.MainHero && h.IsPlayerCompanion && h.IsAlive && !h.IsPrisoner && h.BattleEquipment != null) heroes.Add(h);
            }
            if (heroes.Count == 0) return;
            int day = (int)CampaignTime.Now.ToDays;
            if (_gateDay == day) return;
            _gateDay = day;
            var s = Settings.Current;
            var wares = new Dictionary<ItemObject.ItemTypeEnum, List<Ware>>();
            int n = 0, gold = 0, poor = 0, nothing = 0;
            long purse0 = 0, purse1 = 0;
            var lines = new List<string>();
            var said = new List<string>();
            foreach (var h in heroes)
            {
                purse0 += h.Gold;
                try
                {
                    int reserve = (int)Math.Ceiling(Math.Max(0f, s.CompanionGearReserveDays) * CompanionWage.DailyOf(h.Level));
                    int budget = h.Gold - reserve;
                    if (budget <= 0) { poor++; continue; }
                    var bought = new List<string>();
                    int hn = 0, hg = 0;
                    foreach (var slot in ArmorSlots) TrySlot(h, slot, true, st, main, wares, ref budget, ref hn, ref hg, bought, lines);
                    foreach (var slot in WeaponSlots) TrySlot(h, slot, false, st, main, wares, ref budget, ref hn, ref hg, bought, lines);
                    if (hn == 0) { nothing++; continue; }
                    n += hn; gold += hg;
                    if (said.Count < 4) said.Add(h.Name + " - " + string.Join(", ", bought.ToArray()));
                }
                catch (Exception e) { Stumble("Visit(" + h.StringId + ")", e); }
                finally { purse1 += h.Gold; }
            }
            _sessN += n; _sessGold += gold;
            if (n > 0)
                Log.Player("Your companions bought better gear with their own coin in " + st.Name + " for " + gold + " denars: " + string.Join("; ", said.ToArray())
                           + (said.Count < heroes.Count - poor - nothing ? "; ..." : "") + ". The pieces they took off are in your inventory.");
            Log.Info("Towarzysze (CG): " + st.Name + " (" + why + ") dzien " + day + " | klucz CompanionBuysOwnGear TAK, rezerwa " + Math.Max(0f, s.CompanionGearReserveDays).ToString("0", Inv)
                     + " dni zoldu | towarzysze w druzynie " + heroes.Count + ", kupili " + n + " szt. za " + gold + " d" + (lines.Count > 0 ? " (" + string.Join("; ", lines.ToArray()) + ")" : "")
                     + " | bez zakupow: za malo zlota ponad rezerwe " + poor + ", nic lepszego na targu na ich kiese " + nothing
                     + " | kiesy towarzyszy " + purse0 + " -> " + purse1 + " | sesja: " + _sessN + " szt. za " + _sessGold + " d | potkniecia " + _stumbles + ".");
        }

        private static void TrySlot(Hero h, EquipmentIndex slot, bool armor, Settlement st, MobileParty main, Dictionary<ItemObject.ItemTypeEnum, List<Ware>> wares,
                                    ref int budget, ref int hn, ref int hg, List<string> bought, List<string> lines)
        {
            if (budget <= 0) return;
            var eq = h.BattleEquipment;
            var old = eq[slot];
            var oldIt = old.Item;
            ItemObject.ItemTypeEnum type;
            bool hasCls = false; WeaponClass cls = default(WeaponClass);
            if (armor)
            {
                type = ArmorType(slot);
                if (oldIt != null && oldIt.ItemType != type) return;
            }
            else
            {
                if (oldIt == null || oldIt.PrimaryWeapon == null) return;   // pusty slot broni - nie wiemy, czym ma walczyc
                type = oldIt.ItemType;
                if (type == ItemObject.ItemTypeEnum.Horse || type == ItemObject.ItemTypeEnum.HorseHarness || type == ItemObject.ItemTypeEnum.Banner) return;
                hasCls = true; cls = oldIt.PrimaryWeapon.WeaponClass;
            }
            if (oldIt != null && ArmsPricing.IsUnique(oldIt)) return;
            int oldTier = oldIt != null ? AiGear.TierOf(oldIt) : 0;
            long oldPow = oldIt != null ? RangedRank.Key(oldIt) : long.MinValue;
            bool mounted = eq[EquipmentIndex.Horse].Item != null;
            bool oldMountOk = oldIt == null || MenUpgrade.MountOk(oldIt);
            Ware best = null;
            foreach (var w in Wares(wares, type, st))
            {
                if (w.Left <= 0) continue;
                var it = w.El.Item;
                if (hasCls && (it.PrimaryWeapon == null || it.PrimaryWeapon.WeaponClass != cls)) continue;
                if (oldIt != null && (w.Tier < oldTier + 1 || w.Power <= oldPow)) continue;   // wyraznie gorsza: co najmniej 1 tier nizej i slabsza
                if (!armor && mounted && oldMountOk && !MenUpgrade.MountOk(it)) continue;
                if (!Fits(h, w.El)) continue;
                if (w.Price < 0) { try { w.Price = st.Town.MarketData.GetPrice(w.El, main, false, st.Party); } catch { w.Price = 0; } }
                if (w.Price <= 0 || w.Price > budget) continue;
                if (best == null || w.Tier > best.Tier || (w.Tier == best.Tier && (w.Power > best.Power || (w.Power == best.Power && w.Price < best.Price)))) best = w;
            }
            if (best == null) return;
            int price = best.Price;
            st.ItemRoster.AddToCounts(best.El, -1); best.Left--;
            h.ChangeHeroGold(-price);
            st.Town.ChangeGold(price);
            MoneyLedger.Note169(MoneyLedger.N169Kit, st, price);   // kasy miast - zakup sprzetu z polki (jak braki ludzi gracza)
            try { TownFolk.Sale(st, price, TownFolk.MixGear); } catch { }
            eq[slot] = best.El;
            if (oldIt != null) main.ItemRoster.AddToCounts(old, 1);   // zdjeta sztuka do ekwipunku druzyny gracza - nic nie ginie
            foreach (var w in Wares(wares, type, st)) w.Price = -1;    // polka sie zmienila - ceny tego typu od nowa
            budget -= price; hn++; hg += price;
            bought.Add(best.El.Item.Name + (oldIt != null ? " for " + oldIt.Name : "") + " (" + price + ")");
            if (lines.Count < 8) lines.Add(h.Name + ": " + type + " t" + oldTier + "->t" + best.Tier + " " + (oldIt != null ? oldIt.StringId : "pusty") + "->" + best.El.Item.StringId + " " + price);
        }

        /// <summary>Czy towarzysz moze to nosic: wymog ekranu ekwipunku (CanUseItem z naszym wymogiem pancerza), plec, kultura jak u armii (przy GearFitCulture).</summary>
        private static bool Fits(Hero h, EquipmentElement el)
        {
            var it = el.Item;
            if (it == null) return false;
            try
            {
                if (h.IsFemale && (it.ItemFlags & ItemFlags.NotUsableByFemale) != 0) return false;
                if (!h.IsFemale && (it.ItemFlags & ItemFlags.NotUsableByMale) != 0) return false;
                TextObject why;
                if (!global::Helpers.CharacterHelper.CanUseItem(h.CharacterObject, el, out why)) return false;
                if (GearFit.CultureOn && !GearFit.CultureOk(h.CharacterObject, it)) return false;
                return true;
            }
            catch (Exception e) { Stumble("Fits", e); return false; }
        }

        private static List<Ware> Wares(Dictionary<ItemObject.ItemTypeEnum, List<Ware>> cache, ItemObject.ItemTypeEnum type, Settlement st)
        {
            List<Ware> list;
            if (cache.TryGetValue(type, out list)) return list;
            cache[type] = list = new List<Ware>();
            try
            {
                var r = st.ItemRoster;
                for (int i = 0; i < r.Count; i++)
                {
                    var el = r.GetElementCopyAtIndex(i);
                    var it = el.EquipmentElement.Item;
                    if (it == null || el.Amount <= 0 || it.ItemType != type) continue;
                    if (ArmsPricing.IsUnique(it) || QuartermasterLaw.BarredInBattle(it)) continue;
                    var m = el.EquipmentElement.ItemModifier;
                    if (m != null && m.PriceMultiplier < 1f) continue;   // sprawna
                    list.Add(new Ware { El = el.EquipmentElement, Left = el.Amount, Tier = AiGear.TierOf(it), Power = RangedRank.Key(it) });
                }
            }
            catch (Exception e) { Stumble("Wares", e); }
            return list;
        }
    }
}
