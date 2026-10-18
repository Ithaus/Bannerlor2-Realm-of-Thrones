using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// ZC7 - WOJNA I BLIZNA (projekt docs/PROJEKT-ZYCIE-CODZIENNE-2026-10-16.md rozdz. 11 "Wojna niszczy dostatek" i rozdz. 14 "ZC7"; klucz DailyLifeWarScar z DailyLifeEstates).
    /// Jeff 16.10: "wojny niszcza dobrobyt". Wiekszosc dziala juz dzis (najazd: zabici i uchodzcy, spichlerz spalony, kiesa wsi do napastnika, plon x udzial ludzi
    /// w domu; zlupienie: kiesy W/R/P i kasa kupcow zabrane, zabici BK w ksiedze). ZC7 dodaje BLIZNE w stanach i w Dostatku:
    ///  - NAJAZD (Devastation.Hit - rabunek, zerowanie armii ScorchedEarth, rabunek osada po osadzie DistrictRaid; sluchacz NoteStrike - licznik pod zamkiem;
    ///    rozliczenie o swicie po ludziach doby i przed dochodem doby, a przed zapisem - z Export/ExportEstates): F = 1 - iloczyn (1 - trafieni / ludzie w domu przed
    ///    uderzeniem) z uderzen od ostatniego rozliczenia.
    ///    (1) trafieni traca dochod od razu: srednia biezaca dochodu stanow wolnych wsi (chlopi, rzemieslnicy, kupcy, szlachta - takze majatki BK) x (1 - F);
    ///    (2) uchodzcy okregu poza domem (PeopleBook "u:") licza sie w mianowniku Dostatku stanow wolnych BEZ dochodu (WarPeople, Rec.AwayE) - dotad Dostatek ludzi
    ///        w domu ROSL po najezdzie (ten sam dochod na mniej glow);
    ///    (3) rzemieslnicy wsi -20% x F do chlopow (stracili warsztaty; granica 0.5 x pasma) - dlug blizny; srednia dochodu rzemieslnikow x (1 - zrujnowani).
    ///    Powrot Dostatku: z powrotem uchodzcow (E4/5: po 30 dobach spokoju w regionie, wszyscy w rok) i z plonem ludzi w domu (srednia 28 dob).
    ///  - ZLUPIENIE miasta (postfiks OnSiegeAftermathApplied - CitySack.People.cs ZcScarPost; tylko zdobyte, grabiez albo spustoszenie): od razu 10% (spustoszenie 20%)
    ///    kupcow do rzemieslnikow i 10% (20%) rzemieslnikow do biedoty, oba od stanu przed zlupieniem, w granicach pasm; oszczednosci z ludzmi (P -> R, R -> W -
    ///    TownFolk.ZcCarry, suma kies bez zmian); srednia dochodu kupcow i rzemieslnikow x (1 - zrujnowani) - zrujnowani traca dochod, reszta stanu zostaje przy swoim
    ///    Dostatku; dlug blizny; OKNO RUINY 30 dob: spadki ZC6 (rzemieslnicy, kupcy, szlachta) x4 i bez czekania 4 przegladow. Laska, zamek (ludzie w jego wsiach),
    ///    bitwa bez zdobycia - nic.
    ///  - POWROT (przeglad tygodniowy ZC6, DailyLife.Mobility.cs): po 30 dobach bez zdarzenia wojny w osadzie (miasto bez oblezenia, wies w stanie zwyklym)
    ///    zrujnowani wracaja do zawodu 25% dlugu na rok, gdy stan, do ktorego wracaja, ma Dostatek co najmniej progu spadku (rzemieslnicy 1.3, kupcy 2.5) - w limicie
    ///    0.2% ludzi osady na tydzien ZC6, z oszczednosciami (W -> R, R -> P). Reszte drogi w gore - zwykly awans ZC6.
    /// Stan w zapisie (arm_estates v3, pole "blizna"): epizod (ostatnie zdarzenie, poczatek po 30 dobach spokoju, rodzaj, Dostatek przed i najnizszy w setnych,
    /// udzial trafionych) i dlug blizny w milionowych. Uderzenia czekajace na swit - pamiec sesji (rozliczane takze przy zapisie). Komunikaty gracza (po angielsku):
    /// najazd na wies rodu gracza albo najazd gracza (o swicie, z Dostatkiem przed i po), zlupienie miasta z udzialem gracza albo jego rodu. Linia "Blizna wojny (ZC7):
    /// dzien N" w dobie linii (liczniki od ostatniej linii), linia zdarzenia przy kazdym zlupieniu. Zadnego zlota z niczego - tylko przelewy kies ludu w osadzie.
    /// Wyjatki liczone (potkniecia), bez wylacznikow. Klucz wylaczony - nic (Dostatek jak ZC6, uderzenia nie liczone, dlug stoi w zapisie).
    /// </summary>
    internal static partial class DailyLife
    {
        private const string PW = "Blizna wojny (ZC7): ";
        private const double ScarRaidCraft = 0.20;                       // rzemieslnicy wsi -20% x F do chlopow (projekt rozdz. 11)
        private const double ScarPillage = 0.10, ScarDevastate = 0.20;   // ruina przy zlupieniu (projekt: 10-20%): grabiez / spustoszenie
        private const int RuinDays = 30;                                 // okno ruiny po zlupieniu: spadki x4
        private const double RuinDown = 4.0;
        private const int WarQuiet = 30;                                 // pokoj: 30 dob bez zdarzenia (jak RefugeeReturnQuietDays) - powrot blizny i nowy epizod
        private const double RScarBack = 0.25;                           // powrot zrujnowanych do zawodu: 25% dlugu na rok w pokoju
        private const int WarShowDays = 364;                             // widok: epizod do roku od ostatniego zdarzenia
        private const int SkRaid = 1, SkPillage = 2, SkDevastate = 3;
        private const int WarListN = 20, WarEvKeep = 400;
        private const int WarForget = 728;                              // epizod bez zdarzenia od 2 lat - z ksiegi razem z reszta dlugu (zapis nie rosnie bez konca)
        private const int DebtFloorPpm = 50;                            // dlug blizny ponizej 50 milionowych albo ponizej 1 czlowieka - odpisany (rozpad wykladniczy nie konczy sie)
        private const int WarSeedHold = 28;                             // 28 dob od wlaczenia ZC7: liczniki spadku ZC6 wsi stoja (uchodzcy z dawnych wojn wchodza do mianownika naraz)
        private const double MsgMinF = 0.001;                           // komunikat gracza od 0.1% ludzi w domu trafionych (albo zrujnowanych rzemieslnikow)
        private static readonly string[] SkName = { "-", "najazd", "grabiez", "spustoszenie" };

        internal static bool WarOn { get { var s = Settings.Current; return EstatesOn && s.DailyLifeWarScar; } }

        // ------------------------------------------------------------ uderzenia czekajace na swit (pamiec sesji; rozliczane o swicie albo przy zapisie)
        private static readonly object StrikeGate = new object();
        private static Dictionary<Settlement, double[]> _strike = new Dictionary<Settlement, double[]>();   // wies -> [zostalo w domu (iloczyn), trafieni, zabici, gracz napastnikiem, uderzen]
        private static int _strikeFails;
        private static Exception _strikeErr;

        // ------------------------------------------------------------ liczniki od ostatniej linii
        private static int _wDays, _wRaidV, _wStrikes, _wCut, _wBandCut, _wNoBook, _wEpisodes, _wMsg, _wStum, _wBackNoD, _wNoPurse, _wSaveFlush;
        private static int _wSack, _wSackPill, _wSackDev, _wSackMercy, _wSackCastle, _wSackNoCap, _wSackNoBook, _wSackOff;
        private static double _wStruck, _wDead, _wCraftV, _wSackM, _wSackC, _wRuinDown, _wBackV, _wBackC, _wBackM, _wFMax;
        private static long _wP2R, _wR2W, _wCtl, _wBW2R, _wBR2P;
        private static readonly List<double> _wF = new List<double>(64);
        private static readonly List<KeyValuePair<double, string>> _wEv = new List<KeyValuePair<double, string>>(64);   // najazdy od ostatniej linii (F, opis)
        // doba (stan dzisiejszy): uchodzcy w mianowniku Dostatku
        private static int _wAwayV; private static double _wAwayP, _wAwayHome;
        private static int _eLoadBadScar; private static string _eLoadVer;
        private static int _warSeedDay = -1;                            // doba wlaczenia ZC7 w kampanii (zapis v4; -1 - klucz wylaczony / jeszcze nie)
        private static int _wOffN, _wForgetN, _wHoldV, _wSackLate; private static double _wOffP;
        private static readonly double[] _wBand = new double[6];
        private static readonly long[] _wPurse = new long[3];

        private sealed class WarMsg { public Rec R; public double Struck, Craft, D0; public bool Player, Mine; }
        private static readonly List<WarMsg> _wMsgQ = new List<WarMsg>();

        /// <summary>Z ResetEstates (konstruktor ArmouryBehavior): uderzenia czekajace i liczniki od nowa (epizody i dlug - zapis, ImportEstates).</summary>
        private static void ResetWar()
        {
            lock (StrikeGate) { _strike = new Dictionary<Settlement, double[]>(); }
            Interlocked.Exchange(ref _strikeFails, 0); Interlocked.Exchange(ref _strikeErr, null);
            _warSeedDay = -1;                                           // zapis v4 wczytuje ImportEstates po konstruktorze
            ZeroWarLine();
            _wMsgQ.Clear();
            _wAwayV = 0; _wAwayP = _wAwayHome = 0;
        }

        private static void ZeroWarLine()
        {
            _wDays = _wRaidV = _wStrikes = _wCut = _wBandCut = _wNoBook = _wEpisodes = _wMsg = _wStum = _wBackNoD = _wNoPurse = _wSaveFlush = 0;
            _wSack = _wSackPill = _wSackDev = _wSackMercy = _wSackCastle = _wSackNoCap = _wSackNoBook = _wSackOff = 0;
            _wStruck = _wDead = _wCraftV = _wSackM = _wSackC = _wRuinDown = _wBackV = _wBackC = _wBackM = _wFMax = 0;
            _wP2R = _wR2W = _wCtl = _wBW2R = _wBR2P = 0;
            _wOffN = _wForgetN = _wHoldV = _wSackLate = 0; _wOffP = 0;
            _wF.Clear(); _wEv.Clear();
        }

        // ------------------------------------------------------------ sluchacz uderzen (Devastation.Hit)
        /// <summary>Devastation.Hit (rabunek, zerowanie armii, rabunek osada po osadzie): `struck` trafionych ludzi okregu wobec `homeBefore` ludzi w domu przed uderzeniem,
        /// zabici, druzyna zrodla (zerowanie, DistrictRaid). Tylko licznik pod zamkiem - blizna o swicie (WarFlush). Nie rzuca.</summary>
        internal static void NoteStrike(Village v, double struck, double homeBefore, double dead, MobileParty src)
        {
            try
            {
                if (v == null || v.Settlement == null || !(struck > 0) || !(homeBefore > 0) || double.IsInfinity(struck) || double.IsInfinity(homeBefore) || !WarOn) return;
                double f = Math.Min(1.0, struck / homeBefore);
                bool player = false;
                try
                {
                    var mp = MobileParty.MainParty;
                    if (mp != null)
                    {
                        if (ReferenceEquals(src, mp)) player = true;
                        else
                        {
                            var me = mp.MapEvent;
                            player = me != null && ReferenceEquals(me.MapEventSettlement, v.Settlement) && mp.MapEventSide != null && ReferenceEquals(mp.MapEventSide, me.AttackerSide);
                        }
                    }
                }
                catch { player = false; }
                lock (StrikeGate)
                {
                    double[] a;
                    if (!_strike.TryGetValue(v.Settlement, out a)) { a = new double[] { 1, 0, 0, 0, 0 }; _strike[v.Settlement] = a; }
                    a[0] *= 1.0 - f; a[1] += struck; a[2] += dead > 0 && !double.IsInfinity(dead) ? dead : 0; if (player) a[3] = 1; a[4] += 1;
                }
            }
            catch (Exception e) { Interlocked.Increment(ref _strikeFails); Interlocked.CompareExchange(ref _strikeErr, e, null); }
        }

        // ------------------------------------------------------------ rozliczenie uderzen (swit; zapis)
        /// <summary>Compute (po PeopleDay, przed dochodem doby) i zapis: blizna kazdej wsi z uderzeniami od ostatniego rozliczenia. day - doba uderzen.</summary>
        private static void WarFlush(int day)
        {
            int nf = Interlocked.Exchange(ref _strikeFails, 0);
            if (nf > 0)
            {
                var ne = Interlocked.Exchange(ref _strikeErr, null);
                _stumbles += nf - 1; _stumblesDay += nf - 1; _wStum += nf;
                Stumble("NoteStrike", ne, ne == null ? nf + " w sluchaczu" : null);
            }
            Dictionary<Settlement, double[]> taken;
            lock (StrikeGate)
            {
                if (_strike.Count == 0) return;
                taken = _strike; _strike = new Dictionary<Settlement, double[]>();
            }
            if (!WarOn || _recs == null) return;              // klucz wylaczony w trakcie - uderzenia bez blizny
            foreach (var kv in taken)
            {
                try { RaidScar(kv.Key, kv.Value, day); }
                catch (Exception e) { _wStum++; Stumble("Blizna(wies)", e); }
            }
        }

        /// <summary>Compute (blok ZC7 doby): doba wlaczenia ZC7 w kampanii - pierwsza doba z kluczem (zapis v4); klucz wylaczony - od nowa przy nastepnym wlaczeniu.</summary>
        private static void WarSeed(int day)
        {
            if (!WarOn) { _warSeedDay = -1; return; }
            if (_warSeedDay < 0) _warSeedDay = day;
        }

        /// <summary>28 dob od wlaczenia ZC7: uchodzcy z dawnych wojn weszli do mianownika Dostatku wsi naraz (zmiana pomiaru, nie zdarzenie) - liczniki spadku ZC6
        /// wsi stoja i bez spadkow wsi (recenzja 16.10: start neutralny na starym zapisie).</summary>
        private static bool WarHoldBelow(int day) { return WarOn && _warSeedDay >= 0 && day >= _warSeedDay && day - _warSeedDay < WarSeedHold; }

        /// <summary>Export / ExportEstates: uderzenia od switu do ksiegi stanow i sredniej dochodu PRZED zapisem (zapis wczytany ponownie daje te same liczby). Nie rzuca.</summary>
        private static void WarFlushSave()
        {
            try
            {
                if (_recs == null || !WarOn) return;
                bool any;
                lock (StrikeGate) any = _strike.Count > 0;
                if (!any) return;
                WarFlush((int)CampaignTime.Now.ToDays);
                _wSaveFlush++;
            }
            catch (Exception e) { _wStum++; Stumble("WarFlushSave", e); }
        }

        private static void RefreshRec(Rec r, EBook eb)
        {
            for (int e = 0; e < 6; e++) { r.Share[e] = eb.P[e] / (double)PpmAll; r.N[e] = r.L * r.Share[e]; }
            if (r.Thralls > 0) r.N[ESlave] += r.Thralls;                 // konto NIEWOLNI ksiegi trzymane w regionie (jak PeopleDay)
        }

        /// <summary>Epizod wojny osady: pierwsze zdarzenie po 30 dobach spokoju zaczyna nowy (Dostatek przed - D0 ostatniego pomiaru), kazde przedluza; F - udzial trafionych.</summary>
        private static void Episode(EBook eb, Rec r, int day, int kind, double f)
        {
            bool fresh = eb.ScarFrom < 0 || eb.ScarDay < 0 || day - eb.ScarDay > WarQuiet;
            if (fresh)
            {
                eb.ScarFrom = day; eb.ScarF = 0f; eb.Rep7 = false;
                eb.ScarD = r.DSrc == 0 && Fin(r.D0) && Math.Abs(r.D0) < 1e6 ? (float)(Math.Round(r.D0 * 100.0) / 100.0) : float.NaN;
                eb.ScarMin = eb.ScarD;
                _wEpisodes++;
            }
            double v = 1.0 - (1.0 - eb.ScarF) * (1.0 - Clamp01(f));
            eb.ScarF = (float)(Math.Round(Math.Min(1.0, v) * 10000.0) / 10000.0);   // te same liczby co w zapisie
            eb.ScarDay = day; eb.ScarKind = kind;
        }

        /// <summary>Blizna najazdu jednej wsi: dochod trafionych, rzemieslnicy -20% x F do chlopow, epizod, komunikat gracza (o swicie).</summary>
        private static void RaidScar(Settlement st, double[] a, int day)
        {
            Rec r; EBook eb;
            if (st == null || !_byS.TryGetValue(st, out r) || r == null || r.Kind != KVillage || !SafeId(r.Id) || !_eb.TryGetValue(r.Id, out eb) || eb == null || eb.P == null)
            { _wNoBook++; return; }
            double keep = a[0];
            if (!Fin(keep) || keep < 0) keep = 0; else if (keep > 1) keep = 1;
            double F = 1.0 - keep;
            _wRaidV++; _wStrikes += (int)a[4]; _wStruck += a[1]; _wDead += a[2];
            if (_wF.Count < WarEvKeep * 10) _wF.Add(F);
            if (F > _wFMax) _wFMax = F;
            if (!(F > 0)) return;
            var p = eb.P;
            Bands(r.Cul, false, _wBand);
            int c0 = p[ECraft];
            double want = ScarRaidCraft * F * c0;
            double room = Math.Min(c0 - 0.5 * _wBand[ECraft] * PpmAll, 1.5 * _wBand[EPeasant] * PpmAll - p[EPeasant]);
            if (want > room) { want = Math.Max(0.0, room); _wBandCut++; }
            int q = want > 0 ? Math.Min(c0, RoundPpm(want, r.Id, day, 300)) : 0;
            double d0 = r.DSrc == 0 ? r.D0 : double.NaN;            // Dostatek przed (ostatni pomiar - sprzed uderzen)
            // (1) trafieni traca dochod od razu (srednia biezaca stanow wolnych wsi); zrujnowani rzemieslnicy - swoj
            for (int e = EPeasant; e <= EGentry; e++) if (e != ELabour) r.YAvg[e] *= keep;
            if (q > 0 && c0 > 0) r.YAvg[ECraft] *= 1.0 - (double)q / c0;
            _wCut++;
            double craft = 0;
            if (q > 0)
            {
                p[ECraft] -= q; p[EPeasant] += q;
                eb.Debt[0] = (int)Math.Min(PpmAll, (long)eb.Debt[0] + q);
                RefreshRec(r, eb);
                craft = q * r.L / PpmAll;
                _wCraftV += craft;
            }
            Episode(eb, r, day, SkRaid, F);
            if (_wEv.Count < WarEvKeep)
                _wEv.Add(new KeyValuePair<double, string>(F, r.Id + " F " + F2(100.0 * F) + "% trafieni " + N0(a[1]) + " (uderzen " + N0(a[4]) + ") rzemieslnicy->chlopi " + N0(craft)));
            bool mine = false;
            try { mine = st.OwnerClan != null && st.OwnerClan == Clan.PlayerClan; } catch { mine = false; }
            bool player = a[3] > 0;
            if ((mine || player) && (F >= MsgMinF || craft >= 0.5)) _wMsgQ.Add(new WarMsg { R = r, Struck = a[1], Craft = craft, D0 = d0, Player = player, Mine = mine });   // bez drobnego zerowania armii (< 0.1% ludzi w domu)
        }

        /// <summary>Compute (po PeopleDay i rozliczeniu blizny): uchodzcy okregu poza domem w mianowniku Dostatku stanow wolnych wsi (wedlug ludzi w domu).</summary>
        private static void WarPeople()
        {
            _wAwayV = 0; _wAwayP = _wAwayHome = 0;
            if (!WarOn || _recs == null || PeopleBook.RefugeeVillages == 0) return;
            foreach (var r in _recs)
            {
                if (r.Kind != KVillage || r.St == null || r.St.Village == null) continue;
                double a = 0;
                try { a = PeopleBook.RefugeesOf(r.St.Village); } catch (Exception e) { a = 0; Stumble("RefugeesOf", e); }
                if (!(a > 0) || double.IsInfinity(a)) continue;
                double nf = r.N[EPeasant] + r.N[ECraft] + r.N[EMerchant] + r.N[EGentry];
                if (!(nf > 0)) continue;
                r.Away = a;
                // sw-uchodzcy (RaidRefugeesToTowns): w mianowniku okregu tylko uchodzcy w regionie - czesc w miescie pobytu jest w mianowniku biedoty miasta (TownGuests);
                // Away (widok "away from home") - cale konto. Wylaczone: ar = a (jak dotad)
                double ar = a;
                try { ar = Math.Max(0.0, a - RaidRefugees.InTown(r.St.Village)); } catch (Exception e) { ar = a; Stumble("RaidRefugees.InTown", e); }
                for (int e = EPeasant; e <= EGentry; e++) if (e != ELabour) r.AwayE[e] = ar * r.N[e] / nf;
                _wAwayV++; _wAwayP += a; _wAwayHome += r.L;
            }
        }

        /// <summary>ActiveDay (po Dostatku doby): najnizszy Dostatek epizodow (setne) i komunikaty gracza z rozliczenia doby (Dostatek przed i po).</summary>
        private static void WarAfter(int day)
        {
            _wDays++;
            if (!WarOn || _recs == null) { _wMsgQ.Clear(); return; }
            foreach (var r in _recs)
            {
                if (r.Kind == KCastle || !SafeId(r.Id)) continue;
                EBook eb;
                if (!_eb.TryGetValue(r.Id, out eb) || eb == null) continue;
                // dlug blizny ponizej progu (50 milionowych albo mniej niz 1 czlowiek) - odpisany: rozpad 25%/rok nie dochodzi do zera, a wpis zostalby w zapisie na zawsze
                for (int k = 0; k < 2; k++)
                {
                    int dv = eb.Debt[k];
                    if (dv <= 0) continue;
                    double ppl = r.L > 0 ? dv * r.L / PpmAll : double.NaN;
                    if (dv < DebtFloorPpm || (Fin(ppl) && ppl < 1.0)) { eb.Debt[k] = 0; _wOffN++; if (Fin(ppl)) _wOffP += ppl; }
                }
                if (r.DSrc != 0 || !Fin(r.D0) || Math.Abs(r.D0) >= 1e6 || eb.ScarFrom < 0) continue;
                float d = (float)(Math.Round(r.D0 * 100.0) / 100.0);
                if (float.IsNaN(eb.ScarMin) || d < eb.ScarMin) eb.ScarMin = d;
            }
            // epizod zamkniety: 2 lata bez zdarzenia - z ksiegi razem z reszta dlugu (zrujnowani zostaja w nowym stanie - dalej zwykly awans ZC6; widok pokazuje rok,
            // linia powrotu - spokoj do 2 lat); zapis nie rosnie o wpis kazdej wsi kiedykolwiek najechanej
            foreach (var kv in _eb)
            {
                var b = kv.Value;
                if (b == null || b.ScarDay < 0 || day - b.ScarDay <= WarForget || (b.ScarFrom < 0 && b.Debt[0] <= 0 && b.Debt[1] <= 0)) continue;
                if (b.Debt[0] > 0 || b.Debt[1] > 0) _wForgetN++;
                b.Debt[0] = b.Debt[1] = 0;
                b.ScarFrom = b.ScarDay = -1; b.ScarKind = 0; b.ScarD = b.ScarMin = float.NaN; b.ScarF = 0f; b.Rep7 = false;
            }
            if (_wMsgQ.Count == 0) return;
            foreach (var m in _wMsgQ)
            {
                try
                {
                    Rec r;
                    if (m.R.St == null || !_byS.TryGetValue(m.R.St, out r) || r == null) r = m.R;   // spis osad od nowa miedzy rozliczeniem przy zapisie a switem - wpis biezacy
                    double now = r.DSrc == 0 ? r.D0 : double.NaN;
                    var sb = new StringBuilder(300);
                    sb.Append(m.Player ? "Your men struck the countryside of " : "Raiders struck the countryside of ").Append(r.Name).Append(": ").Append(Num(m.Struck))
                      .Append(" of its people were driven from their homes or killed");
                    if (m.Craft >= 0.5) sb.Append(", and ").Append(Num(m.Craft)).Append(" craftsmen lost their trade");
                    sb.Append('.');
                    if (Fin(m.D0) && m.D0 > 0 && Fin(now))
                    {
                        if (now < m.D0 * 0.995) sb.Append(" Its livelihood fell from ").Append(LivText(m.D0)).Append(" to ").Append(LivText(now)).Append('.');
                        else sb.Append(" Its livelihood holds at ").Append(LivText(now)).Append(" for now.");
                    }
                    Log.Player(sb.ToString(), m.Mine && !m.Player);
                    _wMsg++;
                }
                catch (Exception e) { _wStum++; Stumble("WarMsg", e); }
            }
            _wMsgQ.Clear();
        }

        // ------------------------------------------------------------ zlupienie miasta (CitySack.People.cs ZcScarPost)
        /// <summary>Postfiks OnSiegeAftermathApplied: kind 0 laska, 1 grabiez, 2 spustoszenie (CitySack.KindOf); captured - osada zdobyta; day - doba kampanii. Ruina od razu, okno ruiny,
        /// dlug blizny, oszczednosci z ludzmi, komunikat gracza i linia zdarzenia. Nie rzuca.</summary>
        internal static void SackScar(Settlement st, int kind, bool captured, MobileParty a, Dictionary<MobileParty, float> contrib, Clan prev, int day)
        {
            try
            {
                if (st == null || st.Town == null) return;
                if (!WarOn) { _wSackOff++; return; }
                if (kind != 1 && kind != 2) { _wSackMercy++; return; }
                if (!st.IsTown) { _wSackCastle++; return; }
                if (!captured) { _wSackNoCap++; return; }
                if (_recs == null && PopulationLaw.Calibrated)
                {
                    // zlupienie przed pierwszym switem po wczytaniu: spis osad teraz (jak pierwsza doba - srednia dochodu z zapisu), zeby wynik nie zalezal od wczytania
                    try { EnsureItems(); EnsureRecs(day); _wSackLate++; } catch (Exception e2) { _wStum++; Stumble("SackScar(spis)", e2); }
                }
                Rec r; EBook eb;
                if (_recs == null || !_byS.TryGetValue(st, out r) || r == null || r.Kind != KTown || !SafeId(r.Id) || !_eb.TryGetValue(r.Id, out eb) || eb == null || eb.P == null)
                { _wSackNoBook++; return; }
                double share = kind == 1 ? ScarPillage : ScarDevastate;
                var p = eb.P;
                Bands(r.Cul, true, _wBand);
                int m0 = p[EMerchant], c0 = p[ECraft], l0 = p[ELabour];
                // kupcy -> rzemieslnicy i rzemieslnicy -> biedota, oba od stanu przed zlupieniem; granice pasm (0.5 x dol zrodla, 1.5 x gora celu)
                double wm = share * m0;
                double roomM = Math.Min(m0 - 0.5 * _wBand[EMerchant] * PpmAll, 1.5 * _wBand[ECraft] * PpmAll - c0);
                bool cutM = false, cutC = false;
                if (wm > roomM) { wm = Math.Max(0.0, roomM); _wBandCut++; cutM = true; }
                int qM = wm > 0 ? Math.Min(m0, RoundPpm(wm, r.Id, day, 310)) : 0;
                double wc = share * c0;
                double roomC = Math.Min(c0 + qM - 0.5 * _wBand[ECraft] * PpmAll, 1.5 * _wBand[ELabour] * PpmAll - l0);
                if (wc > roomC) { wc = Math.Max(0.0, roomC); _wBandCut++; cutC = true; }
                int qC = wc > 0 ? Math.Min(c0 + qM, RoundPpm(wc, r.Id, day, 311)) : 0;
                // oszczednosci z ludzmi: P -> R (czesc kiesy P zrujnowanych kupcow - ludzie x zwykly koszyk, jak ZC6), potem R -> W (czesc kiesy R wedlug ludzi)
                long gP2R = 0, gR2W = 0;
                bool purses = false;
                var u = _usual;
                if (u != null && TownFolk.ZcPurses(st, _wPurse))
                {
                    purses = true;
                    double wsP = m0 * u[EMerchant] + p[EGentry] * u[EGentry];          // milionowe x koszyk - ludzie osady sie skracaja
                    if (qM > 0 && wsP > 0) gP2R = Carry7(st, 2, 1, _wPurse[2] * (qM * u[EMerchant]) / wsP);
                    if (qC > 0 && c0 + qM > 0 && TownFolk.ZcPurses(st, _wPurse)) gR2W = Carry7(st, 1, 0, _wPurse[1] * (double)qC / (c0 + qM));
                }
                else _wNoPurse++;
                double d0 = r.DSrc == 0 ? r.D0 : double.NaN;
                p[EMerchant] -= qM; p[ECraft] += qM - qC; p[ELabour] += qC;
                // zrujnowani traca swoj dochod od razu (srednia biezaca stanu); reszta stanu zostaje przy swoim Dostatku
                double kM = qM > 0 && m0 > 0 ? 1.0 - (double)qM / m0 : 1.0, kC = qC > 0 && c0 > 0 ? 1.0 - Math.Min(1.0, (double)qC / c0) : 1.0;
                r.YAvg[EMerchant] *= kM; r.YAvg[ECraft] *= kC;
                eb.Debt[0] = (int)Math.Min(PpmAll, (long)eb.Debt[0] + qM);
                eb.Debt[1] = (int)Math.Min(PpmAll, (long)eb.Debt[1] + qC);
                RefreshRec(r, eb);
                Episode(eb, r, day, kind == 1 ? SkPillage : SkDevastate, share);
                double L = r.L;
                if (!(L > 0)) { try { double pl = PopulationLaw.PeopleOf(st); L = pl > 0 && Fin(pl) ? pl : 0; } catch { L = 0; } }   // przed pierwsza doba sesji - ludzie ksiegi teraz
                double pM = qM * L / PpmAll, pC = qC * L / PpmAll;
                _wSack++; if (kind == 1) _wSackPill++; else _wSackDev++;
                _wSackM += pM; _wSackC += pC; _wP2R += gP2R; _wR2W += gR2W;
                bool playerIn = false, mine = false, lost = false;
                try
                {
                    var mp = MobileParty.MainParty;
                    playerIn = mp != null && (ReferenceEquals(a, mp) || (contrib != null && contrib.ContainsKey(mp)));
                    lost = prev != null && prev == Clan.PlayerClan;
                    mine = lost || (st.OwnerClan != null && st.OwnerClan == Clan.PlayerClan);
                }
                catch { playerIn = mine = lost = false; }
                bool told = false;
                if ((playerIn || mine) && (pM >= 0.5 || pC >= 0.5))
                {
                    Log.Player("The sack of " + st.Name + " ruined its burghers: " + Num(pM) + " merchants fell to a craft and " + Num(pC) + " craftsmen to day labour. "
                               + "For a month more of them will fall, and in peace they win their trades back only slowly.", lost && !playerIn);
                    _wMsg++; told = true;
                }
                var s = Settings.Current;
                if (s != null && s.LogEnabled)
                {
                    var sb = new StringBuilder(700);
                    sb.Append(PW).Append("zlupienie | dzien ").Append(day).Append(" | osada ").Append(r.Id).Append(" (").Append(r.Name.Replace('|', '/')).Append(") | ").Append(kind == 1 ? "grabiez" : "spustoszenie")
                      .Append(" ").Append(F1(100 * share)).Append("% | kupcy ").Append(m0).Append(" -> ").Append(p[EMerchant]).Append(" ppm (-").Append(F2(m0 > 0 ? 100.0 * qM / m0 : 0)).Append("%)")
                      .Append(cutM ? " [granica pasma]" : "").Append(", rzemieslnicy ").Append(c0).Append(" -> ").Append(p[ECraft]).Append(" ppm (odeszlo -").Append(F2(c0 > 0 ? 100.0 * qC / c0 : 0)).Append("%)")
                      .Append(cutC ? " [granica pasma]" : "").Append(", biedota ").Append(l0).Append(" -> ").Append(p[ELabour]).Append(" ppm")
                      .Append(" | ludzie (ksiega): kupcy->rzemieslnicy ").Append(N0(pM)).Append(", rzemieslnicy->biedota ").Append(N0(pC)).Append(" z ").Append(N0(L))
                      .Append(" | pieniadze z ludzmi P->R ").Append(gP2R).Append(", R->W ").Append(gR2W).Append(purses ? "" : " (kiesa nieczynna - bez przelewu)")
                      .Append(" | srednia dochodu: kupcy x ").Append(F3(kM)).Append(", rzemieslnicy x ").Append(F3(kC))
                      .Append(" | D miasta przed ").Append(F2(d0)).Append(" | dlug blizny: kupcy ").Append(eb.Debt[0]).Append(", rzemieslnicy ").Append(eb.Debt[1]).Append(" ppm")
                      .Append(" | okno ruiny do dnia ").Append(day + RuinDays - 1).Append(" (spadki x4)")
                      .Append(" | gracz: udzial ").Append(playerIn ? "tak" : "nie").Append(", rod gracza ").Append(mine ? "tak" : "nie").Append(", komunikat ").Append(told ? "tak" : "nie").Append('.');
                    Log.Info(sb.ToString());
                }
            }
            catch (Exception e) { _wStum++; Stumble("SackScar", e); }
        }

        /// <summary>Oszczednosci ludzi miedzy kiesami miasta (ZC7) - TownFolk.ZcCarry; kontrola: suma kies bez zmian.</summary>
        private static long Carry7(Settlement st, int from, int to, double x)
        {
            long want = (long)Math.Round(x);
            if (want <= 0) return 0;
            long before = PurseSum(st);
            long got = TownFolk.ZcCarry(st, from, to, want);
            long after = PurseSum(st);
            if (after != before) _wCtl += Math.Abs(after - before);
            return got;
        }

        // ------------------------------------------------------------ powrot blizny (przeglad tygodniowy ZC6 - WeekOne)
        /// <summary>WeekOne: przeplywy powrotu zrujnowanych do zawodu (ludzie na tydzien) - w pokoju (30 dob bez zdarzenia, bez oblezenia / najazdu), 25% dlugu na rok,
        /// gdy stan docelowy ma Dostatek >= progu spadku. Limit 0.2%/tydz., granice pasm i pieniadze - w WeekOne.</summary>
        private static void WarBack(Rec r, EBook eb, int day, bool town, double[] fl, double L)
        {
            if (eb.Debt[0] <= 0 && eb.Debt[1] <= 0) return;
            if (eb.ScarDay >= 0 && day - eb.ScarDay < WarQuiet) return;
            try
            {
                if (town ? r.St.IsUnderSiege : (r.St.Village == null || r.St.Village.VillageState != Village.VillageStates.Normal)) return;
            }
            catch { return; }
            double wk = RScarBack * WeekYear * L / PpmAll;         // ludzie na tydzien za 1 milionowa dlugu
            if (town)
            {
                if (eb.Debt[1] > 0) { if (Fin(r.DE[ECraft]) && r.DE[ECraft] >= LCraft) fl[TBackC] = wk * eb.Debt[1]; else _wBackNoD++; }
                if (eb.Debt[0] > 0) { if (Fin(r.DE[EMerchant]) && r.DE[EMerchant] >= LMerch) fl[TBackM] = wk * eb.Debt[0]; else _wBackNoD++; }
            }
            else if (eb.Debt[0] > 0) { if (Fin(r.DE[ECraft]) && r.DE[ECraft] >= LCraft) fl[TBackV] = wk * eb.Debt[0]; else _wBackNoD++; }
        }

        private static void WarBackDone(int t, double ppl)
        {
            if (t == TBackV) _wBackV += ppl; else if (t == TBackC) _wBackC += ppl; else _wBackM += ppl;
        }

        // ------------------------------------------------------------ zapis (arm_estates v3, pole "blizna")
        /// <summary>doba ostatniego zdarzenia,poczatek epizodu,rodzaj,D przed x100,D najnizszy x100,F x10000,dlug0,dlug1 - puste bez epizodu i dlugu.</summary>
        private static void AppendScar(StringBuilder sb, EBook b)
        {
            if (b.ScarFrom < 0 && b.Debt[0] == 0 && b.Debt[1] == 0) return;
            sb.Append(b.ScarDay.ToString(Inv)).Append(',').Append(b.ScarFrom.ToString(Inv)).Append(',').Append(b.ScarKind.ToString(Inv)).Append(',');
            if (!float.IsNaN(b.ScarD) && !float.IsInfinity(b.ScarD)) sb.Append(((long)Math.Round(b.ScarD * 100.0)).ToString(Inv));
            sb.Append(',');
            if (!float.IsNaN(b.ScarMin) && !float.IsInfinity(b.ScarMin)) sb.Append(((long)Math.Round(b.ScarMin * 100.0)).ToString(Inv));
            sb.Append(',').Append(((long)Math.Round(Math.Max(0.0, Math.Min(1.0, b.ScarF)) * 10000.0)).ToString(Inv))
              .Append(',').Append(b.Debt[0].ToString(Inv)).Append(',').Append(b.Debt[1].ToString(Inv));
        }

        private static bool ParseScar(string s, EBook b)
        {
            var q = s.Split(',');
            if (q.Length != 8) return false;
            int day, from, kind, d0, d1; long f, x;
            if (!int.TryParse(q[0], NumberStyles.Integer, Inv, out day) || !int.TryParse(q[1], NumberStyles.Integer, Inv, out from)
                || !int.TryParse(q[2], NumberStyles.Integer, Inv, out kind) || kind < 0 || kind > SkDevastate
                || !long.TryParse(q[5], NumberStyles.Integer, Inv, out f) || !int.TryParse(q[6], NumberStyles.Integer, Inv, out d0) || !int.TryParse(q[7], NumberStyles.Integer, Inv, out d1)
                || d0 < 0 || d1 < 0 || d0 > PpmAll || d1 > PpmAll) return false;
            b.ScarDay = day; b.ScarFrom = from; b.ScarKind = kind;
            b.ScarD = q[3].Length > 0 && long.TryParse(q[3], NumberStyles.Integer, Inv, out x) ? (float)(x / 100.0) : float.NaN;
            b.ScarMin = q[4].Length > 0 && long.TryParse(q[4], NumberStyles.Integer, Inv, out x) ? (float)(x / 100.0) : float.NaN;
            b.ScarF = (float)(Math.Max(0L, Math.Min(10000L, f)) / 10000.0);
            b.Debt[0] = d0; b.Debt[1] = d1;
            return true;
        }

        // ------------------------------------------------------------ linie
        /// <summary>OnSessionLaunched (po MobilitySessionStart): linia startu ZC7 - klucz, zasady, ksiega blizny z zapisu.</summary>
        internal static void WarSessionStart()
        {
            try
            {
                var s = Settings.Current;
                int epi = 0, dV = 0, dT = 0;
                foreach (var kv in _eb)
                {
                    var b = kv.Value;
                    if (b == null || b.P == null) continue;
                    if (b.ScarFrom >= 0) epi++;
                    if (b.Debt[0] > 0 || b.Debt[1] > 0) { if (b.Debt[1] > 0 || b.ScarKind >= SkPillage) dT++; else dV++; }
                }
                Log.Info(PW + "start sesji | klucz Daily Life War Scar " + (s != null && s.DailyLifeWarScar ? "tak" : "WYLACZONE") + " (Daily Life Estates "
                         + (s != null && s.DailyLifeEstates ? "tak" : "WYLACZONE") + ", Daily Life Measure " + (KeyOn ? "tak" : "WYLACZONE") + ")"
                         + (EstatesOn ? "" : " - bez ksiegi stanow nic nie rusza")
                         + " | najazd: trafieni traca dochod od razu, uchodzcy okregu w mianowniku Dostatku bez dochodu, rzemieslnicy wsi -" + F1(100 * ScarRaidCraft) + "% x F do chlopow"
                         + " | zlupienie zdobytego miasta: kupcy->rzemieslnicy i rzemieslnicy->biedota " + F1(100 * ScarPillage) + "% (grabiez) / " + F1(100 * ScarDevastate)
                         + "% (spustoszenie), okno ruiny " + RuinDays + " dob (spadki ZC6 x" + F1(RuinDown) + " bez czekania 4 przegladow)"
                         + " | powrot blizny: " + F1(100 * RScarBack) + "%/rok po " + WarQuiet + " dobach spokoju, gdy stan docelowy ma Dostatek >= progu spadku"
                         + " | ksiega blizny z zapisu: format " + (_eLoadVer ?? "-") + ", epizodow " + epi + ", z dlugiem: wsie " + dV + ", miasta " + dT + ", zle pola blizny " + _eLoadBadScar
                         + " | potkniecia " + _stumbles + ".");
            }
            catch (Exception e) { Stumble("WarSessionStart", e); }
        }

        /// <summary>Linia doby ZC7 (doba linii): najazdy i zlupienia od ostatniej linii, powrot, dlug, uchodzcy, zdarzenia d+7, powrot epizodow. Zeruje liczniki linii.</summary>
        private static string WarLine(int day)
        {
            int dV = 0, dT = 0, ruinT = 0, epi = 0;
            double dVp = 0, dTm = 0, dTc = 0;
            var d7 = new List<string>(); var d7drop = new List<double>(); var d7big = new List<double>();
            var back = new List<KeyValuePair<double, string>>();
            if (_recs != null)
                foreach (var r in _recs)
                {
                    if (r.Kind == KCastle || !SafeId(r.Id)) continue;
                    EBook eb;
                    if (!_eb.TryGetValue(r.Id, out eb) || eb == null || eb.P == null) continue;
                    if (eb.Debt[0] > 0 || eb.Debt[1] > 0)
                    {
                        if (r.Kind == KVillage) { dV++; dVp += eb.Debt[0] * r.L / PpmAll; }
                        else { dT++; dTm += eb.Debt[0] * r.L / PpmAll; dTc += eb.Debt[1] * r.L / PpmAll; }
                    }
                    if (eb.ScarFrom < 0) continue;
                    epi++;
                    if (r.Kind == KTown && eb.ScarKind >= SkPillage && eb.ScarDay >= 0 && day - eb.ScarDay < RuinDays) ruinT++;
                    int age = day - eb.ScarFrom, quiet = eb.ScarDay >= 0 ? day - eb.ScarDay : age;
                    double now = r.DSrc == 0 ? r.D0 : double.NaN;
                    string kn = SkName[Math.Max(0, Math.Min(SkName.Length - 1, eb.ScarKind))];
                    if (!eb.Rep7 && age >= 7 && age <= 13)
                    {
                        eb.Rep7 = true;
                        double ch = Fin(eb.ScarD) && eb.ScarD > 0 && Fin(now) ? now / eb.ScarD - 1.0 : double.NaN;
                        if (d7.Count < WarListN)
                            d7.Add(r.Id + " " + kn + " od " + eb.ScarFrom + " F " + F2(100.0 * eb.ScarF) + "% D " + F2(eb.ScarD) + "->" + F2(now) + " ("
                                   + (Fin(ch) ? (ch >= 0 ? "+" : "") + F1(100 * ch) + "%" : "-") + ")");
                        if (Fin(ch)) { d7drop.Add(-100.0 * ch); if (eb.ScarF >= 0.2f) d7big.Add(-100.0 * ch); }
                    }
                    if (age >= 60 && quiet >= WarQuiet && Fin(eb.ScarD) && Fin(eb.ScarMin) && Fin(now) && eb.ScarD > eb.ScarMin + 0.005)
                    {
                        double rec = (now - eb.ScarMin) / (eb.ScarD - eb.ScarMin);
                        back.Add(new KeyValuePair<double, string>(eb.ScarD - eb.ScarMin, r.Id + " " + kn + " od " + eb.ScarFrom + " (wiek " + age + ", spokoj " + quiet + ") D "
                                 + F2(eb.ScarD) + "/" + F2(eb.ScarMin) + "/" + F2(now) + " powrot " + F1(100 * rec) + "%"));
                    }
                }
            var sb = new StringBuilder(3200);
            var fs = new List<double>(_wF); fs.Sort();
            sb.Append(PW).Append("dzien ").Append(day).Append(" | klucz ").Append(WarOn ? "tak" : "nie").Append(" | od ostatniej linii ").Append(_wDays).Append(" dob")
              .Append(" | najazdy: wsi ").Append(_wRaidV).Append(" (uderzen ").Append(_wStrikes).Append("), trafieni ").Append(N0(_wStruck)).Append(" (zabici ").Append(N0(_wDead)).Append(')')
              .Append(", udzial trafionych z ludzi w domu: mediana ").Append(F2(fs.Count > 0 ? 100 * Median(fs) : double.NaN)).Append("%, max ").Append(F2(100 * _wFMax)).Append('%')
              .Append("; rzemieslnicy wsi -> chlopi ").Append(N0(_wCraftV)).Append(" ludzi; srednia dochodu ucieta trafionym: wsi ").Append(_wCut).Append("; bez ksiegi stanow ").Append(_wNoBook)
              .Append(" | zlupienia: miast ").Append(_wSack).Append(" (grabiez ").Append(_wSackPill).Append(", spustoszenie ").Append(_wSackDev).Append("), pominiete: laska ").Append(_wSackMercy)
              .Append(", zamki ").Append(_wSackCastle).Append(", bez zdobycia ").Append(_wSackNoCap).Append(", bez ksiegi ").Append(_wSackNoBook).Append(", klucz wylaczony ").Append(_wSackOff)
              .Append("; kupcy -> rzemieslnicy ").Append(N0(_wSackM)).Append(" ludzi, rzemieslnicy -> biedota ").Append(N0(_wSackC)).Append(" ludzi; pieniadze z ludzmi P->R ").Append(_wP2R)
              .Append(", R->W ").Append(_wR2W).Append(", kiesa nieczynna ").Append(_wNoPurse)
              .Append(" | granice pasm: ciecia ").Append(_wBandCut)
              .Append(" | okno ruiny (").Append(RuinDays).Append(" dob, spadki x4): miast teraz ").Append(ruinT).Append(", spadki w oknie ").Append(N0(_wRuinDown)).Append(" ludzi")
              .Append(" | powrot blizny (spokoj >= ").Append(WarQuiet).Append(" dob, 25%/rok): chlopi->rzemieslnicy wsi ").Append(N0(_wBackV)).Append(", biedota->rzemieslnicy ").Append(N0(_wBackC))
              .Append(", rzemieslnicy->kupcy ").Append(N0(_wBackM)).Append("; pieniadze W->R ").Append(_wBW2R).Append(", R->P ").Append(_wBR2P).Append("; bez Dostatku do powrotu ").Append(_wBackNoD)
              .Append(" | dlug blizny: wsie ").Append(dV).Append(" (").Append(N0(dVp)).Append(" ludzi), miasta ").Append(dT).Append(" (kupcy ").Append(N0(dTm)).Append(", rzemieslnicy ").Append(N0(dTc)).Append(')')
              .Append(" | dlug odpisany: ponizej progu ").Append(_wOffN).Append(" (").Append(N0(_wOffP)).Append(" ludzi), po 2 latach spokoju ").Append(_wForgetN).Append(" osad")
              .Append(" | ZC7 od dnia ").Append(_warSeedDay).Append(" (liczniki spadku wsi stoja ").Append(WarSeedHold).Append(" dob: przegladow ").Append(_wHoldV).Append(')')
              .Append(" | zlupienia przed pierwszym switem po wczytaniu ").Append(_wSackLate)
              .Append(" | uchodzcy w mianowniku Dostatku: wsi ").Append(_wAwayV).Append(", ludzie ").Append(N0(_wAwayP)).Append(" (").Append(F2(_wAwayHome > 0 ? 100 * _wAwayP / _wAwayHome : 0))
              .Append("% ludzi tych wsi w domu)")
              .Append(" | epizody wojny w ksiedze ").Append(epi).Append(" (nowe od ostatniej linii ").Append(_wEpisodes).Append(')')
              .Append(" | kontrola kies roznica ").Append(_wCtl);
            d7drop.Sort(); d7big.Sort();
            sb.Append(" | zdarzenia d+7 (D przed -> teraz): ").Append(d7drop.Count).Append(" [mediana spadku ").Append(F1(d7drop.Count > 0 ? Median(d7drop) : double.NaN))
              .Append("%; przy F >= 20%: ").Append(d7big.Count).Append(", mediana spadku ").Append(F1(d7big.Count > 0 ? Median(d7big) : double.NaN)).Append("%]: ")
              .Append(d7.Count > 0 ? string.Join("; ", d7.ToArray()) : "-");
            back.Sort((x, y) => y.Key.CompareTo(x.Key));
            sb.Append(" | powrot (>= 60 dob od poczatku, >= ").Append(WarQuiet).Append(" spokoju; D przed/najnizszy/teraz): ").Append(back.Count).Append(": ");
            for (int i = 0; i < back.Count && i < WarListN; i++) sb.Append(i == 0 ? "" : "; ").Append(back[i].Value);
            if (back.Count == 0) sb.Append('-');
            _wEv.Sort((x, y) => y.Key.CompareTo(x.Key));
            sb.Append(" | najazdy od ostatniej linii (najwieksze F): ");
            for (int i = 0; i < _wEv.Count && i < WarListN; i++) sb.Append(i == 0 ? "" : "; ").Append(_wEv[i].Value);
            if (_wEv.Count == 0) sb.Append('-');
            sb.Append(" | rozliczenie przy zapisie ").Append(_wSaveFlush).Append(" | komunikaty gracza ").Append(_wMsg).Append(" | potkniecia ").Append(_wStum).Append('.');
            ZeroWarLine();
            return sb.ToString();
        }

        // ------------------------------------------------------------ widok (People of this land)
        private static string Ago(int d) { return d <= 0 ? "today" : d == 1 ? "yesterday" : d + " days ago"; }

        /// <summary>Zdanie "War: ..." okna People of this land: Dostatek przed i po epizodzie, okno ruiny, uchodzcy poza domem, zrujnowani bez zawodu. null - nic.</summary>
        private static string WarViewText(Rec r)
        {
            try
            {
                if (r == null || r.Kind == KCastle || !WarOn || !SafeId(r.Id)) return null;
                EBook eb;
                if (!_eb.TryGetValue(r.Id, out eb)) eb = null;
                bool town = r.Kind == KTown;
                string place = town ? "town" : "district";
                var parts = new List<string>(4);
                int now = _viewDay;
                if (eb != null && eb.ScarFrom >= 0 && eb.ScarDay >= 0 && now - eb.ScarDay <= WarShowDays)
                {
                    string what = town ? (eb.ScarKind == SkDevastate ? "the sack and burning" : "the sack") : "the raids";
                    int ago = Math.Max(0, now - eb.ScarFrom);
                    double d, inc; bool reg;
                    LivOf(r, out d, out inc, out reg);
                    double before = eb.ScarD;
                    if (!reg && Fin(d) && Fin(before) && before > 0)
                    {
                        double ch = d / before - 1.0;
                        string pct = (100.0 * Math.Abs(ch)).ToString("0", Inv);
                        if (ch <= -0.03) parts.Add("Livelihood of the " + place + " fell " + pct + "% after " + what + " (" + Ago(ago) + "): from " + LivText(before) + " to " + LivText(d) + ".");
                        else if (ch >= 0.03) parts.Add("Livelihood of the " + place + " is " + pct + "% above where it stood before " + what + " (" + Ago(ago) + "): " + LivText(d) + ".");
                        else parts.Add("Livelihood of the " + place + " is back where it stood before " + what + " (" + Ago(ago) + "): " + LivText(d) + ".");
                    }
                    else parts.Add("War struck this " + place + " " + Ago(Math.Max(0, now - eb.ScarDay)) + ".");
                    if (town && eb.ScarKind >= SkPillage && now - eb.ScarDay < RuinDays)
                        parts.Add("For a month after a sack its burghers fall faster: those who cannot keep themselves drop a step down at four times the usual pace.");
                }
                if (r.Away >= 0.5)
                {
                    // sw-uchodzcy (recenzja, drobne 4; RaidRefugeesToTowns): czesc w miescie pobytu nie jest w mianowniku okregu (WarPeople) - widok mowi, ilu mieszka w miescie,
                    // a ilu liczy okreg. Klucz wylaczony / nikt w miescie - tekst co do znaku jak dotad
                    double inT = 0; string host = null;
                    try
                    {
                        if (!town && r.St != null && r.St.Village != null)
                        {
                            inT = Math.Min(r.Away, RaidRefugees.InTown(r.St.Village));
                            if (inT >= 0.5) host = RaidRefugees.HostName(r.St.Village);
                        }
                    }
                    catch (Exception e) { inT = 0; ViewStumble("WarViewText(RT)", e); }
                    if (inT >= 0.5)
                    {
                        double rg = Math.Max(0.0, r.Away - inT);
                        parts.Add(Num(r.Away) + " of its people are away from home, driven off by war: " + Num(inT) + " live in " + (host ?? "a town") + " among its labourers, where they work and buy their bread"
                                  + (rg >= 0.5 ? ", and the district counts the other " + Num(rg) + " in its livelihood with no income until they come home" : "")
                                  + "; they start coming home once the region has had a month of peace.");
                    }
                    else
                        parts.Add(Num(r.Away) + " of its people are away from home, driven off by war - the district counts them in its livelihood with no income until they come home, "
                                  + "and they start coming home once the region has had a month of peace.");
                }
                if (eb != null && eb.P != null && (eb.Debt[0] > 0 || eb.Debt[1] > 0) && r.L > 0)
                {
                    if (town)
                    {
                        double m = eb.Debt[0] * r.L / PpmAll, c = eb.Debt[1] * r.L / PpmAll;
                        if (m >= 0.5 || c >= 0.5)
                            parts.Add(Num(m) + " merchants and " + Num(c) + " craftsmen ruined by war have not yet won back their trades; in peace they do so slowly, about a quarter of them a year, when the trade can feed them.");
                    }
                    else
                    {
                        double c = eb.Debt[0] * r.L / PpmAll;
                        if (c >= 0.5) parts.Add(Num(c) + " craftsmen who lost their trade in the raids have not yet gone back to it; in peace they do so slowly, about a quarter of them a year.");
                    }
                }
                return parts.Count > 0 ? "War: " + string.Join(" ", parts.ToArray()) : null;
            }
            catch (Exception e) { ViewStumble("WarViewText", e); return null; }
        }
    }
}
