using System;
using System.Globalization;
using System.Threading;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// 509 (perki, punkt 57 ForcedLabor; Jeff 14.11: "jency zawsze jak sa budowy w danym zamku sa wykorzystywani do budowy i pracuja zwiekszajac tempo budowy,
    /// ten perk sprawia ze pracuja duzo lepiej np. normalnie za kazdego jenca +0.1% budowy, a przy perku 0.2%. Jest minimalna szansa codziennie ze jakis jeniec
    /// zostanie ranny podczas pracy oraz naprawde znikoma szansa ze umrze, trzeba ich karmic i dawac im jesc. Ma to byc realne.") - PRACA JENCOW PRZY BUDOWIE.
    ///
    /// GRA (dekompilacja 1.4.8): DefaultBuildingConstructionModel - ForcedLabor zarzadcy na miejscu: +1% za kazdych 3 jencow (najwyzej +30%) i drugi raz plaskie +1%
    /// (AddPerkBonusForTown). BKConstructionModel (czynny u Jeffa pod Naval/BKROT) ma TYLKO plaskie +1% (bez jencow). Osady objete rozliczeniem budow (BuildFunding.Covered)
    /// i tak dostaja punkty z PowerPostfix (wynik modelu podmieniony) - premia jencow nie istniala nigdzie. Jedzenie: DefaultSettlementFoodModel i BKFoodModel licza
    /// spozycie z dobrobytu i zalogi, jency lochu NIE jedza (grep PrisonRoster w obu modelach - brak); DungeonFate 402/404 liczy tylko choroby - karmienia nie ma.
    ///
    /// REGULA (klucz PrisonLabor; off = jak dotad co do bitu):
    ///  - PRACUJA zdrowi szeregowi jency lochu twierdzy (bez bohaterow, bez Innych - PeopleBook.IsWight), gdy w osadzie jest budowa w toku (czolo kolejki nie jest
    ///    projektem dziennym). Premia = robotnicy x PrisonLaborPercentPerPrisoner (0.1%); zarzadca NA MIEJSCU z ForcedLabor (warunek gry AddPerkBonusForTown) -
    ///    PrisonLaborPercentForcedLabor (0.2%); najwyzej PrisonLaborMaxPercent (100%) punktow doby.
    ///  - Podstawa premii: osady objete rozliczeniem - punkty oplacone przez pana + przyspieszenie zlotem (ta sama podstawa co premia zarzadcy R3-10b, EngExtra;
    ///    wchodzi przez EngExtra, wiec dymek, liczba calkowita mocy, mury 472 i "ile pan musi doplacic" EngPaidNeed licza ja tak samo); lenna gracza bez rozliczenia -
    ///    wynik modelu gry. Jency nie pracuja bez robot: bez punktow pana (0) premii nie ma - jency nosza i kopia przy oplaconych murarzach i materiale.
    ///  - WYPADKI (blok doby, robotnicy z rana tej doby juz nie pracuja): kazdy robotnik z szansa PrisonLaborInjuryPercent (0.2%/dobe) idzie w rannych
    ///    (leczy go gra - PartyHealCampaignBehavior.OnDailyTickSettlement), z szansa PrisonLaborDeathPercent (0.01%/dobe) ginie: zdjety z lochu,
    ///    ksiega ludzi PeopleBook (DieShares, przyczyna FDieOthers), komunikat dla lochu gracza.
    ///  - JEDZENIE (klucz DungeonPrisonersEat; wszyscy jency lochu twierdzy, takze bohaterowie i bezczynni, bez Innych): postfiks na
    ///    DefaultSettlementFoodModel.CalculateTownFoodStocksChange (BK wola baze jawnie, ROT przekazuje dalej - jak Famine/PeopleMarket) - pozycja "Prisoners"
    ///    = -(jency x DungeonPrisonerFoodShare) / NumberOfMenOnGarrisonToEatOneFood (gra: jeniec w partii je pol porcji - 0.5; zaloga 1/20).
    ///    Zywnosc z zapasow osady (Food), jak zaloga - przy braku BK dokupuje z targu (EconomyPatches), oblezenie bez dostaw (SiegeWalls2 - postfiks Last po nas).
    /// Ekonomia: praca jencow to prawdziwa praca (wiecej punktow z tych samych pieniedzy pana), oplacona jedzeniem osady - nic z niczego, bez zlota.
    /// Potkniecia liczone (Interlocked), blad do logu raz na miejsce - bez globalnego wylacznika. Nic co klatke (postfiksy modeli - tylko petla po rosterze lochu).
    /// Linie: "Praca jencow (509): start sesji ..." i co dobe "Praca jencow (509): dzien N ...".
    /// </summary>
    internal static class PrisonLabor509
    {
        internal const string P = "Praca jencow (509): ";
        private const string HarmonyId = "com.jeff.armoury.prisonlabor509";
        private static bool _tried, _foodWired;
        private static string _note = "-";
        private static int _stumbles;
        private static readonly System.Collections.Generic.HashSet<string> _errSites = new System.Collections.Generic.HashSet<string>();
        private static TextObject _txtWork, _txtFood;

        // liczniki doby (blok dobowy, watek glowny)
        private static int _dSt, _dStPerk, _dWorkers, _dInjured, _dDead, _dEaters, _dEatSt;
        private static double _dPctSum, _dFood;

        internal static bool On { get { var s = Settings.Current; return s != null && s.PrisonLabor; } }
        internal static bool FoodOn { get { var s = Settings.Current; return s != null && s.DungeonPrisonersEat; } }

        private static void Stumble(string where, Exception e)
        {
            Interlocked.Increment(ref _stumbles);
            try { lock (_errSites) { if (!_errSites.Add(where)) return; } Log.Error("PrisonLabor509." + where, e); } catch { }
        }

        /// <summary>Zdrowi szeregowi jency (bez bohaterow i Innych) - robotnicy. Czyste.</summary>
        private static int Workers(TroopRoster pr)
        {
            int n = 0;
            for (int i = 0; i < pr.Count; i++)
            {
                var ch = pr.GetCharacterAtIndex(i);
                if (ch == null || ch.IsHero || PeopleBook.IsWight(ch)) continue;
                n += pr.GetElementNumber(i) - pr.GetElementWoundedNumber(i);
            }
            return n;
        }

        private static bool PerkGov(Town town)
        {
            var g = town.Governor;
            return g != null && g.IsAlive && g.CurrentSettlement != null && g.CurrentSettlement == town.Settlement
                   && DefaultPerks.Steward.ForcedLabor != null && g.GetPerkValue(DefaultPerks.Steward.ForcedLabor);
        }

        /// <summary>Udzial premii jencow w punktach doby (0.1 = +10%); 0 - klucz wylaczony, brak budowy, brak robotnikow, blad. Czyste.</summary>
        internal static float Share(Town town, out int workers, out bool perk)
        {
            workers = 0; perk = false;
            try
            {
                var s = Settings.Current;
                if (s == null || !s.PrisonLabor || town == null || town.Settlement == null || !town.Settlement.IsFortification) return 0f;
                var q = town.BuildingsInProgress;
                if (q == null || q.Count == 0) return 0f;
                var head = q.Peek();
                if (head == null || head.BuildingType == null || head.BuildingType.IsDailyProject) return 0f;
                var party = town.Settlement.Party;
                var pr = party != null ? party.PrisonRoster : null;
                if (pr == null || pr.TotalRegulars <= 0) return 0f;
                workers = Workers(pr);
                if (workers <= 0) return 0f;
                perk = PerkGov(town);
                float per = Math.Max(0f, perk ? s.PrisonLaborPercentForcedLabor : s.PrisonLaborPercentPerPrisoner);
                float pct = Math.Min(Math.Max(0f, s.PrisonLaborMaxPercent), workers * per) / 100f;
                return pct > 0f && !float.IsNaN(pct) && !float.IsInfinity(pct) ? pct : 0f;
            }
            catch (Exception e) { Stumble("Share", e); return 0f; }
        }

        /// <summary>Punkty jencow do podstawy (pana + przyspieszenie zlotem) osady objetej rozliczeniem - wolane z BuildFunding.EngExtra (liniowe w podstawie). Czyste.</summary>
        internal static float Extra(Town town, float paid, float boost)
        {
            float b = paid + boost;
            if (!(b > 0f)) return 0f;
            int w; bool pk;
            float pct = Share(town, out w, out pk);
            return pct > 0f ? b * pct : 0f;
        }

        internal static TextObject WorkText { get { if (_txtWork == null) _txtWork = new TextObject("{=!}Prisoners at work"); return _txtWork; } }

        /// <summary>Lenno gracza bez rozliczenia (wynik modelu gry): pozycja dymku "Prisoners at work" = udzial x wynik.</summary>
        internal static void AddGame(Town town, ref ExplainedNumber r)
        {
            try
            {
                float v = r.ResultNumber;
                if (!(v > 0f)) return;
                int w; bool pk;
                float pct = Share(town, out w, out pk);
                if (pct > 0f) r.Add(v * pct, WorkText);
            }
            catch (Exception e) { Stumble("AddGame", e); }
        }

        /// <summary>Jak wyzej dla mocy bez przyspieszenia (liczba calkowita).</summary>
        internal static int AddGameInt(Town town, int v)
        {
            try
            {
                if (v <= 0) return v;
                int w; bool pk;
                float pct = Share(town, out w, out pk);
                return pct > 0f ? v + (int)Math.Round(v * pct) : v;
            }
            catch (Exception e) { Stumble("AddGameInt", e); return v; }
        }

        // ------------------------------------------------------------ jedzenie jencow lochu
        private static int Eaters(TroopRoster pr)
        {
            int n = 0;
            for (int i = 0; i < pr.Count; i++)
            {
                var ch = pr.GetCharacterAtIndex(i);
                if (ch == null || (!ch.IsHero && PeopleBook.IsWight(ch))) continue;   // Inni nie jedza (jak ROT dla ich partii)
                n += pr.GetElementNumber(i);
            }
            return n;
        }

        private static float EatOf(Town town, out int eaters)
        {
            eaters = 0;
            var s = Settings.Current;
            if (s == null || !s.DungeonPrisonersEat || town == null || town.Settlement == null) return 0f;
            var party = town.Settlement.Party;
            var pr = party != null ? party.PrisonRoster : null;
            if (pr == null || pr.TotalManCount <= 0) return 0f;
            eaters = Eaters(pr);
            if (eaters <= 0) return 0f;
            int per = 20;
            try { var fm = Campaign.Current != null ? Campaign.Current.Models.SettlementFoodModel : null; if (fm != null && fm.NumberOfMenOnGarrisonToEatOneFood > 0) per = fm.NumberOfMenOnGarrisonToEatOneFood; } catch { }
            float eat = eaters * Math.Max(0f, s.DungeonPrisonerFoodShare) / per;
            return eat > 0f && !float.IsNaN(eat) && !float.IsInfinity(eat) ? eat : 0f;
        }

        /// <summary>Postfiks na DefaultSettlementFoodModel.CalculateTownFoodStocksChange (addytywny, obok Famine, PeopleMarket): jency lochu jedza z Food osady.
        /// Moze isc z watkow AI - sam odczyt. Nie rzuca.</summary>
        public static void FoodPostfix(Town town, ref ExplainedNumber __result)
        {
            if (!_foodWired || town == null) return;
            try
            {
                int n;
                float eat = EatOf(town, out n);
                if (eat <= 0f) return;
                if (_txtFood == null) _txtFood = new TextObject("{=!}Prisoners");
                __result.Add(-eat, _txtFood);
            }
            catch (Exception e) { Stumble("FoodPostfix", e); }
        }

        // ------------------------------------------------------------ latka (raz na proces, w kampanii) i linia startu
        internal static void EnsureHooks()
        {
            if (!_tried)
            {
                _tried = true;
                try
                {
                    var m = AccessTools.DeclaredMethod(typeof(DefaultSettlementFoodModel), "CalculateTownFoodStocksChange");
                    if (m == null || m.ReturnType != typeof(ExplainedNumber)) _note = "BRAK (DefaultSettlementFoodModel.CalculateTownFoodStocksChange)";
                    else
                    {
                        new Harmony(HarmonyId).Patch(m, postfix: new HarmonyMethod(typeof(PrisonLabor509), nameof(FoodPostfix)));
                        bool ours = false;
                        var info = Harmony.GetPatchInfo(m);
                        if (info != null) foreach (var p in info.Postfixes) if (p != null && p.owner == HarmonyId) ours = true;
                        _foodWired = ours;
                        _note = ours ? "wpiety (DefaultSettlementFoodModel.CalculateTownFoodStocksChange)" : "BRAK (latka nie weszla)";
                    }
                }
                catch (Exception e) { _foodWired = false; _note = "BRAK (wyjatek " + e.GetType().Name + ")"; Stumble("EnsureHooks", e); }
            }
            try
            {
                var s = Settings.Current;
                if (s == null || !s.LogEnabled) return;
                var inv = CultureInfo.InvariantCulture;
                Log.Info(P + "start sesji | Prison Labor " + (s.PrisonLabor ? "tak" : "nie") + ": robotnik +" + s.PrisonLaborPercentPerPrisoner.ToString("0.###", inv) + "% punktow doby (ForcedLabor zarzadcy na miejscu +"
                         + s.PrisonLaborPercentForcedLabor.ToString("0.###", inv) + "%), najwyzej +" + s.PrisonLaborMaxPercent.ToString("0", inv) + "%; rana " + s.PrisonLaborInjuryPercent.ToString("0.###", inv)
                         + "%/dobe, smierc " + s.PrisonLaborDeathPercent.ToString("0.####", inv) + "%/dobe na robotnika | Dungeon Prisoners Eat " + (s.DungeonPrisonersEat ? "tak" : "nie")
                         + " (jeniec x" + s.DungeonPrisonerFoodShare.ToString("0.##", inv) + " porcji zalogi), postfiks jedzenia " + _note + " | perki budowy zarzadcy (Perk Build Speed On Paid Work) "
                         + (s.PerkBuildSpeedOnPaidWork ? "tak" : "nie") + ", Architectural Commissions z kasy osady " + (s.ArchitecturalCommissionsTownPays ? "tak" : "nie") + " (" + Perk509.Note + ").");
            }
            catch { }
        }

        // ------------------------------------------------------------ doba
        /// <summary>Losowy zdrowy szeregowy jeniec (bez bohaterow i Innych), wazony liczebnoscia: kill - zdjety z lochu, inaczej w rannych. null - nie bylo kogo.</summary>
        private static CharacterObject HitHealthy(TroopRoster roster, bool kill)
        {
            int pool = Workers(roster);
            if (pool <= 0) return null;
            int pick = MBRandom.RandomInt(pool);
            for (int i = 0; i < roster.Count; i++)
            {
                var ch = roster.GetCharacterAtIndex(i);
                if (ch == null || ch.IsHero || PeopleBook.IsWight(ch)) continue;
                int k = roster.GetElementNumber(i) - roster.GetElementWoundedNumber(i);
                if (k <= 0) continue;
                if (pick >= k) { pick -= k; continue; }
                if (kill) roster.AddToCounts(ch, -1, false, 0);
                else roster.WoundTroop(ch);
                return ch;
            }
            return null;
        }

        /// <summary>ArmouryBehavior, doba - po BuildFunding.Daily: wypadki przy pracy, liczniki i linia doby. Kazda osada we wlasnym try.</summary>
        internal static void Daily()
        {
            _dSt = _dStPerk = _dWorkers = _dInjured = _dDead = _dEaters = _dEatSt = 0; _dPctSum = _dFood = 0;
            var s = Settings.Current;
            if (s == null || Campaign.Current == null) return;
            foreach (var st in Settlement.All)
            {
                if (st == null || !st.IsFortification || st.Town == null) continue;
                try
                {
                    var town = st.Town;
                    int eaters;
                    float eat = EatOf(town, out eaters);
                    if (eat > 0f && _foodWired) { _dEatSt++; _dEaters += eaters; _dFood += eat; }
                    if (st.Party == null || st.Party.MapEvent != null) continue;   // osada w bitwie - ta doba bez robot
                    int w; bool pk;
                    float pct = Share(town, out w, out pk);
                    if (!(pct > 0f) || w <= 0) continue;
                    _dSt++; _dWorkers += w; _dPctSum += pct; if (pk) _dStPerk++;
                    var pr = st.Party.PrisonRoster;
                    int hurt = Math.Min(w, MBRandom.RoundRandomized(w * Math.Max(0f, s.PrisonLaborInjuryPercent) / 100f));
                    int die = Math.Min(w - hurt, MBRandom.RoundRandomized(w * Math.Max(0f, s.PrisonLaborDeathPercent) / 100f));
                    int dead = 0;
                    for (int n = 0; n < die; n++) if (HitHealthy(pr, true) != null) dead++;
                    for (int n = 0; n < hurt; n++) { if (HitHealthy(pr, false) == null) break; _dInjured++; }
                    if (dead > 0)
                    {
                        try { PeopleBook.DieShares(PeopleBook.Take(pr, dead, PeopleBook.FDieOthers), PeopleBook.FDieOthers); } catch (Exception e) { Stumble("PeopleBook", e); }
                        _dDead += dead;
                        if (st.OwnerClan == Clan.PlayerClan)
                            Log.Player((dead == 1 ? "A prisoner" : dead + " prisoners") + " died in an accident at the works of " + st.Name + ".", true);
                    }
                }
                catch (Exception e) { Stumble("Daily(osada)", e); }
            }
            try
            {
                if (!s.LogEnabled) return;
                var inv = CultureInfo.InvariantCulture;
                int err = Interlocked.Exchange(ref _stumbles, 0);
                Log.Info(P + "dzien " + (int)CampaignTime.Now.ToDays + " | Prison Labor " + (s.PrisonLabor ? "tak" : "nie") + ": twierdz z budowa i pracujacymi jencami " + _dSt
                         + " (z ForcedLabor zarzadcy " + _dStPerk + "), robotnikow " + _dWorkers + ", srednia premia +" + (_dSt > 0 ? (100.0 * _dPctSum / _dSt).ToString("0.0", inv) : "0.0")
                         + "% punktow doby; rannych przy pracy " + _dInjured + ", zmarlych " + _dDead + " | jedzenie lochow (Dungeon Prisoners Eat " + (s.DungeonPrisonersEat && _foodWired ? "tak" : "nie")
                         + "): twierdz " + _dEatSt + ", jencow " + _dEaters + ", zjadaja " + _dFood.ToString("0.0", inv) + " Food/dobe | Perk509: " + Perk509.DayLine() + "; potkniecia " + err + ".");
            }
            catch (Exception e) { Log.Error("PrisonLabor509.Daily(linia)", e); }
        }
    }
}
