using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// L5b-2 - KARAWANY WROGA W ZDOBYTYM MIESCIE (decyzja Jeffa 13.10 "5 tak, i tak": docs/lup-miast/LUP-ZDOBYTEGO-MIASTA-2026-10-13.md rozdz. 8 pyt. 5b,
    /// rozdz. 2 wiersz 9). Wylacznik EnemyCaravansSeized (wylaczony = stan a913a58: karawany wyjezdzaja jak w grze; linia pomiaru zostaje).
    ///
    /// GRA (dekompilacja 1.4.8): karawany nie bronia murow (Town.GetDefenderParties pomija IsCaravan), a przy zmianie pana PartyDiplomaticHandlerCampaignBehavior.
    /// OnSettlementOwnerChanged -> CheckSettlementSuitabilityForParties wyprowadza z osady kazda partie w wojnie z nowym panem (LeaveSettlementAction) - karawana
    /// wroga wyjezdza z calym towarem i kiesa. Dzieje sie to WEWNATRZ KingdomManager.SiegeCompleted (ApplyBySiege -> zdarzenie), wiec spis karawan bierzemy w
    /// prefiksie tej metody (ten sam warunek zdobycia co gra: szturm, wypad, wypad z blokady i wygrana), a zajecie robi postfiks - po zmianie pana i wejsciu
    /// oblegajacych do osady, gdy stan gry jest juz spojny.
    ///
    /// KTO: karawana (CaravanPartyComponent: kupcow, lordow BK, konwoje DTE, karawany gracza), ktora w chwili zdobycia stoi w osadzie (CurrentSettlement == osada)
    /// i ktorej frakcja jest w wojnie ze zdobywca PO zmianie pana - dokladnie te, ktore gra wyprowadza (obce karawany wroga: kupcow innych osad, lordow BK, gracza).
    /// Karawana kupca zdobytej osady albo jej wsi (MobileParty.MapFaction notabla = frakcja jego osady) staje sie poddana zdobywcy i zostaje - jej kupcy placa
    /// przez lup osady (L2) wedlug wyboru zdobywcy, laska zostaje laska. Pominiete: karawany ze statkami (w porcie odplywaja - zostawiamy grze),
    /// karawana w bitwie, zdobywca Inni (trup monety nie bierze - jak BeatenPurse / CasualtyLoot; karawany wyjezdzaja jak w grze).
    ///
    /// JAK (DECYZJA - pelne uprawnienia: ta sama regula co karawana rozbita w polu, 3.2 "jak rozbije karawane, to zabiera wszystko zwyciezca" - decyzja Jeffa 09.10
    /// Q3b; karawana to partia, nie moneta miasta - podzial "prawa trzecich" lupu miasta L2 jej nie dotyczy, a L2 jest w innej galezi):
    ///  1. przewodnik i kazdy bohater karawany -> niewola zdobywcy (TakePrisonerAction - jak wrog zlapany w zdobytych murach);
    ///  2. kiesa karawany (PartyTradeGold) -> wodz zdobywcow (GiveGoldAction partia -> bohater, zdarzenie gry - ksiega widzi przelew), korona 1/9 od wodza-wasala
    ///     w wojnie (Ransom178.NoteBeatenGold - ta sama podstawa co 3.2); wodza brak - kiesa jego druzyny (partia z kiesa) albo kasa zdobytej osady;
    ///  3. caly ladunek (towar, zywnosc, konie, juczne, bron) -> tabor druzyny zdobywcy (sztuka zdjeta = sztuka dopisana; juczne niosa to, co niosly) - po audycie
    ///     integracji 13/14.10 miara L7: zwierzeta do granicy stada bez kary "Herding", reszta najcenniejsze na kg do wolnego udzwigu; nadmiar na polke zdobytej osady. AI sprzedaje
    ///     to w nastepnym miescie droga gry (PartiesSellLootCampaignBehavior - zloto z kasy miasta, korona 1/9 przez Ransom178.LootPostfix), zywnosc zjada;
    ///  4. pusta karawana -> DestroyPartyAction.Apply(null, karawana): CZYNNE reguly partii rozwiazanej bez bitwy - eskorta do domu (PeopleBook.ArmsPartyGone, E4/8;
    ///     warunek: bez niszczyciela, bez bitwy i oblezenia - dlatego null), sakiewki eskorty do kasy najblizszego miasta (MenPurse, wpis 89). Kiesa (0) i dobytek
    ///     (pusty) - nic do przekazania. Perk Insurance Plans: u AI ucina SmallGold, u gracza placi gra (jego ubezpieczenie).
    /// Gracz: jego karawana zajeta - wiadomosc po angielsku (strata, przewodnik w niewoli); gracz zdobywca - wiadomosc, co wzial.
    ///
    /// OBIEG ZAMKNIETY: zloto partia -> bohater (swiat bez zmian), sztuki roster -> roster (ksiega towarow: przewoz, zero), ludzie do DOM (ksiega ludzi), bohaterowie
    /// do niewoli. Kontrola w linii: zdjete z karawan = dopisane (zloto i sztuki), karawany, ktore nie zniknely - 0.
    /// Linia "Karawany w zdobytym miescie (L5b-2): dzien" przy kazdym zdobyciu twierdzy (takze bez karawan - pomiar "ile karawan stoi w murach"). Stan tylko w pamieci.
    ///
    /// DIAGNOZA (drobne14, inwentarz A11 / BD 7 "L5b-2 karawany wroga 0 w 39 zdobyciach"): bieg dwuletni - 39 linii, w kazdej "karawan w murach 0" (takze
    /// niewrogich), choc karawany STOJA w oblezonych miastach (linia E8/G1 "w oblezonym miescie": 1 744 tikow, ok. 76 karawano-dob; gra i BK trzymaja je
    /// SetMoveModeHold od OnSiegeEventStarted) i w dobie zdobycia z miast wyjezdzaja karawany po 3-7 i 7+ dobach postoju (Harrenhal d108964: 4, Maidenpool
    /// d109284: 2; linia "Postoj karawan (E8/P3)"). Kod zajecia jest poprawny (warunek zdobycia jak w grze, prefiks biegnie - 39 linii), ale w chwili
    /// prefiksu KingdomManager.SiegeCompleted karawan juz nie ma w osadzie - wychodza wczesniej, a dekompilacja gry 1.4.8 nie pokazuje, kto je wyprowadza
    /// (MapEvent.AddInsideSettlementParties i PartyDiplomaticHandler pomijaja karawany tej frakcji). Dlatego tylko pomiar, bez zmiany zachowania:
    ///  - spis karawan w murach przy OnSiegeEventStarted (pamiec, czyszczona przy OnSiegeEventEnded i wczytaniu) - w linii L5b-2: ile ich bylo na poczatku
    ///    oblezenia, ile jest jeszcze w murach, ile stoi poza murami (pierwsza: gdzie i jak daleko), ile zniknelo;
    ///  - wyjazd karawany z OBLEGANEJ osady (OnSettlementLeftEvent przy IsUnderSiege) - licznik i pierwsze 5 na sesje z kim, skad i stosem wywolan
    ///    (linia "Karawana wyszla z oblezonej osady (L5b-2, diagnoza)") - stos wskaze sciezke, ktora je wyprowadza. Naprawa po pierwszym biegu z ta linia.
    ///
    /// A11 (naprawa, klucz EnemyCaravansSiegeTrapped; off = 449d453): kto wyprowadza karawany - dalej nieznane (stos z linii diagnozy pokaze nastepny bieg),
    /// ale FAKT jest z logu: karawany staja w oblezonym miescie na doby (gra i BK - SetMoveModeHold), w dobie zdobycia z niego wychodza, a przy zdobyciu w murach
    /// nie ma zadnej (39 z 39). Regula: karawana, ktora wyszla z murow OBLEZONEJ twierdzy (OnSettlementLeftEvent przy IsUnderSiege - pamiec: osada, godzina,
    /// bitwa osady) najwyzej TrappedHours (24 h) przed zdobyciem tej twierdzy, jest czynna, nie stoi w innej osadzie, nie jest w bitwie, jej frakcja jest w wojnie ze
    /// zdobywca i nie ma statkow - jest zajeta jak stojaca w murach (ta sama droga Seize: kiesa, ladunek, konie, przewodnik, eskorta - jawnie w linii). Pamiec
    /// wyjsc: czyszczona przy wczytaniu i przy koncu oblezenia tej osady (odstapienie - karawany sa wolne; zdobycie - po prefiksie). Starsze wyjscia, karawany w
    /// innej osadzie, w bitwie, niewrogie, ze statkami i znikniete - tylko liczone w linii (pierwsza: godziny przed zdobyciem, odleglosc od murow, zachowanie AI).
    /// Recenzja drobne2: ta regula domyslnie WYLACZONA (EnemyCaravansSiegeTrapped = false) - przyczyna jest w kodzie BK (A11b nizej), a po naprawie u zrodla
    /// regula wzielaby tylko karawany wyprowadzone inna, nieznana droga.
    ///
    /// A11b (recenzja drobne2, klucz SiegeCaravansStayInWalls; off = 449d453) - PRZYCZYNA z kodu BK (dekompilacja bkfull, BKCaravansBehavior):
    /// OnSiegeEventStarted dopisuje karawany oblezanej osady do JEDNEGO zbioru _siegeHeldCaravans (SetMoveModeHold), a OnSiegeEventEnded - koniec DOWOLNEGO
    /// oblezenia na mapie (odstapienie albo zdobycie) - zwalnia caly zbior: ReleaseCaravanFromHold -> SetMoveGoToSettlement do miasta bez oblezenia; gra wyprowadza
    /// karawane w najblizszym ticku (CampaignTickCacheDataStore.Tick -> LeaveSettlementAction; MobileParty.CheckExitingSettlementParallel nie patrzy na oblezenie),
    /// takze z murow osady oblezonej dalej. Nic innego karawany ladowej z oblezonego miasta nie rusza: gra (CaravansCampaignBehavior.HourlyTickParty) i BK
    /// (HourlyTickPartyImpl, OnGameLoaded) pomijaja oblezone miasto, szturm jej nie wyprowadza (Town.GetDefenderParties pomija IsCaravan), MaterialOrders tez nie.
    /// Regula: przy koncu oblezenia S karawana ze zbioru BK stojaca w murach INNEJ, dalej oblezonej osady zostaje w zbiorze i w murach (prefiks wyjmuje ja na
    /// czas petli BK, postfiks oddaje) - BK zwolni ja przy koncu jej wlasnego oblezenia; przy zdobyciu stoi w murach i bierze ja regula L5b-2 (SiegePre/Seize).
    /// Linia "Karawany BK przy koncu oblezenia (A11b)" przy kazdym koncu oblezenia, w ktorym BK zwolnilby karawane z murow innej oblezonej osady (takze przy
    /// kluczu wylaczonym - "W CIENIU": to jest dowod przyczyny w logu).
    /// </summary>
    internal static class EnemyCaravans
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        internal static bool On { get { var s = Settings.Current; return s != null && s.EnemyCaravansSeized; } }

        // spis z prefiksu (jedno zdobycie naraz - SiegeCompleted nie jest zagniezdzone)
        private sealed class Snap
        {
            public Settlement St; public MobileParty Cap; public IFaction WinF;
            public readonly List<MobileParty> Enemy = new List<MobileParty>();
            public int All, Naval, Friendly, InBattle;
            // diagnoza drobne14: spis z poczatku oblezenia (-1 - brak spisu: oblezenie sprzed wczytania)
            public int AtStart = -1, StillIn, Outside, Gone;
            public string OutEx;
            // A11: karawany wyprowadzone z murow w ostatniej dobie oblezenia (kandydaci do zajecia) i reszta wyjsc z tej osady (tylko licznik)
            public readonly List<MobileParty> Trapped = new List<MobileParty>();
            public int TrExits, TrOld, TrOther, TrBattle, TrFriendly, TrNaval, TrGone;
            public string TrEx;
        }

        // A11: wyjscie karawany z murow oblezonej osady - osada, godzina kampanii, bitwa osady w tej chwili
        private sealed class Exit { public Settlement St; public double Hour; public bool Battle; }
        private static readonly Dictionary<MobileParty, Exit> _leftAt = new Dictionary<MobileParty, Exit>();
        internal const double TrappedHours = 24.0;   // A11: okno "ostatniej doby oblezenia" (szturm; Maidenpool d109284 - karawany wyszly ok. 10 h przed zdobyciem)
        internal static bool TrappedOn { get { var s = Settings.Current; return s != null && s.EnemyCaravansSiegeTrapped; } }

        // A11b (recenzja drobne2): zbior BK _siegeHeldCaravans (pole instancji BKCaravansBehavior; null - BK bez tego pola, latka nie wpieta); liczniki od wczytania
        private static System.Reflection.FieldInfo _bkHeld;
        private static int _bkLines, _bkKept, _bkKeptFoe, _bkShadow;
        internal static bool StayOn { get { var s = Settings.Current; return s != null && s.SiegeCaravansStayInWalls; } }

        // diagnoza drobne14: karawany w murach przy OnSiegeEventStarted (osada -> lista); wyjazdy karawan z oblezanych osad
        private static readonly Dictionary<Settlement, List<MobileParty>> _atStart = new Dictionary<Settlement, List<MobileParty>>();
        private static int _leftSiege, _dLeftSiege, _stackLines;
        private const int StackLinesMax = 5;

        // liczniki doby (Last* dla "Obieg: notable i karawany")
        private static int _dCaps, _dSeized;
        private static long _dGold, _dUnits;
        internal static int LastCaps, LastSeized;
        internal static long LastGold, LastUnits;
        private static int _stumbles, _dStumbles;
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static bool _hookTried;

        internal static void Reset()
        {
            _dCaps = _dSeized = 0; _dGold = _dUnits = 0; LastCaps = LastSeized = 0; LastGold = LastUnits = 0;
            _stumbles = _dStumbles = 0; _err.Clear();
            _atStart.Clear(); _leftSiege = _dLeftSiege = _stackLines = 0;
            _leftAt.Clear();
            _bkLines = _bkKept = _bkKeptFoe = _bkShadow = 0;
        }

        // ------------------------------------------------------------ diagnoza drobne14 (tylko pamiec i log - zachowanie gry bez zmian)

        /// <summary>Sluchacz OnSiegeEventStartedEvent: spis karawan w murach oblezanej twierdzy.</summary>
        internal static void OnSiegeStarted(SiegeEvent se)
        {
            try
            {
                var st = se != null ? se.BesiegedSettlement : null;
                if (st == null || !st.IsFortification) return;
                List<MobileParty> l = null;
                foreach (var mp in st.Parties)
                    if (mp != null && mp.IsActive && mp.IsCaravan) { if (l == null) l = new List<MobileParty>(); l.Add(mp); }
                _atStart[st] = l ?? new List<MobileParty>();
            }
            catch (Exception e) { Stumble("OnSiegeStarted", e); }
        }

        /// <summary>Sluchacz OnSiegeEventEndedEvent: koniec oblezenia (zdobycie - po linii L5b-2, odstapienie) - spis znika.</summary>
        internal static void OnSiegeEnded(SiegeEvent se)
        {
            try
            {
                if (se != null && se.BesiegedSettlement != null)
                {
                    var st = se.BesiegedSettlement;
                    _atStart.Remove(st);
                    // A11: koniec oblezenia tej osady - wyjscia z jej murow zapomniane (odstapienie: karawany wolne; zdobycie: prefiks juz je przeczytal)
                    if (_leftAt.Count > 0)
                    {
                        List<MobileParty> drop = null;
                        foreach (var kv in _leftAt) if (kv.Value == null || kv.Value.St == st || kv.Key == null || !kv.Key.IsActive) { if (drop == null) drop = new List<MobileParty>(); drop.Add(kv.Key); }
                        if (drop != null) foreach (var k in drop) _leftAt.Remove(k);
                    }
                }
            }
            catch (Exception e) { Stumble("OnSiegeEnded", e); }
        }

        /// <summary>Sluchacz OnSettlementLeftEvent: karawana wyszla z osady, ktora jest jeszcze oblegana (gra i BK trzymaja je w murach) - licznik i stos.</summary>
        internal static void OnLeft(MobileParty mp, Settlement st)
        {
            if (mp == null || st == null || !mp.IsCaravan || !st.IsUnderSiege) return;
            try
            {
                _leftSiege++; _dLeftSiege++;
                // A11: pamiec wyjscia (zawsze - takze przy wylaczonym kluczu: linia pokaze, co regula by zajela)
                try { _leftAt[mp] = new Exit { St = st, Hour = CampaignTime.Now.ToHours, Battle = st.Party != null && st.Party.MapEvent != null }; }
                catch (Exception e) { Stumble("OnLeft(pamiec)", e); }
                if (_stackLines >= StackLinesMax) return;
                _stackLines++;
                IFaction bf = null;
                try { bf = st.SiegeEvent != null && st.SiegeEvent.BesiegerCamp != null ? st.SiegeEvent.BesiegerCamp.MapFaction : null; } catch { bf = null; }
                bool inStart = false;
                List<MobileParty> l;
                if (_atStart.TryGetValue(st, out l) && l != null) inStart = l.Contains(mp);
                Log.Info("Karawana wyszla z oblezonej osady (L5b-2, diagnoza): dzien " + (int)CampaignTime.Now.ToDays + " godz. " + CampaignTime.Now.GetHourOfDay
                         + " | karawana " + mp.StringId + " (" + Nm(mp.MapFaction) + ", wlasciciel " + Nm(mp.Owner) + ")"
                         + " | osada " + Nm(st) + " (" + st.StringId + ", " + Nm(st.MapFaction) + "; oblega " + Nm(bf) + (bf != null && mp.MapFaction != null ? (bf.IsAtWarWith(mp.MapFaction) ? " - wrog karawany" : " - nie wrog karawany") : "") + ")"
                         + " | bitwa osady " + (st.Party != null && st.Party.MapEvent != null ? "TAK" : "nie") + ", w spisie z poczatku oblezenia " + (inStart ? "TAK" : "nie")
                         + " | wyjazdow z oblezonych od wczytania " + _leftSiege + " (linia " + _stackLines + "/" + StackLinesMax + ")"
                         + " | stos: " + StackText(2) + ".");
            }
            catch (Exception e) { Stumble("OnLeft", e); }
        }

        /// <summary>Stos wywolan do linii diagnozy (skip: 0 - od tej metody; 2 - od wolajacego metode, ktora tu wola). Tylko do rzadkich linii (koszt).</summary>
        internal static string StackText(int skip)
        {
            try
            {
                var frames = new System.Diagnostics.StackTrace(Math.Max(0, skip), false).GetFrames();
                if (frames == null) return "-";
                var sb = new StringBuilder(400);
                int n = 0;
                foreach (var f in frames)
                {
                    var m = f != null ? f.GetMethod() : null;
                    if (m == null) continue;
                    if (n > 0) sb.Append(" <- ");
                    sb.Append(m.DeclaringType != null ? m.DeclaringType.Name : "?").Append('.').Append(m.Name);
                    if (++n >= 12) break;
                }
                return n > 0 ? sb.ToString() : "-";
            }
            catch { return "?"; }
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _dStumbles++;
            try { if (_err.Add(where)) Log.Error("EnemyCaravans." + where, e); } catch { }
        }

        private static string Nm(IFaction f) { try { return f != null && f.Name != null ? f.Name.ToString() : "-"; } catch { return "?"; } }
        private static string Nm(Settlement s) { try { return s != null && s.Name != null ? s.Name.ToString() : "-"; } catch { return "?"; } }
        private static string Nm(Hero h) { try { return h != null && h.Name != null ? h.Name.ToString() : "-"; } catch { return "?"; } }

        /// <summary>Seal31.EnsureHooks (w kampanii, raz na proces): prefiks (spis) i postfiks (zajecie) na KingdomManager.SiegeCompleted.</summary>
        internal static void EnsureHooks()
        {
            if (_hookTried) return;
            _hookTried = true;
            try
            {
                Seal31.Patch("L5b-2: karawany wroga w zdobytym miescie (KingdomManager.SiegeCompleted)", AccessTools.Method(typeof(KingdomManager), "SiegeCompleted"),
                             typeof(EnemyCaravans), nameof(SiegePre), nameof(SiegePost), null);
            }
            catch (Exception e) { Stumble("EnsureHooks", e); }
            // A11b: BK BKCaravansBehavior.OnSiegeEventEnded (prywatna, sluchacz OnSiegeEventEndedEvent) - latka na cialo metody dziala tez dla delegata BK
            try
            {
                var bkc = AccessTools.TypeByName("BannerKings.Behaviours.BKCaravansBehavior");
                var f = bkc != null ? AccessTools.Field(bkc, "_siegeHeldCaravans") : null;
                _bkHeld = f != null && f.FieldType == typeof(HashSet<MobileParty>) ? f : null;
                var m = bkc != null && _bkHeld != null ? AccessTools.Method(bkc, "OnSiegeEventEnded", new[] { typeof(SiegeEvent) }) : null;
                Seal31.Patch("A11b: karawany BK zostaja w murach oblezonych osad do konca wlasnego oblezenia (BKCaravansBehavior.OnSiegeEventEnded)", m,
                             typeof(EnemyCaravans), nameof(BkSiegeEndPre), nameof(BkSiegeEndPost), null);
            }
            catch (Exception e) { Stumble("EnsureHooks(A11b)", e); }
        }

        /// <summary>A11b: prefiks BK BKCaravansBehavior.OnSiegeEventEnded (koniec dowolnego oblezenia - BK zwalnia caly zbior). Karawany ze zbioru stojace w murach
        /// INNEJ, dalej oblezonej osady: przy kluczu wyjete ze zbioru na czas petli BK (postfiks je oddaje), bez klucza tylko liczone. Linia, gdy jest choc jedna.</summary>
        public static void BkSiegeEndPre(object __instance, SiegeEvent __0, out List<MobileParty> __state)
        {
            __state = null;
            try
            {
                var se = __0;
                if (__instance == null || se == null || _bkHeld == null) return;
                var set = _bkHeld.GetValue(__instance) as HashSet<MobileParty>;
                if (set == null || set.Count == 0) return;
                var own = se.BesiegedSettlement;
                int all = set.Count, here = 0, loose = 0, foe = 0;
                List<MobileParty> other = null;
                string ex = null;
                foreach (var c in set)
                {
                    var cs = c != null && c.IsActive ? c.CurrentSettlement : null;
                    if (cs != null && cs == own) { here++; continue; }               // osada konczonego oblezenia - BK zwalnia jak dotad (zdobycie: L5b-2 juz je spisal)
                    var ose = cs != null ? cs.SiegeEvent : null;
                    if (ose == null || ose == se) { loose++; continue; }             // poza murami albo w osadzie bez oblezenia - BK zwalnia jak dotad
                    if (other == null) other = new List<MobileParty>();
                    other.Add(c);
                    IFaction bf = null;
                    try { bf = ose.BesiegerCamp != null ? ose.BesiegerCamp.MapFaction : null; } catch { bf = null; }
                    bool f = bf != null && c.MapFaction != null && bf.IsAtWarWith(c.MapFaction);
                    if (f) foe++;
                    if (ex == null) ex = c.StringId + " w " + Nm(cs) + " (" + cs.StringId + ", oblega " + Nm(bf) + (f ? " - wrog karawany" : " - nie wrog karawany") + ")";
                }
                if (other == null) return;
                bool on = StayOn;
                if (on)
                {
                    foreach (var c in other) set.Remove(c);
                    __state = other;
                    _bkKept += other.Count; _bkKeptFoe += foe;
                }
                else _bkShadow += other.Count;
                _bkLines++;
                Log.Info("Karawany BK przy koncu oblezenia (A11b): dzien " + (int)CampaignTime.Now.ToDays + " godz. " + CampaignTime.Now.GetHourOfDay
                         + " | koniec oblezenia " + Nm(own) + " (" + (own != null ? own.StringId : "-") + ")"
                         + " | zbior BK " + all + ": w murach tej osady " + here + ", poza murami oblezonych osad " + loose + ", w murach INNYCH oblezonych osad " + other.Count
                         + " (wrogowie oblegajacych " + foe + "; pierwsza: " + ex + ")"
                         + " | " + (on ? "zostaja w murach do konca wlasnego oblezenia (Siege Caravans Stay In Walls)"
                                       : "W CIENIU (klucz wylaczony): BK wyprowadza je z murow oblezonych osad - jak 449d453")
                         + " | od wczytania: linii " + _bkLines + ", zostawionych " + _bkKept + " (wrogowie oblegajacych " + _bkKeptFoe + "), w cieniu wyprowadzonych " + _bkShadow + ".");
            }
            catch (Exception e) { Stumble("BkSiegeEndPre", e); }
        }

        /// <summary>A11b: postfiks - karawany wyjete przez prefiks wracaja do zbioru BK (BK zwolni je przy koncu ich wlasnego oblezenia). Wyjatek w petli BK =
        /// bez postfiksu: karawana zostaje na postoju w murach, a po koncu jej oblezenia zwalnia ja tick BK (Hold poza oblezona osada) - nic nie ginie.</summary>
        public static void BkSiegeEndPost(object __instance, List<MobileParty> __state)
        {
            if (__state == null || __state.Count == 0) return;
            try
            {
                var set = __instance != null && _bkHeld != null ? _bkHeld.GetValue(__instance) as HashSet<MobileParty> : null;
                if (set != null) foreach (var c in __state) if (c != null) set.Add(c);
            }
            catch (Exception e) { Stumble("BkSiegeEndPost", e); }
        }

        /// <summary>Prefiks: spis karawan w osadzie przed zmiana pana (potem gra je wyprowadza). __state - spis tego wywolania (null - bez zdobycia).</summary>
        public static void SiegePre(Settlement settlement, MobileParty capturerParty, bool isWin, MapEvent.BattleTypes battleType, out object __state)
        {
            __state = null;
            try
            {
                bool capture = isWin && (battleType == MapEvent.BattleTypes.Siege || battleType == MapEvent.BattleTypes.SallyOut
                                         || battleType == MapEvent.BattleTypes.BlockadeSallyOutBattle);
                if (!capture || settlement == null || capturerParty == null || !settlement.IsFortification) return;
                var sn = new Snap { St = settlement, Cap = capturerParty, WinF = capturerParty.MapFaction };
                var list = new List<MobileParty>(settlement.Parties);
                foreach (var mp in list)
                {
                    try
                    {
                        if (mp == null || !mp.IsActive || !mp.IsCaravan) continue;
                        sn.All++;
                        var f = mp.MapFaction;
                        if (f == null || sn.WinF == null || !f.IsAtWarWith(sn.WinF)) { sn.Friendly++; continue; }
                        if (mp.Ships != null && mp.Ships.Count > 0) { sn.Naval++; continue; }   // w porcie odplywa - gra
                        if (mp.MapEvent != null) { sn.InBattle++; continue; }
                        sn.Enemy.Add(mp);
                    }
                    catch (Exception e) { Stumble("SiegePre(karawana)", e); }
                }
                // diagnoza drobne14: gdzie sa karawany, ktore staly w murach na poczatku oblezenia
                try
                {
                    List<MobileParty> l0;
                    if (_atStart.TryGetValue(settlement, out l0) && l0 != null)
                    {
                        sn.AtStart = l0.Count;
                        foreach (var c in l0)
                        {
                            if (c == null || !c.IsActive) { sn.Gone++; continue; }
                            if (c.CurrentSettlement == settlement) { sn.StillIn++; continue; }
                            sn.Outside++;
                            if (sn.OutEx == null)
                                sn.OutEx = c.StringId + (c.CurrentSettlement != null ? " w " + Nm(c.CurrentSettlement)
                                                         : " na mapie " + c.GetPosition2D.Distance(settlement.GetPosition2D).ToString("0.0", Inv) + " jedn. od murow")
                                           + (c.MapEvent != null ? ", w bitwie" : "") + ", " + c.DefaultBehavior + "/" + c.ShortTermBehavior;
                        }
                    }
                }
                catch (Exception e) { Stumble("SiegePre(spis z poczatku)", e); }
                // A11: karawany wyprowadzone z murow tej osady w trakcie oblezenia - w ostatniej dobie, poza osadami, wrogowie zdobywcy: do zajecia
                try
                {
                    double now = CampaignTime.Now.ToHours;
                    foreach (var kv in _leftAt)
                    {
                        var c = kv.Key; var x = kv.Value;
                        if (x == null || x.St != settlement) continue;
                        sn.TrExits++;
                        if (c == null || !c.IsActive) { sn.TrGone++; continue; }
                        if (c.CurrentSettlement == settlement) continue;                  // wrocila do murow - jest juz w spisie wyzej
                        double ago = now - x.Hour;
                        if (ago > TrappedHours) { sn.TrOld++; continue; }
                        if (c.CurrentSettlement != null) { sn.TrOther++; continue; }      // dotarla do innej osady - uszla
                        if (c.MapEvent != null) { sn.TrBattle++; continue; }               // w bitwie - rozstrzyga gra
                        var f = c.MapFaction;
                        if (f == null || sn.WinF == null || !f.IsAtWarWith(sn.WinF)) { sn.TrFriendly++; continue; }
                        if (c.Ships != null && c.Ships.Count > 0) { sn.TrNaval++; continue; }
                        sn.Trapped.Add(c);
                        if (sn.TrEx == null)
                            sn.TrEx = c.StringId + " " + ago.ToString("0.0", Inv) + " h przed zdobyciem, " + c.GetPosition2D.Distance(settlement.GetPosition2D).ToString("0.0", Inv)
                                      + " jedn. od murow, bitwa osady przy wyjsciu " + (x.Battle ? "TAK" : "nie") + ", " + c.DefaultBehavior + "/" + c.ShortTermBehavior;
                    }
                }
                catch (Exception e) { Stumble("SiegePre(wyprowadzone)", e); }
                __state = sn;
            }
            catch (Exception e) { Stumble("SiegePre", e); }
        }

        /// <summary>Postfiks: zdobycie potwierdzone (osada ma frakcje zdobywcy) - zajecie karawan ze spisu, linia, wiadomosci gracza.</summary>
        public static void SiegePost(object __state)
        {
            var sn = __state as Snap;
            if (sn == null) return;
            try { Seize(sn); }
            catch (Exception e) { Stumble("SiegePost", e); }
        }

        private static void Seize(Snap sn)
        {
            var s = Settings.Current;
            int day = (int)CampaignTime.Now.ToDays;
            var st = sn.St; var cap = sn.Cap;
            bool taken = st.MapFaction != null && sn.WinF != null && ReferenceEquals(st.MapFaction, sn.WinF);
            bool undead = false;
            try { undead = Undead.Party(cap); } catch { undead = false; }
            _dCaps++;
            Hero lead = cap != null && cap.IsActive ? cap.LeaderHero : null;
            int seized = 0, survived = 0, skipped = 0, subjects = 0, heroes = 0, men = 0, horses = 0, playerN = 0;
            long goldOff = 0, goldOn = 0, unitsOff = 0, unitsOn = 0, unitsShelf = 0, value = 0, menPurse = 0, playerGold = 0, playerUnits = 0;
            string why = null;
            var ex = new List<string>();
            var playerLeaders = new List<string>();
            if (!taken) why = "osada nie zmienila frakcji - bez zajecia";
            else if (undead) why = "zdobywca Inni - karawany wyjezdzaja jak w grze";
            else if (cap == null || !cap.IsActive) why = "druzyna zdobywcy nieczynna - karawany wyjezdzaja jak w grze";
            else if (!On) why = "W CIENIU (klucz wylaczony) - karawany wyjezdzaja jak w grze";
            // A11: kandydaci z murow (Enemy) i wyprowadzeni w ostatniej dobie oblezenia (Trapped, tylko przy EnemyCaravansSiegeTrapped) - ta sama regula zajecia
            bool trOn = TrappedOn;
            int trSeized = 0, trSkipped = 0;
            var all = new List<MobileParty>(sn.Enemy);
            int nIn = all.Count;
            all.AddRange(sn.Trapped);
            for (int ci = 0; ci < all.Count; ci++)
            {
                var c = all[ci];
                bool trapped = ci >= nIn;
                if (trapped && !trOn) { trSkipped++; continue; }                    // klucz A11 wylaczony: jak 449d453 (tylko licznik)
                if (why != null) { skipped++; if (trapped) trSkipped++; continue; }
                try
                {
                    if (c == null || !c.IsActive || c.MapEvent != null) { skipped++; if (trapped) trSkipped++; continue; }
                    // po zmianie pana: karawana kupca tej osady (i jej wsi) ma frakcje osady - jest juz poddana zdobywcy i zostaje (gra tez jej nie wyprowadza);
                    // jej pan i jej kupcy placa przez lup osady (L2) wedlug wyboru zdobywcy - laska zostaje laska
                    var fNow = c.MapFaction;
                    if (fNow == null || !fNow.IsAtWarWith(sn.WinF)) { subjects++; continue; }
                    bool player = c.ActualClan == Clan.PlayerClan || (c.Owner != null && c.Owner.Clan == Clan.PlayerClan);
                    string owner = Nm(c.Owner ?? (c.Party != null ? c.Party.Owner : null));
                    // 1. bohaterowie -> niewola zdobywcy
                    var hs = new List<Hero>();
                    var mr = c.MemberRoster;
                    if (mr != null)
                        for (int i = 0; i < mr.Count; i++)
                        {
                            var ch = mr.GetCharacterAtIndex(i);
                            if (ch != null && ch.IsHero && ch.HeroObject != null && ch.HeroObject.IsAlive && ch.HeroObject != Hero.MainHero) hs.Add(ch.HeroObject);
                        }
                    foreach (var h in hs)
                    {
                        try { TakePrisonerAction.Apply(cap.Party, h); heroes++; if (player) playerLeaders.Add(Nm(h)); }
                        catch (Exception e) { Stumble("Seize(niewola)", e); }
                    }
                    // 2. kiesa -> wodz zdobywcow (regula 3.2), korona 1/9 od wodza-wasala
                    int g = c.PartyTradeGold, gotG = 0;
                    if (g > 0)
                    {
                        goldOff += g;
                        if (lead != null && lead.IsAlive)
                        {
                            int h0 = lead.Gold;
                            GiveGoldAction.ApplyForPartyToCharacter(c.Party, lead, g, true);
                            gotG = lead.Gold - h0;
                            Ransom178.NoteBeatenGold(lead, gotG);
                        }
                        else if (cap.IsPartyTradeActive)
                        {
                            int p0 = cap.PartyTradeGold;
                            GiveGoldAction.ApplyForPartyToParty(c.Party, cap.Party, g, true);
                            gotG = cap.PartyTradeGold - p0;
                        }
                        else if (st.SettlementComponent != null)
                        {
                            int s0 = st.SettlementComponent.Gold;
                            GiveGoldAction.ApplyForPartyToSettlement(c.Party, st, g, true);
                            gotG = st.SettlementComponent.Gold - s0;
                        }
                        goldOn += gotG;
                    }
                    // 3. ladunek -> tabor wodza (sztuka po sztuce: zdjeta z karawany = dopisana wodzowi albo polce osady). Poprawka po audycie (obieg Z5, gracz 1):
                    //    ta sama miara co L7 - zwierzeta (konie, juczne, zywiec) najpierw, do granicy stada bez kary "Herding" gry; reszta najcenniejsze na kg do
                    //    wolnego udzwigu PO zwierzetach (juczne niosa). Czego wodz nie uniesie albo nie poprowadzi - na polke zdobytej osady (nie znika, nie plonie);
                    //    osada bez polki - wszystko do wodza, jak dotad
                    int u = 0, uAll = 0;
                    var pack = c.ItemRoster;
                    var shelf = st.ItemRoster;
                    if (pack != null)
                    {
                        var els = new List<ItemRosterElement>();
                        for (int i = 0; i < pack.Count; i++)
                        {
                            var el = pack.GetElementCopyAtIndex(i);
                            if (el.Amount <= 0 || el.EquipmentElement.Item == null || el.EquipmentElement.IsQuestItem) continue;
                            els.Add(el);
                        }
                        els.Sort((x, y) =>
                        {
                            bool wx = CitySack.Walks7(x.EquipmentElement.Item), wy = CitySack.Walks7(y.EquipmentElement.Item);
                            if (wx != wy) return wx ? -1 : 1;
                            return CitySack.PerKg7(y.EquipmentElement.Item).CompareTo(CitySack.PerKg7(x.EquipmentElement.Item));
                        });
                        int herd = CitySack.HerdRoom(cap);
                        double free = double.NaN;                       // wolny udzwig liczony po zwierzetach
                        foreach (var el in els)
                        {
                            var ee = el.EquipmentElement;
                            int at = pack.FindIndexOfElement(ee);
                            int n = at >= 0 ? pack.GetElementNumber(at) : 0;
                            if (n <= 0) continue;
                            bool walks = CitySack.Walks7(ee.Item);
                            double w = Math.Max(0f, ee.Item.Weight);
                            int fit;
                            if (shelf == null) fit = n;
                            else if (walks) fit = Math.Min(n, Math.Max(0, herd));
                            else
                            {
                                if (double.IsNaN(free)) free = CitySack.FreeKg(cap);
                                fit = w > 1e-6 ? (int)Math.Min(n, Math.Floor(Math.Max(0.0, free) / w)) : n;
                            }
                            int dst0 = CountOf(cap.ItemRoster, ee), sh0 = CountOf(shelf, ee);
                            pack.AddToCounts(ee, -n);
                            int at2 = pack.FindIndexOfElement(ee);
                            int off = n - (at2 >= 0 ? pack.GetElementNumber(at2) : 0);
                            if (off <= 0) continue;
                            int toCap = Math.Min(fit, off), toShelf = off - toCap;
                            if (toCap > 0) cap.ItemRoster.AddToCounts(ee, toCap);
                            if (toShelf > 0) shelf.AddToCounts(ee, toShelf);
                            int on = CountOf(cap.ItemRoster, ee) - dst0;
                            int onShelf = toShelf > 0 ? CountOf(shelf, ee) - sh0 : 0;
                            unitsOff += off; unitsOn += on + onShelf; u += on; uAll += on + onShelf; unitsShelf += onShelf;
                            value += (long)on * Math.Max(0, ee.ItemValue);
                            if (ee.Item.HasHorseComponent) horses += on;
                            if (walks) herd -= on; else if (!double.IsNaN(free)) free -= on * w;
                        }
                    }
                    // 4. pusta karawana - rozwiazana bez niszczyciela (eskorta do domu, sakiewki eskorty do kasy najblizszego miasta)
                    int m = mr != null ? mr.TotalManCount : 0;
                    int mp0 = 0;
                    try { mp0 = MenPurse.Get(c); } catch { mp0 = 0; }
                    if (c.IsActive) DestroyPartyAction.Apply(null, c);   // recenzja: partia usunieta przez cudzy nasluch (np. przy niewoli przewodnika) - bez drugiego zdarzenia zniszczenia
                    if (c.IsActive) { survived++; continue; }
                    seized++; men += m; menPurse += mp0; if (trapped) trSeized++;
                    if (player) { playerN++; playerGold += g; playerUnits += uAll; }
                    if (ex.Count < 4) ex.Add(owner + ": kiesa " + g + ", sztuk " + uAll + (uAll != u ? " (wodzowi " + u + ")" : "") + ", ludzie " + m + (hs.Count > 0 ? ", bohaterow " + hs.Count : ""));
                }
                catch (Exception e) { Stumble("Seize(karawana)", e); survived++; }
            }
            _dSeized += seized; _dGold += goldOn; _dUnits += unitsOn;
            var sb = new StringBuilder(800);
            sb.Append("Karawany w zdobytym miescie (L5b-2): dzien ").Append(day)
              .Append(" | osada ").Append(Nm(st)).Append(" (").Append(st.StringId).Append(") zdobyta przez ").Append(cap != null && cap.Name != null ? cap.Name.ToString() : "-")
              .Append(" (").Append(Nm(sn.WinF)).Append(", wodz ").Append(Nm(lead)).Append(')')
              .Append(" | karawan w murach ").Append(sn.All).Append(": wroga ").Append(sn.Enemy.Count).Append(", niewrogich ").Append(sn.Friendly)
              .Append(", ze statkami (odplywaja) ").Append(sn.Naval).Append(", w bitwie ").Append(sn.InBattle)
              .Append(" | zajete ").Append(seized).Append(", poddani nowego pana (zostaja) ").Append(subjects).Append(", pominiete ").Append(skipped).Append(why != null ? " (" + why + ")" : "")
              .Append(" | kiesy: zdjete ").Append(goldOff).Append(" - dopisane ").Append(goldOn).Append(" = ").Append(goldOff - goldOn)
              .Append(" | ladunek: zdjete ").Append(unitsOff).Append(" - dopisane ").Append(unitsOn).Append(" = ").Append(unitsOff - unitsOn)
              .Append(" szt. (wartosc ").Append(value).Append(", konie i juczne ").Append(horses).Append(')')
              .Append(" | bohaterowie do niewoli ").Append(heroes).Append(", eskorta do domu ").Append(men).Append(" (sakiewki eskorty ").Append(menPurse).Append(" do kasy najblizszego miasta)")
              .Append(" | z dopisanych na polke osady ").Append(unitsShelf).Append(" szt. (ponad udzwig i stado wodza; wartosc i konie wyzej - tylko tabor wodza)")
              .Append(" | nie zniknely ").Append(survived)
              .Append(" | gracz: karawan zajetych ").Append(playerN).Append(cap != null && cap.IsMainParty ? ", zdobywca" : "")
              .Append(ex.Count > 0 ? " | " + string.Join("; ", ex.ToArray()) : "")
              .Append(" | na poczatku oblezenia w murach ").Append(sn.AtStart < 0 ? "- (brak spisu: oblezenie sprzed wczytania)"
                      : sn.AtStart + ": teraz w murach " + sn.StillIn + ", poza murami " + sn.Outside + (sn.OutEx != null ? " (" + sn.OutEx + ")" : "") + ", zniknely " + sn.Gone)
              .Append(", wyjazdy karawan z oblezonych osad dzis ").Append(_dLeftSiege).Append(" (od wczytania ").Append(_leftSiege).Append(')')
              // A11 (dopisek za diagnoza drobne14 - pola wyzej czyta narzedzie): wyprowadzone z murow w ostatniej dobie oblezenia (w "zajete" i kiesach wyzej)
              .Append(" | wyprowadzone z murow (A11, ").Append(trOn ? "czynne" : "W CIENIU - klucz wylaczony").Append("): wyjsc z murow w oblezeniu ").Append(sn.TrExits)
              .Append(", w ostatniej dobie wroga poza osadami ").Append(sn.Trapped.Count).Append(" (zajete ").Append(trSeized).Append(", pominiete ").Append(trSkipped).Append(")")
              .Append(", starsze niz ").Append(((int)TrappedHours).ToString(Inv)).Append(" h ").Append(sn.TrOld).Append(", w innej osadzie ").Append(sn.TrOther)
              .Append(", w bitwie ").Append(sn.TrBattle).Append(", niewrogie ").Append(sn.TrFriendly).Append(", ze statkami ").Append(sn.TrNaval).Append(", zniknely ").Append(sn.TrGone)
              .Append(sn.TrEx != null ? " (pierwsza: " + sn.TrEx + ")" : "")
              // A11b (recenzja drobne2, dopisek za A11): karawany BK zostaja w murach do konca wlasnego oblezenia
              .Append(" | karawany BK w murach do konca oblezenia (A11b): ").Append(StayOn ? "tak" : "nie").Append(", od wczytania zostawionych ").Append(_bkKept)
              .Append(_dStumbles > 0 ? " | potkniecia " + _dStumbles + " (od wczytania " + _stumbles + ")" : "").Append('.');
            Log.Info(sb.ToString());
            try
            {
                if (playerN > 0)
                    Log.Player(Nm(st) + " has fallen to " + Nm(sn.WinF) + ": " + (playerN == 1 ? "your caravan there was" : playerN + " of your caravans there were") + " seized - "
                               + playerGold.ToString("N0", Inv) + " denars and " + playerUnits + " items of cargo and pack animals are lost"
                               + (playerLeaders.Count > 0 ? "; " + string.Join(", ", playerLeaders.ToArray()) + (playerLeaders.Count == 1 ? " is" : " are") + " now held prisoner" : "") + ".", true);
                if (cap != null && cap.IsMainParty && seized > 0)
                    Log.Player("In " + Nm(st) + " your men seized " + (seized == 1 ? "an enemy caravan" : seized + " enemy caravans") + ": " + goldOn.ToString("N0", Inv) + " denars go to your purse"
                               + CitySack.CrownNinthText(lead, goldOn) + " and "
                               + (unitsOn - unitsShelf) + " items of cargo, horses and pack animals to your baggage"
                               + (unitsShelf > 0 ? " (" + unitsShelf + " more that your men could not carry or herd are left " + (st.IsTown ? "in the market of " : "behind in ") + Nm(st) + ")" : "") + (heroes > 0 ? "; " + heroes + (heroes == 1 ? " caravan master is your prisoner" : " caravan masters are your prisoners") : "") + ".");
            }
            catch (Exception e) { Stumble("Seize(wiadomosc)", e); }
        }

        private static int CountOf(ItemRoster r, EquipmentElement ee)
        {
            if (r == null) return 0;
            int at = r.FindIndexOfElement(ee);
            return at >= 0 ? r.GetElementNumber(at) : 0;
        }

        /// <summary>Raz na dobe, przed MoneyLedger.Daily ("Obieg: notable i karawany" czyta Last*) - liczby doby. Nie rusza zlota.</summary>
        internal static void DayRoll()
        {
            LastCaps = _dCaps; LastSeized = _dSeized; LastGold = _dGold; LastUnits = _dUnits;
            _dCaps = _dSeized = 0; _dGold = _dUnits = 0; _dStumbles = 0; _dLeftSiege = 0;
        }
    }
}
