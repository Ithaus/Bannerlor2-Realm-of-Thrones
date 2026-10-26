using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// p9 - NAJPIERW DOZBROIC (Jeff 25.10: "powinna zanim pojdzie do bitwy AI najpierw przygotowac wojsko i dobrac pancerz, bo zolnierz taki nie nadaje sie do
    /// bitwy"). Klucz AiArmsBeforeBattle (MCM "AI Arms Before Battle"). Gotowosc R i stan "niegotowa" liczy ArmedStrength (FillPercent, histereza
    /// AiArmsBelowPercent 50 -> AiArmsReadyPercent 60; 60 = prog "chudych" partii rodu ClanFillOnce, 50 = polowa slotow wzorca pusta).
    ///
    /// NIEGOTOWA = partia lorda w regule (GarrisonKit.LordRule - takze towarzysze i partie Twojego rodu; nie gracz) poza armia, bitwa i oblezeniem, z R ponizej progu.
    ///  (a) ZAKAZ ZACZEPIANIA: postfiks DefaultMobilePartyAIModel.ShouldConsiderAttacking (DeclaredMethod - czynny NavalDLCMobilePartyAIModel przekazuje do bazy; wzor
    ///      FreeFolkRaids): niegotowa nie atakuje partii poza bandami (bandy - AiArmsBlockBanditAttacks, domyslnie wolno: rozstrzyga sila z uzbrojeniem). To samo
    ///      sprawdzenie gra robi dla dolaczenia do toczacej sie bitwy (CalculateInitiativeScoresForEnemy po kazdej wrogiej partii). Wyjatek: cel oblega osade rodu
    ///      partii albo jego bitwa toczy sie o osade rodu (obrona wlasnej osady). Ucieczka (ShouldConsiderAvoiding) bez zmian.
    ///  (b) CELE: postfiks Priority.VeryLow na CampaignEventDispatcher.AiHourlyTick (po ROT AIThinkPatch.Patch1 - Normal - i po BesiegedRelief/WatchLaw - Low).
    ///      Ruchy "jedz kupic" przez DisableForHours / SetDoNotMakeNewDecisions odpadaja: wylaczaja tez ucieczke (TickInternal pomija GetBehaviors), a
    ///      DoNotMakeNewDecisions idzie do zapisu gry - dlatego waga w ocenie celow, jak BesiegedRelief i WatchLaw. Glod - najpierw jedzenie (bez zmian).
    ///      - opcje bitwy (oblezenie, szturm, rabunek, obrona cudzej osady, poscig GoAroundParty/EngageParty za partia nie-banda) dostaja -1 (SetBehaviorScore, jak WatchLaw);
    ///      - obrona osady WLASNEGO rodu zostaje (wyjatek) - i wtedy bez wyjazdu po sprzet;
    ///      - stac go (AiGear.BudgetPreview - ten sam wzor co TryBuyCore - >= min(braki do progu gotowosci, 10) sztuk po wczorajszej sredniej cenie zakupow AI, najmniej
    ///        300; recenzja p9: braki z FillPercent, a przy wybranym miescie podglad z rezerwa napraw z materialem tego miasta, jak TryBuyCore): najblizsze przyjazne
    ///        MIASTO droga ladowa najwyzej AiArmsTripMaxDays dob marszu (nie w wojnie, bez oblezenia, po tej samej stronie Muru, poza wytchnieniem) - GoToSettlement
    ///        z ocena max(0.5, 1.2 x najlepszej innej) + 0.01 (wzor BesiegedRelief: 0.5 to prog zmiany celu gry). Na miejscu kupuje dotychczasowa droga (wejscie:
    ///        MenPurse.OnEntered -> AiGear.TryBuy; doba postoju: AiGear.OnDailyTickParty) - nic nowego w ekonomii ("nic z niczego": placi pan i sakiewka ludzi,
    ///        zloto do kasy miasta). Po dobie w miescie: postep (R wzroslo) i dalej stac go - zostaje kolejna dobe (recenzja p9: AiGear kupuje raz na dobe, polka
    ///        mogla miec wiecej); bez postepu (pusta polka albo brak zlota) - wytchnienie AiArmsTripCooldownDays dla tego miasta i nastepne miasto w zasiegu; za dlugo
    ///        w drodze (2 x zasieg + 12 h) - tak samo; NoGainMax wizyt z rzedu bez postepu - jak "nie stac" (bez dalszych wyjazdow; petla "jade - nic nie ma - jade");
    ///      - nie stac go albo brak miasta: tylko -1 opcjom bitwy - patrol i dojazd do wlasnych osad z gry zostaja. Recenzja p9 (uwaga 3, regula Jeffa 12.10: "jak ich
    ///        nie stac na wojne, to pozyczka albo mniejsze wojsko" - nie bezczynnosc): najwyzej AiArmsWaitMaxDays dob w stanie niegotowej (ArmedStrength.Wait), potem
    ///        ZWOLNIONA - walczy tym, co ma (sila dalej z uzbrojeniem), do gotowosci albo do pieniedzy na sprzet (wtedy nowa proba). Pozyczka Banku na sprzet albo
    ///        zwolnienie ludzi bez kompletu - DECYZJA Jeffa (nie w tym kroku).
    ///  (c) ARMIE: postfiksy DefaultArmyManagementCalculationModel.CanLordCreateArmy (baza dla StrategicCampaignAI145 i BK) i BKArmyManagementModel.CanLordCreateArmy
    ///      (BK dokleja partie rodu PO bazie i patrzy tylko na liczbe >= 2): niegotowy wodz - false i pusta lista, niegotowi kandydaci wypadaja. Recenzja p9 (uwaga 2):
    ///      w bazie gry niegotowi wypadaja PRZED rankingiem, cieciem do 70% partii krolestwa i progiem sily 1000 (Siege) - prefiks/finalizer bazy ustawiaja flage watku,
    ///      a postfiks DisbandPartyCampaignBehavior.IsPartyWaitingForDisband (predykat petli kandydatow, wolany przez interfejs) zwraca dla niegotowej true tylko
    ///      przy tej fladze; zapas: gdy postfiks bazy cos usunie (brak haka), prog 1000 liczony od nowa wzorem gry. BK MobilePartyExtensions.
    ///      IsAvailableForArmies - false (wezwania BK gracza i AI); czynny CheckPartyEligibility (SCA) - false z napisem po angielsku (okno armii gracza). Czlonek juz
    ///      w armii zostaje.
    ///  (d) CUDZE BLOKADY (druga recenzja p9, uwaga "kod" major; zasada 8.0 - nie nadpisujemy wyniku innej latki): postfiks VeryLow biegnie PO CrossingLaw.AimPostfix
    ///      (Normal; -1 kazdemu zachowaniu z osada zablokowana po dwoch zawroceniach od przeprawy), ROT AIThinkPatch.Patch1 (Normal), WatchLaw/BesiegedRelief/ReaverLaw/
    ///      RaidTribute (Low). Miasto, ktore w liscie ocen ma JAKIEKOLWIEK zachowanie z wynikiem <= -1 (odczyt PRZED naszymi -1 opcjom bitwy), jest dla wyjazdu
    ///      zablokowane: Nearest je pomija, a biezacy wyjazd do niego konczy sie wytchnieniem miasta (Cool) i wyborem nastepnego. Nigdy nie podnosimy wyniku <= -1.
    /// Stan wyjazdow bez zapisu - po wczytaniu liczony od nowa. Liczniki do linii "Gotowosc do bitwy (p9): dzien" (ArmedStrength.Daily). Potkniecia per wywolanie.
    /// 159c (druga recenzja 159b): (e) WYJAZD PO PIERWSZA BRON dla partii, ktorej p9 nie wysyla (gotowa albo zwolniona z czekania) z bronia ponad
    ///     UnarmedMenAllowedPercent: zleca NewLordKit.OverCap raz na dobe (StartCapTrip - miasto wyceny NearestTown, stac go na pierwsza bron dla nadwyzki), ta sama mysl
    ///     godzinowa (CapThink): GoToSettlement z ta sama ocena co wyjazd p9, BEZ gaszenia opcji bitwy i bez stanu "niegotowa"; obrona zagrozonej osady rodu ma
    ///     pierwszenstwo; postoj StayHours, za dlugo w drodze (2 x zasieg + 12 h), cel zablokowany cudza latka albo niewazny (wojna, oblezenie, wytchnienie) - koniec
    ///     i wytchnienie miasta; armia, nieczynna, przejeta przez p9 (niegotowa, nie zwolniona) - koniec. Najwyzej raz na UnarmedMenSentHomeDays dob na partie
    ///     (_capLast), nigdy obok wyjazdu p9. (b) "nie stac" p9: niegotowa, ktorej nie stac na pelne dozbrojenie (CanAfford), a stac na pierwsza bron dla nadwyzki
    ///     (NewLordKit.FirstOk - wycena polki) - wyjazd p9 (TripTown firstOnly: w miescie i przy przedluzeniu postoju ten sam sprawdzian) zamiast "nie stac".
    /// 159c recenzja 3: (1) TripTown pelnego dozbrojenia, ktorego nie stac w wybranym miescie (rezerwa napraw z materialem tego miasta), a stac na pierwsza bron dla
    ///     nadwyzki w tym miescie (FirstOk) - wyjazd jak firstOnly (Trip.First: przedluzenie postoju tym samym sprawdzianem), "nie stac w miescie" tylko gdy oba nie.
    ///     (2) CapThink: obrona zagrozonej osady KROLESTWA partii (oblezenie albo bitwa o nia), gdy jest najlepsza opcja (np. podbita przez OB5 BesiegedRelief -
    ///     Jeff 14.10 "stolicy i miast trzeba bronic za wszelka cene") - wyjazd po pierwsza bron czeka, jak przy osadzie rodu. (3) Wyjazd po pierwsza bron
    ///     zakonczony przed przyjazdem przez cudza blokade albo niewazne miasto nie liczy sie do odstepu (_capLast) - nastepnej doby nowa proba (inne miasto).
    /// </summary>
    internal static class ArmsFirst
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const double StayHours = 26.0;   // zakup przy wjezdzie i jedna doba postoju (AiGear kupuje raz na dobe)
        internal const float FloorScore = 0.5f;   // najnizsza ocena wyjazdu - prog zmiany celu gry (jak BesiegedRelief.FloorBase)
        internal const float Edge = 1.2f;         // ponad najlepsza inna opcje
        private const int MinNeed = 300;         // najmniejszy budzet "stac go"
        private const int PiecesUnit = 10;       // recenzja p9: "stac go" = min(braki do progu, 10) sztuk po sredniej cenie (zakup rozlozony na doby postoju)
        private const int NoGainMax = 2;         // recenzja p9: tyle wizyt z rzedu bez postepu - jak "nie stac" (koniec wyjazdow do gotowosci albo zwolnienia)
        private const float ArmyFloor = 1000f;   // DefaultArmyManagementCalculationModel.CanLordCreateArmy: num5 < 1000f -> false (Siege, krolestwo z osadami)
        private static readonly TextObject NotArmedText = new TextObject("{=!}Its men lack arms and armour - it must arm them before joining an army.");

        private sealed class Trip { internal MobileParty P; internal Settlement Town; internal double StartH, ArriveH = -1; internal int R0, Ext; internal bool First; }   // First - 159c: wyjazd p9 po pierwsza bron (FirstOk)
        private static readonly Dictionary<MBGUID, Trip> _trip = new Dictionary<MBGUID, Trip>();
        private sealed class Streak { internal MobileParty P; internal int N; }
        private static readonly Dictionary<MBGUID, Streak> _noGain = new Dictionary<MBGUID, Streak>();   // recenzja p9: wizyty z rzedu bez postepu
        [ThreadStatic] private static int _inCreate;   // recenzja p9 (uwaga 2): w toku bazowego CanLordCreateArmy (filtr kandydatow przed progiem)
        private static readonly Dictionary<string, double> _cool = new Dictionary<string, double>(StringComparer.Ordinal);   // partia|miasto -> godzina konca wytchnienia
        // 159c: wyjazdy po pierwsza bron (NewLordKit.OverCap) i godzina ostatniego startu na partie (najwyzej raz na UnarmedMenSentHomeDays dob); bez zapisu
        private static readonly Dictionary<MBGUID, Trip> _capTrip = new Dictionary<MBGUID, Trip>();
        private static readonly Dictionary<MBGUID, double> _capLast = new Dictionary<MBGUID, double>();
        private const double CapLastKeepH = 31 * 24.0;   // UnarmedMenSentHomeDays najwyzej 30
        // 159 (UnarmedMenSentHome): powod, dla ktorego ostatnia mysl nie dala wyjazdu (ThinkPostfix -> NewLordKit.NoteStuck); watek glowny
        internal const string WhyPoor = "nie stac", WhyNoTown = "brak miasta w zasiegu", WhyNoBudgetTown = "nie stac w miescie (rezerwa napraw)", WhyNoGain = "2 wizyty bez postepu";
        private static string _why;

        // latki (raz na proces)
        private static Harmony _h;
        private static readonly HashSet<MethodBase> _patched = new HashSet<MethodBase>();
        private static string _hThink = "nie wpieta", _hAttack = "nie wpieta", _hArmy = "nie wpieta", _hArmyBk = "nie wpieta", _hAvail = "nie wpieta", _hElig = "nie wpieta",
                              _hPre = "nie wpieta";
        private static MethodBase _thinkM;
        private static Func<Vec2, bool> _beyond;
        private static bool _beyondTried;

        // liczniki doby (mysl i armie - watek glowny; atak - dowolny watek, pod zamkiem)
        private static readonly object Gate = new object();
        private static int _dTrips, _dArrived, _dArmed, _dPart, _dNoGain, _dTimeout, _dOff, _dOwnDef, _dStarve, _stumbles, _dExt, _dFreedTrip, _dBlocked, _dCapRelief, _dFirstInTown;
        private static int _vAttackCalls, _vThresh;
        private static readonly HashSet<MBGUID> _vAttack = new HashSet<MBGUID>(), _vLead = new HashSet<MBGUID>(), _vMember = new HashSet<MBGUID>(),
                                                _vAvail = new HashSet<MBGUID>(), _vElig = new HashSet<MBGUID>(), _dPoor = new HashSet<MBGUID>(), _dNoTown = new HashSet<MBGUID>(),
                                                _vPre = new HashSet<MBGUID>(), _dNoBuy = new HashSet<MBGUID>(), _dNoBudgetTown = new HashSet<MBGUID>();

        private static void Stumble(string where, Exception e)
        {
            int n = Interlocked.Increment(ref _stumbles);
            try { if (n <= 5 || n % 100 == 0) Log.Error("ArmsFirst." + where + " (potkniecie " + n + ")", e); } catch { }
        }

        internal static void Reset() { _trip.Clear(); _cool.Clear(); _noGain.Clear(); _capTrip.Clear(); _capLast.Clear(); ClearDay(); _stumbles = 0; }

        internal static void ClearDay()
        {
            _dTrips = _dArrived = _dArmed = _dPart = _dNoGain = _dTimeout = _dOff = _dOwnDef = _dStarve = _dExt = _dFreedTrip = _dBlocked = _dCapRelief = _dFirstInTown = 0;
            lock (Gate) { _vAttackCalls = 0; _vThresh = 0; _vAttack.Clear(); _vLead.Clear(); _vMember.Clear(); _vAvail.Clear(); _vElig.Clear(); _vPre.Clear(); }
            _dPoor.Clear(); _dNoTown.Clear(); _dNoBuy.Clear(); _dNoBudgetTown.Clear();
        }

        /// <summary>Koniec wyjazdu i serii wizyt bez postepu (zniszczona partia, zwolnienie z czekania, obrona wlasnej osady, brak pieniedzy). Watek glowny.</summary>
        internal static void Forget(MobileParty mp) { if (mp != null) { _trip.Remove(mp.Id); _noGain.Remove(mp.Id); } }

        private static int NoGainOf(MobileParty mp) { Streak k; return mp != null && _noGain.TryGetValue(mp.Id, out k) ? k.N : 0; }

        /// <summary>Recenzja p9: partia na postoju w miescie wyjazdu - ArmedStrength.Wait jej nie zwalnia. Druga recenzja p9 (uwaga "kod", limit czekania): kazdy postoj,
        /// nie tylko przedluzony (dotad t.Ext > 0) - lord, ktorego zegar mija pierwszej doby drugiego postoju, nie wyjezdza bez zakupu. Ograniczone: postoj StayHours,
        /// przedluzenie tylko z postepem R (sufit 100%).</summary>
        internal static bool Progressing(MobileParty mp)
        {
            Trip t;
            return mp != null && _trip.TryGetValue(mp.Id, out t) && t.ArriveH >= 0 && mp.CurrentSettlement == t.Town;
        }

        private static int Clamp(int v, int lo, int hi) { return v < lo ? lo : (v > hi ? hi : v); }
        internal static int TripDays(Settings s) { return Clamp(s.AiArmsTripMaxDays, 1, 14); }
        internal static int CoolDays(Settings s) { return Clamp(s.AiArmsTripCooldownDays, 0, 30); }
        internal static int WaitDays(Settings s) { return Clamp(s.AiArmsWaitMaxDays, 1, 60); }

        private static void Note(HashSet<MBGUID> set, MobileParty mp) { if (mp == null) return; lock (Gate) set.Add(mp.Id); }

        // ------------------------------------------------------------ predykaty
        /// <summary>Niegotowa partia lorda AI (dowolny watek - tylko odczyt publikacji ArmedStrength): poza armia, bitwa i oblezeniem, klucz wlaczony.</summary>
        internal static bool Unready(MobileParty mp)
        {
            var s = Settings.Current;
            if (s == null || !s.AiArmsBeforeBattle || mp == null || mp.IsMainParty) return false;
            if (mp.Army != null || mp.MapEvent != null || mp.BesiegedSettlement != null) return false;
            return ArmedStrength.IsLow(mp);
        }

        /// <summary>Stac go na sprzet: podglad budzetu zakupow brakow (AiGear.BudgetPreview - wzor TryBuyCore; z miastem - rezerwa napraw z materialem tego miasta,
        /// jak TryBuyCore) >= min(braki do progu gotowosci, PiecesUnit) sztuk po wczorajszej sredniej cenie zakupow AI (najmniej MinNeed). Recenzja p9 (uwaga 4): nie
        /// pelne braki naraz - AiGear kupuje raz na dobe AiGearBudgetPercent kiesy, a partia z postepem zostaje w miescie kolejne doby; prog "jedna wizyta pokrywa
        /// min(braki, AiGearMaxPiecesPerVisit)" (Darlessa: min(121, 60) = 60 szt. x 36 d = ok. 2.2 tys. na wizyte, przy 25% kiesy ponad rezerwe 2000 - kiesa ok.
        /// 10.6 tys.) zostawilby bez wyjazdu lordow, ktorzy dozbroja sie w kilka dob. Zakupy AI wylaczone - nie stac (nikt nie kupuje).</summary>
        internal static bool CanAfford(MobileParty mp, int shortPieces, Settlement town, out int budget, out int need)
        {
            budget = 0; need = MinNeed;
            try
            {
                int pcs = Math.Max(1, Math.Min(PiecesUnit, shortPieces));
                need = Math.Max(MinNeed, pcs * Math.Max(0, AiGear.AvgPiecePriceYesterday));
                int planNeed = NewLordKit.PlanNeed(mp);   // 159: nowa druzyna z planem - prog najwyzej wycena jej planu (mala druzyna nie potrzebuje MinNeed)
                if (planNeed > 0) need = Math.Min(need, planNeed);
                budget = town != null ? AiGear.BudgetPreview(mp, town) : AiGear.BudgetPreview(mp);
                return AiGear.On && budget >= need;
            }
            catch (Exception e) { Stumble("CanAfford", e); return false; }
        }

        private static bool OwnClanSettlement(Settlement st, Clan clan) { return st != null && clan != null && st.OwnerClan == clan; }

        /// <summary>Obrona ZAGROZONEJ osady wlasnego rodu (oblezenie albo bitwa o nia - rabunek wsi, szturm). Gra dodaje DefendSettlement takze dla osad bez
        /// zagrozenia (patrol przy swoich) - te nie sa wyjatkiem i nie wstrzymuja wyjazdu po sprzet.</summary>
        internal static bool IsOwnDefend(in AIBehaviorData d, Clan clan)
        {
            if (d.AiBehavior != AiBehavior.DefendSettlement) return false;
            var st = d.Party as Settlement;
            return OwnClanSettlement(st, clan) && (st.SiegeEvent != null || (st.Party != null && st.Party.MapEvent != null));
        }

        /// <summary>159c recenzja 3: obrona ZAGROZONEJ osady krolestwa partii (MapFaction - te same osady co OB5 BesiegedRelief), takze cudzego rodu.</summary>
        internal static bool IsKinDefend(in AIBehaviorData d, MobileParty party)
        {
            if (d.AiBehavior != AiBehavior.DefendSettlement) return false;
            var st = d.Party as Settlement;
            var f = party.MapFaction;
            return st != null && f != null && st.MapFaction == f && (st.SiegeEvent != null || (st.Party != null && st.Party.MapEvent != null));
        }

        /// <summary>Opcja oceny celow, ktora szuka bitwy: oblezenie, szturm, rabunek, obrona cudzej osady, poscig za partia (bandy - wedlug klucza).</summary>
        private static bool IsBattle(in AIBehaviorData d, Clan clan, Settings s)
        {
            switch (d.AiBehavior)
            {
                case AiBehavior.BesiegeSettlement:
                case AiBehavior.AssaultSettlement:
                case AiBehavior.RaidSettlement:
                    return true;
                case AiBehavior.DefendSettlement:
                    return !OwnClanSettlement(d.Party as Settlement, clan);
                case AiBehavior.GoAroundParty:
                case AiBehavior.EngageParty:
                    var t = d.Party as MobileParty;
                    return t == null || !t.IsBandit || s.AiArmsBlockBanditAttacks;
                default:
                    return false;
            }
        }

        /// <summary>Cel przy osadzie rodu partii: oblega ja albo jego bitwa toczy sie o nia (rabunek wsi rodu, szturm) - obrona wlasnej osady.</summary>
        internal static bool DefendsOwn(MobileParty party, MobileParty target)
        {
            var clan = party.ActualClan;
            if (clan == null) return false;
            if (OwnClanSettlement(target.BesiegedSettlement, clan)) return true;
            var me = target.MapEvent;
            return me != null && OwnClanSettlement(me.MapEventSettlement, clan);
        }

        internal static Func<Vec2, bool> Beyond()
        {
            if (_beyondTried) return _beyond;
            _beyondTried = true;
            try
            {
                var t = AccessTools.TypeByName("ROT.Misc.ROTUtilities");
                var m = t != null ? AccessTools.Method(t, "IsBeyondTheWall", new[] { typeof(Vec2) }) : null;
                if (m != null && m.IsStatic && m.ReturnType == typeof(bool)) _beyond = (Func<Vec2, bool>)Delegate.CreateDelegate(typeof(Func<Vec2, bool>), m);
            }
            catch (Exception e) { _beyond = null; Stumble("Beyond", e); }
            return _beyond;
        }

        // ------------------------------------------------------------ (a) atak
        /// <summary>Postfiks ShouldConsiderAttacking (watki AI): niegotowa partia nie zaczepia partii poza bandami; wyjatek - obrona osady wlasnego rodu. Nie rzuca.</summary>
        public static void AttackPostfix(MobileParty __0, MobileParty __1, ref bool __result)
        {
            if (!__result || __0 == null || __1 == null) return;
            try
            {
                if (!Unready(__0)) return;
                var s = Settings.Current;
                if (__1.IsBandit && !s.AiArmsBlockBanditAttacks) return;
                if (DefendsOwn(__0, __1)) return;
                __result = false;
                lock (Gate) { _vAttackCalls++; _vAttack.Add(__0.Id); }
            }
            catch { }
        }

        // ------------------------------------------------------------ (b) cele
        /// <summary>Postfiks VeryLow CampaignEventDispatcher.AiHourlyTick: niegotowa partia lorda AI - opcje bitwy -1, wyjazd do miasta po sprzet (opis klasy). Nie rzuca.</summary>
        public static void ThinkPostfix(MobileParty party, PartyThinkParams partyThinkParams)
        {
            if (party == null || partyThinkParams == null) return;
            try
            {
                var s = Settings.Current;
                if (s == null || !s.AiArmsBeforeBattle || !party.IsLordParty || party.IsMainParty) return;
                var info = ArmedStrength.InfoOf(party);
                if (info == null || !info.Low || info.Free)   // recenzja p9: zwolniona z czekania - zwykle decyzje
                {
                    if (_capTrip.Count > 0) CapThink(party, partyThinkParams, s);   // 159c: wyjazd po pierwsza bron (gotowa albo zwolniona, bron ponad %)
                    return;
                }
                if (_capTrip.Count > 0 && _capTrip.Remove(party.Id)) NewLordKit.CapTripEvent(NewLordKit.CapEnded);   // 159c: niegotowa - wyjazd prowadzi p9
                if (party.Army != null || party.MapEvent != null || party.BesiegedSettlement != null) return;
                if (party.CurrentSettlement != null && party.CurrentSettlement.SiegeEvent != null) return;
                if (partyThinkParams.DoNotChangeBehavior) return;
                if (party.Party.IsStarving) { _dStarve++; return; }   // najpierw jedzenie - droga gry
                var scores = partyThinkParams.AIBehaviorScores;
                if (scores == null) return;
                var clan = party.ActualClan;
                float other = 0f, own = -1f;
                for (int i = 0; i < scores.Count; i++)
                {
                    var e = scores[i];
                    if (IsOwnDefend(e.Item1, clan)) { if (e.Item2 > own) own = e.Item2; }
                    else if (e.Item2 > other) other = e.Item2;
                }
                // druga recenzja p9 (uwaga major): osady zablokowane przez latki przed nami (wynik <= -1, np. CrossingLaw.AimPostfix) - odczyt PRZED naszymi -1
                HashSet<Settlement> blocked = null;
                for (int i = 0; i < scores.Count; i++)
                {
                    var e = scores[i];
                    if (e.Item2 > -1f) continue;
                    var bs = e.Item1.Party as Settlement;
                    if (bs == null || !bs.IsTown) continue;
                    if (blocked == null) blocked = new HashSet<Settlement>();
                    blocked.Add(bs);
                }
                Settlement town = null;
                if (own >= 0f) { _dOwnDef++; Forget(party); }   // obrona zagrozonej osady wlasnego rodu - wyjatek, bez wyjazdu (ta opcja zostaje, IsBattle jej nie gasi)
                else
                {
                    int b, need;
                    _why = null;
                    if (NoGainOf(party) >= NoGainMax) { _dNoBuy.Add(party.Id); _trip.Remove(party.Id); _why = WhyNoGain; }   // recenzja p9: seria wizyt bez postepu - jak "nie stac"
                    else if (CanAfford(party, info.Short, null, out b, out need)) town = TripTown(party, info, CampaignTime.Now.ToHours, s, blocked, false);
                    else if (NewLordKit.FirstOk(party, null))
                    {
                        // 159c (druga recenzja 159b, uwaga 2): nie stac na pelne dozbrojenie (prog p9 MinNeed 300 zl), ale stac na pierwsza bron dla nadwyzki bez broni
                        // wedlug wyceny polki (2-3 zl za sztuke) - wyjazd zamiast "nie stac" (bez niego regula 159 odsylalaby ludzi, ktorych pan moze uzbroic)
                        town = TripTown(party, info, CampaignTime.Now.ToHours, s, blocked, true);
                        NewLordKit.NoteLowFirst(party, town != null);
                    }
                    else { _dPoor.Add(party.Id); Forget(party); _why = WhyPoor; }
                    // 159 (UnarmedMenSentHome): od kiedy i czemu niegotowa partia nie moze sie dozbroic (bez wyjazdu); wyjazd - koniec stania. Tylko zapis stanu
                    NewLordKit.NoteStuck(party, town == null ? (_why ?? "?") : null);
                }
                // opcje bitwy -1 (kopia listy - SetBehaviorScore zmienia elementy)
                var copy = new List<(AIBehaviorData, float)>(scores.Count);
                for (int i = 0; i < scores.Count; i++) copy.Add(scores[i]);
                foreach (var it in copy)
                {
                    if (!(it.Item2 > -1f)) continue;
                    var d = it.Item1;
                    if (!IsBattle(d, clan, s)) continue;
                    partyThinkParams.SetBehaviorScore(in d, -1f);
                    _dOff++;
                }
                if (town == null) return;
                Aim(partyThinkParams, scores, town, other);
            }
            catch (Exception e) { Stumble("ThinkPostfix", e); }
        }

        /// <summary>Ocena wyjazdu do miasta: GoToSettlement droga ladowa max(FloorScore, Edge x najlepsza inna) + 0.01; wyniku &lt;= -1 (cudza blokada) nie podnosimy.
        /// Wspolne dla wyjazdu p9 i (159c) wyjazdu po pierwsza bron.</summary>
        private static void Aim(PartyThinkParams partyThinkParams, MBReadOnlyList<(AIBehaviorData, float)> scores, Settlement town, float other)
        {
            float nv = Math.Max(FloorScore, Edge * other) + 0.01f;
            bool set = false;
            for (int i = 0; i < scores.Count; i++)
            {
                var d = scores[i].Item1;
                if (d.AiBehavior != AiBehavior.GoToSettlement || d.WillGatherArmy || d.NavigationType != MobileParty.NavigationType.Default || !ReferenceEquals(d.Party, town)) continue;   // droga ladowa (Nearest)
                if (scores[i].Item2 > -1f && scores[i].Item2 < nv) partyThinkParams.SetBehaviorScore(in d, nv);   // wyniku <= -1 (cudza blokada) nie podnosimy
                set = true;
                break;
            }
            if (!set)
            {
                var a = new AIBehaviorData(town, AiBehavior.GoToSettlement, MobileParty.NavigationType.Default, false, false, false);
                partyThinkParams.AddBehaviorScore((a, nv));
            }
        }

        /// <summary>
        /// 159c: mysl godzinowa partii, ktorej p9 nie wysyla (gotowa albo zwolniona z czekania), z wyjazdem po pierwsza bron (StartCapTrip): te same warunki co
        /// wyjazd p9 (armia - koniec; bitwa, oblezenie, osada w oblezeniu, DoNotChangeBehavior, glod - bez zmian w tej godzinie), cel zablokowany cudza latka
        /// (wynik &lt;= -1) albo niewazny (Valid: wojna, oblezenie, wytchnienie) - koniec i wytchnienie; obrona zagrozonej osady rodu ma pierwszenstwo (bez oceny,
        /// wyjazd czeka); przyjazd, postoj StayHours (AiGear kupuje raz na dobe), za dlugo w drodze - koniec i wytchnienie miasta. Opcji bitwy NIE gasi. Nie rzuca.
        /// </summary>
        private static void CapThink(MobileParty party, PartyThinkParams partyThinkParams, Settings s)
        {
            Trip t;
            if (!_capTrip.TryGetValue(party.Id, out t)) return;
            if (party.Army != null) { _capTrip.Remove(party.Id); NewLordKit.CapTripEvent(NewLordKit.CapEnded); return; }
            if (party.MapEvent != null || party.BesiegedSettlement != null || party.IsCurrentlyAtSea) return;   // na morzu - wyjazd ladowy czeka (za dlugo - koniec)
            if (party.CurrentSettlement != null && party.CurrentSettlement.SiegeEvent != null) return;
            if (partyThinkParams.DoNotChangeBehavior) return;
            if (party.Party.IsStarving) return;   // najpierw jedzenie - droga gry
            var scores = partyThinkParams.AIBehaviorScores;
            if (scores == null) return;
            double now = CampaignTime.Now.ToHours;
            var clan = party.ActualClan;
            float other = 0f, own = -1f, kin = -1f;
            bool blocked = false;
            for (int i = 0; i < scores.Count; i++)
            {
                var e = scores[i];
                if (e.Item2 <= -1f) { if (ReferenceEquals(e.Item1.Party, t.Town)) blocked = true; continue; }
                if (IsOwnDefend(e.Item1, clan)) { if (e.Item2 > own) own = e.Item2; }
                else if (IsKinDefend(e.Item1, party)) { if (e.Item2 > kin) kin = e.Item2; }   // 159c recenzja 3: odsiecz osady krolestwa
                else if (e.Item2 > other) other = e.Item2;
            }
            if (blocked || !Valid(party, t.Town, now))
            {
                if (blocked) Cool(party, t.Town, now, s);
                // 159c recenzja 3: partia nie dojechala - wyjazd sie nie liczy do odstepu (nastepnej doby nowa wycena: miasto zablokowane ma wytchnienie, niewazne
                // odpada w Valid); inaczej CapTripRecent blokowal nowy wyjazd do konca zegara i nadwyzka szla do domu bez osiagalnego miasta
                if (t.ArriveH < 0) _capLast.Remove(party.Id);
                _capTrip.Remove(party.Id); NewLordKit.CapTripEvent(NewLordKit.CapEnded);
                return;
            }
            bool inTown = party.CurrentSettlement == t.Town;
            if (inTown && t.ArriveH < 0) { t.ArriveH = now; NewLordKit.CapTripEvent(NewLordKit.CapArrived); }
            bool done = t.ArriveH >= 0 && (!inTown || now - t.ArriveH >= StayHours);
            bool late = t.ArriveH < 0 && now - t.StartH > TripDays(s) * 48.0 + 12.0;
            if (done || late)
            {
                NewLordKit.CapTripEvent(late ? NewLordKit.CapLate : NewLordKit.CapDone);
                Cool(party, t.Town, now, s);
                _capTrip.Remove(party.Id);
                return;
            }
            if (own >= 0f) return;   // obrona zagrozonej osady wlasnego rodu - pierwszenstwo (wyjazd czeka)
            // 159c recenzja 3: obrona zagrozonej osady krolestwa najlepsza opcja (OB5 podbija odsiecz ponad inne cele; Jeff 14.10 "stolicy i miast trzeba bronic za
            // wszelka cene") - wyjazd czeka (partia gotowa zostaje w zasiegu odsieczy OB5); gdy inna opcja jest lepsza - wyjazd ponad nia jak dotad
            if (kin >= 0f && kin >= other) { _dCapRelief++; return; }
            Aim(partyThinkParams, scores, t.Town, Math.Max(other, kin));
        }

        /// <summary>159c (NewLordKit.OverCap): wyjazd po pierwsza bron do miasta wyceny - nie obok wyjazdu p9, nie drugi w toku, najwyzej raz na UnarmedMenSentHomeDays
        /// dob (CapTripRecent). Watek glowny.</summary>
        internal static bool StartCapTrip(MobileParty mp, Settlement town, double now)
        {
            var s = Settings.Current;
            if (s == null || !s.AiArmsBeforeBattle || mp == null || town == null || _trip.ContainsKey(mp.Id) || _capTrip.ContainsKey(mp.Id)) return false;
            _capTrip[mp.Id] = new Trip { P = mp, Town = town, StartH = now };
            _capLast[mp.Id] = now;
            return true;
        }

        /// <summary>159c: wyjazd po pierwsza bron w toku (w drodze albo na postoju) - zegar OverCap czeka.</summary>
        internal static bool CapTripActive(MobileParty mp) { return mp != null && _capTrip.ContainsKey(mp.Id); }

        /// <summary>159c: ostatni wyjazd po pierwsza bron tej partii zaczal sie mniej niz spacingH godzin temu (albo wyjazd p9 w toku).</summary>
        internal static bool CapTripRecent(MobileParty mp, double now, double spacingH)
        {
            double h;
            return mp != null && (_trip.ContainsKey(mp.Id) || (_capLast.TryGetValue(mp.Id, out h) && now - h < spacingH));
        }

        /// <summary>159c: koniec wyjazdu po pierwsza bron (NewLordKit: bez nadwyzki, poza regula, w armii, p9 przejmuje). Godzina startu zostaje (odstep).</summary>
        internal static void ForgetCap(MobileParty mp)
        {
            if (mp != null && _capTrip.Remove(mp.Id)) NewLordKit.CapTripEvent(NewLordKit.CapEnded);
        }

        internal static int CapTripCount { get { return _capTrip.Count; } }

        private static string Key(MobileParty mp, Settlement st) { return mp.StringId + "|" + st.StringId; }

        private static bool Cooling(MobileParty mp, Settlement st, double now) { double u; return _cool.TryGetValue(Key(mp, st), out u) && u > now; }

        private static void Cool(MobileParty mp, Settlement st, double now, Settings s)
        {
            int d = CoolDays(s);
            if (d > 0 && st != null) _cool[Key(mp, st)] = now + d * 24.0;
        }

        /// <summary>159c recenzja 3 (NewLordKit.OverCap): wycena pierwszej broni w miescie bez sztuk dla nadwyzki - wytchnienie tego miasta dla partii (nastepnej doby
        /// NearestTown bierze kolejne). Watek glowny.</summary>
        internal static void CoolTown(MobileParty mp, Settlement st)
        {
            var s = Settings.Current;
            if (s != null && mp != null && st != null) Cool(mp, st, CampaignTime.Now.ToHours, s);
        }

        private static bool Valid(MobileParty mp, Settlement st, double now)
        {
            return st != null && st.IsTown && st.Town != null && st.ItemRoster != null && !st.IsUnderSiege
                   && !FactionManager.IsAtWarAgainstFaction(mp.MapFaction, st.MapFaction) && !Cooling(mp, st, now);
        }

        /// <summary>Cel wyjazdu po sprzet: biezacy (do konca wizyty) albo najblizsze wazne miasto w zasiegu; null - brak.</summary>
        private static Settlement TripTown(MobileParty party, ArmedStrength.Info info, double now, Settings s, HashSet<Settlement> blocked, bool firstOnly)
        {
            Trip t;
            if (_trip.TryGetValue(party.Id, out t))
            {
                if (blocked != null && blocked.Contains(t.Town)) { _dBlocked++; Cool(party, t.Town, now, s); _trip.Remove(party.Id); }   // druga recenzja p9: cudza blokada celu
                else if (!Valid(party, t.Town, now)) _trip.Remove(party.Id);
                else
                {
                    bool inTown = party.CurrentSettlement == t.Town;
                    if (inTown && t.ArriveH < 0) { t.ArriveH = now; _dArrived++; }
                    bool done = t.ArriveH >= 0 && (!inTown || now - t.ArriveH >= StayHours);
                    bool late = t.ArriveH < 0 && now - t.StartH > TripDays(s) * 48.0 + 12.0;
                    if (!done && !late) return t.Town;
                    bool gain = !late && info.R > t.R0;
                    // recenzja p9 (uwaga 4): postep w tej wizycie i dalej go stac (podglad z rezerwa napraw tego miasta) - zostaje kolejna dobe (AiGear kupuje raz na dobe)
                    int b2, need2;
                    // 159c: wyjazd po pierwsza bron dla nadwyzki (firstOnly) - "dalej go stac" wedlug wyceny pierwszej broni w tym miescie (NewLordKit.FirstOk)
                    // 159c recenzja 3: wyjazd zaczety jako pierwsza bron (t.First) - ten sam sprawdzian, takze gdy ta godzina idzie galezia pelnego dozbrojenia
                    if (gain && inTown && ((firstOnly || t.First) ? NewLordKit.FirstOk(party, t.Town) : CanAfford(party, info.Short, t.Town, out b2, out need2)))
                    {
                        t.ArriveH = now; t.R0 = info.R; t.Ext++; _dExt++; _noGain.Remove(party.Id);
                        return t.Town;
                    }
                    // wizyta skonczona, a partia dalej niegotowa (albo za dlugo w drodze) - wytchnienie tego miasta i nastepne w zasiegu
                    if (late) _dTimeout++; else if (gain) _dPart++; else _dNoGain++;
                    if (gain) _noGain.Remove(party.Id);
                    else
                    {
                        Streak k;
                        if (!_noGain.TryGetValue(party.Id, out k)) { k = new Streak { P = party }; _noGain[party.Id] = k; }
                        k.N++;
                    }
                    Cool(party, t.Town, now, s);
                    _trip.Remove(party.Id);
                    if (NoGainOf(party) >= NoGainMax) { _dNoBuy.Add(party.Id); _why = WhyNoGain; return null; }   // recenzja p9: bez kolejnych wyjazdow
                }
            }
            // 159: nowa druzyna z planem jedzie do miasta, w ktorym wyceniono jej komplet (gdy dalej wazne, niezablokowane i bez wytchnienia); inaczej najblizsze
            var town = NewLordKit.PlanTown(party);
            if (town != null && (!Valid(party, town, now) || (blocked != null && blocked.Contains(town)))) town = null;
            if (town == null) town = Nearest(party, now, s, blocked);
            if (town == null) { _dNoTown.Add(party.Id); _why = WhyNoTown; return null; }
            int b, need;
            bool first = firstOnly;
            if (firstOnly)
            {
                // 159c: wycena pierwszej broni dla nadwyzki na polce TEGO miasta (moze byc inne niz najblizsze bez blokad); nie stac - jak dotad "nie stac"
                if (!NewLordKit.FirstOk(party, town)) { _dPoor.Add(party.Id); _why = WhyPoor; return null; }
            }
            else if (!CanAfford(party, info.Short, town, out b, out need))   // recenzja p9: rezerwa napraw z materialem tego miasta
            {
                // 159c recenzja 3: nie stac w tym miescie na pelne dozbrojenie (podglad z rezerwa napraw <= bez miasta), a stac na pierwsza bron dla nadwyzki na jego
                // polce - wyjazd jak firstOnly; inaczej "nie stac w miescie" i po UnarmedMenSentHomeDays odsylka ludzi, ktorych pan moze uzbroic
                if (!NewLordKit.FirstOk(party, town)) { _dNoBudgetTown.Add(party.Id); _why = WhyNoBudgetTown; return null; }
                first = true; _dFirstInTown++;
                NewLordKit.NoteLowFirst(party, true);
            }
            _trip[party.Id] = new Trip { P = party, Town = town, StartH = now, R0 = info.R, First = first };
            _dTrips++;
            return town;
        }

        /// <summary>159 (NewLordKit.Plan): najblizsze wazne miasto w zasiegu wyjazdu po sprzet dla nowej druzyny - ta sama regula co wyjazd p9 (Nearest). failed - wyjatek
        /// (wolajacy NIE traktuje go jak "brak miasta": plan bez zmian skladu). Nie rzuca.</summary>
        internal static Settlement NearestTown(MobileParty party, out bool failed)
        {
            failed = false;
            try { var s = Settings.Current; return s != null && party != null ? Nearest(party, CampaignTime.Now.ToHours, s, null) : null; }
            catch (Exception e) { failed = true; Stumble("NearestTown", e); return null; }
        }

        /// <summary>Najblizsze droga ladowa wazne miasto w zasiegu AiArmsTripMaxDays dob marszu (EstimatedAverageLordPartySpeed x 24 h), po tej samej stronie Muru.</summary>
        private static Settlement Nearest(MobileParty party, double now, Settings s, HashSet<Settlement> blocked)
        {
            if (party.IsCurrentlyAtSea) return null;
            var c = Campaign.Current;
            var model = c != null && c.Models != null ? c.Models.MapDistanceModel : null;
            if (model == null) return null;
            float range = TripDays(s) * 24f * Math.Max(0.5f, c.EstimatedAverageLordPartySpeed);
            var at = party.CurrentSettlement;
            var pos = party.GetPosition2D;
            var bw = Beyond();
            bool pBeyond = false;
            if (bw != null) { try { pBeyond = bw(pos); } catch { bw = null; } }
            Settlement best = null; float bd = float.MaxValue;
            foreach (var tw in Town.AllTowns)
            {
                var st = tw != null ? tw.Settlement : null;
                if (st == null || !Valid(party, st, now)) continue;
                if (blocked != null && blocked.Contains(st)) continue;   // druga recenzja p9: zablokowane przez latke przed nami (CrossingLaw i inne)
                if (pos.Distance(st.GetPosition2D) > range) continue;   // w linii prostej dalej niz zasieg - droga tym bardziej
                float d;
                if (at == st) d = 0f;
                else
                {
                    float lr;
                    d = at != null ? model.GetDistance(at, st, false, false, MobileParty.NavigationType.Default)
                                   : model.GetDistance(party, st, false, MobileParty.NavigationType.Default, out lr);
                    if (CartGuard.NoLandRoad(d)) continue;
                }
                if (d > range || d >= bd) continue;
                if (bw != null) { try { if (bw(st.GetPosition2D) != pBeyond) continue; } catch { } }
                best = st; bd = d;
            }
            return best;
        }

        internal static string TargetName(MobileParty mp)
        {
            try { Trip t; return mp != null && _trip.TryGetValue(mp.Id, out t) && t.Town != null ? t.Town.Name.ToString() + (t.ArriveH >= 0 ? " (na miejscu)" : "") : "-"; }
            catch { return "?"; }
        }

        /// <summary>Godzina (z ArmedStrength.Hourly): wyjazdy partii juz gotowych (dozbrojone), w armii albo nieczynnych - koniec; wygasle wytchnienia.</summary>
        internal static void Hourly()
        {
            if (_trip.Count > 0)
            {
                List<MBGUID> rm = null;
                foreach (var kv in _trip)
                {
                    var t = kv.Value; var p = t.P;
                    bool end = p == null || !p.IsActive || p.Army != null;
                    if (!end)
                    {
                        var i = ArmedStrength.InfoOf(p);
                        if (i == null || !i.Low) { _dArmed++; end = true; }
                        else if (i.Free) { _dFreedTrip++; end = true; }   // recenzja p9: zwolniona z czekania w drodze
                    }
                    if (end) { if (rm == null) rm = new List<MBGUID>(); rm.Add(kv.Key); }
                }
                if (rm != null) foreach (var k in rm) _trip.Remove(k);
            }
            if (_noGain.Count > 0)
            {
                // recenzja p9: seria wizyt bez postepu tylko dla partii dalej niegotowych (gotowa, zwolniona, w armii albo nieczynna - od nowa)
                List<MBGUID> rm = null;
                foreach (var kv in _noGain)
                {
                    var p = kv.Value.P;
                    if (p == null || !p.IsActive || p.Army != null || !ArmedStrength.IsLow(p)) { if (rm == null) rm = new List<MBGUID>(); rm.Add(kv.Key); }
                }
                if (rm != null) foreach (var k in rm) _noGain.Remove(k);
            }
            if (_capTrip.Count > 0)
            {
                // 159c: wyjazd po pierwsza bron - koniec przy partii nieczynnej, w armii albo przejetej przez p9 (niegotowa, nie zwolniona); zapas: partia, ktora nie
                // mysli (CapThink konczy wyjazd w mysli), najpozniej po 2 x zasieg + 12 h + postoj + doba (zegar OverCap czeka na koniec wyjazdu)
                var s = Settings.Current;
                double now = CampaignTime.Now.ToHours, maxH = (s != null ? TripDays(s) : 3) * 48.0 + 12.0 + StayHours + 24.0;
                List<MBGUID> rm = null;
                foreach (var kv in _capTrip)
                {
                    var p = kv.Value.P;
                    var i = p != null && p.IsActive ? ArmedStrength.InfoOf(p) : null;
                    if (p == null || !p.IsActive || p.Army != null || (i != null && i.Low && !i.Free) || now - kv.Value.StartH > maxH) { if (rm == null) rm = new List<MBGUID>(); rm.Add(kv.Key); }
                }
                if (rm != null) foreach (var k in rm) { _capTrip.Remove(k); NewLordKit.CapTripEvent(NewLordKit.CapEnded); }
            }
            if (_capLast.Count > 0)
            {
                double now = CampaignTime.Now.ToHours;
                List<MBGUID> rm = null;
                foreach (var kv in _capLast) if (now - kv.Value > CapLastKeepH) { if (rm == null) rm = new List<MBGUID>(); rm.Add(kv.Key); }
                if (rm != null) foreach (var k in rm) _capLast.Remove(k);
            }
            if (_cool.Count > 0)
            {
                double now = CampaignTime.Now.ToHours;
                List<string> rm = null;
                foreach (var kv in _cool) if (kv.Value <= now) { if (rm == null) rm = new List<string>(); rm.Add(kv.Key); }
                if (rm != null) foreach (var k in rm) _cool.Remove(k);
            }
        }

        // ------------------------------------------------------------ (c) armie
        /// <summary>Postfiks CanLordCreateArmy (Default - baza SCA i BK; BK - po doklejeniu partii rodu): niegotowy wodz - false i pusta lista; niegotowi kandydaci wypadaja.</summary>
        public static void CreatePostfix(MobileParty __0, ref MBList<MobileParty> __1, ref bool __result, MethodBase __originalMethod)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.AiArmsBeforeBattle) return;
                if (Unready(__0))
                {
                    if (__result || (__1 != null && __1.Count > 0)) Note(_vLead, __0);
                    __result = false;
                    if (__1 != null) __1.Clear();
                    return;
                }
                if (__1 == null || __1.Count == 0) return;
                int removed = 0;
                for (int i = __1.Count - 1; i >= 0; i--)
                {
                    var m = __1[i];
                    if (m != null && Unready(m)) { __1.RemoveAt(i); Note(_vMember, m); removed++; }
                }
                if (__result && __1.Count == 0) __result = false;
                // recenzja p9 (uwaga 2), zapas na brak haka przed progiem: baza gry sprawdzila prog sily z niegotowymi - liczymy go od nowa tym samym wzorem
                // (DefaultArmyManagementCalculationModel.cs:176-189: krolestwo z osadami, GetCustomStrength(Attacker, Siege) wodza i kandydatow, < 1000 -> pusta lista i false)
                if (__result && removed > 0 && __originalMethod != null && __originalMethod.DeclaringType == typeof(DefaultArmyManagementCalculationModel))
                {
                    var k = __0.MapFaction as Kingdom;
                    if (k != null && k.Settlements.Count > 0)
                    {
                        float sum = __0.Party.GetCustomStrength(TaleWorlds.Core.BattleSideEnum.Attacker, TaleWorlds.CampaignSystem.MapEvents.MapEvent.PowerCalculationContext.Siege);
                        foreach (var m in __1) if (m != null) sum += m.Party.GetCustomStrength(TaleWorlds.Core.BattleSideEnum.Attacker, TaleWorlds.CampaignSystem.MapEvents.MapEvent.PowerCalculationContext.Siege);
                        if (sum < ArmyFloor) { __1.Clear(); __result = false; lock (Gate) _vThresh++; }
                    }
                }
            }
            catch (Exception e) { Stumble("CreatePostfix", e); }
        }

        /// <summary>Recenzja p9 (uwaga 2): prefiks bazowego CanLordCreateArmy - flaga watku "w toku petli kandydatow" (filtr niegotowych przed progiem).</summary>
        public static void CreatePrefix() { _inCreate++; }

        /// <summary>Finalizer bazowego CanLordCreateArmy: koniec flagi; wyjatek gry idzie dalej bez zmian.</summary>
        public static Exception CreateFinalizer(Exception __exception) { if (_inCreate > 0) _inCreate--; return __exception; }

        /// <summary>Recenzja p9 (uwaga 2): postfiks DisbandPartyCampaignBehavior.IsPartyWaitingForDisband - TYLKO w toku bazowego CanLordCreateArmy (flaga watku)
        /// niegotowa partia "czeka na rozwiazanie", wiec petla kandydatow gry ja pomija (przed rankingiem, cieciem num4 i progiem 1000). Poza flaga - bez zmian.</summary>
        public static void DisbandPostfix(MobileParty __0, ref bool __result)
        {
            if (_inCreate <= 0 || __result || __0 == null) return;
            try { if (Unready(__0)) { __result = true; Note(_vPre, __0); } } catch (Exception e) { Stumble("DisbandPostfix", e); }
        }

        /// <summary>Postfiks BK MobilePartyExtensions.IsAvailableForArmies: niegotowa partia nie idzie na wezwanie (BK CallBannersGoal gracza i AI, partie rodu BK).</summary>
        public static void AvailPostfix(MobileParty __0, ref bool __result)
        {
            if (!__result) return;
            try { if (Unready(__0)) { __result = false; Note(_vAvail, __0); } } catch (Exception e) { Stumble("AvailPostfix", e); }
        }

        /// <summary>Postfiks czynnego CheckPartyEligibility (okno armii gracza): niegotowa partia - false z napisem po angielsku.</summary>
        public static void EligPostfix(MobileParty __0, ref TextObject __1, ref bool __result)
        {
            if (!__result) return;
            try { if (Unready(__0)) { __result = false; __1 = NotArmedText; Note(_vElig, __0); } } catch (Exception e) { Stumble("EligPostfix", e); }
        }

        // ------------------------------------------------------------ start sesji i linia
        private static string Owners(MethodBase m)
        {
            try
            {
                if (m == null) return "-";
                var info = Harmony.GetPatchInfo(m);
                if (info == null) return "-";
                var o = info.Owners.Distinct().ToList();
                return o.Count > 0 ? string.Join("/", o.ToArray()) : "-";
            }
            catch { return "?"; }
        }

        private static string Patch(MethodInfo m, string what, Type t, string post, int prio)
        {
            if (m == null) return "BRAK (" + what + ")";
            if (_patched.Contains(m)) return "TAK (juz wczesniej w tym procesie)";
            _h.Patch(m, postfix: new HarmonyMethod(t, post) { priority = prio });
            _patched.Add(m);
            return "TAK";
        }

        /// <summary>Latki (raz na proces; w kampanii - modele armii BK czytaja singletony BK). Zwraca opis do linii startu ArmedStrength.</summary>
        internal static string SessionStart()
        {
            if (_h == null) _h = new Harmony("com.jeff.armoury.armsfirst");
            var me = typeof(ArmsFirst);
            try
            {
                var m = AccessTools.Method(typeof(CampaignEventDispatcher), "AiHourlyTick", new[] { typeof(MobileParty), typeof(PartyThinkParams) });
                _thinkM = m;
                _hThink = Patch(m, "CampaignEventDispatcher.AiHourlyTick", me, nameof(ThinkPostfix), Priority.VeryLow);
            }
            catch (Exception e) { _hThink = "wyjatek " + e.GetType().Name; Stumble("SessionStart.Think", e); }
            try
            {
                var m = AccessTools.DeclaredMethod(typeof(DefaultMobilePartyAIModel), "ShouldConsiderAttacking", new[] { typeof(MobileParty), typeof(MobileParty) });
                _hAttack = Patch(m, "DefaultMobilePartyAIModel.ShouldConsiderAttacking", me, nameof(AttackPostfix), Priority.Last);
            }
            catch (Exception e) { _hAttack = "wyjatek " + e.GetType().Name; Stumble("SessionStart.Attack", e); }
            var armyArgs = new[] { typeof(MobileParty), typeof(MBList<MobileParty>).MakeByRefType() };
            try
            {
                var m = AccessTools.DeclaredMethod(typeof(DefaultArmyManagementCalculationModel), "CanLordCreateArmy", armyArgs);
                if (m == null) _hArmy = "BRAK (DefaultArmyManagementCalculationModel.CanLordCreateArmy)";
                else if (_patched.Contains(m)) _hArmy = "TAK (juz wczesniej w tym procesie)";
                else
                {
                    // recenzja p9 (uwaga 2): prefiks i finalizer - flaga watku dla filtra kandydatow przed progiem; postfiks jak dotad (wodz, zapas z progiem 1000)
                    _h.Patch(m, prefix: new HarmonyMethod(me, nameof(CreatePrefix)) { priority = Priority.First },
                             postfix: new HarmonyMethod(me, nameof(CreatePostfix)) { priority = Priority.Last },
                             finalizer: new HarmonyMethod(me, nameof(CreateFinalizer)));
                    _patched.Add(m);
                    _hArmy = "TAK (z filtrem kandydatow przed progiem)";
                }
            }
            catch (Exception e) { _hArmy = "wyjatek " + e.GetType().Name; Stumble("SessionStart.Army", e); }
            try
            {
                // recenzja p9 (uwaga 2): predykat petli kandydatow bazowej armii - czynna klasa zachowania (gra: DisbandPartyCampaignBehavior), metoda deklarowana
                var beh = Campaign.Current != null ? Campaign.Current.GetCampaignBehavior<TaleWorlds.CampaignSystem.CampaignBehaviors.IDisbandPartyCampaignBehavior>() : null;
                var bt = beh != null ? beh.GetType() : null;
                var m = bt != null ? AccessTools.DeclaredMethod(bt, "IsPartyWaitingForDisband", new[] { typeof(MobileParty) }) : null;
                _hPre = beh == null ? "BRAK (zachowanie IDisbandPartyCampaignBehavior nieobecne - zostaje zapas z progiem 1000)"
                      : Patch(m, bt.FullName + ".IsPartyWaitingForDisband", me, nameof(DisbandPostfix), Priority.Last) + " (" + bt.Name + ")";
            }
            catch (Exception e) { _hPre = "wyjatek " + e.GetType().Name; Stumble("SessionStart.Pre", e); }
            try
            {
                var t = AccessTools.TypeByName("BannerKings.Models.Vanilla.BKArmyManagementModel");
                var m = t != null ? AccessTools.DeclaredMethod(t, "CanLordCreateArmy", armyArgs) : null;
                _hArmyBk = t == null ? "BRAK (BK nieobecny)" : Patch(m, "BKArmyManagementModel.CanLordCreateArmy", me, nameof(CreatePostfix), Priority.Last);
            }
            catch (Exception e) { _hArmyBk = "wyjatek " + e.GetType().Name; Stumble("SessionStart.ArmyBk", e); }
            try
            {
                var t = AccessTools.TypeByName("BannerKings.Extensions.MobilePartyExtensions");
                var m = t != null ? AccessTools.DeclaredMethod(t, "IsAvailableForArmies", new[] { typeof(MobileParty) }) : null;
                _hAvail = t == null ? "BRAK (BK nieobecny)" : Patch(m, "MobilePartyExtensions.IsAvailableForArmies", me, nameof(AvailPostfix), Priority.Last);
            }
            catch (Exception e) { _hAvail = "wyjatek " + e.GetType().Name; Stumble("SessionStart.Avail", e); }
            try
            {
                var model = Campaign.Current != null && Campaign.Current.Models != null ? Campaign.Current.Models.ArmyManagementCalculationModel : null;
                var m = model != null ? PatchTarget.OfActive(model, "CheckPartyEligibility", new[] { typeof(MobileParty), typeof(TextObject).MakeByRefType() }, "armia gracza: gotowosc czlonka (p9)") : null;
                _hElig = model == null ? "BRAK (model armii nieznany)" : Patch(m, model.GetType().FullName + ".CheckPartyEligibility", me, nameof(EligPostfix), Priority.Last)
                         + (m != null ? " (" + m.DeclaringType.Name + ")" : "");
            }
            catch (Exception e) { _hElig = "wyjatek " + e.GetType().Name; Stumble("SessionStart.Elig", e); }
            return "najpierw dozbroic: mysl AI (CampaignEventDispatcher.AiHourlyTick, postfiks VeryLow) " + _hThink + ", wlasciciele latek celu: " + Owners(_thinkM)
                   + "; atak (DefaultMobilePartyAIModel.ShouldConsiderAttacking) " + _hAttack
                   + "; armia gry (DefaultArmyManagementCalculationModel.CanLordCreateArmy) " + _hArmy + ", filtr kandydatow przed progiem 1000 (IsPartyWaitingForDisband) " + _hPre
                   + "; armia BK (BKArmyManagementModel.CanLordCreateArmy) " + _hArmyBk
                   + "; wezwania BK (MobilePartyExtensions.IsAvailableForArmies) " + _hAvail + "; okno armii gracza (CheckPartyEligibility) " + _hElig
                   + "; Mur (ROT IsBeyondTheWall) " + (Beyond() != null ? "TAK" : "BRAK");
        }

        /// <summary>Czesc linii doby "Gotowosc do bitwy (p9)" (ArmedStrength.Daily).</summary>
        internal static string DayText()
        {
            int a, aCalls, lead, mem, avail, elig, pre, thr;
            lock (Gate) { a = _vAttack.Count; aCalls = _vAttackCalls; lead = _vLead.Count; mem = _vMember.Count; avail = _vAvail.Count; elig = _vElig.Count; pre = _vPre.Count; thr = _vThresh; }
            int streak = 0;
            foreach (var kv in _noGain) if (kv.Value.N >= NoGainMax) streak++;
            int onWay = 0, there = 0;
            foreach (var t in _trip.Values) { if (t.ArriveH >= 0) there++; else onWay++; }
            return "jada po sprzet: nowe wyjazdy " + _dTrips + ", teraz w drodze " + onWay + " i na miejscu " + there + ", dojechaly " + _dArrived + ", dozbrojone " + _dArmed
                   + ", postoj przedluzony (postep i dalej stac) " + _dExt + ", po wizycie czesciowo " + _dPart + " i bez poprawy " + _dNoGain + ", za dlugo w drodze " + _dTimeout
                   + ", bez miasta w zasiegu " + _dNoTown.Count + " partii, w miescie nie stac (rezerwa napraw z materialem) " + _dNoBudgetTown.Count
                   + ", po " + NoGainMax + " wizytach bez postepu bez wyjazdow " + streak + " partii (mysli " + _dNoBuy.Count + "), wyjazd przerwany zwolnieniem z czekania " + _dFreedTrip
                   + ", cel zablokowany cudza latka (wynik <= -1, wytchnienie i nastepne miasto) " + _dBlocked
                   + ", w miescie nie stac na pelne, stac na pierwsza bron (wyjazd) " + _dFirstInTown
                   + ", wyjazd po pierwsza bron czeka na obrone osady krolestwa (mysli) " + _dCapRelief
                   + ", w wytchnieniu " + _cool.Count + " par partia-miasto"
                   + " | odmowy: atak " + a + " partii (" + aCalls + " razy), opcje bitwy zgaszone " + _dOff + " (mysli bez pieniedzy " + _dPoor.Count + " partii)"
                   + ", obrona wlasnej osady zostawiona " + _dOwnDef + ", glod (bez zmian) " + _dStarve
                   + ", tworzenie armii " + lead + " partii, kandydaci armii przed progiem gry " + pre + " i po nim " + mem + " (armia ponizej progu 1000 po odjeciu " + thr + ")"
                   + ", wezwania BK " + avail + ", okno armii gracza " + elig
                   + " | potkniecia ArmsFirst " + _stumbles;
        }
    }
}
