using System;
using System.Diagnostics;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// N2-E4 (noc 16.10, galaz noc-ekonomia2; raport audytu ekonomii 15.10 rozdz. 1.8 "opal w zimie" i rozdz. 3.2 P7) - OPAL DLA MIESZCZAN (klucz TownFolkFirewood).
    /// Dowod (raport audytu, test 4-letni, 4. rok w zimie): zapas drewna swiata 41 555 -> 96 326, indeks 0.46-0.56, budowy 1 391 -> 780 ladunkow na dobe - drewno
    /// w zimie nie ma ujscia; mieszczanie pala 55-100 ladunkow na dobe (koszyk BK, TownUseHardwood) wobec ok. 11 500 we wzorcu. Autotest 15.10 19:59 (lato):
    /// "Drewno: ... zapas: miasta 4234", zapas rzemiosla miast razem 4115 (40 miast ponizej) - latem drewna ponad zapas rzemiosla jest malo.
    /// Przyczyna [K]: lud miast kupuje drewno tylko w koszyku BK (budzet kategorii hardwood x TownUseHardwood) - nikt nie grzeje domow, a zima nic nie zmienia.
    /// Regula (DECYZJA CLAUDE noc 16.10; wzor zakupu - konie robocze K1-3a, TownFolk.Horses.cs): raz na dobe, po koniach roboczych i dymarkach (kotwica na jutro
    /// policzona, drewno dymarek kupione), kazde miasto z kiesa ludu (Live, osada skalibrowana) potrzebuje FwPer1000 (0.1) ladunku drewna (100 kg) na 1000 glow
    /// kiesy ludu (te same glowy, ktore placa za konia: glowy BK x skala ludzi rynku); latem (pora ekonomii WesterosClimate.Now) x FwSummer (0.5 - gotowanie, bez
    /// grzania), w zimie (WinterBite.WinterNow - zima ROT) x (1 + czynnik krainy WinterRealm.OfTown x gradient polnocy WinterBite.NorthOf) - POPRAWKA PO RECENZJI
    /// 16.10: ta sama sila zimy, ktora mnozy kazdy inny skutek zimy (ciecie plonu wsi WinterBite.CutOf: procent x Northness x kraina), nie druga regula na jedno
    /// zjawisko; czynnik krainy to 1 dla calego Westeros poza cieplymi krainami (WinterWarmRealmFactor 0.4) i Wyspami Letnimi (0), gradient 0.75 (Dorne) - 1.25
    /// (daleka Polnoc) przy NorthGradientPercent 25: wiekszosc Westeros ok. x1.75-2.25, cieple x1.3-1.5, Wyspy Letnie x1. Pora zima przy wylaczonym
    /// WinterBiteEnabled - x1 (skutki zimy wylaczone), linia doby mowi to wprost ("zima bez skutkow").
    /// Swiat (ok. 1.52 mln glow): ok. 75 ladunkow na dobe latem, ok. 150 wiosna i jesienia, ok. 300 w zimie. Ulamek - zaokraglenie losowe
    /// (MBRandom.RoundRandomized), bez pamieci i bez zapisu. Kupuje z polki TEGO miasta tylko drewno ponad zapas rzemiosla (CaravanBulk.TargetFor - ten sam prog,
    /// ponizej ktorego budowy nie biora drewna, K1 BuildWoodAboveCraftStock) i ponad ShelfKeep (5): kuznie, dymarki, warsztaty i deski maja pierwszenstwo; ponad
    /// zapasem opal kupuje przed budowami w kolejnosci doby (latem ok. 75 ladunkow wobec ok. 1 080 budow). Latem, gdy drewna jest tyle, ile rzemioslo potrzebuje,
    /// opalu kupuje sie malo, a zima, gdy zapas rosnie - pelna potrzebe. Cena polki sztuka po sztuce (ShelfBuy.Take, jak koszyk BK). Placi kiesa ludu (klasy W/R/P proporcjonalnie do wolnych pieniedzy) TYLKO z pieniedzy ponad jutrzejszy koszyk klasy (Need z kotwicy - opal
    /// nie odbiera chleba, jak kon roboczy); akcyza TownFolkExcise do gminy, reszta do kasy miasta (sprzedawca polki) - ta sama regula co PayForGoods E5/B1.
    /// Drewno znika ze swiata jako spalone: ksiega towarow - ramka konsumpcji osady (pozycja "mieszczanie"); ksiega drewna OreLedger liczy je jak koszyk BK
    /// mieszczan (w "bez wyjasnienia" linii "Drewno:" - bez nowej pozycji, format linii bez zmian). Pusta polka albo za mala kiesa - lud obywa sie bez opalu
    /// tego dnia (licznik; nic nie powstaje i nic nie ginie). Zamki - bez zmian (podzamcze). Kazde miasto we wlasnym try, licznik potkniec (CLAUDE.md 7).
    /// Wylaczony klucz albo kiesa nieczynna - nic (linia doby mowi dlaczego).
    /// </summary>
    internal static partial class TownFolk
    {
        private const double FwPer1000 = 0.1;      // DECYZJA CLAUDE: ladunku drewna na 1000 glow kiesy ludu na dobe (wiosna i jesien)
        private const double FwSummer = 0.5;       // DECYZJA CLAUDE: latem tylko gotowanie (raport audytu P7: "latem malo")
        private static int _fwStumbles;
        private static string _fwState;

        /// <summary>Z ArmouryBehavior.OnSessionLaunched: linia startu sesji (klucz, stawka, zima).</summary>
        internal static void FirewoodSessionStart()
        {
            _fwStumbles = 0; _fwState = null;
            var s = Settings.Current;
            bool on = s != null && s.TownFolkFirewood;
            Log.Info("Opal mieszczan (N2-E4): start sesji | klucz TownFolkFirewood " + (on ? "TAK" : "NIE | WYLACZONE (MCM) - drewno tylko w koszyku BK mieszczan, jak dotad")
                     + " | potrzeba " + FwPer1000.ToString("0.00", Inv) + " ladunku (100 kg) na 1000 glow kiesy ludu na dobe, latem x" + FwSummer.ToString("0.0", Inv) + ", w zimie ROT x (1 + czynnik krainy x gradient polnocy - jak kazdy skutek zimy: wiekszosc Westeros ok. x1.75-2.25, cieple x1.3-1.5, Wyspy Letnie x1)"
                     + " | tylko drewno ponad zapas rzemiosla miasta (CaravanBulk.TargetFor) i ponad " + ShelfKeep + " szt.; placi kiesa ludu ponad jutrzejszy koszyk, akcyza do gminy, reszta do kasy miasta"
                     + " | kiesa ludu " + (LiveKey ? "czynna" : "NIECZYNNA - brak platnika") + " | potkniecia 0.");
            try { OpalSessionStart(); } catch (Exception e) { _fwStumbles++; Stumble("OpalSessionStart", e); }   // f4-opal: pamiec paczki od nowa, linia startu, proba zimy teraz (autotest)
        }

        /// <summary>
        /// Raz na dobe, po WorkHorsesDaily i Bloomery.Daily (kotwica na jutro policzona, okna doby wyczyszczone, drewno dymarek kupione): opal kazdego miasta z polki tego miasta. Przelew kiesa -> kasa
        /// liczy sie w kontroli zamknietej kies nastepnej doby (_ctlOut) i w podstawie robocizny handlu (PaidKeep) - jak zaplata ludu za towar i kon roboczy.
        /// </summary>
        internal static void FirewoodDaily()
        {
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                var s = Settings.Current;
                if (s == null || Campaign.Current == null || !Campaign.Current.GameStarted) return;
                int day = (int)CampaignTime.Now.ToDays - 1;
                if (!s.TownFolkFirewood || !Live)
                {
                    string why = !s.TownFolkFirewood ? "WYLACZONE (Town Folk Firewood = off)" : "NIECZYNNE (kiesa ludu nieczynna - brak platnika)";
                    OpalIdle();   // f4-opal: pierscien dowozu od nowa, popyt ceny drewna bez reguly paczki
                    if (_fwState != why)
                    {
                        _fwState = why;
                        Log.Info("Opal mieszczan (N2-E4): dzien " + day + " | " + why + " - drewno tylko w koszyku BK mieszczan, jak dotad.");
                    }
                    return;
                }
                _fwState = null;
                EnsureImported();
                var wood = DefaultItems.HardWood;
                if (wood == null)
                {
                    Log.Info("Opal mieszczan (N2-E4): dzien " + day + " | BRAK przedmiotu hardwood - nic.");
                    return;
                }
                double exc = Math.Max(0f, Math.Min(0.2f, s.TownFolkExcise));
                bool winter = WinterBite.WinterNow();
                // pora ekonomii niezaleznie od WinterBiteEnabled (etykieta linii): zima przy wylaczonych skutkach zimy - x1 i "zima bez skutkow"
                bool summer = false, winterOff = false;
                if (!winter)
                {
                    try { var se = WesterosClimate.Now(); summer = se == CampaignTime.Seasons.Summer; winterOff = se == CampaignTime.Seasons.Winter; }
                    catch { summer = false; winterOff = false; }
                }
                var el = new EquipmentElement(wood, null, null, false);
                var op = OpalBegin();   // f4-opal: null - zaden klucz paczki nie czynny (stara droga co do bitu)
                int towns = 0, noCal = 0, waitShelf = 0, waitPurse = 0, part = 0, winterTowns = 0;
                double heads = 0, need = 0, realmSum = 0;
                long want = 0, bought = 0, gold = 0, excise = 0, short0 = 0, kept = 0;
                long fromPurse = 0, toTowns = 0, toComm = 0;   // obieg zlota liczony osobno: zeszlo z kies klas W/R/P, wplacono do kas miast, do gmin
                foreach (var f in _fk)
                {
                    if (f.Kind != 0) continue;
                    try
                    {
                        var town = f.St != null ? f.St.Town : null;
                        if (town == null || !town.IsTown) continue;
                        if (op != null) OpalRoll(op, f.St);   // f4-opal: probka pierscienia dowozu drewna (kazde miasto, takze bez potrzeby)
                        if (!f.Cal) { noCal++; continue; }
                        towns++;
                        // glowy kiesy ludu - te same co konie robocze (glowy BK x skala ludzi rynku; po dzisiejszej dobie ludu - jej skala)
                        double people = Math.Max(0, f.HW + f.HR + f.HP + f.HS) * (f.ADay == _liveDay ? Math.Max(0, f.DR) : Scale(f));
                        if (double.IsNaN(people) || double.IsInfinity(people)) people = 0;
                        heads += people;
                        double k = summer ? FwSummer : 1.0;
                        if (winter)
                        {
                            double rf = Math.Max(0.0, Math.Min(1.0, WinterRealm.OfTown(town))) * Math.Max(0.0, WinterBite.NorthOf(f.St));
                            k = 1.0 + rf; realmSum += rf; winterTowns++;
                        }
                        double x = people / 1000.0 * FwPer1000 * k;
                        // f4-opal krok 1 (Firewood Ks Season): do czesci KS czynnik pory zawsze (latem FwSummer) - jedna regula pory dla obu drog; klucz wylaczony - jak dotad (tylko zima)
                        double kz = op != null && op.Season ? k : (winter ? k : 1.0);
                        if (op == null)
                            x = StartCalibration.Zc4Firewood(town, x, kz);   // ZC4 (KS etap 2): (1 - lambda) x dzis + lambda x 1.5 kg na osobe (tabela KS) x zima (recenzja 16.10); poza ZC4 - bez zmian
                        else
                        {
                            x = OpalNeed(op, town, x, kz);                   // f4-opal krok 2: czesc KS najwyzej do dowozu drewna ponad rzemioslo (klucz Firewood Ks Supply Cap); poza ZC4 - bez zmian
                            OpalKeepNote(op, town, wood);                    // f4-opal krok 3: zapas rzemiosla + drewno dymarek wsi (tylko licznik linii)
                        }
                        need += x;
                        int n = MBRandom.RoundRandomized((float)x);
                        if (n <= 0) continue;
                        want += n;
                        var shelf = f.St.ItemRoster;
                        int have = shelf != null ? shelf.GetItemNumber(wood) : 0;
                        int keep = Math.Max(ShelfKeep, CaravanBulk.TargetFor(town, wood));   // zapas rzemiosla (kuznie, dymarki, warsztaty, deski) ma pierwszenstwo
                        kept += Math.Min(have, keep);
                        int room = have - keep;
                        if (room <= 0) { waitShelf++; short0 += n; continue; }
                        int take = Math.Min(n, room);
                        if (take < n) { part++; short0 += n - take; }
                        long avail = WhAvail(f);
                        if (avail <= 0) { waitPurse++; short0 += take; continue; }
                        var md = town.MarketData;
                        int p0 = md.GetPrice(el);
                        if (p0 <= 0) { waitShelf++; short0 += take; continue; }
                        int cost = 0, got = 0;
                        int budget = (int)Math.Min(int.MaxValue, avail);
                        var gf = GoodsLedger.Begin(GoodsLedger.FCons, town);         // ksiega towarow: spalone przez mieszczan (pozycja "mieszczanie")
                        try
                        {
                            int first, last;
                            got = ShelfBuy.Take(shelf, el, take, budget, () => md.GetPrice(el), out cost, out first, out last, p0, false);
                            if (got > 0 && !WhSplit(cost, avail))
                            {
                                // nie powinno sie zdarzyc (Take nie wydaje ponad budzet = wolne pieniadze) - drewno wraca na polke w tej samej ramce, nikt nie placi
                                shelf.AddToCounts(el, got);
                                MarketSales.NoteShelf(shelf, el, got, cost, -1);   // 241: zakup cofniety - sprzedaz miasta tez (Take ja zapisal)
                                _fwStumbles++; got = -1;
                            }
                        }
                        finally { GoodsLedger.End(gf); }
                        if (got <= 0) { waitPurse++; short0 += take; continue; }
                        if (got < take) short0 += take - got;
                        long ex = (long)Math.Round(cost * exc), kp = cost - ex;
                        f.KW -= _whP[0]; f.KR -= _whP[1]; f.KP -= _whP[2]; f.KG += ex;
                        fromPurse += _whP[0] + _whP[1] + _whP[2]; toComm += ex;
                        if (kp > 0) { town.ChangeGold((int)kp); toTowns += kp; }
                        _ctlOut += kp; f.PaidKeep += kp; f.LDayOut += cost;
                        bought += got; gold += cost; excise += ex;
                    }
                    catch (Exception e) { _fwStumbles++; Stumble("FirewoodDaily(miasto)", e); }
                }
                var sb = new StringBuilder(700);
                sb.Append("Opal mieszczan (N2-E4): dzien ").Append(day).Append(" | CZYNNE | miasta ").Append(towns).Append(": glowy kiesy ludu ").Append(N0(heads))
                  .Append(" | pora: ").Append(winter ? "ZIMA (x (1 + kraina x gradient polnocy), srednio x" + F2(winterTowns > 0 ? 1.0 + realmSum / winterTowns : 1.0) + ")"
                          : (summer ? "lato (x" + FwSummer.ToString("0.0", Inv) + ")" : (winterOff ? "zima bez skutkow (Winter Bite = off, x1)" : "wiosna albo jesien (x1)")))
                  .Append(" | potrzeba ").Append(F2(need)).Append(" ladunkow (po zaokragleniu ").Append(want).Append(")")
                  .Append(" | kupione i spalone ").Append(bought).Append(" ladunkow za ").Append(gold).Append(" zl (z kies ludu ").Append(fromPurse).Append(", akcyza do gmin ").Append(toComm).Append(", do kas miast ").Append(toTowns).Append(")")
                  .Append(" | bez opalu dzis ").Append(short0).Append(" ladunkow: polka nie wyzej niz zapas rzemiosla ").Append(waitShelf).Append(" miast, czesciowo ").Append(part)
                  .Append(", za mala kiesa ponad jutrzejszy koszyk ").Append(waitPurse).Append(" miast | drewno zostawione rzemioslu na polkach ").Append(kept)
                  .Append(" | poza regula: miasta nieskalibrowane ").Append(noCal)
                  .Append(" | czas ").Append(((Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency).ToString("0.0", Inv)).Append(" ms | potkniecia ").Append(_fwStumbles).Append('.');
                Log.Info(sb.ToString());
                OpalEnd(op, day, winter, summer, winterOff, need, want);   // f4-opal: popyt ceny drewna, linia "Opal pod KS (f4-opal)"
            }
            catch (Exception e) { _fwStumbles++; Stumble("FirewoodDaily", e); }
        }
    }
}
