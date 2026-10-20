using System;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// R3-09f (regale gornicze, MineRegale.cs): odczyt stanu dymarki kopalni dla regale - bez zmian stanu i bez licznikow linii "Zelazo (lupy)".
    /// </summary>
    internal static partial class Bloomery
    {
        /// <summary>Otwarty rachunek za drewno kopalni PRZED rozliczeniem F4b w oknie powrotu wozu (ta sama wartosc, ktora BillOf da CoalFirst; bez licznikow
        /// powrotow). 0 - lupa nieczynna, klucz F4b wylaczony albo wies bez dymarki. Nie rzuca.</summary>
        internal static int RegaleBill(Village v)
        {
            if (!_applied || v == null || !CoalFirst) return 0;
            try
            {
                if (_pending != null) ResolvePending();
                Mine m;
                return _mines.TryGetValue(v, out m) ? Math.Max(0, m.Bill) : 0;
            }
            catch (Exception e) { Stumble("RegaleBill", e); return 0; }
        }

        /// <summary>Zwolnienie z regale: true - kopalnia ma dlug u kupcow za drewno dymarki (f5-ruda). Inaczej `reserve` = zapas na drewno dymarki na `days` dob:
        /// potrzeba doby ponad las okregu x cena drewna w miescie handlowym (ta sama miara co "kiesy kopaln ponizej kosztu drewna doby" w linii zelaza);
        /// 0 - lupa nieczynna, wies bez dymarki albo las starcza. Nie rzuca (wyjatek - bez zwolnienia, zapas 0).</summary>
        internal static bool RegaleWoodGuard(Village v, int days, out long reserve)
        {
            reserve = 0;
            if (!_applied || v == null) return false;
            try
            {
                if (_f5pending != null) F5Resolve();
                F5Credit c;
                if (_f5cred.TryGetValue(v, out c) && c != null && c.Owed > 0) return true;
                if (_pending != null) ResolvePending();
                Mine m;
                if (!_mines.TryGetValue(v, out m)) return false;
                float need = m.Want - Math.Min(m.ForAvg, m.Want);
                if (!(need > 0f)) return false;
                int price = m.Price;
                if (price <= 0)
                {
                    var wood = Wood();
                    var mk = v.TradeBound; var town = mk != null ? mk.Town : null;
                    if (town != null && wood != null) price = Math.Max(0, town.MarketData.GetPrice(new EquipmentElement(wood)));
                }
                if (price > 0) reserve = (long)Math.Ceiling(Math.Max(0, days) * (double)need * price);
                return false;
            }
            catch (Exception e) { Stumble("RegaleWoodGuard", e); reserve = 0; return false; }
        }
    }
}
