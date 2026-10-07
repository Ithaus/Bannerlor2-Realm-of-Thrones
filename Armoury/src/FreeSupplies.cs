using System;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// KONIEC DOSYPKI Z NICZEGO (paczka 125; zamknieta ekonomia - "nic z niczego"; fundament 05.10 B1, STAN-PRAC OTWARTE (2)).
    /// RealisticBannerlord.Systems.Supplies.SettlementSupplyAvailabilityBehavior (dekompilacja RBL 1.2.0, OnDailyTickSettlement
    /// :14-31) slucha CampaignEvents.DailyTickSettlementEvent i co dobe KAZDEJ osadzie - gra tyka wszystkie, takze kryjowki
    /// (CampaignPeriodicEventManager.ShuffleSettlements) - dopisuje AddToCounts do progu: narzedzia 15 / 8 / 4, drewno 30 / 15 / 10
    /// (miasto / zamek / reszta). Bez platnika, bez zdarzenia OnItemProduced, bez wylacznika w MCM RBL, rejestracja bezwarunkowa.
    /// Test 07.10 (19 dob): "Drewno: bez wyjasnienia" +1156..+3462 ladunkow na dobe (mediana dob 6-19 ok. 1430) przy wydobyciu
    /// drwali 228-273 - ponad 5 razy wiecej drewna z niczego niz z lasu; narzedzi ksiega nie liczy (szacunek w opisie paczki).
    /// Latka: prefiks na tej jednej metodzie. Liczy to samo, co RBL (prog - stan polki, osobno narzedzia i drewno), i przy
    /// wlaczonym NoFreeTimberAndTools zwraca false - RBL nie dopisuje niczego; przy wylaczonym przepuszcza RBL, a liczniki mowia,
    /// ile dosypal. Linia dnia w obu trybach (pomiar). Reszty RBL nie ruszamy: zuzycie narzedzi w oblezonej osadzie
    /// (SiegeExpansionBehavior, 3 na dobe), premia obrony przy ponad 10 narzedziach w osadzie (RealisticSiegeEventModel),
    /// zapasy obozowe gracza (AdvancedSuppliesBehavior - i tak wyciete na stale przez CrashScribe Quiet.SkipSupplies).
    /// Spojnosc: CaravanBulk liczyl 30 ladunkow drewna kazdego miasta jako "osada dostaje sama" (Good.Free) - przy blokadzie
    /// Free = 0 (CaravanBulk.FreeOf), inaczej karawany nie dowozilyby dolnych 30 ladunkow brakujacego drewna.
    /// Stan: tylko liczniki doby (bez zapisu); wyjatek per osada - licznik potkniec w linii dnia, wlacznik dziala dalej.
    /// </summary>
    internal static class FreeSupplies
    {
        private const string RblType = "RealisticBannerlord.Systems.Supplies.SettlementSupplyAvailabilityBehavior";
        private static bool _wired;
        private static int _stumbles, _stumblesDay;
        // [tryb, osada]: tryb 0 = zablokowane, 1 = przepuszczone (RBL dosypal); osada 0 miasta, 1 zamki, 2 wsie, 3 kryjowki i inne
        private static readonly int[,] _wood = new int[2, 4], _tools = new int[2, 4], _places = new int[2, 4];

        internal static bool On { get { var s = Settings.Current; return s != null && s.NoFreeTimberAndTools; } }

        /// <summary>Dosypka RBL naprawde zablokowana: latka wpieta i wlacznik wlaczony (CaravanBulk.FreeOf).</summary>
        internal static bool Blocking { get { return _wired && On; } }

        /// <summary>Nowa gra albo wczytanie (konstruktor ArmouryBehavior): liczniki doby i potkniec od zera.</summary>
        internal static void Reset()
        {
            Array.Clear(_wood, 0, _wood.Length); Array.Clear(_tools, 0, _tools.Length); Array.Clear(_places, 0, _places.Length);
            _stumbles = 0; _stumblesDay = 0;
        }

        /// <summary>Prefiks RBL OnDailyTickSettlement(Settlement): te same progi i ten sam odczyt polki co RBL (:18-29).
        /// false = RBL nie dosypuje (wlacznik wlaczony), true = RBL dziala jak dotad.</summary>
        public static bool Prefix(Settlement __0)
        {
            bool block = On;
            try
            {
                var st = __0;
                if (st == null || st.Party == null || st.ItemRoster == null) return !block;   // RBL sam osade bez polki pomija
                int k = st.IsTown ? 0 : (st.IsCastle ? 1 : (st.IsVillage ? 2 : 3));
                int toolsFloor = st.IsTown ? 15 : (st.IsCastle ? 8 : 4);
                int woodFloor = st.IsTown ? 30 : (st.IsCastle ? 15 : 10);
                var r = st.ItemRoster;
                int t = Math.Max(0, toolsFloor - r.GetItemNumber(DefaultItems.Tools));
                int w = Math.Max(0, woodFloor - r.GetItemNumber(DefaultItems.HardWood));
                if (t > 0 || w > 0)
                {
                    int b = block ? 0 : 1;
                    _tools[b, k] += t; _wood[b, k] += w; _places[b, k]++;
                }
            }
            catch (Exception e)
            {
                _stumbles++; _stumblesDay++;
                if (_stumbles <= 3) Log.Error("FreeSupplies.Prefix", e);
            }
            return !block;
        }

        private static string Part(int b)
        {
            int w = 0, t = 0, n = 0;
            for (int k = 0; k < 4; k++) { w += _wood[b, k]; t += _tools[b, k]; n += _places[b, k]; }
            return "drewno " + w + " ladunkow [miasta " + _wood[b, 0] + ", zamki " + _wood[b, 1] + ", wsie " + _wood[b, 2] + ", kryjowki i inne " + _wood[b, 3]
                   + "], narzedzia " + t + " szt. [miasta " + _tools[b, 0] + ", zamki " + _tools[b, 1] + ", wsie " + _tools[b, 2] + ", kryjowki i inne " + _tools[b, 3]
                   + "] w " + n + " osadach ponizej progu";
        }

        /// <summary>Raz na dobe (ArmouryBehavior.OnDailyTick, po ksiedze drewna): ile dosypki zablokowano albo przepuszczono od
        /// poprzedniej linii i ile narzedzi lezy na targach (premia obrony RBL w oblezeniu wymaga ponad 10 w osadzie). Tylko log.</summary>
        internal static void Daily()
        {
            if (!_wired) return;
            try
            {
                int towns = 0, tTools = 0, tEmpty = 0, tOver = 0, castles = 0, cTools = 0, cOver = 0;
                var tools = DefaultItems.Tools;
                foreach (var st in Settlement.All)
                {
                    if (st == null || st.Party == null || st.ItemRoster == null || tools == null) continue;
                    if (st.IsTown)
                    {
                        int n = st.ItemRoster.GetItemNumber(tools);
                        towns++; tTools += n; if (n <= 0) tEmpty++; if (n > 10) tOver++;
                    }
                    else if (st.IsCastle)
                    {
                        int n = st.ItemRoster.GetItemNumber(tools);
                        castles++; cTools += n; if (n > 10) cOver++;
                    }
                }
                var sb = new StringBuilder();
                sb.Append("Dosypka z niczego (RealisticBannerlord): dzien ").Append((int)CampaignTime.Now.ToDays - 1).Append(" - ")
                  .Append(On ? "ZABLOKOWANA (125)" : "CZYNNA - wylacznik No Free Timber And Tools wylaczony, RBL dosypuje (tu tylko pomiar)")
                  .Append("; progi RBL: miasto 30 drewna / 15 narzedzi, zamek 15 / 8, wies i kryjowka 10 / 4")
                  .Append("; zablokowano (tyle RBL dopisalby dzis do swojego progu - brak pod progiem, przy blokadzie co dobe od nowa): ").Append(Part(0))
                  .Append("; RBL dosypal z niczego: ").Append(Part(1))
                  .Append("; narzedzia na targach: miasta ").Append(tTools).Append(" (bez narzedzi ").Append(tEmpty).Append(" z ").Append(towns)
                  .Append(", ponad 10 - premia obrony RBL w oblezeniu - ").Append(tOver).Append("), zamki ").Append(cTools)
                  .Append(" (ponad 10: ").Append(cOver).Append(" z ").Append(castles).Append(")")
                  .Append("; potkniecia dzis ").Append(_stumblesDay).Append(" (od wczytania ").Append(_stumbles).Append(").");
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Log.Error("FreeSupplies.Daily", e); }
            finally
            {
                Array.Clear(_wood, 0, _wood.Length); Array.Clear(_tools, 0, _tools.Length); Array.Clear(_places, 0, _places.Length);
                _stumblesDay = 0;
            }
        }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var t = QuartermasterLaw.FindType(RblType);
                if (t == null) { Log.Info("FreeSupplies: RealisticBannerlord nieobecny (brak " + RblType + ") - nie ma dosypki do blokowania."); return; }
                var m = AccessTools.Method(t, "OnDailyTickSettlement", new[] { typeof(Settlement) });
                if (m == null || m.IsStatic || m.ReturnType != typeof(void))
                {
                    Log.Info("FreeSupplies: BRAK metody " + t.Name + ".OnDailyTickSettlement(Settlement) w tej wersji RealisticBannerlord - dosypka RBL BEZ ZMIAN (sprawdzic dekompilacje).");
                    return;
                }
                h.Patch(m, prefix: new HarmonyMethod(typeof(FreeSupplies), nameof(Prefix)));
                _wired = true;
                Log.Info("FreeSupplies: latka na RealisticBannerlord " + t.Name + ".OnDailyTickSettlement wpieta (RBL " + t.Assembly.GetName().Version
                         + ") - dosypka drewna i narzedzi z niczego blokowana wedle wlacznika No Free Timber And Tools (domyslnie wlaczony; tryb i liczby - linia dnia"
                         + " \"Dosypka z niczego\"); reszta RBL bez zmian; karawany licza drewno miasta bez 30 darmowych ladunkow, gdy blokada dziala.");
            }
            catch (Exception e) { Log.Error("FreeSupplies.ApplyAll", e); }
        }
    }
}
