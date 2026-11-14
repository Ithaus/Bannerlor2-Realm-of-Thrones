using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// 511 (perki, kuznia; Jeff 14.11 - audyt perkow docs/audyty-2026-11-14, punkty 40, 41, 42, 91, 92: "ok"). Nasza kuznia (zbroje, luki, kusze, amunicja)
    /// liczyla sie bez perkow kowalskich - przepis Recipes nadpisywal stamine BK (TrueArmourCost.RangedStamina), dymarka gracza nie znala IronMaker przy lupie,
    /// cena sprzedazy znala ArtisanSmith tylko dla broni z ekranu gry, a jakosc Forge.RollQuality nie znala BK Craftsman.
    ///
    ///  41 PracticalSmith (gra, Personal): -50% staminy (PrimaryBonus perka) za sztuke z naszej kuzni - zbroja z zakladki CRAFT BK (postfiks
    ///     BKSmithingModel.CalculateArmorStamina), luk, kusza, amunicja (Forge.Smith) i wlasny projekt (Forge.Begin). Przetop i naprawa bez zmian (w grze perk
    ///     tez dotyczy tylko kucia). XP za prace (305d, SmithXpRule) liczy stamine BEZ perkow (NoPerk) - perk nie tnie nauki.
    ///  91 BK ArtisanSmith (styl zycia Artisan, "Smith"): -10% staminy (PrimaryBonus perka BK) tak samo jak wyzej, x po PracticalSmith (BK mnozy osobno, x0.9);
    ///     -15% (SecondaryBonus perka BK) ceny dnia kuzni przy ForgeHireHistorical (Forge.ForgeDayRent - karnet, dzien projektu i stawka godzinowa z niej)
    ///     i oplaty za robote bez stawek historycznych (Forge.ForgeFee). Kowal dostaje mniej - zloto nie powstaje.
    ///  40 IronMaker (gra): lupa -> sztaby surowki przy kuzni gracza x (1 + IronMakerBloomBonus); ruda (lupa) zuzywa sie dalej jedna na przepis.
    ///  42 ArtisanSmith (gra, PartyLeader): kara handlowa przy sprzedazy zbroi, luku i amunicji WYKUTYCH przez gracza - ten sam wzor co gra dla broni z ekranu
    ///     (AddFactor PrimaryBonus = -50% na czlonie 1.5 + 0.25 x (Tierf - 1)). Gra przyjmuje tylko ItemObject - co jest "nasze", mowi KSIEGA wykutych sztuk
    ///     (id przedmiotu -> sztuk; zapis arm_perk511): dopisuje Forge.Deliver/Finish/Smith, zdejmuje sprzedaz (PlayerInventoryExchangeEvent, isTrading) i doba (nie wiecej,
    ///     niz gracz ma w sakwach i na bohaterach partii; suma wszystkich stosow). Perk tylko, gdy caly zapas wzoru w sakwach jest w ksiedze (recenzja 511). Wycena: postfiks DeclaredMethod DefaultTradeItemPriceFactorModel.GetTradePenalty (Priority.First - przed postfiksem BK
    ///     i 300b; BKROT wola base, wiec Default liczy raz) mnozy wynik przez (num - 0.5 x T) / num, num = czlony gry przed mnoznikami (wynik jest liniowy w num).
    ///  92 BK ArtisanCraftsman: Forge.RollQuality - 5% (SecondaryBonus perka BK) szansy na jakosc o stopien wyzsza (jak BK GetModifierForCraftedItem), potem
    ///     sufit wedlug tieru jak dotad.
    /// Bledy liczone (potkniecia), bez globalnego wylacznika. Linia startu "Perki kuzni (511): ..." i linia doby "Perki kuzni (511): doba ...".
    /// </summary>
    internal static class Perk511
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        [ThreadStatic] internal static int NoPerk;   // > 0: stamina bez perkow (XP pracy 305d)

        private static int _errors, _dErrors;
        private static int _dPieces, _dStamRaw, _dStamNow, _dQualRolls, _dQualUp, _dPriced, _dSoldOurs, _dMade;
        private static readonly Dictionary<string, int> _made = new Dictionary<string, int>();

        private static bool _bkLooked, _bkOk;
        private static PropertyInfo _cfgInstance, _eduManager;
        private static MethodInfo _getEducation, _hasPerk;
        // BKPerks.Instance zyje caly proces, ale BKPerks.Initialize podmienia obiekty perkow w kazdej grze - perk czytany przy kazdym uzyciu (bez starego obiektu)
        private static object _perks;
        private static PropertyInfo _pSmith, _pCraftsman;
        private static PerkObject _bkSmith { get { return PerkOf(_pSmith); } }
        private static PerkObject _bkCraftsman { get { return PerkOf(_pCraftsman); } }

        private static PerkObject PerkOf(PropertyInfo p)
        {
            try { return p != null && _perks != null ? p.GetValue(_perks, null) as PerkObject : null; }
            catch (Exception e) { Stumble("PerkOf", e); return null; }
        }

        private static Settings S { get { return Settings.Current; } }

        internal static void Stumble(string where, Exception e)
        {
            _dErrors++;
            if (++_errors <= 5 || _errors % 100 == 0) Log.Error("Perk511." + where + " (potkniecie " + _errors + ")", e);
        }

        // ------------------------------------------------------------ perki BK (odbicie, jak CrashScribe BkBattlePerks)
        private static bool ResolveBk()
        {
            if (_bkLooked) return _bkOk;
            _bkLooked = true;
            try
            {
                var cfgT = AccessTools.TypeByName("BannerKings.BannerKingsConfig");
                var perksT = AccessTools.TypeByName("BannerKings.Managers.Skills.BKPerks");
                var eduMgrT = AccessTools.TypeByName("BannerKings.Managers.EducationManager");
                var eduT = AccessTools.TypeByName("BannerKings.Managers.Education.EducationData");
                _cfgInstance = cfgT != null ? AccessTools.Property(cfgT, "Instance") : null;
                _eduManager = cfgT != null ? AccessTools.Property(cfgT, "EducationManager") : null;
                _getEducation = eduMgrT != null ? AccessTools.Method(eduMgrT, "GetHeroEducation", new[] { typeof(Hero) }) : null;
                _hasPerk = eduT != null ? AccessTools.Method(eduT, "HasPerk") : null;
                object perks = null;
                for (var b = perksT; b != null && perks == null; b = b.BaseType)
                {
                    var p = b.GetProperty("Instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly);
                    if (p != null) perks = p.GetValue(null, null);
                }
                _perks = perks;
                _pSmith = perksT != null ? AccessTools.Property(perksT, "ArtisanSmith") : null;
                _pCraftsman = perksT != null ? AccessTools.Property(perksT, "ArtisanCraftsman") : null;
                _bkOk = _cfgInstance != null && _eduManager != null && _getEducation != null && _hasPerk != null && _perks != null && (_pSmith != null || _pCraftsman != null);
                if (!_bkOk) Log.Info("Perki kuzni (511): perki BK niedostepne (BannerKings: cfg " + (_cfgInstance != null) + ", edukacja " + (_getEducation != null)
                                     + ", HasPerk " + (_hasPerk != null) + ", BKPerks " + (_perks != null) + ") - Smith i Craftsman nie dzialaja.");
            }
            catch (Exception e) { _bkOk = false; Stumble("ResolveBk", e); }
            return _bkOk;
        }

        private static bool BkHas(Hero h, PerkObject perk)
        {
            if (h == null || perk == null || !ResolveBk()) return false;
            try
            {
                object cfg = _cfgInstance.GetValue(null, null);
                object mgr = cfg != null ? _eduManager.GetValue(cfg, null) : null;
                object edu = mgr != null ? _getEducation.Invoke(mgr, new object[] { h }) : null;
                return edu != null && (bool)_hasPerk.Invoke(edu, new object[] { perk });
            }
            catch (Exception e) { Stumble("BkHas", e); return false; }
        }

        private static bool Has(Hero h, PerkObject p)
        {
            try { return h != null && p != null && h.GetPerkValue(p); } catch { return false; }
        }

        // ------------------------------------------------------------ 41 + 91: stamina
        /// <summary>Mnoznik staminy kowala h za sztuke naszej kuzni: PracticalSmith x BK Smith (1 = bez zmian).</summary>
        internal static float StaminaFactor(Hero h)
        {
            var s = S;
            if (s == null || !s.PerkSmithStamina || h == null) return 1f;
            float f = 1f;
            var ps = DefaultPerks.Crafting.PracticalSmith;
            if (Has(h, ps)) f *= Math.Max(0.05f, 1f + ps.PrimaryBonus);
            var bs = ResolveBk() ? _bkSmith : null;
            if (bs != null && BkHas(h, bs)) f *= Math.Max(0.05f, 1f - Math.Abs(bs.PrimaryBonus));
            return f;
        }

        /// <summary>Stamina sztuki po perkach (raw = przepis). NoPerk > 0 - przepis bez zmian (XP pracy).</summary>
        internal static int Stamina(int raw, Hero h)
        {
            if (NoPerk > 0 || raw <= 0) return raw;
            try
            {
                float f = StaminaFactor(h);
                if (f >= 0.999f) return raw;
                return Math.Max(1, (int)Math.Round(raw * f));
            }
            catch (Exception e) { Stumble("Stamina", e); return raw; }
        }

        /// <summary>Licznik doby: sztuka z naszego kowadla (Forge.Smith / Begin) - stamina przepisu i pobrana.</summary>
        internal static void NoteSpent(int raw, int now)
        {
            _dPieces++; _dStamRaw += Math.Max(0, raw); _dStamNow += Math.Max(0, now);
        }

        // ------------------------------------------------------------ 91: stawka kuzni
        /// <summary>Mnoznik oplaty za kuznie gracza: BK Smith -15% (SecondaryBonus); 1 = bez zmian.</summary>
        internal static float HireFactor()
        {
            try
            {
                var s = S;
                var bs = s != null && s.PerkSmithHireDiscount && Hero.MainHero != null && ResolveBk() ? _bkSmith : null;
                if (bs == null || !BkHas(Hero.MainHero, bs)) return 1f;
                return Math.Max(0.05f, 1f - Math.Abs(bs.SecondaryBonus));
            }
            catch (Exception e) { Stumble("HireFactor", e); return 1f; }
        }

        // ------------------------------------------------------------ 40: IronMaker przy lupie
        /// <summary>Sztaby surowki z lupy przy kuzni kowala h: baza x (1 + IronMakerBloomBonus) z perkiem IronMaker.</summary>
        internal static int BloomBars(int bars, Hero h)
        {
            try
            {
                var s = S;
                if (s == null || s.IronMakerBloomBonus <= 0f || !Has(h, DefaultPerks.Crafting.IronMaker)) return bars;
                return Math.Max(bars, (int)Math.Round(bars * (1f + s.IronMakerBloomBonus)));
            }
            catch (Exception e) { Stumble("BloomBars", e); return bars; }
        }

        // ------------------------------------------------------------ 92: BK Craftsman w losowaniu jakosci
        /// <summary>Forge.RollQuality: z perkiem BK Craftsman szansa SecondaryBonus (5%) na jakosc o stopien wyzsza (przed sufitem tieru).</summary>
        internal static ItemQuality Craftsman(ItemQuality q, Hero smith)
        {
            try
            {
                var s = S;
                var bc = s != null && s.PerkCraftsmanQuality && smith != null && ResolveBk() ? _bkCraftsman : null;
                if (bc == null || !BkHas(smith, bc)) return q;
                _dQualRolls++;
                float p = Math.Abs(bc.SecondaryBonus);
                if (p <= 0f || MBRandom.RandomFloat > p) return q;
                ItemQuality up;
                switch (q)
                {
                    case ItemQuality.Poor: up = ItemQuality.Inferior; break;
                    case ItemQuality.Inferior: up = ItemQuality.Common; break;
                    case ItemQuality.Common: up = ItemQuality.Fine; break;
                    case ItemQuality.Fine: up = ItemQuality.Masterwork; break;
                    case ItemQuality.Masterwork: up = ItemQuality.Legendary; break;
                    default: return q;
                }
                _dQualUp++;
                return up;
            }
            catch (Exception e) { Stumble("Craftsman", e); return q; }
        }

        // ------------------------------------------------------------ 42: ArtisanSmith - ksiega wykutych sztuk i cena
        private static bool Ledgerable(ItemObject it)
        {
            return it != null && !it.IsCraftedWeapon && !it.IsTradeGood && !it.IsAnimal && !it.HasHorseComponent
                   && (it.HasArmorComponent || it.HasWeaponComponent);
        }

        /// <summary>Sztuki wykute przez gracza (dostawa z lawy, wlasny projekt, kucie bez lawy) - do ksiegi.</summary>
        internal static void Made(ItemObject it, int count)
        {
            try
            {
                if (!Ledgerable(it) || count <= 0) return;
                int c; _made.TryGetValue(it.StringId, out c);
                _made[it.StringId] = c + count;
                _dMade += count;
            }
            catch (Exception e) { Stumble("Made", e); }
        }

        /// <summary>Sztuk w sakwach gracza - SUMA wszystkich stosow (GetItemNumber gry liczy tylko pierwszy stos, a nasze wyroby maja rozne jakosci;
        /// jak ArmourQuality.TotalOf).</summary>
        private static int BagCount(ItemObject it)
        {
            var mp = MobileParty.MainParty;
            var r = mp != null ? mp.ItemRoster : null;
            if (it == null || r == null) return 0;
            int n = 0;
            for (int i = 0; i < r.Count; i++) { var el = r.GetElementCopyAtIndex(i); if (el.EquipmentElement.Item == it) n += el.Amount; }
            return n;
        }

        /// <summary>Wyposazenie bohaterow partii gracza (bojowe i cywilne) - zalozona wlasna sztuka nie wypada z ksiegi. it == null: wszystko do slownika.</summary>
        private static int WornCount(ItemObject it, Dictionary<ItemObject, int> all)
        {
            int n = 0;
            var mp = MobileParty.MainParty;
            if (mp == null || mp.MemberRoster == null) return 0;
            foreach (var tr in mp.MemberRoster.GetTroopRoster())
            {
                var h = tr.Character != null ? tr.Character.HeroObject : null;
                if (h == null) continue;
                var sets = new[] { h.BattleEquipment, h.CivilianEquipment };
                for (int s = 0; s < sets.Length; s++)
                {
                    var eq = sets[s];
                    if (eq == null) continue;
                    for (int i = 0; i < (int)EquipmentIndex.NumEquipmentSetSlots; i++)
                    {
                        var el = eq[i];
                        if (el.IsEmpty || el.Item == null) continue;
                        if (all != null) { int c; all.TryGetValue(el.Item, out c); all[el.Item] = c + 1; }
                        else if (el.Item == it) n++;
                    }
                }
            }
            return n;
        }

        /// <summary>Recenzja 511: perk tylko, gdy CALY zapas tego wzoru w sakwach to wlasna robota. Wlasnych w sakwach = ksiega - zalozone (ostroznie: zalozona
        /// sztuka liczy sie jako wlasna); sakwy maja wiecej (mieszany stos z lupami) - bez perka dla calego wzoru (wycena dostaje tylko ItemObject).</summary>
        internal static bool AllOursInBag(ItemObject it)
        {
            int c;
            if (it == null || !_made.TryGetValue(it.StringId, out c) || c <= 0) return false;
            int have = BagCount(it);
            if (have <= 0) return false;
            return have <= Math.Max(0, c - WornCount(it, null));
        }

        /// <summary>Postfiks (Priority.First) DefaultTradeItemPriceFactorModel.GetTradePenalty: perk ArtisanSmith dla sprzedazy wlasnej roboty gracza.</summary>
        public static void PenaltyPostfix(ItemObject __0, MobileParty __1, PartyBase __2, bool __3, ref float __result)
        {
            try
            {
                if (!__3 || __1 == null || !__1.IsMainParty || __result <= 0f) return;
                var s = S;
                if (s == null || !s.PerkArtisanSmithOwnWork || !Ledgerable(__0)) return;
                var perk = DefaultPerks.Crafting.ArtisanSmith;
                if (perk == null || !__1.HasPerk(perk)) return;
                if (!AllOursInBag(__0)) return;   // recenzja 511: mieszany stos (wlasne + lupy) - bez perka
                // czlony gry przed mnoznikami (DefaultTradeItemPriceFactorModel.GetTradePenalty 1.4.8) - sprzedaz, nie karawana, nie kon
                float t = 1.5f + Math.Max(0f, __0.Tierf - 1f) * 0.25f;
                float num = 0.06f + t;
                if (__2 != null && __1.MapFaction != null && __2.MapFaction != null && __1.MapFaction.IsAtWarWith(__2.MapFaction)) num += 0.5f;
                var st = __2 != null ? __2.Settlement : null;
                if (st != null && st.IsVillage) num += 1f;
                if (__2 != null && __2.MobileParty != null && __2.MobileParty.IsCaravan) num += 1f;
                float cut = t * perk.PrimaryBonus;   // -0.5 x T (AddFactor gry)
                float r = (num + cut) / num;
                if (r <= 0f || r >= 1f || float.IsNaN(r)) return;
                __result *= r;
                _dPriced++;
            }
            catch (Exception e) { Stumble("PenaltyPostfix", e); }
        }

        /// <summary>PlayerInventoryExchangeEvent: SPRZEDANE (isTrading) sztuki z ksiegi schodza. Odlozenie do schowka / towarzysza bez handlu ksiegi nie rusza
        /// (przyciecie doby i tak zdejmie, czego nie ma w sakwach ani na bohaterach).</summary>
        internal static void OnPlayerExchange(List<(ItemRosterElement, int)> purchased, List<(ItemRosterElement, int)> sold, bool isTrading)
        {
            try
            {
                if (!isTrading || sold == null || _made.Count == 0) return;
                foreach (var x in sold)
                {
                    var it = x.Item1.EquipmentElement.Item;
                    int c;
                    if (it == null || !_made.TryGetValue(it.StringId, out c) || c <= 0) continue;
                    int n = Math.Min(c, Math.Max(0, x.Item1.Amount));
                    if (n <= 0) continue;
                    _dSoldOurs += n;
                    if (c - n > 0) _made[it.StringId] = c - n; else _made.Remove(it.StringId);
                }
            }
            catch (Exception e) { Stumble("OnPlayerExchange", e); }
        }

        internal static string Export()
        {
            var sb = new StringBuilder();
            foreach (var kv in _made)
            {
                if (kv.Value <= 0) continue;
                if (sb.Length > 0) sb.Append(';');
                sb.Append(kv.Key).Append(':').Append(kv.Value.ToString(Inv));
            }
            return sb.ToString();
        }

        internal static void Import(string data)
        {
            _made.Clear();
            try
            {
                if (string.IsNullOrEmpty(data)) return;
                foreach (var part in data.Split(';'))
                {
                    int i = part.LastIndexOf(':');
                    int n;
                    if (i > 0 && int.TryParse(part.Substring(i + 1), NumberStyles.Integer, Inv, out n) && n > 0) _made[part.Substring(0, i)] = n;
                }
            }
            catch (Exception e) { Stumble("Import", e); }
        }

        internal static void OnNewGame() { _made.Clear(); }

        // ------------------------------------------------------------ wpiecie i linie logu
        internal static void ApplyAll(Harmony h)
        {
            string price = "BRAK";
            try
            {
                var m = AccessTools.DeclaredMethod(typeof(DefaultTradeItemPriceFactorModel), "GetTradePenalty");
                if (m != null)
                {
                    h.Patch(m, postfix: new HarmonyMethod(typeof(Perk511), nameof(PenaltyPostfix)) { priority = Priority.First });
                    price = "wpiete";
                }
            }
            catch (Exception e) { Stumble("ApplyAll", e); price = "BLAD"; }
            var s = S;
            Log.Info("Perki kuzni (511): stamina naszej kuzni z PracticalSmith i BK Smith " + (s != null && s.PerkSmithStamina ? "TAK" : "NIE")
                     + ", stawka kuzni BK Smith " + (s != null && s.PerkSmithHireDiscount ? "TAK" : "NIE")
                     + ", IronMaker przy lupie +" + (s != null ? (s.IronMakerBloomBonus * 100f).ToString("0", Inv) : "-") + "%"
                     + ", ArtisanSmith dla wlasnej roboty " + (s != null && s.PerkArtisanSmithOwnWork ? "TAK" : "NIE") + " (latka kary handlu " + price + ")"
                     + ", BK Craftsman w jakosci " + (s != null && s.PerkCraftsmanQuality ? "TAK" : "NIE") + ".");
        }

        /// <summary>Linia doby (ArmouryBehavior.OnDailyTickBodyInner) i przyciecie ksiegi do sakw.</summary>
        internal static void Daily()
        {
            try
            {
                var mp = MobileParty.MainParty;
                if (mp != null && mp.ItemRoster != null && _made.Count > 0)
                {
                    // recenzja 511: sakwy (suma wszystkich stosow - GetItemNumber liczy tylko pierwszy) + wyposazenie bohaterow partii, jeden przebieg
                    var held = new Dictionary<ItemObject, int>();
                    var r = mp.ItemRoster;
                    for (int i = 0; i < r.Count; i++)
                    {
                        var el = r.GetElementCopyAtIndex(i);
                        var ei = el.EquipmentElement.Item;
                        if (ei == null || el.Amount <= 0) continue;
                        int c; held.TryGetValue(ei, out c); held[ei] = c + el.Amount;
                    }
                    WornCount(null, held);
                    var keys = new List<string>(_made.Keys);
                    foreach (var k in keys)
                    {
                        var it = TaleWorlds.ObjectSystem.MBObjectManager.Instance.GetObject<ItemObject>(k);
                        int have = 0;
                        if (it != null) held.TryGetValue(it, out have);
                        if (have <= 0) _made.Remove(k);
                        else if (_made[k] > have) _made[k] = have;
                    }
                }
                var hero = Hero.MainHero;
                bool any = _dPieces + _dQualRolls + _dPriced + _dSoldOurs + _dMade + _dErrors > 0;
                if (!any || hero == null) return;
                int ours = 0; foreach (var v in _made.Values) ours += v;
                Log.Info("Perki kuzni (511): doba | stamina x" + StaminaFactor(hero).ToString("0.00", Inv) + " (PracticalSmith " + (Has(hero, DefaultPerks.Crafting.PracticalSmith) ? "tak" : "nie")
                         + ", BK Smith " + (ResolveBk() && BkHas(hero, _bkSmith) ? "tak" : "nie") + "), sztuk przy kowadle " + _dPieces + ", stamina przepisu " + _dStamRaw + " -> pobrana " + _dStamNow
                         + " | stawka kuzni x" + HireFactor().ToString("0.00", Inv)
                         + " | IronMaker " + (Has(hero, DefaultPerks.Crafting.IronMaker) ? "tak" : "nie") + " (lupa +" + (S.IronMakerBloomBonus * 100f).ToString("0", Inv) + "%)"
                         + " | ksiega wlasnej roboty: dopisane " + _dMade + ", sprzedane " + _dSoldOurs + ", w sakwach i na bohaterach " + ours + " (" + _made.Count + " wzorow), wyceny z ArtisanSmith " + _dPriced
                         + " | BK Craftsman: losowan " + _dQualRolls + ", w gore " + _dQualUp
                         + " | potkniecia " + _dErrors + ".");
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally { _dPieces = _dStamRaw = _dStamNow = _dQualRolls = _dQualUp = _dPriced = _dSoldOurs = _dMade = _dErrors = 0; }
        }
    }
}
