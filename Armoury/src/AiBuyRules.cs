using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// E8/Z (251, PLAN 8.8a-b; RC8 poz. 28; projekt etapu 8 v2 rozdz. 3 "E8/Z") - ZAKUPY I NADWYZKI AI. Trzy reguly, kazda z wylacznikiem:
    /// (1) AiSkipFineGoods: zakupy brakow AI (AiGear.BuyLoop, sztuki zastepcze, zamowienia zamkow) nie biora sztuk z dodatnim stanem (PriceMultiplier > 1).
    ///     Zbrojownia DTE to "przedmiot: ile" - stan na plus ginal w chwili zakupu razem z doplata (AiWear.NoteBought pamieta tylko stany ponizej 1), a przy
    ///     sprzedazy nadwyzek sztuka wracala na polke jako zwykla. Dodatnie stany zostaja dla gracza i bohaterow; "lepsze za swoje" (MenUpgrade) AI juz je
    ///     pomija. AiSkipTownsfolkDress: to samo dla stroju mieszczan, ktorego nie nosi zaden zolnierz - zbroja ludzi (glowa, tulow, nogi, rece, plaszcz) z flaga
    ///     Civilian nieobecna w zadnym wzorcu oddzialu (WorkshopLaw.CivilianOnly - ta sama lista, wedlug ktorej linie zbrojne warsztatow nie szyja sukien;
    ///     obejmuje tez stroj i zbroje samych bohaterow z ta flaga, np. plytowe buty rodow). Sama flaga Civilian nie wystarcza: w plikach gry i ROT maja ja
    ///     takze buty, rekawice, kaptury i tuniki ze wzorcow wojska, 24 z 44 strzal i cala uprzaz (112 z 112) - dlatego bron, amunicja i uprzaz poza regula.
    /// (2) AiGearMaxPriceFactor (W CIENIU: 0 = wylaczony): sufit ceny = mnoznik x wartosc TEJ sztuki ze stanem (EquipmentElement.ItemValue - podstawa, od
    ///     ktorej gra liczy cene). Kandydat ponad sufit pomijany przy przegladzie polki, kolejne sztuki partii w ShelfBuy.Take (cap); sztuka ponad sufit nie
    ///     liczy sie jako "jest na polce", wiec brak idzie jako zamowienie dla warsztatow (AiGear.OnShelf). Dopoki wylaczony - linia liczy, ile sztuk i zlota
    ///     zablokowalby sufit ShadowFactor.
    /// (3) AiHarnessSurplusSell: uprzaz zbrojowni partii AI ponad 1.5 x zapas idzie na polke osady za zaplata, jak nadwyzki sprzetu (MenPurse.SellArmorySurplus:
    ///     kasa osady z nadwyzki -> trzecia panu, reszta sakiewce ludzi). Zapas = konni szeregowi (z rannymi) + gotowi do awansu na jezdnego (partie lordow)
    ///     + luz AiSpareHorseShare - ta sama histereza co konie E6/K2 (kupno do zapasu, sprzedaz ponad 1.5 x zapas). Dotad uprzaz po poleglych jezdzcach
    ///     lezala w zbrojowni na zawsze ("konie i rzedy - Stajnia", a Stajnia uprzezy nie sprzedaje).
    /// Linia doby "Zakupy i nadwyzki AI (E8/Z)" (z AiGear.BardingDaily). Bez zapisu; stan czyszczony w AiGear.Reset.
    /// </summary>
    internal static class AiBuyRules
    {
        /// <summary>Sufit, dla ktorego linia liczy skutek, dopoki klucz AiGearMaxPriceFactor jest wylaczony (ten sam prog co "sufit 2.0" linii E8/P2).</summary>
        internal const float ShadowFactor = 2f;

        private static bool FineOn { get { var s = Settings.Current; return s != null && s.AiSkipFineGoods; } }
        private static bool DressOn { get { var s = Settings.Current; return s != null && s.AiSkipTownsfolkDress; } }
        /// <summary>Mnoznik sufitu ceny; 0 = wylaczony. Wartosc miedzy 0 a 1 liczy sie jako 1 (sufit nie nizszy niz wartosc sztuki).</summary>
        internal static float CapFactor { get { var s = Settings.Current; float f = s != null ? s.AiGearMaxPriceFactor : 0f; return f <= 0f ? 0f : Math.Max(1f, f); } }
        internal static bool CapOn { get { return CapFactor > 0f; } }
        internal static bool HarnessOn { get { var s = Settings.Current; return s != null && s.AiHarnessSurplusSell; } }

        // liczniki doby (tylko log)
        private static long _dFineSkip, _dFineVisits, _dFineBought, _dFineExtra, _dDressSkip, _dDressVisits, _dDressBought;
        private static long _dCapSkip, _dCapVisits, _dOverN, _dOverG, _dBought, _dOrders, _dLostFine, _dLostDress, _dLostCap;
        /// <summary>Powod ostatniego Barred = true: dodatni stan (inaczej stroj mieszczan).</summary>
        internal static bool LastFine;
        private static long _dHarSold, _dHarGold, _dHarSoldGar, _dHarThird, _dHarParties, _dHarNoBuyer;
        private static int _visit, _fineVisit = -1, _dressVisit = -1, _capVisit = -1;
        private static int _stumbles;
        private static readonly HashSet<string> _errSites = new HashSet<string>();

        internal static void Reset()
        {
            DayClear();
            _visit = 0; _fineVisit = _dressVisit = _capVisit = -1;
            _stumbles = 0; _errSites.Clear();
        }

        private static void DayClear()
        {
            _dFineSkip = _dFineVisits = _dFineBought = _dFineExtra = _dDressSkip = _dDressVisits = _dDressBought = 0;
            _dCapSkip = _dCapVisits = _dOverN = _dOverG = _dBought = _dOrders = _dLostFine = _dLostDress = _dLostCap = 0;
            _dHarSold = _dHarGold = _dHarSoldGar = _dHarThird = _dHarParties = _dHarNoBuyer = 0;
        }

        internal static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_errSites.Add(where)) Log.Error("AiBuyRules." + where, e);
        }

        // ------------------------------------------------------------ (1) i (2): zakupy brakow
        /// <summary>AiGear.TryBuyCore przed przegladem polki: nowa wizyta (liczniki "w ilu wizytach").</summary>
        internal static void NewVisit() { _visit++; }

        /// <summary>Stroj mieszczan bez zolnierza: zbroja ludzi z flaga Civilian, ktorej nie ma zaden wzorzec oddzialu (WorkshopLaw.CivilianOnly).</summary>
        internal static bool Dress(ItemObject it)
        {
            if (it == null || !it.IsCivilian) return false;
            switch (it.ItemType)
            {
                case ItemObject.ItemTypeEnum.HeadArmor:
                case ItemObject.ItemTypeEnum.BodyArmor:
                case ItemObject.ItemTypeEnum.ChestArmor:
                case ItemObject.ItemTypeEnum.LegArmor:
                case ItemObject.ItemTypeEnum.HandArmor:
                case ItemObject.ItemTypeEnum.Cape:
                    return WorkshopLaw.CivilianOnly(it);
                default:
                    return false;
            }
        }

        /// <summary>Sztuka z polki nie dla AI (przed wycena): dodatni stan albo stroj mieszczan. count = false - sam odczyt (AiGear.OnShelf).</summary>
        internal static bool Barred(EquipmentElement el, bool count = true)
        {
            try
            {
                var it = el.Item;
                if (it == null) return false;
                var m = el.ItemModifier;
                if (m != null && m.PriceMultiplier > 1f && FineOn)
                {
                    LastFine = true;
                    if (count) { _dFineSkip++; if (_fineVisit != _visit) { _fineVisit = _visit; _dFineVisits++; } }
                    return true;
                }
                if (it.IsCivilian && DressOn && Dress(it))
                {
                    LastFine = false;
                    if (count) { _dDressSkip++; if (_dressVisit != _visit) { _dressVisit = _visit; _dDressVisits++; } }
                    return true;
                }
            }
            catch (Exception e) { Stumble("Barred", e); }
            return false;
        }

        /// <summary>Najwyzsza cena tej sztuki dla AI: sufit x wartosc sztuki ze stanem; 0 = bez sufitu (klucz wylaczony albo sztuka bez wartosci).</summary>
        internal static int CapPrice(EquipmentElement el)
        {
            float f = CapFactor;
            if (f <= 0f || el.Item == null) return 0;
            int worth = el.ItemValue;
            if (worth <= 0) return 0;
            return Math.Max(1, (int)Math.Floor(f * (double)worth + 1e-3));
        }

        /// <summary>Cena sztuki ponad sufit (tylko przy wlaczonym kluczu). count = false - sam odczyt.</summary>
        internal static bool OverCap(EquipmentElement el, int price, bool count = true)
        {
            int cap = CapPrice(el);
            if (cap <= 0 || price <= cap) return false;
            if (count) { _dCapSkip++; if (_capVisit != _visit) { _capVisit = _visit; _dCapVisits++; } }
            return true;
        }

        /// <summary>AiGear.OnShelf: brak poszedl jako zamowienie dla warsztatow, bo na polce lezala tylko sztuka nie dla AI (tylko licznik).</summary>
        internal static void NoteOrder() { _dOrders++; }

        /// <summary>
        /// AiGear.BuyLoop: koszyk zostal z brakiem (left sztuk w granicach limitu wizyty), a przeglad polki nie znalazl kandydata - choc lezaly na niej sztuki
        /// nie dla AI (dodatni stan fine, stroj dress, cena ponad sufit cap; sztuki, ktore przeszly typ, tier, udzwig i rezerwe kramu). Gorna granica tego,
        /// czego AI nie kupilo przez regule: min(brak, sztuki) - bez wyceny tych sztuk (nie wiadomo, czy starczyloby budzetu). Tylko licznik.
        /// </summary>
        internal static void NoteUnbought(int left, int fine, int dress, int cap)
        {
            if (left <= 0) return;
            if (fine > 0) _dLostFine += Math.Min(left, fine);
            if (dress > 0) _dLostDress += Math.Min(left, dress);
            if (cap > 0) _dLostCap += Math.Min(left, cap);
        }

        /// <summary>
        /// Kupiona partia n sztuk (zakupy brakow AiGear lordow i zalog, zamowienia zamkow) - wolac zaraz po ShelfBuy.Take (ceny sztuk ze sladu tej partii;
        /// gdy slad sie nie zgadza - po sredniej, jak Measure174b.NoteBatch). Tylko liczniki: dodatnie stany i stroj mieszczan kupione mimo reguly (przy
        /// wlaczonych kluczach 0), sztuki ponad sufit (klucz wylaczony - ponad ShadowFactor: tyle zablokowalby).
        /// </summary>
        internal static void NoteBought(EquipmentElement el, int n, int cost)
        {
            try
            {
                if (el.Item == null || n <= 0) return;
                _dBought += n;
                var m = el.ItemModifier;
                if (m != null && m.PriceMultiplier > 1f) { _dFineBought += n; _dFineExtra += cost - (long)Math.Round(cost / m.PriceMultiplier); }
                if (Dress(el.Item)) _dDressBought += n;
                int worth = el.ItemValue;
                if (worth <= 0) return;
                float f = CapFactor;
                int lim = Math.Max(1, (int)Math.Floor((f > 0f ? f : ShadowFactor) * (double)worth + 1e-3));
                bool trace = ShelfBuy.TraceCount == n && ShelfBuy.TraceSum == cost;
                int left = cost;
                for (int i = 0; i < n; i++)
                {
                    int p;
                    if (trace) p = ShelfBuy.TraceAt(i);
                    else { p = left / (n - i); left -= p; }
                    if (p > lim) { _dOverN++; _dOverG += p; }
                }
            }
            catch (Exception e) { Stumble("NoteBought", e); }
        }

        // ------------------------------------------------------------ (3): uprzaz ponad zapas
        /// <summary>
        /// JEDNA definicja zapasu uprzezy partii AI (czyta ja sprzedaz MenPurse.SellSpareHarness; dla E8/H - takze kupno): ile sztuk uprzezy zostaje.
        /// Zapas = konni szeregowi wzorca z wierzchowcem (z rannymi) + gotowi do awansu na jezdnego (tylko partie lordow - Stables.NeedByCategory, jak zapas
        /// koni E6/K2) + luz ceil(AiSpareHorseShare x konni); zostaje 1.5 x zapas (histereza K2 - kupno do zapasu i sprzedaz ponad 1.5 x zapas sie nie spotkaja).
        /// Bez podzialu na rodziny wierzchowcow - tak jak zakup uprzezy E6/R3 (koszyk typ x tier); sprzedaz od najnizszego tieru, wiec zostaja sztuki, ktore
        /// pokrywaja koszyki zakupu.
        /// </summary>
        internal static int HarnessKeep(MobileParty mp, TroopRoster roster, bool garrison, out int riders)
        {
            riders = Riders(roster);
            int wait = 0;
            if (!garrison && mp != null && mp.Party != null)
            {
                var nk = Stables.NeedByCategory(mp.Party);
                if (nk != null) for (int k = 0; k < nk.Length; k++) wait += Math.Max(0, nk[k]);
            }
            return Keep(riders, wait);
        }

        private static int Keep(int riders, int wait)
        {
            var c = Settings.Current;
            double share = c != null ? MBMath.ClampFloat(c.AiSpareHorseShare, 0f, 1f) : 0.1;
            int target = riders + wait + (int)Math.Ceiling(share * riders - 1e-4);   // -1e-4: jak Stables.Reserve (0.1f x 30 to 3.0000000447, nie 4)
            return (3 * target) / 2;
        }

        private static int Riders(TroopRoster roster)
        {
            int n = 0;
            if (roster == null) return 0;
            for (int i = 0; i < roster.Count; i++)
            {
                var el = roster.GetElementCopyAtIndex(i);
                var ch = el.Character;
                if (ch == null || ch.IsHero || el.Number <= 0 || !ch.IsMounted) continue;
                n += el.Number;
            }
            return n;
        }

        /// <summary>Uprzaz zbrojowni, ktora liczy sie do zapasu i moze isc na polke (bez unikatow).</summary>
        internal static bool SpareHarness(ItemObject it)
        {
            return it != null && it.ItemType == ItemObject.ItemTypeEnum.HorseHarness && !ArmsPricing.IsUnique(it);
        }

        /// <summary>MenPurse.SellSpareHarness: wizyta partii z uprzeza ponad zapas (tylko liczniki).</summary>
        internal static void NoteHarness(bool garrison, int sold, int gold, int third, bool noBuyer)
        {
            if (sold > 0) { _dHarParties++; _dHarSold += sold; _dHarGold += gold; _dHarThird += third; if (garrison) _dHarSoldGar += sold; }
            if (noBuyer) _dHarNoBuyer++;
        }

        // ------------------------------------------------------------ linia doby
        private static void CensusOne(MobileParty mp, Dictionary<TaleWorlds.ObjectSystem.MBGUID, Dictionary<ItemObject, int>> all, ref long harness, ref long riders, ref long over, ref long overParties)
        {
            if (mp == null || !mp.IsActive || mp.IsMainParty) return;
            int r = Riders(mp.MemberRoster);
            riders += r;
            Dictionary<ItemObject, int> arm;
            if (!all.TryGetValue(mp.Id, out arm) || arm == null) return;
            int h = 0;
            foreach (var kv in arm) if (kv.Value > 0 && SpareHarness(kv.Key)) h += kv.Value;
            if (h <= 0) return;
            harness += h;
            int o = h - Keep(r, 0);
            if (o > 0) { over += o; overParties++; }
        }

        /// <summary>Linia doby "Zakupy i nadwyzki AI (E8/Z)" - z AiGear.BardingDaily (takt doby, po handlu kupcow).</summary>
        internal static void Daily()
        {
            try
            {
                var ci = CultureInfo.InvariantCulture;
                float f = CapFactor;
                long hL = 0, hG = 0, rL = 0, rG = 0, over = 0, overP = 0;
                double ms = 0;
                bool census = false;
                try
                {
                    var all = AiGear.Armories();
                    if (all != null && Campaign.Current != null)
                    {
                        long t0 = Stopwatch.GetTimestamp();
                        foreach (var mp in MobileParty.AllLordParties) CensusOne(mp, all, ref hL, ref rL, ref over, ref overP);
                        foreach (var mp in MobileParty.AllGarrisonParties) CensusOne(mp, all, ref hG, ref rG, ref over, ref overP);
                        ms = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
                        census = true;
                    }
                }
                catch (Exception e) { Stumble("Census", e); }
                var sb = new StringBuilder();
                sb.Append("Zakupy i nadwyzki AI (E8/Z): dzien ").Append((int)CampaignTime.Now.ToDays)
                  .Append(" | dodatnie stany [").Append(FineOn ? "WL" : "wyl").Append("]: odrzucenia kandydatow ").Append(_dFineSkip).Append(" w ").Append(_dFineVisits)
                  .Append(" wizytach, kupione dodatnie stany ").Append(_dFineBought).Append(" szt. (doplata ").Append(_dFineExtra).Append(" zl)")
                  .Append(" | stroj mieszczan bez zolnierza [").Append(DressOn ? "WL" : "wyl").Append("]: odrzucenia kandydatow ").Append(_dDressSkip).Append(" w ").Append(_dDressVisits)
                  .Append(" wizytach, kupiony stroj mieszczan ").Append(_dDressBought).Append(" szt.")
                  .Append(" | sufit ceny [").Append(f > 0f ? "WL x" + f.ToString("0.00", ci) : "W CIENIU x" + ShadowFactor.ToString("0.00", ci)).Append("]: odrzucenia kandydatow ")
                  .Append(_dCapSkip).Append(" w ").Append(_dCapVisits).Append(" wizytach, kupione ponad sufit ").Append(_dOverN).Append(" szt. / ").Append(_dOverG)
                  .Append(" zl z ").Append(_dBought).Append(" kupionych sztuk").Append(f > 0f ? "" : " (tyle zablokowalby)").Append(" (zakupy brakow AiGear lordow i zalog)")
                  .Append(" | zamowienia dla warsztatow, bo na polce lezala tylko sztuka nie dla AI: ").Append(_dOrders)
                  .Append(" | brak zostal przy polce ze sztuka nie dla AI (najwyzej tyle nie kupiono przez regule): dodatni stan ").Append(_dLostFine)
                  .Append(" szt., stroj mieszczan ").Append(_dLostDress).Append(" szt., ponad sufit ").Append(_dLostCap).Append(" szt.")
                  .Append(" | uprzaz ponad zapas [").Append(HarnessOn ? "WL" : "wyl").Append("]: sprzedana ").Append(_dHarSold).Append(" szt. za ").Append(_dHarGold)
                  .Append(" zl (w tym zalogi ").Append(_dHarSoldGar).Append(" szt.; trzecia panom ").Append(_dHarThird).Append(" zl) w ").Append(_dHarParties)
                  .Append(" partiach, zabraklo kupca ").Append(_dHarNoBuyer).Append(" razy");
                if (census)
                    sb.Append("; uprzaz w zbrojowniach AI ").Append(hL + hG).Append(" szt. (lordowie ").Append(hL).Append(", zalogi ").Append(hG).Append(") przy jezdzcach ")
                      .Append(rL + rG).Append(" (lordowie ").Append(rL).Append(", zalogi ").Append(rG).Append("), ponad 1.5 x (jezdzcy + luz) zostaje ").Append(over)
                      .Append(" szt. w ").Append(overP).Append(" partiach (bez czekajacych na awans); koszt spisu ").Append(ms.ToString("0.0", ci)).Append(" ms");
                else sb.Append("; uprzaz w zbrojowniach AI: brak spisu");
                sb.Append(" | potkniecia (sesja) ").Append(_stumbles).Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Daily", e); }
            DayClear();
        }
    }
}
