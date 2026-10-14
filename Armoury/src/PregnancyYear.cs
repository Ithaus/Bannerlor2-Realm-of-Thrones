using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameComponents;

namespace Armoury
{
    /// <summary>
    /// CR (zasada Jeffa 14.10: "1 rok w grze to 1 rok starzenia, 1 dzien to 1 dzien"; rozpoznanie docs/pytania-2026-10-14/STARZENIE-I-CZAS-ZYCIA.md):
    /// przy roku 364 dob (LongYearTimeModel) wiek juz rosnie o 1 co 364 doby, ale ciaza trwa 36 dob, a szansa poczecia jest "na dobe" dobrana pod
    /// rok gry 84 doby - dzieci na rok 4,33 x za duzo (symulacja wzoru gry: 1. rok malzenstwa 1,9-3,7 dziecka, gra przewidziala 0,7-1,2).
    ///
    /// Gra 1.4.8 (dekompilacja dec32):
    ///  - termin porodu: PregnancyCampaignBehavior.ChildConceived (sluchacz OnChildConceived) zapisuje Pregnancy(matka, ojciec,
    ///    CampaignTime.DaysFromNow(PregnancyModel.PregnancyDurationInDays)) - termin jest w zapisie gry; porod przy DailyTickHero ciezarnej, gdy
    ///    DueDate nie jest juz w przyszlosci (CheckOffspringToDeliver, potem zdarzenie OnGivenBirth);
    ///  - poczecie: DailyTickHero kobiety (wiek > 18, zywy malzonek, nie w ciazy): malzonek blisko (to samo miejsce albo 20% losu AI) i
    ///    RandomFloat <= PregnancyModel.GetDailyChanceOfPregnancyForHero -> MakePregnantAction.Apply; BK drudzy malzonkowie
    ///    (BKMarriageBehavior.RefreshSpouseVisit, PrimarySpouse) woluja TEN SAM model - jedna latka obejmuje obie drogi;
    ///  - DefaultPregnancyModel: PregnancyDurationInDays 36 (18 w trybie Fast), szansa (1,2 - 0,04 x (wiek - 18)) / (dzieci + 1)^2 x 0,12 x rod.
    /// Czynny model to BKROTPregnancyModel (BKROTPatch, AddModel w OnGameStart): dziedziczy po DefaultPregnancyModel, czasu ciazy NIE nadpisuje,
    /// szanse nadpisuje, ale wola metode gry niewirtualnie (IL: call DefaultPregnancyModel::GetDailyChanceOfPregnancyForHero - sprawdzone 14.10)
    /// i mnozy wynik x1,25 za kazde z blogoslawienstw Mother / Pantera / GodsLove. BK ma postfiks na metodzie gry (x1,15 za doktryne Childbirth
    /// u matki i u malzonka). Nasz postfiks (Priority.Last) mnozy wynik metody gry RAZ - mnozniki BK i BKROT dzialaja dalej tak samo.
    ///
    /// Latki (wpinane w menu glownym, klucze czytane przy kazdym wywolaniu):
    ///  1) postfiks gettera DefaultPregnancyModel.PregnancyDurationInDays: wynik = PregnancyDays (0 = gra). PRZYPISANIE. Termin liczy gra przy
    ///     poczeciu - ciaze juz trwajace w zapisie koncza sie wedlug starego terminu (po zmianie klucza w trakcie gry tak samo w druga strone).
    ///     Ten sam getter czytaja wpisy dziennika gry (PregnancyLogEntry / ChildbirthLogEntry: jak dlugo wpis zostaje w historii) - bez wplywu na gre;
    ///  2) postfiks DefaultPregnancyModel.GetDailyChanceOfPregnancyForHero: wynik x 84 / CampaignTime.DaysInYear (rok kalendarza z modelu czasu;
    ///     364 -> 0,2308), tylko gdy rok jest dluzszy niz 84 doby - krotszy rok (tryb Fast, mniej tygodni) zostaje przy szansie gry (x1).
    /// Sluby AI bez zmian (decyzja Jeffa 13.10: MarriageChanceScale CrashScribe 0.7).
    ///
    /// Log: linia wpiecia (menu glowne), "start sesji" (klucze, rok i mnoznik, czynny model, czas ciazy czynnego modelu, ciaze w toku) i linia
    /// doby: ciaze w toku, zamezne 18-45, poczecia i porody (zdarzenia gry OnChildConceived / OnGivenBirth), rzuty szansy z suma szans (po
    /// mnozniku i gry), czas ciazy zmierzony z terminu zapisanego przez gre dla poczec tej sesji i doby od poczecia do porodu. Bez zapisu.
    /// </summary>
    internal static class PregnancyYear
    {
        internal const double GameYear = 84.0;     // rok gry (7 x 3 x 4), pod ktory dobrana jest szansa poczecia na dobe
        internal const int MaxDays = 364;          // najdluzszy czas ciazy z klucza (przyciecie)
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly object Gate = new object();

        private static bool _applied, _durHooked, _chanceHooked;
        private static int _errors;
        private static FieldInfo _fList, _fMother, _fDue;

        // od poprzedniej linii doby
        private static int _rolls, _conceived, _births, _alive, _still;
        private static double _sumNow, _sumGame;
        // od startu sesji
        private static int _sConceived, _sBirths;
        // poczecia tej sesji: matka -> doba poczecia. _pending czeka na odczyt terminu gry (gra dopisuje ciaze PO naszym sluchaczu - MbEvent wola
        // od ostatnio dopisanego), _carry - termin odczytany, czeka na porod
        private static readonly Dictionary<Hero, double> _pending = new Dictionary<Hero, double>();
        private static readonly Dictionary<Hero, double> _carry = new Dictionary<Hero, double>();
        private static int _durN, _bornN;
        private static double _durMin, _durMax, _bornMin, _bornMax;

        /// <summary>Czas ciazy z klucza (0 = gra); przyciety do 0..364.</summary>
        internal static int Days { get { var s = Settings.Current; if (s == null) return 0; int d = s.PregnancyDays; return d < 0 ? 0 : (d > MaxDays ? MaxDays : d); } }
        internal static bool ScaleOn { get { var s = Settings.Current; return s != null && s.FertilityYearScale; } }

        /// <summary>84 / rok kalendarza (CampaignTime.DaysInYear - z modelu czasu); 1, gdy rok nie jest dluzszy niz 84 doby albo jeszcze nieznany.</summary>
        internal static double YearFactor()
        {
            try { int y = CampaignTime.DaysInYear; return y > GameYear ? GameYear / y : 1.0; }
            catch { return 1.0; }
        }

        internal static double ChanceFactor() { return ScaleOn ? YearFactor() : 1.0; }

        private static void Stumble(string where, Exception e)
        {
            _errors++;
            if (_errors <= 5 || _errors % 100 == 0) Log.Error("PregnancyYear." + where + " (potkniecie " + _errors + ")", e);
        }

        private static double Clamp01(double p) { return p < 0 ? 0 : (p > 1 ? 1 : p); }

        // ------------------------------------------------------------ latki
        /// <summary>(1) Postfiks gettera czasu ciazy: PregnancyDays (0 = wynik gry).</summary>
        public static void DurationPostfix(ref float __result)
        {
            int d = Days;
            if (d > 0) __result = d;
        }

        /// <summary>(2) Postfiks szansy poczecia na dobe: x 84 / rok (FertilityYearScale); sumy szans do linii doby (szansa jako
        /// prawdopodobienstwo rzutu - przycieta do 0..1).</summary>
        public static void ChancePostfix(ref float __result)
        {
            try
            {
                float game = __result;
                double f = ChanceFactor();
                if (f < 1.0) __result = (float)(game * f);
                lock (Gate) { _rolls++; _sumGame += Clamp01(game); _sumNow += Clamp01(__result); }
            }
            catch (Exception e) { Stumble("ChancePostfix", e); }
        }

        /// <summary>Wpiecie w menu glownym (SubModuleMain.OnBeforeInitialModuleScreenSetAsRoot), raz na proces. Latki zawsze; klucze na zywo.</summary>
        internal static void ApplyAll(Harmony harmony)
        {
            if (_applied) return;
            _applied = true;
            try
            {
                var g = AccessTools.DeclaredPropertyGetter(typeof(DefaultPregnancyModel), "PregnancyDurationInDays");
                if (g != null)
                {
                    harmony.Patch(g, postfix: new HarmonyMethod(typeof(PregnancyYear), nameof(DurationPostfix)) { priority = Priority.Last });
                    _durHooked = true;
                }
            }
            catch (Exception e) { Log.Error("PregnancyYear.Patch(PregnancyDurationInDays)", e); }
            try
            {
                var m = AccessTools.DeclaredMethod(typeof(DefaultPregnancyModel), "GetDailyChanceOfPregnancyForHero", new[] { typeof(Hero) });
                if (m != null)
                {
                    harmony.Patch(m, postfix: new HarmonyMethod(typeof(PregnancyYear), nameof(ChancePostfix)) { priority = Priority.Last });
                    _chanceHooked = true;
                }
            }
            catch (Exception e) { Log.Error("PregnancyYear.Patch(GetDailyChanceOfPregnancyForHero)", e); }
            try
            {
                var tB = typeof(PregnancyCampaignBehavior);
                var tP = AccessTools.Inner(tB, "Pregnancy");
                _fList = AccessTools.Field(tB, "_heroPregnancies");
                _fMother = tP != null ? AccessTools.Field(tP, "Mother") : null;
                _fDue = tP != null ? AccessTools.Field(tP, "DueDate") : null;
            }
            catch (Exception e) { Log.Error("PregnancyYear.ApplyAll (pola listy ciaz)", e); }
            Log.Info("Ciaza i plodnosc (CR): wpiete - getter czasu ciazy " + (_durHooked ? "tak" : "NIE") + ", szansa poczecia " + (_chanceHooked ? "tak" : "NIE")
                     + ", lista ciaz gry " + (_fList != null && _fMother != null && _fDue != null ? "tak" : "NIE (bez pomiaru terminow)"));
        }

        /// <summary>Nasluchy zdarzen gry (ArmouryBehavior.RegisterEvents): poczecie i porod - same liczniki.</summary>
        internal static void RegisterEvents(CampaignBehaviorBase owner)
        {
            CampaignEvents.OnChildConceivedEvent.AddNonSerializedListener(owner, OnConceived);
            CampaignEvents.OnGivenBirthEvent.AddNonSerializedListener(owner, OnBirth);
        }

        private static void OnConceived(Hero mother)
        {
            try
            {
                _conceived++; _sConceived++;
                if (mother == null) return;
                _carry.Remove(mother);
                _pending[mother] = CampaignTime.Now.ToDays;
            }
            catch (Exception e) { Stumble("OnConceived", e); }
        }

        private static void OnBirth(Hero mother, List<Hero> aliveChildren, int stillborn)
        {
            try
            {
                _births++; _sBirths++;
                _alive += aliveChildren != null ? aliveChildren.Count : 0;
                _still += stillborn;
                if (mother == null) return;
                double t0;
                if (_carry.TryGetValue(mother, out t0) || _pending.TryGetValue(mother, out t0))
                {
                    double d = CampaignTime.Now.ToDays - t0;
                    if (_bornN == 0 || d < _bornMin) _bornMin = d;
                    if (_bornN == 0 || d > _bornMax) _bornMax = d;
                    _bornN++;
                }
                _carry.Remove(mother);
                _pending.Remove(mother);
            }
            catch (Exception e) { Stumble("OnBirth", e); }
        }

        /// <summary>Lista ciaz gry (PregnancyCampaignBehavior._heroPregnancies) albo null.</summary>
        private static IList PregList()
        {
            try
            {
                var c = Campaign.Current;
                var beh = c != null ? c.GetCampaignBehavior<PregnancyCampaignBehavior>() : null;
                return beh != null && _fList != null ? _fList.GetValue(beh) as IList : null;
            }
            catch { return null; }
        }

        /// <summary>Zamezne w wieku rodzenia (18-45 lat, zywy malzonek, poza rodem buntownikow) - ile i ile z nich w ciazy.</summary>
        private static void Wives(out int wives, out int pregnant)
        {
            wives = 0; pregnant = 0;
            foreach (var h in Hero.AllAliveHeroes)
            {
                if (h == null || !h.IsFemale || h.Spouse == null || !h.Spouse.IsAlive) continue;
                float a = h.Age;
                if (a < 18f || a > 45f) continue;
                if (h.Clan != null && h.Clan.IsRebelClan) continue;
                wives++;
                if (h.IsPregnant) pregnant++;
            }
        }

        /// <summary>Ciaze w toku: liczba, najblizszy i najdalszy termin (doby od teraz), ile z terminem dalej niz gra (36 dob).</summary>
        private static int InProgress(IList list, out double minLeft, out double maxLeft, out int beyondGame)
        {
            minLeft = 0; maxLeft = 0; beyondGame = 0;
            if (list == null) return -1;
            double now = CampaignTime.Now.ToDays;
            int n = 0;
            foreach (var p in list)
            {
                if (p == null || _fDue == null) continue;
                double left = ((CampaignTime)_fDue.GetValue(p)).ToDays - now;
                if (n == 0 || left < minLeft) minLeft = left;
                if (n == 0 || left > maxLeft) maxLeft = left;
                if (left > 36.0 + 1e-3) beyondGame++;
                n++;
            }
            return list.Count;
        }

        /// <summary>Linia startu sesji (ArmouryBehavior.OnSessionLaunched, PO McmSettings.Apply): klucze, rok i mnoznik, czynny model, czas ciazy
        /// czynnego modelu, ciaze w toku (z zapisu: stary termin zostaje), zamezne 18-45. Liczniki sesji od zera.</summary>
        internal static void SessionStart()
        {
            lock (Gate) { _rolls = 0; _sumNow = _sumGame = 0; }
            _conceived = _births = _alive = _still = 0;
            _sConceived = _sBirths = 0;
            _pending.Clear(); _carry.Clear();
            _durN = _bornN = 0; _durMin = _durMax = _bornMin = _bornMax = 0;
            var sb = new System.Text.StringBuilder();
            int d = Days;
            int year = 0;
            try { year = CampaignTime.DaysInYear; } catch { }
            double f = ChanceFactor();
            sb.Append("Ciaza i plodnosc (CR): start sesji | PregnancyDays ").Append(d).Append(d == 0 ? " (gra)" : "")
              .Append(", FertilityYearScale ").Append(ScaleOn)
              .Append(" | rok ").Append(year).Append(" dob, mnoznik szansy poczecia ").Append(f.ToString("0.0000", Inv))
              .Append(ScaleOn ? (year > GameYear ? " (84 / " + year + ")" : " (rok nie dluzszy niz 84 - jak gra)") : " (wylaczony)");
            try
            {
                var c = Campaign.Current;
                var pm = c != null ? c.Models.PregnancyModel : null;
                if (pm != null)
                {
                    var ownDur = new List<string>();
                    var ownChance = new List<string>();
                    for (var t = pm.GetType(); t != null && t != typeof(DefaultPregnancyModel) && t != typeof(object); t = t.BaseType)
                    {
                        var p = t.GetProperty("PregnancyDurationInDays", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                        if (p != null && p.GetGetMethod(true) != null) ownDur.Add(t.Name);
                        if (AccessTools.DeclaredMethod(t, "GetDailyChanceOfPregnancyForHero", new[] { typeof(Hero) }) != null) ownChance.Add(t.Name);
                    }
                    bool fast = false;
                    try { fast = c.Options.AccelerationMode == GameAccelerationMode.Fast; } catch { }
                    sb.Append(" | czynny model ").Append(pm.GetType().FullName).Append(": z modelu gry ").Append(pm is DefaultPregnancyModel ? "tak" : "NIE (latki nie dzialaja)")
                      .Append(", wlasny getter czasu ").Append(ownDur.Count > 0 ? string.Join("/", ownDur) + " (UWAGA)" : "brak")
                      .Append(", wlasna szansa ").Append(ownChance.Count > 0 ? string.Join("/", ownChance) : "brak")
                      .Append(" | czas ciazy czynnego modelu ").Append(pm.PregnancyDurationInDays.ToString("0.##", Inv)).Append(" dob (gra ").Append(fast ? 18 : 36).Append(fast ? ", tryb Fast" : "").Append(")");
                }
                else sb.Append(" | czynny model -");
            }
            catch (Exception e) { Stumble("SessionStart (model)", e); sb.Append(" | czynny model - WYJATEK"); }
            try
            {
                sb.Append(" | cykl zycia ").Append(CampaignOptions.IsLifeDeathCycleDisabled ? "WYLACZONY (brak ciaz i porodow)" : "wlaczony");
                double lo, hi; int beyond;
                int n = InProgress(PregList(), out lo, out hi, out beyond);
                if (n < 0) sb.Append(" | ciaze w toku - (brak listy gry)");
                else
                {
                    sb.Append(" | ciaze w toku ").Append(n);
                    if (n > 0) sb.Append(": termin za ").Append(lo.ToString("0.#", Inv)).Append("-").Append(hi.ToString("0.#", Inv)).Append(" dob, dalej niz 36 dob ").Append(beyond)
                                 .Append(" (termin zapisany przy poczeciu - zostaje)");
                }
                int w, wp;
                Wives(out w, out wp);
                sb.Append(" | zamezne 18-45 ").Append(w).Append(" (w ciazy ").Append(wp).Append(")");
            }
            catch (Exception e) { Stumble("SessionStart (stan)", e); sb.Append(" | stan - WYJATEK"); }
            sb.Append(" | latki: czas ").Append(_durHooked ? "tak" : "NIE").Append(", szansa ").Append(_chanceHooked ? "tak" : "NIE")
              .Append(" | potkniecia ").Append(_errors);
            Log.Info(sb.ToString());
        }

        /// <summary>Linia doby (codziennie): ciaze w toku, zamezne 18-45, poczecia i porody od poprzedniej linii i od startu sesji, rzuty szansy
        /// i suma szans (po mnozniku / gry z BK), czas ciazy z terminu gry dla poczec tej sesji, doby od poczecia do porodu.</summary>
        internal static void Daily()
        {
            var list = PregList();
            // terminy poczec tej sesji - gra dopisala je do swojej listy zaraz po naszym sluchaczu
            if (_pending.Count > 0)
            {
                var due = new Dictionary<Hero, double>();
                if (list != null && _fMother != null && _fDue != null)
                    foreach (var p in list)
                    {
                        var m = p != null ? _fMother.GetValue(p) as Hero : null;
                        if (m != null && _pending.ContainsKey(m)) due[m] = ((CampaignTime)_fDue.GetValue(p)).ToDays;
                    }
                foreach (var kv in due)
                {
                    double t0 = _pending[kv.Key];
                    double dur = kv.Value - t0;
                    if (_durN == 0 || dur < _durMin) _durMin = dur;
                    if (_durN == 0 || dur > _durMax) _durMax = dur;
                    _durN++;
                    _carry[kv.Key] = t0;
                }
                _pending.Clear();   // bez wpisu w liscie gry (smierc matki, przerwana ciaza) - poza pomiarem
            }
            if (_carry.Count > 0)
            {
                var dead = new List<Hero>();
                foreach (var kv in _carry) if (kv.Key == null || !kv.Key.IsAlive || !kv.Key.IsPregnant) dead.Add(kv.Key);
                foreach (var h in dead) _carry.Remove(h);
            }
            double lo, hi; int beyond;
            int n = InProgress(list, out lo, out hi, out beyond);
            int w, wp;
            Wives(out w, out wp);
            int rolls; double sNow, sGame;
            lock (Gate) { rolls = _rolls; sNow = _sumNow; sGame = _sumGame; _rolls = 0; _sumNow = _sumGame = 0; }
            var sb = new System.Text.StringBuilder();
            sb.Append("Ciaza i plodnosc (CR): dzien ").Append((int)CampaignTime.Now.ToDays)
              .Append(" | w toku ").Append(n < 0 ? "-" : n.ToString(Inv));
            if (n > 0) sb.Append(" (najdalszy termin za ").Append(hi.ToString("0.0", Inv)).Append(" dob, dalej niz 36 dob ").Append(beyond).Append(")");
            sb.Append(" | zamezne 18-45 ").Append(w).Append(" (w ciazy ").Append(wp).Append(")")
              .Append(" | poczecia ").Append(_conceived).Append(" (sesja ").Append(_sConceived).Append(")")
              .Append(" | porody ").Append(_births).Append(": dzieci ").Append(_alive).Append(", martwe ").Append(_still).Append(" (sesja ").Append(_sBirths).Append(")")
              .Append(" | rzuty ").Append(rolls).Append(", suma szans ").Append(sNow.ToString("0.0000", Inv)).Append(" (gra ").Append(sGame.ToString("0.0000", Inv)).Append(")")
              .Append(" | czas ciazy z terminu gry: poczec tej sesji ").Append(_durN);
            if (_durN > 0) sb.Append(", ").Append(_durMin.ToString("0.0", Inv)).Append("-").Append(_durMax.ToString("0.0", Inv)).Append(" dob");
            sb.Append(" | porody poczec tej sesji ").Append(_bornN);
            if (_bornN > 0) sb.Append(", ").Append(_bornMin.ToString("0.0", Inv)).Append("-").Append(_bornMax.ToString("0.0", Inv)).Append(" dob od poczecia");
            if (_errors > 0) sb.Append(" | potkniecia ").Append(_errors);
            Log.Info(sb.ToString());
            _conceived = _births = _alive = _still = 0;
        }
    }
}
