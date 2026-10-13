using System;
using System.Diagnostics;
using System.Globalization;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// P3/Z4 (TRIAZ-3 bieg roczny 13.10, O2-7 z podwazeniem) - TOWAR, KTORY GRA KASUJE LORDOWI PONAD UDZWIG: LICZNIK W KSIEDZE TOWAROW I POLKA MIASTA.
    /// Bieg roczny: ksiega "Towary: iron" ma 54 doby z ujemnym "bez wyjasnienia", razem -1 474 lupy (1,85% podazy; nigdy na plus); 86% ubytku siedzi u
    /// lordow. Droga (kod gry): DiscardItemsCampaignBehavior co godzine kasuje partii lorda AI z wodzem nadbagaz (DiscardNecessaryAmountOfItems - od konca
    /// taboru, bez 20 dob jedzenia) i stado ponad ludzi (DiscardAnimal); gra rozdaje lup pokonanego bez sprawdzania udzwigu (MapEvent, DefaultBattleRewardModel),
    /// wiec lord, ktory wzial zelazo z wozu, traci je w najblizszej godzinie - sztuki znikaja bez odbiorcy. Kasowanie jest godzinowe, a ramki ksiegi towarow
    /// obejmuja tylko dyspozytory dobowe - dlatego ladowalo w "bez wyjasnienia".
    /// Postfiks na obu metodach gry liczy dokladnie to, co zeszlo (stan stosu sprzed wywolania minus stan po):
    ///  - TOWAR KSIEGI TOWAROW (towar handlowy, takze zywnosc, i zywy inwentarz): przy kluczu LordDiscardToShelf na polke najblizszego przyjaznego MIASTA
    ///    (HorseFate.NearestTown - ta sama kolejnosc co dobytek ponad udzwig zwyciezcy R14: wlasna frakcja nieoblegana, niewroga, dowolna), bez zaplaty
    ///    (porzucony dobytek); w ksiedze towarow przewoz "lordowie porzucili na polki miast". Klucz wylaczony albo brak miasta - sztuki znikaja jak w grze,
    ///    a ksiega dostaje ujscie z nazwa "porzucone przez lordow (gra)". W obu przypadkach ta droga schodzi z "bez wyjasnienia".
    ///  - KONIE (wierzchowce i juczne): bez zmian - liczy je ksiega koni (HorseState / HorseBook, prefiksy K0); przeniesienie koni na targ zmienia bilans
    ///    hodowli (ok. 34 konie/d) - do decyzji z obszarem koni.
    ///  - UZBROJENIE i inne nie-towary: bez zmian, tylko licznik (ksiega uzbrojenia ma wlasne pozycje).
    /// Lord traci to samo co dotad (tempo marszu i AI bez zmian); gracz nietkniety (gra kasuje tylko lordom AI). Wstawka w linii "Konie - smierc, lup,
    /// dobytek (E6/K1)". Koszt: postfiks biegnie tylko przy faktycznym kasowaniu (kilkadziesiat-kilkaset razy na dobe), kazde to jedno przejscie po miastach.
    /// </summary>
    internal static class LordDiscard
    {
        private static bool _hooked;
        private static int _dCalls, _stumbles;
        private static long _dGoods, _dFood, _dLive, _dHorse, _dGear, _dIron, _dToShelf, _dIronShelf, _dNoShelf, _dKeyOff, _ticks;
        private static bool _errLogged;

        internal static bool KeyOn { get { var s = Settings.Current; return s != null && s.LordDiscardToShelf; } }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (!_errLogged) { _errLogged = true; Log.Error("LordDiscard." + where, e); }
        }

        public static void WeightPostfix(MobileParty mobileParty, ItemRosterElement itemRosterElement) { Handle(mobileParty, itemRosterElement); }

        public static void AnimalPostfix(MobileParty mobileParty, ItemRosterElement itemRosterElement) { Handle(mobileParty, itemRosterElement); }

        private static void Handle(MobileParty mp, ItemRosterElement before)
        {
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                var it = before.EquipmentElement.Item;
                if (mp == null || it == null || mp.ItemRoster == null || before.Amount <= 0 || mp.IsMainParty) return;
                int ix = mp.ItemRoster.FindIndexOfElement(before.EquipmentElement);
                int now = ix >= 0 ? mp.ItemRoster.GetElementNumber(ix) : 0;
                int gone = before.Amount - now;   // dokladnie to, co gra zdjela w tym wywolaniu
                if (gone <= 0) return;
                _dCalls++;
                if (it.ItemType == ItemObject.ItemTypeEnum.Horse) { _dHorse += gone; return; }   // konie: ksiega koni (K0) - bez zmian
                bool live = it.IsAnimal && it.HorseComponent != null && it.HorseComponent.IsLiveStock;
                if (!it.IsTradeGood && !live) { _dGear += gone; return; }                        // uzbrojenie i inne nie-towary: bez zmian
                if (live) _dLive += gone; else if (TownFolk.IsFoodCategory(it.ItemCategory)) _dFood += gone; else _dGoods += gone;
                bool iron = it.StringId == "iron";
                if (iron) _dIron += gone;
                Settlement m = null;
                bool key = KeyOn;
                if (key) m = HorseFate.NearestTown(mp.GetPosition2D, mp.MapFaction);
                if (m != null && m.ItemRoster != null)
                {
                    m.ItemRoster.AddToCounts(before.EquipmentElement, gone);
                    _dToShelf += gone; if (iron) _dIronShelf += gone;
                    GoodsLedger.NoteDiscard(it, gone, true);
                }
                else
                {
                    if (key) _dNoShelf += gone; else _dKeyOff += gone;
                    GoodsLedger.NoteDiscard(it, gone, false);
                }
            }
            catch (Exception e) { Stumble("Handle", e); }
            finally { _ticks += Stopwatch.GetTimestamp() - t0; }
        }

        /// <summary>Wstawka linii doby "Konie - smierc, lup, dobytek (E6/K1)" (HorseFate.Daily); zeruje liczniki.</summary>
        internal static string Segment()
        {
            string r;
            try
            {
                r = " | porzucone przez lordow (gra, P3/Z4): latka " + (_hooked ? "wpieta" : "NIE wpieta") + ", na polke miasta " + (KeyOn ? "TAK" : "NIE (Lord Discard To Shelf)")
                    + " - kasowan " + _dCalls + ", towar " + _dGoods + " (w tym zelazo " + _dIron + "), zywnosc " + _dFood + ", zywy inwentarz " + _dLive
                    + ", konie " + _dHorse + " (ksiega koni - bez zmian), uzbrojenie i inne " + _dGear + " (bez zmian); na polki miast " + _dToShelf + " (zelazo " + _dIronShelf
                    + "), bez polki " + _dNoShelf + ", przepadlo przy wylaczonym kluczu " + _dKeyOff
                    + "; czas " + (_ticks * 1000.0 / Stopwatch.Frequency).ToString("0.0", CultureInfo.InvariantCulture) + " ms, potkniecia " + _stumbles;
            }
            catch (Exception e) { Stumble("Segment", e); r = ""; }
            _dCalls = 0; _stumbles = 0;
            _dGoods = _dFood = _dLive = _dHorse = _dGear = _dIron = _dToShelf = _dIronShelf = _dNoShelf = _dKeyOff = _ticks = 0;
            return r;
        }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var tD = typeof(TaleWorlds.CampaignSystem.CampaignBehaviors.DiscardItemsCampaignBehavior);
                var mA = AccessTools.Method(tD, "DiscardAnimal");
                var mW = AccessTools.Method(tD, "DiscardNecessaryAmountOfItems");
                if (mA != null) h.Patch(mA, postfix: new HarmonyMethod(typeof(LordDiscard), nameof(AnimalPostfix)));
                if (mW != null) h.Patch(mW, postfix: new HarmonyMethod(typeof(LordDiscard), nameof(WeightPostfix)));
                _hooked = mA != null && mW != null;
            }
            catch (Exception e) { _hooked = false; Log.Error("LordDiscard.ApplyAll", e); }
            Log.Info("LordDiscard (P3/Z4): towar kasowany lordom przez gre (DiscardItemsCampaignBehavior - ponad udzwig, stado ponad ludzi) - postfiksy "
                     + (_hooked ? "wpiete" : "NIE wpiete") + "; towar ksiegi towarow na polke najblizszego miasta (klucz Lord Discard To Shelf, czytany w kampanii), konie i uzbrojenie bez zmian.");
        }
    }
}
