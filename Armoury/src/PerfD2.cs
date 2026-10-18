using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// PERF-D2D3 (noc 17/18.10, CO-ZOSTALO D2/D3: szybkosc doby naszych modow i cudzego kodu). TE SAME WYNIKI, MNIEJ PRACY - kazda zmiana pod
    /// wlasnym kluczem (domyslnie TAK; wylaczony = kod co do wywolania jak dotad) i z kontrola R9 w probce 1/64 (stara droga policzona obok, rozjazdy
    /// w linii "Szybkosc doby (perf-d2d3)"). Pomiar wyjsciowy: test fin81 (Armoury-2026-10-17_17-55-14.log, "Profil doby (E8/0)").
    ///
    /// 1. PerfValyrianCensus (ValyrianBlades.Gather - dzienny spis stali valyrianskiej, 30-32 ms z 37-39 ms sluchacza LegendaryLaw): (a) napis miejsca
    ///    ("tabor " + nazwa partii, "targ " / "schowek gracza " + nazwa osady) byl skladany dla KAZDEJ z ok. 6 tys. partii i ok. 1.1 tys. osad, zanim
    ///    wiadomo, czy w rosterze lezy klinga - kazdy napis to TextObject.ToString (MBTextManager.ProcessTextToString: lokalizacja i przetwarzanie
    ///    tekstu, bez pamieci podrecznej); teraz roster bez klingi (ten sam warunek co RosterCopies: Amount > 0 i Is) jest pomijany przed napisem;
    ///    (b) bohater, ktory w zadnym slocie broni zestawu bojowego, cywilnego i ukrycia nie ma klingi, nie przechodzi przez Pieces (4-5 alokacji na
    ///    bohatera, ok. 7 tys. bohaterow). Wynik spisu ten sam (pominiete przypadki niczego nie dodawaly).
    /// 2. PerfTownTaxSkipBk (BKTaxModel.CalculateTownTax): przy czynnej rencie miast (Population Rent Enabled + Rent Replaces Town Tax) nasz
    ///    PopulationLaw.TownTaxPostfix ZASTEPUJE caly wynik BK (new ExplainedNumber z linii, ktore ktos naprawde placi - KeptTownLines, Seal31B) i
    ///    CouncilWages35.TownBase nadpisuje DueWage kazdej zajetej pozycji rady ze stawka > 0 - ten sam zbior, ktory pisze BK CalculateDueWages
    ///    (GetOccupiedPositions = Positions z Member != null, AdministrativeCosts() > 0). Cialo BK (podatki klas ludnosci, 5 linii z TextObject,
    ///    warsztaty, kopalnie, autonomia, model administracji, cecha namiestnika) bylo wiec liczone na marne przy kazdym liczeniu dochodu rodu
    ///    (powinnosci korony, ksiega rodow, budowy, Bank, rozliczenie rodu gry; rody krolow z War Tax - wszystkie lenna krolestwa, trzy razy).
    ///    Prefiks (Priority.VeryHigh - po PopulationLaw.TownTaxPrefix) pomija cialo BK tylko gdy: klucz, straznik (model czynny = BKTaxModel z IL 913 B,
    ///    na CalculateTownTax BK i gry tylko nasze latki renty (obce - tylko finalizery), na metodach wolanych w ciele BK zadnej latki poza
    ///    finalizerami), wywolanie zewnetrzne (glebokosc 1), renta zastapi wynik, podstawa rady realna (CouncilWagesRealBase) i BK rozwiazany,
    ///    osada z panem. Kontrola: co 64. takie wywolanie liczy cialo BK (stara droga) i sprawdza, ze TownBase pokryl rade; przy kazdym pominieciu
    ///    tez (TownBase do konca - inaczej "rada niepokryta", prog 0). Stoper na probkach: koszt wywolania z cialem BK i bez.
    /// 3. PerfSutlerWageOnce (Sutlers.One - markietani, 42-52 ms): zold partii (MobileParty.TotalWage - model zoldu przez lancuch BKROT, kazdy wiersz
    ///    rosteru) liczony 2-3 razy na partie (rezerwa CampFree, drugi raz przy ograniczonej rezerwie, WageOf) - teraz raz w zakresie jednej partii
    ///    (MenPurse.WageMemoBegin/End); miedzy odczytami same odczyty. Probka 1/64 liczy zold drugi raz i porownuje.
    /// 4. Pomiar (tylko log): dochod modelu w ksiedze rodow (ClanIncomeBook) wedlug rodzaju rodu - gdzie siedzi 70-85 ms CalculateClanIncome.
    /// 5. PerfTownTaxFastHooks (PopulationLaw.KeptTownLines, Seal31B.NotableTaxLines / UnpaidOwnerTaxLines / Autonomy, CouncilWages35.TownBase - nasze haki
    ///    w kazdym liczeniu podatku miasta przy rencie; po pominieciu ciala BK to one sa jego kosztem): te same wywolania BK (GetWorkshopTaxes, GetMiningRevenue,
    ///    GetMaterialExpenses, GetPopData, Autonomy, CourtManager.GetCouncil, Positions, Member, AdministrativeCosts, DueWage) przez skompilowane delegaty
    ///    (PerfFast) zamiast MethodInfo.Invoke / PropertyInfo.GetValue / Traverse (ok. 100 ns na wywolanie odbiciem wobec ok. 7 ns delegatem - pomiar
    ///    syntetyczny net472 tej nocy), zachowanie BKBuildingsBehavior z pamieci kampanii (GetCampaignBehavior szuka liniowo po ok. 300 zachowaniach). Co 64.
    ///    wywolanie liczy tez stara droga (odbicie) i porownuje wynik - rozjazd: wynik starej drogi, licznik.
    /// 3b. f4b-perf PerfSutlerWageExact: pamiec zoldu z punktu 3 tylko dla zoldu PEWNEGO - BK liczy zold bohatera partii poza wodzem z losowym zaokragleniem
    ///    (BKCompanionPrices.GetHeroWage: MBRandom.RoundRandomized), wiec zold takiej partii jest inny przy kazdym liczeniu i kazde liczenie zuzywa losowanie
    ///    wspolnego generatora gry (fin87 NPERF-4: 44 rozjazdy na 2141 probek). Pamiec: bez bohatera poza wodzem i bez losowania przy pierwszym liczeniu
    ///    (straznik stanu MBFastRandom); reszta - kazdy odczyt jak dotad.
    /// 5b. f4b-perf PerfProbeEachPlace: probki hakow podatku miast z punktu 5 osobno na miejsce (wspolny licznik 1/64 przy 4 hakach na liczenie trafial
    ///    zawsze w TownBase - trzy miejsca nigdy nie byly sprawdzane) i liczba wywolan kazdego miejsca w linii doby.
    /// Linia doby "Szybkosc doby (perf-d2d3): dzien N | ..." (za okres od poprzedniej linii; wolana na poczatku taktu doby po Cost174).
    /// Bez zapisu w grze (same liczniki sesji). Wolane z watku gry; licznik podatku miast - Interlocked (podatek bywa liczony z watkow AI).
    /// </summary>
    internal static class PerfD2
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // ------------------------------------------------------------ klucze
        internal static bool ValyrianOn { get { var s = Settings.Current; return s != null && s.PerfValyrianCensus; } }
        internal static bool SutlerWageOn { get { var s = Settings.Current; return s != null && s.PerfSutlerWageOnce; } }
        internal static bool TaxSkipKey { get { var s = Settings.Current; return s != null && s.PerfTownTaxSkipBk; } }
        internal static bool FastHooksOn { get { var s = Settings.Current; return s != null && s.PerfTownTaxFastHooks; } }
        // f4b-perf (noc 17/18.10): pamiec zoldu markietanow tylko dla zoldu pewnego (3b) i probki hakow podatku miast osobno na miejsce (5b)
        internal static bool SutlerExactOn { get { var s = Settings.Current; return s != null && s.PerfSutlerWageExact; } }
        internal static bool ProbeEachOn { get { var s = Settings.Current; return s != null && s.PerfProbeEachPlace; } }

        private static double Ms(long ticks) { return ticks * 1000.0 / Stopwatch.Frequency; }
        private static double Us(long ticks, long n) { return n > 0 ? ticks * 1000000.0 / Stopwatch.Frequency / n : 0.0; }

        // ------------------------------------------------------------ stan sesji (bez zapisu): nowa kampania - od zera
        private static object _campaign;
        private static int _lastDay = -1;
        private static void EnsureCampaign()
        {
            var c = Campaign.Current;
            if (ReferenceEquals(c, _campaign)) return;
            _campaign = c; _lastDay = -1;
            ZeroPeriod();
        }

        // ================================================================= 1. spis stali valyrianskiej
        private static int _vsSeq, _vsGathers, _vsGathersFast;
        private static long _vsTicks;
        private static int _vsHeroSkip, _vsHeroProbe, _vsHeroDiff, _vsRosterSkip, _vsRosterProbe, _vsRosterDiff;

        /// <summary>Roster bez klingi (empty = AnyIn falsz): true - RosterCopies trzeba wolac. Co 64. pominiecie: stara droga liczenia (CountIn) - 0 sztuk,
        /// inaczej rozjazd (licznik) i RosterCopies wolane jak dotad.</summary>
        internal static bool VsRoster(bool empty, ItemRoster r)
        {
            if (!empty) return true;
            if ((++_vsSeq & 63) == 0)
            {
                _vsRosterProbe++;
                try { if (ValyrianBlades.CountIn(r) != 0) { _vsRosterDiff++; return true; } }
                catch { _vsRosterDiff++; return true; }
            }
            _vsRosterSkip++;
            return false;
        }

        /// <summary>Bohater bez klingi w zadnym slocie broni (MayHold falsz): true - pominac Pieces. Co 64. pominiecie: Pieces policzone (stara droga) -
        /// pusta lista, inaczej rozjazd (licznik) i bohater liczony jak dotad.</summary>
        internal static bool VsHero(Hero h)
        {
            if ((++_vsSeq & 63) == 0)
            {
                _vsHeroProbe++;
                try { if (ValyrianBlades.Pieces(h, null).Count != 0) { _vsHeroDiff++; return false; } }
                catch { _vsHeroDiff++; return false; }
            }
            _vsHeroSkip++;
            return true;
        }

        internal static void VsGatherTicks(long dt, bool fast)
        {
            _vsGathers++; if (fast) _vsGathersFast++;
            _vsTicks += dt;
        }

        // ================================================================= 3. zold markietanow raz na partie
        private static int _wSeq, _wScopes, _wHits, _wProbes, _wDiff;
        private static long _wProbeTicks;

        /// <summary>MenPurse.Wage w zakresie partii markietanow - odczyt z pamieci; co 64. odczyt liczy zold drugi raz (stara droga) i porownuje
        /// (rozjazd - licznik i swiezy wynik).</summary>
        internal static int WageHit(MobileParty mp, int memo)
        {
            _wHits++;
            if ((++_wSeq & 63) == 0)
            {
                _wProbes++;
                long t = Stopwatch.GetTimestamp();
                int fresh = MenPurse.WageRaw(mp);
                _wProbeTicks += Stopwatch.GetTimestamp() - t;
                if (fresh != memo) { _wDiff++; return fresh; }
            }
            return memo;
        }

        internal static void WageScope() { _wScopes++; }

        // ================================================================= 3b. f4b-perf (PerfSutlerWageExact): pamiec zoldu tylko dla zoldu pewnego
        // Dowod (fin87, Armoury-2026-10-18_00-26-23.log, NPERF-4: 44 rozjazdy na 2141 probek): BannerKings BKPartyWageModel.GetTotalWage dodaje zold kazdego
        // bohatera w rosterze partii poza jej wodzem (towarzysze, czlonkowie rodu; gracz z umowa BK - Contract.Wage) przez BKCompanionPrices.GetHeroWage =
        // MBRandom.RoundRandomized(suma umiejetnosci x 0.005) - kazde liczenie zoldu takiej partii losuje zaokraglenie i zuzywa jedno losowanie wspolnego
        // generatora gry (Game.RandomGenerator, MBRandom.RoundRandomized zawsze wola RandomFloat) na bohatera (dekompilacja BannerKings.Redux i TaleWorlds.Core).
        // Pamiec perf-d2d3 zmieniala wiec wynik (2. i 3. odczyt = pierwsze losowanie, nie nowe) i ciag losowy calej gry (mniej losowan). Teraz pamiec tylko, gdy
        // (a) w rosterze nie ma bohatera poza wodzem i (b) pierwsze liczenie w zakresie nie ruszylo generatora losowego (straznik na stanie MBFastRandom: _x _y _z _w -
        // kazde inne, nieznane losowanie w lancuchu zoldu tez wylacza pamiec tej partii). Reszta partii - kazdy odczyt liczony jak dotad (te same losowania).
        // Probka 1/64 odczytow z pamieci: zold liczony drugi raz (jak stara droga w tym odczycie) - rozjazd albo losowanie w probce zdejmuje pamiec do konca zakresu.
        private static int _xFirst, _xSure, _xHero, _xDrew, _xRaw, _xProbeDrew, _xNoGuard;

        // straznik losowania: stan generatora gry (MBRandom.Random = Game.Current.RandomGenerator, MBFastRandom - xorshift128, pola _x _y _z _w)
        private static bool _rngTried, _rngOk;
        private static string _rngWhy = "nie sprawdzano";
        private static Func<TaleWorlds.Core.Game, TaleWorlds.Core.MBFastRandom> _rngGet;
        private static AccessTools.FieldRef<TaleWorlds.Core.MBFastRandom, uint> _rX, _rY, _rZ, _rW;

        internal struct RngState { public uint X, Y, Z, W; public bool Ok; }

        private static void RngResolve()
        {
            _rngTried = true;
            try
            {
                var g = AccessTools.PropertyGetter(typeof(TaleWorlds.Core.Game), "RandomGenerator");
                if (g == null) { _rngWhy = "brak Game.RandomGenerator"; return; }
                _rngGet = (Func<TaleWorlds.Core.Game, TaleWorlds.Core.MBFastRandom>)Delegate.CreateDelegate(typeof(Func<TaleWorlds.Core.Game, TaleWorlds.Core.MBFastRandom>), g);
                _rX = AccessTools.FieldRefAccess<TaleWorlds.Core.MBFastRandom, uint>("_x");
                _rY = AccessTools.FieldRefAccess<TaleWorlds.Core.MBFastRandom, uint>("_y");
                _rZ = AccessTools.FieldRefAccess<TaleWorlds.Core.MBFastRandom, uint>("_z");
                _rW = AccessTools.FieldRefAccess<TaleWorlds.Core.MBFastRandom, uint>("_w");
                _rngOk = _rngGet != null && _rX != null && _rY != null && _rZ != null && _rW != null;
                _rngWhy = _rngOk ? "stan MBFastRandom (_x _y _z _w) generatora gry" : "brak pol MBFastRandom";
            }
            catch (Exception e) { _rngOk = false; _rngWhy = "blad: " + e.GetType().Name; }
        }

        /// <summary>Stan generatora losowego gry teraz (Ok falsz - straznik niedostepny w tym odczycie: decyduje sam warunek bohaterow).</summary>
        internal static RngState RngNow()
        {
            var r = default(RngState);
            if (!_rngTried) RngResolve();
            if (!_rngOk) return r;
            try
            {
                var game = TaleWorlds.Core.Game.Current;
                var gen = game != null ? _rngGet(game) : null;
                if (gen == null) return r;
                r.X = _rX(gen); r.Y = _rY(gen); r.Z = _rZ(gen); r.W = _rW(gen); r.Ok = true;
            }
            catch { r.Ok = false; }   // jeden odczyt - bez globalnego wylacznika (nastepny probuje znowu)
            return r;
        }

        /// <summary>Czy miedzy a i teraz generator gry losowal (oba odczyty czynne i stan inny). Nieczynny straznik - falsz.</summary>
        private static bool RngMoved(RngState a, out bool known)
        {
            known = false;
            if (!a.Ok) return false;
            var b = RngNow();
            if (!b.Ok) return false;
            known = true;
            return a.X != b.X || a.Y != b.Y || a.Z != b.Z || a.W != b.W;
        }

        /// <summary>Warunek (a): w rosterze partii zaden bohater poza wodzem (LeaderHero) - BK nie dolicza zoldu bohatera (GetHeroWage, losowane zaokraglenie).
        /// Bez wodza (LeaderHero null) kazdy bohater w rosterze idzie przez GetHeroWage - wtedy tez falsz.</summary>
        internal static bool WageSure(MobileParty mp)
        {
            var r = mp != null ? mp.MemberRoster : null;
            if (r == null) return false;
            if (r.TotalHeroes <= 0) return true;
            var leader = mp.LeaderHero;
            for (int i = 0; i < r.Count; i++)
            {
                var c = r.GetCharacterAtIndex(i);
                if (c != null && c.IsHero && !ReferenceEquals(c.HeroObject, leader)) return false;
            }
            return true;
        }

        /// <summary>Pierwszy odczyt zoldu partii w zakresie przy PerfSutlerWageExact: zold liczony jak dotad (WageRaw); keep - wolno go pamietac (warunek bohaterow
        /// i straznik losowania). Wyjatek warunku - bez pamieci (stara droga).</summary>
        internal static int WageFirst(MobileParty mp, out bool keep)
        {
            keep = false;
            _xFirst++;
            bool sure;
            try { sure = WageSure(mp); } catch { sure = false; }
            if (!sure) { _xHero++; return MenPurse.WageRaw(mp); }
            var a = RngNow();
            int v = MenPurse.WageRaw(mp);
            bool known;
            if (RngMoved(a, out known)) { _xDrew++; return v; }
            if (!known) _xNoGuard++;
            _xSure++;
            keep = true;
            return v;
        }

        /// <summary>Odczyt zoldu partii bez pamieci w zakresie (zold niepewny) - liczony jak dotad.</summary>
        internal static int WageNoMemo(MobileParty mp) { _xRaw++; return MenPurse.WageRaw(mp); }

        /// <summary>Odczyt z pamieci przy PerfSutlerWageExact: jak WageHit, a probka sprawdza tez generator losowy; rozjazd albo losowanie w probce - drop
        /// (pamiec zdjeta do konca zakresu, dalsze odczyty liczone jak dotad).</summary>
        internal static int WageHitExact(MobileParty mp, int memo, out bool drop)
        {
            drop = false;
            _wHits++;
            if ((++_wSeq & 63) == 0)
            {
                _wProbes++;
                var a = RngNow();
                long t = Stopwatch.GetTimestamp();
                int fresh = MenPurse.WageRaw(mp);
                _wProbeTicks += Stopwatch.GetTimestamp() - t;
                bool known;
                if (RngMoved(a, out known)) { _xProbeDrew++; drop = true; }
                if (fresh != memo) { _wDiff++; drop = true; }
                if (drop) return fresh;
            }
            return memo;
        }

        // ================================================================= 4. dochod modelu w ksiedze rodow wedlug rodzaju rodu (tylko pomiar)
        private const int MNone = 0, MFief = 1, MRuler = 2;
        private static readonly long[] _mTicks = new long[3];
        private static readonly int[] _mCalls = new int[3];
        private static int _mFiefs;

        /// <summary>ClanIncomeBook.Daily: czas jednego CalculateClanIncome rodu (ten sam stoper co "dochod modelu" linii Obieg) wedlug rodzaju rodu -
        /// rod krola (AddRulingClanIncome: polityki krolestwa), rod z lennami (podatek miast i wsi), reszta. Tylko liczniki.</summary>
        internal static void NoteModel(Clan c, long dt)
        {
            try
            {
                int k = MNone, nf = 0;
                var f = c.Fiefs;
                if (f != null) nf = f.Count;
                var kg = c.Kingdom;
                if (kg != null && ReferenceEquals(kg.RulingClan, c)) k = MRuler;
                else if (nf > 0) k = MFief;
                _mTicks[k] += dt; _mCalls[k]++; _mFiefs += nf;
            }
            catch { }
        }

        // ================================================================= 2. podatek miast: cialo BK pominiete, gdy renta i tak zastepuje wynik
        private static Harmony _harmony;
        internal static void SetHarmony(Harmony h) { _harmony = h; }

        private const string BkTaxType = "BannerKings.Models.Vanilla.BKTaxModel";
        private const int BkTaxIl = 913;   // dlugosc IL BKTaxModel.CalculateTownTax (BannerKings.Redux w grze 17.10 - dekompilacja tej nocy); inna = inna logika
        private static MethodInfo _taxTarget;   // metoda zalatana (raz na proces; Patch/Unpatch tym samym MethodInfo)
        private static bool _taxTried, _taxLive;
        private static string _taxWhy = "nie sprawdzano", _taxPatchErr;
        private static int _guardDay = -1;

        // liczniki (Interlocked - podatek bywa liczony z watkow AI)
        private static long _tCalls, _tSkip, _tProbe, _tOff, _tUncov, _tProbeUncov, _tSeq, _tSampSeq;
        private static long _tSkipT, _tSkipTN, _tBodyT, _tBodyTN, _tExSkip, _tExBody;

        internal struct TaxState { public int Mode; public long T0; }   // Mode: 0 poza warunkiem, 1 cialo BK pominiete, 2 probka (cialo BK liczone)

        /// <summary>Start sesji (po PopulationLaw.ApplyTownTax, CouncilWages35 i Seal31B): straznik i - raz na proces - prefiks na czynnym CalculateTownTax BK.</summary>
        internal static void EnsureHooks()
        {
            EnsureCampaign();
            try
            {
                string why;
                MethodInfo m;
                bool ok = Guard(out m, out why);
                if (ok && !_taxTried && TaxSkipKey && _harmony != null)
                {
                    _taxTried = true;
                    try
                    {
                        _harmony.Patch(m, prefix: new HarmonyMethod(typeof(PerfD2), nameof(TaxSkipPrefix)) { priority = Priority.VeryHigh },
                                          finalizer: new HarmonyMethod(typeof(PerfD2), nameof(TaxSkipFinalizer)));
                        _taxTarget = m;
                    }
                    catch (Exception e) { ok = false; why = _taxPatchErr = "blad latki: " + e.Message; Log.Error("PerfD2.EnsureHooks", e); }
                }
                if (_taxTarget == null && ok)
                {
                    ok = false;
                    why = _taxPatchErr ?? (!TaxSkipKey ? "klucz wylaczony przy starcie sesji - latka niezalozona (wlaczenie w trakcie gry dziala od nastepnego wczytania)"
                                                        : "latka niezalozona (brak Harmony)");
                }
                _taxLive = ok && _taxTarget != null;
                _taxWhy = why;
            }
            catch (Exception e) { _taxLive = false; _taxWhy = "blad: " + e.Message; Log.Error("PerfD2.EnsureHooks", e); }
            Log.Info("Szybkosc doby (perf-d2d3): start sesji | spis stali valyrianskiej (PerfValyrianCensus) " + OnOff(ValyrianOn)
                     + " | podatek miast - cialo BK pominiete przy rencie (PerfTownTaxSkipBk) " + OnOff(TaxSkipKey) + ", straznik: " + (_taxLive ? "CZYNNY" : "STOI") + " (" + _taxWhy + ")"
                     + " | zold markietanow raz na partie (PerfSutlerWageOnce) " + OnOff(SutlerWageOn)
                     + " | haki podatku miast bez refleksji (PerfTownTaxFastHooks) " + OnOff(FastHooksOn)
                     + " | zold markietanow tylko pewny (PerfSutlerWageExact) " + OnOff(SutlerExactOn) + " | probki hakow osobno na miejsce (PerfProbeEachPlace) " + OnOff(ProbeEachOn)
                     + " | kontrola: stara droga obok w probce 1/64, rozjazdy w linii doby.");
        }

        private static string OnOff(bool b) { return b ? "[WL]" : "[wyl]"; }

        /// <summary>Straznik: model czynny = BKTaxModel (IL jak w dekompilacji), nasza latka renty na nim, na CalculateTownTax BK i gry tylko nasze latki
        /// renty i ten prefiks (obce - tylko finalizery), na metodach wolanych w ciele BK zadnej latki poza finalizerami (pominiete cialo nie moze pominac
        /// cudzego skutku). m - metoda do zalatania.</summary>
        private static bool Guard(out MethodInfo m, out string why)
        {
            m = null; why = "-";
            var models = Campaign.Current != null ? Campaign.Current.Models : null;
            object model = models != null ? models.SettlementTaxModel : null;
            if (model == null) { why = "brak modelu podatku"; return false; }
            var t = model.GetType();
            if (t.FullName != BkTaxType) { why = "czynny model podatku " + t.FullName + " (oczekiwany " + BkTaxType + ")"; return false; }
            m = AccessTools.DeclaredMethod(t, "CalculateTownTax", new[] { typeof(Town), typeof(bool) });
            if (m == null) { why = "brak BKTaxModel.CalculateTownTax"; return false; }
            int il = -1;
            try { var b = m.GetMethodBody(); il = b != null ? b.GetILAsByteArray().Length : -1; } catch { }
            if (il != BkTaxIl) { why = "BKTaxModel.CalculateTownTax: IL " + il + " B (oczekiwane " + BkTaxIl + " - inna wersja BK)"; return false; }
            var baseM = AccessTools.DeclaredMethod(typeof(TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementTaxModel), "CalculateTownTax", new[] { typeof(Town), typeof(bool) });
            var bad = new List<string>();
            bool rent = false;
            CheckTax(m, bad, ref rent);
            bool rentBase = false;
            if (baseM != null) CheckTax(baseM, bad, ref rentBase);
            if (!rent) bad.Add("brak PopulationLaw.TownTaxPostfix na BKTaxModel.CalculateTownTax");
            // metody wolane w ciele BKTaxModel.CalculateTownTax (dekompilacja BannerKings.Redux) - latka (poza finalizerem) = pominiecie ciala pominie jej skutek
            var callees = new[]
            {
                "BannerKings.Models.Vanilla.BKTaxModel:GetNobleTaxRate", "BannerKings.Models.Vanilla.BKTaxModel:GetCraftsmenTaxRate",
                "BannerKings.Models.Vanilla.BKTaxModel:GetTenantTaxRate", "BannerKings.Models.Vanilla.BKTaxModel:GetSerfTaxRate",
                "BannerKings.Models.Vanilla.BKTaxModel:GetSlaveTaxRate", "BannerKings.Models.Vanilla.BKTaxModel:CalculateDueTax",
                "BannerKings.Models.Vanilla.BKTaxModel:CalculateDueWages", "BannerKings.Models.Vanilla.BKTaxModel:GetTownTaxRatio",
                "TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementTaxModel:GetTownTaxRatio",
                "BannerKings.Models.BKModels.BKAdministrativeModel:CalculateEffect",
                "BannerKings.Behaviours.BKBuildingsBehavior:GetMiningRevenue", "BannerKings.Behaviours.BKBuildingsBehavior:GetMaterialExpenses",
                "BannerKings.Models.Vanilla.BKClanFinanceModel:GetWorkshopTaxes",
                "BannerKings.Managers.PolicyManager:GetPolicy", "BannerKings.Managers.PolicyManager:IsDecisionEnacted",
                "BannerKings.Managers.PolicyManager:GetActiveCostlyDecisionsNumber",
                "BannerKings.Managers.CourtManager:GetCouncil", "BannerKings.Managers.PopulationManager:GetPopData",
                "BannerKings.Managers.TitleManager:GetTitle", "BannerKings.Managers.InnovationsManager:GetInnovationData",
                "BannerKings.Managers.Court.CouncilData:GetOccupiedPositions", "BannerKings.Managers.Court.CouncilMember:AdministrativeCosts",
                "BannerKings.Managers.Court.CouncilMember:set_DueWage", "BannerKings.Managers.Populations.EconomicData:get_Tariff",
                "BannerKings.Managers.Populations.EconomicData:get_ConsumedValue", "BannerKings.Managers.Populations.PopulationData:get_Autonomy",
                "BannerKings.Managers.Populations.PopulationData:GetTypeCount", "BannerKings.Utils.Helpers:ApplyTraitEffect",
                "BannerKings.Managers.Cultures.DefaultPopulationNames:GetPopulationName", "BannerKings.Managers.Titles.FeudalContract:IsLawEnacted",
                "TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementTaxModel:CalculateDailyTaxInternal",
                "TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementTaxModel:CalculateDailyTax",
                "TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementTaxModel:CalculatePolicyGoldCut"
            };
            int seen = 0;
            foreach (var c in callees)
            {
                int i = c.IndexOf(':');
                var ct = AccessTools.TypeByName(c.Substring(0, i));
                if (ct == null) continue;
                string name = c.Substring(i + 1);
                foreach (var cm in ct.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    if (cm.Name != name) continue;
                    seen++;
                    var info = Harmony.GetPatchInfo(cm);
                    if (info == null) continue;
                    int n = info.Prefixes.Count + info.Postfixes.Count + info.Transpilers.Count;
                    if (n > 0) bad.Add(ct.Name + "." + name + " (latek " + n + ": " + Owners(info) + ")");
                }
            }
            if (bad.Count > 0) { why = "obce latki: " + string.Join("; ", bad.ToArray()); return false; }
            if (!CouncilWages35.BaseReady) { why = "podstawa rady nieczynna (Council Wages Real Base albo BK) - DueWage pisze BK"; return false; }
            why = "BKTaxModel IL " + il + " B, latki CalculateTownTax tylko renta (PopulationLaw) i finalizery, metod wolanych w ciele BK sprawdzonych " + seen + " - bez latek";
            return true;
        }

        private static string Owners(HarmonyLib.Patches info)
        {
            var o = new List<string>();
            foreach (var p in info.Prefixes) o.Add("prefiks " + p.owner);
            foreach (var p in info.Postfixes) o.Add("postfiks " + p.owner);
            foreach (var p in info.Transpilers) o.Add("transpiler " + p.owner);
            return string.Join(", ", o.ToArray());
        }

        /// <summary>Latki na CalculateTownTax: dozwolone nasze PopulationLaw.TownTaxPrefix/Postfix, ten prefiks i finalizery kazdego; reszta - do listy.</summary>
        private static void CheckTax(MethodInfo m, List<string> bad, ref bool rent)
        {
            var info = Harmony.GetPatchInfo(m);
            if (info == null) return;
            var mine = typeof(PerfD2).Assembly;
            foreach (var p in info.Prefixes)
            {
                var pm = p.PatchMethod;
                bool ok = pm != null && pm.DeclaringType != null && pm.DeclaringType.Assembly == mine
                          && ((pm.DeclaringType == typeof(PopulationLaw) && pm.Name == "TownTaxPrefix") || (pm.DeclaringType == typeof(PerfD2) && pm.Name == nameof(TaxSkipPrefix)));
                if (!ok) bad.Add(m.DeclaringType.Name + ".CalculateTownTax prefiks " + p.owner + " (" + (pm != null ? pm.DeclaringType + "." + pm.Name : "?") + ")");
            }
            foreach (var p in info.Postfixes)
            {
                var pm = p.PatchMethod;
                bool ok = pm != null && pm.DeclaringType == typeof(PopulationLaw) && pm.Name == "TownTaxPostfix";
                if (ok) rent = true;
                else bad.Add(m.DeclaringType.Name + ".CalculateTownTax postfiks " + p.owner + " (" + (pm != null ? pm.DeclaringType + "." + pm.Name : "?") + ")");
            }
            foreach (var p in info.Transpilers) bad.Add(m.DeclaringType.Name + ".CalculateTownTax transpiler " + p.owner);
        }

        /// <summary>Prefiks BKTaxModel.CalculateTownTax (Priority.VeryHigh - po PopulationLaw.TownTaxPrefix, ktory podnosi glebokosc). false = cialo BK
        /// pominiete (wynik zastapi TownTaxPostfix, rade nadpisze TownBase). Klucz wylaczony w trakcie gry - zawsze true (cialo jak dotad).</summary>
        public static bool TaxSkipPrefix(Town __0, bool __1, ref TaleWorlds.CampaignSystem.ExplainedNumber __result, out TaxState __state)
        {
            __state = default(TaxState);
            if (PopulationLaw.TaxDepth == 1) CouncilWages35.BkBodySkipped = false;   // recenzja: znacznik tylko dla wywolania zewnetrznego, zawsze od zera
            try
            {
                Interlocked.Increment(ref _tCalls);
                if (!_taxLive || !TaxSkipKey) { Interlocked.Increment(ref _tOff); return true; }
                if (PopulationLaw.TaxDepth != 1 || !PopulationLaw.RentReplacesNow || !CouncilWages35.BaseReady) { Interlocked.Increment(ref _tOff); return true; }
                var st = __0 != null ? __0.Settlement : null;
                if (st == null || st.OwnerClan == null) { Interlocked.Increment(ref _tOff); return true; }   // BK bez pana rzuca (rada null) - jak dotad
                CouncilWages35.TownBaseOk = false;
                bool sample = (Interlocked.Increment(ref _tSampSeq) & 63) == 1;
                if ((Interlocked.Increment(ref _tSeq) & 63) == 0)
                {
                    // R9: stara droga (cialo BK) - kontrola pokrycia rady w finalizerze
                    Interlocked.Increment(ref _tProbe);
                    __state.Mode = 2; __state.T0 = Stopwatch.GetTimestamp();
                    return true;
                }
                __state.Mode = 1; __state.T0 = sample ? Stopwatch.GetTimestamp() : 0L;
                Interlocked.Increment(ref _tSkip);
                __result = new TaleWorlds.CampaignSystem.ExplainedNumber(0f, __1, null);   // zastapi go TownTaxPostfix (renta)
                CouncilWages35.BkBodySkipped = true;   // recenzja: TownBase nie liczy DueWage sprzed zapisu jako pensji BK (zdejmuje finalizer)
                return false;
            }
            catch { __state = default(TaxState); return true; }
        }

        /// <summary>Finalizer (po postfiksach): czy TownBase doszedl do konca (rada pokryta) i stoper probki. Nie zmienia wyjatku.</summary>
        public static void TaxSkipFinalizer(Exception __exception, TaxState __state)
        {
            if (__state.Mode == 1) CouncilWages35.BkBodySkipped = false;   // recenzja: znacznik tylko na czas tego wywolania (po TownTaxPostfix)
            if (__state.Mode == 0) return;
            try
            {
                long now = Stopwatch.GetTimestamp();
                if (__exception != null)   // wyjatek w lancuchu (cialo BK albo latki) - tylko licznik, wyjatek idzie dalej
                {
                    if (__state.Mode == 1) Interlocked.Increment(ref _tExSkip); else Interlocked.Increment(ref _tExBody);
                }
                if (__state.Mode == 1)
                {
                    if (!CouncilWages35.TownBaseOk) Interlocked.Increment(ref _tUncov);
                    if (__state.T0 != 0) { Interlocked.Add(ref _tSkipT, now - __state.T0); Interlocked.Increment(ref _tSkipTN); }
                }
                else
                {
                    if (!CouncilWages35.TownBaseOk) Interlocked.Increment(ref _tProbeUncov);
                    Interlocked.Add(ref _tBodyT, now - __state.T0); Interlocked.Increment(ref _tBodyTN);
                }
            }
            catch { }
        }

        // ================================================================= 5. haki podatku miast bez refleksji (PerfTownTaxFastHooks)
        // PopulationLaw.KeptTownLines, Seal31B.NotableTaxLines / UnpaidOwnerTaxLines / Autonomy, CouncilWages35.TownBase: te same wywolania BK przez skompilowane
        // delegaty (PerfFast) zamiast MethodInfo.Invoke / PropertyInfo.GetValue / Traverse, zachowanie BK z pamieci kampanii. Co 64. wywolanie (wspolny licznik)
        // liczy tez stara droga i porownuje wynik (rozjazd - stara droga wygrywa, licznik w linii).
        internal const int FKept = 0, FNotable = 1, FUnpaid = 2, FBase = 3, FBeh = 4, FSites = 5;
        private static readonly string[] FNames = { "linie placone (KeptTownLines)", "linie notabli", "linie wlascicieli", "rada (TownBase)", "zachowanie BK z pamieci" };
        private static readonly long[] _fProbe = new long[FSites], _fDiff = new long[FSites];
        private static long _fSeq;

        internal static bool FastProbe() { return (Interlocked.Increment(ref _fSeq) & 63) == 0; }

        // 5b. f4b-perf (PerfProbeEachPlace): jedno liczenie podatku miasta przy rencie wola FastProbe dokladnie 4 razy w stalej kolejnosci (KeptTownLines,
        // UnpaidOwnerTaxLines, NotableTaxLines, TownBase - PopulationLaw.TownTaxPostfix), a wspolny licznik probkuje co 64. wywolanie - 64 dzieli sie przez 4,
        // wiec probka trafiala ZAWSZE w TownBase (fin87: rada 87 probek/dobe, linie placone, notabli i wlascicieli 0 probek przez 160 dob - "rozjazdow 0" tych
        // trzech miejsc nic nie sprawdzalo). Teraz kazde miejsce ma wlasny licznik (co 64. jego wywolanie) i licznik wywolan do linii doby; klucz wylaczony -
        // wspolny licznik jak dotad. Probka liczy obok stara droge (same odczyty BK - sprawdzone w dekompilacji: GetWorkshopTaxes, GetMiningRevenue,
        // GetMaterialExpenses - odczyt slownika, GetPopData i GetCouncil - leniwe tworzenie raz, Autonomy, Positions, Member, AdministrativeCosts - pola i stale),
        // wiec wynik gry ten sam przy kazdym wzorze probek.
        private static readonly long[] _fSeqAt = new long[FSites], _fCalls = new long[FSites];

        internal static bool FastProbe(int site)
        {
            if (site < 0 || site >= FSites) return FastProbe();
            Interlocked.Increment(ref _fCalls[site]);
            if (!ProbeEachOn) return FastProbe();   // klucz wylaczony - wspolny licznik jak dotad
            return (Interlocked.Increment(ref _fSeqAt[site]) & 63) == 0;
        }

        /// <summary>f4b-perf: wywolanie miejsca z wlasnym licznikiem probek (zachowanie BK z pamieci - PerfFast.Behavior) - tylko licznik linii.</summary>
        internal static void FastCall(int site) { if (site >= 0 && site < FSites) Interlocked.Increment(ref _fCalls[site]); }

        /// <summary>Wynik probki starej drogi: same - ten sam wynik. Zwraca same (falsz - wolajacy bierze wynik starej drogi).</summary>
        internal static bool FastSame(int site, bool same)
        {
            Interlocked.Increment(ref _fProbe[site]);
            if (!same) Interlocked.Increment(ref _fDiff[site]);
            return same;
        }

        // ================================================================= linia doby
        private static void ZeroPeriod()
        {
            for (int i = 0; i < FSites; i++) { Interlocked.Exchange(ref _fProbe[i], 0); Interlocked.Exchange(ref _fDiff[i], 0); }
            _vsGathers = _vsGathersFast = 0; _vsTicks = 0;
            _vsHeroSkip = _vsHeroProbe = _vsHeroDiff = _vsRosterSkip = _vsRosterProbe = _vsRosterDiff = 0;
            _wScopes = _wHits = _wProbes = _wDiff = 0; _wProbeTicks = 0;
            _xFirst = _xSure = _xHero = _xDrew = _xRaw = _xProbeDrew = _xNoGuard = 0;   // f4b-perf
            for (int i = 0; i < FSites; i++) Interlocked.Exchange(ref _fCalls[i], 0);   // f4b-perf (licznik probek _fSeqAt bez zerowania - faza plynie jak _fSeq)
            Array.Clear(_mTicks, 0, 3); Array.Clear(_mCalls, 0, 3); _mFiefs = 0;
            Interlocked.Exchange(ref _tCalls, 0); Interlocked.Exchange(ref _tSkip, 0); Interlocked.Exchange(ref _tProbe, 0); Interlocked.Exchange(ref _tOff, 0);
            Interlocked.Exchange(ref _tUncov, 0); Interlocked.Exchange(ref _tProbeUncov, 0);
            Interlocked.Exchange(ref _tSkipT, 0); Interlocked.Exchange(ref _tSkipTN, 0); Interlocked.Exchange(ref _tBodyT, 0); Interlocked.Exchange(ref _tBodyTN, 0);
            Interlocked.Exchange(ref _tExSkip, 0); Interlocked.Exchange(ref _tExBody, 0);
        }

        /// <summary>Poczatek taktu doby (ArmouryBehavior, po Cost174.Daily): linia za okres od poprzedniej linii, zerowanie, straznik raz na dobe.</summary>
        internal static void Daily()
        {
            EnsureCampaign();
            long t0 = Stopwatch.GetTimestamp();
            int today = (int)CampaignTime.Now.ToDays;
            try
            {
                // straznik raz na dobe - obca latka dopisana w trakcie gry zatrzymuje pomijanie ciala BK (linia mowi dlaczego)
                if (_taxTarget != null && today != _guardDay)
                {
                    _guardDay = today;
                    string why; MethodInfo m;
                    bool ok = Guard(out m, out why) && m != null && m.Equals(_taxTarget);
                    if (ok != _taxLive) Log.Info("Szybkosc doby (perf-d2d3): straznik podatku miast " + (ok ? "CZYNNY" : "STOI") + " od doby " + today + " (" + why + ").");
                    _taxLive = ok; _taxWhy = why;
                }
                var s = Settings.Current;
                // pierwszy takt sesji - okres niepelny (od wczytania), bez linii; dalej dzien ksiegi = dzien gry - 1 (jak "Koszt 171-174")
                if (_lastDay >= 0 && s != null && s.LogEnabled) Log.Info(Line(today - 1, Ms(Stopwatch.GetTimestamp() - t0)));
            }
            catch (Exception e) { Log.Error("PerfD2.Daily", e); }
            ZeroPeriod();
            _lastDay = today;
        }

        private static string Line(int day, double guardMs)
        {
            var sb = new StringBuilder(900);
            sb.Append("Szybkosc doby (perf-d2d3): dzien ").Append(day);
            // 1
            sb.Append(" | spis stali valyrianskiej (PerfValyrianCensus) ").Append(OnOff(ValyrianOn)).Append(": spisow ").Append(_vsGathers).Append(" (szybkich ").Append(_vsGathersFast)
              .Append("), czas ").Append(Ms(_vsTicks).ToString("0.0", Inv)).Append(" ms, bohaterow bez klingi pominietych ").Append(_vsHeroSkip)
              .Append(" (probek ").Append(_vsHeroProbe).Append(", rozjazdow ").Append(_vsHeroDiff).Append("), rosterow bez klingi pominietych ").Append(_vsRosterSkip)
              .Append(" (probek ").Append(_vsRosterProbe).Append(", rozjazdow ").Append(_vsRosterDiff).Append(')');
            // 2
            long calls = Interlocked.Read(ref _tCalls), skip = Interlocked.Read(ref _tSkip), probe = Interlocked.Read(ref _tProbe), off = Interlocked.Read(ref _tOff);
            double usBody = Us(Interlocked.Read(ref _tBodyT), Interlocked.Read(ref _tBodyTN)), usSkip = Us(Interlocked.Read(ref _tSkipT), Interlocked.Read(ref _tSkipTN));
            double save = Interlocked.Read(ref _tSkipTN) > 0 && Interlocked.Read(ref _tBodyTN) > 0 ? Math.Max(0.0, usBody - usSkip) * skip / 1000.0 : 0.0;
            sb.Append(" | podatek miast - cialo BK pominiete (PerfTownTaxSkipBk) ").Append(OnOff(TaxSkipKey)).Append(", straznik ").Append(_taxLive ? "CZYNNY" : "STOI")
              .Append(": wywolan ").Append(calls).Append(", pominietych ").Append(skip).Append(", probek z cialem BK ").Append(probe)
              .Append(" (rada niepokryta ").Append(Interlocked.Read(ref _tProbeUncov)).Append("), przy pominieciu rada niepokryta ").Append(Interlocked.Read(ref _tUncov))
              .Append(", poza warunkiem ").Append(off).Append(", wyjatkow przy pominieciu ").Append(Interlocked.Read(ref _tExSkip)).Append(" / w probkach ").Append(Interlocked.Read(ref _tExBody))
              .Append("; czas wywolania z cialem BK ").Append(usBody.ToString("0.0", Inv)).Append(" us (probek ").Append(Interlocked.Read(ref _tBodyTN))
              .Append("), bez ").Append(usSkip.ToString("0.0", Inv)).Append(" us (probek ").Append(Interlocked.Read(ref _tSkipTN)).Append(") -> oszczednosc ok. ").Append(save.ToString("0.0", Inv)).Append(" ms");
            if (!_taxLive) sb.Append(" (straznik: ").Append(_taxWhy).Append(')');
            // 3
            double usWage = Us(_wProbeTicks, _wProbes);
            sb.Append(" | zold markietanow raz na partie (PerfSutlerWageOnce) ").Append(OnOff(SutlerWageOn)).Append(": odczytow z pamieci ").Append(_wHits)
              .Append(" (probek ").Append(_wProbes).Append(", rozjazdow ").Append(_wDiff).Append("), zold partii srednio ").Append(usWage.ToString("0.0", Inv))
              .Append(" us -> oszczednosc ok. ").Append((usWage * Math.Max(0, _wHits - _wProbes) / 1000.0).ToString("0.0", Inv)).Append(" ms");
            // 4
            sb.Append(" | dochod modelu w ksiedze rodow wedlug rodu: krolow ").Append(_mCalls[MRuler]).Append(" rodow ").Append(Ms(_mTicks[MRuler]).ToString("0.0", Inv))
              .Append(" ms, z lennami ").Append(_mCalls[MFief]).Append(" rodow ").Append(Ms(_mTicks[MFief]).ToString("0.0", Inv)).Append(" ms, bez lenn ").Append(_mCalls[MNone])
              .Append(" rodow ").Append(Ms(_mTicks[MNone]).ToString("0.0", Inv)).Append(" ms (lenn razem ").Append(_mFiefs).Append(')');
            // 5
            sb.Append(" | haki podatku miast bez refleksji (PerfTownTaxFastHooks) ").Append(OnOff(FastHooksOn)).Append(":");
            long fd = 0;
            for (int i = 0; i < FSites; i++)
            {
                long pr = Interlocked.Read(ref _fProbe[i]), d = Interlocked.Read(ref _fDiff[i]); fd += d;
                sb.Append(i == 0 ? " " : ", ").Append(FNames[i]).Append(" probek ").Append(pr).Append(" (rozjazdow ").Append(d).Append(')');
            }
            sb.Append("; rozjazdow razem ").Append(fd);
            // 3b i 5b (f4b-perf): kontrola dokladnosci - nowy segment (stare segmenty bez zmian, checker nperf czyta je jak dotad)
            sb.Append(" | kontrola dokladnosci (f4b-perf): zold markietanow tylko pewny (PerfSutlerWageExact) ").Append(OnOff(SutlerExactOn))
              .Append(": pierwszych liczen ").Append(_xFirst).Append(", z pamiecia ").Append(_xSure)
              .Append(", bez pamieci: bohater poza wodzem (zold BK losowany) ").Append(_xHero).Append(", losowanie przy liczeniu ").Append(_xDrew)
              .Append("; odczytow liczonych jak dotad ").Append(_xRaw).Append(", losowanie w probce ").Append(_xProbeDrew)
              .Append(", bez straznika ").Append(_xNoGuard).Append(", straznik losowania ").Append(_rngOk ? "CZYNNY" : (_rngTried ? "BRAK (" + _rngWhy + ")" : "nieuzyty"))
              .Append(" | probki hakow osobno na miejsce (PerfProbeEachPlace) ").Append(OnOff(ProbeEachOn)).Append(": wywolan");
            for (int i = 0; i < FSites; i++)
                sb.Append(i == 0 ? " " : ", ").Append(FNames[i]).Append(' ').Append(Interlocked.Read(ref _fCalls[i]));
            sb.Append(" | koszt linii i straznika ").Append(guardMs.ToString("0.00", Inv)).Append(" ms.");
            return sb.ToString();
        }
    }

    /// <summary>
    /// perf-d2d3 (PerfTownTaxFastHooks): skompilowane delegaty do metod i wlasciwosci BK znanych tylko z odbicia - to samo wywolanie (callvirt jak MethodInfo.Invoke,
    /// ten sam obiekt, te same argumenty), bez pakowania argumentow i sprawdzen odbicia. Typy nie pasuja albo kompilacja zawiodla - null (wolajacy wola odbiciem,
    /// jak dotad). Pamiec na proces (MethodInfo / PropertyInfo sie nie zmieniaja). Moze byc wolane z watkow AI (blokada na slowniku).
    /// </summary>
    internal static class PerfFast
    {
        private static readonly object Gate = new object();
        private static readonly Dictionary<KeyValuePair<MemberInfo, Type>, Delegate> _cache = new Dictionary<KeyValuePair<MemberInfo, Type>, Delegate>();
        private static readonly Dictionary<KeyValuePair<Type, string>, Func<object, float>> _floatProps = new Dictionary<KeyValuePair<Type, string>, Func<object, float>>();

        private static bool Has<T>(MemberInfo key, out T d) where T : class
        {
            lock (Gate)
            {
                Delegate x;
                if (_cache.TryGetValue(new KeyValuePair<MemberInfo, Type>(key, typeof(T)), out x)) { d = x as T; return true; }
            }
            d = null;
            return false;
        }

        private static T Put<T>(MemberInfo key, T d) where T : class
        {
            lock (Gate) { _cache[new KeyValuePair<MemberInfo, Type>(key, typeof(T))] = d as Delegate; }
            return d;
        }

        /// <summary>Wynik metody pasuje do TR: ten sam typ albo TR = object dla typu referencyjnego (bez pakowania).</summary>
        private static bool RetOk(Type ret, Type want) { return ret == want || (want == typeof(object) && ret != typeof(void) && !ret.IsValueType); }

        private static Expression Ret(Expression e, Type want) { return e.Type == want ? e : Expression.Convert(e, want); }

        /// <summary>Metoda instancji z jednym argumentem typu TA.</summary>
        internal static Func<object, TA, TR> M1<TA, TR>(MethodInfo m)
        {
            if (m == null) return null;
            Func<object, TA, TR> f;
            if (Has(m, out f)) return f;
            f = null;
            try
            {
                var ps = m.GetParameters();
                if (!m.IsStatic && ps.Length == 1 && ps[0].ParameterType == typeof(TA) && RetOk(m.ReturnType, typeof(TR)))
                {
                    var o = Expression.Parameter(typeof(object), "o");
                    var a = Expression.Parameter(typeof(TA), "a");
                    f = Expression.Lambda<Func<object, TA, TR>>(Ret(Expression.Call(Expression.Convert(o, m.DeclaringType), m, a), typeof(TR)), o, a).Compile();
                }
            }
            catch { f = null; }
            return Put(m, f);
        }

        /// <summary>Metoda instancji bez argumentow.</summary>
        internal static Func<object, TR> M0<TR>(MethodInfo m)
        {
            if (m == null) return null;
            Func<object, TR> f;
            if (Has(m, out f)) return f;
            f = null;
            try
            {
                if (!m.IsStatic && m.GetParameters().Length == 0 && RetOk(m.ReturnType, typeof(TR)))
                {
                    var o = Expression.Parameter(typeof(object), "o");
                    f = Expression.Lambda<Func<object, TR>>(Ret(Expression.Call(Expression.Convert(o, m.DeclaringType), m), typeof(TR)), o).Compile();
                }
            }
            catch { f = null; }
            return Put(m, f);
        }

        /// <summary>Getter wlasciwosci instancji (bez indeksu).</summary>
        internal static Func<object, TR> Getter<TR>(PropertyInfo p)
        {
            if (p == null) return null;
            Func<object, TR> f;
            if (Has(p, out f)) return f;
            f = null;
            try
            {
                var g = p.GetGetMethod(true);
                if (g != null && !g.IsStatic && p.GetIndexParameters().Length == 0 && RetOk(p.PropertyType, typeof(TR)))
                {
                    var o = Expression.Parameter(typeof(object), "o");
                    f = Expression.Lambda<Func<object, TR>>(Ret(Expression.Property(Expression.Convert(o, p.DeclaringType), p), typeof(TR)), o).Compile();
                }
            }
            catch { f = null; }
            return Put(p, f);
        }

        /// <summary>Getter wlasciwosci statycznej (typ referencyjny, jako object).</summary>
        internal static Func<object> StaticGetter(PropertyInfo p)
        {
            if (p == null) return null;
            Func<object> f;
            if (Has(p, out f)) return f;
            f = null;
            try
            {
                var g = p.GetGetMethod(true);
                if (g != null && g.IsStatic && p.GetIndexParameters().Length == 0 && RetOk(p.PropertyType, typeof(object)))
                    f = Expression.Lambda<Func<object>>(Ret(Expression.Property(null, p), typeof(object))).Compile();
            }
            catch { f = null; }
            return Put(p, f);
        }

        /// <summary>Setter wlasciwosci instancji typu TV (bez indeksu).</summary>
        internal static Action<object, TV> Setter<TV>(PropertyInfo p)
        {
            if (p == null) return null;
            Action<object, TV> f;
            if (Has(p, out f)) return f;
            f = null;
            try
            {
                var s = p.GetSetMethod(true);
                if (s != null && !s.IsStatic && p.GetIndexParameters().Length == 0 && p.PropertyType == typeof(TV))
                {
                    var o = Expression.Parameter(typeof(object), "o");
                    var v = Expression.Parameter(typeof(TV), "v");
                    f = Expression.Lambda<Action<object, TV>>(Expression.Assign(Expression.Property(Expression.Convert(o, p.DeclaringType), p), v), o, v).Compile();
                }
            }
            catch { f = null; }
            return Put(p, f);
        }

        /// <summary>To samo co Traverse.Create(o).Property(name).GetValue&lt;float&gt;() - wlasciwosc wedlug typu obiektu (AccessTools.Property, jak Traverse);
        /// wlasciwosc float z getterem - delegat, inaczej Traverse (ten sam wynik i ten sam wyjatek co dotad).</summary>
        internal static float FloatProp(object o, string name)
        {
            var t = o.GetType();
            var key = new KeyValuePair<Type, string>(t, name);
            Func<object, float> f; bool have;
            lock (Gate) { have = _floatProps.TryGetValue(key, out f); }
            if (!have)
            {
                f = null;
                try
                {
                    var p = AccessTools.Property(t, name);
                    var g = p != null ? p.GetGetMethod(true) : null;
                    if (p != null && g != null && !g.IsStatic && p.PropertyType == typeof(float) && p.GetIndexParameters().Length == 0)
                    {
                        var po = Expression.Parameter(typeof(object), "o");
                        f = Expression.Lambda<Func<object, float>>(Expression.Property(Expression.Convert(po, p.DeclaringType), p), po).Compile();
                    }
                }
                catch { f = null; }
                lock (Gate) { _floatProps[key] = f; }
            }
            return f != null ? f(o) : Traverse.Create(o).Property(name).GetValue<float>();
        }

        // zachowanie kampanii (Campaign.GetCampaignBehavior<T> - liniowe szukanie po liscie zachowan) z pamieci tej kampanii; brak (null) - zawsze szukane jak dotad
        private sealed class BehHold { public object C; public Type T; public object B; }
        private static BehHold _beh;
        private static long _behSeq;

        /// <summary>To samo co getBeh.Invoke(Campaign.Current, null) (GetCampaignBehavior&lt;t&gt;): pierwsze zachowanie typu t - z pamieci tej kampanii; co 64.
        /// wywolanie szukane jak dotad i porownane (rozjazd - pamiec odswiezona, licznik).</summary>
        internal static object Behavior(Type t, MethodInfo getBeh)
        {
            var c = Campaign.Current;
            var h = _beh;
            object b = h != null && ReferenceEquals(h.C, c) && h.T == t ? h.B : null;
            if (b != null) PerfD2.FastCall(PerfD2.FBeh);   // f4b-perf: odczyt z pamieci - licznik wywolan miejsca (tylko linia)
            bool probe = b != null && (Interlocked.Increment(ref _behSeq) & 63) == 0;
            if (b != null && !probe) return b;
            var real = getBeh.Invoke(c, null);
            if (probe) PerfD2.FastSame(PerfD2.FBeh, ReferenceEquals(real, b));
            if (real != null) _beh = new BehHold { C = c, T = t, B = real };
            return real;
        }
    }
}
