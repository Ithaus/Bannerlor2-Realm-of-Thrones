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
    /// E5/A3 (paczka 163, PLAN 5.3) - MARKIETANI (projekt etapu 5 v2, regula R9). Wylacznik SutlersEnabled; dziala tylko z sakiewka ludzi (MenPurseEnabled).
    /// Bez latek cudzego kodu - przelew wlasny w naszym ticku dobowym (pozycja ksiegi MoneyLedger.MSutlers).
    ///
    /// Dzis sakiewka ludzi schodzi "na zycie" tylko przy wyjezdzie z miasta (MenPurse.OnLeft): druzyny dlugo w polu zbieraja zold - bieg z31-A d120:
    /// sakiewki 22.5 mln = ok. 29 dni zoldu (L:31726, L:31673), przybywa ok. 100-190 tys./d. Regula (OB :171): kazdego dnia ludzie partii rodow (takze
    /// gracza - jedna regula) POZA osada wydaja u markietanow SutlerDailyShare (10%) wolnej sakiewki - po tych samych rezerwach co OnLeft: zalegle naprawy
    /// (AiWear / TroopSelfMend.OutstandingCost bez szacunku materialu - jak zaopatrzenie BK D11-4), odziez czekajaca na zakup (150, ArmyClothing.PendingCost),
    /// oszczednosci K1 juz odlozone (MenPurse - nowe odklada sie dalej tylko przy wyjezdzie z miasta). Rozdzial (MenPurse.CampPlan liczy, CampTake zdejmuje, tu odbiorcy):
    /// SutlerTownShare (85%) - kasa najblizszego miasta niebedacego w wojnie z partia (zamek nie: podzamcze dostanie swoje od B1), reszta - kiesa
    /// najblizszej takiej wsi w stanie Normal (nie rabowanej, nie spladrowanej). Brak takiego miasta - najblizsze miasto w ogole (licznik); brak takiej wsi -
    /// czesc wsi do tego samego miasta (licznik). Te same liczniki co OnLeft: "wlasne" D stalego (ClanIncomeBook.NoteOwnPaid), K3 zwrotu korony we wlasnym
    /// miescie rodu (CrownIncome.NoteOwnSpend), tarcza zoldu przy A1 wylaczonym (SoldierPay.Hold). Pomijane: partia w osadzie (wyda przy wyjezdzie),
    /// w bitwie, na morzu (NavalDLC), nieczynna albo rozwiazywana.
    /// Rozruch (flaga zapisu arm_sutlers): przy pierwszej dobie z wlaczona paczka - gdy sakiewki partii rodow maja wiecej niz 15 dni ich zoldu (stary
    /// zapis Jeffa, zapis testowy z doby 362), stawka rosnie od 1% do SutlerDailyShare w SutlerRampDays (28) dob; inaczej (nowa kampania) od razu pelna.
    /// Bez rozruchu pierwsza doba zabralaby ok. 2 mln naraz, a pierwszy tydzien ok. 10-15 mln (projekt rozdz. 3 E5/A3).
    /// Hak B1 (KL 1.3: uslugi 75% kupcy / 25% lud) - E5/B1: czesc miejska dzielona w One przy wplacie (TownFolk.EarnService KLife); kiesa nieczynna - calosc do kasy miasta.
    /// Zloto nie powstaje i nie znika: sakiewka ludzi -> kasa miasta / kiesa wsi (posiadacze "Pieniadz swiata"). Wyjatek per partia - licznik potkniec.
    /// T3-K1 (klucz SutlerClothing): przed czescia markietanow partia kupuje z polki miasta markietanow cale sztuki odziezy wojska (150) - ArmyClothing.FromSutlers;
    /// zwolniona rezerwa odziezy (PendingCost) wchodzi w czesc markietanow tej samej doby. Liczniki zakupow - linia "Odziez wojska (150)" nastepnej doby.
    /// </summary>
    internal static class Sutlers
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        internal static bool On { get { var s = Settings.Current; return s != null && s.SutlersEnabled && MenPurse.On; } }
        private static bool TributeOn { get { var s = Settings.Current; return s != null && s.VillageTributeFromSutlers; } }

        private const float RampFrom = 0.01f;           // rozruch: stawka pierwszej doby (projekt: "od 1% do 10% w 28 dob")
        private const double RampTriggerDays = 15.0;    // rozruch, gdy sakiewki partii rodow > 15 dni ich zoldu (prog etapu 5: sakiewki <= 15 dni zoldu)

        // rozruch (zapis arm_sutlers): -2 nie rozstrzygniety (stary zapis albo paczka jeszcze nie biegla), -1 bez rozruchu albo skonczony, >= 0 doba startu
        private static int _rampStart = -2;

        // liczniki doby (linie "Markietani (E5/A3)" i "Obieg")
        private static long _dTaken, _dTown, _dVill, _dOwnK3, _dReserve, _dKept, _dPlayer, _dNoTownGold, _dNoVillGold;
        private static long _dLordPurse, _dLordWage, _dLordSaved;   // sakiewki partii rodow, ich zold dnia, w tym oszczednosci K1 (odczyt)
        // E3-G2 (VillageTributeFromSutlers): czesc wsi - do kiesy, na licznik podatku wsi (pan wsi), wlasna wies rodu partii bez daniny (K3); doba
        private static long _dVillPurse, _dVillTax, _dVillOwn; private static int _dVillTaxN, _dVillOwnN;
        /// <summary>Pompy F (MoneyLedger.Daily, ta sama doba): danina pana od czesci wsi markietanow; -1 = klucz albo paczka wylaczone.</summary>
        internal static long LastVillTribute = -1;
        private static int _dParties, _dPaidN, _dInSettl, _dInBattle, _dAtSea, _dInactive, _dEmpty, _dNoTown, _dNoVill, _dTownsN, _dVillsN;
        private static float _dRate, _dFull;
        private static int _dRampDay = -1, _dRampDays;
        private static double _dMs;
        private static int _stumbles;
        private static bool _planned;   // F6: One tej partii doszlo do CampPlan - jej oszczednosci K1 sa juz w _dLordSaved (flaga jednej partii, ustawiana w petli Daily)
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static readonly HashSet<Settlement> _towns = new HashSet<Settlement>(), _vills = new HashSet<Settlement>();

        /// <summary>Linia "Obieg" (MoneyLedger.Daily, ta sama doba): markietani do kas miast i kies wsi; -1 = paczka wylaczona dzis.</summary>
        internal static long LastTown = -1, LastVill = -1;

        // ------------------------------------------------------------ N6 (noc 16.10, audyt ekonomii 15.10 rozdz. 3.1; klucz SutlersByPurse) - MARKIETANI WEDLUG SAKIEWKI
        // Dowod (log Jeffa d108945-946): sakiewki ludzi 9.66 -> 15.55 mln w 42 doby (+140 tys./d, 7% zlota swiata), sakiewki partii rodow 10.05 mln = 17.9 dni zoldu,
        // w tym oszczednosci K1 5.88 mln = 10.5 dni; "Dozbrajanie": brak lepszej na polce 27 112 koszykow/d - ludzie odkladaja na sprzet, ktorego nie ma.
        // Regula (One): stawka dnia x min(ByPurseCap 3, dni wolnej sakiewki / ByPurseDays 10) - wolna sakiewka = po naprawach, odziezy i oszczednosciach K1, w dniach
        // zoldu partii (10 dni - stawka jak dotad; 30+ - x3; 5 - x0.5). Partia bez zakupu sprzetu od IdleDays (14) dob (MenUpgrade.NoteChurn - zakupy brakow i lepszych)
        // chroni w polu najwyzej IdleKeep (0.5) limitu K1 (MenPurse.CampReserve capShare) - reszta oszczednosci jest wolna dla markietanow; limit przy wyjezdzie
        // z miasta (OnLeft) bez zmian. Odbiorcy jak dotad (kasa najblizszego miasta SutlerTownShare / kiesa wsi). Rozruch (pole "p" zapisu arm_sutlers - bez nowego
        // klucza): gdy sakiewki partii rodow > 15 dni zoldu, kwota przechodzi od starej do nowej reguly w SutlerRampDays (28) dob (bez skoku pierwszego dnia - 15.5 mln
        // w sakiewkach); klucz wylaczony - rozruch od nowa przy ponownym wlaczeniu. Doba ostatniego zakupu sprzetu - pole "g" zapisu (stary zapis: licznik 14 dob od wczytania).
        private const double ByPurseCap = 3.0, ByPurseDays = 10.0;
        private const int IdleDays = 14;
        private const float IdleKeep = 0.5f;
        private static int _pRamp = -2;                                   // N6: -2 nie rozstrzygniety, -1 bez rozruchu albo skonczony, >= 0 doba startu
        private static readonly Dictionary<string, int> _gearDay = new Dictionary<string, int>();
        private static bool _n6On; private static float _n6Ramp = 1f; private static int _n6Today, _n6RampDay, _n6RampDays;
        /// <summary>N6 (linia NE, doba): kwoty markietanow wedlug starej i nowej reguly (po rozruchu), partie, x > 1 / x < 1, srednia mnoznika, partie z nizszym limitem K1
        /// i uwolnione z nich oszczednosci.</summary>
        internal static long N6PlanOld, N6Plan, N6Released; internal static int N6Parties, N6Up, N6Down, N6Idle; internal static double N6FacSum;
        internal static bool N6On { get { return _n6On; } }
        internal static float N6Ramp { get { return _n6Ramp; } }
        internal static string N6RampText { get { return _n6RampDay > 0 ? "rozruch doba " + _n6RampDay + " z " + _n6RampDays : "bez rozruchu"; } }
        internal static int N6Tracked { get { return _gearDay.Count; } }

        /// <summary>MenUpgrade.NoteChurn (zakup sztuki przez ludzi partii): doba ostatniego zakupu sprzetu - partie rodow i gracza. Nie rzuca.</summary>
        internal static void NoteGearBuy(MobileParty mp)
        {
            try
            {
                if (mp == null || mp.StringId == null || !(mp.IsLordParty || mp.IsMainParty)) return;
                _gearDay[mp.StringId] = (int)CampaignTime.Now.ToDays;
            }
            catch { }
        }

        /// <summary>N6: czesc limitu K1 chroniona dzis w polu - IdleKeep, gdy partia od IdleDays dob nie kupila sprzetu; partia pierwszy raz widziana - licznik od dzis.</summary>
        private static float IdleShare(MobileParty mp, int today)
        {
            if (mp == null || mp.StringId == null) return 1f;
            int d;
            if (!_gearDay.TryGetValue(mp.StringId, out d)) { _gearDay[mp.StringId] = today; return 1f; }
            return today - d >= IdleDays ? IdleKeep : 1f;
        }

        /// <summary>N6: udzial nowej reguly dzis (rozruch od starej do nowej w SutlerRampDays dob; pierwsza doba z kluczem rozstrzyga rozruch).</summary>
        private static float ByPurseRamp(Settings s, int today)
        {
            int rd = Math.Max(0, s.SutlerRampDays);
            _n6RampDay = -1; _n6RampDays = 0;
            if (_pRamp == -2)
            {
                double d = Days();
                bool ramp = rd > 0 && (_dLordWage > 0 ? d > RampTriggerDays : _dLordPurse > 0);
                _pRamp = ramp ? today : -1;
                Log.Info("Markietani wedlug sakiewki (N6): rozruch - sakiewki partii rodow " + _dLordPurse + " = " + d.ToString("0.0", Inv) + " dni zoldu (zold dnia " + _dLordWage + ")"
                         + (ramp ? " > " + RampTriggerDays.ToString("0", Inv) + " dni: kwota przechodzi od starej do nowej reguly w " + rd + " dob od doby " + today + " (pole p zapisu arm_sutlers)"
                                 : " - bez rozruchu, od razu nowa regula") + ".");
            }
            if (_pRamp < 0) return 1f;
            int k = Math.Max(0, today - _pRamp);
            if (rd <= 0 || k >= rd) { _pRamp = -1; return 1f; }
            _n6RampDay = k + 1; _n6RampDays = rd;
            return (k + 1) / (float)rd;
        }

        /// <summary>Konstruktor ArmouryBehavior (nowa kampania i kazde wczytanie) - stan rozruchu przed SyncData, liczniki, potkniecia, bledy.</summary>
        internal static void Reset()
        {
            _rampStart = -2;
            _pRamp = -2; _gearDay.Clear(); _n6On = false; _n6Ramp = 1f;   // N6 (noc 16.10): stan z zapisu wczytuje Import po Reset
            LastTown = LastVill = -1; LastVillTribute = -1;
            _stumbles = 0;
            _err.Clear();
            ClearDay();
        }

        private static void ClearDay()
        {
            _dTaken = _dTown = _dVill = _dOwnK3 = _dReserve = _dKept = _dPlayer = _dNoTownGold = _dNoVillGold = 0;
            _dLordPurse = _dLordWage = _dLordSaved = 0;
            _dVillPurse = _dVillTax = _dVillOwn = 0; _dVillTaxN = _dVillOwnN = 0;   // E3-G2
            _dParties = _dPaidN = _dInSettl = _dInBattle = _dAtSea = _dInactive = _dEmpty = _dNoTown = _dNoVill = _dTownsN = _dVillsN = 0;
            _dRate = _dFull = 0f; _dRampDay = -1; _dRampDays = 0; _dMs = 0;
            _towns.Clear(); _vills.Clear();
            N6PlanOld = N6Plan = N6Released = 0; N6Parties = N6Up = N6Down = N6Idle = 0; N6FacSum = 0;   // N6
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_err.Add(where)) Log.Error("Sutlers." + where, e); } catch { }
        }

        // ------------------------------------------------------------ zapis (arm_sutlers): tylko stan rozruchu
        internal static string Export()
        {
            // N6 (noc 16.10): pola "p" (rozruch markietanow wedlug sakiewki) i "g" (doba ostatniego zakupu sprzetu partii) - stary kod czyta tylko "r" (zgodne w obie strony)
            var sb = new StringBuilder(64);
            sb.Append("r=").Append(_rampStart.ToString(Inv)).Append(";p=").Append(_pRamp.ToString(Inv));
            try
            {
                if (_gearDay.Count > 0)
                {
                    var live = new HashSet<string>();
                    foreach (var mp in MobileParty.All) if (mp != null && mp.StringId != null) live.Add(mp.StringId);
                    bool first = true;
                    var bad = new[] { ';', '=', ',', ':' };
                    foreach (var kv in _gearDay)
                    {
                        if (!live.Contains(kv.Key) || kv.Key.IndexOfAny(bad) >= 0) continue;
                        sb.Append(first ? ";g=" : ",").Append(kv.Key).Append(':').Append(kv.Value.ToString(Inv));
                        first = false;
                    }
                }
            }
            catch (Exception e) { Stumble("Export(N6)", e); }
            return sb.ToString();
        }

        internal static void Import(string s)
        {
            _rampStart = -2;
            if (string.IsNullOrEmpty(s)) return;
            foreach (var p in s.Split(';'))
            {
                var a = p.Split('='); int v;
                if (a.Length == 2 && a[0] == "r" && int.TryParse(a[1], NumberStyles.Integer, Inv, out v) && v >= -2) _rampStart = v;
                else if (a.Length == 2 && a[0] == "p" && int.TryParse(a[1], NumberStyles.Integer, Inv, out v) && v >= -2) _pRamp = v;   // N6
                else if (a.Length == 2 && a[0] == "g")                                                                                    // N6
                    foreach (var e in a[1].Split(','))
                    {
                        int c = e.LastIndexOf(':');
                        if (c > 0 && int.TryParse(e.Substring(c + 1), NumberStyles.Integer, Inv, out v)) _gearDay[e.Substring(0, c)] = v;
                    }
            }
        }

        // ------------------------------------------------------------ raz na dobe (ArmouryBehavior: po IronBank i migawce MRest, przed ClanBudget.Daily)
        internal static void Daily()
        {
            LastTown = LastVill = -1; LastVillTribute = -1;
            ClearDay();
            var s = Settings.Current;
            if (Campaign.Current == null || s == null) return;
            int today = (int)CampaignTime.Now.ToDays;
            var sw = Stopwatch.StartNew();
            var parties = new List<MobileParty>(800);
            try
            {
                foreach (var mp in MobileParty.AllLordParties) if (mp != null && !mp.IsMainParty) parties.Add(mp);
                var main = MobileParty.MainParty;
                if (main != null) parties.Add(main);   // gracz - jedna regula (tez wtedy, gdy gra nie liczy jego partii do AllLordParties)
                foreach (var mp in parties)
                {
                    // F6 (test zbiorczy 5-8, A3-7): tu sama suma sakiewek; oszczednosci K1 do linii daje One (kept z CampPlan) albo ReadSaved (partie pominiete)
                    try { _dLordPurse += Math.Max(0, MenPurse.Get(mp)); }
                    catch (Exception e) { Stumble("Daily.Read", e); }
                }
                _dLordWage = SoldierPay.LastLordAcc;   // zold partii rodow naliczony tej doby (SoldierPay.Daily biegnie przed nami w bloku)
                if (_dLordWage <= 0) foreach (var mp in parties) { try { if (mp.IsActive) _dLordWage += Math.Max(0, mp.TotalWage); } catch { } }
            }
            catch (Exception e) { Stumble("Daily.Parties", e); }
            if (!On)
            {
                foreach (var mp in parties) ReadSaved(s, mp);   // F6: paczka wylaczona - One nie biegnie, pelna petla odczytu jak dotad
                sw.Stop();
                if (s.LogEnabled)
                    Log.Info("Markietani (E5/A3): dzien " + today + " | wylaczone (Sutlers Enabled / Men Purse Enabled) - sakiewki wydaja tylko przy wyjezdzie z miasta, jak dotad"
                             + " | sakiewki partii rodow " + _dLordPurse + " = " + Days().ToString("0.0", Inv) + " dni zoldu (zold dnia " + _dLordWage + SavedNote() + ").");
                return;
            }
            float rate = 0f;
            try { rate = RateToday(s, today); }
            catch (Exception e) { Stumble("RateToday", e); rate = 0f; }
            _dRate = rate;
            // N6 (SutlersByPurse): udzial nowej reguly dzis (rozruch); klucz wylaczony - rozruch od nowa przy ponownym wlaczeniu, kwota jak dotad
            _n6Today = today; _n6On = false; _n6Ramp = 1f;
            if (s.SutlersByPurse)
            {
                try { _n6Ramp = ByPurseRamp(s, today); _n6On = true; }
                catch (Exception e) { Stumble("ByPurseRamp", e); _n6On = false; }
            }
            else _pRamp = -2;
            float townShare = MBMath.ClampFloat(s.SutlerTownShare, 0f, 1f);
            if (rate > 0f)
                foreach (var mp in parties)
                {
                    _planned = false;
                    try { One(mp, rate, townShare); }
                    catch (Exception e) { Stumble("One", e); }   // wyjatek jednej partii - licznik, reszta biegnie dalej (bez globalnego wylacznika)
                    if (!_planned) ReadSaved(s, mp);             // F6: One nie doszlo do CampPlan (partia pominieta albo wyjatek przed rezerwa) - odczyt jak dotad
                }
            else
                foreach (var mp in parties) ReadSaved(s, mp);    // F6: stawka 0 (Sutler Daily Share 0 albo wyjatek RateToday) - One nie biegnie, pelna petla odczytu jak dotad
            _dTownsN = _towns.Count; _dVillsN = _vills.Count;
            LastTown = _dTown; LastVill = _dVill;
            LastVillTribute = TributeOn ? _dVillTax : -1;                 // E3-G2: Pompy F
            sw.Stop();
            _dMs = sw.Elapsed.TotalMilliseconds;
            try { if (s.LogEnabled) Log.Info(Line(today)); }
            catch (Exception e) { Stumble("Line", e); }
        }

        private static double Days() { return _dLordWage > 0 ? (double)_dLordPurse / _dLordWage : 0.0; }

        /// <summary>F6 (test zbiorczy 5-8, A3-7): oszczednosci K1 jednej partii do linii - sam odczyt, wedlug tej samej reguly co markietani (MenPurse.KeptFor -
        /// limit OnLeft od zoldu TERAZ; recenzja E5/A3 1-1). KeptFor liczy rezerwe napraw (dopasowanie AiWear - drogie), wiec tylko dla partii, ktorych One nie
        /// policzylo: pominietych przed CampPlan (nieczynne, w osadzie, w bitwie, na morzu, bez miasta) i dla calej listy, gdy markietani nie biegna
        /// (paczka wylaczona, stawka 0). Partie w polu daja te sama liczbe z CampPlan w One. Sakiewka pusta - nic (jak dotad).</summary>
        private static void ReadSaved(Settings s, MobileParty mp)
        {
            if (!s.LogEnabled) return;
            try { int pu = Math.Max(0, MenPurse.Get(mp)); if (pu > 0) _dLordSaved += Math.Min(pu, Math.Max(0, MenPurse.KeptFor(mp))); }
            catch (Exception e) { Stumble("Daily.Read", e); }
        }

        /// <summary>Odczyt: ile sakiewek partii rodow to oszczednosci K1 (markietani ich nie ruszaja - rezerwa jak w OnLeft; limit MenGearSaveDays dni zoldu).</summary>
        private static string SavedNote()
        {
            double d = _dLordWage > 0 ? (double)_dLordSaved / _dLordWage : 0.0;
            return "; w tym oszczednosci K1 " + _dLordSaved.ToString(Inv) + " = " + d.ToString("0.0", Inv) + " dni";
        }

        /// <summary>Stawka dnia z rozruchem; pierwsza doba z wlaczona paczka rozstrzyga rozruch (sakiewki partii rodow wobec ich zoldu dnia).</summary>
        private static float RateToday(Settings s, int today)
        {
            float full = MBMath.ClampFloat(s.SutlerDailyShare, 0f, 1f);
            _dFull = full;
            int rd = Math.Max(0, s.SutlerRampDays);
            if (_rampStart == -2)
            {
                double d = Days();
                bool ramp = rd > 0 && full > RampFrom && (_dLordWage > 0 ? d > RampTriggerDays : _dLordPurse > 0);
                _rampStart = ramp ? today : -1;
                Log.Info("Markietani (E5/A3): rozruch - sakiewki partii rodow " + _dLordPurse + " = " + d.ToString("0.0", Inv) + " dni zoldu (zold dnia " + _dLordWage + ")"
                         + (ramp ? " > " + RampTriggerDays.ToString("0", Inv) + " dni: stawka od " + (RampFrom * 100f).ToString("0", Inv) + "% do " + (full * 100f).ToString("0.0", Inv)
                                   + "% w " + rd + " dob od doby " + today + " (flaga zapisu arm_sutlers)"
                                 : (rd <= 0 ? " - rozruch wylaczony (Sutler Ramp Days 0): od razu pelna stawka" : " <= " + RampTriggerDays.ToString("0", Inv) + " dni: bez rozruchu, od razu pelna stawka"))
                         + ".");
            }
            if (_rampStart < 0) return full;
            int k = Math.Max(0, today - _rampStart);
            if (rd <= 0 || k >= rd) { _rampStart = -1; return full; }
            _dRampDay = k + 1; _dRampDays = rd;
            return Math.Min(full, RampFrom + (full - RampFrom) * k / rd);
        }

        /// <summary>Jedna partia: warunki, kwota (MenPurse.CampPlan - rezerwy jak OnLeft), odbiorcy, zdjecie z sakiewki (CampTake), przelew i liczniki.
        /// Recenzja E5/A3 2-2: odbiorcy (miasto, wies) wyznaczeni PRZED zdjeciem, a od zdjecia do obu wplat nie biegnie nic, co moze rzucic - wyjatek
        /// (potkniecie) nie gubi zlota, ktore juz zeszlo z sakiewki.</summary>
        private static void One(MobileParty mp, float rate, float townShare)
        {
            if (!mp.IsActive || mp.IsDisbanding) { _dInactive++; return; }
            if (MenPurse.Get(mp) <= 0) { _dEmpty++; return; }
            if (mp.CurrentSettlement != null) { _dInSettl++; return; }   // w osadzie - wyda przy wyjezdzie z miasta (OnLeft)
            if (mp.MapEvent != null) { _dInBattle++; return; }
            if (mp.IsCurrentlyAtSea) { _dAtSea++; return; }               // NavalDLC: na statku nie ma markietanow - wyda w porcie
            var p = mp.GetPosition2D;
            var f = mp.MapFaction;
            bool noTown = false;
            var town = NearestTown(p, f);
            if (town == null) { town = NearestTown(p, null); noTown = town != null; }
            if (town == null || town.Town == null) { _dEmpty++; return; }   // swiat bez miast - nic nie schodzi
            // T3-K1 (SutlerClothing): markietani przywoza odziez z miasta, do ktorego wplacaja - PRZED ich czescia (jak w OnLeft: odziez przed "zyciem"),
            // z sakiewki ponad zalegle naprawy; naprawy liczone raz (F6) i podane do CampPlan. Klucz wylaczony - mend -1, CampPlan liczy je jak dotad
            long mend = -1L;
            if (ArmyClothing.SutlerOn)
            {
                mend = MenPurse.CampMend(mp);
                int cloth = ArmyClothing.FromSutlers(mp, town, !noTown, mend);   // wlasny try; zloto juz w kasie miasta
                if (cloth > 0) MenPurse.NoteCampCloth(cloth);
            }
            int reserve, kept;
            int want;
            if (_n6On)
            {
                // N6: kwota z wolnej sakiewki wedlug jej zasobnosci (dni zoldu) i z nizszym limitem K1 partii bez zakupu sprzetu od 14 dob; rozruch miesza stara
                // i nowa kwote. Naprawy liczone raz (jak T3-K1)
                if (mend < 0) mend = MenPurse.CampMend(mp);
                int keptOld, res2;
                int freeOld = MenPurse.CampFree(mp, mend, 1f, out reserve, out keptOld);
                float cs = IdleShare(mp, _n6Today);
                int freeNew = freeOld; kept = keptOld;
                if (cs < 1f) { freeNew = MenPurse.CampFree(mp, mend, cs, out res2, out kept); N6Idle++; N6Released += Math.Max(0, keptOld - kept); }
                int wantOld = Math.Max(0, (int)(freeOld * (double)Math.Min(1f, rate)));
                double fac = Math.Min(ByPurseCap, freeNew / (double)Math.Max(1, MenPurse.WageOf(mp)) / ByPurseDays);
                int wantNew = Math.Max(0, (int)(freeNew * Math.Min(1.0, rate * fac)));
                want = (int)Math.Round(wantOld + (wantNew - (double)wantOld) * _n6Ramp);
                want = Math.Max(0, Math.Min(want, freeNew));
                N6Parties++; N6FacSum += fac; if (fac > 1.0) N6Up++; else if (fac < 1.0) N6Down++;
                N6PlanOld += wantOld; N6Plan += want;
            }
            else want = MenPurse.CampPlan(mp, rate, mend, out reserve, out kept);
            // F6: kept z CampPlan to ta sama liczba, ktora dawal odczyt MenPurse.KeptFor (ta sama CampReserve na tej samej sakiewce; 0 <= kept <= sakiewka) -
            // rezerwa napraw (AiWear.OutstandingCost) liczona raz na partie w polu, nie dwa
            _planned = true; _dLordSaved += kept;
            _dParties++; _dReserve += reserve; _dKept += kept;
            if (want <= 0) return;
            // lup miast L3: markietani nie kieruja pieniadza do lupionej osady - wstrzymana tylko czesc miejska (zostaje w sakiewce; wlasny try), czesc wsi jak zwykle
            bool sackHold = CitySack.NoSpend(town, mp, (long)(want * (double)townShare), true);
            if (sackHold) { want = (int)(want * (double)(1f - townShare)); townShare = 0f; if (want <= 0) return; }
            bool wantVill = (int)(want * (double)(1f - townShare)) > 0;
            var vill = wantVill ? NearestVillage(p, f) : null;              // wies szukana tylko, gdy jest co jej dac (koszt: ok. 570 wsi na partie)
            if (vill != null && vill.SettlementComponent == null) vill = null;
            if (sackHold && vill == null) return;                            // bez wsi cala kwota szlaby do lupionej osady - zostaje w sakiewce
            // ---- od zdjecia do obu wplat tylko arytmetyka i ChangeGold
            int x = MenPurse.CampTake(mp, want);
            if (x <= 0) return;
            int villPart = (int)(x * (double)(1f - townShare));
            if (vill == null) villPart = 0;
            int townPart = x - villPart;
            int folk = townPart > 0 ? TownFolk.EarnService(town, townPart, TownFolk.KLife) : 0;   // E5/B1 (R9, 75/25): arytmetyka - nie rzuca (recenzja A3 2-2 zachowana)
            if (townPart > 0) town.Town.ChangeGold(townPart - folk);
            if (villPart > 0)
            {
                // E3-G2 (VillageTributeFromSutlers): ta sama regula co utarg taboru 112 - podatek pana z czynnego modelu na licznik podatku wsi, reszta do kiesy
                // (VillageTakings.Split: arytmetyka, model w try - nie rzuca). Wies rodu partii - bez daniny (K3: zold zwraca korona, nie wraca do wlasnej kiesy rodu)
                var vv = vill.Village;
                if (TributeOn && vv != null && !ReferenceEquals(vill.OwnerClan, mp.ActualClan))
                {
                    int vtax, vpurse;
                    VillageTakings.Split(vv, villPart, villPart, out vtax, out vpurse);
                    _dVillTax += vtax; _dVillPurse += vpurse; if (vtax > 0) _dVillTaxN++;
                    if (vpurse > 0) DsVillage.WarShareSutlers(vv, vpurse);   // DS (F1): udzial korony g/10 od czesci wsi w kiesie (wlasny try; wies spoza DS - nic)
                }
                else
                {
                    vill.SettlementComponent.ChangeGold(villPart);    // kiesa wsi (posiadacz "Pieniadz swiata")
                    _dVillPurse += villPart;
                    if (TributeOn && vv != null) { _dVillOwn += villPart; _dVillOwnN++; }
                    if (vv != null) DsVillage.WarShareSutlers(vv, villPart);   // DS (F1): udzial korony g/10 od czesci wsi w kiesie (wlasny try)
                }
            }
            // ---- liczniki (zloto juz na miejscu)
            _dTaken += x; _dPaidN++;
            if (wantVill && vill == null) { int vp = (int)(x * (double)(1f - townShare)); if (vp > 0) { _dNoVill++; _dNoVillGold += vp; } }
            if (noTown) { _dNoTown++; _dNoTownGold += x; }
            if (mp.IsMainParty) _dPlayer += x;
            Pay(mp, town, vill, townPart, villPart);
            CitySack.NoteSpend(town, mp, townPart);   // lup miast (L0): markietani do osady po nastepstwach oblezenia (tylko licznik, wlasny try)
        }

        /// <summary>Liczniki przelewu sakiewka -> kasa miasta / kiesa wsi (zloto juz zdjete z sakiewki w CampTake i wplacone w One) - te same co MenPurse.OnLeft.
        /// E5/B1: czesc miejska dzielona 75% kupcy / 25% lud (TownFolk.EarnService) przy wplacie w One; liczniki K3 i 169c tu od calej czesci miejskiej (jak OnLeft, KL 5.2).</summary>
        private static void Pay(MobileParty mp, Settlement town, Settlement vill, int townPart, int villPart)
        {
            if (townPart > 0)
            {
                _dTown += townPart; _towns.Add(town);
                var clan = mp.ActualClan;
                ClanIncomeBook.NoteOwnPaid(town, clan, townPart);                  // 169c: "wlasne" D stalego - wydatki ludzi rodu w jego miescie (tylko licznik)
                if (clan != null && ReferenceEquals(town.OwnerClan, clan))
                {
                    CrownIncome.NoteOwnSpend(clan, townPart);                      // 165 (K3): wydane we wlasnym miescie rodu wraca zaworem - bez zwrotu korony od tego (jak OnLeft)
                    _dOwnK3 += townPart;
                }
                SoldierPay.Hold(town, townPart);                                   // tarcza zoldu (gdy wlaczona, A1 wylaczone) - jak "zycie" przy wyjezdzie
                try { TownFolk.Note(town, townPart, TownFolk.KLife); } catch { }   // E5/0 (195, scalenie porcji A): markietani w kasie miasta - "zycie" jak MenPurse.OnLeft (tylko licznik)
            }
            if (villPart > 0)
            {
                _dVill += villPart; _vills.Add(vill);
            }
        }

        /// <summary>Najblizsze miasto; faction != null - tylko niebedace w wojnie z nia (wojna sprawdzana dopiero dla blizszego niz dotad najlepsze).</summary>
        private static Settlement NearestTown(Vec2 p, IFaction faction)
        {
            Settlement best = null; float bd = float.MaxValue;
            foreach (var t in Town.AllTowns)
            {
                var st = t != null ? t.Settlement : null;
                if (st == null || !st.IsTown) continue;
                float d = p.DistanceSquared(st.GetPosition2D);
                if (d >= bd) continue;
                if (faction != null && st.MapFaction != null && FactionManager.IsAtWarAgainstFaction(faction, st.MapFaction)) continue;
                bd = d; best = st;
            }
            return best;
        }

        /// <summary>Najblizsza wies w stanie Normal niebedaca w wojnie z partia.</summary>
        private static Settlement NearestVillage(Vec2 p, IFaction faction)
        {
            Settlement best = null; float bd = float.MaxValue;
            foreach (var v in Village.All)
            {
                var st = v != null ? v.Settlement : null;
                if (st == null || v.VillageState != Village.VillageStates.Normal) continue;
                float d = p.DistanceSquared(st.GetPosition2D);
                if (d >= bd) continue;
                if (faction != null && st.MapFaction != null && FactionManager.IsAtWarAgainstFaction(faction, st.MapFaction)) continue;
                bd = d; best = st;
            }
            return best;
        }

        private static string Line(int today)
        {
            var sb = new StringBuilder(1024);
            double ta = _dTaken > 0 ? 100.0 * _dTown / _dTaken : 0.0;
            sb.Append("Markietani (E5/A3): dzien ").Append(today)
              .Append(" | stawka ").Append((_dRate * 100f).ToString("0.0", Inv)).Append("% (pelna ").Append((_dFull * 100f).ToString("0.0", Inv)).Append("%; ")
              .Append(_dRampDay > 0 ? "rozruch: doba " + _dRampDay + " z " + _dRampDays : "bez rozruchu").Append(')')
              .Append(" | w polu ").Append(_dParties).Append(" partii, wydaly ").Append(_dPaidN).Append(": z sakiewek ").Append(_dTaken).Append(" (gracz ").Append(_dPlayer).Append(')')
              .Append(" -> kasy miast ").Append(_dTown).Append(" w ").Append(_dTownsN).Append(" miastach (").Append(ta.ToString("0.0", Inv)).Append("%), kiesy wsi ").Append(_dVill)
              .Append(" w ").Append(_dVillsN).Append(" wsiach")
              .Append(" | E3-G2 danina pana od czesci wsi (Village Tribute From Sutlers ").Append(TributeOn ? "tak" : "nie").Append("): do kies wsi ").Append(_dVillPurse)
              .Append(", na liczniki podatku wsi (pan wsi) ").Append(_dVillTax).Append(" (").Append(_dVillTaxN).Append(" wplat), wlasne wsie rodu partii bez daniny (K3) ").Append(_dVillOwn)
              .Append(" (").Append(_dVillOwnN).Append("); kontrola: zaplacone wsiom ").Append(_dVill).Append(" = kiesa + licznik ").Append(_dVillPurse + _dVillTax)
              .Append(" (roznica ").Append(_dVill - _dVillPurse - _dVillTax).Append(')')
              .Append(" | we wlasnych miastach rodow (K3) ").Append(_dOwnK3)
              .Append(" | bez niewrogiego miasta -> najblizsze miasto ").Append(_dNoTown).Append(" partii (").Append(_dNoTownGold).Append(')')
              .Append(" | bez niewrogiej wsi w stanie Normal -> miasto ").Append(_dNoVill).Append(" partii (").Append(_dNoVillGold).Append(')')
              .Append(" | rezerwy: naprawy i odziez ").Append(_dReserve).Append(", oszczednosci K1 ").Append(_dKept)
              .Append(" | pominiete: w osadzie ").Append(_dInSettl).Append(", w bitwie ").Append(_dInBattle).Append(", na morzu ").Append(_dAtSea)
              .Append(", nieczynne ").Append(_dInactive).Append(", puste sakiewki ").Append(_dEmpty)
              .Append(" | sakiewki partii rodow ").Append(_dLordPurse).Append(" = ").Append(Days().ToString("0.0", Inv)).Append(" dni zoldu (zold dnia ").Append(_dLordWage).Append(SavedNote()).Append(')')
              .Append(" | koszt ").Append(_dMs.ToString("0.0", Inv)).Append(" ms");
            if (_stumbles > 0) sb.Append(" | potkniecia ").Append(_stumbles).Append(" (pierwszy wyjatek z miejsca w logu jako ERROR)");
            sb.Append('.');
            return sb.ToString();
        }

        /// <summary>Dopisek linii "Obieg" (sakiewki ludzi): markietani tej doby.</summary>
        internal static string ObiegNote()
        {
            if (LastTown < 0) return "- (wylaczone)";
            return (LastTown + LastVill).ToString(Inv) + " (E5/A3: do kas miast " + LastTown.ToString(Inv) + ", do kies wsi " + LastVill.ToString(Inv) + ")";
        }
    }
}
