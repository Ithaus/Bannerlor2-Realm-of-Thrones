using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// 430 (audyt ekonomii 11.11, poprawka C5; decyzja Jeffa 2 "nizszy lub moze odmowic"): KOLEJNY OKUP TEGO SAMEGO RODU TEMU SAMEMU PORYWACZOWI - TANSZY, ROD MOZE ODMOWIC.
    /// Dowod (Armoury-2026-11-09_21-17-29.log, kurier, odbiorca gracz): rod Tully zaplacil 436 424 zl za Florence Vance (21:27:45) i 422 607 zl za Katlyn Piper
    /// (21:28:53), rod Paege 116 263 + 397 824, rod Cox 37 013 + 37 070 za te sama osobe; gracz +4.08 mln w 62 doby, z tego okupy ok. 3.5 mln (audyt, werdykt 10).
    /// Przyczyna: Ransom178.PriceOf liczy cene z dochodu rodu (lord 10 x D, glowa 0.0824 x 364 x D) bez pamieci, ile ten rod juz zaplacil temu porywaczowi;
    /// M6a-2 (RansomRepeat) pamieta tylko TE SAMA OSOBE przez 56 dob - drugi krewny tego samego rodu kosztuje znow pelna cene, a rod placi zawsze.
    ///
    /// REGULA (klucz Ransom House Fatigue; tylko przy okupach wedlug majatku 178):
    ///  1. MNOZNIK RODU: n = okupy, ktore rod jenca zaplacil rodowi porywacza w ostatnich Ransom House Window Days (364) dobach (kazda droga okupu - zdarzenie gry
    ///     HeroPrisonerReleased z EndCaptivityDetail.Ransom). Cena = cena x max(Ransom House Floor, Ransom House Step ^ n). Z mnoznikiem osoby M6a-2 bierze sie
    ///     MNIEJSZY z dwoch (Ransom178.RepeatFactor), nie iloczyn - jedno zjawisko liczone raz. Wszystkie drogi i wyceny przez PriceOf (kurier, posrednik, ekran
    ///     druzyny, barter, rokowania 366, wartosc jenca w wymianie 384, nagroda za wielkiego jenca).
    ///  2. ODMOWA (klucz Ransom House Refusal): przy pojmaniu (albo pierwszego dnia, gdy rod rozwaza okup) rod raz rzuca, czy w ogole zaplaci. Szansa odmowy =
    ///     Ransom House Refuse Percent x n  x kiesa (0.5 gdy okup to drobiazg wobec kies rodziny .. 1.5 gdy to ponad polowa)  x relacja decydujacego z glowa rodu
    ///     porywacza (1 - relacja / 200: przyjaciel 0.5 .. wrog 1.5)  x charakter decydujacego (1 - 0.15 x (Honor + Hojnosc - Wyrachowanie), 0.4 .. 1.6)
    ///     x 0.5, gdy jeniec to glowa rodu, wladca albo nastepca tronu  x 0.5 za kazda wczesniejsza odmowe w tej niewoli; najwyzej Ransom House Refuse Max Percent.
    ///     Pierwszy okup w oknie (n = 0) - rod nie odmawia. Odmowa trwa Ransom House Refuse Days dob, potem rod rozwaza ponownie.
    ///     Odmowa zamyka drogi MONETY: kurier nie przychodzi (bramka w Ransom178.ConsiderPrefix obok DungeonFate.CourierRefused i zakladnika 385c), posrednik
    ///     i ekran druzyny daja 0 (RcLordPrice), barter 0, okup x2 w rokowaniach 366 nieczynny. Wymiana za warunki 384, zakladnik 385c, slowo rodu i wyrok 409,
    ///     uwolnienie - bez zmian (rod nie placi monety, ale dalej mozna sie ukladac).
    ///  Zakres: porywacz = rod gracza (audyt: jedynym, kto rosnie na okupach, jest gracz); Ransom House All Captors TAK - takze pary rodow AI (odmowa = tego dnia
    ///  bez umowy AI-AI). Rod gracza jako placacy - poza regula (gracz decyduje sam).
    /// Zloto: te same drogi (rod jenca / skarbiec krola -> porywacz / korona), mniejsza kwota albo brak umowy - nic nie powstaje i nic nie znika.
    /// Pamiec okupow prowadzona zawsze (takze przy kluczu NIE); zapis "arm_okupy430" (brak klucza = pusto); Reset w konstruktorze ArmouryBehavior.
    /// Linia doby "Okupy rodow (430 C5)" z kontrola pamieci (start + nowe - wygasle - stan = 0).
    /// </summary>
    internal static class RansomHouse430
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private sealed class Dec
        {
            public string Captor = "";   // rod porywacza, dla ktorego zapadla decyzja (zmiana trzymajacego = decyzja niewazna)
            public int State;            // 1 = rod zaplaci, 2 = odmowa
            public int Day, Until, Tries, N;
            public float P;
        }

        private static readonly Dictionary<string, List<int>> _ev = new Dictionary<string, List<int>>(StringComparer.Ordinal);   // "rodJenca>rodPorywacza" -> doby okupow
        private static readonly Dictionary<string, Dec> _dec = new Dictionary<string, Dec>(StringComparer.Ordinal);            // id jenca -> decyzja rodu w tej niewoli
        private static readonly Dictionary<Hero, double> _sale = new Dictionary<Hero, double>();   // posrednik: mnoznik rodu sprzed uwolnienia (SalePrefix -> SalePostfix)

        // liczniki doby
        private static int _dayStart, _dNew, _dExpired, _dDeals, _dDecided, _dRefused, _dAgreed, _dAgain, _dBlkCourier, _dBlkBroker, _dBlkBarter, _dBlkTerms, _dBlkAi, _dFreedRefused;
        private static int _dRecount, _dKept;   // 430-p: ponowne decyzje po nowym okupie rodu; jency zatrzymani u posrednika mimo proby sprzedazy
        private static long _dFull, _dPaid;
        private static double _dPSum;
        private static int _stumbles, _importN = -1, _importBad;
        private static readonly HashSet<string> _err = new HashSet<string>();
        [ThreadStatic] private static bool _busy;   // Decide wola PriceOf -> Factor: bez ponownego wejscia

        internal static bool On { get { var s = Settings.Current; return s != null && s.RansomHouseFatigue && Ransom178.FlowsOn; } }
        internal static bool RefusalOn { get { var s = Settings.Current; return On && s.RansomHouseRefusal; } }
        private static int Window { get { var s = Settings.Current; return s != null ? Math.Max(1, Math.Min(3640, s.RansomHouseWindowDays)) : 364; } }
        private static int Today { get { return (int)CampaignTime.Now.ToDays; } }

        internal static void Reset()
        {
            _ev.Clear(); _dec.Clear(); _sale.Clear(); _dayStart = 0; ZeroDay(); _stumbles = 0; _importN = -1; _importBad = 0; _err.Clear();
        }

        private static void ZeroDay()
        {
            _dNew = _dExpired = _dDeals = _dDecided = _dRefused = _dAgreed = _dAgain = _dBlkCourier = _dBlkBroker = _dBlkBarter = _dBlkTerms = _dBlkAi = _dFreedRefused = 0;
            _dRecount = _dKept = 0;
            _dFull = _dPaid = 0; _dPSum = 0;
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_err.Add(where)) Log.Error("RansomHouse430." + where, e); } catch { }
        }

        private static string Name(Hero h) { try { return h != null && h.Name != null ? h.Name.ToString() : "-"; } catch { return "?"; } }
        private static string Name(Clan c) { try { return c != null && c.Name != null ? c.Name.ToString() : "-"; } catch { return "?"; } }

        private static bool InScope(Clan captor)
        {
            if (captor == null) return false;
            if (captor == Clan.PlayerClan) return true;
            var s = Settings.Current;
            return s != null && s.RansomHouseAllCaptors;
        }

        private static string Pair(Clan payer, Clan captor)
        {
            if (payer == null || captor == null || payer.StringId == null || captor.StringId == null) return null;
            return payer.StringId + ">" + captor.StringId;
        }

        private static int CountIn(List<int> days, int today, int w)
        {
            int n = 0;
            for (int i = 0; i < days.Count; i++) { int ago = today - days[i]; if (ago >= 0 && ago < w) n++; }
            return n;
        }

        /// <summary>Okupy zaplacone przez rod `payer` rodowi `captor` w oknie.</summary>
        private static int Count(Clan payer, Clan captor)
        {
            var key = Pair(payer, captor);
            List<int> days;
            if (key == null || !_ev.TryGetValue(key, out days)) return 0;
            return CountIn(days, Today, Window);
        }

        private static double FactorFor(Clan payer, Clan captor)
        {
            var s = Settings.Current;
            if (!On || payer == null || payer == Clan.PlayerClan || !InScope(captor)) return 1.0;
            int n = Count(payer, captor);
            if (n <= 0) return 1.0;
            double step = Math.Max(0.0, Math.Min(1.0, s.RansomHouseStep)), floor = Math.Max(0.0, Math.Min(1.0, s.RansomHouseFloor));
            return Math.Max(floor, Math.Pow(step, n));
        }

        /// <summary>Mnoznik ceny okupu z pamieci rodu (1 = bez zmian): rod jenca -> rod, ktory go teraz trzyma. Nie rzuca.</summary>
        internal static double Factor(Hero h)
        {
            try
            {
                if (h == null || h.Clan == null || !On) return 1.0;
                return FactorFor(h.Clan, Ransom178.HolderOf(h));
            }
            catch (Exception e) { Stumble("Factor", e); return 1.0; }
        }

        // ------------------------------------------------------------ odmowa
        private static Dec Valid(Hero h, Clan captor)
        {
            Dec d;
            if (h == null || h.StringId == null || !_dec.TryGetValue(h.StringId, out d)) return null;
            if (captor == null || d.Captor != (captor.StringId ?? "")) { _dec.Remove(h.StringId); return null; }
            return d;
        }

        /// <summary>Czy rod jenca odmawia teraz zaplaty monety (tylko odczyt - bez rzutu). Nie rzuca.</summary>
        internal static bool Refused(Hero h)
        {
            try
            {
                if (h == null || !RefusalOn || _dec.Count == 0 || !h.IsPrisoner) return false;
                var captor = Ransom178.HolderOf(h);
                if (!InScope(captor)) return false;
                var d = Valid(h, captor);
                if (d == null) return false;
                // 430-p (recenzja N4): odmowa wygasla - rod rozwaza ponownie TU (pierwsza wycena po terminie), nie dopiero w dobowym takcie kuriera (Blocks) -
                // inaczej od switu doby Until posrednik i barter placily bez rzutu, a przy odprawianych kurierach (404) rzutu nie bylo wcale
                if (d.State == 2 && Today >= d.Until && h.Clan != null && h.Clan != Clan.PlayerClan && Ransom178.IsRansomLord(h)) { _dAgain++; d = Decide(h, captor, d.Tries + 1); }
                return d.State == 2 && Today < d.Until;
            }
            catch (Exception e) { Stumble("Refused", e); return false; }
        }

        private static double Chance(Hero h, Clan captor, int n, int tries, out string why)
        {
            var s = Settings.Current;
            why = "";
            if (n <= 0) return 0;
            double p = Math.Max(0, s.RansomHouseRefusePercent) / 100.0 * n;
            // kiesa: okup wobec kies rodziny (krol - skarbiec korony)
            double purse = 1.0, burden = 0;
            try
            {
                bool byCrown; double D;
                long price = Ransom178.PriceOf(h, out byCrown, out D);
                var k = h.MapFaction as Kingdom;
                long gold = byCrown && k != null ? (long)k.KingdomBudgetWallet : ClanIncomeBook.FamilyGoldOf(h.Clan);
                burden = price / (double)Math.Max(1L, gold);
                purse = Math.Max(0.5, Math.Min(1.5, 0.5 + 2.0 * burden));
            }
            catch (Exception e) { Stumble("Chance.Purse", e); }
            // decydujacy: glowa rodu, a gdy to ona siedzi - inny lord rodu
            Hero dec = null;
            try { dec = Ransom178.DeciderOf(h) ?? h.Clan.Leader; } catch { }
            double rel = 1.0, chr = 1.0; int r = 0, t = 0;
            try
            {
                var cl = captor != null ? captor.Leader : null;
                if (dec != null && cl != null && dec != cl) { r = dec.GetRelation(cl); rel = Math.Max(0.5, Math.Min(1.5, 1.0 - r / 200.0)); }
                if (dec != null)
                {
                    t = dec.GetTraitLevel(DefaultTraits.Honor) + dec.GetTraitLevel(DefaultTraits.Generosity) - dec.GetTraitLevel(DefaultTraits.Calculating);
                    chr = Math.Max(0.4, Math.Min(1.6, 1.0 - 0.15 * t));
                }
            }
            catch (Exception e) { Stumble("Chance.Person", e); }
            double rank = 1.0;
            try { if (h.Clan.Leader == h || Ransom178.IsRulerOrHeir(h)) rank = 0.5; } catch { }
            double again = Math.Pow(0.5, Math.Max(0, tries));
            p = p * purse * rel * chr * rank * again;
            p = Math.Max(0.0, Math.Min(Math.Max(0, Math.Min(100, s.RansomHouseRefuseMaxPercent)) / 100.0, p));
            why = "okupow w oknie " + n + ", kiesa x" + purse.ToString("0.00", Inv) + " (okup " + (burden * 100).ToString("0", Inv) + "% kies), relacja " + r + " x" + rel.ToString("0.00", Inv)
                  + ", charakter " + t + " x" + chr.ToString("0.00", Inv) + " (" + Name(dec) + "), ranga x" + rank.ToString("0.0", Inv) + ", proba " + (tries + 1) + " x" + again.ToString("0.00", Inv);
            return p;
        }

        /// <summary>Decyzja rodu w tej niewoli (jeden rzut). Zwraca wpis; komunikat dla gracza, gdy porywaczem jest jego rod.</summary>
        private static Dec Decide(Hero h, Clan captor, int tries)
        {
            var s = Settings.Current;
            int today = Today, n = Count(h.Clan, captor);
            var d = new Dec { Captor = captor.StringId ?? "", State = 1, Day = today, Until = today, Tries = tries, N = n };
            string why = "";
            if (_busy) return d;
            _busy = true;
            try { d.P = (float)Chance(h, captor, n, tries, out why); }
            finally { _busy = false; }
            if (d.P > 0 && MBRandom.RandomFloat < d.P) { d.State = 2; d.Until = today + Math.Max(1, s.RansomHouseRefuseDays); }
            _dec[h.StringId] = d;
            _dDecided++; _dPSum += d.P;
            if (d.State == 2) _dRefused++; else _dAgreed++;
            if (n > 0 || tries > 0)
                Log.Info("Okupy rodow (430 C5): decyzja - " + Name(h.Clan) + " za " + Name(h) + " (trzyma " + Name(captor) + "): szansa odmowy " + (d.P * 100).ToString("0", Inv) + "% ["
                         + why + "] -> " + (d.State == 2 ? "ODMOWA do doby " + d.Until : "zaplaci") + ".");
            if (captor == Clan.PlayerClan)
            {
                try
                {
                    if (d.State == 2)
                        Log.Player(Name(h.Clan) + " will not pay a ransom for " + Name(h) + (tries > 0 ? " yet" : "") + ": they have paid " + n + (n == 1 ? " ransom" : " ransoms")
                                   + " for captives of your house within the year. They may think again in " + Math.Max(1, s.RansomHouseRefuseDays) + " days.", true);
                    else if (tries > 0)
                        Log.Player(Name(h.Clan) + " is ready to talk about a ransom for " + Name(h) + " again.");
                    else if (n > 0)
                    {
                        double f = Math.Min(FactorFor(h.Clan, captor), RansomRepeat.Factor(h));
                        if (f < 0.995)
                            Log.Player(Name(h.Clan) + " has paid " + n + (n == 1 ? " ransom" : " ransoms") + " for captives of your house within the year: for " + Name(h) + " they will offer about "
                                       + Math.Round(f * 100).ToString(Inv) + "% of the usual price.");
                    }
                }
                catch (Exception e) { Stumble("Decide.Message", e); }
            }
            return d;
        }

        /// <summary>Zdarzenie gry: pojmanie - rod jenca decyduje od razu, gdy porywaczem jest rod gracza (gracz wie, na czym stoi, zanim pojdzie do posrednika).</summary>
        internal static void OnTaken(PartyBase capturer, Hero prisoner)
        {
            try
            {
                if (prisoner == null || prisoner.StringId == null) return;
                _dec.Remove(prisoner.StringId);   // nowa niewola - stara decyzja niewazna
                if (!RefusalOn || !Ransom178.IsRansomLord(prisoner) || prisoner.Clan == Clan.PlayerClan) return;
                var captor = Ransom178.CaptorClan(capturer);
                if (captor == null || captor != Clan.PlayerClan) return;   // pary AI (Ransom House All Captors) - pierwszego dnia rozwazania okupu (Blocks)
                Decide(prisoner, captor, 0);
            }
            catch (Exception e) { Stumble("OnTaken", e); }
        }

        /// <summary>Bramka Ransom178.ConsiderPrefix (raz na dobe na jenca): true = rod odmawia - dzis bez kuriera / bez umowy AI-AI. Tu zapada decyzja, gdy jej nie ma,
        /// i ponowne rozwazenie po uplywie odmowy.</summary>
        internal static bool Blocks(Hero h)
        {
            try
            {
                if (h == null || h.StringId == null || !RefusalOn || !h.IsPrisoner || h.Clan == null || h.Clan == Clan.PlayerClan) return false;
                var captor = Ransom178.HolderOf(h);
                if (!InScope(captor) || !Ransom178.IsRansomLord(h)) return false;
                var d = Valid(h, captor);
                if (d == null) d = Decide(h, captor, 0);
                else if (d.State == 2 && Today >= d.Until) { _dAgain++; d = Decide(h, captor, d.Tries + 1); }
                // 430-p (recenzja N6): rod zaplacil w miedzyczasie kolejny okup temu porywaczowi (jency z jednej bitwy maja przy pojmaniu wszyscy n = 0) - rozwaza ponownie
                else if (d.State == 1 && Count(h.Clan, captor) > d.N) { _dRecount++; d = Decide(h, captor, d.Tries); }
                if (d.State != 2) return false;
                if (captor == Clan.PlayerClan) _dBlkCourier++; else _dBlkAi++;
                return true;
            }
            catch (Exception e) { Stumble("Blocks", e); return false; }
        }

        /// <summary>430-p: jeniec z odmowa rodu zdjety z listy sprzedazy u posrednika (zostal w niewoli gracza).</summary>
        internal static void NoteKept() { _dKept++; _dBlkBroker++; }

        /// <summary>Licznik drogi monety zamknietej odmowa: 1 posrednik / ekran druzyny, 2 barter, 3 rokowania 366 / przeplyw okupu.</summary>
        internal static void NoteBlocked(int what)
        {
            if (what == 1) _dBlkBroker++; else if (what == 2) _dBlkBarter++; else _dBlkTerms++;
        }

        // ------------------------------------------------------------ liczniki umow
        /// <summary>Ransom178.Execute (AI-AI, kurier, rokowania): okup wykonany. paid = cena po mnozniku powtarzalnosci (przed pulapem dlugu), personF = mnoznik osoby M6a-2.</summary>
        internal static void NoteDeal(Hero captive, Clan captor, long paid, double personF)
        {
            try
            {
                if (captive == null || !On) return;
                double hf = FactorFor(captive.Clan, captor);
                if (hf >= 1.0 || hf > personF || hf <= 0) return;   // wiaze mnoznik osoby (albo zaden) - liczy go linia M6a-2
                _dDeals++; _dFull += (long)Math.Round(paid / hf); _dPaid += paid;
            }
            catch (Exception e) { Stumble("NoteDeal", e); }
        }

        /// <summary>Ransom178.SalePrefix (posrednik, ekran druzyny): mnoznik rodu sprzed uwolnienia jenca - do licznika w SaleDone.</summary>
        internal static void SalePeek(Hero h)
        {
            try
            {
                if (h == null || !On) return;
                double hf = FactorFor(h.Clan, Clan.PlayerClan);
                if (hf < 1.0 && hf > 0 && hf <= RansomRepeat.Factor(h)) _sale[h] = hf; else _sale.Remove(h);
            }
            catch (Exception e) { Stumble("SalePeek", e); }
        }

        internal static void SaleDone(Hero h, long price, bool sold)
        {
            try
            {
                double hf;
                if (h == null || !_sale.TryGetValue(h, out hf)) return;
                _sale.Remove(h);
                if (!sold || price <= 0) return;
                _dDeals++; _dFull += (long)Math.Round(price / hf); _dPaid += price;
            }
            catch (Exception e) { Stumble("SaleDone", e); }
        }

        /// <summary>Zdarzenie gry HeroPrisonerReleased: decyzja tej niewoli znika; okup (kazda droga) - wpis do pamieci pary rod jenca -> rod porywacza.</summary>
        internal static void OnReleased(Hero h, PartyBase party, IFaction f, EndCaptivityDetail detail, bool show)
        {
            try
            {
                if (h == null || h.StringId == null) return;
                Dec d; bool had = _dec.TryGetValue(h.StringId, out d);
                if (had) _dec.Remove(h.StringId);
                if (detail != EndCaptivityDetail.Ransom || h == Hero.MainHero || h.Clan == null || party == null) return;
                var captor = Ransom178.CaptorClan(party);
                var key = Pair(h.Clan, captor);
                if (key == null || key.IndexOfAny(Bad) >= 0) return;
                // jeniec sprzedany posrednikowi mimo odmowy rodu: rod nic nie zaplacil - to nie jest okup do pamieci
                if (had && RefusalOn && d.State == 2 && Today < d.Until && d.Captor == (captor.StringId ?? "")) { _dFreedRefused++; return; }
                List<int> days;
                if (!_ev.TryGetValue(key, out days)) { days = new List<int>(2); _ev[key] = days; }
                days.Add(Today); _dNew++;
            }
            catch (Exception e) { Stumble("OnReleased", e); }
        }

        private static int Entries() { int n = 0; foreach (var kv in _ev) n += kv.Value.Count; return n; }

        /// <summary>Linia doby (po "Okupy powtorne (M6a-2)"): wpisy starsze niz okno usuwane, kontrola pamieci.</summary>
        internal static void Report()
        {
            var s = Settings.Current;
            if (s == null || Campaign.Current == null) return;
            try
            {
                int today = Today, w = Window;
                var dead = new List<string>();
                foreach (var kv in _ev)
                {
                    int before = kv.Value.Count;
                    kv.Value.RemoveAll(x => today - x >= w);
                    _dExpired += before - kv.Value.Count;
                    if (kv.Value.Count == 0) dead.Add(kv.Key);
                }
                foreach (var k in dead) _ev.Remove(k);
                // decyzje jencow, ktorzy juz nie siedza (smierc, ucieczka bez zdarzenia) - sprzatanie
                if (_dec.Count > 0)
                {
                    var gone = new List<string>();
                    foreach (var kv in _dec)
                    {
                        try { Hero h = Hero.Find(kv.Key); if (h == null || !h.IsAlive || !h.IsPrisoner) gone.Add(kv.Key); }
                        catch { gone.Add(kv.Key); }
                    }
                    foreach (var k in gone) _dec.Remove(k);
                }
                _sale.Clear();
                int now = Entries(), toPlayer = 0, refusing = 0;
                string pk = Clan.PlayerClan != null && Clan.PlayerClan.StringId != null ? ">" + Clan.PlayerClan.StringId : null;
                if (pk != null) foreach (var kv in _ev) if (kv.Key.EndsWith(pk, StringComparison.Ordinal)) toPlayer += kv.Value.Count;
                foreach (var kv in _dec) if (kv.Value.State == 2 && today < kv.Value.Until) refusing++;
                if (s.LogEnabled)
                {
                    int diff = _dayStart + _dNew - _dExpired - now;
                    var sb = new StringBuilder(700);
                    sb.Append("Okupy rodow (430 C5): dzien ").Append(today)
                      .Append(" | klucz Ransom House Fatigue ").Append(On ? "tak" : "nie").Append(" (okno ").Append(w).Append(" dob, krok x").Append(s.RansomHouseStep.ToString("0.00", Inv))
                      .Append(", podloga ").Append(s.RansomHouseFloor.ToString("0.00", Inv)).Append("; odmowa ").Append(RefusalOn ? "tak" : "nie").Append(' ').Append(s.RansomHouseRefusePercent)
                      .Append("% za okup, najwyzej ").Append(s.RansomHouseRefuseMaxPercent).Append("%, na ").Append(s.RansomHouseRefuseDays).Append(" dob; zakres ")
                      .Append(s.RansomHouseAllCaptors ? "wszyscy porywacze" : "porywacz = rod gracza").Append("; okupy 178 ").Append(Ransom178.FlowsOn ? "tak" : "nie").Append(')')
                      .Append(" | okupy dzis z mnoznikiem rodu (gdzie nie wyzszy od mnoznika osoby M6a-2): ").Append(_dDeals).Append(", cena pelna ").Append(_dFull).Append(", zaplacona ")
                      .Append(_dPaid).Append(", zostalo u rodzin jencow ").Append(_dFull - _dPaid)
                      .Append(" | decyzje rodow dzis ").Append(_dDecided).Append(" (zaplaci ").Append(_dAgreed).Append(", odmowa ").Append(_dRefused).Append(", srednia szansa odmowy ")
                      .Append((_dDecided > 0 ? _dPSum / _dDecided * 100 : 0).ToString("0", Inv)).Append("%; w tym ponowne po uplywie odmowy ").Append(_dAgain).Append(", po nowym okupie rodu ").Append(_dRecount).Append("), odmowy czynne ").Append(refusing)
                      .Append(", decyzji w pamieci ").Append(_dec.Count)
                      .Append(" | drogi monety zamkniete odmowa dzis: kurier ").Append(_dBlkCourier).Append(", posrednik / ekran druzyny ").Append(_dBlkBroker).Append(", barter (wyceny) ").Append(_dBlkBarter)
                      .Append(", rokowania / przeplyw ").Append(_dBlkTerms).Append(", AI-AI ").Append(_dBlkAi).Append("; jency oddani posrednikowi mimo odmowy (bez wpisu okupu) ").Append(_dFreedRefused).Append(", zatrzymani w niewoli zamiast sprzedazy ").Append(_dKept)
                      .Append(" | pamiec okupow: start ").Append(_dayStart).Append(" + nowe ").Append(_dNew).Append(" - wygasle ").Append(_dExpired).Append(" = stan ").Append(now)
                      .Append(" (par rod -> porywacz ").Append(_ev.Count).Append(", do rodu gracza ").Append(toPlayer).Append("); KONTROLA pamieci roznica ").Append(diff)
                      .Append(_stumbles > 0 ? " | potkniecia " + _stumbles : "")
                      .Append(_importN >= 0 ? " | wczytano wpisow " + _importN + " (bledne " + _importBad + ")" : "").Append('.');
                    Log.Info(sb.ToString());
                }
                _importN = -1;
                _dayStart = now;
            }
            catch (Exception e) { Stumble("Report", e); }
            ZeroDay();
        }

        // ------------------------------------------------------------ zapis "arm_okupy430": "v1|rodJenca>rodPorywacza:doba,doba;...|jeniec:porywacz:stan:doba:do:proby:n;..."
        private static readonly char[] Bad = { '|', ';', ':', ',' };

        internal static string Export()
        {
            try
            {
                var sb = new StringBuilder(_ev.Count * 40 + 16);
                sb.Append("v1|");
                bool first = true;
                foreach (var kv in _ev)
                {
                    if (kv.Value.Count == 0 || kv.Key.IndexOfAny(Bad) >= 0) continue;
                    if (!first) sb.Append(';'); first = false;
                    sb.Append(kv.Key).Append(':');
                    for (int i = 0; i < kv.Value.Count; i++) { if (i > 0) sb.Append(','); sb.Append(kv.Value[i].ToString(Inv)); }
                }
                sb.Append('|'); first = true;
                foreach (var kv in _dec)
                {
                    var d = kv.Value;
                    if (kv.Key.IndexOfAny(Bad) >= 0 || d.Captor.IndexOfAny(Bad) >= 0) continue;
                    if (!first) sb.Append(';'); first = false;
                    sb.Append(kv.Key).Append(':').Append(d.Captor).Append(':').Append(d.State.ToString(Inv)).Append(':').Append(d.Day.ToString(Inv)).Append(':')
                      .Append(d.Until.ToString(Inv)).Append(':').Append(d.Tries.ToString(Inv)).Append(':').Append(d.N.ToString(Inv));
                }
                return sb.ToString();
            }
            catch (Exception e) { Stumble("Export", e); return ""; }
        }

        internal static void Import(string data)
        {
            _ev.Clear(); _dec.Clear(); _sale.Clear(); _importN = 0; _importBad = 0;
            try
            {
                if (!string.IsNullOrEmpty(data))
                {
                    var f = data.Split('|');
                    if (f.Length < 3 || f[0] != "v1") _importBad++;
                    else
                    {
                        foreach (var p in f[1].Split(';'))
                        {
                            if (p.Length == 0) continue;
                            var x = p.Split(':');
                            if (x.Length != 2 || x[0].Length == 0 || x[0].IndexOf('>') <= 0) { _importBad++; continue; }
                            var days = new List<int>();
                            foreach (var ds in x[1].Split(',')) { int dd; if (ds.Length > 0 && int.TryParse(ds, NumberStyles.Integer, Inv, out dd)) { days.Add(dd); _importN++; } else if (ds.Length > 0) _importBad++; }
                            if (days.Count > 0) _ev[x[0]] = days;
                        }
                        foreach (var p in f[2].Split(';'))
                        {
                            if (p.Length == 0) continue;
                            var x = p.Split(':');
                            int st, day, until, tries, n;
                            if (x.Length == 7 && x[0].Length > 0 && int.TryParse(x[2], NumberStyles.Integer, Inv, out st) && int.TryParse(x[3], NumberStyles.Integer, Inv, out day)
                                && int.TryParse(x[4], NumberStyles.Integer, Inv, out until) && int.TryParse(x[5], NumberStyles.Integer, Inv, out tries) && int.TryParse(x[6], NumberStyles.Integer, Inv, out n))
                            { _dec[x[0]] = new Dec { Captor = x[1], State = st == 2 ? 2 : 1, Day = day, Until = until, Tries = Math.Max(0, tries), N = Math.Max(0, n) }; _importN++; }
                            else _importBad++;
                        }
                    }
                }
            }
            catch (Exception e) { Stumble("Import", e); }
            _dayStart = Entries();
        }
    }
}
