using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// M4 (pieniadze roku 2, docs/pieniadze-rok-2/PROJEKT-WYBRANY.md rozdz. 3.8; decyzja Jeffa 14.10 "danina - tak") - DANINA W DLUGIM POKOJU.
    /// Klucz FolkPeaceTribute (wylaczony = jak 24bd64b3: w pokoju danina 0). Ta sama droga co danina wojenna P1 (KingdomTreasury.FolkWarAid wolane z
    /// TownFolk.PayAid w kroku 5 doby ludu) - jedna regula, inna stawka w pokoju:
    ///  - KIEDY: krolestwo wlasciciela osady nie jest w wojnie z zadnym krolestwem (WtAtWar) i od ostatniego pokoju minelo co najmniej 91 dob (PtMinPeaceDays,
    ///    data pokoju z gry - StanceLink.PeaceDeclarationDate; krolestwo, ktore nigdy nie wojowalo - data zero, czyli dawno); w wojnie dziala P1 jak dotad.
    ///    Recenzja 14.10: do tego pamiec sesji _ptSince - ostatnia doba w wojnie (krok korony z WtAtWar) i pierwsza doba krolestwa powstalego w tej sesji;
    ///    pokoj = mniejsza z obu miar (wojna z krolestwem juz wyeliminowanym i krolestwo nowe bez wojen nie wchodza w M4 przed 91 dobami). Po wczytaniu
    ///    pamieci nie ma - same laczniki gry (jak przed recenzja).
    ///  - OD CZEGO: tylko lokata W - kiesa wyrobnikow miasta PONAD zapas dni (TownFolkReserveDays; to samo "ov" co P1). Rzemieslnicy, kupcy i podzamcza - nic.
    ///  - ILE: korona bierze tylko tyle, ile wczoraj wydala na chleb M3 (BreadForThePoor, KDay.Bread - wylacznie chleb korony dla glodnych osad WLASNEGO
    ///    krolestwa, z reszty wplywow dnia; chleb pana zastepczo sie nie liczy), najwyzej PtCap (0.5%) lokaty na dobe: stawka krolestwa na jutro
    ///    r = min(0.5%, chleb dzis / lokata W krolestwa po dzisiejszej daninie), kazde miasto oddaje r x swoja kiesa W ponad zapas (w dol do zlotowki, ulamek czeka
    ///    jak w P1), a suma krolestwa najwyzej BUDZET = min(chleb, 0.5% lokaty w gore do zlotowki) (recenzja 14.10: w biednych krolestwach z glodnymi miastami
    ///    wiaze pulap, nie chleb - budzet i linia pokazuja to, co naprawde da sie wziac; miasta po wyczerpaniu nie placa). Chleb plynie w kroku korony PO dobie ludu, wiec plan liczony w
    ///    kroku korony doby D (PeaceTributeDay - po BreadRest, przed CrownIncome.End) i pobierany w dobie ludu D+1; plan starszy niz wczorajszy - nic.
    ///  - BRAMKI P1: klucz Folk War Aid i podatek 184 / kiesa ludu / 165 (WtOn), rod z krolestwem, osada nieoblezona (BesiegedPay - wstrzymane), prog lojalnosci
    ///    P1b (FolkAidLoyalty.GateHolds - poborcy wychodza). KARA P1b dziala sama (te same DAid / AidFrac osady), w dymku lojalnosci "Peace tribute ...".
    ///  - PLATNIK -> ODBIORCA: kiesa W miasta -> skarbiec krolestwa (to samo okno _waWindow co P1 -> wplyw dnia 165, KDay.WarAid); korona wydaje to zloto tej
    ///    samej doby w zwyklej kolejnosci (rata 186, dary, reparacje, kontrakty, zwrot zoldu, chleb M3, renty 180 - zloto M4 nie jest odlozone na chleb, wchodzi do
    ///    wplywow dnia jak P1; korona nie traci na chlebie w sumie dwoch dob). W liczniki P1 (linia P1, "Obieg", ksiega kies) wchodzi razem z P1 -
    ///    to ta sama danina; linia "Danina pokoju (M4): dzien N" podaje osobno: krolestwa, wziete, plan z chleba, chleb dzis, lokata, kontrolki.
    ///  - Gracz: lud jego miast placi tak samo (do skarbca krolestwa, nie do kiesy gracza); komunikaty "war aid" P1 nie dotycza M4 (bez nowych komunikatow);
    ///    w oknie mieszkancow zdanie mowi "tribute toward the bread of the hungry" (po poborze), a przed nim zapowiedz albo prog lojalnosci (PeaceTributeRatePeek).
    /// Stan: pamiec sesji (plan, liczniki) - bez pol zapisu; pierwsza doba po wczytaniu - bez planu, M4 = 0. Wyjatek - licznik potkniec P1 (WaStumble).
    /// </summary>
    internal static partial class KingdomTreasury
    {
        private const double PtMinPeaceDays = 91;   // pokoj co najmniej pore roku (91 dob) - "dlugi pokoj" z projektu PZ-1 / M4
        private const double PtCap = 0.005;         // najwyzej 0.5% lokaty W na dobe

        private sealed class PtPlanK { public int Day; public long Bread, Plan, Left; public double Rate; }   // plan krolestwa: doba korony, chleb, budzet (min(chleb, 0.5% lokaty)), reszta budzetu, stawka
        private sealed class PtDayK { public long Taken; public int Towns; }

        private static readonly Dictionary<Kingdom, PtPlanK> _ptPlan = new Dictionary<Kingdom, PtPlanK>();   // plan na jutro (doba korony Day)
        private static readonly Dictionary<Kingdom, PtDayK> _ptK = new Dictionary<Kingdom, PtDayK>();         // doba ludu: na krolestwo
        private static readonly HashSet<Settlement> _ptPaid = new HashSet<Settlement>();                       // osady, ktore zaplacily M4 w dobie gry _ptPaidDay
        private static int _ptPaidDay = int.MinValue;
        private static readonly Dictionary<Kingdom, double> _ptPeace = new Dictionary<Kingdom, double>();      // doby pokoju krolestwa (pamiec doby)
        private static int _ptPeaceDay = int.MinValue;
        private static readonly Dictionary<Kingdom, int> _ptSince = new Dictionary<Kingdom, int>();   // pamiec sesji: ostatnia doba w wojnie / pierwsza doba krolestwa nowego w sesji
        private static readonly HashSet<Kingdom> _ptKnown = new HashSet<Kingdom>();                   // krolestwa widziane w krokach korony tej sesji
        private static bool _ptKnownInit;
        private static long _ptTaken, _ptHeld; private static int _ptTowns, _ptGate, _ptCut, _ptCtlOver, _ptCtlRate, _ptCtlWar;

        private static bool PtOn(Settings s) { return s != null && s.FolkPeaceTribute && s.FolkWarAid && s.BreadForThePoor && WtOn; }

        private static void PeaceTributeReset()
        {
            _ptPlan.Clear(); _ptK.Clear(); _ptPaid.Clear(); _ptPaidDay = int.MinValue; _ptPeace.Clear(); _ptPeaceDay = int.MinValue;
            _ptSince.Clear(); _ptKnown.Clear(); _ptKnownInit = false;
            PtClearDay();
        }

        private static void PtClearDay()
        {
            _ptK.Clear();
            _ptTaken = 0; _ptHeld = 0; _ptTowns = _ptGate = _ptCut = _ptCtlOver = _ptCtlRate = _ptCtlWar = 0;
        }

        /// <summary>
        /// Doby od ostatniego pokoju krolestwa z innym krolestwem (0 - w wojnie). Pary bez stalego stanu gry (DiplomacyModel.GetShallowDiplomaticStance)
        /// i krolestwa wyeliminowane pomijane - odczyt laczy tylko tam, gdzie AtWar (IsAtWarWith) i tak go codziennie czyta (GetStanceWith nie tworzy nowych).
        /// Pamiec doby. Nigdy w wojnie - double.MaxValue. Recenzja 14.10: najwyzej tyle dob, ile minelo od doby z _ptSince (ostatnia doba w wojnie w tej sesji,
        /// pierwsza doba krolestwa powstalego w tej sesji) - laczniki gry nie pamietaja wojny z krolestwem wyeliminowanym, a nowy lacznik ma date zero.
        /// </summary>
        private static double PtPeaceDays(Kingdom k)
        {
            int today = (int)CampaignTime.Now.ToDays;
            if (_ptPeaceDay != today) { _ptPeace.Clear(); _ptPeaceDay = today; }
            double v;
            if (_ptPeace.TryGetValue(k, out v)) return v;
            v = double.MaxValue;
            var dm = Campaign.Current != null && Campaign.Current.Models != null ? Campaign.Current.Models.DiplomacyModel : null;
            foreach (var o in Kingdom.All)
            {
                if (o == null || o == k || o.IsEliminated) continue;
                if (k.IsAtWarWith(o)) { v = 0; break; }
                if (dm != null && dm.GetShallowDiplomaticStance(k, o).HasValue) continue;
                var link = k.GetStanceWith(o);
                if (link == null || link.IsAtWar) continue;
                double d = link.PeaceDeclarationDate.ElapsedDaysUntilNow;
                if (d < v) v = d;
            }
            int since;
            if (v > 0 && _ptSince.TryGetValue(k, out since) && today - since < v) v = Math.Max(0, today - since);
            _ptPeace[k] = v;
            return v;
        }

        /// <summary>
        /// M4 - stawka dnia daniny pokoju klasy `cls` osady `st` krolestwa `k` (FolkWarAid, gdy P1 dalo 0 z powodu pokoju albo s = 0). 0 - nie placi; gated =
        /// prog lojalnosci P1b (poborcy wychodza). Tylko wyrobnicy miast, tylko krolestwo w pokoju >= 91 dob z wczorajszym planem z chleba korony.
        /// </summary>
        private static double PtRate(Settlement st, int cls, Kingdom k, Settings s, out bool gated)
        {
            gated = false;
            if (k == null || cls != WtW || st == null || !st.IsTown || !PtOn(s)) return 0;
            PtPlanK p;
            int today = (int)CampaignTime.Now.ToDays;
            if (!_ptPlan.TryGetValue(k, out p) || p.Day != today - 1 || p.Left <= 0 || !(p.Rate > 0)) return 0;
            if (WtAtWar(k) || PtPeaceDays(k) < PtMinPeaceDays) return 0;   // wojna wrocila od wczoraj - P1 (albo nic)
            if (FolkAidLoyalty.GateHolds(st, s)) { gated = true; return 0; }
            return p.Rate > PtCap ? PtCap : p.Rate;
        }

        /// <summary>
        /// M4 - TYLKO ODCZYT (okno mieszkancow, zdanie i linia "PeopleMenu (P1 Savings)"): stawka W, z ktora miasto `st` odda danine pokoju o nastepnym swicie
        /// (plan krolestwa z kroku korony tej doby), 0 - nie odda; gated - prog lojalnosci P1b. Bez licznikow, bez pamieci doby, bez zlota.
        /// </summary>
        internal static double PeaceTributeRatePeek(Settlement st, out bool gated)
        {
            gated = false;
            try
            {
                var s = Settings.Current;
                if (st == null || !st.IsTown || st.OwnerClan == null || !PtOn(s)) return 0;
                var k = st.OwnerClan.Kingdom;
                PtPlanK p;
                if (k == null || !_ptPlan.TryGetValue(k, out p) || p.Day != (int)CampaignTime.Now.ToDays || p.Left <= 0 || !(p.Rate > 0)) return 0;
                if (FolkAidLoyalty.GateHolds(st, s)) { gated = true; return 0; }
                return p.Rate > PtCap ? PtCap : p.Rate;
            }
            catch { gated = false; return 0; }
        }

        /// <summary>M4: budzet krolestwa na dzis (reszta wczorajszego budzetu: chleb korony, najwyzej 0.5% lokaty) - FolkWarAid przycina do niego pobor.</summary>
        private static long PtLeft(Kingdom k)
        {
            PtPlanK p;
            return k != null && _ptPlan.TryGetValue(k, out p) ? Math.Max(0, p.Left) : 0;
        }

        /// <summary>M4: zaplacone `ip` przez osade `st` krolestwa `k` - budzet, liczniki doby, zbior osad (okno mieszkancow, dymek lojalnosci).</summary>
        private static void PtNote(Kingdom k, Settlement st, int ip, double rate)
        {
            PtPlanK p;
            if (_ptPlan.TryGetValue(k, out p)) { p.Left -= ip; if (p.Left < 0) _ptCtlOver++; }   // kontrolka: ponad plan (oczekiwane 0)
            else _ptCtlOver++;
            if (rate > PtCap + 1e-12) _ptCtlRate++;                                                // kontrolka: stawka ponad 0.5% (oczekiwane 0)
            if (WtAtWar(k)) _ptCtlWar++;                                                            // kontrolka: M4 w wojnie (oczekiwane 0)
            PtDayK a;
            if (!_ptK.TryGetValue(k, out a)) { a = new PtDayK(); _ptK[k] = a; }
            a.Taken += ip; _ptTaken += ip;
            int today = (int)CampaignTime.Now.ToDays;
            if (_ptPaidDay != today) { _ptPaid.Clear(); _ptPaidDay = today; }
            if (_ptPaid.Add(st)) { a.Towns++; _ptTowns++; }
        }

        /// <summary>M4: danina pokoju krolestwa pobrana w tej dobie ludu (wiersz krolestwa w linii P1).</summary>
        private static long PtTakenOf(Kingdom k)
        {
            PtDayK a;
            return k != null && _ptK.TryGetValue(k, out a) ? a.Taken : 0;
        }

        /// <summary>Czy lud osady oddal danine pokoju M4 (a nie wojenna P1) w dobie ludu biegnacej w takcie doby gry `gameDay` (doba ludu = gameDay - 1) -
        /// zdanie okna mieszkancow i dymek lojalnosci P1b. Tylko odczyt.</summary>
        internal static bool PeaceTributeFrom(Settlement st, int gameDay)
        {
            return st != null && _ptPaidDay == gameDay && _ptPaid.Contains(st);
        }

        // ------------------------------------------------------------ doba korony: plan na jutro i linia (z WarTaxDaily po linii P1, przed CrownIncome.End)
        private static void PeaceTributeDay()
        {
            var s = Settings.Current;
            if (s == null || Campaign.Current == null) return;
            int today = (int)CampaignTime.Now.ToDays;
            bool on = PtOn(s);
            var rows = new List<KeyValuePair<long, string>>();
            long bread = 0, plan = 0, planYest = 0, breadYest = 0; double lok = 0; int planK = 0, longPeaceK = 0, capK = 0;
            // wczorajszy plan (budzet dzisiejszego poboru) - suma do linii; plan zastepowany nowym
            foreach (var kv in _ptPlan) if (kv.Value.Day == today - 1) { planYest += kv.Value.Plan; breadYest += kv.Value.Bread; }
            var old = new Dictionary<Kingdom, PtPlanK>(_ptPlan);
            _ptPlan.Clear();
            // pamiec sesji (recenzja 14.10): krolestwa z pierwszego kroku korony sesji - znane (laczniki gry); pozniejsze - nowe, pokoj od pierwszej doby
            if (!_ptKnownInit) { foreach (var k0 in Kingdom.All) if (k0 != null) _ptKnown.Add(k0); _ptKnownInit = true; }
            foreach (var k in Kingdom.All)
            {
                try
                {
                    if (k == null || k.IsEliminated || k.StringId == null) continue;
                    bool war = WtAtWar(k);
                    if (war) _ptSince[k] = today;                         // ostatnia doba w wojnie (przed PtPeaceDays - pamiec doby)
                    else if (_ptKnown.Add(k)) _ptSince[k] = today;        // krolestwo powstale w tej sesji
                    double peace = war ? 0 : PtPeaceDays(k);
                    bool longPeace = !war && peace >= PtMinPeaceDays;
                    if (longPeace) longPeaceK++;
                    var kd = CrownIncome.DayOf(k);
                    long b = kd != null ? Math.Max(0, kd.Bread) : 0;
                    double ovW, ovK = TownFolk.AidExcessOf(k, today - 1, out ovW);
                    PtDayK a; _ptK.TryGetValue(k, out a);
                    PtPlanK y; old.TryGetValue(k, out y);
                    double rate = 0; long budget = 0;
                    if (on && longPeace && b > 0 && ovW >= 1)
                    {
                        rate = Math.Min(PtCap, b / ovW);
                        long cap = (long)Math.Ceiling(PtCap * ovW);   // pulap 0.5% lokaty (w gore do zlotowki)
                        budget = Math.Min(b, cap);
                        if (cap < b) capK++;
                        _ptPlan[k] = new PtPlanK { Day = today, Bread = b, Plan = budget, Left = budget, Rate = rate };
                        planK++; plan += budget;
                    }
                    if (longPeace) { bread += b; lok += Math.Max(0, ovW); }
                    bool show = (longPeace && b > 0) || (a != null && a.Taken > 0);   // wiersz: krolestwo z planem na jutro albo z poborem dzis
                    if (!show) continue;
                    var sb1 = new StringBuilder(160);
                    sb1.Append(k.Name != null ? k.Name.ToString() : k.StringId).Append(": ")
                       .Append(war ? "WOJNA" : "pokoj " + (peace >= 50000 ? "bez daty (nigdy w wojnie)" : Math.Floor(peace).ToString("0", WtInv) + " dob"))
                       .Append(", wziete dzis ").Append(a != null ? a.Taken : 0).Append(" z ").Append(a != null ? a.Towns : 0).Append(" miast (budzet ")
                       .Append(y != null && y.Day == today - 1 ? y.Plan : 0).Append(')')
                       .Append(", chleb korony dzis ").Append(b).Append(", lokata W ").Append(Math.Round(Math.Max(0, ovW)).ToString("0", WtInv))
                       .Append(", budzet jutro ").Append(budget).Append(", stawka jutro ").Append((100 * rate).ToString("0.0000", WtInv)).Append('%');
                    rows.Add(new KeyValuePair<long, string>((a != null ? a.Taken : 0) + b, sb1.ToString()));
                }
                catch (Exception e) { WaStumble("PeaceTributeDay(krolestwo)", e); }
            }
            if (!s.LogEnabled) return;
            rows.Sort((x, z) => z.Key.CompareTo(x.Key));
            var txt = new List<string>(); foreach (var r in rows) txt.Add(r.Value);
            var sb = new StringBuilder(1024);
            sb.Append("Danina pokoju (M4): dzien ").Append(today)
              .Append(" | wylacznik Folk Peace Tribute ").Append(s.FolkPeaceTribute ? "tak" : "nie (w pokoju danina 0 - jak przed paczka)")
              .Append(", Folk War Aid ").Append(s.FolkWarAid ? "tak" : "nie (M4 nieczynne)").Append(", Bread For The Poor ").Append(s.BreadForThePoor ? "tak" : "nie (M4 nieczynne)")
              .Append(", 184 / kiesa ludu / 165 ").Append(WtOn ? "tak" : "nie (M4 nieczynne)")
              .Append(" | regula: krolestwo w pokoju >= ").Append(PtMinPeaceDays.ToString("0", WtInv)).Append(" dob; lokata W (kiesa wyrobnikow miast ponad zapas dni) oddaje skarbcowi tyle, ile korona wczoraj wydala na chleb M3 swoich osad, najwyzej ")
              .Append((100 * PtCap).ToString("0.0", WtInv)).Append("% lokaty na dobe (budzet = mniejsza z obu); droga, bramki i kara lojalnosci P1 / P1b")
              .Append(" | dzis wziete ").Append(_ptTaken).Append(" zl z ").Append(_ptTowns).Append(" miast (krolestw ").Append(_ptK.Count).Append(") wobec budzetu ").Append(planYest)
              .Append(" (chleb korony wczoraj ").Append(breadYest).Append(", najwyzej 0.5% lokaty); wstrzymane w oblezeniu ").Append(_ptHeld).Append(", prog lojalnosci P1b ").Append(_ptGate).Append(" miast, przyciete do budzetu ").Append(_ptCut)
              .Append(" | krolestw w pokoju >= ").Append(PtMinPeaceDays.ToString("0", WtInv)).Append(" dob ").Append(longPeaceK).Append(": chleb korony dzis ").Append(bread)
              .Append(", lokata W ").Append(Math.Round(lok).ToString("0", WtInv)).Append(" | budzet na jutro ").Append(plan).Append(" zl w ").Append(planK).Append(" krolestwach (w tym pulap 0.5% lokaty nizszy niz chleb ").Append(capK).Append(')')
              .Append(" | kontrola: ponad plan ").Append(_ptCtlOver).Append(", stawka ponad ").Append((100 * PtCap).ToString("0.0", WtInv)).Append("% ").Append(_ptCtlRate)
              .Append(", w wojnie ").Append(_ptCtlWar).Append(" (oczekiwane 0)")
              .Append(" | na krolestwo: ").Append(txt.Count > 0 ? string.Join(" ; ", txt.ToArray()) : "-").Append('.');
            Log.Info(sb.ToString());
        }
    }
}
