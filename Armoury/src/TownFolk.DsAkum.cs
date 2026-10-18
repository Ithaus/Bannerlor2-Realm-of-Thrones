using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// ds-akumulator (noc 17/18.10; Jeff: "przejrzyj logike ... czy nie ma jakiegos stanu ludzi, ktorzy zarabiaja, ale nie moga wydawac i na koniec wchlona cala kase";
    /// diagnoza docs/DIAGNOZA-FIN80-2026-10-17.md "dochod" i "kiesy"): kiesy ludu miast (klasa W) i podzamczy rosly przy DS F3 o +186 tys./d (fin76/78/80; baza bez DS
    /// +11 tys./d; zapas dni W 46 -> 180), bo po rampie F3 odplywy z oszczednosci ludu stanely (P1, podatek od skarbca ludu, straznik obiegu), a do kies W szly place robot
    /// gminy wsi (7.57 mln w biegu) za "material" bez towaru. Cztery klucze (domyslnie TAK; wylaczone = kod co do monety jak dotad):
    ///  1. DsHoardDue (+ DsHoardDueRate): przy firmach F3 danina pana od skarbca ludu ponad poziom normalny (FolkHoardNormalTimes x zapas dni) - sredniowieczny tallage od
    ///     ruchomosci, w pokoju i w wojnie - zamiast zera (TownFolk.PayHoard). Wliczone jako zaplacone tej doby: dziesiata M9 i danina P1 / M4 tej klasy (juz w `aid`),
    ///     pozyczka rodu z kiesy (N8) i czesc firmy pana z oszczednosci ludu (F_Lf / 364 x rampa; DsTowns.FolkFarmPerDay - podzial na klasy wedlug kiesy ponad zapas dni) -
    ///     ta sama warstwa oszczednosci nigdy dwa razy. Stawka w przejsciu F3 liniowo od dawnej (FolkHoardTaxRate; straznik przy firmach wylaczony) do DsHoardDueRate.
    ///     Podzial jak kiesa2: pan FolkHoardTaxLordShare (glowa rodu, RentToday - ziemia D), reszta kasa osady; korona nic (jej czesc wojenna idzie przez M9).
    ///  2. DsWorksRealMaterial (+ DsWorksWagePerMaterial): roboty gminy wsi robi gmina wlasnymi rekami (bez monety za prace wsi); kiesa wsi placi tylko PRAWDZIWY material
    ///     z polki miasta targowego (BuildFunding.DsWorksBuy - regula materialu robot ludu P2, ramka ksiegi towarow FBuild) i rzemieslnikow miasta najwyzej
    ///     DsWorksWagePerMaterial x material. Zlecenia z kroku 5 doby wsi (DsWorks) wykonane PO budowach pana i ludu tej doby (DaWorksRun z ArmouryBehavior) - wies nie
    ///     zabiera materialu budowom pana. Brak materialu - brak robot, pieniadz zostaje w kiesie wsi (pozyczki ponad skrzynie). Ta sama regula dla budowy septow
    ///     z dziesieciny (TownFolk.Tithe.cs: lud miast, wsie, wsie DS przy wozie): polowa budowy nie jest zdejmowana od razu, tylko zlecona - material prawdziwy albo
    ///     nic, rzemieslnicy najwyzej 3 x material (dawne 75% / 25%); czego nie wydano - zostaje u placacego.
    ///  3. FolkBetterTable: przy koszyku ZC4 (KS) oferta nadwyzki ludu na "lepszy stol" (mieso, ser, maslo, piwo, wino, miod pitny, oliwa, miod) nie jest zerowana przez
    ///     Zc4OfferFactor, o ile indeks kategorii nie przekracza ceny granicznej P4 (FolkLadderPriceCeiling - bazowej, bez zasobnosci: jedzenie biednych nie drozeje przez
    ///     bogatych); ponad nia - jak dotad (1 - lambda). Poza KS sciezka bez zmian (lepszy stol nie byl zerowany).
    ///  4. CrownWindfallToReserve (CrownIncome.cs): jednorazowe "inne" wplywy skarbca (ponad 3 x mediana 28 dob krolestwa) ida do zapasu (rozchodza sie 1/450), nie w renty tej doby.
    /// Opcjonalnie DsHoardDueVillages (DOMYSLNIE NIE - decyzja Jeffa: DS F1 zniosl pobory od oszczednosci wsi): ta sama danina od kiesy wsi ponad FolkHoardNormalTimes x skrzynia,
    /// tylko u wsi, ktore nie znalazly towaru ani materialu (zlecenie robot w kroku 5).
    /// Linia doby "Akumulator kies (ds-akumulator): dzien N" (ArmouryBehavior po liniach DS) z tozsamosciami: pobrano = do panow + do kas osad + do korony (0); z kies wsi =
    /// do kas za material + do ludu (rzemieslnicy); material zaplacony = towar zdjety (zl). Zapis gry: nic nowego (zlecenia nie trzymaja zlota - zapis w trakcie doby
    /// zostawia pieniadz u placacego). Kazde zlecenie / osada we wlasnym try, licznik potkniec, bez globalnego wylacznika (CLAUDE.md 7).
    /// </summary>
    internal static partial class TownFolk
    {
        private const double SeptWagePerMat = 3.0;      // budowa septow: rzemieslnicy najwyzej 3 x material (dawne 75% place / 25% material)
        private const int DaQueueMax = 20000;            // bezpiecznik kolejki zlecen (zlecenie nie trzyma zlota - nadmiar zostaje u placacych)
        private const long WindfallMin = 1000;

        private sealed class DaOrder { public Village Vil; public Folk Payer; public Settlement Mk; public long Amount; public bool Sept; public long W0, W1, W2; }
        private static readonly List<DaOrder> _daQ = new List<DaOrder>();

        // ---- liczniki od ostatniej linii
        private static int _daSeenN, _daCovered, _daTakeN; private static double _daRoom, _daPreAid, _daPreFarm, _daDuePlan;
        private static readonly long[] _daTake = new long[4]; private static long _daLord, _daCoffer, _daCrown;
        private static long _dwQ, _dwMat, _dwWage, _dwNoMat, _dwNoCoin, _dwVilOut, _dwCofIn, _dwFolkIn, _dwRefund, _dwGap; private static int _dwQN, _dwN, _dwNoMatN, _dwNoMktN, _dwCap, _dwDropped;
        private static long _dsQ, _dsMat, _dsWage, _dsNoMat, _dsPayOut, _dsCofIn, _dsFolkIn, _dsGap; private static int _dsQN, _dsN, _dsNoMatN, _dsTownN, _dsVilN;
        private static readonly int[] _dwPcs = new int[8];
        private static long _dvTake, _dvLord, _dvCoffer, _dvHeld; private static int _dvN, _dvSeen; private static double _dvRoom;
        private static double _daBtOpen, _daBtCeil; private static int _daBtOpenN, _daBtCeilN;
        private static long _daTicks; private static int _daSt, _daStSess;
        private static readonly HashSet<string> _daErr = new HashSet<string>();

        private static void DaReset()
        {
            _daQ.Clear(); DaZero(); _daStSess = 0; _daErr.Clear(); _daTiSept = 0; _daBtIds = null;
        }

        private static void DaZero()
        {
            _daSeenN = _daCovered = _daTakeN = 0; _daRoom = _daPreAid = _daPreFarm = _daDuePlan = 0;
            Array.Clear(_daTake, 0, 4); _daLord = _daCoffer = _daCrown = 0;
            _dwQ = _dwMat = _dwWage = _dwNoMat = _dwNoCoin = _dwVilOut = _dwCofIn = _dwFolkIn = _dwRefund = _dwGap = 0; _dwQN = _dwN = _dwNoMatN = _dwNoMktN = _dwCap = _dwDropped = 0;
            _dsQ = _dsMat = _dsWage = _dsNoMat = _dsPayOut = _dsCofIn = _dsFolkIn = _dsGap = 0; _dsQN = _dsN = _dsNoMatN = _dsTownN = _dsVilN = 0;
            Array.Clear(_dwPcs, 0, _dwPcs.Length);
            _dvTake = _dvLord = _dvCoffer = _dvHeld = 0; _dvN = _dvSeen = 0; _dvRoom = 0;
            _daBtOpen = _daBtCeil = 0; _daBtOpenN = _daBtCeilN = 0;
            _daTicks = 0; _daSt = 0;
        }

        private static void DaStumble(string where, Exception e)
        {
            _daSt++; _daStSess++;
            try { if (_daErr.Add(where)) Log.Error("TownFolk.DsAkum." + where, e); } catch { }
        }

        // ============================================================ 1. danina od skarbca ludu przy firmach DS (PayHoard)
        /// <summary>DsHoardDue czynne: klucz i firmy F3 ustalone (DsTowns.FarmsLive).</summary>
        private static bool DaDueOn { get { var s = Settings.Current; return s != null && s.DsHoardDue && DsTowns.FarmsLive; } }

        /// <summary>Stawka doby: w przejsciu F3 liniowo od dawnej stawki (HoardRateNow - przy firmach straznik wylaczony, czyli FolkHoardTaxRate) do DsHoardDueRate.</summary>
        private static double DaRateNow(Settings s)
        {
            double r = DsTowns.NewShare;
            double nr = HdClamp(s.DsHoardDueRate, 0, 0.1);
            return (1 - r) * HoardRateNow(s) + r * nr;
        }

        /// <summary>Czesc firmy pana z oszczednosci ludu tej doby przypadajaca na klase `c` (podzial jak DsFolkRate: wedlug kiesy klasy ponad zapas dni; zamek - podzamcze).</summary>
        private static double DaFarmPre(Folk f, int c)
        {
            try
            {
                double day = DsTowns.FolkFarmPerDay(f.St);
                if (!(day > 0)) return 0;
                if (f.Kind == 1) return c == 0 ? day : 0;
                var s = Settings.Current;
                double rdW = Math.Max(1, Math.Min(60, s != null ? s.TownFolkReserveDays : 10));
                double tot = 0, mine = 0;
                for (int k = 0; k < 3; k++)
                {
                    double rdk = k == 0 ? rdW : k == 1 ? RdR : RdP;
                    double r = Math.Max(0, PurseOf(f, k) - rdk * Math.Max(0, f.Need[k]));
                    tot += r; if (k == c) mine = r;
                }
                return tot > 0 ? day * mine / tot : (c == 0 ? day : 0);
            }
            catch (Exception e) { DaStumble("DaFarmPre", e); return 0; }
        }

        private static void DaNoteSeen(double room, long aid, double farm, double due)
        {
            _daSeenN++; _daRoom += Math.Max(0, room); _daPreAid += Math.Max(0, aid); _daPreFarm += Math.Max(0, farm);
            if (due >= 1) _daDuePlan += due; else if (aid > 0 || farm > 0) _daCovered++;
        }

        private static void DaNoteTake(int slot, long x, long lp, long tp, long cp)
        {
            if (x <= 0) return;
            if (slot >= 0 && slot < 4) _daTake[slot] += x;
            _daLord += Math.Max(0, lp); _daCoffer += Math.Max(0, tp); _daCrown += Math.Max(0, cp); _daTakeN++;
        }

        /// <summary>Dopisek linii kiesa2 (przed "czas"): klucz wylaczony albo bez firm - pusty (linia jak dotad).</summary>
        private static string DaHoardNote()
        {
            var s = Settings.Current;
            if (s == null || !DaDueOn) return "";
            return " | danina od skarbca ludu przy firmach DS (Ds Hoard Due) tak: stawka doby " + (100 * DaRateNow(s)).ToString("0.00", Inv) + "%/d (docelowo "
                   + (100 * HdClamp(s.DsHoardDueRate, 0, 0.1)).ToString("0.00", Inv) + "%), wliczone jako zaplacone: dziesiata M9 i danina tej doby, czesc firmy z oszczednosci ludu"
                   + " - liczby w linii 'Akumulator kies (ds-akumulator)'";
        }

        // ============================================================ 2. roboty gminy i septy z prawdziwym materialem (zlecenia po budowach)
        /// <summary>DsWorksRealMaterial czynne (kiesa ludu czynna).</summary>
        internal static bool DaWorksOn { get { var s = Settings.Current; return s != null && s.DsWorksRealMaterial && Live; } }

        /// <summary>Zlecenie robot: wies (kiesa wsi) albo lud miasta (septy z dziesieciny ludu: wagi klas w0..w2) placi za material z polki `mk` i rzemieslnikow - wykonanie
        /// w DaWorksRun. Zloto zostaje u placacego do wykonania. Wies z robotami gminy (nie septy) - takze danina od skarbca wsi (DsHoardDueVillages, domyslnie nie).</summary>
        private static void DaWorksQueue(Village vil, Folk payer, Settlement mk, long amount, bool sept, long w0 = 0, long w1 = 0, long w2 = 0)
        {
            try
            {
                if (amount <= 0 || mk == null || (vil == null && payer == null)) return;
                if (_daQ.Count >= DaQueueMax) { if (sept) _dsNoMat += amount; else _dwNoMat += amount; _dwDropped++; return; }
                _daQ.Add(new DaOrder { Vil = vil, Payer = payer, Mk = mk, Amount = amount, Sept = sept, W0 = w0, W1 = w1, W2 = w2 });
                if (sept) { _dsQ += amount; _dsQN++; } else { _dwQ += amount; _dwQN++; }
                if (!sept && vil != null) DaVillageDue(vil, amount);
            }
            catch (Exception e) { DaStumble("DaWorksQueue", e); }
        }

        private static long _daTiSept;   // budowa septow zlecona od ostatniej linii "Dziesiecina (E5/C3)" (lud miast w dobie ludu, wsie w dobie i przy wozie)

        /// <summary>Dopisek linii "Dziesiecina (E5/C3)": budowa septow zlecona od poprzedniej linii (klucz wylaczony i 0 - pusty, linia jak dotad); zeruje.</summary>
        private static string DaTitheNote()
        {
            long x = _daTiSept; _daTiSept = 0;
            if (!DaWorksOn && x <= 0) return "";
            return " | budowa septow z prawdziwym materialem (Ds Works Real Material): zlecone " + x + " (od poprzedniej linii; w 'budowa septow' wyzej tylko dawna droga - wykonanie po budowach w linii 'Akumulator kies')";
        }

        /// <summary>Tithe.cs: budowa septu z dziesieciny ludu miasta (czesc budowy wrocila juz do kies klas) - zlecenie ludu tej osady na polce wlasnego miasta.</summary>
        private static void DaSeptTown(Folk f, long build, long t0, long t1, long t2)
        {
            if (f == null || f.St == null || build <= 0) return;
            DaWorksQueue(null, f, f.St, build, true, Math.Max(0, t0), Math.Max(0, t1), Math.Max(0, t2));
        }

        /// <summary>Tithe.cs: budowa septu z dziesieciny wsi (kiesa wsi) - zlecenie na polce miasta targowego.</summary>
        private static void DaSeptVillage(Village vil, Settlement mk, long build)
        {
            if (vil == null || mk == null || build <= 0) return;
            DaWorksQueue(vil, null, mk, build, true);
        }

        /// <summary>
        /// ArmouryBehavior (po BuildFunding.Daily - budowy pana i robot ludu, przed migawka MBuild): wykonanie zlecen robot gminy i septow. Na zlecenie: budzet = najwyzej
        /// kiesa placacego; material = budzet / (1 + k) z polki targu (BuildFunding.DsWorksBuy, ramka FBuild z osada targu); rzemieslnicy = najwyzej k x material (k:
        /// DsWorksWagePerMaterial dla robot, 3 dla septow); placacy -> kasa targu (material, sprzedaz kupcow MixBuild - podstawa robocizny) i lud targu (rzemieslnicy, Earn
        /// KBuildLabour; lud miasta za septy - przelew wewnatrz ludu). Brak materialu - nic nie schodzi. Nie rzuca.
        /// </summary>
        internal static void DaWorksRun()
        {
            if (_daQ.Count == 0) return;
            long t0 = Stopwatch.GetTimestamp();
            var q = new List<DaOrder>(_daQ);
            _daQ.Clear();
            try
            {
                var s = Settings.Current;
                if (s == null || !DaWorksOn || OffMain()) { foreach (var o in q) { if (o.Sept) _dsNoMat += o.Amount; else _dwNoMat += o.Amount; } _dwDropped += q.Count; return; }
                double kw = Math.Max(0, Math.Min(3.0, (double)s.DsWorksWagePerMaterial));
                foreach (var o in q)
                {
                    try { DaRunOne(o, o.Sept ? SeptWagePerMat : kw); }
                    catch (Exception e) { DaStumble("DaWorksRun(zlecenie)", e); }
                }
            }
            catch (Exception e) { DaStumble("DaWorksRun", e); }
            finally { _daTicks += Stopwatch.GetTimestamp() - t0; }
        }

        private static void DaRunOne(DaOrder o, double k)
        {
            var mk = o.Mk;
            Folk mf = null;
            if (mk != null && mk.Town != null) _f.TryGetValue(mk, out mf);
            if (mf == null || mf.Kind > 1 || (mf.Kind == 1 && !CastleLive)) { if (o.Sept) _dsNoMat += o.Amount; else { _dwNoMat += o.Amount; _dwNoMktN++; } return; }
            long avail = o.Vil != null ? Math.Max(0, o.Vil.Gold) : (o.Payer != null ? Math.Max(0, o.Payer.KW) + Math.Max(0, o.Payer.KR) + Math.Max(0, o.Payer.KP) : 0);
            long budget = Math.Min(o.Amount, avail);
            if (budget < 2) { if (o.Sept) _dsNoMat += o.Amount; else _dwNoCoin += o.Amount; return; }
            double matBudget = Math.Floor(budget / (1.0 + k));
            int spent = 0, cap = 0;
            if (matBudget >= 1)
            {
                var pcs = new int[Math.Max(BuildFunding.DsWorksMatCount, 1)];
                var gf = GoodsLedger.Begin(GoodsLedger.FBuild, mk);   // ksiega towarow: material budow na rynek osady targu
                try { spent = BuildFunding.DsWorksBuy(mk, matBudget, pcs, out cap); }
                finally { GoodsLedger.End(gf); }
                for (int i = 0; i < pcs.Length && i < _dwPcs.Length; i++) _dwPcs[i] += pcs[i];
            }
            _dwCap += cap;
            if (spent <= 0) { if (o.Sept) { _dsNoMat += o.Amount; _dsNoMatN++; } else { _dwNoMat += o.Amount; _dwNoMatN++; } return; }
            long wage = Math.Max(0, Math.Min((long)Math.Floor(k * spent), budget - spent));
            if (o.Vil != null) DaPayVillage(o, mk, mf, spent, wage);
            else DaPayTown(o, mk, mf, spent, wage);
        }

        /// <summary>Wies placi: kiesa wsi -> kasa targu (material) i lud targu (rzemieslnicy). Towar juz zdjety - material zaplacony zawsze w calosci, ile kiesa ma.</summary>
        private static void DaPayVillage(DaOrder o, Settlement mk, Folk mf, long mat, long wage)
        {
            var vil = o.Vil;
            int had = vil.Gold;
            vil.ChangeGold(-(int)Math.Min(int.MaxValue, mat + wage));
            long got = Math.Max(0, (long)had - vil.Gold);
            long matPaid = Math.Min(got, mat), wagePaid = got - matPaid;
            if (matPaid < mat) _dwGap += mat - matPaid;                     // kontrola: towar zdjety bez zaplaty (oczekiwane 0 - kiesa sprawdzona przed zakupem)
            long cin = 0, fin = 0;
            if (matPaid > 0)
            {
                int c0 = mk.Town.Gold;
                mk.Town.ChangeGold((int)matPaid);
                cin = (long)mk.Town.Gold - c0;
                mf.Sale[MixBuild] += matPaid;                               // sprzedaz kupcow targu - podstawa robocizny (jak material budow pana)
            }
            if (wagePaid > 0)
            {
                fin = Earn(mk, (int)Math.Min(int.MaxValue, wagePaid), KBuildLabour);   // kiesa wsi -> lud targu (zarobek okna, podatek jutro)
                if (fin < wagePaid) { vil.ChangeGold((int)(wagePaid - fin)); _dwRefund += wagePaid - fin; got -= wagePaid - fin; wagePaid = fin; }   // lud nie przyjal - zloto wraca do wsi
            }
            if (o.Sept) { _dsMat += matPaid; _dsWage += wagePaid; _dsN++; _dsVilN++; _dsPayOut += got; _dsCofIn += cin; _dsFolkIn += fin; }
            else
            {
                _dwMat += matPaid; _dwWage += wagePaid; _dwN++; _dwVilOut += got; _dwCofIn += cin; _dwFolkIn += fin;
                try { DsVillage.NoteWorksLate(matPaid, wagePaid); } catch (Exception e) { DaStumble("NoteWorksLate", e); }
            }
        }

        /// <summary>Lud miasta placi za septy z dziesieciny: kiesy klas (wagi dziesieciny) -> kasa miasta (material; ksiega kies _ctlOut) i rzemieslnicy tego ludu (przelew wewnatrz).</summary>
        private static void DaPayTown(DaOrder o, Settlement mk, Folk mf, long mat, long wage)
        {
            var f = o.Payer;
            long want = mat + wage, got = 0;
            long wt = o.W0 + o.W1 + o.W2;
            long[] w = { o.W0, o.W1, o.W2 };
            for (int c = 0; c < 3 && got < want; c++)
            {
                long share = wt > 0 ? (long)Math.Floor((double)want * w[c] / wt) : (c == 0 ? want : 0);
                long x = Math.Min(Math.Min(share, want - got), Math.Max(0, PurseOf(f, c)));
                if (x <= 0) continue;
                AddPurse(f, c, -x); got += x;
            }
            for (int c = 0; c < 3 && got < want; c++)                        // reszta z zaokraglen i z klas z pustsza kiesa - z kazdej, ktora ma
            {
                long x = Math.Min(want - got, Math.Max(0, PurseOf(f, c)));
                if (x <= 0) continue;
                AddPurse(f, c, -x); got += x;
            }
            long matPaid = Math.Min(got, mat), wagePaid = got - matPaid;
            if (matPaid < mat) _dsGap += mat - matPaid;
            long cin = 0;
            if (matPaid > 0)
            {
                int c0 = mk.Town.Gold;
                mk.Town.ChangeGold((int)matPaid);
                cin = (long)mk.Town.Gold - c0;
                _ctlOut += matPaid; f.LDayOut += matPaid;                   // ksiega kies: wydatek ludu (kontrola zamknieta)
                mf.Sale[MixBuild] += matPaid;
            }
            if (wagePaid > 0) { Credit(mf, wagePaid, KBuildLabour); mf.LDayIn -= wagePaid; }   // rzemieslnicy septu - zarobek, przelew wewnatrz ludu (jak dawna dziesiecina)
            _dsMat += matPaid; _dsWage += wagePaid; _dsN++; _dsTownN++; _dsPayOut += got; _dsCofIn += cin; _dsFolkIn += wagePaid;
        }

        // ============================================================ 2b. danina od skarbca wsi (DsHoardDueVillages - domyslnie NIE)
        /// <summary>Wies DS z robotami gminy (towaru ani materialu w kroku 5 zabraklo): stawka DsHoardDueRate od kiesy ponad FolkHoardNormalTimes x skrzynia (po odjeciu zlecenia
        /// robot tej doby), z pozyczka z tej kiesy wliczona (N8). Odbiorcy jak podatek od nadwyzki wsi: glowa rodu pana (RentToday) i kasa osady macierzystej; rabunek,
        /// spladrowanie, oblezenie - nic. Nie rzuca.</summary>
        private static void DaVillageDue(Village vil, long queued)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.DsHoardDueVillages || vil == null || vil.Settlement == null) return;
                var st = vil.Settlement;
                double chest = DsVillage.ChestToday(st);
                if (!(chest > 0)) return;
                _dvSeen++;
                int gold = vil.Gold;
                double room = gold - Math.Max(0, queued) - HoardTimes(s) * chest;
                if (!(room >= 1)) return;
                _dvRoom += room;
                var bound = vil.Bound;
                if (vil.VillageState != Village.VillageStates.Normal || bound == null || bound.Town == null) return;
                long credit = LnCreditFor(st);
                double due = HdClamp(s.DsHoardDueRate, 0, 0.1) * (room + credit) - credit;
                long x = due >= 1 ? (long)Math.Floor(Math.Min(due, room)) : 0;
                x = Math.Min(x, (long)gold);
                if (x <= 0) return;
                if (BesiegedPay.Holds(bound, BesiegedPay.KFolkHoard, x, true)) { _dvHeld += x; return; }
                var clan = st.OwnerClan;
                var lord = clan != null ? clan.Leader : null;
                bool lordOk = lord != null && lord.IsAlive;
                int had = vil.Gold;
                vil.ChangeGold(-(int)x);
                long got = had - vil.Gold;
                if (got <= 0) return;
                long lp = lordOk ? (long)Math.Round(got * HoardLordShare(s), MidpointRounding.AwayFromZero) : 0;
                if (lp > got) lp = got;
                long tp = got - lp;
                if (lp > 0)
                {
                    try { lord.ChangeHeroGold((int)lp); }
                    catch (Exception e) { vil.ChangeGold((int)got); DaStumble("DaVillageDue(pan)", e); return; }
                }
                if (tp > 0)
                {
                    try { bound.Town.ChangeGold((int)tp); }
                    catch (Exception e) { vil.ChangeGold((int)tp); got -= tp; tp = 0; DaStumble("DaVillageDue(kasa)", e); }
                }
                if (got <= 0) return;
                _dvTake += got; _dvLord += lp; _dvCoffer += tp; _dvN++;
                LnNoteTaxed(st, got);                                       // N8: podatek tej doby z kiesy wsi (pozyczka liczy nadwyzke sprzed niego)
                try { StartCalibration.KsVilOut(st, got); } catch { }      // KS-R3 P2: wydatek wsi spoza koszyka (jak podatek od nadwyzki wsi)
                if (lp > 0)
                {
                    int r0; PopulationLaw.RentToday.TryGetValue(clan, out r0);
                    PopulationLaw.RentToday[clan] = (int)Math.Min(int.MaxValue, (long)r0 + lp);   // dochod ziemi D rodu
                    CirculationWindows.NoteHeroGold(lord, lp);
                }
            }
            catch (Exception e) { DaStumble("DaVillageDue", e); }
        }

        // ============================================================ 3. lepszy stol z oszczednosci (Zc4OfferFactor przy KS)
        private static HashSet<string> _daBtIds;

        private static bool DaTableCat(ItemCategory c)
        {
            if (c == null || c.StringId == null) return false;
            if (_daBtIds == null) { var h = new HashSet<string>(); foreach (var id in LxIds[LxTable]) h.Add(id); _daBtIds = h; }
            return _daBtIds.Contains(c.StringId.ToLowerInvariant());
        }

        /// <summary>
        /// LadderOffer / AddOffer: zloto oferty nadwyzki na kategorie `c` (baseX = oferta x udzial). Poza ZC4 i przy kluczu wylaczonym - baseX x Zc4OfferFactor co do bitu
        /// jak dotad. FolkBetterTable przy ZC4: "lepszy stol" miasta (oferta istnieje tylko z oszczednosci ponad zapas dni) - pelna oferta, o ile indeks kategorii nie
        /// przekracza bazowej ceny granicznej P4 (`ceil`); ponad nia - dawny udzial (1 - lambda), reszta w kiesie.
        /// </summary>
        private static double DaOffer(Folk f, Town town, ItemCategory c, double ceil, double baseX)
        {
            double z = StartCalibration.Zc4OfferFactor(town, c);
            if (!(z < 1.0)) return baseX * z;
            var s = Settings.Current;
            if (s == null || !s.FolkBetterTable || f == null || f.Kind != 0 || !(baseX > 0) || !DaTableCat(c)) return baseX * z;
            try
            {
                double ix = town.GetItemCategoryPriceIndex(c);
                if (double.IsNaN(ix) || ix > ceil) { _daBtCeil += baseX * (1 - z); _daBtCeilN++; return baseX * z; }
                _daBtOpen += baseX * (1 - z); _daBtOpenN++;
                return baseX;
            }
            catch (Exception e) { DaStumble("DaOffer", e); return baseX * z; }
        }

        // ============================================================ linia doby
        /// <summary>ArmouryBehavior (koniec bloku doby, po liniach DS): linia "Akumulator kies (ds-akumulator): dzien N" - liczniki od poprzedniej linii; zeruje.
        /// Bez kluczy i bez kiesy ludu - bez linii.</summary>
        internal static void DaDaily()
        {
            try
            {
                var s = Settings.Current;
                if (s == null || Campaign.Current == null || !s.LogEnabled) { DaZero(); return; }
                bool any = s.DsHoardDue || s.DsWorksRealMaterial || s.FolkBetterTable || s.CrownWindfallToReserve || s.DsHoardDueVillages;
                if (!any || !Live) { DaZero(); return; }
                int day = (int)CampaignTime.Now.ToDays - 1;
                long w = 0, r = 0, p = 0, cs = 0;
                for (int i = 0; i < _fk.Count; i++) { var f = _fk[i]; if (f.Kind == 0) { w += f.KW; r += f.KR; p += f.KP; } else if (f.Kind == 1) cs += f.KW; }
                long vil = 0; foreach (var v in Village.All) if (v != null) vil += Math.Max(0, v.Gold);
                int vn; double mg, mc, mr, sg, sc;
                bool med = DsVillage.AkMedians(out vn, out mg, out mc, out mr, out sg, out sc);
                long take = _daTake[0] + _daTake[1] + _daTake[2] + _daTake[3];
                var inv = Inv;
                var sb = new StringBuilder(2200);
                sb.Append("Akumulator kies (ds-akumulator): dzien ").Append(day)
                  .Append(" | danina od skarbca ludu (Ds Hoard Due) ").Append(s.DsHoardDue ? "tak" : "nie").Append(", firmy F3 ").Append(DsTowns.FarmsLive ? "tak" : "nie")
                  .Append(", stawka ").Append((100 * (DsTowns.FarmsLive ? DaRateNow(s) : 0)).ToString("0.00", inv)).Append("%/d (docelowo ").Append((100 * HdClamp(s.DsHoardDueRate, 0, 0.1)).ToString("0.00", inv))
                  .Append("%, rampa F3 ").Append(DsTowns.NewShare.ToString("0.000", inv)).Append(") nadwyzki ponad poziom normalny ").Append(HoardTimes(s).ToString("0.0#", inv)).Append(" x zapas dni")
                  .Append(": klaso-osad ").Append(_daSeenN).Append(", ponad poziom normalny ").Append(N0(_daRoom)).Append(", wliczone jako zaplacone: dziesiata M9 i danina tej doby ").Append(N0(_daPreAid))
                  .Append(" + czesc firmy z oszczednosci ludu ").Append(N0(_daPreFarm)).Append(" (pokryly stawke ").Append(_daCovered).Append(")")
                  .Append(" | pobrano ").Append(take).Append(" (W ").Append(_daTake[0]).Append(" / R ").Append(_daTake[1]).Append(" / P ").Append(_daTake[2]).Append(" / podzamcza ").Append(_daTake[3])
                  .Append("; klaso-osad ").Append(_daTakeN).Append(") = do panow ").Append(_daLord).Append(" + do kas osad ").Append(_daCoffer).Append(" + do korony ").Append(_daCrown)
                  .Append("; kontrola danina: pobrano - panom - kasom - koronie = ").Append(take - _daLord - _daCoffer - _daCrown)
                  .Append(" | roboty gminy z prawdziwym materialem (Ds Works Real Material) ").Append(s.DsWorksRealMaterial ? "tak" : "nie").Append(", rzemieslnicy najwyzej ")
                  .Append(Math.Max(0, Math.Min(3.0, (double)s.DsWorksWagePerMaterial)).ToString("0.00", inv)).Append(" x material: zlecone ").Append(_dwQ).Append(" (").Append(_dwQN).Append(" wsi)")
                  .Append(", material z polek ").Append(_dwMat).Append(", rzemieslnicy miast ").Append(_dwWage).Append(", wsi z robota ").Append(_dwN)
                  .Append(", bez materialu (zostalo w kiesach wsi) ").Append(_dwNoMat).Append(" (").Append(_dwNoMatN).Append(" wsi; bez kiesy ludu targu ").Append(_dwNoMktN).Append(", bez zlota ").Append(_dwNoCoin)
                  .Append("), sufit ceny - material pominiety ").Append(_dwCap).Append(", zlecen porzuconych (zostaly u placacych) ").Append(_dwDropped)
                  .Append("; sztuki ").Append(BuildFunding.DsWorksMatNames).Append(" ").Append(PcsText())
                  .Append("; kontrola robot: z kies wsi - (do kas + do ludu) = ").Append(_dwVilOut - _dwCofIn - _dwFolkIn).Append(", material zaplacony - towar zdjety = ").Append(-_dwGap)
                  .Append(", zwrot do wsi (lud nie przyjal) ").Append(_dwRefund)
                  .Append(" | septy z dziesieciny (ta sama regula): zlecone ").Append(_dsQ).Append(" (").Append(_dsQN).Append("), material ").Append(_dsMat).Append(", rzemieslnicy ").Append(_dsWage)
                  .Append(" (zlecen wykonanych ").Append(_dsN).Append(": lud miast ").Append(_dsTownN).Append(", wsie ").Append(_dsVilN).Append("), bez materialu (zostalo u placacych) ").Append(_dsNoMat)
                  .Append("; kontrola septow: od placacych - (do kas + do ludu) = ").Append(_dsPayOut - _dsCofIn - _dsFolkIn).Append(", material zaplacony - towar zdjety = ").Append(-_dsGap)
                  .Append(" | danina od skarbca wsi (Ds Hoard Due Villages) ").Append(s.DsHoardDueVillages ? "tak" : "nie").Append(": wsi z robota gminy ").Append(_dvSeen)
                  .Append(", ponad poziom normalny (").Append(HoardTimes(s).ToString("0.0#", inv)).Append(" x skrzynia) ").Append(N0(_dvRoom)).Append(", pobrano ").Append(_dvTake)
                  .Append(" (").Append(_dvN).Append(" wsi) = do panow ").Append(_dvLord).Append(" + do kas osad ").Append(_dvCoffer).Append(", wstrzymane w oblezeniu ").Append(_dvHeld)
                  .Append("; kontrola: pobrano - panom - kasom = ").Append(_dvTake - _dvLord - _dvCoffer)
                  .Append(" | lepszy stol z oszczednosci (Folk Better Table) ").Append(s.FolkBetterTable ? "tak" : "nie").Append(": oferta otwarta ").Append(N0(_daBtOpen)).Append(" (").Append(_daBtOpenN)
                  .Append("), ponad cena graniczna P4 (zostalo w kiesie) ").Append(N0(_daBtCeil)).Append(" (").Append(_daBtCeilN).Append(")")
                  .Append(" | jednorazowe 'inne' korony do zapasu (Crown Windfall To Reserve) ").Append(s.CrownWindfallToReserve ? "tak" : "nie").Append(": ")
                  .Append(CrownIncome.LastWindfall).Append(" (").Append(CrownIncome.LastWindfallN).Append(" krolestw)")
                  .Append(" | kiesy: ludu miast W ").Append(w).Append(" / R ").Append(r).Append(" / P ").Append(p).Append(", podzamcza ").Append(cs).Append(", wsi ").Append(vil);
                if (med) sb.Append(" (wsie DS ").Append(vn).Append(": mediana kiesy ").Append(N0(mg)).Append(", mediana skrzyni ").Append(N0(mc)).Append(", mediana kiesa / skrzynia ").Append(F2(mr))
                             .Append(", kiesy ").Append(N0(sg)).Append(", skrzynie ").Append(N0(sc)).Append(")");
                else sb.Append(" (wsie DS: brak doby kroku 4)");
                sb.Append(" | czas ").Append(Ms(_daTicks)).Append(" ms | potkniecia ").Append(_daSt).Append(" (sesja ").Append(_daStSess).Append(").");
                Log.Info(sb.ToString());
            }
            catch (Exception e) { DaStumble("DaDaily", e); }
            finally { DaZero(); }
        }

        private static string PcsText()
        {
            int n = Math.Min(_dwPcs.Length, Math.Max(1, BuildFunding.DsWorksMatCount));
            var a = new string[n];
            for (int i = 0; i < n; i++) a[i] = _dwPcs[i].ToString(Inv);
            return string.Join("/", a);
        }
    }
}
