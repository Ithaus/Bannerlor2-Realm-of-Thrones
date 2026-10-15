using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// OBLEZENIA 2 (Jeff 15.10 "napraw oblezenia" - propozycja z punktami 1-4; punkt 4 - odsiecz OB5 bez zmian). Dane: test 6-letni b1cb26a8 (d0-767):
    /// AI chcialo szturmu 4183 razy, puszczone 38; machiny AI: trebusze 0, tarany 53, wieze 39; trafienia w mury 0; miasta 61 oblezen, 0 zdobytych;
    /// najdluzsze oblezenie 348.9 doby (castle_B8 Eastwatch).
    ///
    /// TB - TREBUSZE AI (klucz SiegeAiTrebuchets). Przyczyna zera trebuszy [K gra + P log]:
    ///   (a) plan losowany: DefaultSiegeEventModel.GetSiegeStrategyScore = RandomFloat, BesiegerCamp.ChangeSiegeStrategyIfNeeded raz na oblezenie - trebusze
    ///       sa tylko w WearOutDefenders (Ram 1, SiegeTower 1, Trebuchet 4; DefaultSiegeStrategyActionModel :269-281), PrepareAssault ma balisty i onagery,
    ///       BreachWalls onagery - z RBM one nie bija w mury (is_anti_personnel, BesiegerCamp.FindAttackableWallSectionWithHighestPriority :460);
    ///   (b) budowa po kolei: GetLogicalActionToCompleteEngineList (:151-181) stawia nowa machine tylko po gotowym obozie i gdy ZADNA rozstawiona nie jest
    ///       w budowie, w kolejnosci listy (taran, wieza, dopiero potem trebusze); SiegeEvent.ConstructionTick buduje jedna machine naraz;
    ///   (c) tempo: trebusz 40 osobodni (RBM), taran 12, wieza 16, przygotowania 24; postep = sqrt(ludzi) / osobodni x BK LongerSieges 0.5 x RB bez narzedzi
    ///       0.75 x SiegePacePercent 0.5 x OB4 0.5 / klasa (zamek 1, miasto 1.5, stolica 2.5) = 9.4% / 6.3% / 3.8% tempa gry. Oboz 3000 ludzi: pierwszy trebusz
    ///       w WearOutDefenders po ok. 18 (zamek) / 27 (miasto) / 45 (stolica) dobach, 1500 ludzi - 25 / 38 / 63; oblezenia trwaly mediana 9 dob, p90 25
    ///       (linie OB5 koniec, 234 oblezen), a oboz z taranem i wieza szturmuje (OB1) - trebusz nie zdazyl ani razu. Warunek dostepnosci trebusza dla AI
    ///       nie istnieje (lista strategii uzywa DefaultSiegeEngineTypes.Trebuchet wprost; GetAvailableAttackerRangedSiegeEngines tylko w menu gracza);
    ///       niszczenie przez machiny obroncow nie jest przyczyna zera (obroncy celuja tylko w machiny gotowe - IsActive, SiegeEvent :1234-1255;
    ///       licznik OB0 liczy ukonczone).
    ///   Zmiana: oboz AI pod miastem i stolica od pierwszej decyzji ma plan WearOutDefenders i DRUGA EKIPE: ekipa bliska stawia taran i wieze (lista gry),
    ///   ekipa daleka trebusze (do 4) - naraz, kazda w tempie gry dla swojej machiny (GetConstructionProgressPerHour z OB4, BK, RB). Postfiks planu
    ///   (GetLogicalActionForStrategy): gdy gra mowi Hold, bo druga kategoria jest w budowie - nowa machina wolnej ekipy (wolne miejsce albo nadmiarowa
    ///   machina daleka, jak gra). Prefiks + postfiks SiegeEvent.ConstructionTick: gra buduje pierwsza niegotowa machine, my pierwsza niegotowa DRUGIEJ
    ///   kategorii (tylko po gotowym obozie). Zamek jak dzis, chyba ze oboz stoi SiegeTrebuchetCastleDays (10) dob po gotowym obozie bez taranu i wiezy
    ///   (oboz 1000 ludzi ma oba po ok. 9.4 dobie) - wtedy to samo. Bez gracza (jego oboz), skryptu fabuly ROT i Innych.
    ///
    /// GL - GLOD OBLEZENIA (klucz SiegeStarvation). Przyczyna castle_B8 [P csv spichlerze-regiony / ludzie-regiony 2026-10-14_15-55-37, K gra]:
    ///   oblegal jeden oddzial Wolnych Ludzi o sile 4 (potem 0 - linie OB5), zapas 8863 spadal -60..-69 na dobe do 0 (doba 109395, 141. doba oblezenia);
    ///   zaloga 255 -> 110 (gra: ranni 10% na dobe tylko przy IsGarrisonStarving = FoodChange < -dobrobyt / 40, DefaultPartyHealingModel :132-146,
    ///   SettlementHelper :549; RB zabija 2-4 na dobe przy zapasie 0); glod zbil dobrobyt 2440 -> 0, wiec konsumpcja zmalala i bilans wrocil na PLUS
    ///   (+18..+86 na dobe przez ok. 70 dob: jedzenie z targu wliczane do zapasu - SoldItems BonusToFoodStores, DefaultSettlementFoodModel :82-91,
    ///   ItemConsumptionBehavior :61-71, BK ItemConsumptionPatch), zapas odrosl do ok. 1150 i stal (-4/d przy zalodze 110); milicja 250 gry nie gloduje
    ///   (glod tylko w zalodze). Gra nie ma kapitulacji - oblezenie konczy szturm, odejscie oblegajacych albo bitwa. Do tego zapasy BK sa ogromne
    ///   (gorny limit = 2 x ludnosc + 1000 x spichlerz, BK FoodStockPatch): przy starcie oblezen mediana 228 dob zamek, 458 miasto (p10 139 / 173).
    ///   Zmiana: (1) bilans jedzenia oblezonej twierdzy nigdy dodatni (postfiks Low-ostatni na CalculateTownFoodStocksChange czynnego modelu - ROT,
    ///   ktory dodaje town_EW4 / ROT_castle61 "ziemie wokol" takze w oblezeniu) - zapas tylko spada; (2) przy zapasie 0 codziennie SiegeStarveLossPercent
    ///   (5%) zalogi, milicji i ludzi w rosterze osady umiera (ranni najpierw; POLEGLI regionu pochodzenia jak smierc w oblezeniu poza bitwa - FDieSiege);
    ///   straty RB w tej twierdzy wtedy pominiete (prefiks na SiegeAttritionBehavior.OnDailyTickSettlement - ta sama przyczyna liczona raz); rany gry
    ///   (IsGarrisonStarving) zostaja - to slabosc, nie smierc; (3) garstka (< max(SiegeSurrenderMen 20, SiegeSurrenderShare 10% ludzi z poczatku oblezenia))
    ///   kapituluje: ludzie ida do lochu wodza obozu (FCaptured), twierdza pusta - droga gry "pusta warownia" (BesiegerCamp.StartingAssaultOnBesiegedSettlementIsLogical
    ///   :420-428 - obroncy 0 = "true"; OB1 przepuszcza pusta warownie), wodz od razu przelicza zamiar (RecalculateShortTermBehavior) - szturm bez obroncow
    ///   i zdobycie jak w grze (SiegeCompleted, lup, nadania). Bez kapitulacji: lordowie z ludzmi w murach, oboz niegotowy, oboz slabszy od reszty zalogi,
    ///   oblezenie gracza (szturmuje sam), gracz w murach, skrypt fabuly, bitwa w toku. Bez Innych (CrashScribe T2c) i twierdz Bialych Wedrowcow.
    ///   Recenzja: (a) obroncy w bitwie (szturm, wypad) - tej doby bez smierci z glodu (rostery w MapEvent; gra tak samo - PartyHealCampaignBehavior
    ///   tylko przy MapEvent == null); (b) dopisek ROT "ziemie wokol" (+100 town_EW4, +120 ROT_castle61 - ROT dodaje go takze w oblezeniu) odejmowany
    ///   w oblezeniu zawsze, nie tylko gdy bilans dodatni; (c) pusta twierdza po kapitulacji nie "kapituluje" drugi raz co dobe; (d) TB wylaczony w MCM
    ///   w trakcie sesji gasi tez druga ekipe obozow oznaczonych wczesniej.
    ///
    /// DR - DRABINY AI (klucz SiegeLadderOverwhelm, w SiegeWalls.Veto): oblezenie >= SiegeLadderMinDays (30 - dluzsze niz 9 na 10 oblezen, p90 25 dob)
    ///   i sila oblegajacych >= SiegeLadderMinRatio (6 = dwa razy 3:1 gry do rozpoczecia oblezenia) x obroncy w pelni (miara OB1) -> OB1 przepuszcza
    ///   decyzje gry (brama, mury, losowanie); bitwa wedlug SZ1-SZ4 (drabiny x2 mury). Gra sama chce szturmu tylko z co najmniej jedna gotowa machina
    ///   (GetNumberOfEquipmentsBuilt, BesiegerCamp :424) - "drabiny" = bez taranu i wiezy. Jeff 15.10 ("3. tak"): progi 21 dob i 4:1 (bylo 30 i 6).
    ///
    /// MB - MACHINA W BUDOWIE NIE WSTRZYMUJE SZTURMU (klucz SiegeEnginesInProgressDontHold; Jeff 15.10 "2. tak"). Przyczyna [K gra]: BesiegerCamp
    ///   .StartingAssaultOnBesiegedSettlementIsLogical :424-427 mnozy szanse szturmu przez min(3, gotowe) / 3 x (1 - 0.85 p^2), p = najwiekszy postep
    ///   NIEGOTOWEJ machiny + 0.25 x (5 - gotowe) (DefaultCombatSimulationModel.GetMaximumSiegeEquipmentProgress). Z gotowym taranem i wieza (2) trebusz
    ///   drugiej ekipy (TB) po ok. 1/3 budowy daje p >= 1.09 - kara <= 0 i gra nie szturmuje az do konca budowy (trebusz 40 osobodni, do 4 sztuk) -
    ///   w tescie F (d272) miasta 0 z ok. 17 mimo przewag 2.8-5.4:1. Bez zadnej gotowej machiny gra nie szturmuje nigdy (min(3, 0) = 0), wiec DR
    ///   (drabiny) nie mial czego przepuscic.
    ///   Zmiana: postfiks decyzji gry (z SiegeRansom.AssaultPost przy "false"), oboz AI po przygotowaniach (bez gracza, skryptu, Innych):
    ///   (a) gotowy taran i wieza albo wyrwa - szansa wzorem gry, ale p liczone tylko z taranow i wiez w budowie (machiny dystansowe - trebusze -
    ///   nie czekaja); (b) DR (LadderOk: >= SiegeLadderMinDays i sila >= SiegeLadderMinRatio x obroncy) - szansa 0.1 x s gry bez kary machin.
    ///   Losowanie raz na godzine i oboz, warunkowo tak, by razem z losowaniem gry szansa byla ta wyliczona; potem OB1 (brama, mury, DR) i okup jak zwykle.
    /// Wyjatek per obiekt = licznik potkniec (CLAUDE.md 7). Stan tylko w pamieci (po wczytaniu: licznik dob zamku i ludzie z poczatku oblezenia od wczytania).
    /// </summary>
    internal static class SiegeWalls2
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static float Now { get { return (float)CampaignTime.Now.ToDays; } }
        private static int Today { get { return (int)CampaignTime.Now.ToDays; } }
        private static string F(double v, string f) { return v.ToString(f, Inv); }
        private static string Nm(Settlement s) { try { return s != null && s.Name != null ? s.Name.ToString() : "-"; } catch { return "?"; } }
        private static string Hn(MobileParty mp) { try { return mp == null ? "-" : mp.LeaderHero != null ? mp.LeaderHero.Name.ToString() : mp.StringId; } catch { return "?"; } }
        private static double Clamp(double v, double lo, double hi) { return v < lo ? lo : v > hi ? hi : v; }

        // ------------------------------------------------------------ klucze
        internal static bool TrebOn { get { var s = Settings.Current; return s != null && s.SiegeAiTrebuchets; } }
        private static int CastleDays { get { var s = Settings.Current; return s == null ? 10 : (int)Clamp(s.SiegeTrebuchetCastleDays, 0, 60); } }
        internal static bool StarveOn { get { var s = Settings.Current; return s != null && s.SiegeStarvation; } }
        private static double LossPct { get { var s = Settings.Current; return s == null || float.IsNaN(s.SiegeStarveLossPercent) ? 5.0 : Clamp(s.SiegeStarveLossPercent, 0.0, 20.0); } }
        private static double SurShare { get { var s = Settings.Current; return s == null || float.IsNaN(s.SiegeSurrenderShare) ? 0.1 : Clamp(s.SiegeSurrenderShare, 0.0, 0.5); } }
        private static int SurMen { get { var s = Settings.Current; return s == null ? 20 : (int)Clamp(s.SiegeSurrenderMen, 0, 200); } }
        internal static bool LadderOn { get { var s = Settings.Current; return s != null && s.SiegeLadderOverwhelm; } }
        private static int LadderDays { get { var s = Settings.Current; return s == null ? 30 : (int)Clamp(s.SiegeLadderMinDays, 0, 364); } }
        private static double LadderRatio { get { var s = Settings.Current; return s == null || float.IsNaN(s.SiegeLadderMinRatio) ? 6.0 : Clamp(s.SiegeLadderMinRatio, 2.0, 20.0); } }
        private static bool LogOn { get { var s = Settings.Current; return s != null && s.SiegeWallsLog && s.LogEnabled; } }
        /// <summary>Lista gry WearOutDefenders: trebuszy (DefaultSiegeStrategyActionModel :273-278).</summary>
        private const int TrebNeed = 4;

        // ------------------------------------------------------------ potkniecia (postfiks bilansu jedzenia moze isc z watkow AI - pod zamkiem)
        private static readonly object Gate = new object();
        private static int _stumbles, _dStumbles;
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static void Stumble(string where, Exception e)
        {
            bool first;
            lock (Gate) { _stumbles++; _dStumbles++; first = _err.Add(where); }
            try { if (first) Log.Error("SiegeWalls2." + where, e); } catch { }
        }

        private static bool Und(MobileParty mp) { try { return mp != null && Undead.Party(mp); } catch { return false; } }
        /// <summary>Oblezenie z Innymi po ktorejkolwiek stronie albo twierdza Bialych Wedrowcow (ROT nie liczy im jedzenia) - jak SiegeWalls.Und.</summary>
        private static bool Und(MobileParty mp, Settlement st)
        {
            if (Und(mp)) return true;
            try { return st != null && st.OwnerClan != null && (ClanIncomeBook.IsUndeadClan(st.OwnerClan) || OthersRise.IsWhiteWalkerClan(st.OwnerClan)); } catch { return false; }
        }
        private static bool Scripted(MobileParty mp) { try { return mp != null && !mp.IsMainParty && mp.Ai != null && mp.Ai.DoNotMakeNewDecisions; } catch { return false; } }
        private static string Cls(Settlement st) { return st == null ? "-" : st.IsTown ? (SiegeWalls.IsCapital(st) ? "stolica" : "miasto") : "zamek"; }

        // ============================================================ TB: trebusze AI
        private sealed class Treb
        {
            public bool On; public float NextEval = -1f, PrepSeen = -1f, OnAt = -1f; public string Cls = "-", Why = "-";
            public int ParT, ParM, ParDone;
        }
        private static readonly Dictionary<SiegeEvent, Treb> _treb = new Dictionary<SiegeEvent, Treb>();
        private static int _marked;                                                 // obozy z druga ekipa (szybkie wyjscie z ticka budowy)
        private static int _dNewTown, _dNewCap, _dNewCastle, _dSwitch, _dParT, _dParM, _dParDone, _dTrebBuilt, _dTrebLost, _dUnmark;

        /// <summary>Postfiks GetLogicalActionForStrategy czynnego modelu planu (wolany co tick obu stron kazdego oblezenia): oboz AI pod miastem / stolica
        /// (zamek po CastleDays) - plan WearOutDefenders i druga ekipa. Szybkie wyjscie dla obroncow i przy wylaczonym kluczu.</summary>
        public static void ActionPost(ISiegeEventSide __0, ref SiegeStrategyActionModel.SiegeAction __1, ref SiegeEngineType __2, ref int __3, ref int __4)
        {
            if (__0 == null || __0.BattleSide != BattleSideEnum.Attacker || !TrebOn) return;
            try
            {
                var camp = __0 as BesiegerCamp;
                var ev = camp != null ? camp.SiegeEvent : null;
                if (ev == null) return;
                Treb t;
                if (!_treb.TryGetValue(ev, out t)) { t = new Treb(); _treb[ev] = t; }
                float now = Now;
                if (now >= t.NextEval) { t.NextEval = now + 1f / 24f; Eval(camp, ev, t, now); }
                if (!t.On) return;
                var cur = camp.SiegeStrategy;
                if (cur != DefaultSiegeStrategies.WearOutDefenders)
                {
                    if (cur == null || cur == DefaultSiegeStrategies.Custom || cur == DefaultSiegeStrategies.PreserveStrength) return;   // plan gracza / bez planu - nie ruszamy
                    camp.SetSiegeStrategy(DefaultSiegeStrategies.WearOutDefenders);   // ta sama droga co zmiana planu gracza w menu i SiegeWalls.TryBreach
                    _dSwitch++;
                    __1 = SiegeStrategyActionModel.SiegeAction.Hold; __2 = null; __3 = -1; __4 = -1;   // akcja tej tury liczona po starym planie - nastepna po nowym
                    return;
                }
                if (__1 != SiegeStrategyActionModel.SiegeAction.Hold || !camp.IsPreparationComplete) return;
                var se = camp.SiegeEngines;
                if (se == null) return;
                bool meleeBusy = false, rangedBusy = false;
                foreach (var e in se.DeployedSiegeEngines)
                {
                    if (e == null || e.SiegeEngine == null || e.Progress >= 1f) continue;
                    if (e.SiegeEngine.IsRanged) rangedBusy = true; else meleeBusy = true;
                }
                if (meleeBusy == rangedBusy) return;   // obie wolne - gra sama zdecydowala (lista pelna albo brak miejsca); obie zajete - czekaja
                if (meleeBusy)
                {
                    // ekipa daleka wolna: trebusze do 4 (lista gry WearOutDefenders)
                    int have;
                    se.DeployedSiegeEngineTypesCount.TryGetValue(DefaultSiegeEngineTypes.Trebuchet, out have);
                    if (have >= TrebNeed) return;
                    int slot = RangedSlot(se);
                    if (slot < 0) return;
                    __1 = SiegeStrategyActionModel.SiegeAction.ConstructNewSiegeEngine; __2 = DefaultSiegeEngineTypes.Trebuchet; __3 = slot; __4 = -1;
                    t.ParT++; _dParT++;
                }
                else
                {
                    // ekipa bliska wolna: taran, potem wieza (kolejnosc listy gry)
                    SiegeEngineType need = null; int c;
                    se.DeployedSiegeEngineTypesCount.TryGetValue(DefaultSiegeEngineTypes.Ram, out c);
                    if (c < 1) need = DefaultSiegeEngineTypes.Ram;
                    else { se.DeployedSiegeEngineTypesCount.TryGetValue(DefaultSiegeEngineTypes.SiegeTower, out c); if (c < 1) need = DefaultSiegeEngineTypes.SiegeTower; }
                    if (need == null) return;
                    int slot = -1;
                    var mel = se.DeployedMeleeSiegeEngines;
                    for (int i = 0; mel != null && i < mel.Length; i++) if (mel[i] == null) { slot = i; break; }
                    if (slot < 0) return;
                    __1 = SiegeStrategyActionModel.SiegeAction.ConstructNewSiegeEngine; __2 = need; __3 = slot; __4 = -1;
                    t.ParM++; _dParM++;
                }
            }
            catch (Exception e) { Stumble("ActionPost", e); }
        }

        /// <summary>Miejsce dla trebusza: wolne miejsce machiny dalekiej, inaczej nadmiarowa gotowa machina daleka spoza listy (gra: num2 w GetLogicalActionToCompleteEngineList
        /// - stara idzie do rezerwy przy DeploySiegeEngineAtIndex). -1 = brak.</summary>
        private static int RangedSlot(SiegeEvent.SiegeEnginesContainer se)
        {
            var r = se.DeployedRangedSiegeEngines;
            if (r == null) return -1;
            for (int i = 0; i < r.Length; i++) if (r[i] == null) return i;
            for (int i = 0; i < r.Length; i++)
                if (r[i] != null && r[i].SiegeEngine != DefaultSiegeEngineTypes.Trebuchet && r[i].IsActive && r[i].Hitpoints > 0f) return i;
            return -1;
        }

        private static void Engines(BesiegerCamp camp, out bool ram, out bool tower, out int treb, out int trebBuilding)
        {
            ram = tower = false; treb = trebBuilding = 0;
            var se = camp.SiegeEngines;
            if (se == null) return;
            foreach (var x in se.AllSiegeEngines())
            {
                if (x == null || x.SiegeEngine == null) continue;
                var t = x.SiegeEngine;
                if (t == DefaultSiegeEngineTypes.Trebuchet) { if (x.IsConstructed) treb++; else trebBuilding++; continue; }
                if (!x.IsConstructed) continue;
                if (t == DefaultSiegeEngineTypes.Ram || t == DefaultSiegeEngineTypes.ImprovedRam) ram = true;
                else if (t == DefaultSiegeEngineTypes.SiegeTower) tower = true;
            }
        }

        /// <summary>Raz na godzine na oboz: wykluczenia (gracz, skrypt, Inni - zdejmuja znacznik) i znacznik: miasto / stolica od razu, zamek po CastleDays dob
        /// od gotowego obozu bez taranu i wiezy.</summary>
        private static void Eval(BesiegerCamp camp, SiegeEvent ev, Treb t, float now)
        {
            var st = ev.BesiegedSettlement;
            var lp = camp.LeaderParty;
            bool excl = st == null || st.Town == null || !st.IsFortification || lp == null || lp.IsMainParty || Scripted(lp) || Und(lp, st);
            if (excl) { if (t.On) { t.On = false; _marked--; _dUnmark++; } return; }
            if (t.On) return;
            string why;
            if (st.IsTown) why = "od poczatku oblezenia";
            else
            {
                if (!camp.IsPreparationComplete) return;
                if (t.PrepSeen < 0f) t.PrepSeen = now;
                double after = now - t.PrepSeen;
                if (after < CastleDays) return;
                bool ram, tower; int tr, tb;
                Engines(camp, out ram, out tower, out tr, out tb);
                if (ram && tower) { t.NextEval = now + 1f; return; }   // zamek jak dzis
                why = F(after, "0.0") + " dob po gotowym obozie bez " + (ram ? "wiezy" : tower ? "taranu" : "taranu i wiezy") + " (prog " + CastleDays + ")";
            }
            t.On = true; _marked++; t.OnAt = now; t.Why = why; t.Cls = Cls(st);
            if (t.Cls == "stolica") _dNewCap++; else if (t.Cls == "miasto") _dNewTown++; else _dNewCastle++;
            if (LogOn) MarkLine(camp, ev, st, lp, t);
        }

        private static void MarkLine(BesiegerCamp camp, SiegeEvent ev, Settlement st, MobileParty lp, Treb t)
        {
            try
            {
                int men = 0;
                foreach (var pb in camp.GetInvolvedPartiesForEventType(MapEvent.BattleTypes.Siege)) if (pb != null) men += pb.NumberOfHealthyMembers;
                bool ram, tower; int tr, tb;
                Engines(camp, out ram, out tower, out tr, out tb);
                var cur = camp.SiegeStrategy;
                string cs = cur != null ? cur.StringId : "-";
                Log.Info("Trebusze AI (TB): dzien " + Today + " | osada " + st.StringId + " (" + Nm(st) + ", " + t.Cls + ") | wodz " + Hn(lp) + " (frakcja "
                         + (lp.MapFaction != null ? lp.MapFaction.StringId : "-") + "), ludzi " + men + " | powod: " + t.Why + " | strategia " + cs
                         + (cur == DefaultSiegeStrategies.WearOutDefenders ? " (bez zmiany)" : " -> " + DefaultSiegeStrategies.WearOutDefenders.StringId)
                         + " | oboz gotowy " + (camp.IsPreparationComplete ? "tak" : "nie") + ", oblezenie " + F(ev.SiegeStartTime.ElapsedDaysUntilNow, "0.0") + " dob"
                         + " | taran " + (ram ? "tak" : "nie") + ", wieza " + (tower ? "tak" : "nie") + ", trebusze " + tr + " (w budowie " + tb + ") | druga ekipa: tak.");
            }
            catch (Exception e) { Stumble("MarkLine", e); }
        }

        /// <summary>Prefiks SiegeEvent.ConstructionTick: maszyna, ktora gra buduje w tej turze (pierwsza rozstawiona niegotowa, nie w przestawianiu; przed gotowym
        /// obozem - zadna, gra buduje oboz). Tylko oboz ze znacznikiem TB.</summary>
        public static void BuildPre(SiegeEvent __instance, ISiegeEventSide __0, out SiegeEvent.SiegeEngineConstructionProgress __state)
        {
            __state = null;
            // recenzja: klucz wylaczony w MCM w trakcie sesji - obozy oznaczone wczesniej tez bez drugiej ekipy (wylaczony = b1cb26a8)
            if (_marked <= 0 || !TrebOn || __0 == null || __0.BattleSide != BattleSideEnum.Attacker) return;
            try
            {
                Treb t;
                if (__instance == null || !_treb.TryGetValue(__instance, out t) || !t.On) return;
                var se = __0.SiegeEngines;
                if (se == null) return;
                if (se.SiegePreparations != null && !se.SiegePreparations.IsActive) return;   // gra buduje oboz (ConstructionTick :1101-1104)
                foreach (var e in se.DeployedSiegeEngines)
                    if (e != null && !e.IsConstructed && !e.IsBeingRedeployed) { __state = e; return; }
            }
            catch (Exception e) { Stumble("BuildPre", e); }
        }

        /// <summary>Postfiks SiegeEvent.ConstructionTick: druga ekipa - pierwsza niegotowa machina DRUGIEJ kategorii (daleka / bliska) dostaje postep w tempie gry
        /// dla swojego typu (GetConstructionProgressPerHour x DeltaTime, jak ConstructionTick :1116-1124); gotowa - CreateSiegeObject (zdarzenie gry SiegeEngineBuilt).</summary>
        public static void BuildPost(SiegeEvent __instance, ISiegeEventSide __0, SiegeEvent.SiegeEngineConstructionProgress __state)
        {
            if (__state == null || __state.SiegeEngine == null || __instance == null || __0 == null) return;
            try
            {
                var se = __0.SiegeEngines;
                if (se == null) return;
                bool pickRanged = __state.SiegeEngine.IsRanged;
                SiegeEvent.SiegeEngineConstructionProgress other = null;
                foreach (var e in se.DeployedSiegeEngines)
                    if (e != null && e != __state && e.SiegeEngine != null && !e.IsConstructed && !e.IsBeingRedeployed && e.SiegeEngine.IsRanged != pickRanged) { other = e; break; }
                if (other == null) return;
                float rate = Campaign.Current.Models.SiegeEventModel.GetConstructionProgressPerHour(other.SiegeEngine, __instance, __0);
                float add = (float)(rate * CampaignTime.DeltaTime.ToHours);
                if (!(add > 0f) || float.IsInfinity(add)) return;
                other.SetProgress((float)Clamp(other.Progress + add, 0.0, 1.0));
                if (other.IsActive)
                {
                    __instance.CreateSiegeObject(other, __0);
                    _dParDone++;
                    Treb t;
                    if (_treb.TryGetValue(__instance, out t)) t.ParDone++;
                }
            }
            catch (Exception e) { Stumble("BuildPost", e); }
        }

        /// <summary>Zdarzenie gry SiegeEngineBuilt (takze z drugiej ekipy): trebusze AI zbudowane.</summary>
        internal static void OnEngineBuilt(SiegeEvent ev, BattleSideEnum side, SiegeEngineType type)
        {
            try
            {
                if (ev == null || type != DefaultSiegeEngineTypes.Trebuchet || side != BattleSideEnum.Attacker) return;
                var camp = ev.BesiegerCamp;
                if (camp == null || camp.LeaderParty == null || camp.LeaderParty.IsMainParty) return;
                _dTrebBuilt++;
            }
            catch (Exception e) { Stumble("OnEngineBuilt", e); }
        }

        /// <summary>Zdarzenie gry OnSiegeEngineDestroyed (strona, ktorej machine zniszczono): trebusze AI zniszczone przez obroncow.</summary>
        internal static void OnEngineDestroyed(MobileParty besieger, Settlement st, BattleSideEnum side, SiegeEngineType type)
        {
            try
            {
                if (type != DefaultSiegeEngineTypes.Trebuchet || side != BattleSideEnum.Attacker || besieger == null || besieger.IsMainParty) return;
                _dTrebLost++;
            }
            catch (Exception e) { Stumble("OnEngineDestroyed", e); }
        }

        // ============================================================ GL: glod oblezenia
        private static readonly TextObject _txtNoSupply = new TextObject("{=!}No supplies under siege");
        private sealed class Gl { public int Start = -1; public float HungerFrom = -1f; public int Surrenders; }
        private static readonly Dictionary<SiegeEvent, Gl> _gl = new Dictionary<SiegeEvent, Gl>();
        private static readonly HashSet<Settlement> _surrendered = new HashSet<Settlement>();   // twierdze po kapitulacji (do linii zdobycia)
        private static readonly HashSet<string> _dClampSt = new HashSet<string>();
        private static readonly HashSet<string> _dRotSt = new HashSet<string>();                 // recenzja: twierdze z odjetymi "ziemiami wokol" ROT w oblezeniu (doba)
        private static bool _rotFood;                                                            // czynny model jedzenia = ROTSettlementFoodModel (z EnsureHooks)
        private static readonly Dictionary<string, int> _dSurSkip = new Dictionary<string, int>(StringComparer.Ordinal);
        private static double _dClampSum, _dLongestHunger;
        private static string _dLongestHungerSt = "-";
        private static int _dBesieged, _dStarving, _dDead, _dSurr, _dSurrMen, _dRbSkip, _dNoPrison;

        /// <summary>Postfiks (ostatni) CalculateTownFoodStocksChange czynnego modelu jedzenia: oblezona twierdza - bilans nie wiekszy niz 0 (bez dostaw z zewnatrz,
        /// takze dopiskow ROT i jedzenia z targu ponad potrzebe). Moze isc z watkow AI - tylko odczyt i licznik pod zamkiem. Nie rzuca.</summary>
        public static void FoodPost(Town __0, ref ExplainedNumber __result)
        {
            try
            {
                if (__0 == null || !__0.IsUnderSiege || !StarveOn) return;
                var st = __0.Settlement;
                if (st == null) return;
                // recenzja: ROT (ROTSettlementFoodModel.CalculateTownFoodStocksChange, dec37 = a6dec) dopisuje "ziemie wokol" +100 town_EW4 i +120 ROT_castle61
                // BEZ warunku oblezenia (gra pomija ziemie wokol przy IsUnderSiege - DefaultSettlementFoodModel :68-81); sam uciety plus nie wystarcza, gdy bilans
                // z ta dostawa jest ujemny (twierdza dalej jadlaby 100-120 dziennie zza murow). Odejmujemy dokladnie dopisek ROT (Add w BaseNumber, bez czynnikow),
                // tylko gdy czynny model to ROT i ROT go dodal (nie dla rodu Bialych Wedrowcow bez krolestwa - ROT zwraca wtedy 0).
                if (_rotFood)
                {
                    float rot = st.StringId == "town_EW4" ? 100f : st.StringId == "ROT_castle61" ? 120f : 0f;
                    var cl = __0.MapFaction as Clan;
                    if (rot > 0f && !(cl != null && OthersRise.IsWhiteWalkerClan(cl)))
                    {
                        __result.Add(-rot, _txtNoSupply);
                        lock (Gate) { _dRotSt.Add(st.StringId); }
                    }
                }
                float v = __result.ResultNumber;
                if (!(v > 0f)) return;
                __result.Add(-v, _txtNoSupply);
                lock (Gate) { if (_dClampSt.Add(st.StringId)) _dClampSum += v; }
            }
            catch (Exception e) { Stumble("FoodPost", e); }
        }

        /// <summary>Prefiks RealisticBannerlord SiegeAttritionBehavior.OnDailyTickSettlement: przy zapasie 0 w oblezeniu smierc z glodu liczy GL (raz) - straty RB pominiete.</summary>
        public static bool RbSettlementPre(Settlement __0)
        {
            try
            {
                if (!StarveOn || __0 == null || __0.SiegeEvent == null || __0.Town == null || __0.Town.FoodStocks > 0f) return true;
                var camp = __0.SiegeEvent.BesiegerCamp;
                if (Und(camp != null ? camp.LeaderParty : null, __0)) return true;
                _dRbSkip++;
                return false;
            }
            catch { return true; }
        }

        internal static void OnSiegeStarted(SiegeEvent ev)
        {
            try
            {
                if (ev == null || ev.BesiegedSettlement == null || ev.BesiegedSettlement.Town == null) return;
                int h, w;
                _gl[ev] = new Gl { Start = MenOf(ev.BesiegedSettlement, out h, out w) };
            }
            catch (Exception e) { Stumble("OnSiegeStarted", e); }
        }

        internal static void OnSiegeEnded(SiegeEvent ev)
        {
            try
            {
                if (ev == null) return;
                Treb t;
                if (_treb.TryGetValue(ev, out t)) { if (t.On) _marked--; _treb.Remove(ev); }
                _gl.Remove(ev);
            }
            catch (Exception e) { Stumble("OnSiegeEnded", e); }
        }

        /// <summary>Rostery obroncow osady liczone przez glod i kapitulacje: zaloga, milicja, roster samej osady (SCA dosypuje tam ludzi).</summary>
        private static List<TroopRoster> Rosters(Settlement st)
        {
            var l = new List<TroopRoster>(3);
            var g = st.Town != null ? st.Town.GarrisonParty : null;
            if (g != null && g.IsActive && g.MemberRoster != null) l.Add(g.MemberRoster);
            var mc = st.MilitiaPartyComponent;
            var m = mc != null ? mc.MobileParty : null;
            if (m != null && m.IsActive && m.MemberRoster != null) l.Add(m.MemberRoster);
            if (st.Party != null && st.Party.MemberRoster != null && st.Party.MemberRoster.TotalManCount > 0) l.Add(st.Party.MemberRoster);
            return l;
        }

        private static int Regulars(TroopRoster r, out int wounded)
        {
            int n = 0; wounded = 0;
            for (int i = 0; i < r.Count; i++)
            {
                var e = r.GetElementCopyAtIndex(i);
                if (e.Character == null || e.Character.IsHero || e.Number <= 0 || Undead.Character(e.Character)) continue;
                n += e.Number; wounded += Math.Min(e.WoundedNumber, e.Number);
            }
            return n;
        }

        private static int MenOf(Settlement st, out int healthy, out int wounded)
        {
            int n = 0; healthy = 0; wounded = 0;
            foreach (var r in Rosters(st)) { int w; n += Regulars(r, out w); wounded += w; }
            healthy = n - wounded;
            return n;
        }

        /// <summary>Smierc z glodu w jednym rosterze: n szeregowych, ranni najpierw (od losowego typu, jak gra), potem zdrowi; POLEGLI regionu pochodzenia (FDieSiege).</summary>
        private static int Kill(TroopRoster r, int n)
        {
            if (r == null || n <= 0) return 0;
            var els = new List<TroopRosterElement>();
            for (int i = 0; i < r.Count; i++)
            {
                var e = r.GetElementCopyAtIndex(i);
                if (e.Character == null || e.Character.IsHero || e.Number <= 0 || Undead.Character(e.Character)) continue;
                els.Add(e);
            }
            if (els.Count == 0) return 0;
            int start = MBRandom.RandomInt(els.Count), left = n, killed = 0;
            var num = new int[els.Count]; var wnd = new int[els.Count];
            for (int i = 0; i < els.Count; i++) { num[i] = els[i].Number; wnd[i] = Math.Min(els[i].WoundedNumber, els[i].Number); }
            for (int pass = 0; pass < 2 && left > 0; pass++)
                for (int k = 0; k < els.Count && left > 0; k++)
                {
                    int i = (start + k) % els.Count;
                    int can = pass == 0 ? wnd[i] : num[i] - wnd[i];
                    int d = Math.Min(can, left);
                    if (d <= 0) continue;
                    r.AddToCounts(els[i].Character, -d, false, pass == 0 ? -d : 0);
                    num[i] -= d; if (pass == 0) wnd[i] -= d;
                    left -= d; killed += d;
                }
            if (killed > 0) { try { PeopleBook.DieShares(PeopleBook.Take(r, killed, PeopleBook.FDieSiege), PeopleBook.FDieSiege); } catch (Exception e) { Stumble("Kill.PeopleBook", e); } }
            return killed;
        }

        /// <summary>Kapitulacja jednego rosteru: szeregowi (zdrowi i ranni) do lochu wodza obozu (FCaptured, jak jency z bitwy). Bohaterowie i Inni zostaja.</summary>
        private static int Surrender(TroopRoster r, TroopRoster prison)
        {
            if (r == null || prison == null) return 0;
            var els = new List<TroopRosterElement>();
            for (int i = 0; i < r.Count; i++)
            {
                var e = r.GetElementCopyAtIndex(i);
                if (e.Character == null || e.Character.IsHero || e.Number <= 0 || Undead.Character(e.Character)) continue;
                els.Add(e);
            }
            var model = Campaign.Current.Models.BattleRewardModel;
            int moved = 0;
            foreach (var e in els)
            {
                if (model != null && !model.CanTroopBeTakenPrisoner(e.Character)) { _dNoPrison += e.Number; continue; }
                int n = e.Number, w = Math.Min(e.WoundedNumber, n);
                PeopleBook.Shares org = null;
                try { if (PeopleBook.OriginLive) org = PeopleBook.Take(r, n, PeopleBook.FCaptured); } catch { org = null; }
                r.AddToCounts(e.Character, -n, false, -w);
                prison.AddToCounts(e.Character, n, false, w);
                if (org != null) { try { PeopleBook.Give(prison, org, PeopleBook.FCaptured); } catch { } }
                moved += n;
            }
            return moved;
        }

        /// <summary>Twierdza, ktorej zaloga skapitulowala z glodu, a szturmu (pustej warowni) jeszcze nie bylo - SiegeWalls.Veto nie trzyma jej w przerwie OB2.</summary>
        internal static bool Surrendered(Settlement st) { return st != null && _surrendered.Count > 0 && _surrendered.Contains(st); }

        /// <summary>Recenzja: obroncy osady w bitwie (szturm, wypad, bitwa pod murami) - rostery w MapEvent (MapEventParty liczy straty na rosterze), wiec glod
        /// i kapitulacja czekaja do konca bitwy; tak samo gra - PartyHealCampaignBehavior.TryHealOrWoundParty tylko przy MapEvent == null.</summary>
        private static bool InBattle(Settlement st)
        {
            if (st.Party != null && st.Party.MapEvent != null) return true;
            var g = st.Town != null ? st.Town.GarrisonParty : null;
            if (g != null && g.MapEvent != null) return true;
            var mc = st.MilitiaPartyComponent;
            var m = mc != null ? mc.MobileParty : null;
            return m != null && m.MapEvent != null;
        }

        private static void StarveDaily()
        {
            var mgr = Campaign.Current != null ? Campaign.Current.SiegeEventManager : null;
            if (mgr == null || mgr.SiegeEvents == null) return;
            var list = new List<SiegeEvent>(mgr.SiegeEvents);
            float now = Now;
            foreach (var ev in list)
            {
                try { StarveOne(ev, now); }
                catch (Exception e) { Stumble("StarveOne", e); }
            }
        }

        private static void StarveOne(SiegeEvent ev, float now)
        {
            var st = ev != null ? ev.BesiegedSettlement : null;
            if (st == null || st.Town == null || !st.IsFortification || st.SiegeEvent != ev) return;
            var camp = ev.BesiegerCamp;
            var lp = camp != null ? camp.LeaderParty : null;
            if (Und(lp, st)) return;                                                   // Inni - CrashScribe T2c; twierdze Bialych Wedrowcow nie jedza
            _dBesieged++;
            Gl g;
            if (!_gl.TryGetValue(ev, out g)) { g = new Gl(); _gl[ev] = g; }
            int h, w;
            int n0 = MenOf(st, out h, out w);
            if (g.Start < 0) g.Start = n0;                                             // po wczytaniu: stan pierwszej doby
            if (st.Town.FoodStocks > 0f) { g.HungerFrom = -1f; return; }
            _dStarving++;
            if (g.HungerFrom < 0f) g.HungerFrom = now;
            double days = now - g.HungerFrom;
            if (days > _dLongestHunger) { _dLongestHunger = days; _dLongestHungerSt = st.StringId; }
            bool battle = InBattle(st);                                                // recenzja: rostery w bitwie - bez smierci tej doby (jak gra)
            double pct = battle ? 0.0 : LossPct;
            double p = pct / 100.0;
            int dead = 0;
            if (p > 0.0)
                foreach (var r in Rosters(st)) { int rw; dead += Kill(r, MBRandom.RoundRandomized((float)(p * Regulars(r, out rw)))); }
            _dDead += dead;
            int h1, w1;
            int left = MenOf(st, out h1, out w1);
            int thr = Math.Max(SurMen, (int)Math.Ceiling(SurShare * g.Start));
            string res = battle ? "glod trwa (bitwa w toku - bez smierci dzis)" : "glod trwa";
            // recenzja: twierdza juz skapitulowala i jest pusta - czeka na zajecie (bez drugiej "kapitulacji" z 0 jencow co dobe)
            if (left == 0 && _surrendered.Contains(st)) res = "po kapitulacji - pusta, czeka na zajecie";
            else if (left < thr) res = TrySurrender(st, camp, lp, left, g);
            if (!LogOn) return;
            try
            {
                int campMen = 0;
                if (camp != null) foreach (var pb in camp.GetInvolvedPartiesForEventType(MapEvent.BattleTypes.Siege)) if (pb != null) campMen += pb.NumberOfHealthyMembers;
                Log.Info("Glod oblezenia (GL): dzien " + Today + " | osada " + st.StringId + " (" + Nm(st) + ", " + Cls(st) + ") | oblegajacy " + (lp != null && lp.MapFaction != null ? lp.MapFaction.StringId : "-")
                         + ", wodz " + Hn(lp) + ", ludzi " + campMen + ", oboz gotowy " + (camp != null && camp.IsPreparationComplete ? "tak" : "nie") + ", oblezenie " + F(ev.SiegeStartTime.ElapsedDaysUntilNow, "0.0") + " dob"
                         + " | glod od " + F(days, "0.0") + " dob | zaloga i milicja: bylo " + n0 + " (zdrowi " + h + ", ranni " + w + "), zmarli dzis " + dead + " (" + F(pct, "0.0") + "%), zostalo " + left
                         + " | na starcie oblezenia " + g.Start + ", prog kapitulacji " + thr + " | wynik: " + res + ".");
            }
            catch (Exception e) { Stumble("StarveLine", e); }
        }

        private static string TrySurrender(Settlement st, BesiegerCamp camp, MobileParty lp, int left, Gl g)
        {
            string why = null;
            if (camp == null || lp == null) why = "brak obozu";
            else if (lp.IsMainParty) why = "oblezenie gracza";
            else if (Scripted(lp)) why = "skrypt fabuly";
            else if (MobileParty.MainParty != null && MobileParty.MainParty.CurrentSettlement == st) why = "gracz w murach";
            else if (!camp.IsPreparationComplete) why = "oboz niegotowy";
            else if (InBattle(st) || lp.MapEvent != null) why = "bitwa w toku";
            else
            {
                int lords = 0, lordMen = 0;
                foreach (var mp in st.Parties)
                {
                    if (mp == null || !mp.IsActive || !mp.IsLordParty || mp.Aggressiveness <= 0.01f || mp.MemberRoster == null) continue;
                    int hm = mp.MemberRoster.TotalHealthyCount;
                    if (hm > 0) { lords++; lordMen += hm; }
                }
                if (lords > 0) why = "lordowie w murach";
                else
                {
                    int campMen = 0;
                    foreach (var pb in camp.GetInvolvedPartiesForEventType(MapEvent.BattleTypes.Siege)) if (pb != null) campMen += pb.NumberOfHealthyMembers;
                    if (campMen <= left) why = "oboz slabszy";
                }
            }
            if (why != null)
            {
                int c; _dSurSkip.TryGetValue(why, out c); _dSurSkip[why] = c + 1;
                return "kapitulacja wstrzymana (" + why + ")";
            }
            int moved = 0;
            var prison = lp.PrisonRoster;
            foreach (var r in Rosters(st)) moved += Surrender(r, prison);
            g.Surrenders++; _dSurr++; _dSurrMen += moved;
            _surrendered.Add(st);
            try { lp.RecalculateShortTermBehavior(); } catch (Exception e) { Stumble("Surrender.Recalculate", e); }   // pusta warownia: decyzja gry o szturmie od razu
            try
            {
                if (st.OwnerClan != null && st.OwnerClan == Clan.PlayerClan)
                    Log.Player("The starving garrison of " + Nm(st) + " has laid down its arms before " + Hn(lp) + ".", true);
            }
            catch { }
            int h, w;
            return "KAPITULACJA - " + moved + " jencow do " + Hn(lp) + ", w murach zostalo " + MenOf(st, out h, out w);
        }

        // ============================================================ DR: drabiny AI przy miazdzacej przewadze (z SiegeWalls.Veto)
        private static readonly HashSet<SiegeEvent> _drLogged = new HashSet<SiegeEvent>();
        private static int _dDrGate, _dDrWalls, _dDrRoll;

        /// <summary>Warunek DR: oblezenie >= LadderDays i sila oblegajacych >= LadderRatio x obroncy w pelni (miara OB1: max(gra, w pelni)).</summary>
        internal static bool LadderOk(BesiegerCamp camp, SiegeWalls.Meas m)
        {
            try
            {
                if (!LadderOn || camp == null || camp.SiegeEvent == null || m == null || m.Empty) return false;
                if (camp.SiegeEvent.SiegeStartTime.ElapsedDaysUntilNow < LadderDays) return false;
                double def = Math.Max(m.Def, m.Def0);
                return def > 0.0 && m.Att >= LadderRatio * def;
            }
            catch (Exception e) { Stumble("LadderOk", e); return false; }
        }

        /// <summary>DR przepuscil decyzje, ktora OB1 by wstrzymal ("brama" / "mury" / "losowanie"): licznik i linia (raz na oblezenie i dobe).</summary>
        internal static void LadderPass(BesiegerCamp camp, SiegeWalls.Meas m, string would)
        {
            try
            {
                if (would == "brama") _dDrGate++; else if (would == "mury") _dDrWalls++; else _dDrRoll++;
                var ev = camp.SiegeEvent;
                if (!LogOn || ev == null || !_drLogged.Add(ev)) return;
                var st = ev.BesiegedSettlement;
                var lp = camp.LeaderParty;
                double def = Math.Max(m.Def, m.Def0);
                Log.Info("Drabiny AI (DR): dzien " + Today + " | osada " + st.StringId + " (" + Nm(st) + ", " + Cls(st) + ") | wodz " + Hn(lp) + " (frakcja "
                         + (lp != null && lp.MapFaction != null ? lp.MapFaction.StringId : "-") + ") | oblezenie " + F(ev.SiegeStartTime.ElapsedDaysUntilNow, "0.0") + " dob (prog " + LadderDays + ")"
                         + " | sila oblegajacy " + F(m.Att, "0") + " / obroncy " + F(def, "0") + " = " + F(def > 0 ? m.Att / def : 0.0, "0.00") + " (prog " + F(LadderRatio, "0.0") + ")"
                         + " | taran " + (m.Ram ? "tak" : "nie") + ", wieza " + (m.Tower ? "tak" : "nie") + ", wyrwa " + (m.Gap ? "tak" : "nie") + ", HP murow " + F(m.Hp * 100.0, "0") + "%, s OB1 " + F(m.S, "0.000")
                         + " | OB1 wstrzymalby: " + would + " | szturm przepuszczony.");
            }
            catch (Exception e) { Stumble("LadderPass", e); }
        }

        // ============================================================ MB: machina w budowie nie wstrzymuje szturmu (Jeff 15.10 "2. tak", "3. tak")
        internal static bool UnblockOn { get { var s = Settings.Current; return s != null && s.SiegeEnginesInProgressDontHold; } }
        private static readonly Dictionary<SiegeEvent, int> _unHour = new Dictionary<SiegeEvent, int>();
        private static readonly HashSet<SiegeEvent> _unLogged = new HashSet<SiegeEvent>();
        private static int _dUnEng, _dUnLad, _dUnRolls;

        /// <summary>Z SiegeRansom.AssaultPost przy "false" gry. true = szturm (dalej OB1 i okup). Szybkie wyjscie: klucz, oboz niegotowy, brak bramy i DR.</summary>
        internal static bool Unblock(BesiegerCamp camp)
        {
            try
            {
                if (!UnblockOn || camp == null) return false;
                var ev = camp.SiegeEvent;
                var st = ev != null ? ev.BesiegedSettlement : null;
                if (st == null || st.Town == null || !st.IsFortification || !camp.IsPreparationComplete) return false;
                var lp = camp.LeaderParty;
                if (lp == null || lp.IsMainParty || Scripted(lp) || Und(lp, st)) return false;
                var side = ev.GetSiegeEventSide(BattleSideEnum.Attacker);
                if (side == null || side.SiegeEngines == null) return false;
                bool ram = false, tower = false; double prog = 0.0;
                foreach (var x in side.SiegeEngines.AllSiegeEngines())
                {
                    if (x == null || x.SiegeEngine == null) continue;
                    var t = x.SiegeEngine;
                    if (x.IsConstructed) { if (t == DefaultSiegeEngineTypes.Ram || t == DefaultSiegeEngineTypes.ImprovedRam) ram = true; else if (t == DefaultSiegeEngineTypes.SiegeTower) tower = true; }
                    else if (!t.IsRanged && x.Progress > prog) prog = x.Progress;      // taran / wieza w budowie - kara gry zostaje, trebusze nie
                }
                bool eng = (ram && tower) || SiegeWalls.WallHpMin(st) <= 0.0;
                bool lad = LadderOn && ev.SiegeStartTime.ElapsedDaysUntilNow >= LadderDays;
                if (!eng && !lad) return false;
                if (SiegeWalls.InRepelPause(ev)) return false;                           // przerwa po odparciu (OB2) - Veto i tak wstrzyma
                int hour = (int)CampaignTime.Now.ToHours, last;
                if (_unHour.TryGetValue(ev, out last) && last == hour) return false;       // jedno losowanie na godzine i oboz
                _unHour[ev] = hour;
                var m = SiegeWalls.Measure(camp);
                if (m.Empty || m.SGame <= 1.0) return false;                             // gra: sila / (obroncy x przewaga^w) <= 1 - bez szturmu
                double pGame = m.PGame, pt = 0.0; string why = null;                      // pGame: szansa gry tej chwili (z kara machin w budowie)
                if (eng)
                {
                    double p = prog + 0.25 * (5 - m.Eq);
                    double pen = Math.Min(3, m.Eq) / 3.0 * (1.0 - 0.85 * p * p);
                    double pe = pen > 0 ? Clamp(0.1 * m.SGame * pen, 0.0, 1.0) : 0.0;
                    if (pe > pt) { pt = pe; why = "machiny"; }
                }
                if (lad && LadderOk(camp, m))
                {
                    double pl = Clamp(0.1 * m.SGame, 0.0, 1.0);
                    if (pl > pt) { pt = pl; why = "drabiny"; }
                }
                if (why == null || pGame >= 1.0 || pt <= pGame) return false;            // kara gry nie byla przeszkoda
                _dUnRolls++;
                if (MBRandom.RandomFloat >= (pt - pGame) / (1.0 - pGame)) return false;  // razem z losowaniem gry: szansa pt
                if (why == "machiny") _dUnEng++; else _dUnLad++;
                if (LogOn && _unLogged.Add(ev))
                {
                    double def = Math.Max(m.Def, m.Def0);
                    Log.Info("Szturm bez czekania na machiny (MB): dzien " + Today + " | osada " + st.StringId + " (" + Nm(st) + ", " + Cls(st) + ") | wodz " + Hn(lp) + " (frakcja "
                             + (lp.MapFaction != null ? lp.MapFaction.StringId : "-") + ") | oblezenie " + F(ev.SiegeStartTime.ElapsedDaysUntilNow, "0.0") + " dob | powod: " + why
                             + " | taran " + (m.Ram ? "tak" : "nie") + ", wieza " + (m.Tower ? "tak" : "nie") + ", wyrwa " + (m.Gap ? "tak" : "nie") + ", gotowe machiny " + m.Eq
                             + ", taran / wieza w budowie " + F(prog * 100.0, "0") + "%, kara gry " + F(m.Pen, "0.000") + " | sila oblegajacy " + F(m.Att, "0") + " / obroncy " + F(def, "0")
                             + " = " + F(def > 0 ? m.Att / def : 0.0, "0.00") + ", s gry " + F(m.SGame, "0.000") + " | szansa gry " + F(pGame, "0.000") + " -> " + F(pt, "0.000") + " | dalej OB1 i okup.");
                }
                return true;
            }
            catch (Exception e) { Stumble("Unblock", e); return false; }
        }

        // ============================================================ zdobycia (pomiar celu: miasta, stolice, zamki)
        private static int _dCaptTown, _dCaptCap, _dCaptCastle, _dCaptAfterSurr;

        /// <summary>Nasluch MapEventEnded (z SiegeWalls.OnMapEventEnded, przed jego wyjsciem): szturm / bitwa pod murami wygrana przez oblegajacych = zdobycie
        /// (gra wola SiegeCompleted po tym zdarzeniu - FinalizeEventAux). Linia "Oblezenia 2 (zdobycie)". Nie rzuca.</summary>
        internal static void OnMapEventEnded(MapEvent me)
        {
            try
            {
                if (me == null || !(me.IsSiegeAssault || me.IsSiegeOutside) || me.WinningSide != BattleSideEnum.Attacker) return;
                var st = me.MapEventSettlement;
                if (st == null || !st.IsFortification || st.Town == null) return;
                string cls = Cls(st);
                if (cls == "stolica") _dCaptCap++; else if (cls == "miasto") _dCaptTown++; else _dCaptCastle++;
                bool surr = _surrendered.Remove(st);
                if (surr) _dCaptAfterSurr++;
                if (!LogOn) return;
                var ev = st.SiegeEvent;
                var lpb = me.AttackerSide != null ? me.AttackerSide.LeaderParty : null;
                var lp = lpb != null && lpb.IsMobile ? lpb.MobileParty : null;
                Log.Info("Oblezenia 2 (zdobycie): dzien " + Today + " | osada " + st.StringId + " (" + Nm(st) + ", " + cls + ") | oblegajacy " + (lp != null && lp.MapFaction != null ? lp.MapFaction.StringId : "-")
                         + ", wodz " + Hn(lp) + " | gracz " + (me.IsPlayerMapEvent ? "tak" : "nie") + " | oblezenie " + (ev != null ? F(ev.SiegeStartTime.ElapsedDaysUntilNow, "0.0") : "-") + " dob"
                         + " | bitwa " + (me.IsSiegeAssault ? "szturm" : "pod murami") + " | po kapitulacji z glodu " + (surr ? "tak" : "nie") + ".");
            }
            catch (Exception e) { Stumble("OnMapEventEnded", e); }
        }

        // ============================================================ doba
        internal static void Daily()
        {
            try
            {
                if (Campaign.Current == null) return;
                // porzadek pamieci: oblezenia, ktorych juz nie ma
                List<SiegeEvent> dead = null;
                foreach (var kv in _treb) if (kv.Key == null || kv.Key.BesiegedSettlement == null || kv.Key.BesiegedSettlement.SiegeEvent != kv.Key) { if (dead == null) dead = new List<SiegeEvent>(); dead.Add(kv.Key); }
                if (dead != null) foreach (var ev in dead) _treb.Remove(ev);
                dead = null;
                foreach (var kv in _gl) if (kv.Key == null || kv.Key.BesiegedSettlement == null || kv.Key.BesiegedSettlement.SiegeEvent != kv.Key) { if (dead == null) dead = new List<SiegeEvent>(); dead.Add(kv.Key); }
                if (dead != null) foreach (var ev in dead) _gl.Remove(ev);
                dead = null;
                foreach (var kv in _unHour) if (kv.Key == null || kv.Key.BesiegedSettlement == null || kv.Key.BesiegedSettlement.SiegeEvent != kv.Key) { if (dead == null) dead = new List<SiegeEvent>(); dead.Add(kv.Key); }
                if (dead != null) foreach (var ev in dead) _unHour.Remove(ev);
                _surrendered.RemoveWhere(s => s == null || s.SiegeEvent == null);
                int mk = 0, mTown = 0, mCap = 0, mCastle = 0;
                foreach (var kv in _treb) if (kv.Value.On) { mk++; if (kv.Value.Cls == "stolica") mCap++; else if (kv.Value.Cls == "miasto") mTown++; else mCastle++; }
                _marked = mk;
                if (StarveOn) StarveDaily();
                if (!LogOn) return;
                int going = 0; double longest = 0; string longestSt = "-"; var over = new List<string>();
                var mgr = Campaign.Current.SiegeEventManager;
                if (mgr != null && mgr.SiegeEvents != null)
                    foreach (var ev in mgr.SiegeEvents)
                    {
                        var st = ev != null ? ev.BesiegedSettlement : null;
                        if (st == null || !st.IsFortification || st.SiegeEvent != ev) continue;
                        going++;
                        double age = ev.SiegeStartTime.ElapsedDaysUntilNow;
                        if (age > longest) { longest = age; longestSt = st.StringId; }
                        if (age > 150.0) over.Add(st.StringId + " " + F(age, "0") + " dob");
                    }
                var sb = new StringBuilder(1400);
                sb.Append("Oblezenia 2 (doba): dzien ").Append(Today)
                  .Append(" | klucze: trebusze ").Append(TrebOn ? "tak" : "nie").Append(" (zamek po ").Append(CastleDays).Append(" dob), glod ").Append(StarveOn ? "tak" : "nie")
                  .Append(" (").Append(F(LossPct, "0.0")).Append("%/d, kapitulacja < ").Append(F(SurShare * 100.0, "0")).Append("% / < ").Append(SurMen).Append("), drabiny ")
                  .Append(LadderOn ? "tak" : "nie").Append(" (>= ").Append(LadderDays).Append(" dob, >= ").Append(F(LadderRatio, "0.0")).Append(" : 1)")
                  .Append(" | TB: obozy z trebuszami ").Append(mk).Append(" (miasta ").Append(mTown).Append(", stolice ").Append(mCap).Append(", zamki ").Append(mCastle)
                  .Append("), nowe dzis: miasta ").Append(_dNewTown).Append(", stolice ").Append(_dNewCap).Append(", zamki ").Append(_dNewCastle)
                  .Append(", zmiany strategii ").Append(_dSwitch).Append(", zdjete (gracz, skrypt, Inni) ").Append(_dUnmark)
                  .Append("; druga ekipa: zaczete trebusze ").Append(_dParT).Append(", taran / wieza ").Append(_dParM).Append(", ukonczone ").Append(_dParDone)
                  .Append("; trebusze AI zbudowane ").Append(_dTrebBuilt).Append(", zniszczone ").Append(_dTrebLost)
                  .Append(" | GL: oblezone twierdze ").Append(_dBesieged).Append(", z zapasem 0: ").Append(_dStarving)
                  .Append(" (najdluzej ").Append(F(_dLongestHunger, "0.0")).Append(" dob glodu - ").Append(_dLongestHungerSt).Append(')');
                int nClamp; double sClamp;
                lock (Gate) { nClamp = _dClampSt.Count; sClamp = _dClampSum; }
                sb.Append(", bilans dodatni uciety: ").Append(nClamp).Append(" twierdz (+").Append(F(sClamp, "0.0")).Append("/d)")
                  .Append(", zmarli z glodu ").Append(_dDead).Append(", kapitulacje ").Append(_dSurr).Append(" (jencow ").Append(_dSurrMen).Append(", bez niewoli ").Append(_dNoPrison).Append(')')
                  .Append(", wstrzymane: ");
                if (_dSurSkip.Count == 0) sb.Append('0');
                else { bool f1 = true; foreach (var kv in _dSurSkip) { if (!f1) sb.Append(", "); sb.Append(kv.Key).Append(' ').Append(kv.Value); f1 = false; } }
                int nRot; lock (Gate) { nRot = _dRotSt.Count; }
                sb.Append(", straty RB pominiete ").Append(_dRbSkip).Append(", ziemie wokol ROT odjete ").Append(nRot)
                  .Append(" | DR: przepuszczone ").Append(_dDrGate + _dDrWalls + _dDrRoll).Append(" (brama ").Append(_dDrGate).Append(", mury ").Append(_dDrWalls).Append(", losowanie ").Append(_dDrRoll).Append(')')
                  .Append(" | MB: ").Append(UnblockOn ? "tak" : "nie").Append(", szturmy bez czekania: machiny ").Append(_dUnEng).Append(", drabiny ").Append(_dUnLad).Append(" (losowan ").Append(_dUnRolls).Append(')')
                  .Append(" | zdobyte dzis: miasta ").Append(_dCaptTown).Append(", stolice ").Append(_dCaptCap).Append(", zamki ").Append(_dCaptCastle).Append(" (po kapitulacji ").Append(_dCaptAfterSurr).Append(')')
                  .Append(" | oblezenia trwajace ").Append(going).Append(", najdluzsze ").Append(F(longest, "0.0")).Append(" dob (").Append(longestSt).Append("), ponad 150 dob: ")
                  .Append(over.Count == 0 ? "-" : string.Join(", ", over.ToArray()))
                  .Append(" | w pamieci: obozy ").Append(_treb.Count).Append(", glod ").Append(_gl.Count)
                  .Append(" | potkniecia ").Append(_dStumbles).Append(" (od wczytania ").Append(_stumbles).Append(").");
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally { ZeroDay(); }
        }

        private static void ZeroDay()
        {
            _dNewTown = _dNewCap = _dNewCastle = _dSwitch = _dParT = _dParM = _dParDone = _dTrebBuilt = _dTrebLost = _dUnmark = 0;
            _dBesieged = _dStarving = _dDead = _dSurr = _dSurrMen = _dRbSkip = _dNoPrison = 0;
            _dLongestHunger = 0; _dLongestHungerSt = "-"; _dSurSkip.Clear();
            _dDrGate = _dDrWalls = _dDrRoll = 0; _drLogged.Clear();
            _dUnEng = _dUnLad = _dUnRolls = 0; _unLogged.Clear();
            _dCaptTown = _dCaptCap = _dCaptCastle = _dCaptAfterSurr = 0;
            lock (Gate) { _dClampSt.Clear(); _dRotSt.Clear(); _dClampSum = 0; _dStumbles = 0; }
        }

        /// <summary>Z SiegeWalls.Reset (konstruktor ArmouryBehavior: nowa kampania i kazde wczytanie).</summary>
        internal static void Reset()
        {
            _treb.Clear(); _gl.Clear(); _surrendered.Clear(); _marked = 0; _unHour.Clear();
            ZeroDay();
            lock (Gate) { _stumbles = 0; _err.Clear(); }
        }

        // ============================================================ latki w kampanii (z SiegeWalls.EnsureHooks, raz na proces)
        private static string _hookAction = "nie zakladana", _hookBuild = "nie zakladana", _hookFood = "nie zakladana", _hookRb = "nie zakladana";

        private static string Wired(System.Reflection.MethodBase m, string name, bool prefix)
        {
            try
            {
                var info = m != null ? Harmony.GetPatchInfo(m) : null;
                var l = info == null ? null : prefix ? info.Prefixes : info.Postfixes;
                if (l != null)
                    foreach (var p in l)
                        if (p != null && p.PatchMethod != null && p.PatchMethod.DeclaringType == typeof(SiegeWalls2) && p.PatchMethod.Name == name) return "wpieta";
            }
            catch { }
            return "BRAK (latka nie weszla - linia 'Szczelnosc (164a)')";
        }

        internal static void EnsureHooks()
        {
            try
            {
                var sm = Campaign.Current != null && Campaign.Current.Models != null ? Campaign.Current.Models.SiegeStrategyActionModel : null;
                var mt = AccessTools.Method(sm != null ? sm.GetType() : typeof(DefaultSiegeStrategyActionModel), "GetLogicalActionForStrategy");
                if (mt == null) _hookAction = "BRAK (GetLogicalActionForStrategy)";
                else
                {
                    Seal31.Patch("oblezenia 2: plan machin AI (" + mt.DeclaringType.Name + ".GetLogicalActionForStrategy)", mt, typeof(SiegeWalls2), null, nameof(ActionPost), null, Priority.Normal, Priority.Low);
                    _hookAction = Wired(mt, nameof(ActionPost), false) + " (" + mt.DeclaringType.FullName + ")";
                }
            }
            catch (Exception e) { _hookAction = "BRAK (blad: " + e.Message + ")"; Stumble("EnsureHooks.Action", e); }
            try
            {
                var mc = AccessTools.Method(typeof(SiegeEvent), "ConstructionTick", new[] { typeof(ISiegeEventSide) });
                if (mc == null) _hookBuild = "BRAK (SiegeEvent.ConstructionTick)";
                else
                {
                    Seal31.Patch("oblezenia 2: druga ekipa budowy (SiegeEvent.ConstructionTick)", mc, typeof(SiegeWalls2), nameof(BuildPre), nameof(BuildPost), null, Priority.Normal, Priority.Low);
                    string a = Wired(mc, nameof(BuildPre), true), b = Wired(mc, nameof(BuildPost), false);
                    _hookBuild = a == "wpieta" && b == "wpieta" ? "wpieta" : "prefiks " + a + ", postfiks " + b;
                }
            }
            catch (Exception e) { _hookBuild = "BRAK (blad: " + e.Message + ")"; Stumble("EnsureHooks.Build", e); }
            try
            {
                var fm = Campaign.Current != null && Campaign.Current.Models != null ? Campaign.Current.Models.SettlementFoodModel : null;
                var mf = AccessTools.Method(fm != null ? fm.GetType() : typeof(DefaultSettlementFoodModel), "CalculateTownFoodStocksChange", new[] { typeof(Town), typeof(bool), typeof(bool) });
                if (mf == null) _hookFood = "BRAK (CalculateTownFoodStocksChange)";
                else
                {
                    Seal31.Patch("oblezenia 2: bilans jedzenia w oblezeniu (" + mf.DeclaringType.Name + ".CalculateTownFoodStocksChange)", mf, typeof(SiegeWalls2), null, nameof(FoodPost), null, Priority.Normal, Priority.Last);
                    _hookFood = Wired(mf, nameof(FoodPost), false) + " (" + mf.DeclaringType.FullName + ")";
                    _rotFood = mf.DeclaringType != null && mf.DeclaringType.FullName == "ROT.Models.ROTSettlementFoodModel";   // recenzja: dopisek ROT "ziemie wokol" w lancuchu
                }
            }
            catch (Exception e) { _hookFood = "BRAK (blad: " + e.Message + ")"; Stumble("EnsureHooks.Food", e); }
            try
            {
                var tr = AccessTools.TypeByName("RealisticBannerlord.Systems.Siege.SiegeAttritionBehavior");
                var mr = tr != null ? AccessTools.Method(tr, "OnDailyTickSettlement") : null;
                if (tr == null) _hookRb = "bez RealisticBannerlord";
                else if (mr == null) _hookRb = "BRAK (SiegeAttritionBehavior.OnDailyTickSettlement)";
                else
                {
                    Seal31.Patch("oblezenia 2: straty RB w oblezonej twierdzy przy zapasie 0 (SiegeAttritionBehavior.OnDailyTickSettlement)", mr, typeof(SiegeWalls2), nameof(RbSettlementPre), null, null, Priority.Normal);
                    _hookRb = Wired(mr, nameof(RbSettlementPre), true);
                }
            }
            catch (Exception e) { _hookRb = "BRAK (blad: " + e.Message + ")"; Stumble("EnsureHooks.Rb", e); }
        }

        private static string TypeOf(object o) { return o == null ? "-" : o.GetType().FullName; }

        /// <summary>Linia startowa (z SiegeWalls.SessionStart): latki, czynne modele planu i jedzenia, klucze.</summary>
        internal static void SessionStart()
        {
            try
            {
                if (Campaign.Current == null || Campaign.Current.Models == null) return;
                var mods = Campaign.Current.Models;
                Log.Info("Oblezenia 2 (start): latki - plan machin AI " + _hookAction + ", druga ekipa budowy (SiegeEvent.ConstructionTick) " + _hookBuild
                         + ", bilans jedzenia w oblezeniu " + _hookFood + ", straty RB przy zapasie 0 " + _hookRb
                         + " | modele: plan " + TypeOf(mods.SiegeStrategyActionModel) + ", jedzenie " + TypeOf(mods.SettlementFoodModel)
                         + " (ziemie wokol ROT w oblezeniu odejmowane: " + (_rotFood ? "tak" : "nie") + "), oblezenie " + TypeOf(mods.SiegeEventModel)
                         + " | klucze: trebusze " + (TrebOn ? "tak" : "nie") + " (zamek po " + CastleDays + " dob), glod " + (StarveOn ? "tak" : "nie") + " (" + F(LossPct, "0.0")
                         + "%/d, kapitulacja < " + F(SurShare * 100.0, "0") + "% / < " + SurMen + "), drabiny " + (LadderOn ? "tak" : "nie") + " (>= " + LadderDays + " dob, >= "
                         + F(LadderRatio, "0.0") + " : 1), log " + (LogOn ? "tak" : "nie") + ".");
            }
            catch (Exception e) { Stumble("SessionStart", e); }
        }
    }
}
