using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// m96-sakiewki (WGRANIE 96, PLAN 5 "sakiewki zolnierzy najwyzej 15 dni zoldu"; RAPORT-NOCNY-2026-10-20 rozdz. 5) - OSZCZEDNOSCI WEDLUG POTRZEBY.
    /// Klucz SavingsByNeed (domyslnie TAK; wylaczony = jak przed paczka). Dziala tylko z MenPurse i MenUpgrade (dozbrajanie z sakiewki).
    ///
    /// Dowod (6 biegow 20.10, linie "Obieg", "Markietani (E5/A3)", "Dozbrajanie: dzien"): sakiewki stoja na 21.6-24.5 dni zoldu (160 dob z zapisu d240,
    /// 4 biegi) i rosna od 2 do 21 dni w 120 dobach nowej kampanii. Sklad (WGRANIE 95, d109235): partie rodow 14.75 mln = 23.5 dni ich zoldu, w tym
    /// oszczednosci K1 8.93 mln = 14.2 dni; zalogi 5.89 mln = 25.4 dni zoldu zalog (limit 30); karawany 0.78 mln. Na lepszy sprzet ludzie odkladaja
    /// 59-76 tys./d, a lordowie kupuja lepszy za 13-17 tys./d; pominiete koszyki: "brak lepszej na polce" 19-22 tys./d, "za malo w sakiewce" 39-124/d.
    /// Pieniadz prawie nigdy nie jest tym, czego brakuje - oszczednosci czekaja na towar, ktorego nie ma (N6 IdleKeep 0.5 x MenGearSaveDays 30 = 15 dni
    /// chronione w polu u partii bez zakupu od 14 dob, zalogi do 30 dni bez zadnego ujscia poza zakupami).
    ///
    /// Regula (jedna dla lordow AI, ludzi gracza i zalog): ludzie odkladaja tylko na sprzet, ktory widzieli na targu i na ktory ich nie bylo stac.
    ///  - Cel oszczedzania: suma cen z JEDNEJ wizyty na targu (okno VisitBegin/VisitEnd: AiGear.TryBuy - braki i lepsze lordow i zalog; MenPurse - braki
    ///    i lepsze gracza): w kazdym koszyku dozbrajania odrzuconym "za malo w sakiewce" najtansza lepsza sztuka (MenUpgrade.Pick) i w kazdym koszyku brakow,
    ///    w ktorym pasujaca sztuka lezala, ale byla za droga na reszte budzetu (AiGear.BuyLoop, MenPurse.BuyPlayerGaps) - najtansza taka sztuka (jedna
    ///    sztuka na koszyk). Wizyta bez przegladu (bramka doby) - cel bez zmian; przeglad bez takich koszykow - cel 0. Recenzja: dotad tylko dozbrajanie,
    ///    a braki placi najpierw ta sama sakiewka (gracz - tylko ona).
    ///  - Wezszy przeglad (zaloga zamku w dniu bez zamowienia wozem - widzi tylko polke zamku): cel z dnia, w ktorym widziala targ miasta, zostaje
    ///    (najwyzej NarrowKeepDays dob), chyba ze nowy jest wyzszy. Zapis "arm_m96sak" (pole t: id:zl:doba).
    ///  - Partie (OnLeft i markietani - MenPurse.CampReserve): chroniona czesc sakiewki = najwyzej zalegle naprawy + cel (i nie wiecej niz dotad:
    ///    MenGearSaveDays x zold). Reszta jest wolna: w polu markietani (N6 bez zmian), w miescie przy wyjezdzie - zycie (jak dotad nadwyzka ponad limit).
    ///  - Zalogi: chronione = zalegle naprawy + cel; wolna reszta schodzi co dobe na zycie w ich osadzie ta sama miara co markietani N6
    ///    (SutlerDailyShare x min(3, dni wolnej sakiewki / 10)) - do kasy osady, 75/25 kupcy / lud (TownFolk.EarnService KGarrison), jak zold zalogi
    ///    wydany na miejscu. Wplyw zoldu do sakiewki zalogi (SoldierPay.GarrisonShare) bez zmian - zaloga ma z czego kupowac braki.
    ///  - Rozruch (pole r zapisu): pierwsza doba z kluczem - gdy nowa regula uwolnilaby wiecej niz RampTriggerDays dnia zoldu (oszczednosci K1 partii
    ///    rodow chronione dzis + wolna czesc sakiewek zalog; trwajaca kampania), wchodzi liniowo w SutlerRampDays (28) dob; nowa kampania (puste sakiewki) -
    ///    od razu. Recenzja: dotad prog "sakiewki > 15 dni" - kampania Jeffa (8-10 dni) dostalaby regule od razu, wbrew opisowi. Klucz wylaczony - rozruch
    ///    od nowa przy ponownym wlaczeniu.
    /// Zloto nie powstaje i nie znika: sakiewka zalogi -> kasa jej osady + kiesa ludu (posiadacze "Pieniadz swiata"); partie - te same drogi co dotad.
    /// Kontrola w linii z liczb niezaleznych: ubytek sakiewek (przed/po Take) = przyrost kas osad (przed/po ChangeGold) + przyjete przez kiesy ludu.
    /// Wyjatek na zalodze - licznik potkniec (bez globalnego wylacznika).
    /// </summary>
    internal static class SavingsByNeed
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const double RampTriggerDays = 1.0;    // rozruch, gdy nowa regula uwolnilaby wiecej niz 1 dzien zoldu partii rodow i zalog
        private const double ByPurseCap = 3.0, ByPurseDays = 10.0;   // ta sama miara co markietani N6 (Sutlers)
        private const int NarrowKeepDays = 7;          // zaloga zamku: cel z targu miasta trzyma sie w dniach bez targu (zamowienie co GarrisonOrderDays = 3)

        internal static bool On { get { var s = Settings.Current; return s != null && s.SavingsByNeed && MenPurse.On && MenUpgrade.On; } }

        private struct Want { public int Zl; public int Day; }
        // cel oszczedzania partii (StringId -> zl i doba ustalenia); zapis pole t
        private static readonly Dictionary<string, Want> _want = new Dictionary<string, Want>();
        // rozruch: -2 nie rozstrzygniety, -1 bez rozruchu albo skonczony, >= 0 doba startu
        private static int _rampStart = -2;
        // wizyta na targu w toku (AiGear.TryBuy / MenPurse - watek glowny)
        private static bool _vOn, _vSaw, _vNarrow, _vWide; private static long _vSum; private static int _vNUp, _vNGap;
        // liczniki doby
        private static int _dVisits, _dVisitsNarrow, _dKeptNarrow, _dVisitsWant, _dBasketsUp, _dBasketsGap, _dGar, _dGarPaid, _dGarSiege, _dGarSack, _stumbles;
        private static long _dWantSum, _dGarPurse, _dGarProt, _dGarFree, _dGarTaken, _dGarTown, _dGarCastle, _dGarFolk, _dGarWage, _dCapOld, _dCapNew;
        private static long _dGarOut, _dGarCoffer;     // kontrola: ubytek sakiewek (przed/po Take) i przyrost kas osad (przed/po ChangeGold)
        private static int _dCapN;
        // linia "Obieg" (sakiewki ludzi): wydatek zalog tej doby; -1 = wylaczone (dopisek pusty)
        internal static long LastGarTaken = -1, LastGarTown, LastGarCastle;
        private static readonly HashSet<string> _err = new HashSet<string>();

        internal static void Reset()
        {
            _want.Clear(); _rampStart = -2; _vOn = false; _vSum = 0; _vNUp = _vNGap = 0; _stumbles = 0; _err.Clear();
            LastGarTaken = -1; LastGarTown = LastGarCastle = 0;
            ClearDay();
        }

        private static void ClearDay()
        {
            _dVisits = _dVisitsNarrow = _dKeptNarrow = _dVisitsWant = _dBasketsUp = _dBasketsGap = _dGar = _dGarPaid = _dGarSiege = _dGarSack = 0;
            _dWantSum = _dGarPurse = _dGarProt = _dGarFree = _dGarTaken = _dGarTown = _dGarCastle = _dGarFolk = _dGarWage = 0;
            _dGarOut = _dGarCoffer = 0;
            _dCapOld = _dCapNew = 0; _dCapN = 0;
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_err.Add(where)) Log.Error("SavingsByNeed." + where, e); } catch { }
        }

        // ------------------------------------------------------------ zapis "arm_m96sak": r=<rozruch>;t=id:zl:doba,... (stary zapis bez klucza = cele 0, rozruch nierozstrzygniety)
        internal static string Export()
        {
            var sb = new StringBuilder(256);
            sb.Append("r=").Append(_rampStart.ToString(Inv));
            try
            {
                if (_want.Count > 0)
                {
                    var live = new HashSet<string>();
                    foreach (var mp in MobileParty.All) if (mp != null && mp.StringId != null) live.Add(mp.StringId);
                    var bad = new[] { ';', '=', ',', ':' };
                    bool first = true;
                    foreach (var kv in _want)
                    {
                        if (kv.Value.Zl <= 0 || !live.Contains(kv.Key) || kv.Key.IndexOfAny(bad) >= 0) continue;
                        sb.Append(first ? ";t=" : ",").Append(kv.Key).Append(':').Append(kv.Value.Zl.ToString(Inv)).Append(':').Append(kv.Value.Day.ToString(Inv));
                        first = false;
                    }
                }
            }
            catch (Exception e) { Stumble("Export", e); }
            return sb.ToString();
        }

        internal static void Import(string s)
        {
            _want.Clear(); _rampStart = -2;
            if (string.IsNullOrEmpty(s)) return;
            try
            {
                foreach (var p in s.Split(';'))
                {
                    int eq = p.IndexOf('=');
                    if (eq <= 0) continue;
                    string k = p.Substring(0, eq), v = p.Substring(eq + 1);
                    int x;
                    if (k == "r" && int.TryParse(v, NumberStyles.Integer, Inv, out x) && x >= -2) _rampStart = x;
                    else if (k == "t")
                        foreach (var e in v.Split(','))
                        {
                            var f = e.Split(':');   // id:zl:doba (id bez ':' - Export je odrzuca); bez doby - 0 (cel stary: wezszy przeglad moze go zmienic)
                            int day;
                            if (f.Length < 2 || f[0].Length == 0 || !int.TryParse(f[1], NumberStyles.Integer, Inv, out x) || x <= 0) continue;
                            if (f.Length < 3 || !int.TryParse(f[2], NumberStyles.Integer, Inv, out day)) day = 0;
                            _want[f[0]] = new Want { Zl = x, Day = day };
                        }
                }
            }
            catch (Exception e) { Stumble("Import", e); }
        }

        // ------------------------------------------------------------ cel oszczedzania (jedna wizyta na targu)
        /// <summary>Poczatek wizyty jednej partii na targu (AiGear.TryBuy; MenPurse - gracz: braki i dozbrajanie). Zawsze od zera (bez zagniezdzen). Nie rzuca.</summary>
        internal static void VisitBegin() { _vOn = true; _vSaw = _vNarrow = _vWide = false; _vSum = 0; _vNUp = _vNGap = 0; }

        /// <summary>W tej wizycie naprawde przegladali polke (braki albo dozbrajanie, po bramkach). `narrow` - przeglad wezszy niz zwykle (zaloga zamku bez
        /// targu miasta tego dnia). Nie rzuca.</summary>
        internal static void Saw(bool narrow) { if (!_vOn) return; _vSaw = true; if (narrow) _vNarrow = true; else _vWide = true; }

        /// <summary>Koszyk, w ktorym pasujaca sztuka lezala, ale ich nie bylo na nia stac: najtansza taka sztuka (gap - brak, inaczej lepsza). Nie rzuca.</summary>
        internal static void NoteWant(int price, bool gap)
        {
            if (!_vOn || price <= 0 || price == int.MaxValue) return;
            _vSum += price;
            if (gap) _vNGap++; else _vNUp++;
        }

        /// <summary>Koniec wizyty: cel partii = suma z tej wizyty (0 - nie ma na co odkladac); bez przegladu - bez zmian; wezszy przeglad nie obniza celu
        /// z ostatnich NarrowKeepDays dob. Nie rzuca.</summary>
        internal static void VisitEnd(MobileParty mp)
        {
            if (!_vOn) return;
            _vOn = false;
            try
            {
                if (!_vSaw || mp == null || mp.StringId == null) return;
                int today = (int)CampaignTime.Now.ToDays;
                bool narrow = _vNarrow && !_vWide;
                _dVisits++;
                if (narrow) _dVisitsNarrow++;
                int nv = (int)Math.Min(int.MaxValue, _vSum);
                Want old;
                if (narrow && _want.TryGetValue(mp.StringId, out old) && old.Zl >= nv && today - old.Day <= NarrowKeepDays) { _dKeptNarrow++; return; }
                if (nv > 0)
                {
                    _want[mp.StringId] = new Want { Zl = nv, Day = today };
                    _dVisitsWant++; _dBasketsUp += _vNUp; _dBasketsGap += _vNGap; _dWantSum += nv;
                }
                else _want.Remove(mp.StringId);
            }
            catch (Exception e) { Stumble("VisitEnd", e); }
        }

        /// <summary>Rozbita / rozwiazana partia - bez celu (MenPurse.OnPartyDestroyed). Nie rzuca.</summary>
        internal static void Forget(MobileParty mp) { try { if (mp != null && mp.StringId != null) _want.Remove(mp.StringId); } catch { } }

        private static int WantOf(MobileParty mp) { Want v; return mp != null && mp.StringId != null && _want.TryGetValue(mp.StringId, out v) ? v.Zl : 0; }

        // ------------------------------------------------------------ chroniona czesc sakiewki partii (MenPurse.OnLeft i CampReserve)
        /// <summary>Udzial nowej reguly dzis (rozruch); przed pierwsza doba z kluczem - 0 (jak dotad).</summary>
        private static double Share()
        {
            if (_rampStart == -2) return 0.0;
            if (_rampStart < 0) return 1.0;
            var s = Settings.Current;
            int rd = s != null ? Math.Max(0, s.SutlerRampDays) : 0;
            int k = (int)CampaignTime.Now.ToDays - _rampStart;
            if (rd <= 0 || k + 1 >= rd) return 1.0;
            return Math.Max(0, k + 1) / (double)rd;
        }

        /// <summary>Limit oszczednosci partii: dotad `cap` (MenGearSaveDays x zold, z czescia N6), teraz najwyzej naprawy + cel; w rozruchu liniowo od starego
        /// do nowego. Klucz wylaczony - `cap` bez zmian. Bez wlasnego catch w arytmetyce (wolajacy ma swoj); odczyt slownika nie rzuca.</summary>
        internal static long Cap(MobileParty mp, long cap, long mend)
        {
            if (!On || mp == null || cap <= 0) return cap;
            double sh = Share();
            if (sh <= 0.0) return cap;
            long nc = Math.Min(cap, Math.Max(0L, mend) + WantOf(mp));
            long r = cap - (long)Math.Round((cap - nc) * sh);
            _dCapN++; _dCapOld += cap; _dCapNew += r;
            return r;
        }

        // ------------------------------------------------------------ doba (Sutlers.Daily, po markietanach - ta sama pozycja ksiegi MSutlers)
        internal static void Daily()
        {
            var s = Settings.Current;
            if (Campaign.Current == null || s == null) return;
            int today = (int)CampaignTime.Now.ToDays;
            if (!On)
            {
                _rampStart = -2;   // ponowne wlaczenie - rozruch od nowa
                LastGarTaken = -1; LastGarTown = LastGarCastle = 0;
                if (s.LogEnabled) Log.Info("Sakiewki wedlug potrzeby (m96): dzien " + today + " | wylaczone (Savings By Need / Men Purse Enabled / Men Upgrade Gear) - oszczednosci do Men Gear Save Days, jak dotad.");
                ClearDay();
                return;
            }
            var sw = Stopwatch.StartNew();
            long lordWage = SoldierPay.LastLordAcc, garWage = SoldierPay.LastGarAcc;
            if (_rampStart == -2) Decide(s, today, lordWage, garWage);
            double sh = Share();
            float rate = MBMath.ClampFloat(s.SutlerDailyShare, 0f, 1f);
            if (rate > 0f && sh > 0.0)
            {
                foreach (var t in Town.AllTowns) Garrison(t, rate, sh);
                foreach (var t in Town.AllCastles) Garrison(t, rate, sh);
            }
            sw.Stop();
            LastGarTaken = _dGarTaken; LastGarTown = _dGarTown; LastGarCastle = _dGarCastle;
            try { if (s.LogEnabled) Log.Info(Line(today, sh, rate, garWage, sw.Elapsed.TotalMilliseconds)); }
            catch (Exception e) { Stumble("Line", e); }
            ClearDay();
        }

        /// <summary>Rozruch (raz, pierwsza doba z kluczem): ile nowa regula uwolnilaby od razu - oszczednosci K1 partii rodow chronione dzis (MenPurse.KeptFor,
        /// stara regula: udzial 0 do konca tej decyzji) + wolna czesc sakiewek zalog (ponad naprawy i cel). Wiecej niz RampTriggerDays dnia zoldu partii rodow
        /// i zalog - liniowo w SutlerRampDays dob.</summary>
        private static void Decide(Settings s, int today, long lordWage, long garWage)
        {
            long lordPurse = 0, lordKept = 0, garPurse = 0, garFree = 0;
            try
            {
                foreach (var mp in MobileParty.AllLordParties)
                {
                    if (mp == null) continue;
                    lordPurse += Math.Max(0, MenPurse.Get(mp));
                    lordKept += Math.Max(0, MenPurse.KeptFor(mp));
                }
                foreach (var t in Town.AllTowns) GarFree(t, ref garPurse, ref garFree);
                foreach (var t in Town.AllCastles) GarFree(t, ref garPurse, ref garFree);
            }
            catch (Exception e) { Stumble("Daily.Ramp", e); }
            long w = lordWage + garWage, rel = lordKept + garFree;
            double d = w > 0 ? (double)(lordPurse + garPurse) / w : 0.0, dr = w > 0 ? (double)rel / w : 0.0;
            int rd = Math.Max(0, s.SutlerRampDays);
            bool ramp = rd > 0 && (w > 0 ? dr > RampTriggerDays : rel > 0);
            _rampStart = ramp ? today : -1;
            Log.Info("Sakiewki wedlug potrzeby (m96): rozruch - sakiewki partii rodow i zalog " + (lordPurse + garPurse) + " = " + d.ToString("0.0", Inv) + " dni zoldu (zold dnia " + w
                     + "); nowa regula uwolni " + rel + " = " + dr.ToString("0.0", Inv) + " dni zoldu (oszczednosci K1 partii rodow " + lordKept + ", wolne w sakiewkach zalog " + garFree + ")"
                     + (ramp ? " > " + RampTriggerDays.ToString("0", Inv) + " dnia: nowa regula wchodzi w " + rd + " dob od doby " + today + " (pole r zapisu arm_m96sak)"
                             : " - bez rozruchu, od razu nowa regula") + ".");
        }

        private static void GarFree(Town t, ref long purse, ref long free)
        {
            var g = t != null ? t.GarrisonParty : null;
            if (g == null) return;
            int p = MenPurse.Get(g);
            if (p <= 0) return;
            purse += p;
            free += Math.Max(0L, p - Math.Max(0, AiWear.OutstandingCost(g, null)) - WantOf(g));
        }

        /// <summary>Jedna zaloga: wolna sakiewka (ponad naprawy i cel) na zycie w osadzie. Od zdjecia do wplat tylko arytmetyka i ChangeGold.</summary>
        private static void Garrison(Town t, float rate, double sh)
        {
            try
            {
                var st = t != null ? t.Settlement : null;
                var g = t != null ? t.GarrisonParty : null;
                if (st == null || g == null || !g.IsGarrison || !g.IsActive || t.Settlement.Town == null) return;
                int purse = MenPurse.Get(g);
                if (purse <= 0) return;
                _dGar++; _dGarPurse += purse;
                int wage = Math.Max(0, MenPurse.WageRaw(g));
                _dGarWage += wage;
                int want = WantOf(g);
                if (purse <= want) { _dGarProt += purse; return; }   // cala sakiewka to cel - naprawy nawet nie liczone (koszt)
                if (st.IsUnderSiege) { _dGarSiege++; _dGarProt += purse; return; }   // oblezenie - karczmy zamkniete, jak dozbrajanie (ForAi)
                long mend = Math.Max(0, AiWear.OutstandingCost(g, null));   // robocizna bez szacunku materialu z polki (jak markietani - CampMend; tanszy odczyt)
                long prot = Math.Min(purse, mend + want);
                long free = purse - prot;
                _dGarProt += prot; _dGarFree += free;
                if (free <= 0) return;
                double fac = Math.Min(ByPurseCap, free / (double)Math.Max(1, wage) / ByPurseDays);
                int x = (int)Math.Min(free, (long)(free * Math.Min(1.0, rate * fac) * sh));
                if (x <= 0) return;
                if (CitySack.NoSpend(st, g, x)) { _dGarSack++; return; }   // lup miast L3: wojsko zdobywcy w lupionej osadzie nie placi (wlasny try; licznik L3 "zycie")
                // ---- od zdjecia do wplat tylko arytmetyka i ChangeGold; kontrola z odczytow przed/po (nie z tej samej zmiennej)
                int got = MenPurse.Take(g, x);
                _dGarOut += purse - MenPurse.Get(g);
                if (got <= 0) return;
                int folk = TownFolk.EarnService(st, got, TownFolk.KGarrison);   // E5/B1 (R9, 75/25): arytmetyka - nie rzuca
                long g0 = t.Gold;
                t.ChangeGold(got - folk);
                _dGarCoffer += t.Gold - g0;
                // ---- liczniki (zloto juz na miejscu)
                _dGarPaid++; _dGarTaken += got; _dGarFolk += folk;
                if (st.IsTown) _dGarTown += got; else _dGarCastle += got;
                ClanIncomeBook.NoteOwnPaid(st, st.OwnerClan, got, true);   // 169c: jak zold zalogi wydany na miejscu (tylko licznik, wlasny try)
                SoldierPay.Hold(st, got);                                    // tarcza zoldu (A1 wylaczone; wlasny try) - jak zold zalog do kas miast
                try { TownFolk.Note(st, got, TownFolk.KGarrison); } catch { }   // E5/0 (195): zold zalogi wydany na miejscu (tylko licznik)
            }
            catch (Exception e) { Stumble("Garrison", e); }
        }

        /// <summary>Dopisek linii "Obieg" (sakiewki ludzi): zalogi na zycie w osadzie tej doby; klucz wylaczony - pusto.</summary>
        internal static string ObiegNote()
        {
            if (LastGarTaken < 0) return "";
            return ", zalogi na zycie w osadzie (m96) " + LastGarTaken.ToString(Inv) + " (do kas miast " + LastGarTown.ToString(Inv) + ", do kas zamkow " + LastGarCastle.ToString(Inv) + ")";
        }

        private static string Line(int today, double sh, float rate, long garWageAcc, double ms)
        {
            var sb = new StringBuilder(896);
            int rd = Settings.Current != null ? Math.Max(0, Settings.Current.SutlerRampDays) : 0;
            string ramp = _rampStart >= 0 && sh < 1.0 ? "rozruch doba " + Math.Max(1, today - _rampStart + 1) + " z " + rd : "bez rozruchu";
            long ctl = _dGarOut - (_dGarCoffer + _dGarFolk);
            sb.Append("Sakiewki wedlug potrzeby (m96): dzien ").Append(today)
              .Append(" | udzial nowej reguly ").Append(sh.ToString("0.00", Inv)).Append(" (").Append(ramp).Append(')')
              .Append(" | cele: przegladow ").Append(_dVisits).Append(" (wezszych - zamek bez targu ").Append(_dVisitsNarrow).Append(", cel z targu zostal ").Append(_dKeptNarrow)
              .Append("), z celem ").Append(_dVisitsWant).Append(" (koszykow: lepsze za malo w sakiewce ").Append(_dBasketsUp).Append(", braki za malo pieniedzy ").Append(_dBasketsGap)
              .Append("; suma celow ").Append(_dWantSum).Append("), partii z celem razem ").Append(_want.Count)
              .Append(" | limit oszczednosci partii: wywolan ").Append(_dCapN).Append(", dotad ").Append(_dCapOld).Append(", teraz ").Append(_dCapNew)
              .Append(" | zalogi: ").Append(_dGar).Append(" z sakiewka ").Append(_dGarPurse).Append(" (zold dnia zalog ").Append(garWageAcc)
              .Append(" = ").Append(garWageAcc > 0 ? ((double)_dGarPurse / garWageAcc).ToString("0.0", Inv) : "-").Append(" dni), chronione (naprawy i cel) ").Append(_dGarProt)
              .Append(", wolne ").Append(_dGarFree).Append(", wydaly na zycie w osadzie ").Append(_dGarTaken).Append(" (").Append(_dGarPaid).Append(" zalog; stawka ")
              .Append((rate * 100f).ToString("0.0", Inv)).Append("% x min(3, dni wolnej / 10)) -> kasy miast ").Append(_dGarTown).Append(", kasy zamkow ").Append(_dGarCastle)
              .Append(", w tym do kies ludu ").Append(_dGarFolk).Append("; kontrola: zeszlo z sakiewek ").Append(_dGarOut).Append(" = przybylo w kasach osad ").Append(_dGarCoffer)
              .Append(" + do kies ludu ").Append(_dGarFolk).Append(" (roznica ").Append(ctl).Append(')')
              .Append(", pominiete: oblezone ").Append(_dGarSiege).Append(", lupione (L3; w linii lupu jako zycie) ").Append(_dGarSack)
              .Append(" | koszt ").Append(ms.ToString("0.0", Inv)).Append(" ms");
            if (_stumbles > 0) sb.Append(" | potkniecia ").Append(_stumbles).Append(" (pierwszy wyjatek z miejsca w logu jako ERROR)");
            sb.Append('.');
            return sb.ToString();
        }
    }
}
