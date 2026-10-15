using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// kiesa2 (decyzja Jeffa 15.10 "napraw to, najwyzej zwieksz podatki" - pieniadz ma wracac od ludu do lordow i miast) - PODATEK PANA OD NADWYZKI OSZCZEDNOSCI
    /// LUDU (regulator kiesy ludu). Klucz FolkHoardTax (wylaczony = 445641f1 co do monety), stawka FolkHoardTaxRate (0.01), poziom normalny FolkHoardNormalTimes (3),
    /// udzial pana FolkHoardTaxLordShare (0.67).
    ///  - DOWOD (test 4 lata b9cb462e, linie "Kiesa ludu", "Zbytki ludu (P3-P5)", "Danina z oszczednosci (P1)", "Danina pokoju (M4)", "Pieniadz swiata"): kiesy ludu
    ///    miast 6.4 mln (d0, zapas dni W 27.8) -> 46.4 mln (d1438, 154 dni), podzamcza 0.7 -> 7.0 mln; glowy rodow 63 -> 46 mln, kasy miast 26 -> 8.6 mln. Lud zarabia
    ///    ok. 0.8 doby potrzeby na dobe ponad to, co wydaje na towar (d1100-1460: wplywy 512 tys./d, towar 302 tys./d); jedyna czynna droga powrotu nadwyzki to
    ///    danina wojenna P1 (175 tys./d - tylko krolestwa w wojnie; w okresie wojen d360-720 kiesa stala, +4 tys./d); w pokoju (19 krolestw, lokata W 32 mln z 42)
    ///    nic poza danina pokoju M4 (chleb korony - ok. 250 zl/d), a zbytki P3 kupuja 8% oferty (oferta 680 tys./d, kupione 57 tys., bez towaru 212 tys.).
    ///  - REGULA (krok 5 doby ludu, zaraz po daninie P1 tej klasy - TownFolk.Live.cs TownLive / CastleLiveDay): klasa W / R / P miasta i podzamcze zamku; poziom
    ///    normalny = FolkHoardNormalTimes x zapas dni potrzeby (W TownFolkReserveDays 10 / R 21 / P 30 / podzamcze 10 -> 30 / 63 / 90 / 30 dob - tyle lud mial na
    ///    starcie kampanii: 27.8 / 55.1 / 82.8 doby W / R / P). Podatek dnia = stawka x (kiesa ponad poziom normalny PRZED danina tej doby) minus danina P1 / M4
    ///    tej doby z tej klasy (danina sie wlicza - w wojnie 2.5% P1 zwykle pokrywa stawke), najwyzej do poziomu normalnego (nic ponizej niego). Ulamek ponizej
    ///    1 zl przepada na korzysc ludu (w dol - nic z niczego).
    ///  - PLATNIK -> ODBIORCY: kiesa klasy -> glowa rodu pana osady (FolkHoardTaxLordShare, ChangeHeroGold + PopulationLaw.RentToday jak podatki panu PayLord - dochod
    ///    ziemi D rodu) i kasa miasta / zamku (reszta, Town.ChangeGold). Bez zywego pana - calosc do kasy osady. Osada oblezona - nic (BesiegedPay KFolkHoard, jak P1).
    ///    Gracz na tych samych zasadach co AI (jak podatki panu). Ksiega kies: _ctlOut i LDayOut (kontrola zamknieta "wydatki", wiersz kiesa-ludu.csv).
    ///  - Nadwyzka ponad zapas dni po poborze (f.DOvC, f.DOver) - od niej oferta zbytkow, place R/P z nadwyzki i W7 (TownFolk.LevyRoom) - zadna zlotowka dwa razy.
    ///  - Linie: "Podatek od nadwyzki ludu (kiesa2): start sesji" i "... dzien N" (pobrane, komu, kiesa W przed / po wobec poziomu normalnego, kontrola);
    ///    dopisek w "Obieg" (rody dostaly, kasy miast, kasy zamkow) - TownFolk.LastHoard*. Zapis gry: nic nowego (liczniki w pamieci doby).
    /// Wyjatek per osada / klasa - licznik potkniec (pierwszy do pliku), bez globalnych wylacznikow (CLAUDE.md 7); przelew w kolejnosci kiesa -> pan -> kasa,
    /// wyjatek przelewu oddaje zloto do kiesy.
    /// </summary>
    internal static partial class TownFolk
    {
        // ---- liczniki doby (HoardBegin zeruje)
        private static readonly long[] _hdTake = new long[4];      // pobrane: W, R, P, podzamcze
        private static long _hdLord, _hdTown, _hdCastle, _hdNoLord, _hdPlayer, _hdHeld, _hdTicks;
        private static int _hdHeldN, _hdCovered, _hdOver, _hdUnder, _hdBelow, _hdStumbles, _hdStumbleSess;
        private static double _hdW0, _hdW1, _hdNormW, _hdE0, _hdE1;   // kiesa W (miasta i podzamcza) przed / po poborze, poziom normalny W, nadwyzka ponad poziom normalny przed / po
        private static int _hdTownsW, _hdCastlesW;                  // osady z potrzeba W (miara kiesy W)
        private static readonly HashSet<Settlement> _hdPlaces = new HashSet<Settlement>();
        private static readonly HashSet<Clan> _hdClans = new HashSet<Clan>();
        private static readonly HashSet<string> _hdErr = new HashSet<string>();
        private static string _lineHoard, _lineHoardStart;
        private static bool _hdStartDone;
        /// <summary>"Obieg": podatek od nadwyzki ludu tej doby - do glow rodow, do kas miast, do kas zamkow (zerowane w TownFolk.Daily i HoardBegin).</summary>
        internal static long LastHoardLord, LastHoardTown, LastHoardCastle;

        private static bool HoardKey { get { var s = Settings.Current; return s != null && s.FolkHoardTax; } }
        private static double HdClamp(double v, double lo, double hi) { return double.IsNaN(v) ? lo : (v < lo ? lo : (v > hi ? hi : v)); }
        private static double HoardRate(Settings s) { return HdClamp(s.FolkHoardTaxRate, 0, 0.1); }
        /// <summary>obieg2: stawka doby - przy strazniku obiegu stawka policzona na poczatku doby ludu (GuardBegin), inaczej FolkHoardTaxRate jak w kiesa2.</summary>
        private static double HoardRateNow(Settings s) { return GuardOn && _gdReady ? _gdRate : HoardRate(s); }
        private static double HoardTimes(Settings s) { return HdClamp(s.FolkHoardNormalTimes, 1, 20); }
        private static double HoardLordShare(Settings s) { return HdClamp(s.FolkHoardTaxLordShare, 0, 1); }

        private static void ResetHoard()
        {
            HoardBegin();
            _hdStumbleSess = 0; _hdErr.Clear();
            _lineHoard = _lineHoardStart = null; _hdStartDone = false;
            ResetGuard();    // obieg2: straznik obiegu i podatek od nadwyzki wsi (stan z zapisu arm_obieg2 wczytuje Import po Reset)
            ResetLoans();    // obieg2: pozyczki od ludu (zapis arm_obieg2)
            ResetCrownLoans();   // korona2: pozyczki koron od miast i mieszczan (zapis arm_korona2 wczytuje Import po Reset)
        }

        private static void HoardBegin()
        {
            Array.Clear(_hdTake, 0, 4);
            _hdLord = _hdTown = _hdCastle = _hdNoLord = _hdPlayer = _hdHeld = _hdTicks = 0;
            _hdHeldN = _hdCovered = _hdOver = _hdUnder = _hdBelow = _hdStumbles = 0;
            _hdW0 = _hdW1 = _hdNormW = _hdE0 = _hdE1 = 0; _hdTownsW = _hdCastlesW = 0;
            _hdPlaces.Clear(); _hdClans.Clear();
            LastHoardLord = LastHoardTown = LastHoardCastle = 0;
        }

        private static void HoardStumble(string where, Exception e)
        {
            _hdStumbles++; _hdStumbleSess++;
            Stumble("Hoard." + where, e);
            try { _hdErr.Add(where); } catch { }
        }

        /// <summary>
        /// Podatek od nadwyzki klasy `c` osady (miasto 0/1/2, zamek 0 - podzamcze; `slot` licznika 0-3) - w kroku 5 doby ludu ZARAZ po daninie P1 tej klasy.
        /// `ov` - kiesa ponad zapas `rd` dni PO daninie (tak liczy wolajacy), `aid` - danina P1 / M4 tej doby z tej klasy. Zwraca pobrane (zdjete z kiesy) -
        /// wolajacy odejmuje je od nadwyzki przed oferta i placami z nadwyzki. Klucz wylaczony, brak potrzeby, kiesa nie ponad poziom normalny, danina pokryla stawke,
        /// oblezenie - 0. Nie rzuca.
        /// </summary>
        private static long PayHoard(Folk f, int c, int slot, double ov, long aid, double rd)
        {
            if (!HoardKey) return 0;
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                var s = Settings.Current;
                double need = f.Need[c];
                if (!(need > 0) || double.IsNaN(ov) || double.IsInfinity(ov)) return 0;
                double room = ov - (HoardTimes(s) - 1) * rd * need;    // kiesa ponad poziom normalny TERAZ (po daninie tej doby)
                if (!(room >= 1)) { _hdUnder++; return 0; }
                _hdOver++;
                double due = HoardRateNow(s) * (room + Math.Max(0, aid)) - Math.Max(0, aid);   // stawka od nadwyzki sprzed daniny; danina tej doby sie wlicza (obieg2: stawka straznika)
                if (!(due >= 1)) { if (aid > 0) _hdCovered++; _hdE0 += room; _hdE1 += room; return 0; }
                long x = (long)Math.Floor(Math.Min(due, room));
                x = Math.Min(x, Math.Max(0, PurseOf(f, c)));
                if (x > int.MaxValue) x = int.MaxValue;
                if (x <= 0) { _hdE0 += room; _hdE1 += room; return 0; }
                var st = f.St;
                var town = st != null ? st.Town : null;
                if (town == null) return 0;
                if (BesiegedPay.Holds(st, BesiegedPay.KFolkHoard, x, true)) { _hdHeld += x; _hdHeldN++; _hdE0 += room; _hdE1 += room; return 0; }
                var clan = st.OwnerClan;
                var lord = clan != null ? clan.Leader : null;
                bool lordOk = lord != null && lord.IsAlive;
                // obieg2 (straznik obiegu): czesc korony najpierw (skarbiec krolestwa rodu pana osady), reszta jak w kiesa2; klucz wylaczony - cp = 0 i podzial co do monety jak dotad
                var kd = GuardCrownOf(clan);
                long cp = kd != null ? (long)Math.Round(x * GuardCrownShare(s), MidpointRounding.AwayFromZero) : 0;
                if (cp > x) cp = x;
                if (cp < 0) cp = 0;
                long rest = x - cp;
                long lp = lordOk ? (long)Math.Round(rest * HoardLordShare(s), MidpointRounding.AwayFromZero) : 0;
                if (lp > rest) lp = rest;
                if (lp < 0) lp = 0;
                long tp = rest - lp;
                // przelew: kiesa klasy -> glowa rodu -> kasa osady -> skarbiec korony; wyjatek przy przelewie oddaje zloto do kiesy (nic z niczego, nic w nicosc)
                AddPurse(f, c, -x);
                if (lp > 0)
                {
                    try { lord.ChangeHeroGold((int)lp); }
                    catch (Exception e) { AddPurse(f, c, x); HoardStumble("PayHoard(pan)", e); _hdE0 += room; _hdE1 += room; return 0; }
                }
                if (tp > 0)
                {
                    try { town.ChangeGold((int)tp); }
                    catch (Exception e) { AddPurse(f, c, tp); x -= tp; tp = 0; HoardStumble("PayHoard(kasa)", e); }
                }
                if (cp > 0)
                {
                    try { kd.KingdomBudgetWallet += (int)cp; }
                    catch (Exception e) { AddPurse(f, c, cp); x -= cp; cp = 0; HoardStumble("PayHoard(korona)", e); }
                    if (cp > 0) GuardCrownReserve(kd, cp);   // korona2 (A): zapas korony, nie wplyw dnia (nie renty 180 tego dnia)
                }
                if (x <= 0) { _hdE0 += room; _hdE1 += room; return 0; }
                _ctlOut += x; f.LDayOut += x;                          // ksiega kies: wydatek (kontrola zamknieta, wiersz kiesa-ludu.csv)
                f.DOver -= x; if (c >= 0 && c < 3) f.DOvC[c] = Math.Max(0, f.DOvC[c] - x);   // oferta, place z nadwyzki i W7 od reszty
                _hdTake[slot] += x - cp; _hdE0 += room; _hdE1 += room - x;   // obieg2: "pobrano" linii kiesa2 = do panow + do kas (czesc korony osobno - _gdCrownT)
                if (cp > 0) { _gdCrownT += cp; _gdCrownTN++; }
                try
                {
                    _hdPlaces.Add(st);
                    if (lp > 0)
                    {
                        int r0; PopulationLaw.RentToday.TryGetValue(clan, out r0);
                        PopulationLaw.RentToday[clan] = (int)Math.Min(int.MaxValue, (long)r0 + lp);   // dochod ziemi D rodu (jak podatki panu PayLord)
                        CirculationWindows.NoteHeroGold(lord, lp);
                        _hdLord += lp; _hdClans.Add(clan);
                        if (lord == Hero.MainHero) _hdPlayer += lp;
                    }
                    if (tp > 0) { if (f.Kind == 0) _hdTown += tp; else _hdCastle += tp; if (!lordOk) _hdNoLord += tp; }
                    if (PurseOf(f, c) + 0.5 < HoardTimes(s) * rd * need) _hdBelow++;   // kontrola: nic ponizej poziomu normalnego (oczekiwane 0)
                }
                catch (Exception e) { HoardStumble("PayHoard(liczniki)", e); }
                return x;
            }
            catch (Exception e) { HoardStumble("PayHoard", e); return 0; }
            finally { _hdTicks += Stopwatch.GetTimestamp() - t0; }
        }

        /// <summary>Miara linii doby: kiesa W miasta (podzamcza zamku) przed i po poborze kiesa2 oraz poziom normalny W (wolane przy potrzebie W > 0, takze bez nadwyzki).</summary>
        private static void HoardW(Folk f, double rd, long took)
        {
            if (!HoardKey) return;
            try
            {
                double need = f.Need[0];
                if (!(need > 0)) return;
                _hdW1 += f.KW; _hdW0 += f.KW + Math.Max(0, took);
                _hdNormW += HoardTimes(Settings.Current) * rd * need;
                if (f.Kind == 0) _hdTownsW++; else _hdCastlesW++;
            }
            catch (Exception e) { HoardStumble("HoardW", e); }
        }

        /// <summary>Koniec doby ludu (DailyLive, po miastach i zamkach): linia startu sesji (raz), linia doby (klucz wlaczony) i liczby dla "Obieg".</summary>
        private static void HoardDay(int day)
        {
            var s = Settings.Current;
            if (s == null) return;
            double times = HoardTimes(s), rate = HoardRate(s), share = HoardLordShare(s);
            double rdW = Math.Max(1, Math.Min(60, s.TownFolkReserveDays));
            string levels = "W " + F1(times * rdW) + " / R " + F1(times * RdR) + " / P " + F1(times * RdP) + " / podzamcze " + F1(times * RdC) + " dob potrzeby";
            if (!_hdStartDone)
            {
                _hdStartDone = true;
                _lineHoardStart = "Podatek od nadwyzki ludu (kiesa2): start sesji | klucz Folk Hoard Tax " + (s.FolkHoardTax ? "tak" : "nie")
                                  + ", kiesa ludu czynna tak, podzamcza (Castle Purse Enabled) " + (CastleLive ? "tak" : "nie")
                                  + ", oblezona osada nie placi (Besieged Pay Nothing) " + (BesiegedPay.On ? "tak" : "nie")
                                  + " | regula: co dobe stawka " + (100 * rate).ToString("0.00", Inv) + "% kiesy klasy ponad poziom normalny (przed danina tej doby) minus danina P1 / M4"
                                  + " tej doby z tej klasy, najwyzej do poziomu normalnego; poziom normalny " + times.ToString("0.0#", Inv) + " x zapas dni (" + levels + ")"
                                  + "; pan " + (100 * share).ToString("0", Inv) + "% (glowa rodu, renta ziemi D), kasa miasta / zamku " + (100 * (1 - share)).ToString("0", Inv)
                                  + "%; bez zywego pana - calosc do kasy osady | klucz wylaczony = 445641f1.";
            }
            LastHoardLord = _hdLord; LastHoardTown = _hdTown; LastHoardCastle = _hdCastle;
            if (!s.FolkHoardTax) { _lineHoard = null; return; }
            long take = _hdTake[0] + _hdTake[1] + _hdTake[2] + _hdTake[3];
            int towns = 0, castles = 0;
            foreach (var st in _hdPlaces) { if (st == null) continue; if (st.IsTown) towns++; else castles++; }
            long diff = take - _hdLord - _hdTown - _hdCastle;
            var sb = new StringBuilder(1536);
            sb.Append("Podatek od nadwyzki ludu (kiesa2): dzien ").Append(day)
              .Append(" | stawka ").Append((100 * HoardRateNow(s)).ToString("0.00", Inv)).Append("%/d nadwyzki ponad poziom normalny ").Append(times.ToString("0.0#", Inv)).Append(" x zapas dni (")   // obieg2: stawka doby (przy strazniku obiegu - stawka straznika)
              .Append(levels).Append("), pan ").Append((100 * share).ToString("0", Inv)).Append("% / kasa osady ").Append((100 * (1 - share)).ToString("0", Inv)).Append("%; danina P1 / M4 tej doby wliczona")
              .Append(" | pobrano ").Append(take).Append(" z ").Append(towns + castles).Append(" osad (miasta ").Append(towns).Append(", zamki ").Append(castles)
              .Append("; rodow ").Append(_hdClans.Count).Append("): W ").Append(_hdTake[0]).Append(" / R ").Append(_hdTake[1]).Append(" / P ").Append(_hdTake[2])
              .Append(" / podzamcza ").Append(_hdTake[3])
              .Append(" | do glow rodow ").Append(_hdLord).Append(" (w tym gracz ").Append(_hdPlayer).Append("), do kas miast ").Append(_hdTown).Append(", do kas zamkow ").Append(_hdCastle)
              .Append(" (w tym bez zywego pana ").Append(_hdNoLord).Append(")")
              .Append(" | danina P1 / M4 pokryla stawke: ").Append(_hdCovered).Append(" klaso-osad | wstrzymane w oblezeniu ").Append(_hdHeld).Append(" (").Append(_hdHeldN).Append(")")
              .Append(" | kiesa W (miasta ").Append(_hdTownsW).Append(" i podzamcza ").Append(_hdCastlesW).Append(") przed / po ").Append(N0(_hdW0)).Append(" / ").Append(N0(_hdW1))
              .Append(", poziom normalny W ").Append(N0(_hdNormW)).Append(" (kiesa W / poziom normalny ").Append(_hdNormW > 0 ? F2(_hdW1 / _hdNormW) : "-").Append(")")
              .Append(" | ponad poziom normalny (klasy ponad zapas dni) przed / po ").Append(N0(_hdE0)).Append(" / ").Append(N0(_hdE1))
              .Append(", klaso-osad ponad poziom normalny ").Append(_hdOver).Append(", ponad zapas dni, nie ponad poziom normalny ").Append(_hdUnder)
              .Append(" | kontrola: pobrano - do panow - do kas ").Append(diff).Append(", ponizej poziomu normalnego po poborze ").Append(_hdBelow).Append(" (oczekiwane 0 / 0)")
              .Append(GuardHoardNote())   // obieg2: czesc korony z miast i zamkow (poza "pobrano" wyzej) i stawka straznika - przed "czas" (narzedzie kiesa2 czyta pola wyzej)
              .Append(" | czas ").Append(Ms(_hdTicks)).Append(" ms | potkniecia ").Append(_hdStumbles).Append(" (sesja ").Append(_hdStumbleSess).Append(").");
            _lineHoard = sb.ToString();
        }

        /// <summary>TakeLiveLines: linia startu sesji i linia doby podatku od nadwyzki - zeruje.</summary>
        private static void TakeHoardLines(List<string> l)
        {
            if (_lineHoardStart != null) l.Add(_lineHoardStart);
            if (_lineHoard != null) l.Add(_lineHoard);
            _lineHoardStart = _lineHoard = null;
        }
    }
}
