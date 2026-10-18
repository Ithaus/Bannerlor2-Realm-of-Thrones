using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;

namespace Armoury
{
    /// <summary>
    /// DS F3 - L1 ZALICZKA MIESZCZAN POD NADANIE (projekt rozdz. 7 L1, 10.3; rozszerzenie korona2 - nowy rodzaj dlugu, klucz DsAnticipationLoans, czynny z CrownTownLoans
    /// i przy ustalonych firmach DsTowns). Jeff 17.10: "oprzec tez o pozyczki od mieszczan, kupcow lub bankow"; "dlugi trzeba splacac".
    ///  - KIEDY: K5 (KingdomTreasury.WageRefund) - korona w wojnie nie ma na nalezny zwrot zoldu z reszty wplywow dnia ani ze skarbca ponad zapas wojny; PRZED Bankiem 186.
    ///  - OD KOGO: miasta krolestwa jak korona2 (KlSources: kasa ponad zapas kupcow i kiesy W / R / P ponad poziom normalny, z kazdego najwyzej polowa nadwyzki na dobe,
    ///    bez oblezonych i miast rodu gracza), najpierw najwieksze.
    ///  - LIMIT: niezebrane raty biezacego nadania (dziesiata od ruchomosci kies i kas krolestwa + prognoza W4 do konca kwartalu - DsSeason.AnticipationRoom) minus
    ///    dlug zaliczek. Zakaz po niesplacie korona2 dotyczy takze zaliczek.
    ///  - CENA I SPLATA: CrownTownLoanRateYear (10%) rocznie, codziennie; splata PIERWSZA z rat nadania wplaconych dzis (dziesiata + W4; TownFolk.CrownLoansPay - po racie
    ///    Banku, przed darami), z wplywow dnia. W dzien kwartalu (i przy wylaczeniu DS) reszta przechodzi do zwyklego dlugu korona2 (L2: odsetki w wojnie, raty w pokoju).
    ///  - Zloto: kasa miasta / kiesa ludu -> skarbiec (pozyczka) i z powrotem (splata), dlug to roszczenie; zaliczka to dlug u miast (CrownTownDebt - poza 1/450 zapasu,
    ///    nadwyzka W5b i skarbcem ponad zapas wojny K5).
    /// Zapis w "arm_korona2": rekordy "A,krolestwo,pozyczone,splacone,odsetki" i czesci "Q,osada,rodzaj,dlug" (starsza wersja moda je pomija). Linia "Pozyczki zaliczkowe (DS)".
    /// </summary>
    internal static partial class TownFolk
    {
        private static readonly Dictionary<string, KlDebtK> _kla = new Dictionary<string, KlDebtK>();   // id krolestwa -> zaliczki mieszczan pod nadanie
        private static long _kaLent, _kaLentKasa, _kaLentPurse, _kaRepaid; private static double _kaToNormal, _kaLost, _kaInt, _kaDust, _kaPrev = double.NaN;
        private static int _kaLentN, _kaBlkLimit, _kaBlkSrc, _kaBlkBan, _kaStumbles;
        private static double _klFromAnt;   // korona2: dlug przeniesiony z zaliczek (kontrola linii korona2)

        private static void ResetAnt()
        {
            _kla.Clear(); _kaPrev = double.NaN; AntDayZero();
        }

        private static void AntDayZero()
        {
            _kaLent = _kaLentKasa = _kaLentPurse = _kaRepaid = 0; _kaToNormal = _kaLost = _kaInt = _kaDust = 0;
            _kaLentN = _kaBlkLimit = _kaBlkSrc = _kaBlkBan = _kaStumbles = 0;
            foreach (var kv in _kla) { kv.Value.DLent = 0; kv.Value.DPaid = 0; }
        }

        private static void AntStumble(string where, Exception e) { _kaStumbles++; Stumble("CrownLoans.Ant." + where, e); }

        private static double AntDebt(string id) { KlDebtK x; return id != null && _kla.TryGetValue(id, out x) ? KlDebt(x) : 0; }

        /// <summary>
        /// K5 krok 2 (WageRefund, po skarbcu ponad zapas wojny, przed Bankiem): zaliczka mieszczan na brakujacy zwrot `want` - najwyzej limit (niezebrane raty nadania minus
        /// dlug zaliczek) i polowa nadwyzek miast krolestwa. Zloto do skarbca (wolajacy wyplaca je rodom tego samego dnia). Zwraca pozyczone. Nie rzuca.
        /// </summary>
        internal static long Anticipate(Kingdom k, long want)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || k == null || k.StringId == null || want <= 0 || !s.DsAnticipationLoans || !CrownLoansOn || !DsTowns.FarmsLive) return 0;
                int until;
                if (_klBan.TryGetValue(k.StringId, out until) && (int)CampaignTime.Now.ToDays < until) { _kaBlkBan++; return 0; }
                double room = DsSeason.AnticipationRoom(k) - AntDebt(k.StringId);
                if (room < 1) { _kaBlkLimit++; return 0; }
                var src = new List<KlSrc>();
                int towns;
                double avail = KlSources(k, src, out towns);
                if (avail < 1) { _kaBlkSrc++; return 0; }
                long left = (long)Math.Floor(Math.Min(Math.Min((double)want, room), Math.Min(avail, int.MaxValue / 2)));
                if (left <= 0) return 0;
                src.Sort((p, q) => q.Ex.CompareTo(p.Ex));
                KlDebtK x;
                if (!_kla.TryGetValue(k.StringId, out x)) { x = new KlDebtK(); _kla[k.StringId] = x; }
                long got = 0;
                foreach (var y in src)
                {
                    if (left <= 0) break;
                    long w = Math.Min(left, (long)Math.Floor(y.Ex));
                    if (w <= 0) continue;
                    long t = KlTake(y, w);
                    if (t <= 0) continue;
                    try { k.KingdomBudgetWallet += (int)t; }
                    catch (Exception e) { if (KlPut(y.T.Settlement, y.Kind, t, false) == 0) AntStumble("Anticipate(zwrot)", e); AntStumble("Anticipate(skarbiec)", e); continue; }
                    string sid = y.T.Settlement.StringId;
                    KlPart part = null;
                    foreach (var p in x.Parts) if (p.St == sid && p.Kind == y.Kind) { part = p; break; }
                    if (part == null) { part = new KlPart { St = sid, Kind = y.Kind }; x.Parts.Add(part); }
                    part.Left += t; x.Lent += t; x.DLent += t; got += t; left -= t;
                    if (y.Kind == 0) _kaLentKasa += t; else _kaLentPurse += t;
                }
                if (got > 0) { _kaLent += got; _kaLentN++; double nd = KlDebt(x); if (nd > x.Peak) x.Peak = nd; DsSeason.NoteAnticipation(k, got); }
                else if (x.Parts.Count == 0) _kla.Remove(k.StringId);
                return got;
            }
            catch (Exception e) { AntStumble("Anticipate", e); return 0; }
        }

        /// <summary>
        /// ArmouryBehavior (po KingdomTreasury.WageRefund i CrownBorrow.SeizePledged, przed CrownRents - W5 i renty 180; dzien korony otwarty): odsetki zaliczek i splata z rat
        /// nadania wplaconych dzis (dziesiata + W4), najwyzej z tego, co ZOSTALO z wplywow dnia po zwrocie zoldu. Recenzja 17.10: dotad splata szla przed zwrotem (CrownLoansPay)
        /// - korona splacala tania zaliczke, a wiekszy brak zwrotu brala w Banku na 20-30%; w dobie, w ktorej brak zwrotu pokryl skarbiec, zaliczka albo Bank - bez splaty
        /// (same odsetki). Nie rzuca.
        /// </summary>
        internal static void AntRepayDay()
        {
            var s = Settings.Current;
            if (s == null || Campaign.Current == null || !CrownLoansOn) return;
            try { AntRepay(s); } catch (Exception e) { AntStumble("RepayDay", e); }
        }

        private static void AntRepay(Settings s)
        {
            if (_kla.Count == 0) return;
            double rate = KlRate(s) / YearDays;
            var drop = new List<string>();
            foreach (var kv in _kla)
            {
                var x = kv.Value;
                try
                {
                    var k = KlKingdom(kv.Key);
                    if (k == null || k.IsEliminated) { _kaLost += KlDebt(x); drop.Add(kv.Key); continue; }   // krolestwa nie ma - dlug przepada (zloto juz w obiegu)
                    double it = 0;
                    foreach (var p in x.Parts) { double a = Math.Max(0, p.Left) * rate; p.Left += a; p.Acc += a; it += a; }   // Acc: recenzja calosci 18.10 (odsetki przed kapitalem)
                    x.Interest += it; _kaInt += it;
                    var d = CrownIncome.DayOf(k);
                    if (d != null && d.DsTreas <= 0 && d.DsAnt <= 0 && d.LoanIn <= 0)
                    {
                        long inst = DsSeason.InstalmentsToday(k);
                        long pay = Math.Min(Math.Min((long)Math.Ceiling(KlDebt(x) - 1e-9), inst), Math.Min(CrownIncome.LeftFor(k), (long)Math.Max(0, k.KingdomBudgetWallet)));
                        if (pay > 0)
                        {
                            long k0 = _klPaidKasa, p0 = _klPaidPurse;
                            long placed = KlRepay(k, x, pay);
                            _klPaidKasa = k0; _klPaidPurse = p0;   // linia korona2 liczy tylko swoje splaty
                            if (placed > 0) { CrownIncome.Spent(k, placed); _kaRepaid += placed; x.DPaid += placed; }
                        }
                    }
                    foreach (var p in x.Parts) if (p.Left < 0.5) _kaDust += p.Left;
                    x.Parts.RemoveAll(p => p.Left < 0.5);
                    if (x.Parts.Count == 0) drop.Add(kv.Key);
                }
                catch (Exception e) { AntStumble("Repay", e); }
            }
            foreach (var id in drop) _kla.Remove(id);
        }

        /// <summary>DsSeason (dzien kwartalu): zaliczki krolestwa niesplacone w kwartale -> zwykly dlug korona2 (te same czesci u tych samych wierzycieli).</summary>
        internal static void AntToNormal(Kingdom k)
        {
            try
            {
                if (k == null || k.StringId == null) return;
                KlDebtK a;
                if (!_kla.TryGetValue(k.StringId, out a)) return;
                _kla.Remove(k.StringId);
                MergeToNormal(k.StringId, a);
            }
            catch (Exception e) { AntStumble("ToNormal", e); }
        }

        /// <summary>DsTowns.DayStart (F3 wylaczone): wszystkie zaliczki -> zwykly dlug korona2 (dlugi trzeba splacac).</summary>
        internal static void AntAllToNormal()
        {
            try
            {
                if (_kla.Count == 0) return;
                var all = new List<KeyValuePair<string, KlDebtK>>(_kla);
                _kla.Clear();
                foreach (var kv in all) MergeToNormal(kv.Key, kv.Value);
            }
            catch (Exception e) { AntStumble("AllToNormal", e); }
        }

        private static void MergeToNormal(string id, KlDebtK a)
        {
            double moved = 0;
            KlDebtK x;
            if (!_kl.TryGetValue(id, out x)) { x = new KlDebtK(); _kl[id] = x; }
            foreach (var p in a.Parts)
            {
                if (!(p.Left >= 0.5)) continue;
                KlPart q = null;
                foreach (var r in x.Parts) if (r.St == p.St && r.Kind == p.Kind) { q = r; break; }
                if (q == null) { q = new KlPart { St = p.St, Kind = p.Kind }; x.Parts.Add(q); }
                q.Left += p.Left; q.Acc += Math.Max(0.0, p.Acc); moved += p.Left;   // Acc: recenzja calosci 18.10
            }
            if (x.Parts.Count == 0) { _kl.Remove(id); return; }
            x.Lent += moved;
            double nd = KlDebt(x); if (nd > x.Peak) x.Peak = nd;
            _kaToNormal += moved; _klFromAnt += moved;
        }

        /// <summary>Linia doby "Pozyczki zaliczkowe (DS)" (DsTowns.Daily) - przy dlugu albo ruchu w oknie; zeruje okno.</summary>
        internal static void AntLine(int day)
        {
            try
            {
                var s = Settings.Current;
                double debt = 0; int parts = 0;
                var per = new List<KeyValuePair<double, string>>();
                foreach (var kv in _kla)
                {
                    double dk = KlDebt(kv.Value); debt += dk; parts += kv.Value.Parts.Count;
                    var k = KlKingdom(kv.Key);
                    double room = k != null ? DsSeason.AnticipationRoom(k) : 0;
                    per.Add(new KeyValuePair<double, string>(dk, (k != null && k.Name != null ? k.Name.ToString() : kv.Key) + " " + N0(dk) + "/" + N0(room)
                            + (kv.Value.DLent > 0 ? ", +" + kv.Value.DLent : "") + (kv.Value.DPaid > 0 ? ", -" + kv.Value.DPaid : "")));
                }
                bool any = debt >= 1 || _kaLent > 0 || _kaRepaid > 0 || _kaToNormal >= 1 || _kaLost >= 1 || _kaBlkLimit + _kaBlkSrc + _kaBlkBan > 0;
                string ctl = "-";
                if (!double.IsNaN(_kaPrev)) ctl = Math.Round(debt - (_kaPrev + _kaLent + _kaInt - _kaRepaid - _kaToNormal - _kaLost - _kaDust)).ToString("0", KlInv);
                _kaPrev = debt;
                if (any && s != null && s.LogEnabled)
                {
                    per.Sort((p, q) => q.Key.CompareTo(p.Key));
                    var txt = new List<string>(); foreach (var p in per) txt.Add(p.Value);
                    var sb = new StringBuilder(900);
                    sb.Append("Pozyczki zaliczkowe (DS): dzien ").Append(day)
                      .Append(" | zawarte ").Append(_kaLent).Append(" zl (").Append(_kaLentN).Append(" pozyczek; z kas miast ").Append(_kaLentKasa).Append(", z kies mieszczan ").Append(_kaLentPurse).Append(')')
                      .Append("; bez zaliczki: limit (niezebrane raty nadania) ").Append(_kaBlkLimit).Append(", miasta bez nadwyzki ").Append(_kaBlkSrc).Append(", zakaz po niesplacie ").Append(_kaBlkBan)
                      .Append(" | splacone z rat nadania ").Append(_kaRepaid).Append(", odsetki ").Append(N0(_kaInt)).Append(" (").Append((100 * KlRate(s)).ToString("0.#", KlInv)).Append("% rocznie)")
                      .Append(", po kwartale do zwyklego dlugu korona2 ").Append(N0(_kaToNormal)).Append(", przepadle z krolestwami zniszczonymi ").Append(N0(_kaLost))
                      .Append(" | dlug zaliczek ").Append(N0(debt)).Append(" (krolestw ").Append(_kla.Count).Append(", czesci ").Append(parts).Append(')')
                      .Append(" | kontrola: dlug - (wczoraj + zawarte + odsetki - splacone - przeniesione - przepadle) ").Append(ctl)
                      .Append(" | na krolestwo (dlug/limit, +zawarte, -splacone): ").Append(txt.Count > 0 ? string.Join("; ", txt.ToArray()) : "-")
                      .Append(" | potkniecia ").Append(_kaStumbles).Append('.');
                    Log.Info(sb.ToString());
                }
            }
            catch (Exception e) { AntStumble("Line", e); }
            AntDayZero();
        }

        /// <summary>ExportKorona2: zaliczki - ";A,krolestwo,pozyczone,splacone,odsetki;Q,osada,rodzaj,dlug...".</summary>
        private static void ExportAnt(StringBuilder sb)
        {
            foreach (var kv in _kla)
            {
                var x = kv.Value;
                if (x.Parts.Count == 0) continue;
                sb.Append(";A,").Append(kv.Key).Append(',').Append(x.Lent.ToString("R", KlInv)).Append(',').Append(x.Repaid.ToString("R", KlInv)).Append(',').Append(x.Interest.ToString("R", KlInv));
                foreach (var p in x.Parts) sb.Append(";Q,").Append(p.St).Append(',').Append(p.Kind).Append(',').Append(p.Left.ToString("R", KlInv));
            }
        }

        /// <summary>ImportKorona2: rekord "A" (nowa zaliczka - zwraca ja, kolejne "Q" sa jej czesciami) albo "Q". Inne - null (bez zmian).</summary>
        private static KlDebtK ImportAnt(string[] f, KlDebtK cur)
        {
            if (f[0] == "A" && f.Length >= 5)
            {
                var a = new KlDebtK();
                double.TryParse(f[2], NumberStyles.Float, KlInv, out a.Lent);
                double.TryParse(f[3], NumberStyles.Float, KlInv, out a.Repaid);
                double.TryParse(f[4], NumberStyles.Float, KlInv, out a.Interest);
                _kla[f[1]] = a;
                return a;
            }
            if (f[0] == "Q" && f.Length >= 4 && cur != null)
            {
                int kind; double left;
                if (int.TryParse(f[2], NumberStyles.Integer, KlInv, out kind) && double.TryParse(f[3], NumberStyles.Float, KlInv, out left) && left > 0 && !double.IsInfinity(left) && kind >= 0 && kind <= 3)
                    cur.Parts.Add(new KlPart { St = f[1], Kind = kind, Left = left });
                return cur;
            }
            return cur;
        }
    }
}
