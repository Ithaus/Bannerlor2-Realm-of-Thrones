using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// 322d (Jeff 03.11, audyt ekonomii propozycja 4 wariant A: "Dlugi rad: szybsze obnizanie raty dla pana i korony (firma DS), gdy rada jest mocno zadluzona").
    /// DOWOD (Armoury-2026-11-03_00-19-16.log, linie "Dlugi miast (m96-dlugi)" doby 109180-109250): dlug rad 1.83 -> 3.26 mln w 70 dob, splaty z nadwyzki kas 0 w 0 miastach,
    /// niewyplacalne 9 (zaleglosc bez kredytu 7 - 1.51 mln), Bank Zelazny 4.14 -> 3.29 mln (pula dla miast konczy sie za ok. 60 dob); linia DS doby 109249: Vaith 555672 zl
    /// zaleglosci = 15.6 rat, Pebbleton 555436 = 14.6 rat, Tyrosh 317965 = 9.5 rat, udzial firm w doplywie netto miast 0.951. PRZYCZYNA: rada splaca tylko bez zaleglosci firmy
    /// (TownDebt.TownDay krok 2: al + ac &lt; 1), a firma pana i korony schodzi najwyzej do biezacego doplywu netto kupcow (FastCut, IncomeCut - sufit doplyw x 364) albo
    /// o 15% raz na kwartal (Review wymuszony). Firma rowna doplywowi zostawia kasie zero na zaleglosci i dlug - miasto pozycza bez konca.
    /// REGULA (miasta z firma DS, kazde - AI i gracz): rada z dlugiem (TownDebt.ArrearsLike &gt;= 1), ktora jest MOCNO zadluzona - dlug rady + zaleglosc firmy z kasy (AL + AC)
    /// &gt;= Town Debt Farm Cut Weeks x rata tygodniowa z kasy ((FL + FC) / 52) albo rada niewyplacalna (TownDebt.TdInsolvent: dlug &gt;= 90% limitu albo zaleglosc bez
    /// kredytu wczoraj) - firma pana i korony z kasy schodzi o Town Debt Farm Cut Step co 7 dob (FastGapDays, raz na rate tygodniowa), najnizej do Town Debt Farm Cut Floor
    /// x biezacy doplyw netto kupcow x 364 (pamiec doplywu najmniej 7 dob; bez pomiaru - bez zmiany). Stop, gdy miasto moze juz splacac: bez zaleglosci z kasy i kasa ponad
    /// zapas kupcow i ponad podloge + najblizsza rate (TdKeep - te same warunki co krok 2 splaty) ALBO bez zaleglosci z kasy i rada splacala z nadwyzki (krok 2 TownDay,
    /// TownDebt.TdRepaidWithin) w ostatnich FastGapDays dobach (recenzja 322d: sam stan kasy w DayStart po dniu raty jest ponizej TdKeep - miasto splacajace bylo
    /// obnizane co tydzien do podlogi) - wtedy reszte robi splata z nadwyzki. Poza tym stara wolna regula bez zmian
    /// (FastCut / IncomeCut / przeglad kwartalny). Czesc z oszczednosci ludu (FLf) bez zmiany - placi ja lud, nie kasa (jak FastCut). Zaleglosci (AL, AC) bez zmiany - to dlug
    /// kasy wobec pana i korony, splaca je nadwyzka ponad nowa rate. Udzial (Share) bez zmiany - po splacie dlugu przeglad zwykly i podwyzka wedlug nadwyzki (obie wstrzymane
    /// przy zaleglosci i dlugu rady - ArrearsLike) prowadza firme z powrotem w gore. Jedna zmiana firmy na dobe: bez obnizki w dobie przegladu (Fixed), szybkiej obnizki
    /// (FastDay) i cyklu obnizki z dochodu (SLow). Oblezenie / 28 dob po zdobyciu (RemitOf) - nic.
    /// NIC Z NICZEGO: zloto sie nie rusza - zmienia sie tylko nalezna rata (mniej przelewu kasa -&gt; pan i skarbiec; zostaje w kasie na zaleglosc, potem na splate
    /// wierzycieli). Kontrola linii DS "platnicy - odbiorcy" jak dotad.
    /// Linia: segment "322d obnizki firm DS przy dlugu rady" w linii "Dlugi miast (m96-dlugi)" tej doby - miasto po miescie: dlug rady, zaleglosc, rata przed i po, powod.
    /// Kazde miasto we wlasnym try, potkniecia liczone (Stumble), bez globalnego wylacznika (CLAUDE.md 7). Watek glowny (DayStart).
    /// </summary>
    internal static partial class DsTowns
    {
        private const int DebtCutListMax = 12;                  // miast wypisanych z nazwy w linii (reszta tylko w liczbach)
        private static readonly List<string> _dcList = new List<string>();
        private static int _dcN, _dcHold, _dcHoldRep, _dcFloor, _dcNoNet, _dcHeavy, _dcNS, _dcDay = int.MinValue;
        private static double _dcAmt, _dcAmtS;

        private static void DebtCutReset()
        {
            _dcList.Clear(); _dcN = _dcHold = _dcHoldRep = _dcFloor = _dcNoNet = _dcHeavy = 0; _dcAmt = 0; _dcNS = 0; _dcAmtS = 0; _dcDay = int.MinValue;
        }

        private static double DcClamp(double v, double lo, double hi) { return double.IsNaN(v) ? lo : (v < lo ? lo : (v > hi ? hi : v)); }

        private static void DebtCut(int today, Settings s)
        {
            _dcList.Clear(); _dcN = _dcHold = _dcHoldRep = _dcFloor = _dcNoNet = _dcHeavy = 0; _dcAmt = 0; _dcDay = today;
            if (s == null || !s.TownDebtFarmCut || !s.TownDebt) return;
            double step = DcClamp(s.TownDebtFarmCutStep, 0, 0.5);
            if (!(step > 0)) return;
            double kw = DcClamp(s.TownDebtFarmCutWeeks, 1, 52);
            double fl = DcClamp(s.TownDebtFarmCutFloor, 0, 1);
            foreach (var f in _farm.Values)
            {
                try
                {
                    var st = f.St;
                    if (st == null || !st.IsTown || st.Town == null || f.Fixed < 0) continue;
                    double debt = TownDebt.ArrearsLike(st);
                    if (!(debt >= 1)) continue;                                      // rada bez dlugu - stara regula
                    double F = f.FL + f.FC;
                    if (!(F > 0)) continue;
                    if (RemitOf(st) != 0) continue;
                    double inst = F / Weeks;
                    double arr = Math.Max(0, f.AL) + Math.Max(0, f.AC);
                    bool ins = TownDebt.TdInsolvent(st, today);
                    bool heavy = debt + arr >= kw * inst;
                    if (!heavy && !ins) continue;
                    _dcHeavy++;
                    var t = st.Town;
                    // miasto juz moze splacac (warunki kroku 2 TownDebt.TownDay) albo splacalo z nadwyzki w ostatnich FastGapDays dobach (recenzja: kasa po dniu raty
                    // jest ponizej TdKeep - sam stan kasy dzis nie wystarcza) - bez obnizki
                    if (arr < 1)
                    {
                        if (t.Gold > Math.Max(TownPurse.Reserve(t), TdKeep(st))) { _dcHold++; continue; }
                        if (TownDebt.TdRepaidWithin(st, today, FastGapDays)) { _dcHold++; _dcHoldRep++; continue; }
                    }
                    if (f.DebtCutDay >= 0 && f.DebtCutDay <= today && today - f.DebtCutDay < FastGapDays) continue;
                    if (f.Fixed == today || f.FastDay == today || f.SLow == today) continue;   // jedna zmiana firmy na dobe
                    double net = NetAvg(f, NetMinDays);
                    if (double.IsNaN(net) || double.IsInfinity(net)) { _dcNoNet++; continue; }
                    double floorF = fl * Math.Max(0.0, net) * YearDays;
                    double nf = Math.Max(floorF, F * (1 - step));
                    if (!(nf >= 0) || !(nf < F - 1)) { _dcFloor++; continue; }
                    double r = nf / F;
                    f.FL *= r; f.FC *= r;
                    f.DebtCutDay = today;
                    f.SG0 = long.MinValue; f.SGDay = -1;                             // pomiar przyrostu kasy (SurplusReview) od nowa
                    _dcN++; _dcNS++; _dcAmt += F - nf; _dcAmtS += F - nf;
                    if (_dcList.Count < DebtCutListMax)
                    {
                        string why = ins && heavy ? "niewyplacalna i dlug+zaleglosc " : ins ? "niewyplacalna, dlug+zaleglosc " : "dlug+zaleglosc ";
                        _dcList.Add((st.Name != null ? st.Name.ToString() : st.StringId) + ": dlug rady " + N0(debt) + ", zaleglosc z kasy " + N0(arr)
                                    + ", rata " + N0(inst) + " -> " + N0(nf / Weeks) + " zl/tydz (" + (r * 100).ToString("0.#", Inv) + "%), powod: " + why
                                    + ((debt + arr) / Math.Max(1.0, inst)).ToString("0.#", Inv) + " rat (prog " + kw.ToString("0.#", Inv) + ")"
                                    + ", doplyw " + N0(net) + "/d, podloga rata " + N0(floorF / Weeks) + (nf <= floorF + 1 ? " (osiagnieta)" : ""));
                    }
                }
                catch (Exception e) { Stumble("DebtCut", e); }
            }
        }

        /// <summary>322d: segment linii "Dlugi miast (m96-dlugi)" - obnizki z DayStart tej doby (DebtCut). Klucz wylaczony - krotka informacja. Nie rzuca.</summary>
        internal static string DebtCutNote(Settings s)
        {
            try
            {
                if (s == null) return "";
                if (!s.TownDebtFarmCut) return " | 322d obnizki firm DS przy dlugu rady: WYLACZONE (Town Debt Farm Cut)";
                bool fresh = _dcDay == (int)CampaignTime.Now.ToDays;
                var sb = new StringBuilder(256);
                sb.Append(" | 322d obnizki firm DS przy dlugu rady (Town Debt Farm Cut TAK, krok ").Append((DcClamp(s.TownDebtFarmCutStep, 0, 0.5) * 100).ToString("0.#", Inv))
                  .Append("% co ").Append(FastGapDays).Append(" dob, prog ").Append(DcClamp(s.TownDebtFarmCutWeeks, 1, 52).ToString("0.#", Inv)).Append(" rat albo niewyplacalna, podloga ")
                  .Append(DcClamp(s.TownDebtFarmCutFloor, 0, 1).ToString("0.##", Inv)).Append(" x doplyw): ");
                if (!fresh) { sb.Append("dzis bez przegladu (firmy DS nieustalone albo nieczynne)"); return sb.ToString(); }
                sb.Append("miast mocno zadluzonych ").Append(_dcHeavy).Append(", dzis obnizone ").Append(_dcN).Append(" (-").Append(N0(_dcAmt)).Append(" zl/rok)")
                  .Append(", juz splaca (bez obnizki) ").Append(_dcHold).Append(" (w tym splata z nadwyzki w ").Append(FastGapDays).Append(" dobach ").Append(_dcHoldRep).Append(")").Append(", przy podlodze ").Append(_dcFloor).Append(", bez pomiaru doplywu ").Append(_dcNoNet)
                  .Append("; w sesji ").Append(_dcNS).Append(" (-").Append(N0(_dcAmtS)).Append(" zl/rok)");
                if (_dcList.Count > 0) sb.Append("; ").Append(string.Join("; ", _dcList.ToArray()));
                if (_dcN > _dcList.Count) sb.Append("; i ").Append(_dcN - _dcList.Count).Append(" innych");
                return sb.ToString();
            }
            catch (Exception e) { Stumble("DebtCutNote", e); return ""; }
        }
    }
}
