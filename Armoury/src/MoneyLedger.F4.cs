using System;
using System.Diagnostics;
using System.Text;
using TaleWorlds.CampaignSystem.Party;
using CW = Armoury.CirculationWindows;

namespace Armoury
{
    /// <summary>
    /// f4-pomiary (noc 17/18.10, koncowy przeglad ekonomii, pakiet P6) - WYNIK HANDLOWY KARAWAN (klucz Measure Caravan Trade). Tylko log, czytane z licznikow ksiegi
    /// pieniadza tej samej doby (przed ClearDay) - niczego nie zmienia. Pytanie przegladu: notable doplacaja karawanom do 5000 zl ok. 90 tys./d, karawany oddaja im
    /// zysk ok. 41 tys./d - czy strata to marza handlu po cenach KS, czy zold? Linia co 7 dob (sumy okna i srednia na dobe):
    ///  - sprzedaz karawan kasom osad i zakup z kas osad: przelewy gry osada &lt;-&gt; karawana (GiveGoldAction w SellItemsAction - jedyna droga zaplaty karawany za towar;
    ///    poza naszym tickiem dobowym - przelewy w bloku ticku ida do migawek, nie do stron) wedlug klasy osady;
    ///  - zold nalezny karawan notabli, niedoplata (okno zoldu notabla, CirculationWindows O02), zold karawan lordow naliczony (z kies rodow);
    ///  - towar w jukach wszystkich karawan w wartosci gry (Item.Value x sztuki; zmiana okna = towar kupiony, a jeszcze nie sprzedany);
    ///  - doplaty notabli do 5000 i zysk do wlascicieli (E3-T2), zmiana kies karawan (posiadacz "karawany" ksiegi);
    ///  - tozsamosc: zmiana kies = sprzedaz - zakup + doplaty - zold z kas karawan - zysk do wlascicieli + reszta; reszta (nazwana, niezmierzona osobno): dochod BK
    ///    do wlascicieli, myto, nowe i rozbite karawany, lup band, zakupy jucznych.
    ///  - recenzja (noc 17/18.10): zold z kas karawan wydzielony z reszty - okno O02 liczy go (CW.UCaravanFromCaravan = karawany z kasa >= zold, ManageCaravanExpensesOfNotable
    ///    gry: karawana placi cala kwote albo nic, reszte notabl), a E3/4 (Seal31B) przenosi go do sakiewek ludzi i odejmuje z tej pozycji - suma obu = zold z kas karawan.
    /// </summary>
    internal static partial class MoneyLedger
    {
        private static bool F4CarOn { get { var s = Settings.Current; return s != null && s.MeasureCaravanTrade; } }
        private static int _f4Days, _f4FirstDay = -1, _f4St;
        private static readonly long[] _f4Sell = new long[Classes], _f4Buy = new long[Classes];
        private static long _f4Due, _f4Short, _f4Top, _f4Own, _f4Own259, _f4LordWage, _f4DPurse, _f4V0 = -1, _f4V1, _f4VPrev = -1, _f4Ms;
        private static long _f4CarWage;                     // recenzja: zold karawan notabli z kas karawan (O02 netto + E3/4 do sakiewek)
        private static int _f4N, _f4Empty;

        /// <summary>MoneyLedger.Daily (doba z liniami, przed ClearDay): dopisuje doba do okna 7 dob; po 7 dobach linia "Karawany - wynik handlowy (f4-pomiary)".</summary>
        private static void F4CaravanDay(int day, long[] now, long[] last)
        {
            if (!F4CarOn) { F4Reset(); return; }
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                // towar w jukach karawan (wartosc gry) - stan na koniec doby
                long v = 0; int n = 0, empty = 0;
                foreach (var mp in MobileParty.AllCaravanParties)
                {
                    if (mp == null || !mp.IsActive) continue;
                    n++;
                    try
                    {
                        var r = mp.ItemRoster; long pv = 0;
                        if (r != null)
                            for (int k = 0; k < r.Count; k++)
                            {
                                var el = r.GetElementCopyAtIndex(k); var it = el.EquipmentElement.Item;
                                if (it != null && el.Amount > 0) pv += (long)el.Amount * Math.Max(0, it.Value);
                            }
                        if (pv == 0) empty++;
                        v += pv;
                    }
                    catch (Exception e) { _f4St++; F4Stumble("F4CaravanDay(karawana)", e); }
                }
                // poczatek okna: towar na koniec doby poprzedniej (pierwsza doba sesji - na koniec tej doby; zmiana okna bez jej handlu)
                if (_f4Days == 0) { _f4FirstDay = day; _f4V0 = _f4VPrev >= 0 ? _f4VPrev : v; }
                for (int c = 0; c < Classes; c++) { _f4Sell[c] += _goldOut[c, PCaravan]; _f4Buy[c] += _goldIn[c, PCaravan]; }
                bool cw = CW.On;
                if (cw) { _f4Due += CW.Sum[CW.ICaravanDue]; _f4Short += CW.Sum[CW.ICaravanShort]; _f4Top += CW.Sum[CW.ICaravanTopup]; }
                if (cw && CW.Wired[CW.WCaravanWage]) _f4CarWage += CW.Sum[CW.UCaravanFromCaravan] + Seal31B.NotFromCarToday;   // te same warunki co odjecie w Seal31B.NotableCarPost
                _f4Own += CaravanGoods.LastOwnerPaid + CaravanSettle.LastPaid;   // 259: rozliczenie karawan rodow - tez z kies karawan do wlascicieli (przez saldo rodu)
                _f4Own259 += CaravanSettle.LastPaid;
                _f4LordWage += _wage[WCaravan];
                if (now != null && last != null && now.Length > HCaravans && last.Length > HCaravans) _f4DPurse += now[HCaravans] - last[HCaravans];
                _f4V1 = v; _f4VPrev = v; _f4N = n; _f4Empty = empty;
                _f4Days++;
                _f4Ms += Stopwatch.GetTimestamp() - t0;
                if (_f4Days < 7) return;
                Log.Info(F4CaravanLine(day, cw));
            }
            catch (Exception e) { _f4St++; F4Stumble("F4CaravanDay", e); }
            // okno od nowa (towar na koniec tego okna = poczatek nastepnego)
            if (_f4Days >= 7)
            {
                _f4Days = 0;
                Array.Clear(_f4Sell, 0, Classes); Array.Clear(_f4Buy, 0, Classes);
                _f4Due = _f4Short = _f4Top = _f4Own = _f4Own259 = _f4LordWage = _f4DPurse = 0; _f4Ms = 0; _f4St = 0; _f4CarWage = 0;
            }
        }

        private static string F4CaravanLine(int day, bool cw)
        {
            int d = Math.Max(1, _f4Days);
            long sell = 0, buy = 0; for (int c = 0; c < Classes; c++) { sell += _f4Sell[c]; buy += _f4Buy[c]; }
            long trade = sell - buy, paid = _f4Due - _f4Short, dv = _f4V1 - Math.Max(0, _f4V0);
            long rest = _f4DPurse - (trade + _f4Top - _f4CarWage - _f4Own);
            double ms = _f4Ms * 1000.0 / Stopwatch.Frequency;
            var sb = new StringBuilder(1400);
            sb.Append("Karawany - wynik handlowy (f4-pomiary): dzien ").Append(day).Append(" | okno ").Append(_f4Days).Append(" dob od dnia ").Append(_f4FirstDay).Append(" (liczby na dobe, w nawiasie suma okna)")
              .Append(" | karawan ").Append(_f4N).Append(" (z pustymi jukami ").Append(_f4Empty).Append("), towar w jukach (wartosc gry) ").Append(_f4V1).Append(" (zmiana okna ").Append(S(dv)).Append(')')
              .Append(" | sprzedaz kasom osad ").Append(sell / d).Append(" (").Append(sell).Append("; miasta ").Append(_f4Sell[CTown]).Append(", zamki ").Append(_f4Sell[CCastle]).Append(", wsie ").Append(_f4Sell[CVill]).Append(')')
              .Append(", zakup z kas osad ").Append(buy / d).Append(" (").Append(buy).Append("; miasta ").Append(_f4Buy[CTown]).Append(", zamki ").Append(_f4Buy[CCastle]).Append(", wsie ").Append(_f4Buy[CVill]).Append(')')
              .Append(" -> wynik handlu ").Append(S(trade / d)).Append(" (").Append(S(trade)).Append(')')
              .Append(" | zold karawan notabli nalezny ").Append(_f4Due / d).Append(" (").Append(_f4Due).Append("), niedoplata ").Append(_f4Short / d).Append(" (").Append(_f4Short).Append("), zaplacony ").Append(paid / d).Append(" (").Append(paid).Append(')')
              .Append(", w tym z kas karawan ").Append(_f4CarWage / d).Append(" (").Append(_f4CarWage).Append("), z kies notabli ").Append((paid - _f4CarWage) / d).Append(" (").Append(paid - _f4CarWage).Append(')')
              .Append(cw ? "" : " [okna obiegu wylaczone - zold i doplaty 0]")
              .Append(", zold karawan lordow naliczony (kiesy rodow) ").Append(_f4LordWage / d).Append(" (").Append(_f4LordWage).Append(')')
              .Append(" | wynik handlu - zold zaplacony ").Append(S((trade - paid) / d)).Append(" (").Append(S(trade - paid)).Append(')')
              .Append(", z towarem w jukach (handel + zmiana towaru - zold) ").Append(S((trade + dv - paid) / d)).Append(" (").Append(S(trade + dv - paid)).Append(')')
              .Append(" | doplaty notabli do 5000 ").Append(S(_f4Top / d)).Append(" (").Append(S(_f4Top)).Append("), zysk do wlascicieli (E3-T2) ").Append(S(-_f4Own / d)).Append(" (").Append(S(-_f4Own)).Append(')').Append(", w tym rozliczenie rodow (259) ").Append(S(-_f4Own259))
              .Append(" | kiesy karawan: zmiana ").Append(S(_f4DPurse / d)).Append(" (").Append(S(_f4DPurse)).Append(") = handel ").Append(S(trade)).Append(" + doplaty ").Append(S(_f4Top))
              .Append(" - zold z kas karawan ").Append(_f4CarWage).Append(" - zysk do wlascicieli ").Append(_f4Own)
              .Append(" + reszta ").Append(S(rest)).Append(" (dochod BK do wlascicieli, myto, nowe i rozbite karawany, lup band, zakupy jucznych)")
              .Append(" | czas ").Append(ms.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)).Append(" ms, potkniecia ").Append(_f4St).Append('.');
            return sb.ToString();
        }

        /// <summary>MoneyLedger.Reset (nowa kampania / wczytanie) i klucz wylaczony: okno od zera.</summary>
        private static void F4Reset()
        {
            _f4Days = 0; _f4FirstDay = -1; _f4V0 = -1; _f4V1 = 0; _f4VPrev = -1; _f4N = _f4Empty = 0;
            Array.Clear(_f4Sell, 0, Classes); Array.Clear(_f4Buy, 0, Classes);
            _f4Due = _f4Short = _f4Top = _f4Own = _f4Own259 = _f4LordWage = _f4DPurse = 0; _f4Ms = 0; _f4St = 0; _f4CarWage = 0;
            _f4Err.Clear();                                 // recenzja: po wczytaniu / nowej kampanii pierwszy blad miejsca znow trafia do logu
        }

        private static readonly System.Collections.Generic.HashSet<string> _f4Err = new System.Collections.Generic.HashSet<string>();
        private static void F4Stumble(string where, Exception e)
        {
            try { if (_f4Err.Add(where)) Log.Error("MoneyLedger." + where, e); } catch { }
        }
    }
}
