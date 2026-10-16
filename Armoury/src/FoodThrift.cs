using System;
using System.Collections.Generic;
using System.Globalization;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// SYTY LORD AI NIE PRZEPLACA ZA JEDZENIE (decyzja Jeffa 15.10 TAK - "lordowie nie przeplacaja"; DECYZJA CLAUDE noc 15/16.10: prog 2.5 x wartosc - AiFoodDearIndex).
    /// Dowod (log 2026-10-15_10-56-30, 12:10:56): "Towary: dzien 108945 - pie (10 d): ... przewoz [... lordowie kupili 235 ...]" przy indeksie placka do 10.00 i 41
    /// pustych miastach. Przyczyna [K]: DefaultPartyFoodBuyingModel.FindItemToBuy (i BK BKPartyBuyingFoodModel) losuje jedzenie z waga ((120 - cena) x 0.0083)^2 x
    /// ((100 - wartosc) x 0.01)^2 z odcieciem 120 - w pensach (ceny 2-60 d) wagi sa prawie rowne, wiec placek po 5-10 x wartosci idzie jak zboze [P - wagi].
    /// Regula: drugi postfiks na KAZDEJ zadeklarowanej FindItemToBuy (ta sama petla co HungerLaw; parametry po indeksach __0..__3): model wybral jedzenie drozsze niz
    /// AiFoodDearIndex x jego wartosc, partia AI nie gloduje - szukamy na tej samej polce jedzenia po cenie <= AiFoodDearIndex x wartosc, < 120 (sufit modelu) i <= zloto
    /// partii (PartyTradeGold - ten sam warunek co petla zakupu gry); losowanie z waga (wartosc / cena)^2 (zostaje rozmaitosc jedzenia); brak tanszego - wybor modelu
    /// zostaje. Glodny kupuje dalej po kazdej cenie (HungerLaw dziala tylko, gdy model NIC nie wybral - warunki rozlaczne). Zwierzeta na mieso (IsLiveStock) - bez zmian.
    /// Zloto i towar: ta sama transakcja gry (SellItemsAction), inna sztuka. Wyjatek - potkniecie (licznik, pierwsze do Log.Error), wybor modelu zostaje.
    /// </summary>
    internal static class FoodThrift
    {
        private static readonly object Gate = new object();
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static int _dear, _dearLord, _swapped, _swappedLord, _kept, _stumbles, _stDay, _hooks;
        private static bool _errLogged;
        private static readonly List<int> _idx = new List<int>();
        private static readonly List<int> _price = new List<int>();
        private static readonly List<double> _w = new List<double>();

        public static void Postfix(MobileParty __0, Settlement __1, ref ItemRosterElement __2, ref float __3)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.AiFoodSkipsDear) return;
                var it = __2.EquipmentElement.Item;
                if (it == null || !it.IsFood) return;                        // nic albo zwierze na mieso - nie ruszamy
                var party = __0; var st = __1;
                if (party == null || st == null || party.IsMainParty || party.Party == null || party.Party.IsStarving) return;
                float k = Math.Max(1f, s.AiFoodDearIndex);
                int value = __2.EquipmentElement.ItemValue;
                if (value <= 0 || !(__3 > k * value)) return;
                var comp = st.SettlementComponent; var roster = st.ItemRoster;
                if (comp == null || roster == null) return;
                bool lord = party.LeaderHero != null && party.IsLordParty;
                lock (Gate) { _dear++; if (lord) _dearLord++; }
                int gold = party.PartyTradeGold;
                _idx.Clear(); _price.Clear(); _w.Clear();
                double sum = 0;
                for (int i = 0; i < roster.Count; i++)
                {
                    var el = roster.GetElementCopyAtIndex(i);
                    var x = el.EquipmentElement.Item;
                    if (x == null || el.Amount <= 0 || !x.IsFood) continue;
                    int v = el.EquipmentElement.ItemValue;
                    if (v <= 0) continue;
                    int p = comp.GetItemPrice(el.EquipmentElement, party);
                    if (p <= 0 || p >= 120 || p > gold || p > k * v) continue;
                    double w = (double)v / p; w *= w;
                    _idx.Add(i); _price.Add(p); _w.Add(w); sum += w;
                }
                if (_idx.Count == 0 || !(sum > 0)) { lock (Gate) _kept++; return; }
                double r = MBRandom.RandomFloat * sum;
                int pick = _idx.Count - 1;
                for (int j = 0; j < _w.Count; j++) { r -= _w[j]; if (r <= 0) { pick = j; break; } }
                __2 = roster.GetElementCopyAtIndex(_idx[pick]);
                __3 = _price[pick];
                lock (Gate) { _swapped++; if (lord) _swappedLord++; }
            }
            catch (Exception e)
            {
                lock (Gate) { _stumbles++; _stDay++; }
                if (!_errLogged) { _errLogged = true; Log.Error("FoodThrift", e); }
            }
        }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var post = new HarmonyMethod(typeof(FoodThrift), nameof(Postfix));
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type[] types;
                    try { types = asm.GetTypes(); }
                    catch (System.Reflection.ReflectionTypeLoadException r) { types = r.Types; }
                    catch { continue; }
                    foreach (var t in types)
                    {
                        if (t == null || t.IsAbstract || !typeof(PartyFoodBuyingModel).IsAssignableFrom(t)) continue;
                        var m = t.GetMethod("FindItemToBuy", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic
                                                             | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly);
                        if (m == null || m.IsAbstract) continue;
                        // kazda latka osobno - egzotyczna sygnatura w cudzym modelu nie moze polozyc pozostalych (jak HungerLaw)
                        try { h.Patch(m, postfix: post); _hooks++; }
                        catch (Exception pe) { Log.Error("FoodThrift.Patch(" + t.Name + ")", pe); }
                    }
                }
                Log.Info("Lordowie a drogie jedzenie (B3): syty nie przeplaca - " + _hooks + " modeli zakupu jedzenia zalatanych (klucz AiFoodSkipsDear czytany przy kazdym wywolaniu).");
            }
            catch (Exception e) { Log.Error("FoodThrift.ApplyAll", e); }
        }

        /// <summary>Z ArmouryBehavior.OnDailyTick: linia "Lordowie a drogie jedzenie (B3)" (tylko log).</summary>
        internal static void Daily()
        {
            var s = Settings.Current;
            if (s == null) return;
            int d, dl, sw, swl, kp, sd, ss;
            lock (Gate) { d = _dear; dl = _dearLord; sw = _swapped; swl = _swappedLord; kp = _kept; sd = _stDay; ss = _stumbles; _dear = _dearLord = _swapped = _swappedLord = _kept = _stDay = 0; }
            Log.Info("Lordowie a drogie jedzenie (B3): dzien " + (int)CampaignTime.Now.ToDays + " | " + (s.AiFoodSkipsDear ? "CZYNNE" : "WYLACZONE (MCM)") + " (prog x" + Math.Max(1f, s.AiFoodDearIndex).ToString("0.0#", Inv)
                     + " wartosci, modeli zalatanych " + _hooks + ") | drogich wyborow modelu (syte partie AI) " + d + " (partie lordow " + dl + "), zamienionych na tansze " + sw + " (lordow " + swl + ")"
                     + ", zostawionych (brak tanszego na polce) " + kp + " | potkniecia " + sd + " (sesja " + ss + ").");
        }
    }
}
