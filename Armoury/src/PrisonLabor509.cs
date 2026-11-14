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
    ///    ksiega ludzi PeopleBook (DieShares, przyczyna FDieWork "wypadki przy pracy jencow"), komunikat dla lochu gracza. Wypadki tylko przy prawdziwej pracy:
    ///    PaidConstruction wlaczone, osada nie w bitwie i nie oblezona, podstawa premii tej doby > 0 (BuildFunding.LaborBase).
    ///  - JEDZENIE (klucz DungeonPrisonersEat; wszyscy jency lochu twierdzy, takze bohaterowie i bezczynni, bez Innych): postfiks na
    ///    DefaultSettlementFoodModel.CalculateTownFoodStocksChange (BK wola baze jawnie, ROT przekazuje dalej - jak Famine/PeopleMarket) - pozycja "Prisoners"
    ///    = -(jency x DungeonPrisonerFoodShare) / NumberOfMenOnGarrisonToEatOneFood (gra: jeniec w partii je pol porcji - 0.5; zaloga 1/20).
    ///    Zywnosc z zapasow osady (Food), jak zaloga - przy braku BK dokupuje z targu (EconomyPatches), oblezenie bez dostaw (SiegeWalls2 - postfiks Last po nas).
    ///  - GLOD (D3, Jeff 14.11: "D3 - tak, pol porcji"; PERKI-DECYZJE.md: "glod oslabia prace; smierc/ucieczki - zgodnie z propozycja"; tylko przy DungeonPrisonersEat -
    ///    jency jedza z tych samych zapasow, wiec gloduja razem z nimi): twierdza z Food 0 (gra Settlement.IsStarving) - (a) jency pracuja
    ///    PrisonLaborHungryWorkPercent (50%) zwyklej premii (Share - jedna liczba dla punktow, dymka i wypadkow); (b) po tygodniu laski (PrisonHungerGraceDays 7
    ///    kolejnych dob glodu - ta sama laska co glod zalogi GL SiegeWalls2.SiegeStarveGraceDays) co dobe ginie PrisonHungerDeathPercent (5%, jak
    ///    SiegeStarveLossPercent zalogi) szeregowych jencow lochu (najpierw ranni; bez bohaterow - gra ich nie glodzi, bez Innych - nie jedza): zdjeci z lochu,
    ///    ksiega ludzi FDieHunger, komunikat dla lochu gracza; (c) ucieczek z glodu NIE ma (propozycja 509 pkt 4: praca i loch bez nowych ucieczek - mury
    ///    i straz te same; ucieczka przez mur GL dotyczy zalogi oblezonej twierdzy, nie jencow). Licznik dob glodu - stan sesji (slownik osad, czyszczony
    ///    przy nowej kampanii); po wczytaniu zapisu laska liczy sie od nowa (najwyzej tydzien zwloki, bez zapisu).
    ///  - UCIECZKI PRZY PRACY (519, Jeff 14.11 "3. tak" na pytanie "praca jencow przy budowie zwieksza szanse ich ucieczki z lochu"): KTO LICZY UCIECZKI Z LOCHU
    ///    (dekompilacja 1.4.8): tylko gra - PrisonerReleaseCampaignBehavior.DailyHeroTick, i tylko BOHATEROW (4%/dobe, x0.5 w lochu rodu gracza, zarzadca:
    ///    SweetTalker / DungeonArchitect / MountedPatrols - SecondaryBonus jako AddFactor). Szeregowi w lochu twierdzy nie uciekaja wcale (HourlyPartyTick /
    ///    ApplyEscapeChanceToExceededPrisoners - tylko partie ruchome ponad limit, bez zalog i milicji). DungeonFate (402) tylko liczy ucieczki bohaterow,
    ///    RealisticCaptivity - niewola gracza, BK / ROT - brak. Robotnicy 509 to wylacznie szeregowi, wiec "wiecej ucieczek" = NOWA szansa tylko dla pracujacych
    ///    (jency w celi dalej 0, jak w grze): kazdy robotnik tej doby (te same warunki co wypadki - prawdziwa praca) ucieka z szansa PrisonLaborEscapePercent
    ///    (0.05%/dobe) x czynnik gry (x0.5 loch rodu gracza; 1 + SecondaryBonus perkow zarzadcy, jak DailyHeroTick, nie mniej niz 0). LICZBY: bohater w lochu 4%/dobe
    ///    ma pomoc z zewnatrz (rod, kurier, przekupstwo); szeregowy przy budowie - tylko okazje przy pracy pod straza: 1/80 szansy lorda = 0.05%/dobe = 1 na 2000
    ///    robotniko-dob; stala pula robotnikow traci przez rok Armoury (364 doby) ok. 17% (1 - 0.9995^364), 100 robotnikow - jeden zbieg co ok. 20 dob. Mniej niz rany
    ///    przy pracy (0.2%), wiecej niz smierc (0.01%). Zbieg schodzi z lochu (zdrowy robotnik, wazony liczebnoscia - HitHealthy) i idzie ta sama droga co jency
    ///    zbiegli ponad limit lochu partii (PeopleBook fixA EscapePost): przy PrisonEscapeGoHome - Take FEscape i LosersFlee.FreedHome (karczma / dom wedlug
    ///    pochodzenia / pula regionu / nazwana nicosc), bez niego znika jak w grze (uzgodnienie doby ksiegi ludzi). Nic z niczego: ten sam czlowiek zmienia konto.
    ///    Glod (D3) nie dodaje ucieczek (bez zmian); glodni robotnicy liczeni jak syci - ucieczka zalezy od wyjscia do pracy, nie od jej wydajnosci.
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
        // D3: glod lochu - kolejne doby glodu twierdzy (StringId osady -> doby; stan sesji, czyszczony przy nowej kampanii) i liczniki doby
        private static readonly System.Collections.Generic.Dictionary<string, int> _hungerDays = new System.Collections.Generic.Dictionary<string, int>();
        private static Campaign _hungerCamp;
        private static int _dHungrySt, _dHungryGrace, _dHungryWork, _dHungerDead;
        // 519: ucieczki robotnikow - zbiegli (do domu / do puli / do karczmy / znikneli)
        private static int _dEsc, _dEscHome, _dEscPool, _dEscTavern, _dEscLost;

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
                if (s == null || !s.PrisonLabor || !BuildFunding.On || town == null || town.Settlement == null || !town.Settlement.IsFortification) return 0f;
                // 509 recenzja: oblezenie (gra nie buduje - BuildingsCampaignBehavior:106) i bitwa w osadzie - jency nie pracuja (ta sama zasada co wypadki w Daily)
                if (town.Settlement.IsUnderSiege || (town.Settlement.Party != null && town.Settlement.Party.MapEvent != null)) return 0f;
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
                pct *= HungerWork(town);   // D3: glodni jency (Food 0 przy DungeonPrisonersEat) pracuja slabiej - jedna liczba dla punktow, dymka i wypadkow
                return pct > 0f && !float.IsNaN(pct) && !float.IsInfinity(pct) ? pct : 0f;
            }
            catch (Exception e) { Stumble("Share", e); return 0f; }
        }

        /// <summary>D3: mnoznik pracy jencow twierdzy - 1, a przy glodzie (DungeonPrisonersEat i Food 0, gra Settlement.IsStarving) PrisonLaborHungryWorkPercent / 100. Czyste.</summary>
        internal static float HungerWork(Town town)
        {
            var s = Settings.Current;
            if (s == null || !s.DungeonPrisonersEat || !_foodWired || town == null || town.Settlement == null || !town.Settlement.IsStarving) return 1f;   // recenzja: bez latki jedzenia lochu jency nie jedza z zapasu - nie gloduja (jak Daily)
            float f = s.PrisonLaborHungryWorkPercent / 100f;
            return float.IsNaN(f) ? 1f : Math.Max(0f, Math.Min(1f, f));
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
                if (!(pct > 0f)) return v;
                int add = (int)Math.Round(v * pct);
                return Math.Max(v, Math.Min(v + add, 100));   // 509 recenzja: ta sama granica co BK LimitMax(100) w mocy z opisem (AddGame) - obie drogi daja to samo
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
                         + "%/dobe, smierc " + s.PrisonLaborDeathPercent.ToString("0.####", inv) + "%/dobe, ucieczka (519) " + s.PrisonLaborEscapePercent.ToString("0.####", inv)
                         + "%/dobe na robotnika (x0.5 loch rodu gracza, perki zarzadcy jak w grze; jency w celi bez ucieczek jak w grze) | Dungeon Prisoners Eat " + (s.DungeonPrisonersEat ? "tak" : "nie")
                         + " (jeniec x" + s.DungeonPrisonerFoodShare.ToString("0.##", inv) + " porcji zalogi), postfiks jedzenia " + _note
                         + " | glod lochu (D3, przy Food 0): praca x" + HungerShareText(s) + ", po " + Math.Max(0, s.PrisonHungerGraceDays) + " dobach laski umiera "
                         + s.PrisonHungerDeathPercent.ToString("0.##", inv) + "%/dobe szeregowych jencow, ucieczek z glodu brak | perki budowy zarzadcy (Perk Build Speed On Paid Work) "
                         + (s.PerkBuildSpeedOnPaidWork ? "tak" : "nie") + ", Architectural Commissions z kasy osady " + (s.ArchitecturalCommissionsTownPays ? "tak" : "nie") + " (" + Perk509.Note + ").");
            }
            catch { }
        }

        private static string HungerShareText(Settings s)
        {
            float f = s.PrisonLaborHungryWorkPercent / 100f;
            return (float.IsNaN(f) ? 1f : Math.Max(0f, Math.Min(1f, f))).ToString("0.##", CultureInfo.InvariantCulture);
        }

        /// <summary>D3: szeregowi jency lochu, ktorzy moga umrzec z glodu (bez bohaterow i Innych; zdrowi i ranni). Czyste.</summary>
        private static int Starvable(TroopRoster pr)
        {
            int n = 0;
            for (int i = 0; i < pr.Count; i++)
            {
                var ch = pr.GetCharacterAtIndex(i);
                if (ch == null || ch.IsHero || PeopleBook.IsWight(ch)) continue;
                n += pr.GetElementNumber(i);
            }
            return n;
        }

        /// <summary>D3: zgon z glodu - najpierw ranny (wazony liczba rannych), inaczej zdrowy (HitHealthy). false - nie bylo kogo.</summary>
        private static bool StarveOne(TroopRoster roster)
        {
            int wounded = 0;
            for (int i = 0; i < roster.Count; i++)
            {
                var ch = roster.GetCharacterAtIndex(i);
                if (ch == null || ch.IsHero || PeopleBook.IsWight(ch)) continue;
                wounded += roster.GetElementWoundedNumber(i);
            }
            if (wounded > 0)
            {
                int pick = MBRandom.RandomInt(wounded);
                for (int i = 0; i < roster.Count; i++)
                {
                    var ch = roster.GetCharacterAtIndex(i);
                    if (ch == null || ch.IsHero || PeopleBook.IsWight(ch)) continue;
                    int k = roster.GetElementWoundedNumber(i);
                    if (k <= 0) continue;
                    if (pick >= k) { pick -= k; continue; }
                    roster.AddToCounts(ch, -1, false, -1);
                    return true;
                }
            }
            return HitHealthy(roster, true) != null;
        }

        /// <summary>D3 (blok doby, osada st z lochem): licznik kolejnych dob glodu i zgony po tygodniu laski. Wolane tylko przy DungeonPrisonersEat.</summary>
        private static void HungerDay(Settlement st, Settings s)
        {
            string id = st.StringId ?? "";
            var pr = st.Party != null ? st.Party.PrisonRoster : null;
            if (!st.IsStarving || pr == null || Starvable(pr) <= 0) { _hungerDays.Remove(id); return; }
            int days;
            _hungerDays.TryGetValue(id, out days);
            days++;
            _hungerDays[id] = days;
            _dHungrySt++;
            if (days <= Math.Max(0, s.PrisonHungerGraceDays)) { _dHungryGrace++; return; }
            int regs = Starvable(pr);
            float pct = float.IsNaN(s.PrisonHungerDeathPercent) ? 0f : Math.Max(0f, Math.Min(100f, s.PrisonHungerDeathPercent));
            int die = Math.Min(regs, MBRandom.RoundRandomized(regs * pct / 100f));
            int dead = 0;
            for (int n = 0; n < die; n++) { if (!StarveOne(pr)) break; dead++; }
            if (dead <= 0) return;
            try { PeopleBook.DieShares(PeopleBook.Take(pr, dead, PeopleBook.FDieHunger), PeopleBook.FDieHunger); } catch (Exception e) { Stumble("PeopleBook(glod)", e); }
            _dHungerDead += dead;
            if (st.OwnerClan == Clan.PlayerClan)
                Log.Player((dead == 1 ? "A prisoner" : dead + " prisoners") + " starved to death in the dungeon of " + st.Name + " - its food stores are empty.", true);
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

        /// <summary>519: czynnik szansy ucieczki jak w grze (PrisonerReleaseCampaignBehavior.DailyHeroTick dla lochu twierdzy): x0.5 w lochu rodu gracza,
        /// zarzadca z SweetTalker / DungeonArchitect / MountedPatrols - 1 + suma SecondaryBonus (AddFactor), nie mniej niz 0. Czyste.</summary>
        private static float EscapeFactor(Settlement st)
        {
            float f = st.OwnerClan == Clan.PlayerClan ? 0.5f : 1f;
            float add = 0f;
            var g = st.Town != null ? st.Town.Governor : null;
            if (g != null)
            {
                if (DefaultPerks.Roguery.SweetTalker != null && g.GetPerkValue(DefaultPerks.Roguery.SweetTalker)) add += DefaultPerks.Roguery.SweetTalker.SecondaryBonus;
                if (DefaultPerks.Engineering.DungeonArchitect != null && g.GetPerkValue(DefaultPerks.Engineering.DungeonArchitect)) add += DefaultPerks.Engineering.DungeonArchitect.SecondaryBonus;
                if (DefaultPerks.Riding.MountedPatrols != null && g.GetPerkValue(DefaultPerks.Riding.MountedPatrols)) add += DefaultPerks.Riding.MountedPatrols.SecondaryBonus;
            }
            return Math.Max(0f, f * (1f + add));
        }

        /// <summary>519: ucieczki robotnikow twierdzy st tej doby (pool - zdrowi robotnicy po wypadkach). Zbieg zdjety z lochu (HitHealthy) i ta sama droga co
        /// ucieczka ponad limit lochu partii (PeopleBook.EscapePost): przy PrisonEscapeGoHome - Take FEscape + LosersFlee.FreedHome, inaczej znika jak w grze.</summary>
        private static void EscapeDay(Settlement st, TroopRoster pr, int pool, Settings s)
        {
            float pct = float.IsNaN(s.PrisonLaborEscapePercent) ? 0f : Math.Max(0f, Math.Min(100f, s.PrisonLaborEscapePercent));
            if (pool <= 0 || !(pct > 0f)) return;
            int esc = Math.Min(pool, MBRandom.RoundRandomized(pool * pct / 100f * EscapeFactor(st)));
            if (esc <= 0) return;
            Vec2 pos = st.GetPosition2D;
            Settlement region = null; bool law = false;
            if (s.PrisonEscapeGoHome)
            {
                region = OutlawLaw.PoolRegionAt(pos);
                law = OutlawLaw.On && region != null;
            }
            int gone = 0;
            for (int n = 0; n < esc; n++)
            {
                var ch = HitHealthy(pr, true);
                if (ch == null) break;
                gone++;
                if (!s.PrisonEscapeGoHome) continue;
                try
                {
                    PeopleBook.Shares org = null;
                    try { if (PeopleBook.OriginLive && !PeopleBook.IsWight(ch)) org = PeopleBook.Take(pr, 1, PeopleBook.FEscape); } catch { org = null; }
                    int vt, got, left;
                    LosersFlee.BeginBattle();   // jak EscapePost: pamiec osad BK jest na jedna bitwe - ucieczka poza bitwa
                    LosersFlee.FreedHome(ch, 1, pos, region, law, org, out vt, out got, out left);
                    _dEscTavern += vt; _dEscHome += got;
                    if (left > 0) { if (law) _dEscPool += left; else _dEscLost += left; }
                }
                catch (Exception e) { Stumble("EscapeDay(dom)", e); }
            }
            if (gone <= 0) return;
            _dEsc += gone;
            if (st.OwnerClan == Clan.PlayerClan)
                Log.Player((gone == 1 ? "A prisoner" : gone + " prisoners") + " slipped away from the works at " + st.Name + " and escaped.", true);
        }

        /// <summary>ArmouryBehavior, doba - po BuildFunding.Daily: wypadki przy pracy, liczniki i linia doby. Kazda osada we wlasnym try.</summary>
        internal static void Daily()
        {
            _dSt = _dStPerk = _dWorkers = _dInjured = _dDead = _dEaters = _dEatSt = 0; _dPctSum = _dFood = 0;
            _dHungrySt = _dHungryGrace = _dHungryWork = _dHungerDead = 0;
            _dEsc = _dEscHome = _dEscPool = _dEscTavern = _dEscLost = 0;
            var s = Settings.Current;
            if (s == null || Campaign.Current == null) return;
            if (!ReferenceEquals(_hungerCamp, Campaign.Current)) { _hungerDays.Clear(); _hungerCamp = Campaign.Current; }   // D3: stan jednej kampanii nie przecieka do drugiej
            if (!s.DungeonPrisonersEat || !_foodWired) _hungerDays.Clear();                                                // D3: jency nie jedza - nie gloduja
            foreach (var st in Settlement.All)
            {
                if (st == null || !st.IsFortification || st.Town == null) continue;
                try
                {
                    var town = st.Town;
                    int eaters;
                    float eat = EatOf(town, out eaters);
                    if (eat > 0f && _foodWired) { _dEatSt++; _dEaters += eaters; _dFood += eat; }
                    if (st.Party == null) continue;
                    // D3: glod lochu - doby glodu i zgony po tygodniu laski (tylko gdy jency jedza z zapasow twierdzy); praca ponizej (Share) juz oslabiona
                    if (s.DungeonPrisonersEat && _foodWired)
                    {
                        try { HungerDay(st, s); } catch (Exception e) { Stumble("HungerDay", e); }
                    }
                    int w; bool pk;
                    float pct = Share(town, out w, out pk);   // bitwa, oblezenie, !PaidConstruction - 0 (Share)
                    if (!(pct > 0f) || w <= 0) continue;
                    // 509 recenzja: wypadki i liczniki tylko tam, gdzie jency naprawde pracowali - podstawa premii tej doby > 0 (punkty pana + przyspieszenie zlotem
                    // albo moc gry w lennie bez rozliczenia); pan nic nie oplacil = robot nie ma = ani premii, ani ran, ani smierci
                    if (!(BuildFunding.LaborBase(town) > 0f)) continue;
                    _dSt++; _dWorkers += w; _dPctSum += pct; if (pk) _dStPerk++;
                    if (HungerWork(town) < 1f) _dHungryWork++;   // D3: twierdza z glodnymi robotnikami
                    var pr = st.Party.PrisonRoster;
                    int hurt = Math.Min(w, MBRandom.RoundRandomized(w * Math.Max(0f, s.PrisonLaborInjuryPercent) / 100f));
                    int die = Math.Min(w - hurt, MBRandom.RoundRandomized(w * Math.Max(0f, s.PrisonLaborDeathPercent) / 100f));
                    int dead = 0;
                    for (int n = 0; n < die; n++) if (HitHealthy(pr, true) != null) dead++;
                    int hurtDone = 0;
                    for (int n = 0; n < hurt; n++) { if (HitHealthy(pr, false) == null) break; _dInjured++; hurtDone++; }
                    if (dead > 0)
                    {
                        try { PeopleBook.DieShares(PeopleBook.Take(pr, dead, PeopleBook.FDieWork), PeopleBook.FDieWork); } catch (Exception e) { Stumble("PeopleBook", e); }   // 509 recenzja: wlasna przyczyna FDieWork (FDieOthers = "Inni")
                        _dDead += dead;
                        if (st.OwnerClan == Clan.PlayerClan)
                            Log.Player((dead == 1 ? "A prisoner" : dead + " prisoners") + " died in an accident at the works of " + st.Name + ".", true);
                    }
                    // 519: ucieczki przy pracy - z robotnikow, ktorzy dotrwali doby zdrowi (po wypadkach)
                    try { EscapeDay(st, pr, w - dead - hurtDone, s); } catch (Exception e) { Stumble("EscapeDay", e); }
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
                         + "% punktow doby; rannych przy pracy " + _dInjured + ", zmarlych " + _dDead
                         + ", zbieglych przy pracy (519, " + s.PrisonLaborEscapePercent.ToString("0.###", inv) + "%/dobe) " + _dEsc
                         + (s.PrisonEscapeGoHome ? " (do domu " + _dEscHome + ", do puli " + _dEscPool + ", do karczmy " + _dEscTavern + ", znikneli " + _dEscLost + ")" : " (znikaja - Prison Escape Go Home wylaczone)")
                         + " | jedzenie lochow (Dungeon Prisoners Eat " + (s.DungeonPrisonersEat && _foodWired ? "tak" : "nie")
                         + "): twierdz " + _dEatSt + ", jencow " + _dEaters + ", zjadaja " + _dFood.ToString("0.0", inv) + " Food/dobe"
                         + " | glod lochu (D3): twierdz z Food 0 i jencami " + _dHungrySt + " (w tygodniu laski " + _dHungryGrace + "), z glodnymi robotnikami (praca x" + HungerShareText(s) + ") " + _dHungryWork
                         + ", zmarlych z glodu " + _dHungerDead + " | Perk509: " + Perk509.DayLine() + "; potkniecia " + err + ".");
            }
            catch (Exception e) { Log.Error("PrisonLabor509.Daily(linia)", e); }
        }
    }
}
