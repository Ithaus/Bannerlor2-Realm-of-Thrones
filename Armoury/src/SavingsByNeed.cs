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
    /// oszczednosci K1 8.93 mln = 14.2 dni; zalogi 5.85 mln = 25.4 dni zoldu zalog (limit 30); karawany 0.78 mln. Na lepszy sprzet ludzie odkladaja
    /// 62-71 tys./d, a lordowie kupuja lepszy za 14-16 tys./d; pominiete koszyki: "brak lepszej na polce" 19.6-21.5 tys./d, "za malo w sakiewce" 44-120/d.
    /// Pieniadz prawie nigdy nie jest tym, czego brakuje - oszczednosci czekaja na towar, ktorego nie ma (N6 IdleKeep 0.5 x MenGearSaveDays 30 = 15 dni
    /// chronione w polu u partii bez zakupu od 14 dob, zalogi do 30 dni bez zadnego ujscia poza zakupami).
    ///
    /// Regula (jedna dla lordow AI, ludzi gracza i zalog): ludzie odkladaja tylko na sprzet, ktory widzieli na targu i na ktory ich nie bylo stac.
    ///  - Cel oszczedzania: przy kazdym przegladzie dozbrajania (MenUpgrade.Run) suma cen najtanszej lepszej sztuki w kazdym koszyku odrzuconym
    ///    "za malo w sakiewce" (jedna sztuka na koszyk). Przeglad bez takiego koszyka = cel 0. Zapis "arm_m96sak" (pole t).
    ///  - Partie (OnLeft i markietani - MenPurse.CampReserve): chroniona czesc sakiewki = najwyzej zalegle naprawy + cel (i nie wiecej niz dotad:
    ///    MenGearSaveDays x zold). Reszta jest wolna: w polu markietani (N6 bez zmian), w miescie przy wyjezdzie - zycie (jak dotad nadwyzka ponad limit).
    ///  - Zalogi: chronione = zalegle naprawy + cel; wolna reszta schodzi co dobe na zycie w ich osadzie ta sama miara co markietani N6
    ///    (SutlerDailyShare x min(3, dni wolnej sakiewki / 10)) - do kasy osady, 75/25 kupcy / lud (TownFolk.EarnService KGarrison), jak zold zalogi
    ///    wydany na miejscu. Wplyw zoldu do sakiewki zalogi (SoldierPay.GarrisonShare) bez zmian - zaloga ma z czego kupowac braki.
    ///  - Rozruch (pole r zapisu): pierwsza doba z kluczem - gdy sakiewki (partie rodow + zalogi) maja wiecej niz 15 dni zoldu (zapis Jeffa), nowa regula
    ///    wchodzi liniowo w SutlerRampDays (28) dob; nowa kampania - od razu. Klucz wylaczony - rozruch od nowa przy ponownym wlaczeniu.
    /// Zloto nie powstaje i nie znika: sakiewka zalogi -> kasa jej osady + kiesa ludu (posiadacze "Pieniadz swiata"); partie - te same drogi co dotad.
    /// Wyjatek na zalodze - licznik potkniec (bez globalnego wylacznika).
    /// </summary>
    internal static class SavingsByNeed
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const double RampTriggerDays = 15.0;   // prog etapu 5: sakiewki <= 15 dni zoldu
        private const double ByPurseCap = 3.0, ByPurseDays = 10.0;   // ta sama miara co markietani N6 (Sutlers)

        internal static bool On { get { var s = Settings.Current; return s != null && s.SavingsByNeed && MenPurse.On && MenUpgrade.On; } }

        // cel oszczedzania partii (StringId -> zl); zapis pole t
        private static readonly Dictionary<string, int> _want = new Dictionary<string, int>();
        // rozruch: -2 nie rozstrzygniety, -1 bez rozruchu albo skonczony, >= 0 doba startu
        private static int _rampStart = -2;
        // przeglad dozbrajania w toku (MenUpgrade.Run - watek glowny)
        private static bool _vOn; private static long _vSum; private static int _vN;
        // liczniki doby
        private static int _dVisits, _dVisitsWant, _dBaskets, _dGar, _dGarPaid, _dGarSiege, _dGarSack, _stumbles;
        private static long _dWantSum, _dGarPurse, _dGarProt, _dGarFree, _dGarTaken, _dGarTown, _dGarCastle, _dGarFolk, _dGarWage, _dCapOld, _dCapNew;
        private static int _dCapN;
        private static readonly HashSet<string> _err = new HashSet<string>();

        internal static void Reset()
        {
            _want.Clear(); _rampStart = -2; _vOn = false; _vSum = 0; _vN = 0; _stumbles = 0; _err.Clear();
            ClearDay();
        }

        private static void ClearDay()
        {
            _dVisits = _dVisitsWant = _dBaskets = _dGar = _dGarPaid = _dGarSiege = _dGarSack = 0;
            _dWantSum = _dGarPurse = _dGarProt = _dGarFree = _dGarTaken = _dGarTown = _dGarCastle = _dGarFolk = _dGarWage = 0;
            _dCapOld = _dCapNew = 0; _dCapN = 0;
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_err.Add(where)) Log.Error("SavingsByNeed." + where, e); } catch { }
        }

        // ------------------------------------------------------------ zapis "arm_m96sak": r=<rozruch>;t=id:zl,id:zl (stary zapis bez klucza = cele 0, rozruch nierozstrzygniety)
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
                        if (kv.Value <= 0 || !live.Contains(kv.Key) || kv.Key.IndexOfAny(bad) >= 0) continue;
                        sb.Append(first ? ";t=" : ",").Append(kv.Key).Append(':').Append(kv.Value.ToString(Inv));
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
                            int c = e.LastIndexOf(':');
                            if (c > 0 && int.TryParse(e.Substring(c + 1), NumberStyles.Integer, Inv, out x) && x > 0) _want[e.Substring(0, c)] = x;
                        }
                }
            }
            catch (Exception e) { Stumble("Import", e); }
        }

        // ------------------------------------------------------------ cel oszczedzania (MenUpgrade.Run)
        /// <summary>Poczatek przegladu dozbrajania jednej partii (ForAi / ForPlayer, przed Run). Nie rzuca.</summary>
        internal static void VisitBegin() { _vOn = true; _vSum = 0; _vN = 0; }

        /// <summary>MenUpgrade.Pick: koszyk odrzucony "za malo w sakiewce" - najtansza lepsza sztuka, na ktora ich nie bylo stac. Nie rzuca.</summary>
        internal static void NoteWant(int price)
        {
            if (!_vOn || price <= 0 || price == int.MaxValue) return;
            _vSum += price; _vN++;
        }

        /// <summary>Koniec przegladu: cel partii = suma z tego przegladu (0 - nie ma na co odkladac). Nie rzuca.</summary>
        internal static void VisitEnd(MobileParty mp)
        {
            if (!_vOn) return;
            _vOn = false;
            try
            {
                if (mp == null || mp.StringId == null) return;
                _dVisits++;
                if (_vSum > 0)
                {
                    _want[mp.StringId] = (int)Math.Min(int.MaxValue, _vSum);
                    _dVisitsWant++; _dBaskets += _vN; _dWantSum += _vSum;
                }
                else _want.Remove(mp.StringId);
            }
            catch (Exception e) { Stumble("VisitEnd", e); }
        }

        /// <summary>Rozbita / rozwiazana partia - bez celu (MenPurse.OnPartyDestroyed). Nie rzuca.</summary>
        internal static void Forget(MobileParty mp) { try { if (mp != null && mp.StringId != null) _want.Remove(mp.StringId); } catch { } }

        private static int WantOf(MobileParty mp) { int v; return mp != null && mp.StringId != null && _want.TryGetValue(mp.StringId, out v) ? v : 0; }

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
                if (s.LogEnabled) Log.Info("Sakiewki wedlug potrzeby (m96): dzien " + today + " | wylaczone (Savings By Need / Men Purse Enabled / Men Upgrade Gear) - oszczednosci do Men Gear Save Days, jak dotad.");
                ClearDay();
                return;
            }
            var sw = Stopwatch.StartNew();
            long lordPurse = 0, garPurse0 = 0;
            long lordWage = SoldierPay.LastLordAcc, garWage = SoldierPay.LastGarAcc;
            if (_rampStart == -2)
            {
                try
                {
                    foreach (var mp in MobileParty.AllLordParties) if (mp != null) lordPurse += Math.Max(0, MenPurse.Get(mp));
                    foreach (var t in Town.AllTowns) { var g = t != null ? t.GarrisonParty : null; if (g != null) garPurse0 += Math.Max(0, MenPurse.Get(g)); }
                    foreach (var t in Town.AllCastles) { var g = t != null ? t.GarrisonParty : null; if (g != null) garPurse0 += Math.Max(0, MenPurse.Get(g)); }
                }
                catch (Exception e) { Stumble("Daily.Ramp", e); }
                long w = lordWage + garWage;
                double d = w > 0 ? (double)(lordPurse + garPurse0) / w : 0.0;
                int rd = Math.Max(0, s.SutlerRampDays);
                bool ramp = rd > 0 && (w > 0 ? d > RampTriggerDays : (lordPurse + garPurse0) > 0);
                _rampStart = ramp ? today : -1;
                Log.Info("Sakiewki wedlug potrzeby (m96): rozruch - sakiewki partii rodow i zalog " + (lordPurse + garPurse0) + " = " + d.ToString("0.0", Inv) + " dni zoldu (zold dnia " + w + ")"
                         + (ramp ? " > " + RampTriggerDays.ToString("0", Inv) + " dni: nowa regula wchodzi w " + rd + " dob od doby " + today + " (pole r zapisu arm_m96sak)"
                                 : " - bez rozruchu, od razu nowa regula") + ".");
            }
            double sh = Share();
            float rate = MBMath.ClampFloat(s.SutlerDailyShare, 0f, 1f);
            if (rate > 0f && sh > 0.0)
            {
                foreach (var t in Town.AllTowns) Garrison(t, rate, sh);
                foreach (var t in Town.AllCastles) Garrison(t, rate, sh);
            }
            sw.Stop();
            try { if (s.LogEnabled) Log.Info(Line(today, sh, rate, garWage, sw.Elapsed.TotalMilliseconds)); }
            catch (Exception e) { Stumble("Line", e); }
            ClearDay();
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
                if (CitySack.NoSpend(st, g, x)) { _dGarSack++; return; }   // lup miast L3: wojsko zdobywcy w lupionej osadzie nie placi (wlasny try)
                // ---- od zdjecia do wplat tylko arytmetyka i ChangeGold
                int got = MenPurse.Take(g, x);
                if (got <= 0) return;
                int folk = TownFolk.EarnService(st, got, TownFolk.KGarrison);   // E5/B1 (R9, 75/25): arytmetyka - nie rzuca
                t.ChangeGold(got - folk);
                // ---- liczniki (zloto juz na miejscu)
                _dGarPaid++; _dGarTaken += got; _dGarFolk += folk;
                if (st.IsTown) _dGarTown += got; else _dGarCastle += got;
                ClanIncomeBook.NoteOwnPaid(st, st.OwnerClan, got, true);   // 169c: jak zold zalogi wydany na miejscu (tylko licznik, wlasny try)
                SoldierPay.Hold(st, got);                                    // tarcza zoldu (A1 wylaczone; wlasny try) - jak zold zalog do kas miast
                try { TownFolk.Note(st, got, TownFolk.KGarrison); } catch { }   // E5/0 (195): zold zalogi wydany na miejscu (tylko licznik)
            }
            catch (Exception e) { Stumble("Garrison", e); }
        }

        private static string Line(int today, double sh, float rate, long garWageAcc, double ms)
        {
            var sb = new StringBuilder(768);
            int rd = Settings.Current != null ? Math.Max(0, Settings.Current.SutlerRampDays) : 0;
            string ramp = _rampStart >= 0 && sh < 1.0 ? "rozruch doba " + Math.Max(1, today - _rampStart + 1) + " z " + rd : "bez rozruchu";
            long ctl = _dGarTaken - (_dGarTown + _dGarCastle);
            sb.Append("Sakiewki wedlug potrzeby (m96): dzien ").Append(today)
              .Append(" | udzial nowej reguly ").Append(sh.ToString("0.00", Inv)).Append(" (").Append(ramp).Append(')')
              .Append(" | cele: przegladow dozbrajania ").Append(_dVisits).Append(", z celem ").Append(_dVisitsWant).Append(" (koszykow za malo w sakiewce ").Append(_dBaskets)
              .Append(", suma celow ").Append(_dWantSum).Append("), partii z celem razem ").Append(_want.Count)
              .Append(" | limit oszczednosci partii: wywolan ").Append(_dCapN).Append(", dotad ").Append(_dCapOld).Append(", teraz ").Append(_dCapNew)
              .Append(" | zalogi: ").Append(_dGar).Append(" z sakiewka ").Append(_dGarPurse).Append(" (zold dnia zalog ").Append(garWageAcc)
              .Append(" = ").Append(garWageAcc > 0 ? ((double)_dGarPurse / garWageAcc).ToString("0.0", Inv) : "-").Append(" dni), chronione (naprawy i cel) ").Append(_dGarProt)
              .Append(", wolne ").Append(_dGarFree).Append(", wydaly na zycie w osadzie ").Append(_dGarTaken).Append(" (").Append(_dGarPaid).Append(" zalog; stawka ")
              .Append((rate * 100f).ToString("0.0", Inv)).Append("% x min(3, dni wolnej / 10)) -> kasy miast ").Append(_dGarTown).Append(", kasy zamkow ").Append(_dGarCastle)
              .Append(", w tym do kies ludu ").Append(_dGarFolk).Append("; kontrola: zdjete = do osad (roznica ").Append(ctl).Append(')')
              .Append(", pominiete: oblezone ").Append(_dGarSiege).Append(", lupione (L3) ").Append(_dGarSack)
              .Append(" | koszt ").Append(ms.ToString("0.0", Inv)).Append(" ms");
            if (_stumbles > 0) sb.Append(" | potkniecia ").Append(_stumbles).Append(" (pierwszy wyjatek z miejsca w logu jako ERROR)");
            sb.Append('.');
            return sb.ToString();
        }
    }
}
